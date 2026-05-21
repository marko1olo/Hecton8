# Rationale_SHINOBU_211

Evidence state: PENDING UNITY/BUILD VERIFICATION. SHINOBU_211 static source/doc gates passed; Runtime/Unity proof absent.

## Initial Mandate Selection

Problem: SHINOBU_211 must build an offline Editor mesh/atlas/LOD pipeline without creating runtime hierarchy, draw-call, or GC debt.
Solution: Use Editor-only tools, Burst-compatible unmanaged DTOs, explicit mesh vertex layout, deterministic atlas/LOD generation, and JSON reports.
Rejected Alternatives: Runtime component consolidation, Static Batching alone, Mesh.CombineMeshes with managed arrays, multiple submeshes/materials, and global authority expansion.
Scalability potential: Low uses baked LOD2 and compact atlases; middle uses LOD1 with 4K atlas profiles; high uses LOD0 with stronger material fidelity; ultra keeps visual-overkill atlas/LOD0 data without runtime transform bloat.
Hardware Impact: Expected low-end i3/MX350 gain is lower main-thread hierarchy traversal and SetPass cost; numeric microsecond proof is pending Unity profiler capture.

Relevant mandates read:
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- DATA_Runtime_Struct_Layout_ARM64
- MATH_AUP_Determinism_Sync
- REND_URP_Graphics_HotPath_Optimization_HLOD
- TOOL_Designer_Facades_CSV_Binary_Bridge
- STRM_Async_Asset_Upload_Texture_Settings
- Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md

## Decision 00 - Scope Boundary

Problem: The assignment targets habitat/interior static clutter but touches render, asset, editor, AUP, and netcode boundaries.
Solution: Keep all bake logic under Editor-only source paths and generated assets under Assets/_Project/BakedGeometry/HabitatInteriors. Runtime output is plain Mesh/Material/LOD assets; interactive children remain owned by their existing systems.
Rejected Alternatives: Add runtime managers, new GlobalRegistry services, new EventBus lanes, or Rigidbody/MonoBehaviour controllers on generated static geometry. Standard Unity hierarchy retention is rejected because it leaves transform traversal and draw-call pressure.
Scalability potential: Low gets the same authored detail through one renderer and lower LODs; ultra can retain denser LOD0 and larger atlas profiles.
Hardware Impact: Removes per-room static child Transform cost from runtime; exact saved microseconds are pending profiler/Frame Debugger evidence.

## Decision 01 - Actual Prefab Root

Problem: Assigned root `Assets/_Project/Prefabs/Habitat/` is absent on disk; blindly hard-failing would leave no scanner path.
Solution: Keep Habitat as the first configured root, then fall back to `Assets/_Project/Prefabs/Construction/Final` for the current project reality. Reports flag `MissingHabitatRoot`.
Rejected Alternatives: Invent a Habitat folder, move prefabs, or edit prefab YAML. Standard Unity manual hierarchy review was rejected because it produces no repeatable report.
Scalability potential: Low/middle/high/ultra all use the same scan path; only atlas and LOD profiles scale.
Hardware Impact: No runtime impact. Editor scan estimate is hundreds of microseconds per small prefab plus Unity asset load.

## Decision 02 - One Material Atlas

Problem: Multiple materials per room preserve SetPass pressure and violate the one draw-call target.
Solution: Generate one atlas material and three atlas textures (albedo, normal, mask) per room. Mesh UVs are remapped in the transform job through per-segment atlas rects; the third texture follows URP/Standard mask semantics, not an incompatible ARM payload.
Rejected Alternatives: Preserve submeshes, use Static Batching, or rely on SRP Batcher alone. Those still keep material state changes.
Scalability potential: Low uses 4K atlas / 512 tiles; middle 4K / 1024; high 8K / 1024; ultra 8K / 2048.
Hardware Impact: Expected MX350 gain is SetPass reduction from N static clutter materials to 1; exact microseconds pending Frame Debugger/profiler.

## Decision 03 - 32-Byte Vertex Contract

Problem: Prompt requested ARM64-safe interleaved vertex buffers; full position+normal+tangent+uv+color cannot fit honestly into 32 bytes with Float32 attributes.
Solution: Use an explicit 32-byte layout: position float3 at 0, normal float3 at 12, uv0 float2 at 24. Source DTO still carries tangent/color for future expansion, but runtime mesh stride remains 32 bytes.
Rejected Alternatives: Fake a 32-byte tangent/color layout, use implicit Unity vertex streams, or create 48/52-byte payloads without mandate evidence.
Scalability potential: Low benefits from smaller vertex bandwidth; ultra can spend saved draw-call/transform budget on higher LOD retention and atlas resolution.
Hardware Impact: 32-byte stride avoids misaligned ARM64 loads and reduces vertex bandwidth versus 48+ byte layouts; exact gain pending GPU capture.

## Decision 04 - LOD Decimation Method

Problem: Full edge-collapse/QEM decimation is broad and high-risk inside this batch; runtime culling of child props is explicitly the wrong target.
Solution: Offline deterministic triangle-soup decimation with small-detail collapse toward triangle centers for LOD2. Continuous `GlobalQualityWeight` controls retention/collapse.
Rejected Alternatives: Runtime procedural culling, binary low/high output switch, or waiting for a third-party decimator package.
Scalability potential: Low uses aggressive LOD2 collapse; middle/high retain more triangles; ultra keeps dense LOD0 and higher LOD ratios.
Hardware Impact: Reduces distant room vertex submission; exact microseconds pending generated mesh stats and profiler.

## Decision 05 - Asset Serialization Shape

Problem: The output must be one draw-call static room section while preserving runtime purity.
Solution: Write `Mesh` assets with `SetVertexBufferParams` / `SetVertexBufferData`, one generated material, three atlas textures, and a generated prefab with an `LODGroup` containing one renderer per LOD.
Rejected Alternatives: Runtime mesh generation, adding a bake manager MonoBehaviour, or mutating source prefabs. Standard Unity `Mesh.CombineMeshes` was rejected because it hides data layout and material/submesh retention.
Scalability potential: Low profile keeps smaller atlas tiles and lower LOD ratios; ultra profile raises atlas tiles and LOD retention without new runtime hierarchy.
Hardware Impact: Expected low-end gain is fewer renderers, fewer material state changes, and lower transform traversal; exact saved microseconds pending Frame Debugger/profiler.

## Decision 06 - Texture Copy Fallback

Problem: Editor atlas generation must handle non-readable source textures without blocking the bake.
Solution: Fill atlas rects with deterministic material colors/default normal/default mask, then attempt `Graphics.CopyTexture`; mismatched source/tile sizes go through temporary RT blit plus copy and raise `AtlasScaledTexture`.
Rejected Alternatives: `Texture2D.PackTextures`, CPU `GetPixels` dependency on readable import settings, upper-left cropping of oversized textures, or runtime texture compression.
Scalability potential: Low gets stable fallback colors if source textures are not copy-compatible; high/ultra benefit when GPU copy succeeds on richer source maps.
Hardware Impact: Runtime impact is lower texture binding count; editor-copy microseconds pending Unity measurement.

## Decision 07 - Interactive Preservation Fence

Problem: Doors, terminals, fabricators, inventory/power objects, and Rigidbody/Joints must not be baked into static mesh truth.
Solution: Exclude objects by configured tag/layer plus component-name tripwires, then copy excluded transforms under `INTERACTIVE_PRESERVED` in the generated prefab.
Rejected Alternatives: Bake all renderers, depend on concrete gameplay classes, or add a new global registry for interactive handoff. Standard direct component dependencies are too brittle with 20+ concurrent agents.
Scalability potential: Low to ultra preserve the same gameplay truth; only static visual density changes.
Hardware Impact: Keeps interaction runtime behavior separate while removing static clutter transforms; exact microseconds pending scene profiler.

## Decision 08 - Black Box Scope

Problem: A failed bake or NaN geometry path must leave forensic evidence.
Solution: Add a 300-entry `NativeArray<InteriorClutterTelemetryEntry>` editor black-box ring and dump `Docs/AgentLogs/Dump_SHINOBU_211.bin` on bake exceptions.
Rejected Alternatives: Rely on Console logs or chat report. Logs allocate strings and disappear; chat is not accepted as evidence.
Scalability potential: Low/editor machines keep compact 64-byte entries; high/ultra do not need larger truth payloads for this tool.
Hardware Impact: Editor-only persistent allocation is 19.2 KB; runtime impact is 0.

