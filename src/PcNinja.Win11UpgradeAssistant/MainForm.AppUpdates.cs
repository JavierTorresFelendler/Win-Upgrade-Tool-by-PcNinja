using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal sealed partial class MainForm
{
    private readonly AppUpdateClient _applicationUpdates = new();
    private readonly LinkLabel _appUpdatesLink = new();
    private CancellationTokenSource? _appUpdateCheck;
    private AppUpdateManifest? _availableAppUpdate;
    private bool _automaticAppUpdateCheck = true;
    private bool _closingForAppUpdate;
    private Func<CancellationToken, Task<AppUpdateCheckResult>>? _checkAppUpdate = null;
    private Func<AppUpdateManifest, IProgress<AppUpdateProgress>, CancellationToken, Task<AppUpdateHandoffResult>>? _installAppUpdate = null;
    private Func<string, bool>? _confirmAppUpdate = null;
    private Action<string>? _showAppUpdateFailure = null;

    private async Task CheckAppUpdatesAsync(bool manual)
    {
        if (_operation is not null || _appUpdateCheck is not null || _setupHasStarted) return;
        using var check = new CancellationTokenSource();
        _appUpdateCheck = check;
        _appUpdatesLink.Enabled = false;
        if (manual) _appUpdatesLink.Text = "Checking app updates...";
        try
        {
            var result = await (_checkAppUpdate?.Invoke(check.Token)
                ?? _applicationUpdates.CheckAsync(ProductIdentity.Version, check.Token));
            if (check.IsCancellationRequested || IsDisposed || _operation is not null) return;
            _availableAppUpdate = result.State == AppUpdateCheckState.Available ? result.Release : null;
            _appUpdatesLink.Text = _availableAppUpdate is not null ? $"Update app to {_availableAppUpdate.Version}"
                : manual ? result.State switch
                {
                    AppUpdateCheckState.UpToDate => "App is up to date",
                    AppUpdateCheckState.NoStableRelease => "No stable app release yet",
                    _ => "App updates unavailable — retry"
                } : "Check app updates";
            _appUpdatesLink.LinkColor = _availableAppUpdate is null ? AccentLight : Success;
            _appUpdatesLink.AccessibleDescription = result.Message;
            AppLog.Info($"Application update check: {result.State}. {result.Message}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLog.Error($"Application update check failed: {ex}");
            if (!IsDisposed && manual) _appUpdatesLink.Text = "App updates unavailable — retry";
        }
        finally
        {
            if (ReferenceEquals(_appUpdateCheck, check)) _appUpdateCheck = null;
            if (!IsDisposed) _appUpdatesLink.Enabled = _operation is null && !_setupHasStarted;
        }
    }

    private async Task InstallAppUpdateAsync()
    {
        if (_operation is not null || _setupHasStarted || _availableAppUpdate is not { } release) return;
        var message = $"Update this app to {release.Version}?\n\n" +
            $"The verified app will be saved at:\n{AppUpdateInstaller.Destination}\n\n" +
            "The app will close and reopen. An original portable copy outside this folder stays unchanged. " +
            "No Windows upgrade or computer restart will start. Unsigned releases rely on this project's GitHub release and checksum verification.";
        var accepted = _confirmAppUpdate?.Invoke(message) ?? MessageBox.Show(this, message,
            "Application update", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        if (!accepted) return;
        _operation = new CancellationTokenSource();
        var operation = _operation;
        var oldStatus = _status.Text;
        SetBusy(true);
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Continuous;
        var handedOff = false;
        try
        {
            var progress = new Progress<AppUpdateProgress>(item =>
            {
                if (!ReferenceEquals(_operation, operation) || operation.IsCancellationRequested) return;
                var percent = item.TotalBytes == 0 ? 0 : (int)(item.BytesReceived * 100 / item.TotalBytes);
                _progress.Value = Math.Clamp(percent, 0, 100);
                _status.Text = $"Downloading app update... {_progress.Value}%";
            });
            _status.Text = "Downloading and verifying the app update...";
            var result = await (_installAppUpdate?.Invoke(release, progress, operation.Token)
                ?? AppUpdateInstaller.InstallAsync(_applicationUpdates, release, true, progress, operation.Token));
            AppLog.Info(result.Message);
            handedOff = true;
            _closingForAppUpdate = true;
            operation.Dispose();
            _operation = null;
            Close();
        }
        catch (OperationCanceledException)
        { _appUpdatesLink.Text = "App update cancelled — retry"; }
        catch (Exception ex)
        {
            AppLog.Error($"Application update installation failed: {ex}");
            _appUpdatesLink.Text = "App update failed — retry";
            _appUpdatesLink.AccessibleDescription = ex.Message;
            var explanation = "The app update could not be started. Your current copy was not replaced.\n\n" + ex.Message;
            if (_showAppUpdateFailure is not null) _showAppUpdateFailure(explanation);
            else MessageBox.Show(this, explanation, "Application update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!handedOff)
            {
                operation.Dispose();
                if (ReferenceEquals(_operation, operation)) _operation = null;
                _progress.Visible = false;
                SetBusy(false);
                _status.Text = oldStatus; // App discovery/errors never replace Windows-media diagnostics.
            }
        }
    }
}
