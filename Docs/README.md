# HECTON-8 Documentation Index

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE / CLI_COMPILE where artifact cited

## Authority Order

1. `AGENTS.md`
2. task-relevant files under `.agents-skills/`
3. current C# source under `Assets/_Project`
4. active contracts listed in this file
5. current reports under `Docs/Reports`
6. archives under `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive`

Dated reports and archived files are evidence snapshots. They are not active system contracts.

## Current Source Reality

| Area | Current fact | Active document |
|---|---|---|
| Save container | `SaveBinaryStorage.CurrentVersion = 0x000B`; header size `56`; legacy header size `44`; aligned section header version `0x000B` | `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`, `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` |
| Data Monolith | Runtime payload target is `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; current file exists (`1,064,384` bytes); H8DM header is `64` bytes; Unity/player boot proof remains pending | `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md` |
| Global authority | `GlobalRegistry` is cold identity/DI only; `SignalBus<T>` is first-party hot broadcast; `GlobalSignals` direct queues are legacy bridge lanes; `HectonEventBus` is mod/API managed isolation; `GlobalDataVault` owns cross-domain native buffers | `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` |
| Signal counts | `SignalBusRegistry.LaneCapacity = 512`; current static route scan found `141` `ClearPostSimulation` hits and `35` `NativeQueue<T>` hits in `Core/Signals/GlobalSignals*.cs`; `Docs/Modding/Validate_Mod_API_Static.ps1` reports `162 / 2 / 160` for source/projected/denied mod signals | `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` |
| Memory sovereignty | Persistent cross-domain native memory must route through `GlobalDataVault` and generation-checked handles. Private persistent `NativeArray` fields in managers are debt unless explicitly local scratch with owner disposal proof | `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` |
| Scalability | Authoritative scalar is `HomeostasisBrain.GlobalQualityWeight` through `ScalabilityStateDTO` (`16` bytes). Shader sinks include `_GlobalQualityWeight` and `_H8GlobalQualityWeight`. `_GlobalQualityParameters` is not current source authority | `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md` |
| AUP | `AbsoluteUniversePosition`/blit layout is `48` bytes. Distance checks subtract `double3` sector/local coordinates first, then cast the local delta to `float3` | `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md` |
| Netcode | `HectonNetworkManager.cs` is a compile-visible placeholder. Merkle/rollback protocol is static design pending transport, loopback, packet fuzz, profiler, and GC proof | `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md` |
| Terrain geography | Active world template is infinite-paging flooded terrestrial geography: 2 km deluge, mountain-crest volcanic islands, 0-400 m shelves, submerged river canyons, and hadal trenches | `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md` |
| Last CLI compile PASS | 2026-05-24 latest zero-warning pass: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`. Latest compile attempt: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log`; failed before C# with `NETSDK1004` missing `Temp/obj/Hecton8.Editor/project.assets.json`, 0 warnings, no `CS*`; retry blocked by `BUILD_GUARD cpu=79 compiler_count=2`. Loop158 source gates cover loop134/139/141/142 log cleanup, HectonVoxelVolume sonar DataVault owner-cache cleanup, pure Environment/Ocean/PlayerSensory context getters, Dispatcher/DataVault/service hot-swap coverage, cached Save/Player/PlayerInventory ownership in `PersistentWorldRegistry`, UI/audio/construction Dispatcher tails through loop156, UI/Construction singleton tail removal in loop157, and world/environment/AI Dispatcher tails in loop158; targeted greps pass; broad file-local scans still have known false positives; Unity import/Play Mode/player/profiler proof still pending. | `Docs/Tasks/Status_EXTERNAL_CODEX.md`, `Docs/AgentLogs/Rationale_EXTERNAL_CODEX.md`, `Docs/AgentLogs/LOG_EXTERNAL_CODEX.md` |

Older prompt/report constants are subordinate to current source. Documentation follows source.

## Active Contract Map

Core:

- `Docs/DOC_GOVERNANCE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/Actual Domains of Project.txt`
- `Docs/ROOT_DOCS_REFERENCE.md`

Architecture spine:

- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`

Domain contracts added or reconciled by this pass:

- `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md`
- `Docs/ARCHITECTURE/MESH_STATE_SWAP_DESTRUCTION_PIPELINE.md`
- `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`

Reports:

