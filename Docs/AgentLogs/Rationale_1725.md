# Rationale_1725

Status: IMPLEMENTED - STATIC VERIFIED; BUILD BLOCKED BY UNRELATED CORE DEPENDENCY

## Decision 00 - Active Domain And Missing Named Shader

Problem: The prompt names Hecton_Master_Fauna.shader, but static search found no shader with that filename under Assets.

Solution: Treat existing fauna-relevant shader files as the real shader contract until source proves otherwise. Primary candidates are Hecton_LeviathanOrganic.shader, HectonBiolumMaster.shader, and Hecton_Master_Lit.shader. Any new baker must write to existing project property names or add an Editor-only include/compute file inside the allowed shader include path.

Rejected Alternatives: Creating a new runtime master shader before proving material usage would add variant and material-route debt. Editing project settings or replacing all fauna materials is outside the prompt domain.

Scalability potential: Low outputs smaller baked static maps and relies on baked AO/emission masks. Middle increases mask resolution. High and Ultra add richer offline wrinkle/chitin/biolum detail without changing gameplay truth.

Hardware Impact: Removing runtime texture/material creation prevents spawn-time GC and material clone residency spikes on i3/MX350. Exact runtime microseconds are PENDING PROFILER.

## Decision 01 - MPB Conflict Handling

Problem: Prompt requests MaterialPropertyBlock for unique fauna state. Current AGENTS.md and REND_URP mandate forbid MPB on standard geometry because it breaks SRP Batcher.

Solution: Do not introduce new MPB usage until source audit proves the current FaunaBrain route already owns a cold MPB. If modifying runtime presentation, keep shared materials and avoid runtime material clones. Any existing MPB use is treated as existing debt unless it is the only narrow way to remove worse material clones without public API churn.

Rejected Alternatives: Adding per-renderer MPB state broadly because the prompt says so; that conflicts with the higher project shader mandate and would trade one performance violation for another.

Scalability potential: Low/Middle stay SRP-batcher-friendly using shared material assets and baked masks. High/Ultra spend saved runtime cost on richer offline maps and shader detail.

Hardware Impact: Avoiding material clones reduces managed allocation and GPU material residency churn. Avoiding new MPB use protects SRP Batcher on compact GPUs.

## Decision 02 - Packed Mask Layout Source Authority

Problem: The batch text uses MRAO language, but the existing fauna shader declares `_MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)")` and decodes through `HectonCoreLitDecodePackedMaskV1`.

Solution: Bake the fauna packed mask as R metallic/chitin, G ambient occlusion, B smoothness, A bioluminescence emission. The baker report will name this `LeviathanOrganicMaskV1` to prevent accidental use as the generic master-lit roughness layout.

Rejected Alternatives: Baking roughness into G because the text says MRAO. That would invert roughness/smoothness behavior in `Hecton_LeviathanOrganic.shader` and force runtime material fixes.

Scalability potential: Low/Middle use the same mask layout at lower resolution. High/Ultra increase offline detail density without changing shader decode or material identity.

Hardware Impact: Correct packed decode avoids runtime channel repacking and prevents extra material variants on i3/MX350.

## Decision 03 - Offline Visual Fake Instead Of Runtime Skin Simulation

Problem: Deep folds, pores, chitin plates, and vein masks can be simulated per creature at spawn, but that directly attacks predator flock spawn time.

Solution: Bake UV-space crease fields, cellular pore noise, chitin plate bands, and spine/jaw vein curves into persistent textures. The only runtime behavior left is shared material sampling plus existing shader time modulation.

Rejected Alternatives: Mesh-space skin strain, per-renderer texture generation, or per-spawn material variants. Those routes add allocation, readback, or draw-state churn for an effect that can be authored offline.

Scalability potential: Low uses 1K albedo and 512 detail/mask. Middle uses 2K/1K. High uses 3K/1536 or aligned 2K detail. Ultra uses 4K albedo with 2K normal/mask and denser baked microstructure.

Hardware Impact: On low silicon, spawn no longer pays texture synthesis or material clone cost. Saved CPU/GPU budget can be spent on visible school density and shader-only biolum modulation.

## Decision 04 - Editor-Owned GraphicsBuffer For Bone Paths

Problem: The prompt requests compute-side skeletal path transfer, but the baker must not leave GPU buffers resident after Editor domain reload or failed bake.

