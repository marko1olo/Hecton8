# Rationale 1718 - Silt Particle Flipbook & Snow Mask Baker

## Decision 001 - Offline Visual Fake Boundary

Problem: Runtime marine snow and silt must not spend CPU or fragment budget on procedural texture/noise generation.
Solution: Keep all noise evaluation, mask packing, normal derivation, validation, PNG serialization, and import setting enforcement inside an Editor-only baker under `Assets/_Project/Editor/Bakers/`.
Rejected Alternatives: Runtime `Texture2D` mutation, per-particle CPU noise, and physical marine-snow simulation were rejected because the mandates prefer authored flipbooks, shader drift, and GPU-side sampling for presentation-only particles.
Scalability potential: Low uses smaller atlases with the same 8x8/64-frame topology; middle keeps full readability; high increases atlas resolution and normal response; ultra spends saved runtime budget on denser and better lit particulate layers.
Hardware Impact: i3/MX350 gain is expected from replacing runtime procedural/noise work with BC7-compressed texture fetches. Microseconds remain STATIC_ESTIMATE until Unity/player profiling exists.

## Decision 002 - Channel Packing Contract

Problem: Separate opacity, emissive, flow, and AO textures would multiply fragment fetches and VRAM residency.
Solution: Pack opacity into R, biolum emissive into G, flow distortion into B, and AO into A for the mask atlas; bake normal data into one separate normal atlas.
Rejected Alternatives: Separate grayscale masks were rejected because they increase texture fetches and asset count without adding gameplay truth.
Scalability potential: Low keeps same shader contract with smaller static textures; high and ultra increase source resolution without changing runtime material semantics.
Hardware Impact: One packed mask replaces four candidate mask fetches. Claimed 75% mask-fetch reduction is architectural/static until shader integration is profiled.

## Decision 003 - Cyclic Noise Instead Of Crossfade Runtime Blend

Problem: A flipbook that pops at frame wrap forces runtime crossfades or duplicated texture samples.
Solution: Evaluate Simplex noise in 4D with temporal coordinates on a unit circle: `z=cos(tau*time)`, `w=sin(tau*time)`. Worley feature points also orbit within a clamped cell.
Rejected Alternatives: Runtime flipbook crossfade was rejected because it doubles particle texture fetch pressure. Linear 2D scrolling noise was rejected because the loop seam remains visible.
Scalability potential: Low uses smaller source atlases with the same 64-frame loop math; middle/high/ultra raise atlas size and visual strength without changing shader authority.
Hardware Impact: MX350 path avoids a second flipbook sample for seam hiding. Saved cost is static until material integration is profiled.

## Decision 004 - Offline Normal Map From Density Gradient

Problem: Flat particulate sprites do not catch boat headlight direction unless the shader either derives normals at runtime or samples authored normals.
Solution: Bake central-difference density gradients into a BC5 normal atlas with positive Z floor and `normalizesafe` fallback.
Rejected Alternatives: Fragment shader gradient/noise was rejected because it spends ALU per pixel on every particle layer. Mesh particles were rejected because they raise vertex/overdraw pressure without enough visual return.
Scalability potential: Low keeps weaker normals and smaller atlases; middle/high/ultra can raise normal strength and resolution for stronger headlight response.
Hardware Impact: Replaces per-fragment procedural normal work with one compressed normal sample. Expected low-end gain is fragment ALU reduction, not CPU gain.

## Decision 005 - Bounded Editor Burst Job

Problem: Per-pixel C# loops over 4096 atlases are slow and easy to allocate in hidden ways.
Solution: Use an editor-only `IJobParallelFor` with `[BurstCompile]`, `NativeArray<Color32>` outputs, and explicit dispose after synchronous completion.
Rejected Alternatives: Runtime compute bake was rejected because generated texture truth is static authoring data. Managed `Color32[]` pixel loops were rejected because they allocate and scale badly for repeated bakes.
Scalability potential: Low/middle can bake smaller atlases quickly; high/ultra can spend editor time on 4096 atlases and 64 frames.
Hardware Impact: Runtime frame cost is 0 us. Editor bake gains are expected from Burst/native parallelism; exact wall-time awaits a non-saturated machine.

