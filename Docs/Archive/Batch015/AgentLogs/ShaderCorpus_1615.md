# Shader Corpus 1615 Static Ledger

Evidence class: STATIC_SOURCE.
Runtime, Unity import, Frame Debugger, RenderDoc, GPU timing, and shader compiler proof remain `PENDING VERIFICATION`.

## Corpus Scan

Scope: `Assets/_Project/Art/Shaders/**/*.shader|*.hlsl|*.shaderprogram`

- Files after master shader addition: 141
- Shader/HLSL compile pragmas matched: 201
- `CBUFFER_START(UnityPerMaterial)` declarations matched: 100
- Texture sample calls matched: 283
- Expensive math calls `pow|sin|cos|sqrt`: 42
- Existing `Hecton_Master_Lit.shader` before work: absent

## First Surface Consolidation Set

These are surface/lit material candidates, not UI/post/sky/stencil-only passes.

| File | Pragmas | CBuffers | Samples | Expensive math |
|---|---:|---:|---:|---:|
| `Core/Hecton8_UberNoir.shader` | 12 | 0 in shader file, CBUFFER in include | 0 in shader file | 0 |
| `Hecton_AbyssalVoxelRock.shader` | 10 | 1 | 7 | 0 |
| `Hecton_CoralMaster.shader` | 8 | 1 | 2 | 0 |
| `Hecton_CoralMaster_GPUI.shader` | 8 | 1 | 2 | 0 |
| `Hecton_KelpMaster.shader` | 8 | 3 | 2 | 0 |
| `Hecton_KelpMaster_GPUI.shader` | 8 | 3 | 2 | 0 |
| `Hecton_ProceduralBio.shader` | 6 | 1 | 10 | 0 |
| `Hecton_SargassumMaster.shader` | 6 | 2 | 4 | 0 |
| `Hecton_DryZoneLit.shader` | 5 | 1 | 5 | 0 |
| `Hecton_WreckIndirectLit.shader` | 4 | 1 | 2 | 0 |
| `Hecton_ToolDecayLit.shader` | 1 | 1 | 3 | 0 |
| `Hecton_RuinSeepSheen.shader` | 1 | 1 | 1 | 0 |
| `Hecton_MarauderOutpostIndirect.shader` | 2 | 1 | 0 | 0 |
| `Hecton_LeviathanOrganic.shader` | 4 | 1 | 3 | 0 |
| `Hecton_ScatterIndirectLit.shader` | 4 | 1 | 0 | 0 |

Surface candidate pragma total: 88.
New master shader pragmas: 4: three instancing pragmas plus one URP punctual shadow caster vertex pragma.
Static pragma debt reduction estimate if candidates migrate to master: 95.45%.
Compiled variant reduction is not claimed without Unity shader compiler data.

## Master Shader Static Row

File: `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader`

- Shader name: `Hecton8/Rendering/Hecton_Master_Lit`
- Passes: `ForwardLit`, `ShadowCaster`, `DepthOnly`
- Texture samples in ForwardLit path: 3 (`_MaskMap`, `_BaseMap`, `_BumpMap`)
- `pow/sin/cos/sqrt` calls: 0
- `shader_feature` pragmas: 0
- `multi_compile` pragmas: 4: three `multi_compile_instancing` plus `_CASTING_PUNCTUAL_LIGHT_SHADOW` for URP point/spot shadow caster bias
- `UnityPerMaterial` size: 192 bytes, 12 x 16-byte registers
- Mask layout decode: `_MasterShadowParams.w` selects 0 = MRAO, 1 = legacy R/metal G/AO B/smoothness, 2 = Standard MetallicGloss alpha/smoothness. This uses the existing CBUFFER lane and keeps the ForwardLit sample count at 3.
- Cutout consistency: `ShadowCaster` and `DepthOnly` now call `H8MasterClipAlphaFromRawUv`, so alpha-cutout materials do not write full opaque silhouettes into shadows/depth.
- Standard emission gate: layout 2 uses `emissionLayoutWeight` to prevent MetallicGloss smoothness alpha from becoming emission.
- URP shadow caster compatibility: `ShadowCaster` declares `_LightDirection` and `_LightPosition`, selects `_CASTING_PUNCTUAL_LIGHT_SHADOW` for punctual lights, and calls `ApplyShadowClamping(positionCS)` after bias.
- Mobile/VR screen-space compatibility: `InputData.normalizedScreenSpaceUV` now uses `H8MasterNormalizedScreenSpaceUv`, mirroring URP 17.4 `UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION` handling for 0/90/180/270 degree display rotations.

