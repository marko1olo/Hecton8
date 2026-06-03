# Rationale_1724

## Session Start
Problem: Agent 1724 had no active status or rationale files.
Solution: Created fresh task and rationale files before code edits. Extracted the XML prompt via CLI from CURRENT_BATCH.md.
Rejected Alternatives: Reading neighboring prompt text or relying on chat summary was rejected because batch protocol requires tag-bound extraction.
Scalability potential: Documentation state prevents cross-agent ambiguity; no runtime impact.
Hardware Impact: 0 us frame cost. Editor-only bookkeeping.

## Mandate Selection
Problem: Geological texture baking touches shader runtime, offline GPU compute, AUP coordinate math, import compression, and zero-GC runtime constraints.
Solution: Selected 8 mandate files: MATH_AUP_Determinism_Sync, MATH_Coordinate_Precision_AUP_FloatingOrigin, GPU_Compute_Kernels_Kernels_Optimization_MX350, REND_Shader_Noir_Aesthetics_Dithering_Fog, REND_Terrain_VirtualTexturing, REND_URP_Graphics_HotPath_Optimization_HLOD, STRM_Async_Asset_Upload_Texture_Settings, OPT_Zero_GC_Policy_AllocFree_Mandate.
Rejected Alternatives: Reading AI, audio, networking, or UI mandates was rejected because no direct ownership route exists for this task.
Scalability potential: Low uses smaller baked textures; middle/high/ultra use higher static texture density without runtime shader noise.
Hardware Impact: Moves geological procedural cost to Editor bake. Runtime target gain depends on existing shader noise removal; expected gain is shader ALU reduction, not CPU frame cost.

## Loop 1 Audit Decisions
Problem: The prompt names `Assets/_Project/Art/Shaders/Include/Hecton_Master_Geology.shader`, but that file is absent.
Solution: Treat `Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader` as the active geology equivalent because it owns voxel rock AUP input, packed mask decoding, triplanar axis projection, silt, normal, AO, and cave phosphor paths.
Rejected Alternatives: Creating an unreferenced master geology shader would satisfy the filename superficially while leaving active rock rendering untouched.
Scalability potential: Low/middle/high/ultra all benefit because the active shader becomes a static texture consumer; ultra spends saved ALU on denser baked maps.
Hardware Impact: Expected runtime gain is fragment ALU reduction on MX350-class GPUs; exact us saved requires Unity GPU profiler after shader import.

Problem: Ore physical placement is already deterministic and AUP-based in `ProceduralOreSpawner.cs`.
Solution: Keep the spawner as gameplay authority and align baked ore veins by shared AUP coordinate/seed semantics instead of adding a direct spawner dependency to the baker.
Rejected Alternatives: Querying ore spawner data in the Editor baker was rejected because it would invent a brittle cross-agent dependency and risk stale bake data.
Scalability potential: Low renders the same collision truth with lower-resolution masks; middle/high/ultra increase mask resolution and vein detail only.
Hardware Impact: 0 us CPU runtime cost from the baker. Visual alignment uses existing spawner coordinates without new polling.

Problem: Editor compute baking must not leak GPU allocations across domain reload or failed bakes.
Solution: Follow local 1721/1605 patterns: structured `ComputeBuffer` payload, `RenderTexture.enableRandomWrite`, ceil thread groups, coordinate guard in HLSL, `try/finally` release, and `OnDisable` cleanup.
Rejected Alternatives: CPU-only texture synthesis was rejected because the prompt requires ComputeShader dispatch and because 4K CPU iteration would slow authoring.
Scalability potential: Low 1024/512, middle 2048/1024, high 3072/1536, ultra 4096/2048 static outputs from one continuous quality scalar.
Hardware Impact: Editor-only GPU work. Player runtime memory uses compressed static assets; no steady-state allocations.

Problem: Vertical strata must align across adjacent rock meshes without relying on object UV islands.
Solution: Compute strata from finite AUP world position: `warpedY = aupY + periodicWarp(aupXYZ) * warpMeters`; color bands index `frac(warpedY / strataPeriodMeters)`. Periodic warp and Repeat wrap keep tile borders matched.
Rejected Alternatives: Mesh-local UV strata and per-object random offsets were rejected because they break cliff continuity.
Scalability potential: Low uses broad layers and fewer fracture octaves; middle/high/ultra add finer baked fracture/vein features with the same authority route.
Hardware Impact: Replaces per-pixel runtime 3D noise with texture sampling. Estimated shader saving target: multiple `ValueNoise3`-style ALU groups per fragment removed from geology path.

