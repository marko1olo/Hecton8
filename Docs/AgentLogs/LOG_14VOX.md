# LOG 14VOX

What was wrong: voxel/terrain seam domain had verified contract drift, not a TempJob allocation leak in the mandated files. Static scan found zero `Allocator.TempJob` and zero `new NativeArray` in `HectonVoxelEngine.cs` and `VoxelDeltaProcessor.cs`.

What was done: removed save-service registration from `VoxelDeltaProcessor.Tick`; added save and simulation-bucketer hot-swap caching. Cached `LODSystemManager` in `HectonVoxelEngine`; removed runtime player `TryGetComponent` fallback from voxel LOD/collider AUP resolution; removed a deferred-bake renderer lookup from late-frame teardown. Moved seam dither rendering resource creation to cold startup and made late frame fail closed on missing/changed resources. Reworked MapMagic AUP height/normal/splat/quantized sampling to resolve cached terrain tiles by AUP bounds instead of converting the query through the current runtime origin. Renamed hybrid terrain seam native fields to terrain-local coordinates and updated producer/job consumers.

Cinematic Cheats used: seam dither remains a capped GPU-indirect visual mask; no physical particle simulation was added. Hybrid terrain seam job keeps continuous `GlobalQualityWeight` scaling for expensive raymarch sampling.

Exact Microseconds saved: `VoxelDeltaProcessor.Tick` saves an estimated 0.2-0.6 us/frame while active. Player AUP fallback removal avoids estimated 2-20 us on missing-context frames. LOD cached strike avoids sub-1 us per over-budget strike. Seam dither avoids unbounded graphics-buffer/array allocation spikes in VISUAL_SYNC; exact spike cost depends on driver and cannot be honestly measured without profiler.

Verification: hot-method static extraction over changed files found no direct `GlobalRegistry.Get<T>`, `GetComponent`, `TryGetComponent`, `new NativeArray`, or `Allocator.TempJob` inside `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`. `git diff --check` returned no whitespace errors. Build was not launched: active `dotnet` processes were present and CPU samples were 77.98%, 91.13%, and 100%.

What was wrong: second-pass audit found deferred voxel collider upload was phase-shaped but functionally incomplete. The late-frame queue disabled proxies but did not assign staged meshes to `MeshCollider.sharedMesh`; chunk publication cleared staged meshes instead of swapping them into live collision. `RefreshBakePresentation` could still cold-search components when called from the late-frame upload path. Several write-lock helpers had balanced manual releases on validation failure, but not strict post-acquire `try/finally`.

What was done: `HectonVoxelEngine` now commits root fallback collider `sharedMesh` in `DrainDeferredVoxelColliderUploads` and shutdown flush through one helper. `HectonVoxelVolume.CommitDeferredColliderChunkUpload` now performs a real late-frame staged/live mesh swap and recycles the previous live mesh as the next bake buffer. Runtime component caching moved to cold lifecycle; play-mode bake presentation uses cached references only. `TryAcquireWritableVaultTable`, `TryAcquireBlackBoxBuffer`, `TryAcquireQueuedCarveEventBuffer`, and `TryAcquirePublishedSonarWriteLock` now release failed acquired write locks inside `finally`.

Cinematic Cheats used: collider chunks still use primitive bake proxies until the late-frame sharedMesh budget drains. The proxy path is deliberately cheaper than forcing same-frame PhysX mesh publication.

Exact Microseconds saved: late-frame `TryGetComponent` removal saves estimated 2-15 us on missing-cache paths. Lock restructuring is correctness work; CPU delta is below honest measurement. Deferred collider fix avoids wasted bake/proxy-only collision rather than claiming deterministic frame savings.

Verification: static in-memory parser scanned 1148 methods across changed domain files; `hotHits=0` for direct `GlobalRegistry.Get<T>`, `GetComponent`, `TryGetComponent`, `Allocator.TempJob`, `new NativeArray`, string format, and `.ToString(` in `Tick`, `FixedUpdate`, `LateFrameTick`, and `Execute`. Phase scan for `LateFrameTick`, deferred collider drain/commit, and bake presentation returned `phaseHits=0`. DataVault write-lock scan found 5 helper methods and all report `acquire=1` with `finallyRelease=1`. `rg` confirmed zero `Allocator.TempJob` and zero `new NativeArray` in `HectonVoxelEngine.cs`, `VoxelDeltaProcessor.cs`, and `HectonVoxelVolume.cs`. `git diff --check` returned only line-ending warnings. Build was not launched: `dotnet` PID 13900 was active; CPU samples were 45.42%, 48.06%, and 39.15%.

What was wrong: APEX follow-up found a real lock-order defect in the scheduled carve path. Sliced scheduled carves, job-overrun reporting, chunk-state pool faults, chunk-state dictionary faults, and carve-mass black-box telemetry could write to the black-box DataVault buffer while scheduled carve writes were still guarded.

What was done: added `_deferredScheduledCarveBlackBoxVolumeId` and `_deferredScheduledCarveBlackBoxFlags`; added `DeferScheduledCarveBlackBoxSample`, `ReportBlackBoxSample`, and `FlushDeferredScheduledCarveBlackBoxSample`. Guarded scheduled carve code now only accumulates telemetry flags and flushes after `UnlockScheduledCarveWrites`. Helper fault reports now use `ReportBlackBoxSample`, so normal paths still write immediately and guarded paths defer without allocation.

Cinematic Cheats used: kept scheduled carve slicing as the cheap Math-LOD route instead of forcing full-volume cuts into one frame. Existing collider proxy publication remains the low-tier visual/physics fake while high-tier devices drain more real mesh uploads through continuous quality budget.

Exact Microseconds saved: not profiler-measured. Expected steady-state CPU delta is 0 us; the fix removes lock-order stalls and deadlock risk rather than optimizing arithmetic.

Validation: static source parser scanned 1990 `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute` methods and found `forbiddenHotHits=0` for `GlobalRegistry.Get<T>()`, `GetComponent()`, and `TryGetComponent()`. DataVault write-lock scanner on 8 touched domain files found 6 methods with `TryAcquireWriteLock`; every method had exactly 1 acquire, 1 release path, and a `finally`. Zero-GC scan on touched domain files found no `Allocator.TempJob` and no `new NativeArray`. Delimiter scan returned brace=0, paren=0, bracket=0 for every touched domain file. `git diff --check` returned no whitespace errors, only CRLF normalization warnings. Build was deliberately not launched: active compiler/build processes were `csc` PID 66204, `dotnet` PIDs 21092 and 48116; CPU samples were 100.00%, 95.01%, and 53.29%.

What was wrong: `14VOX` identity state had been contaminated by a neighboring batch number. In code, MapMagic AUP seam sampling still allowed float-compressed `Vector3` routes for canonical geology planning and `FindTerrainAtAUP` wrote `_lastResolvedTerrainTile` from a read accessor.

