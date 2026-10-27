# Offline policy fixtures. Never requests GitHub, approves, publishes or launches an EXE.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '../.github/scripts/PublicationPolicy.psm1') -Force
function Clone-Json($Value) { $Value | ConvertTo-Json -Depth 16 | ConvertFrom-Json -Depth 16 }
function MustReject([scriptblock]$Action) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (-not $failed) { throw 'Unsafe publication policy fixture was accepted.' }
}
$goodEnvironment = [pscustomobject]@{
    name = 'release'; can_admins_bypass = $false
    deployment_branch_policy = @{ custom_branch_policies = $true; protected_branches = $false }
    protection_rules = @(@{ type = 'required_reviewers'; prevent_self_review = $false
        reviewers = @(@{ type = 'User'; reviewer = @{ id = 153079326 } }) })
}
$goodPolicies = [pscustomobject]@{ total_count = 1; branch_policies = @(@{ type = 'tag'; name = 'v*' }) }
Assert-PcNinjaReleaseEnvironment $goodEnvironment $goodPolicies
foreach ($mutate in @(
    { param($e) $e.name = 'other' },
    { param($e) $e.can_admins_bypass = $true },
    { param($e) $e.can_admins_bypass = $null },
    { param($e) $e.protection_rules = @() },
    { param($e) $e.protection_rules[0].reviewers[0].reviewer.id = 42 },
    { param($e) $e.protection_rules[0].reviewers += $e.protection_rules[0].reviewers[0] },
    { param($e) $e.protection_rules[0].prevent_self_review = $true },
    { param($e) $e.deployment_branch_policy.custom_branch_policies = $false },
    { param($e) $e.deployment_branch_policy.protected_branches = $true }
)) {
    $bad = Clone-Json $goodEnvironment; & $mutate $bad
    MustReject { Assert-PcNinjaReleaseEnvironment $bad $goodPolicies }
}
foreach ($mutate in @(
    { param($p) $p.branch_policies[0].type = 'branch' },
    { param($p) $p.branch_policies[0].name = '*' },
    { param($p) $p.total_count = 2 },
    { param($p) $p.branch_policies += $p.branch_policies[0] }
)) {
    $bad = Clone-Json $goodPolicies; & $mutate $bad
    MustReject { Assert-PcNinjaReleaseEnvironment $goodEnvironment $bad }
}
$digest = 'a' * 64; $commit = 'b' * 40
$goodArtifact = [pscustomobject]@{ id = 123; expired = $false; digest = "sha256:$digest"
    workflow_run = @{ id = 456; repository_id = 1413097416; head_repository_id = 1413097416; head_sha = $commit } }
function Check-Artifact($Value, [string]$Id = '123', [string]$Hash = $digest) {
    Assert-PcNinjaArtifactProvenance -Artifact $Value -ExpectedId $Id -ExpectedDigest $Hash `
        -RunId '456' -RepositoryId '1413097416' -Commit $commit
}
Check-Artifact $goodArtifact
Check-Artifact $goodArtifact -Hash "sha256:$digest"
foreach ($mutate in @(
    { param($a) $a.id = 124 },
    { param($a) $a.expired = $true },
    { param($a) $a.expired = $null },
    { param($a) $a.workflow_run.id = 457 },
    { param($a) $a.workflow_run.repository_id = 42 },
    { param($a) $a.workflow_run.head_repository_id = 42 },
    { param($a) $a.workflow_run.head_sha = 'c' * 40 },
    { param($a) $a.digest = 'sha256:' + ('c' * 64) },
    { param($a) $a.digest = $null }
)) {
    $bad = Clone-Json $goodArtifact; & $mutate $bad
    MustReject { Check-Artifact $bad }
}
foreach ($id in @('0', '-1', '123,124', '1e2', '9223372036854775808', '123/../../other')) {
    MustReject { Check-Artifact $goodArtifact -Id $id }
}
foreach ($hash in @('bad', ('SHA256:' + $digest), ($digest + '00'))) {
    MustReject { Check-Artifact $goodArtifact -Hash $hash }
}
Write-Output 'PASS: Owner/tag/no-bypass policy and exact-run/repository/source/artifact/digest fixtures. No API, approval, publication or EXE.'
