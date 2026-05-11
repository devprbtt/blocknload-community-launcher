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
