using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;
using System.Reflection;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class FeatureSettingsViewModel : ViewModelBase
{
    private readonly AppPaths _paths;
    private readonly FeatureSettingsService _svc;
    private readonly FeaturePresetsService _presets;
    private readonly GameInstallInfo? _installInfo;

    public event Action<string, string>? ErrorOccurred;
    public event Action? Saved;

    // ── Crosshair ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _crosshairEnabled;
    [ObservableProperty] private string _crosshairIdleColor = "#FFFFFF";
    [ObservableProperty] private string _crosshairFullDamageColor = "#FF0000";
    [ObservableProperty] private string _crosshairBelowMaxColor = "#FFFF00";
    [ObservableProperty] private double _crosshairBrightness = 1.0;
    [ObservableProperty] private double _crosshairSize = 1.0;
    [ObservableProperty] private double _crosshairSpread = 1.0;
    [ObservableProperty] private double _crosshairLineThickness = 1.0;
    [ObservableProperty] private double _crosshairGap = 1.0;
    [ObservableProperty] private double _crosshairAlpha = 1.0;
    [ObservableProperty] private string _crosshairShape = "__DEFAULT__";
    [ObservableProperty] private bool _crosshairForceShowInAds;
    [ObservableProperty] private bool _crosshairHide;

    // ── FOV ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _fovEnabled;
    [ObservableProperty] private double _fov = 90;
    [ObservableProperty] private double _adsSensitivity = 1.0;
    [ObservableProperty] private double _weaponModelFov = 30;

    // ── Team Colors ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _teamColorsEnabled;
    [ObservableProperty] private string _friendlyColor = "#4AA3FF";
    [ObservableProperty] private string _enemyColor = "#FF5A5A";

    // ── Damage / Healing ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _damageHealingEnabled;
    [ObservableProperty] private string _damageColor = "#FFFFFF";
    [ObservableProperty] private string _critDamageColor = "#FFFFFF";
    [ObservableProperty] private string _healColor = "#91ED78";
    [ObservableProperty] private double _damageSize = 2.0;
    [ObservableProperty] private double _healSize = 2.0;
    [ObservableProperty] private double _selfHealSize = 2.0;
    [ObservableProperty] private double _damageAlpha = 1.0;
    [ObservableProperty] private double _minimumHeal;
    [ObservableProperty] private bool _showFriendlyHealing = true;
    [ObservableProperty] private bool _showSelfHealing = true;
    [ObservableProperty] private bool _combineDamage;
    [ObservableProperty] private bool _combineHealing;
    [ObservableProperty] private double _selfHealX;
    [ObservableProperty] private double _selfHealY = -550;

    // ── Heal Alerts ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _healAlertEnabled;
    [ObservableProperty] private string _healAlertDamageColor = "#FF6464";
    [ObservableProperty] private string _healAlertHealColor = "#64FF64";
    [ObservableProperty] private double _healAlertDamageSize = 1.0;
    [ObservableProperty] private double _healAlertHealSize = 1.0;
    [ObservableProperty] private double _healAlertAlpha = 1.0;
    [ObservableProperty] private double _healAlertMinimumHeal;
    [ObservableProperty] private bool _healAlertShowDirection;

    // ── Objective Beam ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _objectiveBeamEnabled;
    [ObservableProperty] private bool _objectiveBeamHide;

    // ── Shield Timer ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _shieldEnabled;
    [ObservableProperty] private string _shieldColor = "#FFF04A";
    [ObservableProperty] private double _shieldSize = 1.0;
    [ObservableProperty] private double _shieldOffsetX;
    [ObservableProperty] private double _shieldOffsetY;
    [ObservableProperty] private string _shieldDisplayMode = "circle";

    // ── Miscellaneous ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _localBuildPreviewEnabled;
    [ObservableProperty] private bool _aimHealthbarEnabled = true;
    [ObservableProperty] private bool _deathCamHealthbarEnabled = true;
    [ObservableProperty] private bool _autoCasualQueueEnabled;
    [ObservableProperty] private bool _teammateHpEnabled;
    [ObservableProperty] private bool _teammateHideNameBackground;
    [ObservableProperty] private bool _autoCrouchEnabled;
    [ObservableProperty] private bool _hideImpactVfxEnabled;
    [ObservableProperty] private bool _hideImpactVfx;
    [ObservableProperty] private bool _hideLavaWaterPlane;
    [ObservableProperty] private bool _hideFallingBlocks;
    [ObservableProperty] private double _unitGuiScale = 1.0;
    [ObservableProperty] private bool _unitGuiScaleEnabled;
    [ObservableProperty] private double _wsiScale = 1.0;
    [ObservableProperty] private bool _wsiScaleEnabled;
    [ObservableProperty] private bool _mapRenderEnabled;
    [ObservableProperty] private string _mapRenderPreset = "Default";
    [ObservableProperty] private bool _friendlyLowHealthEnabled = true;
    [ObservableProperty] private string _friendlyLowHealthColor = "#FF4444";
    [ObservableProperty] private double _friendlyLowHealthThreshold = 0.3;
    [ObservableProperty] private bool _friendlyLowHealthIndicator;
    [ObservableProperty] private double _friendlyLowHealthIndicatorSize = 1.0;
    [ObservableProperty] private double _friendlyLowHealthIndicatorAlpha = 1.0;
    [ObservableProperty] private bool _segmentedHealthbarEnabled;
    [ObservableProperty] private bool _fontOverrideEnabled;
    [ObservableProperty] private bool _performanceOptEnabled;

    public IReadOnlyList<string> CrosshairShapes { get; } =
        ["__DEFAULT__", "Dot", "Crosshair", "BrokenCircle", "Hashed", "HashedCrosshair", "Melee"];

    public IReadOnlyList<string> MapRenderPresets { get; } =
        ["Default", "DaytimeWarm", "DaytimeCold", "Sunset", "Night"];

    public IReadOnlyList<string> ShieldDisplayModes { get; } =
        ["circle", "numeric", "text"];

    public FeatureSettingsViewModel(AppPaths paths, GameInstallInfo? installInfo = null)
    {
        _paths = paths;
        _svc = new FeatureSettingsService(paths);
        _presets = new FeaturePresetsService(paths);
        _installInfo = installInfo;

        if (installInfo?.IsDetected == true)
        {
            var sync = new RuntimeMenuSyncService();
            _svc.SetRuntimeConfigPath(sync.GetRuntimeConfigPath(installInfo.ManagedDirectoryPath));
        }

        Load();
    }

    private void Load()
    {
        var ch = _svc.LoadCrosshairSettings();
        CrosshairEnabled = ch.Enabled; CrosshairIdleColor = ch.IdleColor; CrosshairFullDamageColor = ch.FullDamageColor;
        CrosshairBelowMaxColor = ch.BelowMaxColor; CrosshairBrightness = ch.BrightnessMultiplier;
        CrosshairSize = ch.SizeMultiplier; CrosshairSpread = ch.SpreadMultiplier; CrosshairLineThickness = ch.LineThicknessMultiplier;
        CrosshairGap = ch.GapMultiplier; CrosshairAlpha = ch.Alpha;
        CrosshairShape = ch.ForceShape; CrosshairForceShowInAds = ch.ForceShowInAds; CrosshairHide = ch.HideCrosshair;

        var fov = _svc.LoadFovSettings();
        FovEnabled = fov.Enabled; Fov = fov.Fov; AdsSensitivity = fov.AdsSensitivityMultiplier; WeaponModelFov = fov.WeaponModelFov;

        var tc = _svc.LoadTeamColorSettings();
        TeamColorsEnabled = tc.Enabled; FriendlyColor = tc.FriendlyColor; EnemyColor = tc.EnemyColor;

        var dh = _svc.LoadDamageHealingSettings();
        DamageHealingEnabled = dh.Enabled; DamageColor = dh.DamageNumberColor; CritDamageColor = dh.CritDamageNumberColor;
        HealColor = dh.HealNumberColor; DamageSize = dh.DamageNumberSizeMultiplier; HealSize = dh.HealNumberSizeMultiplier;
        SelfHealSize = dh.SelfHealNumberSizeMultiplier; DamageAlpha = dh.Alpha; MinimumHeal = dh.MinimumHeal;
        ShowFriendlyHealing = dh.ShowFriendlyHealing; ShowSelfHealing = dh.ShowSelfHealing;
        CombineDamage = dh.CombineDamageUntilHidden; CombineHealing = dh.CombineHealingUntilHidden;
        SelfHealX = dh.SelfHealX; SelfHealY = dh.SelfHealY;

        var ha = _svc.LoadHealAlertSettings();
        HealAlertEnabled = ha.Enabled; HealAlertDamageColor = ha.DamageIndicatorColor; HealAlertHealColor = ha.HealIndicatorColor;
        HealAlertDamageSize = ha.DamageIndicatorSizeMultiplier; HealAlertHealSize = ha.HealIndicatorSizeMultiplier;
        HealAlertAlpha = ha.Alpha; HealAlertMinimumHeal = ha.MinimumHeal; HealAlertShowDirection = ha.ShowDirectionOnHeal;

        var ob = _svc.LoadBaseObjectiveBeamSettings();
        ObjectiveBeamEnabled = ob.Enabled; ObjectiveBeamHide = ob.HideBeam;

        var sh = _svc.LoadShieldBuffBarSettings();
        ShieldEnabled = sh.Enabled; ShieldColor = sh.ShieldBuffBarColor; ShieldSize = sh.ShieldClockSizeMultiplier;
        ShieldOffsetX = sh.ShieldClockOffsetX; ShieldOffsetY = sh.ShieldClockOffsetY; ShieldDisplayMode = sh.ShieldTimerDisplayMode;

        LocalBuildPreviewEnabled = _svc.LoadLocalBuildPreviewSettings().Enabled;
        AimHealthbarEnabled = _svc.LoadAimHealthbarSettings().Enabled;
        DeathCamHealthbarEnabled = _svc.LoadDeathCamHealthbarSettings().Enabled;
        AutoCasualQueueEnabled = _svc.LoadAutoCasualQueueSettings().Enabled;
        var teammateHp = _svc.LoadTeammateHpSettings();
        TeammateHpEnabled = teammateHp.ShowHpText;
        TeammateHideNameBackground = teammateHp.HideNameBackground;
        AutoCrouchEnabled = _svc.LoadAutoCrouchSettings().Enabled;

        var vfx = _svc.LoadHideImpactVfxSettings();
        HideImpactVfxEnabled = vfx.Enabled; HideImpactVfx = vfx.HideImpactVfx;
        HideLavaWaterPlane = vfx.HideLavaWaterPlane; HideFallingBlocks = vfx.HideFallingBlocks;

        var ugs = _svc.LoadUnitGuiScaleSettings();
        UnitGuiScaleEnabled = ugs.Enabled; UnitGuiScale = ugs.ScaleMultiplier;

        var wsi = _svc.LoadWsiSettings();
        WsiScaleEnabled = wsi.ScaleEnabled; WsiScale = wsi.ScaleMultiplier;

        var mr = _svc.LoadMapRenderOverrideSettings();
        MapRenderEnabled = mr.Enabled; MapRenderPreset = mr.Preset;

        var flh = _svc.LoadFriendlyLowHealthSettings();
        FriendlyLowHealthEnabled = flh.Enabled; FriendlyLowHealthColor = flh.Color;
        FriendlyLowHealthThreshold = flh.Threshold; FriendlyLowHealthIndicator = flh.ShowDirectionIndicator;
        FriendlyLowHealthIndicatorSize = flh.IndicatorSize; FriendlyLowHealthIndicatorAlpha = flh.IndicatorAlpha;

        SegmentedHealthbarEnabled = _svc.LoadSegmentedHealthbarSettings().Enabled;
        FontOverrideEnabled = _svc.LoadFontOverrideSettings().Enabled;
        PerformanceOptEnabled = _svc.LoadPerformanceOptSettings().Enabled;

    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _svc.SaveCrosshairSettings(new CrosshairSettings
            {
                Enabled = CrosshairEnabled, IdleColor = CrosshairIdleColor, FullDamageColor = CrosshairFullDamageColor,
                BelowMaxColor = CrosshairBelowMaxColor, BrightnessMultiplier = CrosshairBrightness,
                SizeMultiplier = CrosshairSize, SpreadMultiplier = CrosshairSpread,
                LineThicknessMultiplier = CrosshairLineThickness, GapMultiplier = CrosshairGap, Alpha = CrosshairAlpha,
                ForceShape = CrosshairShape, ForceShowInAds = CrosshairForceShowInAds, HideCrosshair = CrosshairHide
            });
            _svc.SaveFovSettings(new FovSettings { Enabled = FovEnabled, Fov = Fov, AdsSensitivityMultiplier = AdsSensitivity, WeaponModelFov = WeaponModelFov });
            _svc.SaveTeamColorSettings(new TeamColorSettings { Enabled = TeamColorsEnabled, FriendlyColor = FriendlyColor, EnemyColor = EnemyColor });
            _svc.SaveDamageHealingSettings(new DamageHealingSettings
            {
                Enabled = DamageHealingEnabled, DamageNumberColor = DamageColor, CritDamageNumberColor = CritDamageColor,
                HealNumberColor = HealColor, DamageNumberSizeMultiplier = DamageSize, HealNumberSizeMultiplier = HealSize,
                Alpha = DamageAlpha, MinimumHeal = MinimumHeal, ShowFriendlyHealing = ShowFriendlyHealing,
                ShowSelfHealing = ShowSelfHealing, CombineDamageUntilHidden = CombineDamage,
                CombineHealingUntilHidden = CombineHealing, SelfHealNumberSizeMultiplier = SelfHealSize,
                SelfHealX = SelfHealX, SelfHealY = SelfHealY
            });
            _svc.SaveHealAlertSettings(new HealAlertSettings
            {
                Enabled = HealAlertEnabled, DamageIndicatorColor = HealAlertDamageColor, HealIndicatorColor = HealAlertHealColor,
                DamageIndicatorSizeMultiplier = HealAlertDamageSize, HealIndicatorSizeMultiplier = HealAlertHealSize,
                Alpha = HealAlertAlpha, MinimumHeal = HealAlertMinimumHeal, ShowDirectionOnHeal = HealAlertShowDirection
            });
            _svc.SaveBaseObjectiveBeamSettings(new BaseObjectiveBeamSettings { Enabled = ObjectiveBeamEnabled, HideBeam = ObjectiveBeamHide });
            _svc.SaveShieldBuffBarSettings(new ShieldBuffBarSettings
            {
                Enabled = ShieldEnabled, ShieldBuffBarColor = ShieldColor, ShieldClockSizeMultiplier = ShieldSize,
                ShieldClockOffsetX = ShieldOffsetX, ShieldClockOffsetY = ShieldOffsetY, ShieldTimerDisplayMode = ShieldDisplayMode
            });
            _svc.SaveLocalBuildPreviewSettings(new LocalBuildPreviewSettings { Enabled = LocalBuildPreviewEnabled });
            _svc.SaveAimHealthbarSettings(new AimHealthbarSettings { Enabled = AimHealthbarEnabled });
            _svc.SaveDeathCamHealthbarSettings(new DeathCamHealthbarSettings { Enabled = DeathCamHealthbarEnabled });
            _svc.SaveAutoCasualQueueSettings(new AutoCasualQueueSettings { Enabled = AutoCasualQueueEnabled });
            _svc.SaveTeammateHpSettings(new TeammateHpSettings
            {
                Enabled = TeammateHpEnabled || TeammateHideNameBackground,
                ShowHpText = TeammateHpEnabled,
                HideNameBackground = TeammateHideNameBackground
            });
            _svc.SaveAutoCrouchSettings(new AutoCrouchSettings { Enabled = AutoCrouchEnabled });
            _svc.SaveHideImpactVfxSettings(new HideImpactVfxSettings
            {
                Enabled = HideImpactVfxEnabled, HideImpactVfx = HideImpactVfx,
                HideLavaWaterPlane = HideLavaWaterPlane, HideFallingBlocks = HideFallingBlocks
            });
            _svc.SaveUnitGuiScaleSettings(new UnitGuiScaleSettings { Enabled = UnitGuiScaleEnabled, ScaleMultiplier = UnitGuiScale });
            _svc.SaveWsiSettings(new WsiSettings { ScaleEnabled = WsiScaleEnabled, ScaleMultiplier = WsiScale });
            _svc.SaveMapRenderOverrideSettings(new MapRenderOverrideSettings { Enabled = MapRenderEnabled, Preset = MapRenderPreset });
            _svc.SaveFriendlyLowHealthSettings(new FriendlyLowHealthSettings
            {
                Enabled = FriendlyLowHealthEnabled, Color = FriendlyLowHealthColor, Threshold = FriendlyLowHealthThreshold,
                ShowDirectionIndicator = FriendlyLowHealthIndicator, IndicatorSize = FriendlyLowHealthIndicatorSize,
                IndicatorAlpha = FriendlyLowHealthIndicatorAlpha
            });
            _svc.SaveSegmentedHealthbarSettings(new SegmentedHealthbarSettings { Enabled = SegmentedHealthbarEnabled });
            ApplySegmentedHealthbarTextures(SegmentedHealthbarEnabled);
            _svc.SaveFontOverrideSettings(new FontOverrideSettings { Enabled = FontOverrideEnabled });
            _svc.SavePerformanceOptSettings(new PerformanceOptSettings { Enabled = PerformanceOptEnabled });
            Saved?.Invoke();
        }
        catch (Exception ex) { ErrorOccurred?.Invoke("Save failed", ex.Message); }
    }

    // ── Team color presets ────────────────────────────────────────────────────
    [RelayCommand] private void ApplyBetaColors() { TeamColorsEnabled = true; FriendlyColor = "#0BA187"; EnemyColor = "#9138A5"; }
    [RelayCommand] private void ApplyClassicColors() { TeamColorsEnabled = true; FriendlyColor = "#B4D0FB"; EnemyColor = "#D7373F"; }
    [RelayCommand] private void ApplyDefaultColors() { TeamColorsEnabled = true; FriendlyColor = "#4AA3FF"; EnemyColor = "#FF5A5A"; }

    private void ApplySegmentedHealthbarTextures(bool enabled)
    {
        var mappingPath = Path.Combine(_paths.PatchingDir, "texture-replacements.txt");
        const string fillEntry = "white_rect=white_rect.png";
        const string bgEntry = "white_rect_bg=white_rect_bg.png";

        var lines = File.Exists(mappingPath)
            ? File.ReadAllLines(mappingPath, System.Text.Encoding.UTF8).ToList()
            : [];

        if (enabled)
        {
            var customTexturesDir = _installInfo?.IsDetected == true
                ? _installInfo.CustomTexturesDirectoryPath
                : null;

            if (!string.IsNullOrWhiteSpace(customTexturesDir))
            {
                Directory.CreateDirectory(customTexturesDir);
                var asm = typeof(FeatureSettingsViewModel).Assembly;
                foreach (var (resName, fileName) in new[]
                {
                    ("Patching.white_rect.png", "white_rect.png"),
                    ("Patching.white_rect_bg.png", "white_rect_bg.png")
                })
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null)
                    {
                        continue;
                    }

                    using var dest = File.Create(Path.Combine(customTexturesDir, fileName));
                    stream.CopyTo(dest);
                }
            }

            if (!lines.Any(static l => l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(fillEntry);
            }

            if (!lines.Any(static l => l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(bgEntry);
            }
        }
        else
        {
            lines.RemoveAll(static l =>
                l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase));
        }

        Directory.CreateDirectory(_paths.PatchingDir);
        File.WriteAllLines(mappingPath, lines, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

}
