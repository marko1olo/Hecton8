# Rationale 3229

Evidence class: STATIC_DOC.

Decision:
- Write Contract Continuity Desk as a Deep Reach language-control office, not a rescue office.

Reason:
- Canon locks define Contract Continuity Desk as a lower Deep Reach office surface.
- Canon locks tie present pressure to Recovery Compliance, Black Keel, Aegir Reclamation Pool, Keelmark Mutual, and Aegir Continuity Holdings.
- Writing/narrative rules require in-world artifacts and legally defensible corporate language, not villain confession or design summary.

Mandates applied:
- QA_Evidence_Text_Filter_Audit: all claims are static document claims only.
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc: locale rows preserve stable IDs and mark RTL/CJK/layout proof pending.
- DATA_Runtime_Struct_Layout_ARM64: no DTO/runtime layout claim was made.
- TOOL_Designer_Facades_CSV_Binary_Bridge: no CSV, h8bin, DataMonolith, or bake claim was made.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no hot-path or allocation claim was made.

Boundary:
- This task created authoring text only. It does not assert Unity, runtime, native localization, h8bin, DataMonolith, publication, generated-page, source CSV, route-card, or scene placement readiness.

Validation:
- Static packet checks passed for exact locale roster, locale status count, bracketed-heading absence, UTF-8 read, U+FFFD absence, mojibake marker absence, and positive readiness phrase absence.
