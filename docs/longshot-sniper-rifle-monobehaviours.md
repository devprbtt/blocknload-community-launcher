# Longshot Sniper Rifle MonoBehaviour Reference

This file records the Unity `GameObject` hierarchies and linked `MonoBehaviour` components found by inspecting these asset bundles:

- Current game bundle: `I:\SteamLibrary\steamapps\common\BlockNLoad\assetbundles\character_longshot`
- Old depot bundle: `H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Weapons`
- Old depot bundle scanned for candidate names: `H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\CharacterLongshot`

## Current Bundle: `character_longshot`

### Scripted `SniperRifleOneBarrel` prefab roots

#### `SniperRifleOneBarrel` (`path_id -7328582275522356648`)

Linked `MonoBehaviour` components:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`
- `GearWhizzbySoundHandler`

Hierarchy:

```text
- SniperRifleOneBarrel [-7328582275522356648] :: GearModel, GearModelShotEffect, GearSoundHandler, GearWhizzbySoundHandler
  - SniperRifleOneBarrel_Unit [5009809638469680314] :: Animation
    - SniperRifle_Root [-5824073081599465646] :: no extra comps
      - SniperRifle_Clip [1653726778765601092] :: no extra comps
      - SniperRifle_Handle [-8124034043776094658] :: no extra comps
        - SniperRifle_Bullet [3591730828477525015] :: no extra comps
      - SniperRifle_Trigger [-6712007801088338169] :: no extra comps
      - BulletSpot [7446983293497098347] :: no extra comps
    - SniperRifleOneBarrel [-8472908538850250589] :: SkinnedMeshRenderer
```

#### `SniperRifleOneBarrelS3` (`path_id 8810961220235286000`)

Linked `MonoBehaviour` components:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`
- `GearWhizzbySoundHandler`

Hierarchy:

```text
- SniperRifleOneBarrelS3 [8810961220235286000] :: GearModel, GearModelShotEffect, GearSoundHandler, GearWhizzbySoundHandler
  - SniperRifleOneBarrel_Unit [1502535500438807435] :: Animation
    - SniperRifle_Root [4428820928595827094] :: no extra comps
      - SniperRifle_Clip [-541493817193577572] :: no extra comps
      - SniperRifle_Handle [4625403787438114595] :: no extra comps
        - SniperRifle_Bullet [-4221231282424803208] :: no extra comps
      - SniperRifle_Trigger [-4733568540125330464] :: no extra comps
      - BulletSpot [4958356846742502141] :: no extra comps
    - SniperRifleOneBarrel [-3365598366698442449] :: SkinnedMeshRenderer
```

### Related `OneBarrel` objects without weapon scripts

#### `LongshotSniperRifleOneBarrelUnit` (`path_id 1172468483962977848`)

Linked `MonoBehaviour` components:

- `UnitAnimationContainer`

Hierarchy:

```text
- LongshotSniperRifleOneBarrelUnit [1172468483962977848] :: UnitAnimationContainer
```

#### Mesh-only children

These `GameObject`s do not have weapon `MonoBehaviour`s attached:

- `SniperRifleOneBarrel` (`path_id -8472908538850250589`) -> `Transform`, `SkinnedMeshRenderer`
- `SniperRifleOneBarrel` (`path_id -3365598366698442449`) -> `Transform`, `SkinnedMeshRenderer`

## Old Depot: `Weapons`

The old depot contains explicit no-hammers mesh objects:

- `SniperRifle_mesh_noHammers`
- `SniperRifle_lod_noHammers`

Those are mesh children, not the scripted weapon root. The linked `MonoBehaviour`s live on the parent `SniperRifle` prefab roots.

### Scripted no-hammers sniper rifle root

#### `SniperRifle` (`path_id 320660882`)

This root contains `SniperRifle_mesh_noHammers` as a child and is the clearest scripted no-hammers prefab root in the old bundle.

