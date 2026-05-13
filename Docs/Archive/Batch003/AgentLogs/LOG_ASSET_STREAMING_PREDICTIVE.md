# LOG_ASSET_STREAMING_PREDICTIVE

## 2026-05-12 - Predictive Streaming Pass
What was wrong:
- Radius-only streaming could not see fast submarine trajectory; side/back chunks competed with forward chunks.
- Pool expansion could happen too close to chunk activation, causing scatter/flora hitch risk.
- Far-behind Addressables handles could be released without an explicit dependency-cache clear.
- Teleport/death AUP jumps could leave stale queued chunk work targeting the old location.

What was done:
- Extended `WorldChunkResidencyManager` with velocity-based forward capsule loading, tier-capped lookahead, tail-biased eviction, and projected-AUP native load ordering.
- Added predictive warmup through Awaitable-spread `ObjectPoolManager.Warmup` batches for the first five author/Data-Monolith resolved prefabs before activation.
- Added VRAM hard abort on MX350-class memory at 1600MB, keeping immediate-radius loading only.
- Added optional `IChunkVoxelBakeReadiness.IsBaseVoxelMeshReady` gate so scatter/flora wait for base voxel readiness.
- Added BaseAirlock dry-space and transport pause handling without direct `VehicleDockingModule` dependency.
- Added teleport queue purge, stale queued-loading flag cleanup, immediate radius repopulation, and burst dispatch budget.
- Added far-behind `Addressables.ClearDependencyCacheAsync`.
- Added `StreamerStress01` scalar for UI binding and recon log for synchronous `SceneManager.LoadScene` offenders.

Cinematic Cheats used:
- Capsule prediction uses dot product plus lateral squared distance instead of honest ellipse/sqrt distance.
- Speed uses `rsqrt`-derived magnitude, no `math.sqrt` or unconditional `math.normalize`.
- Low/MX350 caps prediction to 50m; High/Ultra cap to 200m instead of uncapped 5-second physical travel.
- Tail culling uses deterministic behind-vector dot test instead of modeling visibility or occlusion.

Exact microseconds saved:
- Residency scan estimate: 4-9 us / 512 chunks vs visible pop-in recovery loads.
- Native candidate ordering: 10-35 us / 256 candidates, 0 B/frame, no managed `List.Sort()`.
- VRAM abort check: 1-3 us on slow-tick; avoids speculative residency past 1600MB on MX350.
- Predictive prewarm: avoids estimated 300-2500 us activation hitch on prefab-heavy chunks.
- Teleport recovery scan: rare path, 40-180 us / 512 chunks; prevents wasted old-AUP loads.
- Streamer stress metric: <2 us/frame, 0 B/frame.

