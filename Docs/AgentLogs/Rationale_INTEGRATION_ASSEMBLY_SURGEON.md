# Rationale_INTEGRATION_ASSEMBLY_SURGEON

Agent: INTEGRATION_ASSEMBLY_SURGEON
Domain: Unity Compilation Graph / Integrator
Status: PENDING VERIFICATION

## Decision 0 - Session Initialization

Problem: No existing status or rationale file for this assigned compile medic ID.
Solution: Created disk-backed checklist and rationale before touching compile code, using the batch prompt as primary directive and mandate registry as the technical filter.
Rejected Alternatives: Chat-only progress was rejected because the batch protocol makes disk state authoritative. Reading prior unrelated agents' logs as architectural input was rejected because the prompt says to ignore neighboring tasks unless they are compile artifacts.
Scalability potential: Low/Middle/High/Ultra are not runtime visual tiers for this initialization step; compile-boundary cleanup preserves later tier-specific runtime work by avoiding dependency cycles.
Hardware Impact: 0 us runtime change on i3/MX350. This is process state only.

## Decision 1 - Mandate Selection

Problem: Compile wall can tempt brute-force project-reference additions that create cycles.
Solution: Selected `ARCH_Global_Registry_ServiceLocator_DI_Init`, `PROJECT_LTS_Compatibility_Layer`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, and `QA_Evidence_Text_Filter_Audit` as the active mandate set.
Rejected Alternatives: Rendering/physics/audio domain mandates were rejected until diagnostics prove domain-specific runtime edits are required. Adding leaf assemblies to Core was rejected under H-PHI defense.
Scalability potential: Low tier benefits from strict contract isolation because it keeps Core compile/runtime surface smaller; Ultra tier remains free to add overkill visuals behind leaf systems without contaminating Core.
Hardware Impact: Expected runtime gain is indirect only: fewer hard references and smaller compile/runtime dependency surface. Estimated frame impact: 0 us until runtime code is touched.
