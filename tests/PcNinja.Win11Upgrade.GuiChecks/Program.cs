using System.Reflection;
using PcNinja.Win11Upgrade.Core;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var assembly = Assembly.Load("PcNinja-Win11-Upgrade-Assistant");
        var type = assembly.GetType("PcNinja.Win11UpgradeAssistant.MainForm", true)!;
        using var form = (Form)Activator.CreateInstance(type, nonPublic: true)!;
        object Field(string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        void SetField(string name, object? value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(form, value);
        void Invoke(string name, params object[] values) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, values);
        form.Show();
        Application.DoEvents();
        var actualLanguage = CurrentWindows.Read().Language;
        Invoke("ApplyDownloadLanguagePolicy", "en-US");
        var start = (Button)Field("_start");
        if (!start.Enabled) throw new Exception("Start is disabled after startup on the test machine.");
        var choices = new[] { "_autoDownload", "_existingMedia" }.Select(name => (RadioButton)Field(name)).ToArray();
        IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(control => new[] { control }.Concat(Descendants(control)));
        if (Descendants(form).OfType<RadioButton>().Count() != 2)
            throw new Exception("The GUI must contain exactly two primary source choices.");
        foreach (var choice in choices)
        {
            choice.Checked = true;
            if (choices.Count(item => item.Checked) != 1) throw new Exception("Source choices are not exclusive.");
            var automaticPanel = (Control)Field("_autoSourcePanel");
            var existingPanel = (Control)Field("_existingSourcePanel");
            if (automaticPanel.BackColor == existingPanel.BackColor)
                throw new Exception("The selected source is not uniquely highlighted.");
        }
        foreach (var language in new[] { "he-IL", "en-GB", "ru-RU" })
        {
            choices[0].Checked = true;
            Invoke("ApplyDownloadLanguagePolicy", language);
            Invoke("SetBusy", true);
            Invoke("SetBusy", false);
            var note = (Label)Field("_automaticDownloadInfo");
            if (choices[0].Enabled || choices[0].Checked || !choices[1].Checked ||
                ((ComboBox)Field("_downloadLocations")).Enabled || ((Button)Field("_chooseDownloadFolder")).Enabled ||
                !((Button)Field("_browse")).Enabled || !((Button)Field("_browseInstallationFolder")).Enabled ||
                !((Button)Field("_pcNinjaDownload")).Enabled || !((Button)Field("_microsoft")).Enabled ||
                !note.Text.Contains(language) || !note.Text.Contains("en-US"))
                throw new Exception("Language gating hid manual alternatives, lost its explanation, or re-enabled automatic download.");
        }
        Invoke("ApplyDownloadLanguagePolicy", "en-US");
        if (!choices[0].Enabled || !((ComboBox)Field("_downloadLocations")).Enabled)
            throw new Exception("Automatic download did not return for matching en-US media.");
        Invoke("ApplyDownloadLanguagePolicy", actualLanguage);
        // Exercise the real Click event, using an empty source so Setup can NEVER start.
        var sources = (ComboBox)Field("_sources");
        sources.Items.Clear();
        sources.Text = "";
        start.PerformClick();
        Application.DoEvents();
        if (!((Label)Field("_status")).Text.Contains("No existing installation media"))
            throw new Exception("Start click did not reach validation or its message was overwritten.");
        if (!start.Enabled) throw new Exception("Start was not restored after validation failure.");

        using var cancellation = new CancellationTokenSource();
        SetField("_operation", cancellation);
        Invoke("SetBusy", true);
        if (!start.Enabled || start.Text != "CANCEL") throw new Exception("Cancel button is unavailable.");
        start.PerformClick();
        if (!cancellation.IsCancellationRequested || start.Enabled || start.Text != "CANCELLING...")
            throw new Exception("Cancel click failed to signal cancellation and prevent repeated clicks.");
        SetField("_setupHasStarted", true);
        Invoke("SetBusy", true);
        if (start.Enabled || start.Text != "SETUP RUNNING") throw new Exception("Setup phase permits cancellation.");
        SetField("_operation", null);
        SetField("_setupHasStarted", false);
        Invoke("SetBusy", false);

        var folder = Path.Combine(Path.GetTempPath(), "PcNinja-Gui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(folder)!);
            var combo = (ComboBox)Field("_downloadLocations");
            combo.Items.Insert(0, new StorageLocation(folder, "Selected folder", drive.DriveType, drive.DriveFormat, drive.AvailableFreeSpace));
            combo.SelectedIndex = 0;
            Invoke("RefreshSources", false);
            if ((combo.SelectedItem as StorageLocation)?.RootPath != folder)
                throw new Exception("Refresh replaced the manually selected download folder.");

            sources.Text = Path.Combine(folder, "exact-selected.iso");
            var current = CurrentWindows.Read();
            var image = new InstallationImage(6, "Test Windows image", current.Edition, "x64", $"10.0.{current.Build}", "Client", current.Language);
            var prepared = new MountedSource(folder, Path.Combine(folder, "setup.exe"), Path.Combine(folder, "sources", "install.wim"), [image]);
            SetField("_preparedSource", prepared);
            SetField("_preparedKey", sources.Text);
            var images = (ComboBox)Field("_installationImages");
            images.Items.Add(image);
            images.SelectedIndex = 0;
            var otherLanguage = current.Language == "he-IL" ? "en-US" : "he-IL";
            images.Items.Add(image with { Index = 7, Language = otherLanguage });
            images.SelectedIndex = 1;
            var mismatchInfo = (Label)Field("_imageInfo");
            if (!mismatchInfo.Text.Contains(current.Language) || !mismatchInfo.Text.Contains(otherLanguage) ||
                !mismatchInfo.Text.Contains("Language mismatch") || mismatchInfo.ForeColor == form.ForeColor)
                throw new Exception("Language mismatch did not remain visible beside the selected image.");
            images.SelectedIndex = 0;
            ((CheckBox)Field("_autoRestart")).Checked = false;
            Invoke("RefreshSources", false);
            if (sources.Text != Path.Combine(folder, "exact-selected.iso") || !Equals(images.SelectedItem, image) ||
                ((CheckBox)Field("_autoRestart")).Checked)
                throw new Exception("Refresh lost the exact source, image or restart policy.");
            sources.Text = Path.Combine(folder, "different.iso");
            if (images.Items.Count != 0 || Field("_preparedSource") is not null)
                throw new Exception("Changing the source retained an old image index.");
        }
        finally { Directory.Delete(folder); }

        if (args.Length > 0)
        {
            Directory.CreateDirectory(args[0]);
            Invoke("RefreshSources", false);
            ((LinkLabel)Field("_details")).Visible = false;
            choices[0].Checked = true;
            Save("RC5-Ready.png");
            using var previewOperation = new CancellationTokenSource();
            SetField("_operation", previewOperation);
            Invoke("SetBusy", true);
            Save("RC5-Cancel.png");
            SetField("_operation", null);
            Invoke("SetBusy", false);
            void Save(string name)
            {
                form.PerformLayout();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(args[0], name));
            }
        }
        Console.WriteLine("PASS: Language-gated automatic download with manual alternatives; two exclusive highlighted sources; real Start/Cancel; Setup lockout; source/image/restart/folder refresh retention; image reset on source change. No Setup launched.");
        return 0;
    }
}
