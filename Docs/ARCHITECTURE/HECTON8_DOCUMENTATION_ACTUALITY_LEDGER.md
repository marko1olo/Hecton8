# HECTON-8 Documentation Actuality Ledger

Date: 2026-05-28
Status: PENDING VERIFICATION
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_FILESYSTEM / CLI_COMPILE where artifact cited

This ledger is the concise documentation-change register and proof-snapshot holder. Full historical text is archived at `../_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md`.

## Source Constants

| Contract | Current value | Source / proof |
|---|---:|---|
| Save writer version | `0x000B` | `Assets/_Project/Scripts/SaveBinaryStorage.cs` |
| Save header size | `56` bytes | `SaveBinaryStorage.CurrentHeaderSize` |
| Legacy save header size | `44` bytes | `SaveBinaryStorage.LegacyHeaderSize` |
| H8DM header size | `64` bytes | `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs` |
| H8DM directory record size | `64` bytes | `H8DataLayoutConstants.DirectoryRecordSizeBytes` |
| Data Monolith payload | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` | present; `1,064,384` bytes in 2026-05-28 static filesystem check |
| Signal lane capacity | `512` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` |
| Scalability DTO | `16` bytes | `ScalabilityStateDTO` static source |
| AUP/blit struct | `48` bytes | AUP static source |

Prompt/report values that disagree with source are stale. Current source wins.

## 2026-05-28 Root And Architecture Source Reality Audit

No runtime code was edited by `DOC_ROOT_ARCH_AUDIT`. This section records static source/filesystem facts used to update root and architecture onboarding docs.

