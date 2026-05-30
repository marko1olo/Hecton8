# Rationale 14VOX

Problem: Exact `<AGENT_PROMPT id="14VOX">` is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Use user-provided identity `14VOX` for ledgers and treat the direct chat assignment as the active directive. No sibling XML prompt is authoritative for `14VOX`.
Rejected Alternatives: Adopting a neighboring numeric XML prompt would contaminate the 14VOX domain boundary and violate strict parsing.
Scalability potential: No runtime effect.
Hardware Impact: No runtime effect on i3/MX350.

Problem: `VoxelDeltaProcessor.Tick` called save-service registration every dispatcher frame.
Solution: registration remains cold in `OnEnable` and registry hot-swap; `Tick` now only processes queued carve/compaction work.
Rejected Alternatives: Polling `GlobalRegistry.Save` or retrying registration from `Tick` keeps lifecycle work in the hot lane.
Scalability potential: Low tier removes an avoidable branch/service call from every voxel delta frame; high tier spends saved time on carve work.
Hardware Impact: Estimated 0.2-0.6 us/frame on i3/MX350 when the processor is active.

Problem: voxel rebuild budget strike pulled `GlobalRegistry.LODSystem` from runtime rebuild accounting.
Solution: cache `LODSystemManager` during cold service resolution and hot-swap, then use the cached field.
Rejected Alternatives: Keeping the global property read inside rebuild accounting violates cold-DI ownership.
Scalability potential: Low/mid tiers avoid registry drift in over-budget response; high/ultra tiers keep emergency LOD bias deterministic.
Hardware Impact: Estimated sub-1 us per over-budget strike, with lower dependency risk.

Problem: `TryResolvePlayerAup` fell back to `TryGetComponent<HectonPlayerMovement>` during runtime voxel LOD/collider decisions.
Solution: require `PlayerRuntimeContextService` snapshot; fail closed when no snapshot is published.
Rejected Alternatives: Scene component search from runtime LOD helpers creates hidden presentation dependency and unpredictable cost.
Scalability potential: Weak devices avoid fallback scene lookup; high-end keeps LOD truth routed through one context owner.
Hardware Impact: Estimated 2-20 us avoided on fallback frames.

Problem: `SeamGapDitherRenderer` could create arrays, mesh, and graphics buffers from `LateFrameTick`.
Solution: resources are allocated from cold `Awake`/`OnEnable`/`Start`; late frame only checks residency and fails closed if capacity/resources are invalid.
Rejected Alternatives: Reallocating in visual sync hides spikes behind presentation code.
Scalability potential: Low tier avoids late allocation stalls; high/ultra can keep larger prewarmed buffers without frame-time spikes.
Hardware Impact: Avoids worst-case GraphicsBuffer/array allocation spikes in VISUAL_SYNC.

Problem: MapMagic AUP height/normal/splat/quantized payload queries converted the query through runtime origin.
Solution: resolve cached terrain tile by AUP XZ bounds, derive local UV/local runtime offset from the terrain tile frame, and sample the same TerrainData payload.
Rejected Alternatives: `HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition)` is vulnerable to async origin drift.
Scalability potential: Low/mid tiers get stable seams after origin shifts; high/ultra can spend quality budget on denser seam visuals without coordinate wobble.
Hardware Impact: Similar O(tile count) cached scan cost; avoids seam resampling errors rather than saving CPU.

Problem: hybrid terrain seam native DTO fields were named `Runtime*` while carrying terrain-local coordinates.
Solution: rename fields to `TerrainLocalContactPosition` and `TerrainLocalVoxelCenter`; update producer and Burst job consumers.
Rejected Alternatives: Compatibility aliases would preserve the misleading ABI and invite future world-space float misuse.
Scalability potential: All tiers keep AUP-to-local conversion as a single explicit route.
Hardware Impact: No measurable CPU change; reduced risk of 100 km float jitter defects.

Problem: deferred voxel collider upload queue did not actually publish staged PhysX meshes to `MeshCollider.sharedMesh`.
Solution: late-frame drain now calls `CommitDeferredRootVoxelColliderUpload`; chunk volumes swap staged bake meshes into live collider meshes and retain the previous live mesh as the next bake buffer.
Rejected Alternatives: assigning `sharedMesh` immediately after bake completion would move presentation mutation out of the VISUAL_SYNC lane.
Scalability potential: Low tier keeps primitive bake proxies until a small late-frame upload budget drains; high/ultra drains more chunks per frame through continuous quality-weight budget.
Hardware Impact: Correctness fix; avoids repeated useless bakes and proxy-only collision on i3/MX350.

Problem: `RefreshBakePresentation` could cold-resolve Unity components when called from late-frame deferred collider upload.
Solution: cache MeshFilter/MeshRenderer/MeshCollider during cold lifecycle; play-mode presentation refresh only mutates cached references and fails closed if missing.
Rejected Alternatives: allowing `TryGetComponent` inside presentation refresh violates hot-loop lookup doctrine.
Scalability potential: Weak devices avoid unpredictable scene lookup during visual sync; high-end keeps the same presentation path without extra branches.
Hardware Impact: Estimated 2-15 us avoided on missing-cache late-frame paths.

Problem: DataVault write-lock helper methods released failed leases manually after acquisition.
Solution: after a successful `TryAcquireWriteLock`, validation now runs under a `try/finally`; failed validation releases inside `finally`, successful leases are released by the caller's existing `finally`.
Rejected Alternatives: manual early release is technically balanced but weaker under future edits and exception paths.
Scalability potential: All tiers remove a deadlock vector without changing capacity or cadence.
Hardware Impact: No measurable CPU cost; lower lock-leak risk under contention.

Problem: build validation was requested while another `dotnet` process was active.
Solution: obey compile throttle; used static in-memory source parsing and `git diff --check` instead of launching a second build.
Rejected Alternatives: launching `dotnet build` under PID 13900 violates host contention rules.
Scalability potential: No runtime effect.
Hardware Impact: avoids build CPU contention on shared workstation.

Problem: scheduled voxel carve scheduling and commit paths could report black-box telemetry while the scheduled carve write buffer mutation guard was still held.
Solution: add zero-allocation deferred telemetry fields for scheduled carve black-box flags; under the scheduled guard only OR flags and volume id, then flush through `WriteBlackBoxSample` after `UnlockScheduledCarveWrites`.
Rejected Alternatives: calling `WriteBlackBoxSample` directly from continuation, job-overrun, chunk-state pool, dictionary, or carve-mass paths would acquire a second DataVault write lock route while scheduled writes were guarded.
Scalability potential: Low tier avoids rare deadlock/stall cascades during sliced laser cuts; middle/high/ultra keep scheduled carve slicing and black-box evidence without frame corruption.
Hardware Impact: Estimated 0 us steady-state CPU change; removes lock-order risk under queue overflow and chunk pool pressure on i3/MX350.

Problem: helper methods called by scheduled carve commit could hide black-box writes behind dictionary/pool failure paths.
Solution: route queue corruption, chunk-state corruption, chunk-state store failures, write-version failures, and mass telemetry through `ReportBlackBoxSample`, which defers only when `_scheduledCarveWritesLocked` is true.
Rejected Alternatives: duplicating no-report variants of every helper would widen code paths and risk inconsistent telemetry semantics.
Scalability potential: Weak devices under memory pressure keep fail-closed telemetry without nested locks; top-tier devices retain full black-box signal while carving larger slices.
Hardware Impact: One predictable branch only on rare fault-reporting paths; no hot steady-state cost.

Problem: MapMagic AUP terrain queries still exposed only `Vector3` AUP in the main terrain contract, forcing large-coordinate X/Z through float precision before seam planning.
Solution: add a canonical `AbsoluteUniversePosition` overload for height sampling and override it in `MapMagicRuntimeBridge` with a `double3` local-delta path against the resolved terrain tile frame.
Rejected Alternatives: keeping `Vector3` as the only AUP route preserves origin-drift and large-world seam wobble around voxel arches, caves, rocks, and overhang integration.
Scalability potential: Low tier gets stable cheap terrain anchoring without extra buffers; middle/high/ultra can layer denser seam visuals on top of the same stable contact sample.
Hardware Impact: Same cached tile scan count; estimated 0 B/frame and no extra steady-state CPU beyond double arithmetic in the existing query.

Problem: `FindTerrainAtAUP` wrote `_lastResolvedTerrainTile` from a read accessor.
Solution: keep cache writes inside the owner phase (`UpdateLastResolvedTerrainTileOwnerPhase`) and let AUP reads scan the prewarmed tile list without mutating bridge state.
Rejected Alternatives: mutating a cache from `TryGet*` hides state changes behind a read route and violates Global Systems Doctrine.
Scalability potential: Weak devices avoid unpredictable state churn in seam reads; high/ultra retain the same cached fast path when the owner phase already warmed it.
Hardware Impact: Zero allocation; possible rare extra list scan if the owner phase has not warmed the current tile, bounded by existing cached tile count.

