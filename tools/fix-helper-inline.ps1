$path = "assets/patching/Build-ExperimentalCrosshairAssembly.ps1"
$content = Get-Content -Raw -LiteralPath $path

$content = $content.Replace(@'
$HelperBaseTemplatePath = Join-Path $PSScriptRoot "HelperBase.template.cs"
$AutoCasualQueueTemplatePath = Join-Path $PSScriptRoot "AutoCasualQueueRuntime.template.cs"
$TeammateHpTemplatePath = Join-Path $PSScriptRoot "TeammateHpRuntime.template.cs"
$FontOverrideTemplatePath = Join-Path $PSScriptRoot "FontOverrideRuntime.template.cs"
'@, '')

$funcStart = $content.IndexOf('function Expand-TemplateFile {')
if ($funcStart -ge 0) {
    $funcEnd = $content.IndexOf('function Get-JsonConfig {', $funcStart)
    if ($funcEnd -lt 0) {
        throw "function Get-JsonConfig not found."
    }

    $content = $content.Remove($funcStart, $funcEnd - $funcStart)
}

$base = (Get-Content -Raw -LiteralPath 'assets/patching/HelperBase.template.cs').TrimEnd("`r", "`n")
$baseInline = '$HelperSource = @"' + "`r`n" + $base + "`r`n" + '"@'
$content = $content.Replace('$HelperSource = Expand-TemplateFile -Path $HelperBaseTemplatePath', $baseInline)

$aqCurrent = @'
if ($AutoCasualQueueConfig.enabled) {
    $HelperSource += "`r`n"
    $HelperSource += Expand-TemplateFile -Path $AutoCasualQueueTemplatePath
}
'@.Trim()
$aqTemplate = (Get-Content -Raw -LiteralPath 'assets/patching/AutoCasualQueueRuntime.template.cs').TrimEnd("`r", "`n")
$aqInline = 'if ($AutoCasualQueueConfig.enabled) {' + "`r`n" + '$HelperSource += @"' + "`r`n" + $aqTemplate + "`r`n" + '"@' + "`r`n" + '}'
$content = $content.Replace($aqCurrent, $aqInline)

$thCurrent = @'
if ($TeammateHpEnabled) {
    $HelperSource += "`r`n"
    $HelperSource += Expand-TemplateFile -Path $TeammateHpTemplatePath
}
'@.Trim()
$thTemplate = (Get-Content -Raw -LiteralPath 'assets/patching/TeammateHpRuntime.template.cs').TrimEnd("`r", "`n")
$thInline = 'if ($TeammateHpEnabled) {' + "`r`n" + '$HelperSource += @"' + "`r`n" + $thTemplate + "`r`n" + '"@' + "`r`n" + '}'
$content = $content.Replace($thCurrent, $thInline)

$foCurrent = @'
if ($FontOverrideEnabled) {
    $HelperSource += "`r`n"
    $HelperSource += Expand-TemplateFile -Path $FontOverrideTemplatePath
}
'@.Trim()
$foTemplate = (Get-Content -Raw -LiteralPath 'assets/patching/FontOverrideRuntime.template.cs').TrimEnd("`r", "`n")
$foInline = 'if ($FontOverrideEnabled) {' + "`r`n" + '$HelperSource += @"' + "`r`n" + $foTemplate + "`r`n" + '"@' + "`r`n" + '}'
$content = $content.Replace($foCurrent, $foInline)

Set-Content -LiteralPath $path -Value $content -Encoding UTF8
