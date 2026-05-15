# Rationale HABITAT_O2_SCRUBBER_LOD

STATUS: PENDING VERIFICATION

## Initial Decision Record
Problem: Gas and power diffusion for unoccupied bases risks wasting CPU on i3/MX350 when bases are far from the player.
Solution: Implement hibernation as SOA state in native buffers, gate Burst jobs by awake byte mask, and apply analytical catch-up on wake.
Rejected Alternatives: Per-compartment simulation while far away rejected because it burns frame budget for invisible state. Coroutine wake polling rejected because gameplay hot-path scheduling must use dispatcher/tick cadence. Managed dictionaries rejected because global state belongs in native SOA storage.
Scalability potential: Low = 150m hibernation threshold and scalar catch-up. Middle = 500m threshold and standard leak decay. High = longer awake residency and richer telemetry. Ultra = use saved CPU for visual overkill outside this gas solver, not for particle gas truth.
Hardware Impact: Estimated low-end gain is proportional to sleeping base count; each hibernating base avoids recurring gas/power job work except FrostTick distance checks. Exact microseconds remain PENDING VERIFICATION without Unity profiler logs.

## Mandate Bindings
- CORE_Abyss_Survival_Systems_O2_Pressure_Logic: Dalton-law compartments must remain deterministic and gameplay correct.
- LOGI_Energy_Networks_Power_Grid_Graph_Flow: battery drain catch-up must clamp, never underflow, and preserve graph truth.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: base distance checks must use AUP data, not transform positions.
- ARCH_Global_Registry_ServiceLocator_DI_Init: cross-domain dependencies must be cached outside hot paths.
- OPT_Native_Memory_Collections_JobSystem_Protocol: native buffers must have explicit ownership and disposal.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no managed allocation in tick/job/catch-up paths.
- DBG_Telemetry_Crash_Reporting_PostMortem: hibernation transitions need black-box state hooks if the existing domain supports them.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: analytical catch-up is the required deterministic lie, replacing thousands of invisible diffusion iterations.

## Decision 1 - Signals Instead Of Concrete Habitat Hooks
Problem: Base entry/exit ownership is outside the gas solver and other agents may rewrite airlocks/modules.
Solution: Added typed unmanaged `PlayerBaseEnterSignal` and `PlayerBaseExitSignal` lanes, then consumed frame snapshots from `GasDynamicsSolver.Tick` with FixedTick/FrostTick post-step safety drains.
Rejected Alternatives: Direct gas-side `BaseAirlock` or `BaseModule` polling was rejected because it would violate batch parallelism and create fragile compile dependencies. C# events were rejected because managed delegates are the wrong channel for a zero-GC runtime signal.
Scalability potential: Low/MX350 pays only empty snapshot-count checks in normal frames. Middle keeps 500m hibernation. High/Ultra can spend saved gas CPU on richer alarm/HUD presentation rather than more gas truth.
Hardware Impact: Estimated transition batches remain cheaper than managed observer fanout; exact profiler proof absent.

## Decision 2 - Awake Mask As Native SOA
Problem: A far base should not keep paying O(room + edge) gas and power solve cost.
Solution: Added `NativeArray<byte> BaseAwakeState`, `NativeArray<int> _roomBaseIndex`, and base metadata arrays. Gas Burst work checks the byte mask before metabolism and diffusion.
Rejected Alternatives: Managed base objects, dictionaries, and per-room MonoBehaviour flags rejected for cache locality and GC risk. Full compartment simulation while invisible rejected by Cinematic Cheat protocol.
Scalability potential: Low = 150m sleep threshold. Middle = 500m threshold. High = same authority with longer visual residency outside this solver. Ultra = saved cycles buy presentation overkill, not particle gas.
Hardware Impact: Model saves 20-80us per 64-room sleeping base per gas cadence on i3/MX350; measured proof absent.

## Decision 3 - ASMDEF Isolation Blocked Honestly
Problem: The prompt asks for `Hecton8.Habitat.Atmosphere -> Contracts`, but `GlobalRegistry` currently stores concrete `HectonSurfaceWeatherDirector` and `HectonAtmosphereManager` types from the Atmosphere namespace.
Solution: Expanded the existing Core contract seam with hibernation snapshots/config methods and did not add a fake asmdef that would not isolate the live runtime. Marked the task blocked for integrator work.
Rejected Alternatives: A decorative `Contracts.asmdef` marker was rejected as false compliance. Moving all Atmosphere scripts under a new asmdef was rejected because Core would require a reference back to the new assembly while Atmosphere already needs Core.
Scalability potential: Once integrator splits concrete weather/atmosphere registry slots to interfaces, GasDynamics can move to a true Habitat.Atmosphere assembly without behavior churn.
Hardware Impact: No runtime gain yet; this is a compile-bound architecture dependency.

## Decision 4 - AUP FrostTick Hibernation
Problem: Transform-space distance checks break under floating origin and per-frame culling would burn the savings.
Solution: Cached `IPlayerMovementContracts` and `ITickDispatcher`, converted runtime player position to AUP, stored base centers in `NativeArray<AbsoluteUniversePosition>`, and evaluated distance only on FrostTick with 25m hysteresis.
Rejected Alternatives: `transform.position` as authority rejected by AUP mandate. Immediate threshold flipping rejected by state hysteresis mandate. Per-frame checks rejected because 5-second cadence is enough for base hibernation.
Scalability potential: Low/MX350 sleeps at 150m. Mid/High/Ultra sleeps at 500m while keeping the same deterministic wake path.
Hardware Impact: Expected <5us per 32 bases per FrostTick; removes recurring gas/power job work while far away.

