# 1900 WorldSupport Carrier Production Contract

Evidence class: STATIC_SOURCE / STATIC_DOC
Runtime proof: PENDING UNITY
Unity import proof: PENDING UNITY
Screenshot proof: PENDING UNITY
Profiler proof: PENDING UNITY
Frame Debugger proof: PENDING UNITY
Date: 2026-06-04

## Scope

This report defines the production contract for replacing the nine visible primitive WorldSupport carrier prefabs. It does not implement assets, relink prefabs, edit source, run Unity, run import, run builds, run PlayMode, run profiler, capture screenshots, or touch DataMonolith.

Owned outputs:

- `Docs/Reports/Batch18/1900_WORLD_SUPPORT_CARRIER_PRODUCTION_CONTRACT.md`
- `Docs/Reports/Batch18/1900_WORLD_SUPPORT_CARRIER_PRODUCTION_MATRIX.csv`
- `Docs/Tasks/Status_1900.md`
- `Docs/AgentLogs/Rationale_1900.md`
- `Docs/AgentLogs/LOG_1900.md`

## Authority Read

Root and domain bibles:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `terrain.md`
- `creatures.md`
- `vfx.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`

Mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Prior evidence packets:

- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1854_WORLD_SUPPORT_VISIBLE_CARRIER_REPLACEMENT_PACKET.md`
- `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md`

## Static Evidence

Static text search reconfirmed:

- The nine `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_*.prefab` files exist.
- Each current final support prefab contains Unity built-in primitive mesh GUID references (`0000000000000000e000000000000000`) in visible child renderers.
- The nine `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_*.asset` support profiles contain `finalReady: 1` variants pointing at the current final support prefabs.
- Current profiles also preserve `proxyOnly: 1` variants. These are not production visible art.
- Valid candidate sources exist as static files under flora and geology folders, but they remain candidates only. Prior audit state still includes missing manifests, missing named proof, and pending surface/shallow visual proof.

Static source search is not Unity proof. It proves text/file presence only.

## Prime Contract

The production route is not to delete WorldSupport families. The route is to split hidden gameplay truth from visible carrier art.

Hidden truth stays on the WorldSupport family/profile route:

- family id;
- final variant id;
- placement mode;
- heatmap channel;
- min spacing;
- cluster radius;
- cluster count;
- spawn, zone, hazard, resource, or safe-pocket role;
- trigger/collider volumes with renderers absent or disabled;
- owner components and future manifests that gameplay systems read.

Visible carrier art becomes replaceable presentation:

- authored or offline-generated `GEN_WS_*` carrier prefab;
- no built-in cube, sphere, capsule, cylinder, plane, or quad mesh used as visible art;
- no `WorldProceduralProxy` production reuse;
- no `WorldRuntime/ProceduralPlaceholders` production reuse;
- no AI proxy, creature-debug proxy, or current primitive final preserved as production visible art;
- no gameplay truth embedded in emissive, mesh, VFX, or material objects.

Future implementation must be able to replace visual carrier roots without changing spawn/resource/hazard/safe-pocket truth, save identity, family ids, variant ids, DTO layout, or support authority route.

## Nine Blockers

| Support family | Current final prefab | Hidden truth owner | Production visible carrier |
|---|---|---|---|
| `family.creature.spawn.passive` | `PFB_Support_CreatureSpawn_Passive.prefab` | `ProceduralFamily_family_creature_spawn_passive.asset`: spawn anchor, `fauna_density`, spacing 12 m, cluster radius 20 m, count 2-5. | Nursery reef / kelp school anchor with egg clusters, fish-scale glints, shelter holes, and current ribbons. |
| `family.creature.spawn.predator` | `PFB_Support_CreatureSpawn_Predator.prefab` | `ProceduralFamily_family_creature_spawn_predator.asset`: predator spawn anchor, `hazard_density`, spacing 32 m, cluster radius 24 m, count 1-2. | Cave-mouth lair / perch carrier with scratch marks, broken coral or rock splinters, silt scars, carcass/debris evidence. |
| `family.creature.zone.abyss_apex` | `PFB_Support_Zone_AbyssApex.prefab` | `ProceduralFamily_family_creature_zone_abyss_apex.asset`: apex ownership zone, `hazard_density`, spacing 220 m, radius 180 m, count 1. | Abyss landmark with geology mass, industrial ruin scar, deep biolum sensor frame, and instrument-readable silhouette. |
| `family.creature.zone.large_threat` | `PFB_Support_Zone_LargeThreat.prefab` | `ProceduralFamily_family_creature_zone_large_threat.asset`: large-threat ownership zone, `hazard_density`, spacing 180 m, radius 140 m, count 1. | Territorial warning arch / broken spine geology / perch shelf with scrape field and large-owner route warning. |
| `family.creature.zone.reef_apex` | `PFB_Support_Zone_ReefApex.prefab` | `ProceduralFamily_family_creature_zone_reef_apex.asset`: reef apex bio territory, `bio_density`, spacing 160 m, radius 120 m, count 1. | Bright premium coral canopy, branching crown, kelp drift, and shelter pockets. No darkness masking in photic routes. |
| `family.creature.zone.ruin_apex` | `PFB_Support_Zone_RuinApex.prefab` | `ProceduralFamily_family_creature_zone_ruin_apex.asset`: ruin apex ownership zone, `ruin_density`, spacing 180 m, radius 150 m, count 1. | Wreck/ruin perch integrated with rock, coral overgrowth, worn frame material, and route-risk evidence. |
| `family.pocket.hazard` | `PFB_Support_Pocket_Hazard.prefab` | `ProceduralFamily_family_pocket_hazard.asset`: hazard pocket, `hazard_density`, spacing 10 m, radius 14 m, count 2-4. | Vent chimney / parasite coral / tube-worm field / toxic mineral stain / heated water shimmer carrier. VFX remains cause-bound presentation. |
| `family.pocket.resource` | `PFB_Support_Pocket_Resource.prefab` | `ProceduralFamily_family_pocket_resource.asset`: resource pocket, `resource_density`, spacing 6 m, radius 10 m, count 3-7. | Mineral cache / coral-encrusted deposit / sheltering rock pocket / forager-trace carrier. Inventory truth stays outside art. |
| `family.pocket.safe` | `PFB_Support_Pocket_Safe.prefab` | `ProceduralFamily_family_pocket_safe.asset`: safe pocket, `shelter_density`, spacing 10 m, radius 12 m, count 1-2. | Readable shelter arch / canopy / calm water / refuge cue carrier, visible from approach distance. |

## Allowed Source Ingredients

Allowed only after manifest, material, collider, LOD, screenshot, and runtime proof:

- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/*`
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/*`
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp/*`
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/TubeCoral/*`
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/PorousRock/*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_CaveEntrance_*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_LandmarkSpire_*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_RockArch_*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_RockCluster_*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_RockFloor_*`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_RockShelf_*`
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/*/*_LOD0.asset`
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/*/*_LOD1.asset`
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/*/*_LOD2.asset`
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/*/*_COL.asset`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_Coral*`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_Kelp*`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_AlbedoAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_NormalAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_ORMAtlas.png`