What was done: corrected `Status_14VOX.md` and `Rationale_14VOX.md` to treat only `14VOX` as the active identity. Added `ITerrainProvider.TryGetHeightAUP(in AbsoluteUniversePosition, out float)`, added canonical AUP overloads to `MapMagicBridge`, implemented double-precision AUP sampling in `MapMagicRuntimeBridge`, and switched `WorldGenerativeGeologyIntegrationDirector` seam planning to the canonical AUP height route. `FindTerrainAtAUP` now scans the prewarmed tile cache without mutating `_lastResolvedTerrainTile`; cache writes remain in owner phase.

Cinematic Cheats used: no new physical terrain solver. This keeps terrain/voxel seam anchoring as a cached MapMagic height sample, then lets voxel arches/caves/rocks/overhangs hide the seam with existing lightweight blend/dither systems.

Exact Microseconds saved: not a CPU optimization. Expected steady-state delta is 0 B/frame and no material CPU gain; the gain is removal of large-coordinate seam drift and hidden read-state mutation.

Validation: static hot-loop parser scanned all `Assets/_Project/Scripts` and returned `forbiddenHotHits=0` for direct `GlobalRegistry.Get<T>()`, `GetComponent<T>()`, and `TryGetComponent<T>()` inside `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`. AUP regression search found no remaining `HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition)` or runtime `TryGetHeight(runtimeWorldPosition.x, runtimeWorldPosition.z)` in the patched MapMagic/geology files. Delimiter scan on the four newly patched C# files returned brace=0, paren=0, bracket=0. `git diff --check` returned only CRLF normalization warnings. Build was not launched: active `csc` PID 30868 and `dotnet` PID 36900 were present, CPU samples were 100.00%, 100.00%, and 100.00%.

What was wrong: terrain seam reconciliation still allocated managed state on first contact with a terrain: `new TerrainApplyState`, `new List<WorldGenerativeGeologySeamPlan>`, and `new List<SeismicTrenchState>` were reachable from `SlowTick` through `EnsureTerrainState`.

What was done: added fixed cold pools in `WorldGenerativeGeologyTerrainSeamApplier` for `TerrainApplyState`, plan buckets, and trench buckets, all sized by `TerrainStateCapacity`. `EnsureTerrainState` now binds terrain IDs to existing buckets through `TryBindTerrainStateBucket`; callers use `TryGetValue` and fail closed if capacity is exhausted. Runtime seam reconciliation no longer grows terrain plan/trench lists.

Cinematic Cheats used: kept the eight-lane terrain seam cap as a Math LOD boundary. Weak devices skip excess terrain seam buckets instead of growing managed state; high/ultra devices still increase visual detail inside existing continuous quality-weight paths.

Exact Microseconds saved: not honestly measurable without profiler. Allocation removed is one managed state object and two managed list objects per newly touched terrain in runtime seam work; expected impact is avoiding GC pressure and allocator spikes during MapMagic tile streaming.

Validation: domain extended hot scan over voxel/MapMagic/geology files including `SlowTick` returned `domainHotExtendedHits=0` for direct `GlobalRegistry`, component lookups, `new List<>`, `new Dictionary<>`, `new TerrainApplyState`, `Allocator.TempJob`, and `new NativeArray` inside named hot phase methods. New patched files show `Allocator.TempJob=0`, `new NativeArray=0`, direct write-lock calls=0, `.Complete()` calls=0. Delimiter scan over the five current patched C# files returned brace=0, paren=0, bracket=0. `git diff --check` returned only CRLF normalization warnings. Build was not launched: CPU samples were 69.08%, 100.00%, and 98.33%, above the 50% throttle.

APEX correction note: a broader repository regex scan returned two component lookup hits outside the 14VOX domain. Manual inspection showed both are cold paths, not hot loops: `HectonCelestialEngine.InitializePlanetShineLight` uses `GetComponent<Light>()` during light initialization, and `OxygenBubble.Awake` uses `GetComponent<Collider>()` during lifecycle setup. The domain scan across voxel/MapMagic/geology files remains zero for forbidden hot-path lookups.

What was wrong: terrain seam DataVault ownership still had a hard defect. `TryIngestSignalHeightmapToVault`, `RefreshTerrainBaseline`, and `RecordTerrainSeamBlackBox` wrote to DataVault buffers opened through the legacy mutable `TryReadHandle` path. Hybrid terrain seam projection also touched multiple DataVault-backed arrays during one scheduled projection window without one explicit ownership guard.

What was done: added a single-buffer writer-fence helper in `WorldGenerativeGeologyTerrainSeamApplier`. Heightmap ingest, baseline refresh, and terrain seam black-box telemetry now acquire exactly one `TryAcquireWriteLock` and release through `finally`. Added one DataVault mutation guard mask around hybrid terrain seam projection for baseline, optional vault heightmap, native plans, patch heights, blend mask, and optional normals. The guard is released in `finally` and also released before black-box telemetry writes, so writer-lock acquisition is not nested behind the projection guard.

Cinematic Cheats used: kept the terrain seam blend as one bounded heightmap projection plus late-frame blend-mask upload. No physical terrain solver or same-frame visual writeback was added. Continuous `GlobalQualityWeight` still controls expensive SDF raymarch detail.

Exact Microseconds saved: this is correctness work. Expected steady-state CPU delta is below honest profiler threshold. The value is removal of a DataVault write ownership violation and compaction/job lifetime hazard.

Validation: domain hot parser scanned 190 `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute`/`SlowTick` methods and returned `domainForbiddenHotHits=0` for direct `GlobalRegistry.Get/Resolve`, component lookups, `new NativeArray`, and `Allocator.TempJob`. Write-lock call scan confirmed `TryIngestSignalHeightmapToVault`, `RecordTerrainSeamBlackBox`, and `RefreshTerrainBaseline` each have one write acquire, one release, and one `finally`; `TryApplyHybridTerrainProjection` has one mutation guard acquire route, release in `finally`, and an explicit pre-telemetry release. Delimiter scan returned brace=0, paren=0, bracket=0 for the six patched C# files. `git diff --check` returned only CRLF normalization warnings. Build was not launched: `csc` PID 66416 and `dotnet` PID 40836 were active, and CPU load was 53%, above the documented 50% throttle.

What was wrong: the first pass removed hot lazy resolution from runtime reconcile methods, but startup-order repair still needed a non-polling route. If MapMagic or VoxelEngine registered after the geology directors, cold startup refresh alone could leave stale null references.

