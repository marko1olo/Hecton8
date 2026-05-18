# SHINOBU_67 Status - Modular Base Construction Validator

Date: 2026-05-19
Status: PENDING VERIFICATION - STRICT H-PHI PREVIEW PURGE ADDED, BUILD NOT RERUN BY USER ORDER
Domain: MODULAR_BASE_CONSTRUCTION_VALIDATOR
Task Count: 20

## Hygiene

- Fresh status file created; no prior `Status_SHINOBU_67.md` content existed at session start.
- Fresh rationale file required at `Docs/AgentLogs/Rationale_SHINOBU_67.md`.
- Batch prompt conflict noted: `CURRENT_BATCH.md` contains another later `SHINOBU_67` block for Addressables. This agent follows the first `SHINOBU_67` block at line 1466 matching the user directive: `MODULAR_BASE_CONSTRUCTION_VALIDATOR`.

## Mandates Loaded

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - no managed allocation in construction validation hot path.
- `MATH_AUP_Determinism_Sync.txt` - AUP as authority; local float math only after root subtraction.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt` - no raw absolute float casts; shift generation/fence awareness.
- `DATA_Runtime_Struct_Layout_ARM64.txt` - unmanaged DTOs, multiple-of-8 sizes, no runtime bool fields.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - tracked native ownership, Burst job discipline, no mid-frame Complete.
- `DATA_Inventory_Resources_Items_SOA_Layout.txt` - resource cost/crafting must be SoA/numeric and signal-driven.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt` - graph splice via directed weighted node/edge data, no physics callbacks.
- `ARCH_Signal_Lane_Segregation.txt` - typed unmanaged signal lanes, no string event names.
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt` - SDF distance truth for terrain collision, finite guards.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` - 300-frame black box and binary dump on non-finite validation.

## Execution Checklist

