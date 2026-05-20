# OFFLINE_LOD_AND_COLLIDER_BAKER_SHINOBU_213

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact.
Current DOC_GLOBAL boundary (2026-05-20 R46): `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` is the latest local static root/architecture interior-authority, route-field, and proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: SHINOBU_213
Domain: offline LOD and collider baking
Runtime authority: none

## Compile Boundary

- Runtime DTO assembly: `Assets/_Project/Scripts/World/OfflineGeometry/Hecton8.World.OfflineGeometry.asmdef`
- Editor baker assembly: `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/Hecton8.World.OfflineGeometry.Editor.asmdef`
- Editor references: `Hecton8.World.OfflineGeometry`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`
- Sibling runtime references: none

Historical/local Roslyn probe text: `Temp/SHINOBU_213_CompileProbe/` was recorded as containing local probe outputs for the runtime DTO and editor baker assemblies from the pre-endian pass. Treat that as compiler proof only when an artifact path, command/tool, timestamp, environment, and output are linked. The current explicit-endian fallback, bounded-hull support-index source, fail-closed hull asset-binding guard, read-write hull vertex safety annotation, finite inverse-square-root guards, decimator index-stream fail-closed guards, and mock benchmark asset reload guard still require a post-endian bounded-hull safety-index probe when CPU drops below the build gate; Unity import, editor menu execution, profiler, and generated-asset inspection remain pending evidence.

## Boundary

The baker is editor-only. It reads source meshes/prefabs and emits immutable `.mesh` and prefab assets under `Assets/_Project/BakedGeometry/Optimized/`.

Owned source folders and scripts ship with checked-in `.meta` files. Unity must not generate GUIDs for this domain during import.

The batch report also emits `Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod`: a flat little-endian binary manifest with a 64-byte header and 128-byte records for BRG/LOD consumers. This file is immutable editor output, not runtime Vault state. The writer serializes each 4-byte lane explicitly; it does not dump host-endian structs.

Generated prefabs may contain:
- `LODGroup`
- `MeshFilter`
- `MeshRenderer`
- `BoxCollider`
- `SphereCollider`
- convex `MeshCollider`

Generated prefabs must not contain active runtime decimation, hull generation, or LOD switching scripts. Offline bake math consumes continuous `GlobalQualityWeight` and depth to resolve LOD thresholds, LOD1/LOD2 triangle ratios, and primitive collider tolerance. Runtime quality systems may later adjust existing `LODGroup` thresholds using the same continuous quality signal.

## Collision Policy

Physics lies before it simulates:
- Sphere fit first where average radial error is within tolerance.
- Box fit second where surface error is within tolerance.
- Convex fallback is a bounded 8..32 point conservative support hull with plane-deduped fan-triangulated indices. The UI hull limit is honored up to the fixed cap; primitive sphere/box still wins before any hull is authored. Invalid/underpopulated hull output or failed asset binding fails closed to `BoxCollider` with warning flags; counters are never forced upward into uninitialized geometry.

Concave high-poly `MeshCollider` output is forbidden. Static enforcement lives in `Unoptimized_Mesh_Scanner` and reports to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.

## Mesh Policy

LOD meshes use an explicit 32-byte interleaved vertex layout:
- position `float3`
- normal `float3`
- uv0 `float2`

Hot/job geometry DTOs are explicit layouts and are validated during editor entry:
- `OfflineGeometryRawVertex`: 32 bytes, offsets `Position=0`, `Normal=12`, `Uv0=24`.
- `OfflineGeometryVertex32`: 32 bytes, offsets `Position=0`, `Normal=12`, `Uv0=24`.
- `OfflineSubMeshRange`: 16 bytes, offsets `SourceIndexStart=0`, `SourceTriangleCount=4`, `TargetTriangleStart=8`, `TargetTriangleCount=12`.
- `OfflinePrimitiveFitResult`: 40 bytes, offsets `Center=0`, `Size=12`, `Radius=24`, `Error=28`, `VertexCount=32`, `ColliderType=36`, `_pad0=37`, `_pad1=38`.

No `[StructLayout(Pack=1)]` is used. `OfflinePrimitiveFitResult` uses 3 explicit pad bytes after `ColliderType`, making the row 40 bytes, an exact 8-byte multiple. `OfflineGeometryBakeTelemetryEntry` remains the only 64-byte false-sharing row because it is the fixed black-box ring element.

LOD0 is capped by `Lod0HardBudget`. LOD1 and LOD2 derive hard caps from `Lod0HardBudget * resolvedRatio`, with LOD2 clamped below LOD1, so source meshes above budget cannot leak oversized lower-detail meshes.

LOD1/LOD2 source-triangle selection uses bounded partition-local saliency. Each output triangle maps to a deterministic non-overlapping source partition. `GlobalQualityWeight` and depth resolve the sampled candidate count from 1 to 7; low quality pays the cheapest single candidate while high quality preserves stronger area-normalized candidates under the same hard cap. Imported index bases are clamped before raw pointer vertex reads; invalid index streams, empty range tables, empty source vertices, or null position streams collapse to deterministic zero/up-normal triangles instead of unsafe memory access.

Submesh ranges are generated for the full source `subMeshCount`. The baker must not truncate material ranges to an arbitrary cap. If target triangle count is lower than source submesh count, hard budget wins and some submesh ranges receive zero output triangles; zero-output ranges are not serialized as Unity submeshes.

`LodConfigurationDTO` is an explicit 16-byte runtime-safe DTO:
- `float Lod1Threshold`
- `float Lod2Threshold`
- `uint Lod1MeshHash`
- `uint Lod2MeshHash`

`OfflineLodManifestHeader` is 64 bytes and `OfflineLodManifestRecord` is 128 bytes. Both are explicit layouts with 4-byte aligned fields and explicit uint reserve lanes; the manifest header includes an endian tag so importers can reject or byte-swap future non-little-endian payloads. Float lanes are serialized through `math.asuint`; byte reversal is local because this checkout's Unity.Mathematics package does not expose `math.reversebytes`.

## Black Box

The editor baker records the last 300 bake outcomes into a fixed 64-byte row ring:
- row type: `OfflineGeometryBakeTelemetryEntry`
- capacity: 300 rows
- dump path: `Docs/AgentLogs/Dump_SHINOBU_213.bin`

The ring is allocated with `UninitializedMemory` and then written with deterministic sentinel rows. `Dump_SHINOBU_213.bin` writes oldest-to-newest 64-byte rows with explicit little-endian field serialization, not raw host-endian `NativeArray` memory. The ring is editor-owned, disposed on assembly reload and editor quit, and is not runtime gameplay state.

## Self Audit

`OfflineGeometrySelfAudit` writes `Docs/Reports/SHINOBU_213_SELF_AUDIT.xml` with:
- 20-task reconciliation
- DTO field offsets and sizes
- continuous scalability policy
- H-Phi/Vault status
- job dependency graph
- compile guard
- Dear Lie complexity reduction

## Proof Files

- `Docs/Tasks/Status_SHINOBU_213.md`
- `Docs/AgentLogs/Rationale_SHINOBU_213.md`
- `Docs/AgentLogs/LOG_SHINOBU_213.md`
- `Docs/Reports/LOD_OPTIMIZATION_REPORT.json`
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`
- `Docs/Reports/SHINOBU_213_SELF_AUDIT.xml`