Solution: `FaunaTextureBaker` creates one `GraphicsBuffer` of eight float4 points per bake or dry run, binds it to all compute kernels, and releases it in `finally`. Source renderer bones are sampled when available; otherwise a deterministic mesh-bounds path is used.

Rejected Alternatives: Runtime bone-weight readback, persistent static buffers, or scene searches in `FaunaBrain`. Those routes introduce runtime coupling and reload leaks for an offline texture problem.

Scalability potential: Low keeps the same eight-point path with coarse maps. Middle/High/Ultra spend quality on texture density and procedural detail, not more runtime bone state.

Hardware Impact: Editor-only buffer allocation has 0 us player frame cost. Runtime saved estimate remains spawn-time material/texture avoidance rather than per-frame compute.

## Decision 05 - Validation Before AssetDatabase Import

Problem: A corrupt compute write can serialize non-finite displacement or an invalid mask channel and then poison runtime materials.

Solution: The baker reads back every generated texture, checks exact pixel count, finite normalized channels, packed-mask channel independence, and non-empty emission alpha before encoding and importing. Albedo, normal map, packed mask, and optional pulse atlas use PNG assets with enforced importer settings.

Rejected Alternatives: Importing first and relying on visual inspection. That would allow bad assets into version control and cause delayed runtime diagnosis.

Scalability potential: Same validation applies from low 512/1K maps through ultra 2K/4K maps. Larger outputs only increase editor validation time.

Hardware Impact: Prevents invalid runtime texture fetch behavior. On MX350, compressed import is enforced as BC7/ASTC instead of raw resident textures.

## Decision 06 - Compaction Fence Audit Boundary

Problem: Task 19 asks for compaction-fence proof, but this change does not modify runtime DataVault readers.

Solution: Source scan shows existing fauna vault routes check `IsCompactionFenceActive` before `TryResolveHandle` in `FaunaBrain`, `FaunaSimulationEngine`, `FaunaKinematicsRuntime`, and spawn/director paths. The baker writes AssetDatabase textures and never touches `GlobalDataVault`.

Rejected Alternatives: Editing DataVault logic without changing its domain behavior. That would create cross-domain risk unrelated to material clone eradication.

Scalability potential: Texture quality changes do not affect vault pointer safety. Low through Ultra use the same static asset route after bake.

Hardware Impact: No new job scheduling, no hidden `.Complete()`, and no same-frame readback in player. Editor readback occurs only during bake.

## Decision 07 - Build Attempt Inconclusive

Problem: Task 17 authorizes build verification, but global instructions forbid launching dotnet build when CPU is over 50 percent or another dotnet/csc compiler is active.

Solution: Initial CPU sample was 100 percent with active dotnet processes, so the build was blocked. A later sample was 29.61 percent with no compiler process returned, so a single `dotnet build Hecton8.Editor.csproj --no-restore /m:1` was launched. The tool timed out after 120 seconds; the dotnet process and csc child later exited, but no stdout/stderr was recoverable and no output assembly appeared under `Temp/CodexBuild/Hecton8.Editor`.

Rejected Alternatives: Launching a second build without captured diagnostics. That would turn verification into load spam and violate the single-build intent of the task.

Scalability potential: No runtime effect.

Hardware Impact: Build verification remains inconclusive. Static checks passed, but there is no clean compiler proof.

## Decision 08 - Remove Report Writer Duplication

Problem: `FaunaTextureBaker` duplicated folder, path, write, and JSON-report helpers that already exist or belong in `ProceduralTextureBaker`.

Solution: Removed report writing from the baker, deleted the static JSON report artifact, and routed folder creation, asset name sanitation, atomic writes, asset finalization, and importer enforcement through `ProceduralTextureBaker`.

Rejected Alternatives: Keeping a self-contained 1,200-line editor window with duplicate utility code. That increases drift and does not improve runtime.

Scalability potential: Same bake output tiers. Less editor code surface.

Hardware Impact: Player runtime unchanged. Editor bake path now uses atomic first-party file writes and has less I/O.

## Decision 09 - Normal Map Contract Correction

Problem: The first baker version wrote height plus tangent normal XY into an EXR texture, but `Hecton_LeviathanOrganic.shader` samples `_NormalMap` through `UnpackNormalScale`.

Solution: Bake a conventional normal-map PNG from the height field and import it with `ProceduralTextureBaker.TextureRole.Normal`, which enforces `TextureImporterType.NormalMap` and BC5 on Standalone.

