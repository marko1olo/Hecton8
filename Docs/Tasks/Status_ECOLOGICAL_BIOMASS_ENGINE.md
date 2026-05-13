# ECOLOGICAL_BIOMASS_ENGINE Status

Agent: APEX_DIRECTOR
Domain: ECHELON 3 FLORA, FAUNA & BIOTA
Prompt: Lotka-Volterra Predator/Prey Pacing
Status: PENDING VERIFICATION

## Mandates Read Before Coding

- AI_Director_Encounter_Manager.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- MATH_Deterministic_RNG_SlotMachine.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

## State Machine

- [ ] Task 1 - SINGLETON ERADICATION: N/A extends existing director | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 2 - SIGNAL MIGRATION: consume EntityDeathSignal to reduce local biomass | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 3 - ASMDEF ISOLATION: Hecton8.AI.Ecology -> Contracts | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 4 - DEAD CODE HUNT: no spawn weights without Biomass modifier | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 5 - SECTOR BIOMASS GRID: NativeArray<float> PreyBiomass/PredatorBiomass on 50 m macro-grid | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 6 - FROST TICK MATH: Burst Lotka-Volterra every 5 s | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 7 - CAPACITY CLAMP: 0..MaxCarryingCapacity by biome | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 8 - SPAWN CREDIT MODIFIER: apex scarce cost x2, swarm overgrown cost x0.5 | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 9 - DEPLETION PERSISTENCE: sbyte 0-100 RLE save bridge | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 10 - MIGRATION DIFFUSION: slow adjacent-cell diffusion | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 11 - VISUAL FLORA COUPLING: low prey increases kelp overgrowth scalar | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 12 - PLAYER FISHING IMPACT: ItemAcquiredSignal(Fish) deducts prey biomass | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 13 - AUP SHIFT SAFETY: absolute grid, shifted read index only | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 14 - ZERO-GC: math job allocates 0 bytes | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 15 - MATH LOD: Low tier disables diffusion | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 16 - OVERHUNTING HUD: scanner depletion warning | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 17 - BLACKBOX DUMP: GlobalBiomassSum telemetry | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 18 - EVENT BUS ALARM: predator biomass zero emits SectorCleared | Justification: pending | Rejected: pending | Estimate: pending
- [ ] Task 19 - OMEGA COMPILE CHECK: verify Burst equations compile | Justification: pending | Rejected: pending | Estimate: pending

## Compile Attempts

- Pending.

## Iteration Notes

- Loop 0: prompt extracted from Docs/Tasks/CURRENT_BATCH.md. Source scan found existing `World/EcosystemDirector`, `EncounterDirector`, `HectonDirectorAI`, `GlobalSignals`, and `IEcosystemDirectorService`.
