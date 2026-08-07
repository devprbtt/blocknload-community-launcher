using BnlCommunityFixes.Core.Features.Build;
using BnlCommunityFixes.Core.Features.Build.Patching;
using BnlCommunityFixes.Core.Features;
using BnlCommunityFixes.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BnlCommunityFixes.Core.Services;

public sealed class ExperimentalAssemblyBuildService
{
    private const string GeneratedHelperSourceFileName = "BnlCommunityFixes.generated.cs";
    private const string HelperAssemblyFileName = "BnlCommunityFixes.dll";
    private const string ExperimentalAssemblyFileName = "Assembly-CSharp.experimental.dll";
    private static readonly string[] HelperFrameworkReferenceFileNames =
    [
        "ref-mscorlib.dll",
        "ref-System.dll",
        "ref-System.Core.dll"
    ];

    private readonly AppPaths paths;
    private readonly ExperimentalFeatureBuildPlanService buildPlanService = new();
    private readonly ExperimentalFeaturePatchRunner patchRunner = new();
    private readonly HelperSourceGeneratorService helperSourceGenerator = new();
    private readonly AssemblyBaselinePatcher baselinePatcher = new();

    public ExperimentalAssemblyBuildService(AppPaths paths)
    {
        this.paths = paths;
    }

    public bool WillBuildFromLocalConfig()
    {
        return buildPlanService.Create(paths.PatchingDir).HasEnabledTriggerFeature;
    }

    public bool BuildFromLocalConfig(GameInstallInfo installInfo, Logger logger)
    {
        var plan = buildPlanService.Create(paths.PatchingDir);
        if (!plan.HasEnabledTriggerFeature)
        {
            return false;
        }

        var enabledFeatureKeys = plan.EnabledTriggerEntries
            .Select(static entry => entry.Definition.Key)
            .ToArray();
        var csharpFeatureKeys = patchRunner.GetApplicableFeatureKeys(enabledFeatureKeys);

        if (plan.HasEnabledTriggerFeature)
        {
            logger.Info($"Feature config detected. Rebuilding Assembly-CSharp DLL for: {plan.DescribeEnabledTriggerFeatures()}");
            if (csharpFeatureKeys.Count > 0)
            {
                logger.Info($"C# patchers enabled for: {string.Join(", ", csharpFeatureKeys)}");
            }
        }
        else
        {
            logger.Info("Feature config detected. Rebuilding Assembly-CSharp DLL...");
        }

        // Generate helper source and compile it in-process
        helperSourceGenerator.Generate(paths.PatchingDir, installInfo.GameRoot);
        logger.Info("Generated helper source in-process (C# generator).");

        CompileHelperAssembly(installInfo, logger);

        // Create the experimental assembly baseline from the backup
        var backupAssemblyPath = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp-backup.dll");
        baselinePatcher.CreateExperimentalAssembly(backupAssemblyPath, paths.PatchingDir, installInfo.ManagedDirectoryPath, logger);

        ApplyCSharpFeaturePatchers(installInfo, csharpFeatureKeys, logger);

        if (enabledFeatureKeys.Contains("motion-blur", StringComparer.Ordinal))
        {
            DeployMotionBlurBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains("visual-enhancements", StringComparer.Ordinal))
        {
            DeployVisualEnhancementsBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains("nigel-sniper-visual", StringComparer.Ordinal))
        {
            DeployNigelWeaponBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains("ninja-turtle-skin", StringComparer.Ordinal))
        {
            DeployNinjaTurtleSkinBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains("vander-blue-skin", StringComparer.Ordinal))
        {
            DeployVanderBlueSkinBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains("hindu-yeti-skin", StringComparer.Ordinal))
        {
            DeployHinduYetiSkinBundle(installInfo, logger);
        }
        if (enabledFeatureKeys.Contains(
                "darklord-sweet-science-skin", StringComparer.Ordinal))
        {
            DeployDarklordSweetScienceSkinBundle(installInfo, logger);
        }
        return true;
    }

    private void DeployMotionBlurBundle(GameInstallInfo installInfo, Logger logger)
    {
        var sourcePath = Path.Combine(paths.PatchingDir, "motion-blur-windows.bundle");
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException($"Motion blur shader bundle was not found: {sourcePath}");
        }

