# Rationale_1615 - MASTER_MATERIAL_AND_CBUFFER_UNIFIER

## Session Bootstrap

Problem: Agent 1615 prompt was not at the first batch block; a broad read initially missed it by direct regex failure.
Solution: Used `Select-String`/line-bounded PowerShell extraction against `Docs/Tasks/CURRENT_BATCH.md`, then isolated lines 1278-1356 only.
Rejected Alternatives: Reading neighboring prompts or acting from the user's summary would leak other-agent constraints into this domain.
Scalability potential: Correct prompt isolation prevents unrelated systems from introducing shader keywords, variants, or runtime dependencies.
Hardware Impact: No runtime impact; prevents scope drift that could add shader variants harmful to i3/MX350.

Problem: Current user directive rejects heavy builds and useless JSON/binary proof artifacts.
Solution: Treat `dotnet build` as blocked unless C# coordinator edits make it critical and CPU/compiler contention checks pass; use durable shader/source/log proof instead of standalone JSON dumps unless a machine-readable scratch artifact is essential.
Rejected Alternatives: Running the task's default build/report steps blindly would waste host CPU and produce unread proof files.
Scalability potential: Keeps agent cluster throughput available while focusing proof on material/shader assets.
Hardware Impact: Editor host CPU saved; runtime impact depends on shader consolidation work still pending.

## Mandate Selection

Problem: Shader unification touches SRP Batcher, variant count, GPU ALU, zero-GC shader globals, and CBuffer layout, but not gameplay truth.
Solution: Selected eight mandates: URP hot path, shader noir aesthetics, MX350 GPU optimization, frame/VRAM budgets, zero-GC, runtime struct layout, execution phases, and cinematic fake-first.
Rejected Alternatives: Physics, AI, save, audio, and narrative mandates do not govern this shader-only domain and would inflate scope.
Scalability potential: Low uses three texture samples, baked AO, zero POM; middle uses bounded POM/detail; high/ultra spends saved variants on richer detail without new material layouts.
Hardware Impact: Expected benefit for i3/MX350 is fewer shader variants, fewer SetPass breaks, and reduced texture bandwidth; measured microseconds remain `PENDING VERIFICATION`.

## Tasks 01-05 Static Plan

Problem: The shader directory contains 139+ shader/HLSL files, so a blind monolithic rewrite would damage UI, post, sky, stencil, and indirect special-purpose passes.
Solution: Split first consolidation to surface/lit material shaders only: UberNoir, voxel rock, coral, kelp, sargassum, dry zone, wreck, tool decay, ruin sheen, outpost, procedural bio, leviathan, and scatter lit paths.
Rejected Alternatives: Collapsing all hidden/post/celestial shaders into the same surface shader would destroy pass semantics and inflate risk.
Scalability potential: Low/middle/high/ultra share one material ABI; fidelity scales through `_H8GlobalQualityWeight`, not shader keywords.
Hardware Impact: Static candidate pragma count is 88 versus 3 in the master target after final rescan. This is a variant-pressure reduction plan, not compiled GPU proof.

Problem: True POM normally requires repeated height-map samples, directly conflicting with the three-texture-sample mandate.
Solution: Use a deterministic "dear lie" parallax loop: one packed mask sample provides height, then a quality-scaled loop computes UV offset without additional texture fetches. Final shading still samples only mask/albedo/normal.
Rejected Alternatives: True POM per-step mask sampling was rejected because it violates the MX350 sample budget and the task's three-sample requirement.
Scalability potential: Low = zero parallax steps; middle = shallow offset; high = more stable offset; ultra = stronger visual offset through the same ABI.
Hardware Impact: Low path bypasses parallax loop; high path spends ALU only, not extra texture bandwidth. Runtime timing remains `PENDING VERIFICATION`.

Problem: User rejected JSON proof dumps while the prompt requested JSON reporting.
Solution: Keep proof in readable markdown logs and source files; skip `Docs/Reports/MASTER_SHADER_UNIFICATION_1615.json` unless later explicitly required by integrator.
Rejected Alternatives: Writing unread JSON would satisfy old batch text but violate the current user directive.
Scalability potential: No runtime effect; improves review signal.
Hardware Impact: No runtime effect.

## Tasks 06-10 Materialization

Problem: Legacy surface shaders carry many local keyword routes and uneven material buffers, but direct deletion would break unknown materials while 20+ agents are editing nearby assets.
Solution: Add `Hecton_Master_Lit.shader` as the migration target: three required passes, one material CBUFFER, no local quality keywords, and instancing-only compile expansion.
Rejected Alternatives: Mass-rewriting `Hecton_CoralMaster`, `Hecton_KelpMaster`, voxel, wreck, and procedural bio shaders in one pass was rejected because material import and scene assignment proof is not available in this turn.
Scalability potential: Low uses same ABI with cheap normals, no parallax, and stronger dither; middle/high/ultra raise visual offset, normal response, and microcontrast through floats rather than variants.
Hardware Impact: Static candidate pragma pressure drops from 88 candidate pragma lines to 3 master instancing pragma lines after material migration. Compiled microseconds remain `PENDING UNITY IMPORT`.

Problem: Dynamic Bayer table indexing and double `_ST` application could create importer risk and texture-coordinate drift.
Solution: Replaced the 16-value const array with bit-derived Bayer math; kept raw UV in varyings and applied `_BaseMap_ST`, `_MaskMap_ST`, and `_BumpMap_ST` independently while preserving one sample per texture.
Rejected Alternatives: Keeping the const array was legal under target 4.5 but not worth a compiler ambiguity; sharing base UV for all textures was cheaper but wrong for materials with distinct ST.
Scalability potential: All tiers keep deterministic UV behavior; low-tier does not pay extra texture bandwidth.
Hardware Impact: No added texture fetches. Bayer branchless math avoids table indexing and keeps the MX350 path predictable.

