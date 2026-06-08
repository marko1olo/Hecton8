# RS185 First Power Status Trace Articles

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidates pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1200_BREAKER_FLAG_HALF_TRAVEL
- P1201_CONTACT_PAD_MATTE_PATCH
- P1202_RELAY_CASE_OZONE_STAIN
- P1203_EMERGENCY_LIGHT_DUST_HALO
- P1204_BUS_LABEL_HEAT_CURL

## Purpose

RS185 gives early power-panel and emergency-light props readable physical status traces: half-travel breaker flags, matte contact patches, relay case ozone stains, dust halos around emergency lights, and curled bus labels.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Source Boundary

These files do not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review or acceptance state.

## First-20 Boundary

RS185 improves the first-20 route by making power-adjacent hardware communicate caution and inspection order without claiming power state, diagnostics, repairs, shock events, UI, or interaction implementation.

## Localization Boundary

Each packet carries all 15 production locale rows. `en_US` is source authority. Non-English rows are draft summaries marked `draft_machine_or_llm`; native review and assigned-surface layout proof are required before player-facing release.

## Validation Targets

- Markdown source files exist for all five packet IDs.
- Exactly 15 locale rows per packet.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claims.
- Scanner text stays short, object-specific, and useful for power-panel evidence.
- Power content stays physical and local; no live power, diagnostics, repair, shock, UI, inventory, or interaction implementation claim.