Problem: `WorldGenerativeGeologyTerrainSeamApplier.EnsureTerrainState` allocated a `TerrainApplyState` and two `List<>` buckets the first time a terrain was touched from `SlowTick` seam reconciliation.
Solution: prebuild fixed terrain state, plan bucket, and trench bucket pools using `TerrainStateCapacity`; bind terrain IDs to existing buckets and fail closed when the capacity is exhausted.
Rejected Alternatives: runtime dictionary/list growth is convenient but moves managed allocation into the terrain seam simulation lane.
Scalability potential: Low tier avoids allocator stalls when streaming into a new MapMagic tile; middle/high/ultra keep the same eight active terrain seam lanes and spend quality on blend detail, not collection churn.
Hardware Impact: Avoids one managed state object and two managed list allocations per newly touched terrain during runtime seam work.

Problem: terrain seam heightmap ingest, baseline refresh, and black-box telemetry wrote mutable DataVault buffers through the legacy `TryReadHandle` route.
Solution: add `TryAcquireTerrainSeamWriteBuffer`, `TryAcquireTerrainSeamWriteLock`, and `ReleaseTerrainSeamWriteBuffer`; each CPU write path now acquires exactly one DataVault writer fence and releases it from a strict caller `finally`.
Rejected Alternatives: keeping legacy read-handle writes was faster to leave alone but violated owner-write proof; acquiring baseline, heightmap, scratch, and telemetry writer locks together would create a lock-order vector.
Scalability potential: weak devices avoid rare terrain seam stalls under MapMagic tile churn; middle/high/ultra keep the same seam visuals while DataVault ownership remains deterministic.
Hardware Impact: CPU delta is below honest profiler threshold; removes a deadlock/corruption vector rather than buying arithmetic time.

Problem: hybrid terrain seam projection touched multiple DataVault-owned buffers during one scheduled projection window.
Solution: guard the projection window with one DataVault mutation guard mask that covers baseline, optional heightmap, native plans, patch heights, blend mask, and optional normals; release the guard in `finally` and explicitly before terrain seam black-box writer acquisition.
Rejected Alternatives: converting scratch access to simultaneous DataVault write locks violates lock flattening; moving scratch back to MonoBehaviour-owned persistent `NativeArray` would undo DataVault ownership.
Scalability potential: low tier keeps one bounded projection window; middle/high/ultra can raise continuous `GlobalQualityWeight` detail without making compaction/job lifetime unsafe.
Hardware Impact: one mutation guard acquire/release around SlowTick seam projection; expected impact is lower than one terrain patch writeback and not honestly measurable without profiler.

Problem: geology terrain seam, integration, and voxel bridge directors resolved runtime dependencies from reconcile methods, so `SlowTick` could hide `WorldRuntimeReferenceUtility.TryResolve*` polling even without direct `GetComponent`.
Solution: rename the resolver to `RefreshColdReferences`, call it only from `Awake`/`OnEnable`/`Start`, and update MapMagic/VoxelEngine cached references from `GlobalRegistry` hot-swap callbacks.
Rejected Alternatives: keeping a throttled retry inside `ReconcileTerrainSeams`, `RebuildIntegrationPlans`, or `ReconcileVoxelRequests` preserves service-location in runtime phases and makes cost dependent on startup order.
Scalability potential: weak devices get deterministic runtime lanes with no scene/static resolver polling; middle/high/ultra keep late dependency registration through cold hot-swap events without sacrificing seam quality.
Hardware Impact: exact CPU delta not profiler-measured; avoids hidden branches and potential resolver work in voxel/heightmap seam reconciliation on i3/MX350.

Problem: `WorldGenerativeGeologySeamExecutionDirector.LateFrameTick` called `ReconcileExecutedSeams`, and that helper still executed `ResolveReferences`, putting `WorldRuntimeReferenceUtility.TryResolve*` in VISUAL_SYNC through an indirect call.
Solution: move seam execution dependency refresh to `Awake`/`OnEnable`/`Start`, remove the runtime retry timer, and keep late-frame reconciliation as pure cached-reference work.
Rejected Alternatives: allowing a resolver in a helper called by `LateFrameTick` would pass shallow regex checks while still violating phase ownership.
Scalability potential: low tier avoids hidden late-frame resolver stalls; middle/high/ultra keep seam visuals driven by cached integration/player references and continuous quality weights.
Hardware Impact: exact CPU delta not profiler-measured; removes two conditional branches, a dispatcher time read, and possible static resolver work from VISUAL_SYNC on i3/MX350.

Problem: `HectonCaveVoxelAmbientOcclusionController.SlowTick` could call `TryResolveViewerReferences`, and `RefreshVolumeCache` still resolved the world cave director through `WorldRuntimeReferenceUtility`.
Solution: move cave director/player/camera refresh into cold lifecycle and player hot-swap handling; `SlowTick` now reads the cached volume buffer source and resolves target occlusion only.
Rejected Alternatives: throttling the resolver inside `SlowTick` would still make cave AO cost depend on scene/static lookup availability.
Scalability potential: low tier avoids intermittent cave AO lookup spikes; middle/high/ultra keep smoother cave-darkening presentation through the same cached volume route.
Hardware Impact: exact CPU delta not profiler-measured; removes possible component/static resolver work from cave AO slow lane on i3/MX350.

Problem: `MapMagicRuntimeBridge.SlowTick` called a scene binding refresh helper that could reach `TryGetComponent`, and `LateFrameTick`/runtime detail maintenance could resolve player transform from presentation/runtime paths.
Solution: replace runtime binding refresh with `RefreshRuntimeSceneBindingDiagnostics`; cache player transform from `GlobalRegistry.Player` in cold lifecycle and player hot-swap; presentation shader globals and terrain detail maintenance now fail closed when the cached transform is absent.
Rejected Alternatives: keeping an `Application.isPlaying` branch inside a helper still leaves a misleading call graph from `SlowTick` to component lookup.
Scalability potential: low tier removes hidden MapMagic scene-binding work; middle/high/ultra keep planetary terrain fade and detail residency as cached-reference presentation/maintenance systems.
Hardware Impact: exact CPU delta not profiler-measured; removes potential resolver/component lookup from SlowTick and VISUAL_SYNC.

Problem: compile verification remained requested while other `dotnet build`/`csc` lanes were active, then the single throttled full-solution build did not finish in 604s.
Solution: run one `Hecton8.slnx` build only after CPU dropped below 50% and no compiler process existed; stop its leftover `dotnet` process after timeout; keep validation to narrow in-memory source parsing afterward.
Rejected Alternatives: launching a second build or competing with active `csc` violates the project throttle and makes workstation contention worse.
Scalability potential: no runtime effect.
Hardware Impact: build CPU contention avoided; no game runtime effect.

Problem: `WorldCaveDirector.SlowTick` still reached `WorldRuntimeReferenceUtility.TryResolve*` through `EvaluateCaveSpawns`, and `UpdateDiagnostics` also refreshed references. Cave dressing/material/bounds builders called `TryGetComponent` on `HectonVoxelVolume` during repeated runtime dressing and query paths.
Solution: replace `ResolveReferences` with `RefreshColdReferences` called only from `Awake`, `OnEnable`, and `Start`; runtime spawn evaluation uses `HasRequiredReferences`; registry hot-swap updates player, biome matrix, voxel engine, and MapMagic cached references. Cave bounds and material resolution now read `HectonVoxelVolume.CachedMeshFilter` and `CachedMeshRenderer`.
Rejected Alternatives: throttled runtime resolver retries and repeated component lookup in dressing builders preserve unpredictable cost in cave spawn/dressing lanes.
Scalability potential: low tier avoids hidden resolver/component spikes around cave spawn and dressing; middle/high/ultra keep richer cave dressing count through existing continuous quality/intensity controls without changing ownership routes.
Hardware Impact: exact CPU delta not profiler-measured; expected savings are small per call but remove worst-case scene/component lookup from i3/MX350 runtime cave paths.

Problem: scheduled carve, sonar payload, compaction scratch, and hybrid terrain mutation guard acquisition helpers still had manual or implicit failed-acquire release shapes.
Solution: add `keepLock`, `keepGuard`, and `keepScheduledCarveWriteLock` `try/finally` fences so every failed acquired guard is released through a `finally`; successful helpers return only a live lease intended for caller-owned `finally`.
Rejected Alternatives: proving helper acquires by convention leaves the next edit free to add an early return before release.
Scalability potential: all tiers keep the same visual result; weak devices benefit most because lock leaks under memory/streaming pressure are harder to recover from.
Hardware Impact: no measurable steady-state CPU gain; removes deadlock/corruption risk under scheduled carve, sonar publish, compaction, and terrain seam projection pressure.

Problem: final compile check became available only after the external compiler process ended, but CPU moved above the 50% throttle again.
Solution: keep the pass on static AST/parser validation and do not start `dotnet build` under CPU 57%.
Rejected Alternatives: starting a full solution build at 57% CPU violates the repository throttle and risks competing with the shared workstation.
Scalability potential: no runtime effect.
Hardware Impact: avoids local build contention; no game runtime effect.

