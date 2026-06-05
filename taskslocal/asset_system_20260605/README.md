# Asset System Local Tasks - 2026-06-05

Purpose: continue asset-only recovery work without inventing batch IDs or writing Status/Rationale logs.

Required first read:

- `Docs/AssetAudit/README.md`
- `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.md`
- `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.csv`
- `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md`
- `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv`
- `Docs/AssetAudit/GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.md`
- `Docs/AssetAudit/GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.csv`
- `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.md`
- `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv`
- `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv`
- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv`
- `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv`
- `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.md`
- `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md`
- `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.md`
- `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv`
- `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.md`
- `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.csv`
- `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md`
- `Docs/AssetAudit/AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.md`
- `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.md`
- `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.csv`
- `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.md`
- `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.md`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`
- `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.md`
- `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.csv`
- `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.md`
- `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.csv`
- `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md`
- `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.md`
- `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.csv`
- `Docs/AssetAudit/TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md`
- `Docs/AssetAudit/TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.md`
- `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv`
- `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`
- `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_REVIEW_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`
- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/CleanupPass_MANIFEST_20260605.md`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`
- `Docs/AssetAudit/AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv`
- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.md`
- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.md`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`
- `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.md`
- `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/AssetAudit/AUDIO_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_WORKER_BOARD_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_HYGIENE_SWEEP_20260605.md`
- `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/Reports/AssetSystem_20260605/MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md`
- `Docs/Reports/AssetSystem_20260605/MATERIAL_READBACK_PREFLIGHT_STATIC_BLOCKERS_3215_20260605.md`
- `Docs/Reports/AssetSystem_20260605/UI_SPRITE_ROUTE_STATIC_TABLE_3216_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_GROUP_PLAN_3220_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_ADOPTION_DRAFT_3221_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_PLANNING_CONSOLIDATION_3222_20260605.md`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.md`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv`

Evidence rule: all current asset readiness is `PENDING VERIFICATION` unless the assigned owner produces fresh proof. Static source scans do not prove Unity import, material binding, runtime audio behavior, VRAM safety, or visual quality.

Current start-here navigator:

- `Docs/AssetAudit/README.md` is the first handoff file for future asset agents. It states the current P0 blockers, evidence boundary, hard rejections, process gate, owner map, and Low/Middle/High/Ultra consequences.
- `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md/.csv` maps existing contact sheets, waveform sheets, generated source packs, diagnostic screenshot reviews, and taxonomy artifacts without treating them as acceptance.
- `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.md/.csv` is the compact dispatch board for current P0/P1 asset owners.
- `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.md/.csv` is the compact cross-domain row-risk board for assigning packet owners without reading every source CSV row first.
- `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.md/.csv` maps the current asset-front files so future agents do not bulk-read unrelated reports.
- `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md/.csv` maps static GUID reachability for texture, audio, material, model, prefab, scene, vendor-path, and Addressables owner routing.
- `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md/.csv` condenses the GUID graph to P0/P1 active route owner lanes.
- `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md/.csv` isolates unreferenced cleanup-review rows after active-route work; it is not deletion authorization.
- `Docs/AssetAudit/LARGE_SOURCE_OWNER_REVIEW_20260605.md/.csv` buckets large texture/audio source rows for owner review; it is not deletion, import, or residency proof.
- `Docs/AssetAudit/PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.md/.csv` is the P0 material/texture target table for product-face repair owners.
- `Docs/AssetAudit/PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.md/.csv` is the P0 prefab primitive/LOD/collider target table for product-face replacement owners.
- `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv` is the row-level execution refinement for product-face material/prefab P0 blockers; it is not Unity proof or visual acceptance.
- `Docs/AssetAudit/AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.md/.csv` is the P0 audio routing/import/source remediation target table.
- `Docs/Reports/AssetSystem_20260605/AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv` is the row-level execution refinement for the six audio P0 blockers; it is not runtime mix or listening proof.
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.md/.csv` is the no-mutation Unity readback field manifest for the next clean process gate.
- `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.md/.csv` is the rejected/missing visual proof capture gap table.
- `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md/.csv` is the current mandatory visual-reference folder inventory. Use it before citing any reference image path.
- `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png` is the contact sheet for the current 15-reference set.
- `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md/.csv` maps `VREF-01` through `VREF-15` to water, terrain, sky, flora, UI, cockpit, and h8_1475 proof owner packets.
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md` is the current image-read digest for the mandatory visual references. Use it before water, terrain, sky, flora, UI, cockpit, shoreline, or h8_1475 visual proof work.
- Current digest-linked visual/product-face packets: 01, 02, 04, 05, 06, 07, 09, 11-18, 20-22, 24-27, 34, and 36. Remaining no-digest packets are audio-only or unreferenced-source cleanup review, not current mandatory visual-reference scope.
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md/.csv` is the mandatory-reference critique checklist for future h8_1475 screenshot pass/fail review; it is not visual acceptance.
- `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.md/.csv` is the canonical h8_1475 shotlist aligned to the image-read digest; it is not screenshot proof.
- `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md` is the fixed template for the future proof-packet file `h8_1475_visual_reference_comparison.md`; it is not visual proof.
- `Docs/AssetAudit/H8_1475_PROOF_DEPENDENCY_GRAPH_20260605.md/.csv` orders the future no-mutation h8_1475 proof packet dependencies; it is not execution proof.
- `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.md/.csv` is the compact crosswalk for assigning owners 24-37 from the P0 target/readback/capture tables.
- `Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.md/.csv` maps asset owner IDs 01-37 and marks 29-33 as output-only target-table worker IDs.
- `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md` records current static parse hygiene for 62 curated CSV files and 14646 current rows, including visual-reference critique, current visual-reference path continuity, VREF-to-owner requirement matrix, visual-reference current rejection matrix, visual source promotion execution queue, h8_1475 canonical shotlist, h8_1475 anti-false-proof routing, visual proof capture guardrail validation, audio P0 execution refinement, audio route owner matrix, audio mix-priority decision queue, audio critical cue coverage matrix, Batch31 channel-semantics decision queue, foam-contact source role decision queue, VFX DataVault source-context correction, VFX repair anchor map, Biolum black-box route decision, visual hero source coverage matrix, product-face execution refinement, and clean scoped audio CSVs outside `Docs/AssetAudit`. Batch31 import-intent CSV is sparse sidecar evidence, not part of the zero-empty set.
- `Docs/AssetAudit/AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.md/.csv` maps current music, ambient, player-loop, UI, and VO blockers to owners 08, 10, 19, 23, and 28 before audio route execution.
- `Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.md/.csv` maps warning, player-loop, ambience, music, stinger, UI, and VO mix-priority proof order before runtime mix or listening owners act.
- `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.md/.csv` maps warning, sonar/scanner, tool, UI, player-loop, ambience, music, stinger, and VO source coverage gaps before runtime mix or source-authoring owners act.
- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md/.csv/.json` plus `BATCH31_LOCAL_PBR_IMPORT_INTENT_STATIC_VALIDATION_20260605.md` are blocked static import-intent evidence for local PBR sources; do not import packed masks until the MRAO/ARM route is chosen and proven.
- `Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.md/.csv` separates Batch31 usable albedo/normal source candidates from blocked packed-mask candidates before terrain, shoreline, or photic material owners act.
- `Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.md/.csv` separates rejected `foam.png`, cleanup albedo/normal source candidates, blocked cleanup masks, and out-of-scope visor/detail textures before water/contact owners act.
- `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.md/.csv` maps existing local offline/editor tools so future owners reuse known generators/scanners instead of inventing new ones.
- `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.md/.csv` maps MusicDirector/profile/cue-family blockers before audio owners touch route data.
- `Docs/AssetAudit/AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md/.csv` maps audio source folders to long-bed, low-Q, direct-ref, placeholder, owner, and Addressables risks.
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.md/.csv` maps source loudness probes for short/critical rows and explicit deferred long-bed rows; no runtime mix/listening proof exists.
- `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md/.csv` maps texture/material families to route moments, blockers, proof artifacts, import rows, and rejection rules.
- `Docs/AssetAudit/TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md/.csv` maps texture source folders to generated/source-only, active-route, proxy, visible-user, streaming-mip, and owner risks.
- `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.md/.csv` maps exact hash, same-basename, and family-name duplicate risks; no deletion authorization.
- `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.md/.csv` maps serialized material shader/texture/proxy/Crest route tokens before Unity material readback.
- `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.md/.csv` maps row-level prefab token risks before Unity prefab readback.
- `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.md/.csv` maps first-party model source/meta import risks before Unity model importer readback.
- `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md/.csv` maps prefab folders to LOD token, primitive mesh ref, MeshCollider, proxy/placeholder, and owner risks.
- `taskslocal/asset_system_20260605/ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md` is the exact packet for `Player.prefab` direct `AudioClip` refs.
- `taskslocal/asset_system_20260605/ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md` is the exact packet for texture/material blocker rows and safe import/material execution.
- `taskslocal/asset_system_20260605/ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md` is the exact packet for MusicDirector mixer/profile/cue routing blockers.
- `taskslocal/asset_system_20260605/ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md` is the exact packet for rejected active-route water foam/contact source replacement.
- `taskslocal/asset_system_20260605/ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md` is the exact packet for active-world `WorldProceduralProxy` flora/coral/kelp material replacement.
- `taskslocal/asset_system_20260605/ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md` is the exact packet for visible product-face primitive prefab replacement.
- `taskslocal/asset_system_20260605/ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md` is the exact packet for sky/Aegir/cloud/moon source-slot proof and future replacement routing.
- `taskslocal/asset_system_20260605/ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md` is the exact packet for Addressables group/key/lifecycle execution blockers.
- `taskslocal/asset_system_20260605/ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md` is the exact packet for terrain/geology PBR authoring and import proof routing.
- `taskslocal/asset_system_20260605/ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md` is the exact packet for UI oxygen sprite/atlas route cleanup.
- `taskslocal/asset_system_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md` is the exact packet for product-face validator execution and evidence interpretation.
- `taskslocal/asset_system_20260605/ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md` is the exact packet for audio import authority adoption prerequisites.
- `taskslocal/asset_system_20260605/ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md` is the exact packet for ocean/Crest contact proof routing; it now explicitly rejects `h8_1914_surface_water_recovery_probe.png` as diagnostic-only proof.
- `taskslocal/asset_system_20260605/ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md` is the exact packet for texture streaming mip, hero-scale source, large-source, and sRGB/name-risk remediation.
- `taskslocal/asset_system_20260605/ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md` is the exact packet for prefab collider, LOD, primitive mesh, proxy/placeholder, and no-renderer row risk remediation.
- `taskslocal/asset_system_20260605/ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` is the exact packet for long-bed, multichannel, source-rate, import, listening, lifecycle, and DSP route remediation.
- `taskslocal/asset_system_20260605/ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md` is the exact packet for converting active GUID triage rows into owner execution without static-to-runtime promotion.
- `taskslocal/asset_system_20260605/ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md` is the exact packet for unreferenced source cleanup review; it is not deletion authorization.
- `taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` is the exact packet for canonical h8_1475 no-mutation Unity proof execution after a clean process gate; it now rejects `H8VisualProofCapture1912` diagnostic/editor-mutating probe methods as acceptance proof.
- `taskslocal/asset_system_20260605/ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md` is the exact packet for blocking false h8_1475 acceptance from shell-player, overlay-HUD, blockout-tool, landscape-only, or stale MCP screenshot evidence.

