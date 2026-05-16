# Status_ECOSYSTEM_MIGRATION_LINK

Prompt: ECOSYSTEM_MIGRATION_LINK
Role: APEX_DIRECTOR / The Macro Spawner
Domain: AI/ECOLOGY
Task count: 18
State: PENDING VERIFICATION

## Mandates Read Before Coding

- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- MATH_AUP_Determinism_Sync.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt

## Checklist

- [ ] 1. PURGE_SINGLETONS - Extend IEcosystemDirectorService. | Pending code inspection. | Alternatives rejected: direct singleton access. | Estimate: TBD us.
- [ ] 2. DEBT_CLEANUP - Delete legacy SpawnPoint GameObjects. | Pending source/asset scan. | Alternatives rejected: leave scene-only spawn points active. | Estimate: TBD us.
- [ ] 3. DATA_EVICTION - Read NativeArray<MacroSwarm> from Vault. | Pending contract discovery. | Alternatives rejected: ScriptableObject/managed DB in hot path. | Estimate: TBD us.
- [ ] 4. BURST_ALGORITHM - SwarmHydrationJob on SectorHydratedSignal. | Pending signal and chunk contract discovery. | Alternatives rejected: MonoBehaviour loop with managed collections. | Estimate: TBD us.
- [ ] 5. AUP_INTEGRITY - Convert SectorAUP to runtime float3 boid positions. | Pending AUP struct discovery. | Alternatives rejected: Transform.position authority. | Estimate: TBD us.
- [ ] 6. DOD_SOA_LAYOUT - Claim empty boid slots where Flag_IsActive = 0. | Pending boid SOA layout discovery. | Alternatives rejected: Instantiate prefabs. | Estimate: TBD us.
- [ ] 7. SIGNAL_FLOW - Emit EntitySpawnSignal(Ecology). | Pending signal lane discovery. | Alternatives rejected: string event RPC. | Estimate: TBD us.
- [ ] 8. LOW_TIER_FAKE - Border hydration mode. | Pending quality/stress contract discovery. | Alternatives rejected: cave/SDF spawn on low tier. | Estimate: TBD us.
- [ ] 9. HIGH_END_OVERKILL - SDF cave emergence mode. | Pending SDF sampling contract discovery. | Alternatives rejected: full physics simulation. | Estimate: TBD us.
- [ ] 10. REACTIVE_VFX - N/A. | Not started. | Alternatives rejected: new VFX dependency. | Estimate: 0 us.
- [ ] 11. STP_STABILIZATION - N/A. | Not started. | Alternatives rejected: unrelated STP surface edits. | Estimate: 0 us.
- [ ] 12. NAN_VACCINATION - Guard SDF/position writes. | Pending implementation. | Alternatives rejected: blind position writes. | Estimate: TBD us.
- [ ] 13. BLACKBOX_LOGGING - Log MacroSwarmsHydrated. | Pending telemetry contract discovery. | Alternatives rejected: Debug.Log hot-path reporting. | Estimate: TBD us.
- [ ] 14. TRIPLE_STRIKE_REPAIR - Fix capacity overflow attempts. | Pending build loop. | Alternatives rejected: stop after first compile failure. | Estimate: TBD us.
- [ ] 15. HOMEOSTASIS_ADAPTATION - Stress > 0.7 hydrates 50 percent visually. | Pending stress source discovery. | Alternatives rejected: all-or-nothing swarm activation. | Estimate: TBD us.
- [ ] 16. DEHYDRATION_SEAM - Chunk unload packs boids to MacroSwarm. | Pending unload signal discovery. | Alternatives rejected: dropping active biomass. | Estimate: TBD us.
- [ ] 17. CAPACITY_CLAMP - Never exceed MaxBoidCapacity. | Pending implementation. | Alternatives rejected: overflow or resize. | Estimate: TBD us.
- [ ] 18. FINAL_VALIDATION - dotnet build. | Pending. | Alternatives rejected: source-only claim. | Estimate: TBD us.

## Loop Log

- Loop 0: Prompt extracted, domain checked, mandates listed. No code changed yet.
