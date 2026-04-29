# PROJECT_CONTENT_LEDGER

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
- `AudioMaterialID`: `1 = Organic`, `2 = Brittle`, `3 = Metallic`
