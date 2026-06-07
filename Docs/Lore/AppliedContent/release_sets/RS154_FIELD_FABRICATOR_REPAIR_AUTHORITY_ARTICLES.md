# RS154 Field Fabricator Repair Authority Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P820_FIELD_FABRICATOR_GASKET_QUEUE
- P821_COLD_SEALANT_CARTRIDGE_WEIGHT
- P822_CONTACT_CUTTER_SPOOL_LIMIT
- P823_BATTERY_SLAB_WAKE_TEST
- P824_REPAIR_AUTHORITY_STAMP

## Purpose

RS154 makes early fabrication feel constrained, physical, and repair-first. The player should understand why the first field fabricator can produce a gasket, clamp, cutter contact, or wake-test part, but cannot become a general-purpose item printer.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS154 improves the first-20 route by giving the first fabricator/repair-stock lane object-specific constraints: repair queue, cartridge mass, cutter spool limit, battery wake test, and authority stamp.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for repair decisions.
- Fabricator content stays bounded by repair authority and does not imply broad crafting freedom.
