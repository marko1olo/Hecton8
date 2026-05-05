# HECTON-8 NAMING CONVENTION INQUISITION
Date: 2026-05-04
Status: DEPRECATED

## Scribe-Compliance Audit | Status: CONTINUOUS

---

### RULE REMINDER (from AGENTS.md)
- **Prefabs** MUST start with `PFB_` or `GEN_`.
- **Materials** MUST start with `MAT_`.
- **Textures** MUST start with `TX_`.
- **CRITICAL**: Cyrillic characters in filenames are a **CRITICAL VIOLATION** for AA-grade projects.

---

### 🔴 CRITICAL: CYRILLIC FILENAMES

The following assets contain Cyrillic characters and **must be renamed immediately** before any build handoff.

#### Fonts
| Path | Violation |
|------|-----------|
| `Assets/_Project/Art/Materials/Fonts/текст SDF.asset` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/текст SDF.asset.meta` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/текст.ttf` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/текст.ttf.meta` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/цифры SDF.asset` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/цифры SDF.asset.meta` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/цифры.ttf` | Cyrillic filename |
| `Assets/_Project/Art/Materials/Fonts/цифры.ttf.meta` | Cyrillic filename |

#### Meshes (Cleaned)
| Path | Violation |
|------|-----------|
| `Assets/_Project/Art/Meshes/Cleaned/ENV__арка1_GEO_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__арка2_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Арка_1_стонхенж_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Болдер_1_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__большая_горизонтальная_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__донная_куча_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Кам_с_кучк_3-3-1_5__LOD0-rfk_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Камень_баз_4-4-2_м_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Камень_баз_6-4-4м_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__куча1_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__куча2_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__куча_3_болд_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Кучка_3_5-5-2_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Кучка_4__5-5_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Кучка_5___5-5-2_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Огром_скала_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Огроменная_скала_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__пиллар2_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__Скала2_geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV__скала_большая_GEO_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV_блин_плоск_5_на_5__geo_LOD0_cleaned.asset` | Cyrillic filename |
| `Assets/_Project/Art/Meshes/Cleaned/ENV_ПИЛЛАР1_LOD0_cleaned.asset` | Cyrillic filename |

*(Note: Each of the above has LOD1/LOD2/LOD3/PHYSICS_SKIN variants and .meta files — total ~120+ Cyrillic-named files in Meshes/Cleaned alone.)*

#### Models (Baked)
| Path | Violation |
|------|-----------|
| `Assets/_Project/Art/Models/Baked/PILLAR1 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/Арка 1 стонхендж лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/арка 2 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/арка1 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/болдер 1 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/болдер 2 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/донная куча лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/камень баз 6-4-4 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/камень бзв 4-4-2 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/Камень с кучкой 3-3-1.5 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/круговая лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/куча 2 камн лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/куча 3 болд лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/куча1 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/кучка 5 5-5-2 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/кучка мелка 1 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/кучка3 5-5-2 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/кучка4 плоск 5-5 лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/лежач скала лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/огром скала лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/Огромная скала! лод0.asset` | Cyrillic filename |
| `Assets/_Project/Art/Models/Baked/пиллар2 лод0.asset` | Cyrillic filename |

*(Note: Each of the above has LOD1/LOD2/LOD3 variants and .meta files — total ~90+ Cyrillic-named files in Models/Baked alone.)*

