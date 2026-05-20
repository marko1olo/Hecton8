# Status_SHINOBU_211

Agent: SHINOBU_211
Role: OFFLINE_INTERIOR_CLUTTER_FORGE
Domain: Editor-only habitat/interior static-clutter consolidation.
Task count: 20
Batch source: Docs/Tasks/CURRENT_BATCH.md
Evidence state: PENDING VERIFICATION - domain static checks passed; solution build blocked by unrelated dependency errors.

## Hygiene

- [x] Batch prompt extracted from CURRENT_BATCH.md with PowerShell regex over raw file text. DOD: strict XML block isolation. Rejected: truncated MCP/resource read. Estimate: 150 us.
- [x] Existing status/rationale/log checked before start; files were missing, so no stale active-batch residue was detected. DOD: hygiene gate. Rejected: reading archived logs. Estimate: 80 us.
- [x] Relevant mandates identified before code: OPT_Zero_GC, OPT_Native_Memory_Collections, DATA_Runtime_Struct_Layout_ARM64, MATH_AUP_Determinism_Sync, REND_URP_Graphics_HotPath_Optimization_HLOD, TOOL_Designer_Facades_CSV_Binary_Bridge, STRM_Async_Asset_Upload_Texture_Settings, GLOBAL_AUTHORITY_BOUNDARIES. DOD: registry read before implementation. Rejected: coding from prompt only. Estimate: 220 us.

## Tasks

