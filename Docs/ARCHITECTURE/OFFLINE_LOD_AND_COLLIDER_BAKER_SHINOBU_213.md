# OFFLINE_LOD_AND_COLLIDER_BAKER_SHINOBU_213



Owner: SHINOBU_213



Domain: offline LOD and collider baking



Runtime authority: none



## Compile Boundary



- Runtime DTO assembly: `Assets/_Project/Scripts/World/OfflineGeometry/Hecton8.World.OfflineGeometry.asmdef`



- Editor baker assembly: `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/Hecton8.World.OfflineGeometry.Editor.asmdef`



- Editor references: `Hecton8.World.OfflineGeometry`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`



- Sibling runtime references: none



- Historical/local Roslyn probe text: `Temp/SHINOBU_213_CompileProbe/` was recorded as containing local probe outputs for the runtime DTO and editor baker assemblies from the pre-endian pass.

- Treat that as compiler evidence only when an artifact path, command/tool, timestamp, environment, and output are linked.

Post-endian pending proof:

- Endian/hull: explicit-endian fallback, bounded-hull support indices, hull asset-binding fail-close, hull fallback scratch bounds.
- Decimator: finite inverse-square-root guards, raw stream bounds, output-lane bounds, index-stream fail-close.
- MeshData: read-write hull vertex annotation, layout-lane guards, safe transform values, safe transform basis, generated submesh range spans.
- Fallbacks: primitive-fit finite denominators, minimum-8 support hull, hull-counter clear, hull face-fan overflow fail-close.
- Assets: mock benchmark asset reload guard, mock asset bind fail-close, prefab-save fail-closed telemetry, mesh asset-folder fail-close, main LOD asset-path reload.
- CSV/artifacts: project-root suffix guard, atomic replacement, size/header schema guard, full-length read validation, strict row validation.
- Memory/ownership: transient mesh transfer guard, explicit renderer-array bridge, NativeMemorySentinel cold fail-fast bridge, caller-owned LOD mesh cleanup.
- Telemetry: binary-ledger update, hot geometry DTO explicit-layout proof, blackbox non-finite warning bit, per-lane fault encoding, failed-attempt blackbox coverage.
- Reporting: self-audit evidence-class correction, JSON control-character escaping, FixedString hashing without managed `ToString`, report/manifest metric hashing from FixedString bytes.
- ProfilerMarker instrumentation for editor job fences still needs a guarded probe when CPU drops below the build gate.
- Still pending: Unity import, editor menu execution, profiler capture, generated-asset inspection.



## Boundary



The baker is editor-only. It reads source meshes/prefabs and emits immutable `.mesh` and prefab assets under `Assets/_Project/BakedGeometry/Optimized/`.



Owned source folders and scripts ship with checked-in `.meta` files. Unity must not generate GUIDs for this domain during import.



- Generated mesh/collider assets are only saved under valid `Assets/` project folders.

- If the target folder cannot be proven or created by `AssetDatabase`, transient meshes are destroyed and the bake path returns failure instead of publishing a dangling editor object.

- Main LOD prefab assembly requires non-empty LOD0/LOD1/LOD2 saved asset paths before `AssetDatabase.LoadAssetAtPath`.
- Failed path or failed reload sets a warning bit.
- Exit route: black-box failed-attempt path.

- Transient Unity `Mesh` cleanup:
  - Scope: main LOD upload and hull upload.
  - Exception path: destroy unless ownership transferred to caller or `AssetDatabase`.
  - Caller-owned LOD0/LOD1/LOD2: tracked across the multi-mesh bake window.
  - `finally`: destroys tracked meshes if exception occurs before `SaveOrReplaceMesh` transfers or destroys them.

- CSV tuning resolves project root from `Application.dataPath` only after `/Assets` suffix validation; otherwise editor working directory/default settings are used if file is absent.

- CSV fail-closed cases:
  - File above 1 MiB.
  - Stream read returns short.
  - Header differs from `profile_name,lod1_ratio,lod2_ratio,primitive_tolerance,convex_hull_vertex_limit,lod0_hard_budget,global_quality_weight,depth_meters`.
  - Row cells are malformed or missing.
  - Fallback: deterministic default profile.



- The batch report also emits `Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod`: a flat little-endian binary manifest with a 64-byte header and 128-byte records for BRG/LOD consumers.
- This file is immutable editor output, not runtime Vault state.
- The writer serializes each 4-byte lane explicitly; it does not dump host-endian structs.
- The manifest is written to a same-volume `.tmp`, flushed, byte-count validated as `64 + recordCount * 128`, then replaced with `.bak` preservation.
- JSON/XML reports and the black-box dump use the same temp/replace policy; black-box dump replacement also validates the exact 19,200-byte ring size.
- JSON report escaping encodes quotes, backslashes, common control characters, and any character below `0x20` as JSON-safe escapes before report publication.


Generated prefabs may contain:



- `LODGroup`



- `MeshFilter`



- `MeshRenderer`



- `BoxCollider`



- `SphereCollider`



- convex `MeshCollider`



Generated prefab limits:

- No active runtime decimation.
- No hull generation.
- No LOD switching scripts.
- Offline bake math consumes continuous `GlobalQualityWeight` and depth.
- Resolved outputs: LOD thresholds, LOD fade widths, LOD1/LOD2 triangle ratios, primitive collider tolerance.
- Runtime quality may later adjust existing `LODGroup` thresholds using the same continuous signal.



## Collision Policy



Physics lies before it simulates:



- Sphere fit first where average radial error is within tolerance.



- Box fit second where surface error is within tolerance.



- Convex fallback is a bounded 8..32 point conservative support hull with plane-deduped fan-triangulated indices.
- The UI hull limit is honored from the explicit 8-vertex minimum up to the fixed 32-vertex cap; primitive sphere/box still wins before any hull is authored.
- Every finite source vertex must classify inside every emitted hull plane.
- Fail closed to `BoxCollider` with warning flags for:
  - all-nonfinite sources, under-enclosing hulls, support sets below 8 unique vertices;
  - face-fan index overflow, invalid/underpopulated hull output, failed asset binding.
- Invalid hull paths clear counters instead of synthesizing a convex mesh box; counters are never forced upward into uninitialized geometry.



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



No `[StructLayout(Pack=1)]` is used.

`OfflinePrimitiveFitResult` uses 3 explicit pad bytes after `ColliderType`, making a 40-byte exact 8-byte multiple. `OfflineGeometryBakeTelemetryEntry` is the 64-byte black-box ring row.



LOD0 is capped by `Lod0HardBudget`. LOD1 and LOD2 derive hard caps from `Lod0HardBudget * resolvedRatio`, with LOD2 clamped below LOD1, so source meshes above budget cannot leak oversized lower-detail meshes.



- LOD1/LOD2 source-triangle selection uses bounded partition-local saliency.
- Each output triangle maps to a deterministic non-overlapping source partition.
- `GlobalQualityWeight` and depth resolve sampled candidate count `1..7`; low quality uses cheapest single candidate, high quality preserves stronger area-normalized candidates under same cap.
- Imported index bases are clamped before raw pointer vertex reads.
- Position streams must satisfy `offset + 12 <= stride`.
- Optional normal streams must satisfy `offset + 12 <= stride`.
- Optional UV0 streams must satisfy `offset + 8 <= stride`.
- Invalid inputs collapse to deterministic zero/up-normal triangles, no generated rows, or safe defaults.
- Covered faults: bad index streams, empty range tables, empty vertices, invalid position streams, optional normal/UV faults.
- More faults: invalid output lanes, bad mock segment counts, default/mismatched pack/index buffers.
- No unsafe memory access.


Submesh ranges are generated for the full source `subMeshCount`; the baker must not truncate material ranges to an arbitrary cap.

If target triangle count is lower than source submesh count, hard budget wins. Some ranges receive zero output triangles; zero-output ranges are not serialized as Unity submeshes.



`LodConfigurationDTO` is an explicit 16-byte runtime-safe DTO:



- `float Lod1Threshold`



- `float Lod2Threshold`



- `uint Lod1MeshHash`



- `uint Lod2MeshHash`



- `OfflineLodManifestHeader` is 64 bytes and `OfflineLodManifestRecord` is 128 bytes.
- Both are explicit layouts with 4-byte aligned fields and explicit uint reserve lanes; the manifest header includes an endian tag so importers can reject or byte-swap future non-little-endian payloads.
- Float lanes are serialized through `math.asuint`; byte reversal is local because this checkout's Unity.Mathematics package does not expose `math.reversebytes`.



## Black Box



The editor baker records the last 300 bake outcomes into a fixed 64-byte row ring:



- row type: `OfflineGeometryBakeTelemetryEntry`



- capacity: 300 rows



- dump path: `Docs/AgentLogs/Dump_SHINOBU_213.bin`



- The ring is allocated with `UninitializedMemory` and then written with deterministic sentinel rows.

- Persistent ring registers/unregisters with `NativeMemorySentinel` through a cold mandatory reflection bridge when Core sentinel assembly is loaded.
- Registration failure disposes the ring and throws instead of leaving untracked persistent allocation.
- No direct `Hecton8.Core` asmdef reference is added to the offline baker.

- `Dump_SHINOBU_213.bin` writes oldest-to-newest 64-byte rows with explicit little-endian field serialization, not raw host-endian `NativeArray` memory.

- Non-finite metric input sets warning bit `0x80000000` plus per-lane extraction/serialization/LOD/quality/depth bits; raw fault bits fold into `StateHash` before sanitized serialization.

- FixedString source/output paths are hashed byte-by-byte without managed `ToString()` allocation in black-box and manifest hash paths, and JSON metric path fields append escaped ASCII FixedString bytes directly.

- Failed source or mid-bake attempts are recorded into the ring with failure warning bits but are not added to the generated-output manifest/report success list.

- The ring is editor-owned, disposed on assembly reload and editor quit, and is not runtime gameplay state.

- Same-frame editor job fences are instrumented by `ProfilerMarker` names `SHINOBU_213.*JobFence`; profiler evidence is still pending until the Unity editor run is allowed.



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