- [x] Task 01: BINARY_GRAVEYARD_RECONNAISSANCE | DOD: `rg`/archive scan found no live `wfc_module_bounds.h8bin`; fallback is 32B `StructuralBoundsDTO` from `BaseModuleTemplate.ProxyBounds*`, CSV Vault override path, and `GenerateEmergencyMockBounds()` Vault hydration | Rejected: guessing binary layouts from unrelated archive fragments | Estimate: 0 us runtime, cold scan only
- [x] Task 02: PHYSICS_OVERLAP_ERADICATION_PASS | DOD: removed `Physics.OverlapBoxNonAlloc` from `PlayerBuilder.UpdateTerrainSdfPlacementState`; construction AABB now routes through math/SDF validator; live preview also rejects a target grid cell already occupied by registered modules using AUP-local grid math | Rejected: collider broadphase, layer masks, and collider-driven module overlap truth | Estimate: 8-35 us saved per drag validation on i3/MX350, plus O(moduleCount) integer fallback until Vault occupancy owner exists
- [x] Task 03: CS1612_ENCAPSULATION_PURGE | DOD: verified existing `BaseModuleStateDTO` already has explicit 64B raw fields and `AsRef`; new DTOs use raw fields only | Rejected: properties on hot DTOs | Estimate: prevents copy-write mistakes, no direct frame delta
- [x] Task 04: ARM64_PADDING_RECONSTRUCTION | DOD: `ConstructionRequestDTO` explicit 64B; bounds/result/settings/occupancy are 32B | Rejected: `Pack=1`, sequential unknown offsets | Estimate: avoids unaligned ARM64 lanes
- [x] Task 05: BLIND_DEPENDENCY_MOCKING | DOD: local `MockWorldSampler` added; negative distance rejects terrain | Rejected: direct dependency on unavailable Agent 41 runtime lease | Estimate: 1-9 scalar probes per validation
- [x] Task 06: BURST_GRID_VALIDATION_KERNEL | DOD: `BurstGridValidationJob` validates grid occupancy, SDF distance, neighbor port mask, and result flags from unmanaged DTOs; PlayerBuilder now has a no-allocation live occupancy fallback against `ConstructionManager.SpawnedModules` because a Vault occupancy mirror is not yet exported | Rejected: GameObject/socket scans in Burst job and per-frame NativeMultiHashMap allocation | Estimate: target <10 us kernel, live fallback O(moduleCount) integer grid compare
- [x] Task 07: STRUCTURAL_INTEGRITY_PREFLIGHT | DOD: `ConstructionSipBudgetDTO` and pressure ratio warning output added; PlayerBuilder fills added SIP from template yield/volume fallback | Rejected: hard dependency on HullIntegrity asmdef from Core | Estimate: <1 us scalar preflight
- [x] Task 08: THE_DEAR_LIE_HOLOGRAPHIC_PREVIEW | DOD: `ConstructionPreviewSignal` (96B explicit layout) is emitted from PlayerBuilder after SHINOBU_67 validation; `HectonBlueprintPreviewBatch` consumes that unmanaged lane through Vault-backed preview buffers, stores only `VaultBufferHandle<T>` fields, resolves `NativeArray` views as local variables, and locks `ConstructionPreviewWrite/Build/Matrices` while the Burst matrix job owns pointers; true Agent 09 BRG module-hash renderer remains an external upgrade | Rejected: direct renderer dependency, persistent private NativeArray ownership, unlocked Vault compaction risk, or new preview GameObjects in the validator | Estimate: one typed signal push per preview frame, batch path uses one Burst matrix job
- [x] Task 09: LOGISTICS_GRAPH_SPLICING_JOB | DOD: `LogisticsGraphSpliceJob` creates neighbor `int2` edge scratch from port-aligned grid cells | Rejected: inventing incremental `LogisticsNetworkGraph` API | Estimate: 6 neighbor lookups
- [x] Task 10: INVENTORY_COST_DEDUCTION_LINK | DOD: existing `HabitatConstructionManager.HasBuildResources/ConsumeBuildResources` owns transaction and rollback; `DebugDeployActiveBuildable` now also destroys spawned modules if consume fails; no `MockCraftingRequestSignal` used | Rejected: fake production `CraftingRequestSignal` dependency | Estimate: existing managed click-only cost
- [x] Task 11: CONTINUOUS_SCALABILITY_COLLISION_LOD | DOD: `GlobalQualityWeight` maps through `math.step(0.3,q) * math.smoothstep(0.3,1.0,q)` and `math.lerp(1,9,curve)` to 1-9 terrain probes; weight below 0.3 collapses to center-only | Rejected: binary low/high switches | Estimate: low 1 probe, high 9 probes
- [x] Task 12: DECONSTRUCTION_RECOVERY_MATH | DOD: `DeconstructionConnectivityJob` BFS rejects disconnected wings | Rejected: transform hierarchy validity | Estimate: O(edges) cold delete click
- [x] Task 13: AUP_LOCALIZED_GRID_SNAP | DOD: `TryBuildRequestFromAup` computes `LocalPos = TargetAUP - RootAUP` before int grid snap; voxel SDF probes now use `double3 AUP -> HectonFloatingOrigin.ToRuntimePosition(double3)` before runtime SDF sampling, with no absolute `float3` probe cast | Rejected: absolute float world snap and `float3` AUP handoff | Estimate: 1 double subtract + round, no extra probe count
- [x] Task 14: ACOUSTIC_CLUNK_EMISSION | DOD: build commit emits canonical `AcousticPingSignal` with `ChannelMetalStress`, AUP center, module radius, and source hash; duplicate `AcousticEchoTap` structs in other domains were deliberately not referenced | Rejected: adding another same-name `AcousticEchoTap` collision or direct audio runtime dependency | Estimate: one typed signal push per committed module
- [x] Task 15: FLORA_CLEARING_BROADCAST | DOD: `FloraExclusionSignal` (80B explicit layout) is emitted on build commit with AABB center/extents; existing `BaseModuleNavModifier` still performs immediate vegetation/terrain-hole bridge on module enable | Rejected: direct flora manager mutation from PlayerBuilder | Estimate: one typed signal push per committed module
- [x] Task 16: ZERO_INIT_OVERHEAD_BYPASS | DOD: `AllocateRequestScratch` uses `NativeArrayOptions.UninitializedMemory` | Rejected: clear-memory scratch on hot path | Estimate: avoids clear cost
- [x] Task 17: TELEMETRY_BUILDER_RECORDER | DOD: 300-frame `ConstructionTelemetryEntry` Vault ring is resolved from `ConstructionBuilderTelemetry`; PlayerBuilder writes one row per terrain validation; non-finite state dumps `Dump_SHINOBU_67.bin` | Rejected: managed per-frame logs | Estimate: one 64B row/write
- [x] Task 18: BUILDER_TUNER_EDITOR_WINDOW | DOD: `WfcBuilderTunerWindow` added with Grid/Bounds/Clearance controls and DataVault write via `ConstructionBuilderTuning` | Rejected: inspector-only tweak path | Estimate: editor-only
- [x] Task 19: CSV_OVERRIDE_INGESTOR | DOD: span-based `module_bounds.csv` parser writes either caller hash map or `ConstructionBuilderBounds` Vault buffer; editor button loads the CSV cold-path | Rejected: string split/LINQ in parser | Estimate: cold hotload only
- [x] Task 20: GIZMO_GRID_VISUALIZER | DOD: editor SceneView gizmo draws grid and red rejection box from last validator result | Rejected: prefab debug markers | Estimate: editor-only

