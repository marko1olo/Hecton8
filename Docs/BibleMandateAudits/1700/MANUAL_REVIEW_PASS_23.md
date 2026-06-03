# Manual Review Pass 23 - World / Terrain / Voxels / Ecosystem Line-Level Classification

Status: STATIC SOURCE REVIEW - NO UNITY/PROFILER PROOF  
Date: 2026-06-02  
Owner: Agent 1700

## Scope

This pass closes the line-level static review queue for world, terrain, voxels, geology, ecosystem, celestial, atmosphere, and adjacent world-presentation systems.

Evidence used:

- `06_world_terrain_voxels_ecosystem/RUNTIME_TRIAGE.md`
- `06_world_terrain_voxels_ecosystem/RUNTIME_PRECLASSIFICATION.md`
- `_scans/06_world_terrain_voxels_ecosystem_runtime_risks.txt`
- Current source reads for ecosystem installer, world shell, atmosphere/ocean/seismic editor windows, PCIe self-test, procedural wreck tooling, fauna collider validation, and previously reviewed vegetation/radar/scatter/SDF/voxel paths.

This is not Unity import proof, Play Mode proof, player-build proof, profiler proof, GC proof, Memory Profiler proof, Frame Debugger proof, native-memory proof, voxel seam proof, GPU proof, or device proof.

## Result

All 254 world/terrain/voxel/ecosystem runtime suspect lines are now classified in `06_world_terrain_voxels_ecosystem/LINE_LEVEL_CLASSIFICATION.md`.

Classification totals:

- `LEGAL_EDITOR_OR_DEV_GUARDED`: 120
- `LEGAL_COLD_PATH`: 123
- `FALSE_POSITIVE`: 4
- `RUNTIME_VIOLATION`: 7 registered

## Registered Runtime Violations

- `ProceduralWreckGenerator.cs:5660`, `:5866`, `:5988`: runtime `new Mesh()` visual/proxy build routes remain bound to `RB-001`.
- `HectonWorldShellController1428.cs:25`, `:26`, `:33` and `HectonWorldShellVisualDriver1428.cs:28`: prototype `Camera.main`, `Update()`, and `LateUpdate()` shell routes remain bound to `RB-015`.

## Corrected Stale Evidence

- `MarauderOutpostGenerationService.cs`: current source no longer contains old runtime cube/material synthesis evidence. It still needs authored shell mesh/material proof under `RB-003`.
- `SargassumGlobalDragManager.cs`: current source no longer contains old runtime mesh/material/recalculate evidence. It still needs authored ecology package/trail/prototype proof under `RB-013`.
- `HectonIndirectVegetationRenderer.cs`: runtime generation flags are false and procedural mesh builders are editor-only. It still needs authored near/impostor mesh proof under `RB-002`.

## Still Missing

- Published SDF/DataMonolith proof for sonar, radar, construction, and world sampling.
- Vegetation path/flow/threat/thermal/chunk/radar native-memory stress proof showing zero post-bootstrap growth.
- Vegetation upload-byte, dirty-page, and GraphicsBuffer recreate counters.
- GPU scatter/microfauna readback cadence and no normal-frame blocking wait proof.
- Voxel seam, voxel dynamic obstacle, and voxel fade material pool proof.
- Authored Sargassum, brine, resource proxy, impostor, outpost, wreck, and vegetation generated packages.
- Ecosystem authored scene root proof so dynamic installer repair is not normal release composition.
- World-shell exclusion or rewrite proof.
- Compact/high player-build profiler and device captures.

## Decision

The world group is now line-level statically classified, but it remains release-yellow. The next useful work is not another grep pass; it is fixing/proving `RB-001`, `RB-015`, and the world proof gates for native memory, authored packages, SDF/DataMonolith, GPU readbacks, voxel seams, and scene composition.
