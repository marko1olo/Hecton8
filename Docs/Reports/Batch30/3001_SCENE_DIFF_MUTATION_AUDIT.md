# 3001 Scene Diff Mutation Audit

Date: 2026-06-04
ID: 3001_SCENE_DIFF_MUTATION_AUDIT
Evidence status: STATIC VERIFIED ONLY

## Verdict

The `02_HECTON_WORLD.unity` diff is not acceptable as cleanup.

It is a diagnostic quarantine / scene-save mutation bundle that requires Unity-owner review and explicit rollback permission before any cleanup can be kept. No Unity, Play Mode, profiler, screenshot, or build proof was run for this audit.

First-20-minutes route effect: removes a proof-hygiene blocker for the bright/readable first surface/photic exit route. It does not improve gameplay or visuals by itself.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

Authority docs read:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `terrain.md`
- `rendering.md`

## Files Inspected

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Reports/Batch29/BATCH29_SYNTHESIS_FOR_CONTROLLER.md`

`Docs/Actual Domains of Project.txt` was checked and had no content to load.

## Key Command Outputs

Evidence class: STATIC_SOURCE.

Command:

```powershell
git diff --stat -- Assets/_Project/Scenes/02_HECTON_WORLD.unity
```

Output:

```text
 Assets/_Project/Scenes/02_HECTON_WORLD.unity | 93725 ++++++++++++++++++-------
 1 file changed, 68153 insertions(+), 25572 deletions(-)
```

Command:

```powershell
git diff --numstat -- Assets/_Project/Scenes/02_HECTON_WORLD.unity
```

Output:

```text
68153	25572	Assets/_Project/Scenes/02_HECTON_WORLD.unity
```

Command:

```powershell
git status --short -- Assets/_Project/Scenes/02_HECTON_WORLD.unity Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt Docs/Reports/Batch29/BATCH29_SYNTHESIS_FOR_CONTROLLER.md
```

Output:

```text
 M Assets/_Project/Scenes/02_HECTON_WORLD.unity
?? Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs
?? Docs/Reports/Batch29/BATCH29_SYNTHESIS_FOR_CONTROLLER.md
?? Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt
?? Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt
```

## Script Mutation Path

Evidence class: STATIC_SOURCE.

`H8VisualProofCapture1912.cs` contains two safe capture paths and one unsafe mutation path:

- `CaptureSurfaceAndExit()` opens `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, renders a PNG, writes metadata, exits.
- `CaptureSurfaceAfterQuarantineAndExit()` calls the same capture path with a different output name.
- `QuarantineSurfaceRejectsAndExit()` opens the world scene, enumerates all `Renderer` objects including inactive ones, disables matching renderer components by name, writes `h8_1912_surface_quarantine.txt`, marks the scene dirty, saves the scene, then exits.

The script does not intentionally edit GameObject active state, transforms, materials, cameras, lights, prefab links, or hierarchy. Any such diff category is either pre-existing scene work, Unity SaveScene serialization churn, or another agent's concurrent edit. It cannot be accepted as the script's deliberate cleanup.

## Quarantine Object Extraction

Evidence class: STATIC_DOC for the quarantine text, STATIC_SOURCE for current/HEAD scene YAML comparison.

`h8_1912_surface_quarantine.txt` reported:

```text
action=quarantine_renderers_only
disabledCount=3
```

Reason counts:

```text
black_primitive_foreground_boulder=10
black_rejected_photic_rock_garden=1
broken_debug_foam_sheet=12
debug_depth_band_or_cyan_lane=18
flat_green_surface_haze_sheet=1
surface_noir_slab_or_curtain=9
yellow_surface_visible_caustic_sheet=1
```

Every listed object and reason:

