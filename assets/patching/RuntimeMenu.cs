using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BnlCommunityFixes
{
    [Serializable]
    public sealed class RuntimeMenuSettingsData
    {
        public float crosshair_size_multiplier = 1f;
        public float crosshair_spread_multiplier = 1f;
        public float crosshair_alpha = 1f;
        public bool crosshair_force_show_in_ads;
        public bool crosshair_hide_entirely;
        public string crosshair_force_shape = "__DEFAULT__";

        public float fov = 120f;
        public float ads_sensitivity_multiplier = 1f;
        public float weapon_model_fov = 30f;

        public float damage_number_size_multiplier = 2f;
        public float heal_number_size_multiplier = 2f;
        public float self_heal_number_size_multiplier = 0.7f;
        public float self_heal_x = 0f;
        public float self_heal_y = 0f;
        public float damage_number_offset_y = 65f;
        public float heal_number_offset_y = 65f;
        public float combat_alpha = 1f;
        public float minimum_heal = 0.5f;
        public bool show_friendly_healing;
        public bool show_self_healing;
        public bool combine_damage_until_hidden;
        public bool combine_healing_until_hidden;

        public float heal_alert_damage_size_multiplier = 1f;
        public float heal_alert_heal_size_multiplier = 1f;
        public float heal_alert_minimum_heal = 0.5f;
        public bool heal_alert_show_direction_on_heal;

        public string team_friendly_color = "#4AA3FF";
        public string team_enemy_color = "#FF5A5A";

        public bool hide_base_objective_beam;

        public bool local_build_preview_enabled;
        public float local_build_preview_timeout_seconds = 2f;

        public bool aim_healthbar_enabled = true;
        public bool deathcam_healthbar_enabled = true;
        public bool auto_casual_queue_enabled;
        public bool has_auto_casual_queue_enabled;
        public bool teammate_hp_enabled;
        public bool disable_auto_crouch;
        public bool has_disable_auto_crouch;
    }

    public static class RuntimeFeatureState
    {
        private static bool crosshairConfigured;
        private static bool fovConfigured;
        private static bool combatConfigured;
        private static bool healAlertConfigured;
        private static bool teamColorsConfigured;
        private static bool baseObjectiveBeamConfigured;
        private static bool localBuildPreviewConfigured;
        private static bool aimHealthbarConfigured;
        private static bool deathCamHealthbarConfigured;
        private static bool autoCasualQueueConfigured;

        private static Color defaultCrosshairIdleColor;
        private static Color defaultCrosshairFullDamageColor;
        private static Color defaultCrosshairBelowMaxColor;
        private static float defaultCrosshairSizeMultiplier = 1f;
        private static float defaultCrosshairSpreadMultiplier = 1f;
        private static float defaultCrosshairAlpha = 1f;
        private static bool defaultCrosshairForceShowInAds;
        private static bool defaultCrosshairHideEntirely;
        private static string defaultCrosshairForceShape = "__DEFAULT__";

        private static float defaultFov = 120f;
        private static float defaultAdsSensitivityMultiplier = 1f;
        private static float defaultWeaponModelFov = 30f;

        private static Color defaultDamageNumberColor = Color.white;
        private static Color defaultCritDamageNumberColor = Color.white;
        private static Color defaultHealNumberColor = Color.white;
        private static bool defaultUseDamageNumberColor;
        private static bool defaultUseCritDamageNumberColor;
        private static bool defaultUseHealNumberColor;
        private static float defaultDamageNumberSizeMultiplier = 2f;
        private static float defaultHealNumberSizeMultiplier = 2f;
        private static float defaultDamageNumberOffsetY = 65f;
        private static float defaultHealNumberOffsetY = 65f;
        private static float defaultCombatAlpha = 1f;
        private static float defaultMinimumHeal = 0.5f;
        private static bool defaultShowFriendlyHealing;
        private static bool defaultShowSelfHealing;
        private static bool defaultCombineDamageUntilHidden;
        private static bool defaultCombineHealingUntilHidden;

        private static float defaultHealAlertDamageSizeMultiplier = 1f;
        private static float defaultHealAlertHealSizeMultiplier = 1f;
        private static float defaultHealAlertMinimumHeal = 0.5f;
        private static bool defaultHealAlertShowDirectionOnHeal;

        private static Color defaultTeamFriendlyColor = new Color(74f / 255f, 163f / 255f, 1f, 1f);
        private static Color defaultTeamEnemyColor = new Color(1f, 90f / 255f, 90f / 255f, 1f);

        private static bool defaultHideBaseObjectiveBeam;

        private static bool defaultLocalBuildPreviewEnabled;
        private static float defaultLocalBuildPreviewTimeoutSeconds = 2f;

        private static bool defaultAimHealthbarEnabled = true;
        private static bool defaultDeathCamHealthbarEnabled = true;
        private static bool defaultAutoCasualQueueEnabled;
        private static bool defaultTeammateHpEnabled;
        private static bool teammateHpConfigured;
        private static bool defaultDisableAutoCrouch;
        private static bool autoCrouchConfigured;

        private static bool loadedFromDisk;
        private static RuntimeMenuSettingsData loadedData;

        public static bool CrosshairSupported { get; private set; }
        public static bool FovSupported { get; private set; }
        public static bool CombatNumbersSupported { get; private set; }
        public static bool HealAlertSupported { get; private set; }
        public static bool TeamColorsSupported { get; private set; }
        public static bool BaseObjectiveBeamSupported { get; private set; }
        public static bool LocalBuildPreviewSupported { get; private set; }
        public static bool AimHealthbarSupported { get; private set; }
        public static bool DeathCamHealthbarSupported { get; private set; }
        public static bool AutoCasualQueueSupported { get; private set; }
        public static bool TeammateHpSupported { get; private set; }
        public static bool AutoCrouchDisableSupported { get; private set; }

        public static float CrosshairSizeMultiplier { get; private set; }
        public static float CrosshairSpreadMultiplier { get; private set; }
        public static float CrosshairAlpha { get; private set; }
        public static bool CrosshairForceShowInAds { get; private set; }
        public static bool CrosshairHideEntirely { get; private set; }
        public static string CrosshairForceShape { get; private set; }

        public static float ForcedFov { get; private set; }
        public static float AdsSensitivityMultiplier { get; private set; }
        public static float WeaponModelFov { get; private set; }

        public static float DamageNumberSizeMultiplier { get; private set; }
        public static float HealNumberSizeMultiplier { get; private set; }
        public static float SelfHealNumberSizeMultiplier { get; private set; }
        public static float SelfHealX { get; private set; }
        public static float SelfHealY { get; private set; }
        public static float DamageNumberOffsetY { get; private set; }
        public static float HealNumberOffsetY { get; private set; }
        public static float CombatAlpha { get; private set; }
        public static float MinimumHeal { get; private set; }
        public static bool ShowFriendlyHealing { get; private set; }
        public static bool ShowSelfHealing { get; private set; }
        public static bool CombineDamageUntilHidden { get; private set; }
        public static bool CombineHealingUntilHidden { get; private set; }

        public static float HealAlertDamageSizeMultiplier { get; private set; }
        public static float HealAlertHealSizeMultiplier { get; private set; }
        public static float HealAlertMinimumHeal { get; private set; }
        public static bool HealAlertShowDirectionOnHeal { get; private set; }

        public static Color TeamFriendlyColor { get; private set; }
        public static Color TeamEnemyColor { get; private set; }

        public static bool HideBaseObjectiveBeam { get; private set; }

        public static bool LocalBuildPreviewEnabled { get; private set; }
        public static float LocalBuildPreviewTimeoutSeconds { get; private set; }

        public static bool AimHealthbarEnabled { get; private set; }
        public static bool DeathCamHealthbarEnabled { get; private set; }
        public static bool AutoCasualQueueEnabled { get; private set; }
        public static bool TeammateHpEnabled { get; private set; }
        public static bool AutoCrouchDisabled { get; private set; }

        static RuntimeFeatureState()
        {
            CrosshairSizeMultiplier = 1f;
            CrosshairSpreadMultiplier = 1f;
            CrosshairAlpha = 1f;
            CrosshairForceShape = "__DEFAULT__";

            ForcedFov = 120f;
            AdsSensitivityMultiplier = 1f;
            WeaponModelFov = 30f;

            DamageNumberSizeMultiplier = 2f;
            HealNumberSizeMultiplier = 2f;
            SelfHealNumberSizeMultiplier = 0.7f;
            SelfHealX = 0f;
            SelfHealY = 0f;
            DamageNumberOffsetY = 65f;
            HealNumberOffsetY = 65f;
            CombatAlpha = 1f;
            MinimumHeal = 0.5f;

            HealAlertDamageSizeMultiplier = 1f;
            HealAlertHealSizeMultiplier = 1f;
            HealAlertMinimumHeal = 0.5f;

            TeamFriendlyColor = defaultTeamFriendlyColor;
            TeamEnemyColor = defaultTeamEnemyColor;

            LocalBuildPreviewTimeoutSeconds = 2f;
            AimHealthbarEnabled = true;
            DeathCamHealthbarEnabled = true;
            AutoCasualQueueEnabled = false;
            TeammateHpEnabled = false;
        }

        private static string ConfigPath
        {
            get
            {
                string baseDir = Path.GetDirectoryName(typeof(RuntimeFeatureState).Assembly.Location);
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = ".";
                }

                return Path.Combine(baseDir, "bnl-runtime-menu.json");
            }
        }

        public static void ConfigureCrosshair(bool supported, Color idleColor, Color fullDamageColor, Color belowMaxColor, float sizeMultiplier, float spreadMultiplier, bool forceShowInAds, bool hideEntirely, string forceShape)
        {
            CrosshairSupported = CrosshairSupported || supported;
            defaultCrosshairIdleColor = new Color(idleColor.r, idleColor.g, idleColor.b, 1f);
            defaultCrosshairFullDamageColor = new Color(fullDamageColor.r, fullDamageColor.g, fullDamageColor.b, 1f);
            defaultCrosshairBelowMaxColor = new Color(belowMaxColor.r, belowMaxColor.g, belowMaxColor.b, 1f);
            defaultCrosshairSizeMultiplier = sizeMultiplier;
            defaultCrosshairSpreadMultiplier = spreadMultiplier;
            defaultCrosshairAlpha = Mathf.Clamp01(idleColor.a);
            defaultCrosshairForceShowInAds = forceShowInAds;
            defaultCrosshairHideEntirely = hideEntirely;
            defaultCrosshairForceShape = string.IsNullOrEmpty(forceShape) ? "__DEFAULT__" : forceShape;

            if (!crosshairConfigured)
            {
                CrosshairSizeMultiplier = defaultCrosshairSizeMultiplier;
                CrosshairSpreadMultiplier = defaultCrosshairSpreadMultiplier;
                CrosshairAlpha = defaultCrosshairAlpha;
                CrosshairForceShowInAds = defaultCrosshairForceShowInAds;
                CrosshairHideEntirely = defaultCrosshairHideEntirely;
                CrosshairForceShape = defaultCrosshairForceShape;
                crosshairConfigured = true;
                ApplyLoadedCrosshair();
            }
        }

        public static void ConfigureFov(bool supported, float fov, float adsSensitivityMultiplier, float weaponModelFov)
        {
            FovSupported = FovSupported || supported;
            defaultFov = fov;
            defaultAdsSensitivityMultiplier = adsSensitivityMultiplier;
            defaultWeaponModelFov = weaponModelFov;

            if (!fovConfigured)
            {
                ForcedFov = defaultFov;
                AdsSensitivityMultiplier = defaultAdsSensitivityMultiplier;
                WeaponModelFov = defaultWeaponModelFov;
                fovConfigured = true;
                ApplyLoadedFov();
            }
        }

        public static void ConfigureCombat(bool supported, Color damageColor, Color critDamageColor, Color healColor, bool useDamageColor, bool useCritDamageColor, bool useHealColor, float damageSizeMultiplier, float healSizeMultiplier, float selfHealSizeMultiplier, float combatAlpha, float minimumHeal, bool showFriendlyHealing, bool showSelfHealing, bool combineDamageUntilHidden, bool combineHealingUntilHidden, float damageOffsetY, float healOffsetY)
        {
            CombatNumbersSupported = CombatNumbersSupported || supported;
            defaultDamageNumberColor = new Color(damageColor.r, damageColor.g, damageColor.b, 1f);
            defaultCritDamageNumberColor = new Color(critDamageColor.r, critDamageColor.g, critDamageColor.b, 1f);
            defaultHealNumberColor = new Color(healColor.r, healColor.g, healColor.b, 1f);
            defaultUseDamageNumberColor = useDamageColor;
            defaultUseCritDamageNumberColor = useCritDamageColor;
            defaultUseHealNumberColor = useHealColor;
            defaultDamageNumberSizeMultiplier = damageSizeMultiplier;
            defaultHealNumberSizeMultiplier = healSizeMultiplier;
            if (selfHealSizeMultiplier > 0f) SelfHealNumberSizeMultiplier = selfHealSizeMultiplier;
            defaultDamageNumberOffsetY = damageOffsetY;
            defaultHealNumberOffsetY = healOffsetY;
            defaultCombatAlpha = Mathf.Clamp01(combatAlpha);
            defaultMinimumHeal = minimumHeal;
            defaultShowFriendlyHealing = showFriendlyHealing;
            defaultShowSelfHealing = showSelfHealing;
            defaultCombineDamageUntilHidden = combineDamageUntilHidden;
            defaultCombineHealingUntilHidden = combineHealingUntilHidden;

            if (!combatConfigured)
            {
                DamageNumberSizeMultiplier = defaultDamageNumberSizeMultiplier;
                HealNumberSizeMultiplier = defaultHealNumberSizeMultiplier;
                SelfHealNumberSizeMultiplier = defaultHealNumberSizeMultiplier * 0.35f;
                CombatAlpha = defaultCombatAlpha;
                MinimumHeal = defaultMinimumHeal;
                ShowFriendlyHealing = defaultShowFriendlyHealing;
                ShowSelfHealing = defaultShowSelfHealing;
                CombineDamageUntilHidden = defaultCombineDamageUntilHidden;
                CombineHealingUntilHidden = defaultCombineHealingUntilHidden;
                combatConfigured = true;
                ApplyLoadedCombat();
            }
        }

        public static void ConfigureHealAlert(bool supported, float damageSizeMultiplier, float healSizeMultiplier, float minimumHeal, bool showDirectionOnHeal)
        {
            HealAlertSupported = HealAlertSupported || supported;
            defaultHealAlertDamageSizeMultiplier = damageSizeMultiplier;
            defaultHealAlertHealSizeMultiplier = healSizeMultiplier;
            defaultHealAlertMinimumHeal = minimumHeal;
            defaultHealAlertShowDirectionOnHeal = showDirectionOnHeal;

            if (!healAlertConfigured)
            {
                HealAlertDamageSizeMultiplier = defaultHealAlertDamageSizeMultiplier;
                HealAlertHealSizeMultiplier = defaultHealAlertHealSizeMultiplier;
                HealAlertMinimumHeal = defaultHealAlertMinimumHeal;
                HealAlertShowDirectionOnHeal = defaultHealAlertShowDirectionOnHeal;
                healAlertConfigured = true;
                ApplyLoadedHealAlert();
            }
        }

        public static void ConfigureTeamColors(bool supported, Color friendlyColor, Color enemyColor)
        {
            TeamColorsSupported = TeamColorsSupported || supported;
            defaultTeamFriendlyColor = new Color(friendlyColor.r, friendlyColor.g, friendlyColor.b, 1f);
            defaultTeamEnemyColor = new Color(enemyColor.r, enemyColor.g, enemyColor.b, 1f);

            if (!teamColorsConfigured)
            {
                TeamFriendlyColor = defaultTeamFriendlyColor;
                TeamEnemyColor = defaultTeamEnemyColor;
                teamColorsConfigured = true;
                ApplyLoadedTeamColors();
            }
        }

        public static void ConfigureBaseObjectiveBeam(bool supported, bool hideBeam)
        {
            BaseObjectiveBeamSupported = BaseObjectiveBeamSupported || supported;
            defaultHideBaseObjectiveBeam = hideBeam;

            if (!baseObjectiveBeamConfigured)
            {
                HideBaseObjectiveBeam = defaultHideBaseObjectiveBeam;
                baseObjectiveBeamConfigured = true;
                ApplyLoadedBaseObjectiveBeam();
            }
        }

        public static void ConfigureLocalBuildPreview(bool supported, bool enabled, float timeoutSeconds)
        {
            LocalBuildPreviewSupported = LocalBuildPreviewSupported || supported;
            defaultLocalBuildPreviewEnabled = enabled;
            defaultLocalBuildPreviewTimeoutSeconds = timeoutSeconds;

            if (!localBuildPreviewConfigured)
            {
                LocalBuildPreviewEnabled = defaultLocalBuildPreviewEnabled;
                LocalBuildPreviewTimeoutSeconds = defaultLocalBuildPreviewTimeoutSeconds;
                localBuildPreviewConfigured = true;
                ApplyLoadedLocalBuildPreview();
            }
        }

        public static void ConfigureAimHealthbar(bool supported, bool enabled)
        {
            AimHealthbarSupported = AimHealthbarSupported || supported;
            defaultAimHealthbarEnabled = enabled;

            if (!aimHealthbarConfigured)
            {
                AimHealthbarEnabled = defaultAimHealthbarEnabled;
                aimHealthbarConfigured = true;
                ApplyLoadedAimHealthbar();
            }
        }

        public static void ConfigureDeathCamHealthbar(bool supported, bool enabled)
        {
            DeathCamHealthbarSupported = DeathCamHealthbarSupported || supported;
            defaultDeathCamHealthbarEnabled = enabled;

            if (!deathCamHealthbarConfigured)
            {
                DeathCamHealthbarEnabled = defaultDeathCamHealthbarEnabled;
                deathCamHealthbarConfigured = true;
                ApplyLoadedDeathCamHealthbar();
            }
        }

        public static void ConfigureAutoCasualQueue(bool supported, bool enabled)
        {
            AutoCasualQueueSupported = AutoCasualQueueSupported || supported;
            defaultAutoCasualQueueEnabled = enabled;

            if (!autoCasualQueueConfigured)
            {
                AutoCasualQueueEnabled = defaultAutoCasualQueueEnabled;
                autoCasualQueueConfigured = true;
                ApplyLoadedAutoCasualQueue();
            }
        }

        public static void ConfigureTeammateHp(bool supported, bool enabled)
        {
            TeammateHpSupported = TeammateHpSupported || supported;
            defaultTeammateHpEnabled = enabled;

            if (!teammateHpConfigured)
            {
                TeammateHpEnabled = defaultTeammateHpEnabled;
                teammateHpConfigured = true;
                ApplyLoadedTeammateHp();
            }
        }

        public static void ConfigureAutoCrouchDisable(bool supported, bool disabled)
        {
            AutoCrouchDisableSupported = AutoCrouchDisableSupported || supported;
            defaultDisableAutoCrouch = disabled;

            if (!autoCrouchConfigured)
            {
                AutoCrouchDisabled = defaultDisableAutoCrouch;
                autoCrouchConfigured = true;
                ApplyLoadedAutoCrouch();
            }
        }

        public static Color GetCrosshairIdleColor()
        {
            return WithAlpha(defaultCrosshairIdleColor, CrosshairAlpha);
        }

        public static Color GetCrosshairFullDamageColor()
        {
            return WithAlpha(defaultCrosshairFullDamageColor, CrosshairAlpha);
        }

        public static Color GetCrosshairBelowMaxColor()
        {
            return WithAlpha(defaultCrosshairBelowMaxColor, CrosshairAlpha);
        }

        public static bool UseDamageNumberColor
        {
            get { return defaultUseDamageNumberColor; }
        }

        public static bool UseCritDamageNumberColor
        {
            get { return defaultUseCritDamageNumberColor; }
        }

        public static bool UseHealNumberColor
        {
            get { return defaultUseHealNumberColor; }
        }

        public static Color GetDamageNumberColor()
        {
            return WithAlpha(defaultDamageNumberColor, CombatAlpha);
        }

        public static Color GetCritDamageNumberColor()
        {
            return WithAlpha(defaultCritDamageNumberColor, CombatAlpha);
        }

        public static Color GetHealNumberColor()
        {
            return WithAlpha(defaultHealNumberColor, CombatAlpha);
        }

        public static Color GetGuiFriendlyColor()
        {
            return TeamFriendlyColor;
        }

        public static Color GetGuiEnemyColor()
        {
            return TeamEnemyColor;
        }

        public static Color GetGuiBackgroundFriendlyColor()
        {
            return WithAlpha(TeamFriendlyColor, 0.45f);
        }

        public static Color GetGuiBackgroundEnemyColor()
        {
            return WithAlpha(TeamEnemyColor, 0.45f);
        }

        public static void Save()
        {
            RuntimeMenuSettingsData data = CaptureCurrentData();
            File.WriteAllText(ConfigPath, SerializeData(data));
            loadedData = data;
            loadedFromDisk = true;
        }

        public static void ResetToDefaults()
        {
            if (crosshairConfigured)
            {
                CrosshairSizeMultiplier = defaultCrosshairSizeMultiplier;
                CrosshairSpreadMultiplier = defaultCrosshairSpreadMultiplier;
                CrosshairAlpha = defaultCrosshairAlpha;
                CrosshairForceShowInAds = defaultCrosshairForceShowInAds;
                CrosshairHideEntirely = defaultCrosshairHideEntirely;
                CrosshairForceShape = defaultCrosshairForceShape;
            }

            if (fovConfigured)
            {
                ForcedFov = defaultFov;
                AdsSensitivityMultiplier = defaultAdsSensitivityMultiplier;
                WeaponModelFov = defaultWeaponModelFov;
            }

            if (combatConfigured)
            {
                DamageNumberSizeMultiplier = defaultDamageNumberSizeMultiplier;
                HealNumberSizeMultiplier = defaultHealNumberSizeMultiplier;
                DamageNumberOffsetY = defaultDamageNumberOffsetY;
                HealNumberOffsetY = defaultHealNumberOffsetY;
                CombatAlpha = defaultCombatAlpha;
                MinimumHeal = defaultMinimumHeal;
                ShowFriendlyHealing = defaultShowFriendlyHealing;
                ShowSelfHealing = defaultShowSelfHealing;
                CombineDamageUntilHidden = defaultCombineDamageUntilHidden;
                CombineHealingUntilHidden = defaultCombineHealingUntilHidden;
            }

            if (healAlertConfigured)
            {
                HealAlertDamageSizeMultiplier = defaultHealAlertDamageSizeMultiplier;
                HealAlertHealSizeMultiplier = defaultHealAlertHealSizeMultiplier;
                HealAlertMinimumHeal = defaultHealAlertMinimumHeal;
                HealAlertShowDirectionOnHeal = defaultHealAlertShowDirectionOnHeal;
            }

            if (teamColorsConfigured)
            {
                TeamFriendlyColor = defaultTeamFriendlyColor;
                TeamEnemyColor = defaultTeamEnemyColor;
            }

            if (baseObjectiveBeamConfigured)
            {
                HideBaseObjectiveBeam = defaultHideBaseObjectiveBeam;
            }

            if (localBuildPreviewConfigured)
            {
                LocalBuildPreviewEnabled = defaultLocalBuildPreviewEnabled;
                LocalBuildPreviewTimeoutSeconds = defaultLocalBuildPreviewTimeoutSeconds;
            }

            if (aimHealthbarConfigured)
            {
                AimHealthbarEnabled = defaultAimHealthbarEnabled;
            }

            if (deathCamHealthbarConfigured)
            {
                DeathCamHealthbarEnabled = defaultDeathCamHealthbarEnabled;
            }

            if (autoCasualQueueConfigured)
            {
                AutoCasualQueueEnabled = defaultAutoCasualQueueEnabled;
            }

            if (teammateHpConfigured)
            {
                TeammateHpEnabled = defaultTeammateHpEnabled;
            }

            if (autoCrouchConfigured)
            {
                AutoCrouchDisabled = defaultDisableAutoCrouch;
            }
        }

        public static void DeleteSavedConfig()
        {
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }

            loadedData = null;
            loadedFromDisk = false;
        }

        public static void SetCrosshairSizeMultiplier(float value)
        {
            CrosshairSizeMultiplier = Mathf.Clamp(value, 0.5f, 4f);
        }

        public static void SetCrosshairSpreadMultiplier(float value)
        {
            CrosshairSpreadMultiplier = Mathf.Clamp(value, 0.25f, 4f);
        }

        public static void SetCrosshairAlpha(float value)
        {
            CrosshairAlpha = Mathf.Clamp01(value);
        }

        public static void SetCrosshairForceShowInAds(bool value)
        {
            CrosshairForceShowInAds = value;
        }

        public static void SetCrosshairHideEntirely(bool value)
        {
            CrosshairHideEntirely = value;
        }

        public static void SetCrosshairForceShape(string value)
        {
            CrosshairForceShape = string.IsNullOrEmpty(value) ? "__DEFAULT__" : value;
        }

        public static void SetForcedFov(float value)
        {
            ForcedFov = Mathf.Clamp(value, 60f, 150f);
        }

        public static void SetAdsSensitivityMultiplier(float value)
        {
            AdsSensitivityMultiplier = Mathf.Clamp(value, 0.1f, 4f);
        }

        public static void SetWeaponModelFov(float value)
        {
            WeaponModelFov = Mathf.Clamp(value, 10f, 120f);
        }

        public static void SetDamageNumberSizeMultiplier(float value)
        {
            DamageNumberSizeMultiplier = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static void SetHealNumberSizeMultiplier(float value)
        {
            HealNumberSizeMultiplier = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static void SetSelfHealNumberSizeMultiplier(float value)
        {
            SelfHealNumberSizeMultiplier = Mathf.Clamp(value, 0.1f, 5f);
        }

        public static void SetSelfHealX(float value)
        {
            SelfHealX = Mathf.Clamp(value, -2000f, 2000f);
        }

        public static void SetSelfHealY(float value)
        {
            SelfHealY = Mathf.Clamp(value, -2000f, 2000f);
        }

        public static void SetDamageNumberOffsetY(float value)
        {
            DamageNumberOffsetY = Mathf.Clamp(value, -500f, 500f);
        }

        public static void SetHealNumberOffsetY(float value)
        {
            HealNumberOffsetY = Mathf.Clamp(value, -500f, 500f);
        }

        public static void SetCombatAlpha(float value)
        {
            CombatAlpha = Mathf.Clamp01(value);
        }

        public static void SetMinimumHeal(float value)
        {
            MinimumHeal = Mathf.Clamp(value, 0f, 100f);
        }

        public static void SetShowFriendlyHealing(bool value)
        {
            ShowFriendlyHealing = value;
        }

        public static void SetShowSelfHealing(bool value)
        {
            ShowSelfHealing = value;
        }

        public static void SetCombineDamageUntilHidden(bool value)
        {
            CombineDamageUntilHidden = value;
        }

        public static void SetCombineHealingUntilHidden(bool value)
        {
            CombineHealingUntilHidden = value;
        }

        public static void SetHealAlertDamageSizeMultiplier(float value)
        {
            HealAlertDamageSizeMultiplier = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static void SetHealAlertHealSizeMultiplier(float value)
        {
            HealAlertHealSizeMultiplier = Mathf.Clamp(value, 0.5f, 5f);
        }

        public static void SetHealAlertMinimumHeal(float value)
        {
            HealAlertMinimumHeal = Mathf.Clamp(value, 0f, 100f);
        }

        public static void SetHealAlertShowDirectionOnHeal(bool value)
        {
            HealAlertShowDirectionOnHeal = value;
        }

        public static void SetTeamFriendlyColor(Color value)
        {
            TeamFriendlyColor = new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), 1f);
        }

        public static void SetTeamEnemyColor(Color value)
        {
            TeamEnemyColor = new Color(Mathf.Clamp01(value.r), Mathf.Clamp01(value.g), Mathf.Clamp01(value.b), 1f);
        }

        public static void SetHideBaseObjectiveBeam(bool value)
        {
            HideBaseObjectiveBeam = value;
        }

        public static void SetLocalBuildPreviewEnabled(bool value)
        {
            LocalBuildPreviewEnabled = value;
        }

        public static void SetLocalBuildPreviewTimeoutSeconds(float value)
        {
            LocalBuildPreviewTimeoutSeconds = Mathf.Clamp(value, 0.25f, 10f);
        }

        public static void SetAimHealthbarEnabled(bool value)
        {
            AimHealthbarEnabled = value;
        }

        public static void SetDeathCamHealthbarEnabled(bool value)
        {
            DeathCamHealthbarEnabled = value;
        }

        public static void SetAutoCasualQueueEnabled(bool value)
        {
            AutoCasualQueueEnabled = value;
        }

        public static void SetTeammateHpEnabled(bool value)
        {
            TeammateHpEnabled = value;
        }

        public static void SetAutoCrouchDisabled(bool value)
        {
            AutoCrouchDisabled = value;
        }

        private static void EnsureLoadedData()
        {
            if (loadedFromDisk)
            {
                return;
            }

            loadedFromDisk = true;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    loadedData = DeserializeData(File.ReadAllText(ConfigPath));
                }
            }
            catch
            {
                loadedData = null;
            }
        }

        private static void ApplyLoadedCrosshair()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            CrosshairSizeMultiplier = Mathf.Clamp(loadedData.crosshair_size_multiplier, 0.5f, 4f);
            CrosshairSpreadMultiplier = Mathf.Clamp(loadedData.crosshair_spread_multiplier, 0.25f, 4f);
            CrosshairAlpha = Mathf.Clamp01(loadedData.crosshair_alpha);
            CrosshairForceShowInAds = loadedData.crosshair_force_show_in_ads;
            CrosshairHideEntirely = loadedData.crosshair_hide_entirely;
            CrosshairForceShape = string.IsNullOrEmpty(loadedData.crosshair_force_shape) ? "__DEFAULT__" : loadedData.crosshair_force_shape;
        }

        private static void ApplyLoadedFov()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            ForcedFov = Mathf.Clamp(loadedData.fov, 60f, 150f);
            AdsSensitivityMultiplier = Mathf.Clamp(loadedData.ads_sensitivity_multiplier, 0.1f, 4f);
            WeaponModelFov = Mathf.Clamp(loadedData.weapon_model_fov, 10f, 120f);
        }

        private static void ApplyLoadedCombat()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            DamageNumberSizeMultiplier = Mathf.Clamp(loadedData.damage_number_size_multiplier, 0.5f, 5f);
            HealNumberSizeMultiplier = Mathf.Clamp(loadedData.heal_number_size_multiplier, 0.5f, 5f);
            SelfHealNumberSizeMultiplier = Mathf.Clamp(loadedData.self_heal_number_size_multiplier, 0.1f, 5f);
            SelfHealX = loadedData.self_heal_x;
            SelfHealY = loadedData.self_heal_y;
            DamageNumberOffsetY = Mathf.Clamp(loadedData.damage_number_offset_y, -500f, 500f);
            HealNumberOffsetY = Mathf.Clamp(loadedData.heal_number_offset_y, -500f, 500f);
            CombatAlpha = Mathf.Clamp01(loadedData.combat_alpha);
            MinimumHeal = Mathf.Clamp(loadedData.minimum_heal, 0f, 100f);
            ShowFriendlyHealing = loadedData.show_friendly_healing;
            ShowSelfHealing = loadedData.show_self_healing;
            CombineDamageUntilHidden = loadedData.combine_damage_until_hidden;
            CombineHealingUntilHidden = loadedData.combine_healing_until_hidden;
        }

        private static void ApplyLoadedHealAlert()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            HealAlertDamageSizeMultiplier = Mathf.Clamp(loadedData.heal_alert_damage_size_multiplier, 0.5f, 5f);
            HealAlertHealSizeMultiplier = Mathf.Clamp(loadedData.heal_alert_heal_size_multiplier, 0.5f, 5f);
            HealAlertMinimumHeal = Mathf.Clamp(loadedData.heal_alert_minimum_heal, 0f, 100f);
            HealAlertShowDirectionOnHeal = loadedData.heal_alert_show_direction_on_heal;
        }

        private static void ApplyLoadedTeamColors()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            TeamFriendlyColor = ParseHexColorOrDefault(loadedData.team_friendly_color, defaultTeamFriendlyColor);
            TeamEnemyColor = ParseHexColorOrDefault(loadedData.team_enemy_color, defaultTeamEnemyColor);
        }

        private static void ApplyLoadedBaseObjectiveBeam()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            HideBaseObjectiveBeam = loadedData.hide_base_objective_beam;
        }

        private static void ApplyLoadedLocalBuildPreview()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            LocalBuildPreviewEnabled = loadedData.local_build_preview_enabled;
            LocalBuildPreviewTimeoutSeconds = Mathf.Clamp(loadedData.local_build_preview_timeout_seconds, 0.25f, 10f);
        }

        private static void ApplyLoadedAimHealthbar()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            AimHealthbarEnabled = loadedData.aim_healthbar_enabled;
        }

        private static void ApplyLoadedDeathCamHealthbar()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            DeathCamHealthbarEnabled = loadedData.deathcam_healthbar_enabled;
        }

        private static void ApplyLoadedAutoCasualQueue()
        {
            EnsureLoadedData();
            if (loadedData == null || !loadedData.has_auto_casual_queue_enabled)
            {
                return;
            }

            AutoCasualQueueEnabled = loadedData.auto_casual_queue_enabled;
        }

        private static void ApplyLoadedTeammateHp()
        {
            EnsureLoadedData();
            if (loadedData == null)
            {
                return;
            }

            TeammateHpEnabled = loadedData.teammate_hp_enabled;
        }

        private static void ApplyLoadedAutoCrouch()
        {
            EnsureLoadedData();
            if (loadedData == null || !loadedData.has_disable_auto_crouch)
            {
                return;
            }

            AutoCrouchDisabled = loadedData.disable_auto_crouch;
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            return new Color(value.r, value.g, value.b, Mathf.Clamp01(alpha));
        }

        private static RuntimeMenuSettingsData CaptureCurrentData()
        {
            RuntimeMenuSettingsData data = new RuntimeMenuSettingsData();
            data.crosshair_size_multiplier = CrosshairSizeMultiplier;
            data.crosshair_spread_multiplier = CrosshairSpreadMultiplier;
            data.crosshair_alpha = CrosshairAlpha;
            data.crosshair_force_show_in_ads = CrosshairForceShowInAds;
            data.crosshair_hide_entirely = CrosshairHideEntirely;
            data.crosshair_force_shape = CrosshairForceShape;
            data.fov = ForcedFov;
            data.ads_sensitivity_multiplier = AdsSensitivityMultiplier;
            data.weapon_model_fov = WeaponModelFov;
            data.damage_number_size_multiplier = DamageNumberSizeMultiplier;
            data.heal_number_size_multiplier = HealNumberSizeMultiplier;
            data.self_heal_number_size_multiplier = SelfHealNumberSizeMultiplier;
            data.self_heal_x = SelfHealX;
            data.self_heal_y = SelfHealY;
            data.damage_number_offset_y = DamageNumberOffsetY;
            data.heal_number_offset_y = HealNumberOffsetY;
            data.combat_alpha = CombatAlpha;
            data.minimum_heal = MinimumHeal;
            data.show_friendly_healing = ShowFriendlyHealing;
            data.show_self_healing = ShowSelfHealing;
            data.combine_damage_until_hidden = CombineDamageUntilHidden;
            data.combine_healing_until_hidden = CombineHealingUntilHidden;
            data.heal_alert_damage_size_multiplier = HealAlertDamageSizeMultiplier;
            data.heal_alert_heal_size_multiplier = HealAlertHealSizeMultiplier;
            data.heal_alert_minimum_heal = HealAlertMinimumHeal;
            data.heal_alert_show_direction_on_heal = HealAlertShowDirectionOnHeal;
            data.team_friendly_color = ColorToHex(TeamFriendlyColor);
            data.team_enemy_color = ColorToHex(TeamEnemyColor);
            data.hide_base_objective_beam = HideBaseObjectiveBeam;
            data.local_build_preview_enabled = LocalBuildPreviewEnabled;
            data.local_build_preview_timeout_seconds = LocalBuildPreviewTimeoutSeconds;
            data.aim_healthbar_enabled = AimHealthbarEnabled;
            data.deathcam_healthbar_enabled = DeathCamHealthbarEnabled;
            data.auto_casual_queue_enabled = AutoCasualQueueEnabled;
            data.teammate_hp_enabled = TeammateHpEnabled;
            data.disable_auto_crouch = AutoCrouchDisabled;
            return data;
        }

        private static string SerializeData(RuntimeMenuSettingsData data)
        {
            List<string> lines = new List<string>();
            lines.Add("crosshair_size_multiplier=" + data.crosshair_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("crosshair_spread_multiplier=" + data.crosshair_spread_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("crosshair_alpha=" + data.crosshair_alpha.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("crosshair_force_show_in_ads=" + data.crosshair_force_show_in_ads);
            lines.Add("crosshair_hide_entirely=" + data.crosshair_hide_entirely);
            lines.Add("crosshair_force_shape=" + (data.crosshair_force_shape ?? "__DEFAULT__"));
            lines.Add("fov=" + data.fov.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("ads_sensitivity_multiplier=" + data.ads_sensitivity_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("weapon_model_fov=" + data.weapon_model_fov.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("damage_number_size_multiplier=" + data.damage_number_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("heal_number_size_multiplier=" + data.heal_number_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("self_heal_number_size_multiplier=" + data.self_heal_number_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("self_heal_x=" + data.self_heal_x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("self_heal_y=" + data.self_heal_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("damage_number_offset_y=" + data.damage_number_offset_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("heal_number_offset_y=" + data.heal_number_offset_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("combat_alpha=" + data.combat_alpha.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("minimum_heal=" + data.minimum_heal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("show_friendly_healing=" + data.show_friendly_healing);
            lines.Add("show_self_healing=" + data.show_self_healing);
            lines.Add("combine_damage_until_hidden=" + data.combine_damage_until_hidden);
            lines.Add("combine_healing_until_hidden=" + data.combine_healing_until_hidden);
            lines.Add("heal_alert_damage_size_multiplier=" + data.heal_alert_damage_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("heal_alert_heal_size_multiplier=" + data.heal_alert_heal_size_multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("heal_alert_minimum_heal=" + data.heal_alert_minimum_heal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("heal_alert_show_direction_on_heal=" + data.heal_alert_show_direction_on_heal);
            lines.Add("team_friendly_color=" + (data.team_friendly_color ?? "#4AA3FF"));
            lines.Add("team_enemy_color=" + (data.team_enemy_color ?? "#FF5A5A"));
            lines.Add("hide_base_objective_beam=" + data.hide_base_objective_beam);
            lines.Add("local_build_preview_enabled=" + data.local_build_preview_enabled);
            lines.Add("local_build_preview_timeout_seconds=" + data.local_build_preview_timeout_seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines.Add("aim_healthbar_enabled=" + data.aim_healthbar_enabled);
            lines.Add("deathcam_healthbar_enabled=" + data.deathcam_healthbar_enabled);
            lines.Add("auto_casual_queue_enabled=" + data.auto_casual_queue_enabled);
            lines.Add("teammate_hp_enabled=" + data.teammate_hp_enabled);
            lines.Add("disable_auto_crouch=" + data.disable_auto_crouch);
            return string.Join("\n", lines.ToArray());
        }

        private static RuntimeMenuSettingsData DeserializeData(string content)
        {
            RuntimeMenuSettingsData data = new RuntimeMenuSettingsData();
            if (string.IsNullOrEmpty(content))
            {
                return data;
            }

            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();
                ApplyKeyValue(data, key, value);
            }

            return data;
        }

        private static void ApplyKeyValue(RuntimeMenuSettingsData data, string key, string value)
        {
            float parsedFloat;
            bool parsedBool;

            switch (key)
            {
                case "crosshair_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.crosshair_size_multiplier = parsedFloat;
                    break;
                case "crosshair_spread_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.crosshair_spread_multiplier = parsedFloat;
                    break;
                case "crosshair_alpha":
                    if (TryParseFloat(value, out parsedFloat)) data.crosshair_alpha = parsedFloat;
                    break;
                case "crosshair_force_show_in_ads":
                    if (bool.TryParse(value, out parsedBool)) data.crosshair_force_show_in_ads = parsedBool;
                    break;
                case "crosshair_hide_entirely":
                    if (bool.TryParse(value, out parsedBool)) data.crosshair_hide_entirely = parsedBool;
                    break;
                case "crosshair_force_shape":
                    data.crosshair_force_shape = string.IsNullOrEmpty(value) ? "__DEFAULT__" : value;
                    break;
                case "fov":
                    if (TryParseFloat(value, out parsedFloat)) data.fov = parsedFloat;
                    break;
                case "ads_sensitivity_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.ads_sensitivity_multiplier = parsedFloat;
                    break;
                case "weapon_model_fov":
                    if (TryParseFloat(value, out parsedFloat)) data.weapon_model_fov = parsedFloat;
                    break;
                case "damage_number_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.damage_number_size_multiplier = parsedFloat;
                    break;
                case "heal_number_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.heal_number_size_multiplier = parsedFloat;
                    break;
                case "self_heal_number_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.self_heal_number_size_multiplier = parsedFloat;
                    break;
                case "self_heal_x":
                    if (TryParseFloat(value, out parsedFloat)) data.self_heal_x = parsedFloat;
                    break;
                case "self_heal_y":
                    if (TryParseFloat(value, out parsedFloat)) data.self_heal_y = parsedFloat;
                    break;
                case "damage_number_offset_y":
                    if (TryParseFloat(value, out parsedFloat)) data.damage_number_offset_y = parsedFloat;
                    break;
                case "heal_number_offset_y":
                    if (TryParseFloat(value, out parsedFloat)) data.heal_number_offset_y = parsedFloat;
                    break;
                case "combat_alpha":
                    if (TryParseFloat(value, out parsedFloat)) data.combat_alpha = parsedFloat;
                    break;
                case "minimum_heal":
                    if (TryParseFloat(value, out parsedFloat)) data.minimum_heal = parsedFloat;
                    break;
                case "show_friendly_healing":
                    if (bool.TryParse(value, out parsedBool)) data.show_friendly_healing = parsedBool;
                    break;
                case "show_self_healing":
                    if (bool.TryParse(value, out parsedBool)) data.show_self_healing = parsedBool;
                    break;
                case "combine_damage_until_hidden":
                    if (bool.TryParse(value, out parsedBool)) data.combine_damage_until_hidden = parsedBool;
                    break;
                case "combine_healing_until_hidden":
                    if (bool.TryParse(value, out parsedBool)) data.combine_healing_until_hidden = parsedBool;
                    break;
                case "heal_alert_damage_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.heal_alert_damage_size_multiplier = parsedFloat;
                    break;
                case "heal_alert_heal_size_multiplier":
                    if (TryParseFloat(value, out parsedFloat)) data.heal_alert_heal_size_multiplier = parsedFloat;
                    break;
                case "heal_alert_minimum_heal":
                    if (TryParseFloat(value, out parsedFloat)) data.heal_alert_minimum_heal = parsedFloat;
                    break;
                case "heal_alert_show_direction_on_heal":
                    if (bool.TryParse(value, out parsedBool)) data.heal_alert_show_direction_on_heal = parsedBool;
                    break;
                case "team_friendly_color":
                    data.team_friendly_color = value;
                    break;
                case "team_enemy_color":
                    data.team_enemy_color = value;
                    break;
                case "hide_base_objective_beam":
                    if (bool.TryParse(value, out parsedBool)) data.hide_base_objective_beam = parsedBool;
                    break;
                case "local_build_preview_enabled":
                    if (bool.TryParse(value, out parsedBool)) data.local_build_preview_enabled = parsedBool;
                    break;
                case "local_build_preview_timeout_seconds":
                    if (TryParseFloat(value, out parsedFloat)) data.local_build_preview_timeout_seconds = parsedFloat;
                    break;
                case "aim_healthbar_enabled":
                    if (bool.TryParse(value, out parsedBool)) data.aim_healthbar_enabled = parsedBool;
                    break;
                case "deathcam_healthbar_enabled":
                    if (bool.TryParse(value, out parsedBool)) data.deathcam_healthbar_enabled = parsedBool;
                    break;
                case "auto_casual_queue_enabled":
                    if (bool.TryParse(value, out parsedBool))
                    {
                        data.auto_casual_queue_enabled = parsedBool;
                        data.has_auto_casual_queue_enabled = true;
                    }
                    break;
                case "teammate_hp_enabled":
                    if (bool.TryParse(value, out parsedBool)) data.teammate_hp_enabled = parsedBool;
                    break;
                case "disable_auto_crouch":
                    if (bool.TryParse(value, out parsedBool))
                    {
                        data.disable_auto_crouch = parsedBool;
                        data.has_disable_auto_crouch = true;
                    }
                    break;
            }
        }

        private static bool TryParseFloat(string value, out float parsed)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed);
        }

        private static string ColorToHex(Color color)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        private static Color ParseHexColorOrDefault(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            string normalized = value.Trim();
            if (normalized.StartsWith("#"))
            {
                normalized = normalized.Substring(1);
            }

            if (normalized.Length != 6)
            {
                return fallback;
            }

            try
            {
                int r = Convert.ToInt32(normalized.Substring(0, 2), 16);
                int g = Convert.ToInt32(normalized.Substring(2, 2), 16);
                int b = Convert.ToInt32(normalized.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }
            catch
            {
                return fallback;
            }
        }
    }

    public sealed class RuntimeSettingsMenuManager : MonoBehaviour
    {
        private static RuntimeSettingsMenuManager instance;
        private static readonly string[] CrosshairShapes = { "__DEFAULT__", "Dot", "Crosshair", "BrokenCircle", "Hashed", "HashedCrosshair", "Melee" };
        private static readonly string[] TeamColorPresets = { "#4AA3FF", "#66CC66", "#FFD24A", "#FF8A4A", "#FF5A5A", "#FF66CC", "#FFFFFF" };

        private bool visible;
        private int selectedIndex;

        public static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = GameObject.Find("BNL_RUNTIME_SETTINGS_MENU");
            if (go == null)
            {
                go = new GameObject("BNL_RUNTIME_SETTINGS_MENU");
            }

            instance = go.GetComponent<RuntimeSettingsMenuManager>();
            if (instance == null)
            {
                instance = go.AddComponent<RuntimeSettingsMenuManager>();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                visible = !visible;
            }

            if (!visible)
            {
                return;
            }

            int count = GetEntryCount();
            if (count <= 0)
            {
                selectedIndex = 0;
                return;
            }

            if (selectedIndex >= count)
            {
                selectedIndex = 0;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedIndex = (selectedIndex - 1 + count) % count;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedIndex = (selectedIndex + 1) % count;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ApplyEntryDelta(selectedIndex, -1);
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ApplyEntryDelta(selectedIndex, 1);
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                RuntimeFeatureState.Save();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                RuntimeFeatureState.ResetToDefaults();
                RuntimeFeatureState.DeleteSavedConfig();
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            int count = GetEntryCount();
            if (count <= 0)
            {
                GUI.Box(new Rect(24f, 24f, 520f, 88f), string.Empty);
                GUI.Label(new Rect(36f, 36f, 480f, 20f), "Runtime Settings  |  F8 close");
                GUI.Label(new Rect(36f, 64f, 480f, 20f), "No runtime-adjustable settings are active.");
                return;
            }

            GUI.Box(new Rect(24f, 24f, 520f, 32f + (count * 22f) + 44f), string.Empty);
            GUI.Label(new Rect(36f, 36f, 480f, 20f), "Runtime Settings  |  F8 close  |  Arrows navigate/change  |  F5 save  |  F6 reset");

            for (int i = 0; i < count; i++)
            {
                string prefix = i == selectedIndex ? "> " : "  ";
                GUI.Label(new Rect(36f, 64f + (i * 22f), 480f, 20f), prefix + GetEntryLabel(i) + ": " + GetEntryValueText(i));
            }
        }

        private static int GetEntryCount()
        {
            int count = 0;
            if (RuntimeFeatureState.CrosshairSupported)
            {
                count += 6;
            }

            if (RuntimeFeatureState.CombatNumbersSupported)
            {
                count += 8;
            }

            if (RuntimeFeatureState.HealAlertSupported)
            {
                count += 4;
            }

            if (RuntimeFeatureState.TeamColorsSupported)
            {
                count += 2;
            }

            if (RuntimeFeatureState.BaseObjectiveBeamSupported)
            {
                count += 1;
            }

            if (RuntimeFeatureState.LocalBuildPreviewSupported)
            {
                count += 2;
            }

            if (RuntimeFeatureState.AimHealthbarSupported)
            {
                count += 1;
            }

            if (RuntimeFeatureState.DeathCamHealthbarSupported)
            {
                count += 1;
            }

            if (RuntimeFeatureState.AutoCasualQueueSupported)
            {
                count += 1;
            }

            if (RuntimeFeatureState.TeammateHpSupported)
            {
                count += 1;
            }

            if (RuntimeFeatureState.AutoCrouchDisableSupported)
            {
                count += 1;
            }

            if (DebugMenuRuntime.Enabled)
            {
                count += 1;
            }

            return count;
        }

        private static string GetEntryLabel(int index)
        {
            if (RuntimeFeatureState.CrosshairSupported)
            {
                switch (index)
                {
                    case 0: return "Crosshair size";
                    case 1: return "Crosshair spread";
                    case 2: return "Crosshair alpha";
                    case 3: return "Crosshair force ADS";
                    case 4: return "Crosshair hide";
                    case 5: return "Crosshair shape";
                }

                index -= 6;
            }

            if (RuntimeFeatureState.CombatNumbersSupported)
            {
                switch (index)
                {
                    case 0: return "Damage size";
                    case 1: return "Heal size";
                    case 2: return "Self-heal size";
                    case 3: return "Combat alpha";
                    case 4: return "Minimum heal";
                    case 5: return "Show friendly healing";
                    case 6: return "Show self healing";
                    case 7: return "Combine damage";
                    case 8: return "Combine healing";
                    case 9: return "Self-heal X";
                    case 10: return "Self-heal Y";
                }

                index -= 11;
            }

            if (RuntimeFeatureState.HealAlertSupported)
            {
                switch (index)
                {
                    case 0: return "Heal alert damage size";
                    case 1: return "Heal alert heal size";
                    case 2: return "Heal alert minimum heal";
                    case 3: return "Heal alert show direction";
                }

                index -= 4;
            }

            if (RuntimeFeatureState.TeamColorsSupported)
            {
                switch (index)
                {
                    case 0: return "Friendly team color";
                    case 1: return "Enemy team color";
                }

                index -= 2;
            }

            if (RuntimeFeatureState.BaseObjectiveBeamSupported)
            {
                if (index == 0) return "Hide base objective beam";
                index -= 1;
            }

            if (RuntimeFeatureState.LocalBuildPreviewSupported)
            {
                switch (index)
                {
                    case 0: return "Local build preview";
                    case 1: return "Build preview timeout";
                }

                index -= 2;
            }

            if (RuntimeFeatureState.AimHealthbarSupported)
            {
                if (index == 0) return "Aim healthbar";
                index -= 1;
            }

            if (RuntimeFeatureState.DeathCamHealthbarSupported)
            {
                if (index == 0) return "Deathcam healthbar";
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCasualQueueSupported)
            {
                if (index == 0) return "Auto casual queue";
                index -= 1;
            }

            if (RuntimeFeatureState.TeammateHpSupported)
            {
                if (index == 0) return "Teammate HP";
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCrouchDisableSupported)
            {
                if (index == 0) return "Disable auto-crouch";
                index -= 1;
            }

            if (DebugMenuRuntime.Enabled)
            {
                if (index == 0) return "Debug force start match";
            }

            return "Unknown";
        }

        private static string GetEntryValueText(int index)
        {
            if (RuntimeFeatureState.CrosshairSupported)
            {
                switch (index)
                {
                    case 0: return RuntimeFeatureState.CrosshairSizeMultiplier.ToString("0.00");
                    case 1: return RuntimeFeatureState.CrosshairSpreadMultiplier.ToString("0.00");
                    case 2: return RuntimeFeatureState.CrosshairAlpha.ToString("0.00");
                    case 3: return RuntimeFeatureState.CrosshairForceShowInAds ? "ON" : "OFF";
                    case 4: return RuntimeFeatureState.CrosshairHideEntirely ? "ON" : "OFF";
                    case 5: return RuntimeFeatureState.CrosshairForceShape;
                }

                index -= 6;
            }

            if (RuntimeFeatureState.CombatNumbersSupported)
            {
                switch (index)
                {
                    case 0: return RuntimeFeatureState.DamageNumberSizeMultiplier.ToString("0.00");
                    case 1: return RuntimeFeatureState.HealNumberSizeMultiplier.ToString("0.00");
                    case 2: return RuntimeFeatureState.SelfHealNumberSizeMultiplier.ToString("0.00");
                    case 3: return RuntimeFeatureState.CombatAlpha.ToString("0.00");
                    case 4: return RuntimeFeatureState.MinimumHeal.ToString("0.0");
                    case 5: return RuntimeFeatureState.ShowFriendlyHealing ? "ON" : "OFF";
                    case 6: return RuntimeFeatureState.ShowSelfHealing ? "ON" : "OFF";
                    case 7: return RuntimeFeatureState.CombineDamageUntilHidden ? "ON" : "OFF";
                    case 8: return RuntimeFeatureState.CombineHealingUntilHidden ? "ON" : "OFF";
                    case 9: return RuntimeFeatureState.SelfHealX.ToString("0");
                    case 10: return RuntimeFeatureState.SelfHealY.ToString("0");
                }

                index -= 11;
            }

            if (RuntimeFeatureState.HealAlertSupported)
            {
                switch (index)
                {
                    case 0: return RuntimeFeatureState.HealAlertDamageSizeMultiplier.ToString("0.00");
                    case 1: return RuntimeFeatureState.HealAlertHealSizeMultiplier.ToString("0.00");
                    case 2: return RuntimeFeatureState.HealAlertMinimumHeal.ToString("0.0");
                    case 3: return RuntimeFeatureState.HealAlertShowDirectionOnHeal ? "ON" : "OFF";
                }

                index -= 4;
            }

            if (RuntimeFeatureState.TeamColorsSupported)
            {
                switch (index)
                {
                    case 0: return FormatColor(RuntimeFeatureState.TeamFriendlyColor);
                    case 1: return FormatColor(RuntimeFeatureState.TeamEnemyColor);
                }

                index -= 2;
            }

            if (RuntimeFeatureState.BaseObjectiveBeamSupported)
            {
                if (index == 0) return RuntimeFeatureState.HideBaseObjectiveBeam ? "ON" : "OFF";
                index -= 1;
            }

            if (RuntimeFeatureState.LocalBuildPreviewSupported)
            {
                switch (index)
                {
                    case 0: return RuntimeFeatureState.LocalBuildPreviewEnabled ? "ON" : "OFF";
                    case 1: return RuntimeFeatureState.LocalBuildPreviewTimeoutSeconds.ToString("0.00");
                }

                index -= 2;
            }

            if (RuntimeFeatureState.AimHealthbarSupported)
            {
                if (index == 0) return RuntimeFeatureState.AimHealthbarEnabled ? "ON" : "OFF";
                index -= 1;
            }

            if (RuntimeFeatureState.DeathCamHealthbarSupported)
            {
                if (index == 0) return RuntimeFeatureState.DeathCamHealthbarEnabled ? "ON" : "OFF";
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCasualQueueSupported)
            {
                if (index == 0) return RuntimeFeatureState.AutoCasualQueueEnabled ? "ON" : "OFF";
                index -= 1;
            }

            if (RuntimeFeatureState.TeammateHpSupported)
            {
                if (index == 0) return RuntimeFeatureState.TeammateHpEnabled ? "ON" : "OFF";
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCrouchDisableSupported)
            {
                if (index == 0) return RuntimeFeatureState.AutoCrouchDisabled ? "ON" : "OFF";
                index -= 1;
            }

            if (DebugMenuRuntime.Enabled)
            {
                if (index == 0) return "PRESS RIGHT / ENTER";
            }

            return string.Empty;
        }

        private static void ApplyEntryDelta(int index, int direction)
        {
            if (RuntimeFeatureState.CrosshairSupported)
            {
                switch (index)
                {
                    case 0:
                        RuntimeFeatureState.SetCrosshairSizeMultiplier(RuntimeFeatureState.CrosshairSizeMultiplier + (0.05f * direction));
                        return;
                    case 1:
                        RuntimeFeatureState.SetCrosshairSpreadMultiplier(RuntimeFeatureState.CrosshairSpreadMultiplier + (0.05f * direction));
                        return;
                    case 2:
                        RuntimeFeatureState.SetCrosshairAlpha(RuntimeFeatureState.CrosshairAlpha + (0.05f * direction));
                        return;
                    case 3:
                        RuntimeFeatureState.SetCrosshairForceShowInAds(!RuntimeFeatureState.CrosshairForceShowInAds);
                        return;
                    case 4:
                        RuntimeFeatureState.SetCrosshairHideEntirely(!RuntimeFeatureState.CrosshairHideEntirely);
                        return;
                    case 5:
                        CycleCrosshairShape(direction);
                        return;
                }

                index -= 6;
            }

            if (RuntimeFeatureState.CombatNumbersSupported)
            {
                switch (index)
                {
                    case 0:
                        RuntimeFeatureState.SetDamageNumberSizeMultiplier(RuntimeFeatureState.DamageNumberSizeMultiplier + (0.05f * direction));
                        return;
                    case 1:
                        RuntimeFeatureState.SetHealNumberSizeMultiplier(RuntimeFeatureState.HealNumberSizeMultiplier + (0.05f * direction));
                        return;
                    case 2:
                        RuntimeFeatureState.SetSelfHealNumberSizeMultiplier(RuntimeFeatureState.SelfHealNumberSizeMultiplier + (0.05f * direction));
                        return;
                    case 3:
                        RuntimeFeatureState.SetCombatAlpha(RuntimeFeatureState.CombatAlpha + (0.05f * direction));
                        return;
                    case 4:
                        RuntimeFeatureState.SetMinimumHeal(RuntimeFeatureState.MinimumHeal + (1f * direction));
                        return;
                    case 5:
                        RuntimeFeatureState.SetShowFriendlyHealing(!RuntimeFeatureState.ShowFriendlyHealing);
                        return;
                    case 6:
                        RuntimeFeatureState.SetShowSelfHealing(!RuntimeFeatureState.ShowSelfHealing);
                        return;
                    case 7:
                        RuntimeFeatureState.SetCombineDamageUntilHidden(!RuntimeFeatureState.CombineDamageUntilHidden);
                        return;
                    case 8:
                        RuntimeFeatureState.SetCombineHealingUntilHidden(!RuntimeFeatureState.CombineHealingUntilHidden);
                        return;
                    case 9:
                        RuntimeFeatureState.SetSelfHealX(RuntimeFeatureState.SelfHealX + (5f * direction));
                        return;
                    case 10:
                        RuntimeFeatureState.SetSelfHealY(RuntimeFeatureState.SelfHealY + (5f * direction));
                        return;
                }

                index -= 11;
            }

            if (RuntimeFeatureState.HealAlertSupported)
            {
                switch (index)
                {
                    case 0:
                        RuntimeFeatureState.SetHealAlertDamageSizeMultiplier(RuntimeFeatureState.HealAlertDamageSizeMultiplier + (0.05f * direction));
                        return;
                    case 1:
                        RuntimeFeatureState.SetHealAlertHealSizeMultiplier(RuntimeFeatureState.HealAlertHealSizeMultiplier + (0.05f * direction));
                        return;
                    case 2:
                        RuntimeFeatureState.SetHealAlertMinimumHeal(RuntimeFeatureState.HealAlertMinimumHeal + (0.5f * direction));
                        return;
                    case 3:
                        RuntimeFeatureState.SetHealAlertShowDirectionOnHeal(!RuntimeFeatureState.HealAlertShowDirectionOnHeal);
                        return;
                }

                index -= 4;
            }

            if (RuntimeFeatureState.TeamColorsSupported)
            {
                switch (index)
                {
                    case 0:
                        CycleTeamColor(true, direction);
                        return;
                    case 1:
                        CycleTeamColor(false, direction);
                        return;
                }

                index -= 2;
            }

            if (RuntimeFeatureState.BaseObjectiveBeamSupported)
            {
                if (index == 0)
                {
                    RuntimeFeatureState.SetHideBaseObjectiveBeam(!RuntimeFeatureState.HideBaseObjectiveBeam);
                    return;
                }

                index -= 1;
            }

            if (RuntimeFeatureState.LocalBuildPreviewSupported)
            {
                switch (index)
                {
                    case 0:
                        RuntimeFeatureState.SetLocalBuildPreviewEnabled(!RuntimeFeatureState.LocalBuildPreviewEnabled);
                        return;
                    case 1:
                        RuntimeFeatureState.SetLocalBuildPreviewTimeoutSeconds(RuntimeFeatureState.LocalBuildPreviewTimeoutSeconds + (0.25f * direction));
                        return;
                }

                index -= 2;
            }

            if (RuntimeFeatureState.AimHealthbarSupported)
            {
                if (index == 0)
                {
                    RuntimeFeatureState.SetAimHealthbarEnabled(!RuntimeFeatureState.AimHealthbarEnabled);
                    return;
                }

                index -= 1;
            }

            if (RuntimeFeatureState.DeathCamHealthbarSupported && index == 0)
            {
                RuntimeFeatureState.SetDeathCamHealthbarEnabled(!RuntimeFeatureState.DeathCamHealthbarEnabled);
                return;
            }

            if (RuntimeFeatureState.DeathCamHealthbarSupported)
            {
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCasualQueueSupported && index == 0)
            {
                RuntimeFeatureState.SetAutoCasualQueueEnabled(!RuntimeFeatureState.AutoCasualQueueEnabled);
                return;
            }

            if (RuntimeFeatureState.AutoCasualQueueSupported)
            {
                index -= 1;
            }

            if (RuntimeFeatureState.TeammateHpSupported && index == 0)
            {
                RuntimeFeatureState.SetTeammateHpEnabled(!RuntimeFeatureState.TeammateHpEnabled);
                return;
            }

            if (RuntimeFeatureState.TeammateHpSupported)
            {
                index -= 1;
            }

            if (RuntimeFeatureState.AutoCrouchDisableSupported && index == 0)
            {
                RuntimeFeatureState.SetAutoCrouchDisabled(!RuntimeFeatureState.AutoCrouchDisabled);
                return;
            }

            if (RuntimeFeatureState.AutoCrouchDisableSupported)
            {
                index -= 1;
            }

            if (DebugMenuRuntime.Enabled && index == 0)
            {
                DebugMenuRuntime.Execute("force_start_match");
                return;
            }
        }

        private static void CycleCrosshairShape(int direction)
        {
            int currentIndex = 0;
            for (int i = 0; i < CrosshairShapes.Length; i++)
            {
                if (string.Equals(CrosshairShapes[i], RuntimeFeatureState.CrosshairForceShape, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + direction + CrosshairShapes.Length) % CrosshairShapes.Length;
            RuntimeFeatureState.SetCrosshairForceShape(CrosshairShapes[nextIndex]);
        }

        private static void CycleTeamColor(bool friendly, int direction)
        {
            Color currentColor = friendly ? RuntimeFeatureState.TeamFriendlyColor : RuntimeFeatureState.TeamEnemyColor;
            string currentHex = FormatColor(currentColor);
            int currentIndex = 0;
            for (int i = 0; i < TeamColorPresets.Length; i++)
            {
                if (string.Equals(TeamColorPresets[i], currentHex, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + direction + TeamColorPresets.Length) % TeamColorPresets.Length;
            Color nextColor = ParseMenuColor(TeamColorPresets[nextIndex]);
            if (friendly)
            {
                RuntimeFeatureState.SetTeamFriendlyColor(nextColor);
            }
            else
            {
                RuntimeFeatureState.SetTeamEnemyColor(nextColor);
            }
        }

        private static string FormatColor(Color color)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        private static Color ParseMenuColor(string value)
        {
            string normalized = value.StartsWith("#") ? value.Substring(1) : value;
            int r = Convert.ToInt32(normalized.Substring(0, 2), 16);
            int g = Convert.ToInt32(normalized.Substring(2, 2), 16);
            int b = Convert.ToInt32(normalized.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }

    public static class DebugMenuRuntime
    {
        private static bool enabled;
        private static string debugMenuKeyName = "F9";
        private static string mainMenuKeyName = "F10";
        private static string lobbyMenuKeyName = "F11";
        private static string zoneMenuKeyName = "F12";
        private static DebugMenuBootstrapper instance;

        public static void Configure(bool supported, string debugMenuKey, string mainMenuKey, string lobbyMenuKey, string zoneMenuKey)
        {
            enabled = supported;
            debugMenuKeyName = string.IsNullOrEmpty(debugMenuKey) ? "F9" : debugMenuKey;
            mainMenuKeyName = string.IsNullOrEmpty(mainMenuKey) ? "F10" : mainMenuKey;
            lobbyMenuKeyName = string.IsNullOrEmpty(lobbyMenuKey) ? "F11" : lobbyMenuKey;
            zoneMenuKeyName = string.IsNullOrEmpty(zoneMenuKey) ? "F12" : zoneMenuKey;
        }

        public static void EnsureInstance()
        {
            if (!enabled)
            {
                return;
            }

            if (instance != null)
            {
                return;
            }

            GameObject go = GameObject.Find("BNL_DEBUG_MENU_BOOTSTRAP");
            if (go == null)
            {
                go = new GameObject("BNL_DEBUG_MENU_BOOTSTRAP");
            }

            instance = go.GetComponent<DebugMenuBootstrapper>();
            if (instance == null)
            {
                instance = go.AddComponent<DebugMenuBootstrapper>();
            }
        }

        public static KeyCode ParseKey(string keyName, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return fallback;
            }

            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
            }
            catch
            {
                return fallback;
            }
        }

        public static KeyCode DebugMenuKey
        {
            get { return ParseKey(debugMenuKeyName, KeyCode.F9); }
        }

        public static KeyCode MainMenuKey
        {
            get { return ParseKey(mainMenuKeyName, KeyCode.F10); }
        }

        public static KeyCode LobbyMenuKey
        {
            get { return ParseKey(lobbyMenuKeyName, KeyCode.F11); }
        }

        public static KeyCode ZoneMenuKey
        {
            get { return ParseKey(zoneMenuKeyName, KeyCode.F12); }
        }

        public static bool Enabled
        {
            get { return enabled; }
        }

        public static void Execute(string command)
        {
            if (!enabled || string.IsNullOrEmpty(command))
            {
                return;
            }

            try
            {
                NetworkDispatcher dispatcher = Singleton<NetworkDispatcher>.Instance;
                if (dispatcher != null && dispatcher.ServiceDebug != null)
                {
                    dispatcher.ServiceDebug.Execute(command);
                    UnityEngine.Debug.Log("BNL debug command sent: " + command);
                    return;
                }

                DebugLogic.Execute(command);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }

    public sealed class DebugMenuBootstrapper : MonoBehaviour
    {
        private bool initialized;
        private bool shownOnce;
        private DebugMenu debugMenuWindow;
        private DebugMainMenu debugMainMenuWindow;
        private DebugLobbyMenu debugLobbyMenuWindow;
        private DebugZoneMenu debugZoneMenuWindow;

        private void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!initialized)
            {
                initialized = TryInitialize();
            }

            if (!initialized)
            {
                return;
            }

            if (!shownOnce && debugMenuWindow != null)
            {
                debugMenuWindow.Show();
                shownOnce = true;
            }

            if (Input.GetKeyDown(DebugMenuRuntime.DebugMenuKey))
            {
                ToggleWindow(debugMenuWindow);
            }

            if (Input.GetKeyDown(DebugMenuRuntime.MainMenuKey))
            {
                ToggleWindow(debugMainMenuWindow);
            }

            if (Input.GetKeyDown(DebugMenuRuntime.LobbyMenuKey))
            {
                ToggleWindow(debugLobbyMenuWindow);
            }

            if (Input.GetKeyDown(DebugMenuRuntime.ZoneMenuKey))
            {
                ToggleWindow(debugZoneMenuWindow);
            }
        }

        private bool TryInitialize()
        {
            try
            {
                DebugManager manager = UnityEngine.Object.FindObjectOfType(typeof(DebugManager)) as DebugManager;
                if (manager == null)
                {
                    manager = gameObject.GetComponent<DebugManager>();
                }

                if (manager == null)
                {
                    manager = gameObject.AddComponent<DebugManager>();
                }

                manager.Enabled = true;

                DebugLogic debugLogic = UnityEngine.Object.FindObjectOfType(typeof(DebugLogic)) as DebugLogic;
                if (debugLogic == null)
                {
                    debugLogic = gameObject.GetComponent<DebugLogic>();
                }

                if (debugLogic == null)
                {
                    debugLogic = gameObject.AddComponent<DebugLogic>();
                }

                debugMenuWindow = EnsureWindow<DebugMenu>(manager, "Debug Menu", DebugMenuRuntime.DebugMenuKey, false);
                debugMainMenuWindow = EnsureWindow<DebugMainMenu>(manager, "Main Menu Debug", DebugMenuRuntime.MainMenuKey, true);
                debugLobbyMenuWindow = EnsureWindow<DebugLobbyMenu>(manager, "Lobby Debug", DebugMenuRuntime.LobbyMenuKey, true);
                debugZoneMenuWindow = EnsureWindow<DebugZoneMenu>(manager, "Zone Debug", DebugMenuRuntime.ZoneMenuKey, true);
                manager.SetActiveDebugWindows(true);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                return false;
            }
        }

        private static T EnsureWindow<T>(DebugManager manager, string title, KeyCode key, bool showInMenu) where T : DebugWindow
        {
            T window = UnityEngine.Object.FindObjectOfType(typeof(T)) as T;
            if (window == null)
            {
                window = manager.gameObject.GetComponent<T>();
            }

            if (window == null)
            {
                window = manager.gameObject.AddComponent<T>();
            }

            window.Title = title;
            window.Key = key;
            window.ShowInMenu = showInMenu;
            return window;
        }

        private static void ToggleWindow(DebugWindow window)
        {
            if (window == null)
            {
                return;
            }

            window.Toggle();
        }
    }
}
