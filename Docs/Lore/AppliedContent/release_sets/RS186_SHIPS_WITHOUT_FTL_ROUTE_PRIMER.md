# RS186 Ships Without FTL Route Primer

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidate pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1201_SHIPS_WITHOUT_FTL_ROUTE_PRIMER

## Purpose

RS186 gives public-site, in-game wiki, scanner, terminal, audio, field-note, and black-box source copy for HECTON-8's no-FTL route pressure.

The packet explains why signals, legal claims, and debt can arrive before physical rescue; why Black Keel is local Aegir machinery; and why route fragments matter as evidence.

## Source Boundary

This release set does not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review, or acceptance state.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Canon Boundary

Use this packet with the current locks:

- Interstellar travel has no faster-than-light drive, no ansible, and no instant rescue.
- Early Aegir knowledge came through beam-assisted probes and autonomous packets.
- Heavy Atlas/Seed/colony freight used external staging, pellet-beam assisted fusion or related fusion freight, long coasts, magsail/aerobrake braking, and billable route infrastructure.
- Ran/Aegir remains a roughly 10.5-light-year-class route until final ephemeris tables own exact values.
- Black Keel is a local Aegir claim-tender, not the interstellar rescue ship.

## Localization Boundary

The packet carries all 15 supported locale sections:

- en_US
- ar_SA
- de_DE
- es_ES
- fr_FR
- he_IL
- id_ID
- ja_JP
- ko_KR
- nl_NL
- pl_PL
- pt_BR
- ru_RU
- uk_UA
- zh_CN

`en_US` is source_authority. Non-English rows are draft_machine_or_llm; they are not native final, not UI-fit proven, and not runtime-ready.

## Validation Targets

- Production packet source file exists.
- Exactly 15 locale sections exist.
- Non-English rows are not placeholder-only rows.
- No instant rescue, ansible, final receiver outcome, family-revenge hook, or Black-Keel-as-interstellar-rescue claim appears.
- Static text has no U+FFFD replacement character.
- Static text has no mojibake marker codepoint counts for U+00C3, U+00D0, U+00D8, U+00E6, U+00EC, U+00D7.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claim appears.
