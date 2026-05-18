# SHINOBU_67 Final Report - 2026-05-18

What was wrong -> Modular base placement used `Physics.OverlapBoxNonAlloc` in `PlayerBuilder.UpdateTerrainSdfPlacementState`, so base preview validity depended on collider broadphase instead of terrain SDF. That is why non-collider mountains/caves could accept clipping builds. Prompt also requested inventory deduction and SIP preflight, but production source has no real `CraftingRequestSignal`; only mock crafting signal exists.

What was done -> Added `Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs` with 64B `ConstructionRequestDTO`, explicit 32B validation/bounds/SIP/occupancy DTOs, Burst grid validation, logistics splice scratch job, deconstruction connectivity job, span CSV bounds parser, continuous `GlobalQualityWeight` probe budget, and 300-entry telemetry ring with `Dump_SHINOBU_67.bin`. Replaced the construction `OverlapBox` path in `PlayerBuilder.cs` with AUP-root-local grid snap plus math/SDF validation and published voxel SDF probes. Added `WfcBuilderTunerWindow` for grid/bounds/clearance tuning and SceneView rejection gizmos. Added DataVault IDs `ConstructionBuilderTuning`, `ConstructionBuilderTelemetry`, and `ConstructionBuilderBounds`. Resource deduction remains on the existing production transaction seam: `HabitatConstructionManager.HasBuildResources` / `ConsumeBuildResources`; no mock crafting lane was promoted.

Cinematic Cheats used -> Collider truth replaced with cheap local AABB probes against SDF distance. Continuous quality scales terrain probes from 1 to 9 instead of toggling low/high modes. SIP is scalar preflight pressure ratio until HullIntegrity exports a stable first-party ledger surface. Preview diagnostics are editor gizmos and DTO state, not runtime debug prefabs.

Exact Microseconds saved -> Measured profiler delta: 0 us, because no Unity profiler capture was available in this CLI session. Static budget delta: one `Physics.OverlapBoxNonAlloc` broadphase per build-drag validation removed, projected 8-35 us on i3/MX350 depending on collider density. New finite validator path is 1-9 scalar SDF probes plus one grid hash and is expected under 10 us, pending profiler proof.

Verification -> `rg` confirms no `Physics.OverlapBox` / `OverlapBoxNonAlloc` remains in SHINOBU_67 construction files. `git diff --check` reports no SHINOBU_67 whitespace errors; only a Git LF/CRLF warning on already-dirty `H8Memory.cs`. Earlier `dotnet build Hecton8.Core.csproj` and `Hecton8.Editor.csproj` reached clean output. Final post-audit `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` is blocked by external `Assets/_Project/Scripts/Economy/EconomyRuntimeInstaller.cs` missing `TradeMarauderDirector`; SHINOBU_67 files emit no compiler errors before that wall.

Blocked / Not Owned -> BRG holographic preview remains blocked by render owner. Acoustic clunk and flora clearing broadcasts were not invented because no production construction-owned lanes were found. Creating fake single-use signals would violate signal segregation and cross-domain ownership.

# SHINOBU_67 Recheck Report - 2026-05-18

What was wrong -> Repeated prompt forced a second pass. Production build path already checked and consumed inventory with rollback, but `DebugDeployActiveBuildable` spawned first and ignored `ConsumeResources` failure. That left a dev-path free-build race if inventory changed between precheck and consume.

What was done -> Re-extracted `<AGENT_PROMPT id="SHINOBU_67">` via CLI. Re-scanned inventory contracts: production `CraftingRequestSignal` still does not exist; only `MockCraftingRequestSignal` exists. Patched `DebugDeployActiveBuildable` to destroy the spawned module and return false when `ConsumeResources` fails. Added explicit XML self-audit at `Docs/AgentLogs/SelfAudit_SHINOBU_67.xml`.

Cinematic Cheats used -> No new simulation. The validator remains AUP-local grid/SDF math with continuous 1-9 probe scaling.

Exact Microseconds saved -> Runtime hot path unchanged. Debug rollback adds no frame-drag cost; it only runs on debug deploy click failure.

Verification -> Scoped source scans still show no `Physics.OverlapBox`/`OverlapBoxNonAlloc` in SHINOBU_67 construction files. Full Core build remains externally blocked; the latest blocker is `World/VolcanicUpdraftDirector.cs(1451,58)` referencing missing `VolcanicUpdraftVault.SafeNormalize`, not SHINOBU_67 symbols.

