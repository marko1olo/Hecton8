# RS102 Proof Order Relation Receipt Bridge

Evidence class: STATIC_SOURCE

Status: STATIC_SOURCE_CANDIDATE / BYTE_CODEPOINT_RECHECK_PASS / RUNTIME_PROOF_PENDING

Packet scope:

- P506_PUBLIC_ARCHIVE_PROOF_ORDER_BRIDGE
- P507_WIKI_SPOILER_SAFE_RELATION_EDGE_BRIDGE
- P508_TERMINAL_EVIDENCE_RECEIPT_REWRITE_BRIDGE

## Purpose

RS102 groups the next evidence-navigation bridge around proof discipline:

- public archive proof order that ranks object marks before clean labels;
- spoiler-safe relation edges that connect evidence packets without revealing route answers;
- terminal receipt rewrites that preserve old body fields under safer headers.

The set is built for future public/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction. It keeps the player's evidence-reading route practical: inspect the object, follow custody, use relation edges as prompts, and treat rewritten receipt headers as pressure marks until the body fields are tested.

## Source Boundary

This release set is a source-candidate bundle only. It does not admit rows into source CSV, route cards, generated pages, source hashes, h8bin, DataMonolith, Unity placement, runtime string pools, website publication, wiki publication, or native localization.

Runtime readers must not parse Markdown or this JSON bundle.

## 2026-06-05 Byte/Codepoint Recheck

Documentation-completeness recheck superseded the earlier console-render mojibake block. Current disk evidence for the RS102 bundle and P506-P508 production packets:

- JSON parse: pass.
- Packet counts: `3` manifest / `3` bundle.
- Locale counts: `15` per packet.
- UTF-8 BOM: absent.
- U+FFFD: `0`.
- Latin-1/C1 mojibake marker/codepoint hits: `0`.
- Arabic/Hebrew/CJK codepoint ranges are present where expected in draft locale rows.
- Readiness flags remain false.

The earlier failed wording was caused by treating console rendering of RTL/CJK text as file proof. This release set is no longer blocked for mojibake by current static disk evidence. It still must not be source-admitted, exported, baked, published, or used for native localization review until a separate source/bake/native-review owner produces that proof.

## Localization Boundary

Each packet in the bundle carries 15 locale entries:

- en_US as source_authority;
- ar_SA, de_DE, es_ES, fr_FR, he_IL, id_ID, ja_JP, ko_KR, nl_NL, pl_PL, pt_BR, ru_RU, uk_UA, zh_CN as draft_machine_or_llm.

Draft rows are authoring coverage only. They are not native-reviewed strings. RTL, CJK, font atlas, expansion, line-break, and bounded-UI checks remain open.

## Integration Order

1. Keep RS102 isolated as STATIC_SOURCE candidate.
2. Source admission may only happen under a clean process gate and explicit source/bake owner.
3. Preserve all packet IDs without truncation.
4. Preserve all readiness flags as false until separate proof exists.
5. Use packet surfaces for future website/wiki/PDA/scanner/terminal/caption/spoiler/string-pool extraction, not direct runtime parsing.

## First-20 Boundary

The first-20 route may show proof-order prompts, relation-edge hints, and rewritten terminal receipt conflict. It must not reveal final receiver outcomes, final legal results, exact rewrite authority, exact protected claimant, Atlas-basin consequences, ending branches, or rescue conclusions.

## Low / Middle / High / Ultra Consequences

Low/Compact: show one proof-order warning, one relation-edge label, and one receipt-rewrite prompt.

Middle: show object/custody/witness order, relation edge type, and receipt body-field conflict.

High: add relation-graph confidence chips, receipt body/header comparison, and proof-order timeline preview.

Ultra: add dense archive comparison, multi-edge browsing, receipt rewrite diffs, and proof-order filters. Ultra changes presentation density only; it does not change canon truth, Article IDs, LocIDs, source status, native-review status, runtime readiness, or publication state.

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