## Decision 5 - Gas And Power Suspension By Mask
Problem: Sleeping a base in the atmosphere solver alone still leaves diffusion/power graph jobs able to spend cycles on invisible state.
Solution: Gas Burst work checks `BaseAwakeState` per room and per bulkhead. Logistics power graphs gained a read-only base-awake binding; WFC outpost power boot binds to gas base 0 when available.
Rejected Alternatives: Stopping MonoBehaviours, disabling graph state, or deleting node data rejected because wake needs stable native state and other agents may own construction/power boot lifetimes.
Scalability potential: Low gets aggressive sleep and skipped Jacobi work. Middle/High/Ultra keep richer graph state but skip physics truth while the player cannot inspect it.
Hardware Impact: Model saves 20-80us per 64-room gas base and avoids power Jacobi slices for bound outposts; measured profiler proof absent.

## Decision 6 - Burst Analytical Catch-Up
Problem: Waking a base after long absence must preserve believable O2/power consequences without replaying thousands of missed ticks.
Solution: Added `BaseHibernationWakeCatchUpJob` with Burst `math.exp` decay, dispatcher-time elapsed seconds, watt-second battery drain, and O2 depletion when the battery reaches zero.
Rejected Alternatives: Replaying historical gas steps rejected by frame-time dictatorship. Pure linear decay rejected because it requires hard clamp tuning and diverges from the prompt formula. Coroutine/timer catch-up rejected because gameplay cadence belongs to dispatcher lanes.
Scalability potential: Low executes one O(room) catch-up. Middle keeps the same physical lie at 500m. High/Ultra can use saved frames for stronger UI/lighting warnings outside this solver.
Hardware Impact: Replaces O(room * skippedTicks) with O(room) on wake; one-day hibernation avoids up to 17280 FrostTick-equivalent replays.

## Decision 7 - H-Phi Awake State Ownership
Problem: The prompt specifically requires `BaseAwakeState` in GlobalDataVault, but the existing gas solver still owns most Dalton arrays locally.
Solution: Added `BufferID.HabitatBaseAwakeState` and `SystemID.HabitatAtmosphere`, then requested only the hibernation awake mask from `IDataVault`. Local fallback exists only for scenes where the vault is not registered yet.
Rejected Alternatives: Moving every gas array to the vault in this pass rejected because it broadens ownership and disposal risk beyond the task. Local-only awake state rejected because H-Phi sovereignty was explicit.
Scalability potential: Low can share the byte mask with power/culling without duplicating state. High/Ultra can alias the same state into richer diagnostics.
Hardware Impact: No direct microsecond gain; removes future copy cost for cross-system awake-state consumers.

## Decision 8 - Verification Boundary
Problem: The final task asks for Burst compile verification of `math.exp`, while the user explicitly ordered no dotnet rebuilds and no Unity MCP compiler is exposed in this session.
Solution: Put the formula inside a `[BurstCompile]` `IJob` and marked compile verification blocked rather than claiming proof from static source.
Rejected Alternatives: Fake "Burst verified" report rejected. Running dotnet rebuild rejected by user instruction. Adding a decorative compile probe rejected unless it affects the real wake path.
Scalability potential: Once Unity compilation is allowed, the same job validates Low through Ultra because it is the shared wake path.
Hardware Impact: Pending real Burst console/profiler data; static model remains O(room), 0 B GC.

## Self-Review 4 - Binding And Catch-Up Corrections
Problem: The first implementation used a main-thread helper for `math.exp` and a power graph binding that expected a writable NativeArray, while the public gas contract exposes a read-only alias.
Solution: Moved the wake formula into `BaseHibernationWakeCatchUpJob` and changed `LogisticsNetworkGraph.TryBindBaseAwakeState` to accept `NativeArray<byte>.ReadOnly`. WFC power boot now clears stale binding when gas exists but has not initialized its mask.
Rejected Alternatives: Exposing writable `BaseAwakeState` through `IGasDynamicsSolver` rejected because external systems must not mutate the hibernation authority. Leaving `math.exp` outside Burst rejected because it weakened task 15.
Scalability potential: Low keeps a single O(room) wake pass; High/Ultra keep identical authority and can layer visuals elsewhere.
Hardware Impact: Same O(room), 0 B model; reduced integration risk for power graph consumers.

## Self-Review 5 - Hot-Path Dependency Hygiene
Problem: The hibernation FrostTick initially resolved quality tier through `GlobalRegistry` and the solver attempted to configure signal lanes that GlobalSignals already owns.
Solution: FrostTick now reads cached `_lastMathLod` from the fixed-step cadence resolver, and the solver only ensures the base signal lanes exist without overwriting their lane hash/capacity.
Rejected Alternatives: Keeping registry reads in FrostTick rejected by dependency-injection mandate. Reconfiguring signal lanes from the consumer rejected because GlobalSignals is the lane owner.
Scalability potential: Low tier still receives 150m hibernation through `_lastMathLod`; High/Ultra keep the standard threshold.
Hardware Impact: Removes a registry touch from FrostTick and avoids accidental large-lane/hash churn; microsecond gain is negligible but risk is lower.

