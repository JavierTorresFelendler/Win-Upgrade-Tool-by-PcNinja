# Offline source-contract checks, not a YAML parser or a proof of remote GitHub settings.
# Deliberately keep the supported workflow structure explicit and reviewable.
param(
    [string]$ReleasePath = (Join-Path $PSScriptRoot '../.github/workflows/release.yml'),
    [string]$PullRequestPath = (Join-Path $PSScriptRoot '../.github/workflows/pr-checks.yml'),
    [switch]$SelfTestOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Normalize-Workflow([string]$Text) {
    Require ($Text.Length -lt 131072) 'Workflow source exceeds the bounded contract fixture size.'
    $lines = $Text.Replace("`r", '').Split("`n")
    Require (-not @($lines | Where-Object { $_.Contains("`t") }).Count) 'Workflow indentation must use spaces.'
    ($lines | Where-Object { $_ -notmatch '^\s*#' }) -join "`n"
}

function Get-Section([string]$Text, [string]$Key, [int]$Indent) {
    $lines = $Text.Split("`n")
    $prefix = ' ' * $Indent
    $matches = @(0..($lines.Count - 1) | Where-Object { $lines[$_] -match ('^' + $prefix + [regex]::Escape($Key) + ':\s*$') })
    Require ($matches.Count -eq 1) "Expected exactly one explicit $Key mapping at indentation $Indent."
    $start = $matches[0]
    $end = $start + 1
    while ($end -lt $lines.Count) {
        if ($lines[$end] -match '\S' -and ($lines[$end].Length - $lines[$end].TrimStart().Length) -le $Indent) { break }
        $end++
    }
    $lines[$start..($end - 1)] -join "`n"
}

function Assert-Permissions([string]$Text, [int]$Indent, [string]$Contents, [bool]$Actions = $true) {
    $section = Get-Section $Text 'permissions' $Indent
    $entries = @($section.Split("`n") | Select-Object -Skip 1 | Where-Object { $_ -match '\S' })
    $expectedCount = if ($Actions) { 2 } else { 1 }
    Require ($entries.Count -eq $expectedCount) 'Unexpected inherited or additional workflow permissions.'
    Require ($section -match ('(?m)^' + (' ' * ($Indent + 2)) + 'contents:\s*' + $Contents + '\s*$')) 'Contents permission does not match its trust boundary.'
    if ($Actions) {
        Require ($section -match ('(?m)^' + (' ' * ($Indent + 2)) + 'actions:\s*read\s*$')) 'Actions metadata permission must remain read-only.'
    }
}

function Get-Steps([string]$Job) {
    $lines = $Job.Split("`n")
    $starts = @(0..($lines.Count - 1) | Where-Object { $lines[$_] -match '^      -\s+' })
    Require ($starts.Count -gt 0) 'Expected explicit six-space-indented workflow steps.'
    for ($i = 0; $i -lt $starts.Count; $i++) {
        $last = if ($i + 1 -lt $starts.Count) { $starts[$i + 1] - 1 } else { $lines.Count - 1 }
        $lines[$starts[$i]..$last] -join "`n"
    }
}

function Find-Step([string[]]$Steps, [string]$Pattern) {
    $found = @($Steps | Where-Object { $_ -match $Pattern })
    Require ($found.Count -eq 1) 'Expected one unambiguous protected workflow step.'
    $found[0]
}

function Assert-TagGate([string]$Text, [int]$Indent) {
    $line = @($Text.Split("`n") | Where-Object { $_ -match ('^' + (' ' * $Indent) + 'if:') })
    Require ($line.Count -eq 1) 'Expected one explicit tag-only publication condition.'
    $value = ($line[0] -replace '^\s*if:\s*', '').Trim()
    $value = $value -replace '^\$\{\{\s*', '' -replace '\s*\}\}$', ''
    Require ($value -match '^startsWith\(\s*github\.ref\s*,\s*''refs/tags/''\s*\)$') 'Publication must be gated by the current tag ref only.'
}

function Assert-ActionPinsAndTokens([string]$Text) {
    Require ($Text -notmatch '(?m)^\s*<<:|:\s*[&*][A-Za-z]') 'YAML aliases/merges are outside the explicit security contract.'
    foreach ($match in [regex]::Matches($Text, '(?m)^\s*(?:-\s*)?uses:\s*([^\s#]+)')) {
        Require ($match.Groups[1].Value -match '^[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[a-f0-9]{40}$') 'Every external action must be pinned to a full commit SHA.'
    }
    foreach ($line in $Text.Split("`n")) {
        if ($line -match '^\s*(?:GH_TOKEN|GITHUB_TOKEN):') {
            Require ($line -match '^          (?:GH_TOKEN|GITHUB_TOKEN):\s*\$\{\{\s*github\.token\s*\}\}\s*$') 'Only the built-in token in a step-scoped env is allowed.'
        }
    }
    Require ($Text -notmatch '\$\{\{\s*secrets\.') 'A repository PAT/secret must not replace the built-in scoped token.'
    Require ($Text -notmatch '(?i)pull_request_target:|workflow_run:|pending_deployments|gh\s+run\s+approve|gh\s+pr\s+merge|--admin\b') 'Workflow must not approve itself or consume privileged untrusted events.'
}

function Assert-ReleaseWorkflow([string]$Release, [string]$PullRequest) {
    $releaseSource = Normalize-Workflow $Release
    $prSource = Normalize-Workflow $PullRequest
    Assert-ActionPinsAndTokens $releaseSource
    Assert-ActionPinsAndTokens $prSource
    Assert-Permissions $releaseSource 0 'read'
    Assert-Permissions $prSource 0 'read' $false
    Require ($prSource -notmatch '(?m)^\s+contents:\s*write\s*$') 'PR validation must not receive repository write permission.'
    $jobs = Get-Section $releaseSource 'jobs' 0
    $jobNames = @([regex]::Matches($jobs, '(?m)^  ([A-Za-z0-9_-]+):\s*$') | ForEach-Object { $_.Groups[1].Value })
    Require ($jobNames.Count -eq 2 -and 'build' -in $jobNames -and 'publish' -in $jobNames) 'Review any additional release trust boundary explicitly.'
    $build = Get-Section $jobs 'build' 2
    $publish = Get-Section $jobs 'publish' 2
    Assert-Permissions $build 4 'read'
    Assert-Permissions $publish 4 'write'
    Require ($build -notmatch 'Publish-VerifiedRelease\.ps1|\bgh\s+release\b') 'Read-only build must not publish a GitHub release.'
    Require ($publish -notmatch 'Prepare-ReleaseAssets\.ps1|dotnet\s+(?:build|publish|run)\b') 'Privileged publish must not rebuild or regenerate trusted artifact metadata.'
    Require ($publish -match '(?m)^    needs:\s*build\s*$') 'Publisher must depend on the successful current build.'
    Assert-TagGate $publish 4
    Require ($publish -match '(?m)^    environment:\s*release\s*$' -or
        $publish -match '(?m)^    environment:\s*\n      name:\s*release\s*$') 'Publisher must wait for the pre-protected release environment.'
    $outputs = Get-Section $build 'outputs' 4
    Require ($outputs -match '(?m)^      artifact_id:\s*\$\{\{\s*steps\.upload_artifact\.outputs\.artifact-id\s*\}\}\s*$') 'Artifact ID must come from this build upload, not a name lookup.'
    Require ($outputs -match '(?m)^      artifact_digest:\s*\$\{\{\s*steps\.upload_artifact\.outputs\.artifact-digest\s*\}\}\s*$') 'Artifact digest must come from the same build upload.'
    $buildSteps = @(Get-Steps $build)
    $publishSteps = @(Get-Steps $publish)
    foreach ($steps in @($buildSteps, $publishSteps)) {
        $checkout = Find-Step $steps 'uses:\s*actions/checkout@'
        Require ($checkout -match '(?m)^          ref:\s*\$\{\{\s*github\.sha\s*\}\}\s*$') 'Checkout must bind the exact triggering commit.'
        Require ($checkout -match '(?m)^          persist-credentials:\s*false\s*$') 'Checkout must not persist the job credential in git configuration.'
    }
    $upload = Find-Step $buildSteps 'uses:\s*actions/upload-artifact@'
    Require ($upload -match '(?m)^(?:        id:|      - id:)\s*upload_artifact\s*$') 'Build artifact output must identify the exact upload step.'
    $environmentCheck = Find-Step $buildSteps 'Assert-PublicationPolicy\.ps1\s+-Mode\s+Environment\b'
    Assert-TagGate $environmentCheck 8
    $artifactCheck = Find-Step $publishSteps 'Assert-PublicationPolicy\.ps1\s+-Mode\s+Artifact\b'
    Require ($artifactCheck -match '(?m)^          EXPECTED_ARTIFACT_ID:\s*\$\{\{\s*needs\.build\.outputs\.artifact_id\s*\}\}\s*$' -and
        $artifactCheck -match '(?m)^          EXPECTED_ARTIFACT_DIGEST:\s*\$\{\{\s*needs\.build\.outputs\.artifact_digest\s*\}\}\s*$') 'Artifact provenance must verify the expected ID and digest from the build.'
    $download = Find-Step $publishSteps 'uses:\s*actions/download-artifact@'
    Require ($download -match '(?m)^          artifact-ids:\s*\$\{\{\s*needs\.build\.outputs\.artifact_id\s*\}\}\s*$') 'Publisher must download the exact artifact ID from this run.'
    Require ($download -match '(?m)^          path:\s*artifacts\s*$' -and
        $download -match '(?m)^          merge-multiple:\s*true\s*$' -and
        $download -match '(?m)^          digest-mismatch:\s*error\s*$') 'Artifact extraction path/digest policy changed.'
    Require ($download -notmatch '(?m)^          (?:name|pattern|repository|run-id|github-token):') 'Cross-run/name-based artifact lookup is not authorized.'
    Require ([array]::IndexOf($publishSteps, $artifactCheck) -lt [array]::IndexOf($publishSteps, $download)) 'Artifact API provenance must be checked before download.'
    $identity = Find-Step $publishSteps 'Resolve-ReleaseIdentity\.ps1'
    $publisher = Find-Step $publishSteps 'Publish-VerifiedRelease\.ps1'
    Require ([array]::IndexOf($publishSteps, $download) -lt [array]::IndexOf($publishSteps, $identity) -and
        [array]::IndexOf($publishSteps, $identity) -lt [array]::IndexOf($publishSteps, $publisher)) 'Downloaded bytes and reviewed ancestry must be checked before publishing.'
    Require ($publisher -match '(?m)^        run:\s*\./\.github/scripts/Publish-VerifiedRelease\.ps1\s+-Commit\s+\$env:GITHUB_SHA\s*$') 'Existing verified publisher must bind the exact source commit.'
}

function Must-Reject([string]$Release, [string]$PullRequest, [string]$Description) {
    $rejected = $false
    try { Assert-ReleaseWorkflow $Release $PullRequest } catch { $rejected = $true }
    Require $rejected "Unsafe workflow accepted: $Description"
}

# In-memory fixtures exercise the checker itself without GitHub/API/workflow effects.
$fixture = @'
name: Release contract fixture
on:
  push:
    tags: ['v*']
permissions:
  contents: read
  actions: read
jobs:
  build:
    permissions:
      contents: read
      actions: read
    outputs:
      artifact_id: ${{ steps.upload_artifact.outputs.artifact-id }}
      artifact_digest: ${{ steps.upload_artifact.outputs.artifact-digest }}
    steps:
      - uses: actions/checkout@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
        with:
          ref: ${{ github.sha }}
          persist-credentials: false
      - name: Validate environment
        if: startsWith(github.ref, 'refs/tags/')
        env:
          GH_TOKEN: ${{ github.token }}
        run: ./.github/scripts/Assert-PublicationPolicy.ps1 -Mode Environment
      - uses: actions/upload-artifact@bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        id: upload_artifact
  publish:
    needs: build
    if: startsWith(github.ref, 'refs/tags/')
    environment: release
    permissions:
      contents: write
      actions: read
    steps:
      - uses: actions/checkout@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
        with:
          ref: ${{ github.sha }}
          persist-credentials: false
      - name: Validate artifact provenance
        env:
          GH_TOKEN: ${{ github.token }}
          EXPECTED_ARTIFACT_ID: ${{ needs.build.outputs.artifact_id }}
          EXPECTED_ARTIFACT_DIGEST: ${{ needs.build.outputs.artifact_digest }}
        run: ./.github/scripts/Assert-PublicationPolicy.ps1 -Mode Artifact
      - uses: actions/download-artifact@cccccccccccccccccccccccccccccccccccccccc
        with:
          artifact-ids: ${{ needs.build.outputs.artifact_id }}
          path: artifacts
          merge-multiple: true
          digest-mismatch: error
      - name: Revalidate release identity
        run: ./.github/scripts/Resolve-ReleaseIdentity.ps1
      - name: Publish verified release
        env:
          GH_TOKEN: ${{ github.token }}
        run: ./.github/scripts/Publish-VerifiedRelease.ps1 -Commit $env:GITHUB_SHA
'@
$prFixture = @'
name: PR contract fixture
on:
  pull_request:
permissions:
  contents: read
jobs:
  validate:
    steps:
      - uses: actions/checkout@aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
'@
# Git checkout can materialize this script as CRLF on hosted Windows runners.
# Normalize before exact mutation targeting, not only inside the checker.
$fixture = $fixture.Replace("`r", '')
$prFixture = $prFixture.Replace("`r", '')
Assert-ReleaseWorkflow $fixture $prFixture
Assert-ReleaseWorkflow ($fixture.Replace("`n", "`r`n")) ($prFixture.Replace("`n", "`r`n"))
foreach ($mutation in @(
    @("  build:`n    permissions:`n      contents: read", "  build:`n    permissions:`n      contents: write", 'write token in build'),
    @('    environment: release', '    environment: unprotected', 'missing protected environment'),
    @('          artifact-ids: ${{ needs.build.outputs.artifact_id }}', '          name: arbitrary-artifact', 'artifact name lookup'),
    @('actions/download-artifact@cccccccccccccccccccccccccccccccccccccccc', 'actions/download-artifact@v8', 'unpinned action'),
    @('          ref: ${{ github.sha }}', '          ref: main', 'mutable checkout ref'),
    @("    if: startsWith(github.ref, 'refs/tags/')", '    if: always()', 'publisher without tag gate'),
    @('          digest-mismatch: error', '          digest-mismatch: warn', 'digest mismatch warning'),
    @('EXPECTED_ARTIFACT_DIGEST: ${{ needs.build.outputs.artifact_digest }}', 'EXPECTED_ARTIFACT_DIGEST: untrusted', 'missing artifact digest provenance'),
    @('Assert-PublicationPolicy.ps1 -Mode Artifact', 'Assert-PublicationPolicy.ps1 -Mode Environment', 'missing artifact provenance gate'),
    @('Resolve-ReleaseIdentity.ps1', 'Prepare-ReleaseAssets.ps1', 'privileged metadata regeneration'),
    @('GH_TOKEN: ${{ github.token }}', 'GH_TOKEN: ${{ secrets.PUBLISH_PAT }}', 'long-lived secret token')
)) {
    Require ($fixture.Contains($mutation[0])) "Fixture mutation target is missing: $($mutation[2])"
    Must-Reject ($fixture.Replace($mutation[0], $mutation[1])) $prFixture $mutation[2]
}
Must-Reject $fixture ($prFixture.Replace('contents: read', 'contents: write')) 'PR write permission'
$wrongOrder = $fixture.Replace('Resolve-ReleaseIdentity.ps1', '__RESOLVE__')
$wrongOrder = $wrongOrder.Replace('Publish-VerifiedRelease.ps1', 'Resolve-ReleaseIdentity.ps1')
$wrongOrder = $wrongOrder.Replace('__RESOLVE__', 'Publish-VerifiedRelease.ps1')
Must-Reject $wrongOrder $prFixture 'publication before ancestry validation'
Write-Output 'PASS: Workflow checker rejects unsafe privilege, approval, artifact, digest, ref, tag, action-pin and token mutations. In-memory fixtures only.'
if (-not $SelfTestOnly) {
    Assert-ReleaseWorkflow (Get-Content -LiteralPath $ReleasePath -Raw) (Get-Content -LiteralPath $PullRequestPath -Raw)
    Write-Output 'PASS: Read-only build, owner-gated tag publisher and exact same-run artifact/provenance contract. No network, workflow dispatch, release, executable or Windows operation.'
}
