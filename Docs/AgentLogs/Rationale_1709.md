# Rationale 1709

## Initial Authority
Problem: Domain authority must be source-backed while the old standalone domain-map route is retired.
Solution: Constrain edits to the XML-authorized files/directories, active domain index, and coverage matrix.
Rejected Alternatives: Expanding into unrelated systems based on neighboring prompts was rejected because batch parsing says ignore other agent scopes. Waiting for a human was rejected because the XML role and target files define a workable boundary.
Scalability potential: Low/Middle/High/Ultra unaffected by this choice; it prevents architectural drift rather than adding runtime cost.
Hardware Impact: 0 us runtime impact on i3/MX350; prevents accidental coupling and extra hot-path lookups.

Problem: Task requires selecting mandate files before coding.
Solution: Read eight direct mandates: tether physics, rsqrt, native/job memory, zero-GC, DTO layout, cold DI, signal lanes, and telemetry.
Rejected Alternatives: Reading every registry file was rejected as token waste and not task-specific. Reading only AGENTS was rejected because the prompt explicitly requires registry mandates.
Scalability potential: Mandates force continuous quality weight, fixed buffers, and telemetry rings across low, middle, high, and ultra lanes.
Hardware Impact: 0 us runtime impact now; expected savings come from eliminating NativeList growth, sqrt, and runtime effect instantiation.

## Decisions
Problem: RB-116 used per-request `NativeList<Vector3>` for raw and smoothed abyssal paths.
Solution: Replace list length/capacity with fixed DataVault `NativeArray<Vector3>` staging buffers and two-slot `NativeArray<int>` state rows: count plus overflow flags. Jobs append by index and preserve overflow telemetry.
Rejected Alternatives: `NativeList.Clear/AddNoResize` was rejected because it still requires dynamic container ownership per request. Managed arrays were rejected for Burst path jobs. Full removal of all `H8Memory.Allocate` path snapshots was deferred because those copies need a separate scheduled read-only pin contract, not an unsafe pointer shortcut.
Scalability potential: Low tier drops failed-overflow requests without growth. Middle/high/ultra can raise fixed capacity through existing path capacity rules without changing DTO layout or authority.
Hardware Impact: i3/MX350 avoids NativeList constructor/dispose and growth checks; estimated 35-90 us saved per path request under contention.

Problem: RB-111 created cable spark and bio-cable visual roots at runtime.
Solution: `BioCableIK` now resolves authored particle roots first, then prewarmed object-pool prefab instances. `AbyssalThermalManager` now resolves authored `BioCableIK` rigs or prewarmed pool prefab instances and returns only confirmed pooled instances.
Rejected Alternatives: `new GameObject`, `AddComponent<ParticleSystem>`, `AddComponent<BioCableIK>`, and `AddComponent<AbyssalFluidDecalManager>` were rejected because they allocate managed objects and break authoring ownership. Auto-expanding pools were rejected because expansion is hidden runtime creation.
Scalability potential: Low tier can omit optional VFX by leaving pool/prefab unresolved. Middle/high/ultra can prewarm richer authored prefabs with no runtime creation path.
Hardware Impact: i3/MX350 avoids 200-900 us spikes per missed authored rig or particle root and avoids SRP batcher churn from runtime-created renderers.

Problem: Tether constraint solve still allowed an old 15-iteration cap and reconstructed inverse distance via reciprocal.
Solution: Quality weight now maps 0.0..1.0 to 2..8 iterations. The constraint pass uses `math.rsqrt(math.max(lenSq, 0.0001f))` once and reuses that inverse for direction and distance.
Rejected Alternatives: `math.sqrt`, `Vector3.magnitude`, and `math.rcp(distance)` were rejected because they waste cycles after the rsqrt is already available. Binary low/high quality switches were rejected; continuous `GlobalQualityWeight` remains the scalar.
Scalability potential: Low = 2 iterations; middle = interpolated 3-5; high = 6-7; ultra = 8. Fracture/signal truth is not tied to quality tier.
Hardware Impact: i3/MX350 saves up to 7 iterations versus old 15 cap and removes one reciprocal per constraint; estimated 0.2-0.8 us per 1000 constraints.

Problem: Predator cable bite was requested, but no safe Burst-readable `FaunaSpatialHashGrid` DTO route or authoritative `TetherStateDTO.IsFractured` owner was found in scope.
Solution: Add a managed, zero-allocation visual/signal bridge in `BioCableIK`: query `FaunaSpatialHashRegistry` into a fixed `SpatialQueryHit[8]`, filter `IFaunaSpatialContact` predators, run segment-distance checks, snap/deactivate cable visuals, and publish `OxygenCriticalSignal`, `HypoxiaSignal`, `HapticPulseSignal`, and `TetherSnappedSignal`.
Rejected Alternatives: Mutating fauna state, survival state, or `FluidPipeGraphRuntime` directly from `BioCableIK` was rejected as authority violation. Inventing `TetherFracturedSignal` or `TetherStateDTO` fields was rejected because no first-party contract was located.
Scalability potential: Low tier pays bounded 8-contact/24-segment checks only when cable active. Middle/high/ultra can increase authored predator registry fidelity without changing this bridge.
Hardware Impact: i3/MX350 bounded worst case is about 192 segment projections per active cable tick; no managed allocation and no scene search.

Problem: DataVault compaction can invalidate arrays if hot code keeps stale handles across owner phases.
Solution: New abyssal path staging handles are ensured only when the vault exists and no compaction fence is active. Write locks are acquired immediately before scheduling and released in `finally` or pending-job cleanup.
Rejected Alternatives: Holding raw `NativeArray` references beyond the scheduled job without a release route was rejected. Same-frame dispose of list-owned job buffers was removed.
Scalability potential: Low/middle/high/ultra all fail closed during compaction rather than racing relocated memory.
Hardware Impact: 0 us normal-path cost beyond lock calls; avoids crash-class stale pointer faults on weak hardware.

