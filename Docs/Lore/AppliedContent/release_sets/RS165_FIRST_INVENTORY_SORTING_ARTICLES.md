# RS165 First Inventory Sorting Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1110_DRY_POUCH_SALT_CHECK
- P1111_WET_STOCK_RED_TAG
- P1112_DIRTY_TOOL_WRAP_QUARANTINE
- P1113_PERSONAL_EFFECTS_CUSTODY_TRAY
- P1114_SHARP_DEBRIS_EDGE_FLAG

## Purpose

RS165 gives first inventory sorting physical categories before any UI/runtime work: dry, wet, dirty, custody, sharp. The player should understand that not every found object belongs in the same pocket or can be treated as clean usable stock.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS165 improves the first-20 route by giving early salvage, triage, tool-care, and shelter stock a shared sorting language that can later feed scanner, PDA, inventory, or tutorial surfaces.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for sorting decisions.
- Sorting content stays physical and local; no inventory-system implementation claim.
