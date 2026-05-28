# Unity Hardware Thermal Blackbox ABI Pass - UNKNOWN - 2026-05-28

## Scope

- Domain: Core hardware telemetry, DataVault route, crash blackbox artifact.
- Primary source: `Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs`.
- Evidence class: static source and contract checks only.
- Runtime proof not claimed: no Unity Editor import, Play Mode, profiler, GCMonitor, player build, device run, or dump-reader execution.

## Findings

| Finding | Evidence | Risk |
|---|---|---|
| Thermal blackbox ABI mismatch | `HardwareThermalTelemetryEntry` was 64 bytes while `DumpBlackBoxCold()` wrote header stride `24` and `stackalloc byte[24]` | Crash/postmortem dump reader consumes the wrong record stride |
| Mutating resolve helpers | `TryResolveThermalSeverity()` and `TryResolveThermalBlackBox()` could acquire or allocate DataVault-backed mutable views | Read/resolve contract violation and hidden native mutation route |
| Weak rebind path | DataVault replacement disposed old handles but did not warm new thermal native state while the service was active | First tick after rebind could become the implicit allocation route |

## Changes

| Area | Change |
|---|---|
| Blackbox stride | Added `HardwareThermalTelemetryEntryBytes=64` and used it for struct size, dump header stride, and per-entry dump buffer |
| Dump read route | `DumpBlackBoxCold()` now opens `TryReadThermalBlackBox()` read-only view |
| Severity write route | `SampleAndApplyCold()` writes severity through `OpenOrAcquireThermalSeverityWriteView()` and releases the write lock in `finally` |
| Blackbox write route | `WriteBlackBox()` writes ring entries through `OpenOrAcquireThermalBlackBoxWriteView()` and releases the write lock in `finally` |
| Helper contracts | Removed mutating `TryResolveThermalSeverity()` and `TryResolveThermalBlackBox()` names |
| Hot-swap | DataVault service replacement now reopens native state when the thermal service is active and registered |

## Static Proof

| Check | Result |
|---|---|
| `git diff --check -- Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs` | exit `0`; line-ending warning only |
| `TryResolveThermalSeverity` occurrences | `0` |
| `TryResolveThermalBlackBox` occurrences | `0` |
| hard-coded dump stride `WriteInt32LittleEndian(header.Slice(12, 4), 24)` | `0` |
| hard-coded dump buffer `stackalloc byte[24]` | `0` |
| explicit struct size `Size = 64` | `0`; size now uses `HardwareThermalTelemetryEntryBytes` |
| `ReservedPadding` fields | `5` |
| `TryAcquireWriteLock` uses in file | `4` |
| `ReleaseWriteLock` uses in file | `6` |
| `IDataVault` write/read contract | `GlobalDataVault.cs` declares `TryReadOnlyHandle<T>()`, `TryAcquireWriteLock<T>()`, and `ReleaseWriteLock<T>()` |

## Build Boundary

No full solution build was launched in this pass. The live guard showed an active foreign build process, `dotnet.exe` `PID 40436`, and CPU `100%`. The user explicitly assigned current full-project compile errors to another agent, so this pass does not claim compile closure.

## Documentation Boundary

Full `VerifyDocStructure.py` and `OOP_Doc_Scanner.py` gates were not launched while CPU was `100%` and the foreign `dotnet.exe` build was active. Lightweight checks completed: report JSON parsed, report anchors exist in Status/Rationale/LOG/ledger, touched Markdown files have UTF-8 BOM, and scoped `git diff --check` passed.

## Verdict

This was a real Core correctness fix, not cosmetic cleanup. The crash blackbox dump ABI now matches the native telemetry record width, and thermal native writes use explicit DataVault write locks. Runtime microseconds saved claimed: `0`.
