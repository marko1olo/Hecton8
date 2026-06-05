# Batch20 2004 BioForge Flora Coral Source Package

Status: static source package only. No Unity, MCP, imports, asset writes, or dotnet build executed by 2004.

## Scope

This package defines the flora/coral source work required before any BioForge or Unity execution. It does not certify existing scene quality. It converts the Batch19 flora/coral atlas prep, Batch20 dry-land scatter audit, and photic fauna coordination report into concrete source families, texture channels, topology constraints, proof shots, and reject gates.

Primary handoff files:

- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv`
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv`
- `Docs/Reports/Batch20/2004_GENERATION_HANDOFF_CHECKLIST.md`
- `Docs/Reports/Batch20/2004_PROMPT_PACKS.md`

## Authority Read

- `AGENTS.md`: explicit batch ID 2004, concise Status/Rationale/LOG required, no fake proof, no active `Assets` edits for this task.
- `TASTE.md`: photic/surface/shallow visuals must be bright, readable, premium, and not hidden by darkness.
- `VISION_LOCKS.md`: first route is bright semi-open photic water; `GlobalQualityWeight=0.0` is not ugly mode.
- `PROCEDURAL_ASSET_PIPELINE.md`: runtime procedural generation is rejected for visible finals; output packages require deterministic source, LODs, colliders/proxies where appropriate, manifests, and proof artifacts.
- `3DMODEL_FLORA_CORAL.md`: kelp needs holdfast, tapering stipe, blade shells, ribs, serration, folds, tears, scars, root pivot; coral needs low, branching, massive, plate, brittle families with welded/hidden intersections and premium material proof.
- `3DMODEL_TEXTURES_MATERIALS.md`: albedo/detail/normal/mask roles must be explicit and import settings must be documented.
- `world.md` and `water.md`: placement follows substrate, current, light, route readability, and seafloor ownership; shallow water cannot use generic blue fog or darkness to hide assets.

## Static Evidence

Integrated reports:

- `Docs/Reports/Batch19/1908_FLORA_CORAL_KELP_SOURCE_ATLAS_PREP.md`
- `Docs/Reports/Batch20/WORLD_PROCEDURAL_SCATTER_DRY_LAND_RISK_AUDIT_20260604.md`
- `Docs/Reports/Batch20/FAUNA_PHOTIC_CREATURE_VISUAL_PACKAGE_20260604.md`

Editor tooling inspected statically:

- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeWindow.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeJobs.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioRuleData.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/ShallowsBioForgeBatchBaker.cs`
- `Assets/_Project/Editor/Generators/Flora/FloraTopologyStudio1604.cs`
- `Assets/_Project/Editor/Generators/Flora/FloraTopologyStudio1711.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralCoralMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraMaterialAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalVariantAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalVariantValidator.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFamilyContractValidator.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs`

Existing generated/starter coverage found statically:

- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows`: 600 mesh `.asset` files from the shallow BioForge batch route.
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows`: 200 prefab files from the shallow BioForge batch route.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora`: 36 `.asset` and 48 `.png` texture files, including kelp/coral family texture stacks and shallow atlas PNGs.
- `Assets/_Project/Prefabs/WorldProceduralProxy`: 88 proxy prefabs.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora`: 8 placeholder flora prefabs.

These are not accepted as final visual proof. Current validators explicitly warn on generated starter texture stacks and block placeholder or primitive final prefabs.

## Tool Capability Boundary

BioForge can produce deterministic editor-time L-system/SDF/ribbon/rock meshes with three LODs and prefabs. It is useful as a source mesh shell route, not a final art guarantee.

Current BioForge limitations that matter for this package:

- `BioForgeGenerator.NormalizeColorGradientFromFinalBounds` writes vertex color `R=height gradient`, `G=0`, `B=0`, `A=1`. This is not the full project flora/coral vertex contract.
- `ShallowsBioForgeBatchBaker` writes a shared `_ORMAtlas` with `R=AO`, `G=Roughness`, `B=0/metallic unused`, `A=Emission`. It is ORMA, not the normal MRAO order.
- The shallow BioForge route has starter coral/kelp coverage, but no static screenshot, scene placement, profiler, or premium source image proof in this task.

Preferred later execution split:

- Kelp topology: start from `WorldProceduralSeaweedMeshBuilder` or Flora Topology Studio outputs for holdfast, stipe, blade, canopy, and patch silhouettes. Use BioForge ribbon output only as a secondary shell source when it passes the same vertex/material gates.
- Coral topology: start from `WorldProceduralCoralMeshBuilder` for low, branching, massive, and plate families. Use BioForge branch capsules for supplemental branching volume, not as a substitute for welded premium coral topology.
- Textures/materials: route imported texture stacks through `WorldProceduralFloraTextureAuthoring` managed imported roots, then `WorldProceduralFloraMaterialAuthoring`.

## Required Variant Families

The source package defines 10 concrete variants. The full manifest is in `2004_FLORA_CORAL_VARIANT_MATRIX.csv`.

1. `2004.kelp.tall.hero`: `family.kelp.tall`, 2-28 m photic shallows. Tall route silhouette with holdfast, ribbed stipe, large torn blades, visible edge thickness, scars, and sway mask.
2. `2004.kelp.patch.filler`: `family.kelp.patch.dense`, 1.5-24 m. Dense shelter patch with many smaller fronds, gaps for fauna silhouettes, and non-card blade geometry.
3. `2004.kelp.canopy.silhouette`: `family.kelp.canopy`, 3-35 m. Upper crown/canopy landmark with readable silhouette from surface and waterline views.
4. `2004.intertidal.shoreline.flora`: no underwater kelp family. Shoreline wrack, salt grass, wet reef weed, lichen/moss strips, and tide-pool vegetation. It must not place underwater kelp on dry land.
5. `2004.coral.branching`: `family.coral.branching`, 2-80 m. Branching coral/fossil carbonate with welded intersections, inner AO, broken tips, asymmetric growth, and readable cavities.
6. `2004.coral.massive`: `family.coral.massive`, 6-120 m. Dome/boulder coral with pores, lobes, calcium bands, sediment abrasion, and shelter pockets.
7. `2004.coral.plate`: `family.coral.plate`, 18-160 m. Layered shelf/plate coral with thick rims, undersides, chipped edges, and side-light readability.
8. `2004.coral.low.sponge.floor`: `family.coral.low`, 1-70 m. Low coral/sponge floor detail, mound beds, porous mats, and route-edge breakup.
9. `2004.reef.fan.soft.motion`: source role maps to `family.coral.brittle` or a later soft-coral family if added. Fan/soft coral motion accent with ribbed branching panels and localized biolum only.
10. `2004.anchor.debris.shoreline.blend`: structural shoreline transition. Barnacled anchor/debris, rope, encrusted metal, and wet algae. Coordinates with debris families; it is not a kelp substitute.

## Source Image Slots

Actual bitmap source images are not present in this task. Later source generation must produce these managed image slots before Unity import:

- `SRC_2004_KELP_BLADE_FIBER_4K`
- `SRC_2004_KELP_HOLDFAST_ROOT_4K`
- `SRC_2004_KELP_CANOPY_EDGE_4K`
- `SRC_2004_INTERTIDAL_WEED_LICHEN_4K`
- `SRC_2004_CORAL_BRANCH_CALCITE_4K`
- `SRC_2004_CORAL_MASSIVE_POROUS_4K`
- `SRC_2004_CORAL_PLATE_RIM_UNDERSIDE_4K`
- `SRC_2004_CORAL_LOW_SPONGE_BED_4K`
- `SRC_2004_REEF_FAN_SOFT_RIB_4K`
- `SRC_2004_ANCHOR_DEBRIS_ENCRUSTED_4K`
- `SRC_2004_BIOLUM_DETAIL_MASKS_4K`

Each source image must be orthographic or material-sample style, usable for PBR extraction, and free of baked perspective lighting, cast shadows, labels, watermarks, UI, camera blur, and decorative black/noir grading.

## Material And Channel Contract

Use the CSV for exact channel rows. Handoff summary:

- Validator material properties: kelp uses accepted `Hecton8/Flora/KelpMaster` variants; coral uses accepted `Hecton8/Flora/CoralMaster` variants.
- Required bound maps: `_BaseMap`, `_DetailMap`, `_NormalMap`, `_MaskMap`.
- Managed imported texture naming: `{mapToken}___{familyId}.png` under `Assets/_Project/Art/Textures/WorldProceduralFlora/Imported/{familyId}/` or a supported revision folder.
- Import contract: Wrap Repeat, mipmaps on, Read/Write off, albedo sRGB, detail/normal/mask linear, normal map type for normal, BC5 for normal, BC7 for other standalone targets unless platform override demands ASTC.
- Standard final `_MaskMap`: `R=metallic`, `G=roughness`, `B=AO`, `A=emission/wetness/family mask as shader manifest states`. Flora/coral metallic is normally 0 except the anchor/debris transition.
- BioForge shallow `_ORMAtlas` exception: `R=AO`, `G=Roughness`, `B=0/metallic unused`, `A=Emission`. It must not be silently mixed with MRAO source stacks.
- Vertex color final contract: `R=sway amplitude`, `G=biolum/emission mask`, `B=AO/cavity`, `A=family/wear/variant mask documented per family`.

## Placement And Ecology Constraints

Dry-land audit blockers must be resolved before final flora/coral placement proof:

- Current audited underwater rules include `minDepthMeters: 0` for kelp tall, kelp canopy, kelp patch dense, coral low, coral branching, and coral reef.
- Audited placement rules serialize `preferSeafloor: 0`.
- No audited rule serialized `requiredSubstrate`.
- `WorldProceduralFieldSampler` maps dry/above-water depth to 0, making min-depth 0 underwater rules eligible on dry terrain.
- `StrictEnvelopeMapping` currently skips preferred filters in `WorldProceduralScatterDirector.MatchesScatter`.
- `allowProxyPrimitives: 1` is still present on flora/coral procedural family assets.

Required later gate before scene proof:

- Underwater kelp/coral rules must require submerged seafloor or equivalent hard route card.
- Shoreline intertidal flora must use its own coastal/intertidal rule, not kelp/coral rules.
- `preferSeafloor` and substrate filters must be hard accepted, not only preferred.
- Production-visible routes must skip missing final variants instead of falling back to proxies.

Fauna coordination:

- Passive photic shoals need clear view lanes through kelp patches and reef shelves.
- Warning/predator fauna belong in medium-depth, cave, hazard, or route-pressure pockets, not the first calm photic exit.
- Biolum flora masks must support navigation and ecology read, not random glow noise.

## Validator Gates For Later Unity Owner

Static package gates:

- No active `Assets` edits from 2004.
- No Unity or import proof claimed by 2004.

Final prefab gates from inspected validators:

- Flora final root: `Assets/_Project/Prefabs/Nature/Flora/Baked`.
- Supported families: `family.kelp.tall`, `family.kelp.patch.dense`, `family.kelp.canopy`, `family.kelp.abyssal`, `family.coral.low`, `family.coral.branching`, `family.coral.massive`, `family.coral.plate`, `family.coral.brittle`.
- LODGroup required: exactly 3 visible LOD levels, crossfade enabled, `animateCrossFading=true`, thresholds `0.6/0.15/0.04/0`.
- Flora visual finals must not carry Collider, Rigidbody, Animator, ParticleSystem, or AudioSource components.
- Renderers should default to no shadow casting, no receive shadows, no probes, and `ForceNoMotion`.
- Materials must enable instancing and pass accepted kelp/coral shader contracts with no stale `_QUALITY_MX350` or `_QUALITY_HIGH` keywords.
- Final authored flora must use imported managed texture stacks, not editor-generated procedural starter textures.
- No Unity built-in primitive mesh GUID `0000000000000000e000000000000000` in production-visible final prefabs.

Family budget gates from `WorldProceduralFloraFinalBudgetCatalog`:

| Family | Max renderers | Max material slots | Max LOD0 triangles | LOD recommended threshold | Fidelity warning floor |
| --- | ---: | ---: | ---: | ---: | ---: |
| `family.kelp.tall` | 12 | 6 | 8000 | 4500 | 360 |
| `family.kelp.patch.dense` | 18 | 8 | 12000 | 6500 | 320 |
| `family.kelp.canopy` | 14 | 6 | 10000 | 5500 | 460 |
| `family.kelp.abyssal` | 14 | 6 | 9000 | 5200 | 380 |
| `family.coral.low` | 10 | 4 | 7000 | 3500 | 900 |
| `family.coral.branching` | 16 | 6 | 12000 | 6500 | 800 |
| `family.coral.massive` | 12 | 5 | 9000 | 5000 | 1100 |
| `family.coral.plate` | 12 | 5 | 8500 | 4500 | 220 |
| `family.coral.brittle` | 14 | 6 | 9500 | 5200 | 720 |

## Reject Gates

Reject immediately:

- Underwater kelp/coral on dry land or above-water terrain.
- Kelp represented by flat ribbons, alpha-card fields, primitive cylinders, smooth tubes, or hidden holdfasts.
- Coral represented by primitive blobs, balls, cones, un-welded branch intersections, untextured tubes, or smooth toy shapes.
- Dense alpha blend dependency for MX350/low tier.
- Texture-only detail hiding missing topology.
- Baked lighting, cast shadows, perspective camera lighting, labels, text, watermark, blur, muddy/noir grading, neon noise, or cartoon material source.
- Placeholder/proxy/primitive prefabs in production-visible final slots.
- Binary quality switches. Use continuous `GlobalQualityWeight`.
- Claims of runtime, profiler, scene, or visual proof without matching artifacts.

## Required Proof Shots

Later BioForge/Unity executor must capture:

- Flat-material silhouette sheet for all 10 variants.
- PBR material closeup sheet for all 10 variants.
- Channel debug sheet: albedo, detail, normal, mask, vertex RGB/A.
- Shoreline view proving intertidal flora and anchor/debris transition without underwater kelp on dry land.
- 0-5 m underwater photic view proving bright readable shallows.
- 20-50 m underwater route view proving medium-depth coral/kelp readability without dark masking.
- Kelp patch view with passive fauna silhouette lane reserved.
- Coral reef shelf view with navigation/oxygen-return cue visibility preserved.
- LOD overlay and triangle cascade proof per family.
- Validator console proof: flora final variant validator, family contract validator, final prefab quality gate.
- Placement proof: dry terrain rejection and submerged seafloor acceptance.
- Performance proof after import: compact, middle, high, and ultra `GlobalQualityWeight` captures with profiler screenshots. Static documents alone cannot satisfy this.

## GlobalQualityWeight Consequences

The same authored source package scales continuously.

- Low / Minimum survival: fewer variants active, smaller imported texture max size, lower density and update cadence, LOD2 reached earlier, silhouettes still premium and readable, no proxy fallback.
- Middle: full family coverage with moderate atlas sizes, conservative density, all required route cue visibility retained.
- High: higher density in route-safe zones, sharper normals/details, extra branch/blade variants, richer biolum masks where ecological.
- Ultra: hero variants can use highest source maps and denser topology within family validator budgets or explicit hero-only route cards; gameplay truth, DTO layout, save identity, and placement authority do not change.

## Top Blockers

1. Actual source bitmap atlases are not present in this task. This package supplies prompt packs and source slots only.
2. Dry-land scatter risk is unresolved in data and runtime acceptance. Existing min-depth 0 underwater rules plus dry depth 0 mapping can place underwater flora on dry terrain.
3. Flora/coral family assets still allow proxy primitives. Production handoff must disable or hard-gate proxy fallback before visual proof.
4. Current BioForge vertex color output is incomplete for the final flora/coral semantic contract.
5. Existing generated/starter texture stacks are not authored photoreal finals. Validators treat them as starter coverage only.

## Verification State

- Static file/report/code inspection: complete for this package.
- Unity import: not run.
- Unity editor validation menus: not run.
- Scene placement proof: not run.
- Profiler proof: not run.
- Build proof: not run.

All editor/runtime results remain `PENDING VERIFICATION`.
