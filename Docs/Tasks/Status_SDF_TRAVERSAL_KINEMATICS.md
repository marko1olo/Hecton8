# Status_SDF_TRAVERSAL_KINEMATICS

Prompt: `SDF_TRAVERSAL_KINEMATICS`
Role: `LOCOMOTION_ENGINEER`
Domain: ECHELON 4 - PLAYER, KINEMATICS & TOOLS
Status: PENDING VERIFICATION

## Mandates Read Before Coding
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Task Checklist
- [ ] 1. SINGLETON ERADICATION: N/A, extends existing player motor path. Justification pending code scan. Alternative rejected pending.
- [ ] 2. SIGNAL MIGRATION: Emit `PlayerStateSignal(Squeezing)`. Justification pending signal contract scan. Alternative rejected pending.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Physics.Kinematics` -> Contracts. Justification pending asmdef scan. Alternative rejected pending.
- [ ] 4. DEAD CODE HUNT: Remove player movement `Physics.ComputePenetration`. Justification pending movement scan. Alternative rejected pending.
- [ ] 5. THE SDF PROBE: Query `VoxelSdfTexture3D`/SDF provider at `TargetAUP`. Justification pending SDF provider scan. Alternative rejected pending.
- [ ] 6. GRADIENT DESCENT: Calculate 6-sample open-space SDF gradient. Justification pending math integration. Alternative rejected pending.
- [ ] 7. KINEMATIC SQUEEZE: Add orthogonal gradient correction to slide velocity. Justification pending motor integration. Alternative rejected pending.
- [ ] 8. CAMERA TILT: Signal camera roll via camera juice interface. Justification pending camera contract scan. Alternative rejected pending.
- [ ] 9. SPEED PENALTY: Reduce max speed by 60% during squeeze. Justification pending motor profile scan. Alternative rejected pending.
- [ ] 10. OXYGEN STRESS: Send stress delta via EventBus. Justification pending signal contract scan. Alternative rejected pending.
- [ ] 11. HAPTIC SCRAPE: Emit low-amplitude haptic request while squeezing. Justification pending haptics contract scan. Alternative rejected pending.
- [ ] 12. ANTI-TUNNELING: Squeeze after CCD resolver. Justification pending execution order scan. Alternative rejected pending.
- [ ] 13. AUP SHIFT SAFETY: Re-probe after `AupShiftSignal`. Justification pending AUP signal scan. Alternative rejected pending.
- [ ] 14. MATH LOD: Low tier uses 4-tap tetrahedral gradient. Justification pending hardware tier contract scan. Alternative rejected pending.
- [ ] 15. ZERO-GC: Keep math inside Burst/job or cached struct path. Justification pending implementation. Alternative rejected pending.
- [ ] 16. AUDIO TIE-IN: Emit fabric scrape acoustic signal while squeezing. Justification pending audio signal scan. Alternative rejected pending.
- [ ] 17. BLACKBOX DUMP: Push `SqueezeInterventions` to telemetry. Justification pending telemetry contract scan. Alternative rejected pending.
- [ ] 18. EXECUTION PHASE: Runs in `SIMULATION`. Justification pending dispatcher scan. Alternative rejected pending.
- [ ] 19. OMEGA COMPILE CHECK: Verify SDF trilinear interpolation compiles in Burst. Justification pending compile/test.

## Iterative Loop Log
- Loop 0: Prompt extracted, domain checked, mandates selected. Code scan not started. Status: PENDING VERIFICATION.
