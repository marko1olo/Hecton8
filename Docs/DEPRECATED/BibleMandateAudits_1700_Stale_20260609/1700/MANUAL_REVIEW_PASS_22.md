# Manual Review Pass 22 - Generated Assets Line-Level Classification

Status: STATIC SOURCE REVIEW - NO UNITY/PROFILER PROOF  
Date: 2026-06-02  
Owner: Agent 1700

## Scope

This pass closes the line-level static review queue for generated meshes, textures, materials, LOD, collision, and adjacent generated visual package systems.

Evidence used:

- `01_generated_assets/RUNTIME_TRIAGE.md`
- `01_generated_assets/RUNTIME_PRECLASSIFICATION.md`
- `_scans/01_generated_assets_runtime_risks.txt`
- Current source reads for wreck generation, vegetation rendering, outpost generation, Sargassum, world shell prototypes, fauna collider validation, BRG bootstrap, smoke/self-test routes, and editor generator/collider tools.

This is not Unity import proof, Play Mode proof, player-build proof, profiler proof, GC proof, Memory Profiler proof, Frame Debugger proof, material proof, collision proof, or device proof.

## Result

All 249 generated-asset runtime suspect lines are now classified in `01_generated_assets/LINE_LEVEL_CLASSIFICATION.md`.

Classification totals:

- `LEGAL_EDITOR_OR_DEV_GUARDED`: 118
- `LEGAL_COLD_PATH`: 120
- `FALSE_POSITIVE`: 4
- `RUNTIME_VIOLATION`: 7 registered

## Registered Runtime Violations

- `ProceduralWreckGenerator.cs:5660`, `:5866`, `:5988`: runtime `new Mesh()` visual/proxy build routes remain bound to `RB-001`.
- `HectonWorldShellController1428.cs:25`, `:26`, `:33` and `HectonWorldShellVisualDriver1428.cs:28`: prototype `Camera.main`, `Update()`, and `LateUpdate()` shell routes remain bound to `RB-015`.

## Corrected Stale Evidence

- `MarauderOutpostGenerationService.cs`: current source no longer contains the old `CreateCubeMesh`, `new Material`, or `mesh.RecalculateNormals` fallback evidence. Current code validates authored shell mesh/material resources and faults if missing. `RB-003` remains as authored resource proof, not as an active runtime cube synthesis finding.
- `SargassumGlobalDragManager.cs`: current source no longer contains the old runtime `new Mesh`, `new Material`, or `RecalculateNormals` fallback evidence. `RB-013` remains as authored ecology package/trail/prototype proof.
- `HectonIndirectVegetationRenderer.cs`: current source has runtime generation flags false and editor-only procedural near/impostor mesh builders. `RB-002` remains as authored near/impostor mesh and organic vertex-channel proof.

## Still Missing

- Authored wreck visual/proxy package proof and release exclusion of runtime merge/proxy generation.
- Authored vegetation near/impostor meshes with UV/material/LOD/collider/vertex-channel proof.
- Authored outpost shell mesh/material proof.
- Baked impostor atlas/material/mesh registry proof.
- Authored resource proxy prefabs/materials and pool proof.
- Encoded SDF/DataMonolith proof for construction/foundation/drone routes.
- Authored drone materials and production task/data providers.
- Authored Sargassum archetypes/materials/nesting/trail/pool proof.
- Brine pool editor/offline bake route or explicit runtime terrain exception with profiler/device proof.
- Generated asset package manifests, flat-material captures, wireframe captures, UV/atlas reports, collision proxy reports, import settings, pre-save validation reports, black-box artifacts, and compact/high player-build profiler/device captures.

## Decision

The generated-assets group can now be treated as line-level statically classified, but it cannot be treated as release-clean. The remaining work is not another grep pass; it is targeted closure of `RB-001`, `RB-015`, and the authored package/proof gates listed above.
