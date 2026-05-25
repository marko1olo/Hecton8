# OFFLINE_HADAL_ARCH_BAKER_SHINOBU_215

Date: 2026-05-20

Domain: Echelon 2 World Generation / Offline Hadal Arch Baker

## Boundary

`Assets/_Project/Scripts/World/OfflineHadalArchBaker` is an Editor-only bake pipeline for static Hadal geology.

It must not install runtime SDF, CSG, voxel carving, or terrain deformation components into generated prefabs.

Runtime receives immutable `.asset` meshes, a static prefab, an `LODGroup`, and a non-convex static `MeshCollider`.

## Pipeline

1. `SdfShapeDTO` graph defines sphere, box, torus, and cylinder primitives with add, subtract, intersect, or smooth union operations.

2. `EvaluateSdfBooleanGraphJob` or `GenerateMockSdfVolumeJob` writes a signed distance volume into an uninitialized `NativeArray<float>`.

3. `ApplySdfNoiseDisplacementJob` uses AUP-local coordinates and a precomputed `NoiseSeedJitter` generated once from the FNV AUP hash during config sanitization.

4. `SealSdfBoundaryShellJob` forces positive density on all six volume faces so the extracted monolith cannot be an open grid-edge slice.

5. `BakeCavityOcclusionJob` writes cavity visibility into a voxel byte buffer.

6. `ExtractArchMeshJob` emits only the unified zero-crossing shell into `HadalArchVertexDTO` rows and rejects degenerate triangles.

7. `WeldArchMeshJob` deduplicates shared shell vertices in native memory before LOD or serialization.

8. `DeterministicLodDecimationJob` creates seed-stable LOD1/LOD2.

9. `HadalArchBakePipeline` exposes sync `Bake` and Forge-facing `BakeAsync`.
10. `BakeAsync` polls `JobHandle.IsCompleted` across SDF, cavity, extraction, weld, and LOD phases before serializing LOD meshes and optional prefab.

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

Generated Hadal arches are immutable environmental data and excluded from rollback state.

Mesh bytes and static transforms must not serialize into `StateRingBuffer`. During rollback resimulation, dynamic entities rewind; baked geology remains unchanged.

## Verification State

Static source scans passed for this path.

Absent in bake hot path: properties, LINQ, managed `Vector3[]`, `MemClear`, ClearMemory.

Unity compile and profiler timings remain pending because CPU measured `100%`; project rules forbid `dotnet build` above 50%.
