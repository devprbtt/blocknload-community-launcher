# Match Replay Recorder and Analyzer

This is an experimental replay pipeline. It does not use a built-in Block N Load replay format; it records selected game network/zone messages from the patched client and analyzes them outside the game.

## Components

### Recorder

The recorder lives in:

- `assets/patching/MatchReplayRecorderRuntime.cs`
- `assets/patching/experimental-match-replay-recorder-config.json`
- `assets/patching/Build-ExperimentalCrosshairAssembly.ps1`

When `experimental-match-replay-recorder-config.json` has `"enabled": true`, the patch builder injects calls into `Protocol.ServiceZone.Recv_*` methods. Each received zone packet is written as one JSON line. The same config can limit recording to selected match scopes:

- `record_custom_games`
- `record_casual_games`
- `record_ranked_games`

Capture files are written by the game to:

```text
<BlockNLoad>\Win64\BlockNLoad_Data\bnl-match-replays\zone-capture-YYYYMMDD-HHMMSS.jsonl
```

Live capture files are written as plain `.jsonl` so they can be flushed and analyzed immediately after a match. Recording now starts on each `Recv_InitZone`; any previous open capture is closed before a new one starts, which prevents a custom-map session and a casual session from being appended into the same replay file. Before opening the file, the recorder classifies the match as custom, ranked, or casual and skips it when that scope is disabled. Custom is read from `ZoneInitData.IsCustomGame`; ranked is detected from live `ZoneData.IsRankedGame` or the matchmaker queue key when available; otherwise non-custom matches are treated as casual. The recorder writes `session_end` when it sees the match result or when the process exits, including Alt-F4. The launcher compresses completed captures to `.jsonl.gz` after they are no longer active, and the analyzer reads both formats transparently.

Each capture starts with a `session_start` record and then contains `zone_packet` records with:

- Unity/game time
- packet/event name, such as `Recv_UnitMove`
- payload size
- base64 packet payload

The current payload cap is `262144` bytes so `Recv_InitZone` is captured completely.

## Analyzer

The analyzer lives in:

```text
src\BnlCommunityFixes.ReplayAnalyzer
```

It reads a `.jsonl` capture, decodes core packet payloads, writes CSV timelines, and generates a standalone `viewer.html`.

When the game catalogue cache is available at `<BlockNLoad>\Cache\cdb`, the analyzer also decompresses it and scans card IDs. It uses the same CRC32 algorithm as the game's `Key` type to resolve hashes into readable IDs such as:

- `unit_hero_ninja`
- `map_sc_heroes_causeway`
- `impact_melee_common`

Usage:

```powershell
K:\BNL EXPORTED\v2\release\replay-analyzer-test\BnlCommunityFixes.ReplayAnalyzer.exe `
  "H:\Programas\Steam\steamapps\common\BlockNLoad\Win64\BlockNLoad_Data\bnl-match-replays" `
  "K:\BNL EXPORTED\v2\test-output\replay-analysis-latest"
```

The first argument can be either:

- a specific `zone-capture-*.jsonl` file
- the replay folder, in which case the newest capture is used

## Outputs

The analyzer writes:

- `summary.txt`
- `validation.txt`
- `replay.normalized.json`
- `viewer.html`
- `packets.csv`
- `map_spawn_points.csv`
- `map_units.csv`
- `map_cameras.csv`
- `map_triggers.csv`
- `map_blocks.csv`
- `map_block_counts.csv`
- `init_block_updates.csv`
- `unit_creates.csv`
- `unit_moves.csv`
- `unit_updates.csv`
- `unit_drops.csv`
- `unit_maneuvers.csv`
- `damage.csv`
- `kills.csv`
- `impacts.csv`
- `zone_events.csv`
- `zone_updates.csv`
- `players.csv`
- `player_units.csv`
- `objectives.csv`
- `projectile_creates.csv`
- `projectile_moves.csv`
- `projectile_drops.csv`
- `casts.csv`
- `ability_casts.csv`
- `build_starts.csv`
- `build_cancels.csv`
- `devices_built.csv`
- `build_placements.csv`
- `build_placement_footprints.csv`
- `block_mined.csv`
- `block_updates.csv`
- `block_update_items.csv`
- `map_state_timeline.csv`
- `map_state_verification.txt`
- `map_state_final_counts.csv`
- `map_state_changed_cells.csv`
- `barrier_updates.csv`
- `reloads.csv`
- `channels.csv`
- `dash_charges.csv`
- `pickup_taken.csv`
- `recalls.csv`
- `portal_teleports.csv`
- `rpc_results.csv`
- `surrender_events.csv`
- `surrender_progress.csv`
- `end_match_players.csv`
- `init_map_data.bin`
- `init_color_data.bin`
- `map_blocks_data.bin` when present in `MapData`
- `map_colors_data.bin` when present in `MapData`

