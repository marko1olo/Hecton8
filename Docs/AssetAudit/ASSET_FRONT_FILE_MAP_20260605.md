# Asset Front File Map - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: navigation map for asset-front documents generated or integrated during the 2026-06-05 asset run.

Use this map to avoid bulk-reading unrelated reports. It does not prove Unity/runtime state.

CSV companion: `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv`.

## Entry Files

| File | Use |
|---|---|
| `Docs/AssetAudit/README.md` | Start-here guardrails, P0 blockers, hard rejections, owner map, process gate. |
| `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.md/.csv` | Compact P0/P1 dispatch board. |
| `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.md/.csv` | Cross-domain static row-risk dispatch summary. |
| `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md` | Long-form asset system index and dispositions. |
| `Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md` | Controller-level synthesis and verified static row counts. |
| `taskslocal/asset_system_20260605/README.md` | Local task packet index and owner dispatch order. |

## Detail Files By Domain

| Domain | Primary Files |
|---|---|
| texture/material | `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.*`, `TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.*`, `TEXTURE_DUPLICATE_HASH_MATRIX_20260605.*`, `TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.*`, `TEXTURE_IMPORT_ROLE_MATRIX_20260605.*`, `MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.*`, `TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`, `TEXTURE_CANDIDATE_DISPOSITION_20260605.csv` |
| visual/mesh/prefab | `VISUAL_MESH_ASSET_TAXONOMY_20260605.*`, `MODEL_FILE_IMPORT_RISK_MATRIX_20260605.*`, `PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.*`, `MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `VISUAL_ASSET_REVIEW_QUEUE_20260605.*`, `MESH_PREFAB_REVIEW_QUEUE_20260605.*` |
| audio/music | `AUDIO_ASSET_TAXONOMY_20260605.*`, `AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.*`, `AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.*`, `AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `AUDIO_PROFILE_ROUTE_MATRIX_20260605.*`, `AUDIO_DIRECT_REF_DETAIL_20260605.*`, `AUDIO_LISTENING_PASS_QUEUE_20260605.*`, `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.*` |
| addressables | `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.*`, `ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`, `ASSET_PLANNING_CONSOLIDATION_3222_20260605.md` |
| proof artifacts | `ASSET_PROOF_ARTIFACT_INDEX_20260605.*`, `GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.*`, contact sheets, waveform sheets, generated source manifests |
| tools/execution | `ASSET_AUTHORING_TOOL_INVENTORY_20260605.*`, `ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`, `ASSET_OWNER_07_TOOL_AND_ROUTE_EXECUTION_PACKET.md` |
| audio owner packets | `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`, `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md` |
| texture/material owner packets | `ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`, `ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md`, `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md` |
| visual/prefab owner packets | `ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md` |
| sky/celestial owner packets | `ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md` |
| addressables owner packets | `ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md` |
| terrain/geology owner packets | `ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md` |
| UI owner packets | `ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md` |
| validator owner packets | `ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md` |
| audio authority owner packets | `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md` |
| ocean/contact owner packets | `ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md` |
| texture streaming owner packets | `ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md` |
| prefab collider/LOD owner packets | `ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md` |
| audio source technical owner packets | `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` |

## Rule

Read the entry file, then only the domain row that matches the assigned work. Do not read old batch logs or lore reports for this asset front unless a specific file is named as evidence.

Final status: `PENDING VERIFICATION`.