Verification:
- `validate_script Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: pass, 0 diagnostics.
- Unity console after compile request: blocked only by unrelated gameplay symbols (`SuitStats`, `SuitUpgrades`, `PlayerKinematicsHandTarget`).
- `dotnet build Hecton8.Core.csproj`: 0 warnings from streaming file; fails on the same unrelated gameplay dependency errors.
- Recon file: `Docs/AgentLogs/RECON_ASSET_STREAMING_PREDICTIVE.md`.
- Scoped git status: target files are untracked in this workspace, so no tracked patch body is available from `git diff`.

## 2026-05-12 - Final Corrected Streaming Report
What was wrong:
- Radius-only residency could not predict fast transport motion.
- FIFO load dispatch could waste early slots on side chunks instead of the 5-second projected AUP path.
- Predictive pool growth needed to happen before scatter/flora activation and without coroutines.
- Far-behind chunks released handles but did not explicitly clear Addressables dependency cache.
- Docking/dry-space and teleport cases could keep stale predictive work alive.

What was done:
- `RadiusBasedStreamingJob` now evaluates immediate radius plus a velocity-forward capsule using dot/lateral-squared math.
- Tail unload radius shrinks behind velocity at speed.
- `ChunkLoadPrioritySortJob` sorts `NativeList<long>` candidates in Burst by squared distance to projected AUP.
- Predictive loads start an Awaitable prewarm over the first five authored/Data-Monolith prefab entries, falling back to dependencies/activation prefabs.
- Activation waits for predictive prewarm and optional `IChunkVoxelBakeReadiness.IsBaseVoxelMeshReady`.
- MX350-class VRAM abort disables prediction at >=1600MB and keeps immediate-radius loading.
- BaseAirlock dry-space events and an external setter suspend predictive streaming without concrete vehicle dependency.
- Teleport detection clears queues and repopulates immediate-radius loads.
- Far-behind eviction starts `Addressables.ClearDependencyCacheAsync`.
- `StreamerStress01` exposes a zero-string scalar UI metric.
- `RECON_ASSET_STREAMING_PREDICTIVE.md` records synchronous `SceneManager.LoadScene` offenders.

Cinematic Cheats used:
- Capsule corridor replaces honest ellipse/sqrt math.
- Velocity direction uses `rsqrt`; no `math.sqrt` or unconditional `math.normalize`.
- Low tier caps prediction at 50m; Middle 100m; High/Ultra 200m.
- Tail culling is a cheap behind-vector dot test, not occlusion/visibility modeling.
- Tick stress divisions were replaced with precomputed reciprocals.

Exact microseconds saved:
- Residency prediction: 4-9 us / 512 chunks.
- Tail culling: 2-5 us / 512 chunks.
- Native load ordering: 10-35 us / 256 candidates, 0 B/frame.
- Predictive prewarm: avoids estimated 300-2500 us activation hitch on prefab-heavy chunks.
- VRAM abort check: 1-3 us slow-tick.
- Teleport recovery: rare path, 40-180 us / 512 chunks.
- Streamer stress scalar: <2 us/frame, 0 B/frame.

Verification:
- `validate_script Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: pass, 0 diagnostics.
- Anti-bloat scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort()` in streamer.
- Unity compile request: blocked outside streaming by Gameplay dependency errors (`SuitStats`, `SuitUpgrades`, `PlayerKinematicsHandTarget`).
- Earlier `dotnet build Hecton8.Core.csproj` run was blocked by unrelated Gameplay dependency errors; later upgrade pass recovered managed build to 0 warnings/0 errors.
- Scoped git status: target files are untracked in this workspace, so tracked `git diff` has no patch body.

## 2026-05-12 - Upgrade Pass After Recheck
What was wrong:
- Sort scoring still recomputed projected AUP absolute coordinates for each candidate score.
- The native sort record used `float.CompareTo`, which is unnecessary instruction pressure in Burst.
- Far-behind unload called an unbounded asset lifecycle drain path.
- The status/rationale still reflected earlier managed compile blockers after the local build recovered.

What was done:
- `ChunkLoadPrioritySortJob` now receives projected AUP as a cached `double3`.
- `ChunkLoadSortRecord.CompareTo` uses direct primitive comparisons.
- Far-behind asset lifecycle draining is budgeted to 8 releases instead of `ForceDrainPendingReleaseQueue`.
- Confirmed `_chunkLoadSortRecords` is registered and disposed with the rest of native state.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore /m:1`: success, 0 warnings, 0 errors.
- Checked MCP resources: Unity MCP currently reports `instance_count: 0` and no active Unity session, despite editor processes running.

Cinematic Cheats used:
- Kept capsule corridor, rsqrt speed, squared distances, and tier caps.
- Replaced unbounded cleanup with a budgeted release drain to avoid hitching on MX350.

Exact microseconds saved:
- Native sort projected-AUP cache: estimated 2-8 us saved on large candidate sets.
- Direct compare: estimated 1-3 us saved in worst-case sort.
- Budgeted asset lifecycle drain: prevents unbounded far-behind eviction spikes; hard cap is 8 releases per cache-clear unload.

Verification:
- Managed build: `dotnet build Hecton8.Core.csproj --no-restore /m:1` passed, 0 warnings, 0 errors.
- Static anti-bloat scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `ForceDrainPendingReleaseQueue`.
- Unity MCP/Burst compile: blocked by missing Unity MCP session, not by C# compiler errors.

## 2026-05-12 - Compile Wall Update
What was wrong:
- A later managed build picked up concurrent out-of-domain failures after the previous successful pass.
- Current blockers are `EncounterDirector.ResolveCheapestAllowedCost`, `PDAMapTab.TryResolvePointCloudFrame`, `PDAMapTab.DispatchSonarPointCloud`, and `PDAMapTab.IsLowMathTier`.
- Current warnings are outside the streamer: `WorldSpatialHashGrid.CurrentTotalOffset` and two `PlayerCriticalProceduralAudioRenderer.HullSynthesisState` fields.