## Decision 09 - Designer Facade

Problem: Technical artists need a repeatable bake path and pre-bake visibility, not scattered menu commands.
Solution: Implement a UI Toolkit window with folder input, profile dropdown, exclusion fields, scan, preview, and bake controls.
Rejected Alternatives: Menu-only execution, raw YAML mutation, or requiring designers to edit generated binary/mesh data.
Scalability potential: Low/middle/high/ultra profiles are selected through CSV, not hardcoded binary quality switches.
Hardware Impact: Editor-only UI. Runtime impact is 0.

## Decision 10 - Verification Boundary

Problem: CPU was initially above the 50% compile gate, and the project explicitly forbids launching dotnet/csc builds under load; after the gate opened, the solution build failed on unrelated missing contracts outside SHINOBU_211.
Solution: Defer while CPU was high, then run one capped `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` when CPU reached 36.4% and no dotnet/csc was active. Record the compile wall as unrelated dependency errors and do not touch foreign domains.
Rejected Alternatives: Launching build under load, editing core/power/construction/audio/world contracts outside domain, or claiming compile success from static source. All violate project rules.
Scalability potential: No runtime effect.
Hardware Impact: Avoided build contention while CPU was saturated; one capped build attempt consumed 112 seconds and failed before domain verification could be proven.

## Decision 11 - Ancestor Interactive Filter

Problem: A static mesh child under an interactive parent could be baked if the child itself had no tag/component tripwire.
Solution: Make the exclusion filter walk parent transforms and return the first excluded Transform root. The bake path preserves that root once under `INTERACTIVE_PRESERVED`.
Rejected Alternatives: Only checking the MeshFilter GameObject, or copying each child renderer independently. That breaks controller ownership and can split a door/terminal/fabricator hierarchy.
Scalability potential: Low/middle/high/ultra keep the same gameplay exclusion contract; static clutter density and atlas/LOD choice still scale through CSV quality weight.
Hardware Impact: Runtime cost remains 0; editor scan adds bounded ancestor checks per MeshFilter. Expected low-end gain is unchanged because preserved interactives are intentionally not part of the static monolith.

## Decision 12 - Compile-Wall Isolation

Problem: SHINOBU_211 files originally lived under the broad `Hecton8.Editor` assembly with unrelated package/domain references.
Solution: Add `Hecton8.HabitatInteriorClutterForge.Editor.asmdef` in the forge folder with only Unity Burst, Collections, Jobs, and Mathematics references; built-in UnityEditor/UnityEngine APIs remain editor-only.
Rejected Alternatives: Keeping the broad editor assembly, or adding runtime/Core/World/Graphics sibling dependencies. Standard broad editor asmdefs increase compile blast radius for unrelated agents.
Scalability potential: Runtime output remains identical from low to ultra; editor iteration cost is narrowed.
Hardware Impact: Runtime 0. Editor compile/import wall reduction pending Unity import proof.

## Decision 13 - Native Extraction and Atlas Staging

Problem: The previous extraction path used managed Mesh vertex lists and atlas base fill used managed `Color32[]` plus per-pixel calls.
Solution: Source meshes now read through `Mesh.AcquireReadOnlyMeshData`, byte offsets, strides, and Burst UInt16/UInt32 extraction jobs. Atlas base colors now stage through `NativeArray<uint>` and Burst fill jobs before one `Texture2D.SetPixelData`.
Rejected Alternatives: `Mesh.GetVertices(List<T>)`, `Texture2D.SetPixel`, `Texture2D.SetPixels32`, and CPU-readable texture dependence. These are editor-cold, but they are still slower and dirtier than native staging.
Scalability potential: Low profiles use smaller tile caps and LOD collapse; high/ultra profiles can spend editor time on 8K atlases without managed heap spikes.
Hardware Impact: Runtime 0. Editor heap pressure reduced; exact bake-time microseconds pending profiler.

## Decision 14 - Black-Box Ownership

Problem: The first pass used an InitializeOnLoad persistent NativeArray for editor black-box telemetry.
Solution: Replace it with a per-bake `InteriorClutterBlackBoxSession` using TempJob memory, disposed inside the bake transaction, and dumped on exceptions.
Rejected Alternatives: Persistent editor NativeArray and managed byte[] dump staging. Persistent private native ownership violates the new H-PHI reading even though the tool is editor-only.
Scalability potential: Same telemetry across low/middle/high/ultra bakes; memory exists only while the bake is active.
Hardware Impact: Removes 19.2 KB persistent editor native memory outside active forge execution; runtime 0.

## Decision 15 - Forensic Self-Audit Artifact

Problem: A one-line JSON self-audit is insufficient under the ultra-polish mandate.
Solution: Generate `Docs/Reports/HABITAT_CONSOLIDATION_SELF_AUDIT.xml` with 20-task reconciliation, struct layout, scalability curve, H-PHI/vault boundary, job dependency graph, compile guard, Dear Lie proof, and proof boundary.
Rejected Alternatives: Chat-only report or embedding a giant escaped XML blob inside JSON. Disk artifact is easier to inspect and does not corrupt JSON readability.
Scalability potential: Documents continuous quality-weight behavior for low, middle, high, and ultra profiles without binary hardware switches.
Hardware Impact: Editor-only report write; runtime 0.

## Decision 16 - Material UV Scale/Offset Preservation

Problem: The atlas UV remap preserved only original UV fractional wrap plus packed rect, so props using `_BaseMap` or `_MainTex` tiling/offset could visually drift after consolidation.
Solution: Add `MaterialUvScaleOffset` to `InteriorClutterSegment` at offset 80 and apply `uv * scale + offset` inside the Burst remap before wrapping into the atlas rect.
Rejected Alternatives: Per-material runtime UV correction, managed side-table lookups in the transform loop, or ignoring material tiling as an acceptable atlas artifact. Standard Unity material arrays were rejected because they reintroduce SetPass pressure.
Scalability potential: Low keeps exact authored tiling while still using small tiles/LOD2; middle/high/ultra can raise tile caps without changing runtime render path.
Hardware Impact: Editor-only segment staging grows from 128B to 160B. Runtime remains one material/renderer per visible LOD; expected low-end i3/MX350 gain from SetPass collapse is unchanged.

## Decision 17 - Scaled Atlas Copy

Problem: Copying only `min(source, tile)` pixels cropped oversized source textures and left fallback-color borders for undersized sources.
Solution: Use direct `Graphics.CopyTexture` for exact-size source/tile matches and temporary `RenderTexture` blit followed by `Graphics.CopyTexture` when scaling is required; emit `AtlasScaledTexture` in reports.
Rejected Alternatives: CPU-readable `GetPixels`, `Texture2D.PackTextures`, and silent crop. The first two create import and allocation fragility; crop destroys visual fidelity.
Scalability potential: Low profiles scale more sources into 512 tiles; high/ultra profiles preserve more source resolution through larger tile caps.
Hardware Impact: Editor-only GPU copy cost. Runtime reduces texture bindings to one atlas set and keeps draw call target intact.

## Decision 18 - Editor Allocation Surface Reduction

Problem: The editor bake graph still had avoidable managed allocation residue: class-per-render-segment wrappers, an atlas container class, `sharedMaterials` arrays, `GetComponentsInChildren` arrays, and component arrays in exclusion checks.
Solution: Convert immutable segment and atlas containers to readonly structs, use Unity list-fill APIs for component/material traversal, and keep scratch lists local or static in editor-only surfaces.
Rejected Alternatives: Keeping object wrappers because the path is editor-only, or pushing these records into runtime services. The first leaves bake-time heap churn; the second violates the domain boundary.
Scalability potential: Low/middle/high/ultra behavior is unchanged; larger ultra prefabs benefit more because segment/material traversal allocation scales with renderer count.
Hardware Impact: Runtime 0. Editor bake removes one managed object per static segment, one atlas container object, one Material[] per renderer traversal, and MeshFilter[] arrays per scan/preview traversal.

## Decision 19 - Branchless Safety And LOD Job Parallelism

