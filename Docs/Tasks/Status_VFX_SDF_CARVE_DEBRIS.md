# Status - VFX_SDF_CARVE_DEBRIS

Agent: VFX_TECHNICAL_ARTIST
Domain: ECHELON 7 #66 Marine Snow/Silt Compute VFX with ECHELON 2 SDF/Carve/Flow integration
Prompt: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="VFX_SDF_CARVE_DEBRIS">`
Status: PENDING VERIFICATION

## Mandates Read Before Coding

- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `GPU_Compute_Warp_Sizing_Mobile.txt`
- `REND_GPU_Sovereignty.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Core Tasks

- [ ] 1. Singleton eradication: N/A. DOD pending: verify no singleton introduced. Rejected alternative pending. Estimate pending.
- [ ] 2. Signal migration: consume `VoxelCarveEvent(AUP, Radius)`. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 3. ASMDEF isolation: `Hecton8.VFX.Debris` references Contracts. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 4. Debris buffer S.O.A.: `StructuredBuffer<float4> CarveDebris` position/lifetime, max 4096. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 5. C# injection job: dead-slot scan and 64-particle injection per carve. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 6. Random jitter: stable `Unity.Mathematics.Random` seed from frame + AUP. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 7. GPU advection: bind to `Hecton_FluidAdvection.compute`; gravity + AbyssalFlowField drag. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 8. SDF collision: sample `VoxelSdfTexture3D`, collide/decay; Low tier skip. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 9. BRG/indirect render: `Graphics.RenderMeshIndirect`, low-poly rock mesh, `Hecton_CoreLit`. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 10. AUP shift safety: subtract `AupShiftSignal` before compute dispatch. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 11. H-PHI sovereignty: request debris buffer from `GlobalDataVault`. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 12. Math LOD: Low tier = 16 particles/carve and skip SDF collision. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 13. Zero-GC: persistent buffers; no per-frame allocations. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 14. Blackbox dump: push `ActiveCarveDebrisCount` to telemetry. DOD pending. Rejected alternative pending. Estimate pending.
- [ ] 15. Omega compile check: verify indirect args buffer logic. DOD pending. Rejected alternative pending. Estimate pending.

## Loop Log

- Loop 0: Prompt extracted, status missing, rationale missing. Fresh files created. Code not touched yet.
