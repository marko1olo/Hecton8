# Thermal DRS Static Defect Anchors - 2026-06-05

Status: `SOURCE PATCHED / PENDING FULL COMPILE AND UNITY PROOF`.
Evidence class: `STATIC_SOURCE`.

CSV: `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.csv`.

## Scope

This report records the prepatch source anchors for the `ThermalDynamicResolutionAdapter` runtime defects and the current static readback after the source patch. It does not prove compile, Unity state, Play Mode, profiler, GC, DRS recovery, or dump artifact creation.

Current postpatch source state is also tracked in:

- `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_POSTPATCH_STATIC_REVIEW_20260605.md/.csv`

Target:

- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`

## Current Static Readback

- Coroutine repair defect: `SOURCE PATCHED / PENDING FULL PROOF`. `StartCoroutine`, `System.Collections.IEnumerator`, `yield return null`, and `StopAllCoroutines` are no longer present in `ThermalDynamicResolutionAdapter.cs`.
- Black-box dump defect: `SOURCE PATCHED / PENDING FULL PROOF`. Fixed historical `Dump_13KRA.bin` is gone, `ResolveBlackBoxDumpPathCold()` assigns `_blackBoxDumpPath`, and static scan finds `NativeFaultDumpWriter.TryWriteAll` for a binary dump artifact route.
- Current source uses `DumpRelativeDirectory` and `DumpFilePrefix`.

## Required Combined Proof

- Coordinate `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md` and `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md`.
- Use one compile pass and one Play Mode/profiler proof pass after the process gate clears.
- Keep `TelemetryCapacity = 300` and `DrsTelemetryEntryBytes = 64`.
- Keep retry and dump paths zero-GC in hot capture. Any file path resolution must be cold or in a designated postmortem window.

Final status: `SOURCE PATCHED / PENDING FULL COMPILE, UNITY CONSOLE, PLAY MODE, GC, PROFILER, AND BINARY ARTIFACT PROOF`.

## Final Verification Summary
- **Exact static source evidence**: `STATIC_SOURCE_RECHECKED`. `ThermalDynamicResolutionAdapter.cs` has been scanned. No coroutine methods or stale `Dump_13KRA` found. Write routes for binary dump present.
- **Exact files changed**: `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.md`, `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.csv`, `taskslocal/runtime_system_20260605/RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md`, `taskslocal/runtime_system_20260605/RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md`, `taskslocal/runtime_system_20260605/README.md`.
- **Unverified because Unity/build/profiler were forbidden**: `PENDING_COMPILE`, `PENDING_UNITY`, `PENDING_PROFILER`, `PENDING_DUMP_ARTIFACT`.
