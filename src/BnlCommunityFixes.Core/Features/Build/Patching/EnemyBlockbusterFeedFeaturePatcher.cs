using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>Adds the stock blockbuster-collected feed entry for enemy collectors.</summary>
public sealed class EnemyBlockbusterFeedFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "enemy-blockbuster-feed";

    public void Apply(ExperimentalPatchContext context)
    {
        var notifications = context.TargetModule.Types.FirstOrDefault(
                                static type => type.Name == "ZoneNotifications")
                            ?? throw new InvalidOperationException("ZoneNotifications type not found.");
        var method = notifications.Methods.FirstOrDefault(static candidate =>
            candidate.Name == "OnGlobalUnitTakePickup" && candidate.HasBody && candidate.Parameters.Count == 1)
                     ?? throw new InvalidOperationException("ZoneNotifications.OnGlobalUnitTakePickup not found.");
        var instructions = method.Body.Instructions;
        var unitField = FindField(instructions, "GlobalUnitTakePickupArgs", "unit");
        var pickupKeyField = FindField(instructions, "GlobalUnitTakePickupArgs", "pickupKey");
        var teamField = FindField(instructions, "Unit", "Team");
        var isMy = FindMethod(instructions, null, "IsMy");
        var catalogueGetter = FindMethod(instructions, "Singleton`1", "get_Instance");
        var getCard = FindMethod(instructions, "Catalogue", "GetCard");
        var labelsGetter = FindMethod(instructions, "CardUnit", "get_Labels");
        var contains = FindMethod(instructions, "List`1", "Contains");
        var playerIdField = FindField(instructions, "Unit", "PlayerId");
        var sendCommon = notifications.Methods.FirstOrDefault(static candidate =>
                             candidate.Name == "SendCommon" && candidate.Parameters.Count == 3)
                         ?? throw new InvalidOperationException("ZoneNotifications.SendCommon not found.");
        var notificationType = context.TargetModule.Types.FirstOrDefault(
                                   static type => type.Name == "GlobalNotificationCommonType")
                               ?? throw new InvalidOperationException("GlobalNotificationCommonType not found.");
        var unitLabelType = context.TargetModule.Types.FirstOrDefault(static type => type.Name == "UnitLabel")
                            ?? throw new InvalidOperationException("UnitLabel type not found.");
        var supplyBlockbusterValue = unitLabelType.Fields.FirstOrDefault(static field =>
                                         field.Name == "SupplyBlockbuster")?.Constant
                                     ?? throw new InvalidOperationException("SupplyBlockbuster label enum value not found.");
        var blockbusterValue = notificationType.Fields.FirstOrDefault(static field =>
                                   field.Name == "FriendlyHasCollectedBlockbuster")?.Constant
                               ?? throw new InvalidOperationException("Blockbuster notification enum value not found.");

        var firstOriginal = instructions[0];
        var il = method.Body.GetILProcessor();
        var cardVariable = new VariableDefinition(context.TargetModule.ImportReference(labelsGetter.DeclaringType));
        method.Body.Variables.Add(cardVariable);
        method.Body.InitLocals = true;

        // Friendly collectors retain the untouched stock path. For enemies, emit only
        // the blockbuster notification; ordinary resource pickups remain hidden.
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldfld, context.TargetModule.ImportReference(unitField)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldfld, context.TargetModule.ImportReference(teamField)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Call, context.TargetModule.ImportReference(isMy)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Brtrue, firstOriginal));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Call, context.TargetModule.ImportReference(catalogueGetter)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldfld, context.TargetModule.ImportReference(pickupKeyField)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Callvirt, context.TargetModule.ImportReference(getCard)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Stloc, cardVariable));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldloc, cardVariable));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Callvirt, context.TargetModule.ImportReference(labelsGetter)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldc_I4, Convert.ToInt32(supplyBlockbusterValue)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Callvirt, context.TargetModule.ImportReference(contains)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Brfalse, firstOriginal));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldc_I4, Convert.ToInt32(blockbusterValue)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldfld, context.TargetModule.ImportReference(unitField)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldfld, context.TargetModule.ImportReference(playerIdField)));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(firstOriginal, il.Create(OpCodes.Call, context.TargetModule.ImportReference(sendCommon)));
    }

    private static FieldReference FindField(IEnumerable<Instruction> instructions, string typeName, string fieldName) =>
        instructions.Select(static instruction => instruction.Operand).OfType<FieldReference>()
            .FirstOrDefault(field => field.DeclaringType.Name == typeName && field.Name == fieldName)
        ?? throw new InvalidOperationException($"{typeName}.{fieldName} field reference not found.");

    private static MethodReference FindMethod(
        IEnumerable<Instruction> instructions, string? typeName, string methodName) =>
        instructions.Select(static instruction => instruction.Operand).OfType<MethodReference>()
            .FirstOrDefault(method => method.Name == methodName &&
                                      (typeName is null || method.DeclaringType.Name == typeName))
        ?? throw new InvalidOperationException($"{typeName ?? "any"}.{methodName} method reference not found.");
}
