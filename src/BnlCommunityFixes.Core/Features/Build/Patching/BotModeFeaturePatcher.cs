using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BnlCommunityFixes.Core.Features.Build.Patching;

/// <summary>
/// Patches the game to support an offline bot / practice mode.
///
/// Four injection points:
///   1. LoginLogic.DoLogin()         — bypass Steam/server login when bot mode is on.
///   2. SceneManager.ServerLoadZone  — intercept match start so the runtime driver can
///                                     spin up a local fake match instead.
///   3. ZoneServiceListener.Start()  — register the listener instance with the runtime so
///                                     it can emit synthetic server events.
///   4. ZoneManager.Update()         — tick the bot AI driver every frame.
/// </summary>
public sealed class BotModeFeaturePatcher : IExperimentalFeaturePatcher
{
    public string FeatureKey => "bot-mode";

    public void Apply(ExperimentalPatchContext context)
    {
        var config = PatcherConfigReader.Read(context.PatchingDir, "experimental-bot-mode-config.json");
        if (!PatcherConfigReader.GetBool(config, "enabled", false))
        {
            return;
        }

        var runtimeType = context.HelperModule.Types.FirstOrDefault(static t => t.FullName == "BnlCommunityFixes.BotModeRuntime")
            ?? throw new InvalidOperationException("BotModeRuntime not found in helper assembly.");

        PatchLoginLogic(context, runtimeType);
        PatchSceneManagerServerLoadZone(context, runtimeType);
        PatchZoneServiceListenerStart(context, runtimeType);
        PatchZoneManagerUpdate(context, runtimeType);
        PatchGuiRelogin(context, runtimeType);
        PatchCustomGameDataCreateGame(context, runtimeType);
        PatchSceneManagerIsTutorial(context, runtimeType);
        PatchSceneManagerIsTimeTrial(context, runtimeType);
        PatchZoneDataMatchCard(context, runtimeType);
        PatchZoneDataGameModeCard(context, runtimeType);
        PatchMediatorLoader(context, runtimeType);
        PatchServiceSceneEnterScene(context, runtimeType);
        PatchServiceZoneZoneReady(context, runtimeType);
        PatchServiceZoneUnitMove(context, runtimeType);
        PatchServiceZoneFallHit(context, runtimeType);
        PatchNetworkDispatcherServiceZone(context, runtimeType);
        PatchIgorServiceBeginSend(context, runtimeType);
        PatchIgorServiceSend(context, runtimeType);
        PatchServiceZoneRpcs(context, runtimeType);
        PatchServiceZoneCancelBuild(context, runtimeType);
        PatchServiceZoneEndChannel(context, runtimeType);
        PatchServiceZoneCast(context, runtimeType);
        PatchServiceZoneHit(context, runtimeType);
        PatchGuiPlayerInfoUpdate(context, runtimeType);
        // PatchBlockMaterialsAccessors removed: BlockMaterials now loads via FallbackLoadPrefab → FindObjectsOfTypeAll
        PatchMapSpreadLightCreate(context, runtimeType);
        PatchMapSimpleLightCreate(context, runtimeType);
        PatchMainMenuStatisticsUpdate(context, runtimeType);
        PatchGuiHelpScreenStart(context, runtimeType);
        PatchUiChatStart(context, runtimeType);
        PatchSceneLoaderDataBeginProbe(context, runtimeType);
        PatchOnLevelWasLoadedProbe(context, runtimeType);
        PatchLoadLevelLoaderProbe(context, runtimeType);
    }

