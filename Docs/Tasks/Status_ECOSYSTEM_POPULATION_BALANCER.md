# ECOSYSTEM_POPULATION_BALANCER Status

Prompt: `ECOSYSTEM_POPULATION_BALANCER`
Role: `AI_PROGRAMMER`
Domain: `AI/ECOLOGY`
Task Count: 18
Authority: `Docs/Tasks/CURRENT_BATCH.md` extracted by XML tag.
Omega Status: `VERIFIED MASTER GRADE`

## Hygiene

- [x] Batch prompt extracted by ID | DOD: CLI regex over full `CURRENT_BATCH.md`; rejected MCP/basic partial read; estimate 250 us.
- [x] Status file created | DOD: disk-backed state for anti-amnesia; rejected chat-only tracking; estimate 120 us.
- [x] Rationale file created | DOD: decision log before non-trivial work; rejected final-only report; estimate 120 us.
- [x] Mandates read | DOD: 8 task-relevant registry files loaded before coding; rejected unbounded registry sweep; estimate 900 us.
- [x] Compile verification | DOD: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; rejected static-only claim; latest successful Polish21 retry build succeeded with 0 warnings, 0 errors, wrapper wall-clock 37701855 us, `dotnet` elapsed 18630000 us.

## Phase 1: Purge

- [x] 1. PURGE_SINGLETONS | DOD: `rg` found no `SpawnManager.Instance` in AI/Ecosystem, Ecosystem installer, or ecosystem director surfaces; rejected deleting unrelated singleton duplicate guards; estimate 320 us.
- [x] 2. DEBT_CLEANUP | DOD: new domain has zero `Instantiate`, `Destroy`, `Update`, or `string.Format`; rejected GameObject spawn/despawn path; estimate 410 us.
- [x] 3. DATA_EVICTION | DOD: `Data/Precomputed/ecosystem_coefficients.json` cold-loads into `BufferID.EcosystemPopulationCoefficients`; rejected inspector hardcoding/private NativeArray ownership; estimate UNMEASURED until profiler/IO trace.

## Phase 2: Kernel

- [x] 4. BURST_ALGORITHM | DOD: `EcosystemBalancerJob : IJob` Burst scheduled from `ColdTick` and completed in `LateFrameTick` swap window; rejected frame `Update`; estimate UNMEASURED until Burst profiler capture.
- [x] 5. AUP_INTEGRITY | DOD: sector hash resolved from AUP grid/local coordinates, not runtime float position; rejected origin-shift-sensitive `Vector3`; estimate UNMEASURED until Burst profiler capture.
- [x] 6. DOD_SOA_LAYOUT | DOD: job scans `EntityAUPs`/`EntityFlags`, culls only `Flag_IsPrey | Flag_Tier2Frozen`, clears `Flag_IsActive`; rejected GameObject lifetime calls; estimate UNMEASURED until Burst profiler capture.
- [x] 7. SIGNAL_FLOW | DOD: cull events publish existing `EntityDeathSignal` through `SignalBus<EntityDeathSignal>.Push`; rejected duplicate/managed signal lanes; estimate UNMEASURED until lane profiler capture.

## Phase 3: Visual

- [x] 8. LOW_TIER_FAKE | DOD: actual culls/spawns only target Tier 2 frozen/unloaded entries; rejected visible vanish; estimate UNMEASURED until Burst profiler capture.
- [x] 9. HIGH_END_OVERKILL | DOD: loaded Tier 1 entities receive `Flag_EcologyFleeDown` and stay active for SDF dive consumers; rejected instant active-flag clear; estimate UNMEASURED until presentation consumer exists.
- [x] 10. REACTIVE_VFX | N/A per prompt; rejected adding unauthorized VFX owner; estimate 0 us runtime.
- [x] 11. STP_STABILIZATION | N/A per prompt; rejected unauthorized stabilizer ownership in ecology balancer; estimate 0 us runtime.

## Phase 4: Stability

