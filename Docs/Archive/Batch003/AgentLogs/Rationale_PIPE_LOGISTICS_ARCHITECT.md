# PIPE_LOGISTICS_ARCHITECT Rationale

Status: PENDING VERIFICATION

## Initialization
Problem: Pipe logistics prompt demands a new Burst pressure graph while construction/Core coupling may already exist.
Solution: Establish file-backed status/rationale before code, then inspect mandates and current systems.
Rejected Alternatives: Chat-only tracking; speculative architecture without repository scan.
Scalability potential: Low uses low-cadence math; Middle/High/Ultra may buy richer visual flow and rupture rendering with saved CPU.
Hardware Impact: 0 us runtime impact; process artifact only for i3/MX350 traceability.

## Solver Isolation
Problem: Fluid pressure math would be rejected if it lived as another Core/Construction monolith.
Solution: Created `Hecton8.Logistics` with only Unity.Mathematics, Unity.Burst, and Unity.Collections references; Core only adapts through `IFluidPipeGraphService`.
Rejected Alternatives: Core-hosted solver, UI-facing pipe callbacks, `PipeManager.Instance`.
Scalability potential: Low/MX350 runs same SOA data at 1Hz; High/Ultra can run 10Hz and spend saved cycles on shader flow/burst visuals.
Hardware Impact: i3/MX350 avoids per-frame managed traversal and keeps pressure math in contiguous native buffers.

## Pressure Transfer
Problem: Equalizing pressure per node independently can create or destroy fluid.
Solution: Solver processes each undirected edge once (`neighbor > nodeIndex`), computes `delta = (PressA - PressB) * flowRate * dt`, subtracts from source, adds to destination.
Rejected Alternatives: Rigidbody/fluid simulation, per-node coroutine diffusion, destination-capacity clamping that hides pressure rupture.
Scalability potential: Low uses the same conservative math less often; Ultra increases cadence without changing determinism.
Hardware Impact: Edge compare/add/subtract path is cache-linear and avoids heap allocations; expected under 0.1 ms for normal base graphs.

## Sinks And Sources
Problem: Pumps, outside vents, O2 sources, and rupture spills intentionally change total graph mass and must be explicit.
Solution: Pump drain, outside node zeroing, source rates, demand rates, and ruptures are separate solver phases after edge conservation.
Rejected Alternatives: Treating every loss as mysterious pressure decay; direct atmosphere mutation inside Burst.
Scalability potential: Low keeps coarse updates visually plausible; High/Ultra gets smoother pressure response.
Hardware Impact: O(node) scalar passes; MX350 cost buys functional pumps instead of fake UI bars.

## Signal Consequences
Problem: Existing pipe rupture directly registered fluid decals from the pipe node.
Solution: Replaced burst visual side effect with `PipeRuptureSignal(AUP)` and `ImpactSignal`; runtime also pushes `FluidIncursionSignal` for water spill.
Rejected Alternatives: Instantiating particles/decals in gameplay logic; direct VFX references in `Hecton8.Logistics`.
Scalability potential: Low consumers can use cheap decals/audio; Ultra consumers can turn the same signal into GPU particle overkill.
Hardware Impact: Burst path writes queue records only; main-thread consumers decide presentation.

## Renderer Flow Fake
Problem: Prompt requires visible pipe flow but physical particles in pipes are frame-time waste.
Solution: Solver writes `PipeFlowVectors`; runtime converts to scalar; BRG stores scalar in instance data; shader pans texture when `MaskHasFluidFlow` is set.
Rejected Alternatives: Per-droplet simulation, new renderer stack, material instancing per pipe.
Scalability potential: Low gets a panning texture lie; Ultra can layer stronger shader/VFX responses on the same flag.
Hardware Impact: One float scalar and one shader branch; no GameObject churn.

## Black Box
Problem: NaN or rupture failures must be reconstructable without chat archaeology.
Solution: Job writes a fixed 300-entry `NativeArray<FluidPipeTelemetryEntry>` and runtime dumps `Dump_PIPE_LOGISTICS_ARCHITECT.bin` on NaN.
Rejected Alternatives: Debug.Log-only crash diagnosis; managed List history.
Scalability potential: Telemetry format is constant across Low/Mid/High/Ultra.
Hardware Impact: One native ring write per solve; no GC.

## Compile Wall
Problem: Generated dotnet Core build fails in other agents' domains before validating this domain.
Solution: Ran isolated Roslyn compile for `Hecton8.Logistics` and Unity batchmode import; Unity produced `Hecton8.Logistics.dll`.
Rejected Alternatives: Editing Bootstrap/Cartography/VFX/Biolum to force a green unrelated build.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime; verification artifact only.

## Omega Polish Changes
Problem: Core work was functionally complete but still required anti-bloat proof and a final pass for hidden visual/runtime debt.
Solution: Reran a targeted `rg` scan against the touched logistics/runtime/render files; no managed `foreach`, `string.Format`, `.ToString()`, interpolation, `math.sqrt`, `math.normalize`, or `.normalized` remained. Shader normalization uses `rsqrt`. Visual-flow gap was closed through existing BRG instance data instead of a duplicate renderer. Runtime black-box dump diagnostics stay outside the Burst path, with editor/development logging only.
Rejected Alternatives: Repo-wide offender cleanup outside domain; new GameObject renderer; CPU particle fluid inside pipes; real droplet simulation for ruptures.
Scalability potential: Low = 1Hz solver plus one panning texture scalar. Middle = 4Hz pressure cadence. High = 10Hz cadence. Ultra = same deterministic solver with stronger rupture/flow consumers on the same signal and BRG flags.
Hardware Impact: i3/MX350 avoids per-frame pressure solving, managed iteration, material instancing, and pipe particles; expensive visuals remain opt-in consumers of cheap signals.

