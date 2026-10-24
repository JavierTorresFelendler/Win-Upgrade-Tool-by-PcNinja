using PcNinja.Win11Upgrade.Core;
using PcNinja.Win11UpgradeAssistant;

internal static class AppUpdateHelperChecks
{
    internal static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var manifest = new AppUpdateManifest(1, AppUpdateIdentity.Product, AppUpdateIdentity.Repository,
            "stable", "9.9.9", "v9.9.9", AppUpdateIdentity.AssetName,
            $"https://github.com/{AppUpdateIdentity.Repository}/releases/download/v9.9.9/{AppUpdateIdentity.AssetName}",
            10, new string('a', 64), $"https://github.com/{AppUpdateIdentity.Repository}/releases/tag/v9.9.9");
        var request = new AppUpdateRequest(1, id, now, Environment.ProcessId + 10000,
            now.AddMinutes(-1).UtcDateTime.Ticks, @"C:\Fixture\app.exe", new string('a', 64),
            null, null, manifest, false);
        AppUpdateInstaller.ValidateRequest(request, id);
        foreach (var invalid in new[]
        {
            request with { SchemaVersion = 2 }, request with { OperationId = Guid.NewGuid().ToString("N") },
            request with { ParentProcessId = 0 }, request with { ParentProcessId = Environment.ProcessId },
            request with { ParentStartUtcTicks = 0 }, request with { ParentStartUtcTicks = long.MaxValue },
            request with { ParentStartUtcTicks = now.AddMinutes(1).UtcDateTime.Ticks },
            request with { CreatedUtc = now.AddMinutes(-6) }, request with { CreatedUtc = now.AddMinutes(1) },
            request with { ParentPath = "relative.exe" }, request with { ParentPath = @"\\server\share\app.exe" },
            request with { ParentPath = @"\\?\C:\app.exe" }, request with { ParentPath = @"C:\app.exe:ads" },
            request with { ParentPath = @"C:\app*.exe" }, request with { ParentPath = "C:\\bad\nname.exe" },
            request with { ParentHash = "" }, request with { TargetHash = new string('z', 64) },
            request with { RequiredSigner = new string('a', 39) }, request with { RequiredSigner = new string('z', 40) },
            request with { Release = manifest with { Repository = "other/project" } }
        }) Reject(() => AppUpdateInstaller.ValidateRequest(invalid, id));
        Reject(() => AppUpdateInstaller.ValidateRequest(request with { OperationId = new string('0', 32) }, new string('0', 32)));
        Require(AppUpdateInstaller.ReadHandshakeAsync(new StringReader("READY\r\n"), CancellationToken.None)
            .GetAwaiter().GetResult() == "READY", "Handshake line decoding changed.");
        Reject(() => AppUpdateInstaller.ReadHandshakeAsync(new StringReader(new string('x', 65) + "\n"), CancellationToken.None)
            .GetAwaiter().GetResult());
        using (var blocked = new BlockingReader())
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            try
            {
                AppUpdateInstaller.ReadHandshakeAsync(blocked, timeout.Token).GetAwaiter().GetResult();
                throw new Exception("An uncooperative handshake read escaped its deadline.");
            }
            catch (OperationCanceledException) { }
        }

        Fixture((candidate, target, backup, failed) =>
        {
            var events = new List<string>();
            AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, false,
                () => events.Add("acl"), () => { Require(File.ReadAllText(target) == "new", "New copy changed."); events.Add("verify"); },
                () => false, () => events.Add("launch"));
            Require(events.SequenceEqual(new[] { "acl", "verify", "launch" }) && !File.Exists(candidate) && !File.Exists(backup),
                "New-copy commit order/recovery state changed.");
        });
        Fixture((candidate, target, backup, failed) =>
        {
            File.WriteAllText(target, "old");
            AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, true, () => { }, () => { },
                () => File.ReadAllText(backup) == "old", () => { });
            Require(File.ReadAllText(target) == "new" && File.ReadAllText(backup) == "old" && !File.Exists(failed),
                "Existing-copy replacement did not preserve the exact old bytes.");
        });
        foreach (var failAtLaunch in new[] { false, true })
            Fixture((candidate, target, backup, failed) =>
            {
                File.WriteAllText(target, "old");
                Reject(() => AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, true,
                    () => { }, () => { if (!failAtLaunch) throw new IOException("Verification fixture"); },
                    () => File.ReadAllText(backup) == "old", () => throw new IOException("Launch fixture")));
                Require(File.ReadAllText(target) == "old" && File.ReadAllText(failed) == "new" && !File.Exists(backup),
                    "Failed verification/relaunch did not atomically restore the old target.");
            });
        Fixture((candidate, target, backup, failed) =>
        {
            Reject(() => AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, false,
                () => { }, () => throw new IOException("Verification fixture"), () => false, () => { }));
            Require(!File.Exists(target) && File.ReadAllText(failed) == "new", "Failed first install remained active.");
        });
        Fixture((candidate, target, backup, failed) =>
        {
            File.WriteAllText(target, "old");
            Reject(() => AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, true,
                () => { }, () => throw new IOException("Verification fixture"), () => false, () => { }));
            Require(File.ReadAllText(target) == "new" && File.ReadAllText(backup) == "old" && !File.Exists(failed),
                "Unverified recovery bytes were moved into the target.");
        });
        Fixture((candidate, target, backup, failed) =>
        {
            File.WriteAllText(target, "old");
            File.WriteAllText(backup, "existing recovery");
            Reject(() => AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, true,
                () => { }, () => { }, () => true, () => { }));
            Require(File.ReadAllText(target) == "old" && File.ReadAllText(candidate) == "new" &&
                File.ReadAllText(backup) == "existing recovery", "Existing recovery state was overwritten.");
        });
        Fixture((candidate, target, backup, failed) =>
        {
            File.WriteAllText(target, "old");
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
                Reject(() => AppUpdateInstaller.CommitReplacement(candidate, target, backup, failed, true,
                    () => { }, () => { }, () => true, () => { }));
            Require(File.ReadAllText(target) == "old" && File.ReadAllText(candidate) == "new" && !File.Exists(backup),
                "Locked target did not fail without changing either copy.");
        });
        Console.WriteLine("PASS: Helper request identity/expiry/local-path validation and real atomic file move/replace/restore, launch/verification/lock/recovery-failure fixtures. Opaque temporary bytes only; no executable/helper/Program Files/network/Setup/reboot.");
    }

    private static void Fixture(Action<string, string, string, string> check)
    {
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var name = "PcNinja.AppUpdateAtomic.Tests." + Guid.NewGuid().ToString("N");
        var folder = Path.GetFullPath(Path.Combine(parent, name));
        if (Path.GetDirectoryName(folder) != parent || Path.GetFileName(folder) != name)
            throw new Exception("Unsafe temporary fixture path.");
        Directory.CreateDirectory(folder);
        try
        {
            var candidate = Path.Combine(folder, "candidate.bin");
            File.WriteAllText(candidate, "new");
            check(candidate, Path.Combine(folder, "target.bin"), Path.Combine(folder, "backup.bin"), Path.Combine(folder, "failed.bin"));
        }
        finally
        {
            // Exact validated random fixture child only, never a product/workspace tree.
            if (Path.GetDirectoryName(Path.GetFullPath(folder)) != parent || Path.GetFileName(folder) != name)
                throw new Exception("Unsafe fixture cleanup path.");
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return; }
        throw new Exception("Unsafe helper request/replacement was accepted.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class BlockingReader : TextReader
    {
        private readonly TaskCompletionSource<int> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken token = default) => new(_pending.Task);
        protected override void Dispose(bool disposing) { _pending.TrySetResult(0); base.Dispose(disposing); }
    }
}