        var destinationDirectory = Path.Combine(installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, "motion-blur-windows.bundle");
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed motion blur shader bundle to '{destinationPath}'.");
    }

    private void DeployVisualEnhancementsBundle(GameInstallInfo installInfo, Logger logger)
    {
        var sourcePath = Path.Combine(paths.PatchingDir, "visual-enhancements-windows.bundle");
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException($"Visual enhancement shader bundle was not found: {sourcePath}");
        var destinationDirectory = Path.Combine(installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, "visual-enhancements-windows.bundle");
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed visual enhancement shader bundle to '{destinationPath}'.");
    }

    private void DeployNigelWeaponBundle(GameInstallInfo installInfo, Logger logger)
    {
        var destinationDirectory = Path.Combine(installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        foreach (var fileName in new[]
                 {
                     "nigel-weapon-windows.bundle",
                     "nigel-replacement-model-windows.bundle"
                 })
        {
            var sourcePath = Path.Combine(paths.PatchingDir, fileName);
            if (!File.Exists(sourcePath))
                throw new InvalidOperationException($"Nigel weapon bundle was not found: {sourcePath}");
            var destinationPath = Path.Combine(destinationDirectory, fileName);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            logger.Info($"Deployed Nigel weapon bundle to '{destinationPath}'.");
        }
    }

    private void DeployNinjaTurtleSkinBundle(
        GameInstallInfo installInfo, Logger logger)
    {
        const string fileName = "ninja-turtle-skin-windows.bundle";
        var sourcePath = Path.Combine(paths.PatchingDir, fileName);
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException(
                $"Ninja Turtle skin bundle was not found: {sourcePath}");
        var destinationDirectory = Path.Combine(
            installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed Ninja Turtle skin bundle to '{destinationPath}'.");
    }

    private void DeployVanderBlueSkinBundle(
        GameInstallInfo installInfo, Logger logger)
    {
        const string fileName = "vander-blue-skin-windows.bundle";
        var sourcePath = Path.Combine(paths.PatchingDir, fileName);
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException(
                $"Vander Blue skin bundle was not found: {sourcePath}");
        var destinationDirectory = Path.Combine(
            installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed Vander Blue skin bundle to '{destinationPath}'.");
    }

    private void DeployHinduYetiSkinBundle(
        GameInstallInfo installInfo, Logger logger)
    {
        const string fileName = "hindu-yeti-skin-windows.bundle";
        var sourcePath = Path.Combine(paths.PatchingDir, fileName);
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException(
                $"Hindu Yeti skin bundle was not found: {sourcePath}");
        var destinationDirectory = Path.Combine(
            installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed Hindu Yeti skin bundle to '{destinationPath}'.");
    }

    private void DeployDarklordSweetScienceSkinBundle(
        GameInstallInfo installInfo, Logger logger)
    {
        const string fileName =
            "darklord-sweet-science-skin-windows.bundle";
        var sourcePath = Path.Combine(paths.PatchingDir, fileName);
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException(
                $"Darklord SS skin bundle was not found: {sourcePath}");
        var destinationDirectory = Path.Combine(
            installInfo.GameDataDirectoryPath, "CommunityFixes");
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        logger.Info($"Deployed Darklord SS skin bundle to '{destinationPath}'.");
    }

    private void CompileHelperAssembly(GameInstallInfo installInfo, Logger logger)
    {
        var helperSourcePath = Path.Combine(paths.PatchingDir, GeneratedHelperSourceFileName);
        if (!File.Exists(helperSourcePath))
        {
            throw new InvalidOperationException($"Generated helper source was not found: {helperSourcePath}");
        }

        var helperOutputPath = Path.Combine(paths.PatchingDir, HelperAssemblyFileName);
        var backupAssemblyPath = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp-backup.dll");
        var firstPassAssemblyPath = Path.Combine(installInfo.ManagedDirectoryPath, "Assembly-CSharp-firstpass.dll");
        var unityEnginePath = Path.Combine(installInfo.ManagedDirectoryPath, "UnityEngine.dll");
        var unityEngineUiPath = Path.Combine(installInfo.ManagedDirectoryPath, "UnityEngine.UI.dll");

        foreach (var requiredPath in new[] { backupAssemblyPath, firstPassAssemblyPath, unityEnginePath, unityEngineUiPath })
        {
            if (!File.Exists(requiredPath))
            {
                throw new InvalidOperationException($"Required helper compilation reference was not found: {requiredPath}");
            }
        }

        var metadataReferences = new List<MetadataReference>();
        foreach (var referenceFileName in HelperFrameworkReferenceFileNames)
        {
            var referencePath = Path.Combine(paths.PatchingDir, referenceFileName);
            if (!File.Exists(referencePath))
            {
                throw new InvalidOperationException($"Bundled framework reference was not found: {referencePath}");
            }

            metadataReferences.Add(MetadataReference.CreateFromFile(referencePath));
        }

        metadataReferences.Add(MetadataReference.CreateFromFile(unityEnginePath));
        metadataReferences.Add(MetadataReference.CreateFromFile(unityEngineUiPath));
        metadataReferences.Add(MetadataReference.CreateFromFile(firstPassAssemblyPath));
        metadataReferences.Add(MetadataReference.CreateFromFile(backupAssemblyPath));

        var sourceText = File.ReadAllText(helperSourcePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            sourceText,
            new CSharpParseOptions(languageVersion: LanguageVersion.Latest),
            path: helperSourcePath);

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(helperOutputPath),
            syntaxTrees: new[] { syntaxTree },
            references: metadataReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                deterministic: true));

        using var outputStream = File.Create(helperOutputPath);
        var emitResult = compilation.Emit(outputStream);
        if (emitResult.Success)
        {
            outputStream.Flush();
            logger.Info("Compiled helper assembly in-process.");
            if (!File.Exists(helperOutputPath))
            {
                throw new InvalidOperationException($"Feature bundle helper assembly was not created: {helperOutputPath}");
            }

            return;
        }

        outputStream.Dispose();
        File.Delete(helperOutputPath);

        foreach (var diagnostic in emitResult.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error))
        {
            logger.Info(diagnostic.ToString());
        }

        throw new InvalidOperationException("Feature bundle helper compilation failed.");
    }

    private void ApplyCSharpFeaturePatchers(GameInstallInfo installInfo, IReadOnlyList<string> featureKeys, Logger logger)
    {
        var targetAssemblyPath = Path.Combine(paths.PatchingDir, ExperimentalAssemblyFileName);
        if (!File.Exists(targetAssemblyPath))
        {
            throw new InvalidOperationException($"Experimental assembly was not found for C# patchers: {targetAssemblyPath}");
        }

        var helperAssemblyPath = Path.Combine(paths.PatchingDir, HelperAssemblyFileName);
        if (!File.Exists(helperAssemblyPath))
        {
            throw new InvalidOperationException($"Helper assembly was not found for C# patchers: {helperAssemblyPath}");
        }

        patchRunner.ApplyToAssembly(
            targetAssemblyPath,
            helperAssemblyPath,
            featureKeys,
            logger,
            installInfo.ManagedDirectoryPath,
            paths.PatchingDir);
    }

}
