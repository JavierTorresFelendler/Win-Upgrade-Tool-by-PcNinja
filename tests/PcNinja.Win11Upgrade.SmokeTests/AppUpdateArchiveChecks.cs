using System.Security.Cryptography;
using PcNinja.Win11Upgrade.Core;

internal static class AppUpdateArchiveChecks
{
    internal static void Run()
    {
        var parent = Path.Combine(Path.GetTempPath(), "PcNinja-AppUpdate-Fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        const string fileVersion = "1.2.0.0";
        const string content = "harmless archive fixture; never executable";
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
        try
        {
            var source = Path.Combine(parent, "previous.exe");
            var normal = Path.Combine(parent, AppUpdateWorkspace.ArchiveName(fileVersion));
            var qualified = Path.Combine(parent, AppUpdateWorkspace.ArchiveName(fileVersion, hash));
            File.WriteAllText(source, content);
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, new string('0', 64), fileVersion));
            Require(File.Exists(source), "Hash mismatch altered the predecessor.");

            File.WriteAllText(normal, "unrelated same-version archive must survive");
            File.WriteAllText(qualified, "unrelated hash-qualified archive must survive");
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, hash, fileVersion));
            Require(File.Exists(source) && File.Exists(normal) && File.Exists(qualified),
                "Both occupied deterministic archive names did not retain all files.");
            File.Delete(qualified);

            var archived = AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, hash, fileVersion);
            Require(archived.Status == "archived" && archived.Path == qualified && !File.Exists(source) &&
                File.ReadAllText(qualified) == content && File.ReadAllText(normal) == "unrelated same-version archive must survive",
                "Hash-qualified collision archive did not preserve the predecessor.");

            var sameBytes = Path.Combine(parent, "same-bytes.exe");
            File.WriteAllText(sameBytes, content);
            var reused = AppUpdateWorkspace.ArchiveFixtureOriginal(parent, sameBytes, hash, fileVersion);
            Require(reused.Status == "reused" && reused.Path == qualified && !File.Exists(sameBytes),
                "An identical existing archive was not reused safely.");

            var legacy = Path.Combine(parent, "OLD-previous.exe");
            var unrelatedLegacy = Path.Combine(parent, "OLD.V1-previous.exe");
            File.WriteAllText(legacy, content);
            File.WriteAllText(unrelatedLegacy, "unrelated legacy archive must survive");
            File.WriteAllText(source, content);
            var duplicateResult = AppUpdateWorkspace.ArchiveFixtureOriginal(parent, source, hash, fileVersion);
            Require(duplicateResult.Status == "reused" && !File.Exists(legacy) && File.Exists(unrelatedLegacy),
                "Verified legacy duplicate migration removed an unrelated archive or kept an identical duplicate.");

            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent, qualified, hash, fileVersion));
            Reject(() => AppUpdateWorkspace.ArchiveFixtureOriginal(parent,
                Path.Combine(parent, "..", "unrelated.exe"), hash, fileVersion));
            Reject(() => AppUpdateWorkspace.ArchiveName("1.2.0"));
            Reject(() => AppUpdateWorkspace.ArchiveName("1.2.0.0", "not-a-hash"));
            Require(AppUpdateWorkspace.TryParseWindowsFileVersion(fileVersion, out var parsed) && parsed == fileVersion &&
                !AppUpdateWorkspace.TryParseWindowsFileVersion("1.2.0", out _),
                "Windows FileVersion parsing did not require exactly four components.");
            Console.WriteLine("PASS: Verified four-part version archives, deterministic SHA-256 collision names, identical reuse, exact legacy duplicate retirement, and unrelated/mismatched recovery preservation. Harmless fixture bytes only; no app/Setup/reboot.");
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(parent)) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) ||
                !Path.GetFileName(parent).StartsWith("PcNinja-AppUpdate-Fixture-", StringComparison.Ordinal))
                throw new Exception("Unsafe archive fixture cleanup target.");
            Directory.Delete(parent, true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return; }
        throw new Exception("Unsafe predecessor archive operation was accepted.");
    }
}