Problem: The first RB-116 fix still held four DataVault write locks at once for raw path, raw state, result path, and result state.
Solution: Collapse raw/result points and state into one explicit 64-byte `AbyssalPathStagingPoint` lane. A* writes `Raw` and `RawCount`; string-pull writes `Result` and `ResultCount`. Completion copies the result to the prewarmed managed snapshot while holding only the staging lock, releases it, then acquires the path snapshot lock, then separately records telemetry.
Rejected Alternatives: Keeping separate state arrays was rejected because it created a deadlock vector. Releasing staging before copying was rejected because compaction could invalidate the NativeArray. Allocating a temporary NativeArray copy was rejected because it reintroduced runtime allocation.
Scalability potential: Low tier keeps the same fixed capacity with cheaper lock traffic. Middle/high/ultra can increase waypoint capacity without changing job contracts or adding locks.
Hardware Impact: i3/MX350 removes three write-lock acquisitions per abyssal path request and eliminates nested write-lock hold time; expected gain is small per request but removes a crash-class lock ordering fault.

Problem: Packed staging DTO alignment must be proved, not assumed.
Solution: `AbyssalPathStagingPoint` is explicit 48B and `EnsureAbyssalPathStagingHandles` rejects it if `UnsafeUtility.SizeOf<AbyssalPathStagingPoint>()` is not an 8-byte multiple.
Rejected Alternatives: Sequential layout was rejected because padding could drift. A separate validator system was rejected because this gate belongs at the owner handle creation point.
Scalability potential: Same DTO on low, middle, high, and ultra; capacity scales, layout does not.
Hardware Impact: One cold branch during handle ensure; prevents ARM64 misalignment regressions.

Problem: Build verification was requested but host policy forbids builds under active compiler or CPU load above 50%.
Solution: Sampled CPU and process state. CPU reported 100%; Unity `dotnet.exe` processes were active. Build was skipped and static gates were used instead.
Rejected Alternatives: Launching `dotnet build` anyway was rejected because it violates the batch directive and can corrupt concurrent agent throughput.
Scalability potential: No runtime effect. Protects shared workstation capacity across 20+ agents.
Hardware Impact: Avoided saturating i3/MX350-class host further; no compile wall was introduced.

Problem: BioCableIK still had object-pool resolution embedded in an effect setup method reachable from visual activation, and cable arrays were sized to the authored segment count instead of fixed maximum capacity.
Solution: Cache `IObjectPoolService` in cold paths, forward it from `AbyssalThermalManager`, store the spawning pool owner for later despawn, and allocate fixed `Vector3[24]` point/velocity buffers while `_pointCount` controls active simulation work.
Rejected Alternatives: Keeping `GlobalRegistry.ObjectPoolService` inside effect setup was rejected because visual sync could reach that method. Growing arrays when segment count changes was rejected because it is a hidden runtime allocation. Adding a new visual manager was rejected because the existing manager already owns the rigs.
Scalability potential: Low uses the same fixed storage with fewer active segments. Middle/high/ultra can raise authored segment count up to 24 without reallocating or changing gameplay truth.
Hardware Impact: i3/MX350 avoids first-activation service polling and segment-count realloc spikes; estimated 5-40 us saved on cable visual activation paths and zero steady-state allocation risk.

Problem: The first-party tether ABI already exists as `HarpoonTensionSolver328.TetherStateDTO`, but the XML prompt names a separate `IsFractured` field at offset 52 that would overwrite `CurrentTension` and break the snap/tension solver.
Solution: Preserve the validated 64-byte DTO layout, add `TetherStateFlags328.Fractured` as an alias to the existing `Snapped` bit, and route fracture checks through that semantic alias.
Rejected Alternatives: Replacing offset 52 with `IsFractured` was rejected because it destroys tension magnitude and invalidates existing layout proof. Creating a second `TetherStateDTO` was rejected as contract duplication.
Scalability potential: Low, middle, high, and ultra all retain one ABI and one route; only tension stiffness scales.
Hardware Impact: 0 us runtime cost; avoids ABI churn and keeps SIMD/cache layout intact on i3/MX350.

Problem: Cable bite published zero-oxygen signals, but `HectonSurvivalSystem` did not consume `OxygenCriticalSignal`, leaving the life-support owner disconnected from the signal lane.
Solution: Add `ConsumeOxygenCriticalSignals()` in survival slow tick after normal oxygen update and before grace/death evaluation. It reads `SignalBus<OxygenCriticalSignal>.GetFrameSnapshot()`, clamps owner oxygen to the minimum signaled oxygen target, and relies on existing metabolic vault write/grace logic.
Rejected Alternatives: Directly mutating survival from `BioCableIK` was rejected as an authority violation. Adding a new signal type was rejected because `OxygenCriticalSignal` already exists and is validated at 32 bytes.
Scalability potential: Low tier pays only a bounded existing signal snapshot scan. Middle/high/ultra can publish richer signal sources without changing survival authority.
Hardware Impact: i3/MX350 cost is proportional to current frame critical oxygen signal count; no managed allocation and no scene lookup.

