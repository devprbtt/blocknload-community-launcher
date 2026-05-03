param(
    [string]$Version = "2.0.0",
    [string]$Repository = "devprbtt/blocknload-community-launcher",
    [string]$ReleaseTag = "v2.0.0",
    [string]$Channel = "stable",
    [string]$MinimumSupportedVersion = "1.9.0"
)

$ErrorActionPreference = "Stop"

$RepoRoot = "K:\BNL EXPORTED\v2"
$NotesFile = Join-Path $RepoRoot "release-notes\v$Version.md"

if (-not (Test-Path $NotesFile)) {
    throw "Release notes file not found: $NotesFile"
}

& (Join-Path $RepoRoot "tools\Build-ReleaseArtifacts.ps1") `
    -Version $Version `
    -Repository $Repository `
    -ReleaseTag $ReleaseTag `
    -Channel $Channel `
    -MinimumSupportedVersion $MinimumSupportedVersion `
    -NotesFile $NotesFile

$ReleaseRoot = Join-Path $RepoRoot "release\$Version"
$ManifestPath = Join-Path $ReleaseRoot "manifest-$Channel.json"

Write-Output ""
Write-Output "Next steps:"
Write-Output "1. Create GitHub release tag: $ReleaseTag"
Write-Output "2. Upload:"
Write-Output "   - $ReleaseRoot\launcher\BnlCommunityFixes.exe"
Write-Output "   - $ReleaseRoot\updater\BnlUpdater.exe"
Write-Output "3. Publish manifest:"
Write-Output "   & `"$RepoRoot\tools\Publish-ReleaseManifest.ps1`" -SourceManifestPath `"$ManifestPath`" -Channel `"$Channel`""
Write-Output "4. Commit and push: v2\updates\manifest-$Channel.json"
