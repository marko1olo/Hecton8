# SHINOBU_332 Log

## 2026-05-22 - Session Start

What was wrong: Assignment memory was not established on disk. No `Status_SHINOBU_332.md`, `Rationale_SHINOBU_332.md`, or `LOG_SHINOBU_332.md` existed.
What was done: Extracted the exact SHINOBU_332 XML block by CLI, read the domain map, selected the relevant mandate files, and created status/rationale/log artifacts.
Cinematic Cheats used: None yet; planned policy is physical truth only for corrective torque and shader/GPU fake for cockpit horizon.
Exact Microseconds saved: PENDING VERIFICATION. No runtime code has been changed yet.

## 2026-05-22 - Submarine Pitch/Roll Auto-Level Static Implementation

What was wrong: Submarine pitch/roll stabilization lived as direct gyro strength/damping inside `Submarine6DIntegratorJob`, with no independent ARM64-aligned tuning DTO, no force packet artifact, no blackbox ring, and no static proof that Euler/joint stabilizers were absent from the scoped runtime.

What was done: Added `SubmarineGyroDTO` exact 32-byte layout, gyro error/force/visual/profile/counter/telemetry DTOs, and Burst deterministic jobs: `GenerateMockTurbulenceJob`, `CalculateGyroscopicErrorJob`, `EvaluatePdControllerJob`, and `RecordGyroTelemetryJob`. Converted `SubmarineDynamicsRuntime` to a partial owner, added `SubmarineDynamicsRuntime_Gyroscopes.cs`, allocated DataVault BufferID lanes, and scheduled the gyro pipeline between added-mass tensor generation and 6D integration. Removed old direct cross-product gyro torque from the integrator; it now consumes the per-frame gyro force accumulator flag. Added GPU structured-buffer sync for artificial horizon data, editor torque/error gizmos, cold `vehicle_gyro_profiles.csv` ingestion, `Submarine Auto-Level Tuner`, and `Euler_Angle_Scanner`. Updated `PHYSICS_OPTIMIZATION_REPORT.json`, sidecar report, and `SHINOBU_332_SELF_AUDIT.xml`.

Cinematic Cheats used: The cockpit artificial horizon is a Dear Lie: CPU uploads `SubmarineGyroVisualStateDTO` error/effort vectors to `_H8SubmarineGyroVisuals`; shader/UI can rotate pixels without CPU UI transform work. Low-tier math fakes full hydrodynamic inertia by falling back to diagonal angular tensor through continuous `GlobalQualityWeight`.

Exact Microseconds saved: Profiler measurement not executed. Static expected savings: removes Unity joint solver participation and hot Euler/Transform/Rigidbody routes; low-tier avoids full tensor inverse when `ResolveTensorBlend` collapses to diagonal. CPU sample was 87.18%, so dotnet build/profiler proof was deferred by project policy.

Verification: `git diff --check` returned line-ending warnings only. Shared and sidecar JSON parsed with `ConvertFrom-Json`. Focused runtime scan found no `AddTorque`, `Rigidbody`, `Transform.rotation`, `.eulerAngles`, `Mathf.LerpAngle`, `Allocator.Temp`, `MemClear`, or `.Complete(` in the gyro hot-path files. Scoped executable Euler/joint stabilizer count is zero; two remaining strings are non-executable editor/report text outside the runtime route.

## 2026-05-22 - Ultra Polish Audit Response

What was wrong: The first pass still had weak proof around presentation and cold ingest. The visual DTO carried `double3` despite being GPU-facing, `SyncGyroVisualBuffer` could allocate/resize a `GraphicsBuffer` from `LateFrameTick`, the upload used `SetData`, and CSV ingest ignored the declared Vault scratch lane by using `stackalloc`. The force packet route also needed a written architecture card because the prompt's literal `NativeQueue` wording differs from the existing submarine owner route.

What was done: Repacked `SubmarineGyroVisualStateDTO` to shader-safe float/uint lanes. Moved `GraphicsBuffer` creation/resizing into `EnsureGyroVisualGraphicsBuffer` on the Vault/capacity setup path; visual sync now performs no resource allocation, skips duplicate telemetry frames, and uploads with `LockBufferForWrite` plus `UnsafeUtility.MemCpy`. CSV profile bytes now stage through `BufferID.Shinobu332GyroCsvScratch`, and the simulation lock path no longer locks the scratch lane. Added a compatibility fence so `SubmarineAutoLevelBallastController` resets its legacy PID state and refuses `PhysicsForceRouter.QueueTorque` while the SHINOBU_332 DataVault gyro runtime is active. Added `Docs/ARCHITECTURE/SHINOBU_332_SUBMARINE_GYRO_ROUTE_CARD.md`, updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, expanded the `<SELF_AUDIT>` to list all 20 tasks, and added route/visual/csv proof fields to shared and sidecar physics reports.