Problem: The pipe oxygen graph still accepted electrolysis injection during a cable-bite frame unless its owner consumed the existing critical oxygen signal, but broad gating on any critical oxygen signal would stop pipe production for unrelated hazards.
Solution: Add a narrow owner-side gate in `FluidPipeGraphRuntime.ApplyElectrolysisInputs`: clear oxygen source demand rates first, read `SignalBus<OxygenCriticalSignal>.GetFrameSnapshot()`, and skip electrolysis injection only for `SourceBioCablePredatorBite` when oxygen is <= 0.001, severity is critical, and the life-support cutoff flag is present. The source/severity/flag constants live on `OxygenCriticalSignal` so publisher and consumer cannot drift.
Rejected Alternatives: Mutating pipe buffers directly from `BioCableIK` was rejected as cross-domain state corruption. Gating on all critical oxygen signals was rejected because vocal warnings, hypoxia, or future atmosphere hazards should not automatically sever pipe electrolysis. Adding a new `TetherFracturedSignal` was rejected because no first-party payload exists and `OxygenCriticalSignal` already carries the cutoff. Editing the pipe solve job was rejected because it holds many owner locks and would widen the lock surface.
Scalability potential: Low, middle, high, and ultra use the same signal gate; quality weight cannot change oxygen truth, only presentation/cadence elsewhere.
Hardware Impact: i3/MX350 adds one bounded frame-snapshot scan before pipe solve; avoids wasted electrolysis input and preserves zero managed allocation.

Problem: The XML demanded `_EmissionStrength` extinction, but `BioCableIK` does not own a local `Renderer`; it submits splines through `ConnectionSplineBatchRenderer`.
Solution: Treat `SetCableActive(false)` as the legal visual-sync extinction route: it removes the submitted spline link and stops authored/pooled spark particles after predator bite.
Rejected Alternatives: Creating a `MaterialPropertyBlock` for a nonexistent local renderer was rejected. Global shader side effects were rejected because they would dim unrelated content.
Scalability potential: Low tier deletes the link immediately. Middle/high/ultra can author richer pooled sparks before the link is removed without changing the runtime route.
Hardware Impact: i3/MX350 avoids material state churn and renderer lookup; visual extinction is a renderer batch removal plus particle stop only.

Problem: A Burst-side fauna fracture route was requested, but the native spatial hash discovered on disk is `ShinobuSpatialGridSolver` under AI ownership, not the managed `FaunaSpatialHashRegistry` contract used by live fauna contacts.
Solution: Keep the zero-alloc managed bridge in `BioCableIK` and document the Burst route as blocked by missing owner-owned read model. The bridge publishes existing signals and does not mutate AI state.
Rejected Alternatives: Importing AI spatial grid handles into the cable job was rejected because it would introduce a direct dependency on another owner and stale compaction locks. Duplicating fauna grid DTOs in physics/world was rejected as data-route duplication.
Scalability potential: Low tier keeps the 8-contact cap. Middle/high/ultra can replace the registry with an owner-published read model later without changing the signal output.
Hardware Impact: i3/MX350 remains bounded at 192 segment projections per active cable tick; no native lock or job completion dependency is added.

Problem: RB-116 still had six per-request persistent A* scratch arrays for parents, g/f scores, closed flags, heap nodes, and heap positions after the `NativeList<Vector3>` route was removed.
Solution: Grow `AbyssalPathStagingPoint` to an explicit 64-byte DTO and pack the A* scratch lanes into the existing `VegetationAbyssalPathStagingPacked` buffer. The staging handle now allocates `max(nodeCount, pathCapacity)` records under the existing single write lock, while A* and string-pull jobs receive `PathCapacity` so raw/result waypoint counts cannot expand to node capacity.
Rejected Alternatives: Adding a separate scratch DataVault handle was rejected because it would hold a second write lock while the existing path staging lock is live. Keeping six `H8Memory.Allocate` arrays was rejected because it leaves allocator churn in the path request. Passing unpinned vault read views into jobs was rejected because `IDataVault.TryReadOnlyHandle` is documented as current-phase only and does not pin relocation metadata.
Scalability potential: Low uses the same preallocated scratch arena with smaller node/path caps. Middle/high/ultra raise capacity without changing DTO layout or adding lock lanes.
Hardware Impact: i3/MX350 removes six persistent allocations and six deferred releases per A* path request; estimated 20-70 us/request saved depending on node count and allocator contention, with one 64-byte packed record per node retained as reusable vault memory.

Problem: The path scheduler still copied predator fear, abyssal nav, and threat voxel data into per-request H8Memory arrays to protect scheduled jobs from DataVault compaction.
Solution: Replace those copies with explicit read pins via `IDataVault.TryLockBuffer` and a pending-job `ReadPinVault/ReadPinMask` release route. A* receives pinned predator/nav/threat arrays. String-pull reuses the pinned threat voxel view instead of allocating a smoothing-side byte copy.
Rejected Alternatives: Passing unpinned `TryReadVegetationMemoryBuffer` views into jobs was rejected as a compaction race. Copying every source to H8Memory was rejected because it preserved RB-116 allocator churn. Adding a second write-locked staging buffer for read-only sources was rejected as deadlock surface.
Scalability potential: Low, middle, high, and ultra share the same pin contract; quality changes capacity/cadence elsewhere, not data ownership.
Hardware Impact: i3/MX350 removes five path-source copies on non-macro A* requests and one smoothing threat voxel copy; estimated 15-60 us/request saved depending on grid size and allocator pressure.

Problem: Threat propagation still allocated multiple persistent byte/float arrays per solve for previous threat, previous echo, compressed output, echo output, threat output, and voxel output.
Solution: Add `ThreatPropagationStagingPoint` as an explicit 16-byte DTO in `VegetationThreatPropagationStagingPacked`. The propagation job reads previous lanes and writes next lanes; voxelization reads next threat and writes the voxel lane. Completion copies lanes into prewarmed owner commit arrays, releases the staging write lock, then publishes final vault buffers one at a time.
Rejected Alternatives: Writing directly into the four final threat vault buffers was rejected because it would hold multiple DataVault write locks through scheduled jobs. Holding staging plus final write locks during commit was rejected as a lock-order deadlock vector. Keeping the H8Memory arrays was rejected because it preserved per-solve allocator churn.
Scalability potential: Low uses the same packed lane with lower cadence and grid capacity. Middle/high/ultra can increase grid/voxel resolution through existing threat metadata without changing the packed DTO layout.
Hardware Impact: i3/MX350 removes six per-solve propagation allocations/releases from the threat route; expected gain is 25-90 us/solve plus lower allocator fragmentation. Commit copy cost remains deterministic and lock-flattened.