Problem: Transform normalization copied the segment DTO per vertex and used ternary rsqrt guards; LOD2 decimation was serialized behind LOD1 despite both reading LOD0 and writing separate buffers. The segment DTO is now 192 bytes after the normal-basis expansion, so avoiding copies matters more.
Solution: Read source/segment DTOs through `ref readonly`, guard rsqrt with `math.max`, select fallback normals with `math.select`, always lerp triangle collapse by a continuous scalar, and join LOD1/LOD2 jobs through `JobHandle.CombineDependencies`.
Rejected Alternatives: Copying segment DTO per vertex, retaining branchy collapse guards, or serializing independent LOD jobs. These choices waste editor bake CPU and reduce Burst's ability to keep predictable vectorized math.
Scalability potential: At low quality, collapse scalar increases and cheapens LOD2; at high/ultra, collapse drops while LOD jobs still run independently.
Hardware Impact: Runtime 0. Editor transform avoids 192-byte per-vertex segment copies; LOD bake can use worker parallelism instead of artificial dependency chaining.

## Decision 20 - Scratch-State Containment

Problem: The exclusion filter still owned a hidden static `List<Component>` scratch buffer, and preview traversal owned a static mesh-filter scratch list. This was editor-only, but it made the filter stateful and could be misread as a global mutable owner.
Solution: Pass component scratch explicitly from scan, bake collection, and preview transactions into `InteriorClutterExcludeFilter`; make preview mesh-filter scratch local to the preview build. Persistent static state remains only for the SceneView overlay bounds that must survive until repaint.
Rejected Alternatives: Keep static scratch because it avoids one editor allocation, or allocate inside each filter method. Static scratch hides ownership; per-call allocation would scale with hierarchy depth. Transaction-owned scratch is the narrower DOD boundary.
Scalability potential: Low/middle/high/ultra output is unchanged. Large ultra prefabs get deterministic traversal ownership without hidden cross-preview state.
Hardware Impact: Runtime 0. Editor removes global mutable scratch contention; preview pays one cold local list allocation per preview call.

## Decision 21 - Atlas Serialization Fidelity

Problem: `Graphics.CopyTexture` and scaled RT blits write through GPU texture state. Saving a generated `Texture2D` asset immediately after GPU copies risks serializing the original CPU fallback texels if the CPU-side texture data is not synchronized. Normal and mask atlases were also created in sRGB space, which is wrong for data textures.
Solution: After any source texture copy succeeds, blit each atlas into a temporary RT, call one editor-only `ReadPixels`, and `Apply` before `AssetDatabase.CreateAsset`. Albedo atlases are created sRGB; normal and mask atlases are created linear, and temporary RTs use matching `RenderTextureReadWrite`. The generated material enables normal/mask/metallic keywords.
Rejected Alternatives: Trusting GPU-only copy state, using CPU-readable `GetPixels`, or replacing `.asset` textures with PNG encode paths. Trusting GPU state is serialization-fragile; `GetPixels` depends on import readability; PNG encode would introduce managed byte payloads and importer churn outside this domain.
Scalability potential: Low profiles still use small tile caps and can fall back to solid colors. High/ultra profiles now preserve copied source texture fidelity in saved assets, not just in transient GPU memory.
Hardware Impact: Runtime 0. Editor pays one bounded GPU readback per atlas channel only when source copies happen; the cost buys deterministic asset correctness.

## Decision 22 - Source Mesh Safety

Problem: The extraction jobs read vertex attributes through unsafe pointers. `MeshData` attribute presence alone does not prove that an attribute byte window fits inside the declared vertex stride, and corrupted source indices could address outside the vertex buffer.
Solution: Validate every required/optional source attribute window against offset, byte width, and stride before scheduling extraction. Position layout failure rejects the source mesh; malformed optional normal/tangent/uv streams fall back to deterministic defaults. Extraction jobs now carry `SourceVertexCount` and check every UInt16/UInt32 source index before reading through pointers.
Rejected Alternatives: Trust Unity-imported meshes blindly, or add managed mesh API fallback. Blind trust can turn bad prefab data into unsafe reads; managed fallback would reintroduce the allocation-heavy path the forge exists to remove.
Scalability potential: Low/middle/high/ultra output is unchanged for valid meshes. Bad optional channels degrade to stable visual defaults instead of aborting whole-folder bakes.
Hardware Impact: Runtime 0. Editor extraction pays one unsigned bounds compare per source vertex and a cold stride check per mesh, which is acceptable against the risk of undefined unsafe reads.

## Decision 23 - Atlas Overflow Containment

Problem: Overflow material rects used the full atlas as their fallback. The Burst fill job could repaint the entire atlas with an overflow material color, and a later source-copy could scale an unfitted texture over all packed regions.
Solution: Reserve a 16px fallback tile before guillotine packing. Materials that do not fit are assigned only that tile and flagged `MaterialOverflow`; source-copy is skipped for flagged rects.
Rejected Alternatives: Leave overflow as full-atlas fallback, dynamically grow the atlas, or keep packing until failure. Full-atlas fallback corrupts valid materials; atlas growth violates the selected VRAM profile; hard failure blocks batch processing for one bad room.
Scalability potential: Low profiles are more likely to overflow and now degrade predictably to a tiny fallback tile. High/ultra profiles keep larger tiles and should hit the fallback less often, preserving visual-overkill source copies.
Hardware Impact: Runtime 0. Editor sacrifices 256 texels per atlas to prevent catastrophic overwrite. Overflow source copies are skipped, saving editor GPU copy work for failed materials.

## Decision 24 - Asset Publication Flush

Problem: Generated meshes, textures, and materials used `EditorUtility.CopySerialized` when replacing existing assets, but the replaced asset object was not explicitly marked dirty. Prefab publication also ignored the `SaveAsPrefabAsset` success result, and the mock benchmark could return a destroyed transient mesh after replacing an existing mock asset.
Solution: Mark replaced and newly created mesh/material/texture assets dirty, check `SaveAsPrefabAsset(..., out bool success)` before reporting a generated prefab path, flush `AssetDatabase.SaveAssets()` once after successful prefab or mock mesh publication, and reload the persisted mock mesh before returning it.
Rejected Alternatives: Raw YAML `.asset` replacement, deleting/recreating assets, or wrapping the whole bake in `AssetDatabase.StartAssetEditing`. Raw file replacement risks GUID/import corruption; delete/recreate churns `.meta` identity; broad asset-edit suppression can make immediately loaded mesh/material references unavailable during prefab construction.
Scalability potential: Low/middle/high/ultra output is unchanged. The improvement hardens deterministic editor publication for every atlas profile without adding runtime state or draw calls.
Hardware Impact: Runtime 0. Editor pays one explicit asset flush per completed prefab/mock publication; this buys deterministic persistence of generated monoliths and prevents stale asset data in repeated bake iterations.

## Decision 25 - Fixed Texture Property Lookup

Problem: Atlas source texture discovery used a `params string[]` helper. Every albedo/normal/mask lookup created a managed string-array wrapper inside the material loop, which is avoidable heap churn during large room batch bakes.
Solution: Replace the `params` helper with fixed two-property and three-property overloads. The exact shader property search order is preserved for `_BaseMap/_MainTex`, normal aliases, and mask/metallic/occlusion aliases.
Rejected Alternatives: Keep the `params` helper because the path is editor-only, or replace material discovery with reflection. The first is unnecessary allocation debt; the second is worse for IL2CPP/AOT discipline and not needed for fixed URP/Standard property aliases.
Scalability potential: Low profiles with many tiny materials avoid repeated allocation while packing smaller tiles. High/ultra profiles keep richer atlas source lookup without heap spikes from repeated material-channel scans.
Hardware Impact: Runtime 0. Editor removes one small managed array allocation per material/channel texture lookup; exact microseconds remain pending Unity profiler.

## Decision 26 - Fixed Exclusion Filter Tokens

Problem: The interactive-preservation filter parsed tags/layers through `string.Split`, stored `string[]`, trimmed each token, and matched layers by repeatedly resolving layer names. This is cold editor code, but it is still avoidable allocation residue inside the high-volume scanner/bake path.
Solution: Store tag FNV hashes and layer indices in fixed unmanaged lists. Parse the UI strings with a small byte-level token scanner, hash tag tokens without `ToString`, resolve layer names once during parse, and match layers by integer equality during hierarchy traversal.
Rejected Alternatives: Keep `string.Split` because the path is editor-only, or depend on concrete gameplay interactable types. The first preserves unnecessary heap churn; the second violates the cross-domain boundary.
Scalability potential: Low/middle/high/ultra output is unchanged. Large prefab scans now avoid tag/layer filter array churn while preserving designer-editable UI fields.
Hardware Impact: Runtime 0. Editor removes `string.Split` token arrays and per-check layer-name comparisons from the exclusion filter; exact bake-time microseconds remain pending Unity profiler.

