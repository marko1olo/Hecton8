# STEER_1905_UNDERWATER_VISUALS_UNASSIGNED

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04 19:05 +04.
Live evidence capture:
- `Docs/Orchestration/Captures/desktop_after_batch24_send_check_code.png`

Live Unity Game View is still rejected:
- ocean is dark/flat green-teal with no believable foam;
- shoreline/terrain is still weak and blackened;
- Aegir reads dirty green/black and under-occluded, not premium;
- no visible Subnautica-floor photic water proof.

Console evidence in the live capture:
- `[HectonUnderwaterVisuals] biomePalette not assigned.`
- `[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.`
- `[HectonUnderwaterVisuals] skyMaterial not assigned.`

This is a proof blocker. Do not continue visual claims while `HectonUnderwaterVisuals` has unassigned required presentation dependencies.

Required handling:
1. Stop treating this as merely a screenshot/color problem.
2. Fix serialized scene ownership for `HectonUnderwaterVisuals` outside hot Play registration:
   - assign the intended biome palette;
   - assign the intended underwater ocean material;
   - assign the intended sky material;
   - ensure it registers before/with the correct `GlobalRegistry` phase, not after ready-lock.
3. After assignment, produce clean log proof with no `GlobalRegistry ready-lock rejected registration: HectonUnderwaterVisuals` and no `* not assigned` warnings from `HectonUnderwaterVisuals`.
4. Only then run the Batch24 slab/caustic isolation route:
   - `H8_DEPTH_LOW_SHELF_1428`
   - `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`
   - `H8_DEPTH_CEILING_OCCLUSION_1428`
   - `NOIR_UPPER_PRESSURE_LID`
   - `H8_FloorCausticSoft_1443`

Acceptance remains blocked until the full proof packet exists with clean log tail newer than screenshots.
