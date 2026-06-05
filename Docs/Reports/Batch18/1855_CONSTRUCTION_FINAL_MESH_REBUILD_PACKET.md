# 1855 Construction Final Mesh Rebuild Packet

Agent ID: 1855
Evidence class: STATIC_SOURCE
Runtime/build/import proof: PENDING VERIFICATION
Unity/build/importer/bake/DataMonolith execution: NOT RUN
Mutation boundary: no prefabs, assets, source, scenes, binaries, or `.meta` edited.

## Result

The 10 `Construction/Final` primitive blockers are confirmed as production replacement targets. Current evidence is static YAML/text and static source inspection only. This packet defines the rebuild packages, valid source candidates, invalid candidates, downstream gameplay contracts, output route, and proof gates needed before any future agent mutates Unity assets.

This packet does not unblock `ConstructionBootstrapAuthoring.RebuildStarterConstructionKit`, does not approve `GameObject.CreatePrimitive` finals, and does not treat collision proxies as visible production art.

## Authorities Read

- Root: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`.
- Domain: `construction.md`, `world.md`, `terrain.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `3DMODEL_HARD_SURFACE_MODULES.md`, `3DMODEL_EQUIPMENT_PROPS.md`.
- Batch reports: `1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`, `1852_PROCEDURAL_PLACEHOLDER_FINAL_GATE.md`, `1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`.
- Source/data: `ConstructionBootstrapAuthoring.cs`, `WreckagePrefabFactory.cs`, construction/family assets, module/buildable consumers, `Assets/ScifiFacility/Prefabs`, `Assets/ScifiFacility/Models`.
- Mandates: `TOOL_Procedural_Wreckage_Generator`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory`, `QA_Evidence_Text_Filter_Audit`.

## Primitive Blocker Inventory

Unity built-in mesh GUID scanned: `0000000000000000e000000000000000`.

| Prefab | Family/buildable link | Primitive refs | Current visible role |
| --- | --- | ---: | --- |
| `PFB_Debris_WreckField.prefab` | `family.debris.field.final.wreck_field` | 12 total: 6 Cube, 3 Cylinder, 3 Capsule | Wreck-field carrier with hull chunks, pipe run, service plate, scavenger/perch silhouettes. |
| `PFB_Debris_ScrapCluster.prefab` | `family.debris.scatter.final.scrap_cluster` | 9 total: 4 Cube, 2 Cylinder, 3 Capsule | Small scrap/salvage affordance cluster. |
| `PFB_Module_Pylon.prefab` | `family.route.power.final.pylon`, `Build_Utility_Pylon` | 1 total: 1 Cylinder | Utility/power route pylon. |
| `PFB_Module_CurrentTurbine.prefab` | `family.route.power.final.current_turbine`, `Build_Current_Turbine` | 1 total: 1 Cylinder | Current power source, `powerRating=18`, `powerPriority=15`. |
| `PFB_Ruin_ClusterMedium.prefab` | `family.ruin.cluster.medium.final.cluster_medium` | 16 total: 9 Cube, 3 Cylinder, 3 Capsule, 1 Quad | Medium abandoned module cluster and route landmark. |
| `PFB_Ruin_Megastructure.prefab` | `family.ruin.megastructure.final.megastructure` | 23 total: 15 Cube, 2 Cylinder, 4 Capsule, 2 Quad | Large landmark silhouette with bridge/core/ring/proxy schools. |
| `PFB_Module_Foundation.prefab` | `family.ruin.module.single.final.foundation`, `Build_Foundation_Platform` | 9 total: 9 Cube | Buildable/ruin foundation body with sockets and interior trigger. |
| `PFB_Module_Corridor.prefab` | `family.ruin.module.single.final.corridor`, `Build_Corridor_Straight` | 9 total: 9 Cube | Buildable/ruin corridor body with sockets and interior trigger. |
| `PFB_Module_ServicePump.prefab` | `family.service.scar.final.service_pump`, `Build_Service_Pump` | 1 total: 1 Cube | Service scar pump/casing, `powerRating=-8`, `powerPriority=20`. |
| `PFB_SargassumCollapseChunk.prefab` | No scanned family link in required family assets | 1 total: 1 Cube | Collapse/debris chunk in `Construction/Final`; either classify out of final set or rebuild as non-primitive chunk. |

## Material And Template Support

Construction materials present for future package work include `Mat_Module_Corridor`, `Mat_Module_Foundation`, `Mat_Module_Pylon`, `Mat_Module_CurrentTurbine`, `Mat_Module_ServicePump`, `MAT_Equipment_Atlas`, `Mat_LeakWetSheen`, and `Mat_RuinSeepSheen`. These are support candidates, not final proof by themselves.

Construction buildables with current final prefab links:

- `Build_Corridor_Straight` -> `PFB_Module_Corridor`, `powerRating=-6`, `powerPriority=35`.
- `Build_Foundation_Platform` -> `PFB_Module_Foundation`, `powerRating=0`, `powerPriority=25`.
- `Build_Current_Turbine` -> `PFB_Module_CurrentTurbine`, `powerRating=18`, `powerPriority=15`.
- `Build_Service_Pump` -> `PFB_Module_ServicePump`, `powerRating=-8`, `powerPriority=20`.
- `Build_Utility_Pylon` -> `PFB_Module_Pylon`, `powerRating=0`, `powerPriority=40`.

Template sources present under `Assets/_Project/Data/Construction/StandardModuleTemplates` and `Assets/_Project/Data/Construction/AbandonedModuleTemplates` define sockets, proxy bounds, role flags, integrity/flood thresholds, air volume, drag/breach/buoyancy data, and VFX sockets. Replacement visuals must not change those gameplay truth values unless a separate construction design task owns that change.

## Valid Non-Primitive Candidate Sources

`Assets/ScifiFacility/Models` is the strongest available non-primitive source tree for hard-surface construction replacements. Static inventory found 282 `.fbx` files across structural walls, floors, ceilings, trims, rails, scaffolds, stairs, props, tubes, lights, server racks, furniture, decals, control panels, and pipe/technical details.

`Assets/ScifiFacility/Prefabs` is a packaging/reference candidate tree with 255 `.prefab` files. It can provide layout examples, material assignments, detail density, and source mesh references. It is not approved for direct final substitution without construction-specific packaging, socket preservation, collision authoring, LODs, and proof gates.

Valid ScifiFacility source classes:

- Structural walls, columns, connectors, hull/viewing deck pieces: usable for corridor/foundation/ruin module shells.
- Floors, floor borders, ducts, ceilings, trims: usable for module ribs, flanges, gaskets, panel seams, and broken ruin slices.
- Rails, scaffolds, walkways, stairs: usable for megastructure frames, exposed ruin frames, and service platforms after primitive-contaminated prefabs are excluded or sanitized.
- Props, tubes, control panels, server racks, lights, decals: usable for service pump details, pylon cable sockets, turbine support details, salvage-readable affordances, and ruin interior dressing.
- Decal prefabs/models: usable as authored overlay source only after material/render queue proof; transparent spam is not accepted as proof of mesh quality.

`Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs` is a valid future editor-only assembler candidate for wreck/debris packages if real source meshes exist. It discovers source prefabs/meshes, classifies `COL_` collision proxies, separates debris by name tokens, combines hull/debris meshes with `Mesh.CombineMeshes`, persists combined meshes, attaches `VoxelCarveVolume`, `EquipmentMetadata`, `WreckageScatterManager`, and validates material/collision contracts. It refuses fallback material creation when the required wreck material set is missing.

Current blocker for that factory route: `Assets/_Project/BakedGeometry/Wreckage` exists only as `.meta` under static filesystem inspection. No real hull/debris/COL source files were present there. `Assets/Prefabs/Environment/Wrecks` was not present with files in static inspection.

## Invalid Candidates

These are not valid visible final art sources:

- `Assets/_Project/Prefabs/WorldProceduralProxy/*`, including debris, ruin, route power, service scar, and sargassum proxy prefabs. Prior batch reports and current doctrine reject them as primitive/proxy neighborhoods.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Construction/*` and `.../Debris/*`.
- Current `Assets/_Project/Prefabs/Construction/Final/*` primitive blockers.
- Resource pickup prefabs such as `PFB_Resource_TitaniumScrap`.
- Invisible collision proxies, gameplay support prefabs, scanner markers, school/perch markers, or world support zones as visible production art.
- ScifiFacility primitive-contaminated prefabs until sanitized: `Assets/ScifiFacility/Prefabs/structural/rails+scaffolds+stairs/stairs_01.prefab` and `Assets/ScifiFacility/Prefabs/structural/walls/wall_01_4x3_door_02.prefab` each contain a built-in Cube mesh ref.

## Authoring Pattern To Avoid

`ConstructionBootstrapAuthoring.RebuildStarterConstructionKit()` is an old primitive-composite path. It currently uses a legacy primitive final authoring gate and then creates visuals from `PrimitiveType.Cube`, `PrimitiveType.Cylinder`, `PrimitiveType.Capsule`, and `PrimitiveType.Quad` through helper methods such as `CreateFinalPrefab`, `CreateCompositeFinalPrefab`, `BuildCompositeVisuals`, and `CreateVisualPrimitive`.

Future replacement work must not relax this gate or treat those generated primitive composites as final art. That code can inform names/component expectations only.

## Downstream Contracts

Replacement packages must preserve these contracts:

- Existing prefab path/GUID references unless a scoped relink plan updates every `ProceduralFamily`, `BuildableData`, catalog alias, and save/load reference.
- `ModuleMarker` and `BuildableData.PersistentId` identity, because save/load and catalog resolution use them.
- `BaseModuleTemplate` socket definitions, proxy bounds, structural role flags, air volume, flood/integrity thresholds, drag/breach/buoyancy/COM data, and VFX sockets.
- `Socket_*` child transforms used by builder placement and module connections.
- `InteriorTrigger` on buildable corridor/foundation style modules. Trigger must remain assigned and `isTrigger=true`.
- `PowerNode` and power metadata on pylon/turbine/service pump packages where applicable.
- Collision must be authored as simple proxy/compound collision. Visible MeshCollider roots are rejected.
- Read accessors and hot runtime routes must remain pure. No hot scene search, no runtime mesh generation, no material clone path.

## Rebuild Package Specs

### Wreck Field

Build an authored/generated package from real hull and debris source meshes:

- Torn hull plates with readable thickness, ripped ribs, exposed pipes, service plates, salvage cuts, and broken frame silhouettes.
- Mesh groups: `VIS_HullCombined`, `VIS_DebrisScatter`, `COL_WreckField`.
- LOD0 with fractured hull detail, LOD1 simplified silhouette and major ribs, LOD2/HLOD compact recognizable wreck mass.
- Collision: authored convex/box/capsule compound or single convex proxy, no visible root MeshCollider.
- Material slots: exterior pressure metal, burned/exposed interior, debris/scrap, optional wet/sheen/emissive damage.
- Salvage anchors and scanner metadata must be authored or generated deterministically.

### Scrap Cluster

Build a compact salvage cluster:

- Beveled plates, cut pipes, broken cable glands, bolt heads, fasteners, small brackets, and readable salvage affordance points.
- Avoid random primitive piles. Silhouette must read as manufactured wreckage at distance.
- Share wreckage atlas/material slots with wreck field where possible.
- LOD2 must retain non-flat scrap silhouette on weak devices.

### Pylon

Build a pressure-rated utility support:

- Weighted base, bolted floor/terrain anchor, vertical service spine, clamp bands, insulators, cable socket mounts, hatch/panel detail.
- Preserve pylon power/buildable identity and any `PowerNode`/placement metadata.
- Collision: base/body compound, not visual mesh collision.
- Sockets: cable/socket anchors must be named and stable for future route tools.

### Current Turbine

Build a non-primitive power generator:

- Shroud/ring, rotor blades, central hub, radial struts, rear service housing, cable anchors, maintenance bolts, warning/emissive markers.
- Animation/proof route: prefer static authored rotor plus optional cheap transform or shader fake. No runtime mesh deformation. Any motion must be visual-only and not own gameplay truth.
- Compact device route keeps shroud/hub/blade silhouette; high/ultra adds blade bevels, wetness, cables, and secondary decal detail.

### Medium Ruin Cluster

Build a cluster of abandoned modules:

- Broken module shells, torn sockets, corrosion, exposed interior frames, fractured braces, route gaps, and believable scale breaks.
- Keep `family.ruin.cluster.medium` identity and route landmark readability.
- Do not use proxy fish/school/perch silhouettes as structural art. If ecological markers remain, they must be separate non-art support nodes.
- LOD/HLOD must preserve abandoned-module read at route distances.

### Megastructure

Build a landmark package:

- Large ring/core/bridge/frame composition with internal voids, exposed frames, broken decks, collapsed side frames, and corrosion/wet seep detail.
- Split source meshes by HLOD cluster: silhouette shell, frame set, bridge set, close detail set.
- Impostor/HLOD route required before final acceptance. The silhouette must remain readable without hiding behind darkness, fog, or scale alone.
- Collision is navigational/occlusion proxy only, not per-detail collision.

### Foundation

Build a pressure foundation:

- Pressure deck shell, floor ribs, bolted flanges, gasket bands, socket frames, underside bracing, worn edge panels, clear build grid read.
- Preserve `Build_Foundation_Platform`, `ModuleMarker`, `Socket_*`, `InteriorTrigger`, and template proxy bounds.
- Low tier keeps silhouette, sockets, flanges, and readable paneling. Ultra tier adds bolts, gasket material breakup, underside bracing, decals, and wetness.

### Corridor

Build a pressure corridor:

- Rounded/segmented pressure shell, reinforced ribs, gasket rings, socket collars, panel seams, access plates, interior trigger volume preservation.
- Preserve corridor snap points and placement expectations.
- Collision must match placement footprint and interior access, not every visible rib.

### Service Pump

Build a service scar pump:

- Pump casing, intake/outflow ports, bolted panels, gauges/control surface, pipe couplers, maintenance handles, warning/emissive details.
- Preserve `Build_Service_Pump`, `powerRating=-8`, `powerPriority=20`, and any powered/module metadata.
- Must not remain a cube with material.

### Sargassum Collapse Chunk

Resolve before final acceptance:

- If it is a world/procedural/sargassum support chunk, move ownership out of `Construction/Final` in a separate scoped task.
- If it remains a Construction/Final visible chunk, rebuild as non-primitive collapsed organic/structural mass with authored material and HLOD/collision proof.

## Future Source Route And Naming

Preferred future route:

1. Use ScifiFacility FBX models and sanitized prefabs as source kits for hard-surface modules, pylons, turbine, pump, and ruin structures.
2. Use `WreckagePrefabFactory` only for wreck/debris packages after a real source set exists under `Assets/_Project/BakedGeometry/Wreckage` or a newly approved equivalent folder.
3. Replace internals at existing final prefab paths to preserve GUID references, or produce new prefabs only with a complete relink plan for family assets, buildables, catalog aliases, and save/load identity.
4. Keep all generators/editor tools offline. Final runtime prefabs must contain authored meshes, LODs/HLODs, materials, collision proxies, sockets, and metadata.

Required output conventions for the future mutating task:

- Prefabs: keep existing `Assets/_Project/Prefabs/Construction/Final/PFB_*` paths for GUID safety where possible.
- Visual children: `VIS_<Package>_<Role>`, for example `VIS_WreckField_HullCombined`, `VIS_CurrentTurbine_Rotor`.
- Collision children: `COL_<Package>_<Role>`.
- Socket children: retain or add stable `Socket_*` names matching `BaseModuleTemplate` expectations.
- Mesh assets: `Assets/_Project/Art/Meshes/Construction/Final/MESH_Construction_<Package>_<Variant>_LOD0.asset`, `_LOD1.asset`, `_LOD2.asset`.
- HLOD/impostor assets: `Assets/_Project/Art/Meshes/Construction/HLOD/MESH_Construction_<Package>_<Variant>_HLOD.asset`.
- Materials: `Assets/_Project/Art/Materials/Construction/MAT_Construction_<Role>.mat`.
- Textures/atlases: `Assets/_Project/Art/Textures/Construction/TX_Construction_<Atlas>_<Channel>.png`.
- Proof reports: `Docs/Reports/GeneratedAssets/Construction/<PrefabName>_PROOF.md` or equivalent batch report path.

## Proof Gates

No replacement is Final until all gates pass:

- Static primitive scan: no visible `MeshFilter` references to Unity built-in primitive meshes. Collision-only primitive colliders are allowed when named under `COL_*`.
- Source provenance: source mesh/prefab path listed, invalid proxy/placeholder/resource source rejected.
- Material proof: no fallback material creation, no `renderer.material`, no runtime material clones, max approved slots per package, SRP Batcher-compatible shader proof.
- LOD proof: LOD0, LOD1, LOD2 and HLOD/impostor policy documented with triangle/material/renderer counts.
- Collision proof: simple compound or convex proxy, no visible root MeshCollider, placement footprint validated.
- Compatibility proof: `ModuleMarker`, `BuildableData`, `BaseModuleTemplate`, `Socket_*`, `InteriorTrigger`, `PowerNode`, family variant refs, and catalog/save/load identity checked.
- Screenshot/render proof: beauty, flat material, wireframe, collider overlay, and far-distance/HLOD view. Darkness/fog cannot be used to hide weak art.
- Performance proof: renderer/material counts and memory/VRAM estimate recorded. Any runtime animation or update path above suspicion threshold requires profiler proof.
- Continuous quality proof: all visual scaling consumes continuous `GlobalQualityWeight`; it must not change gameplay truth, DTO layout, save identity, or authority route.

## Scalability Consequences

- Compact: shared atlas, early LOD2/HLOD, simple collision, retained silhouette/readability, no flat placeholder geometry.
- Middle: LOD1 held longer, more material breakup, limited decals, stable sockets/colliders unchanged.
- High: richer bevels, corrosion, wetness, pipe/cable detail, longer LOD0 range, optional cheap visual-only turbine motion.
- Ultra: extra bolts, secondary cables, dense decals, extended HLOD range, stronger wreck/ruin interior breakup. Gameplay truth and save identity remain identical.

## Evidence Limits

This packet is complete as STATIC_SOURCE evidence. No Unity import state, prefab previews, scene validation, screenshots, runtime profiler data, or build results were produced. Future production replacement remains blocked until a mutating asset task runs the proof gates above.
