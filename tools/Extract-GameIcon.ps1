param(
    [Parameter(Mandatory=$true)]
    [string]$GameExe,

    [Parameter(Mandatory=$true)]
    [string]$OutIcon
)

Add-Type -AssemblyName System.Drawing
if (-not (Test-Path $GameExe)) {
    throw "Game executable not found: $GameExe"
}

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($GameExe)
$dir = Split-Path $OutIcon -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$fs = [System.IO.File]::Open($OutIcon, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()
Write-Output "Wrote: $OutIcon"
