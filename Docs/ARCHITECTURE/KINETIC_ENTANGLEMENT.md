# KINETIC ENTANGLEMENT
Date: `2026-05-04`

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this mechanic map as current runtime truth.
- This document is a deterministic interaction contract, not proof that transport, flora death authority, voxel eviction, and current sampling are all wired in the current scene.
- Re-open the listed owners before changing gameplay logic.

## Owner Map
- Detection owner: `MountablePlayerTransport`
- Tether kinematics owner: `VehicleMotor`
- Flora death authority: `DestructibleOrganicManager`
- Density and current sampling owner: `HectonMapMagicVegetationBridge`
- Dynamic cave-voxel memory eviction owner: `VoxelDynamicNavGridRuntime`

## Constraint Choice
`ConfigurableJoint` was rejected.

Project rule set forbids Unity joints in gameplay code. The runtime path is already kinematic and authoritative through `VehicleMotor` plus deferred capsule sweep, so the entanglement uses a deterministic tether solve instead of a PhysX joint.

## Trigger Math
The transport samples macro-flora density along its current velocity vector.

Definitions:
- `p0` = current vehicle position
- `v` = current vehicle linear velocity
- `s` = `|v|`
- `d` = normalized velocity direction
- `L` = probe distance
- `N` = probe count
- `rho_i` = macro-flora density at sample `i`

Sample positions:

```text
pi = p0 + d * (L * ((i + 1) / N))
```

Average density:

```text
rho_avg = (sum(rho_i)) / N
```

Entanglement score:

```text
score = rho_avg * s
```

If `score >= EntanglementThreshold`, the system captures the nearest live kelp or sargassum instance UIDs from `DestructibleOrganicManager` and enters `ENTANGLED`.

## Tether Solve
Let:
- `a` = averaged flora anchor position
- `x` = current vehicle position
- `r = x - a`
- `R` = tether length captured at lock time
- `vf` = sampled abyssal current velocity
- `dt` = fixed step

The motor preserves the deferred sweep path. It does not teleport the rigidbody. It computes the next kinematic velocity that would place the vehicle on the tether sphere after the next sweep consume.

Current-driven candidate velocity:

```text
v1 = v0 + vf * CurrentAcceleration * dt
v2 = v1 / (1 + Drag * |v1| * dt)
```

Predicted relative position:

```text
r_pred = r + v2 * dt
```

Sphere constraint:

```text
r_constrained = normalize(r_pred) * R
x_target = a + r_constrained
```

Kinematic velocity fed back into the sweep owner:

```text
v_next = (x_target - x) / dt
```

This gives a pendulum-like response under flow while keeping thrust at zero.

The anchor is captured in the current runtime frame from the flora AUP-backed instance stream. `MountablePlayerTransport` forwards origin-shift events into `VehicleMotor.ApplyOriginShift`, so the cached runtime anchor remains aligned with the same absolute kelp instance after floating-origin rebases.

## Shear Stress Damage
The tether solve also reports deterministic tension for downstream hull stress.

Let:
- `m` = submarine rigidbody mass
- `|r_pred|` = unconstrained predicted anchor-relative length
- `R` = captured tether length
- `v_candidate` = current-driven candidate velocity before projection
- `n = normalize(r_pred)` = tether radial direction
- `dt` = fixed step

Extension and outward velocity:

```text
extension = max(0, |r_pred| - R)
v_out = max(0, dot(v_candidate, n))
```

Constraint acceleration removed by the tether:

```text
a_constraint = extension / (dt * dt) + v_out / dt
```

Solved tether tension:

```text
T_solve = m * a_constraint
```

Pilot fighting tension adds commanded propulsion demand without restoring thrust:

```text
T_command = m * ThrustAcceleration * ThrottleOutput
T_total = T_solve + T_command
```

If:

```text
ThrottleOutput >= StressThrottleThreshold
T_total > TetherYieldLimit
```

then:

```text
overload01 = saturate((T_total - TetherYieldLimit) / TetherYieldLimit)
damage += ShearDamagePerSecond * overload01 * dt
```

