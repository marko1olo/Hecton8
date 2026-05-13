# LOG_VEHICLE_CENTER_OF_MASS_SOLVER

## 2026-05-13 - Dynamic Flooding Physics

What was wrong: submarine flood mass existed in isolated fluid dynamics, but the auto-level/ballast controller did not consume flood COM, did not bias PID torque, did not disable stabilization at catastrophic mass, and did not emit vehicle-level feedback/telemetry for critical flooding.

What was done:
- Added `SubmarineFloodStateSignal` lane support and a publisher from `SubmarineFluidDynamics` after flood mass-property job swap.
- Added `DynamicFloodMassSolverJob` in `SubmarineAutoLevelBallastController`: SOA inputs from `GlobalDataVault` (`RoomWaterLevels`, `RoomVolumes`, `RoomLocalAUPs`), seawater mass calculation, guarded weighted COM, and angular-drag multiplier output.
- Added `Hecton8.Vehicles.Physics.Contracts` asmdef and flood mass contract structs/constants for the vehicle-physics contracts boundary.
- Added `BufferID.RoomVolumes` and `BufferID.RoomLocalAUPs`.
- Fed dynamic COM offset into `SubmarineAutoLevelPidJob` as pitch bias; critical flooding clears PID state and blocks auto-level scheduling.
- Added fake inertia via `Rigidbody.angularDamping = base * (1 + TotalWaterMass/BaseMass)`.
- Added metal-stress `AcousticPingSignal`, critical low-frequency `HapticRequest`, and cooldown-gated `VehicleCommandSignalFlags.CriticalList`.
- Extended 300-frame telemetry with dynamic COM offset, total water mass, angular-drag multiplier, and critical state. Invalid state dumps to `Docs/AgentLogs/Dump_VEHICLE_CENTER_OF_MASS_SOLVER.bin`.

Cinematic Cheats used:
- Scalar angular damping multiplier instead of exact 3x3 inertia tensor.
- Low-tier 1Hz COM solve instead of fixed-tick recompute.
- COM offset audio/haptic events instead of simulated hull stress.
- Binary critical flood cutoff at 40 percent base mass instead of gradual fake stabilization.

Exact Microseconds saved:
- Avoided fixed-tick flood COM scheduling on Low/MX350: about 49 job admissions/second saved at 50Hz fixed step.
- Replaced tensor/slosh solve with scalar multiplier: estimated >100 us/frame saved on i3/MX350 versus a full inertia/slosh path.
- Signal/cooldown feedback avoids per-frame component/audio dispatch; estimated 5-20 us per warning event and 0 B/frame hot allocation.
- 8-room Burst loop math estimate: sub-5 us excluding scheduler overhead; status remains estimate only until global compile blockers clear and profiler proof exists.

Verification:
- Unity MCP `validate_script` returned zero diagnostics for `SubmarineAutoLevelBallastController.cs`, `SubmarineFluidDynamics.cs`, `GlobalSignals.cs`, `VehicleCommandSignals.cs`, and `DynamicFloodMassContracts.cs`.
- `git diff --check` passed for touched tracked files; only line-ending warnings were reported.
- Unity refresh/compile was requested after console clear.
- `dotnet build Hecton8.Core.csproj` was run per mandate.

Blocked:
- Full Unity/dotnet compile proof is blocked by unrelated project errors in `HectonPlayerMovement.cs` and broader generated project assembly-reference gaps. No green compile claim is made.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 7

What was wrong: the follow-up reciprocal scan found the actual flood weighted-average loops still using `math.rcp(totalWaterMass)` and `math.rcp(totalFloodMass)` after only an epsilon branch. That was finite-safe, but not literal compliance with the assignment's `math.rcp(max(mass, 0.01f))` rule.

What was done:
- Patched `DynamicFloodMassSolverJob` in `SubmarineAutoLevelBallastController`.
- Patched `FloodMassPropertiesJob` in `SubmarineFluidDynamics`.
- Added the same 0.01f guard to the max-flood-ratio reciprocal in the producer job.

