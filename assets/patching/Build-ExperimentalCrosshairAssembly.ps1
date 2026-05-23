param(
    [string]$GameRoot = "H:\Programas\Steam\steamapps\common\BlockNLoad"
)

$ErrorActionPreference = "Stop"

$ConfigPath = Join-Path $PSScriptRoot "experimental-font-config.json"
$ProjectileConfigPath = Join-Path $PSScriptRoot "experimental-projectile-config.json"
$AccuracyConfigPath = Join-Path $PSScriptRoot "experimental-accuracy-config.json"
$WeaponSwitchConfigPath = Join-Path $PSScriptRoot "experimental-weapon-switch-config.json"
$CrosshairConfigPath = Join-Path $PSScriptRoot "crosshair-config.json"
$TeamColorConfigPath = Join-Path $PSScriptRoot "experimental-team-color-config.json"
$LockOnConfigPath = Join-Path $PSScriptRoot "experimental-lockon-config.json"
$TrackingConfigPath = Join-Path $PSScriptRoot "experimental-tracking-projectiles-config.json"
$FovConfigPath = Join-Path $PSScriptRoot "fov-config.json"
$DamageHealingConfigPath = Join-Path $PSScriptRoot "damage-healing-indicator-config.json"
$HealAlertConfigPath = Join-Path $PSScriptRoot "heal-alert-indicator-config.json"
$BaseObjectiveBeamConfigPath = Join-Path $PSScriptRoot "experimental-base-objective-beam-config.json"
$EnemyShieldBuffBarConfigPath = Join-Path $PSScriptRoot "experimental-enemy-shield-buffbar-config.json"
$LocalBuildPreviewConfigPath = Join-Path $PSScriptRoot "experimental-local-build-preview-config.json"
$DebugMenuConfigPath = Join-Path $PSScriptRoot "experimental-debug-menu-config.json"
$MatchReplayRecorderConfigPath = Join-Path $PSScriptRoot "experimental-match-replay-recorder-config.json"
$AimHealthbarConfigPath = Join-Path $PSScriptRoot "aim-healthbar-config.json"
$DeathCamHealthbarConfigPath = Join-Path $PSScriptRoot "deathcam-healthbar-config.json"
$FriendlyLowHealthConfigPath = Join-Path $PSScriptRoot "friendly-low-health-config.json"
$TeammateHpConfigPath = Join-Path $PSScriptRoot "teammate-hp-config.json"
$AutoCrouchConfigPath = Join-Path $PSScriptRoot "experimental-auto-crouch-config.json"
$HideImpactVfxConfigPath = Join-Path $PSScriptRoot "experimental-hide-impact-vfx-config.json"
$UnitGuiScaleConfigPath = Join-Path $PSScriptRoot "unit-gui-scale-config.json"
$WsiConfigPath = Join-Path $PSScriptRoot "wsi-config.json"
$MapRenderConfigPath = Join-Path $PSScriptRoot "experimental-map-render-config.json"
$OutputPath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.dll"
$SavedCopyPath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.font-configured.dll"
$TempBasePath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.base.dll"
$HelperOutputPath = Join-Path $PSScriptRoot "BnlCommunityFixes.dll"
$LockOnHelperSourcePath = Join-Path $PSScriptRoot "LockOnRuntime.cs"
$TrackingHelperSourcePath = Join-Path $PSScriptRoot "TrackingProjectileRuntime.cs"
$RuntimeMenuSourcePath = Join-Path $PSScriptRoot "RuntimeMenu.cs"
$MatchReplayRecorderSourcePath = Join-Path $PSScriptRoot "MatchReplayRecorderRuntime.cs"
$ReplayPlayerSourcePath = Join-Path $PSScriptRoot "ReplayPlayerRuntime.cs"
$ManagedDir = Join-Path $GameRoot "Win64\BlockNLoad_Data\Managed"
$BackupPath = Join-Path $ManagedDir "Assembly-CSharp-backup.dll"
$CecilPath = Join-Path $PSScriptRoot "Mono.Cecil.dll"
$UnityEngineDll = Join-Path $ManagedDir "UnityEngine.dll"
$UnityEngineUiDll = Join-Path $ManagedDir "UnityEngine.UI.dll"

function Get-JsonConfig {
    param([string]$Path,[hashtable]$Default)
    if (-not (Test-Path $Path)) {
        return [PSCustomObject]$Default
    }
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        return [PSCustomObject]$Default
    }
}

$Config = Get-JsonConfig -Path $ConfigPath -Default @{
    enabled = $false
    selected_font = "__DEFAULT__"
    font_style = "Keep"
    size_multiplier = 1.0
    line_spacing_multiplier = 1.0
}
$ProjectileConfig = Get-JsonConfig -Path $ProjectileConfigPath -Default @{
    enabled = $false
    rocket_projectile_speed = 500.0
    unit_projectile_speed = 500.0
    rocket_projectile_lifetime_multiplier = 1.0
    unit_projectile_lifetime_multiplier = 1.0
    log_tool_branches = $false
    log_unit_card_id = $false
}
$AccuracyConfig = Get-JsonConfig -Path $AccuracyConfigPath -Default @{
    enabled = $false
    scatter_multiplier = 1.0
    shot_bloom_multiplier = 1.0
    base_angle_multiplier = 1.0
    move_mod_multiplier = 1.0
    jump_mod_multiplier = 1.0
    crouch_mod_multiplier = 1.0
}
$WeaponSwitchConfig = Get-JsonConfig -Path $WeaponSwitchConfigPath -Default @{
    enabled = $false
    switch_time_multiplier = 1.0
}
$CrosshairConfig = Get-JsonConfig -Path $CrosshairConfigPath -Default @{
    enabled = $false
    idle_color = "#FFFFFF"
    full_damage_color = "#FF0000"
    below_max_color = "#FF0000"
    brightness_multiplier = 1.0
    size_multiplier = 1.0
    spread_multiplier = 1.0
    alpha = 1.0
    force_shape = "__DEFAULT__"
    force_show_in_ads = $false
}
$TeamColorConfig = Get-JsonConfig -Path $TeamColorConfigPath -Default @{
    enabled = $false
    friendly_color = "#4AA3FF"
    enemy_color = "#FF5A5A"
}
$LockOnConfig = Get-JsonConfig -Path $LockOnConfigPath -Default @{
    enabled = $false
    toggle_key = "Mouse2"
    max_range = 60.0
    max_angle = 12.0
    turn_speed = 360.0
    require_los = $true
    predict_movement = $true
    live_debug_console = $false
    aim_at_head = $false
}
$TrackingConfig = Get-JsonConfig -Path $TrackingConfigPath -Default @{
    enabled = $false
    toggle_key = "F6"
    seek_range = 30.0
    max_angle_degrees = 25.0
    turn_rate_degrees = 360.0
    controlled_only = $false
    debug_log = $false
    live_debug_console = $false
}
$FovConfig = Get-JsonConfig -Path $FovConfigPath -Default @{
    enabled = $false
    fov = 120.0
    ads_sensitivity_multiplier = 1.0
}
[bool]$EnableFovFeature = $true
$DamageHealingConfig = Get-JsonConfig -Path $DamageHealingConfigPath -Default @{
    enabled = $false
    damage_number_color = "#FFFFFF"
    crit_damage_number_color = "#FFFFFF"
    heal_number_color = "#91ED78"
    damage_number_size_multiplier = 2.0
    heal_number_size_multiplier = 2.0
    alpha = 1.0
    show_friendly_healing = $false
    show_self_healing = $false
    combine_damage_until_hidden = $false
    combine_healing_until_hidden = $false
    minimum_heal = 0.5
    self_heal_number_size_multiplier = 0.7
    self_heal_x = 0
    self_heal_y = 0
}
$HealAlertConfig = Get-JsonConfig -Path $HealAlertConfigPath -Default @{
    enabled = $false
    damage_indicator_color = "__DEFAULT__"
    heal_indicator_color = "#00FF88"
    damage_indicator_size_multiplier = 1.0
    heal_indicator_size_multiplier = 1.0
    alpha = 1.0
    minimum_heal = 0.5
    show_direction_on_heal = $false
}
$BaseObjectiveBeamConfig = Get-JsonConfig -Path $BaseObjectiveBeamConfigPath -Default @{
    enabled = $false
    hide_beam = $false
}
$EnemyShieldBuffBarConfig = Get-JsonConfig -Path $EnemyShieldBuffBarConfigPath -Default @{
    enabled = $false
    shield_buff_bar_color = "#FFF04A"
    shield_clock_size_multiplier = 1.0
    shield_clock_offset_x = 0.0
    shield_clock_offset_y = 0.0
    shield_timer_display_mode = "circle"
}
$LocalBuildPreviewConfig = Get-JsonConfig -Path $LocalBuildPreviewConfigPath -Default @{
    enabled = $false
    prediction_timeout_seconds = 2.0
}
$DebugMenuConfig = Get-JsonConfig -Path $DebugMenuConfigPath -Default @{
    enabled = $false
    debug_menu_key = "F9"
    main_menu_key = "F10"
    lobby_menu_key = "F11"
    zone_menu_key = "F12"
}
$MatchReplayRecorderConfig = Get-JsonConfig -Path $MatchReplayRecorderConfigPath -Default @{
    enabled = $false
    capture_payload = $true
    max_payload_bytes = 262144
    record_custom_games = $true
    record_casual_games = $true
    record_ranked_games = $true
}
$AimHealthbarConfig = Get-JsonConfig -Path $AimHealthbarConfigPath -Default @{
    enabled = $true
}
$DeathCamHealthbarConfig = Get-JsonConfig -Path $DeathCamHealthbarConfigPath -Default @{
    enabled = $true
}
$AutoCasualQueueConfigPath = Join-Path $PSScriptRoot "experimental-auto-casual-queue-config.json"
$AutoCasualQueueConfig = Get-JsonConfig -Path $AutoCasualQueueConfigPath -Default @{
    enabled = $false
}
$FriendlyLowHealthConfig = Get-JsonConfig -Path $FriendlyLowHealthConfigPath -Default @{
    enabled = $true
    threshold = 0.3
    color = "#FF4444"
}
$TeammateHpConfig = Get-JsonConfig -Path $TeammateHpConfigPath -Default @{
    enabled = $false
}
$AutoCrouchConfig = Get-JsonConfig -Path $AutoCrouchConfigPath -Default @{
    enabled = $false
}
$HideImpactVfxConfig = Get-JsonConfig -Path $HideImpactVfxConfigPath -Default @{
    enabled = $false
    hide_impact_vfx = $false
    hide_lava_water_plane = $false
    hide_falling_blocks = $false
}
$UnitGuiScaleConfig = Get-JsonConfig -Path $UnitGuiScaleConfigPath -Default @{
    enabled = $false
    scale_multiplier = 1.0
}
$WsiConfig = Get-JsonConfig -Path $WsiConfigPath -Default @{
    scale_enabled = $false
    scale_multiplier = 1.0
}
$MapRenderConfig = Get-JsonConfig -Path $MapRenderConfigPath -Default @{
    enabled = $false
    preset = "Default"
}
[string]$MapRenderPreset = if (-not [string]::IsNullOrWhiteSpace([string]$MapRenderConfig.preset)) { [string]$MapRenderConfig.preset } else { "Default" }
$MapRenderPresetLiteral = ($MapRenderPreset -replace '\\', '\\\\') -replace '"', '\"'

$AnyEnabled = @(
    [bool]$Config.enabled,
    [bool]$ProjectileConfig.enabled,
    [bool]$AccuracyConfig.enabled,
    [bool]$WeaponSwitchConfig.enabled,
    [bool]$CrosshairConfig.enabled,
    [bool]$TeamColorConfig.enabled,
    [bool]$LockOnConfig.enabled,
    [bool]$TrackingConfig.enabled,
    $EnableFovFeature -and [bool]$FovConfig.enabled,
    [bool]$DamageHealingConfig.enabled,
    [bool]$HealAlertConfig.enabled,
    [bool]$BaseObjectiveBeamConfig.enabled,
    [bool]$EnemyShieldBuffBarConfig.enabled,
    [bool]$LocalBuildPreviewConfig.enabled,
    [bool]$DebugMenuConfig.enabled,
    [bool]$MatchReplayRecorderConfig.enabled,
    [bool]$AimHealthbarConfig.enabled,
    [bool]$DeathCamHealthbarConfig.enabled,
    [bool]$AutoCasualQueueConfig.enabled,
    [bool]$FriendlyLowHealthConfig.enabled,
    [bool]$TeammateHpConfig.enabled,
    [bool]$AutoCrouchConfig.enabled,
    [bool]$HideImpactVfxConfig.enabled,
    [bool]$UnitGuiScaleConfig.enabled,
    [bool]$WsiConfig.scale_enabled,
    [bool]$MapRenderConfig.enabled,
    $SkipIntroEnabled,
    $DisableMainMenuFrameCapEnabled
) -contains $true

if (-not $AnyEnabled) {
    if (Test-Path $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }
    Write-Output "All experimental features are disabled."
    exit 0
}

if (-not (Test-Path $BackupPath)) {
    throw "Backup Assembly-CSharp.dll not found: $BackupPath"
}
if (-not (Test-Path $CecilPath)) {
    throw "Bundled Mono.Cecil.dll not found: $CecilPath"
}

Add-Type -Path $CecilPath

$SelectedFont = [string]$Config.selected_font
if ([string]::IsNullOrWhiteSpace($SelectedFont)) {
    $SelectedFont = "__DEFAULT__"
}
$SelectedFontLiteral = $SelectedFont.Replace("\", "\\").Replace('"', '\"')
$SelectedStyle = [string]$Config.font_style
if ([string]::IsNullOrWhiteSpace($SelectedStyle)) {
    $SelectedStyle = "Keep"
}
$SelectedStyleLiteral = $SelectedStyle.Replace("\", "\\").Replace('"', '\"')
[double]$SizeMultiplier = if ($null -ne $Config.size_multiplier) { [double]$Config.size_multiplier } else { 1.0 }
[double]$LineSpacingMultiplier = if ($null -ne $Config.line_spacing_multiplier) { [double]$Config.line_spacing_multiplier } else { 1.0 }
$SizeMultiplierLiteral = ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$SizeMultiplier)) + "f"
$LineSpacingMultiplierLiteral = ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$LineSpacingMultiplier)) + "f"
[double]$AdsSensitivityMultiplier = if ($null -ne $FovConfig.ads_sensitivity_multiplier) { [double]$FovConfig.ads_sensitivity_multiplier } else { 1.0 }
$AdsSensitivityMultiplierLiteral = ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$AdsSensitivityMultiplier)) + "f"
[double]$DefaultWeaponModelFov = if ($null -ne $FovConfig.weapon_model_fov) { [double]$FovConfig.weapon_model_fov } else { 30.0 }
$DefaultWeaponModelFovLiteral = ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$DefaultWeaponModelFov)) + "f"
[double]$DefaultForcedFov = if ($null -ne $FovConfig.fov) { [double]$FovConfig.fov } else { 120.0 }
$DefaultForcedFovLiteral = ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$DefaultForcedFov)) + "f"

function Convert-HexToColorData {
    param(
        [string]$Hex,
        [double]$Alpha = 1.0
    )

    if ([string]::IsNullOrWhiteSpace($Hex)) {
        throw "Color value is empty."
    }

    $Normalized = $Hex.Trim()
    if ($Normalized.StartsWith("#")) {
        $Normalized = $Normalized.Substring(1)
    }
    if ($Normalized.Length -ne 6) {
        throw "Color '$Hex' must be in #RRGGBB format."
    }

    $R = [Convert]::ToInt32($Normalized.Substring(0, 2), 16)
    $G = [Convert]::ToInt32($Normalized.Substring(2, 2), 16)
    $B = [Convert]::ToInt32($Normalized.Substring(4, 2), 16)

    return [PSCustomObject]@{
        Hex = "#$Normalized".ToUpperInvariant()
        R = [single]($R / 255.0)
        G = [single]($G / 255.0)
        B = [single]($B / 255.0)
        A = [single]([Math]::Max(0.0, [Math]::Min(1.0, $Alpha)))
    }
}

function Format-FloatLiteral {
    param([double]$Value)

    return ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:R}", [single]$Value)) + "f"
}

function Format-BoolLiteral {
    param([bool]$Value)
    if ($Value) { return "true" }
    return "false"
}

function Test-DefaultColor {
    param([string]$Hex)
    return [string]::IsNullOrWhiteSpace($Hex) -or $Hex.Trim().ToUpperInvariant() -eq "__DEFAULT__"
}

$RuntimeFriendlyTeamColor = Convert-HexToColorData -Hex ([string]$TeamColorConfig.friendly_color) -Alpha 1.0
$RuntimeEnemyTeamColor = Convert-HexToColorData -Hex ([string]$TeamColorConfig.enemy_color) -Alpha 1.0

[double]$CrosshairAlpha = if ($null -ne $CrosshairConfig.alpha) { [double]$CrosshairConfig.alpha } else { 1.0 }
[double]$CrosshairBrightnessMultiplier = if ($null -ne $CrosshairConfig.brightness_multiplier) { [double]$CrosshairConfig.brightness_multiplier } else { 1.0 }
[double]$CrosshairSizeMultiplier = if ($null -ne $CrosshairConfig.size_multiplier) { [double]$CrosshairConfig.size_multiplier } else { 1.0 }
[double]$CrosshairSpreadMultiplier = if ($null -ne $CrosshairConfig.spread_multiplier) { [double]$CrosshairConfig.spread_multiplier } else { 1.0 }
[bool]$CrosshairForceShowInAds = if ($null -ne $CrosshairConfig.force_show_in_ads) { [bool]$CrosshairConfig.force_show_in_ads } else { $false }
[bool]$CrosshairHideEntirely = if ($null -ne $CrosshairConfig.hide_crosshair) { [bool]$CrosshairConfig.hide_crosshair } else { $false }
[string]$CrosshairForceShape = if (-not [string]::IsNullOrWhiteSpace([string]$CrosshairConfig.force_shape)) { [string]$CrosshairConfig.force_shape } else { "__DEFAULT__" }

$CrosshairIdleColor = Convert-HexToColorData -Hex $CrosshairConfig.idle_color -Alpha $CrosshairAlpha
$CrosshairFullDamageColor = Convert-HexToColorData -Hex $CrosshairConfig.full_damage_color -Alpha $CrosshairAlpha
$CrosshairBelowMaxColor = Convert-HexToColorData -Hex $CrosshairConfig.below_max_color -Alpha $CrosshairAlpha

foreach ($ColorData in @($CrosshairIdleColor, $CrosshairFullDamageColor, $CrosshairBelowMaxColor)) {
    $ColorData.R = [single]([Math]::Min(1.0, $ColorData.R * $CrosshairBrightnessMultiplier))
    $ColorData.G = [single]([Math]::Min(1.0, $ColorData.G * $CrosshairBrightnessMultiplier))
    $ColorData.B = [single]([Math]::Min(1.0, $ColorData.B * $CrosshairBrightnessMultiplier))
}

$CrosshairIdleR = Format-FloatLiteral $CrosshairIdleColor.R
$CrosshairIdleG = Format-FloatLiteral $CrosshairIdleColor.G
$CrosshairIdleB = Format-FloatLiteral $CrosshairIdleColor.B
$CrosshairIdleA = Format-FloatLiteral $CrosshairIdleColor.A
$CrosshairFullR = Format-FloatLiteral $CrosshairFullDamageColor.R
$CrosshairFullG = Format-FloatLiteral $CrosshairFullDamageColor.G
$CrosshairFullB = Format-FloatLiteral $CrosshairFullDamageColor.B
$CrosshairFullA = Format-FloatLiteral $CrosshairFullDamageColor.A
$CrosshairBelowR = Format-FloatLiteral $CrosshairBelowMaxColor.R
$CrosshairBelowG = Format-FloatLiteral $CrosshairBelowMaxColor.G
$CrosshairBelowB = Format-FloatLiteral $CrosshairBelowMaxColor.B
$CrosshairBelowA = Format-FloatLiteral $CrosshairBelowMaxColor.A
$CrosshairSizeMultiplierLiteral = Format-FloatLiteral $CrosshairSizeMultiplier
$CrosshairSpreadMultiplierLiteral = Format-FloatLiteral $CrosshairSpreadMultiplier
$CrosshairForceShapeLiteral = ($CrosshairForceShape -replace '\\', '\\\\') -replace '"', '\"'
$CrosshairForceShowInAdsLiteral = if ($CrosshairForceShowInAds) { "true" } else { "false" }
[double]$DamageHealingAlpha = if ($null -ne $DamageHealingConfig.alpha) { [double]$DamageHealingConfig.alpha } else { 1.0 }
[double]$DamageNumberSize = if ($null -ne $DamageHealingConfig.damage_number_size_multiplier) { [double]$DamageHealingConfig.damage_number_size_multiplier } else { 2.0 }
[double]$HealNumberSize = if ($null -ne $DamageHealingConfig.heal_number_size_multiplier) { [double]$DamageHealingConfig.heal_number_size_multiplier } else { 2.0 }
[double]$MinimumHeal = if ($null -ne $DamageHealingConfig.minimum_heal) { [double]$DamageHealingConfig.minimum_heal } else { 0.5 }
[bool]$ShowFriendlyHealing = if ($null -ne $DamageHealingConfig.show_friendly_healing) { [bool]$DamageHealingConfig.show_friendly_healing } else { $false }
[bool]$ShowSelfHealing = if ($null -ne $DamageHealingConfig.show_self_healing) { [bool]$DamageHealingConfig.show_self_healing } else { $false }
[bool]$CombineDamageUntilHidden = if ($null -ne $DamageHealingConfig.combine_damage_until_hidden) { [bool]$DamageHealingConfig.combine_damage_until_hidden } else { $false }
[bool]$CombineHealingUntilHidden = if ($null -ne $DamageHealingConfig.combine_healing_until_hidden) { [bool]$DamageHealingConfig.combine_healing_until_hidden } else { $false }
$UseDamageNumberColor = -not (Test-DefaultColor -Hex $DamageHealingConfig.damage_number_color)
$UseCritDamageNumberColor = -not (Test-DefaultColor -Hex $DamageHealingConfig.crit_damage_number_color)
$UseHealNumberColor = -not (Test-DefaultColor -Hex $DamageHealingConfig.heal_number_color)
$DamageNumberColor = Convert-HexToColorData -Hex $(if ($UseDamageNumberColor) { $DamageHealingConfig.damage_number_color } else { "#FFFFFF" }) -Alpha $DamageHealingAlpha
$CritDamageNumberColor = Convert-HexToColorData -Hex $(if ($UseCritDamageNumberColor) { $DamageHealingConfig.crit_damage_number_color } else { "#FFFFFF" }) -Alpha $DamageHealingAlpha
$HealNumberColor = Convert-HexToColorData -Hex $(if ($UseHealNumberColor) { $DamageHealingConfig.heal_number_color } else { "#91ED78" }) -Alpha $DamageHealingAlpha
$DamageColor = $DamageNumberColor
$CritDamageColor = $CritDamageNumberColor
$HealColor = $HealNumberColor
$UseDamageColor = $UseDamageNumberColor
$UseCritDamageColor = $UseCritDamageNumberColor
$UseHealColor = $UseHealNumberColor
$DamageSize = $DamageNumberSize
$HealSize = $HealNumberSize
[double]$SelfHealSize = if ($null -ne $DamageHealingConfig.self_heal_number_size_multiplier) { [double]$DamageHealingConfig.self_heal_number_size_multiplier } else { 0.7 }
[double]$SelfHealX = if ($null -ne $DamageHealingConfig.self_heal_x) { [double]$DamageHealingConfig.self_heal_x } else { 0 }
[double]$SelfHealY = if ($null -ne $DamageHealingConfig.self_heal_y) { [double]$DamageHealingConfig.self_heal_y } else { 0 }