## Decision 006 - Importer Compression Contract

Problem: Raw RGBA flipbooks would waste VRAM and upload bandwidth, especially at 4096 resolution.
Solution: Configure mask atlases as BC7 on Standalone and ASTC_6x6 on mobile; configure normal atlases as BC5 on Standalone and ASTC_6x6 on mobile; disable readability after import.
Rejected Alternatives: Default importer settings were rejected because Unity can leave generated PNGs readable, sRGB, or non-optimal compressed. Separate per-channel textures were rejected because they inflate fetch count and residency.
Scalability potential: Low uses smaller atlas caps; middle/high/ultra retain the same compression contract at larger source sizes.
Hardware Impact: 4096 RGBA source memory is converted to compressed GPU residency after import. Low-end devices benefit from less VRAM pressure and fewer cache misses.

## Decision 007 - Build Gate Honesty

Problem: Task 20 requires compile verification, but AGENTS forbids launching `dotnet build` while CPU is above 50% or another dotnet/csc process is running.
Solution: Sampled CPU and process state first. CPU returned 100/100/100 and dotnet PIDs 3100/32404 were active, so no new build was launched. Unity MCP `validate_script` was used as script-level proof and returned zero diagnostics.
Rejected Alternatives: Starting another build was rejected because it violates the explicit machine gate and risks starving other agents.
Scalability potential: The proof route is deterministic: run full `dotnet build C:\hades\hades.sln` only when CPU and compiler gate clear.
Hardware Impact: Avoided additional CPU contention on a saturated workstation. Saved time is not claimed as runtime gain.

## Decision 008 - No Runtime DataVault Owner

Problem: Particulate mask data could be incorrectly treated as a hot global runtime fact, creating compaction fence and ownership problems.
Solution: Keep baked textures as Unity assets. Runtime consumers should receive references through material/renderer setup, not DataVault or GlobalRegistry polling.
Rejected Alternatives: GlobalDataVault storage was rejected because these are art assets, not mutable cross-domain native state. GlobalRegistry hot polling was rejected because scans showed cached rebind patterns and no `GlobalRegistry.Get<` use in VFX.
Scalability potential: Low-to-ultra scaling remains asset/material driven. Gameplay truth, save identity, and DTO layout are untouched.
Hardware Impact: 0 B/frame and 0 us/frame from the baker path; runtime only pays for normal texture fetch if a material chooses to use it.

## Decision 009 - Shared Importer And Rollback Reuse

Problem: The first 1718 implementation duplicated texture importer enforcement and had no two-output rollback transaction.
Solution: Reuse `ProceduralTextureBaker.TryEnforceTextureImportSettings`, `TryFinalizeAssetDatabase`, `TryWriteBytesAtomic`, and rollback snapshots. Add the missing two-file rollback overload to the shared baker and make the shared importer set/audit `alphaIsTransparency` for masks.
Rejected Alternatives: Keeping a private 1718 importer/audit path was rejected because it creates drift against the first-party baker contract. Writing custom cleanup was rejected because the shared rollback path already restores/deletes matching `.meta` files.
Scalability potential: All baker domains now share the same BC7/BC5/ASTC/readability rules; low/middle/high/ultra texture outputs differ by source size, not by import policy.
Hardware Impact: Runtime unchanged at 0 us/frame. Editor failure path is safer: failed normal import rolls back the mask output instead of leaving half-valid assets.

## Decision 010 - Source Proof Over Bake Report I/O

Problem: The previous bake path emitted JSON proof during normal baker execution, conflicting with the latest source-only proof directive and adding unnecessary editor I/O.
Solution: Remove the JSON writer and delete the generated report file. Keep proof in source structure, validator output, hashes, and status memory.
Rejected Alternatives: Keeping both report and source proof was rejected because it adds another artifact to keep coherent without improving runtime or asset quality.
Scalability potential: Bake path stays focused on texture assets only; platforms differ through importer settings, not proof artifacts.
Hardware Impact: Removes report file I/O from the baker. Runtime remains 0 us/frame.

## Decision 011 - Shared VRAM-Aware Atlas Clamp

