# ARCHITECT_EYE_VISUALIZER - CORE/DIAGNOSTICS

Prompt source: inline user XML. `Docs/Tasks/CURRENT_BATCH.md` does not contain `ARCHITECT_EYE_VISUALIZER`.
Status language: VERIFIED MASTER GRADE - EYES OPEN.

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
- [x] 6. WORLD_SPACE_LABELS | Justification: added a zero-UGUI indirect-glyph renderer using a single cold-built atlas and per-character instanced quads above AUP entities | Alternative rejected: TMP/GameObject labels or per-frame managed text | Estimate: 5Hz sample, 20-45 us CPU on i3/MX350 at low budget, GPU one indirect draw
- [x] 7. SDF_VOLUME_DRAWER | Justification: added a Metal-safe SDF debug shader and indirect wire-cube volume proxy driven from `VoxelSdfTexture3D` density samples | Alternative rejected: CPU mesh rebuilds or SceneView-only Handles | Estimate: 3-8 us CPU, shares indirect draw
- [x] 8. SIGNAL_FLOW_MONITOR | Justification: copied typed `SignalBusRegistry` telemetry into a GlobalDataVault diagnostics buffer and renders lane pressure + waterfall history | Alternative rejected: managed EventBus/delegate hooks or reflection over signal lanes | Estimate: 8-25 us CPU at 24 visible lanes
- [x] 9. AUP_SECTOR_MAP | Justification: uses `IMacroDatabaseService.BuildSectorHashWindow` into a vault-owned hash buffer and renders hash-bit minimap cells | Alternative rejected: hydrating payloads or disk-backed queries during HUD draw | Estimate: 10-35 us CPU, 0 IO reads
- [x] 10. KINETIC_VECTOR_TRAILS | Justification: reads vault AUP/velocity arrays and emits oriented quad trails through the shared indirect renderer | Alternative rejected: `Debug.DrawLine`, line renderers, or Rigidbody GameObject scans | Estimate: 15-60 us CPU by tier budget
- [x] 11. GAS_HEATMAP_OVERLAY | Justification: reads `IGasDynamicsSolver` read-only O2/CO2 arrays and uses `math.select` color selection for screen heat cells | Alternative rejected: room GameObject lookups or material-per-room updates | Estimate: 6-25 us CPU by room budget
- [x] 12. BLACKBOX_TIMELINE_VIEWER | Justification: added an EditorWindow that loads `Dump_ARCHITECT_EYE_VISUALIZER.bin`, casts fixed blackbox records, draws a 300-frame timeline, and projects selected fault positions into SceneView | Alternative rejected: IMGUI runtime replay or managed JSON dump playback | Estimate: 0 us runtime; editor-only load/playback
- [x] 13. NAN_DETECTOR_HUD | Justification: runtime scans vault AUP/velocity/gas samples for non-finite data, renders an indirect red warning at the last fault AUP, and writes a binary dump once per fault burst | Alternative rejected: logging-only detection or waiting for Unity exceptions | Estimate: 3-20 us at 5Hz by sampled buffer budget
- [x] 14. MEMORY_MAP_GRAPH | Justification: renders a 2D block map from `H8Memory.BlockDescriptor` metadata with yellow fragmentation gaps and vault pressure bars | Alternative rejected: editor profiler dependency or heap reflection | Estimate: 6-30 us at 5Hz, no player allocations
- [x] 15. HOMEOSTASIS_DIAGNOSTIC | Justification: samples typed `SystemHealthSignal`/`FrameTimeSignal` lanes and renders a CPU/GPU heartbeat strip linked to `HomeostasisBrain.SystemHealthIndex01` fallback | Alternative rejected: managed delegates or polling Profiler API in player | Estimate: 4-12 us at 5Hz
- [x] 16. IL2CPP_STRIPPING_GUARD | Justification: added `[Preserve]` to runtime API, packed records, command entry points, and PDA console so optional debug builds keep the HUD surface | Alternative rejected: link.xml-only dependency with no source-level evidence | Estimate: 0 us runtime
- [x] 17. COMMAND_CONSOLE_DIEGETIC | Justification: added a fixed-buffer diegetic PDA command receiver with no UGUI and command routing for `ks +/-mask` and STP raw toggles | Alternative rejected: TMP/InputField or managed console delegates | Estimate: 0 us idle, O(command length) only on panel input
- [x] 18. STP_DEBUG_MODE | Justification: added raw STP state flag, command toggle, and indirect status panel fed by `GlobalRegistry.ResolutionScaler` | Alternative rejected: pipeline-specific render feature mutation from diagnostics | Estimate: 1-3 us at 5Hz
- [x] 19. BREADCRUMB_EDITOR | Justification: SceneView Ctrl+LeftClick appends packed AUP coordinates and FNV-1a hash to `Data/Balance/POIs.csv` for rebake through the CSV authority path | Alternative rejected: ScriptableObject POI assets outside Balance CSV source of truth | Estimate: 0 us runtime; editor click only
- [x] 20. PLATINUM_COMPILE | Justification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` and `dotnet build Hecton8.Editor.csproj -v:minimal /clp:ErrorsOnly /m:1` both exited 0 after local diagnostics fixes and minimal external compile-wall repairs | Alternative rejected: marking dependency walls while green builds were reachable with surgical compatibility fixes | Estimate: 0 us runtime

## Loop Log
- Loop 0: Authority files and mandates read. Status/rationale bootstrapped. No code written yet.
- Loop 1: Tasks 1-5 implemented. Compile verification pending.
- Loop 1 Compile Attempt 1: `dotnet build Hecton8.Core.csproj --no-restore` failed before diagnostics code on existing `GameBootstrapper` references to missing `Hecton8.Core.Bucketing.ModuloSimulationBucketer`; `dotnet build Hecton8.Editor.csproj --no-restore` failed because `obj/project.assets.json` is absent. Proceeding under 3-strikes protocol without mutating Bucketing domain yet.
- Loop 2: Tasks 6-11 implemented through `ArchitectEyeVisualizer`, vault-owned diagnostics buffers, and Metal-safe shaders. Compile attempt after local fix no longer reports diagnostics errors; current wall is external `ContextualPhysicalIkRuntime.cs` missing `KccVelocitySignal`.
- Loop 3: Tasks 12-16 implemented. Blackbox dump/replay, NaN warning, memory map, heartbeat graph, and preserve guards are in diagnostics/editor domain. Core build initially failed on moving external files; no diagnostics compiler errors were reported.
- Loop 4: Tasks 17-19 implemented. Diegetic command console, STP raw panel/toggle, and POI breadcrumb CSV writer are in place. Resource lifecycle reread found disabled/re-enabled visualizer GPU resources were not recreated; fixed with idempotent `EnsureResources()` and `OnDestroy` teardown.
- Loop 5: Compile inquisition complete. Minimal external compile repairs applied for older target APIs/import/constants: Bridge float bit hashing, LaserCutter `Unity.Collections` import, and Lockstep signal-lane constants mirrored from `GlobalSignals`.
- Loop 5 Verification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exited 0 on 2026-05-16.
- Loop 5 Verification: `dotnet build Hecton8.Editor.csproj -v:minimal /clp:ErrorsOnly /m:1` exited 0 on 2026-05-16.