[double]$HealAlertAlpha = if ($null -ne $HealAlertConfig.alpha) { [double]$HealAlertConfig.alpha } else { 1.0 }
[double]$HealAlertDamageSize = if ($null -ne $HealAlertConfig.damage_indicator_size_multiplier) { [double]$HealAlertConfig.damage_indicator_size_multiplier } else { 1.0 }
[double]$HealAlertHealSize = if ($null -ne $HealAlertConfig.heal_indicator_size_multiplier) { [double]$HealAlertConfig.heal_indicator_size_multiplier } else { 1.0 }
[double]$HealAlertMinimumHeal = if ($null -ne $HealAlertConfig.minimum_heal) { [double]$HealAlertConfig.minimum_heal } else { 0.5 }
[bool]$HealAlertShowDir = if ($null -ne $HealAlertConfig.show_direction_on_heal) { [bool]$HealAlertConfig.show_direction_on_heal } else { $false }
$HealAlertUseDamageColor = -not (Test-DefaultColor -Hex $HealAlertConfig.damage_indicator_color)
$HealAlertUseHealColor = -not (Test-DefaultColor -Hex $HealAlertConfig.heal_indicator_color)
$HealAlertDamageColor = Convert-HexToColorData -Hex $(if ($HealAlertUseDamageColor) { $HealAlertConfig.damage_indicator_color } else { "#FFFFFF" }) -Alpha $HealAlertAlpha
$HealAlertHealColor = Convert-HexToColorData -Hex $(if ($HealAlertUseHealColor) { $HealAlertConfig.heal_indicator_color } else { "#00FF88" }) -Alpha $HealAlertAlpha
[bool]$EnemyShieldBuffBarEnabled = if ($null -ne $EnemyShieldBuffBarConfig.enabled) { [bool]$EnemyShieldBuffBarConfig.enabled } else { $false }
$EnemyShieldBuffBarColor = Convert-HexToColorData -Hex $(if ([string]::IsNullOrWhiteSpace([string]$EnemyShieldBuffBarConfig.shield_buff_bar_color)) { "#FFF04A" } else { [string]$EnemyShieldBuffBarConfig.shield_buff_bar_color }) -Alpha 1.0
[double]$EnemyShieldClockSizeMultiplier = if ($null -ne $EnemyShieldBuffBarConfig.shield_clock_size_multiplier) { [double]$EnemyShieldBuffBarConfig.shield_clock_size_multiplier } else { 1.0 }
[double]$EnemyShieldClockOffsetX = if ($null -ne $EnemyShieldBuffBarConfig.shield_clock_offset_x) { [double]$EnemyShieldBuffBarConfig.shield_clock_offset_x } else { 0.0 }
[double]$EnemyShieldClockOffsetY = if ($null -ne $EnemyShieldBuffBarConfig.shield_clock_offset_y) { [double]$EnemyShieldBuffBarConfig.shield_clock_offset_y } else { 0.0 }
[string]$EnemyShieldTimerDisplayMode = if ($null -ne $EnemyShieldBuffBarConfig.shield_timer_display_mode -and -not [string]::IsNullOrWhiteSpace([string]$EnemyShieldBuffBarConfig.shield_timer_display_mode)) { [string]$EnemyShieldBuffBarConfig.shield_timer_display_mode } else { "circle" }
[bool]$FriendlyLowHealthEnabled = if ($null -ne $FriendlyLowHealthConfig.enabled) { [bool]$FriendlyLowHealthConfig.enabled } else { $true }
[double]$FriendlyLowHealthThreshold = if ($null -ne $FriendlyLowHealthConfig.threshold) { [double]$FriendlyLowHealthConfig.threshold } else { 0.3 }
$FriendlyLowHealthColor = Convert-HexToColorData -Hex $(if ([string]::IsNullOrWhiteSpace([string]$FriendlyLowHealthConfig.color)) { "#FF4444" } else { [string]$FriendlyLowHealthConfig.color }) -Alpha 1.0
[bool]$FriendlyLowHealthIndicatorEnabled = if ($null -ne $FriendlyLowHealthConfig.show_direction_indicator) { [bool]$FriendlyLowHealthConfig.show_direction_indicator } else { $true }
[double]$FriendlyLowHealthIndicatorSize = if ($null -ne $FriendlyLowHealthConfig.indicator_size) { [double]$FriendlyLowHealthConfig.indicator_size } else { 1.0 }
[double]$FriendlyLowHealthIndicatorAlpha = if ($null -ne $FriendlyLowHealthConfig.indicator_alpha) { [double]$FriendlyLowHealthConfig.indicator_alpha } else { 1.0 }
[bool]$TeammateHpEnabled = if ($null -ne $TeammateHpConfig.enabled) { [bool]$TeammateHpConfig.enabled } else { $false }
[bool]$AutoCrouchEnabled = if ($null -ne $AutoCrouchConfig.enabled) { [bool]$AutoCrouchConfig.enabled } else { $false }
[bool]$SkipIntroEnabled = if ($null -ne $DebugMenuConfig.skip_intro) { [bool]$DebugMenuConfig.skip_intro } else { $false }
[bool]$DisableMainMenuFrameCapEnabled = if ($null -ne $DebugMenuConfig.disable_main_menu_frame_cap) { [bool]$DebugMenuConfig.disable_main_menu_frame_cap } else { $false }
[string]$DebugMenuKeyName = if ($null -ne $DebugMenuConfig.debug_menu_key -and -not [string]::IsNullOrWhiteSpace([string]$DebugMenuConfig.debug_menu_key)) { [string]$DebugMenuConfig.debug_menu_key } else { "F9" }
[string]$DebugMainMenuKeyName = if ($null -ne $DebugMenuConfig.main_menu_key -and -not [string]::IsNullOrWhiteSpace([string]$DebugMenuConfig.main_menu_key)) { [string]$DebugMenuConfig.main_menu_key } else { "F10" }
[string]$DebugLobbyMenuKeyName = if ($null -ne $DebugMenuConfig.lobby_menu_key -and -not [string]::IsNullOrWhiteSpace([string]$DebugMenuConfig.lobby_menu_key)) { [string]$DebugMenuConfig.lobby_menu_key } else { "F11" }
[string]$DebugZoneMenuKeyName = if ($null -ne $DebugMenuConfig.zone_menu_key -and -not [string]::IsNullOrWhiteSpace([string]$DebugMenuConfig.zone_menu_key)) { [string]$DebugMenuConfig.zone_menu_key } else { "F12" }
$DebugMenuKeyLiteral = $DebugMenuKeyName.Replace("\", "\\").Replace('"', '\"')
$DebugMainMenuKeyLiteral = $DebugMainMenuKeyName.Replace("\", "\\").Replace('"', '\"')
$DebugLobbyMenuKeyLiteral = $DebugLobbyMenuKeyName.Replace("\", "\\").Replace('"', '\"')
$DebugZoneMenuKeyLiteral = $DebugZoneMenuKeyName.Replace("\", "\\").Replace('"', '\"')
[bool]$MatchReplayRecorderCapturePayload = if ($null -ne $MatchReplayRecorderConfig.capture_payload) { [bool]$MatchReplayRecorderConfig.capture_payload } else { $true }
[int]$MatchReplayRecorderMaxPayloadBytes = if ($null -ne $MatchReplayRecorderConfig.max_payload_bytes) { [int]$MatchReplayRecorderConfig.max_payload_bytes } else { 262144 }
[bool]$MatchReplayRecorderRecordCustomGames = if ($null -ne $MatchReplayRecorderConfig.record_custom_games) { [bool]$MatchReplayRecorderConfig.record_custom_games } else { $true }
[bool]$MatchReplayRecorderRecordCasualGames = if ($null -ne $MatchReplayRecorderConfig.record_casual_games) { [bool]$MatchReplayRecorderConfig.record_casual_games } else { $true }
[bool]$MatchReplayRecorderRecordRankedGames = if ($null -ne $MatchReplayRecorderConfig.record_ranked_games) { [bool]$MatchReplayRecorderConfig.record_ranked_games } else { $true }
if ($MatchReplayRecorderMaxPayloadBytes -lt 0) { $MatchReplayRecorderMaxPayloadBytes = 0 }
if ($MatchReplayRecorderMaxPayloadBytes -gt 1048576) { $MatchReplayRecorderMaxPayloadBytes = 1048576 }

$HelperSource = @"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Protocol;
using UnityEngine;
using UnityEngine.UI;

namespace BnlCommunityFixes
{
    public sealed class ShieldBuffBarController : MonoBehaviour
    {
        private static readonly bool FeatureEnabled = $(Format-BoolLiteral $EnemyShieldBuffBarEnabled);
        private const string TimedShieldEffectId = "effect_status_shield_block";
        private const float ShieldClockSizeMultiplier = $(Format-FloatLiteral $EnemyShieldClockSizeMultiplier);
        private const float ShieldClockOffsetX = $(Format-FloatLiteral $EnemyShieldClockOffsetX);
        private const float ShieldClockOffsetY = $(Format-FloatLiteral $EnemyShieldClockOffsetY);
        private const string ShieldTimerDisplayMode = "$($EnemyShieldTimerDisplayMode.Replace('\', '\\').Replace('"', '\"'))";
        private static readonly Color ShieldBarColor = new Color($(Format-FloatLiteral $EnemyShieldBuffBarColor.R), $(Format-FloatLiteral $EnemyShieldBuffBarColor.G), $(Format-FloatLiteral $EnemyShieldBuffBarColor.B), $(Format-FloatLiteral $EnemyShieldBuffBarColor.A));
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Sprite cachedClockSprite;

        private GuiHealthbar healthbar;
        private Image bar;
        private RectTransform barRect;
        private Image clock;
        private RectTransform clockRect;
        private Text timerText;
        private RectTransform timerTextRect;
        private float observedShieldMax;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
            EnsureVisuals();
        }

        private void LateUpdate()
        {
            if (!FeatureEnabled || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            EnsureVisuals();
            if (bar == null || clock == null || timerText == null)
            {
                return;
            }

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (unit == null || zoneData == null)
            {
                bar.enabled = false;
                clock.enabled = false;
                timerText.enabled = false;
                return;
            }

            bool isEnemy = unit.Team != TeamType.Neutral && unit.Team != zoneData.MyTeam;
            ConstEffectInfo strongestShieldEffect;
            float strongestShieldValue;
            bool hasStrongestShieldEffect = TryGetStrongestShieldEffect(unit, out strongestShieldEffect, out strongestShieldValue);
            float shieldValue = hasStrongestShieldEffect ? Mathf.Max(strongestShieldValue, 0f) : Mathf.Max(unit.Shield, 0f);
            float configuredShieldMax = 0f;
            if (unit.UnitCard != null && unit.UnitCard.Health != null && unit.UnitCard.Health.Health != null)
            {
                configuredShieldMax = Mathf.Max(unit.UnitCard.Health.Health.Shield, 0f);
            }

            if (shieldValue > observedShieldMax)
            {
                observedShieldMax = shieldValue;
            }

            float shieldMax = Mathf.Max(configuredShieldMax, observedShieldMax);
            float shieldFill = ResolveShieldFill(shieldValue, shieldMax);
            bool visible = isEnemy && shieldFill > Mathf.Epsilon && healthbar.Content != null && healthbar.Content.alpha > 0.01f;
            bar.enabled = visible;
            clock.enabled = false;
            timerText.enabled = false;
            if (!visible)
            {
                return;
            }

            PositionVisuals();
            bar.color = ShieldBarColor;
            bar.fillAmount = shieldFill;

            float timerFill;
            bool hasTimer = TryGetShieldTimerFraction(unit, strongestShieldEffect, out timerFill);
            if (hasTimer)
            {
                if (UseNumericTimer())
                {
                    timerText.color = ShieldBarColor;
                    timerText.text = GetRemainingTimeText(unit, strongestShieldEffect);
                    timerText.enabled = !string.IsNullOrEmpty(timerText.text);
                }
                else
                {
                    clock.color = ShieldBarColor;
                    clock.fillAmount = timerFill;
                    clock.enabled = true;
                }
            }
        }

        private void EnsureVisuals()
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            Image source = healthbar.HealthBar;

            if (bar == null)
            {
                GameObject barGo = new GameObject("ExperimentalShieldBuffBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barGo.transform.SetParent(healthbar.HealthBar.transform, false);
                bar = barGo.GetComponent<Image>();
                barRect = barGo.GetComponent<RectTransform>();
                bar.enabled = false;
                bar.sprite = source.sprite;
                bar.type = source.type;
                bar.fillMethod = source.fillMethod;
                bar.fillOrigin = source.fillOrigin;
                bar.fillClockwise = source.fillClockwise;
                bar.preserveAspect = source.preserveAspect;
                bar.material = source.material;
            }

            if (clock == null)
            {
                GameObject clockGo = new GameObject("ExperimentalShieldBuffClock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                clockGo.transform.SetParent(healthbar.HealthBar.transform, false);
                clock = clockGo.GetComponent<Image>();
                clockRect = clockGo.GetComponent<RectTransform>();
                clock.enabled = false;
                clock.sprite = ResolveClockSprite();
                if (clock.sprite == null)
                {
                    clock.sprite = source.sprite;
                }
                clock.type = Image.Type.Filled;
                clock.fillMethod = Image.FillMethod.Radial360;
                clock.fillOrigin = 2;
                clock.fillClockwise = false;
                clock.preserveAspect = true;
                clock.material = source.material;
            }

            if (timerText == null)
            {
                GameObject textGo = new GameObject("ExperimentalShieldBuffTimerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGo.transform.SetParent(healthbar.HealthBar.transform, false);
                timerText = textGo.GetComponent<Text>();
                timerTextRect = textGo.GetComponent<RectTransform>();
                timerText.enabled = false;
                timerText.alignment = TextAnchor.MiddleRight;
                timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                timerText.verticalOverflow = VerticalWrapMode.Overflow;
                timerText.font = healthbar.Title != null ? healthbar.Title.font : null;
                timerText.fontStyle = healthbar.Title != null ? healthbar.Title.fontStyle : FontStyle.Normal;
                timerText.material = healthbar.Title != null ? healthbar.Title.material : null;
            }
        }

        private static Sprite ResolveClockSprite()
        {
            if (cachedClockSprite != null)
            {
                return cachedClockSprite;
            }

            UnityEngine.Object[] guiBuffs = Resources.FindObjectsOfTypeAll(typeof(GuiBuff));
            for (int i = 0; i < guiBuffs.Length; i++)
            {
                GuiBuff guiBuff = guiBuffs[i] as GuiBuff;
                if (guiBuff != null && guiBuff.RoundCooldown != null)
                {
                    cachedClockSprite = guiBuff.RoundCooldown;
                    return cachedClockSprite;
                }
            }

            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            List<string> preferredNames = new List<string>(new string[] { "roundcooldown", "cooldown_round", "pie", "radial", "circle" });
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                {
                    continue;
                }

                string lower = sprite.name.ToLowerInvariant();
                for (int j = 0; j < preferredNames.Count; j++)
                {
                    if (lower.Contains(preferredNames[j]))
                    {
                        cachedClockSprite = sprite;
                        return cachedClockSprite;
                    }
                }
            }

            return cachedClockSprite;
        }

        private void PositionVisuals()
        {
            if (barRect == null || clockRect == null || timerTextRect == null || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            RectTransform src = healthbar.HealthBar.rectTransform;
            float sourceHeight = Mathf.Abs(src.rect.height);
            if (sourceHeight <= Mathf.Epsilon)
            {
                sourceHeight = Mathf.Abs(src.sizeDelta.y);
            }
            if (sourceHeight <= Mathf.Epsilon)
            {
                sourceHeight = 8f;
            }

            float shieldHeight = Mathf.Max(2f, sourceHeight * 0.35f);
            float yOffset = 2f;

            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, yOffset);
            barRect.sizeDelta = new Vector2(0f, shieldHeight);
            barRect.localScale = src.localScale;
            barRect.localRotation = src.localRotation;

            float clockSize = Mathf.Max(shieldHeight + 4f, sourceHeight * 0.8f) * Mathf.Max(0.25f, ShieldClockSizeMultiplier);
            float baseHealthBarCenterY = -sourceHeight * 0.5f;
            clockRect.anchorMin = new Vector2(0f, 1f);
            clockRect.anchorMax = new Vector2(0f, 1f);
            clockRect.pivot = new Vector2(1f, 0.5f);
            clockRect.anchoredPosition = new Vector2(-4f + ShieldClockOffsetX, baseHealthBarCenterY + ShieldClockOffsetY);
            clockRect.sizeDelta = new Vector2(clockSize, clockSize);
            clockRect.localScale = src.localScale;
            clockRect.localRotation = src.localRotation;

            timerText.fontSize = Mathf.Max(10, healthbar.Title != null ? Mathf.RoundToInt(healthbar.Title.fontSize * Mathf.Max(0.5f, ShieldClockSizeMultiplier)) : Mathf.RoundToInt(12f * Mathf.Max(0.5f, ShieldClockSizeMultiplier)));
            timerTextRect.anchorMin = new Vector2(0f, 1f);
            timerTextRect.anchorMax = new Vector2(0f, 1f);
            timerTextRect.pivot = new Vector2(1f, 0.5f);
            timerTextRect.anchoredPosition = new Vector2(-4f + ShieldClockOffsetX, baseHealthBarCenterY + ShieldClockOffsetY);
            timerTextRect.sizeDelta = new Vector2(48f * Mathf.Max(0.75f, ShieldClockSizeMultiplier), Mathf.Max(shieldHeight + 6f, sourceHeight));
            timerTextRect.localScale = src.localScale;
            timerTextRect.localRotation = src.localRotation;
        }

        private float ResolveShieldFill(float shieldValue, float shieldMax)
        {
            if (shieldValue <= 1.0001f)
            {
                return Mathf.Clamp01(shieldValue);
            }

            if (shieldMax > Mathf.Epsilon)
            {
                return Mathf.Clamp01(shieldValue / shieldMax);
            }

            return 0f;
        }

        private static bool TryGetShieldTimerFraction(Unit unit, ConstEffectInfo strongestShieldEffect, out float fillAmount)
        {
            fillAmount = 1f;
            ConstEffectInfo effectInfo = strongestShieldEffect;
            if ((effectInfo == null || !effectInfo.HasDuration) && !TryGetTimedShieldEffect(unit, out effectInfo))
            {
                return false;
            }

            if (effectInfo == null || !effectInfo.HasDuration || effectInfo.Card == null || effectInfo.Card.Duration == null || effectInfo.Card.Duration.Value <= Mathf.Epsilon || effectInfo.TimestampEnd == null || Singleton<IServerTime>.Instance == null)
            {
                return false;
            }

            float remaining = Mathf.Max(0f, Singleton<IServerTime>.Instance.TimeTill((long)effectInfo.TimestampEnd.Value));
            fillAmount = Mathf.Clamp01(remaining / effectInfo.Card.Duration.Value);
            return true;
        }

        private static string GetRemainingTimeText(Unit unit, ConstEffectInfo strongestShieldEffect)
        {
            ConstEffectInfo effectInfo = strongestShieldEffect;
            if ((effectInfo == null || !effectInfo.HasDuration) && !TryGetTimedShieldEffect(unit, out effectInfo))
            {
                return string.Empty;
            }

            return FormatRemainingTime(effectInfo);
        }

        private static string FormatRemainingTime(ConstEffectInfo effectInfo)
        {
            if (effectInfo == null || effectInfo.TimestampEnd == null || Singleton<IServerTime>.Instance == null)
            {
                return string.Empty;
            }

            float remaining = Mathf.Max(0f, Singleton<IServerTime>.Instance.TimeTill((long)effectInfo.TimestampEnd.Value));
            if (remaining <= Mathf.Epsilon)
            {
                return string.Empty;
            }

            return remaining.ToString("0.0") + "s";
        }

        private static bool UseNumericTimer()
        {
            return string.Equals(ShieldTimerDisplayMode, "text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ShieldTimerDisplayMode, "number", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ShieldTimerDisplayMode, "numeric", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetTimedShieldEffect(Unit unit, out ConstEffectInfo match)
        {
            match = null;
            float strongestValue;
            if (TryGetStrongestShieldEffect(unit, out match, out strongestValue) && match != null && match.HasDuration)
            {
                return true;
            }

            match = null;
            if (unit == null || unit.ActualConstEffects == null)
            {
                return false;
            }

            for (int i = 0; i < unit.ActualConstEffects.Count; i++)
            {
                ConstEffectInfo effectInfo = unit.ActualConstEffects[i];
                if (effectInfo == null || effectInfo.Card == null)
                {
                    continue;
                }

                CardEffect card = effectInfo.Card;
                ConstEffectBuff buffEffect = card.Effect as ConstEffectBuff;
                bool grantsShield = buffEffect != null && buffEffect.Buffs != null && buffEffect.Buffs.ContainsKey(BuffType.Shield);
                if (!grantsShield || !effectInfo.HasDuration)
                {
                    continue;
                }

                if (string.Equals(card.Id, TimedShieldEffectId, StringComparison.Ordinal))
                {
                    match = effectInfo;
                    return true;
                }

                if (match == null)
                {
                    match = effectInfo;
                }
            }

            return match != null;
        }

        private static bool TryGetStrongestShieldEffect(Unit unit, out ConstEffectInfo match, out float shieldValue)
        {
            match = null;
            shieldValue = 0f;
            if (unit == null || unit.ActualConstEffects == null)
            {
                return false;
            }

            for (int i = 0; i < unit.ActualConstEffects.Count; i++)
            {
                ConstEffectInfo effectInfo = unit.ActualConstEffects[i];
                if (effectInfo == null || effectInfo.Card == null)
                {
                    continue;
                }

                CardEffect card = effectInfo.Card;
                ConstEffectBuff buffEffect = card.Effect as ConstEffectBuff;
                if (buffEffect == null || buffEffect.Buffs == null || !buffEffect.Buffs.ContainsKey(BuffType.Shield))
                {
                    continue;
                }

                float candidateValue = Mathf.Max(buffEffect.Buffs[BuffType.Shield], 0f);
                bool takeCandidate = false;
                if (match == null || candidateValue > shieldValue + Mathf.Epsilon)
                {
                    takeCandidate = true;
                }
                else if (Mathf.Abs(candidateValue - shieldValue) <= Mathf.Epsilon)
                {
                    bool candidatePreferred = string.Equals(card.Id, TimedShieldEffectId, StringComparison.Ordinal);
                    bool currentPreferred = match.Card != null && string.Equals(match.Card.Id, TimedShieldEffectId, StringComparison.Ordinal);
                    if (candidatePreferred && !currentPreferred)
                    {
                        takeCandidate = true;
                    }
                    else if (effectInfo.HasDuration && !match.HasDuration)
                    {
                        takeCandidate = true;
                    }
                }

                if (takeCandidate)
                {
                    match = effectInfo;
                    shieldValue = candidateValue;
                }
            }

            return match != null;
        }
    }

    public static class ShieldBuffBarRuntime
    {
        public static void AttachShieldBuffBar(GuiHealthbar healthbar)
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            ShieldBuffBarController controller = healthbar.gameObject.GetComponent<ShieldBuffBarController>();
            if (controller == null)
            {
                controller = healthbar.gameObject.AddComponent<ShieldBuffBarController>();
            }
            controller.Init(healthbar);
        }
    }

    public sealed class FriendlyLowHealthController : MonoBehaviour
    {
        internal static readonly bool FeatureEnabled = $(Format-BoolLiteral $FriendlyLowHealthEnabled);
        internal static readonly float Threshold = $(Format-FloatLiteral $FriendlyLowHealthThreshold);
        internal static readonly bool IndicatorEnabled = $(Format-BoolLiteral ($FriendlyLowHealthEnabled -and $FriendlyLowHealthIndicatorEnabled));
        private static readonly Color AlertColor = new Color($(Format-FloatLiteral $FriendlyLowHealthColor.R), $(Format-FloatLiteral $FriendlyLowHealthColor.G), $(Format-FloatLiteral $FriendlyLowHealthColor.B), 1f);
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FollowFieldCtrl = typeof(GuiHealthbar).GetField("follow", BindingFlags.Instance | BindingFlags.NonPublic);

        private GuiHealthbar healthbar;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
        }

        private void OnDestroy()
        {
            if (IndicatorEnabled)
            {
                Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
                if (unit != null)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
            }
        }

        private void LateUpdate()
        {
            if (!FeatureEnabled || healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.Health <= 0f || unit.IsDeath)
            {
                if (IndicatorEnabled && unit != null)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
                return;
            }

            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (zoneData == null)
            {
                return;
            }

            if (unit.Team == TeamType.Neutral || unit.Team != zoneData.MyTeam)
            {
                if (IndicatorEnabled)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
                return;
            }

            float maxHp = unit.MaxHealth;
            bool isLow = maxHp > 0f && (unit.Health / maxHp) <= Threshold;

            if (isLow)
            {
                // LateUpdate runs after Update() which sets the team color each frame.
                // We overwrite it here every frame so the alert color always wins while low.
                healthbar.HealthBar.color = AlertColor;
                if (healthbar.Title != null)
                {
                    healthbar.Title.color = AlertColor;
                }

                if (IndicatorEnabled)
                {
                    GuiFollow follow = ReferenceEquals(FollowFieldCtrl, null) ? null : FollowFieldCtrl.GetValue(healthbar) as GuiFollow;
                    bool isOffScreen = follow == null || !follow.IsInFrontOfCamera;
                    FriendlyLowHealthIndicatorService.UpdateIndicator(unit, isOffScreen, AlertColor);
                }
            }
            else
            {
                if (IndicatorEnabled)
                    FriendlyLowHealthIndicatorService.RemoveIndicator(unit);
            }
        }
    }

    public static class FriendlyLowHealthIndicatorService
    {
        private const float IndicatorSize  = $(Format-FloatLiteral $FriendlyLowHealthIndicatorSize);
        private const float IndicatorAlpha = $(Format-FloatLiteral $FriendlyLowHealthIndicatorAlpha);

        private static readonly Dictionary<Unit, GuiWorldSpaceIndicator> indicators = new Dictionary<Unit, GuiWorldSpaceIndicator>();

        public static void UpdateIndicator(Unit unit, bool isOffScreen, Color color)
        {
            if (!isOffScreen)
            {
                RemoveIndicator(unit);
                return;
            }

            GuiWorldSpaceIndicatorFactory factory = Singleton<GuiWorldSpaceIndicatorFactory>.Instance;
            if (factory == null) return;

            GuiWorldSpaceIndicator existing;
            if (indicators.TryGetValue(unit, out existing))
            {
                if (existing == null)
                    indicators.Remove(unit);
                return;
            }

            GuiWorldSpaceIndicator indicator = factory.AddArrow(unit);
            if (indicator == null) return;

            indicator.SetColor(color);
            indicator.IconMinSize = IndicatorSize;
            indicator.IconMaxSize = IndicatorSize;

            // Override the fade-in tween's target alpha so it settles at our configured value.
            UiTweenAlpha tween = indicator.GetComponent<UiTweenAlpha>();
            if (tween != null)
                tween.to = IndicatorAlpha;

            indicators[unit] = indicator;
        }

        public static void RemoveIndicator(Unit unit)
        {
            GuiWorldSpaceIndicator existing;
            if (indicators.TryGetValue(unit, out existing))
            {
                indicators.Remove(unit);
                if (existing != null)
                    existing.Kill();
            }
        }
    }

    public static class FriendlyLowHealthRuntime
    {
        private static readonly FieldInfo FollowField = typeof(GuiHealthbar).GetField("follow", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UnitFieldStatic = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void AttachFriendlyLowHealth(GuiHealthbar healthbar)
        {
            if (healthbar == null || healthbar.HealthBar == null)
            {
                return;
            }

            FriendlyLowHealthController controller = healthbar.gameObject.GetComponent<FriendlyLowHealthController>();
            if (controller == null)
            {
                controller = healthbar.gameObject.AddComponent<FriendlyLowHealthController>();
            }
            controller.Init(healthbar);
        }

        public static bool IsFriendlyLowHealth(GuiHealthbar healthbar)
        {
            if (!FriendlyLowHealthController.FeatureEnabled || healthbar == null)
            {
                return false;
            }

            Unit unit = ReferenceEquals(UnitFieldStatic, null) ? null : UnitFieldStatic.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.IsDeath)
            {
                return false;
            }

            ZoneData zoneData = Singleton<ZoneData>.Instance;
            if (zoneData == null)
            {
                return false;
            }

            if (unit.Team == TeamType.Neutral || unit.Team != zoneData.MyTeam)
            {
                return false;
            }

            GuiFollow follow = ReferenceEquals(FollowField, null) ? null : FollowField.GetValue(healthbar) as GuiFollow;
            if (follow == null || !follow.IsInFrontOfCamera)
            {
                return false;
            }

            // During deathcam (CameraDeath singleton is active) show bar for all in-camera friendlies.
            CameraDeath deathCamInst = Singleton<CameraDeath>.Instance;
            if (deathCamInst != null && deathCamInst.Target != null)
            {
                return true;
            }

            // Alive: only show when below threshold.
            float maxHp = unit.MaxHealth;
            if (maxHp <= 0f)
            {
                return false;
            }

            return (unit.Health / maxHp) <= FriendlyLowHealthController.Threshold;
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class FontRuntime
    {
        private const string SelectedFontName = "$SelectedFontLiteral";
        private const string SelectedStyle = "$SelectedStyleLiteral";
        private const float SizeMultiplier = $SizeMultiplierLiteral;
        private const float LineSpacingMultiplier = $LineSpacingMultiplierLiteral;
        private static bool initialized;
        private static Font cachedFont;
        private static string cachedFontName;
        private static readonly HashSet<string> seenFontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, int> originalSizes = new Dictionary<int, int>();
        private static readonly Dictionary<int, float> originalSpacings = new Dictionary<int, float>();
        private static readonly Dictionary<int, FontStyle> originalStyles = new Dictionary<int, FontStyle>();
        private static string cachePath;

        public static void ApplyAllCanvases()
        {
            EnsureCacheInitialized();
            try
            {
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                HashSet<int> roots = new HashSet<int>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas == null)
                    {
                        continue;
                    }

                    GameObject root = canvas.transform.root.gameObject;
                    if (root != null && roots.Add(root.GetInstanceID()))
                    {
                        ApplyToHierarchy(root);
                    }
                }
            }
            catch
            {
            }
        }

        public static void ApplyToHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                ApplyToText(texts[i]);
            }
        }

        public static void ApplyToText(Text text)
        {
            if (text == null)
            {
                return;
            }
            EnsureCacheInitialized();
            RecordFont(text.font);
            CacheOriginals(text);

            if (!string.IsNullOrEmpty(SelectedFontName) && SelectedFontName != "__DEFAULT__")
            {
                Font target = ResolveFont();
                if (target != null && text.font != target)
                {
                    text.font = target;
                }
            }

            ApplyStyle(text);
        }

        private static void CacheOriginals(Text text)
        {
            int id = text.GetInstanceID();
            if (!originalSizes.ContainsKey(id))
            {
                originalSizes[id] = text.fontSize;
            }
            if (!originalSpacings.ContainsKey(id))
            {
                originalSpacings[id] = text.lineSpacing;
            }
            if (!originalStyles.ContainsKey(id))
            {
                originalStyles[id] = text.fontStyle;
            }
        }

        private static void ApplyStyle(Text text)
        {
            int id = text.GetInstanceID();
            int baseSize;
            float baseSpacing;
            FontStyle baseStyle;

            if (!originalSizes.TryGetValue(id, out baseSize))
            {
                baseSize = text.fontSize;
            }
            if (!originalSpacings.TryGetValue(id, out baseSpacing))
            {
                baseSpacing = text.lineSpacing;
            }
            if (!originalStyles.TryGetValue(id, out baseStyle))
            {
                baseStyle = text.fontStyle;
            }

            int targetSize = Math.Max(1, Mathf.RoundToInt((float)baseSize * SizeMultiplier));
            float targetSpacing = baseSpacing * LineSpacingMultiplier;

            if (text.fontSize != targetSize)
            {
                text.fontSize = targetSize;
            }
            if (Math.Abs(text.lineSpacing - targetSpacing) > 0.001f)
            {
                text.lineSpacing = targetSpacing;
            }

            FontStyle targetStyle = baseStyle;
            if (string.Equals(SelectedStyle, "Normal", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Normal;
            }
            else if (string.Equals(SelectedStyle, "Bold", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Bold;
            }
            else if (string.Equals(SelectedStyle, "Italic", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.Italic;
            }
            else if (string.Equals(SelectedStyle, "BoldAndItalic", StringComparison.OrdinalIgnoreCase))
            {
                targetStyle = FontStyle.BoldAndItalic;
            }

            if (text.fontStyle != targetStyle)
            {
                text.fontStyle = targetStyle;
            }
        }

        private static void EnsureCacheInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string launcherDir = Path.Combine(root, "launcher");
                Directory.CreateDirectory(launcherDir);
                cachePath = Path.Combine(launcherDir, "available-fonts.txt");
                if (File.Exists(cachePath))
                {
                    string[] existing = File.ReadAllLines(cachePath);
                    for (int i = 0; i < existing.Length; i++)
                    {
                        string line = existing[i];
                        if (!string.IsNullOrEmpty(line))
                        {
                            seenFontNames.Add(line);
                        }
                    }
                }
            }
            catch
            {
            }

            if (!seenFontNames.Contains("__DEFAULT__"))
            {
                seenFontNames.Add("__DEFAULT__");
            }
            FlushFontCache();
        }

        private static void RecordFont(Font font)
        {
            if (font == null || string.IsNullOrEmpty(font.name))
            {
                return;
            }

            if (seenFontNames.Add(font.name))
            {
                FlushFontCache();
            }
        }

        private static void FlushFontCache()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                List<string> ordered = new List<string>(seenFontNames);
                ordered.Sort(StringComparer.OrdinalIgnoreCase);
                if (ordered.Remove("__DEFAULT__"))
                {
                    builder.AppendLine("__DEFAULT__");
                }
                for (int i = 0; i < ordered.Count; i++)
                {
                    builder.AppendLine(ordered[i]);
                }
                if (!string.IsNullOrEmpty(cachePath))
                {
                    File.WriteAllText(cachePath, builder.ToString());
                }
            }
            catch
            {
            }
        }

        private static Font ResolveFont()
        {
            if (cachedFont != null && cachedFontName == SelectedFontName)
            {
                return cachedFont;
            }

            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                Font font = fonts[i];
                if (font != null && string.Equals(font.name, SelectedFontName, StringComparison.OrdinalIgnoreCase))
                {
                    cachedFont = font;
                    cachedFontName = SelectedFontName;
                    return cachedFont;
                }
            }

            if (SelectedFontName.StartsWith("OS: ", StringComparison.OrdinalIgnoreCase))
            {
                string osFontName = SelectedFontName.Substring(4).Trim();
                if (!string.IsNullOrEmpty(osFontName))
                {
                    try
                    {
                        cachedFont = Font.CreateDynamicFontFromOSFont(osFontName, 16);
                        cachedFontName = SelectedFontName;
                        return cachedFont;
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class CrosshairRuntime
    {
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartScales = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Vector2> OriginalCrosshairPartSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartPositions = new Dictionary<int, Vector3>();

        static CrosshairRuntime()
        {
            RuntimeFeatureState.ConfigureCrosshair(
                $(Format-BoolLiteral $CrosshairConfig.enabled),
                new Color($CrosshairIdleR, $CrosshairIdleG, $CrosshairIdleB, 1f),
                new Color($CrosshairFullR, $CrosshairFullG, $CrosshairFullB, 1f),
                new Color($CrosshairBelowR, $CrosshairBelowG, $CrosshairBelowB, 1f),
                $CrosshairSizeMultiplierLiteral,
                $CrosshairSpreadMultiplierLiteral,
                $CrosshairForceShowInAdsLiteral,
                $(Format-BoolLiteral $CrosshairHideEntirely),
                "$CrosshairForceShapeLiteral");
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ApplyHardHide(GuiCrosshairController controller)
        {
            if (!RuntimeFeatureState.CrosshairHideEntirely || controller == null)
            {
                return false;
            }

            if (controller.Content != null)
            {
                controller.Content.SetActive(false);
            }

            if (controller.NotUse != null)
            {
                controller.NotUse.SetActive(false);
            }

            return true;
        }

        public static void ApplyVisibility(GuiCrosshairController controller)
        {
            if (!RuntimeFeatureState.CrosshairForceShowInAds || controller == null || controller.Content == null || controller.Content.activeSelf)
            {
                return;
            }

            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null)
            {
                return;
            }

            Unit player = registry.GetPlayer();
            if (player == null || player.IsDeath || player.CurrentGear == null)
            {
                return;
            }

            if (player.IsInAimingState() && !player.IsReloading && !player.IsSwitchingGear)
            {
                controller.Content.SetActive(true);
            }
        }

        public static void ApplyController(GuiCrosshairController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.NoTarget = RuntimeFeatureState.GetCrosshairIdleColor();
            controller.FullDamage = RuntimeFeatureState.GetCrosshairFullDamageColor();
            controller.BelowMaxDamage = RuntimeFeatureState.GetCrosshairBelowMaxColor();
        }

        public static void ApplyBlank(GuiCrosshairBlank blank)
        {
            if (blank == null)
            {
                return;
            }

            RectTransform root = blank.transform as RectTransform;
            RectTransform[] rects = blank.GetComponentsInChildren<RectTransform>(true);
            if (rects != null && rects.Length > 0)
            {
                blank.transform.localScale = Vector3.one;
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rect = rects[i];
                    if (rect == null || rect == root)
                    {
                        continue;
                    }

                    int id = rect.GetInstanceID();
                    Vector3 baseScale;
                    if (!OriginalCrosshairPartScales.TryGetValue(id, out baseScale))
                    {
                        baseScale = rect.localScale;
                        OriginalCrosshairPartScales[id] = baseScale;
                    }
                    rect.localScale = baseScale * RuntimeFeatureState.CrosshairSizeMultiplier;

                    Vector2 baseSize;
                    if (!OriginalCrosshairPartSizes.TryGetValue(id, out baseSize))
                    {
                        baseSize = rect.sizeDelta;
                        OriginalCrosshairPartSizes[id] = baseSize;
                    }
                    rect.sizeDelta = baseSize * RuntimeFeatureState.CrosshairSizeMultiplier;

                    Vector3 basePosition;
                    if (!OriginalCrosshairPartPositions.TryGetValue(id, out basePosition) || IsRuntimeCrosshairPart(blank, rect))
                    {
                        basePosition = rect.localPosition;
                        OriginalCrosshairPartPositions[id] = basePosition;
                    }
                    rect.localPosition = basePosition * RuntimeFeatureState.CrosshairSizeMultiplier;
                }
                return;
            }

            blank.transform.localScale = Vector3.one * RuntimeFeatureState.CrosshairSizeMultiplier;
        }

        public static float ScaleAngle(float angle)
        {
            return angle * RuntimeFeatureState.CrosshairSpreadMultiplier;
        }

        public static Vector3 ScaleSizeVector(Vector3 value)
        {
            return value * RuntimeFeatureState.CrosshairSizeMultiplier;
        }

        private static bool IsRuntimeCrosshairPart(GuiCrosshairBlank blank, RectTransform rect)
        {
            GuiCrosshair crosshair = blank as GuiCrosshair;
            if (crosshair == null || crosshair.Movable == null)
            {
                return false;
            }

            for (int i = 0; i < crosshair.Movable.Count; i++)
            {
                if (crosshair.Movable[i] == rect)
                {
                    return true;
                }
            }
            return false;
        }

        public static GameObject GetAppropriateCrosshairPrefab(GuiCrosshairController controller, ReticleInfo reticleInfo)
        {
            if (controller == null || reticleInfo == null)
            {
                return null;
            }

            ReticleType type = reticleInfo.Type;
            ReticleType? forcedType = GetForcedType();
            if (forcedType != null && type != ReticleType.Melee)
            {
                type = forcedType.Value;
            }

            switch (type)
            {
                case ReticleType.Dot:
                    return controller.PrototypeDot.gameObject;
                case ReticleType.Crosshair:
                    return controller.PrototypeCrosshair.gameObject;
                case ReticleType.BrokenCircle:
                    return controller.PrototypeBrokenCircle.gameObject;
                case ReticleType.Hashed:
                    return controller.PrototypeHashed.gameObject;
                case ReticleType.HashedCrosshair:
                    return controller.PrototypeHashedCrosshair.gameObject;
                case ReticleType.Melee:
                    return controller.PrototypeMelee.gameObject;
                default:
                    return controller.PrototypeDot.gameObject;
            }
        }

        private static ReticleType? GetForcedType()
        {
            switch (RuntimeFeatureState.CrosshairForceShape)
            {
                case "Dot":
                    return ReticleType.Dot;
                case "Crosshair":
                    return ReticleType.Crosshair;
                case "BrokenCircle":
                    return ReticleType.BrokenCircle;
                case "Hashed":
                    return ReticleType.Hashed;
                case "HashedCrosshair":
                    return ReticleType.HashedCrosshair;
                case "Melee":
                    return ReticleType.Melee;
                default:
                    return null;
            }
        }
    }
}
"@

$HelperSource += @"
namespace BnlCommunityFixes
{
    public sealed class HealingNumberHoldController : MonoBehaviour
    {
        public float HoldUntil;
        private const float FadeDuration = 0.5f;
        private float fadeStart = -1f;

        public void Extend(float holdUntil)
        {
            HoldUntil = Mathf.Max(HoldUntil, holdUntil);
            gameObject.SetActive(true);
            fadeStart = -1f;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (Time.time <= HoldUntil)
            {
                fadeStart = -1f;
                SetAlpha(1f);
            }
            else
            {
                if (fadeStart < 0f)
                {
                    fadeStart = Time.time;
                }

                float t = Mathf.Clamp01((Time.time - fadeStart) / FadeDuration);
                SetAlpha(1f - t);
                if (t >= 1f)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }

        private void SetAlpha(float alpha)
        {
            CanvasGroup[] groups = gameObject.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].alpha = alpha;
            }

            Graphic[] graphics = gameObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color color = graphics[i].color;
                color.a = alpha;
                graphics[i].color = color;
            }
        }
    }

    public sealed class DamageNumberHoldController : MonoBehaviour
    {
        public float HoldUntil;
        private const float FadeDuration = 0.5f;
        private float fadeStart = -1f;

        public void Extend(float holdUntil)
        {
            HoldUntil = Mathf.Max(HoldUntil, holdUntil);
            gameObject.SetActive(true);
            fadeStart = -1f;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (Time.time <= HoldUntil)
            {
                fadeStart = -1f;
                SetAlpha(1f);
            }
            else
            {
                if (fadeStart < 0f) fadeStart = Time.time;
                float t = Mathf.Clamp01((Time.time - fadeStart) / FadeDuration);
                SetAlpha(1f - t);
                if (t >= 1f) UnityEngine.Object.Destroy(gameObject);
            }
        }

        private void SetAlpha(float alpha)
        {
            CanvasGroup[] groups = gameObject.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++) groups[i].alpha = alpha;
            Graphic[] graphics = gameObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color color = graphics[i].color;
                color.a = alpha;
                graphics[i].color = color;
            }
        }
    }

    public static class CombatNumberRuntime
    {
        private const float CollectTime = 0.15f;
        private const float HealContinueGrace = 2.5f;
        private const float StandardDisplayHold = 1.0f;
        private static readonly Dictionary<uint, ActiveDamageNumber> ActiveDamageNumbers = new Dictionary<uint, ActiveDamageNumber>();
        private static readonly Dictionary<uint, ActiveHealNumber> ActiveHealNumbers = new Dictionary<uint, ActiveHealNumber>();

        static CombatNumberRuntime()
        {
            RuntimeFeatureState.ConfigureCombat(
                $(Format-BoolLiteral $DamageHealingConfig.enabled),
                new Color($(Format-FloatLiteral $DamageColor.R), $(Format-FloatLiteral $DamageColor.G), $(Format-FloatLiteral $DamageColor.B), 1f),
                new Color($(Format-FloatLiteral $CritDamageColor.R), $(Format-FloatLiteral $CritDamageColor.G), $(Format-FloatLiteral $CritDamageColor.B), 1f),
                new Color($(Format-FloatLiteral $HealColor.R), $(Format-FloatLiteral $HealColor.G), $(Format-FloatLiteral $HealColor.B), 1f),
                $(Format-BoolLiteral $UseDamageColor),
                $(Format-BoolLiteral $UseCritDamageColor),
                $(Format-BoolLiteral $UseHealColor),
                $(Format-FloatLiteral $DamageSize),
                $(Format-FloatLiteral $HealSize),
                $(Format-FloatLiteral $SelfHealSize),
                $(Format-FloatLiteral $DamageHealingAlpha),
                $(Format-FloatLiteral $MinimumHeal),
                $(Format-BoolLiteral $ShowFriendlyHealing),
                $(Format-BoolLiteral $ShowSelfHealing),
                $(Format-BoolLiteral $CombineDamageUntilHidden),
                $(Format-BoolLiteral $CombineHealingUntilHidden),
                65f,
                65f);
            RuntimeFeatureState.SetSelfHealX($(Format-FloatLiteral $SelfHealX));
            RuntimeFeatureState.SetSelfHealY($(Format-FloatLiteral $SelfHealY));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void AttachHealing(GuiDamageNumberDetector detector)
        {
            if (detector == null) return;
            currentDetector = detector;
            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger == null) return;
                var field = typeof(ZoneMessenger).GetField("OnGlobalUnitHealthChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (object.ReferenceEquals(field, null)) return;
                var eventSource = field.GetValue(messenger);
                if (object.ReferenceEquals(eventSource, null)) return;
                var subscribeMethod = eventSource.GetType().GetMethod("Subscribe");
                if (object.ReferenceEquals(subscribeMethod, null)) return;
                var handler = System.Delegate.CreateDelegate(
                    typeof(System.Action<>).MakeGenericType(typeof(GlobalUnitHealthChangeArgs)),
                    typeof(CombatNumberRuntime).GetMethod("OnHealthChangedReflected", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        private static void OnHealthChangedReflected(GlobalUnitHealthChangeArgs args)
        {
            try { OnHealthChanged(currentDetector, args); } catch { }
        }

        private static GuiDamageNumberDetector currentDetector;

        public static bool ShouldShowDamageNumber(DamageInfo args)
        {
            if (args == null || args.SourceUnitId == null)
            {
                return false;
            }

            Unit source = Singleton<UnitsRegistry>.Instance.Get(args.SourceUnitId.Value);
            if (source == null)
            {
                return false;
            }

            if (source.IsMyPlayer)
            {
                return true;
            }

            return source.UnitCard != null &&
                source.UnitCard.TreatHitsAsOwnerHits &&
                source.OwnerPlayerId == Singleton<PlayerData>.Instance.Id;
        }

        private static readonly System.Collections.Generic.HashSet<int> attachedNumbers = new System.Collections.Generic.HashSet<int>();
        private static readonly System.Reflection.FieldInfo healthbarMakerField = typeof(GuiHealthbar).GetField("maker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static void AttachToHealthbarUi(GuiDamageNumber number, Unit unit, float offsetY)
        {
            if (object.ReferenceEquals(number, null) || object.ReferenceEquals(unit, null)) return;
            int id = number.GetInstanceID();
            // Already processed - just update position if parented to healthbar
            if (attachedNumbers.Contains(id))
            {
                if (number.transform.parent != null)
                    number.transform.localPosition = new Vector3(0f, offsetY, 0f);
                return;
            }
            GuiHealthBarMaker maker = unit.GetComponentInChildren<GuiHealthBarMaker>();
            if (object.ReferenceEquals(maker, null))
            {
                number.GetOrAddComponent<GuiFollow>().WorldTarget = unit.transform;
                attachedNumbers.Add(id);
                return;
            }
            if (object.ReferenceEquals(healthbarMakerField, null))
            {
                number.GetOrAddComponent<GuiFollow>().WorldTarget = maker.transform;
                attachedNumbers.Add(id);
                return;
            }
            GuiHealthbar[] allBars = UnityEngine.Object.FindObjectsOfType<GuiHealthbar>();
            for (int i = 0; i < allBars.Length; i++)
            {
                if (object.ReferenceEquals(allBars[i], null)) continue;
                object hbMaker = healthbarMakerField.GetValue(allBars[i]);
                if (object.ReferenceEquals(hbMaker, maker))
                {
                    number.transform.SetParent(allBars[i].Content.transform, false);
                    number.transform.localPosition = new Vector3(0f, offsetY, 0f);
                    GuiFollow existing = number.GetComponent<GuiFollow>();
                    if (!object.ReferenceEquals(existing, null))
                    {
                        existing.enabled = false;
                        UnityEngine.Object.Destroy(existing);
                    }
                    attachedNumbers.Add(id);
                    return;
                }
            }
            // No matching healthbar found - track via maker transform
            number.GetOrAddComponent<GuiFollow>().WorldTarget = maker.transform;
            attachedNumbers.Add(id);
        }

        public static void ApplyDamageNumber(GuiDamageNumber number, bool crit)
        {
            if (object.ReferenceEquals(number, null)) return;
            AttachToHealthbarUi(number, number.Unit, RuntimeFeatureState.DamageNumberOffsetY);
            if (RuntimeFeatureState.DamageNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * RuntimeFeatureState.DamageNumberSizeMultiplier;
            bool useColor = crit ? RuntimeFeatureState.UseCritDamageNumberColor : RuntimeFeatureState.UseDamageNumberColor;
            Color color = crit ? RuntimeFeatureState.GetCritDamageNumberColor() : RuntimeFeatureState.GetDamageNumberColor();
            if (number.Damage != null && (useColor || RuntimeFeatureState.DamageNumberSizeMultiplier != 1f))
            {
                if (useColor) number.Damage.color = color;
                if (RuntimeFeatureState.DamageNumberSizeMultiplier != 1f) number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * RuntimeFeatureState.DamageNumberSizeMultiplier));
            }
            if (useColor) ApplyGraphics(number.gameObject, color);
        }

        public static float GetDamageCollectTime(float original)
        {
            return RuntimeFeatureState.CombineDamageUntilHidden ? 99999f : original;
        }

        public static GuiDamageNumber RefreshDamageNumber(GuiDamageNumberDetector detector, GuiDamageNumber oldNumber, Unit unit, float value, bool crit)
        {
            if (!RuntimeFeatureState.CombineDamageUntilHidden || detector == null || unit == null || oldNumber == null)
            {
                RefreshDamageHold(oldNumber);
                ApplyDamageNumber(oldNumber, crit);
                return oldNumber;
            }

            ActiveDamageNumber active;
            if (ActiveDamageNumbers.TryGetValue(unit.Id, out active) && active.Number != null)
            {
                if (active.Number == oldNumber)
                {
                    active.Value = value;
                    active.IsCrit = crit;
                    active.LastTime = Time.time;
                    RefreshDamageHold(active.Number);
                    ApplyDamageNumber(active.Number, active.IsCrit);
                    return active.Number;
                }
                else
                {
                    float combined = active.Value + value;
                    UnityEngine.Object.Destroy(active.Number.gameObject);
                    active.Value = combined;
                    active.IsCrit = crit;
                    active.LastTime = Time.time;
                    active.Number = oldNumber;
                    oldNumber.DamageValue = combined;
                    RefreshDamageHold(oldNumber);
                    ApplyDamageNumber(oldNumber, active.IsCrit);
                    return oldNumber;
                }
            }

            // First hit: adopt the game-created number directly.
            RefreshDamageHold(oldNumber);
            ApplyDamageNumber(oldNumber, crit);
            ActiveDamageNumbers[unit.Id] = new ActiveDamageNumber
            {
                Number = oldNumber,
                Value = value,
                IsCrit = crit,
                LastTime = Time.time
            };
            return oldNumber;
        }

        public static void OnHealthChanged(GuiDamageNumberDetector detector, GlobalUnitHealthChangeArgs args)
        {
            if (detector == null || args == null || args.unit == null) return;
            if (!Singleton<Settings>.Instance.ShowCombatNumbers) return;

            float amount = args.newHealth - args.oldHealth;
            if (amount < RuntimeFeatureState.MinimumHeal) return;
            if (args.unit.PlayerId == null) return;
            // Suppress the fake full-health "heal" fired on spawn: the unit's Health field
            // starts at 0, so the first server update produces oldHealth=0 → newHealth=maxHealth.
            // Any heal event where oldHealth was 0 and the unit is brand-new is a spawn artifact.
            if (args.oldHealth == 0f && Time.time - args.unit.CreationTime < 1f) return;
            if (args.unit.IsMyPlayer)
            {
                if (!RuntimeFeatureState.ShowSelfHealing) return;
            }
            else if (!RuntimeFeatureState.ShowFriendlyHealing || !args.unit.Team.IsMy())
            {
                return;
            }

            SpawnOrUpdateHeal(detector, args.unit, amount);
        }

        private static void SpawnOrUpdateHeal(GuiDamageNumberDetector detector, Unit unit, float amount)
        {
            ActiveHealNumber active;
            if (ActiveHealNumbers.TryGetValue(unit.Id, out active) && ShouldCombineHeal(active))
            {
                active.Value += amount;
                active.LastTime = Time.time;
                if (active.Number == null)
                {
                    active.Number = CreateHealNumber(detector, unit, active.Value);
                }
                else
                {
                    ApplyHealTextAndColor(active.Number, active.Value, detector.HealColor, false);
                    RefreshHealHold(active.Number);
                }
                return;
            }

            GuiDamageNumber number = CreateHealNumber(detector, unit, amount);
            ActiveHealNumbers[unit.Id] = new ActiveHealNumber
            {
                Number = number,
                Value = amount,
                LastTime = Time.time
            };
        }

        private static GuiDamageNumber CreateHealNumber(GuiDamageNumberDetector detector, Unit unit, float amount)
        {
            GuiDamageNumber number = GameObjectMaker.AddChild<GuiDamageNumber>(detector.transform, detector.Prefab.gameObject, false);
            number.IsMeCaster = true;
            number.Unit = unit;
            number.DamageValue = amount;

            if (unit.IsMyPlayer)
            {
                // Self-heal: stay on HUD canvas at a fixed screen position near health bar
                GuiFollow g = number.GetComponent<GuiFollow>();
                if (!object.ReferenceEquals(g, null))
                {
                    g.enabled = false;
                    UnityEngine.Object.Destroy(g);
                }
                number.transform.localPosition = new Vector3(RuntimeFeatureState.SelfHealX, RuntimeFeatureState.SelfHealY, 0f);
                float selfSize = RuntimeFeatureState.SelfHealNumberSizeMultiplier;
                if (selfSize != 1f) number.transform.localScale = number.transform.localScale * selfSize;
            }
            else
            {
                AttachToHealthbarUi(number, unit, RuntimeFeatureState.HealNumberOffsetY);
            }
            if (RuntimeFeatureState.HealNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * RuntimeFeatureState.HealNumberSizeMultiplier;
            ApplyHealTextAndColor(number, amount, detector.HealColor, true);
            RefreshHealHold(number);
            return number;
        }

        private static bool ShouldCombineHeal(ActiveHealNumber active)
        {
            if (active == null) return false;
            if (!RuntimeFeatureState.CombineHealingUntilHidden) return Time.time - active.LastTime <= CollectTime;
            return Time.time - active.LastTime <= HealContinueGrace;
        }

        private static void RefreshHealHold(GuiDamageNumber number)
        {
            if (number == null) return;
            HealingNumberHoldController hold = number.gameObject.GetComponent<HealingNumberHoldController>();
            if (hold == null)
            {
                UiTemporary[] temporaries = number.gameObject.GetComponentsInChildren<UiTemporary>(true);
                for (int i = 0; i < temporaries.Length; i++)
                {
                    UnityEngine.Object.Destroy(temporaries[i]);
                }
                hold = number.gameObject.AddComponent<HealingNumberHoldController>();
            }
            hold.Extend(Time.time + (RuntimeFeatureState.CombineHealingUntilHidden ? HealContinueGrace : StandardDisplayHold));
        }

        private static void RefreshDamageHold(GuiDamageNumber number)
        {
            if (number == null) return;
            DamageNumberHoldController hold = number.gameObject.GetComponent<DamageNumberHoldController>();
            if (hold == null)
            {
                UiTemporary[] temporaries = number.gameObject.GetComponentsInChildren<UiTemporary>(true);
                for (int i = 0; i < temporaries.Length; i++)
                    UnityEngine.Object.Destroy(temporaries[i]);
                Animation[] animations = number.gameObject.GetComponentsInChildren<Animation>(true);
                for (int i = 0; i < animations.Length; i++)
                    animations[i].Stop();
                Animator[] animators = number.gameObject.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                    animators[i].enabled = false;
                hold = number.gameObject.AddComponent<DamageNumberHoldController>();
            }
            hold.Extend(Time.time + (RuntimeFeatureState.CombineDamageUntilHidden ? HealContinueGrace : StandardDisplayHold));
        }

        private static void ApplyHealTextAndColor(GuiDamageNumber number, float amount, Color defaultHealColor, bool applySize)
        {
            if (number == null || number.Damage == null) return;
            number.Damage.text = "+" + Mathf.RoundToInt(amount).ToString();
            number.Damage.color = RuntimeFeatureState.UseHealNumberColor ? RuntimeFeatureState.GetHealNumberColor() : defaultHealColor;
            if (applySize && RuntimeFeatureState.HealNumberSizeMultiplier != 1f)
            {
                number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * RuntimeFeatureState.HealNumberSizeMultiplier));
            }
            ApplyGraphics(number.gameObject, number.Damage.color);
        }

        private static void ApplyGraphics(GameObject go, Color color)
        {
            Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].color = color;
            }
        }

        private static void RestartLifetime(GameObject go)
        {
            if (go == null) return;
            go.SetActive(true);

            Animation[] animations = go.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                animations[i].Stop();
                animations[i].Rewind();
                animations[i].Play();
            }

            Animator[] animators = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = true;
                animators[i].Play(0, -1, 0f);
                animators[i].Update(0f);
            }

            CanvasGroup[] groups = go.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].alpha = 1f;
            }
        }

        private sealed class ActiveHealNumber
        {
            public GuiDamageNumber Number;
            public float Value;
            public float LastTime;
        }

        private sealed class ActiveDamageNumber
        {
            public GuiDamageNumber Number;
            public float Value;
            public bool IsCrit;
            public float LastTime;
        }
    }
}

