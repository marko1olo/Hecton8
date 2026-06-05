# Asset GUID Reference Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: static GUID reachability for asset-like files under `Assets/`: textures, audio/music, models, materials, prefabs, scenes, scriptable assets, shader/VFX assets, sprite atlases, fonts, physics materials, and haptic assets.
First-20 route moment: removes blind asset routing for first exit, ocean/shoreline, sky/Aegir, photic shallows, HUD oxygen, player audio, and medium-depth dressing.

This file is not Unity acceptance. GUID references prove only serialized text reachability. They do not prove import settings, Addressables residency, material binding, runtime load/release, visual quality, audio mix behavior, GC, frame time, or memory safety.

CSV companion: `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv`.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`

## Static Summary

| Metric | Count |
|---|---:|
| Matrix rows | 7420 |
| Referenced assets | 3932 |
| Unreferenced static assets | 3488 |
| Active `02_HECTON_WORLD` reachable assets | 630 |
| Direct audio scene/prefab review rows | 25 |
| Non-first-party or legacy path rows | 3090 |
| Source/demo/proxy/placeholder path rows | 758 |
| Large texture source rows >= 8 MB | 46 |
| Large audio source rows >= 10 MB | 12 |

## Route Priority Counts

| Route priority | Rows |
|---|---:|
| `P3_STATIC_SOURCE` | 3490 |
| `P2_SERIALIZED_ROUTE_REVIEW` | 3130 |
| `P0_ACTIVE_ROUTE_REVIEW` | 655 |
| `P1_SCENE_ROUTE_REVIEW` | 145 |

## Asset Family Counts

| Asset family | Rows |
|---|---:|
| `scriptable_or_native_asset` | 3040 |
| `material` | 1112 |
| `texture` | 964 |
| `prefab` | 943 |
| `shader_or_compute` | 712 |
| `model_mesh_source` | 300 |
| `audio_music` | 84 |
| `texture_normal_candidate` | 62 |
| `scene` | 53 |
| `audio` | 48 |
| `texture_mask_candidate` | 38 |
| `font_asset` | 27 |
| `ui_or_sprite_texture` | 22 |
| `animation_or_controller` | 8 |
| `audio_ui` | 5 |
| `render_asset` | 1 |
| `audio_ambient` | 1 |

## Owner Scope Counts

| Owner scope | Rows |
|---|---:|
| `FIRST_PARTY_PROJECT` | 4330 |
| `NON_PROJECT_ASSETS_PATH` | 2654 |
| `THIRD_PARTY_FEEL` | 290 |
| `THIRD_PARTY_CREST` | 138 |
| `THIRD_PARTY_PLUGINS` | 7 |
| `LEGACY_RESOURCES` | 1 |

## High-Signal Policy Flags

| Policy flag | Rows |
|---|---:|
| `UNREFERENCED_STATIC_ASSET` | 3488 |
| `NON_PROJECT_ASSETS_PATH` | 2654 |
| `PREFAB_REACHABLE` | 1848 |
| `SCRIPTABLE_ASSET_REACHABLE` | 1199 |
| `SCENE_REACHABLE` | 775 |
| `SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` | 758 |
| `ACTIVE_WORLD_SCENE_REACHABLE` | 630 |
| `MATERIAL_REACHABLE` | 475 |
| `MATERIAL_READBACK_REVIEW` | 374 |
| `THIRD_PARTY_FEEL` | 290 |
| `MODEL_ROUTE_REVIEW` | 258 |
| `TEXTURE_BINDING_REVIEW` | 215 |
| `PREFAB_SCENE_ROUTE_REVIEW` | 206 |
| `THIRD_PARTY_CREST` | 138 |
| `LARGE_TEXTURE_SOURCE_BYTES` | 46 |
| `BOOT_OR_MENU_SCENE_REACHABLE` | 40 |
| `DIRECT_AUDIO_REF_REVIEW` | 25 |
| `LARGE_AUDIO_SOURCE_BYTES` | 12 |
| `THIRD_PARTY_PLUGINS` | 7 |
| `LEGACY_RESOURCES` | 1 |

## Top Referenced Assets

| Priority | Family | Asset | Refs | Flags |
|---|---|---|---:|---|
| `P2_SERIALIZED_ROUTE_REVIEW` | `material` | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows.mat` | 203 | `MATERIAL_READBACK_REVIEW; PREFAB_REACHABLE; SCRIPTABLE_ASSET_REACHABLE; SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `prefab` | `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/SmallPassiveProxy.prefab` | 114 | `SCRIPTABLE_ASSET_REACHABLE; SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_creature_spawn_passive.asset` | 111 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_rock_small_floor.asset` | 111 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_pocket_resource.asset` | 102 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_cave_entrance.asset` | 95 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_debris_scatter.asset` | 95 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_creature_spawn_predator.asset` | 91 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_pocket_hazard.asset` | 91 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `prefab` | `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/TerritorialProxy.prefab` | 90 | `SCRIPTABLE_ASSET_REACHABLE; SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `material` | `Assets/ScifiFacility/Materials/Base_Metal_Elox_01.mat` | 86 | `MATERIAL_READBACK_REVIEW; NON_PROJECT_ASSETS_PATH; PREFAB_REACHABLE` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/AI/CreatureArchetypes/Territorial/Archetype_ArchwaySentinel.asset` | 84 | `SCRIPTABLE_ASSET_REACHABLE` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `material` | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat` | 81 | `MATERIAL_READBACK_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_debris_field.asset` | 74 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_landmark_spire.asset` | 74 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `prefab` | `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HunterProxy.prefab` | 68 | `SCRIPTABLE_ASSET_REACHABLE; SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_rock_arch_large.asset` | 68 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_sediment_drift.asset` | 57 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_ruin_module_single.asset` | 57 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P2_SERIALIZED_ROUTE_REVIEW` | `prefab` | `Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HeavyHunterProxy.prefab` | 55 | `SCRIPTABLE_ASSET_REACHABLE; SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |

## Active World Reachability Sample

| Priority | Family | Asset | Refs | Flags |
|---|---|---|---:|---|
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_creature_spawn_passive.asset` | 111 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_rock_small_floor.asset` | 111 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_pocket_resource.asset` | 102 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_cave_entrance.asset` | 95 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_debris_scatter.asset` | 95 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_creature_spawn_predator.asset` | 91 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_pocket_hazard.asset` | 91 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_debris_field.asset` | 74 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_landmark_spire.asset` | 74 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_rock_arch_large.asset` | 68 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_sediment_drift.asset` | 57 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_ruin_module_single.asset` | 57 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_abyssal_silt.asset` | 50 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_service_scar.asset` | 48 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_chemosynthetic_brine.asset` | 47 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_pocket_safe.asset` | 45 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_rift_void.asset` | 43 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_metallic_hadal.asset` | 42 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_fossil_reef.asset` | 39 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_ruin_megastructure.asset` | 39 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_rift_spine.asset` | 38 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_littoral_karst.asset` | 36 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_tectonic_spine.asset` | 34 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_volcanic_hadal.asset` | 32 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_volcanic_glass.asset` | 31 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_granite_escarpment.asset` | 28 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/Biomes/FamilyProfiles/BiomeFamilyProfile_biome_family_crystal_growth.asset` | 27 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `material` | `Assets/_Project/Art/Materials/Mat_HectonSky.mat` | 21 | `ACTIVE_WORLD_SCENE_REACHABLE; BOOT_OR_MENU_SCENE_REACHABLE; MATERIAL_READBACK_REVIEW; PREFAB_REACHABLE; SCENE_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_coral_low.asset` | 19 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `scriptable_or_native_asset` | `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_coral_massive.asset` | 19 | `ACTIVE_WORLD_SCENE_REACHABLE; SCENE_REACHABLE; SCRIPTABLE_ASSET_REACHABLE` |

