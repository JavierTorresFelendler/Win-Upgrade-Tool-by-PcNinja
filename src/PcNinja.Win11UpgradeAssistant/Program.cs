using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using PcNinja.Shared;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal static class Program
{
    /// <summary>Fixed shared single-instance id. Never version it.</summary>
    internal const string SingleInstanceAppId = "WinUpgradeTool";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals(AppUpdateInstaller.HelperArgument, StringComparison.Ordinal))
            return args.Length == 1 ? AppUpdateInstaller.RunHelperAsync().GetAwaiter().GetResult() : 2;
        AppLog.Info($"{ProductIdentity.Name}; build {ProductIdentity.BuildVersion}; " +
            $"mode: {(args.Length > 0 ? "CLI" : "GUI")}.");
        if (args.Length > 0)
        {
            AttachCommandLineConsole();
            return CliRunner.RunAsync(args).GetAwaiter().GetResult();
        }

        // GUI is single-instance per Windows session. A second launch asks the
        // running window to come forward and waits for its acknowledgement. The
        // probe runs before UAC, so an open app causes no second elevation prompt.
        PcNinjaSingleInstance.DiagnosticSink = AppLog.Info;
        var singleInstanceOptions = new PcNinjaSingleInstanceOptions();
        if (!IsAdministrator())
        {
            PcNinjaSingleInstance.TryActivateExisting(SingleInstanceAppId, singleInstanceOptions, out var probe);
            if (probe == PcNinjaSingleInstanceOutcome.ActivatedExisting) return 0;
            if (probe == PcNinjaSingleInstanceOutcome.ExistingNotResponding) return ShowAlreadyRunning();
            return RestartGuiAsAdministrator();
        }
        var singleInstance = PcNinjaSingleInstance.Acquire(SingleInstanceAppId, singleInstanceOptions, out var instance);
        if (singleInstance is null)
        {
            AppLog.Info($"Second GUI launch: {instance}.");
            return instance == PcNinjaSingleInstanceOutcome.ExistingNotResponding ? ShowAlreadyRunning() : 0;
        }
        if (instance == PcNinjaSingleInstanceOutcome.GuardUnavailable)
            AppLog.Info("Single-instance guard unavailable; starting without it (operations remain protected by AppOperationGate).");

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatal(e.ExceptionObject as Exception ?? new Exception("Unknown application error."));
        var readinessPipe = Environment.GetEnvironmentVariable(AppUpdateGuiReadiness.EnvironmentName);
        Environment.SetEnvironmentVariable(AppUpdateGuiReadiness.EnvironmentName, null);
        using var form = new MainForm();
        // Remove leftovers of a finished app update once the window is up.
        form.Shown += (_, _) => _ = Task.Run(AppUpdateInstaller.RemoveFinishedUpdateStagingAsync);
        if (AppUpdateGuiReadiness.IsValidName(readinessPipe))
            form.Shown += (_, _) => form.BeginInvoke(new Action(async () =>
            {
                if (form.IsDisposed || !form.Visible) return;
                form.Update(); // Paint the initialized window before acknowledging startup.
                await AppUpdateGuiReadiness.SignalAsync(readinessPipe);
            }));
        try { Application.Run(form); }
        finally
        {
            // Release ownership as soon as the window is gone, so a relaunch during
            // shutdown is handed over instead of waiting on a closing instance.
            singleInstance.Dispose();
        }
        return 0;
    }

    private static int ShowAlreadyRunning()
    {
        MessageBox.Show(
            $"{ProductIdentity.Name} is already running but is not responding.\n\n" +
            "Wait a moment and try again, or close it from Task Manager.",
            ProductIdentity.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("Run the app as administrator to continue.",
                ProductIdentity.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            "Something went wrong. Open View log for details and try again.",
            ProductIdentity.Name,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void AttachCommandLineConsole()
    {
        // Preserve redirected pipe/file handles when attaching to the caller's console.
        var input = GetStdHandle(-10);
        var output = GetStdHandle(-11);
        var error = GetStdHandle(-12);
        var redirectedInput = GetFileType(input) is 1 or 3;
        var redirectedOutput = GetFileType(output) is 1 or 3;
        var redirectedError = GetFileType(error) is 1 or 3;
        AttachConsole(unchecked((uint)-1));
        if (redirectedInput) SetStdHandle(-10, input);
        if (redirectedOutput) SetStdHandle(-11, output);
        if (redirectedError) SetStdHandle(-12, error);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new System.Text.UTF8Encoding(false)) { AutoFlush = true });
        Console.SetIn(new StreamReader(Console.OpenStandardInput()));
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int handle);
    [DllImport("kernel32.dll")]
    private static extern bool SetStdHandle(int handle, IntPtr value);
    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);
}
