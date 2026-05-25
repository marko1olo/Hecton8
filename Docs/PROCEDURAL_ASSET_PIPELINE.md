# Procedural Asset Pipeline

Date: 2026-05-21
Status: ACTIVE STATIC CONTRACT / RUNTIME PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE orientation only.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Purpose

This file is the production contract for generated or semi-procedural assets: rocks, coral, kelp, wreck fragments, interior clutter, structural dressing, cave props, and biome scatter prefabs.

It is not proof that any specific prefab exists, is referenced by a production scene, is included in Addressables or StreamingAssets, or is wired to runtime scatter. Proof requires a fresh artifact with command, timestamp, environment, and output.

Current tool assumptions are static orientation only: Unity 6000.x, URP Forward+, MapMagic, GPU Instancer Pro or equivalent indirect rendering path, Mantis LOD or equivalent LOD reduction, Mesh Baker or equivalent offline batching. Tool presence and package versions must be checked from the current project before use.

## Required Deliverable

Each completed procedural asset family must produce all of the following:

- production prefab or prefab variant under the active project asset tree
- material set using project shader policy and texture-channel packing policy
- LOD chain with measured triangle counts
- collider or SDF proxy where interaction is required
- scatter or placement profile only when the asset is meant to spawn procedurally
- ownership note linking the authoring source, runtime owner, and proof artifact

Inspection-only validators, markdown packets, or editor windows do not count as asset delivery unless they also generate or wire production assets.

## Asset Classes

Use one of these classes for budgets and review:

| Class | Examples | MX350 LOD0 | High-end LOD0 |
| --- | --- | ---: | ---: |
| ORGANIC | kelp, coral, tubeworms, large flora | <= 3,000 tris | <= 8,000 tris |
| GEOLOGICAL | rocks, vents, cave lips, resource outcrops | <= 8,000 tris | <= 20,000 tris |
| STRUCTURAL | wreck plates, habitat shells, machinery | <= 15,000 tris | <= 40,000 tris |
| INTERIOR_DECOR | small props, panels, clutter, signage | <= 2,000 tris | <= 6,000 tris |

Scene-level MX350 orientation: <= 2.5M visible triangles, <= 800 SetPass calls, <= 1.6GB VRAM, <= 100 unique materials. These are planning thresholds, not profiler proof.

## Visual-Fake-First Rules

- Detail comes from normal maps, channel masks, triplanar projection, vertex displacement, decals, and LOD transitions before extra topology.
- Runtime physics is rejected for visual-only motion. Use vertex shader sine, triangle wave, curl-noise offset, VAT, flow masks, wetness masks, and cheap impostors first.
- One material per repeated asset family is preferred. Variation should come from GPU instance data: color, scale, rotation, phase, damage, wetness, toxicity, and age scalars.
- CPU animation is forbidden for mass scatter. Wind, current sway, bioluminescence pulse, rust creep, silt, and caustic shimmer belong in shaders or indirect instance data.
- Do not add a new simulation owner for a visual asset unless an active architecture route card names its owner, route, buffer, proof requirement, and disposal path.

## Geometry Rules

- Prefer quad-dominant topology, uniform density, and no triangles smaller than 0.05 square meters for large environmental pieces.
- Geological and organic noise should be authored or baked where possible. Runtime deformation is for gameplay truth only.
- Seam vertices on tiling assets must be normalized to the shared edge rule used by the owning terrain or scatter system.
- Reject non-manifold geometry, T-vertices, chaotic triangulation, hidden duplicate surfaces, and UV-only world-scale material assumptions.
- Large-world placement must use local-space deltas after subtracting the sector or camera AUP; never bake an asset workflow that requires absolute float world coordinates at 100km scale.

## Surface Rules

Materials must follow project channel-packing and shader doctrine. The minimum review set is:

- albedo or base color
- normal map
- roughness or smoothness channel
- ambient occlusion or cavity channel
- mask channels for wetness, silt, biofilm, corrosion, emissive, or damage where relevant

Use triplanar or world/object projection for rocks, caves, cliff faces, and modular structural pieces that would show UV stretching. Use authored UVs only where the visual language needs exact decals, labels, panels, or hand-placed detail.

## LOD And Scatter

- LOD0 is the inspection mesh, not the default runtime mesh for all distances.
- LOD1 and LOD2 must preserve silhouette before preserving small surface detail.
- Far organic scatter should collapse to cards, impostors, or indirect instance shells.
- `GlobalQualityWeight` may scale density, update cadence, material tap count, shader noise taps, and optional telemetry. It must not change gameplay truth ownership, save identity, DTO layout, or authority route.
- Runtime scatter must not instantiate thousands of GameObjects. Use pooled instances, BRG, GPU Instancer, or project-approved indirect rendering lanes.

## Collider And Interaction Rules

- Visual-only assets use no collider or a coarse trigger proxy.
- Harvest, construction, scanner, damage, or traversal assets require an explicit owner route and a simple collider/SDF proxy that matches gameplay needs, not mesh detail.
- MeshCollider is rejected for mass scatter and large procedural fields unless a route card and profiler artifact prove it is acceptable.
- Physics materials, buoyancy, damage, and scan metadata are separate facts owned by their systems; do not duplicate them inside visual-only authoring files.

## Binary And Streaming Rules

- Generated binary payloads must follow `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Any `.h8bin` or binary payload must declare byte order, struct size, padding, and validator proof.
- `Pack=1` layouts are forbidden for runtime DTOs.
- Asset streaming readiness requires actual payload presence plus boot/import evidence. A design contract or editor script is not streaming proof.

## Evidence Required Before GREEN

Minimum static evidence:

- generated asset paths
- source authoring inputs
- material and texture-channel paths
- LOD triangle counts
- collider/SDF proxy path where applicable
- route-card link where runtime ownership exists

Runtime evidence remains separate and must include the actual command/tool, timestamp, environment, and output. Accepted runtime classes are Unity import, Console, Play Mode route, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, shader import, network send, platform run, or visual capture when relevant.

## Current Known Boundary

This document replaces the previous damaged `PROCEDURAL_ASSET_PIPELINE.md` text whose Russian headings and bullets were unrecoverable mojibake/question-mark placeholders. The usable intent has been preserved as an English static contract: deliver production assets, prefer visual fakes, keep ownership routed through current architecture, and do not claim runtime readiness from static docs.