Open `viewer.html` in a browser to inspect the replay visually.

`map_blocks.csv`, `map_block_counts.csv`, and `replay.normalized.json` include block catalogue names when the local `Cache\cdb` catalogue is available, for example `3 = block_stone`, `4 = block_lava`, and `10 = block_metal`.

`player_units.csv` joins player IDs, nicknames, Steam IDs, team, spawned unit, skin key, and gear keys. This is the easiest file to use when a replay renderer needs to label a unit as a player or attach the right hero/loadout metadata.

`end_match_players.csv` decodes the match-result payload into scoreboard-style player rows, including total score, category stats, medals, and reward/result metadata when present.

`unit_updates.csv` decodes unit runtime state changes beyond health: team, capture/resource values, current gear, ability state, ammo, effects, buffs, device state, turret target, projectile init speed, bomb timeout, portal link, and Tesla charge when those fields are present in the packet.

`channels.csv`, `dash_charges.csv`, `pickup_taken.csv`, `recalls.csv`, and `portal_teleports.csv` decode special action packets that were previously only counted in `packets.csv`. These are important for replaying channelled tools, charge-start/charge-end tools, pickup UI/audio, recall effects, and portal movement accurately.

`replay.normalized.json` is the stable handoff format for future launcher and in-game replay work. It removes the raw network payloads and stores decoded match metadata, map init metadata, map dimensions, map spawn points, static map units, decoded map block counts and block names, asset references for raw map/color bytes, player/unit identity links, units, movement tracks, health/shield updates, drops, maneuvers, damage, kills, impacts, abilities, build/device activity, block updates, barrier updates, reload state, surrender state, match result data, RPC results, and packet counts. Future replay UI should read this file instead of parsing `zone-capture-*.jsonl` directly.

The packed map/color byte arrays are written separately as `.bin` files so a future in-game loader can consume the original bytes without bloating the normalized JSON. The analyzer also decodes those bytes into `map_blocks.csv`, which contains sparse non-empty blocks with `x/y/z`, block `id`, `damage`, `vdata`, `ldata`, derived `vdata_low_byte`, `vdata_high_byte`, `slope_existing_corner_count`, `slope_existing_corners`, `slope_missing_corners`, `team_bits`, `team`, `ldata_flags`, and `color`.

`block_update_items.csv` expands every `Recv_BlockUpdates` packet into one row per changed block. This is the file a future map-state player should consume to apply block changes over time. The game client code shows `Ldata & 3` is the block team (`1 = Team1`, `2 = Team2`, anything else neutral). The low byte of `Vdata` is used by slope logic as slope shape data, where each bit corresponds to a cube corner (`C000`, `C100`, `C010`, `C001`, `C110`, `C011`, `C101`, `C111`) and a cleared bit means that corner exists. Other block families may interpret `Vdata` differently, so the analyzer exports the raw value and low/high bytes too.

`build_placements.csv` joins `Recv_DoStartBuild`, `Recv_DeviceBuilt`, and the nearest same-cell `Recv_BlockUpdates` entry. It gives a future replay player the builder unit, built unit, device key/name, final block cell, build facing direction, and resulting block state in one row.

`build_placement_footprints.csv` expands each built device into nearby block updates within a short time window and two-cell radius. This captures multi-block or area-effect placements where the useful block changes are around the built unit rather than exactly on its center cell, such as fire traps and other trap/device footprints.

`map_state_timeline.csv` is the renderer-facing map change stream. Start with `map_blocks.csv` as the sparse initial non-empty map state, then apply `map_state_timeline.csv` rows in `sequence` order. Each row is one real block update and is annotated as `block_update`, `placement_center`, or `placement_footprint` when the analyzer can link it to a built device.