Problem: Flow-field solve still allocated `navSupportGrid`, `threatGridSnapshot`, and `flowOutput` arrays per solve after threat propagation was packed.
Solution: Add `FlowFieldStagingPoint` as an explicit 16-byte DTO in `VegetationFlowFieldStagingPacked`. Stage threat and nav-support input into the first half of the staging buffer, write flow output into the second half, copy the output half into a prewarmed `float2[]`, release staging, then publish the final flow field with one owner write lock.
Rejected Alternatives: A single input/output record per cell was rejected because the parallel flow job would read neighbor threat/nav fields while other workers write whole structs, creating a data race. Keeping the H8Memory arrays was rejected because it preserved allocator churn. Running the old managed nav-support loop under the staging lock was rejected because it puts heavy math inside a lock window.
Scalability potential: Low keeps lower flow solve cadence and smaller grid capacity. Middle/high/ultra can spend saved allocator budget on higher flow resolution and stronger wake/road response without changing gameplay truth ownership.
Hardware Impact: i3/MX350 removes three per-solve allocations/releases from the flow-field route; expected gain is 12-45 us/solve plus lower allocator fragmentation. The two-half staging buffer costs one reusable 32 bytes per grid cell.

Problem: Thermal solve still allocated `ThermalOutput`, `FlowVolumeOutput`, and `PreviousFlowVolumeSnapshot` arrays per solve.
Solution: Add `ThermalGridStagingPoint` as an explicit 32-byte DTO in `VegetationThermalGridStagingPacked`. The previous flow lane and thermal lane occupy the first half, the new flow lane occupies the second half, and completion copies results into prewarmed `float[]`/`float3[]` commit caches before publishing final vault buffers one at a time.
Rejected Alternatives: Writing directly to `VegetationAbyssalThermalGrid` and `VegetationAbyssalFlowVolume` from scheduled jobs was rejected because it would hold two final write locks through the job window. Keeping previous-flow H8Memory copies was rejected because it preserves allocator churn.
Scalability potential: Low lowers solve cadence and thermal volume capacity. Middle/high/ultra can spend the fixed staging budget on denser thermal wake and biolume response without changing authority or DTO layout.
Hardware Impact: i3/MX350 removes three per-solve persistent allocations/releases from thermal solve; estimated 15-55 us/solve saved plus lower allocator fragmentation.

Problem: The path smoothing route still copied voxel passability and artificial structures into per-request H8Memory arrays after the main A* scratch purge.
Solution: Store voxel passability bytes in the existing `AbyssalPathStagingPoint.ScratchFlags` lane and make `NativeAStarJob.ResetScratchNode` preserve that lane. Path smoothing reads passability through `PathStaging[flatIndex].ScratchFlags`. Artificial structures are read-pinned from `VegetationArtificialStructureRecords` and passed with an explicit logical count.
Rejected Alternatives: Copying passability after A* scheduling was rejected because it races the job on the same staging buffer. Adding another staging DTO was rejected because `ScratchFlags` already had one byte of unused lane capacity. Keeping the artificial-structure H8Memory copy was rejected because it is a steady-state path allocation.
Scalability potential: Low uses the same fixed packed staging with smaller voxel payloads. Middle/high/ultra can raise path/passability capacity through the existing staging handle without adding a new buffer owner.
Hardware Impact: i3/MX350 removes the last path-side generic H8Memory snapshot plus the artificial-structure copy; estimated 5-35 us/path request saved depending on voxel payload and structure count.

Problem: Threat propagation still used `TryPrepareArtificialStructureJobSnapshot`, allocating a persistent copy for both propagation and voxelization jobs.
Solution: Add a threat-propagation read-pin mask for `VegetationArtificialStructureRecords`, pass the pinned native view plus `ArtificialStructureCount` into both jobs, and release the pin in `ReleaseThreatPropagationPendingJob`.
Rejected Alternatives: Sharing the chunk residency snapshot helper was rejected because it allocates by design for chunk build ownership. Scanning full buffer capacity was rejected because unused records can look like valid zero bounds.
Scalability potential: Low, middle, high, and ultra share one read-pin route; quality can scale cadence and grid size but not structure truth ownership.
Hardware Impact: i3/MX350 removes one persistent allocation/release per threat propagation solve; estimated 3-20 us/solve saved and less allocator pressure.

Problem: Build verification remained requested after the staging purge.
Solution: Sampled CPU/compiler state again. CPU was 100% and `dotnet.exe` PIDs 3100 and 23988 were active, so no build was launched. Static gates covered exact-word stale symbol scans, brace balance, DTO size guards, diff whitespace, orphan `.meta`, and hot-token sweeps.
Rejected Alternatives: Running `dotnet build` under CPU/compiler contention was rejected by the compilation throttle and would contend with concurrent agents.
Scalability potential: No runtime change; protects shared workstation throughput.
Hardware Impact: Avoided additional compiler load on a saturated host.

Problem: Chunk residency still used `TryPrepareArtificialStructureJobSnapshot` and copied `VegetationEcosystemThreatEcho` into a per-build H8Memory byte array before scheduling anchored vegetation jobs.
Solution: Replace both copies with DataVault read pins owned by `ChunkBuildPendingJob`. Pass pinned artificial structures plus `ArtificialStructureCount` into `GenerateAnchoredVegetationJob`, and release pins in failed scheduler paths or chunk-job cleanup after completion.
Rejected Alternatives: Keeping the old snapshot helper was rejected because it preserved allocator churn and stale dead code. Scanning full vault capacity was rejected because unused slots can mimic zero-valued structure records. Holding a write lock was rejected because these are read-only job inputs.
Scalability potential: Low, middle, high, and ultra share the same pin/count route; quality can scale chunk cadence and density without changing structure truth ownership.
Hardware Impact: i3/MX350 removes one artificial-structure allocation/copy and one threat echo byte allocation/copy per eligible chunk build; estimated 4-28 us/build saved depending on structure count and threat grid size.

