# KINEMATIC_CCD_RESOLVER Status

Agent: LOCOMOTION_ENGINEER
Domain: ECHELON 4 - PLAYER, KINEMATICS & TOOLS
Prompt: KINEMATIC_CCD_RESOLVER
Status: PENDING VERIFICATION

## Mandates Read

- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Kinematic_Interaction_Hands.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_Rsqrt_i3_SIMD.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt

## Checklist

- [ ] Task 1 - SINGLETON ERADICATION: extend GlobalPhysicsStateManager without adding singleton pattern.
- [ ] Task 2 - SIGNAL MIGRATION: emit native HighSpeedImpactSignal on CCD trigger.
- [ ] Task 3 - ASMDEF ISOLATION: Hecton8.Physics.CCD references Contracts only where applicable.
- [ ] Task 4 - DEAD CODE HUNT: remove arbitrary velocity clamp hacks used to hide tunneling.
- [ ] Task 5 - THE CCD SWEEP: schedule capsule sweep before kinematic position application.
- [ ] Task 6 - HIT FRACTION: rollback motion by hit fraction minus safety margin.
- [ ] Task 7 - DEFLECTION VECTOR: compute slide velocity from velocity and hit normal.
- [ ] Task 8 - MULTI-BOUNCE: limit deflection loop to bounded bounces.
- [ ] Task 9 - IMPACT KINETIC ENERGY: compute lost KE and emit combat signal when large.
- [ ] Task 10 - AUDIO SPARK: emit debris spark signal with hit AUP and normal.
- [ ] Task 11 - HAPTIC RUPTURE: emit haptic request from lost KE.
- [ ] Task 12 - CAMERA JUICE TIE-IN: emit camera directional bias from impact normal.
- [ ] Task 13 - SPEED GATE: bypass CCD below velocity length squared 25.0.
- [ ] Task 14 - AUP SHIFT SAFETY: prevent cross-origin sweeps after shift.
- [ ] Task 15 - MATH LOD: low tier uses one bounce and stop-on-hit.
- [ ] Task 16 - ZERO-GC: use preallocated/native sweep result buffers.
- [ ] Task 17 - LEVIATHAN BITE DEFLECTION: route lunge motion through CCD.
- [ ] Task 18 - TELEMETRY: write CcdInterventions to Blackbox.
- [ ] Task 19 - OMEGA COMPILE CHECK: verify compile/Burst-compatible slide math.

## Loop Notes

Loop 0: Prompt extracted from Docs/Tasks/CURRENT_BATCH.md by CLI. Code inspection pending.