- [x] Task 01 REALTIME_HIERARCHY_BLOAT_INQUISITION | Implemented `Hierarchy_Bloat_Scanner` with Habitat root fallback to actual Construction/Final prefabs and JSON output. DOD practice: static prefab scan report. Alternative rejected: manual artist notes and raw prefab YAML edits. Estimate: 350 us per small prefab plus Unity asset load.
- [x] Task 02 MULTIPLE_MATERIAL_PURGE | Implemented material collection and one generated atlas material per baked room. DOD practice: one material assigned to LOD monolith renderers. Alternative rejected: submesh/material array retention. Estimate: 900 us per material excluding GPU copy.
- [x] Task 03 CS1612_GEOMETRY_STATE_ANNIHILATION | Implemented raw-field explicit DTOs and pointer-based transform job. DOD practice: no get/set properties in Burst geometry DTOs. Alternative rejected: managed vertex property records. Estimate: 0.04 us per vertex transform target pending profiler.
- [x] Task 04 ARM64_MAPPING_LAYOUT_ASSERTION | Implemented `InteriorClutterVertexLayoutValidator` for 32/64/160-byte DTO and mesh stride checks. DOD practice: `UnsafeUtility.SizeOf` and field offset audit. Alternative rejected: implicit Mesh defaults. Estimate: 15 us per validation call.
- [x] Task 05 EMERGENCY_MOCK_CLUTTER_BENCHMARK | Implemented `GenerateMockClutterCombineJob` for 500 box clutter shapes / 18,000 vertices. DOD practice: Burst mock combine asset path. Alternative rejected: waiting for art-prefab completion. Estimate: pending Burst/Editor measurement.
- [x] Task 06 BURST_MESH_TRANSFORMATION_KERNEL | Implemented `TransformAndAppendVerticesJob` over raw pointers and NativeArray segment windows. DOD practice: Burst IJobParallelFor transform/remap kernel. Alternative rejected: managed `Mesh.CombineMeshes`. Estimate: 0.04 us per vertex target pending profiler.
- [x] Task 07 AUTOMATED_TEXTURE_ATLASING_ALGORITHM | Implemented guillotine free-rect atlas packing and albedo/normal/ARM texture generation with `Graphics.CopyTexture` plus scaled RT blit fallback. DOD practice: one atlas material per room. Alternative rejected: material array/submeshes and cropped oversized source tiles. Estimate: 900 us per material excluding GPU copy.
- [x] Task 08 THE_DEAR_LIE_UV_REMAPPING | Implemented `RemapUvCoordinatesJob` and integrated atlas rect remap in transform job with `_BaseMap`/`_MainTex` scale/offset. DOD practice: UV scale/translate offline. Alternative rejected: runtime UV correction and ignoring material tiling. Estimate: 0.01 us per vertex target pending profiler.
- [x] Task 09 DETERMINISTIC_LOD_DECIMATION_ENGINE | Implemented deterministic triangle-soup LOD1/LOD2 generation with continuous quality-weight collapse. DOD practice: offline LOD variants. Alternative rejected: runtime prop culling. Estimate: pending mock/Unity bake.
- [x] Task 10 ASYNCHRONOUS_ASSET_SERIALIZATION | Implemented Mesh `SetVertexBufferParams`/`SetVertexBufferData` serialization and generated prefab/material/texture outputs under BakedGeometry. DOD practice: direct mesh buffer asset write. Alternative rejected: scene-time mesh mutation. Estimate: pending Unity editor measurement.
- [x] Task 11 INTERACTIVE_ELEMENT_PRESERVATION_FILTER | Implemented tag/layer/component-name exclusion and generated prefab `INTERACTIVE_PRESERVED` copy root. DOD practice: strict pre-bake filter. Alternative rejected: baking everything. Estimate: 80 us per component scan plus Unity load.
- [x] Task 12 AUP_DEPTH_LOCALIZATION_PREPARATION | Implemented root-relative local matrix bake and double3 room-relative offset telemetry in segment DTO. DOD practice: root-pivot local-space bake. Alternative rejected: absolute world float bake. Estimate: 0.02 us per segment.
- [x] Task 13 ROLLBACK_NETCODE_EXCLUSION_FENCE | Documented static generated geometry exclusion in architecture doc and JSON reports. DOD practice: immutable environmental data contract. Alternative rejected: adding static meshes to rollback hash. Estimate: 0 runtime us.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | Implemented `NativeArrayOptions.UninitializedMemory` on source, segment, LOD, packed, index, and telemetry buffers. DOD practice: fully overwritten TempJob buffers. Alternative rejected: `ClearMemory`/`MemClear`. Estimate: saves zero-fill of 32-64 bytes per vertex.
- [x] Task 15 TELEMETRY_CONSOLIDATION_REPORT_GENERATOR | Implemented `HABITAT_CONSOLIDATION_REPORT.json`, `RENDERING_OPTIMIZATION_REPORT.json`, and 300-entry black-box dump path. DOD practice: JSON and fixed-size telemetry. Alternative rejected: chat-only report. Estimate: report write is editor-only.
- [x] Task 16 PROCEDURAL_CLUTTER_FORGE_WINDOW | Implemented UI Toolkit `Interior Consolidation Forge` window with folder/profile/filter fields, scan, preview, and bake buttons. DOD practice: designer facade. Alternative rejected: menu-only tool. Estimate: editor UI only.
- [x] Task 17 CSV_ATLAS_PROFILES_INGESTOR | Implemented byte-level CSV parser and authored `texture_atlas_profiles.csv`. DOD practice: deterministic profile source. Alternative rejected: runtime parser/String.Split in gameplay. Estimate: cold editor parse only.
- [x] Task 18 LIVE_MERGE_PREVIEW_GIZMO | Implemented SceneView overlay drawing static green bounds and interactive red bounds. DOD practice: pre-bake visual verification. Alternative rejected: blind bake. Estimate: SceneView/editor only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | Implemented `Hierarchy_Bloat_Scanner` and `RENDERING_OPTIMIZATION_REPORT.json` writer. DOD practice: static bloat metric report. Alternative rejected: qualitative hierarchy claims. Estimate: 350 us per small prefab plus Unity load.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Implemented layout validator, architecture doc, static source scans, and `<SELF_AUDIT>` block in consolidation report. DOD practice: static audit. Alternative rejected: unverified completion claim. Estimate: layout validation 15 us call target.

## Loop State

Current loop: 19 / 5 minimum.
Last compile/check: Custom trailing-whitespace scan passed for untracked SHINOBU_211 files; forbidden-API `rg -P` scan returned no matches for managed Mesh.GetVertices extraction, per-pixel SetPixel/SetPixels/Color32[] atlas fill, persistent InteriorClutterBlackBox, Pack=1, hot properties, Mesh.CombineMeshes, Texture2D.PackTextures, direct material mutation, runtime update loops, GlobalRegistry/EventBus routes, MemClear, ToArray, ToCharArray, or managed byte staging. Loop 19 static scan returned `LOOP19_TOKEN_FAST_PATH_OK`; it removed unconditional `ToCharArray` from `SanitizeToken`, so safe prefab names now return the original string and invalid names allocate only the final sanitized string plus a cold `StringBuilder`. Loop 18 static scan returned `LOOP18_BAKE_SCRATCH_OK`; it introduced reusable `InteriorClutterBakeScratch` so `BakeFolder` no longer allocates static segment, interactive root, material, mesh-filter, shared-material, or component-probe lists per prefab; scratch clears per prefab and after prefab-content unload. Loop 17 scanner scratch scan returned `LOOP17_SCANNER_SCRATCH_OK`; it moved hierarchy scanner material uniqueness scratch outside the per-prefab loop and clears it per prefab, removing one managed `List<Material>` allocation per scanned prefab. Loop 16 added chronological black-box dump writes after ring wrap without managed staging arrays. Loop 15 black-box scan found no `Time.frameCount`, no stale UI "Complete" bake messages, and found `BakeException`, `RecordFailure`, `ResetRing`, deterministic local `FrameIndex`, and dump reason `entries=` evidence. Loop 14 texture/filter scan found no `params string[]`, no `value.Split`, no `SplitCsv`, and no filter-owned `string[]` tag/layer storage in SHINOBU_211 forge files; atlas source texture discovery now uses fixed two/three-property overloads, and exclusion filters store fixed tag hashes/layer indices. Loop 13 asset-publication scan found `EditorUtility.SetDirty` on mesh/material/texture replacement and creation paths, `PrefabUtility.SaveAsPrefabAsset(..., out bool)`, `AssetDatabase.SaveAssets()` after successful prefab/mock mesh publication, and persisted mock mesh reload before return. Loop 12 atlas overflow scan found `OverflowFallbackTileSize`, reserved fallback-tile packing, and source-copy rejection for `MaterialOverflow` rects. Loop 11 source safety scan found extraction job `SourceVertexCount` bounds checks and `FitsAttributeWindow` stride validation for position/normal/tangent/uv streams. Loop 10 atlas feature scan found sRGB albedo texture creation, linear normal/ARM texture creation, scaled RT copy with color-space-specific `RenderTextureReadWrite`, `CommitGpuAtlasForSerialization`, material keywords, and `AtlasGpuSerializationSync`. Loop 9 legacy scan returned no matches for `ComponentScratch`, old parameterless `IsInteractiveOrExcluded`, old `TryFindExclusionRoot(..., out)`, or `_FilterScratch`; interactive exclusion scratch is caller-owned by scan/bake/preview transactions. Self-audit XML parsed successfully. Feature scan found isolated asmdef, MeshData byte-stream extraction, NativeArray<uint> atlas SetPixelData, material UV scale/offset in the 160B segment DTO, readonly segment/atlas structs, list-fill component/material traversal, `ref readonly` segment reads, guarded `math.select` normalization, parallel LOD job `CombineDependencies`, per-bake TempJob black-box session, explicit layouts, uninitialized NativeArrays, direct mesh buffer writes, self-audit XML path, and dump path. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched only after CPU dropped to 36.4% and no dotnet/csc process existed; it failed with 169 existing unrelated dependency errors in `Hecton8.Core.csproj` (`Hecton8.Logistics.Grid`, `VaultGenerationHandle<>`, `SoundEmissionSignal`, docking/world/atmosphere bridge interfaces, etc.). No SHINOBU_211 file appeared in the emitted error list. Build was not relaunched after Loop 19 because the dependency wall is unchanged and outside SHINOBU_211.
Compile wall strikes: 1 / 3.

## Loop 5 Self Audit

- [x] Re-read SHINOBU_211 implementation after compaction and inspected bake/filter/serialization hot paths. DOD practice: source readback instead of trusting prior memory. Alternative rejected: final report from summary only. Estimate: 420 us static grep/read overhead excluding shell startup.
- [x] Fixed ancestor interactive-preservation miss: child meshes under an excluded parent now resolve to the excluded Transform root and are preserved once. DOD practice: gameplay hierarchy exclusion by ancestry. Alternative rejected: excluding only the MeshFilter GameObject. Estimate: prevents uncontrolled gameplay breakage; runtime cost remains 0 because tool is editor-only.
- [x] Re-ran whitespace/static forbidden API checks after patch. DOD practice: diff hygiene and negative API scan. Alternative rejected: claiming compile success. Estimate: 0 runtime us.

## Loop 6 Ultra Polish

- [x] Re-read CURRENT_BATCH XML, rationale, status, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before new code. DOD practice: disk truth over chat memory. Alternative rejected: continuing from summary alone. Estimate: 250 us excluding shell startup.
- [x] Isolated SHINOBU_211 source into `Hecton8.HabitatInteriorClutterForge.Editor.asmdef` with Unity Burst/Collections/Jobs/Mathematics references only. DOD practice: compile-wall containment. Alternative rejected: broad `Hecton8.Editor` dependency surface. Estimate: no runtime cost; editor compile blast radius reduced pending Unity import proof.
- [x] Replaced persistent editor black-box NativeArray with per-bake TempJob `InteriorClutterBlackBoxSession`. DOD practice: no private persistent runtime/native ownership; session disposed in bake transaction. Alternative rejected: InitializeOnLoad Persistent allocation. Estimate: saves 19.2 KB persistent editor allocation outside active bake.
- [x] Replaced managed atlas base fill (`Color32[]`/`SetPixel`) with Burst `FillAtlasSolidJob` / `FillAtlasRectColorsJob` over `NativeArray<uint>` and one `Texture2D.SetPixelData`. DOD practice: native texel staging. Alternative rejected: per-texel managed calls. Estimate: editor-only; exact microseconds pending Unity profiler.
- [x] Replaced source mesh extraction `Mesh.GetVertices/List<T>` staging with `Mesh.AcquireReadOnlyMeshData` byte-stream extraction jobs for UInt16/UInt32 index buffers. DOD practice: MeshData + pointer offsets + NoAlias. Alternative rejected: managed vertex lists in extraction path. Estimate: editor-only; exact microseconds pending profiler.
- [x] Replaced CSV `File.ReadAllBytes` + `StringBuilder` profile name parse with `FileStream` into `NativeArray<byte>` and `FixedString64Bytes` names. DOD practice: byte parser bridge. Alternative rejected: managed byte[] and per-token StringBuilder. Estimate: cold editor path only.
- [x] Added generated self-audit XML report path `Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml` with explicit 20-task reconciliation, layouts, scalability curve, H-PHI boundary, dependency graph, compile guard, and Dear Lie section. DOD practice: forensic artifact on disk. Alternative rejected: chat-only audit. Estimate: report write is editor-only.

## Loop 7 Material Fidelity Polish

- [x] Re-read SHINOBU_211 XML block after the user repeated the mandate. DOD practice: strict XML isolation via raw regex. Alternative rejected: noisy Select-String context across neighboring agents. Estimate: 250 us excluding shell startup.
- [x] Extended `InteriorClutterSegment` from 128B to 160B to carry `MaterialUvScaleOffset` at offset 80 and moved `RoomRelativeOffset` to offset 120. DOD practice: explicit ARM64 layout with 16B UV ST and 16B tail padding. Alternative rejected: hiding material tiling in managed side tables or dropping original UV tiling. Estimate: +32B per static segment in editor-only staging; runtime 0.
- [x] Updated transform/UV remap jobs to apply `uv * scale + offset`, wrap, then map into atlas rect. DOD practice: offline Dear Lie preserves tiled props inside one atlas material. Alternative rejected: runtime UV material correction. Estimate: 0.01 us per vertex target pending profiler.
- [x] Replaced atlas crop behavior for mismatched source/tile sizes with temporary RT blit plus `Graphics.CopyTexture`, and added `AtlasScaledTexture` warning. DOD practice: deterministic visual preservation. Alternative rejected: upper-left source crop into tile. Estimate: editor-only GPU copy; runtime 0.

## Loop 8 Allocation And Dependency Polish

- [x] Converted `InteriorClutterRenderSegment` and `InteriorMaterialAtlas` from sealed classes to readonly structs. DOD practice: avoid class-per-segment and atlas-container heap objects in the bake graph. Alternative rejected: continuing editor OO wrappers around immutable bake records. Estimate: removes one managed object per static render segment plus one atlas container object.
- [x] Replaced `renderer.sharedMaterials` and `GetComponentsInChildren<T>()` array paths in SHINOBU_211 collection/scan/preview code with list-fill APIs. DOD practice: list reuse in cold editor traversal. Alternative rejected: per-renderer/per-prefab managed arrays. Estimate: removes Material[] allocation per renderer and MeshFilter[] allocation per prefab scan/preview.
- [x] Switched transform job segment/source reads to `ref readonly` and branchless `math.select` normalization with `math.max` guarded `rsqrt`. DOD practice: avoid 160B segment copies and NaN-vaccinate inverse square root. Alternative rejected: copying segment DTO per vertex and ternary normalize. Estimate: saves 160B stack/register copy pressure per transformed vertex target pending Burst proof.
- [x] Scheduled LOD1 and LOD2 decimation independently and joined with `JobHandle.CombineDependencies`. DOD practice: remove false dependency. Alternative rejected: LOD2-after-LOD1 serialization. Estimate: editor bake latency reduction pending profiler.
- [x] Propagated `MaterialOverflow` from atlas rect flags into the bake metric. DOD practice: report truth from packer, not only rect-count mismatch. Alternative rejected: silent full-atlas fallback rects. Estimate: report-only; runtime 0.

## Loop 9 Scratch-State Containment

- [x] Removed hidden static `ComponentScratch` ownership from `InteriorClutterExcludeFilter`. DOD practice: caller-owned transaction scratch for scan/bake/preview. Alternative rejected: global mutable component buffer inside the filter. Estimate: runtime 0; editor contention risk removed.
- [x] Removed preview `_FilterScratch` static scratch list; preview traversal now uses local transaction lists while persistent static state is limited to visible overlay bounds. DOD practice: no hidden traversal scratch across preview calls. Alternative rejected: static scratch retained for allocation micro-optimization. Estimate: one cold editor list allocation per preview.
- [x] Re-ran legacy scratch scan. DOD practice: negative source evidence. Alternative rejected: relying on code review memory. Estimate: 0 runtime us.

## Loop 10 Atlas Serialization Fidelity

- [x] Added GPU-to-CPU atlas serialization sync after `Graphics.CopyTexture`/RT blit writes. DOD practice: copied source pixels are committed into CPU `Texture2D` data before `.asset` serialization. Alternative rejected: trusting GPU-only texture state to serialize. Estimate: editor-only readback cost; runtime 0.
- [x] Split atlas color space: albedo is sRGB, normal and ARM are linear, and temporary RTs use matching `RenderTextureReadWrite`. DOD practice: material channel correctness. Alternative rejected: treating normal/ARM payloads as color data. Estimate: runtime 0; visual correctness fix.
- [x] Enabled atlas material texture keywords for normal and mask/metallic maps. DOD practice: generated one-material room uses the atlas channels it just baked. Alternative rejected: assigning maps without shader feature activation. Estimate: runtime draw-call count unchanged.

## Loop 11 Source Mesh Safety

- [x] Added extraction job source-index bounds checks for UInt16/UInt32 mesh index streams. DOD practice: malformed source meshes write deterministic fallback vertices instead of unsafe pointer reads. Alternative rejected: trusting imported mesh index buffers blindly. Estimate: one unsigned compare per extracted vertex in editor-only bake.
- [x] Added `FitsAttributeWindow` stride/offset validation for source position, normal, tangent, and uv0 streams. DOD practice: declared attribute byte windows must fit inside the vertex stride before pointer reads. Alternative rejected: relying on MeshData attribute presence alone. Estimate: cold layout check per source mesh.
- [x] Optional malformed normal/tangent/uv streams now fall back to defaults instead of invalidating the entire bake; malformed position layout still rejects the mesh. DOD practice: position is required truth, optional presentation channels degrade safely. Alternative rejected: crashing the whole batch for a bad optional stream. Estimate: runtime 0.

## Loop 12 Atlas Overflow Containment

- [x] Reserved a 16px atlas fallback tile before guillotine packing. DOD practice: overflow materials have a bounded UV target. Alternative rejected: full-atlas overflow rect. Estimate: loses 256 atlas pixels from normal packing.
- [x] Overflow rects are now prevented from `Graphics.CopyTexture`/RT blit source-copy. DOD practice: an unfitted material cannot repaint the whole atlas. Alternative rejected: scaling overflow source to the entire atlas. Estimate: runtime 0; editor copy avoided for overflow materials.
- [x] Self-audit and architecture docs now state overflow containment. DOD practice: explicit failure-mode proof. Alternative rejected: silent material-overflow warning without containment. Estimate: report-only.

## Loop 13 Asset Publication Flush

- [x] Re-read SHINOBU_211 XML and current architecture/binary boundary before patching. DOD practice: prompt/document truth over chat memory. Alternative rejected: continuing from summary only. Estimate: 250 us excluding shell startup.
- [x] Marked generated mesh/material/texture replacements and creations dirty after `CopySerialized`/`CreateAsset`. DOD practice: deterministic Unity asset persistence. Alternative rejected: raw YAML or delete/recreate asset replacement. Estimate: runtime 0; one editor object dirty mark per generated asset.
- [x] Added prefab save success validation and one `AssetDatabase.SaveAssets()` flush after successful prefab/mock mesh publication. DOD practice: fail-fast publication proof. Alternative rejected: broad `StartAssetEditing` suppression that could hide assets before prefab construction. Estimate: runtime 0; editor flush once per successful publication.
- [x] Reloaded persisted mock mesh before returning from the emergency mock benchmark. DOD practice: return saved asset identity, not a transient/destroyed mesh. Alternative rejected: returning the pre-save mesh after replacement. Estimate: runtime 0; editor load only.

## Loop 14 Fixed Texture Property Lookup

- [x] Re-read SHINOBU_211 XML and relevant rendering/visual-fake mandates before patching. DOD practice: bounded owner-domain pass. Alternative rejected: editing runtime renderer or shader systems outside the forge. Estimate: 250 us excluding shell startup.
- [x] Removed `params string[]` from atlas source texture discovery. DOD practice: fixed shader-property alias dispatch without per-call array allocation. Alternative rejected: keeping params because path is editor-only. Estimate: runtime 0; removes one managed array allocation per material/channel lookup.
- [x] Preserved URP/Standard property alias order for albedo, normal, and ARM channels. DOD practice: no visual behavior drift while removing allocation residue. Alternative rejected: reflection or broad material abstraction. Estimate: editor-only.
- [x] Replaced exclusion filter `string.Split`/`string[]` storage with fixed tag hashes and layer indices. DOD practice: unmanaged fixed token lists for scan/bake traversal. Alternative rejected: concrete gameplay class dependencies or repeated layer-name comparisons. Estimate: runtime 0; removes filter token arrays and per-check layer-name string comparisons.

