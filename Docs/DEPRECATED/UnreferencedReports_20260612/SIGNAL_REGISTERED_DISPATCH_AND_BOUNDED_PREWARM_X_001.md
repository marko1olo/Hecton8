# SIGNAL REGISTERED DISPATCH AND BOUNDED PREWARM - X_001

Date: 2026-05-24
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor

## Problem

`SignalBusRuntime` still had a central concrete DTO list for direct flush and post-simulation clear. That list was removed. The second problem was storm capacity: `SignalBus<T>.Configure(expectedCapacity, laneHash: ...)` previously inherited `maxFrameSignals=10000` and `lowTierFrameSignals=1000`, so a legacy lane without explicit caps could absorb a 5000-event burst instead of shedding/coalescing at the declared lane capacity.

## Changes

- Replaced `SignalBusRegistry` fallback/concrete dispatch with a registered closed-generic `SignalLaneDispatch[]`.
- Removed the hardcoded concrete DTO dispatch machinery: `FlushDirectSignalLanes`, `FlushDirectSignalLane<T>`, `ClearDirectSignalLaneSnapshots`, and `SignalLanePolicyCache<T>.DirectRegistryDispatch`.
- Kept a compatibility diagnostic alias `FallbackLaneCount => DispatchLaneCount`; it no longer represents a fallback route.
- Changed implicit `SignalBus<T>.Configure` defaults:
  - `maxFrameSignals <= 0` resolves to `expectedCapacity`.
  - `lowTierFrameSignals <= 0` resolves to `expectedCapacity / 4`, clamped to at least 1.
- Changed `RegisterLegacyLane<T>` to pass explicit max-frame and low-tier caps instead of relying on optional defaults.
- Removed central legacy prewarm for eight lanes that already have domain-local configure/prewarm owners:
  - `AcousticPingSignal`
  - `FluidDensityChangedSignal`
  - `FluidIncursionSignal`
  - `PhysiologyStateSignal`
  - `ProgressionEventSignal`
  - `SeismicSignal`
  - `SubmarineLightsChangedSignal`
  - `ToolAcousticSignal`
- Removed dead central legacy prewarm for six lanes with no runtime source use outside generated hashes:
  - `DataReloadSignal`
  - `ItemDecaySignal`
  - `ReconDataSignal`
  - `SolarFlareSignal`
  - `SpectrumScanSignal`
  - `WeatherStrengthSignal`
- Removed 16 duplicate Core lifecycle configure/prewarm pairs whose lanes already have outside-Core configure/prewarm owners:
  - `AcousticPingSignal`
  - `AnomalyProximitySignal`
  - `BaseModuleCompromisedSignal`
  - `BaseStructuralWarningSignal`
  - `BubbleSpawnSignal`
  - `CameraJuiceImpactSignal`
  - `CompassCalibratedSignal`
  - `DynamicMusicScalarSignal`
  - `InventoryRespawnDeathAupSignal`
  - `InventoryRespawnPenaltyResultSignal`
  - `PhysiologyStateSignal`
  - `PlayerRespawnSignal`
  - `ScalabilityChangedEvent`
  - `SeismicSignal`
  - `SubmarineLightsChangedSignal`
  - `ToolAcousticSignal`
- Reordered `Shinobu19EconomyLedger.WarmSignalLanes()` so every local configure is immediately followed by `EnsureInitialized()`.

## Capacity And Overflow Proof

For a lane configured through `RegisterLegacyLane<T>(capacity, label)`, effective caps are now:

- Expected native prewarm capacity: `capacity`.
- Max frame snapshot cap: `capacity`.
- Low-tier frame cap: `max(1, capacity / 4)`.
- Overflow path: existing `SignalBus<T>` drop/coalescing path; no managed queue or heap-backed event list is introduced.

For a 5000-signal burst on an implicit legacy lane:

- The lane no longer keeps a 10000-frame implicit budget.
- Frame processing is bounded by the lane capacity unless the domain explicitly requests a larger `maxFrameSignals`.
- Storm/coalescing specialization remains in `SignalBus<T>` for high-volume lanes already handled in the prior pass.

## Static Verification

- `DirectRegistryDispatch`, `ResolveDirectRegistryDispatch`, `FlushDirectSignalLane`, `ClearDirectSignalLane`, `_fallbackDispatch`, `_fallbackLaneCount`, and `directDispatch`: 0 hits in `Core/Signals`.
- `SignalBusRegistry.Register(...)`: 1 runtime call site, in `SignalBus<T>.EnsureRegistered`.
- Runtime `SignalBus<T>.Configure/ConfigureCacheLineCritical` references: 241.
- `RegisterLegacyLane<T>` registrations: 59, down from 74.
- Core lifecycle configure references: 131.
- Core lifecycle configure overlap with outside-Core configured lanes: 0.
- Central legacy prewarm overlap with outside-Core configured lanes: 0.
- Immediate `EnsureInitialized` gaps after runtime `SignalBus<T>.Configure`: 0.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0.
- Targeted Core payload/route managed-field scan: 0 hits.
- Touched-file brace deltas: 0.

## Build Status

Build not launched. Guarded check reported CPU 96 percent with active `csc` PID 11192 and `dotnet` PID 21360, which violates the project build guard.
