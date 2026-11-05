param(
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$candidate = (Resolve-Path -LiteralPath $CandidatePath).Path
$version = [version]$ExpectedVersion
if ($version.Revision -ge 0) { throw 'A stable three-part candidate version is required.' }
$root = Split-Path -Parent $PSScriptRoot
$fixtures = Join-Path $root '.artifacts/lifecycle-payloads'
$project = Join-Path $root 'src/PcNinja.Win11UpgradeAssistant/PcNinja.Win11UpgradeAssistant.csproj'
$runner = Join-Path $PSScriptRoot 'PcNinja.Win11Upgrade.CliChecks/PcNinja.Win11Upgrade.CliChecks.csproj'
$rc = Join-Path $fixtures 'rc'
$next = Join-Path $fixtures 'next'
$nextVersion = '{0}.{1}.{2}' -f $version.Major, $version.Minor, ($version.Build + 1)
foreach ($fixture in @(@($rc, "$ExpectedVersion-rc.1"), @($next, $nextVersion))) {
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $fixture[0] "-p:PcNinjaReleaseVersion=$($fixture[1])"
    if ($LASTEXITCODE -ne 0) { throw "Lifecycle payload build failed: $($fixture[1])" }
}
# Restore the normal candidate identity in the referenced test assemblies after
# the isolated fixture builds. No fixture version is published to any feed.
& dotnet build $runner -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Lifecycle test runner build failed.' }
& dotnet run --project $runner -c Release --no-build -- --portable-lifecycle `
    (Join-Path $rc 'Windows-Upgrade-Assistant-By-PcNinja.exe') $candidate `
    (Join-Path $next 'Windows-Upgrade-Assistant-By-PcNinja.exe')
if ($LASTEXITCODE -ne 0) {
    # This disposable runner contains fixture diagnostics only, never customer logs.
    $diagnostic = Join-Path $env:ProgramData 'PcNinja/Win11UpgradeAssistant/Upgrade-Assistant.log'
    if (Test-Path -LiteralPath $diagnostic) { Get-Content -LiteralPath $diagnostic -Tail 60 }
    throw 'Real portable helper/GUI lifecycle failed.'
}
