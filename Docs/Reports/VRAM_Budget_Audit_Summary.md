# VRAM Budget Audit Summary

Generated: 2026-05-15T04:48:12
Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.

## Summary

- Texture files scanned: 1645
- Mesh files scanned: 301
- Total BC7 no-mip estimate: 961.85 MiB
- Total BC7 full-mip estimate: 1282.47 MiB
- Runtime-candidate BC7 full-mip estimate: 1251.24 MiB
- First-party production BC7 full-mip estimate: 504.62 MiB
- MX350 texture budget: 900 MiB
- Critical overflow trigger: 1228.8 MiB
- [CRITICAL_VRAM_OVERFLOW] All scanned textures exceed 1.2GB static full-mip BC7 threshold.
- [CRITICAL_VRAM_OVERFLOW] Runtime-candidate textures exceed 1.2GB static full-mip BC7 threshold.
- Texture VRAM crime rows: 800
- Mesh redline/risk rows: 293
- Mesh importer risk rows: 293
- First-party mesh importer risk rows: 16
- First-party large textures with streaming mips off: 50
- link.xml status: LINK_XML_PRESENT_STATIC_ONLY

## Top First-Party Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 11 | 50.69 | 3 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall | 4 | 21.33 | 0 |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials | 1 | 20.35 | 1 |

## Top Runtime-Candidate Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/ScifiFacility/Textures | 67 | 483.67 | 11 |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 11 | 50.69 | 3 |
| Assets/Screenshots | 100 | 43.62 | 0 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt | 4 | 34.28 | 4 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMProtoTextures | 24 | 32.00 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal | 4 | 21.33 | 0 |

## Top Runtime Texture Costs

| Path | Size | BC7 full mip MiB | Flags |
|---|---:|---:|---|
| Assets/MapMagic/Map_Graph/New Gen/heightmap.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;VRAM CRIME: UNCOMPRESSED_RGBA32_STATIC_SUSPECT;STREAMING_MIPMAPS_OFF_LARGE;READ_WRITE_ENABLED_LARGE_STATIC_SUSPECT |
| Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Base_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Base_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/DetailSheet_mask.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/DetailSheet_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Transparent_basecolor.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Transparent_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg | 4000x4000 | 20.35 | VRAM CRIME: TEXTURE_GT_2048 |
| Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png | 3072x3072 | 12.00 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;VRAM CRIME: UNCOMPRESSED_RGBA32_STATIC_SUSPECT;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/Aegir_storms.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/clouds.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/sphere_basecolor.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt1.png | 3840x2160 | 10.55 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |

## Mesh Redlines

| Path | File MiB | Triangles | LOD | Readable | Compression | BlendShapes | Flags |
|---|---:|---:|---:|---:|---:|---:|---|
| Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx | 2.20 | 127645 | false | 0 | 0 | 1 | MESH_GT_80K_ABSOLUTE_STATIC;MESH_REDLINE_GT_50K_NO_LOD;MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/viewing_deck.fbx | 0.45 | 12778 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx | 1.90 | 10000 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_b.fbx | 0.75 | 7377 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx | 2.59 | 6519 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_h.fbx | 0.29 | 5388 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_14.fbx | 0.22 | 5189 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx | 0.23 | 5000 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Forest_Rock_Shelf_wgpqfjl_Mid.fbx | 0.18 | 4038 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_10_base.fbx | 0.11 | 3952 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door.fbx | 0.19 | 3540 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door_b.fbx | 0.18 | 3468 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/Shapes/Models/shapes_primitives.fbx | 0.09 | 3222 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_11.fbx | 0.11 | 3090 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx | 0.11 | 3054 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_15.fbx | 0.18 | 2999 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_02.fbx | 0.11 | 2688 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/chair_01.fbx | 0.15 | 2548 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/bed_02.fbx | 0.13 | 2243 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/chair_02.fbx | 0.14 | 2234 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_02.fbx | 0.09 | 2228 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/stairs_01.fbx | 0.11 | 2176 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_Formation_vd4iecjva_Low.fbx | 0.08 | 2100 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_03.fbx | 0.11 | 2000 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_13.fbx | 0.09 | 1992 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_03_b.fbx | 0.15 | 1964 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_11_base.fbx | 0.09 | 1920 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_01.fbx | 0.09 | 1880 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_10.fbx | 0.09 | 1858 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_c.fbx | 0.15 | 1788 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_04.fbx | 0.09 | 1594 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_05.fbx | 0.22 | 1582 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_01.fbx | 0.33 | 1535 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_d.fbx | 0.09 | 1518 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door.fbx | 0.25 | 1407 | false | 1 | 2 | 0 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/tubes/tube_03.fbx | 0.12 | 1337 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/keyboard_b.fbx | 0.05 | 1314 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/keyboard.fbx | 0.05 | 1314 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |

## Atlas Suggestions

| Group | Count | Combined BC7 MiB | Members |
|---|---:|---:|---|
| Assets/_Project/Art/TEXTURES/Detali | 7 | 7.00 | bubble vent atlas - bad - redo.png, mineral seep mask - looks seamless.png, Mineral Seep Mask - second try.png, Soft Plume Noise - second try.png, soft_plume_noise_-_kakoy_to_seryy_nu_norm.png, visor droplet mask.png, visor runoff normal.png |
| Assets/_Project/Art/Sprites/ui | 6 | 6.00 | BATTERY.png, COPPER.png, CUTTER.png, MICRO.png, OXYGEN.png, TITANIUM.png |
| Assets/_Project/Art/TEXTURES | 4 | 4.00 | FLOOR.png, FLOOR1.png, ORGANIC.png, terrain.png |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching.v2 | 4 | 4.00 | albedo___family.coral.branching.v2.png, detail___family.coral.branching.v2.png, mask___family.coral.branching.v2.png, normal___family.coral.branching.v2.png |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle | 4 | 4.00 | albedo___family.coral.brittle.png, detail___family.coral.brittle.png, mask___family.coral.brittle.png, normal___family.coral.brittle.png |

## Low-Tier Halving Candidates

| Path | Source | Est. full-mip MiB saved by halving | Rationale |
|---|---:|---:|---|
| Assets/MapMagic/Map_Graph/New Gen/heightmap.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/DetailSheet_mask.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/DetailSheet_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Transparent_basecolor.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Transparent_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg | 4000x4000 | 15.26 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png | 3072x3072 | 9.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |

## link.xml Check

- Assets/AstarPathfindingProject/link.xml assemblies=1 types=18 preserve_all=18
- Assets/link.xml assemblies=2 types=10 preserve_all=10
- Assets/Plugins/Sirenix/Assemblies/link.xml assemblies=4 types=0 preserve_all=4

## Evidence Boundary

- STATIC_SOURCE: file dimensions, file sizes, source metadata, and parser-readable mesh triangle counts.
- Scan excludes generated/scratch directories by name: .codex-artifacts, .codex-build, .git, .vs, Build, Builds, Library, Obj, Temp.
- PENDING VERIFICATION: Unity importer compression, actual texture residency, mesh import settings, Memory Profiler VRAM, scene wiring, player-build behavior.
