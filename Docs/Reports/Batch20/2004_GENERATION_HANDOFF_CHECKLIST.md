# Batch20 2004 Flora Coral Generation Handoff Checklist

Status: static checklist only. 2004 did not run Unity, import assets, trigger builds, or edit active `Assets` files.

## Inputs

- [ ] Confirm source package: `Docs/Reports/Batch20/2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md`.
- [ ] Confirm variant matrix: `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv`.
- [ ] Confirm texture channel contract: `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv`.
- [ ] Confirm prompt pack: `Docs/Reports/Batch20/2004_PROMPT_PACKS.md`.
- [ ] Confirm Batch19 atlas prep blockers are still valid or resolved.
- [ ] Confirm Batch20 dry-land scatter audit blockers are resolved before scene placement proof.
- [ ] Confirm fauna photic package view lanes and route cues are not blocked by kelp/coral density.

## Source Image Generation

- [ ] Generate or source `SRC_2004_KELP_BLADE_FIBER_4K`.
- [ ] Generate or source `SRC_2004_KELP_HOLDFAST_ROOT_4K`.
- [ ] Generate or source `SRC_2004_KELP_CANOPY_EDGE_4K`.
- [ ] Generate or source `SRC_2004_INTERTIDAL_WEED_LICHEN_4K`.
- [ ] Generate or source `SRC_2004_CORAL_BRANCH_CALCITE_4K`.
- [ ] Generate or source `SRC_2004_CORAL_MASSIVE_POROUS_4K`.
- [ ] Generate or source `SRC_2004_CORAL_PLATE_RIM_UNDERSIDE_4K`.
- [ ] Generate or source `SRC_2004_CORAL_LOW_SPONGE_BED_4K`.
- [ ] Generate or source `SRC_2004_REEF_FAN_SOFT_RIB_4K`.
- [ ] Generate or source `SRC_2004_ANCHOR_DEBRIS_ENCRUSTED_4K`.
- [ ] Generate or source `SRC_2004_BIOLUM_DETAIL_MASKS_4K`.
- [ ] Reject any source with perspective lighting, cast shadows, labels, text, watermark, blur, muddy/noir grade, neon noise, or toy/cartoon finish.
- [ ] Extract `_BaseMap`, `_DetailMap`, `_NormalMap`, and `_MaskMap` source sets per family.
- [ ] Name imported maps as `{mapToken}___{familyId}.png` under the managed imported root or supported revision folder.

## Topology Generation

- [ ] Build `2004.kelp.tall.hero` with real holdfast, ribbed tapering stipe, blade thickness, serration, tears, scars, and vertex sway masks.
- [ ] Build `2004.kelp.patch.filler` as clustered rooted geometry with fauna sightline gaps.
- [ ] Build `2004.kelp.canopy.silhouette` as an upper crown/canopy landmark with readable waterline silhouette.
- [ ] Build `2004.intertidal.shoreline.flora` as shoreline-specific flora and wet algae; do not use underwater kelp families on dry land.
- [ ] Build `2004.coral.branching` with welded branch intersections, broken tips, inner cavities, and AO-ready geometry.
- [ ] Build `2004.coral.massive` with lobes, pores, calcium bands, sediment abrasion, and shelter cavities.
- [ ] Build `2004.coral.plate` with thick rims, chipped ledges, layered shelves, and underside geometry.
- [ ] Build `2004.coral.low.sponge.floor` with low mounds, sponge openings, floor breakup, and route-edge readability.
- [ ] Build `2004.reef.fan.soft.motion` with geometry-backed ribs and holes; alpha-only fan cards are rejected.
- [ ] Build `2004.anchor.debris.shoreline.blend` as a structural shoreline transition with barnacled debris, rope/chain, algae, and scale reference.
- [ ] Remap vertex colors to final contract: `R=sway`, `G=biolum`, `B=AO/cavity`, `A=family/wear/variant mask`.
- [ ] Confirm topology survives flat material proof before applying detail textures.

## Material Import

