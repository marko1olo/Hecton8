# Cinematic Cheats Ledger

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

This is the project-wide standard for replacing expensive physical simulation with deterministic presentation.

## Rule

Use a visual fake unless gameplay correctness requires physical truth.

Any physical simulation path above `0.1 ms` per frame is suspicious until a profiler artifact proves the cost and the gameplay need.

## Canonical Cheats

| Domain | Canonical fake | Rejected heavy path |
|---|---|---|
| ocean current | scalar/vector flow fields, 1D/2D textures, phase offsets, shader displacement | per-object fluid particles or broad CPU Navier-Stokes |
| waves | Gerstner/profile sums with quality-weighted layer count | simulated surface mesh physics |
| caustics | scrolling dual-layer projected texture or deferred decal pass | photon simulation, per-light caustic mesh generation |
| vegetation and roots | sine/LUT phase sway with AUP seed | per-plant joints, per-frame trig per instance |
| fog and light shafts | depth fog, LUT haze, screen-space shafts, low-step raymarch by quality weight | full volumetric truth everywhere |
| fluid incursion | compartment scalar state, waterline shader, leak audio/haptic fakes | full interior fluid mass simulation unless vessel gameplay requires it |
| material aging | shader masks and bounded DTO buffers | spawned decal hierarchies, runtime crack meshes, per-renderer material clones |
| construction preview | indirect hologram DTOs and shader outlines | preview prefab hierarchies, trigger colliders, per-renderer mutation |
| sonar/cartography | packed bitmasks and hologram raymarch | persistent point-cloud GameObjects or CPU mesh rebuilds |
| storm propagation | scalar flow/audio/biolum/fog fields | high-resolution weather physics |
| destruction | pre-baked mesh state swap plus shader tear/waterline | runtime CPU Voronoi fracture |
| suit/hull crush | pressure scalar drives shader buckling/HUD cracks and acoustic groans | trigger-zone damage, OverlapBox broadphase, CPU mesh deformation |

## Continuous Quality

All cheats that expose fidelity consume `GlobalQualityWeight` as a continuous float. They may scale:

- sample count
- layer count
- cadence
- density
- shader branch weight
- optional telemetry cadence

They must not change gameplay truth, save identity, DTO layout, or authority route.

## Evidence Boundary

Entries here are architectural standards. Runtime readiness requires profiler, GCMonitor, Frame Debugger, and visual capture artifacts.