| Object | Reason |
|---|---|
| H8_WORLD_CYAN_DEPTH_LANE_2_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_3_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_1_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_7_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_0_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_6_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_4_1428 | debug_depth_band_or_cyan_lane |
| H8_WORLD_CYAN_DEPTH_LANE_5_1428 | debug_depth_band_or_cyan_lane |
| H8_BrokenReadableFoam_1443 | broken_debug_foam_sheet |
| H8_VisibleBrokenFoam_1435 | broken_debug_foam_sheet |
| NOIR_CYAN_INSTRUMENT_TICK_07 | debug_depth_band_or_cyan_lane |
| H8_SurfaceFoamTopOnly_1458_01 | broken_debug_foam_sheet |
| NOIR_FAR_WATER_CURTAIN_B | surface_noir_slab_or_curtain |
| NOIR_CYAN_INSTRUMENT_TICK_02 | debug_depth_band_or_cyan_lane |
| H8_HeroWetBasaltBoulder_1453_04 | black_primitive_foreground_boulder |
| H8_HeroWetBasaltBoulder_1453_05 | black_primitive_foreground_boulder |
| H8_SurfaceFoamTopOnly_1458_03 | broken_debug_foam_sheet |
| H8_PHOTIC_ROCK_GARDEN_1469 | black_rejected_photic_rock_garden |
| H8_HeroWetBasaltBoulder_1453_00 | black_primitive_foreground_boulder |
| H8_SurfaceFoamTopOnly_1458_02 | broken_debug_foam_sheet |
| Water_Mass_Far_1428 | debug_depth_band_or_cyan_lane |
| H8_PHOTIC_SOFT_WATER_HAZE_1430 | flat_green_surface_haze_sheet |
| H8_FloorCausticSoft_1443 | yellow_surface_visible_caustic_sheet |
| NOIR_CYAN_INSTRUMENT_TICK_00 | debug_depth_band_or_cyan_lane |
| H8_BrokenShoreFoam_Inner_1434 | broken_debug_foam_sheet |
| NOIR_FAR_WATER_CURTAIN_A | surface_noir_slab_or_curtain |
| H8_SurfaceFoamTopOnly_1458_04 | broken_debug_foam_sheet |
| SURFACE_SKY_DOME_NOIR_1428 | surface_noir_slab_or_curtain |
| H8_HeroWetBasaltBoulder_1453_08 | black_primitive_foreground_boulder |
| NOIR_CYAN_INSTRUMENT_TICK_04 | debug_depth_band_or_cyan_lane |
| H8_SurfaceFoamTopOnly_1458_00 | broken_debug_foam_sheet |
| NOIR_UPPER_PRESSURE_LID | surface_noir_slab_or_curtain |
| H8_HeroWetBasaltBoulder_1453_03 | black_primitive_foreground_boulder |
| H8_SurfaceFoamTopOnly_1458_06 | broken_debug_foam_sheet |
| NOIR_CYAN_INSTRUMENT_TICK_01 | debug_depth_band_or_cyan_lane |
| H8_BrokenShoreFoam_Outer_1434 | broken_debug_foam_sheet |
| NOIR_MIDWATER_VEIL_A | surface_noir_slab_or_curtain |
| Water_Mass_Mid_1428 | debug_depth_band_or_cyan_lane |
| H8_SurfaceFoamTopOnly_1458_05 | broken_debug_foam_sheet |
| H8_HeroWetBasaltBoulder_1453_01 | black_primitive_foreground_boulder |
| NOIR_CYAN_INSTRUMENT_TICK_05 | debug_depth_band_or_cyan_lane |
| H8_HeroWetBasaltBoulder_1453_09 | black_primitive_foreground_boulder |
| H8_HeroWetBasaltBoulder_1453_06 | black_primitive_foreground_boulder |
| NOIR_CYAN_INSTRUMENT_TICK_06 | debug_depth_band_or_cyan_lane |
| NOIR_RIGHT_VIGNETTE_SLAB | surface_noir_slab_or_curtain |
| NOIR_MIDWATER_VEIL_B | surface_noir_slab_or_curtain |
| H8_HeroWetBasaltBoulder_1453_02 | black_primitive_foreground_boulder |
| H8_HeroWetBasaltBoulder_1453_07 | black_primitive_foreground_boulder |
| NOIR_LEFT_VIGNETTE_SLAB | surface_noir_slab_or_curtain |
| SURFACE_SKY_NOIR_BACKDROP_1428 | surface_noir_slab_or_curtain |
| NOIR_CYAN_INSTRUMENT_TICK_03 | debug_depth_band_or_cyan_lane |
| H8_BrokenShoreFoam_1439 | broken_debug_foam_sheet |

## Scene State Comparison

Evidence class: STATIC_SOURCE.

Current scene vs `HEAD` for quarantine names:

```text
HeadPresent_NO=25
HeadPresent_YES=27

HEAD_PRESENT_DELTAS
1, 0, 0, 0=8
1, 1, 1, 0=11
1, 1, 0, 0=6
0, 0, 0, 0=2
```

Tuple order is `HeadActive, HeadRenderer, CurrentActive, CurrentRenderer`.

Objects absent from `HEAD` but present in the current scene diff:

```text
H8_BrokenReadableFoam_1443, broken_debug_foam_sheet, CurrentActive=0, CurrentRenderer=0
H8_BrokenShoreFoam_1439, broken_debug_foam_sheet, CurrentActive=1, CurrentRenderer=0
H8_BrokenShoreFoam_Inner_1434, broken_debug_foam_sheet, CurrentActive=1, CurrentRenderer=0
H8_BrokenShoreFoam_Outer_1434, broken_debug_foam_sheet, CurrentActive=1, CurrentRenderer=0
H8_FloorCausticSoft_1443, yellow_surface_visible_caustic_sheet, CurrentActive=1, CurrentRenderer=0
H8_HeroWetBasaltBoulder_1453_00..09, black_primitive_foreground_boulder, CurrentActive=0, CurrentRenderer=0
H8_PHOTIC_ROCK_GARDEN_1469, black_rejected_photic_rock_garden, CurrentActive=1, CurrentRenderer=0
H8_PHOTIC_SOFT_WATER_HAZE_1430, flat_green_surface_haze_sheet, CurrentActive=1, CurrentRenderer=0
H8_SurfaceFoamTopOnly_1458_00..06, broken_debug_foam_sheet, CurrentActive=0, CurrentRenderer=0
H8_VisibleBrokenFoam_1435, broken_debug_foam_sheet, CurrentActive=0, CurrentRenderer=0
```

