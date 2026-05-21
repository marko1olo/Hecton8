# SHINOBU_268 Rationale

Status: PENDING VERIFICATION

## Initial Boundary

Problem: Ambient flora destruction is a presentation effect, not gameplay truth. Physics debris, mesh severing, colliders, and broadphase queries would spend CPU on state the player does not need as authority.
Solution: Use the Dear Lie path: data-only matrix scale swap to zero, typed VFX signal dispatch, timed data-only regeneration, and telemetry ring proof.
Rejected Alternatives: Unity Rigidbody debris, MeshCollider slicing, Physics.OverlapSphere, Transform hierarchy search, and GameObject lifecycle management. These are main-thread and broadphase costs for presentation-only feedback.
Scalability potential: Low uses silent vanish or minimal VFX under continuous GlobalQualityWeight. Middle emits sparse GPU particles. High emits richer species-colored debris. Ultra spends saved CPU on denser GPU-only leaf/silt feedback without changing gameplay truth.
Hardware Impact: Expected hot event cost target is under 10 microseconds on i3/MX350 class hardware, pending compile and runtime profiler proof.

## Mandate Selection

Problem: Task touches flora rendering, hot damage routing, native payloads, AUP math, SignalBus, and crash telemetry.
Solution: Read cinematic fake, zero-GC, ARM64 layout, AUP precision, instanced flora, SignalBus segregation, and postmortem telemetry mandates before code.
Rejected Alternatives: Starting from existing Unity physics behavior or inventing a direct combat dependency before source scan.
Scalability potential: Continuous quality controls VFX dispatch cadence/severity without binary tier switches.
Hardware Impact: Prevents physics broadphase and GameObject churn on low-end silicon; preserves visual-overkill budget for top-tier devices.

## Phase 1-2 Decisions

Problem: Flora destruction needed immediate feedback but existing flora is indirect native payload, not GameObject instances.
Solution: Kept `DestructibleOrganicManager` as owner and added a Dear Lie sub-route: `CombatDamageSignal` snapshot staging into Vault buffer `72982`, flat bucket-head/next AUP cell lookup in Vault buffers `72987..72990`, matrix basis scale-to-zero, owner-fenced `DebrisSpawnSignal.TryPush`, and 300s native regeneration in Vault buffer `72985`.
Rejected Alternatives: New MonoBehaviour router owning duplicate state; Physics.OverlapSphere; Rigidbody debris; mesh severing; prefab particle instantiation. All violate single owner or main-thread budget.
Scalability potential: Low uses probability-gated/no VFX vanish; Middle emits sparse organic debris; High increases quantity continuously; Ultra spends saved CPU on richer GPU debris through the existing VFX lane.
Hardware Impact: Low-end i3/MX350 estimate: 10-40 us/event removed versus physics/object paths. Top-tier estimate: same CPU route buys visual particle density, not gameplay authority.

Problem: Prompt demanded exact ARM64 DTO layout.
Solution: `FloraDestructionEventDTO` is explicit 32 bytes: `double3 ImpactAUP` offset 0, `uint FloraTypeHash` offset 24, `uint _pad0` offset 28. Magnitude is packed as float bits in `_pad0` to preserve the required envelope.
Rejected Alternatives: Adding `float Magnitude01` as a named DTO field or using automatic layout. Both break the requested byte contract.
Scalability potential: Same DTO feeds all quality weights without layout mutation.
Hardware Impact: Alignment avoids unaligned load faults on ARM64; runtime cost estimate 0 us, failure-prevention only.

Problem: Spatial query must survive 100km AUP edges.
Solution: Hash uses `long floor(doubleAup / cellSize)` directly from `double3`; runtime matrix translation is combined with current double origin only after candidate filtering.
Rejected Alternatives: Convert AUP to `float3` before hashing, Unity Physics scene queries, or Transform hierarchy search.
Scalability potential: Cell hash capacity resizes by payload count; quality only gates VFX, not query truth.
Hardware Impact: Avoids false misses at far offsets; i3/MX350 estimate 12 us/event saved versus dense linear candidate scans.

Problem: Plants must regrow after visual destruction without entering persistent world tombstones or rollback state.
Solution: Store original matrix by instance UID, queue `FloraDearLieRegenRecord` for current time + 300 seconds, then call the existing regrowth visual route at expiry.
Rejected Alternatives: Registering destroyed flora persistence deltas or leaving matrices at zero indefinitely. Persistence would turn a visual lie into world truth.
Scalability potential: Low/Middle/High/Ultra share the same 300s data route; only VFX intensity scales.
Hardware Impact: Queue scan is bounded to active records; estimate 1 us/event amortized on low-end silicon.

