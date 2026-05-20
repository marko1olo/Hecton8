# LOG_SHINOBU_211

Top = old, bottom = new.

## Session Start

What was wrong: SHINOBU_211 had no active status, rationale, or log files in the current batch workspace.
What was done: Created active tracking files and isolated the SHINOBU_211 XML block from CURRENT_BATCH.md.
Cinematic Cheats used: Offline static-clutter baking instead of runtime object simulation/hierarchy traversal.
Exact Microseconds saved: PENDING VERIFICATION; no Unity profiler capture yet.

## Final Report - 2026-05-20

What was wrong: Interior/static clutter had no dedicated offline forge in the current project tree. `Assets/_Project/Prefabs/Habitat` is absent, so the real scan target had to fall back to `Assets/_Project/Prefabs/Construction/Final`. Static props still risked many Transform nodes, MeshRenderers, material sets, and uncontrolled editor-only bake behavior. A late self-audit also found a preservation bug: child meshes under an interactive parent could be baked if only the child GameObject was inspected.

What was done: Added `InteriorClutterForge` Editor tooling under `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/`. The tool scans prefab hierarchy bloat, builds one atlas material per room, writes albedo/normal/ARM atlas assets, transforms triangle-soup geometry in Burst jobs, writes 32-byte mesh vertex buffers with `SetVertexBufferParams`/`SetVertexBufferData`, creates LOD0/LOD1/LOD2 generated prefabs, preserves interactive hierarchy roots under `INTERACTIVE_PRESERVED`, and writes JSON reports. Added CSV atlas profiles at `Assets/_Project/Data/Rendering/texture_atlas_profiles.csv`. Added architecture boundary doc at `Docs/ARCHITECTURE/HABITAT_INTERIOR_CLUTTER_FORGE.md`. Added black-box editor telemetry dump path `Docs/AgentLogs/Dump_SHINOBU_211.bin`.

Cinematic Cheats used: Offline bake instead of runtime clutter simulation. One atlas material instead of preserving material fidelity as draw-call debt. Deterministic triangle-soup LOD collapse instead of full runtime prop culling or expensive QEM decimation. Solid-color atlas fallback plus `Graphics.CopyTexture` attempt instead of blocking on readable texture imports. Root-relative AUP-safe local bake instead of absolute world float truth.

Exact Microseconds saved: Verified runtime savings = 0 us, because Unity import, Frame Debugger, profiler, and generated prefab readback were not run. Estimated targets pending proof: transform traversal removal scales with removed static child count; SetPass reduction target is N clutter materials to 1 visible material per room; Burst transform target remains about 0.04 us per vertex from mandate estimate only; UV remap target remains about 0.01 us per vertex; layout validation estimate is 15 us per call. These numbers are not profiler evidence.

Verification: `git diff --check` passed for SHINOBU_211 files. Forbidden API scan found no `Mesh.CombineMeshes`, `Texture2D.PackTextures`, direct `.material`, direct `.materials`, runtime update loops, GlobalRegistry, EventBus publish, or coroutine use in SHINOBU_211 files. Feature scan found explicit layouts, `NativeArrayOptions.UninitializedMemory`, `UnsafeUtility.AsRef`, direct mesh buffer writes, `Graphics.CopyTexture`, SceneView preview, report self-audit, and dump path. CPU sampled at 100%; no `dotnet`/`csc` process was active, but project rules forbid launching build under >50% CPU, so compile remains PENDING VERIFICATION.

Failure modes left open: GPU copy atlas serialization still needs Unity readback verification. Generated LOD quality needs visual inspection. One visible draw call per LOD renderer must be confirmed in Frame Debugger after generated prefabs exist. Full edge-collapse decimation was rejected for this batch; current decimator is deterministic and cheap, not QEM-grade.

