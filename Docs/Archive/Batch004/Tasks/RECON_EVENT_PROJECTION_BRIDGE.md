# RECON_EVENT_PROJECTION_BRIDGE

Agent: `MODDING_LEAD`
Prompt: `EVENT_PROJECTION_BRIDGE`
Status: `PENDING VERIFICATION`

## Scope

This recon documents first-party managed event usage that still crosses `HectonEventBus`. It also records the explicit dead-code hunt required for `SubmarineStructuralGrid` and `FaunaBrain`.

## Findings

- `HectonEventBus.Instance`: no hits under `Assets/_Project/Scripts`.
- `SubmarineStructuralGrid`: no direct `EventBus.Publish` or `HectonEventBus` hits.
- `FaunaBrain`: no direct `EventBus.Publish` or `HectonEventBus` hits.
- First-party managed event usage still exists and cannot be honestly declared migrated in this slice.

## First-Party Publishers Still Using Managed Events

- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1698` publishes unmanaged storm/shock payload.
- `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:609` publishes unmanaged leak payload.
- `Assets/_Project/Scripts/Economy/ScrapManager.cs:120` publishes `ItemRecycledEvent`.
- `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs:343` publishes `ItemRecycledEvent`.
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs:2064` publishes `PlayerDiedEvent`.
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs:2252` publishes cancellable `PlayerTakeDamageEvent`.
- `Assets/_Project/Scripts/HectonCelestialEngine.cs:5850` publishes unmanaged eclipse payload.
- `Assets/_Project/Scripts/HectonItem.cs:391` publishes `ItemCollectedEvent`.
- `Assets/_Project/Scripts/Items/PickupItem.cs:405` publishes `ItemCollectedEvent`.
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:1242` publishes unmanaged meteor payload.
- `Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:279` publishes `ItemCollectedEvent`.
- `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:564` publishes `AchievementUnlockedEvent`.
- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:224` publishes `PlayerAdvisoryIssuedEvent`.
- `Assets/_Project/Scripts/PlayerInventory.cs:896` publishes unmanaged inventory payload.
- `Assets/_Project/Scripts/PlayerInventory.cs:902` publishes `ItemDiscardedEvent`.
- `Assets/_Project/Scripts/PlayerBuilder.cs:1208` publishes `BaseModulePlacedEvent`.

## First-Party Subscribers Still Using Managed Events

- `Assets/_Project/Scripts/Meta/RunModifierController.cs` subscribes to `PlayerDiedEvent` and `GameLoadedEvent`.
- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs` subscribes to achievement, game, death, collection, craft, and recycle events.
- `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs` subscribes to achievement, advisory, game, and death events.
- `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs` subscribes to crafting and game-load events.
- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs` subscribes to game-load and player-spawn events.
- `Assets/_Project/Scripts/PDA/PDALogbookManager.cs` subscribes to crafting, game-load, and player-spawn events.
- `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs` subscribes to item collection, recycle, and discard events.
- `Assets/_Project/Scripts/UI/HectonOSBootManager.cs` subscribes to game-load and player-spawn events.
- `Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs` subscribes to `PlayerDiedEvent`.

## Modding-Internal Managed Events

These are not first-party gameplay subscriptions. They remain inside the mod API boundary unless a future assembly split moves them:

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs` publishes `GameLoadedEvent` and `PlayerSpawnedEvent`.
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` emits deferred managed mod payloads.
- `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs` defines DTO/event payload contracts.

## Blockers

- Task 2 is blocked by first-party systems that use cancellable managed payloads and profile/meta side effects. Replacing those requires native `SignalBus<T>` contracts and read-models, not a search-and-replace.
- Task 3 is blocked by current assembly layout. `Hecton8.Core.asmdef` still contains both core and modding files, while `GlobalRegistry` and dispatcher code directly reference modding types. Creating `Hecton8.Modding.asmdef` now would manufacture an assembly cycle or missing references.

## Required Follow-Up

- Define native contracts for player damage, death, item economy, achievement, advisory, base placement, and world strain.
- Move first-party consumers onto `SignalBus<T>` snapshots or deterministic native read-models.
- Split contracts/signals/modding into real asmdefs only after core stops directly depending on mod implementation types.
