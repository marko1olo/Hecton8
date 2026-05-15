# TERRAIN AND BIOME REALITY MAP

CURRENT CANONICAL PATH: `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
ROOT PATH STATUS: compatibility mirror / stale legacy surface. Use the canonical report before trusting any fact below.

Generated: 2026-05-04

ROOT MIRROR WARNING: this root file is a compatibility mirror. Current canonical terrain/biome authority is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.

STATUS: PENDING VERIFICATION. "SOVEREIGN VERIFIED" is not claimed for runtime. Static source audit, edited-script validation, and Unity console error check are verified; Play Mode traversal, 100 km floating-origin soak, and GC capture were not executed.

## Mandates Applied

- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `REND_Instanced_Flora_Physics.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Executive Reality

- The active MapMagic graph is `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`.
- The active graph imports `Assets/MapMagic/Map_Graph/New Gen/USE IT.asset` through `Import200` GUID `0f51089f244a765428731114d01436ab`.
- `heightmap.png` exists in the same folder and is readable, GUID `75ce8c3d31a25d54d8987467de01bb61`, but the active graph does not reference the PNG directly.
- `USE IT.asset` is a binary `Den.Tools.MatrixAsset` at 67,111,124 bytes. Size matches a 4096 x 4096 float matrix plus header. This is likely the hand-drawn map baked into MapMagic matrix form; direct conversion lineage is not stored in text metadata.
- The active graph has no `BiomesSet200` node. The deprecated graph has one. Current runtime 108-biome metadata is not coming from the active MapMagic graph.
- The 108 biome matrix exists as data: 108 matrix profile assets, 108 unique indices, 0 missing indices, 44 placeholder/reserve slots.
- There is no explicit biome `HashID` field in `HectonBiomeMatrixProfile`. Stable identity currently equals `matrixIndex` plus Unity asset GUID. Table below uses the asset GUID as the stable HashID surrogate.
- Scatter does not consume a GPU `NativeArray<byte>` biome influence grid today. It samples through `WorldProceduralFieldSampler`, then scores rules in `WorldProceduralScatterDirector`.
- Terrain-to-voxel seam uses concrete directors and structs, not `ICaveProvider` or `IVoxelBridge`. Those interface names were not found.
- Phase 4 active terrain sweep was executed. Runtime code references to `Terrain.activeTerrain`, `Terrain.activeTerrains`, `Terrain.SampleHeight`, and `TerrainData.GetHeights(` under `Assets/_Project/Scripts` are now zero by May 4 source scan.
- The earlier `WorldGenerativeGeologyTerrainSeamApplier.RefreshTerrainBaseline` `TerrainData.GetHeights(...)` claim is stale for the current source snapshot.

## MapMagic Audit

New Gen assets:

- `ACTUAL TERRAIN.asset` - 302,118 bytes, active-looking current graph, last write 2026-04-19.
- `ACTUAL TERRAIN_GEMINI.asset` - 5,654,899 bytes, experimental large graph, last write 2026-03-25.
- `DEPRECATED.asset` - 129,600 bytes, old graph with `BiomesSet200`, last write 2026-03-15.
- `USE IT.asset` - 67,111,124 bytes, binary matrix asset imported by current graph.
- `Mask_Data2 1.asset` - 67,111,188 bytes, binary matrix asset imported by deprecated graph.
- `Global_Mask_Data.asset` - 16,779,544 bytes, binary matrix asset.
- `heightmap.png` - 10,140,349 bytes, 4096 x 4096 RGBA source texture, readable, single-channel import settings.

Graph node evidence:

- `ACTUAL TERRAIN.asset`: `Import200=1`, `HeightOutput200=1`, `Terrace200=1`, `BiomesSet200=0`, `TexturesOutput200=1`, `Noise200=5`, `Selector200=8`, `Slope200=23`, `Blend200=44`, `Blur200=1`.
- `ACTUAL TERRAIN_GEMINI.asset`: `Import200=4`, `HeightOutput200=3`, `Terrace200=81`, `BiomesSet200=0`, `TexturesOutput200=2`, `Noise200=329`, `Selector200=286`, `Blend200=1111`, `Blur200=216`.
- `DEPRECATED.asset`: `Import200=1`, `HeightOutput200=1`, `Terrace200=1`, `BiomesSet200=1`, `Noise200=7`, `Selector200=4`, `Blend200=15`.

Active graph import:

- `ACTUAL TERRAIN.asset:308` is `MapMagic.Nodes.MatrixGenerators.Import200`.
- `ACTUAL TERRAIN.asset:364` references `USE IT.asset` GUID `0f51089f244a765428731114d01436ab`.
- `ACTUAL TERRAIN.asset:419` is `HeightOutput200`.
- `ACTUAL TERRAIN.asset:3627` is `Terrace200`.
- Active `Terrace200` values: `seed=12345`, `num=20`, `uniformity=0.8`, `steepness=0.2`.

Deprecated graph biome fact:

- `DEPRECATED.asset:2287` is the only found `BiomesSet200` in New Gen.
- Therefore current MapMagic graph does not author the 108 matrix directly. The 108 matrix is in project ScriptableObject data.

## Heightmap Mathematical Profile

Source analyzed: `Assets/MapMagic/Map_Graph/New Gen/heightmap.png`.

Method: grayscale luminance, normalized 0..1, using the readable PNG. World-space slope requires confirmed terrain size and vertical scale; those are not proven from the image alone.

- Size: 4096 x 4096.
- Mode: RGBA.
- Unique gray levels: 256.
- Min: 0.000000.
- Max: 1.000000.
- Mean: 0.344350.
- Standard deviation: 0.311811.
- Percentiles 1/5/10/25/50/75/90/95/99: `0.000000, 0.007843, 0.023529, 0.105882, 0.203922, 0.603922, 0.878431, 0.937255, 0.984314`.
- Gradient percentiles 50/75/90/95/99/99.9: `0.000000, 0.001961, 0.002773, 0.003922, 0.007070, 0.015808`.
- Exact/near-flat gradient ratio: 0.809339.
- Soft-flat gradient ratio: 0.992162.
- Strong edge ratio (`gradient >= 0.03`): 0.000106.
- Significant histogram peak count: 21.
- Dominant gray bins: `0, 3, 39, 38, 30, 7, 40, 5, 41, 37, 32, 10, 42, 31, 6, 33, 43, 36, 35, 1`.
- Significant terrace crossing spacing from center/quarter transects: count 5214, median 3 px, p25 1 px, p75 5 px, p95 15 px.

Target DNA:

- Plateau-dominant.
- High quantization and long flat bands.
- Narrow transitions, not continuous smooth-noise terrain.
- A procedural replacement must use masked plateaus plus terraced quantization, not only Perlin/fBm.

## Biome Master List

Catalog file: `Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset`.

Script types found:

- `HectonBiomeMatrixCatalog` - authoritative 108-slot catalog, array size 108, validates matrix indices 1..108.
- `HectonBiomeMatrixProfile` - per-biome matrix profile with matrix index, tier, region, name, placeholder flag, family, runtime visual profile, and gameplay biases.
- `HectonBiomeRegistry` - separate 108-entry registry type.
- `HectonBiomeProfile`, `HectonBiomeFamilyProfile`, `HectonBiomePlayProfile`, `HectonBiomeLandmarkPlanProfile`, `HectonBiomeSpatialPatternProfile`, `HectonBiomeResourcePlanProfile`, `HectonBiomeResourceChannelProfile`.
- `CaveBiomeTemplate`, `FaunaBiomeData`, `HectonMusicBiomeProfile`, `VFX/BiomeProfile`, `WorldProceduralBiomeFamilyContextProfile`, `WorldProceduralBiomeFamilyContextCatalog`.

No common `BiomeBase` inheritance chain was found. These are ScriptableObject families, not a single biome base hierarchy.

Counts:

- Matrix profiles: 108.
- Unique matrix indices: 108.
- Missing indices: none.
- Placeholder/reserve slots: 44.
- Placeholder indices: `45,46,47,48,49,50,51,52,53,54,55,56,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,85,86,87,88,89,90,91,92,93,94,95,96,101,102,103,104`.

Family distribution:

- `biome.family.abyssal_silt`: 22
- `biome.family.chemosynthetic_brine`: 3
- `biome.family.crystal_growth`: 2
- `biome.family.fossil_reef`: 3
- `biome.family.granite_escarpment`: 6
- `biome.family.littoral_karst`: 2
- `biome.family.metallic_hadal`: 6
- `biome.family.rift_spine`: 14
- `biome.family.rift_void`: 18
- `biome.family.sediment_drift`: 9
- `biome.family.tectonic_spine`: 14
- `biome.family.volcanic_glass`: 4
- `biome.family.volcanic_hadal`: 5