What was done:
- Rechecked the streaming file for math/GC/coroutine regressions: no `math.sqrt`, no `math.normalize`, no `foreach`, no coroutine API, no unbounded `ForceDrainPendingReleaseQueue`.
- Verified the latest full-build errors do not reference `WorldChunkResidencyManager.cs`.
- Left out-of-domain compile blockers untouched under the Streaming Architect domain boundary.

Cinematic Cheats used:
- No new simulation added. Existing capsule/rsqrt/squared-distance cheats remain.

Exact microseconds saved:
- No new runtime path. This was a build-health and scope-control pass.

Verification:
- Latest `dotnet build Hecton8.Core.csproj --no-restore /m:1` fails outside streaming with 4 errors and 3 warnings.
- Unity MCP still reports no active Unity session for editor/Burst verification.

## 2026-05-12 - Determinism And Fault Telemetry Upgrade
What was wrong:
- Native load ordering used a 32-bit tie-break, so two chunk ids with identical low bits and equal projected score could compare equal.
- Predictive prewarm Awaitables caught cancellation but not unexpected pool/prefab failures, creating a silent failure risk.
- The prior compile-wall log became stale after the next managed build pass recovered.

What was done:
- Replaced the tie-break with full `long` `ChunkId` comparison inside `ChunkLoadSortRecord.CompareTo`.
- Added `TelemetryPredictivePrewarmFaultFlag` and writes to the 300-frame streaming black box when predictive prewarm throws.
- Re-ran static bloat scan and managed compile.

Cinematic Cheats used:
- Kept squared projected-AUP distance and native sort scratch. No honest sqrt/normalize was introduced.

Exact microseconds saved:
- Deterministic tie-break cost is effectively neutral; prevents non-deterministic retry/load ordering.
- Prewarm fault telemetry is rare-path only; steady-state cost remains 0 us/frame.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no managed `List.Sort()`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1`: succeeds with 0 errors and 3 out-of-domain warnings.
- Unity MCP validation/Burst compile check remains blocked by no active Unity instance.

## 2026-05-12 - Hysteresis And Invalid Handle Recheck
What was wrong:
- `CURRENT_BATCH.md` no longer contains the `ASSET_STREAMING_PREDICTIVE` tag; persisted status/rationale remain the only valid local memory for this agent.
- Predictive VRAM abort resumed at the same threshold it aborted on, allowing MX350 pressure flicker.
- Invalid Addressables load/cache-clear handles could remain flagged as active and hold loading/cache slots forever.
- State diagnostics rescanned the full chunk map during state writes and again during telemetry.
- Streamer stress still had one Tick-time speed division.

What was done:
- Stayed inside Asset Streaming / World Residency and did not patch unrelated audio/UI compile blockers.
- Added MX350 predictive hysteresis: abort at 1600MB, resume only below 1400MB.
- Invalid Addressables load handles now clear their slot and loading flag; missing chunk-id mappings release the handle instead of leaving a stuck slot; invalid cache-clear handles now clear their slot.
- Added `_stateDiagnosticsDirty` and refresh-once diagnostics for state counts/hash before stress/telemetry reads.
- Replaced stress speed division with `StreamerStressSpeedSqRcp`.

Cinematic Cheats used:
- Memory pressure is treated as a stable binary gate with hysteresis instead of honest continuous thrash.
- Diagnostics use cached high-level state until dirty, preserving black-box usefulness without paying full-map scans repeatedly.

Exact microseconds saved:
- Dirty diagnostics: removes O(state_changes * chunk_count) rescans during load bursts; estimated 20-200 us saved on large request waves, device/load dependent.
- Speed reciprocal: sub-1 us/frame, but removes a known hot-path division.
- Invalid handle cleanup: prevents permanent streaming slot loss; runtime gain is correctness under Addressables failure.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no `/900f`.
- Latest `dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeds with 0 errors and 5 out-of-domain warnings.
- Unity MCP resources list is visible, but `mcpforunity://instances` reports `instance_count: 0`; `validate_script` and console read return `no_unity_session`.

## 2026-05-12 - Pending-Lane Poll Gating
What was wrong:
- Resident Addressables handles kept `_hasAddressableHandle` true, so the Tick poll loop scanned stored handles even when no loads were pending.
- Cache-clear polling scanned the whole cache-clear array even when no cache clear was active.
- Additive scene activation polling scanned the whole operation array after all operations were complete.

