# Signal Global Helper Route Demonolithization - X_001

Date: 2026-05-24

## Scope

This pass removed remaining external runtime dependency on `GlobalSignals` helper/bootstrap names after the hot publish/consume path was already cut.

## Code Changes

- Added `RuntimeOriginRoute` as the pure owner for runtime-origin AUP conversion and stable entity-id folding.
- `GlobalSignals.CurrentRuntimeOriginAup`, `GlobalSignals.TryRuntimePositionToAup`, and `GlobalSignals.FoldEntityIdToSourceId` now delegate to `RuntimeOriginRoute` for compatibility only.
- Bulk-rerouted runtime source callers from `GlobalSignals.CurrentRuntimeOriginAup`, `GlobalSignals.TryRuntimePositionToAup`, and `GlobalSignals.FoldEntityIdToSourceId` to `RuntimeOriginRoute`.
- Added `SignalCorridorRuntime` as the lifecycle/phase facade for signal corridor initialization, shutdown, flush, clear, debug-lane prewarm, and haptic-lane prewarm.
- Replaced external `GlobalSignals.InitializeAllQueues`, `DisposeAllQueues`, `FlushPreSimulation`, `ClearPostSimulationSnapshots`, `EnsureDebugSignalLaneInitialized`, and `EnsureHapticPulseSignalLaneInitialized` calls with `SignalCorridorRuntime`.
- Removed direct `GlobalSignals` initialization reads from `SessionLifecycleSignalRoute` and `ProgressionMetaSignalRoute`.
- Replaced `SignalBusRuntime` direct pause read with `SimulationSignalRoute.SimulationPaused`.

## Static Proof

- External runtime `GlobalSignals.` references outside `Assets/_Project/Scripts/Core/Signals`: 0.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` references outside Editor/Tests: 0.
- Runtime helper references `GlobalSignals.CurrentRuntimeOriginAup/TryRuntimePositionToAup/FoldEntityIdToSourceId`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- `RuntimeOriginRoute` runtime call sites: 244 across 190 files.
- `SignalCorridorRuntime` runtime call sites: 20 across 18 files.
- Core signal DTO/route banned field scan for `GameObject`, `Transform`, `string`, `FixedString*`, and native containers: 0.
- `git diff --check` on the new/changed route files reports only LF-to-CRLF normalization warnings.

## Capacity And Overflow Position

The per-lane source-of-truth remains `Docs/Reports/SIGNAL_LANE_POST_SPLIT_STATIC_CAPACITY_X_001.md`: 251 direct `SignalBus<T>.Configure/ConfigureCacheLineCritical` sites, 73 legacy typed prewarm registrations, 273 configured/prewarmed unique typed lanes, and 512 dispatch slots.

Storm behavior is bounded at native lane level: fixed capacities, low-tier frame caps, latest-cache snapshots where configured, deterministic clear/drop, and specific coalescing routes for known flood lanes such as damage/acoustic. No managed queue growth is introduced by `RuntimeOriginRoute` or `SignalCorridorRuntime`.

## Build Status

No build was launched in this pass. Guard check after a 30-second wait showed CPU at 96 percent; `AGENTS.md` forbids `dotnet build` above 50 percent CPU or while compiler work is active.