Problem: Need postmortem proof for NaN or bad AUP damage.
Solution: Fixed 300-entry telemetry ring records counts, quality, hash, flags, and last UID; NaN rejection dumps `Docs/AgentLogs/Dump_SHINOBU_268.bin`.
Rejected Alternatives: Chat-only reports or `Debug.Log` spam. Neither is deterministic proof.
Scalability potential: Telemetry cost is constant across hardware; visual overkill does not change record layout.
Hardware Impact: Estimate 0.5 us/frame ring write; dump allocation only on anomaly.

Problem: Compile verification is mandatory but current machine load blocks it.
Solution: Checked `dotnet`/`csc` processes and CPU. No compiler process was active, but CPU LoadPercentage was 100, so build is deferred under the explicit no-build-over-50%-CPU rule.
Rejected Alternatives: Launching `dotnet build` anyway and competing with other agents.
Scalability potential: N/A.
Hardware Impact: Prevents compile contention from stealing runtime/editor cycles from parallel agents.

## Phase 3 Tooling And Audit

Problem: Runtime visibility needed without adding log spam to the hot route.
Solution: Added `FloraDearLieXRayWindow` with UI Toolkit labels backed by pure runtime counters, plus selected-object gizmos for query radius and cell size.
Rejected Alternatives: Per-event `Debug.Log`, runtime debug meshes, or OnGUI allocations.
Scalability potential: Editor-only; Low/Middle/High/Ultra runtime behavior unchanged.
Hardware Impact: 0 us when window is closed; editor polling only.

Problem: VFX profile ingestion must not parse strings in the destruction loop.
Solution: Added strict editor CSV importer that normalizes rows into `Docs/Reports/FLORA_VFX_PROFILE_IMPORT.json`; runtime route continues using packed DTOs and `GlobalQualityWeight`.
Rejected Alternatives: Runtime CSV parsing or managed dictionaries in the hit path.
Scalability potential: Future profiles can tune continuous quantity/color weights without DTO layout change.
Hardware Impact: 0 us hot path.

Problem: Architecture scan found legacy physical sargassum/pickup paths, and the shared physics report was already occupied by sibling agent evidence.
Solution: Generated `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_268.json`, added a `shinobu268FloraDearLieScanner` section to the shared report, and isolated the new flora damage route from Rigidbody prefab/script paths.
Rejected Alternatives: Editing `SargassumCollapseChunk` blindly or overwriting `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`. The chunk is a separate collapse/salvage owner; the shared report contains sibling evidence that must be preserved.
Scalability potential: Dear Lie route covers CombatDamageSignal ambient flora; canopy collapse and pickup physics remain flagged owner-boundary dependencies for their owners.
Hardware Impact: New route avoids those physical paths for normal ambient plant damage; flagged legacy paths still need owner review.

<SELF_AUDIT id="SHINOBU_268">
DTO: FloraDestructionEventDTO size=32 offsets ImpactAUP=0 FloraTypeHash=24 _pad0=28, editor guard added.
Hot path: SignalBus snapshot copy to Vault-backed NativeArray, Burst flat bucket-head/next query, atomic claim, matrix scale-zero, staged result row, owner-fenced DebrisSpawnSignal publish.
Netcode: no StateRingBuffer or Merkle route touched.
GC: no managed allocation in per-event query/scale/VFX job; anomaly dump and editor tools allocate outside hot path.
Known risk: compile not executed because CPU stayed at 100 percent, per no-build rule.
</SELF_AUDIT>

## Polish Loop 2 - Job Fence, SignalBus, False Sharing