## Self-Review 6 - Static Integration Audit
Problem: The user requested continued professional recheck without dotnet rebuilds, so the remaining useful work was static integration risk reduction, not churn.
Solution: Re-read the modified gas solver, global signal payloads, H-Phi buffer identifiers, power graph jobs, and WFC binding. Verified by source scan that `ReadOnlySpan<T>` signal snapshots and `NativeArray<T>.ReadOnly` job fields already exist elsewhere in the project, and that `AbsoluteUniversePosition` is 48 bytes, leaving the 64-byte base transition signals correctly packed.
Rejected Alternatives: Adding another abstraction or expanding vault ownership was rejected because the code already meets the prompt surface and further uncompiled movement would be risk-positive. Running dotnet rebuild remains rejected by explicit user instruction.
Scalability potential: Low keeps only a byte mask and O(room) wake catch-up. Middle/High/Ultra keep the same native authority and can spend saved cycles in presentation systems.
Hardware Impact: No new microsecond claim; this pass reduced compile/integration risk only. Last touched-file whitespace check exits 0 with CRLF warnings only.

## Self-Review 7 - H-Phi Identifier And Cold Allocation Hygiene
Problem: Continued audit found two non-frame defects: the gas solver was initializing base signal queues as a consumer, and the habitat awake buffer needed append-only ID placement to avoid H-Phi identifier churn.
Solution: Removed consumer-side `SignalBus<PlayerBaseEnterSignal>` / `SignalBus<PlayerBaseExitSignal>` initialization from the gas solver, leaving `GlobalSignals` as lane owner. Kept `HabitatBaseAwakeState` at append-only `BufferID` 63. Also hardened cold `TryConfigureBase` remapping and aligned the black-box dump filename with the agent ID.
Rejected Alternatives: Leaving the solver to allocate signal lanes was rejected because empty snapshot reads are already safe and owner-side lane configuration is cleaner. Reusing a low occupied buffer value was rejected because H-Phi IDs must be stable. Running a rebuild remains rejected by user instruction.
Scalability potential: Low avoids two unnecessary cold signal allocations in scenes without base transitions. Middle/High/Ultra keep deterministic hibernation authority and stable H-Phi aliasing.
Hardware Impact: Cold allocation reduction only; no new frame-time microsecond claim. Room remap fix prevents stale sleeping-base masks from incorrectly gating rooms after procedural base reconfiguration.

## Self-Review 8 - Transition Producer Bridge
Problem: The gas solver consumed player base transition signals, but no live producer emitted them, leaving hibernation wake override dependent on direct interface calls.
Solution: Added a narrow producer in `BaseModule`, the class already owning player interior enter/exit truth. It publishes typed `PlayerBaseEnterSignal` and `PlayerBaseExitSignal` with AUP center, frame, room id when resolvable, and base id 0 until multi-base construction identity exists.
Rejected Alternatives: Directly calling `IGasDynamicsSolver.TrySetBasePlayerInside` from `BaseModule` was rejected because it would create a concrete runtime dependency from gameplay modules into atmosphere authority. Publishing from `BaseAirlock` alone was rejected because module trigger enter/exit is the broader interior truth.
Scalability potential: Low tier wakes the single base through one queued unmanaged signal. Middle/High/Ultra can later replace base id 0 with construction graph base identity without touching the gas solver.
Hardware Impact: Trigger-only signal cost, not a frame path. It closes the decoupled wake path and avoids polling modules from the gas solver.

## Self-Review 9 - Same-Frame Handoff Semantics
Problem: If a player moved between two module triggers in one signal frame, processing all enter packets before all exit packets could leave `_basePlayerInside` false.
Solution: Gas now drains exit packets first and enter packets second. Enter wins for same-frame handoffs, keeping the base awake while any interior enter signal exists.
Rejected Alternatives: Adding a managed per-base occupant set was rejected as unnecessary GC-risk and larger architecture. Adding another native room-occupancy map was rejected until multi-base identity exists.
Scalability potential: Low through Ultra share the same deterministic ordering with no additional buffers.
Hardware Impact: No measurable cost change; same packet count, safer final state.

## Self-Review 10 - Native Inside Count
Problem: Same-frame ordering is not enough if Unity delivers an enter on one physics step and the matching old-module exit on the next step.
Solution: Added `_basePlayerInsideCount` as a native per-base SOA lane. Signal enter increments, signal exit decrements with clamp to zero, and `_basePlayerInside` is derived from count > 0. Direct API writes remain authoritative and reset the count to either 0 or at least 1.
Rejected Alternatives: Managed hash sets of module references rejected for GC and lifetime complexity. Polling active `BaseModule` instances from gas rejected because it violates domain decoupling.
Scalability potential: Low stores one int per base. Middle/High/Ultra can later replace base id 0 with construction base ids while preserving the same count lane.
Hardware Impact: Four bytes per base and one integer op per transition packet; no frame-path cost.

