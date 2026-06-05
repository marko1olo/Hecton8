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
- Historically, the dump filename was hardcoded to an agent ID.
- Previously, no file stream or write route existed in the adapter source.
- Telemetry row serialization only computed a hash.
- The dump path resolution originally assigned a null path.
- The dumped state was historically set without an artifact.

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

Historically, the DRS adapter used a fixed agent dump name. This may have been correct inside an older explicit batch lane, but no active agent ID exists in the current ordinary work mode.

Current root rule says explicit batch/log IDs are not invented. The black-box rule says no-ID critical dumps must use a deterministic system-name/timestamp route instead of an arbitrary agent file.

A second historical defect marked the black box as dumped without writing a deterministic binary artifact. This was a DRS telemetry ownership and proof defect.

## Completed Repair Shape / Pending Verification

1. The fixed `Dump_13KRA.bin` route was replaced with an owner/system route `Dump_THERMAL_DRS_<timestamp>.bin` (or similar owner route).
2. The 300-record ring and fixed record-size serialization are kept.
3. An actual bounded binary artifact write is implemented using `NativeFaultDumpWriter.TryWriteAll`.
4. `_blackBoxDumped = true` is set only after the artifact write succeeds.
5. The dump path is resolved in cold setup.
6. Public DRS contracts and DTO layout are unchanged.
7. Source mutation is complete. Next step is verification when process gate clears.

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