Problem: The first Dear Lie pass staged `DebrisSpawnSignal` through `SignalBus.OpenParallelWriter()` inside `ResolveDearLieDamageJob`. That writer maps to the legacy MPSC queue path and becomes unsafe when the job is completed from a later dispatcher window.
Solution: Burst now writes only `FloraDearLieDestructionResult` rows. The owner drains those rows after `DispatcherJobSwap` completion in `LateFrameTick` and calls `SignalBus<DebrisSpawnSignal>.TryPush` from the owner phase. This keeps the hot broadcast route typed while avoiding deferred writer lifetime races.
Rejected Alternatives: Keeping a NativeQueue ParallelWriter inside the job; forcing same-frame completion in `Tick`; instantiating particle prefabs. Standard Unity VFX prefab lifecycles and legacy MPSC writer storms are slower and harder to fence.
Scalability potential: Low quality suppresses staged VFX by probability and still scales matrices to zero. Middle uses sparse debris. High and Ultra increase quantities continuously through the same result row without changing DTO layout or truth ownership.
Hardware Impact: i3/MX350 gains: avoids main-thread Instantiate and legacy queue contention under damage storms; expected save remains 25-60 us per dense event burst, pending profiler proof.

Problem: `_dearLieCounters` and claim slots were adjacent `int` lanes. Parallel atomic increments/claims could force multiple cores to invalidate the same 64-byte cache line.
Solution: Added explicit-layout `FloraDearLieCounter64` and `FloraDearLieClaim64`, each 64 bytes. `FloraDearLieDestructionResult` is now 128 bytes so adjacent result writers never share a cache line.
Rejected Alternatives: Per-event managed locks; adjacent `int` atomics; relying on low contention. Those paths fail exactly during explosions in dense kelp.
Scalability potential: Low devices get stable CPU behavior under burst hits; Ultra devices can run higher VFX counts without CPU contention becoming the limiting factor.
Hardware Impact: Extra scratch memory buys deterministic cache behavior. MX350/i3 class expected gain is reduced atomics stalls during simultaneous 100-event mock storms.

Problem: `ProcessDearLieDestructionSignals` finalized jobs from `Tick`, outside the dispatcher-owned swap window.
Solution: `Tick` now schedules only when no Dear Lie job is pending. `LateFrameTick` opens the dispatcher swap window, tries non-forced completion, drains results, and publishes VFX. Teardown remains forced.
Rejected Alternatives: Hidden `.Complete()` in the simulation phase; `.Run()` for convenience; blocking until results are available. Those violate phase discipline.
Scalability potential: The job may spill across frames without blocking; visual feedback degrades by latency rather than frame-time spike.
Hardware Impact: Low-end hardware avoids forced synchronization. High-end hardware completes in the same frame when naturally ready.

Problem: The XML demanded human tuning and cold CSV parsing, but the first editor importer used managed line splitting and the X-Ray window only displayed labels.
Solution: X-Ray now exposes bounded sliders for damage radius, regeneration delay, and quality override, plus mock injection. CSV importer tokenizes `ReadOnlySpan<byte>` cells, parses floats through a local ASCII parser, and hashes flora names through lowercase FNV-1a.
Rejected Alternatives: Runtime string parsing, `ReadAllLines`, `string.Split`, or recompiling constants for tuning.
Scalability potential: Designers can sweep Low/Middle/High/Ultra quality behavior without code changes; runtime jobs receive scalar copies only.
Hardware Impact: 0 us hot path. Editor/cold boot pays bounded file-read/report cost only.

Problem: Ultra mandate asked for Vault eviction, but Global Authority docs classify DataVault as cross-domain native ownership, not a place to hide unsupported native maps.
Solution: Rejected fake map migration first, then converted only the Dear Lie flat lanes to real `VaultGenerationHandle<T>` buffers and replaced the spatial map with flat bucket-head/next arrays. No core enum or sibling asmdef surface was added.
Rejected Alternatives: Adding fake Vault IDs for unsupported native maps; touching Core memory enums; using `TryGetLatestCreated()` as a fallback. Those would expand global surface and increase compile-wall risk.
Scalability potential: Low devices keep a bounded flat hash and sparse VFX. Middle/high/ultra use the same Vault-backed rows while debris quantity scales continuously.
Hardware Impact: Removes private Dear Lie persistent arrays/maps and keeps compaction-safe job locks around scheduled pointer use. The flat hash avoids map container overhead but keeps the same O(events * 27 * bucket-chain) query shape.

## Polish Loop 3 - Unity Job Safety Aggregation Fence

