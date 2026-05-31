using Mono.Cecil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class ExperimentalPatchContext
{
    public ExperimentalPatchContext(AssemblyDefinition targetAssembly, AssemblyDefinition helperAssembly, string patchingDir)
    {
        TargetAssembly = targetAssembly;
        TargetModule = targetAssembly.MainModule;
        HelperAssembly = helperAssembly;
        HelperModule = helperAssembly.MainModule;
        PatchingDir = patchingDir;
    }

    public AssemblyDefinition TargetAssembly { get; }

    public ModuleDefinition TargetModule { get; }

    public AssemblyDefinition HelperAssembly { get; }

    public ModuleDefinition HelperModule { get; }

    /// <summary>Directory containing patching config JSON files and the built assemblies.</summary>
    public string PatchingDir { get; }
}
