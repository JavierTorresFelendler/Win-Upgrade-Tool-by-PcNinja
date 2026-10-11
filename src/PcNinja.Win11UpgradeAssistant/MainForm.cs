using System.Diagnostics;
using System.Drawing;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal sealed class MainForm : Form
{
    private readonly UpgradeEngine _engine = new();
    private readonly Label _systemValue = new();
    private readonly RadioButton _autoDownload = new();
    private readonly RadioButton _localIso = new();
    private readonly RadioButton _mountedIso = new();
    private readonly TextBox _isoPath = new();
    private readonly ComboBox _mountedSources = new();
    private readonly Button _browse = new();
    private readonly Button _microsoft = new();
    private readonly Button _start = new();
    private readonly CheckBox _autoRestart = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private CancellationTokenSource? _operation;

    public MainForm()
    {
        Text = "PcNinja Windows 11 Upgrade Assistant — RC1";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(790, 620);
        Size = new Size(860, 690);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 247, 250);
        BuildUi();
        Shown += (_, _) => DetectSources();
        FormClosing += (_, e) =>
        {
            if (_operation is null) return;
            e.Cancel = true;
            MessageBox.Show("Please wait for the current operation to finish.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private void BuildUi()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 7 };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);

        var title = new Label
        {
            AutoSize = true,
            Text = "Windows 11 Upgrade Assistant",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(20, 49, 78),
            Margin = new Padding(0, 0, 0, 4)
        };
        page.Controls.Add(title);
        page.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Standalone RC1 for advanced home users on Windows 10 and Windows 11 x64.",
            ForeColor = Color.FromArgb(74, 85, 104),
            Margin = new Padding(0, 0, 0, 18)
        });

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 11,
            Margin = new Padding(0, 0, 0, 16)
        };
        for (var i = 0; i < 11; i++) card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(card);

        card.Controls.Add(Heading("This computer"));
        _systemValue.AutoSize = true;
        _systemValue.Text = "Detecting Windows version...";
        _systemValue.Margin = new Padding(0, 2, 0, 18);
        card.Controls.Add(_systemValue);
        card.Controls.Add(Heading("Choose the Windows 11 installation source"));

        _autoDownload.Text = "Download the ISO automatically (recommended)";
        _autoDownload.AutoSize = true;
        _autoDownload.Checked = true;
        _autoDownload.Margin = new Padding(0, 8, 0, 3);
        card.Controls.Add(_autoDownload);
        card.Controls.Add(Description("The assistant downloads the configured ISO privately. If it is unavailable, you can use Microsoft or select a local ISO."));

        _localIso.Text = "Use an ISO file already downloaded";
        _localIso.AutoSize = true;
        _localIso.Margin = new Padding(0, 10, 0, 3);
        card.Controls.Add(_localIso);
        var localRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(24, 0, 0, 0) };
        localRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        localRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _isoPath.Dock = DockStyle.Fill;
        _isoPath.ReadOnly = true;
        _isoPath.PlaceholderText = "Select a Windows 11 x64 ISO";
        _browse.Text = "Browse...";
        _browse.AutoSize = true;
        _browse.Margin = new Padding(8, 0, 0, 0);
        _browse.Click += (_, _) => BrowseIso();
        localRow.Controls.Add(_isoPath, 0, 0);
        localRow.Controls.Add(_browse, 1, 0);
        card.Controls.Add(localRow);

        _mountedIso.Text = "Use a Windows ISO that is already mounted";
        _mountedIso.AutoSize = true;
        _mountedIso.Margin = new Padding(0, 12, 0, 3);
        card.Controls.Add(_mountedIso);
        _mountedSources.DropDownStyle = ComboBoxStyle.DropDownList;
        _mountedSources.Dock = DockStyle.Top;
        _mountedSources.Margin = new Padding(24, 0, 0, 14);
        card.Controls.Add(_mountedSources);

        _autoRestart.Text = "Allow Windows Setup to restart the computer automatically";
        _autoRestart.Checked = true;
        _autoRestart.AutoSize = true;
        _autoRestart.Margin = new Padding(0, 12, 0, 3);
        card.Controls.Add(_autoRestart);
        card.Controls.Add(Description("Clear this option to add /noreboot. The assistant never forces a restart itself."));

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 12) };
        _start.Text = "Validate and start upgrade";
        _start.AutoSize = true;
        _start.Padding = new Padding(12, 6, 12, 6);
        _start.BackColor = Color.FromArgb(0, 105, 180);
        _start.ForeColor = Color.White;
        _start.FlatStyle = FlatStyle.Flat;
        _start.Click += async (_, _) => await StartUpgradeAsync();
        _microsoft.Text = "Open Microsoft download page";
        _microsoft.AutoSize = true;
        _microsoft.Padding = new Padding(8, 6, 8, 6);
        _microsoft.Margin = new Padding(10, 0, 0, 0);
        _microsoft.Click += (_, _) => UpgradeEngine.OpenMicrosoftDownloadPage();
        actions.Controls.Add(_start);
        actions.Controls.Add(_microsoft);
        page.Controls.Add(actions);

        _progress.Dock = DockStyle.Top;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Visible = false;
        page.Controls.Add(_progress);
        _status.AutoSize = true;
        _status.Text = "Ready.";
        _status.ForeColor = Color.FromArgb(74, 85, 104);
        _status.Margin = new Padding(0, 8, 0, 8);
        page.Controls.Add(_status);
        var footer = new LinkLabel { AutoSize = true, Text = $"Version 1.0.0-rc.1  •  View log: {AppLog.Path}" };
        footer.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{AppLog.Path}\"") { UseShellExecute = true });
        page.Controls.Add(footer);

        _autoDownload.CheckedChanged += (_, _) => UpdateSourceControls();
        _localIso.CheckedChanged += (_, _) => UpdateSourceControls();
        _mountedIso.CheckedChanged += (_, _) => UpdateSourceControls();
        UpdateSourceControls();
    }

    private void DetectSources()
    {
        try
        {
            _engine.ValidateSupportedComputer();
            _systemValue.Text = _engine.GetOperatingSystemDescription();
            var mounted = _engine.FindMountedSources();
            _mountedSources.Items.Clear();
            foreach (var source in mounted) _mountedSources.Items.Add(source.Root);
            if (_mountedSources.Items.Count > 0) _mountedSources.SelectedIndex = 0;

            var local = _engine.FindLocalIsos();
            if (local.Count > 0)
            {
                _isoPath.Text = local[0];
                _localIso.Checked = true;
                _status.Text = "A local ISO was found next to the application. It will be validated before use.";
            }
            else
            {
                _autoDownload.Checked = true;
                _status.Text = mounted.Count > 0
                    ? "A mounted source was detected. Select it above, or let the assistant download an ISO."
                    : "No local ISO was found. Automatic download is ready.";
            }
            AppLog.Info($"Application started. OS: {_systemValue.Text}. Local ISO count: {local.Count}. Mounted source count: {mounted.Count}.");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _start.Enabled = false;
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateSourceControls();
    }

    private void BrowseIso()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a Windows 11 x64 ISO",
            Filter = "Windows ISO images (*.iso)|*.iso",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _isoPath.Text = dialog.FileName;
        _localIso.Checked = true;
    }

    private async Task StartUpgradeAsync()
    {
        SetBusy(true);
        _operation = new CancellationTokenSource();
        try
        {
            MountedSource source;
            if (_mountedIso.Checked)
            {
                _status.Text = "Validating the mounted Windows installation source...";
                if (_mountedSources.SelectedItem is not string root)
                    throw new InvalidOperationException("No mounted Windows ISO was detected.");
                source = await Task.Run(() => _engine.ValidateMountedSource(root), _operation.Token);
            }
            else
            {
                string isoPath;
                if (_localIso.Checked)
                {
                    isoPath = _isoPath.Text;
                    _engine.ValidateIsoFile(isoPath);
                }
                else
                {
                    _status.Text = "Downloading the Windows 11 ISO...";
                    _progress.Visible = true;
                    var progress = new Progress<int>(value => { _progress.Value = value; _status.Text = $"Downloading the Windows 11 ISO... {value}%"; });
                    try
                    {
                        isoPath = await _engine.DownloadIsoAsync(progress, _operation.Token);
                        _isoPath.Text = isoPath;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error($"Automatic download failed: {ex}");
                        var answer = MessageBox.Show(
                            $"Automatic download is currently unavailable.\n\n{ex.Message}\n\nOpen Microsoft's official Windows 11 download page?",
                            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (answer == DialogResult.Yes) UpgradeEngine.OpenMicrosoftDownloadPage();
                        throw new InvalidOperationException("Automatic download failed. Download an ISO from Microsoft, then choose 'Use an ISO file already downloaded'.");
                    }
                }

                _progress.Style = ProgressBarStyle.Marquee;
                _progress.Visible = true;
                _status.Text = "Mounting and validating the Windows 11 ISO...";
                source = await _engine.MountAndValidateAsync(isoPath, _operation.Token);
            }

            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Visible = false;
            _status.Text = $"Validated Microsoft installation source at {source.Root}.";
            var restartText = _autoRestart.Checked
                ? "Windows Setup may restart the computer automatically when required."
                : "Automatic restart is disabled. You will restart the computer manually.";
            var confirmation = MessageBox.Show(
                $"The Windows installation source is valid.\n\n{restartText}\n\nSave your work and back up important files. This tool uses an experimental unsupported-hardware upgrade method.\n\nStart Windows Setup now?",
                "Final confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                _status.Text = "Upgrade cancelled. Windows Setup was not started.";
                return;
            }

            _status.Text = "Windows Setup is running. Do not close the assistant.";
            var result = await _engine.StartUpgradeAsync(source.SetupPath, _autoRestart.Checked, _operation.Token);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Windows Setup stopped with exit code {result.ExitCode}. No forced restart was issued.");

            _status.Text = "Windows Setup completed its current stage successfully.";
            MessageBox.Show(
                _autoRestart.Checked
                    ? "Windows Setup completed its current stage. Windows Setup controls the natural restart process."
                    : "Windows Setup completed its current stage. Automatic restart is disabled; restart manually when ready.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            AppLog.Error(ex.ToString());
            _status.Text = ex.Message;
            MessageBox.Show($"{ex.Message}\n\nNo forced restart was issued.\n\nLog: {AppLog.Path}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operation?.Dispose();
            _operation = null;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Visible = false;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _start.Enabled = !busy;
        _browse.Enabled = !busy && _localIso.Checked;
        _microsoft.Enabled = !busy;
        _autoDownload.Enabled = !busy;
        _localIso.Enabled = !busy;
        _mountedIso.Enabled = !busy && _mountedSources.Items.Count > 0;
        _mountedSources.Enabled = !busy && _mountedIso.Checked;
        _autoRestart.Enabled = !busy;
    }

    private void UpdateSourceControls()
    {
        _browse.Enabled = _localIso.Checked && _operation is null;
        _isoPath.Enabled = _localIso.Checked;
        _mountedSources.Enabled = _mountedIso.Checked && _operation is null;
        _mountedIso.Enabled = _mountedSources.Items.Count > 0 && _operation is null;
    }

    private static Label Heading(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 12F),
        ForeColor = Color.FromArgb(20, 49, 78),
        Margin = new Padding(0, 0, 0, 2)
    };

    private static Label Description(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(710, 0),
        Text = text,
        ForeColor = Color.FromArgb(90, 100, 115),
        Margin = new Padding(24, 0, 0, 0)
    };
}