`map_state_verification.txt` checks that the timeline can be applied as a map-state stream. It merges partial block updates into the previous state, reports final renderable non-air block counts, tracked cells, changed cells, repeated cells, duplicate no-op updates, and out-of-order updates. `map_state_final_counts.csv` and `map_state_changed_cells.csv` provide machine-readable details for debugging.

For the current Causeway capture, `InitZone` provides size `166/38/116`, 731,728 total block cells, 81,671 non-empty block cells, 25 block IDs, 2 map spawn points, 9 static map units, `init_map_data.bin`, and `init_color_data.bin`.

`validation.txt` reports whether a capture is usable for replay. The normalized JSON also includes the same validation object. Required checks currently cover packet presence, full `InitZone`, map key, zero decode errors, unit creation, movement, match end, and end result. Warnings cover player info, key-name resolution, combat coverage, and health updates.
Warnings also report whether map metadata and initial map bytes were decoded.

## Launcher Integration

The launcher has a Match Replays section with:

- `Record match replays`: checkbox that enables/disables match replay recording by writing the `experimental-match-replay-recorder-config.json`. The adjacent `Custom`, `Casual`, and `Ranked` checkboxes filter which match types are recorded. Finished captures are compressed by the launcher on later replay folder scans so long-term storage uses `.jsonl.gz`.
- `Open Folder`: opens the game's `bnl-match-replays` capture folder.
- `Browse Replays`: opens a replay browser that lists captured matches.

The replay browser shows all `zone-capture-*.jsonl` files and supports:

- `Analyze`: runs `BnlCommunityFixes.ReplayAnalyzer.exe` against the selected capture.
- Analysis shows an indeterminate progress bar while the analyzer is running.
- `Open Location`: selects the capture file in Explorer.
- `Delete Selected`: deletes the selected capture file or files.
- `Refresh`: reloads the replay list.
- `Launch Replay Mode`: writes a one-shot `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-launch-request.json` request for the selected analyzed replay, then launches the game through the normal launcher path. The in-game runtime consumes and deletes this request while booting into replay mode so later normal launches do not accidentally start a replay.

Selected replay analysis output is written per capture under `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-analysis\<capture-name>`. The old quick latest output still exists at `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-analysis\latest`.

Release builds publish `BnlCommunityFixes.ReplayAnalyzer.exe` beside `BnlCommunityFixes.exe` and include it in the update manifest as `replay_analyzer_exe`, so users do not need to download the analyzer separately.

If a user updated from an older launcher that did not know about the analyzer asset yet, `Analyze Latest` can fetch `replay_analyzer_exe` from the current manifest on demand before analyzing the replay.

Portable test builds under `release\replay-launcher-test-*` include `portable-launcher.flag`. When this flag is present, the launcher skips bootstrap redirection and skips update checks, so a lower-version replay test build can run beside a newer installed launcher.

## In-Game Replay Prototype

The first in-game replay runtime lives in:

```text
assets\patching\ReplayPlayerRuntime.cs
```

`Build-ExperimentalCrosshairAssembly.ps1` compiles it into `BnlCommunityFixes.dll` and injects `ReplayPlayerRuntime.EnsureInstance()` from `MainMenu.Start`, beside the existing runtime menu bootstrap. The launcher embeds this source as `Patching.ReplayPlayerRuntime.cs`, so users get it through the normal patching asset extraction path.

This is an early runtime proof, not the final replay app. It loads `replay.normalized.json`, creates replay records from the analyzer CSV/JSON output, and in the real-zone F11 path instantiates replay heroes, unit/device visuals, and fallback markers only when their recorded create time is reached. It also consumes `map_state_timeline.csv` and applies block changes forward through the game's `ZoneServiceListener.BlockUpdates` path.

The replay runtime now starts feeding the original spectator/objective UI instead of depending on a custom player HUD. Replay heroes are registered with `ZoneMessenger` and `ZonePlayersCache`, objective/base units are created through `ZoneServiceListener.UnitCreate` when possible, and decoded `unit_updates.csv` health/resource values are forwarded through `ZoneMessenger.OnUnitHealthChange` and `OnUnitResourcesChange`. Number-key follow still exists, but it now also attempts to use `CameraSpectatorView.SpectateUnit`.

