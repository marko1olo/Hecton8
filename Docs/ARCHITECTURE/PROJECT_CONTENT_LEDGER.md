# PROJECT_CONTENT_LEDGER

STATUS: PENDING VERIFICATION  
OWNER: Resource Matrix / Geology  
SOURCE OF TRUTH: `Assets/_Project/Data/Scavenging/ResourceNodes/`  
MANDATES FOLLOWED:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `STRM_Persistent_Object_Registry.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

## Purpose

This ledger tracks stable authored identifiers and runtime `LocHash` values for the HECTON-8 resource-node matrix.
It is the canonical handoff table for item/resource/entity hash coordination until a broader project-wide uint registry is formalized.

## Entity HashIDs

| Template Asset | Stable ID | Entity HashID (int) | Hex | Item Stable ID | Item HashID (int) | Pickup Prefab |
|---|---|---:|---|---|---:|---|
| `ResourceNodeTemplate_TitaniumScrap.asset` | `resource.node.titanium_scrap` | 193749380 | `0x0B8C6184` | `Data_TitaniumScrap` | -783267794 | `PFB_Resource_TitaniumScrap.prefab` |
| `ResourceNodeTemplate_CopperVein.asset` | `resource.node.copper_vein` | 1609840767 | `0x5FF4387F` | `Data_Copper` | -2018628711 | `PFB_Resource_CopperOre.prefab` |
| `ResourceNodeTemplate_SilicaShardCluster.asset` | `resource.node.silica_shard_cluster` | 1506893700 | `0x59D15F84` | `Data_SilicaShards` | -374612680 | `PFB_Resource_SilicaShards.prefab` |
| `ResourceNodeTemplate_SilverVein.asset` | `resource.node.silver_vein` | -671736365 | `0xD7F61DD3` | `Data_SilverOre` | 884621125 | `PFB_Resource_SilverOre.prefab` |
| `ResourceNodeTemplate_SulfurVentClump.asset` | `resource.node.sulfur_vent_clump` | -1671792710 | `0x9C5A77BA` | `Data_SulfurClumps` | -948091731 | `PFB_Resource_SulfurClumps.prefab` |
| `ResourceNodeTemplate_FiberKelpStand.asset` | `resource.node.fiber_kelp_stand` | 1127860547 | `0x4339C943` | `Data_FiberKelp` | 2069849578 | `PFB_Resource_FiberKelp.prefab` |
| `ResourceNodeTemplate_HydrocarbonResinPod.asset` | `resource.node.hydrocarbon_resin_pod` | 1414759972 | `0x54538624` | `Data_HydrocarbonResin` | 248205602 | `PFB_Resource_HydrocarbonResin.prefab` |
| `ResourceNodeTemplate_MembraneTissueBloom.asset` | `resource.node.membrane_tissue_bloom` | 514678094 | `0x1EAD5D4E` | `Data_MembraneTissue` | 1610755912 | `PFB_Resource_MembraneTissue.prefab` |
| `ResourceNodeTemplate_GoldVein.asset` | `resource.node.gold_vein` | 829496924 | `0x31711E5C` | `Data_GoldOre` | -238158134 | `NONE` |
| `ResourceNodeTemplate_CobaltAlloyNodule.asset` | `resource.node.cobalt_alloy_nodule` | 672038734 | `0x280E7F4E` | `Data_CobaltAlloy` | 857583970 | `NONE` |
| `ResourceNodeTemplate_RareEarthDustBed.asset` | `resource.node.rare_earth_dust_bed` | -1329334677 | `0xB0C3F66B` | `Data_RareEarthDust` | 1997058338 | `NONE` |
| `ResourceNodeTemplate_ThermalGelPocket.asset` | `resource.node.thermal_gel_pocket` | -763320588 | `0xD280A6F4` | `Data_ThermalGel` | 1601052383 | `NONE` |
| `ResourceNodeTemplate_NickelVein.asset` | `resource.node.nickel_vein` | -1982472494 | `0x89D5DED2` | `Data_NickelOre` | 1037165092 | `NONE` |
| `ResourceNodeTemplate_LithiumCrystalCluster.asset` | `resource.node.lithium_crystal_cluster` | 1020671615 | `0x3CD6367F` | `Data_LithiumCrystal` | -531463424 | `NONE` |
| `ResourceNodeTemplate_AbyssalCrystalSpire.asset` | `resource.node.abyssal_crystal_spire` | -1912665273 | `0x8DFF0B47` | `Data_AbyssalCrystal` | 702456815 | `NONE` |

## Flora Template HashIDs

| Template Asset | Stable ID | Flora HashID (int) | Hex | Loot HashID (int) | Vulnerability | AudioMaterialID | Pulse Hz |
|---|---|---:|---|---:|---|---:|---:|
| `FloraDataTemplate_BeamAnemone.asset` | `flora.beam_anemone` | -349366742 | `0xEB2D162A` | 1061475281 | `Drill` | 2 | 0.22 |
| `FloraDataTemplate_BloodKelp.asset` | `flora.blood_kelp` | 718482850 | `0x2AD32DA2` | 2069849578 | `PlasmaCut` | 1 | 0.42 |
| `FloraDataTemplate_CableBloom.asset` | `flora.cable_bloom` | -1750052432 | `0x97B051B0` | 1061475281 | `Drill` | 2 | 0.31 |
| `FloraDataTemplate_CathedralKelp.asset` | `flora.cathedral_kelp` | -1210602032 | `0xB7D7ADD0` | 2069849578 | `PlasmaCut` | 1 | 0.34 |
| `FloraDataTemplate_GhostWeed.asset` | `flora.ghost_weed` | -788800866 | `0xD0FBDA9E` | 2069849578 | `PlasmaCut` | 1 | 0.62 |
| `FloraDataTemplate_HaloSargassum.asset` | `flora.halo_sargassum` | 904227526 | `0x35E56AC6` | 2069849578 | `PlasmaCut` | 1 | 1.12 |
| `FloraDataTemplate_IronCoral.asset` | `flora.iron_coral` | 749939571 | `0x2CB32B73` | -446461043 | `Drill` | 3 | 0.26 |
| `FloraDataTemplate_IronFloatweed.asset` | `flora.iron_floatweed` | 2092772091 | `0x7CBD2AFB` | -446461043 | `Drill` | 3 | 0.46 |
| `FloraDataTemplate_KnifeMat.asset` | `flora.knife_mat` | -408481187 | `0xE7A7125D` | 2069849578 | `PlasmaCut` | 1 | 0.58 |
| `FloraDataTemplate_LanternGrass.asset` | `flora.lantern_grass` | -1773998960 | `0x9642EC90` | 2069849578 | `PlasmaCut` | 1 | 0.94 |
| `FloraDataTemplate_LumenFrond.asset` | `flora.lumen_frond` | 607387284 | `0x2433FE94` | 2069849578 | `PlasmaCut` | 1 | 0.88 |
| `FloraDataTemplate_RiftRibbon.asset` | `flora.rift_ribbon` | 926930409 | `0x373FD5E9` | 2069849578 | `PlasmaCut` | 1 | 0.66 |
| `FloraDataTemplate_SpineMoss.asset` | `flora.spine_moss` | -541571399 | `0xDFB846B9` | 2069849578 | `PlasmaCut` | 1 | 1.08 |
| `FloraDataTemplate_StaticThicket.asset` | `flora.static_thicket` | 1167050606 | `0x458FC76E` | 2069849578 | `PlasmaCut` | 1 | 0.76 |
| `FloraDataTemplate_VeilFern.asset` | `flora.veil_fern` | 363094843 | `0x15A4633B` | 2069849578 | `PlasmaCut` | 1 | 0.48 |

### Flora Notes

- Authoring source: `Assets/_Project/Data/World/FloraTemplates/`
- Runtime owner: `HectonMapMagicVegetationBridge.floraTemplates`
- Loot hash routing is mirrored from authored `FloraDataTemplate` assets and consumed through existing `HarvestableTemplate` drop authority.
- `AudioMaterialID`: `1 = Organic`, `2 = Brittle`, `3 = Metallic`

## Ghost Mesh Standard

- If `ResourceNodeTemplate.nodeMesh == null`, runtime uses a transparent red cube ghost with exact `physicalSize`.
- Collider policy is primitive-only: `BoxCollider` or `SphereCollider`. `MeshCollider` remains forbidden.
- Ghost presentation is driven by `ResourceDistributionDirector` and applied through `ResourceNode.ApplyRuntimeTemplate(...)`.

## Persistence Contract

- Runtime depletion key: `PersistentWorldRegistry.ComputeResourceNodeTombstoneId(...)`
- Legacy display string bridge: `PersistentWorldRegistry.FormatResourceNodeTombstoneId(...)`
- Save-path flag: `PersistentWorldItemFlags.ResourceNodeDestroyed`
- Resident tombstone set: `NativeParallelHashSet<ulong> _resourceNodeTombstoneIds`

## Verification State

- Unity import/serialization verification: BLOCKED, no active Unity MCP instance.
- Console error count: UNVERIFIED.
- Scene wiring for director/template assignment: UNVERIFIED.
- Final status cannot be upgraded beyond `PENDING VERIFICATION` without live Unity evidence.
