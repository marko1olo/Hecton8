# LOCKSTEP_STATE_VALIDATOR Status

Prompt: `LOCKSTEP_STATE_VALIDATOR`
Role: `CORE_ENGINEER`
Domain: `ECHELON 1: CORE & MEMORY INFRASTRUCTURE`
Task Count: 19
Status Contract: PENDING VERIFICATION

## Mandates Read Before Coding

- `PHYS_Determinism_Multithreaded_Body_Solving.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`

## State Machine

- [x] Prompt extracted by CLI from `Docs/Tasks/CURRENT_BATCH.md` | DOD: exact `<AGENT_PROMPT id="LOCKSTEP_STATE_VALIDATOR">` block isolated, neighboring prompts ignored | Alternative rejected: relying on IDE tabs or partial MCP reads | Estimate: 250000us wall time, 0us runtime.
- [x] Status/rationale hygiene checked | DOD: `Status_LOCKSTEP_STATE_VALIDATOR.md`, `Rationale_LOCKSTEP_STATE_VALIDATOR.md`, and `LOG_LOCKSTEP_STATE_VALIDATOR.md` were absent before creation | Alternative rejected: appending to stale batch status | Estimate: 15000us wall time, 0us runtime.
- [x] Domain boundary read | DOD: `Docs/Actual Domains of Project.txt` maps this work to Echelon 1 core/memory/replay infrastructure | Alternative rejected: editing presentation/VFX/audio truth state | Estimate: 18000us wall time, 0us runtime.

## Core Tasks

1. [ ] SINGLETON ERADICATION: N/A.
2. [ ] SIGNAL MIGRATION: Emit `DesyncDetectedSignal` on hash mismatch during replay.
3. [ ] ASMDEF ISOLATION: `Hecton8.Core.Determinism` -> Contracts.
4. [ ] DEAD CODE HUNT: N/A.
5. [ ] THE TARGET ARRAYS: Request `RigidbodyAUPs`, `PlayerKinematicState`, `RoomWaterLevels`, and `EntityAUPs` from `GlobalDataVault`.
6. [ ] PARALLEL HASHING: Write an `IJobParallelFor` that computes FNV-1a hash of each array independently.
7. [ ] MERKLE COMBINATION: Combine individual array hashes into `MasterStateHash`.
8. [ ] BIT-PERFECT SNAPSHOT: Execute at end of `POST_SIMULATION` on frame `N % 300 == 0`.
9. [ ] I/O RECORDING: Write `MasterStateHash` and 300 frames of `InputState` to `.h8replay`.
10. [ ] GHOST REPLAY: Load `.h8replay`, override player input, run simulation at 10x speed.
11. [ ] DESYNC TRIGGER: On mismatch, pause simulation and dump individual array hashes.
12. [ ] AUP SHIFT SAFETY: Hash relative sector-local positions, not absolute floating `float3`.
13. [ ] MATH LOD: Low tier disables normal gameplay hashing; replay mode enables it.
14. [ ] ZERO-GC: Hashes are uints; no hot-path managed allocation.
15. [ ] VRAM BUDGET: N/A.
16. [ ] BLACKBOX DUMP: Push `LastMasterHash` to telemetry.
17. [ ] EXECUTION PHASE: Runs in `POST_SIMULATION`.
18. [ ] CROSS-DOMAIN AUDIT: VFX particles and Audio are not hashed.
19. [ ] OMEGA COMPILE CHECK: Verify Burst compiles FNV-1a array loops with SIMD enabled.

## Iteration Log

- Loop 0: Mandates and prompt extracted. Codebase mapping pending.
