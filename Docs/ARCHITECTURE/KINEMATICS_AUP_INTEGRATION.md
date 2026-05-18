# KINEMATICS_AUP_INTEGRATION
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the kinematics/AUP integration map, not a Play Mode traversal or physics-stability proof.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Player, vehicle, tether, voxel collider bake, and submarine contact paths still require source re-open plus runtime verification before surgical changes.

Mandates followed:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `PHYS_Tether_Cable_Acceleration_Constraints.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Scope

This document is the canonical runtime map for:
- player KCC ownership
- transport-relative frames
- AUP-to-runtime rebasing
- hydrodynamic added mass
- tractor-beam/tether reduced-mass force routing
- prop-wash wake turbulence
- voxel collider bake publication
- pre-solver submarine contact modification

If another document still describes a Unity `CharacterController` owner for player locomotion, that document is obsolete.
Current runtime ownership is `Rigidbody + CapsuleCollider + HectonPlayerMovement + HectonPlayerMotor`.

## Ownership Map

- `HectonPlayerMovement`
  - high-level locomotion state
  - transport-frame input transformation
  - external subsystem force accumulation
  - locomotion mode selection
- `HectonPlayerMotor`
  - authoritative kinematic sweep owner
  - depenetration and wall-slide math
  - current-velocity hydrodynamic presentation state
- `VehicleMotor`
  - mountable vehicle KCC owner
  - vehicle-added-mass and depth-scaled drag
  - fixed-size hydrodynamic wake sample owner
- `TetherInstance`
  - tow-cable/tractor reduced-mass PD pull request owner
  - reaction-force routing for tow anchors
- `HectonContactJob`
  - Burst-safe contact, reduced-mass, and PD math kernels
- `GlobalPhysicsStateManager`
  - active rigidbody registry
  - underwater added-mass tensor baseline/restore owner
- `HectonFloatingOrigin`
  - AUP/runtime rebase ownership
  - drift sentinel ownership
- `HectonVoxelEngine`
  - async Marching Cubes mesh build
  - async `Physics.BakeMesh` publish chain
- `PhysicsApplySystem`
  - deferred force-packet application
  - double-buffered force-packet queue ownership
  - pre-solver heavy-submarine contact modification
  - deferred submarine-impact trauma dispatch

## Force Packet Double Buffer

`PhysicsApplySystem` owns two persistent `NativeQueue<ForcePacket>` instances.
Gameplay producers enqueue into the Back queue through `PhysicsApplySystem.QueueForce`, `QueueForceAtPosition`, `QueueTorque`, or the reduced-mass `QueueTractorBeamPd` helper.
Burst producers request `TryGetForcePacketBackWriter(out NativeQueue<ForcePacket>.ParallelWriter writer)` during scheduling and pass that writer into the job.
The apply system only drains the Front queue after the post-fixed swap boundary.

Cadence:
1. Fixed producers write Back.
2. `SystemDispatcher.PostFixedTick` calls `PhysicsApplySystem.PostFixedTick`.
3. Front and Back queue handles swap.
4. The new Front queue drains into the Burst validation snapshot.
5. `LateFrameTick` completes validation and applies only finite packets.

Swap kernel:

```csharp
NativeQueue<ForcePacket> swap = _frontPacketQueue;
_frontPacketQueue = _backPacketQueue;
_backPacketQueue = swap;
```

No main-thread `.Clear()` is performed on a queue while it is the producer-visible Back queue.
Teleport cleanup explicitly drains both queues after the validation handle is complete.

Force packets carry an explicit priority byte:

```csharp
Visual = 0;
Ambient = 1;
Critical = 2;
```

Critical packets are used for kinematic/tether/impact authority. Ambient packets are used for water currents, buoyancy side effects, and environmental drift. When a `VehicleMotor` is macro-flora entangled, ambient force packets are rejected if their predicted velocity delta would extend the vehicle outside the cached tether radius. The discard gate does not apply to critical packets.

## Tractor Beam PD Controller

The tractor/tow controller is not allowed to call `Rigidbody.AddForce` directly.
`TetherInstance` resolves the tow error and routes payload acceleration through `PhysicsForceRouter`.
Tow-cable anchors receive the reduced-mass reaction force as a force packet so the towing vessel receives physically scaled recoil instead of infinite authority.

Reference kernel:

```csharp
reducedMass = (m1 * m2) / max(m1 + m2, 0.0001f);
kd = 2f * sqrt(kp * reducedMass) * overDamping;
force = kp * (targetPosition - currentPosition)
      - kd * (currentVelocity - targetVelocity);