## CBUFFER Layout

| Offset | Field | Size |
|---:|---|---:|
| 0 | `_BaseMap_ST` | 16 |
| 16 | `_BumpMap_ST` | 16 |
| 32 | `_MaskMap_ST` | 16 |
| 48 | `_BaseColor` | 16 |
| 64 | `_EmissionColor` | 16 |
| 80 | `_MasterSurfaceParams` | 16 |
| 96 | `_MasterAlphaParams` | 16 |
| 112 | `_MasterPomParams` | 16 |
| 128 | `_MasterNoirParams` | 16 |
| 144 | `_MasterShadowParams` | 16 |
| 160 | `_Metallic` | 4 |
| 164 | `_Smoothness` | 4 |
| 168 | `_OcclusionStrength` | 4 |
| 172 | `_BumpScale` | 4 |
| 176 | `_Cutoff` | 4 |
| 180 | `_H8MasterPadding0` | 4 |
| 184 | `_H8MasterPadding1` | 4 |
| 188 | `_H8MasterPadding2` | 4 |

Proof: total size 192 bytes; 192 % 16 = 0. No `float2` or `float3` exists in `UnityPerMaterial`.

## Sky Local Audit

File: `Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader`

- Ring shadow on planet: present through `RingShadow(...)`.
- Procedural stars: present through `StarField(...)`.
- Texture samples: 1 (`_AegirBandTex`).
- `pow/sin/cos/sqrt` calls after local cleanup: 0.

## Variant Collection Proof

File: `Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants`

- Master shader GUID: `49aa0d16489a41c88aef21e218cbc32e`.
- Serialized variants added: empty keyword passType 0, `INSTANCING_ON` passType 0, `_CASTING_PUNCTUAL_LIGHT_SHADOW` passType 0, and `_CASTING_PUNCTUAL_LIGHT_SHADOW INSTANCING_ON` passType 0.
- Existing editor compiler direct root now includes `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader`.
- Existing editor compiler explicit keyword manifest now includes `INSTANCING_ON`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, and the combined instanced punctual route for the master shader.
- No second SVC asset was created.

## Editor Audit Script

File: `Assets/_Project/Scripts/Editor/HectonMasterShaderAudit1615.cs`

