# Asset GUID Active Route Triage - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: compact owner triage derived from `ASSET_GUID_REFERENCE_MATRIX_20260605.csv` for `P0_ACTIVE_ROUTE_REVIEW` and `P1_SCENE_ROUTE_REVIEW` rows.

This is not Unity acceptance. It does not prove import state, material binding, scene instance values, Addressables residency, audio mix behavior, visual quality, GC, frame time, or memory safety.

CSV companion: `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv`.

Known stale slice: current `Assets/_Project/Prefabs/Player.prefab` source-clears prior `Underwater Ambient.wav` and `dive_splash.wav` direct audio refs, while this generated triage still mirrors the older GUID matrix rows for those assets. Use `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` and `Tools/ValidateAudioDirectRefDetail.py` as current Player direct-ref truth until this triage is regenerated.

## Static Summary

| Metric | Count |
|---|---:|
| Triage rows | 800 |
| P0 active-route rows | 655 |
| P1 scene-route rows | 145 |
| Owner lanes | 8 |

## Route Priority Counts

| Route priority | Rows |
|---|---:|
| `P0_ACTIVE_ROUTE_REVIEW` | 655 |
| `P1_SCENE_ROUTE_REVIEW` | 145 |

## Owner Lane Counts

| Owner lane | Rows |
|---|---:|
| `active_world_scriptable_route_owner` | 246 |
| `prefab_scene_route_owner` | 206 |
| `third_party_or_legacy_integrity_owner` | 146 |
| `active_world_material_readback_owner` | 136 |
| `asset_route_assignment_owner` | 32 |
| `audio_direct_ref_owner` | 25 |
| `texture_material_streaming_owner` | 8 |
| `mesh_prefab_model_owner` | 1 |

## Asset Family Counts

| Asset family | Rows |
|---|---:|
| `scriptable_or_native_asset` | 340 |
| `prefab` | 206 |
| `material` | 197 |
| `audio` | 22 |
| `shader_or_compute` | 16 |
| `ui_or_sprite_texture` | 6 |
| `font_asset` | 5 |
| `audio_ui` | 3 |
| `texture` | 2 |
| `animation_or_controller` | 2 |
| `model_mesh_source` | 1 |

## Owner Scope Counts

| Owner scope | Rows |
|---|---:|
| `FIRST_PARTY_PROJECT` | 632 |
| `NON_PROJECT_ASSETS_PATH` | 97 |
| `THIRD_PARTY_FEEL` | 69 |
| `THIRD_PARTY_CREST` | 2 |

## First 25 Rows

| ID | Lane | Family | Asset | Refs | Trigger |
|---|---|---|---|---:|---|
| `GUID_TRIAGE_0001` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Mat_HectonSky.mat` | 21 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|BOOT_OR_MENU_SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0002` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat` | 17 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW|SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `GUID_TRIAGE_0003` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat` | 11 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW|SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `GUID_TRIAGE_0004` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat` | 10 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW|SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `GUID_TRIAGE_0005` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat` | 10 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW|SOURCE_DEMO_PROXY_OR_PLACEHOLDER_PATH` |
| `GUID_TRIAGE_0006` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat` | 4 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0007` | `active_world_material_readback_owner` | `material` | `Assets/Crest/Crest/Materials/Ocean.mat` | 4 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW|THIRD_PARTY_CREST` |
| `GUID_TRIAGE_0008` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_FakeRadarBlipInstanced.mat` | 3 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0009` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat` | 3 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0010` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat` | 3 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0011` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Celestial/MAT_AtmosphericCloudSheet_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0012` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0013` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0014` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0015` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0016` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0017` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8BiomePassiveLifeSilhouette_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0018` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldAbyssRidge_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0019` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldAmberInstrumentPulse_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0020` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldColdFaunaSignal_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0021` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldColdInstrumentPulse_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0022` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldDeepAbyss_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0023` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldDepthCurtain_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0024` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldFaunaSilhouette_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |
| `GUID_TRIAGE_0025` | `active_world_material_readback_owner` | `material` | `Assets/_Project/Art/Materials/MAT_H8WorldPressureVignette_1428.mat` | 2 | `ACTIVE_WORLD_SCENE_REACHABLE|SCENE_REACHABLE|MATERIAL_READBACK_REVIEW` |

## Owner Use

- Start with `audio_direct_ref_owner`, `active_world_material_readback_owner`, `texture_material_streaming_owner`, `prefab_scene_route_owner`, and `active_world_scriptable_route_owner` lanes before broad cleanup.
- Use `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md` for active-world/scriptable/scene rows when Unity gate is clean.
- Use `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md`, `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`, and `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md` for the matching lanes.
- Do not delete unreferenced assets from this triage. This file contains only P0/P1 referenced rows.

## Regression Model

- CPU: static CSV derivation only; no runtime CPU change.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory/VRAM: reachability and source-size pressure only; no resident-memory proof.
- Cadence: no runtime cadence changed.
- Correctness: owner assignment is narrower; Unity/runtime acceptance remains blocked.

Final status: `PENDING_VERIFICATION`.