acceleration = force / reducedMass;
```

`HectonContactJob.ResolveTractorBeamPdForce` and `ResolveTractorBeamPdAcceleration` are Burst-safe.
`PhysicsApplySystem.QueueTractorBeamPd` applies equal/opposite packet forces for explicit tractor-beam users.
Primary cable acceleration remains clamped by the authored cable acceleration cap.

## AUP Rule

All simulation math starts from `AbsoluteUniversePosition`.
`Transform.position` is presentation-space only.

Runtime reconstruction:

```csharp
runtimePosition = absolutePosition - HectonFloatingOrigin.CurrentTotalOffset;
```

Distance rules:
- use `AbsoluteUniversePosition.DistanceSq(...)` for world-scale logic
- convert to runtime `Vector3/float3` only at the last render/physics handoff point

Burst downcast kernel:

```csharp
long gridDeltaX = position.GridX - cameraPosition.GridX;
long gridDeltaY = position.GridY - cameraPosition.GridY;
long gridDeltaZ = position.GridZ - cameraPosition.GridZ;

double x = (gridDeltaX * AbsoluteUniversePosition.CellSizeMeters)
    + ((double)position.LocalX - cameraPosition.LocalX);
double y = (gridDeltaY * AbsoluteUniversePosition.CellSizeMeters)
    + ((double)position.LocalY - cameraPosition.LocalY);
double z = (gridDeltaZ * AbsoluteUniversePosition.CellSizeMeters)
    + ((double)position.LocalZ - cameraPosition.LocalZ);

float3 cameraRelative = new float3((float)x, (float)y, (float)z);
```

The sector delta is computed as `long` before any floating conversion.
The local offset is accumulated in `double`; only the final camera-relative value is narrowed to `float3`.

## Origin Shift And Safe Teleport

`HectonFloatingOrigin` captures `CurrentFixedInterpolationAlpha` when creating `OriginShiftEventData`.
`GlobalPhysicsStateManager` suspends rigidbody interpolation during the shift, writes the shifted body pose, then issues `MovePosition(targetPosition)` before interpolation is restored.
Unity does not expose the internal interpolation target buffer; the supported mitigation is interpolation suspension plus one-frame pose publication.

During an origin shift, tracked rigidbodies moving faster than 20 m/s are temporarily forced to `CollisionDetectionMode.Continuous`. The previous CCD mode is restored after the rebase window. The position shift and interpolation mitigation must be applied in the same rebase window; splitting those operations creates one-frame ghost collisions.

Safe teleport protocol:
1. `HectonFloatingOrigin.BeginSafeTeleportProtocol()` pauses physics integration for the frame.
2. `PhysicsApplySystem.ClearQueuedPacketsStatic()` completes pending packet validation and drains Front/Back queues.
3. Player and vehicle inertial-ghost buffers are reset.
4. Teleport/AUP mutation executes.
5. Rigidbody interpolation history is invalidated by resetting center of mass and toggling kinematic state through the safe-teleport reset owner.
6. `HectonFloatingOrigin.EndSafeTeleportProtocol()` releases the one-frame physics pause.

## Relative Transport Frames

When the player is attached to `ITransportPlatform`, volitional input is solved in the platform frame first.

Reference kernel:

```csharp
Vector3 rawInputWorld = ResolveWorldYawRotation(_bodyYaw) * new Vector3(inputH, 0f, inputV);
Vector3 inputLocal = cache.worldToLocal.MultiplyVector(rawInputWorld);
float magnitude = rawInputWorld.magnitude;
if (inputLocal.sqrMagnitude > 0.0001f)
    inputLocal = inputLocal.normalized * magnitude;

Vector3 inputWorld = cache.localToWorld.MultiplyVector(inputLocal);
```

The local vector is not flattened after entering the platform frame.
Tilted hull traversal must preserve the transformed vertical component.

Carrier integration uses the platform delta rotation:

```csharp
Vector3 rotatedOffset = dq * (playerPositionLast - platformPositionLast);
Vector3 playerPositionNew = platformPositionCurrent + rotatedOffset;
```

Attached velocity sync uses:

```csharp
playerVelocity = platform.GetPlatformPointVelocity(playerPoint) + relativeVelocity;
```

Rotational inheritance keeps the player's local hull-relative rotation invariant:

```csharp
dq = qCurrent * inverse(qLast);
localRotationBeforeDelta = inverse(qLast) * playerWorldRotation;
playerWorldRotationNew = qCurrent * localRotationBeforeDelta;
```

This is equivalent to multiplying by `dq` when the platform cache is coherent, but the explicit local-rotation form is the contract.
The rotation update runs before the KCC fixed movement solve, so input, contact sweeps, carrier motion, and camera feedback all see the same hull-relative frame.

Interaction hits against platform-owned colliders must be cached in platform-local space at raycast publication time. The queued signal is rehydrated to AUP hit coordinates only at dispatch time:

```csharp
localHit = platformTransform.InverseTransformPoint(runtimeHit);
runtimeHitAtDispatch = platformTransform.TransformPoint(localHit);
signal.HitPoint = ToAbsoluteUniversePosition(runtimeHitAtDispatch);
```

This prevents "grab lag" when a submarine translates or rolls between the raycast frame and the interaction dispatch frame.

Dropped items inside active `BaseModule` hazard bounds or submarine fallback bounds inherit platform point velocity before their spawn velocity-change packet is queued.

## Hydrodynamic Wake Turbulence

`VehicleMotor` owns a fixed-size `NativeArray<HydrodynamicWakeSample>` ring.
When submerged vehicle speed exceeds 15 m/s, it writes a prop-wash sample behind the hull.
Samples decay by lifetime, not managed timers.

KCC consumption:

```csharp
if (VehicleMotor.TrySampleAnyHydrodynamicWake(playerCenter, out wakeAcceleration))
    QueueSubsystemExternalAcceleration(wakeAcceleration);
