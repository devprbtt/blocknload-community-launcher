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
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames().Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
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
            logger.Info($"Extracted bundled patching asset '{fileName}'.");
        }
    }
}
