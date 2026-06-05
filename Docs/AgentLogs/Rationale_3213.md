# Rationale_3213

Evidence class: STATIC_DOC.

Non-trivial decisions:

- Packet scope was narrowed to Aegir relay windows because P464 already covers Black Keel claim windows. P469 explains why signal receipt, relay clearance, quarantine handshake, and recovery mass are separate states.
- Route-card and DataMonolith language is candidate/authoring-only. No source CSV, route_cards CSV, h8bin, generated locale pages, Unity scenes, or runtime scripts were touched.
- Locale rows use en_US as source_authority and all 14 non-English rows as draft_machine_or_llm. No native-reviewed or runtime-ready status was claimed.
- Non-English rows were written as real Unicode drafts to avoid prior mojibake failure mode. Static scan showed U+FFFD=0 and U+00C3/U+00D0/U+00D8/U+00E6/U+00EC/U+00D7=0.
- GlobalQualityWeight was limited to presentation density. It does not change Article ID, LocID, unlock route, speaker/source, spoiler band, relay truth, quarantine requirement, save identity, or recovery availability.

Mandates followed:

- QA_Evidence_Text_Filter_Audit.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
