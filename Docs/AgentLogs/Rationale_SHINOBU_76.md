Date: 2026-05-18
Agent: SHINOBU_76
Status: PENDING VERIFICATION / PRIOR BUILD GREEN / POST-DELTA STATIC ONLY

## Decision 00 - Scope Lock
Problem: The batch request targets 100 km AUP origin shifting, but the active worktree already contains extensive Core/AUP edits from parallel agents and untracked files.
Solution: Treat existing files as shared state, audit first, then make only Core/AUP and editor-facade corrections required by the SHINOBU_76 prompt.
Rejected Alternatives: Reverting existing untracked Core files would destroy parallel work; editing concrete physics/render/pathfinding systems would violate the domain boundary.
Scalability potential: Low tier gets predictable batch staggering; middle/high/ultra can spend the saved CPU on denser visual sync and richer telemetry without changing authority.
Hardware Impact: Avoiding scene-wide `Transform.position` loops in the SHINOBU-owned path prevents MX350/i3 main-thread stalls; expected gain is correctness and spike avoidance, profiler proof absent.

## Decision 01 - Mandate Set
Problem: Origin shift touches precision, memory layout, jobs, signals, telemetry, and phase ordering.
Solution: Read the eight relevant mandates named in `Status_SHINOBU_76.md` before code edits.
Rejected Alternatives: Applying a generic floating-origin Unity recipe would conflict with AUP/DataVault authority and zero-GC rules.
Scalability potential: Low/middle/high/ultra behavior is derived from continuous `GlobalQualityWeight`, not binary quality switches.
Hardware Impact: Mandate-driven SoA/NativeArray paths avoid heap traffic and keep cache-line access suitable for i3/MX350.

## Decision 02 - Archive Fallback Constants
Problem: The batch demanded archaeology for `aup_sector_grid.h8bin` / threshold binaries before trusting constants, but no active binary payload exists in the current tree.
Solution: Use the existing `GenerateEmergencyMockThresholds()` fallback: 4000m rebase limit, 5000m sector size, 10k batch size, 50k mock entities. This matches archived SHINOBU_30 evidence.
Rejected Alternatives: Loading absent files during startup or inventing a new binary format would add IO risk and cross-agent ABI drift.
Scalability potential: Low tier gets a 4000m shift before visible precision decay; middle/high/ultra can use the same sector hash and spend headroom on denser visual correction.
Hardware Impact: Eliminates live threshold file IO on i3/MX350 shift frames; estimated 3 us shift-frame saved plus no disk jitter.

## Decision 03 - Double Threshold Monitor
Problem: The PRE_SIM threshold monitor still measured camera distance with `float3 LocalPosition`, violating the 100 km AUP precision mandate and allowing jitter around the rebase threshold.
Solution: `MonitorAupThreshold` receives `TotalUniverseOffset`, computes `double3 local = camera.GlobalPosition - TotalUniverseOffset`, and compares double squared distance to double squared threshold. The first implementation used a one-row job; Decision 18 removed that wrapper because scalar math does not need a job graph.
Rejected Alternatives: Keeping `float3` for speed was rejected because one-camera double math is cheaper than physics jitter and rebase oscillation.
Scalability potential: Low tier pays one double3 subtraction; middle/high/ultra get deterministic threshold behavior without needing a different algorithm.
Hardware Impact: Expected overhead under 1 us on i3/MX350, traded for reduced stutter and fewer false rebase frames.

## Decision 04 - GlobalQualityWeight Slicing
Problem: Low-tier staggering was gated by `SystemHealthIndex01 > 0.85f`, which is inverted relative to the prompt and not the mandated continuous quality input.
Solution: Scheduler authority moved to `math.saturate(HomeostasisBrain.GlobalQualityWeight)`. The first pass used a `<0.3` time-slice threshold; Decision 16 supersedes that with a continuous polynomial batch curve.
Rejected Alternatives: Binary low/ultra switches or health-index stress gates; both violate the Scalability Pillar and create unstable behavior under thermal changes.
Scalability potential: Low tier spreads AUP cache work across smaller slices; middle smoothly expands slices; high/ultra execute near-full/full contiguous Burst rebase and spend saved cycles on visual overkill.
Hardware Impact: Spike reduction is expected on weak silicon, but exact timing is PENDING PROFILER.

