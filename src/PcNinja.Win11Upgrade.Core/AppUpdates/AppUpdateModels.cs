using System.Reflection;

namespace PcNinja.Win11Upgrade.Core;

public static class AppUpdateIdentity
{
    public const string Repository = "JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja";
    public const string Product = "Windows Upgrade Tool by PcNinja";
    public const string AssetName = "Win-Upgrade-Tool-by-PcNinja.exe";
    public const string BuildExecutableName = "Windows-Upgrade-Assistant-By-PcNinja.exe";
    public const string ManifestName = "update-manifest.json";
    public const long MaximumBytes = 100_000_000;
    public static string BuildVersion => typeof(AppUpdateIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Application build identity is unavailable.");
    public static string Version => BuildVersion.Split('+')[0];
    public static string Channel => ChannelFor(Version);
    public static string ChannelFor(string version) => ReleaseVersion.Parse(version).IsBeta ? "beta" : "stable";
    public static string AssetNameFor(string version)
    {
        var parsed = ReleaseVersion.Parse(version);
        if (parsed.IsStable || parsed.IsBeta) return AssetName;

        var display = $"{parsed.Major}.{parsed.Minor}" + (parsed.Patch == 0 ? "" : $".{parsed.Patch}");
        var parts = parsed.Prerelease.Split('.');
        if (parts.Length != 2 || parts[0] != "rc" ||
            !int.TryParse(parts[1], out var number) || number <= 0 || number > 65534)
            throw new FormatException("Only stable, numbered Beta or numbered RC asset identities are supported.");
        display += $"-RC{number}";
        return $"{Path.GetFileNameWithoutExtension(BuildExecutableName)}-V{display}.exe";
    }
}

public sealed record AppUpdateManifest(int SchemaVersion, string Product, string Repository, string Channel,
    string Version, string Tag, string AssetName, string AssetUrl, long SizeBytes, string Sha256, string ReleaseNotesUrl,
    string Importance = "normal", string Message = "A new version of Windows Upgrade Tool by PcNinja is available.");

public sealed record AppUpdateRequest(int SchemaVersion, string OperationId, DateTimeOffset CreatedUtc,
    int ParentProcessId, long ParentStartUtcTicks, string ParentPath, string ParentHash,
    string? RequiredSigner, AppUpdateManifest Release, bool RelaunchGui, string? ParentFileVersion = null);

public sealed record AppUpdateTransactionState(int SchemaVersion, string OperationId, string Status,
    string ParentHash, string ReleaseHash, string ParentFileVersion, string OriginalName, string? ArchivePath);

public enum AppUpdateCheckState { Available, UpToDate, NoStableRelease, Unavailable, NoBetaRelease }

public sealed record AppUpdateCheckResult(AppUpdateCheckState State, string Message, AppUpdateManifest? Release = null);

public sealed record AppUpdateProgress(long BytesReceived, long TotalBytes);

/// <summary>Outcome of removing finished update staging beside the application.</summary>
public sealed record AppUpdateStagingCleanup(int OperationsRemoved, IReadOnlyList<string> Retained, bool StagingRemoved, bool StagingPresent);

public sealed record AppUpdateArchive(string Status, string Path);
