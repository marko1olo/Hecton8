# 2303 Foam/Caustic Activation Patch Plan

Status: STATIC HANDOFF ONLY - PENDING UNITY/PROFILER VERIFICATION
Worker: 2303
Scope: foam and caustic activation design for Unity owner. No Unity, Play Mode, builds, imports, or project setting edits were run.

## Evidence Basis

- `Docs/Reports/Batch22/2201_FOAM_CAUSTICS_UNDERWATER_ACTIVATION_MATRIX.md`
- `Docs/Screenshots/MCP/h8_1473_mainrt_crest_foam_shoreline.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_organic_only.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_vertex_only.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_lace_only.png`
- Static searches in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, `Assets/_Project/Art/Materials`, `Assets/_Project/Art/Shaders`, and `Assets/_Project/Scripts`.

## Key Findings

1. Foam exists, but the usable route is not proven live.
   - `MAT_H8_SurfaceCrestOcean_1428` has `_FOAM_ON`, `_Foam: 1`, `_FoamTexture`, `_FoamScale: 0.019`, `_FoamWhiteColor`, and `_FoamBubbleColor`.
   - `H8_CREST_FOAM_INPUT_PASS_1464` is active in scene YAML. Its MeshRenderer disabled state is expected for a Crest sim input and must not be "fixed" into a visible mesh.
   - `h8_1473_mainrt_crest_foam_shoreline.png` does not prove believable shoreline contact foam. Crest must be debugged before fallback mesh foam is activated.

2. Several authored foam helpers are dangerous, not safe.
   - `h8_1473_rt_foam_organic_only.png` shows a hard rectangular/wedge sheet with visible grid/pixel structure.
   - `h8_1473_rt_foam_lace_only.png` shows a broad pixel-grid sheet over the water.
   - `h8_1473_rt_foam_vertex_only.png` shows no meaningful improvement.
   - Therefore `H8_SurfaceFoamLace_1453`, `MAT_H8_SurfaceFoamBlob_1447`, and related grid/sheet routes are rejected as-is.

