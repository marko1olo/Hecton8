# Status_DEBRIS_PHYSICS_FAKE

Prompt extraction evidence:
- `Docs/Tasks/CURRENT_BATCH.md` was searched with a CLI regex for `<AGENT_PROMPT id="DEBRIS_PHYSICS_FAKE">`.
- Result: active XML prompt found.
- Identity line: `PROMPT IDENTIFIED: DEBRIS_PHYSICS_FAKE | DOMAIN: VFX/COMPUTE | TASK COUNT: 18`.
- Domain boundary from prompt: `Assets/_Project/Scripts/VFX/Debris/`; Phase 1 required narrow cross-domain producer cleanup in mining/bootstrap/signal code.

Phase 1 checklist:
- [x] 1. [PURGE_INSTANTIATE] Scan mining/impact scripts and eradicate GameObject debris paths. Justification: removed CPU dropped-item debris aftermath from `VoxelDeltaProcessor`, removed legacy laser `IDebrisService.SpawnBurst`, and converted voxel carve/outcrop/drill/player-impact/vehicle-impact debris producers to `DebrisSpawnSignal.FlagComputeShard`. DOD practice: visual fake first, zero GameObjects, signal lane decoupling. Alternative rejected: pooling dropped items or keeping `IDebrisService` fallback. Static estimate: 40-250 us saved on burst-heavy mining frames before profiling.
- [x] 2. [SINGLETON_KILL] Register compute debris through GlobalRegistry. Justification: added `IDebrisComputeService`, `GlobalRegistryServiceSlot.DebrisComputeRuntime`, renderer self-registration, and bootstrap lookup through `GlobalRegistry.DebrisCompute`. DOD practice: GlobalRegistry contract, no direct singleton dependency. Alternative rejected: `DebrisManager.EnsureRuntimeInstance()` and direct renderer references from bootstrap. Static estimate: 5-30 us saved in service/lifecycle work during scene startup or mining spikes.
- [x] 3. [DATA_EVICTION] Use `BufferID.CarveDebris` in GlobalDataVault. Justification: compute renderer remains the owner of `BufferID.CarveDebris`, `CarveDebrisVelocity`, `CarveDebrisJobState`, `CarveDebrisBlackBox`, and `CarveDebrisRequests`; scene cleanup now calls `ClearGpuDebris()` instead of legacy CPU debris clearing. DOD practice: DataVault buffer ownership and fixed GPU path. Alternative rejected: producer-owned arrays or PersistentWorldRegistry debris items. Static estimate: 20-120 us saved by avoiding CPU object state churn during carve bursts.

