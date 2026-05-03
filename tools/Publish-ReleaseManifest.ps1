param(
    [Parameter(Mandatory = $true)]
    [string]$SourceManifestPath,

    [string]$Channel = "stable",

    [string]$DestinationRoot = "K:\BNL EXPORTED\v2\updates"
)

$ErrorActionPreference = "Stop"

$SourceResolved = (Resolve-Path $SourceManifestPath).Path
$DestinationPath = Join-Path $DestinationRoot "manifest-$Channel.json"

[IO.Directory]::CreateDirectory($DestinationRoot) | Out-Null
Copy-Item -LiteralPath $SourceResolved -Destination $DestinationPath -Force

Write-Output "Published manifest to $DestinationPath"