    // ---------------------------------------------------------------------------
    // Patch 1: LoginLogic.DoLogin()
    //
    // Inject at the very start of DoLogin(). If BotModeRuntime.ShouldBypassLogin()
    // returns true, skip the entire method (avoids Steam init + server connection).
    // ---------------------------------------------------------------------------
    private static void PatchLoginLogic(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldBypass = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldBypassLogin" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldBypassLogin not found."));

        var onLoginBypassed = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnLoginBypassed" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.OnLoginBypassed not found."));

        var loginLogicType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "LoginLogic");
        if (loginLogicType is null)
        {
            return;
        }

        var doLoginMethod = loginLogicType.Methods.FirstOrDefault(static m => m.Name == "DoLogin" && m.HasBody);
        if (doLoginMethod is null || MethodCalls(doLoginMethod, "ShouldBypassLogin", "BotModeRuntime"))
        {
            return;
        }

        var first = doLoginMethod.Body.Instructions.First();
        var il = doLoginMethod.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        // if (!BotModeRuntime.ShouldBypassLogin()) goto continueOriginal;
        // BotModeRuntime.OnLoginBypassed();
        // return;
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldBypass));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Call, onLoginBypassed));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // ---------------------------------------------------------------------------
    // Patch 2: SceneManager.ServerLoadZone(SceneZone scene)
    //
    // Inject at the start. If BotModeRuntime.TryInterceptLoadZone(scene) returns
    // true the runtime handles the match setup — skip the original method.
    // ---------------------------------------------------------------------------
    private static void PatchSceneManagerServerLoadZone(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var tryIntercept = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "TryInterceptLoadZone" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.TryInterceptLoadZone not found."));

        var sceneManagerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneManager");
        if (sceneManagerType is null)
        {
            return;
        }

        var serverLoadZone = sceneManagerType.Methods.FirstOrDefault(
            static m => m.Name == "ServerLoadZone" && m.HasBody && m.Parameters.Count == 1);
        if (serverLoadZone is null || MethodCalls(serverLoadZone, "TryInterceptLoadZone", "BotModeRuntime"))
        {
            return;
        }

        var first = serverLoadZone.Body.Instructions.First();
        var il = serverLoadZone.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        // if (!BotModeRuntime.TryInterceptLoadZone(scene)) goto continueOriginal;
        // return;
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, tryIntercept));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // ---------------------------------------------------------------------------
    // Patch 3: ZoneServiceListener.Start()
    //
    // Inject before the method's Ret. Calls BotModeRuntime.RegisterListener(this)
    // so the runtime holds a reference to inject synthetic events.
    // ---------------------------------------------------------------------------
    private static void PatchZoneServiceListenerStart(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var registerListener = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "RegisterListener" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.RegisterListener not found."));

        var probe = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ProbeZoneListenerStart" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ProbeZoneListenerStart not found."));

        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var listenerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ZoneServiceListener");
        if (listenerType is null) return;

        // Patch Start(): add probe log, skip NetworkDispatcher line offline, call RegisterListener
        var startMethod = listenerType.Methods.FirstOrDefault(static m => m.Name == "Start" && m.HasBody && m.Parameters.Count == 0);
        if (startMethod is not null && !MethodCalls(startMethod, "RegisterListener", "BotModeRuntime"))
        {
            var il = startMethod.Body.GetILProcessor();
            var first = startMethod.Body.Instructions.First();

            // Always log that Start() was called
            il.InsertBefore(first, il.Create(OpCodes.Call, probe));

            // if (ShouldSkipNetworkSend()) { RegisterListener(this); return; }
            var afterGuard = il.Create(OpCodes.Nop);
            il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
            il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, afterGuard));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, registerListener));
            il.InsertBefore(first, il.Create(OpCodes.Ret));
            il.InsertBefore(first, afterGuard);

            // Online path: inject RegisterListener before each ret
            foreach (var ret in startMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
            {
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Call, registerListener));
            }
        }

        // Also patch Initialize() (called from Singleton.Awake) as a fallback —
        // ZoneServiceListener doesn't override it, so we add it to the base Singleton type
        // via the listenerType's Awake override if present.
        var awakeMethod = listenerType.Methods.FirstOrDefault(static m => m.Name == "Awake" && m.HasBody && m.Parameters.Count == 0);
        if (awakeMethod is not null && !MethodCalls(awakeMethod, "RegisterListener", "BotModeRuntime"))
        {
            var il = awakeMethod.Body.GetILProcessor();
            foreach (var ret in awakeMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
            {
                il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Call, registerListener));
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Patch 4: ZoneManager.Update()
    //
    // Inject before the method's Ret. Calls BotModeRuntime.Tick() each frame so
    // the local match driver and bot AI can advance their state.
    // ---------------------------------------------------------------------------
    private static void PatchZoneManagerUpdate(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var tick = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "Tick" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.Tick not found."));

        var zoneManagerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ZoneManager");
        if (zoneManagerType is null)
        {
            return;
        }

        var updateMethod = zoneManagerType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody && m.Parameters.Count == 0);
        if (updateMethod is null || MethodCalls(updateMethod, "Tick", "BotModeRuntime"))
        {
            return;
        }

        var il = updateMethod.Body.GetILProcessor();
        foreach (var ret in updateMethod.Body.Instructions.Where(static i => i.OpCode == OpCodes.Ret).ToArray())
        {
            il.InsertBefore(ret, il.Create(OpCodes.Call, tick));
        }
    }

    // ---------------------------------------------------------------------------
    // Patch 6: CustomGameData.CreateGame(string, string)
    //
    // Normally sends CreateCustomGame to the matchmaking server.
    // In offline mode, intercept and trigger a local Zone load instead.
    // ---------------------------------------------------------------------------
    private static void PatchCustomGameDataCreateGame(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var tryCreate = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "TryCreateOfflineGame" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.TryCreateOfflineGame not found."));

        var customGameType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "CustomGameData");
        if (customGameType is null) return;

        var createMethod = customGameType.Methods.FirstOrDefault(
            static m => m.Name == "CreateGame" && m.HasBody && m.Parameters.Count == 2);
        if (createMethod is null || MethodCalls(createMethod, "TryCreateOfflineGame", "BotModeRuntime")) return;

        var first = createMethod.Body.Instructions.First();
        var il = createMethod.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, tryCreate));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // ---------------------------------------------------------------------------
    // Patch 10+11: ServiceScene.EnterScene() / ServiceZone.ZoneReady()
    //
    // Both send packets to the game server (which doesn't exist offline).
    // The crash is a NullReferenceException because NetworkDispatcher.Instance is null.
    // Patch the send methods themselves to be no-ops when ShouldSkipNetworkSend().
    // ---------------------------------------------------------------------------
    private static void PatchServiceSceneEnterScene(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchCoroutineLoaderMoveNext(context, runtimeType, "EnterSceneLoader");

    private static void PatchServiceZoneZoneReady(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchZoneBlocksLoaderMoveNext(context, runtimeType);

    // ServiceZone.UnitMove / FallHit call NetworkServices.BeginSend which NPEs when offline.
    private static void PatchServiceZoneUnitMove(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "ServiceZone", "UnitMove", 3);

    private static void PatchServiceZoneFallHit(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "ServiceZone", "FallHit", 3);

    // NetworkDispatcher.get_ServiceZone / MediatorNetworkDispatcher.get_ServiceZone
    // Both return null offline (mediator never connected). Patch to return BotModeRuntime.GetOfflineServiceZone().
    private static void PatchNetworkDispatcherServiceZone(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var getOfflineSz = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetOfflineServiceZone" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.GetOfflineServiceZone not found."));

        foreach (var typeName in new[] { "NetworkDispatcher", "MediatorNetworkDispatcher" })
        {
            var t = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
            if (t is null) continue;
            // The getter property is compiled as get_ServiceZone()
            var getter = t.Methods.FirstOrDefault(m => m.Name == "get_ServiceZone" && m.HasBody);
            if (getter is null || MethodCalls(getter, "ShouldSkipNetworkSend", "BotModeRuntime")) continue;

            var first = getter.Body.Instructions.First();
            var il = getter.Body.GetILProcessor();
            var cont = il.Create(OpCodes.Nop);
            il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
            il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
            il.InsertBefore(first, il.Create(OpCodes.Call, getOfflineSz));
            il.InsertBefore(first, il.Create(OpCodes.Ret));
            il.InsertBefore(first, cont);
        }
    }

    // Igor.Service._BeginSend — returns this.sender.BeginSend(serviceId) which NPEs when sender is null.
    // Patch to return a dummy BinaryWriter backed by a MemoryStream when offline.
    private static void PatchIgorServiceBeginSend(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var getDummyWriter = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetDummyBinaryWriter" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.GetDummyBinaryWriter not found."));

        // Igor.Service lives in namespace Igor — find it
        var serviceType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "Service" && t.Namespace == "Igor")
            ?? context.TargetModule.Types.SelectMany(static t => t.NestedTypes).FirstOrDefault(static t => t.Name == "Service");
        if (serviceType is null) return;

        var method = serviceType.Methods.FirstOrDefault(static m => m.Name == "_BeginSend" && m.HasBody && m.Parameters.Count == 0);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Call, getDummyWriter));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // Igor.Service._Send — calls this.sender.EndSend() which NPEs when sender is null.
    private static void PatchIgorServiceSend(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var serviceType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "Service" && t.Namespace == "Igor")
            ?? context.TargetModule.Types.SelectMany(static t => t.NestedTypes).FirstOrDefault(static t => t.Name == "Service");
        if (serviceType is null) return;
        PatchInstanceMethodNoOpOffline(context, runtimeType, serviceType, "_Send", 0);
    }

    // ServiceZone RPC methods (SwitchGear, StartBuild, etc.) send to server then await _Success.
    // Offline: skip the send and immediately call _Success with accepted=true.
    private static void PatchServiceZoneRpcs(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone");
        if (serviceZoneType is null) return;

        var onReload = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineReload" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineReload not found."));
        var onStartChannel = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineStartChannel" && m.Parameters.Count == 2)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineStartChannel not found."));
        var onSwitchGear = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineSwitchGear" && m.Parameters.Count == 2)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineSwitchGear not found."));
        var onStartBuild = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineStartBuild" && m.Parameters.Count == 2)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineStartBuild not found."));

        // RPC names → their Rpc_ nested type name → _Success param type
        // All bool RPCs: SwitchGear, StartReload, Reload, StartChannel, StartBuild
        var boolRpcMethods = new[] { "SwitchGear", "StartReload", "Reload", "StartChannel", "StartBuild" };
        foreach (var methodName in boolRpcMethods)
        {
            var method = serviceZoneType.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
            if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) continue;

            // Find the Rpc_ nested type for this method
            var rpcTypeName = "Rpc_" + methodName;
            var rpcType = serviceZoneType.NestedTypes.FirstOrDefault(t => t.Name == rpcTypeName);
            if (rpcType is null) continue;

            var successMethod = rpcType.Methods.FirstOrDefault(static m => m.Name == "_Success" && m.HasBody && m.Parameters.Count == 1);
            if (successMethod is null) continue;

            var successRef = context.TargetModule.ImportReference(successMethod);

            // Prepend: if (ShouldSkipNetworkSend()) { var rpc = _CreateRpc<Rpc_X>(); rpc._Success(true); return rpc; }
            var createRpc = serviceZoneType.Methods.FirstOrDefault(m => m.Name == "_CreateRpc" && m.HasBody)
                ?? serviceZoneType.BaseType?.Resolve()?.Methods.FirstOrDefault(m => m.Name == "_CreateRpc");

            // Use the existing _CreateRpc<T> via the instance — patch: just call _Success on the result after original
            // Simpler: prepend the full early-return inline
            var first = method.Body.Instructions.First();
            var il = method.Body.GetILProcessor();
            var cont = il.Create(OpCodes.Nop);
            il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
            il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));

            // We need to: create rpc, call _Success(true), return rpc
            // _CreateRpc<Rpc_X>() is a generic method on base class — find the closed instance
            var baseType = serviceZoneType.BaseType?.Resolve();
            var createRpcOpen = baseType?.Methods.FirstOrDefault(m => m.Name == "_CreateRpc" && m.HasBody && m.GenericParameters.Count == 1);
            if (createRpcOpen is null) { il.Remove(il.Body.Instructions[il.Body.Instructions.IndexOf(cont) - 2]); il.Remove(il.Body.Instructions[il.Body.Instructions.IndexOf(cont) - 1]); il.Remove(cont); continue; }

            var createRpcClosed = new GenericInstanceMethod(context.TargetModule.ImportReference(createRpcOpen));
            createRpcClosed.GenericArguments.Add(context.TargetModule.ImportReference(rpcType));

            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, createRpcClosed));
            il.InsertBefore(first, il.Create(OpCodes.Dup));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(first, il.Create(OpCodes.Call, successRef));
            if (methodName == "Reload")
            {
                // OnOfflineReload(PlayerUnitId=1) — refills ammo
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, il.Create(OpCodes.Call, onReload));
            }
            else if (methodName == "SwitchGear")
            {
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first, il.Create(OpCodes.Call, onSwitchGear));
            }
            else if (methodName == "StartChannel")
            {
                // OnOfflineStartChannel(PlayerUnitId=1, channelData) — Ldarg_1 is the ChannelData param
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first, il.Create(OpCodes.Call, onStartChannel));
            }
            else if (methodName == "StartBuild")
            {
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(first, il.Create(OpCodes.Call, onStartBuild));
            }
            il.InsertBefore(first, il.Create(OpCodes.Ret));
            il.InsertBefore(first, cont);
        }
    }

    // ServiceZone.EndChannel() — void, 0 params. Patch to call DoEndChannel back with toolIndex=0.
    private static void PatchServiceZoneCancelBuild(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var onCancelBuild = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineCancelBuild" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineCancelBuild not found."));

        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone");
        if (serviceZoneType is null) return;

        var method = serviceZoneType.Methods.FirstOrDefault(static m => m.Name == "CancelBuild" && m.HasBody && m.Parameters.Count == 0);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, onCancelBuild));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // ServiceZone.EndChannel() — void, 0 params. Patch to call DoEndChannel back with toolIndex=0.
    private static void PatchServiceZoneEndChannel(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var onEndChannel = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineEndChannel" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineEndChannel not found."));

        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone");
        if (serviceZoneType is null) return;
        // EndChannel() — 0 params
        var method = serviceZoneType.Methods.FirstOrDefault(static m => m.Name == "EndChannel" && m.HasBody && m.Parameters.Count == 0);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1)); // PlayerUnitId
        il.InsertBefore(first, il.Create(OpCodes.Call, onEndChannel));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // ServiceZone.Hit(ulong time, Dictionary<ulong,HitData> hits) — echo block damage offline.
    private static void PatchServiceZoneCast(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var onCast = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineCast" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineCast not found."));

        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone");
        if (serviceZoneType is null) return;
        var method = serviceZoneType.Methods.FirstOrDefault(static m => m.Name == "Cast" && m.HasBody && m.Parameters.Count == 1);
        if (method is null || MethodCalls(method, "OnOfflineCast", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, il.Create(OpCodes.Call, onCast));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // ServiceZone.Hit(ulong time, Dictionary<ulong,HitData> hits) — echo block damage offline.
    private static void PatchServiceZoneHit(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var onHit = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "OnOfflineHit" && m.Parameters.Count == 2)
            ?? throw new InvalidOperationException("BotModeRuntime.OnOfflineHit not found."));

        var serviceZoneType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ServiceZone");
        if (serviceZoneType is null) return;
        // Hit(ulong time, Dictionary<ulong, HitData> hits) — 2 params
        var method = serviceZoneType.Methods.FirstOrDefault(static m => m.Name == "Hit" && m.HasBody && m.Parameters.Count == 2);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        // Call OnOfflineHit(time=arg1, hits=arg2)
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1)); // ulong time
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_2)); // Dictionary hits
        il.InsertBefore(first, il.Create(OpCodes.Call, onHit));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchInstanceMethodNoOpOffline(ExperimentalPatchContext context, TypeDefinition runtimeType,
        TypeDefinition targetType, string methodName, int paramCount)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var method = targetType.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody && m.Parameters.Count == paramCount);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // Patch the MoveNext of a coroutine loader to return false (done) immediately when offline.
    // Returns false = coroutine finished = loader complete.
    private static void PatchCoroutineLoaderMoveNext(ExperimentalPatchContext context, TypeDefinition runtimeType,
        string loaderTypeName)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var loaderType = context.TargetModule.Types.FirstOrDefault(t => t.Name == loaderTypeName);
        if (loaderType is null) return;

        // Coroutine body lives in nested <Load>c__Iterator* type
        var stateMachine = loaderType.NestedTypes.FirstOrDefault(static t => t.Name.StartsWith("<Load>"));
        if (stateMachine is null) return;

        var moveNext = stateMachine.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody);
        if (moveNext is null || MethodCalls(moveNext, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        // Prepend: if (BotModeRuntime.ShouldSkipNetworkSend()) return false;
        var first = moveNext.Body.Instructions.First();
        var il = moveNext.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0)); // return false = done
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // CommonAssetsLoader.Load() waits for CommonAssets.IsDone which never becomes true offline
    // (LoadBundle throws KeyNotFoundException for every bundle). Skip the wait entirely.
    private static void PatchCommonAssetsLoader(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchCoroutineLoaderMoveNext(context, runtimeType, "CommonAssetsLoader");

    // CommonAssets.IsDone never becomes true offline because LoadBundle crashes Start() and UnpackAssets().
    // Patch the getter to return true immediately when offline so CommonAssetsLoader unblocks.
    private static void PatchCommonAssetsIsDone(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var commonAssetsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "CommonAssets");
        if (commonAssetsType is null) return;

        var getter = commonAssetsType.Methods.FirstOrDefault(static m => m.Name == "get_IsDone" && m.HasBody);
        if (getter is null || MethodCalls(getter, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = getter.Body.Instructions.First();
        var il = getter.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // CommonAssets.LoadBundle() always rethrows KeyNotFoundException even when Steam is disabled.
    // Wrap the body so that offline bot mode swallows the exception instead of crashing Start().
    private static void PatchCommonAssetsLoadBundle(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var commonAssetsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "CommonAssets");
        if (commonAssetsType is null) return;

        var loadBundle = commonAssetsType.Methods.FirstOrDefault(
            static m => m.Name == "LoadBundle" && m.HasBody && m.Parameters.Count == 1);
        if (loadBundle is null || MethodCalls(loadBundle, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        // Prepend: if (BotModeRuntime.ShouldSkipNetworkSend()) return;
        var first = loadBundle.Body.Instructions.First();
        var il = loadBundle.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // ZoneBlocksLoader.Load() waits for MapCreated then calls NetworkDispatcher.Instance.ServiceZone.ZoneReady().
    // Patch MoveNext: when ShouldSkipNetworkSend, skip the ZoneReady() call but still wait for MapCreated.
    // Actually simplest: skip the whole coroutine (map will be created by our StartLocalMatch anyway).
    private static void PatchZoneBlocksLoaderMoveNext(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchCoroutineLoaderMoveNext(context, runtimeType, "ZoneBlocksLoader");

    // ---------------------------------------------------------------------------
    // Patch 9: MediatorLoader.Load() coroutine
    //
    // Waits forever for MediatorNetworkDispatcher.IsMediatorReady — which never
    // becomes true in offline mode. Patch the MoveNext of the generated state
    // machine to return false (done) immediately when bot mode is active.
    // Since coroutines compile to state machines, we patch the outer Load() method
    // to skip EnterScene + the wait loop when ShouldSkipMediatorLoader() is true.
    // ---------------------------------------------------------------------------
    private static void PatchMediatorLoader(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipMediatorLoader" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipMediatorLoader not found."));

        // MediatorLoader.Load() is a coroutine — its body is in a nested state machine class.
        // We patch the outer Load() method which just instantiates the state machine and returns it.
        // Instead, we target the IsMediatorReady property getter or the state machine's MoveNext.
        // Simpler: patch MediatorNetworkDispatcher.get_IsMediatorReady to return true when offline.
        var mediatorType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "MediatorNetworkDispatcher");
        if (mediatorType is null) return;

        var getter = mediatorType.Methods.FirstOrDefault(static m => m.Name == "get_IsMediatorReady" && m.HasBody);
        if (getter is null || MethodCalls(getter, "ShouldSkipMediatorLoader", "BotModeRuntime")) return;

        // Prepend: if (BotModeRuntime.ShouldSkipMediatorLoader()) return true;
        var first = getter.Body.Instructions.First();
        var il = getter.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    // ---------------------------------------------------------------------------
    // Patch 12+13: ZoneData.get_MatchCard / get_GameModeCard
    //
    // Both throw when the key is Key.None. Replace with safe versions that return null.
    // Callers must null-check, but most already do (.Data is X checks handle null).
    // ---------------------------------------------------------------------------
    private static void PatchZoneDataMatchCard(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var safeMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "SafeGetMatchCard")
            ?? throw new InvalidOperationException("BotModeRuntime.SafeGetMatchCard not found."));

        var zoneDataType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ZoneData");
        if (zoneDataType is null) return;

        var getter = zoneDataType.Methods.FirstOrDefault(static m => m.Name == "get_MatchCard" && m.HasBody);
        if (getter is null || MethodCalls(getter, "SafeGetMatchCard", "BotModeRuntime")) return;

        // Replace: return BotModeRuntime.SafeGetMatchCard(this.MatchKey);
        getter.Body.Instructions.Clear();
        getter.Body.ExceptionHandlers.Clear();
        var il = getter.Body.GetILProcessor();
        var matchKeyField = zoneDataType.Fields.FirstOrDefault(static f => f.Name == "MatchKey");
        if (matchKeyField is null) return;
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, matchKeyField));
        il.Append(il.Create(OpCodes.Call, safeMethod));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void PatchZoneDataGameModeCard(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var safeMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "SafeGetGameModeCard")
            ?? throw new InvalidOperationException("BotModeRuntime.SafeGetGameModeCard not found."));

        var zoneDataType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "ZoneData");
        if (zoneDataType is null) return;

        var getter = zoneDataType.Methods.FirstOrDefault(static m => m.Name == "get_GameModeCard" && m.HasBody);
        if (getter is null || MethodCalls(getter, "SafeGetGameModeCard", "BotModeRuntime")) return;

        getter.Body.Instructions.Clear();
        getter.Body.ExceptionHandlers.Clear();
        var il = getter.Body.GetILProcessor();
        var gameModeKeyField = zoneDataType.Fields.FirstOrDefault(static f => f.Name == "GameModeKey");
        if (gameModeKeyField is null) return;
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, gameModeKeyField));
        il.Append(il.Create(OpCodes.Call, safeMethod));
        il.Append(il.Create(OpCodes.Ret));
    }

    // ---------------------------------------------------------------------------
    // Patch 7+8: SceneManager.IsTutorial / IsTimeTrial (static getters)
    //
    // Both throw when MatchKey is Key.None (our offline SceneZone has no match key).
    // Replace the getter body with a safe version that returns false on Key.None.
    // ---------------------------------------------------------------------------
    private static void PatchSceneManagerIsTutorial(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchStaticBoolGetter(context, runtimeType, "IsTutorial", "SafeIsTutorial");

    private static void PatchSceneManagerIsTimeTrial(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchStaticBoolGetter(context, runtimeType, "IsTimeTrial", "SafeIsTimeTrial");

    private static void PatchStaticBoolGetter(ExperimentalPatchContext context, TypeDefinition runtimeType,
        string propertyGetterName, string runtimeMethodName)
    {
        var safeMethod = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(m => m.Name == runtimeMethodName && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException($"BotModeRuntime.{runtimeMethodName} not found."));

        var sceneManagerType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneManager");
        if (sceneManagerType is null) return;

        // Property getter is named get_IsTutorial / get_IsTimeTrial
        var getter = sceneManagerType.Methods.FirstOrDefault(
            m => m.Name == $"get_{propertyGetterName}" && m.HasBody && m.Parameters.Count == 0);
        if (getter is null || MethodCalls(getter, runtimeMethodName, "BotModeRuntime")) return;

        // Replace body: call BotModeRuntime.Safe*() and ret
        getter.Body.Instructions.Clear();
        getter.Body.ExceptionHandlers.Clear();
        var il = getter.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Call, safeMethod));
        il.Append(il.Create(OpCodes.Ret));
    }

    // ---------------------------------------------------------------------------
    // Patch 5: GuiRelogin.Update()
    //
    // The game shows a "connection lost" popup whenever NetworkDispatcher.IsDisconnected.
    // In offline mode we're never connected, so we suppress it by returning early
    // when BotModeRuntime.ShouldSuppressDisconnectUi() is true.
    // ---------------------------------------------------------------------------
    private static void PatchGuiRelogin(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSuppress = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSuppressDisconnectUi" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSuppressDisconnectUi not found."));

        var guiReloginType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiRelogin");
        if (guiReloginType is null) return;

        var updateMethod = guiReloginType.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody && m.Parameters.Count == 0);
        if (updateMethod is null || MethodCalls(updateMethod, "ShouldSuppressDisconnectUi", "BotModeRuntime")) return;

        var first = updateMethod.Body.Instructions.First();
        var il = updateMethod.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        // if (!BotModeRuntime.ShouldSuppressDisconnectUi()) goto continueOriginal;
        // return;
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSuppress));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    private static void PatchLoadLevelLoaderProbe(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var probe = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ProbeLoadLevel")
            ?? throw new InvalidOperationException("ProbeLoadLevel not found."));

        var t = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "LoadLevelLoader");
        if (t is null) return;
        var nested = t.NestedTypes.FirstOrDefault(static n => n.Name.StartsWith("<Load>"));
        if (nested is null) return;
        var moveNext = nested.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody);
        if (moveNext is null || MethodCalls(moveNext, "ProbeLoadLevel", "BotModeRuntime")) return;

        // Find the Application.LoadLevel call and insert probe before it
        var loadLevelCall = moveNext.Body.Instructions.FirstOrDefault(
            static i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                        i.Operand is MethodReference mr && mr.Name == "LoadLevel");
        if (loadLevelCall is null) return;

        var il = moveNext.Body.GetILProcessor();
        // Stack before LoadLevel has the string sceneName — dup it for our probe
        il.InsertBefore(loadLevelCall, il.Create(OpCodes.Dup));
        il.InsertBefore(loadLevelCall, il.Create(OpCodes.Call, probe));
    }

    private static void PatchSceneLoaderDataBeginProbe(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var probe = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ProbeLoaderBegin")
            ?? throw new InvalidOperationException("ProbeLoaderBegin not found."));

        var t = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneLoaderData");
        if (t is null) return;
        var m = t.Methods.FirstOrDefault(static m => m.Name == "Begin" && m.HasBody);
        if (m is null || MethodCalls(m, "ProbeLoaderBegin", "BotModeRuntime")) return;

        var first = m.Body.Instructions.First();
        var il = m.Body.GetILProcessor();
        // ProbeLoaderBegin((int)scene)
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1)); // GameSceneType scene
        il.InsertBefore(first, il.Create(OpCodes.Call, probe));
    }

    private static void PatchOnLevelWasLoadedProbe(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var probe = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ProbeLevelLoaded")
            ?? throw new InvalidOperationException("ProbeLevelLoaded not found."));

        var t = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "SceneManager");
        if (t is null) return;
        var m = t.Methods.FirstOrDefault(static m => m.Name == "OnLevelWasLoaded" && m.HasBody);
        if (m is null || MethodCalls(m, "ProbeLevelLoaded", "BotModeRuntime")) return;

        var first = m.Body.Instructions.First();
        var il = m.Body.GetILProcessor();
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1)); // int levelId
        il.InsertBefore(first, il.Create(OpCodes.Call, probe));
    }

    // ZoneBuild.SetMapTileset() / SetMapRender() call AssetCache.LoadPrefab which throws when bundles aren't loaded.
    // Return null offline — MapWorld.InitCache stores the result in fields that are only used for rendering.
    // MapWorld.UpdatePlane / UpdateRender call ZoneBuild which needs bundles — make no-ops offline.
    private static void PatchMapWorldUpdatePlane(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "MapWorld", "UpdatePlane", 2);

    // GuiPlayerInfo.Update — in offline mode use a safe runtime implementation instead of the live server-backed one.
    private static void PatchGuiPlayerInfoUpdate(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var handler = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "HandleOfflineGuiPlayerInfo" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.HandleOfflineGuiPlayerInfo not found."));

        var t = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "GuiPlayerInfo");
        if (t is null) return;
        var m = t.Methods.FirstOrDefault(static m => m.Name == "Update" && m.HasBody && m.Parameters.Count == 0);
        if (m is null || MethodCalls(m, "HandleOfflineGuiPlayerInfo", "BotModeRuntime")) return;

        var first = m.Body.Instructions.First();
        var il = m.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, handler));
        il.InsertBefore(first, il.Create(OpCodes.Pop));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchInstanceMethodNoOpOffline(ExperimentalPatchContext context,
        TypeDefinition runtimeType, string typeName, string methodName, int paramCount)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var t = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        if (t is null) return;
        var m = t.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody && m.Parameters.Count == paramCount);
        if (m is null || MethodCalls(m, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = m.Body.Instructions.First();
        var il = m.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchStaticMethodNoOpOffline(ExperimentalPatchContext context,
        TypeDefinition runtimeType, string typeName, string methodName, int paramCount)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, typeName, methodName, paramCount);

    // AssetCache.LoadPrefab tries to load from bundles which don't exist offline.
    // Patch it to fall back to BotModeRuntime.FallbackLoadPrefab (Resources.Load) on exception.
    private static void PatchAssetCacheLoadPrefab(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var fallback = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "FallbackLoadPrefab" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.FallbackLoadPrefab not found."));

        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var assetCacheType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "AssetCache");
        if (assetCacheType is null) return;

        var method = assetCacheType.Methods.FirstOrDefault(static m => m.Name == "LoadPrefab" && m.HasBody && m.Parameters.Count == 1);
        if (method is null || MethodCalls(method, "FallbackLoadPrefab", "BotModeRuntime")) return;

        // Replace body: if (ShouldSkipNetworkSend()) return FallbackLoadPrefab(name); else <original>
        // Actually wrap the original body in try/catch: catch → return FallbackLoadPrefab(name)
        // Simplest: prepend guard — if offline, return fallback directly.
        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_1)); // name param
        il.InsertBefore(first, il.Create(OpCodes.Call, fallback));
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    private static void PatchBlockMaterialsAccessors(ExperimentalPatchContext context, TypeDefinition runtimeType)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));
        var offlineCount = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetOfflineBlockMaterialsCount" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.GetOfflineBlockMaterialsCount not found."));
        var offlineIndex = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetOfflineBlockMaterialIndex" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.GetOfflineBlockMaterialIndex not found."));
        var offlineMaterial = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "GetOfflineBlockMaterial" && m.Parameters.Count == 1)
            ?? throw new InvalidOperationException("BotModeRuntime.GetOfflineBlockMaterial not found."));

        var blockMaterialsType = context.TargetModule.Types.FirstOrDefault(static t => t.Name == "BlockMaterials");
        if (blockMaterialsType is null) return;

        PatchStaticMethodOfflineReturn(context, blockMaterialsType, "get_MaterialsCount", 0, shouldSkip, il =>
        {
            return new[]
            {
                il.Create(OpCodes.Call, offlineCount),
            };
        });
        PatchStaticMethodOfflineReturn(context, blockMaterialsType, "GetMaterialIndex", 1, shouldSkip, il =>
        {
            return new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Call, offlineIndex),
            };
        });
        PatchStaticMethodOfflineReturn(context, blockMaterialsType, "GetFakeMaterialIndex", 1, shouldSkip, il =>
        {
            return new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Call, offlineIndex),
            };
        });
        PatchStaticMethodOfflineReturn(context, blockMaterialsType, "GetMaterial", 1, shouldSkip, il =>
        {
            return new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Call, offlineMaterial),
            };
        });
    }

    private static void PatchStaticMethodOfflineReturn(
        ExperimentalPatchContext context,
        TypeDefinition type,
        string methodName,
        int paramCount,
        MethodReference shouldSkip,
        Func<ILProcessor, IEnumerable<Instruction>> buildOfflineBody)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody && m.Parameters.Count == paramCount);
        if (method is null || MethodCalls(method, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = method.Body.Instructions.First();
        var il = method.Body.GetILProcessor();
        var cont = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, cont));
        foreach (var instruction in buildOfflineBody(il))
        {
            il.InsertBefore(first, instruction);
        }
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, cont);
    }

    // MapSpreadLight/MapSimpleLight.Create() do expensive per-voxel lighting — takes forever on a 256x48x88 map.
    // Skip entirely offline; MapWorld still renders with vertex color = white (MapEmptyLight behaviour).
    private static void PatchMapSpreadLightCreate(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchLightCreateMoveNext(context, runtimeType, "MapSpreadLight");

    private static void PatchMapSimpleLightCreate(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchLightCreateMoveNext(context, runtimeType, "MapSimpleLight");

    private static void PatchMainMenuStatisticsUpdate(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "MainMenuStatistics", "Update", 0);

    private static void PatchGuiHelpScreenStart(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "GuiHelpScreen", "Start", 0);

    private static void PatchUiChatStart(ExperimentalPatchContext context, TypeDefinition runtimeType)
        => PatchInstanceMethodNoOpOffline(context, runtimeType, "UiChat", "Start", 0);

    private static void PatchLightCreateMoveNext(ExperimentalPatchContext context, TypeDefinition runtimeType, string typeName)
    {
        var shouldSkip = context.TargetModule.ImportReference(
            runtimeType.Methods.FirstOrDefault(static m => m.Name == "ShouldSkipNetworkSend" && m.Parameters.Count == 0)
            ?? throw new InvalidOperationException("BotModeRuntime.ShouldSkipNetworkSend not found."));

        var lightType = context.TargetModule.Types.FirstOrDefault(t => t.Name == typeName);
        if (lightType is null) return;

        // Create() is a coroutine — find the nested state machine
        var stateMachine = lightType.NestedTypes.FirstOrDefault(static t => t.Name.StartsWith("<Create>"));
        if (stateMachine is null) return;

        var moveNext = stateMachine.Methods.FirstOrDefault(static m => m.Name == "MoveNext" && m.HasBody);
        if (moveNext is null || MethodCalls(moveNext, "ShouldSkipNetworkSend", "BotModeRuntime")) return;

        var first = moveNext.Body.Instructions.First();
        var il = moveNext.Body.GetILProcessor();
        var continueOriginal = il.Create(OpCodes.Nop);

        il.InsertBefore(first, il.Create(OpCodes.Call, shouldSkip));
        il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, continueOriginal));
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0)); // return false = done
        il.InsertBefore(first, il.Create(OpCodes.Ret));
        il.InsertBefore(first, continueOriginal);
    }

    private static bool MethodCalls(MethodDefinition method, string methodName, string declaringTypeName)
    {
        return method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == methodName &&
            mr.DeclaringType.Name == declaringTypeName);
    }
}
