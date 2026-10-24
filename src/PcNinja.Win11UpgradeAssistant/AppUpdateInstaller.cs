using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal sealed record AppUpdateRequest(int SchemaVersion, string OperationId, DateTimeOffset CreatedUtc,
    int ParentProcessId, long ParentStartUtcTicks, string ParentPath, string ParentHash,
    string? RequiredSigner, string? TargetHash, AppUpdateManifest Release, bool RelaunchGui);

internal sealed record AppUpdateHandoffResult(string Version, string TargetPath, string OperationId,
    int HelperProcessId, string Message);

/// <summary>Explicit app-only handoff. Never passes CLI/OS-upgrade arguments to a successor.</summary>
internal static class AppUpdateInstaller
{
    internal const string HelperArgument = "--internal-app-update";
    private const string Ready = "PCNINJA_APP_UPDATE_READY_V1";
    private const string Go = "PCNINJA_APP_UPDATE_GO_V1";
    internal static string Destination => AppUpdateWorkspace.InstalledExecutablePath;

    internal static async Task<AppUpdateHandoffResult> InstallAsync(AppUpdateClient client,
        AppUpdateManifest release, bool relaunchGui, IProgress<AppUpdateProgress>? progress, CancellationToken token)
    {
        AppUpdateClient.ValidateManifest(release);
        if (!Program.IsAdministrator()) throw new UnauthorizedAccessException("Run app update installation as administrator.");
        if (ReleaseVersion.Parse(release.Version).CompareTo(ReleaseVersion.Parse(ProductIdentity.Version)) <= 0)
            throw new InvalidOperationException("Only a newer stable application version can be installed.");
        using var gate = AppOperationGate.EnterUpdate();
        using var parent = Process.GetCurrentProcess();
        var parentPath = Environment.ProcessPath ?? throw new InvalidOperationException("Application path is unavailable.");
        var current = AppUpdateWorkspace.InspectExecutable(parentPath);
        if (ReleaseVersion.Parse(current.Version).CompareTo(ReleaseVersion.Parse(ProductIdentity.Version)) != 0)
            throw new InvalidDataException("The running application's file identity changed.");
        var id = Guid.NewGuid().ToString("N");
        using var workspace = AppUpdateWorkspace.Create(id, release.SizeBytes, release.SizeBytes);
        var requiredSigner = current.SignerThumbprint;
        string? targetHash = null;
        if (File.Exists(workspace.TargetPath))
        {
            var installed = AppUpdateWorkspace.InspectExecutable(workspace.TargetPath);
            if (ReleaseVersion.Parse(installed.Version).CompareTo(ReleaseVersion.Parse(release.Version)) >= 0)
                throw new InvalidOperationException($"This version or a newer app is already saved at {workspace.TargetPath}. Open that copy instead.");
            if (installed.SignerThumbprint is not null)
            {
                if (requiredSigner is not null && !requiredSigner.Equals(installed.SignerThumbprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The installed app publisher differs; update manually.");
                requiredSigner = installed.SignerThumbprint;
            }
            targetHash = installed.Sha256;
        }
        var handedOff = false;
        try
        {
            await using (var candidate = new FileStream(workspace.CandidatePath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await client.DownloadAsync(release, candidate, progress, token);
                await candidate.FlushAsync(token);
            }
            AppUpdateWorkspace.VerifyCandidate(workspace.CandidatePath, release, requiredSigner);
            // Run the verified successor's protocol-v1 helper, not a mutable source-path copy.
            File.Copy(workspace.CandidatePath, workspace.HelperPath, overwrite: false);
            AppUpdateWorkspace.VerifyCandidate(workspace.HelperPath, release, requiredSigner);
            var request = new AppUpdateRequest(1, id, DateTimeOffset.UtcNow, parent.Id,
                parent.StartTime.ToUniversalTime().Ticks, Path.GetFullPath(parentPath), current.Sha256,
                requiredSigner, targetHash, release, relaunchGui);
            workspace.WriteRequest(request);
            token.ThrowIfCancellationRequested();
            AppOperationGate.EnsureUpdateAllowed();
            var start = new ProcessStartInfo
            {
                FileName = workspace.HelperPath, WorkingDirectory = workspace.DirectoryPath,
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add(HelperArgument);
            using var helper = Process.Start(start) ?? throw new IOException("The application update helper could not start.");
            workspace.MarkHandoffStarted(); // Running helper images must never be removed by download cleanup.
            using var readyTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            readyTimeout.CancelAfter(TimeSpan.FromSeconds(20));
            var response = await ReadHandshakeAsync(helper.StandardOutput, readyTimeout.Token);
            if (response != Ready) throw new IOException("The update helper was not ready. The current app was not replaced.");
            token.ThrowIfCancellationRequested();
            AppOperationGate.EnsureUpdateAllowed();
            // Disarm BEFORE GO. On a failed write neither process releases a live operation;
            // closing all remaining handles safely destroys the semaphore instead.
            gate.TransferToHelper();
            await helper.StandardInput.WriteLineAsync(Go.AsMemory(), CancellationToken.None);
            await helper.StandardInput.FlushAsync(CancellationToken.None);
            helper.StandardInput.Close();
            handedOff = true;
            AppLog.Info($"App update handoff {id}: {ProductIdentity.Version} -> {release.Version}; target {workspace.TargetPath}.");
            return new(release.Version, workspace.TargetPath, id, helper.Id,
                "Application update handoff started. Exit this process so the helper can apply the verified app update. No Windows upgrade or restart was requested.");
        }
        finally
        {
            if (!handedOff)
            {
                try { workspace.CleanupUncommitted(); }
                catch (Exception ex) { AppLog.Info($"App update staging retained for safe recovery: {ex.Message}"); }
            }
        }
    }

    // Dispatched before normal logging, console attachment, elevation or the GUI.
    internal static async Task<int> RunHelperAsync()
    {
        AppUpdateRequest? request = null;
        var parentExited = false;
        try
        {
            if (!Program.IsAdministrator()) return 5; // Never auto-elevate an internal request.
            var self = Environment.ProcessPath ?? throw new InvalidOperationException("Helper identity unavailable.");
            if (Path.GetFileName(self) != "UpdateHelper.exe") throw new InvalidDataException("Invalid helper executable.");
            var folder = Path.GetDirectoryName(self)!;
            var id = Path.GetFileName(folder);
            using var workspace = AppUpdateWorkspace.Open(id);
            if (!SamePath(folder, workspace.DirectoryPath) || !SamePath(self, workspace.HelperPath))
                throw new InvalidDataException("The helper is outside its protected workspace.");
            request = workspace.ReadRequest<AppUpdateRequest>();
            ValidateRequest(request, id);
            AppUpdateWorkspace.VerifyCandidate(self, request.Release, request.RequiredSigner);
            AppUpdateWorkspace.VerifyCandidate(workspace.CandidatePath, request.Release, request.RequiredSigner);
            using var parent = Process.GetProcessById(request.ParentProcessId);
            _ = parent.SafeHandle; // Retain the exact process object, not a reusable PID-only poll.
            if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartUtcTicks ||
                !SamePath(parent.MainModule?.FileName ?? "", request.ParentPath))
                throw new InvalidDataException("The original application process identity changed.");
            var parentFile = AppUpdateWorkspace.InspectExecutable(request.ParentPath);
            if (!parentFile.Sha256.Equals(request.ParentHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The original executable changed before handoff.");
            if (parentFile.SignerThumbprint is not null &&
                !parentFile.SignerThumbprint.Equals(request.RequiredSigner, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The app update cannot drop the original publisher policy.");
            if (ReleaseVersion.Parse(parentFile.Version).CompareTo(ReleaseVersion.Parse(request.Release.Version)) >= 0)
                throw new InvalidDataException("The successor is not newer than the original app.");
            using var gate = AppOperationGate.OpenHandoff();
            Console.WriteLine(Ready);
            Console.Out.Flush();
            using (var goTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                if (await ReadHandshakeAsync(Console.In, goTimeout.Token) != Go)
                    throw new InvalidDataException("App update handoff was not committed.");
            gate.AdoptHandoff();
            using (var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                await parent.WaitForExitAsync(exitTimeout.Token);
            parentExited = true;
            ValidateRequest(request, id);
            AppOperationGate.EnsureUpdateAllowed();
            ApplyVerified(workspace, request, LaunchGui);
            try { WriteResult(workspace.TargetPath, request, true, "Application update applied."); }
            catch (Exception statusError)
            {
                // Replacement/relaunch already succeeded. A diagnostic write failure
                // must not claim the previous application was restored or undo it.
                AppLog.Error($"Application update {request.OperationId} applied, but its status file could not be saved: {statusError}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Application update helper failed: {ex}");
            if (request is not null)
            {
                try { WriteResult(Destination, request, false, "Application update failed. The previous executable was retained or restored. See the app log."); } catch { }
                if (parentExited && request.RelaunchGui)
                    MessageBox.Show("The app update could not be completed. Your previous copy was not removed. Open it again and check View log.",
                        "Application update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return 1;
        }
    }

    internal static void ValidateRequest(AppUpdateRequest request, string id)
    {
        var now = DateTimeOffset.UtcNow;
        if (request is null || request.SchemaVersion != 1 || request.OperationId != id ||
            id is not { Length: 32 } || !id.All(char.IsAsciiHexDigit) ||
            !Guid.TryParseExact(id, "N", out var operation) || operation == Guid.Empty || request.ParentProcessId <= 0 ||
            request.ParentProcessId == Environment.ProcessId || request.ParentStartUtcTicks <= 0 ||
            request.ParentStartUtcTicks > DateTime.MaxValue.Ticks ||
            request.ParentStartUtcTicks > request.CreatedUtc.UtcDateTime.Ticks ||
            request.ParentStartUtcTicks > now.UtcDateTime.Ticks ||
            request.CreatedUtc > now.AddSeconds(30) || now - request.CreatedUtc > TimeSpan.FromMinutes(5) ||
            !IsOrdinaryLocalPath(request.ParentPath) ||
            !IsHash(request.ParentHash) || request.TargetHash is not null && !IsHash(request.TargetHash) ||
            request.RequiredSigner is not null && (request.RequiredSigner.Length != 40 || !request.RequiredSigner.All(Uri.IsHexDigit)))
            throw new InvalidDataException("The app update handoff is invalid or expired.");
        AppUpdateClient.ValidateManifest(request.Release);
    }

    internal static void ApplyVerified(AppUpdateWorkspace workspace, AppUpdateRequest request, Action<string> launch)
    {
        workspace.ValidateTargetSecurity();
        AppUpdateWorkspace.VerifyCandidate(workspace.CandidatePath, request.Release, request.RequiredSigner);
        var hadTarget = request.TargetHash is not null;
        if (File.Exists(workspace.TargetPath) != hadTarget || File.Exists(workspace.BackupPath))
            throw new InvalidDataException("The app update target or recovery state changed.");
        if (hadTarget && !AppUpdateWorkspace.InspectExecutable(workspace.TargetPath).Sha256.Equals(request.TargetHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installed app changed after staging.");
        // Hash/signature verification above can take seconds. Recheck external Setup
        // and restart state at the actual replacement boundary, not only on entry.
        AppOperationGate.EnsureUpdateAllowed();
        CommitReplacement(workspace.CandidatePath, workspace.TargetPath, workspace.BackupPath,
            Path.Combine(workspace.DirectoryPath, "failed.exe"), hadTarget, workspace.SetTargetReadable,
            () => AppUpdateWorkspace.VerifyCandidate(workspace.TargetPath, request.Release, request.RequiredSigner),
            () => AppUpdateWorkspace.InspectExecutable(workspace.BackupPath).Sha256.Equals(request.TargetHash, StringComparison.OrdinalIgnoreCase),
            () => { if (request.RelaunchGui) launch(workspace.TargetPath); });
    }

    // Production supplies only the fixed, protected workspace paths above. This
    // internal strategy seam also allows harmless opaque-byte recovery fixtures;
    // it is never exposed as a CLI file-copy or arbitrary-path replacement command.
    internal static void CommitReplacement(string candidatePath, string targetPath, string backupPath,
        string failedPath, bool hadTarget, Action setTargetReadable, Action verifyInstalled,
        Func<bool> verifyBackup, Action launch)
    {
        if (File.Exists(backupPath) || File.Exists(failedPath))
            throw new InvalidDataException("Existing application recovery files must not be overwritten.");
        var replaced = false;
        try
        {
            if (hadTarget) File.Replace(candidatePath, targetPath, backupPath);
            else File.Move(candidatePath, targetPath);
            replaced = true;
            setTargetReadable();
            verifyInstalled();
            launch(); // Production callback passes a fixed executable path and no arguments.
        }
        catch
        {
            if (replaced && hadTarget && File.Exists(backupPath) && verifyBackup())
            {
                File.Replace(backupPath, targetPath, failedPath);
                setTargetReadable();
            }
            else if (replaced && !hadTarget && File.Exists(targetPath))
                File.Move(targetPath, failedPath);
            throw;
        }
    }

    private static void LaunchGui(string target)
    {
        using var launched = Process.Start(new ProcessStartInfo
        {
            FileName = target, WorkingDirectory = Path.GetDirectoryName(target)!, UseShellExecute = false
            // Intentionally no Arguments/ArgumentList. An updater must never resume an OS operation.
        }) ?? throw new IOException("The updated app could not be reopened.");
    }

    private static void WriteResult(string target, AppUpdateRequest request, bool success, string message)
    {
        var path = Path.Combine(Path.GetDirectoryName(target)!, "Last-AppUpdate.json");
        var temp = path + "." + request.OperationId + ".tmp";
        using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(file, new { schemaVersion = 1, timestamp = DateTimeOffset.UtcNow,
                operationId = request.OperationId, success, version = request.Release.Version, targetPath = target, message });
        File.Move(temp, path, overwrite: true);
    }

    // Console.In can implement ReadAsync with a synchronous underlying pipe read.
    // Move invocation off the caller and bound the wait itself, not just the read's
    // cooperative cancellation. On failure the helper exits without applying.
    internal static Task<string?> ReadHandshakeAsync(TextReader reader, CancellationToken token) =>
        Task.Run(() => ReadBoundedLineAsync(reader, 64, token), CancellationToken.None).WaitAsync(token);

    private static async Task<string?> ReadBoundedLineAsync(TextReader reader, int limit, CancellationToken token)
    {
        var text = new StringBuilder();
        var one = new char[1];
        while (await reader.ReadAsync(one.AsMemory(), token) != 0)
        {
            if (one[0] == '\n') return text.ToString().TrimEnd('\r');
            if (text.Length >= limit) throw new InvalidDataException("Invalid app update handshake.");
            text.Append(one[0]);
        }
        return null;
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private static bool IsOrdinaryLocalPath(string? path) => !string.IsNullOrWhiteSpace(path) && path == path.Trim() &&
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' &&
        !path.Any(char.IsControl) && path.IndexOfAny(['"', '*', '?']) < 0 && !path.AsSpan(2).Contains(':') &&
        Path.IsPathFullyQualified(path);
    private static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}
