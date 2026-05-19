# LOG SHINOBU_113

## 2026-05-19 - Hydrodynamic KCC Static Implementation Pass

What was wrong:
- Legacy player/vehicle movement still exposes Rigidbody presentation routes and old synchronous compatibility jobs.
- The target KCC domain lacked a clean 64-byte AUP movement DTO, deferred capsule command pipeline, hydrodynamic analytical integrator, rollback fence, wake emission, and designer tuning facade.
- Existing local kinematics structs used `Pack = 1` in explicit layouts; `SdfSqueezeResult` exposed hot state through an `IsActive` property.

What was done:
- Added `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`.
- Added `KinematicStateDTO` as `[StructLayout(LayoutKind.Explicit, Size = 64)]` with required offsets: `double3 AUP_Position` at 0, `float3 Velocity` at 24, `float3 AngularVelocity` at 36, `float Mass` at 48, `float DragCoefficient` at 52, explicit pad bytes 56-63.
- Added Burst jobs for deterministic mock input, analytical hydrodynamic integration, capsule command build, post-simulation resolution, rollback MemCpy fence, visual EWMA sync, and wake signal emission.
- Added Vault buffer IDs `ShinobuHydroKccStates` through `ShinobuHydroKccDebugOutputs`.
- Added `HydrodynamicKccTunerWindow` under UI Toolkit for editor-side tuning DTO control and telemetry graph.
- Added allocation-free `ReadOnlySpan<byte>` CSV parser with FNV-1a profile hashes and vault-compatible flat hash buckets.
- Removed `Pack = 1` from directly touched explicit-layout kinematics structs and replaced `SdfSqueezeResult.IsActive` with static `IsResultActive(in result)`.
- Updated `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md` with the SHINOBU_113 seam.

Cinematic Cheats used:
- Replaced expensive water displacement with analytical drag plus scalar turbulence.
- Wake output is an unmanaged signal packet, not spawned GameObjects.
- Visual smoothness is handled by late EWMA interpolation, not by increasing authoritative simulation frequency.

Exact microseconds saved:
- Deferred capsule batch avoids estimated 20-150 us blocking sweep stalls per controlled body in dense collision frames.
- Property-copy removal and pointer mutation save estimated 1-4 us per 1k state updates.
- Low-quality 2-pass resolution can skip up to 6 projection passes per contact versus Ultra.
- Dear Lie water resistance avoids millisecond-scale CPU fluid approximation if compared to naive particles or mesh water displacement.
- Collision command/result `UninitializedMemory` avoids O(n) zeroing of command pools.