Damage is accumulated into a bounded scalar and emitted on `entanglementStressSignalInterval` through the existing `IDamageSignalEmitter` path. `TraumaDispatcher` remains a receiver only. The same interval gates structural groan audio and stress haptics.

## Cavitation Overload
While entangled, high throttle at low vehicle speed produces cavitation instead of useful flow.

Condition:

```text
ThrottleOutput >= CavitationThrottleThreshold
|v_next| <= CavitationLowSpeedThreshold
```

Intensity:

```text
speedSuppression01 = 1 - saturate(|v_next| / CavitationLowSpeedThreshold)
cavitation01 = saturate(ThrottleOutput * max(speedSuppression01, overYield ? 1 : 0.5))
```

Engine damage:

```text
engineDamage += CavitationEngineDamagePerSecond * cavitation01 * dt
```

Bubble and shockwave requests are fixed-capacity events queued to `HectonFluidEngine`. In `PostFixedTick`, the engine emits optional cavitation particles and uses `Physics.OverlapSphereNonAlloc` plus preallocated collider/rigidbody buffers. Small rigidbodies are deduplicated and pushed through `PhysicsForceRouter.QueueForce(..., ForceMode.VelocityChange)`.

## Release Rule
The transport stores the exact entangling flora instance UIDs.

Release condition:
- every tracked UID exists in `DestructibleOrganicManager._destroyedByInstanceUid`

When all tracked flora are dead:
- entanglement state clears
- thrust integration resumes on the next fixed tick

## HUD And Haptic Hook
Entanglement alert emission is tied to lock entry, not to the sustained tether step.

On the fixed tick where `BeginEntanglement` succeeds:
- `NotificationEvents.PushCritical("PROPULSION ENTANGLED // CUT KELP TO RESTORE THRUST")`
- `ToolHapticsRuntime.EnqueueCommand(...)` with a bounded critical stall pulse

That keeps the warning deterministic and non-spammy:
- one notification per lock event
- one haptic stall pulse per lock event
- no per-frame UI or haptic queue churn while already tethered

`ToolHapticsRuntime.EnqueueCommand` writes into the existing double-buffered haptic queue only. `InputDispatcher.DrainToolHaptics` is the sole device drain. It resolves each motor independently by priority first, then blend mode. Lower-priority tool pulses cannot add on top of active stall or hull-stress pulses; same-priority commands still follow override/additive/max semantics.

## Zero-Allocation Density Query Proof
The fixed-step macro-flora query path is allocation-free after initialization.

`MountablePlayerTransport.SampleMacroFloraDensityAlongVelocity`:
- uses constant `EntanglementDensityProbeCount = 4`
- creates only stack/local `Vector3` temporaries
- performs no `new`, no LINQ, no boxing, no string work
- reads density through `HectonMapMagicVegetationBridge.SampleMacroFloraDensityImmediate`

`HectonMapMagicVegetationBridge.SampleMacroFloraDensityImmediate`:
- samples resident native chunk snapshots already owned by the bridge
- performs hash lookup plus direct `NativeArray` reads
- allocates nothing in the call path

Tracked-flora capture is also bounded:
- `uint[4]` and `Vector3[4]` are preallocated on `MountablePlayerTransport`
- nearest-distance scratch in `DestructibleOrganicManager` is `stackalloc float[4]`

## PhysicsApplySystem Boundary
`PhysicsApplySystem` was inspected but not made the owner of the entanglement force.

Reason:
- mounted transport motion is kinematic
- `PhysicsApplySystem` rejects kinematic rigidbodies for deferred force application
- forcing the lock through that system would be a dead path

The correct owner remains `VehicleMotor`, which already owns the kinematic sweep and collision resolution.

## Pure Void Nav Chunks
`VoxelDynamicNavGridRuntime` now strips voxel buffers from a committed chunk when:
- every passability cell is `OpenCell`
- every clearance-distance cell is `ushort.MaxValue`

That state is marked `IsPureVoid`.

Effect:
- front/back/base/distance buffers are disposed
- portal rebuild is skipped
- nearest/containing voxel payload queries fall through
- open-ocean chunks stop consuming cave-voxel RAM
