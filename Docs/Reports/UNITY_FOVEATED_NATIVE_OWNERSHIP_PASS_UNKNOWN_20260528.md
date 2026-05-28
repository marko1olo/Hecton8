# Unity Foveated Native Ownership Pass - UNKNOWN - 2026-05-28

Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE_ONLY

## Scope

Source inspected and changed:

- `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

Current source SHA-256:

- `1B0EE455A705CC22B82DD0391E50451AE2420644569D5717B5C4879304426338`

Current workspace boundary:

- The Core target file was clean before this follow-up patch.
- `SystemDispatcher.cs` was not dirty before the single call-site rename.
- Other agents have active dirty files and active compiler processes; this pass avoided their dirty files.
- Full build and Unity runtime proof were not run.

## What Was Wrong

The earlier foveated pass removed persistent native aliases, but the APEX self-audit exposed one real residual:

- DataVault write discipline was not proven. `TryAcquireWriteLock` count in the file was `0`.
- Score-position writes and telemetry-ring writes still used raw resolved native views.
- Scheduled importance jobs kept DataVault-backed pointers without explicit relocation pins.

This made the earlier report directionally correct, but not acceptable as a final Data Sovereignty proof.

## What Was Changed Now

- Added `TryAcquireWriteLock` around score-position writes in `TryWriteScorePositionsForImportanceJob()` at lines `1382-1409`.
- Added `TryAcquireWriteLock` around telemetry writes in `WriteTelemetryFrame()` at lines `1120-1157`.
- Both writer locks release via `finally`.
- Added relocation pins for the seven buffers owned by the scheduled importance job at lines `1412-1445`.
- Added deterministic unlock route for those job pins at lines `1447-1484`.
- Job pins are released on normal completion, schedule failure, resolve failure, reset/dispose, and forced completion paths.
- Renamed the mutating tick route from `TryResolveTick` to `TryAdvanceTick` at the interface, implementation, and dispatcher call site.

The earlier alias cleanup remains intact:

- Persistent private `NativeArray<T>` aliases in `FoveatedSimulationManager.cs`: `0`.
- DataVault handle fields remain persistent, but native views stay method-scoped.

## BufferIDs

| Buffer | BufferID | Current route |
|---|---:|---|
| FoveatedScorePositions | 73220 | `TryAcquireWriteLock` for main-thread fill; `TryLockBuffer` while job owns pointer |
| FoveatedEntityAups | 73221 | `TryLockBuffer` while job owns pointer |
| FoveatedImportanceScores | 73222 | `TryLockBuffer` while job owns pointer |
| FoveatedTickRateCodes | 73223 | `TryLockBuffer` while job owns pointer |
| FoveatedInsideFrustumFlags | 73224 | `TryLockBuffer` while job owns pointer |
| FoveatedEntitySimTiers | 73225 | `TryLockBuffer` while job owns pointer |
| FoveatedDistancesMeters | 73226 | `TryLockBuffer` while job owns pointer |
| FoveatedFromPositions | 73227 | DataVault owned, currently not used by scheduled job |
| FoveatedToPositions | 73228 | DataVault owned, currently not used by scheduled job |
| FoveatedAlphas | 73229 | DataVault owned, currently not used by scheduled job |
| FoveatedTelemetryRing | 73234 | `TryAcquireWriteLock` for telemetry write |

## Zero-GC Text Scan

Changed hot ranges scanned:

| Range | reference `new` | struct entry `new` | `string.Format` | `.ToString()` | LINQ | `foreach` |
|---|---:|---:|---:|---:|---:|---:|
| `TryAdvanceTick` `514-547` | 0 | 0 | 0 | 0 | 0 | 0 |
| `TryCompleteFrameJobsInternal` `652-688` | 0 | 0 | 0 | 0 | 0 | 0 |
| `ScheduleImportanceScoringJob` `829-879` | 0 | 1 | 0 | 0 | 0 | 0 |
| `WriteTelemetryFrame` `1120-1158` | 0 | 1 | 0 | 0 | 0 | 0 |
| `TryWriteScorePositionsForImportanceJob` `1382-1410` | 0 | 0 | 0 | 0 | 0 | 0 |
| `TryLockImportanceJobBuffers` `1412-1445` | 0 | 0 | 0 | 0 | 0 | 0 |
| `TryLockImportanceJobBuffer` `1447-1454` | 0 | 0 | 0 | 0 | 0 | 0 |
| `ReleaseImportanceJobBufferLocks` `1456-1484` | 0 | 0 | 0 | 0 | 0 | 0 |

The value-type `new` sites are `new ImportanceScoringJob` and `new FoveatedSimulationTelemetryEntry`; both are structs and neither is a managed reference allocation.

## Route Contract Proof

- `TryResolveTick` active first-party source references: `0`.
- `TryAdvanceTick` active first-party source references: `3`.
- Interface declaration: `FoveatedSimulationManager.cs:29`.
- Implementation: `FoveatedSimulationManager.cs:514-547`.
- Dispatcher call site: `SystemDispatcher.cs:5186`.

## Data Sovereignty Proof

Current file counts:

- `private NativeArray<T>` persistent aliases: `0`.
- `TryAcquireWriteLock`: `2`.
- `ReleaseWriteLock`: `2`.
- `TryLockBuffer`: `1` call site, used by `TryLockImportanceJobBuffer`.
- `TryUnlockBuffer`: `7` explicit unlock calls, reverse order of the seven job pins.

Writer lock sites:

- Lines `1125-1127`: telemetry ring writer lock.
- Lines `1154-1157`: telemetry ring release in `finally`.
- Lines `1384-1386`: score-position writer lock.
- Lines `1406-1409`: score-position release in `finally`.

Job pointer pin sites:

- Lines `1425-1431`: requests pins for `73220..73226` before scheduling the importance job.
- Line `1449`: calls `TryLockBuffer` from the per-buffer helper.
- Lines `1440-1443`: partial pin failure releases already-acquired pins in `finally`.
- Lines `1470-1483`: unlocks pinned buffers in reverse order.
- Lines `672-682`: completion path releases pins in `finally`.
- Lines `868-878`: scheduling failure releases pins in `finally`.
- Line `1527`: native-buffer dispose path releases any remaining pins after forced job completion.

## Struct Layout Proof

`FoveatedSimulationTelemetryEntry` is explicit layout, size `64`.

| Field | Offset | Size |
|---|---:|---:|
| Frame | 0 | 4 |
| TargetCount | 4 | 4 |
| FrozenEntityCount | 8 | 4 |
| Tier0Count | 12 | 4 |
| Tier1Count | 16 | 4 |
| Tier2Count | 20 | 4 |
| CameraPosition | 24 | 12 |
| CameraForward | 36 | 12 |
| Flags | 48 | 4 |
| StateHash | 52 | 4 |
| Reserved0 | 56 | 4 |
| Reserved1 | 60 | 4 |

Max field end is `64`; overlap count is `0`; size is a multiple of `8`.

## Scalability And Cinematic Cheat Status

- No physical simulation, rendering trick, or visual system was added.
- Existing continuous route remains: `ResolveScalabilityThresholds()` reads `HomeostasisBrain.GlobalQualityWeight` at lines `1059-1083`.
- Binary low-end token scan in this file: `0`.
- The foveated distance budget still scales continuously through active/frozen distance interpolation, not through `if (isLowEnd)` switches.

Low-tier effect:

- DataVault relocation risk is lower while the importance job owns native pointers.
- No extra allocation path was added.

Middle/high/ultra effect:

- Same foveated cadence/fidelity route.
- No visual overkill was added because this pass was Core memory ownership hygiene, not a presentation feature.

## Compilation Throttle

Build was not launched.

Guard sample before build decision:

- CPU load: `100%`.
- Compiler process count: `2`.
- Active processes: `csc.exe` PID `16180`, `dotnet.exe` PID `31636`.
- Final pre-build guard after route/doc/hash work: CPU `61.5%`, active `dotnet.exe` PID `6088`.
- Build invocations by this pass: `0`.

Reason: project rule forbids build when CPU is above `50%` or compiler/dotnet processes are active. The user also assigned current global compile-wall repair to another agent.

## Residuals

- Status remains `PENDING VERIFICATION`.
- No Unity Editor import, Console check, Play Mode, profiler, GCMonitor, player build, device run, DataVault hot-swap runtime test, or foveated crash dump artifact was produced.
- `Docs/AgentLogs/Dump_FOVEATED_SIMULATION_DIRECTOR.bin` does not exist after this static pass.

## Verdict

The foveated Core native-memory route is now materially better than the previous pass: aliases remain removed, writer fences exist for direct DataVault writes, scheduled job pointers are pinned until completion, and the mutating tick path no longer uses a `TryResolve*` name. Runtime verification is still absent, so the honest status is `PENDING VERIFICATION`.
