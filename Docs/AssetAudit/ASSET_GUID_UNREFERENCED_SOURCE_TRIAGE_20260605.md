# Asset GUID Unreferenced Source Triage - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: unreferenced asset-like GUID rows derived from `ASSET_GUID_REFERENCE_MATRIX_20260605.csv`.

This file is not deletion authorization. It proves only that selected serialized reference files did not contain these GUID tokens in the static scan. It does not prove Unity import state, runtime use through code, Addressables labels, AssetBundle membership, editor-only use, reflection use, Resources use, visual quality, audio quality, or safety to delete.

CSV companion: `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv`.

Post-cleanup note 2026-06-06: the three `02_HECTON_WORLD_BISECT_*_1428` debug scenes, the non-build sandbox scenes `GeminiSandbox`, `XXX_SANDBOX`, `X_GPUSANDBOX`, `XX_SANDBOX_MASUM`, `03_HECTON_SANDBOX_BIOMES`, and the sandbox-only `HECTON_SANDBOX_BIOMES_BAKED_PREVIEW` asset were removed after owner cleanup review. They were not in `ProjectSettings/EditorBuildSettings.asset`, active scene routing still targets production scenes, and this file remains a historical static snapshot rather than a live deletion queue.

Post-cleanup note 2026-06-06B: `Assets/_Project/Diagnostics/auto_baseline_test.raw`, `Assets/_Project/Scenes/_Temp/FloraBeautyAudit_TMP.unity`, and `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_OldAmberPaint.mat` were removed after a focused GUID-reference recheck found no live serialized references outside historical reports/audit rows. `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`, `Assets/_Project/Art/Models/Sandbox/Coral_Albedo.png`, and `Assets/_Project/Art/Models/Sandbox/Coral_Normal.png` were intentionally retained because current static routing still marks them as active route or pending source-candidate material.

## Static Summary

| Metric | Count |
|---|---:|
| Unreferenced triage rows | 3488 |
| Action buckets | 9 |
| Owner scopes | 6 |
| Rows >= 8 MB source size | 31 |

## Action Bucket Counts

