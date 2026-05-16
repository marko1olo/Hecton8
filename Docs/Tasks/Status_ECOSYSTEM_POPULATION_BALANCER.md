# ECOSYSTEM_POPULATION_BALANCER Status

Prompt: `ECOSYSTEM_POPULATION_BALANCER`
Role: `AI_PROGRAMMER`
Domain: `AI/ECOLOGY`
Task Count: 18
Authority: `Docs/Tasks/CURRENT_BATCH.md` extracted by XML tag.

## Hygiene

- [x] Batch prompt extracted by ID | DOD: CLI regex over full `CURRENT_BATCH.md`; rejected MCP/basic partial read; estimate 250 us.
- [x] Status file created | DOD: disk-backed state for anti-amnesia; rejected chat-only tracking; estimate 120 us.
- [x] Rationale file created | DOD: decision log before non-trivial work; rejected final-only report; estimate 120 us.
- [x] Mandates read | DOD: 8 task-relevant registry files loaded before coding; rejected unbounded registry sweep; estimate 900 us.
- [x] Compile verification | DOD: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; rejected static-only claim; final build succeeded with 0 warnings/0 errors in 2950000 us wall-clock.

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

