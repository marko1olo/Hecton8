# 1894 ProductFace Texture Source Manifest Schema

Agent: 1894  
Mode: REPORT_ONLY_STATIC_MANIFEST_SCHEMA  
Evidence class: STATIC_DOC / STATIC_SOURCE only  
Unity/build/import/bake/PlayMode/profiler/DataMonolith: NOT RUN

## Scope

This packet defines a ProductFace-specific texture/material source manifest schema and a seed family matrix for future implementation. It does not create implementation files, Unity assets, real manifests under `Assets`, prefabs, scenes, binaries, `.meta` files, generated meshes, or DataMonolith payloads.

Owned files:

- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SCHEMA.md`
- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv`
- `Docs/Tasks/Status_1894.md`
- `Docs/AgentLogs/Rationale_1894.md`
- `Docs/AgentLogs/LOG_1894.md`

`ocean.md` was requested by the task but is absent at project root. `water.md` was read as the closest route bible for ocean/surface water rules. This is static document evidence only.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `tools.md`
- `inventory.md`
- `player.md`
- `world.md`
- `water.md` fallback for missing `ocean.md`
- `celestial.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md`
- `Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md`

## Problem

ProductFace texture authoring has three current failure risks:

1. Generic AI texture import can classify `_arm`, `_mask`, or `_packed` names without a shader-specific channel contract.
2. Generic prefab binding through `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` can mutate prefab material slots before ProductFace source, channel, and relink ownership are proven.
3. Environment donors such as sky, Aegir, moons, Crest ocean, terrain, flora, sargassum, storm/noir/depth, and visor glass can be misused as quick ProductFace material sources.

This schema blocks those paths. ProductFace import is source/material-only. Prefab mutation is a separate owner phase with a dry-run proof gate.

## Future Manifest File Format

Future implementation should use a ProductFace-specific source manifest. Recommended future path, not created by this task:

`Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_source_manifest.csv`

This report-only task seeds the same contract in:

`Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv`

Required seed/schema columns:

| Column | Rule |
|---|---|
| `family_id` | Stable family or route id. No filename guessing as authority. |
| `asset_category` | Narrow category: tool, resource_mineral, resource_organic, transport_hull, transport_glass, player_suit, visor, route_owned_environment_not_product_face_donor, etc. |
| `target_material_family` | Human-readable ProductFace material role. Must name casing, rubber, glass, metal, labels, wetness, ore seam, tissue, hull, or route-owned environment role. |
| `allowed_shader_route` | Exact shader route or blocked state. Shader names are not interchangeable. |
| `required_albedo` | Project-owned albedo/source requirement. Albedo must be sRGB and contain base color only, no baked lighting. |
| `required_normal` | Project-owned normal requirement. Normal import must use normal-map type and BC5 where supported. |
| `required_packed_map` | Project-owned packed material map requirement. Empty texture slots are fatal. |
| `packed_channel_contract` | Exact channel contract. `MRAO`, `ORM`, `ARM`, `Mask`, and `Packed` names are not enough. |
| `required_optional_masks` | Detail, emission, wetness, scratch, droplet, runoff, translucency, decal, label, or control masks required by the material family. |
| `forbidden_donor_sources` | ProductFace exclusions: environment assets, placeholders, defaults, package materials, broad AI binding, route-owned donors. |
| `allowed_output_folder` | Future allowed ProductFace-owned output folder. Route-owned environment rows must not output into ProductFace folders. |
| `prefab_binding_allowed` | Must be `false` in this seed. Import-phase prefab mutation is forbidden. |
| `proof_required` | Minimum future proof before promotion. Static reports do not prove runtime/import/visual acceptance. |
| `status` | Static status: missing source, partial source, blocked channel contract, route-owned not donor, etc. |

## Import Phase Rule

Exact rule that prevents import-phase prefab mutation:

`ProductFace texture import may only copy/import source textures, set TextureImporter policy, create or update ProductFace material assets, and write validation reports. It must reject any ProductFace use of AITextureControlMapBaker generic prefab binding, including Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv. Prefab material-slot writes require a separate ProductFaceRelinkOwner dry-run report, explicit allowed_prefab_path, allowed_renderer_path, allowed_material_slot, status=RELINK_READY, and user-scoped implementation task.`

The import phase must not call or extend a path equivalent to `AITextureMaterialBinder.AssignMaterialFromManifest` / `ApplyMaterialToPrefab` for ProductFace categories.

## Channel Contracts

These contracts are static source evidence from prior Batch18 reports. They do not prove import or render behavior.

| Route | Exact contract | ProductFace decision |
|---|---|---|
| `Hecton_ToolDecayLit` / `Hecton_CoreLit.hlsl` | `_MaskMap`: R = Metallic, G = AO/Occlusion, B = Smoothness, A = EmissionMask. Name: `PackedMaskV1`. | Allowed for tool body materials and only when the packed texture is authored to this layout. |
| `Hecton_ProceduralBio` | `_ORMAtlas`: R = Occlusion, G = Roughness, B = Metallic, A = EmissionMask. | Allowed only for organic/flora/biological ProductFace derivatives with explicit pickup-source manifest. |
| `Hecton_MraoAtlasLit` | `_MraoMap`: R = Metallic, G = Roughness, B = AO, A = EmissionMask. | Canonical MRAO reference for mineral, hull, suit body, trim, rubber, and manufactured materials when the owner chooses this route. |
| `SuitVisor` | `_ScratchNormalMap`, `_FingerprintTex` R mask, `_VisorMaskTex` R = Dirt, G = Scratch, B = Salt, A = Condensation, `_WaterRunoffNormalTex`, `_WaterDropletMaskTex`, `_HUD_RenderTexture`. | Dedicated visor route only. It is not MRAO/ORM/ARM. |
| AI/UberNoir ARM via `AITextureControlMapBaker` | ARM-like import/binding is detected, but exact target shader channel truth is not complete enough for ProductFace relink. | Blocked until a target shader/material contract states every channel. Filename tokens are hints only. |

Blocked channel contracts:

- `Hecton_ToolScreenDiegetic` screens: blocked until screen packed-map and emissive/readout contract is inspected.
- Transport glass: blocked until a transport glass shader contract defines scratch, droplet, runoff, grime, alpha/clip, and compact fallback semantics.
- Resource minerals/organics/scrap: blocked unless each row chooses MRAO, ProceduralBio ORM, PackedMaskV1, or a new explicit shader contract.
- AI/UberNoir ARM: blocked for ProductFace body relink until exact target shader contract is attached.
- Moons/photic shallows/micro-fauna: route-owned source gaps remain from 1883; not ProductFace body donors.

## ProductFace Folder Layout

Future implementation may use these folders after a scoped implementation task. This task did not create them.

```text
Docs/AI_Texturing_ProductFace/Templates/
Docs/AI_Texturing_ProductFace/Inbox/
Docs/AI_Texturing_ProductFace/Rejected/
Docs/Reports/ProductFace/
Docs/Reports/ProductFace/RejectedCandidates/

Assets/_Project/Textures/ProductFace/Resources/
Assets/_Project/Textures/ProductFace/Tools/
Assets/_Project/Textures/ProductFace/Transport/
Assets/_Project/Textures/ProductFace/PlayerSuit/
Assets/_Project/Textures/ProductFace/Shared/

Assets/_Project/Materials/ProductFace/Resources/
Assets/_Project/Materials/ProductFace/Tools/
Assets/_Project/Materials/ProductFace/Transport/
Assets/_Project/Materials/ProductFace/PlayerSuit/
Assets/_Project/Materials/ProductFace/Shared/

Assets/_Project/Data/ProductFace/TextureIngestion/
```

Rejected candidates must stay in docs/report quarantine or a future explicitly editor-only quarantine. They must not become production `MAT_` or `TX_` references.

## Import Settings Requirements

Future ProductFace import validation must require:

- Albedo/base maps: `sRGB=true`, no baked directional lighting, no fake AO shadows, mips enabled, compressed high quality.
- Normal maps: `TextureImporterType.NormalMap`, `sRGB=false`, non-readable, mips enabled, BC5 on Standalone where supported, ASTC normal-compatible target on Android/XR where the platform route supports it.
- Packed masks: `sRGB=false`, non-readable, mips enabled, high-quality compression. BC7 on Standalone where supported; ASTC 6x6 or route-approved ASTC setting on Android/XR where prior pipeline evidence supports it.
- Emission/detail/decal/control masks: explicit color space and compression by role. Do not infer from filename alone.
- World/ProductFace textures: mipmaps required. No runtime `Texture2D` creation, pixel filling, or runtime compression for production gameplay.
- Texture readability: imported production textures should be non-readable unless a documented editor-only validation step needs readback before final import.

No claim is made that these settings have been applied. This is a schema requirement only.

## Donor And Source Rejection

Hard reject as ProductFace source or proof:

- `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` for ProductFace.
- Unity default material, package-cache `Lit.mat`, built-in material routes.
- `Mat_Tool_*_Placeholder`, `Mat_ToolTrial_*`, `MAT_PlayerSwimBlockout`, RuntimeFlatColor, RuntimeCheckerboardUnlit, diagnostics/error materials, and flat color shells.
- Empty texture slots promoted as material evidence.
- Crest `Ocean.mat`, Crest foam/wave/caustic/OceanInputs assets, or cloned/mutated third-party Crest materials as ProductFace wetness/body materials.
- Sky/cloud/Aegir/moon/ocean/waterline/foam/terrain/flora/sargassum/depth/noir/storm/weather LUT assets as direct ProductFace donors.
- Visor droplet/runoff textures outside the player visor route unless a new derivative manifest and owner approval exists.
- AI/procedural outputs with baked lighting, perspective, text, watermark, blurred mud, random noise, false PBR channels, crayon patterns, or flat color.
- Fog/noir/depth/storm/darkness as proof that weak surface, ocean, Aegir, moon, tool, resource, transport, or player-suit textures are acceptable.

The visual floor remains Subnautica-level or better for surface, photic shallows, and medium-depth hero routes. ProductFace materials at close range must not rely on darkness or post effects to hide missing texture identity.

## Continuous Quality Scaling

`GlobalQualityWeight` may scale resolution, detail density, optional masks, validation sampling, decal density, and report depth. It must not alter channel truth, material role semantics, gameplay truth, item identity, save identity, DTO layout, collider/anchor truth, prefab authority, or route ownership.

- Low / compact: 512-1024 where appropriate, shared detail maps, baked AO, correct albedo/normal/packed mask truth, strong silhouette/material separation. No flat fallback.
- Middle: full category-specific albedo, normal, packed maps, stronger roughness/AO/wetness/label detail, better material separation.
- High: richer normals, wetness, scratches, decals, trim masks, glass/runoff layers, longer near-field material residency where future proof allows.
- Ultra: hero-only 2048/4096 sources, additional detail maps, denser decal layers, stronger forensic validation and preview capture. No new truth.

## Seed Matrix Coverage

The seed CSV contains:

- 12 tool families from 1880.
- 9 resource pickup rows from 1881, including `TitaniumScrap` and `Item_Titanium` uncertainty.
- 12 transport material-role rows from 1882.
- 5 player suit/visor/label role rows from 1882.
- 21 sky/Aegir/moon/ocean/Crest/photic-shallow/environment route rows from 1883, marked route-owned and not ProductFace body donors.

All rows set `prefab_binding_allowed=false`.

## Proof Boundary

This packet is static schema work. It proves only text/files inspected and CSV parseability once verification runs. It does not prove:

- Unity import.
- texture importer state.
- material assignment.
- prefab relink.
- scene binding.
- screenshot quality.
- Frame Debugger / RenderGraph / profiler / GC / VRAM state.
- gameplay or in-game visual result.

Future visual acceptance remains `PENDING UNITY/PROFILER VERIFICATION` until imported assets, Unity screenshots, material-channel previews, and runtime/profiler artifacts exist.

## Verification

Required verification commands are recorded in `Docs/AgentLogs/LOG_1894.md` after execution:

```powershell
git diff --check -- Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SCHEMA.md Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv Docs/Tasks/Status_1894.md Docs/AgentLogs/Rationale_1894.md Docs/AgentLogs/LOG_1894.md
Import-Csv Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv | Measure-Object
Select-String -Path Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SCHEMA.md,Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv,Docs/Tasks/Status_1894.md,Docs/AgentLogs/Rationale_1894.md,Docs/AgentLogs/LOG_1894.md -Pattern ToolDecayLit,ProceduralBio,MraoAtlasLit,SuitVisor,AITextureControlMapBaker,ai_texture_prefab_bindings,ProductFace,Subnautica
```

## Result

What was wrong: ProductFace texture ingestion could still be interpreted through generic AI filename guesses, broad prefab binding, wrong packed-map channels, or environment donor misuse.

What I did: defined a ProductFace-only source manifest schema, import-phase no-prefab-mutation rule, folder layout, channel contracts, import settings requirements, donor rejection rules, continuous quality consequences, and a seeded family matrix.

In-game result: PENDING VERIFICATION. Unity/runtime/import/profiler work was forbidden by task.

What was verified: static authority reads and generated report/CSV/log/status/rationale files only, plus required CLI checks after file creation.
