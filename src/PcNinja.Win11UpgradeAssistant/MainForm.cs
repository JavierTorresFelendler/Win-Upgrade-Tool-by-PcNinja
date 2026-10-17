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
    private Label _automaticDownloadInfo = null!;
    private bool _automaticDownloadAvailable;
    private readonly RadioButton _existingMedia = new();
    private readonly ComboBox _sources = new();
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
    private readonly Label _operationNotice = new();
    private readonly LinkLabel _details = new();
    private Func<string, string, bool> _confirmUpgrade;
    private Action<string> _showRestartNotice;
    private string? _lastErrorDetails;
    private readonly TableLayoutPanel _autoSourcePanel = new();
    private readonly TableLayoutPanel _existingSourcePanel = new();
    private CancellationTokenSource? _operation;
    private bool _setupHasStarted;
    private bool _changingSourceSelection;
    private bool _refreshingSources;
    private readonly ComboBox _installationImages = new();
    private readonly Button _inspectSource = new();
    private readonly Label _imageInfo = new();
    private MountedSource? _preparedSource;
    private string? _preparedKey;

    public MainForm()
    {
        Text = "PcNinja Windows 11 Upgrade Assistant - RC5";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 700);
        Size = new Size(1160, 800);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Background;
        ForeColor = Foreground;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        _confirmUpgrade = (message, title) => MessageBox.Show(this, message, title,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        _showRestartNotice = message => MessageBox.Show(this, message, "Restart required",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        _existingMedia.CheckedChanged += (_, _) => SourceSelectionChanged(_existingMedia);
        _autoRestart.CheckedChanged += (_, _) => UpdateReadinessSummary();
        _sources.SelectedIndexChanged += (_, _) => ExistingPathChanged();
        _sources.TextChanged += (_, _) => ExistingPathChanged();
        _installationImages.SelectedIndexChanged += (_, _) => UpdateImageInfo();
        _downloadLocations.SelectedIndexChanged += (_, _) => UpdateReadinessSummary();

        ApplyDownloadLanguagePolicy(CurrentWindows.Read().Language);
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
        content.Controls.Add(SectionHeading("Choose Windows installation media"));
        content.Controls.Add(BodyText("Download automatically, or use installation media you already have."));

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
        _automaticDownloadInfo = ConfigureSourcePanel(_autoSourcePanel, _autoDownload,
            "PcNinja preserves the original filename and validates the completed download.", destinationRow);
        content.Controls.Add(_autoSourcePanel);

        _existingMedia.Text = "Use existing installation media";
        StyleRadio(_existingMedia);
        var existingRow = BuildTwoColumnRow();
        _sources.DropDownStyle = ComboBoxStyle.DropDown;
        StyleCombo(_sources);
        _sources.DropDownWidth = 650;
        _sources.DropDown += (_, _) => _existingMedia.Checked = true;
        StyleSecondaryButton(_browse, "ISO...");
        _browse.Click += (_, _) => BrowseIso();
        StyleSecondaryButton(_browseInstallationFolder, "Folder...");
        _browseInstallationFolder.Click += (_, _) => BrowseInstallationFolder();
        var browseActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        browseActions.Controls.Add(_browse);
        browseActions.Controls.Add(_browseInstallationFolder);
        existingRow.Controls.Add(_sources, 0, 0);
        existingRow.Controls.Add(browseActions, 1, 0);
        ConfigureSourcePanel(_existingSourcePanel, _existingMedia,
            "Choose an ISO, mounted drive, extracted folder or network setup folder.", existingRow);
        content.Controls.Add(_existingSourcePanel);

        content.Controls.Add(DividerLine());
        content.Controls.Add(Kicker("INSTALLATION IMAGE"));
        var imageRow = BuildTwoColumnRow();
        _installationImages.DropDownStyle = ComboBoxStyle.DropDownList;
        StyleCombo(_installationImages);
        _installationImages.DropDownWidth = 700;
        imageRow.Controls.Add(_installationImages, 0, 0);
        StyleSecondaryButton(_inspectSource, "Inspect images");
        _inspectSource.Click += async (_, _) => { if (_operation is null) await PrepareOrUpgradeAsync(false); };
        imageRow.Controls.Add(_inspectSource, 1, 0);
        content.Controls.Add(imageRow);
        _imageInfo.AutoSize = true;
        _imageInfo.MaximumSize = new Size(550, 0);
        _imageInfo.ForeColor = Muted;
        _imageInfo.Text = "Images are read from your selected media before Setup starts.";
        _imageInfo.Margin = new Padding(0, 7, 0, 0);
        content.Controls.Add(_imageInfo);

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
        footer.Controls.Add(Kicker("MANUAL ISO DOWNLOADS"));
        footer.Controls.Add(BodyText("For standard Microsoft Windows media, prefer Microsoft. For PcNinja media, use PcNinja ISO downloads. Select a matching language, then choose ISO... or Folder... above."));
        var downloadActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Surface,
            Margin = new Padding(0, 7, 0, 0)
        };
        StyleSecondaryButton(_pcNinjaDownload, "PcNinja ISO downloads");
        _pcNinjaDownload.Margin = new Padding(0, 0, 8, 0);
        _pcNinjaDownload.Click += (_, _) => OpenPage(UpgradeEngine.PcNinjaBrowserFallbackPage);
        StyleSecondaryButton(_microsoft, "Microsoft ISO download");
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
        _operationNotice.AutoSize = true;
        _operationNotice.MaximumSize = new Size(340, 0);
        _operationNotice.Dock = DockStyle.Top;
        _operationNotice.ForeColor = Warning;
        _operationNotice.BackColor = Color.FromArgb(53, 45, 43);
        _operationNotice.Padding = new Padding(12);
        _operationNotice.Margin = new Padding(0, 4, 0, 8);
        _operationNotice.Visible = false;
        content.Controls.Add(_operationNotice);

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
            Text = $"Version 1.1.0-rc.5  ·  View log",
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
        var selected = _sources.Text.Trim();
        var location = _downloadLocations.SelectedItem as StorageLocation;
        try
        {
            _engine.ValidateSupportedComputer();
            ApplyDownloadLanguagePolicy(CurrentWindows.Read().Language);
            _systemValue.Text = _engine.GetOperatingSystemDescription();
            _processorValue.Text = GetProcessorDescription();
            _memoryValue.Text = GetMemoryDescription();
            var systemStorage = _engine.CheckSystemDriveSpace();
            _systemStorage.Text = systemStorage.Message;
            _systemStorage.ForeColor = systemStorage.IsValid ? Success : Warning;

            var local = _engine.FindLocalIsos();
            var mounted = _engine.FindMountedSources();
            var candidates = local.Concat(mounted.Select(source => source.Root)).ToList();
            if (!string.IsNullOrWhiteSpace(selected) && !candidates.Contains(selected, StringComparer.OrdinalIgnoreCase))
                candidates.Insert(0, selected); // Retain exact user input even if a drive was temporarily removed.
            _sources.BeginUpdate();
            _refreshingSources = true;
            _sources.Items.Clear();
            foreach (var path in candidates) _sources.Items.Add(path);
            _sources.Text = selected;
            if (initial && local.Count == 1)
            {
                _sources.Text = local[0];
                _existingMedia.Checked = true;
            }
            _sources.EndUpdate();
            _refreshingSources = false;

            var locations = _engine.FindDownloadLocations().ToList();
            if (location?.VolumeLabel == "Selected folder" && Directory.Exists(location.RootPath))
            {
                var drive = new DriveInfo(Path.GetPathRoot(location.RootPath)!);
                if (drive.IsReady && UpgradeEngine.IsSupportedDownloadFileSystem(drive.DriveFormat))
                    locations.Insert(0, location with { AvailableBytes = drive.AvailableFreeSpace });
            }
            _downloadLocations.Items.Clear();
            foreach (var item in locations) _downloadLocations.Items.Add(item);
            var match = locations.FindIndex(item => item.RootPath.Equals(location?.RootPath, StringComparison.OrdinalIgnoreCase));
            if (locations.Count > 0) _downloadLocations.SelectedIndex = match >= 0 ? match : 0;
            _status.Text = systemStorage.IsValid
                ? $"Refresh complete: {local.Count} ISO(s), {mounted.Count} mounted source(s), {locations.Count} download drive(s)."
                : systemStorage.Message;
            if (initial && UpgradeHistory.ReadStatus() is { } previous)
                _status.Text = $"Previous upgrade: {previous.State}. {previous.Message}";
            _start.Enabled = true;
        }
        catch (Exception ex) { ShowIssue(ex); _start.Enabled = false; }
        ExistingPathChanged();
        UpdateSourceControls();
        UpdateReadinessSummary();
    }

    private void BrowseIso()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a Windows 10 or Windows 11 x64 ISO",
            Filter = "Windows ISO images (*.iso)|*.iso",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!_sources.Items.Contains(dialog.FileName)) _sources.Items.Insert(0, dialog.FileName);
        _sources.Text = dialog.FileName;
        _existingMedia.Checked = true;
    }

    private void BrowseInstallationFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a mounted drive or extracted Windows Setup folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _sources.Text = dialog.SelectedPath;
        _existingMedia.Checked = true;
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
            var check = _engine.CheckDownloadDestination(dialog.SelectedPath, 0);
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

        await PrepareOrUpgradeAsync(true);
    }

    private async Task PrepareOrUpgradeAsync(bool startSetup)
    {
        _lastErrorDetails = null;
        _details.Visible = false;
        _operationNotice.Visible = false;
        _operation = new CancellationTokenSource();
        _setupHasStarted = false;
        SetBusy(true);
        try
        {
            if (startSetup)
            {
                var storage = _engine.CheckSystemDriveSpace();
                if (!storage.IsValid) throw new InvalidOperationException(storage.Message);
            }
            var previousSource = _preparedSource;
            var previousKey = _preparedKey;
            var previousIndex = (_installationImages.SelectedItem as InstallationImage)?.Index;
            MountedSource source;
            MountedSource? automaticSource = null;
            var consentedRestart = _autoRestart.Checked;
            if (_autoDownload.Checked)
            {
                if (!startSetup) throw new InvalidOperationException("Choose existing media to inspect its images.");
                if (_downloadLocations.SelectedItem is not StorageLocation location)
                    throw new InvalidOperationException("Choose a download destination. Connect a drive and Refresh if necessary.");
                var destination = location.VolumeLabel == "Selected folder" ? location.RootPath
                    : Path.Combine(location.RootPath, "PcNinja Downloads");
                if (!ConfirmAutomaticUpgrade(destination, consentedRestart))
                {
                    _status.Text = "Upgrade cancelled. No download or Windows Setup was started.";
                    return;
                }
                _status.Text = "Checking or downloading the Windows ISO...";
                _progress.Visible = true;
                var operation = _operation;
                var progress = new Progress<DownloadProgress>(value =>
                {
                    if (!ReferenceEquals(_operation, operation) || operation.IsCancellationRequested) return;
                    _progress.Style = value.Percentage.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
                    if (value.Percentage.HasValue) _progress.Value = Math.Clamp(value.Percentage.Value, 0, 100);
                    _status.Text = value.Stage ?? $"Downloading {Path.GetFileName(value.DestinationPath)}... " +
                        (value.Percentage.HasValue ? $"{value.Percentage}%" : $"{UpgradeEngine.FormatBytes(value.BytesReceived)} received");
                });
                try
                {
                    var download = await _engine.DownloadIsoAsync(destination, progress, operation.Token);
                    automaticSource = download.ValidatedSource
                        ?? throw new InvalidOperationException("The downloaded ISO did not complete media validation.");
                    _sources.Text = download.IsoPath;
                    _existingMedia.Checked = true;
                }
                catch (DownloadTransferException ex)
                {
                    AppLog.Error($"Automatic download failed: {ex}");
                    var opened = false;
                    try { UpgradeEngine.OpenPcNinjaDownloadPage(); opened = true; }
                    catch (Exception browserError) { AppLog.Error($"Browser fallback failed: {browserError}"); }
                    _existingMedia.Checked = true;
                    _sources.SelectedIndex = -1;
                    _sources.Text = "";
                    throw new InvalidOperationException(opened
                        ? "Browser download opened. When it finishes, use ISO... to select the downloaded file, then Start."
                        : "Automatic download unavailable. Download manually, then use ISO... to select the file.", ex);
                }
            }
            var path = _sources.Text.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("No existing installation media selected. Choose an ISO, drive or folder.");
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            _status.Text = "Validating the selected installation media and images...";
            if (automaticSource is not null) source = automaticSource;
            else if (path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                _engine.ValidateIsoFile(path);
                await UpgradeEngine.VerifyIsoHashAsync(path, UpgradeEngine.ConfiguredIsoSha256, _operation.Token);
                source = await _engine.MountAndValidateAsync(path, _operation.Token);
            }
            else source = await _engine.ValidateMountedSourceAsync(path, _operation.Token);

            var sameSource = previousSource is not null && previousKey == path &&
                source.ImagePath == previousSource.ImagePath &&
                (source.Images ?? []).SequenceEqual(previousSource.Images ?? []);
            _preparedSource = source;
            _preparedKey = path;
            _installationImages.Items.Clear();
            foreach (var image in source.Images ?? []) _installationImages.Items.Add(image);
            var current = CurrentWindows.Read();
            var compatible = InstallationImages.Compatible(source, current);
            var index = sameSource && automaticSource is null ? previousIndex
                : compatible.Count == 1 ? compatible[0].Index : (int?)null;
            _installationImages.SelectedItem = (source.Images ?? []).FirstOrDefault(image => image.Index == index);
            UpdateImageInfo();
            _progress.Visible = false;
            if (!startSetup)
            {
                _status.Text = $"Read {source.Images?.Count} image(s). Select a matching image, then Start.";
                return;
            }
            if (_installationImages.SelectedItem is not InstallationImage selection)
            {
                _status.Text = compatible.Count == 0
                    ? $"No matching image for {current.Edition}, {current.Language}. Choose matching Windows media."
                    : "Choose an installation image above, then click START UPGRADE again.";
                ShowOperationNotice(_status.Text);
                return;
            }
            selection = InstallationImages.Select(source, selection.Index, current);
            if (!ConfirmPreparedUpgrade(source, selection, compatible.Count, automaticSource, consentedRestart))
            {
                _status.Text = "Upgrade cancelled. Windows Setup was not started.";
                return;
            }
            _operation.Token.ThrowIfCancellationRequested();
            _status.Text = "Rechecking safety and starting Windows Setup...";
            var automaticRestart = _autoRestart.Checked;
            var result = await _engine.StartUpgradeAsync(source, selection.Index, automaticRestart, _operation.Token,
                () => { _setupHasStarted = true; SetBusy(true); });
            PresentSetupResult(result, automaticRestart);
        }
        catch (OperationCanceledException) { _status.Text = "Operation cancelled."; }
        catch (Exception ex) { ShowIssue(ex); }
        finally
        {
            _operation?.Dispose();
            _operation = null;
            _setupHasStarted = false;
            _progress.Visible = false;
            _progress.Style = ProgressBarStyle.Continuous;
            SetBusy(false);
            var outcome = _status.Text;
            RefreshSources(false);
            _status.Text = outcome;
        }
    }

    private void PresentSetupResult(UpgradeResult result, bool automaticRestart)
    {
        if (result.NeedsManualRestartNotice(automaticRestart))
        {
            _status.Text = "Waiting for manual restart. Restart your computer to continue the upgrade.";
            ShowOperationNotice(UpgradeResult.ManualRestartMessage);
            _operationNotice.ForeColor = Success;
            _showRestartNotice(UpgradeResult.ManualRestartMessage);
        }
        else if (result.ExitCode == 0)
            _status.Text = "Setup finished this stage. Check the upgrade result after restarting.";
        else if (result.ExitCode == 0x40010004)
            _status.Text = "Setup exited during the transition. Outcome not confirmed; reopen the assistant after restarting.";
        else throw new InvalidOperationException(
            $"Setup returned 0x{unchecked((uint)result.ExitCode):X8}. See View details/log. No forced restart was issued.");
    }

    private bool ConfirmAutomaticUpgrade(string destination, bool restart) => _confirmUpgrade(
        $"Download or reuse the Windows ISO in:\n{destination}\n\n" +
        "Then validate it and start the compatible in-place upgrade automatically, keeping apps and personal files.\n" +
        (restart ? "Windows may restart automatically." : "Restart manually when Setup is ready.") +
        "\n\nExperimental compatibility method. Back up important files and save your work. Continue?",
        "Download and upgrade");

    private bool ConfirmPreparedUpgrade(MountedSource source, InstallationImage selection, int compatibleCount,
        MountedSource? automaticSource, bool consentedRestart)
    {
        // Consent applies only to the result returned during this automatic attempt.
        // A later manual Start, ambiguous image or changed restart policy must confirm again.
        if (ReferenceEquals(source, automaticSource) && compatibleCount == 1 &&
            InstallationImages.GetSetupProfile(selection) == WindowsSetupProfile.Windows11 &&
            (source.Images ?? []).Contains(selection) && _autoRestart.Checked == consentedRestart) return true;
        var restart = _autoRestart.Checked ? "Windows may restart automatically." : "Restart manually when Setup is ready.";
        var targetNotice = InstallationImages.GetSetupProfile(selection) == WindowsSetupProfile.Windows10
            ? $"\n\n{InstallationImages.Windows10Notice}" : "";
        return _confirmUpgrade($"{selection.Name} (image {selection.Index})\n{restart}{targetNotice}\n\nSave your work. Start the upgrade?",
            "Start upgrade");
    }

    private void ShowOperationNotice(string message)
    {
        _operationNotice.Text = message;
        _operationNotice.ForeColor = Warning;
        _operationNotice.Visible = true;
        if (_operationNotice.Parent?.Parent is ScrollableControl scrollHost)
            scrollHost.ScrollControlIntoView(_operationNotice);
    }

    private void ExistingPathChanged()
    {
        if (_refreshingSources) return;
        if (_sources.Focused && _operation is null) _existingMedia.Checked = true;
        if (_preparedKey is not null && !string.Equals(_sources.Text.Trim().Trim('"'), _preparedKey, StringComparison.OrdinalIgnoreCase))
        {
            _preparedSource = null;
            _preparedKey = null;
            _installationImages.Items.Clear();
            _imageInfo.Text = "Source changed. Inspect images or Start to validate this media.";
        }
        UpdateReadinessSummary();
    }

    private void UpdateImageInfo()
    {
        var current = CurrentWindows.Read();
        var incompatibility = _installationImages.SelectedItem is InstallationImage selected
            ? InstallationImages.GetIncompatibility(selected, current) : null;
        _imageInfo.ForeColor = incompatibility is not null ||
            _installationImages.SelectedItem is InstallationImage selectedImage &&
            InstallationImages.GetSetupProfile(selectedImage) == WindowsSetupProfile.Windows10 ? Warning : Muted;
        _imageInfo.Text = _installationImages.SelectedItem is InstallationImage image
            ? $"This PC: {current.Edition}, {current.Language}. " +
                (incompatibility ?? $"Selected image {image.Index}: {image.Language}, {image.Architecture}, image version {image.Version}." +
                    (InstallationImages.GetSetupProfile(image) == WindowsSetupProfile.Windows10
                        ? $" {InstallationImages.Windows10Notice}" : ""))
            : $"This PC: {current.Edition}, {current.Language}. Select an image after validation.";
    }

    private void ShowIssue(Exception error)
    {
        AppLog.Error(error.ToString());
        _lastErrorDetails = error + $"\n\nLog: {AppLog.Path}";
        _details.Visible = true;
        if (error is UnsafeInstallationMediaException)
        {
            _status.Text = "Upgrade blocked. Remove the interfering installation media, then Refresh. See View details.";
            ShowOperationNotice(_status.Text);
            MessageBox.Show(this, error.Message, "Unsafe installation media", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _status.Text = error is IOException and not DownloadTransferException and not CorruptDownloadException
            ? "Could not read or save the file. Check the drive and available space."
            : error.Message;
        ShowOperationNotice(_status.Text);
    }

    private void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ShowIssue(ex); }
    }

    private void SetBusy(bool busy)
    {
        if (!busy) { StyleGoButton(_start, "START UPGRADE"); _start.Enabled = true; }
        else if (_setupHasStarted) { StyleRunningButton(_start, "SETUP RUNNING"); _start.Enabled = false; }
        else { StyleCancelButton(_start, "CANCEL"); _start.Enabled = true; }
        _refresh.Enabled = !busy;
        _pcNinjaDownload.Enabled = !busy;
        _microsoft.Enabled = !busy;
        _autoDownload.Enabled = !busy;
        _existingMedia.Enabled = !busy;
        _autoRestart.Enabled = !busy;
        UpdateSourceControls();
    }

    private void SourceSelectionChanged(RadioButton selectedSource)
    {
        if (_changingSourceSelection) return;
        _changingSourceSelection = true;
        try
        {
            if (selectedSource.Checked)
                foreach (var source in new[] { _autoDownload, _existingMedia })
                    if (!ReferenceEquals(source, selectedSource)) source.Checked = false;
        }
        finally { _changingSourceSelection = false; }
        UpdateSourceControls();
        UpdateReadinessSummary();
    }

    private void UpdateSourceControls()
    {
        var idle = _operation is null;
        _autoDownload.Enabled = idle && _automaticDownloadAvailable;
        _browse.Enabled = idle;
        _browseInstallationFolder.Enabled = idle;
        _sources.Enabled = idle;
        _downloadLocations.Enabled = idle && _automaticDownloadAvailable;
        _chooseDownloadFolder.Enabled = idle && _automaticDownloadAvailable;
        _inspectSource.Enabled = idle && _existingMedia.Checked;
        _installationImages.Enabled = idle && _preparedSource is not null;
        _autoSourcePanel.BackColor = _autoDownload.Checked ? AccentTint : SurfaceRaised;
        _existingSourcePanel.BackColor = _existingMedia.Checked ? AccentTint : SurfaceRaised;
        _autoDownload.Text = _autoDownload.Checked ? "✓ SELECTED  ·  Download the ISO automatically" : "Download the ISO automatically";
        _existingMedia.Text = _existingMedia.Checked ? "✓ SELECTED  ·  Use existing installation media" : "Use existing installation media";
        _autoDownload.ForeColor = _autoDownload.Checked ? Success : Foreground;
        _existingMedia.ForeColor = _existingMedia.Checked ? Success : Foreground;
    }

    private void ApplyDownloadLanguagePolicy(string language)
    {
        _automaticDownloadAvailable = AutomaticDownloadPolicy.IsAvailable(language);
        _automaticDownloadInfo.Text = _automaticDownloadAvailable
            ? $"Automatic Windows 11 ISO: English ({AutomaticDownloadPolicy.ImageLanguage}). Original filename preserved; download validated."
            : $"Automatic download unavailable: this Windows uses {language}; the automatic ISO is {AutomaticDownloadPolicy.ImageLanguage}. " +
                "Download matching media using the links below, then choose ISO... or Folder....";
        _automaticDownloadInfo.ForeColor = _automaticDownloadAvailable ? Muted : Warning;
        if (!_automaticDownloadAvailable) _existingMedia.Checked = true;
        UpdateSourceControls();
    }

    private void UpdateReadinessSummary()
    {
        _sourceSummary.Text = _existingMedia.Checked
            ? (string.IsNullOrWhiteSpace(_sources.Text) ? "Existing ISO / drive / folder" : _sources.Text)
            : _downloadLocations.SelectedItem is StorageLocation location ? $"Automatic · {location.RootPath}" : "Automatic download";
        _restartSummary.Text = _autoRestart.Checked ? "Automatic when required" : "Manual / noreboot";
        _restartSummary.ForeColor = _autoRestart.Checked ? AccentLight : Warning;
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

    private static Label ConfigureSourcePanel(TableLayoutPanel panel, RadioButton radio, string description, Control selector)
    {
        panel.AutoSize = true;
        panel.Dock = DockStyle.Top;
        panel.ColumnCount = 1;
        panel.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        panel.BackColor = SurfaceRaised;
        panel.Padding = new Padding(12, 7, 12, 8);
        panel.Margin = new Padding(0, 6, 0, 0);
        panel.Controls.Add(radio);
        var explanation = BodyText(description, 22, 470);
        panel.Controls.Add(explanation);
        selector.Margin = new Padding(22, 7, 0, 0);
        panel.Controls.Add(selector);
        return explanation;
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
