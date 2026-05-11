# PROJECT_CONTENT_LEDGER
Date: 2026-05-07
Status: PENDING VERIFICATION

STATUS: PENDING VERIFICATION
OWNER: Resource Matrix / Geology
SOURCE OF TRUTH: `Assets/_Project/Data/Scavenging/ResourceNodes/`
MANDATES FOLLOWED:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `STRM_Persistent_Object_Registry.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- Current compile-only evidence: `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, `CS_WRITES_AFTER_END=0`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this ledger as current project truth.
- `Assets/_Project/Data/Scavenging/ResourceNodes/` remains the authored data source for this ledger, but this document is not proof that all resource nodes, hash IDs, layers, or runtime spawn paths are scene-validated.
- Re-open current assets and source before changing IDs or layer contracts.

## Purpose

This ledger tracks stable authored identifiers and runtime `LocHash` values for the HECTON-8 resource-node matrix.
It is the canonical handoff table for item/resource/entity hash coordination until a broader project-wide uint registry is formalized.

## Orphaned Script Deletion Targets

May 7 static audit scanned `1192` `.cs` files under `Assets/_Project/Scripts` and `428` first-party prefab YAML files.

Result: `334` scripts are deletion review targets because the filename token was not found in any other `.cs` file and the script `.meta` GUID was not found in first-party prefab YAML.

Full candidate artifact:

- `CodexArtifacts/2026-05-07_ORPHANED_SCRIPT_AUDIT.csv`

Boundary:

- This is not deletion authorization.
- Editor menu scripts, smoke testers, generated roots, reflection entry points, asmdef owners, ScriptableObject references, scene references, and Addressables references require manual review before removal.
- Treat the list as the deletion queue seed, not as proof that the files are unused at runtime.

## LayerMask Sanitization Ledger

Source of truth: `Assets/_Project/Scripts/Core/HectonLayerMasks.cs` and `ProjectSettings/TagManager.asset`.

| Mask | Decimal | Hex | Layers |
|---|---:|---|---|
| `DataTemplateAuthoringMask` | 1792 | `0x00000700` | `BaseModule`/`DroppedItem`/`Creature` |
| `StrictInteractionLayerMask` | 1792 | `0x00000700` | `BaseModule`/`DroppedItem`/`Creature` |
| `ConstructionSurfaceLayerMask` | 1792 | `0x00000700` | `BaseModule`/`DroppedItem`/`Creature` |
| `TerrainLayerMask` | 128 | `0x00000080` | `Terrain` |
| `BaseModuleLayerMask` | 256 | `0x00000100` | `BaseModule` |
| `DroppedItemLayerMask` | 512 | `0x00000200` | `DroppedItem` |
| `CreatureLayerMask` | 1024 | `0x00000400` | `Creature` |
| `VehicleLayerMask` | 2048 | `0x00000800` | `Vehicle` |
| `VoxelCaveLayerMask` | 4096 | `0x00001000` | `VoxelCave` |
| `DebrisLayerMask` | 16384 | `0x00004000` | `Debris` |
| `SocketsLayerMask` | 524288 | `0x00080000` | `Sockets` |
| `SeamProbeLayerMask` | 22912 | `0x00005980` | `Terrain`/`BaseModule`/`Vehicle`/`VoxelCave`/`Debris` |
| `AllDefinedProjectLayersMask` | 1048575 | `0x000FFFFF` | Layers `0..19` |
| `DefaultRaycastLayerMask` | 1048571 | `0x000FFFFB` | Layers `0..19` except `Ignore Raycast` |
| `AllDefinedProjectRenderingLayerMask` | 1048575 | `0x000FFFFF` | Renderer-safe 20-bit project mask |

