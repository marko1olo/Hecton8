# RS099 Public Evidence Governance Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P496_PUBLIC_EVIDENCE_MISUSE_WARNING_BRIDGE
- P497_EVIDENCE_RELATION_GRAPH_DOSSIER_BRIDGE
- P498_TERMINAL_CLAIMANT_LANGUAGE_AUDIT_BRIDGE
- P499_PUBLIC_INDEX_SPOILER_CAP_BRIDGE

## Purpose

RS099 groups the public evidence governance packets that teach future site, wiki, PDA, scanner, terminal, caption, spoiler-policy, and string-pool surfaces how to talk about evidence without overclaiming it.

The set covers four static authoring lanes:

- public evidence misuse warning;
- evidence relation graph dossier;
- terminal claimant-language audit;
- public index spoiler cap.

These packets belong together because they control reader behavior around evidence: do not treat public captions as verdicts, do not treat relation edges as proof, do not treat claimant-safe language as raw truth, and do not treat category titles as final-route spoilers.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle. Future runtime delivery requires a separate source-table owner, route-card owner, string-pool extraction owner, native localization owner, importer/bake owner, and Unity/runtime proof owner.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS099 isolated as STATIC_SOURCE candidate.
2. Run source admission only after process gate is clean and a source/bake owner is assigned.
3. Preserve all packet IDs without truncation.
4. Preserve `runtime_ready`, `native_localization_ready`, `data_monolith_ready`, `h8bin_ready`, `unity_placement_ready`, `generated_page_ready`, and `publication_ready` as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show evidence categories, misuse warnings, relation graph hints, claimant-language mismatch, and public spoiler caps. It must not reveal final receiver outcomes, final legal results, Atlas-basin consequences, ending branches, protected claimant details, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show warning labels, category caps, and one next-proof target. Do not show dense relation graphs or final receiver data.

Middle: show relation chips, claimant-language mismatch chips, evidence class labels, and scanner gate state.

High: add dossier crosslinks, Marauder caution notes, and richer public/wiki summaries while preserving spoiler gates.

Ultra: add dense browsing, relation filters, and extended archive comparison. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, or runtime readiness.

## Validation Targets

- JSON parse for manifest and packet bundle.
- Exactly 4 packets.
- Exactly 15 locales per packet.
- Required localized surface keys: website_article, wiki_article, pda_codex, scanner_entry, terminal_note, evidence_caption, spoiler_policy, string_pool_key.
- UTF-8 without BOM.
- No U+FFFD.
- No mojibake marker/codepoint hits.
- No P500+ content in RS099.
- No positive readiness claims for runtime, native localization, DataMonolith, h8bin, Unity placement, generated pages, public website, or wiki publication.
