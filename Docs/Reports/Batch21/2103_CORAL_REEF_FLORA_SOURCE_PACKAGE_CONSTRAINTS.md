# 2103 Coral Reef Flora Source Package Constraints

ID: 2103  
Role: CORAL_REEF_FLORA_FINAL_READY_SOURCE_PACKAGE_CONSTRAINTS  
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW  
Status: STATIC VERIFIED for constraints only. Unity/source generation/import/placement/profiler proof is PENDING VERIFICATION.

## Scope

Owned domain: final-ready source package constraints and QA gates for photic coral, reef flora, kelp, soft fans, shoreline/intertidal flora, and anchor debris encrustation. This task does not edit placement rules, generate source images, import assets, run Unity, bind materials, edit prefabs, or claim final visual proof.

First-20-minutes route contribution: removes a photic shallows visual blocker by defining the source/topology/material gates required before kelp/coral/reef flora can replace proxy or fallback finals. It does not close the route visually.

Out of scope: seabed substrate, geology rocks, terrain layers, water shader changes, placement rule diffs, runtime scripts, prefabs, materials, import settings, and sibling agent outputs.

## Authority Read

STATIC VERIFIED:

| Source | Use in this package |
| --- | --- |
| `AGENTS.md` | No fake proof, no forbidden edits, photic/surface visual floor, continuous `GlobalQualityWeight`, active ID logging. |
| `TASTE.md` | Reject primitive shapes, noisy materials, texture-hidden geometry, and shallow darkness masking. |
| `VISION_LOCKS.md` | Bright semi-open photic route; coral/unusual biota are allowed; `GlobalQualityWeight=0.0` is not ugly mode. |
| `PROJECT_BIBLES.md` | Route bible selection for generated flora/coral, textures, world, terrain, water, rendering, shaders, quality. |
| `quality.md` | Static docs cannot claim runtime proof; use `STATIC VERIFIED` and `PENDING VERIFICATION` labels. |
| `PROCEDURAL_ASSET_PIPELINE.md` | Generated objects require source manifest, topology, UVs, maps, LOD, collision/proxy policy, and proof artifacts. |
| `3dmodel.md` | Generated assets must survive texture-off review and include vertex layout, LODs, material identity, proxy proof. |
| `3DMODEL_FLORA_CORAL.md` | Flora/coral topology, vertex color contract, alpha restrictions, collision policy, proof requirements. |
| `3DMODEL_TEXTURES_MATERIALS.md` | Albedo/detail/normal/MRAO roles, import discipline, no duplicated runtime materials. |
| `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` | Texture source QA, PBR derivation, no baked light/text/neon/noisy material. |
| `3DMODEL_HERO_REALISM_OVERKILL.md` | Hero reef/coral setpieces require reference discipline, nonprimitive macro/meso/micro detail. |
| `world.md` | Surface/shore/photic zones must be bright, readable, materially rich, and route-legible. |
| `terrain.md` | Flora follows current, light, substrate, depth, shelter; coral attaches to biological substrate. |
| `water.md` | Shallow water readability floor; wetness/flow/caustic fakes must support beauty and route truth. |
| `rendering.md` | SRP Batcher/GPU instancing/HLOD proof required later; static source docs are not render proof. |
| `shaders.md` | MRAO, vertex-color deformation, wetness, and fallback paths require explicit shader contracts. |
| `.agents-skills/REND_Instanced_Flora_Physics.txt` | Later flora density must use instancing/culling discipline and avoid alpha walls. |
| `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` | Flora motion and reef ambience default to authored shader/VAT/fake paths unless gameplay truth needs physics. |
| `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` | 16.67 ms / compact VRAM constraints; no source package can imply free overkill. |
| `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt` | Dense coral/flora requires dithered fade/HLOD/GPU Resident Drawer proof later. |
| `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt` | Noir/darkness cannot hide weak surface/photic art; no pure black; biolum must be semantic. |
| `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt` | Texture upload/import proof remains future work; source package does not set project quality. |
| `.agents-skills/QA_Evidence_Text_Filter_Audit.txt` | Static search/report evidence must not be upgraded to Unity, profiler, or player-build proof. |