These are source ingredients, not final support replacements. The 1858 packet keeps manifest/proof warnings active.

## Forbidden Shortcuts

Reject:

- visible `WorldProceduralProxy` links in final support prefabs;
- visible `WorldRuntime/ProceduralPlaceholders` links in final support prefabs;
- current primitive final prefabs kept as visible art;
- built-in primitive mesh GUIDs in visible carrier renderers;
- AI proxy or creature-debug proxy objects used as production carrier art;
- `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png`;
- placeholder materials, flat colors, debug colors, crayon-like synthetic albedo, or unverified AI texture output;
- runtime mesh generation, runtime texture synthesis, runtime UV unwrapping, runtime collider cooking;
- LOD0 visual mesh as production collision;
- mesh/material/VFX object treated as gameplay truth owner;
- alpha-blended dense flora fields on compact lane;
- darkness, fog, bloom, or particles used to hide primitive art.

## Future Authoring Route

Future implementation owner scope:

- editor/offline authoring only;
- no runtime generation;
- no gameplay truth migration;
- no public API signature mutation;
- no source dependency on sibling agents' unfinished code.

Recommended future files and folders:

- `Assets/_Project/Scripts/Editor/WorldSupportCarrierAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldSupportCarrierValidator.cs`
- `Assets/_Project/Scripts/Editor/WorldSupportCarrierManifestWriter.cs`
- `Assets/_Project/Data/World/SupportCarriers/WorldSupportCarrierManifest_*.asset`
- `Assets/_Project/Prefabs/WorldSupport/Generated/GEN_WS_*`
- `Assets/_Project/Art/Generated/WorldSupport/Meshes`
- `Assets/_Project/Art/Generated/WorldSupport/Textures`
- `Assets/_Project/Art/Generated/WorldSupport/Materials`
- `Docs/Reports/GeneratedAssets/PROOF_<assetStem>_validation.md`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__flat_material__compact.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__final_material__compact.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__final_material__high.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__gameplay_distance__normal.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__route_composition__normal.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__wire_lod_collider__editor.png`

Future authoring process:

1. Read each `ProceduralFamily_*.asset` as truth owner input.
2. Compose `GEN_WS_*` visible carrier from allowed flora/geology/source ingredients or purpose-built offline meshes.
3. Write a carrier manifest with family id, variant id, hidden marker roles, visual root, source ingredients, material slots, vertex color contract, LOD chain, collider proxies, Addressables target, and proof paths.
4. Validate no visible built-in primitive mesh refs remain.
5. Validate no `WorldProceduralProxy` or placeholder production links remain.
6. Validate hidden truth volumes are rendererless or renderer-disabled.
7. Capture compact/middle/high/ultra screenshots in route-appropriate lighting.
8. Run profiler and Frame Debugger proof before relinking `finalReady` variants.
9. Only then relink final variants into `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_*`.

## Material, Texture, And Vertex Color Contract

Material slots:

- Slot 0: primary geology or organic structure.
- Slot 1: exposed wear, fracture, tooth, nest pad, mineral face, tube-worm surface, or hazard face.
- Slot 2: secondary coral, kelp, biological crust, trim, or ruin overgrowth.
- Slot 3: emissive, biolum, sensor, hazard, route accent, or water-sheen detail.

Texture roles:

- Albedo/base color: sRGB, compressed, no baked lighting.
- Normal: normal import, preferably BC5 where target pipeline permits.
- MRAO/ORM: linear, packed, channel semantics written in manifest.
- Emission/biolum mask: only where a route cue, danger cue, or ecology cue exists.
- Detail/wetness mask: allowed for waterline, cavity, grime, mineral, or biological surface response.

Default mask policy:

- Follow the project shader contract for MRAO/ORM channel semantics. Do not guess roughness vs smoothness.
- Do not ship separate AO/roughness/metallic/emission textures unless the shader route proves need.
- Use shared materials and atlases/arrays. Do not create material-per-instance spam.

Vertex color semantics must be manifest-owned:

- Flora/kelp/coral default: R = current sway amplitude, G = biolum phase/mask, B = baked AO/cavity, A = wetness/stability/damage/family mask.
- Geology default: R = edge wear/fracture stress, G = mineral pulse/deposit cue, B = AO/cavity dirt/depth grime, A = wetness/stability/proof-family mask.
- Hazard default: R = hazard intensity, G = pulse/vent phase, B = cavity AO/toxic staining, A = wetness/heat mask.
- Ruin default: R = worn edge/cut stress, G = biological overgrowth or warning paint mask, B = grime/cavity AO, A = wetness/emission eligibility.

Undocumented vertex color meanings are rejected.

## Collider And Trigger Policy

Visible carrier art:

- no built-in primitive visible mesh references;
- no LOD0 `MeshCollider`;
- `LODGroup` or documented HLOD/impostor route required;
- renderers reference shared `MAT_*` assets;
- no gameplay truth stored on visual-only VFX, material, mesh, or animation objects.

Hidden support truth:

- primitives are allowed only as hidden trigger/collider volumes;
- renderers must be absent or disabled;
- hidden roots use `TRG_*` or equivalent trigger naming;
- collision proxy children use `COL_*`;
- family id, heatmap, radius, cluster count, spawn/zone/pocket role, and final variant identity remain under WorldSupport/procedural family ownership;
- future validator must fail visible primitive refs but allow rendererless trigger primitives.

## VFX Contract

VFX is allowed only as presentation of a named cause:

- hazard vent shimmer, silt, bubbles, heat, toxic pulse, or mineral plume must name a hazard cause owner;
- safe pocket calm water, shelter glow, or light cue must not imply free healing, free oxygen, or false gameplay unless the owner system provides it;
- creature zone particles must not fake a spawned creature;
- VFX must be pooled, capped, and load-shed by continuous `GlobalQualityWeight`;
- no material clones, unbounded particles, or particle state used as truth.

## Proof Required Before Production Relink

Static gates:

- matrix covers all nine support families;
- each replacement has carrier manifest with family id, final variant id, hidden truth roles, material slots, vertex colors, LODs, collider policy, and proof paths;
- YAML scan of final support prefabs finds no visible built-in primitive mesh GUIDs;
- YAML/source scan finds no `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` link in final support prefabs;
- every source ingredient has named proof using the full asset stem;
- every current source-only package is either assembled into a production package or explicitly remains source-only.

Unity/editor gates:

- prefabs open with no missing scripts, materials, meshes, or invalid LODGroups;
- hidden support trigger/collider roots remain active and rendererless;
- visible carrier roots render with intended shared materials;
- material import settings match texture roles;
- collider proxies fit route/support function and are not LOD0 visuals.

Screenshot gates:

- compact capture;
- middle/normal capture;
- high capture;
- ultra capture when visual overkill is claimed;
- flat-material inspection;
- final-material inspection;
- gameplay-distance route composition;
- wireframe/LOD/collider overlay;
- photic/surface carriers tested in bright readable water and not hidden by darkness;
- abyss carriers tested under low light plus instrument/route readability.

Profiler and Frame Debugger gates:

- per-carrier presentation cost within local frame budget;
- no new GC allocation path from carrier presentation;
- Frame Debugger confirms acceptable pass/material count;
- GPU Resident Drawer or BatchRendererGroup route proven for dense repeated static carrier ingredients where used;
- no alpha-blended dense flora field on compact lane;
- Addressables/residency proof exists if carriers are streamed.

Runtime/gameplay gates:

- spawn, hazard, resource, and safe-pocket gameplay truth still resolves from family/profile/WorldSupport route;
- no visual art object becomes authority;
- no save identity, DTO layout, family id, or variant id mutation;
- VFX cause owners and pooling proven where VFX is included;
- no runtime mesh/texture/collider generation.

Until these gates exist, status remains PENDING UNITY.

## Compact / Middle / High / Ultra Consequences

Compact:

- same hidden truth, same family ids, same final variant ids;
- simplified ornaments and lower shader feature set;
- shared atlases/arrays, strong silhouettes, no alpha-blend flora fields;
- LOD2/HLOD aggressive but route carrier silhouette remains readable;
- pooled VFX at low count only where cause-readable.

Middle:

- normal LOD chain and shared material set;
- richer scatter density and material masks;
- conservative route cues;
- VFX/current motion present but bounded.

High:

- longer LOD residency;
- richer wetness, cavity AO, biolum, mineral, fracture, and debris cues;
- denser local geology/flora only where Frame Debugger and profiler proof pass.

Ultra:

- offline visual overkill: micro-detail, decals, pores, strata, richer render response, layered wetness, local particle richness, stronger route-specific accent variation;
- no new gameplay truth, no new authority route, no save or DTO change.

## Acceptance

This contract is accepted as a static production planning artifact only when:

- all nine blockers are represented;
- hidden truth owner and visible carrier concept are separated;
- invalid shortcuts are named;
- material, texture, vertex-color, collider, trigger, VFX, authoring, and proof rules are explicit;
- compact/middle/high/ultra consequences are defined;
- CSV parses to nine rows;
- required static verification commands pass.

It does not accept:

- final production art;
- prefab relinks;
- Unity import state;
- screenshot quality;
- runtime behavior;
- profiler or Frame Debugger state.

## Required Verification Record

Executed by agent 1900:

- `git diff --check -- Docs/Reports/Batch18/1900_WORLD_SUPPORT_CARRIER_PRODUCTION_CONTRACT.md Docs/Reports/Batch18/1900_WORLD_SUPPORT_CARRIER_PRODUCTION_MATRIX.csv Docs/Tasks/Status_1900.md Docs/AgentLogs/Rationale_1900.md Docs/AgentLogs/LOG_1900.md`: PASS, no output.
- `Import-Csv Docs/Reports/Batch18/1900_WORLD_SUPPORT_CARRIER_PRODUCTION_MATRIX.csv | Measure-Object`: PASS, `Count: 9`.
- static term cross-check for `family.creature.spawn.passive`, `family.pocket.safe`, `hidden truth`, `visible carrier`, `WorldProceduralProxy`, `Subnautica`, `PENDING UNITY`: PASS, all found.