Problem: True parallax occlusion mapping would require repeated height-map reads and violate the strict three-texture-sample mandate.
Solution: Use packed mask alpha once, then execute bounded ALU-only parallax offset scaled by `_H8GlobalQualityWeight` and material cap.
Rejected Alternatives: Per-step height lookup, binary-search POM, and quality keywords were rejected as texture-bandwidth or variant debt.
Scalability potential: Low = zero iterations; middle = shallow displacement; high = deeper apparent relief; ultra = stronger offset within same material ABI.
Hardware Impact: Low path exits before loop; high path spends ALU but preserves texture bandwidth. Runtime GPU time is not claimed without profiler proof.

Problem: Shader-global updates could silently allocate or mutate outside the allowed owner phase.
Solution: Inspected dispatcher sources. The active path is `GlobalShaderDispatcher.ExecuteGlobalDispatch` and fallback is `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync`; both use cached `Shader.PropertyToID` integers. No new updater was added.
Rejected Alternatives: A new MonoBehaviour polling route or material-property-block broadcast would violate single-route ownership and risk SRP Batcher breaks.
Scalability potential: GlobalQualityWeight remains a global visual scalar; material ABI and save identity stay unchanged across low/middle/high/ultra.
Hardware Impact: Avoided hot string IDs and per-material writes. `new Vector4` at dispatch sites is a struct value, not a GC allocation.

## Tasks 11-15 Variant And Compile Gate

Problem: The master SVC asset already existed but did not reference the new master shader, so first-frame warmup would keep using legacy direct roots.
Solution: Add `Hecton_Master_Lit` to `Hecton8MasterVariants.shadervariants` with empty and `INSTANCING_ON` passType 0 variants; add the same shader path and instancing manifest to the existing editor compiler.
Rejected Alternatives: Creating a second SVC asset or a new editor compiler was rejected because the bootstrap already has a single warmup route.
Scalability potential: Low/middle/high/ultra all warm the same ABI; no quality-keyword variants are introduced.
Hardware Impact: Avoids first-route shader hitch risk after migration. Compiled warmup timing remains `PENDING UNITY IMPORT`.

Problem: Sky and material shaders needed expensive math reduction without losing first-frame spectacle.
Solution: The master shader remains free of `pow/sin/cos/sqrt`; Aegir sky replaces sphere-intersection `sqrt` with `value * rsqrt(max(value, epsilon))` and keeps procedural stars/ring shadow as cheap hashes and squared terms.
Rejected Alternatives: Physical atmospheric scattering or analytic high-order specular functions were rejected for the MX350 first-frame budget.
Scalability potential: Low keeps stars sparse and drift shallow; middle/high/ultra increase star density feel, ring alpha, rim scatter, and material parallax through continuous floats.
Hardware Impact: Removed one direct `sqrt` from the Aegir sky fragment path and avoided any expensive math calls in the master fragment path.

Problem: Helper math contained branch-like ternaries that were not required for the quality path.
Solution: Rewrote safe reciprocal sign handling and safe normalize fallback with `step`, `lerp`, and `rsqrt`. The only remaining quality branch is the required uniform POM zero-step early exit.
Rejected Alternatives: Forcing true constant-flow POM even at quality 0 was rejected because Task 09 requires absolute zero POM overhead on low-end hardware.
Scalability potential: Quality 0.0 samples mask/albedo/normal and exits parallax; quality 0.5 runs a masked 16-iteration ALU loop with half the active offset; quality 1.0 uses full configured depth without changing shader variants.
Hardware Impact: Predictable ALU replaces branch ambiguity in helper code. Runtime timing remains unmeasured.

Problem: The C# SVC compiler edit could justify a build, but the host was already saturated.
Solution: Ran process/CPU gate. CPU load was 100%, two `dotnet` processes were active, and current user directive bans builds after small edits. Build was skipped; static C# checks replaced it for this turn.
Rejected Alternatives: Launching another `dotnet build` would breach the resource throttle and damage parallel agent throughput.
Scalability potential: No runtime effect; protects cluster throughput.
Hardware Impact: Host CPU contention avoided. Static proof: C# brace count 53/53, using set unchanged, two master shader references.

## Tasks 16-20 Final Static Proof

Problem: CBUFFER alignment proof needed to survive context loss and human review without relying on shader comments.
Solution: Added editor-only `HectonMasterShaderAudit1615` that parses the master shader, counts CBUFFER bytes, checks samples, keyword count, expensive math calls, and SVC GUID presence.
Rejected Alternatives: A runtime validator was rejected because material ABI checks are editor/import concerns; JSON output was rejected by current user directive.
Scalability potential: Low/middle/high/ultra all consume the same aligned material ABI; fidelity changes never alter CBUFFER layout.
Hardware Impact: Static proof confirms 192-byte aligned material buffer, preventing SRP Batcher layout divergence after migration.

Problem: The requested 80% variant reduction could not be honestly proven from Unity compiled variant data without running the shader compiler.
Solution: Report static pragma-pressure reduction only: candidate surface set now counts 88 pragma lines; master has 4 multi_compile lines: 3 instancing routes plus 1 URP punctual shadow caster vertex route. This is 95.45% static pressure reduction after material migration.
Rejected Alternatives: Claiming compiled database numbers or changing all legacy materials blind was rejected as false proof.
Scalability potential: Visual quality scales through `_H8GlobalQualityWeight`, not build-time keyword families.
Hardware Impact: Expected lower warmup/VRAM pressure after migration; actual compiled size and GPU timing remain `PENDING UNITY IMPORT`.

Problem: Zero-GC global dispatch proof can be contaminated by broad unrelated rendering files.
Solution: Inspected the ownership route files only: `GlobalShaderDispatcher`, `HectonShaderGlobalDataVaultBridge`, and `H8ShaderIDs`. SetGlobal calls use cached IDs and run in VisualSync/fallback owner paths.
Rejected Alternatives: Global grep over all rendering systems produced unrelated feature-pass hits; using it as proof would blur ownership.
Scalability potential: Global visual scalars can scale low/middle/high/ultra without material churn.
Hardware Impact: No hot string SetGlobal calls found in inspected dispatch routes. Value-type vector construction is not a managed allocation.

