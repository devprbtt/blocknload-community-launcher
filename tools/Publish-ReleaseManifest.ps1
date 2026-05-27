param(
    [Parameter(Mandatory = $true)]
    [string]$SourceManifestPath,

    [string]$Channel = "stable",

    [string]$DestinationRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "updates"
}

$SourceResolved = (Resolve-Path $SourceManifestPath).Path
$DestinationPath = Join-Path $DestinationRoot "manifest-$Channel.json"

[IO.Directory]::CreateDirectory($DestinationRoot) | Out-Null
Copy-Item -LiteralPath $SourceResolved -Destination $DestinationPath -Force

Write-Output "Published manifest to $DestinationPath"
