# Rationale - SHINOBU_41_EXPLORE_CODE

Status: PENDING VERIFICATION

Problem: The assignment is archaeology-only but HECTON-8 law requires persistent status and rationale files.
Solution: Create minimal tracking files under Docs only; do not mutate source, assets, prefabs, scenes, or public APIs.
Rejected Alternatives: Chat-only reporting violates reporting protocol; source edits violate the sub-agent mission.
Scalability potential: Not runtime code. Evidence gathered will prevent low-tier regressions and preserve high-tier visual budget.
Hardware Impact: No runtime impact. Source untouched; no i3/MX350 frame cost.

Problem: The live batch prompt for SHINOBU_41 has 20 implementation tasks, while this invocation is an inline sub-agent archaeology prompt with 8 scoped evidence tasks.
Solution: Extracted the full live `Docs/Tasks/CURRENT_BATCH.md` SHINOBU_41 XML using CLI regex, but limited execution to the inline Codebase Archaeologist mission: read-only code/contracts, compile risks, and terrain-query violations.
Rejected Alternatives: Executing the full 20-task implementation would violate the sub-agent "Do not edit files" directive and contaminate primary-agent ownership.
Scalability potential: Evidence pass identifies low/middle/high/ultra integration surfaces without adding runtime work.
Hardware Impact: No runtime impact. Prevents future i3/MX350 regressions by identifying existing terrain sampling contracts before implementation.

Problem: SHINOBU_41 touches MapMagic, voxel SDF, DataVault, tick phases, telemetry, and ARM64 DTO layout; using only generic repo rules would miss domain-specific traps.
Solution: Selected 8 registry mandates: VOX_MapMagic_Voxel_Seam_Alignment_Integration, VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Execution_Phases, OPT_Native_Memory_Collections_JobSystem_Protocol, DBG_Telemetry_Crash_Reporting_PostMortem, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate.
Rejected Alternatives: Reading the whole registry wastes time and increases cross-agent contamination; reading fewer mandates misses native ownership/telemetry/ARM64 constraints.
Scalability potential: Low tier demands O(1) height/SDF lookups and vault ownership; high/ultra can spend saved cycles in visual sync, not unbounded terrain truth.
Hardware Impact: No runtime impact in this pass. Expected future savings are from avoiding Physics BVH terrain queries and avoiding local NativeArray leaks.