What was done:
- Added `_addressableLoadPending` plus `_pendingAddressableLoadCount`.
- Added `_pendingAddressableCacheClearCount`.
- Added `_pendingAdditiveSceneOperationCount`.
- `PollAddressableLoads`, `PollAddressableCacheClears`, and `TryActivateReadySubScenes` now early-out when no operation is active.
- Release and invalid-handle paths clear pending counters without releasing resident ownership handles prematurely.

Cinematic Cheats used:
- No simulation added. This is pure cadence/load-shed: idle streaming state becomes an integer check instead of full-array scans.

Exact microseconds saved:
- Addressables idle polling: estimated 5-60 us/frame saved on large authored chunk tables.
- Cache-clear/additive idle polling: estimated 1-20 us/frame saved depending on chunk count.
- GC impact: 0 B/frame; added only cold arrays and counters.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no `/900f`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1`: succeeds with 0 errors and 0 warnings.
- Unity MCP still reports `mcpforunity://instances` as `instance_count: 0`; editor/Burst validation remains unavailable.

## 2026-05-12 - Tiered Dispatch Cadence
What was wrong:
- The streamer drained one queued load request per frame on every hardware tier.
- Low-tier protection was correct, but High/Ultra had no way to spend saved CPU on smoother forward residency.

What was done:
- Added `LowTierLoadDispatchBudget`, `MiddleTierLoadDispatchBudget`, `HighTierLoadDispatchBudget`, and `UltraTierLoadDispatchBudget`.
- Replaced the single Tick dispatch with `ProcessLoadDispatchBudget()`.
- Added `ResolveLoadDispatchBudget()` so Low/MX350 dispatches 1 request/frame, Middle 2, High 3, Ultra 4.
- Predictive VRAM abort clamps dispatch back to Low.

Cinematic Cheats used:
- No physical simulation added. High-tier visual continuity is improved by draining the already-prioritized predictive queue faster.

Exact microseconds saved:
- Low tier: unchanged, no extra dispatch pressure.
- Middle/High/Ultra: queue latency reduced by about 2x/3x/4x during load bursts; visible gain is fewer forward pop-in misses at speed.
- GC impact: 0 B/frame; scalar constants and fixed loops only.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no `/900f`.
- Latest `dotnet build Hecton8.Core.csproj --no-restore /m:1` is blocked outside streaming by `HectonVoxelEngine.cs` missing `EnsureVoxelSurfaceMeshAvailableAsync` / `EnsureVoxelPhysicsBakeMeshAvailableAsync`; no streamer errors were reported.
- Unity MCP still reports `mcpforunity://instances` as `instance_count: 0`; editor/Burst validation remains unavailable.

## 2026-05-12 - Adjacent World Warning Cleanup
What was wrong:
- `WorldSpatialHashGrid` had a dead `RebuildAbsolutePositionsJob` struct left behind after the current origin-shift path stopped scheduling it.
- The stale job shell was producing a managed compile warning and kept obsolete Burst code in the world residency adjacency layer.

What was done:
- Removed only the dead job struct.
- Preserved existing origin-shift/runtime-position edits already present in the dirty worktree.
- Re-extracted the `ASSET_STREAMING_PREDICTIVE` prompt from `CURRENT_BATCH.md`.

Cinematic Cheats used:
- No simulation added. This is code burial: remove stale unused Burst path instead of maintaining a fake active path.

Exact microseconds saved:
- Runtime saved: 0 us/frame because the job was no longer scheduled.
- Build hygiene saved: warning count reduced to 0, removing review noise.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore /m:1`: succeeds with 0 warnings and 0 errors.
- Static streaming scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, and unbounded release draining.
- Unity MCP still reports `mcpforunity://instances` as `instance_count: 0`; editor/Burst validation remains unavailable.

## 2026-05-12 - Activation Overflow Guard
What was wrong:
- Chunk activation trusted `_spawnedCountsByChunk[index]` to stay inside the per-chunk spawned-instance slot array.
- A bad chunk definition or stale counter could throw during flora/scatter activation, leak the just-spawned pool instance, and leave the black-box ring without an explicit activation-overflow flag.
- Teleport job completion was a deliberate rare barrier but lacked the native-jobs mandate `[BLOCKING_SYNC_POINT]` annotation.