<SELF_AUDIT agent="SHINOBU_211" status="PENDING_VERIFICATION">
  <TaskCount>20</TaskCount>
  <RuntimeImpact>Editor-only source; generated assets only.</RuntimeImpact>
  <VertexLayout>32 bytes: position float3 offset 0, normal float3 offset 12, uv0 float2 offset 24.</VertexLayout>
  <DrawCallContract>Generated room section uses one atlas material per LOD renderer; Frame Debugger proof absent.</DrawCallContract>
  <InteractiveFence>Ancestor exclusion filter preserves excluded hierarchy root once.</InteractiveFence>
  <Scalability>Low 4K/512 aggressive LOD2; Middle 4K/1024; High 8K/1024; Ultra 8K/2048 with quality-weight retention.</Scalability>
  <BuildState>Not compiled due CPU rule; no fake green report.</BuildState>
</SELF_AUDIT>

## Scratch-State Containment - 2026-05-20

What was wrong: The interactive exclusion filter kept a hidden static `List<Component>` scratch buffer. Preview traversal also kept static scratch for mesh filters. Both were editor-only, but the ownership model was sloppy: the filter looked stateful and global.

What was done: `InteriorClutterExcludeFilter` now receives caller-owned `List<Component>` scratch from scan, bake collection, and preview transactions. Preview traversal uses local transaction lists; only the visible overlay bounds remain static because SceneView repaint needs persistent draw state.

Cinematic Cheats used: None new. This preserves the offline atlas/LOD Dear Lie and tightens editor ownership.

Exact Microseconds saved: Runtime 0 us. Editor microseconds are PENDING VERIFICATION. This change removes hidden global scratch contention and accepts one cold preview list allocation instead of preserving implicit global mutable traversal state.

Verification: Legacy scratch scan returned no matches for `ComponentScratch`, parameterless `IsInteractiveOrExcluded`, old `TryFindExclusionRoot(..., out)`, or `_FilterScratch` in SHINOBU_211 files. Full solution build was not relaunched because the known unrelated dependency wall remains strike 1/3.

## Atlas Serialization Fidelity - 2026-05-20

What was wrong: Atlas generation copied source textures through GPU routes, then immediately saved generated `Texture2D` assets. Without an explicit readback, saved `.asset` textures could retain fallback CPU texels while copied source pixels lived only in transient GPU state. Normal and ARM atlases were also created as sRGB textures.

What was done: Added an editor-only GPU-to-CPU serialization sync: copied atlases are blitted to a temporary RT, read once through `ReadPixels`, then applied before `AssetDatabase.CreateAsset`. Albedo atlas stays sRGB; normal and ARM atlases are linear. Temporary RTs use matching read/write space. Generated atlas material now enables normal and mask/metallic keywords.

Cinematic Cheats used: Same offline atlas Dear Lie. This pass makes the fake durable on disk instead of relying on transient GPU state.

Exact Microseconds saved: Runtime 0 us. Editor readback cost is PENDING VERIFICATION and intentionally paid only after source-copy success. Visual fidelity and deterministic serialization were prioritized over editor microseconds.

Verification: Static feature scan found `CommitGpuAtlasForSerialization`, color-space-specific atlas creation, `RenderTextureReadWrite` selection, `AtlasGpuSerializationSync`, and material keyword activation. Full solution build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Source Mesh Safety - 2026-05-20

What was wrong: The MeshData extraction jobs trusted source index buffers and attribute stride windows too much. A malformed prefab mesh could make the unsafe pointer path read beyond an attribute window.

What was done: Added source-index bounds checks to both UInt16 and UInt32 extraction jobs. Added `FitsAttributeWindow` validation for source position, normal, tangent, and uv0 streams. Position layout remains mandatory; optional malformed presentation channels degrade to fallback normal/tangent/uv data.

Cinematic Cheats used: None new. This protects the offline bake pipeline that feeds the atlas/LOD Dear Lie.

Exact Microseconds saved: Runtime 0 us. Editor cost is one unsigned compare per extracted vertex plus cold stride validation per source mesh; no profiler proof yet.

Verification: Static scan found `SourceVertexCount` in both extraction jobs and `FitsAttributeWindow` validation in the MeshData layout resolver. Full solution build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Atlas Overflow Containment - 2026-05-20

What was wrong: Overflow material packing used a full-atlas fallback rect. That could let one unfitted material overwrite the entire atlas fill or source-copy over already packed regions.

What was done: Reserved a 16px fallback tile before guillotine packing. Overflow materials now map to that tile and are flagged; source-copy is skipped for `MaterialOverflow` rects.

Cinematic Cheats used: Overflow visual fidelity degrades to a bounded fallback tile instead of preserving a material at the cost of corrupting every other atlas region.

Exact Microseconds saved: Runtime 0 us. Editor saves any skipped overflow source-copy, but exact microseconds are PENDING VERIFICATION. Cost is 256 reserved pixels per atlas.

Verification: Static scan found `OverflowFallbackTileSize`, fallback-tile reservation, and copy rejection for `MaterialOverflow` rects. Full solution build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Allocation And Dependency Polish - 2026-05-20

What was wrong: SHINOBU_211 still had editor allocation residue: class wrappers for render segments and atlas result, per-renderer `sharedMaterials` arrays, array-return `GetComponentsInChildren` calls, component arrays in exclusion checks, branchy normalize/collapse math, and an artificial LOD2-after-LOD1 dependency.

What was done: Converted `InteriorClutterRenderSegment` and `InteriorMaterialAtlas` to readonly structs. Replaced material/component traversal with Unity list-fill APIs and scratch lists. `TransformAndAppendVerticesJob` now reads source and segment DTOs through `ref readonly`, uses `math.max` guarded `rsqrt` and `math.select` fallback normals, and LOD triangle collapse always lerps by a continuous scalar. LOD1 and LOD2 decimation now schedule independently and join via `JobHandle.CombineDependencies`. Atlas material-overflow flags now propagate from rect DTOs into bake metrics.

Cinematic Cheats used: No new runtime simulation. The same offline atlas/LOD Dear Lie remains, now with less editor heap churn and fewer artificial job stalls.

Exact Microseconds saved: Runtime verified savings remain 0 us pending Unity import/profiler proof. Static estimate: removes one managed segment object per static submesh, one atlas container object per room bake, one Material[] per renderer traversal, and MeshFilter[] arrays per scan/preview. Transform job avoids a 160-byte segment DTO copy per vertex; exact Burst microseconds remain pending.

Verification: Loop 8 static scans found no old sealed class segment/atlas wrappers, `sharedMaterials`, `Material[]`, `MeshFilter[]`, old LOD dependency wording, old Segment=128/offset104 claims, forbidden Mesh/Texture/material/global-bus APIs, or trailing whitespace in SHINOBU_211 files. Self-audit XML parsed. Full solution build was not relaunched because the previous build already hit unrelated dependency wall strike 1/3.

## Build Attempt - 2026-05-20

What was wrong: Compile proof was still missing. CPU later dropped below the local 50% build gate and no `dotnet`/`csc` process was active.

What was done: Ran one capped build: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.

Cinematic Cheats used: None. This was verification only.

Exact Microseconds saved: 0 runtime us. Build attempt took 112 seconds and failed before SHINOBU_211 could receive a green compile proof.

Verification result: Build failed with 169 errors in existing non-SHINOBU domains. Representative blockers: `Hecton8.Logistics.Grid` missing in Power files; `VaultGenerationHandle<>` missing across Core/Data/Construction/Power; `SoundEmissionSignal` missing in audio contracts; docking/world/atmosphere bridge interfaces missing. No emitted error referenced `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge*.cs`.

Action: Marked compile wall strike 1/3 in `Docs/Tasks/Status_SHINOBU_211.md`. No foreign-domain fix attempted.

## Material Fidelity Polish - 2026-05-20

What was wrong: The atlas UV remap still assumed material UV identity. Props authored with `_BaseMap` or `_MainTex` tiling/offset would bake into the correct atlas rect but lose their material-space transform. The atlas copy path also cropped mismatched source textures into the tile window instead of scaling them.

What was done: Expanded `InteriorClutterSegment` to 160 bytes with `MaterialUvScaleOffset` at offset 80, scalar fields at 96-116, `RoomRelativeOffset` at offset 120, and 16 bytes of tail padding. `TransformAndAppendVerticesJob` and `RemapUvCoordinatesJob` now apply `uv * scale + offset`, wrap, and then remap into the atlas rect. Atlas copy now uses exact-size `Graphics.CopyTexture` directly and mismatched source/tile sizes through temporary RT blit plus `Graphics.CopyTexture`; scaled copies raise `AtlasScaledTexture`.

Cinematic Cheats used: The Dear Lie is now stricter: authored material tiling is folded into offline UVs, so runtime still renders one atlas material per room LOD without material arrays or per-prop UV controllers.

Exact Microseconds saved: Runtime verified savings remain 0 us pending Unity import/profiler proof. Editor staging cost increases by 32 bytes per static segment; runtime stays at one renderer/material per visible LOD. The crop fix is editor-only GPU work and avoids a visual defect, not a measured runtime speed claim.

Verification: Static forbidden scan still found no managed Mesh.GetVertices/List extraction, per-pixel SetPixel/Color32[] atlas fill, persistent black-box NativeArray, Pack=1, hot get/set DTO properties, Mesh.CombineMeshes, Texture2D.PackTextures, direct material mutation, GlobalRegistry/EventBus, runtime update loops, or coroutine routes in SHINOBU_211 files. Build remains PENDING due CPU gate until a <50% CPU/no dotnet/no csc window exists.

<SELF_AUDIT agent="SHINOBU_211" pass="MATERIAL_FIDELITY_POLISH" status="STATIC_SOURCE_PENDING_UNITY_IMPORT">
  <TaskReconciliation>Tasks 01-20 remain implemented; Task 04 layout and Task 08 UV remap were tightened.</TaskReconciliation>
  <StructLayout>RawVertex=32B offsets 0/12/24; SourceVertex=64B; Segment=160B with MaterialUvScaleOffset at offset80 and double3 at offset120; AtlasColor=16B; Telemetry=64B.</StructLayout>
  <ScalabilityCurve>GlobalQualityWeight remains continuous; low profiles scale more source textures into smaller tiles, high/ultra profiles retain larger source fidelity.</ScalabilityCurve>
  <HPhiVaultStatus>Runtime Vault handles: none. Runtime private NativeArrays: 0. Editor TempJob arrays remain scoped and disposed.</HPhiVaultStatus>
  <DependencyGraph>MeshData extract jobs -> TransformAndAppendVertices with UV ST -> LOD decimation; FillAtlas jobs -> Texture2D.SetPixelData -> Graphics.CopyTexture/scaled RT blit.</DependencyGraph>
  <CompileGuard>No runtime sibling assembly reference added; domain stays in the editor asmdef.</CompileGuard>
  <DearLie>Offline atlas UV transform and texture scaling preserve visual intent while collapsing runtime materials/renderers to O(1) per visible room LOD.</DearLie>
</SELF_AUDIT>

## Ultra Polish Pass - 2026-05-20

What was wrong: The first implementation was functionally scoped but not strict enough for the new mandate. It had broad editor assembly exposure, a persistent editor NativeArray black-box, managed `Mesh.GetVertices(List<T>)` extraction, managed atlas base fill, and CSV profile ingestion through `File.ReadAllBytes` plus `StringBuilder`.

What was done: Added `Hecton8.HabitatInteriorClutterForge.Editor.asmdef` with minimal Unity/Burst references. Replaced source extraction with `Mesh.AcquireReadOnlyMeshData` and Burst UInt16/UInt32 extraction jobs over byte streams, offsets, and strides. Replaced atlas base fill with `NativeArray<uint>` plus `FillAtlasSolidJob` and `FillAtlasRectColorsJob`, uploaded through `Texture2D.SetPixelData`. Replaced persistent black-box allocation with per-bake TempJob `InteriorClutterBlackBoxSession`. Replaced CSV profile staging with `FileStream -> NativeArray<byte> -> FixedString64Bytes`. Added self-audit XML generation at `Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml`.

Cinematic Cheats used: The unchanged central cheat is still offline atlas UV remap plus deterministic LOD collapse: runtime sees one renderer/material per room LOD instead of prop hierarchy/material state churn. No runtime BRG/HZB path was invented because this domain produces immutable room assets, not a runtime culling owner.

Exact Microseconds saved: Runtime verified savings remain 0 us because Unity import, bake, Frame Debugger, and profiler proof are still absent. Editor estimates only: persistent black-box memory outside bake reduced by 19.2 KB; managed atlas base fill removed for `atlasSize^2` texel initialization per texture; managed source vertex-list staging removed for each source mesh. Numerical microseconds are PENDING VERIFICATION.

Verification: Static forbidden scan found no `Mesh.GetVertices`, `GetNormals`, `GetTangents`, `GetUVs`, `GetTriangles`, `SetPixel`, `SetPixels32`, `Color32[]`, persistent `InteriorClutterBlackBox`, `Pack=1`, hot `{ get; set; }`, `Mesh.CombineMeshes`, `Texture2D.PackTextures`, direct `.material/.materials`, runtime update loops, GlobalRegistry, EventBus, or coroutine routes in SHINOBU_211 files. All Burst jobs carry `FloatMode.Fast`, `FloatPrecision.Standard`, and `CompileSynchronously=true`. CPU sampled at 100%; no build launched under project rule.

<SELF_AUDIT agent="SHINOBU_211" pass="ULTRA_POLISH" status="STATIC_SOURCE_PENDING_UNITY_IMPORT">
  <TaskReconciliation>Tasks 01-20 remain PASS in `Docs/Tasks/Status_SHINOBU_211.md`; generated runtime/profiler proof remains PENDING.</TaskReconciliation>
  <StructLayout>RawVertex=32B offsets 0/12/24; SourceVertex=64B; Segment=128B with double3 at offset104; AtlasColor=16B; Telemetry=64B.</StructLayout>
  <ScalabilityCurve>GlobalQualityWeight stays continuous through CSV profiles, LOD ratios, small-detail collapse, mock clutter length, and atlas tile caps. No low/high binary hardware branch was added.</ScalabilityCurve>
  <HPhiVaultStatus>Runtime Vault handles: none. Runtime private NativeArrays: 0. Editor-only Temp/TempJob arrays are scoped to CSV parse, bake transaction, or atlas texture staging and disposed.</HPhiVaultStatus>
  <DependencyGraph>ExtractClutterUInt16/UInt32 -> TransformAndAppendVertices -> DecimateTriangleSoup LOD jobs; FillAtlasSolid -> FillAtlasRectColors -> Texture2D.SetPixelData. No SystemDispatcher runtime edge exists because this is Editor-only.</DependencyGraph>
  <CompileGuard>Assembly `Hecton8.HabitatInteriorClutterForge.Editor`; no direct runtime sibling references added.</CompileGuard>
  <DearLie>Offline UV-atlas remap reduces runtime material/render hierarchy from O(props + materials) to O(1) per visible room LOD.</DearLie>
</SELF_AUDIT>

## Asset Publication Flush - 2026-05-20

What was wrong: Generated asset replacement relied on `CopySerialized` without explicitly dirtying the existing asset object. Prefab save ignored the success flag, and the emergency mock benchmark could return a transient mesh object that had been destroyed after replacing an existing mock asset.

What was done: Marked generated mesh/material/texture replacements and new asset objects dirty, checked `PrefabUtility.SaveAsPrefabAsset(..., out bool success)`, flushed `AssetDatabase.SaveAssets()` once after successful prefab/mock mesh publication, and reloaded the persisted mock mesh before returning it.

Cinematic Cheats used: None added in this pass. Existing Dear Lie remains offline atlas UV remap plus deterministic LOD collapse, replacing runtime prop hierarchy and material diversity with immutable presentation geometry.

Exact Microseconds saved: Runtime 0 by design. Editor save overhead increases by one explicit asset flush per successful publication; the gain is correctness and deterministic persistence, not frame time.

Verification: Static scan found `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`, `SaveAsPrefabAsset(..., out bool)`, and mock asset reload in SHINOBU_211 files. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Fixed Texture Property Lookup - 2026-05-20

What was wrong: Atlas source texture lookup used `ResolveTexture(Material, params string[])`, creating a managed array per material/channel lookup during albedo, normal, and ARM atlas construction.

What was done: Replaced the `params` helper with fixed two-property and three-property overloads while preserving the same URP/Standard alias order.

Cinematic Cheats used: None added. Existing offline atlas UV remap remains the draw-call collapse mechanism.

Exact Microseconds saved: Runtime 0 by design. Editor removes one small managed array allocation per material/channel texture lookup; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan found no `params string[]` in SHINOBU_211 forge files. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Fixed Exclusion Filter Tokens - 2026-05-20

What was wrong: The interactive preservation filter used `string.Split`, `string[]`, and repeated layer-name comparison in scan/bake traversal. It was editor-only, but it was still avoidable heap churn around the part of the forge that may scan hundreds of prefab hierarchies.

What was done: Replaced the filter storage with fixed lists of tag hashes and layer indices. Tags are hashed from fixed tokens; layers resolve once during filter parse and compare by integer during traversal.

Cinematic Cheats used: None added. The gameplay truth remains the same: interactive roots are excluded from static atlasing and preserved as separate children.

Exact Microseconds saved: Runtime 0 by design. Editor removes `string.Split` arrays and layer-name string comparisons during traversal; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan found no `value.Split`, `SplitCsv`, or filter-owned `string[]` tag/layer storage in SHINOBU_211 files. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Black-Box Forensic Tightening - 2026-05-20

What was wrong: The per-bake telemetry ring used uninitialized TempJob memory and did not write a failure marker before direct single-prefab bake dumps. That made exception evidence weaker than the 300-frame black-box mandate requires.

What was done: Added deterministic ring reset through fixed index writes after allocation, replaced Unity frame time with a local monotonically increasing frame index, added `BakeException`, added `RecordFailure` before folder/direct bake dumps, and appended written-entry count to the dump reason sidecar.

Cinematic Cheats used: None added. This is forensic hardening; the existing Dear Lie remains offline atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor cost increases by 300 fixed 64-byte writes per bake session; this buys deterministic `.h8dump`-style evidence instead of uninitialized slots. Exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scans found no `Time.frameCount`, no stale bake UI "Complete" text, no forbidden mesh/material APIs, no `params string[]`, no `value.Split`, no `SplitCsv`, and no filter-owned `string[]`. Self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Chronological Black-Box Dump Order - 2026-05-20

What was wrong: After 300 entries, the circular telemetry ring would dump in physical memory order. The data was present, but post-wrap inspection required reconstructing the cursor manually.

What was done: `Dump` now writes pre-wrap rings as fixed zero-padded order and post-wrap rings as chronological retained entries using two direct `ReadOnlySpan<byte>` writes from the native ring. No managed staging buffer was introduced.

Cinematic Cheats used: None. This is binary forensic ordering only.

Exact Microseconds saved: Runtime 0 by design. Editor dump writes the same 19.2 KB payload; no measured speed claim. The value is faster failure analysis, not frame time.

Verification: Static source read found `WriteRange(stream, basePtr, entrySize, start, ...)` and no managed reorder array. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Scanner Material Scratch Reuse - 2026-05-20

What was wrong: `Hierarchy_Bloat_Scanner.ScanProject` allocated a fresh `List<Material>` for every prefab inspected. That does not affect runtime, but it is avoidable heap churn in the exact batch scanner that may process a large habitat library.

What was done: Moved the material uniqueness list to scan-transaction scope and cleared it per prefab. Shared material list and component scratch already stayed outside the inner loop; the scanner now follows the same ownership pattern for unique material collection.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor removes one managed list allocation per scanned prefab; exact scan-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP17_SCANNER_SCRATCH_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Batch Bake Scratch Reuse - 2026-05-20

What was wrong: Folder bake transactions still allocated six managed scratch lists inside every private `BakePrefab` call: static segments, preserved interactive roots, unique materials, mesh filters, shared materials, and component probes.

What was done: Added `InteriorClutterBakeScratch`, created it once per `BakeFolder`, passed it by ref into private `BakePrefab`, and cleared it before each prefab plus after prefab-content unload. Standalone single-prefab bake still owns one scratch transaction.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor folder bake removes six managed list allocations per prefab; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP18_BAKE_SCRATCH_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Asset Token Allocation Trim - 2026-05-20

What was wrong: `SanitizeToken` used `ToCharArray()` for every generated asset token, allocating an extra char buffer even when prefab names were already safe.

What was done: Added a safe-character scan. Safe tokens return the original string; invalid names sanitize through a short `StringBuilder`.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor avoids one char-array allocation per safe generated token; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP19_TOKEN_FAST_PATH_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches, including `ToCharArray`. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.
