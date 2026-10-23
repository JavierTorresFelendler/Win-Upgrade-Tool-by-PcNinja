using PcNinja.Win11Upgrade.Core;

internal static class DownloadRoutingChecks
{
    internal static void Run()
    {
        var editions = new[]
        {
            "Core", "CoreN", "CoreSingleLanguage", "Education", "EducationN",
            "Professional", "ProfessionalN", "ProfessionalEducation", "ProfessionalEducationN",
            "ProfessionalWorkstation", "ProfessionalWorkstationN"
        };
        foreach (var edition in editions)
        foreach (var build in new[] { 10240, 19043, 19045, 22000, 26100, 26300 })
        {
            var current = new CurrentWindows(edition, "en-US", build);
            if (PcNinjaDownloadPolicy.GetBrowserPage(current, true) != "https://11oem.pcninja.pro/" ||
                PcNinjaDownloadPolicy.GetBrowserPage(current with { Edition = edition.ToLowerInvariant(), Language = "EN-us" }, true) !=
                    UpgradeEngine.DownloadEndpoint)
                throw new Exception($"OEM browser routing drifted from the automatic endpoint: {edition}, build {build}.");
            foreach (var language in new[] { "he-IL", "en-GB", "ru-RU", "", "unknown" })
                ExpectCatalog(current with { Language = language }, true);
            ExpectCatalog(current, false);
        }
        foreach (var edition in new[] { "Enterprise", "EnterpriseN", "EnterpriseS", "EnterpriseSN", "ServerStandard", "", "Unknown", "ProfessionalFoo" })
            ExpectCatalog(new(edition, "en-US", 26300), true);
        foreach (var build in new[] { 0, 9600, 20348 })
            ExpectCatalog(new("Professional", "en-US", build), true);
        if (UpgradeEngine.GetPcNinjaDownloadPage() != PcNinjaDownloadPolicy.GetBrowserPage(CurrentWindows.Read(),
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64))
            throw new Exception("The actual browser launcher route does not use the shared system policy.");
        Console.WriteLine("PASS: OEM direct/browser parity for all 11 edition IDs on Windows 10/11; exact edition, x64 and language routing; catalog for other systems. No browser or ISO download.");
    }

    private static void ExpectCatalog(CurrentWindows current, bool isX64)
    {
        if (PcNinjaDownloadPolicy.GetBrowserPage(current, isX64) != UpgradeEngine.PcNinjaIsoCatalogPage)
            throw new Exception("A system outside the configured OEM media was routed to its direct ISO.");
    }
}