## Decision 05 - Root Presentation Rebase Without Transform.position Writes
Problem: The legacy presentation corridor wrote root transforms through `Transform.position`, which directly conflicts with the user's "No Transform.position" constraint and can trigger world-space hierarchy recalculation during shifts.
Solution: Root shift job and loaded-scene committed-offset application now use `localPosition`. These targets are scene roots, so local space equals world space without the forbidden property.
Rejected Alternatives: Removing legacy root presentation shifting entirely would break current scene presentation and existing tests; rewriting every tracker/physics consumer is outside the SHINOBU domain.
Scalability potential: Low tier avoids the worst root world-position write path; middle/high/ultra preserve presentation compatibility while AUP authority stays in double DataVault state.
Hardware Impact: Expected to reduce main-thread transform hierarchy churn on i3/MX350; exact microseconds require Unity profiler, unavailable due compile wall.

## Decision 06 - Velocity Non-Interference
Problem: Rebase can look like a physical impulse if velocity buffers are shifted or recomputed along with positions.
Solution: AUP rebase jobs shift only local position caches and historical visual/tether points; velocities are allocated but not passed to rebase jobs.
Rejected Alternatives: Velocity compensation or rigidbody impulse correction; those hide coordinate mistakes and create deterministic drift.
Scalability potential: Low/middle/high/ultra all keep predictable physics because the origin shift is a coordinate epoch change, not acceleration.
Hardware Impact: Avoids unnecessary vector writes across 50k velocities; estimated 20-40 us avoided per full rebase batch.

## Decision 07 - Compile Wall Handling
Problem: Project compilation fails in the Economy domain after SHINOBU patches, currently `TradeMarauderRuntime.cs` Vector3-to-double3 casts; an earlier attempt failed on missing `TradeMarauderDirector`.
Solution: Mark compile verification as `[BLOCKED BY DEPENDENCY]`, keep SHINOBU static proof, and avoid editing Economy files without integrator authorization.
Rejected Alternatives: Cross-domain compile fix from Origin Shift would violate the domain boundary and risk destroying another agent's work.
Scalability potential: None in runtime; this is integration hygiene.
Hardware Impact: 0 us runtime. Prevents sabotaging unrelated systems while preserving AUP changes for later integration.

## Decision 08 - Burst Directive Hardening
Problem: SHINOBU jobs used Burst fast/standard flags but did not request synchronous compilation, leaving room for slow safe-path behavior during critical origin-shift kernels.
Solution: Added `CompileSynchronously = true` to every remaining SHINOBU/HFO job in the origin-shift corridor: root presentation shift, drift check, mock init, AUP state rebase, hot entity rebase, and historical point rebase. The former one-row mock camera and threshold jobs were removed by Decision 18.
Rejected Alternatives: Trusting Burst defaults was rejected because the mandate requires explicit compiler behavior.
Scalability potential: Low tier gets predictable compiled kernels before thermal stress; middle/high/ultra keep vectorized paths without runtime compilation ambiguity.
Hardware Impact: Prevents Burst fallback/safe-eval risk; exact microseconds require Unity profiler, but avoiding a 40% kernel regression is mandatory.

## Decision 09 - False Sharing Counter Isolation
Problem: The rebase non-finite counter was a `NativeArray<int>` target for atomic increments. Even with one active counter, it did not prove cache-line isolation under parallel worker contention.
Solution: Added `AupPaddedAtomicCounter` with explicit 64B layout and moved the counter Vault handle to `VaultBufferHandle<AupPaddedAtomicCounter>`.
Rejected Alternatives: Keeping a naked `int` was rejected because it fails the false-sharing audit and gives no structural proof to future maintainers.
Scalability potential: Low tier avoids cache invalidation spikes in fault paths; high/ultra can process larger batches without atomic cache-line ambiguity.
Hardware Impact: Estimated 5-15 us saved only under contended NaN/non-finite fault cases on i3/MX350; normal path cost remains zero because atomics are not hit.

## Decision 10 - Blackbox Dump Without Managed Scratch
Problem: The blackbox dump path kept a private static `byte[]` scratch buffer, violating the "no private arrays" proof even though it was fault-path only.
Solution: Removed the managed scratch array and write the telemetry ring from native memory to `FileStream` with `ReadOnlySpan<byte>` slices.
Rejected Alternatives: Keeping a cold managed array was rejected because the forensic path must be austere and DataVault-owned where possible.
Scalability potential: Low/middle/high/ultra all keep blackbox evidence without managed heap residue.
Hardware Impact: Removes 4096B managed heap ownership from the AUP coordinator; hot path remains 0 B/frame.

