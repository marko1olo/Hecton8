# Rationale_SHINOBU_114

Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE until compile/runtime proof is produced.

## Decision 000 - Initial Architecture Boundary

Problem: Batch requires replacing chaotic base logistics with CSR/Jacobi while 20+ agents may rewrite adjacent systems.
Solution: Keep SHINOBU_114 ownership inside Habitat/Base logistics; expose data through owner-local native buffers first and only use global authority surfaces where existing contracts exist. Use CSR snapshots, Jacobi relaxation, and shader flow scalars.
Rejected Alternatives: Direct dependencies on base-builder, oxygen, power consumer, or presentation concrete classes. Those introduce cross-domain compile walls and hot-path polling.
Scalability potential: Low uses 1-2 Jacobi iterations at low cadence; Middle uses 4-6; High uses 8; Ultra uses 10 plus denser visual flow interpolation.
Hardware Impact: i3/MX350 gains from linear CSR memory and no recursive/OOP graph traversal; expected savings are static estimates until profiler proof.

## Decision 001 - Mandate Set

Problem: Task crosses graph math, native memory, AUP, telemetry, and visual flow.
Solution: Use 8 mandates: logistics graph, ARM64 layout, zero GC, native jobs, execution phases, AUP determinism, crash telemetry, cinematic cheat.
Rejected Alternatives: Reading only the batch prompt. That misses DataVault/global route and visual fake restrictions.
Scalability potential: Mandates define low/middle/high/ultra paths through iteration count, cadence, and shader-only presentation load.
Hardware Impact: Prevents heap/branch/recursion designs that punish weak silicon; preserves headroom for visual overkill on strong devices.

## Decision 002 - Preserve Existing Runtime Owner

Problem: The project already had `ShinobuLogisticsRouter` registered through `PowerGridManager`; creating a second base logistics graph would split authority.
Solution: Upgrade `ShinobuLogisticsRouter` in place: explicit DTOs, DataVault handles, CSR builder, Jacobi solver, telemetry, tuner, and gizmo stay under one owner.
Rejected Alternatives: New parallel runtime service or direct dependency on WFC/base-builder internals. That creates duplicate state and compile walls with other agents.
Scalability potential: Low uses one Jacobi sweep and slower oxygen cadence; Middle uses 4-6 sweeps; High uses 8; Ultra uses 10 plus dense flow scalar updates.
Hardware Impact: i3/MX350 avoids managed adjacency and per-resource routing; RTX-class hardware spends extra iterations on smoother visual flow.

## Decision 003 - DTO Layout And Buffer Route

Problem: Old node DTO carried mixed sequential fields and the graph lacked dedicated component/pressure/flow lanes.
Solution: `LogisticsNodeDTO` is explicit 32B with `NodeHash`, `Capacity`, `CurrentLoad`, `Flags`, `EdgeStartIndex`, `EdgeCount`, pads. New vault lanes: `ShinobuLogisticsComponentIds`, `PressureFront`, `PressureBack`, `EdgeRemainderMilli`, `CsrEdgeCapacities`, `CsrEdgeFlow01`, `ComponentSpecs`.
Rejected Alternatives: C# properties, sequential layout, or local `NativeArray` ownership for shared solver state. Those violate ARM64 and DataVault mandates.
Scalability potential: Low reads fewer contiguous pressure sweeps; Ultra keeps the same layout and increases iteration count without changing ABI.
Hardware Impact: Fixed 32B DTO improves cache-line predictability; uninitialized vault buffers avoid redundant cold memset on weak CPUs.

## Decision 004 - CSR/Jacobi Instead Of Priority BFS

Problem: Previous solve allocated power by BFS reachability/priority, which cannot model looped pressure networks and gives poor flow scalars.
Solution: `BuildCsrGraphJob` rebuilds offsets/destinations/conductance; `LogisticsFlowSolverJob` identifies islands and runs deterministic Jacobi pressure relaxation over CSR.
Rejected Alternatives: Per-resource pathfinding, managed `NativeParallelMultiHashMap` traversal, or recursive DFS. Those are cache-hostile or unstable at large base sizes.
Scalability potential: Low=1 iteration, Middle=4-6, High=8, Ultra=10. The same graph remains deterministic; only convergence quality changes.
Hardware Impact: Linear CSR reads should outperform scattered hash/list traversal on i3/MX350; saved cycles buy smoother shader flow on high-end hardware.

