# Replay Camera Feature — Engineering Handoff

**Project:** BNL Community Fixes (Block N Load modded launcher)  
**Branch:** main  
**Last build:** v2.4.1 (local test only, not released to GitHub)  
**Date:** 2026-05-15  

---

## Context

This launcher patches the game's `Assembly-CSharp.dll` at runtime. The patching flow is:

1. `ReplayPlayerRuntime.cs` (source in `assets/patching/`) is extracted from the launcher as an embedded resource.
2. `Build-ExperimentalCrosshairAssembly.ps1` compiles it with Mono's `csc.exe` targeting **C# 5 / old Unity Mono** — no C# 6+ syntax allowed (no null-conditional, no string interpolation, no inline `out` vars).
3. Output is `Assembly-CSharp.experimental.dll`, deployed by the launcher as `Assembly-CSharp.dll` into the game's managed folder before launch.
4. The launcher itself (`BnlCommunityFixes.dll`) is .NET 8; the patched game DLL runs in old Mono inside Unity.

**Critical constraint:** Every line in `ReplayPlayerRuntime.cs` must compile with old Mono `csc.exe`. Several .NET BCL methods have different/missing overloads on that runtime. Old Mono JITs entire method bodies at once — a missing method anywhere in a method body causes `MissingMethodException` before any try/catch runs.

---

## Hero Rig Naming Conventions (confirmed from game logs)

- Head bone: `{HeroName}_Head` (e.g. `Frag_Head`, `Sarge_Head`)
- Weapon attachment nodes: `AttachmentNode_{WeaponName}` (e.g. `AttachmentNode_SniperRifle`, `AttachmentNode_RightHand`)
- **Typo variant exists:** `AttachementNode_Knife` (extra `e`) — code handles both
- Chest approximation: average of `{HeroName}_Chest` and `{HeroName}_Spine` (fallback: `transform.position + Vector3.up * 1.2f`)

---

## Rotation Data Format

`ReplaySample.Rotation` stores degrees×10 as a `Vector3`:
- `.x` = pitch × 10
- `.y` = yaw × 10
- `.z` = roll × 10

`MoveRealReplayUnit` uses `RotationToYawQuaternion` which **strips pitch** (Y-axis only) for the unit body. FP camera reads pitch directly:

```csharp
float pitch = fpSample.Rotation.x / 10f;
float yaw   = fpSample.Rotation.y / 10f;
// Normalise to -180..180 to prevent Slerp wraparound spin:
while (pitch > 180f)  pitch -= 360f;
while (pitch < -180f) pitch += 360f;
while (yaw > 180f)    yaw   -= 360f;
while (yaw < -180f)   yaw   += 360f;
pitch = Mathf.Clamp(pitch, -89f, 89f);
// Ensure Slerp takes short arc:
if (Quaternion.Dot(fpSmoothedRot, eyeRotation) < 0f)
    eyeRotation = new Quaternion(-eyeRotation.x, -eyeRotation.y, -eyeRotation.z, -eyeRotation.w);
```

---

## Game Rendering Architecture (confirmed from layer/camera dumps)

**Unity layers:**
```
0:Default  1:TransparentFX  2:IgnoreRaycast  4:Water  5:UI
8:Chunks  9:UnitHeroTrigger  10:NGUI  11:PlayerModel  12:Pickup
13:Base  14:Player  15:WorldEffect  16:ChunksTrigger  17:Projectiles
18:Outline  19:Barrier  20:UnitHealthbar  21:AimAssist
22:ClothCollider  23:IgnoreScreenshot
```

**Scene cameras:**
- `MainCamera` (Camera.main): depth=-1, clearFlags=Skybox, cullingMask=-1051649 (excludes NGUI + PlayerModel), renderingPath=DeferredShading
- `PlayerModelCamera` (tag: `ArmsCamera`): depth=0, clearFlags=Depth, cullingMask=2048 (Layer 11: PlayerModel ONLY), FOV=30, managed by `CameraArms` component

**Key findings:**
- BNL does NOT split arms into separate meshes — replay units use full 3rd-person skin (body+head+arms in one mesh on `UnitHeroTrigger` layer)
- `CameraDeath` MonoBehaviour on Camera.main applies B&W desaturation — it's one of 11 components on Camera.main
- `PlayerModelCamera` also has `DesaturationPostEffect` and `FlashPostFx` — must disable all MonoBehaviours on it too
- Disabling the `Camera` component on Camera.main nullifies `Camera.main` — must cache the reference before disabling
- New synthetic cameras default to Forward rendering — must copy `renderingPath` from Camera.main before disabling it