## Self-Review 11 - H-Phi Audit Boundary
Problem: The user requested continued H-Phi improvement, but moving additional gas arrays into DataVault without generation handles, job ownership review, and compile proof would broaden risk.
Solution: Ran the source-only H-Phi summary script. It completed on the longer retry and reports RuntimeHPhiRisk 0.000573574, DataSovereignty 0.021057035, DataVaultRefs 151, NativeArrayRefs 7020. Kept the implemented migration scoped to the cross-system awake mask.
Rejected Alternatives: Migrating all Dalton pressure arrays to DataVault in this pass was rejected because those arrays are swapped by jobs and require a wider generation/alias contract. Claiming H-Phi improvement from the timed-out first script run was rejected.
Scalability potential: Current H-Phi win is narrow but real: power and gas share the awake mask without copies. Full gas-array sovereignty is a separate architecture pass.
Hardware Impact: No additional runtime impact from the audit. Avoided an unverified high-risk memory ownership churn.

## Self-Review 12 - Frame Snapshot Drain Reliability
Problem: `GlobalSignals` snapshots are flushed and cleared every frame, while `IFrostTickable` runs on a slower cadence. A base transition could be published and cleared before `GasDynamicsSolver.FrostTick` observed it.
Solution: Registered the gas solver as `IUpdatable` and switched transition draining to `SignalBus.TryReadFrame` so the per-frame dispatcher path consumes each packet once. If a gas step is running, the signal still updates inside-count state but wake catch-up is deferred until the step is complete.
Rejected Alternatives: Keeping FrostTick-only signal consumption was rejected after reading `SystemDispatcher.Update` ordering. Polling `BaseModule` from gas was rejected for domain coupling. Forcing job completion on transition was rejected because base entry should not introduce a sync stall.
Scalability potential: Low through Ultra pay two snapshot count checks per frame and packet work only when transitions exist.
Hardware Impact: Negligible frame cost in the empty case; avoids lost wake events without blocking the job system.

## Self-Review 13 - Deferred Re-enable Dependency Cache
Problem: If `OnEnable` exits early while deferred native disposal is still finalizing, the next tick can create native state before `_dataVault`, `_tickDispatcher`, and movement contracts are recached.
Solution: Tick, FixedTick, and FrostTick now refresh cold dependencies before `EnsureNativeState` whenever the solver is not initialized.
Rejected Alternatives: Polling dependencies every frame was rejected. Moving fallback buffers into DataVault after allocation was rejected because ownership migration during live jobs is a separate contract.
Scalability potential: Low through Ultra preserve the H-Phi `BaseAwakeState` path after re-enable instead of accidentally taking the local fallback.
Hardware Impact: One cold registry refresh on uninitialized re-entry only; no steady-state frame cost.

## Self-Review 14 - Cold Registry Publication
Problem: After deferred native disposal finalizes, Tick or FrostTick can be the first lane to recreate native state. Before this pass, registry publication still waited for FixedTick.
Solution: Tick and FrostTick now seed standard atmosphere and call `TryRegisterRegistry` immediately after `EnsureNativeState`, matching the OnEnable/FixedTick publication contract.
Rejected Alternatives: Waiting for the next FixedTick was rejected because paused or highly dilated frames can leave `IGasDynamicsSolver` invisible despite valid native state. Registering before seeding was rejected because consumers could observe zeroed room pressure.
Scalability potential: Low through Ultra keep identical native buffers; this only closes a lifecycle visibility gap.
Hardware Impact: Two steady-state guarded calls in Tick/FrostTick; cold-start correctness gain only, no runtime microsecond saving claimed.

## Self-Review 15 - Wake Catch-Up Capacity Guards
Problem: The Burst wake catch-up path read base metadata lanes and front/back gas lanes assuming every native array stayed capacity-aligned forever.
Solution: `ApplyBaseWakeCatchUp` now refuses to schedule unless every base metadata array exists, and `BaseHibernationWakeCatchUpJob` clamps base and room limits across all arrays it touches before reading.
Rejected Alternatives: Trusting constructor symmetry was rejected because future H-Phi/DataVault migration may split ownership or resize lanes independently. Adding managed assertions was rejected because this is runtime fault containment, not editor-only diagnostics.
Scalability potential: Low through Ultra keep the same O(room) wake cost; the extra min checks run only in the cold wake job.
Hardware Impact: Negligible cold-path arithmetic cost. Prevents an out-of-bounds wake fault if future low-end memory pressure or owner migration creates capacity skew.

## Self-Review 16 - Main Step SOA Capacity Clamp
Problem: The main Dalton `GasDynamicsStepJob` still used `RoomO2Front.Length` and `BulkheadRoomA.Length` as implicit capacity authorities for other SOA lanes.
Solution: The job now clamps `roomLimit` across all required room lanes, clamps `bulkheadLimit` across all bulkhead lanes, and guards the telemetry ring write.
Rejected Alternatives: Adding runtime managed validation before scheduling was rejected because it would duplicate the Burst-side safety and add main-thread code. Assuming all arrays stay aligned forever was rejected because the H-Phi roadmap pushes more lanes toward shared ownership.
Scalability potential: Low through Ultra keep the same per-room/per-edge work; the extra length mins run once per gas step.
Hardware Impact: Negligible per-step scalar cost. Prevents bad memory access if future DataVault/native ownership changes desynchronize capacities.

