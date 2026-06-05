# Material / Texture Criticals - 2026-06-05

Status: STATIC INTEGRATION / NO UNITY ACCEPTANCE

Evidence class: `STATIC_SOURCE`, `SUBAGENT_STATIC_REPORT`.

Mandates followed:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Verdict

The current product-face material route is not acceptable. Too many active or candidate surface/photic/sky materials are proxy-bound, texture-null, or GUID-broken.

Do not hide this with fog, darkness, bloom, noir grade, or scatter spam. Material identity must be repaired before end-wave prefab placement can be accepted.

## Critical Blockers

1. `02_HECTON_WORLD` uses active photic coral objects with `WorldProceduralProxy` materials:
   - `H8_PHOTIC_CORAL_PLATE_FIELD_1430` -> `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat`
   - `H8_PHOTIC_BRANCH_THICKET_FIELD_1430` -> `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
   - Disposition: rejected for production route until Unity owner rebinds to final route-owned materials and captures proof.

2. `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat` has unresolved texture GUIDs in Crest/wave-data/sky slots.
   - Disposition: water/rendering owner must classify stale runtime wave-data refs versus real artist texture slots.
   - Rule: no custom runtime Crest material clones or wrappers. If Crest requires an asset material, assign the asset through Unity owner.
   - Resolution detail: see `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`; most Crest `_WD_*` refs are probable runtime/stale wave-data slots replicated in canonical Crest materials, not artist texture slots.

3. `Assets/_Project/Art/Materials/Mat_HectonSky.mat` has missing/null cloud, star, horizon, and main texture slots.
   - Disposition: celestial owner must map existing sky/cloud textures to shader roles before any binding.
   - Rejected fallback: dark sky, fog cover, or low-detail Aegir smear.

4. `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat` repeats high-cloud/atlas missing refs.
   - `_MainCloudTex` reportedly resolves to `oblaka!.png`; other cloud roles remain unresolved.

5. Surface foam and veil materials are color-only:
   - `MAT_H8_SurfaceFoamRibbons_1428.mat`
   - `MAT_H8WorldWaterMassVeil_1428.mat`
   - Disposition: locate or author mask textures; detached strips and color-only sheets are rejected.

6. Terrain and triplanar rock route is broken:
   - `Assets/_Project/Art/Materials/terrain.mat`
   - `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
   - Disposition: identify a valid geology shader and wet basalt/geology texture stack before scene use.
   - Valid static candidates include `Mat_Terrain.mat`, `TerrainMaster.shader`, `H8_PhoticTerrainLit_1453.shader`, `MAT_H8_HeroWetBasaltRock_1453.mat`, and `MAT_H8_AuthoredWetBasaltBreakup_1465.mat`.

7. Photic natural materials have missing PBR roles:
   - muted coral/kelp accents;
   - photic ridge/branch/fan/tube/warm basalt;
   - surface drop pod panel.
   - Disposition: split owner work by flora, geology, and hard-surface. Do not bind one generic texture across all.

8. Photic readability VFX masks are absent:
   - fish silhouette;
   - motes;
   - foam ring;
   - visible foam unlit.
   - Disposition: produce/locate sprite and mask assets; do not compensate with global fog/post.

## Static Nuance

`MAT_H8_PhoticCoralBranching_1428`, `MAT_H8_PhoticCoralLow_1428`, and `MAT_H8_PhoticCoralMassive_1428` may have stale `_MainTex` serialized rows while their `Hecton_CoralMaster_GPUI.shader` uses `[MainTexture] _BaseMap`.

Do not reject those three solely from `_MainTex` null. Verify `_BaseMap`, `_MaskMap`, `_NormalMap`, shader role, import settings, and Unity material readback.

## Gemini / Batch31 Texture Policy

The static intake script is not the art director.

Current Gemini/Batch31 sources are not final production imports. They may still be useful as temporary source/prototype material inputs when they improve route readability and the original full source is preserved.

Binding rule:

- Prototype material work may use `Docs/GeneratedAssets/Batch31_LocalPBR/` sources outside `Assets`.
- Do not destructively crop or overwrite useful source data to remove a small Gemini mark.
- Do not final-bind watermarked/seamed/baked-light sources into production materials without cleanup, PBR separation, import settings proof, and route screenshot proof.

## Required Unity Owner Pass

One Unity owner must perform this as a controlled material binding pass, not raw YAML:

1. read this report plus `GEMINI_TEXTURE_VISUAL_REVIEW_AND_LOCAL_PBR_20260605.md`;
2. inspect current material assets in Unity;
3. classify stale serialized refs versus real missing texture slots;
4. bind only route-owned final/prototype materials with rollback notes;
5. avoid Crest runtime material clone logic;
6. capture surface, shoreline, Aegir/sky, photic route, and close material proof in a manifest packet;
7. run Frame Debugger/render stats only after the scene/import state is clean.

## Low / Middle / High / Ultra Consequences

- Low: BC-compressed route textures, material identity preserved, no color-only sheets, no fog hiding, readable sky/water/shore.
- Middle: richer wetness/contact masks, stronger coral/geology material breakup, stable foam/mote masks.
- High: longer LOD residency, richer shader detail normals, denser route material witnesses.
- Ultra: visual overkill through richer masks, reflections, cloud layers, and near-field detail only after low-tier readability holds.

## Current Disposition

`PENDING VERIFICATION`.

No material is accepted from this report alone. Static blockers are real enough to assign owners; visual acceptance still requires Unity readback, screenshot proof, and clean logs.
