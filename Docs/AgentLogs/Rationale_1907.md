# Rationale 1907

Evidence class: STATIC_DOC / STATIC_SOURCE

## Decisions

1. No Unity or asset edit path was used.
   - Reason: task is package prep only and explicitly forbids Unity, imports, scenes, materials, shaders, terrain layers, prefabs, scripts, and `Assets/**` writes.

2. Package uses mask/source/flipbook routes instead of simulation routes.
   - Reason: `water.md`, rendering authority, and cinematic-cheat mandate require visual fakes first. Shoreline foam, wetness, grime, and caustics are presentation effects unless gameplay truth requires physics.

3. `MAT_H8SurfaceShoreFoam_1428` is a static candidate, not accepted visual proof.
   - Reason: material has `foam.png` assigned, but `H8_SURFACE_SHORE_FOAM_1428` is inactive by static scene YAML.

4. `MAT_H8_SurfaceFoamRibbons_1428` is blocked as final.
   - Reason: `_BaseMap` and `_MainTex` are empty in current material YAML. Flat color foam fails the visual floor.

5. `MAT_H8TerrainLit_BasaltSediment_1428` debt was updated from older Batch18 wording.
   - Reason: current static scan shows `_Splat0-3` and `_Normal0-3` refs now exist, but `_Control` and `_Mask0-3` remain empty. The correct status is partial static source with control/mask debt, not full empty-layer debt.

6. Gemini wet basalt albedo is marked `PENDING CHANNEL_QA`.
   - Reason: source albedo and tile2x2 QA exist, but normal, MRAO, wetness, import, material binding, and in-scene proof are absent.

7. Third-party Crest assets are read-only support candidates.
   - Reason: root third-party integrity rule forbids mutating/cloning complex package assets as a shortcut. Future owner may assign/configure approved assets through Unity slot and proof only.

8. Placeholder/default/bad sources are statically rejected.
   - Reason: Batch18 identified procedural placeholders, primitive-heavy references, and `bubble vent atlas - bad - redo.png` as non-final. Compact lane still needs premium material identity.

## Low / Middle / High / Ultra Consequences

Compact:

- Lower texture and mask resolution, sparse foam lanes, shared masks/atlases.
- Must preserve coast silhouette, foam breakup, wet/dry edge, basalt material identity, photic brightness, and route readability.

Middle:

- More mask resolution, stronger wet/dry gradient, sediment/salt bands, and moderate foam lace.

High:

- More precise source masks, richer wet basalt strata, stronger caustic hints and terrain material variation after proof.

Ultra:

- Visual overkill through dense foam lace, high-detail mineral/wetness masks, richer glancing water/Aegir reference, and stronger photic transition richness.
- No change to gameplay truth, save identity, terrain route, DTO layout, material ownership, or third-party boundaries.

## Residual Risk

All visual/runtime claims remain `PENDING UNITY OWNER`. Static docs and YAML cannot prove final coastline, waterline, terrain, ocean, Aegir, photic transition, performance, GC, or VRAM acceptance.
