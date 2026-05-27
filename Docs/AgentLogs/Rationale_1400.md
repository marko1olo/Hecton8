# Rationale_1400

Status: PENDING VERIFICATION

## Session Initialization

Problem: Active batch assigned build graph surgery with potential MSB4006 circular dependency, Odin CLI reference loss, MapMagic assembly bleed, C# 14 `field` collisions, and domain reload reset risk.

Solution: Use disk evidence only. First loop is static graph discovery and AST-safe ledgers. Persistent build graph fixes must live in `Directory.Build.targets`, `.asmdef`, `.asmref`, or tooling under `Tools/`, not Unity-generated `.csproj` edits.

Rejected Alternatives: Editing generated `.csproj` files is rejected because Unity regenerates them. Running repeated `dotnet build` after minor syntax edits is rejected because the batch explicitly forbids CPU abuse and AGENTS forbids builds under contention.

Scalability potential: Low tier gets deterministic CLI gate and no extra runtime cost. Middle tier gets stable assembly boundaries. High tier gets faster validation cadence after graph health. Ultra tier spends saved engineering time on visual/runtime features, not compiler stall recovery.

Hardware Impact: Static discovery and MSBuild interception should reduce repeated failed CLI compile loops on i3/MX350 host. Microsecond gain is PENDING; no profiler/build timing exists yet.

Evidence Class: STATIC_DOC and STATIC_SOURCE only until guarded compile logs exist.

## Mandate Selection

Problem: Build graph task touches compile gates, domain reload, registry reset, MapMagic boundary, URP/ShaderGraph packages, and evidence claims.

Solution: Read these mandates before coding: CI_MATH_VIOLATIONS_Gate, CORE_Global_State_Reset_NonReload_Transitions, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Execution_Phases, VOX_MapMagic_Voxel_Seam_Alignment_Integration, OPT_Zero_GC_Policy_AllocFree_Mandate, QA_Evidence_Text_Filter_Audit, REND_URP_Graphics_HotPath_Optimization_HLOD.

Rejected Alternatives: Reading the entire registry is rejected as token/time waste; these eight directly constrain the task.

Scalability potential: Tooling-only work does not alter runtime quality tiers. It enables future Low/Middle/High/Ultra runtime verification by unblocking compile gates.

Hardware Impact: Avoids unnecessary compiler invocations on weak host silicon. Estimate: PENDING_STATIC_ANALYSIS.
