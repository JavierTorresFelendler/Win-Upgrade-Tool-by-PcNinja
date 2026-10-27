Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Pure validators: no API requests, credentials, file writes or publication.
function Assert-PcNinjaReleaseEnvironment {
    param([Parameter(Mandatory)]$Environment, [Parameter(Mandatory)]$Policies)
    if ($Environment.name -cne 'release' -or $Environment.can_admins_bypass -isnot [bool] -or
        $Environment.can_admins_bypass -or
        $Environment.deployment_branch_policy.custom_branch_policies -isnot [bool] -or
        -not $Environment.deployment_branch_policy.custom_branch_policies -or
        $Environment.deployment_branch_policy.protected_branches -isnot [bool] -or
        $Environment.deployment_branch_policy.protected_branches) {
        throw 'Release environment is missing its non-bypassable custom-tag protection.'
    }
    $required = @($Environment.protection_rules | Where-Object { $_.type -ceq 'required_reviewers' })
    if ($required.Count -ne 1 -or $required[0].prevent_self_review -isnot [bool] -or
        $required[0].prevent_self_review -or @($required[0].reviewers).Count -ne 1 -or
        $required[0].reviewers[0].type -cne 'User' -or
        $required[0].reviewers[0].reviewer.id -ne 153079326) {
        throw 'Release publication requires exactly the owner reviewer, with deliberate self-review allowed.'
    }
    $tags = @($Policies.branch_policies)
    if ($Policies.total_count -ne 1 -or $tags.Count -ne 1 -or
        $tags[0].type -cne 'tag' -or $tags[0].name -cne 'v*') {
        throw 'Release environment must permit only version tags, never branches or wider policies.'
    }
}

function ConvertTo-PcNinjaPositiveId {
    param([Parameter(Mandatory)][string]$Value)
    $parsed = 0L
    if ($Value -cnotmatch '\A[1-9][0-9]{0,18}\z' -or -not [long]::TryParse($Value, [ref]$parsed)) {
        throw 'GitHub artifact/run/repository identity must be a positive bounded integer.'
    }
    $parsed
}

function Assert-PcNinjaArtifactProvenance {
    param([Parameter(Mandatory)]$Artifact,
        [Parameter(Mandatory)][string]$ExpectedId,
        [Parameter(Mandatory)][string]$ExpectedDigest,
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$RepositoryId,
        [Parameter(Mandatory)][string]$Commit)
    $id = ConvertTo-PcNinjaPositiveId $ExpectedId
    $run = ConvertTo-PcNinjaPositiveId $RunId
    $repository = ConvertTo-PcNinjaPositiveId $RepositoryId
    $digest = $ExpectedDigest -creplace '\Asha256:', ''
    if ($digest -cnotmatch '\A[a-f0-9]{64}\z' -or $Commit -cnotmatch '\A[a-f0-9]{40}\z' -or
        $Artifact.id -ne $id -or $Artifact.expired -isnot [bool] -or $Artifact.expired -or
        $Artifact.workflow_run.id -ne $run -or
        $Artifact.workflow_run.repository_id -ne $repository -or
        $Artifact.workflow_run.head_repository_id -ne $repository -or
        $Artifact.workflow_run.head_sha -cne $Commit -or $Artifact.digest -cne "sha256:$digest") {
        throw 'Release artifact is not the exact unexpired build from this run, repository, source and upload digest.'
    }
}

Export-ModuleMember -Function Assert-PcNinjaReleaseEnvironment, Assert-PcNinjaArtifactProvenance