- [x] 12. NAN_VACCINATION | DOD: coefficients/biomass clamped, reciprocal denominators guarded by `math.max(1f, value)`, non-finite next biomass recovered to zero; rejected trusting baked JSON alone; estimate UNMEASURED until profiler capture.
- [x] 13. BLACKBOX_LOGGING | DOD: 300-entry `EcosystemPopulationTelemetryEntry` ring in DataVault records `TotalActiveEntities`, `CulledByEcology`, spawns, flee-down, sectors, free-ring count, stress, flags; rejected managed log spam; estimate UNMEASURED until profiler capture.
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: all entity/sector/event/free-ring loops clamp against actual array lengths; rejected unchecked prompt-sized loops; owned code compiled cleanly in Loop 2, later full builds blocked externally; estimate UNMEASURED runtime.
- [x] 15. HOMEOSTASIS_ADAPTATION | DOD: when `SystemStress01 > 0.8`, job performs emergency Tier 2 ecology cull beyond LV prey target; rejected clearing non-ecology shared flags; estimate UNMEASURED until profiler capture.
- [x] 16. PLAYER_IMPACT | DOD: verified `World/EcosystemDirector` drains `ReadOnlySpan<EntityDeathSignal>` and `ReadOnlySpan<ItemAcquiredSignal>` for biomass grid impact; balancer emits existing ecology death signal; rejected duplicate biomass writer; estimate 0 us added beyond cull event emission.
- [x] 17. MEMORY_REUSE | DOD: culled prey indices enter DataVault free ring and prey respawns reactivate dead indices in-place; rejected `Instantiate`; estimate UNMEASURED until profiler capture.
- [x] 18. FINAL_VALIDATION | DOD: final `dotnet build` succeeded; 0 warnings, 0 errors, 2950000 us wall-clock; rejected claiming runtime microseconds without profiler capture.

## Iteration Loops

