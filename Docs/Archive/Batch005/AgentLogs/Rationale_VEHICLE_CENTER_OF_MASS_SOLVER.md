# Rationale_VEHICLE_CENTER_OF_MASS_SOLVER

Status: PENDING VERIFICATION

Problem: The batch requires a dynamic flooding mass solver for player submarine physics without per-frame PhysX center-of-mass rebuilds.
Solution: Use visual-fake-first physics truth: Burst computes low-cadence weighted COM and angular drag multiplier from compartment water data; main-thread Rigidbody writes are throttled outside `Update`, while PID/control systems consume a compact result snapshot.
Rejected Alternatives: Continuous slosh simulation, exact 3x3 inertia tensor recomputation, and `Rigidbody.centerOfMass` writes every frame. They are unstable on MX350/i3 and violate the mandate against repeated PhysX rebuilds.
Scalability potential: Low uses 1Hz COM updates with scalar fill math; Middle uses slow tick smoothing; High adds richer stress/audio/visual response; Ultra can spend saved cycles on presentation overkill without changing gameplay authority.
Hardware Impact: Expected hot math is O(roomCount) over fixed room arrays. For 8 rooms, Burst loop is sub-10 us on i3/MX350 class CPU before Unity scheduling overhead; static proof only until profiler evidence exists.

Problem: The prompt requires coupling GAS_DYNAMICS_SOLVER and PIPE_LOGISTICS without inventing direct dependencies on other agents.
Solution: Discover existing contracts first; consume existing signal/provider interfaces if present, otherwise add narrow contract structs/interfaces in a contracts assembly and keep concrete code out of cross-domain references.
Rejected Alternatives: Concrete references to pipe/gas solver classes, polling scene objects, or direct singleton access from hot paths.
Scalability potential: Interface/signal path allows low tier to receive scalar fill deltas and high tier to receive richer compartment state later.
Hardware Impact: Avoids scene search and virtual dependency chains in hot cadence; expected gain is removal of O(scene) lookup risk, not a fake measured number.

Problem: Critical vehicle physics needs post-mortem state.
Solution: Implement/bridge a 300-entry blackbox for COM offset, total water mass, flags, and pitch threshold state, dumping to `Docs/AgentLogs/Dump_VEHICLE_CENTER_OF_MASS_SOLVER.bin` on invalid numeric state if no shared telemetry writer exists.
Rejected Alternatives: Debug.Log spam, string telemetry, or "inspect later" reports.
Scalability potential: Low stores compact hashes/flags; Ultra can add extra presentation diagnostics in dev builds only.
Hardware Impact: 300 compact entries are kilobytes-scale persistent memory, negligible versus 8GB RAM target; runtime write is one struct assignment.

Problem: The flood state existed inside `SubmarineFluidDynamics`, but the PID/ballast controller had no decoupled way to consume it.
Solution: Publish a 64-byte `SubmarineFloodStateSignal` and consume the typed `SignalBus` snapshot in the controller. A `GlobalDataVault` SOA solver remains the Burst authority when room arrays are present; the signal path keeps pipe/gas/flood owners decoupled.
Rejected Alternatives: `FindObjectOfType`, direct pipe/gas concrete class references, and adding a monolithic submarine mass singleton. Those approaches break parallel-agent ownership and create scene-search risk.
Scalability potential: Low uses one active signal and 1Hz SOA refresh; Middle/High can increase room-buffer fidelity without changing PID integration; Ultra can add richer metal-stress presentation from the same scalar offset.
Hardware Impact: Snapshot scan is O(signal count) and bounded by one active vehicle lane in this use. Estimated hot overhead is sub-5 us on i3/MX350 before Unity safety checks.

Problem: Vehicle constants/results needed an isolation seam instead of being hard-coded only in Core gameplay.
Solution: Added `Hecton8.Vehicles.Physics.Contracts` with flood-mass constants and blittable result structs, while leaving the existing controller in Core to avoid a high-risk assembly migration during parallel edits.
Rejected Alternatives: A full new runtime assembly for the controller or moving the existing controller out of Core mid-batch. Both are wider blast radius and likely to collide with other agents.
Scalability potential: Low/Middle/High/Ultra tiers can share the same contract while swapping solver cadence and presentation budgets.
Hardware Impact: Contract assembly has 0 runtime cost; it reduces duplicate constants and bad coupling.

