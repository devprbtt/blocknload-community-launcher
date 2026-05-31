using Mono.Cecil;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class ExperimentalFeaturePatchRunner
{
    private readonly IReadOnlyDictionary<string, IExperimentalFeaturePatcher> patchersByKey;

    public ExperimentalFeaturePatchRunner()
    {
        patchersByKey = ExperimentalFeaturePatcherCatalog.All.ToDictionary(
            static patcher => patcher.FeatureKey,
            static patcher => patcher,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetApplicableFeatureKeys(IEnumerable<string> enabledFeatureKeys)
    {
        return enabledFeatureKeys
            .Where(featureKey => patchersByKey.ContainsKey(featureKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void ApplyToAssembly(
        string targetAssemblyPath,
        string helperAssemblyPath,
        IEnumerable<string> featureKeys,
        Logger? logger = null,
        string? managedDirectoryPath = null,
        string? patchingDir = null)
    {
        var selectedPatchers = ExperimentalFeaturePatcherCatalog.AlwaysRun
            .Concat(featureKeys
                .Select(featureKey => patchersByKey.TryGetValue(featureKey, out var patcher) ? patcher : null)
                .Where(static patcher => patcher is not null)
                .Cast<IExperimentalFeaturePatcher>())
            .Distinct()
            .ToArray();

        if (selectedPatchers.Length == 0)
        {
            logger?.Info("No C# experimental feature patchers were selected.");
            return;
        }

        var resolver = new DefaultAssemblyResolver();
        AddResolverDirectoryIfExists(resolver, Path.GetDirectoryName(targetAssemblyPath));
        AddResolverDirectoryIfExists(resolver, Path.GetDirectoryName(helperAssemblyPath));
        AddResolverDirectoryIfExists(resolver, managedDirectoryPath);

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        using var targetAssembly = AssemblyDefinition.ReadAssembly(targetAssemblyPath, readerParameters);
        using var helperAssembly = AssemblyDefinition.ReadAssembly(helperAssemblyPath, readerParameters);
        var context = new ExperimentalPatchContext(targetAssembly, helperAssembly, patchingDir ?? Path.GetDirectoryName(targetAssemblyPath) ?? string.Empty);

        foreach (var patcher in selectedPatchers)
        {
            logger?.Info($"Applying C# feature patcher: {patcher.FeatureKey}");
            patcher.Apply(context);
        }

        targetAssembly.Write(targetAssemblyPath);
    }

    private static void AddResolverDirectoryIfExists(DefaultAssemblyResolver resolver, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            resolver.AddSearchDirectory(path);
        }
    }
}