Problem: SRP Batcher compliance can be broken by missing `UnityPerMaterial` blocks in modified shaders.
Solution: Text scan over modified shaders confirmed one subshader and one material CBUFFER in both master and Aegir sky, with zero shader_feature pragmas.
Rejected Alternatives: Demanding every legacy shader pass be rewritten now was rejected because only master migration target and sky were modified in this turn.
Scalability potential: Master materials share one ABI. Sky remains a separate background shader with one stable material buffer.
Hardware Impact: Prevents new modified shader assets from adding SRP-batcher-hostile material layouts.

Problem: Final proof needed precise hashes and concise logging, not bureaucratic output.
Solution: Recorded SHA256 hashes in `ShaderCorpus_1615.md` and appended final report to `LOG_1615.md`.
Rejected Alternatives: Writing `Docs/Reports/MASTER_SHADER_UNIFICATION_1615.json` was rejected by user order.
Scalability potential: No runtime effect; review artifact is durable.
Hardware Impact: No runtime effect.

## APEX Integrator Verification Pass

Problem: The editor audit proved shader structure but did not yet prove the APEX constraints: no hot dependency lookups, phase-safe shader-global writes, and no nested DataVault mutation guards.
Solution: Expanded `HectonMasterShaderAudit1615` to inspect the shader-global owner route. It now checks forbidden hot tokens in `LateFrameTick`, `ExecuteGlobalDispatch`, `FlushFallbackVisualSync`, and `VisualSyncTick`; verifies `RunDispatcherLateFrame` owns deferred visual flush calls; rejects string-literal shader setters; and rejects nested `TryAcquireMutationGuard` before release.
Rejected Alternatives: A runtime test or build was rejected because the user explicitly banned build spam and the active process gate showed multiple `dotnet` processes. Pure source-level validation is the correct proof for this pass.
Scalability potential: Low/middle/high/ultra lanes keep one shader-global route and one material ABI; quality changes affect presentation floats only.
Hardware Impact: Prevents hot scene searches, hot registry pulls, and lock stalls from entering shader presentation. Expected gain is stall avoidance on i3/MX350; runtime microseconds remain `PENDING PROFILER`.

Problem: The first master material defaults could make an unmigrated/no-mask material render as metallic and could spend parallax offset from a white default alpha.
Solution: Changed default `_MasterSurfaceParams` to `(0, 0, 1, 1)` and `_MasterPomParams` to `(0, 0, 0, 1)`. Metallic and roughness now lerp from legacy `_Metallic` and `_Smoothness` to packed MRAO channels using continuous map weights.
Rejected Alternatives: Detecting whether a mask texture is bound inside the shader was rejected; that would add a runtime branch and still be unreliable. Adding material keywords was rejected because it reintroduces variant debt.
Scalability potential: Low/no-map materials are stable and cheap; migrated middle/high/ultra materials enable packed MRAO and POM through floats without changing variants.
Hardware Impact: No extra texture fetches. Low-end path avoids accidental POM; high-end path still has the bounded ALU-only parallax fake.

Problem: A hand-picked APEX scan can miss a later hot lookup added under another rendering/material runtime class.
Solution: Expanded `HectonMasterShaderAudit1615` to scan runtime C# files under `Assets/_Project/Scripts/Rendering` and `Assets/_Project/Scripts/Graphics/Materials`. It skips `Editor` folders, sanitizes comments and strings while preserving character positions, and checks all named hot method bodies for forbidden dependency lookup tokens.
Rejected Alternatives: A broad raw grep was rejected as final proof because editor tools contain intentional string counters and cold installer calls. A build was rejected because active `dotnet` contention remains.
Scalability potential: The material/rendering domain gains a durable tripwire; low-to-ultra lanes cannot regress into hot scene search or hot registry pulls without the editor audit failing.
Hardware Impact: Prevents main-thread stalls on weak CPUs. Measured runtime gain is `PENDING PROFILER`; static raw runtime-domain lookup hits are 0.

Problem: Packed mask alpha was serving height and emission. A white default mask plus nonzero emission color could accidentally make unmigrated materials glow, and Standard MetallicGloss alpha is smoothness rather than emission.
Solution: Emission now uses `emissionHeightMask * saturate(_EmissionColor.a)`. Default `_EmissionColor.a` is zero, and mask layout 2 remaps alpha to smoothness without allowing it to drive emission.
Rejected Alternatives: Adding a separate emission texture was rejected because it violates the three-sample target. Adding an emission keyword was rejected because it recreates variant debt.
Scalability potential: Low/no-map materials stay visually stable; high/ultra emissive materials can still use the packed alpha with a continuous scalar.
Hardware Impact: No added sample and no added branch. Removes accidental overdraw/light contribution risk from migrated materials.

Problem: The master shader existed, but manual material migration would be error-prone: `_MainTex` ST could be lost, packed maps could accidentally enable POM, and material keywords could reintroduce variant debt.
Solution: Added `HectonMasterMaterialMigrator1615`, an editor-only selected-material migration tool. It caches texture references and ST before shader swap, transfers base/normal/mask maps, preserves legacy metallic/smoothness/normal/cutoff values, keeps `_MasterPomParams` at `(0,0,0,1)`, uses `_EmissionColor.a` as emission opt-in, and enables instancing without `EnableKeyword`.
Rejected Alternatives: Mass-editing all `.mat` files was rejected because the master shader has not passed Unity import/profiler proof. Enabling `INSTANCING_ON` manually as a keyword was rejected because it creates material keyword debt; `material.enableInstancing` is the correct route.
Scalability potential: Low and unmigrated materials stay visually conservative; selected high/ultra materials can be moved onto the master ABI in controlled batches without shader variant growth.
Hardware Impact: Prevents manual migration mistakes that would break SRP Batcher, add keywords, or introduce accidental POM/emission cost on MX350-class hardware.