Head-present objects with state changes:

```text
H8_WORLD_CYAN_DEPTH_LANE_0..7: HeadActive=1 HeadRenderer=0 -> CurrentActive=0 CurrentRenderer=0
NOIR_CYAN_INSTRUMENT_TICK_00..07: HeadActive=1 HeadRenderer=1 -> CurrentActive=1 CurrentRenderer=0
NOIR_FAR_WATER_CURTAIN_A/B: HeadActive=1 HeadRenderer=1 -> CurrentActive=0 CurrentRenderer=0
NOIR_MIDWATER_VEIL_A/B: HeadActive=1 HeadRenderer=1 -> CurrentActive=0 CurrentRenderer=0
NOIR_LEFT_VIGNETTE_SLAB: HeadActive=1 HeadRenderer=1 -> CurrentActive=1 CurrentRenderer=0
NOIR_RIGHT_VIGNETTE_SLAB: HeadActive=1 HeadRenderer=1 -> CurrentActive=1 CurrentRenderer=0
NOIR_UPPER_PRESSURE_LID: HeadActive=1 HeadRenderer=1 -> CurrentActive=1 CurrentRenderer=0
Water_Mass_Far_1428: HeadActive=1 HeadRenderer=1 -> CurrentActive=0 CurrentRenderer=0
Water_Mass_Mid_1428: HeadActive=1 HeadRenderer=1 -> CurrentActive=0 CurrentRenderer=0
SURFACE_SKY_DOME_NOIR_1428 and SURFACE_SKY_NOIR_BACKDROP_1428: HeadActive=0 HeadRenderer=0 -> CurrentActive=0 CurrentRenderer=0
```

`disabledCount=3` from the quarantine file means the 1912 quarantine run itself only proves three renderers were changed during that execution. The broader HEAD-vs-current deltas cannot be attributed exclusively to `H8VisualProofCapture1912.cs`.

Most likely direct 1912 renderer disables:

- `H8_PHOTIC_ROCK_GARDEN_1469`
- `H8_PHOTIC_SOFT_WATER_HAZE_1430`
- `H8_FloorCausticSoft_1443`

Reason: they are active hierarchy objects in `h8_1912_surface_quarantine.txt`, are shown with `rendererEnabledNow=False`, and `disabledCount=3`.

## High-Risk Diff Categories

Evidence class: STATIC_SOURCE.

Scene object ordering churn: present. The diff contains broad object block movement, name/fileID adjacency changes, parent reference churn, and prefab reference churn. This is not reviewable as a clean three-renderer patch.

Renderer disable: present. Current scene has quarantine-listed renderers disabled. Some are likely direct 1912 changes; many are only HEAD-vs-current changes and may predate the quarantine run.

GameObject active state: present. Several head-present objects changed active state relative to HEAD, including cyan depth lanes, water mass objects, and noir water curtains. `H8VisualProofCapture1912.cs` does not intentionally change `m_IsActive`, so these are not accepted as script-owned cleanup.

Material assignment: no deliberate material assignment found in the script. The diff contains many `m_Materials` and `m_Material` lines because of block churn and object additions/moves. It must be reviewed by the Unity owner before acceptance.

Camera/lighting: present. Static extraction found:

```text
Main Camera:
HEAD    near=0.08 far=16000 position=(-18, 17.1, 126)
CURRENT near=0.05 far=120000 position=(-18, 18.65, 126)

H8_SURFACE_SUN_KEY_1428:
HEAD    color=(1, 0.955, 0.84, 1) intensity=0.94
CURRENT color=(1, 0.96, 0.82, 1) intensity=1.34
```

The 1912 script does not edit those values. They are high-risk concurrent scene mutations or previous scene work.

Prefab/fileID reference churn: present. The diff includes thousands of `m_CorrespondingSourceObject`, `m_PrefabInstance`, `m_PrefabAsset`, and `m_SourcePrefab` additions/moves. This is high-risk YAML churn and cannot be hand-accepted from static review alone.

Transform/light/camera diff volume: a static grep counted `3021` transform/camera/light/color/intensity diff lines. This is not a quarantine-only diff.

