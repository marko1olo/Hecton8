# Rationale_ASSET_STREAMING_PREDICTIVE

Status: PENDING VERIFICATION

## Decision 0: Prompt Isolation And Mandate Set
Problem: Predictive streaming touches native jobs, Addressables, pool prewarm, VRAM pressure, and AUP movement. Wrong domain assumptions would create hard coupling with other agents.
Solution: Extracted only `<AGENT_PROMPT id="ASSET_STREAMING_PREDICTIVE">` via CLI and loaded the streaming/native/GC/VRAM mandates before code.
Rejected Alternatives: Greenfield streamer rewrite was rejected because radius streaming already exists and AGENTS.md forbids architecture drift. Direct concrete dependencies on other agents were rejected; GlobalRegistry/interfaces or existing abstractions must be used.
Scalability potential: Low tier clamps prediction to a short corridor and suspends speculative work under VRAM pressure. Middle tier keeps radius plus moderate lookahead. High tier expands lookahead. Ultra spends saved CPU on stronger residency and visual continuity.
Hardware Impact: Expected low-end i3/MX350 gain is fewer late chunk activations and fewer behind-player residents; estimate pending code inspection.

## Decision 1: Predictive Shape Inside Existing Burst Residency Job
Problem: Fast transport outruns the symmetric radius scan, creating visible chunk pop-in.
Solution: Extended `RadiusBasedStreamingJob` with player velocity, a capped prediction distance, and a forward capsule test using squared distance, dot product, and lateral squared distance only.
Rejected Alternatives: A managed Transform prepass was rejected because it would add GC/domain coupling. A full ellipse with square-root normalization was rejected because the prompt explicitly requires math audit and low-tier CPU protection.
Scalability potential: Low caps at 50m; Middle caps at 100m; High and Ultra cap at 200m. Low keeps a short cheap corridor, Ultra spends the same scan on stronger visual continuity.
Hardware Impact: i3/MX350 expected cost is under 0.01 ms for 512 chunks; saved visual budget comes from fewer emergency activations.

## Decision 2: Predictive Prewarm Through Awaitable Pool Expansion
Problem: Flora/scatter activation can hitch when pools expand on the same frame as chunk activation.
Solution: Predictive loads start an Awaitable prewarm for the first five author-resolved prefabs and activation waits until both prewarm and optional voxel bake readiness are complete.
Rejected Alternatives: Coroutines were rejected by prompt. Direct H8BiomeRecord prefab scanning was rejected because the current `H8BiomeRecord` layout does not expose prefab frequencies; the chunk definition now carries the Data Monolith top-prefab list and falls back deterministically.
Scalability potential: Low/VRAM pressure skips speculative prewarm; High/Ultra can spend saved frames on larger authored prefab pools.
Hardware Impact: On i3/MX350 this avoids estimated 0.3-2.5 ms one-frame activation spikes for prefab-heavy chunks by spreading warmup over Awaitable frames.

## Decision 3: MX350 VRAM Abort
Problem: Predictive loading is dangerous on 2GB-class GPUs once residency crosses 1600MB.
Solution: The streamer checks `VRAMBudgetTracker` and registered `VRAMMonitor`; on MX350-class memory it disables predictive radius, predictive prewarm, and keeps immediate-radius loading only.
Rejected Alternatives: Warning-only telemetry was rejected because the prompt requires a hard halt. Global texture downgrade was rejected as outside the streaming domain.
Scalability potential: Low protects memory; High/Ultra retains predictive corridor and pool expansion.
Hardware Impact: On MX350 this prevents speculative Addressables and pool objects from pushing into swap/driver eviction; expected gain is stability rather than raw microseconds.

## Decision 4: Native Priority Sort
Problem: A fast submarine needs the closest chunks to the projected 5-second AUP dispatched before equally valid but visually irrelevant side chunks.
Solution: Added a Burst-compiled `ChunkLoadPrioritySortJob` that writes bounded `ChunkLoadSortRecord` scratch entries, uses native `NativeList.Sort()`, and orders by squared distance to projected AUP with a full `long` chunk-id tie-break.
Rejected Alternatives: `List.Sort()` was rejected because it allocates and violates the prompt. FIFO-only dispatch was rejected because a forward capsule can overfill with side candidates.
Scalability potential: Low gets the same native sort over a smaller prediction vector; High/Ultra can handle larger candidate counts without managed GC spikes.
Hardware Impact: i3/MX350 estimate is 10-35 us for a full 256-candidate queue, with 0 B/frame managed allocations.

