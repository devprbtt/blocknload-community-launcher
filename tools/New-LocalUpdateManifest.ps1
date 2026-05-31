param(
    [string]$LauncherPath,

    [string]$LauncherExeByRidJson = "",

    [string]$UpdaterPath = "",

    [string]$Version = "2.0.1",

    [string]$OutputPath = "K:\BNL EXPORTED\v2\testdata\manifest-local.json"
)

$LauncherAssets = [ordered]@{}
$UpdaterResolved = $null

if (-not [string]::IsNullOrWhiteSpace($LauncherExeByRidJson)) {
    $launcherByRid = $LauncherExeByRidJson | ConvertFrom-Json -AsHashtable
    foreach ($rid in $launcherByRid.Keys) {
        $resolved = (Resolve-Path $launcherByRid[$rid]).Path
        $fileName = [IO.Path]::GetFileName($resolved)
        $assetKey = "launcher_" + ($rid -replace '-', '_')
        $LauncherAssets[$assetKey] = [ordered]@{
            file_name = $fileName
            url = ([Uri]$resolved).AbsoluteUri
            sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $resolved).Length
        }
    }

    if ($launcherByRid.ContainsKey("win-x64")) {
        $resolved = (Resolve-Path $launcherByRid["win-x64"]).Path
        $fileName = [IO.Path]::GetFileName($resolved)
        $LauncherAssets["launcher_exe"] = [ordered]@{
            file_name = $fileName
            url = ([Uri]$resolved).AbsoluteUri
            sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $resolved).Length
        }
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($LauncherPath)) {
    $LauncherResolved = (Resolve-Path $LauncherPath).Path
    $LauncherAssets["launcher_exe"] = [ordered]@{
        file_name = [IO.Path]::GetFileName($LauncherResolved)
        url = ([Uri]$LauncherResolved).AbsoluteUri
        sha256 = (Get-FileHash -LiteralPath $LauncherResolved -Algorithm SHA256).Hash
        size = (Get-Item -LiteralPath $LauncherResolved).Length
    }
}
else {
    throw "Either -LauncherPath or -LauncherExeByRidJson must be provided."
}

if (-not [string]::IsNullOrWhiteSpace($UpdaterPath)) {
    $UpdaterResolved = (Resolve-Path $UpdaterPath).Path
}

$Manifest = [ordered]@{
    product = "BnlCommunityFixes"
    channel = "stable"
    version = $Version
    minimum_supported_version = "2.0.0"
    published_at = [DateTimeOffset]::UtcNow.ToString("o")
    notes = "Generated local update manifest."
    assets = $LauncherAssets
}

if ($UpdaterResolved) {
    $Manifest.assets.updater_exe = [ordered]@{
        file_name = [IO.Path]::GetFileName($UpdaterResolved)
        url = ([Uri]$UpdaterResolved).AbsoluteUri
        sha256 = (Get-FileHash -LiteralPath $UpdaterResolved -Algorithm SHA256).Hash
        size = (Get-Item -LiteralPath $UpdaterResolved).Length
    }
}

$Json = $Manifest | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $OutputPath -Value $Json -Encoding UTF8
Write-Output "Wrote $OutputPath"