## Loop Notes

- Loop 0: Prompt extracted by CLI, mandates read, status initialized. No code edited yet.
- Loop 1: Tasks 1-5 implemented/verified by source scan. `Hecton8.Core.csproj` initially failed because new validator file was not included in generated project; fixed project include and removed HullIntegrity direct asmdef dependency from PlayerBuilder.
- Loop 2: Tasks 6-13/16-20 implemented as unmanaged validator/jobs/editor facade. Initial compile wall was external `LocRegistry.cs` `math.reversebytes`.
- Loop 3: Re-read validator and builder terrain path. Found wrong dump filename (`Dump_BUILDER_SURGEON.bin`) and wrong quality source (`1 - SystemHealthIndex01`). Fixed to `Dump_SHINOBU_67.bin` and `HomeostasisBrain.GlobalQualityWeight`.
- Loop 4: Re-scanned construction files for `OverlapBox`, literal line-ending damage, and whitespace errors. Result: no construction `OverlapBox` remains; no literal `` `r`n`` remains; diff check has only a Git LF/CRLF warning in already-dirty `H8Memory.cs`.
- Loop 5: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. SHINOBU_67 files produce no compiler errors; current stop is external `Assets/_Project/Scripts/Economy/EconomyRuntimeInstaller.cs` missing `TradeMarauderDirector`.
- Loop 6: Re-extracted prompt and repeated inventory/signal scan. Production `CraftingRequestSignal` still absent; only `MockCraftingRequestSignal` exists. Fixed dev deploy race by rolling back spawned module when `ConsumeResources` fails. Added `Docs/AgentLogs/SelfAudit_SHINOBU_67.xml`.
- Loop 7: Added Unity `.meta` files for the new construction validator and editor tuner assets. Re-ran scoped `OverlapBox`/`Instantiate` scan: no hits. `dotnet build Hecton8.Core.csproj` now stops on external `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs(1451,58)` missing `VolcanicUpdraftVault.SafeNormalize`.
- Loop 8: Ultra polish pass: read batch prompt, rationale, and binary payload ledger; no `dotnet build` launched per user instruction. Added Vault-backed telemetry/bounds resolve, CSV-to-Vault ingestion, emergency mock bounds hydration, center-only low-quality probe collapse, and exhaustive XML self-audit.
- Loop 9: AUP precision recheck: found construction voxel probe still casting `RootAUP + localProbe` to absolute `float3` before `HectonVoxelVolume.GetSDFDensity`. Added runtime-space SDF facade and changed builder probe to `double3 AUP -> runtime Vector3 -> TrySampleRuntimeSdfDensity`. No `dotnet build` launched per user instruction; static grep/diff-check only.
- Loop 10: Grid occupancy recheck: live builder path used `ValidatePlacementNoOccupancy`, so occupied grid cells were only covered by lower-level job surfaces not fed by preview. Added no-allocation AUP-local occupied-cell fallback over registered modules and explicit `GRID OCCUPIED` rejection. Rejected per-frame `NativeParallelMultiHashMap` allocation; Vault occupancy export remains pending owner contract.
- Loop 11: Black-box hash recheck: external occupancy/SDF failure flags were patched after `ValidatePlacementNoOccupancy`, leaving `ResultHash` stale. Added `ApplyFailureFlags()` in the validator so validity, structural-warning byte, occupied cell hash, min SDF, and telemetry hash update atomically.
- Loop 12: Signal lane recheck: added `ConstructionPreviewSignal` and `FloraExclusionSignal` explicit DTOs, routed PlayerBuilder preview/commit through typed SignalBus lanes, emitted build clunk through canonical `AcousticPingSignal`, and hardened `HectonBlueprintPreviewBatch` Burst/layout attributes.
- Loop 13: H-PHI preview buffer recheck: moved `HectonBlueprintPreviewBatch` native preview/matrix buffers to `GlobalDataVault` handles `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices`; later strict pass found alias fields still violated literal Vault law. No `dotnet build` launched by user order; verification is scoped static scan/diff-check only.
- Loop 14: Strict H-PHI preview recheck: removed private `NativeArray` alias fields from `HectonBlueprintPreviewBatch`, kept only `VaultBufferHandle<T>` fields, resolved local Vault views per method, added `TryLockBuffer`/`TryUnlockBuffer` guards for `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices` around the scheduled Burst matrix job, added teardown completion/unlock, changed collision probe curve to `math.step * math.smoothstep * math.lerp`, and guarded SIP pressure division with `math.rcp(math.max(...))`. No `dotnet build` launched by user order; static scans/diff-check only.

