# Surface Route Static Classification - 2026-06-05

Status: `STATIC_SYNTHESIS / VISUAL REJECTED`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_SOURCE + STATIC_ASSET_YAML + DIRECT_IMAGE_REVIEW`

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, or raw YAML edit was performed by this synthesis.

## Verdict

The current surface route is rejected. The h8_1914 captures are diagnostic editor probes, not product proof.

Additional controller update after Unity-worker dialogue review: the failure is systemic, not a local hue/haze issue. Green overlays, temporary water cards, and repeated `h8_1914` screenshot probes are rejected as a direction. The next Unity pass must start from authoritative route readback and repair the saved water/terrain/shore/sky route, not from another color pass.

## Route Classification

- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.*`: `REJECTED / DIAGNOSTIC_ONLY`. Editor-only unsaved probe, not acceptance evidence.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`: `REJECTED_FOR_ACCEPTANCE_PROOF`. The runner applies temporary haze/material/OceanRenderer/MapMagic changes and has no accepted no-mutation restore proof.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` + `Assets/_Project/Prefabs/Ocean_Crest.prefab` + `Assets/Crest/Crest/Materials/Ocean.mat`: `ACTIVE_SAVED_OCEAN_ROUTE / CURRENT_VISUAL_REJECTED`.
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`: `CANDIDATE_ONLY / DO_NOT_ASSIGN_BLINDLY`. It is not active in the scene/prefab and has overdrive risk from higher caustics/foam/light values.
- `SURFACE_HORIZON_SALT_HAZE_1428` + `H8_TEMP_SurfaceHorizonHazeProbe_1428`: `REJECTED_TEMP_COVER`. Saved object is inactive; probe activates a temporary material.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` + `MAT_H8TerrainLit_BasaltSediment_1428.mat`: `ACTIVE_TERRAIN_ROUTE / CURRENT_VISUAL_REJECTED`.
- `H8_PhoticRouteTerrain_1464` + `MAT_H8_PhoticRouteTerrain_1464.mat`: `CANDIDATE_OR_DISABLED_BY_PARENT / NOT_PROOF`.
- `MAT_H8_ShorelineFoamFine_1469.mat`: `ACTIVE / INSUFFICIENT`. Thin transparent ribbon is not convincing shoreline/contact.
- `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` + `MAT_AegirGasGiant_Impostor_1428.mat`: `ACTIVE / CURRENT_QUALITY_REJECTED`.
- `Mat_HectonSky.mat`: `ACTIVE_SKYBOX / READBACK_REQUIRED / CURRENT_QUALITY_REJECTED`. Several sky texture slots are null in static evidence.
- Player/HUD/tool route: `MISSING_OR_UNPROVEN`. Landscape-only capture cannot pass first-20-minutes proof.

Linked controller matrix:

- `Docs/Orchestration/SURFACE_AUTHORITATIVE_ROUTE_RECOVERY_MATRIX_20260605.md`

## Current Visual Failure

Direct image review of the latest h8_1914 PNG shows:

- rectangular slab water;
- visible lower-right rectangular terrain/material patch;
- black detached island underside;
- green haze/acid terrain;
- weak pasted Aegir;
- thin foam/contact;
- no player, HUD, tool, or route gameplay proof.

Latest Unity log risk attached to this route:

- `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeF_20260606_003256.log` contains `HydraulicErosionDeltaApplyJob` NativeArray safety exceptions from MapMagic/Hecton hydraulic erosion and TempJob leak warnings. This is not a visual acceptance blocker only; it is also a terrain-generation/job-lifetime blocker for future proof captures.

## Required No-Mutation Readback

- dirty state before/after;
- active scene path;
- active player root;
- active camera;
- active HUD root and render mode;
- equipped tool renderer/material;
- input route;
- `H8_WORLD_CREST_OCEAN_RUNTIME_1428` active state, layer, transform/sea level, OceanRenderer material asset path/GUID, serialized material, water body culling, extents, min/max scale, LOD count/resolution/downsample, foam/depth/shadow flags, underwater material, foam/normals/caustics texture slots, and any `HideAndDontSave` or `H8_TEMP_*` materials;
- terrain material template, splat/control/mask/normal slots, size, pixel error, basemap distance, draw instanced, MapMagic graph/generation state, shoreline/island renderers, wet basalt material slots, foam bounds/material/renderQueue/ZWrite;
- `RenderSettings.skybox`, `HectonCelestialEngine` sky material, Aegir object active/material/mesh/component state, Aegir `_MainTex/_DetailTex/_StormTex` and scalar values, `Mat_HectonSky` cloud/star/horizon slots, cloud deck renderers/materials.

## Low / Middle / High / Ultra

- Low: same route must remain readable, cyan/blue, non-flat, with visible shore/contact/Aegir/HUD/tool.
- Middle: add wet shore breakup, foam/contact masks, route density.
- High: spend budget on richer normals, caustics, clouds, terrain material detail.
- Ultra: capture-grade polish only after the same no-mutation route passes; no scenic probe substitution.

Final status: `REJECTED / PENDING UNITY READBACK`.
