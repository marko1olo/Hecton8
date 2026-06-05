# Procedural World Vertical Architecture

Date: 2026-05-26
Status: ACTIVE ARCHITECTURE CONTRACT
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: stable contract for vertical world composition, flooded-terrestrial geography, streaming ownership, and approximation-first procedural presentation. This is not a generation progress report.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`.

## Authority

- `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md` owns the flooded-terrestrial geography model.
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` owns cross-domain route rules.
- Source under `Assets/_Project` owns actual generation, streaming, save, and runtime behavior.
- Reports under `Docs/Reports` are evidence snapshots only.

## Contract

- Vertical world state is data-owned, not scene-search owned.
- Streaming systems publish from owner phases; consumers read immutable snapshots or cached interfaces.
- Terrain, flora, scatter, lighting, audio, and water presentation consume world facts; they do not invent gameplay truth.
- Depth, pressure, visibility, and biome transitions must use deterministic scalar bands/data curves where possible; expensive simulation needs profiler proof.
- Far or weak-device presentation uses cheap masks, impostors, batched meshes, and cadence reduction.
- Strong-device presentation adds density, blend layers, particles, decals, and lighting richness without changing authority routes.

## Rejection Rules

- Reject per-frame scene discovery for world facts.
- Reject private persistent native containers in `MonoBehaviour` managers unless an owner contract proves lifetime and disposal.
- Reject same-frame job schedule/readback loops without profiler proof.
- Reject generated-world claims without source path plus artifact path.
