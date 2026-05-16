# Status_AMBIENT_BIOTA_DIRECTOR

Agent ID: AMBIENT_BIOTA_DIRECTOR
Domain: AI/ENVIRONMENT
Task Count: 18
Status: IN_PROGRESS

## Prompt Extraction Evidence

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extraction command: PowerShell `Get-Content -Raw` with regex for `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">...`
- Result: prompt recovered after injection.
- Required first line: `PROMPT IDENTIFIED: AMBIENT_BIOTA_DIRECTOR | DOMAIN: AI/ENVIRONMENT | TASK COUNT: 18`

## Relevant Mandates Read

- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `AI_Director_Encounter_Manager.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Loop 1: Tasks 1-5

- [x] 1. PURGE_INSTANTIATE
  - DOD practice: runtime ambient/fish scan plus new SOA director path; no `Object.Instantiate`, no `Instantiate`, no `Update`, no `Random.Range`.
  - Rejected alternative: pooled GameObject fish spawn in a loop; still pays transform/component activation churn and violates GPU stream target.
  - Microsecond estimate: avoids 2000-8000 us spawn spikes per 64-object burst versus Unity object instantiation; steady-state scan cost 0 us/frame.
- [x] 2. SINGLETON_KILL
  - DOD practice: added `IAmbientBiotaService` and `GlobalRegistry.AmbientBiota` slot; no `AmbientLifeManager.Instance` dependency found or introduced.
  - Rejected alternative: static singleton owner; couples ambient life to scene load order and blocks hot-swap/testing.
  - Microsecond estimate: removes 1-3 us/frame of singleton/null scene-path drift once consumers bind the service directly.
- [x] 3. DATA_EVICTION
  - DOD practice: reserved `BufferID.BiotaAUPs`, `BufferID.BiotaVelocities`, `BufferID.BiotaStates`; director requests fixed buffers from `GlobalDataVault`.
  - Rejected alternative: local persistent arrays hidden inside a MonoBehaviour; not visible to GPU/VFX consumers and harder to police for leaks.
  - Microsecond estimate: avoids 50-150 us cold-path realloc/resize bursts and 0 B/frame runtime GC.
- [x] 4. BIOTA_SPAWN_JOB
  - DOD practice: Burst `IJob` activates dead SOA slots by deterministic hash, biomass, carrying capacity, and bounded slow-tick spawn budget.
  - Rejected alternative: `Random.Range` plus per-fish components; nondeterministic and main-thread heavy.
  - Microsecond estimate: 15-60 us per slow tick on low tier for 2048 slots, replacing millisecond-scale object creation bursts.
- [x] 5. DETERMINISTIC_DRIFT
  - DOD practice: Burst `IJobParallelFor` drifts one modulo bucket per frame using deterministic Brownian noise plus abyssal flow input.
  - Rejected alternative: per-fish MonoBehaviour movement; scales with active objects and burns transform writes.
  - Microsecond estimate: 8-30 us/frame low tier, 40-90 us/frame high tier before GPU draw integration; visual density scales with capacity.
- [x] Compile verification after Tasks 1-5: [BLOCKED BY DEPENDENCY]
  - Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`
  - Result: failed in unrelated project-wide errors outside `AI/Ambient`: missing `JobAdmissionLane` references, missing `HectonShaderGlobalDataVaultBridge`, missing signal types in `GlobalSignals`, missing voxel-debris constants, and stale generated project references.
  - Local note: current generated `.csproj` files do not include the new `Hecton8.AI.Ambient` assembly until Unity regenerates project files. The asmdef directly references `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`.

## Pending Tasks

- [ ] 6. AUP_INTEGRITY
- [ ] 7. MODULO_BUCKETING
- [ ] 8. LOW_TIER_FAKE
- [ ] 9. HIGH_END_OVERKILL
- [ ] 10. REACTIVE_VFX
- [ ] 11. STP_STABILIZATION
- [ ] 12. NAN_VACCINATION
- [ ] 13. BLACKBOX_LOGGING
- [ ] 14. TRIPLE_STRIKE_REPAIR
- [ ] 15. HOMEOSTASIS_ADAPTATION
- [ ] 16. INDIRECT_DRAW_CALL
- [ ] 17. BIOME_SYNC
- [ ] 18. FINAL_VALIDATION

## Phase 1 Audit Notes

- Runtime ambient/fish scan found no direct `AmbientLifeManager.Instance`.
- Runtime ambient/fish scan found no direct `Object.Instantiate` in an ambient-fish owner. Existing `ObjectPoolManager` and editor instantiation paths are not ambient fish scripts.
- `SargassumMicroFaunaBoids` exists under `World`; it is not edited in this phase because the prompt's authoritative write domain is `Assets/_Project/Scripts/AI/Ambient/`.