Problem: The submarine needs to feel tail-heavy when flooded without buying a full fluid slosh simulation.
Solution: Burst computes water mass per room, resolves a dynamic COM offset with guarded reciprocal mass math, and feeds only the scalar offset/mass into PhysX and PID. Angular inertia is faked by scaling angular damping with `1 + water/base`.
Rejected Alternatives: Exact inertia tensor matrix rebuild, continuous slosh particles, and per-compartment Rigidbody children. All are too expensive and unstable for i3/MX350.
Scalability potential: Low runs 1Hz and still tips/sinks; Middle can use the same math at 0.5s cadence; High/Ultra can spend saved cycles on hull groan, UI, and VFX intensity rather than simulation.
Hardware Impact: 8-room solve is linear, branch-light, and Burst-compatible. Expected CPU math is sub-5 us on low-end silicon; job scheduling overhead is the larger cost and is cadence-limited.

Problem: Auto-level PID would hide flooding by endlessly correcting pitch.
Solution: The PID receives COM offset as bias while water mass is survivable, then disables entirely once water mass exceeds 40 percent of base mass.
Rejected Alternatives: Reducing PID gain gradually only. That still lets a critically flooded submarine look stable and violates physical readability.
Scalability potential: Low gets binary critical behavior; High/Ultra can present richer warning layers without changing control authority.
Hardware Impact: Critical threshold saves one PID schedule and one torque queue per fixed tick while flooded beyond threshold.

Problem: Flooding needs readable hull stress without spending simulation budget.
Solution: Use COM offset magnitude as an acoustic stress scalar and emit `AcousticPingSignal` on a metal-stress channel with cooldown.
Rejected Alternatives: Continuous stress audio component, per-room creak emitters, or per-frame procedural audio objects. They allocate or multiply authoring burden.
Scalability potential: Low gets sparse stress pings; High/Ultra can map the same signal to denser hull groan layers.
Hardware Impact: One struct publish on cooldown, no managed allocation in the hot path.

Problem: Origin shifting can corrupt physics if COM math uses world positions.
Solution: Treat `RoomLocalAUPs` as submarine-local room offsets and never convert them through world origin in the Burst mass solve.
Rejected Alternatives: Converting room world positions every solve or storing absolute world coordinates in the PID controller.
Scalability potential: Same local data works at every tier; Ultra presentation can still reproject for VFX outside the physics authority.
Hardware Impact: Removes origin-shift compensation work from the solver entirely.

Problem: Critical flooding needs player feedback and an external control signal without coupling UI/audio directly to the physics code.
Solution: Emit low-frequency haptics while critical, emit metal-stress acoustic pings from COM offset, and publish `VehicleCommandSignalFlags.CriticalList` when pitch exceeds 30 degrees under critical flood.
Rejected Alternatives: Direct UI calls, allocating audio sources, or storing a UI singleton reference in vehicle physics.
Scalability potential: Low receives sparse haptic/audio packets; High/Ultra can layer richer cockpit warnings and hull creak using the same signal payloads.
Hardware Impact: Cooldown-gated struct publishes; no hot managed allocation. Estimated savings versus direct component dispatch is avoiding scene/UI lookup and per-warning object creation.

Problem: Full compile proof is required, but the current Unity compile is blocked outside this task.
Solution: Cleared the console, requested script compilation, captured objective blocking errors in `HectonPlayerMovement.cs`, and separately validated all touched scripts through Unity MCP validation.
Rejected Alternatives: Claiming compile success from stale docs or editing unrelated player movement interfaces to force a green build.
Scalability potential: Not applicable to runtime; this is integration risk containment.
Hardware Impact: No runtime effect. The practical gain is preserving build evidence instead of false reports.