## Decision 5: Cache Clear And Voxel Readiness
Problem: Far-behind chunks can hold Addressables dependency cache, and scatter/flora can appear before base voxel mesh bake completes.
Solution: Far-behind eviction releases handles and starts `Addressables.ClearDependencyCacheAsync`; activation waits on optional `IChunkVoxelBakeReadiness.IsBaseVoxelMeshReady`.
Rejected Alternatives: `Resources.UnloadUnusedAssets` was rejected as a global hitch. Fixed-frame scatter delays were rejected as non-deterministic and habitat/terrain dependent.
Scalability potential: Low clears dead-tail cache aggressively to protect VRAM. Ultra keeps smooth visual activation because scatter waits for the correct readiness signal rather than guessed timing.
Hardware Impact: MX350 saves VRAM residency pressure; activation wait adds no cost when no provider exists and prevents wasted flora spawns in void.

## Decision 6: Docking And Teleport Control
Problem: Predictive velocity is wrong while docked/dry-space, and teleport/death moves invalidate all queued streaming decisions.
Solution: BaseAirlockEvents dry state plus external setter suspend prediction. Teleport detection completes the rare in-flight residency job, clears queues, and synchronously repopulates immediate-radius requests.
Rejected Alternatives: Velocity-zero detection was rejected because stopped submarines and docking are not equivalent. Waiting for the next slow tick after teleport was rejected because it guarantees missing local chunks.
Scalability potential: Low avoids speculative work in interiors; High/Ultra keep prediction only when it buys visible continuity.
Hardware Impact: i3/MX350 benefits from immediate queue cancellation instead of wasting loads on old AUP; rare teleport scan estimate is 40-180 us for 512 chunks.

## Decision 7: Math LOD And Streamer Stress Metric
Problem: Prediction length and streaming backlog need visibility without adding UI text churn or frame allocations.
Solution: Prediction distance uses rsqrt-derived speed and tier caps. `StreamerStress01` exposes a scalar UI metric based on queue pressure, resident pressure, speed, and suspension state.
Rejected Alternatives: `math.sqrt` and formatted debug strings were rejected. In-app explanatory text was rejected; UI can bind to the scalar.
Scalability potential: Low uses short predictive math and conservative stress; Ultra can drive aggressive visual overkill while still exposing backlog pressure.
Hardware Impact: 0 B/frame; estimated <2 us/frame to update the scalar.

## OMEGA POLISH CHANGES
Problem: The first pass still contained one local warning risk and Tick-time divisions in the stress metric.
Solution: Removed the duplicate `Hecton8.Optimization` using, replaced stress divisions with precomputed reciprocals from `ClampSettings`, and kept predictive warmup on the frame-budgeted `ObjectPoolManager.WarmupPrefabAsync` path.
Rejected Alternatives: `math.sqrt`, `math.normalize`, `List.Sort()`, coroutine prewarming, `Resources.UnloadUnusedAssets`, and fixed-frame voxel delays stayed rejected. A concrete dependency on `VehicleDockingModule` was also rejected; transport/dry-space pause remains through BaseAirlock events and an external suspension setter.
Scalability potential: Low/MX350 uses a 50m cap, hard VRAM abort at 1600MB, short tail retention, and immediate-radius fallback. Middle uses 100m. High/Ultra use 200m prediction and can spend the saved hitch budget on heavier authored pools and cleaner scatter continuity.
Hardware Impact: Office i3/MX350 estimate remains under 0.01 ms for 512 chunk residency scan, 10-35 us for worst-case native load ordering, 0 B/frame on steady tick, and 0.3-2.5 ms activation hitch avoided by Awaitable pool expansion.
Git Diff: Target files are currently untracked in this workspace (`WorldChunkResidencyManager.cs`, `Status_ASSET_STREAMING_PREDICTIVE.md`, `Rationale_ASSET_STREAMING_PREDICTIVE.md`, `RECON_ASSET_STREAMING_PREDICTIVE.md`, `LOG_ASSET_STREAMING_PREDICTIVE.md`), so `git diff` has no tracked patch body for them. Scoped status is recorded in the final log.
Build Health: managed build passes now succeed with 0 warnings and 0 errors. Unity MCP editor/Burst verification is blocked because the MCP console read returns `no_unity_session`.

