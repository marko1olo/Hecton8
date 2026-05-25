# Rationale_SHINOBU_221
Date: 2026-05-20
Agent: SHINOBU_221
State: PENDING VERIFICATION

## Pre-Code Analysis
Target: Replace legacy room/global oxygen state with a base-interior atmosphere kernel using aligned cell DTOs, CSR graph buffers, Jacobi diffusion, source/sink jobs, and blackbox telemetry.

Affected systems: `Assets/_Project/Scripts/Atmosphere`, possible legacy references under `Construction` and `Gameplay`, editor diagnostics, docs under `Docs/ARCHITECTURE` only if a new authority route is unavoidable.

Zero GC proof target: hot paths use unmanaged DTOs, NativeArray/NativeList/NativeParallelHashMap or raw pointers inside Burst jobs. No LINQ, managed lists, string formatting, recursion, Unity scene traversal, or concrete cross-domain polling inside solver jobs.

State check: Front/back buffers must not alias. Empty graph must fail safe. OnDisable/dispose must retire native buffers through job handles or cold completed teardown. Signal ingestion must be bounded snapshots or owner-supplied native inputs.

Rule quote: "Front buffer is read-only to all external systems. Back buffer is invisible to all external systems." "Runtime structs used in NativeArray, Burst, SignalBus, telemetry, save staging, or GPU upload paths must be unmanaged, finite-safe, and layout-stable."

First-20-minutes route moment: removes the base-survival blocker where oxygen/CO2/toxin gradients must be local, legible, and dangerous during early habitat exploration instead of a global room float.

## Decision Log
Problem: Task requires gas truth and presentation feedback without global variables or per-frame managed scans.
Solution: Build a stateless Burst kernel around explicit 32-byte `AtmosphereCellDTO`, CSR arrays, quality-weighted Jacobi iterations, quantized gas units, and fixed telemetry ring.
Rejected Alternatives: Unity component traversal and recursive room propagation are unbounded and allocate/branch unpredictably; per-cell GameObjects or particles waste CPU/GPU and violate Visual Fake First.
Scalability potential: Low uses one Jacobi iteration and coarser cadence; Middle uses moderate iterations; High uses more stable diffusion and denser telemetry; Ultra spends saved CPU on richer shader haze, not more gameplay truth than needed.
Hardware Impact: Expected replacement of scene traversal/managed component loops with contiguous Burst reads. Static estimate for 1000 nodes/2500 edges: <100 us solver target on i3/MX350 after Burst, pending Unity profiler proof.

Problem: Legacy `HabitatIntegrityManager` exposes global base oxygen floats while the new task requires local gas truth.
Solution: Route static public reads through `BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot`, and make the legacy accumulator remove its old contribution and stop updating once the runtime snapshot is valid. The old statics remain fallback-only for boot/no-runtime conditions.
Rejected Alternatives: Deleting the statics would break existing UI/save consumers; direct writes into module life-support would create double-drain with suit oxygen.
Scalability potential: Low/Middle read one cached scalar snapshot; High/Ultra can render richer local haze from cell data without changing gameplay authority.
Hardware Impact: Cached scalar read and fallback gate are O(1), estimated <1 us on i3/MX350.

Problem: CSR graph data may not be available from construction agents in this batch.
Solution: Seed a deterministic 1000-node, 2500-edge mock topology in `GlobalDataVault`, then build CSR offsets/destinations/conductance in Burst.
Rejected Alternatives: Managed adjacency lists and recursive traversal were rejected for GC risk and unpredictable stack depth.
Scalability potential: Low uses mock graph at low iterations; Middle/High swap in real construction nodes later through the same DTOs; Ultra can raise graph density without API churn.
Hardware Impact: Cold bootstrap estimate 450 us; hot solver remains contiguous NativeArray reads.

Problem: The first diffusion pass used neighbor-average smoothing instead of the XML-mandated self-weighted Jacobi denominator.
Solution: Relax each gas channel toward `(sumConductanceGas + currentGas) / math.max(sumConductance + 1, 0.0001f)`, then apply continuous diffusion alpha. Temperature uses the same denominator with a lower alpha.
Rejected Alternatives: Pure neighbor average over-diffuses at high quality and drops the explicit self term; in-place Gauss-Seidel remains rejected because it is order-dependent under parallel scheduling.
Scalability potential: Low still runs one cheap pass; Middle/High/Ultra can raise iteration count without changing math identity or introducing binary quality branches.
Hardware Impact: Adds one float4 and one guarded reciprocal per cell with neighbors. Static cost remains inside the 35-220 us estimate for 1000 nodes, pending profiler proof.