What was done: `WorldGenerativeGeologyIntegrationDirector`, `WorldGenerativeGeologyTerrainSeamApplier`, and `WorldGenerativeGeologyVoxelBridgeDirector` now call `RefreshColdReferences` only from cold lifecycle methods. Runtime reconcile methods no longer call any resolver. MapMagic and VoxelEngine late replacement is cached through `GlobalRegistry` hot-swap callbacks, so the runtime lane does not poll `WorldRuntimeReferenceUtility`.

Cinematic Cheats used: no new terrain physics or procedural solve. The fix preserves existing heightmap sample + voxel seam dither/blend cheats and keeps dependency repair in cold event routing.

Exact Microseconds saved: not profiler-measured. Expected benefit is removal of hidden resolver branches from seam reconciliation; the larger value is deterministic ownership and no scene/static lookup drift.

Validation: direct search shows no `ResolveReferences` or auto-resolve retry fields in the patched geology files. `WorldRuntimeReferenceUtility.TryResolve*` appears only inside `RefreshColdReferences`. Domain hot parser scanned 189 `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute`/`SlowTick` methods and returned `domainForbiddenHotHits=0`. DataVault write-lock helper scan reports `TryAcquireBlackBoxBuffer`, `TryAcquireQueuedCarveEventBuffer`, `TryAcquireWritableVaultTable`, `WriteVoxelMeshPipelineBlackBoxSample`, `TryAcquirePublishedSonarWriteLock`, `TryAcquireTerrainSeamWriteLock`, and `TryAcquireVoxelBridgeBlackBox` each have one acquire and one release with a `finally` in the acquiring helper. Comment/string-aware delimiter scan returned brace=0, paren=0, bracket=0, state=code for all 11 touched C# files. `git diff --check` returned only CRLF normalization warnings. Build was not launched: CPU samples were 100% and then 94%, with `csc` PID 51980 and `dotnet` PID 21164 active on the second throttle check.

What was wrong: `WorldGenerativeGeologySeamExecutionDirector.LateFrameTick` called `ReconcileExecutedSeams`, and that helper still called `ResolveReferences`. The resolver did not appear textually inside `LateFrameTick`, but it executed from VISUAL_SYNC and polled `WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyIntegrationDirector` plus player transform resolution.

What was done: replaced seam execution `ResolveReferences` with `RefreshColdReferences`, called only from `Awake`, `OnEnable`, and `Start`. Removed the obsolete auto-resolve retry field and deleted the helper call from `ReconcileExecutedSeams`. Late-frame seam execution now consumes cached `integrationDirector` and `playerTransform` only.

Cinematic Cheats used: no new physical seam solver. The seam execution path remains a cached plan-to-voxel-request and dither/debris visual route, with expensive visuals controlled by existing continuous quality weights.

Exact Microseconds saved: not profiler-measured. Removed from VISUAL_SYNC: one retry-time read, two retry branches, and possible static runtime reference resolution. The correctness gain is stronger than the arithmetic saving.

Validation: extended hot parser scanned 190 `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute`/`SlowTick`/`Update`/`LateUpdate` methods and returned `domainForbiddenHotHitsPlusUpdate=0`. Hot GC/blocking parser over the same 190 methods returned `domainHotGcBlockingHits=0` for managed collection allocation, native allocation, LINQ materialization, string formatting/interpolation, foreach, and `.Complete()`. Direct search shows no `ResolveReferences`, `autoResolveRetryInterval`, or `_nextAutoResolveAttemptTime` in the four geology runtime directors. Comment/string-aware delimiter scan returned brace=0, paren=0, bracket=0, state=code for the four patched geology files. Build and AST executable were not launched: CPU remained 100% and `dotnet` PID 4592 was active.

What was wrong: one-hop audit found remaining indirect hot lookup routes in the 14VOX boundary. `HectonCaveVoxelAmbientOcclusionController.SlowTick` could resolve viewer/cave references through helpers. `MapMagicRuntimeBridge.SlowTick` called a scene binding helper with a `TryGetComponent` branch, while planetary terrain shader globals and runtime detail maintenance could resolve player transform from runtime/presentation lanes.

What was done: Cave AO now refreshes cave director, viewer transform, and viewer camera only from `Awake`, `OnEnable`, `Start`, and player hot-swap; `SlowTick` reads cached volume data and computes occlusion only. MapMagic bridge now caches player transform from `GlobalRegistry.Player` during cold lifecycle and player hot-swap. Runtime `SlowTick` calls `RefreshRuntimeSceneBindingDiagnostics` instead of a binding resolver. `PublishPlanetaryTerrainShaderGlobals` remains queued from `SlowTick` and executed only in `LateFrameTick`; it no longer resolves player transform. Runtime terrain detail maintenance now fails closed if the cached player transform is absent.

Cinematic Cheats used: no new physical terrain/cave simulation. Cave darkness remains a presentation occlusion ramp over cached cave volumes. Planetary terrain fade remains a deferred shader-global visual mask, queued after biome detection and applied in VISUAL_SYNC.

Exact Microseconds saved: not profiler-measured. Removed potential static/component lookup from Cave AO slow lane, MapMagic slow lane, and terrain shader visual sync. Expected benefit is deterministic cadence and no hidden scene lookup stalls rather than a claimed arithmetic saving.

Validation: narrow in-memory source parser over `MapMagicRuntimeBridge.cs` and `HectonCaveVoxelAmbientOcclusionController.cs` reported `files=2 methods=170 hot=5 directForbidden=0 oneHopForbidden=0 presentationOutsideLate=0 lockShapeFindings=0`. Targeted `rg` shows remaining `TryGetComponent`/`WorldRuntimeReferenceUtility.TryResolve*` only in cold lifecycle helpers: MapMagic `TryResolveCoLocatedMapMagicObject` is called from `Awake`; Cave AO `RefreshColdReferences` is called from `Awake`, `OnEnable`, and `Start`. `git diff --check` returned only CRLF normalization warnings. One throttled `dotnet build .\Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1 --no-restore /p:UseSharedCompilation=false` was launched after CPU dropped below 50% and no compiler process existed; it timed out after 604s without returning compiler diagnostics, and leftover `dotnet` PID 37024 was stopped. A stale repo-wide hot-lookup scanner process was stopped; other agents' `dotnet`/`npm` processes were left untouched.

What was wrong: `WorldCaveDirector.SlowTick` still called runtime reference resolution through `EvaluateCaveSpawns`, and diagnostics repeated the resolver. Cave runtime bounds and dressing material helpers also called `TryGetComponent` on `HectonVoxelVolume`, making repeated cave dressing/bounds paths depend on component lookup instead of the volume owner cache.

