# Rationale 1605 - SUBSTANCE_SHADER_AND_TEXTURE_BAKER

Date: 2026-06-01
Status: SOURCE FORTIFIED / UNITY EXECUTION BLOCKED

## Decision 01 - Scope Containment

Problem: Texture baker can poison runtime if dispatch code, readbacks, or compression calls leak into player builds.
Solution: All C# lives under `Assets/_Project/Editor/Bakers/`; compute shader is an asset only invoked by Editor menus. Runtime sees generated PNG/material/mesh assets only.
Rejected Alternatives: Runtime `Texture.Compress()` and runtime compute bake. Both violate VRAM discipline and frame-time law.
Scalability potential: Low uses smaller bake resolution/imported mips; Middle uses 2K atlases; High uses 4K; Ultra can generate denser albedo/normal detail while sharing the same material route.
Hardware Impact: i3/MX350 gains from no runtime bake, no extra SetPass family, no unique material clones. Estimated hot-path gain: 0 us measured absent; expected prevention is whole-frame spike avoidance.

## Decision 02 - Evidence Format

Problem: XML task asks for JSON report, latest operator command rejects useless JSON dumps and binary dumps.
Solution: Keep proof in Status/Rationale/LOG and code-level validators. Do not emit a JSON report unless a future explicit command restores that requirement.
Rejected Alternatives: Generating `Docs/Reports/TEXTURE_SYNTHESIS_BAKER_1605.json` as paperwork. Current directive says texture assets and updated UVs are the proof.
Scalability potential: Low/Middle/High/Ultra evidence remains tied to importer settings, atlas efficiency, and UV remap tests, not dump volume.
Hardware Impact: No runtime impact. Editor disk churn reduced.

## Decision 03 - Texture Packing Contract

Problem: Separate albedo/normal/AO/roughness/metal/emissive textures break batching and inflate VRAM.
Solution: Generate albedo, BC5 normal, and single M.R.A.O. mask where R=metallic, G=roughness, B=AO, A=emissive. Atlas by texture family to support BRG/GPU Resident Drawer material sharing.
Rejected Alternatives: Four separate mask maps or per-object unique materials. Both increase texture samples and state changes.
Scalability potential: Low uses 512/1024 source tiles in shared atlas; Middle 2K; High/Ultra 4K with richer noise and longer mip residency.
Hardware Impact: One mask sample instead of four. Estimated shader texture-fetch savings: PENDING GPU CAPTURE; static expectation is 3 fewer mask fetches per fragment.

## Decision 04 - Compute Shader Thread Groups

Problem: Texture bake kernels must be portable across MX350 and mobile-style compute limits.
Solution: Use `[numthreads(8,8,1)]` for 64 threads per group and query group dimensions from C# before dispatch.
Rejected Alternatives: Hardcoded 256-thread or fixed `size / 8` dispatch. Those break mandate warp sizing and tail coverage.
Scalability potential: Low/Middle/High/Ultra change texture size and noise richness, not the portable kernel contract.
Hardware Impact: Avoids oversized register pressure on compact GPUs. Microseconds saved: PENDING GPU CAPTURE.

## Decision 05 - Atlas Bleed

Problem: Mips average neighboring atlas islands and create black/foreign-color seams on distant coral and rocks.
Solution: Pack padded rects and copy edge pixels outward by 4 px around each island; UV remap targets the interior rect only.
Rejected Alternatives: No padding, shader-side UV clamp, or grid packing. Shader clamp costs runtime ALU/sampler state; no padding fails in mips.
Scalability potential: Low keeps atlas smaller but still padded; Middle/High/Ultra can use 4K atlas with the same seam guarantee.
Hardware Impact: Runtime cost is 0 us; offline extra pixels buy stable mips and fewer draw/material routes.

## Decision 06 - Mesh UV Overwrite Route

Problem: Raw YAML mesh edits can corrupt FileIDs and vertex streams; `Mesh.uv` may fail when generated meshes are uploaded read-only. Unity console proved `Mesh.GetVertexBufferData` is absent in this project API surface, and official Unity 6.4 docs state `Mesh.AcquireReadOnlyMeshData` throws when `isReadable` is false.
Solution: Resolve UV0 vertex-buffer stream, extract with Editor-only `MeshUtility.AcquireReadOnlyMeshData().GetVertexData<byte>()`, run `RemapMeshUVsJob`, write with `Mesh.SetVertexBufferData`, then dirty/save asset.
Rejected Alternatives: YAML mutation, managed `mesh.uv` rewrite only, unavailable `Mesh.GetVertexBufferData`, and runtime `Mesh.AcquireReadOnlyMeshData`. YAML is unsafe; managed UV rewrite may not reach uploaded mesh buffers; runtime mesh data acquisition can reject unreadable generated assets.
Scalability potential: Low/Middle/High/Ultra all share one atlas-material route. Strong hardware spends saved draw calls on richer shader detail.
Hardware Impact: Editor-only cost. Runtime gains are fewer material families and BRG-compatible UVs; exact microseconds PENDING Frame Debugger.

## Decision 07 - Build Contention

Problem: Current host CPU is 97.69-100% and Unity embedded `dotnet.exe` is already running.
Solution: Do not launch `dotnet build`; mark Task 15 as `BLOCKED_BY_CONTENTION` and use static audits until CPU/compiler contention clears.
Rejected Alternatives: Starting another build. Violates user order and cluster CPU law.
Scalability potential: No runtime impact.
Hardware Impact: Prevents additional CPU saturation on the host. Microseconds saved for game runtime: 0; workstation contention avoided.

## Decision 08 - M.R.A.O. Verification Must Respect Profile Truth

Problem: Generic M.R.A.O. verification required every sampled channel to be non-zero, which would false-fail valid non-emissive mineral masks and zero-metal organic masks.
Solution: Split verification into profile-aware bake verification and generic atlas verification. The bake path accepts zero metallic/emissive when the profile declares them, while still requiring roughness, AO, and RGB channel divergence.
Rejected Alternatives: Forcing all profiles to fake non-zero metallic/emissive just to satisfy a brittle audit. That pollutes material truth and wastes shader interpretation.
Scalability potential: Low keeps cheap non-emissive masks valid; Middle/High/Ultra can raise emissive/metal values per profile without changing texture layout.
Hardware Impact: Runtime 0 us. Editor prevents false abort before texture assets exist.

## Decision 09 - Foreign Compile Wall

Problem: Unity did not reach ready state after `refresh_unity compile:none`, MCP `read_console` ping failed, and Editor.log still contains non-1605 compile errors in core/narrative/flora/player domains.
Solution: Stop at source-complete state for 1605, record the blocker, and do not edit foreign files without domain authorization.
Rejected Alternatives: Launching external `dotnet build`, patching unrelated systems, or generating fake PNG evidence outside the compute baker path.
Scalability potential: No runtime impact; preserves domain ownership for parallel agents.
Hardware Impact: Avoided additional build contention and avoided fake asset churn. Runtime microseconds remain pending until Unity can execute the bake and Frame Debugger/GPU capture can inspect BRG texture state.

## Decision 10 - APEX Source Gate Instead Of Prose Proof

Problem: Verbal confirmation cannot prove that 1605 did not introduce hot `GlobalRegistry.Get<T>()`, `GetComponent()`, phase leakage, DataVault lock nesting, or build-spawn paths.
Solution: Add `ApexIntegratorVerifier1605` as Editor-only C# source. It strips comments and string/char literals in memory, extracts method bodies, scans hot methods (`Tick`, `FixedTick`, `LateFrameTick`, `Update`, `FixedUpdate`, `LateUpdate`, `Execute`), rejects forbidden hot lookups, rejects runtime phase hooks, rejects build-spawn tokens, and enforces single DataVault write lock with `try/finally` release if a future lock appears.
Rejected Alternatives: Chat-only proof, JSON report, or broad runtime refactor outside the assigned baker domain.
Scalability potential: Low/Middle/High/Ultra share the same offline-baked asset route; the verifier prevents runtime authority creep as the baker grows.
Hardware Impact: Runtime 0 us. Editor/test-only static analysis protects the hot path from future 1605 drift.

## Decision 11 - Prompt Extraction Parser Correction

Problem: The first re-extraction command used an exact `<AGENT_PROMPT id="1605">` opener and false-failed because the actual batch tag may carry attributes.
Solution: Use an attribute-tolerant CLI regex: `<AGENT_PROMPT\b[^>]*id="1605"[^>]*>...`. Current `CURRENT_BATCH.md` returns a 19947-character 1605 block.
Rejected Alternatives: Trusting stale chat memory or scanning neighboring prompts.
Scalability potential: No runtime impact; protects agent state from batch-file drift.
Hardware Impact: 0 runtime. Negligible CLI I/O.

## Decision 12 - Continuous Quality Weight

Problem: The baker scaled by VRAM and profile texture size but did not consume a continuous `GlobalQualityWeight`, leaving a hidden binary-quality failure mode against project doctrine.
Solution: Add `GlobalQualityWeight` to `BakeProfileDTO`, schema, defaults, C# safe-size resolution, shader dispatch, and `_BakerQualityParams` in HLSL. The same float now scales bake resolution, high-frequency fBm contribution, pore density visibility, and rust/detail frequency.
Rejected Alternatives: Low/Ultra enum, platform if/else, or hardcoded 2K defaults. Those create binary quality switches and make middle-tier hardware impossible to tune cleanly.
Scalability potential: Low uses 512-1K and suppressed fine pores; Middle raises resolution/detail continuously; High/Ultra reaches 2K-4K with richer pore/rust/normal texture while keeping identical material layout.
Hardware Impact: Runtime 0 us because the path remains Editor-only. Low-end i3/MX350 avoids 4K VRAM allocations during bake; saved runtime cost remains indirect through shared compressed atlas/material routing.

## Decision 13 - Dedicated M.R.A.O. Atlas Shader

Problem: The 1605 baker writes M.R.A.O. as R=metallic, G=roughness, B=ambient occlusion, A=emissive. Existing project `HectonPackedMaskV1` and URP-style mask contracts read G/B differently. Binding the 1605 mask to those legacy properties would invert roughness/AO and create false material response.
Solution: Add `Hecton8/Bakers/MraoAtlasLit` with explicit 1605 decode and bind atlas masks only through `_MraoMap`. Atlas material creation now prefers that shader and does not assign the M.R.A.O. atlas into incompatible legacy mask slots.
Rejected Alternatives: Reusing URP Lit mask properties, converting channel meaning silently at material bind time, or renaming 1605 output without changing shader decode. Those hide the contract bug and make future BRG material audits unreliable.
Scalability potential: Low uses the same one-mask sample with cheaper source resolution; Middle/High/Ultra spend quality on richer baked roughness/AO/rust texture, not extra runtime maps.
Hardware Impact: Runtime texture count stays at albedo + normal + one mask. The correction saves visual debug time and prevents shader-side channel repair branches. Exact microseconds PENDING GPU CAPTURE.

