# Rationale_SHINOBU_340

Status: STATIC VERIFIED / COMPILE GATED BY CPU+DOTNET LOAD

## Decision 000 - Domain Route

Problem: Sump-pump evacuation must not become per-pipe object simulation, recursive traversal, or physics particles.
Solution: Use a CSR-directed graph with flat unmanaged DTOs, double-buffered pressure, Burst jobs, AUP gravity conductance, and scalar compartment-volume extraction.
Rejected Alternatives: Per-pipe MonoBehaviour updates, recursive `PropagateWater`, Rigidbody water particles, and Unity physics callbacks; all violate cache locality, determinism, and zero-GC mandates.
Scalability potential: Low uses fewer Jacobi iterations and shader-only visual flow; Middle increases iterations and active-node capacity; High increases cadence and telemetry; Ultra spends saved CPU on richer decoupled pipe visuals without changing simulation truth.
Hardware Impact: i3/MX350 avoids object traversal, recursion, and per-particle physics; expected savings are architectural/static until profiler proof exists.

## Decision 001 - Mandate Set

Problem: The prompt spans logistics, flooding, AUP, layout, memory, and Burst job policy.
Solution: Loaded six relevant registry mandates before code: logistics graph flow, interior flooding, AUP determinism, ARM64 layout, zero-GC, and native jobs.
Rejected Alternatives: Loading unrelated rendering/audio/AI mandates or coding from prompt alone; both increase noise and miss route authority.
Scalability potential: Mandate set covers low-to-ultra behavior without binary quality switches.
Hardware Impact: Prevents main-thread and GC regressions on low-end silicon; no measured microsecond claim yet.

## Decision 002 - Drainage Node ABI

Problem: Fluid pressure rows must be copied, snapshotted, and read by Burst without CS1612 stack-copy traps or ARM64 unaligned penalties.
Solution: Replaced the old pump row with exact `DrainageNodeDTO` layout: 32 bytes, explicit offsets, public raw fields, private 12-byte padding, and editor/development offset validation.
Rejected Alternatives: Auto-layout structs, properties, `Pack=1`, or managed pump classes; each either hides stack copies or risks unaligned mobile reads.
Scalability potential: Low/Middle/High/Ultra all share the same DTO and BufferIDs; quality changes iteration count only, not memory shape.
Hardware Impact: i3/MX350 reads a compact 64 KB node lane at 2000 nodes; no per-node object dereference. Estimated gain versus object graph traversal: tens of microseconds per solve, unmeasured.

## Decision 003 - Power Dependency Route

Problem: Pumps must not run as independent magic when the PowerGrid is brown or absent.
Solution: `ApplyPumpPowerConstraintJob` reads `PowerGridBufferIds.Nodes` and `PowerGridBufferIds.PotentialFront`, clamps each pump's `MaxPumpRate` through the resolved potential, and writes a local scalar lane for later jobs.
Rejected Alternatives: Unity events, `FindObjectOfType`, component listeners, or cached MonoBehaviour references; all add cold/hot ownership ambiguity and managed callbacks.
Scalability potential: Low still receives exact power truth with fewer pressure iterations; Ultra can spend saved cycles on richer pipe VFX while pump authority stays identical.
Hardware Impact: Cheap devices pay one flat indexed read per pump instead of event fanout. Estimated avoided overhead: ~25 us in a 2000-node stress scene, pending profiler proof.

## Decision 004 - Atomic Evacuation

Problem: Multiple pumps may target compartment water rows; failed races must not destroy or create water.
Solution: `ExecuteWaterEvacuationJob` uses a padded room lock plus float-bit `Interlocked.CompareExchange`; failed atomic deduction aborts the frame's deduction and records conservative mass error.
Rejected Alternatives: Plain `front.CurrentWaterVolume -= x`, managed locks, or single-thread water extraction; these either race, allocate/box, or waste parallelism.
Scalability potential: Low quality reduces solve cadence/iterations, not water truth. High/Ultra can drain with more frequent equilibrium while preserving the same scalar mass route.
Hardware Impact: i3/MX350 avoids physics bodies and managed locks. Contention is bounded to 64 spins per room; mock topology maps one room per node.

## Decision 005 - Dear Lie Pipe Flow

Problem: Visible rushing pipe water is needed without CPU geometry, CPU particles, or Rigidbody droplets.
Solution: `PipeEdgeFlowJob` derives scalar flow from pressure delta and conductance, writes `DrainagePipeFlowGpuDTO`, and runtime uploads a double-buffered StructuredBuffer for shader normal/foam panning.
Rejected Alternatives: ParticleSystem leaks, transparent mesh spawning, or per-pipe GameObject animation; all scale badly and do not improve gameplay truth.
Scalability potential: Low shows panned normal/opacity from scalar flow; Middle increases cadence; High/Ultra can add refractive foam/salt crystal shader overkill without changing CPU simulation.
Hardware Impact: MX350 gets a single buffer upload and shader fake instead of thousands of particles. Estimated CPU saving: hundreds of microseconds versus particle authority, unmeasured.