"@

if (Test-Path $LockOnHelperSourcePath) {
    $LockOnHelperSource = Get-Content -Raw -LiteralPath $LockOnHelperSourcePath
    $LockOnHelperSource = [regex]::Replace($LockOnHelperSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $LockOnHelperSource
}
if (Test-Path $TrackingHelperSourcePath) {
    $TrackingHelperSource = Get-Content -Raw -LiteralPath $TrackingHelperSourcePath
    $TrackingHelperSource = [regex]::Replace($TrackingHelperSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $TrackingHelperSource
}
if (Test-Path $RuntimeMenuSourcePath) {
    $RuntimeMenuSource = Get-Content -Raw -LiteralPath $RuntimeMenuSourcePath
    $RuntimeMenuSource = [regex]::Replace($RuntimeMenuSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $RuntimeMenuSource
}
if (Test-Path $MatchReplayRecorderSourcePath) {
    $MatchReplayRecorderSource = Get-Content -Raw -LiteralPath $MatchReplayRecorderSourcePath
    $MatchReplayRecorderSource = [regex]::Replace($MatchReplayRecorderSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $MatchReplayRecorderSource
}
if (Test-Path $ReplayPlayerSourcePath) {
    $ReplayPlayerSource = Get-Content -Raw -LiteralPath $ReplayPlayerSourcePath
    $ReplayPlayerSource = [regex]::Replace($ReplayPlayerSource, '^(using\s+[^\r\n]+;\s*)+', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $HelperSource += "`r`n" + $ReplayPlayerSource
}

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class AdsSensitivityRuntime
    {
        static AdsSensitivityRuntime()
        {
            RuntimeFeatureState.ConfigureFov($(Format-BoolLiteral ($EnableFovFeature -and [bool]$FovConfig.enabled)), $DefaultForcedFovLiteral, $AdsSensitivityMultiplierLiteral, $DefaultWeaponModelFovLiteral);
        }

        public static float ApplyAdsScale(float currentScale, Unit unit)
        {
            if (unit != null && unit.GetAimingState() != null)
            {
                return currentScale * RuntimeFeatureState.AdsSensitivityMultiplier;
            }

            return currentScale;
        }

        private static CameraFov cachedCameraFov;
        private static CameraArms cachedCameraArms;

        public static void ApplyCameraFov()
        {
            if (!RuntimeFeatureState.FovSupported) return;
            try
            {
                if (cachedCameraFov == null)
                    cachedCameraFov = UnityEngine.Object.FindObjectOfType<CameraFov>();
                if (cachedCameraFov == null) return;
                Camera camera = FindPrimaryCamera(cachedCameraFov);
                if (camera != null)
                    camera.fieldOfView = RuntimeFeatureState.ForcedFov;
            }
            catch { }
        }

        public static void ApplyWeaponModelFov()
        {
            if (!RuntimeFeatureState.FovSupported) return;
            try
            {
                if (cachedCameraArms == null)
                    cachedCameraArms = UnityEngine.Object.FindObjectOfType<CameraArms>();
                if (cachedCameraArms == null) return;
                Camera[] cameras = cachedCameraArms.GetComponentsInChildren<Camera>(true);
                if (cameras == null || cameras.Length == 0)
                {
                    Camera primary = cachedCameraArms.GetComponent<Camera>();
                    if (primary != null)
                    {
                        primary.fieldOfView = RuntimeFeatureState.WeaponModelFov;
                    }
                    return;
                }

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null)
                    {
                        continue;
                    }

                    if (camera != Camera.main || cameras.Length == 1)
                    {
                        camera.fieldOfView = RuntimeFeatureState.WeaponModelFov;
                    }
                }
            }
            catch
            {
            }
        }

        private static Camera FindPrimaryCamera(Component component)
        {
            if (component == null)
            {
                return Camera.main;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = component.GetType().GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!typeof(Camera).IsAssignableFrom(fields[i].FieldType))
                {
                    continue;
                }

                Camera fieldCamera = fields[i].GetValue(component) as Camera;
                if (fieldCamera != null)
                {
                    return fieldCamera;
                }
            }

            PropertyInfo[] properties = component.GetType().GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                if (!properties[i].CanRead || !typeof(Camera).IsAssignableFrom(properties[i].PropertyType) || properties[i].GetIndexParameters().Length != 0)
                {
                    continue;
                }

                Camera propertyCamera = null;
                try
                {
                    propertyCamera = properties[i].GetValue(component, null) as Camera;
                }
                catch
                {
                }

                if (propertyCamera != null)
                {
                    return propertyCamera;
                }
            }

            Camera[] children = component.GetComponentsInChildren<Camera>(true);
            if (children != null && children.Length > 0)
            {
                return children[0];
            }

            return Camera.main;
        }
    }
}

namespace BnlCommunityFixes
{
    public static class TeamColorRuntime
    {
        static TeamColorRuntime()
        {
            RuntimeFeatureState.ConfigureTeamColors(
                $(Format-BoolLiteral $TeamColorConfig.enabled),
                new Color($(Format-FloatLiteral $RuntimeFriendlyTeamColor.R), $(Format-FloatLiteral $RuntimeFriendlyTeamColor.G), $(Format-FloatLiteral $RuntimeFriendlyTeamColor.B), 1f),
                new Color($(Format-FloatLiteral $RuntimeEnemyTeamColor.R), $(Format-FloatLiteral $RuntimeEnemyTeamColor.G), $(Format-FloatLiteral $RuntimeEnemyTeamColor.B), 1f));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static Color GetGuiFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetGuiEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetGuiBackgroundFriendlyColor() { return RuntimeFeatureState.GetGuiBackgroundFriendlyColor(); }
        public static Color GetGuiBackgroundEnemyColor() { return RuntimeFeatureState.GetGuiBackgroundEnemyColor(); }
        public static Color GetObjectCommonFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetObjectCommonEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetForceFieldFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetForceFieldEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
        public static Color GetIceFriendlyColor() { return RuntimeFeatureState.GetGuiFriendlyColor(); }
        public static Color GetIceEnemyColor() { return RuntimeFeatureState.GetGuiEnemyColor(); }
    }

    public static class BaseObjectiveBeamRuntime
    {
        static BaseObjectiveBeamRuntime()
        {
            RuntimeFeatureState.ConfigureBaseObjectiveBeam(
                $(Format-BoolLiteral $BaseObjectiveBeamConfig.enabled),
                $(Format-BoolLiteral ([bool]$BaseObjectiveBeamConfig.hide_beam)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ShouldHide()
        {
            return RuntimeFeatureState.HideBaseObjectiveBeam;
        }
    }

    public static class HideImpactVfxRuntime
    {
        private static bool initialized;

        static HideImpactVfxRuntime()
        {
            RuntimeFeatureState.ConfigureHideImpactVfx(
                $(Format-BoolLiteral $HideImpactVfxConfig.enabled),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_impact_vfx)),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_lava_water_plane)),
                $(Format-BoolLiteral ([bool]$HideImpactVfxConfig.hide_falling_blocks)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL HideVfx] initialized — hideImpact=" + RuntimeFeatureState.HideImpactVfx + " hidePlane=" + RuntimeFeatureState.HideLavaWaterPlane + " hideFallingBlocks=" + RuntimeFeatureState.HideFallingBlocks);
            initialized = true;
        }

        public static void EnsureInit() { }

        public static bool ShouldHideVfx()
        {
            return RuntimeFeatureState.HideImpactVfx;
        }

        public static bool ShouldHidePlane()
        {
            return RuntimeFeatureState.HideLavaWaterPlane;
        }

        public static bool ShouldHideFallingBlocks()
        {
            return RuntimeFeatureState.HideFallingBlocks;
        }

        public static void DestroyFallingBlock(UnityEngine.GameObject go)
        {
            if (!RuntimeFeatureState.HideFallingBlocks) return;
            if (go == null) return;
            UnityEngine.Object.Destroy(go);
        }

        public static void HidePlane(MapPlane plane)
        {
            if (!RuntimeFeatureState.HideLavaWaterPlane) return;
            if (plane == null) return;
            Renderer[] renderers = plane.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
            UnityEngine.Debug.Log("[BNL HideVfx] HidePlane: disabled " + renderers.Length + " renderer(s)");
        }
    }

    public static class UnitGuiScaleRuntime
    {
        static UnitGuiScaleRuntime()
        {
            RuntimeFeatureState.ConfigureUnitGuiScale(
                $(Format-BoolLiteral $UnitGuiScaleConfig.enabled),
                $(Format-FloatLiteral ([double]$UnitGuiScaleConfig.scale_multiplier)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL UnitGuiScale] initialized — scale=" + RuntimeFeatureState.UnitGuiScaleMultiplier);
        }

        public static void EnsureInit() { }

        public static float GetScaleMultiplier()
        {
            return RuntimeFeatureState.UnitGuiScaleMultiplier;
        }
    }

    public static class WsiScaleRuntime
    {
        static WsiScaleRuntime()
        {
            RuntimeFeatureState.ConfigureWsiScale(
                $(Format-BoolLiteral $WsiConfig.scale_enabled),
                $(Format-FloatLiteral ([double]$WsiConfig.scale_multiplier)));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL WsiScale] initialized — scale=" + RuntimeFeatureState.WsiScaleMultiplier);
        }

        public static void EnsureInit() { }

        public static void ApplyScale(GuiWorldSpaceIndicator indicator)
        {
            if (!RuntimeFeatureState.WsiScaleSupported) return;
            float m = RuntimeFeatureState.WsiScaleMultiplier;
            indicator.IconMinSize = indicator.IconMinSize * m;
            indicator.IconMaxSize = indicator.IconMaxSize * m;
        }
    }

    public static class MapRenderOverrideRuntime
    {
        static MapRenderOverrideRuntime()
        {
            RuntimeFeatureState.ConfigureMapRenderOverride(
                $(Format-BoolLiteral $MapRenderConfig.enabled),
                "$MapRenderPresetLiteral");
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            UnityEngine.Debug.Log("[BNL MapRender] initialized — preset=" + RuntimeFeatureState.MapRenderOverride);
        }

        public static void EnsureInit() { }

        public static string GetRenderOverride(string original)
        {
            if (!RuntimeFeatureState.MapRenderOverrideSupported) return original;
            string ov = RuntimeFeatureState.MapRenderOverride;
            if (ov == null || ov == "Default") return original;
            return ov;
        }
    }

    public static class DpsOverlayRuntime
    {
        private static float sessionStart;
        private static float sessionTotal;
        private static float lastHitTime;
        private static float currentDps;
        private static bool sessionActive;
        private const float SessionResetSeconds = 2f;

        static DpsOverlayRuntime()
        {
            RuntimeFeatureState.ConfigureDpsOverlay(true, false);
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
            SubscribePhaseUpdate();
            UnityEngine.Debug.Log("[BNL DpsOverlay] initialized");
        }

        public static void EnsureInit() { }

        public static void TryRecordDamage(DamageInfo args)
        {
            if (!RuntimeFeatureState.DpsOverlayEnabled) return;
            if (args == null) return;
            Unit player = Singleton<UnitsRegistry>.Instance.GetPlayer();
            if (player == null || args.SourceUnitId == null || args.SourceUnitId.Value != player.Id) return;
            RecordDamage(args.Damage);
        }

        public static void RecordDamage(float amount)
        {
            if (amount <= 0f) return;

            float now = UnityEngine.Time.time;

            if (!sessionActive || (now - lastHitTime) >= SessionResetSeconds)
            {
                sessionStart = now;
                sessionTotal = amount;
                lastHitTime = now;
                sessionActive = true;
                return;
            }

            sessionTotal += amount;
            lastHitTime = now;

            float elapsed = now - sessionStart;
            if (elapsed > 0f)
                currentDps = sessionTotal / elapsed;
        }

        public static void ResetSession()
        {
            sessionActive = false;
            sessionTotal = 0f;
            currentDps = 0f;
        }

        public static string GetDisplayText()
        {
            return "DPS: " + UnityEngine.Mathf.RoundToInt(currentDps).ToString();
        }


