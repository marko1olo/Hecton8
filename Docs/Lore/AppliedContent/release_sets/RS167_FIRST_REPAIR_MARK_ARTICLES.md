# RS167 First Repair Mark Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1120_PUMP_FLOW_ARROW_SCAR
- P1121_HOSE_CLAMP_TORQUE_TICK
- P1122_FILTER_GASKET_BITE_LINE
- P1123_GROUND_STRAP_SALT_BLOOM
- P1124_MANUAL_BYPASS_LOCKOUT_TAG

## Purpose

RS167 gives first repair spaces a set of readable maintenance marks: flow direction, clamp alignment, gasket compression, ground corrosion, and bypass lockout. The player should learn to inspect the mark before touching the part.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS167 improves the first-20 route by making early pump, hose, filter, power, and bypass objects communicate practical repair state through physical marks.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for repair decisions.
- Repair content stays physical and local; no repair-system implementation claim.
