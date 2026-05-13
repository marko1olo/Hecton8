# Status_TERRAIN_GPR_SYSTEM

Agent: GEOLOGY_MASTER
Prompt: TERRAIN_GPR_SYSTEM
Domain: WORLD_GENERATION_TERRAIN_GEOLOGY
Status: PENDING VERIFICATION
Task count: 19

Mandates selected before coding:
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Loop 1: Tasks 1-5
- [ ] 1. SINGLETON ERADICATION: Scan `Assets/_Project/Scripts/World/Resources`. Delete `GPRManager.Instance`. Register `IGroundRadarService`.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 2. SIGNAL MIGRATION: Consume `ScannerToolActiveSignal` and emit `AcousticPingSignal(Subsurface)`.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 3. ASMDEF ISOLATION: `Hecton8.World.GPR` depends ONLY on Contracts and Mathematics.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 4. DEAD CODE HUNT: Eradicate any `Physics.SphereCastAll` used for finding buried ores.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 5. GPR S.O.A.: Define `NativeArray<float3> GprHits` and `NativeArray<float> GprSignalStrength`.
  - DOD:
  - Rejected:
  - Estimate:

## Loop 2: Tasks 6-10
- [ ] 6. SDF RAYMARCH JOB: In Burst, project 64 rays downward from the Submarine's AUP. Step through the `VoxelSdfTexture3D` density field.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 7. ORE DETECTION: If a ray hits `Density > 0.5` (Solid Rock), check the `OrePositions` NativeArray from the `WORLD_RESOURCE_SPAWNER`. If distance < 5m, register a GPR Hit.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 8. ATTENUATION MATH: Signal strength decays via `math.rcp(depth * depth)`.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 9. GPU BUFFER UPLOAD: Upload the `GprHits` to a `StructuredBuffer<float4>` (`w` = signal strength).
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 10. BRG DRAWING: Use `Graphics.RenderMeshIndirect` to draw pulsing concentric circles at the hit AUPs.
  - DOD:
  - Rejected:
  - Estimate:

## Loop 3: Tasks 11-15
- [ ] 11. DEPTH COLOR MAPPING: In the shader, map signal strength to color (Strong = Bright Green, Weak = Deep Blue).
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 12. SCAN DECAY: The GPR hits fade over 3.0 seconds. Evaluate this decay in the Burst job, not the shader, to cull dead points.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 13. AUP SHIFT SAFETY: Subtract `AupShiftSignal` from all active `GprHits` natively.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 14. MATH LOD: On Low Tier (MX350), cast only 16 rays instead of 64.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 15. ZERO-GC: No allocations. `NativeArray` buffers are persistent.
  - DOD:
  - Rejected:
  - Estimate:

## Loop 4: Tasks 16-19
- [ ] 16. BLACKBOX DUMP: Push `ActiveGprPings` to Telemetry.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 17. AUDIO CUE: Push `ToolAcousticSignal(GPR_Return)` with pitch modulated by the highest signal strength.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 18. CROSS-DOMAIN AUDIT: Ensure the Submarine OS cockpit radar can read this same buffer.
  - DOD:
  - Rejected:
  - Estimate:
- [ ] 19. OMEGA COMPILE CHECK: Verify Raymarch job has a hard step-limit (e.g., max 10 steps).
  - DOD:
  - Rejected:
  - Estimate:

## Loop 5: Recursive Re-Verification
- [ ] Re-read prompt after core tasks.
- [ ] Self-audit raymarch math for finite bounds and no infinite loops.
- [ ] Compile verification.
- [ ] Omega polish mandate parsed only after all core tasks are checked or blocked.
