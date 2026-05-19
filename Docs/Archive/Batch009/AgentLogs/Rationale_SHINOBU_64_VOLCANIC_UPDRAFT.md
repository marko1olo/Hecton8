# Rationale_SHINOBU_64_VOLCANIC_UPDRAFT

Agent: SHINOBU_64
Role: THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR
Status: DISPATCHER FIXED-PIPELINE POLISH APPLIED; fresh compile deferred by CPU/build guard.

## Decision 011: Remove Legacy ThermalGeyser Unity Physics
Problem: `ThermalGeyser` still used `UnityEngine.Physics.OverlapSphereNonAlloc`, `Rigidbody`, `ForceMode.Acceleration`, and `PhysicsForceRouter.QueueForce`, leaving a second nondeterministic geyser force system beside the new Burst director.
Solution: Rewrite `ThermalGeyser` as an authored marker that submits AUP/radius/thrust/height/heat/phase into `VolcanicUpdraftDirector.TryUpsertAuthoredVent()`. Physical lift is now owned by the Vault/Burst director only.
Rejected Alternatives: Keeping the old path for cave-specific flavor, or hiding it behind a quality switch. Both preserve unpredictable physics.
Scalability potential: Low/Middle/High/Ultra all route through the same `GlobalQualityWeight` math. Cave geysers no longer bypass debris culling or turbulence collapse.
Hardware Impact: Removes broadphase overlap and managed force dispatch from erupting cave geyser fixed ticks. Expected saving is proportional to active colliders; no profiler capture.

## Decision 012: CurrentVolume Marker Only
Problem: `CurrentVolume.FlowPattern.Updraft` could remain a hidden vertical transport lane separate from submarine/leviathan velocity injection.
Solution: Keep the `CurrentVolume` component for cave authoring bounds, but stamp flow strength to `0f`.
Rejected Alternatives: Deleting `CurrentVolume` outright would break cave authoring assumptions; leaving nonzero updraft would duplicate physical truth.
Scalability potential: All tiers share a single physical updraft owner; visuals can still use volcanic wake/mock flow data.
Hardware Impact: Avoids extra current sampling influence in systems that consume `CurrentVolume`.

## Decision 013: Duplicate-ID Log Split
Problem: The same `SHINOBU_64` ID is assigned to both volcanic updrafts and rollback netcode. Shared `Status_SHINOBU_64.md` / `Rationale_SHINOBU_64.md` files are being overwritten by the other lane.
Solution: Preserve volcanic status/rationale in suffixed mirror files while leaving the shared files available to the current overwrite owner. The final log remains `LOG_SHINOBU_64.md` because the user/CTO protocol requires it.
Rejected Alternatives: Repeatedly deleting the rollback status, or pretending the collision does not exist.
Scalability potential: None; this is agent-state integrity.
Hardware Impact: Prevents reporting rot, not frame time.

## Decision 014: Thermodynamics Service Cold Cache
Problem: `PublishPresentationSignals()` read `GlobalRegistry.ThermodynamicsService` inside the `LateFrameTick` chain while emitting vent heat into the thermodynamics grid. That made the registry a live hot-path bus.
Solution: Cache `IThermodynamicsService` during `OnEnable()` and implement `IGlobalRegistryHotSwapRefListener` / `IGlobalRegistryHotSwapListener` to rebind the cached pointer when the service slot is replaced. `LateFrameTick` now uses the cached field only.
Rejected Alternatives: Polling `GlobalRegistry` every LateFrame for safety, or inventing a direct concrete dependency on `AbyssalThermalManager`. Polling violates hot-path service-cache rules; concrete dependency violates compile-wall isolation.
Scalability potential: Low/Middle/High/Ultra unchanged mathematically. The heat bridge remains scalar and continuous; service rebinding is cold event-driven.
Hardware Impact: Removes one registry property read per erupting vent signal loop. Expected saving is sub-microsecond, but the architectural gain is stronger: no hidden hot-path dependency lookup.

