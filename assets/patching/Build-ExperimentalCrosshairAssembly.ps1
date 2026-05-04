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
$AimHealthbarConfigPath = Join-Path $PSScriptRoot "aim-healthbar-config.json"
$OutputPath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.dll"
$SavedCopyPath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.font-configured.dll"
$TempBasePath = Join-Path $PSScriptRoot "Assembly-CSharp.experimental.base.dll"
$HelperOutputPath = Join-Path $PSScriptRoot "BnlCommunityFixes.dll"
$LockOnHelperSourcePath = Join-Path $PSScriptRoot "LockOnRuntime.cs"
$TrackingHelperSourcePath = Join-Path $PSScriptRoot "TrackingProjectileRuntime.cs"
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
    log_tool_branches = $true
    log_unit_card_id = $true
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
$DamageHealingConfig = Get-JsonConfig -Path $DamageHealingConfigPath -Default @{
    enabled = $false
    damage_number_color = "#FFFFFF"
    crit_damage_number_color = "#FFFFFF"
    heal_number_color = "#91ED78"
    damage_number_size_multiplier = 1.0
    heal_number_size_multiplier = 1.0
    alpha = 1.0
    show_friendly_healing = $false
    show_self_healing = $false
    combine_damage_until_hidden = $false
    combine_healing_until_hidden = $false
    minimum_heal = 0.5
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
$AimHealthbarConfig = Get-JsonConfig -Path $AimHealthbarConfigPath -Default @{
    enabled = $true
}

$AnyEnabled = @(
    [bool]$Config.enabled,
    [bool]$ProjectileConfig.enabled,
    [bool]$AccuracyConfig.enabled,
    [bool]$WeaponSwitchConfig.enabled,
    [bool]$CrosshairConfig.enabled,
    [bool]$TeamColorConfig.enabled,
    [bool]$LockOnConfig.enabled,
    [bool]$TrackingConfig.enabled,
    [bool]$FovConfig.enabled,
    [bool]$DamageHealingConfig.enabled,
    [bool]$HealAlertConfig.enabled,
    [bool]$BaseObjectiveBeamConfig.enabled,
    [bool]$EnemyShieldBuffBarConfig.enabled,
    [bool]$LocalBuildPreviewConfig.enabled,
    [bool]$AimHealthbarConfig.enabled
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
[double]$DamageNumberSize = if ($null -ne $DamageHealingConfig.damage_number_size_multiplier) { [double]$DamageHealingConfig.damage_number_size_multiplier } else { 1.0 }
[double]$HealNumberSize = if ($null -ne $DamageHealingConfig.heal_number_size_multiplier) { [double]$DamageHealingConfig.heal_number_size_multiplier } else { 1.0 }
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
[double]$LocalBuildPreviewTimeoutSeconds = if ($null -ne $LocalBuildPreviewConfig.prediction_timeout_seconds) { [double]$LocalBuildPreviewConfig.prediction_timeout_seconds } else { 2.0 }

$HelperSource = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            Unit unit = UnitField == null ? null : UnitField.GetValue(healthbar) as Unit;
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
        private static readonly Color IdleColor = new Color($CrosshairIdleR, $CrosshairIdleG, $CrosshairIdleB, $CrosshairIdleA);
        private static readonly Color FullDamageColor = new Color($CrosshairFullR, $CrosshairFullG, $CrosshairFullB, $CrosshairFullA);
        private static readonly Color BelowMaxColor = new Color($CrosshairBelowR, $CrosshairBelowG, $CrosshairBelowB, $CrosshairBelowA);
        private static readonly float SizeMultiplier = $CrosshairSizeMultiplierLiteral;
        private static readonly float SpreadMultiplier = $CrosshairSpreadMultiplierLiteral;
        private static readonly bool ForceShowInAds = $CrosshairForceShowInAdsLiteral;
        private static readonly bool HideCrosshairEntirely = $(Format-BoolLiteral $CrosshairHideEntirely);
        private static readonly string ForceShape = "$CrosshairForceShapeLiteral";
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartScales = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Vector2> OriginalCrosshairPartSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, Vector3> OriginalCrosshairPartPositions = new Dictionary<int, Vector3>();

        public static bool ApplyHardHide(GuiCrosshairController controller)
        {
            if (!HideCrosshairEntirely || controller == null)
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
            if (!ForceShowInAds || controller == null || controller.Content == null || controller.Content.activeSelf)
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

            controller.NoTarget = IdleColor;
            controller.FullDamage = FullDamageColor;
            controller.BelowMaxDamage = BelowMaxColor;
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
                    rect.localScale = baseScale * SizeMultiplier;

                    Vector2 baseSize;
                    if (!OriginalCrosshairPartSizes.TryGetValue(id, out baseSize))
                    {
                        baseSize = rect.sizeDelta;
                        OriginalCrosshairPartSizes[id] = baseSize;
                    }
                    rect.sizeDelta = baseSize * SizeMultiplier;

                    Vector3 basePosition;
                    if (!OriginalCrosshairPartPositions.TryGetValue(id, out basePosition) || IsRuntimeCrosshairPart(blank, rect))
                    {
                        basePosition = rect.localPosition;
                        OriginalCrosshairPartPositions[id] = basePosition;
                    }
                    rect.localPosition = basePosition * SizeMultiplier;
                }
                return;
            }

            blank.transform.localScale = Vector3.one * SizeMultiplier;
        }

        public static float ScaleAngle(float angle)
        {
            return angle * SpreadMultiplier;
        }

        public static Vector3 ScaleSizeVector(Vector3 value)
        {
            return value * SizeMultiplier;
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
            switch (ForceShape)
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
    public sealed class HealingNumberBridge : MonoBehaviour
    {
        public GuiDamageNumberDetector Detector;
        private Action unsubscribe;

        private void Start()
        {
            ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
            if (messenger != null)
            {
                messenger.OnGlobalUnitHealthChange.Subscribe(new Action<GlobalUnitHealthChangeArgs>(OnHealthChanged), ref unsubscribe);
            }
        }

        private void OnDestroy()
        {
            if (unsubscribe != null) unsubscribe();
        }

        private void OnHealthChanged(GlobalUnitHealthChangeArgs args)
        {
            CombatNumberRuntime.OnHealthChanged(Detector, args);
        }
    }

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
        private static readonly Color DamageNumberColor = new Color($(Format-FloatLiteral $DamageColor.R), $(Format-FloatLiteral $DamageColor.G), $(Format-FloatLiteral $DamageColor.B), $(Format-FloatLiteral $DamageColor.A));
        private static readonly Color CritDamageNumberColor = new Color($(Format-FloatLiteral $CritDamageColor.R), $(Format-FloatLiteral $CritDamageColor.G), $(Format-FloatLiteral $CritDamageColor.B), $(Format-FloatLiteral $CritDamageColor.A));
        private static readonly Color ConfigHealColor = new Color($(Format-FloatLiteral $HealColor.R), $(Format-FloatLiteral $HealColor.G), $(Format-FloatLiteral $HealColor.B), $(Format-FloatLiteral $HealColor.A));
        private static readonly bool UseDamageNumberColor = $(Format-BoolLiteral $UseDamageColor);
        private static readonly bool UseCritDamageNumberColor = $(Format-BoolLiteral $UseCritDamageColor);
        private static readonly bool UseConfigHealColor = $(Format-BoolLiteral $UseHealColor);
        private static readonly bool ShowFriendlyHealing = $(Format-BoolLiteral $ShowFriendlyHealing);
        private static readonly bool ShowSelfHealing = $(Format-BoolLiteral $ShowSelfHealing);
        private static readonly bool CombineDamageUntilHidden = $(Format-BoolLiteral $CombineDamageUntilHidden);
        private static readonly bool CombineHealingUntilHidden = $(Format-BoolLiteral $CombineHealingUntilHidden);
        private static readonly float DamageNumberSizeMultiplier = $(Format-FloatLiteral $DamageSize);
        private static readonly float HealNumberSizeMultiplier = $(Format-FloatLiteral $HealSize);
        private static readonly float MinimumHeal = $(Format-FloatLiteral $MinimumHeal);
        private const float CollectTime = 0.15f;
        private const float HealContinueGrace = 2.5f;
        private static readonly Dictionary<uint, ActiveDamageNumber> ActiveDamageNumbers = new Dictionary<uint, ActiveDamageNumber>();
        private static readonly Dictionary<uint, ActiveHealNumber> ActiveHealNumbers = new Dictionary<uint, ActiveHealNumber>();

        public static void AttachHealing(GuiDamageNumberDetector detector)
        {
            if (detector == null) return;
            HealingNumberBridge bridge = detector.gameObject.GetComponent<HealingNumberBridge>();
            if (bridge == null)
            {
                bridge = detector.gameObject.AddComponent<HealingNumberBridge>();
            }
            bridge.Detector = detector;
        }

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

        public static void ApplyDamageNumber(GuiDamageNumber number, bool crit)
        {
            if (number == null) return;
            if (DamageNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * DamageNumberSizeMultiplier;
            bool useColor = crit ? UseCritDamageNumberColor : UseDamageNumberColor;
            Color color = crit ? CritDamageNumberColor : DamageNumberColor;
            if (number.Damage != null && (useColor || DamageNumberSizeMultiplier != 1f))
            {
                if (useColor) number.Damage.color = color;
                if (DamageNumberSizeMultiplier != 1f) number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * DamageNumberSizeMultiplier));
            }
            if (useColor) ApplyGraphics(number.gameObject, color);
        }

        public static float GetDamageCollectTime(float original)
        {
            return CombineDamageUntilHidden ? 99999f : original;
        }

        public static GuiDamageNumber RefreshDamageNumber(GuiDamageNumberDetector detector, GuiDamageNumber oldNumber, Unit unit, float value, bool crit)
        {
            if (!CombineDamageUntilHidden || detector == null || unit == null || oldNumber == null)
            {
                RestartLifetime(oldNumber == null ? null : oldNumber.gameObject);
                ApplyDamageNumber(oldNumber, crit);
                return oldNumber;
            }

            ActiveDamageNumber active;
            if (ActiveDamageNumbers.TryGetValue(unit.Id, out active) && active.Number != null)
            {
                if (active.Number == oldNumber)
                {
                    active.Value = value;
                    active.IsCrit = active.IsCrit || crit;
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
                    active.IsCrit = active.IsCrit || crit;
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
            if (amount < MinimumHeal) return;
            if (args.unit.PlayerId == null) return;
            if (args.unit.IsMyPlayer)
            {
                if (!ShowSelfHealing) return;
            }
            else if (!ShowFriendlyHealing || !args.unit.Team.IsMy())
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
                    ApplyHealTextAndColor(active.Number, active.Value, detector.HealColor);
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

            GuiHealthBarMaker healthBar = unit.GetComponentInChildren<GuiHealthBarMaker>();
            number.GetOrAddComponent<GuiFollow>().WorldTarget = healthBar ? healthBar.transform : unit.transform;
            if (HealNumberSizeMultiplier != 1f) number.transform.localScale = number.transform.localScale * HealNumberSizeMultiplier;
            ApplyHealTextAndColor(number, amount, detector.HealColor);
            RefreshHealHold(number);
            return number;
        }

        private static bool ShouldCombineHeal(ActiveHealNumber active)
        {
            if (active == null) return false;
            if (!CombineHealingUntilHidden) return Time.time - active.LastTime <= CollectTime;
            return Time.time - active.LastTime <= HealContinueGrace;
        }

        private static void RefreshHealHold(GuiDamageNumber number)
        {
            if (number == null) return;
            if (!CombineHealingUntilHidden)
            {
                RestartLifetime(number.gameObject);
                return;
            }

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
            hold.Extend(Time.time + HealContinueGrace);
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
            hold.Extend(Time.time + HealContinueGrace);
        }

        private static void ApplyHealTextAndColor(GuiDamageNumber number, float amount, Color defaultHealColor)
        {
            if (number == null || number.Damage == null) return;
            number.Damage.text = "+" + Mathf.RoundToInt(amount).ToString();
            number.Damage.color = UseConfigHealColor ? ConfigHealColor : defaultHealColor;
            if (HealNumberSizeMultiplier != 1f)
            {
                number.Damage.fontSize = Mathf.Max(1, Mathf.RoundToInt(number.Damage.fontSize * HealNumberSizeMultiplier));
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

$HelperSource += @"

namespace BnlCommunityFixes
{
    public static class AdsSensitivityRuntime
    {
        private const float AdsSensitivityMultiplier = $AdsSensitivityMultiplierLiteral;

        public static float ApplyAdsScale(float currentScale, Unit unit)
        {
            if (unit != null && unit.GetAimingState() != null)
            {
                return currentScale * AdsSensitivityMultiplier;
            }

            return currentScale;
        }
    }
}
"@

$HelperSource += @"

namespace BnlCommunityFixes
{
    public sealed class HealAlertBridge : MonoBehaviour
    {
        public GuiHitAlertMaker Maker;
        private Action unsubscribe;

        private void Start()
        {
            ZoneMessenger messenger = Singleton<ZoneMessenger>.Instance;
            if (messenger != null)
                messenger.OnGlobalUnitHealthChange.Subscribe(new Action<GlobalUnitHealthChangeArgs>(OnHealthChanged), ref unsubscribe);
        }

        private void OnDestroy()
        {
            if (unsubscribe != null) unsubscribe();
        }

        private void OnHealthChanged(GlobalUnitHealthChangeArgs args)
        {
            HealAlertRuntime.OnHealthChanged(Maker, args);
        }
    }

    public static class HealAlertRuntime
    {
        private static readonly Color DamageIndicatorColor = new Color($(Format-FloatLiteral $HealAlertDamageColor.R), $(Format-FloatLiteral $HealAlertDamageColor.G), $(Format-FloatLiteral $HealAlertDamageColor.B), $(Format-FloatLiteral $HealAlertDamageColor.A));
        private static readonly Color HealIndicatorColor   = new Color($(Format-FloatLiteral $HealAlertHealColor.R),   $(Format-FloatLiteral $HealAlertHealColor.G),   $(Format-FloatLiteral $HealAlertHealColor.B),   $(Format-FloatLiteral $HealAlertHealColor.A));
        private static readonly bool UseDamageIndicatorColor = $(Format-BoolLiteral $HealAlertUseDamageColor);
        private static readonly bool UseHealIndicatorColor   = $(Format-BoolLiteral $HealAlertUseHealColor);
        private static readonly float DamageIndicatorSizeMultiplier = $(Format-FloatLiteral $HealAlertDamageSize);
        private static readonly float HealIndicatorSizeMultiplier   = $(Format-FloatLiteral $HealAlertHealSize);
        private static readonly float MinimumHeal      = $(Format-FloatLiteral $HealAlertMinimumHeal);
        private static readonly bool ShowDirectionOnHeal = $(Format-BoolLiteral $HealAlertShowDir);

        public static void AttachHealBridge(GuiHitAlertMaker maker)
        {
            if (maker == null) return;
            HealAlertBridge bridge = maker.gameObject.GetComponent<HealAlertBridge>();
            if (bridge == null) bridge = maker.gameObject.AddComponent<HealAlertBridge>();
            bridge.Maker = maker;
        }

        public static void ApplyDamageIndicator(Component component)
        {
            GameObject go = component == null ? null : component.gameObject;
            if (go == null) return;
            if (DamageIndicatorSizeMultiplier != 1f)
                go.transform.localScale = go.transform.localScale * DamageIndicatorSizeMultiplier;
            if (UseDamageIndicatorColor) ApplyGraphics(go, DamageIndicatorColor);
        }

        public static void OnHealthChanged(GuiHitAlertMaker maker, GlobalUnitHealthChangeArgs args)
        {
            if (maker == null || args == null || args.unit == null) return;
            if (!args.unit.IsMyPlayer) return;
            if (!maker.Content.activeSelf) return;

            float healAmount = args.newHealth - args.oldHealth;
            if (healAmount < MinimumHeal) return;

            if (ShowDirectionOnHeal)
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
            if (HealIndicatorSizeMultiplier > 1f)
                go.transform.localScale = go.transform.localScale * HealIndicatorSizeMultiplier;
            if (UseHealIndicatorColor) ApplyGraphics(go, HealIndicatorColor);
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
    $LocalBuildPreviewTimeoutLiteral = Format-FloatLiteral $LocalBuildPreviewTimeoutSeconds
    $HelperSource += @"

namespace BnlCommunityFixes
{
    public static class LocalBuildPredictionRuntime
    {
        private static readonly bool Enabled = true;
        private static readonly float PredictionTimeoutSeconds = $LocalBuildPreviewTimeoutLiteral;
        private static readonly float InstantCrateChainWindowSeconds = 5.0f;
        private static PredictionManager manager;
        private static float instantCrateChainUntil;

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
            return blockCard != null && blockCard.BlockId != 0 && deviceCard.BuildTime.GetValueOrDefault(0f) <= 0f;
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

        public static void TryInstantAcceptStartBuild(BuildInfo info, ServiceZone.Rpc_StartBuild rpc)
        {
            if (info == null || rpc == null) return;
            if (IsInstantPlacementDeviceKey(info.DeviceKey) ||
                (IsCratePlacementDeviceKey(info.DeviceKey) && IsInstantCrateChainWindowActive()))
            {
                if (IsCratePlacementDeviceKey(info.DeviceKey)) ActivateInstantCrateChainWindow();
                rpc._Success(true);
            }
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
                CardBlock blockCard = objectCard as CardBlock;
                if (blockCard != null && blockCard.BlockId != 0)
                {
                    if (blockCard.BlockId == 58) ActivateInstantCrateChainWindow();
                    Block newBlock = new Block(blockCard.BlockId);
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

        public void AddPrediction(PredictionEntry entry)
        {
            if (entry == null) return;
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                PredictionEntry current = this.entries[i];
                bool sameBlock = !current.IsUnit && !entry.IsUnit && current.BlockPos.Equals(entry.BlockPos);
                bool sameUnitSpot = current.IsUnit == entry.IsUnit && current.DeviceKey.Equals(entry.DeviceKey) && UnityEngine.Vector3.Distance(current.WorldPos, entry.WorldPos) <= 0.75f;
                if (sameBlock || sameUnitSpot) this.RemoveAt(i, false);
            }
            this.entries.Add(entry);
        }

        public void ResolveBlock(Vector3s blockPos)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
                if (!this.entries[i].IsUnit && this.entries[i].BlockPos.Equals(blockPos))
                    this.RemoveAt(i, false);
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
            if (rollbackRealLocalBlock && entry != null && entry.IsRealLocalBlock &&
                Singleton<ZoneManager>.Instance != null && Singleton<ZoneManager>.Instance.MapCreated)
            {
                System.Collections.Generic.Dictionary<Vector3s, BlockUpdate> updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();
                updates[entry.BlockPos] = entry.PreviousBlock.ToUpdate();
                Singleton<ZoneManager>.Instance.UpdateBlocks(updates);
            }
            if (entry != null && entry.PreviewObject != null)
                UnityEngine.Object.Destroy(entry.PreviewObject);
        }
    }
}
"@
}

if (Test-Path $HelperOutputPath) {
    Remove-Item -LiteralPath $HelperOutputPath -Force
}

Add-Type -TypeDefinition $HelperSource -Language CSharp -OutputAssembly $HelperOutputPath -ReferencedAssemblies @(
    $UnityEngineDll,
    $UnityEngineUiDll,
    $BackupPath,
    "System.dll",
    "System.Core.dll"
)
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
if ($AdsRuntimeType) {
    $AdsScaleMethod = $AdsRuntimeType.Methods | Where-Object Name -eq "ApplyAdsScale" | Select-Object -First 1
    if ($AdsScaleMethod) {
        $ImportedAdsScaleMethod = $Module.ImportReference($AdsScaleMethod)
    }
}
$CombatNumberRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.CombatNumberRuntime" | Select-Object -First 1
$ApplyDamageNumber = $Module.ImportReference(($CombatNumberRuntimeType.Methods | Where-Object Name -eq "ApplyDamageNumber" | Select-Object -First 1))
$GetDamageCollectTime = $Module.ImportReference(($CombatNumberRuntimeType.Methods | Where-Object Name -eq "GetDamageCollectTime" | Select-Object -First 1))
$RefreshDamageNumber = $Module.ImportReference(($CombatNumberRuntimeType.Methods | Where-Object Name -eq "RefreshDamageNumber" | Select-Object -First 1))
$AttachHealingIndicator = $Module.ImportReference(($CombatNumberRuntimeType.Methods | Where-Object Name -eq "AttachHealing" | Select-Object -First 1))
$ShouldShowDamageNumber = $Module.ImportReference(($CombatNumberRuntimeType.Methods | Where-Object Name -eq "ShouldShowDamageNumber" | Select-Object -First 1))

$HealAlertRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.HealAlertRuntime" | Select-Object -First 1
$ImportedHealAlertApplyDamageIndicator = $Module.ImportReference(($HealAlertRuntimeType.Methods | Where-Object Name -eq "ApplyDamageIndicator" | Select-Object -First 1))
$ImportedHealAlertAttachBridge = $Module.ImportReference(($HealAlertRuntimeType.Methods | Where-Object Name -eq "AttachHealBridge" | Select-Object -First 1))
$ShieldBuffBarRuntimeType = $HelperAssembly.MainModule.Types | Where-Object FullName -eq "BnlCommunityFixes.ShieldBuffBarRuntime" | Select-Object -First 1
$ImportedShieldBuffBarAttach = $null
if ($ShieldBuffBarRuntimeType) {
    $ShieldBuffBarAttachMethod = $ShieldBuffBarRuntimeType.Methods | Where-Object Name -eq "AttachShieldBuffBar" | Select-Object -First 1
    if ($ShieldBuffBarAttachMethod) {
        $ImportedShieldBuffBarAttach = $Module.ImportReference($ShieldBuffBarAttachMethod)
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
    param([Mono.Cecil.MethodDefinition]$Method)
    if (-not $Method -or -not $Method.HasBody) { return }
    $Il = $Method.Body.GetILProcessor()
    $First = $Method.Body.Instructions[0]
    $Il.InsertBefore($First, $Il.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImportedApplyAllCanvases))
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

if ($ImportedShieldBuffBarAttach) {
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
    }
}
}

$MainMenu = $Module.Types | Where-Object Name -eq "MainMenu" | Select-Object -First 1
$CameraFov = $Module.Types | Where-Object Name -eq "CameraFov" | Select-Object -First 1
$UiStyleFontComponent = $Module.Types | Where-Object Name -eq "UiStyleFontComponent" | Select-Object -First 1

if ($Config.enabled) {
    Insert-CallAtStart -Method ($MainMenu.Methods | Where-Object Name -eq "Start" | Select-Object -First 1)
    Insert-CallAtStart -Method ($CameraFov.Methods | Where-Object Name -eq "Start" | Select-Object -First 1)

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

if ($FovConfig.enabled) {
    $UpdateMethod = $CameraFov.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    $Instructions = @($UpdateMethod.Body.Instructions)
    $ForcedFov = [single]$FovConfig.fov
    if ($null -ne $FovConfig.weapon_model_fov) {
        $WeaponModelFov = [single]([double]$FovConfig.weapon_model_fov)
    } else {
        $WeaponModelFov = [single]30.0
    }

    ($Instructions | Where-Object Offset -eq 72 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
    ($Instructions | Where-Object Offset -eq 72 | Select-Object -First 1).Operand = $ForcedFov
    ($Instructions | Where-Object Offset -eq 77 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($Instructions | Where-Object Offset -eq 77 | Select-Object -First 1).Operand = $null
    ($Instructions | Where-Object Offset -eq 82 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($Instructions | Where-Object Offset -eq 82 | Select-Object -First 1).Operand = $null

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

    $CameraArmsType = $Module.Types | Where-Object Name -eq "CameraArms" | Select-Object -First 1
    $CameraArmsUpdate = $CameraArmsType.Methods | Where-Object Name -eq "Update" | Select-Object -First 1
    $CameraArmsInstructions = @($CameraArmsUpdate.Body.Instructions)

    ($CameraArmsInstructions | Where-Object Offset -eq 103 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
    ($CameraArmsInstructions | Where-Object Offset -eq 103 | Select-Object -First 1).Operand = $WeaponModelFov
    ($CameraArmsInstructions | Where-Object Offset -eq 104 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($CameraArmsInstructions | Where-Object Offset -eq 104 | Select-Object -First 1).Operand = $null
    ($CameraArmsInstructions | Where-Object Offset -eq 109 | Select-Object -First 1).OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
    ($CameraArmsInstructions | Where-Object Offset -eq 109 | Select-Object -First 1).Operand = $null
    ($CameraArmsInstructions | Where-Object Offset -eq 130 | Select-Object -First 1).Operand = $WeaponModelFov
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

    $UnityEngineRef = $Module.AssemblyReferences | Where-Object Name -eq "UnityEngine" | Select-Object -First 1
    $UnityEngineAsm = $Resolver.Resolve($UnityEngineRef)
    $ColorType = $UnityEngineAsm.MainModule.Types | Where-Object FullName -eq "UnityEngine.Color" | Select-Object -First 1
    $ColorCtor = $ColorType.Methods | Where-Object { $_.IsConstructor -and $_.Parameters.Count -eq 4 } | Select-Object -First 1
    $ImportedColorCtor = $Module.ImportReference($ColorCtor)

    $RuntimeType = New-Object Mono.Cecil.TypeDefinition("", "ExperimentalTeamColorRuntime", ([Mono.Cecil.TypeAttributes]::Class -bor [Mono.Cecil.TypeAttributes]::Abstract -bor [Mono.Cecil.TypeAttributes]::Sealed -bor [Mono.Cecil.TypeAttributes]::Public), $Module.TypeSystem.Object)
    $Module.Types.Add($RuntimeType)

    function Add-ColorMethod {
        param([string]$Name,$ColorData)
        $Attrs = [Mono.Cecil.MethodAttributes](([int][Mono.Cecil.MethodAttributes]::Public) -bor ([int][Mono.Cecil.MethodAttributes]::Static) -bor ([int][Mono.Cecil.MethodAttributes]::HideBySig))
        $Method = New-Object Mono.Cecil.MethodDefinition($Name, $Attrs, $Module.ImportReference($ColorType))
        $RuntimeType.Methods.Add($Method)
        $Il = $Method.Body.GetILProcessor()
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$ColorData.R))
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$ColorData.G))
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$ColorData.B))
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_R4, [single]$ColorData.A))
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Newobj, $ImportedColorCtor))
        $Il.Append($Il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        return $Method
    }

    $ReplacementMethods = @{
        "TeamFriendly" = Add-ColorMethod -Name "GetGuiFriendlyColor" -ColorData $Friendly
        "TeamEnemy" = Add-ColorMethod -Name "GetGuiEnemyColor" -ColorData $Enemy
        "BackgroundTeamFriendly" = Add-ColorMethod -Name "GetGuiBackgroundFriendlyColor" -ColorData $FriendlyBg
        "BackgroundTeamEnemy" = Add-ColorMethod -Name "GetGuiBackgroundEnemyColor" -ColorData $EnemyBg
        "CommonTeamFriendly" = Add-ColorMethod -Name "GetObjectCommonFriendlyColor" -ColorData $Friendly
        "CommonTeamEnemy" = Add-ColorMethod -Name "GetObjectCommonEnemyColor" -ColorData $Enemy
        "ForceFieldTeamFriendly" = Add-ColorMethod -Name "GetForceFieldFriendlyColor" -ColorData $Friendly
        "ForceFieldTeamEnemy" = Add-ColorMethod -Name "GetForceFieldEnemyColor" -ColorData $Enemy
        "IceTeamFriendly" = Add-ColorMethod -Name "GetIceFriendlyColor" -ColorData $Friendly
        "IceTeamEnemy" = Add-ColorMethod -Name "GetIceEnemyColor" -ColorData $Enemy
    }

    $ImportedReplacementMethods = @{}
    foreach ($Key in $ReplacementMethods.Keys) { $ImportedReplacementMethods[$Key] = $Module.ImportReference($ReplacementMethods[$Key]) }
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
    $Il = $UpdateMethod.Body.GetILProcessor()
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
    $Il.InsertBefore($TargetInstruction, $Il.Create([Mono.Cecil.Cil.OpCodes]::Stfld, $ImportedActiveField))
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

    # BuildGhostController.Place — call OnLocalPlace after ServiceZone.Hit
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

    # ZoneServiceListener — BlockUpdates, DeviceBuilt, UnitCreate reconciliation
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

    # ServiceZone.StartBuild — inject TryInstantAcceptStartBuild before return
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

    # BuffHelper.BuildTime — zero build time for instant-placement devices
    $BuffHelperType = $Module.Types | Where-Object Name -eq "BuffHelper" | Select-Object -First 1
    $BuildTimeMethod = $BuffHelperType.Methods | Where-Object Name -eq "BuildTime" | Select-Object -First 1
    if (-not $BuildTimeMethod -or -not $BuildTimeMethod.HasBody) { throw "BuffHelper.BuildTime not found." }
    $BuildTimeIl = $BuildTimeMethod.Body.GetILProcessor()
    Inject-FloatZeroBypass -Method $BuildTimeMethod -PrefixInstructions @(
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_2),
        $BuildTimeIl.Create([Mono.Cecil.Cil.OpCodes]::Call, $ImpShouldZeroBuildTime)
    )

    # ToolLogicBuild.ValidateUse — bypass validation for instant-placement
    $ToolLogicBuildType = $Module.Types | Where-Object Name -eq "ToolLogicBuild" | Select-Object -First 1
    $ValidateUseMethod = $ToolLogicBuildType.Methods | Where-Object Name -eq "ValidateUse" | Select-Object -First 1
    if (-not $ValidateUseMethod -or -not $ValidateUseMethod.HasBody) { throw "ToolLogicBuild.ValidateUse not found." }
    Inject-BoolTrueBypass -Method $ValidateUseMethod -Call $ImpShouldBypassBuildValidate -LoadArg ([Mono.Cecil.Cil.OpCodes]::Ldarg_0)

    # PlayerActSwitch coroutine — instant gear switch
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

    # ToolLogicBuild coroutines — replace timing helpers to zero build time
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

    # Crosshair singleton — reuse the exact closed get_Instance<Crosshair> call from ShowNameAndTitle
    $ShowNameAndTitleMethod = $GuiHealthbarType.Methods | Where-Object Name -eq "ShowNameAndTitle" | Select-Object -First 1
    $ExistingGetInstanceCall = @($ShowNameAndTitleMethod.Body.Instructions) | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.Name -eq "get_Instance"
    } | Select-Object -First 1
    if (-not $ExistingGetInstanceCall) { throw "Could not find existing get_Instance call in ShowNameAndTitle." }

    $CrosshairType = $Module.Types | Where-Object Name -eq "Crosshair" | Select-Object -First 1
    if (-not $CrosshairType) { throw "Crosshair type not found." }

    $RaycastUnitInfoField = $CrosshairType.Fields | Where-Object Name -eq "RaycastUnitInfo" | Select-Object -First 1
    if (-not $RaycastUnitInfoField) { throw "Crosshair.RaycastUnitInfo field not found." }

    $RaycastInfoType = $RaycastUnitInfoField.FieldType.Resolve()
    $RiUnitField = $RaycastInfoType.Fields | Where-Object Name -eq "Unit" | Select-Object -First 1
    if (-not $RiUnitField) { throw "RaycastInfo.Unit field not found." }

    $ImportedGetInstance      = $Module.ImportReference($ExistingGetInstanceCall.Operand)
    $ImportedUnitField        = $Module.ImportReference($UnitField)
    $ImportedContentField     = $Module.ImportReference($ContentField)
    $ImportedSetAlpha         = $Module.ImportReference($SetAlphaMethod)
    $ImportedRaycastInfoField = $Module.ImportReference($RaycastUnitInfoField)
    $ImportedRiUnitField      = $Module.ImportReference($RiUnitField)

    # --- Patch IsUnitAvailableForShow() ---
    # Insert at the top: if (object.ReferenceEquals(Crosshair.Instance.RaycastUnitInfo.Unit, this.unit)) return true;
    # Uses ldflda (no struct copy) + ceq (raw managed ref compare, no Unity native interop).

    $AvailMethod = $GuiHealthbarType.Methods | Where-Object Name -eq "IsUnitAvailableForShow" | Select-Object -First 1
    if (-not $AvailMethod -or -not $AvailMethod.HasBody) { throw "GuiHealthbar.IsUnitAvailableForShow not found." }

    $AvailIl = $AvailMethod.Body.GetILProcessor()
    $FirstInstr = $AvailMethod.Body.Instructions | Select-Object -First 1

    $BranchNotThisUnit = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Brfalse_S, $FirstInstr)

    # Null-check idiom that never corrupts the stack:
    #   call get_Instance   → stack: [x]
    #   dup                 → stack: [x, x]
    #   brtrue.s NOT_NULL   → stack: [x]   (jumps forward if not null, x still on stack)
    #   pop                 → stack: []    (null path: discard, fall to original)
    #   br.s ORIGINAL       → stack: []
    # NOT_NULL:             → stack: [x]   (not-null path: x ready for ldflda)
    $PopInstr       = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Pop)
    $BrNotNull      = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Brtrue_S,  $PopInstr)   # target fixed below
    $BrToOriginal   = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Br_S,      $FirstInstr)
    $Ldflda         = $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldflda,    $ImportedRaycastInfoField)
    # Fix brtrue target to the ldflda (the not-null continuation), not the pop
    $BrNotNull.Operand = $Ldflda

    $Instrs = @(
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Call,  $ImportedGetInstance),  # [x]
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Dup),                          # [x, x]
        $BrNotNull,                                                               # [x]  → Ldflda if not null
        $PopInstr,                                                                # []   (null path)
        $BrToOriginal,                                                            # []   → FirstInstr
        $Ldflda,                                                                  # [&RaycastUnitInfo]
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld,  $ImportedRiUnitField), # [Unit]
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0),
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld,  $ImportedUnitField),   # [Unit, this.unit]
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ceq),                          # [bool]
        $BranchNotThisUnit,                                                       # false → FirstInstr
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1),
        $AvailIl.Create([Mono.Cecil.Cil.OpCodes]::Ret)
    )

    foreach ($instr in $Instrs) {
        $AvailIl.InsertBefore($FirstInstr, $instr)
    }

    # --- Patch AlphaUpdate(): at the top, if showNameByCrosshair jump to the existing "set alpha=1" block ---
    # AlphaUpdate already calls set_alpha on every path — one extra branch costs nothing.
    # We jump to the existing IL_006b block (ldc.r4 1 / callvirt set_alpha / br end) rather than
    # calling set_alpha a second time, so no extra canvas dirty calls are introduced.
    $ShowNameByCrosshairField = $GuiHealthbarType.Fields | Where-Object Name -eq "showNameByCrosshair" | Select-Object -First 1
    $ImportedShowNameField = $Module.ImportReference($ShowNameByCrosshairField)

    $AlphaUpdateMethod = $GuiHealthbarType.Methods | Where-Object Name -eq "AlphaUpdate" | Select-Object -First 1
    if (-not $AlphaUpdateMethod -or -not $AlphaUpdateMethod.HasBody) { throw "GuiHealthbar.AlphaUpdate not found." }
    $AlphaIl = $AlphaUpdateMethod.Body.GetILProcessor()
    $AlphaInstrs = @($AlphaUpdateMethod.Body.Instructions)
    $AlphaFirstInstr = $AlphaInstrs | Select-Object -First 1

    # Find the "ldc.r4 1 / callvirt set_alpha" block that corresponds to IL_0071/IL_0076 in original.
    # It's the first set_alpha call preceded by ldc.r4 1.
    # Find the ldarg.0 that starts the "load Content / ldc.r4 1 / set_alpha" sequence.
    # Pattern: [i] ldarg.0, [i+1] ldfld Content, [i+2] ldc.r4 1, [i+3] callvirt set_alpha
    $SetAlphaOneInstr = $null
    for ($ai = 0; $ai -lt ($AlphaInstrs.Count - 3); $ai++) {
        $a0 = $AlphaInstrs[$ai]
        $a1 = $AlphaInstrs[$ai + 1]
        $a2 = $AlphaInstrs[$ai + 2]
        $a3 = $AlphaInstrs[$ai + 3]
        if ($a0.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0 -and
            $a1.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and $a1.Operand.Name -eq "Content" -and
            $a2.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_R4 -and [single]$a2.Operand -eq [single]1.0 -and
            $a3.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Callvirt -and $a3.Operand.Name -eq "set_alpha") {
            $SetAlphaOneInstr = $a0  # branch to ldarg.0 so the full sequence runs
            break
        }
    }
    if (-not $SetAlphaOneInstr) { throw "Could not find ldarg.0/ldfld Content/ldc.r4 1/set_alpha block in AlphaUpdate." }

    $BranchToSetAlpha1 = $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Brtrue_S, $SetAlphaOneInstr)

    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $AlphaIl.Create([Mono.Cecil.Cil.OpCodes]::Ldfld, $ImportedShowNameField))
    $AlphaIl.InsertBefore($AlphaFirstInstr, $BranchToSetAlpha1)
}

$Assembly.Write($OutputPath)
$Assembly.Dispose()
$HelperAssembly.Dispose()
Copy-Item -LiteralPath $OutputPath -Destination $SavedCopyPath -Force
Remove-Item -LiteralPath $TempBasePath -Force

$Features = New-Object System.Collections.Generic.List[string]
if ($FovConfig.enabled) { $Features.Add("fov") | Out-Null }
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
if ($AimHealthbarConfig.enabled) { $Features.Add("aim-healthbar") | Out-Null }
$Hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA1).Hash
Write-Output "Experimental all-in-one DLL built. SHA1=$Hash features=$([string]::Join(',', $Features))"