Problem: Multiple consumers/leaks can target the same gas cell in parallel.
Solution: Convert breathing and leaks into integer delta lanes and apply `Interlocked.Add` before Jacobi diffusion consumes the deltas.
Rejected Alternatives: Float atomics are unavailable/unsafe in Burst; direct cell mutation from parallel producers would race.
Scalability potential: Low keeps one player consumer and few leaks; Middle/High add bounded NPC/reactor sources; Ultra can add more sources by increasing Vault capacities.
Hardware Impact: Atomic contention bounded by source count, estimated 6-30 us for current caps on i3/MX350.

Problem: A reactor leak source did not exist as a typed gas signal.
Solution: Added unmanaged `ReactorDamageSignal` and made `BioReactor` publish leak severity through `SignalBus<ReactorDamageSignal>`.
Rejected Alternatives: Atmosphere polling `BioReactor` instances or power graph internals would violate domain boundaries and allocate through scene traversal.
Scalability potential: Low uses scalar toxin leak; Middle adds thermal/CO2 response; High/Ultra can drive denser local VFX from the same payload.
Hardware Impact: One signal push during overheat/meltdown; steady cost bounded by SignalBus capacity.

Problem: Hot gas solver needs visual feedback without physically simulating volumetric fog.
Solution: Telemetry writes one shader scalar payload for global haze/fog response.
Rejected Alternatives: CPU particle gas clouds and true volumetric per-cell rendering exceed the 0.1 ms suspicion threshold.
Scalability potential: Low uses one scalar; Middle adds material property response; High/Ultra can spend GPU budget on denser fog shaders without changing gas truth.
Hardware Impact: VisualSync scalar upload estimated <2 us CPU.

Problem: Deterministic rollback needs stable snapshots and postmortem evidence.
Solution: Use explicit DTO layouts, Burst deterministic float mode, active front/back generation-handle swaps, telemetry state hashes, and `Dump_SHINOBU_221.bin` on NaN.
Rejected Alternatives: In-place solver writes and string logs are non-deterministic and not reconstructable.
Scalability potential: Low records same 300-frame ring; High/Ultra can add richer visualization while blackbox remains fixed size.
Hardware Impact: Telemetry pass estimated 20 us for 1000 nodes on i3/MX350; dump is fault-only cold IO.

Problem: Designers need tuning without runtime allocations.
Solution: UI Toolkit editor window writes static tuning scalars, and cold CSV ingest parses `ReadOnlySpan<byte>` into Vault profile DTOs.
Rejected Alternatives: Runtime UI and `string.Split` CSV parsing allocate and violate hot-path policy.
Scalability potential: Low can reduce diffusion rate/iterations; Middle/High/Ultra tune gas dissipation while preserving continuous `GlobalQualityWeight`.
Hardware Impact: Editor/cold path only; no frame cost in player hot path.

Problem: Final compile/profiler verification is blocked by the project build gate because CPU usage is 100.0%.
Solution: Per rule, wait until CPU <=50% and no compiler is running; when the gate opened at CPU=27.2%, launch one single-threaded `dotnet build Hecton8.Core.csproj`.
Rejected Alternatives: Starting another compiler under load risks false failure; attempting to fix unrelated dependency errors in Power/World/Construction/Save domains violates SHINOBU_221 authority.
Scalability potential: None at runtime; this preserves workstation stability so later verification measures actual solver cost instead of saturated-machine noise.
Hardware Impact: Build failed with 72 unrelated dependency errors and no SHINOBU_221-owned files in the emitted error list. Profiler/GC proof remains blocked by external compile wall.

Problem: Central `H8Memory.BufferID` enum additions create compile-wall and merge-conflict risk for a domain-local atmosphere lane.
Solution: Move SHINOBU_221 IDs `71500..71522` into `AtmosphereLogisticsBufferIds` as owner-local numeric `BufferID` casts and document the lane in the binary payload ledger and route card.
Rejected Alternatives: Keeping central enum growth would force core memory file churn for a local atmosphere route; local casts without documentation would violate the Vault route proof rule.
Scalability potential: Runtime behavior unchanged; ownership is clearer for Low/Middle/High/Ultra because buffer capacities and IDs remain stable without core rebuild pressure.
Hardware Impact: No frame-time delta; build graph and merge contention risk reduced.