## Decision 8: Upgrade Pass After Managed Compile Recovery
Problem: The managed build path recovered, but the streaming code still had minor performance and lifecycle risks: repeated projected-AUP conversion inside sort scoring, partial sort tie-breaks, unbounded asset lifecycle release draining, and silent predictive prewarm Awaitable failures.
Solution: Cached projected AUP as `double3` in `ChunkLoadPrioritySortJob`, replaced sort comparison with direct primitive comparisons and full `long` chunk-id tie-breaks, constrained far-behind asset lifecycle draining to 8 releases, verified `_chunkLoadSortRecords` unregisters/disposes with other NativeCollections, and added `TelemetryPredictivePrewarmFaultFlag` for prewarm exceptions.
Rejected Alternatives: Global `ForceDrainPendingReleaseQueue` was rejected because it can turn far-behind unload into an unbounded hitch. Recomputing projected absolute position inside every score was rejected as pointless ALU pressure.
Scalability potential: Low/MX350 gets bounded release work and less sort ALU; High/Ultra keep the same deterministic prioritization while spending cycles on visible residency instead of housekeeping.
Hardware Impact: Expected MX350 savings are small but real: 2-8 us avoided in large candidate sorts and unbounded release-drain spikes removed from far-behind eviction.

## Decision 9: Recheck Pass - Hysteresis And Handle Fault Cleanup
Problem: The recheck found three remaining production risks: predictive VRAM abort could oscillate around the 1600MB threshold, invalid Addressables/cache-clear handles could remain marked active forever, and diagnostic state scans could rescan all chunks on every state mutation plus every telemetry write.
Solution: Added a 1600MB abort / 1400MB resume hysteresis band for MX350 predictive streaming, clear invalid Addressables load/cache-clear handles with loading-flag recovery, and replaced repeated full-map diagnostics with a dirty-bit refresh that runs once when state changes. Removed the final Tick-time speed division by using a precomputed reciprocal constant.
Rejected Alternatives: Immediate VRAM resume at the same threshold was rejected because it violates state hysteresis. Leaving invalid handles to wait for `IsDone` was rejected because invalid handles never complete. Per-state-write full diagnostics were rejected because load bursts multiply the chunk map scan cost.
Scalability potential: Low/MX350 holds a stable immediate-radius-only mode until memory pressure genuinely recovers; Middle/High/Ultra avoid unnecessary diagnostic CPU and keep streaming cycles available for denser residency and smoother scatter activation.
Hardware Impact: Expected i3/MX350 gain is burst-dependent: eliminates O(state_changes * chunk_count) diagnostics during load waves, removes a Tick division, and prevents permanent loading slots from invalid Addressables handles. Static scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, and `string.Format`. Managed compile now passes with 0 errors and 0 warnings.

## Decision 10: Pending-Lane Poll Gating
Problem: Even after invalid-handle cleanup, the Tick path still scanned the full Addressables handle array and additive scene operation array every frame whenever arrays existed. On a large authored chunk set, resident handles turned into permanent scan cost.
Solution: Added cold pending counters and a separate `_addressableLoadPending` lane. `PollAddressableLoads`, `PollAddressableCacheClears`, and `TryActivateReadySubScenes` now early-out unless there are active operations. Completed/invalid/released handles decrement the pending counters while ownership handles remain tracked for release.
Rejected Alternatives: Scanning `_hasAddressableHandle` forever was rejected because resident handles are not active load work. Addressables completed callbacks were rejected because callback/delegate ownership is easier to leak and harder to keep deterministic under concurrent agents. A managed queue was rejected because hot streaming must remain array/counter based.
Scalability potential: Low/MX350 avoids O(chunk_count) polling once chunks are resident; High/Ultra can keep more resident chunks and loaded handles without turning that visual richness into per-frame CPU tax.
Hardware Impact: Expected i3/MX350 gain is proportional to authored chunk count: idle polling collapses from full-array scans to integer checks, saving roughly 5-60 us/frame on large chunk tables. Managed compile passes with 0 errors and 0 warnings.

