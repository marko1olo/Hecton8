# 1854 World Support Visible Carrier Replacement Packet

Evidence class: STATIC_SOURCE
Date: 2026-06-04

## Result

This packet removes the planning blocker for replacing the nine WorldSupport visible primitive carrier prefabs. It does not implement or relink replacements.

Runtime proof: PENDING.
Unity editor validation: PENDING.
Screenshot proof: PENDING.
Profiler, Frame Debugger, GC, and residency proof: PENDING.
Production replacement prefabs: PENDING.

## Scope Boundary

Owned writes:
- `Docs/Tasks/Status_1854.md`
- `Docs/AgentLogs/Rationale_1854.md`
- `Docs/AgentLogs/LOG_1854.md`
- `Docs/Reports/Batch18/1854_WORLD_SUPPORT_VISIBLE_CARRIER_REPLACEMENT_PACKET.md`
- `Docs/Reports/Batch18/1854_WORLD_SUPPORT_REPLACEMENT_MATRIX.csv`

No Unity, PlayMode, builds, importers, bakes, validators, source edits, prefab edits, asset edits, scene edits, binaries, or meta files were touched.

## Authority Sources

Root and domain files read:
- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `terrain.md`
- `water.md`
- `creatures.md`
- `vfx.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`

Mandates read:
- `QA_Evidence_Text_Filter_Audit.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `REND_Instanced_Flora_Physics.txt`
- `REND_Terrain_VirtualTexturing.txt`
- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `TOOL_Procedural_Wreckage_Generator.txt`

Prior report sources read:
- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1852_PROCEDURAL_PLACEHOLDER_FINAL_GATE.md`
- `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`

## Static Findings

The nine blocked families are valid support concepts, but their current final variant prefabs are visible built-in primitive mesh compositions. The replacement route is not to delete the support role. The route is to separate hidden gameplay truth from visible art carriers.

Gameplay truth remains in hidden support objects:
- spawn point
- zone ownership or trigger volume
- resource pocket truth
- hazard pocket truth
- safe pocket truth
- spacing, cluster, and heatmap metadata from family assets

Visible carrier art must become authored or offline-generated production assets:
- no visible built-in cube, sphere, capsule, cylinder, quad, plane, or primitive mesh
- no placeholder materials
- no WorldProceduralProxy production reuse
- no AI proxy object reuse
- no current primitive finals preserved as visible art
- no final-ready claim without manifests, screenshots, validators, and runtime proof

## Blocker Matrix

