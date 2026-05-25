# Habitat Interior Clutter Forge

Date: 2026-05-20

Status: PENDING VERIFICATION

Owner: SHINOBU_211

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

- Consolidation reports separate one generated static monolith draw call from preserved interactive renderer draw calls.
- Aggregate `drawCallsAfter` and scanner `estimatedDrawCallsAfterForge` include preserved interactive renderers.
- They do not pretend the generated prefab costs exactly one draw call when gameplay roots remain.

- Scan, preview, and bake consider only renderers with enabled prefab `activeSelf` chain up to the source root.
- `MeshRenderer.enabled` must be true.
- Inactive prefab variants and disabled renderers are ignored, not reported or baked as visible clutter truth.

- Generated mesh vertex layout is 32 bytes: position float3 offset 0, normal float3 offset 12, uv0 float2 offset 24.

- Generated mesh vertex-buffer setup routes through `InteriorClutterVertexLayoutValidator.ApplyVertexBufferParams`; descriptor records are written into a disposed Temp `NativeArray<VertexAttributeDescriptor>`, and mesh validation uses direct vertex-attribute accessors instead of array-returning mesh attribute reads.

- Source mesh extraction uses `Mesh.AcquireReadOnlyMeshData` byte streams and Burst extraction jobs; managed `Mesh.GetVertices(List<T>)` staging is not part of the extraction path.