Problem: Runtime registry polling would violate cold-DI rules if geology parameters were fetched each frame.
Solution: Text sweep found no active geology `GlobalRegistry.Get<` hot route in associated rendering/world files. No refactor required for this task.
Rejected Alternatives: Editing unrelated rendering managers was rejected because it would expand blast radius without evidence.
Scalability potential: No direct visual impact; preserves deterministic ownership route.
Hardware Impact: 0 us change. Audit prevents adding a new hot dependency path.

## Loops 2-3 Implementation Decisions
Problem: The geology baker needs high-detail basalt/shale/sandstone/ore history without runtime material or texture creation.
Solution: Added `GeologicalStrataBaker1724.cs` as an EditorWindow-only bake path. It creates random-write RenderTextures, sends a structured parameter buffer to `GeologicalStrataBaker1724.compute`, reads back to Editor `Texture2D`, writes PNGs, enforces import settings, and releases all GPU state in `finally`, `OnDisable`, and `OnDestroy`.
Rejected Alternatives: Runtime `new Texture2D`, `new Material`, and material cloning were rejected because player steady state must allocate 0 bytes and keep SRP batcher paths stable.
Scalability potential: Low 1024 albedo / 512 MRAO; middle around 2048/1024; high around 3072/1536; ultra 4096/2048. Only static asset resolution changes.
Hardware Impact: Editor-only bake cost. Runtime cost becomes two added texture samples when `_GeologyStrataBlend` is enabled, replacing multiple procedural fragment noise paths.

Problem: The legacy rock mask describes smoothness in B while the new geology MRAO contract requires roughness in G.
Solution: The baker writes MRAO as R metallic, G roughness, B AO, A sediment. The rock shader samples `_GeologyStrataMraoMap`, converts `1 - roughness` into existing smoothness, and blends AO/metallic into the legacy decoded mask.
Rejected Alternatives: Reusing the old packed mask layout was rejected because it would violate the prompt's channel contract and confuse downstream texture validation.
Scalability potential: Low keeps broad readable ore response; middle/high/ultra add tighter vein detail in the same channel layout.
Hardware Impact: One packed mask fetch replaces separate metallic/roughness/AO/sediment lookups; expected VRAM fetch count is controlled.

Problem: Sediment accumulation needs flat-surface behavior, but the offline texture atlas has no per-pixel world normal.
Solution: Bake sediment eligibility into MRAO alpha from ledges/fractures; runtime multiplies it by the already available upward normal mask. This preserves static bake ownership while avoiding fake sediment on vertical walls.
Rejected Alternatives: Baking separate mesh-normal textures or reading mesh topology in the runtime shader was rejected because it adds files and runtime complexity outside the prompt scope.
Scalability potential: Low receives broad silt ledges; middle/high/ultra receive finer static ledge/fracture alpha at higher MRAO resolution.
Hardware Impact: Uses existing normal value and one alpha channel. No extra texture.

Problem: `Hecton_AbyssalVoxelRock.shader` still executed dynamic 3D helper calls in fragment-era material, normal, silt, malfunction, and caustic paths.
Solution: Removed `ValueNoise3` entirely. Geological color/mask identity now comes from baked textures; remaining shimmer uses deterministic triangle pulses or 2D hash without 3D noise helper.
Rejected Alternatives: Leaving the helper unused in fragment only was rejected because future edits could silently reintroduce it.
Scalability potential: Low-end avoids fragment ALU spikes; top-tier spends quality on higher baked texture density rather than live noise.
Hardware Impact: Static scan shows zero `ValueNoise3` symbols after patch; exact GPU us requires Unity shader profiling.

