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
- [ ] 4. [DEBRIS_BUFFER_SOA] Define `_DebrisBuffer` and `_DebrisPhysicsBuffer` layout verification.
- [ ] 5. [INJECTION_JOB] Burst injection into dead slots.
- [ ] 6. [COMPUTE_ADVECTION] Fluid/SDF compute advection.
- [ ] 7. [DETERMINISTIC_TUMBLE] Shader hash tumble only.
- [ ] 8. [LOW_TIER_FAKE] MX350 cap and collision skip.
- [ ] 9. [HIGH_END_OVERKILL] RTX 16384/full collision tier.
- [ ] 10. [REACTIVE_VFX] Dust scale-down on expiry.
- [ ] 11. [STP_STABILIZATION] Motion vector validation.
- [ ] 12. [NAN_VACCINATION] Signal NaN discard.
- [ ] 13. [BLACKBOX_LOGGING] ActiveDebrisCount telemetry ring.
- [ ] 14. [TRIPLE_STRIKE_REPAIR] Double-buffer staging if SetData stalls.
- [ ] 15. [HOMEOSTASIS_ADAPTATION] Stress-based lifetime reduction.
- [ ] 16. [INDIRECT_DRAW_CALL] Single rock-chip indirect draw validation.
- [ ] 17. [AUP_REBASE] Atomic `_AupShiftOffset` handling.
- [BLOCKED BY DEPENDENCY] 18. [FINAL_VALIDATION] `dotnet build` exits 0. Blocker: external domain compile drift. First build failed in fauna bite IK; second build failed in docking autopilot, VFX wakes, and ecosystem macro swarm contracts. This is outside DEBRIS/VFX ownership.

Compile:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Result 1: FAILED, 4 errors, all in `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs`.
- Result 2: FAILED, 14 errors in external domains:
  - `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: unresolved `IDockingAutopilotService`.
  - `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs`: unresolved `ActiveSplineData` and `IDockingAutopilotService`.
  - `Assets/_Project/Scripts/World/FloraInteractionManager.cs`: unresolved `Hecton8.VFX.Wakes`, `WakeSource`, and `WakeTelemetryEntry`.
  - `Assets/_Project/Scripts/World/EcosystemDirector.cs`: missing implementations for new macro swarm methods on `IEcosystemDirectorService`.
- Targeted debris `git diff --check` result: no whitespace errors; only line-ending warnings from existing worktree settings.

State:
- ACTIVE. Phase 1 source purge complete; final compile blocked by external fauna dependency.
