# Unity Global Telemetry DataVault Binding Pass - UNKNOWN - 2026-05-28

## Scope

Domain: Core global telemetry blackbox and DataVault registration route.

Evidence class: static source proof only. Full build, full documentation scanners, Unity import, Play Mode, profiler, GC monitor, player build, device runs, and dump parser runs were not performed in this pass.

## Problem

`GlobalTelemetryBus.TryBindBlackboxVaultBuffersNoLock()` read `GlobalRegistry.DataVault`.

That kept a hidden global registry lookup in the SHINOBU blackbox storage bind path. The helper is mutating by name and behavior, but the project route doctrine still requires global owners to receive dependencies through explicit cold bind or owner initialization, not poll the registry internally during first use.

## Change

- Added `_blackboxBoundVault`.
- Added `BindBlackboxDataVaultCold(IDataVault)`.
- Bound the blackbox Vault from `GlobalRegistry.RegisterDataVault()`.
- Cleared the blackbox Vault from `GlobalRegistry.UnregisterDataVault()` when the authoritative Vault is removed.
- Changed `TryBindBlackboxVaultBuffersNoLock()` to use `_blackboxBoundVault`.
- Cleared `_blackboxBoundVault` during full blackbox state disposal.

## Proof

| Check | Result |
|---|---|
| Direct `GlobalRegistry.DataVault` reads in `GlobalTelemetryBus.cs` and `GlobalTelemetryBus.Blackbox.cs` | `0` |
| `BindBlackboxDataVaultCold` route | method plus `RegisterDataVault` and `UnregisterDataVault` call sites |
| `TryBindBlackboxVaultBuffersNoLock()` Vault source | `_blackboxBoundVault` |
| `GlobalTelemetryBus.Blackbox` brace delta | `0` |
| `GlobalRegistry` brace delta | `0` |
| Scoped `git diff --check` | exit `0`; line-ending warnings only |
| CPU/build guard | CPU `100%`; active `dotnet.exe` process, latest observed PID `55080`; build skipped |

## Architecture Verdict

This was worth doing. It removes a remaining hidden registry lookup from global telemetry storage binding without changing blackbox payload layout, BufferIDs, capacity, watchdog behavior, MMF behavior, or dump format.

Runtime microseconds saved: `0`. No profiler/player proof was collected.

## Scaling Behavior

- Low: if boot has not supplied a Vault, blackbox Vault binding fails closed instead of polling global state.
- Middle: normal bootstrap binds the Vault once through `RegisterDataVault()`.
- High: SHINOBU blackbox capacity and event route remain unchanged after explicit binding.
- Ultra: no change to visual-overkill policy; this is dependency ownership, not quality scaling.

## Residuals

- Full solution build was not launched because CPU was `100%` and an active `dotnet.exe` process was observed.
- Full documentation scanners were not launched under load.
- There are still other direct `GlobalRegistry.DataVault` reads in Core/Bootstrap; inspected examples are explicit init/editor/cold routes or separate domain candidates, not part of this narrow blackbox fix.