What was done: `WorldCaveDirector` now refreshes dependencies only from `Awake`, `OnEnable`, and `Start`; hot-swap updates player, biome matrix, voxel engine, and MapMagic references without polling. `SlowTick` uses `HasRequiredReferences`. `ApplyMineralCrustToVolume`, `CaveRuntimeBoundsUtility`, `CaveWallGrowthRuntimeBuilder`, `CaveServiceRemnantRuntimeBuilder`, `CaveSedimentShelfRuntimeBuilder`, and `CaveGlowingTissueRuntimeBuilder` now read `HectonVoxelVolume.CachedMeshFilter` / `CachedMeshRenderer`.

Cinematic Cheats used: cave dressing stays deterministic primitive/particle dressing over cached volume bounds. No physical cave surface solver was introduced; weak devices skip/limit dressing through existing config/intensity, stronger devices can spend the same path on higher counts.

Exact Microseconds saved: not profiler-measured. Removed possible scene/runtime resolver and component lookup spikes from cave spawn/dressing lanes; expected steady-state gain is small, but worst-case lookup variance is removed.

What was wrong: several DataVault mutation guard acquisition helpers still had manual release or no local failed-acquire `finally` shape. They were balanced by callers in normal execution, but the helper body itself was not edit-proof against future early returns.

What was done: `VoxelDeltaProcessor` scheduled carve write acquisition now uses `keepLock` and `keepScheduledCarveWriteLock` fences; compaction scratch pinning uses `keepGuard`; `HectonVoxelVolume.TryAcquirePublishedSonarPayloadWriteGuard` and `WorldGenerativeGeologyTerrainSeamApplier.TryAcquireHybridTerrainMutationGuard` use `try/finally` release on failed acquired guard. Successful leases are still caller-owned and released by existing caller `finally` blocks.

Cinematic Cheats used: no new simulation. Existing scheduled carve slicing, collider proxy publication, cave dressing primitives, and terrain seam blending remain the low-cost visual cheats.

Exact Microseconds saved: no honest profiler claim. The result is removal of deadlock/leak vectors, not an arithmetic optimization.

Validation: in-memory static C# parser scanned `files=19 methods=1654 hotMethods=52` and returned `directForbiddenHot=0`, `oneHopForbiddenHot=0`, `presentationOutsideLateHot=0`, `lockShapeFindings=0`, `braceBalance=0`, `parenBalance=0`, `bracketBalance=0`. Targeted search shows `WorldRuntimeReferenceUtility.TryResolve*` in `WorldCaveDirector` only inside `RefreshColdReferences`, called from `Awake`, `OnEnable`, and `Start`. Targeted cave builder search shows no remaining `TryGetComponent(out MeshRenderer)` or `TryGetComponent(out MeshFilter)` in the patched cave bounds/dressing files. `git diff --check` returned only CRLF normalization warnings. Build was not launched during the first check because external `dotnet` PID 20592 remained active; after that process exited, the guarded retry skipped before starting `dotnet` because CPU was 57%, above the 50% throttle.
## 14VOX pass 12 - cave visual-sync state transfer

What was wrong:
- `WorldCaveDirector.SpawnEntranceVisualCues` rebuilt cave graph data from `seed/preset/position` inside the `LateFrameTick` drain.
- That visual-sync path allocated `NativeArray<CaveNode>`, `NativeArray<CaveTunnel>`, `NativeArray<CaveEntrance>`, and `NativeArray<CaveStructure>` with `Allocator.Temp`.
- Cave dressing builders used the shared primitive factory path that could call `TryGetComponent` while reached from `LateFrameTick`.

What was done:
- Removed entrance graph regeneration from `LateFrameTick`; entrance visuals now read `HectonVoxelVolume.Entrances`, the already generated volume snapshot.
- Moved entrance hint caching and cave visual runtime state preparation to the one-shot volume generation completion.
- Added `CaveVisualRuntimeState` keyed by `caveKey` to carry prepared marker, quality-zone, roots, geyser, and fungi component references into visual sync.
- Added `WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources()` and hot primitive factory APIs with cached `MeshFilter/MeshRenderer` references.
- Updated cave wall growth, glowing tissue, service remnants, and sediment shelves to call the hot primitive factory APIs.

Cinematic cheats used:
- Presentation reads settled entrance points instead of simulating or refilling the cave graph a second time.
- Cave dressing uses cached primitive mesh/material resources and deterministic object reuse rather than runtime scene repair.

Exact microseconds saved:
- Profiler sample not available under build/runtime throttle.
- Deterministic removed work: 4 transient native allocations plus one duplicate `CaveGraphGenerator.TryFill` per cave visual sync.
- Hot parser proof: `files=16 methods=1272 hotMethods=48 directForbiddenHot=0 oneHopForbiddenHot=0 nativeOrCompleteHits=0 braceBalance=0`.
- Build throttle proof: no `dotnet/csc` process was running, but CPU samples were 95.52/87.40/96.58, so `dotnet build` was not launched.

## 14VOX pass 13 - cave dressing cold-prewarm closure

What was wrong:
- Cave entrance marker fallback still created marker GameObjects, lights, and particle systems from the `LateFrameTick` visual-sync call-chain if prepared state was incomplete.
- Cave dressing primitive pools could skip existing children during prewarm, leaving `WorldGeneratedPrimitiveFactory` hot cache empty after a domain reload.
- Thermal geyser prewarm created only the current intensity count, not the maximum configured capacity.

What was done:
- `WorldCaveDirector.PrepareCaveVisualRuntimeState` now prepares all cave visual roots and optional dressing pools after volume generation: wall growth, glowing tissue, service remnants, sediment shelves, entrance markers, entrance quality, bio-roots, thermal geysers, and fungi.
- `QueueCaveVisualSync` now replaces duplicate cave keys and refuses to grow beyond the fixed queue capacity.
- `SpawnEntranceMarker` now fails closed when marker transform/light/particle references are missing; it no longer creates components in VISUAL_SYNC.
- Cave dressing builder `Prewarm` methods cold-register existing children into `WorldGeneratedPrimitiveFactory` before the late frame can configure them.
- Removed `WorldGeneratedPrimitiveFactory.CreatePrimitiveVisualHot`; hot primitive work is configure-only and reads cached component references.

Cinematic Cheats used:
- Cave richness remains primitive mesh/particle dressing over cached voxel volume bounds, not a physical cave surface simulation.
- Presentation consumes settled `HectonVoxelVolume.Entrances` and prewarmed runtime state; no graph refill, no native temporary buffers, no scene repair.

