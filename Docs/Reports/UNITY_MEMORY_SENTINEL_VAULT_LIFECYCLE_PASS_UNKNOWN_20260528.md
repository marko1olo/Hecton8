# Unity Memory Sentinel Vault Lifecycle Pass - UNKNOWN - 2026-05-28

## Scope

Domain: Core memory integrity sentinel and DataVault lifecycle.

Evidence class: static source proof only. Full build, full documentation scanners, Unity import, Play Mode, profiler, GC monitor, player build, device runs, and runtime DataVault hot-swap tests were not performed in this pass.

## Problem

`MemorySentinelRuntime` cached `_dataVault` but did not subscribe to `GlobalRegistry` hot-swap.

If the sentinel enabled before Vault registration, or after a Vault replacement, it could keep null or stale Vault state. Its `VisualSyncTick()` and `PublishHashDelta()` also called `EnsureVaultBuffers()`, which can open DataVault buffers through `EnsureGenerationHandle<T>()` from a frame path.

## Change

- Added `IGlobalRegistryHotSwapListener` to `MemorySentinelRuntime`.
- Registered the listener in `OnEnable()`.
- Unregistered the listener in `OnDisable()`.
- Added `RebindVaultDependencyCold(IDataVault)` for DataVault replacement, pending job completion, unlock, release, assignment, and cold buffer open.
- Added `TryResolveVaultBuffers()` as an existing-view-only check.
- Moved `VisualSyncTick()`, `PublishHashDelta()`, and non-forced validation completion to `TryResolveVaultBuffers()`.

## Proof

| Check | Result |
|---|---|
| `MemorySentinelRuntime` implements hot-swap listener | `1` |
| `TryResolveVaultBuffers` active references | `4` |
| `VisualSyncTick()` buffer route | `TryResolveVaultBuffers()` |
| `PublishHashDelta()` buffer route | `TryResolveVaultBuffers()` |
| Touched source brace delta | `0` |
| Scoped `git diff --check` | exit `0`; line-ending warning only |
| CPU/build guard | CPU `100%`; active compiler/dotnet process; build skipped |

## Architecture Verdict

This was worth doing. It removes a stale-Vault risk and prevents the normal visual-sync frame path from being the first owner of sentinel DataVault storage.

Runtime microseconds saved: `0`. No profiler/player proof was collected.

## Scaling Behavior

- Low: first usable sentinel frame consumes already-open bounded Vault buffers or fails closed.
- Middle: normal cold enable/hot-swap route owns memory initialization.
- High: validation cadence, rollback capacity, and telemetry capacity remain unchanged.
- Ultra: no change to visual-overkill policy; this is lifecycle correctness.

## Residuals

- Manual editor/tuner/dump routes may still call `EnsureVaultBuffers()` because they are explicit owner or diagnostic operations.
- Full solution build was not launched because CPU was `100%` with active compiler/dotnet work.
- Full documentation scanners were not launched under load.
- No Unity Editor import, Play Mode, profiler, GC monitor, player build, device run, or runtime DataVault hot-swap test was performed.