## Decision 14 - Atomic Asset Writes

Problem: Direct PNG overwrite can leave a half-written texture/atlas if Unity import, disk access, or antivirus contention interrupts the write.
Solution: Route direct bake and atlas writes through `TryWriteBytesAtomic`: write bytes to a same-directory `.tmp1605`, then swap with `File.Replace` when supported, use `File.Move` on first creation, and fall back to `.bak1605` restore semantics on platforms where replace is unsupported. Failed writes delete the temp file and preserve the old asset when possible.
Rejected Alternatives: `File.WriteAllBytes` directly to the final path, large binary backup dumps, or JSON telemetry. Direct overwrite risks corrupt assets; backups/reports add I/O without becoming proof of texture correctness.
Scalability potential: Low/Middle/High/Ultra all rely on valid compressed assets. Atomic writes protect every tier from broken import state.
Hardware Impact: Runtime 0 us. Editor I/O reliability improved; no orphan `.tmp1605` or `.bak1605` files were present after static inspection.

## Decision 15 - Seed As Integer Contract

Problem: The compute shader previously carried `BakeProfileDTO.Seed` through `_BakerTextureSize.w`, a float lane. Large `uint` seeds lose lower-bit identity when converted to float, breaking deterministic texture variants.
Solution: Add `_BakerSeed` as a `uint` shader uniform and set it from C# with `compute.SetInt(s_seedId, unchecked((int)seed))`. Shader seed phase values now derive from integer hash salts instead of float-packed seed coordinates.
Rejected Alternatives: Keeping seed in `float4`, limiting seed range in the schema, or using string/profile names as implicit seeds. All three make deterministic bake reproduction weaker.
Scalability potential: Low/Middle/High/Ultra all keep identical seed identity; only resolution/detail changes with `GlobalQualityWeight`.
Hardware Impact: Runtime 0 us. Editor compute spends a few integer hash ops but avoids non-reproducible texture variants and broken atlas cache identity.

## Decision 16 - Readable Source Bridge Without Runtime Readability

Problem: Final generated textures are correctly imported as non-readable for runtime memory, but atlas packing needs CPU pixels. That makes the honest bake-to-pack chain fail unless source textures remain readable forever.
Solution: `TextureAtlasPacker.TryReadTexturePixels` first tries direct pixels, then temporarily flips `TextureImporter.isReadable=true`, reads pixels, and restores the previous readability state.
Rejected Alternatives: Keeping generated BC7/BC5 textures readable permanently, duplicating uncompressed staging PNGs, or requiring manual import setting edits. Those waste RAM or create human-only pipeline steps.
Scalability potential: Low keeps runtime memory tight; Middle/High/Ultra can pack richer generated textures without changing import residency policy.
Hardware Impact: Runtime memory stays lower because final source textures return to non-readable. Editor-only reimport cost is accepted for deterministic offline packing.

## Decision 17 - Asset Path And Atlas Allocation Guards

Problem: A string starting with `Assets/` can still escape through `Assets/../`, and public atlas APIs could allocate arbitrary huge `Color32[]` buffers before failing.
Solution: Atomic writes now resolve full paths and require them to stay under `Application.dataPath`. Atlas packing rejects non-power-of-two, below-512, and above-4096 sizes before allocation.
Rejected Alternatives: Trusting caller strings, relying on OS path normalization after write, or letting oversized atlas requests fail through memory pressure. Those are avoidable Editor crashes.
Scalability potential: Low/Middle/High/Ultra remain bounded by the same max atlas contract; visual quality scales through source detail and count, not unbounded atlas dimensions.
Hardware Impact: Runtime 0 us. Editor avoids pathological allocations beyond the 4K atlas contract.

## Decision 18 - Self-Contained Atlas Shader Frame

Problem: `Hecton_MraoAtlasLit` depended on `BuildTangentToWorld`, but that helper is not guaranteed by the included URP/Core chain and exists locally in other shaders. The same file also left varyings uninitialized before stereo macros.
Solution: Add local `BuildMraoFallbackTangent` and `BuildMraoTangentToWorld`, project tangents against the normal to avoid degenerate TBN frames, preserve tangent handedness, and initialize `Varyings`/`ShadowVaryings` to zero.
Rejected Alternatives: Depending on another shader's private helper, disabling normal maps, or binding the 1605 atlas to a different material shader just to avoid compile risk.
Scalability potential: Low keeps the same shader path with stable normals at cheaper source resolution; Middle/High/Ultra spend saved runtime texture count on richer baked normal detail.
Hardware Impact: Runtime cost is the same one normal sample and one TBN transform. The change removes a shader compile vector; exact GPU timing remains PENDING GPU CAPTURE.

## Decision 19 - No-Throw Readability Restore

Problem: The atlas packer must temporarily enable `TextureImporter.isReadable` for source textures, but `SaveAndReimport` inside `finally` can throw and mask the original atlas read failure or leave assets readable.
Solution: Guard null source textures, preserve the direct read failure message, catch Unity/IO/import exceptions during readable import, and route cleanup through `RestoreTextureReadableState`, which logs a warning instead of throwing from cleanup.
Rejected Alternatives: Permanent readable textures, unmanaged staging copies, or cleanup that can overwrite the primary failure route.
Scalability potential: Low/Middle/High/Ultra keep runtime source textures non-readable after offline packing, while editor packing still works on BC7/BC5 imports.
Hardware Impact: Runtime memory stays lower than readable texture imports. Editor reimport cost is accepted only in the offline pack step.

## Decision 20 - Output Path Inputs Are Data, Not Trust

Problem: Atomic writes protected final file paths, but `EnsureAssetFolder` still accepted raw public `outputFolder` values before folder creation. `atlasName` also flowed into texture/material filenames without sanitation.
Solution: Add `TryEnsureAssetFolder` and `TryNormalizeAssetFolder`, accepting only `Assets`/`Assets/...` with no empty, `.` or `..` segments. Share `SanitizeAssetNameForPath` between profile names and atlas names, and fail pack requests whose atlas name collapses to empty.
Rejected Alternatives: Trusting editor callers, relying only on final file containment, or silently creating nested folders from slash-containing atlas names.
Scalability potential: Low/Middle/High/Ultra all depend on deterministic generated asset identity; path sanitation keeps atlas cache names stable across devices and agents.
Hardware Impact: Runtime 0 us. Editor avoids malformed AssetDatabase operations before bake/pack writes.

## Decision 21 - Deterministic Quality Tier Rounding

Problem: The continuous `GlobalQualityWeight` texture-size resolver used `Mathf.RoundToInt` on exponent values. Half values can be implementation-sensitive, making quality 0.5 ambiguous for a 512..4096 request.
Solution: Use explicit `Mathf.FloorToInt(Mathf.Lerp(minPower, maxPower, quality) + 0.5f)` before clamping, so the midpoint maps deterministically to the 2K exponent in the source test.
Rejected Alternatives: Keeping `RoundToInt`, hardcoding 0.5 to 2K, or replacing continuous quality with named tiers.
Scalability potential: Low/Middle/High/Ultra keep continuous interpolation, but midpoint behavior is stable for automated bake profiles and tests.
Hardware Impact: Runtime 0 us. Editor VRAM selection becomes deterministic; no bake-time measurement until Unity can run.

## Decision 22 - BRG Instancing And Shadow Pass Must Carry Instance Identity

Problem: The dedicated M.R.A.O. shader owned the channel contract, but its varyings did not explicitly transfer Unity instance IDs through the forward and shadow paths, and the shadow pass depended on shadow helpers without including the URP shadow library.
Solution: Include `ShaderLibrary/Shadows.hlsl`, add `UNITY_VERTEX_INPUT_INSTANCE_ID` to varyings, transfer instance IDs in `Vert` and `ShadowVert`, and set up instance IDs in `Frag` and `ShadowFrag`.
Rejected Alternatives: Trusting implicit instance state, disabling shadows for atlas materials, or falling back to a generic URP material with the wrong mask channel meaning.
Scalability potential: Low/Middle/High/Ultra keep one atlas material route; strong devices can spend saved draw calls on denser baked texture detail without losing BRG/shadow compatibility.
Hardware Impact: Runtime delta PENDING GPU CAPTURE. The change removes an instancing/shadow compile vector without adding texture samples.

## Decision 23 - Empty Atlas Pixels Must Be Physically Neutral

Problem: Edge padding copies island borders, but any remaining unfilled atlas pixels can still enter mips. Black empty pixels are catastrophic for normal maps and M.R.A.O. masks because black means invalid normal, zero roughness, and zero AO.
Solution: Prefill atlas buffers before blitting: albedo black opaque, normal neutral `(128,128,255,255)`, and M.R.A.O. neutral `(0,255,255,0)` for metallic 0, roughness 1, AO 1, emissive 0.
Rejected Alternatives: Leaving `Color32[]` default black, running shader-side clamps, or expanding padding until every unused pixel is covered. Defaults poison mips; clamps cost runtime; excessive padding wastes atlas area.
Scalability potential: Low/Middle/High/Ultra all retain stable distant mips. Weak devices benefit most because lower mip levels are sampled more often under smaller texture budgets.
Hardware Impact: Runtime 0 us. Offline memory write is one linear fill per atlas; it buys seam resistance without runtime ALU.

## Decision 24 - Texture Compression Belongs On Confirmed Importer Surface

Problem: `TextureImporterPlatformSettings` examples and docs confirmed platform `format`/`compressionQuality`, but did not prove a platform-level `textureCompression` property. Keeping that initializer risks compile failure on Unity API variants.
Solution: Keep `importer.textureCompression = TextureImporterCompression.CompressedHQ` on the confirmed importer-level property, and leave platform overrides to `format` plus `compressionQuality`.
Rejected Alternatives: Duplicating compression on an unconfirmed platform field, removing HQ compression entirely, or launching `dotnet build` under active external compiler contention.
Scalability potential: Low/Middle/High/Ultra keep BC7/BC5/ASTC override intent without compile fragility. Compression quality remains deterministic across generated assets.
Hardware Impact: Runtime memory target unchanged. Compile-risk removal only; build execution still blocked by active foreign `dotnet` PID 31232.