- [ ] Bind kelp finals to accepted `Hecton8/Flora/KelpMaster` material route.
- [ ] Bind coral finals to accepted `Hecton8/Flora/CoralMaster` material route.
- [ ] Confirm `_BaseMap`, `_DetailMap`, `_NormalMap`, and `_MaskMap` exist on every material.
- [ ] Confirm albedo import: sRGB, mipmaps on, Read/Write off, Wrap Repeat.
- [ ] Confirm detail import: linear, mipmaps on, Read/Write off, Wrap Repeat.
- [ ] Confirm normal import: normal map type, linear, BC5 target, mipmaps on, Read/Write off, Wrap Repeat.
- [ ] Confirm mask import: linear, BC7 or platform equivalent, mipmaps on, Read/Write off, Wrap Repeat.
- [ ] Confirm no final authored material mixes imported, generated starter, and external unmanaged texture sources.
- [ ] Confirm no stale `_QUALITY_MX350` or `_QUALITY_HIGH` binary keywords.
- [ ] Confirm continuous `GlobalQualityWeight` drives density, fidelity, cadence, and optional visual overkill only.

## Prefab And Family Linkage

- [ ] Write finals under `Assets/_Project/Prefabs/Nature/Flora/Baked` only in the later Unity-authoring task.
- [ ] Keep visual flora finals free of Collider, Rigidbody, Animator, ParticleSystem, and AudioSource components.
- [ ] Use exactly three visible LODs with thresholds `0.6/0.15/0.04/0`.
- [ ] Use crossfade LOD and `animateCrossFading=true`.
- [ ] Disable shadow casting, receive shadows, light probes, reflection probes, and motion vectors unless a route bible explicitly overrides with proof.
- [ ] Confirm LOD triangle cascade is strictly descending.
- [ ] Keep LOD0 within `WorldProceduralFloraFinalBudgetCatalog` family budgets.
- [ ] Link real final-ready variants into `WorldPrefabFamilyProfile` assets.
- [ ] Disable or hard-gate proxy fallback for production-visible flora/coral families.
- [ ] Confirm missing real final variants are skipped, not replaced with proxy primitives.

## Placement Gates Before Visual Proof

- [ ] Fix underwater kelp/coral dry-land risk before claiming scene placement.
- [ ] Kelp and coral placement must require submerged seafloor or an explicit route-card fallback.
- [ ] Shoreline/intertidal flora must use its own coastal rule and cannot reuse kelp/coral rules for dry terrain.
- [ ] `preferSeafloor` or equivalent seafloor gate must be hard-enforced.
- [ ] Required substrate or route context must be serialized and honored.
- [ ] `StrictEnvelopeMapping` must not bypass placement filters for production route acceptance.
- [ ] Capture dry-land rejection proof and submerged acceptance proof.

## Proof Artifact Requirements

- [ ] Flat-material silhouette sheet for all 10 variants.
- [ ] Final PBR closeup sheet for all 10 variants.
- [ ] Texture channel sheet: albedo/detail/normal/mask.
- [ ] Vertex color channel sheet: R/G/B/A.
- [ ] Shoreline proof: intertidal flora and anchor/debris transition, no underwater kelp on dry land.
- [ ] 0-5 m photic water proof: bright, readable, premium, not dark-masked.
- [ ] 20-50 m route proof: coral/kelp readable with route cues intact.
- [ ] Fauna coordination proof: passive shoal or silhouette lane visible through kelp/reef.
- [ ] LOD overlay and triangle cascade proof for each family.
- [ ] Validator proof: `WorldProceduralFloraFinalVariantValidator`.
- [ ] Validator proof: `WorldProceduralFamilyContractValidator`.
- [ ] Quality gate proof: `WorldProceduralFinalPrefabQualityGate`.
- [ ] Profiler proof: compact, middle, high, ultra `GlobalQualityWeight` captures after import.

## GlobalQualityWeight Acceptance

- [ ] Low: lower density and smaller maps, but silhouettes remain premium and no proxy/primitive fallback appears.
- [ ] Middle: full family coverage, moderate density, route readability intact.
- [ ] High: richer normals/detail and additional variants within budget.
- [ ] Ultra: hero variants may spend extra texture/detail budget by route card, without changing gameplay truth or placement ownership.

## Final Static Note

This checklist is not evidence that Unity assets were generated, imported, validated, placed, or profiled. All such items are `PENDING VERIFICATION` until a later Unity owner supplies artifacts.
