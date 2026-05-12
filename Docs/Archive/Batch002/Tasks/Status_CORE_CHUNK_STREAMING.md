# Status_CORE_CHUNK_STREAMING

PROMPT IDENTIFIED: CORE_CHUNK_STREAMING | DOMAIN: CORE & MEMORY INFRASTRUCTURE / World Chunk Residency & Addressables | TASK COUNT: 15
STATUS: PENDING VERIFICATION

Relevant mandates loaded:
- STRM_World_Streaming_Residency_Chunk_Management.txt
- STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- STRM_Async_Standard.txt
- STRM_Async_Asset_Upload_Texture_Settings.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Loop 0 - Prompt Extraction / Hygiene
- [x] Extract CORE_CHUNK_STREAMING XML prompt | DOD: CLI regex extraction from Docs/Tasks/CURRENT_BATCH.md. Rejected: relying on IDE open tabs. Estimate: 35 us.
- [x] Verify status/rationale hygiene | DOD: missing files confirmed before creation. Rejected: reusing stale batch files. Estimate: 20 us.

## Titanium Tasks
- [x] Task 1 - SPATIAL CHUNK HASHING | DOD: `BuildChunkId(in AbsoluteUniversePosition, int)` hashes AUP chunk coordinates into a non-negative 64-bit FNV-1a id and stores states in `NativeParallelHashMap<long, ChunkState>`. Rejected: Vector3/Transform-space IDs and managed Dictionary hot-path lookups. Estimate: 18 us saved per 512-chunk scan.
- [x] Task 2 - RADIUS-BASED STREAMING JOB | DOD: `RadiusBasedStreamingJob : IJobParallelFor` compares Player AUP and chunk-center AUP with `math.distancesq`, outputs `NativeList<long>` load/unload lists via `ParallelWriter`. Rejected: main-thread radius scan and sqrt distance. Estimate: 420 us saved at 512 chunks on i3-class CPU.
- [x] Task 3 - HYSTERESIS DEADZONE | DOD: load radius = 500m default, unload radius = 600m default with forced clamp above load radius. Rejected: single threshold that flickers at boundaries. Estimate: 80 us saved from avoided churn spikes; visual stability gain is primary.
- [x] Task 4 - ASYNC ADDRESSABLES QUEUE | DOD: `NativeQueue<ChunkLoadRequest>` and `_pendingLoadRequestCount` gate dispatch to one load per frame. Rejected: `Addressables.InstantiateAsync` burst fan-out. Estimate: 600-1200 us spike avoided under dense queue.
- [x] Task 5 - PREFAB POOL WARMUP | DOD: chunk definitions prewarm `prefabDependencies` and loaded GameObject results through `ObjectPoolManager.Instance.Warmup`. Rejected: runtime raw instantiation on chunk load. Estimate: 300 us+ avoided per prefab family.
- [x] Task 6 - TIME-SLICED INSTANTIATION | DOD: `ActivateChunkAsync` uses Unity `Awaitable` and `AwaitableDebtMonitor.NextFrameAsync`, max 5 pooled spawns/frame. Rejected: coroutine state machines and `Task.Run` Unity access. Estimate: 300-900 us activation spike flattening.
- [x] Task 7 - EXPLICIT ASSET RELEASE | DOD: Addressables `AsyncOperationHandle<GameObject>` stored per definition and released on chunk unload; additive scenes unload explicitly. Rejected: orphan handles and `Resources.UnloadUnusedAssets`. Estimate: memory retention reduction pending profiler proof.
- [x] Task 8 - THE UNLOAD BAN | DOD: removed first-party `Resources.UnloadUnusedAssets()` from `Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs` and `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`; scan of `Assets/_Project` returns zero hits. Rejected: leaving editor calls as harmless because mandate says project ban. Estimate: avoids 40ms+ accidental editor/runtime path stalls if copied forward.
- [x] Task 9 - GPU UPLOAD THROTTLING | DOD: runtime tier sets `QualitySettings.asyncUploadBufferSize`, `asyncUploadTimeSlice`, and persistent buffer once per tier. Rejected: per-frame setting churn. Estimate: upload-buffer resize spikes reduced; measured proof absent.
- [x] Task 10 - AUP ORIGIN SHIFT SYNC | DOD: drains `GlobalSignals.TryDequeueAupShift`, records shift frame id, forces immediate residency reevaluation. Rejected: polling only on slow cadence after shift. Estimate: up to 500ms stale-center delay removed.
- [x] Task 11 - LOD CROSS-FADE MASK | DOD: global shader property `_ChunkFadeMask` ramps over 2 seconds after promotion. Rejected: hard chunk pop. Estimate: CPU neutral; performance buys visual continuity.
- [x] Task 12 - MEMORY BUDGET WATCHDOG | DOD: added `RuntimeWatchdog.GetAvailableMemory()` and halts chunk load below 500MB headroom while publishing `GlobalTelemetryBus.PublishMemoryBreachEvent`. Rejected: separate streaming-only memory authority. Estimate: prevents critical allocation path under pressure.
- [x] Task 13 - SUB-SCENE LOADING | DOD: structural chunks use `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` with `allowSceneActivation=false` until the activation gate. Rejected: direct scene activation on load dispatch. Estimate: scene activation spikes moved behind throttle; measured proof absent.
- [x] Task 14 - RECONNAISSANCE PROTOCOL | DOD: generated `Docs/AgentLogs/RECON_CORE_CHUNK_STREAMING.md` with 412 `Instantiate()`/`Destroy()` matches outside `ObjectPoolManager.cs`. Rejected: chat-only report. Estimate: no direct runtime savings; exposes debt.
- [x] Task 15 - OMEGA COMPILE CHECK [BLOCKED BY DEPENDENCY] | DOD: ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` three times during core work and once after Polish. Current blocker is external: `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`. Earlier external blockers for tether and transport were present during looped verification. Rejected: patching unrelated survival ownership. Estimate: 0 us claimed.

## Verification
- [x] Compile pass 1 after Tasks 1-5 [BLOCKED BY DEPENDENCY] | First build failed outside this slice: survival, AUP pre-shift, tether telemetry, boid duplicate helpers.
- [x] Compile pass 2 after Tasks 6-10 [BLOCKED BY DEPENDENCY] | Second build included `WorldChunkResidencyManager.cs`; blockers reduced but remained outside this slice.
- [x] Compile pass 3 after Tasks 11-15 [BLOCKED BY DEPENDENCY] | Third build failed on survival scalar result, Manta scooter interface, tether telemetry.
- [x] Strict self-review loop 1 | Forbidden-pattern scan on new manager: no Update/FixedUpdate/LateUpdate, coroutine, LINQ, Task.Run, Addressables.InstantiateAsync, Resources.Load/Unload.
- [x] Strict self-review loop 2 | Addressables polling review found repeated promotion risk; patched resident skip and failed-handle release.
- [x] Strict self-review loop 3 | Native/job lifetime review: job Complete only when `IsCompleted` in Tick or teardown; persistent NativeCollections disposed in teardown.
- [x] Strict self-review loop 4 | Lifecycle review moved chunk release to `OnDisable`; `OnDestroy` now native teardown only.
- [x] Strict self-review loop 5 | Prompt re-extracted after implementation; `git diff --check` returned no whitespace errors, only CRLF warnings on pre-existing files.
- [x] OMEGA POLISH LOOP | DOD: extracted `<POLISH_MANDATE id="OMEGA_POLISH">` after core completion, replaced fade division with reciprocal multiply, normalized non-ASCII code comments to ASCII, reran zero-GC/string/Task.Run/Addressables/Unload scans on touched runtime files, and reran `dotnet build`. Rejected: marking `VERIFIED MASTER GRADE` while compile is blocked by `SurvivalPhysiologyScalarResult`. Estimate: 0.02 us saved per fade update; visual behavior unchanged.

## Honest R&D Continuation - 2026-05-12
- [x] Prompt re-extraction | DOD: CLI line extraction from `Docs/Tasks/CURRENT_BATCH.md` lines 233-262 captured only `<AGENT_PROMPT id="CORE_CHUNK_STREAMING">`. Rejected: root `CURRENT_BATCH.md` assumption after it tested missing. Estimate: 35 us.
- [x] Compile-medic include audit | DOD: `Hecton8.Core.csproj` currently contains `SaveDeltaCompression.cs`, `SurvivalPhysiologyScalarJob.cs`, and `SurvivalStatusMasks.cs`; build progressed past prior `SaveVoxelDeltaRun8` / `SurvivalPhysiologyScalarResult` include walls. Rejected: regenerating ignored `*.csproj` wholesale while other agents edit domains. Estimate: 0 us runtime.
- [x] Additive scene activation/unload hardening | DOD: `WorldChunkResidencyManager` now waits for `AsyncOperation.isDone` before marking additive scenes loaded and defers unload through `_additiveSceneUnloadWhenLoaded` when eviction arrives during activation. Rejected: marking loaded immediately after `allowSceneActivation=true`, which can lose the unload path. Estimate: 0 us steady-state; prevents structural chunk residency leak/race.
- [x] Focused zero-GC/hot-path scan | DOD: `rg` scan on touched runtime files found no `foreach`, string formatting/interpolation, `.ToString()`, `Task.Run`, `Addressables.InstantiateAsync`, `Resources.UnloadUnusedAssets`, managed List/Dictionary construction, LINQ, or Unity `Update`/`FixedUpdate`/`LateUpdate`. Rejected: relying on visual review. Estimate: 0 us claimed.
- [x] Build after R&D [BLOCKED BY DEPENDENCY] | DOD: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` executed after the additive-scene fix. Result: 73 errors, 3 warnings, all currently outside `WorldChunkResidencyManager.cs`. Dominant missing authorities: `HectonPersistentPathPolicy`, `HectonNativeBridge` / `HectonNativeLibrary`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy` / `HectonThreadRole`, `SteamDeckInputPal`, `HapticWaveformLibrary`, `HardwareTierDetector`. Rejected: patching Save/Core/Audio/Input/Optimization ownership from the streaming slice. Estimate: 0 us claimed.

## Honest R&D Continuation - NativeCollection Race Gate - 2026-05-12
- [x] Chunk-state mutation gate | DOD: `_chunkStates` is no longer read or written by Tick-side load dispatch, addressable polling, telemetry scan, `IsResident`, `RequestLoad`, or `RequestEvict` while `_residencyJobScheduled` is true. Rejected: calling `JobHandle.Complete()` mid-Tick to make the race disappear. Estimate: 0 us saved; removes undefined NativeCollection access.
- [x] Deferred explicit request lanes | DOD: explicit `RequestLoad` while the residency job is alive can queue a load without reading `_chunkStates`; explicit `RequestEvict` is stored in `_deferredEvictChunkIds` and drained after the job fence. Rejected: silently dropping external requests. Estimate: 0 us steady-state; prevents a missed load/evict command.
- [x] Telemetry cached snapshot | DOD: `WriteTelemetrySample` uses cached resident/loading/evicting counts and state hash while the job owns `_chunkStates`, then refreshes the snapshot only when the map is writable. Rejected: black-box scanning a live job input. Estimate: removes up to one 512-entry hash scan during active job frames.
- [x] Forced reschedule ordering | DOD: `_forceResidencyEvaluation` now schedules after deferred evict/load handling and telemetry, not inside `ProcessResidencyResults`. Rejected: scheduling a new job before applying deferred state mutations. Estimate: removes one stale-evaluation cycle under explicit request pressure.
- [x] Teardown fence | DOD: `OnDisable` now uses `CompleteResidencyJobForTeardown()` before releasing chunk handles. Rejected: leaving a residency job alive after unregistering the manager. Estimate: 0 us runtime; avoids teardown race.
- [x] Build after native race gate [BLOCKED BY DEPENDENCY] | DOD: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:quiet -clp:ErrorsOnly` wrote `Docs/AgentLogs/Build_CORE_CHUNK_STREAMING_errors3.txt`. Result: 76 external errors, 3 warnings, no `WorldChunkResidencyManager.cs` errors. Rejected: editing Save/Core/Input/Audio/Voxel owners from this slice. Estimate: 0 us claimed.
