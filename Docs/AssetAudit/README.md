# Asset Audit Start Here

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_IMAGE_QA`, `AUDIO_WAVEFORM_QA`.
Scope: textures, music/audio, meshes, prefabs, materials, UI sprites, generated source packs, and Addressables planning.
First-20 route moment: bright surface exit, sky/Aegir/moons, ocean contact, photic shallows, player breath/audio continuity, HUD oxygen readability, and medium-depth route dressing.

This directory is the current asset-front control surface. It is not Unity acceptance. No current file here proves import quality, material binding, Crest state, Addressables residency, VRAM safety, mix behavior, GC, frame time, or in-game visual quality.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`

## Read Order

1. `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
2. `Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md`
3. `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md`
4. `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
5. `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`
6. `taskslocal/asset_system_20260605/README.md`
7. `taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`
8. `taskslocal/asset_system_20260605/ASSET_OWNER_07_TOOL_AND_ROUTE_EXECUTION_PACKET.md`
9. `taskslocal/asset_system_20260605/ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`
10. `taskslocal/asset_system_20260605/ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`
11. `taskslocal/asset_system_20260605/ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`
12. `taskslocal/asset_system_20260605/ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md`
13. `taskslocal/asset_system_20260605/ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`
14. `taskslocal/asset_system_20260605/ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md`
15. `taskslocal/asset_system_20260605/ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md`
16. `taskslocal/asset_system_20260605/ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md`
17. `taskslocal/asset_system_20260605/ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md`
18. `taskslocal/asset_system_20260605/ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md`
19. `taskslocal/asset_system_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md`
20. `taskslocal/asset_system_20260605/ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`
21. `taskslocal/asset_system_20260605/ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md`
22. `taskslocal/asset_system_20260605/ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md`
23. `taskslocal/asset_system_20260605/ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md`
24. `taskslocal/asset_system_20260605/ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md`
25. `taskslocal/asset_system_20260605/ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md`
26. `taskslocal/asset_system_20260605/ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`
27. `taskslocal/asset_system_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`
28. `taskslocal/asset_system_20260605/ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md`
29. `taskslocal/asset_system_20260605/ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md`
30. `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md`
31. `Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.md`
32. `taskslocal/asset_system_20260605/ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md`
33. `taskslocal/asset_system_20260605/ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md`
34. `taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`
35. `taskslocal/asset_system_20260605/ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md`
36. `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md`
37. `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md`
38. `Docs/Reports/AssetSystem_20260605/AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.md`
39. `Docs/AssetAudit/LARGE_SOURCE_OWNER_REVIEW_20260605.md`
40. `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md`
41. `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md`

## Current P0 Blockers

| Domain | Static Fact | Required Next Owner |
|---|---|---|
| water_visual | `foam.png` is visually rejected but serialized-reachable through active world/ocean users. | Unity material readback plus texture authoring |
| flora_materials | Four `WorldProceduralProxy` flora/coral/kelp materials are serialized in `02_HECTON_WORLD.unity`. | Unity material readback plus mesh/prefab owner |
| audio_routing | `MusicDirectorConfig_Global.asset` has null music and stinger mixer group refs in static evidence. | Audio/MusicDirector owner |
| audio_lifecycle | `Player.prefab` has direct AudioClip refs without owner/release/Addressables proof. | Audio lifecycle owner |

## Route Queues

Use these files before assigning or doing asset work:

- Existing proof-adjacent artifact map: `ASSET_PROOF_ARTIFACT_INDEX_20260605.md`.
- Existing proof-adjacent artifact CSV: `ASSET_PROOF_ARTIFACT_INDEX_20260605.csv`.
- Generated source pack inventory: `GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.md` and `.csv`.
- Current static validation summary: `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md`.
- Cross-domain static row blocker summary: `ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.md` and `.csv`.
- Asset-front file map: `ASSET_FRONT_FILE_MAP_20260605.md` and `.csv`.
- Asset GUID reference matrix: `ASSET_GUID_REFERENCE_MATRIX_20260605.md` and `.csv`.
- Asset GUID active-route triage: `ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md` and `.csv`.
- Asset GUID unreferenced source triage: `ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md` and `.csv`.
- Large source owner review: `LARGE_SOURCE_OWNER_REVIEW_20260605.md` and `.csv`.
- Product-face material P0 table: `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.md` and `.csv`.
- Product-face prefab P0 table: `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.md` and `.csv`.
- Product-face static execution refinement: `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md` and `.csv`.
- Audio P0 remediation table: `AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.md` and `.csv`.
- Audio P0 static execution refinement: `Docs/Reports/AssetSystem_20260605/AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.md` and `.csv`.
- No-mutation Unity readback field manifest: `H8_1475_READBACK_FIELD_MANIFEST_20260605.md` and `.csv`.
- Visual reference capture gap table: `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.md` and `.csv`.
- Visual reference critique checklist: `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md` and `.csv`.
- Visual reference path continuity: `VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md` and `.csv`.
- Current mandatory visual-reference contact sheet: `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png`.
- VREF-to-owner visual requirement matrix: `VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md` and `.csv`.
- Visual reference current rejection matrix: `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.md` and `.csv`.
- H8 proof dependency graph: `H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md` and `.csv`.
- P0 target-table routing synthesis: `ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md` and `.csv`.
- Asset owner packet index: `ASSET_OWNER_PACKET_INDEX_20260605.md` and `.csv`.
- Consolidated next-action board: `ASSET_NEXT_ACTION_BOARD_20260605.md` and `.csv`.
- Local authoring/tool inventory: `ASSET_AUTHORING_TOOL_INVENTORY_20260605.md` and `.csv`.
- Audio taxonomy: `AUDIO_ASSET_TAXONOMY_20260605.md` and `.csv`.
- Audio source folder matrix: `AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md` and `.csv`.
- Audio route owner requirement matrix: `AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.md` and `.csv`.
- Audio mix-priority decision queue: `AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.md` and `.csv`.
- Audio critical cue coverage matrix: `AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.md` and `.csv`.
- Audio source technical probe matrix: `AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
- Audio loudness/source dynamics matrix: `AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
- Audio profile/cue route matrix: `AUDIO_PROFILE_ROUTE_MATRIX_20260605.md` and `.csv`.
- Visual/mesh taxonomy: `VISUAL_MESH_ASSET_TAXONOMY_20260605.md` and `.csv`.
- Model/source import risk matrix: `MODEL_FILE_IMPORT_RISK_MATRIX_20260605.md` and `.csv`.
- Prefab source technical token matrix: `PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
- Texture/material family route matrix: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md` and `.csv`.
- Texture source folder matrix: `TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md` and `.csv`.
- Texture source technical probe matrix: `TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
- Texture duplicate/hash matrix: `TEXTURE_DUPLICATE_HASH_MATRIX_20260605.md` and `.csv`.
- Texture active-route blocker detail: `TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.md` and `.csv`.
- Batch31 local PBR import intent: `BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md`, `.csv`, and `.json` (`STATIC_SOURCE`; packed-mask rows are `BLOCKED_CHANNEL_SEMANTICS`).
- Batch31 static validation: `BATCH31_LOCAL_PBR_IMPORT_INTENT_STATIC_VALIDATION_20260605.md` (`STATIC_VALIDATION_ONLY`; unit-tested import-intent generation, no Unity promotion).
- Batch31 channel-semantics decision queue: `BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.md` and `.csv` (`STATIC_IMAGE_QA`; usable albedo/normal candidates vs blocked packed-mask rows).
- Foam/contact source role decision queue: `FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.md` and `.csv` (`STATIC_IMAGE_QA`; rejected foam, cleanup maps, and out-of-scope water/detail sources).
- VFX DataVault source-context correction: `VFX_DATAVAULT_SOURCE_CONTEXT_REVIEW_20260605.md` and `.csv` (`STATIC_SOURCE_READBACK`; MarineSnow current disk source uses a DataVault rewrite, old 1347/2005 audit anchors are historical, and current editor/offline wake-profile scratch is 1948).
- VFX DataVault repair anchor map: `VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.md` and `.csv` (`STATIC_SOURCE_READBACK`; Biolum, MarineSnow, and PlasmaBeam repair anchors only).
- DataVault audit execution-surface recheck: `DATAVAULT_AUDIT_EXECUTION_SURFACE_RECHECK_20260605.md` (`STATIC_SOURCE_TOOL_OUTPUT`; original audit JSON line-surface split was valid for the historical source snapshot, while current disk readback shows the MarineSnow runtime path rewritten through DataVault).
- Biolum black-box route decision: `BIOLUM_BLACKBOX_ROUTE_DECISION_20260605.md` and `.csv` (`STATIC_TOOL_OUTPUT`; source decision fields are present; compile, Unity, GC/profiler, and dump proof remain absent).
- Visual hero source coverage matrix: `VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.md` and `.csv` (`STATIC_IMAGE_QA`; mandatory-reference source fit and blocker routing only).
- Visual source promotion execution queue: `VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.md` and `.csv` (`STATIC_SOURCE_QUEUE_ONLY`; exact owner actions and rejection gates only).
- Surface horizon haze rejection review: `SURFACE_HORIZON_HAZE_1428_STATIC_REVIEW_20260605.md` (`STATIC_REVIEW_ONLY`; rejects untracked `ZTest Always` haze and raw no-clip screenshot as proof).
- Surface water recovery probe rejection review: `SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md` (`STATIC_SCREENSHOT_REVIEW`; rejects editor-only unsaved flat-water diagnostic screenshot as proof).
- h8_1475 proof-tool risk review: `H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md` (`STATIC_SOURCE_REVIEW`; rejects editor-mutated diagnostic capture paths as canonical proof).
- Visual proof capture guardrail validator: `Tools/ValidateVisualProofCaptureGuardrails.py` and `Tools/test_validate_visual_proof_capture_guardrails.py` (`STATIC_SOURCE_TOOL`; validates risk-routing and capture-tool asset-path existence only, not no-mutation Unity proof).
- Asset static summary validator: `Tools/ValidateAssetStaticSummary.py` and `Tools/test_validate_asset_static_summary.py` (`STATIC_SOURCE_TOOL`; validates curated CSV row/count hygiene only, not whole-folder or Unity proof).
- Batch31 local PBR promotion prep: `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_INDEX.md` (`STATIC_IMAGE_PREP_ONLY`; preview/source artifacts only).
- Material serialized risk matrix: `MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
- Audio direct-ref detail: `AUDIO_DIRECT_REF_DETAIL_20260605.md` and `.csv`.
- Texture import/meta planning: `TEXTURE_IMPORT_ROLE_MATRIX_20260605.md` and `.csv`.
- Addressables planning: `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md` and `.csv`.
- Audio import-policy exception planning: `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md` and `.csv`.
- Audio listening/remediation order: `AUDIO_LISTENING_PASS_QUEUE_20260605.md` and `.csv`.
- Visual inspection order: `VISUAL_ASSET_REVIEW_QUEUE_20260605.md` and `.csv`.
- Mesh/prefab inspection order: `MESH_PREFAB_REVIEW_QUEUE_20260605.md` and `.csv`.
- Mesh/prefab source folder matrix: `MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md` and `.csv`.
- Audio row-level remediation: `Docs/Audio/audio_remediation_matrix_20260605.csv`.

