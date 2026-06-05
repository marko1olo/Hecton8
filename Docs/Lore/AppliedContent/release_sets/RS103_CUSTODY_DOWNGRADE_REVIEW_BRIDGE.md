# RS103 Custody Downgrade Review Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P509_PUBLIC_ARCHIVE_CUSTODY_DIVERGENCE_BRIDGE
- P510_SCANNER_CONFIDENCE_DOWNGRADE_REASON_BRIDGE
- P511_PDA_EVIDENCE_FAMILY_REVIEW_PROMPT_BRIDGE

## Purpose

RS103 groups the next evidence-review bridge around player-facing uncertainty:

- custody divergence between object custody, office custody, claimant-safe custody, and legal custody;
- scanner confidence downgrades when evidence channels conflict;
- PDA evidence-family review prompts that point the player to related proof without declaring a conclusion.

The set is built for future public/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction. It keeps evidence review usable without turning archive conflict into verdict text.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS103 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show custody divergence, scanner confidence downgrades, and PDA family-review prompts. It must not reveal final receiver outcomes, final legal results, exact protected claimant, Atlas-basin consequences, ending branches, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show one downgrade reason and one next-proof target.

Middle: show custody family, scanner confidence label, and PDA review chip.

High: add evidence-family grouping, route relation prompts, and receipt/header conflict hints.

Ultra: add dense archive comparison, family graph browsing, scanner-confidence history, and PDA review filters. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

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
