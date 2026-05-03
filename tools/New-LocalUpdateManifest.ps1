param(
    [Parameter(Mandatory = $true)]
    [string]$LauncherPath,

    [Parameter(Mandatory = $true)]
    [string]$UpdaterPath,

    [string]$Version = "2.0.1",

    [string]$OutputPath = "K:\BNL EXPORTED\v2\testdata\manifest-local.json"
)

$LauncherResolved = (Resolve-Path $LauncherPath).Path
$UpdaterResolved = (Resolve-Path $UpdaterPath).Path

$Manifest = [ordered]@{
    product = "BnlCommunityFixes"
    channel = "stable"
    version = $Version
    minimum_supported_version = "2.0.0"
    published_at = [DateTimeOffset]::UtcNow.ToString("o")
    notes = "Generated local update manifest."
    assets = [ordered]@{
        launcher_exe = [ordered]@{
            file_name = [IO.Path]::GetFileName($LauncherResolved)
            url = ([Uri]$LauncherResolved).AbsoluteUri
            sha256 = (Get-FileHash -LiteralPath $LauncherResolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $LauncherResolved).Length
        }
        updater_exe = [ordered]@{
            file_name = [IO.Path]::GetFileName($UpdaterResolved)
            url = ([Uri]$UpdaterResolved).AbsoluteUri
            sha256 = (Get-FileHash -LiteralPath $UpdaterResolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $UpdaterResolved).Length
        }
    }
}

$Json = $Manifest | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $OutputPath -Value $Json -Encoding UTF8
Write-Output "Wrote $OutputPath"