Problem: Surface and underwater Dear Lie resolve jobs are intentionally scheduled in parallel and aggregate into shared result/counter buffers. The data model was atomic-safe, but Unity Job Safety can reject two simultaneous writable `NativeArray` handles even when the writes are disjoint by atomic index.
Solution: Added `NativeDisableContainerSafetyRestriction` only to `Results` and `Counters` in `ResolveDearLieDamageJob`. `Results` rows are allocated by the 64-byte padded atomic counter and are 128-byte stride; readers remain fenced by `DispatcherJobSwap`.
Rejected Alternatives: Serializing underwater behind surface; duplicating result buffers per lane and merging on main thread; leaving safety to `NativeDisableParallelForRestriction`. Serialization wastes parallelism, duplicate buffers add owner-local memory churn, and parallel-for restriction alone does not address cross-job write-write scheduling.
Scalability potential: Low devices keep non-blocking two-lane work without frame spikes. Middle/High/Ultra can absorb mixed surface/underwater damage bursts while VFX quantity still scales continuously through GlobalQualityWeight.
Hardware Impact: Preserves parallel candidate search under dense flora bursts while keeping atomic contention isolated to 64-byte counters. Expected gain versus serialized lane fallback is workload-dependent, estimated 15-40 us on i3/MX350 for mixed-lane 100-event mock storms.

## Polish Loop 4 - Human Facade And Gizmo Proof

Problem: The X-Ray window only displayed current counters, and the gizmo only showed the mock center. That did not sufficiently prove the 300-frame telemetry route or the live impact-to-target math from SignalBus input.
Solution: Added an editor-only `EditorCopyDearLieTelemetry(Span<int>...)` accessor that copies fixed telemetry lanes without allocation. The X-Ray window now draws destroyed/VFX/regen series through UI Toolkit `Painter2D`. The selected-object gizmo samples current `CombatDamageSignal` snapshot AUP and draws the last resolved impact-to-target line recorded by the owner.
Rejected Alternatives: `Debug.Log` streams, runtime debug meshes, allocating managed telemetry rows for the editor, or exposing the private telemetry DTO type. Those would either allocate in editor refresh or leak private layout into a broader API.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Designers can sweep quality override and watch graph response without recompiling C#.
Hardware Impact: 0 us player hot path; editor-only polling copies at most 300 integer rows and repaints one vector graph.

## Polish Loop 5 - Telemetry Timing Breach

Problem: Task 14 required query timing and dump behavior. The first telemetry ring stored counts and hashes but had no timing lane.
Solution: Added `QueryMicroseconds` at telemetry offset 56 while preserving 64-byte record size. Completion records owner-fenced elapsed microseconds for same-frame jobs and dumps the 300-frame ring when a same-frame query crosses 500 us. Frame-spill latency is not treated as Burst query cost.
Rejected Alternatives: Adding a managed Stopwatch per event, forcing a job `.Complete()` to measure more precisely, or storing a variable telemetry object. Those either allocate, block the frame, or break blittable ring layout.
Scalability potential: Low devices get postmortem proof when the hash query is too dense; High/Ultra keep the same record format while spending visual budget elsewhere.
Hardware Impact: One double timestamp at schedule and one finite check at completion; expected under 0.1 us/frame outside active bursts.

## Polish Loop 6 - GUID Stability And Raw Dump Scratch

Problem: Four new editor scripts were untracked without `.meta` companions, and the anomaly dump still used a managed `byte[]` scratch buffer despite the blackbox mandate asking for raw span dumping.
Solution: Added stable `.cs.meta` files for the new editor facades/scanners and changed `DumpDearLieTelemetry` to copy each 64-byte telemetry row into a `stackalloc Span<byte>` before `FileStream.Write(ReadOnlySpan<byte>)`.
Rejected Alternatives: Letting Unity generate GUIDs on import; keeping the managed scratch array because dumps are rare. Both make verification weaker and leave avoidable churn in the crash path.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged; crash proof remains fixed-size and quality-independent.
Hardware Impact: Removes one 64-byte managed allocation per anomaly dump and prevents Unity asset GUID churn across parallel agents.

