# World Procedural Scatter Dry-Land Risk Audit

Date: 2026-06-04  
Evidence class: STATIC_SOURCE + STATIC_DOC  
Scope: offline text/YAML audit only. Unity was not launched. `dotnet build` was not run. No asset imports, scene edits, prefab edits, material edits, or rule edits were performed.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `world.md`
- `terrain.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

`Docs/Actual Domains of Project.txt` was checked and was not present.

## Files Scanned

- 37 placement rules: `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_*.asset`
- 33 procedural families: `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_*.asset`
- Runtime/static logic:
  - `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
  - `Assets/_Project/Scripts/WorldProceduralPlacementRule.cs`
  - `Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs`
  - `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
  - `Assets/_Project/Scripts/World/ScatterCandidateEvaluator.cs`
  - `Assets/_Project/Scripts/WorldProceduralProxyInstance.cs`
- Proxy/placeholder folders:
  - `Assets/_Project/Prefabs/WorldProceduralProxy`
  - `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`

## Static Findings

1. Dry-land depth acceptance is real risk. `WorldProceduralFieldSampler` computes `DepthMeters = max(0, WaterSurface - CenterHeight)` and `TrySampleSeafloor` computes `depthMeters = max(0, waterSurface - seafloorHeight)`. Dry terrain at or above water resolves to depth 0, so underwater rules with `minDepthMeters: 0` remain eligible unless another hard gate rejects them.
2. 12 rules serialize `minDepthMeters: 0`. The high-risk cases are kelp, coral, seafloor rock, rock cluster, safe pocket, passive fauna, and landmark spire.
3. All 37 scanned placement rules serialize `preferSeafloor: 0`. This includes rules whose labels or domains are explicitly seafloor, kelp, coral, rock floor, cave entrance, salvage debris, ruins, power routes, and underwater fauna zones.
4. No scanned placement rule serializes `requiredSubstrate`. Runtime therefore uses the C# default `FloraSubstrateMask.Any` (`3`) from `WorldProceduralPlacementRule`.
5. `ScatterCandidateEvaluator.PassesStrictSubstrateEnvelope` treats `None` and `Any` as pass. Static search found no runtime call from `WorldProceduralScatterDirector` to this helper in the scatter acceptance path, so substrate is not a reliable hard barrier in the current path.
6. No scanned rule serializes `strictEnvelopeMapping`; the C# default is `true`. `WorldProceduralScatterDirector.MatchesScatter` currently applies preferred biome/zone/socket checks only when `!runtimeRule.StrictEnvelopeMapping`. With default `true`, preferred filters are skipped in runtime scatter acceptance.
7. Empty or weak preferred filters are common. Even when a rule serializes preferred biome/zone data, the current strict mapping branch skips those checks. Missing socket filters are especially broad because `GetScatterContentKind` can fall back from family domain.
8. All 33 scanned procedural families serialize `allowProxyPrimitives: 1`.
9. The project contains 88 `WorldProceduralProxy` prefabs and 30 `WorldRuntime/ProceduralPlaceholders` prefabs. Family variants resolve proxy prefab GUIDs into `Assets/_Project/Prefabs/WorldProceduralProxy/...`.
10. Many risky families already have final-ready alternatives. Proxy selection remains possible because `ResolveRuntimeVariant` falls back to proxy variants and proxy primitives are enabled even where final-ready variants exist.

## Top 10 Risks

| Rank | Rule | Family | Risk |
|---:|---|---|---|
| 1 | `rule.coral.reef` | `family.coral.branching` | `minDepthMeters: 0`, `preferSeafloor: 0`, default `requiredSubstrate: Any`, strict mapping skips filters, proxy variants enabled despite 6 final-ready alternatives. |
| 2 | `rule.kelp.starter` | `family.kelp.tall` | Kelp can accept depth 0 dry terrain; no seafloor preference; default Any substrate; no hard preferred filters; proxy variants enabled despite 14 final-ready alternatives. |
| 3 | `rule.rocks.floor` | `family.rock.small_floor` | Rule label says seafloor, but depth 0 + no seafloor preference + Any substrate can place floor rocks on shoreline/dry terrain. 12 final-ready alternatives exist. |
| 4 | `rule.rocks.cluster` | `family.rock.cluster.medium` | Clustered rock cover accepts depth 0 and no seafloor/substrate hard gate; proxy cluster/ridge/stack variants remain selectable despite 13 final-ready alternatives. |
| 5 | `rule.coral.branching` | `family.coral.branching` | Serializes biome/zone preferences, but default strict mapping causes runtime `MatchesScatter` to skip them; depth 0 and Any substrate remain. |
| 6 | `rule.coral.low` | `family.coral.low` | Low coral beds accept depth 0; no seafloor/substrate hard gate; proxy bed/plate variants remain selectable despite final baked coral prefabs. |
| 7 | `rule.kelp.canopy` | `family.kelp.canopy` | Canopy crowns accept depth 0 and proxy crown/frond variants; 15 final baked canopy variants exist. |
| 8 | `rule.kelp.patch.dense` | `family.kelp.patch.dense` | Dense kelp patches accept depth 0 and proxy patch/grove variants; 12 final baked variants exist. |
| 9 | `rule.kelp.tall` | `family.kelp.tall` | Same risk class as `rule.kelp.starter`: depth 0, seafloor false, Any substrate, proxy variants, final alternatives available. |
| 10 | `rule.pocket.safe` | `family.pocket.safe` | Safe underwater reorientation pockets accept depth 0, no seafloor/substrate hard gate, and proxy bubble/shelter variants despite a final support prefab. |

## `minDepthMeters: 0` Rules

These are the dry-land acceptance candidates because depth 0 is both dry/shoreline and very shallow water in current field sampling.

| Rule | Family | Domain | Max Depth |
|---|---|---|---:|
| `rule.coral.reef` | `family.coral.branching` | Coral | 600 |
| `rule.kelp.starter` | `family.kelp.tall` | Kelp | 180 |
| `rule.rocks.cluster` | `family.rock.cluster.medium` | RockCluster | 5000 |
| `rule.rocks.floor` | `family.rock.small_floor` | Rock | 5000 |
| `rule.coral.branching` | `family.coral.branching` | Coral | 600 |
| `rule.coral.low` | `family.coral.low` | Coral | 550 |
| `rule.kelp.canopy` | `family.kelp.canopy` | Kelp | 180 |
| `rule.kelp.patch.dense` | `family.kelp.patch.dense` | Kelp | 220 |
| `rule.kelp.tall` | `family.kelp.tall` | Kelp | 180 |
| `rule.pocket.safe` | `family.pocket.safe` | SafePocket | 5000 |
| `rule.fauna.passive` | `family.creature.spawn.passive` | CreatureSpawn | 2500 |
| `rule.landmark.spire` | `family.landmark.spire` | Landmark | 5000 |

## Seafloor / Underwater Rules With `preferSeafloor: 0`

All 37 scanned placement rules serialize `preferSeafloor: 0`. The severe subset is every ground-attached underwater family:

- Kelp: `rule.kelp.starter`, `rule.kelp.tall`, `rule.kelp.patch.dense`, `rule.kelp.canopy`, `rule.kelp.abyssal`
- Coral: `rule.coral.reef`, `rule.coral.low`, `rule.coral.branching`, `rule.coral.massive`, `rule.coral.plate`, `rule.coral.brittle`
- Rocks/geology: `rule.rocks.floor`, `rule.rocks.cluster`, `rule.rocks.shelf`, `rule.rocks.arch`, `rule.landmark.spire`, `rule.cave.entries`
- Underwater support/debris: `rule.debris.scatter`, `rule.debris.field`, `rule.debris.salvage`, `rule.route.power`, `rule.service.scar`, `rule.ruin.module.single`, `rule.ruin.medium`, `rule.ruin.cluster.medium`, `rule.ruin.megastructure`
- Underwater pockets/fauna anchors: `rule.pocket.safe`, `rule.pocket.resource`, `rule.pocket.hazard`, `rule.egg.cluster`, all scanned fauna rules

## Substrate Gate Risk

Serialized placement data does not contain `requiredSubstrate` at all. Runtime default is `Any`, and the current helper treats `Any` as pass. For underwater/seafloor placement this is too broad.

Recommended hard substrate defaults:

- Kelp/coral: `Rock` unless a specific sand-rooted shallow flora rule exists.
- Rock floor/cluster/shelf/arch/landmark/cave: `Rock`.
- Debris/salvage/ruins/power/service/pockets: `Sand | Rock` is acceptable only with a separate submerged/seafloor gate.
- Open-water fauna anchors: no substrate requirement, but they must not use seafloor placement rules.

## Biome / Zone / Socket Filter Risk

Runtime code risk is higher than data risk. `WorldProceduralPlacementRule.MatchesScatter` would enforce preferred arrays directly, but `WorldProceduralScatterDirector.MatchesScatter` only checks preferred biome/zone/socket when `StrictEnvelopeMapping` is false. Because no asset serializes `strictEnvelopeMapping`, the runtime default is `true`, so the current scatter path skips preferred filters.

Correction target: invert or rename this logic. If `StrictEnvelopeMapping == true`, preferred biome/zone/socket arrays must be hard acceptance gates. If false, they can remain score-only affinities.

## Proxy / Placeholder Risk

Static counts:

- 33/33 scanned procedural families have `allowProxyPrimitives: 1`.
- 88 prefabs exist under `Assets/_Project/Prefabs/WorldProceduralProxy`.
- 30 prefabs exist under `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`.
- Editor scripts still define both roots:
  - `WorldProceduralProxyAuthoring`: `Assets/_Project/Prefabs/WorldProceduralProxy`
  - `WorldProceduralPlaceholderAuthoring`: `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`

Visible shoreline/photic routes must not use these proxy/placeholder families when final-ready variants exist. This violates the visual floor from `TASTE.md`, `VISION_LOCKS.md`, `world.md`, `terrain.md`, `3DMODEL_FLORA_CORAL.md`, `3DMODEL_GEOLOGY_ROCKS.md`, and `PROCEDURAL_ASSET_PIPELINE.md`.

## Final-Ready Alternatives Already Present

Use these as replacement targets or default runtime variants after owner review.

| Risk Rule | Reject Proxy Variants | Safer Final Alternatives |
|---|---|---|
| `rule.kelp.starter`, `rule.kelp.tall` | `PFB_family_kelp_tall__stalk.prefab`, `PFB_family_kelp_tall__lean.prefab` | `GEN_family_kelp_tall__broadleaf__s110-170.prefab`, `GEN_family_kelp_tall__colossus__s160-240.prefab`, `GEN_family_kelp_tall__frondcrest__s105-165.prefab`, `GEN_family_kelp_tall__paddle__s90-150.prefab` |
| `rule.kelp.patch.dense` | `PFB_family_kelp_patch_dense__patch.prefab`, `PFB_family_kelp_patch_dense__grove.prefab` | `GEN_family_kelp_patch_dense__bladder__s80-135.prefab`, `GEN_family_kelp_patch_dense__frilltuft__s75-125.prefab`, `GEN_family_kelp_patch_dense__nest__s65-105.prefab`, `GEN_family_kelp_patch_dense__paddlespray__s70-120.prefab` |
| `rule.kelp.canopy` | `PFB_family_kelp_canopy__crown.prefab`, `PFB_family_kelp_canopy__frond.prefab` | `GEN_family_kelp_canopy__featherfan__s120-200.prefab`, `GEN_family_kelp_canopy__laminaria__s105-165.prefab`, `GEN_family_kelp_canopy__oar__s110-180.prefab`, `GEN_family_kelp_canopy__paddlefan__s120-190.prefab` |
| `rule.coral.reef`, `rule.coral.branching` | `PFB_family_coral_branching__branch.prefab`, `PFB_family_coral_branching__mass.prefab` | `GEN_family_coral_branching__bouquet.prefab`, `GEN_family_coral_branching__branch.prefab`, `GEN_family_coral_branching__crest.prefab`, `GEN_family_coral_branching__fan.prefab` |
| `rule.coral.low` | `PFB_family_coral_low__bed.prefab`, `PFB_family_coral_low__plate.prefab` | `GEN_family_coral_low__bed.prefab`, `GEN_family_coral_low__knoll.prefab`, `GEN_family_coral_low__mound.prefab`, `GEN_family_coral_low__plate.prefab` |
| `rule.rocks.floor` | `PFB_family_rock_small_floor__low.prefab`, `PFB_family_rock_small_floor__flat.prefab`, `PFB_family_rock_small_floor__group.prefab` | `PFB_Geo_RockFloor_00.prefab`, `PFB_Geo_RockFloor_01.prefab`, `PFB_Geo_RockFloor_02.prefab`, `PFB_Geo_RockFloor_03.prefab`; also serialized as final: `Nordic_Beach_Rock.prefab`, `Mossy_Forest_Rock.prefab` |
| `rule.rocks.cluster` | `PFB_family_rock_cluster_medium__cluster.prefab`, `PFB_family_rock_cluster_medium__ridge.prefab`, `PFB_family_rock_cluster_medium__stack.prefab` | `PFB_Geo_RockCluster_00.prefab`, `PFB_Geo_RockCluster_01.prefab`, `PFB_Geo_RockCluster_02.prefab`, `PFB_Geo_RockCluster_03.prefab`; also serialized as final: `Nordic_Beach_Rock_Formation.prefab`, `Forest_Rock_Shelf.prefab`, `Rock_Skala.prefab` |
| `rule.pocket.safe` | `PFB_family_pocket_safe__bubble.prefab`, `PFB_family_pocket_safe__shelter.prefab` | `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Safe.prefab` |

The `Nordic_Beach` and `Forest` rock prefabs are serialized as final-ready alternatives. They still need owner visual/material review before underwater seafloor use; the safer production route is the `PFB_Geo_*` procedural finals where available.

## Correction Plan For Unity Owner

1. Data gate: set `minDepthMeters` above dry acceptance for underwater ground-attached rules. Use a small submerged threshold such as `1.0-2.0m` for photic kelp/coral and create separate shoreline/coast rules for wet/dry rock if needed. Do not use `0` for kelp/coral/seafloor rocks.
2. Data gate: set `preferSeafloor: 1` for ground-attached underwater rocks, kelp, coral, eggs, debris, ruins, construction support, cave entries, pockets, and resource/hazard nodes. Leave it false only for true open-water spawn/volume anchors.
3. Data gate: serialize `requiredSubstrate` explicitly. Use `Rock` for kelp/coral/geology/caves; use `Sand | Rock` only where sediment placement is intended and also submerged.
4. Data gate: fill preferred biome/zone/socket arrays for shallow production routes. Rules that are broad by design must say so in their rule label/intent and still pass depth/substrate gates.
5. Code gate: fix `WorldProceduralScatterDirector.MatchesScatter` so strict envelope mapping enforces preferred biome/zone/socket arrays. Current default strict mapping skips them.
6. Code gate: add an explicit submerged seafloor test before accepting underwater/seafloor domains: `waterSurface - seafloorHeight >= minWetDepth` or an equivalent field on `FieldSample`. Depth 0 must be rejected for underwater ground-attached domains.
7. Code gate: wire `PassesStrictSubstrateEnvelope` or equivalent into the runtime candidate acceptance path. `Any` should mean "any resolved substrate bit", not "ignore substrate", for seafloor rules.
8. Proxy gate: set `allowProxyPrimitives: 0` for production-visible families with final variants. Runtime variant selection should prefer final-ready variants in normal play. Proxies/placeholder prefabs should be editor preview, diagnostics, or missing-final fallbacks only.
9. Runtime fallback gate: if a final variant is missing, skip placement in product routes instead of displaying a proxy in surface, shoreline, photic, or medium-depth hero routes.
10. Proof after edits: run Unity owner validation only after data/code changes: rule asset diff, editor import, scene preview screenshot at shoreline and photic shallows, and a static/runtime placement dump proving zero kelp/coral/seafloor proxy instances above the submerged threshold.

## Quality Consequences

- Low: fewer accepted instances after strict gates, but visible assets stay final-authored LODs. No dry-land kelp/coral. No visible proxy primitives. Saved instance budget buys stronger silhouettes and material identity.
- Middle: final variants populate photic routes with substrate-correct density. Route readability improves because scatter is not wasted on dry terrain or wrong zones.
- High: higher final-variant residency radius and richer near-field flora/rock density can be enabled without changing placement truth.
- Ultra: longer final-variant radius, denser final baked flora/geology, and visual overkill are allowed. Ultra must not introduce new placement truth or make proxies acceptable.

## Residual Risk

This report proves static data/code risk only. It does not prove scene wiring, active rule lists, runtime placement counts, visual quality, profiler cost, or build health. Runtime confirmation requires Unity/editor evidence after the owner applies data/code corrections.
