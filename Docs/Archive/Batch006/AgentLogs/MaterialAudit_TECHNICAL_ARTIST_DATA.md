# Material Audit - TECHNICAL_ARTIST_DATA

Root: `C:/Hecton8/Assets/_Project`
Resolve root: `C:/Hecton8/Assets/_Project`
Sample size: `256`
Include third-party: `False`

## Summary

| Metric | Value |
| --- | --- |
| Textures | 138 |
| Albedo candidates | 26 |
| Albedo energy failures | 0 |
| Albedo energy warnings | 0 |
| Texture read errors | 1 |
| Import issue textures | 5 |
| Estimated texture residency MiB | 497.565 |
| ORM candidates | 17 |
| Detail candidates | 13 |
| Materials | 176 |
| Materials with prompt ORM | 0 |
| Materials with legacy mask | 9 |
| Materials with packed mask | 9 |
| Materials with detail | 0 |
| Materials with issues | 37 |
| Materials with unresolved texture refs | 9 |
| Unresolved texture refs | 27 |
| Channel packing candidates | 31 |

## Gate Exit Codes

| Gate | Exit code |
| --- | --- |
| energy_failures | 1 |
| import_issues | 2 |
| material_issues | 3 |
| unresolved_texture_refs | 4 |
| texture_budget | 5 |
| texture_read_errors | 6 |

## Texture Budget Model

| Metric | Value |
| --- | --- |
| Estimated MiB | 497.565 |
| Budget MiB | 900.0 |
| Warning threshold MiB | 810.0 |
| Used ratio | 0.5528 |
| Status | PASS |

## Channel Packing VRAM Model

| Metric | Value |
| --- | --- |
| Standard MiB/material | 6.65 |
| Optimized MiB/material | 2.99 |
| Candidate standard MiB | 206.15 |
| Candidate optimized MiB | 92.69 |
| Candidate saved MiB | 113.46 |
| Candidate reduction percent | 55.0 |

## GOD_MODE Texture Overrides

| Asset class | TOASTER | DECK | PRO | GOD_MODE | Format | Fallback |
| --- | --- | --- | --- | --- | --- | --- |
| Hero cockpit albedo | 1024 | 2048 | 2048 | 4096 | BC7 sRGB | Demote one mip tier when VRAM used/total > 0.90. |
| Hero cockpit normal | 1024 | 2048 | 2048 | 4096 | BC5 linear | Prefer shared detail normal before unique 4K normal. |
| Hero cockpit ORM | 512 | 1024 | 1024 | 2048 | BC7/BC3 linear | Keep ORM below albedo unless mask aliasing is visible. |
| World module albedo | 1024 | 1024 | 2048 | 2048 | BC7 sRGB | Do not promote all panels; reserve for inspection-radius sets. |
| World module normal | 1024 | 1024 | 2048 | 2048 | BC5 linear | Shared trimsheet normal before unique resolution increase. |
| Terrain albedo | 1024 | 2048 | 2048 | 4096 | BC7/BC1 sRGB | Near hero terrain only; macro terrain stays 2048 tiled. |
| Terrain ORM | 512 | 1024 | 1024 | 2048 | BC7/BC3 linear | Shared packed masks; no separate AO/roughness/metallic. |
| Flora albedo atlas | 1024 | 1024 | 2048 | 2048 | BC7 sRGB | Wire detail overlays before increasing atlas size. |
| Flora detail atlas | 512 | 512 | 1024 | 1024 | BC4/BC5 linear | Global tiling; no per-family duplication above 1024. |
| Decal sheet | 512 | 1024 | 1024 | 1024 | BC7/BC3 | Damage and wear decals outrank raw base-map resolution. |
| Brush/scratch globals | 512 | 1024 | 1024 | 1024 | BC4/BC5 linear | Shared globally across cockpit, habitat, and vehicle materials. |
| Diegetic UI atlas | 1024 | 1024 | 2048 | 2048 | BC7 sRGB | Close-read UI only; regular UI is outside world PBR budget. |

## Global Detail Overlay Plan