Optional 2004 files were present and read:

- `Docs/Reports/Batch20/2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv`
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv`
- `Docs/Reports/Batch20/2004_GENERATION_HANDOFF_CHECKLIST.md`
- `Docs/Reports/Batch20/2004_PROMPT_PACKS.md`

## Evidence Table

STATIC VERIFIED:

| Evidence | Relevant rows / statements | Constraint derived |
| --- | --- | --- |
| `2019_PROXY_DEBT_QUEUE.csv` | `2019-Q006` dry_land_kelp | Kelp finals need submerged seafloor or explicit shoreline split; dry-land kelp is rejected. |
| `2019_PROXY_DEBT_QUEUE.csv` | `2019-Q007` dry_land_coral | Coral finals need submerged reef/substrate rules; dry-land coral is rejected. |
| `2019_PROXY_DEBT_QUEUE.csv` | `2019-Q009` kelp_proxy_finals | Kelp tall/patch/canopy need flat silhouette, PBR closeup, vertex color sheet, LOD overlay, validator output. |
| `2019_PROXY_DEBT_QUEUE.csv` | `2019-Q010` coral_proxy_finals | Branching/massive/plate/low/soft fan coral finals need topology, PBR, channel debug, route capture. |
| `2019_PROXY_DEBT_QUEUE.csv` | `2019-Q014` bioforge_default_material_fallback | Default/generated starter material fallback is not final production proof. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G004` kelp tall hero | Source planned, not imported; reject primitive stalks, ribbon-only blades, dry land, starter maps as final. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G005` kelp patch/canopy | Source planned, not imported; reject alpha wall, route cue occlusion, proxy crown/patch, dry-land kelp. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G006` intertidal shoreline flora | Source missing; must be separate coastal/intertidal family, not underwater kelp on land. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G007` branching coral | Source planned, not imported; require welded branch finals and seafloor/substrate placement only. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G008` massive/plate/low coral | Source planned, not imported; reject smooth blobs, paper-thin plates, flat decals, noisy carpets. |
| `2019_GENERATION_ROUTE_MATRIX.csv` | `2019-G009` reef fan soft motion | Source missing/new family; only accepted with explicit photic/mid placement and shader support. |
| `2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` | Rank 4 | `TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604` is spend-first source candidate, not final proof. |
| `2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` | Rank 8 | `TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604` is a later source candidate after top blockers. |
| `2004_FLORA_CORAL_VARIANT_MATRIX.csv` | 10 variants | Defines family IDs, depth bands, topology, source slots, vertex color contracts, proof, and reject gates. |
| `2004_TEXTURE_CHANNEL_CONTRACTS.csv` | Final flora stack rows | `_BaseMap`, `_DetailMap`, `_NormalMap`, `_MaskMap`, vertex colors, source slots, and BioForge ORMA exception. |
| `2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md` | Tool boundary and blockers | BioForge can be source shell only; starter atlases/vertex colors are incomplete for final contract. |

No flora/coral evidence blocker: not triggered. Evidence exists and is sufficient for a static constraints package.

## Family Matrix

STATIC VERIFIED:

| Accepted family | Evidence basis | Biome/depth route | Final source slots | Placement owner requirement |
| --- | --- | --- | --- | --- |
| Tall kelp hero | `2019-G004`, `2004.kelp.tall.hero` | 2-28 m photic shallows; submerged rock/sand edge with current flow | `SRC_2004_KELP_BLADE_FIBER_4K`, `SRC_2004_KELP_HOLDFAST_ROOT_4K`, `SRC_2004_BIOLUM_DETAIL_MASKS_4K` | Submerged seafloor only. No dry-land eligibility. |
| Kelp patch/canopy | `2019-G005`, `2004.kelp.patch.filler`, `2004.kelp.canopy.silhouette` | 1.5-35 m photic route fill, shelf edge, waterline silhouette | `SRC_2004_KELP_BLADE_FIBER_4K`, `SRC_2004_KELP_CANOPY_EDGE_4K`, `SRC_2004_KELP_HOLDFAST_ROOT_4K`, biolum masks | Rooted clusters/canopy with route sightline gaps. No alpha wall. |
| Intertidal shoreline flora | `2019-G006`, `2004.intertidal.shoreline.flora` | Tidal band above and below waterline; wet rock, tide pool, shoreline seam | `SRC_2004_INTERTIDAL_WEED_LICHEN_4K`, optional debris/encrust source | Separate coastal/intertidal rule. Must not reuse kelp/coral dry-land placement. |
| Branching coral | `2019-G007`, `2004.coral.branching`, 2022 rank 4 | 2-80 m submerged reef rock/fossil carbonate | `SRC_2004_CORAL_BRANCH_CALCITE_4K`, `SRC_2004_BIOLUM_DETAIL_MASKS_4K`, 2022 coral candidate | Seafloor/substrate placement only; reef shelf proof required later. |
| Massive coral | `2019-G008`, `2004.coral.massive` | 6-120 m submerged fossil reef/rock shelf | `SRC_2004_CORAL_MASSIVE_POROUS_4K`, biolum masks | Submerged shelter pocket/reef bulk only. No generic rock substitution. |
| Plate coral | `2019-G008`, `2004.coral.plate` | 18-160 m submerged reef wall/shelf/ledge | `SRC_2004_CORAL_PLATE_RIM_UNDERSIDE_4K`, biolum masks | Reef wall/shelf placement. Must preserve route visibility. |
| Low sponge/floor coral | `2019-G008`, `2004.coral.low.sponge.floor` | 1-70 m submerged seafloor rock/sand/fossil rubble | `SRC_2004_CORAL_LOW_SPONGE_BED_4K`, biolum masks | Seafloor breakup only. No flat decal/noisy carpet. |
| Soft reef fan | `2019-G009`, `2004.reef.fan.soft.motion` | 8-90 m photic/mid current edge if soft family is added | `SRC_2004_REEF_FAN_SOFT_RIB_4K`, biolum masks | Conditional family: explicit shader support and current-edge placement required. |
| Anchor debris encrustation | `2019-G010`, `2004.anchor.debris.shoreline.blend` | Tidal band to 20 m; wet shoreline/tide-pool/debris field | `SRC_2004_ANCHOR_DEBRIS_ENCRUSTED_4K`, intertidal source | Structural/debris owner handles collision. Flora/coral only defines encrustation/source constraints. |

## Placement Constraints

STATIC VERIFIED future-owner requirements:

| Family | Required placement constraint | Rejected placement |
| --- | --- | --- |
| Tall kelp hero | Submerged seafloor, rock/sand edge, current flow, root/holdfast contact. | Dry land, floating water column, cliff face without root contact, decorative land plant use. |
| Kelp patch/canopy | Submerged pockets, shelf edge, route-safe negative space, fauna sightline gaps. | Alpha wall, route cue occlusion, dry terrain, proxy canopy as final. |
| Intertidal shoreline flora | Tidal band, wet/dry boundary, tide-pool rock, salt/lichen/wrack logic. | Underwater kelp silhouette on dry land, ordinary lawn grass, jungle foliage. |
| Branching coral | Submerged reef substrate/fossil carbonate, shelf/rock attachment, branch AO visible. | Dry land, smooth tube bouquet, unwelded intersections, floating branch proxy. |
| Massive coral | Submerged fossil reef/rock shelf, shelter cavity context, sediment abrasion. | Smooth blob, generic boulder, texture-only pore illusion. |
| Plate coral | Submerged reef wall/shelf/ledge, side-light route read, underside AO. | Paper planes, wall of plates hiding route cues, dry shelf coral. |
| Low sponge/floor coral | Submerged floor/rubble/rock edge, route-edge breakup, readable low profile. | Flat decal carpet, noisy color scatter, primitive mounds. |
| Soft reef fan | Current-edge reef accent, explicit shader support, geometry-backed fan ribs. | Alpha-only fan card, random glow, wrong abyssal placement for photic route. |
| Anchor debris encrustation | Shoreline/tide-pool/debris field, metal/rope/algae/coral masks separated. | Clean metal, primitive debris, kelp substitution, no scale cue. |

