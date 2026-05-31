using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class FovFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "fov";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "fov-config.json");
        var fov          = PatcherConfigReader.GetFloat(config, "fov", 120f);
        var weaponFov    = PatcherConfigReader.GetFloat(config, "weapon_model_fov", 30f);
        var adsMult      = PatcherConfigReader.GetFloat(config, "ads_sensitivity_multiplier", 1f);

        PatchCameraFovUpdate(context, fov);
        PatchCameraArmsUpdate(context, weaponFov);
        PatchAdsScale(context, adsMult);
    }

    // CameraFov.Update — non-ADS branch reads FOV from Settings::get_CameraFov.
    // Pattern: call get_Instance / callvirt get_CameraFov / call op_Implicit / callvirt set_fieldOfView
    // We replace: get_Instance → ldc.r4 fov, get_CameraFov → nop, op_Implicit → nop
    private static void PatchCameraFovUpdate(ExperimentalPatchContext context, float fov)
    {
        var cameraFovType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "CameraFov");
        var update = cameraFovType?.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (update is null) return;

        // Find the callvirt get_CameraFov that is NOT in the ADS branch (ADS uses get_CameraFov from ToolAiming)
        // The non-ADS one reads from Settings, not from ToolAiming
        foreach (var instr in update.Body.Instructions.ToArray())
        {
            if (instr.OpCode.Code != Code.Callvirt) continue;
            if (instr.Operand is not MethodReference mr) continue;
            if (mr.Name != "get_CameraFov") continue;
            if (mr.DeclaringType.Name != "Settings") continue; // only the Settings one, not ToolAiming

            // instr is: callvirt Settings::get_CameraFov
            // prev should be: callvirt Settings::get_Instance (or call Singleton::get_Instance)
            // next should be: call SettingFloat::op_Implicit
            var getInstanceInstr = instr.Previous;
            var opImplicitInstr  = instr.Next;

            if (getInstanceInstr is null || opImplicitInstr is null) continue;

            // Replace get_Instance call with ldc.r4 fov
            getInstanceInstr.OpCode  = OpCodes.Ldc_R4;
            getInstanceInstr.Operand = fov;

            // Nop the get_CameraFov call
            instr.OpCode  = OpCodes.Nop;
            instr.Operand = null;

            // Nop the op_Implicit call
            opImplicitInstr.OpCode  = OpCodes.Nop;
            opImplicitInstr.Operand = null;
        }
    }

    // CameraArms.Update — patch ALL set_fieldOfView calls that feed the weapon camera FOV:
    // 1. Ramp with gear card: get_Card / get_RenderFov / set_fieldOfView → replace get_RenderFov with ldc.r4 + nop
    // 2. Ramp without gear:   ldc.r4 <float> / set_fieldOfView            → replace the float
    private static void PatchCameraArmsUpdate(ExperimentalPatchContext context, float weaponFov)
    {
        var cameraArmsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "CameraArms");
        var update = cameraArmsType?.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody);
        if (update is null) return;

        foreach (var instr in update.Body.Instructions.ToArray())
        {
            if (instr.OpCode.Code != Code.Callvirt) continue;
            if (instr.Operand is not MethodReference mr || mr.Name != "set_fieldOfView") continue;

            var prev = instr.Previous;
            if (prev is null) continue;

            if (prev.OpCode == OpCodes.Ldc_R4)
            {
                // Ramp 2: direct constant → replace value
                prev.Operand = weaponFov;
            }
            else if (prev.OpCode.Code == Code.Callvirt &&
                     prev.Operand is MethodReference prevMr &&
                     prevMr.Name == "get_RenderFov")
            {
                // Ramp 1: get_RenderFov → replace with ldc.r4 weaponFov, nop get_RenderFov
                prev.OpCode  = OpCodes.Ldc_R4;
                prev.Operand = weaponFov;
                // Also nop get_Card call before it (2 instructions back)
                var getCard = prev.Previous;
                if (getCard is not null && getCard.OpCode.Code == Code.Callvirt &&
                    getCard.Operand is MethodReference gcMr && gcMr.Name == "get_Card")
                {
                    getCard.OpCode  = OpCodes.Nop;
                    getCard.Operand = null;
                    // And ldloc before get_Card
                    var ldloc = getCard.Previous;
                    if (ldloc is not null && (ldloc.OpCode == OpCodes.Ldloc_0 ||
                        ldloc.OpCode == OpCodes.Ldloc_1 || ldloc.OpCode == OpCodes.Ldloc_2 ||
                        ldloc.OpCode == OpCodes.Ldloc_3 || ldloc.OpCode == OpCodes.Ldloc_S))
                    {
                        ldloc.OpCode  = OpCodes.Nop;
                        ldloc.Operand = null;
                    }
                }
            }
        }
    }

    // MouseLook.RotateByMouse — ADS sensitivity scale.
    // The ADS scale call is already present in the helper (AdsSensitivityRuntime::ApplyAdsScale).
    // The helper's static ctor configures the multiplier via RuntimeFeatureState.ConfigureFov.
    // No IL injection needed here — the helper reads the value at runtime from its own state.
    // We only need to ensure it is applied if not already present (it is injected by the PS1 still
    // for this block, but the helper configures itself via fov-config.json through the helper source).
    // This method is intentionally empty — the ADS scale value is baked into the helper source by the PS1.
    private static void PatchAdsScale(ExperimentalPatchContext context, float adsMult)
    {
        // ADS sensitivity is configured in the helper's static ctor (AdsSensitivityRuntime)
        // via RuntimeFeatureState.ConfigureFov(..., adsSensitivityMultiplier).
        // The IL injection of ldarg.0/ldfld unit/call ApplyAdsScale at the two mouse axis
        // multiplications is done by the ADS block in the PS1 using offsets 53 and 142.
        // We replicate it here by finding the two multiplication sites pattern:
        // ... / ldfld MouseLook::SensitivityX or SensitivityY / mul / ... / div / ret-ish
        // and insert ldarg.0 / ldfld unit / call ApplyAdsScale before the final mul of each axis.

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.AdsSensitivityRuntime");
        if (runtimeType is null) return;

        var applyAdsScale = runtimeType.Methods.FirstOrDefault(static m => m.Name == "ApplyAdsScale");
        if (applyAdsScale is null) return;

        var importedScale = context.TargetModule.ImportReference(applyAdsScale);

        var mouseLookType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MouseLook");
        var rotateByMouse = mouseLookType?.Methods.FirstOrDefault(static m => m.Name == "RotateByMouse" && m.HasBody);
        if (rotateByMouse is null) return;

        // Skip if already patched (ApplyAdsScale already present)
        if (rotateByMouse.Body.Instructions.Any(i =>
                i.Operand is MethodReference mr && mr.Name == "ApplyAdsScale"))
        {
            return;
        }

        var unitField = mouseLookType!.Fields.FirstOrDefault(static f => f.Name == "unit");
        if (unitField is null) return;
        var importedUnit = context.TargetModule.ImportReference(unitField);

        var il = rotateByMouse.Body.GetILProcessor();
        var instructions = rotateByMouse.Body.Instructions.ToArray();

        // Find the two `div` instructions that divide by CameraFov (preceded by get_fieldOfView)
        // Pattern: callvirt Camera::get_fieldOfView / ... / div
        // Insert before each `mul` that follows the `div`
        var divInstrs = instructions.Where(static i =>
            i.OpCode == OpCodes.Div &&
            FindBackwards(i, 5, static x => x.Operand is MethodReference mr && mr.Name == "get_fieldOfView") is not null)
            .ToArray();

        foreach (var div in divInstrs)
        {
            // The next mul after div is where we insert the ADS scale
            var mulInstr = div.Next;
            while (mulInstr is not null && mulInstr.OpCode != OpCodes.Mul) mulInstr = mulInstr.Next;
            if (mulInstr is null) continue;

            // Insert: ldarg.0 / ldfld unit / call ApplyAdsScale — before the mul
            il.InsertBefore(mulInstr, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(mulInstr, il.Create(OpCodes.Ldfld, importedUnit));
            il.InsertBefore(mulInstr, il.Create(OpCodes.Call, importedScale));
        }
    }

    private static Instruction? FindBackwards(Instruction start, int maxSteps, Func<Instruction, bool> predicate)
    {
        var cur = start.Previous;
        var steps = 0;
        while (cur is not null && steps < maxSteps)
        {
            if (predicate(cur)) return cur;
            cur = cur.Previous;
            steps++;
        }
        return null;
    }
}
