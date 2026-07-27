# Original User Request

## 2026-07-26T22:22:12Z

Fix the voxel SDF sampling logic in HectonVoxelEngine.cs (Section 4.2 of Handoff) so that camera orientation and GlobalQualityWeight do not mutate underlying SDF terrain geometry truth prior to extraction, restoring determinism and preventing mesh/collider mismatch across quality levels.

Working directory: C:\hades\Hecton8
Integrity mode: development

## Requirements

### R1. Remove Quality & Camera Bias from Core SDF Noise Evaluation
Ensure ApplyVoxelCliffOverhangNoise or overhang/cave SDF noise functions evaluate using canonical position/world inputs independent of GlobalQualityWeight or camera view vectors prior to mesh extraction.

### R2. Deterministic Volume Reconstruction
Guarantee that re-extracting a voxel volume under different graphics quality settings or camera angles yields deterministic SDF values and identical collision topology.

### R3. Capacity Overflow Protection
Prevent scratch capacity overflows when building dense voxel chunks at lower quality settings so chunk silhouettes do not disappear.

## Acceptance Criteria

### Determinism & Quality Gate Compliance
- [ ] Voxel SDF sampling returns identical values for identical world coordinates across all camera view directions and quality tiers.
- [ ] No mesh/collider vertex divergence occurs due to camera angle or quality weight shifts.
- [ ] Code compiles cleanly and passes all pre-commit Iron Gate checks.

## Follow-up — 2026-07-26T22:51:20Z

Perform deep codebase analysis using Reconnaissance Arsenal and execute targeted fixes for voxel physics signals, voxel vertex color channel encoding, and terrain chunk boundary erosion guards in Hecton8.

Working directory: C:\hades\Hecton8
Integrity mode: development

## Requirements

### R1. Voxel Physics Bake Signal & Kinematic Spawner Integration
Verify and strengthen WorldChunkPhysicsBakedSignal publishing in HectonVoxelVolume.cs and HectonVoxelEngine.cs. Ensure HectonPlayerSpawner.cs and MapMagicBridge.cs reliably receive physics readiness signals so player/entities never drop through colliders.

### R2. Voxel Vertex Color Channel & Shader Blending Audit
Audit VoxelSurfaceColorEncoding in HectonVoxelEngine.cs to ensure Red (Floor weight), Green (Wall weight), and Alpha (Ambient Occlusion) channels strictly conform to the URP cave shader texture blending spec without debug artifacts or NaN values.

### R3. Terrain Boundary Guard & Erosion Stability Audit
Audit WorldProceduralTerrainThermalWeatheringJobs.cs and HydraulicErosionJob.cs using ripgrep/ast-grep to ensure chunk edge boundaries [x==0, z==0] carry non-destructive guards and preserve mass-conserving талус heightmaps across contiguous tiles.

## Acceptance Criteria

### Physics & Terrain Integrity
- [ ] WorldChunkPhysicsBakedSignal is published on every completed PhysX chunk bake with valid FlagColliderActive and FlagHeightmapSynced.
- [ ] Voxel vertex color channels produce finite, valid floor/wall blending weights (R: floor, G: wall, B: 0, A: AO).
- [ ] Thermal weathering and hydraulic erosion jobs execute with 0 perimeter height artifacts on chunk borders.
- [ ] Code compiles cleanly and passes all pre-commit Iron Gate checks.

