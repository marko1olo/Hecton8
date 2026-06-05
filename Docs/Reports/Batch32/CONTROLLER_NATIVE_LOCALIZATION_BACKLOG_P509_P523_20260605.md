# Controller Native Localization Backlog P509-P523

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Native localization proof: absent.
Font/layout proof: absent.
Publication proof: absent.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

## Scope

Backlog for non-English locale rows in P509-P523. This proves only that rows exist and are marked `draft_machine_or_llm`. It does not prove native review, RTL/CJK layout, font atlas coverage, source extraction, runtime string-pool binding, generated pages, or publication.

## Packet Groups

| Packet range | Locale state | Primary blocker |
|---|---|---|
| P509-P511 | 14 non-English draft rows per packet. | Native review plus field-confidence terminology consistency. |
| P512-P514 | 14 non-English draft rows per packet. | Native review plus dispute/hold/checklist terminology consistency. |
| P515-P517 | 14 non-English draft rows per packet. | Native review plus contradiction/redaction/route-alias terminology consistency. |
| P518-P520 | 14 non-English draft rows per packet. | Native review plus source-voice, confidence-ladder, and proof-escalation terminology consistency. |
| P521-P523 | 14 non-English draft rows per packet; rows are ASCII-safe machine drafts. | Native replacement or review required before any player-facing non-English release. |

## Required Native Pass

1. Preserve Article IDs, LocIDs, packet IDs, and locale codes.
2. Preserve `source_authority` only for en_US.
3. Keep every non-English row marked `draft_machine_or_llm` until native reviewer approval, RTL/CJK/font/layout check, source extraction, and runtime proof exist.
4. Replace P521-P523 ASCII-safe machine drafts with real locale text before public/player-facing non-English use.
5. Test Arabic and Hebrew in RTL containers.
6. Test Japanese, Korean, and Simplified Chinese in CJK font atlases.
7. Test line length for scanner tags, PDA cards, terminal notes, captions, and public/wiki sidebars.

## Terminology Hotspots

- `source voice`
- `proof order`
- `confidence ladder`
- `claimant-safe`
- `route alias`
- `contradiction card`
- `proof escalation`
- `safe comparison lane`
- `crosslink-held`
- `edge-suppressed`

## Boundary

This backlog is not native localization review, generated-page proof, runtime proof, source CSV admission, route-card wiring, Unity placement, DataMonolith payload, h8bin bake, public website publication, wiki publication, or player-build proof.