## Decision 11: Adjacent World Warning Cleanup
Problem: `WorldSpatialHashGrid` had a now-dead `RebuildAbsolutePositionsJob` shell after the existing origin-shift rebase path stopped scheduling it. The unused private job field triggered managed compile warnings and kept stale code in the world residency adjacency layer.
Solution: Removed only the dead job struct. Preserved the existing dirty worktree origin-shift behavior, including transient signal rebasing and runtime-position updates, to avoid reverting another agent/user change.
Rejected Alternatives: Reverting the origin-shift edit was rejected because it was pre-existing work outside this narrow warning cleanup. Reworking the whole origin-shift pipeline was rejected as architectural scope creep under the streaming prompt.
Scalability potential: Low/MX350 benefits from a clean compile and less stale Burst code surface; High/Ultra behavior is unchanged.
Hardware Impact: Runtime cost change is 0 us/frame because the job was no longer scheduled. Build hygiene improves to 0 warnings / 0 errors.

## Decision 12: Tiered Load Dispatch Cadence
Problem: The streamer still dispatched one load request per frame on every hardware tier. That protected MX350 but wasted high-tier headroom and could let fast travel outrun queued predictive chunks even after native prioritization sorted them correctly.
Solution: Added scalar tier budgets: Low/MX350 dispatches 1 request/frame, Middle 2, High 3, Ultra 4. Predictive VRAM abort forces the budget back to Low so pressure recovery does not compete with speculative loads. Each request still passes the existing memory guard, VRAM abort, duplicate guard, and Addressables ownership path.
Rejected Alternatives: A single universal 4-request dispatch budget was rejected because it can spike MX350 main-thread asset work. Keeping one request for all tiers was rejected because it creates a flat middle-ground solution and wastes saved cycles on high-end machines. Callback-driven drain was rejected because delegate/callback ownership is harder to audit in the concurrent batch.
Scalability potential: Low stays conservative and predictable. Middle/High/Ultra spend available CPU on smoother high-speed chunk residency, reducing visual pop-in without increasing the Burst scan cost.
Hardware Impact: Low remains unchanged at 1 request/frame. Middle/High/Ultra can reduce queue drain latency by roughly 2x/3x/4x during load bursts; estimated visual benefit is fewer forward-capsule misses during fast submarine travel. A later full managed build passes with 0 warnings and 0 errors; static scan remains clean for streamer hot-path bans.