Verification state:
- `git diff --check` passed for touched files, with only CRLF warnings.
- Static grep found no `CharacterController` or `Physics.CapsuleCast/SphereCast` in the target KCC path.
- Static grep found no `Pack = 1`, hot DTO properties, `AddForce`, `Complete`, `Run`, or local `new NativeArray` in the new KCC file.
- Compile was not launched. Guard samples reported CPU `79.45-88.68%` first, then `78.60-86.86%` after static cleanup, while `dotnet/csc` were absent. Project law forbids `dotnet build` under CPU load above 50%.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Static scan logged no first-party CharacterController in target set and identified remaining MovePosition legacy routes as presentation/compatibility debt.</TASK>
    <TASK id="02" status="PASS">New KCC route uses deferred CapsulecastCommand.ScheduleBatch and contains no Physics.CapsuleCast/SphereCast hot path.</TASK>
    <TASK id="03" status="PASS">KinematicStateDTO is flat unmanaged state and integration/resolution mutate via UnsafeUtility.AsRef.</TASK>
    <TASK id="04" status="PASS">HydrodynamicKccLayoutValidator checks UnsafeUtility.SizeOf and exact offsets.</TASK>
    <TASK id="05" status="PASS">GenerateMockMovementInputJob and queue variant use deterministic Unity.Mathematics.Random seeded by sector/frame/index.</TASK>
    <TASK id="06" status="PASS">HydrodynamicIntegrationJob uses v = v / (1 + drag * |v| * dt), depth buoyancy, finite guards, deterministic Burst.</TASK>
    <TASK id="07" status="PASS">Simulation schedules command build and CapsulecastCommand batch without waiting.</TASK>
    <TASK id="08" status="PASS">Dear Lie maps speed to nonlinear drag and turbulence scalar; no CPU water displacement simulation.</TASK>
    <TASK id="09" status="PASS">KinematicResolutionJob projects velocity along collision normal and writes final AUP.</TASK>
    <TASK id="10" status="PASS">Final AUP update is millimeter-quantized.</TASK>
    <TASK id="11" status="PASS">Iterations use math.lerp(2, 8, GlobalQualityWeight), no hardware binary switch.</TASK>
    <TASK id="12" status="PASS">Rollback fence copies contiguous KinematicStateDTO bytes from Vault state into Vault rollback bytes.</TASK>
    <TASK id="13" status="PASS">KinematicVisualSyncJob outputs EWMA local float3 visual state.</TASK>
    <TASK id="14" status="PASS">EmitWakeSignalsJob pushes WakeGeneratedSignal through SignalBus ParallelWriter.</TASK>
    <TASK id="15" status="PASS">Capsule command/result buffers are requested from GlobalDataVault with UninitializedMemory.</TASK>
    <TASK id="16" status="PASS">300-entry KinematicTelemetryEntry ring and NaN dump path are implemented.</TASK>
    <TASK id="17" status="PASS">UI Toolkit Hydrodynamic KCC tuner reads/writes Vault tuning DTO and renders telemetry graph.</TASK>
    <TASK id="18" status="PASS">CSV parser is span/FNV based and writes to vault-compatible profile/bucket arrays. NativeHashMap was rejected because IDataVault does not own persistent NativeHashMap handles.</TASK>
    <TASK id="19" status="PASS">Solver writes debug DTO; gizmo draws current capsule, predicted capsule, and collision normal.</TASK>
    <TASK id="20" status="FAIL">Compile verification is blocked by CPU guard. No completion claim is made.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64">
      <field name="AUP_Position" offset="0" size="24" />
      <field name="Velocity" offset="24" size="12" />
      <field name="AngularVelocity" offset="36" size="12" />
      <field name="Mass" offset="48" size="4" />
      <field name="DragCoefficient" offset="52" size="4" />
      <field name="_pad0.._pad7" offset="56" size="8" />
    </KinematicStateDTO>
    <KinematicTelemetryEntry size="64" />
    <HydrodynamicKccTuningDTO size="64" />
    <FalseSharing>No atomic counters were introduced. Shared cursor is a single-element diagnostic write by index 0 only; no parallel atomic counter cache line is used.</FalseSharing>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below GlobalQualityWeight 0.3, resolver iterations collapse toward 2, acceleration/added-mass scalar uses cheaper low-weight lerps, visual sync alpha is reduced, and Dear Lie turbulence remains a scalar. At weight 1.0, resolver reaches 8 projection passes and wake scalar carries richer downstream GPU/audio information.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PrivatePersistentNativeArrays>0 in HydrodynamicKccRuntime; only VaultBufferHandle fields are cached.</PrivatePersistentNativeArrays>
    <VaultBufferHandles>ShinobuHydroKccStates, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, CsvScratch, DebugOutputs</VaultBufferHandles>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Applied to NativeArray fields in new Burst jobs and SdfSqueezeJob where arrays are independent.</NoAlias>
    <Graph>GenerateMockMovementInputJob -> HydrodynamicIntegrationJob -> BuildCapsuleCastCommandsJob -> CapsulecastCommand.ScheduleBatch -> KinematicResolutionJob -> KinematicVisualSyncJob + KinematicRollbackFenceJob + EmitWakeSignalsJob -> LateFrame non-blocking DispatcherJobSwap.TryComplete.</Graph>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    New files did not introduce a new sibling runtime asmdef reference. Existing root Hecton8.Core asmdef debt is unchanged. dotnet build not run because CPU guard stayed above 50%.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy CPU fluid simulation was rejected. Before: O(particles or mesh fluid samples) per frame with likely ms-scale cost. After: O(entities) scalar analytical drag plus unmanaged wake packet, with GPU/audio systems consuming turbulence.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Static Risk Closure Before Compile Gate

What was wrong:
- `RaycastHit.normal` was relying on implicit UnityEngine/Mathematics conversion assumptions inside the Burst resolver.
- Capsule command endpoints/direction were relying on implicit `float3 -> Vector3` conversion assumptions.
- `QueryParameters` received `_collisionMask` instead of `_collisionMask.value`.
- The black-box dump path could rewrite the same fault mask every LateFrame if a non-finite state persisted.

