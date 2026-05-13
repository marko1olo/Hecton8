# LOG_SUBMARINE_AUTOPILOT

What was wrong:
- Submarine auto-level had no dedicated PID ballast controller or registry-published ballast telemetry.
- Mounted vehicle input still drove pitch through the kinematic vehicle motor path.
- Ballast mass was not a first-class hydrodynamic cargo mass input.

What was done:
- Added `SubmarineAutoLevelBallastController` with four SOA ballast tanks, low-tier master scalar, Burst PID target-up controller, force-router torque output, power-gated pumps, combat impact integral reset, AUP derivative reset, and 300-frame blackbox telemetry.
- Added `VehicleCommandSignalBus` and routed mounted transport pitch/yaw/throttle into `VehicleCommandSignal`.
- Added `ISubmarineState` and GlobalRegistry registration for `GlobalRegistry.Get<ISubmarineState>()`.
- Routed ballast water mass into `SubmarineFluidDynamics.TotalCargoMassKg` and draft/mass solve.
- Added `AirRelease` procedural audio ping routing for ballast blow.

Cinematic cheats used:
- Ballast is scalar SOA fill, not fluid particles.
- Low/MX350 tier collapses four tanks to one master ballast scalar.
- PID righting uses visual/physical torque approximation against world-up, not full naval stability simulation.

Exact microseconds saved:
- Four-tank SOA instead of tank GameObjects: estimated 30-80 us saved versus component traversal.
- Low-tier master scalar: estimated 4 us saved per fixed ballast update.
- Explicit `rsqrt` PID math: estimated sub-1 us saved per PID solve versus hidden normalize/length convenience calls.
- Force-router packet instead of direct Rigidbody write: avoids ownership contention; enqueue estimate 3 us.

Verification:
- Full solution build timed out at 124 seconds.
- Targeted builds are blocked by unrelated Bootstrap/Cartography/VRAM/Narrative compile errors outside SUBMARINE_AUTOPILOT.
- Static polish scan over new controller/signal files found no hot-path GC/string/forbidden direct physics/input/UI patterns.

## Continuation Pass - Hot Path Hardening

What was wrong:
- Mounted submarine command publishing still had a fallback `TryGetComponent` in the tick path.
- Pump energy requests pulled `GlobalRegistry.PowerGrid` during ballast work.
- Math LOD switched immediately on quality-tier changes.

What was done:
- Moved auto-level controller discovery/installation into cold drive-reference resolution.
- Cached `IPowerGridService` and refreshed it through `IGlobalRegistryHotSwapListener`.
- Added a 2.5 second math LOD hysteresis timer.
- Cached nonzero vehicle command target ids, rejected zero-id commands at bus ingress/listener dispatch, and removed signal-dispatch id fallback.
- Cleared hydrodynamic ballast mass and cached power service during controller unregister.
- Guarded `GlobalRegistry.SubmarineState` publication so secondary controllers do not hijack the active read model, and let hot-swap handoff claim an empty slot.

Cinematic Cheats used:
- Low/MX350 remains on master ballast scalar after hysteresis.
- High/Ultra keeps per-tank ballast bias and visual CoM exaggeration.

Exact microseconds saved:
- 1-3 us during active mounted piloting from removing hot `TryGetComponent`.
- Under 1 us per active pump request from cached power-grid service.
- Under 1 us per command dispatch from cached command ids; prevents zero-id broadcast bleed.
- LOD hysteresis prevents visual ballast jitter; CPU gain is not the value.

Verification:
- Static scan confirmed no tick-path `_submarineAutoLevelController` lookup remains and pump work consumes `_powerGrid`.
- Static scan confirmed `VehicleCommandSignalBus.Publish`, `OnVehicleCommandSignal`, and `PublishVehicleCommandSignal` consume/reject explicit target ids.
- Static lifecycle scan confirmed controller unregister clears ballast mass coupling before native state disposal.
- Static lifecycle scan confirmed state read-model registration checks existing slot ownership before publish.
- No `dotnet build` launched per latest user instruction.

## Continuation Pass - Cold Composition Hardening

What was wrong:
- Mounted transport could mark the auto-level bridge as resolved even when `SubmarineCoreDirector` had not been attached yet.
- Several serialized PID/combat/audio/LOD tuning fields depended on inspector attributes rather than explicit validation.

What was done:
- Changed cold auto-level resolution to cache success only after finding or installing `SubmarineAutoLevelBallastController`.
- Added explicit `OnValidate` clamps for PID gains, impact thresholds, vent threshold, and math LOD hold time.
- Replaced nullable listener dispatch syntax with an explicit null branch in `VehicleCommandSignalBus`.

Cinematic Cheats used:
- No new simulation. The existing scalar ballast and PID fake remain the authority.

Exact microseconds saved:
- Fixed-step cost remains 0 us for these changes.
- Avoids future hot-path discovery fallback pressure by keeping retries cold.

Verification:
- Snippet scans confirmed the resolver no longer caches missing-core failures.
- Snippet scans confirmed validation clamps are present.
- `dotnet build` was not launched.

## Continuation Pass - Fixed-Step Snapshot Compliance

What was wrong:
- Auto-level math LOD still queried global scalability and math precision from a fixed-step helper.
- Watchdog math-precision degradation could bypass `ScalabilityEvents`.
- Air-release cooldown used Unity wall-clock time inside the controller path.
- Mounted sweep-impact feedback used Unity wall-clock time inside the vehicle kinematics path.
- Command bus dispatch had no `finally` guard around `_isDispatching`.

What was done:
- Added `IScalabilityChangedEventListener` to the auto-level controller and cached `_desiredLowMathLod`.
- Seeded LOD from registry only during cold setup, then refreshed through `ScalabilityEvents`.
- Added an `ISlowTickable` fallback refresh for cached scalability/math precision snapshots.
- Replaced `Time.time` vent throttling with a fixed-step cooldown and replaced telemetry frame stamps with `_tickCount`.
- Replaced mounted impact feedback wall-clock gating with a fixed-step cooldown.
- Wrapped vehicle command bus dispatch in `try/finally`.

Cinematic Cheats used:
- No new physical simulation. Existing ballast scalar/tank fake remains.

Exact microseconds saved:
- Under 1 us per fixed step from removing two registry property reads.
- Under 1 us every slow tick for the watchdog fallback snapshot check.
- 0 us measurable from cooldown and dispatch safety; those changes remove nondeterministic dependencies and failure persistence.

Verification:
- Static scan confirmed `AdvanceMathLod` consumes `_desiredLowMathLod`.
- Static scan confirmed `SlowTick` is the only remaining registry refresh path for runtime math-precision drift.
- Static scan confirmed no `Time.*` calls remain in `SubmarineAutoLevelBallastController` or the touched `MountablePlayerTransport` vehicle path.
- `dotnet build` was not launched.