Problem: Terrain holes still used `TryCreateTerrainHoleJobSnapshot`, allocating a TempJob H8Memory copy for abyssal path smoothing and chunk-build vegetation jobs.
Solution: Remove the snapshot helper and read-pin `VegetationTerrainHoleRecords` through the existing abyssal-path and chunk-build pin masks. Jobs receive pinned `NativeArray<TerrainHoleRecord>` views plus logical counts and cleanup releases the pins after job completion.
Rejected Alternatives: Keeping the TempJob snapshot was rejected because it preserved allocator churn in both path and chunk routes. Pinning active tile sand/rock/height caches was rejected because tile eviction and removal currently mark matching chunk jobs cancelled before disposing active tile cache buffers; replacing those protective copies would require disposal semantics changes or forced completion stalls.
Scalability potential: Low, middle, high, and ultra share the same terrain-hole owner buffer; quality can scale chunk/path cadence without changing hole truth ownership. Tile cache snapshots remain the correct low-risk route until eviction has non-stalling pin-aware disposal.
Hardware Impact: i3/MX350 removes one terrain-hole allocation/copy/release per path smoothing request with holes and per eligible chunk build; estimated 2-16 us saved depending on hole count while avoiding use-after-release risk on tile cache buffers.

Problem: Internal abyssal path smoothing, threat propagation, flow-field, and thermal jobs still consumed density query chunks, density grids, and threat-attractor grids through per-solve H8Memory copies.
Solution: Add a shared `TryPinVegetationReadBuffer` helper and a `TryPinDensityQueryJobSnapshot` route. Pending jobs now own the density read pins through their existing `ReadPinVault` and `ReadPinMask` fields, and release all density/threat-attractor pins after job completion.
Rejected Alternatives: Reusing the public `TryPrepareDensityQueryJobSnapshot` copies was rejected for internal pending jobs because it preserved allocator churn. Passing unpinned density views was rejected as a compaction race. Replacing public visibility/biomass APIs with pins was rejected because those APIs return an external `JobHandle` and have no pending owner that can guarantee managed DataVault unlock at caller completion.
Scalability potential: Low reduces allocator pressure while keeping the same density truth. Middle/high/ultra can spend the saved budget on higher flow/threat solve cadence without changing DTO layout or ownership.
Hardware Impact: i3/MX350 removes two or three density-related H8Memory allocations/releases from each internal path/threat/flow/thermal schedule; estimated 6-34 us saved per solve depending on chunk count and grid size.

Problem: `RebuildDensityQuerySnapshot` allocated three TempJob scratch arrays on every rebuild even though selected chunk capacity is owner-bounded.
Solution: Add persistent tracked scratch arrays for density chunks, density grid, and threat-attractor grid. They are prewarmed to `InitialChunkArrayCapacity`, cold-grown with overflow guards only if capacity policy expands, and disposed through `DisposeDensityQuerySnapshot`.
Rejected Alternatives: Keeping TempJob scratch was rejected because rebuild cadence can coincide with residency churn. Publishing directly into the final vault buffers during accumulation was rejected because it would hold write locks around heavy density math.
Scalability potential: Low avoids repeated rebuild allocator pressure. Middle/high/ultra can increase selected chunk capacity and reuse the same persistent scratch route.
Hardware Impact: i3/MX350 removes three TempJob allocations/releases per density snapshot rebuild; estimated 8-30 us saved per rebuild plus reduced allocator fragmentation.

Problem: Chunk-build scheduling still allocated three persistent `JobInstanceRecord` arrays per build for grass, floating vegetation, and kelp outputs.
Solution: Prewarm one grass/floating/kelp `NativeArray<JobInstanceRecord>` bank per fixed chunk-build slot and pass `GetSubArray(0, count)` to jobs. `GenerateAnchoredVegetationJob` and `GenerateFloatingVegetationJob` now write `Output[index] = default` before any placement early return, preserving the old ClearMemory invalid-record contract. `UnsafeUtility.SizeOf<JobInstanceRecord>() & 7` guards bank allocation.
Rejected Alternatives: Keeping `H8Memory.Allocate<JobInstanceRecord>` per build was rejected because it preserved allocator churn in residency scheduling. Sharing one bank across jobs was rejected because parallel chunk builds would race writes. Pinning final chunk pools as direct job outputs was rejected because payload finalization needs valid-record compaction and owner-side pool slice allocation after completion.
Scalability potential: Low uses the same banks with lower chunk cadence and sparse counts. Middle/high/ultra reuse fixed banks while spending saved allocator budget on denser visible grass and richer abyssal kelp density.
Hardware Impact: i3/MX350 removes three persistent allocations/releases per scheduled chunk build; estimated 10-45 us saved per build plus reduced allocator fragmentation.