| Action bucket | Rows |
|---|---:|
| `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | 2203 |
| `UNREFERENCED_STATIC_SOURCE_REVIEW` | 594 |
| `PREFAB_UNUSED_SOURCE_REVIEW` | 226 |
| `SOURCE_PROXY_PLACEHOLDER_QUARANTINE_REVIEW` | 162 |
| `SHADER_UNUSED_SOURCE_REVIEW` | 132 |
| `MATERIAL_UNUSED_SOURCE_REVIEW` | 92 |
| `TEXTURE_UNUSED_SOURCE_REVIEW` | 51 |
| `AUDIO_UNUSED_SOURCE_REVIEW` | 27 |
| `MODEL_UNUSED_SOURCE_REVIEW` | 1 |

## Asset Family Counts

| Asset family | Rows |
|---|---:|
| `scriptable_or_native_asset` | 843 |
| `texture` | 802 |
| `material` | 719 |
| `prefab` | 537 |
| `shader_or_compute` | 413 |
| `scene` | 53 |
| `model_mesh_source` | 42 |
| `audio` | 24 |
| `texture_normal_candidate` | 18 |
| `font_asset` | 16 |
| `texture_mask_candidate` | 8 |
| `ui_or_sprite_texture` | 7 |
| `animation_or_controller` | 3 |
| `audio_ui` | 2 |
| `audio_ambient` | 1 |

## Owner Scope Counts

| Owner scope | Rows |
|---|---:|
| `NON_PROJECT_ASSETS_PATH` | 1951 |
| `FIRST_PARTY_PROJECT` | 1285 |
| `THIRD_PARTY_FEEL` | 128 |
| `THIRD_PARTY_CREST` | 116 |
| `THIRD_PARTY_PLUGINS` | 7 |
| `LEGACY_RESOURCES` | 1 |

## Largest Unreferenced Rows

| ID | Bucket | Family | Scope | Size bytes | Asset |
|---|---|---|---|---:|---|
| `UNREF_GUID_0697` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scene` | `FIRST_PARTY_PROJECT` | 33762760 | `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_MONOBEHAVIOURS_1428.unity` |
| `UNREF_GUID_0698` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scene` | `FIRST_PARTY_PROJECT` | 33753560 | `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_HEAVY_BOOT_1428.unity` |
| `UNREF_GUID_0699` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scene` | `FIRST_PARTY_PROJECT` | 33753016 | `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_TERRAIN_PROCEDURAL_1428.unity` |
| `UNREF_GUID_0001` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 26496044 | `Assets/_Project/Audio/Atmos 1.wav` |
| `UNREF_GUID_0002` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 26112044 | `Assets/_Project/Audio/Atmos 2.wav` |
| `UNREF_GUID_0003` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 24960044 | `Assets/_Project/Audio/Atmos 3.wav` |
| `UNREF_GUID_0004` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 24768044 | `Assets/_Project/Audio/Atmos 4.wav` |
| `UNREF_GUID_0005` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 24576044 | `Assets/_Project/Audio/Atmos 1 Loop.wav` |
| `UNREF_GUID_0006` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 24576044 | `Assets/_Project/Audio/Atmos 2 Loop.wav` |
| `UNREF_GUID_3225` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `texture_mask_candidate` | `NON_PROJECT_ASSETS_PATH` | 24490400 | `Assets/ScifiFacility/Textures/Base_05_dirt_roughness.png` |
| `UNREF_GUID_0007` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 24384044 | `Assets/_Project/Audio/Atmos 5.wav` |
| `UNREF_GUID_0008` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 23040044 | `Assets/_Project/Audio/Atmos 3 Loop.wav` |
| `UNREF_GUID_0009` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 23040044 | `Assets/_Project/Audio/Atmos 4 Loop.wav` |
| `UNREF_GUID_0010` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 23040044 | `Assets/_Project/Audio/Atmos 5 Loop.wav` |
| `UNREF_GUID_2514` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `texture` | `NON_PROJECT_ASSETS_PATH` | 20045037 | `Assets/ScifiFacility/Textures/Fabric_basecolor_dirt_only.png` |
| `UNREF_GUID_2195` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `scriptable_or_native_asset` | `NON_PROJECT_ASSETS_PATH` | 16779544 | `Assets/MapMagic/Map_Graph/New Gen/Global_Mask_Data.asset` |
| `UNREF_GUID_0692` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `font_asset` | `FIRST_PARTY_PROJECT` | 16467736 | `Assets/_Project/Art/Fonts/NotoSansCJKjp-Regular.otf` |
| `UNREF_GUID_0693` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `font_asset` | `FIRST_PARTY_PROJECT` | 16437364 | `Assets/_Project/Art/Fonts/NotoSansCJKsc-Regular.otf` |
| `UNREF_GUID_0011` | `AUDIO_UNUSED_SOURCE_REVIEW` | `audio` | `FIRST_PARTY_PROJECT` | 15876044 | `Assets/_Project/Audio/Breathing/inside suit sounds (too loud).wav` |
| `UNREF_GUID_2515` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `texture` | `NON_PROJECT_ASSETS_PATH` | 12582956 | `Assets/ScifiFacility/Textures/Lights_emissive.tga` |
| `UNREF_GUID_3230` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `texture_normal_candidate` | `NON_PROJECT_ASSETS_PATH` | 11665539 | `Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png` |
| `UNREF_GUID_0709` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scriptable_or_native_asset` | `FIRST_PARTY_PROJECT` | 11185863 | `Assets/_Project/Art/TEXTURES/TX_H8SurfaceGasGiantDisc_1428.asset` |
| `UNREF_GUID_0710` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scriptable_or_native_asset` | `FIRST_PARTY_PROJECT` | 10836187 | `Assets/_Project/Art/Models/Baked/arka_2.asset` |
| `UNREF_GUID_0676` | `TEXTURE_UNUSED_SOURCE_REVIEW` | `texture_normal_candidate` | `FIRST_PARTY_PROJECT` | 10160588 | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock037_2K-JPG_NormalGL.jpg` |
| `UNREF_GUID_2516` | `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` | `texture` | `NON_PROJECT_ASSETS_PATH` | 10140349 | `Assets/MapMagic/Map_Graph/New Gen/heightmap.png` |
| `UNREF_GUID_0700` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scene` | `FIRST_PARTY_PROJECT` | 9611016 | `Assets/_Project/Scenes/GeminiSandbox.unity` |
| `UNREF_GUID_0711` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scriptable_or_native_asset` | `FIRST_PARTY_PROJECT` | 9599517 | `Assets/_Project/Art/Models/Baked/krugovaya.asset` |
| `UNREF_GUID_0677` | `TEXTURE_UNUSED_SOURCE_REVIEW` | `texture_normal_candidate` | `FIRST_PARTY_PROJECT` | 9225367 | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock012_2K-JPG_NormalGL.jpg` |
| `UNREF_GUID_0641` | `TEXTURE_UNUSED_SOURCE_REVIEW` | `texture` | `FIRST_PARTY_PROJECT` | 9215950 | `Assets/_Project/Art/TEXTURES/Sky/eb2.png` |
| `UNREF_GUID_0712` | `UNREFERENCED_STATIC_SOURCE_REVIEW` | `scriptable_or_native_asset` | `FIRST_PARTY_PROJECT` | 8533341 | `Assets/_Project/Art/Models/Baked/arka1.asset` |

## Owner Use

- Use this after the active-route triage, not before it. Active route blockers outrank cleanup.
- Treat `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` as quarantine review, not deletion.
- Treat `SOURCE_PROXY_PLACEHOLDER_QUARANTINE_REVIEW` as source isolation review, not visible-route cleanup proof.
- Deletion of `.cs`, `.shader`, `.asset`, textures, audio, prefabs, or metas requires a separate explicit owner task, exact route proof, paired `.meta` handling, and post-delete orphan scan.

## Regression Model

- CPU: static CSV derivation only; no runtime CPU change.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory/VRAM: source size pressure is static only; no resident-memory proof.
- Cadence: no runtime cadence changed.
- Correctness: cleanup candidates are isolated from active-route work; no deletion or runtime acceptance is implied.

Final status: `PENDING_VERIFICATION`.
