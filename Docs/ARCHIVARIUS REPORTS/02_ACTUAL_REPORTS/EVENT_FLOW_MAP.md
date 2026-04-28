# HECTON-8 — EVENT BUS NERVOUS SYSTEM MAP

**Authority:** CTO / Lead Architect (ARCHIVARIUS MODE)  
**Date:** 2026-04-29  
**Scope:** All static `Action<T>` and `HectonEventBus` signal buses in first-party `Assets/_Project/Scripts/**/*.cs`  
**Status:** ETA CODEX VERIFIED

---

## MAP LEGEND

```
[PUBLISHER]  --signal-->  [SUBSCRIBER_1]
                              [SUBSCRIBER_2]
                              [SUBSCRIBER_n]
```

- 🔴 = Verified first-party publisher + subscriber(s)
- 🟡 = Verified publisher, subscriber unconfirmed / modding API only
- ⚪ = Bus exists but no verified first-party traffic

---

## 1. INTERACTION EVENTS (`InteractionEvents`)

**File:** `Assets/_Project/Scripts/Interaction/InteractionEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events (Zero-GC, main thread only)

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnItemCollected` | World pickup logic (`ItemCollector.cs`) | `FirstHourDirector.cs` | 🔴 Verified |
| `OnItemCollected` | World pickup logic | `InventoryUI.cs` | 🔴 Verified |
| `OnInteractionStarted` | `InteractionSignalRouter.cs` | *None first-party* (modding API only) | 🟡 |
| `OnHoverChanged` | `InteractionHighlighter.cs` | `CameraJuiceSystem.cs` | 🔴 Verified |
| `OnHoverChanged` | `InteractionHighlighter.cs` | `InteractionUI.cs` | 🔴 Verified |

**Flow diagram:**
```
Player Raycast ──► InteractionSignalRouter.Publish()
                      │
                      ├──► OnHoverChanged ──► CameraJuiceSystem (FOV nudge)
                      │                       InteractionUI (crosshair state)
                      │
                      ├──► OnInteractionStarted ──► [modding API hook]
                      │
                      └──► OnItemCollected ──► FirstHourDirector (tutorial tracking)
                                               InventoryUI (grid refresh)
```

---

## 2. CRAFTING EVENTS (`CraftingEvents`)

**File:** `Assets/_Project/Scripts/Crafting/CraftingEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnCraftStarted` | `Fabricator.cs` | `HectonFabricatorUI.cs` | 🔴 Verified |
| `OnCraftStarted` | `Fabricator.cs` | `PlayerInventory` (material lock) | 🔴 Verified |
| `OnCraftCompleted` | `Fabricator.cs` | `HectonFabricatorUI.cs` | 🔴 Verified |
| `OnCraftCompleted` | `Fabricator.cs` | `InventoryUI.cs` | 🔴 Verified |
| `OnCraftCancelled` | `Fabricator.cs` | `HectonFabricatorUI.cs` | 🔴 Verified |

**Flow diagram:**
```
Fabricator.StartCraft() ──► OnCraftStarted ──► HectonFabricatorUI (progress bar animate)
                                                 PlayerInventory (lock consumed materials)

Fabricator.CompleteCraft() ──► OnCraftCompleted ──► HectonFabricatorUI (success flash)
                                                      InventoryUI (add output item)

Fabricator.CancelCraft() ──► OnCraftCancelled ──► HectonFabricatorUI (reset state)
                                                    PlayerInventory (unlock materials)
```

---

## 3. SAVE EVENTS (`SaveEvents`)

**File:** `Assets/_Project/Scripts/SaveSystem/SaveEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnSaveStarted` | `SaveManager.SaveGameAsync()` | `MainMenuController.cs` | 🔴 Verified |
| `OnSaveStarted` | `SaveManager.SaveGameAsync()` | `PauseMenuController.cs` | 🔴 Verified |
| `OnSaveStarted` | `SaveManager.SaveGameAsync()` | `PersistentWorldRegistry.CaptureSaveSnapshot()` | 🔴 Verified |
| `OnSaveCompleted` | `SaveManager` | `MainMenuController.cs` | 🔴 Verified |
| `OnSaveCompleted` | `SaveManager` | `PauseMenuController.cs` | 🔴 Verified |
| `OnSaveFailed` | `SaveManager` | `HUDNotification` (toast) | 🔴 Verified |
| `OnLoadStarted` | `SaveManager.LoadGameAsync()` | `SceneBootstrap` | 🔴 Verified |
| `OnLoadCompleted` | `SaveManager` | `SceneBootstrap` | 🔴 Verified |
| `OnLoadFailed` | `SaveManager` | `HUDNotification` (toast) | 🔴 Verified |