## Decision 015: ThermalGeyser Cold Director Cache
Problem: `ThermalGeyser.SubmitVolcanicDirectorVent()` still read `VolcanicUpdraftDirector.ActiveRuntimeInstance` from fixed tick before publishing the authored cave vent.
Solution: Cache `VolcanicUpdraftDirector` during `Awake`, `OnEnable`, `Start`, and `Configure`. Fixed tick now uses `_volcanicDirector` only and returns if the director is not available.
Rejected Alternatives: Keeping the static read in fixed tick, or reintroducing a local Unity force fallback. The first keeps a hot global bridge; the second reopens nondeterministic physics.
Scalability potential: Low/Middle/High/Ultra unchanged. All actual force math remains inside the Burst director and still consumes `GlobalQualityWeight` for turbulence and debris culling.
Hardware Impact: Removes one static singleton lookup from every active cave geyser fixed tick after cold wiring. Expected saving is sub-microsecond per marker; the main gain is removing another hot-path global access.

## Decision 016: Construction Signal Compile Wall
Problem: A fresh `dotnet build Hecton8.Core.csproj` was allowed by CPU guard and failed in `Assets/_Project/Scripts/Construction/ConstructionSignals.cs`: `ISignal` is unresolved at lines 13 and 36.
Solution: Do not edit the Construction domain from the volcanic agent. Record the dependency failure and keep SHINOBU volcanic static scans focused on the touched files.
Rejected Alternatives: Adding a guessed `using` or contract reference in Construction would violate the domain boundary and could mask another agent's ownership problem.
Scalability potential: None; this is integration hygiene.
Hardware Impact: Prevents cross-domain churn and avoids widening the compile wall while 20+ agents are modifying sibling systems.

## Decision 017: Leviathan DTO Compile-Wall Audit
Problem: `VolcanicUpdraftDirector` directly consumes `AlphaLeviathanCognitionState` and `AlphaLeviathanSteeringOutput` to apply upward force/float-state data to leviathans. That is a sibling domain type dependency.
Solution: Leave the dependency in place for this pass because the vault buffers are registered by the AI owner with those exact generic types and the current `Hecton8.Core.asmdef` already references `Hecton8.AI.Cognition`. The AI DTOs are explicit-layout unmanaged structs without `Pack=1`, so aliasing them locally would add type/stride risk without removing the existing assembly edge.
Rejected Alternatives: Replacing the real leviathan injection with signals only would weaken Task 10. Defining local mirror structs would risk mismatching `GlobalDataVault` handle validation and would create a more dangerous binary contract lie.
Scalability potential: Low/Middle/High/Ultra unchanged. The same Burst job still uses `GlobalQualityWeight` to collapse turbulence and only adds scalar riding output when a predator is inside the vent.
Hardware Impact: No runtime microsecond claim. This is compile-wall risk containment: keep the exact owner DTO path rather than adding a second binary interpretation of the same buffer.

## Decision 018: Debris Intersection Kill Switch
Problem: `MockDebrisParticleDTO` lift used `ResolveDebrisLiftWeight()` but still executed `TryEvaluateVent()` for every vent when `GlobalQualityWeight < 0.3`. That satisfied visual outcome but failed Task 11's CPU mandate: weak hardware must not calculate cylinder intersections for small debris.
Solution: Multiply the quality curve by `math.step(0.3f, q)` and branch the debris path before the vent loop. At zero lift weight, debris records only the culled flag and does not execute AUP delta, axial/radial, cone radius, falloff, or turbulence math.
Rejected Alternatives: Leaving the loop and relying on zero multiplier, or adding a binary hardware tier check. The former wastes ALU; the latter violates the continuous quality law.
Scalability potential: Low = debris lift and debris cylinder queries collapse to zero; Middle = polynomial smooth ramp begins above 0.3; High/Ultra = full debris chimney with turbulent vectors and VFX wake scalars.
Hardware Impact: For default mock capacity, low quality skips up to `64 debris * ventCount` `TryEvaluateVent()` calls per mock injection pass. With 8 active vents this removes up to 512 AUP-local cylinder/cone evaluations before considering real debris lanes.

