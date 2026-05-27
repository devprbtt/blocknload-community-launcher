param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RuntimeIdentifier = "win-x64",

    [string]$Configuration = "Release",

    [string]$OutputRoot = "",

    [string]$Repository = "",

    [string]$ReleaseTag = "",

    [string]$Channel = "stable",

    [string]$MinimumSupportedVersion = "",

    [string]$Notes = "",

    [string]$NotesFile = "",

    [switch]$Portable,
    [switch]$IncludeLegacyUpdater
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$LauncherProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Launcher\BnlCommunityFixes.Launcher.csproj"
$UpdaterProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Updater\BnlCommunityFixes.Updater.csproj"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot "release\$Version"
}

$LauncherOutput = Join-Path $OutputRoot "launcher"
$UpdaterOutput = Join-Path $OutputRoot "updater"
$PublishArgs = @(
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:Version=$Version",
    "-p:AssemblyVersion=$Version",
    "-p:FileVersion=$Version",
    "-p:InformationalVersion=$Version",
    "-p:IncludeSourceRevisionInInformationalVersion=false"
)

Remove-Item -LiteralPath $OutputRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $LauncherOutput | Out-Null
if ($IncludeLegacyUpdater) {
    New-Item -ItemType Directory -Force -Path $UpdaterOutput | Out-Null
}

dotnet publish $LauncherProject @PublishArgs -o $LauncherOutput | Out-Null
if ($IncludeLegacyUpdater) {
    dotnet publish $UpdaterProject @PublishArgs -o $UpdaterOutput | Out-Null
}
if ($Portable -or $OutputRoot -like "*replay-launcher-test*") {
    Set-Content -LiteralPath (Join-Path $LauncherOutput "portable-launcher.flag") -Value "portable" -Encoding ASCII
}

$LauncherExe = Join-Path $LauncherOutput "BnlCommunityFixes.exe"
$UpdaterExe = Join-Path $UpdaterOutput "BnlUpdater.exe"

if (-not (Test-Path $LauncherExe)) {
    throw "Launcher publish output not found: $LauncherExe"
}

if ($IncludeLegacyUpdater -and -not (Test-Path $UpdaterExe)) {
    throw "Updater publish output not found: $UpdaterExe"
}

$ManifestPath = $null
if (-not [string]::IsNullOrWhiteSpace($Repository)) {
    $ManifestPath = Join-Path $OutputRoot "manifest-$Channel.json"
    $ManifestArgs = @(
        "-Version", $Version,
        "-LauncherPath", $LauncherExe,
        "-Repository", $Repository,
        "-ReleaseTag", $ReleaseTag,
        "-Channel", $Channel,
        "-MinimumSupportedVersion", $MinimumSupportedVersion,
        "-Notes", $Notes,
        "-NotesFile", $NotesFile,
        "-OutputPath", $ManifestPath
    )
    if ($IncludeLegacyUpdater) {
        $ManifestArgs += @("-UpdaterPath", $UpdaterExe)
    }
    & (Join-Path $RepoRoot "tools\New-ReleaseManifest.ps1") @ManifestArgs | Out-Null
}

Write-Output "Built launcher: $LauncherExe"
if ($IncludeLegacyUpdater) {
    Write-Output "Built updater: $UpdaterExe"
}
if ($ManifestPath) {
    Write-Output "Built manifest: $ManifestPath"
}
