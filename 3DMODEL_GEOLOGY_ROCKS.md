# 3DMODEL_GEOLOGY_ROCKS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC / AUTHORING_STANDARD
Scope: rocks, boulders, cliffs, ore nodes, cave chunks, thermal vents, mineral shelves, basalt columns, sediment layers, and geological props.

## First-20 Route Hook

- First-20 moment: first shoreline/exposed rock, shallow traversal shelf, resource node, cave mouth, vent mark, route landmark, or terrain-detail object that proves the world has physical geology.
- Route blocker removed: prevents opening terrain and resource reads from becoming smooth blobs, colorized heightfields, noise spheres, or shader-only geology.
- Proof class: STATIC_DOC until geology process manifest, topology/normal validation, LOD/collider proof, material/triplanar proof, compact capture, and route screenshot exist.

## 1. Geology Mesh Law

Generated geology must look stratified, pressure-eroded or wave-eroded, mineral-stained, fractured, and embedded in its route: surface, photic shallows, depth, cave, vent, or abyss. Smooth noise blobs and perfect ico-spheres are rejected. The shape must contain readable geological process: sediment bands, chipped edges, sheared planes, pitted cavities, erosion shelves, mineral veins, vent chimneys, waterline marks, shoreline undercuts, or collapsed fracture faces.

Geology may use voxel/SDF, marching cubes, signed distance blends, Voronoi fracture, ridged noise, erosion filters, or authored profiles. The saved output must still pass topology, UV/material, LOD, and collider gates.

## 2. Topology Rules

Solid rocks and vents must be manifold unless the asset is a render-only shell. Marching cubes output must be repaired:

- Remove degenerate triangles.
- Weld colocated vertices under a tolerance tied to voxel step.
- Remove isolated islands below minimum volume.
- Recompute or analytically derive normals from SDF gradient.
- Preserve sharp fracture planes by splitting normals at material/angle borders.
- Cap open cave chunk borders or tag them as chunk seam borders with explicit stitching rules.

Fractures and strata must not be only albedo paint. Major cracks need mesh relief, bevel chips, or baked normal/depth support.

## 3. Geological Detail Layers

Required layers by asset type:

- Sedimentary boulder: banded strata, fractured edges, softer eroded shelves.
- Basalt/volcanic: columnar breaks, sharper chips, rough porous cavities.
- Thermal vent: chimney stacks, mineral crust, soot/heat discoloration, porous openings.
- Ore node: host rock plus vein material, exposed chipped vein cross-sections.
- Cave chunk: overhang/shelf silhouette, occluded cavities, ground contact blend.
- Surface/photic rock: wet waterline, sunlit mineral breakup, erosion shelves, shallow-water contact, foam/wetness edge, and clear silhouette.

The mesh generator must use deterministic seeds and stable profile parameters. Random noise without geological identity is rejected.

## 4. Normals, AO, And Material Masks

Geology normals may be weighted face normals for fracture planes and SDF gradient normals for smooth eroded fields. Split normals at sharp fracture edges above 45 degrees. Do not smooth a chipped plane into a soft blob.

Vertex color contract:

- R = exposed edge/chip/mineral reveal.
- G = wetness, algae, mineral stain, sulfur/oxide channel, or vent heat mask.
- B = baked ambient occlusion/cavity darkness.
- A = material blend or ore/emission mask.

AO must be baked offline using hemisphere/cavity sampling, bent normal approximation, or validated cheap approximation. Runtime does not raycast rock crevices for darkness.

## 5. UV And Triplanar Rules

Large and irregular geology should prefer triplanar or world/object-space material projection with baked masks. Unique UVs are required when the asset has hero markings, ore seams, labels, decals, or baked unique normal detail.

Triplanar does not excuse missing UVs. UV0 still stores decal/manifest coordinates or a fallback unwrap. UV1 may store lightmap/detail scale. Generated triplanar materials must expose scale, sharpness, normal strength, cavity strength, and wetness strength as material properties.

