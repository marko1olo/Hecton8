# ProductFace Source Manifest Plan - 2026-06-04

## Scope

Offline prep only. This report drafts ProductFace-owned texture/source package rows and material channel contracts for later Unity import and relink work. It does not create Unity assets, import images, edit prefabs, edit materials, edit scenes, run Unity, or run a build.

Requested production targets are still missing from active data:

- `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_source_manifest.csv`
- `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_manifest.csv`

This Batch20 package supplies draft rows under `Docs/Reports/Batch20` only.

## Authority Read

Root and route authorities read for this pass:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3dmodel.md`
- `presentation.md`
- `tools.md`
- `vehicles.md`
- `player.md`
- `construction.md`
- `inventory.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `authoring.md`
- `data.md`
- `world.md`
- `celestial.md`
- `atmosphere.md`

Relevant mandates loaded:

- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`

Prior evidence read:

- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md`
- `Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_RUNBOOK.md`
- `Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SCHEMA.md`
- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`

`Docs/Actual Domains of Project.txt` was not present. Narrow domain used: ProductFace texture/source manifests, material channel contracts, and later Unity relink checklist.

## Static Debt Evidence

Prior static reports identify 42 ProductFace primitive prefab errors and 55 of 61 actual material assignment rows blocked.

Blocked patterns confirmed from the prior reports:

- Tool held/world prefabs still route through `Mat_Tool_*_Placeholder`, with `Tool_Propulsion` held additionally using package `Lit.mat`.
- Resource pickups use flat `Mat_Resource_*` shells with empty texture slots.
- Transport prefabs `PFB_CargoSled_Transport`, `PFB_Exosuit_Frame_Transport`, `PFB_MicroSub_Transport`, and `PFB_ScoutGlider_Transport` use package/default Lit material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- Player body rows use `MAT_PlayerSwimBlockout` or package/default Lit. `Mat_Visor_Glass` is partial and not full proof.
- Sky/ocean rows contain route-owned material candidates and hidden Crest input materials; hidden input status requires later Frame Debugger proof.
- Construction/building rows include `Assets/_Project/Prefabs/Buildings/Cube.prefab` with third-party prototype checker material and `STRUCTURES.prefab` with package/default Lit.

This plan does not authorize deletion. It gives later owners a source manifest and relink contract.

## Shader Channel Contracts

### `Hecton_ToolDecayLit`

Source inspected: `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader`

Approved body route for tool held/world materials only.

- `_BaseMap`: sRGB albedo, no UI-only labels baked as gameplay truth.
- `_BumpMap`: imported as normal map, BC5 or platform normal compression.
- `_MaskMap`: linear `PackedMaskV1`.
- `PackedMaskV1.R`: Metallic.
- `PackedMaskV1.G`: Ambient Occlusion.
- `PackedMaskV1.B`: Smoothness.
- `PackedMaskV1.A`: Emission mask.

Forbidden for tool body relink:

- `ToolScreenDiegetic` as a body shader.
- VFX cone/beam/projection/line shaders as body shaders.
- `Mat_Tool_*_Placeholder`.
- Package/default `Lit.mat`.
- Direct prefab binding from AITexture output.

### `Hecton_MraoAtlasLit`

Source inspected: `Assets/_Project/Art/Shaders/Bakers/Hecton_MraoAtlasLit.shader`

Approved candidate for mineral, scrap, hull, trim, construction, debris, and ruin source rows when the route owner accepts the material family.

- `_BaseMap`: sRGB albedo.
- `_NormalMap`: imported as normal map.
- `_MraoMap`: linear MRAO.
- `MRAO.R`: Metallic.
- `MRAO.G`: Roughness.
- `MRAO.B`: Ambient Occlusion.
- `MRAO.A`: Emission mask.

Do not substitute this contract into shaders expecting ORM or `PackedMaskV1`.

### `Hecton_ProceduralBio`

Source inspected: `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`

Approved candidate for organic pickup rows only after the resource pickup route owner accepts the shader/material path.

- `_AlbedoAtlas`: sRGB albedo atlas.
- `_NormalAtlas`: normal atlas.
- `_ORMAtlas`: linear ORM.
- `ORM.R`: Occlusion.
- `ORM.G`: Roughness.
- `ORM.B`: Metallic.
- `ORM.A`: Emission mask.

Do not feed MRAO maps into this shader without repacking.

### `SuitVisor`

Source inspected: `Assets/_Project/Art/Shaders/SuitVisor.shader`

Dedicated visor route. Not a generic PBR material.

- `_HUD_RenderTexture`: dynamic HUD source owned by visor/HUD route.
- `_ScratchNormalMap`: visor scratch normal.
- `_FingerprintTex.R`: fingerprint mask.
- `_VisorMaskTex.R`: dirt.
- `_VisorMaskTex.G`: scratch.
- `_VisorMaskTex.B`: salt.
- `_VisorMaskTex.A`: condensation.
- `_WaterRunoffNormalTex`: water runoff normal.
- `_WaterDropletMaskTex`: droplet mask.

`Mat_Visor_Glass` is partial static evidence only. Primitive-sphere proof is not ProductFace acceptance.

### Route-Owned Sky/Ocean Contracts

Sky, Aegir, moons, ocean, foam, and photic shallows are not ProductFace donor materials. Their source rows are included because visible primitive/default debt overlaps ProductFace acceptance, but route owners must keep the contracts in their own material families.

- Sky/cloud panorama: sRGB cloud/color textures plus route-specific density or coverage masks if declared by the sky shader. No PBR mask substitution.
- Aegir gas giant: route-specific cloud/storm/color inputs. No PBR mask substitution.
- Moons: moon albedo sRGB, optional normal/height/roughness only if declared by the moon shader.
- Ocean surface: route-specific water normals, foam masks, color/absorption, caustic or clarity inputs. No generic ProductFace PBR material.
- Crest hidden inputs: allowed only if Frame Debugger proves they are hidden simulation inputs, not visible fallback art.
- Photic shallows: bright, readable, premium surface and shallow-water source package. Darkness is not an allowed cover for weak art.

## Draft Manifest Output

Rows are written in:

- `Docs/Reports/Batch20/product_face_source_manifest_draft_20260604.csv`

The CSV gives exact draft roles for:

- 12 held/world tools.
- 8 resource pickups plus the legacy `Item_Titanium` classification row.
- 4 transport vehicles split into hull/body, rubber/grip/seal, glass/lens, and labels/trim roles.
- Player suit, visor, gloves, fins, helmet, and trim roles.
- Sky/ocean visible primitive rows.
- Construction, debris, and ruins rows.

The row-level `prefab_binding_allowed` field is `false` for every row. Later AITexture/source outputs must land in source/package folders first. Unity owners may import and relink only after manifest validation.

## Source Package Rules

Allowed ProductFace source package shape:

- Layered source file where applicable: `.psd`, `.kra`, `.blend`, `.spp`, `.sbsar`, `.exr`, or documented generator source.
- Exported texture maps named by row role and shader slot.
- A plain metadata sidecar that records prompt/source intent, authoring owner, shader route, packing contract, source license status, and rejection gates.
- No direct prefab mutation from generated textures.
- No import into active project texture/material folders until the Unity owner accepts the row.

Allowed later output folders after Unity owner approval:

- `Assets/_Project/Textures/ProductFace/Tools`
- `Assets/_Project/Textures/ProductFace/Resources`
- `Assets/_Project/Textures/ProductFace/Transport`
- `Assets/_Project/Textures/ProductFace/Player`
- `Assets/_Project/Textures/ProductFace/Environment`
- `Assets/_Project/Textures/ProductFace/Construction`
- `Assets/_Project/Materials/ProductFace/...` matching the same route group

This pass writes none of those assets.

## Low / Middle / High / Ultra Consequences

GlobalQualityWeight remains continuous. Quality tiers below describe consequences for authored texture resolution, decal density, secondary detail, and validation strictness. They do not change gameplay truth, item IDs, channel semantics, prefab authority, save identity, or DTO layout.

- Low: preserve the visible silhouette and material identity with compact 512 to 1024 maps, major labels, broad wear, broad normals, and no extra decal variants.
- Middle: use 1024 to 2048 maps, clearer seams, secondary wear, better baked AO, and readable route labels where visible.
- High: use 2048-class maps, stronger normal definition, additional decal/wear variants, cleaner mask gradients, and sharper close-camera readability.
- Ultra: keep high-resolution sources and optional 4096 exports where route budgets allow, add micro-scratch/detail normal layers and extra decal atlases, but keep the same material slots and authority route.

The minimum tier still must pass the visual floor. Low is not permission for flat, muddy, default, primitive, or placeholder-looking art.

## Unity Owner Boundary

Later Unity owner work must be a single scoped relink pass:

1. Import accepted source package outputs.
2. Create or update ProductFace materials using the declared shader contract.
3. Dry-run prefab slot relinks against the rows in the manifest.
4. Run static material assignment and primitive validators.
5. Capture proof views for held/world, pickup, vehicle, player, sky/ocean, and construction/debris/ruins surfaces.

This Batch20 pass provides no Unity proof and no build proof.