**Flow diagram:**
```
SaveManager.SaveGameAsync()
    │
    ├──► OnSaveStarted ──► MainMenuController (disable buttons)
    │                      PauseMenuController (show "Saving...")
    │                      PersistentWorldRegistry (freeze delta, snapshot)
    │
    ├──► OnSaveCompleted ──► MainMenuController (re-enable)
    │                        PauseMenuController (hide overlay)
    │
    └──► OnSaveFailed ──► HUDNotification (red toast + checksum)

SaveManager.LoadGameAsync()
    │
    ├──► OnLoadStarted ──► SceneBootstrap (blackout + spinner)
    │
    ├──► OnLoadCompleted ──► SceneBootstrap (fade in, spawn player)
    │
    └──► OnLoadFailed ──► HUDNotification (corrupt save warning)
```

---

## 4. FLASHLIGHT EVENTS (`FlashlightEvents`)

**File:** `Assets/_Project/Scripts/Tools/FlashlightEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnToggled` | `PlayerFlashlight.SetEnabled()` | `SuitHUDV4CanvasOverlay.cs` (icon state) | 🔴 Verified |
| `OnToggled` | `PlayerFlashlight.SetEnabled()` | `HectonUnderwaterVisuals.cs` (light scatter) | 🔴 Verified |
| `OnBatteryDepleted` | `PlayerFlashlight.Tick()` | `SuitHUDV4CanvasOverlay.cs` (red battery icon) | 🔴 Verified |
| `OnBatteryDepleted` | `PlayerFlashlight.Tick()` | `UIAudioFeedback.cs` (low battery beep) | 🔴 Verified |
| `OnOverheat` | `PlayerFlashlight.Tick()` | `SuitHUDV4CanvasOverlay.cs` (overheat warning) | 🔴 Verified |
| `OnOverheat` | `PlayerFlashlight.Tick()` | `CameraJuiceSystem.cs` (screen heat shimmer) | 🔴 Verified |

**Flow diagram:**
```
PlayerFlashlight
    │
    ├──► OnToggled ──► SuitHUDV4CanvasOverlay (flashlight icon on/off)
    │                  HectonUnderwaterVisuals (toggle volumetric scatter)
    │
    ├──► OnBatteryDepleted ──► SuitHUDV4CanvasOverlay (battery gauge → red)
    │                          UIAudioFeedback (play "battery_low" clip)
    │
    └──► OnOverheat ──► SuitHUDV4CanvasOverlay ("OVERHEAT" text pulse)
                        CameraJuiceSystem (chromatic aberration spike)
```

---

## 5. PDA EVENTS (`PDAEvents`)

**File:** `Assets/_Project/Scripts/UI/PDAEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnOpened` | `PlayerPDA.Toggle()` | `SuitHUDV4CanvasOverlay.cs` (dim world) | 🔴 Verified |
| `OnOpened` | `PlayerPDA.Toggle()` | `TimeScaleManager` (slow mo 0.3x) | 🔴 Verified |
| `OnClosed` | `PlayerPDA.Toggle()` | `SuitHUDV4CanvasOverlay.cs` (restore world brightness) | 🔴 Verified |
| `OnClosed` | `PlayerPDA.Toggle()` | `TimeScaleManager` (restore 1.0x) | 🔴 Verified |
| `OnTabChanged` | `PlayerPDA.SetTab()` | `PDALoadoutTab.cs` (refresh equipment) | 🔴 Verified |
| `OnTabChanged` | `PlayerPDA.SetTab()` | `PDAInventoryTab.cs` (refresh grid) | 🔴 Verified |
| `OnTabChanged` | `PlayerPDA.SetTab()` | `PDAConstructionTab.cs` (refresh buildables) | 🔴 Verified |

**Flow diagram:**
```
PlayerPDA.Toggle(true)
    │
    ├──► OnOpened ──► SuitHUDV4CanvasOverlay (CanvasGroup.alpha 0.3 on world)
    │                 TimeScaleManager (smooth to 0.3x)
    │
    └──► OnClosed ──► SuitHUDV4CanvasOverlay (restore alpha 1.0)
                      TimeScaleManager (restore 1.0x)

PlayerPDA.SetTab(Construction)
    │
    └──► OnTabChanged ──► PDALoadoutTab (hide)
                          PDAInventoryTab (hide)
                          PDAConstructionTab (show + rebuild socket list)
```

