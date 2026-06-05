# STEER_BATCH25_SYNTHESIS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Source:
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md`

Send only when current compile/import/ILPP is quiet enough not to interrupt route.

Batch25 completed. Use this order:

1. First clean runtime route/proof blockers:
   - Current route can complete, but first timeout owner was Step 8 Runtime World Prime / scatter prime, not async scene activation.
   - Do not run StartGame during Asset Pipeline Refresh / script compile / ILPP.
   - `WeatherEvents` leak is real; owner is `WeatherEvents` static NativeQueue lanes. Current disk patch may fix it but needs fresh play/exit/reload proof.

2. Underwater/celestial ownership:
   - `HectonUnderwaterVisuals` has one static scene owner and palette/material/sky are now assigned.
   - Registration remains phase-sensitive: publish before ready-lock or inside the scene runtime publication gate.
   - `HectonCelestialEngine.sunVisualTransform` is still `{fileID: 0}`.
   - Candidate `SURFACE_LOW_SUN_DISC_1428` transform `1985271341` exists but is inactive/renderer-disabled. Either wire and prove it, or document sky-material ownership and remove stale expectation.

3. Water/material blockers:
   - `Ocean.mat`: `_ClipSurface/_ClipUnderTerrain` changed `1 -> 0`; clip keywords removed. Primary material-side plane/slab suspect.
   - `Ocean_UnderwaterCurtain.mat`: `_CAUSTICS_ON`, no `_CLIPUNDERTERRAIN_ON`, no `_TRANSPARENCY_ON`, `_CausticsStrength 10`. High sheet/curtain risk.
   - `MAT_H8_SurfaceCrestOcean_1428`: overdriven shallow/subsurface/sky/foam/caustic values can explain acid/flat green water.

4. Then Batch24 isolation:
   - `H8_DEPTH_LOW_SHELF_1428`
   - `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`
   - `H8_DEPTH_CEILING_OCCLUSION_1428`
   - `NOIR_UPPER_PRESSURE_LID`
   - `H8_FloorCausticSoft_1443`

5. Acceptance packet:
   - no diagnostics-only claim;
   - no screenshots under `Assets/Screenshots`;
   - six views + manifest + checksums + camera/depth/quality/toggles/log path;
   - clean log tail newer than final screenshot.

Current visual floor remains unchanged: Subnautica-level or better for surface/photic shallows. No dark/green haze over slabs. No raw `H8_UnderwaterHazeCurtain_1454`. No numeric foam/caustic claims without close proof.
