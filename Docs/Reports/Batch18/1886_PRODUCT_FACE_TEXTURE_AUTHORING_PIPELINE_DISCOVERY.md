# 1886 Product Face Texture Authoring Pipeline Discovery

Static report only. No Unity execution, import, bake, screenshot, profiler, source edit, asset edit, prefab edit, scene edit, binary edit, generated mesh edit, task-file edit, or `.meta` edit was performed.

## Scope

- Agent: 1886
- Task file: `taskslocal/batch18_night_orchestration/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.txt`
- Evidence class: static filesystem and text inspection only
- Owned output: this report and `1886_PRODUCT_FACE_TEXTURE_AUTHORING_IMPLEMENTATION_QUEUE.csv`

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `terrain.md`
- `water.md`
- `world.md`
- `lighting.md`
- `vfx.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

Requested mandate `.agents-skills/DATA_Binary_DataMonolith_Blob_Runtime_Bootstrap.txt` is absent in the current registry. Static search found data/bootstrap-adjacent mandates, but no substitute was treated as the requested authority.

`Docs/Actual Domains of Project.txt` does not exist. Narrow inferred domain: product-face material and texture source authoring pipeline for resources, tools, transport, player suit, with sky/ocean/terrain only as source-context risks.

## Batch Evidence Read

- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1876_TRANSPORT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1877_PLAYER_SUIT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`

Prior reports establish that product-face geometry source authoring exists for tools, resources, transport, and player suit, but texture/material source authoring remains mostly missing. Current resource materials are flat URP Lit color shells with empty texture slots. Transport/player prefabs still depend on primitive/default material debt in static evidence. Player visor is the only meaningful partial material source because `Mat_Visor_Glass.mat`, `SuitVisor.shader`, `visor runoff normal.png`, and `visor droplet mask.png` exist.

## Strongest Reusable Existing Pipeline

Strongest reusable route: `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269`.

Why it is strongest:

- It already separates mesh-derived control-map baking, texture ingestion, import settings, material binding, reporting, and black-box telemetry.
- It has explicit static paths for templates, inbox, imported textures, imported materials, ingestion profiles, and reports.
- It classifies albedo, normal, ARM/packed, curvature, depth, and color-ID from file names.
- It applies platform-aware import policy: albedo sRGB, normal map import for normals, mipmaps, non-readable imported textures, BC5 normals, BC7 packed/albedo, Android ASTC.
- It binds imported textures to an UberNoir material with `_BaseMap`, `_MainTex`, `_ArmMap`, `_MaskMap`, `_MetallicGlossMap`, `_BumpMap`, `_NormalMap`, and `_NORMALMAP`.
- It writes JSON reports under `Docs/Reports` and uses a 300-frame fixed-size editor black box dump route.

Restriction: reuse must be scoped before implementation. The existing prefab binder uses a general CSV route under `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` and can assign materials into prefabs under `Assets/_Project/Prefabs`. Future product-face work must add a product-face-specific manifest or validator gate before any prefab relink.

Second strongest route: `Assets/_Project/Scripts/Editor/ProceduralGen/ShallowsBioForgeBatchBaker.cs`.

Reusable properties:

- Complete static pattern for atlas authoring, import, material creation, shader binding, prefab generation, validation, and reports.
- Bakes albedo, normal, ORM, and MatCap atlases for procedural biological assets.
- Uses `Hecton8/Flora/ProceduralBio` with `_AlbedoAtlas`, `_NormalAtlas`, `_ORMAtlas`, `_MatCap`.
- `Hecton_ProceduralBio.shader` decodes ORM as R occlusion, G roughness, B metallic, A emission mask.

Restriction: this is an organic/flora pipeline. It is useful for membranes, kelp/coral-derived wear, biofilm, and organic masks. It must not become the default metal/tool/transport material route without shader-channel conversion.

## Existing Pipeline Inventory