## Decision 25 - Staging Texture Color Space Is Part Of The Contract

Problem: The `Texture2D(..., linear)` flag is easy to invert. If Albedo is staged as linear when the importer expects sRGB, or Normal/M.R.A.O. are staged as sRGB when they are data textures, the baker can produce visually wrong assets while all pack/UV math remains correct.
Solution: Keep Albedo staging and atlas textures on the sRGB-facing constructor path, keep Normal/M.R.A.O. on the linear data path, and add source assertions for both constructor calls plus the importer `sRGBTexture` rule.
Rejected Alternatives: Trusting manual review, making every texture linear, or making every generated PNG sRGB. Those either break color response or corrupt data maps.
Scalability potential: Low/Middle/High/Ultra use the same color/data contract; quality weight changes resolution/detail, not texture interpretation.
Hardware Impact: Runtime 0 us. Prevents asset color/data corruption before GPU sampling; no bake timing available until Unity is ready.

## Decision 26 - No Incompatible Material Fallbacks

Problem: Atlas packing created the correct M.R.A.O. mask, but material creation could silently fall back to URP Lit or Standard when `Hecton8/Bakers/MraoAtlasLit` was missing. Those fallback shaders do not own the `_MraoMap` channel contract, so the generated material could render with no packed mask.
Solution: Convert material creation to `TryCreateOrUpdateMaterial`, require the dedicated M.R.A.O. shader, and fail atlas packing with a clear error if the shader is unavailable.
Rejected Alternatives: Silent fallback to URP Lit/Standard, binding `_MaskMap`, or duplicating channels into legacy material properties. Those hide a contract failure and can corrupt roughness/AO interpretation.
Scalability potential: Low/Middle/High/Ultra preserve one material route and one mask sample. If the shader contract is absent, no tier gets a fake success artifact.
Hardware Impact: Runtime texture fetch target unchanged. Editor prevents bad material assets; exact frame-time gain remains pending GPU capture.

## Decision 27 - Public Atlas Rect Math Must Fail Before Addition

Problem: `TryPackRectangles` is a public utility and accepted raw integer dimensions. Adding padding before checking fit can overflow signed integers for pathological input, potentially turning an invalid huge rect into a negative or small value.
Solution: Compute `totalPadding` once, reject `width > atlasSize - totalPadding` and `height > atlasSize - totalPadding` before padded addition, then construct candidates only inside the safe 512..4096 atlas envelope.
Rejected Alternatives: Trusting Texture2D dimensions, relying on C# checked arithmetic, or letting later overlap tests catch corrupted rectangles. Public utilities need their own guard.
Scalability potential: Low/Middle/High/Ultra all retain bounded atlas memory. The guard prevents editor OOM/corruption regardless of target device tier.
Hardware Impact: Runtime 0 us. Editor path avoids pathological allocations and invalid rect packing.

## Decision 28 - Meta Audit Must Match Unity's Serialized Enum Values

Problem: The importer audit regex checked BC7/BC5 numeric values `50/48`, but existing Unity `.meta` files in this project serialize Standalone BC7 as `textureFormat: 25` and BC5 as `textureFormat: 27`. The audit could reject correctly compressed generated textures.
Solution: Expand the regex to accept `25|50` for BC7 and `27|48` for BC5, while still accepting named `BC7`/`BC5` tokens if Unity changes serialization style.
Rejected Alternatives: Removing numeric meta auditing, trusting only importer API assignment, or keeping stale enum constants. The audit must verify disk reality.
Scalability potential: Low/Middle/High/Ultra all depend on compressed texture residency. A correct audit prevents false blockers and still catches uncompressed assets.
Hardware Impact: Runtime 0 us. Editor validation accuracy improved; no asset generation until Unity is ready.

## Decision 29 - RenderTexture Allocation Result Is A Hard Gate

Problem: The compute bake path created an RW `RenderTexture` and ignored the boolean result of `Create()`. Under low VRAM or unsupported UAV format conditions, the path could proceed into dispatch/readback against an invalid target.
Solution: Check `if (!rt.Create())`, log the kernel and requested dimensions, and return false before any compute dispatch or `ReadPixels`.
Rejected Alternatives: Trusting Unity allocation, relying on later readback exceptions, or reducing quality silently after allocation failure. The baker must fail closed with visible cause.
Scalability potential: Low/Middle/High/Ultra still select texture size through `GlobalQualityWeight` and VRAM clamp; allocation failure is now an explicit stop instead of undefined editor behavior.
Hardware Impact: Runtime 0 us. Prevents editor crash/stall path on constrained GPUs such as MX350.

## Decision 30 - Final Verification Scope Must Match Ownership

Problem: A broad project scan catches foreign-domain code and verifier/test string literals, which produces false positives and hides whether 1605 production baker code is actually clean.
Solution: Use production-scoped scans for `BakeProfileDTO`, `ProceduralTextureBaker`, and `TextureAtlasPacker`; use source-aware brace checks that strip comments/strings before counting; treat forbidden strings in tests as negative assertions, not production violations.
Rejected Alternatives: Claiming the whole project is clean, editing foreign files, or accepting raw-text scan noise as evidence.
Scalability potential: Low/Middle/High/Ultra all depend on the same offline baker route; ownership-scoped verification keeps future hardening precise without cross-domain churn.
Hardware Impact: Runtime 0 us. Host CPU saved by static focused checks and no `dotnet build` under external compiler contention.

## Decision 31 - Padding Arithmetic Must Be Guarded Before Packing

Problem: Public atlas packing already rejected hostile source dimensions, but `padding * 2` still used `int` arithmetic. A pathological caller could overflow padding before MaxRects validation.
Solution: Compute doubled padding in `long`, reject padding that consumes the atlas, derive a single `maxSourceDimension`, and only then add padding to source dimensions.
Rejected Alternatives: Trusting menu defaults, relying on texture dimensions, or waiting for MaxRects placement to fail. The API is public and must fail before arithmetic can wrap.
Scalability potential: Low/Middle/High/Ultra keep bounded 512..4096 atlas memory. The guard protects all tiers from editor-side corrupt rectangles while preserving normal 4 px bleed.
Hardware Impact: Runtime 0 us. Editor avoids invalid allocations/rect math; exact frame gain is not applicable because packing is offline.

## Decision 32 - Material Assets Must Not Prove A Failed Atlas Import

Problem: The packer wrote atlas PNGs and then created or mutated the material before proving Unity had imported all three atlas textures. A failed import could leave an empty material asset and look like partial success.
Solution: Load albedo, normal, and M.R.A.O. atlas textures immediately after atlas writes/import settings and fail before material creation or mutation if any texture is missing.
Rejected Alternatives: Setting null textures, trusting `EnforceTextureImportSettings`, or creating the material first and relying on later manual inspection. The material asset is a proof artifact and must not exist as a lie.
Scalability potential: Low/Middle/High/Ultra keep the same one-material atlas route; failed import now stops every tier before bad material state leaks into scenes.
Hardware Impact: Runtime 0 us. Editor prevents invalid material artifacts and future frame-debugger noise.

## Decision 33 - Normal Map Borders Must Not Sample Outside Their Tile

Problem: The normal kernel estimated derivatives by sampling height at `uv +/- texel`. Border pixels sampled outside 0..1, so atlas padding could preserve an out-of-domain normal instead of a stable tile-edge normal.
Solution: Add `HectonHeightClamped` and use it for normal derivative taps only. Albedo and mask keep their existing in-domain center sampling; boundary normals now match clamped texture edges before padding/mips.
Rejected Alternatives: Shader-side atlas UV clamps, larger padding, or wrapping procedural height at the tile edge. Runtime clamps add cost; larger padding copies the same wrong normal; wrapping changes non-tileable generated asset intent.
Scalability potential: Low/Middle/High/Ultra all benefit because lower mips and weak-device texture budgets expose atlas borders sooner.
Hardware Impact: Runtime 0 us. Offline compute adds four saturate clamps per normal pixel; cost is paid once in the Editor to prevent seam artifacts.

## Decision 34 - Atlas Selection Roles Must Be Named, Not Ordered

Problem: The manual atlas menu consumed `Selection.objects` as albedo/normal/mask triples. Unity selection order is not a texture role contract, so one wrong click order can bake Normal or M.R.A.O. bytes into the wrong atlas family and still produce a material asset.
Solution: Replace triple indexing with suffix-based grouping. The packer now accepts selected `Texture2D` assets ending in `_Albedo`, `_Normal`, `_MRAO`, or `_Mask`, groups them by shared base name, rejects duplicates and incomplete sets, and preserves first-seen set order only after role validation.
Rejected Alternatives: Keeping the triple convention, sorting by name without role parsing, or guessing by importer type. Triple order is silent corruption; name sort still fails on arbitrary file names; importer type cannot distinguish M.R.A.O. from generic data textures reliably.
Scalability potential: Low/Middle/High/Ultra all depend on the same atlas contract. Correct channel grouping keeps compact devices from sampling corrupted low mips and keeps high/ultra materials eligible for visual-overkill detail without per-object material repair.
Hardware Impact: Runtime 0 us. Editor-only grouping adds small dictionary/list allocation during manual packing and prevents broken BRG material artifacts before runtime profiling.

## Decision 35 - Manual Atlas Packing Must Reach Mesh UV Remap

Problem: `TextureSetInput` supported `Mesh`, and `TryPackTextureSets` remapped mesh UV0 after packing, but the manual selection menu only accepted textures. That left the main human-facing pack route unable to produce updated mesh UVs.
Solution: Extend the suffix grouping to accept `Mesh` assets named either the shared set base or `*_Mesh`. The grouped builder carries the optional mesh into `TextureSetInput.Mesh`; incomplete texture sets still fail, while mesh absence remains valid for pure texture-atlas assembly.
Rejected Alternatives: A second mesh-only menu, raw YAML mesh mutation, or requiring caller code to construct `TextureSetInput` manually. A second menu splits proof paths; YAML is unsafe; direct API-only flow does not satisfy the manual bake/pack workflow.
Scalability potential: Low/Middle/High/Ultra all need the same UV remap math to sample compact shared atlases. Correct manual mesh routing keeps weak devices on one material/atlas path and lets high/ultra spend saved draw calls on richer baked detail.
Hardware Impact: Runtime 0 us. Editor-only selection parsing adds no gameplay cost; successful UV remap reduces material/texture binding divergence at runtime once Unity can execute the menu.

## Decision 36 - M.R.A.O. Verification Must Not Be Sparse