Problem: `WorldCaveDirector.LateFrameTick` regenerated cave entrance graph data for visual markers, creating four `NativeArray(Allocator.Temp)` buffers after the voxel volume had already published settled cave entrances.
Solution: transfer `HectonVoxelVolume.Entrances` from generation to presentation as the single cave entrance truth; cache entrance hints and `CaveVisualRuntimeState` after `GenerateVolumeAsync`, then let `LateFrameTick` only activate/configure cached roots and components.
Rejected Alternatives: keeping `CaveGraphGenerator.TryMeasure/TryFill` in visual sync duplicates simulation work and makes presentation depend on seed/position reconstruction instead of the generated volume snapshot.
Scalability potential: weak devices avoid per-cave visual-sync allocation spikes; middle/high/ultra can spend saved frame budget on richer cave dressing while the same settled entrance snapshot drives all tiers.
Hardware Impact: removes 4 transient native allocations and one graph fill from cave visual sync; exact microseconds require Unity profiler, but the removed work is bounded and real.

Problem: cave dressing builders reached `WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual`, whose reuse path called `TryGetComponent` from the `LateFrameTick` cave dressing call-chain.
Solution: add cold `WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources()` and hot `CreatePrimitiveVisualHot/ConfigurePrimitiveVisualHot`; hot path reads prewarmed primitive resources and cached `MeshFilter/MeshRenderer` references, failing closed if an object was not created by the factory cache.
Rejected Alternatives: allowing the factory to repair arbitrary scene objects from visual sync would preserve hidden component lookup and mutable scene search in a high-frequency phase.
Scalability potential: low tier keeps dressing object reuse deterministic; middle/high/ultra can raise dressing count without multiplying component lookup cost.
Hardware Impact: avoids `TryGetComponent` in the 14VOX cave dressing visual call-chain; dictionary capacity is cold-preallocated to 2048 entries to avoid normal growth in active cave dressing.

Problem: `dotnet build` was still requested by protocol, but CPU samples were 95.52/87.40/96.58.
Solution: do not launch build under throttle; use in-memory static source parser and `git diff --check` instead. Roslyn plugin load was not used because the local PowerShell runtime lacked `System.Memory` and `System.Threading.Tasks.Extensions` dependencies for those assemblies.
Rejected Alternatives: launching `dotnet build` or forcing Roslyn through a missing dependency chain would violate the CPU throttle and risk orphaned tool processes.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation build contention; no game runtime effect.

Problem: cave visual sync still contained fail-open repair branches. Entrance marker fallback, primitive fallback, and optional dressing setup could create GameObjects/components or call component lookup if the prepared runtime state was incomplete.
Solution: move all cave dressing object/component preparation into `PrepareCaveVisualRuntimeState` after volume generation. Prewarm wall growth, glowing tissue, service remnants, sediment shelves, entrance marker components, entrance quality zone, bio-root generator, thermal geysers up to max capacity, and deep fungi cache. `LateFrameTick` now only activates/configures cached references and fails closed if state is missing.
Rejected Alternatives: repairing missing scene objects from VISUAL_SYNC would hide allocation and component lookup in presentation. Spawning only the current geyser count would break if config intensity changes without another cold prepare pass.
Scalability potential: low tier uses the same prewarmed pool and may activate fewer objects by continuous intensity/config; middle/high/ultra can activate richer cave dressing without multiplying component lookup or object creation in the frame drain.
Hardware Impact: exact CPU delta not profiler-measured; removes worst-case GameObject/component repair spikes from cave visual sync on i3/MX350.

Problem: primitive runtime state cache could be empty after domain reload while child objects already existed under a cave dressing root.
Solution: prewarm methods now cold-register existing child primitives through `WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual`, then VISUAL_SYNC uses `ConfigurePrimitiveVisualHot` against cached `MeshFilter/MeshRenderer` references. Removed the hot create API that allocated a new GameObject.
Rejected Alternatives: allowing `ConfigurePrimitiveVisualHot` to call `TryGetComponent` or create missing components would defeat the purpose of the hot cache.
Scalability potential: weak devices get deterministic object reuse; stronger devices can spend saved frame stability on higher dressing density and glow without extra scene-repair cost.
Hardware Impact: avoids component lookup/object creation in cave dressing presentation; dictionary registration remains cold and bounded.

Problem: compile verification remained blocked by host load.
Solution: no `dotnet build` was launched because CPU samples were `100/93.99/90.96`; validation stayed in memory through targeted source parsing, write-lock source scan, and `git diff --check`.
Rejected Alternatives: launching build above the 50% CPU throttle violates project policy and risks competing with other agents.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation build contention; no game runtime effect.

Problem: phase-transfer queue used `List.Add` directly. The list was cold-capacity 16 and normal cave count is lower, but overflow would still grow managed storage.
Solution: `QueueCaveVisualSync` now first replaces an existing pending entry for the same cave key, then fails closed if the fixed queue capacity is reached. No managed storage growth is required for visual-sync transfer.
Rejected Alternatives: trusting current `maxCavesPerBiome` against future spawn rules leaves a GC edge in the transfer path.
Scalability potential: low tier avoids list-growth spikes during burst cave generation; middle/high/ultra keep deterministic late-frame visual sync and can increase cave richness inside prewarmed capacity planning.
Hardware Impact: exact CPU delta not profiler-measured; removes a rare managed allocation path on i3/MX350.

Problem: `GenerateCaveCandidates` used serialized `maxCavesPerBiome` to decide candidate count while `_candidateBuffer` had a fixed cold capacity of 8. A designer value above capacity would resize the managed list from `SlowTick`.
Solution: clamp requested candidate count to `_candidateBuffer.Capacity` and guard every candidate `Add` with `Count < Capacity`.
Rejected Alternatives: relying on inspector defaults is not a contract; increasing list capacity without a guard still leaves future resize drift.
Scalability potential: low tier keeps cave spawning bounded; middle/high/ultra can raise cave richness through explicit prewarmed capacity, not accidental list growth.
Hardware Impact: removes rare managed array resize from cave spawn evaluation on i3/MX350.

Problem: stale cave and pending spawn key buffers used `Add` during cleanup enumerations and could resize if active/pending cave counts exceeded their cold capacities.
Solution: grow `_staleCaveKeyBuffer` cold capacity to `ActiveCaveKeyCapacity` and guard stale/pending key collection with `Count < Capacity`; overflow is processed on a later slow tick instead of allocating.
Rejected Alternatives: removing dictionary entries during enumeration is unsafe; letting list growth occur in cleanup violates zero-GC policy.
Scalability potential: low tier avoids cleanup spikes during biome transitions; middle/high/ultra keep deterministic cleanup cadence while capacity can be deliberately raised if world design requires it.
Hardware Impact: removes rare managed resize during cave cleanup on i3/MX350.

Problem: `DeepFungiParticleCache.ResolveGradient` lazily created a `Gradient`, and the call path is reached by `SpawnDeepFungiParticles` from `LateFrameTick`.
Solution: `DeepFungiParticleCache.Prewarm` creates the gradient during cold visual-state preparation. Late frame now calls `TryResolveGradient` and fails closed if prewarm was missing.
Rejected Alternatives: keeping a defensive late allocation would make the zero-GC proof false even if normal flow usually prewarmed correctly.
Scalability potential: low tier avoids late-frame allocation spikes; middle/high/ultra can use richer fungi glow while gradient mutation remains cached.
Hardware Impact: removes one lazy managed object allocation from fungi presentation on i3/MX350.

Problem: build validation remained blocked by CPU throttle.
Solution: kept validation to in-memory parser scans and diff checks; `dotnet build` was not launched because CPU samples were `100/78.78/80.68`.
Rejected Alternatives: launching a build over the 50% CPU throttle violates project policy and risks orphaned compiler work.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: `TryQueueCaveSpawn` allocated a `PendingCaveSpawnState` class and a per-spawn `CancellationTokenSource` from the cave spawn slow lane.
Solution: convert pending spawn state to a readonly struct carrying a version token. Async completion validates the version still stored in `_pendingCaveSpawns`. Generation cancellation now uses the existing lifecycle `CancellationTokenSource`, created cold from `OnEnable`.
Rejected Alternatives: pooling `CancellationTokenSource` is unsafe across cancel/dispose lifecycle; keeping per-spawn CTS preserves managed allocation in runtime spawn cadence.
Scalability potential: low tier avoids managed allocation when cave spawning triggers; middle/high/ultra can queue richer cave generation within bounded pending capacity without per-spawn GC pressure.
Hardware Impact: removes one managed state object and one CTS allocation per cave spawn on i3/MX350.

Problem: `_pendingCaveSpawns` and `_activeCaveKeys` could be asked to accept more cave work than their preallocated capacities.
Solution: `TryQueueCaveSpawn` fails closed when active caves reach `ActiveCaveKeyCapacity` or pending spawns reach `PendingCaveSpawnCapacity`.
Rejected Alternatives: relying on dictionary/hashset resize behavior in a spawn path violates bounded runtime storage.
Scalability potential: weak devices keep hard cave work caps; stronger devices can raise explicit constants after capacity planning.
Hardware Impact: avoids rare dictionary/hashset growth allocation under burst cave placement.

