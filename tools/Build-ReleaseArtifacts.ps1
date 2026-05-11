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

    [string]$NotesFile = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = "K:\BNL EXPORTED\v2"
$LauncherProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Launcher\BnlCommunityFixes.Launcher.csproj"
$UpdaterProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Updater\BnlCommunityFixes.Updater.csproj"
$ReplayAnalyzerProject = Join-Path $RepoRoot "src\BnlCommunityFixes.ReplayAnalyzer\BnlCommunityFixes.ReplayAnalyzer.csproj"

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
New-Item -ItemType Directory -Force -Path $LauncherOutput, $UpdaterOutput | Out-Null

dotnet publish $LauncherProject @PublishArgs -o $LauncherOutput | Out-Null
dotnet publish $UpdaterProject @PublishArgs -o $UpdaterOutput | Out-Null
dotnet publish $ReplayAnalyzerProject @PublishArgs -o $LauncherOutput | Out-Null

$LauncherExe = Join-Path $LauncherOutput "BnlCommunityFixes.exe"
$UpdaterExe = Join-Path $UpdaterOutput "BnlUpdater.exe"
$ReplayAnalyzerExe = Join-Path $LauncherOutput "BnlCommunityFixes.ReplayAnalyzer.exe"

if (-not (Test-Path $LauncherExe)) {
    throw "Launcher publish output not found: $LauncherExe"
}

if (-not (Test-Path $UpdaterExe)) {
    throw "Updater publish output not found: $UpdaterExe"
}

if (-not (Test-Path $ReplayAnalyzerExe)) {
    throw "Replay analyzer publish output not found: $ReplayAnalyzerExe"
}

$ManifestPath = $null
if (-not [string]::IsNullOrWhiteSpace($Repository)) {
    $ManifestPath = Join-Path $OutputRoot "manifest-$Channel.json"
    & (Join-Path $RepoRoot "tools\New-ReleaseManifest.ps1") `
        -Version $Version `
        -LauncherPath $LauncherExe `
        -UpdaterPath $UpdaterExe `
        -ReplayAnalyzerPath $ReplayAnalyzerExe `
        -Repository $Repository `
        -ReleaseTag $ReleaseTag `
        -Channel $Channel `
        -MinimumSupportedVersion $MinimumSupportedVersion `
        -Notes $Notes `
        -NotesFile $NotesFile `
        -OutputPath $ManifestPath | Out-Null
}

Write-Output "Built launcher: $LauncherExe"
Write-Output "Built updater: $UpdaterExe"
Write-Output "Built replay analyzer: $ReplayAnalyzerExe"
if ($ManifestPath) {
    Write-Output "Built manifest: $ManifestPath"
}
