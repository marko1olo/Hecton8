# X_001 Canonical Signal Lane Contracts

Date: 2026-05-24
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor

## Problem

The registered-dispatch pass removed several duplicate Core lifecycle prewarms. That exposed a real race shape: a first producer could call `SignalBus<T>.EnsureInitialized()` or `TryPush()` before the intended domain owner called `Configure(...)`. For high-fanout lanes this allowed the native queue and snapshot buffer to come up with generic defaults, or with a local domain hash that was not the lane contract hash.

Affected paths found in this pass:

- Reactor bridge opened `BaseModuleCompromisedSignal` through `EnsureInitialized()` before the habitat deformation runtime's configure path.
- Battery charger configured global `AcousticPingSignal` with the charger hum source hash as the lane hash.
- Seaglide configured global `ToolAcousticSignal` and `BubbleSpawnSignal` from local constants.
- Manta configured global `SubmarineLightsChangedSignal` from a local hash.
- Gyro compass configured `AnomalyProximitySignal` from a compass-local lane hash.
- Physiology/metabolism and damage producers had multiple `EnsureInitialized()` paths that could beat Core lifecycle configuration for storm lanes.

## Change

Added canonical capacity/hash constants directly to the unmanaged DTO contracts and made `SignalBus<T>` apply known contracts before first native initialization. `Configure(...)` now resolves these known contracts back to the canonical tuple, so later local config calls cannot drift lane hash/capacity for the selected cross-domain storm and respawn/inventory lanes.

Canonical contract table:

| Signal | Expected | Max/frame | Low-tier | Lane hash | Overflow behavior |
| --- | ---: | ---: | ---: | ---: | --- |
| `CombatDamageSignal` | 256 | 128 | 16 | `3474161304` | coalesces by target/damage/channel, then bounded drop |
| `ImpactSignal` | 256 | 256 | 64 | `1490821407` | coalesces by AUP impact grid, then bounded drop |
| `HighSpeedImpactSignal` | 128 | 128 | 32 | `2004661978` | coalesces by high-speed impact grid, then bounded drop |
| `AcousticPingSignal` | 128 | 128 | 16 | `2525108346` | coalesces by AUP/channel grid, then bounded drop |
| `ToolAcousticSignal` | 128 | 128 | 32 | `1213288304` | bounded drop |
| `BubbleSpawnSignal` | 64 | 64 | 16 | `512036682` | non-critical VFX shedding + bounded drop |
| `SubmarineLightsChangedSignal` | 64 | 64 | 16 | `887228434` | bounded drop |
| `AnomalyProximitySignal` | 16 | 16 | 4 | `3986232183` | bounded drop |
| `BaseModuleCompromisedSignal` | 64 | 64 | 16 | `3041159082` | bounded drop |
| `HullDeformedSignal` | 64 | 64 | 16 | `4279913826` | non-critical VFX shedding + bounded drop |
| `HullRepairedSignal` | 64 | 64 | 16 | `2577695098` | bounded drop |
| `PhysiologyStateSignal` | 64 | 64 | 32 | `0x50485953` | bounded drop |
| `ReactorDamageSignal` | 64 | 64 | 64 | `0x52474153` | bounded drop |
| `PlayerRespawnSignal` | 16 | 16 | 16 | `0x5253504E` | bounded drop |
| `InventoryRespawnDeathAupSignal` | 16 | 16 | 16 | `0x49524441` | native job writer + bounded drop |
| `InventoryRespawnPenaltyResultSignal` | 16 | 16 | 16 | `0x49525052` | bounded drop |
| `InventoryDeathLootCacheSignal` | 64 | 64 | 64 | `0x49444C43` | bounded drop |

## Verification

- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits outside `Core/Signals`, Editor, and Tests: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` hits outside ModdingAPI, Editor, and Tests: 0.
- External `SignalBus<T>.Push` wrappers for the canonical storm/cross-domain lane set outside `Core/Signals`, Editor, and Tests: 0; producers use `TryPush` or `ParallelWriter` so drop semantics are explicit.
- Direct `TryPush` now rejects at `_expectedCapacity` before enqueue, preventing single-thread producer storms from growing a lane beyond its prewarmed queue budget.
- External selected-lane `ParallelWriter` compatibility opens remain: 10. These are job producer lanes (`CombatDamageSignal`, `ImpactSignal`, `BaseModuleCompromisedSignal`, `PlayerRespawnSignal`, `InventoryRespawnDeathAupSignal`), not managed events. They still flush through the registered native lane path with max-frame caps and `LaneOverflowFaultThreshold=1024`; they do not allocate managed heap or touch `GlobalSignals` queues.
- Managed/string/native-container field scan over Core signal DTO files and Core contract signals: 0.
- Brace balance on touched runtime files: 0 delta.
- `git diff --check` on touched files: no whitespace errors; only existing LF-to-CRLF warnings.
- Build not launched: CPU guard reported 56.7 percent, above the 50 percent build threshold.