Problem: The first flood signal bridge pushed `SubmarineFloodStateSignal` into both a `SignalBus` snapshot and a dedicated `NativeQueue`, but the new queue had no verified drain path.
Solution: Removed the redundant queue capacity, field, allocation, and enqueue; kept `SignalBus<SubmarineFloodStateSignal>` as the single active read path used by the ballast controller.
Rejected Alternatives: Leaving the queue allocated "just in case" or adding a second consumer loop. Both create native memory pressure or duplicate signal semantics without adding gameplay authority.
Scalability potential: Low avoids invisible queue buildup; High/Ultra keep the same typed signal lane and can add richer consumers through `SignalBus` snapshots.
Hardware Impact: Prevents an undrained native queue from growing under repeated flood-state publishes. Estimated saved cost is small per publish, but the failure mode was unbounded memory pressure over long play sessions.

Problem: Publishing submarine compartment SOA into `GlobalDataVault.RoomWaterLevels` can collide with the existing habitat/determinism mirror if only one shared buffer already exists.
Solution: The producer now allocates only when none of the three room buffers exist; if any exists, it writes only when all three (`RoomWaterLevels`, `RoomVolumes`, `RoomLocalAUPs`) exist and are at least the submarine compartment capacity. Active count zero clears an existing complete set instead of leaving stale fill data.
Rejected Alternatives: Resizing or partially hijacking `RoomWaterLevels` when it is already owned by habitat determinism, or falling back to scene/component traversal.
Scalability potential: Low tier still gets the signal fallback and 1Hz solver when the complete SOA is present; higher tiers can supply richer room buffers without changing controller math.
Hardware Impact: Avoids reallocation and cross-system buffer stomping. Worst-case runtime remains an 8-slot scalar write loop after the mass-properties job completes.

Problem: The controller's first post-polish exact COM blend could read beyond active submarine compartments if a shared room buffer was longer than the active flood signal.
Solution: Store `SubmarineFloodStateSignal.RoomCount` and clamp the scheduled Burst solver room count to the minimum of signal count and buffer lengths. The job still clamps internally as a second guard.
Rejected Alternatives: Trusting global buffer length as active room count or deriving local room count from world/habitat objects every solve.
Scalability potential: Low/Middle avoid stale inactive slots; High/Ultra can increase buffer length later without changing the active-count contract.
Hardware Impact: One scalar `min` on the main thread; saves bad mass contributions from inactive or stale lanes.

Problem: Directly using the new contracts type from `SubmarineAutoLevelBallastController` produced fresh `Hecton8.Vehicles.Physics.Contracts`/`DynamicFloodMassConstants` errors in the stale local generated `Hecton8.Core.csproj` while Unity was disconnected.
Solution: Kept the contracts asmdef and source as the cross-domain boundary, but backed runtime constants in Core to local literals until Unity regenerates/validates the asmdef graph.
Rejected Alternatives: Reporting the local build red as somebody else's issue while my direct usage added errors, or deleting the contracts boundary entirely.
Scalability potential: Contract types remain available for later producer/consumer assembly migration; runtime remains stable under current generated project state.
Hardware Impact: No runtime cost. The gain is integration containment: no extra project-reference failure from the flood solver code path.

Problem: `SubmarineFluidDynamics` and `SubmarineAutoLevelBallastController` could both write `Rigidbody.centerOfMass` on the same hull in one fixed tick: flood-only first, combined ballast+flood second.
Solution: Added an explicit external COM authority switch. While the controller is enabled, fluid dynamics keeps resolving/publishing its smoothed flood center but skips the PhysX COM write; the controller becomes the single combined COM writer. On controller unregister, authority is handed back.
Rejected Alternatives: Leaving two fixed-tick COM writes because they were outside `Update`, or removing fluid dynamics COM calculation entirely. The first burns avoidable PhysX rebuilds; the second would break flood telemetry/signaling.
Scalability potential: Low tier avoids duplicate COM rebuilds; High/Ultra keep the same authoritative combined COM and can spend saved budget on presentation.
Hardware Impact: Saves up to one `Rigidbody.centerOfMass` write/rebuild per active submarine fixed tick when the ballast controller is present. Estimated saving depends on PhysX internals, but the avoided operation is exactly the stutter risk identified in the prompt.

