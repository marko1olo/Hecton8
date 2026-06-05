# Rationale_3238
## Decision
Build RS096 by mechanically adapting RS095 packet-bundle structure and extracting P480-P487 source rows. English rows preserve authored surface text where available. Other locale rows use their packet-local draft title/body/surface rows and stay inside the candidate authoring boundary.
## Mandates Followed
- QA_Evidence_Text_Filter_Audit.txt: evidence class stays STATIC_SOURCE; no engine/tool claims.
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt: preserved 15-locale roster and stable locale keys; no runtime text path changed.
- DATA_Runtime_Struct_Layout_ARM64.txt: no runtime DTO or native memory layout changed.
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt: no source CSV, bake path, importer, or binary output touched.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: no gameplay hot path or runtime code touched.
## Scope Control
Only the seven worker-scoped output files were written. Production packet markdown, source CSV, route cards, generated pages, engine assets, runtime scripts, and batch index paths were not edited by this worker.

## Controller Repair
Controller validation found truncated packet IDs for P483 and P486 in the RS096 manifest and packet bundle. The source input paths and summary packet list already carried the full names. Controller repaired only those ID values and re-ran static JSON validation.

## Residual Risk
STATIC_SOURCE only. Downstream string-pool extraction, localization review, route binding, and engine placement remain separate proof lanes.
