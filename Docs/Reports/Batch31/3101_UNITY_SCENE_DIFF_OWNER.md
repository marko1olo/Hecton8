# 3101 Unity Scene Diff Owner Review Queue

Date: 2026-06-05
ID: 3101_UNITY_SCENE_DIFF_OWNER
Status: STATIC VERIFIED / PENDING EDITOR VERIFICATION / NO VISUAL ACCEPTANCE

## Verdict

`Assets/_Project/Scenes/02_HECTON_WORLD.unity` is not cleanup-ready.

The diff remains a mixed diagnostic/quarantine/scene-churn bundle. It contains likely visual rejects, possible route-critical water/sky/lighting/camera candidates, and broad Unity serialization or concurrent-agent churn. No restore, delete, or keep decision is accepted without Unity owner readback and proof capture.

First-20-minutes route effect: this report removes a proof-hygiene blocker for the bright/readable surface and photic exit route. It does not improve gameplay or visuals by itself.

## Evidence Class

STATIC_SOURCE and STATIC_DOC only.

Unity/editor action was blocked by workstation state:

```text
CPU LoadPercentage=100
Active processes:
dotnet              15340
UnityShaderCompiler 9532
```

No Unity API readback, Play Mode, profiler, Frame Debugger, screenshot, or player capture was obtained.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Authority docs read:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `HECTON8_ORCHESTRATOR.md`
- `quality.md`
- `terrain.md`
- `rendering.md`
- `water.md`
- `world.md`

## Files Inspected

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`
- `Docs/Reports/Batch30/3001_SCENE_DIFF_MUTATION_AUDIT.md`
- `Docs/Reports/Batch31/CONTROLLER_SYNTHESIS_20260605_0118.md`

`Docs/Actual Domains of Project.txt` was checked and had no content to load.

## Static Command Outputs

Command:

```powershell
git status --short -- Assets/_Project/Scenes/02_HECTON_WORLD.unity Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt Docs/Reports/Batch30/3001_SCENE_DIFF_MUTATION_AUDIT.md Docs/Reports/Batch31/CONTROLLER_SYNTHESIS_20260605_0118.md
```

Output:

```text
 M Assets/_Project/Scenes/02_HECTON_WORLD.unity
?? Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs
?? Docs/Reports/Batch30/3001_SCENE_DIFF_MUTATION_AUDIT.md
?? Docs/Reports/Batch31/CONTROLLER_SYNTHESIS_20260605_0118.md
?? Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt
```

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
git diff --name-status -- Assets/_Project/Scenes/02_HECTON_WORLD.unity
```

Output:

```text
M	Assets/_Project/Scenes/02_HECTON_WORLD.unity
```

## Diff Category Counts

Command class: `git diff --unified=0` static pattern count.

```text
AddedName=789
DeletedName=620
AddedActive=410
DeletedActive=249
AddedEnabled=447
DeletedEnabled=316
AddedMaterial=213
DeletedMaterial=93
AddedPrefab=3954
DeletedPrefab=1223
AddedTransform=2580
DeletedTransform=1775
AddedCameraLight=74
DeletedCameraLight=62
```

This is not a three-renderer cleanup. It is scene-level churn with object identity, prefab, transform, active-state, renderer, material, camera, and light risk.

## 1912 Quarantine Link

`Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt` reports:

```text
action=quarantine_renderers_only
disabledCount=3
```

`H8VisualProofCapture1912.cs` confirms `QuarantineSurfaceRejectsAndExit()`:

- opens `02_HECTON_WORLD.unity`;
- finds renderers by object name;
- sets `renderer.enabled = false`;
- writes the quarantine text file;
- calls `EditorSceneManager.MarkSceneDirty(scene)`;
- calls `EditorSceneManager.SaveScene(scene)`.

The script does not intentionally edit GameObject active state, transforms, materials, cameras, lights, prefab links, or hierarchy.

Likely direct 1912 disables:

| Object | Current state | Static reason | Queue action |
|---|---|---|---|
| `H8_PHOTIC_ROCK_GARDEN_1469` | active=1 renderer=0 current-only | `black_rejected_photic_rock_garden` | KEEP DISABLED until replacement/Unity review |
| `H8_PHOTIC_SOFT_WATER_HAZE_1430` | active=1 renderer=0 current-only | `flat_green_surface_haze_sheet` | KEEP DISABLED until replacement/Unity review |
| `H8_FloorCausticSoft_1443` | active=1 renderer=0 current-only | `yellow_surface_visible_caustic_sheet` | KEEP DISABLED until replacement/Unity review |

## Quarantine State Summary

Tuple source: parsed current scene and `HEAD` scene YAML. Tuple fields: `HeadPresent`, `HeadA`, `HeadR`, `CurPresent`, `CurA`, `CurR`.

```text
19 HeadPresent=False; CurPresent=True; CurA=0; CurR=0
11 HeadPresent=True;  HeadA=1; HeadR=1; CurPresent=True; CurA=1; CurR=0
 8 HeadPresent=True;  HeadA=1; HeadR=0; CurPresent=True; CurA=0; CurR=0
 6 HeadPresent=True;  HeadA=1; HeadR=1; CurPresent=True; CurA=0; CurR=0
 6 HeadPresent=False; CurPresent=True; CurA=1; CurR=0
 2 HeadPresent=True;  HeadA=0; HeadR=0; CurPresent=True; CurA=0; CurR=0
```

The current scene contains many current-only disabled visual objects and many HEAD-present objects whose renderer or active state changed. These are not all attributable to 1912.

## Unity Owner Review Queue

### A. Confirmed Visual Rejects, Keep Disabled Pending Replacement

| Group | Evidence | Why rejected | Required next action |
|---|---|---|---|
| `H8_PHOTIC_ROCK_GARDEN_1469` | current-only active=1 renderer=0; material `MAT_H8_PhoticReefBasaltSand_1435`; mesh `MESH_H8_PhoticRockGarden_1469` | quarantine labels it black/rejected; controller rejects current shoreline/terrain visual floor | Do not restore blindly. Replace with authored/premium geology or review in Unity for deletion after route substitute exists. |
| `H8_PHOTIC_SOFT_WATER_HAZE_1430` | current-only active=1 renderer=0; material `MAT_H8_PhoticSoftWaterHaze_1430`; mesh `MESH_H8_PhoticSoftWaterHaze_1430` | flat green surface haze is explicitly rejected; normal surface cannot be hidden with haze | Keep disabled. Replace through proper water/rendering owner with transparent photic water/fog route. |
| `H8_FloorCausticSoft_1443` | current-only active=1 renderer=0; material `MAT_H8_FloorCausticSoft_1443`; mesh `MESH_H8_FloorCausticPatches_1438` | yellow caustic sheet is rejected; caustics need a motivated light/water reason and proof | Keep disabled. Rebuild as gated caustic material/pass or authored decal with capture proof. |
| `H8_HeroWetBasaltBoulder_1453_00..09` | current-only active=0 renderer=0; material `MAT_H8_PhoticHeroTerrain_1453`; mesh `MESH_H8_HeroWetBoulder_1453` | quarantine labels black primitive foreground boulders | Delete/replace candidate, not restore candidate. Review if any mesh/material is salvageable offline. |

### B. Broken Diagnostic Foam, Delete/Replace Candidates

| Group | Evidence | Why rejected | Required next action |
|---|---|---|---|
| `H8_BrokenReadableFoam_1443`, `H8_VisibleBrokenFoam_1435`, `H8_BrokenShoreFoam_*`, `H8_SurfaceFoamTopOnly_1458_*` | current-only or quarantine-listed; renderers disabled | controller rejects broken foam sheets and repeated turquoise/sheet behavior | Do not restore. Replace with semantic waterline/contact foam from a real PBR/foam route; prove shoreline screenshot. |

Foam function is route-critical. These specific objects are not accepted, but removing foam without replacement leaves shoreline below the visual floor.

### C. Debug / Quarantine Clutter, Keep Disabled Or Delete After Unity Review