```

The player samples wake acceleration before queued external kinematic forces are integrated.
Dry interiors reject wake injection.
No managed collection growth is allowed on the wake path.

## Cinematic Heaviness

Purpose:
- make underwater vehicles feel heavy without frame-history buffers
- keep dry-space KCC response immediate

Owner:
- player: `HectonPlayerMotor`
- vehicle: `VehicleMotor`
- camera: `CameraJuiceProcessor`

Rules:
- perceived velocity is the current safe velocity
- vehicle acceleration is scaled down by `cinematicAccelerationScale`
- underwater damping is raised by `cinematicDragScale`
- camera FOV uses delayed `math.lerp` against current speed only

Reference presentation equation:

```csharp
speed01 = smoothstep(10f, 22f, currentSpeed);
targetFovOffset = speed01 * SpeedFovMaxDegrees;
fovOffset = math.lerp(fovOffset, targetFovOffset, 1f - exp(-sharpness * dt));
```

## Added Mass Tensor

`GlobalPhysicsStateManager` owns underwater angular inertia scaling for registered rigidbodies.
Vehicles publish submersion; the global manager captures dry baselines once, applies the tensor multiplier while submerged, and restores the baseline when submersion returns to zero or the body unregisters.

Reference kernel:

```csharp
multiplier = 1f + 0.35f * submersionFactor;
body.angularDamping = dryAngularDamping * multiplier;
body.inertiaTensor = dryInertiaTensor * multiplier;
body.inertiaTensorRotation = dryInertiaTensorRotation;
```

This is physical rigidbody inertia, separate from the cinematic FOV-only speed presentation.

## Analytical Depth Drag

Vehicle hydrodynamic damping uses the analytical drag form rather than Unity linear drag:

```csharp
kDrag = baseK * math.log10(1f + depthMeters / 100f);
if (length(velocity) < 0.001f)
    velocity = float3.zero;