## Decision 11 - Transform.position Literal Eradication
Problem: The prior patch removed root rebase writes but still left direct `Transform.position` reads in anchor/tracker code, which failed the literal "No Transform.position" audit.
Solution: Replaced anchor/tracker reads with `Transform.GetPositionAndRotation(out position, out _)`; root rebase remains `localPosition`.
Rejected Alternatives: Leaving reads because they are not writes was rejected; the mandate explicitly called out the property and the origin corridor should not depend on it.
Scalability potential: Low tier removes a fragile Unity property pattern from the shift corridor; high/ultra keep the same AUP math without direct transform-position coupling.
Hardware Impact: Expected to reduce transform-property churn risk; measured impact pending Unity profiler.

## Decision 12 - Final Compile State
Problem: Earlier builds failed first in Economy and then in a local HFO definite-assignment bug created during polish.
Solution: Fixed the local `CS0165` by initializing `anchorRuntimePosition`, reran `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal`, and confirmed success.
Rejected Alternatives: Reporting the stale external compile-wall state after the tree became buildable would be false.
Scalability potential: None directly; buildability protects iteration speed.
Hardware Impact: 0 us runtime. Compile result: 0 errors, 9 warnings unrelated to SHINOBU behavior.

## Decision 13 - Deterministic Mock Camera Tick
Problem: The blind fallback camera AUP advanced by the caller's scaled `deltaTime`, creating a rollback-unsafe mock path even though real anchor mode already receives an explicit AUP-derived local input.
Solution: Replace the fallback step with `MockCameraSimulationTickSeconds = 1/60` multiplied by `MockCameraSpeedMetersPerSecond = 125`. `deltaTime` remains in the public signature for the legacy tick contract but is not simulation truth in the SHINOBU mock path.
Rejected Alternatives: Reading `Time.deltaTime`, multiplying by variable tick delta, or adding a new SystemDispatcher dependency just to hydrate a mock-only fallback. The real anchor path remains authoritative through `totalUniverseOffset + anchorLocal`.
Scalability potential: Low/middle/high/ultra all get the same deterministic fallback trigger cadence; quality scaling remains in the rebase batch scheduler through `GlobalQualityWeight`, not in coordinate authority.
Hardware Impact: 0 B/frame and no measurable CPU change. The gain is determinism: frame-time jitter no longer changes mock rebase distance or sector hash cadence.

## Decision 14 - Current Batch Hygiene Drift
Problem: Re-running the required tag-bounded extraction against active `Docs/Tasks/CURRENT_BATCH.md` returned `MISSING_SHINOBU_76`; the file now contains another batch scope.
Solution: Record the hygiene drift, do not import neighboring prompt requirements, and continue from the already-maintained `Status_SHINOBU_76.md`, `Rationale_SHINOBU_76.md`, domain boundary, and current SHINOBU source files.
Rejected Alternatives: Treating current SDF/POI/audio/flora prompts as SHINOBU authority or pretending the active batch file still contains the extracted XML.
Scalability potential: None directly; this protects architectural scope under multi-agent concurrency.
Hardware Impact: 0 us runtime. Prevents accidental cross-domain edits and compile-wall expansion.

## Decision 15 - Rollback Deterministic Burst Mode
Problem: Origin-shift jobs still used `FloatMode.Fast` after the rollback mandate was re-read. Fast math is acceptable for presentation-only approximation, not for AUP authority, sector hash cadence, rebase drift probes, and rollback-sensitive local cache mutation.
Solution: Switched every origin-shift corridor Burst job in `AupOriginShiftCoordinator` plus the HFO rebase/drift jobs to `FloatMode.Deterministic`, preserving `CompileSynchronously = true` and `FloatPrecision.Standard`.
Rejected Alternatives: Keeping `FloatMode.Fast` for speed, or changing unrelated world/render jobs outside the Origin Shift domain. The local Burst package proves `FloatMode.Deterministic` exists, and Core/Determinism already uses it.
Scalability potential: Low/middle/high/ultra use the same deterministic coordinate authority; visual overkill remains decoupled from this math. High-tier visuals can still be richer because presentation systems consume the stable offset facade.
Hardware Impact: No microsecond saving claimed. Expected cost is bounded to origin-shift/drift kernels; benefit is preventing cross-platform float drift between x86 and ARM64.

