# RS185 Black Keel Local Tender Dossier

Evidence class: STATIC_DOC / CONTENT_SOURCE

Status: production article source candidate pending importer admission, route-card bake, DataMonolith bake, Unity placement, native localization and runtime proof.

Packet scope:

- P1200_BLACK_KEEL_LOCAL_TENDER_DOSSIER

## Purpose

RS185 gives the current Black Keel canon a player-facing source packet: Black Keel is a local Aegir claim-tender, not a home base, not a giant station parked over HECTON-8, and not instant rescue.

The packet turns early carrier contact into readable evidence: signal receipt, claim queue, bathydrop damage, quarantine handshake, accepted payload state, and tonne-window allocation.

## Source Boundary

This release set does not create website pages, in-game wiki pages, route cards, binding maps, source CSV rows, generated assets, Unity objects, runtime scripts, importer output, h8bin payloads, DataMonolith payloads, public deployment, native localization review, or acceptance state.

Runtime readers must not parse these Markdown files. These are cold authoring sources for future importer/bake work.

## Canon Boundary

Use this packet with the current locks:

- Black Keel is an Aegir-system claim-tender / salvage carrier.
- Public owner: Aegir Reclamation Pool.
- Insurance/custody shell: Keelmark Mutual.
- The player's starting lien is `4.8 tonne-window` equivalent.
- Black Keel holds around Aegir or a high transfer orbit, not safely above HECTON-8.
- Acknowledgement is not rescue.

Do not use stale draft assumptions where Black Keel is a four-kilometer orbital fortress over HECTON-8, a friendly ship, a companion station, a direct Deep Reach vessel, or an instant extraction solution.

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
- No FTL, ansible, instant rescue, final receiver outcome, family-revenge hook, or Black-Keel-as-home-base claim appears.
- Static text has no U+FFFD replacement character.
- Static text has no mojibake marker codepoint counts for U+00C3, U+00D0, U+00D8, U+00E6, U+00EC, U+00D7.
- No runtime, native-localization, DataMonolith, h8bin, Unity-placement or publication readiness claim appears.
