# 2026-05-01 Zero-GC / Jobs Continuation Delta

Mandates followed:
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `UI_Diegetic_Physical_Interfaces.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

## What Was Wrong

1. `LoreAcquiredEvent` was a managed `HectonEvent` class. Lore pickup/discovery paths allocated a managed event object before dispatching to quest, lore DB, and audio-log consumers.
2. `LogisticsPipeOverpressureLeakEvent` was a managed `HectonEvent` class. Pipe rupture allocated a managed event object even though the payload is fully blittable and no managed subscribers are present.
3. `PowerGrid.ResolveBatteryDispatch()` scheduled `ResolveBatteryDispatchJob` and immediately called `Complete()` in the same balance path. That violates the project jobs mandate and can cost more than the parallel work for small battery counts.
4. `WorldSpatialHashGrid` allocated a dead managed `SpatialHashEntryUnloadedEvent` during far-unload consumption. Static search found no subscribers or references.
5. `WorldSpatialHashGrid.TryGetNearestBioform()` and `TryGetNearestAggressiveBioform()` built `System.Predicate<Entry>` lambdas for AI spatial queries.
6. `OrganicDebrisProfile.ApplyRuntimeAuthoringVisibility()` called `GetComponentsInChildren<Collider>(true)` from the runtime `OnEnable()` path.
7. `SuitHUDV4CanvasOverlay.HideIncompleteRootImmediately()` allocated an `Image[]` via `GetComponentsInChildren<Image>(true)` during HUD bootstrap/enable.
8. `FaunaBrain.RefreshVoxelRouteRuntimeCacheFromAup()` scheduled `RehydrateVoxelRouteJob` for at most 16 route waypoints and immediately completed it, while holding a persistent NativeArray cache only for that synchronous copy.
9. `TetherManager.OnOriginShift()` scheduled `TranslateVisualPointsJob` per active tether and immediately completed it before uploading the visual rebase.
10. `HectonUIScaler` used Burst/NativeArray/JobHandle for a trivial linear UI layout, then polled/completed the job from `Tick()`.
11. `PlayerInventory.SortInventory()` and `RefreshDerivedMassAndSurvivalLoad()` scheduled bounded inventory jobs and immediately completed them, despite the same file already using `.Run()` for synchronous inventory kernels.
12. `PlayerInventory` still contained a dead `InventorySortJob` wrapper around `NativeSortExtension.Sort()` after the radix-sort path replaced it.
13. `WorldGenerativeGeologyIntegrationDirector`, `HectonWorldGenerator`, and `HectonVoxelEngine` had `OnDrawGizmosSelected()` methods compiled outside a top-level `UNITY_EDITOR` guard.
14. `ProximityColliderSystem.Tick()` and `PerformanceMonitor.Tick()` had development diagnostic logs reachable in release builds.
15. `PlayerToolManager` built interpolated debug strings for swap/spawn/despawn diagnostics before `LogToolDebug()` could check `toolDebugLogging`.
16. `PlayerToolManager`, `BeaconNetworkSystem`, and `CameraJuiceSystem` still had noncritical `Debug.*` branches reachable from gameplay/action paths in production builds.
17. `PDAAtlasSignalTab.Tick()` could route through `UpdateCountdownDisplay()` and write `_pulseTimerLabel.text = "—:—"` from the active PDA Atlas tab.
18. `AcousticEcholocationTranslator.TerminalBootSequence` built a pooled `StringBuilder`, called `ToString()`, and assigned `_consoleLabel.text` on every sonar boot sequence event.
19. `AudioCaptionOverlay` compared and assigned `slot.Label.text` for runtime caption requests, causing TMP string surface use in a player-facing caption path.
20. `HectonOSBootManager.StartSequence()` built the boot log through `StringBuilderPool.Get()`, `builder.ToString()`, `_consoleLabel.text`, `slotName.ToUpperInvariant()`, enum `.ToString().ToUpperInvariant()`, and float `.ToString(format)`.
21. `PDABarterTab.RefreshCards()` built card titles with `_sb.ToString()` and assigned `_cardTitles[i].text`, materializing a new managed string for every refreshed barter card.
22. `PDADeathMemoryDump` built the active death dump with `_dumpBuilder.ToString()` and assigned `_dumpLabel.text`; the clear path also used `_dumpLabel.text = string.Empty`.
23. `PDASpectrumTab` still used direct TMP `.text` assignments for procedural static labels, mode text, descriptions, and the shared `SetLabelText()` helper.
24. `PDAControlsRebindUI` used direct `.text` writes for row labels, binding labels, and status text. Its header hint, rebind status, ready status, action-not-found status, and reset hint paths also built managed strings through interpolation.
25. `PDAConstructionTab` used `_sb.ToString()` immediately before TMP writes for summary, directive, hint, and card body text. Its builder-state/action labels also used slot interpolation, and card body power used `powerRating.ToString("0")`.
26. `PauseControlsPanel` already owned `_statusBuilder`, but still materialized it with `_statusBuilder.ToString()` for normal status TMP writes.
27. `PDAControlsRebindUI` and `PauseControlsPanel` still resolved valid binding labels through `InputManager.GetBindingDisplayString(...)`, `InputManager.TryGetBindingDisplayStringSafe(... out string)`, or local `GetBindingDisplaySafe()` helpers, despite `InputManager.TryWriteBindingDisplayStringSafe(... char[])` already existing.
28. `PDAAtlasSignalTab` still used direct TMP `.text` assignments for procedural static labels and read/assigned `label.text` inside `SetLabelText()`.
29. First-party UI still had isolated direct TMP `.text` writes/reads in `BuilderStatusOverlay`, `RelayHUDRuntimeBootstrap`, `SubnauticaSystemsDebugUI`, and `SettingsPanel`.
30. `PhysicalInteractionHandler` read `_activeBehaviour.gameObject.name` when starting pocket pickup and heavy-carry interactions. That is a Unity native string surface in an interaction runtime path, and the value is only diagnostic.
31. `DiegeticPanelController.SetCursorVisible()` used `cursorTransform.gameObject.SetActive(...)` from the tick-driven hover/clear path. That can trigger hierarchy activation and canvas/renderer churn for a cursor visibility change.
32. `DiegeticPDAController.ApplyPresentationState()` used `tabletRoot.SetActive(openState)` from the PDA open-state Tick path. Closing/opening the diegetic PDA could activate/deactivate a full tablet hierarchy instead of changing presentation state.
33. `PauseControlsPanel.BuildDefaultRows()` created a `List<RebindRow>`, filled it through a helper, trimmed static literals, and called `ToArray()` even though the final data is a fixed 15-row array.
34. `UIAudioFeedback` classified buttons by reading `button.gameObject.name`, allocating a lowercase copy with `ToLowerInvariant()`, and always adding a new `EventTrigger.Entry` for hover registration. The teardown path removed hover listeners, but registration could still duplicate pointer-enter entries if called again.
35. `UIAudioFeedback.PlaySound()` computed pitch variation with `Random.Range(...)` and logged pitch, but `IAudioService.PlayStatic2D(...)` has no pitch parameter and `SpatialAudioManager.PlayStatic2D(...)` forces `source.pitch = 1f`. The setting was inert and added useless callback work.
36. `BaseIntegrityHUD` pushed base integrity and air-quality warnings by formatting a new notification string on every warning event, even though `NotificationEventPayload` already carries only a stable message hash.
37. `SettingsManager` still had unguarded `Debug.LogError(...)` interpolation inside settings apply `catch` branches. Release builds could evaluate exception-message formatting while applying quality, VSync, fullscreen, resolution, shadow distance, or texture quality.
38. `SaveStation` had noncritical runtime interaction warnings for missing slot config and busy save state compiled into player builds. The busy path used interpolation with `_saveSlot` even though the player already receives HUD feedback.
39. `PauseMenuController.SaveSlot(...)` logged save exceptions with interpolated slot/error text in the runtime save failure path, while the same path already drives status text and retry modal feedback.
40. `HUDSaveNotificationLink` claimed Zero-GC but converted `SaveEventPayload.SlotName` from `FixedString64Bytes` to `string` and rebuilt localized save notification strings on every save-completed/save-failed event.
41. The new `HUDSaveNotificationLink` cache was keyed by slot/event/language only. `LocalizationManager.GetOrFallback(...)` can also change output through transient glyph/corruption visual state, so the cache could retain stale save HUD copy after localization visual events.
42. `ToolHitUtility.ShowInfo(in FixedCharBuffer)` and `ShowWarning(in FixedCharBuffer)` were false zero-GC overloads: both called `messageBuffer.ToString()` before reaching `HUDNotification`. The no-notification fallback also formatted debug strings in player builds.

## What Changed

1. `LoreAcquiredEvent` is now an unmanaged readonly struct dispatched through `HectonEventBus.Publish(in payload)`.
2. Lore consumers now use `HectonUnmanagedEventHandler<T>` signatures with `in LoreAcquiredEvent`.
3. `LogisticsPipeOverpressureLeakEvent` is now an unmanaged readonly struct dispatched through `HectonEventBus.Publish(in leakEvent)`.
4. Battery dispatch no longer uses `Schedule()` followed by immediate `Complete()`. It now runs a direct indexed loop over the preallocated native buffers and reuses the same deterministic math in `ResolveBatteryDispatchRecord()`.
5. Removed dead `SpatialHashEntryUnloadedEvent` and the only `HectonEventBus.Publish(new SpatialHashEntryUnloadedEvent(...))` call.
6. Inlined the two bioform nearest-query filters into indexed loops. No delegate/predicate allocation remains in `WorldSpatialHashGrid`.
7. Added a serialized `cachedRuntimeColliders` cold cache to `OrganicDebrisProfile`; runtime visibility now disables cached colliders by index.
8. Added a preallocated `List<Image>` resolve buffer to `SuitHUDV4CanvasOverlay` and switched legacy radar image discovery to `GetComponentsInChildren<Image>(true, List<Image>)`.
9. `FaunaBrain` route rehydration now uses a bounded direct loop over the existing managed AUP/waypoint arrays and no longer owns a `FaunaRouteRehydrationCache`.
10. `TetherManager` origin-shift visual rebase now writes directly into the existing `NativeArray<float3>` with an indexed loop, then commits the upload.
11. `HectonUIScaler` manual linear layout now applies direct `RectTransform` anchored-position/size writes during bootstrap/SlowTick/editor rebuild; Burst/Jobs/NativeArray lifecycle was removed.
12. `PlayerInventory` sort and derived carry-total kernels now execute via `.Run()` instead of `Schedule()+Complete()`.
13. Removed the unused `InventorySortJob` struct from `PlayerInventory`.
14. Wrapped the remaining unguarded runtime `OnDrawGizmosSelected()` methods in `#if UNITY_EDITOR`.
15. Guarded the proximity retry warning and performance auto-report log behind `UNITY_EDITOR || DEVELOPMENT_BUILD`.
16. `PlayerToolManager.LogToolDebug()` is now compile-time conditional for editor/development builds, and its `Debug.Log` body is guarded. Release builds no longer evaluate the interpolated arguments.
17. `PlayerToolManager` direct warning/error logs in swap/spawn/inventory error branches are guarded behind `UNITY_EDITOR || DEVELOPMENT_BUILD`.
18. `BeaconNetworkSystem` verbose deployment logging now routes through a conditional guarded helper, so release builds do not format beacon deploy strings.
19. `CameraJuiceSystem.TriggerShake(null)` and `TransitionToBiome(null)` diagnostics are now editor/development-only; early-return behavior is unchanged.
20. `PDAAtlasSignalTab` no-signal countdown now writes the cached `—:—` payload through `TMP_Text.SetCharArray()` instead of `TMP_Text.text`.
21. `TerminalBootSequence` now owns a fixed `StringBuilder[192]` and writes directly with `TMP_Text.SetText(StringBuilder)`, removing `StringBuilder.ToString()` and `_consoleLabel.text`.
22. `AudioCaptionOverlay` now caches `LastCaptionText` per slot and writes captions through `TMP_Text.SetText(string)` only when the requested caption changes; slot initialization uses `SetText(string.Empty)`.
23. `HectonOSBootManager` now owns a fixed `StringBuilder[512]`, writes boot text through `TMP_Text.SetText(StringBuilder)`, appends slot names in-place as uppercase chars, resolves language tags through a switch, and appends boot numerics without formatted `.ToString()`.
24. `PDABarterTab` card title composition now writes the existing `StringBuilder` directly with `TMP_Text.SetText(StringBuilder)`.
25. `PDADeathMemoryDump` active dump rendering now writes `_dumpBuilder` directly with `TMP_Text.SetText(StringBuilder)` and clears through `SetText(string.Empty)`.
26. `PDASpectrumTab` procedural label writes now route through `TMP_Text.SetText(...)`; no direct `.text =` write remains in that file.
27. `PDAControlsRebindUI` row labels and binding labels now route through `SetText(...)`.
28. `PDAControlsRebindUI` dynamic header hint now uses an owned `StringBuilder[128]` and `TMP_Text.SetText(StringBuilder)`.
29. `PDAControlsRebindUI` rebind status, ready status, action-not-found status, no-rebindable status, failed-start status, and reset-hint composition now use an owned `StringBuilder[192]` and `TMP_Text.SetText(StringBuilder)`.
30. `PDAConstructionTab` summary, directive, hint, card body, builder-state, and slot action labels now reuse the existing `_sb` and write through `TMP_Text.SetText(StringBuilder)`.
31. `PDAConstructionTab` card power display now appends `Mathf.RoundToInt(data.powerRating)` instead of allocating formatted float strings.
32. `PauseControlsPanel` status TMP writes now use `SetStatus(System.Text.StringBuilder)` and `TMP_Text.SetText(StringBuilder)` where the payload is already in `_statusBuilder`.
33. `PDAControlsRebindUI` now owns a fixed `char[64]` binding display buffer and writes row binding labels through `InputManager.TryWriteBindingDisplayStringSafe(...)` + `TMP_Text.SetCharArray(...)`.
34. `PDAControlsRebindUI` selected-row status now appends binding display chars directly into `_statusBuilder` via `StringBuilder.Append(char[], int, int)`.
35. `PauseControlsPanel` now owns a fixed `char[64]` binding display buffer and uses it for row binding labels and selected-row status.
36. `PauseControlsPanel.TryResolveRowBinding(...)` no longer builds a display string on the valid path; success now returns action + binding index and leaves `resolutionMessage` empty.
37. `PDAControlsRebindUI` header hint and reset hint now append binding labels through the same char-buffer path; first-party UI no longer calls `InputManager.GetBindingDisplayString(...)`.
38. `PDAAtlasSignalTab` static/procedural label writes now use `TMP_Text.SetText(...)`; no direct `.text =` or `label.text` access remains in that file.
39. `BuilderStatusOverlay`, `RelayHUDRuntimeBootstrap`, `SubnauticaSystemsDebugUI`, and `SettingsPanel` now use `SetText(...)` for the previously detected direct TMP text sites.
40. `PhysicalInteractionHandler` now routes diagnostic target-name capture through a `UNITY_EDITOR`-conditional helper. Player builds no longer execute the `behaviour.name`/`gameObject.name` string read when an interaction begins.
41. `DiegeticPanelController` now caches cursor visibility targets when the cursor transform changes and toggles `CanvasGroup`, `Graphic`, `Renderer`, and `Collider` enabled state instead of calling `SetActive()` in the tick-driven cursor path. Initial hide now applies through an explicit `_cursorVisibilityInitialized` guard.
42. `DiegeticPDAController` now caches tablet `Renderer`, `Collider`, and `CanvasGroup` lists when the tablet root changes and toggles those components for open/closed presentation. `DiegeticPanelController.enabled` still controls the actual panel runtime.
43. `PauseControlsPanel.BuildDefaultRows()` now returns a direct fixed `RebindRow[]` initializer and no longer needs `System.Collections.Generic`, `AddRow(...)`, `List<RebindRow>`, trimming, or `ToArray()`.
44. `UIAudioFeedback` now classifies button names with `IndexOf(..., StringComparison.OrdinalIgnoreCase)` and reuses an existing `PointerEnter` entry when present. Registration removes the cached hover listener before adding it, preventing duplicate runtime callbacks on repeated registration.
45. `UIAudioFeedback.PlaySound()` no longer computes ignored pitch variation before 2D UI playback. The serialized pitch fields are retained as deprecated inspector state only, because removing them would churn scene/prefab serialized data without changing runtime behavior.
46. `BaseIntegrityHUD` now caches registered notification hashes by localized format and rounded percent bucket. Repeated integrity/air warnings publish a registered `uint` hash through `NotificationEvents` instead of rebuilding the message string.
47. `SettingsManager` settings-apply exception diagnostics now route through `[System.Diagnostics.Conditional("UNITY_EDITOR")]` and `[System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]` helpers. Release builds keep the `return false` failure behavior without compiling the diagnostic call arguments.
48. `SaveStation` now keeps its HUD warning/info feedback in release, but compiles the noncritical `Debug.LogWarning(...)` diagnostics only for editor/development builds. The critical missing `SaveManager` init error remains visible.
49. `PauseMenuController` save exception diagnostics now route through a compile-time conditional `LogSaveSlotFailed(...)` helper. Player-facing save status and retry modal behavior are unchanged.
50. `HUDSaveNotificationLink` now uses a fixed `SaveNotificationCacheEntry[8]` keyed by slot `FixedString64Bytes`, event type, and current language hash. Managed message construction remains only on bounded cache miss.
51. `HUDSaveNotificationLink` now listens to `LocalizationEvents` language and corruption-visual changes and clears its fixed cache with an 8-element loop. OnDisable unregisters from SaveEvents and both LocalizationEvents lanes, then clears retained message references.
52. `HUDNotification` now exposes `ShowInfo/ShowWarning/ShowCritical(in FixedCharBuffer)` overloads and stores fixed-buffer notifications in a bounded `FixedBufferMessageCacheEntry[9]` plus `char[4608]` backing store. The normal uint notification queue is reused.
53. `HUDNotification.TryWriteDisplayMessage(...)` now writes fixed-buffer and registered-string messages through `LocalizationManager.TryApplyHullStressCorruptionIfNeeded(ReadOnlySpan<char>, char[], out int)`, avoiding the previous string-return corruption path for notification display.
54. `ToolHitUtility` fixed-buffer overloads now pass buffers directly to `HUDNotification`. Debug fallback logging is compile-time conditional for editor/development builds, so release builds do not evaluate `FixedCharBuffer.ToString()`.