## Decision 16 - Continuous Quality Batch Curve
Problem: The scheduler still used a hard `qualityWeight < 0.3f` switch, which violates the continuous GlobalQualityWeight law and can produce visible cadence changes under thermal pressure.
Solution: Added `ResolveQualityScaledBatchSize()`: `q*q*(3-2q)` smooths the quality input, `math.lerp` blends low-tier micro-slices toward configured batch/full rebase, and `math.step` gates active work without hardware flags. `timeSliced` is now only a consequence of `batchCount < activeCount`.
Rejected Alternatives: Keeping the binary 0.3 threshold, using hardware class enums, or splitting low/high code paths. All were rejected because AUP rebase load must breathe with the hardware.
Scalability potential: Low tier processes small deterministic slices; middle grows batch size smoothly; high/ultra converge toward full contiguous Burst rebase and spend saved CPU on visual sync rather than additional coordinate truth.
Hardware Impact: Low-tier spike risk is reduced without fake exact timing claims. The target remains under the prior 36-70 us per 10k slice envelope, but actual curve timing is PENDING PROFILER.

## Decision 17 - NoAlias Expansion
Problem: Several NativeArray job fields did not carry `[NoAlias]`, so Burst could conservatively assume buffer overlap and suppress vectorization even though these Vault buffers are independent handles.
Solution: Added `[NoAlias]` to independent NativeArray fields in mock initialization, AUP state rebase, hot entity rebase, historical rebase, and HFO drift probes. The prior one-row camera/runtime-state and threshold jobs were subsequently removed because they did not need the job scheduler.
Rejected Alternatives: Trusting pointer-only NoAlias in `AupStateRebaseJob` or relying on source-level human knowledge. The compiler needs explicit facts.
Scalability potential: Low tier gets cheaper slices; high/ultra get cleaner SIMD opportunities in full batches.
Hardware Impact: SIMD opportunity restored; exact gain requires Burst Inspector/profiler.

## Decision 18 - One-Row Job Removal
Problem: `TickPreSimulation` launched two one-element jobs with `.Run()` for mock camera increment and threshold monitoring. That path produced no useful dependency graph and made the pre-sim audit look more parallel than it was.
Solution: Replaced both one-row jobs with inline scalar functions operating on Vault `NativeArray` row zero, preserving the `double3` local-delta math and finite guards. Burst jobs remain only where there is real batch work: cold mock initialization, AUP state rebase, hot entity cache rebase, historical point rebase, and HFO drift probes.
Rejected Alternatives: Scheduling async one-row jobs would add overhead; turning the time-slice rebases into fire-and-forget async jobs was rejected because there is no dispatcher fence returned to downstream AUP readers in the current public API.
Scalability potential: Low/middle/high/ultra all avoid fake scheduling for scalar pre-sim math. Low-tier time-slice behavior remains synchronous micro-slices until a dispatcher-owned fence contract exists.
Hardware Impact: Job-dispatch overhead is removed from the one-camera monitor path; exact microseconds are PENDING PROFILER. No `dotnet build` was launched after this delta by user instruction.

## Decision 19 - Blackbox Dump Header And Endianness
Problem: The origin-shift blackbox dump wrote raw telemetry ring bytes without a schema header, byte-order marker, entry stride, or wrap-order proof. That makes postmortem readers guess the payload shape.
Solution: Added a 64-byte `AupOriginShiftDumpHeader`, written before the telemetry payload. Header numeric fields are explicitly little-endian through `ToLittleEndian()` and manual `ReverseBytes()` helpers. Payload rows are exported oldest-to-newest from the circular cursor.
Rejected Alternatives: `BinaryWriter`, `new byte[]` serialization scratch, JSON sidecars, and raw array-order dumps. Those either allocate managed memory or force the forensic reader to reverse-engineer ring order.
Scalability potential: Low/middle/high/ultra behavior is unchanged during gameplay; this only improves crash autopsy quality. Ultra/dev builds can consume richer forensic context without adding hot-path work.
Hardware Impact: 64 additional bytes per dump, fault path only. 0 B/frame and no gameplay CPU change. No `dotnet build` was launched after this delta by user instruction.

