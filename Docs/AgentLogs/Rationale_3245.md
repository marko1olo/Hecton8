# Rationale 3245

Mandates followed:

- QA_Evidence_Text_Filter_Audit
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc
- DATA_Runtime_Struct_Layout_ARM64
- TOOL_Designer_Facades_CSV_Binary_Bridge
- OPT_Zero_GC_Policy_AllocFree_Mandate

Decisions:

- Wrote P493 as source packet text only. Reason: task forbids UI/runtime/source-table/binary edits and requires STATIC_DOC scope.
- Used scanner/codex/terminal/Marauder surfaces instead of implementation notes. Reason: writing.md requires player-facing artifact text, not a design explanation.
- Kept the gate focused on next physical proof target. Reason: task requires hiding consequence-heavy evidence while preserving readability of the next evidence action.
- Connected first-20 sanitized accident contradiction to packet custody, redaction headers, evidence queue, and public ledger release without exposing final receiver outcomes. Reason: Canon_Locks and Lore_Bible define public ledger exposure as delayed evidence pressure, not rescue or clean resolution.
- Wrote clean Unicode locale rows instead of copying adjacent mojibake patterns. Reason: task validation requires strict UTF-8 and explicit mojibake marker count of zero.

Open proof gaps:

- Native review absent.
- RTL/CJK/font/layout proof absent.
- String-pool extraction absent.
- Runtime and Unity delivery proof absent.