## Decision 13: Activation Overflow Guard And Teleport Barrier Annotation
Problem: The activation loop trusted authored prefab count and spawned slot capacity to stay aligned forever. A bad chunk definition or stale state could throw during chunk activation and skip cleanup. The teleport path also completed an in-flight residency job without the native-jobs mandate annotation required for rare blocking sync points.
Solution: Added `TelemetryActivationOverflowFlag`, guarded slot writes with an unsigned bounds check, returned overflowed instances to `ObjectPoolManager` immediately, clamped despawn count to slot capacity, and annotated the teleport completion as `[BLOCKING_SYNC_POINT]` because teleport invalidates queued AUP residency data before immediate-radius repopulation.
Rejected Alternatives: Growing the slot array during activation was rejected because it would allocate on an activation path and hide bad authoring. Ignoring overflow was rejected because it leaks spawned pool instances. Removing the teleport completion was rejected because the prompt requires immediate queue invalidation and synchronous radius loading after teleport.
Scalability potential: Low/MX350 turns malformed activation into a bounded pool return and telemetry flag instead of a frame-breaking exception. Middle/High/Ultra keep denser activation lists without converting a single overflow into a failed streaming pass.
Hardware Impact: Normal path cost is one integer bounds check per spawned prefab and 0 B/frame. Fault path saves an exception/failed activation and preserves pool ownership; estimated recovery avoids millisecond-scale exception/unwind cost plus leaked scene objects.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors. Static scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, `string.Format`, unbounded release draining, and synchronous `SceneManager.LoadScene` in the streamer.
Verification Boundary: Unity MCP console read returns `no_unity_session`, so Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 14: Per-Chunk AUP Conversion Removal And Tier Cache
Problem: The recheck found two avoidable hot-path costs after the streamer was already functionally complete: `RadiusBasedStreamingJob` recomputed the player's absolute AUP in every chunk iteration, and queued load dispatch re-read static hardware tier data while draining load bursts.
Solution: Replaced the job's per-execute player AUP conversion with a scheduled `double3 PlayerAbsolute`, and cached `_resolvedTier` once during startup for prediction and dispatch budget decisions. Async upload settings still apply once per resolved tier.
Rejected Alternatives: Leaving the per-chunk conversion was rejected because it burns ALU in the exact Burst loop that scales with authored chunk count. Re-resolving `SystemInfo` during load dispatch was rejected because hardware class is static for the session and the mandate forbids needless hot-path work. Dynamic per-frame tier switching was rejected because scalability changes belong to the hardware dictator/profiler layer, not the streaming hot path.
Scalability potential: Low/MX350 gets the smallest residency scan cost with the same 50m prediction cap and 1-load budget. Middle/High/Ultra keep wider prediction and faster load drain while spending less CPU on repeated coordinate and tier bookkeeping.
Hardware Impact: Estimated i3/MX350 gain is 2-6 us per 512-chunk residency scan from removing repeated player AUP conversion, plus sub-1 us during load burst dispatch by avoiding repeated `SystemInfo` reads. Normal-path GC remains 0 B/frame.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal` succeeds with 0 warnings and 0 errors. Static scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, `string.Format`, and synchronous `SceneManager.LoadScene` in the streamer.
Verification Boundary: Unity MCP resources are visible, but `mcpforunity://instances` reports `instance_count: 0`; Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 15: Duplicate Chunk Id Guard
Problem: Authoring ingest could compute a duplicate deterministic chunk id, then still append the duplicate center/id into the SoA arrays even though the hash maps rejected the duplicate key. That creates ambiguous residency scans sharing one state record across multiple authored centers.
Solution: Added `TelemetryDuplicateChunkIdFlag` and a `_chunkIndexById.ContainsKey(chunkId)` guard before writing to `_chunkIds`, `_chunkCenters`, `_chunkStates`, and `_chunkIndexById`. Duplicate definitions are skipped and recorded in the black-box telemetry ring.
Rejected Alternatives: Allowing duplicate SoA rows was rejected because it makes load/unload decisions nondeterministic. Throwing during ingest was rejected because bad authoring should produce telemetry and let the rest of the chunk table operate. Hash-salting duplicates was rejected because deterministic chunk identity must stay stable for save/load and Addressables ownership.
Scalability potential: Low/MX350 avoids wasted scan slots and malformed duplicate loads. Middle/High/Ultra can keep dense authored chunk tables without one duplicate corrupting residency state.
Hardware Impact: Normal path adds one hash lookup per authored chunk at startup only; runtime Tick cost is 0 us/frame. Fault path saves ambiguous duplicate load/unload work and preserves deterministic state ownership.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal` succeeds with 0 warnings and 0 errors. Static scan remains clean for streamer hot-path bans.
Verification Boundary: Unity MCP still reports no active instance, so Burst/editor verification remains PENDING VERIFICATION.

## Decision 16: Async Activation Generation Guards
Problem: `destroyCancellationToken` only cancels on destruction, not on chunk eviction or manager disable. Activation and predictive prewarm Awaitables could resume after release, then spawn flora/scatter or continue warming prefab pools for content that was no longer resident.
Solution: Added `_activationVersions` as a cold per-chunk generation array. `PromoteChunkResident` captures the current activation generation, `ReleaseChunkHandles` invalidates it, and `ActivateChunkAsync` checks the generation before and after every await and before every spawn. Predictive prewarm now checks its existing generation before warmup work and after every awaited slice.
Rejected Alternatives: Allocating cancellation tokens per chunk was rejected because it adds managed lifetime complexity and allocation pressure. Waiting synchronously for async activation on eviction was rejected because it would create a main-thread stall. Letting stale continuations finish was rejected because it can spawn released content and corrupt pool ownership.
Scalability potential: Low/MX350 avoids wasted pool work after aggressive tail culling and disable/reload churn. High/Ultra can keep heavier predictive prewarm without stale continuations turning high density into leaks.
Hardware Impact: Normal path cost is scalar integer generation checks around existing await boundaries, 0 B/frame. Fault path prevents stale spawned objects and wasted warmup slices; estimated savings are 100-2500 us on evicted prefab-heavy chunks depending on authored pool density.
Build Health: Static scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, `string.Format`, and synchronous `SceneManager.LoadScene` in the streamer. Latest managed build succeeds with 0 warnings and 0 errors after an out-of-domain `HectonPlayerMovement.cs` transient compile error was resolved outside this prompt.
Verification Boundary: Unity MCP console read returns `Unity session not ready`, so Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 17: Release-All Residency Reset And Late Handle Gate
Problem: `ReleaseAllChunks` released spawned instances and asset handles but left the native chunk state map untouched. A disable/re-enable path could therefore leave chunks marked Resident or Loading after their content had been released. A completed Addressables load could also promote a chunk after eviction/disable if the async handle finished late.
Solution: Added `TelemetryReleaseAllResetFlag`, reset each released chunk state back to `Pinned` or `Unloaded`, cleared per-chunk queued load/evict flags, drained native runtime queues and sort scratch, and guarded `PollAddressableLoads` so a completed handle promotes only while the chunk is still `Loading` and not `Evicting`.
Rejected Alternatives: Rebuilding the whole chunk table on every disable was rejected because it would churn native state and risk other agents' origin-shift work. Letting late Addressables completions promote resident content was rejected because it resurrects released visuals and breaks pool ownership. Synchronously cancelling Addressables was rejected because Addressables does not support true cancellation and blocking would violate the asset lifecycle mandate.
Scalability potential: Low/MX350 aggressive tail culling and disable/reload churn now return to deterministic immediate-radius loading instead of stale resident state. Middle/High/Ultra can carry heavier predictive prewarm and larger resident sets without late handles resurrecting old chunks.
Hardware Impact: Normal hot path cost is 0 B/frame and no added Tick scan. Cold release-all path does O(chunkDefinitions) reset work only during disable/dispose, estimated 15-60 us for 512 chunks. Fault path prevents late async promotions, leaked pooled visuals, and stale loading queues after teardown.
Build Health: Static streamer ban scan remains clean for `math.sqrt`, `math.normalize`, coroutine APIs, managed `foreach`, `List.Sort`, `string.Format`, and synchronous `SceneManager.LoadScene`. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors.
Verification Boundary: Unity MCP console read returns `no_unity_session`, so Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 18: Pending Additive Scene Reuse Clears Stale Unload Intent
Problem: If a chunk was released while its additive scene was still loading, `ReleaseChunkHandles` correctly marked the operation to unload once complete. If the same chunk was re-requested before that operation completed, `BeginOrTrackAdditiveSceneLoad` reused the pending operation but left `_additiveSceneUnloadWhenLoaded` true. Completion would unload the scene and strand the chunk in `Loading`.
Solution: When a new request deliberately tracks an existing pending additive scene operation, clear `_additiveSceneUnloadWhenLoaded[index]` before returning `Pending`. The existing operation is then allowed to complete and promote through the normal `TryActivateReadySubScenes` path.
Rejected Alternatives: Starting a second additive scene load was rejected because duplicate scene loads are undefined and can corrupt residency. Blocking until the old operation completes was rejected because it stalls the main thread. Callback-based cleanup was rejected because delegate ownership is harder to audit under concurrent agents and the existing pending-operation poll lane already owns completion.
Scalability potential: Low/MX350 aggressive unload/reload churn no longer traps chunks in Loading when movement reverses or disable/re-enable happens mid-scene-load. High/Ultra can retain larger predictive corridors without additive scene operations poisoning reused load requests.
Hardware Impact: Normal cost is one cold boolean write only when reusing an already-pending additive operation, 0 B/frame. Fault path prevents a dead Loading state that would otherwise block immediate-radius reload and visible chunk activation indefinitely.
Build Health: Static streamer ban scan remains clean. `project.assets.json` was missing after concurrent workspace activity; `dotnet restore Hecton8.Core.csproj` regenerated it, and the exact `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors.
Verification Boundary: Unity MCP console now fails at transport (`http://127.0.0.1:8088/mcp` request failed), so Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 19: Additive Scene Readiness Gates Chunk Promotion
Problem: Additive-scene chunks could be promoted to Resident/Staged before the async additive scene was actually loaded. That let scatter/flora activation race ahead of the structural chunk scene, especially when no Addressables root prefab was configured or when Addressables completed before the scene activation operation.
Solution: Added an explicit `AdditiveSceneLoadState` lane. `DispatchChunkLoad` now starts/tracks the additive scene before resident promotion, returns while the scene is pending, fails closed with `TelemetryAdditiveSceneFaultFlag` if scene tracking is invalid or `LoadSceneAsync` returns null, and only promotes through `TryActivateReadySubScenes` once the additive scene operation is done. Addressables completions now wait behind `IsAdditiveSceneLoadPending`.
Rejected Alternatives: Fixed-frame activation delays were rejected because async scene load timing is nondeterministic. Promoting before the scene finishes was rejected because it allows floating scatter. Blocking on `AsyncOperation` was rejected because it creates a main-thread stall. Starting duplicate additive scene loads was rejected because scene identity and unload ownership become ambiguous.
Scalability potential: Low/MX350 avoids wasted scatter spawns against unloaded structural scenes and keeps load work deterministic. Middle/High/Ultra can author large additive chunks with Addressables payloads without racing visual activation ahead of scene activation.
Hardware Impact: Normal hot path cost is a few cold branch checks during load dispatch and 0 B/frame. Fault path prevents indefinite Loading states, duplicate scene dispatch, and visible scatter in void. Static streamer ban scan remains clean.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors.
Verification Boundary: Unity MCP console still fails at HTTP transport (`http://127.0.0.1:8088/mcp`), so Unity Console, Burst Inspector, and editor validation remain PENDING VERIFICATION.