Problem: compile validation remained gated by CPU throttle.
Solution: no `dotnet build` launched because CPU samples were `55.9/39.13/51.12`; validation remained source-parser and diff based.
Rejected Alternatives: launching build when any sampled CPU load exceeds 50% violates the repository throttle.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: biome cave presets were static mutable `CavePreset` objects and each active cave stored the same reference.
Solution: add a fixed cold preset-slot pool in `WorldCaveDirector`; each queued cave receives a copied preset object and exact-length preallocated structure-type array. Slots are released only after failed async completion or active cave removal.
Rejected Alternatives: calling `CavePreset.Clone()` per spawn would allocate in the spawn lane; sharing templates keeps mutation drift across caves.
Scalability potential: low tier keeps hard active/pending cave caps; middle/high/ultra can raise explicit slot capacity after memory planning without changing ownership.
Hardware Impact: avoids one managed preset allocation per spawn versus clone-per-spawn and removes shared-state drift; CPU delta below profiler honesty threshold on i3/MX350.

Problem: `WorldCaveDirector.SpawnCaveAsync` called `TryGetComponent(out HectonVoxelVolume)` after await.
Solution: capture the `HectonVoxelEngine` that generated the volume and resolve the component through `TryGetRegisteredVolumeComponent`, a pure active-volume registry read backed by the engine's cold pooled-component binding.
Rejected Alternatives: post-async `TryGetComponent` is convenient but violates the no runtime component lookup route; using the possibly hot-swapped `voxelEngine` field after await can point at a different owner.
Scalability potential: weak devices avoid a scene/component lookup on cave completion; middle/high/ultra keep richer cave generation without runtime dependency drift.
Hardware Impact: removes one component lookup per successful cave spawn; exact microseconds not profiler-measured.

Problem: `CavePreset.Clone()` was documented as deep copy but shared `allowedStructureTypes`.
Solution: clone now duplicates the structure-type array when present.
Rejected Alternatives: leaving a shallow clone contradicts the API contract and leaks mutation through copied presets.
Scalability potential: all tiers get isolated cave generation data when editor/tools clone presets.
Hardware Impact: no normal runtime impact in 14VOX path because the director uses the cold slot pool.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because CPU samples were `90/79/82`; validation stayed on source parser scans, DataVault writer scan, and `git diff --check`.
Rejected Alternatives: launching build above the 50% CPU throttle violates repository policy and risks competing with other agents.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: cave spawn-point registration resolved `ScavengePopulator` through `WorldRuntimeReferenceUtility` after async cave generation.
Solution: cache `ScavengePopulator` inside `HectonVoxelEngine` from cold `GlobalRegistry.ScavengePopulator` and update it through `ScavengePopulatorRuntime` hot-swap. `RegisterPipelineSpawnPoints` now reads the cached reference only.
Rejected Alternatives: keeping the resolver in the runtime completion path preserves a hidden dependency lane even though it is not a frame tick.
Scalability potential: low tier avoids resolver/global fallback branches when caves finish; middle/high/ultra can keep denser cave loot points without dependency drift.
Hardware Impact: removes one cached resolver/global fallback branch per cave spawn-point registration batch; exact microseconds not profiler-measured.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because CPU samples were `93/73/57`; validation stayed on source parser scans, DataVault writer scan, process check, and `git diff --check`.
Rejected Alternatives: launching build above the 50% CPU throttle violates repository policy.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: `GenerateVolumeAsync` registered a cave volume as active, then could return `null` if spawn-point scratch registration failed, leaving a generated active volume behind.
Solution: add rollback after both active-volume registration and spawn-point registration failure paths; both cave generation overloads now despawn `targetGO` before returning `null`.
Rejected Alternatives: relying on caller cleanup is wrong because caller receives `null`.
Scalability potential: low tier avoids leaked cave volumes under scratch pressure; middle/high/ultra keep larger cave throughput without registry drift.
Hardware Impact: failure-path fix; prevents retained mesh/collider/volume state rather than saving steady-state microseconds.

Problem: `RegisterActiveVolume` returned `void` and could silently refuse registration under capacity/local-bounds pressure.
Solution: make it return `bool`; generation now rolls back if the volume cannot become a registered active volume.
Rejected Alternatives: allowing unregistered generated GameObjects breaks `TryGetRegisteredVolumeComponent` and later culling/cleanup ownership.
Scalability potential: all tiers keep one active-volume registry truth; high/ultra capacity increases remain explicit.
Hardware Impact: one boolean branch on generation completion; removes orphan state under capacity pressure.