## Loop 4-5 Verification Decisions
Problem: Full build verification was required, but build preflight initially found CPU 100 with an active dotnet process, then later CPU 34 with no dotnet/csc.
Solution: Ran exactly one `dotnet build Hecton8.slnx --no-restore` after the host became eligible. The shell timed out at 124 seconds and the build left 8 dotnet workers running for more than 5 minutes without diagnostics. Terminated only the workers started by this build.
Rejected Alternatives: Launching repeated build attempts was rejected because it would violate the no-refactoring-loop and no-concurrent-dotnet rules.
Scalability potential: No visual impact. This preserves workstation availability for other agents.
Hardware Impact: Build wall wait consumed about 420 seconds; no player runtime impact.

Problem: Runtime allocation proof must distinguish Editor bake allocations from player runtime.
Solution: `GeologicalStrataBaker1724.cs` allocates `Texture2D`, `RenderTexture`, and `ComputeBuffer` only inside `#if UNITY_EDITOR`; the shader consumes assigned texture properties and scalar uniforms. Static scan of edited runtime scope found no `new Material()` and no `GlobalRegistry.Get<`.
Rejected Alternatives: Runtime-generated geology textures and material clones were rejected because they break SRP batching and allocate managed memory.
Scalability potential: Low/middle/high/ultra all share the same runtime shader route; only offline asset resolution differs.
Hardware Impact: Player steady-state allocation target is 0 B/frame for the 1724 path. GPU cost is fixed texture sampling, not live noise synthesis.

Problem: SRP batcher compatibility can be broken by moving material properties outside `UnityPerMaterial`.
Solution: Added `_GeologyStrataBlend`, `_GeologyOreGlintStrength`, and `_GeologySedimentStrength` inside `UnityPerMaterial`; added texture declarations as standard shader texture properties. No material cloning route was added.
Rejected Alternatives: Per-rock unique material instances were rejected. Three shared materials plus MPB/instancing offsets remain the target route for 1000 rocks.
Scalability potential: Cheap devices share materials and lower-res bakes; ultra devices can assign denser textures with the same SetPass ownership.
Hardware Impact: Expected SetPass count remains material-count-bound, not rock-count-bound. Exact SetPass proof requires Unity frame debugger.

## Apex Polish Pass
Problem: The Editor baker still had a per-bake managed array payload and MRAO validation used `GetPixels32()`, which copies texture data into a managed array.
Solution: Reused one static `GeologyBakeParams1724[1]` upload payload, read MRAO validation through `GetRawTextureData<Color32>()`, and validated the GPU DTO stride via `UnsafeUtility.SizeOf<GeologyBakeParams1724>()` with 8-byte alignment.
Rejected Alternatives: Ignoring allocations because the baker is Editor-only was rejected; the protocol required static allocation policing.
Scalability potential: Low and ultra authoring paths both use the same allocation-stable validation code; larger ultra bakes avoid a larger managed MRAO copy.
Hardware Impact: Avoids one managed array allocation per bake and one managed `Color32[]` copy sized to MRAO pixels; on 2048 MRAO this avoids about 16 MB managed copy pressure.

Problem: Runtime geology sampling originally reused `_Tiling`, while the compute baker defines physical scale through `tileMeters`.
Solution: Added `_GeologyWorldOriginAup` and `_GeologyTileMeters` to the shader material contract and derived baked geology UVs from `(AUP - origin) / tileMeters`; tile meters are finite-guarded before reciprocal.
Rejected Alternatives: Keeping base texture tiling was rejected because it changes the mathematical strata period and can desync neighboring rocks from the bake.
Scalability potential: Low, middle, high, and ultra bakes now share the same physical AUP layer scale; only texture density changes.
Hardware Impact: Two scalar subtracts and reciprocal setup in the sampler replace a wrong shared tiling assumption. Runtime cost is negligible relative to texture sampling; visual stability gain is deterministic layer alignment.

Problem: 1724 duplicated asset folder/name/import/write helpers and wrote a JSON proof artifact from the bake path.
Solution: Routed folder normalization, name sanitation, rollback capture, atomic writes, importer audit, and finalization through `ProceduralTextureBaker`; removed the JSON report writer and artifact.
Rejected Alternatives: Keeping local helpers was rejected because 1605 already owns this texture-baker infrastructure; keeping JSON proof was rejected by the current Apex protocol.
Scalability potential: All bake quality tiers now share the same rollback-safe asset transaction route.
Hardware Impact: Runtime impact remains 0 us. Editor failure cases now restore previous texture files and `.meta` state instead of leaving partial outputs.