| Family | Current final prefab | Built-in primitive refs | Current visible primitive children | Required replacement direction |
|---|---|---:|---|---|
| `family.creature.spawn.passive` | `PFB_Support_CreatureSpawn_Passive.prefab` | 11 | `FryA`, `SpawnVisitor`, `BeaconA`, `BeaconC`, `SpawnMass`, `SpawnRing`, `SpawnSilhouette`, `BeaconB`, `FryB` | Hidden school/spawn marker plus reef or kelp nursery carrier |
| `family.creature.spawn.predator` | `PFB_Support_CreatureSpawn_Predator.prefab` | 12 | `ToothD`, `ScoutPerch`, `NestCore`, `ToothB`, `ToothC`, `ScoutA`, `ToothA`, `ScoutB`, `PredatorSilhouette`, `NestMass` | Hidden predator spawn marker plus lair/perch geology carrier |
| `family.creature.zone.abyss_apex` | `PFB_Support_Zone_AbyssApex.prefab` | 12 | `AbyssSilhouette`, `Mass`, `Halo`, `Base`, `FinB`, `WatcherA`, `WatcherSilhouette`, `CrossFin`, `Monolith`, `WatcherB`, `FinA` | Hidden apex zone ownership plus abyss landmark carrier |
| `family.creature.zone.large_threat` | `PFB_Support_Zone_LargeThreat.prefab` | 12 | `Base`, `ArchB`, `ArchMass`, `Ring`, `Spine`, `ArchA`, `SentryA`, `SentryB`, `SentrySilhouette`, `ZoneSpine`, `ZoneSilhouette` | Hidden threat zone plus territorial warning/perch carrier |
| `family.creature.zone.reef_apex` | `PFB_Support_Zone_ReefApex.prefab` | 12 | `StemB`, `ReefSilhouette`, `Base`, `StemA`, `StemC`, `Canopy`, `StemMass`, `DriftVisitorA`, `DriftVisitorB`, `CanopyVisitor` | Hidden reef apex ownership plus premium coral canopy carrier |
| `family.creature.zone.ruin_apex` | `PFB_Support_Zone_RuinApex.prefab` | 12 | `Nest`, `FrameB`, `RuinSilhouette`, `PerchB`, `PerchA`, `FrameMass`, `Anchor`, `CrossSpan`, `Base`, `NestSentinel`, `ThreatNest`, `FrameA` | Hidden ruin apex ownership plus wreck/ruin perch carrier |
| `family.pocket.hazard` | `PFB_Support_Pocket_Hazard.prefab` | 15 | `SpineB`, `ParasiteA`, `ParasiteB`, `PredatorPerch`, `VentMass`, `SpineD`, `SpineA`, `HazardSilhouette`, `VentSheen_Secondary`, `VentSheen_LOD1`, `VentSheen_Main`, `VentCore`, `SpineC` | Hidden hazard pocket plus vent chimney or toxic ecology carrier |
| `family.pocket.resource` | `PFB_Support_Pocket_Resource.prefab` | 12 | `ForagerB`, `Silhouette`, `MassB`, `BaseChunkA`, `ShardB`, `MassA`, `ForagerSilhouette`, `CoreShard`, `MassC`, `BaseChunkB`, `ShardA`, `ForagerA` | Hidden resource pocket plus deposit/cache ecology carrier |
| `family.pocket.safe` | `PFB_Support_Pocket_Safe.prefab` | 11 | `Support`, `VisitorA`, `VisitorB`, `VisitorSilhouette`, `ShelterArchB`, `Base`, `Canopy`, `ShelterArch`, `SafeSilhouette` | Hidden safe pocket plus shelter arch/canopy carrier |

## Candidate Inventory

### Valid Candidate Sources, Not Drop-In

`Assets/_Project/Prefabs/Nature/Flora/Baked` exists and contains baked flora/coral/kelp candidates:
- coral branching: `GEN_family_coral_branching__bouquet`, `__branch`, `__crest`, `__fan`, `__mass`, `__thicket`
- coral brittle: `GEN_family_coral_brittle__candelabra`, `__cathedral`, `__crown`, `__fan`, `__halo`, `__lace`, `__spire`, `__sprig`, `__thicket`, `__wreath`
- coral low: `GEN_family_coral_low__bed`, `__knoll`, `__mound`, `__plate`, `__saucer`, `__spread`
- coral massive: `GEN_family_coral_massive__boulder`, `__buttress`, `__dome`, `__head`, `__lobed`, `__porous`
- coral plate: `GEN_family_coral_plate__bastion`, `__canopy`, `__ledge`, `__shelf`, `__stack`, `__terrace`
- kelp abyssal/canopy/patch/tall families, including abyssal cathedral, cowl, lantern, mantle, nodule, and braid forms

Use: visual carrier ingredients for passive spawn, reef apex, safe pocket, resource pocket, and hazard ecology trim after proof.
Limit: prior 1851 warnings show manifest/proof gaps. Static source only is not final proof.

`Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows` exists and contains shallow kelp, tube coral, and porous rock candidates.
Use: shallow route support carriers and nursery silhouettes.
Limit: not final support replacements until manifest, material, collider, LOD, and screenshot proof exists.

`Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` exists and contains procedural geology candidates:
- `PFB_Geo_CaveEntrance_00..05`
- `PFB_Geo_Cave_Entrance`
- `PFB_Geo_LandmarkSpire_00..05`
- `PFB_Geo_Landmark_Spire`
- `PFB_Geo_RockArch_00..05`
- `PFB_Geo_RockArch_Large`
- `PFB_Geo_RockCluster_00..09`
- `PFB_Geo_RockFloor_00..08`
- `PFB_Geo_RockShelf_*`

Use: predator lair, large threat territory, abyss landmark, safe shelter, resource pocket, and hazard vent bases.
Limit: still requires carrier-specific composition, proof, and no visible primitive leakage.

