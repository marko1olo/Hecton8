# Rationale 1874 - Tool Mesh Source Authoring

Evidence class: STATIC_SOURCE

## Decisions

- Implemented a single editor-only source route instead of touching current held/world prefabs. Reason: task forbids prefab/asset/scene/binary edits and 1869 says current prefab replacement must wait for accepted source assets.
- Generator writes only Mesh `.asset` files when a future Unity owner executes it. Reason: source asset authoring is this task scope; prefab replacement, collider proxies, materials, screenshots, and runtime proof are separate proof steps.
- Required support shaders are checked before writing. Reason: fail closed if the material/source route assumptions from 1869 are absent.
- All 12 tool specs are explicit and named by `Tool_*` ID. Reason: static audit needs stable source identity and no sibling-agent dependency.
- Continuous `GlobalQualityWeight` controls radial segment count, fin count, and bevel width. Reason: binary quality switches are rejected.
- Helper geometry stays deterministic and simple: beveled boxes, cylinders, nozzles, lenses/screens, fins, spools, rails, grips, and blade wedges. Reason: compile-oriented source route, not a giant generator framework.

## Scaling Consequences

- Low: fewer radial segments, fewer fins, retained bevels and core silhouette.
- Middle: denser tool detail from the same silhouettes, no gameplay truth change.
- High: more rounded cylinders, stronger bevel width, richer authored mesh density.
- Ultra: highest segment/detail count from the same spec, still Mesh-only and presentation-only.

## Pending Proof

- Unity import/compile.
- Future menu execution.
- Mesh asset validation inside Unity.
- Material assignment, prefab replacement, collider split, anchors, screenshots, player capture, profiler only if runtime/render path changes.