Blocker: this task edits no placement rules. Dry-land zero proof and submerged acceptance proof remain PENDING VERIFICATION.

## Rejection Matrix

STATIC VERIFIED:

| Rejection | Applies to |
| --- | --- |
| Land kelp or dry-land coral as production art | All kelp/coral families. |
| Primitive stalk, cylinder, cone, constant-radius tube | Tall kelp, branching coral, soft fan supports. |
| Ribbon-only blade or flat alpha-card field | Kelp tall, patch, canopy. |
| Smooth tube bouquet | Branching coral. |
| Smooth blob or generic rock | Massive coral. |
| Paper-thin plate or flat shelf card | Plate coral. |
| Flat decal/noisy carpet | Low sponge/floor coral. |
| Alpha-only fan | Soft reef fan. |
| Starter/generated BioForge material as final | All flora/coral finals. |
| Proxy fallback final | All production-visible family profiles. |
| Baked light, cast shadows, text/logo/UI/watermark, perspective object render, blur, muddy/noir grade, random neon | All source images. |
| Binary quality switch or low-tier ugly mode | All families and proof gates. |

## Topology Matrix

STATIC VERIFIED:

| Family | Required topology |
| --- | --- |
| Tall kelp hero | Visible holdfast/root cluster, tapering ribbed stipe, thick blade shells, serrated/torn rims, scars, non-card blade thickness, root-pinned vertex sway. |
| Kelp patch/canopy | Clustered holdfasts, mixed frond heights, canopy crown negative space, torn waterline silhouette, fauna/route gaps. |
| Intertidal shoreline flora | Wrack strands, salt grass tufts, lichen/moss strips, wet algae adhered to rock, grounded roots, tide boundary forms. |
| Branching coral | Welded/blended branch intersections, irregular branch taper, knuckles/collars, broken tips, cavities, AO under branches. |
| Massive coral | Lobed dome, porous/cratered caps, calcium bands, sediment abrasion, chipped shelter cavities, non-blob silhouette. |
| Plate coral | Layered plates, thick rims, underside geometry/AO, chipped ledges, support stems or terraces, non-paper thickness. |
| Low sponge/floor coral | Low mounds, porous mats, sponge openings, ridges, floor breakup forms, route-edge readable silhouette. |
| Soft reef fan | Ribbed fan panels, branched support skeleton, holes/tears, frayed edges, sway mask geometry, no transparent flat card. |
| Anchor debris encrustation | Barnacled anchor/debris, rope/chain, wet algae/coral encrustation, metal/organic region separation, scale-cue forms. |

## Vertex Color Contract

STATIC VERIFIED. Any final family lacking channel meaning is BLOCKED.

| Channel | Global flora/coral meaning | Family-specific notes |
| --- | --- | --- |
| R | Sway amplitude or motion leverage. Anchor/root/rigid mineralized sections near 0. Flexible frond/fan tips high. | Coral massive/plate/low generally 0. Branching coral low/0. Kelp/fronds/fans use gradient from anchor to tip. |
| G | Bioluminescence mask or phase. Non-emissive tissue 0. | Only localized ecology/navigation signals: pores, cavities, tips, cuts, edges. Random full-surface glow rejected. |
| B | Baked ambient occlusion/cavity/contact darkness. | Holdfast roots, branch joins, underside plates, pores, sponge openings, debris encrustation. |
| A | Family wear, thickness, damage/harvest/wetness, or variant mask. | Must be documented per variant. No hidden gameplay truth stored only in visual vertex color. |

BioForge blocker: current starter output writes height gradient R and incomplete G/B/A semantics. BioForge output is candidate shell only until remapped and validated.

## Texture Source Stack

STATIC VERIFIED:

| Map/slot | Source role | Required contract |
| --- | --- | --- |
| `_BaseMap` RGB | Albedo pigment | sRGB. No baked lighting, labels, text, cast shadows, or scene perspective. |
| `_BaseMap` A | Optional cutout | Only if shader route explicitly supports it. Not a license for alpha walls. |
| `_DetailMap` RGB/A | Micro fibers, pores, calcium grain, scars, sediment, detail influence | Linear. Low-frequency shape stays in mesh, not in detail texture. |
| `_NormalMap` RG/XYZ | Tangent-space ribs, pores, folds, serration, chipped rims | Linear normal map, BC5 target later, no lighting baked into source. |
| `_MaskMap` R | Metallic | 0 for kelp/coral; anchor/debris may use metal only on structural portions. |
| `_MaskMap` G | Roughness unless shader manifest states smoothness | Channel order must not be guessed. Document inversion if any. |
| `_MaskMap` B | AO/cavity | Branch intersections, undersides, holdfast roots, pores, plate undersides. |
| `_MaskMap` A | Emission/wetness/family mask | Localized only. Random full-surface glow rejected. |
| Optional `_ThicknessSubsurface` | Thickness/transmission/wetness/edge scatter source | Kelp blades and soft coral only if material route supports it. Source only until shader support is proven. |
| BioForge `_ORMAtlas` | Starter exception | ORMA order: R=AO, G=Roughness, B=0/metallic unused, A=Emission. Do not bind as final MRAO without conversion/proof. |

## Prompt Requirements

STATIC VERIFIED prompt requirements are also written to `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2103_coral_reef_flora_prompt_requirements.md`.

| Prompt target | Basis | Required output | Rejection core |
| --- | --- | --- | --- |
| `TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604` | 2022 rank 4, 2004 coral channel matrix | Seamless square 4096 preferred albedo plus height-like material source for geometry-backed branching coral. | Whole coral object render, smooth tube coral, candy/neon reef, baked shadows, low-res mush, perspective. |
| `TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604` | 2022 rank 8, 2004 kelp matrix | Seamless square 4096 preferred albedo plus height-like source for kelp blade/holdfast material derivation. | Flat ribbon wallpaper, alpha-card identity, whole plant render, muddy dark mass, random neon, baked lighting. |

No generation was run. No image exists from this task.

## Source QA Checklist

STATIC VERIFIED. Reject source candidates that show:

- non-square or non-tileable output where the row requires tileability;
- baked directional lighting, cast shadows, object render framing, perspective camera, horizon, or scene background;
- text, logo, UI, watermark, labels, serial-like random glyphs, or prompt artifacts;
- low-resolution mush, blurred JPEG damage, wallpaper symmetry, obvious repeated pore stamps, or unusable height signal;
- random neon or full-surface bioluminescence instead of localized biological signals;
- dark/noir grading in photic source material;
- texture-only detail hiding missing topology;
- impossible material truth: metallic coral/kelp, uniformly glossy coral, plastic kelp, glowing dirt;
- alpha-only vegetation/fan interpretation where geometry-backed families are required.