Problem: Chunk-build scheduling still copied active tile sand mask, rock mask, and height samples into three persistent H8Memory arrays before scheduling grass/kelp/floating vegetation jobs.
Solution: Add a pin-aware active tile cache route. `TryPinActiveTileCacheForJob` locks the three dynamic tile cache buffers, reads their NativeArray views, and stores the exact BufferIDs in `ChunkBuildPendingJob`. Pending-job cleanup unlocks those dynamic read pins after job completion. Tile cache eviction/removal now defers native cache disposal while same-tile chunk jobs are active or cancelled but not finalized.
Rejected Alternatives: Keeping copies was rejected because it preserved the last major per-build tile allocator route. Pinning without deferred disposal was rejected because eviction/removal can mark chunk jobs cancelled before the scheduler releases their NativeArray aliases. Forcing same-frame completion during eviction was rejected because it would introduce a main-thread stall.
Scalability potential: Low avoids allocator spikes during tile churn. Middle/high/ultra can raise near-field chunk density while reusing the same active tile cache buffers and DataVault read pins.
Hardware Impact: i3/MX350 removes three allocation/copy/release paths per scheduled chunk build; estimated 8-35 us saved per build depending on tile resolution, with no extra write lock and no synchronous completion.

Problem: Public vegetation density sampling still allocated TempJob H8Memory snapshots for density chunks and density grids every time visibility or biomass jobs were scheduled.
Solution: Add a fixed `DensityQuerySnapshotLease` bank owned by `HectonMapMagicVegetationBridge`. Public density schedulers copy the current immutable snapshot into a reusable persistent lease, schedule the job, and store the returned `JobHandle` in the lease. Later schedule attempts reclaim only leases whose stored handle reports completed.
Rejected Alternatives: Directly pinning the DataVault for these public APIs was rejected because the caller owns the returned `JobHandle`, and there is no safe managed unlock owner after arbitrary external dependency chaining. Keeping H8Memory TempJob copies was rejected because it kept allocator churn on every query. Forcing completion to reclaim a lease was rejected because it would create the exact stall vector the protocol forbids.
Scalability potential: Low devices get bounded no-allocation visibility/biomass sampling. Middle/high/ultra can issue up to four concurrent public density queries and spend saved allocator budget on richer vegetation concealment and biomass readbacks.
Hardware Impact: i3/MX350 removes two TempJob allocation/release chains per public density sample job; estimated 4-18 us saved per schedule plus lower allocator fragmentation.

Problem: The first density lease bank reclaimed slots by checking `JobHandle.IsCompleted` and then clearing the handle, which risks reusing a `NativeArray` before Unity safety ownership is formally completed.
Solution: Reclaim public density leases through `DispatcherJobFence.TryFinalizeCompleted(ref lease.Handle)`. This calls `Complete()` only after the handle reports completed and resets the handle before the slot becomes available.
Rejected Alternatives: Raw `IsCompleted` reuse was rejected because it is not a safety-handle completion. Forced completion of unfinished jobs was rejected because public query callers own the returned handle and a reclaim stall would violate the scheduler throttle.
Scalability potential: Low, middle, high, and ultra use the same no-stall lease ownership route; high/ultra can keep four concurrent public vegetation queries without extra allocator churn.
Hardware Impact: i3/MX350 cost is one completed-handle finalize per reclaimed lease; no wait path is introduced because unfinished handles are skipped.

Problem: The lease bank removed TempJob H8Memory snapshots, but `TryAcquireDensityQuerySnapshotLease` could still cold-grow persistent `NativeArray` storage from a public query schedule path.
Solution: Prewarm the four public density snapshot leases during runtime initialization for the fixed selected-chunk capacity. `TryAcquireDensityQuerySnapshotLease` now only reclaims completed handles and checks ready capacity; it does not allocate or grow.
Rejected Alternatives: Lazy allocation on first public query was rejected because it hides runtime allocator work in a schedule route. Auto-growing the selected chunk arrays was rejected because residency selection is already fixed-capacity and overflow is reported through existing telemetry.
Scalability potential: Low devices never pay a first-query allocator spike. Middle/high/ultra keep four bounded concurrent public visibility/biomass queries without changing density truth ownership.
Hardware Impact: i3/MX350 moves four chunk/density lease allocations to cold initialization and removes the first-query persistent allocation spike from public sampling.

Problem: `OnDisable` disposes density snapshot resources, but `OnEnable` restored chunk/job/native pools without restoring density scratch and public lease banks.
Solution: Mirror density scratch and public lease prewarm in `OnEnable`, immediately after chunk record bank recovery and before path/navigation preallocation.
Rejected Alternatives: Waiting for the next public schedule to repair the bank was rejected because public schedule is now allocation-free and must fail closed, not allocate. Rebuilding in `Tick` was rejected because it would hide lifecycle recovery in a hot phase.
Scalability potential: Low through ultra retain the same fixed public density sampling capacity across disable/enable cycles.
Hardware Impact: i3/MX350 avoids a post-reenable fail-closed visibility/biomass sampling gap without adding per-frame work.

Problem: Bio-cable predator bite broadphase used full cable length from start/end midpoint, which overqueries coiled cables and trusted the registry result count before indexing the fixed hit scratch.
Solution: Build a fixed 24-point cable bounds sphere, derive radius with `math.rsqrt`, clamp `CollectContactsNonAlloc` result count to `SpatialQueryHit[8]`, and clear only the safe written range.
Rejected Alternatives: Keeping full-length overquery was rejected because it spends fauna broadphase budget on empty space. Allocating a dynamic hit list was rejected because bite detection is in the cable tick path.
Scalability potential: Low devices query a tighter sphere with the same 8-contact cap. Middle/high/ultra can author more active cables without multiplying broadphase waste.
Hardware Impact: i3/MX350 pays two fixed 24-point loops but reduces fauna hash candidates; worst-case scratch indexing is bounded and cannot exceed 8.

Problem: `SetCableActive(true)` could call `EnsureChargeEffects`, which can resolve pooled spark effects from a runtime visual activation path.
Solution: Remove effect resolution from `SetCableActive`; spark resolution remains in `Awake` and `ConfigureObjectPoolServiceCold`, where object-pool service injection is cold.
Rejected Alternatives: Lazy first-activation pool spawn was rejected because it hides VFX ownership work in `LateFrameTick`. Recreating runtime particle systems was already rejected by RB-111.
Scalability potential: Low devices can run cable visuals without optional sparks. Middle/high/ultra can prewarm richer spark prefabs and inject them before presentation ticks.
Hardware Impact: i3/MX350 removes first-active pool lookup/spawn risk from cable presentation; no per-frame cost added.

