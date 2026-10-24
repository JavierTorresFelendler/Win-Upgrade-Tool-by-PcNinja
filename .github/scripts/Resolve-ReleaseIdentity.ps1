param([string]$PropsPath = 'Directory.Build.props')
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AppUpdateRelease.psm1') -Force
$identity = Get-PcNinjaReleaseIdentity -PropsPath $PropsPath
if (-not (Test-Path -LiteralPath $identity.NotesPath -PathType Leaf)) { throw 'Version-specific release notes are missing.' }
if ($env:IS_TAG -eq 'true') {
    if ($env:REF_NAME -cne $identity.Tag) { throw 'Release tag and authoritative product version differ.' }
    git merge-base --is-ancestor $env:GITHUB_SHA origin/main
    if ($LASTEXITCODE -ne 0) { throw 'Only reviewed commits already merged into main may be released.' }
}
if ($env:GITHUB_ENV) {
    @("PRODUCT_VERSION=$($identity.Version)", "RELEASE_CHANNEL=$($identity.Channel)", "RELEASE_LABEL=$($identity.Label)",
        "ASSET_NAME=$($identity.AssetName)", "RELEASE_NOTES=$($identity.NotesPath)") | Add-Content -LiteralPath $env:GITHUB_ENV -Encoding utf8
}
$identity