---

# SHINOBU_67 Status - Addressables Heap Sanitizer

Date: 2026-05-19
Status: PENDING VERIFICATION - MANAGED CAPACITY SEAL ADDED, BUILD DEFERRED BY CPU GUARD
Domain: CORE & MEMORY INFRASTRUCTURE / ADDRESSABLES_HEAP_SANITIZER
Task Count: 20

## Hygiene

- [HYGIENE_VIOLATION] Existing `Status_SHINOBU_67.md` and `Rationale_SHINOBU_67.md` contained the older modular-base validator pass. Preserved old evidence and appended a separated Addressables section.
- Active prompt is the second `<AGENT_PROMPT id="SHINOBU_67" role="ADDRESSABLES_HEAP_SANITIZER">` at `Docs/Tasks/CURRENT_BATCH.md:2620`, matching the user request for memory control during chunk loading.
- Existing first prompt at `Docs/Tasks/CURRENT_BATCH.md:1466` is not active for this request.

## Mandates Loaded

- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt` - every handle must enter a global registry, ref-count zero schedules release, leak audits required.
- `STRM_World_Streaming_Residency_Chunk_Management.txt` - chunk state must avoid stop-the-world defrag and use non-blocking eviction.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - hot tracking uses uint keys and native/fixed storage, not string dictionaries.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` - 300-frame black box and binary dump on leak/fault.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` - use existing registered `AssetLifecycleGovernor`, no competing singleton.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` - VRAM > 0.90 triggers aggressive response.
- `CORE_Global_State_Reset_NonReload_Transitions.txt` - static/native state must reset cleanly on non-reload transitions.
- `STRM_Async_Standard.txt` - no coroutine/task storms for runtime load orchestration.

