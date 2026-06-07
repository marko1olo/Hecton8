# RS174 First Suit Fit Mark Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1145_CUFF_GASKET_FOLD_LINE
- P1146_VISOR_WIPE_SALT_ARC
- P1147_GLOVE_SEAM_GRIT_CHANNEL
- P1148_BOOT_SOLE_SUCTION_RING
- P1149_PATCH_STITCH_PRESSURE_DOT

## Purpose

RS172 gives early suit and personal gear checks physical readability: cuff fold lines, visor salt arcs, glove seam grit, boot suction rings, and patch stitch dots. The player should learn that personal equipment has inspectable evidence before any suit-system implementation claim.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS172 improves the first-20 route by making worn gear and emergency suit checks readable through local marks without touching player, suit, pressure, or visor runtime code.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for gear inspection decisions.
- Suit content stays physical and local; no suit, player, pressure, or visor system implementation claim.