## Static Evidence

Commands used:

```text
rg -n "PhysicsEvents\.OnImpact|OnImpact\s*\+=|OnImpact\s*-=" Assets/_Project/Scripts -g "*.cs"
rg -n "new LoreAcquiredEvent|Publish\(new LoreAcquiredEvent|LoreAcquiredEvent : HectonEvent" Assets/_Project/Scripts -g "*.cs"
rg -n "new LogisticsPipeOverpressureLeakEvent|LogisticsPipeOverpressureLeakEvent\s*:\s*HectonEvent|class\s+LogisticsPipeOverpressureLeakEvent" Assets/_Project/Scripts -g "*.cs"
rg -n "ResolveBatteryDispatchJob|dispatchHandle|Schedule\(batteryCount|BatteryParallelBatchSize" Assets/_Project/Scripts/PowerGrid.cs
rg -n "TryGetNearestMatch|System\.Predicate<Entry>|entry =>|SpatialHashEntryUnloadedEvent|Hecton8\.Modding|HectonEventBus" Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs
rg -n "cachedRuntimeColliders|GetComponentsInChildren<Collider>" Assets/_Project/Scripts/Gameplay/DebrisManager.cs
rg -n "s_imageResolveBuffer|GetComponentsInChildren<Image>" Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs
rg -n "_voxelRouteRehydrationCache|EnsureVoxelRouteRehydrationCache|DisposeVoxelRouteRehydrationCache|RehydrateVoxelRouteJob|Unity\.Jobs|Unity\.Burst|BurstCompile|IJobParallelFor|JobHandle|Schedule\(" Assets/_Project/Scripts/Fauna/FaunaBrain.cs Assets/_Project/Scripts/TetherManager.cs
rg -n "ScheduleManualLinearLayout|CompleteManualLinearLayoutIfReady|EnsureManualLayoutCapacity|DisposeManualLayoutBuffers|_manualLayout|LinearLayoutJob|NativeMemorySentinel|Unity\.Burst|Unity\.Collections|Unity\.Jobs|Unity\.Mathematics|float2|JobHandle|NativeArray|BurstCompile|IJob|Burst-generated" Assets/_Project/Scripts/UI/HectonUIScaler.cs
rg -n "InventorySortJob|JobHandle|\.Schedule\(|\.Complete\(" Assets/_Project/Scripts/PlayerInventory.cs
pwsh scan: every `void OnDrawGizmos*` in `Assets/_Project/Scripts` must have active `UNITY_EDITOR` preprocessor scope.
rg -n "_nextPlayerResolveWarningTime|ProximityColliderSystem\] playerTransform|GetReport\(\)|enableAutoReporting" Assets/_Project/Scripts/ProximityColliderSystem.cs Assets/_Project/Scripts/PerformanceMonitor.cs
rg -n "LogToolDebug\(|Debug\.Log|Debug\.LogWarning|Debug\.LogError|Conditional\(" Assets/_Project/Scripts/PlayerToolManager.cs
rg -n "Debug\.Log|LogBeaconDeployed|Conditional\(" Assets/_Project/Scripts/BeaconNetworkSystem.cs
rg -n "TriggerShake called with null profile|TransitionToBiome called with null biome|UNITY_EDITOR \|\| DEVELOPMENT_BUILD" Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs
rg -n "UpdateCountdownDisplay|_pulseTimerLabel\.text|PulseTimerEmptyChars|SetCharArray" Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs
rg -n -- "\.text\s*=|label\.text|SetText\(" Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs
rg -n --glob "*.cs" --glob "!Assets/_Project/Scripts/UI/Editor/**" "\.text\s*=|\.text\s*!=|\.text\s*==" Assets/_Project/Scripts/UI Assets/_Project/Scripts/Interaction Assets/_Project/Scripts/PDA
rg -n -- "\.text\s*=|BuildSequenceText\(|StringBuilderPool|LastCaptionText|SetText\(_sequenceBuilder\)" Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs
rg -n -- "\.text\s*=|BuildSequenceText\(|StringBuilderPool|ToString\(|ToUpperInvariant|SetText\(_sequenceBuilder\)|AppendLanguageTag|AppendFixedOne" Assets/_Project/Scripts/UI/HectonOSBootManager.cs
rg -n -- "\.text\s*=|_sb\.ToString\(\)|_dumpLabel\.text|_cardTitles\[i\]\.text" Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDASpectrumTab.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n --pcre2 "\$\"|BuildResetHintText|SetStatus\(\$|\.text\s*=|_statusBuilder|SetText\(_statusBuilder\)" Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n -- "SetText\(_sb\)|SetText\(_dumpBuilder\)|SetText\(_headerHintBuilder\)|StringBuilder\[128\]|StringBuilder\[192\]" Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n -- "_sb\.ToString\(\)|powerRating\.ToString|DescribeBuilderState|BuildCardBody|SetText\(_sb\.ToString\(\)|SetText\(\$|return \$\"ASSIGNED" Assets/_Project/Scripts/UI/PDAConstructionTab.cs
rg -n -- "SetSlotActionLabel|SetText\(_sb\)|AppendBuilderState|WriteCardBody" Assets/_Project/Scripts/UI/PDAConstructionTab.cs
rg -n -- "_statusBuilder\.ToString\(\)|resolutionMessage = \$|SetStatus\(\$|\.text\s*=" Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n -- "SetStatus\(_statusBuilder|SetStatus\(System\.Text\.StringBuilder|ModalWindow currently requires" Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n -- "GetBindingDisplaySafe|TryGetBindingDisplayStringSafe|TryWriteBindingDisplayStringSafe|_bindingDisplayBuffer|SetCharArray\(_bindingDisplayBuffer|Append\(_bindingDisplayBuffer" Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n -- "GetBindingDisplayString\(" Assets/_Project/Scripts/UI Assets/_Project/Scripts/PDA Assets/_Project/Scripts/Input/InputManager.cs
Select-String -Path Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs -Pattern "gameObject\.name|CacheDebugTargetName|_debugTargetName"
Select-String -Path Assets/_Project/Scripts/UI/DiegeticPanelController.cs -Pattern "SetActive\(|ResolveCursorVisibilityTargets|_cursorVisibilityInitialized|cursorTransform\.gameObject"
Select-String -Path Assets/_Project/Scripts/UI/DiegeticPDAController.cs -Pattern "SetActive\(|RebuildTabletVisibilityCache|SetTabletVisible|_tabletVisibilityInitialized"
pwsh scan: `SetActive\(` under first-party UI/Interaction/PDA excluding UI/Editor.
Select-String -Path Assets/_Project/Scripts/UI/PauseControlsPanel.cs -Pattern "System.Collections.Generic|List<|ToArray\(|AddRow\(|BuildDefaultRows"
Select-String -Path Assets/_Project/Scripts/UI/UIAudioFeedback.cs -Pattern "gameObject\.name|ToLowerInvariant|GetOrCreatePointerEnterEntry|ContainsOrdinalIgnoreCase|COLD ALLOC: EventTrigger.Entry|button.name|GetComponent<EventTrigger>"
Select-String -Path Assets/_Project/Scripts/UI/UIAudioFeedback.cs -Pattern "Random\.Range|float pitch|Pitch:|PlayStatic2D\(|Deprecated inspector state|enablePitchVariation|pitchVariation"
Select-String -Path Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs,Assets/_Project/Scripts/UI/NotificationEvents.cs -Pattern "PushWarning\(string\.Format|PushInfo\(string\.Format|string\.Format|PushRegistered|GetPercentNotificationHash|PercentMessageCacheSize|RegisterMessage\(message\)|ComputeMessageHash\(format\)"
Select-String -Path Assets/_Project/Scripts/UI/SettingsManager.cs -Pattern "Debug\.Log|Debug\.LogWarning|Debug\.LogError|LogApply.*Failed|Conditional\(\"UNITY_EDITOR\"\)"
Select-String -Path Assets/_Project/Scripts/Interaction/SaveStation.cs -Pattern "Debug\.Log|Debug\.LogWarning|Debug\.LogError|UNITY_EDITOR|DEVELOPMENT_BUILD|Save skipped"
Select-String -Path Assets/_Project/Scripts/UI/PauseMenuController.cs -Pattern "Debug\.LogError|LogSaveSlotFailed|Conditional\(\"UNITY_EDITOR\"\)|Save failed for"
Select-String -Path Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs -Pattern "MessageCacheCapacity|SaveNotificationCacheEntry|ResolveCachedMessage|SlotName\.ToString|Substring|string\.Concat|ILocalization|RegisterLanguageListener|UnregisterLanguageListener|RegisterCorruption|UnregisterCorruption|ClearMessageCache|COLD ALLOC"
Select-String -Path Assets/_Project/Scripts/HUDNotification.cs,Assets/_Project/Scripts/ToolHitUtility.cs -Pattern "FixedBufferMessage|ShowInfo\(in FixedCharBuffer|ShowWarning\(in FixedCharBuffer|TryApplyHullStressCorruptionIfNeeded|Conditional|ToString\(\)|Debug\.Log"
rg -n "void SetText\(StringBuilder" Library/PackageCache Packages "Assets/TextMesh Pro"
dotnet build Hecton8.Core.csproj --no-restore -v:minimal
dotnet build-server shutdown; dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal -m:1 /p:UseSharedCompilation=false
git diff --check -- Assets/_Project/Scripts/PlayerToolManager.cs Assets/_Project/Scripts/BeaconNetworkSystem.cs Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs Assets/_Project/Scripts/UI/DiegeticPDAController.cs Assets/_Project/Scripts/UI/DiegeticPanelController.cs Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs Assets/_Project/Scripts/UI/HectonOSBootManager.cs Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDASpectrumTab.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs Assets/_Project/Scripts/UI/PDAConstructionTab.cs Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n --pcre2 "\s+$" Docs/Reports/2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md
git status --short -- Docs/Reports/2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md
```

Current static result:
- No `PhysicsEvents.OnImpact` references exist on disk.
- No managed `LoreAcquiredEvent` publish remains.
- No managed `LogisticsPipeOverpressureLeakEvent` publish remains.
- No battery dispatch `Schedule(batteryCount)` / immediate `Complete()` remains in `PowerGrid.cs`.
- No `WorldSpatialHashGrid` predicate helper/lambda/dead event bridge remains.
- `DebrisManager.cs` still uses `GetComponentsInChildren<Collider>(true)` only in `RebuildCache()` cold-cache construction, not in `ApplyRuntimeAuthoringVisibility()`.
- `SuitHUDV4CanvasOverlay.cs` uses the nonalloc `GetComponentsInChildren<Image>(true, List<Image>)` overload for the legacy radar image scan.
- `FaunaBrain.cs` and `TetherManager.cs` no longer contain the removed one-shot route/tether jobs or their Burst/Jobs usings.
- `HectonUIScaler.cs` no longer contains UI manual-layout Jobs/Burst/NativeArray plumbing.
- `PlayerInventory.cs` no longer contains `JobHandle`, `Schedule()`, `Complete()`, or the unused `InventorySortJob`.
- No unguarded `OnDrawGizmos` / `OnDrawGizmosSelected` method remains under `Assets/_Project/Scripts` outside `Editor` folders.
- `ProximityColliderSystem` retry warning and `PerformanceMonitor` auto-report log are now compiled only for editor/development builds.
- `PlayerToolManager` debug-only swap/spawn logging is removed from release call sites via `System.Diagnostics.Conditional` and guarded `Debug.*` bodies.
- `BeaconNetworkSystem` verbose deployment logging is removed from release call sites via `System.Diagnostics.Conditional`.
- `CameraJuiceSystem` null-profile/null-biome diagnostics are now guarded by `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `PDAAtlasSignalTab` no-signal countdown hot path no longer contains `_pulseTimerLabel.text`.
- `PDAAtlasSignalTab` no longer contains direct `.text =` assignments or `label.text` access.
- Static scan now finds no direct `.text =`, `.text !=`, or `.text ==` matches under `Assets/_Project/Scripts/UI`, `Assets/_Project/Scripts/Interaction`, and `Assets/_Project/Scripts/PDA` excluding `UI/Editor`.
- TMP package source confirms `TMP_Text.SetText(StringBuilder)` exists.
- `AcousticEcholocationTranslator` no longer uses `StringBuilderPool` for the terminal boot sequence and no longer assigns `_consoleLabel.text` or `slot.Label.text`.
- Remaining known debt: `AcousticEcholocationTranslator.ShowClassification()` still uses `_lineBuilder.ToString()` through `ResolveStressReactiveText()` and then writes `_headerLabel.text` / `_classificationLabel.text`. This was not changed because the stress-localization path currently accepts `string` only.
- `HectonOSBootManager` no longer contains `_consoleLabel.text`, `StringBuilderPool`, `.ToString()`, or string `ToUpperInvariant()` in the boot sequence formatter. The only `ToUpperInvariant` static match is `char.ToUpperInvariant()` inside the in-place slot append helper.
- `PDABarterTab` no longer assigns TMP `.text` or calls `_sb.ToString()` for refreshed card titles.
- `PDADeathMemoryDump` no longer assigns `_dumpLabel.text`; active dump display uses `SetText(_dumpBuilder)`.
- `PDASpectrumTab` no longer contains `.text =` assignments.
- `PDAControlsRebindUI` no longer uses `.text =` for row labels, binding labels, status text, or the dynamic header hint.
- `PDAControlsRebindUI` no longer contains status-path interpolations for submit/rebind/reset/error display. Remaining `$"` hits in that file are cold generated object names or hierarchy lookup names.
- `PDAConstructionTab` no longer contains `_sb.ToString()`, `powerRating.ToString("0")`, `BuildCardBody()`, `DescribeBuilderState()`, slot-action `SetText($"...")`, or `return $"ASSIGNED..."` in the patched construction tab paths.
- `PauseControlsPanel` no longer calls `_statusBuilder.ToString()` for normal status TMP writes. Current remaining `_statusBuilder.ToString()` is the ModalWindow dialog bridge, not a TMP status write.
- `PDAControlsRebindUI` and `PauseControlsPanel` no longer contain `GetBindingDisplaySafe` or `TryGetBindingDisplayStringSafe` references. Valid binding display writes now use `TryWriteBindingDisplayStringSafe`.
- No first-party UI/PDA file currently calls `InputManager.GetBindingDisplayString(...)`; the remaining matches are inside `InputManager.cs` itself.
- `PhysicalInteractionHandler.cs` no longer contains `gameObject.name`; diagnostic target capture is isolated behind `CacheDebugTargetName()` compiled by `System.Diagnostics.Conditional("UNITY_EDITOR")`.
- `DiegeticPanelController.cs` no longer contains `SetActive(` or `cursorTransform.gameObject`; cursor visibility is now component-enable/CanvasGroup based.
- `DiegeticPDAController.cs` no longer contains `SetActive(`; tablet presentation is now component-enable/CanvasGroup based.
- Broad first-party UI/Interaction/PDA scan for `SetActive(` now returns only methods named `SetActive(...)` in `PDATabButton` and `PDAInventoryFilterButton`, not `GameObject.SetActive(...)` call sites.
- `PauseControlsPanel.cs` no longer contains `System.Collections.Generic`, `List<`, `ToArray()`, or `AddRow(...)`; its default controls rows are direct fixed-array data.
- `UIAudioFeedback.cs` no longer contains `gameObject.name`, `ToLowerInvariant`, or direct `GetComponent<EventTrigger>` in the registration path. Pointer-enter hover entries are reused by `GetOrCreatePointerEnterEntry(...)`.
- `UIAudioFeedback.PlaySound()` no longer contains `Random.Range`, local `float pitch`, or a `Pitch:` debug payload. The remaining `enablePitchVariation` and `pitchVariation` matches are deprecated serialized fields only.
- `BaseIntegrityHUD.cs` no longer contains direct `PushWarning(string.Format(...))` or `PushInfo(string.Format(...))` notification dispatch. Its remaining `string.Format` is isolated in `GetPercentNotificationHash(...)` and only runs on cache miss for a localized template/percent bucket.
- `NotificationEvents.cs` now has internal registered-hash publish methods that preserve queue capacity, reentrant next-frame routing, and message-registry validation.
- `SettingsManager.cs` settings-apply `catch` branches now call `LogApplyQualityLevelFailed`, `LogApplyVSyncFailed`, `LogApplyFullscreenFailed`, `LogApplyResolutionFailed`, `LogApplyShadowDistanceFailed`, and `LogApplyTextureQualityFailed`; those helpers are compile-time conditional for editor/development builds.
- `SaveStation.cs` noncritical slot-not-configured and busy-save warnings are now guarded behind `UNITY_EDITOR || DEVELOPMENT_BUILD`. The critical missing `SaveManager` error remains unguarded by design.
- `PauseMenuController.cs` save exception logging now calls `LogSaveSlotFailed(...)`, which is compile-time conditional for editor/development builds. The remaining matched `Debug.LogError` in that file is editor-only.
- `HUDSaveNotificationLink.cs` now resolves save notification messages through a bounded cache. The remaining `SlotName.ToString()`, `Substring`, and `string.Concat` matches are cache-miss construction paths, not every-event paths.
- `HUDSaveNotificationLink.cs` now implements `ILocalizationLanguageChangedListener` and `ILocalizationCorruptionVisualStateListener`, registers/unregisters with `LocalizationEvents`, and clears the fixed cache on localization state changes.
- `HUDNotification.cs` now has fixed-buffer notification overloads, a bounded fixed-buffer message cache, and display writes through the zero-GC hull-stress corruption API.
- `ToolHitUtility.cs` fixed-buffer paths no longer call `messageBuffer.ToString()` before HUD dispatch. Remaining `ToString()` calls are inside editor/development-only conditional debug fallback helpers.
- Local `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` previously completed with `0 Warning(s)` and `0 Error(s)`.
- After the latest edits, full dependency build hit `MSB4166` child-node termination without C# diagnostics; a later diagnostic build left compiler processes alive and was terminated. After `dotnet build-server shutdown`, rerun as `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal -m:1 /p:UseSharedCompilation=false` completed with `0 Warning(s)` and `0 Error(s)`.
- `Docs/Reports/2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md` is currently untracked in Git (`??`), so its whitespace check was performed with `rg -n --pcre2 "\s+$"` instead of relying on `git diff --check`.

## Verification State

Unity/MCP verification is blocked:
- `mcpforunity://instances` fails during HTTP handshake against `127.0.0.1:8088/mcp`.
- Direct HTTP POST to `http://127.0.0.1:8088/mcp` currently returns `Unable to connect to the remote server`.
- Local process scan currently sees `Unity.exe`, `Unity.ILPP.*`, `UnityPackageManager`, and licensing/crash helper processes.
- MCP is still unreachable despite the Unity process being alive, so console/screenshot/GCMonitor evidence is absent.
- Local `dotnet build Assembly-CSharp.csproj` is not a valid substitute: it restores projects, then fails on missing Unity-generated/third-party DLLs under `Temp/bin/Debug` before compiling the changed project code.

Editor.log facts:
- Older `PhysicsEvents.OnImpact` errors are stale relative to current source: disk files now contain `PhysicsEvents.Register/Unregister`.
- A later `McpBridgeAutoStartOnce.cs` compile error referenced a file that no longer exists on disk and no longer appears in `git status`.

Status: `PENDING VERIFICATION`. A fresh Unity compile is still required before declaring the project compile-clean.

## Regression Model

- CPU: expected no runtime CPU regression; release builds skip newly conditional logging calls and arguments.
- GC: expected reduction in release event-path allocations for tool swapping, beacon deployment diagnostics, camera juice invalid calls, PDA Atlas countdown empty-state writes, binding display writes, direct TMP text surfaces, physical-interaction diagnostic target-name capture, diegetic cursor hierarchy activation, PDA tablet hierarchy activation, pause-controls default row construction, UI audio button registration, ignored UI audio pitch variation, repeated base-integrity notification formatting, settings-apply exception diagnostics, save-station noncritical warnings, pause-menu save exception diagnostics, repeated save HUD notification message construction, and fixed-buffer tool telemetry HUD notifications. Measured proof absent because MCP/GCMonitor is unreachable.
- Memory: one new static cached `char[]` in `PDAAtlasSignalTab`, one owned `StringBuilder[128]`, one owned `StringBuilder[192]`, and one owned `char[64]` in `PDAControlsRebindUI`; one owned `char[64]` in `PauseControlsPanel`; one owned `FixedBufferMessageCacheEntry[9]` plus `char[4608]` in `HUDNotification`. Fixed caps, no growth path under expected strings.
- Cadence: no tick registration/cadence changes in this pass.
- Correctness: behavior-preserving except release builds no longer emit noncritical debug logs. Error/early-return behavior remains.
- Failure modes: if a production-only issue relied on these logs, diagnostics are reduced; editor/development builds still show them.
- UI residual risk: sonar classification overlay still needs a string-free stress-localization path before it can be called fully zero-GC.
- UI residual risk: `PDADeathMemoryDump` still builds cold line-library entries with `_dumpLineLibrary[i] = _dumpBuilder.ToString()` during cache generation, not during active reveal writes.
- UI residual risk: `PDAControlsRebindUI` still uses serialized/fallback hint strings for unresolved bindings, but valid row/header/reset/status binding labels no longer use managed binding-display strings.
- UI residual risk: `PDAConstructionTab` still sends dynamic `string` payloads into `HUDNotification` because that API currently accepts `string`; a full fix needs a notification text-buffer overload or notification key/payload event.
- UI residual risk: `PauseControlsPanel` still has `ModalWindow.Show(...)` requiring a managed dialog string and `TryResolveRowBinding(... out string resolutionMessage)` building two failure strings with interpolation. Valid row binding labels and selected status binding labels no longer use managed binding-display strings.
- UI residual risk: `BaseIntegrityHUD` now rounds warning percentages to integer buckets. This removes repeated formatting churn but requires runtime verification that localized copy still reads correctly at threshold boundaries.
- UI residual risk: `HUDSaveNotificationLink` still uses string-only `HUDNotification` on cache miss. The cache now invalidates on language and localization corruption visual events, but a full zero-GC path still requires a notification payload API that accepts pre-registered hashes or fixed buffers directly.
- UI residual risk: `HUDNotification` fixed-buffer cache is keyed by FNV hash and holds active-plus-queued capacity. A theoretical hash collision can display the cached fixed-buffer payload for the colliding hash; measured risk is low but runtime soak is still required.
- UI residual risk: `DiegeticPanelController` cursor visibility now requires at least one `CanvasGroup`, `Graphic`, `Renderer`, or `Collider` under `cursorTransform` to visibly hide/show the cursor. Static code cannot prove the prefab has one while MCP is unreachable.
- UI residual risk: `DiegeticPDAController` no longer disables the whole tablet root hierarchy. If hidden child scripts depended on `OnDisable()` from the old `SetActive(false)`, that behavior changed and needs prefab/runtime verification.
- UI residual risk: `UIAudioFeedback` still infers button category from object names during cold registration. That heuristic should eventually be replaced by explicit serialized button role metadata if UI naming changes.
- UI residual risk: `UIAudioFeedback` still exposes deprecated serialized pitch fields for scene compatibility. A later serialized migration can remove them after prefab/scene owners confirm no inspector workflow depends on them.
- Interaction residual risk: `PhysicalInteractionHandler` still resolves some components in interaction-start paths. Those are not per-frame tick calls, but should be revisited if interaction spam profiling shows spikes.

## Remaining Risk

`LogisticsNetworkGraph` still has real synchronous completion points:
- `CompleteEvaluation()`
- `CompleteNodeStatePublish()`

These are not safe to remove as a one-line fix. The current graph/power flow reads results immediately after scheduling, so a proper fix needs a two-phase state machine: schedule on SlowTick start, consume in a later end-of-frame or next-SlowTick window, and keep previous-state output available while the new graph evaluation is pending.