3. Caustics have one low-risk receiver route and one unproven runtime pass.
   - `H8_FloorCausticSoft_1443` is active and uses `MAT_H8_FloorCausticSoft_1443`.
   - `HectonDeferredCausticsFeature` is active in `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, and `Quest_VR_Renderer`.
   - The render feature early-outs unless `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer` succeeds. Batch22 found no serialized runtime owner GUID in searched scene/prefab/data assets.

4. Material/property mismatch risks are real.
   - `MAT_H8_VisibleFoamUnlit_1436` has `_BaseMap` and `_MainTex` as `fileID: 0`; enabling it can render invisible or flat.
   - Shader/material routes depend on receiver masks, vertex alpha, camera path, and light context. Populated shader properties do not prove visibility.
   - `MAT_H8_SurfaceFoamBlob_1447` exists under `World/Photic1428`, not `World/Photic1453` as Batch22 text implied.

## Route Class Summary

### Foam

- Organic/premium candidates:
  - `MAT_H8_SurfaceCrestOcean_1428` + Crest foam output, if Frame Debugger proves foam simulation and material sampling.
  - `MAT_H8_ShorelineFoamFine_1469` / `MESH_H8_ShorelineFoamFine_1469`, asset exists but no scene object was found by static search. Requires authored placement proof.
  - `H8_OFFSHORE_FOAM_BREAK_1428_0` with `MAT_H8_PhoticShoreFoamOrganic_1428`, only as a one-route screenshot test after Crest baseline.

- Debug-looking / primitive slab risks:
  - `SURFACE_FOAM_RIBBON_1428_2`
  - `H8_SurfaceFoamTopOnly_1458_00..06`
  - `H8_SurfaceFoamPatchCloud_1459` assets until placement/shape proof exists.

- Disabled but salvageable:
  - `H8_VisibleWaveFoam_1438`
  - `H8_OFFSHORE_FOAM_BREAK_1428_0` renderer only
  - Crest foam debug path through `H8_CREST_FOAM_INPUT_PASS_1464`

- Reject/delete candidates as-is:
  - `H8_SurfaceFoamLace_1453`
  - `MAT_H8_SurfaceFoamBlob_1447` route as represented by 1473 lace/blob screenshots
  - `H8_VisibleFoamUnlit_1436` until texture slots are fixed
  - `H8_VisibleBrokenFoam_1435`

### Caustics

- Receiver material:
  - `H8_FloorCausticSoft_1443` / `MAT_H8_FloorCausticSoft_1443`

- Projected/floor fake:
  - `H8_FloorCausticPatches_1438`, disabled but salvageable only after the active soft mesh is proven invisible/too weak.
  - `H8_PhoticTerrainCaustics_1453`, deferred until floor receiver proof passes.

- Deferred feature:
  - `HectonDeferredCausticsFeature` with `MAT_DeferredCaustics`, active in renderer assets but not accepted until runtime constant buffer proof exists.

- Runtime publisher:
  - `AbyssalDeferredCausticsRuntime`, code route exists; must be installed/owned by approved bootstrap/runtime owner before deferred caustics can render.

- Disabled legacy:
  - `CausticsProjectorManager`
  - `AnalyticalCausticsService`
  - `WATER_CAUSTIC_RIB_3` / `WATER_CAUSTIC_RIB_1428_10`

## Believable Visual Targets

### Shoreline Foam

Accepted shoreline foam must show:

- broken contact at rock/waterline, not a rectangular sheet;
- scale variation from fine lace to thicker contact accumulations;
- opacity falloff into water and against wet rock;
- foam tied to wave/contact logic, not floating parallel slabs;
- no visible pixel grid, no hard square atlas border, no debug ribbon geometry;
- surface remains bright, readable, and premium on compact lanes.

### Photic Caustics

Accepted caustics must show:

- subtle moving light lace on seabed, rocks, wet surfaces, or justified shallow receivers;
- motivated light source from surface, lamp, glass, shallow volume, or local projector;
- non-neon color and controlled intensity;
- no flat decal spam;
- no global abyssal dancing caustics without light reason;
- no hidden terrain/interactable readability loss.

## Foam Activation Sequence For Unity Owner

Baseline rule: capture exact baseline first. Store screenshots under `Docs/Screenshots/MCP` or `Docs/Reports/Batch23`, never under `Assets`.

1. Crest-only proof.
   - Keep `H8_CREST_FOAM_INPUT_PASS_1464` active and MeshRenderer disabled.
   - Use Frame Debugger/Crest debug to prove foam sim output and ocean material sampling on `MAT_H8_SurfaceCrestOcean_1428`.
   - Capture shoreline before/after.
   - Rollback: restore only any temporary Crest debug/settings; do not edit visible mesh helpers.
   - Stop if Crest foam already gives contact breakup without slabs.

2. One authored contact candidate.
   - Test one route only: `H8_OFFSHORE_FOAM_BREAK_1428_0` MeshRenderer, or `H8_VisibleWaveFoam_1438` if it is the nearest camera-relevant object.
   - Do not enable `H8_SurfaceFoamLace_1453`, `H8_VisibleFoamUnlit_1436`, or `H8_VisibleBrokenFoam_1435`.
   - Capture shoreline.
   - Rollback: restore original `m_IsActive` and `m_Enabled` values immediately if rectangular sheet/grid appears.

3. Replacement/high-tier candidate only if steps 1-2 fail.
   - Prefer authored placement of `MESH_H8_ShorelineFoamFine_1469` with `MAT_H8_ShorelineFoamFine_1469`, because assets exist but no scene object was found by static search.
   - Capture close waterline and medium route read.
   - Rollback: remove added placement or disable added GameObject.

## Caustic Activation Sequence For Unity Owner

1. Active floor receiver proof.
   - Inspect `H8_FloorCausticSoft_1443` first because it is already active.
   - Verify visibility from exact underwater/photic camera before changing material strength.
   - Capture seabed/rocks.
   - Rollback: restore original material/object state if edited.

2. Deferred publisher proof.
   - Verify `HectonDeferredCausticsFeature` enqueues only after `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer` succeeds.
   - If no owner exists, add/enable `AbyssalDeferredCausticsRuntime` only through approved bootstrap/runtime owner, not ad hoc scene clutter.
   - Capture Frame Debugger and underwater receiver.
   - Rollback: remove the added owner or restore disabled state.

3. Strength/material tuning.
   - Adjust caustic strength only after receiver and runtime path are proven.
   - Keep result subtle: moving lace, not neon stripes.
   - If `H8_FloorCausticSoft_1443` is outside view, test `H8_FloorCausticPatches_1438` as one route only.
   - Rollback: restore original material values and object states.

## Explicit Forbids

- Do not re-enable `H8_SurfaceFoamLace_1453` as-is.
- Do not re-enable the 1473 organic/lace/wedge/sheet foam route unless mesh/material is replaced first.
- Do not treat `m_Active: 1` renderer features as visible caustics.
- Do not enable all foam helpers at once.
- Do not darken surface/photic water to hide bad foam or missing caustics.
- Do not resurrect `CausticsProjectorManager` or `AnalyticalCausticsService`.
- Do not use caustics/foam as noisy visual clutter.

## Tier Behavior

Minimum:
- Crest/material contact foam or one authored mask only if it passes screenshot proof.
- Sparse floor caustic receiver only where a light reason exists.
- No compute-heavy foam/caustic route unless already proven below budget.

Low:
- Slightly more contact coverage and lower-frequency material movement.
- 256-512 caustic/water texture residency target.
- No broad slab mesh or global caustic spam.

Middle:
- Crest foam plus one authored highlight route if both pass.
- Deferred caustics publisher may run with receiver proof and GPU timing.
- Continuous `GlobalQualityWeight` drives strength/cadence, not binary enable.

High:
- Richer foam breakup, better receiver intensity, more local caustic lace near visible shallow surfaces.
- Add selective light/receiver richness only with Frame Debugger/GPU proof.

Ultra:
- Visual-overkill layering: richer shoreline breakup, finer caustic texture scale, stronger wet rock/seabed light response.
- No new gameplay truth; no hidden pass without profiler proof.

## Profiler And Proof Gate

Every activation step needs:

- before/after screenshot from the exact camera;
- rollback screenshot/state note if rejected;
- Frame Debugger proof for Crest/deferred routes;
- GPU timing for deferred caustics/Jacobian foam/runtime passes;
- 0 B/frame GC proof for runtime owner paths;
- no hot scene search, hot `GlobalRegistry` polling, CPU particle readback, or same-frame GPU readback;
- compact and high-tier captures before acceptance;
- visual rejection if foam is rectangular, pixel-grid, sheet-like, or if caustics are neon/decal spam.

Acceptance status remains `PENDING UNITY/PROFILER VERIFICATION` until fresh runtime artifacts exist.

## Direct Unity-Owner Handoff

Safe first candidates:

1. `MAT_H8_SurfaceCrestOcean_1428` + `H8_CREST_FOAM_INPUT_PASS_1464`: prove Crest foam sim/material sampling before mesh activation.
2. `H8_FloorCausticSoft_1443` + `MAT_H8_FloorCausticSoft_1443`: verify active receiver visibility before deferred route work.
3. `H8_OFFSHORE_FOAM_BREAK_1428_0` or `H8_VisibleWaveFoam_1438`: one authored foam route only, immediate rollback on sheet/grid artifacts.

Reject first candidates:

1. `H8_SurfaceFoamLace_1453` / `MAT_H8_SurfaceFoamBlob_1447` route as-is.
2. `H8_VisibleFoamUnlit_1436` because `_BaseMap` and `_MainTex` are empty.
3. `H8_VisibleBrokenFoam_1435`, `SURFACE_FOAM_RIBBON_1428_2`, and `WATER_CAUSTIC_RIB_*` routes as recovery visuals.

Proof gate:

The route is accepted only after screenshots show believable shoreline contact foam and subtle photic caustic lace, Frame Debugger proves the active pass, profiler/GPU/GC data stays inside budget, and rollback state is documented for every tested route.
