# Status_1615 - MASTER_MATERIAL_AND_CBUFFER_UNIFIER

Prompt source: `Docs/Tasks/CURRENT_BATCH.md`, `<AGENT_PROMPT id="1615">`
Domain: `Assets/_Project/Art/Shaders/` and shader-global dispatcher code only when required for zero-GC visual sync.
Task count: 20
Hygiene: fresh status file created; no old batch data present.
Build rule: `dotnet build` is suppressed unless C# coordinator edits require critical verification and host CPU/compiler checks pass.
JSON dumps: suppressed by current user directive unless needed as an internal scratch artifact; durable proof goes to shader files, logs, and static scan outputs.

## Selected Registry Mandates

- Read: `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` - SRP Batcher mandatory; one CBUFFER; no material clones; no MPB for standard geometry.
- Read: `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt` - preserve noir/deep-sea visual identity through fog, dither, rough industrial material response.
- Read: `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt` - MX350 favors bounded ALU, camera-relative math, no expensive trig in inner loops.
- Read: `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` - single runtime system over 0.1 ms is suspicious; compact VRAM ceiling 1800 MB; texture budget 900 MB.
- Read: `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - hot path must remain 0 B/frame; static source proof is not runtime proof.
- Read: `DATA_Runtime_Struct_Layout_ARM64.txt` - runtime/GPU upload payloads need stable 8/16-byte alignment and explicit padding proof.
- Read: `ARCH_Execution_Phases.txt` - shader/global presentation writes belong in `VISUAL_SYNC`; no presentation mutation in simulation.
- Read: `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` - material/lighting complexity must be deterministic presentation fake before simulation.

## Task Ledger

- [x] Task 01 - EXHAUSTIVE_SHADER_CORPUS_INQUISITION
  - DOD practice: static corpus scan under `Assets/_Project/Art/Shaders`; counted files, pragmas, CBUFFERs, samples, expensive math.
  - Rejected alternative: reading only obvious lit shaders; missed hidden debt in vegetation/indirect/celestial passes.
  - Microsecond estimate: 2,100,000 us wall for full PowerShell source scan.
  - Proof: `Docs/AgentLogs/ShaderCorpus_1615.md`.
- [x] Task 02 - CBUFFER_ALIGNMENT_FORENSIC_ANALYSIS
  - DOD practice: explicit byte-offset map for new master `UnityPerMaterial`; 192 bytes, 16-byte aligned.
  - Rejected alternative: `float3`/`float2` packing in material buffer; violates clean register lanes.
  - Microsecond estimate: 180,000 us static offset audit.
  - Proof: `Docs/AgentLogs/ShaderCorpus_1615.md`.
- [x] Task 03 - SHADER_VARIANT_REDUCTION_PLANNING
  - DOD practice: candidate surface set isolated; 88 pragma lines mapped against 4 master pragmas: 3 instancing plus 1 URP punctual shadow caster vertex pragma.
  - Rejected alternative: deleting URP shadow/fog keywords from production shaders before master import proof.
  - Microsecond estimate: 500,000 us candidate scan and reduction math.
  - Proof: static reduction estimate 95.45%; compiled variant proof remains `PENDING VERIFICATION`.
- [x] Task 04 - CONTINUOUS_MATH_LOD_ALGORITHM_DESIGN
  - DOD practice: `_H8GlobalQualityWeight` drives parallax steps, normal scale, microcontrast, and low/high material fidelity continuously.
  - Rejected alternative: `_QUALITY_LOW/_QUALITY_HIGH` shader keywords; variant explosion.
  - Microsecond estimate: 120,000 us design pass.
  - Proof: implemented in `Hecton_Master_Lit.shader`.
- [x] Task 05 - TELEMETRY_AND_REPORTING_ARCHITECTURE
  - DOD practice: current user directive forbids JSON dumps; durable proof goes to `ShaderCorpus_1615.md`, `Rationale_1615.md`, `LOG_1615.md`.
  - Rejected alternative: generating `Docs/Reports/MASTER_SHADER_UNIFICATION_1615.json`; user explicitly rejected unread JSON proof.
  - Microsecond estimate: 90,000 us reporting architecture decision.
  - Proof: rationale entry and markdown ledger.
- [x] Task 06 - MASTER_LIT_SHADER_MATERIALIZATION
  - DOD practice: created `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader` with `ForwardLit`, `ShadowCaster`, and `DepthOnly` passes.
  - Rejected alternative: editing every legacy material shader in-place before import proof; too much cross-agent blast radius.
  - Microsecond estimate: 1,400,000 us shader authoring and local reread.
  - Proof: static scan reports 29 `{` / 29 `}`, three passes, fallback off.
- [x] Task 07 - UNIFIED_UNITYPERMATERIAL_CBUFFER_IMPLEMENTATION
  - DOD practice: one `CBUFFER_START(UnityPerMaterial)` with explicit 192-byte 16-byte-aligned lane map.
  - Rejected alternative: mixed `float2`/`float3` packing and per-pass material buffers; SRP Batcher hostile.
  - Microsecond estimate: 240,000 us offset map and count audit.
  - Proof: static scan reports exactly one CBUFFER start and one CBUFFER end in master.
- [x] Task 08 - MULTI_COMPILE_VARIANT_ANNIHILATION
  - DOD practice: master shader uses zero `shader_feature`, three instancing `multi_compile` pragmas, and one URP-required vertex-only punctual shadow caster pragma.
  - Rejected alternative: low/high quality keywords; violates continuous `GlobalQualityWeight`.
  - Microsecond estimate: 160,000 us pragma audit.
  - Proof: `ShaderFeature=0`, `MultiCompile=4`, `Instancing=3`, `PunctualShadowCaster=1`. Legacy shader deletion remains `PENDING MATERIAL MIGRATION`.
- [x] Task 09 - CONTINUOUS_POM_LOD_IMPLEMENTATION
  - DOD practice: `_H8GlobalQualityWeight` and material quality cap drive zero-to-16 bounded parallax iterations with no additional texture fetches.
  - Rejected alternative: true per-step POM mask sampling; exceeds the MX350 three-fetch mandate.
  - Microsecond estimate: 320,000 us implementation and UV reread.
  - Proof: master shader has exactly three `SAMPLE_TEXTURE2D` calls: mask, albedo, normal.
- [x] Task 10 - ZERO_GC_SHADER_GLOBAL_DISPATCH
  - DOD practice: inspected `GlobalShaderDispatcher`, `HectonShaderGlobalDataVaultBridge`, and `H8ShaderIDs`; hot `Shader.SetGlobal*` calls use cached integer IDs and run from VisualSync/fallback paths.
  - Rejected alternative: adding a new per-material updater; would duplicate a dispatcher route and risk hot string/property allocation.
  - Microsecond estimate: 650,000 us narrowed source scan.
  - Proof: relevant calls pass cached ID fields; `new Vector4` usages are value-type stack writes, not managed heap allocations.
- [x] Task 11 - SHADER_VARIANT_COLLECTION_COMPILATION
  - DOD practice: updated existing `Hecton8MasterVariants.shadervariants` with `Hecton_Master_Lit` empty and `INSTANCING_ON` warmup variants; wired existing editor compiler direct roots.
  - Rejected alternative: creating a second SVC asset and duplicate bootstrap path; would split warmup ownership.
  - Microsecond estimate: 420,000 us SVC/editor script edit and YAML proof.
  - Proof: master shader GUID `49aa0d16489a41c88aef21e218cbc32e` is now serialized in the SVC asset.
- [x] Task 12 - PREPROCESSOR_HLSL_OPTIMIZATION_PASS
  - DOD practice: removed direct `sqrt()` from Aegir sky via `rsqrt` approximation and kept master shader free of `pow/sin/cos/sqrt/tan/asin/acos/atan`.
  - Rejected alternative: photoreal atmosphere math; sky first frame needs cheap deterministic spectacle.
  - Microsecond estimate: 260,000 us scan and sky patch.
  - Proof: expensive math regex returns zero matches for modified master and Aegir sky files.
- [x] Task 13 - COMPILE_WALL_AND_NAMESPACE_HYGIENE
  - DOD practice: C# edit was limited to existing string manifests; no new `using`, no namespace change, brace count 53/53.
  - Rejected alternative: new editor validator assembly; unnecessary compile blast radius.
  - Microsecond estimate: 180,000 us static C# hygiene scan.
  - Proof: `HectonShaderVariantCollectionCompiler1336.cs` contains two master shader references and unchanged using set.
- [x] Task 14 - DRY_RUN_VERIFICATION_EXECUTION
  - DOD practice: traced fragment path at quality 0.0/0.5/1.0 and removed helper ternary branches where cheap `step/lerp/rsqrt` suffices.
  - Rejected alternative: dynamic quality keywords and true per-step POM sampling.
  - Microsecond estimate: 360,000 us dry-run pass and shader reread.
  - Proof: only remaining master shader quality branch is the required uniform `steps <= 0` POM bypass.
- [x] Task 15 - BATCHED_COMPILATION_AND_SYNTAX_ASSERTION
  - DOD practice: performed process and CPU gate; did not launch `dotnet build` because host CPU was 100% and two `dotnet` processes were active.
  - Rejected alternative: starting a third build; violates user directive and batch resource throttle.
  - Microsecond estimate: 90,000 us contention check plus static assertion.
  - Proof: `BLOCKED_BY_CONTENTION`; static checks used instead: HLSL brace/CBuffer/sample/pragma counts and C# brace/using counts.
- [x] Task 16 - MOCK_CBUFFER_ALIGNMENT_ASSERTION
  - DOD practice: added `HectonMasterShaderAudit1615.cs`; static PowerShell mirror calculated 192 CBUFFER bytes and 16-byte alignment.
  - Rejected alternative: relying on comments in shader file only; comments are not executable proof.
  - Microsecond estimate: 520,000 us editor audit script authoring and static mirror run.
  - Proof: script has 75 `{` / 75 `}`, 0 char-literal brace noise, 0 `List<`, 0 `Queue<`, 0 LINQ, 0 JSON writes after APEX and migration guard expansion.
- [x] Task 17 - SHADER_VARIANT_DEBT_REDUCTION_TEST
  - DOD practice: static candidate pragma scan compares 88 candidate pragma lines against 4 master pragmas, including the URP-required punctual shadow caster vertex pragma.
  - Rejected alternative: claiming Unity compiled variant counts without shader compiler database access.
  - Microsecond estimate: 270,000 us candidate reduction scan.
  - Proof: static reduction estimate 95.45%; compiled reduction remains `PENDING UNITY IMPORT`.
- [x] Task 18 - ZERO_GC_MATERIAL_UPDATE_VERIFICATION
  - DOD practice: focused dispatch scan confirms zero `Shader.SetGlobal*("literal")`, zero `CommandBuffer.SetGlobal*("literal")`, and no inspected reference-type allocator patterns in dispatch hot files.
  - Rejected alternative: adding a new material update loop; existing VisualSync/fallback routes already own shader globals.
  - Microsecond estimate: 410,000 us scan and line inspection.
  - Proof: `ShaderCorpus_1615.md` dispatch audit; `Vector4/float4` constructors are value types.
- [x] Task 19 - SRP_BATCHER_COMPLIANCE_AUDIT
  - DOD practice: custom text scan over modified shaders confirms each modified shader has one `UnityPerMaterial` start/end and zero `shader_feature`.
  - Rejected alternative: auditing all 141 shader files as if they were touched; domain-safe audit is limited to modified shader assets plus master migration candidates.
  - Microsecond estimate: 230,000 us SRP text scan.
  - Proof: master: 1 SubShader, 3 Passes, 1 CBUFFER; Aegir sky: 1 SubShader, 1 Pass, 1 CBUFFER.
- [x] Task 20 - AUTOMATED_METRIC_VALIDATOR_REPORT
  - DOD practice: final metrics were written to `Docs/AgentLogs/ShaderCorpus_1615.md`, `Rationale_1615.md`, and `LOG_1615.md`; JSON dump suppressed by current user directive.
  - Rejected alternative: generating `Docs/Reports/MASTER_SHADER_UNIFICATION_1615.json`; user explicitly called JSON dumps useless.
  - Microsecond estimate: 300,000 us final metrics and SHA256 collection.
  - Proof: SHA256 hashes recorded for modified shader/SVC/editor files.

## Loop State

- Loop 0: prompt extracted, ledgers initialized. No project files changed.
- Loop 1: mandate read complete. Next operation is shader corpus scan for Tasks 01-05.
- Loop 1 complete: Tasks 01-05 closed with static-source proof. No build, no Unity import claim.
- Loop 2 complete: Tasks 06-10 closed by source/static proof. No `dotnet build`, no Unity shader import claim.
- Loop 3 complete: Tasks 11-15 closed with SVC source proof, HLSL math scan, C# static hygiene, and build gate blocked by CPU/compiler contention.
- Loop 4 complete: Tasks 16-20 closed with editor audit script, static reduction math, zero-GC dispatch scan, SRP text scan, and final SHA256 ledger.
- Loop 5 complete: reread status/rationale, re-extracted prompt, reran static checks after edits. No build and no Unity import claim.
- Loop 6 complete: APEX integrator verification added to editor audit; nested mutation-guard detection added; master material defaults corrected so no-map fallback stays non-metallic and POM-off. No build and no Unity import claim.
- Loop 7 complete: editor audit expanded to domain-wide runtime hot-method scanning for rendering/material C# roots; master emission now requires `_EmissionColor.a` opt-in so packed height alpha cannot accidentally glow. No build and no Unity import claim.
- Loop 8 complete: editor-only selected-material migrator added; it preserves texture ST before shader swap, keeps POM off by default, uses `_EmissionColor.a` as emission opt-in, and avoids manual material keywords. No material assets were rewritten. No build and no Unity import claim.
- Loop 9 complete: master mask decoding now supports MRAO/Packed, legacy project `_MaskMap`, and Standard `_MetallicGlossMap` semantics through `_MasterShadowParams.w`; migrator sets these semantics instead of treating every mask as full MRAO. No material assets were rewritten. No build and no Unity import claim.
- Loop 10 complete: editor audit now rejects hot runtime material mutation tokens in rendering/material hot methods: `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, and `DisableKeyword`. No runtime files or material assets were rewritten. No build and no Unity import claim.
- Loop 11 complete: master shadow and depth passes now alpha-clip through the same base-map sample helper, so cutout materials no longer write full silhouettes into shadow/depth. Standard MetallicGloss layout now gates emission to zero while keeping neutral height. No new sampler, keyword, or CBUFFER lane was added. No build and no Unity import claim.
- Loop 12 complete: master `ShadowCaster` now follows the local URP 17.4 punctual-light contract with `_LightDirection`, `_LightPosition`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, and `ApplyShadowClamping`; SVC and compiler manifest now include punctual shadow variants. No new sampler, material keyword, or CBUFFER lane was added. No build and no Unity import claim.
- Loop 13 complete: master `InputData.normalizedScreenSpaceUV` now follows the URP 17.4 display-pretransform route for rotated mobile/VR displays instead of the old direct `GetNormalizedScreenSpaceUV(input.positionCS)` assignment. No new sampler, keyword, CBUFFER lane, or material asset rewrite was added. No build and no Unity import claim.
- Loop 14 complete: master mask decode now supports UberNoir ARM layout `R=AO G=roughness B=metallic A=emission` as layout `3`; migrator detects `Hecton8/Rendering/UberNoir` before shader swap and assigns layout `3`. No new sampler, keyword, CBUFFER lane, or material asset rewrite was added. No build and no Unity import claim.
- Loop 15 complete: master alpha clipping is now gated by continuous `_MasterAlphaParams.w` so opaque migrated materials do not discard from incidental albedo alpha; selected-material migrator sets clip weight only for alpha-test sources and rejects transparent/stencil sources. Migrator AO semantics now represent channel presence only, leaving `_OcclusionStrength` as the sole AO strength scalar. No sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 16 complete: selected-material migrator now preserves/assigns render surface routing after shader swap: alpha-test sources receive `RenderType=TransparentCutout` and AlphaTest queue, opaque sources receive `RenderType=Opaque` and Geometry queue, and custom source queues are preserved inside the same opaque/cutout bands. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 17 complete: editor audit now asserts that `Hecton_Master_Lit.shader.meta` exists and serializes the same GUID used by `Hecton8MasterVariants.shadervariants`, closing the asset-identity gap in the warmup proof. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 18 complete: master forward output alpha now respects clip weight before `AlphaToMask On`: opaque materials return alpha 1, cutout materials keep alpha coverage. No sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 19 complete: rendering/material hot-method audit now scans `Update` and `LateUpdate` in addition to Tick/Fixed/LateFrame/Execute/VisualSync routes. Current runtime-domain count after `Editor` exclusion is `Update=0`, `LateUpdate=0`, `LateFrameTick=5`, `Execute=14`, `VisualSyncTick=14`. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 20 complete: editor audit now scans runtime rendering/material files for string-literal global setters across `Shader.SetGlobal*`, `CommandBuffer/context.cmd.SetGlobal*`, and `SetGlobalConstantBuffer` routes. Current runtime-domain scan reports `SetGlobal*("...")` = 0 after `Editor` exclusion. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 21 complete: editor audit now rejects direct `Shader.SetGlobal*` writes outside approved presentation/cold routes: `VisualSyncTick`, `LateFrameTick`, `ExecuteGlobalDispatch`, `FlushFallbackVisualSync`, `EnsureLoadedAndBound`, `ReleaseGraphicsBuffers`, and `PublishTint`. This covers the non-dispatcher material runtimes without banning cached-ID RenderGraph `context.cmd.SetGlobal*` publication. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 22 complete: editor audit now expands hot-loop dependency rejection to scene-search APIs (`FindObjectOfType`, `FindFirstObjectByType`, `FindAnyObjectByType`, `FindObjectsOfType`, `Resources.FindObjectsOfTypeAll`, and `GameObject.Find*`) and adds a runtime-domain flat DataVault write-lock scanner for `TryAcquireMutationGuard`/`TryAcquireWriteLock` before matching releases. No runtime path, sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.
- Loop 23 complete: selected-material migrator now supports legacy voxel aliases `_Base_Map`, `_Normal_Map`, and `_Mask_Map`, transfers `_MetallicScale`, `_NormalScale`, and `_EmissionStrength`, and fails closed when unsupported extra texture slots are assigned (`_DetailNormalMap`, `_DetailMap`, fresh-rock/silt/cavity maps, terrain/control maps, visor/HUD maps, explicit emission maps). Audit now requires these migration guards. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 24 complete: selected-material migrator now preserves packed-map scalar semantics by converting `_Metallic`/`_MetallicScale`, `_Smoothness`/`_GlossMapScale`/`_Glossiness`, and `_RoughnessScale` into master map weights before shader swap. It also multiplies `_NormalStrength` with `_NormalScale` when `_BumpScale` is absent and rejects additional unsupported source textures (`_HectonMicroNormalTex`, `_RustDetailMap`, UberNoir arrays, parasite overlays, detail/parallax/atlas/volume textures). Audit now requires these guards. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 25 complete: selected-material migrator now fails closed on specialized source shader families (`Hidden`, UI/VFX/sky/celestial, flora/fauna, GPU instancer/indirect/impostor, terrain, overlays, ocean/weather, runtime/physics/submarine/terminal/scanner/tether/plasma/decal routes). Audit now requires the source-shader deny list and fragment helper. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 26 complete: editor audit now fails closed if selected-material migration stops preserving base/normal/mask texture scale-offset before shader swap or stops applying master texture scale-offset after shader swap. The master shader and migrator code were not changed. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 27 complete: editor audit now proves `_H8GlobalQualityWeight` stays outside `UnityPerMaterial` and that master POM step activation remains driven by global quality multiplied by material cap, with a zero-step bypass and branchless `step`-masked fixed loop. The master shader and migrator code were not changed. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 28 complete: editor audit now also rejects `_H8GlobalQualityWeight` in the material `Properties` region and enforces exact token count 3, preventing inspector/material serialization from turning runtime quality into material identity. The master shader and migrator code were not changed. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 29 complete: editor audit now proves the master `UnityPerMaterial` block lives in shared `HLSLINCLUDE` before all passes, and asserts exactly three named LightMode passes: `ForwardLit`, `ShadowCaster`, and `DepthOnly`. The master shader code was not changed. No material assets were rewritten; no runtime path, sampler, keyword, CBUFFER byte, or build was added.
- Loop 30 complete: master parallax normal sampling is now ST-aware. Base-space parallax delta is converted back to raw UV through `_BaseMap_ST.xy` before applying the normal-map transform, preventing normal-map drift when base and normal tiling differ. No new sampler, keyword, CBUFFER byte, material asset rewrite, or build was added.