| Pipeline | Existing output | Reuse decision | Reason |
|---|---|---|---|
| AI Texture Control Maps / Shinobu269 | `Docs/AI_Texturing_Templates`, `Docs/AI_Texturing_Inbox`, `Assets/_Project/Textures/AI_Texturing`, `Assets/_Project/Materials/AI_Texturing`, JSON reports | Extend | Best fit for product-face texture source templates, import policy, material binding, reports, and black-box pattern. |
| AI Texture Material Setup Scanner | `Docs/Reports/AI_TEXTURE_MATERIAL_SETUP_REPORT.json`, merged rendering report | Extend | Static validation already checks missing packed maps and albedo import policy. Needs product-face categories and channel contracts. |
| Shallows BioForge Batch Baker | World procedural flora atlases/materials/prefabs | Reference/extend for organic subsets | Good atlas/material/import pattern, but channel layout is ORM, not generic MRAO. |
| ProductFace mesh source authoring 1874-1877 | Generated mesh source paths for tools, resources, transport, player suit | Extend as bake inputs | Geometry source route exists. It lacks albedo/normal/MRAO source output. |
| GeologyForge / TopographyForge | Terrain heightmap `.h8bin`, terrain reports | Reference only | Useful for terrain material context and validation patterns. Mutates StreamingAssets binaries and is not product-face material authoring. |
| Hydraulic Erosion Forge / Shinobu242 | Erosion `.h8bin`, silt/macro erosion reports | Reference only | Useful for silt/wetness mask ideas. Binary terrain output makes it wrong for product-face texture pass. |
| Offline Geometry Baker / Shinobu213 | Optimized meshes, colliders, prefabs, LOD/physics reports | Reference only | Useful validation/reporting pattern, not texture/material source authoring. |
| Sky/ocean/lighting shaders and textures | Aegir, sky, moon, cloud, ocean normal/swell/foam assets | Do not repurpose | Route-specific environmental art. Use as visual floor reference, not product-face texture source. |

## Texture And Channel Contracts Found

Global product-face target from project bibles: albedo, normal, and packed material masks are required. Flat color shells are rejected.

Static shader evidence shows multiple packed-mask dialects. Future implementation must not treat every packed texture as one universal MRAO layout.

| Shader or route | Static channel evidence | Product-face impact |
|---|---|---|
| `Hecton_ToolDecayLit.shader` plus `Hecton_CoreLit.hlsl` | Packed Mask V1: R metallic, G occlusion, B smoothness, A emission mask | Good fit for tools if future source textures are generated to this exact layout. |
| `Hecton8/Rendering/UberNoir` / AI texture ingestion | Code and comments call the packed texture ARM. Static HLSL uses R occlusion, G roughness, B metallic, A emission/sss/bio/family depending state. | Strong pipeline, but not direct MRAO unless manifest declares channel conversion. |
| `Hecton_ProceduralBio.shader` | ORM atlas: R occlusion, G roughness, B metallic, A emission mask | Reusable for organic resource/biofilm variants only with explicit naming. |
| `Bakers/Hecton_MraoAtlasLit.shader` | MRAO atlas lit reference: metallic, roughness, AO, emission mask | Useful canonical reference, but future pass must confirm exact property names and intended material category. |
| `Hecton_Master_Lit.shader` | Multiple mask layouts selected by shader parameters. | Requires material-by-material channel audit before reuse. |
| `Hecton_KelpMaster.shader`, `Hecton_CoralMaster.shader` | Mask maps drive organic wetness/spec/gloss/emission-like features. | Source pool for biological surface language, not a generic packed-material contract. |
| `SuitVisor.shader` | Dedicated runoff normal and droplet mask textures exist. | Partial player visor source pipeline exists; body/glove/housing textures still missing. |

## Candidate Source Pools

### Resources

Existing static source pools:

- Terrain/basalt textures under `Assets/_Project/Art/TEXTURES/Terrain Textures`, including color and normal maps for basalt, rock, gravel, mud, moss, sand, and related terrain surfaces.
- Biological/flora atlases and imported families under `Assets/_Project/Art/TEXTURES/WorldProceduralFlora`.
- Procedural bio atlas route: `TX_ProceduralBio_Shallows_AlbedoAtlas.png`, `TX_ProceduralBio_Shallows_NormalAtlas.png`, `TX_ProceduralBio_Shallows_ORMAtlas.png`, `TX_ProceduralBio_Shallows_MatCap.png`.

Gaps:

- 1881 records resource pickup material roles as mostly `MISSING_SOURCE_REQUIRED`.
- Flat resource material shells under `Assets/_Project/Art/Materials/Resources/Mat_Resource_*.mat` are not acceptable source evidence.
- Mineral-specific albedo/normal/packed maps for titanium scrap, copper, silver, sulfur, silica, resin, seed, and sampled biomass are not present as a complete product-face set.

Decision: use terrain/flora assets as visual reference and donor texture language only after a manifest declares ownership, channels, and category. Do not directly relink resource pickups to terrain/flora route materials.

### Tools

Existing static source pools:

- `ProductFaceToolMeshSourceAuthoring.cs` defines future tool source mesh output under `Assets/_Project/Art/Generated/ProductFace/Tools`.
- `Hecton_ToolDecayLit.shader` has a useful packed mask contract for metallic, occlusion, smoothness, and emission.
- `Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat` exists, but its texture slots are empty in static evidence.
- `Assets/_Project/Art/Materials/Tools/Mat_Tool_*_Placeholder.mat` are placeholders.

