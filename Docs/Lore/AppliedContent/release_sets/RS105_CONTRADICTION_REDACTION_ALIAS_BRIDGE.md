# RS105 Contradiction Redaction Alias Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P515_PUBLIC_ARCHIVE_CONTRADICTION_CARD_BRIDGE
- P516_CLAIMANT_SAFE_REDACTION_AUDIT_PROMPT_BRIDGE
- P517_ROUTE_ALIAS_CONFLICT_RESOLUTION_HINT_BRIDGE

## Purpose

RS105 groups the next evidence-governance bridge around contradictions that must stay visible without becoming verdicts:

- contradiction cards that preserve two conflicting evidence facts;
- claimant-safe redaction audit prompts that protect identity while checking route effects;
- route-alias conflict hints that compare old marks, public labels, terminal paths, scanner confidence, and custody routes.

The set is built for future public/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction. It prevents the archive from flattening disputed evidence into a clean public sentence before proof order resolves the next test.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS105 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show contradiction cards, claimant-safe redaction audit prompts, and route-alias conflict hints. It must not reveal final receiver outcomes, final legal results, exact protected claimant, Atlas-basin consequences, ending branches, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show one contradiction or redaction reason with a single next-proof prompt.

Middle: show affected field, reason code, and next-proof target across archive and PDA surfaces.

High: add custody-route crosslinks, witness-hash state, scanner confidence, and terminal receipt comparison.

Ultra: add dense contradiction-family browsing, route-alias filters, claimant-safe audit history, and proof-order comparison panes. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

## Validation Targets

- JSON parse for manifest and packet bundle.
- Exactly 3 packets.
- Exactly 15 locales per packet.
- Required localized surface keys: website_article, wiki_article, pda_codex, scanner_entry, terminal_note, evidence_caption, spoiler_policy, string_pool_key.
- UTF-8 without BOM.
- No U+FFFD.
- No mojibake marker/codepoint hits.
- No beyond-scope packet content.
- No positive readiness claims for runtime, native localization, DataMonolith, h8bin, Unity placement, generated pages, public website, or wiki publication.
