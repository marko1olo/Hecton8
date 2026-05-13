# Status_ABYSSAL_CURRENT_ADVECTION

Agent: FLUID_MECHANIC  
Prompt ID: ABYSSAL_CURRENT_ADVECTION  
Domain: Echelon 2.19 Abyssal Flow Fields / Echelon 7.66 Marine Snow & Silt Compute  
Status: PENDING VERIFICATION  
Task Count: 18 numbered primary objectives. XML header says 19; no task 19 exists.

## Mandates Read Before Coding
- CORE_Weather_Abyssal_FlowField_Currents.txt
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Loop 0 - Prompt Extraction
- [x] Extracted `<AGENT_PROMPT id="ABYSSAL_CURRENT_ADVECTION">` from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell raw file read and regex, from opening tag to closing tag.
  - DOD practice: full-cover extraction, not partial MCP reading.
  - Alternative rejected: relying on open tabs or neighbor prompts.
  - Estimate: 250 us one-time CLI scan.
- [x] Read authoritative domain map.
  - DOD practice: bounded domain assignment against `Docs/Actual Domains of Project.txt`.
  - Alternative rejected: treating "fluid" as generic Physics ownership.
  - Estimate: 200 us one-time CLI read.

## Loop 1 - Tasks 1-5
- [ ] 1. Extend `HectonFluidEngine`; singleton eradication N/A.
- [ ] 2. Consume `DebrisSpawnSignal`.
- [ ] 3. ASMDEF isolation: `Hecton8.Environment.Fluids` to Contracts.
- [ ] 4. Dead code hunt: dropped loot must not use `Rigidbody.AddForce`.
- [ ] 5. Add unified dispatch/binds in `Hecton_FluidAdvection.compute`.
- [ ] Compile verification after Tasks 1-5.

## Loop 2 - Tasks 6-10
- [ ] 6. Integrate velocity from `AbyssalFlowField`.
- [ ] 7. Apply buoyancy inversion per element type.
- [ ] 8. Sample `VoxelSdfTexture3D` in compute.
- [ ] 9. Stop debris/silt or pop bubbles on solid SDF.
- [ ] 10. Hook exhale/underwater bubble source into GPU bubble AUP buffer.
- [ ] Compile verification after Tasks 6-10.

## Loop 3 - Tasks 11-15
- [ ] 11. Apply AUP shift offset before integration.
- [ ] 12. Low tier Math LOD fallback disables debris/bubble compute.
- [ ] 13. Fixed buffers; 0 B managed allocation in hot path.
- [ ] 14. Enforce caps: 1000 debris, 2000 bubbles.
- [ ] 15. Dispatch in visual sync phase.
- [ ] Compile verification after Tasks 11-15.

## Loop 4 - Tasks 16-18
- [ ] 16. Push `ActiveAdvectedParticles` to telemetry.
- [ ] 17. RenderGraph path or documented blocker if URP hook is absent.
- [ ] 18. Verify `numthreads(64,1,1)`.
- [ ] Compile verification after Tasks 16-18.

## Loop 5 - Re-Verification
- [ ] Re-read prompt after Tasks 1-18.
- [ ] Re-read own code and buffer binds.
- [ ] Ensure compute buffers are unbound/released safely.
- [ ] Run final compile/console check.
- [ ] Polish mandate read and executed only after all core tasks are done or blocked.

## Compile Attempts
- Attempt 0: PENDING.