## Decision 20: Release Path Bounds Hardening
Problem: Cleanup paths were assuming `chunkDefinitions`, Addressables handle arrays, cache-clear arrays, and additive-scene tracking arrays always stayed length-aligned. If authoring or concurrent initialization produced a mismatch, the streamer could throw while already trying to release or recover a chunk.
Solution: Added unsigned bounds guards to `ReleaseChunkHandles`, `RequestAddressablesCacheClear`, `PollAddressableLoads`, `PollAddressableCacheClears`, and `UnloadAdditiveScene`. Poll loops now use the minimum valid paired-array length, cache clear no longer writes to a handle array without checking the matching storage, and `ReleaseChunkHandles` still releases Addressables handles even if authored chunk definitions are unavailable.
Rejected Alternatives: Rebuilding arrays on mismatch was rejected because this is a fault/recovery path and would allocate or churn residency state. Ignoring the mismatch was rejected because an exception in cleanup is worse than dropping speculative work. A managed diagnostic string was rejected because black-box telemetry already carries high-level fault state.
Scalability potential: Low/MX350 release churn from aggressive tail culling is less likely to turn a malformed authoring/runtime mismatch into a hard exception. High/Ultra can keep larger resident sets and more pending operations without cleanup paths assuming perfect array alignment.
Hardware Impact: Normal idle Tick remains 0 B/frame. Active pending-poll cost adds integer min/bounds checks only when pending Addressables/cache-clear work exists; estimated below 1 us for normal pending counts. Static scan remains clean.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors.
Verification Boundary: Unity MCP console still fails at HTTP transport, so Unity Console and Burst Inspector remain PENDING VERIFICATION for this loop.

## Decision 21: Addressables Dispatch Fault Gate
Problem: `DispatchChunkLoad` assumed Addressables tracking arrays were present and length-aligned whenever an authored address existed. If those arrays were missing or short, the dispatcher could index directly or fall through into non-Addressables promotion, marking a chunk resident without its authored payload.
Solution: Added `TelemetryAddressablesFaultFlag` and guarded the authored Addressables lane before any handle writes. Addressed chunks now either start a tracked `LoadAssetAsync`, wait for an existing pending load, promote only from a valid completed handle, or fail closed by releasing any started companion scene/handles and clearing Loading with telemetry. If Addressables support is compiled out, authored Addressables chunks now also fail closed instead of silently promoting without payload.
Rejected Alternatives: Falling back to null-payload promotion was rejected because it hides missing root content. Synchronous load was rejected by the asset lifecycle mandate. Rebuilding Addressables arrays at dispatch time was rejected because it would allocate and mask initialization faults.
Scalability potential: Low/MX350 avoids loading-state corruption during aggressive unload/reload churn. High/Ultra can keep larger Addressables-backed chunk sets without one malformed tracking lane silently promoting empty content.
Hardware Impact: Normal dispatch cost is a few integer bounds checks only when an authored Addressables address exists, 0 B/frame. Fault path prevents empty resident chunks and avoids repeated speculative requests against a broken tracking lane.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors. Static scan and `git diff --check` remain clean except repository CRLF conversion warnings.
Verification Boundary: Unity MCP console still fails at HTTP transport, so Unity Console and Burst Inspector remain PENDING VERIFICATION for this loop.