## APEX Integrator Verification

- Hot lookup proof: focused ownership scan over `GlobalShaderDispatcher.cs`, `HectonShaderGlobalDataVaultBridge.cs`, and `SystemDispatcher.cs` found `GlobalRegistry.Get<T>()` = 0, `GetComponent` = 0, string-literal shader setters = 0.
- Phase proof: `RunDispatcherLateFrame` contains `UpdatePauseFreezeFrameDitherState`, `UpdateVisualStaticGlitchState`, `FlushSimulationBucketVisualSync`, and `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync`; request/publish methods only stage scalar state.
- Lock proof: `TryAcquireMutationGuard`/`ReleaseMutationGuard` pairs are `GlobalShaderDispatcher` 5/5 and bridge 1/1; PowerShell mirror reports 0 nested acquire-before-release violations.
- Domain lock proof: `HectonMasterShaderAudit1615` now scans runtime rendering/material C# files after `Editor` exclusion and rejects a second `TryAcquireMutationGuard` or `TryAcquireWriteLock` before `ReleaseMutationGuard` or `ReleaseWriteLock`. PowerShell mirror reports `domainFlatWriteLockScan=pass`.
- Compile throttle proof: `dotnet build` was not run. Current process gate shows active `dotnet` processes owned by the wider workspace, so build remains blocked by contention.
- C# static syntax proof: Unity MCP `validate_script` returned 0 errors for `HectonMasterShaderAudit1615.cs`, `HectonMasterMaterialMigrator1615.cs`, and `HectonShaderVariantCollectionCompiler1336.cs`. The only warning was a false-positive `GetComponent` check in the audit file; raw source scan reports 0 actual `GetComponent`/`TryGetComponent` calls and only quoted audit tokens.
- Material fallback proof: `Hecton_Master_Lit` now defaults `_MasterSurfaceParams` to `(0, 0, 1, 1)` and `_MasterPomParams` to `(0, 0, 0, 1)`; packed maps are enabled by material float weights, not by keywords.
- Domain scan proof: `HectonMasterShaderAudit1615` now scans runtime files under `Assets/_Project/Scripts/Rendering` and `Assets/_Project/Scripts/Graphics/Materials`, skips `Editor` folders, sanitizes comments/strings, and rejects forbidden lookup tokens in `Tick`, `FixedTick`, `FixedUpdate`, `LateFrameTick`, `Execute`, and `VisualSyncTick`.
- Hot-method coverage proof: the same domain scan now also includes `Update` and `LateUpdate`, matching the broader AGENTS hot-path definition instead of only the APEX subset.
- String-literal global proof: the same runtime-domain audit now rejects `SetGlobalFloat/Int/Vector/Color/Texture/Buffer/Matrix/ConstantBuffer` calls whose first argument is a string literal, covering `Shader.SetGlobal*` and `CommandBuffer/context.cmd.SetGlobal*` forms with cached integer IDs required instead.
- Direct shader global phase proof: the same runtime-domain audit now rejects direct `Shader.SetGlobal*` writes unless they are inside `VisualSyncTick`, `LateFrameTick`, the explicit dispatcher/bridge visual flush, cold LUT bootstrap, cold graphics-buffer teardown, or the flora tint helper that is driven by `LateFrameTick` plus cold enable/disable reset.
- Emission proof: `Hecton_Master_Lit` now uses `emissionHeightMask * saturate(_EmissionColor.a)` for emission mask. Default alpha is zero, and MetallicGloss layout remaps alpha to smoothness without enabling emission.
- Migration proof: `HectonMasterMaterialMigrator1615` migrates selected materials only, records Undo, copies base/normal/mask textures and ST before shader swap, sets `_MasterPomParams` to `(0,0,0,1)`, enables instancing through `material.enableInstancing`, and contains 0 `EnableKeyword(` calls.
- Migration scalar/alias proof: `HectonMasterMaterialMigrator1615` now recognizes `_Base_Map`, `_Normal_Map`, and `_Mask_Map`, maps `_Mask_Map` as legacy packed layout, reads `_MetallicScale` and `_NormalScale` aliases, scales emission RGB by `_EmissionStrength`, and disables packed-alpha emission when emission strength is zero.
- Migration packed-scale proof: `HectonMasterMaterialMigrator1615` now folds metallic/smoothness/roughness map scales into `_MasterSurfaceParams.x/y` before the master shader ignores fallback scalars on active mask lanes. Legacy masks use smoothness scale, MRAO masks use roughness scale, and ARM masks use smoothness as roughness interpolation weight.
- Migration fail-closed proof: the migrator rejects selected materials with assigned extra texture slots that cannot be preserved by the 3-sample master route; these require a bake/repack pass instead of silent migration.
- Migration source-family proof: the migrator now captures `sourceShaderName` before shader swap and rejects source shader name fragments that imply hidden, UI/VFX, sky/celestial, flora/fauna, GPU-instanced, indirect, impostor, stencil, terrain, overlay, ocean/weather, runtime/physics, vehicle/control, plasma, or decal semantics. The editor audit requires `UnsupportedSourceShaderNameFragments`, representative deny fragments, `ContainsAnyFragment(sourceShaderName, UnsupportedSourceShaderNameFragments)`, and the helper method.
- Migration ST proof: the editor audit now requires `Vector2 baseScale`, `Vector2 normalScaleVector`, and `Vector2 maskScale` reads before `material.shader = masterShader`, and requires `CopyTexture` plus `SetTextureScale`/`SetTextureOffset` writes after shader swap for `_BaseMap`, `_BumpMap`, and `_MaskMap`.
- Global quality proof: the editor audit extracts the `UnityPerMaterial` block and the material `Properties` region, rejects `_H8GlobalQualityWeight` in both, enforces exact token count 3, and requires the external `float _H8GlobalQualityWeight;`, NaN-safe global/material-cap math, and quality-scaled POM fixed loop tokens.
- ST-aware parallax proof: `Hecton_Master_Lit` now converts `parallaxDelta` to `parallaxRawDelta` with `H8MasterSafeRcp2(_BaseMap_ST.xy)` before sampling `_BumpMap`; the audit requires this route and rejects the old `TRANSFORM_TEX(input.uv, _BumpMap) + parallaxDelta` base-space offset.
- Mask layout proof: `Hecton_Master_Lit` uses `_MasterShadowParams.w` as a continuous material scalar for mask layout remap: `0` MRAO, `1` legacy R/metal G/AO B/smoothness, `2` Standard MetallicGloss alpha/smoothness. `HectonMasterMaterialMigrator1615` derives this from the source texture name and keeps raw `EnableKeyword(` count at 0.
- UberNoir ARM proof: `Hecton_Master_Lit` now treats mask layout `3` as `R=AO G=roughness B=metallic A=emission`. `HectonMasterMaterialMigrator1615` captures `sourceShaderName` before shader replacement and maps `Hecton8/Rendering/UberNoir` `_MaskMap` to layout `3` instead of legacy layout `1`.
- Alpha/AO migration proof: `Hecton_Master_Lit` now computes `clip(lerp(1.0h, clipValue, clipWeight))` from `_MasterAlphaParams.w`, defaulting opaque materials to no discard. `HectonMasterMaterialMigrator1615` writes `_MasterAlphaParams` explicitly, resolves alpha-test clip weight before shader swap, rejects transparent/stencil sources, and no longer passes `_OcclusionStrength` into `MaskSemantics`.
- Alpha-to-coverage proof: `Hecton_Master_Lit` now returns `lerp(1.0h, alpha, saturate(_MasterAlphaParams.w))` from `Frag`, so `AlphaToMask On` cannot punch opaque MSAA coverage from incidental albedo alpha.
- Surface routing migration proof: `HectonMasterMaterialMigrator1615` resolves target render queue before shader swap and applies `SetOverrideTag("RenderType", "TransparentCutout")` or `SetOverrideTag("RenderType", "Opaque")` plus `material.renderQueue = targetRenderQueue` after the master shader is assigned. `HectonMasterShaderAudit1615` asserts this route.
- Asset identity proof: `HectonMasterShaderAudit1615` now reads `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader.meta` and requires `guid: 49aa0d16489a41c88aef21e218cbc32e`, the same GUID serialized in the master SVC.
- Shared-pass CBUFFER proof: `HectonMasterShaderAudit1615` now requires `HLSLINCLUDE < CBUFFER_START(UnityPerMaterial) < ENDHLSL < first Pass`, counts exactly three shader `Pass` blocks, and requires `ForwardLit/UniversalForward`, `ShadowCaster/ShadowCaster`, and `DepthOnly/DepthOnly` routes.
- Hot material mutation proof: `HectonMasterShaderAudit1615` rejects runtime hot-method `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, and `DisableKeyword` tokens inside the rendering/material domain. Raw runtime-domain scan for these tokens is 0 after excluding `Editor` folders.
- Cutout pass proof: `ShadowFrag(ShadowVaryings input)` and `DepthFrag(DepthVaryings input)` call `H8MasterClipAlphaFromRawUv`. Static source count remains exactly 3 `SAMPLE_TEXTURE2D` calls because `_BaseMap` sampling is centralized in `H8MasterSampleBase`.
- URP shadow proof: `ShadowVert` no longer calls `ApplyShadowBias(... _MainLightPosition.xyz)`. It uses URP `_LightDirection` for directional casters, `_LightPosition - positionWS` for punctual casters, and `ApplyShadowClamping(positionCS)` after bias.
- Mobile/VR screen-space proof: `Hecton_Master_Lit` now routes `InputData.normalizedScreenSpaceUV` through `H8MasterNormalizedScreenSpaceUv`, including `UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_0/90/180/270` cases. The old direct `GetNormalizedScreenSpaceUV(input.positionCS)` assignment is rejected by `HectonMasterShaderAudit1615`.
- Loop 14 static proof: shader braces 37/37, CBUFFER 1/1, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, `multi_compile` 4, instancing 3, expensive math 0, ARM layout token count 4, old direct screen-UV route 0. Unity MCP `validate_script` returned 0 errors and 0 warnings for audit, migrator, and SVC compiler. Runtime-domain forbidden hot token scan returned 0 hits.
- Loop 15 static proof: shader braces 37/37, CBUFFER bytes 192 aligned, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, `multi_compile` 4, expensive math 0, old always-clip token 0. Unity MCP `validate_script` returned 0 errors and 0 warnings for audit and migrator. Runtime-domain forbidden hot token scan returned 0 hits.
- Loop 16 static proof: `HectonMasterMaterialMigrator1615.cs` braces 27/27, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0; `HectonMasterShaderAudit1615.cs` braces 75/75, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0. Unity MCP `validate_script` returned 0 errors and 0 warnings for both edited scripts. Runtime-domain forbidden hot token scan returned 0 hits.
- Loop 17 static proof: `HectonMasterShaderAudit1615.cs` braces 75/75, `List<` 0, raw `EnableKeyword(` 0, `MasterShaderMetaPath` assertion present. Unity MCP `validate_script` returned 0 errors and 0 warnings. `git diff --check` returned clean for the edited audit script.
- Loop 18 static proof: `Hecton_Master_Lit.shader` braces 37/37, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, `multi_compile` 4, expensive math 0, output-alpha clip-weight route present. `HectonMasterShaderAudit1615.cs` braces 75/75 and asserts the route. Unity MCP `validate_script` returned 0 errors and 0 warnings for the audit script.
- Loop 19 static proof: `HectonMasterShaderAudit1615.cs` braces 75/75, `List<` 0, raw `EnableKeyword(` 0, hot method array includes `Update` and `LateUpdate`. Runtime rendering/material method count after `Editor` exclusion: `Tick=0`, `Update=0`, `FixedTick=0`, `FixedUpdate=0`, `LateUpdate=0`, `LateFrameTick=5`, `Execute=14`, `VisualSyncTick=14`. Unity MCP `validate_script` returned 0 errors and 0 warnings.
- Loop 20 static proof: `HectonMasterShaderAudit1615.cs` braces 102/102, `List<` 0, raw `EnableKeyword(` 0, `SetGlobalLiteralPatternInAuditSource` 0. Runtime rendering/material string-literal global setter scan after `Editor` exclusion: 0. Unity MCP `validate_script` returned 0 errors and 0 warnings.
- Loop 21 static proof: `HectonMasterShaderAudit1615.cs` braces 107/107, `List<` 0, raw `EnableKeyword(` 0, direct shader-global route guard present. Unity MCP `validate_script` returned 0 errors and 0 warnings.
- Loop 22 static proof: `HectonMasterShaderAudit1615.cs` braces 117/117, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0, expanded scene-search hot tokens present, flat DataVault lock guard present. PowerShell runtime-domain flat write-lock scan passed. Unity MCP `validate_script` was blocked by HTTP transport failure at `127.0.0.1:8088`; no success is claimed.
- Loop 23 static proof: `HectonMasterMaterialMigrator1615.cs` braces 30/30, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0; `HectonMasterShaderAudit1615.cs` braces 117/117, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0. Hashes: migrator `838B92832A12814864991DA760857BFA8702FCF9247B8D5F30D161FA39443769`, audit `54E2A9910984597E349F61CB3400A32C9482D4EC4414EA003EB6BBCACB2A50DF`. Unity MCP `validate_script` was blocked by HTTP transport failure at `127.0.0.1:8088`; no success is claimed.
- Loop 24 static proof: `HectonMasterMaterialMigrator1615.cs` braces 33/33, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0; `HectonMasterShaderAudit1615.cs` braces 117/117, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0. Hashes: migrator `CE1CB5C2E1C71EBE984C6A42ECA3FFBF9105A9141F9B45AE60CFFCB99E640BE1`, audit `159A7093900D06623D71C2B0689FEC7B668C68D74FC8DD898A047398E46D7EAC`. Unity MCP was not used in this loop; prior transport failure remains unresolved.
- Loop 25 static proof: `HectonMasterMaterialMigrator1615.cs` braces 36/36, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0; `HectonMasterShaderAudit1615.cs` braces 117/117, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0. Hashes: migrator `C8766B3AAF208B533A96F809550FBBCCF41F0641E9DCFEBA4BEE27B73929DA20`, audit `CA0A888BCF417F8DDE26D865D7C67A4C41D8576AD03528BCA4CF4F5D34135C49`. `git diff --check` returned clean for edited source/docs. Unity MCP was not used; prior transport failure remains unresolved.
- Loop 26 static proof: `Hecton_Master_Lit.shader` braces 37/37, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, hash unchanged `121D483200A799EEFAFBB9F706400F8FD50AD814A4D75EA52F519CE060B352C1`; `HectonMasterMaterialMigrator1615.cs` braces 36/36, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0, hash unchanged `C8766B3AAF208B533A96F809550FBBCCF41F0641E9DCFEBA4BEE27B73929DA20`; `HectonMasterShaderAudit1615.cs` braces 117/117, `List<` 0, LINQ hot patterns 0, raw `EnableKeyword(` 0, hash `FCA99D0B9910A77B50206E7B33CDF5331BBCE77CA295DEC4719F9DD055CE21EE`. `git diff --check` returned clean for edited source/docs. Unity MCP was not used; prior transport failure remains unresolved.
- Loop 27 static proof: `Hecton_Master_Lit.shader` quality scalar inside CBUFFER = 0, total quality scalar token count = 3, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, `multi_compile` 4; `HectonMasterShaderAudit1615.cs` braces 118/118, hash `73f98a51db759225dc031996b02de31c142462928358cb1f8dd700563aaf3fe8`. `git diff --check` returned clean for the edited audit script. Unity MCP `validate_script` returned 0 errors and 0 warnings for the edited audit script.
- Loop 28 static proof: `Hecton_Master_Lit.shader` quality scalar total = 3, quality scalar in material Properties = 0, quality scalar inside CBUFFER = 0; `HectonMasterShaderAudit1615.cs` braces 119/119, hash `e1fa5cd7c0f01a6c654809e1e4cf8a32e8f7af35ad4382bb08f367a168e41a01`. `git diff --check` returned clean for the edited audit script. Unity MCP `validate_script` returned 0 errors and 0 warnings for the edited audit script.
- Loop 29 static proof: `Hecton_Master_Lit.shader` layout indices `HLSLINCLUDE=1397`, `CBUFFER=1909`, first shared `ENDHLSL=17805`, first `Pass=17822`, pass count `3`; `HectonMasterShaderAudit1615.cs` braces 124/124, `List<` 0, LINQ hot patterns 0, hash `235d05f490cf35a4e4551cdb32a282f60da26dd73f5c00e3d068240a0358301c`. `git diff --check` returned clean for the edited audit script. Unity MCP `validate_script` returned 0 errors and 0 warnings for the edited audit script.
- Loop 30 static proof: `Hecton_Master_Lit.shader` braces 38/38, `SAMPLE_TEXTURE2D` 3, `shader_feature` 0, `multi_compile` 4, expensive math 0, old base-space normal offset token 0, new raw-UV normal offset token 1, hash `eefe67853753fd125d241187451aefad221d3df5841f14eb84d33146417c35ee`; `HectonMasterShaderAudit1615.cs` braces 124/124, `List<` 0, LINQ hot patterns 0, hash `597bfeea0ce9e0564a13f39e3cd6feaa25f18c30c4b6ced8cecc25777675b2c3`. `git diff --check` returned clean for edited shader/audit. Unity MCP `validate_script` first hit transport disconnect, then retry returned 0 errors and 0 warnings for the edited audit script.
- Compile throttle proof update: `dotnet build` was not run. Latest gate showed CPU 37.475445% but active `dotnet` processes PIDs 15112 and 25728, so compiler contention still blocks build.

## First 20 Minutes

Moment: World load / first visual frame.
Route impact: Fewer shader variants and SRP-batcher-compatible materials reduce first-route load hitch risk and keep route-critical environment visible.
Proof required: static shader audit now; Unity shader import, Frame Debugger, GPU timings, and texture memory remain `PENDING VERIFICATION`.
Parked work rejected: no broad scene/material reassignment until shader corpus and pass layout prove targets.
