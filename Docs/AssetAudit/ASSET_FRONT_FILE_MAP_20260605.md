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
| `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md/.csv` | Static GUID reachability graph for texture, audio, material, model, prefab, scene, and vendor-path routing. |
| `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md/.csv` | Compact P0/P1 owner triage derived from the GUID reachability graph. |
| `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md/.csv` | Unreferenced GUID cleanup-review triage. No deletion authority. |
| `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md/.csv` | Compact P0 target/readback/capture routing crosswalk for owners 24-37. |
| `Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.md/.csv` | Owner packet index for asset owner IDs 01-37 and output-only IDs 29-33. |
| `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md` | Long-form asset system index and dispositions. |
| `Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md` | Controller-level synthesis and parsed static row counts. |
| `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md/.csv` | Mandatory-reference critique gates for future h8_1475 screenshots. |
| `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.md/.csv` | Canonical h8_1475 screenshot shotlist and rejection rubric. |
| `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md` | Fixed required comparison shape for future `h8_1475_visual_reference_comparison.md`. |
| `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md/.csv` | Dependency order for future no-mutation h8_1475 proof execution and triage. |
| `Docs/AssetAudit/LARGE_SOURCE_OWNER_REVIEW_20260605.md/.csv` | Large texture/audio source owner-review buckets before cleanup, import, or retention decisions. |
| `Docs/AssetAudit/AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.md/.csv` | Audio route owner requirement matrix for MusicDirector, player loops, ambience, UI, and VO blockers. |
| `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.md/.csv` | Audio mix-priority decision queue for warning, player-loop, ambience, music, stinger, UI, and VO proof order. |
| `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.md/.csv` | Audio critical cue family coverage matrix for missing warning, sonar/scanner, tool, UI, player-loop, ambience, music, stinger, and VO source proof. |
| `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md/.csv/.json` | Sparse static import intent for Batch31 local PBR sources; packed masks remain blocked by channel semantics. |
| `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_STATIC_VALIDATION_20260605.md` | Static validation and unit-test result for Batch31 import intent. |
| `Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.md/.csv` | Owner decision queue separating Batch31 usable albedo/normal candidates from blocked packed masks. |
| `Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.md/.csv` | Owner decision queue for rejected foam source, cleanup maps, and water-contact source roles. |
| `Docs/AssetAudit/SURFACE_HORIZON_HAZE_1428_STATIC_REVIEW_20260605.md` | Static rejection review for the untracked horizon haze proof attempt. |
| `Docs/AssetAudit/SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md` | Static rejection review for the diagnostic surface water recovery probe. |
| `Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md` | Static source-risk review for `H8VisualProofCapture1912` diagnostic/editor-mutating probe paths. |
| `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv` | Product-face material/prefab P0 execution refinement before repair owners mutate anything. |
| `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md/.csv` | Current mandatory visual reference path inventory for h8_1475 critique. |
| `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md/.csv` | VREF-to-owner visual requirement matrix for water, terrain, sky, flora, UI, cockpit, and h8_1475 proof owners. |
| `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.md/.csv` | Static current-diagnostic rejection matrix against the mandatory references. |
| `Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.md/.csv` | Execution queue from mandatory visual references and source coverage into exact owner actions, proof gates, and rejection rules. |
| `taskslocal/asset_system_20260605/README.md` | Local task packet index and owner dispatch order. |

## Detail Files By Domain

