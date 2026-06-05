# RS106 Source Voice Confidence Escalation Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P518_PUBLIC_ARCHIVE_SOURCE_VOICE_LABEL_BRIDGE
- P519_WIKI_EVIDENCE_CONFIDENCE_LADDER_BRIDGE
- P520_PDA_PROOF_ESCALATION_WARNING_BRIDGE

## Purpose

RS106 groups the next evidence-governance bridge around provenance and proof-state display:

- source voice labels that keep office filings, object marks, scanner reads, public summaries, and Marauder notes from collapsing into one neutral sentence;
- evidence confidence ladders that attach trust to fields instead of whole stories;
- proof escalation warnings that name a held proof class while keeping protected answers behind later gates.

The set is built for future public/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction. It prevents the archive from turning uncertain evidence into clean exposition before source voice, field confidence, and escalation boundary are visible.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS106 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show source voice labels, field-specific confidence states, and proof escalation warnings. It must not reveal final receiver outcomes, final legal results, exact protected claimant, Atlas-basin consequences, ending branches, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show source voice type, one field-confidence state, and one safe comparison lane.

Middle: add affected field, downgrade/hold reason, and next-proof class across PDA, scanner, and archive surfaces.

High: add source-lane comparisons, confidence history, contradiction-card links, and route alias/custody crosslinks.

Ultra: add dense provenance browsing, confidence-family timelines, escalation-boundary review panels, and proof-order comparison panes. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

## Validation Targets

- JSON parse for manifest and packet bundle.
- Exactly 3 packets.
- Exactly 15 locales per packet.
- Required localized surface keys: website_article, wiki_article, pda_codex, scanner_entry, terminal_note, evidence_caption, spoiler_policy, string_pool_key.
- UTF-8 without BOM.
- No U+FFFD.
- No C1 control-code mojibake markers.
- No four-question-mark placeholder locale rows.
- No beyond-scope packet content.
- No positive readiness claims for runtime, native localization, DataMonolith, h8bin, Unity placement, generated pages, public website, or wiki publication.
