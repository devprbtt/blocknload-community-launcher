param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$LauncherPath,

    [string]$LauncherExeByRidJson = "",

    [string]$UpdaterPath = "",

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [string]$ReleaseTag,

    [string]$Channel = "stable",

    [string]$MinimumSupportedVersion = "",

    [string]$Product = "BnlCommunityFixes",

    [string]$Notes = "",

    [string]$NotesFile = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $ReleaseTag = "v$Version"
}

if ([string]::IsNullOrWhiteSpace($MinimumSupportedVersion)) {
    $MinimumSupportedVersion = $Version
}

if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    $Notes = Get-Content -LiteralPath $NotesFile -Raw -Encoding UTF8
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "release\$Version\manifest-$Channel.json"
}

$BaseUrl = "https://github.com/$Repository/releases/download/$ReleaseTag"
$LauncherAssets = [ordered]@{}
$UpdaterResolved = $null
$UpdaterFileName = $null

if (-not [string]::IsNullOrWhiteSpace($LauncherExeByRidJson)) {
    $launcherByRid = $LauncherExeByRidJson | ConvertFrom-Json -AsHashtable
    foreach ($rid in $launcherByRid.Keys) {
        $resolved = (Resolve-Path $launcherByRid[$rid]).Path
        $fileName = [IO.Path]::GetFileName($resolved)
        $assetKey = "launcher_" + ($rid -replace '-', '_')
        $LauncherAssets[$assetKey] = [ordered]@{
            file_name = $fileName
            url = "$BaseUrl/$fileName"
            sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $resolved).Length
        }
    }

    if ($launcherByRid.ContainsKey("win-x64")) {
        $resolved = (Resolve-Path $launcherByRid["win-x64"]).Path
        $fileName = [IO.Path]::GetFileName($resolved)
        $LauncherAssets["launcher_exe"] = [ordered]@{
            file_name = $fileName
            url = "$BaseUrl/$fileName"
            sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $resolved).Length
        }
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($LauncherPath)) {
    $LauncherResolved = (Resolve-Path $LauncherPath).Path
    $LauncherFileName = [IO.Path]::GetFileName($LauncherResolved)
    $LauncherAssets["launcher_exe"] = [ordered]@{
        file_name = $LauncherFileName
        url = "$BaseUrl/$LauncherFileName"
        sha256 = (Get-FileHash -LiteralPath $LauncherResolved -Algorithm SHA256).Hash
        size = (Get-Item -LiteralPath $LauncherResolved).Length
    }
}
else {
    throw "Either -LauncherPath or -LauncherExeByRidJson must be provided."
}

if (-not [string]::IsNullOrWhiteSpace($UpdaterPath)) {
    $UpdaterResolved = (Resolve-Path $UpdaterPath).Path
    $UpdaterFileName = [IO.Path]::GetFileName($UpdaterResolved)
}

$Manifest = [ordered]@{
    product = $Product
    channel = $Channel
    version = $Version
    minimum_supported_version = $MinimumSupportedVersion
    published_at = [DateTimeOffset]::UtcNow.ToString("o")
    notes = $Notes
    assets = $LauncherAssets
}

if ($UpdaterResolved) {
    $Manifest.assets.updater_exe = [ordered]@{
        file_name = $UpdaterFileName
        url = "$BaseUrl/$UpdaterFileName"
        sha256 = (Get-FileHash -LiteralPath $UpdaterResolved -Algorithm SHA256).Hash
        size = (Get-Item -LiteralPath $UpdaterResolved).Length
    }
}

[IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
$Json = $Manifest | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $OutputPath -Value $Json -Encoding UTF8

Write-Output "Wrote $OutputPath"
