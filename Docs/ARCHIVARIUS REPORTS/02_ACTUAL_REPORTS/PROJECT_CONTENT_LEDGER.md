# PROJECT_CONTENT_LEDGER

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: content hash authority, proxy/ghost policy, and authored template integrity surface
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Persistent_Object_Registry.txt`

## Purpose

This file is the current content-forensics ledger for authored template identity and visual fallback policy.

It now also records the editor validation owner introduced for this pass:

- validator file: `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`
- menu path: `Hecton-8/Validate Content`
- scope: `ItemData`, `FloraDataTemplate`, `FaunaDataTemplate`, `CreatureArchetypeData`, `ResourceNodeTemplate`, `BaseModuleTemplate`, data-folder prefabs, and referenced content prefabs

## Current Hash Authority

The project does not use one universal serialized `HashID` field across all content types.
Current reality is split by domain:

| Domain | Authored identity source | Runtime hash authority | Notes |
|---|---|---|---|
| Items | `ItemData.PersistentId` | `LocHash.Compute(PersistentId)` | FNV-1a 32-bit via `LocHash` |
| Flora | `FloraDataTemplate.StableId` | `LocHash.Compute(StableId)` | FNV-1a 32-bit via `LocHash` |
| Fauna | `FaunaDataTemplate.SpeciesId` | pre-authored `speciesId` integer | generated from archetype-side stable creature id by fauna authoring |
| Resource nodes | `ResourceNodeTemplate.StableId` | `ResourceNodeTemplate.StableHashId` | not part of the current collision set requested for item/flora/fauna |
| Base modules | `BaseModuleTemplate.PersistentId` | `BaseModuleTemplate.TemplateHashId` | not part of the current collision set requested for item/flora/fauna |

## Current Ghost / Proxy / Collider Policy

The validator pass clarified that one global mesh rule does not exist.

Real domain-specific rules are:

| Surface | Missing visual fallback | Collider rule | Current owner |
|---|---|---|---|
| `ResourceNodeTemplate` | runtime ghost box is already legal when `nodeMesh == null` | primitive only: `BoxCollider` / `SphereCollider`; `MeshCollider` forbidden | `ResourceNode.ApplyRuntimeTemplate(...)` |
| `FloraDataTemplate` | generated or authored `proxyPrefab` is required when `mesh == null` | generated proxy uses primitive collider only | `ContentSanityValidator` + flora proxy assets |
| `CreatureArchetypeData.prefab` / data prefabs | missing renderable mesh gets injected `__ContentSanityWireProxy` child | `MeshCollider` forbidden for scanned content prefabs | `ContentSanityValidator` |
| `ItemData.worldPrefab` | missing renderable mesh gets injected `__ContentSanityWireProxy` child | `MeshCollider` forbidden for scanned content prefabs | `ContentSanityValidator` |

Important boundary:

- `MeshCollider` is not globally banned across every project prefab
- rock, cave, and geology prefabs outside this content-validation scope still contain many `MeshCollider` uses
- the validator therefore limits `MeshCollider` errors to data-folder prefabs and prefabs explicitly referenced by scanned content assets

## Base Module Template Ledger

| Module | PersistentId | HashId | DefaultIntegrityState | DragArea m2 | Yield N | BreachArea m2 | AssetPath |
|---|---|---:|---:|---:|---:|---:|---|
| BaseModuleTemplate_Corridor | base.module.corridor | -1561972746 | 0.38 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_Corridor.asset |
| BaseModuleTemplate_Airlock | base.module.airlock | -1900346693 | 0.42 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_Airlock.asset |
| BaseModuleTemplate_BioReactor | base.module.bioreactor | 318713642 | 0.24 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_BioReactor.asset |
| BaseModuleTemplate_WindowObservation | base.module.window | -752382274 | 0.31 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_WindowObservation.asset |
| BaseModuleTemplate_ControlRoom | base.module.control_room | -247614979 | 0.29 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_ControlRoom.asset |
| BaseModuleTemplate_JunctionT | base.module.junction_t | 1962095695 | 0.34 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_JunctionT.asset |
| BaseModuleTemplate_CrewQuarters | base.module.crew_quarters | 273123897 | 0.27 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_CrewQuarters.asset |
| BaseModuleTemplate_ServiceSpine | base.module.service_spine | 52203761 | 0.22 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_ServiceSpine.asset |
| BaseModuleTemplate_DockingClamp | base.module.docking_clamp | -1151154059 | 0.33 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_DockingClamp.asset |
| BaseModuleTemplate_ResearchLab | base.module.research_lab | -207977013 | 0.26 | 12.0 | 180000 | 1.2 | Assets/_Project/Data/Construction/AbandonedModuleTemplates/BaseModuleTemplate_ResearchLab.asset |

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

## 64-bit Flora Genetic Trait Definitions

- Authoring/runtime owner: `FloraDataTemplate.GeneticsMask` and `CultivationManager.CultivationSlotState.GeneticsMask`.
- Persistence owner: `InventoryDTO.itemGeneticsWords : ulong[]` and `ModuleDTO.cultivationGeneticsMasks : ulong[]`.
- Save format: v53 introduced 64-bit genetics; v54 keeps the same layout and migrates v48-v52 legacy `uint[]` masks into `ulong[]`.
- Splice equation: `result = (maskA | maskB) ^ (XorShift32(seed) & 0x000000000000000FUL)`.

| Bit | Mask | Trait | Runtime effect |
|---:|---:|---|---|
| 0 | `0x0000000000000001` | `Biolum` | Enables biolum lighting credit and shader emission trait inheritance. |
| 1 | `0x0000000000000002` | `O2_Produce` | Mature cultivation slots inject oxygen into the owning module atmosphere. |
| 2 | `0x0000000000000004` | `Toxic` | Adds scrubber load, hazard contribution, and mature spore acoustic behavior. |
| 3 | `0x0000000000000008` | `RapidGrowth` | Applies cultivation growth-rate multiplier during slow tick. |

- Bits `4-63` remain 64-bit reserved space for authored `GeneticTraitProfile` rows; mutation currently toggles only bits `0-3`.

## Item Audio-Material Reality

`ItemData` does not have a true null / missing-state `AudioMaterialID`.
Current schema always resolves an effective value through one of two paths:

1. explicit serialized `audioMaterialId` when `autoResolvePhysicalMetadata == false`
2. `ItemPhysicalMetadataUtility.ResolveDefaultAudioMaterialId(...)` when `autoResolvePhysicalMetadata == true`

Therefore the new validator cannot honestly flag a literal "missing field".
It instead flags:

- invalid serialized enum values
- explicit `Organic` authoring on items whose classification resolves to non-organic material families

This is stricter and more truthful than pretending the current schema can contain a null audio-material slot.

## Fauna Scavenging States

- `ApexTerritoryOverride`: rival leviathans inside the authored territory band are promoted above player pursuit in the predator cognition target stack.
- `ApexForcedRetreat`: apex losers below `30%` health are forced into migration/flee logic and leave the current sector.
- `ApexIntimidation`: territorial winners hold a temporary intimidation aura that smaller predators read as an avoidance threat.
- `CorpseResourceNode`: large-fauna deaths register bounded organic corpse nodes, inject blood scent into `ChemicalInfluenceGrid`, and remain available until scavengers consume the remaining biomass.
- `BaitFeedingLock`: dropped organic bait items are surfaced to fauna through `PickupItem.IsFaunaBait`, allowing herbivores, scavengers, and smaller predators to enter a local feeding lock near the bait source.
- `AudioMaterialID`: `1 = Organic`, `2 = Brittle`, `3 = Metallic`

## 2026-05-01 Content Delta - Hadal Carbon And Meteorite Resources

Current workspace contains three additional raw item assets and three matching resource-node templates not covered by the older `02_ACTUAL_REPORTS` ledger section.

Item assets:

| Item Asset | Stable ID | Item HashID | Hex | Tier | Family | AudioMaterialID | Depth |
|---|---|---:|---|---:|---:|---:|---|
| `Data_CarbonGraphite.asset` | `Data_CarbonGraphite` | 2008184373 | `0x77B27635` | 4 | 6 | 2 | 3200-6000 |
| `Data_PressureDiamond.asset` | `Data_PressureDiamond` | -1593575957 | `0xA103F5EB` | 4 | 6 | 2 | 3500-6000 |
| `Data_VoidGlassMeteorite.asset` | `Data_VoidGlassMeteorite` | 1720811528 | `0x66918008` | 5 | 6 | 1 | explicit node depth owns placement |

Resource-node templates:

| Node Template | Stable ID | Entity HashID | Hex | Yield Item | Placement Probability | Valid Layers | Runtime owner note |
|---|---|---:|---|---|---:|---:|---|
| `ResourceNodeTemplate_CarbonGraphiteNodule.asset` | `resource.node.carbon_graphite_nodule` | -645249103 | `0xD98A47B1` | `Data_CarbonGraphite` | 0.18 | 1792 | pressure-metamorphism source candidate |
| `ResourceNodeTemplate_PressureDiamond.asset` | `resource.node.pressure_diamond` | -1829783455 | `0x92EFB861` | `Data_PressureDiamond` | 0 | 1792 | pressure-metamorphism output candidate |
| `ResourceNodeTemplate_VoidGlassMeteorite.asset` | `resource.node.void_glass_meteorite` | 307316757 | `0x12514815` | `Data_VoidGlassMeteorite` | 0 | 1792 | `ResourceDistributionDirector` resolves this stable ID for rare impact spawns |

Authoring constraints observed from YAML:

- All three node templates use `colliderShape: 1` and have `nodeMesh` / `nodeMaterial` empty, so ghost/proxy visual policy still needs validator execution before this can be called art-complete.
- `CarbonGraphiteNodule` and `PressureDiamond` support autonomous extraction; `VoidGlassMeteorite` does not.
- `VoidGlassMeteorite` carries radiation through the item asset (`radiationSvPerSecond: 1.4`) and through `ResourceDistributionDirector` impact hazard settings.
- Hash values above were recomputed from `LocHash.Compute` algorithm in `LocRegistry.cs`; editor collision validator was not executed in this pass.

## Validation Boundary

What this ledger now covers:

- active hash authority for item/flora/fauna identities
- ghost/proxy policy for resource nodes, flora proxies, and scanned content prefabs
- current `MeshCollider` validation boundary
- current item-audio-material truth

What it still does not prove:

- live execution of `Hecton-8/Validate Content`
- clean Unity console after validator compile
- zero collision result at runtime/editor execution time

STATUS: PENDING VERIFICATION
