param(
    [Parameter(Mandatory = $true)] [string]$InputPath,
    [string]$OutputPath
)

. "$PSScriptRoot\ReadPerformanceLog.ps1"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = [IO.Path]::ChangeExtension((Resolve-Path -LiteralPath $InputPath).Path, '.csv')
}

Import-BnlPerformanceLog -Path $InputPath |
    Select-Object UtcTicks,Label,ElapsedSeconds,FrameMs,ManagedMB,GC0,GC1,GC2 |
    Export-Csv -LiteralPath $OutputPath -NoTypeInformation -UseQuotes AsNeeded
Write-Host "Converted performance trace: $OutputPath"
