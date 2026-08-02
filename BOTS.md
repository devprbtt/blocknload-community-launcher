# Bot / Offline Practice Mode — Architecture & Implementation

> **ARCHITECTURE PIVOT (2026-07-15):** The in-process server simulation described below
> (IL hooks + BotModeRuntime driving ZoneServiceListener with synthetic events) is being
> **retired** in favor of running a real local server: **bnlReloaded**
> (https://github.com/nascarman9/bnlReloaded) — the same open-source server recreation
> that powers "Nascarman's Master Server" in the community server list.
>
> ## Architecture v2 — Local bnlReloaded server
>
> - bnlReloaded (.NET 10) runs locally: master server on `127.0.0.1:28100`, region server
>   on `28101`. Runtime dir needs `Configs/configs.json` (snake_case keys), `Cache/cdb`
>   (copied from the game), `PlayerData/`. Verified building and running 2026-07-15.
> - The community client patch already reads `servers.txt`, so **zero new client patches
>   are needed**. The server is fully embedded: the **Bot Mode toggle in Feature Settings**
>   is the only switch. On launch, `LaunchCoordinator` sees the enabled flag,
>   `OfflineBotServerService` starts the bundled server (binaries in
>   `app/offline-server/bin`, runtime state in `app/offline-server/run`, cdb auto-synced
>   from the game install) and `servers.txt` is pointed at `127.0.0.1:28100` — no entry
>   appears in the server list.
> - All match logic (phases, objectives, damage, builds, loot, pickups) is the server's
>   real implementation (`GameZone` + `ServiceZone`), not a client-side simulation.
> - The old bot-mode experimental feature stays available but disabled
>   (`experimental-bot-mode-config.json` → `enabled: false`); its IL hooks are inert.
> - **Bot AI moves server-side**: the next phase is a bnlReloaded fork where bots join
>   custom games as server-driven players (the server already has full authority over
>   units, so bot units are ordinary units it moves/fires itself).
> - Local server working dir: `C:\Users\Paulo\bnlReloaded-run`, source clone (fork):
>   `C:\Users\Paulo\bnlReloaded`.
>
> ### Custom game start fix (2026-07-15)
>
> `StartCustomGame` silently returned false because `MapDatabase.LoadMapData` found no
> `Maps/` folder — the server loads playable maps from `Maps/<map_id>/card.json+data.json`
> in its working dir (block/color binaries zlib, embedded base64 in data.json). Fork changes:
> - `BNLReloadedServer export-map <mapId> <blocks.bin> <colors.bin>` CLI mode — merges the
>   catalogue card with block binaries and writes the Maps folder via `SaveMap`.
> - `StartCustomGame` logs the failure and falls back to the first available map when the
>   requested one isn't in the Maps folder.
> - `map_sr2_mountain_express` exported into the embedded run dir from
>   `assets/patching/bot_map_blocks.bin` / `bot_map_colors.bin` (256×48×88, 2 spawns, 12 units).
> - More maps can be added later by extracting InitZone MapData from the launcher's
>   zone-capture replays.
>
> ### Server-side bots — implementation plan (next)
>
> **Phase A — bots join custom games (no AI):**
> 1. Fork `Configs`: add optional `bot_count` / `bot_difficulty` (snake_case json).
>    Launcher's `OfflineBotServerService` rewrites `Configs/configs.json` each start with
>    values from `BotModeSettings` (stop skipping when file exists).
> 2. `GameInstance.StartMatch`: when `GameInitiator is CustomGamePlayerGroup` and bots
>    configured, append `Databases.PlayerDatabase.GetDummyPlayerLobbyInfo(botId, heroKey,
>    team)` entries (bot ids 900001+, random hero cards, fill the opposite/smaller team).
>    Verify `GetPlayerProfile(botId)` tolerates unknown ids (map-editor test mode uses this
>    same path via `StartMapEditorGame`, so it should).
> 3. Bot unit spawn: bots never connect, so `TryBeginGame`'s load-stage wait ignores them
>    (checks `_connectedUsers` only ✓). After `Zone.BeginBuildPhase`, create bot units:
>    factor `CreatePlayerUnit(playerId, service)` so bot units use a **no-op/null
>    ZoneService** for owner-only messages (`UnitControl`, personal ammo/loadout updates
>    must NOT broadcast) while normal creation broadcasts UnitCreate to everyone.
> 4. Respawn: hook the zone's existing death/respawn flow — bots respawn like players
>    (server already owns respawn timers for player units; verify no client ack needed).
>
> **Phase B — AI:** a server-side updater (GameZone is an `Updater`) per bot unit:
> roam toward objectives (server has the voxel map in `MapBinary`; write a bounded A*),
> engage nearest enemy (use the zone's own damage pipeline — the same methods
> `ServiceZone` calls when a client reports `Cast`/`Hit`), difficulty = reaction delay +
> aim error from `bot_difficulty`. Build/repair behaviors later.

---

## (Historical) Architecture v1 — in-process simulation

## Overview

This document describes the design and implementation of bot (AI) opponents for Block N Load, delivered as a community launcher patch. Since the official servers are offline, bots allow players to practice locally without any network connection.

The feature is integrated into the existing launcher patching pipeline like the other experimental features.

---

## How the Game Works (Relevant Systems)

### Network Architecture

The game uses a custom binary TCP protocol with `Igor` serialization. The connection flow is:

```
LoginLogic → MasterNetworkDispatcher (https://www.blocknload.com/feed/server/)
           → RegionServer selection
           → NetworkDispatcher (game server TCP connection)
           → ZoneServiceListener (receives all in-match messages)
```

All match state flows **from server to client**. The client never simulates game state — it only renders what the server sends.

### Unit System

Every entity in the game is a `Unit` (MonoBehaviour). Units are created via `UnitsRegistry.CreateUnit(id, UnitInit data)` when the server sends a `UnitCreate` message.

Key `Unit` fields:
| Field | Type | Purpose |
|-------|------|---------|
| `Id` | `uint` | Unique identifier |
| `PlayerId` | `uint?` | Owner (null = NPC/turret) |
| `Controlled` | `bool` | True = local player controls this unit |
| `Team` | `TeamType` | Team 1 or Team 2 |
| `Health` / `MaxHealth` | `float` | HP |
| `Gears` | `List<GearData>` | Weapons / equipment |
| `IsDeath` | `bool` | Dead flag |
| `LocalVelocity` | `Vector3` | Current movement |

Units with `PlayerId == null` already exist in the game (turrets, landmines, pickups). Bots extend this pattern.

### UnitHandler — Event Dispatch

`UnitHandler` (component on each Unit GameObject) receives server events:
- `UnitMove(time, ZoneTransform)` — position update for non-controlled units
- `Cast(CastData)` — weapon fire for non-controlled units
- `CastAbility(AbilityCastData)` — ability use
- `Damage(DamageInfo)` — damage received
- `Kill(KillInfo)` — death

The key insight: **non-controlled units already receive all these events and animate normally**. Bots reuse this path.

### ZoneServiceListener — Match Lifecycle

```
InitZone(ZoneInitData)        → builds map, spawns all units
UnitCreate(id, UnitInit)      → spawns one unit mid-match
UnitUpdate(id, UnitUpdate)    → syncs unit state (health, gear, buffs)
UnitMove(id, time, transform) → moves a unit
Cast(unitId, CastData)        → fires a weapon
Kill(KillInfo)                → unit died
UpdateZone(ZoneUpdate)        → match-wide state (scores, timers)
EndMatch(winner)              → match over
```

### PathFindingHelper — Navigation (A*)

Static class with `FindPath(Vector3s start, Vector3s goal) → List<Vector3s>`.

Uses block-level A* on the voxel grid:
- `ZoneManager.GetBlock(pos)` — query block solidity (Id == 0 = air)
- Movement costs: grounded step = 1.0, ungrounded (jump) = 3.0
- 6-directional: forward, back, left, right, up, down

---

## Bot Architecture

Because the game is fully server-authoritative, a true offline bot mode requires **simulating the server locally**. Rather than a full simulation, we take a lighter approach: **intercept the connection attempt and replace the server with a local driver running inside the game process**.

### Approach: Local Match Driver (In-Process Server Stub)

```
BotModeRuntime (injected helper)
├── LocalMatchDriver     — drives the match as a fake server would
│   ├── Spawns player unit + N bot units via ZoneServiceListener calls
│   ├── Runs bot AI tick each frame
│   └── Sends synthetic server events (UnitMove, Cast, UnitUpdate, Kill...)
├── BotController[]      — one per bot unit
│   ├── BotBrain         — decision-making (state machine)
│   ├── NavAgent         — wraps PathFindingHelper
│   └── ActionExecutor   — emits fake server events
└── OfflineMapLoader     — loads a bundled map without a live server
```

The injected IL patches:
1. **Skip server connection** — when bot mode is enabled, bypass `MasterNetworkDispatcher.Connect()` and `NetworkDispatcher.Connect()`.
2. **Intercept `InitZone`** — call `LocalMatchDriver.Initialize()` instead of waiting for a real `ZoneInitData` packet.
3. **Hook `ZoneManager.Update()`** — tick `LocalMatchDriver.Tick()` each frame.
4. **Expose `ZoneServiceListener` instance** to the runtime so it can inject synthetic events.

### Bot State Machine

Each `BotController` runs a simple FSM:

```
States:
  SPAWN     → wait for unit to be alive
  ROAM      → wander toward enemy base (uses PathFindingHelper)
  ENGAGE    → enemy in range → shoot primary weapon
  RETREAT   → health < 30% → return toward spawn
  BUILD     → place a defensive device if carrying one
  DEAD      → wait for respawn
```

Transitions:
```
SPAWN  → ROAM    (unit alive)
ROAM   → ENGAGE  (enemy unit within weapon range)
ROAM   → BUILD   (near own base and has unplaced device)
ENGAGE → ROAM    (enemy dead or out of range)
ENGAGE → RETREAT (health < 30%)
RETREAT→ ROAM    (health > 70% or back at spawn)
ANY    → DEAD    (IsDeath == true)
DEAD   → SPAWN   (IsDeath == false)
```

### Action Execution

Bots act by calling `ZoneServiceListener` methods directly (same code path real server packets use):

```csharp
// Move bot unit to next path node
listener.UnitMove(botUnit.Id, serverTime, new ZoneTransform { Position = nextNode });

// Shoot at target
listener.Cast(botUnit.Id, new CastData {
    ToolIndex = 0,
    ShotPos = botUnit.transform.position,
    Shots = [ new ShotData { Target = targetUnit.transform.position } ]
});

// Bot died (after simulated damage)
listener.Kill(new KillInfo { TargetId = botUnit.Id, ... });
```

---

## Implementation Files

### Launcher Side (C#, .NET 8)

| File | Purpose |
|------|---------|
| `src/BnlCommunityFixes.Core/Models/BotModeSettings.cs` | Settings POCO: enabled, bot count, difficulty |
| `src/BnlCommunityFixes.Core/Features/FeatureConfigCatalog.Gameplay.cs` | +1 entry: `"bot-mode"` |
| `src/BnlCommunityFixes.Core/Services/FeatureSettingsService.cs` | Load/Save BotModeSettings |
| `src/BnlCommunityFixes.Core/Features/Build/Patching/BotModeFeaturePatcher.cs` | IL patcher: hooks 4 methods |
| `src/BnlCommunityFixes.Core/Features/Build/Patching/ExperimentalFeaturePatcherCatalog.cs` | Register `BotModeFeaturePatcher` |

### Game Helper (C#, Unity-compatible, compiled into helper assembly)

| File | Purpose |
|------|---------|
| `assets/patching/BotModeRuntime.cs` | Entry point called by IL hooks |
| (future) `assets/patching/BotController.cs` | Per-bot FSM + pathfinding |
| (future) `assets/patching/LocalMatchDriver.cs` | Fake server driver |

---

## IL Patches Applied

### Patch 1 — Skip Master Server HTTP Fetch

**Target:** `MasterNetworkDispatcher` method that does `HTTP GET https://www.blocknload.com/feed/server/`

**Injection:** Before the HTTP call, check `BotModeRuntime.IsEnabled()`. If true, call `BotModeRuntime.OnMasterServerBypassed()` and return early.

### Patch 2 — Intercept Match Start

**Target:** `SceneManager.ServerLoadZone(SceneZone scene)`

**Injection:** At method start, call `BotModeRuntime.TryStartLocalMatch(scene)`. If bot mode active and it handles the call, return early (skip server-driven scene load).

### Patch 3 — Tick Bot AI Each Frame

**Target:** `ZoneManager.Update()`

**Injection:** Before `Ret`, call `BotModeRuntime.Tick()`.

### Patch 4 — Expose ZoneServiceListener

**Target:** `ZoneServiceListener` singleton init (Awake or `Initialize`)

**Injection:** After init, call `BotModeRuntime.RegisterListener(this)` so the runtime holds a reference to drive synthetic events.

---

## Config File

`experimental-bot-mode-config.json`:
```json
{
  "enabled": false,
  "bot_count": 3,
  "difficulty": "medium",
  "map": "default"
}
```

| Field | Values | Default |
|-------|--------|---------|
| `enabled` | bool | `false` |
| `bot_count` | 1–9 | `3` |
| `difficulty` | `"easy"`, `"medium"`, `"hard"` | `"medium"` |
| `map` | map key string | `"default"` |

---

## Build / Phased Plan

### Phase 1 — Infrastructure (current)
- [x] Architecture documentation (this file)
- [x] `BotModeSettings.cs`
- [x] Feature catalog entry
- [x] Settings service Load/Save
- [x] `BotModeFeaturePatcher.cs` (skeleton hooks)
- [x] `BotModeRuntime.cs` (skeleton with `IsEnabled()` + `Tick()` stubs)

### Phase 2 — Offline Match Bootstrap
- [x] Map selection from Catalogue (`CatalogueHelper.MapList.Friendly[0]`, fallback to Tutorial)
- [x] `BuildZoneInitData()` — construct `ZoneInitData` from local `CardMap.Data`
- [x] `cachedListener.InitZone()` — creates the map world via `ZoneManager.CreateMap`
- [x] `SpawnPlayerUnit()` — Team1, Controlled=true, default gears from CardUnit catalogue
- [x] `SpawnBotUnit()` — Team2, PlayerId=null, hero from catalogue pool
- [x] `BuildInitialZoneUpdate()` — sends `ZonePhaseType.Playing` so UI doesn't crash
- [x] Patch 1: bypass `LoginLogic.DoLogin()` + load `SceneMenu` directly
- [x] Patch 2: intercept `SceneManager.ServerLoadZone()` + load `SceneZone` locally
- [x] Patch 3: `ZoneServiceListener.Start()` → `RegisterListener` triggers deferred `StartLocalMatch()`

### Phase 3 — Bot AI
- [x] `BotController` with FSM: Spawn → Roam → Engage → Retreat → Dead
- [x] `PathFindingHelper.FindPath()` integration for navigation (`UnitMove` calls)
- [x] `listener.Cast(CastData)` for primary weapon fire
- [x] Difficulty scaling: `reactionDelaySec` + `aimInaccuracyUnits` per easy/medium/hard
- [x] Enemy detection via `UnitsRegistry.GetAllPlayersByTeam()`
- [x] Health-based retreat / re-engage transitions

### Phase 4 — Polish
- [x] UI launcher settings page — "Bot Mode" tab in Feature Settings window
  - Enabled toggle (with warning about login bypass)
  - Bot count spinner (1–9)
  - Difficulty ComboBox (easy / medium / hard)
  - Map key text field (default = auto-pick from catalogue)
- [x] `BotModeSettings` wired into `FeatureSettingsViewModel` Load + Save
- [x] Bot respawn handling (`respawnQueue` → `UnitDrop` + re-`UnitCreate` after 6 s; player respawns too)
- [x] Win condition simulation (objective units tracked; `EndMatch` when a team's objectives are all destroyed)

### Phase 5 — Combat & simulation fixes (2026-07-04)

- **Block-break stutter**: `CollectFloatingCluster` BFS now early-exits as soon as the cluster
  is proven anchored and caps at 512 nodes (was flooding the whole ~1M-block map per break).
- **Block damage**: per-pellet `WorldDamage` extracted recursively from the tool's
  `HitEffect` graph (`InstEffectBunch` → `InstEffectDamage`/`InstEffectDamageBlocks`/`InstEffectSplashDamage`).
  No more hardcoded 30f fallback — tools without world damage don't damage blocks (matches live).
- **Building**: `BuildInfo.DeviceKey` is a `CardDevice` key; it is now resolved via
  `CardDevice.DeviceKey`/`StartingDeviceKey` to the internal `CardBlock`/`CardUnit` before placement.
- **Unit damage**: `ServiceZone.Hit` entries with `TargetId` now damage units
  (`ApplyOfflineUnitDamage` → `Damage` + `UnitUpdate.Health`, `Kill` + `UnitDrop` on death).
  Bots also apply simulated damage to the player after `Cast` (hit chance scales with
  difficulty inaccuracy and distance).
- **Bot navigation**: A* goals capped at 24 blocks (full-map searches were slow/failing),
  2 s backoff after failed searches, `ResolveGroundHeight` searches down from the bot's own
  height instead of the map top (was teleporting bots onto ceilings). Bots log state every 10 s.
- **Map units / objectives**: catalogue map's `SpawnPoints`/`Units` are reused for the bundled
  map when they fit the decoded size; `SpawnMapUnits` creates them (ids 11000+) and tracks
  `UnitLabel.Objective` cubes for the win condition.

---

## Key Decompiled Game Files (Reference)

| File | Location in game dump |
|------|-----------------------|
| `PathFindingHelper.cs` | `J:\Block N Load Exported\PathFindingHelper.cs` |
| `Unit.cs` | `J:\Block N Load Exported\Unit.cs` |
| `UnitHandler.cs` | `J:\Block N Load Exported\UnitHandler.cs` |
| `ZoneServiceListener.cs` | `J:\Block N Load Exported\ZoneServiceListener.cs` |
| `ZoneManager.cs` | `J:\Block N Load Exported\ZoneManager.cs` |
| `UnitsRegistry.cs` | `J:\Block N Load Exported\UnitsRegistry.cs` |
| `SceneManager.cs` | `J:\Block N Load Exported\SceneManager.cs` |
| `MasterNetworkDispatcher.cs` | `J:\Block N Load Exported\MasterNetworkDispatcher.cs` |
| `LoginLogic.cs` | `J:\Block N Load Exported\LoginLogic.cs` |
| `Protocol/UnitInit.cs` | `J:\Block N Load Exported\Protocol\UnitInit.cs` |
| `Protocol/CastData.cs` | `J:\Block N Load Exported\Protocol\CastData.cs` |
| `Protocol/ZoneInitData.cs` | `J:\Block N Load Exported\Protocol\ZoneInitData.cs` |
