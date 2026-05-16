# PATH_FUNNEL_NAVMESH_FIXER Rationale

## Decision 001 - Scope And Existing Contracts

Problem: The assigned pathfinding folder did not exist, but Core/World already expose WFC state signals, DataVault buffer IDs, AUP structs, and dispatcher lanes.
Solution: Add a narrow `Assets/_Project/Scripts/AI/Pathfinding/` module instead of editing legacy vendor A* or World/Power owners. Use `SignalBus<WfcOutpostStateChangedSignal>` and `GlobalRegistry.DataVault` as cross-domain interfaces.
Rejected Alternatives: Using `Assets/AstarPathfindingProject` was rejected because AGENTS forbids the legacy A* package. Editing World voxel navgraph internals was rejected because the prompt domain is AI/PATHING and World owns SDF grid generation.
Scalability potential: Low uses short local smoothing windows; Middle/High/Ultra can widen look-ahead while preserving the same data layout.
Hardware Impact: Avoiding managed A* modifiers and `Vector3` path lists prevents hot-path GC and cuts per-corner smoothing to scalar cross products. Estimated low-end gain: 20-80 microseconds per 32-portal path versus angle/managed-list smoothing, pending profiler proof.

## Decision 002 - Mandates Applied

Problem: Funnel smoothing touches AI navigation, dynamic WFC obstruction, native jobs, AUP, signals, and blackbox telemetry.
Solution: Loaded mandates for AI funnel pathing, dynamic navgrid/SDF, zero-GC, native jobs, execution phases, GlobalRegistry DI, telemetry, and AUP determinism.
Rejected Alternatives: Reading unrelated batch prompts or dated reports was rejected; the XML prompt and stable mandates are the authority.
Scalability potential: The same kernel supports Low/Middle/High/Ultra by changing only look-ahead and clearance radius.
Hardware Impact: Cross-product funnel math avoids `acos`, normalized angles, and managed path modifiers. Estimated ALU saving: 30-50 scalar ops per portal, pending Burst inspection.

## Decision 003 - Burst XZ Funnel Kernel

Problem: The path needed smoothing without angle math, managed path lists, or legacy A* package ownership.
Solution: Implemented `FunnelSmoothingJob` as a Burst `IJob` over `NativeArray<NavPortal>` and used the XML-required XZ cross product `(ab.x * ac.z) - (ab.z * ac.x)` for funnel tightening.
Rejected Alternatives: `Vector3.Angle`, `acos`, normalized dot/cross comparisons, Bezier smoothing, and A* project modifiers were rejected because they add ALU, branch noise, and package dependency risk. The AI mandate's 3D funnel warning was noted, but the batch prompt explicitly required XZ cross math for this WFC outpost lane.
Scalability potential: Low = 2-portal look-ahead; Middle = 8 portals; High = 16 portals; Ultra = same stable 16-portal kernel with higher call frequency or larger caller-owned buffers.
Hardware Impact: On i3/MX350-class silicon, removing angle math saves an estimated 30-50 scalar ALU ops per portal and 20-80 microseconds per 32-portal smoothing request compared with managed angle smoothing.

## Decision 004 - WFC Door Invalidation Through Vault And Signals

Problem: Door closures must invalidate only paths that pass through the changed WFC cell without coupling AI to Power or World implementation classes.
Solution: Cached `GlobalRegistry.DataVault`, read `BufferID.WfcOutpostGrid`, consumed `SignalBus<WfcOutpostStateChangedSignal>`, and tracked each active path with an exact 500-bit cell mask.
Rejected Alternatives: Direct WFC registry references, broad sector invalidation, and path owner polling were rejected. Broad invalidation is cheaper to write but too destructive for AI behavior and wastes replans.
Scalability potential: Low = 128 tracked paths and 64 invalidations; Middle/High = raise serialized capacities; Ultra = same bitmask path can be duplicated per crowd lane without changing signal contracts.
Hardware Impact: Bit testing one `ulong` per candidate path saves an estimated 20-80 microseconds per door event versus scanning corridor cell arrays on i3/MX350-class hardware.