# SHINOBU_67 Asset Hygiene Addendum - 2026-05-18

What was wrong -> New Unity source assets lacked `.meta` files, leaving GUID generation to the editor.

What was done -> Added `ModularBaseConstructionValidator.cs.meta` and `WfcBuilderTunerWindow.cs.meta` with explicit GUIDs.

Cinematic Cheats used -> None.

Exact Microseconds saved -> 0 us runtime. This is repository hygiene, not frame work.

Verification -> `git diff --check` reports only LF/CRLF warnings on changed C# files. Scoped scan for `OverlapBox`, `OverlapBoxNonAlloc`, `Physics.OverlapBox`, and `Instantiate(` in SHINOBU_67 files returns no matches.

# SHINOBU_67 Ultra Polish Report - 2026-05-18

What was wrong -> The first implementation still had three pieces of technical rot: telemetry/bounds Vault integration was too report-driven, the CSV parser did not have a direct Vault ingestion path, and low-quality collision LOD used `ceil(lerp(1,9,q))`, which gave two probes at `GlobalQualityWeight=0.1` instead of the mandated center-only collapse. The self-audit also lacked explicit struct offset proof and dependency caveats.

What was done -> Re-read the active construction `SHINOBU_67` batch prompt, the rationale log, and the binary payload ledger. Added `ConstructionBuilderTelemetry` and `ConstructionBuilderBounds` Vault resolution, `GenerateEmergencyMockBounds()` fallback hydration, direct `TryParseModuleBoundsCsvToVault()` ingestion, PlayerBuilder telemetry writes per terrain validation, and a `smoothstep(0.3,1.0)` probe curve that produces exactly one SDF sample below weight 0.3 and nine at weight 1.0. Expanded `Docs/AgentLogs/SelfAudit_SHINOBU_67.xml` with 20-task reconciliation, byte offsets, Vault handles, dependency graph, compile guard, and Dear Lie Big-O.

Cinematic Cheats used -> Runtime construction collision remains strict-grid AABB plus SDF distance samples. No mesh intersection, no collider broadphase, no preview instantiation in the validator. Low-tier collapses to a single center SDF sample; higher quality buys more evidence through AABB corner probes and editor diagnostics.

Exact Microseconds saved -> New measured profiler delta: 0 us, because the user explicitly forbade launching build/profiler work unless needed. Static delta: one SDF sample saved at `GlobalQualityWeight=0.1` compared to the previous two-probe curve; up to eight SDF samples bypassed versus ultra mode. The original collider broadphase removal projection remains 8-35 us per drag validation on i3/MX350, pending Unity profiler proof.

Verification -> No `dotnet build` was launched during this ultra-polish pass by explicit user order. Scoped scan over `PlayerBuilder.cs`, `ModularBaseConstructionValidator.cs`, and `WfcBuilderTunerWindow.cs` returns no `Physics.OverlapBox`, `OverlapBoxNonAlloc`, or `Instantiate(` hits. `git diff --check` reports only Git LF/CRLF warnings in `H8Memory.cs` and `PlayerBuilder.cs`.

Blocked / Not Owned -> BRG holographic preview, acoustic clunk signal, and flora clearing broadcast remain external-contract failures. I did not invent fake signals or direct sibling runtime dependencies. The latest known full-build wall remains external `World/VolcanicUpdraftDirector.cs(1451,58)` missing `VolcanicUpdraftVault.SafeNormalize`; it was not rechecked after the user ordered no build.

# SHINOBU_67 AUP SDF Precision Recheck - 2026-05-18

What was wrong -> The construction mountain check still had a precision leak. `PlayerBuilder.TryFindVoxelSdfIntersection` built a `double3` probe from `RootAUP + localProbe`, then cast it to absolute `float3` before sampling `HectonVoxelVolume.GetSDFDensity`. That violates the AUP rule and can quantize probe positions near 100km world offsets.

What was done -> Added `HectonVoxelVolume.GetSDFDensity(double3, out float)` and `HectonVoxelVolume.TrySampleRuntimeSdfDensity(Vector3, out float)`. Changed construction probing to keep AUP in `double3` until `HectonFloatingOrigin.ToRuntimePosition(double3)` subtracts the committed origin offset, then sample the already-local runtime point. Existing legacy `float3` SDF API remains for callers not touched by SHINOBU_67.