Required later static intake for generated images: save under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`, run candidate-specific intake audit, perform manual 2x2 and 3x3 tiling review, then derive PBR maps. `PASS_STATIC` is not Unity acceptance.

## Future Mesh Proof Template

PENDING VERIFICATION until a later Unity/source owner produces artifacts:

| Required proof | Minimum content |
| --- | --- |
| Flat-material silhouette sheet | Texture-off view for each family proving it does not read as primitive, ribbon, tube, blob, paper plate, carpet, or alpha card. |
| Topology/weld proof | Branch unions, holdfast/root contact, plate rims, fan ribs, holes/tears, and mesh thickness visible. |
| Vertex color sheet | R/G/B/A channel debug with the meanings in this report. |
| LOD overlay | LOD0/1/2 triangle cascade, dither/crossfade policy, no blind silhouette collapse. |
| Collider/no-collider justification | Flora visual finals default no collision; large blocking coral/debris needs separate proxy owner proof. |
| Material debug | Albedo/detail/normal/mask/emission/vertex data alignment. |
| Final photic capture | Bright 0-5 m and route-depth captures. No dark/fog masking. |
| Black box/generator postmortem | Critical generation pipelines should keep the last 300 bake steps and dump on failure/non-finite geometry. |

## Future Material Proof Template

PENDING VERIFICATION until a later Unity/source owner produces artifacts:

| Required proof | Minimum content |
| --- | --- |
| PBR closeup | Wet tissue/calcite/sediment/pores/fibers readable under actual URP lighting. |
| Map debug | Albedo, detail, normal, mask, emission/wetness, optional thickness/subsurface. |
| Import settings | sRGB/linear, normal type, mipmaps, Read/Write off, compression target, wrap mode. |
| Shader contract | `Hecton8/Flora/KelpMaster`, `Hecton8/Flora/CoralMaster`, or explicit soft-coral/coastal shader route. |
| SRP Batcher/shared material note | No duplicated runtime materials; instancing/drawer route proven later. |
| Channel order proof | Standard MRAO versus BioForge ORMA exception explicitly converted or rejected. |

## Future Placement Proof Gate

PENDING VERIFICATION until placement owner artifacts exist:

| Gate | Required proof |
| --- | --- |
| Dry-land zero proof | Kelp/coral cannot spawn on dry terrain or above-water dry shoreline. |
| Submerged acceptance proof | Kelp/coral accept valid submerged seafloor/reef substrate in photic route. |
| Shoreline/intertidal split | Coastal flora appears only in tidal/wet boundary contexts and does not borrow kelp/coral dry-land rules. |
| Substrate proof | Reef rock/fossil carbonate/floor rubble/shelf edge constraints are serialized and honored. |
| Route readability | Kelp patches and coral shelves preserve fauna sightlines, navigation cues, oxygen-return cues, and route landmarks. |
| Overdraw/profiler | Dense fields require overdraw and profiler proof before High/Ultra density claims. |

## Continuous GlobalQualityWeight Consequences

STATIC VERIFIED source constraint. This table describes source/fidelity consequences only; it does not change gameplay truth, material role semantics, save identity, collider truth, DTO layout, or placement authority.

| Weight band | Consequence |
| --- | --- |
| Low / compact | Sparse density, smaller imported max sizes, earlier LOD/HLOD, fewer soft fans, simple shared materials. Silhouette, anchors, substrate truth, channel semantics, wet material identity, and dry-land rejection remain mandatory. |
| Middle | Complete family coverage for photic route density with accepted LOD/material/source constraints and route gaps preserved. |
| High | Richer pores, branch knuckles, blade fibers, scars, pigment variation, localized biolum masks, stronger wetness/detail normals, and current-response presentation. |
| Ultra | Dense reef/flora hero overkill only after placement, overdraw, import, profiler, and route readability proof. Ultra cannot rescue bad seams, wrong channels, proxy fallback, or weak topology. |

## No-Fallback Rule

STATIC VERIFIED:

Production family profiles must skip missing real finals or mark the family BLOCKED. They must not fall back to primitive meshes, placeholder prefabs, BioForge starter maps, default materials, proxy crowns, proxy beds, alpha-only fans, or generated starter atlases as production visuals.

If a family cannot meet topology, vertex color, texture stack, and placement constraints, it is BLOCKED rather than accepted as a proxy.

## Independence And Handoff

STATIC VERIFIED:

- This package has no dependency on 2101, 2102, 2104, 2105, 2106, or 2107 output.
- References to 2004, 2019, and 2022 are static evidence inputs, not direct execution dependencies.
- Substrate/geology/material-only tasks outside flora/coral source constraints are handoff notes only.
- No visual proof claim is based on static YAML, path existence, old screenshots, or generated starter coverage.

## Proof Packet

STATIC VERIFIED:

- family constraints;
- placement/rejection/topology/channel/material constraints;
- prompt requirements;
- source QA gates;
- future mesh/material/placement proof templates;
- no-fallback and evidence-label audit.

PENDING VERIFICATION:

- source image generation;
- source intake QA on actual generated images;
- PBR derivation;
- mesh generation;
- Unity import;
- material binding;
- placement rule edits;
- route captures;
- overdraw/profiler;
- GC/memory/VRAM;
- build/player proof.

Unity, MCP, Play Mode, profiler, imports, dotnet build, csc, and project build were not run.
