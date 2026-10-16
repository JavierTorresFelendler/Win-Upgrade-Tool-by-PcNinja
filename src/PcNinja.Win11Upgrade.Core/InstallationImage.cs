using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PcNinja.Win11Upgrade.Core;

public sealed record InstallationImage(int Index, string Name, string Edition, string Architecture,
    string Version, string InstallationType, string Language)
{
    public override string ToString() => $"{Index}: {Name} · {Language} · {Architecture} · {Version}";
}

public sealed record CurrentWindows(string Edition, string Language, int Build)
{
    public static CurrentWindows Read()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var edition = key?.GetValue("EditionID")?.ToString() ?? "";
        var language = CultureInfo.GetCultureInfo(GetSystemDefaultUILanguage()).Name;
        var build = int.TryParse(key?.GetValue("CurrentBuildNumber")?.ToString(), out var number)
            ? number : Environment.OSVersion.Version.Build;
        return new(edition, language, build);
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetSystemDefaultUILanguage();
}

public static class InstallationImages
{
    public static IReadOnlyList<InstallationImage> Compatible(MountedSource source, CurrentWindows current) =>
        (source.Images ?? []).Where(image => GetIncompatibility(image, current) is null).ToArray();

    public static string? GetIncompatibility(InstallationImage image, CurrentWindows current)
    {
        if (!image.InstallationType.Equals("Client", StringComparison.OrdinalIgnoreCase)) return "Not a Windows client image.";
        if (!image.Architecture.Equals("x64", StringComparison.OrdinalIgnoreCase) &&
            !image.Architecture.Equals("amd64", StringComparison.OrdinalIgnoreCase)) return "The image is not x64.";
        if (!image.Edition.Equals(current.Edition, StringComparison.OrdinalIgnoreCase)) return "The edition does not match this Windows installation.";
        if (!image.Language.Equals(current.Language, StringComparison.OrdinalIgnoreCase)) return "The system UI language does not match.";
        if (!System.Version.TryParse(image.Version, out var version) || version.Build < 22000 || version.Build < current.Build)
            return "The image is not an equal-or-newer Windows 11 build.";
        return null;
    }

    public static InstallationImage Select(MountedSource source, int? index, CurrentWindows current)
    {
        var images = source.Images ?? throw new InvalidOperationException("Installation images have not been validated.");
        if (index.HasValue)
        {
            var choice = images.SingleOrDefault(image => image.Index == index.Value)
                ?? throw new InvalidOperationException($"Image index {index} does not exist in the selected source.");
            if (GetIncompatibility(choice, current) is string reason)
                throw new InvalidOperationException($"Image {index} cannot be used for this upgrade: {reason}");
            return choice;
        }
        var candidates = Compatible(source, current);
        if (candidates.Count == 1) return candidates[0];
        throw new InvalidOperationException(candidates.Count == 0
            ? $"No matching upgrade image was found for {current.Edition}, {current.Language}, x64. Choose matching Windows 11 media."
            : "More than one matching image was found. Select an image index before starting Setup.");
    }

    internal static int[] ParseIndexes(string output) => Regex.Matches(output, @"(?m)^\s*Index\s*:\s*(\d+)\s*$")
        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().ToArray();

    internal static InstallationImage ParseDetail(string output, int index)
    {
        var detailStart = Regex.Match(output, @"(?m)^\s*Index\s*:\s*\d+\s*$");
        if (!detailStart.Success) throw new InvalidOperationException("DISM returned no image detail block.");
        output = output[detailStart.Index..];
        string Field(string name) => Regex.Match(output, @"(?m)^\s*" + Regex.Escape(name) + @"\s*:\s*([^\r\n]+)")
            .Groups[1].Value.Trim();
        var languages = Regex.Matches(output, @"(?m)^\s*([a-z]{2,3}(?:-[A-Za-z0-9]{2,8})+)\s*(\(Default\))?\s*$");
        var language = languages.FirstOrDefault(match => match.Groups[2].Success) ?? languages.FirstOrDefault();
        var image = new InstallationImage(index, Field("Name"), Field("Edition"), Field("Architecture"),
            Field("Version"), Field("Installation"), language?.Groups[1].Value ?? "");
        if (string.IsNullOrWhiteSpace(image.Name) || string.IsNullOrWhiteSpace(image.Edition) ||
            string.IsNullOrWhiteSpace(image.Architecture) || string.IsNullOrWhiteSpace(image.Language))
            throw new InvalidOperationException($"Windows image {index} has incomplete metadata; the image cannot be selected safely.");
        return image;
    }
}
