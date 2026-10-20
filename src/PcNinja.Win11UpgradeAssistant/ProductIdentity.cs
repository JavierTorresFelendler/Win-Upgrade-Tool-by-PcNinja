using System.Reflection;

namespace PcNinja.Win11UpgradeAssistant;

internal static class ProductIdentity
{
    internal const string Name = "Windows Upgrade Assistant By PcNinja";
    internal const string ExecutableName = "Windows-Upgrade-Assistant-By-PcNinja.exe";
    internal static string BuildVersion => typeof(ProductIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    internal static string Version => BuildVersion.Split('+')[0];
}