Cinematic Cheats used:
- Kept scalar weighted COM and angular-damping fake.
- No exact inertia tensor, slosh particles, or compartment Rigidbody expansion.

Exact Microseconds saved:
- No meaningful CPU saving; this pass buys deterministic numeric safety.
- Cost is one scalar `max` before each affected reciprocal, paid only on low-cadence flood mass jobs.

Verification:
- Static scan confirms flood weighted-average reciprocals now use `math.rcp(math.max(MinimumMassForReciprocal, ...))`.
- `git diff --check` passes for the two patched scripts; line-ending warnings only.
- Unity MCP validation could not run because the Unity session is unavailable (`no_unity_session`).
- `dotnet build Hecton8.Core.csproj --no-restore` remains red with 90 unrelated generated-project errors: missing environment fluids, core scheduling, CCD, acoustic propagation/types, macro swarm, brine samples, and related cross-domain types.

Blocked:
- Full Unity/Burst compile proof remains blocked by the broader project graph and disconnected Unity MCP session.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 6

What was wrong: disable/re-enable lifecycle could leave the dynamic flood angular-damping fake on the Rigidbody after the controller unregistered while flooded.

What was done:
- Added `RestoreDynamicFloodAngularDrag()` to `SubmarineAutoLevelBallastController`.
- `UnregisterRuntime()` now resets dynamic flood state and restores cached dry angular damping.
- The restore is unregister-only, so the normal fixed-tick reset path still avoids redundant damping writes.

Cinematic Cheats used:
- Preserved the scalar angular-damping fake and added lifecycle cleanup rather than adding a real inertia tensor owner.

Exact Microseconds saved:
- Hot path unchanged.
- One scalar write on unregister prevents stale damping without adding per-frame work.

Verification:
- Unity MCP `validate_script` passed for `SubmarineAutoLevelBallastController.cs` with 0 diagnostics.
- `git diff --check` passed for controller/asmdef touched files; line-ending warnings only.

Blocked:
- Full Unity/Burst compile proof remains blocked by unrelated project graph errors.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 5

What was wrong: `SubmarineFluidDynamics` intentionally zeros Unity angular damping during its environment-lane damping pass. The ballast controller cached that zero and multiplied it by the flood inertia scalar, making the "sluggish when flooded" fake visually disappear on setups where the base Rigidbody damping is zero.

What was done:
- Added serialized `floodAngularDampingFloor` to `SubmarineAutoLevelBallastController`.
- The floor is applied only when `_dynamicFloodAngularDragMultiplier > 1.0001f`, so dry/no-flood damping remains untouched.
- Replaced remaining flood-path mass reciprocal floors with the explicit `MinimumMassForReciprocal = 0.01f` rule in the controller.
- Added the same `0.01f` reciprocal guard to `SubmarineFluidDynamics.PublishSubmarineFloodStateSignal()`.

Cinematic Cheats used:
- Kept the dear-lie scalar angular damping fake, but made it visible when the lower hydrodynamic layer uses zero Unity angular damping.
- No exact inertia tensor solve, no slosh particles, no child Rigidbody compartments.

Exact Microseconds saved:
- Still avoids exact tensor recomputation; expected saved budget remains >0.1 ms/frame versus a real tensor/slosh path on i3/MX350 class hardware.
- Added cost is one scalar `max` on the active flood damping path.
- Prevents wasted authoring/testing time from a no-op inertia fake; runtime frame cost remains effectively unchanged.

Verification:
- Unity MCP `validate_script` passed for `SubmarineAutoLevelBallastController.cs` with 0 diagnostics after retry.
- Unity MCP `validate_script` on `SubmarineFluidDynamics.cs` still times out in the MCP regex engine; this is consistent with prior large-file validation behavior.
- `git diff --check` passed for touched tracked scripts; line-ending warnings only.
- Static scan confirms flood-path reciprocal guards now use `MinimumMassForReciprocal`; remaining `math.max(1f, ...)` hits in `SubmarineFluidDynamics` are unrelated depressurization/thermal paths.
- Latest Unity console errors are unrelated UI diegetic contract misses in `VehicleSubOsCockpitRuntime.cs`.
- Local `dotnet build Hecton8.Core.csproj --no-restore` remains red on unrelated generated-project reference/type failures across environment fluids, core scheduling, CCD, acoustic, macro swarm, brine, and other cross-domain assemblies.

