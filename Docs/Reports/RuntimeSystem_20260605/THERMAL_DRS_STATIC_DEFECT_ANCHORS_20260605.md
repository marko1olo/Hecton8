# Thermal DRS Static Defect Anchors - 2026-06-05

Status: `STATIC_SOURCE_ANCHORS / PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.

CSV: `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.csv`.

## Scope

This report records source anchors for the `ThermalDynamicResolutionAdapter` runtime defects that cannot be repaired while the process gate is red. It does not prove compile, Unity state, Play Mode, profiler, GC, DRS recovery, or dump artifact creation.

Target:

- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`

## Findings

- Coroutine repair defect: `StartCoroutine`, `System.Collections.IEnumerator`, `yield return null`, and `StopAllCoroutines` remain in the DRS dispatcher registration repair path.
- Black-box dump defect: fixed historical `Dump_13KRA.bin` remains, `ResolveBlackBoxDumpPathCold()` sets `_blackBoxDumpPath = null`, and static scan finds no file/stream/write route for a binary dump artifact.
- Current source computes `_blackBoxDumpHash` and sets `_blackBoxDumped = true`; that is not a crash/postmortem dump artifact.

## Required Combined Repair

- Coordinate `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md` and `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md`.
- Use one source edit, one compile pass, and one Play Mode/profiler proof pass after the process gate clears.
- Keep `TelemetryCapacity = 300` and `DrsTelemetryEntryBytes = 64`.
- Keep retry and dump paths zero-GC in hot capture. Any file path resolution must be cold or in a designated postmortem window.

Final status: `PENDING VERIFICATION`.