## Decision 20 - Shift Sequence Is Not Rebase Count
Problem: Time-sliced hot entity continuation used `runtime.RebaseCount` as the `ShiftFrameId`. Rebase count is an event counter, not the generation ID carried by AUP signals. If those diverge, stale-cache probes can accept or reject the wrong frame.
Solution: Expanded `AupOriginShiftRuntimeState` to 112B and added `LastShiftSequence` plus `PendingTimeSliceShiftSequence`. Initial scheduled slices store the incoming `shiftSequence`; continuation slices reuse that exact pending sequence. Frame telemetry now writes `LastShiftSequence`, not `RebaseCount`.
Rejected Alternatives: Continuing to overload `RebaseCount`, adding a managed dictionary keyed by pending shifts, or mutating hot entity consumers outside Origin Shift. The fix belongs in the AUP runtime-state DTO.
Scalability potential: Low-tier time slicing now preserves generation correctness across multiple frames; high/ultra full-batch rebases still write the same generation ID in one fence.
Hardware Impact: `AupOriginShiftRuntimeState` increases by 8 bytes for one Vault row. Runtime cost is negligible; correctness gain is stale-cache prevention. No `dotnet build` was launched after this delta by user instruction.

## Decision 21 - Historical Buffers Must Slice With Entities
Problem: The AUP entity cache used `GlobalQualityWeight` time slicing, but primary historical points and tether float3 buffers were still scheduled as full-length rebase jobs during the initial shift frame. That preserves correctness but can still produce the exact low-tier stutter the SHINOBU prompt targets.
Solution: Expanded `AupOriginShiftRuntimeState` to explicit 120B with `HistoricalTimeSliceStartIndex`. `ScheduleHistoricalRebaseBatch` and `RunHistoricalRebaseBatch` now rebase primary history plus tether position/history buffers through ranged `Float3HistoricalRebaseJob` slices. `ContinueTimeSlicedRebase` clears the time-slice flag only after both entity and historical cursors reach their totals.
Rejected Alternatives: Leaving full-array historical jobs because they are "visual only"; using one entity cursor for buffers with different lengths; adding a cross-domain tether callback. The ranged Vault buffer walk stays inside Origin Shift and does not create a sibling dependency.
Scalability potential: Low tier rebases cables/trails over deterministic micro-slices; middle increases slice size continuously; high/ultra converge toward a single-frame full rebase when `GlobalQualityWeight` is high. The visual fake remains stable because trails/cables are shifted as presentation history, not re-simulated physics.
Hardware Impact: Adds 8B to one singleton runtime-state row and one integer cursor update per continuation frame. It removes the low-tier full-history first-frame spike class; exact microseconds are PENDING PROFILER. No `dotnet build` was launched after this delta by user instruction.

## Decision 22 - AUP Commit Rows Must Be Self-Describing
Problem: The 48B `AUP_StateDTO` was aligned but under-specified for rollback consumers: shift generation, finite state, source ownership, and quantized local millimeters lived outside the row or were missing. During low-quality time slicing, that makes each row depend on external frame context and weakens stale-cache detection.
Solution: Expand `AUP_StateDTO` to explicit 64B and write `ShiftFrameId`, `LocalMillimeters`, `FiniteFlags`, and `SourceSystemId` from both mock initialization and `AupStateRebaseJob`. `QuantizeLocalMillimeters` rounds finite local meters to integer millimeters with clamp guards. The row is now one L1 cache line and can be copied by rollback/save/QA tooling without side-channel metadata.
Rejected Alternatives: Keeping the 48B row to save 16 bytes, adding a managed dictionary keyed by entity index, using `RebaseCount` as generation, or storing millimeters only in blackbox telemetry. Those alternatives either hide correctness state outside the row or create GC/lookup risk.
Scalability potential: Low tier can time-slice and still leave every processed row self-describing for consumers that observe partially completed epochs. Middle/high/ultra keep the same row ABI while larger batches converge in fewer frames. Ultra visual systems can consume stable shift ids and integer local caches without asking Origin Shift for concrete domain callbacks.
Hardware Impact: At 50,000 AUP rows this adds roughly 800KB of Vault memory. The cost is bounded and cache-line-aligned; the gain is deterministic stale-cache detection and rollback-friendly memcopy rows. Exact microseconds are PENDING PROFILER. No `dotnet build` was launched after this delta by user instruction.