## Decision 005 - Visual Fake And Telemetry

Problem: Physical resource objects in pipes would waste CPU and hide solver failures.
Solution: Edge `Flow01` drives existing pipe shader scalar via `ConnectionSplineBatchRenderer.SetPipeNodeFlow`; telemetry ring records nodes/components/iterations/micros and dumps to both `Docs/AgentLogs/Dump_SHINOBU_114.bin` and `Docs/AgentLogs/Dump_LOGISTICS_SURGEON.bin` on fault.
Rejected Alternatives: Debug GameObjects, particle payloads, or string-only diagnostics. They add GC and do not preserve forensic state.
Scalability potential: Low still shows coarse flow pulses; Middle/High/Ultra increase smoothness through solver iterations and shader scalar density.
Hardware Impact: Removes per-item simulation entirely; ring write is fixed O(1), target below 5 us pending profiler.

## Decision 006 - H-Phi Scratch Eviction Polish

Problem: Ultra-polish audit found private persistent `NativeQueue<int>`, `NativeQueue<MockModuleStateSignal>`, `NativeQueue<HullBreachSignal>`, and `NativeList<int>` in the router. Those allocations were cold, but they still violated the Vault sovereignty rule and created a local signal corridor duplicate.
Solution: Expand the existing `ShinobuLogisticsCounters` Vault int lane with three bounded scratch segments: `BfsQueueBase`, `ReachableOrderBase`, and `BreachNodeBase`. The solver now uses head/tail indices for BFS, writes source-less island order into a contiguous int range, records breached node indices into the same Vault lane, and publishes the existing `FluidIncursionSignal` after the solve. Burst jobs now use `[NoAlias]` and `CompileSynchronously=true`; runtime layout validation checks sizes without reflection, editor validation checks offsets.
Rejected Alternatives: Keeping local native queues/lists was rejected because it creates memory ownership outside GlobalDataVault. Adding new BufferIDs for BFS and breach scratch was rejected because the existing counters lane can hold the bounded int scratch without widening Core.Memory again. Publishing a local breach signal was rejected because `FluidIncursionSignal` already owns the public flood/incursion route.
Scalability potential: Low keeps one Jacobi sweep and sparse oxygen cadence while still isolating islands exactly; Middle/High/Ultra increase convergence and shader flow smoothness without changing ABI. On top-tier hardware the saved CPU path buys denser visual scalar response in pipe shaders rather than physical resource objects.
Hardware Impact: i3/MX350 avoids NativeQueue block churn, local container lifetime tracking, and hash/list traversal; expected gain is static until Unity profiler proof. RTX-class hardware gets cleaner SIMD aliasing input for Burst and smoother high-iteration flow visuals.

## Decision 007 - ARM64 Telemetry And CSV Scratch Polish

Problem: Ultra-polish audit found two stale seams: `LogisticsGraphTelemetryEntry` relied on sequential layout with only total size asserted, and the cold CSV importer owned a private managed `byte[16KB]` scratch buffer with a stale SHINOBU_13 owner comment. The fault export also lacked the global `Dump_SHINOBU_114.bin` path.
Solution: Convert `LogisticsGraphTelemetryEntry` to `[StructLayout(LayoutKind.Explicit, Size = 64)]` with 0/8/12/16/20/24/28/32/36/40/44/48/52/56/60 offsets; expand editor offset validation to node, edge, tuning, component spec, and telemetry DTOs; route fault dumps to `Dump_SHINOBU_114.bin`, `Dump_LOGISTICS_SURGEON.bin`, and `Dump_SHINOBU_114.h8dump`; add `BufferID.ShinobuLogisticsCsvScratch=70550` and read CSV bytes directly into Vault-owned native scratch via `Span<byte>` over the NativeArray pointer.
Rejected Alternatives: Keeping sequential telemetry was rejected because size-only proof does not catch field drift. Keeping private `byte[]` was rejected because it is persistent managed staging outside the Vault, even if cold/editor gated. Reusing unrelated Babel/input CSV scratch lanes was rejected because one fact needs one owner and one route.
Scalability potential: Low keeps CSV reload cold and absent from player hot path; Middle/High keep designer hot reload at safe phase boundaries; Ultra can add richer editor previews without widening gameplay DTOs.
Hardware Impact: i3/MX350 avoids managed scratch retention and guards telemetry cache-line predictability. Runtime speed is unchanged until a fault/CSV reload path; the value is sovereignty, ARM64 field proof, and forensic route correctness.