Exact microseconds saved:
- No profiler sample available.
- Removed worst-case VISUAL_SYNC object/component creation from entrance markers and cave dressing.
- Static proof: `targetFiles=6 targetBlocks=24 forbiddenTargetBlockHits=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=16 hotBlocks=46 directForbiddenHot=0 nativeOrCompleteFileHits=0`.
- DataVault proof: current 6 cave visual files have no DataVault routes; existing 14VOX writer scan returned `tryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: `dotnet/csc` was not active, but CPU samples were `100/93.99/90.96`; `dotnet build` was not launched.

## 14VOX pass 14 - bounded cave buffers and fungi gradient prewarm

What was wrong:
- `GenerateCaveCandidates` could resize `_candidateBuffer` from `SlowTick` if `maxCavesPerBiome` exceeded the cold list capacity.
- Cave cleanup key buffers used unguarded `Add` while enumerating active or pending caves.
- `DeepFungiParticleCache.ResolveGradient` had a lazy `new Gradient()` fallback reachable from the fungi presentation path in `LateFrameTick`.

What was done:
- Candidate count is clamped to `_candidateBuffer.Capacity`, and candidate `Add` is guarded by `Count < Capacity`.
- Stale cave cleanup and pending spawn cancellation buffers now collect keys only within fixed capacity; overflow is deferred to later slow ticks instead of resizing managed storage.
- `DeepFungiParticleCache` now creates its `Gradient` during cold prewarm; late frame uses `TryResolveGradient` and fails closed if state is not prepared.

Cinematic cheats used:
- Cave spawn remains bounded deterministic placement, not a continuous cave simulation.
- Deep fungi remains a particle/gradient presentation fake over cached volume bounds.

Exact microseconds saved:
- Profiler sample not available.
- Static target proof: `targetFiles=6 targetBlocks=28 forbiddenTargetBlockHits=0 addBlocks=4 addBlocksWithoutCapacityGuard=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=16 hotBlocks=46 directForbiddenHot=0 nativeOrCompleteFileHits=0`.
- DataVault proof: `tryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `100/78.78/80.68`; `dotnet build` was not launched.

## 14VOX pass 15 - pending cave spawn allocation purge

What was wrong:
- `TryQueueCaveSpawn` created a `PendingCaveSpawnState` class and a per-spawn `CancellationTokenSource` from the cave spawn slow lane.
- Pending spawn and active cave insertion did not fail closed before hitting their intended fixed capacities.

What was done:
- `PendingCaveSpawnState` is now a readonly struct with a version token.
- `SpawnCaveAsync` captures the cold lifecycle cancellation token and validates the pending version on return.
- `TryQueueCaveSpawn` refuses new work when active or pending cave capacity is reached.
- `CancelAllPendingSpawns` removes bounded pending keys without disposing per-spawn objects.

Cinematic cheats used:
- Cave generation remains bounded event-driven world dressing, not a continuous procedural solver.
- Cancellation is coarse lifecycle cancellation plus versioned acceptance, avoiding per-spawn managed cancellation objects.

Exact microseconds saved:
- Removes one managed state object and one `CancellationTokenSource` allocation per queued cave spawn.
- Static target proof: `targetFiles=6 targetBlocks=30 forbiddenTargetBlockHits=0 mutationBlocks=5 mutationBlocksWithoutCapacityGuard=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=16 hotBlocks=46 directForbiddenHot=0 nativeOrCompleteFileHits=0`.
- DataVault proof: `tryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `55.9/39.13/51.12`; `dotnet build` was not launched.

## 14VOX pass 16 - cave preset ownership and generated-volume component route

What was wrong:
- `WorldCaveDirector` handed static mutable biome `CavePreset` templates into async generation and then stored that same reference on every active cave.
- `SpawnCaveAsync` used `TryGetComponent(out HectonVoxelVolume)` after the volume generation await.
- `CavePreset.Clone()` claimed deep copy while sharing `allowedStructureTypes`.

What was done:
- Added fixed cold runtime preset slots in `WorldCaveDirector`; each cave gets a copied preset and exact-length preallocated structure-type array.
- Runtime preset slots are released on failed async completion or active cave removal, not during pending cancellation while an async generation can still read the preset.
- Captured the generating `HectonVoxelEngine` across await and resolved the generated `HectonVoxelVolume` through `TryGetRegisteredVolumeComponent`, a pure active registry read.
- `CavePreset.Clone()` now duplicates the structure-type array.

Cinematic cheats used:
- Cave preset selection remains a cheap biome template copy, not per-cave procedural material/profile synthesis.
- Cave completion reads the engine registry populated during generation instead of repairing scene/component state.

Exact microseconds saved:
- Removes one runtime component lookup per successful cave spawn.
- Avoids clone-per-spawn managed allocations by using cold slots.
- Static target proof: `targetFiles=8 hotBlocks=23 forbiddenHotHits=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=110 hotBlocks=135 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `90/79/82`; `dotnet build` was not launched.

## 14VOX pass 17 - cave loot spawn dependency cache

What was wrong:
- `HectonVoxelEngine.RegisterPipelineSpawnPoints` used `WorldRuntimeReferenceUtility.TryResolveScavengePopulator` after async cave generation.
- That route was not a frame tick, but it was still a runtime resolver/global fallback dependency inside the cave generation completion lane.

What was done:
- Added cached `_scavengePopulator` to `HectonVoxelEngine`.
- Bound it from `GlobalRegistry.ScavengePopulator` during cold service caching.
- Updated it through `GlobalRegistryServiceSlot.ScavengePopulatorRuntime` hot-swap.
- `RegisterPipelineSpawnPoints` now reads the cached reference only and fails closed when absent.

Cinematic cheats used:
- Cave loot points remain lightweight spawn requests emitted after voxel generation, not a live scene search.

Exact microseconds saved:
- Removes one resolver/global fallback branch per cave spawn-point registration batch.
- Static target proof: `targetFiles=8 hotBlocks=23 forbiddenHotHits=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=110 hotBlocks=135 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `93/73/57`; `dotnet build` was not launched.

## 14VOX pass 18 - generated cave rollback and terrain-hole release

What was wrong:
- `GenerateVolumeAsync` could register a generated cave volume as active, then return `null` if spawn-point scratch registration failed.
- `RegisterActiveVolume` silently refused registration under capacity/local-bounds pressure.
- `DespawnVolume` returned volumes to pool without immediate terrain-hole unregister because `HectonVoxelVolume.OnDisable` does not release terrain-hole handles.

What was done:
- Both generated cave paths now despawn `targetGO` if active-volume registration or spawn-point registration fails.
- `RegisterActiveVolume` now returns `bool`; generation rollback is explicit.
- `DespawnVolume` and `ClearAllVolumes` now call `HectonVoxelVolume.PrepareForReuse()` before pool return or destruction.

Cinematic cheats used:
- Cave entrance terrain holes remain registry masks with deterministic handles, not live terrain resimulation.
- Rollback keeps the cave volume lifecycle single-owner: generated, registered, or removed.

Exact microseconds saved:
- Steady-state CPU saving not profiler-measured.
- Removes retained mesh/collider/terrain-hole work after failure paths.
- Static target proof: `targetFiles=9 hotBlocks=26 forbiddenHotHits=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=110 hotBlocks=135 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `84/54/67`; `dotnet build` was not launched.