`Assets/_Project/Prefabs/Nature/Rocks/Baked/Baked_Kucha_01.prefab` exists.
Use: possible geology ingredient only.
Limit: not enough static proof to promote as a complete support carrier.

`Assets/_Project/Art/TEXTURES/WorldProceduralFlora` contains coral/kelp base, detail, normal, mask, atlas, ORM, and MatCap assets.
Use: material source candidates for coral and kelp support carriers.
Limit: material route must define final shader slots and import proof. Existing placeholder materials are not acceptable.

### Builder Sources

`WorldProceduralCoralMeshBuilder.cs` exists.
- Editor namespace: `Hecton8.EditorTools`
- Mesh data route: `Mesh.AllocateWritableMeshData`
- Vertex attributes: Position, Normal, Tangent, Color, TexCoord0
- LOD clamp: 0 to 2
- Supported roots include coral low, branching, massive, and plate variants

Use: source pattern for offline/editor support carrier mesh authoring.
Limit: managed editor lists and editor-only construction cannot be runtime gameplay generation.

`WorldProceduralSeaweedMeshBuilder.cs` exists.
- Editor namespace: `Hecton8.EditorTools`
- Mesh data route: `Mesh.AllocateWritableMeshData`
- Vertex attributes: Position, Normal, Tangent, Color, TexCoord0
- LOD clamp: 0 to 3
- Builds holdfast, stipe, blades, and bulbs for kelp/seaweed families

Use: source pattern for offline/editor kelp and seaweed carrier ingredients.
Limit: not a production replacement packet by itself.

## Invalid Candidates

The following are explicitly invalid for production replacement:
- `Assets/_Project/Prefabs/WorldProceduralProxy`
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- current `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_*.prefab` visible primitive meshes
- AI proxy objects and AI-only markers such as hunter, leviathan, or creature-debug proxies
- `Assets/_Project/Art/TEXTURES/Detali/bubble vent atlas - bad - redo.png`
- any visible carrier that keeps built-in primitive mesh fileIDs as art

## Replacement Specs By Family

### Passive Creature Spawn

Hidden truth:
- spawn anchor
- school radius and visitor route metadata
- family heatmap read remains `fauna_density`

Visible carrier:
- shallow nursery reef, kelp fronds, branching coral, and small shelter holes
- visible schooling lure forms can be fish-scale glints, egg clusters, or current ribbons, not primitive marker beacons
- valid ingredients after proof: coral branching, coral low, coral plate, kelp canopy, kelp tall, shallow tube coral

Minimum production contents:
- `VIS_school_anchor` root
- at least LOD0, LOD1, LOD2 or documented HLOD replacement
- hidden `TRG_spawn_radius` or equivalent marker with renderer disabled or absent
- material slots for coral/kelp/biolum route accents
- screenshot proof in shallow/photic route light

### Predator Creature Spawn

Hidden truth:
- predator spawn anchor and leash/perch metadata
- no creature AI proxy object as visible carrier

Visible carrier:
- geology lair or cave-mouth perch
- scratch marks, tooth-like broken coral or rock splinters, silt scarring, carcass/debris evidence
- valid ingredients after proof: cave entrance, rock arch, rock cluster, rock shelf, coral brittle trim

Minimum production contents:
- `VIS_predator_lair`
- `COL_lair_proxy` hidden primitive or convex collider allowed
- material wear/fracture slot
- route cue readable before the player enters attack radius

### Abyss Apex Zone

Hidden truth:
- apex zone ownership and large radius metadata
- no visible primitive monolith, halo, fins, or watcher markers

Visible carrier:
- abyss landmark with geology mass, industrial ruin scar, or deep biolum sensor frame
- darkness cannot hide weak geometry; silhouette must read under low light and instrument light
- valid ingredients after proof: landmark spires, cave entrance pieces, large rock shelves, custom abyss emissive trim

Minimum production contents:
- `VIS_abyss_apex_landmark`
- no gameplay truth embedded in emissive art objects
- compact tier keeps silhouette and warning cues
- high and ultra tiers add micro-emissive, particulates, wetness, and route-scale detail

### Large Threat Zone

Hidden truth:
- large threat territory volume
- no AI or sentry proxy object visible as production art

Visible carrier:
- territorial arch, broken spine-like geology, perch shelf, and scrape/debris field
- visual language must warn of a large owner without spawning fake creatures
- valid ingredients after proof: large rock arch, rock shelf, landmark spire, fracture/wear decals