## Decision 019: Fixed Dispatcher Updraft Pipeline
Problem: The volcanic director still followed the legacy fixed/post-fixed owner model and could only prove safety by completing its own handle before unlocking vault-owned buffers. That breaks the dispatcher dependency story and can create unpredictable stalls under contention.
Solution: Register `VolcanicUpdraftDirector` as `IDispatcherFixedSystem`. `ScheduleFixedSimulation()` now combines the dispatcher dependency with pending submarine read handles, schedules reset/eruption/entity/player/leviathan/VFX/telemetry jobs, registers the final handle with `H8Memory`, and returns it to the master fixed bridge. `PostFixedSimulation()` now only clears bookkeeping and unlocks buffers after the master bridge has completed the fixed batch. `OnDisable()` keeps a cold `.Complete()` teardown guard because disabling while a job mutates vault buffers cannot unlock memory speculatively.
Rejected Alternatives: Keeping `IFixedTickable`/`IPostFixedTickable` and relying on local completion, or letting hot code unlock buffers before the dispatcher completes. Both make the updraft system a private scheduler instead of a deterministic pipeline participant.
Scalability potential: Low = dispatcher dependency chain still runs, but debris and turbulence are collapsed by `GlobalQualityWeight`; Middle = debris ramp resumes above 0.3; High/Ultra = full vent, leviathan ride, wake, thermodynamics, and telemetry signals with the same chain.
Hardware Impact: Removes the volcanic owner's hot fixed-batch wait from normal execution. Exact microseconds depend on system count, but the structural saving is one owner-side synchronization point; dispatcher remains the sole fixed bridge completion site.

## Decision 020: Compile Guard After Dispatcher Polish
Problem: The dispatcher refactor changes interface wiring and should be compiler-verified, but the project guard forbids builds when CPU is above 50 percent.
Solution: Run static scans and defer `dotnet build`. Latest guard sample after the patch was `CPU=100,100,100; active dotnet:30376`.
Rejected Alternatives: Starting `dotnet build` while CPU is saturated, or claiming compile verification from static scans.
Scalability potential: None; this is local machine protection.
Hardware Impact: Prevents adding a compiler workload to an already saturated machine and avoids misleading build diagnostics.

## Decision 021: Fresh XML Reconciliation And Guard Recheck
Problem: The repeated prompt requires treating disk as truth again, not relying on prior chat memory. The shared `SHINOBU_64` files are still polluted by rollback entries because `CURRENT_BATCH.md` has duplicate IDs.
Solution: Re-extracted the volcanic `<AGENT_PROMPT id="SHINOBU_64">` block from `Docs/Tasks/CURRENT_BATCH.md`, confirmed the 20-task volcanic matrix, re-ran source scans, and kept this suffixed volcanic rationale as the authoritative lane. The latest build guard sampled `CPU=100,100,100; compiler processes=0`, so compile remains deferred by CPU saturation alone.
Rejected Alternatives: Trusting the prior final answer, overwriting rollback history in shared files, or launching `dotnet build` just because no compiler process is active.
Scalability potential: Low/Middle/High/Ultra volcanic behavior unchanged: weak quality skips debris intersections and collapses turbulence; higher quality restores analytic turbulence, debris chimney, VFX wake scalars, heat, acoustic, and leviathan ride outputs.
Hardware Impact: No new runtime change. Prevents a compiler workload from contending with an already saturated machine.

## Decision 022: Bottom-Appended Self-Audit Repair
Problem: A fresh volcanic XML recheck audit was present near the top of `LOG_SHINOBU_64.md`, violating the required old-top/new-bottom reporting order.
Solution: Removed the misplaced block and appended the corrected volcanic self-audit at the true bottom of `LOG_SHINOBU_64.md`.
Rejected Alternatives: Leaving a duplicate near-top report, or only reporting the correction in chat. The CTO reads the log file, so the file order must be true.
Scalability potential: No runtime math change. The recorded audit still documents low/middle/high/ultra behavior through the continuous quality curve.
Hardware Impact: No frame-time claim. Latest guard after the log repair was `CPU=73.7,88.6,86.8; compiler processes=0`, so build remains deferred.