## Self-Review 17 - Telemetry Cursor Live Length
Problem: After adding a job-side telemetry bounds guard, the scheduler still advanced `_telemetryWriteIndex` modulo the compile-time `TelemetryCapacity`.
Solution: `ScheduleStep` now computes the write index and next cursor from `_telemetryRing.Length` when the ring exists.
Rejected Alternatives: Relying on the job guard to silently drop telemetry was rejected because the black-box ring is crash evidence, not optional garnish.
Scalability potential: Low through Ultra keep the same 300-frame black box by default; future ring-size changes remain safe.
Hardware Impact: One live-length branch per scheduled gas step. No frame-time saving claimed.

## Self-Review 18 - Toxicity Queue Schedule Guard
Problem: `GasDynamicsStepJob` carries a `NativeQueue<ToxicitySignal>.ParallelWriter`, but `ScheduleStep` only checked `RoomO2` before building the job.
Solution: The scheduler now refuses to run the gas step unless `_toxicitySignals` is created.
Rejected Alternatives: Letting the job discover a missing queue was rejected because a `NativeQueue` writer has no safe in-job fallback. Removing toxicity emission from hibernating builds was rejected because player CO2/narcosis feedback is domain-critical.
Scalability potential: Low through Ultra keep the same queue and toxicity semantics; this only preserves the invariant required by the job.
Hardware Impact: One boolean check per attempted gas schedule. Prevents an invalid native writer path after partial disposal or future owner migration.

## Self-Review 19 - H-Phi Audit Ownership Accuracy
Problem: `TryGetNativeMemoryAudit` counted `BaseAwakeState` as local gas memory even when the buffer was owned by GlobalDataVault.
Solution: The audit now skips `BaseAwakeState` in local allocation totals when `_baseAwakeVaultOwned` is true.
Rejected Alternatives: Adding a new audit field for vault-owned bytes was rejected because the public contract would widen without compile proof. Counting it twice was rejected because it hides the H-Phi ownership improvement.
Scalability potential: Low through Ultra get accurate memory accounting for the shared awake mask.
Hardware Impact: No runtime saving; one cold audit branch only. Improves evidence quality for H-Phi/data-sovereignty review.

## Self-Review 20 - Base Lane Readiness Guard
Problem: Base transition and hibernation methods guarded `BaseAwakeState` but then indexed player-inside, room mapping, center AUP, and power metadata lanes that could become independently owned in later H-Phi work.
Solution: Added length-aware `AreBaseStateLanesReady` and used it before base snapshots, configuration, signal draining, player-inside writes, hibernation scans, and wake/sleep transitions.
Rejected Alternatives: Repeating partial guards at each callsite was rejected because it would drift. Trusting constructor symmetry was rejected because the domain is explicitly moving toward shared native buffers.
Scalability potential: Low through Ultra keep the same base mask behavior; future per-lane ownership can fail closed instead of indexing partial state.
Hardware Impact: Small managed branch set on base cold/Frost paths and transition packets only. Prevents crash-class faults under future H-Phi migration.

## Self-Review 21 - Room API SOA Readiness Guard
Problem: Public room mutation APIs checked one lane, then wrote several room or bulkhead SOA lanes.
Solution: Added length-aware `AreRoomStateLanesReady` and `AreBulkheadLanesReady`, then routed room snapshots, room mutators, player-room state, CO2 injection, and bulkhead configuration through them.
Rejected Alternatives: Letting the Burst job clamp after unsafe API writes was rejected because the fault would already have happened. Guarding only the modified lane was rejected because room APIs maintain cross-lane Dalton consistency.
Scalability potential: Low through Ultra keep the same room authority while future H-Phi room-lane migration fails closed on capacity skew.
Hardware Impact: Cold/API path branch cost only. Prevents out-of-bounds public setter faults before the scheduled gas step.

## Self-Review 22 - Depth Stress Pressure Guard
Problem: `ResolveEffectiveDepthStress01` used `_roomCount` to trust `RoomPressure` indexing.
Solution: Added an explicit `roomId < RoomPressure.Length` guard before reading pressure.
Rejected Alternatives: Routing through full room readiness was rejected because depth-stress relief only needs the pressure lane and may be queried often by hull systems.
Scalability potential: Low through Ultra keep the same pressure-relief math while future room-pressure ownership can fail closed.
Hardware Impact: One integer comparison on a public query; no frame-time saving claimed.

## Self-Review 23 - Redundant Guard Trim
Problem: After length-aware room/base readiness helpers, some callsites still kept obsolete `IsCreated` branches.
Solution: Removed redundant checks in room configuration, room flag updates, CO2 pressure injection, and base transition packet drains.
Rejected Alternatives: Leaving the branches was rejected because the readiness helpers already prove those lanes before indexing. Removing readiness helpers was rejected because future H-Phi capacity skew still needs fail-closed protection.
Scalability potential: Low through Ultra keep identical behavior with fewer branch checks on transition packets and room API calls.
Hardware Impact: Tiny branch reduction only; no measurable frame-time claim.

