# P6801 Black Keel Nearlight Wake

Status: source_candidate_pending_native_localization_route_card_bake_and_unity_placement
Evidence class: STATIC_SOURCE
Release set: RS283_BLACK_KEEL_NEARLIGHT_WAKE
Packet ID: P6801_BLACK_KEEL_NEARLIGHT_WAKE
Article ID: applied_lore.black_keel_nearlight_wake
Runtime layer: Narrative
Spoiler tier: 1

## Source Brief

Speaker/source:
- External site: Public Site Hard-Sci-Fi Primer.
- In-game wiki: Recovered Route Engineering Note.
- Scanner: Suit Carrier-Link Classifier.
- Terminal: Black Keel Transfer Ledger.
- Audio: Black Keel Relay.
- Field note: Marauder Field Note.

Audience:
- Player and public/wiki reader who has encountered the first Black Keel receipt but does not yet know deep Atlas payload outcomes.

Date/era:
- 2190, after the player is stranded and before any final payload receiver branch.

Location/depth/route:
- Surface-to-photic shelf carrier-link route; Shallow Annex P-63 relay board, Black Keel window clock, Keelmark lien terminal, bathydrop ascent sleeve.

Unlock context:
- Packet receipt or carrier-window tone has occurred, but recovery remains closed.

Evidence object:
- Damaged speaker grille, flooded P-63 relay board, carrier packet queue receipt, ascent sleeve seal fault, Kestrel shadow notice, tonne-window lien line.

What this source knows:
- Black Keel is local Aegir claim-tender infrastructure.
- Data packets, bodies, samples and evidence use different clearance gates.
- Recovery requires mass, seal, storm/relay geometry, quarantine and receiver/custody alignment.

What this source does not know:
- Final Atlas basin receiver outcome.
- Final Deep Reach payload branch.
- Exact future ephemeris constants and numeric orbital periods.

What this source hides or gets wrong:
- Public/site version does not expose hidden Deep Reach hooks beyond established custody pressure.
- Carrier/terminal voice treats the player as payload/accounting state, not as a person needing mercy.

Player use:
- Teaches that carrier contact is route evidence, not rescue.
- Gives a practical next behavior: log the window, preserve proof tags, repair ascent hardware, do not spend ascent capacity on an audio cue.

Forbidden facts:
- No FTL, no ansible, no instant rescue.
- No Black Keel as personal loyal ship.
- No final Atlas payload or ending receiver spoilers.
- No exact orbital constants not owned by future celestial tables.

Required proper nouns/units:
- Black Keel, Aegir, Ran, Sol, Kestrel, Keelmark Mutual, Aegir Reclamation Pool, Recovery Compliance, tonne-window, P-63, HECTON-8.

LocIDs:
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_TITLE
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_SCANNER
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_TERMINAL
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_AUDIO
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_WIKI
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_SITE
- LORE_BLACK_KEEL_NEARLIGHT_WAKE_FIELD_NOTE

Localization status:
- en_US source_authority.
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN draft_machine_or_llm pending native review.

## Surface Contract

Scanner:
- Short operational carrier-link classification.
- No poetry and no final truth.

Terminal:
- Black Keel ledger language; cold, procedural, useful.

Audio:
- One carrier line that separates heard packet from lift impossibility.

In-game wiki:
- Recovered operational knowledge after the first receipt tone.
- Explains why the player should log windows and preserve proof.

External site:
- Longform public hard-sci-fi article stored as per-locale body files under `articles/RS283_BLACK_KEEL_NEARLIGHT_WAKE`.
- Spoiler-safe tier 1: route mechanics and Black Keel limits without final receiver outcomes.

Field note:
- Practical Marauder correction: hearing is not lift.

## Data Path

Authoring source:
- `Docs/Lore/AppliedContent/packets/RS283_BLACK_KEEL_NEARLIGHT_WAKE.packets.json`
- `Docs/Lore/AppliedContent/articles/RS283_BLACK_KEEL_NEARLIGHT_WAKE/*_external_site.md`

Generated/exported artifacts:
- `Docs/Lore/AppliedContent/in_game_wiki/<locale>/P6801_BLACK_KEEL_NEARLIGHT_WAKE.md`
- `Docs/Lore/AppliedContent/external_site/<locale>/P6801_BLACK_KEEL_NEARLIGHT_WAKE.md`
- `Docs/Lore/AppliedContent/Publication_Surface_Index.csv`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`

Runtime owner:
- Runtime must consume baked AppliedLore packet rows and packet hash only.
- Runtime must not read JSON, markdown, or site article bodies.

## Failure Model

No data:
- Targeted exporter and coverage audit fail missing locale/page/CSV rows.

Bad data:
- UTF-8/mojibake scan and AppliedLore text integrity fail source/page output.

Duplicate owner:
- Targeted exporter and coverage audit reject duplicate packet IDs and duplicate route ownership.

Stale handle:
- Route card export check catches stale DataMonolith route-card rows after source CSV edits.

Interrupted job:
- Targeted exporter can be rerun for `P6801_BLACK_KEEL_NEARLIGHT_WAKE` to restore selected publication and baked rows without overwriting unrelated packet content.

Save/load:
- No save runtime change in this packet; future unlock persistence remains under AppliedLore runtime/PDA owner.

Repeated subscribe/unsubscribe:
- No runtime event code changed; future placement should use baked packet hash in existing `NarrativeDiscovery`, `ScannableFragment`, `MessageTerminal`, or `NarrativeSpatialTriggerAuthoring` routes.

## QA Notes

Non-English rows are draft text coverage, not native publication approval.
External site pages are publication candidates only.
Unity placement, h8bin bake, runtime UI, native localization, RTL/CJK layout and profiler proof remain pending until separate proof artifacts exist.