## Decision 27 - Deterministic Black-Box Failure Evidence

Problem: The per-bake black-box ring used uninitialized TempJob memory and only wrote entries for successful metrics. A direct single-prefab bake exception could dispose the ring without dumping, and unused ring slots could contain meaningless bytes.
Solution: Reset the 300-entry ring immediately after allocation through index writes, replace Unity `Time.frameCount` with a local deterministic frame counter, add `RecordFailure` for exception paths, add a `BakeException` warning bit, dump from both folder-bake and direct-prefab catch paths, and include written-entry count in the reason sidecar.
Rejected Alternatives: Use `UnsafeUtility.MemClear`, keep uninitialized unused entries, or rely on Console strings. `MemClear` violates the explicit zero-init bypass intent; uninitialized slots weaken forensic dumps; Console strings are not a binary proof artifact.
Scalability potential: Low/middle/high/ultra output is unchanged. The fixed 300-entry ring remains compact and deterministic across all atlas profiles.
Hardware Impact: Runtime 0. Editor pays 300 fixed 64-byte writes per bake session to ensure dumps are deterministic; exact microseconds remain pending Unity profiler.

## Decision 28 - Chronological Black-Box Dump Order

Problem: A circular ring written in physical memory order is harder to inspect after wrap because the oldest retained frame may sit in the middle of the file.
Solution: Dump pre-wrap rings as fixed 300-entry zero-padded order and post-wrap rings as two native span writes: cursor-to-end, then zero-to-cursor. The binary file remains fixed size while preserving chronological read order for the last retained entries.
Rejected Alternatives: Allocate a managed reorder buffer or keep raw physical ring order. A managed buffer is unnecessary allocation; raw order slows failure autopsy.
Scalability potential: Low/middle/high/ultra output is unchanged. The file format remains one fixed-size telemetry stream for all profiles.
Hardware Impact: Runtime 0. Editor exception dump writes the same byte count; only the order of two direct span writes changes.

## Decision 29 - Scanner Material Scratch Reuse

Problem: `Hierarchy_Bloat_Scanner.ScanProject` created a new material uniqueness list inside the prefab loop. The path is editor-only, but batch scans can touch hundreds of prefabs and should not allocate a list per asset.
Solution: Move the `List<Material>` scratch to the scan transaction scope and call `Clear()` for each prefab before collecting unique materials.
Rejected Alternatives: Keep the allocation because the scanner is cold, or replace material uniqueness with a hash table. The first leaves avoidable heap churn; the second adds more machinery than the small material-count set needs.
Scalability potential: Low/middle/high/ultra output is unchanged. Larger ultra-quality habitat libraries benefit most because scan allocation no longer scales by one material list per prefab.
Hardware Impact: Runtime 0. Editor removes one managed `List<Material>` allocation per scanned prefab; exact microseconds remain pending Unity profiler.

## Decision 30 - Batch Bake Scratch Reuse

Problem: Folder baking still allocated static segment, preserved interactive, material, mesh-filter, shared-material, and component-probe lists for every prefab. This is editor-only, but a 100-room bake multiplies the same scratch allocations 100 times.
Solution: Add `InteriorClutterBakeScratch` as an explicit transaction-owned struct, create it once for `BakeFolder`, pass it by ref into private `BakePrefab`, and clear it before each prefab plus after `PrefabUtility.UnloadPrefabContents`.
Rejected Alternatives: Keep per-prefab list allocation, or move scratch into static globals. Per-prefab allocation leaves avoidable heap churn; static globals hide ownership and can retain destroyed prefab references between bake calls.
Scalability potential: Low/middle/high/ultra output is unchanged. Ultra habitat-library bakes get the biggest editor-memory win because scratch allocation no longer scales with prefab count.
Hardware Impact: Runtime 0. Editor folder bake removes six managed list allocations per prefab; exact microseconds remain pending Unity profiler.

## Decision 31 - Safe Asset Token Fast Path

Problem: `SanitizeToken` cloned every prefab token into a `char[]` even when the source name already contained only asset-safe characters.
Solution: Scan the input first; return the original string when every character is safe. Only invalid names allocate a short `StringBuilder` and final sanitized string.
Rejected Alternatives: Keep `ToCharArray`, or introduce a pooled mutable token buffer. The first allocates unnecessarily; the second adds lifecycle complexity for an editor-cold asset-name path.
Scalability potential: Low/middle/high/ultra output is unchanged. Large batch bakes with already-safe prefab names avoid one extra char-array allocation per generated token.
Hardware Impact: Runtime 0. Editor avoids one char-array allocation per safe prefab token; exact microseconds remain pending Unity profiler.

## Decision 32 - Asset Folder Path Walk

Problem: `EnsureAssetFolder` normalized paths and split them with `Split('/')`, allocating a path-segment array during generated mesh/material/texture folder checks.
Solution: Keep one conditional slash normalization inside the helper and walk separator ranges directly, creating each missing folder from substring pairs without a `string[]` segment array.
Rejected Alternatives: Keep `Split('/')`, or cache global folder state. The split path is unnecessary allocation; global folder caches risk stale editor state after external asset moves.
Scalability potential: Low/middle/high/ultra output is unchanged. Batch bakes creating many output roots avoid repeated segment-array allocation on folder creation paths.
Hardware Impact: Runtime 0. Editor avoids one string-array allocation per missing asset-folder creation path; exact microseconds remain pending Unity profiler.

## Decision 33 - LOD Renderer Handle Return

Problem: Generated prefab construction had no reason to reacquire renderers after creating the LOD child objects; any `GetComponent<Renderer>()` call in that path is avoidable editor work and weakens the no-hidden-lookup discipline.
Solution: Keep `CreateLodRenderer` returning the `MeshRenderer` handle it already creates, and pass those handles directly into `LODGroup.SetLODs`.
Rejected Alternatives: Keep post-creation `GetComponent<Renderer>()`, or store child `GameObject`s and query components later. The first performs redundant component lookup; the second preserves object-first flow where the renderer is the data needed by the LOD group.
Scalability potential: Low/middle/high/ultra output is unchanged. Large folder bakes that generate many room prefabs avoid repeated component lookups while preserving the same LOD thresholds and atlas material route.
Hardware Impact: Runtime 0. Editor avoids three component lookups per generated prefab; exact microseconds remain pending Unity profiler.

## Decision 34 - TryGet Renderer Traversal

Problem: Scan, preview, and bake traversal used `GetComponent<MeshRenderer>()` after fetching each `MeshFilter`. The path is editor-only, but it is high-volume and should use explicit success probes instead of object-return lookup followed by null checks.
Solution: Replace those calls with `TryGetComponent(out MeshRenderer renderer)` and keep the existing shared-material and bounds logic unchanged.
Rejected Alternatives: Keep `GetComponent<MeshRenderer>()`, or traverse renderers first and then search for matching filters. The first preserves weaker lookup style; the second would require broader ownership changes and risk behavior drift.
Scalability potential: Low/middle/high/ultra output is unchanged. Larger ultra-prefab scans and bakes benefit most because renderer probes scale with static clutter count.
Hardware Impact: Runtime 0. Editor removes the object-return `GetComponent<MeshRenderer>()` pattern from three traversal loops; exact microseconds remain pending Unity profiler.

## Decision 35 - Atlas Shader Fail-Fast

Problem: Atlas material creation nested fallback shader resolution inside the `Material` constructor argument. If URP Lit, Standard, and Hidden/InternalErrorShader are all unavailable in a broken/headless editor import, the constructor path could receive null instead of producing a clear forge-owned failure.
Solution: Resolve URP Lit, Standard, then Hidden/InternalErrorShader into one `Shader` variable and throw `InvalidOperationException` before constructing the material if no shader resolves.
Rejected Alternatives: Keep the nested constructor fallback, or create a runtime shader dependency. The first hides the failure point; the second violates the offline editor-only boundary.
Scalability potential: Low/middle/high/ultra output is unchanged. The guard protects all atlas profiles equally and prevents corrupted generated materials from entering any quality tier.
Hardware Impact: Runtime 0. Editor adds two null checks and a clear failure path only during atlas material creation; exact microseconds are irrelevant against asset correctness.

## Decision 36 - Atlas Copy Failure Flag