Cinematic Cheats used -> Still AABB/SDF point evidence, not mesh collision and not Unity Physics. This pass did not add a heavier simulation; it removed a precision fault from the existing mathematical fake.

Exact Microseconds saved -> 0 us expected. Probe count is unchanged. The saved cost is correctness: no large-coordinate float quantization before terrain rejection.

Verification -> Scoped scan over `PlayerBuilder.cs` and `ModularBaseConstructionValidator.cs` returns no `Physics.OverlapBox`, `OverlapBoxNonAlloc`, `Instantiate(`, `probeAupFloat`, or `new float3((float)probeAup` hits. `git diff --check` reports only Git LF/CRLF warnings in `HectonVoxelVolume.cs` and `PlayerBuilder.cs`. No `dotnet build` was launched by user order.

# SHINOBU_67 Grid Occupancy Recheck - 2026-05-18

What was wrong -> The lower-level Burst job supported occupancy, but live preview was still calling `ValidatePlacementNoOccupancy`. That meant SHINOBU_67's own runtime path did not explicitly reject a grid coordinate already occupied by a registered module.

What was done -> Added `PlayerBuilder.TryFindOccupiedConstructionGridCell`. It scans `ConstructionManager.SpawnedModules` with a plain `for` loop, converts each module runtime position to AUP, snaps it relative to the same RootAUP/grid size, and flags `OccupiedGridCell` / `GRID OCCUPIED` when the candidate coordinate is already taken.

Cinematic Cheats used -> Occupancy remains integer grid truth, not collider truth. No Unity Physics and no per-frame `NativeParallelMultiHashMap` allocation were added.

Exact Microseconds saved -> Measured proof absent. Compared to a collider broadphase, the fallback is O(moduleCount) integer math; for 500 modules it is hundreds of compares, not scene physics traversal. Future Vault occupancy mirror should collapse this to O(1).

Verification -> Scoped construction scan still returns no `Physics.OverlapBox`, `OverlapBoxNonAlloc`, `Instantiate(`, `probeAupFloat`, or absolute `probeAup` float cast hits. `dotnet build` was not launched by user order.

# SHINOBU_67 Black-Box Hash Recheck - 2026-05-18

What was wrong -> `ResultHash` was computed inside the core validator before PlayerBuilder added live occupancy and published voxel-SDF evidence. Telemetry could therefore store a hash that did not match the final rejection flags.

What was done -> Added `ModularBaseConstructionValidator.ApplyFailureFlags()` and routed occupied-cell plus terrain-SDF rejections through it. The helper updates failure flags, validity bytes, occupied cell hash, min SDF distance, and result hash in one place.

Cinematic Cheats used -> No new simulation. This preserves the existing AABB/SDF fake and makes the black-box record faithful.

Exact Microseconds saved -> 0 us. This is a correctness fix; it adds one small deterministic hash recompute only when a live failure is detected.

Verification -> Source scan confirms PlayerBuilder no longer hand-patches `FailureFlags |=` for SHINOBU_67 live failures. Build was not launched by user order.

# SHINOBU_67 Signal Lane Recheck - 2026-05-18

What was wrong -> Task 08/14/15 were still too soft. The repo had a construction preview batch, but PlayerBuilder was not publishing a validator-owned unmanaged preview packet to it. Acoustic clunk was only local `AudioClip` playback, and flora clearing had an existing module-enable bridge but no construction-owned AABB signal.

What was done -> Added `ConstructionPreviewSignal` (96B) and `FloraExclusionSignal` (80B) in `Assets/_Project/Scripts/Construction/ConstructionSignals.cs`. PlayerBuilder now configures both lanes, emits preview packets after mathematical validation, emits flora AABB packets only after inventory-backed build commit, and publishes a metal-stress `AcousticPingSignal` for the build clunk. `HectonBlueprintPreviewBatch` now consumes `ConstructionPreviewSignal`, uses explicit 64B preview instance layout, has exact Burst flags, and marks job arrays `[NoAlias]`.

H-PHI correction -> The preview batch no longer owns persistent native buffers with `Allocator.Persistent`. Its write/build/matrix arrays resolve through `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices` Vault handles. Superseding 2026-05-19 recheck removed the private alias fields entirely; the managed `Matrix4x4[]` mirror remains a cold fallback requirement for `Graphics.DrawMeshInstanced`, not validator hot-path state.

