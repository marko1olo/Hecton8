# Habitat Interior Clutter Forge

Date: 2026-05-20
Status: PENDING VERIFICATION
Owner: SHINOBU_211

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R46): `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` is the latest local static root/architecture interior-authority, route-field, and proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
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
- Generated mesh vertex layout is 32 bytes: position float3 offset 0, normal float3 offset 12, uv0 float2 offset 24.
- Source mesh extraction uses `Mesh.AcquireReadOnlyMeshData` byte streams and Burst extraction jobs; managed `Mesh.GetVertices(List<T>)` staging is not part of the extraction path.
- Source mesh extraction validates attribute byte windows against declared vertex strides and bounds-checks source indices before unsafe pointer reads; malformed optional normal/tangent/UV streams fall back to deterministic defaults.
- Editor scan/collection paths reuse list-fill Unity APIs (`GetComponentsInChildren<T>(..., List<T>)`, `Renderer.GetSharedMaterials(List<Material>)`) instead of per-renderer arrays for material and component traversal.
- Interactive exclusion filters parse tag hashes and layer indices into fixed unmanaged lists; the filter no longer stores `string[]` or calls `string.Split` for tag/layer matching.
- Interactive exclusion component scratch lists are caller-owned per scan/bake/preview transaction; the filter does not own a static global component buffer.
- Folder bakes own one reusable `InteriorClutterBakeScratch` for render segments, preserved interactive roots, materials, mesh filters, shared materials, and component probes. The scratch is cleared per prefab and after prefab-content unload so destroyed editor references do not survive the transaction.
- Atlas UV remap preserves `_BaseMap`/`_MainTex` texture scale and offset in the 160-byte `InteriorClutterSegment` DTO before wrapping into the packed rect.
- Atlas packing reserves a 16px overflow fallback tile. Materials that cannot fit the atlas are flagged and remapped to that tile; they are not allowed to source-copy over the full atlas.
- Atlas base texel staging uses `NativeArray<uint>` and Burst fill jobs before `Texture2D.SetPixelData`; per-texel `SetPixel`/managed `Color32[]` fill is not part of the path.
- Atlas source-texture resolution uses fixed two/three-property overloads instead of `params string[]`, avoiding per-material/channel array allocation during batch atlas construction.
- Source textures that differ from the packed tile size use an editor GPU blit into a temporary RT followed by `Graphics.CopyTexture`, flagged as `AtlasScaledTexture` in reports.
- Any atlas receiving GPU copies is committed through one editor GPU-to-CPU `ReadPixels` sync before `AssetDatabase.CreateAsset`; this prevents generated `.asset` textures from serializing fallback CPU texels while the copied source data exists only on the GPU. Albedo atlas is created in sRGB space; normal and ARM atlases are created in linear space.
- Generated mesh, material, and texture replacements are marked dirty after `EditorUtility.CopySerialized`; generated prefab writes use the `SaveAsPrefabAsset(..., out bool success)` result and the bake flushes `AssetDatabase.SaveAssets()` once after successful prefab or mock mesh publication.
- Black-box telemetry is a per-bake `NativeArray<InteriorClutterTelemetryEntry>[300]` TempJob session. The ring is explicitly reset after uninitialized allocation, uses deterministic local frame indices instead of Unity frame time, records failing prefab hashes before exception dumps, writes chronological retained entries after wrap, and emits `Docs/AgentLogs/Dump_SHINOBU_211.bin` plus a reason sidecar on bake exceptions.
- LOD1 and LOD2 decimation jobs consume LOD0 independently and are joined with `JobHandle.CombineDependencies`; there is no artificial LOD2-after-LOD1 dependency.

Scalability:
- Low: `MX350_Interior_4K`, 512 tile cap, aggressive LOD2 triangle reduction.
- Middle: 4K atlas with larger tiles and higher LOD retention.
- High: 8K atlas profile, higher LOD retention.
- Ultra: 8K atlas with 2048 tile cap and full visual-overkill retention.
- `GlobalQualityWeight` is continuous CSV input, not a low/high switch; it drives LOD retention and small-detail collapse.

Proof boundary:
- Static source and docs only until Unity import, Console, Frame Debugger, profiler, and generated prefab readback are captured.
- Self-audit XML output path: `Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml`.