| ID | Stable HashID Surrogate | Tier | Region | Biome | Placeholder | Family | Depth m |
|---:|---|---:|---|---|---:|---|---|
| 1 | 16c4240c933d37548b01111ceffafec9 | 1 | North | Archipelago Needles | 0 | biome.family.littoral_karst | -200..0 |
| 2 | 597e74085c5f89342a0936a6ae2bf243 | 1 | South | Mesa Plateaus | 0 | biome.family.littoral_karst | -200..0 |
| 3 | 8570f458023715b47ac3d7efc20d5fc2 | 1 | East | The Granite Spine | 0 | biome.family.volcanic_glass | -200..0 |
| 4 | 9e3b6d4db40e71e458c985bd5c990b53 | 1 | West | The Silt Tongue | 0 | biome.family.sediment_drift | -200..0 |
| 5 | 8fb0942e2067b934d9e17453c74553ea | 2 | North | Sea-Stack Forest | 0 | biome.family.fossil_reef | 0..300 |
| 6 | b8cea3e25948b1e488575640ab0fc415 | 2 | South | White Alabaster Pools | 0 | biome.family.crystal_growth | 0..300 |
| 7 | 3e1ea733e78740444b0b3ed1263b287a | 2 | East | The Tectonic Chute | 0 | biome.family.tectonic_spine | 0..300 |
| 8 | 82dfa8dffaec8434b802a0a53225c070 | 2 | West | Sand-Fan Deltas | 0 | biome.family.sediment_drift | 0..300 |
| 9 | 1de6d9c4f8615014ba7728c68da9fc2d | 3 | North | Basalt Steps | 0 | biome.family.tectonic_spine | 300..600 |
| 10 | 133a29ce750b9194181ff3c203d14c9f | 3 | South | Meander-Basins | 0 | biome.family.volcanic_glass | 300..600 |
| 11 | 22060d7291ef33a4e92616786363b2bf | 3 | East | Sharp Finned Ridges | 0 | biome.family.tectonic_spine | 300..600 |
| 12 | 6e4c33420ccd3614ebbad80f41c09031 | 3 | West | Coral-Porous Walls | 0 | biome.family.fossil_reef | 300..600 |
| 13 | 5c7a6d105af304e42b1f7f2802646606 | 4 | North | Silt Dunes | 0 | biome.family.sediment_drift | 600..1000 |
| 14 | 80deb9073b7a19a4592e27605b4b7e61 | 4 | South | Pothole Fields | 0 | biome.family.sediment_drift | 600..1000 |
| 15 | 3a21cf37526e6c3458d713943e9737c0 | 4 | East | The Slab Wall | 0 | biome.family.tectonic_spine | 600..1000 |
| 16 | b57e79c9f518db34fbbab9d3148bcf63 | 4 | West | Crystalline Ridges | 0 | biome.family.crystal_growth | 600..1000 |
| 17 | 54a7959bca101a14785d422704f7aed0 | 5 | North | The Great Staircase | 0 | biome.family.tectonic_spine | 1000..1500 |
| 18 | c9712622a79143f469a87edd752ddcaf | 5 | South | Dendritic Erosion Gully | 0 | biome.family.tectonic_spine | 1000..1500 |
| 19 | 00816fbebff086b4bbee8315633f1102 | 5 | East | The Vertical Shadow Wall | 0 | biome.family.granite_escarpment | 1000..1500 |
| 20 | de1ad4617d2ca83498736e0cf881e56c | 5 | West | Table-Land Benches | 0 | biome.family.granite_escarpment | 1000..1500 |
| 21 | fd92068a8d97b2945b486f8e684945bb | 6 | North | Labyrinth Trenches | 0 | biome.family.tectonic_spine | 1500..2000 |
| 22 | 8a3ec51db6b6af545bd791b81fe8b5a5 | 6 | South | Bubble Mound Fields | 0 | biome.family.tectonic_spine | 1500..2000 |
| 23 | 7bde0b8e742a2004a9c699a67a384638 | 6 | East | The Shattered Cliff-Base | 0 | biome.family.granite_escarpment | 1500..2000 |
| 24 | 00956065004ad184ca0cc4800e21c333 | 6 | West | The Silt Cascades | 0 | biome.family.sediment_drift | 1500..2000 |
| 25 | 118c08ca42e21ed4e910057ba693a1b2 | 7 | North | Fracture Slabs | 0 | biome.family.tectonic_spine | 2000..2500 |
| 26 | 8a1aa7574793da04e840d560281f1151 | 7 | South | The Eye of the Abyss | 0 | biome.family.tectonic_spine | 2000..2500 |
| 27 | 55703217ebf658841a8b6b8a680ddf5a | 7 | East | The Wall-Fissures | 0 | biome.family.granite_escarpment | 2000..2500 |
| 28 | 4c95ecea13ed5d24c8db35c766d36782 | 7 | West | Meandering Silt-Rivers | 0 | biome.family.sediment_drift | 2000..2500 |
| 29 | 31a5614bb718dcb4195d794af0a7efd1 | 8 | North | Basalt Prisms | 0 | biome.family.tectonic_spine | 2500..3000 |
| 30 | 99176e0902fef244c9289e2395d1e0f7 | 8 | South | Soft Domes | 0 | biome.family.sediment_drift | 2500..3000 |
| 31 | e244454e666ed814b983453fb6615180 | 8 | East | Spine-Teeth | 0 | biome.family.tectonic_spine | 2500..3000 |
| 32 | 18750b1ffde7d4e4b9e87cf0f1535e45 | 8 | West | Dune-Drains | 0 | biome.family.sediment_drift | 2500..3000 |
| 33 | ab29c68fc7b5572469d9394ccb44c60f | 9 | North | Silt Catacombs | 0 | biome.family.rift_void | 3000..3500 |
| 34 | 7d0f01e5e20a7844281d00e09cd5ccf4 | 9 | South | Fossil Gallows | 0 | biome.family.fossil_reef | 3000..3500 |
| 35 | 07581dcf8ce6cf54c9da005e80980666 | 9 | East | The Granite Maw | 0 | biome.family.granite_escarpment | 3000..3500 |
| 36 | ffe0b5b24d9092f46a0fd501938cd53a | 9 | West | The Flat Margin | 0 | biome.family.sediment_drift | 3000..3500 |
| 37 | 389f06a925e348648a5f447347e97fd0 | 10 | North | Methane Mounds | 0 | biome.family.chemosynthetic_brine | 3500..4025 |
| 38 | 9ad311b7d0f1a6f45b75577a5363464e | 10 | South | The Fluid Seam | 0 | biome.family.tectonic_spine | 3500..4025 |
| 39 | e8f3b6160a12084469eb8783a2dc97d1 | 10 | East | Block-City | 0 | biome.family.abyssal_silt | 3500..4025 |
| 40 | ee83377bc4af55e4b9d42ed2471533e7 | 10 | West | Silt-Void | 0 | biome.family.rift_void | 3500..4025 |
| 41 | 5bcda487325b18b45838c8aa0548942e | 11 | North | Cinder Fields | 0 | biome.family.volcanic_glass | 4025..4550 |
| 42 | 13b28c6617755134c825b30fd0696f04 | 11 | South | Obsidian Flows | 0 | biome.family.volcanic_glass | 4025..4550 |
| 43 | 8a7e8ee203c4a4b43a092ee44b400d98 | 11 | East | Tectonic Shards | 0 | biome.family.tectonic_spine | 4025..4550 |
| 44 | b24f14b36b54f7e4bb667d8195b52bf5 | 11 | West | Fluid Hills | 0 | biome.family.abyssal_silt | 4025..4550 |
| 45 | bc71f3949ffde634cb9b411e0dab7188 | 12 | North | Tier 12 North Reserve | 1 | biome.family.abyssal_silt | 4550..5075 |
| 46 | 283aa8870e2727e48890817cbb3c35a1 | 12 | South | Tier 12 South Reserve | 1 | biome.family.abyssal_silt | 4550..5075 |
| 47 | 9219b4583094ac94bad556e4a3ed4232 | 12 | East | Tier 12 East Reserve | 1 | biome.family.abyssal_silt | 4550..5075 |
| 48 | 511063c98d9f7a54bb3067f34235bdc8 | 12 | West | Tier 12 West Reserve | 1 | biome.family.abyssal_silt | 4550..5075 |
| 49 | 07c86cf0f7dba554abbcb023507be155 | 13 | North | Tier 13 North Reserve | 1 | biome.family.abyssal_silt | 5075..5600 |
| 50 | 73cc2fb180cbb444f849d8474400c1e7 | 13 | South | Tier 13 South Reserve | 1 | biome.family.abyssal_silt | 5075..5600 |
| 51 | dda124d1e606aa0489b5d13548844fc4 | 13 | East | Tier 13 East Reserve | 1 | biome.family.abyssal_silt | 5075..5600 |
| 52 | c952ac0cc813b9b4fae74874bddf16fa | 13 | West | Tier 13 West Reserve | 1 | biome.family.abyssal_silt | 5075..5600 |
| 53 | e4958ee60d5e7fc41ad0bf17be032ae5 | 14 | North | Tier 14 North Reserve | 1 | biome.family.abyssal_silt | 5600..6125 |
| 54 | 62b360d925e38784b8068a916716e971 | 14 | South | Tier 14 South Reserve | 1 | biome.family.abyssal_silt | 5600..6125 |
| 55 | 2f4694eafbee56142a159c6813b47c7a | 14 | East | Tier 14 East Reserve | 1 | biome.family.abyssal_silt | 5600..6125 |
| 56 | 6d3b226dea4ac324fb66eb36e8712236 | 14 | West | Tier 14 West Reserve | 1 | biome.family.abyssal_silt | 5600..6125 |
| 57 | 1e85a41d87474964d969ec2f116da0f2 | 15 | North | The Iron Plains | 0 | biome.family.metallic_hadal | 6125..6650 |
| 58 | b7cea6aadaf631a489430cd8f1eaf6bf | 15 | South | Brine Rivers | 0 | biome.family.chemosynthetic_brine | 6125..6650 |
| 59 | 05ed0e198b3e3024198adb37ddfa4176 | 15 | East | The Black Spine | 0 | biome.family.metallic_hadal | 6125..6650 |
| 60 | b0cb13b70e4a10046a933f773d000b0a | 15 | West | The Silt Shadows | 0 | biome.family.granite_escarpment | 6125..6650 |
| 61 | fd260d667d96cce4a9c995d34741fe30 | 16 | North | Tier 16 North Reserve | 1 | biome.family.abyssal_silt | 6650..7175 |
| 62 | b7ad33383eb631641b208cb640e20fab | 16 | South | Tier 16 South Reserve | 1 | biome.family.abyssal_silt | 6650..7175 |
| 63 | 5d618be6036b331488adc5d35901de73 | 16 | East | Tier 16 East Reserve | 1 | biome.family.abyssal_silt | 6650..7175 |
| 64 | 3868037afec3ee04fbbf4961473bf38e | 16 | West | Tier 16 West Reserve | 1 | biome.family.abyssal_silt | 6650..7175 |
| 65 | d7bfc6356e7396a4ea34c6b2c52b7580 | 17 | North | Tier 17 North Reserve | 1 | biome.family.abyssal_silt | 7175..7700 |
| 66 | 6ed2df0e4b3dced419b6a4aa5424e508 | 17 | South | Tier 17 South Reserve | 1 | biome.family.abyssal_silt | 7175..7700 |
| 67 | c2a349ea9adc6244a879ac65ac2c696e | 17 | East | Tier 17 East Reserve | 1 | biome.family.abyssal_silt | 7175..7700 |
| 68 | f5255b8fd7dbd654290a23e818f80b05 | 17 | West | Tier 17 West Reserve | 1 | biome.family.abyssal_silt | 7175..7700 |
| 69 | 0687b2621fc909d4e8fc9e147217900c | 18 | North | Tier 18 North Reserve | 1 | biome.family.rift_spine | 7700..8225 |
| 70 | a9235561f65b2d548906d796ec085a71 | 18 | South | Tier 18 South Reserve | 1 | biome.family.rift_spine | 7700..8225 |
| 71 | 1cb7d7db0d24bc14cb8b0a1bf4b7249c | 18 | East | Tier 18 East Reserve | 1 | biome.family.rift_spine | 7700..8225 |
| 72 | 3d659790de5cfd144a400782888dd299 | 18 | West | Tier 18 West Reserve | 1 | biome.family.rift_spine | 7700..8225 |
| 73 | ed129cbfe6b65824f96830632175e620 | 19 | North | Tier 19 North Reserve | 1 | biome.family.rift_spine | 8225..8750 |
| 74 | 0bf6b07bb7e5e8648bcc3dd9de36838d | 19 | South | Tier 19 South Reserve | 1 | biome.family.rift_spine | 8225..8750 |
| 75 | d391c43bd4e54bb439087901f9669379 | 19 | East | Tier 19 East Reserve | 1 | biome.family.rift_spine | 8225..8750 |
| 76 | 317c30a2925ea9945a5f98698b2e496f | 19 | West | Tier 19 West Reserve | 1 | biome.family.rift_spine | 8225..8750 |
| 77 | dcf56e2b2901f894bad82a4589e0b2a7 | 20 | North | The Ash-Wastes | 0 | biome.family.volcanic_hadal | 8750..9275 |
| 78 | 946388c5711cf29418d66c0006a36eed | 20 | South | Hydrothermal Spires | 0 | biome.family.chemosynthetic_brine | 8750..9275 |
| 79 | b05f49fbda4332448b86d1037f51d3ef | 20 | East | The Rift-Gates | 0 | biome.family.rift_void | 8750..9275 |
| 80 | 60804b2dc59853c4082d01046788cb9d | 20 | West | Pressure-Slabs | 0 | biome.family.metallic_hadal | 8750..9275 |
| 81 | 6c9493052a977a645883e2c9bd470033 | 21 | North | Iron Shards | 0 | biome.family.metallic_hadal | 9275..9800 |
| 82 | 0cc0d3e0abeb6d64c974149d7cda0964 | 21 | South | Magma Pools | 0 | biome.family.volcanic_hadal | 9275..9800 |
| 83 | 7b666dbed42fb9e4d812ff4f8e2e2a0d | 21 | East | The Shattered Spine | 0 | biome.family.rift_spine | 9275..9800 |
| 84 | b84ab1b48b73a7b44af2a68e47e4804f | 21 | West | The Glass Plains | 0 | biome.family.volcanic_hadal | 9275..9800 |
| 85 | 7abce49e3ae6f98489ee599dc7bbd1c3 | 22 | North | Tier 22 North Reserve | 1 | biome.family.rift_spine | 9800..10325 |
| 86 | dddc939ec71b29048a613e32188c763b | 22 | South | Tier 22 South Reserve | 1 | biome.family.rift_spine | 9800..10325 |
| 87 | fb1ab8eadcd3dcf4a90b6cd073523735 | 22 | East | Tier 22 East Reserve | 1 | biome.family.rift_spine | 9800..10325 |
| 88 | 45fae1aedefdf4c49a38d3344c4ef1e7 | 22 | West | Tier 22 West Reserve | 1 | biome.family.rift_spine | 9800..10325 |
| 89 | b6ad9626377fee74d815b8a8949aa550 | 23 | North | Tier 23 North Reserve | 1 | biome.family.rift_void | 10325..10850 |
| 90 | 431c3645ad40cee49956c624419bc20c | 23 | South | Tier 23 South Reserve | 1 | biome.family.rift_void | 10325..10850 |
| 91 | c36e1c059947e7041994a96b3d558be8 | 23 | East | Tier 23 East Reserve | 1 | biome.family.rift_void | 10325..10850 |
| 92 | 59504d2c4a2c0e64abbf59c81b9652ba | 23 | West | Tier 23 West Reserve | 1 | biome.family.rift_void | 10325..10850 |
| 93 | f3754e92f170ee14dbd34172e0740d68 | 24 | North | Tier 24 North Reserve | 1 | biome.family.rift_void | 10850..11375 |
| 94 | 55a83b8630fcf6e4c9ab4ce286459150 | 24 | South | Tier 24 South Reserve | 1 | biome.family.rift_void | 10850..11375 |
| 95 | d8764c968f14e66449438af44b40f7bb | 24 | East | Tier 24 East Reserve | 1 | biome.family.rift_void | 10850..11375 |
| 96 | 65982e5c81a200d41888088b6642ac10 | 24 | West | Tier 24 West Reserve | 1 | biome.family.rift_void | 10850..11375 |
| 97 | 226e66112546a7943b07bc36d4fd1eb8 | 25 | North | The Shivering Slabs | 0 | biome.family.rift_spine | 11900..12425 |
| 98 | af35b3f22b51a0243b13b46d7d1e15f8 | 25 | South | The Pillow-Lava Hives | 0 | biome.family.volcanic_hadal | 11900..12425 |
| 99 | 2da9a23b7d35c5c49bd890dffe983392 | 25 | East | The Rift-Maw | 0 | biome.family.rift_void | 11900..12425 |
| 100 | d189e2cd353718248a71b1309f4584b8 | 25 | West | The Basalt Flux | 0 | biome.family.rift_void | 11900..12425 |
| 101 | 5a2b39f6f4a36a346aaa31f5bed4e1a0 | 26 | North | Tier 26 North Reserve | 1 | biome.family.rift_void | 11900..12425 |
| 102 | 82fa2f3be3cb4684fa51252c7a32f770 | 26 | South | Tier 26 South Reserve | 1 | biome.family.rift_void | 11900..12425 |
| 103 | 29f77fdd7b98fcd499a2dc24527fea41 | 26 | East | Tier 26 East Reserve | 1 | biome.family.rift_void | 11900..12425 |
| 104 | 951f1eb427fd90745a51491f8c5f27de | 26 | West | Tier 26 West Reserve | 1 | biome.family.rift_void | 11900..12425 |
| 105 | 269bcfca20526914b877b71f212a6b4e | 27 | North | The Iron Peak | 0 | biome.family.metallic_hadal | 14000..15000 |
| 106 | 792208ef349059c49be4b94d9d105d83 | 27 | South | The Lava Seam | 0 | biome.family.volcanic_hadal | 14000..15000 |
| 107 | cc621c83ec4ea0c458b4929af39b7f77 | 27 | East | The Heart of the Rift | 0 | biome.family.rift_void | 14000..15000 |
| 108 | 684183d061fca7440b37d7e3e373e960 | 27 | West | The Static Matrix | 0 | biome.family.metallic_hadal | 14000..15000 |

