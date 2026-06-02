# HECTON-8 Voxel And SDF Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: voxel caves, SDF terrain, carving, Marching Cubes output, seams, collision bake, persistence, and visual quality.

## Prime Law

Voxel terrain must never look like blocks, random blobs, or low-poly caves. HECTON-8 voxel work exists to create carved pressure geography: caves, fractures, flooded industrial cuts, volcanic vents, collapsed tunnels, and player-made scars that remain physically readable.

## Current Source Boundary

The active first-party voxel path is Marching Cubes with float working SDF buffers, sbyte quantized extraction, edge-indexed vertex construction, baked normals, curvature/skirt channels, UV3 absolute world position, staged mesh upload, and staged physics bake.

Do not replace this path with Transvoxel, half-buffer runtime truth, or a second primary mesher because an older mandate mentions it. Migration requires owner, fallback, save compatibility, seam proof, MX350 profiler proof, and visual comparison.

## Truth Ownership

Voxel owns SDF field state, carve deltas, extraction state, seam state, mesh upload admission, collider bake state, and voxel persistence deltas. It does not own player movement, tool permission, AI cognition, world narrative, or rendering presentation beyond the mesh/material payload it publishes.

Tools, construction, AI, physics, and rendering consume voxel-owned snapshots or baked outputs. They do not edit voxel buffers directly.

## SDF Contract

Signed distance convention:

- negative = solid;
- zero = surface;
- positive = void.

Every SDF operation must guard division, clamp exponentials, preserve finite values, and keep material IDs or blend weights where surface materials change.

Allowed shape logic:

- analytic primitives for clean cuts and forms;
- smooth union where biological or eroded blending is wanted;
- crisp subtract for tool cuts and industrial tunnels;
- bounded displacement below topology inversion threshold;
- deterministic noise with named seed.

Random noise pasted onto a cave is rejected.

## Mesh Extraction

Marching Cubes output must:

- clamp edge interpolation away from exact corners;
- avoid degenerate triangles;
- use deterministic edge ownership;
- bake normals from density gradients;
- store curvature or seam/skirt support channels where required;
- generate UV/material data compatible with `3dmodel.md`;
- upload meshes only through the approved staged route;
- mark physics bake pending before interaction.

Physics interaction is blocked until collider bake is complete.

## Carving And Persistence

Runtime carving is gameplay truth only when it creates a physical decision: access, blockage, resource extraction, breach, repair, evidence, or hazard.

Carving operations must batch. Direct per-frame SDF edit and mesh rebuild is rejected. Dirty chunks must propagate to neighbors when boundary voxels change.

Persistent voxel changes are stored as deltas from deterministic seed state, not whole-world snapshots. Per-tile edit caps and corruption validation are mandatory.

## Seam And Terrain Integration

Terrain/voxel seams must be solved as geometry, normals, collision, and raycast ownership. A seam hidden only by fog is not accepted.

Required:

- overlap or skirt band;
- normal blending near terrain boundary;
- collision arbitration so the player does not hit duplicate colliders;
- unified raycast route for tools and interaction;
- LOD synchronization;
- AUP/floating-origin safety.

## Visual Quality And GlobalQualityWeight Scaling

Voxel surfaces need geology, not polygons:

- strata;
- erosion ledges;
- pressure fractures;
- mineral seams;
- wet cavities;
- sediment shelves;
- tool scars;
- scale witnesses;
- route silhouettes.

High/Ultra may increase SDF resolution, surface detail, and seam quality, but Compact must keep navigational silhouette and collision truth.

`GlobalQualityWeight` may scale SDF resolution, extraction distance, material blend richness, seam/skirt detail, tool-scar decal density, diagnostic overlay depth, and background rebuild cadence. It must not change SDF truth, carve permission, save delta identity, collision bake requirements, or terrain ownership.

## Rejection Gates

Reject:

- blocky terrain unless explicitly authored as machinery;
- smooth noise caves with no geological history;
- visible seams between terrain and voxel meshes;
- direct mesh rebuild per tool tick;
- collision enabled before bake completion;
- voxel saves without delta schema and checksum;
- migration away from current pipeline without source-compatible fallback.

## Proof Artifacts

Voxel work must provide:

- SDF convention and operation list;
- dirty chunk count and rebuild budget;
- mesh extraction validation report;
- seam/skirt proof;
- collider bake proof;
- persistence delta schema and checksum;
- Compact and High visual capture where terrain changed;
- profiler/GC/memory proof for runtime carving or rebuild changes;
- black-box fields for invalid SDF, failed bake, seam failure, and rebuild overload.

## Acceptance Sentence

Voxel work is accepted only when it is deterministic, seam-safe, persistent, collision-baked, visually geological, and bounded enough to run without starving player-critical systems.