| Role | Status | Targets | TOASTER | GOD_MODE | Detail gain % |
| --- | --- | --- | --- | --- | --- |
| fine_cockpit_scratches | MISSING_AUTHORING | Cockpit glass, painted metal, polished hand-contact panels | Disabled except inspection props. | BC4/BC5 1024 overlay at 8x-16x tiling. | 20 |
| panel_dust_grit | MISSING_AUTHORING | Habitat panels, wall seams, low-traffic module floors | Use baked albedo dirt only. | BC4 1024 mask blended into roughness and albedo breakup. | 20 |
| carbon_fiber_weave | MISSING_AUTHORING | Tool grips, suit hardpoints, high-end cockpit inserts | Normal overlay disabled. | BC5 1024 tangent-aligned weave normal. | 25 |
| worn_rubber | MISSING_AUTHORING | Gaskets, grips, boot contact zones, black utility trim | Use scalar roughness only. | BC4 1024 pitted roughness detail plus low-strength normal. | 20 |
| brushed_steel_streaks | MISSING_AUTHORING | Cockpit frames, rails, latches, exposed machined metal | Use anisotropic fake without detail sample. | BC4 1024 directional streak mask for anisotropic fake. | 25 |
| oxidized_aluminum_pitting | MISSING_AUTHORING | Exterior module shells, old brackets, pressure fittings | Baked AO/roughness only. | BC4 1024 pitting mask into roughness and edge darkening. | 20 |
| salt_deposit_speckle | MISSING_AUTHORING | Wet glass edges, flooded doors, submarine exterior seams | Use static decal or base-map stain. | BC4 1024 speckle mask blended by wetness depth. | 20 |
| grease_hand_smudges | MISSING_AUTHORING | Switch panels, handles, lockers, tool drawers | Use low-frequency decal only. | BC4 1024 roughness-darkening overlay in interaction zones. | 20 |
| edge_chipped_paint | MISSING_AUTHORING | Painted industrial panels, doors, crates, railings | Vertex color or baked mask only. | BC4 1024 edge wear mask multiplied by curvature/AO author data. | 20 |
| condensation_micro_droplets | MISSING_AUTHORING | Cold glass, wet acrylic, instrument covers, exterior windows | Disabled; rely on fake clearcoat. | BC5 1024 tiny droplet normal at 0.05-0.12 strength. | 25 |

## Import Issue Counts

| Issue | Count |
| --- | --- |
| DATA_TEXTURE_SRGB_ON | 3 |
| NORMAL_NOT_TEXTURETYPE_NORMAL | 3 |
| NORMAL_SRGB_ON | 3 |

## Material Issue Counts

| Issue | Count |
| --- | --- |
| LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW | 9 |
| NO_DETAIL_MAP_SLOT | 31 |
| NO_PACKED_ORM_OR_MASK_SLOT | 22 |
| NO_PROMPT_ORM_SLOT | 31 |
| UNRESOLVED_TEXTURE_GUID | 9 |

## Detail Candidates

| Path | Import issues |
| --- | --- |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching.v2/detail___family.coral.branching.v2.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/detail___family.coral.brittle.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low/detail___family.coral.low.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/detail___family.coral.massive.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive.2/detail___family.coral.massive.2.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/detail___family.coral.plate.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal/detail___family.kelp.abyssal.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy/detail___family.kelp.canopy.png |  |
| Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/detail___family.kelp.patch.dense.png |  |

## Texture Import Issues

| Path | Issues | Recommendations |
| --- | --- | --- |
| Art/TEXTURES/Detali/Soft Plume Noise - second try.png | DATA_TEXTURE_SRGB_ON | Disable sRGB; data/mask/detail maps must be sampled linear. |
| Art/TEXTURES/Detali/soft_plume_noise_-_kakoy_to_seryy_nu_norm.png | NORMAL_SRGB_ON, NORMAL_NOT_TEXTURETYPE_NORMAL, DATA_TEXTURE_SRGB_ON | Disable sRGB for normal maps.; Set Texture Type to Normal Map.; Disable sRGB; data/mask/detail maps must be sampled linear. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | NORMAL_SRGB_ON, NORMAL_NOT_TEXTURETYPE_NORMAL | Disable sRGB for normal maps.; Set Texture Type to Normal Map. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | NORMAL_SRGB_ON, NORMAL_NOT_TEXTURETYPE_NORMAL | Disable sRGB for normal maps.; Set Texture Type to Normal Map. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | DATA_TEXTURE_SRGB_ON | Disable sRGB; data/mask/detail maps must be sampled linear. |

## Texture Read Errors

| Path | Error |
| --- | --- |
| Scenes/02_HECTON_WORLD/ReflectionProbe-0.exr | cannot identify image file 'C:\\Hecton8\\Assets\\_Project\\Scenes\\02_HECTON_WORLD\\ReflectionProbe-0.exr' |

## Material Slot Issues

| Material | Issues | Recommendations |
| --- | --- | --- |
| Art/Materials/MAT_Diegetic_HUD_V4_Projection.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Mat_GasGiant.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Mat_HectonSky.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/Mat_HectonSky_CloudOverlay.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/Mat_TriplanarRock.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Skybox.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/Snow.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/terrain.mat | UNRESOLVED_TEXTURE_GUID, NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Resolve the texture GUID inside first-party assets or quarantine the external dependency.; Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/Construction/Mat_LeakWetSheen.mat | UNRESOLVED_TEXTURE_GUID | Resolve the texture GUID inside first-party assets or quarantine the external dependency. |
| Art/Materials/Construction/Mat_RuinSeepSheen.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat | NO_PROMPT_ORM_SLOT, LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Review legacy mask/gloss channel order before treating it as prompt ORM.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_arch_large.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_cluster_medium.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_small_floor.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Models/Rocks/Rock 6/rock6/rock_6.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Models/Rocks/Rock 7/Materials/2.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat | UNRESOLVED_TEXTURE_GUID, NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Resolve the texture GUID inside first-party assets or quarantine the external dependency.; Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat | UNRESOLVED_TEXTURE_GUID, NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Resolve the texture GUID inside first-party assets or quarantine the external dependency.; Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_arch_large_Placeholder.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonClouds.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonSurface.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Materials/clouds0_diff.mat | NO_PROMPT_ORM_SLOT, NO_PACKED_ORM_OR_MASK_SLOT, NO_DETAIL_MAP_SLOT | Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved.; Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved.; Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier. |

## Unresolved Material Texture GUIDs

| Material | Unresolved refs |
| --- | --- |
| Art/Materials/Mat_HectonSky.mat | _HighCloudTex:97dacc0c8637b304f9451ecd290acffb; _MainCloudAtlas:161f2ad7f77e8bf408b29aa7e3d29966 |
| Art/Materials/Mat_HectonSky_CloudOverlay.mat | _HighCloudTex:97dacc0c8637b304f9451ecd290acffb; _MainCloudAtlas:161f2ad7f77e8bf408b29aa7e3d29966 |
| Art/Materials/Mat_TriplanarRock.mat | _Rock_Albedo:47f0a231c050423488e0ff6f7d66f813 |
| Art/Materials/Skybox.mat | _BackTex:1227708538876854fa69e8d12b770599; _DownTex:bb8086d1d88f4454291b183304b77a9c; _FrontTex:e4b329141c218da4e85a06e04de20385; _LeftTex:f783f7d87033f2d46aa0213fe99ebeae; _RightTex:c14d76bd0519285429da8d1ca6420ffb; _UpTex:3c564301b9820a14e9455ebdcc20a898 |
| Art/Materials/Snow.mat | _BumpMap:e80c3c84ea861404d8a427db8b7abf04; _ParallaxMap:e80c3c84ea861404d8a427db8b7abf04 |
| Art/Materials/terrain.mat | _BaseMap:47f0a231c050423488e0ff6f7d66f813; _MainTex:47f0a231c050423488e0ff6f7d66f813; _Rock_Albedo:47f0a231c050423488e0ff6f7d66f813 |
| Art/Materials/Construction/Mat_LeakWetSheen.mat | _BumpMap:7d09e7c7fffa5a94eadbfb28633eaa1e; _Normal:a116c6e469d15244ea70d6a8403f52d9; _NormalMap:a116c6e469d15244ea70d6a8403f52d9 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat | _BaseMap:920eb4a5d89009b42a522ff2aa1e6ef3; _BumpMap:5189dcc817bc5e24095d445209499d1f; _MainTex:920eb4a5d89009b42a522ff2aa1e6ef3; _OcclusionMap:df147ac10298ce44e9557850251a533a |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat | _BaseMap:920eb4a5d89009b42a522ff2aa1e6ef3; _BumpMap:5189dcc817bc5e24095d445209499d1f; _MainTex:920eb4a5d89009b42a522ff2aa1e6ef3; _OcclusionMap:df147ac10298ce44e9557850251a533a |

