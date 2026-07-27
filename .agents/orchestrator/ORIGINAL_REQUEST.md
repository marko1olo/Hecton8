# Original User Request

## Initial Request — 2026-07-27T02:03:12Z

Fix the Voxel Surface Nets terrain & cave collision pipeline in Hecton8 (Unity URP 6000.5.0f1) so that physics colliders run on canonical max-quality meshes independent of visual LOD settings, with full job scheduling and compile integrity.

Working directory: C:\hades\Hecton8
Integrity mode: development

## Requirements

### R1. Fix Compilation Errors & Hallucinated APIs
Revert/fix invalid properties (such as non-existent ColliderMeshIdBase) in HectonVoxelEngine.cs. Ensure clean compilation with Unity's C# assembly definitions without breaking existing interfaces.

### R2. Complete Extraction & Physics Job Scheduling
Bridge the Volume-to-Chunk mapping inside HectonVoxelEngine.cs. Correctly schedule SurfaceNetExtractionJob (both visual and canonical collider passes) and VoxelSurfacePhysicsBakeRequestJob using VoxelSurfaceNetsVault pinned handles.

### R3. Dual-Mesh Pipeline Isolation (Visual LOD vs Canonical Collider)
Ensure quality and LOD settings affect solely the visual cave meshes while the canonical physical collider mesh is always generated at maximum precision (stride = 1, decimationBias = 0).

### R4. Dedicated Vault Memory & Safety Clamping
Use dedicated non-aliased BufferID entries in H8Memory.cs (VoxelSurfaceNetsColliderVertices, Indices, CellVertexMap) and enforce capacity checks so that buffer overflows do not corrupt active fluid or terrain memory.

## Acceptance Criteria

### Compilation & Static Health
- [ ] Code compiles without errors in Unity assembly pipeline.
- [ ] No hallucinated methods or properties in HectonVoxelEngine.cs.

### Pipeline Execution & Collision Parity
- [ ] SurfaceNetExtractionJob and VoxelSurfacePhysicsBakeRequestJob are actively scheduled and executed.
- [ ] Cave collision meshes generate correctly and remain invariant regardless of graphics quality / LOD toggles.
- [ ] Physical colliders successfully bake into PhysX (Physics.BakeMesh).

## Follow-up Request — 2026-07-27T02:22:20Z

Fix the voxel SDF sampling logic in HectonVoxelEngine.cs (Section 4.2 of Handoff) so that camera orientation and GlobalQualityWeight do not mutate underlying SDF terrain geometry truth prior to extraction, restoring determinism and preventing mesh/collider mismatch across quality levels.

Requirements:
R1. Remove Quality & Camera Bias from Core SDF Noise Evaluation (ensure ApplyVoxelCliffOverhangNoise or overhang/cave SDF noise functions evaluate using canonical position/world inputs independent of GlobalQualityWeight or camera view vectors prior to mesh extraction).
R2. Deterministic Volume Reconstruction (guarantee that re-extracting a voxel volume under different graphics quality settings or camera angles yields deterministic SDF values and identical collision topology).
R3. Capacity Overflow Protection (prevent scratch capacity overflows when building dense voxel chunks at lower quality settings so chunk silhouettes do not disappear).

