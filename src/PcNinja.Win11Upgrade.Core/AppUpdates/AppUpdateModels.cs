using System.Reflection;

namespace PcNinja.Win11Upgrade.Core;

public static class AppUpdateIdentity
{
    public const string Repository = "JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja";
    public const string Product = "Windows Upgrade Assistant By PcNinja";
    public const string AssetName = "Windows-Upgrade-Assistant-By-PcNinja.exe";
    public const string ManifestName = "update-manifest.json";
    public const long MaximumBytes = 100_000_000;
    public static string BuildVersion => typeof(AppUpdateIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Application build identity is unavailable.");
    public static string Version => BuildVersion.Split('+')[0];
}

public sealed record AppUpdateManifest(int SchemaVersion, string Product, string Repository, string Channel,
    string Version, string Tag, string AssetName, string AssetUrl, long SizeBytes, string Sha256, string ReleaseNotesUrl);

public enum AppUpdateCheckState { Available, UpToDate, NoStableRelease, Unavailable }

public sealed record AppUpdateCheckResult(AppUpdateCheckState State, string Message, AppUpdateManifest? Release = null);

public sealed record AppUpdateProgress(long BytesReceived, long TotalBytes);
