# Rationale 2202 - Underwater Plane/Slab Offenders

Evidence class: STATIC VERIFIED. Runtime visual acceptance remains PENDING VERIFICATION.

## Decision Notes

- `H8_UnderwaterSurfaceSheet_1455`, `BLACK_WATER_PLANE`, `BASALT_SEABED`, `ABYSS_SURFACE_CEILING`, `ABYSS_BLACKWATER_CEILING_1428`, `Water_Mass_Far_1428`, and `Water_Mass_Mid_1428` exist in scene YAML, but their GameObjects are serialized inactive. They remain rollback/watch candidates, not top active causes.
- `H8_DEPTH_LOW_SHELF_1428` is active and visually matches the pale/yellow slab: huge built-in primitive slab, beige opaque material, located under the underwater route view. It is the highest-likelihood active source.
- `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` are active, renderer-enabled primitive cube strips on layer 0. Their material is dark `MAT_WorldShell_1428`, so they are less likely to create the pale sheet but likely contribute slicing/occlusion artifacts.
- `H8_DEPTH_CEILING_OCCLUSION_1428`, `NOIR_UPPER_PRESSURE_LID`, and `NOIR_*_VIGNETTE_SLAB` are active primitive slab/curtain/lid geometry on layer 0. They are legitimate staging candidates only if camera/layer masked correctly; current static evidence cannot prove masking.
- First pass must not delete assets. Unity owner should isolate renderers/layers/materials with screenshots and rollback notes.

## Scalability Consequences

- Low: preserve route silhouettes and remove only visible offender renderers from underwater camera composition; no gameplay truth changes.
- Middle: replace crude slab occlusion with authored masked water/terrain presentation if still needed.
- High: spend saved overdraw/primitive clutter budget on richer wet terrain, silt, and waterline detail.
- Ultra: add sensory density and material response only after compact view proves no pale sheet, slicing, or flat seabed.

## Proof Boundary

Static evidence can rank suspects. It cannot prove which renderer drew the pixels. Required proof is Unity-owner selective disable/layer-mask capture with exact before/after screenshots and rollback.
