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

## Follow-up — 2026-08-11T13:56:36Z

Conduct a comprehensive audit, consolidation, and verification of all documentation in the HECTON-8 project. The team must identify contradictions, verify code compliance, refactor obsolete files, and generate a unified knowledge graph.

Working directory: C:\hades\Hecton8
Integrity mode: development

## Requirements

### R1. Integrity Audit & Stale Data Removal
Identify and eliminate contradictions, outdated architectural assumptions (e.g., old world sizes, obsolete pipeline rules), and conflicting constraints across all documentation files in `Docs/`.

### R2. Mandate Verification
Compare the active documentation rules (the mandate spine) against the current Unity C# codebase in `Assets/_Project/Scripts/`. Identify where the codebase violates the documentation, or where the documentation is disconnected from reality.

### R3. Documentation Refactoring
Consolidate redundant files, move obsolete logs/task files to `Docs/Archive/`, and enforce the single source of truth routing dictated by `AGENTS.md` and `PROJECT_BIBLES.md`.

### R4. Knowledge Graph Generation
Produce a unified Markdown index/knowledge graph that maps all active systems, their governing bibles, and their authoritative file paths to serve as the new navigation hub.

## Acceptance Criteria

### Automated Mandate Integrity
- [ ] Running `python Tools/Docs/TestMandateRegistry.py --strict` exits with code 0 (PASS) with 0 errors and 0 warnings.
- [ ] Running `git diff --check` shows no trailing whitespace or unresolved merge conflicts in the documentation.

### Content Quality
- [ ] No two active `.md` files assert conflicting technical limits (e.g., one saying world is 12km, another saying 30km).
- [ ] All completed task logs from previous batches are moved out of the active `Docs/Tasks/` directory and into `Docs/Archive/`.

### Knowledge Graph Deliverable
- [ ] A new file `Docs/HECTON8_KNOWLEDGE_GRAPH.md` exists and contains links to every active project bible and routing file.