## Polish Loop 7 - Shared Report Preservation

Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` already contained SHINOBU_264/261/263 payloads. The first flora scanner version would overwrite that shared artifact if executed from the Unity menu.
Solution: Changed `FloraDearLiePhysicsScanner` to write `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_268.json` and added a nested SHINOBU_268 summary to the shared JSON. Both JSON files validate through `ConvertFrom-Json`.
Rejected Alternatives: Overwriting the shared report; hiding the flora scan only in chat. Overwrite would destroy sibling evidence, while chat-only proof violates the reporting protocol.
Scalability potential: Report ownership does not change runtime behavior. The scan documents that plant destruction continues through matrix scale-zero and GPU debris signals across all quality weights.
Hardware Impact: 0 us runtime. Editor scanner output is cold tooling only.

## Polish Loop 8 - Layout Guard Uses Unity Unsafe View

Problem: The layout guard checked private nested struct sizes through `Marshal.SizeOf(type)`. That is useful, but the actual Unity/Burst native payload view is `UnsafeUtility.SizeOf<T>()` and `UnsafeUtility.GetFieldOffset`.
Solution: Reworked `FloraDearLieDestructionLayoutGuard` so public DTO and private nested result/counter/claim/telemetry types are validated through Unity unsafe size/offset APIs. DTO alignment is now exact `AlignOf == 8`.
Rejected Alternatives: Keeping marshal-only validation or weakening alignment to `>=8`. The task demands exact ARM64 layout proof, and Burst uses the unsafe layout view.
Scalability potential: No runtime behavior change. The guard prevents future payload drift from silently breaking all quality tiers.
Hardware Impact: 0 us runtime; editor import/play-mode failure-prevention only.

## Polish Loop 9 - XML Reconciliation And Static Compile Risk

Problem: The XML assignment names `VfxSpawnSignal`, `NativeQueue`, and Vault-backed transient buffers, while the current project surface already exposes `DebrisSpawnSignal` on the typed `SignalBus` and the architecture docs forbid treating GlobalDataVault as an unsupported map heap.
Solution: Keep the existing first-party GPU debris signal lane: Burst writes 128-byte `FloraDearLieDestructionResult` intent rows, then the owner phase publishes `DebrisSpawnSignal.TryPush` after the dispatcher fence. Later Loop 18 moved the flat Dear Lie lanes to real Vault buffers and replaced the spatial map with flat bucket arrays.
Rejected Alternatives: Inventing a new `VfxSpawnSignal`, retaining a deferred `NativeQueue.ParallelWriter`, or fabricating Vault BufferIDs. Those would add compile-wall surface, lifetime risk, or false authority claims.
Scalability potential: Low quality keeps the matrix vanish and probabilistically suppresses debris; middle/high/ultra increase GPU debris quantity through the same continuous scalar without DTO or authority-route changes.
Hardware Impact: Preserves the zero-physics route and avoids extra global indirection on i3/MX350-class hardware. Subagent static compile-risk review found no blocking issues in touched C# files; build remains gated by CPU=100.

## Polish Loop 10 - Direct Native Mutation

Problem: The resolve job still used `Matrices[bestIndex]`, `Health[bestIndex]`, and `Metadata[bestIndex]` copy/write mutation after the claim. That works functionally, but the XML explicitly requires direct pointer mutation with `UnsafeUtility.AsRef` to avoid defensive struct copies in the hot loop.
Solution: `ResolveDearLieDamageJob` now obtains raw pointers from the three native lanes and mutates `Matrix4x4`, `half` health, and `HectonVegetationInstanceData` by `ref` through `UnsafeUtility.AsRef<T>`. The original matrix is copied once only for the regeneration proof row before the scale basis columns are zeroed in place.
Rejected Alternatives: Leaving the copy/write pattern because Unity usually optimizes it; adding properties or wrapper methods around the DTOs; moving the mutation to the main thread. Those options weaken Burst's direct memory model or reintroduce managed/main-thread cost.
Scalability potential: The mutation path is quality-independent. Low devices get the minimum write cost; middle/high/ultra spend saved CPU on continuously scaled GPU debris quantity, not additional CPU simulation.
Hardware Impact: Reduces hidden struct-copy risk on ARM64 and keeps the visible destruction to one matrix basis write plus scalar metadata updates. Honest estimate remains 2 us/event protected rather than a new measured profiler claim; build remains gated by CPU=100.

## Polish Loop 11 - Regen Matrix Cache Eviction And Vault Boundary

Problem: Dear Lie regeneration used a separate `NativeHashMap<uint, Matrix4x4>` only to remember original matrices for plants already queued in `_dearLieRegenRecords`. That duplicated state and could not be moved to `GlobalDataVault` as a map because the current Vault API supports flat `NativeArray<T>` lanes only.
Solution: Embedded `OriginalMatrix` directly into `FloraDearLieRegenRecord`, expanded the record to an explicit 96-byte layout, removed the original-matrix `NativeHashMap` allocation/dispose/sentinel registration, and restored matrices from the regen row after a finite-matrix guard. The layout guard now validates `FloraDearLieRegenRecord` as `OriginalMatrix@0` and `Underwater@88`.
Rejected Alternatives: Inventing local or reused Vault IDs, touching Core memory enums without a route card, retaining duplicate matrix cache state, or forcing permanent scale-zero visuals. Fake IDs would alias ownership and create compile-wall risk; the separate map was avoidable memory and lookup churn.
Scalability potential: Low devices keep one contiguous restore queue scan with no extra hash-map lookup/removal. Middle/high/ultra behavior is unchanged visually; saved owner-phase overhead remains available for continuously scaled GPU debris.
Hardware Impact: Removes one persistent `NativeHashMap` and one hash lookup/removal pair per regrowth. Expected low-end impact is small but deterministic: less memory fragmentation and fewer random cache probes in the 300s recovery path. Build remains gated by CPU rule.

## Polish Loop 18 - Vault Flat Spatial Hash

Problem: Dear Lie still owned private persistent native lanes for damage events, results, counters, claims, regeneration, telemetry, and two spatial maps. The subagent audit confirmed Vault cannot own native maps, but can own flat `NativeArray<T>` lanes through local numeric BufferIDs.
Solution: Added local high BufferIDs `72980..72990` under `SystemID.FloraGenomics`, cached `IDataVault` only in cold bootstrap/hot-swap, requested pointer-free `VaultGenerationHandle<T>` descriptors, and resolved phase-local `NativeArray<T>` views. Replaced the spatial map with four Vault arrays: surface/underwater bucket heads and per-instance next links. Jobs clear bucket heads to `-1`, build linked lists with atomic `Interlocked.Exchange`, and query 27 neighboring buckets without physics or managed state.
Rejected Alternatives: Keeping private persistent Dear Lie arrays; inventing a Vault map abstraction; adding `H8Memory.BufferID` enum members; reusing unrelated BufferIDs; calling `GlobalDataVault.TryGetLatestCreated()` from runtime. Those options either violate ownership or increase compile-wall risk.
Scalability potential: Low hardware gets the same bounded hash and sparse/silent VFX. Middle/high/ultra keep identical truth ownership while GPU debris quantity scales through `GlobalQualityWeight`. DTO layout and authority route do not change with quality.
Hardware Impact: Persistent Dear Lie storage now sits in the central Vault with generation handles and job locks. The query remains bounded by 27 buckets per event; replacing the map with flat arrays removes native map container ownership and keeps memory linear for ARM64 cache behavior. Build remains gated by CPU rule.

## Polish Loop 19 - Generation Resolver BufferID Correction

Problem: The first post-Vault audit misread the flat generation metadata cap as `MaxBufferCapacity=32768`. Source re-check showed `GlobalDataVault.Initialize` allocates `_metadataByBufferId` with `MaxGenerationHandleCapacity=100000`, and sibling domains already use high local BufferID ranges in the 70k band.
Solution: Restored SHINOBU_268 Dear Lie Vault lanes to high local IDs `72980..72990`, below the 100000 strict generation metadata cap and absent from BufferID collision scans. The route still avoids editing `H8Memory.cs`, so compile-wall isolation is preserved.
Rejected Alternatives: Keeping the temporary `644..654` detour; adding global enum entries in Core; changing `GlobalDataVault.TryResolveHandle`. Low IDs are closer to the crowded core enum range and create avoidable future collision risk.
Scalability potential: No visual behavior change. Low/Middle/High/Ultra still use identical ownership and scale only GPU debris probability/quantity through `GlobalQualityWeight`.
Hardware Impact: Prevents a local ID governance mistake without changing runtime cost. This is correctness and route-proof repair, not a measured performance gain.

Problem: Subagent 019e4bd5 completed after the ID correction and reported stale `644..654` observations, but its API cross-check still verified the requested Vault methods and `SystemID.FloraGenomics` are present.
Solution: Treated the API findings as valid and the ID text as superseded by the current `72980..72990` source state. Re-ran current collision/static checks locally after the correction.
Rejected Alternatives: Accepting the stale subagent ID note as current truth, or editing Core enum entries to silence a hypothetical analyzer. Both would produce false reporting or widen compile-wall surface.
Scalability potential: No route change; quality remains continuous and visual-only.
Hardware Impact: 0 us runtime. This is audit hygiene and dependency-surface confirmation.

## Polish Loop 12 - Finite Guard Compile Profile

Problem: Static compile-risk review flagged `float.IsFinite` and `double.IsFinite` as potentially profile-sensitive under Unity's configured .NET surface.
Solution: Runtime finite checks now use `math.isfinite` for `float`/`double` values. The cold editor CSV parser uses `!float.IsNaN(value) && !float.IsInfinity(value)`, which is supported on older profiles.
Rejected Alternatives: Leaving profile-sensitive APIs until a compiler failure, or replacing checks with unchecked casts. Both weaken fail-fast behavior; unchecked values can poison quality fallback and parsed VFX profile scalars.
Scalability potential: No behavior change across Low/Middle/High/Ultra. The quality fallback still smoothly lerps from scalability tier when `HomeostasisBrain.GlobalQualityWeight` is not finite.
Hardware Impact: Runtime cost is unchanged; this is compile-wall risk reduction. Build remains gated by CPU=100.

## Polish Loop 13 - Dump Writer Profile Hardening

Problem: `DumpDearLieTelemetry` used `FileStream.Write(ReadOnlySpan<byte>)`, which is clean but depends on newer profile overloads and was not compiler-proven on this machine.
Solution: Kept the zero-GC crash dump path but changed it to `byte* scratchPtr = stackalloc byte[stride]`, `UnsafeUtility.MemCpy`, and a `WriteByte` loop for each telemetry byte. This removes the Span write overload while preserving fixed-size raw telemetry output.
Rejected Alternatives: `byte[]` scratch allocation, `BinaryWriter`, or keeping the profile-sensitive overload. A managed scratch array is small but violates the black-box zero-GC posture; `BinaryWriter` adds object layering; the overload risk is avoidable.
Scalability potential: Runtime normal path is unchanged. Crash dump is rare and fixed-size, independent of quality tier.
Hardware Impact: Dump path is slower by roughly 19KB of byte writes per anomaly, but only after a fault; the normal frame path remains 0 us changed. Build remains gated by CPU rule.

## Polish Loop 14 - Verification Gate Hygiene

Problem: The first automated XML task counter searched for nonexistent `<task>` tags and returned zero, even though the extracted SHINOBU_268 block uses `Task NN:` lines. Compiler execution also remains forbidden by the CPU gate.
Solution: Re-counted the exact SHINOBU_268 XML block with `(?m)^Task\s+\d{2}:`, confirming 20 tasks. Re-ran forbidden-pattern grep over the touched Dear Lie runtime/editor files, parsed both JSON reports, ran `git diff --check`, and rechecked CPU/compiler processes.
Rejected Alternatives: Trusting the bad `<task>` parser output, launching a build under CPU=100, or calling Task 20 verified without compiler proof. Those would produce a fake report.
Scalability potential: No route change. This protects the audit trail for the same continuous GlobalQualityWeight matrix-vanish/GPU-debris path across Low/Middle/High/Ultra.
Hardware Impact: 0 us runtime. Build remains blocked by CPU=100 with compiler process count 0; static checks only reduce integration risk.

## Polish Loop 15 - Hot Registry Fallback Removal

Problem: `ResolveDearLieGlobalQualityWeight()` used `GlobalRegistry.ScalabilityTierProfileByte` when `HomeostasisBrain.GlobalQualityWeight` was non-finite. That fallback can run from Tick/LateFrame telemetry and scheduling paths, so even a rare branch still violated the cold-registry-only doctrine.
Solution: Added `_dearLieFallbackQualityWeight`, initialized to 0.25 and refreshed only from `CacheRegistryServicesCold()`. The hot quality resolver now uses `HomeostasisBrain.GlobalQualityWeight` when finite or the cached fallback scalar when not finite.
Rejected Alternatives: Keeping the registry read because it only runs on non-finite quality, or calling another registry accessor from the job scheduling path. Both preserve a hidden hot dependency on cold identity state.
Scalability potential: Low/Middle/High/Ultra behavior remains continuous. The fallback scalar still maps the cold scalability tier through `SmoothStep01` and `math.lerp(0.25, 1.0)`, but the frame route reads only a local float.
Hardware Impact: No measured runtime claim. It removes a cold service lookup hazard from the frame route and prevents registry contention from becoming part of damage/VFX scheduling.

## Polish Loop 16 - Result Overflow Before Matrix Mutation

Problem: Surface and underwater Dear Lie lanes are scheduled independently against the same bounded damage snapshot. In a boundary burst, two lane jobs can reserve more destruction results than the original 128-event cap. The previous job scaled the matrix to zero before checking result capacity, so overflow could create a visual disappearance without a result row, regen record, telemetry proof, or VFX route.
Solution: `DearLieMaxResultsPerFrame` is now `DearLieMaxDamageSignalsPerFrame * 2`, matching the two scheduled lanes. `ResolveDearLieDamageJob` now reserves the result index immediately after a successful claim and returns before matrix/health/metadata mutation if the result buffer is exhausted. Overflow increments counter slot 6; completion folds that count into rejected telemetry, sets flag 16, and dumps the 300-frame blackbox.
Rejected Alternatives: Assuming one lane can never overlap another, mutating first and dropping only the VFX, or serializing surface and underwater lanes to avoid buffer pressure. Those either hide an untracked state hole or throw away parallelism.
Scalability potential: Low devices get a cheap bounded fail-fast path under damage storms instead of unbounded repair work. Middle/High/Ultra retain dual-lane throughput and can emit continuously scaled GPU debris without changing authority or DTO layout.
Hardware Impact: Adds one atomic reservation before mutation and doubles the result scratch lane from 16KB to 32KB. This is a deliberate memory-for-correctness trade: it prevents orphaned zero-scale matrices and keeps recovery deterministic under worst-case bursts. Build remains gated by CPU=100.

## Polish Loop 17 - Prompt Re-Extract And Shared Report Preservation

Problem: The anti-amnesia extractor initially searched for an exact `<AGENT_PROMPT id="SHINOBU_268">` opener, but CURRENT_BATCH uses an attributed opener with role and chat_name. The shared physics report was also overwritten externally with a different agent payload after SHINOBU_268 had previously added its nested section.
Solution: Re-extracted the block with `<AGENT_PROMPT id="SHINOBU_268"[^>]*>` and confirmed 20 `Task NN:` lines. Re-added only the nested `shinobu268FloraDearLieScanner` proof section to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`, preserving the current SHINOBU_274 content, and updated the dedicated SHINOBU_268 report with the 256-result overflow guard.
Rejected Alternatives: Trusting the failed exact-tag extractor, overwriting the shared report back to a SHINOBU_268-only payload, or leaving the dedicated report stale after the overflow fix. All three weaken objective disk proof.
Scalability potential: No runtime behavior change. The report now documents the same Low/Middle/High/Ultra continuous GPU-debris route and the overflow blackbox proof.
Hardware Impact: 0 us runtime. This is audit integrity only.

