using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class FeatureSettingsForm : Form
{
    private readonly FeatureSettingsService featureSettingsService;
    private readonly FeaturePresetsService presetsService;
    private readonly AppPaths appPaths;
    private readonly GameInstallInfo? gameInstallInfo;

    private readonly CheckBox crosshairEnabledCheckBox;
    private readonly TextBox crosshairIdleColorTextBox;
    private readonly TextBox crosshairFullDamageColorTextBox;
    private readonly TextBox crosshairBelowMaxColorTextBox;
    private readonly NumericUpDown crosshairBrightnessNumeric;
    private readonly NumericUpDown crosshairSizeNumeric;
    private readonly NumericUpDown crosshairSpreadNumeric;
    private readonly NumericUpDown crosshairAlphaNumeric;
    private readonly ComboBox crosshairShapeComboBox;
    private readonly CheckBox crosshairShowInAdsCheckBox;
    private readonly CheckBox crosshairHideCheckBox;

    private readonly CheckBox fovEnabledCheckBox;
    private readonly NumericUpDown fovNumeric;
    private readonly NumericUpDown adsSensitivityNumeric;
    private readonly NumericUpDown weaponModelFovNumeric;

    private readonly CheckBox teamColorsEnabledCheckBox;
    private readonly TextBox friendlyColorTextBox;
    private readonly TextBox enemyColorTextBox;

    private readonly CheckBox damageHealingEnabledCheckBox;
    private readonly TextBox damageColorTextBox;
    private readonly TextBox critDamageColorTextBox;
    private readonly TextBox healColorTextBox;
    private readonly NumericUpDown damageSizeNumeric;
    private readonly NumericUpDown healSizeNumeric;
    private readonly NumericUpDown damageAlphaNumeric;
    private readonly NumericUpDown minimumHealNumeric;
    private readonly CheckBox showFriendlyHealingCheckBox;
    private readonly CheckBox showSelfHealingCheckBox;
    private readonly CheckBox combineDamageCheckBox;
    private readonly CheckBox combineHealingCheckBox;
    private readonly NumericUpDown selfHealSizeNumeric;
    private readonly NumericUpDown selfHealXNumeric;
    private readonly NumericUpDown selfHealYNumeric;

    private readonly CheckBox healAlertEnabledCheckBox;
    private readonly TextBox healAlertDamageColorTextBox;
    private readonly TextBox healAlertHealColorTextBox;
    private readonly NumericUpDown healAlertDamageSizeNumeric;
    private readonly NumericUpDown healAlertHealSizeNumeric;
    private readonly NumericUpDown healAlertAlphaNumeric;
    private readonly NumericUpDown healAlertMinimumHealNumeric;
    private readonly CheckBox healDirectionCheckBox;

    private readonly CheckBox objectiveBeamEnabledCheckBox;
    private readonly CheckBox objectiveBeamHideCheckBox;

    private readonly CheckBox shieldEnabledCheckBox;
    private readonly TextBox shieldColorTextBox;
    private readonly NumericUpDown shieldSizeNumeric;
    private readonly NumericUpDown shieldOffsetXNumeric;
    private readonly NumericUpDown shieldOffsetYNumeric;
    private readonly ComboBox shieldDisplayModeComboBox;

    private readonly CheckBox localBuildPreviewEnabledCheckBox;

    private readonly CheckBox aimHealthbarEnabledCheckBox;

    private readonly CheckBox deathCamHealthbarEnabledCheckBox;

    private readonly CheckBox autoCasualQueueEnabledCheckBox;

    private readonly CheckBox teammateHpEnabledCheckBox;

    private readonly CheckBox autoCrouchEnabledCheckBox;
    private readonly CheckBox hideImpactVfxEnabledCheckBox;
    private readonly CheckBox hideImpactVfxCheckBox;
    private readonly CheckBox hideLavaWaterPlaneCheckBox;
    private readonly CheckBox hideFallingBlocksCheckBox;

    private readonly CheckBox unitGuiScaleEnabledCheckBox;
    private readonly NumericUpDown unitGuiScaleNumeric;

    private readonly CheckBox wsiScaleEnabledCheckBox;
    private readonly NumericUpDown wsiScaleNumeric;

    private readonly CheckBox mapRenderEnabledCheckBox;
    private readonly ComboBox mapRenderPresetComboBox;

    private readonly CheckBox friendlyLowHealthEnabledCheckBox;
    private readonly TextBox friendlyLowHealthColorTextBox;
    private readonly NumericUpDown friendlyLowHealthThresholdNumeric;
    private readonly CheckBox friendlyLowHealthIndicatorCheckBox;
    private readonly NumericUpDown friendlyLowHealthIndicatorSizeNumeric;
    private readonly NumericUpDown friendlyLowHealthIndicatorAlphaNumeric;

    private readonly CheckBox segmentedHealthbarEnabledCheckBox;

    private readonly CheckBox fontOverrideEnabledCheckBox;

    private readonly CheckBox skipIntroCheckBox;
    private readonly CheckBox disableMainMenuFrameCapCheckBox;

    // Color field columns: each block is label+textbox(130)+gap(4)+button(44)+gap(4)+swatch(22) = 204px; 26px between columns
    private const int ColA = 14;
    private const int ColB = 244;
    private const int ColC = 474;

    // Numeric columns: step=180, width=150
    private const int NumC1 = 14;
    private const int NumC2 = 194;
    private const int NumC3 = 374;
    private const int NumC4 = 554;
    private const int NumW = 150;

    // Tab layout rows (shifted down 60px vs original to fit description + preset bar)
    private const int DescY       = 8;    // description label
    private const int PresetCtrlY = 41;   // preset bar controls
    private const int EnabledY    = 72;   // enabled checkbox
    private const int ColorRow1Y  = 106;  // first color-field label row
    private const int NumLabelY   = 170;  // numeric labels
    private const int NumCtrlY    = 188;  // numeric controls
    private const int ExtraLabelY = 242;  // extra label row (shape, display mode)
    private const int ExtraCtrlY  = 260;  // extra controls row
    private const int CheckRow1Y  = 238;  // first checkbox row (damage/healing, heal alerts)
    private const int CheckRow2Y  = 266;  // second checkbox row
    private const int SelfHealLblY = 300; // self-healing label row
    private const int SelfHealCtlY = 318; // self-healing control row

    public FeatureSettingsForm(AppPaths paths, GameInstallInfo? installInfo = null)
    {
        appPaths = paths;
        gameInstallInfo = installInfo;
        featureSettingsService = new FeatureSettingsService(paths);
        presetsService = new FeaturePresetsService(paths);

        if (installInfo?.IsDetected == true)
        {
            var runtimeSync = new RuntimeMenuSyncService();
            featureSettingsService.SetRuntimeConfigPath(runtimeSync.GetRuntimeConfigPath(installInfo.ManagedDirectoryPath));
        }

        Text = "Feature Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);

        using var iconStream = typeof(FeatureSettingsForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new Icon(iconStream);

        var infoLabel = new Label
        {
            Text = "Settings are written into the local patching config files and rebuilt/applied on next launch.",
            AutoSize = false,
            Size = new Size(720, 20),
            Location = new Point(20, 14)
        };

        var tabs = new TabControl
        {
            Location = new Point(20, 38),
            Size = new Size(720, 462)
        };

        // --- Crosshair ---
        var crosshairTab = new TabPage("Crosshair");
        AddDescription(crosshairTab,
            "Custom weapon reticle. Colors change by damage state: idle / at full damage range / beyond max range. " +
            "Size x scales the whole reticle, spread x controls tightness, brightness x boosts RGB, alpha controls opacity.");
        crosshairEnabledCheckBox        = NewCheckBox("Enabled", 14, EnabledY);
        crosshairIdleColorTextBox       = NewColorField(crosshairTab, "Idle color",         ColA, ColorRow1Y);
        crosshairFullDamageColorTextBox = NewColorField(crosshairTab, "Full damage color",  ColB, ColorRow1Y);
        crosshairBelowMaxColorTextBox   = NewColorField(crosshairTab, "Below max color",    ColC, ColorRow1Y);
        crosshairBrightnessNumeric = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.25M,  3, 2, 0.05M);
        crosshairSizeNumeric       = NewDecimalNumeric(NumC2, NumCtrlY, NumW, 0.1M,   5, 2, 0.05M);
        crosshairSpreadNumeric     = NewDecimalNumeric(NumC3, NumCtrlY, NumW, 0.1M,   5, 2, 0.05M);
        crosshairAlphaNumeric      = NewDecimalNumeric(NumC4, NumCtrlY, NumW, 0.1M,   1, 2, 0.05M);
        crosshairShapeComboBox     = NewComboBox(14, ExtraCtrlY, 210, "__DEFAULT__", "Dot", "Crosshair", "BrokenCircle", "Hashed", "HashedCrosshair", "Melee");
        crosshairShowInAdsCheckBox = NewCheckBox("Force show in ADS", 250, ExtraCtrlY + 2);
        crosshairHideCheckBox      = NewCheckBox("Hide crosshair",    440, ExtraCtrlY + 2);
        var crosshairHintLabel = new Label
        {
            Text = "Spread x: lower = tighter, higher = wider.  Alpha = opacity.  Brightness x boosts RGB intensity.  Size x scales the whole reticle.",
            AutoSize = false,
            Size = new Size(688, 32),
            Location = new Point(14, 298),
            ForeColor = SystemColors.GrayText
        };
        crosshairTab.Controls.AddRange([
            crosshairEnabledCheckBox,
            NewLabel("Brightness", NumC1, NumLabelY), crosshairBrightnessNumeric,
            NewLabel("Size",       NumC2, NumLabelY), crosshairSizeNumeric,
            NewLabel("Spread",     NumC3, NumLabelY), crosshairSpreadNumeric,
            NewLabel("Alpha",      NumC4, NumLabelY), crosshairAlphaNumeric,
            NewLabel("Forced shape", 14, ExtraLabelY), crosshairShapeComboBox,
            crosshairShowInAdsCheckBox,
            crosshairHideCheckBox,
            crosshairHintLabel
        ]);
        AddPresetBar<CrosshairSettings>(crosshairTab, "crosshair",
            readFields: () => new CrosshairSettings
            {
                Enabled = crosshairEnabledCheckBox.Checked,
                IdleColor = crosshairIdleColorTextBox.Text.Trim(),
                FullDamageColor = crosshairFullDamageColorTextBox.Text.Trim(),
                BelowMaxColor = crosshairBelowMaxColorTextBox.Text.Trim(),
                BrightnessMultiplier = (double)crosshairBrightnessNumeric.Value,
                SizeMultiplier = (double)crosshairSizeNumeric.Value,
                SpreadMultiplier = (double)crosshairSpreadNumeric.Value,
                Alpha = (double)crosshairAlphaNumeric.Value,
                ForceShape = crosshairShapeComboBox.Text.Trim(),
                ForceShowInAds = crosshairShowInAdsCheckBox.Checked,
                HideCrosshair = crosshairHideCheckBox.Checked
            },
            applyFields: s =>
            {
                crosshairEnabledCheckBox.Checked = s.Enabled;
                crosshairIdleColorTextBox.Text = s.IdleColor;
                crosshairFullDamageColorTextBox.Text = s.FullDamageColor;
                crosshairBelowMaxColorTextBox.Text = s.BelowMaxColor;
                crosshairBrightnessNumeric.Value = ToDecimal(s.BrightnessMultiplier, crosshairBrightnessNumeric);
                crosshairSizeNumeric.Value = ToDecimal(s.SizeMultiplier, crosshairSizeNumeric);
                crosshairSpreadNumeric.Value = ToDecimal(s.SpreadMultiplier, crosshairSpreadNumeric);
                crosshairAlphaNumeric.Value = ToDecimal(s.Alpha, crosshairAlphaNumeric);
                crosshairShapeComboBox.Text = s.ForceShape;
                crosshairShowInAdsCheckBox.Checked = s.ForceShowInAds;
                crosshairHideCheckBox.Checked = s.HideCrosshair;
            });

        // --- FOV ---
        var fovTab = new TabPage("FOV");
        AddDescription(fovTab,
            "Overrides the camera field of view and ADS sensitivity. Higher FOV gives a wider view at the cost of less zoom. " +
            "Weapon model FOV controls how large the gun appears in first-person.");
        fovEnabledCheckBox    = NewCheckBox("Enabled", 14, EnabledY);
        fovNumeric            = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 1,   180, 1, 1);
        adsSensitivityNumeric = NewDecimalNumeric(NumC2, NumCtrlY, NumW, 0,    10, 2, 0.05M);
        weaponModelFovNumeric = NewDecimalNumeric(NumC3, NumCtrlY, NumW, 0,   180, 1, 1);
        fovTab.Controls.AddRange([
            fovEnabledCheckBox,
            NewLabel("FOV",              NumC1, NumLabelY), fovNumeric,
            NewLabel("ADS sensitivity",  NumC2, NumLabelY), adsSensitivityNumeric,
            NewLabel("Weapon model FOV", NumC3, NumLabelY), weaponModelFovNumeric
        ]);
        AddPresetBar<FovSettings>(fovTab, "fov",
            readFields: () => new FovSettings
            {
                Enabled = fovEnabledCheckBox.Checked,
                Fov = (double)fovNumeric.Value,
                AdsSensitivityMultiplier = (double)adsSensitivityNumeric.Value,
                WeaponModelFov = (double)weaponModelFovNumeric.Value
            },
            applyFields: s =>
            {
                fovEnabledCheckBox.Checked = s.Enabled;
                fovNumeric.Value = ToDecimal(s.Fov, fovNumeric);
                adsSensitivityNumeric.Value = ToDecimal(s.AdsSensitivityMultiplier, adsSensitivityNumeric);
                weaponModelFovNumeric.Value = ToDecimal(s.WeaponModelFov, weaponModelFovNumeric);
            });

        // --- Team Colors ---
        var teamColorsTab = new TabPage("Team Colors");
        AddDescription(teamColorsTab,
            "Changes the color of friendly and enemy indicators — nameplates, health bars, hit effects. " +
            "Use the preset buttons for colors from past community seasons, or pick your own.");
        teamColorsEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        friendlyColorTextBox = NewColorField(teamColorsTab, "Friendly color", ColA, ColorRow1Y);
        enemyColorTextBox    = NewColorField(teamColorsTab, "Enemy color",    ColB, ColorRow1Y);
        var btnPresetBeta    = new Button { Text = "Beta",    Location = new Point(14,  173), Size = new Size(72, 23) };
        var btnPresetClassic = new Button { Text = "Classic", Location = new Point(90,  173), Size = new Size(72, 23) };
        var btnPresetDefault = new Button { Text = "Default", Location = new Point(166, 173), Size = new Size(72, 23) };
        btnPresetBeta.Click    += (_, _) => { teamColorsEnabledCheckBox.Checked = true; friendlyColorTextBox.Text = "#0BA187"; enemyColorTextBox.Text = "#9138A5"; };
        btnPresetClassic.Click += (_, _) => { teamColorsEnabledCheckBox.Checked = true; friendlyColorTextBox.Text = "#B4D0FB"; enemyColorTextBox.Text = "#D7373F"; };
        btnPresetDefault.Click += (_, _) => { teamColorsEnabledCheckBox.Checked = true; friendlyColorTextBox.Text = "#4AA3FF"; enemyColorTextBox.Text = "#FF5A5A"; };
        teamColorsTab.Controls.AddRange([
            teamColorsEnabledCheckBox,
            NewLabel("Season presets:", 14, 155),
            btnPresetBeta, btnPresetClassic, btnPresetDefault
        ]);
        AddPresetBar<TeamColorSettings>(teamColorsTab, "teamColors",
            readFields: () => new TeamColorSettings
            {
                Enabled = teamColorsEnabledCheckBox.Checked,
                FriendlyColor = friendlyColorTextBox.Text.Trim(),
                EnemyColor = enemyColorTextBox.Text.Trim()
            },
            applyFields: s =>
            {
                teamColorsEnabledCheckBox.Checked = s.Enabled;
                friendlyColorTextBox.Text = s.FriendlyColor;
                enemyColorTextBox.Text = s.EnemyColor;
            });

        // --- Damage/Healing ---
        var damageTab = new TabPage("Damage/Healing");
        AddDescription(damageTab,
            "Customizes floating damage and heal numbers in combat. Combine options merge rapid-fire hits into one number until " +
            "it fades. Minimum heal filters out heals below the set threshold.");
        damageHealingEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        damageColorTextBox     = NewColorField(damageTab, "Damage color",      ColA, ColorRow1Y);
        critDamageColorTextBox = NewColorField(damageTab, "Crit damage color", ColB, ColorRow1Y);
        healColorTextBox       = NewColorField(damageTab, "Heal color",        ColC, ColorRow1Y);
        damageSizeNumeric  = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.25M, 10,   2, 0.05M);
        healSizeNumeric    = NewDecimalNumeric(NumC2, NumCtrlY, NumW, 0.25M, 10,   2, 0.05M);
        damageAlphaNumeric = NewDecimalNumeric(NumC3, NumCtrlY, NumW, 0,      1,   2, 0.05M);
        minimumHealNumeric = NewDecimalNumeric(NumC4, NumCtrlY, NumW, 0,   1000,   0, 1M);
        showFriendlyHealingCheckBox = NewCheckBox("Show friendly healing", 14,  CheckRow1Y);
        showSelfHealingCheckBox     = NewCheckBox("Show self healing",    214,  CheckRow1Y);
        combineDamageCheckBox       = NewCheckBox("Combine damage",        14,  CheckRow2Y);
        combineHealingCheckBox      = NewCheckBox("Combine healing",      214,  CheckRow2Y);
        selfHealSizeNumeric = NewDecimalNumeric(NumC1, SelfHealCtlY, NumW, 0.1M,  5, 2, 0.05M);
        selfHealXNumeric    = NewDecimalNumeric(NumC2, SelfHealCtlY, NumW, -2000, 2000, 1, 1M);
        selfHealYNumeric    = NewDecimalNumeric(NumC3, SelfHealCtlY, NumW, -2000, 2000, 1, 1M);
        damageTab.Controls.AddRange([
            damageHealingEnabledCheckBox,
            NewLabel("Damage size",  NumC1, NumLabelY), damageSizeNumeric,
            NewLabel("Heal size",    NumC2, NumLabelY), healSizeNumeric,
            NewLabel("Alpha",        NumC3, NumLabelY), damageAlphaNumeric,
            NewLabel("Minimum heal", NumC4, NumLabelY), minimumHealNumeric,
            showFriendlyHealingCheckBox,
            showSelfHealingCheckBox,
            combineDamageCheckBox,
            combineHealingCheckBox,
            NewLabel("Self-heal size", NumC1, SelfHealLblY), selfHealSizeNumeric,
            NewLabel("Self-heal X",    NumC2, SelfHealLblY), selfHealXNumeric,
            NewLabel("Self-heal Y",    NumC3, SelfHealLblY), selfHealYNumeric
        ]);
        AddPresetBar<DamageHealingSettings>(damageTab, "damage",
            readFields: () => new DamageHealingSettings
            {
                Enabled = damageHealingEnabledCheckBox.Checked,
                DamageNumberColor = damageColorTextBox.Text.Trim(),
                CritDamageNumberColor = critDamageColorTextBox.Text.Trim(),
                HealNumberColor = healColorTextBox.Text.Trim(),
                DamageNumberSizeMultiplier = (double)damageSizeNumeric.Value,
                HealNumberSizeMultiplier = (double)healSizeNumeric.Value,
                Alpha = (double)damageAlphaNumeric.Value,
                MinimumHeal = (double)minimumHealNumeric.Value,
                ShowFriendlyHealing = showFriendlyHealingCheckBox.Checked,
                ShowSelfHealing = showSelfHealingCheckBox.Checked,
                CombineDamageUntilHidden = combineDamageCheckBox.Checked,
                CombineHealingUntilHidden = combineHealingCheckBox.Checked,
                SelfHealNumberSizeMultiplier = (double)selfHealSizeNumeric.Value,
                SelfHealX = (double)selfHealXNumeric.Value,
                SelfHealY = (double)selfHealYNumeric.Value
            },
            applyFields: s =>
            {
                damageHealingEnabledCheckBox.Checked = s.Enabled;
                damageColorTextBox.Text = s.DamageNumberColor;
                critDamageColorTextBox.Text = s.CritDamageNumberColor;
                healColorTextBox.Text = s.HealNumberColor;
                damageSizeNumeric.Value = ToDecimal(s.DamageNumberSizeMultiplier, damageSizeNumeric);
                healSizeNumeric.Value = ToDecimal(s.HealNumberSizeMultiplier, healSizeNumeric);
                damageAlphaNumeric.Value = ToDecimal(s.Alpha, damageAlphaNumeric);
                minimumHealNumeric.Value = ToDecimal(s.MinimumHeal, minimumHealNumeric);
                showFriendlyHealingCheckBox.Checked = s.ShowFriendlyHealing;
                showSelfHealingCheckBox.Checked = s.ShowSelfHealing;
                combineDamageCheckBox.Checked = s.CombineDamageUntilHidden;
                combineHealingCheckBox.Checked = s.CombineHealingUntilHidden;
                selfHealSizeNumeric.Value = ToDecimal(s.SelfHealNumberSizeMultiplier, selfHealSizeNumeric);
                selfHealXNumeric.Value = ToDecimal(s.SelfHealX, selfHealXNumeric);
                selfHealYNumeric.Value = ToDecimal(s.SelfHealY, selfHealYNumeric);
            });

        // --- Heal Alerts ---
        var healAlertTab = new TabPage("Heal Alerts");
        AddDescription(healAlertTab,
            "Shows a directional indicator when a heal lands — separate from the floating numbers and larger in scale. " +
            "Damage indicator color supports __DEFAULT__ to keep the game's built-in color.");
        healAlertEnabledCheckBox    = NewCheckBox("Enabled", 14, EnabledY);
        healAlertDamageColorTextBox = NewColorField(healAlertTab, "Damage indicator color", ColA, ColorRow1Y, allowDefaultToken: true);
        healAlertHealColorTextBox   = NewColorField(healAlertTab, "Heal indicator color",   ColB, ColorRow1Y);
        healAlertDamageSizeNumeric  = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.25M, 10,   2, 0.05M);
        healAlertHealSizeNumeric    = NewDecimalNumeric(NumC2, NumCtrlY, NumW, 0.25M, 10,   2, 0.05M);
        healAlertAlphaNumeric       = NewDecimalNumeric(NumC3, NumCtrlY, NumW, 0,      1,   2, 0.05M);
        healAlertMinimumHealNumeric = NewDecimalNumeric(NumC4, NumCtrlY, NumW, 0,   1000,   0, 1M);
        healDirectionCheckBox       = NewCheckBox("Show direction on heal", 14, CheckRow1Y);
        healAlertTab.Controls.AddRange([
            healAlertEnabledCheckBox,
            NewLabel("Damage size",  NumC1, NumLabelY), healAlertDamageSizeNumeric,
            NewLabel("Heal size",    NumC2, NumLabelY), healAlertHealSizeNumeric,
            NewLabel("Alpha",        NumC3, NumLabelY), healAlertAlphaNumeric,
            NewLabel("Minimum heal", NumC4, NumLabelY), healAlertMinimumHealNumeric,
            healDirectionCheckBox
        ]);
        AddPresetBar<HealAlertSettings>(healAlertTab, "healAlert",
            readFields: () => new HealAlertSettings
            {
                Enabled = healAlertEnabledCheckBox.Checked,
                DamageIndicatorColor = healAlertDamageColorTextBox.Text.Trim(),
                HealIndicatorColor = healAlertHealColorTextBox.Text.Trim(),
                DamageIndicatorSizeMultiplier = (double)healAlertDamageSizeNumeric.Value,
                HealIndicatorSizeMultiplier = (double)healAlertHealSizeNumeric.Value,
                Alpha = (double)healAlertAlphaNumeric.Value,
                MinimumHeal = (double)healAlertMinimumHealNumeric.Value,
                ShowDirectionOnHeal = healDirectionCheckBox.Checked
            },
            applyFields: s =>
            {
                healAlertEnabledCheckBox.Checked = s.Enabled;
                healAlertDamageColorTextBox.Text = s.DamageIndicatorColor;
                healAlertHealColorTextBox.Text = s.HealIndicatorColor;
                healAlertDamageSizeNumeric.Value = ToDecimal(s.DamageIndicatorSizeMultiplier, healAlertDamageSizeNumeric);
                healAlertHealSizeNumeric.Value = ToDecimal(s.HealIndicatorSizeMultiplier, healAlertHealSizeNumeric);
                healAlertAlphaNumeric.Value = ToDecimal(s.Alpha, healAlertAlphaNumeric);
                healAlertMinimumHealNumeric.Value = ToDecimal(s.MinimumHeal, healAlertMinimumHealNumeric);
                healDirectionCheckBox.Checked = s.ShowDirectionOnHeal;
            });

        // --- Objective Beam ---
        var beamTab = new TabPage("Objective Beam");
        AddDescription(beamTab,
            "Adds a tall vertical beam above the capture objective so it is visible from anywhere on the map. " +
            "No additional options — this is a simple enable/disable toggle.");
        objectiveBeamEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        objectiveBeamHideCheckBox = NewCheckBox("Hide Beam By Default", 14, CheckRow1Y);
        beamTab.Controls.Add(objectiveBeamEnabledCheckBox);
        beamTab.Controls.Add(objectiveBeamHideCheckBox);
        AddPresetBar<BaseObjectiveBeamSettings>(beamTab, "objectiveBeam",
            readFields: () => new BaseObjectiveBeamSettings { Enabled = objectiveBeamEnabledCheckBox.Checked, HideBeam = objectiveBeamHideCheckBox.Checked },
            applyFields: s => { objectiveBeamEnabledCheckBox.Checked = s.Enabled; objectiveBeamHideCheckBox.Checked = s.HideBeam; });

        // --- Shield Timer ---
        var shieldTab = new TabPage("Shield Timer");
        AddDescription(shieldTab,
            "Countdown timer for the shield buff bar. Circle mode draws a clock-style ring, numeric shows a number, off hides it. " +
            "Offsets move the element relative to its default screen position.");
        shieldEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        shieldColorTextBox    = NewColorField(shieldTab, "Timer color", ColA, ColorRow1Y);
        shieldSizeNumeric     = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.25M, 10,   2, 0.05M);
        shieldOffsetXNumeric  = NewDecimalNumeric(NumC2, NumCtrlY, NumW, -100, 100,   1, 1M);
        shieldOffsetYNumeric  = NewDecimalNumeric(NumC3, NumCtrlY, NumW, -100, 100,   1, 1M);
        shieldDisplayModeComboBox = NewComboBox(14, ExtraCtrlY, 180, "circle", "numeric", "off");
        shieldTab.Controls.AddRange([
            shieldEnabledCheckBox,
            NewLabel("Size multiplier", NumC1, NumLabelY), shieldSizeNumeric,
            NewLabel("Offset X",        NumC2, NumLabelY), shieldOffsetXNumeric,
            NewLabel("Offset Y",        NumC3, NumLabelY), shieldOffsetYNumeric,
            NewLabel("Display mode",    14,    ExtraLabelY), shieldDisplayModeComboBox
        ]);
        AddPresetBar<ShieldBuffBarSettings>(shieldTab, "shield",
            readFields: () => new ShieldBuffBarSettings
            {
                Enabled = shieldEnabledCheckBox.Checked,
                ShieldBuffBarColor = shieldColorTextBox.Text.Trim(),
                ShieldClockSizeMultiplier = (double)shieldSizeNumeric.Value,
                ShieldClockOffsetX = (double)shieldOffsetXNumeric.Value,
                ShieldClockOffsetY = (double)shieldOffsetYNumeric.Value,
                ShieldTimerDisplayMode = shieldDisplayModeComboBox.Text.Trim()
            },
            applyFields: s =>
            {
                shieldEnabledCheckBox.Checked = s.Enabled;
                shieldColorTextBox.Text = s.ShieldBuffBarColor;
                shieldSizeNumeric.Value = ToDecimal(s.ShieldClockSizeMultiplier, shieldSizeNumeric);
                shieldOffsetXNumeric.Value = ToDecimal(s.ShieldClockOffsetX, shieldOffsetXNumeric);
                shieldOffsetYNumeric.Value = ToDecimal(s.ShieldClockOffsetY, shieldOffsetYNumeric);
                shieldDisplayModeComboBox.Text = s.ShieldTimerDisplayMode;
            });

        // --- Local Build Preview ---
        var localBuildPreviewTab = new TabPage("Build Preview");
        AddDescription(localBuildPreviewTab,
            "Only enable if you have high ping. Blocks and devices appear on your screen immediately when placed, " +
            "without waiting for server confirmation — eliminating the visual delay caused by network latency. " +
            "The server remains authoritative: if it rejects a placement the local preview reverts. " +
            "Also reduces the delay felt when switching weapons and tools.");
        localBuildPreviewEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        localBuildPreviewTab.Controls.Add(localBuildPreviewEnabledCheckBox);
        AddPresetBar<LocalBuildPreviewSettings>(localBuildPreviewTab, "localBuildPreview",
            readFields: () => new LocalBuildPreviewSettings
            {
                Enabled = localBuildPreviewEnabledCheckBox.Checked
            },
            applyFields: s =>
            {
                localBuildPreviewEnabledCheckBox.Checked = s.Enabled;
            });

        // --- Aim Healthbar ---
        var aimHealthbarTab = new TabPage("Aim Healthbar");
        AddDescription(aimHealthbarTab,
            "Shows the enemy health bar whenever your crosshair is aimed at them, even if they haven't taken damage yet.");
        aimHealthbarEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        aimHealthbarTab.Controls.Add(aimHealthbarEnabledCheckBox);
        AddPresetBar<AimHealthbarSettings>(aimHealthbarTab, "aimHealthbar",
            readFields: () => new AimHealthbarSettings { Enabled = aimHealthbarEnabledCheckBox.Checked },
            applyFields: s => { aimHealthbarEnabledCheckBox.Checked = s.Enabled; });

        // --- Death Cam Healthbar ---
        var deathCamTab = new TabPage("Death Cam HP");
        AddDescription(deathCamTab,
            "Shows friendly health bars when spectating teammates during the death cam.");
        deathCamHealthbarEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        deathCamTab.Controls.Add(deathCamHealthbarEnabledCheckBox);
        AddPresetBar<DeathCamHealthbarSettings>(deathCamTab, "deathCamHealthbar",
            readFields: () => new DeathCamHealthbarSettings { Enabled = deathCamHealthbarEnabledCheckBox.Checked },
            applyFields: s => { deathCamHealthbarEnabledCheckBox.Checked = s.Enabled; });

        // --- Auto Casual Queue ---
        var autoCasualQueueTab = new TabPage("Auto Queue");
        AddDescription(autoCasualQueueTab,
            "Automatically joins the casual matchmaking queue as soon as you enter a custom game lobby, " +
            "and auto-accepts the match popup when a game is found — pulling you out of the custom game into the casual match.");
        autoCasualQueueEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        autoCasualQueueTab.Controls.Add(autoCasualQueueEnabledCheckBox);
        AddPresetBar<AutoCasualQueueSettings>(autoCasualQueueTab, "autoCasualQueue",
            readFields: () => new AutoCasualQueueSettings { Enabled = autoCasualQueueEnabledCheckBox.Checked },
            applyFields: s => { autoCasualQueueEnabledCheckBox.Checked = s.Enabled; });

        var friendlyLowHealthTab = new TabPage("Low HP Alert");
        AddDescription(friendlyLowHealthTab,
            "Changes the health bar and name color of friendly players when their health drops below a configurable threshold.");
        friendlyLowHealthEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        friendlyLowHealthColorTextBox    = NewColorField(friendlyLowHealthTab, "Alert color", ColA, ColorRow1Y);
        friendlyLowHealthThresholdNumeric = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.05M, 0.95M, 2, 0.05M);
        friendlyLowHealthIndicatorCheckBox = NewCheckBox("Show direction indicator when off-screen", 14, CheckRow1Y);
        friendlyLowHealthIndicatorSizeNumeric  = NewDecimalNumeric(NumC1, CheckRow2Y + 18, NumW, 0.1M, 5.0M, 2, 0.1M);
        friendlyLowHealthIndicatorAlphaNumeric = NewDecimalNumeric(NumC2, CheckRow2Y + 18, NumW, 0.1M, 1.0M, 2, 0.05M);
        friendlyLowHealthTab.Controls.AddRange([
            friendlyLowHealthEnabledCheckBox,
            NewLabel("Threshold (0–1)", NumC1, NumLabelY), friendlyLowHealthThresholdNumeric,
            friendlyLowHealthIndicatorCheckBox,
            NewLabel("Indicator size", NumC1, CheckRow2Y), friendlyLowHealthIndicatorSizeNumeric,
            NewLabel("Indicator alpha", NumC2, CheckRow2Y), friendlyLowHealthIndicatorAlphaNumeric
        ]);
        AddPresetBar<FriendlyLowHealthSettings>(friendlyLowHealthTab, "friendlyLowHealth",
            readFields: () => new FriendlyLowHealthSettings
            {
                Enabled                = friendlyLowHealthEnabledCheckBox.Checked,
                Color                  = friendlyLowHealthColorTextBox.Text.Trim(),
                Threshold              = (double)friendlyLowHealthThresholdNumeric.Value,
                ShowDirectionIndicator = friendlyLowHealthIndicatorCheckBox.Checked,
                IndicatorSize          = (double)friendlyLowHealthIndicatorSizeNumeric.Value,
                IndicatorAlpha         = (double)friendlyLowHealthIndicatorAlphaNumeric.Value
            },
            applyFields: s =>
            {
                friendlyLowHealthEnabledCheckBox.Checked        = s.Enabled;
                friendlyLowHealthColorTextBox.Text              = s.Color;
                friendlyLowHealthThresholdNumeric.Value         = ToDecimal(s.Threshold, friendlyLowHealthThresholdNumeric);
                friendlyLowHealthIndicatorCheckBox.Checked      = s.ShowDirectionIndicator;
                friendlyLowHealthIndicatorSizeNumeric.Value     = ToDecimal(s.IndicatorSize, friendlyLowHealthIndicatorSizeNumeric);
                friendlyLowHealthIndicatorAlphaNumeric.Value    = ToDecimal(s.IndicatorAlpha, friendlyLowHealthIndicatorAlphaNumeric);
            });

        // --- Teammate HP ---
        var teammateHpTab = new TabPage("Teammate HP");
        AddDescription(teammateHpTab,
            "Shows each teammate's current HP next to their name in the team panel while you are alive.");
        teammateHpEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        teammateHpTab.Controls.Add(teammateHpEnabledCheckBox);
        AddPresetBar<TeammateHpSettings>(teammateHpTab, "teammateHp",
            readFields: () => new TeammateHpSettings { Enabled = teammateHpEnabledCheckBox.Checked },
            applyFields: s => { teammateHpEnabledCheckBox.Checked = s.Enabled; });

        // --- Disable Auto-Crouch ---
        var autoCrouchTab = new TabPage("Auto-Crouch");
        AddDescription(autoCrouchTab,
            "Disables the forced-crouch behaviour that triggers when the ceiling is too low to stand. Useful for testing. The setting can also be toggled in-game via the runtime menu.");
        autoCrouchEnabledCheckBox = NewCheckBox("Disable auto-crouch", 14, EnabledY);
        autoCrouchTab.Controls.Add(autoCrouchEnabledCheckBox);
        AddPresetBar<AutoCrouchSettings>(autoCrouchTab, "autoCrouch",
            readFields: () => new AutoCrouchSettings { Enabled = autoCrouchEnabledCheckBox.Checked },
            applyFields: s => { autoCrouchEnabledCheckBox.Checked = s.Enabled; });

        // --- Hide Impact VFX ---
        var hideImpactVfxTab = new TabPage("Impact VFX");
        AddDescription(hideImpactVfxTab,
            "Suppress explosion and impact visual effects (bombs, rockets, grenades, cannons, etc.). " +
            "Only the visual particles are hidden — sound and damage still apply. " +
            "Lava/Water plane hides the visual surface of the map's lava or water floor (collision is kept).");
        hideImpactVfxEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        hideImpactVfxCheckBox = NewCheckBox("Hide impact/explosion VFX", 14, CheckRow1Y);
        hideLavaWaterPlaneCheckBox = NewCheckBox("Hide lava/water plane", 14, CheckRow2Y);
        hideFallingBlocksCheckBox = NewCheckBox("Hide falling blocks", 14, CheckRow2Y + 28);
        hideImpactVfxTab.Controls.AddRange([hideImpactVfxEnabledCheckBox, hideImpactVfxCheckBox, hideLavaWaterPlaneCheckBox, hideFallingBlocksCheckBox]);
        AddPresetBar<HideImpactVfxSettings>(hideImpactVfxTab, "hideImpactVfx",
            readFields: () => new HideImpactVfxSettings { Enabled = hideImpactVfxEnabledCheckBox.Checked, HideImpactVfx = hideImpactVfxCheckBox.Checked, HideLavaWaterPlane = hideLavaWaterPlaneCheckBox.Checked, HideFallingBlocks = hideFallingBlocksCheckBox.Checked },
            applyFields: s => { hideImpactVfxEnabledCheckBox.Checked = s.Enabled; hideImpactVfxCheckBox.Checked = s.HideImpactVfx; hideLavaWaterPlaneCheckBox.Checked = s.HideLavaWaterPlane; hideFallingBlocksCheckBox.Checked = s.HideFallingBlocks; });

        // --- Unit GUI Scale ---
        var unitGuiScaleTab = new TabPage("Unit GUI Scale");
        AddDescription(unitGuiScaleTab,
            "Scales the healthbar and name label shown above all units (friendlies and enemies). " +
            "Use values below 1.0 to shrink them for a cleaner view. Adjustable in-game via F8.");
        unitGuiScaleEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        unitGuiScaleNumeric = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.1M, 3.0M, 2, 0.05M);
        unitGuiScaleTab.Controls.AddRange([
            unitGuiScaleEnabledCheckBox,
            NewLabel("Scale multiplier", NumC1, NumLabelY), unitGuiScaleNumeric
        ]);
        AddPresetBar<UnitGuiScaleSettings>(unitGuiScaleTab, "unitGuiScale",
            readFields: () => new UnitGuiScaleSettings { Enabled = unitGuiScaleEnabledCheckBox.Checked, ScaleMultiplier = (double)unitGuiScaleNumeric.Value },
            applyFields: s => { unitGuiScaleEnabledCheckBox.Checked = s.Enabled; unitGuiScaleNumeric.Value = ToDecimal(s.ScaleMultiplier, unitGuiScaleNumeric); });

        // --- WSI Scale ---
        var wsiScaleTab = new TabPage("WSI Scale");
        AddDescription(wsiScaleTab,
            "Scales the world-space indicators (WSI) shown above units in the game world. " +
            "Adjustable in-game via F8.");
        wsiScaleEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        wsiScaleNumeric = NewDecimalNumeric(NumC1, NumCtrlY, NumW, 0.1M, 3.0M, 2, 0.05M);
        wsiScaleTab.Controls.AddRange([
            wsiScaleEnabledCheckBox,
            NewLabel("Scale multiplier", NumC1, NumLabelY), wsiScaleNumeric
        ]);
        AddPresetBar<WsiSettings>(wsiScaleTab, "wsiScale",
            readFields: () => new WsiSettings { ScaleEnabled = wsiScaleEnabledCheckBox.Checked, ScaleMultiplier = (double)wsiScaleNumeric.Value },
            applyFields: s => { wsiScaleEnabledCheckBox.Checked = s.ScaleEnabled; wsiScaleNumeric.Value = ToDecimal(s.ScaleMultiplier, wsiScaleNumeric); });

        // --- Map Render Override ---
        var mapRenderTab = new TabPage("Map Render");
        AddDescription(mapRenderTab,
            "Override the map's environmental lighting preset. " +
            "\"Default\" uses the map's own preset. Cycle through options in-game via F8.");
        mapRenderEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        mapRenderPresetComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(NumC1, NumCtrlY),
            Width = 160
        };
        mapRenderPresetComboBox.Items.AddRange(["Default", "DaytimeWarm", "DaytimeCold", "Sunset", "Night"]);
        mapRenderPresetComboBox.SelectedIndex = 0;
        mapRenderTab.Controls.AddRange([
            mapRenderEnabledCheckBox,
            NewLabel("Lighting preset", NumC1, NumLabelY), mapRenderPresetComboBox
        ]);
        AddPresetBar<MapRenderOverrideSettings>(mapRenderTab, "mapRender",
            readFields: () => new MapRenderOverrideSettings { Enabled = mapRenderEnabledCheckBox.Checked, Preset = mapRenderPresetComboBox.SelectedItem as string ?? "Default" },
            applyFields: s => { mapRenderEnabledCheckBox.Checked = s.Enabled; var i = mapRenderPresetComboBox.Items.IndexOf(s.Preset); mapRenderPresetComboBox.SelectedIndex = i >= 0 ? i : 0; });

        // --- Segmented Healthbar ---
        var segmentedHealthbarTab = new TabPage("Segmented HP");
        AddDescription(segmentedHealthbarTab,
            "Replaces the overhead health bars with the segmented style from the beta version of the game. " +
            "Requires the game to be installed — textures are copied into the game's CustomTextures folder on save.");
        segmentedHealthbarEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        segmentedHealthbarTab.Controls.Add(segmentedHealthbarEnabledCheckBox);
        AddPresetBar<SegmentedHealthbarSettings>(segmentedHealthbarTab, "segmentedHealthbar",
            readFields: () => new SegmentedHealthbarSettings { Enabled = segmentedHealthbarEnabledCheckBox.Checked },
            applyFields: s => { segmentedHealthbarEnabledCheckBox.Checked = s.Enabled; });

        // --- Font Override ---
        var fontOverrideTab = new TabPage("Font Override");
        AddDescription(fontOverrideTab,
            "Restores the stylized Edo SZ font (used in the original game) to kill feed messages and center-screen notices " +
            "(e.g. \"You've killed...\", \"Our cube is under attack!\"). Requires a feature bundle rebuild to take effect.");
        fontOverrideEnabledCheckBox = NewCheckBox("Enabled", 14, EnabledY);
        fontOverrideTab.Controls.Add(fontOverrideEnabledCheckBox);
        AddPresetBar<FontOverrideSettings>(fontOverrideTab, "fontOverride",
            readFields: () => new FontOverrideSettings { Enabled = fontOverrideEnabledCheckBox.Checked },
            applyFields: s => { fontOverrideEnabledCheckBox.Checked = s.Enabled; });

        // --- Misc ---
        var miscTab = new TabPage("Misc");
        AddDescription(miscTab,
            "Miscellaneous gameplay tweaks. Skip intro bypasses the warning screen and video that play each time the game starts.");
        skipIntroCheckBox = NewCheckBox("Skip intro screen on game start", 14, EnabledY);
        disableMainMenuFrameCapCheckBox = NewCheckBox("Disable frame cap on main menu (uncaps FPS while on the main menu screen)", 14, EnabledY + 30);
        miscTab.Controls.AddRange([skipIntroCheckBox, disableMainMenuFrameCapCheckBox]);

        tabs.TabPages.AddRange([crosshairTab, fovTab, teamColorsTab, damageTab, healAlertTab, beamTab, shieldTab, localBuildPreviewTab, aimHealthbarTab, deathCamTab, autoCasualQueueTab, friendlyLowHealthTab, teammateHpTab, autoCrouchTab, hideImpactVfxTab, unitGuiScaleTab, wsiScaleTab, mapRenderTab, segmentedHealthbarTab, fontOverrideTab, miscTab]);

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(520, 512),
            Size = new Size(100, 32)
        };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(640, 512),
            Size = new Size(100, 32)
        };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange([infoLabel, tabs, saveButton, cancelButton]);
        LoadValues();
    }

    private static void AddDescription(TabPage tab, string text)
    {
        tab.Controls.Add(new Label
        {
            Text = text,
            AutoSize = false,
            Size = new Size(690, 30),
            Location = new Point(14, DescY),
            ForeColor = SystemColors.GrayText
        });
    }

    private void AddPresetBar<T>(TabPage tab, string featureKey, Func<T> readFields, Action<T> applyFields)
        where T : class, new()
    {
        var combo = new ComboBox
        {
            Location = new Point(72, PresetCtrlY),
            Size = new Size(200, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var applyBtn  = new Button { Text = "Apply",   Location = new Point(276, PresetCtrlY), Size = new Size(60, 23), Enabled = false };
        var saveAsBtn = new Button { Text = "Save As", Location = new Point(340, PresetCtrlY), Size = new Size(65, 23) };
        var deleteBtn = new Button { Text = "Delete",  Location = new Point(409, PresetCtrlY), Size = new Size(60, 23), Enabled = false };

        void RefreshCombo()
        {
            var selected = combo.SelectedItem?.ToString();
            combo.Items.Clear();
            foreach (var name in presetsService.Load<T>(featureKey).Keys)
                combo.Items.Add(name);
            if (selected != null && combo.Items.Contains(selected))
                combo.SelectedItem = selected;
        }

        combo.SelectedIndexChanged += (_, _) =>
        {
            applyBtn.Enabled  = combo.SelectedIndex >= 0;
            deleteBtn.Enabled = combo.SelectedIndex >= 0;
        };

        applyBtn.Click += (_, _) =>
        {
            if (combo.SelectedItem is not string name) return;
            var presets = presetsService.Load<T>(featureKey);
            if (presets.TryGetValue(name, out var settings))
                applyFields(settings);
        };

        saveAsBtn.Click += (_, _) =>
        {
            var name = PromptText("Save Preset", "Enter a name for this preset:", combo.SelectedItem?.ToString() ?? "");
            if (string.IsNullOrWhiteSpace(name)) return;
            presetsService.Save(featureKey, name, readFields());
            RefreshCombo();
            combo.SelectedItem = name;
        };

        deleteBtn.Click += (_, _) =>
        {
            if (combo.SelectedItem is not string name) return;
            if (MessageBox.Show($"Delete preset \"{name}\"?", "Feature Settings",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            presetsService.Delete(featureKey, name);
            RefreshCombo();
        };

        tab.Controls.AddRange([
            new Label { Text = "Presets:", AutoSize = true, Location = new Point(14, PresetCtrlY + 4) },
            combo, applyBtn, saveAsBtn, deleteBtn
        ]);

        RefreshCombo();
    }

    private string? PromptText(string title, string prompt, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(360, 118)
        };
        var label   = new Label   { Text = prompt,        AutoSize = true,              Location = new Point(12, 12) };
        var textBox = new TextBox { Text = defaultValue,  Size = new Size(336, 23),     Location = new Point(12, 36) };
        var ok      = new Button  { Text = "OK",          DialogResult = DialogResult.OK,     Location = new Point(196, 76), Size = new Size(75, 26) };
        var cancel  = new Button  { Text = "Cancel",      DialogResult = DialogResult.Cancel, Location = new Point(276, 76), Size = new Size(75, 26) };
        form.Controls.AddRange([label, textBox, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : null;
    }

    // Creates label + textbox + picker button + color swatch as a unit inside parent.
    // textbox is at (x, y+18); button at (x+134, y+18); swatch at (x+182, y+18).
    // Total block width = 204px.
    private TextBox NewColorField(Control parent, string labelText, int x, int y, bool allowDefaultToken = false)
    {
        parent.Controls.Add(new Label { Text = labelText, AutoSize = true, Location = new Point(x, y) });

        var textBox = new TextBox
        {
            Location = new Point(x, y + 18),
            Size = new Size(130, 23)
        };

        var swatch = new Panel
        {
            Location = new Point(x + 182, y + 18),
            Size = new Size(22, 23),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Control
        };

        var button = new Button
        {
            Text = "...",
            Location = new Point(x + 134, y + 18),
            Size = new Size(44, 23)
        };

        void UpdateSwatch()
        {
            swatch.BackColor = TryParseColor(textBox.Text, allowDefaultToken, out var c)
                ? c
                : SystemColors.Control;
        }

        textBox.TextChanged += (_, _) => UpdateSwatch();

        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { FullOpen = true };
            if (TryParseColor(textBox.Text, allowDefaultToken, out var current))
                dialog.Color = current;
            if (dialog.ShowDialog(parent.FindForm()) == DialogResult.OK)
                textBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        };

        parent.Controls.AddRange([textBox, button, swatch]);
        return textBox;
    }

    private void LoadValues()
    {
        var crosshair = featureSettingsService.LoadCrosshairSettings();
        crosshairEnabledCheckBox.Checked = crosshair.Enabled;
        crosshairIdleColorTextBox.Text = crosshair.IdleColor;
        crosshairFullDamageColorTextBox.Text = crosshair.FullDamageColor;
        crosshairBelowMaxColorTextBox.Text = crosshair.BelowMaxColor;
        crosshairBrightnessNumeric.Value = ToDecimal(crosshair.BrightnessMultiplier, crosshairBrightnessNumeric);
        crosshairSizeNumeric.Value = ToDecimal(crosshair.SizeMultiplier, crosshairSizeNumeric);
        crosshairSpreadNumeric.Value = ToDecimal(crosshair.SpreadMultiplier, crosshairSpreadNumeric);
        crosshairAlphaNumeric.Value = ToDecimal(crosshair.Alpha, crosshairAlphaNumeric);
        crosshairShapeComboBox.Text = crosshair.ForceShape;
        crosshairShowInAdsCheckBox.Checked = crosshair.ForceShowInAds;
        crosshairHideCheckBox.Checked = crosshair.HideCrosshair;

        var fov = featureSettingsService.LoadFovSettings();
        fovEnabledCheckBox.Checked = fov.Enabled;
        fovNumeric.Value = ToDecimal(fov.Fov, fovNumeric);
        adsSensitivityNumeric.Value = ToDecimal(fov.AdsSensitivityMultiplier, adsSensitivityNumeric);
        weaponModelFovNumeric.Value = ToDecimal(fov.WeaponModelFov, weaponModelFovNumeric);

        var team = featureSettingsService.LoadTeamColorSettings();
        teamColorsEnabledCheckBox.Checked = team.Enabled;
        friendlyColorTextBox.Text = team.FriendlyColor;
        enemyColorTextBox.Text = team.EnemyColor;

        var damage = featureSettingsService.LoadDamageHealingSettings();
        damageHealingEnabledCheckBox.Checked = damage.Enabled;
        damageColorTextBox.Text = damage.DamageNumberColor;
        critDamageColorTextBox.Text = damage.CritDamageNumberColor;
        healColorTextBox.Text = damage.HealNumberColor;
        damageSizeNumeric.Value = ToDecimal(damage.DamageNumberSizeMultiplier, damageSizeNumeric);
        healSizeNumeric.Value = ToDecimal(damage.HealNumberSizeMultiplier, healSizeNumeric);
        damageAlphaNumeric.Value = ToDecimal(damage.Alpha, damageAlphaNumeric);
        minimumHealNumeric.Value = ToDecimal(damage.MinimumHeal, minimumHealNumeric);
        showFriendlyHealingCheckBox.Checked = damage.ShowFriendlyHealing;
        showSelfHealingCheckBox.Checked = damage.ShowSelfHealing;
        combineDamageCheckBox.Checked = damage.CombineDamageUntilHidden;
        combineHealingCheckBox.Checked = damage.CombineHealingUntilHidden;
        selfHealSizeNumeric.Value = ToDecimal(damage.SelfHealNumberSizeMultiplier, selfHealSizeNumeric);
        selfHealXNumeric.Value = ToDecimal(damage.SelfHealX, selfHealXNumeric);
        selfHealYNumeric.Value = ToDecimal(damage.SelfHealY, selfHealYNumeric);

        var healAlert = featureSettingsService.LoadHealAlertSettings();
        healAlertEnabledCheckBox.Checked = healAlert.Enabled;
        healAlertDamageColorTextBox.Text = healAlert.DamageIndicatorColor;
        healAlertHealColorTextBox.Text = healAlert.HealIndicatorColor;
        healAlertDamageSizeNumeric.Value = ToDecimal(healAlert.DamageIndicatorSizeMultiplier, healAlertDamageSizeNumeric);
        healAlertHealSizeNumeric.Value = ToDecimal(healAlert.HealIndicatorSizeMultiplier, healAlertHealSizeNumeric);
        healAlertAlphaNumeric.Value = ToDecimal(healAlert.Alpha, healAlertAlphaNumeric);
        healAlertMinimumHealNumeric.Value = ToDecimal(healAlert.MinimumHeal, healAlertMinimumHealNumeric);
        healDirectionCheckBox.Checked = healAlert.ShowDirectionOnHeal;

        var beam = featureSettingsService.LoadBaseObjectiveBeamSettings();
        objectiveBeamEnabledCheckBox.Checked = beam.Enabled;
        objectiveBeamHideCheckBox.Checked = beam.HideBeam;

        var shield = featureSettingsService.LoadShieldBuffBarSettings();
        shieldEnabledCheckBox.Checked = shield.Enabled;
        shieldColorTextBox.Text = shield.ShieldBuffBarColor;
        shieldSizeNumeric.Value = ToDecimal(shield.ShieldClockSizeMultiplier, shieldSizeNumeric);
        shieldOffsetXNumeric.Value = ToDecimal(shield.ShieldClockOffsetX, shieldOffsetXNumeric);
        shieldOffsetYNumeric.Value = ToDecimal(shield.ShieldClockOffsetY, shieldOffsetYNumeric);
        shieldDisplayModeComboBox.Text = shield.ShieldTimerDisplayMode;

        var localBuildPreview = featureSettingsService.LoadLocalBuildPreviewSettings();
        localBuildPreviewEnabledCheckBox.Checked = localBuildPreview.Enabled;

        var aimHealthbar = featureSettingsService.LoadAimHealthbarSettings();
        aimHealthbarEnabledCheckBox.Checked = aimHealthbar.Enabled;

        var deathCamHealthbar = featureSettingsService.LoadDeathCamHealthbarSettings();
        deathCamHealthbarEnabledCheckBox.Checked = deathCamHealthbar.Enabled;

        var autoCasualQueue = featureSettingsService.LoadAutoCasualQueueSettings();
        autoCasualQueueEnabledCheckBox.Checked = autoCasualQueue.Enabled;

        var friendlyLowHealth = featureSettingsService.LoadFriendlyLowHealthSettings();
        friendlyLowHealthEnabledCheckBox.Checked = friendlyLowHealth.Enabled;
        friendlyLowHealthColorTextBox.Text = friendlyLowHealth.Color;
        friendlyLowHealthThresholdNumeric.Value = ToDecimal(friendlyLowHealth.Threshold, friendlyLowHealthThresholdNumeric);
        friendlyLowHealthIndicatorCheckBox.Checked      = friendlyLowHealth.ShowDirectionIndicator;
        friendlyLowHealthIndicatorSizeNumeric.Value     = ToDecimal(friendlyLowHealth.IndicatorSize, friendlyLowHealthIndicatorSizeNumeric);
        friendlyLowHealthIndicatorAlphaNumeric.Value    = ToDecimal(friendlyLowHealth.IndicatorAlpha, friendlyLowHealthIndicatorAlphaNumeric);

        var teammateHp = featureSettingsService.LoadTeammateHpSettings();
        teammateHpEnabledCheckBox.Checked = teammateHp.Enabled;

        var autoCrouch = featureSettingsService.LoadAutoCrouchSettings();
        autoCrouchEnabledCheckBox.Checked = autoCrouch.Enabled;

        var hideImpactVfx = featureSettingsService.LoadHideImpactVfxSettings();
        hideImpactVfxEnabledCheckBox.Checked = hideImpactVfx.Enabled;
        hideImpactVfxCheckBox.Checked = hideImpactVfx.HideImpactVfx;
        hideLavaWaterPlaneCheckBox.Checked = hideImpactVfx.HideLavaWaterPlane;
        hideFallingBlocksCheckBox.Checked = hideImpactVfx.HideFallingBlocks;

        var unitGuiScale = featureSettingsService.LoadUnitGuiScaleSettings();
        unitGuiScaleEnabledCheckBox.Checked = unitGuiScale.Enabled;
        unitGuiScaleNumeric.Value = ToDecimal(unitGuiScale.ScaleMultiplier, unitGuiScaleNumeric);

        var wsiScale = featureSettingsService.LoadWsiSettings();
        wsiScaleEnabledCheckBox.Checked = wsiScale.ScaleEnabled;
        wsiScaleNumeric.Value = ToDecimal(wsiScale.ScaleMultiplier, wsiScaleNumeric);

        var mapRender = featureSettingsService.LoadMapRenderOverrideSettings();
        mapRenderEnabledCheckBox.Checked = mapRender.Enabled;
        var mapRenderIdx = mapRenderPresetComboBox.Items.IndexOf(mapRender.Preset);
        mapRenderPresetComboBox.SelectedIndex = mapRenderIdx >= 0 ? mapRenderIdx : 0;

        var segmentedHealthbar = featureSettingsService.LoadSegmentedHealthbarSettings();
        segmentedHealthbarEnabledCheckBox.Checked = segmentedHealthbar.Enabled;

        var fontOverride = featureSettingsService.LoadFontOverrideSettings();
        fontOverrideEnabledCheckBox.Checked = fontOverride.Enabled;

        skipIntroCheckBox.Checked = LoadDebugMenuBool("skip_intro");
        disableMainMenuFrameCapCheckBox.Checked = LoadDebugMenuBool("disable_main_menu_frame_cap");
    }

    private void ApplySegmentedHealthbarTextures(bool enabled)
    {
        var mappingPath = Path.Combine(appPaths.PatchingDir, "texture-replacements.txt");
        const string fillEntry = "white_rect=white_rect.png";
        const string bgEntry   = "white_rect_bg=white_rect_bg.png";

        var lines = File.Exists(mappingPath)
            ? File.ReadAllLines(mappingPath, System.Text.Encoding.UTF8).ToList()
            : new List<string>();

        if (enabled)
        {
            // Extract textures to CustomTextures folder
            var customTexturesDir = gameInstallInfo?.IsDetected == true
                ? gameInstallInfo.CustomTexturesDirectoryPath
                : null;

            if (customTexturesDir != null)
            {
                Directory.CreateDirectory(customTexturesDir);
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (var (resName, fileName) in new[]
                {
                    ("Patching.white_rect.png",    "white_rect.png"),
                    ("Patching.white_rect_bg.png", "white_rect_bg.png")
                })
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null) continue;
                    using var dest = File.Create(Path.Combine(customTexturesDir, fileName));
                    stream.CopyTo(dest);
                }
            }

            // Add entries to texture-replacements.txt if not present
            if (!lines.Any(l => l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase)))
                lines.Add(fillEntry);
            if (!lines.Any(l => l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase)))
                lines.Add(bgEntry);
        }
        else
        {
            lines.RemoveAll(l =>
                l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase));
        }

        Directory.CreateDirectory(appPaths.PatchingDir);
        File.WriteAllLines(mappingPath, lines, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private bool LoadDebugMenuBool(string key)
    {
        try
        {
            var path = Path.Combine(appPaths.PatchingDir, "experimental-debug-menu-config.json");
            if (!File.Exists(path)) return false;
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
            return doc.RootElement.TryGetProperty(key, out var el) && el.GetBoolean();
        }
        catch { return false; }
    }

    private void SaveDebugMenuBool(string key, bool value)
    {
        try
        {
            var path = Path.Combine(appPaths.PatchingDir, "experimental-debug-menu-config.json");
            var existing = new Dictionary<string, System.Text.Json.JsonElement>();
            if (File.Exists(path))
            {
                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    existing[prop.Name] = prop.Value.Clone();
            }
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            foreach (var kvp in existing)
            {
                if (kvp.Key == key) continue;
                writer.WritePropertyName(kvp.Key);
                kvp.Value.WriteTo(writer);
            }
            writer.WriteBoolean(key, value);
            writer.WriteEndObject();
            writer.Flush();
            File.WriteAllBytes(path, ms.ToArray());
        }
        catch { }
    }

    private void SaveAndClose()
    {
        var validationErrors = new List<string>();
        CollectColorValidation(validationErrors, crosshairIdleColorTextBox.Text);
        CollectColorValidation(validationErrors, crosshairFullDamageColorTextBox.Text);
        CollectColorValidation(validationErrors, crosshairBelowMaxColorTextBox.Text);
        CollectColorValidation(validationErrors, friendlyColorTextBox.Text);
        CollectColorValidation(validationErrors, enemyColorTextBox.Text);
        CollectColorValidation(validationErrors, damageColorTextBox.Text);
        CollectColorValidation(validationErrors, critDamageColorTextBox.Text);
        CollectColorValidation(validationErrors, healColorTextBox.Text);
        CollectColorValidation(validationErrors, healAlertDamageColorTextBox.Text, allowDefaultToken: true);
        CollectColorValidation(validationErrors, healAlertHealColorTextBox.Text);
        CollectColorValidation(validationErrors, shieldColorTextBox.Text);
        CollectColorValidation(validationErrors, friendlyLowHealthColorTextBox.Text);

        if (validationErrors.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, validationErrors),
                "Feature Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        featureSettingsService.SaveCrosshairSettings(new CrosshairSettings
        {
            Enabled = crosshairEnabledCheckBox.Checked,
            IdleColor = crosshairIdleColorTextBox.Text.Trim(),
            FullDamageColor = crosshairFullDamageColorTextBox.Text.Trim(),
            BelowMaxColor = crosshairBelowMaxColorTextBox.Text.Trim(),
            BrightnessMultiplier = (double)crosshairBrightnessNumeric.Value,
            SizeMultiplier = (double)crosshairSizeNumeric.Value,
            SpreadMultiplier = (double)crosshairSpreadNumeric.Value,
            Alpha = (double)crosshairAlphaNumeric.Value,
            ForceShape = crosshairShapeComboBox.Text.Trim(),
            ForceShowInAds = crosshairShowInAdsCheckBox.Checked,
            HideCrosshair = crosshairHideCheckBox.Checked
        });

        featureSettingsService.SaveFovSettings(new FovSettings
        {
            Enabled = fovEnabledCheckBox.Checked,
            Fov = (double)fovNumeric.Value,
            AdsSensitivityMultiplier = (double)adsSensitivityNumeric.Value,
            WeaponModelFov = (double)weaponModelFovNumeric.Value
        });

        featureSettingsService.SaveTeamColorSettings(new TeamColorSettings
        {
            Enabled = teamColorsEnabledCheckBox.Checked,
            FriendlyColor = friendlyColorTextBox.Text.Trim(),
            EnemyColor = enemyColorTextBox.Text.Trim()
        });

        featureSettingsService.SaveDamageHealingSettings(new DamageHealingSettings
        {
            Enabled = damageHealingEnabledCheckBox.Checked,
            DamageNumberColor = damageColorTextBox.Text.Trim(),
            CritDamageNumberColor = critDamageColorTextBox.Text.Trim(),
            HealNumberColor = healColorTextBox.Text.Trim(),
            DamageNumberSizeMultiplier = (double)damageSizeNumeric.Value,
            HealNumberSizeMultiplier = (double)healSizeNumeric.Value,
            Alpha = (double)damageAlphaNumeric.Value,
            MinimumHeal = (double)minimumHealNumeric.Value,
            ShowFriendlyHealing = showFriendlyHealingCheckBox.Checked,
            ShowSelfHealing = showSelfHealingCheckBox.Checked,
            CombineDamageUntilHidden = combineDamageCheckBox.Checked,
            CombineHealingUntilHidden = combineHealingCheckBox.Checked,
            SelfHealNumberSizeMultiplier = (double)selfHealSizeNumeric.Value,
            SelfHealX = (double)selfHealXNumeric.Value,
            SelfHealY = (double)selfHealYNumeric.Value
        });

        featureSettingsService.SaveHealAlertSettings(new HealAlertSettings
        {
            Enabled = healAlertEnabledCheckBox.Checked,
            DamageIndicatorColor = healAlertDamageColorTextBox.Text.Trim(),
            HealIndicatorColor = healAlertHealColorTextBox.Text.Trim(),
            DamageIndicatorSizeMultiplier = (double)healAlertDamageSizeNumeric.Value,
            HealIndicatorSizeMultiplier = (double)healAlertHealSizeNumeric.Value,
            Alpha = (double)healAlertAlphaNumeric.Value,
            MinimumHeal = (double)healAlertMinimumHealNumeric.Value,
            ShowDirectionOnHeal = healDirectionCheckBox.Checked
        });

        featureSettingsService.SaveBaseObjectiveBeamSettings(new BaseObjectiveBeamSettings
        {
            Enabled = objectiveBeamEnabledCheckBox.Checked,
            HideBeam = objectiveBeamHideCheckBox.Checked
        });

        featureSettingsService.SaveShieldBuffBarSettings(new ShieldBuffBarSettings
        {
            Enabled = shieldEnabledCheckBox.Checked,
            ShieldBuffBarColor = shieldColorTextBox.Text.Trim(),
            ShieldClockSizeMultiplier = (double)shieldSizeNumeric.Value,
            ShieldClockOffsetX = (double)shieldOffsetXNumeric.Value,
            ShieldClockOffsetY = (double)shieldOffsetYNumeric.Value,
            ShieldTimerDisplayMode = shieldDisplayModeComboBox.Text.Trim()
        });

        featureSettingsService.SaveLocalBuildPreviewSettings(new LocalBuildPreviewSettings
        {
            Enabled = localBuildPreviewEnabledCheckBox.Checked
        });

        featureSettingsService.SaveAimHealthbarSettings(new AimHealthbarSettings
        {
            Enabled = aimHealthbarEnabledCheckBox.Checked
        });

        featureSettingsService.SaveDeathCamHealthbarSettings(new DeathCamHealthbarSettings
        {
            Enabled = deathCamHealthbarEnabledCheckBox.Checked
        });

        featureSettingsService.SaveAutoCasualQueueSettings(new AutoCasualQueueSettings
        {
            Enabled = autoCasualQueueEnabledCheckBox.Checked
        });

        featureSettingsService.SaveFriendlyLowHealthSettings(new FriendlyLowHealthSettings
        {
            Enabled                = friendlyLowHealthEnabledCheckBox.Checked,
            Color                  = friendlyLowHealthColorTextBox.Text.Trim(),
            Threshold              = (double)friendlyLowHealthThresholdNumeric.Value,
            ShowDirectionIndicator = friendlyLowHealthIndicatorCheckBox.Checked,
            IndicatorSize          = (double)friendlyLowHealthIndicatorSizeNumeric.Value,
            IndicatorAlpha         = (double)friendlyLowHealthIndicatorAlphaNumeric.Value
        });

        featureSettingsService.SaveTeammateHpSettings(new TeammateHpSettings
        {
            Enabled = teammateHpEnabledCheckBox.Checked
        });

        featureSettingsService.SaveAutoCrouchSettings(new AutoCrouchSettings
        {
            Enabled = autoCrouchEnabledCheckBox.Checked
        });

        featureSettingsService.SaveHideImpactVfxSettings(new HideImpactVfxSettings
        {
            Enabled = hideImpactVfxEnabledCheckBox.Checked,
            HideImpactVfx = hideImpactVfxCheckBox.Checked,
            HideLavaWaterPlane = hideLavaWaterPlaneCheckBox.Checked,
            HideFallingBlocks = hideFallingBlocksCheckBox.Checked
        });

        featureSettingsService.SaveUnitGuiScaleSettings(new UnitGuiScaleSettings
        {
            Enabled = unitGuiScaleEnabledCheckBox.Checked,
            ScaleMultiplier = (double)unitGuiScaleNumeric.Value
        });

        featureSettingsService.SaveWsiSettings(new WsiSettings
        {
            ScaleEnabled = wsiScaleEnabledCheckBox.Checked,
            ScaleMultiplier = (double)wsiScaleNumeric.Value
        });

        featureSettingsService.SaveMapRenderOverrideSettings(new MapRenderOverrideSettings
        {
            Enabled = mapRenderEnabledCheckBox.Checked,
            Preset = mapRenderPresetComboBox.SelectedItem as string ?? "Default"
        });

        featureSettingsService.SaveSegmentedHealthbarSettings(new SegmentedHealthbarSettings
        {
            Enabled = segmentedHealthbarEnabledCheckBox.Checked
        });
        ApplySegmentedHealthbarTextures(segmentedHealthbarEnabledCheckBox.Checked);

        featureSettingsService.SaveFontOverrideSettings(new FontOverrideSettings
        {
            Enabled = fontOverrideEnabledCheckBox.Checked
        });

        SaveDebugMenuBool("skip_intro", skipIntroCheckBox.Checked);
        SaveDebugMenuBool("disable_main_menu_frame_cap", disableMainMenuFrameCapCheckBox.Checked);

        DialogResult = DialogResult.OK;
    }

    private static Label NewLabel(string text, int x, int y) =>
        new() { Text = text, AutoSize = true, Location = new Point(x, y) };

    private static CheckBox NewCheckBox(string text, int x, int y) =>
        new() { Text = text, AutoSize = true, Location = new Point(x, y) };

    private static ComboBox NewComboBox(int x, int y, int width, params string[] items)
    {
        var combo = new ComboBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        combo.Items.AddRange(items);
        return combo;
    }

    private static NumericUpDown NewDecimalNumeric(int x, int y, int width, decimal min, decimal max, int decimals, decimal increment) =>
        new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = increment
        };

    private static decimal ToDecimal(double value, NumericUpDown numeric)
    {
        var d = Convert.ToDecimal(value);
        if (d < numeric.Minimum) return numeric.Minimum;
        if (d > numeric.Maximum) return numeric.Maximum;
        return d;
    }


    private static bool ValidateColor(string value, out string error, bool allowDefaultToken = false)
    {
        var trimmed = value.Trim();
        if (allowDefaultToken && string.Equals(trimmed, "__DEFAULT__", StringComparison.OrdinalIgnoreCase))
        {
            error = string.Empty;
            return true;
        }

        if (Regex.IsMatch(trimmed, "^#[0-9A-Fa-f]{6}$"))
        {
            error = string.Empty;
            return true;
        }

        error = $"Invalid color value: {value}";
        return false;
    }

    private static void CollectColorValidation(List<string> errors, string value, bool allowDefaultToken = false)
    {
        if (!ValidateColor(value, out var error, allowDefaultToken) && !string.IsNullOrWhiteSpace(error))
            errors.Add(error);
    }

    private static bool TryParseColor(string value, bool allowDefaultToken, out Color color)
    {
        color = Color.White;
        var trimmed = value.Trim();
        if (allowDefaultToken && string.Equals(trimmed, "__DEFAULT__", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!Regex.IsMatch(trimmed, "^#[0-9A-Fa-f]{6}$"))
            return false;
        color = ColorTranslator.FromHtml(trimmed);
        return true;
    }
}