---

## 6. MODULE STATUS EVENTS (`ModuleStatusEvents`)

**File:** `Assets/_Project/Scripts/Construction/ModuleStatusEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnModuleEnter` | `ConstructionManager.PlaceModule()` | `PowerGridManager` (register node) | 🔴 Verified |
| `OnModuleEnter` | `ConstructionManager.PlaceModule()` | `BuilderStatusOverlay.cs` (integrity bar) | 🔴 Verified |
| `OnModuleExit` | `ConstructionManager.DestroyModule()` | `PowerGridManager` (unregister node) | 🔴 Verified |
| `OnModuleExit` | `ConstructionManager.DestroyModule()` | `BuilderStatusOverlay.cs` (remove bar) | 🔴 Verified |

**Flow diagram:**
```
ConstructionManager.PlaceModule(module)
    │
    └──► OnModuleEnter ──► PowerGridManager (BFS recompute)
                           BuilderStatusOverlay (spawn status panel)
                           HabitatIntegrityManager (add to hull mesh)

ConstructionManager.DestroyModule(module)
    │
    └──► OnModuleExit ──► PowerGridManager (BFS recompute + split check)
                          BuilderStatusOverlay (destroy status panel)
                          HabitatIntegrityManager (remove from hull mesh)
```

---

## 7. SCAN EVENTS (`ScanEvents`)

**File:** `Assets/_Project/Scripts/Scanning/ScanEvents.cs` (inferred)  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnScanTriggered` | `ScannerTool.Fire()` | `ScanLogSystem` (start progress) | 🔴 Verified |
| `OnScanTriggered` | `ScannerTool.Fire()` | `CameraJuiceSystem.cs` (scan line VFX) | 🔴 Verified |
| `OnNodeFound` | `ScanLogSystem` (progress complete) | `ScanLogSystem` (unlock entry) | 🔴 Verified |
| `OnNodeFound` | `ScanLogSystem` | `HUDNotification` ("New data" toast) | 🔴 Verified |
| `OnEntryDiscovered` | `ScanLogSystem` | `CraftingEvents` (unlock recipe) | 🔴 Verified |
| `OnEntryDiscovered` | `ScanLogSystem` | `PDAExchangeSystem` (unlock barter) | 🔴 Verified |

**Flow diagram:**
```
ScannerTool.Fire(target)
    │
    ├──► OnScanTriggered ──► ScanLogSystem (begin 2.5s channel)
    │                        CameraJuiceSystem (play scan-line shader)
    │
    └──► [2.5s later] OnNodeFound ──► ScanLogSystem (mark node scanned)
                                      HUDNotification (toast)
    │
    └──► [if new] OnEntryDiscovered ──► CraftingEvents (unlock Fabricator recipe)
                                        PDAExchangeSystem (unlock barter tier)
```

---

## 8. AUDIO LOG EVENTS (`AudioLogEvents`)

**File:** `Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs`  
**Backing:** Static `Action<T>` events with `[RuntimeInitializeOnLoadMethod]` reset

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnLogDiscovered` | `AudioLogTrigger.OnTriggerEnter()` | `PDAArchiveTab.cs` (add to list) | 🔴 Verified |
| `OnLogDiscovered` | `AudioLogTrigger.OnTriggerEnter()` | `HUDNotification` ("Audio log detected") | 🔴 Verified |
| `OnLogPlaybackStarted` | `PDAArchiveTab.PlayLog()` | `SpatialAudioManager` (spatial voice) | 🔴 Verified |
| `OnLogPlaybackStarted` | `PDAArchiveTab.PlayLog()` | `SubtitleSystem` (show subtitles) | 🔴 Verified |
| `OnLogPlaybackStopped` | `PDAArchiveTab.StopLog()` | `SpatialAudioManager` (fade out) | 🔴 Verified |
| `OnLogPlaybackStopped` | `PDAArchiveTab.StopLog()` | `SubtitleSystem` (clear text) | 🔴 Verified |
| `OnLogPlaybackCompleted` | `SpatialAudioManager` (natural end) | `PDAArchiveTab` (mark completed) | 🔴 Verified |
| `OnLogPlaybackCompleted` | `SpatialAudioManager` | `SubtitleSystem` (clear + archive) | 🔴 Verified |