Problem: `TryCopyTexture` caught GPU copy/blit exceptions and returned false, making copy-path failure indistinguishable from a material with no source texture.
Solution: Add `AtlasCopyFailure` as a warning flag and thread a `copyFailure` boolean through the atlas copy attempts. Null source textures still return false without setting the failure bit; exceptions set the bit.
Rejected Alternatives: Keep a generic solid-color fallback flag, or throw on every texture-copy exception. The first loses forensic detail; the second blocks a whole room bake when a single texture import setting can safely degrade to fallback color.
Scalability potential: Low profiles often rely on fallback colors and should not be mislabeled as copy failures. High/ultra profiles now surface actual GPU copy failures when richer source maps fail to transfer.
Hardware Impact: Runtime 0. Editor adds one boolean flag write on exception paths only; report correctness improves without changing draw-call output.

## Decision 37 - Column-Wise Transform Kernel

Problem: `TransformAndAppendVerticesJob` built a temporary `float4` for every position transform and a temporary `float3x3` normal matrix for every vertex, even though the segment's `LocalToRoom` columns already contain the data needed for both operations.
Solution: Compute transformed position and normal directly from `LocalToRoom.c0/c1/c2/c3` columns and source xyz lanes, then keep the existing NaN guards and UV remap.
Rejected Alternatives: Keep `math.mul` plus per-vertex `float3x3`, or introduce platform-specific SSE-only intrinsics without ARM64 proof. The first does extra temporary construction; the second would violate the Quest/desktop parity target without a NEON path.
Scalability potential: Low profiles and weak editor machines benefit from cheaper offline bake math; high/ultra profiles can spend the saved bake time on denser LOD0 and larger atlases without changing runtime output.
Hardware Impact: Runtime 0. Editor transform kernel avoids one `float4` construction and one `float3x3` construction per transformed vertex; exact microseconds remain pending Burst profiler.

## Decision 38 - Vertex Layout Facade

Problem: `InteriorClutterVertexLayoutValidator` exposed an internal static managed descriptor array. The field was readonly, but the array contents remained mutable by any code in the editor assembly, weakening the 32-byte mesh ABI boundary.
Solution: Make the descriptor array private as `_layout` and expose `ApplyVertexBufferParams(Mesh, int)` so mesh upload uses the validator as the single writer route for vertex layout configuration. Superseded in Decision 64 by removing the managed descriptor array entirely.
Rejected Alternatives: Keep direct array access, or allocate a fresh managed descriptor array per mesh upload. Direct access exposes mutable ABI state; fresh managed arrays add avoidable editor allocation.
Scalability potential: Low, middle, high, and ultra atlas/LOD profiles all keep the same 32-byte vertex ABI. The hardening prevents a local editor change from drifting the mesh layout for any quality tier.
Hardware Impact: Runtime 0. This historical pass contained ABI mutability; Decision 64 removes the remaining static managed descriptor storage.

## Decision 39 - Named Job Barriers And Atlas Fill Dependency

Problem: Several editor jobs used inline `Schedule(...).Complete()` chains, and atlas rect-color fill was scheduled as a separate job without an explicit dependency on the solid atlas fill. That made the completion barriers harder to audit and could allow the base fill to overwrite rect colors if the scheduler executed the jobs out of order.
Solution: Store every completion barrier in a named `JobHandle` (`transformHandle`, `lodHandle`, `mockHandle`, `extractHandle`, `indexHandle`, `solidFillHandle`, `rectFillHandle`) and schedule `FillAtlasRectColorsJob` with `solidFillHandle` as its dependency before CPU texture serialization.
Rejected Alternatives: Keep same-line completion because the path is editor-only, or complete the solid fill before scheduling rect fill. Same-line completion hides the barrier reason; completing solid fill early serializes work that can remain chained through the scheduler.
Scalability potential: Low profiles with many fallback-color materials and high/ultra profiles with richer atlases now share the same deterministic atlas texel ordering. LOD/output quality behavior is unchanged.
Hardware Impact: Runtime 0. Editor scheduling semantics are clearer and atlas fill correctness improves; exact bake-time microseconds are PENDING VERIFICATION.

## Decision 40 - Pre-Sized Vertex Windows Instead Of Shared NativeList Append

Problem: The original XML described appending transformed vertices into a shared `NativeList`. In a parallel `IJobParallelFor`, shared append would require synchronization, unstable write ordering, or an unsafe parallel writer contract that does not match the already-known triangle counts.
Solution: Pre-count triangle-soup vertices, allocate fixed `NativeArray<InteriorClutterSourceVertex>` and `NativeArray<InteriorClutterRawVertex>` buffers, and give each segment/job index a deterministic destination window. The self-audit now states this as an intentional DOD deviation.
Rejected Alternatives: Pretend `NativeList` append was used, use a lock/atomic append per vertex, or route through a managed list before upload. Those choices add contention, nondeterminism, or GC pressure.
Scalability potential: Low profiles gain predictable cheap writes; high/ultra profiles can push denser LOD0 buffers without append contention. Quality weight still controls LOD collapse, not ownership.
Hardware Impact: Runtime 0. Editor avoids synchronized appends in the densest vertex transformation path; exact microseconds are PENDING VERIFICATION.

## Decision 41 - Proof Boundary Correction

Problem: The self-audit XML still reported `PENDING_CPU_BUILD_GUARD`, but the CPU gate had already opened once and the capped build failed on unrelated Core dependency errors outside SHINOBU_211.
Solution: Update the self-audit proof boundary to `BLOCKED_UNRELATED_CORE_DEPENDENCY_WALL_STRIKE_1` while keeping Unity import, Frame Debugger, and profiler proof marked pending.
Rejected Alternatives: Leave the stale pending label, or edit foreign Core domains to force a build. The first misstates evidence; the second violates domain boundaries and the 3-strike protocol.
Scalability potential: No runtime or visual change. The correction protects integration planning across all profiles.
Hardware Impact: Runtime 0. Build was not relaunched; the change is report accuracy only.

## Decision 42 - Direct Intrinsics Deviation

Problem: Task 06 requested explicit `Unity.Burst.Intrinsics`, but the current source has no verified cross-platform hand-written NEON/SSE transform path. Adding SSE-only or token-only intrinsics would be fake compliance and risk Quest/desktop divergence.
Solution: Keep the raw-pointer column-wise Burst kernel and document the deviation in status/self-audit: direct intrinsics are rejected until a proven NEON and SSE implementation exists.
Rejected Alternatives: Add a `using Unity.Burst.Intrinsics` without use, write SSE-only code, or attempt uncompiled v128/Neon code in a project already behind an unrelated compile wall. These options are dishonest or unsafe.
Scalability potential: Low-to-ultra profiles keep one transform path. High/ultra can still benefit from Burst auto-vectorization; low/Quest is not exposed to an unverified architecture-specific branch.
Hardware Impact: Runtime 0. Editor transform remains column-wise and Burst-compatible; exact microseconds are PENDING VERIFICATION.

## Decision 43 - XML Escape Split

Problem: `AppendTaskAudit` wrote XML text using the JSON escape helper, which only escaped backslashes and quotes. Any future task text containing `<`, `>`, or `&` would corrupt `HABITAT_CONSOLIDATION_SELF_AUDIT.xml`.
Solution: Add `EscapeXml` and use it only for self-audit task text. Keep the existing `Escape` helper for JSON report string fields.
Rejected Alternatives: Reuse JSON escaping for XML, or change the JSON helper globally. Reusing it is invalid XML handling; changing it globally would corrupt JSON path output with XML entities.
Scalability potential: No visual/runtime effect. The artifact remains parseable across all profile/report variants.
Hardware Impact: Runtime 0. Report-only string replacement cost in editor.

## Decision 44 - LOD Asset Load Guard

Problem: `CreateLodRenderer` loaded generated mesh/material assets by path and assigned them directly. If Unity asset lookup failed after serialization, the forge could save a generated prefab with null LOD renderer assets while still returning an output path.
Solution: Load mesh and atlas material into local handles, validate both, and throw a forge-owned `InvalidOperationException` before renderer assignment if either lookup fails.
Rejected Alternatives: Trust generated path strings, defer detection to Frame Debugger, or save a prefab with null renderers and rely on later human inspection. Those paths create false-positive bake reports.
Scalability potential: Low, middle, high, and ultra profiles all keep the same output shape; the guard prevents empty baked geometry in every atlas/LOD profile.
Hardware Impact: Runtime 0. Editor adds two null checks per LOD renderer and avoids shipping a baked prefab with missing geometry/material identity.