Blocked:
- Full Unity/Burst compile proof remains blocked by unrelated project graph errors already listed in Status/Rationale.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 4

What was wrong: flood authority could stay alive from a retained `SignalBus` snapshot frame, and the controller still had a habitat room-count fallback that could read generic room buffers as submarine compartment mass. The critical-list pitch check also used inverse trig for a threshold test.

What was done:
- Added `_hasFloodSignalFrame`, `_dynamicFloodSignalActive`, and `_floodSignalAgeSeconds` to the ballast controller.
- Duplicate `SubmarineFloodStateSignal.Frame` values are ignored instead of refreshing liveness every fixed tick.
- Active flood authority expires after 3 seconds without a fresh signal, resets dynamic flood COM/mass/angular damping state, and prevents stale pending room-solve output from committing.
- The room SOA Burst solve now requires an active submarine flood signal and positive `RoomCount`; the unused habitat graph fallback and registry branch were removed.
- Reciprocal guards in the Burst flood mass solve now use the prompt's explicit `math.rcp(math.max(mass, 0.01f))` pattern.
- Replaced the critical-list inverse-pitch calculation with a sine-threshold comparison.

Cinematic Cheats used:
- Kept signal liveness as the authority instead of trying to infer flood ownership from global room arrays.
- Kept scalar COM/angular-damping illusion; no slosh particles, no exact tensor, no extra Rigidbody children.
- Used a threshold compare for pitch instead of reconstructing an angle.

Exact Microseconds saved:
- Prevents stale 1Hz room-solve admissions after the flood producer stops; low-tier saving is up to one stale job admission per second plus avoided COM/damping writes after timeout.
- Removed one inverse trig call from critical flood event checks; exact CPU time requires profiler proof, but the remaining path is a clamp, sine threshold, and compare.
- Removed unused habitat graph service replacement branch; cold-path saving only, mainly ownership hardening.

