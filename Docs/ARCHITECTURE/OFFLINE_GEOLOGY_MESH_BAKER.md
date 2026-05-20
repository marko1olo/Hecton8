# Offline Geology Mesh Baker

Date: 2026-05-20
Status: STATIC SOURCE POLISHED / PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- Docs/README.md
- Docs/DOC_GOVERNANCE.md
- Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: SHINOBU_208 / Echelon 2 World Generation

## Contract

Static geology mesh generation belongs in Editor-only tools under `Assets/_Project/Scripts/Editor/GeologyForge`.
Runtime gameplay must consume immutable baked mesh assets from `Assets/_Project/BakedGeometry/Geology`.

## Runtime Boundary

- Baked rocks are static environmental art. Collision/proxy/SDF ownership is outside this render-bake lane.
- Baked mesh vertex layout is 32 bytes: position Float32x3 at byte 0, normal Float32x3 at byte 12, vertex color UNorm8x4 at byte 24, UV0 UNorm16x2 at byte 28.
- Vertex `Color.r` stores baked ambient occlusion.
- Generated LOD0/1/2 assets are immutable and are not gameplay state.
- The BRG handoff artifact is `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom`: 64B header plus 128B records containing LOD mesh GUIDs, AUP seed, bounds, triangle counts, variation, flags, and 32B vertex stride proof.
- Editor raw working rows are fixed at 64 bytes to avoid parallel worker false sharing while keeping runtime meshes compact.
- Editor normal smoothing builds transient quantized weld buckets, accumulates angle-weighted neighboring face normals, aligns them with SDF gradients, and writes tangents before packing the 32B runtime stream.
- Editor bake black-box rows are fixed at 64 bytes and held in a 300-entry ring. Fault dump path is `Docs/AgentLogs/Dump_SHINOBU_208.bin`; this is editor diagnostics, not runtime state.
- Non-asset bake probes destroy transient LOD mesh objects after metrics are recorded; only saved assets retain mesh ownership.
- Generated prefabs are no longer emitted by this lane. Runtime consumers must use static mesh assets plus the binary manifest, not generated GameObjects or `LODGroup` wrappers.
- Generated meshes intentionally do not add `MeshCollider`.
- Netcode rollback, Merkle hashing, and `StateRingBuffer` must not hash baked mesh vertex or index buffers every frame.
- `GlobalQualityWeight` is a continuous `smoothstep` curve over bake math: SDF noise frequency, noise amplitude, fractional octave contribution, Voronoi/ridged contribution, AO ray budget, AO step count, AO range, UV scale, LOD budgets, and collapse size.
- Runtime LOD transition distances may also be shifted by continuous `GlobalQualityWeight`; the generator does not author binary quality forks.

## First-20-Minutes Route Impact

This removes static geology topology generation from the route budget and buys readable cave/seabed silhouette in the Copper Wire route without adding runtime Marching Cubes stalls.

## Verification

Current evidence is static source only. Unity import, bake execution, mesh inspector validation, Frame Debugger, GCMonitor, and player-route proof are pending.

Static black-box source is present: SDF, extraction, attribute, AO, and serialization stages write `GeologyBakeTelemetryEntry` rows. Non-finite stage timing and exceptions dump the ring to `Docs/AgentLogs/Dump_SHINOBU_208.bin`. No dump file is expected until a fault path is exercised.

Static normal-weld source is present: `BuildNormalBucketJob` writes transient `NativeParallelMultiHashMap<ulong,int>` buckets and `CalculateSmoothNormalsJob` consumes the buckets with `[NoAlias]` fields and raw `GeologyRawVertex*` mutation. Unity import/Burst Inspector proof remains pending.

Static manifest source is present: `GeologyMeshManifestHeader` validates at 64 bytes and `GeologyMeshManifestRecord` validates at 128 bytes. Unity import and runtime BRG consumption proof remain pending.
Raw geology binary payloads are explicitly little-endian. The writer fails fast on a non-little-endian host instead of emitting native-endian `.h8geom` or dump bytes.

Static quality-weight source is present: `GenerateMockFractalNoiseJob` takes `GlobalQualityWeight` directly, and both full bake and SceneView preview pass the same profile scalar. Unity bake timing proof remains pending.

On-demand layout audit source is present: `HECTON-8/Geology Forge/Run Layout Self Audit` validates generated mesh streams and `geology_mesh_manifest.h8geom`, then writes `Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json`. Current report is a placeholder until a Unity Editor bake/audit is executed.

`Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` currently uses scanner schema v2 and reports `findingCount=34`, `actionableFindingCount=28`, `simulationPhaseFindingCount=0`, `bootstrapPhaseFindingCount=0`, `proceduralMaterialCloneFindingCount=0`, and `runtimeMeshAllocationsEradicated=false`. These remaining runtime topology sites are outside the GeologyForge render-bake lane and require owner-specific migration before project-wide eradication can be claimed.