## Decision 45 - Interactive Root Compaction

Problem: The preserve list deduped interactive roots only by exact `Transform` reference. If a child mesh first identified a child controller and a later mesh identified its parent, the generated prefab could clone overlapping parent/child hierarchies under `INTERACTIVE_PRESERVED`.
Solution: Compact the preserve list by ancestry: skip a new root when an existing ancestor is already preserved, remove existing descendants when a new parent root is found, and remove stale null entries.
Rejected Alternatives: Preserve every excluded root exactly as discovered, or depend on concrete interactable classes to choose ownership. The first duplicates gameplay objects; the second violates the cross-domain isolation requirement.
Scalability potential: Low-to-ultra profiles preserve the same gameplay ownership while static render density scales through atlas/LOD data only.
Hardware Impact: Runtime 0. Editor adds bounded `Transform.IsChildOf` checks only when an excluded hierarchy is discovered; generated prefabs avoid duplicated interactive controller work.

## Decision 46 - Index Destination Guards

Problem: Extraction jobs validated source vertex indices after reading the index buffer, but `IndexStart + localIndex` itself was not guarded inside the Burst job. A malformed submesh descriptor could read outside the index NativeArray before fallback logic executed.
Solution: Guard the index-buffer offset before reading `Indices`, use `-1` as deterministic fallback source index when out of bounds, and guard destination writes against both output arrays.
Rejected Alternatives: Trust Unity import descriptors, add only main-thread checks, or let invalid imported meshes throw after unsafe reads. The job owns the unsafe window and must guard it locally.
Scalability potential: All quality profiles keep the same output; low-tier bakes are more resilient to bad source art without changing runtime truth or DTO layout.
Hardware Impact: Runtime 0. Editor adds two unsigned comparisons per extracted vertex; prevents unsafe reads in malformed source data.

## Decision 47 - Finite Bounds Guard

Problem: Generated mesh bounds were seeded from `vertices[0].Position` before proving that position was finite. A corrupt first vertex could push NaN/Infinity into mesh bounds and then prefab culling state, even if later vertices were valid.
Solution: Reduce bounds from numeric sentinels, accept only finite positions, and emit a one-meter fallback bounds if no finite position survives. This keeps invalid geometry from owning culling truth.
Rejected Alternatives: Trust upstream pack jobs, call `Mesh.RecalculateBounds`, or clamp the final `Bounds` after NaN has already entered min/max. Upstream-only trust is fragile; Unity recalculation hides the data contract; final clamp after poison is less explicit.
Scalability potential: Low, middle, high, and ultra profiles keep identical geometry ownership and LOD output. The guard prevents bad source art from creating tier-specific culling failures.
Hardware Impact: Runtime 0. Editor adds one finite-position test per generated vertex during the existing bounds pass; prevents poisoned bounds from causing overdraw or invisible baked rooms.

## Decision 48 - Submesh Material Fallback

Problem: Static collection capped source submesh iteration by `sharedMaterials.Count`, so any mesh with more triangle submeshes than renderer material slots lost geometry before atlas packing.
Solution: Iterate all mesh submeshes and use the existing null-material atlas fallback for missing slots. Geometry remains owned by the mesh; material absence only affects fallback color/texture data.
Rejected Alternatives: Keep the material-count cap, clone source material arrays, or generate multiple output materials. The cap loses geometry; cloning arrays preserves SetPass debt; multiple output materials violates the one-material room target.
Scalability potential: Low through ultra profiles preserve the same source geometry truth. Quality weight still controls LOD collapse and atlas tile sizing, not whether submeshes exist.
Hardware Impact: Runtime 0. Editor can process submeshes previously skipped; the cost is correct offline bake work and avoids invisible missing clutter sections in generated prefabs.

## Decision 49 - Active Renderer Truth

Problem: Scan, preview, and bake traversal use inactive-inclusive hierarchy queries so excluded gameplay roots can be discovered, but that also counted inactive prefab variants and disabled MeshRenderers as visible clutter.
Solution: Add one `IsActiveInPrefabHierarchy` predicate and pair it with `renderer.enabled` in scan, preview, and bake loops. The visibility rule now matches authored prefab active flags instead of the broad traversal shape.
Rejected Alternatives: Filter only in bake, use `activeInHierarchy` on prefab-stage objects, or keep inactive renderers in reports. Filter-only bake would leave preview/scanner lying; `activeInHierarchy` is less explicit for loaded prefab contents; counting inactive variants inflates draw-call reports.
Scalability potential: Low-to-ultra profiles now bake the same visible-authored truth. Quality weight still controls LOD/atlas fidelity only, not whether a disabled renderer becomes geometry.
Hardware Impact: Runtime 0. Editor adds bounded parent-chain checks per MeshFilter and prevents generated prefabs from including disabled variant geometry that would inflate MX350 VRAM and vertex submission.

## Decision 50 - Source Extraction Hardening

Problem: MeshData extraction ignored `SubMeshDescriptor.baseVertex`, accepted non-multiple-of-three triangle index windows, and let `TransformAndAppendVerticesJob` dereference whatever segment id existed in an uninitialized map slot.
Solution: Apply base-vertex adjustment in both UInt16 and UInt32 extraction jobs, use 64-bit guarded index-offset arithmetic, truncate malformed submesh index windows to triangle multiples with `UnsupportedMesh` telemetry, and give the transform job explicit vertex/segment counts with fallback writes for invalid segment ids.
Rejected Alternatives: Trust imported MeshData descriptors, throw after scheduling extraction, or clear whole staging buffers. Descriptor trust is unsafe under bad source art; post-schedule throws are late; whole-buffer clearing violates the zero-init bypass goal.
Scalability potential: Low-to-ultra output remains one static atlas/LOD route. Bad source geometry now degrades to bounded fallback vertices and warning flags instead of corrupting quality-tier-specific meshes.
Hardware Impact: Runtime 0. Editor adds guarded integer math per extracted/transformed vertex; prevents wrong vertices from base-vertex meshes and avoids unsafe segment pointer dereference under malformed extraction windows.

## Decision 51 - Atlas Mask Contract And Compression

Problem: The third generated atlas was still documented and partly named as ARM while the generated material assigned it to `_MaskMap` and `_MetallicGlossMap`. That is a channel-contract fault: AO/roughness/metal packing is not the same payload as URP mask map or Standard metallic/smoothness. Generated atlas `.asset` textures also stayed RGBA32 unless a later importer step changed them.
Solution: Rename the third atlas path to mask, store `MaskRgba` in `InteriorClutterAtlasColor`, pack fallback channels as R=metallic, G=occlusion strength, B=detail mask zero, A=smoothness, and assign the same generated texture to `_MaskMap` and `_MetallicGlossMap`. Do not copy `_OcclusionMap` because it cannot populate the complete mask payload. Before asset publication, call editor texture compression: normals prefer BC5, albedo/mask prefer BC7, DXT5 is fallback, and failures set `AtlasCompressionFallback`.
Rejected Alternatives: Keep ARM naming while binding to mask slots, copy `_OcclusionMap` into a four-channel mask texture, or leave generated textures as RGBA32 and depend on importer defaults. Those choices either corrupt shader meaning or publish excess VRAM/bandwidth debt.
Scalability potential: Low profiles get compressed compact atlases with correct metallic/AO/smoothness fallback; middle/high keep the same shader contract at larger tile sizes; ultra spends saved bandwidth on denser LOD0 and larger atlas profiles without changing material authority.
Hardware Impact: Runtime draw-call count remains unchanged. Expected low-end i3/MX350 gain is reduced texture memory traffic versus uncompressed RGBA32 generated atlases; exact milliseconds remain PENDING VERIFICATION until Unity import/profiler capture.

## Decision 52 - Self-Audit Evidence Qualifier

Problem: The self-audit task reconciliation used mandated PASS/FAIL statuses, but a plain PASS could be interpreted as full Unity import, Frame Debugger, profiler, and clean solution-build evidence. Current proof is static source plus one capped build blocked by unrelated Core dependencies.
Solution: Preserve `status="PASS"` for the 20-task implementation reconciliation and add `evidence="STATIC_SOURCE_ONLY"` to every `<Task>` node. The root `ProofBoundary` remains the owner of compile/import/profiler truth.
Rejected Alternatives: Replace PASS with custom statuses, or leave the task rows unqualified. Custom statuses violate the requested XML shape; unqualified PASS overstates the verification state.
Scalability potential: No runtime or visual change. The change prevents low/middle/high/ultra claims from being treated as profiler-proven before Unity import and generated asset capture happen.
Hardware Impact: Runtime 0. Report-only XML attribute addition; no build or editor import launched.

