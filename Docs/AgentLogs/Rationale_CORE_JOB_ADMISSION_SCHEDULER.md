# CORE_JOB_ADMISSION_SCHEDULER Rationale

Status: PENDING VERIFICATION

## Mandates Selected

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Decision 000: Scope Discipline

Problem: Job admission touches every Burst scheduling caller, but 20+ agents can be editing adjacent systems. A concrete mass rewrite of all `.Schedule()` sites risks compile churn and cross-domain coupling.
Solution: Build the core admission interface, service, wrappers, signals, bootstrap registration, and surgical integration hooks. Generate recon for remaining callers rather than blindly editing unrelated domains.
Rejected Alternatives: Rewriting every job call immediately was rejected because it would mutate voxel, world, AI, physics, logistics, and tests without domain ownership proof. Adding a `JobManager.Instance` was rejected by prompt and registry mandate.
Scalability potential: Low uses aggressive background shedding; Middle allows stable world jobs; High/Ultra spend saved CPU on richer AI/VFX admission instead of uncontrolled worker saturation.
Hardware Impact: On i3/MX350, expected gain is stall avoidance, not raw throughput. Microsecond estimate pending measurement; static target is to prevent multi-ms worker pileups by rejecting low-priority jobs before schedule.