- Parses `Hecton_Master_Lit.shader` from disk.
- Asserts exactly one `UnityPerMaterial` CBUFFER.
- Asserts CBUFFER byte size is 16-byte aligned.
- Asserts exactly three `SAMPLE_TEXTURE2D` calls.
- Asserts zero `shader_feature` pragmas.
- Asserts exactly four `multi_compile` pragmas: three instancing plus one URP punctual shadow caster vertex pragma.
- Asserts zero `pow/sin/cos/sqrt/tan/asin/acos/atan` calls.
- Asserts the master SVC serializes the master shader GUID.
- Asserts the master SVC serializes `_CASTING_PUNCTUAL_LIGHT_SHADOW`.
- Static script scan: 75 `{` / 75 `}`, 0 char-literal brace noise, 0 `List<`, 0 `Queue<`, 0 LINQ, 0 JSON writes.
- Unity MCP `validate_script` scan: 0 errors for `HectonMasterShaderAudit1615.cs`, `HectonMasterMaterialMigrator1615.cs`, and `HectonShaderVariantCollectionCompiler1336.cs`; audit-file `GetComponent` warning is a quoted-token false positive, with 0 actual calls by raw source regex.
- APEX additions: no hot `GlobalRegistry.Get`/`GetComponent` in inspected shader-global owners, deferred visual sync assertions in `RunDispatcherLateFrame`, and nested mutation guard rejection.
- Domain-hot additions: runtime rendering/material C# roots are scanned outside `Editor` folders; comments and strings are sanitized before checking `Tick`, `FixedTick`, `FixedUpdate`, `LateFrameTick`, `Execute`, and `VisualSyncTick`.
- Hot material mutation additions: the same domain-hot scan rejects `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, and `DisableKeyword` tokens inside runtime rendering/material hot methods.
- Material migration guard: `_MasterSurfaceParams`, `_MasterPomParams`, and `_MasterShadowParams.w` mask-layout defaults are asserted; direct packed-alpha emission is rejected in favor of `_EmissionColor.a` opt-in.
- Alpha migration guard: `_MasterAlphaParams.w` is asserted as clip weight; audit rejects the old always-clip token and requires selected-material migration to set clip weight from source alpha-test state.
- Shadow/depth cutout guard: audit asserts `ShadowFrag(ShadowVaryings input)`, `DepthFrag(DepthVaryings input)`, zero-initialized varyings, and `H8MasterClipAlphaFromRawUv`.
- URP shadow guard: audit asserts `_LightDirection`, `_LightPosition`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, `ApplyShadowClamping(positionCS)`, and rejects the old `_MainLightPosition.xyz` bias route.
- Screen-space UV guard: audit asserts `H8MasterNormalizedScreenSpaceUv`, `UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_90`, and rejects the old direct `GetNormalizedScreenSpaceUV(input.positionCS)` assignment.
- UberNoir ARM guard: audit asserts layout `3` decode for `R=AO G=roughness B=metallic A=emission` and rejects migration paths that do not capture `sourceShaderName` before shader swap.
- Selected-material migration tool: `HectonMasterMaterialMigrator1615.cs` exists, uses Undo, copies texture ST before shader swap, distinguishes MRAO/Packed, legacy `_MaskMap`, Standard `_MetallicGlossMap`, and `Hecton8/Rendering/UberNoir` ARM masks, keeps POM disabled by default, rejects transparent/stencil source shaders, writes `_MasterAlphaParams` explicitly, treats AO weight as channel presence only, reapplies opaque/cutout RenderType and render queue after shader swap, and has 0 raw `EnableKeyword(` calls.

## Modified Shader SRP Batcher Audit

| File | SubShaders | Passes | CBUFFER_START | CBUFFER_END | ShaderFeature | Samples | Expensive math |
|---|---:|---:|---:|---:|---:|---:|---:|
| `Hecton_Master_Lit.shader` | 1 | 3 | 1 | 1 | 0 | 3 | 0 |
| `Sky/Hecton_AegirSky.shader` | 1 | 1 | 1 | 1 | 0 | 1 | 0 |

## Shader Global Dispatch Audit

Scope:
- `Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs`
- `Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs`
- `Assets/_Project/Scripts/Graphics/Materials/H8ShaderIDs.cs`

Results:
- `Shader.SetGlobal*("literal")`: 0 matches.
- `CommandBuffer.SetGlobal*("literal")`: 0 matches.
- Hot inspected dispatcher code uses cached `Shader.PropertyToID` integer fields.
- Reference-type allocator scan for `new List/Dictionary/Queue/HashSet/StringBuilder/MaterialPropertyBlock/Material/Texture/RenderTexture`: 0 matches in inspected dispatch files.
- Existing `new Vector4` and `new float4` instances are value-type construction, not managed heap allocation.

## Domain Hot Material Mutation Audit

Scope:
- `Assets/_Project/Scripts/Rendering`
- `Assets/_Project/Scripts/Graphics/Materials`

Rules:
- `Editor` folders are skipped.
- Comments and strings are sanitized before method-body scan.
- Hot methods: `Tick`, `Update`, `FixedTick`, `FixedUpdate`, `LateUpdate`, `LateFrameTick`, `Execute`, `VisualSyncTick`.
- Rejected hot tokens: `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, `DisableKeyword`.
- Global setter first-argument scan rejects string literals for `SetGlobalFloat`, `SetGlobalInt`, `SetGlobalVector`, `SetGlobalColor`, `SetGlobalTexture`, `SetGlobalBuffer`, `SetGlobalMatrix`, array variants, and `SetGlobalConstantBuffer`.
- Direct `Shader.SetGlobal*` writes are allowed only in `VisualSyncTick`, `LateFrameTick`, `ExecuteGlobalDispatch`, `FlushFallbackVisualSync`, `EnsureLoadedAndBound`, `ReleaseGraphicsBuffers`, or `PublishTint`.

Results:
- Raw runtime-domain hot material mutation hits after `Editor` exclusion: 0.
- Runtime-domain string-literal global setter hits after `Editor` exclusion: 0.
- Direct runtime-domain `Shader.SetGlobal*` writes outside approved visual/cold routes: 0 by editor audit.
- Runtime-domain method counts after `Editor` exclusion: `Tick=0`, `Update=0`, `FixedTick=0`, `FixedUpdate=0`, `LateUpdate=0`, `LateFrameTick=5`, `Execute=14`, `VisualSyncTick=14`.
- The audit fails closed if these tokens enter a hot method body.

## UberNoir ARM Mask Layout Audit

Source shader:
- `Hecton8/Rendering/UberNoir`

Observed contract:
- `_MaskMap("Packed ARM Emission", 2D)`
- R: ambient occlusion.
- G: roughness.
- B: metallic / anisotropic material response.
- A: emission / scanline mask.

Master route:
- `_MasterShadowParams.w = 3` selects ARM layout.
- `metallicMask` uses `packedMask.b`.
- `roughnessMask` uses `packedMask.g`.
- `occlusionMask` uses `packedMask.r`.
- `emissionHeightMask` keeps `packedMask.a`.
- No sampler, keyword, CBUFFER lane, material asset rewrite, or texture repack was added.

Migrator route:
- Captures `sourceShaderName` before assigning `Hecton8/Hecton_Master_Lit`.
- Maps `Hecton8/Rendering/UberNoir` `_MaskMap` to layout `3`.
- Other `_MaskMap` sources remain legacy layout `1`.
- `_MetallicGlossMap` remains Standard layout `2`.

## Alpha Clip And AO Strength Audit

Master route:
- `_MasterAlphaParams.w = 0` disables discard through `clip(lerp(1.0h, clipValue, clipWeight))`.
- `_MasterAlphaParams.w = 1` enables alpha-test/dithered cutout behavior.
- No alpha keyword, sampler, or CBUFFER byte was added.

Migrator route:
- Transparent sources are rejected when `RenderType` or `Queue` resolves to `Transparent`, or render queue is `>=3000`.
- Stencil sources are rejected by source shader name because the master PBR shader cannot preserve stencil-only `ColorMask 0` behavior.
- Alpha-test sources receive clip weight `1` from `RenderType=TransparentCutout`, `Queue=AlphaTest`, render queue `2450..2999`, or `_AlphaClip > 0.5`.
- Opaque sources receive clip weight `0`.
- AO channel presence is `1` for MRAO/legacy/UberNoir ARM masks and `0` for Standard MetallicGloss/no mask. `_OcclusionStrength` remains the only AO strength scalar.

Surface routing:
- `ResolveTargetRenderQueue` runs before shader swap.
- Alpha-test sources keep valid AlphaTest-band custom queues, otherwise use `UnityEngine.Rendering.RenderQueue.AlphaTest`.
- Opaque sources keep valid Geometry-band custom queues, otherwise use `UnityEngine.Rendering.RenderQueue.Geometry`.
- `ApplySurfaceRouting` writes `RenderType=TransparentCutout` or `RenderType=Opaque`, then writes `material.renderQueue`.

Alpha-to-coverage route:
- `AlphaToMask On` stays enabled for cutout coverage.
- Forward output alpha is now `lerp(1.0h, alpha, saturate(_MasterAlphaParams.w))`.
- Opaque materials with clip weight `0` write alpha 1, preventing incidental albedo alpha from reducing MSAA coverage.
- Cutout materials with clip weight `1` keep authored alpha coverage.

## Master Shader Asset Identity Audit

Master shader GUID route:
- `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader.meta` contains `guid: 49aa0d16489a41c88aef21e218cbc32e`.
- `Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants` serializes the same GUID.
- `HectonMasterShaderAudit1615` now fails closed if the meta GUID is missing or mismatched.

## APEX Runtime Domain Guard Audit

Hot dependency route:
- Runtime rendering/material hot methods now reject `GlobalRegistry.Get`, `GlobalRegistry.TryGet`, `GlobalRegistry.Resolve`, `GetComponent`, `TryGetComponent`, Unity object searches, `Resources.FindObjectsOfTypeAll`, `GameObject.Find*`, `.material`, `.materials`, `SetPropertyBlock`, `EnableKeyword`, and `DisableKeyword`.
- PowerShell raw scan after `Editor` exclusion found only editor references for the newly banned scene-search/material mutation tokens.

Flat DataVault write-lock route:
- `HectonMasterShaderAudit1615` scans runtime rendering/material files and rejects a second `TryAcquireMutationGuard` or `TryAcquireWriteLock` before `ReleaseMutationGuard` or `ReleaseWriteLock`.
- PowerShell mirror result: `domainFlatWriteLockScan=pass`.
- Scope is lock-window flattening, not a claim that every neighboring runtime owner has been semantically refactored.

## Material Migrator Compatibility Audit

Accepted legacy aliases:
- `_Base_Map` -> `_BaseMap`.
- `_Normal_Map` -> `_BumpMap`.
- `_Mask_Map` -> `_MaskMap`, decoded as legacy layout `1`.
- `_MetallicScale` and `_NormalScale` are read when `_Metallic` or `_BumpScale` are absent.
- `_NormalStrength` multiplies `_NormalScale` when `_BumpScale` is absent.
- `_RoughnessScale`, `_Smoothness`, `_GlossMapScale`, and `_Glossiness` are folded into master mask weights rather than being discarded by active packed-map lanes.
- `_EmissionStrength` scales emission RGB and disables packed-alpha emission when zero.

Texture scale-offset route:
- Base, normal, and mask scale-offset are read before `material.shader = masterShader`.
- `_BaseMap`, `_BumpMap`, and `_MaskMap` are assigned after shader swap through `CopyTexture`.
- `CopyTexture` writes texture reference, `SetTextureScale`, and `SetTextureOffset`.
- `HectonMasterShaderAudit1615` now fails if any of these transfer tokens or ordering constraints are removed.

Global quality route:
- `_H8GlobalQualityWeight` is declared after `CBUFFER_END`, not inside `UnityPerMaterial`.
- `HectonMasterShaderAudit1615` extracts the CBUFFER block and material `Properties` region, rejecting `_H8GlobalQualityWeight` in both.
- Exact `_H8GlobalQualityWeight` token count is 3: uniform declaration plus two reads in `H8MasterQuality`.
- `H8MasterQuality` multiplies NaN-safe `saturate(_H8GlobalQualityWeight)` by NaN-safe `saturate(_MasterPomParams.w)`.
- POM uses `floor(saturate(quality) * clamp(_MasterPomParams.y, 0.0, 16.0) + 0.5)`, a zero-step bypass, and a fixed 16-iteration loop masked by `step`.
- POM normal sampling converts base texture-space `parallaxDelta` to raw UV through `H8MasterSafeRcp2(_BaseMap_ST.xy)` before `_BumpMap` transform.
- Old base-space normal offset `TRANSFORM_TEX(input.uv, _BumpMap) + parallaxDelta` is rejected by audit.

Shared pass route:
- `HLSLINCLUDE` index `1397`.
- `CBUFFER_START(UnityPerMaterial)` index `1909`.
- First shared `ENDHLSL` index `17805`.
- First `Pass` index `17822`.
- Pass count is exactly `3`: `ForwardLit`, `ShadowCaster`, `DepthOnly`.
- LightMode routes are `UniversalForward`, `ShadowCaster`, and `DepthOnly`.

Packed scalar route:
- MRAO layout `0`: `_MasterSurfaceParams.x = metallicScale`, `_MasterSurfaceParams.y = roughnessScale`, `_Metallic = 0`, `_Smoothness = 1`.
- Legacy layout `1`: `_MasterSurfaceParams.x = metallicScale`, `_MasterSurfaceParams.y = smoothnessScale`, `_Metallic = 0`, `_Smoothness = 0`.
- MetallicGloss layout `2`: `_MasterSurfaceParams.x = metallicScale`, `_MasterSurfaceParams.y = smoothnessScale`, `_Metallic = 0`, `_Smoothness = 0`.
- UberNoir ARM layout `3`: `_MasterSurfaceParams.x = metallicScale`, `_MasterSurfaceParams.y = smoothnessScale`, `_Metallic = 0`.

Fail-closed texture slots:
- `_DetailNormalMap`, `_DetailMap`, `_DetailMask`, `_DetailAlbedoMap`.
- `_FreshRockAlbedoMap`, `_FreshRockNormalMap`, `_SiltLayerMap`, `_CavityNoiseRamp`.
- `_HectonMicroNormalTex`, `_BiomeFamilyTintVolume`, `_SargassumCutMaskRT`, `_HectonDamageVolumeTex`.
- `_RustDetailMap`, `_BlueNoiseTex`, `_H8UberNoirAlbedoArray`, `_H8UberNoirNormalArray`, `_H8UberNoirMaskArray`.
- `_TerrainControlRGBA`, `_FlowNormal`, `_DetailTex`, `_ParallaxMap`.
- `_EmissionTex`, `_EmissionMap`, `_ParasiteOverlayMap`, `_ParasiteNormalMap`, `_NormalAtlas`, `_MaskAtlas`, `_BaseAtlas`.
- `_HUD_RenderTexture`, `_ScratchNormalMap`, `_FingerprintTex`, `_WaterRunoffNormalTex`, `_WaterDropletMaskTex`.

Fail-closed source shader families:
- Hidden, UI, VFX, sky, celestial, flora, fauna.
- GPUInstancer, indirect, impostor, stencil, terrain.
- Visor, PDA, hologram, sonar, radar, ocean, weather, fabrication.
- Physics/runtime, submarine, terminal, scanner, tether, plasma, fluid decal, decal.

Reason:
- These maps and source shader families encode semantics that cannot be preserved by the 3-sample opaque/cutout master route without a bake/repack pass or a dedicated domain-owned shader.
- No `.mat` files were changed.

## SHA256 Proof

- `EEFE67853753FD125D241187451AEFAD221D3DF5841F14EB84D33146417C35EE`  `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader`
- `C890991EF5DEF6DB2CD44ED4AC9DDCD9B50E47FF47032E75BB9E194477D0626F`  `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader.meta`
- `29E2400FDFA52679C5E8769B4536AE6A1FB89F7A4E4324ECA3B19338861AE736`  `Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader`
- `22FFEF48051DF4A8C51405F93F3F913D773534B12E6D0C20EF71144BA40C963A`  `Assets/_Project/Scripts/Editor/HectonShaderVariantCollectionCompiler1336.cs`
- `597BFEEA0CE9E0564A13F39E3CD6FEAA25F18C30C4B6CED8CECC25777675B2C3`  `Assets/_Project/Scripts/Editor/HectonMasterShaderAudit1615.cs`
- `8332A2D8389DC76EDA9B84CF2A7100B65F345509BC80E13A13C38FDBA7E4A89D`  `Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants`
- `C8766B3AAF208B533A96F809550FBBCCF41F0641E9DCFEBA4BEE27B73929DA20`  `Assets/_Project/Scripts/Editor/HectonMasterMaterialMigrator1615.cs`