| Area | Current static fact | Source / proof |
|---|---|---|
| Unity version | `6000.4.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | URP package `17.4.0` | `Packages/manifest.json` |
| Core packages | Addressables `2.7.6`, Input System `1.19.0`, Memory Profiler `1.1.12` | `Packages/manifest.json` |
| XR package presence | OpenXR `1.17.0`, Meta OpenXR `2.5.0`, XR Management `4.6.0` | `Packages/manifest.json`; package presence is not platform readiness |
| Enabled scene spine | `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD` | `ProjectSettings/EditorBuildSettings.asset` |
| First 20 route docs | New game uses `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`; load-game resume may enter `02_HECTON_WORLD` directly | `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`, `FIRST_20_MINUTES_ROUTE_BRIEF.md` |
| Scene authority drift | `AGENTS.md` still carries older no-orbit scene-flow wording; static source/docs include `01_ORBIT`; owner/integrator decision required before changing route doctrine | `AGENTS.md`, `ProjectSettings/EditorBuildSettings.asset`, `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` |
| First-party asmdefs | `167` under `Assets/_Project` | static filesystem count |
| Data Monolith payload | present, `1,064,384` bytes | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| Data Monolith scoped validator | `PASS`; `files=2`; `structs=32`; `mb=1.0495`; `seconds=0.491846`; Python schema/payload proof only | `../Reports/DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json` |
| Source topology doc | Added current project topology, source owner spine, route map, and verification gaps | `PROJECT_RUNTIME_TOPOLOGY.md` |
| Source topology source-spine recheck | Corrected player/environment runtime-context source anchors to `Assets/_Project/Scripts/Core/...` | PowerShell path scan |
| Domain coverage doc | Added echelon map to active docs, source anchors, and proof gaps | `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` |
| Domain coverage reference check | Every listed architecture doc and `Assets/_Project/Scripts` anchor exists | PowerShell path scan |
| Black-box doctrine sync | 300-frame rings; `Dump_*.bin` primary; `.h8dump` legacy | `../Actual Domains of Project.txt`, `../PROJECT_ATLAS.md` |
| Index updates | Root and architecture read orders now surface topology, boot, dispatch, first route, and platform proof ladder | `../README.md`, `README.md`, `../PROJECT_BASELINE.md`, `../HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `../HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` |
| Atlas stub cleanup | `observedAssemblyCount = 83` is compatibility-only; current count lives in topology/generated graph | `../PROJECT_ATLAS.md` |
| Dependency graph regeneration | `BuildArchitectureAtlas.py` regenerated graph markdown/json/cache; graph reports `220` asmdefs scanned, `167` first-party asmdefs under `Assets/_Project` | `../Generated/DEPENDENCY_GRAPH.md`, `../Generated/DEPENDENCY_GRAPH.json`, `../Generated/DEPENDENCY_GRAPH.cache.json` |
| AtlasCheck | `ATLAS_CHECK_PASS references=5807` | `python Tools/AtlasCheck.py` |
| H-Phi atlas | `PROJECT_ATLAS_HPHI.md` is absent; `HectonPhiStaticAudit.py --no-fail` timed out after 300 seconds before producing it | no proof artifact; see `../Generated/README.md` |
| Structure validation | `pass=true`; activeDocCount `704`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; encodingWithoutUtf8Sig `0` | `../Reports/DOC_STRUCTURE_VALIDATION_X_012.json` |
| OOP doc scanner | `finalPass=true`; activeFileCount `704`; sourceSyncPass `true`; active stale parameter files `0`; wordReductionPercent above `31%` | `../Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |

Interpretation: project file topology is now easier to read.

Compile, import, Play Mode, profiler, GC, save/load, player-build, shader, platform, and visual readiness remain unproven until fresh artifacts exist.

## Current Proof Snapshots

### Native Memory Ownership

Latest reviewed native alias evidence: `../Reports/VAULT_NATIVE_ALIAS_LEDGER_1315_PASS23.json`.

| Metric | Value |
|---|---:|
| Scanned files | `2439` |
| Parse failures | `0` |
| Native field declarations | `6648` |
| Forbidden persistent candidates | `834` |
| Forbidden MonoBehaviour candidates | `72` |
| Job-transient fields | `5481` |
| Stack-only/ref-struct view fields | `288` |
| Core-memory-allowed fields | `45` |
| Raw pointer fields | `861` |

Earlier cleanup evidence recorded `1770` forbidden persistent candidates and `358` MonoBehaviour candidates. The reduction is real. The project is still not clean.

Largest residual native groups are recorded in `../Reports/VAULT_NATIVE_ALIAS_LEDGER_1315_PASS23.json`; the project is still not clean.

### Compile Slice

Latest reviewed green full-solution CLI pass is stale after later source edits.

Current guarded builds hit generated-project or CPU-guard boundaries.

Runtime proof remains absent.

Required proof: Unity import, Console, Play Mode, profiler, GC, player build, shader import, save/load, platform, visual checks.

## 2026-05-26 Documentation Distillation

No C# source was edited by `DOCS_ACTUALIZATION`.

| Area | Active action | Proof |
|---|---|---|
| Project baseline | Rebuilt root baseline as stable authority/doctrine, not a current status page | `../PROJECT_BASELINE.md` |
| Root index | Removed current-build and scanner-counter prose from root read map | `../README.md` |
| Root policy | Removed current compile/report boundary from root reference | `../ROOT_DOCS_REFERENCE.md` |
| Architecture index | Kept only contract read order and routed current facts here | `README.md` |
| Reports index | Reframed reports as evidence storage, not knowledge base | `../Reports/README.md` |
| Deprecated transient indexes | Removed folder-index approach from active docs and recorded rejection | `../DEPRECATED/DOCS_ACTUALIZATION_TRANSIENT_INDEXES_2026-05-26.md` |
| Deprecated root docs noise | Moved token telemetry, FAQ, glossary, and marketing binary archive out of active `Docs/` root | `../DEPRECATED/Root_Docs_Noise_2026-05-26/MANIFEST.md` |
| Deprecated active-doc cleanup | Legacy bundles, superseded route card, and raw marketing sheets left active docs | `../DEPRECATED/Active_Doc_Deprecation_2026-05-26/MANIFEST.md` |

## Historical Distillate

| Area | Distilled fact | Proof |
|---|---|---|
| 1303 report bloat | `47` superseded SignalBus/tether report revisions were archived before V16 | `../_Archive/Reports_1334_2026-05-26/SignalBus1303Superseded/MANIFEST_1334.json` |
| SignalBus evidence | V16 SignalBus hot-path audit remains the current evidence snapshot | `../Reports/SIGNALBUS_HOTPATH_AUDIT_1303_APEX_V16.md` |
| X_012 root docs | Root text anchors were reduced to stable authority files | `../Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |
| X_012 historical reports | `160` top-level report text files were moved out of active corpus | `../_Archive/Reports_X_012_2026-05-23/MANIFEST.md` |
| APEX architecture pass | Verbose architecture ledgers were compressed and full snapshots archived | `../_Archive/Architecture_X_012_APEX_2026-05-23/` |
| Concision chain | Historical concision scans remain report evidence only | `../Reports/README.md` |

## Active Verification Gaps

| Area | Required proof artifact |
|---|---|
| Current full-solution compile | fresh guarded build log matching current source |
| Data Monolith runtime readiness | bake/import/boot/checksum/player-build proof for `static_data.h8bin` |
| Save readiness | current write/read/migration/checksum-failure artifact |
| Global authority runtime behavior | lane overflow, route-card, and profiler proof |
| Native memory ownership | fresh alias ledger plus owner-route fixes for remaining persistent and MonoBehaviour hotspots |
| Continuous scalability | frame-time, shader, and dynamic-resolution capture across quality weight range |
| AUP compliance | static scan plus rebase replay |
| Netcode | transport loopback, fuzz, jitter, hash replay, profiler, GC proof |
| UI zero-GC | GCMonitor or Memory Profiler capture |
| Terrain geography | generator/streaming proof against flooded terrestrial template |

## 2026-05-27 Runtime Allocation Route Pass

| Area | Current fact | Proof |
|---|---|---|
| Clean runtime files patched | Eight allocation-route call sites were removed; `WorldSliceAnchor` and `H8DataBaker` remain dirty-file residuals | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Release shader lookup | First-party `Shader.Find(...)` sites remain editor/development guarded in the local static scan | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Remaining residuals | `2` first-party non-Editor-folder `.ToArray()` text hits remain in dirty files | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Build boundary | Second guarded build launched legally; current failure is missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_RUNTIME_ALLOC_ROUTE_PASS2_20260527.log` |

## 2026-05-27 Read Accessor Purity Pass

| Area | Current fact | Proof |
|---|---|---|
| Read-shaped allocation route | `PerformanceBudgetController.GetBudgetStatus()` now reuses an owner snapshot; `RTLProcessor` lazy buffer route is named `EnsureBuffer()` | `../Reports/UNITY_READ_ACCESSOR_PURITY_PASS_UNKNOWN_20260527.md` |
| Read/create split | `WorldShippingContentFilter` now uses pure `TryGetSuppressedHierarchyIds()` and explicit `EnsureSuppressedHierarchyIds()` | `../Reports/UNITY_READ_ACCESSOR_PURITY_PASS_UNKNOWN_20260527.md` |
| Build boundary | Guarded build launched legally after final source edit; current failure is missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_READ_ACCESSOR_PURITY_RECHECK2_20260527.log` |

## 2026-05-27 Signal Contract Pass

| Area | Current fact | Proof |
|---|---|---|
| Runtime signal names | Confirmed duplicate `ToolDepletedSignal` contract errors are fixed; gameplay local event is now `PlayerToolDepletedSignal` | `../Reports/UNITY_SIGNAL_CONTRACT_PASS_UNKNOWN_20260527.md` |
| Plugin asmdef route | `Hecton8.Plugins` now directly references `Hecton8.Core.Contracts` for signal-contract usage | `../Reports/UNITY_SIGNAL_CONTRACT_PASS_UNKNOWN_20260527.md` |
| Contract scan | Final SignalBus contract audit reports `errors=0`, `confirmedErrors=0`, `asmdefContractBoundaryHits=0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_FINALSCAN.json` |
| Build boundary | Guarded build launched legally; current failure remains missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_SIGNAL_CONTRACT_PASS_RECHECK_20260527.log` |

## 2026-05-27 Signal Queue Diagnostics Pass

| Area | Current fact | Proof |
|---|---|---|
| Queue ownership scanner | NativeQueue helper ownership is now detected; `POSSIBLE_ORPHANED_SIGNAL_QUEUE=0` in the recheck | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Toolchain target | `SignalBusContractAuditCli` now targets `net10.0`; tool restore/build succeeded with `0` warnings and `0` errors | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Diagnostics flush allocation | `RuntimeDiagnosticsTrace.FlushSuppressedDuplicates()` no longer allocates a list on flush | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Remaining native rings | `8` registered non-Vault telemetry rings remain as owner-by-owner architecture decisions | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_QUEUE_RECHECK.json` |
| Build boundary | Full solution build was guard-blocked by CPU above `50%` for `30` attempts | `../Reports/BUILD_UNKNOWN_SIGNAL_QUEUE_DIAGNOSTICS_RECHECK_20260527.log` |