Problem: The controller cached gas and pipe graph service fields but did not read them in the mass solver; the actual gas/pipe coupling already occurs inside `SubmarineFluidDynamics`.
Solution: Removed those dead controller fields and replacement handlers. The controller now consumes flood state/data-vault outputs only; pipe/gas authority stays in the existing fluid/atmosphere/logistics domain.
Rejected Alternatives: Inventing direct pipe/gas mass reads in the controller or leaving dead fields as architectural decoration.
Scalability potential: Simpler controller hot state; future richer coupling can enter through the existing signal/SOA contract.
Hardware Impact: No measurable per-frame math change, but removes stale service references and replacement-branch work on registry hot swaps.

Problem: The controller could keep applying a retained flood signal frame or stale room SOA after the flood producer stopped.
Solution: Added duplicate-frame rejection, a 3-second flood signal timeout, dynamic flood state reset, and stale pending job-output discard. The room SOA Burst solve now requires an active submarine flood signal and positive signal room count before reading `RoomWaterLevels`, `RoomVolumes`, and `RoomLocalAUPs`.
Rejected Alternatives: Treating generic habitat room buffers as submarine rooms or letting a retained signal snapshot refresh liveness forever. Both can leave the submarine tail-heavy after the actual flood authority has stopped.
Scalability potential: Low tier avoids stale physics with only scalar age tracking; Middle/High keep the same room SOA refinement when the producer is alive; Ultra can add richer flood presentation without changing authority.
Hardware Impact: One age increment and two byte/room-count checks per fixed tick. It prevents stale Burst job admissions and stale PhysX COM writes after timeout; expected low-end saving is up to the previous 1Hz room solve plus one combined COM/damping update when the source is dead.

Problem: The critical-list pitch check used inverse trig to recover degrees from hull forward vector.
Solution: Replaced `asin` degree recovery with a sine-threshold comparison against the configured pitch limit. This keeps the same gameplay threshold while removing inverse trig from the critical feedback path.
Rejected Alternatives: Keeping degree reconstruction for readability or adding an exact Euler-angle path. Both are slower and less stable than comparing the forward-vector vertical component.
Scalability potential: Low tier runs a cheap threshold compare; High/Ultra presentation still receives the same `VehicleCommandSignalFlags.CriticalList` event.
Hardware Impact: Removes one inverse trig call on cooldown-gated critical checks. Exact microseconds depend on CPU math library, but the saved operation is materially heavier than the remaining multiply/sine-threshold path.

Problem: The angular-drag inertia fake could become a no-op because `SubmarineFluidDynamics` zeros Unity angular damping in the environment tick before the controller's player-lane write.
Solution: Added a tiny serialized `floodAngularDampingFloor` and only applies it when the flood multiplier is actually above 1. This preserves zero damping when there is no flood mass, but makes flooded sluggishness readable even when the captured base damping is zero.
Rejected Alternatives: Exact inertia tensor recalculation, always forcing nonzero angular damping, or moving damping ownership out of the controller. The first violates the dear-lie mandate; the second changes dry handling; the third widens cross-domain ownership.
Scalability potential: Low gets a cheap scalar floor; Middle/High/Ultra can raise the serialized floor or presentation layers without altering solver math.
Hardware Impact: One `max` in the active flood damping path. It buys visible inertia for essentially no frame cost and avoids spending simulation budget on tensor work.

Problem: Some remaining flood-path mass guards used `1f` floors while the prompt explicitly required `math.rcp(max(mass, 0.01f))`.
Solution: Added/used `MinimumMassForReciprocal = 0.01f` in the controller and flood producer signal path for base mass, total mass, haptics intensity, and angular drag multiplier reciprocals.
Rejected Alternatives: Leaving the stronger `1f` floor because it was already numerically safe. It was safe but did not match the assigned explicit verification rule.
Scalability potential: Same deterministic scalar path across tiers; corrupt low-mass data fails soft instead of exploding.
Hardware Impact: No measurable cost; this is numeric correctness and assignment compliance.