        private static void SubscribePhaseUpdate()
        {
            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger == null) return;
                var field = typeof(ZoneMessenger).GetField("OnGlobalPhaseUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (object.ReferenceEquals(field, null)) return;
                var eventSource = field.GetValue(messenger);
                if (object.ReferenceEquals(eventSource, null)) return;
                var subscribeMethod = eventSource.GetType().GetMethod("Subscribe");
                if (object.ReferenceEquals(subscribeMethod, null)) return;
                var handler = System.Delegate.CreateDelegate(
                    typeof(System.Action<>).MakeGenericType(typeof(GlobalPhaseUpdateEventArgs)),
                    typeof(DpsOverlayRuntime).GetMethod("OnPhaseUpdate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        public static void OnPhaseUpdate(GlobalPhaseUpdateEventArgs args)
        {
            try
            {
                if (args == null) return;
                bool wasNotAssault = args.oldPhase == null || args.oldPhase.PhaseType != Protocol.ZonePhaseType.Assault;
                bool isAssault = args.newPhase != null && args.newPhase.PhaseType == Protocol.ZonePhaseType.Assault;
                if (wasNotAssault && isAssault)
                {
                    ResetSession();
                }
            }
            catch { }
        }
    }

    public static class AimHealthbarRuntime
    {
        static AimHealthbarRuntime()
        {
            RuntimeFeatureState.ConfigureAimHealthbar($(Format-BoolLiteral $AimHealthbarConfig.enabled), $(Format-BoolLiteral $AimHealthbarConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool ShouldShow(Unit healthbarUnit)
        {
            if (!RuntimeFeatureState.AimHealthbarEnabled || healthbarUnit == null)
            {
                return false;
            }

            Crosshair crosshair = Singleton<Crosshair>.Instance;
            if (crosshair == null)
            {
                return false;
            }

            return crosshair.RaycastUnitInfo.Unit == healthbarUnit;
        }
    }

    public static class DeathCamRuntime
    {
        static DeathCamRuntime()
        {
            RuntimeFeatureState.ConfigureDeathCamHealthbar($(Format-BoolLiteral $DeathCamHealthbarConfig.enabled), $(Format-BoolLiteral $DeathCamHealthbarConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static bool IsDeathCamFriendly(Unit healthbarUnit)
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return false;
            if (healthbarUnit == null) return false;
            if (healthbarUnit.IsMyPlayer) return false;
            // Only show for actual player units (has PlayerId), not devices/minions
            if (!healthbarUnit.PlayerId.HasValue) return false;
            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null) return false;
            Unit player = registry.GetPlayer();
            if (player == null || !player.IsDeath) return false;
            return healthbarUnit.Team == player.Team;
        }

        public static void AttachDeathCamController(GuiHealthbar healthbar)
        {
            if (healthbar == null) return;
            DeathCamHealthbarController ctrl = healthbar.gameObject.GetComponent<DeathCamHealthbarController>();
            if (ctrl == null)
                ctrl = healthbar.gameObject.AddComponent<DeathCamHealthbarController>();
            ctrl.Init(healthbar);
        }

        public static void UpdateDeathCamHpText(UnityEngine.UI.Text nicknameText)
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return;
            if (nicknameText == null) return;
            try
            {
                CameraDeath deathCam = Singleton<CameraDeath>.Instance;
                if (deathCam == null) return;
                Transform targetTransform = deathCam.Target;
                if (targetTransform == null) return;
                Unit targetUnit = targetTransform.GetComponent<Unit>();
                if (targetUnit == null) return;
                float health = targetUnit.Health;
                float maxHealth = targetUnit.MaxHealth;
                float pct = (maxHealth > 0f) ? Mathf.Clamp01(health / maxHealth) : 0f;
                int filled = Mathf.RoundToInt(pct * 10f);
                string bar = "";
                for (int i = 0; i < 10; i++)
                    bar += (i < filled) ? "\u2588" : "\u2591";
                string playerName = targetUnit.name;
                if (targetUnit.PlayerId.HasValue)
                {
                    FriendInfo fi = PlayerData.Instance.FindFriend(targetUnit.PlayerId.Value);
                    if (fi != null && !string.IsNullOrEmpty(fi.Nickname))
                    {
                        playerName = fi.Nickname;
                    }
                }
                string hpInfo = string.Format(" {0} {1:F0}/{2:F0}", bar, health, maxHealth);
                nicknameText.text = playerName + hpInfo;
            }
            catch { }
        }
    }

    public sealed class DeathCamHealthbarController : MonoBehaviour
    {
        private static readonly float LowThreshold = $(Format-FloatLiteral $FriendlyLowHealthThreshold);
        private static readonly Color AlertColor = new Color($(Format-FloatLiteral $FriendlyLowHealthColor.R), $(Format-FloatLiteral $FriendlyLowHealthColor.G), $(Format-FloatLiteral $FriendlyLowHealthColor.B), 1f);
        private static readonly FieldInfo UnitField = typeof(GuiHealthbar).GetField("unit", BindingFlags.Instance | BindingFlags.NonPublic);

        private GuiHealthbar healthbar;

        public void Init(GuiHealthbar source)
        {
            healthbar = source;
        }

        private void LateUpdate()
        {
            if (!RuntimeFeatureState.DeathCamHealthbarEnabled) return;
            if (healthbar == null || healthbar.HealthBar == null) return;

            Unit unit = ReferenceEquals(UnitField, null) ? null : UnitField.GetValue(healthbar) as Unit;
            if (unit == null || unit.IsMyPlayer || !unit.PlayerId.HasValue || unit.IsDeath) return;

            UnitsRegistry registry = Singleton<UnitsRegistry>.Instance;
            Unit myPlayer = registry != null ? registry.GetPlayer() : null;
            if (myPlayer == null || !myPlayer.IsDeath) return;
            if (unit.Team != myPlayer.Team) return;

            float maxHp = unit.MaxHealth;
            bool isLow = maxHp > 0f && (unit.Health / maxHp) <= LowThreshold;
            if (isLow)
            {
                healthbar.HealthBar.color = AlertColor;
                if (healthbar.Title != null)
                    healthbar.Title.color = AlertColor;
            }
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class HealAlertRuntime
    {
        private static readonly bool UseDamageIndicatorColor = $(Format-BoolLiteral $HealAlertUseDamageColor);
        private static readonly bool UseHealIndicatorColor   = $(Format-BoolLiteral $HealAlertUseHealColor);
        private static readonly Color DamageIndicatorBaseColor = new Color($(Format-FloatLiteral $HealAlertDamageColor.R), $(Format-FloatLiteral $HealAlertDamageColor.G), $(Format-FloatLiteral $HealAlertDamageColor.B), $(Format-FloatLiteral $HealAlertDamageColor.A));
        private static readonly Color HealIndicatorBaseColor   = new Color($(Format-FloatLiteral $HealAlertHealColor.R),   $(Format-FloatLiteral $HealAlertHealColor.G),   $(Format-FloatLiteral $HealAlertHealColor.B),   $(Format-FloatLiteral $HealAlertHealColor.A));

        static HealAlertRuntime()
        {
            RuntimeFeatureState.ConfigureHealAlert(
                $(Format-BoolLiteral $HealAlertConfig.enabled),
                $(Format-FloatLiteral $HealAlertDamageSize),
                $(Format-FloatLiteral $HealAlertHealSize),
                $(Format-FloatLiteral $HealAlertMinimumHeal),
                $(Format-BoolLiteral $HealAlertShowDir));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void AttachHealBridge(GuiHitAlertMaker maker)
        {
            if (maker == null) return;
            currentBridgeMaker = maker;
            try
            {
                ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
                if (messenger == null) return;
                var field = typeof(ZoneMessenger).GetField("OnGlobalUnitHealthChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (object.ReferenceEquals(field, null)) return;
                var eventSource = field.GetValue(messenger);
                if (object.ReferenceEquals(eventSource, null)) return;
                var subscribeMethod = eventSource.GetType().GetMethod("Subscribe");
                if (object.ReferenceEquals(subscribeMethod, null)) return;
                var handler = System.Delegate.CreateDelegate(
                    typeof(System.Action<>).MakeGenericType(typeof(GlobalUnitHealthChangeArgs)),
                    maker,
                    typeof(HealAlertRuntime).GetMethod("OnHealthChangedReflected", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                if (object.ReferenceEquals(handler, null)) return;
                var parameters = new object[] { handler, null };
                subscribeMethod.Invoke(eventSource, parameters);
            }
            catch { }
        }

        private static void OnHealthChangedReflected(GlobalUnitHealthChangeArgs args)
        {
            try { OnHealthChanged(currentBridgeMaker, args); } catch { }
        }

        private static GuiHitAlertMaker currentBridgeMaker;

        public static void ApplyDamageIndicator(Component component)
        {
            GameObject go = component == null ? null : component.gameObject;
            if (go == null) return;
            if (RuntimeFeatureState.HealAlertDamageSizeMultiplier != 1f)
                go.transform.localScale = go.transform.localScale * RuntimeFeatureState.HealAlertDamageSizeMultiplier;
            if (UseDamageIndicatorColor) ApplyGraphics(go, DamageIndicatorBaseColor);
        }

        public static void OnHealthChanged(GuiHitAlertMaker maker, GlobalUnitHealthChangeArgs args)
        {
            if (maker == null || args == null || args.unit == null) return;
            if (!args.unit.IsMyPlayer) return;
            if (maker.Content == null || !maker.Content.activeSelf) return;

            float healAmount = args.newHealth - args.oldHealth;
            if (healAmount < RuntimeFeatureState.HealAlertMinimumHeal) return;

            if (RuntimeFeatureState.HealAlertShowDirectionOnHeal)
            {
                GuiHitAlert dir = GameObjectMaker.AddChild<GuiHitAlert>(maker.transform, maker.DirectionHitPrefab.gameObject, false);
                dir.DestroySelf = true;
                dir.AttackerPosition = Vector3.zero;
                ApplyHealIndicator(dir);
            }

            GuiHitAlertOnScreenEdge edge = GameObjectMaker.AddChild<GuiHitAlertOnScreenEdge>(maker.transform, maker.OnScreenEdgePrefab.gameObject, false);
            edge.DestroySelf = true;
            edge.AttackerPosition = Vector3.zero;
            ApplyHealIndicator(edge);
        }

        private static void ApplyHealIndicator(Component component)
        {
            GameObject go = component == null ? null : component.gameObject;
            if (go == null) return;
            if (RuntimeFeatureState.HealAlertHealSizeMultiplier != 1f)
                go.transform.localScale = go.transform.localScale * RuntimeFeatureState.HealAlertHealSizeMultiplier;
            if (UseHealIndicatorColor) ApplyGraphics(go, HealIndicatorBaseColor);
            ApplyImmediateFade(go);
        }

        private static void ApplyGraphics(GameObject go, Color color)
        {
            Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) graphics[i].color = color;
        }

        private static void ApplyImmediateFade(GameObject go)
        {
            UiTemporary[] temporaries = go.GetComponentsInChildren<UiTemporary>(true);
            for (int i = 0; i < temporaries.Length; i++)
            {
                temporaries[i].ShowDuration = 0f;
                temporaries[i].FadeDuration = 0.1f;
            }
        }
    }
}
"@

if ($LocalBuildPreviewConfig.enabled) {
    $HelperSource += @"

namespace BnlCommunityFixes
{
    public static class LocalBuildPredictionRuntime
    {
        private static readonly float InstantCrateChainWindowSeconds = 5.0f;
        private static PredictionManager manager;
        private static float instantCrateChainUntil;

        private const float PredictionTimeoutSeconds = 3f;

        static LocalBuildPredictionRuntime()
        {
            RuntimeFeatureState.ConfigureLocalBuildPreview($(Format-BoolLiteral $LocalBuildPreviewConfig.enabled), true, PredictionTimeoutSeconds);
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        private static bool Enabled
        {
            get { return RuntimeFeatureState.LocalBuildPreviewEnabled; }
        }

        private static PredictionManager Manager
        {
            get
            {
                if (!Enabled) return null;
                if (manager == null)
                {
                    UnityEngine.GameObject go = UnityEngine.GameObject.Find("BNL_LOCAL_BUILD_PREDICTION");
                    if (go == null) { go = new UnityEngine.GameObject("BNL_LOCAL_BUILD_PREDICTION"); UnityEngine.Object.DontDestroyOnLoad(go); }
                    manager = go.GetComponent<PredictionManager>();
                    if (manager == null) manager = go.AddComponent<PredictionManager>();
                }
                return manager;
            }
        }

        private static bool IsBlockBackedDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            return blockCard != null && blockCard.BlockId != 0;
        }

        private static bool IsInstantPlacementDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            if (blockCard == null || blockCard.BlockId == 0) return false;
            if (deviceCard.BuildTime.GetValueOrDefault(0f) > 0f) return false;
            // Bounce/speed pads have server-side placement validation that clients can't replicate —
            // skip instant placement for them to avoid ghost blocks the server rejects.
            if (blockCard.Special is BlockSpecialBounce) return false;
            if (blockCard.Special is BlockSpecialFastMovement) return false;
            return true;
        }

        private static bool IsCratePlacementDeviceKey(Key deviceKey)
        {
            if (!Enabled) return false;
            CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
            if (deviceCard == null) return false;
            CardBlock blockCard = Singleton<Catalogue>.Instance.GetCard<CardBlock>(deviceCard.DeviceKey);
            return blockCard != null && blockCard.BlockId == 58;
        }

        private static void ActivateInstantCrateChainWindow()
        {
            instantCrateChainUntil = UnityEngine.Mathf.Max(instantCrateChainUntil, UnityEngine.Time.time + InstantCrateChainWindowSeconds);
        }

        private static bool IsInstantCrateChainWindowActive()
        {
            return Enabled && UnityEngine.Time.time < instantCrateChainUntil;
        }

        public static bool ShouldBypassBuildValidate(ToolLogicBuild tool)
        {
            return tool != null && tool.Unit != null && tool.Unit.IsMyPlayer && tool.Unit.CurrentDevice != null &&
                   (IsInstantPlacementDeviceKey(tool.Unit.CurrentDevice.DeviceKey) ||
                    (IsCratePlacementDeviceKey(tool.Unit.CurrentDevice.DeviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static bool ShouldZeroBuildTime(Unit unit, Key deviceKey)
        {
            return unit != null && unit.IsMyPlayer &&
                   (IsInstantPlacementDeviceKey(deviceKey) ||
                    (IsCratePlacementDeviceKey(deviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static bool ShouldZeroBuildTiming()
        {
            Unit player = Singleton<UnitsRegistry>.Instance != null ? Singleton<UnitsRegistry>.Instance.GetPlayer() : null;
            return player != null && player.IsMyPlayer && player.CurrentDevice != null &&
                   (IsInstantPlacementDeviceKey(player.CurrentDevice.DeviceKey) ||
                    (IsCratePlacementDeviceKey(player.CurrentDevice.DeviceKey) && IsInstantCrateChainWindowActive()));
        }

        public static float GetBuildPrecastTime(ToolTiming timing)
        {
            return ShouldZeroBuildTiming() ? 0f : ToolTimingHelper.GetPrecastTime(timing);
        }

        public static float GetBuildTotalCastTime(ToolTiming timing)
        {
            return ShouldZeroBuildTiming() ? 0f : ToolTimingHelper.GetTotalCastTime(timing);
        }

        public static bool ShouldSkipBuildCompletionWait()
        {
            return false;
        }

        // Maps RPC ID → block position so OnStartBuildResult can roll back the right prediction.
        private static readonly System.Collections.Generic.Dictionary<ushort, Vector3s> pendingRpcBlockPos
            = new System.Collections.Generic.Dictionary<ushort, Vector3s>();

        public static void TryInstantAcceptStartBuild(BuildInfo info, ServiceZone.Rpc_StartBuild rpc)
        {
            if (info == null || rpc == null) return;
            if (IsInstantPlacementDeviceKey(info.DeviceKey) ||
                (IsCratePlacementDeviceKey(info.DeviceKey) && IsInstantCrateChainWindowActive()))
            {
                if (IsCratePlacementDeviceKey(info.DeviceKey)) ActivateInstantCrateChainWindow();
                // Record which block position this RPC corresponds to before optimistically accepting.
                pendingRpcBlockPos[rpc._Id] = info.BuildInsidePosition;
                rpc._Success(true);
            }
        }

        public static void OnStartBuildResult(ServiceZone.Rpc_StartBuild rpc, bool accepted)
        {
            Vector3s blockPos;
            if (!pendingRpcBlockPos.TryGetValue(rpc._Id, out blockPos)) return;
            pendingRpcBlockPos.Remove(rpc._Id);
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (accepted)
                // Server accepted the build — resolve immediately so the prediction doesn't
                // time out and roll back a block the server confirmed.
                predictionManager.ResolveBlock(blockPos);
            else
                predictionManager.RollbackBlock(blockPos);
        }

        public static void TryInstantAcceptSwitchGear(Unit unit, ServiceZone.Rpc_SwitchGear rpc)
        {
            if (!Enabled || unit == null || rpc == null) return;
            if (unit.IsMyPlayer) rpc._Success(true);
        }

        public static void OnLocalPlace(BuildGhostController controller)
        {
            if (!Enabled || controller == null) return;
            try
            {
                BuildHelper.BuildData buildData = controller.TryPlaceDevice();
                if (buildData == null || buildData.Result != BuildHelper.BuildResultType.Success || buildData.Ri == null) return;
                Unit unit = controller.GetComponent<Unit>();
                if (unit == null || unit.CurrentDevice == null) return;
                PredictionManager predictionManager = Manager;
                if (predictionManager == null) return;
                RaycastInfo ri = buildData.Ri.Value;
                CardDevice deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(unit.CurrentDevice.DeviceKey);
                if (deviceCard == null) return;
                Card objectCard = Singleton<Catalogue>.Instance.GetCard<Card>(deviceCard.DeviceKey);
                if (objectCard == null) return;
                CardBlock blockCard = objectCard as CardBlock;
                // Don't create a local preview for bounce/speed pads — orientation is server-determined
                // and optimistic placement renders them wrong.
                if (blockCard != null && (blockCard.Special is BlockSpecialBounce || blockCard.Special is BlockSpecialFastMovement)) return;
                BuildGhostObject preview = BuildGhostObject.Create(unit.CurrentDevice.DeviceKey, false, unit.Team);
                PredictionEntry entry = new PredictionEntry
                {
                    DeviceKey = unit.CurrentDevice.DeviceKey,
                    SpawnCardKey = deviceCard.DeviceKey,
                    BlockPos = ri.BlockPosBuildIn,
                    WorldPos = preview.transform.position,
                    PreviousBlock = Singleton<ZoneManager>.Instance.Map.Blocks[ri.BlockPosBuildIn],
                    IsUnit = objectCard.Category == CardCategory.Unit,
                    ExpireTime = UnityEngine.Time.time + UnityEngine.Mathf.Max(0.25f, PredictionTimeoutSeconds)
                };
                if (blockCard != null && blockCard.BlockId != 0)
                {
                    if (blockCard.BlockId == 58) ActivateInstantCrateChainWindow();
                    Block newBlock = new Block((ushort)blockCard.BlockId);
                    newBlock.Team = unit.Team;
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    updates[ri.BlockPosBuildIn] = newBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(updates);
                    entry.WorldPos = ri.BlockPosBuildIn.ToVector3();
                    entry.IsRealLocalBlock = true;
                    UnityEngine.Object.Destroy(preview.gameObject);
                    preview = null;
                }
                else
                {
                    preview.SetValid(true);
                    preview.SetBlockPosition(ri.BlockPosBuildIn, ri.BlockPosBuildOn, ri.Direction);
                    preview.SetVisible(true);
                    preview.SetValue(1f);
                    entry.PreviewObject = preview.gameObject;
                    entry.WorldPos = preview.transform.position;
                }
                predictionManager.AddPrediction(entry);
            }
            catch (System.Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        public static void OnBlockUpdates(System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates)
        {
            if (!Enabled || updates == null) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            foreach (System.Collections.Generic.KeyValuePair<Vector3s, BlockUpdate> pair in updates)
                predictionManager.ResolveBlock(pair.Key);
        }

        public static void OnDeviceBuilt(uint builderPlayerId, Key deviceKey, UnityEngine.Vector3 position)
        {
            if (!Enabled) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (builderPlayerId != Singleton<PlayerData>.Instance.Id) return;
            predictionManager.ResolveDevice(deviceKey, position);
        }

        public static void OnUnitCreate(UnitInit data)
        {
            if (!Enabled || data == null || data.OwnerId == null) return;
            PredictionManager predictionManager = Manager;
            if (predictionManager == null) return;
            if (data.OwnerId.Value != Singleton<PlayerData>.Instance.Id) return;
            predictionManager.ResolveUnit(data.Key, data.Transform.GetPosition());
        }
    }

    public sealed class PredictionEntry
    {
        public Key DeviceKey;
        public Key SpawnCardKey;
        public Vector3s BlockPos;
        public UnityEngine.Vector3 WorldPos;
        public Block PreviousBlock;
        public bool IsUnit;
        public bool IsRealLocalBlock;
        public UnityEngine.GameObject PreviewObject;
        public float ExpireTime;
    }

    public sealed class PredictionManager : UnityEngine.MonoBehaviour
    {
        private readonly System.Collections.Generic.List<PredictionEntry> entries = new System.Collections.Generic.List<PredictionEntry>();
        // Rollbacks deferred because ZoneManager wasn't ready at removal time.
        private readonly System.Collections.Generic.List<PredictionEntry> pendingRollbacks = new System.Collections.Generic.List<PredictionEntry>();

        public void AddPrediction(PredictionEntry entry)
        {
            if (entry == null) return;
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                PredictionEntry current = this.entries[i];
                bool sameBlock = !current.IsUnit && !entry.IsUnit && current.BlockPos.Equals(entry.BlockPos);
                bool sameUnitSpot = current.IsUnit == entry.IsUnit && current.DeviceKey.Equals(entry.DeviceKey) && UnityEngine.Vector3.Distance(current.WorldPos, entry.WorldPos) <= 0.75f;
                if (sameBlock || sameUnitSpot)
                {
                    // If we're overwriting a prediction for the same block/unit,
                    // pass along the original world state (PreviousBlock) so
                    // we don't accidentally "resolve" to a predicted crate.
                    if (entry.PreviousBlock == null || entry.PreviousBlock.Id == 0)
                    {
                        entry.PreviousBlock = current.PreviousBlock;
                    }
                    this.RemoveAt(i, false);
                }
            }
            this.entries.Add(entry);
        }

        public void ResolveBlock(Vector3s blockPos)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (!this.entries[i].IsUnit && this.entries[i].BlockPos.Equals(blockPos))
                    this.RemoveAt(i, false);
        }

        // Server explicitly rejected the build — roll back the local block.
        public void RollbackBlock(Vector3s blockPos)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (!this.entries[i].IsUnit && this.entries[i].BlockPos.Equals(blockPos))
                    this.RemoveAt(i, true);
        }

        public void ResolveDevice(Key deviceKey, UnityEngine.Vector3 position)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (this.entries[i].DeviceKey.Equals(deviceKey) && UnityEngine.Vector3.Distance(this.entries[i].WorldPos, position) <= 1.5f)
                    this.RemoveAt(i, false);
        }

        public void ResolveUnit(Key spawnCardKey, UnityEngine.Vector3 position)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (this.entries[i].IsUnit && this.entries[i].SpawnCardKey.Equals(spawnCardKey) && UnityEngine.Vector3.Distance(this.entries[i].WorldPos, position) <= 1.5f)
                    this.RemoveAt(i, false);
        }

        private void Update()
        {
            // Retry any rollbacks that were deferred because ZoneManager wasn't ready.
            if (this.pendingRollbacks.Count > 0 &&
                Singleton<ZoneManager>.Instance != null && Singleton<ZoneManager>.Instance.MapCreated)
            {
                for (int i = this.pendingRollbacks.Count - 1; i >= 0; i--)
                {
                    PredictionEntry rb = this.pendingRollbacks[i];
                    this.pendingRollbacks.RemoveAt(i);
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> rbUpdates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    rbUpdates[rb.BlockPos] = rb.PreviousBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(rbUpdates);
                }
            }

            float now = UnityEngine.Time.time;
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                PredictionEntry entry = this.entries[i];
                if (entry == null) { this.RemoveAt(i, false); continue; }
                if (!entry.IsRealLocalBlock && entry.PreviewObject == null) { this.RemoveAt(i, false); continue; }
                if (now >= entry.ExpireTime) this.RemoveAt(i, true);
            }
        }

        private void RemoveAt(int index, bool rollbackRealLocalBlock)
        {
            PredictionEntry entry = this.entries[index];
            this.entries.RemoveAt(index);
            if (rollbackRealLocalBlock && entry != null && entry.IsRealLocalBlock)
            {
                if (Singleton<ZoneManager>.Instance != null && Singleton<ZoneManager>.Instance.MapCreated)
                {
                    System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                    updates[entry.BlockPos] = entry.PreviousBlock.ToUpdate();
                    Singleton<ZoneManager>.Instance.UpdateBlocks(updates);
                }
                else
                {
                    // ZoneManager not ready — defer rollback to next Update tick.
                    this.pendingRollbacks.Add(entry);
                }
            }
            if (entry != null && entry.PreviewObject != null)
                UnityEngine.Object.Destroy(entry.PreviewObject);
        }
    }
}
"@
}

if ($AutoCasualQueueConfig.enabled) {
$HelperSource += @"

namespace BnlCommunityFixes
{
    public sealed class AutoCasualQueueRuntime : UnityEngine.MonoBehaviour
    {
        private static AutoCasualQueueRuntime instance;
        private bool wasInCustomGame;
        private bool leaveRequestedForMatch;
        private MatchmakerStateType lastLoggedState = MatchmakerStateType.None;

        static AutoCasualQueueRuntime()
        {
            RuntimeFeatureState.ConfigureAutoCasualQueue(true, $(Format-BoolLiteral $AutoCasualQueueConfig.enabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void EnsureInstance()
        {
            if (instance != null) return;
            UnityEngine.GameObject go = UnityEngine.GameObject.Find("BNL_AUTO_CASUAL_QUEUE");
            if (go == null) { go = new UnityEngine.GameObject("BNL_AUTO_CASUAL_QUEUE"); UnityEngine.Object.DontDestroyOnLoad(go); }
            instance = go.GetComponent<AutoCasualQueueRuntime>();
            if (instance == null) instance = go.AddComponent<AutoCasualQueueRuntime>();
        }

        private void Update()
        {
            if (!RuntimeFeatureState.AutoCasualQueueEnabled) { wasInCustomGame = false; leaveRequestedForMatch = false; lastLoggedState = MatchmakerStateType.None; return; }
            try
            {
                CustomGameData customGameData = Singleton<CustomGameData>.Instance;
                MatchmakerData matchmakerData = Singleton<MatchmakerData>.Instance;
                NetworkDispatcher dispatcher = Singleton<NetworkDispatcher>.Instance;
                if (customGameData == null || matchmakerData == null || dispatcher == null) return;

                bool isInCustomGame = customGameData.IsCustomGame;
                MatchmakerStateType currentState = matchmakerData.State != null ? matchmakerData.State.State : MatchmakerStateType.None;

                if (currentState != lastLoggedState)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: matchmaker state=" + currentState + " inCustom=" + isInCustomGame);
                    lastLoggedState = currentState;
                }

                if (currentState != MatchmakerStateType.Confirming)
                {
                    leaveRequestedForMatch = false;
                }

                if (isInCustomGame && !wasInCustomGame)
                {
                    if (currentState == MatchmakerStateType.None)
                    {
                        UnityEngine.Debug.Log("BNL auto casual queue: entering casual queue from custom game");
                        dispatcher.ServiceMatchmaker.EnterQueue(CatalogueHelper.ModeFriendly.Key);
                    }
                }

                wasInCustomGame = isInCustomGame;

                if (isInCustomGame && currentState == MatchmakerStateType.Confirming && !leaveRequestedForMatch)
                {
                    UnityEngine.Debug.Log("BNL auto casual queue: leaving custom game after match found");
                    ZoneData zoneData = Singleton<ZoneData>.Instance;
                    if (zoneData != null && zoneData.IsCustomGame)
                    {
                        dispatcher.ServiceZone.ExitMatch();
                    }
                    else
                    {
                        customGameData.LeaveGame();
                    }
                    leaveRequestedForMatch = true;
                }
            }
            catch { }
        }
    }
}
"@
}

if ($TeammateHpEnabled) {
$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class TeammateHpRuntime
    {
        static TeammateHpRuntime()
        {
            RuntimeFeatureState.ConfigureTeammateHp(true, $(Format-BoolLiteral $TeammateHpEnabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        public static void UpdateTeammateHpText(GuiTeammate gui)
        {
            if (gui == null) return;
            if (!RuntimeFeatureState.TeammateHpEnabled) return;
            try
            {
                Unit unit = Singleton<UnitsRegistry>.Instance.GetByPlayerId(gui.PlayerId);
                if (unit == null || unit.IsDeath) return;
                float health = unit.Health;
                float maxHealth = unit.MaxHealth;
                if (maxHealth <= 0f) return;
                int pct = Mathf.RoundToInt((health / maxHealth) * 100f);
                string hpText = pct + "%";
                if (gui.PlayerName != null)
                    gui.PlayerName.text = Singleton<ZonePlayersCache>.Instance.GetPlayerName(gui.PlayerId) + " " + hpText;
                if (gui.RespawnTime != null)
                    gui.RespawnTime.text = hpText;
            }
            catch { }
        }
    }
}
"@
}

if ($AutoCrouchEnabled) {
$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class AutoCrouchRuntime
    {
        private static bool configured;

        static AutoCrouchRuntime()
        {
            EnsureConfigured();
        }

        public static void EnsureConfigured()
        {
            if (configured) return;
            configured = true;
            RuntimeFeatureState.ConfigureAutoCrouchDisable(true, $(Format-BoolLiteral $AutoCrouchEnabled));
            RuntimeSettingsMenuManager.EnsureInstance();
            TextureReplacementBootstrapper.EnsureInstance();
        }

        // Returns true when auto-crouch should be suppressed (i.e. "ceiling check passes").
        // Only called from PlayerMovementGroundMove.Update to replace the IsPossibleToStay call
        // in the auto-crouch condition. Voluntary crouch/stand logic is unaffected.
        public static bool IsPossibleToStayForAutoCrouch(MovementController controller)
        {
            EnsureConfigured();
            if (RuntimeFeatureState.AutoCrouchDisabled) return true;
            return controller.IsPossibleToStay();
        }
    }
}
"@
}

if (Test-Path $HelperOutputPath) {
    Remove-Item -LiteralPath $HelperOutputPath -Force
}

# Use csc.exe (net4 compiler) so that Unity/Mono references resolve correctly.
# Add-Type in PowerShell 7 uses Roslyn targeting .NET 8 which conflicts with mscorlib 2.0.
$CscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    throw "csc.exe not found at $CscPath. Please install .NET Framework 4."
}
$TempCsPath = [System.IO.Path]::ChangeExtension($HelperOutputPath, ".tmp.cs")
[System.IO.File]::WriteAllText($TempCsPath, $HelperSource, [System.Text.UTF8Encoding]::new($false))
$CscArgs = @(
    "/noconfig",
    "/nostdlib",
    "/target:library",
    "/utf8output",
    "/out:$HelperOutputPath",
    "/reference:$UnityEngineDll",
    "/reference:$UnityEngineUiDll",
    "/reference:$BackupPath",
    "/reference:C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Core.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.IO.Compression.dll",
    $TempCsPath
)
$cscResult = & $CscPath @CscArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $TempCsPath -Force -ErrorAction SilentlyContinue
    throw "csc.exe compilation failed:`n$($cscResult -join "`n")"
}
Remove-Item -LiteralPath $TempCsPath -Force -ErrorAction SilentlyContinue
[void][System.Reflection.Assembly]::LoadFrom($UnityEngineDll)

Copy-Item -LiteralPath $BackupPath -Destination $TempBasePath -Force

function Apply-Bytes {
    param([string]$Path,[int]$Offset,[byte[]]$Bytes)
    $Stream = [System.IO.File]::Open($Path,[System.IO.FileMode]::Open,[System.IO.FileAccess]::ReadWrite,[System.IO.FileShare]::Read)
    try {
        $Stream.Position = $Offset
        $Stream.Write($Bytes,0,$Bytes.Length)
    } finally {
        $Stream.Dispose()
    }
}

Apply-Bytes -Path $TempBasePath -Offset 0x00061f49 -Bytes ([byte[]](0x00,0x00,0x00,0x00,0x17))
Apply-Bytes -Path $TempBasePath -Offset 0x0015eb80 -Bytes ([byte[]](0x3A))
Apply-Bytes -Path $TempBasePath -Offset 0x0015B585 -Bytes ([byte[]](0x16))

$Resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$Resolver.AddSearchDirectory($ManagedDir)
$Resolver.AddSearchDirectory($PSScriptRoot)
$ReaderParameters = New-Object Mono.Cecil.ReaderParameters
$ReaderParameters.AssemblyResolver = $Resolver
$Assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($TempBasePath, $ReaderParameters)
$Module = $Assembly.MainModule
$HelperAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($HelperOutputPath, $ReaderParameters)
$MatchReplayRecorderRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.MatchReplayRecorderRuntime" | Select-Object -First 1
$ImportedConfigureMatchReplayRecorder = $null
$ImportedRecordMatchReplayPacket = $null
$ImportedRecordLocalCast = $null
$ImportedRecordLocalProjectileInfo = $null
$ImportedRecordLocalProjectileMove = $null
$ImportedRecordLocalProjectileDrop = $null
$ImportedRecordLocalUnitMove = $null
$ImportedRecordLocalUnitProjectileHit = $null
if ($MatchReplayRecorderRuntimeType) {
    $ConfigureMatchReplayRecorderMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "Configure" -and $_.Parameters.Count -eq 6 } | Select-Object -First 1
    $RecordMatchReplayPacketMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordPacket" -and $_.Parameters.Count -eq 2 } | Select-Object -First 1
    $RecordLocalCastMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalCast" -and $_.Parameters.Count -eq 2 } | Select-Object -First 1
    $RecordLocalProjectileInfoMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalProjectileInfo" -and $_.Parameters.Count -eq 3 } | Select-Object -First 1
    $RecordLocalProjectileMoveMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalProjectileMove" -and $_.Parameters.Count -eq 4 } | Select-Object -First 1
    $RecordLocalProjectileDropMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalProjectileDrop" -and $_.Parameters.Count -eq 2 } | Select-Object -First 1
    $RecordLocalUnitMoveMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalUnitMove" -and $_.Parameters.Count -eq 4 } | Select-Object -First 1
    $RecordLocalUnitProjectileHitMethod = $MatchReplayRecorderRuntimeType.Methods | Where-Object { $_.Name -eq "RecordLocalUnitProjectileHit" -and $_.Parameters.Count -eq 3 } | Select-Object -First 1
    if ($ConfigureMatchReplayRecorderMethod) {
        $ImportedConfigureMatchReplayRecorder = $Module.ImportReference($ConfigureMatchReplayRecorderMethod)
    }
    if ($RecordMatchReplayPacketMethod) {
        $ImportedRecordMatchReplayPacket = $Module.ImportReference($RecordMatchReplayPacketMethod)
    }
    if ($RecordLocalCastMethod) {
        $ImportedRecordLocalCast = $Module.ImportReference($RecordLocalCastMethod)
    }
    if ($RecordLocalProjectileInfoMethod) {
        $ImportedRecordLocalProjectileInfo = $Module.ImportReference($RecordLocalProjectileInfoMethod)
    }
    if ($RecordLocalProjectileMoveMethod) {
        $ImportedRecordLocalProjectileMove = $Module.ImportReference($RecordLocalProjectileMoveMethod)
    }
    if ($RecordLocalProjectileDropMethod) {
        $ImportedRecordLocalProjectileDrop = $Module.ImportReference($RecordLocalProjectileDropMethod)
    }
    if ($RecordLocalUnitMoveMethod) {
        $ImportedRecordLocalUnitMove = $Module.ImportReference($RecordLocalUnitMoveMethod)
    }
    if ($RecordLocalUnitProjectileHitMethod) {
        $ImportedRecordLocalUnitProjectileHit = $Module.ImportReference($RecordLocalUnitProjectileHitMethod)
    }
}
$DebugMenuRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.DebugMenuRuntime" | Select-Object -First 1
$ImportedConfigureDebugMenu = $null
$ImportedEnsureDebugMenu = $null
if ($DebugMenuRuntimeType) {
    $ConfigureDebugMenuMethod = $DebugMenuRuntimeType.Methods | Where-Object { $_.Name -eq "Configure" -and $_.Parameters.Count -eq 5 } | Select-Object -First 1
    $EnsureDebugMenuMethod = $DebugMenuRuntimeType.Methods | Where-Object Name -eq "EnsureInstance" | Select-Object -First 1
    if ($ConfigureDebugMenuMethod) {
        $ImportedConfigureDebugMenu = $Module.ImportReference($ConfigureDebugMenuMethod)
    }
    if ($EnsureDebugMenuMethod) {
        $ImportedEnsureDebugMenu = $Module.ImportReference($EnsureDebugMenuMethod)
    }
}
$RuntimeMenuType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.RuntimeSettingsMenuManager" | Select-Object -First 1
$ImportedEnsureRuntimeMenu = $null
if ($RuntimeMenuType) {
    $EnsureRuntimeMenuMethod = $RuntimeMenuType.Methods | Where-Object Name -eq "EnsureInstance" | Select-Object -First 1
    if ($EnsureRuntimeMenuMethod) {
        $ImportedEnsureRuntimeMenu = $Module.ImportReference($EnsureRuntimeMenuMethod)
    }
}
$ReplayPlayerRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.ReplayPlayerRuntime" | Select-Object -First 1
$ImportedEnsureReplayPlayer = $null
if ($ReplayPlayerRuntimeType) {
    $EnsureReplayPlayerMethod = $ReplayPlayerRuntimeType.Methods | Where-Object Name -eq "EnsureInstance" | Select-Object -First 1
    if ($EnsureReplayPlayerMethod) {
        $ImportedEnsureReplayPlayer = $Module.ImportReference($EnsureReplayPlayerMethod)
    }
}
$FontRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.FontRuntime" | Select-Object -First 1
$ApplyAllCanvasesMethod = $FontRuntimeType.Methods | Where-Object Name -eq "ApplyAllCanvases" | Select-Object -First 1
$ApplyToTextMethod = $FontRuntimeType.Methods | Where-Object { $_.Name -eq "ApplyToText" -and $_.Parameters.Count -eq 1 } | Select-Object -First 1
$ImportedApplyAllCanvases = $Module.ImportReference($ApplyAllCanvasesMethod)
$ImportedApplyToText = $Module.ImportReference($ApplyToTextMethod)
$CrosshairRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.CrosshairRuntime" | Select-Object -First 1
$ApplyCrosshairHardHideMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ApplyHardHide" | Select-Object -First 1
$ApplyCrosshairVisibilityMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ApplyVisibility" | Select-Object -First 1
$ApplyCrosshairControllerMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ApplyController" | Select-Object -First 1
$ApplyCrosshairBlankMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ApplyBlank" | Select-Object -First 1
$ScaleCrosshairAngleMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ScaleAngle" | Select-Object -First 1
$ScaleCrosshairSizeVectorMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "ScaleSizeVector" | Select-Object -First 1
$GetCrosshairPrefabMethod = $CrosshairRuntimeType.Methods | Where-Object Name -eq "GetAppropriateCrosshairPrefab" | Select-Object -First 1
$ImportedApplyCrosshairHardHideMethod = $Module.ImportReference($ApplyCrosshairHardHideMethod)
$ImportedApplyCrosshairVisibilityMethod = $Module.ImportReference($ApplyCrosshairVisibilityMethod)
$ImportedApplyCrosshairControllerMethod = $Module.ImportReference($ApplyCrosshairControllerMethod)
$ImportedApplyCrosshairBlankMethod = $Module.ImportReference($ApplyCrosshairBlankMethod)
$ImportedScaleCrosshairAngleMethod = $Module.ImportReference($ScaleCrosshairAngleMethod)
$ImportedScaleCrosshairSizeVectorMethod = $Module.ImportReference($ScaleCrosshairSizeVectorMethod)
$ImportedGetCrosshairPrefabMethod = $Module.ImportReference($GetCrosshairPrefabMethod)
$AppendAllText = $Module.ImportReference(([System.IO.File].GetMethod("AppendAllText", [Type[]]@([string], [string]))))
$StringConcat2 = $Module.ImportReference(([string].GetMethod("Concat", [Type[]]@([string], [string]))))
$LockOnRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.LockOnRuntime" | Select-Object -First 1
$LockOnAttachMethod = $null
$ImportedLockOnAttachMethod = $null
if ($LockOnRuntimeType) {
    $LockOnAttachMethod = $LockOnRuntimeType.Methods | Where-Object Name -eq "AttachTo" | Select-Object -First 1
    if ($LockOnAttachMethod) {
        $ImportedLockOnAttachMethod = $Module.ImportReference($LockOnAttachMethod)
    }
}
$TrackingRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.TrackingProjectileRuntime" | Select-Object -First 1
$TrackingAttachMethod = $null
$TrackingAttachUnitMethod = $null
$ImportedTrackingAttachMethod = $null
$ImportedTrackingAttachUnitMethod = $null
if ($TrackingRuntimeType) {
    $TrackingAttachMethod = $TrackingRuntimeType.Methods | Where-Object Name -eq "AttachTo" | Select-Object -First 1
    $TrackingAttachUnitMethod = $TrackingRuntimeType.Methods | Where-Object Name -eq "AttachToUnitProjectile" | Select-Object -First 1
    if ($TrackingAttachMethod) {
        $ImportedTrackingAttachMethod = $Module.ImportReference($TrackingAttachMethod)
    }
    if ($TrackingAttachUnitMethod) {
        $ImportedTrackingAttachUnitMethod = $Module.ImportReference($TrackingAttachUnitMethod)
    }
}
$AdsRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.AdsSensitivityRuntime" | Select-Object -First 1
$AdsScaleMethod = $null
$ImportedAdsScaleMethod = $null
$ImportedApplyCameraFov = $null
$ImportedApplyWeaponModelFov = $null
if ($AdsRuntimeType) {
    $AdsScaleMethod = $AdsRuntimeType.Methods | Where-Object Name -eq "ApplyAdsScale" | Select-Object -First 1
    if ($AdsScaleMethod) {
        $ImportedAdsScaleMethod = $Module.ImportReference($AdsScaleMethod)
    }
    $ApplyCameraFovMethod = $AdsRuntimeType.Methods | Where-Object Name -eq "ApplyCameraFov" | Select-Object -First 1
    if ($ApplyCameraFovMethod) {
        $ImportedApplyCameraFov = $Module.ImportReference($ApplyCameraFovMethod)
    }
    $ApplyWeaponModelFovMethod = $AdsRuntimeType.Methods | Where-Object Name -eq "ApplyWeaponModelFov" | Select-Object -First 1
    if ($ApplyWeaponModelFovMethod) {
        $ImportedApplyWeaponModelFov = $Module.ImportReference($ApplyWeaponModelFovMethod)
    }
}
$CombatNumberRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.CombatNumberRuntime" | Select-Object -First 1
if (-not $CombatNumberRuntimeType) { throw "CombatNumberRuntime type not found in helper assembly." }

$ApplyDamageNumberMethod = $CombatNumberRuntimeType.Methods | Where-Object Name -eq "ApplyDamageNumber" | Select-Object -First 1
if (-not $ApplyDamageNumberMethod) { throw "CombatNumberRuntime.ApplyDamageNumber method not found." }
$ApplyDamageNumber = $Module.ImportReference($ApplyDamageNumberMethod)

$GetDamageCollectTimeMethod = $CombatNumberRuntimeType.Methods | Where-Object Name -eq "GetDamageCollectTime" | Select-Object -First 1
if (-not $GetDamageCollectTimeMethod) { throw "CombatNumberRuntime.GetDamageCollectTime method not found." }
$GetDamageCollectTime = $Module.ImportReference($GetDamageCollectTimeMethod)

$RefreshDamageNumberMethod = $CombatNumberRuntimeType.Methods | Where-Object Name -eq "RefreshDamageNumber" | Select-Object -First 1
if (-not $RefreshDamageNumberMethod) { throw "CombatNumberRuntime.RefreshDamageNumber method not found." }
$RefreshDamageNumber = $Module.ImportReference($RefreshDamageNumberMethod)

if ([bool]$MatchReplayRecorderConfig.enabled) {
    if (-not $ImportedConfigureMatchReplayRecorder -or -not $ImportedRecordMatchReplayPacket) {
        throw "MatchReplayRecorderRuntime helper methods not found."
    }

    $MainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if (-not $MainMenuType) { throw "MainMenu type not found." }
    $MainMenuStartMethod = $MainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $MainMenuStartMethod -or -not $MainMenuStartMethod.HasBody) { throw "MainMenu.Start not found." }

    $MainMenuStartIl = $MainMenuStartMethod.Body.GetILProcessor()
    $MainMenuStartFirst = $MainMenuStartMethod.Body.Instructions | Select-Object -First 1
    foreach ($instruction in @(
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $MatchReplayRecorderMaxPayloadBytes),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $(if ($MatchReplayRecorderCapturePayload) { 1 } else { 0 })),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $(if ($MatchReplayRecorderRecordCustomGames) { 1 } else { 0 })),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $(if ($MatchReplayRecorderRecordCasualGames) { 1 } else { 0 })),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $(if ($MatchReplayRecorderRecordRankedGames) { 1 } else { 0 })),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedConfigureMatchReplayRecorder)
    )) {
        $MainMenuStartIl.InsertBefore($MainMenuStartFirst, $instruction)
    }

    function Add-MatchReplayRecvRecorderHooks {
        param(
            [Parameter(Mandatory=$true)] [string] $ServiceFullName
        )

        $ServiceType = $Module.Types | Where-Object FullName -eq $ServiceFullName | Select-Object -First 1
        if (-not $ServiceType) { throw "$ServiceFullName type not found." }

        $PatchedMethods = 0
        foreach ($RecvMethod in ($ServiceType.Methods | Where-Object { $_.Name -like "Recv_*" -and $_.HasBody -and $_.Parameters.Count -ge 1 })) {
            $ReaderParameter = $RecvMethod.Parameters[0]
            if ($ReaderParameter.ParameterType.FullName -ne "System.IO.BinaryReader") {
                continue
            }

            $RecvIl = $RecvMethod.Body.GetILProcessor()
            $RecvFirst = $RecvMethod.Body.Instructions | Select-Object -First 1
            foreach ($instruction in @(
                $RecvIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $RecvMethod.Name),
                $RecvIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
                $RecvIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedRecordMatchReplayPacket)
            )) {
                $RecvIl.InsertBefore($RecvFirst, $instruction)
            }

            $PatchedMethods++
        }

        if ($PatchedMethods -eq 0) {
            throw "No $ServiceFullName Recv_* methods were patched for match replay recording."
        }
    }

    $ServiceZoneType = $Module.Types | Where-Object FullName -eq "Protocol.ServiceZone" | Select-Object -First 1
    if (-not $ServiceZoneType) { throw "Protocol.ServiceZone type not found." }

    Add-MatchReplayRecvRecorderHooks -ServiceFullName "Protocol.ServiceZone"
    Add-MatchReplayRecvRecorderHooks -ServiceFullName "Protocol.ServiceChat"

    if (-not $ImportedRecordLocalCast -or
        -not $ImportedRecordLocalProjectileInfo -or
        -not $ImportedRecordLocalProjectileMove -or
        -not $ImportedRecordLocalProjectileDrop -or
        -not $ImportedRecordLocalUnitMove -or
        -not $ImportedRecordLocalUnitProjectileHit) {
        throw "MatchReplayRecorderRuntime local projectile helper methods not found."
    }

    function Insert-ServiceZoneRecorderCall {
        param(
            [Parameter(Mandatory=$true)] [Mono.Cecil.TypeDefinition] $Type,
            [Parameter(Mandatory=$true)] [string] $MethodName,
            [Parameter(Mandatory=$true)] [Mono.Cecil.MethodReference] $RecorderMethod,
            [Parameter(Mandatory=$true)] [Mono.Cecil.Cil.Instruction[]] $ArgumentLoads
        )

        $Method = $Type.Methods | Where-Object { $_.Name -eq $MethodName -and $_.HasBody } | Select-Object -First 1
        if (-not $Method) {
            throw "ServiceZone.$MethodName not found for match replay recording."
        }

        $Il = $Method.Body.GetILProcessor()
        $First = $Method.Body.Instructions | Select-Object -First 1
        $Instructions = @($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "ServiceZone.$MethodName")) + $ArgumentLoads + @($Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $RecorderMethod))
        foreach ($Instruction in $Instructions) {
            $Il.InsertBefore($First, $Instruction)
        }
    }

    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "Cast" -RecorderMethod $ImportedRecordLocalCast -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
    )
    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "CreateProjectile" -RecorderMethod $ImportedRecordLocalProjectileInfo -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2))
    )
    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "MoveProjectile" -RecorderMethod $ImportedRecordLocalProjectileMove -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_3))
    )
    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "DropProjectile" -RecorderMethod $ImportedRecordLocalProjectileDrop -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
    )
    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "UnitMove" -RecorderMethod $ImportedRecordLocalUnitMove -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_3))
    )
    Insert-ServiceZoneRecorderCall -Type $ServiceZoneType -MethodName "UnitProjectileHit" -RecorderMethod $ImportedRecordLocalUnitProjectileHit -ArgumentLoads @(
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1)),
        ([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2))
    )
}

if ([bool]$DebugMenuConfig.enabled) {
    if (-not $ImportedConfigureDebugMenu -or -not $ImportedEnsureDebugMenu) {
        throw "DebugMenuRuntime helper methods not found."
    }

    $MainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if (-not $MainMenuType) { throw "MainMenu type not found." }
    $MainMenuStartMethod = $MainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $MainMenuStartMethod -or -not $MainMenuStartMethod.HasBody) { throw "MainMenu.Start not found." }

    $MainMenuStartIl = $MainMenuStartMethod.Body.GetILProcessor()
    $MainMenuStartFirst = $MainMenuStartMethod.Body.Instructions | Select-Object -First 1
    foreach ($instruction in @(
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $DebugMenuKeyLiteral),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $DebugMainMenuKeyLiteral),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $DebugLobbyMenuKeyLiteral),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $DebugZoneMenuKeyLiteral),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedConfigureDebugMenu),
        $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureDebugMenu)
    )) {
        $MainMenuStartIl.InsertBefore($MainMenuStartFirst, $instruction)
    }
}

if ($ImportedEnsureRuntimeMenu) {
    $MainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if (-not $MainMenuType) { throw "MainMenu type not found." }
    $MainMenuStartMethod = $MainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $MainMenuStartMethod -or -not $MainMenuStartMethod.HasBody) { throw "MainMenu.Start not found." }

    $MainMenuStartIl = $MainMenuStartMethod.Body.GetILProcessor()
    $MainMenuStartFirst = $MainMenuStartMethod.Body.Instructions | Select-Object -First 1
    $MainMenuStartIl.InsertBefore($MainMenuStartFirst, $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureRuntimeMenu))
}

if ($AutoCrouchEnabled) {
    $AutoCrouchRuntimeTypeForMenu = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.AutoCrouchRuntime" | Select-Object -First 1
    if (-not $AutoCrouchRuntimeTypeForMenu) { throw "AutoCrouchRuntime type not found in helper assembly." }
    $EnsureAutoCrouchConfiguredMethod = $AutoCrouchRuntimeTypeForMenu.Methods | Where-Object Name -eq "EnsureConfigured" | Select-Object -First 1
    if (-not $EnsureAutoCrouchConfiguredMethod) { throw "AutoCrouchRuntime.EnsureConfigured not found." }
    $ImportedEnsureAutoCrouchConfigured = $Module.ImportReference($EnsureAutoCrouchConfiguredMethod)

    $MainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if (-not $MainMenuType) { throw "MainMenu type not found." }
    $MainMenuStartMethod = $MainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $MainMenuStartMethod -or -not $MainMenuStartMethod.HasBody) { throw "MainMenu.Start not found." }

    $MainMenuStartIl = $MainMenuStartMethod.Body.GetILProcessor()
    $MainMenuStartFirst = $MainMenuStartMethod.Body.Instructions | Select-Object -First 1
    $MainMenuStartIl.InsertBefore($MainMenuStartFirst, $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureAutoCrouchConfigured))
}

if ($ImportedEnsureReplayPlayer) {
    $MainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if (-not $MainMenuType) { throw "MainMenu type not found." }
    $MainMenuStartMethod = $MainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $MainMenuStartMethod -or -not $MainMenuStartMethod.HasBody) { throw "MainMenu.Start not found." }

    $MainMenuStartIl = $MainMenuStartMethod.Body.GetILProcessor()
    $MainMenuStartFirst = $MainMenuStartMethod.Body.Instructions | Select-Object -First 1
    $MainMenuStartIl.InsertBefore($MainMenuStartFirst, $MainMenuStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureReplayPlayer))
}

$AttachHealingMethod = $CombatNumberRuntimeType.Methods | Where-Object Name -eq "AttachHealing" | Select-Object -First 1
if (-not $AttachHealingMethod) { throw "CombatNumberRuntime.AttachHealing method not found." }
$AttachHealingIndicator = $Module.ImportReference($AttachHealingMethod)

$ShouldShowDamageNumberMethod = $CombatNumberRuntimeType.Methods | Where-Object Name -eq "ShouldShowDamageNumber" | Select-Object -First 1
if (-not $ShouldShowDamageNumberMethod) { throw "CombatNumberRuntime.ShouldShowDamageNumber method not found." }
$ShouldShowDamageNumber = $Module.ImportReference($ShouldShowDamageNumberMethod)

$HealAlertRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.HealAlertRuntime" | Select-Object -First 1
if (-not $HealAlertRuntimeType) { throw "HealAlertRuntime type not found in helper assembly." }

$ImportedHealAlertApplyDamageIndicatorMethod = $HealAlertRuntimeType.Methods | Where-Object Name -eq "ApplyDamageIndicator" | Select-Object -First 1
if (-not $ImportedHealAlertApplyDamageIndicatorMethod) { throw "HealAlertRuntime.ApplyDamageIndicator method not found." }
$ImportedHealAlertApplyDamageIndicator = $Module.ImportReference($ImportedHealAlertApplyDamageIndicatorMethod)

$ImportedHealAlertAttachBridgeMethod = $HealAlertRuntimeType.Methods | Where-Object Name -eq "AttachHealBridge" | Select-Object -First 1
if (-not $ImportedHealAlertAttachBridgeMethod) { throw "HealAlertRuntime.AttachHealBridge method not found." }
$ImportedHealAlertAttachBridge = $Module.ImportReference($ImportedHealAlertAttachBridgeMethod)
$ShieldBuffBarRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.ShieldBuffBarRuntime" | Select-Object -First 1
$ImportedShieldBuffBarAttach = $null
if ($ShieldBuffBarRuntimeType) {
    $ShieldBuffBarAttachMethod = $ShieldBuffBarRuntimeType.Methods | Where-Object Name -eq "AttachShieldBuffBar" | Select-Object -First 1
    if ($ShieldBuffBarAttachMethod) {
        $ImportedShieldBuffBarAttach = $Module.ImportReference($ShieldBuffBarAttachMethod)
    }
}
$FriendlyLowHealthRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.FriendlyLowHealthRuntime" | Select-Object -First 1
$ImportedFriendlyLowHealthAttach = $null
if ($FriendlyLowHealthRuntimeType) {
    $FriendlyLowHealthAttachMethod = $FriendlyLowHealthRuntimeType.Methods | Where-Object Name -eq "AttachFriendlyLowHealth" | Select-Object -First 1
    if ($FriendlyLowHealthAttachMethod) {
        $ImportedFriendlyLowHealthAttach = $Module.ImportReference($FriendlyLowHealthAttachMethod)
    }
}
function Insert-Before {
    param(
        [Mono.Cecil.Cil.ILProcessor]$Il,
        [Mono.Cecil.Cil.Instruction]$Target,
        [Mono.Cecil.Cil.Instruction[]]$Instructions
    )
    foreach ($Instruction in $Instructions) {
        $Il.InsertBefore($Target, $Instruction)
    }
}

function Insert-After {
    param(
        [Mono.Cecil.Cil.ILProcessor]$Il,
        [Mono.Cecil.Cil.Instruction]$Target,
        [Mono.Cecil.Cil.Instruction[]]$Instructions
    )

    $Current = $Target
    foreach ($Instruction in $Instructions) {
        $Il.InsertAfter($Current, $Instruction)
        $Current = $Instruction
    }
}

function Inject-BoolTrueBypass {
    param(
        [Mono.Cecil.MethodDefinition]$Method,
        [Mono.Cecil.MethodReference]$Call,
        [Mono.Cecil.Cil.OpCode]$LoadArg
    )
    if (-not $Method -or -not $Method.HasBody) { return }
    $Il = $Method.Body.GetILProcessor()
    $First = $Method.Body.Instructions[0]
    $Continue = $Il.Create([Mono.Cecil.Cil.OpCodes]::Nop)
    $Instructions = @(
        $Il.Create($LoadArg),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $Call),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $Continue),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ret),
        $Continue
    )
    Insert-Before -Il $Il -Target $First -Instructions $Instructions
}

function Inject-FloatZeroBypass {
    param(
        [Mono.Cecil.MethodDefinition]$Method,
        [Mono.Cecil.Cil.Instruction[]]$PrefixInstructions
    )
    if (-not $Method -or -not $Method.HasBody) { return }
    $Il = $Method.Body.GetILProcessor()
    $First = $Method.Body.Instructions[0]
    $Continue = $Il.Create([Mono.Cecil.Cil.OpCodes]::Nop)
    $Instructions = New-Object System.Collections.Generic.List[Mono.Cecil.Cil.Instruction]
    foreach ($Instruction in $PrefixInstructions) { $Instructions.Add($Instruction) }
    $Instructions.Add($Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $Continue))
    $Instructions.Add($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]0))
    $Instructions.Add($Il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $Instructions.Add($Continue)
    Insert-Before -Il $Il -Target $First -Instructions $Instructions.ToArray()
}

function Inject-ImmediateReturnAfterInstruction {
    param(
        [Mono.Cecil.MethodDefinition]$Method,
        [Mono.Cecil.Cil.Instruction]$AfterInstruction,
        [Mono.Cecil.MethodReference]$ConditionCall,
        [Mono.Cecil.Cil.Instruction]$ReturnTarget
    )
    if (-not $Method -or -not $Method.HasBody -or -not $AfterInstruction -or -not $ReturnTarget) { return }
    $Il = $Method.Body.GetILProcessor()
    Insert-After -Il $Il -Target $AfterInstruction -Instructions @(
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ConditionCall),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Brtrue, $ReturnTarget)
    )
}

function Replace-MethodCalls {
    param(
        [Mono.Cecil.MethodDefinition]$Method,
        [string]$OriginalTypeName,
        [string]$OriginalMethodName,
        [Mono.Cecil.MethodReference]$ReplacementCall
    )
    if (-not $Method -or -not $Method.HasBody) { return }
    foreach ($Instruction in $Method.Body.Instructions) {
        if (($Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -or $Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Callvirt) -and
            $Instruction.Operand -is [Mono.Cecil.MethodReference] -and
            $Instruction.Operand.Name -eq $OriginalMethodName -and
            $Instruction.Operand.DeclaringType.Name -eq $OriginalTypeName) {
            $Instruction.Operand = $ReplacementCall
        }
    }
}

function Insert-CallAtStart {
    param(
        [Mono.Cecil.MethodDefinition]$Method,
        [Mono.Cecil.MethodReference]$Call
    )
    if (-not $Method -or -not $Method.HasBody -or -not $Call) { return }
    $Il = $Method.Body.GetILProcessor()
    $First = $Method.Body.Instructions[0]
    $Il.InsertBefore($First, $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $Call))
}

if ($DamageHealingConfig.enabled) {
$GuiDamageNumberDetector = $Module.Types | Where-Object Name -eq "GuiDamageNumberDetector" | Select-Object -First 1
$OnGlobalUnitDamageMethod = $GuiDamageNumberDetector.Methods | Where-Object Name -eq "OnGlobalUnitDamage" | Select-Object -First 1
$OnGlobalUnitDamageIl = $OnGlobalUnitDamageMethod.Body.GetILProcessor()
$OnGlobalUnitDamageFirst = $OnGlobalUnitDamageMethod.Body.Instructions[0]
$ContinueDamageNumber = $OnGlobalUnitDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Nop)
Insert-Before -Il $OnGlobalUnitDamageIl -Target $OnGlobalUnitDamageFirst -Instructions @(
    $OnGlobalUnitDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
    $OnGlobalUnitDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ShouldShowDamageNumber),
    $OnGlobalUnitDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Brtrue_S, $ContinueDamageNumber),
    $OnGlobalUnitDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ret),
    $ContinueDamageNumber
)

$UpdateDamage = $GuiDamageNumberDetector.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
$DamageIl = $UpdateDamage.Body.GetILProcessor()
$DamageInstructions = @($UpdateDamage.Body.Instructions)
$GuiDamageNumberType = $Module.Types | Where-Object Name -eq "GuiDamageNumber" | Select-Object -First 1
$GuiDamageNumberGetDamageValue = $GuiDamageNumberType.Methods | Where-Object Name -eq "get_DamageValue" | Select-Object -First 1
$CollectedDamageType = $GuiDamageNumberDetector.NestedTypes | Where-Object Name -eq "CollectedDamage" | Select-Object -First 1
$CollectedDamageIsCrit = $CollectedDamageType.Fields | Where-Object Name -eq "IsCrit" | Select-Object -First 1
$CollectedDamageUnit = $CollectedDamageType.Fields | Where-Object Name -eq "Unit" | Select-Object -First 1
$LastHitInfoType = $GuiDamageNumberDetector.NestedTypes | Where-Object Name -eq "LastHitInfo" | Select-Object -First 1
$LastHitHitField = $LastHitInfoType.Fields | Where-Object Name -eq "Hit" | Select-Object -First 1
$LastHitCollectingTimeField = $LastHitInfoType.Fields | Where-Object Name -eq "CollectingTime" | Select-Object -First 1
$UpdateClosureType = $GuiDamageNumberDetector.NestedTypes | Where-Object { $_.Name -like "*Update*c__AnonStorey*" } | Select-Object -First 1
$UpdateClosureDamageField = $UpdateClosureType.Fields | Where-Object Name -eq "d" | Select-Object -First 1
$UpdateClosureLocal = $UpdateDamage.Body.Variables | Where-Object { $_.VariableType.Name -like "*Update*c__AnonStorey*" } | Select-Object -First 1
# Find lastHitInfo2 (the Find result) by tracing back from the Stfld Hit assignment.
# There are two LastHitInfo locals: lastHitInfo (foreach var, lower index) and lastHitInfo2
# (Find result, higher index). We need the one stored just before "lastHitInfo2.Hit = guiDamageNumber2".
$LastHitHitAssign = $DamageInstructions | Where-Object {
    $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and $_.Operand -eq $LastHitHitField
} | Select-Object -First 1
$LastHitLocal = $null
if ($LastHitHitAssign -and $LastHitHitAssign.Previous -and $LastHitHitAssign.Previous.Previous) {
    $ldloc = $LastHitHitAssign.Previous.Previous
    switch ($ldloc.OpCode.Code) {
        ([Mono.Cecil.Cil.Code]::Ldloc_0) { $LastHitLocal = $UpdateDamage.Body.Variables[0] }
        ([Mono.Cecil.Cil.Code]::Ldloc_1) { $LastHitLocal = $UpdateDamage.Body.Variables[1] }
        ([Mono.Cecil.Cil.Code]::Ldloc_2) { $LastHitLocal = $UpdateDamage.Body.Variables[2] }
        ([Mono.Cecil.Cil.Code]::Ldloc_3) { $LastHitLocal = $UpdateDamage.Body.Variables[3] }
        ([Mono.Cecil.Cil.Code]::Ldloc_S) { $LastHitLocal = $ldloc.Operand }
        ([Mono.Cecil.Cil.Code]::Ldloc)   { $LastHitLocal = $ldloc.Operand }
    }
}
$CollectingTimeStore = $DamageInstructions | Where-Object {
    $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and $_.Operand -eq $LastHitCollectingTimeField
} | Select-Object -First 1
if ($CollectingTimeStore) {
    $DamageIl.InsertBefore($CollectingTimeStore, $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $GetDamageCollectTime))
}
$DamageValueSets = @($DamageInstructions | Where-Object {
    $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Callvirt -and $_.Operand -and $_.Operand.Name -eq "set_DamageValue"
})
function New-LdlocForVar {
    param([Mono.Cecil.Cil.ILProcessor]$Il, [Mono.Cecil.Cil.VariableDefinition]$Var)
    switch ($Var.Index) {
        0 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_0) }
        1 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_1) }
        2 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_2) }
        3 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_3) }
        default { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_S, $Var) }
    }
}
if ($DamageValueSets.Count -gt 0 -and $LastHitLocal -and $UpdateClosureLocal -and $UpdateClosureDamageField) {
    foreach ($DamageValueSet in $DamageValueSets) {
        Insert-After -Il $DamageIl -Target $DamageValueSet -Instructions @(
            (New-LdlocForVar -Il $DamageIl -Var $LastHitLocal),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
            (New-LdlocForVar -Il $DamageIl -Var $LastHitLocal),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $LastHitHitField),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_S, $UpdateClosureLocal),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $UpdateClosureDamageField),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $CollectedDamageUnit),
            (New-LdlocForVar -Il $DamageIl -Var $LastHitLocal),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $LastHitHitField),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Callvirt, $GuiDamageNumberGetDamageValue),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_S, $UpdateClosureLocal),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $UpdateClosureDamageField),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $CollectedDamageIsCrit),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $RefreshDamageNumber),
            $DamageIl.Create([Mono.Cecil.Cil.OpCodes]::Stfld, $LastHitHitField)
        )
    }
}

$StartMethod = $GuiDamageNumberDetector.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
$StartIl = $StartMethod.Body.GetILProcessor()
$StartRet = @($StartMethod.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
$StartIl.InsertBefore($StartRet, $StartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
$StartIl.InsertBefore($StartRet, $StartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $AttachHealingIndicator))


}

if ($HealAlertConfig.enabled) {
$GuiHitAlertMaker = $Module.Types | Where-Object Name -eq "GuiHitAlertMaker" | Select-Object -First 1

# Patch OnGlobalUnitDamage(): apply size/color to each spawned instance, not the template
$OnGlobalUnitDamageMethod = $GuiHitAlertMaker.Methods | Where-Object Name -eq "OnGlobalUnitDamage" | Select-Object -First 1
$OnDamageIl = $OnGlobalUnitDamageMethod.Body.GetILProcessor()
$OnDamageInstructions = @($OnGlobalUnitDamageMethod.Body.Instructions)
$HitAlertLocal     = $OnGlobalUnitDamageMethod.Body.Variables | Where-Object { $_.VariableType.Name -eq "GuiHitAlert" }             | Select-Object -First 1
$HitAlertEdgeLocal = $OnGlobalUnitDamageMethod.Body.Variables | Where-Object { $_.VariableType.Name -eq "GuiHitAlertOnScreenEdge" } | Select-Object -First 1

function Find-StlocForVar {
    param([Mono.Cecil.Cil.Instruction[]]$Instructions, [Mono.Cecil.Cil.VariableDefinition]$Var)
    $Idx = $Var.Index
    return $Instructions | Where-Object {
        $c = $_.OpCode.Code
        ($c -eq [Mono.Cecil.Cil.Code]::Stloc_0 -and $Idx -eq 0) -or
        ($c -eq [Mono.Cecil.Cil.Code]::Stloc_1 -and $Idx -eq 1) -or
        ($c -eq [Mono.Cecil.Cil.Code]::Stloc_2 -and $Idx -eq 2) -or
        ($c -eq [Mono.Cecil.Cil.Code]::Stloc_3 -and $Idx -eq 3) -or
        (($c -eq [Mono.Cecil.Cil.Code]::Stloc_S -or $c -eq [Mono.Cecil.Cil.Code]::Stloc) -and $_.Operand -eq $Var)
    } | Select-Object -First 1
}

function New-LdlocInstr {
    param([Mono.Cecil.Cil.ILProcessor]$Il, [Mono.Cecil.Cil.VariableDefinition]$Var)
    switch ($Var.Index) {
        0 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_0) }
        1 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_1) }
        2 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_2) }
        3 { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_3) }
        default { return $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_S, $Var) }
    }
}

$HitAlertStoreInstr     = Find-StlocForVar -Instructions $OnDamageInstructions -Var $HitAlertLocal
$HitAlertEdgeStoreInstr = Find-StlocForVar -Instructions $OnDamageInstructions -Var $HitAlertEdgeLocal

if ($HitAlertStoreInstr -and $HitAlertLocal) {
    Insert-After -Il $OnDamageIl -Target $HitAlertStoreInstr -Instructions @(
        (New-LdlocInstr -Il $OnDamageIl -Var $HitAlertLocal),
        $OnDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedHealAlertApplyDamageIndicator)
    )
}
if ($HitAlertEdgeStoreInstr -and $HitAlertEdgeLocal) {
    Insert-After -Il $OnDamageIl -Target $HitAlertEdgeStoreInstr -Instructions @(
        (New-LdlocInstr -Il $OnDamageIl -Var $HitAlertEdgeLocal),
        $OnDamageIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedHealAlertApplyDamageIndicator)
    )
}

# Patch Start(): attach the HealAlertBridge MonoBehaviour
$HitAlertStartMethod = $GuiHitAlertMaker.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
$HitAlertStartIl = $HitAlertStartMethod.Body.GetILProcessor()
$HitAlertStartRet = @($HitAlertStartMethod.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
$HitAlertStartIl.InsertBefore($HitAlertStartRet, $HitAlertStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
$HitAlertStartIl.InsertBefore($HitAlertStartRet, $HitAlertStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedHealAlertAttachBridge))

if ($ImportedShieldBuffBarAttach -or $ImportedFriendlyLowHealthAttach) {
    $GuiHealthbarType = $Module.Types | Where-Object Name -eq "GuiHealthbar" | Select-Object -First 1
    $GuiHealthbarStart = $GuiHealthbarType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if ($GuiHealthbarStart) {
        $GuiHealthbarStartIl = $GuiHealthbarStart.Body.GetILProcessor()
        $GuiHealthbarStartRet = @($GuiHealthbarStart.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
        if (-not $GuiHealthbarStartRet) {
            throw "Could not locate GuiHealthbar.Start() return instruction."
        }
        if ($ImportedShieldBuffBarAttach) {
            $GuiHealthbarStartIl.InsertBefore($GuiHealthbarStartRet, $GuiHealthbarStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $GuiHealthbarStartIl.InsertBefore($GuiHealthbarStartRet, $GuiHealthbarStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShieldBuffBarAttach))
        }
        if ($ImportedFriendlyLowHealthAttach) {
            $GuiHealthbarStartIl.InsertBefore($GuiHealthbarStartRet, $GuiHealthbarStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $GuiHealthbarStartIl.InsertBefore($GuiHealthbarStartRet, $GuiHealthbarStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedFriendlyLowHealthAttach))
        }
    }
}
}

$MainMenu = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
$CameraFov = $Module.Types | Where-Object Name -eq "CameraFov" | Select-Object -First 1
$UiStyleFontComponent = $Module.Types | Where-Object Name -eq "UiStyleFontComponent" | Select-Object -First 1

if ($Config.enabled) {
    Insert-CallAtStart -Method ($MainMenu.Methods | Where-Object Name -eq "Start" | Select-Object -First 1) -Call $ImportedApplyAllCanvases
    Insert-CallAtStart -Method ($CameraFov.Methods | Where-Object Name -eq "Start" | Select-Object -First 1) -Call $ImportedApplyAllCanvases

    $SetStyle = $UiStyleFontComponent.Methods | Where-Object Name -eq "SetStyle" | Select-Object -First 1
    if ($SetStyle -and $SetStyle.HasBody) {
        $MTextField = $UiStyleFontComponent.Fields | Where-Object Name -eq "m_text" | Select-Object -First 1
        $Il = $SetStyle.Body.GetILProcessor()
        foreach ($Instruction in @($SetStyle.Body.Instructions)) {
            if ($Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret) {
                $Il.InsertBefore($Instruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
                $Il.InsertBefore($Instruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $MTextField))
                $Il.InsertBefore($Instruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyToText))
            }
        }
    }
}

if ($EnableFovFeature -and $CameraFov -and $ImportedApplyCameraFov) {
    # Direct offset-based patching for CameraFov.Update() like the old working bundle
    $FovUpdateMethod = $CameraFov.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if ($FovUpdateMethod -and $FovUpdateMethod.HasBody) {
        $FovInstrs = @($FovUpdateMethod.Body.Instructions)
        $ForcedFov = [single]$FovConfig.fov
        ($FovInstrs | Where-Object Offset -eq 72 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
        ($FovInstrs | Where-Object Offset -eq 72 | Select-Object -First 1).Operand = $ForcedFov
        ($FovInstrs | Where-Object Offset -eq 77 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
        ($FovInstrs | Where-Object Offset -eq 77 | Select-Object -First 1).Operand = $null
        ($FovInstrs | Where-Object Offset -eq 82 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
        ($FovInstrs | Where-Object Offset -eq 82 | Select-Object -First 1).Operand = $null
    }
}

if ($EnableFovFeature) {
    $CameraArmsType = $Module.Types | Where-Object Name -eq "CameraArms" | Select-Object -First 1
    if ($CameraArmsType) {
        $CameraArmsUpdate = $CameraArmsType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
        if ($CameraArmsUpdate -and $CameraArmsUpdate.HasBody) {
            $CamArmInstrs = @($CameraArmsUpdate.Body.Instructions)
            $WeapFov = [single]30.0
            if ($null -ne $FovConfig.weapon_model_fov) { $WeapFov = [single]([double]$FovConfig.weapon_model_fov) }
            ($CamArmInstrs | Where-Object Offset -eq 103 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
            ($CamArmInstrs | Where-Object Offset -eq 103 | Select-Object -First 1).Operand = $WeapFov
            ($CamArmInstrs | Where-Object Offset -eq 104 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
            ($CamArmInstrs | Where-Object Offset -eq 104 | Select-Object -First 1).Operand = $null
            ($CamArmInstrs | Where-Object Offset -eq 109 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
            ($CamArmInstrs | Where-Object Offset -eq 109 | Select-Object -First 1).Operand = $null
            ($CamArmInstrs | Where-Object Offset -eq 130 | Select-Object -First 1).Operand = $WeapFov
        }
    }
}

    if ($ImportedAdsScaleMethod) {
        $MouseLookType = $Module.Types | Where-Object Name -eq "MouseLook" | Select-Object -First 1
        $RotateByMouseMethod = $MouseLookType.Methods | Where-Object Name -eq "RotateByMouse" | Select-Object -First 1
        $RotateByMouseIl = $RotateByMouseMethod.Body.GetILProcessor()
        $RotateByMouseInstructions = @($RotateByMouseMethod.Body.Instructions)
        $UnitField = $MouseLookType.Fields | Where-Object Name -eq "unit" | Select-Object -First 1

        $XScaleTarget = $RotateByMouseInstructions | Where-Object Offset -eq 53 | Select-Object -First 1
        $XInjected = @(
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $UnitField),
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedAdsScaleMethod)
        )
        foreach ($Instruction in $XInjected) {
            $RotateByMouseIl.InsertBefore($XScaleTarget, $Instruction)
        }

        $YScaleTarget = $RotateByMouseInstructions | Where-Object Offset -eq 142 | Select-Object -First 1
        $YInjected = @(
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $UnitField),
            $RotateByMouseIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedAdsScaleMethod)
        )
        foreach ($Instruction in $YInjected) {
            $RotateByMouseIl.InsertBefore($YScaleTarget, $Instruction)
        }
    }

if ($AccuracyConfig.enabled) {
    $BloomLogic = $Module.Types | Where-Object Name -eq "BloomLogic"
    $RayScatter = $Module.Types | Where-Object Name -eq "RayScatter"

    $OnShot = $BloomLogic.Methods | Where-Object Name -eq "OnShot" | Select-Object -First 1
    $OnShotIl = $OnShot.Body.GetILProcessor()
    $OnShotInstructions = @($OnShot.Body.Instructions)
    Insert-Before -Il $OnShotIl -Target ($OnShotInstructions | Where-Object Offset -eq 22 | Select-Object -First 1) -Instructions @(
        $OnShotIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.shot_bloom_multiplier),
        $OnShotIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )

    $CalcAngle = $BloomLogic.Methods | Where-Object Name -eq "CalcAngle" | Select-Object -First 1
    $CalcAngleIl = $CalcAngle.Body.GetILProcessor()
    $CalcAngleInstructions = @($CalcAngle.Body.Instructions)
    Insert-Before -Il $CalcAngleIl -Target ($CalcAngleInstructions | Where-Object Offset -eq 19 | Select-Object -First 1) -Instructions @(
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.base_angle_multiplier),
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )
    Insert-Before -Il $CalcAngleIl -Target ($CalcAngleInstructions | Where-Object Offset -eq 45 | Select-Object -First 1) -Instructions @(
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.jump_mod_multiplier),
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )
    Insert-Before -Il $CalcAngleIl -Target ($CalcAngleInstructions | Where-Object Offset -eq 70 | Select-Object -First 1) -Instructions @(
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.crouch_mod_multiplier),
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )
    Insert-Before -Il $CalcAngleIl -Target ($CalcAngleInstructions | Where-Object Offset -eq 90 | Select-Object -First 1) -Instructions @(
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.move_mod_multiplier),
        $CalcAngleIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )

    $Apply = $RayScatter.Methods | Where-Object Name -eq "Apply" | Select-Object -First 1
    $ApplyIl = $Apply.Body.GetILProcessor()
    $ApplyInstructions = @($Apply.Body.Instructions)
    Insert-Before -Il $ApplyIl -Target ($ApplyInstructions | Where-Object Offset -eq 82 | Select-Object -First 1) -Instructions @(
        $ApplyIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$AccuracyConfig.scatter_multiplier),
        $ApplyIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
    )
}

if ($WeaponSwitchConfig.enabled) {
    $BuffHelper = $Module.Types | Where-Object Name -eq "BuffHelper"
    $Multiplier = [single]$WeaponSwitchConfig.switch_time_multiplier
    foreach ($MethodName in @("GearDropTime", "GearPickupTime")) {
        $Method = $BuffHelper.Methods | Where-Object Name -eq $MethodName | Select-Object -First 1
        $Il = $Method.Body.GetILProcessor()
        $Ret = $Method.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -First 1
        $Il.InsertBefore($Ret, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $Multiplier))
        $Il.InsertBefore($Ret, $Il.Create([Mono.Cecil.Cil.OpCodes]::Mul))
    }
}

if ($ProjectileConfig.enabled) {
    $ToolLogicGetUnit = $Module.ImportReference((($Module.Types | Where-Object Name -eq "ToolLogic").Methods | Where-Object Name -eq "GetUnit" | Select-Object -First 1))
    $UnitCardGetter = $Module.ImportReference((($Module.Types | Where-Object Name -eq "Unit").Methods | Where-Object Name -eq "get_UnitCard" | Select-Object -First 1))
    $CardIdGetter = $Module.ImportReference((($Module.Types | Where-Object FullName -eq "Protocol.Card").Methods | Where-Object Name -eq "get_Id" | Select-Object -First 1))
    $LogPath = Join-Path $GameRoot "Win64\bnl-experimental.log"

    function New-AppendLogInstructions {
        param([Mono.Cecil.Cil.ILProcessor]$Il,[string]$Text)
        return @(
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $LogPath),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "$Text`r`n"),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText)
        )
    }

    function New-AppendUnitCardIdInstructions {
        param([Mono.Cecil.Cil.ILProcessor]$Il,[string]$Prefix)
        return @(
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $LogPath),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $Prefix),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ToolLogicGetUnit),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Callvirt, $UnitCardGetter),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Callvirt, $CardIdGetter),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $StringConcat2),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "`r`n"),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $StringConcat2),
            $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText)
        )
    }

    if ($ProjectileConfig.log_tool_branches) {
        $Targets = @(
            @{ Type = "ToolLogicShot"; Method = "ApplyShot" },
            @{ Type = "ToolLogicBurst"; Method = "ApplyShot" },
            @{ Type = "ToolLogicCharge"; Method = "ApplyShot" },
            @{ Type = "ToolLogicSpinup"; Method = "ApplyShot" },
            @{ Type = "ToolLogicThrow"; Method = "ApplyShot" }
        )

        foreach ($Target in $Targets) {
            $Type = $Module.Types | Where-Object Name -eq $Target.Type
            if (-not $Type) { continue }
            $Method = $Type.Methods | Where-Object Name -eq $Target.Method | Select-Object -First 1
            if (-not $Method) { continue }

            $Il = $Method.Body.GetILProcessor()
            $Instructions = @($Method.Body.Instructions)
            if ($ProjectileConfig.log_unit_card_id) {
                Insert-Before -Il $Il -Target $Instructions[0] -Instructions (New-AppendUnitCardIdInstructions -Il $Il -Prefix "[$($Target.Type)] unit=")
            }

            $Instructions = @($Method.Body.Instructions)
            $HitscanCall = $Instructions | Where-Object { $_.Operand -and $_.Operand.FullName -like "*ShotHelper::InstantShotFull*" } | Select-Object -First 1
            if ($HitscanCall) {
                Insert-Before -Il $Il -Target $HitscanCall -Instructions (New-AppendLogInstructions -Il $Il -Text "[$($Target.Type)] branch=Hitscan")
            }
            $Instructions = @($Method.Body.Instructions)
            $ProjectileCall = $Instructions | Where-Object { $_.Operand -and $_.Operand.FullName -like "*ShotHelper::ProjectileShotFull*" } | Select-Object -First 1
            if ($ProjectileCall) {
                Insert-Before -Il $Il -Target $ProjectileCall -Instructions (New-AppendLogInstructions -Il $Il -Text "[$($Target.Type)] branch=Projectile")
            }
            $Instructions = @($Method.Body.Instructions)
            $UnitProjectileCall = $Instructions | Where-Object { $_.Operand -and $_.Operand.FullName -like "*ShotHelper::UnitProjectileShotFull*" } | Select-Object -First 1
            if ($UnitProjectileCall) {
                Insert-Before -Il $Il -Target $UnitProjectileCall -Instructions (New-AppendLogInstructions -Il $Il -Text "[$($Target.Type)] branch=UnitProjectile")
            }
        }
    }

    $RocketSpeed = [single]$ProjectileConfig.rocket_projectile_speed
    $RocketLifetimeMultiplier = [single]$(if ($null -ne $ProjectileConfig.rocket_projectile_lifetime_multiplier) { $ProjectileConfig.rocket_projectile_lifetime_multiplier } else { 1.0 })
    $RocketType = $Module.Types | Where-Object Name -eq "ProjectileMovementRocket"
    $RocketStart = $RocketType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    $RocketInstructions = @($RocketStart.Body.Instructions)
    ($RocketInstructions | Where-Object Offset -eq 86 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
    ($RocketInstructions | Where-Object Offset -eq 86 | Select-Object -First 1).Operand = $RocketSpeed
    ($RocketInstructions | Where-Object Offset -eq 87 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($RocketInstructions | Where-Object Offset -eq 87 | Select-Object -First 1).Operand = $null
    ($RocketInstructions | Where-Object Offset -eq 92 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($RocketInstructions | Where-Object Offset -eq 92 | Select-Object -First 1).Operand = $null
    if ([Math]::Abs($RocketLifetimeMultiplier - 1.0) -gt 0.0001) {
        $RocketUpdate = $RocketType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
        $RocketUpdateIl = $RocketUpdate.Body.GetILProcessor()
        $RocketUpdateTarget = @($RocketUpdate.Body.Instructions) | Where-Object Offset -eq 431 | Select-Object -First 1
        Insert-Before -Il $RocketUpdateIl -Target $RocketUpdateTarget -Instructions @(
            $RocketUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $RocketLifetimeMultiplier),
            $RocketUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
        )
    }

    $UnitProjectileSpeed = [single]$ProjectileConfig.unit_projectile_speed
    $UnitProjectileLifetimeMultiplier = [single]$(if ($null -ne $ProjectileConfig.unit_projectile_lifetime_multiplier) { $ProjectileConfig.unit_projectile_lifetime_multiplier } else { 1.0 })
    $UnitProjectileType = $Module.Types | Where-Object Name -eq "UnitProjectileMovement"
    $UnitProjectileUpdate = $UnitProjectileType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    $UnitProjectileIl = $UnitProjectileUpdate.Body.GetILProcessor()
    $UnitProjectileInstructions = @($UnitProjectileUpdate.Body.Instructions)
    $AfterClamp = $UnitProjectileInstructions | Where-Object Offset -eq 174 | Select-Object -First 1
    Insert-Before -Il $UnitProjectileIl -Target $AfterClamp -Instructions @(
        $UnitProjectileIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $UnitProjectileSpeed),
        $UnitProjectileIl.Create([Mono.Cecil.Cil.OpCodes]::Stloc_0)
    )
    if ([Math]::Abs($UnitProjectileLifetimeMultiplier - 1.0) -gt 0.0001) {
        $UnitTimeoutCompare = $UnitProjectileInstructions | Where-Object Offset -eq 118 | Select-Object -First 1
        Insert-Before -Il $UnitProjectileIl -Target $UnitTimeoutCompare -Instructions @(
            $UnitProjectileIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $UnitProjectileLifetimeMultiplier),
            $UnitProjectileIl.Create([Mono.Cecil.Cil.OpCodes]::Mul)
        )
    }
}

if ($CrosshairConfig.enabled) {
    $GuiCrosshairControllerType = $Module.Types | Where-Object Name -eq "GuiCrosshairController" | Select-Object -First 1
    $GuiCrosshairBlankType = $Module.Types | Where-Object Name -eq "GuiCrosshairBlank" | Select-Object -First 1

    if ($GuiCrosshairControllerType) {
        $CrosshairUpdate = $GuiCrosshairControllerType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
        if ($CrosshairUpdate -and $CrosshairUpdate.HasBody) {
            $CrosshairUpdateIl = $CrosshairUpdate.Body.GetILProcessor()
            $CrosshairUpdateInstructions = @($CrosshairUpdate.Body.Instructions)
            if ($CrosshairUpdateInstructions.Count -gt 0) {
                $CrosshairContinue = $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Nop)
                Insert-Before -Il $CrosshairUpdateIl -Target $CrosshairUpdateInstructions[0] -Instructions @(
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyCrosshairHardHideMethod),
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $CrosshairContinue),
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ret),
                    $CrosshairContinue
                )
            }
            $SetActiveCall = $CrosshairUpdateInstructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "SetActive"
            } | Select-Object -First 1
            if ($SetActiveCall) {
                Insert-After -Il $CrosshairUpdateIl -Target $SetActiveCall -Instructions @(
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyCrosshairVisibilityMethod)
                )
            }

            $UpdatePopulationCall = $CrosshairUpdateInstructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "UpdatePopulation"
            } | Select-Object -First 1
            if ($UpdatePopulationCall) {
                Insert-Before -Il $CrosshairUpdateIl -Target $UpdatePopulationCall -Instructions @(
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
                    $CrosshairUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyCrosshairControllerMethod)
                )
            }
        }

        $GetAppropriateMethod = $GuiCrosshairControllerType.Methods | Where-Object Name -eq "GetAppropriateCrosshairPrefab" | Select-Object -First 1
        if ($GetAppropriateMethod) {
            $GetAppropriateMethod.Body.Instructions.Clear()
            $GetAppropriateMethod.Body.Variables.Clear()
            $GetAppropriateMethod.Body.ExceptionHandlers.Clear()
            $GetAppropriateMethod.Body.InitLocals = $false
            $GetAppropriateIl = $GetAppropriateMethod.Body.GetILProcessor()
            $GetAppropriateIl.Append($GetAppropriateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $GetAppropriateIl.Append($GetAppropriateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
            $GetAppropriateIl.Append($GetAppropriateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedGetCrosshairPrefabMethod))
            $GetAppropriateIl.Append($GetAppropriateIl.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        }
    }

    if ($GuiCrosshairBlankType) {
        $SetColorMethod = $GuiCrosshairBlankType.Methods | Where-Object Name -eq "SetColor" | Select-Object -First 1
        if ($SetColorMethod -and $SetColorMethod.HasBody) {
            $SetColorIl = $SetColorMethod.Body.GetILProcessor()
            $SetColorRet = @($SetColorMethod.Body.Instructions) | Where-Object OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret | Select-Object -Last 1
            if ($SetColorRet) {
                Insert-Before -Il $SetColorIl -Target $SetColorRet -Instructions @(
                    $SetColorIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
                    $SetColorIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyCrosshairBlankMethod)
                )
            }
        }
    }

    foreach ($TypeName in @("GuiCrosshair", "GuiCrosshairCircle", "GuiCrosshairMelee")) {
        $Type = $Module.Types | Where-Object Name -eq $TypeName | Select-Object -First 1
        if (-not $Type) { continue }
        $Method = $Type.Methods | Where-Object Name -eq "SetAngle" | Select-Object -First 1
        if (-not $Method -or -not $Method.HasBody -or $Method.Parameters.Count -lt 1) { continue }
        $Il = $Method.Body.GetILProcessor()
        if ($TypeName -eq "GuiCrosshair") {
            $MaxBloomField = $Type.Fields | Where-Object Name -eq "MaxBloom" | Select-Object -First 1
            if ($MaxBloomField) {
                $MaxBloomLoads = @($Method.Body.Instructions | Where-Object {
                    $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and $_.Operand -eq $MaxBloomField
                })
                foreach ($MaxBloomLoad in $MaxBloomLoads) {
                    Insert-After -Il $Il -Target $MaxBloomLoad -Instructions @(
                        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedScaleCrosshairSizeVectorMethod)
                    )
                }
            }
        }
        if ($Method.Body.Instructions.Count -gt 0) {
            $First = $Method.Body.Instructions[0]
            Insert-Before -Il $Il -Target $First -Instructions @(
                $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
                $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedScaleCrosshairAngleMethod),
                $Il.Create([Mono.Cecil.Cil.OpCodes]::Starg_S, $Method.Parameters[0])
            )
        }

        $Ret = @($Method.Body.Instructions) | Where-Object OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret | Select-Object -Last 1
        if ($Ret) {
            Insert-Before -Il $Il -Target $Ret -Instructions @(
                $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
                $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyCrosshairBlankMethod)
            )
        }
    }
}

if ($TeamColorConfig.enabled) {
    function Convert-HexToColorData {
        param([string]$Hex, [single]$Alpha = 1.0)
        $Normalized = $Hex.Trim()
        if ($Normalized.StartsWith("#")) { $Normalized = $Normalized.Substring(1) }
        if ($Normalized.Length -ne 6) { throw "Color '$Hex' must be in #RRGGBB format." }
        $R = [Convert]::ToInt32($Normalized.Substring(0, 2), 16)
        $G = [Convert]::ToInt32($Normalized.Substring(2, 2), 16)
        $B = [Convert]::ToInt32($Normalized.Substring(4, 2), 16)
        return [PSCustomObject]@{ Hex = "#$Normalized".ToUpperInvariant(); R = [single]($R / 255.0); G = [single]($G / 255.0); B = [single]($B / 255.0); A = $Alpha }
    }

    $Friendly = Convert-HexToColorData -Hex $TeamColorConfig.friendly_color -Alpha 1.0
    $Enemy = Convert-HexToColorData -Hex $TeamColorConfig.enemy_color -Alpha 1.0
    $FriendlyBg = Convert-HexToColorData -Hex $TeamColorConfig.friendly_color -Alpha 0.45
    $EnemyBg = Convert-HexToColorData -Hex $TeamColorConfig.enemy_color -Alpha 0.45

    $TeamColorRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.TeamColorRuntime" | Select-Object -First 1
    if (-not $TeamColorRuntimeType) { throw "TeamColorRuntime type not found in helper assembly." }
    $ImportedReplacementMethods = @{
        "TeamFriendly" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetGuiFriendlyColor" | Select-Object -First 1))
        "TeamEnemy" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetGuiEnemyColor" | Select-Object -First 1))
        "BackgroundTeamFriendly" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetGuiBackgroundFriendlyColor" | Select-Object -First 1))
        "BackgroundTeamEnemy" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetGuiBackgroundEnemyColor" | Select-Object -First 1))
        "CommonTeamFriendly" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetObjectCommonFriendlyColor" | Select-Object -First 1))
        "CommonTeamEnemy" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetObjectCommonEnemyColor" | Select-Object -First 1))
        "ForceFieldTeamFriendly" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetForceFieldFriendlyColor" | Select-Object -First 1))
        "ForceFieldTeamEnemy" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetForceFieldEnemyColor" | Select-Object -First 1))
        "IceTeamFriendly" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetIceFriendlyColor" | Select-Object -First 1))
        "IceTeamEnemy" = $Module.ImportReference(($TeamColorRuntimeType.Methods | Where-Object Name -eq "GetIceEnemyColor" | Select-Object -First 1))
    }
    $TeamColorContainerType = $Module.Types | Where-Object Name -eq "TeamColorContainer" | Select-Object -First 1
    $GuiField = $TeamColorContainerType.Fields | Where-Object Name -eq "Gui" | Select-Object -First 1
    $ObjectsField = $TeamColorContainerType.Fields | Where-Object Name -eq "Objects" | Select-Object -First 1
    $GuiFieldToken = $GuiField.MetadataToken.ToInt32()
    $ObjectsFieldToken = $ObjectsField.MetadataToken.ToInt32()
    $ColorFieldTokens = @{}
    foreach ($Nested in $TeamColorContainerType.NestedTypes) {
        foreach ($Field in $Nested.Fields) {
            if ($ImportedReplacementMethods.ContainsKey($Field.Name)) {
                $ColorFieldTokens[$Field.MetadataToken.ToInt32()] = $Field.Name
            }
        }
    }

    function Get-AllTypes {
        param([Mono.Cecil.TypeDefinition]$TypeDef)
        $List = New-Object System.Collections.Generic.List[Mono.Cecil.TypeDefinition]
        $List.Add($TypeDef) | Out-Null
        foreach ($Nested in $TypeDef.NestedTypes) {
            foreach ($Child in (Get-AllTypes -TypeDef $Nested)) { $List.Add($Child) | Out-Null }
        }
        return $List
    }

    $AllTypes = New-Object System.Collections.Generic.List[Mono.Cecil.TypeDefinition]
    foreach ($Type in $Module.Types) {
        foreach ($Resolved in (Get-AllTypes -TypeDef $Type)) { $AllTypes.Add($Resolved) | Out-Null }
    }

    foreach ($Type in $AllTypes) {
        foreach ($Method in $Type.Methods) {
            if (-not $Method.HasBody) { continue }
            $Instructions = @($Method.Body.Instructions)
            for ($i = 2; $i -lt $Instructions.Count; $i++) {
                $Current = $Instructions[$i]
                if ($Current.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ldfld) { continue }
                $CurrentField = $Current.Operand -as [Mono.Cecil.FieldReference]
                if (-not $CurrentField) { continue }
                $CurrentToken = $CurrentField.Resolve().MetadataToken.ToInt32()
                if (-not $ColorFieldTokens.ContainsKey($CurrentToken)) { continue }
                $Prev1 = $Instructions[$i - 1]
                $Prev2 = $Instructions[$i - 2]
                if ($Prev1.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ldfld) { continue }
                if ($Prev2.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Call) { continue }
                $RootField = $Prev1.Operand -as [Mono.Cecil.FieldReference]
                $CallMethod = $Prev2.Operand -as [Mono.Cecil.MethodReference]
                if (-not $RootField -or -not $CallMethod) { continue }
                if ($CallMethod.Name -ne "get_Instance") { continue }
                $RootToken = $RootField.Resolve().MetadataToken.ToInt32()
                if ($RootToken -ne $GuiFieldToken -and $RootToken -ne $ObjectsFieldToken) { continue }
                $ReplacementName = $ColorFieldTokens[$CurrentToken]
                $Prev2.OpCode = [Mono.Cecil.Cil.OpCodes]::Call
                $Prev2.Operand = $ImportedReplacementMethods[$ReplacementName]
                $Prev1.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
                $Prev1.Operand = $null
                $Current.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
                $Current.Operand = $null
            }
        }
    }

    $BlockGenericClone = $Module.Types | Where-Object Name -eq "BlockGenericClone" | Select-Object -First 1
    if ($BlockGenericClone) {
        $BuildMethod = $BlockGenericClone.Methods | Where-Object Name -eq "Build" | Select-Object -First 1
        if ($BuildMethod -and $BuildMethod.HasBody) {
            foreach ($Instruction in @($BuildMethod.Body.Instructions)) {
                if ($Instruction.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Call) { continue }
                $Operand = $Instruction.Operand -as [Mono.Cecil.MethodReference]
                if (-not $Operand) { continue }
                if ($Operand.Name -eq "get_blue") {
                    $Instruction.Operand = $ImportedReplacementMethods["CommonTeamFriendly"]
                }
                elseif ($Operand.Name -eq "get_red") {
                    $Instruction.Operand = $ImportedReplacementMethods["CommonTeamEnemy"]
                }
            }
        }
    }
}

if ($LockOnConfig.enabled -and $ImportedLockOnAttachMethod) {
    $MouseLookType = $Module.Types | Where-Object Name -eq "MouseLook" | Select-Object -First 1
    $OnUnitCreateMethod = $MouseLookType.Methods | Where-Object Name -eq "OnUnitCreate" | Select-Object -First 1
    if (-not $OnUnitCreateMethod) {
        throw "OnUnitCreate method not found in MouseLook"
    }

    $TargetInstruction = $OnUnitCreateMethod.Body.Instructions | Select-Object -Last 1
    $Il = $OnUnitCreateMethod.Body.GetILProcessor()
    $KeyCodeType = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType("UnityEngine.KeyCode", $false) } | Where-Object { $_ -ne $null } | Select-Object -First 1
    $ToggleKeyValue = [int][System.Enum]::Parse($KeyCodeType, [string]$LockOnConfig.toggle_key)
    $MaxRange = [single]$LockOnConfig.max_range
    $MaxAngle = [single]$LockOnConfig.max_angle
    $TurnSpeed = [single]$LockOnConfig.turn_speed
    $RequireLos = [bool]$LockOnConfig.require_los
    $PredictMovement = [bool]$LockOnConfig.predict_movement
    $AimAtHead = [bool]$LockOnConfig.aim_at_head
    $SelfLogPath = Join-Path $GameRoot "Win64\bnl-lockon-selflog.log"
    $RequireLosOpCode = if ($RequireLos) { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1 } else { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0 }
    $PredictMovementOpCode = if ($PredictMovement) { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1 } else { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0 }
    $AimAtHeadOpCode = if ($AimAtHead) { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1 } else { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0 }

    $Injected = @(
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $SelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[LOCKON-SELF] MouseLook.OnUnitCreate`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $ToggleKeyValue),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $MaxRange),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $MaxAngle),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $TurnSpeed),
        $Il.Create($RequireLosOpCode),
        $Il.Create($PredictMovementOpCode),
        $Il.Create($AimAtHeadOpCode),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $SelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[LOCKON-SELF] Before AttachTo`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedLockOnAttachMethod),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $SelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[LOCKON-SELF] After AttachTo`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText)
    )

    foreach ($Instruction in $Injected) {
        $Il.InsertBefore($TargetInstruction, $Instruction)
    }
}

if ($TrackingConfig.enabled -and $ImportedTrackingAttachMethod -and $ImportedTrackingAttachUnitMethod) {
    $RocketType = $Module.Types | Where-Object Name -eq "ProjectileMovementRocket" | Select-Object -First 1
    $RocketStart = $RocketType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if (-not $RocketStart) {
        throw "ProjectileMovementRocket.Start not found"
    }

    $Il = $RocketStart.Body.GetILProcessor()
    $TargetInstruction = $RocketStart.Body.Instructions | Select-Object -Last 1
    $ControlledOnlyOpCode = if ([bool]$TrackingConfig.controlled_only) { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1 } else { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0 }
    $DebugOpCode = if ([bool]$TrackingConfig.debug_log) { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1 } else { [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0 }
    $SeekRange = [single]$TrackingConfig.seek_range
    $MaxAngle = [single]$TrackingConfig.max_angle_degrees
    $TurnRate = [single]$TrackingConfig.turn_rate_degrees
    $KeyCodeType = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType("UnityEngine.KeyCode", $false) } | Where-Object { $_ -ne $null } | Select-Object -First 1
    $TrackingToggleKeyValue = [int][System.Enum]::Parse($KeyCodeType, [string]$TrackingConfig.toggle_key)
    $TrackingSelfLogPath = Join-Path $GameRoot "Win64\bnl-tracking-selflog.log"

    $Injected = @(
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] ProjectileMovementRocket.Start`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $TrackingToggleKeyValue),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $SeekRange),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $MaxAngle),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $TurnRate),
        $Il.Create($ControlledOnlyOpCode),
        $Il.Create($DebugOpCode),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] Before AttachTo`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedTrackingAttachMethod),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] After AttachTo`r`n"),
        $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText)
    )

    foreach ($Instruction in $Injected) {
        $Il.InsertBefore($TargetInstruction, $Instruction)
    }

    $UnitProjectileType = $Module.Types | Where-Object Name -eq "UnitProjectileMovement" | Select-Object -First 1
    $UnitProjectileOnCreate = $UnitProjectileType.Methods | Where-Object Name -eq "OnUnitCreate" | Select-Object -First 1
    if (-not $UnitProjectileOnCreate) {
        throw "UnitProjectileMovement.OnUnitCreate not found"
    }

    $UnitIl = $UnitProjectileOnCreate.Body.GetILProcessor()
    $UnitTargetInstruction = $UnitProjectileOnCreate.Body.Instructions | Select-Object -Last 1
    $UnitInjected = @(
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] UnitProjectileMovement.OnUnitCreate`r`n"),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4, $TrackingToggleKeyValue),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $SeekRange),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $MaxAngle),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, $TurnRate),
        $UnitIl.Create($ControlledOnlyOpCode),
        $UnitIl.Create($DebugOpCode),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] Before AttachToUnitProjectile`r`n"),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedTrackingAttachUnitMethod),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, $TrackingSelfLogPath),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Ldstr, "[TRACK-SELF] After AttachToUnitProjectile`r`n"),
        $UnitIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $AppendAllText)
    )

    foreach ($Instruction in $UnitInjected) {
        $UnitIl.InsertBefore($UnitTargetInstruction, $Instruction)
    }
}

if ($BaseObjectiveBeamConfig.enabled) {
    $BuildingBeamEffect = $Module.Types | Where-Object Name -eq "BuildingBeamEffect" | Select-Object -First 1
    if (-not $BuildingBeamEffect) {
        throw "BuildingBeamEffect type not found."
    }

    $UpdateMethod = $BuildingBeamEffect.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if (-not $UpdateMethod) {
        throw "BuildingBeamEffect.Update method not found."
    }

    $ActiveField = $BuildingBeamEffect.Fields | Where-Object Name -eq "Active" | Select-Object -First 1
    if (-not $ActiveField) {
        throw "BuildingBeamEffect.Active field not found."
    }

    $TargetInstruction = $UpdateMethod.Body.Instructions |
        Where-Object {
            $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and
            $_.Operand -ne $null -and
            $_.Operand.Name -eq "Active"
        } |
        Select-Object -First 1

    if (-not $TargetInstruction) {
        throw "Could not find Active check in BuildingBeamEffect.Update."
    }

    $ImportedActiveField = $Module.ImportReference($ActiveField)
    $BaseObjectiveBeamRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.BaseObjectiveBeamRuntime" | Select-Object -First 1
    if (-not $BaseObjectiveBeamRuntimeType) { throw "BaseObjectiveBeamRuntime type not found in helper assembly." }
    $ImportedShouldHideBaseObjectiveBeam = $Module.ImportReference(($BaseObjectiveBeamRuntimeType.Methods | Where-Object Name -eq "ShouldHide" | Select-Object -First 1))
    $Il = $UpdateMethod.Body.GetILProcessor()
    $SkipHideInstruction = $Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $TargetInstruction)
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldHideBaseObjectiveBeam))
    $Il.InsertBefore($TargetInstruction, $SkipHideInstruction)
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Stfld, $ImportedActiveField))
}

