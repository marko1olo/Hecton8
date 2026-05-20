# Rationale_SHINOBU_211

Evidence state: PENDING VERIFICATION. Runtime/Unity proof absent.

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
Solution: Generate one atlas material and three atlas textures (albedo, normal, ARM) per room. Mesh UVs are remapped in the transform job through per-segment atlas rects.
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
Solution: Fill atlas rects with deterministic material colors/default normal/default ARM, then attempt `Graphics.CopyTexture`; mismatched source/tile sizes go through temporary RT blit plus copy and raise `AtlasScaledTexture`.
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

Problem: Transform normalization copied the 160-byte segment DTO per vertex and used ternary rsqrt guards; LOD2 decimation was serialized behind LOD1 despite both reading LOD0 and writing separate buffers.
Solution: Read source/segment DTOs through `ref readonly`, guard rsqrt with `math.max`, select fallback normals with `math.select`, always lerp triangle collapse by a continuous scalar, and join LOD1/LOD2 jobs through `JobHandle.CombineDependencies`.
Rejected Alternatives: Copying segment DTO per vertex, retaining branchy collapse guards, or serializing independent LOD jobs. These choices waste editor bake CPU and reduce Burst's ability to keep predictable vectorized math.
Scalability potential: At low quality, collapse scalar increases and cheapens LOD2; at high/ultra, collapse drops while LOD jobs still run independently.
Hardware Impact: Runtime 0. Editor transform avoids 160-byte per-vertex segment copies; LOD bake can use worker parallelism instead of artificial dependency chaining.

## Decision 20 - Scratch-State Containment

Problem: The exclusion filter still owned a hidden static `List<Component>` scratch buffer, and preview traversal owned a static mesh-filter scratch list. This was editor-only, but it made the filter stateful and could be misread as a global mutable owner.
Solution: Pass component scratch explicitly from scan, bake collection, and preview transactions into `InteriorClutterExcludeFilter`; make preview mesh-filter scratch local to the preview build. Persistent static state remains only for the SceneView overlay bounds that must survive until repaint.
Rejected Alternatives: Keep static scratch because it avoids one editor allocation, or allocate inside each filter method. Static scratch hides ownership; per-call allocation would scale with hierarchy depth. Transaction-owned scratch is the narrower DOD boundary.
Scalability potential: Low/middle/high/ultra output is unchanged. Large ultra prefabs get deterministic traversal ownership without hidden cross-preview state.
Hardware Impact: Runtime 0. Editor removes global mutable scratch contention; preview pays one cold local list allocation per preview call.

## Decision 21 - Atlas Serialization Fidelity

Problem: `Graphics.CopyTexture` and scaled RT blits write through GPU texture state. Saving a generated `Texture2D` asset immediately after GPU copies risks serializing the original CPU fallback texels if the CPU-side texture data is not synchronized. Normal and ARM atlases were also created in sRGB space, which is wrong for data textures.
Solution: After any source texture copy succeeds, blit each atlas into a temporary RT, call one editor-only `ReadPixels`, and `Apply` before `AssetDatabase.CreateAsset`. Albedo atlases are created sRGB; normal and ARM atlases are created linear, and temporary RTs use matching `RenderTextureReadWrite`. The generated material enables normal/mask/metallic keywords.
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

Problem: Atlas source texture discovery used a `params string[]` helper. Every albedo/normal/ARM lookup created a managed string-array wrapper inside the material loop, which is avoidable heap churn during large room batch bakes.
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
