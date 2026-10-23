namespace PcNinja.Win11Upgrade.Core;

public static class PcNinjaDownloadPolicy
{
    // EditionIDs covered by the configured multi-edition client ISO. Routing is
    // not validation: the actual downloaded WIM/ESD still decides compatibility.
    private static readonly HashSet<string> OemEditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Core", "CoreN", "CoreSingleLanguage", "Education", "EducationN",
        "Professional", "ProfessionalN", "ProfessionalEducation", "ProfessionalEducationN",
        "ProfessionalWorkstation", "ProfessionalWorkstationN"
    };

    public static string GetBrowserPage(CurrentWindows current, bool isX64)
    {
        var clientBuild = current.Build is >= 10240 and <= 19045 or >= 22000;
        return isX64 && clientBuild && OemEditions.Contains(current.Edition) &&
            AutomaticDownloadPolicy.IsAvailable(current.Language)
            ? UpgradeEngine.DownloadEndpoint : UpgradeEngine.PcNinjaIsoCatalogPage;
    }
}
