# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SIGNAL MANAGED EVENT HOTPATH AUDIT X_001

Generated: 2026-05-23 23:28:00 +04:00
Evidence class: STATIC SOURCE ONLY. No Unity Play Mode, Profiler, GCMonitor, or player build proof is implied.

## Summary

- Runtime external `GlobalSignals.Publish/Push/TryDequeue/*Writer` hits in audited signal domains: 0.
- Central `GlobalSignals` compatibility hot API declarations now compile-fail for new callers: 119 `Publish`, 3 `Push`, 84 `TryDequeue*`, 34 writer properties.
- Unused gameplay managed events removed from `HectonPlayerHealth`: 5 declarations and 9 invoke sites.
- Unused survival managed events removed from `HectonSurvivalSystem`: 16 declarations and 17 invoke sites; `OnDeath` is retained because it has a live PDA logbook subscriber.
- Remaining C# event declarations in selected signal-heavy domain folders plus `HectonSurvivalSystem` after removal: 3.
- Non-modding `HectonEventBus.Publish/Subscribe` hits outside Editor/Tests/ModdingAPI: 29. These are managed/cold API surfaces, not typed hot `SignalBus<T>` lanes.

## Removed Managed Gameplay Events

`Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs` had five public events with zero runtime subscribers in source:

- `OnHealthChanged`
- `OnDeath`
- `OnDamageTaken`
- `OnHealed`
- `OnMutationFlagsChanged`

Action taken: removed the declarations and 9 invoke sites from health damage/heal/death/mutation paths. The remaining owner state sync stays on `MarkCombatDamageSyncDirty()`, `SignalBus<PhysiologyStateSignal>` reads, and `SignalBus<VitalWarningSignal>.TryPush`.

## Removed Survival Managed Events

`Assets/_Project/Scripts/HectonSurvivalSystem.cs` already publishes `SurvivalVitalsChangedSignal` through `SurvivalSignalRoute.QueueVitals`. Static source showed no runtime subscribers for 13 public vitals/critical events and 3 internal injury/thermal/bleed events.

Removed declarations:

- `OnOxygenChanged`, `OnEnergyChanged`, `OnDepthChanged`, `OnIntegrityChanged`, `OnPressureChanged`, `OnWeightChanged`
- `OnOxygenCritical`, `OnTemperatureChanged`, `OnRadiationChanged`
- `OnHungerChanged`, `OnThirstChanged`, `OnHungerCritical`, `OnThirstCritical`
- `InjuryStateChanged`, `ThermalStateChanged`, `BleedingTrailPulse`

Retained: `OnDeath`, because `PDA/PDALogbookManager.cs` subscribes to it.

## Remaining C# Event Declarations In Selected Domains

- `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:37` - `ActiveTransportLifecycleChanged`; used by audio and trauma systems as a low-frequency lifecycle owner change, not a storm signal lane.
- `Assets/_Project/Scripts/UI/SubtitleManager.cs:265` - `OnCueChanged`; UI subtitle presentation callback, not gameplay truth ownership.
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs:440` - `OnDeath`; live PDA logbook subscriber retained until a death route-card migrates the managed consumer to `SurvivalSignalRoute`.

These were not converted in X_001 because they are not hidden `GlobalSignals` routes and have live consumers or UI-only presentation ownership.

## Non-Modding HectonEventBus Hits

The managed `HectonEventBus` is still present outside `ModdingAPI`. This is cold/meta/UI/progression/event API debt, not a typed hot signal corridor. It must not be used for reactor, hull deformation, airlock animation, collision, or damage storm lanes.

- `Assets/_Project/Scripts/Economy/ScrapManager.cs:132` - `Publish(ItemRecycledEvent)`.
- `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs:343` - `Publish(ItemRecycledEvent)`.
- `Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:290` - `Publish(ItemCollectedEvent)`.
- `Assets/_Project/Scripts/PlayerInventory.cs:1097` - `Publish(ItemCollectedEvent payload)`.
- `Assets/_Project/Scripts/PlayerInventory.cs:1103` - `Publish(ItemDiscardedEvent)`.
- `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:590` - `Publish(AchievementUnlockedEvent)`.
- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:227` - `Publish(PlayerAdvisoryIssuedEvent)`.
- 22 subscribers in `Meta`, `PDA`, `Progression`, `World.EnvironmentalStrainManager`, and `UI` listen to game-loaded/player-spawned/player-died/item/advisory/profile events.

Rejected action: mass-converting `HectonEventBus` payloads to `SignalBus<T>` in X_001. Several payloads are managed API contracts and carry authored IDs/messages; converting them requires owner route cards and DTO hashing decisions outside the typed signal corridor pass.

## Reactor / Hull / Airlock Verdict

- Reactor/power audited domain hot hits: 0 `GlobalSignals.Publish/Push/TryDequeue/*Writer`.
- Hull deformation audited domain hot hits: 0 `GlobalSignals.Publish/Push/TryDequeue/*Writer`.
- Airlock animation/domain hot hits: 0 `GlobalSignals.Publish/Push/TryDequeue/*Writer`.
- Remaining hull/power/world helper hits are AUP origin/bootstrap helpers, not old central event-queue traffic.

## Build Status

Not run. Latest guard showed CPU 100 percent with active `csc` and multiple `dotnet` processes.
