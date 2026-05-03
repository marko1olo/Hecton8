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
rg -n -- "\.text\s*=|BuildSequenceText\(|StringBuilderPool|LastCaptionText|SetText\(_sequenceBuilder\)" Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs
rg -n -- "\.text\s*=|BuildSequenceText\(|StringBuilderPool|ToString\(|ToUpperInvariant|SetText\(_sequenceBuilder\)|AppendLanguageTag|AppendFixedOne" Assets/_Project/Scripts/UI/HectonOSBootManager.cs
rg -n -- "\.text\s*=|_sb\.ToString\(\)|_dumpLabel\.text|_cardTitles\[i\]\.text" Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDASpectrumTab.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n --pcre2 "\$\"|BuildResetHintText|SetStatus\(\$|\.text\s*=|_statusBuilder|SetText\(_statusBuilder\)" Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n -- "SetText\(_sb\)|SetText\(_dumpBuilder\)|SetText\(_headerHintBuilder\)|StringBuilder\[128\]|StringBuilder\[192\]" Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs
rg -n -- "_sb\.ToString\(\)|powerRating\.ToString|DescribeBuilderState|BuildCardBody|SetText\(_sb\.ToString\(\)|SetText\(\$|return \$\"ASSIGNED" Assets/_Project/Scripts/UI/PDAConstructionTab.cs
rg -n -- "SetSlotActionLabel|SetText\(_sb\)|AppendBuilderState|WriteCardBody" Assets/_Project/Scripts/UI/PDAConstructionTab.cs
rg -n -- "_statusBuilder\.ToString\(\)|resolutionMessage = \$|SetStatus\(\$|\.text\s*=" Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n -- "SetStatus\(_statusBuilder|SetStatus\(System\.Text\.StringBuilder|ModalWindow currently requires" Assets/_Project/Scripts/UI/PauseControlsPanel.cs
rg -n "void SetText\(StringBuilder" Library/PackageCache Packages "Assets/TextMesh Pro"
git diff --check -- Assets/_Project/Scripts/PlayerToolManager.cs Assets/_Project/Scripts/BeaconNetworkSystem.cs Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs Assets/_Project/Scripts/UI/HectonOSBootManager.cs Assets/_Project/Scripts/UI/PDABarterTab.cs Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs Assets/_Project/Scripts/UI/PDASpectrumTab.cs Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs Assets/_Project/Scripts/UI/PDAConstructionTab.cs Assets/_Project/Scripts/UI/PauseControlsPanel.cs
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
- `Docs/Reports/2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md` is currently untracked in Git (`??`), so its whitespace check was performed with `rg -n --pcre2 "\s+$"` instead of relying on `git diff --check`.

## Verification State

Unity/MCP verification is blocked:
- `mcpforunity://instances` fails during HTTP handshake against `127.0.0.1:8088/mcp`.
- Local process scan currently sees `Unity.exe`, `Unity.ILPP.*`, `UnityPackageManager`, and licensing/crash helper processes.
- MCP is still unreachable despite the Unity process being alive, so console/screenshot/GCMonitor evidence is absent.
- Local `dotnet build Assembly-CSharp.csproj` is not a valid substitute: it restores projects, then fails on missing Unity-generated/third-party DLLs under `Temp/bin/Debug` before compiling the changed project code.

Editor.log facts:
- Older `PhysicsEvents.OnImpact` errors are stale relative to current source: disk files now contain `PhysicsEvents.Register/Unregister`.
- A later `McpBridgeAutoStartOnce.cs` compile error referenced a file that no longer exists on disk and no longer appears in `git status`.

Status: `PENDING VERIFICATION`. A fresh Unity compile is still required before declaring the project compile-clean.

## Regression Model

- CPU: expected no runtime CPU regression; release builds skip newly conditional logging calls and arguments.
- GC: expected reduction in release event-path allocations for tool swapping, beacon deployment diagnostics, camera juice invalid calls, and PDA Atlas countdown empty-state writes. Measured proof absent because MCP/GCMonitor is unreachable.
- Memory: one new static cached `char[]` in `PDAAtlasSignalTab`, one owned `StringBuilder[128]`, and one owned `StringBuilder[192]` in `PDAControlsRebindUI`; fixed caps, no growth path under expected strings.
- Cadence: no tick registration/cadence changes in this pass.
- Correctness: behavior-preserving except release builds no longer emit noncritical debug logs. Error/early-return behavior remains.
- Failure modes: if a production-only issue relied on these logs, diagnostics are reduced; editor/development builds still show them.
- UI residual risk: sonar classification overlay still needs a string-free stress-localization path before it can be called fully zero-GC.
- UI residual risk: `PDADeathMemoryDump` still builds cold line-library entries with `_dumpLineLibrary[i] = _dumpBuilder.ToString()` during cache generation, not during active reveal writes.
- UI residual risk: `PDAControlsRebindUI` still depends on upstream `InputManager.GetBindingDisplayString(...)` / `TryGetBindingDisplayStringSafe(...)` returning managed binding-display strings. This pass removed local status/header interpolation and direct TMP `.text` writes, not the full Input System display-string allocation chain.
- UI residual risk: `PDAConstructionTab` still sends dynamic `string` payloads into `HUDNotification` because that API currently accepts `string`; a full fix needs a notification text-buffer overload or notification key/payload event.
- UI residual risk: `PauseControlsPanel` still has `ModalWindow.Show(...)` requiring a managed dialog string and `TryResolveRowBinding(... out string resolutionMessage)` building two failure strings with interpolation.

## Remaining Risk

`LogisticsNetworkGraph` still has real synchronous completion points:
- `CompleteEvaluation()`
- `CompleteNodeStatePublish()`

These are not safe to remove as a one-line fix. The current graph/power flow reads results immediately after scheduling, so a proper fix needs a two-phase state machine: schedule on SlowTick start, consume in a later end-of-frame or next-SlowTick window, and keep previous-state output available while the new graph evaluation is pending.