Minimum production contents:
- `VIS_large_threat_warning`
- hidden zone/truth root
- collider proxies separated from visuals
- route visibility proof before final link

### Reef Apex Zone

Hidden truth:
- reef apex zone ownership and bio-density routing

Visible carrier:
- premium bright reef apex canopy, branching coral crown, kelp drift, shelter pockets
- no noir-dark masking for shallow/photic routes
- valid ingredients after proof: coral plate canopy, coral branching thicket, coral massive boulder, kelp canopy

Minimum production contents:
- `VIS_reef_apex_canopy`
- material slots with coral, kelp, cavity AO, and controlled emissive accents
- screenshot proof in surface/photic light

### Ruin Apex Zone

Hidden truth:
- ruin apex ownership and route risk

Visible carrier:
- ruin frame or wreck perch integrated with rock/coral overgrowth
- primitive frames, cubes, quads, and AI markers are invalid
- valid ingredients after proof: generated construction/ruin meshes if available through proper generator proof, rock arch/shelf, coral overgrowth

Minimum production contents:
- `VIS_ruin_apex_perch`
- material slots for worn metal/stone, biological overgrowth, and hazard marks
- no AI proxy objects or construction debug proxy art

### Hazard Pocket

Hidden truth:
- hazard pocket location, radius, damage/pressure/toxic cause owner
- VFX is presentation, not gameplay truth

Visible carrier:
- vent chimney, parasite coral, tube-worm field, toxic mineral stain, heated water shimmer
- reject `bubble vent atlas - bad - redo.png`
- valid ingredients after proof: rock cluster, rock floor, brittle coral, low coral, dedicated vent mesh/material/VFX

Minimum production contents:
- `VIS_hazard_vent_cluster`
- `VFX_hazard_cause_bound` or equivalent pooled effect with cause owner proof
- dedicated material/texture proof for heat/mineral/toxic state
- hidden primitive colliders allowed only as triggers/proxies

### Resource Pocket

Hidden truth:
- resource pocket spawn/cache logic and resource heatmap
- visible art must not own inventory truth

Visible carrier:
- mineral cache, coral-encrusted deposit, sheltering rock pocket, forager traces
- valid ingredients after proof: rock floor, rock cluster, coral massive/low, mineral material set

Minimum production contents:
- `VIS_resource_cache`
- material slots for mineral/deposit, organic crust, wetness, and cavity AO
- collision/search proxies separated from art

### Safe Pocket

Hidden truth:
- safe pocket/shelter metadata and route respite role

Visible carrier:
- readable shelter arch, canopy, calm water, biological refuge, bright route cue
- surface/photic safe pockets must not use darkness to conceal weak art
- valid ingredients after proof: rock arch, kelp canopy, coral plate, coral low, rock shelf

Minimum production contents:
- `VIS_safe_shelter`
- readable from player approach distance
- compact tier preserves shelter silhouette and lighting cue
- high and ultra tiers add detail, current motion, fauna traces, and wetness

## Material, Texture, And Vertex Color Contract

Required material slots:
- Slot 0: primary geology or organic structure
- Slot 1: exposed wear, fracture, tube-worm, tooth, nest pad, or mineral face
- Slot 2: secondary coral, kelp, biological crust, or trim
- Slot 3: emissive, biolum, sensor, hazard, or route accent

Texture roles:
- albedo/base color
- normal, preferably BC5 for production normal maps where pipeline permits
- MRAO or ORM packed map, with channel semantics written in manifest
- emission or biolum mask where route requires it
- wetness/detail mask where water interaction requires it

Candidate source textures:
- `TX_Coral*`
- `TX_Kelp*`
- `TX_ProceduralBio_Shallows_AlbedoAtlas.png`
- `TX_ProceduralBio_Shallows_NormalAtlas.png`
- `TX_ProceduralBio_Shallows_ORMAtlas.png`

Placeholder materials in `WorldRuntime/ProceduralPlaceholders` are invalid for final support carriers.