Problem: `TryCollectMraoStats` sampled every `pixels.Length / 64` pixel. Sparse rust, metal flecks, AO pits, or biolum emission could sit between sampled indices, creating a false failure or hiding a malformed channel proof.
Solution: Scan every pixel and break only after R/G/B/A presence plus RGB divergence are all proven. Profile-aware checks still allow legitimately zero metallic/emissive profiles, but the underlying stats are now exact for the texture data in memory.
Rejected Alternatives: Keep 64-sample stride, random sampling, or hash-only validation. Sparse sampling is not mathematical proof; random sampling is nondeterministic; hashes do not prove channel independence.
Scalability potential: Low/Middle/High/Ultra all share the same atlas mask. Exact Editor verification prevents weak devices from shipping with missing low-mip AO/roughness data and lets high/ultra use richer rare-detail masks safely.
Hardware Impact: Runtime 0 us. Editor verification may scan a full 4K mask once; early exit handles dense masks, while sparse masks pay the correct offline proof cost.

## Decision 37 - Role Texture Dimensions Are A Hard Atlas Contract

Problem: Atlas rectangles are derived once per texture set. If Albedo, Normal, and M.R.A.O. dimensions diverge, the packer must fail before it copies pixels or writes proof artifacts, otherwise one role can be clipped or padded against the wrong rectangle.
Solution: Preserve the production dimension guard and add a direct EditMode test that calls `TryPackTextureSets` with mismatched role dimensions. The expected failure occurs before output-folder creation and before atlas/material writes.
Rejected Alternatives: Trusting naming conventions, scaling role textures during pack, or testing only the private source string. Runtime scaling changes data contracts and can blur normals/masks; source-only checks do not prove public API behavior.
Scalability potential: Low/Middle/High/Ultra all depend on identical atlas UVs across albedo/normal/M.R.A.O. maps. Early rejection prevents weak devices from sampling mismatched low mips and high/ultra from inheriting bad material detail.
Hardware Impact: Runtime 0 us. Editor test path allocates three tiny transient textures only; production benefit is preventing corrupted atlas assets before runtime.

## Decision 38 - Mesh UV Failure Must Be Preflighted Before Atlas IO

Problem: `TryPackTextureSets` could build atlas PNGs and then discover a mesh without remappable UV0 during the later UV overwrite stage. It also remapped meshes before proving material creation succeeded, so shader/import failure could leave mesh assets changed with no valid material proof.
Solution: Add `TryValidateAllMeshUvsBeforeAssetWrites` before output folder creation, atlas writes, material creation, or UV mutation. The actual remap path and the preflight share `TryResolveMeshUv0RemapLayout`, so the validation and execution use one UV0 contract. Material creation now happens before mesh mutation.
Rejected Alternatives: Catching remap failure after atlas writes, letting artists inspect failed output folders, or duplicating a weaker mesh check in tests only. Partial proof artifacts corrupt pipeline trust.
Scalability potential: Low/Middle/High/Ultra all need mesh UVs and atlas material to agree. Preflight keeps weak devices on the compact atlas route and prevents high/ultra tiers from spending extra material/state bandwidth to compensate for broken UVs.
Hardware Impact: Runtime 0 us. Editor preflight is cold O(mesh count), avoids wasted atlas IO, and reduces risk of partial asset mutation.

## Decision 39 - Mesh Preflight Must Probe The Actual Vertex Buffer

Problem: UV layout validation proves the attribute contract, but it does not prove `MeshUtility.AcquireReadOnlyMeshData(...).GetVertexData<byte>(stream)` will return a buffer long enough for the remap copy. That failure could still occur after atlas/material IO.
Solution: Add `TryValidateMeshUv0ReadableForRemap`. It shares the UV0 layout resolver, allocates a cold `NativeArray<byte>` sized to the target stream, calls `CopyVertexBufferFromMesh`, and disposes in strict `finally` before any atlas output is touched.
Rejected Alternatives: Trusting `vertexCount * stride`, catching buffer failure during real remap, or leaving buffer read proof to Unity execution only. Layout metadata is not the same as readable buffer proof.
Scalability potential: Low/Middle/High/Ultra keep the same atlas material route only when mesh data can actually be remapped. Broken mesh assets fail early instead of creating partial BRG texture artifacts.
Hardware Impact: Runtime 0 us. Editor-only cold preflight does one extra mesh stream copy before IO; this is cheaper than generating atlases that cannot be applied.

## Decision 40 - Mesh Buffer Allocation Size Must Use Long Arithmetic

Problem: `vertexCount * stride` was used as the byte length for `NativeArray<byte>` allocation in both preflight and real UV remap. Corrupt or extreme mesh metadata could overflow `int` arithmetic before allocation.
Solution: Add `TryComputeVertexBufferByteLength`, calculate `(long)vertexCount * stride`, reject non-positive or above-`int.MaxValue` byte lengths, and route both preflight and remap allocations through it.
Rejected Alternatives: Trusting Unity mesh metadata, relying on NativeArray allocation exceptions, or guarding only the preflight route. The execution route must share the same bound.
Scalability potential: Low/Middle/High/Ultra all benefit from fail-closed editor behavior on bad generated meshes. Valid low-tier meshes keep the same path; high/ultra large meshes fail before allocator corruption.
Hardware Impact: Runtime 0 us. Editor saves failed allocation stalls and prevents corrupted atlas/UV transactions on pathological mesh assets.

## Decision 41 - UV0 Offset Must Fit Inside The Vertex Stride

Problem: The mesh preflight validated UV0 existence, Float32 format, and dimension, but the final byte-copy loop assumes two floats starting at `offset` fit inside each vertex stride. Corrupt generated meshes or importer edge cases could pass metadata checks while still making `offset + 8` cross the vertex record boundary.
Solution: Add a shared `RequiredUv0ByteWidth = 8` guard to `TryResolveUv0Layout`. Both preflight and execution now reject any UV0 layout where `offset + RequiredUv0ByteWidth > stride` before atlas IO, material mutation, or mesh UV overwrite.
Rejected Alternatives: Trusting Unity attribute metadata, catching the exception during the remap copy, or duplicating a later-only guard in `CopyUvFromVertexBuffer`. The shared resolver is the single contract owner.
Scalability potential: Low/Middle/High/Ultra all keep one BRG atlas route only when UV data is byte-addressable. Weak devices avoid corrupted compact atlases; high/ultra can spend atlas savings on richer baked detail without hidden mesh corruption.
Hardware Impact: Runtime 0 us. Editor saves failed copy stalls and prevents partial atlas/material artifacts on malformed mesh input.

## Decision 42 - Atlas Role Assembly Must Reuse One Scratch Buffer

Problem: `TryBuildAtlas` allocated a fresh `Color32[atlasSize * atlasSize]` for every role. At 4096x4096 this is roughly 64 MB per role, so Albedo, Normal, and M.R.A.O. assembly could stack large managed allocations in the Editor for no visual gain.
Solution: Add `TryCreateAtlasScratch`, validate `(long)atlasSize * atlasSize`, allocate one `Color32[]`, and pass it through all three role builds. Each role still clears to its correct neutral background before blitting, so output semantics are unchanged.
Rejected Alternatives: Keep per-role arrays, downscale the atlas, or move atlas assembly to runtime GPU buffers. Per-role arrays waste Editor heap; downscaling violates visual-overkill targets; runtime assembly violates the Editor-only mandate.
Scalability potential: Low/Middle/High/Ultra all keep the same compressed atlas output. Weak machines avoid unnecessary Editor memory spikes, while ultra still gets full 4K atlas detail.
Hardware Impact: Runtime 0 us. Editor peak managed atlas scratch drops from up to three large atlas arrays to one; 4K role scratch pressure is reduced by roughly 128 MB before source texture arrays.

## Decision 43 - Import Enforcement Must Fail Closed

Problem: Texture import enforcement was a `void` path. If `AssetDatabase.ImportAsset`, `AssetImporter.GetAtPath`, platform settings, or `SaveAndReimport` failed after PNG write, the standalone baker or atlas builder could throw out of the controlled `Try*` contract.
Solution: Add `TryEnforceTextureImportSettings`. The old public `EnforceTextureImportSettings` remains as a strict throwing wrapper, but production bake/atlas paths now call the `Try*` route and return false with a concrete failure string.
Rejected Alternatives: Letting exceptions bubble through menu execution, swallowing importer failure, or auditing `.meta` after the fact only. The importer step is part of the proof artifact and must be a first-class fail-closed stage.
Scalability potential: Low/Middle/High/Ultra all depend on BC7/BC5/ASTC import settings, not just PNG bytes. Controlled importer failure prevents weak devices from silently receiving uncompressed or color-space-corrupt textures.
Hardware Impact: Runtime 0 us. Editor avoids uncontrolled menu aborts and makes compression failures visible before assets are treated as valid.

## Decision 44 - Rust Must Follow Curvature, Not Pure Noise

Problem: The industrial rust mask was driven by fBm noise plus edge wear only. It did not read the local height-field curvature, so rust could float across flat panels instead of accumulating on exposed ridges, pits, and worn edges. With `RustSpread=0`, the old `smoothstep(1.0, 1.0, noise)` path was also mathematically unsafe.
Solution: Add `HectonCurvature` using a clamped four-neighbor height laplacian and `HectonRustMask` that multiplies safe spread-gated noise by `curvature * 1.35 + wear * 0.65`. Albedo and M.R.A.O. now share the same rust mask contract.
Rejected Alternatives: Keep pure fBm rust, add a CPU curvature prepass, or rely on artist-painted masks. Pure noise is visually ungrounded; CPU prepass violates the compute-first mandate; manual masks do not scale to procedural biomes.
Scalability potential: Low/Middle/High/Ultra share identical material semantics. Low devices get clearer rust silhouettes in lower mips; high/ultra gain more believable close-up industrial decay without runtime cost.
Hardware Impact: Runtime 0 us. Offline compute adds four clamped height samples in the Albedo and Mask kernels; cost is paid once during Editor baking to buy better authored texture data.

## Decision 45 - Compute Dispatch Shape Must Validate Queried Groups Before Cast

Problem: Dispatch counts were derived from `GetKernelThreadGroupSizes` by immediately casting `uint groupSizeX/Y` to `int`. Valid Unity kernels return small values, but a broken kernel metadata path should fail explicitly before casting and before `Dispatch`.
Solution: Add `TryResolveDispatchGroups`. It rejects non-positive texture sizes, zero group dimensions, and `uint` values larger than `int.MaxValue`, then calculates dispatch counts through `CeilDivide`.
Rejected Alternatives: Trusting `[numthreads(8,8,1)]` forever, relying on `CeilDivide` returning zero after a bad cast, or hardcoding 8 in C#. The mandate requires queried group sizes and fail-closed dispatch math.
Scalability potential: Low/Middle/High/Ultra keep portable dispatch sizing from shader metadata. Bad kernel metadata now stops before GPU work is scheduled.
Hardware Impact: Runtime 0 us. Editor adds a few scalar checks per bake kernel and prevents malformed dispatch attempts.

