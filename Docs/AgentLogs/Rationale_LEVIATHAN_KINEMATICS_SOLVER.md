# Rationale_LEVIATHAN_KINEMATICS_SOLVER

Status: PENDING VERIFICATION.

## Mandates Read Before Code

- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt
- ANIM_Contextual_Physical_IK.txt
- REND_GPU_Driven_Animation_VAT.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt

## Decision 0: Runtime Shape

Problem: Leviathan visual body clips because the current visual authority is a simple transform path, while the prompt requires no Unity Physics and no Animator/SkinnedMeshRenderer dependency.
Solution: Build a persistent native SOA solver around spine positions/velocities/matrices, schedule Burst jobs in simulation cadence, then upload matrices for GPU deformation/BRG-style rendering.
Rejected Alternatives: Standard Unity Animator IK and SkinnedMeshRenderer are rejected by the prompt and GPU-driven fauna mandate. Unity Physics raycasts are rejected because task requires SDF/MapMagic probing and no Unity Physics.
Scalability potential: Low uses eight segments and height fallback only; Middle uses 12-16 segments; High uses 20 segments with SDF pushout; Ultra keeps 20 segments and spends saved CPU on smoother tail whip and denser visual matrices.
Hardware Impact: Expected low-end i3/MX350 gain is avoiding Animator CPU skinning and PhysX queries; actual microseconds require profiler evidence.

## Decision 1: Black Box Requirement

Problem: IK is critical creature presentation and can corrupt render matrices if NaN reaches GPU buffers.
Solution: Add a fixed 300-entry native telemetry ring for high-level spine state and non-finite flags, with dump path `Docs/AgentLogs/Dump_LEVIATHAN_KINEMATICS_SOLVER.bin`.
Rejected Alternatives: Debug.Log-only failure reporting is rejected because it allocates, loses preceding frames, and violates Black Box protocol.
Scalability potential: Low stores compact hashes and key positions; Ultra can preserve more per-segment detail if needed without changing external contract.
Hardware Impact: 300 compact entries are negligible native memory; avoids expensive crash diagnosis loops on weak hardware.
