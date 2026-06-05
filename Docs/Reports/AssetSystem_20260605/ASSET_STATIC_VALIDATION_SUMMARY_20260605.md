# Asset Static Validation Summary - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: curated asset-front CSV/doc hygiene after the current asset-only consolidation.

This file proves only static parse hygiene for the curated current asset-front planning artifacts listed below. It does not prove whole-folder CSV hygiene, Unity import state, material binding, Addressables residency, audio mix behavior, visual quality, GC, frame time, or memory safety.

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
| `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv` | 7420 | 21 | 0 |
| `Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.csv` | 800 | 15 | 0 |
| `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv` | 3488 | 15 | 0 |
| `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv` | 77 | 7 | 0 |
| `Docs/AssetAudit/PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv` | 124 | 10 | 0 |
| `Docs/AssetAudit/PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` | 39 | 11 | 0 |
| `Docs/AssetAudit/AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.csv` | 6 | 9 | 0 |
| `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` | 120 | 7 | 0 |
| `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` | 7 | 8 | 0 |
| `Docs/AssetAudit/ASSET_P0_TARGET_TABLE_ROUTING_SYNTHESIS_20260605.csv` | 5 | 14 | 0 |
| `Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.csv` | 36 | 13 | 0 |
| `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.csv` | 7 | 7 | 0 |

Total current rows: `14171`.

## Excluded Older/Sidecar CSV Boundary

The whole `Docs/AssetAudit/*.csv` folder currently contains `43` CSV files; `39` of those are in this curated zero-empty set. The visual critique checklist CSV lives under `Docs/Reports/AssetSystem_20260605/` and is also included above. The following older/sidecar CSVs are outside the curated zero-empty set and must not be treated as covered by the result above:

| File | Rows | Empty cells | Boundary |
|---|---:|---:|---|
| `Docs/AssetAudit/AUDIO_ASSET_STATIC_LEDGER_20260605.csv` | 138 | 0 | Older ledger, not part of current curated asset-front parse set. |
| `Docs/AssetAudit/TEXTURE_ASSET_STATIC_LEDGER_20260605.csv` | 190 | 202 | Older/source ledger with known empty cells. |
| `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv` | 190 | 152 | Older disposition table with known empty cells. |
| `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv` | 141 | 831 | Usage-map sidecar with known sparse fields. |

## Static Hygiene Result

- CSV parse hygiene: 40 files parse with zero empty cells.
- Encoding hygiene: scoped replacement-character scan returned `0` in the latest run.
- Diff hygiene: scoped `git diff --check` returned no whitespace errors in the latest run; Git reported CRLF normalization warnings only.
- Language hygiene: current proof-language hits are negative caveats, evidence-boundary phrasing, or section headings; no Unity/runtime/visual/audio acceptance claim is accepted from this static pass.

## Current Process Gate

Latest sampled gate before this summary:

- CPU load: `8`.
- Active blocked processes: `dotnet`, `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, `UnityShaderCompiler`.

Unity readback, import, Addressables build, Play Mode, project-setting work, scene/prefab save, and runtime audio/visual proof remain blocked until a fresh gate is clean.

## Regression Model

- CPU: static document work only; no runtime CPU change.
- GC: no runtime code changed; no GC claim.
- Memory/VRAM: no residency proof; current data only improves owner routing.
- Cadence: no runtime cadence changed.
- Correctness: future asset owners now have parse-clean route documents, a static GUID reference graph, compact active-route GUID triage, unreferenced cleanup-review triage, compact P0 target-table routing synthesis, and asset owner packet index; product acceptance remains blocked by Unity/runtime proof.

Final status: `PENDING VERIFICATION`.
