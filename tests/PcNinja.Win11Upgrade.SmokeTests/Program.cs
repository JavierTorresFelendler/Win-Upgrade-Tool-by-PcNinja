using PcNinja.Win11Upgrade.Core;

var failures = new List<string>();
var engine = new UpgradeEngine();
var os = engine.GetOperatingSystemDescription();
if (string.IsNullOrWhiteSpace(os) || !os.Contains("build", StringComparison.OrdinalIgnoreCase))
    failures.Add("Operating system description was not populated.");

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
Console.WriteLine($"PASS: Microsoft signer: {signer}");
Console.WriteLine("PASS: Unsigned file rejected.");
return 0;
