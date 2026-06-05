# Asset Static Validation Summary - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: asset-front CSV/doc hygiene after the current asset-only consolidation.

This file proves only static parse hygiene for current asset-front planning artifacts. It does not prove Unity import state, material binding, Addressables residency, audio mix behavior, visual quality, GC, frame time, or memory safety.

## Current Static Parse Set

| File | Rows | Columns | Empty cells |
|---|---:|---:|---:|
| `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv` | 11 | 9 | 0 |
| `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv` | 13 | 16 | 0 |
| `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv` | 11 | 15 | 0 |
| `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv` | 8 | 9 | 0 |
| `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv` | 13 | 9 | 0 |
| `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv` | 11 | 9 | 0 |
| `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv` | 8 | 9 | 0 |
| `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv` | 26 | 8 | 0 |
| `Docs/AssetAudit/GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.csv` | 26 | 13 | 0 |
| `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.csv` | 16 | 9 | 0 |
| `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.csv` | 11 | 13 | 0 |
| `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv` | 138 | 23 | 0 |
| `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv` | 138 | 15 | 0 |
| `Docs/AssetAudit/AUDIO_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv` | 15 | 17 | 0 |
| `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.csv` | 19 | 13 | 0 |
| `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.csv` | 16 | 18 | 0 |
| `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv` | 602 | 18 | 0 |
| `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv` | 40 | 11 | 0 |
| `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv` | 21 | 14 | 0 |
| `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv` | 10 | 15 | 0 |
| `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv` | 140 | 23 | 0 |
| `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.csv` | 140 | 19 | 0 |
| `Docs/AssetAudit/TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv` | 56 | 20 | 0 |
| `Docs/AssetAudit/MATERIAL_FILE_TECHNICAL_PROPERTIES_20260605.csv` | 392 | 16 | 0 |
| `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.csv` | 13 | 14 | 0 |
| `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` | 28 | 18 | 0 |
| `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv` | 109 | 21 | 0 |
| `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv` | 11 | 11 | 0 |
| `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv` | 52 | 7 | 0 |

Total current rows: `2094`.

## Static Hygiene Result

- CSV parse hygiene: all listed files parse with zero empty cells.
- Encoding hygiene: current touched asset docs reported `replacement_chars=0` in the latest scoped scan.
- Diff hygiene: scoped `git diff --check` over the current asset-front docs returned clean after this integration.
- Language hygiene: current flagged wording hits are negative caveats only, not acceptance claims.

## Current Process Gate

Latest sampled gate before this summary:

- CPU load: `45`.
- Active blocked processes: `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, `UnityShaderCompiler`.

Unity readback, import, Addressables build, Play Mode, project-setting work, scene/prefab save, and runtime audio/visual proof remain blocked until a fresh gate is clean.

## Regression Model

- CPU: static document work only; no runtime CPU change.
- GC: no runtime code changed; no GC claim.
- Memory/VRAM: no residency proof; current data only improves owner routing.
- Cadence: no runtime cadence changed.
- Correctness: future asset owners now have parse-clean route documents; product acceptance remains blocked by Unity/runtime proof.

Final status: `PENDING VERIFICATION`.
