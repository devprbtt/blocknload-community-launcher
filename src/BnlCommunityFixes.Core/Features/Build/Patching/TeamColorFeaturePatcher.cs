using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

public sealed class TeamColorFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "team-colors";

    public void Apply(ExperimentalPatchContext context)
    {
        var helperRuntimeType = context.HelperModule.Types.FirstOrDefault(static type => type.FullName == "BnlCommunityFixes.TeamColorRuntime")
            ?? throw new InvalidOperationException("TeamColorRuntime type not found in helper assembly.");

        var replacementMethods = new Dictionary<string, MethodReference>(StringComparer.Ordinal)
        {
            ["TeamFriendly"] = ImportHelperMethod(context, helperRuntimeType, "GetGuiFriendlyColor"),
            ["TeamEnemy"] = ImportHelperMethod(context, helperRuntimeType, "GetGuiEnemyColor"),
            ["BackgroundTeamFriendly"] = ImportHelperMethod(context, helperRuntimeType, "GetGuiBackgroundFriendlyColor"),
            ["BackgroundTeamEnemy"] = ImportHelperMethod(context, helperRuntimeType, "GetGuiBackgroundEnemyColor"),
            ["CommonTeamFriendly"] = ImportHelperMethod(context, helperRuntimeType, "GetObjectCommonFriendlyColor"),
            ["CommonTeamEnemy"] = ImportHelperMethod(context, helperRuntimeType, "GetObjectCommonEnemyColor"),
            ["ForceFieldTeamFriendly"] = ImportHelperMethod(context, helperRuntimeType, "GetForceFieldFriendlyColor"),
            ["ForceFieldTeamEnemy"] = ImportHelperMethod(context, helperRuntimeType, "GetForceFieldEnemyColor"),
            ["IceTeamFriendly"] = ImportHelperMethod(context, helperRuntimeType, "GetIceFriendlyColor"),
            ["IceTeamEnemy"] = ImportHelperMethod(context, helperRuntimeType, "GetIceEnemyColor")
        };

        var teamColorContainerType = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "TeamColorContainer")
            ?? throw new InvalidOperationException("TeamColorContainer type not found.");
        var guiField = teamColorContainerType.Fields.FirstOrDefault(static field => field.Name == "Gui")
            ?? throw new InvalidOperationException("TeamColorContainer.Gui field not found.");
        var objectsField = teamColorContainerType.Fields.FirstOrDefault(static field => field.Name == "Objects")
            ?? throw new InvalidOperationException("TeamColorContainer.Objects field not found.");

        var guiFieldToken = guiField.MetadataToken.ToInt32();
        var objectsFieldToken = objectsField.MetadataToken.ToInt32();
        var colorFieldTokens = new Dictionary<int, string>();
        foreach (var nestedType in teamColorContainerType.NestedTypes)
        {
            foreach (var field in nestedType.Fields)
            {
                if (replacementMethods.ContainsKey(field.Name))
                {
                    colorFieldTokens[field.MetadataToken.ToInt32()] = field.Name;
                }
            }
        }

        foreach (var type in GetAllTypes(context.TargetModule))
        {
            foreach (var method in type.Methods.Where(static method => method.HasBody))
            {
                var instructions = method.Body.Instructions;
                for (var i = 2; i < instructions.Count; i++)
                {
                    var current = instructions[i];
                    if (current.OpCode.Code != Code.Ldfld)
                    {
                        continue;
                    }

                    if (current.Operand is not FieldReference currentFieldReference)
                    {
                        continue;
                    }

                    var currentField = currentFieldReference.Resolve();
                    if (currentField is null)
                    {
                        continue;
                    }

                    if (!colorFieldTokens.TryGetValue(currentField.MetadataToken.ToInt32(), out var replacementName))
                    {
                        continue;
                    }

                    var previousFieldInstruction = instructions[i - 1];
                    var previousCallInstruction = instructions[i - 2];
                    if (previousFieldInstruction.OpCode.Code != Code.Ldfld || previousCallInstruction.OpCode.Code != Code.Call)
                    {
                        continue;
                    }

                    if (previousFieldInstruction.Operand is not FieldReference rootFieldReference ||
                        previousCallInstruction.Operand is not MethodReference callMethodReference)
                    {
                        continue;
                    }

                    if (!string.Equals(callMethodReference.Name, "get_Instance", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var rootField = rootFieldReference.Resolve();
                    if (rootField is null)
                    {
                        continue;
                    }

                    var rootToken = rootField.MetadataToken.ToInt32();
                    if (rootToken != guiFieldToken && rootToken != objectsFieldToken)
                    {
                        continue;
                    }

                    previousCallInstruction.OpCode = OpCodes.Call;
                    previousCallInstruction.Operand = replacementMethods[replacementName];
                    previousFieldInstruction.OpCode = OpCodes.Nop;
                    previousFieldInstruction.Operand = null;
                    current.OpCode = OpCodes.Nop;
                    current.Operand = null;
                }
            }
        }

        var blockGenericCloneType = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "BlockGenericClone");
        var buildMethod = blockGenericCloneType?.Methods.FirstOrDefault(static method => method.Name == "Build" && method.HasBody);
        if (buildMethod is null)
        {
            return;
        }

        foreach (var instruction in buildMethod.Body.Instructions)
        {
            if (instruction.OpCode.Code != Code.Call || instruction.Operand is not MethodReference operand)
            {
                continue;
            }

            if (string.Equals(operand.Name, "get_blue", StringComparison.Ordinal))
            {
                instruction.Operand = replacementMethods["CommonTeamFriendly"];
            }
            else if (string.Equals(operand.Name, "get_red", StringComparison.Ordinal))
            {
                instruction.Operand = replacementMethods["CommonTeamEnemy"];
            }
        }
    }

    private static MethodReference ImportHelperMethod(ExperimentalPatchContext context, TypeDefinition helperRuntimeType, string methodName)
    {
        var helperMethod = helperRuntimeType.Methods.FirstOrDefault(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"TeamColorRuntime.{methodName} not found.");
        return context.TargetModule.ImportReference(helperMethod);
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            foreach (var nested in GetAllTypes(type))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(TypeDefinition type)
    {
        yield return type;

        foreach (var nestedType in type.NestedTypes)
        {
            foreach (var nested in GetAllTypes(nestedType))
            {
                yield return nested;
            }
        }
    }
}
