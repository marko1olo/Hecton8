# ANIM_PROCEDURAL_LEGS_IK Status

Status: PENDING VERIFICATION
Domain: ECHELON 4 PLAYER, KINEMATICS & TOOLS / VR LOWER BODY IK
Mandates read: ANIM_Contextual_Physical_IK, ANIM_IK_FABRIK_GroundSnapping_Procedural, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, MATH_Coordinate_Precision_AUP_FloatingOrigin, DBG_Telemetry_Crash_Reporting_PostMortem, PHYS_Physics_Integrity_Determinism_ForceMode, REND_Foveated_Simulation_LOD

## Checklist

- [ ] Task 1: SINGLETON ERADICATION / extend `ContextualPhysicalIkRuntime` | DOD: no new singleton owner; integrate lower-body data into existing runtime registry | Rejected: new IK manager | Estimate: 0.0 us/frame extra ownership overhead.
- [ ] Task 2: SIGNAL MIGRATION / consume `KccVelocitySignal` | DOD: NativeQueue-backed typed velocity signal, latest snapshot consumed by IK | Rejected: direct `HectonPlayerMovement` dependency | Estimate: <1.0 us/frame.
- [ ] Task 3: ASMDEF ISOLATION / `Hecton8.Animation.IK` -> Contracts | DOD: lower-body data lives in Animation.IK, asmdef already references Core.Contracts | Rejected: Gameplay-only private struct | Estimate: 0.0 us/frame.
- [ ] Task 4: DEAD CODE HUNT / eradicate Unity Animator foot IK passes | DOD: static scan for `OnAnimatorIK` and `SetIK*`; no foot IK pass found | Rejected: editing unrelated Animator parameter drivers | Estimate: 0.0 us/frame.
- [ ] Task 5: S.O.A. LEG TARGETS | DOD: persistent `NativeArray<float3>` foot target/current lanes | Rejected: deriving from target frame only | Estimate: <2.0 us/frame.
- [ ] Task 6: RAYCAST BATCH | DOD: existing batched foot raycasts retained, ground-contact gated by 3m seabed distance | Rejected: synchronous `Physics.RaycastNonAlloc` | Estimate: existing batch cost unchanged.
- [ ] Task 7: STEP TRIGGER | DOD: squared-distance threshold per foot, alternating phase lock | Rejected: simultaneous foot stepping | Estimate: <2.0 us/frame.
- [ ] Task 8: STEP CURVE | DOD: triangle-wave +Y lift over nlerp/lerp foot path | Rejected: physical leg simulation | Estimate: <2.0 us/frame.
- [ ] Task 9: COSINE RULE IK | DOD: existing Burst `SolveTwoBone` path consumes stepped foot frames | Rejected: Unity Animator IK | Estimate: existing animation job cost.
- [ ] Task 10: SWIMMING POSTURE | DOD: no-ground or >3m ground distance blends feet backward with KCC velocity | Rejected: full swim-body solver | Estimate: <2.0 us/frame.
- [ ] Task 11: BODY ROTATION | DOD: pelvis yaw bias follows camera forward through target frame | Rejected: full spine twist solve | Estimate: <1.0 us/frame.
- [ ] Task 12: AUP SHIFT SAFETY | DOD: all new foot lanes rebase on origin shift | Rejected: cached world-space foot targets across shift | Estimate: shift-only.
- [ ] Task 13: H-PHI ALIGNMENT | DOD: `FootIKData` `[StructLayout(Pack=1)]` | Rejected: bool fields/default layout | Estimate: 0.0 us/frame.
- [ ] Task 14: MATH LOD | DOD: Low/MX350 non-XR disables foot IK; XR remains mandatory | Rejected: always-on desktop low-tier IK | Estimate: saves batched foot solve on low non-XR.
- [ ] Task 15: OMEGA COMPILE CHECK | DOD: compile attempted and failures fixed or blocked after 3 strikes | Rejected: chat-only verification | Estimate: verification-only.

## Iteration Log

- Loop 0: Prompt extracted from `CURRENT_BATCH.md`; existing IK/KCC systems inspected. Status: PENDING VERIFICATION.
