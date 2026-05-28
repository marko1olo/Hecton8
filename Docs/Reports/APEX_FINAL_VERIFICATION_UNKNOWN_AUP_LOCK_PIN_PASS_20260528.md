# APEX Final Verification UNKNOWN AUP Lock/Pin Pass 2026-05-28

Status: `PENDING_RUNTIME_VERIFICATION`.

## What Changed

- `Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs`
  - Added owner-buffer writer locks through `TryAcquireWriteView`.
  - Added scheduled rebase pins through `TryLockScheduledBuffer`.
  - Added `ReleaseScheduledRebaseLocks` for all async rebase pins.
  - Reopened pinned scheduled buffers after pin acquisition before passing them to jobs.
- `Assets/_Project/Scripts/HectonFloatingOrigin.cs`
  - Releases AUP scheduled pins after `AwaitTransformShiftJobAsync` completes.
  - Releases the same pins again in `finally` before `UnlockAllocationsAfterAupShift` as a fault-path guard.

## Buffer Evidence

- `MockStatesBuffer=73030`: scheduled pin.
- `MockVelocitiesBuffer=73031`: writer-lock during owner initialization.
- `MockHistoricalPointsBuffer=73032`: writer-lock for synchronous time slice, scheduled pin for async job.
- `TelemetryRingBuffer=73033`: writer-lock for frame and completion telemetry.
- `RuntimeStateBuffer=73034`: writer-lock for runtime/tuner/CSV/time-slice state.
- `MockCameraBuffer=73035`: writer-lock for pre-simulation camera AUP state.
- `CsvScratchBuffer=73036`: writer-lock during editor CSV read into scratch.
- `CounterBuffer=73037`: writer-lock for reset/direct slice, scheduled pin for async counter job.

Cross-domain AUP shift buffers are pinned, not writer-locked: `VaultHotEntityData`, `TetherCablePositions`, `TetherCablePreviousPositions`, `TetherVisualSegmentPositions`, `TetherVisualAnchorPositions`.

## Static Verification

- Added-line scan: `added_lines=820`, reference `new=0`, `new` token occurrences `8`, `string.Format=0`, `.ToString()=0`, LINQ tokens `0`, `foreach=0`, `.Complete()=0`, added `EnsureGenerationHandle=0`, added `GlobalRegistry=0`.
- Value-type/ref-struct `new` tokens only: `AupMockInitializeJob`, `double3`, `OriginShiftSignalDTO`, `AupStateRebaseJob`, `float3`, `VaultHotEntityRebaseJob`, `ReadOnlySpan<byte>`.
- Brace counts: `AupOriginShiftCoordinator.cs 173/173`, `HectonFloatingOrigin.cs 230/230`.
- Scoped `git diff --check`: exit `0`; LF/CRLF warnings only.

## Compilation Boundary

Pre-build guard was green: CPU `31.2%`, no active `dotnet`, `csc`, or `VBCSCompiler`.

Command attempted:

```text
dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false
```

Result: no green compile proof. The process timed out at `904s`; the owned lingering `dotnet.exe` PID `31496` was stopped. Log: `Docs/Reports/BUILD_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.log`, SHA-256 `CC2A637194272D74A26D4BD294F30B28D15BAAF65774FDE4ADCFA630BBBE8289`.

The captured compiler wall is outside this AUP pass:

- `Assets/_Project/Scripts/ModularEquipmentEngine.cs`: `CS8168` / `CS8350` around `acquiredCount`.
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`: missing `TryAcquireSargassumWriteLock`, `ReleaseSargassumWriteLock`, `TryReadOnlySargassumVaultArray`, and one unassigned local.
- Pre-final recheck: CPU `80%`; active external `dotnet.exe` PID `62104` running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. It was not stopped because it did not match this pass command and may belong to another agent.

## Hashes

- `AupOriginShiftCoordinator.cs`: `5F11D259FF25C1469463034608EEFD2CC783A9B3F9D32A0E5C17FA3ED52EBF03`.
- `HectonFloatingOrigin.cs`: `061B6D3820FF35AA0CC4F51068F97ACA7EAE555309D217354787D57E0AC67EC6`.
- JSON report: `E10FCFC8F1A3824A92380982B7C182854DD1FF203D7D94314127D7EC1C7BDD4A`.

## Residuals

- Unity Editor import, Console, Play Mode, Profiler/GCMonitor, player build, and device proof were not available in this session.
- No crash/NaN runtime execution occurred, so no `Docs/AgentLogs/Dump_UNKNOWN.*` artifact was produced.
- Full project compile is currently blocked by unrelated active files; per user instruction, this pass did not repair global compile errors.