- Every mathematical job in the SHINOBU_211 job file uses `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Deterministic rollback float mode is not used.
- Reason: forge publishes editor-only static presentation assets excluded from gameplay state rings.

- Static collection iterates every triangle submesh on a source mesh.
- If renderer material slots are fewer than submeshes, missing slots route through null-material atlas fallback.
- Geometry is not dropped.

- Transform/mock jobs write deterministic windows into pre-sized `NativeArray<InteriorClutterRawVertex>` buffers. Shared `NativeList` append is rejected: contention and unstable parallel write order.

- Source mesh extraction:
  - validates attribute byte windows against declared vertex strides;
  - applies `SubMeshDescriptor.baseVertex` before source reads;
  - guards index-buffer offsets and destination windows inside Burst kernels;
  - truncates malformed non-triangle-aligned index windows to a multiple of three with `UnsupportedMesh` reporting;
  - bounds-checks source indices before unsafe pointer reads;
  - falls back to deterministic defaults for malformed optional normal/tangent/UV streams.

- `TransformAndAppendVerticesJob` receives explicit source vertex and segment counts. Invalid segment-map entries emit deterministic fallback vertices instead of dereferencing uninitialized segment memory.

- Generated mesh bounds derive only from finite vertex positions.
- If every baked position is invalid, forge emits one-meter fallback bounds.
- NaN/Infinity cannot poison prefab culling state.

- Editor scan/collection paths reuse list-fill Unity APIs (`GetComponentsInChildren<T>(..., List<T>)`, `Renderer.GetSharedMaterials(List<Material>)`) instead of per-renderer arrays for material and component traversal.

- Interactive exclusion filters parse tag hashes and layer indices into fixed unmanaged lists; the filter no longer stores `string[]` or calls `string.Split` for tag/layer matching.

- Interactive exclusion component scratch lists are caller-owned per scan/bake/preview transaction; the filter does not own a static global component buffer.

- Folder bakes own one reusable `InteriorClutterBakeScratch`.
- Scratch covers render segments, preserved interactive roots, materials, mesh filters, shared materials, and component probes.
- It is cleared per prefab and after prefab-content unload, so destroyed editor references do not survive.

- Atlas UV remap:
  - preserves `_BaseMap` / `_MainTex` scale and offset in the `192`-byte `InteriorClutterSegment`;
  - stores inverse-transpose normal basis as signed cofactor columns;
  - prevents non-uniform scale from poisoning baked vertex normals;
  - fuses remap into `TransformAndAppendVerticesJob`;
  - keeps standalone `RemapUvCoordinatesJob` bounds/NaN guarded for the UV utility path.

- Atlas packing reserves a 16px overflow fallback tile. Oversized materials are flagged and remapped there; they cannot source-copy over the full atlas.

- Atlas base texel staging uses `NativeArray<uint>` and Burst fill jobs before `Texture2D.SetPixelData`; per-texel `SetPixel`/managed `Color32[]` fill is not part of the path.

- Atlas rect-color fill depends on solid fallback fill through a named `JobHandle`.
- Burst kernel validates rect/color length windows.
- Malformed rect extents are clamped with 64-bit arithmetic.
- Invalid spans are skipped, so bad atlas metadata cannot overwrite unrelated texels.

- Albedo copy uses a tint-aware top-mip temp tile path when `_BaseColor`/`_Color` is not white.
- `TintAtlasTileJob` multiplies only the copied atlas payload in unmanaged memory before atlas copy.
- `AtlasTintFallback` is raised only when tinted copy fails.

- Atlas source-texture resolution uses fixed two/three-property overloads instead of `params string[]`, avoiding per-material/channel array allocation during batch atlas construction.

- Source textures with non-matching packed tile size use editor GPU blit into a temporary RT, then `Graphics.CopyTexture`.
- Reports flag this as `AtlasScaledTexture`.
- Exact-size `Graphics.CopyTexture` failures retry through the same RT blit route before `AtlasCopyFailure`.
- Successful retries raise `AtlasDirectCopyFallback`.

- GPU-copy atlas commit:
  - one editor GPU-to-CPU `ReadPixels` sync before `AssetDatabase.CreateAsset`;
  - prevents generated `.asset` textures from serializing fallback CPU texels while copied source data exists only on GPU;
  - albedo atlas: sRGB;
  - normal and mask atlases: linear.

- Generated mesh layout: 32 bytes; no tangent attribute.
- Normal atlas textures may still be emitted offline.
- Generated material does not bind `_BumpMap`/`_NormalMap` or enable `_NORMALMAP`.
- Tangent-space normal mapping remains fenced until a tangent-bearing layout is authorized.

- Third atlas contract:
  - URP/Standard mask map, not ARM texture;
  - R = metallic;
  - G = occlusion;
  - B = detail mask default `0`;
  - A = smoothness;
  - source texture copy limited to `_MaskMap`;
  - Standard `_MetallicGlossMap` stays on scalar fallback until channel-aware repack exists;
  - `_OcclusionMap` is not copied because its channel contract differs from `_MaskMap`.

- Generated atlas `Texture2D` assets are compressed before `AssetDatabase.CreateAsset`.
- Normals prefer BC5; albedo/mask prefer BC7 with DXT5 fallback.
- Failures raise `AtlasCompressionFallback` unless the platform lacks a supported compressed format.

- Generated mesh/material/texture replacements are marked dirty after `EditorUtility.CopySerialized`.
- Generated prefab writes use `SaveAsPrefabAsset(..., out bool success)`.
- Bake flushes `AssetDatabase.SaveAssets()` once after successful prefab or mock mesh publication.

- Generated LOD renderer construction validates loaded mesh and atlas material assets before assigning renderer state, preventing null-asset prefab publication when editor asset lookup fails.

- Generated static monolith renderers keep GPU instancing material eligibility.
- Static flags: `OccludeeStatic | ContributeGI`.
- `BatchingStatic` is not set.
- Reason: avoid geometry duplication and GPU Resident Drawer / instancing ownership conflict.

- Black-box telemetry:
  - per-bake `NativeArray<InteriorClutterTelemetryEntry>[300]` TempJob session;
  - ring reset after uninitialized allocation;
  - deterministic local frame indices, not Unity frame time;
  - failing prefab hashes recorded before exception dump;
  - writes only recorded entries before wrap;
  - writes chronological retained entries after wrap;
  - emits `Docs/AgentLogs/Dump_SHINOBU_211.bin` plus reason sidecar on bake exceptions.

- Static clutter translation:
  - derived from prefab-local hierarchy TRS from source root to clutter transform;
  - avoids subtracting absolute float `Transform.position` values for contained source roots;
  - leaves true world AUP placement to module/runtime authority;
  - positions preserved interactive clones under the generated prefab through the same prefab-local path.

- LOD1 and LOD2 decimation jobs consume LOD0 independently.
- Burst kernel guards source/output triangle windows and writes deterministic fallback triangles for malformed windows or non-finite positions.
- Jobs depend on `transformHandle`, join with `JobHandle.CombineDependencies`, and complete once before mesh serialization.

- Generated `LODGroup` screen thresholds derive from `GlobalQualityWeight` through `math.smoothstep` and `math.lerp`.
- Low profiles lower LOD0/LOD1 residency to shed dense room geometry earlier; high/ultra profiles keep richer LODs resident longer.
- Scope: presentation residency only; gameplay ownership, DTO layout, and save identity are unchanged.

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