## 14VOX pass 19 - vegetation bridge dependency cache

What was wrong:
- `HectonVoxelEngine.ExecuteVoxelPipelineAsync` read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`.
- `RegisterEntranceTerrainHoles` also read `ActiveRuntimeInstance`.
- Those are runtime generation paths and should use cached owner identity, not static active-instance lookup.

What was done:
- Added cached `_vegetationBridge` to `HectonVoxelEngine`.
- Bound it from cold `GlobalRegistry.MapMagicVegetation`.
- Updated it through `GlobalRegistryServiceSlot.MapMagicVegetationRuntime`.
- Voxel pipeline height/biome fill and terrain-hole registration now use the cached bridge.

Cinematic cheats used:
- Cave entrance terrain holes remain a cached vegetation bridge mask path, not scene/terrain search.

Exact microseconds saved:
- Removes two static runtime owner lookups per cave generation pipeline.
- Static target proof: `targetFiles=9 hotBlocks=26 forbiddenHotHits=0 nativeOrCompleteFileHits=0`.
- Domain hot proof: `domainFiles=110 hotBlocks=135 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Build throttle proof: CPU samples were `100/100/99`; `dotnet build` was not launched.

## 14VOX pass 20 - chthonic resource-distribution dependency cache

What was wrong:
- `HectonVoxelEngine.TryBindSelectedChthonicPillarResources` read `ResourceDistributionDirector.ActiveRuntimeInstance`.
- The call sits after voxel anomaly selection in the runtime pipeline, so it was still a hidden static owner lookup in 14VOX generation.

What was done:
- Added cached `_resourceDistributionDirector` to `HectonVoxelEngine`.
- Bound it from cold `GlobalRegistry.ResourceDistribution`.
- Updated it through `GlobalRegistryServiceSlot.ResourceDistributionRuntime`.
- `TryBindSelectedChthonicPillarResources` now receives the cached director explicitly and fails closed if absent.

Cinematic cheats used:
- Chthonic pillar resources stay a cheap halo bind around the selected voxel anomaly, not a scene search or terrain resimulation.

Exact microseconds saved:
- Removes one static runtime owner lookup per chthonic pillar voxel pipeline.
- Static target proof: `targetFiles=9 hotBlocks=44 forbiddenHotHits=0`.
- Domain hot proof: `domainFiles=103 hotBlocks=150 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=15 withoutReleaseOrFinallyNearby=0`.
- Direct lookup proof: zero `ResourceDistributionDirector.ActiveRuntimeInstance`, zero `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, and zero `WorldRuntimeReferenceUtility.TryResolveScavengePopulator` in patched 14VOX cave/voxel files.
- Build throttle proof: `dotnet` PID 68252 was active and CPU load was 76%; `dotnet build` was not launched.

## 14VOX pass 21 - voxel engine self-singleton hot dependency purge

What was wrong:
- `HectonVoxelEngine` still read `ActiveRuntimeInstance` from rare hot/runtime helper branches.
- Affected paths were predictive proxy dampening, VRAM pressure collider fake selection, and voxel rebuild over-budget LOD strike.

What was done:
- Collider fake selection now reads instance `_vramPressureReadModel`.
- Rebuild budget strike now reads instance `_lodSystemManager`.
- Predictive proxy dampener reads `s_predictiveVoxelProxyPhysicsService`, published by the active engine during `OnEnable` and physics hot-swap, and cleared during teardown.
- Direct `HectonVoxelEngine activeEngine = ActiveRuntimeInstance` reads were removed from patched 14VOX files.

Cinematic cheats used:
- The collider fake stays a distance/pressure fake, not a heavier PhysX fidelity path.
- Predictive proxy dampening remains a cheap velocity clamp queued through the cached physics service.

Exact microseconds saved:
- Removes three static owner reads from late-frame/pipeline emergency branches.
- Static target proof: `targetFiles=9 hotBlocks=44 forbiddenHotHits=0`.
- Domain hot proof: `domainFiles=103 hotBlocks=150 directForbiddenHot=0`.
- DataVault proof: `domainTryAcquireWriteLockCalls=15 withoutReleaseOrFinallyNearby=0`.
- Direct lookup proof: zero `HectonVoxelEngine activeEngine = ActiveRuntimeInstance` in patched 14VOX files.
- Build throttle proof: `csc` PID 40372 and `dotnet` PID 68252 were active and CPU load was 100%; `dotnet build` was not launched.

## 14VOX pass 22 - seam execution registry cache and bounded VISUAL_SYNC

What was wrong:
- `WorldGenerativeGeologySeamExecutionDirector` could read `SeamRegistry.ActiveRuntimeInstance` from the seam apply/cleanup path reached by `LateFrameTick`.
- Runtime key carry-over used `AddRange`.
- Runtime, selection, and cleanup lists trusted initial capacity instead of enforcing it.

What was done:
- Added cached `SeamRegistry` ownership to the director and refreshed it only from cold lifecycle.
- `ApplySeam` and cleanup use the cached registry and fail closed when it is absent.
- Replaced retained-key `AddRange` with bounded indexed copy.
- Guarded runtime selection, stale-runtime, and voxel-request list inserts by fixed capacity.

Cinematic cheats used:
- Seam dither and primitive presentation stay deferred to VISUAL_SYNC; the simulation truth remains the settled integration plan.
- Overflow skips extra visual seam work instead of growing managed storage.

Exact microseconds saved:
- Removes one static registry read path from seam visual reconciliation.
- Removes rare managed list growth from retained/selection buffers.
- Static target proof: `targetFiles=12 hotBlocks=50 forbiddenHotHits=0`.
- One-hop proof: `files=2 methods=172 hotMethods=4 forbiddenMethods=17 indirectForbiddenHotFindings=0`.
- Build throttle proof: CPU load was 55%; `dotnet build` was not launched.

## 14VOX pass 23 - voxel bridge bounded queues and resource cache

What was wrong:
- `WorldGenerativeGeologyVoxelBridgeDirector` could resize pending, queued, desired, removal, cancellation, and active runtime collections during burst request reconciliation or async completion.
- Queued launch order kept canceled stale keys until dequeue and could fill the fixed list.
- Hydrothermal vent binding read `ResourceDistributionDirector.ActiveRuntimeInstance` from runtime completion.

What was done:
- Added fail-closed capacity guards for pending request state, queued launch dictionaries, queued key order, desired runtime key order, active runtime dictionaries, removal buffers, and cancellation buffers.
- Added in-place queue compaction for stale queued keys.
- Converted full cancel/clear paths to fixed-size batch loops.
- Added cached `_resourceDistributionDirector`, filled from cold `GlobalRegistry.ResourceDistribution`, and updated from `ResourceDistributionRuntime` hot-swap.

Cinematic cheats used:
- Voxel bridge overflow drops optional runtime visual geology work rather than reallocating.
- Hydrothermal resource flavor stays a cached owner call, not a scene/static search.

Exact microseconds saved:
- Removes rare collection resize spikes in `ReconcileVoxelRequests`.
- Removes one static owner lookup per hydrothermal voxel completion.
- Target DataVault proof: `targetTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.
- Delimiter proof: seam execution and voxel bridge both reported `brace=0 paren=0 bracket=0`.
- `git diff --check` returned only CRLF warnings.
- Build throttle proof: CPU load was 55%; `dotnet build` was not launched.