Problem: Project materials expose multiple incompatible mask conventions. New MRAO uses R/metal G/roughness B/AO A/emission, legacy HECTON `_MaskMap` often uses R/metal G/AO B/smoothness A/emission, and Unity Standard `_MetallicGlossMap` uses R/metal A/smoothness. Treating all of them as full MRAO would corrupt roughness and AO during migration.
Solution: Reused `_MasterShadowParams.w` as the mask-layout scalar and added branchless decode in `Hecton_Master_Lit`: 0 = MRAO/Packed, 1 = legacy project mask, 2 = Standard MetallicGloss. The migrator now derives layout and channel weights from the source texture name, keeps `_MetallicGlossMap` emission disabled, and does not add a new sampler or CBUFFER register.
Rejected Alternatives: Adding a fourth texture sample for separate AO/smoothness was rejected by the three-sample mandate. Adding shader keywords for mask layout was rejected because it recreates variant debt. Mass-repacking textures was rejected before Unity import and art review proof.
Scalability potential: Low/middle/high/ultra all keep one master ABI and one texture triad. Old materials can migrate safely in batches; new high-end materials can author true MRAO without changing variants.
Hardware Impact: Prevents visual regressions and unnecessary material variants on MX350-class GPUs while keeping sample count at 3 and CBUFFER size at 192 bytes.

