using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class MeshReplacerFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "mesh-replacer";

    public void Apply(ExperimentalPatchContext context)
    {
        var config        = PatcherConfigReader.Read(context.PatchingDir, "experimental-mesh-replacer-config.json");
        var meshes        = PatcherConfigReader.GetStringDict(config, "meshes");
        var transformFixes= PatcherConfigReader.GetNestedObjectDict(config, "transformFixes");

        if (meshes.Count == 0) return;

        var managerType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.MeshReplacerManager")
            ?? throw new InvalidOperationException("MeshReplacerManager not found in helper assembly.");

        var beginBootstrap      = Imp(context, managerType, "BeginBootstrap");
        var registerMesh        = Imp(context, managerType, "RegisterReplacement");
        var onGameObject        = Imp(context, managerType, "OnGameObjectInstantiated");
        var registerTransformFix= Imp(context, managerType, "RegisterTransformFix");

        // Resolve UnityEngine.Component.get_gameObject from the managed dir
        var getGameObject = ResolveGetGameObject(context);

        // MainMenu.Start — bootstrap + register + transform fixes
        InjectBootstrap(context, "MainMenu", "Start", meshes, transformFixes,
            beginBootstrap, registerMesh, registerTransformFix, null, null);

        // GearModel.Awake — bootstrap + register + transform fixes + OnGameObjectInstantiated(this.gameObject)
        InjectBootstrap(context, "GearModel", "Awake", meshes, transformFixes,
            beginBootstrap, registerMesh, registerTransformFix, onGameObject, getGameObject);

        // UnitView.UpdateUnit — bootstrap + register + transform fixes + OnGameObjectInstantiated(this.gameObject)
        InjectBootstrap(context, "UnitView", "UpdateUnit", meshes, transformFixes,
            beginBootstrap, registerMesh, registerTransformFix, onGameObject, getGameObject);
    }

    private static void InjectBootstrap(
        ExperimentalPatchContext context,
        string typeName,
        string methodName,
        IReadOnlyDictionary<string, string> meshes,
        IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> transformFixes,
        MethodReference beginBootstrap,
        MethodReference registerMesh,
        MethodReference registerTransformFix,
        MethodReference? onGameObject,
        MethodReference? getGameObject)
    {
        var type = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
        if (method is null) return;

        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        var cont = il.Create(OpCodes.Nop);

        if (HasMeshRuntimeCall(method))
        {
            return;
        }

        // if (!BeginBootstrap()) goto cont;
        il.InsertBefore(first, il.Create(OpCodes.Call, beginBootstrap));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse, cont));

        // RegisterReplacement(meshName, fileName) for each mesh
        foreach (var kvp in meshes)
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Key));
            il.InsertBefore(first, il.Create(OpCodes.Ldstr, kvp.Value));
            il.InsertBefore(first, il.Create(OpCodes.Call, registerMesh));
        }

        // RegisterTransformFix(goName, path, px, py, pz, rx, ry, rz, rw, sx, sy, sz) for each fix
        foreach (var (goName, fixes) in transformFixes)
        {
            foreach (var fix in fixes)
            {
                var path = PatcherConfigReader.GetString(fix, "path", string.Empty);
                var pos  = PatcherConfigReader.GetFloatArray(fix, "position", 3);
                var rot  = PatcherConfigReader.GetFloatArray(fix, "rotation", 4);
                var scl  = PatcherConfigReader.GetFloatArray(fix, "scale",    3);

                il.InsertBefore(first, il.Create(OpCodes.Ldstr,  goName));
                il.InsertBefore(first, il.Create(OpCodes.Ldstr,  path));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, pos[0]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, pos[1]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, pos[2]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, rot[0]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, rot[1]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, rot[2]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, rot[3]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, scl[0]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, scl[1]));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_R4, scl[2]));
                il.InsertBefore(first, il.Create(OpCodes.Call, registerTransformFix));
            }
        }

        il.InsertBefore(first, cont);

        // After cont: OnGameObjectInstantiated(this.gameObject)
        if (onGameObject is not null && getGameObject is not null)
        {
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, getGameObject));
            il.InsertBefore(first, il.Create(OpCodes.Call, onGameObject));
        }
    }

    private static MethodReference? ResolveGetGameObject(ExperimentalPatchContext context)
    {
        // Find UnityEngine.Component.get_gameObject in the target module's references
        foreach (var assembly in context.TargetModule.AssemblyResolver?.Resolve(new AssemblyNameReference("UnityEngine", null)) is { } ue
            ? new[] { ue } : Array.Empty<AssemblyDefinition>())
        {
            var componentType = assembly.MainModule.Types.FirstOrDefault(static t => t.FullName == "UnityEngine.Component");
            var getGo = componentType?.Methods.FirstOrDefault(static m => m.Name == "get_gameObject");
            if (getGo is not null)
            {
                return context.TargetModule.ImportReference(getGo);
            }
        }

        // Fallback: search in target module directly
        var component = context.TargetModule.Types.FirstOrDefault(static t => t.FullName == "UnityEngine.Component");
        var method = component?.Methods.FirstOrDefault(static m => m.Name == "get_gameObject");
        return method is null ? null : context.TargetModule.ImportReference(method);
    }

    private static MethodReference Imp(ExperimentalPatchContext context, TypeDefinition type, string name) =>
        context.TargetModule.ImportReference(
            type.Methods.FirstOrDefault(m => m.Name == name)
            ?? throw new InvalidOperationException($"{type.Name}.{name} not found."));

    private static bool HasMeshRuntimeCall(MethodDefinition method)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.DeclaringType.Name == "MeshReplacerManager");
    }
}