---

## Camera Features Implemented

### F4 — Weapon/Cinematic Camera (`weaponCamActive`)

**Status: Working**

Synthetic `Camera` at chest height, facing back toward the player:

```csharp
Vector3 camPos = chestPos + facingRotation * Vector3.forward * 2.4f + Vector3.up * 0.2f;
camera.transform.rotation = Quaternion.LookRotation(chestPos - camPos, Vector3.up);
```

Key method: `UpdateWeaponCamera()`

---

### F3 — First-Person Camera (`fpCameraActive`)

**Status: Working** — colour, smooth, animated weapons, F3 toggle in/out functional

#### Camera Pipeline

```
Render order:
  1. fpCamera      (depth=100) clearFlags=Skybox, cullingMask=all except PlayerModel(11)
                   renderingPath copied from Camera.main
  2. ArmsCamera    (depth=101) clearFlags=Depth, cullingMask=PlayerModel(11) only
                   parented to fpCamera at localPosition=zero

Camera.main: Camera component disabled + all 11 MonoBehaviours disabled
             GameObject stays ACTIVE so Camera.main resolves for other game scripts
             Reference cached in fpMainCamRef before disabling
```

#### On F3 Activate

1. Find ArmsCamera via `FindGameObjectsWithTag("ArmsCamera")` **before** disabling Camera.main (inactive GOs are skipped by tag search)
2. Copy `renderingPath` from Camera.main to fpCamera
3. Disable Camera.main's `Camera` component + all MonoBehaviours (cache ref in `fpMainCamRef`)
4. Find weapon renderers (under `AttachmentNode_*` bones) → move to PlayerModel layer (11)
5. Disable all non-weapon (body/skin) renderers on followed unit
6. Disable all MonoBehaviours on ArmsCamera (incl. `CameraArms`, `DesaturationPostEffect`, `FlashPostFx`) → re-enable `Camera` component
7. Parent ArmsCamera to fpCamera at `localPosition=zero, localRotation=identity`
8. Set fpCamera FOV=120, ArmsCamera FOV=120

#### On F3 Disable

1. Restore Camera.main via `fpMainCamRef.enabled = true` + restore cullingMask + re-enable all cached MonoBehaviours
2. Restore weapon renderer layers to originals
3. Re-enable body/skin renderers
4. Restore ArmsCamera: re-enable cached MonoBehaviours, unparent back to original parent, depth=0

#### Smoothing

Position and rotation are smoothed to remove bone animation jitter:

```csharp
// Initialised to unit position on activate — no snap on first frame
fpSmoothedPos = Vector3.Lerp(fpSmoothedPos, eyePosition, 6f * Time.deltaTime);   // slow = kills jitter
fpSmoothedRot = Quaternion.Slerp(fpSmoothedRot, eyeRotation, 20f * Time.deltaTime); // fast = responsive aim
```

Position speed 6 damps bone micro-shake while tracking player movement. Rotation speed 20 keeps aim feeling responsive.

#### Weapon Rendering

Weapon renderers stay on their original bone (animations play — digging, shooting etc). Only their layer is changed to PlayerModel(11) so ArmsCamera renders them. ArmsCamera is parented to fpCamera so it always looks at the weapon from eye position.

---

## Approaches Tried (full log)

| # | Approach | Result |
|---|---|---|
| 1 | Synthetic fpCamera only, Camera.main untouched | ✅ World in colour. ❌ No arms |
| 2 | Move ALL unit renderers to PlayerModel | ❌ Full body visible from inside head |
| 3 | Move only weapon renderers to PlayerModel | ✅ Weapons visible. ❌ Body visible |
| 4 | Disable body renderers | ✅ Weapons only. ❌ B&W |
| 5 | Camera.main cullingMask=0 | ❌ B&W (OnRenderImage still runs) |
| 6 | Disable all MonoBehaviours on Camera.main | ❌ Still B&W (11 disabled, but B&W persisted) |
| 7 | Camera.main GameObject.SetActive(false) | ✅ Colour. ❌ Camera.main=null breaks game scripts, F3 toggle breaks |
| 8 | Camera.main.enabled=false (component only) | ✅ Colour. ❌ Camera.main=null (same issue) |
| 9 | Camera.main.enabled=false + cache ref in fpMainCamRef | ✅ Colour. ✅ Toggle works. ✅ Current approach |
| 10 | ArmsCamera not parented to fpCamera | ❌ Weapons render from wrong world position (float) |
| 11 | Parent ArmsCamera to fpCamera | ✅ Weapons at eye position |
| 12 | Parent weapon GOs to fpCamera at fixed offset | ✅ Fixed position. ❌ Animations frozen |
| 13 | Keep weapons on bone, only change layer | ✅ Animations play. ✅ Current approach |
| 14 | fpCamera renderingPath=Forward (default) | ❌ B&W (game uses Deferred) |
| 15 | Copy renderingPath from Camera.main | ✅ Colour |

