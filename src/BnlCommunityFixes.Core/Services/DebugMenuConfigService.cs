using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BnlCommunityFixes.Core.Services;

public sealed class DebugMenuConfigService
{
    private static readonly string[] KeyPropertyNames =
    [
        "debug_menu_key",
        "main_menu_key",
        "lobby_menu_key",
        "zone_menu_key"
    ];

    public bool LoadBool(string path, string key)
    {
        try
        {
            var config = Load(path);
            return config[key]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }

    public void SaveBooleanOption(string path, string key, bool value, bool isDebugLauncher)
    {
        var config = Load(path);
        config[key] = value;
        Normalize(config, isDebugLauncher);
        Save(path, config);
    }

    public void ApplyLauncherProfile(string path, bool isDebugLauncher)
    {
        var config = Load(path);
        Normalize(config, isDebugLauncher);
        Save(path, config);
    }

    private static JsonObject Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new JsonObject();
            }

            var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject;
            return root ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static void Save(string path, JsonObject config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static void Normalize(JsonObject config, bool isDebugLauncher)
    {
        var skipIntro = ReadBool(config, "skip_intro");
        var disableMainMenuFrameCap = ReadBool(config, "disable_main_menu_frame_cap");

        config["skip_intro"] = skipIntro;
        config["disable_main_menu_frame_cap"] = disableMainMenuFrameCap;

        if (isDebugLauncher)
        {
            config["enabled"] = true;
            config["debug_menu_key"] = "F9";
            config["main_menu_key"] = "F10";
            config["lobby_menu_key"] = "F11";
            config["zone_menu_key"] = "F12";
            return;
        }

        foreach (var keyPropertyName in KeyPropertyNames)
        {
            config.Remove(keyPropertyName);
        }

        config["enabled"] = skipIntro || disableMainMenuFrameCap;
    }

    private static bool ReadBool(JsonObject config, string key)
    {
        var node = config[key];
        if (node is null)
        {
            return false;
        }

        try
        {
            return node.GetValueKind() switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) && number != 0,
                JsonValueKind.String => bool.TryParse(node.GetValue<string>(), out var boolValue) && boolValue,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