- `Docs/Reports/README.md`
- `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`
- `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_SANITIZATION_REPORT.md`
- `Docs/Reports/DOCUMENTATION_CORPUS_INVENTORY_X_012.json`
- `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json`
- `Docs/Reports/DOC_STRUCTURE_VALIDATION_X_012.json`
- `Docs/Reports/BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json`
- `Docs/Reports/ARCHITECTURE_RESIDUAL_PROSE_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_MANUAL_PROSE_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_MANUAL_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_MICRO_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_ULTRA_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_35WORD_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_34WORD_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_33WORD_DENSITY_AUDIT_X_012.json`
- `Docs/Reports/ARCHITECTURE_32WORD_DENSITY_AUDIT_X_012.json`

Archives:

- `Docs/DEPRECATED/README.md`
- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/README.md`
- `Docs/_Archive/README.md`
- `Docs/_Archive/Reports_X_012_2026-05-23/README.md`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/README.md`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_RESIDUAL_PROSE/README.md`
- `Docs/Archive/README.md`

## 2026-05-23 X_012 Actuality Refresh

No C# source was edited.

Archived:

- root historical roadmap copy: `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/MASTER_RELEASE_WORK_PLAN.md`
- root historical build/playtest ledger copy: `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`
- top-level historical report markdown/txt set: `Docs/_Archive/Reports_X_012_2026-05-23/` (`160` report files)
- full binary payload ledger snapshot: `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/BINARY_PAYLOAD_INTEGRATION_LEDGER.full.md`
- full documentation actuality ledger snapshot: `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md`

Rewritten:

- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`
- `Docs/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`

Source corrections:

- `SignalBusRegistry.LaneCapacity` is `512`.
- Data Monolith payload exists at `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- H8DM header is `64` bytes.
- Save writer remains `0x000B` with 56-byte current header.

Validation:

- First-pass exact metrics are superseded by the APEX validation artifacts below.

## 2026-05-23 X_012 APEX Architecture Concision

No C# source was edited.

Actions:

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` rewritten from historical run log into active index.
- Full pre-compression copy archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/BINARY_PAYLOAD_INTEGRATION_LEDGER.full.md`.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` rewritten as a concise current-fact register; full snapshot archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md`.
- Extracted machine-readable payload inventory written to `Docs/Reports/BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json`.
- Global-boundary boilerplate, repeated refresh blocks, and historical Data Monolith absence wording removed from active architecture specs.

APEX validation:

- Exact current metrics: `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json`.
- Exact current structure gate: `Docs/Reports/DOC_STRUCTURE_VALIDATION_X_012.json`.
- Exact current micro-density gate: `Docs/Reports/ARCHITECTURE_MICRO_DENSITY_AUDIT_X_012.json`.
- Exact current 45-word density gate: `Docs/Reports/ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json`.
- Exact current 40-word density gate: `Docs/Reports/ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json`.
- Exact current 35-word density gate: `Docs/Reports/ARCHITECTURE_35WORD_DENSITY_AUDIT_X_012.json`.
- Exact current 34-word density gate: `Docs/Reports/ARCHITECTURE_34WORD_DENSITY_AUDIT_X_012.json`.
- Exact current 33-word density gate: `Docs/Reports/ARCHITECTURE_33WORD_DENSITY_AUDIT_X_012.json`.
- Exact current 32-word density gate: `Docs/Reports/ARCHITECTURE_32WORD_DENSITY_AUDIT_X_012.json`.
- Required scanner state: `finalPass=true`; source sync pass `true`; stale parameter files `0`; reduction at least `30%`; architecture marker hits `0`; long narrative paragraphs `0`.
- Required density state: architecture paragraphs/sentences/structured lines `>=32` words `0`; document-voice marker hits `0`; max architecture file words `<2500`.
- Required structure state: `pass=true`; root text docs `3`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; active non-BOM files `0`.

## 2026-05-24 X_012 APEX Strict Architecture Paragraph Pass

No C# source was edited.

Actions:

- `67` unstructured architecture paragraphs over `90` words were rewritten into bullet/list structure.
- `39` pre-strict architecture snapshots were archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-24/`.
- Exact changed-paragraph artifact: `Docs/Reports/ARCHITECTURE_CONCISION_AUDIT_X_012.json`.
- `Tools/OOP_Doc_Scanner.py` now fails if active architecture has unstructured paragraphs over `90` words or instructional markers.