## Decision 23 - Do Not Overload MemoryAddressShiftSignal For Coordinate Epochs
Problem: The SHINOBU prompt asked for a `MemoryAddressShiftSignal` carrying `ShiftDelta`, but current project source defines `MemoryAddressShiftSignal` as a 32B DataVault pointer relocation packet with `OldPointer`, `NewPointer`, `BufferId`, `ByteLength`, `Version`, `Flags`, and `SystemId`. It has no coordinate delta. The prior SHINOBU publish emitted this lane with zero pointers and an AUP buffer id, which is semantically false and could cause raw-pointer cache consumers to refresh for a relocation that never happened.
Solution: Remove the SHINOBU-originated `MemoryAddressShiftSignal` publish. Coordinate epoch shifts continue through the existing `AupShiftSignal` typed lane, which is explicit 32B and already carries `ShiftMeters` plus `ShiftFrameId`. DataVault relocation notices remain owned by `SystemDispatcher.PublishMemoryAddressShiftSignals(IDataVault)`, fed by `VaultRelocationRecord`.
Rejected Alternatives: Mutating the public `MemoryAddressShiftSignal` ABI to add `ShiftDelta` would break DataVault relocation consumers and the signal layout guard. Creating a second near-duplicate coordinate signal would add a single-use lane when `AupShiftSignal` already exists. Keeping the false zero-pointer publish was rejected because it lies to the relocation lane.
Scalability potential: Low/middle/high/ultra all keep one coordinate shift broadcast and one separate memory relocation broadcast. Visual overkill consumers can read `AupShiftSignal` without waking raw-pointer repair paths.
Hardware Impact: Removes one unnecessary signal enqueue/snapshot payload per origin shift. No measured microsecond claim. The main gain is correctness and compile-wall protection. No `dotnet build` was launched after this delta by user instruction.

## Decision 24 - Bounded Continuation Runs Are Safer Than Orphaned Async Slices
Problem: Low-quality origin shifts continue over multiple frames. Scheduling those continuation slices asynchronously without returning a dispatcher-owned fence would expose downstream AUP readers to partially written local caches. Conversely, full-array synchronous continuation would recreate the stutter the prompt targets.
Solution: Keep continuation slices synchronous through `.Run()` but make them mathematically bounded by `ResolveQualityScaledBatchSize`. The active low-tier batch floor is now 10,000 rows, satisfying the original 50k/5-frame prompt while still covering the default 1024-row `VaultHotEntityData` hot cache in frame one. No direct `.Complete()` exists in the SHINOBU/HFO corridor.
Rejected Alternatives: Fire-and-forget scheduled continuation jobs were rejected because no public API returns a `JobHandle` to the SystemDispatcher for all readers. Freezing physics until all 50k rows finish was rejected because it converts low-tier time slicing into a long pause. Reducing the first batch below hot-cache capacity was rejected because near/critical rows could remain stale for a frame.
Scalability potential: Low tier gets bounded slices and first-frame hot-row coverage. Middle grows the slice size continuously. High/ultra finish in one or few batches and can spend saved headroom on visual systems consuming `AupShiftSignal`.
Hardware Impact: No measured microseconds. Static source proves the stutter class is bounded by batch math, not removed by profiler evidence. No `dotnet build` was launched after this audit by user instruction.

## Decision 25 - Five-Frame Prompt Cadence Beats Over-Slicing
Problem: The prior continuous curve used a 1024-row low-tier floor. That protected tiny hot caches but violated Task 13's explicit 50,000 entities over roughly five frames / 10,000 rows per frame cadence. At 50k rows it could leave distant rows in the old local epoch for dozens of frames, which is a different kind of visible desync.
Solution: Add `MinimumTimeSliceBatchSize = 10000` and clamp configured AUP rebase batches to 10k..50k. `ResolveQualityScaledBatchSize` now builds its low-quality floor from `max(10000, activeCount * 0.2)` and still uses polynomial `math.lerp`/`math.step` scaling to converge toward full active count as `GlobalQualityWeight` rises.
Rejected Alternatives: Keeping 1024 was rejected as over-slicing. A binary low/high branch was rejected by the GlobalQualityWeight law. Always shifting 50k in one frame was rejected because it recreates the stutter spike Task 13 exists to remove.
Scalability potential: Low/MX350 gets a prompt-faithful five-slice cadence; middle increases slice width smoothly; high/ultra move toward full rebase and let visual systems spend saved CPU on richer post-shift presentation.
Hardware Impact: Low-tier per-slice work increases from the prior 1024 floor to about 10k rows, but total stale-frame exposure drops by roughly 5x-8x for 50k rows. Exact frame cost remains PENDING PROFILER. No `dotnet build` was launched after this delta by user instruction.