| Group | Evidence | Why rejected | Required next action |
|---|---|---|---|
| `H8_WORLD_CYAN_DEPTH_LANE_0..7` | HEAD active=1 renderer=0 -> current active=0 renderer=0 | debug depth lane names; not a product-facing surface solution | Keep disabled. Delete only after Unity owner confirms no route/instrument dependency. |
| `NOIR_CYAN_INSTRUMENT_TICK_00..07` | HEAD active=1 renderer=1 -> current active=1 renderer=0 | debug/instrument tick naming; may be stale cue clutter | Review in Unity. If intended route cue, replace with diegetic/instrument-owned effect; otherwise delete/keep disabled. |
| `NOIR_*` curtains/veils/vignette slabs/lid | HEAD visible for several, current renderer disabled or active disabled | surface noir slabs are rejected for normal surface; darkness belongs to depth/caves/storms/interiors | Keep disabled for surface. Review whether any belongs in depth/interior route before deletion. |
| `SURFACE_SKY_DOME_NOIR_1428`, `SURFACE_SKY_NOIR_BACKDROP_1428` | HEAD and current active=0 renderer=0 | surface noir sky rejected by visual locks | Do not enable for normal surface. Delete candidate after sky owner confirms no dependency. |

### D. Route-Critical Candidates, Do Not Blindly Leave Disabled

| Group | Current static state | Route risk | Required next action |
|---|---|---|---|
| `Water_Mass_Far_1428` | HEAD active=1 renderer=1 -> current active=0 renderer=0 | water mass/readability may be required for surface/photic depth composition | Unity review: decide restore vs replace. If it looked like cyan/debug lane, replace with premium transparent water volume rather than deleting function. |
| `Water_Mass_Mid_1428` | HEAD active=1 renderer=1 -> current active=0 renderer=0 | same water readability risk | Unity review: restore/replace only with screenshot proof. |
| `H8_DEPTH_LOW_SHELF_1428` | HEAD active=1 renderer=1 -> current active=1 renderer=0 | depth shelf may be landmark/route silhouette | Unity review. Do not accept invisibility unless replacement route landmark exists. |
| `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`, `H8_DEPTH_CEILING_OCCLUSION_1428` | HEAD active=1 renderer=1 -> current active=1 renderer=0 | could affect depth structure/readability | Unity review; classify as visual fake, occlusion helper, or stale slab. |
| `H8_PhoticRouteTerrain_1464` | current-only active=1 renderer=1 | potential route terrain candidate | Review material/mesh quality and visual result. Candidate, not accepted. |
| `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` | current-only active=1 renderer=1 | shoreline foam is needed if it passes | Review in Unity; keep only if it reads as semantic waterline contact and not a sheet. |
| `H8_UnderwaterSurfaceSheet_1455`, `H8_UnderwaterHazeCurtain_1454` | current-only active=0 renderer=0 | underwater surface/haze function may be needed | Review as replacement candidates, not proof. |

### E. Sky / Aegir / Sun / Camera Review Queue

| Object | Static state | Risk | Required next action |
|---|---|---|---|
| `Main Camera` | diff includes camera churn; inherited audit found near/far/position changes | capture proof may be invalid if camera changed during diagnostic passes | Unity readback before any proof. Do not accept screenshots from unknown camera state. |
| `H8_SURFACE_SUN_KEY_1428` | HEAD/current active=1; inherited audit found intensity/color delta | surface brightness and material truth are route-critical | Unity readback and sky owner review. Do not enable mesh sun hacks. |
| `H8_AEGIR_SKY_BACKDROP_1428` | HEAD active=0 renderer=1 -> current active=1 renderer=0 | controller says do not enable beside active Aegir sphere; current active object is suspect even with renderer off | Unity review. Keep renderer disabled; check children/runtime scripts before changing active state. |
| `SURFACE_LOW_SUN_DISC_1428` | HEAD active=1 renderer=1 -> current active=0 renderer=0 | controller says do not enable | Keep disabled unless sky owner explicitly replaces primary sun route. |
| `H8_AEGIR_ATMOSPHERE_VEIL_1428` | current active=0 renderer=0 | candidate atmosphere veil, not active proof | Review only after sky material/cloud slot repair. |
| `Mat_HectonSky.mat` cloud slots | controller says `_HighCloudTex` and `_MainCloudAtlas` stale/missing; `_MainCloudTex` candidate exists | sky/Aegir proof blocked | Sky owner must perform material/runtime readback. 3101 makes no material edit. |

