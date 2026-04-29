# HECTON-8 EVENT FLOW MAP

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: source-backed event topology visible in first-party code
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## 1. Audit Standard

This file only records flows that were directly observed in current `.cs` source.
Older versions mixed real buses with inferred publishers, inferred subscribers, and file names not confirmed in the repository. Those claims were removed.

Evidence basis for this pass:

- direct reads of bus definitions
- direct subscription scans via `rg`
- direct publish/raise call scans via `rg`

No runtime replay was executed in this pass.

## 2. Core Finding

The project uses multiple event styles at once:

- standalone static bus classes
- static bus classes nested inside feature owners
- feature-local instance events
- separate modding bus `HectonEventBus`

Because of that, there is no single "nervous system". There is a collection of overlapping signal surfaces.

## 3. Source-Confirmed Static Gameplay Buses

### 3.1 `InteractionEvents`

Definition confirmed in `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`

Signals confirmed:

- `OnItemCollected : Action<ItemData, int, Transform>`
- `OnInteractionStarted : Action<IInteractable, Transform>`
- `OnHoverChanged : Action<IInteractable>`

Confirmed subscribers:

- `FirstHourDirector` subscribes to `OnItemCollected`
- `InteractionUI` subscribes to `OnHoverChanged`
- `CameraJuiceSystem` subscribes to `OnHoverChanged`

Observed risk:

- static direct `Action` dispatch, not `NativeQueue`-backed

### 3.2 `CraftingEvents`

Definition confirmed in `Assets/_Project/Scripts/CraftingEvents.cs`

Signals confirmed:

- `OnFabricatorOpened`
- `OnFabricatorClosed`
- `OnCraftStarted`
- `OnCraftProgressUpdated`
- `OnCraftCompleted`
- `OnCraftCancelled`

Confirmed subscribers:

- `HectonFabricatorUI`
- `FirstHourDirector` on `OnCraftCompleted`
- `ModLoader` on `OnCraftCompleted`

Observed risk:

- comments claim "zero-GC" static bus, but architecture still diverges from the `NativeQueue` requirement in `AGENTS.md`

### 3.3 `SaveEvents`

Definition confirmed in `Assets/_Project/Scripts/SaveEvents.cs`

Signals confirmed:

- `OnSaveStarted`
- `OnSaveCompleted`
- `OnSaveFailed`
- `OnLoadStarted`
- `OnLoadCompleted`
- `OnLoadFailed`
- `OnEmergencyBackupRestoreRequested`

Confirmed subscribers:

- `MainMenuController`
- `PauseMenuController`
- `HUDSaveNotificationLink`
- `SaveThumbnailCapture`
- `ModLoader`
- `ModWorldPersistenceManager`

Observed risk:

- save/load events use `string` payloads for slot/error surfaces
- bus is static `Action`, not `NativeQueue`-backed

### 3.4 `ModuleStatusEvents`

Definition confirmed in `Assets/_Project/Scripts/ModuleStatusEvents.cs`

Signals confirmed:

- `OnModuleEnter`
- `OnModuleExit`

Confirmed publishers:

- `BaseModule` via `NotifyEnter` and `NotifyExit`

Confirmed subscribers:

- `TraumaDispatcher`
- `PlayerToolManager`

### 3.5 `ScanEvents`

Definition confirmed in `Assets/_Project/Scripts/ScanEvents.cs`

Signals confirmed:

- `OnScanTriggered : Action<float3, float>`
- `OnNodeFound : Action<float3>`
- `OnEntryDiscovered : Action<string, string, string, string>`

Confirmed publishers:

- `ScannerTool` raises all three surfaces directly

Confirmed subscribers:

- `HectonScanMarkerSystem` on `OnNodeFound`
- `ScanLogSystem` on `OnNodeFound` and `OnEntryDiscovered`
- `FirstHourDirector` on `OnEntryDiscovered`

Observed risk:

- `OnEntryDiscovered` carries four strings
- bus uses public delegate fields rather than `event` for some signals

### 3.6 `AudioLogEvents`

Definition confirmed in `Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs`

Signals confirmed:

- `OnLogDiscovered`
- `OnLogPlaybackStarted`
- `OnLogPlaybackStopped`
- `OnLogPlaybackCompleted`

Confirmed publishers:

- `AudioLogSystem`

Confirmed subscribers:

- `PDADataLogTab`
- `FirstHourDirector` on `OnLogDiscovered`

Observed risk:

- mixed payloads include `string` IDs

## 4. Source-Confirmed Feature-Embedded Static Buses

### 4.1 `PDAEvents`

Definition confirmed inside `Assets/_Project/Scripts/PlayerPDA.cs`

Signals confirmed:

- `OnOpened`
- `OnClosed`
- `OnTabChanged`
- `OnLowBatteryShutdown`

Confirmed publisher:

- `PlayerPDA`

Confirmed subscribers:

- `PDAInventoryTab`
- `PDALoadoutTab`
- `PDAConstructionTab`
- `PDABarterTab`
- `PDADataLogTab`
- `PDAAtlasSignalTab`
- `PDASpectrumTab`
- `PDAShellChrome`
- `PDAControlsRebindUI`
- `HectonOSBootManager`
- nested diagnostics terminal in `PlayerPDA`

### 4.2 `FlashlightEvents`

Definition confirmed inside `Assets/_Project/Scripts/PlayerFlashlight.cs`

Signals confirmed:

- `OnToggled`
- `OnBatteryDepleted`
- `OnOverheat`
- `OnFlickerStart`

Confirmed publisher:

- `PlayerFlashlight`

Confirmed subscribers:

- `SargassumMicroFaunaBoids` on `OnToggled`

No source-confirmed subscribers were established in this pass for:

- `OnBatteryDepleted`
- `OnOverheat`
- `OnFlickerStart`

### 4.3 `NarrativeEvents`

Definition confirmed in `Assets/_Project/Scripts/NarrativeEvents.cs`

Signals confirmed:

- `OnNarrativePOIRegistered`
- `OnNarrativePOIDisposed`
- `OnDiscoveryMade`
- `OnDepthTierReached`

Confirmed publishers:

- `NarrativeDiscovery`
- `HectonNarrativeDirector`
- `AudioLogSystem`
- `AtlasSignalSystem`
- `AtlasSignalDecoder`
- `DirectorMissionBridge`
- `EndingSystem`
- `EndingTerminalInteractable`
- `DepthZoneDirector`
- `EmergencyServiceRelay`
- `CorporateOrderSystem`
- `FirstHourDirector`

Confirmed subscribers:

- `HectonNarrativeDirector`
- `AudioLogSystem`
- `Atlas6DirectiveSystem`
- `FirstHourDirector`
- `SuitUpgradeManager`
- `QuestManager`

Observed risk:

- `OnDiscoveryMade` uses `string`

### 4.4 `RandomEventEvents`

Definition confirmed inside `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`

Signals confirmed:

- `OnEventStarted : Action<RandomEventType, float>`
- `OnEventEnded : Action<RandomEventType>`

Confirmed publisher:

- `RandomEventSystem`

Confirmed subscribers:

- none confirmed by source scan in this pass

Interpretation:

- either this bus is currently underconsumed
- or listeners exist outside the scanned patterns used in this pass

Status therefore remains partial, not dead and not verified live.

### 4.5 Celestial Surface Events

Definition confirmed inside `Assets/_Project/Scripts/HectonCelestialEngine.cs`

Signals directly observed:

- `OnEclipseStart`
- `OnEclipseEnd`
- `OnSunAngleChanged`
- `OnPlanetPhaseChanged`

Confirmed subscribers:

- `EclipseGameplaySystem` on `OnEclipseStart` and `OnEclipseEnd`
- `QuestManager` on `OnEclipseStart`

No source-confirmed subscribers were established in this pass for:

- `OnSunAngleChanged`
- `OnPlanetPhaseChanged`

## 5. Separate Modding Bus

`Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` is a separate typed event system.

Observed properties:

- typed channels per event type
- `List`-backed subscription storage
- try/catch isolation around handler dispatch
- resettable static channel registry

What it is not:

- not a `NativeQueue<T>` gameplay event bus
- not the same thing as the static gameplay buses listed above

## 6. Conformity Findings Against `AGENTS.md`

### 6.1 Architecture Drift

`AGENTS.md` requires event-bus backing by `NativeQueue<T>` with late flush semantics.
Current source-backed picture shows widespread direct static `Action` dispatch instead.

This is documented drift, not a guess.

### 6.2 String Payload Debt

Confirmed string-heavy surfaces include:

- `SaveEvents`
- `ScanEvents.OnEntryDiscovered`
- `AudioLogEvents`
- `NarrativeEvents.OnDiscoveryMade`

This does not automatically prove hot-path GC, but it is a documented risk surface and contradicts the spirit of the zero-alloc mandate.

### 6.3 Subscription Hygiene

Many subscribers do pair `+=` with `-=`.
That is positive, but this audit did not exhaustively prove leak safety for every event surface in the project.

## 7. What Was Removed From The Old Version

Removed as unsupported:

- invented files such as `ItemCollector.cs` and `TimeScaleManager`
- claims that specific UI or world systems were subscribed where current source scan did not prove it
- fake totals such as fully verified signal counts
- "ETA CODEX VERIFIED" language

## 8. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: no runtime cadence change
Correctness: improved documentation accuracy by replacing inferred topology with source-backed topology

## 9. Hot Path Impact

None. Markdown-only change.

## 10. Failure Modes

- some subscribers may still be missed if they use indirect hookup patterns not matched by the search pass
- runtime-only wiring from scenes/prefabs is outside this static-source pass
- modding/event surfaces outside the named buses remain underdocumented

## 11. Why This Version Was Kept

Kept because it narrows claims to what current source actually proves.
Rejected content: fabricated certainty, inferred class names, and unsupported publisher-to-subscriber chains.

STATUS: PENDING VERIFICATION