Vertex color semantics must be declared per asset manifest:
- R: current sway, edge wear, hazard intensity, or fracture stress
- G: biolum phase, growth band, mineral pulse, or route cue
- B: ambient occlusion, cavity dirt, depth grime, or shelter mask
- A: wetness, stability, damage, or proof-family mask

No carrier may rely on undocumented vertex color meanings.

## Collider And Marker Policy

Visible art:
- must not use built-in primitive mesh references as presentation
- must not use LOD0 MeshCollider as production collision
- must include LOD/HLOD plan and material proof

Hidden support:
- may use primitives as trigger volumes or simple colliders when renderer is absent or disabled
- must keep gameplay truth on marker/owner components, not visual mesh children
- should use `COL_*` naming for collision proxies
- should use `TRG_*` or equivalent naming for support trigger volumes

## Future Authoring Route

Recommended future owner pass:
- `Assets/_Project/Scripts/Editor/WorldSupportCarrierAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldSupportCarrierValidator.cs`
- `Assets/_Project/Scripts/Editor/WorldSupportCarrierManifestWriter.cs`
- `Assets/_Project/Data/World/SupportCarriers/WorldSupportCarrierManifest_*.asset` or equivalent project-approved data format
- `Assets/_Project/Prefabs/WorldSupport/Generated/GEN_WS_*`
- replacement links into `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_*` only after proof
- `Assets/_Project/Art/Generated/WorldSupport/{Meshes,Textures,Materials}`
- proof report under `Docs/Reports/Batch18/` or the active batch report route

Authoring process:
1. Compose candidate carrier art from Flora/Baked, BioForge Shallows, ProceduralFinals rocks, and purpose-built support meshes.
2. Use coral and seaweed builder patterns only in editor/offline generation.
3. Write carrier manifest with family ID, variant ID, source ingredients, material slots, vertex color semantics, LOD chain, collider proxies, hidden marker roles, and proof links.
4. Validate no visible built-in primitive mesh refs remain.
5. Validate no proxy or placeholder production links.
6. Capture screenshots in relevant route light and depth conditions.
7. Run static validators and runtime proof in a later mutation-approved task.

## Validation Gates For A Later Implementation Task

Static gates:
- generated asset production audit reports zero primitive-final errors for the nine support carriers
- support family contract validator resolves all nine final variants
- final prefab quality gate finds no visible primitive mesh carriers
- YAML/source scan finds no `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` link in final support prefabs
- every replacement has manifest, material list, LOD chain, collider proxy list, and proof references

Unity/editor gates:
- prefabs open without missing scripts/materials/meshes
- hidden marker volumes remain active where gameplay needs them
- renderers are absent or disabled on hidden support primitives
- visible carrier roots render with intended materials
- LODGroup or HLOD path is present where required

Runtime gates:
- spawn, hazard, resource, and safe pocket gameplay truth still resolves from the family route
- no visual art object becomes the authority for gameplay truth
- no new managed allocation/GC path is introduced by carrier presentation
- VFX cause owners and pooling are proven for hazard pockets

Visual gates:
- surface/photic/medium-depth carriers meet project visual floor without darkness masking
- abyss carriers read as premium silhouettes under low light and instrument light
- compact, middle, high, and ultra fidelity captures preserve readability

Performance gates:
- profiler sample shows support carrier presentation does not exceed the local frame-time budget
- Frame Debugger confirms acceptable material/pass count
- residency/Addressables proof exists if carriers are streamed

## GlobalQualityWeight Consequences

Compact:
- simplified ornaments
- lower particle density
- lower shader feature set
- same carrier silhouette, marker separation, and route readability

Middle:
- normal LOD chain
- stable material set
- conservative route cues
- reduced but present VFX and current motion

High:
- LOD0 geometry and richer material response
- stronger biological/geology variation
- stronger biolum, wetness, and debris cues

Ultra:
- micro-detail, decals, local particle richness, layered wetness, and route-specific accent variation
- no change to gameplay truth ownership, DTO layout, save identity, or support authority route

## Acceptance

Accepted for task 1854:
- static blocker inventory
- candidate inventory including amended sources
- invalid shortcut list
- family-by-family replacement requirements
- material, texture, vertex color, collider, authoring, and validation packet
- CSV matrix

Not accepted or claimed:
- final production art
- prefab relinks
- runtime behavior
- screenshots
- profiler proof
- Unity validation