## High-Risk Categories Not To Accept

- Prefab/fileID churn: `AddedPrefab=3954`, `DeletedPrefab=1223`.
- Transform churn: `AddedTransform=2580`, `DeletedTransform=1775`.
- Camera/light churn: `AddedCameraLight=74`, `DeletedCameraLight=62`.
- Material churn: `AddedMaterial=213`, `DeletedMaterial=93`.
- Active-state churn: `AddedActive=410`, `DeletedActive=249`.
- Renderer churn: `AddedEnabled=447`, `DeletedEnabled=316`.

These categories exceed quarantine intent. Treat them as scene corruption or concurrent scene work until Unity owner proves otherwise.

## Review Order For Unity Owner

1. Confirm scene loads without saving.
2. Snapshot object states through Unity API without `SaveScene` or `MarkSceneDirty`.
3. Inspect `Main Camera`, `H8_SURFACE_SUN_KEY_1428`, `RenderSettings.skybox`, `RenderSettings.sun`, active Aegir object, and active water material route.
4. Review 1912 likely direct disables: `H8_PHOTIC_ROCK_GARDEN_1469`, `H8_PHOTIC_SOFT_WATER_HAZE_1430`, `H8_FloorCausticSoft_1443`.
5. Review route-critical disabled water/depth objects: `Water_Mass_Far_1428`, `Water_Mass_Mid_1428`, `H8_DEPTH_LOW_SHELF_1428`, low-water occlusion/ceiling objects.
6. Review broken foam groups and decide replacement route before deletion.
7. Review noir slabs and sky noir objects for deletion or relocation to depth/interior only.
8. Make scene mutation only through Unity API and only after explicit controller/user approval.
9. Capture valid proof packet under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.

## Regression Model

CPU: no runtime CPU claim. Scene diff may affect load/render cost, but no profiler run occurred.

GC: no runtime GC claim. No code path or Play Mode was executed.

Memory/VRAM: no memory claim. Disabled renderers do not prove lower residency because scene/assets still exist.

Cadence: no runtime cadence claim. No tick or render cadence was measured.

Correctness: high risk. Active-state and renderer changes can hide route cues, water readability, sky/Aegir state, camera proof, and surface sun state.

Visual floor: high risk. Hiding visual rejects does not prove Subnautica-level surface/photic replacement.

Hot path impact: none from this report. No runtime code changed.

Failure modes:

- Blind restore can reintroduce black boulders, flat green haze, yellow caustic sheets, broken foam, and surface noir slabs.
- Blind keep-disabled can remove water mass, route silhouette, depth structure, and shoreline contact cues.
- Blind delete can remove candidate replacement geometry or hidden dependencies.
- Static-only acceptance can launder diagnostic capture state into production scene.

Why kept/rejected:

- Kept for review: route-critical candidates whose function may be needed even if current visual execution failed.
- Rejected for restore: named quarantine visual rejects and broken diagnostic sheets.
- Rejected for deletion without approval: all scene objects until Unity owner proves object-level safety.

## Low / Middle / High / Ultra Consequences

Low: normal surface/photic route must keep readable ocean color, sky/Aegir, terrain silhouettes, shoreline contact, and return cues. Disable weak slabs, but do not leave empty/flat water.

Middle: replacement should add richer wet material response, semantic foam, underwater particles, and clear route instruments.

High: saved cost buys better waterline detail, longer LOD residency, denser coral/rocks/fauna, stronger Aegir cloud bands, and richer visor/camera feedback.

Ultra: visual overkill is allowed only after low-tier readability and route truth hold. No object queue decision may change gameplay truth through quality tier.

## Final Disposition

STATIC VERIFIED:

- The scene diff is large and mixed.
- The 1912 quarantine path is unsafe because it saves scene mutations.
- Only three renderer disables are directly likely from `disabledCount=3`.
- A Unity owner queue is required before any scene cleanup.

PENDING VERIFICATION:

- Visual quality of any current scene object.
- Runtime state of water, sky, Aegir, sun, and camera.
- Whether any disabled object can be deleted safely.
- Whether any current-only replacement object passes the surface/photic visual floor.
