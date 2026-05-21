# Naming Violations

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Scope: `Assets/_Project` and `Docs` non-ASCII path/content sweep

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`

This file is an inventory and replacement queue.
It is not a rename patch.

## Command Evidence

Path sweep:

```powershell
Get-ChildItem Assets/_Project,Docs -Recurse -Force |
  Where-Object { $_.Name -match '[^\x00-\x7F]' }
```

Content sweep:

```powershell
rg -n --pcre2 "[^\x00-\x7F]" Assets/_Project Docs `
  -g "*.cs" -g "*.md" -g "*.txt" -g "*.json" -g "*.shader" -g "*.compute" `
  -g "!Docs/_Archive/**" -g "!Docs/DEPRECATED/**" -g "!Docs/Reports/DEPRECATED/**" `
  -g "!Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/obj/**"
```

## Counts

| Finding | Count |
|---|---:|
| Cyrillic path entries under repository scan scope, excluding `Library`, `Temp`, `obj`, and `bin` | `790` |
| Cyrillic comment/content sample hits captured from first-party code/shader comments | `300` sample cap reached |
| Previous non-ASCII path entries under `Assets/_Project` and `Docs` | `638` |
| Previous non-ASCII path entries excluding archive/deprecated/obsolete folders | `575` |
| Previous non-ASCII path entries under `Assets/_Project` | `570` |
| Previous non-ASCII path entries under `Docs` | `68` |
| Previous non-ASCII content files in active scan scope | `646` |
| Current non-ASCII path entries under `Assets/_Project` and `Docs` | `638` |
| Current non-ASCII path entries excluding archive/deprecated/obsolete folders | `575` |
| Current non-ASCII path entries under `Assets/_Project` | `570` |
| Current non-ASCII path entries under `Docs` | `68` |
| Current non-ASCII content files in active scan scope | `623` |

## 2026-05-07 Cyrillic Path / Comment Sweep

Command:

```powershell
Get-ChildItem -LiteralPath . -Recurse -File -Force -ErrorAction SilentlyContinue |
  Where-Object {
    $_.FullName -match '[\p{IsCyrillic}]' -and
    $_.FullName -notmatch '\\Library\\|\\Temp\\|\\obj\\|\\bin\\'
  }

rg -n "//.*[\p{Cyrillic}]|/\*.*[\p{Cyrillic}]|\*.*[\p{Cyrillic}]" `
  Assets/_Project -g "*.cs" -g "*.shader" -g "*.hlsl" -g "*.compute"
```

Representative Cyrillic path hits:

- `Assets/kuchka melka 1 lod 1.asset`
- `Assets/pillar2 lod1.asset`
- `Assets/Scenes/pustaya stsena.unity`
- `Assets/_Project/Art/Materials/Fonts/tekst.ttf`
- `Assets/_Project/Art/Materials/Fonts/tsifry.ttf`
- `Assets/_Project/Art/Meshes/Cleaned/ENV__arka1_GEO_LOD0_cleaned.asset`
- `Assets/_Project/Art/Meshes/Cleaned/ENV__Bolder_1_geo_LOD0_cleaned.asset`
- `Assets/_Project/Art/Meshes/Cleaned/ENV__donnaya_kucha_geo_LOD0_cleaned.asset`

Representative Cyrillic comment hits:

- `Assets/_Project/Art/Shaders/SkyboxBlend.shader:2`
- `Assets/_Project/Art/Shaders/SG_GasGiant_CelestialLighting.hlsl:3`
- `Assets/_Project/Editor/VisorOpaqueTextureEnsurer.cs:2`
- `Assets/_Project/Editor/RockDataBakerWindow.cs:3`
- `Assets/_Project/Editor/ObjectSpawner.cs:5`
- `Assets/_Project/Scripts/BeaconRuntime.cs:200`
- `Assets/_Project/Scripts/BeaconNetworkSystem.cs:528`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:3`
- `Assets/_Project/Scripts/BaseModule.cs:3`

This is a log only. No asset rename was performed because Unity `.meta` GUIDs, prefab references, Addressables, and MapMagic graph links require a dependency walk before any move.

## High-Impact Path Violations

These are representative active path classes, not the full 637-entry dump.