if ($HideImpactVfxConfig.enabled) {
    $HideImpactVfxRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.HideImpactVfxRuntime" | Select-Object -First 1
    if (-not $HideImpactVfxRuntimeType) { throw "HideImpactVfxRuntime helper type not found." }

    $ImportedEnsureInit    = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "EnsureInit"    | Select-Object -First 1))
    $ImportedShouldHideVfx = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "ShouldHideVfx" | Select-Object -First 1))
    $ImportedShouldHidePlane = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "ShouldHidePlane" | Select-Object -First 1))

    # Patch ZoneServiceListener.Impact — only inject EnsureInit so the static ctor fires.
    # Do NOT early-return here: Impact also handles sounds, GotHit, HitUnit etc.
    # VFX is suppressed by patching GlobalEffects.MakeImpactEffect directly below.
    $ZoneServiceListenerType = $Module.Types | Where-Object Name -eq "ZoneServiceListener" | Select-Object -First 1
    if (-not $ZoneServiceListenerType) { throw "ZoneServiceListener type not found." }

    $ImpactMethod = $ZoneServiceListenerType.Methods | Where-Object Name -eq "Impact" | Select-Object -First 1
    if (-not $ImpactMethod -or -not $ImpactMethod.HasBody) { throw "ZoneServiceListener.Impact not found." }

    $ImpactIl = $ImpactMethod.Body.GetILProcessor()
    $FirstInstruction = $ImpactMethod.Body.Instructions[0]
    $ImpactIl.InsertBefore($FirstInstruction, $ImpactIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureInit))

    # Patch GlobalEffects.MakeImpactEffect (both overloads) — if (ShouldHideVfx()) return;
    # This suppresses only visual effects while leaving PostImpact (sounds) untouched.
    $GlobalEffectsType = $Module.Types | Where-Object Name -eq "GlobalEffects" | Select-Object -First 1
    if (-not $GlobalEffectsType) { throw "GlobalEffects type not found." }

    $GlobalEffectsType.Methods | Where-Object Name -eq "MakeImpactEffect" | ForEach-Object {
        $m = $_
        if (-not $m.HasBody) { return }
        $mIl = $m.Body.GetILProcessor()
        $first = $m.Body.Instructions[0]
        $cont = $mIl.Create([Mono.Cecil.Cil.OpCodes]::Nop)
        $mIl.InsertBefore($first, $mIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldHideVfx))
        $mIl.InsertBefore($first, $mIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $cont))
        $mIl.InsertBefore($first, $mIl.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        $mIl.InsertBefore($first, $cont)
    }

    # Patch MapPlane.Awake — after normal Awake runs (ground + collider both created),
    # call HideImpactVfxRuntime.HidePlane(this) which disables renderers on the ground GO.
    # Collision is preserved; only the visual mesh is hidden.
    $MapPlaneType = $Module.Types | Where-Object Name -eq "MapPlane" | Select-Object -First 1
    if (-not $MapPlaneType) { throw "MapPlane type not found." }

    $MapPlaneAwake = $MapPlaneType.Methods | Where-Object Name -eq "Awake" | Select-Object -First 1
    if (-not $MapPlaneAwake -or -not $MapPlaneAwake.HasBody) { throw "MapPlane.Awake not found." }

    $ImportedHidePlane = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "HidePlane" | Select-Object -First 1))

    # Append before the final ret: HideImpactVfxRuntime.HidePlane(this)
    $AwakeIl = $MapPlaneAwake.Body.GetILProcessor()
    $RetInstruction = @($MapPlaneAwake.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1
    $AwakeIl.InsertBefore($RetInstruction, $AwakeIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $AwakeIl.InsertBefore($RetInstruction, $AwakeIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedHidePlane))

    # Helper: inject if (ShouldHideVfx()) return; at the start of a void method.
    function Inject-HideVfxEarlyReturn($method) {
        $il = $method.Body.GetILProcessor()
        $first = $method.Body.Instructions[0]
        $cont = $il.Create([Mono.Cecil.Cil.OpCodes]::Nop)
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldHideVfx))
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $cont))
        $il.InsertBefore($first, $il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        $il.InsertBefore($first, $cont)
    }

    # Patch RolyTankBallCannon.OnGearToolFire — cannon barrel smoke/flash
    $CannonType = $Module.Types | Where-Object Name -eq "RolyTankBallCannon" | Select-Object -First 1
    if ($CannonType) {
        $CannonFire = $CannonType.Methods | Where-Object Name -eq "OnGearToolFire" | Select-Object -First 1
        if ($CannonFire -and $CannonFire.HasBody) { Inject-HideVfxEarlyReturn $CannonFire }
    }

    # Patch RolyTankBallRocketEffect.OnGearToolFire — rocket muzzle trail
    $RocketEffectType = $Module.Types | Where-Object Name -eq "RolyTankBallRocketEffect" | Select-Object -First 1
    if ($RocketEffectType) {
        $RocketFire = $RocketEffectType.Methods | Where-Object Name -eq "OnGearToolFire" | Select-Object -First 1
        if ($RocketFire -and $RocketFire.HasBody) { Inject-HideVfxEarlyReturn $RocketFire }
    }

    # Patch GearModelFireEffect.ShotEffect — generic muzzle flash used by most weapons
    $GearModelFireEffectType = $Module.Types | Where-Object Name -eq "GearModelFireEffect" | Select-Object -First 1
    if ($GearModelFireEffectType) {
        $ShotEffect = $GearModelFireEffectType.Methods | Where-Object Name -eq "ShotEffect" | Select-Object -First 1
        if ($ShotEffect -and $ShotEffect.HasBody) { Inject-HideVfxEarlyReturn $ShotEffect }
    }

    # Patch GearModelShotEffect.ShotEffect — cast-triggered muzzle effects
    $GearModelShotEffectType = $Module.Types | Where-Object Name -eq "GearModelShotEffect" | Select-Object -First 1
    if ($GearModelShotEffectType) {
        $ShotEffect2 = $GearModelShotEffectType.Methods | Where-Object { $_.Name -eq "ShotEffect" } | Select-Object -First 1
        if ($ShotEffect2 -and $ShotEffect2.HasBody) { Inject-HideVfxEarlyReturn $ShotEffect2 }
    }

    # Patch BlockFalling.Create — DestroyFallingBlock(childBlock) then ret.
    # childBlock (arg0) is the already-instantiated GO; we must destroy it, not just skip AddComponent.
    $ImportedShouldHideFallingBlocks = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "ShouldHideFallingBlocks" | Select-Object -First 1))
    $ImportedDestroyFallingBlock = $Module.ImportReference(($HideImpactVfxRuntimeType.Methods | Where-Object Name -eq "DestroyFallingBlock" | Select-Object -First 1))
    $BlockFallingType = $Module.Types | Where-Object Name -eq "BlockFalling" | Select-Object -First 1
    if ($BlockFallingType) {
        $BlockFallingCreateMethod = $BlockFallingType.Methods | Where-Object Name -eq "Create" | Select-Object -First 1
        if ($BlockFallingCreateMethod -and $BlockFallingCreateMethod.HasBody) {
            $bfIl = $BlockFallingCreateMethod.Body.GetILProcessor()
            $bfFirst = $BlockFallingCreateMethod.Body.Instructions[0]
            $bfCont = $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Nop)
            # if (!HideFallingBlocks) goto cont
            $bfIl.InsertBefore($bfFirst, $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldHideFallingBlocks))
            $bfIl.InsertBefore($bfFirst, $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $bfCont))
            # DestroyFallingBlock(childBlock)
            $bfIl.InsertBefore($bfFirst, $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $bfIl.InsertBefore($bfFirst, $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedDestroyFallingBlock))
            $bfIl.InsertBefore($bfFirst, $bfIl.Create([Mono.Cecil.Cil.OpCodes]::Ret))
            $bfIl.InsertBefore($bfFirst, $bfCont)
        }

        # Patch BlockFalling.OnDestroy — early return suppresses ember/destroy VFX for falling blocks.
        # MakeBlockDestroy is also called for regular block destruction (ZoneManager), so we can't patch
        # that globally — patch OnDestroy specifically instead.
        $BlockFallingOnDestroy = $BlockFallingType.Methods | Where-Object Name -eq "OnDestroy" | Select-Object -First 1
        if ($BlockFallingOnDestroy -and $BlockFallingOnDestroy.HasBody) {
            $odIl = $BlockFallingOnDestroy.Body.GetILProcessor()
            $odFirst = $BlockFallingOnDestroy.Body.Instructions[0]
            $odCont = $odIl.Create([Mono.Cecil.Cil.OpCodes]::Nop)
            $odIl.InsertBefore($odFirst, $odIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldHideFallingBlocks))
            $odIl.InsertBefore($odFirst, $odIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $odCont))
            $odIl.InsertBefore($odFirst, $odIl.Create([Mono.Cecil.Cil.OpCodes]::Ret))
            $odIl.InsertBefore($odFirst, $odCont)
        }
    }
}

