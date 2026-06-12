# X_001 Death And Discovery Typed Route Pass

Date: 2026-05-24

## What Was Wrong

- `HectonSurvivalSystem.OnDeath` still exposed a managed first-party death callback. `PDALogbookManager` was the remaining runtime subscriber.
- `HectonDiscoveryManager.OnBiomeDiscovered` still exposed a managed multicast route into difficulty, profile, achievements, and PDA logbook.
- These were not `GlobalSignals.Publish` defects, but they were hidden managed first-party signal paths beside the typed bus corridor.

## What Changed

- Survival death is now published once through the existing `SurvivalVitalsChangedSignal` lane by `HectonSurvivalSystem`.
- `DynamicDifficultyDirector`, `GlobalProfileManager`, `RunModifierController`, `PDADeathMemoryDump`, and `PDALogbookManager` consume death through `SurvivalSignalRoute.TryGetLatestDeath`.
- `HectonSurvivalSystem.OnDeath` was removed.
- Biome discovery now publishes `ProgressionMetaSignal.KindBiomeDiscovered` through `ProgressionMetaSignalRoute.PublishBiomeDiscovered`.
- `DynamicDifficultyDirector`, `GlobalProfileManager`, `PlayerAchievementRegistry`, and `PDALogbookManager` consume biome discovery from `SignalBus<ProgressionMetaSignal>`.
- `HectonDiscoveryManager.OnBiomeDiscovered` was removed.

## Lane Capacity And Overflow

- `SurvivalVitalsChangedSignal`: 32-byte DTO, configured expected capacity 64. Death consumers read latest death state through the route cache. A 5000-event flood is hard-cleared by `SignalBus<T>` when queue depth exceeds `LaneOverflowFaultThreshold = 1024`; this is deterministic shedding with telemetry, not heap growth.
- `ProgressionMetaSignal`: 32-byte DTO, configured capacity 64, max frame 64, low-tier frame cap 16. Over frame limit, oldest queued entries are dropped before snapshot copy. Above 1024 queued entries, the lane is cleared and telemetry is emitted.
- `ItemLifecycleSignal`: 64-byte DTO, configured capacity 128, max frame 128, low-tier frame cap 32. Over frame limit, oldest queued entries are dropped. Above 1024 queued entries, the lane is cleared.

## Static Proof

- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests: 0 hits.
- First-party retired managed item/progression/death/biome event scan outside `ModdingAPI`: 0 hits.
- Core signal DTO banned field scan for `GameObject`, `Transform`, `string`, `FixedString*`, and native containers: 0 hits.
- Death/discovery touched file brace balance: 0 delta.
- Build not claimed for this pass: guard check reported active `dotnet` and `VBCSCompiler` processes, so another build would violate the project rule.