## 2026-05-27 Signal Audit Classifier And Seam MPB Pass

| Area | Current fact | Proof |
|---|---|---|
| Signal audit classifier | Constructor/editor-only/MPB/ComputeShader/owner-local telemetry/cold allocation/multiline method/layout cases are separated from hard warnings in the CLI source | `../Reports/UNITY_SIGNAL_AUDIT_CLASSIFIER_AND_SEAM_MPB_PASS_UNKNOWN_20260527.md` |
| Seam dither draw parameters | `SeamGapDitherRenderer` now sends per-draw buffers/camera/distance through a cached `MaterialPropertyBlock` instead of mutating the material | `../Reports/UNITY_SIGNAL_AUDIT_CLASSIFIER_AND_SEAM_MPB_PASS_UNKNOWN_20260527.md` |
| Contract-name cleanup | Clean local payload shadows were renamed; duplicate-name review warnings dropped from `48` to `14` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json` |
| Latest verified audit | Final full scan reports `errors=0`, `confirmedErrors=0`, `warnings=171`, `infos=826`, `SIGNAL_LAYOUT_REVIEW=2`, `JOB_STRUCT_LAYOUT_REVIEW=69 info`, `EXECUTABLE_STRUCT_LAYOUT_REVIEW=2 info` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json` |
| Build boundary | CLI build exits `0`; guarded full solution build exits `1` on Unity-generated editor project circular dependency and missing `Temp/CodexBuild/Unity.ShaderGraph.Editor.dll` | `../Reports/BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log` |

## 2026-05-27 Signal Telemetry Ownership Recheck

| Area | Current fact | Proof |
|---|---|---|
| TMP font swap route | `FontStreamingManager.ProcessSwapBatch()` no longer resolves `_targetFont.material`; the material is cached at queue start | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |
| NativeArray ownership audit | `SignalBusContractAuditCli` recognizes `VaultGenerationHandle`, `ref _fieldHandle`, and helper-owned `AllocateArray<T>` telemetry routes | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |
| Latest verified audit | Final recheck reports `errors=0`, `confirmedErrors=0`, `warnings=166`, `infos=832`; declared-only telemetry rings dropped to `1` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TELEMETRY_OWNERSHIP_RECHECK2.json` |
| Build boundary | Full solution build is not green: `350` error lines, `0` warning lines, no touched-source hits; failure buckets are generated/plugin project graph refs | `../Reports/BUILD_UNKNOWN_TELEMETRY_OWNERSHIP_FULL_SOLUTION_RECHECK_20260527.log` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=688`; `OOP_Doc_Scanner finalPass=true activeFileCount=688 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |

## 2026-05-27 Signal Layout/Alias Classifier Pass

| Area | Current fact | Proof |
|---|---|---|
| Cache-line classifier | `ProgressionEventSignal` and `VocalCueSignal` false stride debt is removed by global struct-layout indexing | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Native telemetry aliases | Expression-bodied Vault accessors, private-ref nested buffers, H8Memory release helpers, and DataVault allocator aliases are classified separately | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Signal-like DTO layouts | `FaunaDirector.AcousticPanicCommand` and `VocalWarningSystem.VocalWarningTelemetrySnapshot` now have explicit layout proof | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Latest verified audit | Final recheck reports `errors=0`, `confirmedErrors=0`, `warnings=155`, `infos=1171`, `SIGNAL_LAYOUT_REVIEW=0`, `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_FINAL.json` |
| Closed gap | Final borrowed-view classifier build passed in `UnknownFinal` with `0` warnings and `0` errors | `../Reports/BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_FINAL2_20260527.log` |
| Full solution boundary | Escalated `Hecton8.slnx` build is not green: `3141` warnings, `365` errors, `0` touched-file hits | `../Reports/BUILD_UNKNOWN_SIGNAL_LAYOUT_ALIAS_FULL_SOLUTION_ESCALATED_20260527.log` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=692`; `OOP_Doc_Scanner finalPass=true activeFileCount=692 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |

## 2026-05-27 Signal Residual Contract Cleanup

| Area | Current fact | Proof |
|---|---|---|
| Sector override commit route | `PersistentWorldRegistry` no longer snapshots due commit work with `List<T>.ToArray()`; the route uses a cold-owned bounded `SectorOverrideCommitWork[16]` buffer | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Tether tension lane | `TetherTensionSignal` is no longer declared cache-line-critical while carrying a 192-byte endpoint telemetry payload | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TBDR_UI_SANDBOX_TETHER_SECTOR_RECHECK.json` |
| Duplicate signal-like names | Clean sandbox/UI/TBDR local carriers were renamed; duplicate-name review warnings are now `8` instead of `14` | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Build boundary | Full project compile errors were intentionally not fixed in this pass by user instruction | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=693`; `OOP_Doc_Scanner finalPass=true activeFileCount=693 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |

## 2026-05-27 Core Memory Signal Domain Deep Pass

| Area | Current fact | Proof |
|---|---|---|
| Mod projection cull telemetry | Production ownership moved from local persistent ring to `GlobalDataVault` generation handle, with local fallback only when the Vault is unavailable | `../Reports/UNITY_CORE_MEMORY_SIGNAL_DOMAIN_DEEP_PASS_UNKNOWN_20260527.md` |
| TBDR Vault lifetime | Runtime and vertex-budget Vault buffers now release their `VaultGenerationHandle` routes on dispose | `../Reports/UNITY_CORE_MEMORY_SIGNAL_DOMAIN_DEEP_PASS_UNKNOWN_20260527.md` |
| SignalBus audit movement | Rebuilt-CLI recheck reports warnings `148 -> 145`, registered non-Vault telemetry rings `3 -> 0`, owner-local rings `7 -> 8`, and Vault aliases `3 -> 5` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_FINAL.json` |
| Tool build boundary | `SignalBusContractAuditCli` build succeeds with `0` warnings and `0` errors; full project compile errors are intentionally untouched | `../Reports/BUILD_UNKNOWN_SIGNAL_CLI_DEEP_DOMAIN_RECHECK_20260527.log` |

## 2026-05-27 Global Route Cache Pass

| Area | Current fact | Proof |
|---|---|---|
| Dispatcher lane registration | `ConnectionSplineBatchRenderer` and `SceneRuntimeService` use cached dispatcher availability plus direct `SystemDispatcher` lanes instead of registry wrappers in live helpers | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Object-pool command route | `ThreadSafeCommandQueue` and `ModWorldPersistenceManager` use cached object-pool dependencies for command drain, mod spawn, despawn, and restore routes | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Physics late-frame route | `SystemDispatcher` late-frame physics pending-count and flush now read cached `IPhysicsService`, not `GlobalRegistry.Physics` | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Transition presentation route | `SceneRuntimeService` terminal boot handles, world-drone audio bridge, tick dispatcher, and camera-juice handles are cached and hot-swap refreshed | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_CACHE_RECHECK.json` |

## 2026-05-27 Mod Registry Cache Pass

| Area | Current fact | Proof |
|---|---|---|
| Mod settings route | `ModSettingsRegistry` caches `UserOptionsPersistence` and storage keys; apply routes no longer read `GlobalRegistry.UserOptions` or rebuild the key string | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod slider persistence | Mod slider rows apply live callbacks in memory and persist once on commit/disable instead of saving options on every value event | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod catalog route | `ModItemRegistry` and `ModBuildableRegistry` cache inventory/logistics services; active catalog resolution reads cached owner interfaces | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod sandbox Vault route | `FutureCommandSandboxValidator.OpenVaultLane()` and rollback checks read cached `IDataVault`; the fallback registry read was removed | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`; touched-file findings are info-only | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_MOD_REGISTRY_CACHE_RECHECK.json` |