Rejected Alternatives: Forcing the runtime shader to decode a custom EXR channel layout. That would add shader debt and would not match existing fauna material contracts.

Scalability potential: Low through Ultra still scale only texture resolution and procedural detail density.

Hardware Impact: Keeps runtime sampling to the existing normal-map path; no extra texture decode branch on i3/MX350.

## Decision 10 - Shared Presentation MPB Scratch

Problem: The current presentation route avoided runtime material clones but still allocated a `MaterialPropertyBlock` per fauna instance during spawn initialization.

Solution: Use one shared main-thread MPB scratch, clear it before every `GetPropertyBlock`, and rely on `SetPropertyBlock` copying state into the target renderer.

Rejected Alternatives: Returning to runtime material instances or keeping one MPB per predator. Both increase spawn-time allocation pressure.

Scalability potential: Low and Middle avoid managed spawn spikes during predator flock creation. High and Ultra keep the same shader presentation path and spend budget on baked maps.

Hardware Impact: Removes one managed MPB object allocation per spawned fauna; exact GC byte delta depends on Unity runtime wrapper size.

## Decision 11 - Deterministic Mapped Skeleton Buffer Upload

Problem: The first fauna baker skeleton path upload used a managed `Vector4[]` and `GraphicsBuffer.SetData`. When a renderer had fewer than eight sampled bones, the unused tail was not semantically meaningful even though the current compute path reads only `pointCount`.

Solution: Fill a stackalloc eight-point payload, duplicate the last valid sampled bone into any unused tail slots, and upload through `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`. The buffer is still Editor-owned and released in `finally`.

Rejected Alternatives: Keeping `SetData` because it is editor-only, or increasing skeleton point count for visual excess. The current eight-point path is enough for UV-space vein/fold projection and avoids new runtime bone dependencies.

Scalability potential: Low through Ultra use the same stable path payload. Quality scales texture resolution and procedural detail, not runtime skeleton traffic.

Hardware Impact: Player runtime impact remains 0 us. Editor upload avoids the extra managed point array and leaves deterministic data in every GPU slot.

## Decision 12 - UV Metric Gate Before Texture Serialization

Problem: The baker validated generated pixels and import settings, but a malformed source mesh UV0 could still produce convincing-looking skin masks with stretched pores, broken chitin bands, or smeared biolum veins. That would push the defect into runtime material presentation.

Solution: Add a pre-serialization UV gate inside `FaunaTextureBaker`: resolve the source mesh once, read vertices/UV0/indices through fixed-capacity editor scratch lists, reject missing UV0, non-finite data, degenerate triangles, and any measured per-triangle stretch above 1.50. `UvMetrics` is included in `BakeResult` and validated by `UnsafeUtility.SizeOf<UvMetrics>()`.

Rejected Alternatives: Using `mesh.vertices`, `mesh.uv`, or `mesh.triangles` copy properties for convenience. Those allocate full managed arrays and contradict the same memory discipline the baker is meant to enforce.

Scalability potential: Low through Ultra all share the same authoring gate. Low maps stay cheaper, Middle/High/Ultra maps get denser detail only when the UV layout is structurally safe enough to carry it.

Hardware Impact: Player runtime cost remains 0 us. Editor-time O(vertex + index) scan prevents bad texture packages from creating shimmer/stretch artifacts on compact GPUs where there is no spare budget for runtime correction.

## Decision 13 - Build Blocked Upstream

Problem: After the UV gate pass, CPU and compiler guards allowed one new throttled build. `dotnet build Hecton8.Editor.csproj --no-restore /m:1` failed in `Hecton8.Core.csproj` before editor-domain proof.

Solution: Treat the build as blocked by an unrelated core dependency. The diagnostics are `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:70` CS8168 and CS8350 around exposing local `record` through a `ReadOnlySpan<byte>` out parameter. This is outside the 1725 write domain.

Rejected Alternatives: Editing Data Monolith from the fauna baker task. That would create cross-domain authority drift and hide the real owner of the compile failure.

Scalability potential: No direct visual scaling effect. The fauna baker remains editor-only and static-verified; compile proof needs the Data Monolith owner to clear the upstream C# lifetime error.

Hardware Impact: No player runtime impact from this decision. No repeated build spam after the dependency failure.

