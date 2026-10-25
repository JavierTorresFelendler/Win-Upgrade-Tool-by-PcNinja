using System.Security.Cryptography;
using PcNinja.Win11Upgrade.Core;

internal static class AppUpdateArchiveChecks
{
    internal static void Run()
    {
        var parent = Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var source = Path.Combine(parent, "previous.exe");
        var destination = Path.Combine(parent, "OLD-previous.exe");
        const string content = "harmless rename fixture; never executable";
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
        try
        {
            File.WriteAllText(source, content);
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, new string('0', 64)));
            if (!File.Exists(source)) throw new Exception("Mismatch altered the predecessor.");
            File.WriteAllText(destination, "existing OLD must survive");
            var occupied = Path.Combine(parent, "OLD.V1-previous.exe");
            File.WriteAllText(occupied, "existing V1 must survive");
            using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, hash));
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, Path.Combine(parent, "..", "unrelated.exe"), hash));
            var result = AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, hash);
            if (result != Path.Combine(parent, "OLD.V2-previous.exe") || File.Exists(source) || File.ReadAllText(result) != content ||
                File.ReadAllText(destination) != "existing OLD must survive" || File.ReadAllText(occupied) != "existing V1 must survive")
                throw new Exception("Exact handle-bound OLD rename did not preserve the predecessor bytes.");
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, destination, hash));
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, result, hash));
            var fresh = Path.Combine(parent, "fresh.exe");
            File.WriteAllText(fresh, content);
            if (AppUpdateWorkspace.ArchiveFixtureOriginal(parent, fresh, hash) != Path.Combine(parent, "OLD-fresh.exe"))
                throw new Exception("An unoccupied archive name unexpectedly gained a number.");
            Console.WriteLine("PASS: Exact handle-bound hash-verified OLD/OLD.V1/V2 archive naming; existing archives preserved, mismatch/locked/outside/already-OLD refused. Disposable non-executable bytes only; no app/Setup/reboot.");
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(parent)) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) ||
                !Path.GetFileName(parent).StartsWith("PcNinja-AppUpdate-Fixture-", StringComparison.Ordinal))
                throw new Exception("Unsafe archive fixture cleanup target.");
            Directory.Delete(parent, true);
        }
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return; }
        throw new Exception("Unsafe predecessor rename succeeded.");
    }
}