Problem: Disabling the ballast controller while flooded could leave the angular-damping inertia fake on the Rigidbody until another owner overwrote damping.
Solution: Added `RestoreDynamicFloodAngularDrag()` and call it during unregister after flood state reset. It restores the cached dry damping or zero if the cached value is invalid.
Rejected Alternatives: Trusting `SubmarineFluidDynamics` to overwrite damping later or forcing a damping write every reset. The first leaves a stale-frame lifecycle bug; the second can add redundant writes during normal fixed-tick reset paths.
Scalability potential: All tiers get deterministic cleanup on disable/re-enable; high-end presentation remains decoupled.
Hardware Impact: One scalar write on unregister only. Runtime hot path unchanged.

Problem: A follow-up reciprocal scan found the exact weighted-average flood COM loops still used `math.rcp(totalWaterMass)` / `math.rcp(totalFloodMass)` behind an epsilon branch instead of the prompt's explicit 0.01f mass floor.
Solution: Patched `DynamicFloodMassSolverJob` and `SubmarineFluidDynamics.FloodMassPropertiesJob` so the water/flood center weighted average and max-flood-ratio reciprocal use `math.rcp(math.max(MinimumMassForReciprocal, mass))`.
Rejected Alternatives: Keeping the epsilon branch because it was already finite-safe. It was not literal compliance with the assigned recursive verification rule.
Scalability potential: Same Low/Middle/High/Ultra scalar math; corrupt tiny masses now degrade deterministically instead of relying on a looser branch condition.
Hardware Impact: One `max` before each affected reciprocal in low-cadence flood jobs. Cost is below profiler noise; the value is numeric determinism and assignment compliance.

## OMEGA POLISH CHANGES

Problem: The implementation needed an anti-bloat pass after core task closure.
Solution: Audited for dear-lie replacements, divisions, normalization/sqrt, managed foreach/LINQ/string formatting, hot allocations, and compile evidence. Kept the inertia fake as scalar angular damping, kept COM solve as O(roomCount), and left exact tensor/slosh simulation out.
Rejected Alternatives: 3x3 inertia tensor solve, world-space AUP conversions, per-room managed objects, and continuous audio/haptic components.
Scalability potential: Low/MX350 runs 1Hz COM refresh and sparse feedback; Middle/High run 0.5s refresh; Ultra can spend presentation budget on stronger cockpit/hull response without changing physics authority.
Hardware Impact: Cinematic cheats used: scalar angular-drag multiplier, cooldown-gated acoustic/haptic packets, signal-fed PID bias, binary critical flood cutoff. Estimated saved cost versus exact slosh/tensor path is >0.1 ms/frame on i3/MX350 class hardware; local code still needs profiler proof after global compile blockers clear.

Problem: Final compile evidence is blocked by unrelated project state.
Solution: Latest Unity MCP `validate_script` retry could not run because the Unity session is unavailable (`no_unity_session`). Local `dotnet build Hecton8.Core.csproj --no-restore` still fails with 90 unrelated generated-project errors: missing environment fluids, core scheduling, CCD, acoustic propagation/types, macro swarm, brine samples, and other cross-domain assemblies/types. Status remains PENDING VERIFICATION.
Rejected Alternatives: Reporting green compile, editing unrelated missing assemblies/types, or reverting other agents' files.
Scalability potential: N/A.
Hardware Impact: N/A.

Final Git Diff: modified `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Core/Memory/H8Memory.cs`, `Gameplay/SubmarineAutoLevelBallastController.cs`, `Gameplay/VehicleCommandSignals.cs`, `Hecton8.Core.asmdef`, `SubmarineFluidDynamics.cs`, status/rationale docs; added `Assets/_Project/Scripts/Vehicles/Physics/Contracts/` with asmdef, contract source, folder meta, and Unity-generated `.meta` files. `git diff --stat` for tracked touched files after hardening: 8 files, 1523 insertions, 48 deletions. Untracked contract folder and untracked log file are not included in that tracked stat.