## Texture Memory Hotspots

| Texture | MiB | Role | Size |
| --- | --- | --- | --- |
| Art/Models/Rocks/Rock 7/Materials/2.jpg | 20.345 | BC7_UNKNOWN_8BPP | 4000x4000 |
| Art/TEXTURES/Aegir_storms.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| Art/TEXTURES/clouds.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| Art/TEXTURES/clouds0_diff.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 10.667 | BC7_UNKNOWN_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 10.667 | BC5_NORMAL_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 10.667 | BC7_ALBEDO_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 10.667 | BC5_NORMAL_8BPP | 4096x2048 |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 10.667 | BC7_ORM_LINEAR_8BPP | 4096x2048 |
| Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid_2K_AO.jpg | 5.333 | BC7_ORM_LINEAR_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid_2K_BaseColor.jpg | 5.333 | BC7_ALBEDO_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid_2K_Normal.jpg | 5.333 | BC5_NORMAL_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock012_2K-JPG_AmbientOcclusion.jpg | 5.333 | BC7_UNKNOWN_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock012_2K-JPG_Color.jpg | 5.333 | BC7_ALBEDO_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock012_2K-JPG_NormalGL.jpg | 5.333 | BC5_NORMAL_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock037_2K-JPG_AmbientOcclusion.jpg | 5.333 | BC7_UNKNOWN_8BPP | 2048x2048 |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock037_2K-JPG_Color.jpg | 5.333 | BC7_ALBEDO_8BPP | 2048x2048 |

## Channel Packing Candidates

| Material | Priority | Reason | Mask sources | Has detail |
| --- | --- | --- | --- | --- |
| Art/Materials/MAT_Diegetic_HUD_V4_Projection.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Mat_GasGiant.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/terrain.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/Construction/Mat_RuinSeepSheen.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/mask___family.coral.branching.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/mask___family.coral.brittle.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low/mask___family.coral.low.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/mask___family.coral.massive.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/mask___family.coral.plate.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal/mask___family.kelp.abyssal.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy/mask___family.kelp.canopy.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/mask___family.kelp.patch.dense.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat | MEDIUM | Legacy packed/gloss slot exists, but prompt ORM slot is absent. | _MaskMap:Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/mask___family.kelp.tall.png | False |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_arch_large.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_AmbientOcclusion.jpg | False |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_cluster_medium.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_AmbientOcclusion.jpg | False |
| Art/Materials/WorldProceduralProxy/MAT_family_rock_small_floor.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_AmbientOcclusion.jpg | False |
| Art/Models/Rocks/Rock 6/rock6/rock_6.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid_2K_AO.jpg | False |
| Art/Models/Rocks/Rock 7/Materials/2.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:df147ac10298ce44e9557850251a533a | False |
| Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:df147ac10298ce44e9557850251a533a | False |
| Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_arch_large_Placeholder.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. | _OcclusionMap:Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_AmbientOcclusion.jpg | False |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonClouds.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonSurface.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
| _PROLOGUE_CONTENT/Textures/Planets/pLANET/Materials/clouds0_diff.mat | LOW | Base material has no prompt ORM slot; author or reuse ORM if the material is near-field. |  | False |
