# Habitat Interior Clutter Forge

Date: 2026-05-20
Status: PENDING VERIFICATION
Owner: SHINOBU_211

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
Editor-only pipeline for collapsing non-interactive habitat/interior clutter into generated room meshes under `Assets/_Project/BakedGeometry/HabitatInteriors`.

Assembly boundary:
- Source lives in `Hecton8.HabitatInteriorClutterForge.Editor.asmdef`.
- References are limited to Unity Burst/Collections/Jobs/Mathematics plus built-in Unity Editor/Engine APIs.
- No runtime sibling assembly reference, runtime manager, runtime MonoBehaviour, or GlobalRegistry/EventBus route is introduced by this forge.

Runtime contract:
- Generated static clutter is immutable presentation/environment data.
- Rollback/Merkle state must synchronize module type hash and AUP placement only; generated vertex/index/atlas data stays excluded from gameplay state rings.
- Interactive objects are filtered before bake and copied as preserved children in generated prefabs.
- Preserved interactive roots are ancestor-compacted before prefab generation; a parent exclusion suppresses child clones and a later parent exclusion replaces already-recorded child roots.
- Consolidation reports separate the one generated static monolith draw call from preserved interactive renderer draw calls. Aggregate `drawCallsAfter` and scanner `estimatedDrawCallsAfterForge` include preserved interactive renderers instead of pretending the generated prefab costs exactly one draw call when gameplay roots remain.
- Scan, preview, and bake consider only renderers whose prefab `activeSelf` chain is enabled up to the source root and whose `MeshRenderer.enabled` flag is true. Inactive prefab variants and disabled renderers are ignored instead of being reported or baked as visible clutter truth.
- Generated mesh vertex layout is 32 bytes: position float3 offset 0, normal float3 offset 12, uv0 float2 offset 24.
- Generated mesh vertex-buffer setup routes through `InteriorClutterVertexLayoutValidator.ApplyVertexBufferParams`; descriptor records are written into a disposed Temp `NativeArray<VertexAttributeDescriptor>`, and mesh validation uses direct vertex-attribute accessors instead of array-returning mesh attribute reads.
- Source mesh extraction uses `Mesh.AcquireReadOnlyMeshData` byte streams and Burst extraction jobs; managed `Mesh.GetVertices(List<T>)` staging is not part of the extraction path.
- Every mathematical job in the SHINOBU_211 job file uses the exact directive `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Deterministic rollback float mode is not used because the forge publishes editor-only static presentation assets excluded from gameplay state rings.
- Static collection iterates every triangle submesh on a source mesh. If a renderer has fewer material slots than submeshes, the missing slots route through a null-material atlas fallback instead of dropping geometry.
- Transform and mock jobs write deterministic windows into pre-sized `NativeArray<InteriorClutterRawVertex>` buffers. Shared `NativeList` append is intentionally rejected because it would create contention and unstable write ordering in the parallel kernels.
- Source mesh extraction validates attribute byte windows against declared vertex strides, applies `SubMeshDescriptor.baseVertex` before source reads, guards index-buffer offsets and destination windows inside the Burst kernels, truncates malformed non-triangle-aligned index windows to a multiple of three with `UnsupportedMesh` reporting, and bounds-checks source indices before unsafe pointer reads; malformed optional normal/tangent/UV streams fall back to deterministic defaults.
- `TransformAndAppendVerticesJob` receives explicit source vertex and segment counts. Invalid segment-map entries emit deterministic fallback vertices instead of dereferencing uninitialized segment memory.
- Generated mesh bounds are derived only from finite vertex positions. If every baked position is invalid, the forge emits a one-meter fallback bounds instead of allowing NaN/Infinity to poison prefab culling state.
- Editor scan/collection paths reuse list-fill Unity APIs (`GetComponentsInChildren<T>(..., List<T>)`, `Renderer.GetSharedMaterials(List<Material>)`) instead of per-renderer arrays for material and component traversal.
- Interactive exclusion filters parse tag hashes and layer indices into fixed unmanaged lists; the filter no longer stores `string[]` or calls `string.Split` for tag/layer matching.
- Interactive exclusion component scratch lists are caller-owned per scan/bake/preview transaction; the filter does not own a static global component buffer.
- Folder bakes own one reusable `InteriorClutterBakeScratch` for render segments, preserved interactive roots, materials, mesh filters, shared materials, and component probes. The scratch is cleared per prefab and after prefab-content unload so destroyed editor references do not survive the transaction.
- Atlas UV remap preserves `_BaseMap`/`_MainTex` texture scale and offset in the 192-byte `InteriorClutterSegment` DTO before wrapping into the packed rect. The segment also stores a precomputed inverse-transpose normal basis as signed cofactor columns so non-uniform scale does not poison baked vertex normals. The active bake path fuses remap into `TransformAndAppendVerticesJob` to avoid a second vertex pass; the standalone `RemapUvCoordinatesJob` remains bounds/NaN guarded for the task-mandated UV utility path.
- Atlas packing reserves a 16px overflow fallback tile. Materials that cannot fit the atlas are flagged and remapped to that tile; they are not allowed to source-copy over the full atlas.
- Atlas base texel staging uses `NativeArray<uint>` and Burst fill jobs before `Texture2D.SetPixelData`; per-texel `SetPixel`/managed `Color32[]` fill is not part of the path.
- Atlas rect-color fill depends on the solid fallback fill through a named `JobHandle`, validates rect/color length windows inside the Burst kernel, clamps malformed rect extents with 64-bit arithmetic, and skips invalid spans so bad atlas metadata cannot overwrite unrelated texels.
- Albedo source texture copy uses a tint-aware top-mip temp tile path when `_BaseColor`/`_Color` is not white. `TintAtlasTileJob` multiplies only the copied atlas payload in unmanaged memory before the tile is copied into the atlas, and `AtlasTintFallback` is raised only when that tinted copy fails.
- Atlas source-texture resolution uses fixed two/three-property overloads instead of `params string[]`, avoiding per-material/channel array allocation during batch atlas construction.
- Source textures that differ from the packed tile size use an editor GPU blit into a temporary RT followed by `Graphics.CopyTexture`, flagged as `AtlasScaledTexture` in reports. Exact-size `Graphics.CopyTexture` failures retry through the same RT blit route before `AtlasCopyFailure`; successful retries raise `AtlasDirectCopyFallback`.
- Any atlas receiving GPU copies is committed through one editor GPU-to-CPU `ReadPixels` sync before `AssetDatabase.CreateAsset`; this prevents generated `.asset` textures from serializing fallback CPU texels while the copied source data exists only on the GPU. Albedo atlas is created in sRGB space; normal and mask atlases are created in linear space.
- The generated mesh layout remains 32 bytes and intentionally has no tangent attribute. Normal atlas textures may still be emitted as offline artifacts, but the generated material does not bind `_BumpMap`/`_NormalMap` or enable `_NORMALMAP`; tangent-space normal mapping is fenced until a tangent-bearing layout is explicitly authorized.
- The third atlas is a URP/Standard-compatible mask map, not an AO/roughness/metal ARM texture: R=metallic, G=occlusion, B=detail mask default 0, A=smoothness. Source texture copy into this atlas is limited to `_MaskMap`; Standard `_MetallicGlossMap` does not own the G/B mask lanes and therefore stays on scalar fallback packing until a channel-aware repack path exists. `_OcclusionMap` is not copied into this texture because its channel contract differs from `_MaskMap`.
- Generated atlas Texture2D assets are compressed before `AssetDatabase.CreateAsset`: normals prefer BC5, albedo/mask prefer BC7 with DXT5 fallback, and failures raise `AtlasCompressionFallback` instead of silently shipping RGBA32 unless the platform lacks a supported compressed format.
- Generated mesh, material, and texture replacements are marked dirty after `EditorUtility.CopySerialized`; generated prefab writes use the `SaveAsPrefabAsset(..., out bool success)` result and the bake flushes `AssetDatabase.SaveAssets()` once after successful prefab or mock mesh publication.
- Generated LOD renderer construction validates loaded mesh and atlas material assets before assigning renderer state, preventing null-asset prefab publication when editor asset lookup fails.
- Generated static monolith renderers keep GPU instancing material eligibility and are marked only `OccludeeStatic | ContributeGI`; `BatchingStatic` is deliberately not set so Unity static batching does not duplicate geometry or collide with the GPU Resident Drawer / instancing ownership path.
- Black-box telemetry is a per-bake `NativeArray<InteriorClutterTelemetryEntry>[300]` TempJob session. The ring is explicitly reset after uninitialized allocation, uses deterministic local frame indices instead of Unity frame time, records failing prefab hashes before exception dumps, writes only recorded entries before wrap, writes chronological retained entries after wrap, and emits `Docs/AgentLogs/Dump_SHINOBU_211.bin` plus a reason sidecar on bake exceptions.
- Static clutter translation is derived from prefab-local hierarchy TRS from the source root to the clutter transform. This avoids subtracting absolute float `Transform.position` values for contained source roots; true world AUP placement remains owned by the module/runtime authority and is not manufactured by the editor forge. The same prefab-local path positions preserved interactive clones under the generated prefab.
- LOD1 and LOD2 decimation jobs consume LOD0 independently, guard source/output triangle windows inside the Burst kernel, write deterministic fallback triangles for malformed windows or non-finite source positions, are scheduled with `transformHandle` as their dependency, and are joined with `JobHandle.CombineDependencies`; the forge completes once before mesh serialization.
- Generated `LODGroup` screen thresholds are derived from `GlobalQualityWeight` through `math.smoothstep` and `math.lerp`: low profiles lower LOD0/LOD1 residency to shed dense room geometry earlier, while high/ultra profiles keep richer LODs resident longer. This scales presentation residency only; it does not change gameplay ownership, DTO layout, or save identity.
- Editor-only completion barriers are named handles at asset serialization or MeshData disposal boundaries; inline `Schedule(...).Complete()` chains are not part of the current forge source.

Scalability:
- Low: `MX350_Interior_4K`, 512 tile cap, aggressive LOD2 triangle reduction.
- Middle: 4K atlas with larger tiles and higher LOD retention.
- High: 8K atlas profile, higher LOD retention.
- Ultra: 8K atlas with 2048 tile cap and full visual-overkill retention.
- `GlobalQualityWeight` is continuous CSV input, not a low/high switch; it drives LOD retention, small-detail collapse, and generated LODGroup residency thresholds.

Proof boundary:
- Static source and docs only until Unity import, Console, Frame Debugger, profiler, and generated prefab readback are captured.
- Self-audit XML output path: `Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml`.
