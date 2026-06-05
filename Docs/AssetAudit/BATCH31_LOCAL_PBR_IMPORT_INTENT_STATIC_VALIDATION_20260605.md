# Batch31 Local PBR Import Intent Static Validation - 2026-06-05

Status: `STATIC_VALIDATION_ONLY / BLOCKED BEFORE UNITY PROMOTION`
Evidence class: `STATIC_ARTIFACT_REVIEW + PYTHON_UNIT_TEST`

No Unity run, Play Mode, import, material creation, prefab edit, `.meta` edit, Addressables change, or `Assets` mutation was performed.

## Scope

Reviewed artifacts:

- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md`
- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.csv`
- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.json`
- `Tools/Batch31LocalPbrImportIntent.py`
- `Tools/test_batch31_local_pbr_import_intent.py`

## Mandates Followed

- `AGENTS.md`
- `TASTE.md`
- `rendering.md`
- `terrain.md`
- `water.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Static Counts

CSV, JSON, and markdown summaries agree:

- Packages: `3`
- Texture rows: `21`
- Runtime import candidate rows: `6`
- Blocked rows: `3`
- Error rows: `0`
- Review rows: `0`
- Static-pass rows: `18`
- Channel-contract blocked packages: `3`

The three blocked rows are the `PackedMask`/`MRAOSource` rows. They are correctly marked `runtime_import=0` because source package semantics are MRAO while the target route contract states `_MaskMap ARM R=AO G=Roughness B=Metallic A=Emission/default1`.

## Unit Test Result

Command:

`python -m unittest Tools/test_batch31_local_pbr_import_intent.py`

Result:

- `Ran 6 tests in 1.606s`
- `OK`

The tests cover static report generation, blocked channel semantics, hash mismatch detection, unknown-role rejection, path-boundary rejection, absolute-path rejection, and CLI `--fail-on-error` behavior.

## Decision

Keep Batch31 as a blocked static import-intent package until a shader/material owner chooses the channel convention and proves the repack or relabel route.

Do not import Batch31 `MRAOSource` files into Unity as `_MaskMap` by name alone.

## Low / Middle / High / Ultra Consequences

Low/compact:

- Do not spend memory on unresolved packed masks. Import only after channel semantics and compression settings are owner-approved.

Middle:

- Candidate albedo/normal rows can be considered after Unity import proof, seam review, and material binding proof.

High:

- Stronger material response must come from correct mask semantics, normals, detail layering, and route screenshots, not guessed channel names.

Ultra:

- Near-field visual overkill can layer additional detail later, but source identity, channel semantics, and sampler count must remain explicit.

## Regression Model

- CPU: static review and unit tests only. No frame-time claim.
- GC: no runtime measurement. No `0 B/frame` claim.
- Memory: no Unity import or residency claim.
- Cadence: no runtime cadence claim.
- Correctness: importer intent blocks unsafe MRAO-to-ARM promotion. Visual acceptance remains absent.

Final status: `STATIC_VALIDATION_ONLY / BLOCKED BEFORE UNITY PROMOTION`.
