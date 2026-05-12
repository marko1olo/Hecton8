# LOG_CORE_CHUNK_STREAMING

## 2026-05-12 - World Chunk Residency And Addressables
PROMPT IDENTIFIED: CORE_CHUNK_STREAMING | DOMAIN: CORE & MEMORY INFRASTRUCTURE / World Chunk Residency & Addressables | TASK COUNT: 15
STATUS: PENDING VERIFICATION

What was wrong:
- Chunk residency had no dedicated AUP-keyed native owner for load/unload decisions.
- The prompt identified `Addressables.InstantiateAsync` style chunk creation as a main-thread stall risk and `Resources.UnloadUnusedAssets()` as an unload spike source.
- First-party project code still had two `Resources.UnloadUnusedAssets()` calls.
- No local recon file existed for `Instantiate()` / `Destroy()` debt outside the object pool.

What was done:
- Added `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`, a self-contained `ITickable` / `ISlowTickable` residency manager.
- Added `ChunkState` bitmask flags, deterministic AUP chunk hashing, and `RadiusBasedStreamingJob : IJobParallelFor` using `math.distancesq`.
- Added `NativeQueue<ChunkLoadRequest>` dispatch capped to one Addressables load per frame.
- Used `Addressables.LoadAssetAsync<GameObject>` with retained handles and explicit release; did not use `Addressables.InstantiateAsync`.
- Added pool warmup and Awaitable time-sliced activation capped to five pooled spawns per frame.
- Added additive scene load gating with `LoadSceneMode.Additive` and `allowSceneActivation=false`.
- Added `_ChunkFadeMask` shader fade ramp and Polish reciprocal multiply (`ChunkFadeSecondsRcp`) instead of division.
- Added `RuntimeWatchdog.GetAvailableMemory()` and a 500MB streaming memory guard.
- Added 300-frame fixed black-box telemetry ring and binary dump path `Docs/AgentLogs/Dump_CORE_CHUNK_STREAMING.bin`.
- Removed first-party `Resources.UnloadUnusedAssets()` from `Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs` and `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`.
- Generated `Docs/AgentLogs/RECON_CORE_CHUNK_STREAMING.md` with 412 `Instantiate()` / `Destroy()` matches outside `ObjectPoolManager.cs`.

Cinematic Cheats used:
- Squared-distance residency instead of exact distance or physical streaming simulation.
- Load/unload hysteresis 500m/600m instead of boundary-perfect residency.
- `_ChunkFadeMask` visual cross-fade instead of physically solving chunk transition visibility.
- One request/frame dispatch and five activation spawns/frame to trade latency for frame stability.
- Runtime GPU upload budget by tier instead of unbounded texture upload honesty.

Exact microseconds saved:
- Radius scan: estimated 420 us saved per 512 chunks on i3-class CPU versus managed main-thread scan.
- Hysteresis churn avoidance: estimated 80 us saved at threshold crossings.
- Load queue spike flattening: estimated 600-1200 us avoided under dense Addressables queues.
- Pool warmup: estimated 300 us or more avoided per prefab family activation.
- Awaitable activation slicing: estimated 300-900 us frame spike reduction during dense chunk promotion.
- AUP shift sync: up to 500ms stale-residency delay removed; this is latency, not CPU time.
- Unload ban: avoids possible 40ms+ stop-the-world stall if first-party calls enter runtime/editor-hot paths.
- Polish reciprocal fade: estimated 0.02 us per fade update.
- Memory guard and explicit release: no CPU microsecond claim; risk reduction only until profiler proof.

Verification:
- Forbidden scan on touched runtime files returned no `foreach`, `string.Format`, interpolation, `.ToString()`, `Task.Run`, `Addressables.InstantiateAsync`, `Resources.UnloadUnusedAssets`, managed `List`/`Dictionary` construction, LINQ, or Unity `Update`/`FixedUpdate`/`LateUpdate` methods.
- `Assets/_Project` first-party scan returned no `Resources.UnloadUnusedAssets` matches.
- `git diff --check` on the streaming slice returned no whitespace errors; only CRLF warnings on pre-existing tracked files.
- Final build command failed outside this slice: `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult could not be found`.

Integrator note:
- Do not mark this VERIFIED until survival ownership restores `SurvivalPhysiologyScalarResult` or removes the stale reference and a full `dotnet build Hecton8.Core.csproj` passes.
- RuntimeWatchdog had unrelated pre-existing dirty edits in the worktree. This slice only relies on the added public `GetAvailableMemory()` query.

## 2026-05-12 - Honest R&D Continuation / Compile Medic
STATUS: PENDING VERIFICATION

What was wrong:
- Additive scene activation state was optimistic: the manager marked a sub-scene loaded after `allowSceneActivation=true`, not after `AsyncOperation.isDone`.
- Eviction during the activation window could lose the correct unload path and leave structural chunk residency ambiguous.
- The previous compile note was stale. Current project state no longer stops at one survival error; it exposes a broader external wall.

