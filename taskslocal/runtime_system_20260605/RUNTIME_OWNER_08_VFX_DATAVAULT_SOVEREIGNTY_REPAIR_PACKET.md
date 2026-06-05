# Runtime Owner 08 - VFX DataVault Sovereignty Repair Packet

Status: `PENDING SOURCE REPAIR AND UNITY PROOF`
Evidence class: `STATIC_TOOL_OUTPUT`
Route moment: first-20 stability proof. VFX black-box, wake, and dump paths must keep bounded evidence without owner-local persistent runtime NativeArray debt.

This packet did not mutate Unity assets, scenes, prefabs, materials, project settings, or runtime C# sources.

## Mandates

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `telemetry.md`
- `systems.md`
- `performance.md`

## Evidence Basis

- `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_STATIC_REVIEW_20260605.md`
- `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json`
- `Docs/AssetAudit/VFX_DATAVAULT_SOURCE_CONTEXT_REVIEW_20260605.md`
- `Docs/AssetAudit/VFX_DATAVAULT_SOURCE_CONTEXT_REVIEW_20260605.csv`
- `Tools/DataVaultSovereigntyAudit.py`
- `Tools/test_data_vault_sovereignty_audit.py`

## Static Tool Facts

- Command: `python Tools/DataVaultSovereigntyAudit.py --root Assets/_Project/Scripts/VFX --no-report --audit-json Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json --top 20`.
- Output: `direct=18`, `allowed=12`, `forbidden=6`, `runtimeForbidden=4`, `editorOfflineForbidden=2`, `editorOfflineTransientScratch=12`, `forbiddenDeclarations=6`, `persistentDeclarations=5`, `jobInputDeclarations=62`.
- `python -m unittest Tools/test_data_vault_sovereignty_audit.py`: 18 tests OK.
- Scanner now classifies constructor execution surface per line, so mixed runtime/editor files are not treated as all-runtime.

## Runtime Debt To Repair

1. `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`
   - Source decision comment: `311-315` (`SOURCE DECISION BIOLUM_BLACKBOX_OWNER_LOCAL_20260605`).
   - Runtime persistent direct constructors: `336`, `3993`.
   - Runtime forbidden persistent declarations: `319`, `384`.
   - Context: black-box dump snapshot/write mirrors are owner-local diagnostic NativeArrays. Current source decision names Session lifetime, owner disposal, no gameplay authority, no cross-domain snapshot contract, and no blind DataVault migration.
   - Required handling: do not blind-migrate Biolum mirrors. Remaining proof is compile, Unity, GC/profiler, scanner recheck, and deterministic dump artifact.

2. `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
   - Current source rewrite anchors: DataVault handles at `429`, `432`, and `436`; mock wake write-lock path at `2560`; propwash write-lock path at `2763`; wake-source bridge at `2984-3021`.
   - Historical audit anchors: old JSON/source-context rows recorded `673`, `674`, `1347`, and `2005`; those are not the current disk route for runtime wake/propwash scratch.
   - Context: `_mockWakeScratch`, `_propwashEventScratch`, and `EnsureRuntimeScratchBuffers()` are absent in current source readback. Mock wake, propwash, and procedural wake-source paths now route through DataVault handles/write buffers.
   - Required handling: preserve the DataVault rewrite. Do not reintroduce runtime scratch fields. Remaining proof is scanner rerun, compile, Unity, GC/profiler, and VFX route exercise.

3. `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs`
   - Runtime direct constructor: `1483`.
   - Context: `Allocator.Temp` payload inside dump serialization. It is probably a cold/fault path, but still needs a telemetry route review because fault export must not allocate unmanaged scratch without an approved owner path.

## Editor/Offline Debt Not To Misclassify As Runtime

1. `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3018`
   - Editor/offline persistent CSV staging inside an `#if UNITY_EDITOR` block.
   - Required handling: move under an Editor-only surface or document an editor/offline owner route. Do not migrate this as gameplay runtime state.

2. `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1948`
   - Editor/offline wake-profile CSV parse scratch inside `#if UNITY_EDITOR`.
   - Source-context review corrected earlier routing: old audit JSON rows are historical for current disk source; current runtime wake/propwash scratch appears rewritten through DataVault.
   - Required handling: move under an Editor-only surface or document an editor/offline owner route. Do not count it as runtime VFX gameplay debt.

## Required Repair Shape

1. Do not edit while CPU is over 50 percent or Unity/import/compiler/shader/package/dotnet/csc work is active.
2. Before source mutation, classify every target buffer as one of:
   - runtime authority;
   - runtime diagnostic mirror;
   - black-box dump snapshot;
   - editor/offline scratch;
   - Burst job input view.
3. Runtime authority and persistent cross-frame scratch must be DataVault-owned or resolved as bounded generation-checked DataVault views inside the owning phase.
4. Owner-local black-box mirrors are acceptable only with an explicit route card or in-file decision record naming owner, capacity, schema, lifetime, dump trigger, disposal, no gameplay authority, and no hot allocation. Biolum has the in-file decision record; it still lacks compile, Unity, GC/profiler, scanner recheck, and dump artifact proof.
5. Fault/dump serialization must use bounded buffers and must not allocate unmanaged scratch in gameplay hot paths.
6. Editor/offline persistent scratch must be isolated from runtime classification. Prefer an Editor-only file/surface if the code is editor-only.
7. Do not change DTO layout, public VFX contracts, SignalBus payloads, save identity, or gameplay truth ownership.
8. Keep continuous `GlobalQualityWeight` behavior. Do not introduce low/high binary branches while repairing memory ownership.

## Forbidden

- Do not bulk-migrate all `NativeArray<T>` declarations. Job input views and DataVault-resolved views are not the same defect as persistent owner fields.
- Do not move editor CSV tooling into runtime just to satisfy the scanner.
- Do not add `GlobalRegistry` hot polling, `Find*`, `GetComponent`, LINQ, managed heap collections, or string formatting in VFX hot paths.
- Do not add `NativeArray<T>`, `NativeList<T>`, or `NativeQueue<T>` persistent fields in MonoBehaviour/runtime manager owners.
- Do not use `Debug.Log` as black-box proof.
- Do not run Unity, Play Mode, player build, profiler, or `dotnet build` while process gate is red.

## Proof Required After Repair

- Static:
  - VFX audit command reports `runtimeForbiddenDirectConstructors=0` or each retained runtime constructor has an explicit approved exception route.
  - `forbiddenNativeCollectionDeclarations` does not increase.
  - Scanner still reports editor/offline constructors separately from runtime constructors.
  - `python -m unittest Tools/test_data_vault_sovereignty_audit.py` remains green.
- Compile:
  - Clean C# compile after process gate clears.
  - Unity Console has no new VFX compile/import errors.
- Runtime:
  - Play Mode VFX route exercises Biolum, MarineSnow, and PlasmaBeam dump/fault paths where feasible.
  - GCMonitor/profiler proof shows VFX hot paths remain `0 B/frame`.
  - Black-box dumps, if triggered, produce deterministic bounded binary artifacts with fixed schema and no hot managed allocation.

## Acceptance State

PENDING VERIFICATION. Static classification is fixed; runtime source repair and Unity/profiler proof do not exist yet.
