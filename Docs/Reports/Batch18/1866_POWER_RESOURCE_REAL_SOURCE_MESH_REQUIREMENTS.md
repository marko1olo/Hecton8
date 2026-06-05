# 1866 Power/Resource Real Source Mesh Requirements

Agent ID: 1866  
Evidence class: STATIC_SOURCE, STATIC_DOC  
Unity/build/importer/bake/PlayMode/screenshot/profiler execution: NOT RUN  
Mutation boundary: no source, prefab, asset, scene, binary, `.meta`, Unity, importer, bake, screenshot, or profiler edits.

## Scope

This packet defines the real source mesh/material requirements needed before the factory routes blocked by `1861_PRIMITIVE_FACTORY_SOURCE_GATES.md` may be restored.

Blocked routes covered:

- `PowerGridPrefabFactory`: Reactor, RTG, Battery, Relay, Breaker, Junction.
- `ResourceWorldBootstrapAuthoring`: TitaniumScrap, CopperOre, SilicaShards, FiberKelp, MembraneTissue, SilverOre, SulfurClumps, HydrocarbonResin.
- `ResourceDistributionBootstrapAuthoring`: Ore_Generic, Ore_MagmaVentMarker.
- `WorldProceduralInteriorColonyFinalAuthoring`: PanelTrim, ConduitRun, ServiceClutter, HabitatLimb, DockingBay, HabitatShell.

Detailed row matrix: `Docs/Reports/Batch18/1866_POWER_RESOURCE_SOURCE_MATRIX.csv`.

## Authorities Read

- Root/project: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`.
- Domain: `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `construction.md`.
- Missing requested authority: `resources.md` does not exist at `C:\hades\Hecton8\resources.md`; static recursive search found unrelated archived/lore/resource files, not a root bible replacement.
- Batch evidence: `Docs/Reports/Batch18/1860_PRIMITIVE_FACTORY_RISK_CLASSIFICATION_PACKET.md`, `Docs/Reports/Batch18/1861_PRIMITIVE_FACTORY_SOURCE_GATES.md`, `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`, `Docs/Reports/Batch18/1855_CONSTRUCTION_FINAL_MESH_REBUILD_PACKET.md`, `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md`.
- Mandates: `QA_Evidence_Text_Filter_Audit.txt`, `TOOL_Procedural_Wreckage_Generator.txt`, `DATA_Inventory_Resources_Items_SOA_Layout.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`.

## Static Findings

`PowerGridPrefabFactory` expects source discovery from `Assets/_Project/Prefabs/Construction/Power/Sources`, but `Assets/_Project/Prefabs/Construction/Power` is missing. No real Reactor, RTG, Battery, Relay, Breaker, or Junction source mesh package was found. `Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_route_power__relay.prefab` exists, but proxy art is not valid final source.

`ResourceWorldBootstrapAuthoring` currently targets `Assets/_Project/Prefabs/Resources/Pickups`. Existing `PFB_Resource_*` pickup prefabs are present, but `1860` classified the route as a primitive blocker and `1861` blocked regeneration. These prefabs are not proof of real source art. Raw item data assets exist for every listed resource. Copper uses `Data_Copper.asset` while the visible pickup/prefab convention uses `CopperOre`; the future manifest must bind that alias explicitly so audits do not invent a missing item asset.

`ResourceDistributionBootstrapAuthoring` targets `Assets/_Project/Prefabs/Resources/Nodes/PFB_Ore_Generic.prefab` and `PFB_Ore_MagmaVentMarker.prefab`. `Assets/_Project/Prefabs/Resources/Nodes` is missing. Resource node data/templates exist for several biological/scrap sources, but no production ore or magma vent visual source package was found.

`WorldProceduralInteriorColonyFinalAuthoring` targets `Assets/_Project/Prefabs/Construction/Final/InteriorColony`, which is missing. `1855` identifies `Assets/ScifiFacility/Models` as the strongest hard-surface source kit candidate for construction replacements, but no InteriorColony package, manifest, collider proof, or render proof exists.

Generated kelp source assets exist under `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp`. `1858` classifies them as source candidates only: manifests, named proof, and visual captures are missing. They may inform `FiberKelp`, not unblock it.

## Required Source Package Law

Each source must ship as an authored or offline-generated package, not as a factory primitive:

- `PFB_*_Source.prefab` under the source convention named in the matrix.
- Persistent `MESH_*_LOD0`, `MESH_*_LOD1`, `MESH_*_LOD2`, and HLOD/impostor where scale demands it.
- Shared `MAT_*` material assets and `TX_*_Albedo`, `TX_*_Normal`, `TX_*_MRAO`, optional `TX_*_Emission/Detail`.
- MRAO channel semantics declared per shader. Root docs use MRAO as Metallic/Roughness-or-Smoothness/AO/Emission-or-family-mask; the exact G/A interpretation must be manifest-owned, not guessed.
- `VIS_*` children for render meshes and `COL_*` children for collision/proxies. LOD0 visual mesh as production `MeshCollider` is rejected.
- `LODGroup` or documented HLOD/impostor route with dithered fade/hysteresis.
- Manifest and proof artifacts before menu unblock.

## Proof Artifact Requirements

Minimum proof per source:

- Local manifest: `MANIFEST_<assetStem>.md` or `.json`.
- Mesh validation: finite vertices, no degenerate triangles, normals/tangents, UV density, submesh/material slot match, triangle counts.
- Material/texture import report: sRGB/linear roles, normal map role, compression, mip/streaming settings, MRAO channel map, SRP Batcher/instancing note.
- Collider/proxy report: `COL_*` paths, primitive/convex counts, trigger roles, explicit no-visual-MeshCollider proof.
- LOD/HLOD report: LOD0/1/2/HLOD paths, budgets, preserved anchors/sockets/ore seams, dither/hysteresis policy.
- Static primitive scan: no visible Unity built-in primitive mesh GUIDs in production visual mesh filters.
- Render proof when Unity is allowed later: flat material, final material, wire/LOD/collider overlay, compact capture, high capture, route/gameplay-distance capture.
- Source provenance: original FBX/generated mesh/source prefab path and invalid proxy/placeholder rejection.

Static text cannot clear `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`, runtime cost, import correctness, or profiler/GC claims.

## Factory-Specific Requirements

### PowerGridPrefabFactory

Required source root: `Assets/_Project/Prefabs/Construction/Power/Sources`.

Each baseline source group must provide real visual source prefabs for Reactor, RTG, Battery, Relay, Breaker, and Junction. Names must allow deterministic type classification or include metadata that maps to `PowerNodeTypeID`. The source must preserve runtime metadata needs: power node type, port/connector transforms, breaker handle transforms, battery capacity where applicable, collision proxy, and interaction trigger. A warning log is not a gate. Missing source keeps the menu blocked.

### ResourceWorldBootstrapAuthoring

Required source root: `Assets/_Project/Prefabs/Resources/Sources`.

World pickups are resource identity objects, not abstract currency. Each pickup must link to its `Data_*` item asset, use dumb proxy presentation, and keep data truth in item templates/registries. Biological resources require organic material identity; mineral resources require geology/mineral process identity; salvage resources require manufactured damage and weight. Existing primitive pickup prefabs remain blocked.

### ResourceDistributionBootstrapAuthoring

Required source root: `Assets/_Project/Prefabs/Resources/Nodes/Sources`.

Ore and vent nodes are world/route objects, not marker pins. `Ore_Generic` needs an authored ore outcrop package with harvest seam readability. `Ore_MagmaVentMarker` needs a hydrothermal vent visual source with chimney/mineral/hazard readability and named sockets for future VFX/hazard routes. Geology source meshes can be used only after material, collider, manifest, and route captures exist.

### WorldProceduralInteriorColonyFinalAuthoring

Required source root: `Assets/_Project/Prefabs/Construction/Final/InteriorColony/Sources`.

Interior and colony final packages must read as pressure-rated industrial habitat equipment. ScifiFacility FBX/models are valid kitbash candidates only after sanitizing primitive-contaminated prefabs, repackaging under HECTON-8 naming, adding LODs/HLODs, materials, collider proxies, sockets/triggers, and proof. The old composite primitive authoring path remains blocked.

## Existing Candidate Sources

Accepted as candidates only:

- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp`: candidate for `FiberKelp` source shape language. Blocked by missing manifests and named proof per `1858`.
- `Assets/_Project/Art/Meshes/WorldProceduralGeology`: candidate language for ore/vent host rock only. `1858` says source-only, no material/prefab/proof acceptance.
- `Assets/ScifiFacility/Models`: candidate hard-surface kit for power/interior/colony. Must be sanitized and repackaged; direct substitution is not approved.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_*`: material reference candidates for family style only; proxy material existence is not final material proof.

Rejected as final visible sources:

- `Assets/_Project/Prefabs/WorldProceduralProxy/*`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/*`.
- Existing primitive `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_*`.
- Collision-only proxies, debug markers, scene-only placeholders, icon sprites, data assets, recipes, and runtime scripts.

## Why Primitive Fallbacks Stay Prohibited

Cube/cylinder/sphere fallback violates all three acceptance pillars:

- Graphics: primitive silhouettes expose programmer blockout, cannot carry pressure machinery, mineral fracture, organic tissue, or colony habitat material identity.
- Optimization: primitive art wastes review time and later replacement labor; stronger lighting and high-tier rendering make the cheap geometry more visible.
- Gameplay: player decisions around power, resources, salvage, hazards, docking, and infrastructure need readable physical identity. Abstract shapes do not communicate source, risk, failure, or route value.

The menu stays blocked until real sources and proof exist.

## Scaling Consequences

All future source packages must consume continuous `GlobalQualityWeight`; Low/Middle/High/Ultra are documentation checkpoints, not binary branches.

- Low/compact: preserve silhouette, material identity, route/resource/power read, simple collision, LOD2/HLOD, shared materials, no ugly mode.
- Middle: add density, labels, sockets, decals, stronger material breakup, longer LOD1.
- High: add wetness, corrosion, mineral vein normals, richer organic fold/detail, longer LOD0.
- Ultra: spend saved budget on bolts, cables, micro scratches, scars, richer masks, HLOD residency, route composition density. Gameplay truth, item identity, collider authority, DTO layout, and save identity remain unchanged.

## Verification

Claim: required source paths and blocked route names were identified from static source/docs.  
Evidence Class: STATIC_SOURCE, STATIC_DOC.  
Artifact: this report and `1866_POWER_RESOURCE_SOURCE_MATRIX.csv`.  
Command or tool: `rg` and `Get-ChildItem` static reads only.  
Date: 2026-06-04.  
Residual risk: no Unity/import/render/profiler proof.

Claim: no production source package is accepted by this packet.  
Evidence Class: STATIC_DOC.  
Artifact: this report.  
Command or tool: static review of `1851`, `1855`, `1858`, `1860`, `1861`.  
Date: 2026-06-04.  
Residual risk: future Unity-side artifact work may prove some candidates later.

## Result

State: PACKET_COMPLETE_STATIC_REQUIREMENTS.

No source, prefab, asset, scene, binary, importer, bake, Unity, screenshot, profiler, or `.meta` work was performed.