---

## Key Fields Added to ReplayPlayerRuntime.cs

```csharp
// FP camera
private bool fpCameraActive;
private Camera fpCamera;
private Camera fpMainCamRef;                     // Camera.main ref cached before disabling
private int fpMainCamOrigCullingMask;
private MonoBehaviour[] fpMainCamDisabledEffects;
private bool fpMainCamWasActive;
private UnitMarker fpFollowMarker;
private string fpBoneName;
private Vector3 fpSmoothedPos;                   // smoothed camera position (speed 6)
private Quaternion fpSmoothedRot;                // smoothed camera rotation (speed 20)
private int fpDiagFrames;

// ArmsCamera hijack
private Camera fpArmsCamera;
private Transform fpArmsCameraOrigParent;
private MonoBehaviour[] fpArmsCamDisabledComponents;

// Weapon rendering
private int[] fpWeaponRendererOrigLayers;
private GameObject[] fpWeaponRenderers;
private string fpWeaponUnitId;

// Body hiding
private List<Renderer> fpDisabledBodyRenderers;
```

---

## Known Remaining Issues / Next Steps

- **Weapon placement** — weapon renders at its 3rd-person bone position, not true viewmodel position. Looks reasonable but not identical to in-game FP arms placement.
- **Crosshair** — not implemented. Can be added via `OnGUI` with a simple screen-center dot/cross using `GUI.Label` or `GUI.DrawTexture`. No game system dependency needed.
- **Damage numbers** — not implemented. Replay data has damage events but they need a separate event-driven spawning system to show floating text at world positions at the right replay timestamps.
- **Camera shake on movement** — smoothing at speed 6 reduces but doesn't fully eliminate bone jitter. Could go lower (3-4) if still too shaky, at cost of slight position lag.

---

## Respawn / UnitId Handling

`PlayerReplayInfo.UnitId` is first-spawn only. On respawn a new `UnitMarker` with a new `UnitId` is created.

Fix: `TryReattachMarker` — two-step lookup:
1. Find marker by `UnitId` (fast path)
2. Fall back to scan all markers by `PlayerId`

Called from `UpdateFirstPersonCamera` and `UpdateWeaponCamera` each frame.

---

## Number Keys 1–9 Re-targeting

If `fpCameraActive || weaponCamActive`, pressing 1–9 re-targets to that player slot instead of switching to `CameraSpectatorView`. On retarget: old unit's weapon layers and body renderers are restored, new unit's are set up.

---

## Key Bindings

| Key | Action |
|---|---|
| F3 | Toggle first-person camera |
| F4 | Toggle weapon/cinematic camera |
| F5 | RuntimeMenu: Save settings (do not use) |
| F6 | RuntimeMenu: Reset settings (do not use) |
| 1–9 | Re-target active camera / switch spectator |

---

## Build Instructions

Always use the Bash tool (PowerShell tool fails silently on dotnet):

```bash
cd "k:\BNL EXPORTED\v2"
dotnet publish src/BnlCommunityFixes.Avalonia/BnlCommunityFixes.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=2.4.1 -o release/2.4.1/launcher
dotnet publish src/BnlCommunityFixes.Updater/BnlCommunityFixes.Updater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=2.4.1 -o release/2.4.1/updater
`BnlCommunityFixes.exe --analyze-replay <capture-or-directory> [output-directory]`
```

## Game Log Location

```
H:\Programas\Steam\steamapps\common\BlockNLoad\Win64\BlockNLoad_Data\output_log.txt
```

Filter for `[BNL Replay]` prefix. Key diagnostic lines:
- `FP: Camera.main disabled (11 components)` — successful activate
- `FP weapon: ArmsCamera components disabled: CameraArms, FlashPostFx, DesaturationPostEffect` — ArmsCamera hijacked
- `FP toggle: disabling` / `FP toggle: activating` — F3 toggle working
