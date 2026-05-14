# HABITAT_O2_SCRUBBER_LOD Status

STATUS: PENDING VERIFICATION
Domain: HABITAT/ATMOSPHERE GAS DYNAMICS
Prompt task count: 15
Batch hygiene: Fresh status file created; no prior Status_HABITAT_O2_SCRUBBER_LOD.md existed.

## Relevant Mandates
- [x] CORE_Abyss_Survival_Systems_O2_Pressure_Logic
- [x] LOGI_Energy_Networks_Power_Grid_Graph_Flow
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate
- [x] DBG_Telemetry_Crash_Reporting_PostMortem
- [x] OPT_Cinematic_Cheat_Protocol_Visual_Fake_First

## Task Checklist
- [x] 1. SINGLETON ERADICATION: N/A | Justification: no singleton added; solver remains GlobalRegistry-registered service. | Alternatives Rejected: static singleton/Instance access rejected by ARCH mandate. | Estimate: avoids 0 runtime lookup churn; no measurable microsecond claim.
- [x] 2. SIGNAL MIGRATION: Consume PlayerBaseEnterSignal and PlayerBaseExitSignal | Justification: typed SignalBus lanes added and drained in FrostTick, keeping base transitions decoupled. | Alternatives Rejected: direct BaseAirlock/BaseModule concrete references rejected because 20+ agents may rewrite those systems. | Estimate: 1-3us per FrostTick signal batch vs managed event/listener traversal.
- [BLOCKED BY EXISTING CORE CYCLE] 3. ASMDEF ISOLATION: Hecton8.Habitat.Atmosphere -> Contracts | Justification: GlobalRegistry currently holds concrete Hecton8.Atmosphere service slots (`HectonSurfaceWeatherDirector`, `HectonAtmosphereManager`), so moving Atmosphere into a new asmdef would require Core -> Atmosphere and Atmosphere -> Core circular references. Interface expansion was placed in existing Core.Contracts instead. | Alternatives Rejected: adding a cosmetic asmdef marker rejected as fake isolation; forcing a runtime asmdef without compile permission rejected as build sabotage. | Estimate: blocked; integrator must split concrete registry slots to interfaces first.
- [x] 4. S.O.A. AWAKE MASK: Define NativeArray<byte> BaseAwakeState | Justification: `BaseAwakeState` native byte mask added with room-to-base SOA mapping. | Alternatives Rejected: managed dictionary/base objects rejected for GC and cache locality. | Estimate: 20-80us saved per sleeping 64-room solve by skipping room metabolism/diffusion work.
- [x] 5. DISTANCE CULLING: FrostTick distance gate with inside override | Justification: IFrostTickable gate uses cached player movement contract, AUP distance, and 25m hysteresis; inside signal forces wake. | Alternatives Rejected: per-frame distance polling rejected; transform-only distance rejected for floating-origin/AUP safety. | Estimate: 5s cadence keeps culling cost below measurable frame impact; expected <5us per 32 bases per FrostTick.
- [ ] 6. SUSPEND DIFFUSION: Burst gas and power jobs skip hibernating bases | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 7. TIMESTAMPING: Record H8Time.UnscaledTime on hibernation | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 8. WAKE-UP MATH: Calculate elapsed seconds on approach | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 9. ANALYTICAL RESOLVE: Decay O2 toward ambient with exp formula | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 10. POWER DRAIN: Deduct idle draw over elapsed time and clamp battery | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 11. H-PHI SOVEREIGNTY: Store BaseAwakeState in GlobalDataVault | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 12. AUP SHIFT SAFETY: BaseCenterAUP shifted natively | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 13. MATH LOD: Low tier aggressive hibernation at 150m | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 14. ZERO-GC: Catch-up allocates 0 bytes | Justification: pending | Alternatives Rejected: pending | Estimate: pending
- [ ] 15. OMEGA COMPILE CHECK: Verify Burst compiles math.exp | Justification: pending | Alternatives Rejected: pending | Estimate: pending

## Iteration Log
- Iteration 0: Prompt extracted from Docs/Tasks/CURRENT_BATCH.md with regex CLI. Implementation not started.
- Iteration 1: Tasks 1-5 implemented or honestly blocked. Static touched-file `git diff --check` passed; no dotnet rebuild run per user instruction.
- Iteration 1 Prompt Re-Extract: CLI regex against current `Docs/Tasks/CURRENT_BATCH.md` returned `PROMPT_NOT_FOUND`; current batch file appears to have been rotated and no longer contains `HABITAT_O2_SCRUBBER_LOD`.