## Scatter Linkage

Current chain:

1. `WorldProceduralFieldSampler` builds cell samples.
2. It calls `MapMagicBridge.TryGetBiomeIndex(x, z, out biomeIndex)` for a cached MapMagic alphamap-layer index.
3. It also pulls `BiomeMatrixDirector.CurrentProfile` and bakes `HectonBiomeMatrixCatalog` profile data into native arrays.
4. `WorldProceduralScatterDirectorSamplingPipeline` consumes `FieldSample` and scores procedural rules, biome family context, matrix profile bonuses, pattern profile, slope, depth, heat, geology, and budget state.

Result:

- Scatter is not purely hardcoded.
- Scatter is not driven by a 108-id MapMagic biome map.
- MapMagic biome index is an 0-based alphamap dominant layer, capped by `maxBiomeCount` on `MapMagicBridge`; it is not the 1..108 matrix ID.
- The active graph has no `BiomesSet200`, so MapMagic biome index can be absent or texture-layer based rather than true biome-matrix based.

## AUP Synchronization

Stable AUP primitive:

- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:24` defines `AbsoluteUniversePosition`.
- It uses `long GridX/GridY/GridZ` plus `float LocalX/LocalY/LocalZ`.
- Cell size is 5000 m.
- `AUPMath` converts AUP to double absolute and runtime float.

Floating origin:

- `HectonFloatingOrigin.ToAbsoluteUniversePosition(runtime)` returns `runtime + CurrentTotalOffset`.
- `HectonFloatingOrigin.ToRuntimePosition(absolute)` returns `absolute - CurrentTotalOffset`.
- Serialized `_threshold` default is 1000 m, but `RefreshThresholdCache` clamps it to minimum 5000 m.
- Precision watchdog triggers at 5000 m.
- At 100 km, the system should have shifted many times; the runtime coordinate frame should remain near origin. This is source evidence only. No 100 km runtime soak was executed.

MapMagic/geology seam AUP state:

- `WorldGenerativeGeologyIntegrationDirector.TryBuildPlan` samples terrain through `MapMagicBridge.TryGetHeight(runtimeX, runtimeZ)`.
- It converts terrain contact and voxel volume center through `HectonFloatingOrigin.ToAbsoluteUniversePosition`.
- `WorldGenerativeGeologySeamPlan` stores `absoluteUniversePosition`, `absoluteTerrainHeight`, and `absoluteVoxelVolumeCenter`.
- `WorldGenerativeGeologyVoxelBlendRequest` passes `absoluteUniverseCenter` and `absoluteTerrainContactPosition` to voxel bridge.
- `HectonVoxelEngine` still stores generation absolute universe position as `Vector3`, not `AbsoluteUniversePosition`.

Risk:

- High-level persistent systems use int64-grid AUP.
- MapMagic/geology seam transfer still uses Vector3 absolute positions. At 100 km this is tolerable only if origin shift keeps runtime sampling near zero and the absolute Vector3 is not used for high-precision deltas. Runtime proof is missing.

## Terrain-Voxel Seam Map

No `ICaveProvider` or `IVoxelBridge` interface was found.

Actual seam chain:

1. `WorldGenerativeGeologyBinding` supplies terrain seam mode, cave blend mode, slope, cave proximity, radius, terrain cut/raise, and geology metadata.
2. `WorldGenerativeGeologyIntegrationDirector` creates `WorldGenerativeGeologySeamPlan`.
3. `WorldGenerativeGeologyTerrainSeamApplier` applies terrain-side patches for `RequiresTerrainBlend`.
4. `WorldGenerativeGeologySeamExecutionDirector` creates seam proxy geometry and registers `WorldGenerativeGeologyVoxelBlendRequest`.
5. `WorldGenerativeGeologyVoxelBridgeDirector` consumes voxel requests and builds cave nodes, entrances, structures, and runtime voxel volume data.
6. `HectonVoxelEngine.VoxelDensityJob` consumes `NativeArray<float> terrainHeights` and evaluates terrain density plus cave SDF.
7. `VoxelSeamDirector` centralizes seam constants and cave-mouth heuristics.

Seam constants:

- `VoxelSeamDirector.TerrainOverlapMeters = 0.10`.
- `VoxelSeamDirector.SeamTransitionBandMeters = 3.5`.
- `VoxelSeamDirector.CliffSlopeThresholdDegrees = 60`.

Exact height handoff:

- 2D terrain height enters the voxel path as `WorldGenerativeGeologyVoxelBlendRequest.absoluteTerrainContactPosition`.
- Voxel density sampling uses a prefilled `NativeArray<float> terrainHeights` in `VoxelDensityJob.SampleTerrainHeight`.
- Cave mouth creation uses `VoxelSeamDirector.BuildCaveEntrance(request.RuntimeTerrainContactPosition, request.RuntimeCenter, ...)`.

## Biome Influence Grid Proposal

Implement inside the existing scatter pipeline, not as a new scatter owner.

Data contract:

```csharp
public struct BiomeInfluenceCell
{
    public byte PrimaryBiomeId;   // 0 invalid, 1..108 matrix ID
    public byte SecondaryBiomeId; // 0 if none
    public byte Blend255;         // secondary weight
    public byte Flags;            // placeholder/reserve/edge/hazard bits
}
```

CPU path:

- Add `NativeArray<BiomeInfluenceCell>` to existing `WorldProceduralScatterWorkingMemory`.
- Fill it in `WorldProceduralFieldSampler.CellSamplingJob` from resolved `HectonBiomeMatrixProfile.matrixIndex`, not MapMagic alphamap index.
- Use current `WorldZoneDirector` primary/secondary biome blend data where present.
- Keep 0 as invalid and preserve 1..108 for catalog IDs.
- Do not allocate per frame; resize only when scatter cell capacity changes.

GPU path:

- Upload to one persistent `GraphicsBuffer` only when the scatter sampling job swaps a completed snapshot.
- Pack one cell into one `uint` for MX350 bandwidth: `primary | (secondary << 8) | (blend << 16) | (flags << 24)`.
- Compute shaders branch by byte ID or index into a compact biome LUT buffer.
- Do not call `GetData` at runtime.

Failure rules:

- If biome ID is placeholder, use family-level context and set `Flags.Placeholder`.
- If no matrix profile exists, use `PrimaryBiomeId=0` and fallback to current pattern/family scoring.

## Procedural Terracing Kernel

Existing project state:

- MapMagic graph has `Terrace200`.
- Active graph has one terrace node: 20 steps, uniformity 0.8, steepness 0.2.
- No Burst-compatible project-side terrain terrace kernel was found in first-party runtime scripts.

Required replacement target:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float Terrace01(float h, float stepCount, float sharpness, float strength)
{
    float steps = math.max(1f, stepCount);
    float scaled = math.saturate(h) * steps;
    float baseStep = math.floor(scaled);
    float frac = scaled - baseStep;
    float s = math.saturate(sharpness);
    float eased = math.smoothstep(0.5f - s * 0.5f, 0.5f + s * 0.5f, frac);
    float terraced = (baseStep + eased) / steps;
    return math.lerp(h, terraced, math.saturate(strength));
}
```

