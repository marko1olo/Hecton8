# Status_3103 - WATER_CREST_FOAM_CAUSTIC_OWNER

Status: STATIC VERIFIED / PENDING UNITY READBACK

Mandates followed:
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`

Authority read:
- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `quality.md`
- `taskslocal/batch31_night_visual_recovery/3103_WATER_CREST_FOAM_CAUSTIC_OWNER.txt`
- `Docs/Reports/Batch30/3004_WATER_FOAM_CAUSTIC_ROUTE_AUDIT.md`
- `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`
- `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`

Findings:
- Active serialized surface route remains `Assets/Crest/Crest/Materials/Ocean.mat` via `Ocean_Crest.prefab` and `02_HECTON_WORLD.unity`.
- `02_HECTON_WORLD.unity` overrides Crest `_createFoamSim` to `1`; prefab source remains `_createFoamSim: 0`.
- Active underwater material reference is `Assets/Crest/Crest/Materials/Ocean-Underwater.mat` through `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- `Crest.UnderwaterRenderer` has `_volumeGeometry: {fileID: 0}` and `_copyOceanMaterialParamsEachFrame: 1`; underwater asset values can be overwritten at runtime.
- `MAT_H8_SurfaceCrestOcean_1428.mat` is not active route proof. It has overdriven foam/caustic values and replicated Crest wave-data/stale GUID slots.
- Missing Crest `_WD_*`, `_MainTex`, and `_Skybox` GUIDs repeat in canonical Crest materials. Treat as runtime/stale Crest slots until Unity readback proves otherwise. Do not bind artist textures by text edit.
- `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` is renderer-enabled and uses `MAT_H8_ShorelineFoamFine_1469`; this proves only an authored transparent foam ribbon, not Crest foam simulation.
- `H8_FloorCausticSoft_1443` is active but renderer-disabled; no visible caustic receiver is proven.
- `Ocean_UnderwaterCurtain.mat` remains a high-risk green curtain route if enabled raw.

Process gate:
- Unity process: not running.
- Unity MCP/tools: not exposed in this session.
- `dotnet` process active. Build not launched.
- No asset, scene, prefab, material, shader, or C# mutation performed.

Next required owner action:
- Unity owner readback only first: active material GUIDs, Crest foam sim state, underwater runtime material values, object enabled states, and clean Console.
- No mutation until proof queue exists and rollback path is named.

Low / Middle / High / Ultra consequences:
- Low: canonical Crest route only, readable ocean color, no broad curtains/slabs, no detached foam acceptance, caustics limited to shallow justified light hints.
- Middle: verified Crest foam plus one narrow authored shoreline fallback only after sorting/overdraw proof.
- High: richer foam breakup, normals/specular, and light/depth-gated caustic lace after base route passes.
- Ultra: added sensory density only; no gameplay truth, save, material ownership, or route authority changes.

Final disposition:
- Report written: `Docs/Reports/Batch31/3103_WATER_CREST_FOAM_CAUSTIC_OWNER.md`
- Runtime acceptance: PENDING VERIFICATION.