#### Prefabs
| Path | Violation |
|------|-----------|
| `Assets/_Project/Prefabs/Nature/Rocks/Метки для наростов/Socket_Side.prefab` | Cyrillic folder name |
| `Assets/_Project/Prefabs/Nature/Rocks/Метки для наростов/Socket_Top.prefab` | Cyrillic folder name |
| `Assets/_Project/Prefabs/Nature/Rocks/Метки для наростов/Socket_Under.prefab` | Cyrillic folder name |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Арка 1 стонхенж.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ арка1.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ арка2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ болдер 1.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Болдер 2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ большая горизонтальная.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ донная куча.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Кам с кучк 3-3-1.5 .prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Камень баз 4-4-2 м.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Камень баз 6-4-4м.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ круговая.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ куча 3 болд.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ куча1.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ куча2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Кучка 3 5-5-2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Кучка 4  5-5.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Кучка 5   5-5-2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ кучка мелка 1.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Огром скала.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Огроменная скала.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ пиллар2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ скала большая.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Скала2.prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_блин плоск 5 на 5 .prefab` | Cyrillic folder + filename |
| `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ПИЛЛАР1.prefab` | Cyrillic folder + filename |

#### Other Data
| Path | Violation |
|------|-----------|
| `Assets/_Project/Data/текст.txt` | Cyrillic filename |

**TOTAL CYRILLIC FILES IDENTIFIED: ~250+** (including .meta companions and LOD variants)

---

### 🟡 PREFAB NAMING VIOLATIONS (missing PFB_/GEN_ prefix)

| Path | Current Name | Required Prefix |
|------|-------------|-----------------|
| `Assets/_Project/Prefabs/Directional Light.prefab` | `Directional Light` | `PFB_DirectionalLight` |
| `Assets/_Project/Prefabs/GasGiant_Aegir.prefab` | `GasGiant_Aegir` | `PFB_GasGiant_Aegir` |
| `Assets/_Project/Prefabs/GEOGRAPHY.prefab` | `GEOGRAPHY` | `PFB_GEOGRAPHY` |
| `Assets/_Project/Prefabs/Hecton Ocean.prefab` | `Hecton Ocean` | `PFB_HectonOcean` |
| `Assets/_Project/Prefabs/HUD_Internal.prefab` | `HUD_Internal` | `PFB_HUD_Internal` |
| `Assets/_Project/Prefabs/Item_Titanium.prefab` | `Item_Titanium` | `PFB_Item_Titanium` |
| `Assets/_Project/Prefabs/Mesh_Arch_010.prefab` | `Mesh_Arch_010` | `PFB_Mesh_Arch_010` |
| `Assets/_Project/Prefabs/Objects.prefab` | `Objects` | `PFB_Objects` |
| `Assets/_Project/Prefabs/Ocean_Crest.prefab` | `Ocean_Crest` | `PFB_Ocean_Crest` |
| `Assets/_Project/Prefabs/Player.prefab` | `Player` | `PFB_Player` |
| `Assets/_Project/Prefabs/PROPS.prefab` | `PROPS` | `PFB_PROPS` |
| `Assets/_Project/Prefabs/Sky_System.prefab` | `Sky_System` | `PFB_Sky_System` |
| `Assets/_Project/Prefabs/STRUCTURES 1.prefab` | `STRUCTURES 1` | `PFB_STRUCTURES_1` |
| `Assets/_Project/Prefabs/STRUCTURES.prefab` | `STRUCTURES` | `PFB_STRUCTURES` |
| `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` | `Suit_HUD_Canvas` | `PFB_Suit_HUD_Canvas` |
| `Assets/_Project/Prefabs/TECH_DEBRIS.prefab` | `TECH_DEBRIS` | `PFB_TECH_DEBRIS` |
| `Assets/_Project/Prefabs/VoxelChunk.prefab` | `VoxelChunk` | `PFB_VoxelChunk` |
| `Assets/_Project/Prefabs/WorldGenerator.prefab` | `WorldGenerator` | `PFB_WorldGenerator` |
| `Assets/_Project/Prefabs/[FAUNA_DIRECTOR].prefab` | `[FAUNA_DIRECTOR]` | `PFB_FAUNA_DIRECTOR` |
| `Assets/_Project/Prefabs/[LOOT_MANAGER].prefab` | `[LOOT_MANAGER]` | `PFB_LOOT_MANAGER` |
| `Assets/_Project/Prefabs/Buildings/Cube.prefab` | `Cube` | `PFB_Cube` |
| `Assets/_Project/Prefabs/Nature/Rocks/Forest_Rock_Shelf.prefab` | `Forest_Rock_Shelf` | `PFB_Forest_Rock_Shelf` |
| `Assets/_Project/Prefabs/Nature/Rocks/Mossy_Forest_Rock.prefab` | `Mossy_Forest_Rock` | `PFB_Mossy_Forest_Rock` |
| `Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock.prefab` | `Nordic_Beach_Rock` | `PFB_Nordic_Beach_Rock` |
| `Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock_Formation.prefab` | `Nordic_Beach_Rock_Formation` | `PFB_Nordic_Beach_Rock_Formation` |
| `Assets/_Project/Prefabs/Nature/Rocks/Rock_Skala.prefab` | `Rock_Skala` | `PFB_Rock_Skala` |
| `Assets/_Project/Prefabs/Nature/Rocks/Baked/Baked_Kucha_01.prefab` | `Baked_Kucha_01` | `PFB_Baked_Kucha_01` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab` | `Item_Tool_BeaconDeployer_World` | `PFB_Item_Tool_BeaconDeployer_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab` | `Item_Tool_Builder_World` | `PFB_Item_Tool_Builder_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab` | `Item_Tool_EnvAnalyzer_World` | `PFB_Item_Tool_EnvAnalyzer_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab` | `Item_Tool_Flashlight_World` | `PFB_Item_Tool_Flashlight_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab` | `Item_Tool_HarpoonLauncher_World` | `PFB_Item_Tool_HarpoonLauncher_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab` | `Item_Tool_Knife_World` | `PFB_Item_Tool_Knife_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab` | `Item_Tool_LaserCutter_World` | `PFB_Item_Tool_LaserCutter_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab` | `Item_Tool_Propulsion_World` | `PFB_Item_Tool_Propulsion_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab` | `Item_Tool_Repair_World` | `PFB_Item_Tool_Repair_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab` | `Item_Tool_SalvageSampler_World` | `PFB_Item_Tool_SalvageSampler_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab` | `Item_Tool_Scanner_World` | `PFB_Item_Tool_Scanner_World` |
| `Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab` | `Item_Tool_StunPistol_World` | `PFB_Item_Tool_StunPistol_World` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab` | `Tool_BeaconDeployer_Held` | `PFB_Tool_BeaconDeployer_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab` | `Tool_Builder_Held` | `PFB_Tool_Builder_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab` | `Tool_EnvAnalyzer_Held` | `PFB_Tool_EnvAnalyzer_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab` | `Tool_Flashlight_Held` | `PFB_Tool_Flashlight_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab` | `Tool_HarpoonLauncher_Held` | `PFB_Tool_HarpoonLauncher_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab` | `Tool_Knife_Held` | `PFB_Tool_Knife_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab` | `Tool_LaserCutter_Held` | `PFB_Tool_LaserCutter_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab` | `Tool_Propulsion_Held` | `PFB_Tool_Propulsion_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab` | `Tool_Repair_Held` | `PFB_Tool_Repair_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab` | `Tool_SalvageSampler_Held` | `PFB_Tool_SalvageSampler_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab` | `Tool_Scanner_Held` | `PFB_Tool_Scanner_Held` |
| `Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab` | `Tool_StunPistol_Held` | `PFB_Tool_StunPistol_Held` |

**TOTAL PREFAB VIOLATIONS: 54**

---

### 🟡 MATERIAL NAMING VIOLATIONS (missing MAT_ prefix)

| Path | Current Name | Required Prefix |
|------|-------------|-----------------|
| `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat` | `Mat_AegirHazeOverlay` | `MAT_AegirHazeOverlay` |
| `Assets/_Project/Art/Materials/Mat_GasGiant.mat` | `Mat_GasGiant` | `MAT_GasGiant` |
| `Assets/_Project/Art/Materials/Mat_HectonSky.mat` | `Mat_HectonSky` | `MAT_HectonSky` |
| `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat` | `Mat_HectonSky_CloudOverlay` | `MAT_HectonSky_CloudOverlay` |
| `Assets/_Project/Art/Materials/Mat_Shelf.mat` | `Mat_Shelf` | `MAT_Shelf` |
| `Assets/_Project/Art/Materials/Mat_Skybox_Final.mat` | `Mat_Skybox_Final` | `MAT_Skybox_Final` |
| `Assets/_Project/Art/Materials/Mat_Sun.mat` | `Mat_Sun` | `MAT_Sun` |
| `Assets/_Project/Art/Materials/Mat_Terrain.mat` | `Mat_Terrain` | `MAT_Terrain` |
| `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat` | `Mat_TriplanarRock` | `MAT_TriplanarRock` |
| `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat` | `Mat_Visor_Glass` | `MAT_Visor_Glass` |
| `Assets/_Project/Art/Materials/Mat_Water.mat` | `Mat_Water` | `MAT_Water` |
| `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.mat` | `Meshy_AI_Alien_barnacles_clust_0301230506_texture` | `MAT_Meshy_AI_Alien_barnacles_clust_0301230506_texture` |
| `Assets/_Project/Art/Materials/red.mat` | `red` | `MAT_Red` |
| `Assets/_Project/Art/Materials/Sand.mat` | `Sand` | `MAT_Sand` |
| `Assets/_Project/Art/Materials/Skybox.mat` | `Skybox` | `MAT_Skybox` |
| `Assets/_Project/Art/Materials/Snow.mat` | `Snow` | `MAT_Snow` |
| `Assets/_Project/Art/Materials/terrain 1.mat` | `terrain 1` | `MAT_Terrain_1` |
| `Assets/_Project/Art/Materials/terrain 2.mat` | `terrain 2` | `MAT_Terrain_2` |
| `Assets/_Project/Art/Materials/terrain.mat` | `terrain` | `MAT_Terrain` |

*(Note: Materials inside subfolders like `Celestial/`, `Construction/`, `Diagnostics/`, `Gameplay/`, `Nature/` also use `Mat_` prefix instead of `MAT_`. Count is partial due to scope limits.)*

**TOTAL MATERIAL VIOLATIONS (confirmed): 19+**

---

### 🟡 TEXTURE NAMING VIOLATIONS (missing TX_ prefix)

The `Assets/_Project/Art/TEXTURES/` folder contains many textures without `TX_` prefix. Due to the large volume, a representative sample:

| Path | Current Name | Required Prefix |
|------|-------------|-----------------|
| `Assets/_Project/Art/TEXTURES/Aegir_storms.png` | `Aegir_storms` | `TX_Aegir_storms` |
| `Assets/_Project/Art/TEXTURES/clouds.png` | `clouds` | `TX_clouds` |
| `Assets/_Project/Art/TEXTURES/clouds0_diff.png` | `clouds0_diff` | `TX_clouds0_diff` |
| `Assets/_Project/Art/TEXTURES/FLOOR.png` | `FLOOR` | `TX_FLOOR` |
| `Assets/_Project/Art/TEXTURES/FLOOR1.png` | `FLOOR1` | `TX_FLOOR1` |
| `Assets/_Project/Art/TEXTURES/foam.png` | `foam` | `TX_foam` |
| `Assets/_Project/Art/TEXTURES/gameart.png` | `gameart` | `TX_gameart` |
| `Assets/_Project/Art/TEXTURES/menuview.png` | `menuview` | `TX_menuview` |
| `Assets/_Project/Art/TEXTURES/ORGANIC.png` | `ORGANIC` | `TX_ORGANIC` |
| `Assets/_Project/Art/TEXTURES/terrain.png` | `terrain` | `TX_terrain` |

*(Note: The `WorldProceduralFlora/Imported/` subfolder contains textures that also lack `TX_` prefix. The `TX_Coral*` and `TX_Kelp*` assets in `WorldProceduralFlora/` are COMPLIANT.)*

**TOTAL TEXTURE VIOLATIONS (estimated): 50+**

---

### SUMMARY

| Category | Violation Count | Severity |
|----------|----------------|----------|
| Cyrillic in filenames | ~250+ | 🔴 CRITICAL |
| Prefabs missing `PFB_`/`GEN_` | 54 | 🟡 HIGH |
| Materials missing `MAT_` | 19+ | 🟡 HIGH |
| Textures missing `TX_` | 50+ | 🟡 HIGH |

**RECOMMENDATION**: Batch-rename all Cyrillic assets using an Editor script or external tool. Rename materials and prefabs in a dedicated cleanup pass. Texture rename volume is high — prioritize hero/world textures first.
