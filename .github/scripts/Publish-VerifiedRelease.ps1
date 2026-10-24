param([string]$ArtifactDirectory = 'artifacts', [string]$PropsPath = 'Directory.Build.props',
    [Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AppUpdateRelease.psm1') -Force
$identity = Get-PcNinjaReleaseIdentity $PropsPath
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:IS_TAG -ne 'true' -or $env:REF_NAME -cne $identity.Tag -or
    $env:GITHUB_REPOSITORY -cne $identity.Repository -or [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw 'Publishing requires the matching tagged GitHub workflow, pinned repository and its ephemeral job token.'
}
$exe = Get-Item -LiteralPath (Join-Path $ArtifactDirectory $identity.AssetName)
if ($exe.PSIsContainer -or ($exe.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Final EXE must be a regular non-reparse file.' }
Assert-PcNinjaExecutableIdentity $identity $exe.VersionInfo.ProductName $exe.VersionInfo.FileVersion $exe.VersionInfo.ProductVersion $exe.Length $Commit

function Invoke-ReleaseApi {
    param([string]$Endpoint, [string]$Method = 'GET', $Body = $null, [switch]$AllowNotFound)
    $arguments = @('api', '--hostname', 'github.com', '--method', $Method, $Endpoint,
        '-H', 'Accept: application/vnd.github+json')
    if ($null -ne $Body) {
        $arguments += @('--input', '-')
        $output = ($Body | ConvertTo-Json -Depth 5 -Compress) | & gh @arguments 2>&1 | Out-String
    } else { $output = & gh @arguments 2>&1 | Out-String }
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        if ($AllowNotFound -and $output -match '\(HTTP 404\)') { return $null }
        throw "GitHub release API operation failed (exit $code); publication stopped."
    }
    if ([string]::IsNullOrWhiteSpace($output)) { return $null }
    $output | ConvertFrom-Json -Depth 16
}

$repository = $identity.Repository
$adapter = {
    param($operation, $parameters)
    switch ($operation) {
        'GetRelease' { Invoke-ReleaseApi "repos/$repository/releases/tags/$($parameters.Tag)" -AllowNotFound }
        'GetLatest' { Invoke-ReleaseApi "repos/$repository/releases/latest" -AllowNotFound }
        'CreateDraft' {
            Invoke-ReleaseApi "repos/$repository/releases" -Method POST -Body @{
                tag_name = $parameters.Tag; target_commitish = $parameters.Commit; name = $parameters.Name
                body = $parameters.Notes; draft = $true; prerelease = $parameters.Prerelease; make_latest = 'false'
            }
        }
        'UploadAssets' {
            $arguments = @('release', 'upload', $parameters.Tag) + @($parameters.Paths) + @('--repo', "https://github.com/$repository")
            # Intentionally no --clobber: a conflicting asset must fail, never be deleted/replaced.
            $output = & gh @arguments 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw 'GitHub release upload failed; the draft remains unpublished.' }
        }
        'Publish' {
            Invoke-ReleaseApi "repos/$repository/releases/$($parameters.Id)" -Method PATCH -Body @{
                draft = $false; prerelease = $parameters.Prerelease; make_latest = $(if ($parameters.Latest) { 'true' } else { 'false' })
            }
        }
        default { throw 'Unknown release publication operation.' }
    }
}
Publish-PcNinjaVerifiedRelease $identity $ArtifactDirectory (Get-Content -LiteralPath $identity.NotesPath -Raw) $Commit $adapter
