using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class LauncherConfigService
{
    private const int LauncherConfigVersion = 1;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public LauncherConfigContext GetContext(GameInstallInfo installInfo)
    {
        var launcherDirectoryPath = Path.Combine(installInfo.GameRoot, "launcher");
        var cacheDirectoryPath = Path.Combine(launcherDirectoryPath, "cache");

        return new LauncherConfigContext
        {
            LauncherDirectoryPath = launcherDirectoryPath,
            ConfigPath = Path.Combine(launcherDirectoryPath, "config.json"),
            CustomConfigPath = Path.Combine(launcherDirectoryPath, "custom.json"),
            CacheDirectoryPath = cacheDirectoryPath,
            MainCachePath = Path.Combine(cacheDirectoryPath, "main.json")
        };
    }

    public LauncherConfig LoadOrCreate(GameInstallInfo installInfo, Logger logger)
    {
        var context = GetContext(installInfo);
        EnsureBaseFiles(context, logger);

        var config = ReadConfig(context.ConfigPath, logger) ?? CreateDefaultConfig();
        if (config.Version != LauncherConfigVersion)
        {
            logger.Warning($"Launcher config version mismatch ({config.Version} != {LauncherConfigVersion}). Resetting to defaults.");
            config = CreateDefaultConfig();
        }

        NormalizeUpdateSources(config);
        Save(context.ConfigPath, config);

        return MergeLocalSources(installInfo, context, config, logger);
    }

    public LauncherConfig LoadCustomConfig(GameInstallInfo installInfo, Logger logger)
    {
        var context = GetContext(installInfo);
        EnsureBaseFiles(context, logger);

        var customConfig = ReadConfig(context.CustomConfigPath, logger) ?? new LauncherConfig
        {
            Version = LauncherConfigVersion
        };

        if (customConfig.Version != LauncherConfigVersion)
        {
            logger.Warning($"Custom config version mismatch ({customConfig.Version} != {LauncherConfigVersion}). Resetting to defaults.");
            customConfig = new LauncherConfig
            {
                Version = LauncherConfigVersion
            };
        }

        Save(context.CustomConfigPath, customConfig);
        return customConfig;
    }

    public void SaveCustomConfig(GameInstallInfo installInfo, LauncherConfig customConfig)
    {
        var context = GetContext(installInfo);
        customConfig.Version = LauncherConfigVersion;
        Save(context.CustomConfigPath, customConfig);
    }

    public void SaveConfig(GameInstallInfo installInfo, LauncherConfig config)
    {
        var context = GetContext(installInfo);
        Save(context.ConfigPath, config);
    }

    public void SaveSelection(GameInstallInfo installInfo, LauncherConfig config, string selectedServerKey)
    {
        var context = GetContext(installInfo);
        config.SelectedServer = selectedServerKey;
        Save(context.ConfigPath, config);
    }

    public void WriteServersFile(GameInstallInfo installInfo, LauncherServer server)
    {
        var line = $"public {server.Host} {server.Port}";
        File.WriteAllText(installInfo.ServersFilePath, line + Environment.NewLine, new UTF8Encoding(false));
    }

    private LauncherConfig MergeLocalSources(GameInstallInfo installInfo, LauncherConfigContext context, LauncherConfig currentConfig, Logger logger)
    {
        logger.Info("Merging local configurations...");

        var mergedServers = new Dictionary<string, LauncherServer>(StringComparer.OrdinalIgnoreCase);
        var mergedPatchConfigurations = new Dictionary<string, PatchDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceEntry in currentConfig.UpdateSources)
        {
            var json = ReadSourceContent(installInfo, sourceEntry.Value, logger);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                var sourceConfig = JsonSerializer.Deserialize<LauncherConfig>(json, ReadOptions);
                if (sourceConfig is null)
                {
                    continue;
                }

                foreach (var serverEntry in sourceConfig.Servers)
                {
                    mergedServers[serverEntry.Key] = serverEntry.Value;
                }

                foreach (var patchEntry in sourceConfig.PatchConfigurations)
                {
                    mergedPatchConfigurations[patchEntry.Key] = patchEntry.Value;
                }
            }
            catch (JsonException exception)
            {
                logger.Exception(exception, $"Failed to parse source '{sourceEntry.Key}'");
            }
        }

        var mergedConfig = new LauncherConfig
        {
            Version = currentConfig.Version,
            UpdateSources = new Dictionary<string, string>(currentConfig.UpdateSources, StringComparer.OrdinalIgnoreCase),
            AutoUpdateServerList = currentConfig.AutoUpdateServerList,
            SelectedServer = currentConfig.SelectedServer,
            Servers = mergedServers,
            PatchConfigurations = mergedPatchConfigurations,
            TextureReplacementFolder = currentConfig.TextureReplacementFolder
        };

        if (string.IsNullOrWhiteSpace(mergedConfig.SelectedServer) || !mergedConfig.Servers.ContainsKey(mergedConfig.SelectedServer))
        {
            mergedConfig.SelectedServer = mergedConfig.Servers.Keys.FirstOrDefault() ?? string.Empty;
        }

        Save(context.ConfigPath, mergedConfig);
        logger.Info("Local configuration merged and saved.");

        return mergedConfig;
    }

    private static string? ReadSourceContent(GameInstallInfo installInfo, string sourceUri, Logger logger)
    {
        if (!sourceUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            logger.Warning($"Skipping unsupported source '{sourceUri}'.");
            return null;
        }

        var relativePath = sourceUri["file://".Length..];
        var fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(installInfo.GameRoot, relativePath);

        if (!File.Exists(fullPath))
        {
            logger.Warning($"Local source not found: {fullPath}");
            return null;
        }

        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    private static LauncherConfig? ReadConfig(string configPath, Logger logger)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(configPath, Encoding.UTF8);
            if (raw.Contains("}{", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Corrupted configuration detected.");
            }

            return JsonSerializer.Deserialize<LauncherConfig>(raw, ReadOptions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException)
        {
            logger.Exception(exception, "Failed to parse config.json");
            return null;
        }
    }

    private static void EnsureBaseFiles(LauncherConfigContext context, Logger logger)
    {
        Directory.CreateDirectory(context.LauncherDirectoryPath);
        Directory.CreateDirectory(context.CacheDirectoryPath);

        if (!File.Exists(context.MainCachePath))
        {
            File.WriteAllText(context.MainCachePath, GetDefaultMainCacheJson(), new UTF8Encoding(false));
            logger.Info("Created launcher cache main.json.");
        }

        if (!File.Exists(context.CustomConfigPath))
        {
            File.WriteAllText(context.CustomConfigPath, GetDefaultCustomJson(), new UTF8Encoding(false));
            logger.Info("Created launcher custom.json.");
        }
    }

    private static void NormalizeUpdateSources(LauncherConfig config)
    {
        config.UpdateSources = new Dictionary<string, string>(config.UpdateSources, StringComparer.OrdinalIgnoreCase)
        {
            ["main"] = "file://launcher/cache/main.json",
            ["custom"] = "file://launcher/custom.json"
        };
    }

    private static void Save(string path, LauncherConfig config)
    {
        var json = JsonSerializer.Serialize(config, WriteOptions);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static LauncherConfig CreateDefaultConfig()
    {
        return new LauncherConfig
        {
            Version = LauncherConfigVersion,
            AutoUpdateServerList = false,
            SelectedServer = "bnlps-v310-minimal-eu",
            UpdateSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["main"] = "file://launcher/cache/main.json",
                ["custom"] = "file://launcher/custom.json"
            }
        };
    }

    private static string GetDefaultCustomJson()
    {
        return
            """
            {
              "version": 1,
              "servers": {},
              "patch_configurations": {}
            }
            """;
    }

    private static string GetDefaultMainCacheJson()
    {
        return
            """
            {
              "version": 1,
              "servers": {
                "bnlps-v310-minimal-eu": {
                  "name": "Block N Load Community Server - EU",
                  "host": "v310.blocknload.pauldh.nl",
                  "port": 28100,
                  "patch": "default"
                },
                "nascar-master": {
                  "name": "Nascarman's Master Server",
                  "host": "5.161.46.214",
                  "port": 28100,
                  "patch": "default"
                }
              },
              "patch_configurations": {
                "default": {
                  "name": "Minimal V310 patches",
                  "files": {
                    "Win64/BlockNLoad_Data/Managed/Assembly-CSharp.dll": {
                      "hash_before": "CF008C2DCA408B77FF42DB770AA1B5782AAD360C",
                      "hash_after": "8D633ECB1B0C2E9AB5523683F1189831765C6DC7",
                      "patches": [
                        {
                          "description": "Prevent EAC kick",
                          "offset": "00061f49",
                          "before": "28 E8 0E 00 0A",
                          "after": "00 00 00 00 17"
                        },
                        {
                          "description": "Use servers.txt for server selection with default Steam branches",
                          "offset": "0015eb80",
                          "before": "39",
                          "after": "3A"
                        },
                        {
                          "description": "Prevent new player behaviour",
                          "offset": "15B585",
                          "before": "1C",
                          "after": "16"
                        }
                      ]
                    },
                    "Win64/BlockNLoad_Data/Plugins/CSteamworks.dll": {
                      "hash_before": "8868E8ED1B9DA70226C3B0808345E8D7F931F996",
                      "hash_after": "9FB7D982911FDEF890B05A4A2C62A2EEC77B1719",
                      "patches": [
                        {
                          "description": "Stop game file verification",
                          "offset": "0000215f",
                          "before": "74",
                          "after": "EB"
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;
    }
}