## Decision 46 - Compression Proof Must Read The TextureImporter Object

Problem: Import enforcement set BC7/BC5/ASTC values, and `.meta` regex audit existed, but the import step did not re-read the `TextureImporter` object after `SaveAndReimport`. A failed or altered platform override could be missed until runtime memory pressure.
Solution: Add `AuditTextureImporterSettings`. After `SaveAndReimport`, the baker reads Standalone and Android platform settings and verifies texture type, sRGB flag, readable=false, HQ compression, max texture size, BC7/BC5, and ASTC_6x6.
Rejected Alternatives: Relying on `.meta` regex only, trusting `SetPlatformTextureSettings`, or checking only Standalone. Regex is brittle across Unity serialization; setters are not proof; Android is a target path.
Scalability potential: Low/Middle/High/Ultra all depend on the same compression proof. Low/Quest avoids accidental uncompressed uploads; high/ultra keeps 4K detail while preserving VRAM contracts.
Hardware Impact: Runtime 0 us. Editor adds one cold importer read after reimport and prevents false-valid texture artifacts.

## Decision 47 - Try Methods Must Contain Unity Exceptions

Problem: `TryDispatchAndWrite` and `TryBuildAtlas` cleaned resources in `finally`, but Unity exceptions from dispatch, readback, texture staging, pixel upload, or PNG encoding could still escape the `Try*` contract.
Solution: Add targeted exception catch filters. Compute bake failures log `Bake dispatch failed`; atlas build failures return `atlas build failed for ...` through the existing failure string.
Rejected Alternatives: Catching all exceptions including fatal memory failures, or relying on menu callers to absorb Unity exceptions. Fatal conditions should still surface; normal Unity API failures should be controlled proof failures.
Scalability potential: Low/Middle/High/Ultra all benefit from deterministic Editor failure behavior. Weak machines are more likely to hit allocation/API failures and now receive a clean abort path.
Hardware Impact: Runtime 0 us. Editor avoids uncontrolled menu unwinds and preserves cleanup on failed bake/atlas operations.

## Decision 48 - Meta Audit Reads Must Fail Closed

Problem: `AuditTextureMeta` was a public proof helper but still read `.meta` files through `File.ReadAllText` without a controlled IO/path failure route.
Solution: Reject empty asset paths before building the meta path, then catch normal IO/access/path exceptions and return `false` with `meta audit read failed ...`.
Rejected Alternatives: Letting source tests be the only caller, relying on `File.Exists`, or deleting the meta audit now that `TextureImporter` object audit exists. The regex audit remains useful as a serialization cross-check and must keep the same `bool/out failure` contract.
Scalability potential: Low/Middle/High/Ultra all depend on proof artifacts staying deterministic under editor file contention. Weak machines and shared workspaces are more likely to hit transient file access failures.
Hardware Impact: Runtime 0 us. Editor avoids uncontrolled audit unwinds; build/test execution was still throttled because external `dotnet` processes were active.

## Decision 49 - Filename Sanitation Must Not Invent Identity

Problem: `SanitizeAssetNameForPath` replaced every invalid character with `_`, so a name with no valid characters still became a plausible asset identity like `___`. That made prior fail-closed callers weaker than their status claimed.
Solution: Track `hasValidAssetNameChar` and return `string.Empty` when the source contains no valid ASCII filename character. Existing profile, atlas, and selection-set checks now reject or fall back honestly.
Rejected Alternatives: Allowing underscore-only generated names, accepting Unicode filenames in this tool, or silently hashing names. Underscore-only names collide and hide bad inputs; Unicode broadens path variance across platforms; hashes obscure artist/debug identity.
Scalability potential: Low/Middle/High/Ultra all need deterministic generated asset names for atlas reuse and source control. Bad names now fail before asset IO.
Hardware Impact: Runtime 0 us. Editor prevents false asset identities and avoids accidental atlas/material overwrite clusters.

## Decision 50 - Material Proof Assets Must Fail Closed

Problem: `TryCreateOrUpdateMaterial` could throw during shader/material/AssetDatabase operations and leave a newly created material asset as a false proof artifact.
Solution: Wrap the material creation/update route in targeted exception filters. If a new material asset was created during a failed operation, delete it; if a transient material exists without an asset path, destroy it.
Rejected Alternatives: Letting the menu exception escape, catching only atlas texture import failure, or leaving a partial material for manual cleanup. The material is a proof artifact and must not lie.
Scalability potential: Low/Middle/High/Ultra all depend on the same one-material M.R.A.O. atlas contract. Failed material creation now stops before mesh UV remap and before scenes can reference a broken atlas material.
Hardware Impact: Runtime 0 us. Editor avoids partial material assets and downstream Frame Debugger noise.

## Decision 51 - Manual Atlas Menu Must Respect Compact VRAM

Problem: The manual selection menu always requested the 4K default atlas even though standalone bake resolution already used the VRAM-aware size resolver.
Solution: Add `ResolveSafeAtlasSize` and route the menu default through `ProceduralTextureBaker.ResolveSafeTextureSize(DefaultAtlasSize, 1f)`. Explicit API callers can still request a specific atlas size; the human menu no longer forces 4K on compact hardware.
Rejected Alternatives: Hardcoding 2K, silently changing `DefaultAtlasSize`, or applying a binary low/high switch. The existing continuous/VRAM-aware resolver is the single size policy owner.
Scalability potential: Low/compact gets 2K when VRAM is constrained; Middle/High/Ultra retain 4K atlas output when the hardware budget allows it.
Hardware Impact: Runtime 0 us. Editor avoids avoidable 4K atlas staging pressure on compact machines.

## Decision 52 - Atlas Source Pixels Must Match Texture Geometry

Problem: `TryReadTexturePixels` returned a `Color32[]`, but the atlas blit trusted that buffer to match `source.width * source.height`. A corrupt import/readback mismatch could index wrong source data or fail after atlas scratch mutation.
Solution: Add `TryValidateSourcePixelBuffer` immediately before `BlitWithEdgePadding`. The check uses `long` width-height multiplication and rejects null, invalid count, or length mismatch before writing atlas pixels.
Rejected Alternatives: Trusting Unity texture metadata, catching `IndexOutOfRangeException` during blit, or hashing pixels after atlas encode. Those fail late and can leave false atlas proof artifacts.
Scalability potential: Low/Middle/High/Ultra all depend on stable mip-safe shared atlases. Bad source textures now fail before atlas writes on weak machines and before high-tier 4K proof assets are polluted.
Hardware Impact: Runtime 0 us. Editor adds one scalar pixel-count check per source texture and prevents wasted atlas assembly on malformed imports.

## Decision 53 - Atlas Failure Cleanup Must Not Mask Primary Failure

Problem: The readable texture bridge called importer lookup directly, and material failure cleanup queried/destroyed transient materials inside the catch path. Either cleanup step could hide the original bake/material failure.
Solution: Add `TryResolveTextureImporterForReadableBridge` for controlled importer lookup failures and `TryDestroyTransientMaterial` for no-throw transient material cleanup.
Rejected Alternatives: Letting cleanup exceptions escape, swallowing importer lookup failure as a generic unreadable texture, or leaving transient materials alive after failed material creation.
Scalability potential: Low/Middle/High/Ultra all use the same generated atlas/material proof route. Clear failure ownership makes compact and ultra-tier asset transactions debuggable without runtime repair code.
Hardware Impact: Runtime 0 us. Editor avoids false failure causes and partial material cleanup stalls.

## Decision 54 - Albedo Empty Gutter Must Be Neutral

Problem: Empty albedo atlas pixels were black. Even with 4px edge bleed, distant mips can average empty gutters into small islands and darken silhouettes on compact tiers.
Solution: Clear albedo gutters to mid-gray `(128,128,128,255)` while keeping normal `(128,128,255,255)` and M.R.A.O. `(0,255,255,0)` neutral values.
Rejected Alternatives: Keep black gutters, shader-side atlas clamping, or larger fixed padding. Black creates mip halos; shader clamping costs runtime; larger padding wastes atlas area.
Scalability potential: Low gets cleaner low-mip silhouettes; Middle/High/Ultra keep the same atlas layout and spend quality on texture detail instead of runtime seam repair.
Hardware Impact: Runtime 0 us. Offline atlas pixels become more mip-stable without extra samples or material branches.

## Decision 55 - M.R.A.O. Missing Map Fallback Must Be Neutral

Problem: `_MraoMap` uses Unity's built-in white texture fallback. With default `_MetallicScale=1` and `_EmissionStrength=1`, any manually created or temporarily incomplete material could render as fully metallic and emissive before the generated M.R.A.O. atlas is bound.
Solution: Default `_MetallicScale` and `_EmissionStrength` to `0` in `Hecton_MraoAtlasLit`. `TextureAtlasPacker.TryCreateOrUpdateMaterial` still sets both values to `1f` after all three atlas textures are imported and bound, so the valid generated path keeps full M.R.A.O. semantics.
Rejected Alternatives: Add a neutral fallback texture asset, branch/lerp inside the fragment shader, or keep white fallback behavior. A fallback asset adds proof-path asset churn; shader branching spends runtime ALU; keeping white produces false metal/glow.
Scalability potential: Low/Middle/High/Ultra share the same generated material route. Weak devices avoid low-mip false glare from incomplete materials; high/ultra keep full metallic/rust/emission once the atlas proof exists.
Hardware Impact: Runtime 0 us for valid generated materials. The fix changes defaults only; no additional texture sample, branch, or per-frame material mutation.

## Decision 56 - Failed First-Run Atlas Packs Must Not Leave Proof Artifacts

Problem: `TryPackTextureSets` wrote Albedo, Normal, and M.R.A.O. atlases sequentially. On a first-run pack, a later import/material/UV failure could leave newly-created partial output files that look like successful proof artifacts.
Solution: Record prior output state for the three atlas PNGs and material before writes. On later failure, delete only assets that did not exist before the pack. If prior state cannot be proven, treat the asset as pre-existing and preserve it.
Rejected Alternatives: Delete all outputs on failure, leave partial first-run artifacts, or implement a broad multi-file transaction that overwrites existing atlas assets. Deleting all can destroy user-approved outputs; leaving partial files lies; full transaction staging is heavier and risks churn outside the immediate defect.
Scalability potential: Low/Middle/High/Ultra all depend on trustworthy generated atlas/material artifacts. Weak machines are more likely to hit import/IO failures; high/ultra 4K packs avoid stale partial artifacts after expensive offline work.
Hardware Impact: Runtime 0 us. Editor saves manual cleanup and prevents false-positive atlas evidence; valid successful packs keep the same output path and compression contract.

