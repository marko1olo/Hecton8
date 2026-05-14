# OUTPOST_LOGISTICS_INITIALIZER Rationale

Status: `PENDING VERIFICATION`

## Decision 0 - Decoupled Boot Boundary

Problem: WFC outpost generation and logistics power must integrate while 20+ agents are editing adjacent domains. The initial local scan note that no source-backed outpost generator existed was stale; `MarauderOutpostGenerationService` is present and now provides the handoff.
Solution: Keep the boundary signal-driven anyway: `MarauderOutpostGenerationService` registers the native WFC grid through `WfcOutpostGridRegistry` and broadcasts `WfcOutpostGeneratedSignal`; the power boot consumes only contracts and the registry handle.
Rejected Alternatives: Direct concrete references from power runtime to generator internals, scene searches, or cross-domain polling were rejected because they would violate the parallel-agent rule and create load-order fragility. Physics overlap was rejected because the prompt requires WFC logical adjacency.
Scalability potential: Low uses the 500-cell logical grid only; Middle can add door/light response; High and Ultra can spend saved CPU on richer brownout flicker, sparks, cable emissive response, and diegetic hologram distress without changing power truth.
Hardware Impact: Avoids 500-cell collider adjacency scans on i3/MX350; estimated cold-generation saving is 250-900 us versus overlap-based module discovery.

## Decision 1 - Native Graph Translation

Problem: Generated modules need a power network without MonoBehaviour `PowerNode` objects or managed adjacency lists.
Solution: Build SOA node records and a `NativeParallelMultiHashMap<int,int>` edge set from the 10x10x5 logical grid using a Burst-compatible `IJob`.
Rejected Alternatives: GameObject components, `List<PowerNode>` neighbor links, and `Physics.OverlapSphereNonAlloc` were too tied to presentation and authored-base topology.
Scalability potential: Low uses one node per active cell; Middle/High/Ultra can add visual-only cable arcs, stronger emissive flicker, and localized sound while the same compact graph remains authoritative.
Hardware Impact: Fixed 500-cell scan is predictable; expected cold-path translation remains under 150 us on target silicon after persistent buffers exist.

## Decision 2 - Contracts and Assembly Isolation

Problem: The WFC generator, power runtime, and logistics job need shared grid constants without creating an outpost-to-power concrete dependency loop.
Solution: Add `Hecton8.Logistics.Grid.Contracts` for constants, descriptor, count slots, fault flags, and `WfcOutpostPowerNode`; add `Hecton8.Logistics.Grid` for the Burst graph translation job.
Rejected Alternatives: Duplicated byte constants in outpost and power code, or adding the job to `Hecton8.Core`, were rejected because drift would corrupt cell meaning and compile scopes would keep expanding.
Scalability potential: Low has one stable byte layout; High/Ultra can add flags for presentation density without modifying graph ownership.
Hardware Impact: Runtime cost is 0 us; compile/import isolation reduces accidental dependency churn.

## Decision 3 - Generator Cell and Dying Source

Problem: The outpost can be fully generated but logically dead unless one cell is a known power source with predictable failure behavior.
Solution: Mark the center floor cell as `Generator`, inject producer capacity at 5%, decay by `0.01 / 60` per second, and flag missing-generator fallback as a graph fault if external data arrives without the marker.
Rejected Alternatives: Random generator placement was rejected because mission fail-safe logic needs deterministic reachability. A MonoBehaviour reactor was rejected because the task requires SOA node data.
Scalability potential: Low receives a single fading source. Middle can use warning lights. High can layer per-room flicker, audio relays, and hologram corruption. Ultra can add dense visual sparks while the scalar source remains the truth.
Hardware Impact: One scalar decay update per slow tick; no per-frame simulation.

## Decision 4 - Door and Brownout Coupling

Problem: Doors must start locked and become usable only when their WFC node has voltage, while brownout presentation belongs to other systems.
Solution: Lock `SealedDoor` proxies on spawn; publish `WfcOutpostDoorPowerSignal` with AUP, grid handle, cell index, voltage, and unlocked flag; publish `BrownoutSignal` when reactor output falls below 2%.
Rejected Alternatives: Direct calls from power runtime into door components or light controllers were rejected as cross-domain coupling and lifetime risk.
Scalability potential: Low uses one typed signal and binary lock state. Middle adds basic flicker. High/Ultra can let holograms, lights, audio, and decals react independently to the same brownout severity.
Hardware Impact: O(door count) signal handling, bounded by `MaxInteractables`; no scene search.

## Decision 5 - Gas Solver Boundary

