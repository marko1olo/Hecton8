# DataVault Audit Execution Surface Recheck - 2026-06-05

Status: `STATIC_RECHECK_COMPLETE`
Evidence class: `STATIC_SOURCE_TOOL_OUTPUT`

## Scope

This recheck originally investigated the MarineSnow line-surface mismatch found while building `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.md/.csv`. Later current-disk source readback found that the live MarineSnow runtime route had been rewritten through DataVault. The old JSON facts below are therefore historical scanner evidence, not current source-repair anchors.

No runtime C# source was edited. No Unity, Play Mode, profiler, import, build, dotnet, csc, msbuild, package restore, shader compiler, or Jules CLI command was run.

## Commands

- `python -B -m unittest Tools.test_data_vault_sovereignty_audit`
- CSV parse for `Docs/AssetAudit/VFX_DATAVAULT_REPAIR_ANCHOR_MAP_20260605.csv`
- Historical static source readback for `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` around lines `1347`, `1952`, `2005`, `2243`, and `2280`
- Current disk source readback for `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` around DataVault handles/write paths `429`, `432`, `436`, `2560`, `2763`, `2984-3021`, and editor/offline scratch `1948`
- JSON payload readback for `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json`

## Results

- Unit tests: `Ran 18 tests in 0.186s`, `OK`.
- Anchor CSV: `CSV_ROWS=12 WIDTHS=[14] EMPTY_CELLS=0`.
- Historical source snapshot line `1347` was outside the editor-only CSV reader region and was a runtime constructor anchor in the audit JSON snapshot.
- Historical source snapshot line `2005` was inside the editor-only CSV reader region:
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
- Current disk source no longer contains `_mockWakeScratch`, `_propwashEventScratch`, or `EnsureRuntimeScratchBuffers()`.
- Current disk source contains DataVault handles/write paths:
  - `_dynamicWakeDtoHandle` at `429`
  - `_propwashEventHandle` at `432`
  - `_propwashWakeProfileHandle` at `436`
  - `TryWriteMockWakeVaultAndGpu(...)` at `2560`
  - `TryBuildAndPublishMockPropwashEvents(...)` at `2763`
  - `HarvestProceduralWakeSourcesIntoPropwash(...)` / write bridge at `2984-3021`
- Current disk editor/offline wake-profile scratch constructor is at `1948`.

## Verdict

The historical JSON split was not a live `DataVaultSovereigntyAudit.py` preprocessor classifier defect.

The JSON marks `1347` as `Runtime` and `2005` as `Editor` for that audit snapshot. Current disk source supersedes those runtime repair anchors: MarineSnow runtime wake/propwash scratch appears rewritten through DataVault. Current `1948` remains editor/offline owner-route debt, not gameplay runtime DataVault debt.

The stale/incorrect layer was human summary and anchor-map interpretation, not the scanner line-surface classifier. A scanner re-run is still required before claiming the current source route is accepted.

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