## Decision 006 - AUP Gravity Math

Problem: Absolute 100 km positions lose vertical precision if cast to float before slope math.
Solution: The gravity multiplier subtracts high and low `double3` AUPs first, then clamps/casts the relative vertical meters to float before assist/resistance scaling.
Rejected Alternatives: `float3` absolute positions, Unity `Transform.position`, or physics gravity forces; all can drift at map edges or pull in scene hierarchy dependencies.
Scalability potential: All quality levels keep the same gravity truth. Low merely evaluates fewer Jacobi iterations so pressure waves settle slower.
Hardware Impact: Double subtract is a tiny fixed ALU cost buying correctness; cheaper than debugging uphill water or running rigidbody flow.

## Decision 007 - Compile Gate

Problem: The mandate requires compile verification but also forbids rebuilds under CPU load or active dotnet/csc.
Solution: Checked CPU/process state before build. CPU was 100% on the first gate, 65% on an intermediate gate, 100% on a later gate, 47% with seven `dotnet` workers on a later gate, 99.03% with one `dotnet` worker on a later gate, and 91.47% with one `dotnet` worker on the latest gate, so build was not launched. Static scans and file proofs were completed instead.
Rejected Alternatives: Launching `dotnet build` anyway or killing other agents' processes; both violate machine protection and concurrent-agent rules.
Scalability potential: No runtime effect.
Hardware Impact: Prevented adding more compiler load to an already saturated developer machine.

## Decision 008 - Read Accessor Purity

Problem: `TryReadTuning()` could allocate/grow/resolve Vault handles through `TryResolveAndInitializeBuffers()`, violating the global read-accessor doctrine.
Solution: Cache `GlobalRegistry.DataVault` during `OnEnable()` and make `TryReadTuning()` pure: if buffers are not ready, return the offline fallback and `false`.
Rejected Alternatives: Lazy Vault initialization from `TryRead*`, editor-triggered bootstrap through a read facade, or polling `GlobalRegistry.DataVault` during slow ticks.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; only ownership route changed.
Hardware Impact: Prevents hidden cold-path allocation and route mutation from a read call on i3/MX350; no measured microsecond claim.

## Decision 009 - Front Snapshot Water Ownership

Problem: `ExecuteWaterEvacuationJob` previously deducted from both fluid front and back buffers, then attempted a best-effort rollback if the back write failed. That can corrupt a snapshot and can fail under contention.
Solution: Treat `FrontCompartments` as read-only owner snapshot, compute available water from min(front, back), and mutate only `BackCompartments` under the 64-byte room lock.
Rejected Alternatives: Double mutation, compensation writes into front, managed locks, or serial main-thread draining.
Scalability potential: All quality weights keep the same fluid truth route. Quality controls cadence/iterations, not buffer ownership.
Hardware Impact: Removes an atomic write and rollback branch from the contended path; expected low-end gain is small but correctness gain is material.

## Decision 010 - Blackbox Raw ABI

Problem: The dump wrote telemetry with `BinaryWriter` field-by-field and omitted `Reserved0`, producing 60-byte rows instead of the 64-byte telemetry ABI.
Solution: Add explicit 64-byte `DrainageDumpHeader`, validate its offsets, and write header plus raw `DrainageTelemetryEntry` rows through `ReadOnlySpan<byte>`.
Rejected Alternatives: Managed field serialization, JSON/text dumps, or relying on row reconstruction heuristics.
Scalability potential: No quality effect; fault proof stays stable across all tiers.
Hardware Impact: Crash-only path. The gain is forensic determinism, not frame-time reduction.

## Decision 011 - Mock Facade And No Force-Complete

Problem: The public mock generator could force-complete an active solver chain, creating an unbounded main-thread stall outside teardown.
Solution: Add `TryGenerateMockDrainageNetwork()` as a cold/editor facade and reject mock seeding while `_solverScheduled` is active. `CompleteScheduledSolverForTeardown()` remains teardown-only.
Rejected Alternatives: Editor button scene search, public force-complete during gameplay, or silent job cancellation.
Scalability potential: No quality effect; protects dispatcher ownership at every tier.
Hardware Impact: Prevents worst-case frame hitch on low-end hardware if a designer presses the mock button during an active solve.

## Decision 012 - Compile-Wall Contract Debt

