using PcNinja.Win11Upgrade.Core;

var failures = new List<string>();
var engine = new UpgradeEngine();
var os = engine.GetOperatingSystemDescription();
if (string.IsNullOrWhiteSpace(os) || !os.Contains("build", StringComparison.OrdinalIgnoreCase))
    failures.Add("Operating system description was not populated.");

var normalizedWindows11 = UpgradeEngine.FormatOperatingSystemDescription(
    "Windows 10 Pro", "23H2", 22631, true, "Professional");
if (!normalizedWindows11.StartsWith("Windows 11 Pro 23H2", StringComparison.Ordinal))
    failures.Add($"Windows 11 build normalization failed: {normalizedWindows11}");

var normalizedWindows10 = UpgradeEngine.FormatOperatingSystemDescription(
    "Windows 10 Home", "22H2", 19045, true, "Core");
if (!normalizedWindows10.StartsWith("Windows 10 Home 22H2", StringComparison.Ordinal))
    failures.Add($"Windows 10 build normalization failed: {normalizedWindows10}");

var collisionFolder = Path.Combine(Path.GetTempPath(), $"PcNinja-Collision-{Guid.NewGuid():N}");
Directory.CreateDirectory(collisionFolder);
try
{
    File.WriteAllText(Path.Combine(collisionFolder, "Original.iso"), "existing");
    File.WriteAllText(Path.Combine(collisionFolder, "Original (Copy 1).iso.partial"), "partial");
    var collisionPath = UpgradeEngine.GetAvailableDestination(collisionFolder, "Original.iso");
    if (!collisionPath.EndsWith("Original (Copy 2).iso", StringComparison.Ordinal))
        failures.Add($"ISO collision naming failed: {collisionPath}");
}
finally
{
    Directory.Delete(collisionFolder, true);
}

var microsoftFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
    "System32", "curl.exe");
if (!AuthenticodeVerifier.IsTrustedMicrosoftFile(microsoftFile, out var signer))
    failures.Add($"Microsoft signature validation rejected {microsoftFile}. Signer: {signer}");
else if (!signer.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
    failures.Add("The accepted Microsoft file returned an unexpected signer.");

var unsignedFile = Path.GetTempFileName();
try
{
    File.WriteAllText(unsignedFile, "not a signed executable");
    if (AuthenticodeVerifier.IsTrustedMicrosoftFile(unsignedFile, out _))
        failures.Add("Signature validation accepted an unsigned file.");
}
finally
{
    File.Delete(unsignedFile);
}

if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
    return 1;
}

Console.WriteLine($"PASS: OS detection: {os}");
Console.WriteLine($"PASS: Windows 11 normalization: {normalizedWindows11}");
Console.WriteLine($"PASS: Windows 10 normalization: {normalizedWindows10}");
Console.WriteLine("PASS: ISO collision naming.");
Console.WriteLine($"PASS: Microsoft signer: {signer}");
Console.WriteLine("PASS: Unsigned file rejected.");
return 0;