## 14VOX pass 24 - geology binding and integration plan capacity

What was wrong:
- `WorldGenerativeGeologyBinding` could grow static active/known registries and stale-index staging.
- `CopyActiveBindingsTo` and `CopyKnownBindingsTo` could grow caller destination lists.
- `WorldGenerativeGeologyIntegrationDirector` trusted `maxTrackedPlans` above its prewarmed 256-plan storage and used `AddRange`.

What was done:
- Added fixed binding registry capacity and guarded active/known registration.
- Guarded stale-index staging and destination copies by existing capacity.
- Clamped tracked plan capacity to `PlanRuntimeKeyCapacity`.
- Replaced `AddRange` in integration copying/stabilization with bounded loops.
- Added `TryUpsertPlan` so plan dictionaries/lists mutate only after capacity checks.

Cinematic cheats used:
- Geology plan overflow drops optional seam candidates instead of growing managed storage.
- Existing ordered-plan retention remains, so visible seams do not churn when quality/distance shifts slightly.

Exact microseconds saved:
- Removes rare list/dictionary resize spikes in geology binding/planning.
- Static target proof: `targetFiles=10 hotBlocks=44 forbiddenHotHits=0`.
- One-hop proof: `files=5 methods=433 hotMethods=8 forbiddenMethods=32 indirectForbiddenHotFindings=0`.
- Target DataVault proof: `targetTryAcquireWriteLockCalls=8 withoutReleaseOrFinallyNearby=0`.

## 14VOX pass 25 - voxel volume cached lifecycle routes

What was wrong:
- `HectonVoxelVolume.ProcessQueuedRebuildsAsync` fell back to `HectonVoxelEngine.ActiveRuntimeInstance`.
- `HectonVoxelVolume.UnregisterTerrainHoles` read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`.

What was done:
- Added cached `HectonVoxelEngine` and `HectonMapMagicVegetationBridge` routes.
- Filled them from cold `GlobalRegistry` and hot-swap.
- Queued rebuild and terrain-hole cleanup now use cached fields only and fail closed if absent.

Cinematic cheats used:
- Terrain holes remain cheap cached masks; no terrain resimulation or scene lookup is introduced.
- Rebuild failure remains fail-closed instead of reaching for a global owner.

Exact microseconds saved:
- Removes two runtime singleton fallback reads from voxel volume lifecycle/rebuild paths.
- Direct targeted grep found zero `HectonVoxelEngine.ActiveRuntimeInstance` and zero `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` in `HectonVoxelVolume`.
- `git diff --check` returned only CRLF warnings.
- Build throttle proof: CPU load was 34% and no compiler process was active, but `dotnet build` was intentionally not launched because this pass used in-memory static validation per APEX throttling request.

## 14VOX pass 26 - magma-vein organic owner cache

What was wrong:
- `HectonVoxelVolume.ApplyMagmaVeinSpline` read `DestructibleOrganicManager.ActiveRuntimeInstance`.
- That made voxel deformation reach across into organic ownership through a runtime singleton fallback.

What was done:
- Added cached `_cachedOrganicManager`.
- Filled it from cold `GlobalRegistry.OrganicToolHits` and `DestructibleOrganicRuntime` hot-swap.
- Magma-vein burn uses the cached owner and fails closed if absent.

Cinematic cheats used:
- Flora burn remains optional visual/gameplay flavor around the voxel magma vein; voxel SDF truth does not depend on organic owner availability.

Exact microseconds saved:
- Removes one active singleton read per magma-vein burn batch.
- Targeted grep over `HectonVoxelVolume` returned zero `HectonVoxelEngine.ActiveRuntimeInstance`, zero `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, and zero `DestructibleOrganicManager.ActiveRuntimeInstance`.
- Static target proof: `targetFiles=5 hotBlocks=8 forbiddenHotHits=0`.
- `git diff --check` returned only CRLF warnings.

## 14VOX pass 27 - seam dither/navgrid owner cache and lock flattening

What was wrong:
- Seam dither visual sync and biome/flora helpers still had active singleton reads.
- Voxel navgrid hybrid navigation and obstacle reads still used active vegetation bridge fallback.
- Navgrid build and dynamic-clear scheduling held 2-3 DataVault writer locks across scheduled jobs.

What was done:
- Cached vegetation bridge through cold `GlobalRegistry.MapMagicVegetation` and hot-swap.
- Cached geology integration director through cold resolution only.
- Updated voxel navgrid lifecycle to publish/clear cached vegetation bridge.
- Replaced simultaneous navgrid writer locks with one mutation guard mask stored on the record and released in completion/failure `finally`.

Cinematic cheats used:
- Seam/flora-root dust remains presentation dither; no physical terrain or vegetation truth is resimulated.
- Navgrid obstacle integration remains bounded runtime data, not scene polling.

Exact microseconds saved:
- Removes singleton lookup paths from seam visual sync and hybrid nav sampling.
- Removes navgrid multi-lock deadlock vector; CPU delta was not profiler-measured.
- Static source proof: `targetFiles=28 methods=2441 hotBlocks=76 directForbiddenHot=0 oneHopForbiddenHot=0`.
- DataVault proof: `writeLockMethods=7 findings=0`.
- Delimiter proof: patched files returned zero brace/paren/bracket imbalance.
- `git diff --check` returned only CRLF warnings.
- Build throttle proof: active `dotnet` PID 7108 and CPU 50.41%; `dotnet build` was not launched.

## 14VOX pass 28 - navgrid snapshots, seam copy, MapMagic cold resources

What was wrong:
- Navgrid obstacle schedules allocated `NativeArray<NavObstaclePrimitive>` with `Allocator.TempJob`.
- Navgrid read accessors could write telemetry while mutation guards were active.
- Seam visual sync could grow caller lists through `SeamRegistry.CopyStatesTo`.
- MapMagic visual sync and slow tick could allocate terrain shadow and biome cache resources.

