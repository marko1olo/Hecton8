# KINEMATICS_AUP_INTEGRATION

Mandates followed:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Scope

This document is the canonical runtime map for:
- player KCC ownership
- transport-relative frames
- AUP-to-runtime rebasing
- hydrodynamic added mass
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
  - hydrodynamic inertial ghost state
- `VehicleMotor`
  - mountable vehicle KCC owner
  - vehicle-added-mass and depth-scaled drag
- `HectonFloatingOrigin`
  - AUP/runtime rebase ownership
  - drift sentinel ownership
- `HectonVoxelEngine`
  - async Marching Cubes mesh build
  - async `Physics.BakeMesh` publish chain
- `PhysicsApplySystem`
  - deferred force-packet application
  - pre-solver heavy-submarine contact modification
  - deferred submarine-impact trauma dispatch

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

## Inertial Ghost

Purpose:
- simulate added water mass without using `Rigidbody.drag`
- keep dry-space KCC response immediate

Owner:
- player: `HectonPlayerMotor`
- vehicle: `VehicleMotor`

Storage:

```csharp
NativeArray<float3> _hydrodynamicGhostVelocityHistory; // length = 4
```

History semantics:
- slot count = 4
- write index advances each fixed step
- oldest readable sample = `history[writeIndex]`
- when the array is full, that sample is effectively 3 frames old

Blend equation:

```csharp
ghostBlend = 0.15f * submersionFactor;
perceivedVelocity = math.lerp(currentVelocity, oldestVelocity, ghostBlend);
```

Rules:
- if `ghostBlend <= 0.0001f`, reset the history and use current velocity directly
- this is locomotion-only inertia shaping, not a rigidbody mass mutation
- player `HectonPlayerMovement` only feeds submersion into the owner; it does not own the history buffer
- contact-modification and impact-trauma math do not alter the inertial-ghost blend rule

## Heavy Impact Resolver

Heavy submarine contacts are intercepted before PhysX finalizes the pair response.
This path is not a replacement for the inertial ghost.

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