if ($UnitGuiScaleConfig.enabled) {
    $UnitGuiScaleRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.UnitGuiScaleRuntime" | Select-Object -First 1
    if (-not $UnitGuiScaleRuntimeType) { throw "UnitGuiScaleRuntime helper type not found." }

    $ImportedUgsEnsureInit     = $Module.ImportReference(($UnitGuiScaleRuntimeType.Methods | Where-Object Name -eq "EnsureInit"        | Select-Object -First 1))
    $ImportedUgsGetScaleMult   = $Module.ImportReference(($UnitGuiScaleRuntimeType.Methods | Where-Object Name -eq "GetScaleMultiplier" | Select-Object -First 1))

    # Patch GuiFollow.UpdateScale — append: mul GetScaleMultiplier() before ret
    # UpdateScale returns a float on the stack; we multiply it by our runtime value.
    $GuiFollowType = $Module.Types | Where-Object Name -eq "GuiFollow" | Select-Object -First 1
    if (-not $GuiFollowType) { throw "GuiFollow type not found." }

    $GuiFollowUpdateScale = $GuiFollowType.Methods | Where-Object Name -eq "UpdateScale" | Select-Object -First 1
    if (-not $GuiFollowUpdateScale -or -not $GuiFollowUpdateScale.HasBody) { throw "GuiFollow.UpdateScale not found." }

    $ugsIl = $GuiFollowUpdateScale.Body.GetILProcessor()
    $ugsRet = @($GuiFollowUpdateScale.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1

    # Before ret: stack has the lerped float — multiply by GetScaleMultiplier() then ret
    $ugsIl.InsertBefore($ugsRet, $ugsIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedUgsGetScaleMult))
    $ugsIl.InsertBefore($ugsRet, $ugsIl.Create([Mono.Cecil.Cil.OpCodes]::Mul))

    # Patch GuiFollow.Update to trigger static ctor via EnsureInit on first call
    $GuiFollowUpdate = $GuiFollowType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if ($GuiFollowUpdate -and $GuiFollowUpdate.HasBody) {
        $uguIl = $GuiFollowUpdate.Body.GetILProcessor()
        $uguFirst = $GuiFollowUpdate.Body.Instructions[0]
        $uguIl.InsertBefore($uguFirst, $uguIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedUgsEnsureInit))
    }
}