## Execution Checklist

- [x] Task 01: BINARY_GRAVEYARD_RECONNAISSANCE | DOD: archive scan found no authoritative `asset_cache_profiles.h8bin`; `GenerateEmergencyMockProfiles()` seeds 16B cache profile DTOs | Rejected: guessing a binary layout from unrelated archive hits | Estimate: 0 us runtime, cold init only
- [x] Task 02: ORPHANED_HANDLE_ERADICATION_PASS | DOD: chunk prefab loads in `WorldChunkResidencyManager` and world prefab loads in `ItemCatalog` now route through `AssetLifecycleGovernor`; remaining first-party direct releases are bootstrap dependency handles, content-authority fixed ledgers, or fallback paths when governor is unavailable | Rejected: replacing `ContentRuntimeServices` bundle ledger in a chunk-memory pass | Estimate: one duplicate Addressables handle avoided per cache hit
- [x] Task 03: CS1612_ENCAPSULATION_PURGE | DOD: `AssetTrackerDTO` uses raw public fields only; no property setter/copy mutation path | Rejected: `{ get; private set; }` hot DTO accessors | Estimate: ref mutation remains direct integer lane
- [x] Task 04: ARM64_PADDING_RECONSTRUCTION | DOD: `AssetTrackerDTO` is explicit 16B: uint/int/ulong; `AssetCacheProfileDTO` and mock signal are 16B | Rejected: `Pack=1` or sequential layout | Estimate: avoids unaligned ARM64 reads
- [x] Task 05: BLIND_DEPENDENCY_MOCKING | DOD: `MockChunkLoadSignal` and `MockChunkLoadSpamJob` added for synthetic refcount spam | Rejected: hard dependency on Agent 35 chunk director internals | Estimate: test job only, no gameplay cost
- [x] Task 06: NATIVE_HANDLE_REGISTRY_KERNEL | DOD: Vault-backed `AddressableHeapHandleMap` open-address table stores `AssetHash -> slot/refcount/ulong handle id` in 64B `AssetHandleMapEntryDTO`; fixed `AsyncOperationHandle[]` pool remains the Unity bridge | Rejected: `Dictionary<string, AsyncOperationHandle>`, private `NativeHashMap` ownership after Vault-law recheck, and second singleton manager | Estimate: cache hit path avoids creating a second Unity handle
- [x] Task 07: BURST_TTL_EVALUATOR | DOD: `AssetTtlEvaluationJob` is scheduled as a `JobHandle` after release drain and its result is consumed on the next slow tick or through an `IsCompleted`-guarded mutation fence; no `job.Run()` and no arbitrary blocking mutation `Complete()` remain | Rejected: immediate release on boundary unload, synchronous `Run()`, and blocking acquire/release fences | Estimate: O(active handles) at 1Hz, target below 0.1 ms
- [x] Task 08: SAFE_FRAME_GARBAGE_COLLECTION | DOD: `Addressables.Release` drain is blocked unless hard-reaper blind window, mock fade-to-black, zero-delta blind frame, or VRAM panic is active | Rejected: `Resources.UnloadUnusedAssets()` and arbitrary mid-frame release | Estimate: moves release spikes out of visible frames
- [x] Task 09: THE_DEAR_LIE_MESH_SWAP | DOD: fallback cube impostor mesh is created once and exposed via `TryGetFallbackImpostorMesh()` | Rejected: blocking physics/world load until disk completes | Estimate: cold mesh alloc only
- [x] Task 10: VRAM_EVICTION_ROUTING | DOD: `VRAMPressureMonitor.VramPressureFactor >= 0.9` forces unused TTL to zero and permits emergency release outside blind frames | Rejected: unlimited retention under mobile VRAM pressure | Estimate: pressure path only
- [x] Task 11: CONTINUOUS_SCALABILITY_CACHE_SIZE | DOD: `GlobalQualityWeight` smoothsteps TTL from 10s to configurable 300s without binary quality switches | Rejected: low/high dichotomy | Estimate: one scalar curve per release arm
- [x] Task 12: ATOMIC_REFERENCE_COUNTING | DOD: `AssetTrackerAtomic` uses `Interlocked.Increment/Decrement` over native tracker memory | Rejected: C# `lock` or main-thread-only refcounts | Estimate: atomic integer cost per acquire/release
- [x] Task 13: AUP_PRECISION_IGNORE | DOD: tracker/cache/mock/telemetry DTOs contain hashes, counts, TTL, flags only; no `double3` in memory tracking structs | Rejected: storing spatial AUP in handle manager | Estimate: zero spatial precision overhead
- [x] Task 14: ASSET_BUNDLE_FRAGMENTATION_DEFRAG | DOD: bundle prefix hash refcounts inflate TTL for shared bundle assets | Rejected: repeatedly unloading small assets from the same bundle | Estimate: one prefix hash on cold load
- [x] Task 15: NARRATIVE_PINNING_LOCK | DOD: `SetHeapSanitizerPin(assetHash, pinned)` blocks TTL release for objective/narrative assets through flag bit | Rejected: distance-only eviction for critical set pieces | Estimate: flag test inside 1Hz TTL job
- [x] Task 16: ZERO_INIT_OVERHEAD_BYPASS | DOD: tracker, TTL, flags, handle-map, cache-profile, and telemetry buffers are requested from `GlobalDataVault` with `NativeArrayOptions.UninitializedMemory`; `AssetLifecycleGovernor` now stores Vault handles only and resolves `NativeArray` views as local variables | Rejected: private `new NativeArray`, private `NativeHashMap`, persistent private `NativeArray` alias fields, and default clear-memory allocation as hidden semantic dependencies | Estimate: avoids local persistent native heap fragmentation and allocator zero-fill dependence
- [x] Task 17: TELEMETRY_HEAP_RECORDER | DOD: 300-frame `AssetHeapTelemetryEntry` Vault ring tracks active handles, orphan releases, hit ratio, VRAM, TTL, leak hash; leak dumps to `Dump_MEMORY_SURGEON.bin` and `Dump_SHINOBU_67_Addressables.bin` | Rejected: managed per-frame logs and mirror-only Vault claims | Estimate: one 64B row per slow tick
- [x] Task 18: MEMORY_TUNER_EDITOR_WINDOW | DOD: `HeapSanitizerTunerWindow` shows active handles, hits/misses, TTL and VRAM panic controls, and CSV load; cache profiles are mirrored through `BufferID.AddressableHeapCacheProfiles` | Rejected: inspector-only tuning | Estimate: editor-only
- [x] Task 19: CSV_OVERRIDE_INGESTOR | DOD: `TryParseAssetCacheRules(ReadOnlySpan<char>)` parses hash/TTL/multiplier/flags without split/LINQ and overwrites native cache profiles plus Vault mirror | Rejected: runtime string split parser | Estimate: cold editor/hotload only
- [x] Task 20: LIVE_LEAK_DETECTOR_GIZMO | DOD: editor window displays giant red `LEAK SUSPECT` when any tracker refcount exceeds 50 | Rejected: waiting for profiler-only discovery | Estimate: editor repaint only