Problem: The first 1718 atlas resolver duplicated power-of-two size logic and did not use the existing compact-VRAM clamp.
Solution: Route atlas size through `ProceduralTextureBaker.ResolveSafeTextureSize(MaximumAtlasSize, GlobalQualityWeight)` and enforce the particulate minimum of 1024 afterward.
Rejected Alternatives: Local power-of-two resolver was rejected because it ignores the first-party `SystemInfo.graphicsMemorySize` compact path.
Scalability potential: Low gets 1024, middle/high scale through the shared policy, ultra reaches 4096 only when the shared baker allows it.
Hardware Impact: Protects compact GPUs from accidental 4K editor output when the shared policy clamps to 2K.

## Decision 012 - Partial Extension Instead Of Parallel Baker Manager

Problem: A separate top-level `ParticleFlipbookBaker1718` class looked like a second baker manager beside the first-party `ProceduralTextureBaker`.
Solution: Make `ProceduralTextureBaker` partial and put the 1718 flipbook menu/generation code in `ParticleFlipbookBaker1718.cs` as a partial extension. Remove the unused output set struct, output array allocation, and `Stopwatch`.
Rejected Alternatives: Keeping a separate top-level class was rejected because it violates the no-parallel-manager polish rule when the existing baker can own the domain. Keeping output metrics was rejected after JSON/report I/O was removed.
Scalability potential: Future particulate variants can extend the same baker surface without adding new manager classes. Low/middle/high/ultra remain data-driven through quality weight and shared import policy.
Hardware Impact: Runtime remains 0 us/frame. Editor path loses one small managed array allocation and one `Stopwatch` allocation per default bake invocation.

## Decision 013 - Existing Test Class As Source Contract

Problem: The source invariants could drift back toward a separate manager or duplicate importer logic after this pass.
Solution: Extend `ProceduralTextureBaker1605EditTests` with string-level source checks for the 1718 partial extension, shared importer use, periodic noise, normal baking, and forbidden runtime dependency tokens.
Rejected Alternatives: Creating a new test class was rejected because the existing baker test class already owns this source-contract style.
Scalability potential: The test protects the baker stack as a single topology. Future agents get a fast editor-test signal if they reintroduce drift.
Hardware Impact: Runtime unchanged; test runs only in EditMode.

## Decision 014 - Fixed 64-Frame Runtime Contract

Problem: The task text contains a low-tier suggestion for fewer frames, but the primary directive requires 64-frame cyclic flipbooks and the runtime shader contract is simplest when every atlas is 8x8.
Solution: Keep `RequiredFrameGridSize = 8` for every quality weight. Use `GlobalQualityWeight` for atlas resolution, density exponent, and normal/visual weight, not for frame topology.
Rejected Alternatives: A 4x4 low-tier variant was rejected because it adds runtime material branching or per-asset metadata checks and weakens animation consistency. Binary low/high routing was rejected by the scalability pillar.
Scalability potential: Low uses 1024 8x8 with cheaper texels; middle/high/ultra increase source resolution and preserve identical shader UV math.
Hardware Impact: MX350 avoids extra material variants and branchy frame-count logic. Runtime samples remain one mask atlas and one optional normal atlas.

## Decision 015 - Shader Consumers Sample The Baked Contract

Problem: An offline baker without material consumers only creates assets; the visual pipeline would still fall back to radial dots and hash silt.
Solution: Add `_MarineSnowMaskAtlas`, `_MarineSnowNormalAtlas`, `_SiltMaskAtlas`, and `_SiltNormalAtlas` to the existing shaders. Blend baked density over the old procedural/radial fallback and use normal atlas data for headlight response.
Rejected Alternatives: Removing fallback shape math was rejected because existing materials without baked textures must not go black. A new shader family was rejected because it would duplicate the VFX material stack.
Scalability potential: Low can assign smaller baked atlases with reduced normal weight; ultra can push stronger normals and biolum masks without changing runtime code.
Hardware Impact: Main gain is replacing procedural morphology with atlas fetches. Cost is bounded to existing particle fragments; no CPU simulation or texture mutation is introduced.

## Decision 016 - Cached Runtime Material Binding