Problem: Dense per-node `int` delta arrays allow false sharing when several workers atomically add source/sink deltas into neighboring cells.
Solution: Replace the five gas/temperature delta arrays with `AtmosphereDeltaLane64` rows, explicit 64-byte layout, and `NoAlias` raw pointers in Burst jobs.
Rejected Alternatives: Keeping compact `int[]` saves 300 KB but risks MESI cache-line churn under multi-player/fauna/source contention.
Scalability potential: Low pays bounded memory for deterministic safety; Middle/High/Ultra can add more consumers/leaks without changing the write contract.
Hardware Impact: Estimated memory increase is about 300 KB for five 1000-row lanes; expected contention savings only measurable under source spam and still pending profiler proof.

Problem: `ReactorDamageSignal` originally stored a World-domain `AbsoluteUniversePosition`, creating unnecessary contract coupling.
Solution: Store `double3 DamageAup` directly in the 64-byte unmanaged signal. `BioReactor` converts runtime position through `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`, and atmosphere consumes the double3 directly.
Rejected Alternatives: Keeping World AUP in the new signal would tie the new lane to a sibling domain type. Casting absolute AUP to float was rejected for 100 km precision failure.
Scalability potential: Same data width as double3 source truth; no quality branch.
Hardware Impact: Signal payload remains 64 bytes; fewer contract dependencies.

Problem: Post-polish verification still cannot legally compile because CPU stayed above the build gate.
Solution: Run only static proof: XML re-extract, forbidden-pattern scan, pointer alias scan, route-doc presence, and diff whitespace check.
Rejected Alternatives: Running `dotnet build` at CPU=89.0% would violate the user and project guard.
Scalability potential: No runtime change; verification remains static until hardware load drops.
Hardware Impact: No compiler load added to an already saturated machine.

Problem: Task18 required `gas_diffusion_profiles.csv` module-type rows hashed with FNV-1a, but the parser accepted only numeric profile IDs.
Solution: Change the cold parser to read the first token as either a numeric ID or a lowercase ASCII FNV-1a module-name hash from `ReadOnlySpan<byte>`, add `Docs/Atmosphere/gas_diffusion_profiles.csv`, and keep all row hydration inside Vault profile DTOs.
Rejected Alternatives: Managed `string` tokenization and `string.Split` were rejected for allocation risk; numeric-only IDs were rejected because designers need human-readable module names.
Scalability potential: Low/Middle/High/Ultra all use the same profile ABI; high tiers can add more authored module profiles without recompiling C# or changing solver jobs.
Hardware Impact: Cold editor/file ingest only. Hot solver cost unchanged; expected frame cost remains 0 us.

Problem: Direct integer iteration selection from raw `GlobalQualityWeight` can flicker if thermal pressure oscillates near a threshold.
Solution: Filter `GlobalQualityWeight` with a smoothstep hysteresis curve before writing the tuning DTO, shedding quality faster than recovery while preserving the XML-mandated `math.lerp(1,8,q)` iteration law.
Rejected Alternatives: Binary low/high switches and immediate per-frame iteration jumps were rejected because they cause visible cadence instability and violate the state-hysteresis mandate.
Scalability potential: Weak devices shed Jacobi passes quickly; middle hardware recovers gradually; high/ultra retains visual overkill without sudden solver snapping.
Hardware Impact: Adds a handful of scalar ALU ops in PreSimulation, estimated <1 us on i3/MX350, no GC.

Problem: Owned BaseAtmosphereLogistics source still imported `Hecton8.World` for presentation AUP conversion, creating unnecessary sibling-source coupling in the atmosphere lane.
Solution: Remove `using Hecton8.World` from the owned runtime/gizmo files and route gizmo presentation through Core `HectonFloatingOrigin.ToRuntimePosition(double3)`.
Rejected Alternatives: Keeping `AbsoluteUniversePosition.FromAbsolutePosition` in the gizmo was rejected because the gizmo only needs presentation conversion from the already-owned `double3` node AUP.
Scalability potential: No runtime behavior change; compile-wall surface is smaller across Low/Middle/High/Ultra.
Hardware Impact: Same O(sampled gizmo cells) editor-only cost; no player hot-path delta.