## Decision 14 - Deterministic Biolum Pulse Loop Seam

Problem: The pulse atlas used `frame / frames` for phase. That is periodic in continuous time, but the serialized last active atlas tile was not the positive boundary frame, so a runtime flipbook loop could visibly jump from frame 63 back to frame 0.

Solution: Map the last active frame to `2*pi` with `frame / (frames - 1)` and add a CPU validation gate comparing frame 0 against frame 63 before serialization. The accepted maximum byte-channel delta is 2 to allow GPU/PNG quantization noise.

Rejected Alternatives: Adding runtime crossfade, shader-side phase smoothing, or extra dynamic light state. Those spend frame time to hide an offline asset defect.

Scalability potential: Low through Ultra all use the same deterministic loop seam. Higher tiers spend resolution and microstructure detail, not runtime repair.

Hardware Impact: Player runtime remains 0 us. Editor validation scans one atlas tile once per bake and prevents pulse stutter on compact devices.

## Decision 15 - Offline Shared Material Binding

Problem: Texture assets were generated correctly, but leaving texture assignment as manual or runtime glue invites a later regression where a creature script creates material instances to attach baked maps.

Solution: Add an optional Target Shared Material asset slot to `FaunaTextureBaker`. If provided, the baker validates the material is an AssetDatabase asset with `_BaseMap`, `_NormalMap`, and `_MaskMap`, then binds the imported textures in the Editor. The old texture slots are snapshotted and restored if a later transaction step fails.

Rejected Alternatives: Creating a material clone or scene renderer override from the baker. That would move the problem from runtime to authoring state and still break shared-material discipline.

Scalability potential: Low through Ultra all use the same shared material route. Quality scales the baked texture package, not runtime material identity.

Hardware Impact: Player runtime remains 0 us. This prevents spawn-time material/texture assignment code from being needed on i3/MX350.

## Decision 16 - Shared Material Asset Scope Gate

Problem: `AssetDatabase.Contains(targetMaterial)` proves the material is known to Unity, but it does not prove the material is a project-owned `.mat` asset under `Assets/`. A package or built-in material could pass too far into the authoring route.

Solution: `FaunaTextureBaker` now resolves the target material path and rejects empty paths, non-`Assets/` paths, and non-`.mat` assets before any texture slot snapshot or bind.

Rejected Alternatives: Accepting package/built-in materials because they expose `_BaseMap`, `_NormalMap`, and `_MaskMap`. That keeps authoring ambiguous and invites runtime override code later.

Scalability potential: Low through Ultra all keep the same authored shared material identity. Quality still scales baked texture resolution and biolum atlas density only.

Hardware Impact: Player runtime remains 0 us. The guard prevents bad authoring inputs from forcing per-spawn material assignment or clone repair on i3/MX350.

## Decision 17 - Native Pixel Alias For Shared Mask Validator

Problem: `ProceduralTextureBaker.TryCollectMraoStats()` used `Texture2D.GetPixels32()`, which copies the full mask into a managed array during editor validation.

Solution: Read the texture through `GetRawTextureData<Color32>()` and iterate the native alias directly. The method still fails closed on unreadable or empty texture data.

Rejected Alternatives: Leaving the managed copy because the path is editor-only. The shared baker helper is reused by texture authors, and it should not normalize wasteful validation patterns.

Scalability potential: Low through Ultra keep the same texture contracts. Larger masks get proportionally more benefit because validation no longer creates a full managed color array.

Hardware Impact: Player runtime remains 0 us. Editor memory pressure drops by one RGBA32 array copy per MRAO validation pass.

## Decision 18 - Explicit Cold Allocation Proof On Shared MPB

Problem: The new shared `MaterialPropertyBlock` scratch in `FaunaBrain` had an informal cold-allocation comment rather than the mandated `Type[capacity]`, reason, and owner proof.

Solution: Updated the comment to name `MaterialPropertyBlock[1]`, the renderer-copy reason, and `FaunaBrain` as owner.

Rejected Alternatives: Leaving the comment as-is because the allocation is already outside hot paths. The code is correct, but audit proof must also be unambiguous.

Scalability potential: Low through Ultra use the same single MPB scratch route. Quality scaling remains in baked texture assets and shader data, not material instances.

Hardware Impact: Runtime behavior is unchanged. Audit clarity prevents future reintroduction of per-predator MPB allocation.