## Decision 53 - Interactive Draw-Call Report Truth

Problem: Reports used `DrawCallsAfter = 1` and scanner `estimatedDrawCallsAfterForge = 1`, which is true only for the generated static monolith. Generated prefabs can also preserve interactive renderer roots, so aggregate after-cost was underreported.
Solution: Count visible preserved interactive renderers from the existing mesh-filter scratch after collection, keep `staticDrawCallsAfter = 1`, and set total after draw calls to `1 + preservedInteractiveRenderers`. The scanner now emits `estimatedStaticDrawCallsAfterForge = 1` and total `estimatedDrawCallsAfterForge = 1 + InteractiveChildRenderers`.
Rejected Alternatives: Count preserved roots as renderers, hide interactive cost from reports, or bake interactives into the static monolith. Root count underreports multi-renderer gameplay objects; hiding cost is false evidence; baking interactives violates gameplay ownership.
Scalability potential: Low through ultra profiles keep the same generated static monolith route. The report now shows the cost that remains after preserving gameplay objects, so quality tiers are not justified by false one-draw-call totals.
Hardware Impact: Runtime 0. Editor adds bounded checks over already-collected mesh filters and preserved roots; MX350 planning gets a more honest post-forge draw-call estimate.

## Decision 54 - Remap UV Utility Guard

Problem: Task 08 explicitly asks for `RemapUvCoordinatesJob`, but the active bake path sensibly fuses UV remap into `TransformAndAppendVerticesJob` to avoid a second vertex pass. The standalone job remained unused and lacked local source/output length and NaN output guards.
Solution: Keep the standalone job for the task-mandated utility surface, but add unsigned source/output bounds checks and a finite fallback for remapped UV output. Document that the production bake path uses the fused transform remap.
Rejected Alternatives: Delete the standalone job, or schedule a redundant UV-only pass to prove use. Deletion conflicts with the XML task wording; redundant scheduling wastes editor time and duplicates memory traffic.
Scalability potential: Low through ultra profiles keep the same fused one-pass bake path. The guarded standalone job is available for future utility use without introducing bad output under malformed caller lengths.
Hardware Impact: Runtime 0. Editor utility adds two unsigned comparisons and one finite check per scheduled UV only if the standalone job is used; active bake cost is unchanged.

## Decision 55 - LOD Decimation Window Guard

Problem: `DecimateTriangleSoupJob` assumed `TargetTriangleCount`, `SourceTriangleCount`, schedule count, and NativeArray lengths were mutually consistent. The current caller constructs them consistently, but the Burst kernel writes into uninitialized LOD buffers and must prove its own source/output windows.
Solution: Add destination-window guards, source-triangle-window guards, finite source-position validation, finite area fallback, and deterministic fallback triangle writes inside the LOD kernel.
Rejected Alternatives: Trust caller math forever, clear the whole LOD output buffer, or let a malformed future caller trip a NativeArray bounds exception after uninitialized allocation. Caller trust is brittle; full clears violate the zero-init bypass; exception-first behavior leaves weaker forensic output.
Scalability potential: Low through ultra profiles keep identical continuous quality-weight LOD ratios. The guard prevents bad source art or future caller drift from creating tier-specific NaN geometry or uninitialized LOD triangles.
Hardware Impact: Runtime 0. Editor LOD pass adds bounded unsigned window checks and finite predicates per decimated triangle; prevents poisoned LOD mesh output before asset serialization.

## Decision 56 - Atlas Rect Fill Window Guard

Problem: `FillAtlasRectColorsJob` assumed rect/color NativeArray length parity and used 32-bit rect extent addition before clamping. Current builder creates matching arrays, but the Burst texel kernel writes a native pixel buffer and must own malformed metadata guards.
Solution: Skip rects without a matching color record, clamp rect bounds through 64-bit arithmetic, skip empty spans, and compute pixel indices in 64-bit before the final bounded write.
Rejected Alternatives: Trust builder parity, clear the texture after failure, or let integer overflow wrap malformed rects into unrelated texel rows. Builder-only trust is brittle; clearing wastes editor time; overflow writes corrupt visual evidence.
Scalability potential: Low-to-ultra atlas profiles keep the same visual route. Larger high/ultra atlases benefit most from avoiding corrupt spans when bad source metadata appears.
Hardware Impact: Runtime 0. Editor adds one unsigned color-window check and 64-bit clamp arithmetic per rect; protects native atlas staging before `Texture2D.SetPixelData`.

## Decision 57 - Double-Subtracted AUP Translation

Problem: The segment DTO stored a `double3` room-relative offset, but the transform matrix consumed by Burst still carried translation from `sourceRoot.worldToLocalMatrix * sourceTransform.localToWorldMatrix`, which is float-based Unity matrix math.
Solution: Historical Loop 44 attempted to compute a double-subtracted root-relative delta from Unity `Transform.position`. Decision 64 supersedes that proof: the active code now composes prefab-local hierarchy TRS from the source root and does not claim to recover precision from already-float world transforms.
Rejected Alternatives: Keep float matrix translation, cast absolute world positions to float, or move AUP proof into telemetry only. Those choices leave the real vertex transform exposed to 100 km jitter.
Scalability potential: Low through ultra profiles retain identical geometry ownership; precision no longer depends on profile tier or world placement distance.
Hardware Impact: Runtime 0. Historical world-position proof is marked superseded; active editor parent-walk cost is bounded by prefab hierarchy depth.

## Decision 58 - Mask Source Texture Ownership

Problem: Copying `_MetallicGlossMap` wholesale into the generated mask atlas contradicts the documented mask contract. Standard metallic maps do not own URP mask G/B lanes, so the copy can corrupt occlusion/detail data.
Solution: Source texture copy into the mask atlas now accepts `_MaskMap` only. Materials with only `_MetallicGlossMap` use scalar fallback packing: R=metallic, G=occlusion strength, B=detail default zero, A=smoothness.
Rejected Alternatives: Continue wholesale `_MetallicGlossMap` copy, copy `_OcclusionMap`, or invent a partial channel repacker without Unity import proof. These corrupt channel ownership or expand scope.
Scalability potential: Low profiles get stable scalar mask fallback; high/ultra can still use authored `_MaskMap` when present without changing runtime material shape.
Hardware Impact: Runtime 0. Editor may copy fewer Standard textures, but avoids shipping bad mask channels that would cost visual QA time.

## Decision 59 - Black-Box Pre-Wrap Dump Count

Problem: Before the 300-entry ring wrapped, `Dump` wrote all 300 reset rows while the reason sidecar reported only `_written` entries. The binary and sidecar disagreed for early failures.
Solution: Pre-wrap dumps now write only `_written` recorded entries. After wrap, dumps still write the chronological retained 300-entry window.
Rejected Alternatives: Keep fixed-width padded dumps and document the padding, or allocate a managed staging array to trim output. Padding keeps contradictory evidence; managed staging violates the native dump path.
Scalability potential: No visual change. Early low-end/editor failures generate smaller forensic files; long bakes keep the full retained ring.
Hardware Impact: Runtime 0. Editor failure path writes fewer bytes before wrap and preserves exact row-count semantics.

## Decision 60 - Profile-Weighted LOD Residency

Problem: The bake used `GlobalQualityWeight` for triangle ratios and collapse strength, but generated prefab `LODGroup` thresholds were hardcoded. That left low-profile rooms holding dense LODs for the same screen residency as high/ultra rooms.
Solution: Pass `InteriorAtlasProfile` into generated prefab publication and derive LOD0/LOD1/LOD2 screen thresholds with `math.smoothstep` and `math.lerp`. Low profiles lower LOD residency; high/ultra profiles retain richer static monolith LODs longer.
Rejected Alternatives: Keep fixed thresholds, add a binary low/high switch, or push runtime code to decide room LOD policy. Fixed thresholds waste low-tier vertex bandwidth; binary switches violate the scalability pillar; runtime policy adds hot-path logic for a decision already known at bake time.
Scalability potential: Low sheds dense interiors earlier, middle keeps balanced residency, high/ultra retain visual-overkill mesh detail longer. Gameplay truth, DTO layout, generated mesh identity, and rollback exclusion remain unchanged.
Hardware Impact: Runtime 0 additional managed work. Editor prefab construction adds one smoothstep and three lerps per generated prefab; expected MX350 benefit is lower dense-LOD residency in generated low profiles, pending Unity/Frame Debugger proof.

