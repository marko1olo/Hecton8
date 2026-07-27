# Hecton8 Voxel Physics, Vertex Color Encoding & Terrain Erosion Master Plan

## Authority Quote & Constraints
> "Physics interaction is blocked until collider bake is complete... GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity." — `voxels.md`
> "Zero-GC Terrain Height Reads: On chunk generation complete (or chunk stream-in), the terrain subsystem copies its height data... Continuous coordinate wrapping must be achieved via signed modulo (math.fmod) exclusively" — `terrain.md`

## Architecture & Scope
This plan covers the voxel physics bake signal publishing, vertex color encoding for URP cave shader blending, and non-destructive perimeter guards for terrain erosion jobs.

## Milestones & Status
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1-M7 | Voxel Surface Nets & SDF Determinism Pipeline | Dual-mesh extraction, vault memory, SDF determinism, capacity protection, and forensic verification. | none | DONE |
| M8 | Voxel Physics Bake Signal & Kinematic Spawner Integration (R1) | Audit & strengthen WorldChunkPhysicsBakedSignal publishing in HectonVoxelVolume.cs & HectonVoxelEngine.cs. Integrate signal reception in HectonPlayerSpawner.cs & MapMagicBridge.cs. | M7 | DONE |
| M9 | Voxel Vertex Color Channel & Shader Blending Audit (R2) | Audit VoxelSurfaceColorEncoding in HectonVoxelEngine.cs. Ensure Red (Floor weight), Green (Wall weight), Alpha (AO) conform to URP cave shader blending without debug artifacts or NaNs. | M8 | DONE |
| M10 | Terrain Boundary Guard & Erosion Stability Audit (R3) | Audit WorldProceduralTerrainThermalWeatheringJobs.cs & HydraulicErosionJob.cs. Ensure perimeter guards at chunk edge boundaries [x==0, z==0] preserve mass-conserving talus heightmaps across contiguous tiles. | M9 | DONE |
| M11 | Final Verification, Reviewer, Challenger & Forensic Integrity Audit | Reviewer code review (PASS), Challenger empirical stress testing (PASS), Forensic Integrity Audit (CLEAN). | M10 | DONE |

## Interface Contracts & Data Flow
- **Signals**: `WorldChunkPhysicsBakedSignal` with `FlagColliderActive` and `FlagHeightmapSynced` and minCorner `TerrainPosition = pos - size * 0.5f`.
- **Color Encoding**: `VoxelSurfaceColorEncoding.ResolveFloorWeight` delegating SSOT in `VoxelSurfaceNetsJobs.cs` packing `Color32(R: FloorWeight, G: WallWeight, B: 0, A: AmbientOcclusion)`.
- **Erosion Jobs**: `WorldProceduralTerrainThermalWeatheringJobs.cs` & `HydraulicErosionJob.cs` with `writeMaxZ - 2` clamping, `return 0f` out-of-window sediment carrying, and outer apron protection `x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1`.

## Verification Gate Summary
- **Reviewer**: PASS (Reviewer Iteration 2)
- **Challenger**: PASS (Challenger Iteration 2)
- **Forensic Auditor**: CLEAN (Auditor Iteration 2)