Gaps:

- No complete tool albedo/normal/packed source set was found.
- Handle rubber, chipped metal, etched labels, LEDs, grime, and edge wear are not source-owned.

Decision: future tool pass should extend AITexture ingestion and ToolDecayLit channel layout. It must output project-owned texture sets, not only material color parameters.

### Transport

Existing static source pools:

- `ProductFaceTransportMeshSourceAuthoring.cs` defines CargoSled, ExosuitFrame, MicroSub, and ScoutGlider source mesh output.
- Prior reports identify semantic material candidates such as wet steel, shell, glass, rubber, and runtime proof materials.

Gaps:

- 1882 records unresolved/default material debt on current prefabs and no complete transport texture set.
- No owned hull albedo/normal/packed wetness/scratch/damage atlases were proven.
- Glass and canopy materials need category-specific transparency/scratch/runoff contracts before relink.

Decision: future transport pass should own a dedicated `ProductFace/Transport` texture/material family. Do not clone or mutate ocean/Crest materials to fake transport wetness.

### Player Suit

Existing static source pools:

- `ProductFacePlayerSuitMeshSourceAuthoring.cs` defines source parts for the player suit.
- `Mat_Visor_Glass.mat` binds `visor runoff normal.png` and `visor droplet mask.png`.
- `SuitVisor.shader` has a specific visor wetness/runoff route.

Gaps:

- Suit body, gloves, trims, housing, seals, LEDs, scratches, and fabric/rubber/graphite panels lack complete source albedo/normal/packed maps.
- Visor partial source does not solve full suit material identity.

Decision: preserve the visor-specific route and build a separate player suit material family around it. Do not flatten the suit into generic URP Lit color shells.

### Sky, Ocean, Coast, Terrain

Existing static source pools:

- Aegir, moon, cloud, ocean normal/swell/interference, foam, basalt, and surface/coast assets exist across sky/ocean/terrain routes.

Gaps:

- These are route-owned environmental assets, not product-face material sources.
- Ocean/Crest materials are shader-specific and cannot be repurposed for pickups, tools, vehicles, or suit parts.

Decision: use them only as visual-floor references and environmental context. Product-face authoring must not weaken sky/ocean/terrain ownership or mutate route materials.

## Required Future Gate Before Relink

No product-face relink should run until a static manifest proves each material role has:

- Project-owned albedo texture.
- Project-owned normal texture with correct normal import settings.
- Project-owned packed mask texture with declared shader-specific channel layout.
- Material path and shader path.
- Category: resource, tool, transport, player, visor, organic, glass, rubber, metal, fabric, mineral.
- Texture source path and generation/import route.
- Low, Middle, High, Ultra GlobalQualityWeight consequences.
- Validator that checks missing texture slots, import settings, channel contract, and forbidden fallback/default material usage.

1886 did not run that gate. This report only identifies the route.

## Low / Middle / High / Ultra Consequences

- Low: product-face materials must still have readable silhouette-safe albedo, correct normal maps, and packed masks with reduced resolution or cadence; no flat placeholder color shells.
- Middle: category-specific atlas resolution and MRAO/ORM/ARM masks are required for resources, tools, transport, and player suit; wetness and wear may be baked or cheap shader-driven.
- High: dedicated hero-material variants should add stronger normals, edge wear, emissive masks, wetness/runoff detail, and category-specific roughness response.
- Ultra: visual overkill should spend cycles on richer source maps, atlas density, wetness/scratch layering, and proof captures, without changing gameplay truth, DTO layout, or authority ownership.

## Static Verification Summary

- `git diff --check -- Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_IMPLEMENTATION_QUEUE.csv Docs/Tasks/Status_1886.md Docs/AgentLogs/Rationale_1886.md Docs/AgentLogs/LOG_1886.md`: exit 0, no output.
- `Import-Csv Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_IMPLEMENTATION_QUEUE.csv | Measure-Object`: Count 8.
- Static term cross-check across this report and queue CSV:
  - `MRAO=8`
  - `wetness=10`
  - `normal=27`
  - `albedo=19`
  - `resources=8`
  - `tools=11`
  - `transport=15`
  - `player=14`
  - `sky=8`
  - `ocean=10`
- `git status --short -- <five owned files>`: exactly five untracked owned files listed; no source, asset, prefab, scene, binary, generated mesh, task-file, or `.meta` file listed in the owned-path check.
