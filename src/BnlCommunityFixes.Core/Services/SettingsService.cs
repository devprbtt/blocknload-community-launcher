using System.Text.Json;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class SettingsService
{
    private const string ManifestUrlEnvironmentVariable = "BNL_MANIFEST_URL";
    private const string DefaultManifestUrl = "https://api.github.com/repos/devprbtt/blocknload-community-launcher/contents/updates/manifest-stable.json?ref=main";
    private const string LegacyRawManifestUrl = "https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/main/updates/manifest-stable.json";

    private readonly AppPaths paths;

    public SettingsService(AppPaths paths)
    {
        this.paths = paths;
    }

    public string SettingsPath => Path.Combine(paths.DataDir, "launcher-settings.json");

    public LauncherSettings Load()
    {
        var settings = ReadSettingsFile();
        var environmentOverride = Environment.GetEnvironmentVariable(ManifestUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            settings.ManifestUrl = environmentOverride.Trim();
        }

        if (string.IsNullOrWhiteSpace(settings.ManifestUrl))
        {
            settings.ManifestUrl = DefaultManifestUrl;
        }
        else if (IsLegacyRawManifestUrl(settings.ManifestUrl))
        {
            settings.ManifestUrl = DefaultManifestUrl;
            Save(settings);
        }

        return settings;
    }

    public void EnsureDefaultFile()
    {
        if (File.Exists(SettingsPath))
        {
            return;
        }

        var defaultSettings = new LauncherSettings();
        Save(defaultSettings);
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
    }

    private LauncherSettings ReadSettingsFile()
    {
        if (!File.Exists(SettingsPath))
        {
            return new LauncherSettings();
        }

        var json = File.ReadAllText(SettingsPath);
        var settings = JsonSerializer.Deserialize<LauncherSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return settings ?? new LauncherSettings();
    }

    private static bool IsLegacyRawManifestUrl(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.Equals(normalized, LegacyRawManifestUrl, StringComparison.OrdinalIgnoreCase);
    }
}