This is Burst-compatible math. It is not wired today. It must be applied in a terrain matrix generation job or pre-MapMagic custom generator path, then matched against the heightmap DNA above.

## Phase 4 Changes Applied

Files changed:

- `Assets/_Project/Scripts/MapMagicBridge.cs`
  - Removed Unity global terrain lookup from `FindTerrainAt`.
  - Added `CopyResolvedTerrainsTo(Terrain[] destination)` using cached MapMagic `TerrainTile` entries.
  - Removed `Terrain.SampleHeight`; height now uses normalized `TerrainData.GetInterpolatedHeight` after cached tile resolution.
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
  - Removed direct Unity terrain fallback. Seam terrain mutation now requires `MapMagicBridge.TryResolveTerrainAt`.
- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs`
  - Replaced `Terrain.activeTerrains` coverage scan with a fixed `Terrain[64]` buffer populated by `MapMagicBridge.CopyResolvedTerrainsTo`.

Post-edit grep:

- `Terrain.activeTerrain`: 0 runtime code hits.
- `Terrain.activeTerrains`: 0 runtime code hits.
- `Terrain.SampleHeight`: 0 runtime code hits.
- `TerrainData.GetHeights`: 0 runtime code hits by May 4 source scan.

## Biome Transition Protocol

Current state:

- `WorldZoneDirector` already evaluates primary/secondary biome and `_currentBlendFactor`.
- It blends effective densities for near/mid/far bands.
- Atmosphere/underwater visuals listen to MapMagic biome events, but that path is not 108-matrix aware.

Protocol:

1. Source transition from matrix data, not MapMagic texture layers.
2. Emit `BiomeTransitionSample` per player/zone sample:

```csharp
public struct BiomeTransitionSample
{
    public byte FromBiomeId;
    public byte ToBiomeId;
    public byte Blend255;
    public byte Flags;
}
```

3. Fog:
   - Resolve both biome runtime visual profiles.
   - Blend color, density, absorption, turbidity, particulate intensity, and audio wetness over distance.
   - Blend in AUP space; presentation transform is not authority.
4. Flora:
   - Scatter uses the packed biome influence grid.
   - Existing placements fade by biome membership and distance to transition edge.
   - New placements reserve quota from both biome families during the blend window.
5. Persistence:
   - Save dominant biome ID plus transition ID/weight for long-lived flora/fauna sectors.
6. Failure mode:
   - Placeholder biome uses family defaults and sets placeholder flag. No direct placeholder visuals.

## Known Defects And Blockers

- The previous `WorldGenerativeGeologyTerrainSeamApplier` `TerrainData.GetHeights` baseline defect is no longer present by May 4 source scan. Runtime seam traversal and GC proof are still absent.
- `MapMagicBridge.TryGetBiomeIndex` reads dominant alphamap texture layer. It is not a 108-biome matrix sampler.
- Active MapMagic graph has no `BiomesSet200`.
- `HectonBiomeMatrixProfile` lacks an explicit stable `HashID`; asset GUID is the only stable source identity today.
- `WorldProceduralFieldSampler` uses a managed dictionary for biome index cache. It is outside Burst and can be cold, but it is not a native biome influence grid.
- Full CLI build after Unity refresh is blocked by unrelated `AtmosphereDirector` symbol resolution errors in atmosphere/visual systems. Unity console currently reports zero errors.

## Verification

- `rg` MapMagic audit completed for `Assets/MapMagic/Map_Graph/New Gen`.
- Biome profile parse found 108 assets, 108 unique indices, 44 placeholders, 0 missing.
- Heightmap analyzed from `heightmap.png`.
- `rg -n "Terrain\\.activeTerrain|Terrain\\.activeTerrains|Terrain\\.SampleHeight" Assets/_Project/Scripts -g "*.cs"` returned no matches.
- `rg -n "GetHeights\\(" Assets/_Project/Scripts -g "*.cs"` returns no matches in the May 4 source snapshot.
- Unity MCP `validate_script`:
  - `MapMagicBridge.cs`: 0 warnings, 0 errors.
  - `WorldGenerativeGeologyTerrainSeamApplier.cs`: 0 warnings, 0 errors.
  - `World/HectonCrestOceanDepthCacheBootstrap.cs`: 0 warnings, 0 errors.
- Unity MCP world-gen console filters retrieved 0 error entries for `WorldGenerative`, `MapMagic`, `Biome`, and `Terrain`.
- Unity MCP global error query later retrieved one unrelated bootstrap error: `BIOS ERROR 0xBOOT_TIMEOUT` at `Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs:200`. This is not a world-gen script error, but it prevents a truthful whole-console zero-error claim.

MCP CONSOLE LOG: `read_console(types=["error"], filter_text="WorldGenerative|MapMagic|Biome|Terrain equivalents")` returned 0 world-gen error entries by separate filters. Whole-console errors are not zero because of the unrelated bootstrap timeout above.
