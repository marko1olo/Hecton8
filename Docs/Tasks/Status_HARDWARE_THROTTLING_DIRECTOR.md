# Status_HARDWARE_THROTTLING_DIRECTOR

## Identity
- Prompt: HARDWARE_THROTTLING_DIRECTOR
- Domain: CORE/HARDWARE
- Task Count: 18
- Active Phase: Phase 1 - The Great Purge

## Mandates Read
- PROJECT_LTS_Compatibility_Layer.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt

## Phase 1 - The Great Purge
- [x] 1. PURGE_SINGLETONS | Justification: removed `HardwareThermalService` static runtime instance ownership; service identity is now registry-gated through `GlobalRegistry.HardwareThermal`. Rejected: `ThermalMonitor.Instance`/self-owned static service references. Estimate: 4 us cold path, 0 us hot path.
- [x] 2. DEBT_CLEANUP | Justification: `rg Application.targetFrameRate` found no UI writers; production writer remains bootstrap matrix and QA writers remain headless harness scoped. Rejected: moving QA test overrides into runtime hardware policy. Estimate: 0 us runtime.
- [x] 3. DATA_EVICTION | Justification: added DataVault `BufferID.HardwareMetrics` and `SystemID.HardwareHomeostasis`; `HomeostasisBrain` resolves metrics from `GlobalDataVault` before falling back to H8Memory. Rejected: independent persistent NativeArray as first choice. Estimate: 3 us cold init, 0 us per frame after alias.

## Phase 2 - Cross-Platform Sensory Kernel
- [ ] 4. ANDROID_JNI_BRIDGE | Pending Phase 2.
- [ ] 5. STEAM_DECK_SENSORS | Pending Phase 2.
- [ ] 6. SHI_CALCULATION | Pending Phase 2.
- [ ] 7. EWMA_SMOOTHING | Pending Phase 2.

## Phase 3 - Hierarchy of Sacrifice
- [ ] 8. LEVEL_1_WARNING | Pending Phase 3.
- [ ] 9. LEVEL_2_THROTTLING | Pending Phase 3.
- [ ] 10. LEVEL_3_CRITICAL | Pending Phase 3.
- [ ] 11. POWER_RECOVERY | Pending Phase 3.

## Phase 4 - Stability, Telemetry, Blackbox
- [ ] 12. NAN_VACCINATION | Pending Phase 4.
- [ ] 13. BLACKBOX_LOGGING | Pending Phase 4.
- [ ] 14. TRIPLE_STRIKE_REPAIR | Pending compile.
- [ ] 15. HOMEOSTASIS_SIGNAL | Pending Phase 4.
- [ ] 16. MAC_METAL_THERMALS | Pending Phase 4.
- [ ] 17. VR_REFRESH_SYNC | Pending Phase 4.
- [ ] 18. FINAL_VALIDATION | Pending final build.

## Iterative Loops
- [x] Loop 1: prompt extracted, mandates read, Phase 1 implemented.
- [ ] Loop 2: compile and Phase 2.
- [ ] Loop 3: compile and Phase 3.
- [ ] Loop 4: compile and Phase 4.
- [ ] Loop 5: self-inquisition / omega polish after all tasks done or blocked.

## Compile State
- [BLOCKED BY DEPENDENCY] `dotnet build Hecton8.Core.csproj --no-restore` failed after three attempts.
- Strike 1: empty MSBuild log, exit -1.
- Strike 2: temporary generated-csproj ladder include exposed 179 unrelated errors; generated-csproj edit reverted.
- Strike 3: current blocker is external `FaunaKinematicsRuntime.cs` missing `Hecton8.Animation.Fauna` types: `JawIkTarget`, `CurrentJawPos`, `BiteIkSolveEvent`.
- Build logs: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase1_Strike1.txt`, `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase1_Strike2.txt`, `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase1_Strike3.txt`.

## Phase Ownership Record
- Phase: PRE_SIMULATION for SHI policy, FrostTick for hardware API sampling, POST_SIMULATION for blackbox/signal writes.
- Owner Assembly: Hecton8.Core plus Core/Hardware service boundary due existing assembly dependency graph.
- DataVault Buffers Written: BufferID.HardwareMetrics.
- Signal Lanes Published: SystemHealthSignal, FrameTimeSignal, KillSwitchSignal, BatteryLevelSignal, ThermalStateChangedSignal.
- Budget: Phase 1 adds 0 us hot path; DataVault resolve is cold init only.
- Load-Shed Fallback: H8Memory fallback if GlobalDataVault is unavailable.

## Integrator Note
Phase 1 hardware edits are not represented in current compiler errors. The active build wall is external animation/fauna assembly drift plus broader dirty-batch errors observed in Strike 2. Hardware work is PENDING VERIFICATION until Integrator restores the core build graph.
