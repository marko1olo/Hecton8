# Status_PHYSICS_CULLING_OVERSEER

STATUS: PENDING VERIFICATION
Agent: LOCOMOTION_ENGINEER
Prompt ID: PHYSICS_CULLING_OVERSEER
Task Count: 19
Domain: META, POLISH & INTEGRATION (Quality Control)

## Mandates Loaded
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## State Machine
- [ ] Task 1: SINGLETON ERADICATION | Pending code inspection. | Alternative rejected: blind rewrite before locating existing owner. | Estimate: TBD us.
- [ ] Task 2: SIGNAL MIGRATION | Pending code inspection. | Alternative rejected: scene search / FindObjectOfType. | Estimate: TBD us.
- [ ] Task 3: ASMDEF ISOLATION | Pending asmdef inspection. | Alternative rejected: cross-domain direct references. | Estimate: TBD us.
- [ ] Task 4: DEAD CODE HUNT | Pending prefab/script scan. | Alternative rejected: leaving per-object distance Update virus. | Estimate: TBD us.
- [ ] Task 5: NATIVE RIGIDBODY REGISTRY | Pending implementation. | Alternative rejected: List.Remove / managed-only state. | Estimate: TBD us.
- [ ] Task 6: BURST DISTANCE CULLING | Pending implementation. | Alternative rejected: main-thread Vector3.Distance. | Estimate: TBD us.
- [ ] Task 7: FRUSTUM BIAS | Pending implementation. | Alternative rejected: uniform threshold wasting behind-camera bodies. | Estimate: TBD us.
- [ ] Task 8: DEPTH-BASED VARIANCE | Pending implementation. | Alternative rejected: full-distance physics in abyss visibility. | Estimate: TBD us.
- [ ] Task 9: VELOCITY DAMPENING | Pending implementation. | Alternative rejected: visual freeze with unchanged velocity. | Estimate: TBD us.
- [ ] Task 10: EXPLICIT SLEEP DISPATCH | Pending implementation. | Alternative rejected: relying on PhysX implicit sleep. | Estimate: TBD us.
- [ ] Task 11: KINEMATIC CULL | Pending implementation. | Alternative rejected: sleeping only beyond solver range. | Estimate: TBD us.
- [ ] Task 12: COLLIDER STRIPPING | Pending implementation. | Alternative rejected: active MeshColliders outside interaction range. | Estimate: TBD us.
- [ ] Task 13: HYSTERESIS | Pending implementation. | Alternative rejected: immediate edge flipping. | Estimate: TBD us.
- [ ] Task 14: EXCLUSION BITMASK | Pending implementation. | Alternative rejected: string tags for critical items. | Estimate: TBD us.
- [ ] Task 15: EVENT BUS AWAKEN | Pending existing signal inspection. | Alternative rejected: inventing direct dependencies. | Estimate: TBD us.
- [ ] Task 16: ORIGIN SHIFT SAFETY | Pending existing AUP signal inspection. | Alternative rejected: waking during origin shift. | Estimate: TBD us.
- [ ] Task 17: ZERO-GC TRACKING | Pending implementation. | Alternative rejected: List.Remove / LINQ. | Estimate: TBD us.
- [ ] Task 18: MATH LOD | Pending hardware tier contract inspection. | Alternative rejected: one-size sleep distance. | Estimate: TBD us.
- [ ] Task 19: OMEGA COMPILE CHECK | Pending build. | Alternative rejected: static-only confidence. | Estimate: TBD us.

## Iteration Log
- Loop 0: Prompt extracted, domain checked, status/rationale missing confirmed, mandates loaded. No code modified.