Problem: The SHINOBU_340 runtime depends on `Hecton8.Physics` and `Hecton8.Power` DTO identity for `FluidCompartmentDTO`, `PowerNodeDTO`, and existing cross-domain BufferIDs.
Solution: Record this as YELLOW route debt. Literal namespace imports are removed, and no Construction asmdef exists in the current tree, but a future asmdef split requires moving these DTOs/IDs into a shared contracts assembly or introducing first-party contract facades.
Rejected Alternatives: Duplicating DTOs in Construction, inventing shadow BufferIDs, or lying that the direct namespace dependency is fixed.
Scalability potential: No runtime quality effect until assembly split; contract extraction would reduce compile-wall blast radius.
Hardware Impact: Runtime cost is unchanged; iteration-speed risk remains for future assembly boundaries.

## Decision 013 - Type Identity Over Shadow Contracts

Problem: Removing direct `using` imports was feasible, but duplicating Physics/Power DTOs inside Construction would silently break Vault handle resolution because GlobalDataVault hashes `typeof(T).TypeHandle`.
Solution: Delete literal `using Hecton8.Physics;` and `using Hecton8.Power;`, keep exact existing DTOs as fully qualified names, and leave shared-contract extraction as coordinated future work.
Rejected Alternatives: Layout-identical Construction DTO shadows, raw byte views, or alias tricks; these either fail Vault type validation or bypass safety checks.
Scalability potential: No visual/runtime quality effect. Compile-wall hygiene improves future assembly split readiness without changing data truth.
Hardware Impact: No frame-time claim. Prevents a catastrophic runtime route mismatch that would stall pumps on all hardware.

## Decision 014 - Deferred Blackbox Writer

Problem: The fault branch previously built paths and opened a FileStream from `LateFrameTick`, making a diagnostic fault capable of blocking the visual-sync frame.
Solution: Cold-create dump path, directory, byte scratch, event, and background writer thread in `OnEnable()`. Fault handling copies raw 64-byte header plus telemetry rows into the preallocated byte array and signals the writer.
Rejected Alternatives: `BinaryWriter`, JSON/text dumps, synchronous `FileStream` in the fault branch, or dropping dump proof entirely.
Scalability potential: Low-to-ultra behavior is unchanged; forensic proof no longer competes with active visual-sync work.
Hardware Impact: Removes unbounded disk I/O from the fault frame on i3/MX350-class storage. Exact stall avoided depends on filesystem and is not honestly measurable here.

## Decision 015 - Header ABI Decoder Contract

Problem: The old dump header used a 4-byte magic and row-size ordering that did not match the debug decoder mandate.
Solution: `DrainageDumpHeader` now starts with 8-byte little-endian `HECTON8\0`, `EntryCount@8`, `StructSizeBytes@12`, then version/capacity/write cursor/oldest/runtime hash/flags, padded to 64 bytes.
Rejected Alternatives: Keeping the old `uint Magic` layout or making the decoder infer row size from file length.
Scalability potential: No quality effect; improves blackbox interoperability across all tiers.
Hardware Impact: Fixed 64-byte header remains one L1 row; no frame-time claim.

## Decision 016 - Runtime-Owned Editor Snapshot

Problem: The SceneView pressure x-ray polled GlobalRegistry/DataVault directly on repaint and could observe live Vault rows while the solver was scheduled.
Solution: Add `TryCopyPressureDebugSnapshot()` on the runtime. It refuses active solver/mock fences and copies bounded node/edge snapshots into editor-owned static arrays.
Rejected Alternatives: Editor-side direct Vault reads, scene objects for debug lines, or blocking completion from the gizmo.
Scalability potential: Editor-only; runtime quality unaffected. Debug visibility remains dense at high capacity without gameplay allocations.
Hardware Impact: Prevents editor repaint from racing Burst-owned lanes; no runtime hardware estimate.

## Decision 017 - Visual Fanout And Prewarm

Problem: Flow visual upload could allocate GraphicsBuffers from `LateFrameTick`, and connection-spline node flow published all active nodes through a managed renderer route.
Solution: Prewarm double GraphicsBuffers at cold initialization and make the upload path fail closed if prewarm is absent. Spline flow publication now samples a continuous quality-scaled node budget and ignores tiny flow rows.
Rejected Alternatives: First-use GraphicsBuffer creation during visual sync, all-node dictionary fanout every frame, or removing shader flow output.
Scalability potential: Low quality collapses managed spline publication toward 16 sampled nodes while StructuredBuffer flow remains available; Ultra can publish dense flow and spend GPU shader budget.
Hardware Impact: Removes first visual upload allocation spike and reduces low-tier managed fanout. Exact profiler savings pending Unity run.

