param(
    [Parameter(Mandatory)][string]$ExePath,
    [Parameter(Mandatory)][string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $ExePath).Path
function Invoke-Assistant([string[]]$Arguments) {
    $text = & $exe @Arguments | Out-String
    $script:LastCode = $LASTEXITCODE
    return $text
}
$version = Invoke-Assistant @('--version')
if ($LastCode -ne 0 -or $version.Trim() -ne $ExpectedVersion) { throw 'Version mismatch.' }
$help = Invoke-Assistant @('help', 'all')
if ($LastCode -ne 0) { throw 'Offline help failed.' }
if (-not $help.Contains('manualRestartRequired') -or -not $help.Contains('restart manually to continue')) {
    throw 'Offline help omitted successful manual-restart guidance.'
}
if (-not $help.Contains('Windows 10 -> Windows 10') -or -not $help.Contains('does NOT compare build order') -or
    -not $help.Contains('Windows 10 target arguments:') -or -not $help.Contains('buildOrderChecked')) {
    throw 'Offline help omitted the Windows 10 POC profile/policy.'
}
foreach ($option in @('--mounted', '--iso', '--download', '--image-index', '--sha256', '--no-restart', '--dry-run', '--yes', '--json')) {
    if (-not $help.Contains($option)) { throw "Offline help missing $option" }
}
$status = (Invoke-Assistant @('status', '--json')) | ConvertFrom-Json
if ($LastCode -ne 0 -or -not $status.operatingSystem -or -not $status.version) { throw 'Status JSON failed.' }
if (-not $status.systemUiLanguage -or $null -eq $status.automaticDownloadAvailable) { throw 'Language availability missing from status.' }
$automaticExpected = $status.systemUiLanguage -ieq 'en-US'
if ($status.automaticDownloadAvailable -ne $automaticExpected) { throw 'Incorrect automatic download language policy in status.' }
if (-not $automaticExpected -and (-not $status.automaticDownloadUnavailableReason -or
    -not $status.automaticDownloadUnavailableReason.Contains('https://iso.pcninja.pro'))) {
    throw 'Unavailable automatic download omitted its reason/manual alternative.'
}
if (-not $help.Contains('en-US') -or -not $help.Contains('https://iso.pcninja.pro')) { throw 'Offline help omitted the language policy/manual catalog.' }
$scan = (Invoke-Assistant @('scan', '--json')) | ConvertFrom-Json
if ($LastCode -ne 0 -or $null -eq $scan.localIsos -or $null -eq $scan.mountedSources) { throw 'Scan JSON failed.' }
foreach ($case in @(
    @('status', '--no-restart', '--json'),
    @('status', '--image-index', '1', '--json'),
    @('upgrade', '--iso', 'first.iso', '--iso', 'second.iso', '--json'),
    @('upgrade', '--iso', 'not-used.iso', '--image-index', '0', '--json'),
    @('validate', '--mounted', 'Z:\not-used', '--image-index', '-1', '--json'),
    @('validate', '--mounted', 'Z:\not-used', '--image-index', 'abc', '--json'),
    @('upgrade', '--mounted', 'Z:\not-used', '--destination', 'Z:\not-used', '--json')
)) {
    $errorResult = (Invoke-Assistant $case) | ConvertFrom-Json
    if ($LastCode -ne 2 -or $errorResult.exitCode -ne 2) { throw "Invalid arguments accepted: $case" }
}
$completion = Invoke-Assistant @('completion', 'powershell')
if ($LastCode -ne 0 -or -not $completion.Contains('--image-index')) { throw 'Completion options missing.' }
[scriptblock]::Create($completion) | Out-Null
Invoke-Expression $completion
$inputLine = "& '$($exe.Replace("'", "''"))' upgrade --image-"
$matches = [System.Management.Automation.CommandCompletion]::CompleteInput($inputLine, $inputLine.Length, $null)
if ('--image-index' -notin $matches.CompletionMatches.CompletionText) { throw 'Native Tab completion failed.' }
$bytes = [IO.File]::ReadAllBytes($exe)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
if ([BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68) -ne 2) { throw 'The EXE has a Console subsystem.' }
if ($bytes.Length -gt 100000000) { throw 'Portable EXE exceeds 100,000,000 bytes.' }
Write-Output "PASS: $ExpectedVersion CLI version/help/JSON/invalid arguments/completion/GUI subsystem/size. No Windows Setup invoked."