Problem: Task17 required a real-time efficiency graph and direct Vault-backed tuning mutation, but the first editor facade only exposed sliders and a text telemetry line.
Solution: Add a UI Toolkit `AtmosphereEfficiencyGraphElement` that draws from the 300-frame telemetry ring through a direct native read handle, and make slider changes write the live `AtmosphereTuningDTO` via `UnsafeUtility.AsRef` while retaining pending defaults for cold start.
Rejected Alternatives: A text-only label hides solver spikes; pending-only statics require waiting for the next pre-simulation write; managed chart datasets were rejected for editor repaint churn.
Scalability potential: Low/Middle/High/Ultra tuning can be exercised in Play Mode without C# recompilation; the graph exposes when quality shedding keeps the solver below the 0.1 ms suspicion line.
Hardware Impact: Player hot path remains 0 us. Editor repaint reads at most 300 telemetry entries and paints columns; no runtime frame cost.

Problem: The emergency mock graph and CSR builder jobs were Burst-decorated but invoked through direct `Execute()`, weakening the proof that the cold isolated stress graph uses the job-system route.
Solution: Replace direct `Execute()` calls with `IJob.Run()` for both mock topology and CSR build during cold bootstrap.
Rejected Alternatives: `Schedule().Complete()` would introduce an explicit completion call and trigger the stall scanner; keeping `Execute()` leaves Burst/job-route ambiguity.
Scalability potential: Low/Middle/High/Ultra all use the same cold seeded graph when construction data is absent; real topology can later replace the source buffers without changing solver ABI.
Hardware Impact: Cold bootstrap only. No player hot-path delta; preserves Task05/06 profiling isolation without frame-loop stalls.

Problem: A reactor leak signal declared in Atmosphere would force future reactor/gameplay publishers to depend on the Atmosphere runtime assembly or source file.
Solution: Move `ReactorDamageSignal` to `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs` under `Hecton8.Core.Contracts.Signals`; BioReactor publishes the Core contract, and Atmosphere consumes it through `SignalBus<ReactorDamageSignal>`.
Rejected Alternatives: Keeping the signal in Atmosphere would invert ownership of the unmanaged ABI; duplicating a gameplay-local signal would create two facts and desync risk.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; the compile-wall surface is smaller because signal ABI authority is central contract-only.
Hardware Impact: No frame-time delta. Payload remains 64 bytes and unmanaged.

Problem: The simulation lock mask protected solver write lanes but did not explicitly lock every read-only input lane used by scheduled jobs.
Solution: Extend `TryLockJobBuffers`/`UnlockJobBuffers` to include `Nodes`, `Consumers`, `ToxicSources`, `Vents`, and `Tuning` while the scheduled solver handle is outstanding.
Rejected Alternatives: Relying on phase convention alone was rejected because editor/cold topology mutation can otherwise race a method-local native view.
Scalability potential: Same across Low/Middle/High/Ultra; more sources can be admitted without changing lane ownership.
Hardware Impact: Adds five Vault lock/unlock calls per scheduled simulation phase, estimated low single-digit microseconds pending profiler proof; prevents undefined memory races.

Problem: The CSR builder counted degrees into `EdgeOffsets[node + 1]` but used a running-prefix loop that wrote `EdgeOffsets[1] = 0`, shifting every adjacency range and starving node 0 of edges.
Solution: Use standard shifted-count CSR prefixing: set `EdgeOffsets[0] = 0`, then for `i=1..nodeCount`, accumulate the stored degree and write the cumulative end offset.
Rejected Alternatives: Counting directly into `EdgeOffsets[node]` would also work but would widen the change and risk cursor assumptions; the shifted-count contract was already present and only the prefix loop was wrong.
Scalability potential: Low through Ultra all get correct contiguous adjacency ranges; high graph density no longer silently loses the first node range.
Hardware Impact: Same O(N+E) cold rebuild cost. Runtime hot solver correctness improves; no added frame cost.

