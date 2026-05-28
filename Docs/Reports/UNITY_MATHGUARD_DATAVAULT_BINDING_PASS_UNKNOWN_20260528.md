# Unity MathGuard DataVault Binding Pass - UNKNOWN - 2026-05-28

Status: PENDING VERIFICATION.

## Scope

- Domain: Core & Memory Infrastructure.
- Files changed: `Assets/_Project/Scripts/Core/MathGuard.cs`, `Assets/_Project/Scripts/Core/GlobalRegistry.cs`.
- First-20-minutes blocker removed: invalid-number telemetry no longer depends on a hidden registry lookup from MathGuard initialization.

## What Was Wrong

- `MathGuard.Initialize()` called `CacheDataVaultCold()`.
- `CacheDataVaultCold()` read `GlobalRegistry.DataVault` inside `MathGuard`.
- `MathGuard` owns Vault handles for the invalid-number ring used by physics/runtime ingress helpers.
- If the authoritative DataVault was replaced, MathGuard had no explicit bind/unbind route from the owner registration path.

## What Was Done

- Added `MathGuard.BindDataVaultCold(IDataVault)`.
- Moved DataVault bind/unbind ownership to `GlobalRegistry.RegisterDataVault()` and `GlobalRegistry.UnregisterDataVault()`.
- Removed `CacheDataVaultCold()` and the `_dataVaultColdCacheAttempted` fallback.
- `MathGuard.Initialize()` now consumes only the bound Vault.
- `MathGuard.Dispose()` releases existing handles through the cached Vault and clears its local binding.

## Rejected Alternatives

- Keeping lazy `GlobalRegistry.DataVault` lookup in MathGuard was rejected because MathGuard is reached from physics/runtime finite guards.
- Adding per-frame registry polling was rejected by the global registry doctrine.
- Editing broad physics call sites was rejected because the defect was in the Core owner route, not in domain consumers.

## Proof

- `rg` scoped to `MathGuard.cs` shows `GlobalRegistry.DataVault` and `CacheDataVaultCold` active references: `0`.
- `GlobalRegistry.RegisterDataVault()` now calls `MathGuard.BindDataVaultCold(instance)`.
- `GlobalRegistry.UnregisterDataVault()` now calls `MathGuard.BindDataVaultCold(null)` when clearing the authoritative Vault.
- Touched source brace delta: `0` for `MathGuard.cs` and `GlobalRegistry.cs`.
- Scoped `git diff --check` exited `0`; line-ending warnings only.

## Residuals

- Full solution build was not run. Latest guard showed CPU `100%`, active `csc.exe` PID `8756`, and active `dotnet.exe` PID `55080`; compile-wall repair belongs to another agent.
- No Unity Editor import, Play Mode, profiler, GCMonitor, player build, device run, or DataVault hot-swap runtime test was performed.
- Runtime microseconds saved claimed: `0`. This is lifecycle/route correctness, not a measured speedup.
