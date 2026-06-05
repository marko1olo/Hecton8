# RS104 Dispute Hold Checklist Bridge

Evidence class: STATIC_SOURCE

Status: canonical source candidate pending controller review and downstream wiring

Packet scope:

- P512_PUBLIC_ARCHIVE_DISPUTE_REASON_CODE_BRIDGE
- P513_ARCHIVE_RESOLUTION_HOLD_PROMPT_BRIDGE
- P514_PDA_NEXT_PROOF_CHECKLIST_BRIDGE

## Purpose

RS104 groups the next evidence-action bridge around unresolved archive state:

- dispute reason codes that explain why confidence dropped;
- resolution hold prompts that name the missing proof class;
- PDA next-proof checklists that turn the dispute reason into one player action.

The set is built for future public/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction. It prevents unresolved evidence from becoming verdict text while still giving the player a concrete next proof target.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS104 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show dispute reason codes, resolution holds, and next-proof checklist prompts. It must not reveal final receiver outcomes, final legal results, exact protected claimant, Atlas-basin consequences, ending branches, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show one dispute reason and one next-proof action.

Middle: show reason code, missing proof class, and PDA checklist target.

High: add unresolved-family grouping, scanner downgrade crosslink, and receipt/custody prompts.

Ultra: add dense archive comparison, hold-state browsing, proof-target filters, and family review history. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

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
