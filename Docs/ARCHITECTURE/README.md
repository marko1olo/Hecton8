# Architecture Index

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE / CLI_COMPILE where artifact cited

This folder stores stable architecture contracts. Dated reports are evidence only.

## Read Order

1. `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
2. `GLOBAL_AUTHORITY_BOUNDARIES.md`
3. `GLOBAL_AUTHORITY_OPERATING_MODEL.md`
4. `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
5. `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
6. `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
7. `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
8. `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
9. `DATA_MONOLITH_H8BIN_SPEC.md`
10. `DATA_MONOLITH_RUNTIME_INTEGRATION.md`
11. `SAVE_PAGING_PROTOCOL.md`
12. `SCALABILITY_MATRIX.md`
13. `CINEMATIC_CHEATS_LEDGER.md`
14. `FLOODED_TERRESTRIAL_GEOGRAPHY.md`
15. `AUP_PRECISION_STANDARDS.md`
16. `KINEMATICS_AUP_INTEGRATION.md`
17. `EQUIPMENT_SOA_LAYOUT.md`
18. `ZERO_GC_UI_PIPELINE.md`
19. `COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
20. `MESH_STATE_SWAP_DESTRUCTION_PIPELINE.md`

## Current Source Constants

| Surface | Current source fact |
|---|---|
| Save container | version `0x000B`; header `56` bytes; legacy header `44` bytes |
| H8DM static data | target path `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; file exists (`1,064,384` bytes); header `64` bytes; Unity/player proof pending |
| Scalability | `ScalabilityStateDTO` is `16` bytes; `GlobalQualityWeight` is continuous `0.0..1.0` |
| Shader quality sinks | `_GlobalQualityWeight`, `_H8GlobalQualityWeight`; `_GlobalQualityParameters` is not current source authority |
| Signal registry | `SignalBusRegistry.LaneCapacity = 512`; direct queue surface remains legacy bridge |
| AUP | 48-byte sector/local struct; subtract in double before float local handoff |
| Netcode | static protocol only; `HectonNetworkManager.cs` is not a transport implementation |

## Current Evidence Boundary

- 2026-05-24 EXTERNAL_CODEX:
  - Last zero-warning local dirty-workspace CLI PASS: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.
  - Latest compile attempt: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log`; fails before C# with missing project.assets and Temp/obj access denied.
  - Latest state: editor DLL output reached; 1 `MSB3101` Temp/obj cache warning; 0 errors; no `CS*`; no final summary/exit line.
- Loops66-151 fixed warning causes, registry fallbacks, SaveRuntime tails, owner-cache leaks, dispatcher/save/DataVault rebind gaps, interaction scene scan, Atlas read-model tails.
- Same span also fixed duplicate source inputs, tick-list probes, static-driver residues, release log callsites, sonar polls, and context getter mutation.
- It also covers loop151 rebind gaps, loop153 owner-cache ownership, loop154-156 Dispatcher tails, loop157 UI/Construction singleton tail removal, duplicate include tail, and FaunaBrain namespace wall.
- Targeted hot-swap greps pass in touched scopes; broad file-level scan still includes split-line/static-driver/legacy-stub false positives.
  - Runtime proof remains pending.
- 2026-05-23 X_012: `Docs/Reports/DOCUMENTATION_CORPUS_INVENTORY_X_012.json`, `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json`, and `Docs/Reports/DOC_STRUCTURE_VALIDATION_X_012.json` are offline documentation scan artifacts.
- This is CLI_COMPILE evidence only. Runtime architecture remains `PENDING VERIFICATION` until Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, save/load, scene wiring, and visual proof exist.
- The generated-project proof is provisional until Unity/project regeneration preserves the local asmdef references without hand-patching ignored `.csproj` files.

## Active Contract Groups

Global authority:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

Data and persistence:

- `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `DATA_MONOLITH_H8BIN_SPEC.md`
- `DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `SAVE_PAGING_PROTOCOL.md`
- `CONTENT_SAVE_SLOT_TOPOLOGY.md`

Runtime systems:

- `DISPATCH_PIPELINE.md`
- `SCALABILITY_MATRIX.md`
- `AUP_PRECISION_STANDARDS.md`
- `KINEMATICS_AUP_INTEGRATION.md`
- `ZERO_GC_UI_PIPELINE.md`
- `COOP_MERKLE_STATE_DELTA_PROTOCOL.md`

World and presentation:

- `FLOODED_TERRESTRIAL_GEOGRAPHY.md`
- `CINEMATIC_CHEATS_LEDGER.md`
- `MESH_STATE_SWAP_DESTRUCTION_PIPELINE.md`
- `EQUIPMENT_SOA_LAYOUT.md`
- `OFFLINE_MODULE_DAMAGE_BAKER_SHINOBU_210.md`

## Non-Claims

This index is not compile proof, Unity import proof, Play Mode proof, profiler proof, GC proof, player-build proof, or visual proof.

Use `PENDING VERIFICATION` unless the document links a current artifact path.