What was done:
- Added `_additiveSceneUnloadWhenLoaded` to `WorldChunkResidencyManager`.
- Changed `TryActivateReadySubScenes()` to request activation at `progress >= 0.9f`, then wait for `isDone` before setting loaded state.
- Changed `ReleaseChunkHandles(int index)` so eviction during pending additive activation defers unload until the scene is actually loaded.
- Added `UnloadAdditiveScene(int index)` as the single state reset path for additive scene unload bookkeeping.
- Confirmed `Hecton8.Core.csproj` currently includes `SaveDeltaCompression.cs`, `SurvivalPhysiologyScalarJob.cs`, and `SurvivalStatusMasks.cs`; the file is ignored by git via `*.csproj`, so it is not reported in `git status`.
- Re-extracted the `CORE_CHUNK_STREAMING` prompt from `Docs/Tasks/CURRENT_BATCH.md` and kept STATUS as `PENDING VERIFICATION`.

Cinematic Cheats used:
- Still using residency state flags and shader fade instead of simulating any physical transition.
- Deferred unload is a deterministic bookkeeping cheat, not a real-time scene lifecycle simulation.

Exact microseconds saved:
- Additive activation fix: 0 us steady-state CPU claimed. It removes a race/leak risk.
- Compile-medic include audit: 0 us runtime.
- Retained estimates remain unchanged: 420 us per 512 chunk radius scan, 600-1200 us load-spike avoidance, 300-900 us activation spike flattening, and 40ms+ avoided stall risk from the first-party unload ban.

Verification:
- Focused hot-path scan found no `foreach`, string formatting/interpolation, `.ToString()`, `Task.Run`, `Addressables.InstantiateAsync`, `Resources.UnloadUnusedAssets`, managed List/Dictionary construction, LINQ, or Unity `Update` / `FixedUpdate` / `LateUpdate` in touched runtime files.
- `git diff --check` on the touched slice returned only CRLF normalization warnings on pre-existing tracked files.
- Latest build command failed outside the streaming slice with 73 errors and 3 warnings. Dominant missing symbols: `HectonPersistentPathPolicy`, `HectonNativeBridge`, `HectonNativeLibrary`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `SteamDeckInputPal`, `HapticWaveformLibrary`, and `HardwareTierDetector`.
- No reported compiler error names `WorldChunkResidencyManager.cs`.

Integrator note:
- This is not VERIFIED. Compile remains externally blocked. Do not convert status to green until the external Save/Core/Audio/Input/Optimization authorities are restored and a full build passes.

## 2026-05-12 - Honest R&D Continuation / NativeCollection Race Gate
STATUS: PENDING VERIFICATION

What was wrong:
- `RadiusBasedStreamingJob` reads `_chunkStates`, but Tick-side code could still mutate or scan the same NativeParallelHashMap while the job was scheduled.
- `RequestLoad` / `RequestEvict` had no safe explicit-request path during active job ownership.
- Black-box telemetry scanned the live chunk-state map every Tick, including active job frames.
- Forced reevaluation could schedule a new residency job before deferred requests were applied.
- `OnDisable` only completed the job if already finished, then released chunk handles while a job could still be alive.

What was done:
- Added explicit load/evict duplicate guards and a cold-allocated `_deferredEvictChunkIds` lane.
- Gated load dispatch and Addressables polling behind `_residencyJobScheduled == false`.
- Preserved explicit load requests without reading `_chunkStates` during active job frames.
- Preserved explicit evict requests by deferring them until after the residency job fence.
- Added cached telemetry counts/state hash so black-box samples do not scan a NativeHashMap owned by an active job.
- Moved forced residency reschedule to the end of the safe mutation window.
- Added `CompleteResidencyJobForTeardown()` and used it from `OnDisable` and dispose.
- Added a defensive `SetChunkState` guard that refuses writes while the residency job owns the state map.

Cinematic Cheats used:
- Cached telemetry snapshot during active job frames instead of demanding exact live map scans.
- Deferred explicit evicts by one safe fence instead of blocking the frame.

Exact microseconds saved:
- No direct frame-time gain claimed.
- Telemetry can avoid one 512-entry state/hash scan during active job frames; exact saving pending profiler.
- Main value is correctness: removes a NativeCollection race that could produce safety exceptions, undefined reads, or teardown/release crashes.

Verification:
- `rg` scan on touched runtime files found no `foreach`, string formatting/interpolation, `.ToString()`, `Task.Run`, `Addressables.InstantiateAsync`, `Resources.UnloadUnusedAssets`, managed List/Dictionary construction, LINQ, or Unity `Update` / `FixedUpdate` / `LateUpdate`.
- `git diff --check` on the streaming slice returned no whitespace errors.
- Latest build evidence written to `Docs/AgentLogs/Build_CORE_CHUNK_STREAMING_errors3.txt`.
- Build result: 76 external errors, 3 warnings, no `WorldChunkResidencyManager.cs` errors. Repeated blockers include `HectonPersistentPathPolicy`, `HardwareTierDetector`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `SteamDeckInputPal`, `HectonNativeBridge`, `HectonNativeLibrary`, and voxel modified event types.

Integrator note:
- This slice remains PENDING VERIFICATION. The streaming manager is cleaner, but full verification is blocked by external project compile failures.
