# Rationale 3243

Evidence class: STATIC_SOURCE
Timestamp: 2026-06-05 05:43:50 +04:00

## Mandates Followed

- QA_Evidence_Text_Filter_Audit.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

## Decisions

- Used RS096 packet JSON and manifest as the schema reference.
- Kept top-level packet JSON keys to schema, release_set_id, status, evidence_class, runtime_contract, packets.
- Used localized, not localization, with the 15 locale keys required by the task.
- Kept runtime/import/native/DataMonolith flags false because this is an authoring candidate only.
- Manifest uses authoring_packet_sources and omits packet_sources and canonical_importer_sources.
- Mapped English surface keys from P488-P491 surface sections where present.
- Mapped non-English draft rows from the production packet localization blocks into required surface keys without claiming native review.
- Preserved P490 receipt/note split where source rows provide separate receipt and Marauder note fields.

## Boundary

No Unity, dotnet build, h8bin bake, source importer/exporter, protected source/runtime/generated paths, or production packet markdown edits were used.