What was done:
- Replaced the implicit normal conversion with explicit `new float3(hitNormal.x, hitNormal.y, hitNormal.z)`.
- Replaced capsule command endpoint/direction arguments with explicit `Vector3` structs before constructing `CapsulecastCommand`.
- Replaced `new QueryParameters(_collisionMask, ...)` with `new QueryParameters(_collisionMask.value, ...)`.
- Added `_dumpedFaultMask` so `Dump_KINEMATICS_SURGEON.bin` is written once per distinct fault mask while preserving the fault flag for diagnostics.

Cinematic Cheats used:
- No new physical simulation. The KCC still sells water via analytical drag, turbulence scalar, wake signal, and EWMA visual smoothing.

Exact microseconds saved:
- Healthy frames: no measurable new cost.
- Faulted persistent NaN frames: avoids repeated 19.2 KB managed copy and file write per LateFrame after the first dump for that fault mask.

Verification state:
- `git diff --check` passed for tracked SHINOBU_113 files with only CRLF warnings.
- Targeted grep found no `Pack = 1`, DTO auto-properties, `Complete`, `Run`, local `new NativeArray`, synchronous `Physics.CapsuleCast/SphereCast`, `CharacterController`, or `AddForce` in the new KCC/SDF target path.
- `dotnet build` remains prohibited: latest CPU utility samples were `85.62, 86.86, 83.29, 78.60, 79.24`; no `dotnet` or `csc` process was active.

## 2026-05-19 - Teardown Ownership Patch

What was wrong:
- `OnDisable` drained only post-simulation/collision handles. During a disable between scheduling stages, command/integration/input handles could remain implicit and make Vault alias ownership harder to reason about.

What was done:
- Added `DrainPendingJobsForTeardown()` and call it before lane unregister. It drains post, collision, command, integration, and input handles through `DispatcherJobSwap.TryComplete(forceComplete:true)`.

Cinematic Cheats used:
- None; this is a memory ownership fix.

Exact microseconds saved:
- Healthy frame cost: 0 us. Disable/hot-swap path cost is bounded by outstanding job work and prevents undefined ownership rather than saving steady-state frame time.

Verification state:
- Build still not launched. Latest CPU utility samples were `87.48, 88.60, 90.77, 91.41, 87.64`; no `dotnet` or `csc` process was active.

## 2026-05-19 - Rollback Seam Tightening

What was wrong:
- The KCC had a contiguous rollback byte fence, but no callable fast-forward entry point for rollback owners.
- Adding a direct reference to netcode runtime would have been a compile-wall violation.

What was done:
- Added `TryRunRollbackResimulation(int requestedFrames, float fixedDeltaTime)`.
- The method drains outstanding work, runs fixed/post KCC stages for a quality-budgeted frame count, force-completes only inside this explicit rollback path, and marks visual sync bypass frames.

Cinematic Cheats used:
- Presentation smoothing is bypassed during rollback resim. The player sees the corrected state instead of an EWMA lie.

Exact microseconds saved:
- Normal path: 0 us extra cost.
- Low quality rollback clamps to one replay frame per call, avoiding up to seven replay steps compared to the default 8-frame upper seam.

Verification state:
- Static only. Build remains blocked by CPU guard.

## 2026-05-19 - Layout Proof Tightening

What was wrong:
- The first layout validator used `Marshal.OffsetOf`. Correct, but not the exact proof path requested by Task 04.

What was done:
- Replaced the helper with `UnsafeUtility.GetFieldOffset` over a cold reflection field lookup.
- Missing fields return `-1`, making the validator fail closed.

Cinematic Cheats used:
- None; structural proof only.

Exact microseconds saved:
- Runtime: 0 us. This is cold validation.

Verification state:
- Static only. Build still blocked by CPU guard.

## 2026-05-19 - Compile Guard Recheck

What was wrong:
- Build verification is still required for Task 20, but the host remains above the project CPU threshold.

What was done:
- Rechecked `dotnet/csc`: no active process.
- Rechecked CPU utility: `96.94, 99.95, 93.34, 99.53, 92.71`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change. The guard prevents a compiler spike on a saturated workstation.

Verification state:
- `dotnet build` not launched because CPU exceeded 50%.

## 2026-05-19 - Compile Guard Recheck 2

What was wrong:
- The previous high utility readings were rechecked against both CPU counters before deciding whether to build.

What was done:
- Rechecked `dotnet/csc`: no active process.
- Processor Time samples: `91.84, 100.00, 99.57, 99.44, 100.00`.
- Processor Utility samples: `92.03, 98.50, 96.96, 96.86, 96.05`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build remains forbidden by the >50% CPU guard.