Problem: The new baker's default output folder initially targeted `Assets/Art/Textures/Geology`, outside the first-party `Assets/_Project` asset root, and baked textures were not directly bindable to a shared rock material.
Solution: Corrected the default output to `Assets/_Project/Art/Textures/Geology`; added optional target-material contract validation plus property snapshot/restore for `_GeologyStrataAlbedoMap`, `_GeologyStrataMraoMap`, `_GeologyStrataBlend`, `_GeologyWorldOriginAup`, and `_GeologyTileMeters`.
Rejected Alternatives: Leaving designers to manually bind outputs was rejected because it allows AUP/tile mismatch; writing outside `_Project` was rejected because it violates the project folder contract.
Scalability potential: Low, middle, high, and ultra bakes now land in the same first-party texture family route and can bind shared materials without material clones.
Hardware Impact: Runtime impact remains 0 us. Editor failure cases restore previous material bindings rather than leaving half-applied geology texture contracts.

Problem: The compute upload DTO carried stale fields and the Editor route still used the older `ComputeBuffer` API.
Solution: Removed the unused DTO vector, switched to `GraphicsBuffer`, and kept `UnsafeUtility.SizeOf<GeologyBakeParams1724>()` as the authoritative stride check. The current DTO is 48 bytes, a multiple of 8.
Rejected Alternatives: Keeping the padded 64-byte payload was rejected because unused upload bandwidth is still waste on compact and UMA devices.
Scalability potential: Low and ultra bakes share the same compact upload contract; texture resolution changes continuously while DTO layout remains stable.
Hardware Impact: Saves 16 bytes per parameter upload and removes stale buffer contract ambiguity. Runtime impact remains 0 us because the route is Editor-only.

Problem: The compute kernel used `pow()` for fixed ore/fracture shaping.
Solution: Replaced fixed exponents with multiply-only saturate helpers for x^7, x^9, and x^12 masks.
Rejected Alternatives: Keeping generic exponent calls was rejected because fixed artistic masks do not need a general function.
Scalability potential: Weak devices get faster Editor bakes; high/ultra can spend the saved bake cost on higher resolution static maps.
Hardware Impact: Editor GPU ALU reduced in fracture/ore masks. Runtime impact remains 0 us.

Problem: Author-entered tile height could desync vertical strata repeat, producing a top/bottom phase seam despite Repeat wrapping.
Solution: Sanitization now snaps `TileMeters.y` to an integer multiple of finite `StrataPeriodMeters`, preserving AUP-Y band phase at the texture border.
Rejected Alternatives: Hiding seams with extra blend noise was rejected because it would mask a mathematical contract failure.
Scalability potential: Low/middle/high/ultra all preserve the same physical layer period; only map density changes.
Hardware Impact: 0 us runtime. One Editor-side clamp/snap prevents seam artifacts without shader cost.

Problem: The new `_GeologyStrataMraoMap` packed texture was outside the first-party channel-pack validation route.
Solution: Extended `HectonMaterialChannelPackValidator` with a supplemental packed mask pass for `_GeologyStrataMraoMap`, preserving the existing mandatory `_Mask_Map` contract.
Rejected Alternatives: Duplicating a 1724-only validator was rejected because channel packing already has a first-party owner.
Scalability potential: All quality tiers keep the same MRAO import contract: linear data, mipmaps, repeat wrap, platform compression.
Hardware Impact: Runtime 0 us. Editor audit prevents accidental sRGB/uncompressed packed masks that would waste VRAM or corrupt roughness/AO/sediment values.

Problem: The albedo bake wrote linear RGB bytes into a PNG that is imported as sRGB, which would darken runtime sampling.
Solution: Added a no-`pow()` linear-to-sRGB approximation in the compute kernel and read albedo back through a linear `Texture2D` so the encoded PNG bytes survive until sRGB import converts them back at sample time.
Rejected Alternatives: Disabling sRGB import for albedo was rejected because it would violate the texture-family contract; adding exact `pow()` encoding was rejected by the static ALU/token gate.
Scalability potential: Low through ultra all get stable perceived rock color with the same static asset route.
Hardware Impact: Editor-only ALU cost. Runtime 0 us; avoids a visual brightness regression without adding shader work.

