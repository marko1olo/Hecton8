# 1888 Product-Face Texture Channel Manifest And Shader Audit

Agent: 1888  
Mode: REPORT_ONLY_STATIC_SHADER_CHANNEL_AUDIT  
Evidence class: STATIC_SOURCE / STATIC_DOC  
Unity/build/import/bake/PlayMode/profiler/DataMonolith: NOT RUN

## Scope

This report creates the product-face texture channel manifest future material and texture agents must obey before authoring, importing, relinking, or promoting product-face materials.

Owned machine-readable manifest:

`Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv`

No source, Unity asset, prefab, scene, binary, generated mesh, `.meta`, DataMonolith, or task file was edited.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `shaders.md`
- `rendering.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- Required shader/source files listed in the task.

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: product-face shader/channel manifest and static material-source audit.

## Static Shader Channel Contracts

### ToolDecayLit / PackedMaskV1

Source:

- `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader`
- `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`

Contract:

- `_BaseMap`: albedo/base color, sRGB.
- `_BumpMap`: tangent normal, normal import.
- `_MaskMap`: `R = Metallic`, `G = AO/Occlusion`, `B = Smoothness`, `A = EmissionMask`.

This is not ORM, not MRAO, and not AI ARM. Future tool materials using `Hecton_ToolDecayLit` must author the packed map in this exact layout.

### ProceduralBio / ORM

Source:

- `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`

Contract:

- `_AlbedoAtlas`: organic albedo atlas.
- `_NormalAtlas`: triplanar normal atlas.
- `_ORMAtlas`: `R = Occlusion`, `G = Roughness`, `B = Metallic`, `A = EmissionMask`.

This route is valid only for organic/flora/bio material language. It must not be used as a metal/tool/transport mask without an explicit channel conversion manifest.

### MraoAtlasLit / MRAO

Source:

- `Assets/_Project/Art/Shaders/Bakers/Hecton_MraoAtlasLit.shader`

Contract:

- `_BaseMap`: albedo/base color, sRGB.
- `_NormalMap`: normal map.
- `_MraoMap`: `R = Metallic`, `G = Roughness`, `B = AO`, `A = EmissionMask`.

This is the clearest static MRAO reference. It differs from ToolDecayLit because its G channel is roughness and its B channel is AO.

### AI Texture / UberNoir ARM-style Ingestion

Source:

- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureImportAndMaterialPipeline.cs`
- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/Material_Setup_Scanner.cs`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`

Static evidence:

- File names containing `_arm`, `-arm`, `_mask`, or `_packed` are classified as `Arm`.
- Imported ARM textures are assigned to `_ArmMap`, `_MaskMap`, and `_MetallicGlossMap` if the material exposes those properties.
- Import policy uses albedo sRGB, normal map import for normals, mipmaps, non-readable textures, BC5 normals, BC7 packed/albedo on Standalone, ASTC 6x6 on Android.

Blocked point:

The code path classifies and binds ARM-style packed maps but does not define one universal channel contract in the inspected source. Prior 1886 static report records the AI/UberNoir route as ARM-like and not directly MRAO. Future agents must not assume MRAO or ToolDecayLit semantics for `_ArmMap` until the specific shader material contract is supplied.

### SuitVisor Dedicated Slots

Source:

- `Assets/_Project/Art/Shaders/SuitVisor.shader`

Contract:

- `_HUD_RenderTexture`: HUD render texture.
- `_ScratchNormalMap`: scratch normal.
- `_FingerprintTex`: R mask for fingerprint/smudge.
- `_VisorMaskTex`: `R = Dirt`, `G = Scratch`, `B = Salt`, `A = Condensation`.
- `_WaterRunoffNormalTex`: runoff normal.
- `_WaterDropletMaskTex`: droplet mask.

SuitVisor is not a generic PBR packed-map route. Its droplet/runoff/wear semantics are visor-specific and must not be collapsed into MRAO/ORM/ARM.

## Product-Face Manifest Decisions

The CSV contains 17 rows covering all required product-face families and route blockers.

Status meanings:

- `ACCEPTED_CHANNEL_CONTRACT_STATIC`: shader channel semantics are static-source clear, but visual acceptance is not claimed.
- `PARTIAL_SOURCE_STATIC`: credible static source exists, but one or more required slots or proof artifacts are missing.
- `BLOCKED_CHANNEL_CONTRACT_REQUIRED`: channel meaning or source ownership is not complete enough for future authoring/relink.
- `REJECTED_SOURCE_ROUTE`: forbidden as final product-face source.

