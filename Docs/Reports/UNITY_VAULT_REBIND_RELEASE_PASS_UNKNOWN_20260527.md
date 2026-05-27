# Unity Vault Rebind Release Pass - UNKNOWN - 2026-05-27

Status: STATIC SOURCE PROOF ONLY / RUNTIME PENDING

Domain: Core & Memory Infrastructure / SignalBus Contracts / Zero-GC Runtime Architecture.

## Problem

Two mod/core bridge paths still had incomplete DataVault lifecycle handling:

- `FutureCommandSandboxValidator` opened twenty `VaultLane<T>` buffers through `IDataVault.EnsureGenerationHandle<T>()`, but `Shutdown()` and DataVault hot-swap only invalidated local descriptors.
- `ModEventProjectionBridge` opened `ShinobuModProjectionCullTelemetryRing` through DataVault or fallback native storage, but did not rebind this telemetry ring when `GlobalRegistryServiceSlot.DataVault` changed.

This is not a measured frame-time problem. It is an ownership problem: stale aliases and unreleased Vault buffers break the project rule "one fact -> one owner -> one release route".

## Fix

- `FutureCommandSandboxValidator.Shutdown()` now calls `ReleaseVaultHandles(_dataVault)` before clearing cached Vault state.
- `FutureCommandSandboxValidator.RebindDataVault()` now completes the scheduled validation barrier, releases every current lane through the old cached `IDataVault`, then acquires lanes from the new Vault when initialized.
- Added `ReleaseVaultLane<T>()` for all twenty sandbox lanes. The release count matches the declared `VaultLane<T>` field count: `20/20`.
- `ModEventProjectionBridge` now routes cull telemetry through `EnsureCullTelemetryStorage()` and `ReleaseCullTelemetryStorage()`.
- `ModEventProjectionBridge` now handles `GlobalRegistryServiceSlot.DataVault` hot-swap by completing any scheduled projection job, releasing old cull telemetry storage, rebinding the cached Vault, and reopening Vault-backed or fallback telemetry.

## Proof

- `git diff --check` on touched source files: exit `0`; line-ending warnings only.
- Brace balance:
  - `FutureCommandSandboxValidator.cs`: `Delta=0`.
  - `ModEventProjectionBridge.cs`: `Delta=0`.
- Fresh SignalBus audit:
  - Artifact: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_VAULT_REBIND_RELEASE_RECHECK.json`.
  - Result: `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Touched-file audit findings are info-only:
  - `LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW` for `FutureCommandSandboxValidator.MockModQueue`.
  - `COLD_OR_FATAL_SYNC_IO_REVIEW` for sandbox dump methods.
  - `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS` for `ModEventProjectionBridge` cull telemetry.

## Not Claimed

- No profiler proof.
- No Unity Editor import proof.
- No Play Mode proof.
- No full solution build proof. Full compile wall remains intentionally untouched by user instruction.

## Residual Warnings

The fresh static audit still reports `warnings=145`. The warning categories are unchanged:

- `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69`: mostly graphics/UI owned.
- `RUNTIME_SYNC_FILE_IO_REVIEW=65`: representative Core hits inspected; they are setup, persistence, dump, or background writer setup paths, not proven per-frame hot defects in this pass.
- `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`: atmosphere, fluid, habitat, thermodynamics, and world-domain DTO names; not edited in this Core/Mod sandbox pass.

## First-20-Minutes Route Impact

This removes a stability blocker from the mod sandbox and projected mod event bridge. It does not add gameplay truth, visuals, or new global authority. It reduces risk during boot, mod activation, DataVault service replacement, teardown, and crash telemetry collection.
