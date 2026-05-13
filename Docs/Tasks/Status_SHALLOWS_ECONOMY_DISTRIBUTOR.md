# SHALLOWS_ECONOMY_DISTRIBUTOR Status

Agent: SHALLOWS_ECONOMY_DISTRIBUTOR
Role: GAMEPLAY_PROGRAMMER
Domain: ECHELON 2 WORLD GENERATION / ORE ECONOMY DISTRIBUTION
Prompt source: Docs/Tasks/CURRENT_BATCH.md
Task count: 15
Status: PENDING VERIFICATION

## Mandates Loaded Before Coding

- MATH_Deterministic_RNG_SlotMachine.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Checklist

- [x] Prompt extraction | Justification: exact SHALLOWS_ECONOMY_DISTRIBUTOR XML block extracted from Docs/Tasks/CURRENT_BATCH.md by PowerShell CLI regex, neighboring prompts ignored | Alternative rejected: MCP/basic file read because batch files can truncate | Estimate: 1200 us
- [x] Mandate selection | Justification: loaded deterministic RNG, zero-GC, AUP, telemetry, registry, perf budget, native job, and visual-fake mandates before code | Alternative rejected: coding from prompt alone | Estimate: 3800 us
- [ ] 1. SINGLETON ERADICATION: Extend IWorldResourceSpawnerReadModel | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 2. SIGNAL MIGRATION: Consume DropPodLandedSignal(AUP) | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 3. ASMDEF ISOLATION: Hecton8.World.Economy -> Contracts | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after tasks 1-5 | Result: PENDING | Estimate: PENDING
- [ ] 4. DISTANCE GRADIENT: distSq from OreAUP to DropPodAUP | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 5. QUOTA MATH: <50m 70% Titanium / 30% Copper / 0% Silver | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 6. PROGRESSION PUSH: >100m 40% Titanium / 40% Copper / 20% Silver | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 7. SPAWN CLUMPING: Copper vein bias within 2m | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after tasks 6-10 | Result: PENDING | Estimate: PENDING
- [ ] 8. RADAR SIGNATURES: expose OreTypes to TERRAIN_GPR_SYSTEM | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 9. FILTERING: HUD tuned radar alpha for non-matching ore | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 10. AUP SHIFT SAFETY: rebase DropPodAUP natively | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Compile verification after tasks 11-15 | Result: PENDING | Estimate: PENDING
- [ ] 11. MATH LOD: low tier cheap clump check | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 12. EXECUTION PHASE: Generation runs cold | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 13. ZERO-GC: probability math allocates 0 bytes | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 14. BLACKBOX DUMP: LocalTitaniumCount telemetry | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] 15. OMEGA COMPILE CHECK: probabilities sum to 1.0 safely | Justification: PENDING | Alternative rejected: PENDING | Estimate: PENDING
- [ ] Loop 1 strict self-review | Result: PENDING | Estimate: PENDING
- [ ] Loop 2 strict self-review | Result: PENDING | Estimate: PENDING
- [ ] Loop 3 strict self-review and prompt re-extraction | Result: PENDING | Estimate: PENDING
- [ ] Loop 4 strict self-review | Result: PENDING | Estimate: PENDING
- [ ] Loop 5 strict self-review and polish gate | Result: PENDING | Estimate: PENDING