## Decision 57 - Mesh UV Remap Must Roll Back On Late Failure

Problem: Atlas/material outputs were cleaned on failure, but a late mesh UV remap failure could leave earlier meshes already remapped to a now-deleted or invalid atlas. Preflight lowers that risk but does not prove `SetVertexBufferData`/save cannot fail during execution.
Solution: Route mesh mutation through `TryRemapAllMeshUvsWithRollback`. Before each remap, capture the original vertex stream bytes into a cold Editor rollback snapshot. If any later mesh remap fails, restore all captured mesh streams in reverse order. Also reject the same `Mesh` assigned to multiple atlas sets.
Rejected Alternatives: Trust preflight only, remap meshes before material creation, or ignore partial mesh mutation because it is rare. Preflight is not execution; remapping before material reopens the old no-material failure; rare partial mutation still corrupts BRG atlas proof.
Scalability potential: Low/Middle/High/Ultra all depend on mesh UVs matching the shared atlas. Weak machines are more likely to hit editor save/import failures; high/ultra packs avoid expensive partial remap cleanup after 4K transactions.
Hardware Impact: Runtime 0 us. Editor-only rollback allocates temporary managed byte snapshots only during atlas packing, not in gameplay or renderer hot paths.

## Decision 58 - File Cleanup Must Remove Orphan Meta Files

Problem: If `AssetDatabase.DeleteAsset` cannot see a newly-created output because import failed, the file-system fallback deleted the asset bytes but could leave a Unity `.meta` file behind.
Solution: The fallback path now deletes `absolutePath + ".meta"` after deleting the newly-created asset file.
Rejected Alternatives: Rely on AssetDatabase only, leave orphan metas, or run a broad generated-folder cleanup. AssetDatabase can miss failed imports; orphan metas pollute source control; broad cleanup risks deleting unrelated generated content.
Scalability potential: Low/Middle/High/Ultra all need clean generated asset identity. Failed packs no longer leave meta ghosts that can confuse later atlas/material imports.
Hardware Impact: Runtime 0 us. Editor avoids stale meta artifacts after failed offline texture transactions.

## Decision 59 - Meta Cleanup Must Not Depend On Asset Bytes

Problem: The previous fallback still gated `.meta` deletion behind `File.Exists(absolutePath)`. A failed import can leave only a `.meta` after the asset bytes are already gone.
Solution: Once `TryResolveAbsoluteAssetPathNoThrow` succeeds, check and delete the asset file and `.meta` file independently.
Rejected Alternatives: Treating missing asset bytes as full cleanup, or running a broad generated-folder sweep. Missing bytes do not prove missing meta; broad sweeps risk unrelated generated assets.
Scalability potential: Low/Middle/High/Ultra all need deterministic generated atlas identity. Failed packs now remove orphan metadata without touching existing user-approved outputs.
Hardware Impact: Runtime 0 us. Editor avoids later GUID/import confusion after failed offline atlas writes.

## Decision 60 - Final AssetDatabase Save Must Stay Inside Rollback Window

Problem: The final `AssetDatabase.SaveAssets()/Refresh()` was outside the `Try*` failure contract and after mesh UV mutation. A late AssetDatabase failure could throw out of `TryPackTextureSets` and leave atlas outputs or remapped meshes in an unproven state.
Solution: Add `TryFinalizeAtlasTransaction` and call it from `TryRemapMeshesAndFinalizeWithRollback` while rollback snapshots are still alive. On finalization failure, captured meshes are restored and newly-created atlas/material outputs are deleted by the caller.
Rejected Alternatives: Trusting final Save/Refresh, swallowing the exception, or moving mesh UV mutation after finalization. Trusting can corrupt proof state; swallowing lies; moving UV mutation after finalization reopens mesh partial-failure risk.
Scalability potential: Low/Middle/High/Ultra share the same atlas/material/UV transaction. Weak machines hit AssetDatabase contention more often; high/ultra packs avoid expensive half-committed 4K atlas states.
Hardware Impact: Runtime 0 us. Editor-only rollback/finalization control prevents failed offline work from polluting source assets.

## Decision 61 - AssetDatabase Finalization Needs One Controlled Route

Problem: Direct `AssetDatabase.SaveAssets()/Refresh()` calls existed in the default bake menu and atlas finalization path, creating two exception surfaces after proof assets were generated.
Solution: Add `ProceduralTextureBaker.TryFinalizeAssetDatabase(operationName, out failure)` and route default seed pack plus atlas transaction finalization through it.
Rejected Alternatives: Leaving raw `SaveAssets()/Refresh()` in menu code, or swallowing failures. Raw calls can throw after partial output; swallowing failures lies about proof artifact state.
Scalability potential: Low/Middle/High/Ultra all depend on generated texture/import state being either committed or rejected. Weak machines hit AssetDatabase contention more often; ultra-tier 4K output cannot afford false success.
Hardware Impact: Runtime 0 us. Editor failure ownership is centralized; no build was launched.

## Decision 62 - M.R.A.O. Verification Must Not Require Readable Imports Forever

Problem: Public M.R.A.O. verification called `Texture2D.GetPixels32()` directly. A non-readable or broken imported mask could throw instead of returning a controlled failure string.
Solution: Catch normal Unity/API exceptions in `TryCollectMraoStats` and return `mask texture pixel read failed ...` through the existing `bool/out failure` contract.
Rejected Alternatives: Keeping generated masks readable forever, deleting the verification route, or trusting import settings. Readable runtime textures waste memory; deleting the route removes proof; trust is not proof.
Scalability potential: Low/Middle/High/Ultra keep non-readable compressed masks as the runtime target while Editor verification fails cleanly when a source is unsuitable.
Hardware Impact: Runtime 0 us. Editor avoids uncontrolled verification exceptions and keeps BC7/BC5 non-readable policy intact.

## Decision 63 - Direct Bake Outputs Need The Same Cleanup Discipline As Atlases

Problem: `TryBakeProfile` writes Albedo, then Normal, then M.R.A.O. A later role failure could leave newly-created earlier PNGs on disk as false completion evidence.
Solution: Record prior output state for all three direct bake assets and delete only newly-created outputs on any later kernel/import/audit failure. Unknown prior state is preserved.
Rejected Alternatives: Leave partial direct bake outputs, delete all outputs unconditionally, or require manual cleanup. Partial outputs lie; unconditional deletion can destroy approved assets; manual cleanup is not a system contract.
Scalability potential: Low/Middle/High/Ultra all depend on trustworthy generated source tiles before atlas packing. Weak machines are more likely to hit import/readback failures; high/ultra avoids stale high-resolution orphan outputs.
Hardware Impact: Runtime 0 us. Editor prevents false standalone texture proof artifacts; cleanup is cold and no-throw.

## Decision 64 - Variant Selection Must Survive Dirty Serialized Data

Problem: `BakeProfileDTO.Variant` is an enum, but serialized/editor data can still hold out-of-range integer values. The shader's old weight selection could make all weights zero and generate black/empty textures.
Solution: Clamp the variant in C# through `ResolveSafeVariant`, then clamp/round `_BakerTextureSize.z` again in `HectonVariantWeights`.
Rejected Alternatives: Trusting enum serialization or adding a runtime material fallback. Serialized enum trust is not proof; runtime fallback spends frame time for an offline data defect.
Scalability potential: Low/Middle/High/Ultra all get deterministic Organic/Mineral/Industrial output even if a profile is dirty. High/Ultra can still spend quality on richer noise, not recovery logic.
Hardware Impact: Runtime 0 us. Offline compute avoids invalid black proof textures; shader adds one cold bake-time clamp/round only.

## Decision 65 - Importer Audits Must Return A Failure String

Problem: `AuditTextureImporterSettings` could throw from Unity importer lookups/settings reads, bypassing the `bool/out failure` proof contract.
Solution: Wrap the audit in normal Unity/API exception guards and return `texture importer audit failed ...`; import enforcement also catches `NotSupportedException`.
Rejected Alternatives: Let the exception escape or trust importer state after `SaveAndReimport`. Escapes break transaction cleanup; trust is not proof.
Scalability potential: Low/Middle/High/Ultra all rely on BC7/BC5/ASTC settings being proven or rejected. Weak machines and stale import states now fail cleanly.
Hardware Impact: Runtime 0 us. Editor-only proof route avoids uncontrolled exceptions and keeps runtime textures non-readable/compressed.

## Decision 66 - Asset Folder Creation Must Fail Before Writes

Problem: `TryEnsureAssetFolder` called `AssetDatabase.IsValidFolder/CreateFolder` raw. A Unity/path exception could abort before the bake or atlas transaction had a chance to report a controlled failure.
Solution: Wrap folder validation/creation in a catch-filtered `try`, returning `asset folder creation failed ...` through the existing cold Editor route.
Rejected Alternatives: Assume folders always create, or create folders with raw `Directory.CreateDirectory`. AssetDatabase can reject invalid project state; raw directories bypass Unity asset identity.
Scalability potential: Low/Middle/High/Ultra output folders now fail deterministically before texture writes. Large 4K atlas packs no longer risk half-started transactions from a folder setup exception.
Hardware Impact: Runtime 0 us. Editor avoids failed offline bakes that leave ambiguous output state; no build was launched.

## Decision 67 - Selection Names Must Not Be Raw AssetDatabase Calls

Problem: Manual atlas packing resolved selected Texture2D/Mesh names through raw `AssetDatabase.GetAssetPath`. A Unity/path exception could abort selection grouping before a controlled failure string existed.
Solution: Add `TryResolveAssetObjectName` and route texture/mesh selection names through it.
Rejected Alternatives: Trusting editor selection state or requiring artists to retry after an exception. Selection state can be dirty; retry instructions are not a system contract.
Scalability potential: Low/Middle/High/Ultra all use the same manual proof route for generated tiles. Weak machines under import contention now get deterministic failure instead of abrupt editor exceptions.
Hardware Impact: Runtime 0 us. Editor-only name lookup failure is contained before atlas bytes or mesh UVs are touched.

## Decision 68 - Public Atlas API Must Enforce VRAM Clamp