Cinematic Cheats used -> Preview stays an AABB wire hologram, not mesh intersection or semi-transparent prefab truth. Acoustic weight is a compact metal-stress signal, not live structural acoustics. Flora clearing is AABB exclusion, not per-plant CPU iteration from PlayerBuilder.

Exact Microseconds saved -> Measured profiler delta remains 0 us because no profiler/build was run. Static delta: one signal enqueue per preview frame and one Burst matrix job for the fallback preview; one acoustic signal and one flora signal per committed module. No physics broadphase or new per-frame managed allocation was added.

Verification -> Scoped scans over PlayerBuilder, ConstructionSignals, HectonBlueprintPreviewBatch, and ModularBaseConstructionValidator find no `Physics.OverlapBox`, `OverlapBoxNonAlloc`, `Instantiate(`, direct sibling `AcousticEchoTap`, or forbidden absolute probe float cast. `git diff --check` reports only LF/CRLF warnings in changed C# files. `dotnet build` was not launched by user order.

# SHINOBU_67 Addressables Heap Sanitizer Report - 2026-05-18

What was wrong -> Active batch prompt collision existed: the old `SHINOBU_67` construction logs contaminated status/rationale, while the user requested the later `ADDRESSABLES_HEAP_SANITIZER` block. Runtime chunk prefab loads in `WorldChunkResidencyManager` and world prefab loads in `ItemCatalog` created local `AsyncOperationHandle` ownership and released immediately, which can duplicate handles and churn bundles when the player oscillates around chunk borders.

What was done -> Extended existing `AssetLifecycleGovernor` instead of adding a second singleton. Added `AssetTrackerDTO` 16B explicit layout, `NativeHashMap<uint,int>` refcounts, `NativeHashMap<uint,int>` slots, `NativeHashMap<uint,ulong>` handle ids, fixed `AsyncOperationHandle[]` pool, atomic refcount mutation, 1Hz Burst TTL evaluator, blind-frame release gate, VRAM panic override, bundle-prefix TTL inflation, pin flag facade, fallback impostor mesh, 300-frame heap telemetry ring, leak dumps, CSV cache rule parser, DataVault mirrors `AddressableHeapCacheProfiles` / `AddressableHeapTelemetry`, and `HeapSanitizerTunerWindow`.

Cinematic Cheats used -> No simulated disk miracle. The cheat is retention: cache and delay release instead of unloading immediately, then perform actual `Addressables.Release()` only during hard-reaper visual static, mock fade-to-black, zero-delta blind frame, or VRAM panic. The second cheat is the fallback cube impostor mesh for still-loading assets so gameplay does not wait on disk.

Exact Microseconds saved -> Measured profiler delta is not available. Static expected saving: every sanitizer cache hit avoids creating one duplicate Addressables handle and may avoid one bundle unload/reload cycle. Release spike avoidance is schedule movement, not raw CPU reduction; target is hiding the 15 ms-class release cost inside blind frames. Atomic ref mutation remains one interlocked integer operation per acquire/release. Telemetry cost is one 64B row per slow tick.