| Domain | Primary Files |
|---|---|
| cross-asset GUID references | `ASSET_GUID_REFERENCE_MATRIX_20260605.*`, `ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.*`, `ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.*` |
| texture/material | `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.*`, `TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.*`, `TEXTURE_DUPLICATE_HASH_MATRIX_20260605.*`, `TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.*`, `TEXTURE_IMPORT_ROLE_MATRIX_20260605.*`, `MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.*`, `TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`, `TEXTURE_CANDIDATE_DISPOSITION_20260605.csv` |
| visual/mesh/prefab | `VISUAL_MESH_ASSET_TAXONOMY_20260605.*`, `MODEL_FILE_IMPORT_RISK_MATRIX_20260605.*`, `PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.*`, `MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `VISUAL_ASSET_REVIEW_QUEUE_20260605.*`, `MESH_PREFAB_REVIEW_QUEUE_20260605.*` |
| audio/music | `AUDIO_ASSET_TAXONOMY_20260605.*`, `AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.*`, `AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.*`, `AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.*`, `AUDIO_PROFILE_ROUTE_MATRIX_20260605.*`, `AUDIO_DIRECT_REF_DETAIL_20260605.*`, `AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.*`, `AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.*`, `AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.*`, `AUDIO_LISTENING_PASS_QUEUE_20260605.*`, `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.*`, `AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.*`, `audio_asset_ledger.csv`, `audio_remediation_matrix_20260605.csv`, `audio_preview_waveform_stats_20260605.csv` |
| addressables | `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.*`, `ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`, `ASSET_PLANNING_CONSOLIDATION_3222_20260605.md` |
| proof artifacts | `ASSET_PROOF_ARTIFACT_INDEX_20260605.*`, `GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.*`, contact sheets, waveform sheets, generated source manifests |
| large source review | `LARGE_SOURCE_OWNER_REVIEW_20260605.*` |
| Batch31 local PBR import intent | `BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md`, `.csv`, `.json`, `BATCH31_LOCAL_PBR_IMPORT_INTENT_STATIC_VALIDATION_20260605.md`, `BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.*` |
| foam/contact source role queue | `FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.*` |
| surface horizon haze static rejection | `SURFACE_HORIZON_HAZE_1428_STATIC_REVIEW_20260605.md` |
| surface water recovery probe static rejection | `SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md` |
| h8 visual proof capture risk review | `H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md` |
| VFX DataVault/source context | `VFX_DATAVAULT_SOURCE_CONTEXT_REVIEW_20260605.*`, `VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.*`, `DATAVAULT_AUDIT_EXECUTION_SURFACE_RECHECK_20260605.md`, `BIOLUM_BLACKBOX_ROUTE_DECISION_20260605.*`, `VFX_DATAVAULT_SOVEREIGNTY_STATIC_REVIEW_20260605.md`, `VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json` |
| visual hero source coverage | `VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.*`, `VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.*`, `VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.*` |
| visual source promotion queue | `VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.*`, `VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.*`, `BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.*`, `FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.*` |
| audio mix-priority decision queue | `AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.*` |
| audio critical cue coverage matrix | `AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.*` |
| visual reference critique | `VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.*` |
| h8_1475 canonical shotlist | `H8_1475_CANONICAL_SHOTLIST_20260605.*` |
| h8_1475 visual comparison template | `H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md` |
| visual reference path continuity | `VISUAL_REFERENCE_PATH_CONTINUITY_20260605.*` |
| visual reference owner matrix | `VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.*` |
| visual reference current rejection matrix | `VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.*` |
| h8_1475 proof dependency graph | `H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.*` |
| product-face execution refinement | `PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.*` |
| target tables | `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.*`, `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.*`, `AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.*`, `H8_1475_READBACK_FIELD_MANIFEST_20260605.*`, `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.*`, `ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.*` |
| owner packet index | `ASSET_OWNER_PACKET_INDEX_20260605.*` |
| early owner packets | `ASSET_OWNER_01_UNITY_MATERIAL_READBACK.md`, `ASSET_OWNER_02_TEXTURE_AUTHORING.md`, `ASSET_OWNER_03_AUDIO_LEDGER_LISTENING.md`, `ASSET_OWNER_04_MESH_PREFAB_PROMOTION.md`, `ASSET_OWNER_05_UI_SPRITE_ROUTE.md` |
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
| product-face material repair packets | `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md` |
| product-face primitive replacement packets | `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md` |
| Unity no-mutation readback packets | `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md` |
| underwater VFX source packets | `ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md` |
| audio remediation execution packets | `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md` |
| active-route execution packets | `ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md` |
| cleanup-review execution packets | `ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md` |
| h8_1475 proof execution packets | `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` |
| h8_1475 anti-false-proof packets | `ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md` |
| visual proof capture guardrail validator | `Tools/ValidateVisualProofCaptureGuardrails.py`, `Tools/test_validate_visual_proof_capture_guardrails.py` |
| asset static summary validator | `Tools/ValidateAssetStaticSummary.py`, `Tools/test_validate_asset_static_summary.py` |
| foam contact decision queue validator | `Tools/ValidateFoamContactDecisionQueue.py`, `Tools/test_validate_foam_contact_decision_queue.py` |

## Rule

Read the entry file, then only the domain row that matches the assigned work. Do not read old batch logs or lore reports for this asset front unless a specific file is named as evidence.

Final status: `PENDING VERIFICATION`.