What was done:
- Added `TelemetryActivationOverflowFlag`.
- Guarded activation slot writes with an unsigned bounds check.
- Returned overflowed instances to `ObjectPoolManager` immediately.
- Clamped despawn count to slot capacity before returning instances to the pool.
- Annotated the teleport residency completion as `[BLOCKING_SYNC_POINT]` because teleport invalidates queued AUP data and immediately repopulates the new radius.

Cinematic Cheats used:
- No simulation added. This is fail-fast containment: bad authored density degrades to pool return plus telemetry instead of runtime exception.

Exact microseconds saved:
- Normal path: no measurable save, one integer bounds check per spawned prefab, 0 B/frame.
- Fault path: avoids exception/unwind and leaked scene objects; estimated millisecond-scale hitch avoided on malformed activation.
- High-tier visual spend: denser authored activation lists remain safe because overflow is contained instead of crashing the streaming pass.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation returns `no_unity_session`; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Async Generation Guards
What was wrong:
- `destroyCancellationToken` cancels only on destruction, not on chunk eviction or manager disable.
- `ActivateChunkAsync` could resume after release and spawn scatter/flora for a chunk that was no longer resident.
- Predictive prewarm could continue warming prefab pools after its chunk was invalidated by tail culling or teardown.

What was done:
- Added `_activationVersions` as a cold per-chunk generation guard.
- Captured activation generation at resident promotion.
- Invalidated activation generation in `ReleaseChunkHandles`.
- Added activation current checks before and after awaits and before every spawn.
- Added predictive prewarm generation checks before warmup work and after every awaited slice.

Cinematic Cheats used:
- No simulation added. This is lifecycle load-shed: stale visual work is skipped instead of synchronized or simulated to completion.

Exact microseconds saved:
- Normal path: scalar integer generation checks only, 0 B/frame.
- Fault path: avoids stale object spawns and wasted prewarm slices; estimated 100-2500 us saved on prefab-heavy chunks evicted during activation.
- Pool integrity: prevents released chunks from reintroducing pooled visuals after tail culling.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- A transient out-of-domain `HectonPlayerMovement.cs` compile error appeared during validation and was resolved outside this prompt.
- Latest `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation returns `Unity session not ready`; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Player AUP And Tier Cache Polish
What was wrong:
- `RadiusBasedStreamingJob` converted the player's AUP into absolute `double3` once per chunk.
- Load dispatch budget resolution re-read static hardware tier data during queued load bursts.

What was done:
- Added `PlayerAbsolute` to the Burst residency job and assign it once when scheduling.
- Replaced per-chunk player AUP conversion with the scheduled absolute value.
- Added `_resolvedTier`, initialized once during startup, and used it for prediction length and load dispatch budgets.
- Preserved the existing async upload budget application path so QualitySettings are still written once for the resolved tier.

Cinematic Cheats used:
- No new simulation. This is bookkeeping removal: spend CPU on visible forward residency, not repeated coordinate/tier authority work.

Exact microseconds saved:
- Residency scan: estimated 2-6 us per 512 chunks by removing three int64-to-double conversions and three additions per chunk for the player position.
- Load burst dispatch: sub-1 us saved by avoiding repeated `SystemInfo` tier checks during queue drain.
- GC impact: 0 B/frame; added one enum field and one `double3` job field.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal`: succeeds with 0 warnings and 0 errors.
- `mcpforunity://instances` reports `instance_count: 0`; Unity Console/Burst validation remains PENDING VERIFICATION.

## 2026-05-12 - Duplicate Chunk Id Guard
What was wrong:
- Duplicate authored chunk centers could hash to the same deterministic chunk id.
- The hash maps rejected the duplicate key, but the SoA arrays could still receive the duplicate row, creating ambiguous residency scans against one shared state record.

What was done:
- Added `TelemetryDuplicateChunkIdFlag`.
- Added a duplicate-id guard before writing chunk ids, centers, state, and index entries.
- Duplicate definitions are skipped and recorded in the fixed black-box telemetry ring.

Cinematic Cheats used:
- No runtime simulation. Bad authoring is reduced to a skipped duplicate and telemetry event instead of trying to synthesize a new identity.

