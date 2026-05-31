using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Unconditional — patches GuiSpriteResources.GetShopImage to allow texture replacement overrides.</summary>
public sealed class ShopImagePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "__always__shop-image";

    public void Apply(ExperimentalPatchContext context)
    {
        var helperTextureType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.TextureReplacementBootstrapper");
        if (helperTextureType is null) return;

        var spriteResourcesType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiSpriteResources");
        if (spriteResourcesType is null) return;

        var getShopImage = spriteResourcesType.Methods.FirstOrDefault(static m => m.Name == "GetShopImage" && m.HasBody);
        var getOverrideMethod = helperTextureType.Methods.FirstOrDefault(static m => m.Name == "GetShopImageOverride");
        if (getShopImage is null || getOverrideMethod is null) return;
        if (getShopImage.Body.Instructions.Any(i =>
                (i.OpCode.Code == Code.Call || i.OpCode.Code == Code.Callvirt) &&
                i.Operand is MethodReference mr &&
                mr.Name == "GetShopImageOverride" &&
                mr.DeclaringType.Name == "TextureReplacementBootstrapper"))
        {
            return;
        }

        var importedOverride = context.TargetModule.ImportReference(getOverrideMethod);
        var spriteType = getShopImage.ReturnType;
        var il = getShopImage.Body.GetILProcessor();
        var firstInstr = getShopImage.Body.Instructions[0];

        // Add local variable for override result
        var localVar = new Mono.Cecil.Cil.VariableDefinition(spriteType);
        getShopImage.Body.Variables.Add(localVar);
        getShopImage.Body.InitLocals = true;

        // Prefix: ldarg.0 → call override → stloc → ldloc → brfalse skip → ldloc → ret → skip:nop
        var nopSkip = il.Create(OpCodes.Nop);
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, importedOverride));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Stloc, localVar));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ldloc, localVar));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Brfalse_S, nopSkip));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ldloc, localVar));
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ret));
        il.InsertBefore(firstInstr, nopSkip);
    }
}