## Blocked Channel Contracts

- Tool screens using `Hecton_ToolScreenDiegetic`: blocked. This task did not include that shader in the required source list, and no enough static shader/channel evidence was inspected for a final screen packed-map rule.
- Resource minerals: blocked until each mineral family declares shader route and packed-map layout. Do not reuse terrain/geology textures as final ore maps without seam/nodule-specific albedo, normal, packed mask, and import proof.
- Resource organics: blocked until each organic pickup declares whether it uses ProceduralBio ORM, a pickup-specific PBR shader, or another explicit organic material contract.
- Resource scrap: blocked until a scrap shader route and packed map are declared. It may use MRAO or ToolDecayLit only after the material owner chooses one and converts channels.
- Transport hull metal, glass, and rubber: blocked until dedicated transport material families declare shader and packed map. Runtime proof materials and shell candidates are semantic references only.
- Player suit body and player labels/trims: blocked until suit body/decal/trim atlases define albedo, normal, packed map, emission/wear masks, import policy, and shader route.
- AI/UberNoir ARM: blocked for product-face relink until the exact UberNoir shader channel contract is attached to the material family.
- Moons: blocked for explicit texture role paths and phase/albedo/normal contract despite material assets existing.
- Photic shallows: blocked in 1883 and not separately accepted here.
- Sargassum micro-fauna: blocked because current static evidence has built-in plane mesh and missing VAT position/normal textures.
- Default/package/material-placeholder route: rejected.

## Future-Agent Rules

1. Never infer channel meaning from the word `MRAO`, `ORM`, `ARM`, `Mask`, or `Packed`. Use the shader-specific contract in the manifest.
2. `Hecton_ToolDecayLit` requires `_MaskMap R Metallic / G AO / B Smoothness / A EmissionMask`.
3. `Hecton_ProceduralBio` requires `_ORMAtlas R Occlusion / G Roughness / B Metallic / A EmissionMask`.
4. `Hecton_MraoAtlasLit` requires `_MraoMap R Metallic / G Roughness / B AO / A EmissionMask`.
5. `SuitVisor` uses dedicated visor slots and `_VisorMaskTex R Dirt / G Scratch / B Salt / A Condensation`; do not treat it as a PBR packed mask.
6. AI/UberNoir imported `_ArmMap` is blocked for product-face relink until its exact channel contract is documented with the target shader/material.
7. Environment materials, Crest inputs, sky/ocean/Aegir/moon assets, terrain textures, RuntimeVisualProof materials, placeholder materials, package `Lit.mat`, Unity defaults, and debug/blockout materials are not product-face donor sources unless owner approval and source-role proof exist.
8. Empty texture slots cannot be promoted as material sources.
9. Visual acceptance is not claimed by this report. Static channel contracts only.

## Low / Middle / High / Ultra Consequences

The manifest does not change runtime behavior. It constrains future authoring:

- Low: compact lane still requires correct albedo, normal, and packed-map semantics. Reduced resolution is allowed; flat placeholder color is not.
- Middle: family-specific material identity, roughness/smoothness correctness, AO, wetness/emission masks, and texture import reports are required before relink.
- High: richer normals, wetness, scratches, decals, trim masks, and organic emission may be authored only inside the declared channel contract.
- Ultra: additional detail maps, scratch/runoff layers, decal density, and hero texture resolution may increase, but channel meaning, gameplay truth, material identity, save identity, and authority route cannot change.

## Verification Performed

Commands run:

```powershell
git diff --check -- Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv Docs/Tasks/Status_1888.md Docs/AgentLogs/Rationale_1888.md Docs/AgentLogs/LOG_1888.md
Import-Csv Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv | Measure-Object
Select-String -Path Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md,Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv -Pattern 'ToolDecayLit','ProceduralBio','SuitVisor','MRAO','ORM','ARM','wetness','emission','normal','albedo'
```

Results are recorded in `Docs/AgentLogs/LOG_1888.md`.

## Result

What was wrong: product-face routes had multiple incompatible packed-map dialects and prior reports showed missing/placeholder material source packages. Future agents could still guess channel meaning and relink wrong maps.

What I did: created a static shader/channel manifest and report that names concrete channel layouts, blocks uncertain contracts, and rejects default/package/placeholder/environment donor routes.

In-game result: PENDING VERIFICATION. Unity, screenshots, import, PlayMode, Frame Debugger, profiler, and DataMonolith were not run by task order.

What was verified: static docs, prior Batch18 reports, required shader/source files, CSV parse count, whitespace diff check, and required term presence only.
