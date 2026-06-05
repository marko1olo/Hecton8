# RS100 Public Evidence Cleanup Conflict Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P500_PUBLIC_ARCHIVE_RECEIVER_AMBIGUITY_BRIDGE
- P501_EVIDENCE_MARKET_CLEANUP_BID_BRIDGE
- P502_CLAIMANT_SAFE_SUMMARY_CONFLICT_BRIDGE

## Purpose

RS100 groups the next public evidence governance packets after RS099. The set teaches future public/wiki/in-game surfaces how to handle three evidence conflicts without collapsing them into verdicts:

- receiver labels can disagree without proving forgery;
- cleanup bids prove pressure around a fragment, not buyer guilt;
- claimant-safe summaries can omit true details for safety, leverage, or both.

The set is useful for future website articles, wiki policy entries, PDA/codex entries, scanner hints, terminal warnings, evidence captions, spoiler policies, and string-pool extraction.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle. Future runtime delivery requires separate source-table ownership, route-card ownership, string-pool extraction, native localization review, importer/bake proof, Unity placement proof, and player/runtime evidence.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS100 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show receiver conflict, cleanup pressure, claimant-safe omission, and next-proof targets. It must not reveal final receiver outcomes, final legal results, Atlas-basin consequences, ending branches, protected claimant details, exact coordinates, injury specifics, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show one conflict label, one warning, and one next-proof target per evidence object.

Middle: show receiver, cleanup, claimant, custody, and spoiler-state chips.

High: add archive comparison, Marauder caution notes, and relation-graph crosslinks.

Ultra: add dense browsing, side-by-side summary/object comparison, and cleanup-market filters. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

## Validation Targets

- JSON parse for manifest and packet bundle.
- Exactly 3 packets.
- Exactly 15 locales per packet.
- Required localized surface keys: website_article, wiki_article, pda_codex, scanner_entry, terminal_note, evidence_caption, spoiler_policy, string_pool_key.
- UTF-8 without BOM.
- No U+FFFD.
- No mojibake marker/codepoint hits.
- No P503+ content.
- No positive readiness claims for runtime, native localization, DataMonolith, h8bin, Unity placement, generated pages, public website, or wiki publication.
