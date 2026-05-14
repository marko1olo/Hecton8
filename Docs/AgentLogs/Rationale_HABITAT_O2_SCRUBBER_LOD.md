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
Solution: Added typed unmanaged `PlayerBaseEnterSignal` and `PlayerBaseExitSignal` lanes, then consumed snapshots from `GasDynamicsSolver.FrostTick`.
Rejected Alternatives: Direct `BaseAirlock` or `BaseModule` references were rejected because they would violate batch parallelism and create fragile compile dependencies. C# events were rejected because managed delegates are the wrong channel for a zero-GC runtime signal.
Scalability potential: Low/MX350 reads at FrostTick only. Middle keeps 500m hibernation. High/Ultra can spend saved gas CPU on richer alarm/HUD presentation rather than more gas truth.
Hardware Impact: Estimated 1-3us saved per transition batch compared with managed observer fanout; exact profiler proof absent.

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