if ($WsiConfig.scale_enabled) {
    $WsiRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.WsiScaleRuntime" | Select-Object -First 1
    if (-not $WsiRuntimeType) { throw "WsiScaleRuntime helper type not found." }

    $ImportedWsiEnsureInit  = $Module.ImportReference(($WsiRuntimeType.Methods | Where-Object Name -eq "EnsureInit"   | Select-Object -First 1))
    $ImportedWsiApplyScale  = $Module.ImportReference(($WsiRuntimeType.Methods | Where-Object Name -eq "ApplyScale"   | Select-Object -First 1))

    # Patch GuiWorldSpaceIndicator.Awake — call ApplyScale(this) at the end, before ret
    $WsiType = $Module.Types | Where-Object Name -eq "GuiWorldSpaceIndicator" | Select-Object -First 1
    if (-not $WsiType) { throw "GuiWorldSpaceIndicator type not found." }

    $WsiAwake = $WsiType.Methods | Where-Object Name -eq "Awake" | Select-Object -First 1
    if (-not $WsiAwake -or -not $WsiAwake.HasBody) { throw "GuiWorldSpaceIndicator.Awake not found." }

    $wsiIl  = $WsiAwake.Body.GetILProcessor()
    $wsiRet = @($WsiAwake.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1

    # call EnsureInit to trigger static ctor, then call ApplyScale(this)
    $wsiIl.InsertBefore($wsiRet, $wsiIl.Create([Mono.Cecil.Cil.OpCodes]::Call,  $ImportedWsiEnsureInit))
    $wsiIl.InsertBefore($wsiRet, $wsiIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $wsiIl.InsertBefore($wsiRet, $wsiIl.Create([Mono.Cecil.Cil.OpCodes]::Call,  $ImportedWsiApplyScale))
}

if ($MapRenderConfig.enabled) {
    $MapRenderRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.MapRenderOverrideRuntime" | Select-Object -First 1
    if (-not $MapRenderRuntimeType) { throw "MapRenderOverrideRuntime helper type not found." }

    $ImportedMapRenderEnsureInit    = $Module.ImportReference(($MapRenderRuntimeType.Methods | Where-Object Name -eq "EnsureInit"         | Select-Object -First 1))
    $ImportedMapRenderGetOverride   = $Module.ImportReference(($MapRenderRuntimeType.Methods | Where-Object Name -eq "GetRenderOverride"  | Select-Object -First 1))

    # Patch MapWorld.UpdateRender(string prefab): inject EnsureInit at start,
    # then intercept the ZoneBuild.SetMapRender call's string argument.
    $MapWorldType = $Module.Types | Where-Object Name -eq "MapWorld" | Select-Object -First 1
    if (-not $MapWorldType) { throw "MapWorld type not found." }

    $UpdateRenderMethod = $MapWorldType.Methods | Where-Object Name -eq "UpdateRender" | Select-Object -First 1
    if (-not $UpdateRenderMethod -or -not $UpdateRenderMethod.HasBody) { throw "MapWorld.UpdateRender not found." }

    $mrIl = $UpdateRenderMethod.Body.GetILProcessor()

    # Inject EnsureInit at start
    $mrFirst = $UpdateRenderMethod.Body.Instructions[0]
    $mrIl.InsertBefore($mrFirst, $mrIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedMapRenderEnsureInit))

    # Find the call to ZoneBuild.SetMapRender and insert GetRenderOverride before it
    $SetMapRenderCall = $UpdateRenderMethod.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "SetMapRender"
    } | Select-Object -First 1
    if (-not $SetMapRenderCall) { throw "ZoneBuild.SetMapRender call not found in MapWorld.UpdateRender." }

    # Stack before SetMapRender: ..., prefab(string). Insert GetRenderOverride(prefab) -> string
    $mrIl.InsertBefore($SetMapRenderCall, $mrIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedMapRenderGetOverride))
}

if ($LocalBuildPreviewConfig.enabled) {
    $LbpRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.LocalBuildPredictionRuntime" | Select-Object -First 1
    if (-not $LbpRuntimeType) { throw "LocalBuildPredictionRuntime helper type not found." }

    $ImpOnLocalPlace              = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "OnLocalPlace"              | Select-Object -First 1))
    $ImpOnBlockUpdates            = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "OnBlockUpdates"            | Select-Object -First 1))
    $ImpOnDeviceBuilt             = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "OnDeviceBuilt"             | Select-Object -First 1))
    $ImpOnUnitCreate              = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "OnUnitCreate"              | Select-Object -First 1))
    $ImpShouldBypassBuildValidate = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "ShouldBypassBuildValidate" | Select-Object -First 1))
    $ImpShouldZeroBuildTime       = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "ShouldZeroBuildTime"       | Select-Object -First 1))
    $ImpGetBuildPrecastTime       = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "GetBuildPrecastTime"       | Select-Object -First 1))
    $ImpGetBuildTotalCastTime     = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "GetBuildTotalCastTime"     | Select-Object -First 1))
    $ImpShouldSkipWait            = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "ShouldSkipBuildCompletionWait" | Select-Object -First 1))
    $ImpTryInstantAcceptStartBuild = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "TryInstantAcceptStartBuild" | Select-Object -First 1))
    $ImpTryInstantAcceptSwitchGear = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "TryInstantAcceptSwitchGear" | Select-Object -First 1))
    $ImpOnStartBuildResult         = $Module.ImportReference(($LbpRuntimeType.Methods | Where-Object Name -eq "OnStartBuildResult"         | Select-Object -First 1))

    # BuildGhostController.Place � call OnLocalPlace after ServiceZone.Hit
    $BuildGhostControllerType = $Module.Types | Where-Object Name -eq "BuildGhostController" | Select-Object -First 1
    $PlaceMethod = $BuildGhostControllerType.Methods | Where-Object Name -eq "Place" | Select-Object -First 1
    if (-not $PlaceMethod -or -not $PlaceMethod.HasBody) { throw "BuildGhostController.Place not found." }
    $PlaceIl = $PlaceMethod.Body.GetILProcessor()
    $HitCall = $PlaceMethod.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Callvirt -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "Hit"
    } | Select-Object -First 1
    if (-not $HitCall) { throw "BuildGhostController.Place ServiceZone.Hit call not found." }
    Insert-After -Il $PlaceIl -Target $HitCall -Instructions @(
        $PlaceIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $PlaceIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpOnLocalPlace)
    )

    # ZoneServiceListener � BlockUpdates, DeviceBuilt, UnitCreate reconciliation
    $ZoneServiceListenerType = $Module.Types | Where-Object Name -eq "ZoneServiceListener" | Select-Object -First 1
    if (-not $ZoneServiceListenerType) { throw "ZoneServiceListener type not found." }

    $BlockUpdatesMethod = $ZoneServiceListenerType.Methods | Where-Object Name -eq "BlockUpdates" | Select-Object -First 1
    if (-not $BlockUpdatesMethod -or -not $BlockUpdatesMethod.HasBody) { throw "ZoneServiceListener.BlockUpdates not found." }
    $BlockUpdatesIl = $BlockUpdatesMethod.Body.GetILProcessor()
    Insert-Before -Il $BlockUpdatesIl -Target $BlockUpdatesMethod.Body.Instructions[0] -Instructions @(
        $BlockUpdatesIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
        $BlockUpdatesIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpOnBlockUpdates)
    )

    $DeviceBuiltMethod = $ZoneServiceListenerType.Methods | Where-Object Name -eq "DeviceBuilt" | Select-Object -First 1
    if (-not $DeviceBuiltMethod -or -not $DeviceBuiltMethod.HasBody) { throw "ZoneServiceListener.DeviceBuilt not found." }
    $DeviceBuiltIl = $DeviceBuiltMethod.Body.GetILProcessor()
    Insert-Before -Il $DeviceBuiltIl -Target $DeviceBuiltMethod.Body.Instructions[0] -Instructions @(
        $DeviceBuiltIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
        $DeviceBuiltIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2),
        $DeviceBuiltIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_3),
        $DeviceBuiltIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpOnDeviceBuilt)
    )

    $UnitCreateMethod = $ZoneServiceListenerType.Methods | Where-Object Name -eq "UnitCreate" | Select-Object -First 1
    if (-not $UnitCreateMethod -or -not $UnitCreateMethod.HasBody) { throw "ZoneServiceListener.UnitCreate not found." }
    $UnitCreateIl = $UnitCreateMethod.Body.GetILProcessor()
    Insert-Before -Il $UnitCreateIl -Target $UnitCreateMethod.Body.Instructions[0] -Instructions @(
        $UnitCreateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2),
        $UnitCreateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpOnUnitCreate)
    )

    # ServiceZone.StartBuild � inject TryInstantAcceptStartBuild before return
    $ServiceZoneType = $Module.Types | Where-Object Name -eq "ServiceZone" | Select-Object -First 1
    $StartBuildMethod = $ServiceZoneType.Methods | Where-Object Name -eq "StartBuild" | Select-Object -First 1
    if (-not $StartBuildMethod -or -not $StartBuildMethod.HasBody) { throw "ServiceZone.StartBuild not found." }
    $StartBuildIl = $StartBuildMethod.Body.GetILProcessor()
    $StartBuildRet = $StartBuildMethod.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -First 1
    Insert-Before -Il $StartBuildIl -Target $StartBuildRet -Instructions @(
        $StartBuildIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
        $StartBuildIl.Create([Mono.Cecil.Cil.OpCodes]::Ldloc_0),
        $StartBuildIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpTryInstantAcceptStartBuild)
    )

    # Rpc_StartBuild._Success — notify OnStartBuildResult so server rejection rolls back the local block
    $RpcStartBuildType = $ServiceZoneType.NestedTypes | Where-Object Name -eq "Rpc_StartBuild" | Select-Object -First 1
    if (-not $RpcStartBuildType) { throw "ServiceZone.Rpc_StartBuild nested type not found." }
    $RpcSuccessMethod = $RpcStartBuildType.Methods | Where-Object Name -eq "_Success" | Select-Object -First 1
    if (-not $RpcSuccessMethod -or -not $RpcSuccessMethod.HasBody) { throw "Rpc_StartBuild._Success not found." }
    $RpcSuccessIl = $RpcSuccessMethod.Body.GetILProcessor()
    Insert-Before -Il $RpcSuccessIl -Target $RpcSuccessMethod.Body.Instructions[0] -Instructions @(
        $RpcSuccessIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $RpcSuccessIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1),
        $RpcSuccessIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpOnStartBuildResult)
    )

    # BuffHelper.BuildTime � zero build time for instant-placement devices
    $BuffHelperType = $Module.Types | Where-Object Name -eq "BuffHelper" | Select-Object -First 1
    $BuildTimeMethod = $BuffHelperType.Methods | Where-Object Name -eq "BuildTime" | Select-Object -First 1
    if (-not $BuildTimeMethod -or -not $BuildTimeMethod.HasBody) { throw "BuffHelper.BuildTime not found." }
    $BuildTimeIl = $BuildTimeMethod.Body.GetILProcessor()
    Inject-FloatZeroBypass -Method $BuildTimeMethod -PrefixInstructions @(
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2),
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpShouldZeroBuildTime)
    )

    # ToolLogicBuild.ValidateUse � bypass validation for instant-placement
    $ToolLogicBuildType = $Module.Types | Where-Object Name -eq "ToolLogicBuild" | Select-Object -First 1
    $ValidateUseMethod = $ToolLogicBuildType.Methods | Where-Object Name -eq "ValidateUse" | Select-Object -First 1
    if (-not $ValidateUseMethod -or -not $ValidateUseMethod.HasBody) { throw "ToolLogicBuild.ValidateUse not found." }
    Inject-BoolTrueBypass -Method $ValidateUseMethod -Call $ImpShouldBypassBuildValidate -LoadArg ([Mono.Cecil.Cil.OpCodes]::Ldarg_0)

    # PlayerActSwitch coroutine � instant gear switch
    $PlayerActType = $Module.Types | Where-Object Name -eq "PlayerAct" | Select-Object -First 1
    $PlayerActUnitField = $PlayerActType.Fields | Where-Object Name -eq "unit" | Select-Object -First 1
    $PlayerActSwitchType = $Module.Types | Where-Object Name -eq "PlayerActSwitch" | Select-Object -First 1
    $SwitchIteratorType = $PlayerActSwitchType.NestedTypes | Where-Object Name -eq "<DoAction>c__Iterator4B" | Select-Object -First 1
    $SwitchMoveNext = $SwitchIteratorType.Methods | Where-Object Name -eq "MoveNext" | Select-Object -First 1
    if (-not $SwitchMoveNext -or -not $SwitchMoveNext.HasBody) { throw "PlayerActSwitch/<DoAction>c__Iterator4B.MoveNext not found." }
    $SwitchRpcStore = $SwitchMoveNext.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
        $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.FullName -like "*PlayerActSwitch/<DoAction>c__Iterator4B::<rpc>__*"
    } | Select-Object -First 1
    if (-not $SwitchRpcStore) { throw "PlayerActSwitch RPC store not found." }
    $SwitchIl = $SwitchMoveNext.Body.GetILProcessor()
    Insert-After -Il $SwitchIl -Target $SwitchRpcStore -Instructions @(
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, ($SwitchIteratorType.Fields | Where-Object Name -eq "<>f__this" | Select-Object -First 1)),
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $PlayerActUnitField),
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, ($SwitchIteratorType.Fields | Where-Object Name -like "<rpc>__*" | Select-Object -First 1)),
        $SwitchIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpTryInstantAcceptSwitchGear)
    )

    # ToolLogicBuild coroutines � replace timing helpers to zero build time
    $ProjectileIteratorType = $ToolLogicBuildType.NestedTypes | Where-Object Name -eq "<DoUseProjectile>c__IteratorC2" | Select-Object -First 1
    $ProjectileMoveNext = $ProjectileIteratorType.Methods | Where-Object Name -eq "MoveNext" | Select-Object -First 1
    if (-not $ProjectileMoveNext -or -not $ProjectileMoveNext.HasBody) { throw "ToolLogicBuild/<DoUseProjectile>c__IteratorC2.MoveNext not found." }
    Replace-MethodCalls -Method $ProjectileMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetTotalCastTime" -ReplacementCall $ImpGetBuildTotalCastTime
    Replace-MethodCalls -Method $ProjectileMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetPrecastTime" -ReplacementCall $ImpGetBuildPrecastTime

    $RotationLockIteratorType = $ToolLogicBuildType.NestedTypes | Where-Object Name -eq "<DoUseRotationLock>c__IteratorC3" | Select-Object -First 1
    $RotationLockMoveNext = $RotationLockIteratorType.Methods | Where-Object Name -eq "MoveNext" | Select-Object -First 1
    if (-not $RotationLockMoveNext -or -not $RotationLockMoveNext.HasBody) { throw "ToolLogicBuild/<DoUseRotationLock>c__IteratorC3.MoveNext not found." }
    Replace-MethodCalls -Method $RotationLockMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetTotalCastTime" -ReplacementCall $ImpGetBuildTotalCastTime
    Replace-MethodCalls -Method $RotationLockMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetPrecastTime" -ReplacementCall $ImpGetBuildPrecastTime
    $RotationLockLastShotStore = $RotationLockMoveNext.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
        $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.FullName -eq "System.Single GearData::LastShotEndTime"
    } | Select-Object -Last 1
    $RotationLockReturnTarget = $RotationLockMoveNext.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_M1 } | Select-Object -Last 1
    Inject-ImmediateReturnAfterInstruction -Method $RotationLockMoveNext -AfterInstruction $RotationLockLastShotStore -ConditionCall $ImpShouldSkipWait -ReturnTarget $RotationLockReturnTarget

    $RotationFreeIteratorType = $ToolLogicBuildType.NestedTypes | Where-Object Name -eq "<DoUseRotationFree>c__IteratorC4" | Select-Object -First 1
    $RotationFreeMoveNext = $RotationFreeIteratorType.Methods | Where-Object Name -eq "MoveNext" | Select-Object -First 1
    if (-not $RotationFreeMoveNext -or -not $RotationFreeMoveNext.HasBody) { throw "ToolLogicBuild/<DoUseRotationFree>c__IteratorC4.MoveNext not found." }
    Replace-MethodCalls -Method $RotationFreeMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetTotalCastTime" -ReplacementCall $ImpGetBuildTotalCastTime
    Replace-MethodCalls -Method $RotationFreeMoveNext -OriginalTypeName "ToolTimingHelper" -OriginalMethodName "GetPrecastTime" -ReplacementCall $ImpGetBuildPrecastTime
    $RotationFreeLastShotStore = $RotationFreeMoveNext.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
        $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.FullName -eq "System.Single GearData::LastShotEndTime"
    } | Select-Object -Last 1
    $RotationFreeReturnTarget = $RotationFreeMoveNext.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_M1 } | Select-Object -Last 1
    Inject-ImmediateReturnAfterInstruction -Method $RotationFreeMoveNext -AfterInstruction $RotationFreeLastShotStore -ConditionCall $ImpShouldSkipWait -ReturnTarget $RotationFreeReturnTarget
}

if ($AimHealthbarConfig.enabled) {
    $GuiHealthbarType = $Module.Types | Where-Object Name -eq "GuiHealthbar" | Select-Object -First 1
    if (-not $GuiHealthbarType) { throw "GuiHealthbar type not found in assembly." }

    # --- Gather fields and types needed ---
    $UnitField = $GuiHealthbarType.Fields | Where-Object Name -eq "unit" | Select-Object -First 1
    if (-not $UnitField) { throw "GuiHealthbar.unit field not found." }

    $ContentField = $GuiHealthbarType.Fields | Where-Object Name -eq "Content" | Select-Object -First 1
    if (-not $ContentField) { throw "GuiHealthbar.Content field not found." }

    $CanvasGroupType = $ContentField.FieldType.Resolve()
    if (-not $CanvasGroupType) { throw "CanvasGroup type could not be resolved." }

    $SetAlphaMethod = $CanvasGroupType.Methods | Where-Object Name -eq "set_alpha" | Select-Object -First 1
    if (-not $SetAlphaMethod) { throw "CanvasGroup.set_alpha not found." }

    $ImportedUnitField        = $Module.ImportReference($UnitField)
    $ImportedContentField     = $Module.ImportReference($ContentField)
    $ImportedSetAlpha         = $Module.ImportReference($SetAlphaMethod)
    $AimHealthbarRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.AimHealthbarRuntime" | Select-Object -First 1
    if (-not $AimHealthbarRuntimeType) { throw "AimHealthbarRuntime type not found in helper assembly." }
    $ImportedShouldShowAimHealthbar = $Module.ImportReference(($AimHealthbarRuntimeType.Methods | Where-Object Name -eq "ShouldShow" | Select-Object -First 1))

    $AvailMethod = $GuiHealthbarType.Methods | Where-Object Name -eq "IsUnitAvailableForShow" | Select-Object -First 1
    if (-not $AvailMethod -or -not $AvailMethod.HasBody) { throw "GuiHealthbar.IsUnitAvailableForShow not found." }

    $AvailIl = $AvailMethod.Body.GetILProcessor()
    $FirstInstr = $AvailMethod.Body.Instructions | Select-Object -First 1

    $BranchNotThisUnit = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $FirstInstr)

    $Instrs = @(
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld,  $ImportedUnitField),
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Call,   $ImportedShouldShowAimHealthbar),
        $BranchNotThisUnit,
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ret)
    )

    foreach ($instr in $Instrs) {
        $AvailIl.InsertBefore($FirstInstr, $instr)
    }

    # --- Patch AlphaUpdate(): at the top, if showNameByCrosshair set showTime=1 so the existing
    # showTime>0 fade-in path keeps alpha at 1 each frame while aiming.
    # Branching directly to set_alpha(1) was insufficient � the next frame showTime=0 triggered
    # the fade-out path and overwrote alpha back to 0.
    $ShowNameByCrosshairField = $GuiHealthbarType.Fields | Where-Object Name -eq "showNameByCrosshair" | Select-Object -First 1
    $ImportedShowNameField = $Module.ImportReference($ShowNameByCrosshairField)

    $ShowTimeField = $GuiHealthbarType.Fields | Where-Object Name -eq "showTime" | Select-Object -First 1
    if (-not $ShowTimeField) { throw "GuiHealthbar.showTime field not found." }
    $ImportedShowTimeField = $Module.ImportReference($ShowTimeField)

    $AlphaUpdateMethod = $GuiHealthbarType.Methods | Where-Object Name -eq "AlphaUpdate" | Select-Object -First 1
    if (-not $AlphaUpdateMethod -or -not $AlphaUpdateMethod.HasBody) { throw "GuiHealthbar.AlphaUpdate not found." }
    $AlphaIl = $AlphaUpdateMethod.Body.GetILProcessor()
    $AlphaInstrs = @($AlphaUpdateMethod.Body.Instructions)
    $AlphaFirstInstr = $AlphaInstrs | Select-Object -First 1

    $BranchToOriginal = $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $AlphaFirstInstr)

    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $ImportedUnitField))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedShouldShowAimHealthbar))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $BranchToOriginal)
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]1.0))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Stfld, $ImportedShowTimeField))
}

