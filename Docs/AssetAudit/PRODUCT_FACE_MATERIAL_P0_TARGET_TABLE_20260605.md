# Product-Face Material P0 Target Table - 2026-06-05

Status: PENDING UNITY READBACK.
Evidence class: STATIC_DOC + STATIC_SOURCE + STATIC_YAML_SCAN + STATIC_IMAGE_QA + UNITY_BATCHMODE_LOG.
Runtime proof: absent.
Unity readback: absent.
Write scope: this markdown plus PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv only.

First-20 route moment: bright first exit, ocean skin, shoreline/waterline, photic shallows, held tools, resource pickups, first transport/tool view, Aegir/sky context, flora/coral route material trust.

Mandates followed: QA_Evidence_Text_Filter_Audit; OPT_Performance_Budgets_FrameTime_VRAM_Limits; REND_URP_Graphics_HotPath_Optimization_HLOD; REND_Shader_Noir_Aesthetics_Dithering_Fog; STRM_Async_Asset_Upload_Texture_Settings.

## Inputs

- AGENTS.md
- PROJECT_BIBLES.md
- VISION_LOCKS.md
- TASTE.md
- quality.md
- rendering.md
- shaders.md
- water.md
- streaming.md
- 3DMODEL_TEXTURES_MATERIALS.md
- Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md
- Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md
- Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md
- Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv
- Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv
- Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv
- taskslocal/asset_system_20260605/ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md

## CSV Shape

priority,family,asset_path,blocker_type,static_evidence,likely_owner_packet,required_readback,forbidden_shortcut,proof_required,status

Generated target rows: 124.

- P0 texture blocker rows from TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv: 45.
- Foam/contact rows: 2.
- Flora/coral/kelp texture rows: 40.
- Proxy geology texture rows: 3.
- Material-route rows added from MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv and validator evidence: 79.

## Top Blockers

1. Foam/contact is P0 because rejected foam.png is serialized-reachable through active world/ocean material paths in 02_HECTON_WORLD.unity. Next owner: ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md; prerequisite readback: Crest/ocean visible slots, foam/contact material slots, bright shoreline screenshot, Frame Debugger/Stats, memory/VRAM.
2. WorldProceduralProxy flora/coral/kelp is P0 because 40 P0 texture rows and direct material rows point at proxy contamination or material-proof blocks. Next owner: ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md; prerequisite readback: active renderers, final non-proxy material family, alpha/dither path, LOD/silhouette proof, import settings.
3. Placeholder/blockout/default product-face routes are P0 because the Unity batchmode validator failed tool placeholder materials, MAT_PlayerSwimBlockout.mat, and package-default URP Lit.mat renderer slots. Next owner: ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md; prerequisite readback: exact renderer slots and route-owned replacement family contracts.
4. Null/empty texture-role routes are P0 for the product-face rows because static material scan reports missing texture GUIDs, empty slots, missing albedo, missing normal, missing packed mask, or missing channel declarations. Next owner: ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md; prerequisite readback: effective shader slots, channel semantics, import role matrix rows.
5. Sky/Aegir is included as validator-backed P0 readback target even though sky/Aegir texture rows are P1 in the texture blocker CSV. Evidence: VISUAL_REFERENCE_REJECTION_20260605.md rejects current Aegir/sky quality and ASSET_OWNER_18 failed the sky/ocean source gate. Next owner: material repair owner plus no-mutation Unity readback owner; prerequisite readback: Mat_HectonSky, Aegir material slots, live skybox/scene binding, screenshots, Frame Debugger/Stats.

## Exact Next Owner Mapping

- foam_contact: taskslocal/asset_system_20260605/ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md; read with taskslocal/asset_system_20260605/ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md before mutation.
- water_ocean_contact: same owner; Crest canonical asset route only; no wrapper/clone/override.
- flora_coral_fauna: same owner; active route proof must replace or disprove proxy material contamination.
- proxy_placeholder: same owner; reject visible route placement until route-owned material family exists.
- player_product_face, unclassified_first_party tool placeholder rows, and package_default_material: same owner; prove player/tool/pickup/transport renderer slots before binding replacements.
- sky_aegir_cloud: same owner plus no-mutation Unity readback; no promotion from static material tokens.

## Forbidden Shortcuts

- No raw YAML edits to .mat, .unity, .prefab, .asset, or .meta files.
- No Crest runtime wrappers, material clones, overrides, or artist textures in _WD_* wave-data lanes.
- No foam.png, WorldProceduralProxy, WorldRuntime placeholder, blockout, package-default, null material, or empty-role material promotion into visible product-face routes.
- No fog, darkness, bloom, vignette, storm grade, cropped camera, or green haze used to hide weak art.
- No generated/source-only direct import as final material without cleanup, PBR role separation, import readback, material binding proof, screenshots, and memory proof.

## Proof Required Before Promotion

- Unity readback table for every target row that gets repaired.
- Texture import readback for every changed map: sRGB, type, compression, mips, streaming mips, max size, read/write state, platform overrides.
- Material family/channel manifest: albedo/base, normal/detail-normal, packed MRAO/mask, emission/wetness/contact/alpha roles where used.
- h8_1475 proof packet with manifest, checksum, copied Unity log, canonical screenshots, Console, and evidence list.
- Frame Debugger or RenderGraph/Stats proof for material assets, shader names, keywords, SetPass, batches, material instance count, and Crest visible-slot use.
- Memory/VRAM proof for texture residency and compact 1800 MB VRAM / 900 MB texture budget risk.

## Low / Middle / High / Ultra Consequences

- Low/compact: no proxy or flat fallback; preserve bright ocean color, waterline breakup, sky/Aegir readability, organic silhouettes, and product-face material identity through compressed role-correct maps and controlled residency.
- Middle: route-owned PBR stacks, import-role proof, stable shared materials, dithered LOD/material transition proof, and clean active scene binding.
- High: spend saved cost on wet-edge detail, stronger Aegir/cloud material depth, richer flora/coral detail maps, and longer near-field texture residency after proof.
- Ultra: add layered hero material response, denser route dressing, richer shoreline/organic overdetail, and capture-grade sky/ocean response only after render and memory proof. Gameplay truth and ownership route do not change.

## Regression Model

- CPU: static table only; future material repair risks SetPass growth, material instance growth, shader keyword growth, and renderer-state churn.
- GC: no runtime code touched; no 0 B/frame claim.
- Memory/VRAM: static source and serialized refs only; residency, compression, and streaming mips remain unproven.
- Cadence: no runtime cadence changed.
- Correctness: row-level targets reduce owner guessing; active renderer binding and visual floor remain PENDING UNITY READBACK.

Final status: PENDING UNITY READBACK.
