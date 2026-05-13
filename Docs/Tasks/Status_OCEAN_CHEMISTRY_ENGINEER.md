# Status: OCEAN_CHEMISTRY_ENGINEER

Domain: ENVIRONMENT_ENGINEER / Environment.Fluids  
Task Count: 19  
Prompt Source: Docs/Tasks/CURRENT_BATCH.md  
Current State: PENDING VERIFICATION  
Last Prompt Extract: 2026-05-13

## Mandates Loaded

- OPT_Zero_GC_Policy_AllocFree_Mandate: no managed allocation in hot paths; no LINQ/new strings inside ticks/jobs.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: render/audio brine as deterministic fakes before real simulation.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits: MX350 target; any 0.1 ms addition is suspect.
- OPT_Native_Memory_Collections_JobSystem_Protocol: NativeArray ownership, disposal, no hidden job sync.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: absolute brine heights; runtime evaluation subtracts floating-origin offset.
- ARCH_Global_Registry_ServiceLocator_DI_Init: GlobalRegistry/EventBus only for cross-domain coupling.
- DBG_Telemetry_Crash_Reporting_PostMortem: fixed black-box state, no "unknown crash" reports.
- REND_Shader_Noir_Aesthetics_Dithering_Fog: fog/depth/caustic lies over simulated volume.

## Task Loop 1: Tasks 1-5

- [ ] 1. SINGLETON ERADICATION: Purge BrineManager.Instance.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 2. SIGNAL MIGRATION: Player entry into brine emits FluidDensityChangedSignal.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 3. ASMDEF ISOLATION: Hecton8.Environment.Fluids -> Contracts.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 4. DEAD CODE HUNT: Eradicate OnTriggerEnter from all Brine Pool prefabs.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 5. BRINE PLANE S.O.A.: Define NativeArray<float> BrineHeights mapped to 50x50m sectors.
  - DOD:
  - Rejected:
  - Estimate:
- Compile Check:

## Task Loop 2: Tasks 6-10

- [ ] 6. BUOYANCY OVERRIDE: HectonFluidEngine density multiplier 3.0 below brine height.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 7. KCC MOVEMENT PENALTY: density multiplier reduces swim speed by 40%.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 8. DEPTH PLANE SHADER: global _BrineHeightY and _BrineColor, no physical mesh render dependency.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 9. POST-PROCESS FOG: HectonVisorUberPost applies green/yellow brine fog below plane.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 10. CAUSTICS ABSORPTION: caustics disabled below brine plane.
  - DOD:
  - Rejected:
  - Estimate:
- Compile Check:

## Task Loop 3: Tasks 11-14

- [ ] 11. AUDIO MUFFLE: camera brine submersion applies heavy low-pass.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 12. AUP SHIFT SAFETY: brine heights absolute; runtime checks subtract ShiftOffset.y.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 13. TOXICITY LINK: submerged brine injects +10 CO2 equivalent pressure into GasDynamicsSolver local room.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 14. MATH LOD: Low Tier post fog uses hard clipping plane, not soft fade.
  - DOD:
  - Rejected:
  - Estimate:
- Compile Check:

## Task Loop 4: Tasks 15-18

- [ ] 15. ZERO-GC: height checks mathematically evaluated in Burst; 0 bytes allocated.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 16. TELEMETRY: write BrineSubmersionTime to Blackbox.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 17. EVENT BUS: emit AcousticPingSignal(ThickFluid) when hull breaches brine layer.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 18. CROSS-DOMAIN AUDIT: Fauna pathfinding treats Brine sectors as high-cost nodes.
  - DOD:
  - Rejected:
  - Estimate:
- Compile Check:

## Task Loop 5: Task 19 + Re-Verification

- [ ] 19. OMEGA COMPILE CHECK: verify shader uses world-space Y correctly without allocating matrices.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] Re-read prompt and re-check buoyancy math for infinite acceleration.
- [ ] Polish Mandate parsed only after every task is checked or blocked.
- Final Compile Check:

