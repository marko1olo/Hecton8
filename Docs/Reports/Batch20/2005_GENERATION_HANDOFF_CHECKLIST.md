# 2005 Generation Handoff Checklist

Batch ID: 2005  
Scope: later Unity/GeologyForge/RockSculptor executor. This worker produced static docs only.

## Preflight

- [ ] Confirm no other Unity import/build task is active before opening the editor.
- [ ] Read `2005_GEOLOGY_SHORELINE_ROCK_SOURCE_PACKAGE.md`.
- [ ] Stage generated profiles from `2005_ROCK_VARIANT_MATRIX.csv` into a temporary or reviewed profile CSV. Do not overwrite active profile data blindly.
- [ ] Lock the packed material channel order per actual shader before generating MRAO/wetness textures.
- [ ] Confirm final output path and manifest path from `GeologyForgeConstants`.
- [ ] Snapshot active placement/family assets before any repair.

## Source Generation

- [ ] Bake `GEO2005_SHORE_WET_DRY_OUTCROP` through GeologyForge.
- [ ] Bake `GEO2005_BEACH_COBBLE_FIELD` through GeologyForge with dense render-only policy.
- [ ] Bake `GEO2005_COAST_CLIFF_FACE_CHUNK` through RockSculptor or GeologyForge, then validate seam behavior.
- [ ] Bake `GEO2005_TIDEPOOL_RIM_WET_LEDGE` through RockSculptor and verify playable lip proxy.
- [ ] Bake `GEO2005_SHALLOW_REEF_ANCHOR_ROCK` through GeologyForge only after underwater placement guards are repaired.
- [ ] Bake `GEO2005_UNDERWATER_SHELF_ROCK` through GeologyForge.
- [ ] Bake `GEO2005_HERO_ARCH_OVERHANG` through RockSculptor with compound proxy children.
- [ ] Bake `GEO2005_MEDIUM_DEPTH_ROUTE_MARKER` through GeologyForge.
- [ ] Bake `GEO2005_DEBRIS_BLEND_ROCKS` through GeologyForge with render-only default.
- [ ] Build `GEO2005_DISTANT_HLOD_COAST_MASS` only from accepted close-source rocks.

## Material Work

- [ ] Complete wet basalt shoreline base/albedo, normal, packed MRAO/wetness, detail, waterline mask, and foam/salt residue mask.
- [ ] Complete dry basalt/mineral stain material.
- [ ] Complete beach sediment contact material.
- [ ] Complete shallow reef anchor material.
- [ ] Complete medium-depth basalt material.
- [ ] Confirm texture color spaces: albedo sRGB; normal/MRAO/masks linear.
- [ ] Confirm all generated materials are shared and SRP Batcher compatible. No per-instance material clones.
- [ ] Reject any RockSculptor output using Unity `Default-Material`.

## Placement Repair

- [ ] Split shoreline rock placement from underwater rock placement.
- [ ] Replace raw `depth == 0` assumptions with signed waterline or explicit shoreline sockets.
- [ ] Set underwater rock/ecology rules to nonzero submerged minimum depth where applicable.
- [ ] Serialize `preferSeafloor: true` for underwater rules.
- [ ] Serialize required substrate for underwater rock/coral/kelp rules.
- [ ] Disable primitive proxy fallback for product-facing procedural families.
- [ ] Fix strict envelope mapping so preferred biome, zone, and socket filters are honored.
- [ ] Prove zero coral, kelp, reef anchor, seafloor rock, underwater fauna, or safe pocket instances above submerged threshold.

## Validation

- [ ] Run `GeologyVertexLayoutValidator`.
- [ ] Run `GeologyForgeSelfAudit`.
- [ ] Run `WorldProceduralFinalPrefabQualityGate`.
- [ ] Run `Tools/GeneratedAssetProductionAudit.py`.
- [ ] Run `Tools/MaterialAudit.py`.
- [ ] Capture Unity console errors and fix them before claiming acceptance.
- [ ] Run profiler/Frame Debugger only in the Unity execution slot and record real measurements. Do not invent frame cost.

## Required Proof Shots

- [ ] Shoreline close shot with wet/dry outcrop, foam/salt residue, bright waterline.
- [ ] Shoreline wide shot with coastline, ocean surface, sky, and no darkness masking.
- [ ] Neutral material flat shot for every family.
- [ ] Final lit shot for every product-facing family.
- [ ] Shallow underwater proof: 1.5-8 m reef anchors and shelves, no dry ecology scatter.
- [ ] Medium-depth route marker proof: readable silhouette without black fog dependency.
- [ ] LOD overlay proof for close, medium, and HLOD families.
- [ ] Collider overlay proof for every interactive family.
- [ ] Low, Middle, High, Ultra quality comparison showing continuous scaling.
- [ ] HLOD coastline transition proof.

## Reject Before Handoff If

- [ ] Any product family uses `WorldProceduralProxy` primitive prefabs.
- [ ] Any production mesh references Unity built-in primitive geometry.
- [ ] Any final material is albedo-only, base/normal-only, or `Default-Material`.
- [ ] Any packed channel order is undocumented.
- [ ] Any LOD0 visual mesh is used as collision.
- [ ] Any underwater family can spawn dry because of `minDepthMeters: 0`.
- [ ] Any proof shot is dark/fogged enough to hide weak art.
- [ ] Any report claims static package completion as visual acceptance.

## Completion Record

Fill this only after Unity execution:

- Unity version:
- GeologyForge/RockSculptor bake date:
- Generated manifest path:
- Audit outputs:
- Proof screenshot folder:
- Remaining blockers:

