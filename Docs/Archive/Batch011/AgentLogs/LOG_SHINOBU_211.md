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

Exact Microseconds saved: Runtime verified savings remain 0 us pending Unity import/profiler proof. Static estimate: removes one managed segment object per static submesh, one atlas container object per room bake, one Material[] per renderer traversal, and MeshFilter[] arrays per scan/preview. Transform job avoids a 192-byte segment DTO copy per vertex after the normal-basis expansion; exact Burst microseconds remain pending.

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

## Asset Folder Path Walk - 2026-05-20

What was wrong: `EnsureAssetFolder` used `Split('/')`, allocating a string array of path parts on generated asset folder creation paths.

What was done: Replaced split-based folder construction with a separator-range walk. Save helpers now pass `Path.GetDirectoryName(path)` directly; normalization is centralized inside `EnsureAssetFolder`.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor avoids one path-segment string-array allocation per missing-folder creation path; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP20_ASSET_FOLDER_WALK_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## LOD Renderer Handle Return - 2026-05-20

What was wrong: Generated prefab construction could waste component lookup work if the LOD setup reacquired renderers from child objects after creating them.

What was done: Verified the generated prefab path keeps `CreateLodRenderer` returning the `MeshRenderer` handle and feeds that handle directly into `LODGroup.SetLODs`.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor avoids three renderer component lookups per generated prefab; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP21_LOD_RENDERER_HANDLE_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Renderer Lookup Tightening - 2026-05-20

What was wrong: Static traversal code in scanner, preview, and bake collection queried `MeshRenderer` components with `GetComponent<MeshRenderer>()` after already holding each `MeshFilter`.

What was done: Replaced those probes with `TryGetComponent(out MeshRenderer renderer)` and kept every exclusion, bounds, and shared-material path unchanged.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor removes the `GetComponent<MeshRenderer>()` object-return pattern from three traversal loops; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP22_TRYGET_RENDERER_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Atlas Shader Fail-Fast - 2026-05-20

What was wrong: Atlas material construction could still pass a null shader to `new Material` if all fallback shader lookups failed in a broken editor import.

What was done: Resolved URP Lit, Standard, and Hidden/InternalErrorShader explicitly and throw `InvalidOperationException` before material construction if none resolve.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor adds two null checks during atlas material creation; this is correctness hardening, not a speed claim.

Verification: Static scan returned `LOOP23_SHADER_FAIL_FAST_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Atlas Copy Failure Flag - 2026-05-20

What was wrong: Texture copy exceptions collapsed into the same `false` result as missing source textures, reducing report value when an import setting or GPU copy path failed.

What was done: Added `AtlasCopyFailure` and threaded a `copyFailure` boolean through albedo, normal, and ARM copy attempts. Missing textures remain normal fallback; exceptions set the warning bit.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor cost is one exception-path flag write; this is report correctness, not speed.

Verification: Static scan returned `LOOP24_ATLAS_COPY_FAILURE_FLAG_OK`; self-audit XML parsed; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Column-Wise Transform Kernel - 2026-05-20

What was wrong: The transform job created a `float4` position and a `float3x3` normal matrix per vertex even though the segment transform columns were already available.

What was done: Replaced those temporaries with direct column-wise position and normal transforms from `LocalToRoom.c0/c1/c2/c3`, keeping NaN guards and atlas UV remap unchanged.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor avoids one `float4` and one `float3x3` temporary per transformed vertex; exact Burst-loop microseconds are PENDING VERIFICATION.

Verification: Static scan returned `LOOP25_COLUMNWISE_TRANSFORM_OK`; trailing whitespace scan passed; refined forbidden-API scan returned no matches. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Vertex Layout Facade - 2026-05-20

What was wrong: The 32-byte mesh vertex descriptor lived in an `internal static readonly` array. The reference could not be replaced, but any same-assembly code could still mutate descriptor elements and silently corrupt the mesh ABI.

What was done: Made the descriptor array private to `InteriorClutterVertexLayoutValidator` and routed mesh uploads through `ApplyVertexBufferParams(mesh, vertexCount)`. Loop 49 later removed the managed descriptor array entirely.

Cinematic Cheats used: None added. Existing Dear Lie remains offline texture atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor time is unchanged; this is ABI containment, not a speed claim.

Verification: Static scan returned `LOOP26_VERTEX_LAYOUT_FACADE_OK`; XML parse, trailing whitespace scan, and refined forbidden-API scan passed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Named Job Barriers And Atlas Fill Dependency - 2026-05-20

What was wrong: The forge still had inline `Schedule(...).Complete()` chains, and atlas rect-color fill did not explicitly depend on the solid fallback fill. Independent writes to the same texel buffer are not a dependency graph.

What was done: Replaced inline completion chains with named handles for transform, LOD, mock, extraction, mesh buffer, and atlas fill barriers. `FillAtlasRectColorsJob` now schedules after `solidFillHandle`.

Cinematic Cheats used: None added. Existing Dear Lie remains offline atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor microseconds are PENDING VERIFICATION; the important gain is deterministic atlas write order and auditable completion barriers.

Verification: Static scan returned `LOOP27_NAMED_JOB_BARRIERS_OK`; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## NativeArray Window Audit Wording - 2026-05-20

What was wrong: The audit wording could be read as if the forge followed the prompt's shared `NativeList` append literally. The code does the better DOD path: pre-sized buffers and deterministic windows.

What was done: Updated generated self-audit text and architecture docs to state that mock/transform jobs write fixed `NativeArray` windows and explicitly reject shared `NativeList` append contention.

Cinematic Cheats used: None added. Existing Dear Lie remains offline atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0 by design. Editor avoids synchronized append contention in the vertex transform path; exact bake-time microseconds are PENDING VERIFICATION.

Verification: Static scan found the explicit NativeList-rejection wording and `sharedAppend` self-audit attribute; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Proof Boundary Correction - 2026-05-20

What was wrong: `HABITAT_CONSOLIDATION_SELF_AUDIT.xml` still said `PENDING_CPU_BUILD_GUARD`, even though one capped build was already attempted and blocked by unrelated Core dependency errors.

What was done: Changed the generated and checked-in self-audit proof boundary to `BLOCKED_UNRELATED_CORE_DEPENDENCY_WALL_STRIKE_1`.

Cinematic Cheats used: None. This is evidence bookkeeping.

Exact Microseconds saved: Runtime 0. Build was not relaunched.

Verification: Proof-boundary scan found `BLOCKED_UNRELATED_CORE_DEPENDENCY_WALL_STRIKE_1`; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Direct Intrinsics Deviation Wording - 2026-05-20

What was wrong: Task 06 wording could be read as direct `Unity.Burst.Intrinsics` compliance, but the owned source has no verified NEON/SSE hand-written intrinsic path.

What was done: Updated Task 06 status/self-audit wording and rationale to state the deviation plainly: raw-pointer column-wise Burst math remains, direct intrinsics are rejected until cross-platform parity is proven.

Cinematic Cheats used: None. This is transform-kernel evidence hygiene.

Exact Microseconds saved: Runtime 0. Editor transform microseconds are PENDING VERIFICATION.

Verification: Direct-intrinsics deviation text was found in generated self-audit and source generator; forbidden API and trailing whitespace scans passed; self-audit XML parsed; ten Burst job attributes carry the mandated FloatMode/Fast precision flags. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## XML Escape Split - 2026-05-20

What was wrong: Self-audit task text used the JSON escape helper. That was format leakage; future `<`, `>`, or `&` in a task reason could break XML.

What was done: Added `EscapeXml` and routed `AppendTaskAudit` through it. JSON reports still use the existing quote/backslash `Escape`.

Cinematic Cheats used: None. This is report serialization hardening.

Exact Microseconds saved: Runtime 0. Editor report generation cost is negligible and PENDING VERIFICATION.

Verification: Static scan found `Append(EscapeXml(reason))` and the `EscapeXml` helper; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## LOD Asset Load Guard - 2026-05-20

What was wrong: Generated LOD renderer construction trusted mesh/material path strings. If `AssetDatabase.LoadAssetAtPath` returned null after a replace/save edge case, the forge could publish an empty baked prefab while reporting an output path.

What was done: `CreateLodRenderer` now loads mesh and atlas material handles explicitly and throws before renderer assignment if either asset is missing. Task 10 self-audit text and the architecture doc now record the guard.

Cinematic Cheats used: None added. Existing Dear Lie remains offline atlas UV remap plus deterministic LOD collapse.

Exact Microseconds saved: Runtime 0. Editor adds two null checks per LOD renderer; the gain is false-positive bake prevention, not frame time.

Verification: Static scan found `LOD mesh load failed`, `atlas material load failed`, and the Task 10 audit wording; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Interactive Root Compaction - 2026-05-20

What was wrong: Preserved interactive roots were deduped only by exact transform identity. Parent/child exclusion roots could both be cloned, duplicating interactive controllers under the generated prefab.

What was done: `AddUniqueTransform` now ancestor-compacts the preserve list: existing ancestors suppress child roots, later parent roots remove recorded descendants, and stale null entries are removed.

Cinematic Cheats used: None added. The existing Dear Lie remains static-clutter baking while gameplay-owned objects are preserved as separate roots.

Exact Microseconds saved: Runtime 0. Editor adds bounded `Transform.IsChildOf` checks only on excluded roots; the gain is preventing duplicated interactive hierarchy work.

Verification: Static scan found `transform.IsChildOf(existing)`, `existing.IsChildOf(transform)`, Task 11 self-audit wording, and the architecture note; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.

## Index Destination Guards - 2026-05-20

What was wrong: The UInt16/UInt32 extraction jobs read `Indices[IndexStart + localIndex]` before proving the index-buffer window stayed inside the NativeArray. Source vertex fallback happened after the risky read.

What was done: Both extraction jobs now guard `IndexStart + localIndex`, use fallback source index `-1` when the index window is invalid, and guard destination writes against both output arrays.

Cinematic Cheats used: None added. Existing Dear Lie remains offline atlas UV remap and immutable static geometry output.

Exact Microseconds saved: Runtime 0. Editor adds two unsigned comparisons per extracted vertex; the gain is memory safety under malformed source meshes.

Verification: Static scan found `indexOffset = IndexStart + localIndex`, destination-window guards, Task 06 self-audit wording, and pointer graph wording; forbidden API and trailing whitespace scans passed; self-audit XML parsed. Full build was not relaunched because the unrelated dependency wall remains strike 1/3.
## 2026-05-20 Loop 35 - Finite Bounds Guard

What was wrong:
- `CalculateBounds` seeded min/max from vertex 0 before proving that vertex was finite, allowing a corrupt first vertex to poison generated mesh bounds and prefab culling state.

What was done:
- Bounds reduction now starts from numeric sentinels, accepts only finite vertex positions, and returns a one-meter fallback bounds if every baked position is invalid.
- `HABITAT_CONSOLIDATION_SELF_AUDIT.xml`, the self-audit generator, architecture docs, status, and rationale were updated to record the finite-bounds guard.

Cinematic Cheats used:
- None added in this loop. This is NaN/culling containment for the existing offline Dear Lie pipeline.

Exact Microseconds saved:
- Runtime: 0 us direct; generated assets remain immutable static presentation data.
- Editor: one finite check per vertex in the existing bounds pass. Profiler proof is still PENDING VERIFICATION.

Verification:
- Static evidence scan found `hasFinitePosition`, finite-bounds audit text, and architecture text.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 36 - Submesh Material Fallback

What was wrong:
- Static collection capped source submesh iteration by renderer material-slot count. Meshes with more triangle submeshes than assigned materials could lose geometry during bake.

What was done:
- `CollectSegments` now iterates every source mesh submesh.
- Missing material slots route through the existing null-material atlas fallback instead of dropping geometry.
- Self-audit generator, generated self-audit XML, architecture docs, status, and rationale were updated.

Cinematic Cheats used:
- Existing atlas Dear Lie remains intact: many source material slots collapse to one generated atlas material. Missing material slots degrade visually to fallback atlas data, not runtime material arrays.

Exact Microseconds saved:
- Runtime: 0 us direct; this prevents missing baked geometry.
- Editor: may process additional submeshes that were previously skipped. Profiler proof remains PENDING VERIFICATION.

Verification:
- Static evidence scan found `int subMeshCount = mesh.subMeshCount`, null-material fallback audit text, and architecture text.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 37 - Active Renderer Truth

What was wrong:
- Inactive prefab branches and disabled MeshRenderers could be included by inactive-inclusive traversal in scan, preview, or bake output.

What was done:
- Added shared active-self-chain and renderer-enabled filtering across scan, SceneView preview, and bake collection.
- Updated self-audit and architecture artifacts to state active/enabled visible renderer truth.

Cinematic Cheats used:
- Preserved the offline monolith illusion only for visible static clutter; disabled variants remain authored options, not baked runtime geometry.

Exact Microseconds saved:
- Runtime: 0 us direct.
- Editor: bounded parent-chain check per MeshFilter; profiler proof remains PENDING VERIFICATION.
- MX350 result: avoids invisible variant vertices and texture/material inputs entering generated static assets.

Verification:
- Static evidence scan found `IsActiveInPrefabHierarchy`, active/enabled Task 01/18 wording, and the architecture note.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 38 - Source Extraction Hardening

What was wrong:
- MeshData extraction ignored `SubMeshDescriptor.baseVertex`, so meshes with non-zero base vertices could bake wrong source vertices.
- Malformed triangle submeshes with index counts not divisible by three could feed invalid triangle topology into generated mesh assets.
- Invalid/unwritten segment-map entries could drive `TransformAndAppendVerticesJob` into unsafe segment pointer reads.

What was done:
- Added base-vertex adjustment and 64-bit guarded index offset arithmetic to UInt16/UInt32 extraction jobs.
- Truncated submesh index windows to multiples of three and flags `UnsupportedMesh` on malformed descriptors.
- Added `VertexCount`/`SegmentCount` guards to the transform job and deterministic fallback vertex writes for invalid segment ids.
- Updated self-audit generator, checked-in self-audit XML, architecture docs, status, and rationale.

Cinematic Cheats used:
- Existing Dear Lie remains offline triangle-soup static geometry. Bad source topology degrades to bounded fallback output instead of runtime repair logic.

Exact Microseconds saved:
- Runtime: 0 us direct.
- Editor: adds guarded integer checks per extracted/transformed vertex; profiler proof remains PENDING VERIFICATION.
- Correctness gain: prevents base-vertex mesh corruption and unsafe transform segment dereference.

Verification:
- Static evidence scan found `BaseVertex`, `descriptor.baseVertex`, `triangleIndexCount`, `SegmentCount`, guarded segment-map wording, and architecture/self-audit text.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 39 - Atlas Mask And Compression

What was wrong:
- The third atlas was treated as ARM in prior wording while the generated material bound it to `_MaskMap` and `_MetallicGlossMap`.
- Generated atlas `.asset` textures stayed RGBA32 unless a later import step compressed them.

What was done:
- Replaced the third atlas contract with explicit mask-map semantics: R=metallic, G=occlusion, B=detail mask zero, A=smoothness.
- Stopped copying `_OcclusionMap` into the generated mask texture because it does not own the full four-channel mask payload.
- Added editor compression before texture asset publication: BC5 for normals where supported, BC7 for albedo/mask where supported, DXT5 fallback, and `AtlasCompressionFallback` warnings on unsupported/failed compression.
- Updated generated self-audit source, checked-in self-audit XML, architecture docs, status, and rationale.

Cinematic Cheats used:
- The atlas remains the visual fake: many source materials collapse into one generated material and three generated textures instead of runtime material arrays. The mask payload is now shader-contract-correct rather than pretending ARM data can drive every Lit shader path.

Exact Microseconds saved:
- Runtime: pending profiler proof; expected savings are VRAM and texture bandwidth from compressed generated atlas assets.
- Editor: compression adds cold publication work; acceptable because it buys runtime memory bandwidth.
- MX350 result: lower atlas memory traffic than uncompressed RGBA32, pending Unity import proof.

Verification:
- Static evidence scan found `AtlasChannelMask`, `MaskRgba`, `_MaskMap`/`_MetallicGlossMap`, `CompressAtlasTexture`, `EditorUtility.CompressTexture`, and BC5/BC7/DXT5 fallback.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 40 - Self-Audit Evidence Qualifier

What was wrong:
- The self-audit reconciliation used mandated `PASS` rows without per-task evidence qualifiers. That could be misread as Unity import/profiler/build proof even though `ProofBoundary` still marks those gates pending or externally blocked.

What was done:
- Added `evidence="STATIC_SOURCE_ONLY"` to every generated and checked-in `<Task>` node.
- Kept the mandated PASS/FAIL status shape intact.
- Updated status and rationale.

Cinematic Cheats used:
- None. This is forensic-report hardening only.

Exact Microseconds saved:
- Runtime: 0 us.
- Editor: 0 us. Report-only attribute generation.

Verification:
- Static evidence scan found 20 task nodes and 20 `evidence="STATIC_SOURCE_ONLY"` attributes.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 41 - Honest Interactive Draw-Call Metrics

What was wrong:
- `drawCallsAfter` and scanner `estimatedDrawCallsAfterForge` could report `1` even when preserved interactive renderer roots remained in the generated prefab.

What was done:
- Added `CountPreservedInteractiveRenderers` over existing mesh-filter scratch with active/enabled renderer checks.
- Bake reports now emit `staticDrawCallsAfter=1`, total `drawCallsAfter=1 + preservedInteractiveRenderers`, and `estimatedTotalDrawCallsAfter`.
- Scanner reports now emit `estimatedStaticDrawCallsAfterForge=1` and total `estimatedDrawCallsAfterForge=1 + InteractiveChildRenderers`.
- Updated self-audit, architecture docs, status, and rationale.

Cinematic Cheats used:
- The static monolith Dear Lie remains intact. The report now admits preserved gameplay renderers still cost draw calls instead of hiding them behind the static atlas win.

Exact Microseconds saved:
- Runtime: 0 us direct; this is report truth.
- Editor: bounded filter/root checks after collection; profiler proof remains PENDING VERIFICATION.
- MX350 result: avoids false planning data for post-forge draw-call budget.

Verification:
- Static evidence scan found `CountPreservedInteractiveRenderers`, `staticDrawCallsAfter`, `estimatedTotalDrawCallsAfter`, `estimatedStaticDrawCallsAfterForge`, and updated architecture/self-audit text.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.

## 2026-05-20 Loop 42 - Remap UV Utility Guard

What was wrong:
- `RemapUvCoordinatesJob` is explicitly requested by Task 08 but is not the active bake path because the transform job fuses UV remap to avoid a second pass.
- The standalone utility job lacked local source/output length checks and finite fallback.

What was done:
- Added source/output NativeArray length guards to `RemapUvCoordinatesJob`.
- Added finite fallback for remapped UV output.
- Updated Task 08 self-audit, architecture docs, status, and rationale to state the fused active path plus guarded standalone utility.

Cinematic Cheats used:
- The UV Dear Lie remains offline atlas remapping. Hundreds of source materials still present as one material without runtime UV correction.

Exact Microseconds saved:
- Runtime: 0 us.
- Active editor bake path: unchanged; no redundant UV-only pass scheduled.
- Utility path: two unsigned checks and one finite check per scheduled UV if used later.

Verification:
- Static evidence scan found `RemapUvCoordinatesJob`, `SourceUvs.Length`, `OutputUvs.Length`, remapped UV finite fallback, and fused-path wording.
- Forbidden API scan passed.
- Trailing whitespace scan passed.
- Self-audit XML parsed.
- Dotnet build was not relaunched; the known unrelated Core compile wall remains strike 1/3.
## Loop 43 - LOD Decimation Window Guard

What was wrong: `DecimateTriangleSoupJob` trusted schedule/source/output length agreement while writing into uninitialized LOD buffers. Current caller math is consistent, but the kernel itself did not prove the source triangle window or destination triangle window before reads/writes.

What was done: Added output-window guards, source-window guards, finite source-position checks, finite area fallback, and deterministic fallback triangle writes. Updated Task 09 self-audit and architecture wording to record the guard boundary.

Cinematic Cheats used: Same Dear Lie remains: offline triangle-soup LOD collapse replaces runtime per-prop culling and hierarchy traversal. No physical runtime simulation was introduced.

Exact Microseconds saved: 0 runtime us. Editor cost is a few predicates per decimated triangle; exact profiler proof is pending Unity import/profiler. The gain is correctness: no uninitialized or NaN LOD triangle output under malformed future inputs.

Verification: Static scan found `DecimateTriangleSoupJob`, `WriteFallbackTriangle`, finite triangle validation, source/output length-window proof in the self-audit, and Loop 43 log/rationale entries. Forbidden API scan OK. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Full build not relaunched because the unrelated Core dependency wall remains strike 1/3 and the user explicitly forbids premature rebuild.

## Loop 44 - Atlas/AUP/Forensic Guard

What was wrong: A read-only subagent found three valid defects, and local review found one adjacent texel-kernel guard gap. Static clutter matrix translation still came from float `Matrix4x4` math even though the DTO recorded a double offset; `_MetallicGlossMap` was copied as if it owned URP mask G/B lanes; pre-wrap black-box dumps wrote unrecorded zero rows; atlas rect-color fill trusted rect/color length parity and 32-bit rect extent math.

What was done: Historical Loop 44 replaced matrix translation with an attempted root-relative delta; Loop 49 superseded that with prefab-local hierarchy TRS because editor prefabs do not own true world AUP placement. Mask atlas source copy now accepts `_MaskMap` only; Standard metallic/smoothness uses scalar fallback packing. `InteriorClutterBlackBoxSession.Dump` writes only `_written` rows before wrap. `FillAtlasRectColorsJob` skips missing color records, clamps rect extents in 64-bit, skips empty spans, and bounds texel writes after 64-bit index calculation. Self-audit and architecture docs were updated.

Cinematic Cheats used: The atlas Dear Lie remains one material per room, but Standard metallic maps no longer pretend to be full URP masks. The AUP patch preserves the offline single-mesh trick without runtime precision correction.

Exact Microseconds saved: Runtime 0 us. Editor cost in this historical pass was one root-relative translation computation per segment, one rect/color bounds check per atlas rect, and smaller early failure dump writes. Profiler proof remains pending Unity import.

Verification: Static evidence scan found `BuildRoomLocalMatrix`, the then-existing translation helper, `_MaskMap`-only source copy route, recorded-only pre-wrap dump write, atlas rect/color guard text, self-audit updates, and architecture proof. Forbidden API scan OK. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.

## Loop 45 - Profile-Weighted LOD Residency

What was wrong: `GlobalQualityWeight` affected LOD mesh density and collapse strength, but generated prefab `LODGroup` screen thresholds were fixed at `0.62/0.22/0.055`. Low profiles therefore retained dense room LODs for the same screen residency as high/ultra profiles.

What was done: `CreateGeneratedPrefab` now receives `InteriorAtlasProfile`, calls `ResolveLodThresholds`, and builds LOD0/LOD1/LOD2 thresholds from `math.smoothstep` and `math.lerp`. Low profiles lower dense LOD residency; high/ultra profiles keep richer static room detail longer. Self-audit and architecture docs were updated.

Cinematic Cheats used: The room remains an offline static monolith. The fake is still pre-baked LOD residency and atlas UV remap, not runtime per-prop culling or hierarchy logic.

Exact Microseconds saved: Runtime added 0 us and 0 B/frame. Editor adds one smoothstep plus three lerps per generated prefab. MX350 benefit is expected from earlier dense-LOD shedding in low profiles, but profiler/Frame Debugger proof remains PENDING VERIFICATION.

Verification: Static evidence scan found `CreateGeneratedPrefab(..., profile)`, `ResolveLodThresholds`, `math.smoothstep`, profile-weighted LOD thresholds, updated self-audit, and architecture proof. `rg -P` forbidden API scan OK. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.

## 2026-05-21 Loop 46 - Render Ownership And Normal/Tint Fence

What was wrong: Read-only audit found generated monolith renderers were marked for `BatchingStatic` while the generated atlas material still had instancing enabled. The generated 32-byte vertex layout does not contain tangents, but the generated material enabled tangent-space normal maps. Albedo source texture copy also lost `_BaseColor`/`_Color` tint because the generated material uses white base color.

What was done: Removed `BatchingStatic` from generated root and LOD child flags while keeping `OccludeeStatic | ContributeGI`. Left normal atlas generation as an offline artifact but stopped binding `_BumpMap`/`_NormalMap` and stopped enabling `_NORMALMAP` on generated materials. Added `AtlasTintFallback` and `HasWhiteBaseTint`; albedo texture copy is accepted only when source tint is effectively white, otherwise the prefilled fallback rect color remains the owner.

Cinematic Cheats used: The room still renders as one offline static monolith. The normal atlas is now fenced as dormant visual data instead of pretending tangent-space shading exists; tinted albedo uses a deterministic rect-color fake rather than a wrong untinted texture.

Exact Microseconds saved: Runtime added 0 us and 0 B/frame. Static batching removal avoids potential generated mesh duplication; exact memory and render-thread numbers require Unity import/Frame Debugger proof. Editor adds one white-tint predicate per material.

Verification: Static scan found `AtlasTintFallback`, `HasWhiteBaseTint`, `enableInstancing`, `OccludeeStatic | ContributeGI`, normal-map boundary text, and no `StaticEditorFlags.BatchingStatic` or generated-material `_NORMALMAP`/`_BumpMap`/`_NormalMap` binding. Forbidden API scan OK. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.

## 2026-05-21 Loop 47 - Inverse-Transpose Normal Basis

What was wrong: Vertex normals still used the same `LocalToRoom` columns as positions. That is only valid for rotation/uniform scale; non-uniformly scaled static clutter would bake incorrect normals and carry lighting error into every generated LOD.

What was done: Expanded `InteriorClutterSegment` from 160 to 192 bytes and placed `NormalToRoomC0/C1/C2` at offsets 144/160/176. Added `ResolveNormalToRoomColumns`, which derives signed cofactor columns once per segment and falls back to identity on degenerate/non-finite transforms. `TransformAndAppendVerticesJob` now consumes the precomputed normal basis instead of position columns.

Cinematic Cheats used: This preserves the no-tangent, 32-byte generated vertex layout. Correct vertex lighting is bought with a per-segment cofactor fake instead of a tangent-bearing runtime mesh or per-vertex inverse matrix.

Exact Microseconds saved: Runtime added 0 us and 0 B/frame. Editor memory increases by 32 bytes per static segment. Compared with per-vertex inverse-transpose reconstruction, the vertex loop avoids three cross products, determinant handling, and degeneracy checks per vertex; profiler proof remains pending.

Verification: Static scan found `InteriorClutterSegment` size 192, `NormalToRoomC0/C1/C2`, offset validators at 144/160/176, `ResolveNormalToRoomColumns`, transform-job normal-basis consumption, updated self-audit, and updated architecture text. Forbidden API scan OK. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.

## 2026-05-21 Loop 48 - Burst Attribute Exactness

What was wrong: The SHINOBU_211 job file had all required Burst flags, but each attribute listed `FloatMode` and `FloatPrecision` before `CompileSynchronously`. The compiler accepts named arguments in any order, but the batch mandate demands the exact directive text.

What was done: Reordered the then-existing ten `[BurstCompile]` attributes in `InteriorClutterForgeJobs.cs` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Updated generated self-audit text, checked-in self-audit XML, and architecture docs. Sibling SHINOBU_213 files were intentionally not edited.

Cinematic Cheats used: None new. The existing Dear Lie remains offline atlas/LOD consolidation; this pass hardens compiler-directive proof only.

Exact Microseconds saved: Runtime 0 us. Editor behavior unchanged beyond literal attribute order; no measurable bake-time gain is claimed.

Verification: Static scan found `EXACT_BURST_COUNT=10`, `OLD_ORDER_BURST_ABSENT_OK`, proof text in source/XML/docs, forbidden API scan OK, trailing whitespace OK, self-audit XML parsed, and targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.

## 2026-05-21 Loop 49 - Gauss Audit Integration And Mesh ABI Hardening

What was wrong: Exact-size atlas GPU copy exceptions fell straight to `AtlasCopyFailure`; tinted albedo textures preserved material color only by abandoning source texture detail; LOD jobs waited behind a transform completion barrier; AUP wording implied stronger world-position proof than the editor prefab source can own; the mesh ABI facade still had static managed descriptor-array residue and array-returning validation.

What was done: Added `AtlasDirectCopyFallback`, exact-size copy retry through RT blit, and `TintAtlasTileJob` for offline albedo tint multiplication. LOD1/LOD2 now schedule with `transformHandle` and complete through one combined handle before serialization. Room matrices now compose prefab-local hierarchy TRS and record double3 local offsets without claiming runtime AUP ownership. Mesh layout setup now writes descriptor records into a disposed Temp `NativeArray<VertexAttributeDescriptor>` and validation uses direct Mesh attribute accessors.

Cinematic Cheats used: Texture tint is baked into atlas tiles offline; runtime still sees one material and one albedo atlas instead of per-prop material color logic. LOD collapse remains immutable triangle-soup presentation geometry, not runtime prop simulation.

Exact Microseconds saved: Runtime 0 us added. Potential editor savings: removes one validation-time mesh-attribute array allocation per generated mesh validation and removes the forced transform barrier before LOD scheduling. Exact bake/runtime microseconds remain PENDING VERIFICATION until Unity import/profiler capture. Build not relaunched because the unrelated Core dependency wall remains strike 1/3.

Verification: Static scan found `EXACT_BURST_COUNT=11`, `TintAtlasTileJob`, `AtlasDirectCopyFallback`, `CopyTextureViaTintedBlit`, transformHandle-fed LOD schedules, `JobHandle.CombineDependencies`, prefab-local `BuildRoomLocalMatrix`, Temp `NativeArray<VertexAttributeDescriptor>` layout setup, and direct Mesh attribute validation. Absence scan found no transform completion barrier, obsolete world-translation helper, removed double-conversion helper, removed absolute root-position local, array-returning mesh attribute validation, managed descriptor array, or static descriptor array. Forbidden API scan returned no matches. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF working-copy warnings.

## 2026-05-21 Loop 50 - AUP Proof Drift Cleanup

What was wrong: Historical AUP rationale/log/status wording still carried obsolete helper names and implied a world-position precision proof that Loop 49 intentionally replaced with prefab-local hierarchy TRS. Current proof text also quoted exact removed mesh-validation API names, which could pollute negative scans.

What was done: Marked Loop 44 AUP proof as superseded by Loop 49, removed obsolete helper names from active evidence text, and replaced exact removed API-name quotes with behavioral descriptions.

Cinematic Cheats used: None new. This is forensic truth maintenance for the existing offline atlas/LOD Dear Lie.

Exact Microseconds saved: Runtime 0 us. Documentation-only cleanup.

Verification: Final static gate found `EXACT_BURST_COUNT=11`, Gauss atlas-copy evidence, tint tile evidence, transformHandle-fed LOD schedules, prefab-local `BuildRoomLocalMatrix`, Temp `NativeArray<VertexAttributeDescriptor>` mesh ABI setup, and direct Mesh attribute validation. Forbidden SHINOBU_211 source API scan returned no matches. Obsolete-proof scan returned no stale world-translation helper names, removed double-conversion helper names, removed absolute root-position local names, array-returning mesh validation names, or managed descriptor-array signatures across SHINOBU_211 source/docs/log artifacts. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3 and this pass was static/editor proof hardening.

## 2026-05-21 Loop 51 - Tinted Tile Mip Payload Trim

What was wrong: The tint-aware atlas copy created a temporary RGBA32 tile with a mip chain. `GetRawTextureData<uint>()` exposes the full mip payload, so `TintAtlasTileJob` could process unused lower mips even though the subsequent atlas copy consumes only mip 0.

What was done: The temporary tint tile is now top-mip only and applies without mip regeneration. Generated self-audit text, checked-in self-audit XML, architecture notes, status, and rationale were updated to state that the tint path multiplies only the copied atlas payload.

Cinematic Cheats used: Same offline material-color Dear Lie. Source texture detail and material tint are baked into the atlas tile once; runtime still uses one generated atlas material.

Exact Microseconds saved: Runtime 0 us and 0 B/frame. Editor tint work drops from roughly 1.333x top-mip texel payload to 1.0x per tinted copied tile. Exact profiler proof remains pending Unity import/profiler.

Verification: Static gate found top-mip-only tinted tile creation, updated tint proof text, and `EXACT_BURST_COUNT=11`. Forbidden SHINOBU_211 source API scan returned no matches. Obsolete-proof scan returned no matches. Trailing whitespace scan OK. Self-audit XML parsed. Targeted `git diff --check` emitted only LF-to-CRLF warnings. Build not relaunched because the unrelated Core compile wall remains strike 1/3.
