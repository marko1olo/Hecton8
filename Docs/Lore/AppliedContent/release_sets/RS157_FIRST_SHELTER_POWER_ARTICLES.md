# RS157 First Shelter Power Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P865_LOW_LOAD_BREAKER_RAIL
- P866_EMERGENCY_LIGHT_STRIP_DIM_TEST
- P867_CABLE_LASH_WET_KNOT
- P868_BUSBAR_SCORCH_WITNESS
- P869_PANEL_LABEL_HALF_POWER

## Purpose

RS157 makes first-shelter power readable as a narrow support lane. The player should understand low-load routing, dim emergency light, wet cable restraint, scorch evidence, and half-power labels before trusting a shelter panel.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS157 improves the first-20 route after battery slab wake-test and first shelter habitability by giving power state concrete objects: breaker rail, light strip, cable lash, busbar scorch, and panel labels.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for first-shelter power decisions.
- Power content stays low-load and local; no base-wide power claim.
