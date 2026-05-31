param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    # Comma-separated list of RIDs to publish. Default builds both Windows and Linux.
    [string]$RuntimeIdentifiers = "win-x64,linux-x64",

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
$LauncherProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Avalonia\BnlCommunityFixes.Avalonia.csproj"
$UpdaterProject  = Join-Path $RepoRoot "src\BnlCommunityFixes.Updater\BnlCommunityFixes.Updater.csproj"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot "release\$Version"
}

$BasePublishArgs = @(
    "-c", $Configuration,
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

# Publish one launcher binary per RID
$RIDs = $RuntimeIdentifiers -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
$LauncherExeByRid = @{}

foreach ($rid in $RIDs) {
    $ridOutput = Join-Path $OutputRoot "launcher-$rid"
    New-Item -ItemType Directory -Force -Path $ridOutput | Out-Null

    Write-Host "Publishing launcher for $rid..."
    dotnet publish $LauncherProject @BasePublishArgs -r $rid -o $ridOutput | Out-Null

    $exeName = if ($rid -like "win-*") { "BnlCommunityFixes.exe" } else { "BnlCommunityFixes" }
    $exePath = Join-Path $ridOutput $exeName

    if (-not (Test-Path $exePath)) {
        throw "Launcher publish output not found for ${rid}: $exePath"
    }

    if ($Portable) {
        Set-Content -LiteralPath (Join-Path $ridOutput "portable-launcher.flag") -Value "portable" -Encoding ASCII
    }

    $LauncherExeByRid[$rid] = $exePath
    Write-Output "Built launcher ($rid): $exePath"
}

# Optionally build the updater (Windows-only legacy component)
$UpdaterExe = $null
if ($IncludeLegacyUpdater) {
    $UpdaterOutput = Join-Path $OutputRoot "updater"
    New-Item -ItemType Directory -Force -Path $UpdaterOutput | Out-Null
    dotnet publish $UpdaterProject @BasePublishArgs -r "win-x64" -o $UpdaterOutput | Out-Null
    $UpdaterExe = Join-Path $UpdaterOutput "BnlUpdater.exe"
    if (-not (Test-Path $UpdaterExe)) {
        throw "Updater publish output not found: $UpdaterExe"
    }
    Write-Output "Built updater: $UpdaterExe"
}

# Generate a platform-aware manifest
if (-not [string]::IsNullOrWhiteSpace($Repository)) {
    $ManifestPath = Join-Path $OutputRoot "manifest-$Channel.json"
    $ManifestParams = @{
        Version                 = $Version
        LauncherExeByRidJson    = ($LauncherExeByRid | ConvertTo-Json -Compress)
        Repository              = $Repository
        ReleaseTag              = $ReleaseTag
        Channel                 = $Channel
        MinimumSupportedVersion = $MinimumSupportedVersion
        OutputPath              = $ManifestPath
    }
    if (-not [string]::IsNullOrWhiteSpace($Notes)) {
        $ManifestParams.Notes = $Notes
    }
    if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
        $ManifestParams.NotesFile = $NotesFile
    }
    if ($UpdaterExe) {
        $ManifestParams.UpdaterPath = $UpdaterExe
    }
    & (Join-Path $RepoRoot "tools\New-ReleaseManifest.ps1") @ManifestParams | Out-Null
    Write-Output "Built manifest: $ManifestPath"
}