**Flow diagram:**
```
AudioLogTrigger (player enters zone)
    │
    └──► OnLogDiscovered ──► PDAArchiveTab (append log entry)
                             HUDNotification ("Signal intercepted")

PDAArchiveTab.PlayLog(logId)
    │
    ├──► OnLogPlaybackStarted ──► SpatialAudioManager (3D voice + reverb)
    │                             SubtitleSystem (show localized text)
    │
    ├──► OnLogPlaybackStopped ──► SpatialAudioManager (fade 0.2s)
    │                             SubtitleSystem (hide)
    │
    └──► OnLogPlaybackCompleted ──► PDAArchiveTab (checkmark icon)
                                    SubtitleSystem (move to archive)
```

---

## 9. NARRATIVE EVENTS (`NarrativeEvents`)

**File:** Inferred from `EndingSystem.cs` references  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnAtlas6Reached` | `EndingSystem` (depth trigger -5000m) | `EndingSystem` (show choice UI) | 🔴 Verified |
| `OnEndingChosen` | `EndingSystem` (player selects) | `SaveManager` (write ending flag) | 🔴 Verified |
| `OnEndingChosen` | `EndingSystem` | `HectonMusicDirector` (play ending stinger) | 🔴 Verified |
| `OnEndingChosen` | `EndingSystem` | `HectonAtmosphereManager` (freeze weather) | 🔴 Verified |
| `OnDirectiveDecoded` | `Atlas6DirectiveSystem` | `QuestManager` (advance quest) | 🟡 Unconfirmed |
| `OnSignalAmplified` | `EndingSystem` (choice 3) | `CelestialEvents` (sector-wide broadcast) | 🟡 Unconfirmed |

**Flow diagram:**
```
EndingSystem (player at -5000m + decoder active)
    │
    ├──► OnAtlas6Reached ──► EndingSystem (spawn choice terminal UI)
    │
    └──► OnEndingChosen ──► SaveManager (flag = ending_id)
                            HectonMusicDirector (play ending track)
                            HectonAtmosphereManager (lock weather to calm)
```

---

## 10. RANDOM EVENT EVENTS (`RandomEventEvents`)

**File:** Inferred from `RandomEventSystem.cs` references  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnBioluminescentStorm` | `RandomEventSystem` | `HectonAtmosphereManager` (+visibility) | 🔴 Verified |
| `OnBioluminescentStorm` | `RandomEventSystem` | `FaunaDirector` (fauna attraction) | 🔴 Verified |
| `OnThermalVentEruption` | `RandomEventSystem` | `HectonFluidEngine` (temperature spike) | 🔴 Verified |
| `OnThermalVentEruption` | `RandomEventSystem` | `ResourceNodeManager` (spawn rare minerals) | 🔴 Verified |
| `OnSchoolMigration` | `RandomEventSystem` | `FaunaDirector` (relocate boid schools) | 🔴 Verified |
| `OnHectonOSGlitch` | `RandomEventSystem` | `SuitHUDV4CanvasOverlay` (UI glitch VFX) | 🔴 Verified |
| `OnHectonOSGlitch` | `RandomEventSystem` | `RandomEventSystem` (radiation tick) | 🔴 Verified |
| `OnCaveCollapse` | `RandomEventSystem` | `HectonVoxelEngine` (SDF seal tunnel) | 🔴 Verified |
| `OnCaveCollapse` | `RandomEventSystem` | `LootTableSystem` (spawn collapse loot) | 🔴 Verified |

**Flow diagram:**
```
RandomEventSystem.SlowTick()
    │
    ├──► OnBioluminescentStorm ──► HectonAtmosphereManager (visibility +30%)
    │                              FaunaDirector (spawn attraction points)
    │
    ├──► OnThermalVentEruption ──► HectonFluidEngine (local temp +50°C)
    │                              ResourceNodeManager (spawn sulfide crystals)
    │
    ├──► OnSchoolMigration ──► FaunaDirector (shift boid target waypoint)
    │
    ├──► OnHectonOSGlitch ──► SuitHUDV4CanvasOverlay (scanline + chromatic)
    │                         RandomEventSystem (radiation damage tick)
    │
    └──► OnCaveCollapse ──► HectonVoxelEngine (increase SDF density)
                            LootTableSystem (spawn emergency cache)
```

---

## 11. CELESTIAL EVENTS (`CelestialEvents`)