## Decision 61 - Render Ownership And Normal/Tint Fence

Problem: Read-only audit found three presentation-contract faults: generated renderers enabled material instancing while also marking static batching, the 32-byte vertex layout omitted tangents while the generated material enabled normal maps, and albedo texture copy discarded material tint because the generated material color is white.
Solution: Remove `StaticEditorFlags.BatchingStatic` from generated prefabs while retaining `OccludeeStatic | ContributeGI`; keep normal atlases as offline artifacts but do not bind `_BumpMap`/`_NormalMap` or enable `_NORMALMAP`; add `AtlasTintFallback` plus a white-tint gate so tinted materials keep deterministic fallback rect color until tint-aware atlas multiplication exists.
Rejected Alternatives: Static-batch the already consolidated monolith, expand the vertex ABI with tangents inside a polish loop, or copy tinted albedo sources and rely on a white generated material. Static batching duplicates geometry and conflicts with GPU submission ownership; tangent expansion changes the 32-byte ARM64 stride contract; tint loss is a visible material lie.
Scalability potential: Low profiles avoid duplicated static-batch mesh memory and shader normal-map variant risk; middle/high keep correct albedo ownership; ultra can later authorize a tangent-bearing visual-overkill path without changing current DTO identity or gameplay authority.
Hardware Impact: Runtime added 0 us and 0 B/frame. Editor adds one white-tint predicate per material. Expected i3/MX350 benefit is lower generated asset memory pressure and fewer false shader features; exact profiler/import proof remains PENDING VERIFICATION.

## Decision 62 - Precomputed Inverse-Transpose Normal Basis

Problem: Disabling generated normal-map binding removed the tangent-space shader lie, but vertex normals still used the same scaled `LocalToRoom` columns as positions. Under non-uniform scale that bakes incorrect lighting normals into the static monolith.
Solution: Expand `InteriorClutterSegment` to 192 bytes and store `NormalToRoomC0/C1/C2` as signed cofactor columns derived once per segment from the local-to-room linear basis. `TransformAndAppendVerticesJob` now transforms source normals with that precomputed inverse-transpose basis and keeps the existing finite fallback.
Rejected Alternatives: Run `math.inverse` or cross products per vertex, expand the runtime 32-byte mesh vertex ABI with tangents, or accept wrong normals because normal maps are fenced. Per-vertex inverse math burns editor ALU; tangent expansion changes the ARM64 vertex contract; wrong baked normals create visible shading debt.
Scalability potential: Low profiles get correct Lambert response without shader variants; middle/high keep stable baked lighting under authored non-uniform scale; ultra can add a tangent-bearing normal-map path later without rewriting the current static mesh ownership route.
Hardware Impact: Runtime added 0 us and 0 B/frame. Editor memory increases by 32 bytes per static segment. Editor vertex loop avoids per-vertex inverse-transpose reconstruction; exact bake-time microseconds remain PENDING VERIFICATION.

## Decision 63 - Burst Attribute Exactness

Problem: All SHINOBU_211 mathematical jobs already carried the required Burst flags, but the attribute named-argument order did not match the mandate's exact directive text. Static auditors can read that as non-compliance even though C# semantics are equivalent.
Solution: Normalize the then-existing ten job attributes in `InteriorClutterForgeJobs.cs` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` and update the self-audit/architecture proof text. This is a source-contract tightening pass, not a logic change.
Rejected Alternatives: Leave semantic equivalence undocumented, edit sibling SHINOBU_213 jobs, or switch to deterministic float mode. Undocumented equivalence fails the exact-text mandate; sibling edits violate domain boundary; deterministic float mode is not required because this editor forge emits static presentation assets excluded from rollback state rings.
Scalability potential: Low, middle, high, and ultra profiles keep the same continuous `GlobalQualityWeight` behavior; exact Burst directives preserve vectorized editor bake kernels across all profile outputs.
Hardware Impact: Runtime 0 us and 0 B/frame. Editor compiler metadata is behaviorally unchanged; the value is compliance proof and removal of audit ambiguity, not a measured microsecond gain.

## Decision 64 - Gauss Audit Integration And Mesh ABI Hardening

Problem: Read-only audit found recoverable atlas copy failures were reported as hard copy failures, tinted albedo texture detail was lost, LOD jobs were serialized behind a transform completion barrier, transform proof still leaned on absolute float world positions, and the mesh layout facade still retained static managed descriptor-array residue.
Solution: Retry failed exact-size `Graphics.CopyTexture` through the existing RT-blit route and flag `AtlasDirectCopyFallback`; multiply tinted albedo tiles offline through `TintAtlasTileJob`; schedule LOD1/LOD2 with `transformHandle` as dependency and complete their combined handle once before mesh serialization; build room-local matrices by walking prefab-local hierarchy TRS; replace the static managed descriptor array with a disposed Temp `NativeArray<VertexAttributeDescriptor>` and validate mesh layout through direct Mesh attribute accessors.
Rejected Alternatives: Collapse exact-size copy exceptions directly to solid fallback, use source albedo textures while generated material color stays white, complete transform before LOD scheduling, cast world `Transform.position` values into local geometry proof, or keep static managed layout descriptors because the path is editor-only. Those alternatives either lose visual data, add an artificial barrier, overclaim AUP authority, or contradict the no-private-array proof.
Scalability potential: Low profiles keep detail-preserving tint and copy fallback without adding runtime cost; middle/high retain source texture fidelity when GPU copy paths vary by platform; ultra can spend the same runtime budget on denser LOD retention because all work remains offline and quality-weight-driven.
Hardware Impact: Runtime 0 us and 0 B/frame. Editor cost is one fallback blit only after direct-copy failure, one readback/tint Burst pass per tinted source texture, and one Temp 3-record NativeArray per mesh ABI setup. Expected low-end i3/MX350 runtime gain remains from one static renderer/material route; exact profiler proof remains PENDING VERIFICATION.

## Decision 65 - AUP Proof Drift Cleanup

Problem: Historical Loop 44 text still contained obsolete helper names and stronger world-position precision wording than the active code now owns. Some current proof text also quoted exact array-returning mesh validation API names, which polluted negative policy scans even though source usage had been removed.
Solution: Mark the Loop 44 AUP proof as superseded by Decision 64, describe the active route as prefab-local hierarchy TRS only, and replace obsolete API-name quotes in proof text with behavioral descriptions.
Rejected Alternatives: Leave historical wording untouched, or teach every future scanner to ignore quoted prose. Historical wording can be mistaken for active proof; scanner exceptions create audit fragility.
Scalability potential: No visual or runtime change. Low through ultra profiles keep the same generated static mesh route; the benefit is proof integrity.
Hardware Impact: Runtime 0 us and 0 B/frame. Documentation-only cleanup; exact performance is unchanged.

## Decision 66 - Tinted Tile Mip Payload Trim

Problem: `CopyTextureViaTintedBlit` created a temporary RGBA32 `Texture2D` with a mip chain, then `TintAtlasTileJob` processed the full raw texture payload. Only mip 0 is copied into the generated atlas, so lower mip data was unused editor work.
Solution: Create the temporary tint tile without mipmaps, apply it without mip regeneration, and document the path as top-mip-only. The atlas asset still owns its own mip/compression pipeline; the throwaway tile exists only as a readback/tint/copy bridge.
Rejected Alternatives: Keep mip-chain temp textures because the path is editor-only, or generate/tint all lower mips before copying only mip 0. Both waste memory bandwidth and Burst iterations that cannot change the produced atlas tile.
Scalability potential: Low profiles with many 512px tinted tiles avoid unnecessary temp payload; middle/high/ultra 1024/2048px tinted tiles avoid a larger 33% throwaway mip overhead while keeping source texture fidelity.
Hardware Impact: Runtime 0 us and 0 B/frame. Editor tint path reduces temporary texture raw payload and tint iterations from about 1.333x top-mip area to 1.0x for each tinted copied tile; exact Unity profiler proof remains pending.
