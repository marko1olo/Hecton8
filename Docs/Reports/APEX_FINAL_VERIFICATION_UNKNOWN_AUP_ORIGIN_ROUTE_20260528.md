# APEX Final Verification - UNKNOWN - AUP Origin Route

Date: 2026-05-28
Status: PENDING_RUNTIME_VERIFICATION
Evidence class: STATIC_SOURCE

## What Changed

- `AupOriginShiftCoordinator` no longer has active `TryResolveOrAcquire` or `EnsureRuntimeState` names in the touched AUP files.
- Owner/bootstrap mutation is explicit: `OpenOrAcquireRuntimeStateForOwnerRoute`.
- Frame and shift phases use `TryResolveRuntimeState`, which resolves existing handles only and fails closed if owner prewarm did not run.
- `HectonFloatingOrigin.ShiftWorldAsync` now explicitly opens/acquires AUP runtime state before `LockAllocationsForAupShift`.

## Proof

- `AupOriginShiftCoordinator.cs` SHA-256: `4FE8D7649224590D900F97E2C79B53E24FEC7831C30E750920C3B39316FAF04D`.
- `HectonFloatingOrigin.cs` SHA-256: `86C8B2CC51620AED9DFF22C522A168BD4A643F42053ABAD00D649F198A2E105F`.
- Old-name scan: `TryResolveOrAcquire|EnsureRuntimeState` exit `1`.
- Scoped `git diff --check`: exit `0`.
- Brace counts: `AupOriginShiftCoordinator.cs` `134/134`; `HectonFloatingOrigin.cs` `229/229`.
- Added-line scan: reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ calls `0`, `foreach=0`, `.Complete()=0`, `GlobalRegistry.DataVault=0`, `EnsureGenerationHandle=0`.

## Data Sovereignty

BufferIDs under this route:

- `MockStatesBuffer = 73030`
- `MockVelocitiesBuffer = 73031`
- `MockHistoricalPointsBuffer = 73032`
- `TelemetryRingBuffer = 73033`
- `RuntimeStateBuffer = 73034`
- `MockCameraBuffer = 73035`
- `CsvScratchBuffer = 73036`
- `CounterBuffer = 73037`

No new field migration was performed. This pass did not add `TryAcquireWriteLock`; AUP still needs a separate writer-lock/job-pin pass because scheduled rebase jobs and async completion must not release fences before `JobHandle` completion.

## Runtime Status

Build was not launched. CPU guard was `87.5%` with active `dotnet.exe` PIDs `37700` and `67192`.

Unity import, Console, Play Mode, profiler, GCMonitor, player build, device run, and dump artifact are absent. Runtime status remains `PENDING_RUNTIME_VERIFICATION`.