Problem: The menu entrypoint called `ResolveSafeAtlasSize`, but public `TryPackTextureSets` accepted any supported atlas size up to 4096. Non-menu callers could bypass compact-VRAM downgrade.
Solution: Inside `TryPackTextureSets`, compute `safeAtlasSize = ResolveSafeAtlasSize(atlasSize)` and reject requests above the current safe limit.
Rejected Alternatives: Clamp silently or trust callers. Silent clamp hides output size changes; caller trust breaks the 2GB VRAM floor.
Scalability potential: Low rejects unsafe 4K and uses smaller atlases; Middle/High/Ultra keep higher atlas sizes when `SystemInfo.graphicsMemorySize` allows it.
Hardware Impact: Runtime 0 us. Editor avoids over-allocating 4K atlas scratch/texture objects on compact hardware.

## Decision 69 - Readable Bridge Must Reload Stale Texture References

Problem: If direct `GetPixels32()` failed and the importer was already readable, the bridge retried the same stale `Texture2D` object instead of reloading from AssetDatabase.
Solution: Always reload `source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)` after importer bridge resolution; only reimport when `isReadable` had to change.
Rejected Alternatives: Re-read same object, or always force reimport. Same object can fail twice; always reimport adds avoidable editor churn.
Scalability potential: Low/Middle/High/Ultra all get stable atlas input reads from selected assets. Large high/ultra atlas packs avoid wasting work on stale object references.
Hardware Impact: Runtime 0 us. Editor reduces failed atlas reads without extra runtime memory or texture residency.

## Decision 70 - Unsupported Exceptions Are Normal Editor Failure Modes

Problem: Material creation and M.R.A.O. pixel verification still missed `NotSupportedException`, which can occur in path/platform/importer surfaces.
Solution: Add `NotSupportedException` to controlled catch filters for material creation and mask pixel read verification.
Rejected Alternatives: Treat unsupported path/platform cases as fatal. Fatal escape breaks cleanup and rollback proof.
Scalability potential: Low/Middle/High/Ultra all preserve transaction cleanup when platform/path support differs.
Hardware Impact: Runtime 0 us. Editor failures stay inside cleanup paths; no build was launched.

## Decision 71 - Existing Materials Need Rollback, Not Preservation Of Corruption

Problem: Atlas failure after `TryCreateOrUpdateMaterial` could leave an existing material rebound to new atlas textures even when later mesh UV remap/finalization failed.
Solution: Capture shader, name, relevant texture bindings, and relevant scalar bindings before atlas writes. Restore the snapshot on material update failure or later remap/finalization failure.
Rejected Alternatives: Delete existing material, leave mutated material, or postpone all material work after mesh finalization. Deletion destroys user assets; mutation lies about transaction success; postponing after finalization creates a different rollback hole.
Scalability potential: Low/Middle/High/Ultra all keep material-to-atlas state coherent. Low devices avoid loading half-valid atlas references; high/ultra keep visual-overkill atlas materials without corrupting previous approved assets.
Hardware Impact: Runtime 0 us. Editor-only rollback adds bounded material property reads during atlas build and prevents failed packs from causing later VRAM residency of wrong textures.

## Decision 72 - Unsupported Texture Reads Should Still Try The Importer Bridge

Problem: Direct `Texture2D.GetPixels32()` only bridged Unity/argument/operation exceptions. Unsupported format/path cases could skip the readable importer bridge and collapse into a generic atlas failure.
Solution: Treat `NotSupportedException` as a direct-read failure, preserve the exception type in `directReadFailure`, and proceed through the existing readable import bridge.
Rejected Alternatives: Fail immediately or always reimport before trying direct read. Immediate failure rejects recoverable assets; unconditional reimport adds editor churn to already-readable textures.
Scalability potential: Low/Middle/High/Ultra all get one deterministic read route from source assets. Large high/ultra atlas packs avoid wasting previous successful work because one selected texture used an unsupported direct-read surface.
Hardware Impact: Runtime 0 us. Editor-only path reduces failed atlas reads without increasing runtime texture readability or VRAM.

## Decision 73 - The Verifier Must Fail Closed On Its Own File I/O

Problem: `ApexIntegratorVerifier1605` used raw `Directory.GetFiles` and `File.ReadAllText`, so the proof tool itself could throw instead of returning a violation result.
Solution: Add `TryCollectSourceFiles` and `TryReadSourceFile`, sort files ordinally, and return controlled `ApexVerificationResult1605` failures on file I/O problems.
Rejected Alternatives: Let the verifier throw or ignore unreadable files. Throwing breaks the proof path; skipping unreadable files is false compliance.
Scalability potential: Low/Middle/High/Ultra all rely on deterministic static gates before expensive Unity/player proof. Weak machines under file/indexer contention now get a clear failure instead of a broken verification call.
Hardware Impact: Runtime 0 us. Editor verification remains CPU-light and deterministic; no build was launched.

## Decision 74 - Material Rollback Must Include Shader Keywords

Problem: Atlas material update enables `_NORMALMAP`. The previous rollback restored shader/name/properties but could leave keyword state drift after a failed atlas transaction.
Solution: Capture `NormalMapKeywordEnabled`, restore it with `RestoreKeyword`, and save the restored material snapshot through a controlled `TrySaveMaterialRollbackSnapshot` route.
Rejected Alternatives: Ignore keyword state, disable `_NORMALMAP` unconditionally, or rely on unsaved dirty state. Ignoring leaves visual drift; unconditional disable breaks materials that already used normal maps; unsaved rollback is not proof.
Scalability potential: Low/Middle/High/Ultra all preserve material rendering state after failed offline atlas packs. Low hardware avoids accidental normal sampling state on bad assets; high/ultra keeps approved normal-rich materials intact.
Hardware Impact: Runtime 0 us. Editor-only rollback adds one keyword bool and one controlled save on failure; no build was launched because external Unity `dotnet` PID 10780 was active.

## Decision 75 - Existing Texture Outputs Need Byte-Level Rollback

Problem: Direct bake and atlas pack cleanup deleted newly-created outputs, but an approved existing PNG/.meta could still be overwritten by a later-failing transaction. That corrupts disk proof without touching runtime code.
Solution: Capture existing output asset bytes and `.meta` bytes in `AssetFileRollbackSnapshot` before the first write. On failure, restore snapshots in reverse order and reimport restored assets through a controlled Editor-only path.
Rejected Alternatives: Keep relying on newly-created cleanup, leave `.bak1605` files on disk for cross-stage rollback, or always delete existing outputs. Newly-created cleanup does not protect approved assets; persistent backups violate temp-artifact hygiene; deleting approved outputs destroys user work.
Scalability potential: Low/Middle/High/Ultra all preserve previously approved compressed texture assets when a larger or richer atlas pass fails. Low devices avoid being left with unsafe 4K imports after a failed pack; high/ultra can attempt visual-overkill atlases without corrupting the previous shipped set.
Hardware Impact: Runtime 0 us. Editor-only memory cost is bounded to three compressed PNG files plus three meta files during a bake/atlas transaction. No build was launched; external Unity `dotnet` PID 27484 was active.

## Decision 76 - Rollback Restore Must Be Atomic And Bounded

Problem: Byte-level texture rollback fixed late transaction corruption, but direct restore writes and unbounded snapshot reads could create a second corruption vector or cold-editor memory spike if an existing output was huge or damaged.
Solution: Route asset and `.meta` restore through `TryWriteBytesAtomicAbsolute`, allow zero-byte exact restore only for rollback, and cap rollback reads with `MaxRollbackAssetBytes`/`MaxRollbackMetaBytes` before loading bytes.
Rejected Alternatives: Direct `File.WriteAllBytes`, persistent backup files, or no size ceiling. Direct writes can tear; persistent backups leave artifacts; no ceiling can read hostile or corrupted files into memory.
Scalability potential: Low/Middle/High/Ultra all get deterministic output integrity. Low devices fail before memory pressure from oversized artifacts; high/ultra can still attempt richer atlas passes with rollback bounded to compressed disk assets.
Hardware Impact: Runtime 0 us. Editor-only cap prevents unbounded managed memory spikes; no build was launched because external Unity `dotnet` PID 27484 was active.

## Decision 77 - Meta Audits Must Not Read Corrupt Files Unbounded

Problem: `AuditTextureMeta` used `File.ReadAllText` directly. A damaged or hostile `.meta` file could force an unbounded cold Editor read before the BC7/BC5/ASTC proof route rejects it.
Solution: Check `.meta` length against `MaxRollbackMetaBytes` before reading and return `meta audit byte ceiling exceeded ...` through the existing `bool/out failure` contract.
Rejected Alternatives: Trust Unity `.meta` sizes or skip text audit. Trust creates avoidable memory pressure; skipping audit removes a source-level proof of compression/import settings.
Scalability potential: Low/Middle/High/Ultra all keep importer proof deterministic. Low devices avoid memory pressure from corrupt metadata; high/ultra keep strict proof before accepting richer 4K atlas outputs.
Hardware Impact: Runtime 0 us. Editor-only read ceiling prevents avoidable managed memory spikes; no build was launched because external Unity `dotnet` PIDs 7940 and 30560 were active.

## Decision 78 - Atlas Workload Needs A Hard Set Ceiling

Problem: Atlas packing target is 10-20 generated flora/rock sets, but the public packer accepted arbitrarily large `TextureSetInput[]`/`TextureRect[]` counts. That can grow candidate arrays, free-rect lists, and mesh rollback work before any artist-visible quality gain exists.
Solution: Add `MaxTextureSetsPerAtlas = 256`, reject larger batches in both `TryPackTextureSets` and `TryPackRectangles`, and cap mesh UV rollback stream copies with `MaxMeshUvRollbackBytes`.
Rejected Alternatives: Rely on selection discipline, silently split atlases, or allow huge batches because the code is Editor-only. Selection discipline is not a contract; silent split changes material/UV ownership; Editor-only memory spikes still break the cluster.
Scalability potential: Low devices fail before large managed/native buffers. Middle keeps normal biome atlas batches. High/Ultra can still pack far above the 10-20 target without crossing into unbounded work.
Hardware Impact: Runtime 0 us. Editor avoids runaway atlas/free-rect and mesh rollback memory on i3/MX350-class hosts. No build/test/player build was launched.

## Decision 79 - Atlas Source Reads Must Fail Before Pixel Allocation