- Sanitizer replacement target for `m_Bits: -1` / `m_Bits: 4294967295`: `DataTemplateAuthoringMask` (`1792`) for `ResourceNodeTemplate`, `FaunaDataTemplate`, and `FloraDataTemplate`; `AllDefinedProjectLayersMask` (`1048575`) for other managed data assets.
- `Physics.DefaultRaycastLayers` is not an accepted project mask. Use `HectonLayerMasks.DefaultRaycastLayerMask` or a stricter semantic mask.

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
| `ResourceNodeTemplate_DeepMantleGeode.asset` | `resource.node.deep_mantle_geode` | 1840897952 | `0x6DB9DFA0` | `Data_AbyssalCrystal` | 702456815 | `NONE` |
| `ResourceNodeTemplate_ThermalDiamond.asset` | `resource.node.thermal_diamond` | -858705345 | `0xCCD1323F` | `Data_AbyssalCrystal` | 702456815 | `NONE` |
| `ResourceNodeTemplate_CrystallizedOsmium.asset` | `resource.node.crystallized_osmium` | -731924692 | `0xD45FB72C` | `Data_RareEarthDust` | 1997058338 | `NONE` |
| `ResourceNodeTemplate_ToxicSulfurDeposit.asset` | `resource.node.toxic_sulfur_deposit` | -232166803 | `0xF2296A6D` | `Data_SulfurClumps` | -948091731 | `NONE` |
| `ResourceNodeTemplate_BrineIsotopeGeode.asset` | `resource.node.brine_isotope_geode` | 694959346 | `0x296C3CF2` | `Data_AbyssalCrystal` | 702456815 | `NONE` |
| `ResourceNodeTemplate_TitaniumBasaltMass.asset` | `resource.node.titanium_basalt_mass` | 384986101 | `0x16F26BF5` | `Data_TitaniumScrap` | -783267794 | `NONE` |
| `ResourceNodeTemplate_XenonOmegaVentCache.asset` | `resource.node.xenon_omega_vent_cache` | -779526194 | `0xD1895FCE` | `Data_RareEarthDust` | 1997058338 | `NONE` |
| `ResourceNodeTemplate_Silicon7BGlassVein.asset` | `resource.node.silicon_7b_glass_vein` | 1566735374 | `0x5D627C0E` | `Data_SilicaShards` | -374612680 | `NONE` |
| `ResourceNodeTemplate_AegiriumCrustNodule.asset` | `resource.node.aegirium_crust_nodule` | 174837396 | `0x0A6BCE94` | `Data_CobaltAlloy` | 857583970 | `NONE` |
| `ResourceNodeTemplate_CarbonGraphiteNodule.asset` | `resource.node.carbon_graphite_nodule` | -645249103 | `0xD98A47B1` | `Data_CarbonGraphite` | 2008184373 | `NONE` |
| `ResourceNodeTemplate_PressureDiamond.asset` | `resource.node.pressure_diamond` | -1829783455 | `0x92EFB861` | `Data_PressureDiamond` | -1593575957 | `NONE` |

### Extreme-Depth Notes

- `ResourceNodeTemplate_DeepMantleGeode.asset` is the hydrothermal-only geode owner. Runtime gate: `Temperature > 80C`, steam explosion without `ToolUpgradeBits.ThermalShield`, crater carve on depletion, extractor-enabled.
- `ResourceNodeTemplate_ThermalDiamond.asset` is the flash-freeze crystallization output. Runtime owner: `AbyssalThermalManager.ReportFlashFreeze(...)` -> Burst boundary validation -> `ResourceDistributionDirector.TrySpawnThermalDiamondCrystallization(...)`.
- `ResourceNodeTemplate_CrystallizedOsmium.asset`, `ResourceNodeTemplate_ToxicSulfurDeposit.asset`, and `ResourceNodeTemplate_BrineIsotopeGeode.asset` are deterministic hadal brine-pool resources. Runtime gate: `RequiresBrinePool = true`, brine density override `1250 kg/m3`, toxicity hazard routing, and seismic upwelling reinstatement.
- `ResourceNodeTemplate_TitaniumBasaltMass.asset` is the new hadal titanium vein for autonomous production scaling.
- `ResourceNodeTemplate_CrystallizedOsmium.asset`, `ResourceNodeTemplate_XenonOmegaVentCache.asset`, `ResourceNodeTemplate_Silicon7BGlassVein.asset`, `ResourceNodeTemplate_AegiriumCrustNodule.asset`, and `ResourceNodeTemplate_BrineIsotopeGeode.asset` currently route into placeholder item assets until dedicated isotope item records exist. Lore IDs are `crystallized_osmium`, `xenon_omega`, `silicon_7b`, `aegirium`, and `brine_isotope`.
- `ResourceNodeTemplate_CarbonGraphiteNodule.asset` is the pressure-metamorphism source. Runtime gate: resident node, depth `>3500m`, `ResourceNode.PressureMetamorphismProgressSeconds` accumulates in the Burst slow-tick lane.
- `ResourceNodeTemplate_PressureDiamond.asset` is the pressure-metamorphism output. Runtime owner: `ResourceDistributionDirector.PressureMetamorphismJob`, persistence marker: `PersistentWorldItemFlags.ResourceNodeMetamorphosed`, stable entity remains alive and changes template without destruction.

## Flora Template HashIDs