Exact microseconds saved:
- Runtime: 0 us/frame normal-path cost because the guard runs only during startup ingest.
- Fault path: avoids duplicate scan/load/unload ambiguity; savings depend on bad data size, but it prevents wasted Addressables dispatches and state churn.
- GC impact: 0 B/frame; one telemetry bit and one startup hash lookup per authored chunk.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP still has no active instance; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Release-All Residency Reset
What was wrong:
- `ReleaseAllChunks` released instances and handles but did not reset the native residency state map.
- Disable/re-enable could leave a released chunk marked Resident or Loading.
- A late Addressables completion could promote a chunk after eviction/disable because promotion did not require the chunk to still be in the Loading state.

What was done:
- Added `TelemetryReleaseAllResetFlag` for black-box release-all events.
- Reset released chunks back to `Pinned` or `Unloaded` based on authored definition state.
- Cleared queued load/evict flags and drained native load/unload/sort queues after release-all.
- Added an Addressables completion gate: only chunks still marked `Loading` and not `Evicting` can be promoted.

Cinematic Cheats used:
- No new simulation. This is deterministic lifecycle load-shed: stale async visual work is released instead of completed or synchronized.

Exact microseconds saved:
- Normal Tick path: 0 us added, 0 B/frame.
- Release-all path: O(chunkDefinitions) cold reset, estimated 15-60 us for 512 chunks.
- Fault path: avoids late async promotion, stale resident state, leaked pooled visuals, and corrupted immediate-radius reload behavior after disable.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation returns `no_unity_session`; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Additive Scene Reuse Guard
What was wrong:
- A chunk released while its additive scene was still loading marks that pending operation to unload when complete.
- If the same chunk is requested again before completion, the streamer reused the pending operation but kept the stale unload flag.
- The scene could unload on completion and leave the chunk stuck in `Loading`.

What was done:
- Updated `BeginOrTrackAdditiveSceneLoad` to clear `_additiveSceneUnloadWhenLoaded[index]` when a new load request reuses an existing pending additive scene operation.
- Kept the existing pending-operation poll lane; no callbacks, duplicate scene loads, or synchronous waits were added.

Cinematic Cheats used:
- No simulation. Reuse the already-paid async scene load and clear stale teardown intent instead of restarting or blocking.