Verification -> `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` succeeded after CPU guard dropped to 31.4% and no compiler processes were present. Build result: 0 errors, 8 existing CS0649 warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`. `git diff --check` reports only LF/CRLF warnings on touched files. Static scan confirms central Addressables loads are in `AssetLifecycleGovernor`; direct releases left in touched chunk/item files are fallback or cache-clear paths.

# SHINOBU_67 Addressables Vault Polish Report - 2026-05-19

What was wrong -> The previous Addressables implementation still owned private persistent `NativeHashMap` and `NativeArray` containers inside `AssetLifecycleGovernor`. That satisfied the first XML wording but violated the stronger Data Vault sovereignty rule and left local native allocation surfaces that can fragment memory across scene lifetimes. Burst jobs also used bare `[BurstCompile]` and no `[NoAlias]`.

What was done -> Replaced `_nativeRefCounts`, `_nativeHandleSlots`, `_nativeHandlePointers`, and `_bundlePrefixRefCounts` with a Vault-backed fixed open-address table: `BufferID.AddressableHeapHandleMap` storing 64B `AssetHandleMapEntryDTO` rows (`AssetHash -> slot/refcount/ulong handle id/bundle prefix`). Moved tracker, TTL, flags, cache profile, and telemetry storage behind Vault handles: `AddressableHeapTrackers`, `AddressableHeapTimeToLive`, `AddressableHeapTrackerFlags`, `AddressableHeapHandleMap`, `AddressableHeapCacheProfiles`, and `AddressableHeapTelemetry`. `AssetLifecycleGovernor` now holds aliases only and does not dispose Vault storage. The fixed `AsyncOperationHandle[]` pool remains because Unity handles are managed engine structs and cannot be blittable Vault payloads.

Cinematic Cheats used -> The Dear Lie stays predictive retention: do not force disk to be faster; keep recently discarded chunk assets resident through a continuous TTL curve and release only in blind frames or VRAM panic. Shared bundle detection now lives in the Vault table and inflates TTL without a second native map.

Exact Microseconds saved -> Profiler delta remains unmeasured. Static savings are architectural: five private persistent native owners removed from `AssetLifecycleGovernor`; cache-hit path remains integer hash probe plus `Interlocked` refcount instead of new Unity handle creation. The open-address table can cost O(probe count), bounded by fixed capacity; bundle sharing scan is cold acquire/release work, not per-frame.

Verification -> Static scan found no `NativeHashMap`, no private `new NativeArray`, no Sentinel native registration/unregistration, and no old `_native*` fields in `AssetLifecycleGovernor`. `git diff --check` reported only LF/CRLF warnings in touched files. CPU guard measured 16.7% with no `dotnet/csc/VBCSCompiler`; `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` passed: 0 errors, 8 pre-existing CS0649 warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`.

# SHINOBU_67 Addressables Alias Purge Report - 2026-05-19

What was wrong -> The prior Vault polish still left private persistent `NativeArray<>` alias fields in `AssetLifecycleGovernor` and a synchronous `job.Run()` TTL evaluator. That is not strict H-PHI. The class shape still looked like it owned hot arrays, and the TTL kernel had no visible `JobHandle` dependency.

What was done -> Removed all persistent `NativeArray<>` fields from the governor. Tracker, TTL, flags, handle-map, cache-profile, and telemetry access now resolves local Vault views from `VaultBufferHandle<T>` per method. The TTL evaluator now schedules as a `JobHandle` after release draining, locks its three Vault lanes while the job owns pointers, consumes completed results on the next slow tick, and uses an explicit mutation fence before any write to those lanes.

Cinematic Cheats used -> No heavier simulation was added. The existing cheat remains predictive retention plus fallback impostor mesh: cache assets, delay real `Addressables.Release()`, and hide release under blind-frame/static-glitch or VRAM-panic windows.

Exact Microseconds saved -> Measured profiler delta is unavailable. Static savings: zero private native-array owner fields in the MonoBehaviour, no `job.Run()` slow-tick stall, and no hidden local native storage to fragment. Any forced mutation fence is dependency-specific and should be rare because the TTL job is scheduled after release drain.

Verification -> Static scan found no `private NativeArray`, no `NativeHashMap`, no `new NativeArray`, no `job.Run()`, no LINQ, no `foreach`, no `string.Format`, no `Resources.UnloadUnusedAssets`, and no `UnityEngine.Random` in the Addressables sanitizer files. `git diff --check` reports only LF/CRLF warnings. `dotnet build` was deferred: CPU guard measured 100%, and a later guard saw active `dotnet`/`csc` compiler processes.

# SHINOBU_67 Addressables Nonblocking Fence Report - 2026-05-19

What was wrong -> The scheduled TTL job was no longer `Run()`, but normal mutations could still force `JobHandle.Complete()` before the job was finished. That protects memory but can create the exact frame hitch this sanitizer is supposed to prevent.

What was done -> Replaced the blocking mutation fence with an `IsCompleted`-guarded path. Cache-hit acquire can update the managed `AssetRecord` and return the existing `AsyncOperationHandle` while native tracker sync is deferred. Release decrements managed state and sets `_nativeRefSyncRequired` instead of racing the job. `SyncNativeRefCountsFromRegistry()` reconciles native refcounts, TTL values, handle-map refcounts, and releasable flags after the TTL job completes.

Cinematic Cheats used -> Still predictive retention. Cold misses during an active TTL job do not create unmanaged duplicate handles; they wait/retry instead of pretending disk work is free.