- [x] Loop 1: Tasks 1-5, compile, checklist readback. Build log: `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Loop1_PostFreeListFix_NoRestore.txt`.
- [x] Loop 2: Tasks 6-10, compile, checklist readback. Build log: `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Loop2.txt`.
- [x] Loop 3: Tasks 11-14, compile, checklist readback. Full compile blocked by unrelated `LaserCutter.cs` after owned Loop 2 clean build.
- [x] Loop 4: Tasks 15-18, compile, checklist readback. Build log: `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Loop4.txt`; blocked by unrelated `LockstepStateValidator.cs`.
- [x] Loop 5: strict self-inquisition, compile, final log. Final build log: `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Final.txt`; report: `Docs/AgentLogs/LOG_ECOSYSTEM_POPULATION_BALANCER.md`.
- [x] Loop 6: multiplatform polish after user inquisition. DOD: AUP sector hash no longer truncates to `int`; ARM64 struct layout scan still shows explicit Pack=1; final build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish7_Aup64.txt`.
- [x] Loop 7: H-Phi/static-dispatch polish. DOD: removed `System.Reflection`, `AppDomain.GetAssemblies`, and type-name lookup from the ecology bootstrap/layout integration path; build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish8_StaticDispatch_Retry.txt` is `[BLOCKED BY DEPENDENCY]` in unrelated `ArchitectEyeVisualizer`, `PlayerCriticalProceduralAudioRenderer`, and `AbyssalThermalManager`.
- [x] Loop 8: Data-sovereignty/I/O polish. DOD: cached DataVault/EcosystemDirector dependencies, removed repeated registry reads from ColdTick/LateFrame paths, replaced whole-file coefficient read with bounded sequential cold I/O, and reduced local `NativeArray<T>` declarations to DataVault view/job boundaries; build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish8_OwnedHardening.txt` is `[BLOCKED BY DEPENDENCY]` in unrelated `HectonMarineSnowRenderer`.
- [x] Loop 9: Hot-swap/registry polish. DOD: verified `IGlobalRegistryHotSwapListener` invalidates cached DataVault/director handles, completes the scheduled job before handle reset, unregisters ticks on failed DataVault replacement, and preserves stateless DataVault-owned buffers; forbidden-pattern scan remains clean. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish9_HotSwap_Retry.txt` is `[BLOCKED BY DEPENDENCY]` in unrelated `PhysicsApplySystem`.
- [x] Loop 10: Atomic tick-lane polish. DOD: ColdTick/LateFrame registration is now all-or-none, DataVault swaps clear telemetry cursor/fault dump latch, and owned forbidden-pattern scan remains clean. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish10_AtomicTicks.txt` is `[BLOCKED BY DEPENDENCY]` with 194 errors in external `World/EcosystemDirector`, `SystemDispatcher`, and `TetherManager`; no owned AI/Ecosystem error was emitted.
- [x] Loop 11: ABI/signal-ring polish. DOD: filled explicit Pack=1 tail bytes with named reserved fields, extended binary layout sentinel offsets, bounded free-ring cursor, prevented unsignaled culls when cull-event capacity is exhausted, and hardened coefficient JSON fallback. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish11_ABI_SignalRing_Warnings.txt` succeeded with 4 external `ArchitectEyeVisualizer` warnings, 0 errors, and no owned AI/Ecosystem warnings/errors.
- [x] Loop 12: Blackbox fault-containment polish. DOD: invalid-math dump path now reports missing telemetry and dump I/O failure through hashed `GlobalTelemetryBus` markers, always emits the math-guard invalid-number marker, and leaves hot-path/Burst code untouched. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish12_BlackboxFaultContainment.txt` is `[BLOCKED BY DEPENDENCY]` in unrelated `World/SargassumMicroFaunaBoids.cs` missing `SaturateFinite01`; no owned AI/Ecosystem warning/error was emitted.
- [x] Loop 13: Free-ring rebuild polish. DOD: ColdTick now rebuilds `EcosystemPopulationFreeRing` from authoritative inactive prey flags, purges stale slots, writes bounded cursor/count counters, and reports ring overflow through telemetry. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish13_FreeRingRebuild.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 14: Death-signal lane-cap polish. DOD: ecology cull-event production is capped to the existing `EntityDeathSignal` lane prewarm budget of 64, so overflow trips before typed-lane queue growth; serialized `cullEventCapacity` is clamped to that budget. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish14_DeathLaneCap.txt` is `[BLOCKED BY DEPENDENCY]` in unrelated `ArchitectEyeVisualizer`; no owned AI/Ecosystem error was emitted.
- [x] Loop 15: Empty-heartbeat polish. DOD: no-sector telemetry now records free-ring count and system stress, keeping the 300-frame blackbox useful even when no ecology sector is active. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish15_EmptyHeartbeat.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 16: Chronological blackbox polish. DOD: invalid-math dump now writes format version, ring capacity, written count, cursor, oldest slot, and telemetry entries in chronological order; build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish16_ChronologicalBlackbox.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 17: Telemetry cursor rollover polish. DOD: telemetry writes now reserve ring slots through bounded positive modulo and recover from `int.MaxValue` without negative cursor state; initial build was blocked by transient missing Unity editor metadata, retry log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish17_TelemetryCursorRollover_Retry1.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 18: Stale free-slot guard polish. DOD: prey reactivation now validates free-slot index, active state, `Flag_IsPrey | Flag_FreeList`, finite AUP, and sector hash before reusing an entity index; stale slots are purged and flagged in telemetry. Build log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish18_StaleFreeSlotGuard.txt` is `[BLOCKED BY DEPENDENCY]` with 40 unrelated `SubmarineFluidDynamics.cs` missing-field errors; retries `Retry1` and `Retry2` exited `-1` before compiler diagnostics; no owned AI/Ecosystem error was emitted.
- [x] Loop 19: Prey-only free-ring polish. DOD: stress culls no longer insert non-prey/predator slots into `EcosystemPopulationFreeRing`, preserving prey reuse capacity; initial build was blocked externally in `SubmarineFluidDynamics.cs`, retry log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish19_FreeRingPreyOnly_Retry1.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 20: Player coefficient-read polish. DOD: coefficient JSON loading is no longer editor-only, so shipped player builds can read the baked LV coefficients when present and still fall back safely when absent; build logs `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish20_PlayerCoefficientRead*.txt` are `[BLOCKED BY DEPENDENCY]` in unrelated player/tether/acoustic files, with no owned AI/Ecosystem error emitted.
- [x] Loop 21: ABI/player/blackbox contract polish. DOD: `BinaryLayoutManifest` now matches the 64/96/32 byte Pack=1 ecology structs, `TryReadCoefficientJson` actually runs outside editor builds, and invalid-math dumps target `Dump_ECOSYSTEM_POPULATION_BALANCER.bin` with only written telemetry entries. First build exited `-1` after 191013723 us, retry 1 timed out under concurrent builds, retry 2 log `Docs/AgentLogs/Build_ECOSYSTEM_POPULATION_BALANCER_Polish21_ABI_PlayerBlackbox_Retry2.txt` succeeded with 0 warnings, 0 errors.
- [x] Loop 22: Shared-entity ownership polish. DOD: population balancer no longer creates `BufferID.EntityAUPs` or `BufferID.EntityFlags`; it only resolves existing vault handles, sets `TelemetryEntityBuffersMissingFlag` when the shared universe is absent, and still records empty blackbox telemetry through its own DataVault ring. Per user instruction, no dotnet rebuild was run; verification used `rg` ownership scans and `git diff --check`.
- [x] Loop 23: Missing-buffer heartbeat polish. DOD: `ColdTick` now records an empty telemetry heartbeat when `TryBuildSectorState` fails after DataVault setup, so `TelemetryEntityBuffersMissingFlag` and other setup faults reach the 300-frame blackbox instead of vanishing behind an early return. Per user instruction, no dotnet rebuild was run; verification used targeted source read, `rg`, and `git diff --check`.
- [x] Loop 24: DataVault job-lock and H8Memory fence polish. DOD: `EcosystemBalancerJob` now locks every DataVault buffer it reads/writes before resolving job views, registers the scheduled handle through `H8Memory.RegisterActiveJob(SystemID.AIEcology, ...)`, and unlocks on resolve failure, schedule rejection, late-frame completion, force completion, and disable cleanup. Fixed the draft false-positive `_jobLocksHeld` assignment that would have blocked scheduling before a lock existed. Per user instruction, no dotnet rebuild was run; verification used targeted source read, forbidden-pattern `rg`, lock/fence `rg`, and `git diff --check`.