## Loop 15 Black-Box Forensic Tightening

- [x] Re-read status/rationale and re-extracted SHINOBU_211 XML before patching. DOD practice: anti-amnesia source-of-truth loop. Alternative rejected: continuing from compressed summary only. Estimate: 250 us excluding shell startup.
- [x] Reset the per-bake black-box ring explicitly after uninitialized TempJob allocation. DOD practice: deterministic forensic payload without `MemClear`. Alternative rejected: dumping uninitialized telemetry slots. Estimate: 300 fixed 64-byte writes per bake session, editor-only.
- [x] Replaced black-box `Time.frameCount` with deterministic local frame indices and added `RecordFailure` before exception dumps. DOD practice: reproducible source hash in failure dumps. Alternative rejected: relying on Unity frame time or Console text. Estimate: runtime 0; editor-only failure path.
- [x] Re-ran Loop 15 scans: no `Time.frameCount`, no stale UI "Complete" bake messages, XML parse OK, forbidden API scan OK, trailing whitespace OK. DOD practice: negative source evidence. Alternative rejected: claiming from code review only. Estimate: 0 runtime us.

## Loop 16 Chronological Dump Ordering

- [x] Re-read status/rationale and SHINOBU_211 XML before patching. DOD practice: disk-state continuity. Alternative rejected: patching from memory. Estimate: 250 us excluding shell startup.
- [x] Changed black-box dump to write the oldest retained entry first after ring wrap, then newest last, using two direct `ReadOnlySpan<byte>` writes from the native ring. DOD practice: forensic dump is ordered without managed staging. Alternative rejected: raw physical ring order. Estimate: runtime 0; editor dump path only.

## Loop 17 Scanner Scratch Reuse

- [x] Re-read status/rationale and SHINOBU_211 XML before patching. DOD practice: anti-amnesia disk truth. Alternative rejected: relying on compressed context. Estimate: 250 us excluding shell startup.
- [x] Moved `Hierarchy_Bloat_Scanner` material uniqueness scratch outside the prefab iteration and clears it per prefab. DOD practice: transaction scratch reuse in cold batch scanner. Alternative rejected: one `List<Material>` allocation per scanned prefab. Estimate: runtime 0; editor removes one managed list allocation per prefab scan.

## Loop 18 Bake Scratch Reuse

- [x] Re-read owned source and allocation scan before patching. DOD practice: evidence-based scratch ownership pass. Alternative rejected: broad refactor. Estimate: 300 us excluding shell startup.
- [x] Added `InteriorClutterBakeScratch` and passed it through batch `BakeFolder` to private `BakePrefab`, reusing segment/material/filter/component lists per prefab and clearing after prefab-content unload. DOD practice: transaction-owned managed scratch. Alternative rejected: allocating six lists per prefab bake. Estimate: runtime 0; editor removes six list allocations per prefab in folder bake.
- [x] Updated architecture and self-audit HPhi section to reflect batch scratch ownership. DOD practice: docs mirror code. Alternative rejected: stale forensic artifacts. Estimate: report-only.

## Loop 19 Asset Token Allocation Trim

- [x] Re-read allocation-surface scan and selected `SanitizeToken` because it touched every generated asset name. DOD practice: narrow cold-path trim. Alternative rejected: broad string-system rewrite. Estimate: 120 us excluding shell startup.
- [x] Removed unconditional `ToCharArray`; safe names return as-is, invalid names sanitize through a short `StringBuilder`. DOD practice: avoid extra char buffer. Alternative rejected: `char[]` clone per prefab token. Estimate: runtime 0; editor avoids one char-array allocation per already-safe token.