## Decision 005 - AUP And Native SOA Layout

Problem: Path smoothing must not accumulate world-space float drift and must not allocate managed waypoint lists.
Solution: Kept math in sector-local `float3`, wrote `NativeArray<float3> Waypoints`, and optionally converted final waypoints to `AbsoluteUniversePositionBlit`.
Rejected Alternatives: World-space `Vector3`, managed `List<Vector3>`, and automatic `NativeList` growth were rejected because they hide allocation and rebase costs.
Scalability potential: Low = small caller-owned waypoint buffers; Middle = larger buffers for dense interiors; High/Ultra = same ABI with extra AUP output for deterministic remote consumers.
Hardware Impact: Avoiding managed lists and world-float rebasing saves an estimated 10-30 microseconds per long path and prevents GC spikes on low-end hardware.

## Decision 006 - SDF And Radius Corner Guard

Problem: String pulling can cut corners through narrow doors or SDF-eroded obstacles if portal clearance is not represented.
Solution: Added `NavPortal.ClearanceMeters` as a pre-eroded SDF clearance lane and clamped portals when clearance or portal width is below `AgentRadiusMeters`.
Rejected Alternatives: Raw SDF texture sampling inside AI was rejected because World owns SDF generation and metadata. Post-smooth collision repair was rejected because it hides the error until after the path is already published.
Scalability potential: Low = radius and clearance clamp only; Middle = caller supplies better per-portal clearance; High = denser portal generation; Ultra = visual overkill comes from more candidate paths, not heavier per-portal math.
Hardware Impact: Early clamp costs a few scalar ops and avoids 10-40 microseconds of late corner repair or replan churn on i3/MX350-class hardware.

## Decision 007 - Schedule Window And Homeostasis

Problem: Funnel jobs must not serialize simulation by forcing completion in the same frame, and stressed hardware must reduce path smoothing cost.
Solution: Added `PathFunnelSchedule.SchedulePreSimulation` and `TryReadPostSimulation`; readback refuses to call `Complete()` until the handle is already done. `Stressed` forces one-portal look-ahead.
Rejected Alternatives: Synchronous job completion and fixed 16-portal smoothing were rejected because frame-time spikes matter more than perfect smoothing under stress.
Scalability potential: Low = look-ahead 2; stressed = 1; Middle = 8; High/Ultra = 16 with non-blocking readback.
Hardware Impact: Avoiding forced completion prevents estimated 50-300 microsecond sync spikes. Stressed one-portal mode saves 20-60 microseconds per long path.

## Decision 008 - Black Box Telemetry

Problem: Door-driven invalidation failures need a deterministic last-frames record without managed log spam.
Solution: Added a 300-frame `NativeArray<PathFunnelTelemetryEntry>` ring with `PathInvalidationCount`, last sector/path/cell, active count, invalidated count, and binary dump path `Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin`.
Rejected Alternatives: `Debug.Log` per invalidation and unbounded event history were rejected for hot-path GC and noise.
Scalability potential: Low = same 300-frame ring; Middle/High/Ultra = larger runtime capacities can be serialized without changing telemetry entry format.
Hardware Impact: One struct write per late frame is estimated below 1 microsecond; dump allocation only happens on explicit crash/NaN request, outside the hot path.

## Decision 009 - Build Wall

Problem: Validation cannot reach a clean project build because Core currently fails before pathfinding diagnostics matter.
Solution: Restored project assets, ran `Assembly-CSharp.csproj`, then isolated `Hecton8.Core.csproj` with errors-only logging. The isolated Core log reports 110 errors and 0 `PathFunnel`/`AI\Pathfinding` matches.
Rejected Alternatives: Editing Fauna, Voxel, Bootstrap, GlobalSignals, or Core assembly references was rejected as cross-domain work outside AI/PATHING. Reporting a green build was rejected because the objective data says otherwise.
Scalability potential: Once Core is repaired, the new `Hecton8.AI.Pathfinding.asmdef` keeps pathfinding isolated and lets this module validate as its own assembly instead of bloating Core.
Hardware Impact: No runtime hardware impact. Build blockage is integration debt, not a frame-time choice.