| Template Asset | Stable ID | Flora HashID (int) | Hex | Loot HashID (int) | Vulnerability | AudioMaterialID | Attachment Surface | Pulse Hz |
|---|---|---:|---|---:|---|---:|---|---:|
| `FloraDataTemplate_BeamAnemone.asset` | `flora.beam_anemone` | -349366742 | `0xEB2D162A` | 1061475281 | `Drill` | 2 | `Metal` | 0.22 |
| `FloraDataTemplate_BloodKelp.asset` | `flora.blood_kelp` | 718482850 | `0x2AD32DA2` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.42 |
| `FloraDataTemplate_CableBloom.asset` | `flora.cable_bloom` | -1750052432 | `0x97B051B0` | 1061475281 | `PlasmaCut` | 2 | `Metal` | 0.31 |
| `FloraDataTemplate_CathedralKelp.asset` | `flora.cathedral_kelp` | -1210602032 | `0xB7D7ADD0` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.34 |
| `FloraDataTemplate_FungalStalk.asset` | `flora.fungal_stalk` | -44415284 | `0xFD5A46CC` | 2069849578 | `Cut|Drill` | 1 | `Seabed` | 0.52 |
| `FloraDataTemplate_GhostWeed.asset` | `flora.ghost_weed` | -788800866 | `0xD0FBDA9E` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.62 |
| `FloraDataTemplate_HaloSargassum.asset` | `flora.halo_sargassum` | 904227526 | `0x35E56AC6` | 2069849578 | `PlasmaCut` | 1 | `Any` | 1.12 |
| `FloraDataTemplate_AcidShroom.asset` | `flora.acid_shroom` | -1214853303 | `0xB796CF49` | 2069849578 | `Cut|Drill` | 1 | `Seabed` | 0.66 |
| `FloraDataTemplate_Blindcap.asset` | `flora.blindcap` | 531854346 | `0x1FB3740A` | 2069849578 | `Cut|Drill` | 1 | `Seabed` | 0.58 |
| `FloraDataTemplate_IronCoral.asset` | `flora.iron_coral` | 749939571 | `0x2CB32B73` | -446461043 | `Drill` | 3 | `Metal` | 0.26 |
| `FloraDataTemplate_IronFloatweed.asset` | `flora.iron_floatweed` | 2092772091 | `0x7CBD2AFB` | -446461043 | `Drill` | 3 | `Any` | 0.46 |
| `FloraDataTemplate_KnifeMat.asset` | `flora.knife_mat` | -408481187 | `0xE7A7125D` | 2069849578 | `PlasmaCut` | 1 | `Any` | 0.58 |
| `FloraDataTemplate_LanternGrass.asset` | `flora.lantern_grass` | -1773998960 | `0x9642EC90` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.94 |
| `FloraDataTemplate_LumenFrond.asset` | `flora.lumen_frond` | 607387284 | `0x2433FE94` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.88 |
| `FloraDataTemplate_NerveVine.asset` | `flora.nerve_vine` | 1090552876 | `0x4100842C` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 1.24 |
| `FloraDataTemplate_RiftRibbon.asset` | `flora.rift_ribbon` | 926930409 | `0x373FD5E9` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.66 |
| `FloraDataTemplate_RustMoss.asset` | `flora.rust_moss` | 1719756568 | `0x66816718` | 1061475281 | `PlasmaCut` | 1 | `Metal` | 0.24 |
| `FloraDataTemplate_SporeCannon.asset` | `flora.spore_cannon` | -1562404102 | `0xA2DF9AFA` | 2069849578 | `Cut|Drill` | 1 | `Seabed` | 0.74 |
| `FloraDataTemplate_SpineMoss.asset` | `flora.spine_moss` | -541571399 | `0xDFB846B9` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 1.08 |
| `FloraDataTemplate_StaticThicket.asset` | `flora.static_thicket` | 1167050606 | `0x458FC76E` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.76 |
| `FloraDataTemplate_ThermalTubeworm.asset` | `flora.thermal_tubeworm` | -958274584 | `0xC6E1E3E8` | 1061475281 | `Burn` | 1 | `Metal` | 0.66 |
| `FloraDataTemplate_VeilFern.asset` | `flora.veil_fern` | 363094843 | `0x15A4633B` | 2069849578 | `PlasmaCut` | 1 | `Seabed` | 0.48 |

### Flora Notes

- Authoring source: `Assets/_Project/Data/World/FloraTemplates/`
- Runtime owner: `HectonMapMagicVegetationBridge.floraTemplates`
- Loot hash routing is mirrored from authored `FloraDataTemplate` assets and consumed through existing `HarvestableTemplate` drop authority.
- `AudioMaterialID`: `1 = Organic`, `2 = Brittle`, `3 = Metallic`
- `Attachment Surface`: `Any = floating/freeform`, `Seabed = terrain-anchored`, `Metal = artificial-structure overgrowth`
- `Cable Bloom` and `Rust Moss` are authored as parasitic module flora. `Thermal Tubeworm` is authored as thermophilic module flora with a 100 C / 300 s activation gate.

### Flora Audio-Visual Sync Parameters

