param(
    [Parameter(Mandatory = $true)] [string]$Baseline,
    [Parameter(Mandatory = $true)] [string]$Test
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReadPerformanceLog.ps1"

function Resolve-PerformanceFiles([string]$InputPath) {
    if (Test-Path -LiteralPath $InputPath -PathType Leaf) { return @(Get-Item -LiteralPath $InputPath) }
    if (Test-Path -LiteralPath $InputPath -PathType Container) {
        return @(Get-ChildItem -LiteralPath $InputPath -File | Where-Object Extension -In '.bnlperf','.csv')
    }
    return @(Get-ChildItem -Path $InputPath -File)
}

function Get-Percentile([double[]]$Values, [double]$Fraction) {
    $index = [math]::Min($Values.Count - 1, [math]::Max(0, [math]::Ceiling($Values.Count * $Fraction) - 1))
    return $Values[$index]
}

function Measure-Performance([string]$Name, [string]$InputPath) {
    $files = Resolve-PerformanceFiles $InputPath
    if ($files.Count -eq 0) { throw "No performance CSV files matched: $InputPath" }
    $rows = @($files | ForEach-Object { Import-BnlPerformanceLog -Path $_.FullName })
    if ($rows.Count -eq 0) { throw "The matched CSV files contain no recorded match frames: $InputPath" }

    [double[]]$frameTimes = @($rows | ForEach-Object { $_.FrameMs } | Sort-Object)
    $averageMs = ($frameTimes | Measure-Object -Average).Average
    $slowestOneCount = [math]::Max(1, [math]::Ceiling($frameTimes.Count * 0.01))
    $slowestPointOneCount = [math]::Max(1, [math]::Ceiling($frameTimes.Count * 0.001))
    $oneLowMs = ($frameTimes | Select-Object -Last $slowestOneCount | Measure-Object -Average).Average
    $pointOneLowMs = ($frameTimes | Select-Object -Last $slowestPointOneCount | Measure-Object -Average).Average

    [pscustomobject]@{
        Name = $Name; Files = $files.Count; Frames = $rows.Count
        Seconds = [math]::Round((($frameTimes | Measure-Object -Sum).Sum / 1000), 1)
        AvgFPS = [math]::Round(1000 / $averageMs, 2)
        OnePercentLowFPS = [math]::Round(1000 / $oneLowMs, 2)
        PointOnePercentLowFPS = [math]::Round(1000 / $pointOneLowMs, 2)
        P95ms = [math]::Round((Get-Percentile $frameTimes 0.95), 3)
        P99ms = [math]::Round((Get-Percentile $frameTimes 0.99), 3)
        WorstFrameMs = [math]::Round($frameTimes[-1], 3)
        Stalls50ms = @($frameTimes | Where-Object { $_ -gt 50 }).Count
        GC0 = ($rows | Measure-Object -Property GC0 -Maximum).Maximum
        GC1 = ($rows | Measure-Object -Property GC1 -Maximum).Maximum
        GC2 = ($rows | Measure-Object -Property GC2 -Maximum).Maximum
        ManagedMB = [math]::Round(($rows | Measure-Object -Property ManagedMB -Average).Average, 2)
    }
}

$baselineResult = Measure-Performance 'Baseline' $Baseline
$testResult = Measure-Performance 'Test' $Test
$baselineResult, $testResult | Format-Table -AutoSize
Write-Host ''
Write-Host ('Average FPS change: {0:+0.00;-0.00;0.00}%' -f ((($testResult.AvgFPS / $baselineResult.AvgFPS) - 1) * 100))
Write-Host ('1% low FPS change: {0:+0.00;-0.00;0.00}%' -f ((($testResult.OnePercentLowFPS / $baselineResult.OnePercentLowFPS) - 1) * 100))
Write-Host ('P95 frame-time change: {0:+0.00;-0.00;0.00}%' -f ((($testResult.P95ms / $baselineResult.P95ms) - 1) * 100))
Write-Host ('P99 frame-time change: {0:+0.00;-0.00;0.00}%' -f ((($testResult.P99ms / $baselineResult.P99ms) - 1) * 100))