## Source-Only Packs

Generated and cleaned source packs live under `Docs/GeneratedAssets/AssetSystem_20260605/`. They are reference/source material only. Do not import them as product art without a named authoring owner, cleaned PBR/channel role, import settings, material route, Unity readback, screenshot, stats, and memory proof.

Current source-only reviews:

- `GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.md` and `.csv`
- `SOURCE_PROTOTYPE_REVIEW_20260605.md`
- `SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`
- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/CleanupPass_MANIFEST_20260605.md`

## Hard Rejections

- Do not mutate `Assets` during static audit work.
- Do not raw YAML patch `.mat`, `.prefab`, `.unity`, or `.asset` files.
- Do not clone, wrap, or runtime-instantiate Crest materials.
- Do not promote `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` into visible route content.
- Do not call generated source packs final art.
- Do not create Addressables groups, labels, keys, catalogs, or settings from planning docs alone.
- Do not claim audio runtime behavior from waveform/contact-sheet review.
- Do not claim visual acceptance from static source inspection.

## Process Gate

Unity readback, import, build, Play Mode, prefab edit, scene save, Addressables build, or project-setting work is blocked unless a fresh gate has:

- CPU samples below 50 percent.
- No active `dotnet`, `csc`, `MSBuild`, `Unity.ILPP.Runner`, `UnityShaderCompiler`, or `UnityPackageManager`.
- Unity idle and safe for read-only inspection, or Unity closed.

If the gate is red, continue static/source documentation only.

## Asset Owner Map

- Unity readback owner: `ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`.
- Texture authoring owner: `TEXTURE_AUTHORING_RECIPES_20260605.md`, cleanup reviews, and `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`.
- Texture/material route owner: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv` before any family promotion or material-route edit.
- Cross-asset GUID/reference owner: `ASSET_GUID_REFERENCE_MATRIX_20260605.csv` before assigning texture, audio, material, model, prefab, scene, vendor-path, or Addressables reachability work.
- Active GUID triage owner: `ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv` before assigning P0/P1 active-world, direct-audio, scene-reachable, or vendor-path GUID rows.
- Unreferenced GUID cleanup-review owner: `ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv` after active-route triage only; this file is not deletion authorization.
- Large source owner review: `LARGE_SOURCE_OWNER_REVIEW_20260605.csv` before cleanup, import, retention, or residency decisions for large texture/audio rows.
- Product-face material P0 target owner: `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv` before `ASSET_OWNER_24` repair execution.
- Product-face prefab P0 target owner: `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` before `ASSET_OWNER_25` primitive replacement execution.
- Product-face execution refinement owner: `PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.csv` before any material/prefab repair owner mutates product-face routes.
- Audio P0 remediation target owner: `AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.csv` before `ASSET_OWNER_28` audio remediation execution.
- Audio P0 execution refinement owner: `AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.csv` before row-level MusicDirector or Player direct-ref audio P0 execution.
- Unity readback field owner: `H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` before `ASSET_OWNER_26` no-mutation readback execution.
- Visual capture gap owner: `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` before any product-face screenshot acceptance attempt.
- Visual critique owner: `VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.csv` before any h8_1475 screenshot pass/fail category review.
- Visual reference path owner: `VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv` before future reviewers cite mandatory reference images.
- H8 proof dependency owner: `H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.csv` before executing or triaging the canonical h8_1475 packet.
- P0 target routing synthesis owner: `ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.csv` before choosing between material, prefab, audio, h8_1475, or visual-gap owner routes.
- Owner packet index: `ASSET_OWNER_PACKET_INDEX_20260605.csv` before recreating, redistributing, or assuming missing asset owner packet IDs.
- Audio owner: `AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`, `AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`, `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv`, `AUDIO_LISTENING_PASS_QUEUE_20260605.csv`, `AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.csv`, `AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv`, and `audio_remediation_matrix_20260605.csv`.
- Audio profile owner: `AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv` before any MusicDirector/profile/cue route edit.
- Audio direct-ref owner: `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md` and `AUDIO_DIRECT_REF_DETAIL_20260605.csv` before touching `Player.prefab` audio refs.
- MusicDirector owner: `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md` plus `AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv` before profile/mixer/cue route edits.
- Texture/material blocker owner: `TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`, `ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`, and `TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv` before import/material route edits.
- Water foam/contact owner: `ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md` before any foam/contact source authoring, import, Crest/ocean material binding, or screenshot proof pass.
- Flora/coral proxy material owner: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md` before any active-world `WorldProceduralProxy` flora/coral/kelp material replacement.
- Product-face prefab owner: `ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md` before any visible tool, pickup, construction, transport, building, or support prefab primitive-mesh replacement.
- Sky/Aegir/cloud owner: `ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md` before any skybox, Aegir, cloud, moon, or bright-surface hero slot acceptance or replacement pass.
- Addressables owner: `ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md`, `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`, and `ASSET_PLANNING_CONSOLIDATION_3222_20260605.md`.
- Terrain/geology PBR owner: `ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md` before any first-exit/photic/medium-depth terrain or geology PBR source promotion.
- UI oxygen sprite/atlas owner: `ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md` before any oxygen sprite, atlas, HUD binding, or import-route change.
- Product-face validator owner: `ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md` before running or interpreting the product-face material, prefab, or sky-ocean source validators.
- Audio authority adoption owner: `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md` before any stable authority patch or import-policy exception promotion.
- Ocean/Crest contact owner: `ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md` before any ocean surface, Crest contact, foam/contact source, or waterline proof pass.
- Texture streaming/mip owner: `ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md` before any streaming mip, sRGB/name-risk, large-source, or hero-scale texture import work.
- Prefab collider/LOD owner: `ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md` before any prefab collider, LOD, built-in primitive mesh, no-renderer, or proxy/placeholder row remediation.
- Audio source technical owner: `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` before any source-rate, channel, long-bed, import, lifecycle, Addressables, listening, or DSP route remediation.
- Product-face material repair owner: `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md` before repairing material routes after product-face validator failure and visual-reference rejection.
- Product-face primitive replacement owner: `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md` before replacing visible built-in primitive meshes, adding LOD chains, collider proxies, or product-face prefab proof.
- Unity no-mutation readback owner: `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md` when the Unity process gate is clean and current product-face blockers need readback without save/mutation.
- Underwater VFX source owner: `ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md` before generating/prepping fish silhouette, marine snow, foam/contact, or shallow caustic source packs.
- Audio remediation execution owner: `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md` before P0 MusicDirector/direct-ref/import/source remediation execution.
- Active-route execution owner: `ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md` before converting active GUID triage rows into owner execution.
- Unreferenced cleanup-review owner: `ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md` before any quarantine or deletion-review planning; it is not deletion authorization.
- h8_1475 proof execution owner: `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` when the Unity process gate is clean and canonical no-mutation visual/readback proof is needed.
- Mesh/prefab owner: `PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv`, `MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`, `MESH_PREFAB_REVIEW_QUEUE_20260605.csv`, and `MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md`.
- UI sprite owner: `UI_SPRITE_ROUTE_STATIC_TABLE_3216_20260605.md`.

## Scalability Consequences

- Low: preserve premium silhouette, material identity, water/sky readability, baked AO, and route legibility; reduce cadence/residency only through owner-approved continuous `GlobalQualityWeight` paths.
- Middle: use stable material stacks, controlled texture residency, and conservative LOD proof; no proxy pools or flat water/sky substitutes.
- High: spend saved frame time on richer material detail, stronger contact art, denser near-field dressing, and better audio routing, not idle budget.
- Ultra: extend LOD residency, shader detail, reflection/lighting quality, and ambience layering after measured proof; do not change gameplay truth or asset ownership route.

## Regression Model

- CPU: static docs only; no runtime CPU improvement or regression claimed.
- GC: no runtime code touched; no `0 B/frame` claim.
- Memory/VRAM: source size and planning risk only; no residency proof.
- Cadence: no runtime cadence changed.
- Correctness: blocker ownership is clearer; acceptance remains blocked by Unity/runtime proof.

Final status: `PENDING VERIFICATION`.