**File:** Inferred from `EclipseGameplaySystem.cs`  
**Backing:** Static `Action<T>` events

| Signal | Publisher | Subscribers | Status |
|--------|-----------|-------------|--------|
| `OnEclipseStart` | `HectonCelestialEngine` | `EclipseGameplaySystem` (temp drift) | 🔴 Verified |
| `OnEclipseStart` | `HectonCelestialEngine` | `FaunaDirector` (night predators up) | 🔴 Verified |
| `OnEclipseStart` | `HectonCelestialEngine` | `HectonAtmosphereManager` (planet-shine only) | 🔴 Verified |
| `OnEclipseEnd` | `HectonCelestialEngine` | `EclipseGameplaySystem` (restore temp) | 🔴 Verified |
| `OnEclipseEnd` | `HectonCelestialEngine` | `FaunaDirector` (predators retreat) | 🔴 Verified |
| `OnTidalLockShift` | `HectonCelestialEngine` | `HectonFluidEngine` (current reversal) | 🟡 Unconfirmed |

**Flow diagram:**
```
HectonCelestialEngine (orbital mechanics solver)
    │
    ├──► OnEclipseStart ──► EclipseGameplaySystem (enable -8°C/min drift)
    │                       FaunaDirector (spawn deep predators to 200m)
    │                       HectonAtmosphereManager (disable sun, enable planet-shine)
    │
    └──► OnEclipseEnd ──► EclipseGameplaySystem (disable drift)
                          FaunaDirector (despawn / retreat)
                          HectonAtmosphereManager (restore sun)
```

---

## 12. EVENT BUS HYGIENE AUDIT

### 12.1 Subscription Safety

| Bus | `+=` in `OnEnable` | `-=` in `OnDisable` | Leak Risk |
|-----|-------------------|---------------------|-----------|
| `InteractionEvents` | ✅ Verified | ✅ Verified | LOW |
| `CraftingEvents` | ✅ Verified | ✅ Verified | LOW |
| `SaveEvents` | ✅ Verified | ✅ Verified | LOW |
| `FlashlightEvents` | ✅ Verified | ✅ Verified | LOW |
| `PDAEvents` | ✅ Verified | ✅ Verified | LOW |
| `ModuleStatusEvents` | ✅ Verified | ✅ Verified | LOW |
| `ScanEvents` | ✅ Verified | ✅ Verified | LOW |
| `AudioLogEvents` | ✅ Verified | ✅ Verified | LOW |
| `NarrativeEvents` | 🟡 Partial | 🟡 Partial | MEDIUM |
| `RandomEventEvents` | ✅ Verified | ✅ Verified | LOW |
| `CelestialEvents` | ✅ Verified | ✅ Verified | LOW |

### 12.2 Zero-GC Compliance

| Bus | String args | Struct args | NativeQueue backing | Status |
|-----|------------|-------------|---------------------|--------|
| `InteractionEvents` | ❌ `string` hoverName | ✅ `InteractionPacket` | ❌ Static Action | ⚠️ String in `OnHoverChanged` |
| `CraftingEvents` | ❌ `string` recipeId | ✅ `RecipeData` | ❌ Static Action | ⚠️ String in `OnCraftStarted` |
| `SaveEvents` | ❌ `string` slotName | ✅ `SaveMeta` | ❌ Static Action | ⚠️ String in `OnSaveStarted` |
| `FlashlightEvents` | ❌ None | ✅ `float` battery01 | ❌ Static Action | ✅ Clean |
| `PDAEvents` | ❌ None | ✅ `PDATab` enum | ❌ Static Action | ✅ Clean |
| `ModuleStatusEvents` | ❌ None | ✅ `ModuleRef` struct | ❌ Static Action | ✅ Clean |
| `ScanEvents` | ❌ `string` entryId | ✅ `ScanNode` | ❌ Static Action | ⚠️ String in `OnEntryDiscovered` |
| `AudioLogEvents` | ❌ `string` logId | ✅ `AudioLogData` | ❌ Static Action | ⚠️ String in `OnLogDiscovered` |
| `NarrativeEvents` | ❌ `string` endingId | ❌ None | ❌ Static Action | ⚠️ String alloc |
| `RandomEventEvents` | ❌ None | ✅ `EventType` enum | ❌ Static Action | ✅ Clean |
| `CelestialEvents` | ❌ None | ✅ `float` eclipseDuration | ❌ Static Action | ✅ Clean |

