using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using PcNinja.Win11Upgrade.Core;
using PcNinja.Win11UpgradeAssistant;

// Explicit opt-in test runner only. Starts real product GUIs/helpers in one
// uniquely owned temp directory; never invokes Windows Setup or any upgrade CLI.
internal static class PortableLifecycleChecks
{
    internal static async Task RunAsync(string rcPath, string stablePath, string newerPath)
    {
        using var identity = WindowsIdentity.GetCurrent();
        Require(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
            "Real portable lifecycle checks require an elevated disposable Windows environment.");
        var rc = AppUpdateWorkspace.InspectExecutable(rcPath);
        var stable = AppUpdateWorkspace.InspectExecutable(stablePath);
        var newer = AppUpdateWorkspace.InspectExecutable(newerPath);
        Require(ReleaseVersion.Parse(rc.Version).Prerelease.StartsWith("rc.", StringComparison.Ordinal) &&
            ReleaseVersion.Parse(stable.Version).IsStable && ReleaseVersion.Parse(newer.Version).IsStable &&
            ReleaseVersion.Parse(rc.Version).CompareTo(ReleaseVersion.Parse(stable.Version)) < 0 &&
            ReleaseVersion.Parse(stable.Version).CompareTo(ReleaseVersion.Parse(newer.Version)) < 0,
            "Lifecycle payloads must be an RC, its newer stable release and a still newer stable fixture.");

        var folder = Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var original = Path.Combine(folder, "Windows-Upgrade-Assistant-By-PcNinja-RC.exe");
            File.Copy(rcPath, original);
            using var parent = StartGui(original);
            await WaitForWindowAsync(parent);
            using var stableGui = await UpdateAsync(folder, parent, original, rc, stablePath, stable);
            await WaitForCleanupAsync(folder);
            VerifyFolder(folder, stable, rc);
            Console.WriteLine("PASS: Real single-file RC-to-stable helper READY/GO, parent exit, successor native GUI readiness, versioned archive and cleanup.");

            var current = Path.Combine(folder, AppUpdateIdentity.AssetName);
            using var newerGui = await UpdateAsync(folder, stableGui, current, stable, newerPath, newer);
            // Close immediately after the helper completes, without waiting for
            // cleanup. Its independent worker must survive the GUI's departure.
            Require(newerGui.CloseMainWindow(), "The successor GUI could not be closed after readiness.");
            await newerGui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            await WaitForCleanupAsync(folder);
            VerifyFolder(folder, newer, rc, stable);
            Console.WriteLine("PASS: Real repeated fixed-name single-file replacement, predecessor histories preserved and independent cleanup after immediate successor close. No Windows Setup or reboot.");
        }
        finally
        {
            ValidateFolder(folder);
            // Only processes executing from this exact random fixture folder
            // belong to this test. Preserve all unrelated applications.
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (path is null || !Path.GetFullPath(path).StartsWith(folder + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase)) continue;
                        if (!process.CloseMainWindow() || !process.WaitForExit(3000)) process.Kill();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    { /* An exact fixture process may already have exited. */ }
                }
            }
            // Image teardown/scanners can briefly retain a just-exited mapped
            // EXE. Retry only removal of our validated random temp fixture.
            for (var attempt = 0; ; attempt++)
            {
                ValidateFolder(folder);
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            throw new InvalidOperationException("Unexpected reparse point in lifecycle cleanup.");
                        if ((attributes & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                    }
                    Directory.Delete(folder, recursive: true);
                    break;
                }
                catch (Exception ex) when (attempt < 39 && ex is IOException or UnauthorizedAccessException)
                { await Task.Delay(250); }
            }
        }
    }

    private static async Task<Process> UpdateAsync(string folder, Process parent, string parentPath,
        AppExecutableInfo predecessor, string successorPath, AppExecutableInfo successor)
    {
        var target = Path.Combine(folder, AppUpdateIdentity.AssetName);
        var release = new AppUpdateManifest(1, AppUpdateIdentity.Product, AppUpdateIdentity.Repository, "stable",
            successor.Version, "v" + successor.Version, AppUpdateIdentity.AssetName,
            $"https://github.com/{AppUpdateIdentity.Repository}/releases/download/v{successor.Version}/{AppUpdateIdentity.AssetName}",
            successor.SizeBytes, successor.Sha256,
            $"https://github.com/{AppUpdateIdentity.Repository}/releases/tag/v{successor.Version}");
        using var gate = AppOperationGate.EnterUpdate();
        using (var workspace = AppUpdateWorkspace.CreateFixture(folder, Guid.NewGuid().ToString("N"),
            successor.SizeBytes, successor.SizeBytes))
        {
            File.Copy(successorPath, workspace.CandidatePath);
            workspace.PrepareHelper(release, predecessor.SignerThumbprint);
            workspace.WriteRequest(new AppUpdateRequest(2, Path.GetFileName(workspace.DirectoryPath), DateTimeOffset.UtcNow,
                parent.Id, parent.StartTime.ToUniversalTime().Ticks, parentPath, predecessor.Sha256,
                predecessor.SignerThumbprint, release, true, predecessor.FileVersion));
            var start = new ProcessStartInfo(workspace.HelperPath)
            {
                WorkingDirectory = workspace.DirectoryPath, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add(AppUpdateInstaller.HelperArgument);
            IsolateFixtureHttp(start);
            using var helper = Process.Start(start) ?? throw new Exception("Fixture helper did not launch.");
            workspace.MarkHandoffStarted();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            Require(await AppUpdateInstaller.ReadHandshakeAsync(helper.StandardOutput, timeout.Token) ==
                "PCNINJA_APP_UPDATE_READY_V2", "Real helper did not acknowledge READY.");
            gate.TransferToHelper();
            await helper.StandardInput.WriteLineAsync("PCNINJA_APP_UPDATE_GO_V2");
            helper.StandardInput.Close();
            Require(parent.CloseMainWindow(), "The fixture parent GUI did not accept close after GO.");
            await parent.WaitForExitAsync(timeout.Token);
            await helper.WaitForExitAsync(timeout.Token);
            // The successor may inherit the helper's standard handles. Its GUI
            // stays alive, so pipe EOF is not a helper-completion signal.
            Require(helper.ExitCode == 0, $"Real helper failed ({helper.ExitCode}); see fixture diagnostics.");
            Require(workspace.ReadTransactionState<AppUpdateTransactionState>().Status == "ready",
                "Real helper did not persist readiness and completed predecessor archival.");
        }
        Require(AppUpdateWorkspace.InspectExecutable(target).Sha256 == successor.Sha256,
            "The actual successor bytes differ from the manifest.");
        return FindGui(target);
    }

    private static Process StartGui(string path)
    {
        var start = new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Normal
        };
        IsolateFixtureHttp(start);
        return Process.Start(start) ?? throw new Exception("Fixture GUI did not start.");
    }

    private static void IsolateFixtureHttp(ProcessStartInfo start)
    {
        // Once this stable version is published, its synthetic RC predecessor
        // would otherwise see a real update and open a consent dialog. These
        // fixture GUIs/helpers use only prepared local bytes, never a live feed.
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
            start.Environment[name] = "http://127.0.0.1:1";
        start.Environment["NO_PROXY"] = "";
    }

    private static async Task WaitForWindowAsync(Process process)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            process.Refresh();
            Require(!process.HasExited, "Fixture GUI exited before creating its window.");
            if (process.MainWindowHandle != IntPtr.Zero) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("Fixture GUI window was not created.");
    }

    private static Process FindGui(string path)
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero && string.Equals(process.MainModule?.FileName,
                    path, StringComparison.OrdinalIgnoreCase)) return process;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            process.Dispose();
        }
        throw new Exception("Ready successor GUI was not found at its exact fixture path.");
    }

    private static async Task WaitForCleanupAsync(string folder)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(135))
        {
            if (!Directory.Exists(Path.Combine(folder, AppUpdateWorkspace.StateDirectoryName))) return;
            await Task.Delay(500);
        }
        throw new TimeoutException("The independent cleanup worker did not remove completed staging.");
    }

    private static void VerifyFolder(string folder, AppExecutableInfo current, params AppExecutableInfo[] predecessors)
    {
        Require(AppUpdateWorkspace.InspectExecutable(Path.Combine(folder, AppUpdateIdentity.AssetName)).Sha256 == current.Sha256,
            "Canonical current executable changed.");
        foreach (var predecessor in predecessors)
            Require(AppUpdateWorkspace.InspectExecutable(Path.Combine(folder,
                AppUpdateWorkspace.ArchiveName(predecessor.FileVersion))).Sha256 == predecessor.Sha256,
                "A distinct predecessor archive was lost or modified.");
        Require(Directory.GetFileSystemEntries(folder).Length == predecessors.Length + 1,
            "Finished lifecycle left files other than the current executable and distinct predecessor archives.");
    }

    private static void ValidateFolder(string folder)
    {
        Require(Path.GetDirectoryName(Path.GetFullPath(folder)) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) &&
            Path.GetFileName(folder).StartsWith("PcNinja-AppUpdate-Fixture-", StringComparison.Ordinal),
            "Unsafe lifecycle cleanup target.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