Verification:
- Unity MCP `validate_script` passed for `SubmarineAutoLevelBallastController.cs` with 0 diagnostics after this pass.
- `git diff --check` passed for touched tracked files; line-ending warnings only.
- Static scan found no habitat graph fallback and no `asin`/`acos` in the controller.
- Unity console remains red on unrelated `GlobalDataVault` memory audit symbols and missing `Hecton8.Vehicles.VFX`.
- `dotnet build Hecton8.Core.csproj --no-restore` remains red on unrelated generated project missing assemblies/types (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Physics.CCD`, acoustic propagation/types, macro swarm, brine samples, etc.).

Blocked:
- Full Unity/Burst compile proof remains blocked by the broader project graph. No green Burst compile claim is made.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 3

What was wrong: flood mass had become physically authoritative in two places. `SubmarineFluidDynamics` wrote a smoothed flood-only `Rigidbody.centerOfMass`, then `SubmarineAutoLevelBallastController` wrote the combined ballast+flood COM later in the same fixed tick. It was not an `Update` loop bug, but it was still an avoidable PhysX COM rebuild.

What was done:
- Added `SetExternalCenterOfMassAuthority(bool)` to `SubmarineFluidDynamics`.
- While the ballast controller is enabled, fluid dynamics keeps calculating and publishing its smoothed flood COM but skips writing `Rigidbody.centerOfMass`.
- On controller unregister/disable, COM authority is handed back to fluid dynamics.
- Removed dead cached gas/pipe graph fields from the controller; gas and pipe coupling stays in `SubmarineFluidDynamics`/atmosphere/logistics, while the controller consumes the resulting flood signal/SOA data.

Cinematic Cheats used:
- Preserved one scalar combined COM authority instead of adding a second physical body or exact tensor handoff.
- Kept the angular-damping inertia fake and avoided full slosh/tensor expansion.

Exact Microseconds saved:
- Avoids up to one redundant `Rigidbody.centerOfMass` write per active submarine fixed tick when the ballast controller is present. The exact PhysX rebuild cost must be profiled in Unity, but the removed operation is the expensive one named by the prompt.
- Removed dead registry hot-swap branches for gas/pipe fields in the controller; small cold-path saving, mainly architecture cleanup.

Verification:
- Unity MCP `validate_script` passed for `SubmarineAutoLevelBallastController.cs` with 0 diagnostics.
- Unity MCP `validate_script` timed out on `SubmarineFluidDynamics.cs`; this file is large and has timed out repeatedly under MCP.
- `git diff --check` passed for touched tracked files; line-ending warnings only.
- Static scan found no `_gasDynamics`/`_pipeGraph` controller residue and no redundant flood native queue residue.
- Local `dotnet build Hecton8.Core.csproj --no-restore` remains red on unrelated generated project missing references/types. No new controller diagnostic appeared in the filtered output.

Blocked:
- Full Unity/Burst compile proof still requires a connected Unity editor and a resolved generated project graph.

Final status: PENDING VERIFICATION.

## 2026-05-13 - Hardening Pass 2

What was wrong: the first completed pass still had three integration risks: `SubmarineFloodStateSignal` was duplicated into an unused native queue, the data-vault room bridge could partially collide with an existing `RoomWaterLevels` lane, and direct use of the new contracts type added fresh local generated-project errors while Unity was disconnected.

What was done:
- Removed the redundant flood native queue allocation/enqueue; `SignalBus<SubmarineFloodStateSignal>` is now the single active flood signal path.
- Hardened `SubmarineFluidDynamics` room SOA publishing so it allocates all three room buffers as a set or only writes when all three already exist. It clears existing complete lanes when active compartment count drops to zero.
- Clamped the controller's scheduled room count to the minimum of signal room count and buffer lengths before scheduling `DynamicFloodMassSolverJob`.
- Backed direct `DynamicFloodMassConstants` usage out of the controller after local build proved the stale generated `Hecton8.Core.csproj` could not resolve the new contracts assembly. The contracts asmdef/source remain for the isolation boundary.
- Re-ran static scans for hot managed allocations, redundant flood queues, direct contract usage, and whitespace errors.

Cinematic Cheats used:
- Kept scalar angular damping and cooldown-gated feedback as the authority; no tensor/slosh expansion was added during polish.
- Kept low-tier 1Hz COM solve and 0.5s high-tier cadence; no fixed-tick flood mass solve.
- Used squared COM-offset thresholding before `sqrt` for metal stress so the expensive path runs only on cooldown-gated emissions.

Exact Microseconds saved:
- Removed one native enqueue per flood-state publish and one unused persistent queue allocation. Per-publish gain is small, but it removes unbounded queue growth risk over long sessions.
- Avoided partial data-vault reallocation/hijack when only `RoomWaterLevels` exists; prevents cross-system buffer churn and stale mass work.
- Squared stress gating avoids a fixed-tick `sqrt`; estimated <1 us/tick saved, but deterministic and free on low-end budget.
- Retained the original large savings: about 49 job admissions/second avoided on Low/MX350 versus fixed-tick solve, and estimated >100 us/frame saved versus exact slosh/tensor simulation.

Verification:
- `git diff --check` passed for touched tracked files; line-ending warnings only.
- Static scan found no `SubmarineFloodStateSignalCapacity`/`_submarineFloodStateSignals` residue and no direct `DynamicFloodMassConstants` usage in runtime controller code.
- Static allocation scan found only pre-existing cold scratch `List<>` fields in `SubmarineFluidDynamics`, already annotated as cold.
- Unity MCP validation could not run after this hardening pass because the Unity editor instance is disconnected (`No Unity instances are currently connected`).
- `dotnet build Hecton8.Core.csproj --no-restore` remains red on unrelated generated project missing assemblies/types (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `BrineLayerSample`, `SoundEmissionSignal`, acoustic portal types, etc.). The direct contract errors introduced during hardening were removed before this final report.

Blocked:
- Burst compile proof remains blocked until Unity is connected and the generated project graph resolves the broader missing assemblies/types.

Final status: PENDING VERIFICATION.
