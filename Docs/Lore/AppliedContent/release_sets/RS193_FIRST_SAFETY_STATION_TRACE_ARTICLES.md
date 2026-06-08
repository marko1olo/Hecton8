# RS193 First Safety Station Trace Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1240_FIRE_BLANKET_FOLD_SHADOW
- P1241_EXTINGUISHER_BRACKET_DUST_CRESCENT
- P1242_ALARM_PULL_SALT_CRUST
- P1243_MUSTER_TAG_MISSING_SCREW
- P1244_EXIT_ARROW_PAINT_GHOST

## Purpose

RS193 gives first-shelter safety stations physical readiness-history traces: fold shadows from fire blanket covers, dust crescents at extinguisher brackets, salt crust on alarm pulls, missing screws on muster tags, and paint ghosts from old exit arrows.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS193 improves the first-20 route by making early safety stations communicate procedure, absence, and maintenance age without claiming fire systems, alarm logic, emergency equipment availability, evacuation navigation, UI, interactables, or safety readiness.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for safety-station trace evidence.
- Safety content stays physical and local; no fire system, alarm logic, emergency equipment availability, evacuation navigation, UI, interactable, or readiness claim.
