# Runtime Owner 07 - Thermal DRS Black-Box Dump Route Packet

Status: `SOURCE PATCHED / PENDING COMPILE AND UNITY PROOF`
Evidence class: `STATIC_SOURCE_PATCH + EDITOR_LOG_TAIL_READ`
Route moment: first-20 stability proof. Dynamic resolution failure must leave bounded evidence instead of a stale agent-owned artifact name.

## Mandates

- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `telemetry.md`
- `performance.md`
- `systems.md`

## Target

- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`

## Evidence Basis

- `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.md/.csv`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md`

## Original Static Facts

- `ThermalDynamicResolutionAdapter.cs:35` defines `TelemetryCapacity = 300`.
- `ThermalDynamicResolutionAdapter.cs:57` defines `DumpFileName = "Dump_13KRA.bin"`.
- Static scan finds no `File.*`, stream, or `WriteAll*` write route in `ThermalDynamicResolutionAdapter.cs`.
- `ThermalDynamicResolutionAdapter.cs:2174-2209` builds a 20-byte header and fixed 64-byte telemetry row span only to compute `_blackBoxDumpHash`.
- `ThermalDynamicResolutionAdapter.cs:2213` resolves the dump path through `ResolveBlackBoxDumpPathCold()`, but that method currently sets `_blackBoxDumpPath = null`.
- Current static source sets `_blackBoxDumped = true` after hash calculation, not after a binary artifact is written.

## Current Patched Facts

- `Dump_13KRA` scan count in `ThermalDynamicResolutionAdapter.cs`: zero.
- The dump route now resolves a cold owner/system filename with prefix `Dump_THERMAL_DRS_` under `Docs/AgentLogs`.
- `ResolveBlackBoxDumpPathCold()` now assigns `_blackBoxDumpPath` instead of null.
- `DumpBlackBoxOnceLocked(...)` now creates a bounded transient payload, writes the 20-byte header plus 300 fixed 64-byte DRS telemetry rows, hashes the emitted bytes, and calls `NativeFaultDumpWriter.TryWriteAll(...)`.
- `_blackBoxDumped` is now set only when `NativeFaultDumpWriter.TryWriteAll(...)` returns true. Failed write paths publish `DumpIoFailureHash` to `GlobalTelemetryBus`.
- `TelemetryCapacity = 300` and `DrsTelemetryEntryBytes = 64` are preserved.
- `BinaryWriter`, `JsonUtility`, and `Dump_13KRA` scan count in the touched file: zero.
- `git diff --check` on the touched C# file returned only a CRLF normalization warning.
- Editor.log tail scan after Unity import showed no `error CS`, no `Compilation failed`, and no `ThermalDynamicResolutionAdapter` compile error, but this is log-tail evidence only, not full compile proof.

## Problem

The live DRS adapter keeps a fixed historical agent dump name, `Dump_13KRA.bin`. This may have been correct inside an older explicit batch lane, but no active agent ID exists in the current ordinary work mode.

Current root rule says explicit batch/log IDs are not invented. The black-box rule says no-ID critical dumps must use a deterministic system-name/timestamp route instead of an arbitrary agent file.

Static source review shows a second defect: the DRS path marks the black box as dumped without writing a deterministic binary artifact. This is not a VFX catalog parity defect. It is a DRS telemetry ownership and proof defect.

## Required Repair

1. Replace the fixed `Dump_13KRA.bin` route with an owner/system route for ordinary runtime, for example `Dump_THERMAL_DRS_<timestamp>.bin`, unless a current explicit batch ID is active.
2. Keep the 300-record ring and fixed record-size serialization.
3. Implement an actual bounded binary artifact write to the resolved dump path. `_blackBoxDumped = true` is valid only after the artifact write succeeds or an explicit failure flag is recorded.
4. Resolve the dump path in cold setup or a designated fault/postmortem window. Do not allocate a fresh task, timestamp string chain, `BinaryWriter`, JSON object, LINQ query, or heap collection from telemetry hot capture.
5. Keep a compact manifest route if the implementation already has one; if not, record owner, schema version, record size, record count, trigger, scene/build if available.
6. Do not change DTO layout or public DRS contracts.
7. Coordinate with `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md`; both repairs touch the same file and must be one compile/profiler pass when the process gate clears.

## Forbidden

- Do not rename to another invented agent ID.
- Do not write into `Docs/AgentLogs/Dump_[ID].bin` unless an explicit current ID exists.
- Do not use `Debug.Log` as proof.
- Do not add `BinaryWriter`, `JsonUtility`, LINQ, string interpolation, or heap collections in telemetry hot capture.
- Do not run Unity, Play Mode, player build, profiler, or `dotnet build` while CPU is above 50 percent or Unity/import/compiler processes are active.

## Proof Required After Repair

- Static scan:
  - no `Dump_13KRA.bin` in `ThermalDynamicResolutionAdapter.cs`;
  - no invented `Dump_[agent].bin`;
  - resolved deterministic owner/system dump route exists;
  - binary file write path exists and is outside hot telemetry capture;
  - `TelemetryCapacity = 300` preserved;
  - `DrsTelemetryEntryBytes = 64` preserved;
  - dump method still writes fixed binary rows.
- Compile proof after process gate clears.
- Play Mode proof with forced invalid DRS state or NaN trigger.
- Artifact proof: one binary dump exists under the repaired deterministic owner route.
- GCMonitor/profiler proof: telemetry write path remains 0 B/frame.

## Acceptance State

SOURCE PATCHED. Full acceptance is still blocked until clean compile, Unity Console, Play Mode forced invalid DRS trigger, binary artifact proof, GCMonitor, and profiler proof exist.