Problem: Shader properties need atlas references, but runtime must not search scene objects or poll registries in hot phases.
Solution: `HectonMarineSnowRenderer` caches shader property IDs and last-bound atlas/parameter values. Binding updates only when serialized references or scalar settings change.
Rejected Alternatives: Per-frame blind `SetTexture`/`SetVector` calls were rejected because they dirty material state unnecessarily. `GlobalRegistry.Get<` and `GetComponent()` lookup routes were rejected because atlas references are renderer-owned presentation data.
Scalability potential: Low/middle/high/ultra tuning stays in serialized floats and texture assets. Gameplay truth and DataVault ownership remain untouched.
Hardware Impact: Hot path avoids managed allocation and dependency lookup. Runtime binding cost is limited to change detection and material property writes when values differ.

## Decision 017 - Compile Wall Classification

Problem: The build gate eventually cleared, but the single allowed editor build failed outside the 1718 domain.
Solution: Record the failure as an unrelated dependency wall: `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs` references missing `MCPForUnity.Editor`. Keep 1718 proof to Unity MCP script validation and source scans.
Rejected Alternatives: Editing the MCP bridge was rejected because it is outside the particulate baker domain and belongs to another active agent area. Re-running builds repeatedly was rejected by the compilation throttling rule.
Scalability potential: Once the MCP bridge dependency is restored, the same single-build route can verify the full editor assembly without changing 1718 code.
Hardware Impact: No runtime impact. Build throttling avoided CPU contention and orphan compiler pressure.

## Decision 018 - Material Asset Handoff

Problem: Texture assets alone do not guarantee adoption; the search found no existing `.mat`, prefab, or scene reference for `Hecton8/VFX/FlashlightConeSilt`.
Solution: The baker now creates or updates `MAT_Flipbook_*` material assets beside the generated mask and normal textures. The material path is captured in the same rollback set as the two texture paths.
Rejected Alternatives: A new runtime binding manager was rejected because silt cone presentation has no discovered first-party owner in the assigned domain. Manual material setup was rejected because it leaves the baker output incomplete.
Scalability potential: Low/middle/high/ultra material scalars are emitted from the same continuous quality weight and preserve the 8x8 atlas contract.
Hardware Impact: Runtime avoids editor-time asset search and procedural setup. Material creation is editor-only and adds 0 B/frame.

## Decision 019 - Unity Normal Import Compatibility

Problem: The normal atlas is imported as `TextureImporterType.NormalMap`; sampling it as raw `xyz * 2 - 1` can be wrong after Unity normal-map swizzle/compression.
Solution: Both particulate shaders now use `UnpackNormal(normalPacked)` after sampling the baked normal atlas.
Rejected Alternatives: Importing the normal atlas as a generic linear mask was rejected because it would lose the shared BC5/normal-map importer contract. Raw RGB sampling was rejected because it is platform-compression fragile.
Scalability potential: All quality tiers keep the same shader contract and let Unity choose the correct normal-map encoding per platform.
Hardware Impact: Correct BC5 normal decoding protects headlight response on MX350 and mobile ASTC paths without adding CPU work.

## Decision 020 - Default Bake Batch Atomicity

Problem: Baking silt first and marine snow second can leave updated silt assets behind if the snow profile or final AssetDatabase step fails.
Solution: Resolve both profile output paths before writing, capture one rollback snapshot set for all six assets, and restore the full set on any later failure.
Rejected Alternatives: Per-profile rollback was rejected because it protects only the currently failing profile. Keeping partial success was rejected because it creates incoherent material/texture pairs in source control.
Scalability potential: Future particulate profiles can join the same transaction by extending the path array without changing runtime code or shader contracts.
Hardware Impact: Editor-only safety change. Runtime cost remains 0 B/frame and 0 us/frame.

## Decision 021 - Persistent Sampler And Layout Gate