## Diagnostic Objects vs Cleanup Candidates

Evidence class: STATIC_SOURCE plus STATIC_DOC.

Likely diagnostic or rejected proof objects:

- `H8_WORLD_CYAN_DEPTH_LANE_*`
- `NOIR_CYAN_INSTRUMENT_TICK_*`
- `WaterColumnBand_*`
- `Water_Mass_Far_1428`
- `Water_Mass_Mid_1428`
- `NOIR_*` slabs/curtains/veils/lid
- `SURFACE_SKY_*_NOIR_*`
- `H8_BrokenReadableFoam_1443`
- `H8_BrokenShoreFoam_*`
- `H8_VisibleBrokenFoam_1435`
- `H8_SurfaceFoamTopOnly_1458_*`

These names and quarantine reasons read as diagnostic/proof-recovery clutter. They are still scene objects until the Unity owner confirms deletion/disable safety, `.meta` handling where applicable, replacements, and visual proof.

Likely production cleanup candidates, not accepted yet:

- `H8_PHOTIC_ROCK_GARDEN_1469`
- `H8_PHOTIC_SOFT_WATER_HAZE_1430`
- `H8_FloorCausticSoft_1443`
- `H8_HeroWetBasaltBoulder_1453_00..09`

These are named as rejected visual content, but cleanup cannot mean leaving the first surface/shallow route emptier, flatter, or less readable. Owner-correct cleanup must replace or remove them through a reviewed scene pass with fresh capture proof.

## Regression Model

Evidence class: STATIC_SOURCE / PENDING VERIFICATION for runtime impact.

- CPU: no runtime CPU claim. Scene churn may affect load/import/scene hierarchy behavior; unmeasured.
- GC: no GC claim. No Unity/Profiler/GCMonitor run.
- Memory/VRAM: no memory claim. Disabling renderers may reduce visible draw/load pressure, but the scene still contains objects/assets. PENDING VERIFICATION.
- Cadence: no runtime cadence claim. No tick/render profiling.
- Correctness: high risk. Renderer and active-state changes may hide route cues, water/foam/caustic presentation, lighting/camera proof, or prefab-linked content.
- Visual floor: high risk. Raw disabling may remove ugly artifacts, but it does not prove a Subnautica-level surface/photic replacement exists.

GlobalQualityWeight consequences:

- Low: cannot accept renderer removals unless surface/water/route cues remain readable and attractive.
- Middle: cannot accept loss of material identity, foam, caustic, or route landmarks without replacement.
- High: saved cost must buy richer visual detail, not emptier scene state.
- Ultra: no new truth; only higher sensory detail after cleanup is proven.

## Rollback / Review Plan

Do not execute destructive commands without owner permission.

1. Unity owner snapshots the current dirty scene state externally for review.
2. Unity owner compares `HEAD`, current scene, and the quarantine object list inside Unity.
3. Unity owner decides per object: keep disabled, restore renderer, delete with `.meta` handling if asset deletion is involved, or replace with owner-correct content.
4. For a full rollback candidate only after permission: use a non-destructive patch/stash branch or explicit `git restore -- Assets/_Project/Scenes/02_HECTON_WORLD.unity` plan. Do not run it from this audit.
5. Re-run Unity scene load and capture a manifest-bound proof packet under `Docs/Screenshots/HectonProofPackets/...`.
6. Acceptance requires fresh screenshot/capture plus scene/prefab readback. Static YAML review is insufficient.

## Residual Risks

- Current scene contains a 93,725-line diff that mixes likely prior scene work, Unity serialization churn, and diagnostic quarantine state.
- `H8VisualProofCapture1912.cs` is untracked under `Assets/_Project/Scripts/Editor`, which can trigger Unity import and should not be the pattern for future proof capture.
- The quarantine log proves only three renderers were disabled during that run, not that the entire diff is intended cleanup.
- Camera and surface sun values differ from `HEAD`; not caused by the 1912 script.
- No Unity runtime proof exists in this audit.
- No visual acceptance exists from the 1912 proof event. Batch29 already rejects the 1912 surface capture.

## Final Classification

Claim: `H8VisualProofCapture1912.cs` likely saved renderer disable mutations into `02_HECTON_WORLD.unity`.
Evidence class: STATIC_SOURCE + STATIC_DOC.
Residual risk: exact pre-run scene state is unknown; only `disabledCount=3` is directly tied to the quarantine execution.

Claim: the scene diff is not cleanup-ready.
Evidence class: STATIC_SOURCE + STATIC_DOC.
Residual risk: Unity owner may intentionally keep parts of the scene work, but static audit cannot certify them.

Claim: the safe next action is owner review plus rollback/restore decision, not blind acceptance.
Evidence class: STATIC_DOC + STATIC_SOURCE.
Residual risk: none removed by this audit; no destructive action was taken.