Problem: `SolveTetherConstraintsJob` wrote segment tensions and force packets during every projection iteration, duplicating output memory traffic while consumers only read the completed frame.
Solution: Keep all constraint math and peak/error accumulation intact, but write `SegmentTensions` and `ForcePackets` only on the final 2..8 iteration; compute inverse-mass reciprocal once per solved constraint.
Rejected Alternatives: Writing every iteration was rejected as redundant memory traffic. Reducing iterations further was rejected because `GlobalQualityWeight` already controls 2..8 projection fidelity.
Scalability potential: Low uses 2 iterations with one output pass. Middle/high/ultra keep higher projection fidelity without multiplying final-output writes.
Hardware Impact: i3/MX350 removes up to 7 redundant tension/force packet write passes per constraint at ultra quality and one duplicate reciprocal per active solved constraint.

Problem: `TetherAupBlackBoxDumper.TryDumpLatestVault` returned success after a void dump writer call, so fault handling could report a dump even when the writer failed internally.
Solution: Return `TetherBlackBoxDumpWriter.TryWritePrimaryAndLegacy(...)` directly while preserving the existing `Dump_CABLE_SURGEON.bin` and `.h8dump` paths.
Rejected Alternatives: Adding a new `Dump_1709` alias was rejected because SHINOBU_143 architecture and tuner documentation already own `Dump_CABLE_SURGEON`. Keeping the void call was rejected because it hides IO failure from callers.
Scalability potential: Low through ultra unchanged; this is fault-path truthfulness, not a fidelity path.
Hardware Impact: 0 us steady-state. On fault, callers now get an accurate bool instead of a false-positive dump result.

Problem: Bio-cable spark setup configured pooled/authored particles and then played the particle system with zero emission, while predator-bite tick performed visual-state and renderer sync after `SetCableActive(false)` had already removed the spline link.
Solution: Keep spark configuration cold but stop inactive zero-emission particles instead of playing them. Let `ApplyVisualState` start sparks only when the charge gate opens. Return immediately after predator bite deactivates the cable. Remove unused `planarDelta` locals from cable visual and zone checks.
Rejected Alternatives: Keeping a zero-emission particle system alive was rejected because it burns presentation overhead for no pixels. Forcing a renderer sync after cable deactivation was rejected because `SyncRenderer` correctly returns inactive and the spline link has already been removed.
Scalability potential: Low devices avoid idle spark simulation. Middle, high, and ultra still get authored/pool sparks when EMP charge crosses the continuous threshold.
Hardware Impact: i3/MX350 saves small but steady presentation work on inactive/far cable rigs; estimated 1-5 us/frame in scenes with multiple dormant bio-cables and no gameplay-truth change.

Problem: `AbyssalThermalManager.ReleaseBioCableVisualsToPool` deactivated pooled rig GameObjects but skipped `BioCableIK.SetCableActive(false)` for authored rigs, leaving an authored spline link capable of surviving manager cleanup.
Solution: Deactivate every non-null bio-cable rig through `SetCableActive(false)` during cleanup, then despawn and clear only instances marked as pooled.
Rejected Alternatives: Destroying authored rigs was rejected because scene-authored visuals are not pool-owned. Leaving authored rigs active was rejected because presentation ownership must stop at manager cleanup without relying on Unity object disable side effects.
Scalability potential: Low avoids stale spline submissions after thermal-manager teardown. Middle, high, and ultra keep richer authored cable rigs while using the same no-allocation cleanup route.
Hardware Impact: i3/MX350 removes a stale visual/update fault surface; steady-state cost is 0 us and cleanup cost is bounded by the existing fixed cable rig array.

Problem: Optional DataVault read pins were kept until scheduled-job cleanup even when optional reads failed and the native arrays were not passed to the job.
Solution: Release predator-fear, artificial-structure, threat-voxel, flow nav-node, and previous-flow optional pins immediately on read failure, clearing the exact pending read-mask bit at the failure site.
Rejected Alternatives: Waiting for pending-job cleanup was rejected because an unused lock can block compaction longer than needed. Retrying the read under the same lock was rejected because optional inputs already have fail-closed job defaults.
Scalability potential: Low devices shorten compaction stalls under partial data availability. Middle, high, and ultra can run larger vegetation buffers without accumulating unused read locks.
Hardware Impact: i3/MX350 saves small lock-hold windows under data-missing cases and removes a deadlock-adjacent fault surface; no new allocations or job completions are introduced.

Problem: Active bio-cables with a closed EMP charge gate could keep a zero-emission particle system playing, and pooled cable rigs returned to the manager pool without returning their nested pooled spark object.
Solution: `ApplyVisualState` now stops spark particles whenever `sparkGate <= 0f`. Pooled rigs call `PrepareForPoolReturnCold()` before manager despawn, which deactivates the cable and releases the nested spark pool object. The release path keeps cold reacquire possible when an authored spark prefab and object pool are still configured.
Rejected Alternatives: Keeping zero-emission particles warm was rejected because it burns presentation time for no pixels. Destroying nested spark objects was rejected because RB-111 requires prewarmed pools. Returning authored rig spark roots was rejected because authored scene roots are not pool-owned.
Scalability potential: Low devices avoid invisible particle simulation and nested-pool leaks. Middle, high, and ultra keep richer spark prefabs, but only when the charge gate buys visible pixels.
Hardware Impact: i3/MX350 saves small per-frame presentation cost in active low-charge cable scenes and prevents pooled spark depletion during repeated cable checkout/return cycles.

