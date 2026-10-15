using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal sealed class MainForm : Form
{
    // Nocturne design tokens adapted from the PcNinja dashboard handoff.
    private static readonly Color Background = Color.FromArgb(22, 24, 38);
    private static readonly Color Surface = Color.FromArgb(35, 37, 50);
    private static readonly Color SurfaceRaised = Color.FromArgb(41, 43, 55);
    private static readonly Color Divider = Color.FromArgb(63, 66, 77);
    private static readonly Color Accent = Color.FromArgb(145, 132, 217);
    private static readonly Color AccentLight = Color.FromArgb(210, 206, 253);
    private static readonly Color AccentTint = Color.FromArgb(43, 39, 65);
    private static readonly Color Foreground = Color.FromArgb(233, 233, 237);
    private static readonly Color Muted = Color.FromArgb(178, 182, 202);
    private static readonly Color Success = Color.FromArgb(133, 200, 163);
    private static readonly Color Warning = Color.FromArgb(229, 183, 123);

    private readonly UpgradeEngine _engine = new();
    private readonly Label _systemValue = new();
    private readonly Label _processorValue = new();
    private readonly Label _memoryValue = new();
    private readonly Label _systemStorage = new();
    private readonly Label _sourceSummary = new();
    private readonly Label _restartSummary = new();
    private readonly RadioButton _autoDownload = new();
    private readonly RadioButton _localIso = new();
    private readonly RadioButton _mountedIso = new();
    private readonly ComboBox _localIsos = new();
    private readonly ComboBox _mountedSources = new();
    private readonly ComboBox _downloadLocations = new();
    private readonly Button _browse = new();
    private readonly Button _browseInstallationFolder = new();
    private readonly Button _chooseDownloadFolder = new();
    private readonly Button _refresh = new();
    private readonly Button _pcNinjaDownload = new();
    private readonly Button _microsoft = new();
    private readonly Button _start = new();
    private readonly CheckBox _autoRestart = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly LinkLabel _details = new();
    private string? _lastErrorDetails;
    private readonly TableLayoutPanel _autoSourcePanel = new();
    private readonly TableLayoutPanel _localSourcePanel = new();
    private readonly TableLayoutPanel _mountedSourcePanel = new();
    private CancellationTokenSource? _operation;
    private bool _setupHasStarted;
    private bool _changingSourceSelection;

    public MainForm()
    {
        Text = "PcNinja Windows 11 Upgrade Assistant - RC4";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 700);
        Size = new Size(1160, 800);
        AutoScaleMode = AutoScaleMode.Dpi;
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
            _status.Text = _setupHasStarted ? "Windows Setup is running. Please wait."
                : "Use CANCEL to stop the current operation first.";
        };
    }

    private void BuildUi()
    {
        SuspendLayout();

        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 14),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background
        };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(page);

        page.Controls.Add(BuildHeader(), 0, 0);

        var dashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Background,
            Margin = new Padding(0, 16, 0, 12)
        };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sourceCard = BuildSourceCard();
        sourceCard.Margin = new Padding(0, 0, 8, 0);
        dashboard.Controls.Add(sourceCard, 0, 0);

        var readinessCard = BuildReadinessCard();
        readinessCard.Margin = new Padding(8, 0, 0, 0);
        dashboard.Controls.Add(readinessCard, 1, 0);
        page.Controls.Add(dashboard, 0, 1);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 6;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Visible = false;
        _progress.Margin = new Padding(0, 0, 0, 8);
        page.Controls.Add(_progress, 0, 2);
        page.Controls.Add(BuildStatusBar(), 0, 3);

        _autoDownload.CheckedChanged += (_, _) => SourceSelectionChanged(_autoDownload);
        _localIso.CheckedChanged += (_, _) => SourceSelectionChanged(_localIso);
        _mountedIso.CheckedChanged += (_, _) => SourceSelectionChanged(_mountedIso);
        _autoRestart.CheckedChanged += (_, _) => UpdateReadinessSummary();
        _localIsos.SelectedIndexChanged += (_, _) => UpdateReadinessSummary();
        _localIsos.TextChanged += (_, _) => UpdateReadinessSummary();
        _mountedSources.SelectedIndexChanged += (_, _) => UpdateReadinessSummary();
        _downloadLocations.SelectedIndexChanged += (_, _) => UpdateReadinessSummary();

        UpdateSourceControls();
        UpdateReadinessSummary();
        ResumeLayout(true);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Background,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var brand = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Background,
            Margin = new Padding(0)
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = CreateMascot(62);
        logo.Margin = new Padding(0, 0, 14, 0);
        brand.Controls.Add(logo, 0, 0);

        var text = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Background,
            Padding = new Padding(0, 4, 0, 0),
            Margin = new Padding(0)
        };
        var website = new LinkLabel
        {
            Text = "www.pcninja.pro", AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F), LinkColor = AccentLight,
            ActiveLinkColor = Accent, VisitedLinkColor = AccentLight,
            Margin = new Padding(0, 0, 0, 4), BackColor = Color.Transparent
        };
        website.LinkClicked += (_, _) => OpenPage("https://www.pcninja.pro");
        text.Controls.Add(website);
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Windows 11 Upgrade Assistant",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Foreground,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 1, 0, 0)
        });
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "A guided in-place upgrade for advanced Windows 10 and Windows 11 users",
            ForeColor = Muted,
            BackColor = Color.Transparent,
            Margin = new Padding(1, 4, 0, 0)
        });
        brand.Controls.Add(text, 1, 0);
        header.Controls.Add(brand, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Background,
            Margin = new Padding(14, 14, 0, 0)
        };
        StyleSecondaryButton(_refresh, "Refresh sources");
        _refresh.Margin = new Padding(0, 0, 8, 0);
        _refresh.Click += (_, _) => RefreshSources(false);
        actions.Controls.Add(_refresh);
        StyleGoButton(_start, "START UPGRADE");
        _start.Click += async (_, _) => await StartOrCancelAsync();
        actions.Controls.Add(_start);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private Control BuildSourceCard()
    {
        var card = CreateCard();
        var scrollHost = CreateScrollHost();

        var content = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = Surface,
            Margin = new Padding(0)
        };
        scrollHost.Controls.Add(content);

        content.Controls.Add(Kicker("INSTALLATION SOURCE"));
        content.Controls.Add(SectionHeading("Choose how Windows 11 Setup is provided"));
        content.Controls.Add(BodyText("Use automatic download, a local ISO, mounted media, or an extracted Windows Setup folder."));

        _autoDownload.Text = "Download the ISO automatically";
        StyleRadio(_autoDownload, true);
        var destinationRow = BuildTwoColumnRow();
        _downloadLocations.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_downloadLocations);
        _downloadLocations.DropDown += (_, _) => _autoDownload.Checked = true;
        destinationRow.Controls.Add(_downloadLocations, 0, 0);
        StyleSecondaryButton(_chooseDownloadFolder, "Choose folder...");
        _chooseDownloadFolder.Click += (_, _) => ChooseDownloadFolder();
        destinationRow.Controls.Add(_chooseDownloadFolder, 1, 0);
        ConfigureSourcePanel(_autoSourcePanel, _autoDownload,
            "PcNinja preserves the original filename and validates the completed download.", destinationRow);
        content.Controls.Add(_autoSourcePanel);

        _localIso.Text = "Use an ISO file already downloaded";
        StyleRadio(_localIso);
        var localRow = BuildTwoColumnRow();
        _localIsos.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_localIsos);
        _localIsos.DropDown += (_, _) => _localIso.Checked = true;
        StyleSecondaryButton(_browse, "Browse...");
        _browse.Click += (_, _) => BrowseIso();
        localRow.Controls.Add(_localIsos, 0, 0);
        localRow.Controls.Add(_browse, 1, 0);
        ConfigureSourcePanel(_localSourcePanel, _localIso,
            "Select the exact ISO file. The assistant never chooses a broad wildcard match.", localRow);
        content.Controls.Add(_localSourcePanel);

        _mountedIso.Text = "Use mounted media or an extracted setup folder";
        StyleRadio(_mountedIso);
        var mountedRow = BuildTwoColumnRow();
        _mountedSources.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_mountedSources);
        _mountedSources.DropDown += (_, _) => _mountedIso.Checked = true;
        mountedRow.Controls.Add(_mountedSources, 0, 0);
        StyleSecondaryButton(_browseInstallationFolder, "Browse folder...");
        _browseInstallationFolder.Click += (_, _) => BrowseInstallationFolder();
        mountedRow.Controls.Add(_browseInstallationFolder, 1, 0);
        ConfigureSourcePanel(_mountedSourcePanel, _mountedIso,
            "Select mounted media or a folder containing setup.exe and the sources directory.", mountedRow);
        content.Controls.Add(_mountedSourcePanel);

        var footer = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = Surface,
            Margin = new Padding(0),
            Padding = new Padding(0, 10, 0, 0)
        };
        footer.Controls.Add(DividerLine());
        footer.Controls.Add(Kicker("MANUAL DOWNLOAD FALLBACKS"));
        var downloadActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Surface,
            Margin = new Padding(0, 7, 0, 0)
        };
        StyleSecondaryButton(_pcNinjaDownload, "Open PcNinja download page");
        _pcNinjaDownload.Margin = new Padding(0, 0, 8, 0);
        _pcNinjaDownload.Click += (_, _) => OpenPage(UpgradeEngine.PcNinjaBrowserFallbackPage);
        StyleSecondaryButton(_microsoft, "Open Microsoft download page");
        _microsoft.Margin = new Padding(0);
        _microsoft.Click += (_, _) => OpenPage(UpgradeEngine.MicrosoftDownloadPage);
        downloadActions.Controls.Add(_pcNinjaDownload);
        downloadActions.Controls.Add(_microsoft);
        footer.Controls.Add(downloadActions);

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Surface,
            Margin = new Padding(0)
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardLayout.Controls.Add(scrollHost, 0, 0);
        cardLayout.Controls.Add(footer, 0, 1);
        card.Controls.Add(cardLayout);

        return card;
    }

    private Control BuildReadinessCard()
    {
        var card = CreateCard();
        var scrollHost = CreateScrollHost();

        var content = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = Surface,
            Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0),
            Margin = new Padding(0)
        };
        scrollHost.Controls.Add(content);

        content.Controls.Add(Kicker("THIS COMPUTER"));
        content.Controls.Add(SectionHeading("Upgrade readiness"));

        var computer = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            BackColor = SurfaceRaised,
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 12)
        };
        computer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _systemValue.AutoSize = true;
        _systemValue.MaximumSize = new Size(272, 0);
        _systemValue.Text = "Detecting Windows version...";
        _systemValue.Font = new Font("Segoe UI Semibold", 10F);
        _systemValue.ForeColor = Foreground;
        _systemValue.BackColor = Color.Transparent;
        _systemValue.Margin = new Padding(0, 0, 0, 7);
        computer.Controls.Add(_systemValue);

        ConfigureSystemDetailValue(_processorValue, "Detecting processor...");
        ConfigureSystemDetailValue(_memoryValue, "Detecting memory...");
        ConfigureSystemDetailValue(_systemStorage, "Checking storage...");
        computer.Controls.Add(SystemDetailRow("CPU", _processorValue));
        computer.Controls.Add(SystemDetailRow("Memory", _memoryValue));
        computer.Controls.Add(SystemDetailRow("System drive", _systemStorage));
        content.Controls.Add(computer);

        content.Controls.Add(ReadinessRow("Installation source", _sourceSummary));
        content.Controls.Add(ReadinessRow("Validation", ValueLabel("Required before Setup", AccentLight)));
        content.Controls.Add(ReadinessRow("Restart policy", _restartSummary));
        content.Controls.Add(DividerLine());

        _autoRestart.Text = "Allow Windows Setup to restart automatically";
        _autoRestart.Checked = true;
        _autoRestart.AutoSize = true;
        _autoRestart.ForeColor = Foreground;
        _autoRestart.BackColor = Color.Transparent;
        _autoRestart.Margin = new Padding(0, 2, 0, 2);
        content.Controls.Add(_autoRestart);
        content.Controls.Add(BodyText(
            "Turn off to restart manually after Setup finishes.",
            22, 280));

        var warning = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            MaximumSize = new Size(340, 0),
            Text = "Experimental compatibility method\nBack up important files before starting the upgrade.",
            ForeColor = Warning,
            BackColor = Color.FromArgb(53, 45, 43),
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 9, 0, 8)
        };
        content.Controls.Add(warning);

        card.Controls.Add(scrollHost);

        return card;
    }

    private Control BuildStatusBar()
    {
        var bar = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Background,
            Margin = new Padding(0)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _status.AutoEllipsis = true;
        _status.Height = 24;
        _status.Dock = DockStyle.Fill;
        _status.Text = "Ready.";
        _status.ForeColor = Muted;
        _status.BackColor = Color.Transparent;
        _status.Margin = new Padding(1, 3, 12, 0);
        bar.Controls.Add(_status, 0, 0);

        var footer = new LinkLabel
        {
            AutoSize = true,
            Text = $"Version 1.1.0-rc.4  ·  View log",
            LinkColor = AccentLight,
            ActiveLinkColor = Accent,
            VisitedLinkColor = AccentLight,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 3, 0, 0)
        };
        footer.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(
            "explorer.exe", $"/select,\"{AppLog.Path}\"")
        { UseShellExecute = true });
        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        _details.Text = "View details";
        _details.AutoSize = true;
        _details.Visible = false;
        _details.LinkColor = AccentLight;
        _details.Margin = new Padding(0, 3, 14, 0);
        _details.LinkClicked += (_, _) => MessageBox.Show(this, _lastErrorDetails ?? "No details available.",
            "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        links.Controls.Add(_details);
        links.Controls.Add(footer);
        bar.Controls.Add(links, 1, 0);
        return bar;
    }

    private void RefreshSources(bool initial)
    {
        var selectedLocal = _localIsos.Text;
        var selectedMounted = _mountedSources.SelectedItem?.ToString();
        var selectedDownload = _downloadLocations.SelectedItem as StorageLocation;
        var selectedDownloadRoot = selectedDownload?.RootPath;
        try
        {
            _engine.ValidateSupportedComputer();
            _systemValue.Text = _engine.GetOperatingSystemDescription();
            _processorValue.Text = GetProcessorDescription();
            _memoryValue.Text = GetMemoryDescription();
            var systemStorage = _engine.CheckSystemDriveSpace();
            _systemStorage.Text = systemStorage.Message;
            _systemStorage.ForeColor = systemStorage.IsValid ? Success : Warning;

            var mounted = _engine.FindMountedSources();
            _mountedSources.Items.Clear();
            foreach (var source in mounted) _mountedSources.Items.Add(source.Root);
            if (!string.IsNullOrWhiteSpace(selectedMounted) && Directory.Exists(selectedMounted) &&
                File.Exists(Path.Combine(selectedMounted, "setup.exe")) &&
                !_mountedSources.Items.Contains(selectedMounted))
                _mountedSources.Items.Insert(0, selectedMounted);
            SelectText(_mountedSources, selectedMounted);

            var local = _engine.FindLocalIsos().ToList();
            if (!string.IsNullOrWhiteSpace(selectedLocal) && File.Exists(selectedLocal) &&
                !local.Contains(selectedLocal, StringComparer.OrdinalIgnoreCase)) local.Insert(0, selectedLocal);
            _localIsos.Items.Clear();
            foreach (var path in local) _localIsos.Items.Add(path);
            if (!string.IsNullOrWhiteSpace(selectedLocal)) _localIsos.Text = selectedLocal;
            else if (_localIsos.Items.Count > 0) _localIsos.SelectedIndex = 0;

            var locations = _engine.FindDownloadLocations().ToList();
            if (selectedDownload?.VolumeLabel == "Selected folder" && Directory.Exists(selectedDownloadRoot))
            {
                var drive = new DriveInfo(Path.GetPathRoot(selectedDownloadRoot!)!);
                if (drive.IsReady && UpgradeEngine.IsEligibleDownloadLocation(drive.DriveFormat, drive.AvailableFreeSpace))
                    locations.Insert(0, selectedDownload with { AvailableBytes = drive.AvailableFreeSpace });
            }
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
            _status.Text = systemStorage.IsValid
                ? $"Refresh complete: {local.Count} local ISO file(s), {mounted.Count} mounted source(s), {locations.Count} download drive(s)."
                : $"Storage action required: {systemStorage.Message}";
            // Keep Start clickable so a failed readiness check produces a clear
            // explanation instead of leaving a green button that appears broken.
            // StartUpgradeAsync repeats the check and safely blocks the upgrade.
            _start.Enabled = true;
            AppLog.Info($"Sources refreshed. OS: {_systemValue.Text}. Local ISO: {local.Count}. Mounted: {mounted.Count}. Drives: {locations.Count}.");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex.ToString());
            _status.Text = ex.Message;
            _start.Enabled = false;
            ShowIssue(ex);
        }
        UpdateSourceControls();
        UpdateReadinessSummary();
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

    private void BrowseInstallationFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a mounted Windows source or extracted folder containing setup.exe",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var source = _engine.ValidateMountedSource(dialog.SelectedPath);
            if (!_mountedSources.Items.Contains(source.Root)) _mountedSources.Items.Insert(0, source.Root);
            _mountedSources.SelectedItem = source.Root;
            _mountedIso.Checked = true;
        }
        catch (Exception ex)
        {
            ShowIssue(ex);
        }
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
            var check = _engine.CheckDownloadDestination(dialog.SelectedPath);
            if (!check.IsValid) throw new InvalidOperationException(check.Message);
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
            ShowIssue(ex);
        }
    }

    private async Task StartOrCancelAsync()
    {
        AppLog.Info($"Start/Cancel button clicked. Operation active: {_operation is not null}. Setup started: {_setupHasStarted}.");
        if (_operation is not null)
        {
            if (_setupHasStarted) return;
            _status.Text = "Cancelling the current operation...";
            _start.Text = "CANCELLING...";
            _start.Enabled = false;
            _operation.Cancel();
            return;
        }

        await StartUpgradeAsync();
    }

    private async Task StartUpgradeAsync()
    {
        _lastErrorDetails = null;
        _details.Visible = false;
        _operation = new CancellationTokenSource();
        _setupHasStarted = false;
        SetBusy(true);
        try
        {
            var systemStorage = _engine.CheckSystemDriveSpace();
            if (!systemStorage.IsValid) throw new InvalidOperationException(systemStorage.Message);

            MountedSource source;
            if (_mountedIso.Checked)
            {
                _status.Text = "Validating the selected Windows installation source...";
                if (_mountedSources.SelectedItem is not string root)
                    throw new InvalidOperationException(
                        "No mounted media or extracted setup folder was selected. Click Refresh or Browse folder.");
                source = await _engine.ValidateMountedSourceAsync(root, _operation.Token);
            }
            else
            {
                string isoPath;
                if (_localIso.Checked)
                {
                    isoPath = _localIsos.Text.Trim().Trim('"');
                    _engine.ValidateIsoFile(isoPath);
                    _status.Text = "Checking the ISO checksum...";
                    _progress.Style = ProgressBarStyle.Marquee;
                    _progress.Visible = true;
                    await UpgradeEngine.VerifyIsoHashAsync(isoPath, UpgradeEngine.ConfiguredIsoSha256, _operation.Token);
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
                    var downloadOperation = _operation;
                    var progress = new Progress<DownloadProgress>(value =>
                    {
                        if (!ReferenceEquals(_operation, downloadOperation) || downloadOperation.IsCancellationRequested) return;
                        if (value.Stage is not null)
                        {
                            _status.Text = value.Stage;
                            _progress.Style = ProgressBarStyle.Marquee;
                            return;
                        }
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
                        _localIso.Checked = true;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (DownloadTransferException ex)
                    {
                        AppLog.Error($"Automatic download failed: {ex}");
                        var browserOpened = false;
                        try
                        {
                            UpgradeEngine.OpenPcNinjaDownloadPage();
                            browserOpened = true;
                            _localIso.Checked = true;
                        }
                        catch (Exception browserError) { AppLog.Error($"Browser fallback failed: {browserError}"); }
                        throw new InvalidOperationException(browserOpened
                            ? "Download interrupted. Choose automatic download to retry, or select the browser download."
                            : "Download interrupted. Retry, or download manually and select the ISO.", ex);
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
                ? "Your computer may restart automatically."
                : "You will restart the computer manually.";
            var finalStorage = _engine.CheckSystemDriveSpace();
            if (!finalStorage.IsValid) throw new InvalidOperationException(finalStorage.Message);
            var confirmation = MessageBox.Show(
                $"Save your work before continuing.\n{restartText}\n\nStart the Windows upgrade?",
                "Start upgrade", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                _status.Text = "Upgrade cancelled. Windows Setup was not started.";
                return;
            }

            _status.Text = "Windows Setup is running. Do not close the assistant.";
            _setupHasStarted = true;
            SetBusy(true);
            var result = await _engine.StartUpgradeAsync(source.SetupPath, _autoRestart.Checked, _operation.Token);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Windows Setup stopped with exit code {result.ExitCode}. No forced restart was issued.");

            _status.Text = "Windows Setup completed its current stage successfully.";
            MessageBox.Show(
                _autoRestart.Checked
                    ? "Setup finished this stage. Windows will handle the restart."
                    : "Setup finished this stage. Restart when you are ready.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            ShowIssue(ex);
        }
        finally
        {
            _operation?.Dispose();
            _operation = null;
            _setupHasStarted = false;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Visible = false;
            SetBusy(false);
            var outcome = _status.Text;
            RefreshSources(false);
            _status.Text = outcome;
        }
    }

    private void ShowIssue(Exception error)
    {
        AppLog.Error(error.ToString());
        _lastErrorDetails = error + $"\n\nLog: {AppLog.Path}";
        _details.Visible = true;
        _status.Text = error is IOException and not DownloadTransferException
            ? "Could not read or save the file. Check the drive and available space."
            : error.Message;
    }

    private void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ShowIssue(ex); }
    }

    private void SetBusy(bool busy)
    {
        if (!busy)
        {
            StyleGoButton(_start, "START UPGRADE");
            _start.Enabled = true;
        }
        else if (_setupHasStarted)
        {
            StyleRunningButton(_start, "SETUP RUNNING");
            _start.Enabled = false;
        }
        else
        {
            StyleCancelButton(_start, "CANCEL");
            _start.Enabled = true;
        }
        _refresh.Enabled = !busy;
        _browse.Enabled = !busy;
        _browseInstallationFolder.Enabled = !busy;
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

    private void SourceSelectionChanged(RadioButton selectedSource)
    {
        if (_changingSourceSelection) return;
        _changingSourceSelection = true;
        try
        {
            if (selectedSource.Checked)
            {
                foreach (var source in new[] { _autoDownload, _localIso, _mountedIso })
                    if (!ReferenceEquals(source, selectedSource)) source.Checked = false;
            }
        }
        finally
        {
            _changingSourceSelection = false;
        }
        UpdateSourceControls();
        UpdateReadinessSummary();
    }

    private void UpdateSourceControls()
    {
        var idle = _operation is null;
        _browse.Enabled = idle;
        _localIsos.Enabled = idle;
        _downloadLocations.Enabled = idle;
        _chooseDownloadFolder.Enabled = idle;
        _mountedSources.Enabled = idle && _mountedIso.Checked;
        _browseInstallationFolder.Enabled = idle;
        _mountedIso.Enabled = idle;

        _autoSourcePanel.BackColor = _autoDownload.Checked ? AccentTint : SurfaceRaised;
        _localSourcePanel.BackColor = _localIso.Checked ? AccentTint : SurfaceRaised;
        _mountedSourcePanel.BackColor = _mountedIso.Checked ? AccentTint : SurfaceRaised;
        _autoDownload.Text = _autoDownload.Checked
            ? "✓ SELECTED  ·  Download the ISO automatically"
            : "Download the ISO automatically";
        _localIso.Text = _localIso.Checked
            ? "✓ SELECTED  ·  Use an ISO file already downloaded"
            : "Use an ISO file already downloaded";
        _mountedIso.Text = _mountedIso.Checked
            ? "✓ SELECTED  ·  Use mounted media or an extracted setup folder"
            : "Use mounted media or an extracted setup folder";
        _autoDownload.ForeColor = _autoDownload.Checked ? Success : Foreground;
        _localIso.ForeColor = _localIso.Checked ? Success : Foreground;
        _mountedIso.ForeColor = _mountedIso.Checked ? Success : Foreground;
    }

    private void UpdateReadinessSummary()
    {
        if (_mountedIso.Checked)
        {
            var mounted = _mountedSources.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(mounted)) _sourceSummary.Text = "Mounted media / folder";
            else
            {
                var sourceRoot = Path.GetFullPath(mounted);
                var driveRoot = Path.GetPathRoot(sourceRoot);
                _sourceSummary.Text = string.Equals(
                    Path.TrimEndingDirectorySeparator(sourceRoot),
                    Path.TrimEndingDirectorySeparator(driveRoot ?? ""),
                    StringComparison.OrdinalIgnoreCase)
                    ? $"Mounted: {mounted}"
                    : $"Folder: {mounted}";
            }
        }
        else if (_localIso.Checked)
        {
            var path = _localIsos.Text.Trim().Trim('"');
            _sourceSummary.Text = string.IsNullOrWhiteSpace(path) ? "Local ISO" : Path.GetFileName(path);
        }
        else
        {
            _sourceSummary.Text = _downloadLocations.SelectedItem is StorageLocation location
                ? $"Automatic · {location.RootPath}"
                : "Automatic download";
        }

        _restartSummary.Text = _autoRestart.Checked ? "Automatic when required" : "Manual / noreboot";
        _restartSummary.ForeColor = _autoRestart.Checked ? AccentLight : Warning;
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

    private static Panel CreateCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(20)
        };
        card.Paint += (_, e) =>
        {
            var rectangle = new Rectangle(0, 0, Math.Max(0, card.Width - 1), Math.Max(0, card.Height - 1));
            using var pen = new Pen(Divider);
            e.Graphics.DrawRectangle(pen, rectangle);
        };
        return card;
    }

    private static Panel CreateScrollHost() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = Surface,
        Margin = new Padding(0)
    };

    private static void ConfigureSourcePanel(TableLayoutPanel panel, RadioButton radio, string description, Control selector)
    {
        panel.AutoSize = true;
        panel.Dock = DockStyle.Top;
        panel.ColumnCount = 1;
        panel.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        panel.BackColor = SurfaceRaised;
        panel.Padding = new Padding(12, 7, 12, 8);
        panel.Margin = new Padding(0, 6, 0, 0);
        panel.Controls.Add(radio);
        panel.Controls.Add(BodyText(description, 22, 470));
        selector.Margin = new Padding(22, 7, 0, 0);
        panel.Controls.Add(selector);
    }

    private static TableLayoutPanel BuildTwoColumnRow()
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return row;
    }

    private static Control ReadinessRow(string title, Label value)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = false,
            Height = 27,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            BackColor = Surface,
            Padding = new Padding(0, 4, 0, 4),
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            ForeColor = Muted,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 8, 0)
        }, 0, 0);
        value.AutoSize = false;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleRight;
        value.MaximumSize = Size.Empty;
        value.Margin = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);
        row.Controls.Add(value, 1, 0);
        return row;
    }

    private static Control SystemDetailRow(string title, Label value)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            BackColor = SurfaceRaised,
            Padding = new Padding(0, 1, 0, 1),
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            ForeColor = Muted,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 8, 0)
        }, 0, 0);
        value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        value.MaximumSize = new Size(197, 0);
        row.Controls.Add(value, 1, 0);
        return row;
    }

    private static void ConfigureSystemDetailValue(Label label, string text)
    {
        label.AutoSize = true;
        label.Text = text;
        label.ForeColor = AccentLight;
        label.BackColor = Color.Transparent;
        label.Margin = new Padding(0, 2, 0, 0);
    }

    private static string GetProcessorDescription()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("ProcessorNameString") is string processorName &&
                !string.IsNullOrWhiteSpace(processorName))
            {
                return string.Join(" ", processorName.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }
        catch
        {
            // Fall back to a useful value even when the registry cannot be queried.
        }

        return $"{Environment.ProcessorCount} logical processor(s)";
    }

    private static string GetMemoryDescription()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };
        return GlobalMemoryStatusEx(ref status)
            ? $"{UpgradeEngine.FormatBytes((long)status.TotalPhysical)} installed"
            : "Unavailable";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static Label ValueLabel(string text, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        BackColor = Color.Transparent
    };

    private static Label Kicker(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 8.5F),
        ForeColor = Accent,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4)
    };

    private static Label SectionHeading(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 17F),
        ForeColor = Foreground,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 4)
    };

    private static Label BodyText(string text, int leftMargin = 0, int maximumWidth = 520) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(maximumWidth, 0),
        Text = text,
        ForeColor = Muted,
        BackColor = Color.Transparent,
        Margin = new Padding(leftMargin, 0, 0, 2)
    };

    private static Panel DividerLine() => new()
    {
        Dock = DockStyle.Top,
        Height = 1,
        BackColor = Divider,
        Margin = new Padding(0, 14, 0, 14)
    };

    private static PictureBox CreateMascot(int size)
    {
        var logo = new PictureBox
        {
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PcNinja.Win11UpgradeAssistant.Assets.PcNinja-mascot.png");
            if (stream is not null)
            {
                using var image = Image.FromStream(stream);
                logo.Image = new Bitmap(image);
            }
        }
        catch { }
        return logo;
    }

    private static void StyleRadio(RadioButton radio, bool isChecked = false)
    {
        radio.AutoSize = true;
        radio.Checked = isChecked;
        radio.Font = new Font("Segoe UI Semibold", 9.5F);
        radio.ForeColor = Foreground;
        radio.BackColor = Color.Transparent;
        radio.Margin = new Padding(0, 0, 0, 3);
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.Dock = DockStyle.Fill;
        combo.BackColor = Background;
        combo.ForeColor = Foreground;
        combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 24;
        combo.IntegralHeight = false;
        combo.MaxDropDownItems = 8;
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? AccentTint : Background);
            e.Graphics.FillRectangle(background, e.Bounds);
            var textBounds = new Rectangle(e.Bounds.X + 7, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 7), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font,
                textBounds, Foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
    }

    private static void StylePrimaryButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(14, 7, 14, 7);
        button.BackColor = Surface;
        button.ForeColor = AccentLight;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Accent;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = AccentTint;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(66, 58, 106);
        button.UseVisualStyleBackColor = false;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0);
    }

    private static void StyleGoButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(14, 7, 14, 7);
        button.BackColor = Color.FromArgb(48, 142, 86);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Success;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(57, 163, 99);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(39, 119, 72);
        button.UseVisualStyleBackColor = false;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0);
    }

    private static void StyleCancelButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(14, 7, 14, 7);
        button.BackColor = Color.FromArgb(177, 55, 61);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(236, 119, 124);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(199, 67, 73);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(145, 42, 47);
        button.UseVisualStyleBackColor = false;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0);
    }

    private static void StyleRunningButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(14, 7, 14, 7);
        button.BackColor = SurfaceRaised;
        button.ForeColor = Muted;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Divider;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0);
    }

    private static void StyleSecondaryButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(9, 5, 9, 5);
        button.BackColor = SurfaceRaised;
        button.ForeColor = Foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Divider;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 54, 68);
        button.FlatAppearance.MouseDownBackColor = AccentTint;
        button.UseVisualStyleBackColor = false;
        button.Margin = new Padding(6, 0, 0, 0);
    }
}