## Decision 018 - Human Tuning Bridge

Problem: The editor window exposed live sliders but did not expose the CSV/binary profile path, schema hash, row count, validation status, or bake/import controls required for non-programmer tuning.
Solution: Add an editor-only Pipe Profile CSV Bridge panel. It reads a selected CSV on explicit button press, hashes the source bytes, imports through the runtime facade, validates `PipeProfileDTO` layout, and can bake a deterministic `.h8bin` with a 64-byte header plus 32-byte profile rows.
Rejected Alternatives: Requiring C# recompiles for profile changes, relying on inspector fields only, or using `BinaryWriter`/managed string splitting for the profile bake.
Scalability potential: Low-to-ultra runtime behavior is unaffected; designers can tune pipe conductance/pump profiles without touching code, then ship a compact binary payload.
Hardware Impact: Editor-only cold path. Runtime benefit is fewer debug/recompile cycles, not per-frame microseconds.

## Decision 019 - Timing Provenance And Read-Only Fluid Snapshot

Problem: `SolverWallMicroseconds` could be misread as exact Burst body execution time, and the read-only fluid front snapshot was converted with a write-capable unsafe pointer helper.
Solution: Mark stamped solver timing with `SumpDrainageTelemetryFlags.ScheduleWindowTiming` and use `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` for `FrontCompartments`. The value is a scheduler-to-finalize window until Unity Profiler/SystemDispatcher timing proof exists.
Rejected Alternatives: Forcing hidden `.Complete()` just to time the job body, claiming profiler-grade Burst timing without a profiler run, or passing a mutable pointer for a read-only owner snapshot.
Scalability potential: Quality behavior is unchanged. The change preserves route truth across low-to-ultra devices and makes telemetry provenance explicit for forensic tools.
Hardware Impact: No frame-time gain claimed. It prevents safety-check/write-permission faults in editor/runtime validation and blocks misleading performance reports.

## Decision 020 - Subagent A Compile-Risk Reconciliation

Problem: The static compile-risk auditor correctly flagged dependencies that need Unity/project validation: tick registration APIs, Fluid and Power DTO fields, BufferIDs, pointer utilities, GraphicsBuffer lock-write APIs, and editor span APIs.
Solution: Source-confirm these routes before any build: the registry tick APIs are widely used with `PriorityLayer`, Fluid front/back BufferIDs and `FluidCompartmentDTO` fields exist, `FluidCompartmentPointerUtility.ElementRef` exists, PowerGrid IDs and `PowerNodeDTO.NodeHash` exist, and `GraphicsBuffer.LockBufferForWrite` with `UsageFlags.LockBufferForWrite` is already an established project pattern.
Rejected Alternatives: Launching build under CPU/dotnet gate, duplicating Fluid/Power contracts, or inventing fallback BufferIDs.
Scalability potential: No quality curve change. This reduces integration risk without touching runtime authority, DTO layout, or save identity.
Hardware Impact: No runtime gain claimed. Prevents avoidable compile-wall churn and false fixes under a closed build gate.

## Decision 021 - Heartbeat Telemetry And Atomic Profile Bake

Problem: Subagent B identified that solve-cadence throttling left blackbox gaps on non-solve frames, and the editor profile bake directly overwrote the target `.h8bin` without temp validation or row/column diagnostics.
Solution: Add `SumpDrainageTelemetryFlags.HeartbeatFrame` and write idle LateFrame heartbeat rows only when no mock/solver job is scheduled. The heartbeat preserves last solved total/pressure state, zeroes per-frame evacuation and solver wall time, advances `_frameIndex`, and writes frame summary plus ring without completing jobs or touching files. The profile bridge now exposes schema version and validation code/row/column/field, writes to `.tmp`, flushes, validates header/count/stride/source hash/layout hash, then publishes through `File.Replace` or first-create `File.Move`.
Rejected Alternatives: Marking Task 15 as solve-cadence-only, forcing a same-frame `.Complete()` for telemetry purity, writing blackbox heartbeat from a tiny Burst job, or retaining `FileMode.Create` direct target overwrite. Tiny heartbeat jobs would violate the dispatcher amortization rule; direct overwrite violates the CSV-to-binary bridge law.
Scalability potential: Low-quality cadence may stretch solves, but blackbox proof remains per free LateFrame through cheap 64-byte heartbeat rows. Middle/High/Ultra keep richer solver rows when cadence allows. The profile bridge remains editor-only and does not alter runtime quality curves, DTO layout, or authority route.
Hardware Impact: Runtime heartbeat adds bounded owner-phase scalar stores and avoids hidden synchronization stalls. Editor atomic bake adds cold readback safety only; no runtime frame-time claim is made.