Remaining checklist:
- [x] 4. [DEBRIS_BUFFER_SOA] Define `_DebrisBuffer` and `_DebrisPhysicsBuffer` layout verification. Justification: render shader now declares canonical `_DebrisBuffer` (xyz position, w life) and `_DebrisPhysicsBuffer` (xyz velocity, w reserved rotation), bound from the DataVault-backed position/velocity GPU buffers. DOD practice: SoA, 16-byte float4 stride, indirect-compatible. Alternative rejected: interleaved per-particle class/struct payload. Static estimate: 10-40 us avoided versus mixed CPU object state upload.
- [x] 5. [INJECTION_JOB] Burst injection into dead slots. Justification: `CarveDebrisInjectBatchJob` consumes typed `DebrisSpawnSignal`/`VoxelCarveEvent` requests, selects dead slots where Life <= 0, and writes randomized finite velocity. DOD practice: Burst job, bounded request buffer, no managed EventBus. Alternative rejected: per-signal GameObject spawn or unbounded append list. Static estimate: 50-180 us saved on burst frames.
- [x] 6. [COMPUTE_ADVECTION] Fluid/SDF compute advection. Justification: `Hecton_FluidAdvection.compute` advects debris with gravity, flow drag, optional dynamic wakes, and SDF collision on non-low tiers. DOD practice: GPU ping-pong buffers and SDF skip on toaster. Alternative rejected: Unity Physics/Rigidbody debris. Static estimate: 0.2-1.5 ms avoided on shard clouds versus CPU physics.
- [x] 7. [DETERMINISTIC_TUMBLE] Shader hash tumble only. Justification: `Hecton_CarveDebrisIndirect.shader` now derives orientation from particle ID plus time in the vertex shader; CPU writes no rotation transforms. DOD practice: deterministic visual fake. Alternative rejected: CPU quaternion per shard. Static estimate: 20-90 us avoided for thousands of shards.
- [x] 8. [LOW_TIER_FAKE] MX350 cap and collision skip. Justification: low tier remains capped at 1024 active shards, 16 particles per carve, and compute skips SDF/flow heavy paths. DOD practice: Dear Lie path. Alternative rejected: full collision on MX350/Quest. Static estimate: 0.3-1.2 ms GPU saved under debris storms.
- [x] 9. [HIGH_END_OVERKILL] RTX 16,384/full collision tier. Justification: high/ultra capacity is now 16,384 with 128 particles per carve and high shader lighting/motion features gated to High/Ultra. DOD practice: hardware-scaled visual overkill. Alternative rejected: one balanced 4096-particle middle path. Static estimate: spends saved CPU budget on 4x shard density.
- [x] 10. [REACTIVE_VFX] Dust scale-down on expiry. Justification: shader scales shards by life and dither-clips fadeout before slot reclamation. DOD practice: visual disappearance fake, no CPU callback. Alternative rejected: spawning secondary dust objects. Static estimate: 30-120 us saved versus secondary emitters.
- [x] 11. [STP_STABILIZATION] Motion vector validation. Justification: debris material now has a MotionVectors pass using current position and velocity-derived previous position; render params no longer force no motion. DOD practice: STP motion history instead of shimmer hiding. Alternative rejected: disabling motion vectors for procedural debris. Static estimate: image-stability gain, microsecond runtime pending GPU capture.
- [x] 12. [NAN_VACCINATION] Signal NaN discard. Justification: signal ingestion rejects non-finite positions/axes/radii, injection validates positions/velocities, and compute kills invalid particles. DOD practice: NaN propagation breaker. Alternative rejected: trusting producer math. Static estimate: crash prevention, runtime cost negligible compared with a GPU reset.
- [x] 13. [BLACKBOX_LOGGING] ActiveDebrisCount telemetry ring. Justification: `CarveDebrisTelemetryEntry` stores active count, queued carves, injected particles, flags, state hash, and AUP shift in a 300-entry DataVault buffer. DOD practice: fixed blackbox ring. Alternative rejected: Debug.Log/string diagnostics. Static estimate: 0 B GC.
- [x] 14. [TRIPLE_STRIKE_REPAIR] Double-buffer staging if SetData stalls. Justification: debris upload path uses `GraphicsBuffer.LockBufferForWrite` into double-buffered position/velocity buffers; no `GraphicsBuffer.SetData` in the hot path. DOD practice: stall-resistant staging. Alternative rejected: SetData full-buffer uploads each frame. Static estimate: avoids intermittent main-thread sync spikes.
- [x] 15. [HOMEOSTASIS_ADAPTATION] Stress-based lifetime reduction. Justification: `SignalBusRegistry.SystemStress01` and `SystemHealthIndexSignal` now drive a 4x lifetime decay when stress > 0.9, equivalent to 75% shorter lifetime. DOD practice: homeostasis load shedding. Alternative rejected: dropping producer signals globally. Static estimate: recycles slots 4x faster during pressure.
- [x] 16. [INDIRECT_DRAW_CALL] Single rock-chip indirect draw validation. Justification: render path uses `Graphics.RenderMeshIndirect` with the fallback octahedron rock chip mesh and compute-written indirect args. DOD practice: one indirect draw, no shard GameObjects. Alternative rejected: DrawMesh per shard. Static estimate: saves thousands of CPU draw submissions.
- [x] 17. [AUP_REBASE] Atomic `_AupShiftOffset` handling. Justification: `AupShiftSignal` is consumed from typed lanes, accumulated as `_CarveDebrisAupShiftDelta`, and applied inside the compute advection pass before integration. DOD practice: GPU-side rebasing. Alternative rejected: CPU rewriting all live shard positions on origin shift. Static estimate: 50-250 us avoided on origin shift frames.
- [BLOCKED BY DEPENDENCY] 18. [FINAL_VALIDATION] `dotnet build` exits 0. Blocker: external domain compile drift. First build failed in fauna bite IK; second build failed in docking autopilot, VFX wakes, and ecosystem macro swarm contracts. This is outside DEBRIS/VFX ownership.

Compile:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Result 1: FAILED, 4 errors, all in `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs`.
- Result 2: FAILED, 14 errors in external domains:
  - `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: unresolved `IDockingAutopilotService`.
  - `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs`: unresolved `ActiveSplineData` and `IDockingAutopilotService`.
  - `Assets/_Project/Scripts/World/FloraInteractionManager.cs`: unresolved `Hecton8.VFX.Wakes`, `WakeSource`, and `WakeTelemetryEntry`.
  - `Assets/_Project/Scripts/World/EcosystemDirector.cs`: missing implementations for new macro swarm methods on `IEcosystemDirectorService`.
- Result 3: FAILED, 39 errors in external domains:
  - `Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs`: unavailable `XRDisplaySubsystem.TryRequestDisplayRefreshRate`.
  - `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultProbeUtility.cs`: generic inference failure.
  - `Assets/_Project/Scripts/HectonItem.cs`: missing `ItemAcquiredSignal`.
  - `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`: missing breach/damage-control members.
  - `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`: missing `_profileFloats`/`_blackBox`.
- Targeted debris `git diff --check` result: no whitespace errors; only line-ending warnings from existing worktree settings.
- Targeted debris static scan result: no `ForceNoMotion`, no `Update()`, no `string.Format`, no `Instantiate`; only DataVault NativeArray leases and job parameters remain.

State:
- ACTIVE. Tasks 1-17 source work complete by static validation; final compile blocked by external domain dependency drift.
