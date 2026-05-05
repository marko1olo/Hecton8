# EVENT FLOW MAP — HECTON-8 Static Audit
Date: 2026-05-04
Status: DEPRECATED


**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandates Followed:** ARCH_Global_Registry_ServiceLocator_DI_Init, OPT_Zero_GC_Policy_AllocFree_Mandate

---

## I. HectonEventBus (Modding API Event Bus)

| Event Type | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `ItemCollectedEvent` | `PlayerInventory.cs` (L649, L983) | `ResourceScarcityDirector.cs`, `QuestManager.cs`, `GlobalProfileManager.cs` | Low |
| `ItemRecycledEvent` | `ScrapManager.cs` (L87), `ResourceRecyclerModule.cs` (L334) | `EnvironmentalStrainManager.cs`, `GlobalProfileManager.cs` | Low |
| `ItemDiscardedEvent` | `PDAInventoryTab.cs` (L1544) | `EnvironmentalStrainManager.cs` | Low |
| `ItemCraftedEvent` | `ModLoader.cs` (L524) | `PlayerAchievementRegistry.cs`, `PDALogbookManager.cs`, `GlobalProfileManager.cs` | Low |
| `BiomeDiscoveredEvent` | `HectonDiscoveryManager.cs` (L138) | `DynamicDifficultyDirector.cs`, `QuestManager.cs`, `GlobalProfileManager.cs` | Low |
| `LoreAcquiredEvent` | `NarrativeDiscovery.cs` (L138), `AudioLogSystem.cs` (L184) | `LoreDatabaseManager.cs`, `QuestManager.cs` | Low |
| `PlayerDiedEvent` | `HectonSurvivalSystem.cs` (L1092) | `DynamicDifficultyDirector.cs`, `RunModifierController.cs`, `GlobalProfileManager.cs`, `PDADeathMemoryDump.cs` | Low |
| `PlayerTakeDamageEvent` | `HectonSurvivalSystem.cs` (L1244) | *(0 subscribers via HectonEventBus)* | **High — see Dead Events** |
| `AchievementUnlockedEvent` | `PlayerAchievementRegistry.cs` (L309) | `DynamicDifficultyDirector.cs`, `GlobalProfileManager.cs` | Low |
| `PlayerAdvisoryIssuedEvent` | `PDAContextualAdvisorySystem.cs` (L150) | `DynamicDifficultyDirector.cs` | Low |
| `GameLoadedEvent` | `ModLoader.cs` (L516) | `DynamicDifficultyDirector.cs`, `PlayerAchievementRegistry.cs`, `PDAContextualAdvisorySystem.cs`, `PDALogbookManager.cs`, `GlobalProfileManager.cs`, `RunModifierController.cs`, `HectonOSBootManager.cs` | Low |
| `PlayerSpawnedEvent` | `ModLoader.cs` (L548) | `PDAContextualAdvisorySystem.cs`, `PDALogbookManager.cs`, `HectonOSBootManager.cs` | Low |
| `BaseModulePlacedEvent` | `PlayerBuilder.cs` (L1012) | *(0 subscribers via HectonEventBus)* | **High — see Dead Events** |

---

## II. Static Event Buses (C# `event` delegates)

### InteractionEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnItemCollected` | `PlayerInventory.cs` | `FirstHourDirector.cs` | Low |
| `OnInteractionStarted` | `PlayerInteraction.cs`, `Fabricator.cs`, `SaveStation.cs`, `EmergencyServiceRelay.cs` | *(no subscribers found)* | **Medium — see Dead Events** |
| `OnHoverChanged` | `PlayerInteraction.cs` | `InteractionUI.cs`, `CameraJuiceSystem.cs` | Low |

### CraftingEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnFabricatorOpened` | `Fabricator.cs` (L295) | `HectonFabricatorUI.cs` | Low |
| `OnFabricatorClosed` | `PauseMenuController.cs` (L273) | `HectonFabricatorUI.cs` | Low |
| `OnCraftStarted` | `Fabricator.cs` (L381) | *(no subscribers found)* | **Low — telemetry only** |
| `OnCraftCompleted` | `Fabricator.cs` (L546) | `FirstHourDirector.cs`, `ModLoader.cs`, `HectonFabricatorUI.cs` | Low |
| `OnCraftCancelled` | `Fabricator.cs` (L411) | *(no subscribers found)* | **Low — telemetry only** |
| `OnCraftProgressUpdated` | `Fabricator.cs` (L462, L543) | `HectonFabricatorUI.cs` | Low |

### SaveEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnSaveStarted` | `SaveManager.cs` (L272) | `MainMenuController.cs`, `PauseMenuController.cs` | Low |
| `OnSaveCompleted` | `SaveManager.cs` (L353) | `MainMenuController.cs`, `PauseMenuController.cs`, `HUDSaveNotificationLink.cs`, `SaveThumbnailCapture.cs` | Low |
| `OnSaveFailed` | `SaveManager.cs` (L258, L267, L360) | `MainMenuController.cs`, `PauseMenuController.cs`, `HUDSaveNotificationLink.cs` | Low |
| `OnLoadStarted` | `SaveManager.cs` (L423) | `MainMenuController.cs` | Low |
| `OnLoadCompleted` | `SaveManager.cs` (L540) | `MainMenuController.cs`, `ModLoader.cs`, `ModWorldPersistenceManager.cs` | Low |
| `OnLoadFailed` | `SaveManager.cs` (L400, L409, L418, L547) | `MainMenuController.cs` | Low |

### FlashlightEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnToggled` | `PlayerFlashlight.cs` (L441, L452) | `HectonSuitHUD_v4.cs`, `HectonSuitHUDExtensions.cs`, `SargassumMicroFaunaBoids.cs` | Low |
| `OnBatteryDepleted` | `PlayerFlashlight.cs` (L791) | `HectonSuitHUDExtensions.cs` | Low |
| `OnOverheat` | `PlayerFlashlight.cs` (L809) | `HectonSuitHUDExtensions.cs` | Low |
| `OnFlickerStart` | `PlayerFlashlight.cs` (L847) | `HectonSuitHUDExtensions.cs` | Low |

### PDAEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnOpened` | `PlayerPDA.cs` (L587) | `HectonSuitHUD_v4.cs`, `HectonSuitHUDExtensions.cs`, `PDAInventoryTab.cs`, `PDAShellChrome.cs`, `PDALoadoutTab.cs`, `PDAConstructionTab.cs`, `PDAControlsRebindUI.cs`, `PDADataLogTab.cs`, `PDABarterTab.cs`, `PDASpectrumTab.cs`, `PDAAtlasSignalTab.cs` | Low |
| `OnClosed` | `PlayerPDA.cs` (L618, L672) | `HectonSuitHUD_v4.cs`, `HectonSuitHUDExtensions.cs`, `PDAShellChrome.cs`, `PDALoadoutTab.cs`, `PDAConstructionTab.cs`, `PDABarterTab.cs`, `HectonOSBootManager.cs` | Low |
| `OnTabChanged` | `PlayerPDA.cs` (L643) | `PDAInventoryTab.cs`, `PDAShellChrome.cs`, `PDALoadoutTab.cs`, `PDAConstructionTab.cs`, `PDAControlsRebindUI.cs`, `PDABarterTab.cs` | Low |
| `OnLowBatteryShutdown` | `PlayerPDA.cs` (L854) | `HectonSuitHUDExtensions.cs` | Low |

### ModuleStatusEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnModuleEnter` | `BaseModule.cs` (L1463) | `PlayerToolManager.cs`, `TraumaDispatcher.cs` | Low |
| `OnModuleExit` | `BaseModule.cs` (L584, L1496) | `PlayerToolManager.cs`, `TraumaDispatcher.cs` | Low |

### ScanEvents

| Event | Publisher (File) | Subscriber(s) (Files) | Logic Risk |
|---|---|---|---|
| `OnScanTriggered` | `ScannerTool.cs` (L414) | `HectonScanMarkerSystem.cs` (via OnNodeFound) | Low |
| `OnNodeFound` | `ScannerTool.cs` (L700) | `HectonScanMarkerSystem.cs`, `ScanLogSystem.cs` | Low |
| `OnEntryDiscovered` | `ScannerTool.cs` (L656, L703, L852, L871) | `ScanLogSystem.cs`, `FirstHourDirector.cs` | Low |

---

## III. Dead Events (Published, 0 Subscribers)

| Event | Bus | Publisher | Severity | Notes |
|---|---|---|---|---|
| `PlayerTakeDamageEvent` | HectonEventBus | `HectonSurvivalSystem.cs` | **Medium** | Published for modding API consumption but no internal subscriber. Damage cancellation logic via `IsCancelled` is dead code internally. |
| `BaseModulePlacedEvent` | HectonEventBus | `PlayerBuilder.cs` | **Medium** | Published for modding API but no internal subscriber. Quest/achievement hooks missing. |
| `InteractionEvents.OnInteractionStarted` | Static C# event | `PlayerInteraction.cs` + others | **Low** | Published but no `.OnInteractionStarted +=` found in first-party code. May be consumed by mods or future systems. |
| `CraftingEvents.OnCraftStarted` | Static C# event | `Fabricator.cs` | **Low** | Telemetry-only event, no subscriber. |
| `CraftingEvents.OnCraftCancelled` | Static C# event | `Fabricator.cs` | **Low** | Telemetry-only event, no subscriber. |

---

## IV. Orphan Events (Subscribed, 0 Publishers)

| Event | Bus | Subscriber | Severity | Notes |
|---|---|---|---|---|
| *(none detected)* | — | — | — | All subscriptions have matching publishers. |

---

**STATUS:** PENDING VERIFICATION — runtime event dispatch not verified. Static analysis only.
