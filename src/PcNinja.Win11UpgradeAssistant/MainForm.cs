using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(14, 18, 28);
    private static readonly Color Card = Color.FromArgb(25, 31, 45);
    private static readonly Color CardAlt = Color.FromArgb(31, 38, 55);
    private static readonly Color Primary = Color.FromArgb(53, 126, 255);
    private static readonly Color Accent = Color.FromArgb(132, 91, 255);
    private static readonly Color Foreground = Color.FromArgb(238, 242, 250);
    private static readonly Color Muted = Color.FromArgb(166, 176, 196);

    private readonly UpgradeEngine _engine = new();
    private readonly Label _systemValue = new();
    private readonly Label _systemStorage = new();
    private readonly RadioButton _autoDownload = new();
    private readonly RadioButton _localIso = new();
    private readonly RadioButton _mountedIso = new();
    private readonly ComboBox _localIsos = new();
    private readonly ComboBox _mountedSources = new();
    private readonly ComboBox _downloadLocations = new();
    private readonly Button _browse = new();
    private readonly Button _chooseDownloadFolder = new();
    private readonly Button _refresh = new();
    private readonly Button _pcNinjaDownload = new();
    private readonly Button _microsoft = new();
    private readonly Button _start = new();
    private readonly CheckBox _autoRestart = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private CancellationTokenSource? _operation;

    public MainForm()
    {
        Text = "PcNinja Windows 11 Upgrade Assistant - RC3";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 760);
        Size = new Size(980, 820);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Background;
        ForeColor = Foreground;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        BuildUi();
        Shown += (_, _) => RefreshSources(true);
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
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26, 20, 26, 18),
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Background
        };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);

        page.Controls.Add(BuildHeader());

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Card,
            Padding = new Padding(22, 18, 22, 18),
            ColumnCount = 1,
            RowCount = 16,
            Margin = new Padding(0, 14, 0, 14)
        };
        for (var i = 0; i < card.RowCount; i++) card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(card);

        var computerHeader = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        computerHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        computerHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        computerHeader.Controls.Add(Heading("This computer"), 0, 0);
        StyleSecondaryButton(_refresh, "Refresh sources and drives");
        _refresh.Click += (_, _) => RefreshSources(false);
        computerHeader.Controls.Add(_refresh, 1, 0);
        card.Controls.Add(computerHeader);

        _systemValue.AutoSize = true;
        _systemValue.Text = "Detecting Windows version...";
        _systemValue.ForeColor = Foreground;
        _systemValue.Margin = new Padding(0, 2, 0, 2);
        card.Controls.Add(_systemValue);
        _systemStorage.AutoSize = true;
        _systemStorage.ForeColor = Muted;
        _systemStorage.Margin = new Padding(0, 0, 0, 16);
        card.Controls.Add(_systemStorage);
        card.Controls.Add(Heading("Choose the Windows 11 installation source"));

        _autoDownload.Text = "Download the ISO automatically";
        StyleRadio(_autoDownload, true);
        card.Controls.Add(_autoDownload);
        card.Controls.Add(Description("The assistant downloads from PcNinja, preserves the original filename, and validates the completed media."));
        var destinationRow = BuildTwoColumnRow();
        _downloadLocations.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_downloadLocations);
        _downloadLocations.DropDown += (_, _) => _autoDownload.Checked = true;
        destinationRow.Controls.Add(_downloadLocations, 0, 0);
        StyleSecondaryButton(_chooseDownloadFolder, "Choose folder...");
        _chooseDownloadFolder.Click += (_, _) => ChooseDownloadFolder();
        destinationRow.Controls.Add(_chooseDownloadFolder, 1, 0);
        destinationRow.Margin = new Padding(24, 4, 0, 4);
        card.Controls.Add(destinationRow);

        _localIso.Text = "Use an ISO file already downloaded";
        StyleRadio(_localIso);
        card.Controls.Add(_localIso);
        var localRow = BuildTwoColumnRow();
        _localIsos.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_localIsos);
        _localIsos.DropDown += (_, _) => _localIso.Checked = true;
        StyleSecondaryButton(_browse, "Browse...");
        _browse.Click += (_, _) => BrowseIso();
        localRow.Controls.Add(_localIsos, 0, 0);
        localRow.Controls.Add(_browse, 1, 0);
        localRow.Margin = new Padding(24, 4, 0, 4);
        card.Controls.Add(localRow);

        _mountedIso.Text = "Use a Windows ISO that is already mounted";
        StyleRadio(_mountedIso);
        card.Controls.Add(_mountedIso);
        _mountedSources.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_mountedSources);
        _mountedSources.DropDown += (_, _) => _mountedIso.Checked = true;
        _mountedSources.Margin = new Padding(24, 4, 0, 10);
        card.Controls.Add(_mountedSources);

        _autoRestart.Text = "Allow Windows Setup to restart the computer automatically";
        _autoRestart.Checked = true;
        _autoRestart.AutoSize = true;
        _autoRestart.ForeColor = Foreground;
        _autoRestart.Margin = new Padding(0, 12, 0, 2);
        card.Controls.Add(_autoRestart);
        card.Controls.Add(Description("Clear this option to add /noreboot. PcNinja never forces a restart after a failed Setup process."));

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Background
        };
        StylePrimaryButton(_start, "Validate and start upgrade");
        _start.Click += async (_, _) => await StartUpgradeAsync();
        StyleSecondaryButton(_pcNinjaDownload, "Open PcNinja download page");
        _pcNinjaDownload.Click += (_, _) => UpgradeEngine.OpenPcNinjaDownloadPage();
        StyleSecondaryButton(_microsoft, "Open Microsoft download page");
        _microsoft.Click += (_, _) => UpgradeEngine.OpenMicrosoftDownloadPage();
        actions.Controls.Add(_start);
        actions.Controls.Add(_pcNinjaDownload);
        actions.Controls.Add(_microsoft);
        page.Controls.Add(actions);

        _progress.Dock = DockStyle.Top;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Visible = false;
        page.Controls.Add(_progress);
        _status.AutoSize = true;
        _status.Text = "Ready.";
        _status.ForeColor = Muted;
        _status.Margin = new Padding(0, 8, 0, 8);
        page.Controls.Add(_status);
        var footer = new LinkLabel
        {
            AutoSize = true,
            Text = $"Version 1.1.0-rc.3  |  View log: {AppLog.Path}",
            LinkColor = Color.FromArgb(119, 174, 255),
            ActiveLinkColor = Accent,
            VisitedLinkColor = Color.FromArgb(168, 137, 255)
        };
        footer.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{AppLog.Path}\"") { UseShellExecute = true });
        page.Controls.Add(footer);

        _autoDownload.CheckedChanged += (_, _) => UpdateSourceControls();
        _localIso.CheckedChanged += (_, _) => UpdateSourceControls();
        _mountedIso.CheckedChanged += (_, _) => UpdateSourceControls();
        UpdateSourceControls();
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Background };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var logo = new PictureBox { Size = new Size(72, 72), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 16, 0) };
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PcNinja.Win11UpgradeAssistant.Assets.Ninja-DMT-header.png");
            if (stream is not null)
            {
                using var image = Image.FromStream(stream);
                logo.Image = new Bitmap(image);
            }
        }
        catch { }
        header.Controls.Add(logo, 0, 0);
        var text = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(0, 7, 0, 0) };
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Windows 11 Upgrade Assistant",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Foreground,
            Margin = new Padding(0)
        });
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "PcNinja standalone RC3 for advanced Windows 10 and Windows 11 users",
            ForeColor = Muted,
            Margin = new Padding(1, 3, 0, 0)
        });
        header.Controls.Add(text, 1, 0);
        return header;
    }

    private void RefreshSources(bool initial)
    {
        var selectedLocal = _localIsos.Text;
        var selectedMounted = _mountedSources.SelectedItem?.ToString();
        var selectedDownloadRoot = (_downloadLocations.SelectedItem as StorageLocation)?.RootPath;
        try
        {
            _engine.ValidateSupportedComputer();
            _systemValue.Text = _engine.GetOperatingSystemDescription();
            var systemStorage = _engine.CheckSystemDriveSpace();
            _systemStorage.Text = systemStorage.Message;
            _systemStorage.ForeColor = systemStorage.IsValid ? Color.FromArgb(101, 211, 151) : Color.FromArgb(255, 164, 110);

            var mounted = _engine.FindMountedSources();
            _mountedSources.Items.Clear();
            foreach (var source in mounted) _mountedSources.Items.Add(source.Root);
            SelectText(_mountedSources, selectedMounted);

            var local = _engine.FindLocalIsos().ToList();
            if (!string.IsNullOrWhiteSpace(selectedLocal) && File.Exists(selectedLocal) &&
                !local.Contains(selectedLocal, StringComparer.OrdinalIgnoreCase)) local.Insert(0, selectedLocal);
            _localIsos.Items.Clear();
            foreach (var path in local) _localIsos.Items.Add(path);
            if (!string.IsNullOrWhiteSpace(selectedLocal)) _localIsos.Text = selectedLocal;
            else if (_localIsos.Items.Count > 0) _localIsos.SelectedIndex = 0;

            var locations = _engine.FindDownloadLocations();
            _downloadLocations.Items.Clear();
            foreach (var location in locations) _downloadLocations.Items.Add(location);
            var matchIndex = locations.ToList().FindIndex(location =>
                string.Equals(location.RootPath, selectedDownloadRoot, StringComparison.OrdinalIgnoreCase));
            if (_downloadLocations.Items.Count > 0) _downloadLocations.SelectedIndex = matchIndex >= 0 ? matchIndex : 0;

            if (initial)
            {
                if (local.Count > 0) _localIso.Checked = true;
                else _autoDownload.Checked = true;
            }
            _status.Text = $"Refresh complete: {local.Count} local ISO file(s), {mounted.Count} mounted source(s), {locations.Count} download drive(s).";
            _start.Enabled = systemStorage.IsValid;
            AppLog.Info($"Sources refreshed. OS: {_systemValue.Text}. Local ISO: {local.Count}. Mounted: {mounted.Count}. Drives: {locations.Count}.");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex.ToString());
            _status.Text = ex.Message;
            _start.Enabled = false;
            if (initial) MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        if (!_localIsos.Items.Contains(dialog.FileName)) _localIsos.Items.Insert(0, dialog.FileName);
        _localIsos.Text = dialog.FileName;
        _localIso.Checked = true;
    }

    private void ChooseDownloadFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder for the Windows ISO download",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var root = Path.GetPathRoot(dialog.SelectedPath)!;
            var drive = new DriveInfo(root);
            var custom = new StorageLocation(dialog.SelectedPath, "Selected folder", drive.DriveType,
                drive.DriveFormat, drive.AvailableFreeSpace);
            _downloadLocations.Items.Insert(0, custom);
            _downloadLocations.SelectedIndex = 0;
            _autoDownload.Checked = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartUpgradeAsync()
    {
        SetBusy(true);
        _operation = new CancellationTokenSource();
        try
        {
            var systemStorage = _engine.CheckSystemDriveSpace();
            if (!systemStorage.IsValid) throw new InvalidOperationException(systemStorage.Message);

            MountedSource source;
            if (_mountedIso.Checked)
            {
                _status.Text = "Validating the mounted Windows installation source...";
                if (_mountedSources.SelectedItem is not string root)
                    throw new InvalidOperationException("No mounted Windows ISO was detected. Insert or mount it, then click Refresh.");
                source = await _engine.ValidateMountedSourceAsync(root, _operation.Token);
            }
            else
            {
                string isoPath;
                if (_localIso.Checked)
                {
                    isoPath = _localIsos.Text.Trim().Trim('"');
                    _engine.ValidateIsoFile(isoPath);
                }
                else
                {
                    if (_downloadLocations.SelectedItem is not StorageLocation location)
                        throw new InvalidOperationException("Choose a download destination. Connect a drive and click Refresh if necessary.");
                    var destinationDirectory = location.VolumeLabel == "Selected folder"
                        ? location.RootPath
                        : Path.Combine(location.RootPath, "PcNinja Downloads");
                    _status.Text = "Downloading the Windows 11 ISO...";
                    _progress.Visible = true;
                    var progress = new Progress<DownloadProgress>(value =>
                    {
                        if (value.Percentage.HasValue)
                        {
                            _progress.Style = ProgressBarStyle.Continuous;
                            _progress.Value = value.Percentage.Value;
                            _status.Text = $"Downloading {Path.GetFileName(value.DestinationPath)}... {value.Percentage}%";
                        }
                        else
                        {
                            _progress.Style = ProgressBarStyle.Marquee;
                            _status.Text = $"Downloading {Path.GetFileName(value.DestinationPath)}... {UpgradeEngine.FormatBytes(value.BytesReceived)} received";
                        }
                    });
                    try
                    {
                        var download = await _engine.DownloadIsoAsync(destinationDirectory, progress, _operation.Token);
                        isoPath = download.IsoPath;
                        if (!_localIsos.Items.Contains(isoPath)) _localIsos.Items.Insert(0, isoPath);
                        _localIsos.Text = isoPath;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error($"Automatic download failed: {ex}");
                        try { UpgradeEngine.OpenPcNinjaDownloadPage(); }
                        catch (Exception browserError) { AppLog.Error($"Browser fallback failed: {browserError}"); }
                        throw new InvalidOperationException(
                            $"Automatic download failed: {ex.Message}\n\n" +
                            "The PcNinja download page was opened in your browser. Download the ISO, then click Refresh or Browse and select the exact file.");
                    }
                }

                _progress.Style = ProgressBarStyle.Marquee;
                _progress.Visible = true;
                _status.Text = $"Mounting and validating {Path.GetFileName(isoPath)}...";
                source = await _engine.MountAndValidateAsync(isoPath, _operation.Token);
            }

            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Visible = false;
            _status.Text = $"Validated Microsoft installation source at {source.Root}.";
            var restartText = _autoRestart.Checked
                ? "Windows Setup may restart the computer automatically when required."
                : "Automatic restart is disabled. You will restart the computer manually.";
            var confirmation = MessageBox.Show(
                $"The Windows installation source passed signature and image validation.\n\n{restartText}\n\n" +
                "Save your work and back up important files. This tool uses an experimental unsupported-hardware upgrade method.\n\nStart Windows Setup now?",
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
            RefreshSources(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _start.Enabled = !busy;
        _refresh.Enabled = !busy;
        _browse.Enabled = !busy;
        _chooseDownloadFolder.Enabled = !busy;
        _pcNinjaDownload.Enabled = !busy;
        _microsoft.Enabled = !busy;
        _autoDownload.Enabled = !busy;
        _localIso.Enabled = !busy;
        _mountedIso.Enabled = !busy;
        _mountedSources.Enabled = !busy && _mountedIso.Checked;
        _localIsos.Enabled = !busy;
        _downloadLocations.Enabled = !busy;
        _autoRestart.Enabled = !busy;
    }

    private void UpdateSourceControls()
    {
        var idle = _operation is null;
        _browse.Enabled = idle;
        _localIsos.Enabled = idle;
        _downloadLocations.Enabled = idle;
        _chooseDownloadFolder.Enabled = idle;
        _mountedSources.Enabled = idle && _mountedIso.Checked;
        _mountedIso.Enabled = idle;
    }

    private static void SelectText(ComboBox combo, string? value)
    {
        if (combo.Items.Count == 0) return;
        var index = -1;
        if (!string.IsNullOrWhiteSpace(value))
            for (var i = 0; i < combo.Items.Count; i++)
                if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
        combo.SelectedIndex = index >= 0 ? index : 0;
    }

    private static TableLayoutPanel BuildTwoColumnRow()
    {
        var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, BackColor = Card };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return row;
    }

    private static Label Heading(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 11.5F),
        ForeColor = Foreground,
        Margin = new Padding(0, 0, 0, 2)
    };

    private static Label Description(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(820, 0),
        Text = text,
        ForeColor = Muted,
        Margin = new Padding(24, 0, 0, 2)
    };

    private static void StyleRadio(RadioButton radio, bool isChecked = false)
    {
        radio.AutoSize = true;
        radio.Checked = isChecked;
        radio.ForeColor = Foreground;
        radio.Margin = new Padding(0, 9, 0, 2);
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.Dock = DockStyle.Fill;
        combo.BackColor = CardAlt;
        combo.ForeColor = Foreground;
        combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 23;
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Primary : CardAlt);
            e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font,
                e.Bounds, Foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            e.DrawFocusRectangle();
        };
    }

    private static void StylePrimaryButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(13, 7, 13, 7);
        button.BackColor = Primary;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(0, 0, 8, 0);
    }

    private static void StyleSecondaryButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(9, 5, 9, 5);
        button.BackColor = CardAlt;
        button.ForeColor = Foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(63, 75, 103);
        button.Margin = new Padding(6, 0, 0, 0);
    }
}
