using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
            return CliRunner.RunAsync(args).GetAwaiter().GetResult();

        FreeConsole();
        if (!IsAdministrator()) return RestartGuiAsAdministrator();

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatal(e.ExceptionObject as Exception ?? new Exception("Unknown application error."));
        Application.Run(new MainForm());
        return 0;
    }

    internal static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static int RestartGuiAsAdministrator()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath
                    ?? throw new InvalidOperationException("The application path could not be determined."),
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });
            return 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show("Administrator permission is required to mount an ISO and start Windows Setup.",
                "PcNinja Windows 11 Upgrade Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 5;
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
            return 1;
        }
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

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
}
