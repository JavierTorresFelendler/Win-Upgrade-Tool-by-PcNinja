using System.Security.Principal;
using PcNinja.Win11Upgrade.Core;
using PcNinja.Win11UpgradeAssistant;

internal static class PortablePublishChecks
{
    internal static void Run(string builtHost, AppExecutableInfo info)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        { Console.WriteLine("SKIP: Elevated fixed-name publish/rollback fixture; no actual helper or executable is run."); return; }
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N")));
        if (Path.GetDirectoryName(parent) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
            throw new Exception("Unsafe temporary publisher fixture.");
        Directory.CreateDirectory(parent);
        try
        {
            var release = MakeRelease(info);
            var legacyName = "Win-Upgrade-Tool-by-PcNinja-v.1.2.exe";
            var original = Path.Combine(parent, legacyName);
            File.Copy(builtHost, original);
            var target = Path.Combine(parent, AppUpdateIdentity.AssetName);
            var id = Guid.NewGuid().ToString("N");
            AppUpdateArchive archived;
            using (var workspace = AppUpdateWorkspace.CreateFixture(parent, id, info.SizeBytes, info.SizeBytes))
            {
                File.Copy(builtHost, workspace.CandidatePath);
                var request = MakeRequest(workspace, original, legacyName, info, release);
                var callbackCount = 0;
                var applied = AppUpdateInstaller.ApplyVerified(workspace, request, launchedPath =>
                {
                    callbackCount++;
                    Require(launchedPath == target && File.Exists(target) && File.Exists(original),
                        "Historical versioned-to-fixed bootstrap selected the wrong file.");
                    Reject(() => File.WriteAllText(target, "must not change verified bytes"));
                    Reject(() => File.Move(target, target + ".renamed"));
                    Reject(() => Directory.Move(parent, parent + ".renamed"));
                    return new(true, false);
                });
                Require(callbackCount == 1 && applied.Ready, "The injected readiness result was not honored.");
                var archiveResult = AppUpdateInstaller.ArchivePrevious(workspace, request, true);
                var expectedArchive = Path.Combine(parent, AppUpdateWorkspace.ArchiveName(info.FileVersion));
                Require(archiveResult.Status == "archived" && archiveResult.Path == expectedArchive &&
                    !File.Exists(original) && AppUpdateWorkspace.InspectExecutable(expectedArchive).Sha256 == info.Sha256,
                    $"The historical predecessor was not atomically archived under its verified FileVersion. " +
                    $"Status={archiveResult.Status}; path={archiveResult.Path ?? "<none>"}; " +
                    $"reason={archiveResult.Reason ?? "<none>"}; sourceExists={File.Exists(original)}.");
                archived = new(archiveResult.Status, archiveResult.Path!);
            }

            for (var update = 0; update < 2; update++)
            {
                using var workspace = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes);
                workspace.CopyExistingCandidate(release, info.SignerThumbprint);
                var request = MakeRequest(workspace, target, AppUpdateIdentity.AssetName, info, release);
                var applied = AppUpdateInstaller.ApplyVerified(workspace, request, launchedPath =>
                {
                    Require(launchedPath == target, "Same-name update launched a noncanonical path.");
                    Reject(() => File.WriteAllText(target, "must not change successor bytes"));
                    Reject(() => File.Move(target, target + ".renamed"));
                    return new(true, false);
                });
                Require(applied.Ready && AppUpdateWorkspace.InspectExecutable(target).Sha256 == info.Sha256,
                    "Repeated fixed-name replacement failed exact successor verification.");
                var result = AppUpdateInstaller.ArchivePrevious(workspace, request, true);
                Require(result.Status == "reused" && result.Path == archived.Path &&
                    !File.Exists(workspace.PreviousPath), "Identical predecessor archive was not reused safely.");
            }

            using (var rollback = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                rollback.CopyExistingCandidate(release, info.SignerThumbprint);
                var request = MakeRequest(rollback, target, AppUpdateIdentity.AssetName, info, release);
                var failed = AppUpdateInstaller.ApplyVerified(rollback, request, _ => new(false, true));
                Require(!failed.Ready && failed.SafeToRollback &&
                    AppUpdateWorkspace.InspectExecutable(target).Sha256 == info.Sha256 &&
                    File.Exists(rollback.FailedPath) && File.Exists(rollback.DisplacedPath),
                    "A definitely exited failed successor did not restore and retain verifiable rollback bytes.");
            }

            using (var uncertain = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                uncertain.CopyExistingCandidate(release, info.SignerThumbprint);
                var request = MakeRequest(uncertain, target, AppUpdateIdentity.AssetName, info, release);
                var outcome = AppUpdateInstaller.ApplyVerified(uncertain, request, _ => new(false, false));
                Require(!outcome.Ready && !outcome.SafeToRollback &&
                    File.Exists(uncertain.PreviousPath) && File.Exists(uncertain.DisplacedPath) &&
                    AppUpdateWorkspace.InspectExecutable(target).Sha256 == info.Sha256,
                    "Uncertain GUI readiness discarded or restored potentially live-process bytes.");
            }

            using (var refused = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                Reject(() => refused.CopyExistingCandidate(release with { Sha256 = new string('0', 64) }, info.SignerThumbprint));
                Require(AppUpdateWorkspace.InspectExecutable(target).Sha256 == info.Sha256,
                    "Mismatch handling overwrote the current fixed-name EXE.");
            }
            CheckTransactionCleanup(builtHost, info);
            Require(Directory.GetFiles(parent, "*.exe").Length == 2,
                "The portable folder did not settle to one canonical EXE plus its predecessor archive.");
            Console.WriteLine("PASS: Elevated historical-name bootstrap, two repeated fixed-name atomic replacements, hash-verified version archive/reuse, definite rollback and uncertain-readiness retention. Test-host bytes only; no helper/executable/Setup launched.");
        }
        finally
        {
            var resolved = Path.GetFullPath(parent);
            if (Path.GetDirectoryName(resolved) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) ||
                !Path.GetFileName(resolved).StartsWith("PcNinja-AppUpdate-Fixture-", StringComparison.Ordinal))
                throw new Exception("Unsafe publisher cleanup path.");
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static AppUpdateManifest MakeRelease(AppExecutableInfo info)
    {
        var asset = AppUpdateIdentity.AssetNameFor(info.Version);
        var tag = "v" + info.Version;
        return new(1, AppUpdateIdentity.Product, AppUpdateIdentity.Repository,
            AppUpdateIdentity.ChannelFor(info.Version), info.Version, tag, asset,
            $"https://github.com/{AppUpdateIdentity.Repository}/releases/download/{tag}/{asset}", info.SizeBytes,
            info.Sha256, $"https://github.com/{AppUpdateIdentity.Repository}/releases/tag/{tag}");
    }