Problem: `TryReadTexturePixels` validated source pixel counts after `Texture2D.GetPixels32()`. Oversized or corrupt texture dimensions could allocate a large managed `Color32[]` before the packer rejects the source.
Solution: Add `MaxAtlasSourcePixels` and `TryValidateSourceTextureReadDimensions`; call it before direct reads and after readable-bridge reload, then require exact pixel count from both paths.
Rejected Alternatives: Keep post-read validation only, rely on earlier rectangle packing, or clamp source dimensions. Post-read validation is too late; earlier packing is not proof for private helper reuse; clamping would corrupt texels.
Scalability potential: Low devices fail before managed source pixel memory pressure. Middle keeps standard 512-2K source tiles. High/Ultra still accept up to the 4K atlas source ceiling without false resize.
Hardware Impact: Runtime 0 us. Editor avoids avoidable `GetPixels32()` managed allocation on bad inputs. External Unity `dotnet` PIDs 25728 and 28292 were active; no build/test/player build was launched.

## Decision 80 - Atlas Byte Costs Must Be Explicit

Problem: `TryCreateAtlasScratch` guarded pixel count but not raw byte cost, and encoded atlas PNG bytes had no ceiling before disk write/import.
Solution: Add `AtlasScratchBytesPerPixel`, `MaxAtlasScratchBytes`, and `MaxAtlasEncodedPngBytes`; reject oversized scratch allocations and encoded atlas payloads through controlled failure strings.
Rejected Alternatives: Trust current 4096 max forever, or let importer reject huge encoded files. Future constant drift is common; importer rejection happens after memory/disk cost is already paid.
Scalability potential: Low devices fail before large scratch arrays. Middle/High/Ultra keep 4K atlas support while the limits make future overkill atlas changes deliberate.
Hardware Impact: Runtime 0 us. Editor prevents accidental memory/disk spikes; external Unity `dotnet` PID 25728 was active and no build/test/player build was launched.

## Decision 81 - Standalone Bake PNGs Need The Same Ceiling As Atlases

Problem: Atlas PNG output gained a byte ceiling, but direct compute bake output still accepted any `ImageConversion.EncodeToPNG` payload before atomic write/import.
Solution: Add `MaxEncodedPngBytes` to `ProceduralTextureBaker.TryWritePng` and reject oversized direct Albedo/Normal/M.R.A.O. payloads.
Rejected Alternatives: Reuse rollback byte limits implicitly or leave direct bake unconstrained. Rollback limits are for pre-existing disk assets; direct bake output needs an explicit production proof gate.
Scalability potential: Low devices reject pathological direct bakes before disk/import churn. Middle/High/Ultra retain 4K procedural output while oversized future payloads fail visibly.
Hardware Impact: Runtime 0 us. Editor avoids direct output disk/import spikes. External Unity `dotnet` PID 25728 was active; no build/test/player build was launched.

## Decision 82 - The APEX Verifier Must Protect Memory Ceilings

Problem: The APEX verifier proved dependency/build/phase hygiene, but the new memory ceilings were only protected by tests/source probes. A future patch could remove them while still passing the old verifier.
Solution: Add `s_requiredMemoryCeilingTokens` and `VerifyRequiredMemoryCeilingTokens` to fail closed if the 1605 ceiling identifiers disappear from production Baker sources.
Rejected Alternatives: Keep the check only in tests, or rely on human review. Tests can be skipped; human review is inconsistent under multi-agent churn.
Scalability potential: Low/Middle/High/Ultra all keep bounded offline authoring costs across future edits. High/Ultra overkill remains deliberate instead of accidental memory growth.
Hardware Impact: Runtime 0 us. Editor verifier gains one cold pass over small source files. External Unity `dotnet` PID 25728 was active; no build/test/player build was launched.

## Decision 83 - Verifier Token Lists Are Not Proof Artifacts

Problem: The APEX memory-ceiling gate could count required identifiers inside `ApexIntegratorVerifier1605` itself, because the verifier stores those names in `s_requiredMemoryCeilingTokens`.
Solution: Add `VerifierSourceFileName` and skip the verifier source when scanning for required memory ceiling tokens, so proof must come from production Baker implementation files.
Rejected Alternatives: Leave the self-counting scan or special-case only one token. Self-counting creates false compliance; partial special cases rot as the token list changes.
Scalability potential: Low/Middle/High/Ultra all keep actual production ceilings protected. Future high/ultra atlas expansion cannot pass by editing only the verifier list.
Hardware Impact: Runtime 0 us. Editor verifier remains a cold source scan. External Unity `dotnet` PIDs 3040 and 25728 were active; no build/test/player build was launched.

## Decision 84 - Compute Kernel Lookup Must Be A Controlled Failure

Problem: `TryDispatchAndWrite` called `ComputeShader.HasKernel` and `FindKernel` before the common bake dispatch `try/finally`. A corrupt, missing, or platform-unsupported compute asset could escape the controlled bake failure route before the transaction reports a precise kernel error.
Solution: Add `TryResolveComputeKernel` with null, missing-kernel, invalid-index, and supported Unity exception handling. `TryDispatchAndWrite` now enters dispatch only after a resolved kernel id exists.
Rejected Alternatives: Leave raw `HasKernel`/`FindKernel` calls inline, or wrap the whole method in one broad catch. Inline calls are an unproofed escape point; broad catch hides the exact contract that failed.
Scalability potential: Low/Middle/High/Ultra all fail deterministically before GPU allocation when the compute asset is damaged or unsupported. High/Ultra overkill profiles do not start partial output writes without a resolved kernel.
Hardware Impact: Runtime 0 us. Editor avoids wasted RenderTexture/Texture2D allocation on bad compute assets. External Unity `dotnet` PIDs 25728, 30400, and 26704 were active; no build/test/player build was launched.

## Decision 85 - Mesh UV Transactions Must Not Catch Programmer Faults

Problem: Mesh UV remap, mesh UV rollback capture, and mesh UV preflight used `catch (Exception ex) when (!(ex is FatalArchitectureException))`. That can mask programmer defects and memory pressure as ordinary atlas failures.
Solution: Replace the broad filters with `IsRecoverableEditorException`, limited to Unity, IO, access, argument, invalid-operation, and unsupported editor failure modes.
Rejected Alternatives: Keep broad catches for convenience or add more fatal exclusions one by one. Broad catches hide defects; exclusion lists rot and still catch unexpected runtime failures.
Scalability potential: Low/Middle/High/Ultra all keep deterministic artist-facing failures for recoverable editor state, while real code defects remain visible to the integrator instead of silently corrupting atlas proof.
Hardware Impact: Runtime 0 us. Editor-only mesh UV transactions no longer misclassify severe faults. External Unity `dotnet` PIDs 25728 and 20200 were active; no build/test/player build was launched.

## Decision 86 - Packed Rect Lookup Must Be Transactional

Problem: `FindPackedRectForSource` threw `FatalArchitectureException` from atlas texture blit and mesh UV remap. In mesh remap, that throw could happen after rollback snapshots existed and after earlier meshes were already modified.
Solution: Replace the throwing lookup with `TryFindPackedRectForSource`; atlas build now returns a controlled failure, and mesh remap restores captured UV snapshots before returning false.
Rejected Alternatives: Keep the throw because packing should be internally consistent, or catch the fatal exception outside. Internal consistency is not proof under multi-agent churn; catching fatal exceptions outside hides where rollback must occur.
Scalability potential: Low/Middle/High/Ultra all get deterministic atlas transaction rollback when packed-rect data is corrupted or incomplete. Overkill atlas batches fail without leaving partially remapped meshes.
Hardware Impact: Runtime 0 us. Editor avoids partial mesh UV mutation on inconsistent atlas data. External Unity `dotnet` PIDs 25728 and 22004 were active; no build/test/player build was launched.

## Decision 87 - Empty Rectangle Packs Are Not Valid Atlases

Problem: `TryPackRectangles` accepted zero input rectangles and returned success with efficiency 0. That is a false proof path for callers using the lower-level packing API directly.
Solution: Add `inputs.Length == 0` to the public rectangle packer guard and add a functional EditMode source test for empty input rejection.
Rejected Alternatives: Rely on `TryPackTextureSets` to reject empty inputs. The lower-level public API is separately callable and must defend its own contract.
Scalability potential: Low/Middle/High/Ultra all avoid no-op atlas success states. Batch tooling cannot accidentally report a valid atlas when selection grouping produces no texture sets.
Hardware Impact: Runtime 0 us. Editor avoids downstream atlas/material/UV work after an empty pack request. External Unity `dotnet` PIDs 25728 and 19792 were active; no build/test/player build was launched.

## Decision 88 - APEX Must Guard Transaction Safety Tokens

Problem: The APEX verifier protected memory ceilings but not the transaction hardening added around compute kernel resolution, packed rect lookup, recoverable mesh exceptions, empty pack rejection, and mesh UV rollback.
Solution: Add `s_requiredTransactionSafetyTokens` and `VerifyRequiredTransactionSafetyTokens`, skipping the verifier file so required tokens must exist in production Baker sources.
Rejected Alternatives: Keep transaction guard checks only in EditMode source probes. Source probes can be skipped; APEX is the explicit integrator verification surface for this domain.
Scalability potential: Low/Middle/High/Ultra all keep transaction integrity across future atlas/bake edits. High/Ultra overkill batches cannot pass verifier if rollback or fail-fast gates are deleted.
Hardware Impact: Runtime 0 us. Editor verifier gains one cold source pass over small Baker files. External Unity `dotnet` PIDs 15112 and 16700 were active; no build/test/player build was launched.

## Decision 89 - Mesh Buffer Shortage Must Trigger Rollback, Not Fatal Escape

Problem: `CopyVertexBufferFromMesh` threw `FatalArchitectureException` when Unity returned a shorter vertex buffer than the validated UV remap size. In a multi-mesh atlas transaction this could bypass the caller's rollback of earlier remapped meshes.
Solution: Throw `InvalidOperationException` for the short-buffer condition so `TryRemapMeshUvs` catches it through `IsRecoverableEditorException`, returns false, and `TryRemapMeshesAndFinalizeWithRollback` restores captured snapshots.
Rejected Alternatives: Keep the fatal throw or catch fatal exceptions at the outer transaction. Fatal throw skips the precise rollback site; catching fatal broadly makes real architecture defects look recoverable.
Scalability potential: Low/Middle/High/Ultra all get consistent mesh UV rollback when imported mesh buffers are inconsistent. Large overkill atlas batches cannot leave earlier meshes partially remapped because one later mesh had a short buffer.
Hardware Impact: Runtime 0 us. Editor avoids partial UV mutation on inconsistent mesh data. External Unity `dotnet` PIDs 15112 and 16700 were active; no build/test/player build was launched.