if ($DeathCamHealthbarConfig.enabled) {
    $DeathCamRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.DeathCamRuntime" | Select-Object -First 1
    if (-not $DeathCamRuntimeType) { throw "DeathCamRuntime type not found in helper assembly." }

    $IsDeathCamFriendly = $DeathCamRuntimeType.Methods | Where-Object Name -eq "IsDeathCamFriendly" | Select-Object -First 1
    if (-not $IsDeathCamFriendly) { throw "DeathCamRuntime.IsDeathCamFriendly method not found." }
    $ImportedIsDeathCamFriendly = $Module.ImportReference($IsDeathCamFriendly)

    $UpdateDeathCamHpTextMethod = $DeathCamRuntimeType.Methods | Where-Object Name -eq "UpdateDeathCamHpText" | Select-Object -First 1
    if (-not $UpdateDeathCamHpTextMethod) { throw "DeathCamRuntime.UpdateDeathCamHpText method not found." }
    $ImportedUpdateDeathCamHpText = $Module.ImportReference($UpdateDeathCamHpTextMethod)

    # Patch GuiDeathCameraTargets.Update(): at the end (before final ret), inject call
    # to DeathCamRuntime.UpdateDeathCamHpText(Nickname) to display spectated player HP
    $DeathCamTargetsType = $Module.Types | Where-Object Name -eq "GuiDeathCameraTargets" | Select-Object -First 1
    if (-not $DeathCamTargetsType) { throw "GuiDeathCameraTargets type not found." }

    $GuiDeathCamUpdateMethod = $DeathCamTargetsType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if (-not $GuiDeathCamUpdateMethod -or -not $GuiDeathCamUpdateMethod.HasBody) { throw "GuiDeathCameraTargets.Update not found." }

    $DeathCamUpdateIl = $GuiDeathCamUpdateMethod.Body.GetILProcessor()
    $DeathCamUpdateRet = @($GuiDeathCamUpdateMethod.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
    if (-not $DeathCamUpdateRet) { throw "GuiDeathCameraTargets.Update Ret not found." }

    $NicknameField = $DeathCamTargetsType.Fields | Where-Object Name -eq "Nickname" | Select-Object -First 1
    if (-not $NicknameField) { throw "GuiDeathCameraTargets.Nickname field not found." }
    $ImportedNicknameField = $Module.ImportReference($NicknameField)

    # Inject before the last ret:
    #   ldarg.0
    #   ldfld Nickname
    #   call void DeathCamRuntime::UpdateDeathCamHpText(Text)
    $DeathCamUpdateIl.InsertBefore($DeathCamUpdateRet, $DeathCamUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $DeathCamUpdateIl.InsertBefore($DeathCamUpdateRet, $DeathCamUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $ImportedNicknameField))
    $DeathCamUpdateIl.InsertBefore($DeathCamUpdateRet, $DeathCamUpdateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedUpdateDeathCamHpText))

    # --- Also patch IsUnitAvailableForShow() and AlphaUpdate() for death cam ---
    # Try to patch IsUnitAvailableForShow if GuiHealthbar was already resolved by aim healthbar
    $GuiHealthbarType = $Module.Types | Where-Object Name -eq "GuiHealthbar" | Select-Object -First 1
    if ($GuiHealthbarType) {
        $AvailMethodDeathCam = $GuiHealthbarType.Methods | Where-Object Name -eq "IsUnitAvailableForShow" | Select-Object -First 1
        if ($AvailMethodDeathCam -and $AvailMethodDeathCam.HasBody) {
            $AvailIlDeathCam = $AvailMethodDeathCam.Body.GetILProcessor()
            $FirstInstrDeathCam = $AvailMethodDeathCam.Body.Instructions | Select-Object -First 1
            $BranchToOriginalDeathCam = $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $FirstInstrDeathCam)
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, ($GuiHealthbarType.Fields | Where-Object Name -eq "unit" | Select-Object -First 1)))
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedIsDeathCamFriendly))
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $BranchToOriginalDeathCam)
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1))
            $AvailIlDeathCam.InsertBefore($FirstInstrDeathCam, $AvailIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ret))

            # Also patch AlphaUpdate: keep showTime=1 during death cam
            $AlphaMethodDeathCam = $GuiHealthbarType.Methods | Where-Object Name -eq "AlphaUpdate" | Select-Object -First 1
            if ($AlphaMethodDeathCam -and $AlphaMethodDeathCam.HasBody) {
                $AlphaIlDeathCam = $AlphaMethodDeathCam.Body.GetILProcessor()
                $AlphaFirstInstrDeathCam = $AlphaMethodDeathCam.Body.Instructions | Select-Object -First 1
                $BranchAlphaOrig = $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $AlphaFirstInstrDeathCam)
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, ($GuiHealthbarType.Fields | Where-Object Name -eq "unit" | Select-Object -First 1)))
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedIsDeathCamFriendly))
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $BranchAlphaOrig)
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]1.0))
                $AlphaIlDeathCam.InsertBefore($AlphaFirstInstrDeathCam, $AlphaIlDeathCam.Create([Mono.Cecil.Cil.OpCodes]::Stfld, ($GuiHealthbarType.Fields | Where-Object Name -eq "showTime" | Select-Object -First 1)))
            }
        }
    }

    # Attach DeathCamHealthbarController to every GuiHealthbar via Start()
    $DeathCamCtrlType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.DeathCamHealthbarController" | Select-Object -First 1
    if ($DeathCamCtrlType) {
        $AttachDeathCamMethod = ($DeathCamRuntimeType.Methods | Where-Object Name -eq "AttachDeathCamController" | Select-Object -First 1)
        if ($AttachDeathCamMethod) {
            $ImportedAttachDeathCam = $Module.ImportReference($AttachDeathCamMethod)
            $GuiHealthbarTypeForCtrl = $Module.Types | Where-Object Name -eq "GuiHealthbar" | Select-Object -First 1
            if ($GuiHealthbarTypeForCtrl) {
                $GuiHealthbarStartForCtrl = $GuiHealthbarTypeForCtrl.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
                if ($GuiHealthbarStartForCtrl -and $GuiHealthbarStartForCtrl.HasBody) {
                    $StartIlCtrl = $GuiHealthbarStartForCtrl.Body.GetILProcessor()
                    $StartRetCtrl = @($GuiHealthbarStartForCtrl.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
                    if ($StartRetCtrl) {
                        $StartIlCtrl.InsertBefore($StartRetCtrl, $StartIlCtrl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
                        $StartIlCtrl.InsertBefore($StartRetCtrl, $StartIlCtrl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedAttachDeathCam))
                    }
                }
            }
        }
    }
}

if ($FriendlyLowHealthConfig.enabled) {
    $FriendlyLowHealthRuntimeType2 = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.FriendlyLowHealthRuntime" | Select-Object -First 1
    if (-not $FriendlyLowHealthRuntimeType2) { throw "FriendlyLowHealthRuntime type not found in helper assembly." }
    $IsFriendlyLowHealthMethod = $FriendlyLowHealthRuntimeType2.Methods | Where-Object Name -eq "IsFriendlyLowHealth" | Select-Object -First 1
    if (-not $IsFriendlyLowHealthMethod) { throw "FriendlyLowHealthRuntime.IsFriendlyLowHealth method not found." }
    $ImportedIsFriendlyLowHealth = $Module.ImportReference($IsFriendlyLowHealthMethod)

    $GuiHealthbarTypeFLH = $Module.Types | Where-Object Name -eq "GuiHealthbar" | Select-Object -First 1
    if ($GuiHealthbarTypeFLH) {
        $UnitFieldFLH   = $GuiHealthbarTypeFLH.Fields | Where-Object Name -eq "unit"     | Select-Object -First 1
        $ShowTimeFieldFLH = $GuiHealthbarTypeFLH.Fields | Where-Object Name -eq "showTime" | Select-Object -First 1

        # Patch IsUnitAvailableForShow(): return true early for friendly low-health units
        # Pass 'this' (GuiHealthbar) so IsFriendlyLowHealth can check camera visibility
        $AvailMethodFLH = $GuiHealthbarTypeFLH.Methods | Where-Object Name -eq "IsUnitAvailableForShow" | Select-Object -First 1
        if ($AvailMethodFLH -and $AvailMethodFLH.HasBody) {
            $AvailIlFLH    = $AvailMethodFLH.Body.GetILProcessor()
            $FirstInstrFLH = $AvailMethodFLH.Body.Instructions | Select-Object -First 1
            $BranchFLH     = $AvailIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $FirstInstrFLH)
            $AvailIlFLH.InsertBefore($FirstInstrFLH, $AvailIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $AvailIlFLH.InsertBefore($FirstInstrFLH, $AvailIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedIsFriendlyLowHealth))
            $AvailIlFLH.InsertBefore($FirstInstrFLH, $BranchFLH)
            $AvailIlFLH.InsertBefore($FirstInstrFLH, $AvailIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1))
            $AvailIlFLH.InsertBefore($FirstInstrFLH, $AvailIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        }

        # Patch AlphaUpdate(): keep showTime=1 each frame so the bar never fades out
        $AlphaMethodFLH = $GuiHealthbarTypeFLH.Methods | Where-Object Name -eq "AlphaUpdate" | Select-Object -First 1
        if ($AlphaMethodFLH -and $AlphaMethodFLH.HasBody -and $ShowTimeFieldFLH) {
            $AlphaIlFLH       = $AlphaMethodFLH.Body.GetILProcessor()
            $AlphaFirstFLH    = $AlphaMethodFLH.Body.Instructions | Select-Object -First 1
            $BranchAlphaFLH   = $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $AlphaFirstFLH)
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedIsFriendlyLowHealth))
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $BranchAlphaFLH)
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]1.0))
            $AlphaIlFLH.InsertBefore($AlphaFirstFLH, $AlphaIlFLH.Create([Mono.Cecil.Cil.OpCodes]::Stfld, $Module.ImportReference($ShowTimeFieldFLH)))
        }
    }
}

if ($AutoCrouchEnabled) {
    $AutoCrouchRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.AutoCrouchRuntime" | Select-Object -First 1
    if (-not $AutoCrouchRuntimeType) { throw "AutoCrouchRuntime type not found in helper assembly." }
    $IsPossibleToStayHelperMethod = $AutoCrouchRuntimeType.Methods | Where-Object Name -eq "IsPossibleToStayForAutoCrouch" | Select-Object -First 1
    if (-not $IsPossibleToStayHelperMethod) { throw "AutoCrouchRuntime.IsPossibleToStayForAutoCrouch not found." }
    $ImportedIsPossibleToStayForAutoCrouch = $Module.ImportReference($IsPossibleToStayHelperMethod)

    # Find MovementController.IsPossibleToStay so we can locate its call site in PlayerMovementGroundMove.Update
    $MovementControllerType = $Module.Types | Where-Object Name -eq "MovementController" | Select-Object -First 1
    if (-not $MovementControllerType) { throw "MovementController type not found." }
    $OriginalIsPossibleToStayMethod = $MovementControllerType.Methods | Where-Object { $_.Name -eq "IsPossibleToStay" -and -not $_.IsStatic } | Select-Object -First 1
    if (-not $OriginalIsPossibleToStayMethod) { throw "MovementController.IsPossibleToStay not found." }

    # Find PlayerMovementGroundMove.Update and replace the IsPossibleToStay call with our wrapper.
    # The call appears in the condition: CrouchHold || !IsPossibleToStay()
    # We replace the callvirt/call to IsPossibleToStay with a call to IsPossibleToStayForAutoCrouch,
    # which takes the same argument (ldarg.0 = the MovementController via 'this.controller').
    $GroundMoveType = $Module.Types | Where-Object Name -eq "PlayerMovementGroundMove" | Select-Object -First 1
    if (-not $GroundMoveType) { throw "PlayerMovementGroundMove type not found." }
    $GroundMoveUpdateMethod = $GroundMoveType.Methods | Where-Object { $_.Name -eq "Update" -and -not $_.IsStatic } | Select-Object -First 1
    if (-not $GroundMoveUpdateMethod -or -not $GroundMoveUpdateMethod.HasBody) { throw "PlayerMovementGroundMove.Update not found." }

    $GroundMoveInstructions = $GroundMoveUpdateMethod.Body.Instructions
    $IsPossibleToStayCallInstr = $null
    for ($i = 0; $i -lt $GroundMoveInstructions.Count; $i++) {
        $instr = $GroundMoveInstructions[$i]
        if (($instr.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -or $instr.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Callvirt) `
            -and $instr.Operand -and $instr.Operand.Name -eq "IsPossibleToStay") {
            $IsPossibleToStayCallInstr = $instr
            break
        }
    }
    if (-not $IsPossibleToStayCallInstr) { throw "IsPossibleToStay call not found in PlayerMovementGroundMove.Update." }

    # Replace the call operand: same opcode, new target pointing to our wrapper
    $IsPossibleToStayCallInstr.OpCode = [Mono.Cecil.Cil.OpCodes]::Call
    $IsPossibleToStayCallInstr.Operand = $ImportedIsPossibleToStayForAutoCrouch
}

if ($TeammateHpEnabled) {
    $TeammateHpRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.TeammateHpRuntime" | Select-Object -First 1
    if (-not $TeammateHpRuntimeType) { throw "TeammateHpRuntime type not found in helper assembly." }
    $UpdateTeammateHpTextMethod = $TeammateHpRuntimeType.Methods | Where-Object Name -eq "UpdateTeammateHpText" | Select-Object -First 1
    if (-not $UpdateTeammateHpTextMethod) { throw "TeammateHpRuntime.UpdateTeammateHpText not found." }
    $ImportedUpdateTeammateHpText = $Module.ImportReference($UpdateTeammateHpTextMethod)

    $GuiTeammateType = $Module.Types | Where-Object Name -eq "GuiTeammate" | Select-Object -First 1
    if (-not $GuiTeammateType) { throw "GuiTeammate type not found." }
    $GuiTeammateUpdateMethod = $GuiTeammateType.Methods | Where-Object { $_.Name -eq "Update" -and -not $_.IsStatic } | Select-Object -First 1
    if (-not $GuiTeammateUpdateMethod -or -not $GuiTeammateUpdateMethod.HasBody) { throw "GuiTeammate.Update not found." }

    $TeammateIl = $GuiTeammateUpdateMethod.Body.GetILProcessor()
    $TeammateRet = @($GuiTeammateUpdateMethod.Body.Instructions) | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -Last 1
    if (-not $TeammateRet) { throw "GuiTeammate.Update Ret not found." }

    # Inject before last ret: ldarg.0 / call TeammateHpRuntime::UpdateTeammateHpText(GuiTeammate)
    $TeammateIl.InsertBefore($TeammateRet, $TeammateIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $TeammateIl.InsertBefore($TeammateRet, $TeammateIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedUpdateTeammateHpText))
}

if ($AutoCasualQueueConfig.enabled) {
    $AutoCasualQueueRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.AutoCasualQueueRuntime" | Select-Object -First 1
    if (-not $AutoCasualQueueRuntimeType) { throw "AutoCasualQueueRuntime type not found in helper assembly." }
    $EnsureAutoCasualQueueMethod = $AutoCasualQueueRuntimeType.Methods | Where-Object Name -eq "EnsureInstance" | Select-Object -First 1
    if (-not $EnsureAutoCasualQueueMethod) { throw "AutoCasualQueueRuntime.EnsureInstance not found." }
    $ImportedEnsureAutoCasualQueue = $Module.ImportReference($EnsureAutoCasualQueueMethod)

    $MainMenuType2 = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    $MainMenuStartMethod2 = $MainMenuType2.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    $MainMenuStartIl2 = $MainMenuStartMethod2.Body.GetILProcessor()
    $MainMenuStartFirst2 = $MainMenuStartMethod2.Body.Instructions | Select-Object -First 1
    $MainMenuStartIl2.InsertBefore($MainMenuStartFirst2, $MainMenuStartIl2.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedEnsureAutoCasualQueue))
}

# Disable frame cap on main menu — patch SceneManager::ServerLoadMainMenu and ServerLoadLobby
# to pass -1 instead of 60 to SetTargetFramerate::.ctor so the cap is never applied.
if ($DisableMainMenuFrameCapEnabled) {
    $FcSceneManagerType = $Module.Types | Where-Object Name -eq "SceneManager" | Select-Object -First 1
    if (-not $FcSceneManagerType) { throw "SceneManager type not found." }

    foreach ($FcMethodName in @("ServerLoadMainMenu", "ServerLoadLobby")) {
        $FcMethod = $FcSceneManagerType.Methods | Where-Object Name -eq $FcMethodName | Select-Object -First 1
        if (-not $FcMethod -or -not $FcMethod.HasBody) { throw "$FcMethodName not found." }
        # Find the ldc.i4.s 60 immediately before the SetTargetFramerate newobj call
        # and mutate it in-place to ldc.i4.m1 (-1)
        $FcInstructions = $FcMethod.Body.Instructions
        for ($i = 1; $i -lt $FcInstructions.Count; $i++) {
            $instr = $FcInstructions[$i]
            if ($instr.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and
                $instr.Operand -and $instr.Operand.ToString() -match "SetTargetFramerate") {
                $prev = $FcInstructions[$i - 1]
                $prev.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I4_M1
                $prev.Operand = $null
                break
            }
        }
    }
}

# DPS Overlay — always active, hooks OnGlobalUnitDamage unconditionally
if ($true) {
    $DpsRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.DpsOverlayRuntime" | Select-Object -First 1
    if (-not $DpsRuntimeType) { throw "DpsOverlayRuntime helper type not found." }

    $ImportedDpsEnsureInit   = $Module.ImportReference(($DpsRuntimeType.Methods | Where-Object Name -eq "EnsureInit"      | Select-Object -First 1))
    $ImportedDpsTryRecord    = $Module.ImportReference(($DpsRuntimeType.Methods | Where-Object Name -eq "TryRecordDamage" | Select-Object -First 1))

    $DpsGuiDetectorType = $Module.Types | Where-Object Name -eq "GuiDamageNumberDetector" | Select-Object -First 1
    if (-not $DpsGuiDetectorType) { throw "GuiDamageNumberDetector type not found for DPS patch." }
    $DpsOnGlobalUnitDamageMethod = $DpsGuiDetectorType.Methods | Where-Object Name -eq "OnGlobalUnitDamage" | Select-Object -First 1
    if (-not $DpsOnGlobalUnitDamageMethod -or -not $DpsOnGlobalUnitDamageMethod.HasBody) { throw "OnGlobalUnitDamage not found for DPS patch." }

    # Inject EnsureInit into MainMenu.Start so the ctor fires at startup (main menu loads
    # before any match), making the F8 menu entry visible immediately.
    $DpsMainMenuType = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
    if ($DpsMainMenuType) {
        $DpsMainMenuStartMethod = $DpsMainMenuType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
        if ($DpsMainMenuStartMethod -and $DpsMainMenuStartMethod.HasBody) {
            $DpsMainMenuIl = $DpsMainMenuStartMethod.Body.GetILProcessor()
            $DpsMainMenuFirst = $DpsMainMenuStartMethod.Body.Instructions[0]
            $DpsMainMenuIl.InsertBefore($DpsMainMenuFirst, $DpsMainMenuIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedDpsEnsureInit))
        }
    }

    # Also inject into GuiDamageNumberDetector.Start as a fallback for scene reloads
    $DpsStartMethod = $DpsGuiDetectorType.Methods | Where-Object Name -eq "Start" | Select-Object -First 1
    if ($DpsStartMethod -and $DpsStartMethod.HasBody) {
        $DpsStartIl = $DpsStartMethod.Body.GetILProcessor()
        $DpsStartFirst = $DpsStartMethod.Body.Instructions[0]
        $DpsStartIl.InsertBefore($DpsStartFirst, $DpsStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedDpsEnsureInit))
    }

    $DpsIl = $DpsOnGlobalUnitDamageMethod.Body.GetILProcessor()

    # Call TryRecordDamage(args) before the last ret only — it does its own player/enabled checks
    $DpsLastRet = @($DpsOnGlobalUnitDamageMethod.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1
    $DpsIl.InsertBefore($DpsLastRet, $DpsIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
    $DpsIl.InsertBefore($DpsLastRet, $DpsIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedDpsTryRecord))
}

# Skip intro — patch GuiLoginIntro.Start() to immediately call FinishWarning() + FinishIntro()
if ($SkipIntroEnabled) {
    $GuiLoginIntroType = $Module.Types | Where-Object Name -eq "GuiLoginIntro" | Select-Object -First 1
    if (-not $GuiLoginIntroType) { throw "GuiLoginIntro type not found." }
    $IntroStartMethod     = $GuiLoginIntroType.Methods | Where-Object Name -eq "Start"         | Select-Object -First 1
    $FinishWarningMethod  = $GuiLoginIntroType.Methods | Where-Object Name -eq "FinishWarning"  | Select-Object -First 1
    $FinishIntroMethod    = $GuiLoginIntroType.Methods | Where-Object Name -eq "FinishIntro"    | Select-Object -First 1
    if (-not $IntroStartMethod -or -not $FinishWarningMethod -or -not $FinishIntroMethod) { throw "GuiLoginIntro methods not found." }
    $IntroStartIl  = $IntroStartMethod.Body.GetILProcessor()
    $IntroStartRet = $IntroStartMethod.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret } | Select-Object -First 1
    Insert-Before -Il $IntroStartIl -Target $IntroStartRet -Instructions @(
        $IntroStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $IntroStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $FinishWarningMethod),
        $IntroStartIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $IntroStartIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $FinishIntroMethod)
    )
}

# -----------------------------------------------------------------------
# Null-guard patches — suppress per-frame exceptions during replay
# -----------------------------------------------------------------------

# 1. TeamFieldOfView.IsInVisionSector — guard viewer/target null (including destroyed Unity objects)
# Uses UnityEngine.Object::op_Equality(obj, null) which returns true for destroyed objects.
# op_Equality is resolved from UnityEngine.dll directly to get a clean resolvable MethodReference.
# Pattern per arg: ldarg / ldnull / call op_Equality / brfalse <skip> / ldc.i4.0 / ret / <skip>: ...original...
$TfovType = $Module.Types | Where-Object Name -eq "TeamFieldOfView" | Select-Object -First 1
if ($TfovType) {
    $IsInVisionSector = $TfovType.Methods | Where-Object Name -eq "IsInVisionSector" | Select-Object -First 1
    if ($IsInVisionSector) {
        $Il = $IsInVisionSector.Body.GetILProcessor()

        # Resolve op_Equality from UnityEngine.dll
        $UnityEngineAsm   = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($UnityEngineDll)
        $UnityObjectType  = $UnityEngineAsm.MainModule.Types | Where-Object FullName -eq "UnityEngine.Object" | Select-Object -First 1
        $OpEqMethod       = $UnityObjectType.Methods | Where-Object { $_.Name -eq "op_Equality" } | Select-Object -First 1
        $OpEq             = $Module.ImportReference($OpEqMethod)
        $UnityEngineAsm.Dispose()

        foreach ($argOpCode in @([Mono.Cecil.Cil.OpCodes]::Ldarg_2, [Mono.Cecil.Cil.OpCodes]::Ldarg_1)) {
            $originalFirst = @($IsInVisionSector.Body.Instructions)[0]
            $retFalse = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0)
            $retInstr = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ret)
            $brfalse  = $Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $originalFirst)
            $callEq   = $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $OpEq)
            $ldnull   = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldnull)
            $ldarg    = $Il.Create($argOpCode)
            $Il.InsertBefore($originalFirst, $retInstr)
            $Il.InsertBefore($retInstr,      $retFalse)
            $Il.InsertBefore($retFalse, $brfalse)
            $Il.InsertBefore($brfalse,  $callEq)
            $Il.InsertBefore($callEq,   $ldnull)
            $Il.InsertBefore($ldnull,   $ldarg)
        }
        Write-Output "[NullGuard] Patched TeamFieldOfView.IsInVisionSector"
    }

    # IsVisibleTroughBlocks(Vector3s viewPoint, Unit target) — guard target (arg2) null
    $IsVisibleTroughBlocks = $TfovType.Methods | Where-Object Name -eq "IsVisibleTroughBlocks" | Select-Object -First 1
    if ($IsVisibleTroughBlocks) {
        $Il2           = $IsVisibleTroughBlocks.Body.GetILProcessor()
        $OriginalFirst = @($IsVisibleTroughBlocks.Body.Instructions)[0]
        $retFalse2 = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0)
        $retInstr2 = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Ret)
        $brfalse2  = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $OriginalFirst)
        $callEq2   = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Call, $OpEq)
        $ldnull2   = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Ldnull)
        $ldarg22   = $Il2.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2)
        $Il2.InsertBefore($OriginalFirst, $retInstr2)
        $Il2.InsertBefore($retInstr2,     $retFalse2)
        $Il2.InsertBefore($retFalse2, $brfalse2)
        $Il2.InsertBefore($brfalse2,  $callEq2)
        $Il2.InsertBefore($callEq2,   $ldnull2)
        $Il2.InsertBefore($ldnull2,   $ldarg22)
        Write-Output "[NullGuard] Patched TeamFieldOfView.IsVisibleTroughBlocks"
    }
}

# 3. GuiFollow.Update — return early if Camera.main is null (crashes every frame during replay)
$GuiFollowType = $Module.Types | Where-Object Name -eq "GuiFollow" | Select-Object -First 1
if ($GuiFollowType) {
    $GuiFollowUpdate = $GuiFollowType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if ($GuiFollowUpdate) {
        $Il3 = $GuiFollowUpdate.Body.GetILProcessor()
        # Find the first call to Camera::get_main
        $GetMainInstr = $GuiFollowUpdate.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq "get_main"
        } | Select-Object -First 1
        if ($GetMainInstr) {
            $FinalRet3 = @($GuiFollowUpdate.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1
            # Resolve op_Implicit(UnityEngine.Object) from UnityEngine.dll — already have $OpEq, use op_Implicit instead
            $UnityEngineAsm2  = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($UnityEngineDll)
            $UnityObjectType2 = $UnityEngineAsm2.MainModule.Types | Where-Object FullName -eq "UnityEngine.Object" | Select-Object -First 1
            $OpImplicit       = $UnityObjectType2.Methods | Where-Object { $_.Name -eq "op_Implicit" } | Select-Object -First 1
            $OpImpl           = $Module.ImportReference($OpImplicit)
            $UnityEngineAsm2.Dispose()
            # Insert before GetMainInstr: call get_main / call op_Implicit / brfalse FinalRet
            $brfalse3  = $Il3.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $FinalRet3)
            $callImpl  = $Il3.Create([Mono.Cecil.Cil.OpCodes]::Call, $OpImpl)
            $callMain  = $Il3.Create([Mono.Cecil.Cil.OpCodes]::Call, $Module.ImportReference($GetMainInstr.Operand))
            $Il3.InsertBefore($GetMainInstr, $brfalse3)
            $Il3.InsertBefore($brfalse3,     $callImpl)
            $Il3.InsertBefore($callImpl,     $callMain)
            Write-Output "[NullGuard] Patched GuiFollow.Update"
        }
    }
}

# 5. UnitGhostHandler.Update — guard buildStartTime/buildEndTime .HasValue before .Value
$UghType = $Module.Types | Where-Object Name -eq "UnitGhostHandler" | Select-Object -First 1
if ($UghType) {
    $UpdateMethod = $UghType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    if ($UpdateMethod) {
        $Il       = $UpdateMethod.Body.GetILProcessor()
        $FinalRet = @($UpdateMethod.Body.Instructions) | Where-Object { $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ret } | Select-Object -Last 1

        # Find get_HasValue on Nullable<Single> by scanning the Update method's own instructions
        # (the existing ldflda/call get_Value pattern uses Nullable<Single>, so get_HasValue must match)
        $HasValueRef = $null
        foreach ($instr in $UpdateMethod.Body.Instructions) {
            if ($instr.Operand -is [Mono.Cecil.MethodReference] -and
                $instr.Operand.Name -eq "get_HasValue" -and
                $instr.Operand.DeclaringType.Name -eq "Nullable``1") {
                $HasValueRef = $instr.Operand
                break
            }
        }
        if (-not $HasValueRef) {
            # Fall back: find get_HasValue on Nullable<Single> anywhere in module
            foreach ($t in $Module.Types) {
                foreach ($m in $t.Methods) {
                    if (-not $m.HasBody) { continue }
                    foreach ($instr in $m.Body.Instructions) {
                        if ($instr.Operand -is [Mono.Cecil.MethodReference] -and
                            $instr.Operand.Name -eq "get_HasValue" -and
                            $instr.Operand.DeclaringType.FullName -eq "System.Nullable``1<System.Single>") {
                            $HasValueRef = $instr.Operand
                            break
                        }
                    }
                    if ($HasValueRef) { break }
                }
                if ($HasValueRef) { break }
            }
        }
        if (-not $HasValueRef) { throw "Could not find Nullable<Single>::get_HasValue reference in module" }
        $HasValue = $Module.ImportReference($HasValueRef)

        # Find all `call get_Value` on Nullable<Single> — pattern is ldarg.0, ldflda <field>, call get_Value
        # Insert HasValue guard before each ldarg.0 that precedes the pattern
        $GetValueInstrs = @(@($UpdateMethod.Body.Instructions) | Where-Object {
            $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Call -and
            $_.Operand -ne $null -and
            [string]$_.Operand.Name -eq "get_Value"
        })

        # Strategy: find each brfalse that jumps to IL_0074 (separates the two object branches).
        # Before each ldloc that follows such a brfalse, insert HasValue guards for both nullable fields.
        # Stack is guaranteed empty at those ldloc points.
        $NullableFields = @($UpdateMethod.Body.Instructions | Where-Object {
            $_.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldflda -and
            $_.Operand -ne $null -and
            $_.Operand.FieldType.Name -eq "Nullable``1"
        } | ForEach-Object { $_.Operand } | Select-Object -Unique)

        # Find all ldloc instructions that immediately follow a brfalse (clean stack points)
        $allInstrs = @($UpdateMethod.Body.Instructions)
        $insertPoints = @()
        for ($i = 1; $i -lt $allInstrs.Count; $i++) {
            $prev = $allInstrs[$i - 1]
            $curr = $allInstrs[$i]
            if (($prev.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Brfalse -or $prev.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Brfalse_S) -and
                ($curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc_0 -or $curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc_1 -or
                 $curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc_2 -or $curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc_3 -or
                 $curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc_S -or $curr.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldloc)) {
                $insertPoints += $curr
            }
        }

        foreach ($insertPoint in $insertPoints) {
            # Insert HasValue guards for each nullable field, in reverse field order so first field is first in IL
            $fieldsReversed = @($NullableFields)
            [array]::Reverse($fieldsReversed)
            foreach ($field in $fieldsReversed) {
                $brfalse   = $Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $FinalRet)
                $callHv    = $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $HasValue)
                $ldfldaNew = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldflda, $field)
                $ldarg0New = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0)
                $Il.InsertBefore($insertPoint, $brfalse)
                $Il.InsertBefore($brfalse,     $callHv)
                $Il.InsertBefore($callHv,      $ldfldaNew)
                $Il.InsertBefore($ldfldaNew,   $ldarg0New)
            }
        }
        Write-Output "[NullGuard] Patched UnitGhostHandler.Update"
    }
}

# Patch GuiSpriteResources.GetShopImage to call TextureReplacementBootstrapper.GetShopImageOverride first
$GuiSpriteResourcesType = $Module.Types | Where-Object Name -eq "GuiSpriteResources" | Select-Object -First 1
$HelperTextureType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.TextureReplacementBootstrapper" | Select-Object -First 1
if ($GuiSpriteResourcesType -and $HelperTextureType) {
    $GetShopImageMethod = $GuiSpriteResourcesType.Methods | Where-Object Name -eq "GetShopImage" | Select-Object -First 1
    $GetShopImageOverrideMethod = $HelperTextureType.Methods | Where-Object Name -eq "GetShopImageOverride" | Select-Object -First 1
    if ($GetShopImageMethod -and $GetShopImageOverrideMethod) {
        $ImportedGetShopImageOverride = $Module.ImportReference($GetShopImageOverrideMethod)
        $SpriteType = $GetShopImageMethod.ReturnType
        $Il = $GetShopImageMethod.Body.GetILProcessor()
        $FirstInstr = $GetShopImageMethod.Body.Instructions | Select-Object -First 1
        # Add local variable to hold override result
        $LocalVar = [Mono.Cecil.Cil.VariableDefinition]::new($SpriteType)
        $GetShopImageMethod.Body.Variables.Add($LocalVar) | Out-Null
        $GetShopImageMethod.Body.InitLocals = $true
        # Build prefix: ldarg.0 -> call override -> stloc -> ldloc -> brfalse(skip) -> ldloc -> ret
        $NopSkip = $Il.Create([Mono.Cecil.Cil.OpCodes]::Nop)
        $i1 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0)
        $i2 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedGetShopImageOverride)
        $i3 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Stloc, $LocalVar)
        $i4 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc, $LocalVar)
        $i5 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $NopSkip)
        $i6 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc, $LocalVar)
        $i7 = $Il.Create([Mono.Cecil.Cil.OpCodes]::Ret)
        $Il.InsertBefore($FirstInstr, $i1)
        $Il.InsertBefore($FirstInstr, $i2)
        $Il.InsertBefore($FirstInstr, $i3)
        $Il.InsertBefore($FirstInstr, $i4)
        $Il.InsertBefore($FirstInstr, $i5)
        $Il.InsertBefore($FirstInstr, $i6)
        $Il.InsertBefore($FirstInstr, $i7)
        $Il.InsertBefore($FirstInstr, $NopSkip)
        Write-Output "[ShopImage] Patched GuiSpriteResources.GetShopImage"
    }
}

$Assembly.Write($OutputPath)
$Assembly.Dispose()
$HelperAssembly.Dispose()
Copy-Item -LiteralPath $OutputPath -Destination $SavedCopyPath -Force
Remove-Item -LiteralPath $TempBasePath -Force

$Features = New-Object System.Collections.Generic.List[string]
if ($EnableFovFeature -and $FovConfig.enabled) { $Features.Add("fov") | Out-Null }
if ($ProjectileConfig.enabled) { $Features.Add("projectile") | Out-Null }
if ($AccuracyConfig.enabled) { $Features.Add("accuracy") | Out-Null }
if ($WeaponSwitchConfig.enabled) { $Features.Add("weapon-switch") | Out-Null }
if ($TeamColorConfig.enabled) { $Features.Add("team-color") | Out-Null }
if ($LockOnConfig.enabled) { $Features.Add("lock-on") | Out-Null }
if ($TrackingConfig.enabled) { $Features.Add("tracking-projectiles") | Out-Null }
if ($CrosshairConfig.enabled) { $Features.Add("crosshair") | Out-Null }
if ($DamageHealingConfig.enabled) { $Features.Add("damage-healing-indicator") | Out-Null }
if ($HealAlertConfig.enabled) { $Features.Add("heal-alert-indicator") | Out-Null }
if ($Config.enabled) { $Features.Add("font") | Out-Null }
if ($BaseObjectiveBeamConfig.enabled) { $Features.Add("base-objective-beam") | Out-Null }
if ($LocalBuildPreviewConfig.enabled) { $Features.Add("local-build-preview") | Out-Null }
if ($DebugMenuConfig.enabled) { $Features.Add("debug-menu") | Out-Null }
if ($AimHealthbarConfig.enabled) { $Features.Add("aim-healthbar") | Out-Null }
if ($DeathCamHealthbarConfig.enabled) { $Features.Add("deathcam-healthbar") | Out-Null }
if ($AutoCasualQueueConfig.enabled) { $Features.Add("auto-casual-queue") | Out-Null }
if ($MatchReplayRecorderConfig.enabled) { $Features.Add("match-replay-recorder") | Out-Null }
if ($TeammateHpEnabled) { $Features.Add("teammate-hp") | Out-Null }
if ($AutoCrouchEnabled) { $Features.Add("disable-auto-crouch") | Out-Null }
if ($HideImpactVfxConfig.enabled) { $Features.Add("hide-impact-vfx") | Out-Null }
if ($UnitGuiScaleConfig.enabled) { $Features.Add("unit-gui-scale") | Out-Null }
if ($WsiConfig.scale_enabled) { $Features.Add("wsi-scale") | Out-Null }
if ($MapRenderConfig.enabled) { $Features.Add("map-render-override") | Out-Null }
$Hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA1).Hash
Write-Output "Experimental all-in-one DLL built. SHA1=$Hash features=$([string]::Join(',', $Features))"
