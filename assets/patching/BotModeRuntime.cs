// BotModeRuntime.cs — Phase 2
// Compiled into the helper assembly and injected into the game via IL patches.
// All code here runs inside the Unity game process.
//
// Phase 1: IL hooks + config skeleton
// Phase 2: Offline match bootstrap (local ZoneServiceListener calls replace server)
// Phase 3: Bot AI FSM (see BotController below)
//
// See BOTS.md for full architecture documentation.

namespace BnlCommunityFixes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public static class BotModeRuntime
    {
        // ── config ────────────────────────────────────────────────────────────────
        private const string ConfigFileName = "experimental-bot-mode-config.json";
        private const float ConfigRefreshInterval = 5f;

        private static bool configLoaded;
        private static float nextConfigRefresh;
        private static bool enabled;
        private static int botCount = 3;
        private static string difficulty = "medium";
        private static UnityEngine.Material offlineBlockMaterial;
        private static UnityEngine.Material offlineUnitMaterialTeam1;
        private static UnityEngine.Material offlineUnitMaterialTeam2;
        private static UnityEngine.Camera offlineCamera;

        // ── state ─────────────────────────────────────────────────────────────────
        private static ZoneServiceListener cachedListener;
        private static Protocol.ServiceZone offlineServiceZone;
        private static bool matchRunning;
        private static float tickAccumulator;
        private const float BotTickInterval = 0.1f;
        private const int OfflineMapSizeX = 32;
        private const int OfflineMapSizeY = 16;
        private const int OfflineMapSizeZ = 32;
        private const int OfflineSpawnY = 6;
        private static int phaseUpdateRetries = 0;
        private static bool usingBundledMap = false;
        // Deferred spawn: set after InitZone, units spawned once map is ready.
        private static bool spawnPending = false;
        private static Key pendingPlayerHeroKey;
        private static Protocol.MapData pendingMapData;
        private static readonly Dictionary<uint, BuildInfo> pendingBuilds = new Dictionary<uint, BuildInfo>();
        private static readonly Dictionary<uint, byte> lastToolIndices = new Dictionary<uint, byte>();
        private static uint nextOfflineDeviceUnitId = 12000;
        // Death/respawn simulation (server-driven in a live match).
        private static readonly Dictionary<uint, Key> botHeroKeys = new Dictionary<uint, Key>();
        private static readonly Dictionary<uint, float> respawnQueue = new Dictionary<uint, float>();
        private const float RespawnDelaySec = 6f;
        private const float AssistWindowSec = 10f;
        private static readonly Dictionary<uint, Dictionary<uint, float>> recentDamagers = new Dictionary<uint, Dictionary<uint, float>>();
        // Map units (turrets, objective cubes...) spawned from MapData.Units.
        private const uint MapUnitIdBase = 11000;
        private static readonly List<uint> objectiveUnitIds = new List<uint>();
        private static MapData pendingSourceMapData;
        // World simulation (auras, pickups, kill plane) — server jobs replicated locally.
        private static readonly Dictionary<uint, float> auraNextFire = new Dictionary<uint, float>();
        private static readonly Dictionary<uint, Key> activePickups = new Dictionary<uint, Key>();
        // Active heal-over-time entries (from pickup/aura constant effects).
        private sealed class RegenEntry
        {
            public uint UnitId;
            public uint SourceId;   // 0 = pickup/one-shot; aura entries keyed by source unit
            public float RatePerSec;
            public float EndTime;
        }
        private static readonly List<RegenEntry> activeRegens = new List<RegenEntry>();

        // Projectile weapons need the same server messages that a live match sends.
        // Cast alone only plays the remote unit's firing animation; it does not create
        // a world projectile. Keep a small local simulation driven from the CDB bullet.
        private sealed class BotProjectile
        {
            public ulong ShotId;
            public uint OwnerId;
            public uint TargetId;
            public UnityEngine.Vector3 Start;
            public UnityEngine.Vector3 End;
            public float StartTime;
            public float TravelTime;
            public float Damage;
            public Key? Impact;
            public bool ShouldHit;
        }
        private static readonly List<BotProjectile> botProjectiles = new List<BotProjectile>();

        private static void AddOrRefreshRegen(uint unitId, uint sourceId, float ratePerSec, float endTime)
        {
            foreach (var entry in activeRegens)
            {
                if (entry.UnitId == unitId && entry.SourceId == sourceId)
                {
                    entry.RatePerSec = ratePerSec;
                    entry.EndTime = endTime;
                    return;
                }
            }
            activeRegens.Add(new RegenEntry { UnitId = unitId, SourceId = sourceId, RatePerSec = ratePerSec, EndTime = endTime });
        }
        private static float offlineKillY = -1000f;
        private static float nextWorldSim;
        private const float WorldSimInterval = 0.5f;
        private const float PickupRadius = 1.8f;

        private static readonly List<BotController> bots = new List<BotController>();

        // IDs assigned to bot units — start high to avoid collisions with real units.
        private const uint BotUnitIdBase = 9000;
        private const uint BotPlayerIdBase = 5000;
        // Player's own unit gets id 1 in offline mode.
        private const uint PlayerUnitId = 1;

        // ─────────────────────────────────────────────────────────────────────────
        // IL hook 1 — LoginLogic.DoLogin()
        // ─────────────────────────────────────────────────────────────────────────
        public static bool ShouldBypassLogin()
        {
            return IsEnabled();
        }

        // Called by SceneManager.IsTutorial / IsTimeTrial patches — return false instead of throwing on Key.None.
        public static bool SafeIsTutorial()
        {
            try
            {
                SceneZone param = SceneManager.GetParam<SceneZone>();
                if (param == null || param.MatchKey == Key.None) return false;
                return Singleton<Catalogue>.Instance.GetCard<CardMatch>(param.MatchKey).Data is MatchDataTutorial;
            }
            catch { return false; }
        }

        public static bool SafeIsTimeTrial()
        {
            try
            {
                SceneZone param = SceneManager.GetParam<SceneZone>();
                if (param == null || param.MatchKey == Key.None) return false;
                return Singleton<Catalogue>.Instance.GetCard<CardMatch>(param.MatchKey).Data is MatchDataTimeTrial;
            }
            catch { return false; }
        }

        // Called by ZoneData.get_MatchCard / get_GameModeCard patches — return null instead of throwing on Key.None.
        public static CardMatch SafeGetMatchCard(Key key)
        {
            if (key == Key.None) return null;
            try { return Singleton<Catalogue>.Instance.GetCard<CardMatch>(key); }
            catch { return null; }
        }

        public static CardGameMode SafeGetGameModeCard(Key key)
        {
            if (key == Key.None) return null;
            try { return Singleton<Catalogue>.Instance.GetCard<CardGameMode>(key); }
            catch { return null; }
        }

        // Called by MediatorLoader.Load() patch — skip mediator connection in offline mode.
        public static bool ShouldSkipMediatorLoader()
        {
            return IsEnabled();
        }

        // Called by ServiceScene.EnterScene() and ServiceZone.ZoneReady() patches.
        public static bool ShouldSkipNetworkSend()
        {
            return IsEnabled();
        }

        public static bool HandleOfflineGuiPlayerInfo(GuiPlayerInfo gui)
        {
            if (!IsEnabled() || gui == null)
                return false;

            try
            {
                if (gui.Content == null || Singleton<Hud>.Instance == null)
                    return true;

                gui.Content.SetActive(Singleton<Hud>.Instance.IsShow(Hud.Window.PlayerInfo));
                if (!gui.Content.activeSelf)
                    return true;

                var player = Singleton<UnitsRegistry>.Instance != null ? Singleton<UnitsRegistry>.Instance.GetPlayer() : null;
                if (player == null)
                    return true;

                var skinCard = player.SkinCard;
                if (skinCard != null && gui.HeroIcon != null)
                    gui.HeroIcon.sprite = skinCard.GetIcon();

                if (gui.HeroLevel != null)
                {
                    int level = 0;
                    var progress = Singleton<PlayerData>.Instance != null ? Singleton<PlayerData>.Instance.Progression : null;
                    var heroes = progress != null ? progress.HeroesProgress : null;
                    if (heroes != null && heroes.ContainsKey(player.UnitCard.Key))
                        level = heroes[player.UnitCard.Key].Level;
                    gui.HeroLevel.text = level.ToString();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] HandleOfflineGuiPlayerInfo failed: " + ex.Message);
            }

            return true;
        }

        // Called by NetworkDispatcher.get_ServiceZone / MediatorNetworkDispatcher.get_ServiceZone patches.
        // Returns a cached offline ServiceZone instance so gear-switch, reload, etc. RPCs work offline.
        public static Protocol.ServiceZone GetOfflineServiceZone()
        {
            if (offlineServiceZone == null)
            {
                offlineServiceZone = new Protocol.ServiceZone(6, null);
                offlineServiceZone.Listener = cachedListener;
                UnityEngine.Debug.Log("[BNL-Bots] Created offline ServiceZone.");
            }
            else if (offlineServiceZone.Listener == null && cachedListener != null)
            {
                offlineServiceZone.Listener = cachedListener;
            }
            return offlineServiceZone;
        }

        // Called by Igor.Service._BeginSend patch — returns a dummy BinaryWriter so offline sends don't NPE.
        private static System.IO.BinaryWriter dummyWriter;
        public static System.IO.BinaryWriter GetDummyBinaryWriter()
        {
            if (dummyWriter == null)
                dummyWriter = new System.IO.BinaryWriter(new System.IO.MemoryStream(256));
            ((System.IO.MemoryStream)dummyWriter.BaseStream).SetLength(0);
            return dummyWriter;
        }

        // Called by ServiceZone.Reload RPC patch — simulate server sending back full ammo.
        public static void OnOfflineReload(uint unitId)
        {
            if (cachedListener == null) return;
            try
            {
                var unit = Singleton<UnitsRegistry>.Instance?.Get(unitId);
                if (unit == null || unit.Gears == null) return;

                var ammoDict = new System.Collections.Generic.Dictionary<Key, List<Protocol.Ammo>>();
                foreach (var gear in unit.Gears)
                {
                    if (gear == null || gear.Ammo == null || gear.Ammo.Count == 0) continue;
                    var ammoList = new List<Protocol.Ammo>();
                    foreach (var ga in gear.Ammo)
                    {
                        ammoList.Add(new Protocol.Ammo
                        {
                            Index = ga.AmmoIndex,
                            Mag = ga.IsMag ? (float?)ga.MagSize : null,
                            Pool = ga.IsPool ? (float?)ga.PoolSize : null,
                        });
                    }
                    ammoDict[gear.Key] = ammoList;
                }

                if (ammoDict.Count > 0)
                    cachedListener.UnitUpdate(unitId, new UnitUpdate { Ammo = ammoDict });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineReload failed: " + ex.Message);
            }
        }

        // Called by ServiceZone.StartChannel patch — simulate server echoing DoStartChannel back.
        public static void OnOfflineStartChannel(uint unitId, Protocol.ChannelData data)
        {
            if (cachedListener == null) return;
            try { cachedListener.DoStartChannel(unitId, data); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineStartChannel failed: " + ex.Message); }
        }

        // Called by ServiceZone.EndChannel patch (0 params) — simulate server echoing DoEndChannel back.
        public static void OnOfflineEndChannel(uint unitId)
        {
            if (cachedListener == null) return;
            try { cachedListener.DoEndChannel(unitId, 0); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineEndChannel failed: " + ex.Message); }
        }

        public static void OnOfflineSwitchGear(uint unitId, Key gearKey)
        {
            if (cachedListener == null) return;
            try
            {
                cachedListener.UnitUpdate(unitId, new UnitUpdate { CurrentGear = gearKey });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineSwitchGear failed: " + ex.Message);
            }
        }

        public static void OnOfflineStartBuild(uint unitId, BuildInfo info)
        {
            if (cachedListener == null || info == null) return;
            try
            {
                pendingBuilds[unitId] = info;
                lastToolIndices[unitId] = info.ToolIndex;
                cachedListener.DoStartBuild(unitId, info);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineStartBuild failed: " + ex.Message);
            }
        }

        public static void OnOfflineCast(Protocol.CastData data)
        {
            if (data == null)
                return;

            try
            {
                lastToolIndices[PlayerUnitId] = data.ToolIndex;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineCast failed: " + ex.Message);
            }
        }

        public static void OnOfflineCancelBuild(uint unitId)
        {
            if (cachedListener == null) return;
            try
            {
                pendingBuilds.Remove(unitId);
                cachedListener.DoCancelBuild(unitId);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineCancelBuild failed: " + ex.Message);
            }
        }

        // Called by ServiceZone.Hit patch — echo block damage as BlockUpdates.
        // Damage is calculated from the current gear's damage stat vs block MaxHealth from the catalogue.
        public static void OnOfflineHit(ulong time, System.Collections.Generic.Dictionary<ulong, Protocol.HitData> hits)
        {
            if (cachedListener == null || hits == null || hits.Count == 0) return;
            try
            {
                var zm = Singleton<ZoneManager>.Instance;
                var updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>();

                BuildInfo pendingBuild;
                if (pendingBuilds.TryGetValue(PlayerUnitId, out pendingBuild))
                {
                    // Remove up-front: a build that throws mid-finalize must never be retried
                    // on later hits (it blocked digging and duplicated devices).
                    pendingBuilds.Remove(PlayerUnitId);

                    bool finalized = false;
                    try { finalized = TryFinalizeOfflineBuild(PlayerUnitId, pendingBuild, null); }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit build finalize failed: " + ex.Message);
                    }

                    if (!finalized)
                    {
                        foreach (var kv in hits)
                        {
                            try
                            {
                                if (kv.Value != null && TryFinalizeOfflineBuild(PlayerUnitId, pendingBuild, kv.Value))
                                {
                                    finalized = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit build finalize failed: " + ex.Message);
                            }
                        }
                    }
                    if (!finalized)
                        UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit build finalize failed: no valid placement.");
                    return;
                }

                // Per-pellet damage of the player's current tool (server applies the hit effect once per hit entry).
                float worldDamage = GetCurrentToolDamage(PlayerUnitId, OfflineDamageKind.World);
                float playerDamage = GetCurrentToolDamage(PlayerUnitId, OfflineDamageKind.Player);
                float objectiveDamage = GetCurrentToolDamage(PlayerUnitId, OfflineDamageKind.Objective);
                Key? toolImpact = GetCurrentToolImpact(PlayerUnitId);
                var hitCounts = new Dictionary<Vector3s, int>();
                var unitHitCounts = new Dictionary<uint, int>();
                var unitCrits = new Dictionary<uint, bool>();
                try
                {
                    foreach (var kv in hits)
                    {
                        var hit = kv.Value;
                        if (hit == null)
                            continue;

                        // Pellets that hit a unit damage that unit, not the block behind it.
                        if (hit.TargetId != null)
                        {
                            int ucount;
                            unitHitCounts.TryGetValue(hit.TargetId.Value, out ucount);
                            unitHitCounts[hit.TargetId.Value] = ucount + 1;
                            if (hit.Crit == true)
                                unitCrits[hit.TargetId.Value] = true;
                            continue;
                        }

                        var pos = ToBlockPos(hit.InsidePoint);
                        int count;
                        hitCounts.TryGetValue(pos, out count);
                        hitCounts[pos] = count + 1;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit aggregate failed: " + ex);
                    return;
                }

                // Apply unit damage (bots, turrets, objectives...). Objectives take the
                // tool's ObjectiveDamage, everything else PlayerDamage — as live does.
                foreach (var kv in unitHitCounts)
                {
                    try
                    {
                        bool crit;
                        unitCrits.TryGetValue(kv.Key, out crit);

                        bool isObjective = false;
                        try
                        {
                            var target = Singleton<UnitsRegistry>.Instance?.Get(kv.Key);
                            isObjective = target != null && target.UnitCard != null && target.UnitCard.IsObjective;
                        }
                        catch { }

                        float perPellet = isObjective ? objectiveDamage : playerDamage;
                        float dmg = perPellet * kv.Value * (crit ? 1.5f : 1f);
                        ApplyOfflineUnitDamage(PlayerUnitId, kv.Key, dmg, crit, toolImpact);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit unit damage failed: " + ex.Message);
                    }
                }

                // Tools without world damage don't damage blocks at all (matches live behaviour).
                if (worldDamage <= 0f)
                {
                    if (updates.Count > 0)
                        cachedListener.BlockUpdates(updates);
                    return;
                }

                foreach (var kv in hitCounts)
                {
                    try
                    {
                        var pos = kv.Key;
                        var pelletHits = kv.Value;

                        // Get the block at this position to know its health and current damage.
                        byte currentDamage = 0;
                        float blockMaxHealth = 100f;
                        if (zm == null)
                            continue;

                        var nullableBlock = zm.GetBlock(pos);
                        if (nullableBlock == null)
                            continue;

                        var block = nullableBlock.Value;
                        if (block.Id == 0) continue; // air
                        currentDamage = block.Damage;
                        var card = BlockCardsCache.GetCard(block.Id);
                        if (card != null && card.Health != null && card.Health.MaxHealth > 0f)
                            blockMaxHealth = card.Health.MaxHealth;
                        else if (card == null)
                            continue;

                        var update = block.ToUpdate();

                        // Live semantics: the hit effect's WorldDamage applies once per pellet that hit this block.
                        float effectiveDamage = worldDamage * pelletHits;
                        float damageAsFraction = effectiveDamage / blockMaxHealth;
                        int addDamage = (int)UnityEngine.Mathf.Ceil(damageAsFraction * 255f);
                        if (addDamage < 1) addDamage = 1;
                        int newDamage = currentDamage + addDamage;

                        if (newDamage >= 255)
                        {
                            update.Id = 0;
                            update.Damage = 0;
                            update.Vdata = 1;
                        }
                        else
                        {
                            update.Damage = (byte)newDamage;
                        }

                        updates[pos] = update;

                        if (update.Id == 0)
                        {
                            AppendUnsupportedBlocks(pos, updates);
                        }
                        else if (block.Card != null && block.Card.Visual.Material != null)
                        {
                            Singleton<GlobalSounds>.Instance.PostImpact(
                                pos.ToVector3() + UnityEngine.Vector3.one * 0.5f,
                                block.Card.Visual.Material.Value,
                                new bool?());
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit block failed: " + ex);
                    }
                }
                if (updates.Count > 0)
                    cachedListener.BlockUpdates(updates);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnOfflineHit failed: " + ex);
            }
        }

        public enum OfflineDamageKind { Player, World, Objective }

        private static float GetCurrentToolDamage(uint unitId, OfflineDamageKind kind)
        {
            try
            {
                return GetToolDamage(GetCurrentTool(unitId), kind);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] GetCurrentToolDamage failed: " + ex.Message);
            }

            return 0f;
        }

        private static Tool GetCurrentTool(uint unitId)
        {
            var unit = Singleton<UnitsRegistry>.Instance?.Get(unitId);
            var gear = unit?.CurrentGear;
            if (gear == null)
                return null;

            byte toolIndex;
            if (!lastToolIndices.TryGetValue(unitId, out toolIndex))
                toolIndex = 0;

            return gear.GetTool(toolIndex)?.Tool;
        }

        // Per-pellet damage of a tool for the given damage kind.
        internal static float GetToolDamage(Tool tool, OfflineDamageKind kind)
        {
            if (tool == null)
                return 0f;

            var shot = tool as ToolShot;
            if (shot != null)
                return GetEffectDamage(shot.HitEffect, kind);

            var burst = tool as ToolBurst;
            if (burst != null)
                return GetEffectDamage(burst.HitEffect, kind);

            var spinup = tool as ToolSpinup;
            if (spinup != null)
                return GetEffectDamage(spinup.HitEffect, kind);

            var melee = tool as ToolMelee;
            if (melee != null)
                return GetEffectDamage(melee.HitEffect, kind);

            return 0f;
        }

        private static float PickDamage(Damage damage, OfflineDamageKind kind)
        {
            if (damage == null)
                return 0f;
            switch (kind)
            {
                case OfflineDamageKind.World: return damage.WorldDamage;
                case OfflineDamageKind.Objective: return damage.ObjectiveDamage;
                default: return damage.PlayerDamage;
            }
        }

        // Impact card key of a tool's damage effect — drives hit/crit sound feedback
        // (CommonCharacterSoundHandler.OnUnitDealDamage plays nothing without it).
        internal static Key? GetToolImpact(Tool tool)
        {
            if (tool == null)
                return null;

            var shot = tool as ToolShot;
            if (shot != null) return GetEffectImpact(shot.HitEffect);
            var burst = tool as ToolBurst;
            if (burst != null) return GetEffectImpact(burst.HitEffect);
            var spinup = tool as ToolSpinup;
            if (spinup != null) return GetEffectImpact(spinup.HitEffect);
            var melee = tool as ToolMelee;
            if (melee != null) return GetEffectImpact(melee.HitEffect);
            return null;
        }

        private static Key? GetEffectImpact(InstEffect effect)
        {
            if (effect == null)
                return null;

            if (effect.Impact != null)
                return effect.Impact;

            var bunch = effect as InstEffectBunch;
            if (bunch != null && bunch.Instant != null)
            {
                foreach (var inner in bunch.Instant)
                {
                    var key = GetEffectImpact(inner);
                    if (key != null)
                        return key;
                }
            }

            return null;
        }

        private static Key? GetCurrentToolImpact(uint unitId)
        {
            try { return GetToolImpact(GetCurrentTool(unitId)); }
            catch { return null; }
        }

        // Weapons wrap their damage in InstEffectBunch → InstEffectDamage/InstEffectDamageBlocks.
        private static float GetEffectDamage(InstEffect effect, OfflineDamageKind kind)
        {
            if (effect == null)
                return 0f;

            var blockDamage = effect as InstEffectDamageBlocks;
            if (blockDamage != null)
                return kind == OfflineDamageKind.Player ? 0f : PickDamage(blockDamage.Damage, kind);

            var damage = effect as InstEffectDamage;
            if (damage != null)
                return PickDamage(damage.Damage, kind);

            var splash = effect as InstEffectSplashDamage;
            if (splash != null)
                return PickDamage(splash.Damage, kind);

            var bunch = effect as InstEffectBunch;
            if (bunch != null && bunch.Instant != null)
            {
                float total = 0f;
                foreach (var inner in bunch.Instant)
                    total += GetEffectDamage(inner, kind);
                return total;
            }

            return 0f;
        }

        private static bool TryFinalizeOfflineBuild(uint unitId, BuildInfo buildInfo, HitData hit)
        {
            var builder = Singleton<UnitsRegistry>.Instance?.Get(unitId);
            var zoneManager = Singleton<ZoneManager>.Instance;
            if (builder == null || buildInfo == null || zoneManager?.Map == null)
                return false;

            Vector3s supportBlock;
            Vector3s buildInside;
            Direction2D direction;
            if (!TryGetCurrentBuildPlacement(builder, buildInfo, hit, out buildInside, out supportBlock, out direction))
                return false;

            if (!zoneManager.Map.Blocks.Check(buildInside))
                return false;

            var buildOutside = supportBlock;

            // BuildInfo.DeviceKey is a CardDevice key; the actual thing built is its
            // (level-resolved) internal device card — a CardBlock or CardUnit.
            var deviceKey = buildInfo.DeviceKey;
            var deviceCard = SafeGetCard<CardDevice>(deviceKey);
            if (deviceCard != null)
            {
                var resolved = Key.None;
                try { resolved = deviceCard.DeviceKey; }
                catch { }
                if (resolved == Key.None)
                {
                    try { resolved = deviceCard.StartingDeviceKey; }
                    catch { }
                }
                if (resolved != Key.None)
                    deviceKey = resolved;
            }

            var blockCard = SafeGetCard<CardBlock>(deviceKey);
            if (blockCard != null)
            {
                // Block team lives in Ldata bits 0-1 (1=Team1, 2=Team2); 0 renders as
                // neutral/enemy colouring, so stamp the builder's team on team blocks.
                byte ldata = 0;
                try
                {
                    if (blockCard.HasTeam)
                        ldata = (byte)(builder.Team == TeamType.Team1 ? 1 : builder.Team == TeamType.Team2 ? 2 : 0);
                }
                catch { }

                var update = new BlockUpdate
                {
                    Id = blockCard.BlockId,
                    Damage = 0,
                    Vdata = 0,
                    Ldata = ldata,
                };

                cachedListener.BlockUpdates(new Dictionary<Vector3s, BlockUpdate> { [buildInside] = update });
                NotifyDeviceBuilt(builder, deviceKey, buildInside.ToVector3() + UnityEngine.Vector3.one * 0.5f);
                return true;
            }

            var unitCard = SafeGetCard<CardUnit>(deviceKey);
            if (unitCard == null)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Build failed: device key does not resolve to a block or unit card: " + deviceKey);
                return false;
            }

            UnityEngine.Debug.Log("[BNL-Bots] Building unit device " + unitCard.Id + " at " + buildInside
                + " initEffects=" + (unitCard.InitEffects != null ? unitCard.InitEffects.Count : 0)
                + " enabledEffects=" + (unitCard.EnabledEffects != null ? unitCard.EnabledEffects.Count : 0));

            var transform = BuildOfflineDeviceTransform(deviceKey, builder.Team, buildInside, buildOutside, direction);
            var worldPos = transform.Position;
            transform.NoInterpolation = true;

            var builtUnitId = nextOfflineDeviceUnitId++;
            cachedListener.UnitCreate(builtUnitId, new UnitInit
            {
                Key = deviceKey,
                Team = builder.Team,
                Controlled = false,
                OwnerId = builder.PlayerId,
                Transform = transform,
            });
            cachedListener.UnitUpdate(builtUnitId, new UnitUpdate
            {
                Health = GetUnitMaxHealth(deviceKey),
                MovementActive = true,
            });
            NotifyDeviceBuilt(builder, deviceKey, worldPos);
            return true;
        }

        // Call UnitHandler.DeviceBuilt directly on the builder: its broadcast clears the
        // build ghost and plays the built sound BEFORE the ZoneNotifications handler that
        // NREs offline — so the exception can be safely swallowed after the cleanup ran.
        private static bool deviceBuiltWarned;
        private static void NotifyDeviceBuilt(Unit builder, Key deviceKey, UnityEngine.Vector3 position)
        {
            try
            {
                builder.GetComponent<UnitHandler>().DeviceBuilt(deviceKey, position);
            }
            catch (Exception ex)
            {
                if (!deviceBuiltWarned)
                {
                    deviceBuiltWarned = true;
                    UnityEngine.Debug.LogWarning("[BNL-Bots] DeviceBuilt notification failed (ignored, logged once): " + ex.Message);
                }
            }
        }

        private static Vector3s ToBlockPos(UnityEngine.Vector3 point)
        {
            return new Vector3s(
                (short)UnityEngine.Mathf.FloorToInt(point.x),
                (short)UnityEngine.Mathf.FloorToInt(point.y),
                (short)UnityEngine.Mathf.FloorToInt(point.z));
        }

        private static ZoneTransform BuildOfflineDeviceTransform(Key deviceKey, TeamType team, Vector3s buildInside, Vector3s buildOutside, Direction2D direction)
        {
            BuildGhostObject ghost = null;
            try
            {
                ghost = BuildGhostObject.Create(deviceKey, false, team);
                if (ghost != null)
                {
                    ghost.SetBlockPosition(buildInside, buildOutside, direction);
                    return new ZoneTransform
                    {
                        Position = ghost.transform.position,
                        Rotation = ZoneTransformHelper.ToVector3s(ghost.transform.rotation),
                    };
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] BuildOfflineDeviceTransform failed: " + ex.Message);
            }
            finally
            {
                if (ghost != null)
                    UnityEngine.Object.Destroy(ghost.gameObject);
            }

            var fallback = MakeTransform(GetOfflineBuiltUnitPosition(null, buildInside, buildOutside));
            fallback.Rotation = DirectionToRotation(direction);
            return fallback;
        }

        private static Vector3s GetBuildPlacementBlock(HitData hit)
        {
            var supportBlock = ToBlockPos(hit.InsidePoint);
            var normal = hit.Normal.ToVector3();
            if (normal == UnityEngine.Vector3.zero)
                return supportBlock;

            var targetPoint = hit.InsidePoint + normal * 0.51f;
            return ToBlockPos(targetPoint);
        }

        private static bool TryGetCurrentBuildPlacement(Unit builder, BuildInfo buildInfo, HitData hit, out Vector3s buildInside, out Vector3s supportBlock, out Direction2D direction)
        {
            if (HasBuildPosition(buildInfo.BuildInsidePosition) || HasBuildPosition(buildInfo.BuildOutsidePosition))
            {
                supportBlock = buildInfo.BuildInsidePosition;
                buildInside = buildInfo.BuildOutsidePosition;
                direction = buildInfo.Direction;
                return true;
            }

            if (hit == null)
            {
                supportBlock = Vector3s.zero;
                buildInside = Vector3s.zero;
                direction = buildInfo.Direction;
                return false;
            }

            supportBlock = ToBlockPos(hit.InsidePoint);
            buildInside = GetBuildPlacementBlock(hit);
            direction = hit.Direction ?? buildInfo.Direction;

            // The build Hit arrives the same frame Place() ran, so re-running the ghost
            // placement here reproduces exactly what the preview showed (BuildHelper
            // applies floor-attach / snap rules a raw hit-normal offset doesn't).
            try
            {
                var ghost = builder.GetComponent<BuildGhostController>();
                if (ghost != null)
                {
                    var buildData = ghost.TryPlaceDevice();
                    if (buildData.Ri != null)
                    {
                        var ri = buildData.Ri.Value;
                        buildInside = ri.BlockPosBuildIn;
                        supportBlock = ri.BlockPosBuildOn;
                        direction = ri.Direction;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Ghost placement refinement failed: " + ex.Message);
            }

            return true;
        }

        private static bool HasBuildPosition(Vector3s pos)
        {
            return pos.x != 0 || pos.y != 0 || pos.z != 0;
        }

        private static UnityEngine.Vector3 GetOfflineBuiltUnitPosition(CardUnit unitCard, Vector3s buildInside, Vector3s buildOutside)
        {
            var pos = buildInside.ToVector3() + UnityEngine.Vector3.one * 0.5f;
            if (unitCard != null && unitCard.GroundOnly)
                pos.y = UnityEngine.Mathf.Floor(pos.y) + 0.01f;

            if (unitCard != null && unitCard.Data != null && unitCard.Data.Type == UnitType.Turret)
            {
                // Turrets attach onto the face they were built against, so bias slightly out of the host block.
                var delta = (buildInside - buildOutside).ToVector3().normalized;
                if (delta != UnityEngine.Vector3.zero)
                    pos += delta * 0.15f;
            }

            return pos;
        }

        private static Vector3s DirectionToRotation(Direction2D direction)
        {
            switch (direction)
            {
                case Direction2D.Left:
                    return new Vector3s(0, 270, 0);
                case Direction2D.Right:
                    return new Vector3s(0, 90, 0);
                case Direction2D.Back:
                    return new Vector3s(0, 180, 0);
                default:
                    return new Vector3s(0, 0, 0);
            }
        }

        // Fallback for AssetCache.LoadPrefab when bundles aren't loaded — use Resources.Load instead.
        // For bundle-only assets that are already in sharedassets, use FindObjectsOfTypeAll.
        public static UnityEngine.GameObject FallbackLoadPrefab(string name)
        {
            // BlockMaterials: use FindObjectsOfTypeAll to get the already-loaded sharedassets version.
            if (string.Equals(name, "Prefabs/MapMaterials/BlockMaterials", StringComparison.Ordinal))
            {
                var all = UnityEngine.Resources.FindObjectsOfTypeAll<BlockMaterials>();
                if (all != null && all.Length > 0)
                {
                    UnityEngine.Debug.Log("[BNL-Bots] FallbackLoadPrefab: found BlockMaterials via FindObjectsOfTypeAll");
                    return all[0].gameObject;
                }
                UnityEngine.Debug.LogWarning("[BNL-Bots] FallbackLoadPrefab: BlockMaterials not found via FindObjectsOfTypeAll");
                return null;
            }

            // MapRender prefabs (skybox/lighting settings): find an existing MapRenderSettings in scene.
            if (name.StartsWith("Prefabs/MapRender/", StringComparison.Ordinal))
            {
                // Try Resources.Load first (some render settings may be in Resources/).
                var loaded = UnityEngine.Resources.Load<UnityEngine.GameObject>(name);
                if (loaded != null)
                {
                    UnityEngine.Debug.Log("[BNL-Bots] FallbackLoadPrefab: found MapRender via Resources.Load: " + name);
                    return loaded;
                }
                var existing = UnityEngine.Resources.FindObjectsOfTypeAll<MapRenderSettings>();
                UnityEngine.Debug.Log("[BNL-Bots] FallbackLoadPrefab: FindObjectsOfTypeAll<MapRenderSettings> count=" + (existing != null ? existing.Length.ToString() : "null") + " for " + name);
                if (existing != null && existing.Length > 0)
                    return existing[0].gameObject;
                return null;
            }

            // MapPlane prefabs (water/lava): UpdatePlane is patched to no-op, but log any attempt.
            if (name.StartsWith("Prefabs/MapPlane/", StringComparison.Ordinal))
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] FallbackLoadPrefab: MapPlane not available offline: " + name);
                return null;
            }

            var go = UnityEngine.Resources.Load<UnityEngine.GameObject>(name);
            if (go != null)
                return go;

            UnityEngine.Debug.LogWarning("[BNL-Bots] FallbackLoadPrefab: not found in Resources: " + name);
            return null;
        }

        public static int GetOfflineBlockMaterialsCount()
        {
            return 1;
        }

        public static int GetOfflineBlockMaterialIndex(ushort blockId)
        {
            return 0;
        }

        public static UnityEngine.Material GetOfflineBlockMaterial(int index)
        {
            if (offlineBlockMaterial == null)
            {
                var shader =
                    UnityEngine.Shader.Find("Diffuse") ??
                    UnityEngine.Shader.Find("Standard") ??
                    UnityEngine.Shader.Find("Particles/Alpha Blended");
                if (shader == null)
                    throw new InvalidOperationException("No fallback shader available for offline block material.");

                offlineBlockMaterial = new UnityEngine.Material(shader);
                offlineBlockMaterial.name = "OfflineBlockMaterial";
                offlineBlockMaterial.color = UnityEngine.Color.white;
                UnityEngine.Debug.Log("[BNL-Bots] Created offline fallback block material.");
            }

            return offlineBlockMaterial;
        }

        // Called by GuiRelogin.Update() patch — suppress the disconnect popup in offline mode.
        public static bool ShouldSuppressDisconnectUi()
        {
            return IsEnabled();
        }

        // Called by CustomGameData.CreateGame() patch — start local match instead of contacting server.
        public static bool TryCreateOfflineGame()
        {
            if (!IsEnabled()) return false;

            UnityEngine.Debug.Log("[BNL-Bots] TryCreateOfflineGame — triggering local match via SceneManager.");
            try
            {
                var sm = Singleton<SceneManager>.Instance;
                if (sm == null) { UnityEngine.Debug.LogWarning("[BNL-Bots] SceneManager null."); return true; }

                // Pick a valid MatchKey and GameMode to avoid catalogue lookup crashes.
                var matchKey = Key.None;
                var gameModeKey = Key.None;
                try
                {
                    // Find any non-tutorial, non-time-trial CardMatch
                    var allCards = Singleton<Catalogue>.Instance.All;
                    foreach (var c in allCards)
                    {
                        var cm = c as CardMatch;
                        if (cm != null && !(cm.Data is MatchDataTutorial) && !(cm.Data is MatchDataTimeTrial))
                        { matchKey = cm.Key; break; }
                    }
                    gameModeKey = CatalogueHelper.ModeCustom?.Key ?? CatalogueHelper.ModeFriendly?.Key ?? Key.None;
                }
                catch { }

                EnsureOfflinePlayerIdentity();

                var scene = new SceneZone
                {
                    Restart = false,
                    MatchKey = matchKey,
                    GameMode = gameModeKey,
                    MyTeam = TeamType.Team1,
                    IsSpectator = false,
                    IsMapEditor = false,
                };
                sm.ServerChangeScene(scene);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] TryCreateOfflineGame error: " + ex.Message);
            }

            return true;
        }


        public static void OnLoginBypassed()
        {
            UnityEngine.Debug.Log("[BNL-Bots] Login bypassed — offline bot mode active.");
            try
            {
                LoadCatalogueFromCacheNow();

                var sm = Singleton<SceneManager>.Instance;
                if (sm != null)
                    sm.ServerChangeScene(new SceneMainMenu());
                else
                    UnityEngine.Debug.LogWarning("[BNL-Bots] SceneManager not ready yet.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnLoginBypassed error: " + ex.Message);
            }
        }

        private static void LoadCatalogueFromCacheNow()
        {
            try
            {
                if (!File.Exists(CatalogueCache.CachePath))
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] Cache/cdb not found at: " + CatalogueCache.CachePath);
                    return;
                }

                var raw = Singleton<Catalogue>.Instance;
                UnityEngine.Debug.Log("[BNL-Bots] Catalogue instance: " + (raw == null ? "null" : raw.GetType().FullName));

                var catalogue = raw as IServiceCatalogueListener;
                if (catalogue == null) { UnityEngine.Debug.LogWarning("[BNL-Bots] Catalogue not IServiceCatalogueListener."); return; }

                using (var zlib = ZLibHelper.UnZip(CatalogueCache.Load()))
                using (var reader = new System.IO.BinaryReader(zlib))
                {
                    reader.ReadByte(); // service function id (0 = Replicate)
                    var cards = Igor.Read.List<Card>(Card.ReadVariant)(reader);
                    catalogue.Replicate(cards);
                    UnityEngine.Debug.Log("[BNL-Bots] Catalogue loaded: " + cards.Count + " cards.");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] LoadCatalogueFromCacheNow failed: " + ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IL hook 2 — SceneManager.ServerLoadZone(SceneZone scene)
        // ─────────────────────────────────────────────────────────────────────────
        public static bool TryInterceptLoadZone(object sceneObj)
        {
            if (!IsEnabled()) return false;

            UnityEngine.Debug.Log("[BNL-Bots] Intercepting ServerLoadZone — starting local match.");

            // Defer match bootstrap to ZoneServiceListener.Start() (IL hook 3).
            // Let the original ServerLoadZone proceed to load the Zone scene properly —
            // we only intercept it to set the pending flag, then return false so the
            // original method continues its loader chain (LoadLevelLoader("Zone") etc.).
            pendingMatchStart = true;
            return false;
        }

        private static bool pendingMatchStart;

        // ─────────────────────────────────────────────────────────────────────────
        // IL hook 3 — ZoneServiceListener.Start()
        // ─────────────────────────────────────────────────────────────────────────
        public static void ProbeZoneListenerStart()
        {
            UnityEngine.Debug.Log("[BNL-Bots] ZoneServiceListener.Start() called.");
        }

        // Called from SceneLoaderData.Begin patch — logs when the loader chain starts.
        public static void ProbeLoaderBegin(int scene)
        {
            UnityEngine.Debug.Log("[BNL-Bots] SceneLoaderData.Begin() called, scene=" + scene);
        }

        // Called from SceneManager.OnLevelWasLoaded patch.
        public static void ProbeLevelLoaded(int levelId)
        {
            UnityEngine.Debug.Log("[BNL-Bots] OnLevelWasLoaded: levelId=" + levelId + " loadedLevel=" + UnityEngine.Application.loadedLevelName);
        }

        public static void ProbeLoadLevel(string sceneName)
        {
            UnityEngine.Debug.Log("[BNL-Bots] Application.LoadLevel called: " + sceneName);
        }

        public static void RegisterListener(ZoneServiceListener listener)
        {
            cachedListener = listener;
            UnityEngine.Debug.Log("[BNL-Bots] ZoneServiceListener registered.");

            if (pendingMatchStart)
            {
                pendingMatchStart = false;
                StartLocalMatch();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IL hook 4 — ZoneManager.Update()
        // ─────────────────────────────────────────────────────────────────────────
        public static void Tick()
        {
            if (!IsEnabled()) return;

            // Deferred spawn: wait until the map geometry coroutine finishes.
            if (spawnPending && cachedListener != null)
            {
                var zm = Singleton<ZoneManager>.Instance;
                bool created = zm != null && zm.MapCreated;
                if (created)
                {
                    spawnPending = false;
                    DoSpawnUnits(pendingMapData, pendingPlayerHeroKey);
                }
                return;
            }

            if (!matchRunning) return;

            // Retry UpdateZone until ZoneData confirms Playing phase (handles timing race with loader coroutine).
            if (phaseUpdateRetries > 0)
            {
                var zd = Singleton<ZoneData>.Instance;
                if (zd == null || zd.Phase == null || zd.Phase.PhaseType == ZonePhaseType.Waiting)
                {
                    cachedListener.UpdateZone(BuildInitialZoneUpdate(pendingMapData, pendingPlayerHeroKey));
                    phaseUpdateRetries--;
                    UnityEngine.Debug.Log("[BNL-Bots] Retrying UpdateZone (phase=" + (zd != null && zd.Phase != null ? zd.Phase.PhaseType.ToString() : "null") + " retries_left=" + phaseUpdateRetries + ")");
                }
                else
                {
                    phaseUpdateRetries = 0;
                }
            }

            tickAccumulator += UnityEngine.Time.deltaTime;
            if (tickAccumulator < BotTickInterval) return;
            tickAccumulator -= BotTickInterval;
            ProcessRespawns();
            SimulateWorld();
            SimulateRegens(UnityEngine.Time.realtimeSinceStartup);
            SimulateBotProjectiles(UnityEngine.Time.realtimeSinceStartup);
            EnsureOfflineVisualsAndCamera();
            TickBots();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Match bootstrap — Phase 2 core
        // ─────────────────────────────────────────────────────────────────────────
        private static void StartLocalMatch()
        {
            if (cachedListener == null)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Cannot start local match: listener is null.");
                return;
            }

            bots.Clear();
            botHeroKeys.Clear();
            respawnQueue.Clear();
            recentDamagers.Clear();
            objectiveUnitIds.Clear();
            pendingBuilds.Clear();
            auraNextFire.Clear();
            activePickups.Clear();
            activeRegens.Clear();
            botProjectiles.Clear();
            matchRunning = false;

            try
            {
                EnsureOfflinePlayerIdentity();

                // 1. Pick a map from the catalogue.
                var sourceMapData = PickMap();
                if (sourceMapData == null)
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] No map available in catalogue. Aborting.");
                    return;
                }

                pendingSourceMapData = sourceMapData;
                var mapData = BuildOfflineMapData(sourceMapData);

                // 2. Build and send ZoneInitData — starts the async map-creation coroutine.
                UnityEngine.Debug.Log("[BNL-Bots] Map: Size=" + mapData.Size.x + "x" + mapData.Size.y + "x" + mapData.Size.z + " BlocksData=" + (mapData.BlocksData != null ? mapData.BlocksData.Length + " bytes" : "null"));
                var initData = BuildZoneInitData(mapData);
                cachedListener.InitZone(initData);
                UnityEngine.Debug.Log("[BNL-Bots] InitZone sent.");

                // Kill height: the lava/acid plane surface if the map has one, else the fall limit.
                var props = initData.Map != null ? initData.Map.Properties : null;
                if (props != null)
                    offlineKillY = !string.IsNullOrEmpty(props.Plane) ? props.PlanePosition : props.KillPosition;
                UnityEngine.Debug.Log("[BNL-Bots] Kill height: " + offlineKillY + " (plane=" + (props != null ? props.Plane : "?") + ")");

                // 3. Send UpdateZone immediately so ZoneWaitingPhaseLoader unblocks and the scene finishes loading.
                var heroKeys = GetAvailableHeroKeys();
                pendingPlayerHeroKey = GetPreferredHeroKey(heroKeys);
                cachedListener.UpdateZone(BuildInitialZoneUpdate(mapData, pendingPlayerHeroKey));
                phaseUpdateRetries = 30;

                // 4. Defer unit spawning until ZoneManager.MapCreated == true (map geometry ready).
                pendingMapData = initData.Map;
                spawnPending = true;
                UnityEngine.Debug.Log("[BNL-Bots] Waiting for map to finish loading before spawning units...");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[BNL-Bots] StartLocalMatch failed: " + ex);
            }
        }

        private static void DoSpawnUnits(Protocol.MapData mapData, Key playerHeroKey)
        {
            try
            {
                UnityEngine.Debug.Log("[BNL-Bots] Map ready — spawning units.");

                var playerSpawn = GetSpawnPosition(mapData, TeamType.Team1);
                SpawnPlayerUnit(playerSpawn, playerHeroKey);

                var count = UnityEngine.Mathf.Clamp(botCount, 1, 9);
                int spawned = 0;
                for (int i = 0; i < count; i++)
                {
                    var botSpawn = GetSpawnPosition(mapData, TeamType.Team2);
                    var botId = BotUnitIdBase + (uint)i;
                    try
                    {
                        SpawnBotUnit(botId, playerHeroKey, botSpawn);
                        bots.Add(new BotController(botId, difficulty));
                        spawned++;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning("[BNL-Bots] Bot " + i + " spawn failed (skipped): " + ex.Message);
                    }
                }
                UnityEngine.Debug.Log("[BNL-Bots] Spawned " + spawned + "/" + count + " bots.");

                SpawnMapUnits(mapData);

                matchRunning = true;
                EnsureOfflineVisualsAndCamera();
                UnityEngine.Debug.Log("[BNL-Bots] Local match started — " + count + " bot(s), difficulty=" + difficulty);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[BNL-Bots] DoSpawnUnits failed: " + ex);
            }
        }

        // Spawn the map's static units — objective cubes, base turrets, etc.
        // The live server does this via UnitCreate; MapData.Units carries the layout.
        private static void SpawnMapUnits(Protocol.MapData mapData)
        {
            objectiveUnitIds.Clear();
            if (mapData == null || mapData.Units == null || mapData.Units.Count == 0)
            {
                UnityEngine.Debug.Log("[BNL-Bots] No map units to spawn.");
                return;
            }

            uint id = MapUnitIdBase;
            int spawned = 0, objectives = 0;
            foreach (var mu in mapData.Units)
            {
                try
                {
                    if (mu == null)
                        continue;

                    var card = SafeGetCard<CardUnit>(mu.UnitKey);
                    if (card == null || card.Data is UnitDataPlayer)
                        continue;

                    // Skip logical markers: supply-drop / block-buster drop points and
                    // pickup spawners are server bookkeeping, not physical units.
                    bool isMarker = false;
                    try { isMarker = card.IsDropPoint || card.Data is UnitDataPickup; }
                    catch { }
                    if (isMarker)
                        continue;

                    var unitId = id++;
                    cachedListener.UnitCreate(unitId, new UnitInit
                    {
                        Key = mu.UnitKey,
                        Team = mu.Team,
                        Controlled = false,
                        Transform = new ZoneTransform
                        {
                            Position = mu.Position,
                            Rotation = mu.Rotation,
                            NoInterpolation = true,
                        },
                    });
                    cachedListener.UnitUpdate(unitId, new UnitUpdate
                    {
                        Health = GetUnitMaxHealth(mu.UnitKey),
                        MovementActive = false,
                    });
                    spawned++;

                    if (card.IsObjective)
                    {
                        objectiveUnitIds.Add(unitId);
                        objectives++;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] Map unit spawn failed (" + (mu != null ? mu.UnitKey.ToString() : "null") + "): " + ex.Message);
                }
            }

            UnityEngine.Debug.Log("[BNL-Bots] Spawned " + spawned + " map units (" + objectives + " objectives).");
            objectiveShieldState.Clear();
            UpdateObjectiveShields();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Map selection — prefers Friendly pool, falls back to Tutorial
        // ─────────────────────────────────────────────────────────────────────────
        private static MapData PickMap()
        {
            try
            {
                var mapList = CatalogueHelper.MapList;
                if (mapList == null) return null;

                // Try Friendly maps first (proper 2-team layout).
                if (mapList.Friendly != null && mapList.Friendly.Count > 0)
                {
                    var key = mapList.Friendly[0];
                    var card = Singleton<Catalogue>.Instance.GetCard<CardMap>(key);
                    if (card?.Data != null) return card.Data;
                }

                // Fall back to Tutorial map (guaranteed to exist offline).
                if (mapList.Tutorial != Key.None)
                {
                    var card = Singleton<Catalogue>.Instance.GetCard<CardMap>(mapList.Tutorial);
                    if (card?.Data != null) return card.Data;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] PickMap error: " + ex.Message);
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ZoneInitData construction
        // ─────────────────────────────────────────────────────────────────────────
        private static string GetPatchingDir()
        {
            // Try assembly location first (works if DLL is on disk)
            var loc = typeof(BotModeRuntime).Assembly.Location;
            if (!string.IsNullOrEmpty(loc))
            {
                var dir = System.IO.Path.GetDirectoryName(loc);
                if (!string.IsNullOrEmpty(dir)) return dir;
            }
            // Fallback: %LOCALAPPDATA%\BNL-CommunityFixes\app\patching
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(System.IO.Path.Combine(appData, "BNL-CommunityFixes"), System.IO.Path.Combine("app", "patching"));
        }

        private static ZoneInitData BuildZoneInitData(MapData mapData)
        {
            byte[] blockData = null;
            byte[] colorData = null;

            var dir = GetPatchingDir();
            var blocksPath = System.IO.Path.Combine(dir, "bot_map_blocks.bin");
            var colorsPath = System.IO.Path.Combine(dir, "bot_map_colors.bin");

            if (System.IO.File.Exists(blocksPath) && System.IO.File.Exists(colorsPath))
            {
                blockData = System.IO.File.ReadAllBytes(blocksPath);
                colorData = System.IO.File.ReadAllBytes(colorsPath);
                usingBundledMap = true;
                UnityEngine.Debug.Log("[BNL-Bots] Using bundled map binary: blocks=" + blockData.Length + " colors=" + colorData.Length);
            }
            else
            {
                usingBundledMap = false;
                blockData = BuildMinimalMapBinary(mapData);
                colorData = BuildMinimalColorBinary(mapData);
                UnityEngine.Debug.Log("[BNL-Bots] Using generated offline map binary (bundled files not found at: " + blocksPath + ")");
            }

            // If using bundled map, override MapData size and spawns to match the bundled map geometry.
            if (usingBundledMap)
            {
                mapData = OverrideMapDataForBundledMap(mapData, blockData);
            }

            return new ZoneInitData
            {
                Map = mapData,
                MapData = blockData,
                ColorData = colorData,
                Updates = new System.Collections.Generic.Dictionary<Vector3s, BlockUpdate>(),
                CanSwitchHero = false,
                IsCustomGame = true,
            };
        }

        // Read the actual size from the zlib-compressed block binary header.
        private static Vector3s ReadMapSizeFromBinary(byte[] zlibData)
        {
            try
            {
                using (var decompressed = ZLibHelper.UnZip(zlibData))
                {
                    var buf = new byte[6];
                    decompressed.Read(buf, 0, 6);
                    int sx = buf[0] | (buf[1] << 8);
                    int sy = buf[2] | (buf[3] << 8);
                    int sz = buf[4] | (buf[5] << 8);
                    return new Vector3s(sx, sy, sz);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] ReadMapSizeFromBinary failed: " + ex.Message + " — using default 256x48x88");
                return new Vector3s(256, 48, 88);
            }
        }

        private static MapData OverrideMapDataForBundledMap(MapData mapData, byte[] blockData)
        {
            var size = ReadMapSizeFromBinary(blockData);
            UnityEngine.Debug.Log("[BNL-Bots] Bundled map size decoded: " + size.x + "x" + size.y + "x" + size.z);
            mapData.Size = size;

            // The catalogue map we happened to pick is usually NOT the map the bundled
            // binary was extracted from — its spawns/units then land inside/above the
            // wrong geometry. Only adopt a layout from a catalogue map whose Size
            // matches the bundled binary exactly.
            var source = FindCatalogueMapBySize(size);
            if (source != null && source.SpawnPoints != null && source.SpawnPoints.Count > 0)
            {
                mapData.SpawnPoints = new List<MapSpawnPoint>(source.SpawnPoints);
                UnityEngine.Debug.Log("[BNL-Bots] Using matching catalogue spawn points: " + mapData.SpawnPoints.Count);
            }
            else
            {
                // Known spawn points for map_sr2_mountain_express (256x48x88)
                mapData.SpawnPoints = new List<MapSpawnPoint>
                {
                    new MapSpawnPoint { Team = TeamType.Team1, Position = new UnityEngine.Vector3(59.5f, 12f, 55.5f), Direction = Direction2D.Front, Label = SpawnPointLabel.Base },
                    new MapSpawnPoint { Team = TeamType.Team2, Position = new UnityEngine.Vector3(196.5f, 12f, 55.5f), Direction = Direction2D.Back, Label = SpawnPointLabel.Base },
                };
            }

            if (source != null && source.Units != null && source.Units.Count > 0)
            {
                mapData.Units = new List<MapUnit>(source.Units);
                UnityEngine.Debug.Log("[BNL-Bots] Using matching catalogue map units: " + mapData.Units.Count);
            }

            // Adopt the matched map's plane (lava/acid/water) so visuals and the kill
            // height simulation agree with the real map.
            if (source != null && source.Properties != null)
            {
                mapData.Properties.Plane = source.Properties.Plane ?? string.Empty;
                mapData.Properties.PlanePosition = source.Properties.PlanePosition;
                if (!string.IsNullOrEmpty(source.Properties.Render))
                    mapData.Properties.Render = source.Properties.Render;
            }
            else
            {
                mapData.Units = new List<MapUnit>();
                UnityEngine.Debug.Log("[BNL-Bots] No catalogue map matches bundled size — skipping map units.");
            }

            mapData.Properties.ResetBarriers(size.x);
            mapData.Properties.KillPosition = -8f;
            return mapData;
        }

        // Find the catalogue map the bundled binary was extracted from by exact size match.
        private static MapData FindCatalogueMapBySize(Vector3s size)
        {
            try
            {
                MapData match = null;
                int matches = 0;
                foreach (var card in Singleton<Catalogue>.Instance.All)
                {
                    var cm = card as CardMap;
                    var data = cm?.Data;
                    if (data == null)
                        continue;
                    if (data.Size.x != size.x || data.Size.y != size.y || data.Size.z != size.z)
                        continue;

                    matches++;
                    if (match == null || (data.Units != null && data.Units.Count > (match.Units != null ? match.Units.Count : 0)))
                        match = data;
                }

                UnityEngine.Debug.Log("[BNL-Bots] Catalogue maps matching bundled size: " + matches);
                return match;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] FindCatalogueMapBySize failed: " + ex.Message);
                return null;
            }
        }

        private static MapData BuildOfflineMapData(MapData sourceMapData)
        {
            var mapData = new MapData
            {
                Version = sourceMapData != null ? sourceMapData.Version : 0,
                Schema = sourceMapData != null ? sourceMapData.Schema : 6,
                Match = sourceMapData != null ? sourceMapData.Match : default(MatchType),
                Size = new Vector3s(OfflineMapSizeX, OfflineMapSizeY, OfflineMapSizeZ),
                Properties = CloneOfflineMapProps(sourceMapData != null ? sourceMapData.Properties : null),
                ColorPalette = BuildOfflineColorPalette(sourceMapData),
                SpawnPoints = BuildOfflineSpawnPoints(),
                Units = new List<MapUnit>(),
                Cameras = new List<MapCamera2>(),
                Triggers = new List<MapTrigger2>(),
                BlocksData = null,
                ColorsData = null,
            };

            UnityEngine.Debug.Log("[BNL-Bots] Using synthetic offline map data: size=" + mapData.Size.x + "x" + mapData.Size.y + "x" + mapData.Size.z + " spawns=" + mapData.SpawnPoints.Count);
            return mapData;
        }

        private static MapDataProps CloneOfflineMapProps(MapDataProps source)
        {
            var props = new MapDataProps();
            if (source != null)
            {
                props.AudioAmbience = source.AudioAmbience ?? string.Empty;
                props.BuildTime = source.BuildTime;
            }
            props.StartingResources = 9999f;

            props.Render = (source != null && !string.IsNullOrEmpty(source.Render)) ? source.Render : "DaytimeWarm";
            props.Plane = source != null ? source.Plane ?? string.Empty : string.Empty;
            props.PlanePosition = source != null ? source.PlanePosition : -1f;
            props.KillPosition = -8f;
            props.MinFallHeight = 3f;
            props.MaxFallHeight = 12f;
            props.ResetBarriers(OfflineMapSizeX);
            return props;
        }

        private static List<UnityEngine.Color32> BuildOfflineColorPalette(MapData sourceMapData)
        {
            if (sourceMapData != null && sourceMapData.ColorPalette != null && sourceMapData.ColorPalette.Count > 0)
                return new List<UnityEngine.Color32>(sourceMapData.ColorPalette);

            return new List<UnityEngine.Color32>
            {
                new UnityEngine.Color32(255, 255, 255, 255),
            };
        }

        private static List<MapSpawnPoint> BuildOfflineSpawnPoints()
        {
            return new List<MapSpawnPoint>
            {
                new MapSpawnPoint
                {
                    Team = TeamType.Team1,
                    Position = new UnityEngine.Vector3(8.5f, OfflineSpawnY, 8.5f),
                    Direction = Direction2D.Front,
                    Label = SpawnPointLabel.Base,
                },
                new MapSpawnPoint
                {
                    Team = TeamType.Team2,
                    Position = new UnityEngine.Vector3(23.5f, OfflineSpawnY, 23.5f),
                    Direction = Direction2D.Back,
                    Label = SpawnPointLabel.Base,
                },
            };
        }

        // Replace prefab-only blocks with a generic solid cube so offline mode can build
        // regular-map geometry without the original asset bundles.
        private static byte[] SanitizeMapBinary(byte[] binary)
        {
            if (binary == null || binary.Length == 0)
                return binary;

            try
            {
                ushort solidId = FindSolidBlockId();
                int replaced = 0;

                using (var input = ZLibHelper.UnZip(binary))
                using (var reader = new System.IO.BinaryReader(input))
                using (var raw = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(raw))
                {
                    ushort sx = reader.ReadUInt16();
                    ushort sy = reader.ReadUInt16();
                    ushort sz = reader.ReadUInt16();

                    writer.Write(sx);
                    writer.Write(sy);
                    writer.Write(sz);

                    int total = sx * sy * sz;
                    for (int i = 0; i < total; i++)
                    {
                        ushort id = reader.ReadUInt16();
                        byte damage = reader.ReadByte();
                        ushort vdata = reader.ReadUInt16();
                        byte ldata = reader.ReadByte();

                        if (ShouldReplacePrefabBlock(id))
                        {
                            id = solidId;
                            damage = 0;
                            vdata = 0;
                            ldata = 0;
                            replaced++;
                        }

                        writer.Write(id);
                        writer.Write(damage);
                        writer.Write(vdata);
                        writer.Write(ldata);
                    }

                    writer.Flush();
                    if (replaced > 0)
                        UnityEngine.Debug.Log("[BNL-Bots] Sanitized bundled map prefab blocks: " + replaced);

                    return ZLibHelper.Zip(raw.ToArray(), 1).ToArray();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Failed to sanitize bundled map: " + ex.Message);
                return binary;
            }
        }

        private static bool ShouldReplacePrefabBlock(ushort blockId)
        {
            if (blockId == 0)
                return false;

            try
            {
                CardBlock card = BlockCardsCache.GetCard(blockId);
                return card != null &&
                       card.Visual != null &&
                       card.Visual.Type == BlockVisualType.Prefab;
            }
            catch
            {
                return false;
            }
        }

        // Find a usable solid terrain block ID from the catalogue.
        private static ushort FindSolidBlockId()
        {
            try
            {
                foreach (var card in Singleton<Catalogue>.Instance.All)
                {
                    var b = card as CardBlock;
                    // Must be a generic renderable block that can build without bundle-only prefabs.
                    if (b != null &&
                        b.Category == CardCategory.Block &&
                        b.BlockId > 0 &&
                        b.BlockId != 59 &&
                        b.Visual != null &&
                        b.IsVisualGeneric &&
                        b.Solid)
                    {
                        UnityEngine.Debug.Log("[BNL-Bots] Using block id=" + b.BlockId + " card=" + b.Id);
                        return b.BlockId;
                    }
                }
            }
            catch { }
            return 1;
        }

        // Build a zlib-compressed binary with a solid floor near spawn height.
        // Format: UInt16 SizeX, UInt16 SizeY, UInt16 SizeZ, then SizeX*SizeY*SizeZ*6 bytes (UInt16 Id, byte Damage, UInt16 Vdata, byte Ldata).
        private static byte[] BuildMinimalMapBinary(MapData mapData)
        {
            int sx = mapData.Size.x > 0 ? mapData.Size.x : 32;
            int sy = mapData.Size.y > 0 ? mapData.Size.y : 32;
            int sz = mapData.Size.z > 0 ? mapData.Size.z : 32;

            // Determine floor y from spawn points — place floor 2 blocks below lowest spawn.
            int floorY = 0;
            if (mapData.SpawnPoints != null && mapData.SpawnPoints.Count > 0)
            {
                int minSpawnY = int.MaxValue;
                foreach (var sp in mapData.SpawnPoints)
                    if ((int)sp.Position.y < minSpawnY) minSpawnY = (int)sp.Position.y;
                floorY = UnityEngine.Mathf.Max(0, minSpawnY - 2);
            }

            ushort solidId = FindSolidBlockId();
            UnityEngine.Debug.Log("[BNL-Bots] BuildMinimalMap: size=" + sx + "x" + sy + "x" + sz + " floorY=" + floorY + " blockId=" + solidId);

            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                bw.Write((ushort)sx);
                bw.Write((ushort)sy);
                bw.Write((ushort)sz);

                for (int x = 0; x < sx; x++)
                {
                    for (int y = 0; y < sy; y++)
                    {
                        for (int z = 0; z < sz; z++)
                        {
                            // Solid floor slab (3 blocks thick) at floorY and below
                            ushort id = (y <= floorY) ? solidId : (ushort)0;
                            bw.Write(id);        // Id (UInt16)
                            bw.Write((byte)0);   // Damage
                            bw.Write((ushort)0); // Vdata
                            bw.Write((byte)0);   // Ldata
                        }
                    }
                }

                bw.Flush();
                return ZLibHelper.Zip(ms.ToArray(), 1).ToArray();
            }
        }

        private static byte[] BuildMinimalColorBinary(MapData mapData)
        {
            int sx = mapData.Size.x > 0 ? mapData.Size.x : 32;
            int sy = mapData.Size.y > 0 ? mapData.Size.y : 32;
            int sz = mapData.Size.z > 0 ? mapData.Size.z : 32;
            int total = sx * sy * sz;
            byte[] colors = new byte[total];

            return ZLibHelper.Zip(colors, 1).ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Spawn position helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static UnityEngine.Vector3 GetSpawnPosition(MapData mapData, TeamType team)
        {
            if (mapData.SpawnPoints != null)
            {
                foreach (var sp in mapData.SpawnPoints)
                {
                    if (sp.Team == team)
                        return sp.Position;
                }
            }

            // Hardcoded spawn for bundled map (map_sr2_mountain_express)
            if (usingBundledMap)
            {
                if (team == TeamType.Team1) return new UnityEngine.Vector3(59.5f, 12f, 55.5f);
                return new UnityEngine.Vector3(196.5f, 12f, 55.5f);
            }
            // Generic fallback
            var sz2 = mapData.Size;
            return new UnityEngine.Vector3(sz2.x * 0.5f, sz2.y * 0.5f, team == TeamType.Team1 ? sz2.z * 0.2f : sz2.z * 0.8f);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Unit spawning
        // ─────────────────────────────────────────────────────────────────────────
        // Spawn points from map data can sit inside terrain — lift out of solid blocks,
        // then settle onto the ground so units never spawn buried or floating.
        private static UnityEngine.Vector3 FindSafeSpawn(UnityEngine.Vector3 pos)
        {
            try
            {
                for (int i = 0; i < 12 && (BotController.IsSolidAt(pos + UnityEngine.Vector3.up * 0.1f) || BotController.IsSolidAt(pos + UnityEngine.Vector3.up * 1.2f)); i++)
                    pos.y += 1f;

                float ground = BotController.GroundYBelow(pos);
                if (ground > -999f)
                    pos.y = ground + 0.05f;
            }
            catch { }
            return pos;
        }

        private static void SpawnPlayerUnit(UnityEngine.Vector3 pos, Key heroKey)
        {
            pos = FindSafeSpawn(pos);
            var gears = GetDefaultGears(heroKey);
            var devices = GetDefaultDevices(heroKey);
            var skinKey = GetDefaultSkinKey(heroKey);

            uint playerId = EnsureOfflinePlayerIdentity();
            UnityEngine.Debug.Log("[BNL-Bots] Spawning player unit id=" + PlayerUnitId + " playerId=" + playerId);

            var init = new UnitInit
            {
                Key = heroKey,
                Team = TeamType.Team1,
                PlayerId = playerId,
                Controlled = true,
                Transform = MakeTransform(pos),
                Gears = gears,
                SkinKey = skinKey,
            };

            cachedListener.UnitCreate(PlayerUnitId, init);
            cachedListener.UnitControl(PlayerUnitId);
            cachedListener.UnitUpdate(PlayerUnitId, new UnitUpdate
            {
                Health = GetHeroMaxHealth(heroKey),
                MovementActive = true,
                CurrentGear = gears != null && gears.Count > 0 ? (Key?)gears[0] : null,
                Resource = 9999f,
                Devices = devices,
            });
            var unit = Singleton<UnitsRegistry>.Instance != null ? Singleton<UnitsRegistry>.Instance.Get(PlayerUnitId) : null;
            if (unit != null && devices.Count > 0)
                unit.CurrentDeviceSlot = 1;
            UnityEngine.Debug.Log("[BNL-Bots] Player unit local=" + (unit != null && unit.IsMyPlayer) + " controlled=" + (unit != null && unit.Controlled));
            UnityEngine.Debug.Log("[BNL-Bots] Player unit spawned at " + pos);
        }

        private static void SpawnBotUnit(uint unitId, Key heroKey, UnityEngine.Vector3 pos)
        {
            if (heroKey == Key.None)
            {
                var keys = GetAvailableHeroKeys();
                heroKey = keys.Count > 0 ? keys[(int)(unitId % (uint)keys.Count)] : Key.None;
            }
            botHeroKeys[unitId] = heroKey;
            pos = FindSafeSpawn(pos);

            var init = new UnitInit
            {
                Key = heroKey,
                Team = TeamType.Team2,
                PlayerId = GetBotPlayerId(unitId),
                Controlled = false,
                Transform = MakeTransform(pos),
                Gears = GetDefaultGears(heroKey),
                SkinKey = GetDefaultSkinKey(heroKey),
            };

            // Spawn holding the combat weapon: the initial equip path works even without
            // animations, but a later gear switch stalls waiting on the unequip anim.
            var combatGear = SelectCombatGearKey(heroKey);
            UnityEngine.Debug.Log("[BNL-Bots] Bot " + unitId + " hero=" + (SafeGetCard<CardUnit>(heroKey)?.Id ?? "?")
                + " combatGear=" + (SafeGetCard<CardGear>(combatGear)?.Id ?? combatGear.ToString()));
            cachedListener.UnitCreate(unitId, init);
            cachedListener.UnitUpdate(unitId, new UnitUpdate
            {
                Health = GetHeroMaxHealth(heroKey),
                MovementActive = true,
                CurrentGear = combatGear != Key.None ? (Key?)combatGear : null,
            });
            UnityEngine.Debug.Log("[BNL-Bots] Bot unit " + unitId + " spawned at " + pos);
        }

        private static float nextVisualsCheck;
        private static void EnsureOfflineVisualsAndCamera()
        {
            // Renderer scans are expensive — at the 10 Hz bot tick they cause visible
            // hitches, and once-a-second is plenty for a fallback-visuals check.
            if (UnityEngine.Time.realtimeSinceStartup < nextVisualsCheck)
                return;
            nextVisualsCheck = UnityEngine.Time.realtimeSinceStartup + 1f;

            try
            {
                var registry = Singleton<UnitsRegistry>.Instance;
                if (registry == null)
                    return;

                foreach (var unit in registry.All)
                {
                    if (unit == null)
                        continue;

                    EnsureOfflineUnitVisual(unit);
                }

                var player = registry.Get(PlayerUnitId);
                if (player != null)
                    EnsureOfflineCamera(player, player.transform.Find("OfflineVisual") != null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] EnsureOfflineVisualsAndCamera failed: " + ex.Message);
            }
        }

        private static void EnsureOfflineUnitVisual(Unit unit)
        {
            if (unit.transform.Find("OfflineVisual") != null)
                return;

            var renderers = unit.GetComponentsInChildren<UnityEngine.Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    return;
            }

            var visual = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
            visual.name = "OfflineVisual";
            visual.transform.SetParent(unit.transform, false);
            visual.transform.localPosition = new UnityEngine.Vector3(0f, 1f, 0f);
            visual.transform.localRotation = UnityEngine.Quaternion.identity;
            visual.transform.localScale = new UnityEngine.Vector3(0.9f, 1.8f, 0.9f);

            var collider = visual.GetComponent<UnityEngine.Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            var renderer = visual.GetComponent<UnityEngine.Renderer>();
            if (renderer != null)
                renderer.material = GetOfflineUnitMaterial(unit.Team);
        }

        private static UnityEngine.Material GetOfflineUnitMaterial(TeamType team)
        {
            if (team == TeamType.Team1)
            {
                if (offlineUnitMaterialTeam1 == null)
                    offlineUnitMaterialTeam1 = CreateOfflineTintMaterial(new UnityEngine.Color(0.2f, 0.65f, 1f, 1f), "OfflineUnitTeam1");
                return offlineUnitMaterialTeam1;
            }

            if (offlineUnitMaterialTeam2 == null)
                offlineUnitMaterialTeam2 = CreateOfflineTintMaterial(new UnityEngine.Color(1f, 0.35f, 0.25f, 1f), "OfflineUnitTeam2");
            return offlineUnitMaterialTeam2;
        }

        private static UnityEngine.Material CreateOfflineTintMaterial(UnityEngine.Color color, string name)
        {
            var shader =
                UnityEngine.Shader.Find("Diffuse") ??
                UnityEngine.Shader.Find("Standard") ??
                UnityEngine.Shader.Find("Particles/Alpha Blended");
            if (shader == null)
                throw new InvalidOperationException("No fallback shader available for offline unit material.");

            var material = new UnityEngine.Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static void EnsureOfflineCamera(Unit player, bool needsFallbackCamera)
        {
            if (player == null)
                return;

            if (!needsFallbackCamera)
                return;

            if (offlineCamera == null)
                offlineCamera = UnityEngine.Camera.main ?? UnityEngine.Object.FindObjectOfType<UnityEngine.Camera>();

            if (offlineCamera == null)
            {
                var go = new UnityEngine.GameObject("OfflineBotCamera");
                offlineCamera = go.AddComponent<UnityEngine.Camera>();
                go.AddComponent<UnityEngine.AudioListener>();
            }

            var target = player.transform.position + new UnityEngine.Vector3(0f, 1.6f, 0f);
            var desired = target - player.transform.forward * 6f + UnityEngine.Vector3.up * 2.5f;
            offlineCamera.transform.position = desired;
            offlineCamera.transform.rotation = UnityEngine.Quaternion.LookRotation(target - desired, UnityEngine.Vector3.up);
            offlineCamera.nearClipPlane = 0.1f;
            offlineCamera.farClipPlane = 1000f;
        }

        private static Key? GetDefaultSkinKey(Key heroKey)
        {
            try
            {
                CardSkin fallback = null;
                foreach (var card in Singleton<Catalogue>.Instance.All)
                {
                    var skin = card as CardSkin;
                    if (skin == null) continue;
                    if (fallback == null) fallback = skin;
                    if (skin.HeroKey == heroKey)
                        return skin.Key;
                }
                if (fallback != null) return fallback.Key;
            }
            catch { }
            return Key.None;
        }

        private static Key GetPreferredHeroKey(List<Key> heroKeys)
        {
            if (heroKeys == null || heroKeys.Count == 0)
                return Key.None;

            try
            {
                var preferred = Singleton<PlayerData>.Instance != null ? Singleton<PlayerData>.Instance.LastPlayedHero : null;
                if (preferred != null && heroKeys.Contains(preferred.Value))
                    return preferred.Value;
            }
            catch { }

            return heroKeys[0];
        }

        private static ZoneTransform MakeTransform(UnityEngine.Vector3 pos)
        {
            return new ZoneTransform { Position = pos };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Hero / gear catalogue helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static List<Key> cachedHeroKeys;

        private static List<Key> GetAvailableHeroKeys()
        {
            if (cachedHeroKeys != null) return cachedHeroKeys;

            cachedHeroKeys = new List<Key>();
            try
            {
                var catalogue = Singleton<Catalogue>.Instance;
                if (catalogue == null) return cachedHeroKeys;

                foreach (var card in catalogue.All)
                {
                    var cu = card as CardUnit;
                    if (cu?.Data is UnitDataPlayer)
                        cachedHeroKeys.Add(cu.Key);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] GetAvailableHeroKeys error: " + ex.Message);
            }

            return cachedHeroKeys;
        }

        private static List<Key> GetDefaultGears(Key heroKey)
        {
            try
            {
                var card = Singleton<Catalogue>.Instance?.GetCard<CardUnit>(heroKey);
                var playerData = card?.Data as UnitDataPlayer;
                if (playerData?.Gears != null && playerData.Gears.Count > 0)
                    return new List<Key>(playerData.Gears);
            }
            catch { }
            return new List<Key>();
        }

        private static System.Collections.Generic.Dictionary<int, DeviceData> GetDefaultDevices(Key heroKey)
        {
            var result = new System.Collections.Generic.Dictionary<int, DeviceData>();
            try
            {
                var card = Singleton<Catalogue>.Instance?.GetCard<CardUnit>(heroKey);
                var playerData = card?.Data as UnitDataPlayer;
                if (playerData == null)
                    return result;

                var keys = new List<Key>();
                if (playerData.DefaultDevices != null)
                    keys.AddRange(playerData.DefaultDevices);
                if (playerData.SpecialDevices != null)
                {
                    foreach (var key in playerData.SpecialDevices)
                    {
                        if (!keys.Contains(key))
                            keys.Add(key);
                    }
                }

                int slot = 1;
                foreach (var deviceKey in keys)
                {
                    var deviceCard = Singleton<Catalogue>.Instance.GetCard<CardDevice>(deviceKey);
                    if (deviceCard == null)
                        continue;

                    result[slot++] = new DeviceData
                    {
                        DeviceKey = deviceKey,
                        TotalCost = deviceCard.BaseCost ?? 0f,
                        CostInc = deviceCard.CostIncPerUnit ?? 0f,
                    };
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] GetDefaultDevices failed: " + ex.Message);
            }

            return result;
        }

        // Pick the hero's gear whose primary tool is a ranged weapon with player damage
        // (falls back to highest-damage melee, then to the first gear).
        private static Key SelectCombatGearKey(Key heroKey)
        {
            var gearKeys = GetDefaultGears(heroKey);
            if (gearKeys == null || gearKeys.Count == 0)
                return Key.None;

            Key best = Key.None;
            float bestDamage = 0f;
            bool bestRanged = false;
            foreach (var gearKey in gearKeys)
            {
                var gearCard = SafeGetCard<CardGear>(gearKey);
                var tool = gearCard?.Tools != null && gearCard.Tools.Count > 0 ? gearCard.Tools[0] : null;
                float dmg = GetToolDamage(tool, OfflineDamageKind.Player);
                if (dmg <= 0f)
                    continue;

                bool ranged = tool is ToolShot || tool is ToolBurst || tool is ToolSpinup;
                if (best == Key.None || (ranged && !bestRanged) || (ranged == bestRanged && dmg > bestDamage))
                {
                    best = gearKey;
                    bestDamage = dmg;
                    bestRanged = ranged;
                }
            }

            return best != Key.None ? best : gearKeys[0];
        }

        private static float GetHeroMaxHealth(Key heroKey)
        {
            try
            {
                var card = Singleton<Catalogue>.Instance?.GetCard<CardUnit>(heroKey);
                if (card?.Health?.Health != null && card.Health.Health.MaxHealth > 0f)
                    return card.Health.Health.MaxHealth;
            }
            catch { }
            return 100f;
        }

        private static float GetUnitMaxHealth(Key unitKey)
        {
            try
            {
                var card = Singleton<Catalogue>.Instance?.GetCard<CardUnit>(unitKey);
                if (card?.Health?.Health != null && card.Health.Health.MaxHealth > 0f)
                    return card.Health.Health.MaxHealth;
            }
            catch { }

            return 100f;
        }

        private static T SafeGetCard<T>(Key key) where T : Card
        {
            if (key == Key.None)
                return null;
            try { return Singleton<Catalogue>.Instance?.GetCard<T>(key); }
            catch { return null; }
        }

        public static bool TryFireBotProjectile(Unit owner, Unit target, Tool tool, ulong shotId,
            UnityEngine.Vector3 start, UnityEngine.Vector3 end, float damage, Key? impact, bool shouldHit)
        {
            if (cachedListener == null || owner == null || tool == null)
                return false;

            ToolBullet bullet = null;
            var shot = tool as ToolShot;
            if (shot != null) bullet = shot.Bullet;
            var spinup = tool as ToolSpinup;
            if (spinup != null) bullet = spinup.Bullet;
            var burst = tool as ToolBurst;
            if (burst != null && burst.Bullet != null && burst.Bullet.Count > 0) bullet = burst.Bullet[0];

            Key projectileKey = Key.None;
            float speed = 0f;
            var projectile = bullet as ToolBulletProjectile;
            if (projectile != null)
            {
                projectileKey = projectile.ProjectileKey;
                speed = projectile.GetSpeed(0f);
            }
            // UnitProjectile bullets are server-created units (not ProjectileRegistry
            // entries) and must continue down the non-projectile fallback for now.
            if (projectileKey == Key.None || speed <= 0.01f)
                return false;

            var direction = end - start;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
                return false;
            direction /= distance;

            cachedListener.CreateProjectile(shotId, new ProjectileInfo
            {
                ProjectileKey = projectileKey,
                Transform = new ZoneTransform
                {
                    Position = start,
                    Rotation = ZoneTransformHelper.ToVector3s(UnityEngine.Quaternion.LookRotation(direction, UnityEngine.Vector3.up)),
                    NoInterpolation = true,
                },
                Speed = speed,
                OwnerUnitId = owner.Id,
                OwnerTeam = owner.Team,
            });

            botProjectiles.Add(new BotProjectile
            {
                ShotId = shotId,
                OwnerId = owner.Id,
                TargetId = target != null ? target.Id : 0,
                Start = start,
                End = end,
                StartTime = UnityEngine.Time.realtimeSinceStartup,
                TravelTime = distance / speed,
                Damage = damage,
                Impact = impact,
                ShouldHit = shouldHit,
            });
            return true;
        }

        private static void SimulateBotProjectiles(float now)
        {
            if (cachedListener == null || botProjectiles.Count == 0)
                return;

            for (int i = botProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = botProjectiles[i];
                float progress = UnityEngine.Mathf.Clamp01((now - projectile.StartTime) / projectile.TravelTime);
                var position = UnityEngine.Vector3.Lerp(projectile.Start, projectile.End, progress);
                cachedListener.MoveProjectile(projectile.ShotId, (ulong)(now * 1000f), new ZoneTransform
                {
                    Position = position,
                    NoInterpolation = false,
                });

                if (progress < 1f)
                    continue;

                cachedListener.DropProjectile(projectile.ShotId);
                if (projectile.ShouldHit && projectile.TargetId != 0)
                    ApplyOfflineUnitDamage(projectile.OwnerId, projectile.TargetId, projectile.Damage, false, projectile.Impact);
                botProjectiles.RemoveAt(i);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Offline unit damage / death / respawn (replaces the server's simulation)
        // ─────────────────────────────────────────────────────────────────────────
        public static void ApplyOfflineUnitDamage(uint sourceUnitId, uint targetUnitId, float damage, bool crit)
        {
            ApplyOfflineUnitDamage(sourceUnitId, targetUnitId, damage, crit, null);
        }

        public static void ApplyOfflineUnitDamage(uint sourceUnitId, uint targetUnitId, float damage, bool crit, Key? impact)
        {
            if (cachedListener == null || damage <= 0f)
                return;

            var registry = Singleton<UnitsRegistry>.Instance;
            var target = registry?.Get(targetUnitId);
            if (target == null || target.IsDeath)
                return;

            var source = registry.Get(sourceUnitId);

            if (source != null && source.PlayerId.HasValue && target.PlayerId.HasValue && source.Id != target.Id)
            {
                Dictionary<uint, float> damagers;
                if (!recentDamagers.TryGetValue(targetUnitId, out damagers))
                {
                    damagers = new Dictionary<uint, float>();
                    recentDamagers[targetUnitId] = damagers;
                }
                damagers[source.PlayerId.Value] = UnityEngine.Time.realtimeSinceStartup;
            }

            // Shielded objectives take no damage — feedback only (the hit sound handler
            // plays the "shielded" cue off a zero-damage event on invulnerable units).
            bool invulnerable = false;
            try { invulnerable = target.Buffs != null && target.Buffs.ContainsKey(BuffType.Invulnerability); }
            catch { }
            if (invulnerable)
                damage = 0f;

            // Impact event first — it drives all hit feedback (hit/crit sounds + effects
            // for the shooter, "hit me" cue for the victim). Live sends this per hit.
            try
            {
                var targetPos = target.transform.position + UnityEngine.Vector3.up * 1.2f;
                var dir = source != null ? (target.transform.position - source.transform.position).normalized : UnityEngine.Vector3.forward;
                var normal = new Vector3s(
                    (short)UnityEngine.Mathf.RoundToInt(-dir.x),
                    (short)UnityEngine.Mathf.RoundToInt(-dir.y),
                    (short)UnityEngine.Mathf.RoundToInt(-dir.z));
                cachedListener.Impact(new ImpactData
                {
                    InsidePoint = targetPos,
                    Normal = normal,
                    CasterUnitId = sourceUnitId,
                    CasterPlayerId = source != null ? source.PlayerId : null,
                    Impact = impact,
                    HitUnits = new List<uint> { targetUnitId },
                    ShotPos = source != null ? source.transform.position + UnityEngine.Vector3.up * 1.4f : targetPos,
                    Crit = crit,
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Impact event failed: " + ex.Message);
            }

            try
            {
                cachedListener.Damage(new DamageInfo
                {
                    TargetUnitId = targetUnitId,
                    SourceUnitId = sourceUnitId,
                    SourcePosition = source != null ? (UnityEngine.Vector3?)source.transform.position : null,
                    Impact = impact,
                    Damage = damage,
                    InitialDamage = damage,
                    Crit = crit,
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Damage event failed: " + ex.Message);
            }

            float newHealth = target.Health - damage;
            if (newHealth > 0f)
            {
                cachedListener.UnitUpdate(targetUnitId, new UnitUpdate { Health = newHealth });
                return;
            }

            KillOfflineUnit(target, source, crit);
        }

        private static void KillOfflineUnit(Unit target, Unit source, bool crit)
        {
            uint targetId = target.Id;
            var targetTeam = target.Team;
            var deathPos = target.transform.position;
            CardUnit deadCard = null;
            try { deadCard = target.UnitCard; }
            catch { }
            bool wasObjective = false;
            bool wasHero = false;
            try { wasObjective = deadCard != null && deadCard.IsObjective; }
            catch { }
            try { wasHero = deadCard != null && deadCard.Data is UnitDataPlayer; }
            catch { }

            var damageSource = Key.None;
            try
            {
                if (source != null && source.CurrentGear != null)
                    damageSource = source.CurrentGear.Key;
            }
            catch { }

            var assistants = new List<uint>();
            Dictionary<uint, float> damagers;
            if (recentDamagers.TryGetValue(targetId, out damagers))
            {
                uint? killerPlayerId = source != null ? source.PlayerId : null;
                float now = UnityEngine.Time.realtimeSinceStartup;
                foreach (var entry in damagers)
                {
                    if ((!killerPlayerId.HasValue || entry.Key != killerPlayerId.Value) && now - entry.Value <= AssistWindowSec)
                        assistants.Add(entry.Key);
                }
                recentDamagers.Remove(targetId);
            }

            try
            {
                cachedListener.Kill(new KillInfo
                {
                    DeadUnitId = targetId,
                    Dead = target.PlayerId,
                    Killer = source != null ? source.PlayerId : null,
                    Assistants = assistants,
                    DamageSource = damageSource,
                    SourcePosition = source != null ? (UnityEngine.Vector3?)source.transform.position : null,
                    Crit = crit,
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Kill event failed: " + ex.Message);
            }

            try { cachedListener.UnitDrop(targetId); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[BNL-Bots] UnitDrop failed: " + ex.Message); }

            // Players and bots respawn; map units (objectives, turrets) stay dead.
            if (targetId == PlayerUnitId || botHeroKeys.ContainsKey(targetId))
            {
                respawnQueue[targetId] = UnityEngine.Time.realtimeSinceStartup + RespawnDelaySec;
                PublishRespawnTimers();
            }

            // CDB-driven death consequences: loot pickups (heroes drop unit_pickup_hero_loot)
            // and OnDeath unit spawns (destroyed objective cubes leave debris).
            if (deathPos.y > offlineKillY + 1f)
                HandleDeathSpawns(deadCard, deathPos, targetTeam);

            // Scoreboard: track kills/deaths like the live server.
            if (wasHero)
                UpdateKillStats(source, target, assistants);

            if (wasObjective)
                OnObjectiveDestroyed(targetId, targetTeam);
        }

        private static void ProcessRespawns()
        {
            if (respawnQueue.Count == 0 || cachedListener == null || pendingMapData == null)
                return;

            List<uint> due = null;
            float now = UnityEngine.Time.realtimeSinceStartup;
            foreach (var kv in respawnQueue)
            {
                if (now < kv.Value)
                    continue;
                if (due == null)
                    due = new List<uint>();
                due.Add(kv.Key);
            }
            if (due == null)
                return;

            foreach (var unitId in due)
            {
                try
                {
                    if (unitId == PlayerUnitId)
                    {
                        SpawnPlayerUnit(GetSpawnPosition(pendingMapData, TeamType.Team1), pendingPlayerHeroKey);
                    }
                    else
                    {
                        Key heroKey;
                        botHeroKeys.TryGetValue(unitId, out heroKey);
                        SpawnBotUnit(unitId, heroKey, GetSpawnPosition(pendingMapData, TeamType.Team2));
                    }
                    respawnQueue.Remove(unitId);
                    UnityEngine.Debug.Log("[BNL-Bots] Respawned unit " + unitId);
                    PublishRespawnTimers();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] Respawn of " + unitId + " failed: " + ex.Message);
                    // Keep it queued and retry instead of permanently losing the bot.
                    respawnQueue[unitId] = UnityEngine.Time.realtimeSinceStartup + 0.5f;
                }
            }
        }

        private static void PublishRespawnTimers()
        {
            if (cachedListener == null)
                return;

            var info = new Dictionary<uint, ulong>();
            foreach (var entry in respawnQueue)
            {
                uint playerId = entry.Key == PlayerUnitId
                    ? EnsureOfflinePlayerIdentity()
                    : GetBotPlayerId(entry.Key);
                info[playerId] = (ulong)(entry.Value * 1000f);
            }
            cachedListener.UpdateZone(new ZoneUpdate { RespawnInfo = info });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // World simulation — healing auras, pickups, kill plane (server-side in live)
        // ─────────────────────────────────────────────────────────────────────────
        private static void SimulateWorld()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < nextWorldSim)
                return;
            nextWorldSim = now + WorldSimInterval;

            var registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null || cachedListener == null)
                return;

            // Snapshot — kills/pickup collection mutate the registry while we iterate.
            var units = new List<Unit>();
            try
            {
                foreach (var u in registry.All)
                    if (u != null && !u.IsDeath)
                        units.Add(u);
            }
            catch { return; }

            SimulateKillPlane(units);
            SimulateAuras(units, now);
            SimulatePickups(units);
        }

        // Runs every bot tick (0.1 s) so healing is smooth like live regen.
        private static void SimulateRegens(float now)
        {
            for (int i = activeRegens.Count - 1; i >= 0; i--)
            {
                var entry = activeRegens[i];
                if (now >= entry.EndTime)
                {
                    activeRegens.RemoveAt(i);
                    continue;
                }

                try
                {
                    var unit = Singleton<UnitsRegistry>.Instance?.Get(entry.UnitId);
                    if (unit == null || unit.IsDeath)
                    {
                        activeRegens.RemoveAt(i);
                        continue;
                    }
                    HealUnit(unit, entry.RatePerSec * BotTickInterval);
                }
                catch { }
            }
        }

        private static void SimulateKillPlane(List<Unit> units)
        {
            foreach (var unit in units)
            {
                try
                {
                    if (unit.IsDeath)
                        continue;
                    if (unit.transform.position.y <= offlineKillY)
                        KillOfflineUnit(unit, null, false);
                }
                catch { }
            }
        }

        // Healing auras (health station, etc): CardUnit.InitEffects/EnabledEffects →
        // CardEffect.Effect (ConstEffectAura). Healing comes in two shapes:
        //  - IntervalEffects (InstEffectHeal fired every aura.Interval)
        //  - ConstantEffects → CardEffect → ConstEffectBuff { HealthRegen: hp/s } while inside
        private static void SimulateAuras(List<Unit> units, float now)
        {
            foreach (var unit in units)
            {
                try
                {
                    if (unit.IsDeath || unit.UnitCard == null)
                        continue;

                    var effectKeys = new List<Key>();
                    if (unit.UnitCard.InitEffects != null) effectKeys.AddRange(unit.UnitCard.InitEffects);
                    if (unit.UnitCard.EnabledEffects != null) effectKeys.AddRange(unit.UnitCard.EnabledEffects);
                    if (effectKeys.Count == 0)
                        continue;

                    float nextFire;
                    bool intervalDue = !auraNextFire.TryGetValue(unit.Id, out nextFire) || now >= nextFire;
                    float minInterval = 0f;

                    foreach (var key in effectKeys)
                    {
                        var effectCard = SafeGetCard<CardEffect>(key);
                        var aura = effectCard?.Effect as ConstEffectAura;
                        if (aura == null)
                            continue;

                        bool hasInterval = aura.IntervalEffects != null && aura.IntervalEffects.Count > 0;
                        float regenPerSec = GetAuraHealthRegen(aura);
                        if (!hasInterval && regenPerSec <= 0f)
                            continue;

                        if (hasInterval)
                        {
                            float interval = aura.Interval > 0.05f ? aura.Interval : 1f;
                            if (minInterval <= 0f || interval < minInterval)
                                minInterval = interval;
                        }

                        foreach (var other in units)
                        {
                            if (other == null || other.IsDeath || other.Team != unit.Team)
                                continue;
                            if (UnityEngine.Vector3.Distance(unit.transform.position, other.transform.position) > aura.OuterRadius)
                                continue;

                            if (hasInterval && intervalDue)
                                foreach (var inst in aura.IntervalEffects)
                                    ApplyHealEffect(other, inst);

                            // Route through the regen list so healing ticks smoothly at
                            // the 0.1 s cadence instead of 5 hp chunks every half second.
                            if (regenPerSec > 0f)
                            {
                                AddOrRefreshRegen(other.Id, unit.Id, regenPerSec, now + WorldSimInterval + 0.2f);

                                // Show the heal effect (icon + loop sfx) while inside the aura.
                                if (aura.ConstantEffects != null && aura.ConstantEffects.Count > 0)
                                {
                                    try
                                    {
                                        ulong stampEnd = (ulong)(Singleton<IServerTime>.Instance.TimeMillis + 700L);
                                        var stamp = new Dictionary<Key, ulong?>();
                                        foreach (var ck in aura.ConstantEffects)
                                            stamp[ck] = stampEnd;
                                        cachedListener.UnitUpdate(other.Id, new UnitUpdate { Effects = stamp });
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    if (intervalDue && minInterval > 0f)
                        auraNextFire[unit.Id] = now + minInterval;
                }
                catch { }
            }
        }

        // hp/s of HealthRegen buffs carried by the aura's constant effects.
        private static float GetAuraHealthRegen(ConstEffectAura aura)
        {
            return GetConstantKeysHealthRegen(aura.ConstantEffects, 0);
        }

        // Recursive: regen buffs are often nested, e.g. the medikit's
        // effect_status_regen_health_5s → ConstEffectSelf → effect_status_regen_health.
        private static float GetConstantKeysHealthRegen(List<Key> keys, int depth)
        {
            if (keys == null || depth > 4)
                return 0f;

            float total = 0f;
            foreach (var key in keys)
            {
                var card = SafeGetCard<CardEffect>(key);
                total += GetCardEffectHealthRegen(card, depth + 1);
            }
            return total;
        }

        private static float GetCardEffectHealthRegen(CardEffect card, int depth)
        {
            if (card == null || depth > 4)
                return 0f;

            var buff = card.Effect as ConstEffectBuff;
            if (buff?.Buffs != null)
            {
                float regen;
                if (buff.Buffs.TryGetValue(BuffType.HealthRegen, out regen))
                    return regen;
                return 0f;
            }

            var self = card.Effect as ConstEffectSelf;
            if (self != null)
                return GetConstantKeysHealthRegen(self.ConstantEffects, depth + 1);

            var team = card.Effect as ConstEffectTeam;
            if (team != null)
                return GetConstantKeysHealthRegen(team.ConstantEffects, depth + 1);

            return 0f;
        }

        private static void HealUnit(Unit target, float amount)
        {
            if (target == null || target.IsDeath || amount <= 0f)
                return;

            float newHealth = UnityEngine.Mathf.Min(target.MaxHealth, target.Health + amount);
            if (newHealth > target.Health)
                cachedListener.UnitUpdate(target.Id, new UnitUpdate { Health = newHealth });
        }

        private static float GetHealAmount(InstEffect effect)
        {
            if (effect == null)
                return 0f;

            var heal = effect as InstEffectHeal;
            if (heal != null)
                return heal.PlayerHeal;

            var bunch = effect as InstEffectBunch;
            if (bunch != null && bunch.Instant != null)
            {
                float total = 0f;
                foreach (var inner in bunch.Instant)
                    total += GetHealAmount(inner);
                return total;
            }

            return 0f;
        }

        private static void ApplyHealEffect(Unit target, InstEffect effect)
        {
            if (effect == null || target == null || target.IsDeath)
                return;

            var heal = effect as InstEffectHeal;
            if (heal != null && heal.PlayerHeal > 0f)
            {
                float newHealth = UnityEngine.Mathf.Min(target.MaxHealth, target.Health + heal.PlayerHeal);
                if (newHealth > target.Health)
                    cachedListener.UnitUpdate(target.Id, new UnitUpdate { Health = newHealth });
                return;
            }

            var bunch = effect as InstEffectBunch;
            if (bunch != null && bunch.Instant != null)
            {
                foreach (var inner in bunch.Instant)
                    ApplyHealEffect(target, inner);
            }
        }

        private static void SimulatePickups(List<Unit> units)
        {
            if (activePickups.Count == 0)
                return;

            var registry = Singleton<UnitsRegistry>.Instance;
            List<uint> taken = null;
            foreach (var kv in activePickups)
            {
                try
                {
                    var pickup = registry.Get(kv.Key);
                    if (pickup == null || pickup.IsDeath)
                    {
                        if (taken == null) taken = new List<uint>();
                        taken.Add(kv.Key);
                        continue;
                    }

                    foreach (var unit in units)
                    {
                        if (unit == null || unit.IsDeath || unit.PlayerId == null || unit.Id == kv.Key)
                            continue;
                        if (UnityEngine.Vector3.Distance(pickup.transform.position, unit.transform.position) > PickupRadius)
                            continue;

                        var pickupCard = SafeGetCard<CardUnit>(kv.Value);
                        var pickupData = pickupCard?.Data as UnitDataPickup;
                        if (pickupData == null || !ApplyPickupTakeEffect(unit, pickupData.TakeEffect))
                            HealUnit(unit, 40f); // pickup whose effect we can't decode — sane default

                        try { cachedListener.PickupTaken(unit.PlayerId.Value, kv.Value); }
                        catch { }
                        try { cachedListener.UnitDrop(kv.Key); }
                        catch { }

                        if (taken == null) taken = new List<uint>();
                        taken.Add(kv.Key);
                        break;
                    }
                }
                catch { }
            }

            if (taken != null)
                foreach (var id in taken)
                    activePickups.Remove(id);
        }

        // Apply a pickup's TakeEffect: instant heals directly, and constant effects the
        // live way — mark the effect on the unit (visuals + healing loop sfx) and run
        // its HealthRegen buff as heal-over-time. Returns true if anything applied.
        private static bool ApplyPickupTakeEffect(Unit target, InstEffect effect)
        {
            if (effect == null || target == null || target.IsDeath)
                return false;

            bool applied = false;

            var heal = effect as InstEffectHeal;
            if (heal != null && heal.PlayerHeal > 0f)
            {
                HealUnit(target, heal.PlayerHeal);
                return true;
            }

            var bunch = effect as InstEffectBunch;
            if (bunch != null)
            {
                if (bunch.Instant != null)
                    foreach (var inner in bunch.Instant)
                        applied |= ApplyPickupTakeEffect(target, inner);

                if (bunch.Constant != null)
                    foreach (var key in bunch.Constant)
                        applied |= ApplyConstantEffect(target, key);
            }

            return applied;
        }

        private static bool ApplyConstantEffect(Unit target, Key effectKey)
        {
            try
            {
                var card = SafeGetCard<CardEffect>(effectKey);
                if (card == null)
                    return false;

                float duration = card.Duration ?? 5f;

                // Mark the effect on the unit — the client shows its visuals, GUI icon
                // and loop sound (the healing sfx) while the end time is in the future.
                // Stamp nested constant effects too: the visuals often live on the inner
                // card (e.g. effect_status_regen_health carries the heal prefab).
                ulong endTime = (ulong)(Singleton<IServerTime>.Instance.TimeMillis + (long)(duration * 1000f));
                var effects = new Dictionary<Key, ulong?> { [effectKey] = endTime };
                var self = card.Effect as ConstEffectSelf;
                if (self?.ConstantEffects != null)
                    foreach (var inner in self.ConstantEffects)
                        effects[inner] = endTime;
                cachedListener.UnitUpdate(target.Id, new UnitUpdate { Effects = effects });

                // e.g. medikit: HealthRegen 10 hp/s for Duration 5 s (values from the CDB).
                float regen = GetCardEffectHealthRegen(card, 0);
                if (regen > 0f)
                    AddOrRefreshRegen(target.Id, 0u, regen, UnityEngine.Time.realtimeSinceStartup + duration);

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] ApplyConstantEffect failed: " + ex.Message);
                return false;
            }
        }

        // Death consequences straight from the dead unit's card:
        //  - UnitLoot → LootItemCommon.Item.LootUnitKey: the dropped pickup (hero loot, medikits...)
        //  - ConstEffectOnDeath → InstEffectUnitSpawn: replacement units (objective debris cube)
        private static void HandleDeathSpawns(CardUnit deadCard, UnityEngine.Vector3 position, TeamType team)
        {
            if (deadCard == null || cachedListener == null)
                return;

            try
            {
                var lootCommon = deadCard.Loot?.LootItem as LootItemCommon;
                var lootKey = lootCommon?.Item != null ? lootCommon.Item.LootUnitKey : Key.None;
                if (lootKey != Key.None)
                    SpawnLootUnit(lootKey, position, TeamType.Neutral);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Death loot failed: " + ex.Message);
            }

            try
            {
                var effectKeys = new List<Key>();
                if (deadCard.InitEffects != null) effectKeys.AddRange(deadCard.InitEffects);
                if (deadCard.EnabledEffects != null) effectKeys.AddRange(deadCard.EnabledEffects);
                foreach (var key in effectKeys)
                {
                    var effectCard = SafeGetCard<CardEffect>(key);
                    var onDeath = effectCard?.Effect as ConstEffectOnDeath;
                    if (onDeath != null)
                        SpawnUnitsFromInstEffect(onDeath.Effect, position, team, 0);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnDeath spawn failed: " + ex.Message);
            }
        }

        private static void SpawnUnitsFromInstEffect(InstEffect effect, UnityEngine.Vector3 position, TeamType team, int depth)
        {
            if (effect == null || depth > 4)
                return;

            var spawn = effect as InstEffectUnitSpawn;
            if (spawn != null && spawn.UnitKey != Key.None)
            {
                SpawnLootUnit(spawn.UnitKey, position, team);
                return;
            }

            var bunch = effect as InstEffectBunch;
            if (bunch != null && bunch.Instant != null)
                foreach (var inner in bunch.Instant)
                    SpawnUnitsFromInstEffect(inner, position, team, depth + 1);
        }

        private static void SpawnLootUnit(Key unitKey, UnityEngine.Vector3 position, TeamType team)
        {
            var card = SafeGetCard<CardUnit>(unitKey);
            if (card == null)
                return;

            var unitId = nextOfflineDeviceUnitId++;
            cachedListener.UnitCreate(unitId, new UnitInit
            {
                Key = unitKey,
                Team = team,
                Controlled = false,
                Transform = new ZoneTransform
                {
                    Position = position + UnityEngine.Vector3.up * 0.3f,
                    NoInterpolation = true,
                },
            });

            if (card.Data is UnitDataPickup)
                activePickups[unitId] = unitKey;

            UnityEngine.Debug.Log("[BNL-Bots] Death spawn: " + card.Id + " at " + position);
        }

        private static void OnObjectiveDestroyed(uint unitId, TeamType team)
        {
            try
            {
                objectiveUnitIds.Remove(unitId);

                var registry = Singleton<UnitsRegistry>.Instance;
                bool anyLeft = false;
                foreach (var id in objectiveUnitIds)
                {
                    var unit = registry?.Get(id);
                    if (unit != null && !unit.IsDeath && unit.Team == team)
                    {
                        anyLeft = true;
                        break;
                    }
                }

                UnityEngine.Debug.Log("[BNL-Bots] Objective destroyed (team=" + team + ", remaining=" + (anyLeft ? "yes" : "none") + ")");
                if (!anyLeft)
                {
                    var winner = team == TeamType.Team1 ? TeamType.Team2 : TeamType.Team1;
                    cachedListener.EndMatch(winner);
                    return;
                }

                UpdateObjectiveShields();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] OnObjectiveDestroyed failed: " + ex.Message);
            }
        }

        // Live progression: line-1 cubes are vulnerable first; line-2 and the base carry an
        // Invulnerability buff until the earlier tier falls. The assault HP bar picks the
        // first non-invulnerable objective, so these buffs are what make it track correctly.
        private static readonly Dictionary<uint, bool> objectiveShieldState = new Dictionary<uint, bool>();

        private static int GetObjectiveTier(CardUnit card)
        {
            try
            {
                if (card.IsLine1) return 1;
                if (card.IsLine2) return 2;
                if (card.IsBase) return 3;
            }
            catch { }
            return 1;
        }

        private static void UpdateObjectiveShields()
        {
            var registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null || cachedListener == null)
                return;

            // Lowest tier still alive per team = the vulnerable tier.
            var vulnerableTier = new Dictionary<TeamType, int>();
            var alive = new List<Unit>();
            foreach (var id in objectiveUnitIds)
            {
                var unit = registry.Get(id);
                if (unit == null || unit.IsDeath || unit.UnitCard == null)
                    continue;
                alive.Add(unit);

                int tier = GetObjectiveTier(unit.UnitCard);
                int current;
                if (!vulnerableTier.TryGetValue(unit.Team, out current) || tier < current)
                    vulnerableTier[unit.Team] = tier;
            }

            foreach (var unit in alive)
            {
                bool shielded = GetObjectiveTier(unit.UnitCard) > vulnerableTier[unit.Team];
                bool wasShielded;
                if (objectiveShieldState.TryGetValue(unit.Id, out wasShielded) && wasShielded == shielded)
                    continue;
                objectiveShieldState[unit.Id] = shielded;

                var buffs = new Dictionary<BuffType, float>();
                if (shielded)
                    buffs[BuffType.Invulnerability] = 1f;
                cachedListener.UnitUpdate(unit.Id, new UnitUpdate { Buffs = buffs });
            }
        }

        private static void AppendUnsupportedBlocks(Vector3s removedPos, Dictionary<Vector3s, BlockUpdate> updates)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            var map = zoneManager?.Map;
            if (map == null)
                return;

            var seeds = new[]
            {
                removedPos + Vector3s.up,
                removedPos + Vector3s.down,
                removedPos + Vector3s.left,
                removedPos + Vector3s.right,
                removedPos + Vector3s.forward,
                removedPos + Vector3s.back,
            };

            var visited = new HashSet<Vector3s>();
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (!map.Blocks.Check(seed) || !visited.Add(seed))
                    continue;

                var block = map.Blocks[seed];
                if (block.Id == 0)
                    continue;

                var cluster = new List<Vector3s>();
                bool anchored = CollectFloatingCluster(seed, visited, cluster, updates);
                if (anchored)
                    continue;

                for (int c = 0; c < cluster.Count; c++)
                {
                    var pos = cluster[c];
                    if (!map.Blocks.Check(pos))
                        continue;

                    var clusterBlock = map.Blocks[pos];
                    var update = clusterBlock.ToUpdate();
                    update.Id = 0;
                    update.Damage = 0;
                    update.Vdata = 2;
                    updates[pos] = update;
                }
            }
        }

        // Cap the flood-fill so breaking a block never scans the whole map (1M+ blocks = 1s freeze).
        // Clusters larger than this are treated as anchored (real floating debris is small).
        private const int MaxFloatingClusterSize = 512;

        private static bool CollectFloatingCluster(Vector3s start, HashSet<Vector3s> visited, List<Vector3s> cluster, Dictionary<Vector3s, BlockUpdate> updates)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            var map = zoneManager?.Map;
            if (map == null || !map.Blocks.Check(start))
                return true;

            var queue = new Queue<Vector3s>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                if (!map.Blocks.Check(pos))
                    continue;

                BlockUpdate pending;
                if (updates.TryGetValue(pos, out pending) && pending.Id == 0)
                    continue;

                var block = map.Blocks[pos];
                if (block.Id == 0)
                    continue;

                cluster.Add(pos);

                // Anchored — stop scanning immediately instead of flooding the rest of the map.
                if (pos.y <= 0 || block.Card.Grounded)
                    return true;

                // Too big to be floating debris — treat as anchored to bound the cost.
                if (cluster.Count > MaxFloatingClusterSize)
                    return true;

                var neighbors = new[]
                {
                    pos + Vector3s.up,
                    pos + Vector3s.down,
                    pos + Vector3s.left,
                    pos + Vector3s.right,
                    pos + Vector3s.forward,
                    pos + Vector3s.back,
                };

                for (int i = 0; i < neighbors.Length; i++)
                {
                    var next = neighbors[i];
                    if (!map.Blocks.Check(next) || !visited.Add(next))
                        continue;

                    BlockUpdate nextPending;
                    if (updates.TryGetValue(next, out nextPending) && nextPending.Id == 0)
                        continue;

                    var nextBlock = map.Blocks[next];
                    if (nextBlock.Id == 0)
                        continue;

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Initial ZoneUpdate — gives the UI a valid Phase so it doesn't crash
        // ─────────────────────────────────────────────────────────────────────────
        private static ZoneUpdate BuildInitialZoneUpdate(Protocol.MapData mapData, Key playerHeroKey)
        {
            if (mapData == null)
                mapData = pendingMapData;

            var localPlayerId = EnsureOfflinePlayerIdentity();
            var playerInfo = new System.Collections.Generic.Dictionary<uint, ZonePlayerInfo>
            {
                [localPlayerId] = CreateZonePlayerInfo(GetPlayerDisplayName(), Singleton<PlayerData>.Instance != null ? Singleton<PlayerData>.Instance.SteamId : 0UL),
            };

            liveStats.Clear();
            liveStats[localPlayerId] = new MatchPlayerStats { Team = TeamType.Team1, Kills = 0, Deaths = 0, Assists = 0 };

            for (int i = 0; i < UnityEngine.Mathf.Clamp(botCount, 1, 9); i++)
            {
                var unitId = BotUnitIdBase + (uint)i;
                var botPlayerId = GetBotPlayerId(unitId);
                playerInfo[botPlayerId] = CreateZonePlayerInfo("Bot " + (i + 1), 0UL);
                liveStats[botPlayerId] = new MatchPlayerStats { Team = TeamType.Team2, Kills = 0, Deaths = 0, Assists = 0 };
            }
            var playerStats = liveStats;
            var playerSpawnPoints = new System.Collections.Generic.Dictionary<uint, uint?>
            {
                [localPlayerId] = 1,
            };
            for (int i = 0; i < UnityEngine.Mathf.Clamp(botCount, 1, 9); i++)
                playerSpawnPoints[GetBotPlayerId(BotUnitIdBase + (uint)i)] = 2;

            return new ZoneUpdate
            {
                Phase = new ZonePhase
                {
                    PhaseType = ZonePhaseType.Assault,
                    StartTime = (long)(UnityEngine.Time.realtimeSinceStartup * 1000),
                    EndTime = null,
                },
                Statistics = new MatchStats
                {
                    PlayerStats = playerStats,
                    Team1Stats = new MatchTeamStats(),
                    Team2Stats = new MatchTeamStats(),
                },
                SpawnPoints = BuildOfflineSpawnPoints(mapData),
                PlayerSpawnPoints = playerSpawnPoints,
                RespawnInfo = new System.Collections.Generic.Dictionary<uint, ulong>(),
                PlayerInfo = playerInfo,
                SupplyInfo = new SupplyInfo(),
                Objectives = new List<ZoneObjective>(),
                ResourceCap = 9999f,
            };
        }

        private static List<SpawnPoint> BuildOfflineSpawnPoints(Protocol.MapData mapData)
        {
            if (mapData == null)
                return new List<SpawnPoint>();

            return new List<SpawnPoint>
            {
                new SpawnPoint
                {
                    Id = 1,
                    Team = TeamType.Team1,
                    Pos = GetSpawnPosition(mapData, TeamType.Team1),
                    Lock = SpawnPointLockType.Free,
                    Owner = null,
                },
                new SpawnPoint
                {
                    Id = 2,
                    Team = TeamType.Team2,
                    Pos = GetSpawnPosition(mapData, TeamType.Team2),
                    Lock = SpawnPointLockType.Free,
                    Owner = null,
                },
            };
        }

        private static ZonePlayerInfo CreateZonePlayerInfo(string nickname, ulong steamId)
        {
            return new ZonePlayerInfo
            {
                Nickname = string.IsNullOrEmpty(nickname) ? "Offline Player" : nickname,
                SteamId = steamId == 0UL ? null : steamId,
                SquadId = null,
                LookingForFriends = false,
            };
        }

        private static uint GetBotPlayerId(uint unitId)
        {
            return BotPlayerIdBase + (unitId - BotUnitIdBase);
        }

        // Scoreboard state — mirrors what the live server tracks in MatchStats.
        private static readonly Dictionary<uint, MatchPlayerStats> liveStats = new Dictionary<uint, MatchPlayerStats>();

        private static void UpdateKillStats(Unit killer, Unit dead, List<uint> assistants)
        {
            try
            {
                MatchPlayerStats stats;
                if (killer != null && killer.PlayerId != null && liveStats.TryGetValue(killer.PlayerId.Value, out stats))
                    stats.Kills++;
                if (dead.PlayerId != null && liveStats.TryGetValue(dead.PlayerId.Value, out stats))
                    stats.Deaths++;
                if (assistants != null)
                {
                    foreach (var assistant in assistants)
                        if (liveStats.TryGetValue(assistant, out stats))
                            stats.Assists++;
                }

                cachedListener.UpdateZone(new ZoneUpdate
                {
                    Statistics = new MatchStats
                    {
                        PlayerStats = liveStats,
                        Team1Stats = new MatchTeamStats(),
                        Team2Stats = new MatchTeamStats(),
                    },
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] UpdateKillStats failed: " + ex.Message);
            }
        }

        private static string GetPlayerDisplayName()
        {
            try
            {
                var pd = Singleton<PlayerData>.Instance;
                if (pd != null && !string.IsNullOrEmpty(pd.DebugName))
                    return pd.DebugName;
            }
            catch { }

            return "Offline Player";
        }

        private static uint EnsureOfflinePlayerIdentity()
        {
            var playerData = Singleton<PlayerData>.Instance;
            if (playerData == null)
                return PlayerUnitId;

            if (playerData.Id == 0)
                playerData.Id = PlayerUnitId;
            if (string.IsNullOrEmpty(playerData.DebugName))
                playerData.DebugName = "Offline Player";
            return playerData.Id;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Bot AI tick
        // ─────────────────────────────────────────────────────────────────────────
        private static void TickBots()
        {
            foreach (var bot in bots)
            {
                try { bot.Tick(cachedListener); }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[BNL-Bots] Bot " + bot.UnitId + " tick error: " + ex.Message);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Config helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static bool IsEnabled()
        {
            if (!configLoaded || UnityEngine.Time.realtimeSinceStartup >= nextConfigRefresh)
            {
                configLoaded = true;
                nextConfigRefresh = UnityEngine.Time.realtimeSinceStartup + ConfigRefreshInterval;
                ReloadConfig();
            }
            return enabled;
        }

        private static void ReloadConfig()
        {
            try
            {
                var path = GetConfigPath();
                if (!File.Exists(path)) { enabled = false; return; }

                var json = File.ReadAllText(path, Encoding.UTF8);
                enabled = ReadBool(json, "enabled", false);
                botCount = ReadInt(json, "bot_count", 3);
                difficulty = ReadString(json, "difficulty", "medium");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Failed to read config: " + ex.Message);
                enabled = false;
            }
        }

        private static string GetConfigPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(Path.Combine(Path.Combine(localAppData, "BNL-CommunityFixes"), "app"), Path.Combine("patching", ConfigFileName));
        }

        private static bool ReadBool(string json, string key, bool fallback)
        {
            var tag = "\"" + key + "\"";
            var idx = json.IndexOf(tag, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            var colon = json.IndexOf(':', idx + tag.Length);
            if (colon < 0) return fallback;
            var rest = json.Substring(colon + 1).TrimStart();
            if (rest.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (rest.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static int ReadInt(string json, string key, int fallback)
        {
            var tag = "\"" + key + "\"";
            var idx = json.IndexOf(tag, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            var colon = json.IndexOf(':', idx + tag.Length);
            if (colon < 0) return fallback;
            var rest = json.Substring(colon + 1).TrimStart();
            int end = 0;
            while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '-')) end++;
            return end > 0 && int.TryParse(rest.Substring(0, end), out var v) ? v : fallback;
        }

        private static string ReadString(string json, string key, string fallback)
        {
            var tag = "\"" + key + "\"";
            var idx = json.IndexOf(tag, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            var colon = json.IndexOf(':', idx + tag.Length);
            if (colon < 0) return fallback;
            var rest = json.Substring(colon + 1).TrimStart();
            if (!rest.StartsWith("\"")) return fallback;
            var end = rest.IndexOf('"', 1);
            return end > 0 ? rest.Substring(1, end - 1) : fallback;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BotController — one per bot unit (Phase 2: roam + basic shoot)
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class BotController
    {
        public uint UnitId { get; }

        private BotState state = BotState.Spawn;
        private List<Vector3s> currentPath = new List<Vector3s>();
        private int pathIndex;
        private uint? targetUnitId;
        private float reactionDelaySec;   // difficulty-driven delay before acting
        private float aimInaccuracyUnits; // max positional offset on shots (difficulty)

        private ulong lastMoveTime;
        private const ulong MoveIntervalMs = 100;  // movement update cadence (10 Hz, near-realtime)
        private const float MoveSpeed = 5.5f;      // units/sec, ≈ hero run speed
        private const float engageRange = 12f;
        private bool combatGearChosen;
        private bool diagnosticsLogged;
        private UnityEngine.Vector3 lastProgressPosition;
        private float lastProgressTime;

        // One-shot dump of the unit's animation/equipment state — tells us why gear
        // models and fire sfx aren't showing (anim components missing? disabled?).
        private void LogUnitDiagnostics(Unit myUnit)
        {
            try
            {
                var anim = myUnit.GetComponentInChildren<UnityEngine.Animation>();
                var animator = myUnit.GetComponentInChildren<UnityEngine.Animator>();
                var equiping = myUnit.GetComponentInChildren<Equiping>();
                var unitAnim = myUnit.GetComponentInChildren<UnitAnimation>();
                UnityEngine.Debug.Log("[BNL-Bots] Diag bot " + UnitId
                    + " activeGO=" + myUnit.gameObject.activeInHierarchy
                    + " anim=" + (anim != null ? (anim.enabled + "/" + anim.GetClipCount()) : "none")
                    + " animator=" + (animator != null ? animator.enabled.ToString() : "none")
                    + " equiping=" + (equiping != null)
                    + " unitAnim=" + (unitAnim != null ? unitAnim.enabled.ToString() : "none")
                    + " currentGear=" + (myUnit.CurrentGear != null ? myUnit.CurrentGear.Key.ToString() : "null")
                    + " gearIndex=" + myUnit.CurrentGearIndex
                    + " gears=" + (myUnit.Gears != null ? myUnit.Gears.Count : 0));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] Diag failed: " + ex.Message);
            }
        }
        private float nextPathAttempt;  // throttle A* when it keeps failing
        private float nextStateLog;     // periodic debug logging

        public BotController(uint unitId, string difficulty)
        {
            UnitId = unitId;

            switch ((difficulty ?? "medium").ToLowerInvariant())
            {
                case "easy":
                    reactionDelaySec = 1.5f;
                    aimInaccuracyUnits = 3f;
                    break;
                case "hard":
                    reactionDelaySec = 0.1f;
                    aimInaccuracyUnits = 0.2f;
                    break;
                default: // medium
                    reactionDelaySec = 0.6f;
                    aimInaccuracyUnits = 1.2f;
                    break;
            }
        }

        public void Tick(ZoneServiceListener listener)
        {
            if (listener == null) return;

            var registry = Singleton<UnitsRegistry>.Instance;
            if (registry == null) return;

            var myUnit = registry.Get(UnitId);
            if (myUnit == null) return;

            if (!diagnosticsLogged)
            {
                diagnosticsLogged = true;
                LogUnitDiagnostics(myUnit);
            }

            if (!combatGearChosen)
                ChooseCombatGear(listener, myUnit);

            // ── state transitions ─────────────────────────────────────────────
            if (myUnit.IsDeath)
            {
                if (state != BotState.Dead)
                {
                    state = BotState.Dead;
                    currentPath.Clear();
                    targetUnitId = null;
                }
                return; // wait for respawn (server-driven in a full match)
            }

            if (state == BotState.Dead)
            {
                // Unit is alive again — respawned (fresh unit, re-equip weapon).
                state = BotState.Spawn;
                combatGearChosen = false;
            }

            if (state == BotState.Spawn)
            {
                state = BotState.Roam;
                lastProgressPosition = myUnit.transform.position;
                lastProgressTime = UnityEngine.Time.realtimeSinceStartup;
            }

            // Recover from stale/blocked paths. Dynamic units and recently changed
            // blocks can invalidate a route after it was calculated.
            if (UnityEngine.Vector3.Distance(myUnit.transform.position, lastProgressPosition) >= 0.35f)
            {
                lastProgressPosition = myUnit.transform.position;
                lastProgressTime = UnityEngine.Time.realtimeSinceStartup;
            }
            else if (UnityEngine.Time.realtimeSinceStartup - lastProgressTime >= 2f)
            {
                currentPath.Clear();
                pathIndex = 0;
                nextPathAttempt = 0f;
                lastProgressTime = UnityEngine.Time.realtimeSinceStartup;
                UnityEngine.Debug.Log("[BNL-Bots] Bot " + UnitId + " was stuck; forcing a fresh route.");
            }

            if (myUnit.Health < myUnit.MaxHealth * 0.3f && state != BotState.Retreat)
            {
                state = BotState.Retreat;
                currentPath.Clear();
                targetUnitId = null;
            }

            if (state == BotState.Retreat && myUnit.Health >= myUnit.MaxHealth * 0.7f)
            {
                state = BotState.Roam;
            }

            // ── find nearest enemy ────────────────────────────────────────────
            var enemies = registry.GetAllPlayersByTeam(myUnit.Team == TeamType.Team1 ? TeamType.Team2 : TeamType.Team1);
            Unit nearestEnemy = null;
            float nearestDist = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e == null || e.IsDeath) continue;
                var d = UnityEngine.Vector3.Distance(myUnit.transform.position, e.transform.position);
                if (d < nearestDist) { nearestDist = d; nearestEnemy = e; }
            }

            if (nearestEnemy != null && nearestDist < engageRange && state == BotState.Roam)
            {
                state = BotState.Engage;
                targetUnitId = nearestEnemy.Id;
                currentPath.Clear();
            }

            if (state == BotState.Engage && (nearestEnemy == null || nearestDist > engageRange * 1.5f))
            {
                state = BotState.Roam;
                targetUnitId = null;
                currentPath.Clear();
            }

            // ── execute state ─────────────────────────────────────────────────
            var nowMs = (ulong)(UnityEngine.Time.realtimeSinceStartup * 1000);

            if (UnityEngine.Time.realtimeSinceStartup >= nextStateLog)
            {
                nextStateLog = UnityEngine.Time.realtimeSinceStartup + 10f;
                UnityEngine.Debug.Log("[BNL-Bots] Bot " + UnitId + " state=" + state + " pos=" + myUnit.transform.position
                    + " hp=" + myUnit.Health + " path=" + currentPath.Count + "/" + pathIndex
                    + " nearestEnemyDist=" + (nearestEnemy != null ? nearestDist.ToString("F1") : "none"));
            }

            switch (state)
            {
                case BotState.Roam:
                    TickRoam(listener, myUnit, registry, nowMs);
                    break;

                case BotState.Engage:
                    TickEngage(listener, myUnit, nearestEnemy, nowMs);
                    break;

                case BotState.Retreat:
                    TickRetreat(listener, myUnit, registry, nowMs);
                    break;
            }

            // Gravity while standing still (e.g. engaging, or the floor was dug out).
            // Skip while following a path — jump waypoints intentionally go up.
            if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                var pos = myUnit.transform.position;
                var settled = ApplyGravity(pos, 0.1f);
                if (settled.y < pos.y - 0.01f)
                    SendMove(listener, myUnit, settled);
            }
        }

        // Equip the first gear whose primary tool actually damages players — gear 0 is
        // usually the dig tool, which is why bots looked unarmed and shot silently.
        private void ChooseCombatGear(ZoneServiceListener listener, Unit myUnit)
        {
            try
            {
                if (myUnit.Gears == null || myUnit.Gears.Count == 0)
                    return;

                combatGearChosen = true;
                // Prefer a ranged weapon; the dig tool is melee and also damages players,
                // so a damage-only check kept picking it.
                GearData best = null;
                Tool bestTool = null;
                float bestDamage = 0f;
                bool bestRanged = false;
                foreach (var gear in myUnit.Gears)
                {
                    if (gear == null)
                        continue;
                    Tool tool = null;
                    try { tool = gear.GetTool(0)?.Tool; }
                    catch { }

                    float dmg = BotModeRuntime.GetToolDamage(tool, BotModeRuntime.OfflineDamageKind.Player);
                    if (dmg <= 0f)
                        continue;

                    bool ranged = tool is ToolShot || tool is ToolBurst || tool is ToolSpinup;
                    if (best == null || (ranged && !bestRanged) || (ranged == bestRanged && dmg > bestDamage))
                    {
                        best = gear;
                        bestTool = tool;
                        bestDamage = dmg;
                        bestRanged = ranged;
                    }
                }

                // Bots now spawn with the combat gear equipped — only act if somehow not.
                if (best != null && (myUnit.CurrentGear == null || myUnit.CurrentGear.Key != best.Key))
                {
                    listener.UnitUpdate(UnitId, new UnitUpdate { CurrentGear = best.Key });
                    UnityEngine.Debug.Log("[BNL-Bots] Bot " + UnitId + " switched to " + best.Key
                        + " tool=" + (bestTool != null ? bestTool.GetType().Name : "null")
                        + " dmg=" + bestDamage + " ranged=" + bestRanged);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[BNL-Bots] ChooseCombatGear failed: " + ex.Message);
            }
        }

        // ── ROAM: navigate toward the enemy base ──────────────────────────────
        private void TickRoam(ZoneServiceListener listener, Unit myUnit, UnitsRegistry registry, ulong nowMs)
        {
            if (nowMs - lastMoveTime < MoveIntervalMs) return;
            lastMoveTime = nowMs;

            // Find a target: enemy base direction or first enemy unit.
            UnityEngine.Vector3 goal;
            var enemies = registry.GetAllPlayersByTeam(myUnit.Team == TeamType.Team1 ? TeamType.Team2 : TeamType.Team1);
            if (enemies.Count > 0 && !enemies[0].IsDeath)
                goal = enemies[0].transform.position;
            else
                goal = GetEnemyBaseApprox(myUnit);

            TryFollowPath(listener, myUnit, goal);
        }

        // ── ENGAGE: shoot at target ───────────────────────────────────────────
        private void TickEngage(ZoneServiceListener listener, Unit myUnit, Unit target, ulong nowMs)
        {
            if (target == null || target.IsDeath) return;
            if (nowMs - lastMoveTime < (ulong)(reactionDelaySec * 1000)) return;
            lastMoveTime = nowMs;

            // Move slightly toward the target if far.
            var dist = UnityEngine.Vector3.Distance(myUnit.transform.position, target.transform.position);
            if (dist > 6f)
            {
                TryFollowPath(listener, myUnit, target.transform.position);
                return;
            }

            // Fire primary weapon (gear index 0).
            if (myUnit.IsReloading || myUnit.IsSwitchingGear) return;

            // Face the target and fire from chest height at chest height, so the shot
            // visuals/audio and tracers read correctly.
            var chest = UnityEngine.Vector3.up * 1.4f;
            var aimPos = target.transform.position + chest + RandomOffset(aimInaccuracyUnits);
            var toTarget = target.transform.position - myUnit.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                listener.UnitMove(UnitId, nowMs, new ZoneTransform
                {
                    Position = myUnit.transform.position,
                    Rotation = ZoneTransformHelper.ToVector3s(UnityEngine.Quaternion.LookRotation(toTarget.normalized, UnityEngine.Vector3.up)),
                    NoInterpolation = true,
                });
            }

            ulong shotId = ShotId.Gen();
            var shotOrigin = myUnit.transform.position + chest;
            var cast = new CastData
            {
                ToolIndex = 0,
                ShotPos = shotOrigin,
                Shots = new List<ShotData>
                {
                    new ShotData { ShotId = (ulong?)shotId, TargetPos = aimPos }
                },
            };

            listener.Cast(UnitId, cast);

            // Live also sends a ToolFire event with each cast — it drives the fire
            // animation and the gunshot audio for non-controlled units.
            try { UnitEventHelper.HandleToolFire(myUnit, 0); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[BNL-Bots] HandleToolFire failed: " + ex.Message); }

            // The Cast above is only cosmetic for non-controlled units — simulate the
            // server-side hit: chance shrinks with aim inaccuracy and distance.
            float hitChance = UnityEngine.Mathf.Clamp01(1.1f - aimInaccuracyUnits * 0.25f)
                            * UnityEngine.Mathf.Clamp01(1.5f - dist / engageRange);
            bool shouldHit = rng.NextDouble() < hitChance;
            Tool tool = null;
            try { tool = myUnit.CurrentGear != null ? myUnit.CurrentGear.GetTool(0)?.Tool : null; }
            catch { }
            float damage = BotModeRuntime.GetToolDamage(tool, BotModeRuntime.OfflineDamageKind.Player);
            Key? impact = BotModeRuntime.GetToolImpact(tool);

            // Projectile bullets require explicit Create/Move/Drop messages. Their CDB
            // speed now determines both the visible flight and when damage is applied.
            bool simulatedProjectile = BotModeRuntime.TryFireBotProjectile(
                myUnit, target, tool, shotId, shotOrigin, aimPos, damage, impact, shouldHit);

            if (!simulatedProjectile && shouldHit)
            {
                if (damage > 0f)
                    BotModeRuntime.ApplyOfflineUnitDamage(UnitId, target.Id, damage, false, impact);
            }
        }

        // ── RETREAT: move back toward own base ───────────────────────────────
        private void TickRetreat(ZoneServiceListener listener, Unit myUnit, UnitsRegistry registry, ulong nowMs)
        {
            if (nowMs - lastMoveTime < MoveIntervalMs) return;
            lastMoveTime = nowMs;
            TryFollowPath(listener, myUnit, GetOwnBaseApprox(myUnit));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Path following via PathFindingHelper
        // ─────────────────────────────────────────────────────────────────────
        private void TryFollowPath(ZoneServiceListener listener, Unit myUnit, UnityEngine.Vector3 goal)
        {
            // Bound the A* search: path toward a waypoint at most 24 blocks away.
            // Full-map searches are slow (hitches) and often fail outright.
            var toGoal = goal - myUnit.transform.position;
            if (toGoal.magnitude > 24f)
                goal = myUnit.transform.position + toGoal.normalized * 24f;

            var myPos = (Vector3s)myUnit.transform.position;
            var goalPos = FindReachableGoal((Vector3s)goal, myPos);

            // Refresh path when empty or exhausted; back off after failures.
            if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                if (UnityEngine.Time.realtimeSinceStartup < nextPathAttempt)
                {
                    MoveDirect(listener, myUnit, goal);
                    return;
                }

                // FindReachableGoal falls back to our own position when the waypoint isn't
                // walkable — pathing there produces a 1-node path and a frozen bot.
                // Never use the game's PathFindingHelper here: it has no iteration cap and
                // O(n²) list scans, so an unreachable goal freezes the whole game.
                if (goalPos == myPos)
                    currentPath = new List<Vector3s>();
                else
                    currentPath = FindPathBounded(myPos, goalPos) ?? new List<Vector3s>();
                pathIndex = 0;

                // A path of 0–1 nodes can't be followed — walk directly for a while instead.
                if (currentPath.Count <= 1)
                {
                    currentPath.Clear();
                    nextPathAttempt = UnityEngine.Time.realtimeSinceStartup + 2f;
                }
            }

            if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
            {
                MoveDirect(listener, myUnit, goal);
                return;
            }

            var nextNode = currentPath[pathIndex];
            var nextWorld = NodeCenter(nextNode);

            // Advance waypoint when close enough.
            if (UnityEngine.Vector3.Distance(myUnit.transform.position, nextWorld) < 0.6f)
            {
                pathIndex++;
                if (pathIndex >= currentPath.Count) return;
                nextNode = currentPath[pathIndex];
                nextWorld = NodeCenter(nextNode);
            }

            SendMove(listener, myUnit, nextWorld);
        }

        private static UnityEngine.Vector3 NodeCenter(Vector3s node)
        {
            return new UnityEngine.Vector3(node.x + 0.5f, node.y + 0.05f, node.z + 0.5f);
        }

        // Step toward the target at run speed and send a movement update with rotation +
        // local velocity so the unit's animator plays a run cycle instead of gliding.
        private void SendMove(ZoneServiceListener listener, Unit myUnit, UnityEngine.Vector3 target)
        {
            var current = myUnit.transform.position;
            var delta = target - current;
            float maxStep = MoveSpeed * (MoveIntervalMs / 1000f);
            var next = delta.magnitude <= maxStep ? target : current + delta.normalized * maxStep;

            var flat = next - current;
            flat.y = 0f;

            var transform = new ZoneTransform
            {
                Position = next,
                NoInterpolation = false,
                IsSprint = false,
            };

            if (flat.sqrMagnitude > 0.0001f)
            {
                var look = UnityEngine.Quaternion.LookRotation(flat.normalized, UnityEngine.Vector3.up);
                transform.Rotation = ZoneTransformHelper.ToVector3s(look);
                // LocalVelocity is in the unit's local frame (z = forward) and packed ×10.
                transform.SetLocalVelocity(new UnityEngine.Vector3(0f, 0f, MoveSpeed));
            }

            listener.UnitMove(UnitId, (ulong)(UnityEngine.Time.realtimeSinceStartup * 1000), transform);
        }

        // Bounded A* over the voxel grid. Unlike the game's PathFindingHelper this caps the
        // search (no main-thread freeze on unreachable goals) and returns a best-effort
        // partial path toward the goal when it can't be reached within the budget.
        private sealed class PathNode
        {
            public Vector3s Pos;
            public PathNode From;
            public bool IsGrounded;
            public float G;
            public float H;
            public float F { get { return G + H; } }
        }

        private static List<Vector3s> FindPathBounded(Vector3s start, Vector3s goal)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            if (zoneManager == null)
                return null;

            const int MaxExpansions = 600;
            var dirs = new[]
            {
                Vector3s.forward, Vector3s.back, Vector3s.left, Vector3s.right, Vector3s.up, Vector3s.down
            };

            var open = new List<PathNode>();
            var seen = new HashSet<Vector3s>();
            var startNode = new PathNode { Pos = start, IsGrounded = true, G = 0f, H = UnityEngine.Vector3.Distance(start.ToVector3(), goal.ToVector3()) };
            open.Add(startNode);
            seen.Add(start);

            var best = startNode;
            int expansions = 0;

            while (open.Count > 0 && expansions++ < MaxExpansions)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].F < open[bi].F) bi = i;
                var cur = open[bi];
                open.RemoveAt(bi);

                if (cur.H < best.H)
                    best = cur;
                if (cur.Pos == goal)
                {
                    best = cur;
                    break;
                }

                for (int d = 0; d < dirs.Length; d++)
                {
                    var next = cur.Pos + dirs[d];
                    if (!seen.Add(next))
                        continue;

                    var block = zoneManager.GetBlock(next);
                    if (block == null || block.Value.Id != 0)
                        continue;

                    var below = zoneManager.GetBlock(next + Vector3s.down);
                    bool grounded = below != null && below.Value.Id != 0;

                    // Same rule as the game's pathfinder: an airborne node is only
                    // enterable from a grounded one (a jump) — never chained. This is
                    // what kept bots from walking across thin air.
                    if (!grounded && !cur.IsGrounded)
                        continue;

                    open.Add(new PathNode
                    {
                        Pos = next,
                        From = cur,
                        IsGrounded = grounded,
                        G = cur.G + (grounded ? 1f : 3f),
                        H = UnityEngine.Vector3.Distance(next.ToVector3(), goal.ToVector3()),
                    });
                }
            }

            if (best == startNode)
                return null;

            var path = new List<Vector3s>();
            for (var n = best; n != null; n = n.From)
                path.Add(n.Pos);
            path.Reverse();
            return path;
        }

        private static Vector3s FindReachableGoal(Vector3s preferredGoal, Vector3s fallback)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            if (zoneManager == null)
                return fallback;

            if (IsWalkableAir(preferredGoal))
                return preferredGoal;

            var offsets = new[]
            {
                Vector3s.zero,
                Vector3s.forward,
                Vector3s.back,
                Vector3s.left,
                Vector3s.right,
                Vector3s.up,
                Vector3s.down
            };

            for (int radius = 1; radius <= 3; radius++)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    var candidate = preferredGoal + offsets[i] * radius;
                    if (IsWalkableAir(candidate))
                        return candidate;
                }
            }

            return fallback;
        }

        private static bool IsWalkableAir(Vector3s pos)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            var block = zoneManager?.GetBlock(pos);
            var below = zoneManager?.GetBlock(pos + Vector3s.down);
            return block != null && block.Value.Id == 0 && below != null && below.Value.Id != 0;
        }

        // Straight-line fallback movement with voxel collision and gravity — non-controlled
        // units have no physics of their own, so walls and floors must be enforced here.
        private void MoveDirect(ZoneServiceListener listener, Unit myUnit, UnityEngine.Vector3 goal)
        {
            var current = myUnit.transform.position;
            var delta = goal - current;
            delta.y = 0f;

            float dt = MoveIntervalMs / 1000f;
            var next = current;
            if (delta.sqrMagnitude >= 0.25f)
                next = current + delta.normalized * (MoveSpeed * dt);

            // Horizontal collision at feet and head height.
            if (next != current && (IsSolidAt(next + UnityEngine.Vector3.up * 0.1f) || IsSolidAt(next + UnityEngine.Vector3.up * 1.2f)))
            {
                // Try a one-block step up, otherwise stay put this tick.
                var stepped = next + UnityEngine.Vector3.up;
                if (!IsSolidAt(stepped + UnityEngine.Vector3.up * 0.1f) && !IsSolidAt(stepped + UnityEngine.Vector3.up * 1.2f))
                    next = stepped;
                else
                    next = current;
            }

            next = ApplyGravity(next, dt);

            if ((next - current).sqrMagnitude < 0.0001f)
                return;

            SendMove(listener, myUnit, next);
        }

        // Fall toward the ground below (capped fall speed); snap up only tiny steps.
        private static UnityEngine.Vector3 ApplyGravity(UnityEngine.Vector3 pos, float dt)
        {
            float ground = GroundYBelow(pos);
            if (ground <= -999f)
                return pos;

            if (pos.y > ground + 0.06f)
                pos.y = UnityEngine.Mathf.Max(ground + 0.05f, pos.y - 9f * dt);
            else if (pos.y < ground)
                pos.y = ground + 0.05f;
            return pos;
        }

        internal static bool IsSolidAt(UnityEngine.Vector3 pos)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            var block = zoneManager?.GetBlock(new Vector3s(
                (short)UnityEngine.Mathf.FloorToInt(pos.x),
                (short)UnityEngine.Mathf.FloorToInt(pos.y),
                (short)UnityEngine.Mathf.FloorToInt(pos.z)));
            return block != null && block.Value.Id != 0;
        }

        // Top surface (y) of the first solid block at or below the given position; -1000 if none.
        internal static float GroundYBelow(UnityEngine.Vector3 pos)
        {
            var zoneManager = Singleton<ZoneManager>.Instance;
            var map = zoneManager?.Map;
            if (map == null)
                return -1000f;

            int x = UnityEngine.Mathf.FloorToInt(pos.x);
            int z = UnityEngine.Mathf.FloorToInt(pos.z);
            int startY = UnityEngine.Mathf.Min(map.Blocks.SizeY - 1, UnityEngine.Mathf.FloorToInt(pos.y));
            for (int y = startY; y >= 0; y--)
            {
                var probe = new Vector3s((short)x, (short)y, (short)z);
                if (!map.Blocks.Check(probe))
                    continue;

                var block = map.Blocks[probe];
                if (block.Id != 0)
                    return y + 1f;
            }

            return -1000f;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private static UnityEngine.Vector3 GetOwnBaseApprox(Unit unit)
        {
            var map = Singleton<ZoneManager>.Instance?.Map;
            if (map == null) return unit.transform.position;
            float sx = map.Blocks.SizeX; float sz = map.Blocks.SizeZ;
            float z = unit.Team == TeamType.Team1 ? sz * 0.15f : sz * 0.85f;
            return new UnityEngine.Vector3(sx * 0.5f, unit.transform.position.y, z);
        }

        private static UnityEngine.Vector3 GetEnemyBaseApprox(Unit unit)
        {
            var map = Singleton<ZoneManager>.Instance?.Map;
            if (map == null) return unit.transform.position;
            float sx = map.Blocks.SizeX; float sz = map.Blocks.SizeZ;
            float z = unit.Team == TeamType.Team1 ? sz * 0.85f : sz * 0.15f;
            return new UnityEngine.Vector3(sx * 0.5f, unit.transform.position.y, z);
        }

        private static readonly System.Random rng = new System.Random();

        private static UnityEngine.Vector3 RandomOffset(float maxUnits)
        {
            if (maxUnits <= 0f) return UnityEngine.Vector3.zero;
            float x = (float)(rng.NextDouble() * 2 - 1) * maxUnits;
            float y = (float)(rng.NextDouble() * 2 - 1) * maxUnits * 0.3f;
            float z = (float)(rng.NextDouble() * 2 - 1) * maxUnits;
            return new UnityEngine.Vector3(x, y, z);
        }

        private enum BotState { Spawn, Roam, Engage, Retreat, Dead }
    }
}