## Loop Notes

- Loop A0: Extracted active Addressables prompt by CLI after detecting duplicate `SHINOBU_67` blocks. Loaded streaming/memory/registry/telemetry mandates.
- Loop A1: Static archaeology found central `AssetLifecycleGovernor`, `AssetLoadDispatcher`, chunk direct `Addressables.LoadAssetAsync`, item prefab direct `AssetReference.LoadAssetAsync`, and no first-party runtime `Resources.Load` hits.
- Loop A2: Extended `AssetLifecycleGovernor` with native ref mirrors, `uint->ulong` handle ids, fixed Unity handle pool, adaptive TTL, blind-frame release gate, VRAM panic override, pin flags, fallback impostor mesh, CSV parser, editor facade, Vault mirror buffers, and heap telemetry dumps.
- Loop A3: Routed `WorldChunkResidencyManager` chunk prefab loads/releases and `ItemCatalog` world prefab loads/releases through the governor. Direct project scan now shows central loads in governor plus scoped fallback/boot/content-authority handles.
- Loop A4: Re-read prompt from `CURRENT_BATCH.md`; added explicit `NativeHashMap<uint, ulong>` to satisfy the mandatory off-heap handle pointer/id contract.
- Loop A5: Static verification completed: `git diff --check` only reports LF/CRLF warnings on touched files; initial CPU guard measured 81.9% then 71.3%, so build was deferred.
- Loop A6: CPU guard later measured 31.4% with no `dotnet/csc/VBCSCompiler` processes. Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal`: build succeeded with 8 pre-existing CS0649 warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, 0 errors.
- Loop A7: Ultra-polish re-read found real rot: private `NativeHashMap`/`NativeArray` ownership inside `AssetLifecycleGovernor` violated Data Vault sovereignty. Replaced it with Vault handles `AddressableHeapTrackers`, `AddressableHeapTimeToLive`, `AddressableHeapTrackerFlags`, `AddressableHeapHandleMap`, `AddressableHeapCacheProfiles`, and `AddressableHeapTelemetry`; old `_native*` maps were removed.
- Loop A8: Hardened `AssetTtlEvaluationJob` and `MockChunkLoadSpamJob` with exact Burst flags and `[NoAlias]`. Static scan: no `NativeHashMap`, no private `new NativeArray`, no old `_native*` fields in `AssetLifecycleGovernor`. CPU guard 16.7%, no compiler processes; `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` passed with 8 pre-existing CS0649 warnings, 0 errors.
- Loop A9: Re-read status/rationale and re-scanned `AssetLifecycleGovernor`. Found residual private `NativeArray` alias fields and `job.Run()` TTL execution. Removed all persistent `NativeArray` fields from the governor, converted Vault access to local resolved views, added Vault buffer locks around scheduled TTL jobs, moved TTL scheduling after release drain, and added mutation fences before writes to tracker lanes. Static scan: no `private NativeArray`, no `NativeHashMap`, no `new NativeArray`, no `job.Run()` in the Addressables sanitizer files. Build was deferred because CPU guard measured 100%, and a later guard saw active `dotnet`/`csc` compiler processes.
- Loop A10: Re-read active Addressables prompt and binary payload ledger. Found a new rot layer: the mutation fence could still call `Complete()` on an unfinished TTL job during acquire/release/pin/clear. Replaced it with `TryPrepareTrackerMutation()`: if the TTL job is still running, cache-hit acquire updates the managed `AssetRecord` and sets `_nativeRefSyncRequired`, release decrements managed state without racing native lanes, and native refcounts/TTL flags are reconciled after the job completes. Static scan: no forbidden sanitizer hot-path patterns; `Complete()` remains only teardown or after `IsCompleted`. Build deferred again because CPU samples hit 100%, 100%, 98.1%, 84.8% with active `dotnet`/`csc`.
- Loop A11: Managed heap capacity recheck: `_registry` and `_pendingRelease` still cold-allocated at 512 even though `maxTrackedAddressableHandles` can scale to 8192. Raised both cold allocations to `MaxTrackedAddressableCapacity`, added `MaxAddressableHandleMapCapacity`, replaced hard-coded 8192/16384 clamps, and forced `maxRegistryCapacity >= maxTrackedAddressableHandles` during cold storage setup. DOD: no managed container growth should occur for any tracked handle count inside the configured sanitizer ceiling. Static scan remains clean for forbidden sanitizer patterns; `git diff --check` reports only LF/CRLF warnings. Build was not launched after this patch because CPU guard sampled 100%, 100%, 100%, 100% even with no compiler process.