| Current class | Example path class | Proposed English replacement |
|---|---|---|
| Russian font assets | `Assets/_Project/Art/Materials/Fonts/<ru:text>.ttf` | `Text.ttf` |
| Russian digit font assets | `Assets/_Project/Art/Materials/Fonts/<ru:digits>.ttf` | `Digits.ttf` |
| Russian cleaned mesh names | `Assets/_Project/Art/Meshes/Cleaned/ENV__<ru:arch|rock|pile>_*_cleaned.asset` | `ENV__Arch_Stonehenge_01_LOD0_cleaned.asset`, `ENV__Boulder_01_LOD0_cleaned.asset`, `ENV__RockPile_01_LOD0_cleaned.asset` |
| Russian baked model names | `Assets/_Project/Art/Models/Baked/<ru:rock names>.asset` | `Rock_Large_LOD0.asset`, `Pillar_01_LOD0.asset` |
| Russian rock source folder | `Assets/_Project/Art/Models/Rocks/Rock 4 - <ru:universal choice>/` | `Assets/_Project/Art/Models/Rocks/Rock_04_UniversalChoice/` |
| Russian texture filenames | `Assets/_Project/Art/TEXTURES/Detali/soft plume noise - <ru:note>.png` | `SoftPlumeNoise_Gray_V01.png` |
| Russian rock socket assets | `Assets/_Project/Data/RockSockets/<ru:rock name>_Sockets.asset` | `RockSocket_Arch_Stonehenge_01.asset`, `RockSocket_BoulderPile_02.asset` |
| Russian loose data docs | `Assets/_Project/Data/<ru:plan>.txt`, `Assets/_Project/Data/<ru:text>.txt` | Move translated content to `Docs/Legacy_Backlog/` or delete after source validation |
| Russian prefab folders | `Assets/_Project/Prefabs/Nature/<ru:ready rock prefabs>/` | `Assets/_Project/Prefabs/Nature/RockPrefabs_Ready/` |
| Russian prefab names | `Assets/_Project/Prefabs/Nature/<ru folder>/ENV_ <ru:rock>.prefab` | `ENV_Rock_Arch_01.prefab`, `ENV_Boulder_02.prefab`, `ENV_RockPile_03.prefab` |

## Active Content Violations

These are representative active content classes from the content sweep.

| File | Violation class | Required action |
|---|---|---|
| `Assets/_Project/BACKLOG.txt` | Russian backlog prose in active project asset tree | Translate and move to `Docs/Legacy_Backlog/` or remove after migration |
| `Docs/AI_Fauna/AI_CREATURE_ROSTER_ENTERPRISE.md` | Russian design prose in active reference docs | Translate to English or mark as legacy/reference-only |
| `Docs/Legacy_Backlog/*.md` and `*.txt` | Russian backlog/reference prose | Keep only if explicitly legacy; otherwise translate |
| `Assets/_Project/Scripts/ConstructionManager.cs` | Encoding-damaged Russian comments/mojibake in active source | Replace comments with short English comments during next touched-source pass |
| `Assets/_Project/Scripts/BuoyancyObject.cs` | Russian comments and tooltips in active source | Translate comments/tooltips to English |
| `Assets/_Project/Editor/*.cs` | Non-ASCII editor comments/labels in editor tooling | Translate editor labels or move obsolete tools to deprecated bundle |
| `Assets/_Project/Shaders/*.shader` | Non-ASCII comments in shader surface | Translate comments to ASCII English |

## Special Case: `USE IT.asset`

`Assets/MapMagic/Map_Graph/New Gen/USE IT.asset` is ASCII, but it violates semantic naming discipline.
It is also the visible legacy hand-authored terrain graph anchor referenced by current reality-delta work.

Proposed replacement names:

- `MapMagic_ProceduralShelf_Current.asset`
- `MapMagic_ProductionTerrainGraph.asset`
- `MapMagic_LegacyHandAuthoredTerrain.asset` if it is retained only for comparison

No path named with the literal Russian phrase equivalent to "USE IT ASSET" was found in the current scan.
The concrete file found is `USE IT.asset`.

## Rename Policy

Do not mass-rename assets in this pass.
Unity `.meta` GUIDs, serialized scene references, prefab references, Addressables, and MapMagic graph links can break if files are renamed without a dependency walk.

Required safe sequence:

1. Generate a rename map.
2. Resolve every `.meta` GUID and serialized reference hit.
3. Move files with Unity AssetDatabase or `git mv` plus Unity refresh.
4. Open affected scenes/prefabs/graphs and verify references.
5. Run targeted Unity import/compile/smoke checks.

## Current Blocking Facts

- Full active naming cleanup is not done.
- Current sweep produced evidence only.
- New active docs should keep ASCII names and English prose.
- This file intentionally uses ASCII placeholders for Russian path fragments so the active ledger does not add new non-ASCII content.

STATUS: PENDING VERIFICATION
