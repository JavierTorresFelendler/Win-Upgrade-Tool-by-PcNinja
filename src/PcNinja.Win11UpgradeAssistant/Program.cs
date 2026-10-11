using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatal(e.ExceptionObject as Exception ?? new Exception("Unknown application error."));
        Application.Run(new MainForm());
    }

    private static void ShowFatal(Exception exception)
    {
        try { AppLog.Error(exception.ToString()); } catch { }
        MessageBox.Show(
            $"An unexpected error occurred.\n\n{exception.Message}\n\nLog: {AppLog.Path}",
            "PcNinja Windows 11 Upgrade Assistant",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