Problem: The baker set clamp/filter/aniso only on the transient `Texture2D` used for PNG encoding, so the imported Unity asset could fall back to default sampler metadata. The new 1718 job/settings structs also lacked an explicit unmanaged layout alignment gate.
Solution: Move sampler policy into shared `ProceduralTextureBaker.TryEnforceTextureImportSettings`: clamp wrap, bilinear filter, and normal-map aniso are now persisted and audited with compression/readability settings. Add `ValidateUnmanagedLayouts1718()` using `UnsafeUtility.SizeOf<ResolvedBakeSettings>()` and `UnsafeUtility.SizeOf<ParticleFlipbookBakeJob>()` before scheduling the editor bake.
Rejected Alternatives: A 1718-only importer override was rejected because sampler drift belongs to the shared baker contract. Ignoring layout validation was rejected because sibling baker domains already enforce 8-byte unmanaged alignment.
Scalability potential: Low/middle/high/ultra generated textures now share one sampler/compression contract; only atlas size and authored detail vary. The layout gate protects future job-field additions without introducing runtime branches.
Hardware Impact: Runtime remains 0 B/frame and 0 us/frame. MX350 benefit is correctness: no default Repeat sampler drift on flipbook edges, and no unverified job struct alignment before editor generation.

## Decision 022 - Flow Channel Consumption And Half-Texel Frame Inset

Problem: The baker packed flow distortion into the mask B channel, but the existing shader consumers did not use that channel as a presentation distortion input. Their atlas UVs also sampled exact frame-cell boundaries at quad corners, leaning on padding instead of avoiding boundary taps.
Solution: Add `_MarineSnowMaskAtlas_TexelSize` and `_SiltMaskAtlas_TexelSize` based local UV inset helpers. Use the packed B channel to offset normal atlas sampling and lighting direction in both marine snow and flashlight silt shaders.
Rejected Alternatives: A second mask sample for full density re-distortion was rejected because it increases fragment texture pressure. A separate flow texture was rejected because B is already packed for this purpose. Runtime C# distortion was rejected because the domain is presentation shader sampling, not simulation.
Scalability potential: Low keeps the same one packed mask plus normal atlas and gets safer mip/bilinear edges; middle/high/ultra can raise atlas resolution and flow detail without shader variant changes.
Hardware Impact: Runtime C# remains 0 B/frame and 0 us/frame. GPU cost stays bounded to the existing mask/normal texture reads; the B channel now buys visible flow shimmer instead of occupying dead bandwidth.

## Decision 023 - Finite-Safe Gradient And Channel Packing

Problem: The normal bake used `normalizesafe`, but the central-difference deltas, emissive high-frequency noise, flow noise, and final returned density still trusted every upstream noise sample to be finite.
Solution: Wrap neighbor-gradient deltas, high-frequency emissive input, flow input, returned density, and Z-clamped normal reuse through `FiniteOrZero`/`math.normalizesafe` before writing packed mask and normal bytes.
Rejected Alternatives: Ignoring NaN propagation was rejected because one poisoned pixel can enter a serialized atlas and survive import. Adding runtime shader guards was rejected because the defect belongs to the offline authoring step, not the player frame.
Scalability potential: Low/middle/high/ultra all use the same finite-safe 64-frame topology; higher atlas sizes spend editor time on more pixels, not runtime repair code.
Hardware Impact: Runtime remains 0 B/frame and 0 us/frame. MX350 benefit is failure containment: invalid editor noise math cannot become fragment-time sparkle, black pixels, or invalid normal lighting in the shipped atlas.

## Decision 024 - Two-Buffer Padding Validation

Problem: The padding gate validated the packed mask border, but a corrupted normal atlas border could still serialize non-flat normals into the flipbook edge area.
Solution: Extend `ValidatePadding` to inspect both generated buffers before PNG serialization: packed mask padding must be all zero, and normal padding must remain the explicit flat normal byte pattern `(128,128,255,0)`.
Rejected Alternatives: Relying on shader half-texel insets alone was rejected because import/mip behavior can still sample near border data. Adding shader-side normal clamps was rejected because the offline baker can reject the bad asset before it reaches runtime.
Scalability potential: Low/middle/high/ultra all keep the same border invariant; larger atlases only add more editor validation work, not runtime branches.
Hardware Impact: Runtime remains 0 B/frame and 0 us/frame. MX350 benefit is artifact prevention without per-fragment repair logic.
