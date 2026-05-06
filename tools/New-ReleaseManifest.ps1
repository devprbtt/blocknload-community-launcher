param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$LauncherPath,

    [Parameter(Mandatory = $true)]
    [string]$UpdaterPath,

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
    $OutputPath = "K:\BNL EXPORTED\v2\release\$Version\manifest-$Channel.json"
}

$LauncherResolved = (Resolve-Path $LauncherPath).Path
$UpdaterResolved = (Resolve-Path $UpdaterPath).Path
$LauncherFileName = [IO.Path]::GetFileName($LauncherResolved)
$UpdaterFileName = [IO.Path]::GetFileName($UpdaterResolved)

$BaseUrl = "https://github.com/$Repository/releases/download/$ReleaseTag"

$Manifest = [ordered]@{
    product = $Product
    channel = $Channel
    version = $Version
    minimum_supported_version = $MinimumSupportedVersion
    published_at = [DateTimeOffset]::UtcNow.ToString("o")
    notes = $Notes
    assets = [ordered]@{
        launcher_exe = [ordered]@{
            file_name = $LauncherFileName
            url = "$BaseUrl/$LauncherFileName"
            sha256 = (Get-FileHash -LiteralPath $LauncherResolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $LauncherResolved).Length
        }
        updater_exe = [ordered]@{
            file_name = $UpdaterFileName
            url = "$BaseUrl/$UpdaterFileName"
            sha256 = (Get-FileHash -LiteralPath $UpdaterResolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $UpdaterResolved).Length
        }
    }
}

[IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
$Json = $Manifest | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $OutputPath -Value $Json -Encoding UTF8

Write-Output "Wrote $OutputPath"