## Decision 22: Activation Slot Fault Hardening
Problem: Activation and despawn paths assumed activation version/in-progress arrays and spawned-instance slot arrays were always available and length-aligned. A partial initialization or authored/runtime mismatch could throw during activation or cleanup, leaving a chunk Staged forever.
Solution: Added `TelemetryActivationFaultFlag`, guarded activation bookkeeping before starting `ActivateChunkAsync`, guarded spawned-instance arrays before activation/despawn writes, and replaced direct predictive-prewarm array access with `IsPredictivePrewarmBusy`. If activation bookkeeping is invalid, the chunk records telemetry and clears Staged instead of crashing.
Rejected Alternatives: Allocating replacement slot arrays during activation was rejected because it would add managed allocation to a visual activation path. Throwing was rejected because activation faults must be recoverable and diagnosable through black-box telemetry. Spawning without slot ownership was rejected because it leaks pooled objects.
Scalability potential: Low/MX350 aggressive unload/reload and low memory churn recover without exception. High/Ultra can keep larger activation sets while malformed slot state fails closed instead of corrupting pool ownership.
Hardware Impact: Normal activation adds a few integer bounds checks on a cold activation path, 0 B/frame. Fault path avoids exception/unwind cost and leaked pool instances.
Build Health: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false /clp:ErrorsOnly /v:minimal` succeeds with 0 warnings and 0 errors. Static scan and `git diff --check` remain clean except repository CRLF conversion warnings.
Verification Boundary: Unity MCP console still fails at HTTP transport, so Unity Console and Burst Inspector remain PENDING VERIFICATION for this loop.

## Decision 23: Additive Scene Poll Bounds Hardening
Problem: `TryActivateReadySubScenes` still iterated `_additiveSceneOperations.Length` and then directly indexed activation flags, loaded flags, unload flags, chunk definitions, and `_chunkIdsByDefinitionIndex`. A partial initialization or array mismatch could throw during the exact cold poll that is supposed to recover additive scene operations.
Solution: Require every paired additive-scene tracking array plus chunk definitions and chunk-id maps before polling. Iterate only the minimum valid paired length before activation, unload, or promotion writes.
Rejected Alternatives: Rebuilding the tracking arrays during polling was rejected because it allocates in a recovery path and hides initialization faults. Ignoring the mismatch was rejected because exception-based recovery violates the black-box/fail-closed mandate.
Scalability potential: Low/MX350 gets safer aggressive tail-cull unload/reload churn. High/Ultra can keep more pending additive scene operations without one malformed lane breaking the poll loop.
Hardware Impact: Normal pending-scene path adds only integer min/bounds work, estimated below 1 us for typical pending counts and 0 B/frame. `dotnet restore Hecton8.Core.csproj` regenerated missing assets, then the exact managed build succeeded with 0 warnings and 0 errors. `CURRENT_BATCH.md` has rotated and no longer contains this agent prompt; no neighboring prompt content was adopted.
Verification Boundary: Static scan and managed compile pass. Unity MCP console still fails at HTTP transport, so Unity Console and Burst Inspector remain PENDING VERIFICATION.