Cinematic Cheats used: Cockpit stabilization remains a shader-side Dear Lie. The CPU sends only 64-byte scalar rows; dashboard pixels can rotate, shimmer, and show stabilizer load without any 3D compass transform or UI geometry rebuild.

Exact Microseconds saved: Profiler capture still pending. Static bound improved: visual upload is capped at `16 * 64 = 1024` bytes per new simulation frame, duplicate-frame uploads are skipped, hot GPU resource allocation from `LateFrameTick` is removed, and the legacy PhysX PID torque queue is bypassed when SHINOBU_332 is active. Force dispatch avoids queue atomics by using one deterministic packet/accumulator slot per vehicle.

Verification: Attribute-aware XML extraction returned `TASK_COUNT=20`. Shared/sidecar JSON parsed and self-audit XML parsed. Focused scan shows no `SetData(`, `stackalloc byte`, visual DTO double lane, Euler/AddTorque/Transform rotation, temp allocator, memclear, or hidden complete in SHINOBU_332 runtime files. `SubmarineGyroVisualStateDTO` no longer contains `double3`; `CurrentAup` remains only in error/force forensic DTOs and editor gizmo origin. Legacy Gameplay PID torque line remains only behind `!IsShinobu332GyroRouteActive()`. `git diff --check` returned line-ending warnings only. Build not launched because CPU sampled 87.58% with project policy ceiling 50%.

## 2026-05-22 - Double Buffer And Compile-Wall Truth Pass

What was wrong: The previous visual upload still used a single `GraphicsBuffer`, violating the project bandwidth discipline rule that all GPU data must be double-buffered. The compile guard prose also implied a dedicated vehicle runtime asmdef that is not present in the current tree.

What was done: Split the gyro visual upload resource into `_gyroVisualBufferA` and `_gyroVisualBufferB`, created only in the Vault/capacity setup path with canonical `COLD ALLOC:` comments. `SyncGyroVisualBuffer` now writes the inactive buffer, unlocks it, binds that buffer to `_H8SubmarineGyroVisuals`, and flips the write index. Verified runtime scripts remain under the existing root `Hecton8.Core.asmdef`; no asmdef was added or changed.

Additional correction: the legacy `SubmarineAutoLevelBallastController` fence no longer calls global `TryGetLatest(out _)`. It refreshes a cached `_shinobu332GyroRouteActive` byte through an entity-validated route, refuses legacy torque for the matched submarine, and stops suppressing kinematic pitch input while SHINOBU_332 owns stabilization.

Cinematic Cheats used: Same artificial-horizon shader fake, now double-buffered. The CPU still emits only scalar stabilizer rows; GPU owns visual overkill.

Exact Microseconds saved: No profiler measurement. Static risk removed: avoids possible driver synchronization/stall from writing a buffer the GPU may still read. Upload ceiling remains `1024 bytes` per new simulation frame.

Verification: Root `AGENTS.md`, SHINOBU_332 XML prompt, global authority docs, and SHINOBU_332 ledger entry were re-read before mutation. Runtime asmdef search found no `Physics/Vehicles` or `Gameplay` asmdef; editor scanner asmdef is isolated.

## 2026-05-22 - Subagent Collision And Fence Repair

What was wrong: Static audit found three non-negotiable defects. SHINOBU_332 draft Vault IDs overlapped terrain pager ownership at `71740..71742`; gyro telemetry was uninitialized while OnDisable could read it before the first frame; the legacy ballast fence was global enough to suppress unrelated controllers and stale enough to allow one fixed-tick duplicate torque.

What was done: Moved SHINOBU_332 Vault lanes to `71780..71787` in `H8Memory` and all route/report artifacts. Switched the 300-frame gyro telemetry ring to cold `ClearMemory` and added a `_frameCounter == 0u` dump guard. Added `SubmarineDynamicsRuntime.TryGetActiveGyroRouteForEntity(uint)` and changed the legacy ballast controller to refresh with hull/fallback entity hashes before PID schedule and torque apply.

Cinematic Cheats used: No new visual cheat in this repair pass. Existing artificial-horizon shader fake remains double-buffered and bounded to 1024 bytes per new frame.

Exact Microseconds saved: Not a performance pass. Collision repair prevents Vault alias corruption; telemetry fix costs one cold 19,200-byte clear; entity validation costs two hash checks on the legacy fixed path and prevents duplicate torque work.

Verification: Static scan now shows current SHINOBU_332 Vault IDs only as `71780..71787`; remaining `71735..71742` strings are explicit rejected-draft documentation. Shared/sidecar JSON parsed, self-audit XML parsed, focused hot-token scan returned no matches, and `git diff --check` returned line-ending warnings only. Dotnet build was not launched because latest CPU sampled 54% with 7 dotnet processes active.