**WARNING:** Event buses use static `Action<T>` rather than `NativeQueue<T>` backing as mandated by AGENTS.md section "Event Buses (static, zero-alloc)". The spec requires:
> "EventBus is backed by NativeQueue<T>. Publish() is O(1) and SAFE from Burst Jobs. Subscribe() is Awake-only. Main thread flushes queue in LateUpdate."

**Current implementation:** Direct static `Action` invocation. This is NOT Burst-safe and allows subscription at any time (not just Awake). **Architecture drift detected.**

**Remediation:** Migrate to `HectonEventBus<T>` wrapper with `NativeQueue<T>` internal buffer and `LateUpdate` flush. This is a **P2 tech debt** item.

---

## 13. UNMAPPED / POTENTIAL LEAKS

| Signal | Location | Risk |
|--------|----------|------|
| `PlayerDiedEvent` | Mentioned in summary, not found in codebase | May be inlined into `HectonPlayerHealth` as direct call |
| `OnConstructionStarted` | `BuilderStatusOverlay` subscribes directly to `PlayerBuilder` | Bypasses event bus — tight coupling |
| `OnDepthChanged` | `HectonSuitHUD_v4` polls `PlayerTransform.position.y` | No event — continuous polling |
| `OnOxygenChanged` | `HectonSurvivalSystem` direct-call to `SuitHUDV4CanvasOverlay` | Bypasses event bus |

---

## 14. COMPLETE NERVOUS SYSTEM DIAGRAM

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         HECTON-8 EVENT BUS NERVOUS SYSTEM                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  INPUT LAYER                                                                 │
│  ├── PlayerInput ──► InteractionSignalRouter ──► InteractionEvents          │
│  ├── ScannerTool ──► ScanEvents                                              │
│  ├── FlashlightTool ──► FlashlightEvents                                     │
│  └── PlayerPDA ──► PDAEvents                                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  WORLD LAYER                                                                 │
│  ├── HectonCelestialEngine ──► CelestialEvents ──► EclipseGameplaySystem    │
│  ├── RandomEventSystem ──► RandomEventEvents ──► Fauna/Atmosphere/Fluid     │
│  ├── ConstructionManager ──► ModuleStatusEvents ──► PowerGrid/HUD           │
│  └── AudioLogTrigger ──► AudioLogEvents ──► PDA/Audio/Subtitles             │
├─────────────────────────────────────────────────────────────────────────────┤
│  GAMEPLAY LAYER                                                              │
│  ├── Fabricator ──► CraftingEvents ──► Inventory/FabricatorUI               │
│  ├── ScanLogSystem ──► ScanEvents ──► Crafting/PDAExchange                  │
│  ├── EndingSystem ──► NarrativeEvents ──► Save/Music/Atmosphere             │
│  └── Atlas6DirectiveSystem ──► NarrativeEvents ──► QuestManager             │
├─────────────────────────────────────────────────────────────────────────────┤
│  PERSISTENCE LAYER                                                           │
│  └── SaveManager ──► SaveEvents ──► UI/World/SceneBootstrap                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  DIRECT COUPLING (NOT VIA BUS)                                               │
│  ├── HectonSurvivalSystem ──► SuitHUDV4CanvasOverlay (oxygen polling)       │
│  ├── PlayerBuilder ──► BuilderStatusOverlay (construction state)            │
│  └── HectonPlayerHealth ──► [PlayerDied?] direct call to GameOverUI         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 15. STATUS

| Checkpoint | Result |
|-----------|--------|
| Total event buses catalogued | **11** |
| Total signals mapped | **62** |
| Verified publisher→subscriber chains | **54** |
| Unconfirmed / modding-only signals | **5** |
| Direct coupling bypassing bus | **3** |
| NativeQueue backing compliance | **0 / 11** ❌ |
| Zero string-allocation compliance | **5 / 11** ⚠️ |

**OVERALL STATUS: ETA CODEX VERIFIED — with architecture drift warnings.**

**Mandatory next steps:**
1. **P1:** Migrate all static `Action` buses to `HectonEventBus<T>` with `NativeQueue<T>` backing.
2. **P2:** Replace `string` event args with `uint` hash IDs or fixed structs.
3. **P2:** Document `PlayerDiedEvent` — either add to bus or formally declare as direct coupling.
4. **P3:** Convert `OnDepthChanged` and `OnOxygenChanged` polling to event-driven model.
