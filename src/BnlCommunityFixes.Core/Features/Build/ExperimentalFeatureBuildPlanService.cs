using System.Text.Json;

namespace BnlCommunityFixes.Core.Features.Build;

public sealed class ExperimentalFeatureBuildPlanService
{
    public ExperimentalFeatureBuildPlan Create(string patchingDirectory)
    {
        var entries = FeatureConfigCatalog.All
            .Select(definition => CreateEntry(patchingDirectory, definition))
            .ToArray();

        return new ExperimentalFeatureBuildPlan(entries);
    }

    private static ExperimentalFeatureBuildEntry CreateEntry(string patchingDirectory, FeatureConfigDefinition definition)
    {
        var configPath = Path.Combine(patchingDirectory, definition.FileName);
        if (!File.Exists(configPath))
        {
            return new ExperimentalFeatureBuildEntry(definition, configPath, ConfigExists: false, IsEnabled: false);
        }

        return new ExperimentalFeatureBuildEntry(
            definition,
            configPath,
            ConfigExists: true,
            IsEnabled: ReadEnabledState(configPath, definition.EnabledPropertyName));
    }

    private static bool ReadEnabledState(string configPath, string enabledPropertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!document.RootElement.TryGetProperty(enabledPropertyName, out var property))
            {
                return false;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => property.TryGetInt32(out var number) && number != 0,
                JsonValueKind.String => bool.TryParse(property.GetString(), out var boolValue) && boolValue,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
