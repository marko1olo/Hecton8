# VFX DataVault Sovereignty Static Review - 2026-06-05

Evidence class: STATIC_ONLY. No Unity compile, Play Mode, profiler, GCMonitor, or runtime mutation was performed.

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `telemetry.md`
- `systems.md`
- `performance.md`

## Commands

- `python Tools/DataVaultSovereigntyAudit.py --root Assets/_Project/Scripts/VFX --no-report --audit-json Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json --top 20`
- `python -m unittest Tools/test_data_vault_sovereignty_audit.py`

## Tool Output

- Audit output after scanner guard fix: `direct=18`, `allowed=12`, `forbidden=6`, `runtimeForbidden=6`, `editorOfflineTransientScratch=12`, `files=3`, `declarations=68`, `forbiddenDeclarations=6`, `persistentDeclarations=5`.
- Unit tests: 16 tests, OK.
- JSON artifact: `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json`.

The audit exit status is not an acceptance signal because `--fail-on-any` was not used. The counts are the evidence.

## Findings

### Runtime Debt

1. `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
   - Direct constructors: `1347`, `2005`.
   - Forbidden persistent declarations: `673`, `674`, `712`.
   - Verdict: real ownership blocker under current Memory Sovereignty rule. Future edits must migrate local persistent scratch to a DataVault-owned route or explicitly reject the touch.

2. `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`
   - Direct constructors: `330`, `3987`.
   - Forbidden declarations: `313`, `378`.
   - Context: black-box dump snapshot/write mirrors are owner-local persistent NativeArrays.
   - Verdict: runtime sovereignty debt unless a current route card or approved telemetry exception keeps these owner-local buffers.

3. `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs`
   - Direct constructor: `1483`.
   - Context: `Allocator.Temp` payload inside dump serialization.
   - Verdict: likely cold/fault dump path, not per-frame gameplay, but still needs telemetry-route review because fault export must not allocate unmanaged scratch without an approved owner path.

### Static Scanner False Positive

`Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs`

- Audit direct constructors: 12.
- Static context: file begins with `#if UNITY_EDITOR` at line `1` and ends with `#endif` at line `722`.
- Verdict after tool fix: editor-only transient scratch, not runtime debt. `DataVaultSovereigntyAudit.py` now classifies file-scoped `#if UNITY_EDITOR` sources as `Editor` for direct-constructor findings.

## Repair Rules For Future Owner

- Do not bulk-migrate every NativeArray blindly.
- First classify each buffer as runtime authority, diagnostic mirror, dump snapshot, editor-only scratch, or Burst job input.
- Runtime authority and persistent cross-frame scratch must be DataVault-owned.
- Black-box buffers may stay owner-local only if the route card states owner, capacity, schema, lifetime, dump trigger, disposal, and no gameplay authority.
- Editor-only scratch should be moved under an `Editor` folder or scanner updated to recognize file-level `#if UNITY_EDITOR` if this debt keeps polluting runtime reports.

## Verification State

Runtime VFX DataVault sovereignty: PENDING VERIFICATION.

Static evidence exists. No source repair was performed because the process gate was red and these files require compile/profiler proof after any mutation.
