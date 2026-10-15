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
        var start = (Button)Field("_start");
        if (!start.Enabled) throw new Exception("Start is disabled after startup on the test machine.");
        var choices = new[] { "_autoDownload", "_localIso", "_mountedIso" }.Select(name => (RadioButton)Field(name)).ToArray();
        foreach (var choice in choices)
        {
            choice.Checked = true;
            if (choices.Count(item => item.Checked) != 1) throw new Exception("Source choices are not exclusive.");
        }
        // Exercise the real Click event, using an empty source so Setup can NEVER start.
        ((ComboBox)Field("_mountedSources")).Items.Clear();
        start.PerformClick();
        Application.DoEvents();
        if (!((Label)Field("_status")).Text.Contains("No mounted media"))
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
        }
        finally { Directory.Delete(folder); }

        if (args.Length > 0)
        {
            Directory.CreateDirectory(args[0]);
            Invoke("RefreshSources", false);
            ((LinkLabel)Field("_details")).Visible = false;
            choices[0].Checked = true;
            Save("RC4-Ready.png");
            Invoke("SetBusy", true);
            Save("RC4-Cancel.png");
            Invoke("SetBusy", false);
            void Save(string name)
            {
                form.PerformLayout();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                image.Save(Path.Combine(args[0], name));
            }
        }
        Console.WriteLine("PASS: Real Start/Cancel clicks, Setup lockout, exclusive source selection, retained outcome and custom-folder refresh. No Setup launched.");
        return 0;
    }
}