    private static AppUpdateRequest MakeRequest(AppUpdateWorkspace workspace, string original, string originalName,
        AppExecutableInfo info, AppUpdateManifest release) =>
        new(2, Path.GetFileName(workspace.DirectoryPath), DateTimeOffset.UtcNow,
            Environment.ProcessId + 10000, DateTime.UtcNow.AddMinutes(-1).Ticks,
            original, info.Sha256, info.SignerThumbprint, release, true, info.FileVersion);

    private static void CheckTransactionCleanup(string builtHost, AppExecutableInfo info)
    {
        var id = Guid.NewGuid().ToString("N");
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(parent);
        try
        {
            var target = Path.Combine(parent, AppUpdateIdentity.AssetName);
            File.Copy(builtHost, target);
            using (var workspace = AppUpdateWorkspace.CreateFixture(parent, id, info.SizeBytes, info.SizeBytes))
            {
                workspace.SnapshotOriginal(target, info.Sha256);
                workspace.WriteTransactionState(new AppUpdateTransactionState(1, id, "awaiting-gui",
                    info.Sha256, info.Sha256, info.FileVersion, AppUpdateIdentity.AssetName, null));
            }
            var pending = AppUpdateWorkspace.RemoveFinishedStagingFixture(parent);
            var previous = Path.Combine(parent, AppUpdateWorkspace.StateDirectoryName, id, "previous.exe");
            Require(!pending.StagingRemoved && File.Exists(previous) &&
                pending.Retained.Any(item => item.StartsWith(id + ":", StringComparison.Ordinal)),
                "Cleanup archived or removed a predecessor before successor GUI readiness.");
            AppUpdateInstaller.ConfirmPendingGuiStartup(target);
            using (var workspace = AppUpdateWorkspace.OpenFixture(parent, id))
                Require(workspace.ReadTransactionState<AppUpdateTransactionState>().Status == "successor-ready",
                    "A fully initialized canonical GUI did not authenticate the pending transaction.");
            var finished = AppUpdateWorkspace.RemoveFinishedStagingFixture(parent);
            var archive = Path.Combine(parent, AppUpdateWorkspace.ArchiveName(info.FileVersion));
            Require(finished.StagingRemoved && File.Exists(Path.Combine(parent, AppUpdateIdentity.AssetName)) &&
                AppUpdateWorkspace.InspectExecutable(archive).Sha256 == info.Sha256 &&
                Directory.GetFiles(parent, "*.exe").Length == 2,
                "Readiness-proven cleanup did not archive the predecessor and remove only updater staging.");
        }
        finally
        {
            if (Path.GetDirectoryName(parent) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) ||
                !Path.GetFileName(parent).StartsWith("PcNinja-AppUpdate-Fixture-", StringComparison.Ordinal))
                throw new Exception("Unsafe transaction fixture cleanup target.");
            Directory.Delete(parent, recursive: true);
        }
    }

    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return; }
        throw new Exception("Unsafe portable publish operation was accepted.");
    }
}