Problem: Public editor/gizmo telemetry reads could resolve `_frontCells` after the runtime swapped active handles but before the scheduled solver completed writing the new front buffer.
Solution: Make `TryGetEditorTuning`, `TryGetLatestTelemetry`, `TryGetTelemetryReadOnly`, and `TryGetGizmoCell` fail closed while `_simulationScheduled` is true; `SetEditorTuning` also refuses live Vault mutation during an outstanding solver job.
Rejected Alternatives: Relying on Vault locks alone was rejected after source inspection showed `TryLockBuffer` is a lock-count/compaction guard, not mutual exclusion.
Scalability potential: Debug presentation may skip a frame under load, which is acceptable across Low/Middle/High/Ultra; gas truth remains deterministic.
Hardware Impact: Adds one boolean branch to editor/debug read APIs and avoids unsafe read/write races. Player hot solver cost unchanged.

Problem: CSV parsing used one `malformed` flag for the whole file, so a single bad profile row marked every subsequent valid profile row as malformed.
Solution: Split diagnostics into `rowMalformed` and `anyMalformed`; each `AtmosphereGasProfileDTO.Flags` now reflects only its own row while the aggregate return still reports a malformed file.
Rejected Alternatives: Ignoring malformed rows would hide designer data errors; keeping a global row flag corrupts valid profile diagnostics.
Scalability potential: Human-authored Low/Middle/High/Ultra gas profiles can coexist in one file without one bad row poisoning all later rows.
Hardware Impact: Cold CSV ingest only. Adds no player frame cost.

Problem: Conservation correction previously concentrated all quantization residual into the first back-buffer cell, creating an artificial gas anchor and local visual/logic bias. A correction pass before quantization would still allow the following floor/remainder pass to change the final integer mass.
Solution: Run quantization first, then apply bounded residual correction across Back cells per gas channel after expected/actual integer totals are known; fill available capacity for positive residuals and remove available units for negative residuals until the frame residual is exhausted.
Rejected Alternatives: Keeping `Back[0]` correction is O(1) but makes one node absorb every rounding error; correcting before quantization was rejected because quantization can reintroduce mass drift; adding another parallel pass with atomics would increase scheduling and contention for a bounded 1000-node lane.
Scalability potential: Low pays the same deterministic pass shape; Middle/High/Ultra avoid a visible/toxic hotspot caused by conservation bookkeeping rather than actual gas flow.
Hardware Impact: Adds up to four bounded linear correction scans after the existing conservation sum. Static estimate rises from about 18 us to 18-30 us for 1000 nodes on i3/MX350, pending profiler proof.

Problem: Simulation Vault locks were acquired through the active front/back buffer IDs, but active handles can be swapped after each Jacobi iteration. With an odd iteration count, unlocking through the current active handles could release the wrong cell rows and leave the original locked row retained.
Solution: Capture `_lockedFrontBufferId` and `_lockedBackBufferId` at lock acquisition and use those exact IDs during `UnlockJobBuffers`, resetting them only after all locked rows are released.
Rejected Alternatives: Unlocking through `ActiveFrontBufferId()`/`ActiveBackBufferId()` was rejected because it is handle-state dependent; blocking front/back swaps until post-simulation was rejected because it would break double-buffered Jacobi ownership.
Scalability potential: Low through Ultra can vary iteration count continuously without changing lock semantics; odd/even iteration changes no longer alter Vault lifetime behavior.
Hardware Impact: Adds two `BufferID` fields and two assignments per scheduled solve. Runtime cost is below 1 us; it prevents lock-count leaks that would otherwise block later Vault compaction or editor hydration.

Problem: The diffusion job trusted CSR offsets after build. A corrupted or stale offset row could produce a negative start or an end beyond the destination/conductance lane, causing an unsafe Burst read before telemetry can record the fault.
Solution: Clamp `start = clamp(EdgeOffsets[index], 0, EdgeCount)` and `end = clamp(EdgeOffsets[index + 1], start, EdgeCount)` before iterating edges.
Rejected Alternatives: Trusting the CSR builder alone was rejected because Vault state can be hot-swapped or editor-mutated; adding a separate validation pass every frame was rejected as unnecessary O(N) overhead when two scalar clamps inside the existing loop protect the same read.
Scalability potential: The guard is constant-cost per active node across Low/Middle/High/Ultra and does not introduce a binary quality branch.
Hardware Impact: Adds two integer clamps per cell, estimated <3 us at 1000 nodes on i3/MX350; it eliminates undefined memory read risk from malformed CSR ranges.