Exact Microseconds saved -> Profiler measurement unavailable. Static saving is removal of an unbounded main-thread `Complete()` wait from acquire/release/pin/clear. Deferred cache-hit path costs managed record mutation now and one fixed native sync scan later.

Verification -> Static scan found no forbidden sanitizer hot-path patterns and no queue `Contains()` loop. `Complete()` remains only teardown or after an explicit `IsCompleted` guard. `dotnet build` was deferred because CPU samples hit 100%, 100%, 98.1%, and 84.8% with active `dotnet`/`csc`.

# SHINOBU_67 Modular Preview H-PHI Recheck - 2026-05-19

What was wrong -> Active user request is modular construction validation, not the later Addressables prompt contaminating this file. The construction preview fallback still kept private `NativeArray` alias fields after the Vault migration, so the class shape still violated the literal H-PHI rule. The scheduled matrix job also held Vault-backed pointers without explicit Vault buffer locks. The collision LOD curve lacked an explicit `math.step` term and SIP pressure division was not written in the mandated guarded reciprocal form.

What was done -> Removed `_writeInstances`, `_buildInstances`, and `_matrices` from `HectonBlueprintPreviewBatch`. The batch now stores only `VaultBufferHandle<T>` fields, resolves `NativeArray` views as local variables, locks `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices` while `BuildPreviewMatricesJob` owns pointers, and releases those locks after nonblocking completion or teardown force-completion. `ResolveProbeBudget()` now uses `math.step(0.3,q) * math.smoothstep(0.3,1,q)` into `math.lerp(1,9,curve)`. SIP pressure warning now uses `math.rcp(math.max(projectedSip, 0.0001f))`.

Cinematic Cheats used -> Preview remains an AABB hologram emitted through `ConstructionPreviewSignal`, not a prefab clone or mesh intersection. Terrain truth remains direct SDF sampling with 1-9 probes from continuous quality, not `Physics.OverlapBox`.

Exact Microseconds saved -> Profiler delta is not measured because the user ordered no build unless needed. Static savings: zero private native-array field surfaces in the preview batch, zero Vault relocation window while the matrix job owns pointers, one-probe collision path below quality 0.3, and no collider broadphase. Prior broadphase removal estimate remains 8-35 us per drag validation on i3/MX350, pending Unity profiler proof.

Verification -> No `dotnet build` launched in this pass. Scoped scan over SHINOBU_67 construction files found no `private NativeArray`, no `Allocator.Persistent`, no `Physics.OverlapBox`, no `OverlapBoxNonAlloc`, no `Instantiate(`, and no `job.Run()`. `git diff --check` reports only LF/CRLF warnings.

# SHINOBU_67 Addressables Managed Capacity Seal - 2026-05-19

What was wrong -> The Addressables sanitizer had a remaining managed heap resize vector. `_registry` and `_pendingRelease` were initialized at 512 entries while the configured fixed handle table supports 8192 tracked handles. Under heavy chunk churn, the sanitizer could rehash the dictionary or grow the queue during gameplay.

What was done -> Added explicit capacity constants for the managed bridge and Vault handle map. `_registry` and `_pendingRelease` now pre-size to `MaxTrackedAddressableCapacity` at cold construction. `Awake()` and `EnsureNativeHandleStorage()` force `maxRegistryCapacity` to cover the active handle slot count, and hard-coded 8192/16384 clamps were replaced by named constants.

Cinematic Cheats used -> No new physical simulation. The cheat remains predictive caching plus blind-frame release. This patch only removes a heap growth trap in that retention manager.

Exact Microseconds saved -> Profiler proof unavailable. Static saving is removal of dictionary/queue growth and dictionary rehash from the gameplay streaming path inside the 8192-handle ceiling. The trade is a deliberate cold boot managed allocation sized for worst-case sanitizer capacity.

Verification -> Static scan found no `private NativeArray`, `NativeHashMap`, `new NativeArray`, `job.Run()`, LINQ, `foreach`, `string.Format`, `Resources.UnloadUnusedAssets`, `UnityEngine.Random`, `CompleteTtlEvaluationForMutation`, or queue `Contains()` in sanitizer files. `git diff --check` reports only LF/CRLF warnings. Build was deferred after this patch because CPU guard sampled 100%, 100%, 100%, 100% with no compiler process.
