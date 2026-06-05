# Rationale 3248

Evidence class: STATIC_SOURCE
Timestamp: 2026-06-05 06:02:40 +04:00

## Mandates Followed

- QA_Evidence_Text_Filter_Audit.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

## Decisions

- Used RS097 packet JSON and manifest as the schema reference.
- Kept top-level packet JSON keys to schema, release_set_id, status, evidence_class, runtime_contract, packets.
- Used localized, not localization, with the 15 locale keys required by the task.
- Kept runtime/import/native/DataMonolith flags false because this is an authoring candidate only.
- Manifest uses authoring_packet_sources and omits packet_sources and canonical_importer_sources.
- Mapped English surface keys from P492-P495 surface sections where present.
- Mapped non-English draft rows from the production packet localization blocks into required surface keys without claiming native review.
- Preserved P492 archive-caption, P493 scanner-gate, P494 evidence-index, and P495 string-pool custody boundaries as source-only packet data.

## Boundary

No Unity, dotnet build, h8bin bake, source importer/exporter, publication tooling, protected source/runtime/generated paths, or production packet markdown edits were used.

## Controller Repair

Controller strict validation rejected the worker-generated RS098 artifact because the files carried UTF-8 BOM and the packet bundle contained mojibake marker hits in localized rows. Controller regenerated the RS098 markdown, manifest, and packet bundle from the validated UTF-8 production packet sources P492-P495. The repair did not edit production packet markdown, source CSV, route cards, generated pages, h8bin, Unity assets, runtime scripts, or DataMonolith payloads.