Additional live-system bridges:

- `reloads.csv` is replayed through `ZoneServiceListener.DoStartReload` / `DoEndReload`.
- `kills.csv` is replayed through `ZoneServiceListener.Kill`, allowing the live kill/death UI path to receive dead unit/player, killer, assistants, damage source, source position, and crit data.
- `build_placements.csv` is replayed through `ZoneServiceListener.DoStartBuild` and `DeviceBuilt` so the real build ghost/device-built path can run.
- `zone_updates.csv` team statistics are replayed through `ZoneServiceListener.UpdateZone` so the original spectator team score/resource widgets can receive updates.
- `match_player_stats.csv` exports mid-match kills/deaths/assists/team from decoded `ZoneUpdate.Statistics.PlayerStats`; the runtime replays those into `MatchStats.PlayerStats`, which is the data source needed by the live TAB scoreboard.
- Debug world labels are disabled by default; player names should come from the game's own UI/cache path.
- Replay heroes are inserted into `UnitsRegistry` so `GuiSpectatorPlayerPopulation` can resolve rows through `UnitsRegistry.GetByPlayerId`.
- Number-key follow now delegates to `CameraSpectatorView.SpectateUnit` without enabling the prototype free camera, preserving the game's normal orbit/zoom spectator camera behavior.
- Normal replay loads use the richer JSON movement tracks so hero animation flags continue to work. Very large replay loads over 512 MB use CSV movement as a memory fallback and avoid loading huge `replay.normalized.json` files into memory in-game.
- Projectile CSV replay instantiates real projectile prefabs from `CardProjectile.Prefab`. Physics colliders are disabled and rigidbodies are made kinematic so replay visuals do not affect the simulated scene.
- Projectiles that only have create/drop packets can derive a path from `casts.csv` shot targets, so bullets/rockets still appear at the right time and move toward the recorded shot point.
- Replay projectile prefabs explicitly activate and play their child particle systems on spawn, which helps projectiles with prefab-driven trails/auras such as Djinn orb-style projectiles.
- Phase timestamps from capture data are translated onto the current local `IServerTime` every replay update before being sent through `ZoneServiceListener.UpdateZone`, so timer display tracks replay fast-forward/scrub instead of the real wall clock.
- Respawn timestamps from `respawns.csv` are translated from captured server time onto the current local `IServerTime` before `ZoneServiceListener.UpdateZone`, so spectator respawn countdown widgets can count down in replay time.
- Replay projectile prefab audio/sound behaviours are enabled. `impacts.csv` is also replayed through `ZoneServiceListener.Impact`, which lets live impact FX/audio paths run for melee hits, projectile impacts, and explosive impacts when the capture contains an impact key.
- Tool-fire events from `zone_events.csv` are also sent directly to `GearSoundHandler.ToolFire` / `ToolFireLoop` on replay units. This covers gear sounds that do not fire from the higher-level replay event bridge, such as some digging/melee/weapon launch sounds.
- `casts.csv` is replayed through `UnitEventHelper.HandleToolCast` and `GearSoundHandler.ToolCast`, including shot origin, target shot list, shot IDs, and projectile speed when available. This covers tools whose launch sounds/FX are driven by cast data rather than plain fire events.
- Barrier updates from `barrier_updates.csv` are replayed through `ZoneServiceListener.UpdateBarriers`.
- Static device visuals use `build_placements.csv` device positions plus the decoded build surface normal. Wall/floor devices such as mines, tikis, traps, caltrops, and bombs align their local up axis to the surface and are offset back onto the contacted face so side-mounted devices do not fall back to a top-block pose.
- Pickup units now honor their recorded drop/take timing directly instead of forcing a minimum visible lifetime. This keeps health/loot/lamp pickups from lingering after a player takes them.
- Hero loot pickups are included in the static replay unit filter even when their key name is `unit_pickup_hero_loot`, so death-drop health pickups can spawn in the replay scene.
- The analyzer now writes `phase_start`/`phase_end` in `zone_updates.csv` and emits `respawns.csv`; re-analyze captures with the current analyzer before testing match clock and respawn timers.
- The runtime applies decoded `unit_updates.csv` device slot data onto `Unit.Devices`, which is the source used by the real spectator player panel for brick/loadout icons.
- The runtime builds a device name-to-hash cache from analyzer CSVs before reading `unit_updates.csv`, so older analyses that store device slot names can still populate `Unit.Devices`. New analyzer output writes device slot keys as hashes in `unit_updates.csv`.
- The runtime also feeds decoded `unit_updates.csv` effects and buffs into `Unit.UpdateData`, so player buff/debuff state is available to the original unit and buff UI paths when the capture includes those fields.
- Replay hero movement is driven from decoded movement/rotation samples with local smoothing while animation flags are still copied onto the `Unit`. The replay disables `UnitMotor` authority on manually spawned replay heroes because synthetic replay units were producing `UnitMotor.Update` null refs and visible jitter when fed through the live packet path.