## Self-Review 24 - Base Room Mapping Capacity Guard
Problem: `TryConfigureBase` and signal-derived base slot creation still trusted `_roomCount` when writing `_roomBaseIndex`, even though future H-Phi room-lane migration can desynchronize mapping capacity.
Solution: `TryConfigureBase` now requires full live room readiness for the current room count and clamps remap ranges to `_roomBaseIndex.Length`; transition signals only write a room-to-base mapping when the room id is inside both `_roomCount` and the mapping array length.
Rejected Alternatives: Trusting `_roomCount` was rejected because it is logical occupancy, not native capacity proof. Rebuilding all base room ranges from managed module state was rejected because gas must stay decoupled from habitat construction modules.
Scalability potential: Low through Ultra keep identical hibernation behavior while partial native owner migration fails closed instead of indexing stale room mapping memory.
Hardware Impact: One readiness check and scalar min during base configuration, plus one bounds comparison per transition packet with a valid room id. No frame-time saving claimed; this is crash containment.

## Self-Review 25 - Bulkhead And Telemetry Live-Capacity Guard
Problem: Bulkhead configuration accepted logical room endpoints without proving room SOA readiness, and the black-box fault checker still indexed telemetry with `TelemetryCapacity` after the scheduler moved to live ring length.
Solution: `TrySetBulkhead` now requires current room-lane readiness before accepting endpoints. `CheckTelemetryForFault` and dump headers now use `_telemetryRing.Length`, with a zero-length fail-closed guard.
Rejected Alternatives: Letting the Burst job skip invalid endpoint rooms was rejected for API consistency; the public setter should not admit edges against incoherent room state. Keeping constant dump capacity was rejected because future telemetry ring migration should produce truthful dump metadata.
Scalability potential: Low through Ultra keep the 300-frame default black box. Future smaller diagnostic rings on low-end devices remain safe, while higher-tier builds can expand the ring without code changes.
Hardware Impact: One room readiness check per bulkhead edit and one live-length branch on post-step telemetry fault checks. No frame-time saving claimed.

