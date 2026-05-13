# Status - WAKE_TURBULENCE_COMPUTE

Prompt: Leviathan & Pod Advection
Agent: VFX_TECHNICAL_ARTIST
Domain: Environment.Fluids / VFX
Status: PENDING VERIFICATION

## Hygiene

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`: yes
- Existing status file at session start: missing
- Existing rationale file at session start: missing
- Task count in XML tag: 15
- Polish mandate parsed: no

## Relevant Mandates

- [ ] CORE_Weather_Abyssal_FlowField_Currents.txt
- [ ] REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- [ ] GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- [ ] GPU_Compute_Warp_Sizing_Mobile.txt
- [ ] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- [ ] DBG_Telemetry_Crash_Reporting_PostMortem.txt
- [ ] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [ ] OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Tasks

- [ ] 1. SINGLETON ERADICATION: Extend `HectonFluidEngine`.
- [ ] 2. SIGNAL MIGRATION: Consume `FluidImpulseSignal(AUP, Vector, Radius, Lifetime)`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Environment.Fluids` -> `Contracts`.
- [ ] 4. WAKE S.O.A.: Define `StructuredBuffer<float4> _DynamicWakes`; max 8 active wakes.
- [ ] 5. SIGNAL DRAIN: Dead-slot wake allocation from `FluidImpulseSignal`.
- [ ] 6. SHADER MATH: 8-wake vortex/push injection in `Hecton_FluidAdvection.compute`; no `length()`.
- [ ] 7. LEVIATHAN TIE-IN: Emit impulse on sharp Alpha Leviathan tail direction change.
- [ ] 8. DROP POD TIE-IN: Emit 50m splashdown push impulse.
- [ ] 9. DECAY: Native job reduces intensity by `dt * decayRate` before upload.
- [ ] 10. AUP SHIFT SAFETY: Subtract `AupShiftSignal` from active wake AUPs.
- [ ] 11. MATH LOD: Low tier caps active wakes to 2.
- [ ] 12. EXECUTION PHASE: Compute dispatch in `VISUAL_SYNC`.
- [ ] 13. ZERO-GC: Array updates allocate 0 bytes.
- [ ] 14. BLACKBOX DUMP: Push `ActiveTurbulenceWakes` to telemetry.
- [ ] 15. OMEGA COMPILE CHECK: Verify GPU buffer mapping.

## Iteration Log

### Loop 0 - Intake

- Extracted prompt and counted 15 tasks.
- Created status/rationale files because none existed at session start.
- Next: read mandates and inspect current fluid/compute implementation.