Runtime lookup order:

1. `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-launch-request.json`, when it points at an existing `replay.normalized.json`
2. Newest `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-analysis\<capture-name>\replay.normalized.json` that also has `map_blocks.csv`
3. Newest `%LOCALAPPDATA%\BNL-CommunityFixes\data\replay-analysis\<capture-name>\replay.normalized.json`
4. `<BlockNLoad>\Win64\BlockNLoad_Data\bnl-match-replays\replay.normalized.json`
5. `<BlockNLoad>\Win64\BlockNLoad_Data\bnl-match-replays\latest\replay.normalized.json`

Runtime controls:

- `F7`: load the latest normalized replay, then play/pause.
- `F8`: reset to the beginning.
- `F9`: toggle the prototype spectator camera.
- `F10`: show/hide the rendered replay map surface.
- Replay zone loading is launcher-only. The old in-game `F6`/`F11` start path is disabled so a player cannot accidentally start a replay during a live custom or casual match.
- `[` and `]`: scrub backward/forward 5 seconds.
- `-` and `+`: decrease/increase playback speed.
- With the spectator camera enabled, hold right mouse to look, use `WASD` to move, `Space`/`Ctrl` for up/down, and `Shift` for faster movement.

The prototype intentionally uses marker spheres and top-surface map cubes first. The next in-game steps are replacing markers with unit prefabs/animation, rendering block changes over replay time, then adding a proper replay controls UI.

Timed replay spawning is forward-oriented. If replay time moves backwards, units/devices are destroyed and re-created as needed, but block updates are not fully reversible yet; reload with `F11` to restore the exact initial map state before replaying forward again.

The `F11` path is deliberately marked experimental because it depends on the original client accepting a synthetic `Protocol.SceneZone` with `game_mode_custom` and `match_shield_rush_v2_custom`. If it fails, the game log should show which scene, catalogue, or initialization assumption is still missing.

Some captures contain more than one `Recv_InitZone` packet, for example a custom lobby/test map init followed by the actual replay match init. The in-game loader now matches the normalized replay `mapKeyHash` first and falls back to the largest full `InitZone` payload, so selecting a replay in the launcher should not accidentally load the TESTING lobby map.

## Currently Decoded Packets

The analyzer currently decodes:

- `Recv_InitZone`: map key, `MapData`, dimensions, map properties, spawn points, static units, cameras, triggers, packed map/color bytes, initial block updates, custom-game flags
- `Recv_UnitCreate`
- `Recv_UnitMove`
- `Recv_UnitUpdate`
- `Recv_UnitDrop`
- `Recv_UnitManeuver`
- `Recv_Damage`
- `Recv_Kill`
- `Recv_Impact`
- `Recv_BroadcastZoneEvent`
- `Recv_CreateProjectile`
- `Recv_MoveProjectile`
- `Recv_DropProjectile`
- `Recv_Cast`
- `Recv_CastAbility`
- `Recv_DoCastAbility`
- `Recv_StartBuild`
- `Recv_DoStartBuild`
- `Recv_DeviceBuilt`
- `Recv_BlockMined`
- `Recv_UpdateZone`
- `Recv_UpdateBarriers`
- `Recv_BlockUpdates`
- `Recv_SwitchGear`
- `Recv_Reload`
- `Recv_StartReload`
- `Recv_DoStartReload`
- `Recv_DoEndReload`
- `Recv_DoStartChannel`
- `Recv_DoEndChannel`
- `Recv_DoDashStartCharge`
- `Recv_DoDashEndCharge`
- `Recv_DashEndCharge`
- `Recv_PickupTaken`
- `Recv_DoStartRecall`
- `Recv_DoCancelRecall`
- `Recv_DoRecall`
- `Recv_PortalTeleport`
- `Recv_KickPlayer`
- `Recv_SurrenderBegin`
- `Recv_SurrenderStart`
- `Recv_SurrenderProgress`
- `Recv_SurrenderEnd`
- `Recv_EndMatch`
- `Recv_EndMatchResult`