## Self-Review 26 - H-Phi Audit Recheck
Problem: Continued H-Phi work needed fresh source evidence after the additional native-capacity hardening, but full Unity/Burst validation remains blocked by the no-rebuild instruction.
Solution: Re-ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary` as a source-only check. The script emitted its STATIC_SOURCE summary before timing out at 240s, so the captured metrics are recorded as partial evidence only: RuntimeHPhiRisk 0.000591666, DataSovereignty 0.021395609, DataVaultRefs 153, NativeArrayRefs 6998.
Rejected Alternatives: Treating the timed-out run as a clean pass was rejected. Running dotnet or Unity compilation was rejected by explicit user instruction.
Scalability potential: The metrics show the awake-mask DataVault path is still present and AUP precision risk remains zero; broader Dalton array sovereignty is still a separate owner-migration pass.
Hardware Impact: Audit-only pass; no runtime impact and no microsecond savings claimed.

## Self-Review 27 - Initialization Invariant Tightening
Problem: `IsInitialized` only checked `RoomO2` and the toxicity queue, while the scheduler and external consumers require coherent room, base, bulkhead, telemetry, and queue lanes.
Solution: `IsInitialized` now requires the same length-aware readiness helpers used by public APIs, plus telemetry ring and toxicity queue creation. `ScheduleStep` now gates on `IsInitialized`.
Rejected Alternatives: Letting the Burst job clamp partial state was rejected because the solver could still advertise itself as valid to power/registry consumers. Checking only `RoomO2` was rejected as a stale single-lane proxy.
Scalability potential: Low through Ultra keep identical fast paths when state is coherent; partial H-Phi/native migration now fails closed before scheduling work.
Hardware Impact: A few cold/main-thread readiness branches before scheduling. No frame-time saving claimed; this prevents invalid native job submission.

## Self-Review 28 - Power Awake Mask Binding Guard
Problem: The WFC power graph bridge could bind `BaseAwakeState` when the gas solver existed but did not satisfy the strengthened initialization invariant.
Solution: `WfcOutpostPowerBootRuntime.TryBindGraphBaseAwakeState` now clears the graph binding whenever `IGasDynamicsSolver.IsInitialized` is false, before reading the mask alias.
Rejected Alternatives: Trusting `BaseAwakeState.IsCreated` was rejected because the byte mask alone no longer proves full gas/native telemetry readiness. Pulling power graph logic into gas was rejected because the domains must stay decoupled through `IGasDynamicsSolver`.
Scalability potential: Low through Ultra keep the same read-only mask binding when gas is coherent; stale aliases fail closed so sleeping-base skips cannot run from partial gas state.
Hardware Impact: One boolean check on binding refresh only. No frame-time saving claimed; this prevents stale cross-domain aliasing.

## Self-Review 29 - Gas Seed Retry Honesty
Problem: WFC outpost gas seeding ignored the return values from `TryConfigureRoom` and `TrySetScrubberPowered`, so a transient gas step or partial readiness window could permanently drop initial atmosphere seeding.
Solution: `TrySeedGasRooms` now tracks both writes and keeps `_gasSeedPending` true if any targeted gas room write fails.
Rejected Alternatives: Forcing the gas job to complete before seeding was rejected because room seeding must not introduce a sync stall. Assuming failures are impossible was rejected because the gas API explicitly returns `bool` for step/readiness contention.
Scalability potential: Low through Ultra keep identical room seed values; failed writes retry on the next boot runtime pass instead of leaving modules with stale gas state.
Hardware Impact: Two boolean operations per seeded room during boot/graph refresh only. No frame-time saving claimed.

## Self-Review 30 - Registry Publication Readiness Gate
Problem: The gas solver could publish itself into `GlobalRegistry` after partial native creation, relying on every consumer to check `IsInitialized`.
Solution: `TryRegisterRegistry` now returns until the strengthened `IsInitialized` invariant is true.
Rejected Alternatives: Publishing early with a false `IsInitialized` flag was rejected because not every future consumer can be trusted to perform the check before reading read-only aliases. Throwing on partial state was rejected because fail-closed registration is sufficient without taking down the scene.
Scalability potential: Low through Ultra keep identical behavior when native state is coherent; partial H-Phi/native migration produces no registry-visible gas service until ready.
Hardware Impact: One cold registry-publication branch. No frame-time saving claimed.

## Self-Review 31 - Partial Native State Self-Recovery
Problem: After the stronger readiness invariant, a partial native state could fail closed forever because `EnsureNativeState` returned as soon as `RoomO2` existed.
Solution: `EnsureNativeState` now treats `RoomO2.IsCreated && !IsInitialized` as a cold recovery case: unregister from `GlobalRegistry`, retire the partial native lanes through the existing deferred disposal path, and recreate only after disposal finalizes.
Rejected Alternatives: Keeping the solver invisible but unrecovered was rejected because a single missing native lane could strand atmosphere for the scene. Forcing immediate job completion was rejected; the existing deferred disposal handle remains the correct no-stall path.
Scalability potential: Low through Ultra keep normal startup unchanged; partial H-Phi/native migration now recovers to coherent SOA state instead of staying disabled.
Hardware Impact: Cold fault-recovery path only. No steady-state frame cost and no microsecond saving claimed.

## Self-Review 32 - Base Transition Job Race Closure
Problem: Per-frame base transition draining could run while `GasDynamicsStepJob` was active and mutate `_roomBaseIndex` or `BaseAwakeState`, both of which the job reads.
Solution: Added a fixed-capacity `NativeList<PendingBaseTransitionSignal>` owned by the gas solver. When a step is running, frame snapshots are captured into this native buffer only; after the job completes, deferred packets are applied in captured order before current-frame packets.
Rejected Alternatives: Forcing the gas job to complete on base entry was rejected because a trigger event must not introduce a sync stall. Dropping frame snapshots while the job runs was rejected because it can lose the inside override and falsely hibernate an occupied base. Managed queues were rejected by the zero-GC mandate.
Scalability potential: Low tier pays one preallocated native list sized for two full transition frames; High/Ultra keep the same deterministic transition authority without a main-thread job stall.
Hardware Impact: Expected steady-state cost is zero when no transition occurs. Transition storm cost is bounded by 128 native records and prevents a race-class fault; no profiler microsecond claim without Unity verification.

## Self-Review 33 - Transition Overflow Fail-Open
Problem: The fixed deferred transition buffer originally dropped excess packets silently when full, which could lose the only player-inside wake override.
Solution: Overflow now sets a native-domain fail-open flag. After the active gas job completes, the solver wakes configured bases and blocks hibernation for a two-second unscaled guard window.
Rejected Alternatives: Growing the native list on demand was rejected because transition capture runs on the gameplay frame path and must not allocate. Keeping silent drops was rejected because hibernation correctness beats saving a few microseconds under a storm. Permanent player-inside marking was rejected because one overflow could pin a base awake forever.
Scalability potential: Low devices get bounded memory and fail-open correctness under storms; High/Ultra keep the same path and can absorb the short awake guard without extra simulation complexity.
Hardware Impact: Steady-state cost is one boolean branch. Overflow cost wakes at most `MaxBaseCapacity` bases once and temporarily spends extra gas/power work rather than risking an occupied-base sleep. No profiler microsecond claim without Unity verification.

## Self-Review 34 - Static H-Phi And Zero-GC Recheck
Problem: Full H-Phi source scan remains timeout-prone in the dirty multi-agent workspace, but the domain still needed fresh static evidence after the transition-buffer changes.
Solution: Ran `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`. It exited 0 with STATIC_SOURCE evidence; core build graph gate remains true/opt-in, with existing debt counts: Core asmdef 25, generated project 10, source-backed bridge 14, source-backed compile bridge 8, project-reference replacement 6. Targeted fixed-string scans of `GasDynamicsSolver.cs` found no `.Complete()`, `GetComponent`, coroutine, managed collection, `FindObject`, `ToString`, or string-format patterns.
Rejected Alternatives: Running the full H-Phi scan again was rejected because prior 120s/240s runs timed out. Running dotnet/Unity compilation remains rejected by explicit user instruction.
Scalability potential: Low through Ultra keep the same zero-GC transition and hibernation paths; remaining H-Phi graph debt is outside this domain and still requires integrator work.
Hardware Impact: Audit-only pass; no runtime change and no microsecond saving claimed.

## Self-Review 35 - Overflow Guard Default Timestamp
Problem: `_transitionOverflowAwakeUntil` defaults to `0`, so a strict `now <= awakeUntil` check could treat a time-zero startup hibernation pass as overflow-guarded even though no overflow occurred.
Solution: The guard now requires `_transitionOverflowAwakeUntil > 0` before blocking hibernation.
Rejected Alternatives: Initializing the field to a negative sentinel was rejected because disposal already resets to zero and a positive-active contract is clearer.
Scalability potential: Low through Ultra avoid an unnecessary initial awake hold while retaining fail-open behavior after real transition overflow.
Hardware Impact: One extra scalar comparison on the FrostTick hibernation path; no measurable claim.

## Self-Review 36 - Base AUP Ingress Finite Gate
Problem: Direct gas API calls could configure a base center from an `AbsoluteUniversePosition` with non-finite local coordinates. Startup base centers also came from `transform.position` without proving the vector was finite before `AbsoluteUniversePosition.FromRuntimePosition`.
Solution: `TryConfigureBase`, `TrySetBaseCenterAup`, and signal-derived base slot creation now reject non-finite AUP local coordinates. Startup default base center resolution now uses a finite `transform.position` gate and falls back to default AUP if the transform is corrupt.
Rejected Alternatives: Sanitizing invalid AUP to zero inside public setters was rejected because that fabricates a plausible habitat location and can make hibernation distance logic lie. Adding managed diagnostics or exceptions was rejected because the solver must stay zero-GC/fail-closed in gameplay paths.
Scalability potential: Low through Ultra keep the same hibernation Math LOD behavior on valid data. Corrupt base centers now fail closed instead of poisoning distance-gated sleep/wake decisions.
Hardware Impact: Adds three scalar finite checks on cold/direct API paths and transition packet admission. No frame-time saving claimed; this is AUP integrity and false-hibernation prevention.

## Self-Review 37 - Stale Base Center Fail-Open
Problem: The new AUP ingress gates protect future writes, but an already-corrupt `_baseCenterAup` lane could still make `ResolveBaseHibernationStates` compute non-finite distance and leave a sleeping room-owning base asleep indefinitely.
Solution: The Frost hibernation resolver now checks the stored base center before distance math. Non-finite centers skip distance-gated sleep logic; if the base owns rooms and is asleep, it wakes through the existing analytical catch-up path.
Rejected Alternatives: Rewriting the corrupt center to default AUP was rejected because it invents location authority and can hibernate the wrong base. Throwing/asserting was rejected for gameplay runtime; the fail-open path is deterministic and zero-GC.
Scalability potential: Low tier may spend extra gas simulation for a corrupted base instead of saving cycles through an invalid sleep. Middle/High/Ultra get the same correctness guard while still using distance Math LOD for valid centers.
Hardware Impact: One AUP finite check per tracked base on FrostTick. On i3/MX350 this is cheaper than a false asleep occupied base fault; no profiler microsecond claim without Unity runtime.

## Self-Review 38 - Sanitized Base Signal Rejection
Problem: `GlobalSignals` sanitizes invalid AUP payloads to finite zero-local coordinates and still enqueues the signal. Gas-side finite checks could therefore accept a repaired default/origin center as real base authority.
Solution: Added a high-bit `SanitizedBaseCenterFlag` to the player base transition signal payloads. The signal sanitizer sets that flag when it repairs `BaseCenterAup`, and `GasDynamicsSolver` rejects flagged transition packets before creating/updating base slots.
Rejected Alternatives: Dropping the packet inside the generic `SignalBus<T>.Push` path was rejected because that would change all guarded signal semantics. Rejecting default AUP in gas was rejected because origin can be a legitimate authored location. Adding a managed diagnostic queue was rejected under zero-GC.
Scalability potential: Low through Ultra avoid poisoning hibernation distance authority with sanitized coordinates while keeping the same fixed-size 64-byte signal lane and native deferred buffer.
Hardware Impact: One ushort bit test per base transition packet only. No frame-time claim; this is a correctness guard for rare corrupt producer data.

## Self-Review 39 - Post-Signal H-Phi Core Graph Recheck
Problem: The sanitized base signal fix touched Core signal contracts, so H-Phi evidence needed to be refreshed without using a forbidden rebuild.
Solution: Re-ran `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`. The source-only audit exited 0 with STATIC_SOURCE evidence; core build graph gate remains true/opt-in and existing debt counts are unchanged.
Rejected Alternatives: Running a dotnet/Unity compile was rejected by the explicit no-rebuild instruction. Treating the previous audit as current after a Core touch was rejected because it would be stale evidence.
Scalability potential: The gas hibernation signal lane stays fixed-size and decoupled; remaining Core graph debt is unchanged and still outside this agent's direct domain.
Hardware Impact: Audit-only; no runtime impact and no microsecond saving claimed.

## Self-Review 40 - Read-Only Alias Initialization Gate
Problem: `GlobalRegistry` publication was gated on `IsInitialized`, but a caller holding a direct `IGasDynamicsSolver` reference could still read room/base aliases when only one native lane was created.
Solution: The explicit `IGasDynamicsSolver` room and base read-only properties now return default until the full initialization invariant passes.
Rejected Alternatives: Trusting consumers to check `IsInitialized` was rejected because read-only aliases are cross-domain H-Phi surfaces. Throwing was rejected because the safe alias contract should fail closed without allocations or exceptions.
Scalability potential: Low through Ultra keep identical aliases when coherent; partial native recovery windows no longer leak stale buffers into power/UI consumers.
Hardware Impact: One `IsInitialized` branch per alias request, not per frame job. No profiler microsecond claim; this is alias safety.
