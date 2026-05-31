using System.Reflection;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class BundledPatchingAssetService
{
    private const string ResourcePrefix = "Patching.";

    private readonly AppPaths paths;
    private readonly Logger logger;

    public BundledPatchingAssetService(AppPaths paths, Logger logger)
    {
        this.paths = paths;
        this.logger = logger;
    }

    public void EnsureAssetsExtracted()
    {
        var resourceAssemblies = GetResourceAssemblies();
        var extractedAny = false;

        foreach (var assembly in resourceAssemblies)
        {
            foreach (var resourceName in assembly.GetManifestResourceNames().Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                extractedAny = true;

                var fileName = resourceName[ResourcePrefix.Length..];
                var destinationPath = Path.Combine(paths.PatchingDir, fileName);
                var preserveExistingJson = string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase);

                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream is null)
                {
                    throw new InvalidOperationException($"Bundled resource '{resourceName}' could not be opened.");
                }

                var destinationInfo = new FileInfo(destinationPath);
                if (preserveExistingJson && destinationInfo.Exists)
                {
                    continue;
                }

                Directory.CreateDirectory(destinationInfo.DirectoryName!);
                using var output = File.Create(destinationPath);
                resourceStream.CopyTo(output);
                logger.Info($"Extracted bundled patching asset '{fileName}' from '{assembly.GetName().Name}'.");
            }
        }

        if (!extractedAny)
        {
            throw new InvalidOperationException(
                "No bundled patching resources were found in the launcher assembly. " +
                "The published app is missing embedded patching assets.");
        }
    }

    private static IReadOnlyList<Assembly> GetResourceAssemblies()
    {
        var assemblies = new List<Assembly>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddAssembly(Assembly? assembly)
        {
            if (assembly is null)
            {
                return;
            }

            var fullName = assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N");
            if (seen.Add(fullName))
            {
                assemblies.Add(assembly);
            }
        }

        AddAssembly(Assembly.GetEntryAssembly());
        AddAssembly(Assembly.GetExecutingAssembly());

        return assemblies;
    }
}