## Route Card - SHINOBU_114 Vault CSR Lanes

Route ID: SHINOBU_114_BASE_LOGISTICS_CSR
Date: 2026-05-19
Owner: SHINOBU_114
Owner domain: Echelon 6 Habitat & Vehicles
Owning file/system: `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs`

Problem: Base logistics needs shared, persistent CSR pressure state for solver, editor, gizmo, telemetry, and shader flow without managed graph traversal.
Why owner-local data is insufficient: Existing runtime already uses `GlobalDataVault`; editor/gizmo/telemetry need stable handles without direct object graph ownership.
Why direct caller/owner interface is insufficient: Consumers are cold/editor visual inspection and existing renderer scalar bridge; raw topology state must survive route rebinding/compaction.

Instrument: GlobalDataVault / IDataVault; black-box telemetry route.
Producer phase: SlowTick schedules simulation jobs; LateFrameTick completes and publishes visual scalar.
Consumer phase: solver jobs read/write during scheduled simulation; editor reads only when no pending solve/local shift; renderer receives scalar after completion.
Cadence: topology rebuild on mutation/mock rebuild; flow solve on existing slow tick cadence; editor refresh on editor update only.
Expected max events/reads per frame: 1000 nodes, 2500 flat edges, 6000 CSR adjacency entries, 300 telemetry rows.
GlobalQualityWeight behavior: smoothstep-shaped `math.lerp(1,10.999,weightCurve)` iteration count; oxygen cadence blends 5..1; Jacobi smoothing scales 0.72..1.0 through the same curve.

Payload/data shape: unmanaged DTOs and primitive arrays only. BFS queue, reachable order, and breach node side-effects are int slices inside `ShinobuLogisticsCounters`; cold CSV staging uses `ShinobuLogisticsCsvScratch=70550`.
Managed fields present: cold `string _csvPath` and `DateTime _csvLastWriteUtc` for editor/development CSV reload only; no managed hot-path arrays or containers.
UnityEngine.Object fields present: no.
Layout proof: `ValidateLayouts` uses `UnsafeUtility.SizeOf`; editor validation checks explicit field offsets for node, edge, tuning, component spec, and telemetry DTOs.
Capacity: `MaxNodes=1000`, `MaxDirectedEdges=3000`, `MaxAdjacencyEntries=6000`.
Overflow/failure mode: `CapacityExceeded` fault flag; source-less islands forced to zero; NaN/infinite-loop faults dump black box.

Telemetry fields: frame, node count, active count, fault flags, generated/consumed power, oxygen ratio, supply ratio, component count, Jacobi iterations, solver micros.
Black-box fields: last 300 `LogisticsGraphTelemetryEntry` records.
Profiler marker: pending; static implementation only.
GC proof required: compile + Unity profiler/GCMonitor pending.

Shutdown/disposal rule: complete pending job handles only as a shutdown fence, then clear vault aliases; no SHINOBU_114 private native containers remain to dispose.
Scene unload behavior: `Dispose` clears active singleton and native scratch.
Stale-handle behavior: `RefreshVaultAliases` rejects compaction fence and resolves current handles.

Rejected alternatives: owner-local private arrays only; managed `List<Node>` graph; new duplicate service; cold HectonEventBus hook; physical resource GameObjects.
Why this does not increase global monolith risk: It extends existing SHINOBU logistics vault route and keeps ownership in one runtime; no new registry service slot.
H-Phi impact expected: low to medium, because added global buffers are cohesive state for one existing owner.
Runtime proof required before acceptance: Unity compile, play-mode mock graph solve, GC allocation capture, profiler timing, visual gizmo screenshot.
Reviewer: Integrator
Status: PROPOSED pending compile/runtime proof.

## Verification Note 001 - Compile Wall Boundary

