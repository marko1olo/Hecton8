# OFFLINE_HADAL_ARCH_BAKER_SHINOBU_215 <!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Date: 2026-05-20
Domain: Echelon 2 World Generation / Offline Hadal Arch Baker

## Boundary

`Assets/_Project/Scripts/World/OfflineHadalArchBaker` is an Editor-only bake pipeline for static Hadal geology. It must not install runtime SDF, CSG, voxel carving, or terrain deformation components into generated prefabs. Runtime receives immutable `.asset` meshes, a static prefab, an `LODGroup`, and a non-convex static `MeshCollider`.

## Pipeline

1. `SdfShapeDTO` graph defines sphere, box, torus, and cylinder primitives with add, subtract, intersect, or smooth union operations.
2. `EvaluateSdfBooleanGraphJob` or `GenerateMockSdfVolumeJob` writes a signed distance volume into an uninitialized `NativeArray<float>`.
3. `ApplySdfNoiseDisplacementJob` uses AUP-local coordinates and a precomputed `NoiseSeedJitter` generated once from the FNV AUP hash during config sanitization.
4. `SealSdfBoundaryShellJob` forces positive density on all six volume faces so the extracted monolith cannot be an open grid-edge slice.
5. `BakeCavityOcclusionJob` writes cavity visibility into a voxel byte buffer.
6. `ExtractArchMeshJob` emits only the unified zero-crossing shell into `HadalArchVertexDTO` rows and rejects degenerate triangles.
7. `WeldArchMeshJob` deduplicates shared shell vertices in native memory before LOD or serialization.
8. `DeterministicLodDecimationJob` creates seed-stable LOD1/LOD2.
9. `HadalArchBakePipeline` exposes sync `Bake` and Forge-facing `BakeAsync`; `BakeAsync` polls `JobHandle.IsCompleted` across SDF, cavity, extraction, weld, and LOD phases before serializing LOD mesh assets and an optional static prefab.

## Vertex Layout

The mesh vertex stream is explicitly interleaved:

- Position: `Float32 x3`, offset 0
- Normal: `Float32 x3`, offset 12
- Tangent: `Float32 x4`, offset 24
- UV0: `Float32 x2`, offset 40
- Color: `UNorm8 x4`, offset 48; red stores baked cavity visibility
- UV3: `Float32 x3`, offset 52; AUP-local bake position

`HadalArchVertexDTO` is 64 bytes. `SdfShapeDTO` is 64 bytes. `HadalArchBakeConfigDTO` is 128 bytes with `NoiseSeedJitter` at offset 108 and final `ulong` padding at offset 120. `HadalArchBakeTelemetryEntry` is 64 bytes.

## Reports

The bake writes `Docs/Reports/HADAL_BAKE_REPORT.json` after Unity execution. The geometry debt scanner writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`. Runtime CSG inquisition writes `Docs/Reports/HADAL_RUNTIME_CSG_INQUISITION.json`. `HadalArchSelfAudit` writes `Docs/Reports/SHINOBU_215_SELF_AUDIT.xml`.

## Rollback Fence

Generated Hadal arches are immutable environmental data and are excluded from rollback state. Mesh bytes and static transforms must not be serialized into `StateRingBuffer`. If rollback resimulation occurs, dynamic entities rewind; baked geology remains unchanged.

## Verification State

Static source scans passed for this path: no properties, LINQ, managed `Vector3[]`, `MemClear`, or ClearMemory in the bake hot path. Unity compile and profiler timings remain pending because system CPU measured 100%, and project rules forbid `dotnet build` while CPU is above 50%.