Do not:

- raw YAML patch `.mat`, `.prefab`, `.unity`, or `.asset`;
- promote `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` into visible route content;
- clone or wrap Crest materials;
- launch Unity/build/import while CPU is above gate or `dotnet`/`csc` is active;
- call any candidate final without screenshots, readback, Console, and stats/proof.

Task files:

- `ASSET_OWNER_01_UNITY_MATERIAL_READBACK.md`
- `ASSET_OWNER_02_TEXTURE_AUTHORING.md`
- `ASSET_OWNER_03_AUDIO_LEDGER_LISTENING.md`
- `ASSET_OWNER_04_MESH_PREFAB_PROMOTION.md`
- `ASSET_OWNER_05_UI_SPRITE_ROUTE.md`
- `ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md`
- `ASSET_OWNER_07_TOOL_AND_ROUTE_EXECUTION_PACKET.md`
- `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`
- `ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`
- `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`
- `ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md`
- `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`
- `ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md`
- `ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md`
- `ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md`
- `ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md`
- `ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md`
- `ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md`
- `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`
- `ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md`
- `ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md`
- `ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md`
- `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md`
- `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md`
- `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`
- `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`
- `ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md`
- `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md`
- `ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md`
- `ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md`
- `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`
- `ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md`

Active follow-up workers:

- Texture/material usage map output exists: `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv` and `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`.
- Audio/profile usage map output exists: `Docs/Audio/audio_profile_usage_20260605.csv` and `Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md`.