## 6. Continuous Quality Scaling

`GlobalQualityWeight` scales offline geology fidelity through SDF resolution, fracture plane count, erosion pass count, decal density, texture resolution, mask precision, and LOD transition distance. It never changes ore node identity, collision proxy route, navigation blocker identity, material channel semantics, or runtime generation law.

Compact geology must still show a believable process in silhouette. Higher tiers add richer mineral veins, sharper fracture detail, better wetness masks, and denser near-field bakes; they do not permit smooth blobs with expensive shaders.

## 7. LOD And Decimation

Geology LOD must preserve silhouette and major fracture planes.

Allowed decimation:

- Quadric Edge Collapse with boundary, UV seam, material border, and sharp edge preservation.
- Voxel cell collapse/grid simplification when it preserves SDF silhouette and does not create non-manifold output.
- Impostor shells or baked cards for distant clusters.

Forbidden:

- Uniform vertex skipping.
- Smoothing away all fracture planes.
- Removing ore/vent silhouette that communicates gameplay affordance.

Default budgets:

- Small rock: LOD0 4,000, LOD1 1,200, LOD2 250.
- Medium boulder/ore: LOD0 9,000, LOD1 3,000, LOD2 600.
- Large vent/cliff chunk: LOD0 18,000, LOD1 7,000, LOD2 1,200.

## 8. Collision

Geology collision uses:

- BoxCollider for broad static blockers where silhouette does not affect movement.
- Convex hull under 200 triangles for medium rocks/coral-like blockers.
- Compound primitive set for caves, vents, and shelves.
- SDF/nav proxy when navigation uses voxel authority.

LOD0 MeshCollider is banned. Collision proxy must be saved as `COL_*` mesh or primitive child set and cooked offline when a mesh proxy is unavoidable.

## 8A. Runtime And Hot-Path Boundary

Geology runtime truth is the serialized visual package, material/mask contract, collider or SDF/nav proxy, ore/vent identity, traversal blocker identity, and route placement owner. Hot paths must not run SDF mesh generation, marching cubes, erosion, fracture, UV/triplanar setup, AO baking, collider cooking, ore-mask derivation, or visual-triangle collision.

Runtime may stream prebuilt geology variants, select LOD/HLOD/impostors, drive approved wetness/vent/emission shader parameters, and read precomputed collision/nav proxies. `GlobalQualityWeight` may scale fidelity and residency only; it must not change ore identity, collision route, navigation blocker truth, material channel semantics, save identity, or route ownership.

## 9. Rejection Gates

Reject if:

- The rock reads as a smoothed sphere or procedural blob.
- No geological process is visible in silhouette or material masks.
- Triplanar scale is undocumented or mismatched between LODs.
- Collision proxy exceeds budget or references LOD0.
- LOD1/LOD2 destroys ore/vent gameplay readability.
- Marching cubes output has holes, degenerate triangles, or unbounded seams.

## 10. Proof Artifacts

Geology generation must output:

- asset family, seed, geological process tag, biome/depth route, and material family;
- SDF, voxel, fracture, erosion, or profile parameters used to generate the mesh;
- manifold/open-shell validation report;
- degenerate triangle, island, seam, and normal validation report;
- vertex color channel summary for chip/mineral reveal, wetness/stain/heat, AO, and blend/emission masks;
- triplanar scale, UV fallback, decal UV, and material property report;
- LOD triangle counts and decimation method with boundary, UV seam, material border, and sharp edge preservation flags;
- collider proxy type, primitive count, convex hull triangle count, or SDF/nav proxy route;
- screenshots with flat material override and final material to prove the silhouette carries geology before texture detail.

## 11. Acceptance Sentence

A generated geology asset is accepted only when its silhouette and topology reveal a believable geological process, its masks and materials support the correct surface/photic/depth/cave/abyss material truth, its LODs preserve gameplay-readable ore/vent/fracture/shore forms, and its collision proxy remains separate from decorative visual triangles.
