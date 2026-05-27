param(
    [string]$VersionFrom = "2.0.0",
    [string]$VersionTo = "2.0.1",
    [string]$TestRoot = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($TestRoot)) {
    $TestRoot = Join-Path $RepoRoot "test-output\smoke"
}
$LauncherProject = Join-Path $RepoRoot "src\BnlCommunityFixes.Launcher\BnlCommunityFixes.Launcher.csproj"
$PublishRoot = Join-Path $TestRoot "publish"
$InstallRoot = Join-Path $TestRoot "install-root"
$ManifestPath = Join-Path $TestRoot "manifest-local.json"
$LogLauncher = Join-Path $InstallRoot "logs\launcher.log"
$LogUpdater = Join-Path $InstallRoot "logs\updater.log"

Remove-Item -LiteralPath $TestRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $PublishRoot | Out-Null

$FromDir = Join-Path $PublishRoot $VersionFrom
$ToDir = Join-Path $PublishRoot $VersionTo
$FromLauncherDir = Join-Path $FromDir "launcher"
$ToLauncherDir = Join-Path $ToDir "launcher"
$PublishArgs = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

dotnet publish $LauncherProject @PublishArgs -o $FromLauncherDir -p:Version=$VersionFrom -p:AssemblyVersion=$VersionFrom -p:FileVersion=$VersionFrom | Out-Null
dotnet publish $LauncherProject @PublishArgs -o $ToLauncherDir -p:Version=$VersionTo -p:AssemblyVersion=$VersionTo -p:FileVersion=$VersionTo | Out-Null

$ToLauncherExe = Join-Path $ToLauncherDir "BnlCommunityFixes.exe"
& (Join-Path $RepoRoot "tools\New-LocalUpdateManifest.ps1") -LauncherPath $ToLauncherExe -Version $VersionTo -OutputPath $ManifestPath | Out-Null

$AppDir = Join-Path $InstallRoot "app"
$DataDir = Join-Path $InstallRoot "data"
New-Item -ItemType Directory -Force -Path $AppDir, $DataDir | Out-Null

Copy-Item -LiteralPath (Join-Path $FromLauncherDir "BnlCommunityFixes.exe") -Destination (Join-Path $AppDir "BnlCommunityFixes.exe") -Force

$Settings = @"
{
  "product": "BnlCommunityFixes",
  "channel": "stable",
  "manifestUrl": "$(([Uri](Resolve-Path $ManifestPath).Path).AbsoluteUri)"
}
"@
Set-Content -LiteralPath (Join-Path $DataDir "launcher-settings.json") -Value $Settings -Encoding UTF8

$env:BNL_INSTALL_ROOT = $InstallRoot
$LauncherPath = Join-Path $AppDir "BnlCommunityFixes.exe"
$Process = Start-Process -FilePath $LauncherPath -ArgumentList "--headless-smoke-test" -PassThru

if (-not $Process.WaitForExit(30000)) {
    try { $Process.Kill() } catch {}
    throw "Launcher process did not exit within 30 seconds."
}

Start-Sleep -Seconds 2

$InstalledLauncherHash = (Get-FileHash -LiteralPath $LauncherPath -Algorithm SHA256).Hash
$ExpectedLauncherHash = (Get-FileHash -LiteralPath $ToLauncherExe -Algorithm SHA256).Hash

if ($InstalledLauncherHash -ne $ExpectedLauncherHash) {
    throw "Smoke test failed: installed launcher hash does not match target version."
}

if (-not (Test-Path $LogLauncher)) {
    throw "Smoke test failed: launcher log was not created."
}

if (-not (Test-Path $LogUpdater)) {
    throw "Smoke test failed: updater log was not created."
}

Write-Output "Smoke test passed."
Write-Output "Install root: $InstallRoot"
Write-Output "Launcher log: $LogLauncher"
Write-Output "Update helper log: $LogUpdater"
