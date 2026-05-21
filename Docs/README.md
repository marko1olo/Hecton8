# HECTON-8 Documentation Index

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE unless an artifact path is cited

## Authority Order

1. `AGENTS.md`
2. task-relevant files under `.agents-skills/`
3. current C# source under `Assets/_Project`
4. active contracts listed in this file
5. dated reports under `Docs/Reports`
6. archives under `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive`

Dated reports and archived files are evidence snapshots. They are not active system contracts.

## Current Source Reality

| Area | Current fact | Active document |
|---|---|---|
| Save container | `SaveBinaryStorage.CurrentVersion = 0x000B`; header size `56`; legacy header size `44`; aligned section header version `0x000B` | `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`, `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` |
| Data Monolith | Runtime payload target is `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; file is absent in this workspace scan; H8DM header is `16` bytes | `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md` |
| Global authority | `GlobalRegistry` is cold identity/DI only; `SignalBus<T>` is first-party hot broadcast; `GlobalSignals` direct queues are legacy bridge lanes; `HectonEventBus` is mod/API managed isolation; `GlobalDataVault` owns cross-domain native buffers | `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` |
| Signal counts | `SignalBusRegistry` capacity is `256`; current static route scan found `136` `ClearPostSimulation` lanes and `74` direct `NativeQueue<T>` fields in `GlobalSignals.cs`; `Docs/Modding/Validate_Mod_API_Static.ps1` reports `162 / 2 / 160` for source/projected/denied mod signals | `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` |
| Memory sovereignty | Persistent cross-domain native memory must route through `GlobalDataVault` and generation-checked handles. Private persistent `NativeArray` fields in managers are debt unless explicitly local scratch with owner disposal proof | `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` |
| Scalability | Authoritative scalar is `HomeostasisBrain.GlobalQualityWeight` through `ScalabilityStateDTO` (`16` bytes). Shader sinks include `_GlobalQualityWeight` and `_H8GlobalQualityWeight`. `_GlobalQualityParameters` is not current source authority | `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md` |
| AUP | `AbsoluteUniversePosition`/blit layout is `48` bytes. Distance checks subtract `double3` sector/local coordinates first, then cast the local delta to `float3` | `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md` |
| Netcode | `HectonNetworkManager.cs` is a compile-visible placeholder. Merkle/rollback protocol is static design pending transport, loopback, packet fuzz, profiler, and GC proof | `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md` |
| Terrain geography | Active world template is infinite-paging flooded terrestrial geography: 2 km deluge, mountain-crest volcanic islands, 0-400 m shelves, submerged river canyons, and hadal trenches | `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md` |

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
- `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`
- `Docs/Reports/2026-05-21_DOCUMENTATION_SANITIZATION_REPORT.md`

Archives:

- `Docs/DEPRECATED/README.md`
- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/README.md`
- `Docs/_Archive/README.md`
- `Docs/Archive/README.md`

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