## 2026-05-13 Quality Recheck
Problem: Static review found second-pass defects: atmosphere lookup could repeat from the pipe output path, pump ingress searched all nodes every cadence, flow visuals used net signed transfer instead of total moved volume, pipe-edge capacity had no explicit owner guard, invalid water room indices mapped to room 0, and water ruptures could publish the same spill through both rupture and exchange paths.
Solution: Resolved atmosphere only in cold lifecycle, moved pump routing to owner-registered nodes, added explicit connection-count capacity guard, rejected nonfinite content injection, drove visual flow from accumulated absolute moved volume, throttled unchanged BRG flow submissions with a native last-flow array, ignored invalid room spill targets, and kept rupture water spill ownership on the rupture record.
Rejected Alternatives: Running `dotnet build` against known unrelated broken generated projects; broad GlobalRegistry/Cartography/VFX cleanup; adding another renderer; simulating water drops to make rupture spill visible.
Scalability potential: Low keeps 1Hz pipe solve and avoids repeated scans. Middle/High/Ultra preserve 10Hz-capable visuals while skipping stable-flow renderer updates. Ultra can still consume rupture/flow signals for heavier VFX without changing solver truth.
Hardware Impact: MX350/i3 avoids repeated component lookups, repeated all-node pump scans after cache warmup, redundant BRG link rescans for stable flow, and duplicate spill events that would double downstream VFX/audio work.

## 2026-05-13 Reachability Patch
Problem: Static review found the pressure graph was reachable only through its service API; no current pump or O2 owner registered pipe nodes, and a pump-fed isolated ingress node would just build pressure until rupture.
Solution: `WaterPumpModule` now registers a water ingress node plus a same-network outside outlet node and connects them through `IFluidPipeGraphService`. `SubmarineElectrolysisModule` queues generated oxygen into pipe nodes when the graph is alive, and `FluidPipeGraphRuntime` consumes that queue before scheduling the Burst solve. `TryReadPipeNode` now fails while a solve is scheduled to avoid native-array ownership reads.
Rejected Alternatives: New `PipeManager`, scene-wide scan/bootstrap dependency, direct atmosphere-only electrolysis, or launching `dotnet build` despite the user ban.
Scalability potential: Low keeps coarse 1Hz pump venting with no per-frame owner scans. Middle/High/Ultra can run smoother pipe response while the same module registrations feed richer rupture and flow visuals.
Hardware Impact: i3/MX350 pays cold node registration and one per-active-pump/module loop at solver cadence; it avoids useless isolated pressure buildup, avoids read/write safety hazards during jobs, and keeps the pure Burst solver unchanged.

## 2026-05-13 Lifecycle Hardening
Problem: Owner recache could clear a cached node's `Ruptured` bit, stopped electrolysis could leave demand draining a room pipe after production stopped, and the old generic water-ingress fallback could route pump drainage into the wrong network.
Solution: Cached pump/electrolysis nodes only clear `Disabled`, never `Ruptured`; ruptured cached nodes are abandoned for fresh registration. Electrolysis clears pipe demand when references are invalid, power/water production stops, or generated oxygen is zero. Pump drainage now requires its own confirmed outside outlet connection, and the generic ingress search/cache was removed.
Rejected Alternatives: Treating owner recache as implicit repair; leaving room demand as a passive O2 consumer after generation stops; falling back to any water node in the graph.
Scalability potential: Low avoids hidden pressure churn and stale O2 drains at 1Hz. High/Ultra preserve smoother response without letting rupture state oscillate or cross-network fallback paths appear.
Hardware Impact: i3/MX350 avoids wasted solver work on isolated pump pressure, avoids downstream VFX/audio repeat from revived rupture loops, and keeps pump routing to O(active pumps) without all-node scans.

## 2026-05-13 Demand Ownership And Cold Binding
Problem: Electrolysis could still rely on a SlowTick `GlobalRegistry.FluidPipeGraph` lookup to find the pipe runtime, stale oxygen-source demand cleanup lived partly in module stop paths, and pump pipe-room/network resolution still had a component lookup fallback if cold references were missing.
Solution: `FluidPipeGraphRuntime` now clears oxygen-source demand rates immediately before scheduling each solve, so the graph owner owns stale-demand cleanup while native arrays are writable. The graph runtime binds/unbinds active electrolysis modules when its service slot registers or unregisters; modules cache the graph/ocean services during cold lifecycle and no longer poll `GlobalRegistry.FluidPipeGraph` from `TryQueuePipeOxygen`. Pump adapters cache host/atmosphere references in cold lifecycle and pipe node resolution no longer calls `GetComponentInParent`.
Rejected Alternatives: Leaving demand cleanup only to producer modules; per-SlowTick registry polling as a hidden dependency; repairing lookup misses by scene search; adding a new pipe manager singleton.
Scalability potential: Low keeps 1Hz solve with deterministic stale-demand clearing and no pipe-path scene traversal. Middle/High/Ultra keep the same ownership path while richer flow/rupture visuals remain fed by the existing BRG/signal proxies.
Hardware Impact: i3/MX350 avoids a graph service lookup on electrolysis cadence, avoids pump component traversal during node reuse, and bounds oxygen-demand cleanup to one O(nodeCount) native loop at solve cadence instead of scattered managed owner cleanup.