Problem: Runtime rendering/material hot methods can silently break SRP Batcher by touching `renderer.material`, mutating `SetPropertyBlock`, or toggling material keywords after the migration tool has produced batchable master materials.
Solution: Extended `HectonMasterShaderAudit1615` domain-hot scan to reject `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, and `DisableKeyword` inside `Tick`, `FixedTick`, `FixedUpdate`, `LateFrameTick`, `Execute`, and `VisualSyncTick` bodies under runtime rendering/material roots.
Rejected Alternatives: Rewriting all existing renderers to a new material state bus was rejected as broad cross-agent churn. Banning `MaterialPropertyBlock` globally was also rejected because indirect/instanced renderers may need an explicit owner route later; the current hard boundary is hot MonoBehaviour/runtime mutation.
Scalability potential: Low/middle/high/ultra material lanes remain SRP-batcher compatible by default. Future visual overkill must flow through shader globals, structured buffers, or cold selected-material migration instead of hot renderer material cloning.
Hardware Impact: Prevents per-frame material instance creation, keyword churn, and SRP-batcher breakage on i3/MX350-class CPUs. Measured frame microseconds remain `PENDING PROFILER`; static runtime-domain hot material mutation hits are 0.

Problem: The master shader clipped alpha in `ForwardLit` but not in `ShadowCaster` or `DepthOnly`, so cutout meshes would render correct color edges while writing full opaque silhouettes into shadows and depth.
Solution: Added `ShadowVaryings`, `DepthVaryings`, `H8MasterSampleBase`, `H8MasterClipAlpha`, and `H8MasterClipAlphaFromRawUv`. Shadow/depth fragments now use the same alpha clip rule as forward. The `_BaseMap` sample is centralized, keeping the source-level `SAMPLE_TEXTURE2D` count at three.
Rejected Alternatives: Adding separate shadow/depth texture samples inline was rejected because it weakens the three-sample audit and increases pass drift. Disabling cutout support was rejected because flora and damaged geometry need stable silhouettes.
Scalability potential: Low/middle/high/ultra share the same alpha rule; no binary keyword or material variant is introduced.
Hardware Impact: Shadow/depth cutout passes pay one base alpha sample only when those passes render. Forward path remains 3 samples; source static count remains 3. Visual corruption from full-silhouette depth/shadows is removed.

Problem: Standard `_MetallicGlossMap` alpha is smoothness, not emission. The previous neutral height remap could still produce constant emission if a user manually set `_EmissionColor.a` on layout 2.
Solution: Added `emissionLayoutWeight`, a branchless scalar that is 1 for layouts 0/1 and 0 for layout 2. MetallicGloss keeps neutral height for parallax math but cannot drive emission.
Rejected Alternatives: A separate emission texture was rejected by the three-sample mandate. A keyword for Standard versus MRAO was rejected as variant debt.
Scalability potential: Low/middle/high/ultra keep one mask ABI. Standard migrated materials remain physically sane; authored MRAO materials can still use packed alpha emission.
Hardware Impact: One ALU scalar prevents accidental emissive overdraw and keeps sample count, CBUFFER size, and variant count unchanged.

Problem: Local URP 17.4 `ShadowCasterPass.hlsl` does not use `_MainLightPosition.xyz` for shadow bias. It declares `_LightDirection` and `_LightPosition`, selects `_CASTING_PUNCTUAL_LIGHT_SHADOW` for point/spot lights, and clamps the biased clip position through `ApplyShadowClamping`. The master shader's previous hand-written shadow caster would compile against a weaker directional-only assumption and risk bad bias for punctual lights.
Solution: Updated `Hecton_Master_Lit.shader` to match the URP 17.4 shadow caster contract: declare `_LightDirection`/`_LightPosition`, compute punctual light direction from `_LightPosition - positionWS`, use `_LightDirection` for directional lights, call `ApplyShadowClamping(positionCS)`, and add the vertex-only `_CASTING_PUNCTUAL_LIGHT_SHADOW` pragma. Updated the SVC and direct compiler manifest to include punctual and instanced-punctual routes. Updated `HectonMasterShaderAudit1615` to fail if the old `_MainLightPosition.xyz` bias route returns.
Rejected Alternatives: Keeping exactly three `multi_compile` pragmas was rejected because it made the proof numerically cleaner but violated URP's point/spot shadow contract. Inlining URP's full ShadowCaster include was rejected because the master needs shared alpha-cutout sampling and the current single-CBUFFER material ABI.
Scalability potential: Low/middle/high/ultra still share the same material ABI and texture triad. The extra keyword is vertex-only shadow infrastructure, not a material-quality branch. High-end punctual lighting stops producing biased shadow artifacts without changing forward shading cost.
Hardware Impact: Adds one shadow-caster variant route and zero ForwardLit texture samples. Static pragma reduction changes from 96.59% to 95.45%, still above the 80% target. It prevents shadow acne/peter-panning regressions that would otherwise force expensive art-side workarounds on MX350-class hardware.

Problem: URP 17.4 `LitForwardPass.hlsl` applies `UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION` when filling `InputData.normalizedScreenSpaceUV`. The master shader used the simpler direct `GetNormalizedScreenSpaceUV(input.positionCS)` route, which can misalign screen-space AO, clustered lighting, and screen-dependent lighting work on rotated mobile/VR displays.
Solution: Added `H8MasterNormalizedScreenSpaceUv(float4 positionCS)` to mirror the URP LitForwardPass pretransform cases for 0/90/180/270 degrees, then routed `inputData.normalizedScreenSpaceUV` through that helper. The editor audit now requires the helper and rejects the old direct assignment.
Rejected Alternatives: Ignoring rotated-display support was rejected because Quest/mobile targets are part of the mandate. Adding a new renderer feature or per-platform shader variant was rejected because this is a pure URP input-data contract fix.
Scalability potential: Low/middle/high/ultra all keep the same material ABI, shader variant set, and texture triad. Screen-space effects remain correctly indexed on mobile/VR without changing desktop output.
Hardware Impact: Adds only a small URP-standard coordinate transform branch under the existing display-pretransform macro. It adds zero texture samples, zero CBUFFER bytes, zero material keywords, and prevents expensive platform-specific workaround shaders.

Problem: The master migrator treated every `_MaskMap` as legacy packed mask `R=metal G=AO B=smoothness A=emission`. `Hecton8/Rendering/UberNoir` uses `_MaskMap("Packed ARM Emission")`, decoded in its own shader as `R=AO G=roughness B=metallic A=emission`. Migrating UberNoir through the legacy layout would swap AO/metal response and corrupt the main candidate shader family.
Solution: Added mask layout `3` in `Hecton_Master_Lit` for UberNoir ARM without adding a sampler, keyword, or CBUFFER lane. The migrator now captures `sourceShaderName` before the shader swap and maps `Hecton8/Rendering/UberNoir` `_MaskMap` to layout `3`. The audit now requires ARM decode tokens and the migrator source-shader route.
Rejected Alternatives: Splitting UberNoir into a separate master shader was rejected because it keeps variant/material ABI debt alive. Repacking all UberNoir masks was rejected before Unity import and art review proof. Adding a mask-layout keyword was rejected as variant debt.
Scalability potential: Low/middle/high/ultra keep one shader ABI and one texture triad. Legacy packed masks, Standard metallic-gloss maps, MRAO maps, and UberNoir ARM maps can migrate in controlled selected-material batches.
Hardware Impact: Prevents wrong metallic/AO migration on MX350-class hardware without changing sample count, CBUFFER size, or keyword count. Runtime microseconds remain `PENDING PROFILER`.

Problem: The master shader always executed alpha clipping with cutoff 0.5. Opaque migrated materials with incidental albedo alpha below cutoff could become perforated, and selected transparent/stencil sources could be forced into an opaque PBR shader.
Solution: Reused `_MasterAlphaParams.w` as a continuous clip weight. The shader now clips with `lerp(1.0h, clipValue, clipWeight)`, so default opaque materials never discard. The migrator writes `_MasterAlphaParams` explicitly, enables clip weight only for alpha-test sources, and rejects transparent or stencil source shaders.
Rejected Alternatives: Adding `_ALPHATEST_ON` or surface keywords was rejected because it recreates shader variant debt. Creating a transparent master route now was rejected because blended glass/water/overlay materials are not the same batchable opaque/cutout family and would violate overdraw discipline on MX350.
Scalability potential: Low/middle/high/ultra keep one master ABI. Opaque objects stay stable; cutout objects use the same dithered clip scalar; transparent/stencil materials remain on specialized shaders until a separate route has proof.
Hardware Impact: Prevents accidental discard on opaque geometry without adding texture samples, keywords, or CBUFFER bytes. Runtime microseconds remain `PENDING PROFILER`.

Problem: Migrator AO semantics mixed channel presence with `_OcclusionStrength`. `_MasterSurfaceParams.z` received the source occlusion strength and `_OcclusionStrength` was also set, so AO was effectively applied as strength squared.
Solution: `MaskSemantics.OcclusionWeight` now means channel availability only: 1 for mask layouts with AO, 0 for Standard MetallicGloss/no mask. `_OcclusionStrength` remains the only artistic AO strength scalar used by the shader.
Rejected Alternatives: Removing `_OcclusionStrength` from the shader was rejected because legacy materials and Unity inspector habits already own that scalar. Baking AO strength into textures was rejected before material import and art review.
Scalability potential: Material response now migrates predictably across low/middle/high/ultra without changing texture layout or shader variants.
Hardware Impact: No runtime cost change. Prevents visual darkening drift and follow-up material churn on MX350-class targets.

Problem: Alpha-test source materials could get the correct clip scalar but lose their render surface route after `material.shader = masterShader`, because the master SubShader tag is opaque/geometry by default.
Solution: The migrator now resolves target render queue before shader swap and reapplies surface routing after the swap. Alpha-test sources receive `RenderType=TransparentCutout` and an AlphaTest queue, preserving custom alpha-test queue offsets when they stay below Transparent. Opaque sources receive `RenderType=Opaque` and a Geometry queue, preserving valid opaque queue offsets.
Rejected Alternatives: Adding a second transparent/cutout master shader was rejected because it reintroduces shader ABI and variant debt. Adding shader keywords for surface type was rejected for the same reason. Migrating transparent/stencil materials remains rejected until a separate overdraw/stencil-safe route exists.
Scalability potential: Low/middle/high/ultra keep one master ABI; surface routing is material metadata, not shader variant state.
Hardware Impact: Prevents cutout materials from being sorted as opaque geometry after migration without adding runtime cost, texture samples, keywords, or CBUFFER bytes.

Problem: The master SVC serialized `49aa0d16489a41c88aef21e218cbc32e`, but the editor audit did not prove that the new shader asset's `.meta` file actually owns that GUID. A missing or regenerated `.meta` would make the SVC warmup proof false.
Solution: `HectonMasterShaderAudit1615` now reads `Hecton_Master_Lit.shader.meta` and asserts `guid: 49aa0d16489a41c88aef21e218cbc32e`, tying shader source, Unity asset identity, and SVC warmup reference to one cold import proof.
Rejected Alternatives: Trusting the untracked `.meta` file by visual inspection was rejected because it would not fail closed in the editor audit. Rewriting the SVC GUID was rejected because the current GUID already matches the meta and all dependent proof artifacts.
Scalability potential: Low/middle/high/ultra all warm the same master shader asset. Variant and material ABI proof now survives Unity asset database reimport unless the GUID is intentionally changed and the SVC is updated in the same route.
Hardware Impact: No runtime cost. Prevents first-frame shader warmup misses on i3/MX350-class hardware caused by stale or mismatched asset GUIDs.

Problem: `Hecton_Master_Lit` used `AlphaToMask On` for cutout coverage, but `Frag` returned raw albedo alpha even when `_MasterAlphaParams.w` disabled clipping. Opaque materials with incidental alpha below 1 could lose MSAA coverage without any discard.
Solution: The forward fragment now returns `outputAlpha = lerp(1.0h, alpha, saturate(_MasterAlphaParams.w))`. Opaque migration lanes force alpha 1 for coverage; cutout lanes still feed authored alpha to alpha-to-coverage. The editor audit asserts this route.
Rejected Alternatives: Turning `AlphaToMask Off` was rejected because cutout foliage/decals lose smoother MSAA coverage. Adding a cutout shader keyword was rejected because it recreates variant debt.
Scalability potential: Low/middle/high/ultra keep one shader ABI. Weak devices avoid accidental opaque holes; stronger devices retain cutout alpha-to-coverage polish without a separate variant.
Hardware Impact: One scalar lerp in forward output, zero texture samples, zero keywords, zero CBUFFER bytes. Prevents opaque coverage artifacts that would force duplicate materials or specialized shaders on MX350-class targets.

Problem: The rendering/material hot-method audit covered the APEX method subset but not the broader `AGENTS.md` hot-path names `Update` and `LateUpdate`. Future domain code could regress through those method names while passing the audit.
Solution: Added `Update` and `LateUpdate` to `HotMethodNames` in `HectonMasterShaderAudit1615`. Current runtime-domain grep after excluding `Editor` folders reports zero `Update` and zero `LateUpdate` bodies, but the audit now fails closed if they appear with forbidden lookup/material mutation tokens later.
Rejected Alternatives: Relying on current absence was rejected because absence is not a future-proof rule. Banning all `Update` text globally was rejected because editor windows may use `Update` and are already excluded from runtime-domain proof.
Scalability potential: Low/middle/high/ultra rendering lanes keep one dependency and material mutation discipline across all Unity hot method names, not just custom dispatcher methods.
Hardware Impact: No runtime cost. Prevents future hot scene search, material clone, or keyword mutation stalls from entering shader/rendering code under a standard Unity `Update`/`LateUpdate` name.

Problem: The audit rejected string-literal `Shader.SetGlobal*` only in the focused dispatcher/bridge route and used exact tokens that did not cover `context.cmd.SetGlobal*` or whitespace before a string argument. Runtime render features already use `context.cmd.SetGlobal*` with cached IDs, so the proof needed to cover that command-buffer form without banning legitimate render-pass global writes.
Solution: Added `StringLiteralGlobalSetterNames` and a first-argument scanner to `HectonMasterShaderAudit1615`. The runtime-domain audit now checks all rendering/material C# files after `Editor` exclusion and rejects `SetGlobalFloat/Int/Vector/Color/Texture/Buffer/Matrix/ConstantBuffer` calls whose first argument is a string literal. Current source scan reports zero such calls.
Rejected Alternatives: Banning all `SetGlobal*` in render features was rejected because RenderGraph passes legitimately publish transient textures/buffers through cached IDs after render resources are produced. A regex-only PowerShell proof was rejected as too easy to rot; the editor audit must own the durable failure mode.
Scalability potential: Low/middle/high/ultra rendering lanes can still publish globals through owner phases and render passes, but only through cached IDs. This prevents a future material/rendering patch from adding hot string hashing or managed property-name churn.
Hardware Impact: No runtime cost from the validator. Prevents string hash lookup and accidental managed work in per-frame shader global publishing on i3/MX350-class CPUs.

Problem: Direct `Shader.SetGlobal*` calls existed outside the central dispatcher in valid material/rendering owners: water optics, visual pressure aging, Shinobu material response, flora tint, and cold LUT bootstrap. The audit proved cached IDs but did not yet prove these writes stayed in presentation/cold routes.
Solution: Added `AllowedShaderGlobalWriteMethods` and `AssertShaderGlobalWritesInAllowedRoutes` to `HectonMasterShaderAudit1615`. It now rejects direct `Shader.SetGlobal*` unless the write is inside `VisualSyncTick`, `LateFrameTick`, `ExecuteGlobalDispatch`, `FlushFallbackVisualSync`, `EnsureLoadedAndBound`, `ReleaseGraphicsBuffers`, or the flora `PublishTint` helper used by `LateFrameTick` plus cold enable/disable reset.
Rejected Alternatives: Moving all existing material runtimes into `GlobalShaderDispatcher` was rejected as cross-domain churn and would risk breaking systems owned by other agents. Banning RenderGraph `context.cmd.SetGlobal*` was rejected because those are render-pass publications of transient handles, already protected by cached-ID and string-literal checks.
Scalability potential: Low/middle/high/ultra material systems can keep their specialized buffers, but direct shader globals remain in visual synchronization or cold initialization/teardown, preventing simulation-phase drift.
Hardware Impact: No runtime cost from the validator. Prevents future main-thread stalls and half-state shader reads caused by adding `Shader.SetGlobal*` to `Update`, `Execute`, pre-sim, or post-sim routes on i3/MX350-class CPUs.

Problem: The APEX hot-loop proof still allowed future Unity scene-search APIs if they were introduced under different names than `GetComponent`, and the lock proof only checked the focused shader-global owner pair rather than the runtime rendering/material domain.
Solution: Expanded `ForbiddenHotLookupTokens` with `GlobalRegistry.TryGet`, Unity object search APIs, and `GameObject.Find*` routes. Added `DataVaultWriteAcquireTokens`, `DataVaultWriteReleaseTokens`, and `AssertNoNestedDataVaultWriteLocks` to fail if a runtime rendering/material file acquires `TryAcquireMutationGuard` or `TryAcquireWriteLock` while a previous DataVault write window is still open.
Rejected Alternatives: Rewriting existing material runtimes that hold job guards through helper routes was rejected as cross-owner churn. A blanket ban on all DataVault access in rendering was rejected because cold bootstrap and owner-controlled buffer writes are valid. The implemented rule is narrower: no nested write-lock window in the domain.
Scalability potential: Low/middle/high/ultra visual systems retain specialized buffers and cold bootstraps, but future patches cannot add scene search or nested DataVault write windows to hot rendering/material code without failing the editor audit.
Hardware Impact: No runtime cost from the validator. Prevents future main-thread scene-search stalls and lock convoy/deadlock vectors on i3/MX350-class CPUs. Runtime microseconds remain `PENDING PROFILER`.

Problem: The selected-material migrator could silently degrade real project materials. `Hecton_AbyssalVoxelRock` exposes `_Base_Map`, `_Normal_Map`, and `_Mask_Map`, while `Hecton_MraoAtlasLit` exposes `_MetallicScale`, `_NormalScale`, and `_EmissionStrength`. The previous migrator ignored those aliases and could enable emission from a visible `_EmissionColor` even when `_EmissionStrength` was zero.
Solution: Added legacy texture aliases, scalar alias transfer, and `_EmissionStrength` scaling to `HectonMasterMaterialMigrator1615`. Added an unsupported-extra-texture guard for detail normals, fresh-rock/silt/cavity maps, terrain/control maps, visor/HUD maps, and separate emission maps because the 3-sample master shader cannot preserve those semantics without a bake/repack path.
Rejected Alternatives: Silently migrating complex multi-texture materials was rejected because it would trade variant debt for visual corruption. Adding extra samplers to `Hecton_Master_Lit` was rejected because it violates the three-fetch MX350 mandate and weakens SRP-batcher material ABI discipline. Mass-repacking textures was rejected without Unity import and art review proof.
Scalability potential: Low/middle/high/ultra keep one safe master ABI for simple MRAO/legacy/Standard/UberNoir materials. Complex source materials now require an explicit bake route, which can compress detail/fresh/silt/emission data into the existing packed mask instead of adding runtime variants.
Hardware Impact: No runtime cost. Prevents accidental emission overdraw and texture loss on MX350-class hardware, and blocks migration mistakes that would force later duplicate shaders or material clones.

Problem: Packed-map migration still lost source scalar math. Legacy `HectonCoreLitDecodePackedMaskV1` multiplies mask R/B by `_Metallic` and `_Smoothness`; `Hecton_MraoAtlasLit` multiplies R/G by `_MetallicScale` and `_RoughnessScale`; UberNoir ARM multiplies metallic by `_Metallic` and smoothness by `_Smoothness`. The master shader uses `lerp(fallback, mask, weight)`, so weight `1` silently discarded those scalars.
Solution: Added `ApplyMaskScalarCompatibility` to fold source metallic/smoothness/roughness scales into `_MasterSurfaceParams.x/y`. MRAO uses metallic and roughness scale as map weights with fallback roughness forced to zero. Legacy and MetallicGloss use metallic and smoothness scale with fallback smoothness forced to zero. ARM uses metallic scale and smoothness as roughness interpolation weight. `_NormalStrength` now multiplies `_NormalScale` when `_BumpScale` is absent.
Rejected Alternatives: Adding new CBUFFER lanes for every legacy scalar was rejected because it expands the material ABI and weakens SRP Batcher discipline. Adding shader keywords per mask family was rejected because it reintroduces variant debt. Accepting scalar drift was rejected because it makes migration visually dishonest.
Scalability potential: Low keeps the same three-sample master path and conservative migrated response; middle/high/ultra can preserve source-authored metal, roughness, and normal intensity while still using one shader variant and one CBUFFER layout.
Hardware Impact: No runtime sampler, keyword, or CBUFFER cost. Prevents visual rework churn and duplicate material families on i3/MX350-class hardware; measured frame microseconds remain `PENDING PROFILER`.

Problem: The fail-closed texture guard did not cover all source semantics discovered in the shader corpus. UberNoir has rust detail, blue noise, and texture arrays; voxel/dry-zone paths have micro normals, volumes, parallax/detail, and parasite overlays. Migrating those materials would drop authored effects without warning.
Solution: Expanded `UnsupportedExtraTextureNames` with `_HectonMicroNormalTex`, `_BiomeFamilyTintVolume`, `_SargassumCutMaskRT`, `_HectonDamageVolumeTex`, `_RustDetailMap`, `_BlueNoiseTex`, UberNoir arrays, parallax/detail, parasite overlay, and atlas textures. The audit now requires representative tokens from this guard.
Rejected Alternatives: Trying to emulate these effects in `Hecton_Master_Lit` was rejected because it would add samplers and turn the master shader into a new monolith of special cases. The right path is offline bake/repack into base/normal/mask before migration.
Scalability potential: Low/middle/high/ultra keep a stable simple-master lane; high/ultra material richness comes from better packed source assets, not runtime sampler sprawl.
Hardware Impact: No runtime cost. Blocks accidental feature loss and prevents future requests for extra shader variants caused by unsafe material migration.

Problem: Texture-slot rejection was still insufficient for source shaders whose entire pass contract is not master-lit compatible. UI, VFX, sky, indirect, GPU-instanced, terrain, overlay, ocean, weather, stencil, runtime control, and decal shaders can use the same texture names as surface materials while relying on fundamentally different queues, passes, lighting, or ownership routes.
Solution: Added `UnsupportedSourceShaderNameFragments` and `ContainsAnyFragment` to `HectonMasterMaterialMigrator1615`. The migrator captures `sourceShaderName` before shader swap and refuses specialized shader families before any master assignment. `HectonMasterShaderAudit1615` now requires the deny list, representative fragments, the call site, and the helper method.
Rejected Alternatives: Migrating by texture presence only was rejected because a UI/VFX/decal shader can carry `_BaseMap` and still be semantically incompatible. Adding more master passes for these families was rejected because it would turn the master material shader into a cross-domain dumping ground and reintroduce pass/variant debt.
Scalability potential: Low/middle/high/ultra keep a clean opaque/cutout surface-material lane. Specialized systems stay on their own optimized shaders until a dedicated bake/repack or domain-owned migration route exists.
Hardware Impact: No runtime cost. Prevents silent conversion of specialized materials into heavier or broken master-lit draws on i3/MX350-class hardware; measured frame microseconds remain `PENDING PROFILER`.

Problem: The migrator correctly preserved texture scale-offset, but the editor audit only proved the base-map scale read before shader swap. A future edit could silently drop normal/mask ST transfer or write texture transforms before assigning the master shader, producing UV drift in migrated materials while still passing the previous audit.
Solution: Expanded `AssertMaterialMigratorSafety` to require `baseScale`, `normalScaleVector`, and `maskScale` reads before `material.shader = masterShader`. It also now requires post-swap `CopyTexture` calls for `_BaseMap`, `_BumpMap`, and `_MaskMap`, and requires `CopyTexture` to call both `SetTextureScale` and `SetTextureOffset`.
Rejected Alternatives: Adding a second migration runtime pass or material postprocessor was rejected because selected migration already has the needed data cold in Editor. Broad `.mat` rewrites remain rejected until Unity import/material preview proof exists.
Scalability potential: Low/middle/high/ultra material lanes keep authored UV scale-offset without adding shader branches, samplers, or material keywords.
Hardware Impact: No runtime cost. Prevents UV repair churn and duplicate material variants caused by incorrect migrated normal/mask tiling on i3/MX350-class hardware.

Problem: The master shader correctly declared `_H8GlobalQualityWeight` outside `UnityPerMaterial`, but the editor audit only proved CBUFFER byte count. A future edit could move quality into the material CBUFFER, making global Math LOD per-material state and weakening SRP Batcher identity.
Solution: Added `ExtractUnityPerMaterialBlock` and audit assertions that reject `_H8GlobalQualityWeight` inside `UnityPerMaterial`, require the external global scalar, require NaN-safe global/material-cap multiplication, and require the POM zero-step bypass plus fixed 16-iteration loop masked by `step`.
Rejected Alternatives: Moving `_H8GlobalQualityWeight` into material properties was rejected because quality is runtime presentation state, not material identity. Adding shader keywords for POM quality was rejected because it reintroduces variant debt. Rewriting the shader loop was rejected because the existing fixed-loop/step-mask route already preserves the three-sample ABI and continuous LOD contract.
Scalability potential: Low uses global quality near zero and bypasses POM; middle/high/ultra raise the same global scalar under one shader binary while material cap limits only authored outliers. No material layout or DTO route changes.
Hardware Impact: No runtime cost from the validator. Prevents future per-material quality drift, hidden CBUFFER expansion, and keyword reintroduction on i3/MX350-class hardware.

Problem: Even with `_H8GlobalQualityWeight` outside the CBUFFER, it could still be accidentally exposed in the shader `Properties` block. That would serialize runtime quality into `.mat` files and create material-identity drift across quality tiers.
Solution: Added `ExtractMaterialPropertyRegion`, exact `_H8GlobalQualityWeight` token-count enforcement, and a no-token assertion against the material property region. The audit now proves global quality exists only as the standalone uniform plus its two reads in `H8MasterQuality`.
Rejected Alternatives: Accepting an inspector-visible global quality field was rejected because artists could bake per-material performance state into assets. Adding a material override quality lane was rejected because `_MasterPomParams.w` already provides a bounded per-material cap without changing the runtime global route.
Scalability potential: Low/middle/high/ultra use the same material assets and one global runtime quality scalar. Per-material authored cap remains a cap, not an independent quality owner.
Hardware Impact: No runtime cost. Prevents material variant proliferation and serialized quality divergence on i3/MX350-class hardware.

Problem: The master audit counted one `UnityPerMaterial` CBUFFER but did not prove it lived in shared `HLSLINCLUDE` before every pass. A later edit could place the CBUFFER inside one pass, leaving shadow/depth with a divergent material contract while preserving the raw count.
Solution: Added pass-layout checks to `HectonMasterShaderAudit1615`: exact pass count 3, `HLSLINCLUDE < CBUFFER_START(UnityPerMaterial) < ENDHLSL < first Pass`, and required pass name/LightMode pairs for `ForwardLit`, `ShadowCaster`, and `DepthOnly`.
Rejected Alternatives: Duplicating the same CBUFFER inside every pass was rejected because duplication invites drift and weakens the single ABI proof. Trusting visual shader structure was rejected because this must fail closed in source.
Scalability potential: Low/middle/high/ultra keep one shared material layout across forward, shadow, and depth paths. Future pass additions must be deliberate and audit-visible.
Hardware Impact: No runtime cost. Prevents SRP Batcher breakage and first-frame shadow/depth material divergence on i3/MX350-class hardware.

Problem: The parallax fake produced `parallaxDelta` in transformed base-map UV space and then added that delta directly to `_BumpMap` UVs. If base and normal tiling differed, high-quality POM could visually slide normals against albedo.
Solution: Added `H8MasterSafeRcp2` and converted base texture-space parallax to raw UV delta through `_BaseMap_ST.xy`. The normal sample now uses `TRANSFORM_TEX(input.uv + parallaxRawDelta, _BumpMap)`. The audit now requires the raw-UV route and rejects the old base-space normal offset token.
Rejected Alternatives: Adding a second mask sample to resample all packed channels after POM was rejected because it breaks the three-fetch MX350 mandate. Disabling normal parallax was rejected because high/ultra would lose depth polish. Requiring identical texture STs in migrated materials was rejected because the migrator preserves authored ST.
Scalability potential: Low remains unchanged because POM defaults to zero steps. Middle/high/ultra keep normal detail aligned with base parallax across different texture tiling without adding variants or samplers.
Hardware Impact: Adds a small vector reciprocal and multiply only when the forward shader runs; no new texture fetches, CBUFFER bytes, material keywords, or asset rewrites. Prevents visual drift that would otherwise create duplicate materials or per-shader fixes on i3/MX350-class hardware.
