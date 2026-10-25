using System.Security.Principal;
using PcNinja.Win11Upgrade.Core;
using PcNinja.Win11UpgradeAssistant;

internal static class PortablePublishChecks
{
    internal static void Run(string builtHost, AppExecutableInfo info)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        { Console.WriteLine("SKIP: Elevated same-folder publish/reuse/launch-lock fixture; no actual helper or executable is run."); return; }
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N")));
        if (Path.GetDirectoryName(parent) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
            throw new Exception("Unsafe temporary publisher fixture.");
        Directory.CreateDirectory(parent);
        try
        {
            var asset = AppUpdateIdentity.AssetNameFor(info.Version);
            var tag = "v" + info.Version;
            var release = new AppUpdateManifest(1, AppUpdateIdentity.Product, AppUpdateIdentity.Repository,
                AppUpdateIdentity.ChannelFor(info.Version), info.Version, tag, asset,
                $"https://github.com/{AppUpdateIdentity.Repository}/releases/download/{tag}/{asset}", info.SizeBytes,
                info.Sha256, $"https://github.com/{AppUpdateIdentity.Repository}/releases/tag/{tag}");
            var original = Path.Combine(parent, "original-retained.exe");
            File.Copy(builtHost, original);
            string target;
            using (var workspace = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                File.Copy(builtHost, workspace.CandidatePath);
                target = workspace.TargetPath(release);
                var callbackCount = 0;
                var request = new AppUpdateRequest(2, Path.GetFileName(workspace.DirectoryPath), DateTimeOffset.UtcNow,
                    Environment.ProcessId + 10000, DateTime.UtcNow.AddMinutes(-1).Ticks,
                    original, info.Sha256, info.SignerThumbprint, release, true);
                AppUpdateInstaller.ApplyVerified(workspace, request, launchedPath =>
                {
                    callbackCount++;
                    Require(launchedPath == target, "Helper apply chose a different launch path.");
                    Require(File.Exists(original) && File.Exists(target) && Path.GetDirectoryName(target) == parent, "New version moved away from its source directory.");
                    Reject(() => File.WriteAllText(target, "must not change verified bytes"));
                    Reject(() => File.Move(target, target + ".renamed"));
                    Reject(() => Directory.Move(parent, parent + ".renamed"));
                });
                Require(callbackCount == 1, "App-only helper apply did not reach its injected launch exactly once.");
                if (AppUpdateInstaller.ArchivePrevious(workspace, request, false).Status != "not-requested" || !File.Exists(original))
                    throw new Exception("Missing GUI readiness renamed the predecessor.");
                if (AppUpdateInstaller.ArchivePrevious(workspace, request with { RelaunchGui = false }, true).Status != "not-requested")
                    throw new Exception("CLI without GUI relaunch archived an unconfirmed predecessor.");
            }
            using (var reused = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                reused.CopyExistingCandidate(release, info.SignerThumbprint);
                using (reused.PublishAndHold(release, info.SignerThumbprint)) { }
                reused.CleanupUncommitted();
                Require(File.Exists(original) && File.Exists(target) && Directory.GetFiles(parent, "*.exe").Length == 2,
                    "Reuse removed the original or created a collision/numbered executable.");
            }
            // Change this fixture's expected release digest, not a customer's file.
            using (var refused = AppUpdateWorkspace.CreateFixture(parent, Guid.NewGuid().ToString("N"), info.SizeBytes, info.SizeBytes))
            {
                Reject(() => refused.CopyExistingCandidate(release with { Sha256 = new string('0', 64) }, info.SignerThumbprint));
                Require(AppUpdateWorkspace.InspectExecutable(target).Sha256 == info.Sha256, "Mismatch handling overwrote the existing version.");
                var request = new AppUpdateRequest(2, Path.GetFileName(refused.DirectoryPath), DateTimeOffset.UtcNow,
                    Environment.ProcessId + 10000, DateTime.UtcNow.AddMinutes(-1).Ticks, original, info.Sha256, null, release, true);
                using (var held = AppUpdateWorkspace.HoldOriginal(original, info.Sha256))
                    Require(AppUpdateInstaller.ArchivePrevious(refused, request, true).Status == "retained", "Locked predecessor housekeeping failed the update.");
                var archived = AppUpdateInstaller.ArchivePrevious(refused, request, true);
                Require(archived.Status == "renamed" && archived.Path == Path.Combine(parent, "OLD-" + Path.GetFileName(original)), "Confirmed GUI did not archive the exact predecessor.");
            }
            Console.WriteLine("PASS: Real elevated same-folder versioned publish, existing-file reuse, no overwrite/rename/Copy N, original preservation and no-write/no-delete launch guard. Temporary test-host bytes only; no helper/executable/Setup launched.");
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
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return; }
        throw new Exception("Unsafe portable publish operation was accepted.");
    }
}