Linked `MonoBehaviour` components:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`

Hierarchy:

```text
- SniperRifle [320660882] :: GearModel, GearModelShotEffect, GearSoundHandler
  - SniperRifle_Player [1854698118] :: Animation
    - SniperRifle_mesh_noHammers [-535968408] :: SkinnedMeshRenderer
    - SniperRifle_Root [-1996767149] :: no extra comps
      - SniperRifle_Trigger [900506118] :: no extra comps
      - SniperRifle_Tubes [2034134745] :: no extra comps
        - SniperRifle_Left_Shell [1208929789] :: no extra comps
        - SniperRifle_Right_Shell [-1026785367] :: no extra comps
      - BulletNode_SniperRifle [-1108957241] :: no extra comps
    - SniperRifle_Shell [-1757503773] :: SkinnedMeshRenderer
```

### No-hammers mesh instances without weapon scripts

These are animation or LOD sub-objects, not the main scripted weapon root:

- `SniperRifle_mesh_noHammers` (`path_id -535968408`)
  - Parent: `SniperRifle_Player` (`path_id 1854698118`)
  - Components: `Transform`, `SkinnedMeshRenderer`
- `SniperRifle_mesh_noHammers` (`path_id 1581177237`)
  - Parent: `SniperRifle_Player` (`path_id -210986473`)
  - Components: `Transform`, `SkinnedMeshRenderer`
- `SniperRifle_lod_noHammers` (`path_id -632780151`)
  - Parent: `SniperRifle_Unit` (`path_id -1136878822`)
  - Components: `Transform`, `SkinnedMeshRenderer`
- `SniperRifle_lod_noHammers` (`path_id 2027557079`)
  - Parent: `SniperRifle_Unit` (`path_id 735027477`)
  - Components: `Transform`, `SkinnedMeshRenderer`

### Old `SniperRifleOneBarrel` roots also found in `Weapons`

These are separate scripted prefabs in the old bundle and are not the same as the explicit no-hammers `SniperRifle` mesh path above, but they are included here for completeness.

#### `SniperRifleOneBarrel` (`path_id -1806056951`)

Linked `MonoBehaviour` components:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`

Hierarchy:

```text
- SniperRifleOneBarrel [-1806056951] :: GearModel, GearModelShotEffect, GearSoundHandler
  - SniperRifle [-118094187] :: no extra comps
    - SniperRifle [-830762123] :: Animation
      - SniperRifle_Root [-135022933] :: no extra comps
        - SniperRifle_Clip [-610710112] :: no extra comps
        - SniperRifle_Handle [1860808643] :: no extra comps
          - SniperRifle_Bullet [-569067703] :: no extra comps
        - SniperRifle_Trigger 1 [2142938917] :: no extra comps
        - BulletSpot [-896594413] :: no extra comps
      - SniperRifleOneBarrel [1279398975] :: SkinnedMeshRenderer
```

#### `SniperRifleOneBarrel` (`path_id -35112580`)

Linked `MonoBehaviour` components:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`
- `GearWhizzbySoundHandler`

Hierarchy:

```text
- SniperRifleOneBarrel [-35112580] :: GearModel, GearModelShotEffect, GearSoundHandler, GearWhizzbySoundHandler
  - SniperRifle [1917809140] :: no extra comps
    - SniperRifle [1429675537] :: Animation
      - SniperRifle_Root [-1571828169] :: no extra comps
        - SniperRifle_Clip [-265611729] :: no extra comps
        - SniperRifle_Handle [1367832065] :: no extra comps
          - SniperRifle_Bullet [-536788778] :: no extra comps
        - SniperRifle_Trigger 1 [1438145561] :: no extra comps
        - BulletSpot [-1809048916] :: no extra comps
      - SniperRifleOneBarrel [1817075338] :: SkinnedMeshRenderer
```

## Summary

### Current `character_longshot`

Main scripted `SniperRifleOneBarrel` roots:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`
- `GearWhizzbySoundHandler`

### Old `Weapons` no-hammers sniper rifle

Main scripted root tied to the explicit `SniperRifle_mesh_noHammers` child:

- `GearModel`
- `GearModelShotEffect`
- `GearSoundHandler`

The explicit `noHammers` objects themselves are mesh-only and do not carry `MonoBehaviour`s.