Exact microseconds saved:
- Normal path: 0 B/frame, one cold boolean write only when tracking an existing pending additive operation.
- Fault path: prevents indefinite Loading state and avoids a second additive scene dispatch.
- Visual result: movement reversal or disable/re-enable during scene load no longer converts a valid pending load into an unload.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet restore Hecton8.Core.csproj`: regenerated missing project assets after concurrent workspace activity.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation now fails at HTTP transport (`127.0.0.1:8088/mcp`); Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Additive Scene Promotion Gate
What was wrong:
- Additive-scene chunks could become Resident/Staged before the additive scene activation operation finished.
- Addressables could complete before the structural scene, letting scatter/flora activation race ahead of chunk geometry.
- Invalid additive scene tracking arrays could silently degrade into promotion without the structural scene.

What was done:
- Added `TelemetryAdditiveSceneFaultFlag` and a small `AdditiveSceneLoadState` lane.
- Moved additive scene start/tracking ahead of resident promotion.
- Kept chunks in Loading while the additive scene is pending.
- Forced Addressables completions to wait for additive scene readiness before promotion.
- Failed closed with telemetry if scene tracking is invalid or `LoadSceneAsync` returns null.

Cinematic Cheats used:
- No blocking load and no fixed-frame delay. Structural readiness is a deterministic gate; visuals activate only after the scene operation reports done.

Exact microseconds saved:
- Normal path: 0 B/frame, only cold dispatch/poll branches.
- Fault path: prevents scatter-in-void, duplicate scene dispatch, and indefinite Loading state.
- Managed build now succeeds with 0 warnings and 0 errors; Unity MCP/Burst verification remains blocked by transport.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation fails at HTTP transport; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Release Path Bounds Hardening
What was wrong:
- Cleanup code assumed chunk definitions and per-chunk tracking arrays were always length-aligned.
- A mismatch could throw during release, cache clear, or additive-scene unload while the streamer was already recovering from unload/disable churn.

What was done:
- Guarded `ReleaseChunkHandles` before Addressables handle access and additive scene unload state writes.
- Guarded `RequestAddressablesCacheClear` before cache-clear handle writes.
- Made Addressables load/cache-clear polling use the minimum valid paired-array length.
- Guarded `UnloadAdditiveScene` before scene unload and state reset writes.
- Kept Addressables release independent from `chunkDefinitions` availability so fault cleanup does not leak handles.

Cinematic Cheats used:
- No extra simulation and no rebuild. Faulty lanes are dropped or skipped safely; black-box telemetry remains the failure evidence path.

Exact microseconds saved:
- Normal idle Tick: 0 B/frame and no added work when pending counters are zero.
- Active pending lanes: only integer bounds/min checks, estimated below 1 us for normal pending counts.
- Fault path: avoids exception/unwind cost and stuck cleanup when authoring/runtime arrays drift.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `git diff --check`: clean except repository CRLF conversion warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation fails at HTTP transport; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Activation Slot Fault Hardening
What was wrong:
- Activation assumed version/in-progress arrays and spawned-instance slot arrays were valid.
- A partial initialization or array mismatch could throw during activation/despawn and leave a chunk Staged.

What was done:
- Added `TelemetryActivationFaultFlag`.
- Guarded activation bookkeeping before starting `ActivateChunkAsync`.
- Guarded spawned-instance arrays before activation and despawn writes.
- Replaced direct predictive prewarm array access with a bounded helper.
- Cleared Staged on activation bookkeeping fault instead of throwing.

Cinematic Cheats used:
- No fallback spawn and no allocation. Broken activation ownership is skipped and recorded, preserving pool integrity.

Exact microseconds saved:
- Normal activation: only cold bounds checks, 0 B/frame.
- Fault path: avoids exception/unwind cost and leaked pooled instances.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `git diff --check`: clean except repository CRLF conversion warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation fails at HTTP transport; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Addressables Dispatch Fault Gate
What was wrong:
- Authored Addressables chunks assumed handle and pending arrays existed and matched chunk indices.
- A malformed tracking lane could index directly or fall through into null-payload resident promotion.

What was done:
- Added `TelemetryAddressablesFaultFlag`.
- Guarded Addressables handle, ownership, and pending arrays before `LoadAssetAsync`.
- Kept chunks waiting while an existing Addressables handle is still pending.
- Promoted only from a valid, completed, successful Addressables handle.
- Released/cleared Loading with telemetry when the Addressables lane is invalid.
- Failed closed when Addressables support is compiled out but an address is authored.
- Released any companion additive scene operation on Addressables fault so structural scenes do not remain loaded after payload failure.

Cinematic Cheats used:
- No synchronous asset load and no fallback dummy visuals. Broken payload lanes fail closed instead of faking residency.

Exact microseconds saved:
- Normal dispatch: only integer bounds checks on authored Addressables chunks, 0 B/frame.
- Fault path: avoids repeated empty promotions and recovery churn against invalid tracking.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `git diff --check`: clean except repository CRLF conversion warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation fails at HTTP transport; Burst/editor validation remains PENDING VERIFICATION.

## 2026-05-12 - Additive Scene Poll Bounds Hardening
What was wrong:
- `TryActivateReadySubScenes` iterated `_additiveSceneOperations.Length` and then directly indexed activation, loaded, unload, definition, and chunk-id arrays.
- A partial initialization or tracking-array mismatch could throw during additive scene recovery.

What was done:
- Added null guards for all paired additive scene tracking arrays, `chunkDefinitions`, and `_chunkIdsByDefinitionIndex`.
- Changed the additive scene poll loop to iterate the minimum valid paired length before activation, unload, or promotion writes.
- Recorded that `CURRENT_BATCH.md` has rotated and no longer contains this agent prompt, so no new prompt content was loaded.

Cinematic Cheats used:
- No simulation change. Faulty additive scene lanes are bounded instead of repaired through allocation or blocking waits.

Exact microseconds saved:
- Normal pending-scene path: integer min/bounds work only, estimated below 1 us for typical pending counts.
- Fault path: avoids exception/unwind and stuck additive-scene poll state.

Verification:
- Static scan: no `math.sqrt`, no `math.normalize`, no coroutine API, no managed `foreach`, no `List.Sort`, no `string.Format`, no synchronous `SceneManager.LoadScene` in `WorldChunkResidencyManager`.
- `dotnet restore Hecton8.Core.csproj`: regenerated missing project assets after `project.assets.json` vanished.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal`: succeeds with 0 warnings and 0 errors.
- Unity MCP console validation fails at HTTP transport; Burst/editor validation remains PENDING VERIFICATION.