New hard blockers from usage maps:

- `foam.png` is visually rejected but serialized-reachable through active world/ocean users.
- Four `WorldProceduralProxy` flora/coral/kelp materials are serialized in `02_HECTON_WORLD.unity`.
- `MusicDirectorConfig_Global.asset` has null music and stinger mixer group refs.
- `Player.prefab` has direct AudioClip refs; Addressables owner/release route remains unproven.

Dispatch order:

1. P0 water foam active-route replacement/readback.
2. P0 proxy flora/coral/kelp active-world replacement/readback.
3. P0 MusicDirector mixer refs and Player prefab direct audio refs.
4. P1 Aegir/cloud and terrain PBR authoring.
5. P1/P2 import-role and UI sprite cleanup.

Latest owner packets:

- For step 3 direct `Player.prefab` audio refs, use `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`.
- For step 3 MusicDirector mixer/profile/cue blockers, use `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`.
- For steps 1, 2, and 4 texture/material blockers, use `ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`.
- For step 1 rejected active-route foam/contact art, use `ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md`.
- For step 2 active-world flora/coral/kelp proxy material contamination, use `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- For product-face primitive mesh replacement in visible tools/pickups/construction/transport/building/support prefabs, use `ASSET_OWNER_13_PRODUCT_FACE_PREFAB_PRIMITIVE_REPLACEMENT_PACKET.md`.
- For step 4 sky/Aegir/cloud/moon proof or replacement routing, use `ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md`.
- For Addressables settings/groups/keys/labels/catalogs/load-release execution, use `ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md`.
- For first-exit/photic/medium-depth terrain/geology PBR authoring or source promotion, use `ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md`.
- For HUD oxygen sprite/atlas/import/binding work, use `ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md`.
- For product-face validator execution or interpreting validator logs, use `ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md`.
- For audio stable authority/import-policy exception adoption, use `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`.
- For ocean surface, Crest contact, waterline, foam/contact proof work, use `ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md`.
- For texture streaming mips, hero-scale source rows, large source rows, or sRGB/name-risk rows, use `ASSET_OWNER_21_TEXTURE_STREAMING_MIP_STATIC_RISK_PACKET.md`.
- For prefab collider, LOD, primitive mesh, proxy/placeholder, or no-renderer row risks, use `ASSET_OWNER_22_PREFAB_COLLIDER_LOD_ROW_RISK_PACKET.md`.
- For audio long-bed, multichannel, high-rate, import, listening, lifecycle, or DSP source remediation, use `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md`.
- For product-face material/texture repair after validator failure and visual-reference rejection, use `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md`.
- For visible built-in primitive mesh replacement, LOD chains, collider proxies, and product-face prefab proof, use `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`.
- For no-mutation Unity readback of active product-face blockers and h8_1475 readiness, use `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`.
- For missing underwater fish/marine-snow/foam/caustic source generation and QA specs, use `ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md`.
- For P0 audio routing/import/source remediation execution planning, use `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md`.
- For active GUID route triage execution, use `ASSET_OWNER_34_ACTIVE_ROUTE_TRIAGE_PACKET.md`.
- For unreferenced source cleanup review, use `ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET.md`; never delete from the triage table alone.
- For canonical h8_1475 proof execution, use `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`.
- For h8_1475 anti-false-proof gating, use `ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md` before accepting any screenshot packet that could bypass active production player, HUD, input, foreground tool, or VREF comparison proof.

Recipe/spec handoff:

- Texture authoring owner must follow `TEXTURE_AUTHORING_RECIPES_20260605.md`.
- Texture authoring owner must review source-only prototype folders under `Docs/GeneratedAssets/AssetSystem_20260605/` before creating new variants.
- Current cleanup pass exists under `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/`; it is source-only and not import-ready.
- Texture import/meta owner must use `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv` before changing import settings.
- Addressables owner must use `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` before creating groups, labels, keys, or exceptions. The plan is static only and not readiness proof.
- Audio owner must follow `AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`.
- Audio owner must use `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv` as a proposed exception table only. It is not stable authority and not import proof.
- Audio listening owner must use `AUDIO_LISTENING_PASS_QUEUE_20260605.csv` for pass order.
- Visual review owner must use `VISUAL_ASSET_REVIEW_QUEUE_20260605.csv` for target order.
- Mesh/prefab owner must use `PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv` plus `MESH_PREFAB_REVIEW_QUEUE_20260605.csv` for row-level risk and promotion/rejection order.
- Audio owner must use `audio_remediation_matrix_20260605.csv` for row-level ordering; it currently has `58` rows and `6` P0 rows.
- Audio policy inputs are draft-only: `AUDIO_POLICY_ADOPTION_DRAFT_3221_20260605.md` and `AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv` do not change stable authority or import settings.
- Addressables planning inputs are draft-only: `ADDRESSABLES_GROUP_PLAN_3220_20260605.md` and `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` do not create settings, groups, labels, keys, or catalogs.
- Planning precedence is consolidated in `ASSET_PLANNING_CONSOLIDATION_3222_20260605.md`; use it before choosing between duplicated Addressables/audio/texture planning artifacts.