Problem: HLOD registry rebuild held a DataVault write lock while running structure distance culling, camera-relative AUP math, and managed snapshot writes.
Solution: Build `_hlodRegistrySnapshot` and `_hlodRegistryCount` before taking the DataVault write lock, then publish through the existing mirror function so the lock scope is only a bounded memory copy.
Rejected Alternatives: Keeping culling under lock was rejected because it lengthens compaction-fence blocking. Adding a new HLOD manager or a second DataVault route was rejected because the existing synchronizer owns the registry snapshot and already has the mirror route.
Scalability potential: Low devices shorten write-lock duration during structure churn. Middle, high, and ultra can carry more authored artificial structures without widening the lock-heavy section.
Hardware Impact: i3/MX350 reduces lock hold time by moving per-structure math out of the critical section; expected gain depends on structure count, with deadlock/stall risk reduced more than raw microseconds.

Problem: HLOD visible/registry arrays could still grow from `SlowTick` through `EnsureHLODDataCapacity` when authored mega-wreck or persistent-structure capacity exceeded the previous managed snapshot size.
Solution: Prewarm `_hlodRegistrySnapshot` and `_visibleHlodSnapshot` in cold lifecycle to `megaWreckDefinitions.Length + MaxPersistentArtificialStructureRecords`.
Rejected Alternatives: Letting `SlowTick` allocate on first large structure set was rejected as hidden steady-state GC. Allocating unbounded defensive arrays was rejected because capacity must follow authored structures plus the fixed persistent record limit.
Scalability potential: Low devices pay capacity once during cold setup. Middle, high, and ultra can author more persistent structures inside the same deterministic capacity rule.
Hardware Impact: i3/MX350 moves managed array allocation away from runtime tick and prevents a first-HLOD-update GC spike.

Problem: Pool-spawned bio-cable rigs and spark effects could be accepted even when the object pool could not later despawn them without destruction.
Solution: Require pool existence, positive available count, and `CanDespawnWithoutDestroy` before accepting a pooled `BioCableIK` or spark object. Misconfigured or empty pool routes fail closed or return the rejected instance through `Despawn`.
Rejected Alternatives: Accepting an unowned spawned object was rejected because cleanup would either leak it or destroy a runtime object. Runtime instantiate/destroy fallback was rejected by RB-111. Adding a second VFX manager was rejected because `AbyssalThermalManager` already owns cable visual routing.
Scalability potential: Low devices can run without optional spark/cable prefab pools. Middle, high, and ultra can prewarm richer prefab variants, but every accepted runtime instance remains pool-owned and despawn-safe.
Hardware Impact: i3/MX350 avoids hidden destroy/instantiate spikes and pool depletion during repeated cable checkout/return cycles; estimated 20-80 us saved on bad-pool edge cases, 0 us added to steady-state ticks.

Problem: Abyssal path and thermal black-box dump paths still allocated temporary `NativeArray<byte>` payloads on fault.
Solution: Add owner-lifecycle persistent byte payloads sized to the fixed 300-frame rings: abyssal path uses 20-byte header plus 300 x 56-byte rows, thermal uses 8-byte header plus 300 x 60-byte rows. Cold lifecycle prewarms these buffers; dump paths only check readiness and write into the existing payload.
Rejected Alternatives: Keeping `Allocator.Temp` was rejected because crash/fault paths must not allocate while the system is already unstable. Stackalloc was rejected because `NativeFaultDumpWriter.TryWriteAll` consumes `NativeArray<byte>`. Growing payloads from dump/record routes was rejected because it hides allocation behind fault handling.
Scalability potential: Low through ultra use the same fixed black-box capacity and binary layout. Quality weight does not alter fault evidence layout or ownership.
Hardware Impact: i3/MX350 saves a fault-time allocator call and sentinel register/unregister churn per dump; 0 us steady-state cost after cold prewarm.

Problem: Abyssal path telemetry used a local helper that could still fall back to `GlobalRegistry.DataVault`, and DataVault hot-swap did not explicitly release the old abyssal telemetry handle.
Solution: Bind abyssal telemetry only through the already cached `_vegetationMemoryVault`. `RebindVegetationMemoryVault` now calls `RebindAbyssalPathTelemetryVaultCold`, which releases the old telemetry buffer on the previous vault before assigning the new vault and re-ensuring telemetry only when the fixed dump payload is already ready.
Rejected Alternatives: Keeping the registry fallback was rejected because completion/record routes can reach telemetry ensure. Releasing all vegetation state on hot-swap was rejected because it is a broader ownership reset than this bug requires.
Scalability potential: Low, middle, high, and ultra use one cached DataVault route; quality weight cannot change telemetry ownership or handle generation.
Hardware Impact: 0 us steady-state; removes a stale-vault release fault and one registry fallback branch from telemetry recovery.

Problem: `DumpVegetationMemoryBlackBox` still allocated a temporary byte payload with `Allocator.Temp` even after abyssal path and thermal dumps were moved to fixed buffers.
Solution: Add a fixed 19224-byte persistent `_vegetationMemoryTelemetryDumpPayload` prewarmed in `EnsureVegetationMemoryTelemetryCold` and disposed in `ReleaseVegetationMemoryTelemetryResources`. Fault dump now writes header and ring bytes into that payload.
Rejected Alternatives: Keeping the Temp allocation was rejected because black-box dump must work when allocator state may already be compromised. Stackalloc was rejected because the first-party dump writer expects a `NativeArray<byte>`. Lazy allocation from dump was rejected because fault handling must not allocate.
Scalability potential: Low through ultra keep a 300-frame binary ring with identical layout. Device quality can alter vegetation solve cadence, not fault evidence capacity.
Hardware Impact: i3/MX350 removes one Temp NativeArray allocation/dispose from vegetation memory fault dumps; 0 us steady-state after cold prewarm.
