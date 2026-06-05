# DataVault Audit Execution Surface Recheck - 2026-06-05

Status: `STATIC_RECHECK_COMPLETE`
Evidence class: `STATIC_SOURCE_TOOL_OUTPUT`

## Scope

This recheck investigated the MarineSnow line-surface mismatch found while building `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.md/.csv`.

No runtime C# source was edited. No Unity, Play Mode, profiler, import, build, dotnet, csc, msbuild, package restore, shader compiler, or Jules CLI command was run.

## Commands

- `python -B -m unittest Tools.test_data_vault_sovereignty_audit`
- CSV parse for `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.csv`
- Static source readback for `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` around lines `1347`, `1952`, `2005`, `2243`, and `2280`
- JSON payload readback for `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json`

## Results

- Unit tests: `Ran 18 tests in 0.186s`, `OK`.
- Anchor CSV: `CSV_ROWS=12 WIDTHS=[14] EMPTY_CELLS=0`.
- Current source line `1347` is outside the editor-only CSV reader region and remains a runtime constructor anchor.
- Current source line `2005` is inside the editor-only CSV reader region:
  - `#if UNITY_EDITOR` at line `1952`
  - `_wakeProfileParseScratch = new NativeArray<PropwashWakeProfileDTO>(` at line `2005`
  - nested editor block closes at line `2243`
  - outer editor block closes at line `2280`
- Audit JSON direct-constructor row for `HectonMarineSnowRenderer.cs` records:
  - `lines`: `[1347, 2005]`
  - `lineExecutionSurfaces`: `["Runtime", "Editor"]`
  - `forbiddenLines`: `[1347, 2005]`
  - `forbiddenLineExecutionSurfaces`: `["Runtime", "Editor"]`
  - `executionSurface`: `Mixed`

## Verdict

This was not a live `DataVaultSovereigntyAudit.py` preprocessor classifier defect.

The JSON marks `1347` as `Runtime` and `2005` as `Editor`. Both are still forbidden constructor anchors because `Allocator.Persistent` editor/offline scratch also needs an approved editor/offline owner route, but only `1347` is runtime repair debt.

The stale/incorrect layer was human summary and anchor-map interpretation, not the scanner line-surface classifier.

## Files Updated

- `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_STATIC_REVIEW_20260605.md`
- `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.md`
- `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.csv`
- `Docs/AssetAudit/DATAVAULT_AUDIT_EXECUTION_SURFACE_RECHECK_20260605.md`

## Non-Claims

- No source repair performed.
- No compile proof.
- No Unity proof.
- No profiler/GC proof.
- No runtime dump proof.
