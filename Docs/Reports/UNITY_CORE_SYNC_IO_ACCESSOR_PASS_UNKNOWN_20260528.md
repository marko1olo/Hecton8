# Unity Core Sync IO And Accessor Pass - UNKNOWN - 2026-05-28

Status: STATIC SOURCE PROOF ONLY / RUNTIME PENDING

Domain: Core & Memory Infrastructure / SignalBus Contracts / Zero-GC Runtime Architecture.

## Problem

The remaining Core warnings after the Vault release pass were not all equal. Four files had actionable Core issues:

- `LockstepStateValidator.StageReplayWrite()` could reach replay writer setup from the post-fixed simulation route. That setup opens a `FileStream` and starts a writer thread.
- `InputDispatcher.EnsureInputReplayWriter()` and `RebindingManager.TryDeleteOverridesFile()` were cold lifecycle/user-commit routes, but their names did not expose the cold IO boundary to the static contract audit.
- `HectonPersistentPathPolicy.EnsureParentDirectory()` is explicit IO, but the public name did not make the cold/persistence boundary clear.
- `LockstepStateValidator.GetVaultBuffer<T>()` opened/acquired DataVault buffers through `EnsureGenerationHandle<T>()`; the behavior was valid in owner phases, but the `Get*` name violated read-accessor purity doctrine.

## Fix

- `LockstepStateValidator` now initializes the replay writer in `OnEnable()` through `EnsureReplayWriterCold()`.
- `StageReplayWrite()` no longer opens or creates the replay file. It only uses an already-open writer or skips replay writing.
- Renamed cold IO helpers:
  - `EnsureInputReplayWriterCold()`
  - `DeleteOverridesFileIfExistsCold()`
  - `TryDeleteOverridesFileCold()`
  - `EnsureParentDirectoryCold()`
- Kept `HectonPersistentPathPolicy.EnsureParentDirectory()` as a compatibility wrapper that delegates to the cold-named method.
- Renamed `LockstepStateValidator.GetVaultBuffer<T>()` to `OpenOrAcquireVaultBufferView<T>()` so the method name matches DataVault acquisition semantics.

## Source Files Changed

- `Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs`
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`
- `Assets/_Project/Scripts/Core/RebindingManager.cs`
- `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs`

## Proof

- `git diff --check` on the four touched source files: exit `0`; line-ending warnings only.
- Brace balance is `0` for all four touched source files.
- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_SYNC_IO_ACCESSOR_RECHECK.json`:
  - `files=2443`
  - `shaders=71`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=68`
  - `infos=1025`
- Touched-file non-info audit findings: none.
- Core subtree non-info audit findings: none.
- Documentation gates after this report:
  - `VerifyDocStructure.py`: `pass=true`, `activeDocCount=704`, `encodingWithoutUtf8Sig=0`
  - `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=704`, `sourceSyncPass=true`

## Residuals

- Full `Hecton8.slnx` build was not run by this agent; overall compile errors remain owned by another agent per user instruction.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof was produced.
- Remaining non-info audit warnings are outside the Core touched surface:
  - `RUNTIME_SYNC_FILE_IO_REVIEW=57`
  - `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`
  - `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=3`

## Hardware Impact

Runtime microseconds saved claimed: `0`. No profiler/player proof.

Low-tier impact is removal of a possible first-replay file setup hitch from post-fixed simulation. Middle, high, and ultra tiers keep deterministic replay behavior when the cold writer is available.