## Polish Loop 20 - Counted Vault Lock Rollback

Problem: `TryLockDearLieVaultJobBuffers` acquired 11 DataVault buffers with a short-circuit `||` chain, then called the full unlock routine after any failure. Because `IDataVault.TryUnlockBuffer` decrements a lock count rather than verifying this attempt acquired that exact buffer, a partial failure could release a later buffer owned by another route.
Solution: Replaced the direct chain with fixed-order BufferID resolution, a local `lockedCount`, and `_dearLieVaultJobLockCount`. Partial failure releases only the acquired prefix in reverse order. Normal dispatcher completion and teardown release exactly the held count and clear the count. Impossible resolver indices return `default` and are ignored rather than double-unlocking the last lane.
Rejected Alternatives: Leaving the full unlock because the route is owner-local; adding a global lock owner check inside Core DataVault; inventing a new lock API. The first can corrupt sibling lock counts, while the latter two widen core/global surface during a domain polish pass.
Scalability potential: No quality behavior change. Low/Middle/High/Ultra still share the same Vault buffers and continuous debris quality curve; the patch protects relocation/defrag safety while scheduled jobs hold native pointers.
Hardware Impact: Adds one bounded 11-iteration lock/unlock loop only when scheduling a Dear Lie batch, not inside the per-event Burst query. Expected hot-frame cost is below measurement noise; correctness gain is preventing accidental cross-owner unlock under contention.

Problem: The X-Ray editor facade still used `Object.FindFirstObjectByType` as a fallback inside its refresh path.
Solution: Removed the scene-search fallback and resolved only `DestructibleOrganicManager.ActiveRuntimeInstance`, the owner-published runtime reference.
Rejected Alternatives: Keeping an editor-only scene search for convenience or adding a polling cache. Both hide authority discovery inside a diagnostic accessor.
Scalability potential: Runtime route unchanged. Editor diagnostics now reflect the real owner publication path.
Hardware Impact: 0 us player runtime. Editor refresh avoids a scene-search fallback.