## 2026-05-27 Vault Rebind Release Pass

| Area | Current fact | Proof |
|---|---|---|
| Future command Vault lifetime | `FutureCommandSandboxValidator` releases all `20/20` sandbox `VaultLane<T>` handles through cached `IDataVault` on shutdown and DataVault rebind | `../Reports/UNITY_VAULT_REBIND_RELEASE_PASS_UNKNOWN_20260527.md` |
| Projected mod cull telemetry | `ModEventProjectionBridge` releases and reopens cull telemetry storage on DataVault hot-swap, with Vault-backed storage preferred and fallback only when Vault is unavailable | `../Reports/UNITY_VAULT_REBIND_RELEASE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`; touched-file findings are info-only | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_VAULT_REBIND_RELEASE_RECHECK.json` |

## 2026-05-28 Core Vault Release Pass

| Area | Current fact | Proof |
|---|---|---|
| StaticData/Babel telemetry ownership | Babel telemetry and BTree telemetry now use Babel-specific DataVault buffer IDs instead of sharing StaticData/BTree telemetry IDs | `../Reports/UNITY_CORE_VAULT_RELEASE_PASS_UNKNOWN_20260528.md` |
| Core Vault release routes | StaticData, Babel, SignalWarden tuning/telemetry/scratchpad, and MacroDatabase shutdown/rebind paths release Vault handles before clearing descriptors | `../Reports/UNITY_CORE_VAULT_RELEASE_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=73`, `infos=1020`; touched-file non-info findings are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_VAULT_RELEASE_COLD_RECHECK.json` |

## 2026-05-28 Core Sync IO And Accessor Pass

| Area | Current fact | Proof |
|---|---|---|
| Lockstep replay writer route | Replay writer setup moved to `OnEnable()` cold lifecycle; post-fixed replay write no longer opens/creates file state | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Cold Core IO contracts | Input replay setup, binding override deletion, and parent-directory creation now expose cold/persistence names | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Lockstep DataVault helper contract | Mutating `GetVaultBuffer<T>()` was renamed to `OpenOrAcquireVaultBufferView<T>()`; `TryGetVaultBuffer<T>()` remains existing-handle-only | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=68`, `infos=1025`; Core subtree non-info findings are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_SYNC_IO_ACCESSOR_RECHECK.json` |

## 2026-05-28 Global Signal Name Pass

| Area | Current fact | Proof |
|---|---|---|
| Global telemetry DTO names | Duplicate signal-like DTO names were removed by renaming local/narrow structs; runtime layout and BufferID/SystemID ownership were not changed | `../Reports/UNITY_GLOBAL_SIGNAL_NAME_PASS_UNKNOWN_20260528.md` |
| Editor diagnostics rows | `SystemDiagnosticsBoard` crash table row no longer uses a runtime signal-like DTO name | `../Reports/UNITY_GLOBAL_SIGNAL_NAME_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=57`, `infos=1024`; duplicate/editor signal-like warning categories are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_SIGNAL_NAME_RECHECK.json` |

## Validation

| Validator | Required state |
|---|---|
| Current `Tools/OOP_Doc_Scanner.py` | 2026-05-28 `finalPass=true`; activeFileCount `705`; sourceSyncPass `true`; active stale parameter files `0`; wordReductionPercent `31.170258220190806` |
| Current `Tools/VerifyDocStructure.py` | 2026-05-28 `pass=true`; activeDocCount `705`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; encodingWithoutUtf8Sig `0` |
| `Tools/OOP_Doc_Scanner.py` | `finalPass=true`; source sync; reduction `>=30%`; markers `0`; stale parameter files `0` |
| `Tools/VerifyDocStructure.py` | `pass=true`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0` |
| 1334 final scan | `DOCUMENTATION_OPTIMIZATION_REPORT_1334_FINAL_SCAN.json`: `finalPass=true`; active docs `693`; stale parameter files `0`; reduction `48.0975%` |
| 1334 final structure | `DOC_STRUCTURE_VALIDATION_1334_FINAL.json`: `pass=true`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0` |

This ledger may cite CLI compile artifacts. It is not Unity import, Play Mode, profiler, GC, player-build, or visual proof.
