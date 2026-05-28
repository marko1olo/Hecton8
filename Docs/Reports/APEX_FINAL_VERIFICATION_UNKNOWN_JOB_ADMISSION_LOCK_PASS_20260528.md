# APEX Final Verification - UNKNOWN Job Admission Lock Pass - 2026-05-28

Status: `PENDING_RUNTIME_VERIFICATION`

Evidence class: `STATIC_SOURCE`, `STATIC_DOC`

## Changed Source

- `Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs`
- Source SHA-256: `1EBF137AC6055BF2F3B3E8AC8EA03CAA8AD00CC10603654671AB4D4C70AD8FD7`

## What Was Wrong

`BurstTokenBucketJobAdmissionService` wrote DataVault-backed admission buffers through mutable resolve views in `Initialize`, `Refill`, `TryAdmitJob`, and `ReportJobCompleted`. That gave no explicit `SystemID.JobAdmission` writer-lock proof for `JobAdmissionLaneBudgets`, `JobAdmissionBaseRefill`, `JobAdmissionJobHashes`, and `JobAdmissionEwmaCosts`.

## What Changed

- `Initialize(...)` now acquires writer views for all five JobAdmission buffers before cold initialization writes and releases acquired locks in `finally`.
- `Refill(...)` now locks lane budgets and base refill before cadence/sanitization writes and releases both in `finally`.
- `TryAdmitJob(...)` now locks lane budgets for debit/debt mutation and reads cost state through read-only handles.
- `ReportJobCompleted(...)` now locks job hashes and EWMA costs before slot allocation/update and releases both in `finally`.
- Fault telemetry now reads through read-only handles instead of mutable `Resolve*` helpers.

## Zero-GC Static Added-Line Scan

- Added lines: `256`
- Reference `new` suspects: `0`
- `string.Format`: `0`
- `.ToString()`: `0`
- LINQ tokens: `0`
- `foreach`: `0`
- `.Complete()`: `0`
- Added `GlobalRegistry.`: `0`
- Added binary low-end switches: `0`

## Data Sovereignty

- `BufferID.JobAdmissionLaneBudgets = 283`
- `BufferID.JobAdmissionBaseRefill = 284`
- `BufferID.JobAdmissionJobHashes = 285`
- `BufferID.JobAdmissionEwmaCosts = 286`
- `BufferID.JobAdmissionBlackBox = 287`
- Writer-lock helper: `BurstTokenBucketJobAdmissionService.cs:484-494`
- Release helper: `BurstTokenBucketJobAdmissionService.cs:496-501`
- Mutable `Resolve*` helpers after patch: `0`
- Mutable `Resolve*` calls after patch: `0`

## Struct Layout

`JobAdmissionBlackboxEntry` is explicit size `64` bytes, multiple of 8.

- `FrameSequence`: offset `0`, uint32
- `JobHash`: offset `4`, uint32
- `EstimatedCostMs`: offset `8`, float32
- `RemainingBudgetMs`: offset `12`, float32
- `CriticalDebtFrames`: offset `16`, int32
- `KillSwitchMask`: offset `20`, uint32
- `Lane`: offset `24`, byte
- `Flags`: offset `25`, byte
- `Reserved`: offset `26`, uint16
- `StateHash`: offset `28`, uint32
- `_pad0.._pad31`: offsets `32-63`, bytes

## Scalability / Cinematic Cheat

No physical simulation was added. This remains a scalar admission/load-shed gate. `Refill(float globalQualityWeight01, ...)` keeps continuous scaling via `SanitizeQualityWeight01`, `SmoothStep01`, and `math.lerp(SurvivalBudgetScalar, 1f, qualityCurve01)`. No `isLowEnd` branch was added.

## Build Throttle

Build invocations: `0`.

CPU sample: `90.0%`.

Active processes: `dotnet.exe PID 28668`, `csc.exe PID 21340`.

Build was skipped because AGENTS forbids build under CPU > 50% or active dotnet/csc, and broad compile repair is assigned to another agent.

## Residual Risk

No Unity import, Console check, PlayMode run, profiler/GCMonitor pass, player build, device run, or crash dump was produced. Runtime status remains `PENDING_RUNTIME_VERIFICATION`.