- Visual owner: `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`.
- Audio owner: `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` dispatching through `SpatialAudioManager.PlaySporeEmissionAtAup(...)`.
- Mature toxic acoustic cadence is locked to `1.0 / max(PulseFrequency, 0.01 Hz)`.
- Shared shader/audio phase seed: `phase01 = frac(SimulationTimeSeconds * PulseFrequency + ((position.x * 0.07 + position.z * 0.05) / 2PI))`.
- Acoustic event target phase: `0.25`, matching the shader sine crest.
- Edge-inward necrosis uses the health payload from `HectonVegetationInstanceData.HealthNormalized`; shader clip term is `clip(edgeSignal - saturate(1.0 - saturate(health01) + noise * 0.22))`.
- Current-reactive bioluminescence multiplier remains `1.0 + flowMagnitude * 0.5`, sourced from the indirect vegetation flow-field sample.
- Distance LOD dimming starts at 50 m and dither-suppresses biolum contribution by 90 m to reduce far-field emission pressure on MX350.

## 64-bit Flora Genetic Trait Definitions

- Authoring/runtime owner: `FloraDataTemplate.GeneticsMask` and `CultivationManager.CultivationSlotState.GeneticsMask`.
- Persistence owner: `InventoryDTO.itemGeneticsWords : byte[]` and `ModuleDTO.cultivationGeneticsMasks : ulong[]`.
- Save format: v53 introduced 64-bit genetics; v54 keeps the same layout and migrates v48-v52 legacy `uint[]` masks into `ulong[]`.
- Splice equation: `result = (maskA | maskB) ^ (XorShift32(seed) & 0x000000000000000FUL)`.

| Bit | Mask | Trait | Runtime effect |
|---:|---:|---|---|
| 0 | `0x0000000000000001` | `Biolum` | Enables biolum lighting credit and shader emission trait inheritance. |
| 1 | `0x0000000000000002` | `O2_Produce` | Mature cultivation slots inject oxygen into the owning module atmosphere. |
| 2 | `0x0000000000000004` | `Toxic` | Adds scrubber load, hazard contribution, and mature spore acoustic behavior. |
| 3 | `0x0000000000000008` | `RapidGrowth` | Applies cultivation growth-rate multiplier during slow tick. |

- Bits `4-63` remain 64-bit reserved space for authored `GeneticTraitProfile` rows; mutation currently toggles only bits `0-3`.

## Ghost Mesh Standard

- If `ResourceNodeTemplate.nodeMesh == null`, runtime uses a transparent red cube ghost with exact `physicalSize`.
- Collider policy is primitive-only: `BoxCollider` or `SphereCollider`. `MeshCollider` remains forbidden.
- Ghost presentation is driven by `ResourceDistributionDirector` and applied through `ResourceNode.ApplyRuntimeTemplate(...)`.

## Persistence Contract

- Runtime depletion key: `PersistentWorldRegistry.ComputeResourceNodeTombstoneId(...)`
- Legacy display string bridge: `PersistentWorldRegistry.FormatResourceNodeTombstoneId(...)`
- Save-path flag: `PersistentWorldItemFlags.ResourceNodeDestroyed`
- Resident tombstone set: `NativeParallelHashSet<ulong> _resourceNodeTombstoneIds`
- Metamorphosis flag: `PersistentWorldItemFlags.ResourceNodeMetamorphosed`
- Resident metamorphosis set: `NativeParallelHashSet<ulong> _resourceNodeMetamorphosedIds`

## Fauna Scavenging States

- `ApexTerritoryOverride`: `PredatorCognitionDomain` now treats rival leviathans inside the authored `ApexTerritoryProfile.territoryRadiusMeters` band as the dominant hunt target instead of the player.
- `ApexForcedRetreat`: apex predators below `30%` health receive a forced migration/flee override and vacate the current sector after losing a territorial dispute.
- `ApexIntimidation`: victorious apex predators broadcast a temporary intimidation aura; smaller predators treat that apex as a threat source and stay outside the authored intimidation radius.
- `CorpseResourceNode`: large-fauna deaths register bounded corpse resource nodes in `DestructibleOrganicManager`, emit blood scent into `ChemicalInfluenceGrid`, and are depleted by scavenger feeding until despawn.
- `BaitFeedingLock`: dropped organic bait items are now resolved through `PickupItem.IsFaunaBait`; herbivores, scavengers, and non-leviathan aggressive fauna can enter a local feeding investigate/sated loop around the bait source.

## Verification State

- Unity import/serialization verification: BLOCKED, no active Unity MCP instance.
- Console error count: UNVERIFIED.
- Scene wiring for director/template assignment: UNVERIFIED.
- Final status cannot be upgraded beyond `PENDING VERIFICATION` without live Unity evidence.