Problem: After the ARM64/Vault scratch patch, a compile check was required, but project rules forbid build under CPU >50 percent or while `dotnet`/`csc` is already running.
Solution: Waited for CPU guard to sample below the limit (`44.393,22.728,13.718`) and no `dotnet`/`csc` process output, then ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`. The build failed before SHINOBU_114-specific errors on unrelated files: `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs` missing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, and `UberNoirReconstructionVaultIds`; `Assets/_Project/Scripts/Optimization/AssetRecord.cs` missing `double3`.
Rejected Alternatives: Fixing Visor reconstruction or Optimization asset records inside the base logistics patch was rejected as cross-domain sabotage. Re-running build immediately was rejected because it would reproduce the same unrelated compile wall.
Scalability potential: No runtime scalability claim changes; this is verification routing only.
Hardware Impact: No runtime impact. Compile proof remains blocked by unrelated source errors.

## Decision 008 - CSV Retry Fence And Full Vault Audit Hash

Problem: The cold CSV hot-reload path assigned `_csvLastWriteUtc` before successful native-scratch read and parse. A transient file-lock, partial write, or zero-byte read could therefore suppress retry of the same timestamp until a designer saved again. `SelfAuditArchitecture` also hashed only a subset of the Vault lanes, which weakened forensic coverage for the H-Phi proof.
Solution: Move `_csvLastWriteUtc = writeUtc` after `ReadFileIntoNativeScratch` and `ParseCsv`, and treat `read <= 0` as `CsvParseFault` without advancing the timestamp. Failed imports now retry on the next editor/development SlowTick. Clamp file length and solver microsecond telemetry with explicit `long` intermediates instead of mixed-width `math.min` calls. Expand `SelfAuditArchitecture` to include every SHINOBU_114 Vault BufferID: nodes, edges, state flags, oxygen front/back, pressure/yield/reinforcement, AUP/local positions, priority/visited/cell map, counters, tuning, black box, component IDs, pressure front/back, edge remainders, CSR conductance/flow, component specs, and CSV scratch.
Rejected Alternatives: Timestamping before parse was rejected because it hides transient I/O failure. Treating empty input as valid was rejected because it can be a partial-save race and silently clears designer intent. Relying on Unity.Mathematics overload selection for mixed `long`/`int` min was rejected because explicit clamps are clearer and lower compile risk. Adding a managed retry queue was rejected because the path is cold and the timestamp fence is enough. Keeping a partial audit hash was rejected because the final self-audit must identify one owner route for every persistent native lane.
Scalability potential: Low/Middle/High/Ultra runtime math is unchanged; designer iteration is safer because a failed CSV read does not require a manual file touch. Ultra editor previews can rely on the same Vault route proof without widening gameplay DTOs.
Hardware Impact: No hot-path cost. The cold-path branch saves human debugging time and prevents stale balance data from being mistaken for solver failure on weak hardware test passes.

## Decision 009 - Parallel Jacobi Kernel Split

Problem: Task 07 explicitly required `LogisticsFlowSolverJob` to be an `IJobParallelFor`, but the previous implementation kept island BFS, Jacobi sweeps, integer conservation, oxygen solve, and telemetry in one serial `IJob`. That was deterministic, but it weakened the evidence trail and left parallel pressure relaxation on the table.
Solution: Split the solve chain into owner-local stages: `LogisticsFlowPrepareJob` performs serial component BFS into Vault scratch lanes; `LogisticsFlowSolverJob : IJobParallelFor` executes one Jacobi sweep per scheduled job over CSR offsets with `[NoAlias]` read/write buffers; odd sweep counts use `LogisticsPressureCopyJob` to normalize the final result into `PressureFront`; `LogisticsFlowFinalizeJob` runs the serial conservation boundary, breach side effects, oxygen/pressure update, and black-box write. The job chain is dependency-threaded through `JobHandle` without mid-frame `Complete()`.
Rejected Alternatives: Keeping the serial all-in-one job was rejected because it failed the exact Task 07 shape. Parallelizing BFS and final quantization was rejected because component discovery uses queue state and final milli-unit conservation must stay ordered to avoid race-driven drift. Atomic writes from the parallel Jacobi job were rejected; the math now sanitizes every denominator/input and finalization performs the fault sweep without shared parallel mutation.
Scalability potential: Low runs one parallel pressure sweep plus one copy/finalize boundary, preserving 5Hz-ish degraded behavior through cadence. Middle/High/Ultra schedule more independent pressure sweeps using the same ping-pong buffers, buying smoother pipe shader flow without changing DTOs or adding GameObjects.
Hardware Impact: i3/MX350 gains deterministic job-worker distribution for node pressure reads when graph size grows; RTX-class machines can spend 8-10 sweeps on visual smoothness. Exact microseconds remain pending because compile/runtime proof is blocked by active `dotnet` processes and CPU over 50 percent.
