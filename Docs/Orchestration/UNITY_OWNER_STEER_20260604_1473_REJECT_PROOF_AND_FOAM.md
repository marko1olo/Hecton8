# Unity Owner Steer - 1473 Reject: Proof Route, Foam, Runtime Fault

Status: ACTIVE STEER / DO NOT CLAIM ACCEPTANCE
Source evidence:
- `Docs/Screenshots/MCP/h8_1473_surface_coast_aegir_ui_off.png`
- `Docs/Screenshots/MCP/h8_1473_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1473_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1473_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1473_regression_low_oblique.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_all_off.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_organic_only.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_vertex_only.png`
- `Docs/Screenshots/MCP/h8_1473_rt_foam_lace_only.png`
- `Docs/Orchestration/UNITY_OWNER_HANDOFF_2205_PROOF_AND_FAULTS.md`
- `Docs/Orchestration/UNITY_OWNER_HANDOFF_2202_UNDERWATER_SLABS.md`
- `Docs/Reports/Batch22/2201_FOAM_CAUSTICS_UNDERWATER_ACTIVATION_MATRIX.md`

Verdict: 1473 is rejected.

## What Improved

- Surface water color is no longer the earlier acid-green failure.
- Surface/coast/Aegir composition is closer to a bright surface direction.
- The old huge pale/yellow underwater sheet is not visible in the main 1473 underwater filenames.

## Hard Rejects

1. `h8_1473_underwater_0_5m.png` visually matches the surface/coast view.
   - It does not prove an actual shallow underwater camera/post stack.
   - It does not show shallow water volume, near seabed, caustics on floor, turbidity, marine snow, or water-column depth.

2. `h8_1473_underwater_20_50m_route.png` also visually matches the same surface/coast composition.
   - It does not prove a 20-50 m route.
   - It has no route structure, depth cue, underwater terrain silhouette, fauna/biota, silt, caustics, or medium-depth readability.

3. Foam route tests are not acceptable:
   - `rt_foam_organic_only` shows a large technical wedge/strip with blocky pixel/grid artifacts.
   - `rt_foam_lace_only` shows a broad pixelated sheet, not organic shoreline foam.
   - `rt_foam_vertex_only` is basically no visible foam improvement.
   - Foam must be waterline/contact detail, broken lace, salt/wet edge breakup, and believable scale. Current output reads as debug/proxy mask.

4. Terrain/coast remains weak:
   - shoreline still reads as a broad shell/terrain ramp with insufficient authored breakup;
   - material tiling/detail is not yet Subnautica-floor;
   - do not hide this with saturation, darkness, fog, or post.

5. Runtime proof is still not clean:
   - Worker 2205 found repeated `ArgumentNullException: Value cannot be null. Parameter name: dest`
   - Stack route: `Renderer.GetPropertyBlock(null)` -> `HectonCelestialEngine.UpdateAegirMaterial()` -> `FlushCelestialVisualSync()` -> `LateFrameTick()`.
   - The old `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465` remains a proof blocker for route stability until a later clean route log proves it cleared.

6. Static audit now has concrete slab/plane suspects:
   - `H8_DEPTH_LOW_SHELF_1428`: active renderer, built-in cube, huge horizontal scale, beige opaque material. Top pale-sheet suspect.
   - `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`: active primitive occlusion strips; likely slicing/black-band contributors.
   - `H8_DEPTH_CEILING_OCCLUSION_1428`: active huge false-ceiling slab.
   - `NOIR_UPPER_PRESSURE_LID`: active transparent horizontal lid; sorting/lid risk.
   - `NOIR_LEFT_VIGNETTE_SLAB` / `NOIR_RIGHT_VIGNETTE_SLAB`: active visible curtain/slab geometry risk.

7. Static audit now also explains missing foam/caustics/haze:
   - Most authored foam/caustic/haze/speck helpers are disabled by `m_IsActive: 0` or `MeshRenderer m_Enabled: 0`.
   - Deferred caustics renderer features are active, but the required `AbyssalDeferredCausticsRuntime` publisher was not found serialized in searched scene/prefab/data assets.
   - WaterOptics, Jacobian foam, marine snow, and custom underwater visuals exist as code but were not found as serialized active owners in the searched route.
   - Crest underwater is serialized/enabled, but Crest foam input still needs runtime proof.

## Required Next Actions

1. Stop producing "underwater" filenames from a surface-looking capture.
   - First fix the capture route.
   - If a temp camera is used, label it temp-only and do not use it for acceptance.
   - Accepted underwater proof must come through the real GameView/player camera path or explicitly proven equivalent post stack/layer/water volume path.

1a. Isolate the concrete slab/plane suspects one group at a time.
   - First test `H8_DEPTH_LOW_SHELF_1428`.
   - Then test `H8_WORLD_LOW_WATER_OCCLUSION_*`.
   - Then test `H8_DEPTH_CEILING_OCCLUSION_1428` / `NOIR_UPPER_PRESSURE_LID` / `NOIR_*_VIGNETTE_SLAB`.
   - For each group: before/after `0-5m` and `20-50m_route` proof, plus rollback note. Do not delete first; disable renderer/layer-filter or quarantine only after proof.

2. Fix or prove clear the celestial null before acceptance.
   - `HectonCelestialEngine.UpdateAegirMaterial()` must not call `Renderer.GetPropertyBlock(null)`.
   - Required proof: clean capture-session log after the fix with no repeated exception.

3. Replace the current foam-test approach.
   - Do not ship pixelated rectangular/wedge masks.
   - Use small-scale broken shoreline contact foam/salt/wet-edge masks.
   - The first acceptable proof is a close shoreline waterline shot with foam at believable scale and no grid/pixel/block edge.

3a. Activate/repair visual helpers through real owners, not debug planes.
   - Foam/caustics/haze/marine snow need active serialized owners or a documented runtime bootstrap route.
   - Do not present disabled-helper static files as visual proof.
   - If a helper is enabled, prove it with the actual GameView/camera path and a close shot.

4. Restore real underwater visual proof:
   - `0-5m`: shallow underwater, visible water volume, floor/shore transition, caustics or clear reason they are absent, haze/particles at correct density, no hard plane/surface clipping.
   - `20-50m`: route structure, depth cue, readable terrain/biota/landmarks, turbidity/silt, no surface duplicate, no flat empty seabed.

5. Re-capture one complete packet under a new ID only after the above:
   - surface coast Aegir UI off;
   - shoreline close 1 m;
   - underwater 0-5 m through accepted route;
   - underwater 20-50 m route through accepted route;
   - Aegir/celestial;
   - regression low oblique;
   - clean runtime log tail.

## Do Not Claim

- Do not claim 1473 accepted.
- Do not claim underwater proof accepted.
- Do not claim foam accepted.
- Do not claim runtime clean until the celestial null and route-load proof are clean.

Acceptance floor remains: surface and photic shallows must be bright, beautiful, detailed, and Subnautica-level or better. Fast flat water, debug-looking foam, mislabeled screenshots, or runtime exception spam are rejected.