Problem: PNG encoding results were written without checking for null or zero-length output.
Solution: Added an explicit encoder gate before `TryWriteBytesAtomic`; failed encoding restores the captured asset rollback snapshots.
Rejected Alternatives: Relying on the writer to reject bad byte arrays was rejected because encoder failure should be owned by the baker.
Scalability potential: All quality tiers fail cleanly under texture encode failure instead of leaving partial outputs.
Hardware Impact: Runtime 0 us. Editor failure path is deterministic and rollback-safe.

Problem: Sediment and metallic ore shared fracture regions, so silt could visually dull copper/titanium veins in the packed MRAO result.
Solution: Suppressed sediment by ore mask in the compute kernel and gated sediment roughness lift by non-metallic material.
Rejected Alternatives: Runtime material-specific sediment filtering was rejected because the packed bake already has the ore mask and should own the conflict.
Scalability potential: Low keeps readable ore highlights; middle/high/ultra preserve sharper vein glints without extra runtime samples.
Hardware Impact: Runtime 0 us. Editor kernel adds two saturate/multiply operations and avoids an art regression.

Problem: Horizontal geology projection reused a side-axis X/Y projection, causing top and bottom rock surfaces to under-use Z variation while still needing AUP-Y layer continuity.
Solution: Kept side projections as Z/Y and X/Y, then added a diagonal XZ/Y projection for horizontal faces. The Y coordinate remains absolute AUP height.
Rejected Alternatives: Sampling full XZ plus a separate Y layer lookup was rejected because it would add texture samples or split the baked contract.
Scalability potential: Weak devices keep the same two-sample triplanar route; ultra bakes benefit from richer top-surface strata detail at higher texture density.
Hardware Impact: Runtime adds one multiply/add only on UV resolve, no extra texture fetches.

Problem: A robust `.meta` scan found old orphan metadata outside the 1724 ownership boundary.
Solution: Verified the new 1724 `.cs` and `.compute` metas have matching source files; did not delete unrelated `Assets/Shapes/...` and prefab orphan metas.
Rejected Alternatives: Deleting non-domain package/prefab metadata was rejected because the geology baker has no ownership proof for those assets.
Scalability potential: No runtime impact. Keeps this agent's asset hygiene exact without cross-domain damage.
Hardware Impact: 0 us runtime. Static repository hygiene risk remains outside 1724 scope.

Problem: `_GeologyStrataBlend` defaulted to 0, but the fragment path still sampled baked geology albedo and MRAO textures before lerping them away.
Solution: Added a uniform `[branch]` around geology texture sampling and mask blending; default-off materials now keep the legacy base/mask route.
Rejected Alternatives: Keeping unconditional samples was rejected because it taxes every rock material before geology bake adoption.
Scalability potential: Weak devices avoid two unnecessary triplanar texture sample pairs on unbaked/default rocks; high/ultra still get the baked geology path when blend is enabled.
Hardware Impact: Runtime GPU fetches drop on `_GeologyStrataBlend == 0`; exact us requires shader profiler. CPU impact remains 0 us.

Problem: The baker UI allowed zero ore intensity and zero sediment strength while validation required metallic and sediment pixels.
Solution: Added minimum nonzero constants for ore and sediment, applied them to Range attributes, EditorWindow sliders, and sanitized settings.
Rejected Alternatives: Letting the bake fail after dispatch was rejected because that wastes GPU/editor time and contradicts the prompt's required ore/sediment masks.
Scalability potential: Low through ultra bakes always contain the visual facts the runtime shader expects; intensity still scales continuously from subtle to heavy.
Hardware Impact: Runtime 0 us. Editor avoids deterministic failed bakes for zero mask settings.

Problem: The full orphan `.meta` scan produced many paths, but ownership was unclear.
Solution: Compared against one `git ls-files` meta list; all 189 orphan metas are tracked. No deletion was performed from agent 1724.
Rejected Alternatives: Mass-deleting tracked third-party/generated material metas and unrelated prefab metas was rejected as cross-domain destructive work.
Scalability potential: No runtime impact; leaves a clear integrator-level hygiene item instead of causing asset GUID churn.
Hardware Impact: 0 us runtime.
