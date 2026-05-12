using System.Globalization;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class RuntimeMenuSyncService
{
    private const string RuntimeConfigFileName = "bnl-runtime-menu.json";

    public string GetRuntimeConfigPath(string managedDirectoryPath) =>
        Path.Combine(managedDirectoryPath, RuntimeConfigFileName);

    // Returns true if the runtime config file is newer than the launcher JSON config.
    public bool IsRuntimeNewer(string runtimeConfigPath, string launcherConfigPath)
    {
        if (!File.Exists(runtimeConfigPath))
            return false;
        if (!File.Exists(launcherConfigPath))
            return true;

        return File.GetLastWriteTimeUtc(runtimeConfigPath) > File.GetLastWriteTimeUtc(launcherConfigPath);
    }

    // Reads the damage/healing fields from the runtime key=value config and applies
    // them onto an existing DamageHealingSettings instance (leaves other fields intact).
    public DamageHealingSettings ReadDamageHealingSettings(string runtimeConfigPath, DamageHealingSettings current)
    {
        if (!File.Exists(runtimeConfigPath))
            return current;

        try
        {
            var lines = File.ReadAllLines(runtimeConfigPath);
            foreach (var line in lines)
            {
                var sep = line.IndexOf('=');
                if (sep <= 0) continue;
                var key = line[..sep].Trim();
                var val = line[(sep + 1)..].Trim();
                ApplyKeyValue(current, key, val);
            }
        }
        catch
        {
            // If we can't read the runtime config, keep existing values.
        }

        return current;
    }

    // Merges the damage/healing fields from settings into the runtime key=value config,
    // preserving all other runtime settings (crosshair, fov, etc.).
    public void WriteDamageHealingSettings(string runtimeConfigPath, DamageHealingSettings settings)
    {
        try
        {
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(runtimeConfigPath))
            {
                foreach (var line in File.ReadAllLines(runtimeConfigPath))
                {
                    var sep = line.IndexOf('=');
                    if (sep <= 0) continue;
                    existing[line[..sep].Trim()] = line[(sep + 1)..].Trim();
                }
            }

            existing["damage_number_size_multiplier"] = settings.DamageNumberSizeMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["heal_number_size_multiplier"] = settings.HealNumberSizeMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["self_heal_number_size_multiplier"] = settings.SelfHealNumberSizeMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["self_heal_x"] = settings.SelfHealX.ToString(CultureInfo.InvariantCulture);
            existing["self_heal_y"] = settings.SelfHealY.ToString(CultureInfo.InvariantCulture);
            existing["combat_alpha"] = settings.Alpha.ToString(CultureInfo.InvariantCulture);
            existing["minimum_heal"] = settings.MinimumHeal.ToString(CultureInfo.InvariantCulture);
            existing["show_friendly_healing"] = settings.ShowFriendlyHealing.ToString();
            existing["show_self_healing"] = settings.ShowSelfHealing.ToString();
            existing["combine_damage_until_hidden"] = settings.CombineDamageUntilHidden.ToString();
            existing["combine_healing_until_hidden"] = settings.CombineHealingUntilHidden.ToString();

            File.WriteAllText(runtimeConfigPath, string.Join("\n", existing.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch
        {
            // Best-effort — don't fail a save just because the game dir isn't accessible.
        }
    }

    public TeamColorSettings ReadTeamColorSettings(string runtimeConfigPath, TeamColorSettings current)
    {
        if (!File.Exists(runtimeConfigPath))
            return current;

        try
        {
            var lines = File.ReadAllLines(runtimeConfigPath);
            foreach (var line in lines)
            {
                var sep = line.IndexOf('=');
                if (sep <= 0) continue;
                var key = line[..sep].Trim();
                var val = line[(sep + 1)..].Trim();
                switch (key)
                {
                    case "team_friendly_color": current.FriendlyColor = val; break;
                    case "team_enemy_color":    current.EnemyColor    = val; break;
                }
            }
        }
        catch { }

        return current;
    }

    public void WriteTeamColorSettings(string runtimeConfigPath, TeamColorSettings settings)
    {
        try
        {
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(runtimeConfigPath))
            {
                foreach (var line in File.ReadAllLines(runtimeConfigPath))
                {
                    var sep = line.IndexOf('=');
                    if (sep <= 0) continue;
                    existing[line[..sep].Trim()] = line[(sep + 1)..].Trim();
                }
            }
            existing["team_friendly_color"] = settings.FriendlyColor;
            existing["team_enemy_color"]    = settings.EnemyColor;
            File.WriteAllText(runtimeConfigPath, string.Join("\n", existing.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch { }
    }

    public CrosshairSettings ReadCrosshairSettings(string runtimeConfigPath, CrosshairSettings current)
    {
        if (!File.Exists(runtimeConfigPath))
            return current;

        try
        {
            var lines = File.ReadAllLines(runtimeConfigPath);
            foreach (var line in lines)
            {
                var sep = line.IndexOf('=');
                if (sep <= 0) continue;
                var key = line[..sep].Trim();
                var val = line[(sep + 1)..].Trim();
                switch (key)
                {
                    case "crosshair_size_multiplier":
                        if (TryParseDouble(val, out var d)) current.SizeMultiplier = d; break;
                    case "crosshair_spread_multiplier":
                        if (TryParseDouble(val, out d)) current.SpreadMultiplier = d; break;
                    case "crosshair_alpha":
                        if (TryParseDouble(val, out d)) current.Alpha = d; break;
                    case "crosshair_force_show_in_ads":
                        if (bool.TryParse(val, out var b)) current.ForceShowInAds = b; break;
                    case "crosshair_hide_entirely":
                        if (bool.TryParse(val, out b)) current.HideCrosshair = b; break;
                    case "crosshair_force_shape":
                        current.ForceShape = string.IsNullOrEmpty(val) ? "__DEFAULT__" : val; break;
                }
            }
        }
        catch { }

        return current;
    }

    public void WriteCrosshairSettings(string runtimeConfigPath, CrosshairSettings settings)
    {
        try
        {
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(runtimeConfigPath))
            {
                foreach (var line in File.ReadAllLines(runtimeConfigPath))
                {
                    var sep = line.IndexOf('=');
                    if (sep <= 0) continue;
                    existing[line[..sep].Trim()] = line[(sep + 1)..].Trim();
                }
            }
            existing["crosshair_size_multiplier"] = settings.SizeMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["crosshair_spread_multiplier"] = settings.SpreadMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["crosshair_alpha"] = settings.Alpha.ToString(CultureInfo.InvariantCulture);
            existing["crosshair_force_show_in_ads"] = settings.ForceShowInAds.ToString();
            existing["crosshair_hide_entirely"] = settings.HideCrosshair.ToString();
            existing["crosshair_force_shape"] = string.IsNullOrEmpty(settings.ForceShape) ? "__DEFAULT__" : settings.ForceShape;
            File.WriteAllText(runtimeConfigPath, string.Join("\n", existing.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch { }
    }

    public FovSettings ReadFovSettings(string runtimeConfigPath, FovSettings current)
    {
        if (!File.Exists(runtimeConfigPath))
            return current;

        try
        {
            var lines = File.ReadAllLines(runtimeConfigPath);
            foreach (var line in lines)
            {
                var sep = line.IndexOf('=');
                if (sep <= 0) continue;
                var key = line[..sep].Trim();
                var val = line[(sep + 1)..].Trim();
                switch (key)
                {
                    case "fov":
                        if (TryParseDouble(val, out var d)) current.Fov = d; break;
                    case "ads_sensitivity_multiplier":
                        if (TryParseDouble(val, out d)) current.AdsSensitivityMultiplier = d; break;
                    case "weapon_model_fov":
                        if (TryParseDouble(val, out d)) current.WeaponModelFov = d; break;
                }
            }
        }
        catch { }

        return current;
    }

    public void WriteFovSettings(string runtimeConfigPath, FovSettings settings)
    {
        try
        {
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(runtimeConfigPath))
            {
                foreach (var line in File.ReadAllLines(runtimeConfigPath))
                {
                    var sep = line.IndexOf('=');
                    if (sep <= 0) continue;
                    existing[line[..sep].Trim()] = line[(sep + 1)..].Trim();
                }
            }
            existing["fov"] = settings.Fov.ToString(CultureInfo.InvariantCulture);
            existing["ads_sensitivity_multiplier"] = settings.AdsSensitivityMultiplier.ToString(CultureInfo.InvariantCulture);
            existing["weapon_model_fov"] = settings.WeaponModelFov.ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(runtimeConfigPath, string.Join("\n", existing.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch { }
    }

    private static void ApplyKeyValue(DamageHealingSettings s, string key, string val)
    {
        switch (key)
        {
            case "damage_number_size_multiplier":
                if (TryParseDouble(val, out var d)) s.DamageNumberSizeMultiplier = d;
                break;
            case "heal_number_size_multiplier":
                if (TryParseDouble(val, out d)) s.HealNumberSizeMultiplier = d;
                break;
            case "self_heal_number_size_multiplier":
                if (TryParseDouble(val, out d)) s.SelfHealNumberSizeMultiplier = d;
                break;
            case "self_heal_x":
                if (TryParseDouble(val, out d)) s.SelfHealX = d;
                break;
            case "self_heal_y":
                if (TryParseDouble(val, out d)) s.SelfHealY = d;
                break;
            case "combat_alpha":
                if (TryParseDouble(val, out d)) s.Alpha = d;
                break;
            case "minimum_heal":
                if (TryParseDouble(val, out d)) s.MinimumHeal = d;
                break;
            case "show_friendly_healing":
                if (bool.TryParse(val, out var b)) s.ShowFriendlyHealing = b;
                break;
            case "show_self_healing":
                if (bool.TryParse(val, out b)) s.ShowSelfHealing = b;
                break;
            case "combine_damage_until_hidden":
                if (bool.TryParse(val, out b)) s.CombineDamageUntilHidden = b;
                break;
            case "combine_healing_until_hidden":
                if (bool.TryParse(val, out b)) s.CombineHealingUntilHidden = b;
                break;
        }
    }

    private static bool TryParseDouble(string val, out double result) =>
        double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
