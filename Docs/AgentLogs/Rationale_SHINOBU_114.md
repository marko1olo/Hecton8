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
Solution: Edge `Flow01` drives existing pipe shader scalar via `ConnectionSplineBatchRenderer.SetPipeNodeFlow`; telemetry ring records nodes/components/iterations/micros and dumps to `Docs/AgentLogs/Dump_LOGISTICS_SURGEON.bin` on fault.
Rejected Alternatives: Debug GameObjects, particle payloads, or string-only diagnostics. They add GC and do not preserve forensic state.
Scalability potential: Low still shows coarse flow pulses; Middle/High/Ultra increase smoothness through solver iterations and shader scalar density.
Hardware Impact: Removes per-item simulation entirely; ring write is fixed O(1), target below 5 us pending profiler.

## Decision 006 - H-Phi Scratch Eviction Polish

Problem: Ultra-polish audit found private persistent `NativeQueue<int>`, `NativeQueue<MockModuleStateSignal>`, `NativeQueue<HullBreachSignal>`, and `NativeList<int>` in the router. Those allocations were cold, but they still violated the Vault sovereignty rule and created a local signal corridor duplicate.
Solution: Expand the existing `ShinobuLogisticsCounters` Vault int lane with three bounded scratch segments: `BfsQueueBase`, `ReachableOrderBase`, and `BreachNodeBase`. The solver now uses head/tail indices for BFS, writes source-less island order into a contiguous int range, records breached node indices into the same Vault lane, and publishes the existing `FluidIncursionSignal` after the solve. Burst jobs now use `[NoAlias]` and `CompileSynchronously=true`; runtime layout validation checks sizes without reflection, editor validation checks offsets.
Rejected Alternatives: Keeping local native queues/lists was rejected because it creates memory ownership outside GlobalDataVault. Adding new BufferIDs for BFS and breach scratch was rejected because the existing counters lane can hold the bounded int scratch without widening Core.Memory again. Publishing a local breach signal was rejected because `FluidIncursionSignal` already owns the public flood/incursion route.
Scalability potential: Low keeps one Jacobi sweep and sparse oxygen cadence while still isolating islands exactly; Middle/High/Ultra increase convergence and shader flow smoothness without changing ABI. On top-tier hardware the saved CPU path buys denser visual scalar response in pipe shaders rather than physical resource objects.
Hardware Impact: i3/MX350 avoids NativeQueue block churn, local container lifetime tracking, and hash/list traversal; expected gain is static until Unity profiler proof. RTX-class hardware gets cleaner SIMD aliasing input for Burst and smoother high-iteration flow visuals.

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

Payload/data shape: unmanaged DTOs and primitive arrays only. BFS queue, reachable order, and breach node side-effects are int slices inside `ShinobuLogisticsCounters`.
Managed fields present: no.
UnityEngine.Object fields present: no.
Layout proof: `ValidateLayouts` uses `UnsafeUtility.SizeOf` and explicit field offsets.
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