Problem: WFC rooms must start at 5% O2 without making power own gas dynamics internals.
Solution: Cache `IGasDynamicsSolver` through `PowerGridManager` registration and hot-swap callback; seed room partial pressure once when both graph and gas runtime are ready, and force local scrubbers unpowered.
Rejected Alternatives: Direct gas runtime class references, trigger colliders, or transform-room searches were rejected because gas is a separate authority domain.
Scalability potential: Low uses one-time room seeding. Middle/High can have gas solver run richer diffusion or visual breath/haze response. Ultra can add denser suffocation presentation without changing WFC power topology.
Hardware Impact: O(room count) once per grid or gas availability; 0 B hot path by code review.

## Decision 6 - Legacy Overlap Boundary

Problem: The prompt says to eradicate overlap adjacency for generated base modules, but legacy authored-base `PowerNode` still uses `Physics.OverlapSphereNonAlloc` for player-built topology.
Solution: Remove overlap from the WFC path by using logical grid edges. Leave legacy authored-base overlap untouched because it is outside this generated-outpost boot and currently owns construction topology.
Rejected Alternatives: Removing or rewriting `PowerNode.FindAndConnectNeighbors()` in this batch was rejected as an unscoped regression risk to player construction and relay visuals.
Scalability potential: Low WFC outposts are native-grid only. High/Ultra authored bases can later migrate under a separate power-routing batch with presentation upgrades.
Hardware Impact: WFC generation avoids overlap scans. Legacy overlap remains a known debt outside this task.

## Decision 7 - Cadence Gate and Math LOD

Problem: The dispatcher slow tick runs faster than the reactor changes; reevaluating the WFC graph every 0.1 s wastes budget on low-end machines.
Solution: Force one immediate graph evaluation after WFC generation, then cadence-gate runtime power evaluation to 1 Hz.
Rejected Alternatives: Evaluating every dispatcher slow tick was rejected because the source decays 1% per minute and doors only need bounded slow-state updates.
Scalability potential: Low runs the 1 Hz authoritative graph. Middle/High/Ultra can spend the saved cycles on visual response density instead of invisible numeric churn.
Hardware Impact: Saves roughly 9 graph evaluation schedules per second per active outpost versus the un-gated slow tick path.

## Decision 8 - Black Box and Verification Wall

Problem: Outpost power failure must leave evidence, but Unity compile/playmode proof is unavailable in the current environment.
Solution: Add a 300-entry native telemetry ring and binary dump on fault/NaN. Capture dotnet build output to `Docs/AgentLogs/Build_OUTPOST_LOGISTICS_INITIALIZER_dotnet.log` and keep status `PENDING VERIFICATION`.
Rejected Alternatives: Chat-only reporting, naked `Debug.Log`, or claiming Unity verification without console/profiler logs were rejected.
Scalability potential: Low writes compact numeric state. High/Ultra debugging can correlate this with richer VFX telemetry without increasing gameplay truth cost.
Hardware Impact: One 64-byte entry per graph evaluation; bounded memory and no unbounded logs.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass required checking whether any honest math, hot string formatting, managed `foreach`, or excessive cadence remained in the WFC power boot.
Solution: Parsed `OMEGA_POLISH` from the original batch git object after all core tasks were checked or blocked. Scoped `Select-String` audits over WFC-owned runtime/registry/job/contracts and touched bridge files found no `foreach`, `string.Format`, `$"`, `.ToString(`, `math.sqrt`, `math.normalize`, `Mathf.Sqrt`, or `Vector3.Normalize`. Replaced the reactor decay division with a precomputed reciprocal multiply and retained the 1 Hz graph-evaluation cadence gate.
Rejected Alternatives: Broad third-party/project-wide churn was rejected because it would edit outside ECHELON 6 logistics power ownership. Keeping dispatcher-rate graph evaluation was rejected because it spends invisible CPU while the reactor decays at 1% per minute.
Scalability potential: Low/Middle run one authoritative graph evaluation per second after the immediate generation evaluation. High/Ultra can spend the saved cadence budget on brownout presentation density through existing `BrownoutSignal` consumers.
Hardware Impact: Removes one runtime division expression from the reactor decay constant path and avoids roughly 9 graph scheduling opportunities per second per active outpost versus the pre-polish cadence.

Final Git Diff:
- `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs`: added `SecondsToMinutesReciprocal`, changed reactor decay to multiply by the reciprocal, added `PowerEvaluationIntervalSeconds`, added `_nextGraphEvaluationTime`, and force-immediate/cadence-gated graph evaluation.
- `Docs/Tasks/Status_OUTPOST_LOGISTICS_INITIALIZER.md`: replaced pending checklist with 5-loop evidence status and dependency blocks.
- `Docs/AgentLogs/Rationale_OUTPOST_LOGISTICS_INITIALIZER.md`: corrected stale generator scan claim and added decision records plus this polish entry.
- `Docs/AgentLogs/Build_OUTPOST_LOGISTICS_INITIALIZER_dotnet.log`: captured build wall; `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 on missing generated/temp metadata assemblies.
