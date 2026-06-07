# RS169 First Navigation Mark Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1130_BEARING_CARD_SWELL_MARK
- P1131_DEPTH_TAB_FOGGED_WINDOW
- P1132_TETHER_KNOT_WET_SET
- P1133_LADDER_RUNG_PAINT_GAP
- P1134_SIGNAL_MAST_SHADOW_MARK

## Purpose

RS169 gives the first route physical navigation marks that work before a full map or UI route layer: swollen bearing cards, fogged depth tabs, wet-set knots, ladder paint gaps, and mast shadow marks.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS169 improves the first-20 route by making local navigation evidence legible through objects, not abstract markers.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for local route decisions.
- Navigation content stays physical and local; no map/UI/navigation-system implementation claim.
