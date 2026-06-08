using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BnlCommunityFixes.Core.Services;

public sealed class HelperSourceGeneratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Generate(string patchingDir, string gameRoot)
    {
        var vars = BuildVariables(patchingDir, gameRoot);

        var sb = new StringBuilder();
        AppendTemplateFile(sb, patchingDir, "HelperBase.template.cs", vars);
        AppendTemplateFile(sb, patchingDir, "AutoCasualQueueRuntime.template.cs", vars);
        AppendTemplateFile(sb, patchingDir, "TeammateHpRuntime.template.cs", vars);
        AppendTemplateFile(sb, patchingDir, "FontOverrideRuntime.template.cs", vars);
        AppendTemplateFile(sb, patchingDir, "AutoCrouchRuntime.cs", vars);

        foreach (var staticFile in new[]
        {
            "AudioReplacerRuntime.cs",
            "MeshReplacerRuntime.cs",
            "MatchReplayRecorderRuntime.cs",
            "ReplayPlayerRuntime.cs",
            "RuntimeMenu.cs"
        })
        {
            var path = Path.Combine(patchingDir, staticFile);
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                content = Regex.Replace(content, @"^using\s+[^\r\n]+;\s*", "", RegexOptions.Multiline);
                sb.AppendLine(content);
            }
        }

        var performanceOpt = ReadJson(patchingDir, "experimental-performance-opt-config.json");
        if (GetBool(performanceOpt, "enabled", false))
        {
            var deviceHealthbarCullDistance = GetFloat(performanceOpt, "device_healthbar_cull_distance", 35f);
            sb.AppendLine("namespace BnlCommunityFixes { public static class PerformanceOptGeneratedConfig { public const float DeviceHealthbarCullDistance = " + deviceHealthbarCullDistance + "; } }");
            AppendStaticFile(sb, patchingDir, "PerformanceOptRuntime.cs");
        }

        var outputPath = Path.Combine(patchingDir, "BnlCommunityFixes.generated.cs");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static void AppendStaticFile(StringBuilder sb, string patchingDir, string fileName)
    {
        var path = Path.Combine(patchingDir, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        content = Regex.Replace(content, @"^using\s+[^\r\n]+;\s*", "", RegexOptions.Multiline);
        sb.AppendLine(content);
    }

    // Extracts all C# content from a PS1/C# mixed template file.
    // Two formats are supported:
    //   1. Files with @"..."@ here-string blocks (HelperBase.template.cs): C# lives inside @"..."@.
    //      Content before the FIRST "@ terminator is also treated as C# (the initial block has no @" opener).
    //   2. Files that are pure C# with $(Format-*) placeholders inline (AutoCasualQueue, TeammateHp, FontOverride).
    private static void AppendTemplateFile(StringBuilder sb, string patchingDir, string fileName, IReadOnlyDictionary<string, string> vars)
    {
        var path = Path.Combine(patchingDir, fileName);
        if (!File.Exists(path)) return;

        var lines = File.ReadAllLines(path);
        bool hasHereStringBlocks = lines.Any(l => l.TrimStart() == "\"@");

        if (!hasHereStringBlocks)
        {
            // Pure C# template — process every line
            foreach (var line in lines)
                sb.AppendLine(ResolvePlaceholders(line, vars));
            return;
        }

        // Mixed PS1/C# template: treat lines before the first "@ as C#,
        // then alternate between PS1 (skip) and C# (emit) at each "@ / @" boundary.
        bool inCSharp = true; // starts in C# (the initial block before first "@ has no @" opener)

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (inCSharp && trimmed == "\"@")
            {
                // End of current C# block
                inCSharp = false;
                continue;
            }

            if (!inCSharp && (trimmed == "@\"" || trimmed.EndsWith(" @\"")))
            {
                // Start of next C# block
                inCSharp = true;
                continue;
            }

            if (inCSharp)
                sb.AppendLine(ResolvePlaceholders(line, vars));
            // else: PS1 code — skip
        }
    }

    private static string ResolvePlaceholders(string line, IReadOnlyDictionary<string, string> vars)
    {
        // Step 1: resolve $(...) placeholders — supports nested parens like $(Format-BoolLiteral ($X -and $Y))
        line = ReplacePsInterpolations(line, vars);

        // Step 2: resolve bare $VarLiteral placeholders (not inside $(...))
        // These appear as standalone values like: $DefaultForcedFovLiteral, $CrosshairSizeMultiplierLiteral
        line = Regex.Replace(line, @"\$([A-Za-z][A-Za-z0-9_.]*)", match =>
        {
            var varName = match.Groups[1].Value;
            return vars.TryGetValue(varName, out var val) ? val : match.Value;
        });

        return line;
    }

    // Replaces all $(...) interpolations in a line, handling nested parentheses.
    private static string ReplacePsInterpolations(string line, IReadOnlyDictionary<string, string> vars)
    {
        var sb = new StringBuilder(line.Length);
        int i = 0;
        while (i < line.Length)
        {
            if (i + 1 < line.Length && line[i] == '$' && line[i + 1] == '(')
            {
                // Find the matching closing paren, tracking nesting
                int depth = 0;
                int start = i + 2;
                int j = start;
                while (j < line.Length)
                {
                    if (line[j] == '(') depth++;
                    else if (line[j] == ')')
                    {
                        if (depth == 0) break;
                        depth--;
                    }
                    j++;
                }
                var expr = line[start..j].Trim();
                sb.Append(EvalInterpolation(expr, vars));
                i = j + 1; // skip past the closing ')'
            }
            else
            {
                sb.Append(line[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string EvalInterpolation(string expr, IReadOnlyDictionary<string, string> vars)
    {
        if (expr.StartsWith("Format-BoolLiteral "))
            return EvalBool(expr["Format-BoolLiteral ".Length..].Trim(), vars) ? "true" : "false";

        if (expr.StartsWith("Format-FloatLiteral "))
            return EvalFloat(expr["Format-FloatLiteral ".Length..].Trim(), vars);

        // $($VarName.Replace(...)) or $($VarName) — string expr
        if (expr.StartsWith("$"))
        {
            var fullName = ExtractVarName(expr[1..]);
            // Try full dotted name first, then just the simple identifier (before the first '.')
            var simpleName = Regex.Match(fullName, @"^[\w]+").Value;
            var lookupName = vars.ContainsKey(fullName) ? fullName : simpleName;
            if (vars.TryGetValue(lookupName, out var val))
                return val.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        var key = expr.TrimStart('$');
        return vars.TryGetValue(key, out var v) ? v : "$(" + expr + ")";
    }

    private static bool EvalBool(string expr, IReadOnlyDictionary<string, string> vars)
    {
        expr = expr.Trim();

        // Strip outer parens
        while (expr.StartsWith("(") && expr.EndsWith(")"))
            expr = expr[1..^1].Trim();

        if (expr.Contains(" -and "))
            return expr.Split(" -and ").All(part => EvalBool(part.Trim(), vars));

        if (expr.Contains(" -or "))
            return expr.Split(" -or ").Any(part => EvalBool(part.Trim(), vars));

        // Cast prefixes like [bool], [bool]$Var
        expr = Regex.Replace(expr, @"^\[bool\]", "").Trim();

        if (expr.Equals("$true", StringComparison.OrdinalIgnoreCase)) return true;
        if (expr.Equals("$false", StringComparison.OrdinalIgnoreCase)) return false;

        // Try compound key first (e.g. "FriendlyLowHealthEnabled -and FriendlyLowHealthIndicatorEnabled")
        if (vars.TryGetValue(expr.TrimStart('$'), out var directVal))
            return directVal == "true";

        var varName = ExtractVarName(expr.TrimStart('$'));
        return vars.TryGetValue(varName, out var val) && val == "true";
    }

    private static string EvalFloat(string expr, IReadOnlyDictionary<string, string> vars)
    {
        expr = expr.Trim();
        expr = Regex.Replace(expr, @"^\[(double|single|float)\]", "").Trim();
        // Strip outer parens
        while (expr.StartsWith("(") && expr.EndsWith(")"))
            expr = expr[1..^1].Trim();

        var varName = ExtractVarName(expr.TrimStart('$'));
        if (vars.TryGetValue(varName, out var val))
        {
            // val may already be a float literal ("1f") or raw number
            var numStr = val.TrimEnd('f');
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return FormatFloat(f);
        }
        return "0f";
    }

    private static string ExtractVarName(string expr)
    {
        return Regex.Match(expr, @"^[\w.]+").Value;
    }

    private Dictionary<string, string> BuildVariables(string patchingDir, string gameRoot)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Font config
        var font = ReadJson(patchingDir, "experimental-font-config.json");
        var selectedFont = GetString(font, "selected_font", "__DEFAULT__");
        if (string.IsNullOrWhiteSpace(selectedFont)) selectedFont = "__DEFAULT__";
        vars["SelectedFontLiteral"] = selectedFont.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var selectedStyle = GetString(font, "font_style", "Keep");
        if (string.IsNullOrWhiteSpace(selectedStyle)) selectedStyle = "Keep";
        vars["SelectedStyleLiteral"] = selectedStyle.Replace("\\", "\\\\").Replace("\"", "\\\"");
        vars["SizeMultiplierLiteral"] = GetFloat(font, "size_multiplier", 1f);
        vars["LineSpacingMultiplierLiteral"] = GetFloat(font, "line_spacing_multiplier", 1f);

        // Shield timer
        var shield = ReadJson(patchingDir, "experimental-enemy-shield-buffbar-config.json");
        vars["EnemyShieldBuffBarEnabled"] = GetBool(shield, "enabled", false) ? "true" : "false";
        vars["EnemyShieldClockSizeMultiplier"] = GetFloat(shield, "shield_clock_size_multiplier", 1f);
        vars["EnemyShieldClockOffsetX"] = GetFloat(shield, "shield_clock_offset_x", 0f);
        vars["EnemyShieldClockOffsetY"] = GetFloat(shield, "shield_clock_offset_y", 0f);
        vars["EnemyShieldTimerDisplayMode"] = GetString(shield, "shield_timer_display_mode", "circle");
        var shieldColor = ParseColor(GetString(shield, "shield_buff_bar_color", "#FFF04A"));
        vars["EnemyShieldBuffBarColor.R"] = shieldColor.r;
        vars["EnemyShieldBuffBarColor.G"] = shieldColor.g;
        vars["EnemyShieldBuffBarColor.B"] = shieldColor.b;
        vars["EnemyShieldBuffBarColor.A"] = "1f";

        // Friendly low health
        var flh = ReadJson(patchingDir, "friendly-low-health-config.json");
        bool flhEnabled = GetBool(flh, "enabled", true);
        vars["FriendlyLowHealthEnabled"] = flhEnabled ? "true" : "false";
        vars["FriendlyLowHealthThreshold"] = GetFloat(flh, "threshold", 0.3f);
        vars["LowThreshold"] = vars["FriendlyLowHealthThreshold"];
        var flhColor = ParseColor(GetString(flh, "color", "#FF4444"));
        vars["FriendlyLowHealthColor.R"] = flhColor.r;
        vars["FriendlyLowHealthColor.G"] = flhColor.g;
        vars["FriendlyLowHealthColor.B"] = flhColor.b;
        bool flhIndicatorEnabled = GetBool(flh, "indicator_enabled", false);
        vars["FriendlyLowHealthIndicatorEnabled"] = flhIndicatorEnabled ? "true" : "false";
        vars["FriendlyLowHealthIndicatorSize"] = GetFloat(flh, "indicator_size", 1f);
        vars["FriendlyLowHealthIndicatorAlpha"] = GetFloat(flh, "indicator_alpha", 1f);
        // Compound expression used as key in the template
        vars["FriendlyLowHealthIndicatorEnabled -and $FriendlyLowHealthEnabled"] = (flhEnabled && flhIndicatorEnabled) ? "true" : "false";
        vars["FriendlyLowHealthEnabled -and FriendlyLowHealthIndicatorEnabled"] = (flhEnabled && flhIndicatorEnabled) ? "true" : "false";

        // Crosshair
        var ch = ReadJson(patchingDir, "crosshair-config.json");
        bool chEnabled = GetBool(ch, "enabled", false);
        vars["CrosshairConfig.enabled"] = chEnabled ? "true" : "false";
        vars["CrosshairHideEntirely"] = GetBool(ch, "hide_crosshair", false) ? "true" : "false";
        double chAlpha = GetDouble(ch, "alpha", 1.0);
        double chBright = GetDouble(ch, "brightness_multiplier", 1.0);
        var chIdle = ParseColorWithAlphaAndBrightness(GetString(ch, "idle_color", "#FFFFFF"), chAlpha, chBright);
        var chFull = ParseColorWithAlphaAndBrightness(GetString(ch, "full_damage_color", "#FF0000"), chAlpha, chBright);
        var chBelow = ParseColorWithAlphaAndBrightness(GetString(ch, "below_max_color", "#FFFF00"), chAlpha, chBright);
        vars["CrosshairIdleR"] = chIdle.r; vars["CrosshairIdleG"] = chIdle.g; vars["CrosshairIdleB"] = chIdle.b; vars["CrosshairIdleA"] = "1f";
        vars["CrosshairFullR"] = chFull.r; vars["CrosshairFullG"] = chFull.g; vars["CrosshairFullB"] = chFull.b; vars["CrosshairFullA"] = "1f";
        vars["CrosshairBelowR"] = chBelow.r; vars["CrosshairBelowG"] = chBelow.g; vars["CrosshairBelowB"] = chBelow.b; vars["CrosshairBelowA"] = "1f";
        vars["CrosshairSizeMultiplierLiteral"] = GetFloat(ch, "size_multiplier", 1f);
        vars["CrosshairSpreadMultiplierLiteral"] = GetFloat(ch, "spread_multiplier", 1f);
        vars["CrosshairLineThicknessMultiplierLiteral"] = GetFloat(ch, "line_thickness_multiplier", 1f);
        vars["CrosshairGapMultiplierLiteral"] = GetFloat(ch, "gap_multiplier", 1f);
        vars["CrosshairForceShowInAdsLiteral"] = GetBool(ch, "force_show_in_ads", false) ? "true" : "false";
        vars["CrosshairForceShapeLiteral"] = GetString(ch, "force_shape", "__DEFAULT__").Replace("\\", "\\\\").Replace("\"", "\\\"");

        // Damage / healing
        var dh = ReadJson(patchingDir, "damage-healing-indicator-config.json");
        bool dhEnabled = GetBool(dh, "enabled", false);
        vars["DamageHealingConfig.enabled"] = dhEnabled ? "true" : "false";
        double dhAlpha = GetDouble(dh, "alpha", 1.0);
        var dmgColor = ParseColorHex(GetString(dh, "damage_number_color", "#FFFFFF"), dhAlpha);
        var critColor = ParseColorHex(GetString(dh, "crit_damage_number_color", "#FFFFFF"), dhAlpha);
        var healColor = ParseColorHex(GetString(dh, "heal_number_color", "#91ED78"), dhAlpha);
        vars["DamageColor.R"] = dmgColor.r; vars["DamageColor.G"] = dmgColor.g; vars["DamageColor.B"] = dmgColor.b;
        vars["CritDamageColor.R"] = critColor.r; vars["CritDamageColor.G"] = critColor.g; vars["CritDamageColor.B"] = critColor.b;
        vars["HealColor.R"] = healColor.r; vars["HealColor.G"] = healColor.g; vars["HealColor.B"] = healColor.b;
        vars["UseDamageColor"] = GetString(dh, "damage_number_color", "") != "" ? "true" : "false";
        vars["UseCritDamageColor"] = GetString(dh, "crit_damage_number_color", "") != "" ? "true" : "false";
        vars["UseHealColor"] = GetString(dh, "heal_number_color", "") != "" ? "true" : "false";
        vars["DamageSize"] = GetFloat(dh, "damage_number_size_multiplier", 2f);
        vars["HealSize"] = GetFloat(dh, "heal_number_size_multiplier", 2f);
        vars["SelfHealSize"] = GetFloat(dh, "heal_number_size_multiplier", 2f);
        vars["DamageHealingAlpha"] = FormatFloat((float)dhAlpha);
        vars["MinimumHeal"] = GetFloat(dh, "minimum_heal", 0f);
        vars["ShowFriendlyHealing"] = GetBool(dh, "show_friendly_healing", true) ? "true" : "false";
        vars["ShowSelfHealing"] = GetBool(dh, "show_self_healing", true) ? "true" : "false";
        vars["CombineDamageUntilHidden"] = GetBool(dh, "combine_damage_until_hidden", false) ? "true" : "false";
        vars["CombineHealingUntilHidden"] = GetBool(dh, "combine_healing_until_hidden", false) ? "true" : "false";
        vars["SelfHealX"] = GetFloat(dh, "self_heal_x", 0f);
        vars["SelfHealY"] = GetFloat(dh, "self_heal_y", -550f);

        // Heal alerts
        var ha = ReadJson(patchingDir, "heal-alert-indicator-config.json");
        vars["HealAlertConfig.enabled"] = GetBool(ha, "enabled", false) ? "true" : "false";
        double haAlpha = GetDouble(ha, "alpha", 1.0);
        var haDmgColor = ParseColorHex(GetString(ha, "damage_color", "#FF6464"), haAlpha);
        var haHealColor = ParseColorHex(GetString(ha, "heal_color", "#64FF64"), haAlpha);
        vars["HealAlertDamageColor.R"] = haDmgColor.r; vars["HealAlertDamageColor.G"] = haDmgColor.g;
        vars["HealAlertDamageColor.B"] = haDmgColor.b; vars["HealAlertDamageColor.A"] = FormatFloat((float)haAlpha);
        vars["HealAlertHealColor.R"] = haHealColor.r; vars["HealAlertHealColor.G"] = haHealColor.g;
        vars["HealAlertHealColor.B"] = haHealColor.b; vars["HealAlertHealColor.A"] = FormatFloat((float)haAlpha);
        vars["HealAlertUseDamageColor"] = GetString(ha, "damage_color", "") != "" ? "true" : "false";
        vars["HealAlertUseHealColor"] = GetString(ha, "heal_color", "") != "" ? "true" : "false";
        vars["HealAlertDamageSize"] = GetFloat(ha, "damage_size_multiplier", 1f);
        vars["HealAlertHealSize"] = GetFloat(ha, "heal_size_multiplier", 1f);
        vars["HealAlertMinimumHeal"] = GetFloat(ha, "minimum_heal", 0f);
        vars["HealAlertShowDir"] = GetBool(ha, "show_direction", false) ? "true" : "false";

        // Team colors
        var tc = ReadJson(patchingDir, "experimental-team-color-config.json");
        vars["TeamColorConfig.enabled"] = GetBool(tc, "enabled", false) ? "true" : "false";
        var friendlyColor = ParseColor(GetString(tc, "friendly_color", "#4AA3FF"));
        var enemyColor = ParseColor(GetString(tc, "enemy_color", "#FF5A5A"));
        vars["RuntimeFriendlyTeamColor.R"] = friendlyColor.r; vars["RuntimeFriendlyTeamColor.G"] = friendlyColor.g; vars["RuntimeFriendlyTeamColor.B"] = friendlyColor.b;
        vars["RuntimeEnemyTeamColor.R"] = enemyColor.r; vars["RuntimeEnemyTeamColor.G"] = enemyColor.g; vars["RuntimeEnemyTeamColor.B"] = enemyColor.b;

        // Base objective beam
        var beam = ReadJson(patchingDir, "experimental-base-objective-beam-config.json");
        vars["BaseObjectiveBeamConfig.enabled"] = GetBool(beam, "enabled", false) ? "true" : "false";
        vars["BaseObjectiveBeamConfig.hide_beam"] = GetBool(beam, "hide_beam", false) ? "true" : "false";

        // Hide impact vfx
        var vfx = ReadJson(patchingDir, "experimental-hide-impact-vfx-config.json");
        vars["HideImpactVfxConfig.enabled"] = GetBool(vfx, "enabled", false) ? "true" : "false";
        vars["HideImpactVfxConfig.hide_impact_vfx"] = GetBool(vfx, "hide_impact_vfx", false) ? "true" : "false";
        vars["HideImpactVfxConfig.hide_lava_water_plane"] = GetBool(vfx, "hide_lava_water_plane", false) ? "true" : "false";
        vars["HideImpactVfxConfig.hide_falling_blocks"] = GetBool(vfx, "hide_falling_blocks", false) ? "true" : "false";

        // Unit GUI scale
        var ugs = ReadJson(patchingDir, "unit-gui-scale-config.json");
        vars["UnitGuiScaleConfig.enabled"] = GetBool(ugs, "enabled", false) ? "true" : "false";
        vars["UnitGuiScaleConfig.scale_multiplier"] = GetFloat(ugs, "scale_multiplier", 1f);

        // WSI scale
        var wsi = ReadJson(patchingDir, "wsi-config.json");
        vars["WsiConfig.scale_enabled"] = GetBool(wsi, "scale_enabled", false) ? "true" : "false";
        vars["WsiConfig.scale_multiplier"] = GetFloat(wsi, "scale_multiplier", 1f);

        // Map render
        var mr = ReadJson(patchingDir, "experimental-map-render-config.json");
        vars["MapRenderConfig.enabled"] = GetBool(mr, "enabled", false) ? "true" : "false";
        vars["MapRenderPresetLiteral"] = GetString(mr, "preset", "Default").Replace("\\", "\\\\").Replace("\"", "\\\"");

        // Aim healthbar
        var ah = ReadJson(patchingDir, "aim-healthbar-config.json");
        vars["AimHealthbarConfig.enabled"] = GetBool(ah, "enabled", true) ? "true" : "false";

        // Deathcam healthbar
        var dc = ReadJson(patchingDir, "deathcam-healthbar-config.json");
        vars["DeathCamHealthbarConfig.enabled"] = GetBool(dc, "enabled", true) ? "true" : "false";

        // FOV
        var fov = ReadJson(patchingDir, "fov-config.json");
        bool fovEnabled = GetBool(fov, "enabled", false);
        // Compound key matching the template expression
        vars["EnableFovFeature -and [bool]$FovConfig.enabled"] = fovEnabled ? "true" : "false";
        vars["EnableFovFeature -and [bool]FovConfig.enabled"] = fovEnabled ? "true" : "false";
        vars["DefaultForcedFovLiteral"] = GetFloat(fov, "fov", 120f);
        vars["AdsSensitivityMultiplierLiteral"] = GetFloat(fov, "ads_sensitivity_multiplier", 1f);
        vars["DefaultWeaponModelFovLiteral"] = GetFloat(fov, "weapon_model_fov", 30f);

        // Local build preview
        var lbp = ReadJson(patchingDir, "experimental-local-build-preview-config.json");
        vars["LocalBuildPreviewConfig.enabled"] = GetBool(lbp, "enabled", false) ? "true" : "false";

        // Auto casual queue
        var acq = ReadJson(patchingDir, "experimental-auto-casual-queue-config.json");
        vars["AutoCasualQueueConfig.enabled"] = GetBool(acq, "enabled", false) ? "true" : "false";

        // Auto crouch
        var autoCrouch = ReadJson(patchingDir, "experimental-auto-crouch-config.json");
        vars["AutoCrouchEnabled"] = GetBool(autoCrouch, "enabled", false) ? "true" : "false";

        // Teammate HP
        var thp = ReadJson(patchingDir, "teammate-hp-config.json");
        bool teammateShowHpText = GetBool(thp, "show_hp_text", GetBool(thp, "enabled", false));
        bool teammateHideNameBackground = GetBool(thp, "hide_name_background", false);
        vars["TeammateHpEnabled"] = teammateShowHpText ? "true" : "false";
        vars["TeammateHideNameBackground"] = teammateHideNameBackground ? "true" : "false";
        vars["TeammateHpEnabledOrBackgroundHide"] = (teammateShowHpText || teammateHideNameBackground) ? "true" : "false";

        // Font override
        var fo = ReadJson(patchingDir, "experimental-font-override-config.json");
        vars["FontOverrideEnabled"] = GetBool(fo, "enabled", false) ? "true" : "false";

        return vars;
    }

    private static JsonElement ReadJson(string patchingDir, string fileName)
    {
        var path = Path.Combine(patchingDir, fileName);
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path), JsonOptions);
        }
        catch { return default; }
    }

    private static bool GetBool(JsonElement el, string key, bool def)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p))
            return p.ValueKind == JsonValueKind.True || (p.ValueKind != JsonValueKind.False && def);
        return def;
    }

    private static double GetDouble(JsonElement el, string key, double def)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number)
            return p.TryGetDouble(out var v) ? v : def;
        return def;
    }

    private static int GetInt(JsonElement el, string key, int def)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number)
            return p.TryGetInt32(out var v) ? v : def;
        return def;
    }

    private static string GetFloat(JsonElement el, string key, float def)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number)
            return p.TryGetSingle(out var v) ? FormatFloat(v) : FormatFloat(def);
        return FormatFloat(def);
    }

    private static string GetString(JsonElement el, string key, string def)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString() ?? def;
        return def;
    }

    private static string FormatFloat(float v) =>
        v.ToString("G9", CultureInfo.InvariantCulture) + "f";

    private static (string r, string g, string b) ParseColor(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return ("0f", "0f", "0f");
        int r = Convert.ToInt32(hex[..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);
        return (FormatFloat(r / 255f), FormatFloat(g / 255f), FormatFloat(b / 255f));
    }

    private static (string r, string g, string b) ParseColorHex(string hex, double alpha)
    {
        return ParseColor(hex);
    }

    private static (string r, string g, string b) ParseColorWithAlphaAndBrightness(string hex, double alpha, double brightness)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return ("0f", "0f", "0f");
        float r = Math.Min(1f, (Convert.ToInt32(hex[..2], 16) / 255f) * (float)brightness);
        float g = Math.Min(1f, (Convert.ToInt32(hex[2..4], 16) / 255f) * (float)brightness);
        float b = Math.Min(1f, (Convert.ToInt32(hex[4..6], 16) / 255f) * (float)brightness);
        return (FormatFloat(r), FormatFloat(g), FormatFloat(b));
    }
}
