# STEER_BATCH24_1474_SLAB_CAUSTIC_ISOLATION

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Evidence:
- `Docs/Screenshots/MCP/h8_1474_diag_underwater_route_from_mcp.png`
- `Docs/Reports/Batch24/2401_CURRENT_SCENE_DELTA_UNDERWATER_CUT_AUDIT.md`
- `Docs/Reports/Batch24/2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md`

1474 is still rejected. Do not solve it by adding darker fog, saturation, or more green/blue haze on top.

Batch24 static audit says the current underwater failure is probably two combined problems:

1. Visible service/slab geometry:
   - `H8_DEPTH_LOW_SHELF_1428`
   - `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`
   - `H8_DEPTH_CEILING_OCCLUSION_1428`
   - `NOIR_UPPER_PRESSURE_LID`

2. Risky active caustic/receiver material:
   - `H8_FloorCausticSoft_1443` is active, additive, alpha about `0.42`, sine-only, no depth/light gating.
   - It can read as a bright sheet/streak instead of subtle caustic lace.

Required next Unity tests:

1. Use the exact same underwater camera as `h8_1474_diag_underwater_route_from_mcp.png`.
2. Capture baseline with metadata:
   - active scene,
   - camera transform,
   - fog color/density,
   - underwater state/depth/profile/writer,
   - enabled state for the suspect renderers above,
   - log tail newer than the screenshot.
3. Disable only `H8_DEPTH_LOW_SHELF_1428` MeshRenderer. Capture before/after. Roll back unless proven.
4. Disable `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` MeshRenderers as a group. Capture before/after. Roll back unless proven.
5. Disable `H8_DEPTH_CEILING_OCCLUSION_1428`. Capture before/after.
6. Disable `NOIR_UPPER_PRESSURE_LID` separately. Capture before/after.
7. Disable `H8_FloorCausticSoft_1443`. Capture before/after.
8. Only after the hard wall/flat sheet is identified, test missing particulate routes:
   - first `H8_UnderwaterSuspendedSpecks_1446`,
   - then very conservative horizon haze if needed.
   Do not raw-enable `H8_UnderwaterHazeCurtain_1454`; it is a sheet risk.

Crest/ocean material warning:
- `Ocean.mat` has `_ClipSurface` and `_ClipUnderTerrain` changed from `1` to `0`.
- If slab/plane artifacts persist after renderer isolation, verify/rollback those clipping changes before adding more visual layers.
- `MAT_H8_SurfaceCrestOcean_1428` caustics/foam boosts are aggressive and need proof. Do not claim foam/caustics quality from values alone.

Acceptance remains blocked until a complete proof packet exists:
- surface/coast/Aegir,
- shoreline close foam/wet contact,
- underwater 0-5 m,
- underwater 20-50 m route,
- Aegir/celestial,
- low oblique regression,
- metadata/checksums,
- clean log tail newer than the final screenshot.

The target is not “less broken”. The target is Subnautica-floor or better photic water: readable depth, soft particulate, organic caustic hints, believable foam/wet shoreline contact, and no visible product-facing debug/service planes.