velocity = velocity / (1f + kDrag * length(velocity) * fixedDeltaTime);
```

Depth scaling is clamped non-negative.
At zero depth, this term contributes no depth-density drag.

## Heavy Impact Resolver

Heavy submarine contacts are intercepted before PhysX finalizes the pair response.
This path is not a replacement for cinematic speed presentation.

Current owner:
- `PhysicsApplySystem`

Current rule:
1. identify contact pairs involving `GlobalRegistry.Submarine.HullRigidbody`
2. compute impact energy from the dominant hull mass and relative closing speed
3. if energy exceeds the hull-yield threshold:
   - set contact restitution to zero
   - clamp solver impulse per contact
   - force tangential target velocity only
   - queue structural diffusion impact into `SubmarineStructuralGrid`
   - queue an optional Gaussian hull-dent command into `SubmarineStructuralGrid`
   - queue a deferred submarine impact signal into `TraumaDispatcher`

Reference kernel:

```csharp
relativeVelocity = dominantVelocity - otherVelocity;
kineticEnergy = 0.5f * dominantMass * lengthsq(relativeVelocity);
closingSpeed = max(0f, -dot(relativeVelocity, normal));
maxImpulsePerContact = (dominantMass * closingSpeed) / contactCount;
```

Hull-dent publication rule:
- `SubmarineStructuralGrid` may only publish dents when an explicit hull `MeshFilter` is bound
- no dent logic is allowed to guess arbitrary child renderers
- dent math is local-space Gaussian displacement along the struck face normal
- inertial-ghost math remains unchanged
- hull mesh capture uses `Mesh.AcquireReadOnlyMeshData`, never `mesh.vertices`
- capture accepts both common 32-byte interleaved `Position/Normal/UV0` streams and strict separate Float32 streams
- publication uses `Mesh.AllocateWritableMeshData(1)` and `Mesh.ApplyAndDisposeWritableMeshData`
- if an explicit hull `MeshCollider` is bound, the collider reference is cleared and rebound after publication so PhysX sees the updated mesh instead of a stale same-reference assignment

Gaussian dent kernel:

```csharp
safeNormal = normalizesafe(localNormal);
delta = vertex - dentCenter;
normalDistance = dot(delta, safeNormal);
radial = delta - safeNormal * normalDistance;
weight = exp(-lengthsq(radial) / (2f * sigma * sigma));
vertex -= safeNormal * dentDepthMeters * weight;
```

The command is rejected when the vertex is behind the struck face tolerance or outside the authored dent radius.
The job reads the front vertex buffer and writes the back vertex buffer; buffers swap only after `JobHandle.IsCompleted` and `Complete()` in the grid's fixed-step consume window.

## Rigidbody Sleep And NaN Recovery

`GlobalPhysicsStateManager` preserves sleeping rigidbody state across origin shifts and distance-based solver eviction.
Distance kinematic sleep records whether the body was already sleeping before eviction; when the player returns inside the 500 m solver radius, the body is restored to its previous kinematic/collision mode but is not blindly woken if it was sleeping before eviction.

NaN recovery rule:

```csharp
if (!isfinite(position) || !isfinite(rotation) || !isfinite(velocity))
{
    runtimePosition = lastKnownGoodAup.ToRuntimeFloat3();
    rb.position = runtimePosition;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.Sleep();
    CrashTelemetryBuffer.ReportNanPhysicsRecovery();
}
```

The last-good runtime position remains as a fallback, but the authoritative recovery target is the last valid AUP so a post-rebase recovery does not snap to a stale camera-relative coordinate.

Kinetic anomaly telemetry is emitted when tracked-body `deltaVelocity / fixedDeltaTime` exceeds 100 m/s2 outside an origin-shift window. The crash ring stores the AUP position, packed velocity delta, and measured acceleration under `ExportReason.KineticAnomaly`.

## Core AUP Cache Subscription

Core systems with persistent runtime-space `Vector3` caches must implement `IOriginShiftListener` when those cached vectors survive across frames. `FoveatedSimulationManager` owns visual interpolation and Doppler caches, so it subtracts `OriginShiftEventData.ShiftOffset` from `_visualFromPositions`, `_visualToPositions`, and `_lastListenerPosition` during origin shifts after completing outstanding scoring/interpolation jobs.

Non-Mono Core services must not register origin-shift listeners from constructors or field initializers. The owning bootstrap/dispatcher calls an explicit runtime-initialization method, and disposal unregisters idempotently.

## Wall Slide And Depenetration

KCC slide resolution is math-only.
No player locomotion step should rely on dynamic rigidbody penetration solving.

Reference kernel:

```csharp
safeDistance = max(0, hit.distance - skinWidth);
penetrationDepth = max(0, skinWidth - hit.distance);
position += direction * safeDistance;
position += hit.normal * penetrationDepth;

remainingAfterAdvance = remainingDisplacement - advance;
slide = remainingAfterAdvance - hit.normal * dot(remainingAfterAdvance, hit.normal);
```

The same projection rule is used for deferred scheduled sweeps:

```csharp
projectedVelocity = velocity - hit.normal * dot(velocity, hit.normal);
```

## Voxel Collider Publish Chain

The async bake owner is `HectonVoxelEngine`, not `VoxelDeltaProcessor`.

Current publish order:
1. visual mesh rebuild finishes
2. collider chunk mesh is uploaded into the staging mesh
3. `VoxelMeshBakeJob` calls `Physics.BakeMesh(meshId, false)`
4. async wait happens off the gameplay hot path
5. only after completion is the staged collider mesh published to `MeshCollider.sharedMesh`

Reference chain:

```csharp
Mesh chunkMesh = volume.GetOrCreateColliderChunkBakeMesh(chunkIndex);
UploadColliderMesh(...);

JobHandle bakeHandle = new VoxelMeshBakeJob
{
    MeshId = chunkMesh.GetEntityId(),
    Convex = false
}.Schedule();

await AwaitForJobCompletionAsync(bakeHandle, ct);
volume.PublishColliderChunkMesh(chunkIndex);
chunkCollider.enabled = true;
```

Stale collision stays live until the baked replacement is ready.
Visual truth may lead collision truth during the bake window by design.

## Obsolete Guidance

Obsolete:
- any document claiming `HectonPlayerMovement` requires Unity `CharacterController`
- any document describing immediate collider replacement before bake completion

Current canonical sources:
- this document
- `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md`
- code owners listed above