Required strict state:

- `architecture.strictUnstructuredParagraphCount = 0`
- `architecture.tutorialMarkerHitCount = 0`
- `architecture.markerHits.* = 0`
- `activeStaleParameterFiles = 0`

## 2026-05-24 X_012 APEX Strict Architecture Line Pass

No C# source was edited.

Actions:

- Long architecture list/table lines over `70` words were split into shorter bullets or compact tables.
- Pre-line-split snapshots were archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_LINE_SPLIT/`.
- Exact line-split artifact: `Docs/Reports/ARCHITECTURE_LINE_CONCISION_AUDIT_X_012.json`.
- `Tools/OOP_Doc_Scanner.py` now fails if active architecture has structured list/table lines over `70` words.

Required line state:

- `architecture.strictStructuredLineCount = 0`
- `architecture.strictUnstructuredParagraphCount = 0`
- `architecture.tutorialMarkerHitCount = 0`
- `activeStaleParameterFiles = 0`

## 2026-05-24 X_012 APEX Architecture File-Cap Pass

No C# source was edited.

Actions:

- Six active architecture files over `2500` words were compressed into current-contract summaries.
- Full pre-compression snapshots were archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/`.
- Exact file-cap artifact: `Docs/Reports/ARCHITECTURE_FILE_CAP_AUDIT_X_012.json`.
- `Tools/OOP_Doc_Scanner.py` now fails if any active architecture file exceeds `2500` words.

Required file-cap state:

- `architecture.strictFileWordCount = 0`
- `architecture.strictStructuredLineCount = 0`
- `architecture.strictUnstructuredParagraphCount = 0`
- `architecture.tutorialMarkerHitCount = 0`

## 2026-05-24 X_012 APEX Residual Prose Pass

No C# source was edited.

Actions:

- Two active architecture `.diff` provenance files were archived at `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_RESIDUAL_PROSE/`.
- `184` residual long unstructured prose blocks were converted into bullet/list structure.
- `Docs/ARCHITECTURE/` line endings were normalized to UTF-8-SIG/LF.
- Exact residual-prose artifact: `Docs/Reports/ARCHITECTURE_RESIDUAL_PROSE_AUDIT_X_012.json`.
- Exact manual prose-pass artifact: `Docs/Reports/ARCHITECTURE_MANUAL_PROSE_AUDIT_X_012.json`.
- Exact micro-density artifact: `Docs/Reports/ARCHITECTURE_MICRO_DENSITY_AUDIT_X_012.json`.
- Exact 45-word density artifact: `Docs/Reports/ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json`.
- Exact 40-word density artifact: `Docs/Reports/ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json`.
- Exact 35-word density artifact: `Docs/Reports/ARCHITECTURE_35WORD_DENSITY_AUDIT_X_012.json`.
- Exact 34-word density artifact: `Docs/Reports/ARCHITECTURE_34WORD_DENSITY_AUDIT_X_012.json`.
- Exact 33-word density artifact: `Docs/Reports/ARCHITECTURE_33WORD_DENSITY_AUDIT_X_012.json`.
- Exact 32-word density artifact: `Docs/Reports/ARCHITECTURE_32WORD_DENSITY_AUDIT_X_012.json`.
- `Tools/OOP_Doc_Scanner.py` now fails on architecture paragraphs, sentences, or structured lines over `31` words.
- It also fails active architecture `.diff` and non-contract text files.

Required residual-prose state:

- `architecture.strictUnstructuredParagraphCount = 0`
- `architecture.strictUnstructuredSentenceCount = 0`
- `architecture.strictNonContractTextFileCount = 0`
- `architecture.strictStructuredLineCount = 0`
- `architecture.tutorialMarkerHitCount = 0`

## Verification Language

Use `PENDING VERIFICATION` unless the document links the current proof artifact.

Required proof classes:

- compile: build log path with command, timestamp, and exit code
- Unity import or Console: Unity log path
- runtime: Play Mode or player capture path
- profiler: Profiler or frame-time capture path
- memory: GCMonitor or Memory Profiler capture path
- rendering: Frame Debugger, renderdoc, screenshot, or GPU timing artifact

Static source reads do not prove runtime behavior.