Other packets are still counted in `packets.csv`, but not deeply decoded yet. Current known gaps include any chat packet/RPC once identified in captures, and specialized ability state that is only observable through unit effects/buffs unless we add ability-specific interpretation.

## Viewer Features

The generated viewer currently provides:

- top-down unit movement playback
- playback speed control
- scrubber
- unit trails
- team colors
- health bars when health updates are available
- damage popups
- impact and zone event feed entries
- phase, player, and objective feed entries when `Recv_UpdateZone` includes them
- projectile creation/drop feed entries when projectile packets are present
- impact markers and shot lines on the playback canvas
- projectile trails and active projectile dots when projectile packets are present
- ability, build, and device-built feed entries
- ability shot lines and target markers on the playback canvas
- build ghost markers and device-built markers on the playback canvas
- decoded event feed
- packet count summary
- resolved map/unit/impact IDs when catalogue cache data is available

## Limitations

This is still a debug replay viewer, not full game playback.

Known limitations:

- Resolved names currently use internal card IDs, not localized display names.
- If `<BlockNLoad>\Cache\cdb` is missing or unreadable, names fall back to key hashes.
- Map rendering is still only a grid; `InitZone` map blocks are decoded into CSV/JSON samples, but the viewer does not yet render those blocks as geometry.
- Ability casts and build/device events are decoded, but ability-specific effects are not simulated.
- Channel, dash-charge, pickup-taken, recall, and portal-teleport packets are decoded to CSV, but the in-game runtime does not yet replay all of them through live client systems.
- Projectile CSV files can be empty for heroes/tools that do not emit projectile packets.
- Some weapons that look like projectiles in-game may only emit `Recv_Impact` and tool-fire events, not `Recv_CreateProjectile`/`Recv_MoveProjectile`.
- Objective data only appears when the server sends it in `Recv_UpdateZone`; some captures may have no objective records.
- Cube/objective health is still not proven. The current long Causeway analysis has an empty `objectives.csv`, so the top cube-health widget cannot be reconstructed from objective records yet. We need to find whether cube health lives in objective records, objective unit health updates, damage capturer state, or another packet.
- TAB scoreboard data is now exported as `match_player_stats.csv` and replayed into `MatchStats.PlayerStats`, but the live TAB panel still may need an explicit UI open/input bridge in replay mode.
- Chat is not decoded yet. No chat-specific CSV exists, and we still need to identify whether chat is captured as a zone packet, RPC result, or a separate event not currently recorded.
- The original spectator UI now receives replay unit events, but any widget that depends on not-yet-decoded server state can still display incomplete data.
- Player death animation still depends on original client handlers. The runtime now triggers `OnUnitDrop`/`UnitDie` at kill time while keeping the body visible briefly, but if the replay-created unit is missing animation state or animator parameters, it can still freeze on the previous animation.
- The recorder is experimental and should remain opt-in until performance and file sizes are better understood.
- Captures are client-side observations, not authoritative server replay files.

## Next Work

The most useful next improvements are:

1. Identify and replay the live TAB scoreboard open path in replay mode.
2. Locate cube/objective health source and feed the top objective UI.
3. Identify chat packet/RPC source and add chat export/replay if captures contain it.
4. Replay decoded channel, dash-charge, pickup-taken, recall, and portal-teleport events through live game systems.
5. Fix replay-created unit death animation state so `OnUnitDrop` produces the same animation/ragdoll behavior as live matches.
6. Continue ability-specific FX mapping for tools whose visuals are not covered by projectile/cast/impact packets.
