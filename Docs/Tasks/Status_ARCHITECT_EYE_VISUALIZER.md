# ARCHITECT_EYE_VISUALIZER - CORE/DIAGNOSTICS

Prompt source: inline user XML. `Docs/Tasks/CURRENT_BATCH.md` does not contain `ARCHITECT_EYE_VISUALIZER`.
Status language: PENDING VERIFICATION until compile/runtime evidence exists.

## Mandates Read
- `ARCH_Signal_Lane_Segregation.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

## Checklist
- [x] 1. CSV_MASTER_PARSER | Justification: extended existing `H8DataMonolithCompiler` source enumeration to include `Data/Balance/*.csv` recursively while preserving `Assets/_SourceData` | Alternative rejected: parallel CSV parser/baker that would drift from Data Monolith authority | Estimate: 0 us runtime, editor-only ingest
- [x] 2. BINARY_BAKER_INTEGRATION | Justification: routed Balance CSV through the existing `.h8bin` Data Monolith bake path and output blob instead of a new binary format | Alternative rejected: separate `.h8bin` writer with duplicate section/header logic | Estimate: 0 us runtime, avoids duplicate cold bake maintenance
- [x] 3. LOC_ID_VALIDATOR | Justification: added FNV-1a pair validation for `*_id`/`*_hash32`, `id`/`hash32`, and `output`/`output_hash32`, using `H8DataHash` and fail-fast exceptions under `SIGNAL_AUTHORITY_VALIDATOR` label | Alternative rejected: trusting generated hash columns or validating after binary bake | Estimate: 0 us runtime, prevents bad hashes before blob write
- [x] 4. BULK_EDIT_FLOW | Justification: extended the editor `FileSystemWatcher` to watch both `Assets/_SourceData` and `Data/Balance`, then reuse existing bake + play-mode hot-reload socket | Alternative rejected: polling files during runtime or adding a gameplay file watcher | Estimate: 0 us runtime in player; editor watcher only
- [x] 5. VAULT_PROBE_API | Justification: added `VaultProbeUtility` generic byte-span bridge over existing `IDataVault.TryGetBuffer`/handle APIs and finite scanners for float, float3, and AUP buffers | Alternative rejected: reflection over `NativeArray` fields or exposing raw vault internals | Estimate: 0-5 us per debug probe, bounded by sampled buffer length
- [ ] 6. WORLD_SPACE_LABELS
- [ ] 7. SDF_VOLUME_DRAWER
- [ ] 8. SIGNAL_FLOW_MONITOR
- [ ] 9. AUP_SECTOR_MAP
- [ ] 10. KINETIC_VECTOR_TRAILS
- [ ] 11. GAS_HEATMAP_OVERLAY
- [ ] 12. BLACKBOX_TIMELINE_VIEWER
- [ ] 13. NAN_DETECTOR_HUD
- [ ] 14. MEMORY_MAP_GRAPH
- [ ] 15. HOMEOSTASIS_DIAGNOSTIC
- [ ] 16. IL2CPP_STRIPPING_GUARD
- [ ] 17. COMMAND_CONSOLE_DIEGETIC
- [ ] 18. STP_DEBUG_MODE
- [ ] 19. BREADCRUMB_EDITOR
- [ ] 20. PLATINUM_COMPILE

## Loop Log
- Loop 0: Authority files and mandates read. Status/rationale bootstrapped. No code written yet.
- Loop 1: Tasks 1-5 implemented. Compile verification pending.
- Loop 1 Compile Attempt 1: `dotnet build Hecton8.Core.csproj --no-restore` failed before diagnostics code on existing `GameBootstrapper` references to missing `Hecton8.Core.Bucketing.ModuloSimulationBucketer`; `dotnet build Hecton8.Editor.csproj --no-restore` failed because `obj/project.assets.json` is absent. Proceeding under 3-strikes protocol without mutating Bucketing domain yet.
