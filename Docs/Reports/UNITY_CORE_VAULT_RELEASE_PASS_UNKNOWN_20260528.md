# Unity Core Vault Release Pass - UNKNOWN - 2026-05-28

Status: STATIC SOURCE PROOF ONLY / RUNTIME PENDING

Domain: Core & Memory Infrastructure / SignalBus Contracts / Zero-GC Runtime Architecture.

## Problem

Core DataVault users still had three proven ownership defects:

- `StaticDataStore` and `BabelDictionaryStore` shared StaticData/BTree telemetry buffer IDs even though `GlobalDataVault.EnsureGenerationHandle<T>()` does not create a per-consumer lease for an existing buffer. Releasing one logical owner can invalidate the other owner.
- `StaticDataStore`, `BabelDictionaryStore`, `SignalTuningTable`, `SignalTelemetryRingBuffer`, `SignalThreadLocalScratchpad`, and `H8MacroDatabaseService` cleared `VaultGenerationHandle<T>` fields without consistently calling `IDataVault.ReleaseBuffer()`.
- `BabelDictionaryStore` had failure branches after `EnsureGenerationHandle<byte>()` where the mapped dictionary/error slice handles were reset without a release route.

This is an ownership and teardown correctness problem. It is not a measured frame-time optimization.

## Fix

- Added Babel-specific DataVault buffer IDs for Babel telemetry cursor and BTree telemetry ring/cursor/accumulator.
- Moved `BabelDictionaryStore` telemetry and BTree telemetry off StaticData/BTree shared IDs.
- Added release helpers for StaticData, Babel, SignalWarden, and MacroDatabase Vault handles.
- `GlobalSignals.DisposeAllQueues()` now releases Signal tuning, telemetry ring, and scratchpad Vault handles.
- `H8MacroDatabaseService.Shutdown()` now releases scratch, blackbox, dirty payload, payload-copy, and sector-coordinate Vault handles before clearing cached Vault state.
- Renamed MacroDatabase cold file helpers to `TryCreateEmptyFileCold()` and `CleanupCompactionTempCold()` so static IO review reflects cold/persistence phase.

## Source Files Changed

- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Core/Data/StaticDataStore.cs`
- `Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs`
- `Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs`
- `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs`
- `Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs`

## Proof

- `git diff --check` on the six touched source files: exit `0`; line-ending warnings only.
- Brace balance is `0` for all six touched source files.
- StaticData/Babel `EnsureGenerationHandle<T>()` buffer scan now has no duplicate buffer IDs between the two stores.
- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_VAULT_RELEASE_COLD_RECHECK.json`:
  - `files=2443`
  - `shaders=71`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=73`
  - `infos=1020`
- Touched-file non-info audit findings: none.
- Documentation gates after this report:
  - `VerifyDocStructure.py`: `pass=true`, `activeDocCount=703`, `encodingWithoutUtf8Sig=0`
  - `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=703`, `sourceSyncPass=true`

## Residuals

- Full `Hecton8.slnx` build was not run in this pass by user instruction; another agent owns overall compile errors.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof was produced.
- Remaining audit warnings are outside the touched files:
  - `RUNTIME_SYNC_FILE_IO_REVIEW=62`
  - `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`
  - `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=3`

## Hardware Impact

Runtime microseconds saved claimed: `0`. No profiler/player proof.

Low-tier impact is reduced stale native alias and teardown leak risk in core DataVault paths. Middle, high, and ultra tiers keep the same telemetry fidelity and database/static-data behavior without changing gameplay truth ownership.
