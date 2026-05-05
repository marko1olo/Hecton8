# AGENT_03 Physics Log
Date: 2026-05-04
Status: DEPRECATED


## Mandates Followed
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `PHYS_Kinematic_Interaction_Hands.txt`
- `ANIM_Contextual_Physical_IK.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `PHYS_Fluid_Incursion_Interior.txt`

## Scope
- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`
- `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs`
- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkMath.cs`
- `Assets/_Project/Scripts/PhysicsApplySystem.cs`
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

## Init Profiling Audit
- `SubmarineFluidDynamics.Awake()` / `OnEnable()` contain no `FindObjectsOfType`, `FindWithTag`, or `GetComponentsInChildren` scene scans.
- `ContextualPhysicalIkRuntime.Awake()` / `OnEnable()` contain no scene-wide searches; buffer allocation and registration stay local.
- `AbyssalThermalManager.Awake()` / `OnEnable()` contain no literal scene-wide searches. Dependency resolution uses:
  - static runtime-instance accessors (`BiomeMatrixDirector.ActiveRuntimeInstance`, `WorldZoneDirector.ActiveRuntimeInstance`)
  - cached bootstrap/runtime reference helpers (`WorldRuntimeReferenceUtility.TryResolvePlayerTransform`)
  - local `TryGetComponent` on the already-owned player transform or manager host
- Result: the audited physics/IK owners do not use the forbidden whole-scene search APIs during initialization.

## ILPP Struct Purge
- `ContextualPhysicalIkApplyJob` was carrying duplicated spine and secondary handle/cache arrays by value.
- `PhysicalHandController` was carrying four separate `NativeArray<float3>` ray payloads into Burst jobs.
- All new transport structs use `[StructLayout(LayoutKind.Sequential)]`.

### Size Reductions
Pre-pass values came from Unity reflection before the refactor. Post-pass values are deterministic from the compiled sequential layout and the 48-byte `NativeArray<T>` payload size already verified in the pre-pass.

| Type | Before | After | Delta | Reason |
|---|---:|---:|---:|---|
| `Hecton8.Gameplay.ContextualPhysicalIkApplyJob` | 832 B | 640 B | -192 B | Removed 4 redundant `NativeArray<>` fields (`SpineHandles`, `SecondaryHandles`, `CachedSpinePoseStates`, `CachedSecondaryRotationStates`). |
| `Hecton8.Interaction.PhysicalHandController+BuildFingerSpherecastCommandsJob` | 304 B | 208 B | -96 B | Replaced 4 split float3 arrays with `FingerRayDefinition[5]` + `FingerRayRuntime[5]`. |
| `Hecton8.Interaction.PhysicalHandController+ProcessFingerHitsJob` | 200 B | 152 B | -48 B | Replaced origin/direction arrays with one `FingerRayRuntime` stream. |

### Layout Notes
- `FingerRayDefinition` = 24 B (`float3` + `float3`)
- `FingerRayRuntime` = 24 B (`float3` + `float3`)
- `ThermalVentGpuData` remains 40 B
- `AshParticleData` remains 48 B

## Thermal Buffer Fix
- `AbyssalThermalManager` no longer uses `LockBufferForWrite` for thermal vent or particle uploads.
- Vent metadata now publishes through a 3-slot `GraphicsBuffer` ring:
  - upload target must not be the currently bound slot
  - upload target must either have no armed fence or report `GraphicsFence.passed == true`
  - publish is skipped, not stalled, when every spare slot is still in flight
- Smoke particle reseeds now defer until the last smoke-frame fence has passed, so both particle ping-pong buffers stay GPU-owned until the previous dispatch and optional draw retire.
- `SystemDispatcher.GraphicsBufferUploadUtility` still hard-checks:
  - `destination.stride == UnsafeUtility.SizeOf<T>()`
  - `safeCount = min(requestedCount, sourceLength, destination.count)`
- Result: the old vent-buffer `LockBufferForWrite` overrun path is gone, and the thermal path now uses explicit non-blocking fence checks before CPU-side buffer reuse.

### Triple Buffer Lifecycle
1. `OnEnable()` recreates particle buffers and the 3-slot vent ring if they are absent.
2. `SlowTick()` rebuilds vent topology and marks vent upload / particle reseed dirty when topology changed.
3. `UploadVentBuffer()` only publishes into a non-active slot whose previous fence has already passed.
4. `Tick()` dispatches smoke simulation, optionally draws smoke, then inserts a `GraphicsFence` after the final submitted GPU consumer for that frame.
5. `CanRewriteParticleBuffers()` and `IsVentBufferSlotReusable()` poll `GraphicsFence.passed`; if the GPU has not retired the slot yet, the CPU skips the upload instead of forcing a slow-path lock.
6. `OnDisable()` and `OnDestroy()` both release `_particleBufferA`, `_particleBufferB`, and the full vent ring so disabling the manager cannot leak VRAM across enable/disable cycles.

### Relevant Code Paths
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - `UploadNativeArray<T>()`
  - `UploadArray<T>()`
  - `ResolveSafeWriteCount<T>()`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
  - `CaptureInFlightFences()`
  - `TryResolveReusableVentUploadBuffer()`
  - `CanRewriteParticleBuffers()`
  - `UploadVentBuffer()`
  - `ResetParticles()`
  - `EnsureBuffer<T>()`

## Hydro / Slosh / Barrier Changes
- Exterior buoyancy remains sample-based, not center-of-mass based.
- Sample count stays at 8 discrete points.
- Heavy-object hand lag now gates on `mass > 50 kg` instead of applying virtual-mass drag to lighter carry targets.
- `PhysicalHandController` now uses the integrated virtual-hand velocity as the PD target velocity for the grabbed rigidbody. The old stale-controller-position derivative path was removed.
- Virtual-hand lag stays symplectic Euler:
  - `springForce = (effectiveControllerPosition - virtualHandPosition) * VirtualSpringK`
  - `dampingForce = -virtualHandVelocity * (2 * sqrt(virtualHandMass * VirtualSpringK) * 0.9)`
  - `virtualHandVelocity += ((springForce + dampingForce) / virtualHandMass) * dt`
  - `virtualHandPosition += virtualHandVelocity * dt`
- Added-mass term is now depth-driven for both translational and rotational damping:
  - `linearDamping = baseLinearDamping * (1 + addedMassLinearDampingScale * submersionFactor)`
  - `angularDamping = baseAngularDamping * (1 + addedMassAngularDampingScale * submersionFactor)`
- Exterior buoyancy is applied per sample:
  - `F_i = rho * V_i * g * submersion_i`
  - `tau_i = (p_i - centerOfMass) x F_i`
- Buoyancy NaN guards now trip on:
  - invalid displacement volume
  - invalid sample volume / per-sample force scalar
  - invalid center of mass
  - invalid sample point
  - invalid accumulated submerged volume
  - invalid normalized submersion ratio (`submergedVolume / displacementVolume`)
  - invalid final force / torque vectors
- Delayed slosh torque now uses a 16-sample local angular-velocity ring buffer so the delay tap can move across the requested `50 ms → 150 ms` band at 50 Hz.
- Delay tap math:
  - `delaySeconds = lerp(0.05, 0.15, internalFloodRatio)`
  - `delayFrames = round(delaySeconds / fixedDeltaTime)`
  - `delayedAngularVelocity = angVelHistory[(ringHead - delayFrames - 1) & 15]`
- Delayed slosh torque remains local-space and now explicitly scales by total internal flood ratio:
  - `tau_slosh += -omega_delay * (fillRatio * sloshMass * freeSurface^2)`
  - `tau_slosh *= sloshFactor * internalFloodRatio`

## Double Barrier / Front-Back Ownership
- `SubmarineFluidDynamics.ConsumeCompletedFluidTransfer()` now swaps front/back flood and flag buffers instead of copying them per element.
- `FixedTick()` still consumes the completed back buffer at the start of the step and schedules the next job at the end of the step.
- Authoritative writes still force completion through `CompletePendingFluidTransferForAuthoritativeWrite()` before mutating compartment state.
- `ContextualPhysicalIkRuntime` still schedules ground detection/response into `_backTargetFrames`, then swaps and publishes only after `_pendingGroundResponseHandle` completes.
- `PhysicalHandController.StepFixed()` still completes the previous finger-pose batch at the start of the next fixed step, applies the result, and only then schedules the next batch. No same-method `Schedule()+Complete()` regression was introduced there.
- `PhysicsApplySystem.FixedTick()` is still the single deferred-force flush point:
  - `FlushFrontBuffer()` applies the previous fixed step’s queued packets exactly once.
  - `SwapBuffers()` publishes the current gather buffer to become next step’s front buffer.
  - Result: force application remains one authoritative main-thread sink, one flush per fixed step, one-frame deferred.

## Runtime Mass Audit
- `rg -n "\.mass\s*=" Assets/_Project/Scripts -g "*.cs"` currently returns:
  - `Assets/_Project/Scripts/HectonPlayerMovement.cs:6867` → `_rb.mass = currentSuitData.mass;`
  - `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:333` → `_runtimeHandBody.mass = 1f;` during runtime proxy creation only
  - `Assets/_Project/Scripts/Dev/PhysicalInteractionRuntimeVerifier.cs:328` → `body.mass = 2f;` one-shot dev probe creation
  - `Assets/_Project/Scripts/Dev/PhysicalInteractionRuntimeVerifier.cs:349` → `body.mass = heavyProbeMass;` one-shot dev probe creation
  - `Assets/_Project/Scripts/Editor/...` bootstrap authoring writes (editor only)
- Per-call-site classification:
  - `HectonPlayerMovement.ApplySuitToRigidbody()` is a runtime write, but `rg -n "ApplySuitToRigidbody\(|SetSuit\(" Assets/_Project/Scripts/HectonPlayerMovement.cs` shows it is called from initialization and `SetSuit()` entry points, not from `Tick()` / `FixedTick()`.
  - `PhysicalHandController` mass write is initialization of the hidden articulation proxy, not a per-step mutation.
  - `PhysicalInteractionRuntimeVerifier` writes are dev-only probe setup, not gameplay hot-path mass mutation.
- Result: no per-frame `Rigidbody.mass` mutations were found in the audited runtime paths. One non-frame gameplay runtime write remains in `HectonPlayerMovement` suit application and is outside this submarine/IK ownership slice.

## NaN Guard Injection
- `ContextualPhysicalIkMath.SafeNormalize()`, `ProjectOnPlane()`, and `IntegrateSpringDamper()` now reject non-finite input.
- `PhysicalHandController` now resets grabbed-body motion and breaks the grip on invalid hand/body rotation or angular velocity.
- `SubmarineFluidDynamics` now calls `EmergencyResetHydrodynamics()` when NaN/Inf is detected in:
  - delayed slosh angular velocity capture
  - buoyancy center/sample/result vectors
  - delayed slosh accumulation/result vectors
- Emergency response:
  - `rigidbody.linearVelocity = Vector3.zero`
  - `rigidbody.angularVelocity = Vector3.zero`
  - debug force/torque state cleared
  - guarded error log emitted in editor/development builds

## Compile Evidence
- Unity `Editor.log` recorded:
  - `ExitCode: 0 Duration: 26s`
  - `*** Tundra build success (26.16 seconds), 10 items updated, 2034 evaluated`
  - `AssetDatabase: script compilation time: 29.373503s`
- No compile errors were recorded for the edited files.
- ILPP completed after the struct purge. No `Unity.ILPP.Trigger.exe` stack-buffer-overrun entry was present in the successful compile log.
- Local fallback audit on `2026-04-26`: `dotnet build Assembly-CSharp.csproj` is currently blocked by unrelated pre-existing errors in `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`.
- Focused build-log sweep did not report compiler errors for:
  - `AbyssalThermalManager.cs`
  - `SubmarineFluidDynamics.cs`
  - `ContextualPhysicalIkRuntime.cs`
  - `PhysicalHandController.cs`
- A later local fallback attempt on `2026-04-26` was blocked earlier by unrelated Unity package-cache and UI/Input reference failures before it reached the edited physics files.

## Verification Status
- Thermal vent upload race fix: CODED, compile-audited, runtime PENDING VERIFICATION
- Particle reset fence gate: CODED, compile-audited, runtime PENDING VERIFICATION
- Disable/destroy VRAM release path: CODED, code-audited, runtime PENDING VERIFICATION
- Physics init search audit: PASS (code audit), runtime PENDING VERIFICATION
- Added-mass linear + angular damping: CODED, compile-audited, runtime PENDING VERIFICATION
- 8-point buoyancy sampling: PRESENT, compile-audited, runtime PENDING VERIFICATION
- GC / frame-time / memory-retention measurements: PENDING VERIFICATION
- Remaining `LockBufferForWrite` users still exist in non-thermal systems (`HectonBoidController`, `HectonMarineSnowRenderer`, vegetation, BRG renderers, visor). They were audited as out-of-scope for this thermal/IK pass and remain PENDING VERIFICATION.

## Iteration 20 — AUP Teleport / Collider Bake Audit
- `HectonFloatingOrigin` now snapshots tracked dynamic rigidbodies before the root-transform shift and re-applies their translated runtime position through `Rigidbody.position` before `Physics.SyncTransforms()`.
- `PhysicsApplySystem` is only a routing bridge here. It now forwards the shift phases into the existing tracked-body owner instead of inventing a second rigidbody registry.
- `GlobalPhysicsStateManager` is the actual teleport authority:
  - pre-shift snapshot captures `body.position`, `linearVelocity`, `angularVelocity`, and sleeping state for every tracked non-kinematic rigidbody
  - post-shift commit writes `body.position = capturedPosition - shiftOffset`
  - velocities are restored verbatim
  - previously sleeping bodies are put back to sleep; awake bodies are explicitly woken
  - `_lastValidPositions` is updated to the translated runtime-space position so NaN recovery and post-shift safety state stay coherent
- `SubmarineFluidDynamics` now implements `IOriginShiftListener` and flushes `_angularVelocityHistoryLocal`, `_ringHead`, delayed slosh debug state, and last applied slosh torque on every committed origin shift. This prevents old delayed angular-velocity samples from surviving across an AUP teleport boundary.
- Runtime collider baking audit:
  - `HectonVoxelEngine` was already compliant. It schedules `UnityEngine.Physics.BakeMesh(...)` inside `VoxelMeshBakeJob` and waits asynchronously before rebinding `MeshCollider.sharedMesh`.
  - `HectonWorldGenerator` was not compliant. It previously executed `Physics.BakeMesh(mesh.GetInstanceID(), false)` on the main thread inside `BakePhysicsBatch()`.
  - `HectonWorldGenerator` now uses `HectonPhysicsBakeJob : IJob` and a background bake queue:
    - per-tick phase 1 schedules up to `MAX_BAKES_PER_FRAME` worker jobs
    - per-tick phase 2 only binds a collider after `handle.IsCompleted`
    - stream stop / chunk teardown retires any matching in-flight bake before destroying the mesh owner
- Sleep-threshold audit:
  - `rg -n "sleepThreshold" Assets/_Project/Scripts -g "*.cs"` returned no gameplay/runtime writes.
  - Existing distant-object parking already relies on `isKinematic` / `Sleep()` in `PersistentWorldRegistry`, `WorldFidelityRoot`, and chunk/debris owners rather than lowering `Rigidbody.sleepThreshold`.

## Iteration 22 - AUP Teleport Coverage Audit
- `GlobalPhysicsStateManager.PrepareTrackedBodiesForOriginShiftInternal()` now begins with `RegisterActiveRigidbodiesForOriginShift()`.
- Reason: the previous registry only guaranteed teleport coverage for:
  - scene-load sweeps
  - bodies that had already routed force through `PhysicsApplySystem`
  - bodies explicitly registered through physics-connection owners
  - bodies that already carried `PhysicsStateReporter`
- Gap closed: pooled or runtime-spawned dynamic rigidbodies that became active after scene load could otherwise miss the interpolation-disable wrap during an origin shift.
- The new pre-shift sweep is cold-path only:
  - `FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude)` runs once per origin shift, not per-frame
  - each active non-kinematic rigidbody is registered before snapshot/teleport/finalize
  - result: interpolation disable -> `Rigidbody.position` translate -> velocity restore -> `Physics.SyncTransforms()` -> interpolation restore now covers all live dynamic bodies, not just previously tracked ones
- The tracked-body registry is no longer a hard `512`-body ceiling:
  - `_trackedBodies`, `_bodyStates`, and `_lastValidPositions` now grow on the cold registration path before overflow can drop a live body
  - growth occurs before pre-shift registration, so the AUP teleport wrap does not silently skip bodies when active-body count exceeds the initial allocation