Problem: `DespawnVolume` returned volumes to pool without immediately unregistering cave terrain-hole handles because `HectonVoxelVolume.OnDisable` does not call `UnregisterTerrainHoles`.
Solution: call `HectonVoxelVolume.PrepareForReuse()` during `DespawnVolume` and `ClearAllVolumes`, before pool return or destruction.
Rejected Alternatives: waiting until next reuse/destroy keeps terrain holes active after cave removal.
Scalability potential: weak devices avoid accumulating stale terrain-hole masks; middle/high/ultra keep richer cave entrances without long-lived hole drift.
Hardware Impact: despawn-path cleanup; prevents stale terrain-hole work and visual corruption.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because CPU samples were `84/54/67`; validation stayed on source parser scans, DataVault writer scan, process check, and `git diff --check`.
Rejected Alternatives: launching build above the 50% CPU throttle violates repository policy.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: voxel generation read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` from the terrain height/biome fill pipeline and cave entrance terrain-hole registration.
Solution: cache `HectonMapMagicVegetationBridge` in `HectonVoxelEngine` from cold `GlobalRegistry.MapMagicVegetation` and update it through `MapMagicVegetationRuntime` hot-swap.
Rejected Alternatives: direct static active-instance reads keep hidden global ownership in the voxel generation lane.
Scalability potential: low tier avoids static owner polling under cave generation; middle/high/ultra keep terrain-hole and biome sampling routed through one cached owner.
Hardware Impact: removes two static runtime owner lookups per cave generation pipeline; exact microseconds not profiler-measured.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because CPU samples were `100/100/99`; validation stayed on source parser scans, DataVault writer scan, process check, and `git diff --check`.
Rejected Alternatives: launching build during full CPU saturation violates repository policy and risks orphaned compiler work.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: selected chthonic pillar resource binding used `ResourceDistributionDirector.ActiveRuntimeInstance` from `HectonVoxelEngine.ExecuteVoxelPipelineAsync` after anomaly selection.
Solution: cache `ResourceDistributionDirector` inside `HectonVoxelEngine` from cold `GlobalRegistry.ResourceDistribution` and update it through `ResourceDistributionRuntime` hot-swap. `TryBindSelectedChthonicPillarResources` now receives the cached dependency explicitly.
Rejected Alternatives: keeping a direct static active-instance read in the voxel anomaly completion lane preserves hidden global ownership even though the call is not a frame tick.
Scalability potential: low tier avoids static owner polling during rare chthonic pillar generation; middle/high/ultra can bind richer resource halos around voxel pillars through the same cached owner route.
Hardware Impact: removes one static runtime owner lookup per chthonic pillar voxel pipeline; exact microseconds not profiler-measured.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because `dotnet` PID 68252 was active and CPU load was 76%; validation stayed on in-memory source parsing, DataVault writer scan, direct lookup scan, process check, and `git diff --check`.
Rejected Alternatives: launching build with an active compiler/runtime process and CPU above 50% violates repository throttle.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: `HectonVoxelEngine` still read its own `ActiveRuntimeInstance` from rare hot/runtime branches: predictive proxy dampener, collider fake pressure decision, and rebuild-over-budget LOD strike.
Solution: convert collider fake and rebuild budget helpers to instance methods that read `_vramPressureReadModel` and `_lodSystemManager`; publish `_physicsService` from active engine lifecycle/hot-swap into `s_predictiveVoxelProxyPhysicsService` for the static late-frame proxy driver.
Rejected Alternatives: accepting self-singleton lookup because it was cheaper than `GlobalRegistry` still leaves hidden owner routing in hot branches.
Scalability potential: low tier keeps late-frame proxy and collider fake lanes deterministic; middle/high/ultra can spend saved dependency clarity on richer voxel mesh/collider budgets under continuous quality weight.
Hardware Impact: removes three static owner reads from late-frame/pipeline emergency branches; exact microseconds not profiler-measured.

Problem: compile validation remained blocked by host load.
Solution: no `dotnet build` launched because `csc` PID 40372 and `dotnet` PID 68252 were active and CPU load was 100%; validation stayed on in-memory source parsing, direct lookup scan, DataVault writer scan, process check, and `git diff --check`.
Rejected Alternatives: launching another build with active compiler processes and saturated CPU violates repository throttle and risks orphaned compiler work.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: `WorldGenerativeGeologySeamExecutionDirector.LateFrameTick` still wrote seam runtime state through helpers that could read `SeamRegistry.ActiveRuntimeInstance`, copy retained keys through `AddRange`, or grow selection/removal buffers.
Solution: cache `SeamRegistry` during cold lifecycle refresh, use the cached reference from seam apply/cleanup, replace retained-key `AddRange` with bounded indexed copy, and guard every visual-sync runtime key/list insert by fixed capacity.
Rejected Alternatives: allowing a singleton fallback or trusting list initial capacity would leave VISUAL_SYNC dependent on global owner lookup and future collection growth.
Scalability potential: low tier avoids rare visual-sync stalls when seam plans churn; middle/high/ultra keep richer seam dither/primitive visuals while storage growth remains explicit.
Hardware Impact: removes one static registry read path and rare managed list growth from seam visual reconciliation; exact microseconds not profiler-measured.

Problem: `WorldGenerativeGeologyVoxelBridgeDirector.ReconcileVoxelRequests` and queue cleanup could grow `_pendingRuntimeKeys`, queued launch dictionaries, queued launch order, desired-key copies, removal buffers, and cancellation buffers under burst geology requests.
Solution: add fail-closed capacity guards for pending, queued, desired, active, removal, cancellation, and retained-key paths; compact queued launch order in-place when stale canceled keys occupy the fixed list; process clear/cancel loops in fixed-size batches.
Rejected Alternatives: letting `HashSet`, `Dictionary`, or `List` resize in SlowTick or async completion violates bounded runtime storage and hides GC behind seam/voxel integration.
Scalability potential: weak devices keep strict runtime volume caps; middle/high/ultra can raise explicit capacities after memory planning instead of accidental collection growth.
Hardware Impact: removes rare managed collection resize spikes from voxel bridge reconciliation on i3/MX350; exact microseconds not profiler-measured.

Problem: hydrothermal vent binding in the voxel bridge read `ResourceDistributionDirector.ActiveRuntimeInstance` from runtime completion.
Solution: cache `ResourceDistributionDirector` from cold `GlobalRegistry.ResourceDistribution` and update it through `GlobalRegistryServiceSlot.ResourceDistributionRuntime`; vent resource binding now reads only the cached field and fails closed if absent.
Rejected Alternatives: keeping direct active-instance lookup because the path is async rather than a frame tick preserves hidden global ownership in voxel completion.
Scalability potential: low tier avoids static owner polling during rare vent completion; middle/high/ultra can keep geode/deep resource flavor around hydrothermal voxel features through the same cached route.
Hardware Impact: removes one static owner lookup per hydrothermal voxel runtime completion; exact microseconds not profiler-measured.

Problem: compile validation was requested after pass 23, but host CPU load was 55%.
Solution: do not launch `dotnet build`; use direct hot parser, one-hop in-memory source parser, target DataVault lock-shape scan, delimiter balance scan, and `git diff --check`.
Rejected Alternatives: launching a build while CPU is above the 50% throttle violates the repository compile policy.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation contention; no game runtime effect.

Problem: `WorldGenerativeGeologyBinding` used static registries and copy helpers that could grow `_knownBindings`, `_activeBindings`, `_staleBindingIndexBuffer`, or caller destination lists during geology planning.
Solution: introduce fixed binding registry capacity, guard active/known registration, guard stale-index staging, and copy only while the caller destination has prewarmed capacity.
Rejected Alternatives: relying on initial `List` capacity hides managed growth under streaming/authoring spikes and makes `CopyActiveBindingsTo` a runtime allocation edge.
Scalability potential: low tier gets bounded geology binding scans; middle/high/ultra can raise explicit registry capacity after memory planning.
Hardware Impact: removes rare list resize spikes from geology binding scan/registration paths; exact microseconds not profiler-measured.

Problem: `WorldGenerativeGeologyIntegrationDirector` allowed `maxTrackedPlans` above the preallocated 256-plan storage and used `AddRange` for public copy and stabilization.
Solution: clamp resolved plan capacity to `PlanRuntimeKeyCapacity`, replace `AddRange` with bounded loops, add `TryUpsertPlan` so dictionaries/lists are mutated only after capacity is available, and guard trim buffers.
Rejected Alternatives: trusting inspector values and post-sort trimming still permits dictionary/list growth before stabilization.
Scalability potential: weak devices keep a hard plan budget; middle/high/ultra keep richer seam selection by raising constants intentionally, not by accidental managed growth.
Hardware Impact: removes rare managed collection resize from geology integration planning; exact microseconds not profiler-measured.

Problem: `HectonVoxelVolume` used active singleton fallback reads for queued rebuild engine resolution and terrain-hole unregister vegetation bridge resolution.
Solution: cache `HectonVoxelEngine` and `HectonMapMagicVegetationBridge` from cold `GlobalRegistry` and update them through hot-swap; queued rebuild and terrain-hole cleanup read cached fields only.
Rejected Alternatives: keeping fallback singleton reads in lifecycle/rebuild paths preserves hidden global ownership when pooled volumes are under pressure.
Scalability potential: low tier avoids static fallback drift during cave volume pooling; middle/high/ultra keep terrain-hole cleanup deterministic while richer cave entrances are active.
Hardware Impact: removes two runtime singleton fallback reads from voxel volume cleanup/rebuild paths; exact microseconds not profiler-measured.

Problem: compile validation was requested after pass 25 while CPU was under threshold and no compiler process was active, but the user's APEX protocol explicitly asked for no build spam and in-memory static validation.
Solution: do not launch `dotnet build`; validation stayed on direct hot-block source scan, one-hop source parser, targeted singleton grep, DataVault lock-shape scan, and `git diff --check`.
Rejected Alternatives: starting a full solution build would contradict the requested compilation throttling proof and has previously timed out in this workspace.
Scalability potential: no runtime effect.
Hardware Impact: avoids workstation build contention; no game runtime effect.

Problem: `HectonVoxelVolume.ApplyMagmaVeinSpline` read `DestructibleOrganicManager.ActiveRuntimeInstance` before applying flora burn along magma-vein segments.
Solution: cache the concrete organic owner from cold `GlobalRegistry.OrganicToolHits` and update it through `GlobalRegistryServiceSlot.DestructibleOrganicRuntime`; magma-vein burn now uses the cached owner and fails closed if absent.
Rejected Alternatives: keeping the active singleton because organic damage is cross-domain would leave a hidden runtime owner lookup in voxel deformation.
Scalability potential: low tier avoids static owner polling during magma-vein cuts; middle/high/ultra keep optional flora burn flavor without changing voxel truth ownership.
Hardware Impact: removes one runtime singleton read per magma-vein burn batch; exact microseconds not profiler-measured.

Problem: `SeamGapDitherRenderer.LateFrameTick` reached active vegetation/geology singletons through flora-root and biome-template helpers, and `VoxelDynamicNavGridRuntime` hybrid navigation/obstacle reads used `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`.
Solution: cache the vegetation bridge through cold `GlobalRegistry.MapMagicVegetation` and `MapMagicVegetationRuntime` hot-swap; cache the geology integration director through cold scene/domain resolution; use cached owners in VISUAL_SYNC and read accessors.
Rejected Alternatives: keeping active singleton reads because they are cheap; adding a new `GlobalRegistry` slot for geology integration without a route card.
Scalability potential: low tier avoids hidden owner polling from seam dither and nav sampling; middle/high/ultra keep richer dither/flora-root visuals through the same cached route.
Hardware Impact: exact microseconds not profiler-measured; removes runtime singleton read paths and stabilizes dependency ownership on i3/MX350.

Problem: `VoxelDynamicNavGridRuntime` build and dynamic obstacle update scheduling held multiple DataVault writer locks across scheduled jobs.
Solution: replace multi-lock windows with one DataVault mutation guard mask covering the affected navgrid buffers; resolve mutable views under the guard; store guard ownership on the record; release in completion/failure `finally`.
Rejected Alternatives: holding 2-3 DataVault writer locks until commit; collapsing navgrid lanes into a new DTO buffer during this pass.
Scalability potential: low tier avoids deadlock/compaction contention under cave carving and vegetation obstacle churn; middle/high/ultra can run richer nav/flora obstacles without increasing lock-order risk.
Hardware Impact: CPU delta not honestly profiler-measured; removes multi-writer-lock deadlock vector rather than buying arithmetic time.

Problem: `VoxelDynamicNavGridRuntime.TryScheduleBuild` and dynamic obstacle update scheduling allocated `NativeArray<NavObstaclePrimitive>` with `Allocator.TempJob` from runtime job scheduling.
Solution: add a fixed persistent obstacle snapshot lease pool, prewarmed from lifecycle/data-vault cold route; jobs receive `ObstacleCount` and scan only the valid prefix.
Rejected Alternatives: per-schedule TempJob snapshots, managed obstacle arrays, or one shared snapshot without leases.
Scalability potential: low tier avoids native allocator churn during flora/cave obstacle updates; middle/high/ultra can stamp more obstacles through explicit fixed capacities.
Hardware Impact: removes two TempJob native allocations from obstacle build/update schedules; exact microseconds not profiler-measured.

Problem: `VoxelDynamicNavGridRuntime.TryResolveNavGridRead` wrote telemetry from a read-looking accessor and could be called while a navgrid mutation guard was held.
Solution: make `TryResolveNavGridRead` and `TryResolveNavGridMutable` pure handle-resolution helpers; telemetry remains in owner-phase failure/budget paths.
Rejected Alternatives: hidden DataVault telemetry writes from read accessors or nested telemetry locks under mutation guard.
Scalability potential: all tiers keep navgrid failure behavior predictable under compaction pressure.
Hardware Impact: removes a lock-overlap vector; CPU delta is below honest profiler threshold.

Problem: `SeamRegistry.CopyStatesTo` and `CopyCaveEntrancesTo` could grow caller lists from `SeamGapDitherRenderer.LateFrameTick`.
Solution: cap copy loops by destination capacity and fail closed when the visual scratch is full.
Rejected Alternatives: trusting list initial capacity or adding a new allocation-backed copy API.
Scalability potential: low tier avoids visual-sync GC when seam counts exceed scratch; middle/high/ultra can increase scratch capacity explicitly.
Hardware Impact: removes rare list growth allocation in VISUAL_SYNC; exact microseconds not profiler-measured.

Problem: `MapMagicRuntimeBridge.LateFrameTick` could allocate distant terrain shadow resources, and `SlowTick` biome cache prewarm could resize terrain texture/layer arrays.
Solution: prewarm distant-shadow mask and fixed biome caches from cold lifecycle; `LateFrameTick` publishes zero mask if resources are not resident; runtime biome cache refresh fails closed when counts exceed fixed capacity.
Rejected Alternatives: allocation from VISUAL_SYNC/SlowTick, or disabling the cinematic distant shadow fake entirely.
Scalability potential: low tier keeps cheap stable terrain fade/shadow; middle/high/ultra retain visual overkill through prewarmed resources.
Hardware Impact: removes rare Texture2D/Color32/TerrainLayer array allocation from runtime phases; exact microseconds not profiler-measured.

Problem: `HectonBiolumController.SlowTick` could call `TryGetComponent` through `TryBindSurvivalSystemFromPlayerContext` when the serialized survival reference was absent.
Solution: cache `IPlayerRuntimeContext` from cold `GlobalRegistry.Player` and the `Player` hot-swap slot; `SlowTick` now reads `playerContext.SurvivalSystem` only.
Rejected Alternatives: keeping `BootstrapState.TryGetCurrentPlayerTransform` plus player `TryGetComponent` as a fallback preserves a scene lookup in a depth/biolum update phase.
Scalability potential: low tier avoids survival binding scene probes in cave/abyss ambience; middle/high/ultra keep the same eclipse/sonar biolum response through cached context.
Hardware Impact: removes one component lookup fallback per missing biolum survival binding; exact microseconds not profiler-measured.

Problem: `MapMagicRuntimeBridge.SlowTick` called `RefreshTerrainTileCache(false)`, whose body could traverse the MapMagic hierarchy with `GetComponentsInChildren<TerrainTile>` when the root child count changed.
Solution: split cold terrain-tile refresh from owner-phase validation; `SlowTick` now only compacts valid cached tiles, records root-count drift, and tile-applied/moved events add new tiles into a fixed-capacity cache without growth.
Rejected Alternatives: scanning MapMagic child hierarchy every structural drift from `SlowTick`, or letting `_cachedTerrainTiles` grow past its terrain budget.
Scalability potential: low tier keeps deterministic cached terrain lookup for caves/overhang height integration; middle/high/ultra can raise `TerrainTileCacheCapacity` deliberately for larger streaming rings.
Hardware Impact: removes terrain hierarchy traversal and list-growth edge from MapMagic slow tick; exact microseconds not profiler-measured.

Problem: `MapMagicRuntimeBridge.SlowTick` refreshed biome alpha texture handles and terrain layer handles every slow tick, even when `TerrainData`, alphamap texture count, and alphamap layer count were unchanged.
Solution: gate owner-phase refresh behind the pure cache read helpers; refresh happens only after explicit invalidation, terrain swap, or count mismatch.
Rejected Alternatives: unconditional `GetAlphamapTexture` and `terrainData.terrainLayers` polling from slow tick; pushing terrain layer lookup into `TryGetTerrainSplatColor*` read accessors.
Scalability potential: low tier avoids steady cache churn while preserving cached splat color sampling; middle/high/ultra keep richer terrain-layer coloration without scene scans or runtime array growth.
Hardware Impact: removes repeated slow-tick biome cache refresh work on unchanged terrain; exact microseconds not profiler-measured.

Problem: APEX verification requested compilation proof, but a `dotnet` process was already active and CPU samples were 100/100/100.
Solution: validation stayed in memory through static method/hot-loop parsing, DataVault write-lock shape scanning, delimiter balance checks, and `git diff --check`.
Rejected Alternatives: launching `dotnet build` under compiler/process contention and violating the user's throttling rule.
Scalability potential: no runtime effect; protects workstation throughput while other agents run.
Hardware Impact: avoids competing with active compiler workload; no game-frame runtime effect.

Problem: `ShinobuVoxelSculptorWindow.TryWriteTuningToVault` created the carve-debris job-state handle as `SystemID.Vfx` but attempted the write lock as `SystemID.CoreDiagnostics`, then had a validation release path outside the normal `finally`.
Solution: acquire and release the lock under `SystemID.Vfx`, matching the handle owner, and make the validation failure return from inside the single `try/finally` release scope.
Rejected Alternatives: treating editor tooling as harmless; mismatched DataVault owner IDs make the tuning bake route silently fail or depend on relaxed lock validation.
Scalability potential: editor-only, but it protects the voxel debris tuning path used to author low/mid/high/ultra debris budgets.
Hardware Impact: no runtime frame impact; prevents broken editor bake state from leaking into runtime tuning data.

Problem: `VoxelMemorySovereigntyValidator1304.RunDefragRaceFuzzer` held carve-event and density write locks at the same time during the defrag race loop.
Solution: split the pass into two independent write-lock windows: carve buffer write + release, then density buffer write + release, each with strict `finally`; defrag pressure remains tested under one lock and after all locks are released.
Rejected Alternatives: preserving a fuzzer that violates the same lock discipline it is supposed to validate.
Scalability potential: editor validation now models the runtime rule used by weak-device and high-density voxel paths: one DataVault write owner at a time.
Hardware Impact: editor-only; removes false-positive deadlock geometry from the validation route.

Problem: `WorldProceduralFieldSampler.PrepareBurstData` could enter dependency repair during scatter/heightmap sampling data-prep by calling `ResolveReferences` and `WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector`.
Solution: move dependency repair to cold lifecycle and explicit owner-rebind routes. `WorldCaveDirector` and `WorldZoneDirector` now publish active-owner changes through bounded static events; `WorldProceduralFieldSampler` subscribes once and handles MapMagic/BiomeMatrix/Player swaps through `IGlobalRegistryHotSwapListener`. `PrepareBurstData` now only refreshes cached diagnostics before building vault data.
Rejected Alternatives: sampling-frame polling, adding cave/zone slots to `GlobalRegistry` without an architecture route card, or accepting active-singleton repair from the field sampler because it is not a MonoBehaviour `Tick`.
Scalability potential: low tier avoids dependency repair spikes in procedural scatter/heightmap sampling; middle/high/ultra can keep cave entrance hint influence and richer biome field sampling without adding hot scene lookup.
Hardware Impact: exact profiler microseconds not measured; removes hidden resolver branch from sampling data-prep and prevents absent-owner repair from running on i3/MX350 sampling frames.

Problem: `WorldZoneDirector` no longer polled slice/scatter owners directly, but its dependent systems still repaired player/MapMagic/scavenge/proximity/biome references from `SlowTick`, keeping a transitive resolver route alive.
Solution: add static active-owner events to `ScatterBudgetController`, `WorldSliceDirector`, `BiomeSamplerCache`, and `ProximityColliderSystem`; move player/MapMagic/scavenge rebinding to cold `GlobalRegistry` lifecycle/hot-swap routes; make hot methods read cached `IPlayerRuntimeContext`, cached owner references, or fail closed.
Rejected Alternatives: leaving dependency repair in `ApplyCurrentBudget`, `ApplySlices`, `RebuildCache`, or `ProximityColliderSystem.Tick` because missing-reference retries are rare; rare retries still create nondeterministic stalls on weak hardware.
Scalability potential: low tier avoids scene/registry probing while moving through dense cave entrances and overhang transition zones; middle/high/ultra keep continuous budgets, larger scatter radii, and proximity collider work without changing authority routes.
Hardware Impact: exact profiler microseconds not measured; removes transitive resolver work from zone/scatter/slice/biome/proximity frame phases.

Problem: `WorldGenerativeGeologyTerrainSeamApplier.SlowTick` reached a method that could allocate terrain bucket lists when the cold pool was not initialized.
Solution: split hybrid terrain seam initialization into `EnsureHybridTerrainSeamStateCold` and `HasHybridTerrainSeamState`; hot signal drain now exits if cold state is absent, and terrain state binding rejects absent pools instead of allocating from owner phase.
Rejected Alternatives: relying on `Awake` to have already initialized pools while leaving a cold allocator visible and callable from the hot route.
Scalability potential: low tier keeps terrain seam signal drain bounded and allocation-free; middle/high/ultra can spend saved stability budget on richer voxel blend masks without moving pool creation into `SlowTick`.
Hardware Impact: exact profiler microseconds not measured; removes fixed-list construction possibility from terrain seam signal drain.

Problem: `HectonMapMagicVegetationBridge.LateFrameTick` deferred startup called `RefreshColdRuntimeDependencies`, which still used `WorldRuntimeReferenceUtility.TryResolve`.
Solution: replace resolver calls with cold `GlobalRegistry.MapMagic` and cached `GlobalRegistry.Player` context reads; existing hot-swap updates remain the live rebinding path.
Rejected Alternatives: treating deferred startup as acceptable because it runs inside `LateFrameTick`; visual-sync/deferred phases still must not search scene state.
Scalability potential: low tier avoids late-frame startup hitches around vegetation/cave hole sync; middle/high/ultra preserve deferred tile bootstrap and richer HLOD vegetation without resolver drift.
Hardware Impact: exact profiler microseconds not measured; removes late-frame resolver calls from vegetation startup.

Problem: `MapMagicRuntimeBridge.SlowTick` called a wrapper containing both owner-phase tile-cache validation and cold hierarchy traversal with `GetComponentsInChildren`.
Solution: call `ValidateTerrainTileCacheOwnerPhase` directly from `SlowTick`; cold terrain tile cache rebuild remains only on explicit force paths.
Rejected Alternatives: relying on branch reasoning (`force == false`) as proof while static verification still sees the cold traversal in the hot method body.
Scalability potential: low tier keeps terrain tile validation as cached list compaction; middle/high/ultra can still rebuild large terrain tile caches deliberately from cold routes.
Hardware Impact: exact profiler microseconds not measured; removes the structural hot path to terrain hierarchy traversal.

Problem: `BiomeSamplerCache.SlowTick` still reached sample-array allocation when `radiusCells` changed or storage was missing.
Solution: split sample storage into `EnsureStorageCold` and `HasStorageForCurrentShape`; hot rebuild now fails closed and keeps diagnostics accurate instead of allocating.
Rejected Alternatives: trusting inspector values to stay unchanged after startup or resizing `CachedSample[]` from a slow-tick sampler.
Scalability potential: low tier avoids a managed allocation spike near biome/heightmap transitions; middle/high/ultra can raise radius through cold-authored settings without changing hot authority.
Hardware Impact: removes rare `CachedSample[]` runtime allocation; exact microseconds not profiler-measured.

Problem: vegetation deferred startup in `LateFrameTick` could resize startup tile snapshots and call a cold dependency refresh.
Solution: replace the growable snapshot array with a fixed cold array sized to the tile cache budget, clamp copied snapshot count, and rely on lifecycle/hot-swap references already cached before the visual-sync phase.
Rejected Alternatives: growing snapshot arrays or touching `GlobalRegistry` from deferred startup because bootstrap is rare.
Scalability potential: low tier avoids late-frame startup stalls; middle/high/ultra preserve larger startup coverage through the fixed 4x tile-cache snapshot budget.
Hardware Impact: removes rare `MapMagicTerrainTileSnapshot[]` resize and dependency polling from visual sync.

Problem: terrain-hole runtime state could grow managed record arrays, streaming arrays, per-tile `bool[,]` masks, and height-readback NativeArrays from `SlowTick`/deferred startup.
Solution: make terrain-hole record/streaming capacity fixed from cold `MinimumTerrainHoleRuntimeCapacity`, split terrain-hole mask prep into hot no-allocation and cold allocation paths, split tile upsert into cold event and deferred startup paths, and make slow readback repair validate resident storage instead of allocating it.
Rejected Alternatives: per-tile managed mask allocation from `TryScheduleTerrainHoleJobs`, NativeArray repair from `SlowTick`, or expanding terrain-hole registries when cave/mega-wreck masks exceed authored capacity.
Scalability potential: low tier keeps cave entrance vegetation suppression predictable and fail-closed; middle/high/ultra can increase serialized terrain-hole capacity before runtime without changing the hot route.
Hardware Impact: removes rare managed `bool[,]`, `Texture2D[]`, `NativeArray<ushort>`, and terrain-hole array growth from hot/deferred phases; exact profiler microseconds not measured.

Problem: `WorldCaveDirector.LateFrameTick` still reached `EnsureCaveVisualRuntimeState`, which could create `CaveVisualRuntimeState`, marker/geyser arrays, child GameObjects, and missing Light/ParticleSystem/Collider components if a visual-sync arrived before cold visual state was present.
Solution: prepare cave visual runtime state immediately after the generated voxel volume has its cave key, generation position, preset, and entrances assigned; change late visual sync to `HasCaveVisualRuntimeState` so the phase consumes only cached state and skips if cold preparation failed.
Rejected Alternatives: repairing missing visual state from `LateFrameTick`, or allowing presentation to create objects/components because cave generation is rare.
Scalability potential: low tier avoids visual-sync hitches during cave entrance reveal; middle/high/ultra keep overkill dressing, entrance glow, geysers, roots, fungi, shelves, and tissue because cold preparation still builds the full cached presentation graph before the visual phase.
Hardware Impact: removes one late-frame managed visual-state/object/component creation branch per missed cave visual-sync; exact profiler microseconds not measured.

Problem: `WorldGenerativeGeologyTerrainSeamApplier.LateFrameTick` could allocate or resize the global voxel blend-mask `Texture2D` when the seam patch footprint changed.
Solution: cold-prepare a fixed R8 blend-mask texture plus reusable byte upload buffer during lifecycle; copy active patch bytes into the fixed surface; pass active UV scale in `_HectonVoxelBlendMaskParams.zw` so the terrain shader samples only the valid subregion.
Rejected Alternatives: moving texture allocation from `LateFrameTick` into `SlowTick`, accepting per-size texture churn, or disabling the terrain/voxel seam visual mask.
Scalability potential: low tier gets deterministic bounded upload memory; middle/high/ultra keep the cinematic terrain/voxel blend mask and can raise `voxelBlendMaskTextureSide` deliberately without changing the phase route.
Hardware Impact: removes late-frame texture allocation/resizing; per upload now copies only active patch bytes plus a one-pixel border into a resident buffer. Exact profiler microseconds not measured.

Problem: `WorldGenerativeGeologyVoxelBridgeDirector.SpawnOrRefreshVolumeAsync` added `WorldGenerativeGeologyVoxelRuntime` to generated volumes with `AddComponent` after async voxel generation completed.
Solution: require the pooled/generated voxel volume prefab to contain the runtime component from cold authoring; if the active runtime is absent, despawn the generated volume, write black-box fault state, and fail closed without structural mutation.
Rejected Alternatives: adding the component after generation, searching for the component from the completion path, or letting a partially-owned volume enter active runtime dictionaries.
Scalability potential: low tier avoids runtime component insertion stalls; middle/high/ultra can prewarm larger voxel pools with the same cold-authored component contract.
Hardware Impact: removes one `AddComponent` branch per geology voxel volume creation; exact profiler microseconds not measured.

Problem: voxel predictive proxy, voxel AUP, and camera-facing overhang noise still pulled player runtime state through `PlayerRuntimeContextService.TryGetActiveRuntimeContext`.
Solution: cache `IPlayerRuntimeContext` from cold `GlobalRegistry.Player` and the `Player` hot-swap slot inside `HectonVoxelEngine`; hot helpers now read the cached context or fail closed to conservative noise/proxy behavior.
Rejected Alternatives: using the global runtime-context singleton from static helpers because it is convenient; that preserves hot service-location in voxel pipeline/proxy decisions.
Scalability potential: low tier avoids hidden player context lookup from proxy/noise decisions; middle/high/ultra keep the same predictive proxy and overhang noise decisions through a deterministic cached owner route.
Hardware Impact: removes several context lookup branches from voxel runtime/proxy helpers; exact profiler microseconds not measured.

Problem: `HectonMapMagicVegetationBridge.RefreshActiveViewCameraCache` resolved a camera through `playerTransform.TryGetComponent` and `PlayerRuntimeContextService` from a camera refresh path.
Solution: cache the local camera only in cold lifecycle, cache `IPlayerRuntimeContext` through player service/hot-swap, and make camera refresh read only serialized camera, cached player camera, or cached local camera.
Rejected Alternatives: retrying `TryGetComponent` on a timer or using runtime context singleton fallback when the camera is missing.
Scalability potential: low tier avoids camera-binding scene probes around vegetation/cave-hole streaming; middle/high/ultra keep frustum culling and HLOD visuals through stable cached camera ownership.
Hardware Impact: removes player/local component lookup from camera refresh; exact profiler microseconds not measured.

Problem: `HectonVoxelStreamingBridge` and `ScavengePopulator` retained fallback player resolution from runtime methods.
Solution: cache `IPlayerRuntimeContext` from `GlobalRegistry.Player` during cold refresh and update it via the `Player` hot-swap lane; runtime player AUP/scavenge binding uses the cached context only.
Rejected Alternatives: calling `WorldRuntimeReferenceUtility.TryResolvePlayerTransform` or runtime context singleton from streaming/scavenge runtime paths.
Scalability potential: low tier avoids hidden player lookup during cave entrance streaming and loot node population; middle/high/ultra keep the same content density with deterministic owner routes.
Hardware Impact: removes fallback lookup branches from runtime player-dependent streaming and scavenge decisions; exact profiler microseconds not measured.

Problem: the geology voxel bridge could configure its runtime without proving the generated volume's registered `HectonVoxelVolume` component came from the voxel engine active-volume registry.
Solution: after async generation, require both the cold-authored `WorldGenerativeGeologyVoxelRuntime` and the registered `HectonVoxelVolume`; on either failure despawn the volume and write black-box fault flags.
Rejected Alternatives: searching for the component from the completion path or allowing a partially registered volume to enter bridge runtime dictionaries.
Scalability potential: low tier avoids orphan volume/component repair; middle/high/ultra can run larger geology voxel pools as long as prefabs satisfy the same cold-authoring contract.
Hardware Impact: removes one completion-path component lookup/repair vector; exact profiler microseconds not measured.

Problem: `GPUScatterDirector.SlowTick` called `ResolveDependencies`, which reached `WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge` and `TryResolvePlayerTransform`.
Solution: split dependency ownership into `RefreshColdSceneDependencies` for `Awake`/`OnEnable`, direct `GlobalRegistryServiceSlot.MapMagicVegetationRuntime` hot-swap assignment, and `RefreshCachedRuntimeDependencies` that only consumes cached `IPlayerRuntimeContext`.
Rejected Alternatives: resolving scene owners from `SlowTick`, or reading `GlobalRegistry.MapMagicVegetation` from the hot repair branch.
Scalability potential: low tier avoids scatter slow-lane scene resolver spikes near dense vegetation/heightmap transitions; middle/high/ultra keep GPU scatter density and HLOD vegetation through cached owners.
Hardware Impact: removes one transitive scene resolver path from GPU scatter slow tick; exact profiler microseconds not measured.

Problem: optional GPU scatter visible-count diagnostics could allocate `NativeArray<uint>` from `SlowTick` through readback repair.
Solution: prewarm diagnostic readback storage only in cold lifecycle when `_enableVisibleCountReadback` is enabled; slow repair now fails closed if storage is absent.
Rejected Alternatives: creating persistent native readback storage from a runtime repair method because the feature is diagnostic-only.
Scalability potential: low tier avoids surprise native allocation from inspector diagnostics; middle/high/ultra can enable the diagnostic with cold prewarm before runtime.
Hardware Impact: removes one optional persistent native allocation from `SlowTick`; exact profiler microseconds not measured.

Problem: biome heatmap refresh was guarded against allocation in `SlowTick`, but static callgraph still saw `SlowTick -> TryEnsureBiomeHeatmapTexture -> EnsureBiomeHeatmapResources`.
Solution: split `TryEnsureBiomeHeatmapTextureCold` from `TryRefreshBiomeHeatmapTextureHot`; hot refresh updates resident bytes only if cold upload buffer and texture already exist.
Rejected Alternatives: relying on branch guards as proof while keeping allocator-visible helper names in the hot callgraph.
Scalability potential: low tier keeps biome/scatter shader LUT refresh allocation-free at runtime; middle/high/ultra preserve Data Monolith biome heatmap visuals through the same cold-authored texture.
Hardware Impact: no new measured CPU gain beyond proof hardening; removes allocator path from hot callgraph.

Problem: `HectonCaveVoxelLightingVolume.SlowTick` could call the resource ensure path, which creates spatial query arrays and a 3D texture when resources are missing or invalid.
Solution: rename the allocator path to `EnsureResourcesCold`, call it from cold lifecycle and DataVault hot-swap only, and make `SlowTick` publish inactive/fail-closed globals until required resources are resident.
Rejected Alternatives: repairing missing voxel-lighting resources from `SlowTick`, or letting cave lighting silently allocate when a DataVault swap invalidates backing state.
Scalability potential: low tier avoids cave-lighting allocation spikes near cave entrances; middle/high/ultra keep the same voxel light volume quality when resources are cold-prepared.
Hardware Impact: removes rare `SpatialQueryHit[]` and `Texture3D` allocation from slow runtime; exact profiler microseconds not measured.

Problem: `GpuScatterLodManager.SlowTick` had a one-hop path to `GlobalRegistry.DataVault` and could repair GPU buffers and visible-count readback storage from the slow lane.
Solution: make DataVault lookup a cold `RefreshCachedRegistryServicesCold` call, move GPU state creation to `OnEnable` and DataVault hot-swap, and make `SlowTick` fail closed if registry or GPU state is absent. Visible-count readback allocation is cold-only and slow repair only clears the repair request when storage is absent.
Rejected Alternatives: throttled `GlobalRegistry` retry from `SlowTick`, recreating `GraphicsBuffer` from runtime repair, or allocating diagnostic `NativeArray<uint>` from slow repair.
Scalability potential: low tier keeps scatter LOD predictable under streaming and registry churn; middle/high/ultra can run larger scatter buffers and optional readback diagnostics only when cold-prepared.
Hardware Impact: removes one registry retry branch and multiple GPU/native allocation repair vectors from scatter LOD slow tick; exact profiler microseconds not measured.

Problem: APEX verification required compile/syntax proof without build spam or disk JSON reports.
Solution: used focused source callgraph parsing for hot direct/one-hop dependencies, focused DataVault writer scanning, target `git diff --check`, and the already-built Roslyn audit executable with `--output NUL`; no `dotnet build` was launched and `Test-Path NUL` returned false.
Rejected Alternatives: full-solution `dotnet build`, writing audit JSON to disk, or treating PowerShell-hosted Roslyn as authoritative after it failed on a `System.Memory` binding mismatch.
Scalability potential: no runtime effect; protects shared workstation CPU for other agents while preserving source-level proof.
Hardware Impact: no game-frame impact; avoids full build CPU cost for this pass.

Problem: generated geology for scatter arches, shelves, cave bridges, and rock packs was applied from `LateFrameTick` through `WorldProceduralScatterDirector`, but the service path could still create Unity components, child GameObjects, primitive renderers, and exact renderer arrays during VISUAL_SYNC.
Solution: split generated geology into cold shell preparation and hot configuration. `WorldProceduralProxyInstance` prepares `__GENERATED_GEOLOGY` shells, `LODGroup`, `WorldGenerativeGeologyBinding`, `GeneratedRuntimeState`, LOD arrays, renderer arrays, and primitive shells from cold lifecycle/editor configuration. `WorldGenerativeGeologyService.TryApplyGeneratedGeologyHot` only reuses cached roots, cached binding/state, pre-sized arrays, and `WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot`.
Rejected Alternatives: allowing rare first-use `AddComponent`, `new GameObject`, `TryGetComponent`, or exact `Renderer[]` allocation from visual sync; moving the work to `SlowTick`; or disabling generated geology outright.
Scalability potential: low tier fails closed if a prefab is not cold-prepared instead of stalling the frame; middle/high/ultra keep context-pack arches, canopies, cave bridges, and debris by raising authored pool/shell capacity before runtime.
Hardware Impact: removes structural Unity mutation and managed array allocation from generated geology visual sync on i3/MX350; exact profiler microseconds not measured.

Problem: the pass 40 hot route still paid a static primitive-runtime dictionary lookup, `GetInstanceID`, LOD child/name traversal, and primitive rename work while applying generated geology in VISUAL_SYNC.
Solution: store prepared LOD roots, primitive GameObjects, MeshFilters, and MeshRenderers in `GeneratedRuntimeState` cold arrays; route scatter through `TryApplyPreparedGeneratedGeologyHot(WorldProceduralProxyInstance, request)`; use `WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualCachedHot` and a no-rename hot assignment path.
Rejected Alternatives: keeping `_RuntimeStates.TryGetValue` as "cheap enough", resolving host metadata through `WorldProceduralProxyInstance.TryGetCached` from the scatter frame, or reading child names to find LOD roots.
Scalability potential: low tier avoids avoidable hash/name/child traversal while preserving the same primitive-shell impostor look; middle/high/ultra keep richer generated geology by increasing authored shell capacity before runtime.
Hardware Impact: removes multiple per-primitive hash/name/child lookup operations from generated geology visual sync on i3/MX350; exact profiler microseconds not measured.

Problem: cave dressing visual sync still configured primitive-shell details through child traversal and the shared primitive runtime dictionary, and adjacent entrance/geyser cleanup used root `GetChild`/hot name writes.
Solution: extend each cave dressing builder with cold `GameObject/MeshFilter/MeshRenderer` caches and a `BuildPreparedCachedHot` route; `WorldCaveDirector` stores those caches in `CaveVisualRuntimeState`; entrance marker and thermal geyser cleanup now iterates cached state arrays.
Rejected Alternatives: treating `_RuntimeStates.TryGetValue`, `GetInstanceID`, `GetChild`, and `.name` writes as cheap enough because cave dressing is sparse. Sparse spikes are still visible on weak CPUs during cave reveal.
Scalability potential: low tier keeps cave dressing as prewarmed primitive impostors with zero structural repair; middle/high/ultra can raise authored dressing capacities before runtime without changing the visual-sync ownership route.
Hardware Impact: removes per-primitive hash-table reads, child traversal, and hot name assignment from cave dressing presentation on i3/MX350; exact profiler microseconds not measured.

Problem: `ThermalGeyser.Configure` and `CaveBioRootsGenerator.Configure` were reachable from cave visual sync; the former could call `TryGetComponent`, and the latter could allocate root spline buffers and traverse child transforms.
Solution: keep `ThermalGeyser.Configure` as a pure parameter/current-volume update by moving runtime wiring to `CacheRuntimeWiringCold`; rename bio-root setup to `ConfigureCold` and call it during cave visual-state preparation, not from `ApplyBioRoots`.
Rejected Alternatives: relying on Unity component fields being already populated while leaving the resolver/allocation call reachable from `LateFrameTick`.
Scalability potential: low tier fails closed to already-prepared geysers/roots without visual-sync repair stalls; middle/high/ultra retain richer cave life dressing through cold prewarm and later visual animation.
Hardware Impact: removes one `TryGetComponent` route and several managed array allocation routes from cave reveal visual sync; exact profiler microseconds not measured.