## Direct Audio Reference Sample

| Priority | Family | Asset | Refs | Flags |
|---|---|---|---:|---|
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (1).ogg` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (2).ogg` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (3).ogg` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Default/default step (gravel - mud) (4).ogg` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Metal/metal step (1).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Metal/metal step (2).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Metal/metal step (3).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Metal/metal step (4).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Rock/rock step (1).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Rock/rock step (2).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Rock/rock step (3).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Rock/rock step (4).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Sand/sand step  (1).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Sand/sand step  (2).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Sand/sand step  (3).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Sand/sand step  (4).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Wet/wet step (1).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Wet/wet step (2).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Wet/wet step (3).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |
| `P0_ACTIVE_ROUTE_REVIEW` | `audio` | `Assets/_Project/Audio/Footsteps/Wet/wet step (4).wav` | 1 | `DIRECT_AUDIO_REF_REVIEW; PREFAB_REACHABLE` |

## Owner Use

- Texture/material owners use rows with `TEXTURE_BINDING_REVIEW`, `MATERIAL_REACHABLE`, `ACTIVE_WORLD_SCENE_REACHABLE`, `LARGE_TEXTURE_SOURCE_BYTES`, and `TEXTURE_MATERIAL_OR_STREAMING_OWNER` before touching import settings or material bindings.
- Audio owners use rows with `DIRECT_AUDIO_REF_REVIEW`, `LARGE_AUDIO_SOURCE_BYTES`, `AUDIO_LIFECYCLE_OR_SOURCE_OWNER`, and `MusicDirector` references before touching prefab audio refs, profiles, or import policy.
- Mesh/prefab owners use `MODEL_ROUTE_REVIEW`, `PREFAB_SCENE_ROUTE_REVIEW`, and `MESH_PREFAB_OWNER` rows before prefab/model promotion.
- Streaming/Addressables owners use all `P0_ACTIVE_ROUTE_REVIEW` and `P1_SCENE_ROUTE_REVIEW` rows as the minimum static candidate set for group/key/lifecycle planning.
- Third-party integrity owners use `THIRD_PARTY_*`, `LEGACY_RESOURCES`, and `LEGACY_ASTAR_CONTAMINATION` rows to separate vendor quarantine from first-party asset work.

## Evidence Boundary

- `reference_file_count` counts distinct selected serialized files containing the GUID token.
- `reference_token_count` counts GUID token occurrences in selected serialized files.
- `first_reference_paths` is a short sample, not the full graph.
- Binary importer state, effective shader slots, SpriteAtlas packing, AudioImporter settings, Addressables labels, material overrides, scene instance values, and runtime residency remain unproven.

## Low / Middle / High / Ultra Consequences

- Low: this matrix helps identify active-route texture/audio/model rows that must keep readability and survival cues before decorative assets compete for memory.
- Middle: referenced material/prefab rows become the primary owner queue for improving first-20 visual/audio identity without broad blind imports.
- High: saved budgets should buy richer referenced material, sky, shoreline, flora, and audio detail after owner readback, not extra unreferenced source imports.
- Ultra: longer LOD/residency and denser ambience can be planned only after these GUID routes are converted into Addressables/release/memory proof.

## Regression Model

- CPU: static scan only; no runtime CPU change.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory/VRAM: file size and reachability only; no resident-memory proof.
- Cadence: no runtime cadence changed.
- Correctness: future owners now have a static GUID graph; acceptance remains blocked by Unity/readback/runtime proof.

Final status: `PENDING_VERIFICATION`.