What was done:
- Added fixed persistent obstacle snapshot lease pool and `ObstacleCount` job fields.
- Made `TryResolveNavGridRead/Mutable` pure handle-resolution helpers.
- Bounded seam and cave-entrance copies by caller list capacity.
- Prewarmed MapMagic distant-shadow and biome cache storage in cold lifecycle; runtime phases now fail closed instead of resizing.

Cinematic cheats used:
- Distant terrain darkness remains a cheap prewarmed texture fake, not a terrain lighting simulation.
- Seam overflow drops optional visual state instead of allocating.

Exact microseconds saved:
- Removes two TempJob native allocations from navgrid obstacle build/update scheduling.
- Removes rare Texture2D/Color32/TerrainLayer/list allocation spikes from VISUAL_SYNC/SlowTick.
- Static source proof: `targetFiles=19 methods=1294 hotBlocks=50 directForbiddenHot=0 oneHopForbiddenHot=0`.
- DataVault proof: `dataVaultWriteLockMethods=2 findings=0`.
- Delimiter proof: patched files returned zero brace/paren/bracket imbalance.
- `git diff --check` returned only CRLF warnings.
- Build throttle proof: active `dotnet` PID 34320 and CPU 88.97%; `dotnet build` was not launched.
2026-05-30 14VOX pass 29

What was wrong:
- `HectonBiolumController.SlowTick` had an indirect player `TryGetComponent` fallback through survival binding.
- `MapMagicRuntimeBridge.SlowTick` had an indirect terrain hierarchy traversal path through `RefreshTerrainTileCache(false)` and `GetComponentsInChildren<TerrainTile>`.

What was done:
- Cached `IPlayerRuntimeContext` in `HectonBiolumController` from cold `GlobalRegistry.Player` and `GlobalRegistryServiceSlot.Player`; survival depth reads now use `playerContext.SurvivalSystem`.
- Split `MapMagicRuntimeBridge` terrain-tile refresh into cold hierarchy scan and hot owner-phase validation; slow tick now compacts existing tile references only.
- Added fixed `TerrainTileCacheCapacity` and event-driven tile cache insertion with no runtime list growth.

Cinematic cheats used:
- Kept MapMagic terrain/biome response as cached terrain snapshots instead of live scene scans.
- Kept biolum depth response as a cached survival signal, not a scene query.

Exact microseconds saved:
- Not profiler-measured. Static proof: target parser `targetFiles=4 methods=348 hotBlocks=14 directForbiddenHot=0 oneHopForbiddenHot=0`.
- Domain proof: same-file parser `sameFileDomainFiles=227 methods=9716 hotBlocks=342 directForbiddenHot=0 oneHopForbiddenHot=3`; remaining findings are flora/sargassum ownership, not 14VOX voxel-heightmap authority.
- Build not launched: `dotnet` PID 34320 active and CPU load was 100%.

2026-05-30 14VOX pass 30

What was wrong:
- `MapMagicRuntimeBridge.SlowTick` still refreshed biome alpha texture and terrain layer handle caches unconditionally for the last resolved terrain.
- That made `terrainData.terrainLayers` and `GetAlphamapTexture` owner-phase polling steady-state work instead of invalidation-driven cache repair.

What was done:
- Changed `PrewarmBiomeAlphaTextureCacheOwnerPhase` so it first uses pure cache readers.
- Alpha textures refresh only when cached `TerrainData` or expected texture count is invalid.
- Terrain layers refresh only when cached `TerrainData` or expected layer count is invalid.

Cinematic cheats used:
- Terrain splat color remains a cached biome handle approximation for visual integration; no physical biome resimulation or scene scan is added.

Exact microseconds saved:
- Not profiler-measured. Static proof: target parser `targetFiles=5 methods=449 hotBlocks=15 directForbiddenHot=0 oneHopForbiddenHot=0`.
- Domain proof: parser `domainFiles=96 methods=4094 hotBlocks=144 directForbiddenHot=0 oneHopForbiddenHot=0`.
- DataVault proof: `dataVaultWriteLockMethods=2 findings=0`.
- Delimiter proof: five touched files returned zero brace/paren/bracket imbalance.
- `git diff --check` returned only CRLF warnings.
- Build not launched: active `dotnet` PID 56788 and CPU samples were 100/100/100.

2026-05-30 14VOX pass 31

What was wrong:
- `ShinobuVoxelSculptorWindow.TryWriteTuningToVault` created the carve-debris job-state handle under `SystemID.Vfx` but attempted to lock and release it as `SystemID.CoreDiagnostics`.
- `VoxelMemorySovereigntyValidator1304.RunDefragRaceFuzzer` held carve-event and density write locks simultaneously, violating the lock-flattening rule it should enforce.

What was done:
- Repaired the voxel sculptor vault write owner to `SystemID.Vfx` end-to-end and moved validation failure under the single `try/finally` release scope.
- Split the voxel memory fuzzer into two single-lock windows: carve write, release, density write, release.

Cinematic cheats used:
- None. This pass was DataVault/editor-tool correctness only.

Exact microseconds saved:
- Runtime frame impact not claimed; both fixes are editor/validation tooling.
- Static proof: target parser `targetFiles=7 methods=485 hotBlocks=15 direct=0 onehop=0`.
- DataVault proof: `domainWriteLockMethods=13 findings=0`.
- Delimiter proof: patched files returned zero brace/paren/bracket imbalance.
- `git diff --check` returned only CRLF warnings.
- Build not launched: active `dotnet` PID 29396 and CPU samples were 91.25/78.77.

2026-05-30 14VOX pass 32

What was wrong:
- `WorldProceduralFieldSampler.PrepareBurstData` still owned runtime dependency repair for procedural field data.
- The call path could resolve MapMagic/zone/biome/cave owners while scatter/heightmap sampling was preparing Burst data.

What was done:
- Removed dependency repair from `PrepareBurstData`.
- Added active-owner change events to `WorldCaveDirector` and `WorldZoneDirector`.
- Registered `WorldProceduralFieldSampler` as a hot-swap listener for Player, MapMagic, and BiomeMatrix slots.
- Cold lifecycle repair remains in `RefreshColdReferences`; hot sampling data-prep only consumes cached references.

Cinematic cheats used:
- Cave entrance influence remains a cheap cached hint field in the procedural sampler; no runtime cave volume query or physical seam resimulation was added.

Exact microseconds saved:
- Not profiler-measured.
- Static target proof: `targetFiles=3 methods=296 hotRoots=7 findings=0`.
- Static domain proof: `domainFiles=91 methods=3218 hotRoots=121 findings=0`.
- DataVault proof: `domainWriteLockMethods=6 findings=0` with max held writer count <= 1.
- `git diff --check` returned only CRLF warnings.
- Build not launched: active `dotnet` PID 17292 and CPU sample was 100%.
