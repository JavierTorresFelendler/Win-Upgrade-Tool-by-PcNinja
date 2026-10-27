# Read-only GitHub checks. This script never approves a deployment or publishes.
param([Parameter(Mandatory)][ValidateSet('Environment', 'Artifact')][string]$Mode)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'PublicationPolicy.psm1') -Force
$repository = 'JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja'
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_REPOSITORY -cne $repository -or
    $env:GITHUB_REPOSITORY_ID -cne '1413097416' -or
    $env:GITHUB_REF -cnotmatch '\Arefs/tags/v[^\s]+\z' -or
    [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw 'Publication policy checks require this repository, a version tag and its scoped job token.'
}
function Read-PolicyJson([string]$Endpoint) {
    $json = & gh api --hostname github.com --method GET $Endpoint `
        -H 'Accept: application/vnd.github+json' -H 'X-GitHub-Api-Version: 2026-03-10' | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read required publication protection; refusing to publish.' }
    $json | ConvertFrom-Json -Depth 32
}
$environment = Read-PolicyJson "repos/$repository/environments/release"
$policies = Read-PolicyJson "repos/$repository/environments/release/deployment-branch-policies"
Assert-PcNinjaReleaseEnvironment $environment $policies
if ($Mode -eq 'Artifact') {
    if ($env:EXPECTED_ARTIFACT_ID -cnotmatch '\A[1-9][0-9]{0,18}\z') { throw 'Missing or malformed build artifact ID.' }
    $artifact = Read-PolicyJson "repos/$repository/actions/artifacts/$($env:EXPECTED_ARTIFACT_ID)"
    Assert-PcNinjaArtifactProvenance -Artifact $artifact -ExpectedId $env:EXPECTED_ARTIFACT_ID `
        -ExpectedDigest $env:EXPECTED_ARTIFACT_DIGEST -RunId $env:GITHUB_RUN_ID `
        -RepositoryId $env:GITHUB_REPOSITORY_ID -Commit $env:GITHUB_SHA
}
Write-Output "PASS: $Mode publication policy (read-only; no approval or release mutation)."
