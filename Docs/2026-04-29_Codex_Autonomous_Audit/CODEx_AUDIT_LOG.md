# CODEX Autonomous Audit Log

Date: 2026-04-29
Scope: Active branch technical audit, console stabilization, mandate compliance tracking.
Status: PENDING VERIFICATION

## Mandates Followed

- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `PHYS_Fluid_Incursion_Interior.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Current Console State

Latest Unity MCP console poll: `0 entries`.

Resolved in this pass:

- `SaveBinaryStorage.cs(742)`: removed dead legacy write body under constant indexed-save path.
- `SubmarineFluidDynamics.cs(2662)`: replaced obsolete `GetInstanceID()` hazard ID source with `GetEntityId()`/`EntityId.ToULong(...)`.
- `PersistentWorldRegistry.cs(396)`: removed unused `SectorEvictionDistanceSq`.
- `Editor/GCSentinel.cs(26)`: replaced obsolete `FindFirstObjectByType<T>()` with `FindAnyObjectByType<T>()`.
- `SuitHUDV4CanvasOverlay.cs`: removed invalid `CanvasGroup` mutation from `OnValidate`; moved hide pass to delayed editor-safe refresh.

Residual note:

- `MCP-FOR-UNITY` regex timeout remains a tooling-side issue when validating very large scripts. This is package-side, not first-party runtime code.

## Architecture Findings

### Registry / Ownership

- `SpatialAudioManager` still bypasses `GlobalRegistry.Audio` and keeps its own singleton path plus `DontDestroyOnLoad`.
- `IUIService` is still contested by multiple runtime owners:
  - `HectonSuitHUD_v4`
  - `HectonFabricatorUI`
  - `SuitHUDV4CanvasOverlay`

Evidence:

- `GlobalRegistry.RegisterAudioService(...)` exists, but first-party caller scan returned no registrations.
- `SpatialAudioManager` still holds `private static SpatialAudioManager s_Instance` and calls `DontDestroyOnLoad(gameObject)`.
- `IAudioService` currently appears only in:
  - `Core/GlobalRegistryContracts.cs`
  - `Core/GlobalRegistry.cs`
- First-party scan returned no `IAudioService` implementer and no caller outside `GlobalRegistry` itself for `RegisterAudioService(...)` / `UnregisterAudioService(...)`.
- Direct usage blast radius is already large:
  - `51` script files matched `SpatialAudioManager.Instance` or `SpatialAudioManager.TryGetInstance(...)`.
  - Call sites span gameplay, UI, audio log, weather, player movement, tools, underwater visuals, PDA, and module systems.
- Conclusion: migration to `GlobalRegistry.Audio` is not a one-file cleanup. It is a staged ownership refactor.
- Additional dead-slot evidence:
  - `GlobalRegistry.Audio` had `0` first-party read sites in the current scan.
  - The registry slot exists as contract surface, but not as an active dependency path.
- Live scene evidence:
  - `Suit_HUD_Canvas` currently contains `SuitHUDV4CanvasOverlay`, `HectonFabricatorUI`, and `PauseMenuController`.
  - `Suit_HUD_ProjectionSource` currently contains a second `SuitHUDV4CanvasOverlay`.
  - `SpatialAudioManager_Root` currently contains `SpatialAudioManager`.
  - `HectonSuitHUD_v4` was not found active in the current scene.
- Additional dead-slot evidence:
  - `GlobalRegistry.UI` had `3` first-party read sites in the current scan.
  - All `3` reads are the registrars themselves checking whether the slot is already occupied.
  - No downstream consumer of `GlobalRegistry.UI` was found.

### Event Bus Drift

- Event architecture is mixed rather than singular:
  - legacy static `Action` buses (`SaveEvents`, `InteractionEvents`, `PDAEvents`, `AudioLogEvents`, `PhysicsEventBus`)
  - synchronous typed `HectonEventBus` (`List`-backed immediate dispatch with try/catch isolation)
  - queued service-style path in `IInteractionSignalService`
- This does not match the mandate requirement for a single NativeQueue-backed publish/flush model.
- String payloads remain in save/audio-log/quest/notification buses.

Current scan snapshot:

- `64` script files matched `event Action` / `event Action<T>`.
- Multiple first-party buses still expose string payloads directly:
  - `SaveEvents`
  - `AudioLogEvents`
  - `QuestEvents`
  - `NarrativeEvents`
  - `AtlasSignalEvents`
- `HectonEventBus` exists, but current implementation is synchronous and `List`-backed, not `NativeQueue<T>` with late-frame flush.
- `IInteractionSignalService` is a separate queued path and therefore evidence of partial migration, not uniform compliance.
- `HectonEventBus` drift is deeper than dispatch timing:
  - it lives under `ModdingAPI`, but first-party systems use it directly
  - current implementation dispatches managed event classes, not blittable structs
  - current first-party scan found `19` call sites of `HectonEventBus.Publish(new ...Event(...))`
- That means first-party runtime is currently allocating managed event objects through a mod-facing bus in gameplay code.

### UI Zero-GC Drift

- Core UI infra is partially present:
  - `UI/TMP_TextRegistry.cs` exists
  - `UI/HectonTextNode.cs` exists
  - `UI/LabelSwapScheduler.cs` enforces the documented `18` labels-per-tick font swap cadence
  - `UI/FontStreamingManager.cs` consumes the registry for staged font swaps
- Therefore the problem is not absence of UI infra. The problem is partial adoption plus fallback behavior that weakens the mandate.
- Gameplay-facing UI still contains direct `.text =` mutation paths.
- Notable live examples:
  - `InteractionUI` (partially improved in this audit)
  - `PauseMenuController`
  - `PDADataLogTab`

Current scan snapshot:

- `26` script files matched direct `.text =`.
- The highest-value gameplay-facing offenders remain:
  - `UI/InteractionUI.cs`
  - `UI/PauseMenuController.cs`
  - `UI/PDADataLogTab.cs`
- Partial-compliance gaps inside the registry stack:
  - `TMP_TextRegistry.EnsureRegistered(...)` can still `AddComponent<HectonTextNode>()` at runtime.
  - `HectonTextNode` falls back to `gameObject.GetHashCode()` when no baked hierarchy hash exists.
  - This means the registry is real, but not yet purely bake-authored/deterministic for all TMP owners.

### Lifetime / Bootstrap Drift

- `47` script files matched `DontDestroyOnLoad(...)`.
- Some of these are bootstrap-owned and expected.
- `SpatialAudioManager` remains a non-bootstrap singleton/DDOL owner and is therefore off-mandate relative to the registry/bootstrap contract.
- `GameBootstrapper.InitializeUILayer()` currently contains only:
  - `No UI-layer GlobalRegistry adapter exists yet.`
  - `Existing menu/HUD ownership remains on scene-authored controllers.`
- `GameBootstrapper.InitializeEnvironmentLayer()` initializes physics/debris/environment/ocean services, but no canonical audio-service registration path is present there.
- Result: both `GlobalRegistry.UI` and `GlobalRegistry.Audio` currently behave as incomplete contracts rather than authoritative bootstrap-owned services.

## Visual Verification

- `GameView` screenshot after fixes: `Assets/Screenshots/screenshot-20260429-045217.png`
- `HUD_Render_Camera` screenshot after fixes: `Assets/Screenshots/screenshot-20260429-045229.png`
- Result: visor/HUD no longer reproduces the previous full-white frame in this captured state.

## Actions Performed In This Session

1. Restored compile lane blockers in `PersistentWorldRegistry.cs` and `SaveManager.cs`.
2. Verified visor white-screen regression is no longer reproducing as a full white frame via Unity MCP screenshots.
3. Established this audit log for ongoing findings and fixes.
4. Cleared active first-party console warnings/errors in `SaveBinaryStorage.cs`, `SubmarineFluidDynamics.cs`, `PersistentWorldRegistry.cs`, `Editor/GCSentinel.cs`, and `SuitHUDV4CanvasOverlay.cs`.
5. Re-polled Unity console after compile and captured a clean `0 entries` state.
6. Replaced direct `promptText.text = expandedPrompt` in `UI/InteractionUI.cs` with preallocated `char[]` staging plus `TMP_Text.SetCharArray(...)`.

## Partial Compliance Notes

### InteractionUI

- `InteractionUI` no longer writes prompt text through direct `TMP_Text.text = string`.
- `InteractionUI` now uses `TryGetComponent(...)` for concrete prompt probes (`BatteryCharger`, `BioReactor`, `StorageCrate`, `PickupItem`) instead of uncached `GetComponent<T>()`.
- The path is still not fully mandate-clean:
  - `LocalizationManager.ExpandText(...)` still returns `string`.
  - prompt composition still relies on string-based builders/formatting upstream.
  - interface probes still use `collider.GetComponent<IInteractable>()` and `collider.GetComponent<IBatteryTool>()` inside the hot path.
- Conclusion: writeback violation removed; prompt generation remains a separate zero-GC remediation target.

## Next Targets

1. Audit and reduce gameplay-facing `.text =` writes after `InteractionUI`, prioritizing `PauseMenuController` only if it can be isolated without menu regressions.
2. Audit direct static `Action` buses against the NativeQueue event-bus mandate.
3. Resolve ownership drift around `SpatialAudioManager` versus `GlobalRegistry.Audio`.
4. Re-run Unity console and visual capture after each batch; append results here.

## Update — 2026-04-29 / Compile Lane Follow-Up

### Active Console Slice Closed

- Fixed `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs(314)`:
  - cause: `in _volumes[index]` attempted to pass a `NativeArray` indexer temporary by reference.
  - fix: copied the moved entry to a local `HazardVolumeData movedVolume` before `UpdateSpatialEntry(...)`.
- Fixed `Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs(374/376/377)`:
  - cause: obsolete TMP API `enableWordWrapping`.
  - fix: migrated to `textWrappingMode = TextWrappingModes.NoWrap`.

### Unity Verification

- Unity refresh/compile completed after one domain-reload delay.
- Current Unity console snapshot after the above fixes: `0 entries`.

### Additional Confirmed Architecture Evidence

- `GameBootstrapper.InitializeUILayer()` still contains:
  - `No UI-layer GlobalRegistry adapter exists yet.`
  - `Existing menu/HUD ownership remains on scene-authored controllers.`
- `GlobalRegistry.Audio` remains a dead contract surface:
  - `IAudioService` exists in `Core/GlobalRegistryContracts.cs`.
  - `RegisterAudioService(...)` / `UnregisterAudioService(...)` exist in `Core/GlobalRegistry.cs`.
  - no first-party `GlobalRegistry.Audio` consumption was found in runtime gameplay code.
- `SpatialAudioManager` still owns itself via `DontDestroyOnLoad(gameObject)` and remains off the explicit bootstrap/registry path.
- `TMP_TextRegistry` remains only partially mandate-compliant:
  - it may still `AddComponent<HectonTextNode>()` at runtime.
  - `HectonTextNode` still falls back to `gameObject.GetHashCode()` when no baked hierarchy hash exists.
- `HectonEventBus` remains synchronous managed dispatch, not the mandated `NativeQueue<T>` main-thread flush model.

### Current State

- Compile lane: clean in current Unity session.
- HUD white-screen regression: not reproduced in the previously captured MCP screenshots.
- Architecture: still materially off-mandate in audio ownership, UI ownership, event-bus determinism, and TMP registry determinism.

### Updated Quantitative Scan

- `190` matches for `event Action` / `event Action<T>` across `Assets/_Project/Scripts`.
- `104` matches for direct `.text =` writes across `Assets/_Project/Scripts`.
- `65` matches for `DontDestroyOnLoad(...)` across `Assets/_Project/Scripts`.
- `20` matches for `HectonEventBus.Publish(...)` across `Assets/_Project/Scripts`.
- `98` matches for `SpatialAudioManager.Instance` / `SpatialAudioManager.TryGetInstance(...)`.
- `0` matches for `GlobalRegistry.Audio`.
- `3` matches for `GlobalRegistry.UI`, and all three are UI registrars rather than downstream consumers.

## Update — 2026-04-29 / HUD + Construction Follow-Up

### Hot-Path UI Cleanup

- `PDA/PDAMarkerHUDElement.cs` no longer writes marker title/distance via `TMP_Text.text` inside `Tick`.
- Added per-marker preallocated `char[]` staging buffers and switched writeback to `TMP_Text.SetCharArray(...)`.
- Existing string caches (`cachedTitle`, `cachedDistance`, `DistanceLabelCache`) were preserved; this was a writeback-path cleanup, not a behavioral rewrite.

### Construction Compile Drift Closed

- `Construction/HabitatGraphManager.cs` was missing `using Hecton8.Gameplay;`, so `BaseModule` could not resolve.
- The file also still targeted an old `LogisticsNetworkGraph` constructor signature.
- Fixed constructor call to current API:
  - from stale four-argument path with `LogisticsNetworkType`
  - to current `LogisticsNetworkGraph(nodeCapacity, edgeCapacity, consumerCapacity)`
- Replaced obsolete `moduleObject.GetInstanceID()` with `EntityId.ToULong(moduleObject.GetEntityId())`.

### Unity Verification

- Current Unity console snapshot after the above fixes: `0 entries`.
- `HabitatGraphManager.cs` validates clean via Unity script validation.
- `HazardZoneManager.cs` validates clean via Unity script validation.
- `PDAMarkerHUDElement.cs` compiles clean in Unity, but MCP validator still reports a false duplicate-signature error for `BuildDistanceLabelCache()`. Code inspection and project compile do not reproduce an actual duplicate method in that file.

## Update — 2026-04-29 / Event Bus Evidence Pass

### Direct Off-Mandate Examples

- `SaveEvents.cs` still exposes static `Action<string>` / `Action<string,string>` channels for slot names and error strings.
- `Quest/QuestEvents.cs` still exposes static `Action<string>` channels for quest IDs, despite the quest mandate requiring pre-baked hashed IDs and signal-driven evaluation.
- `AudioLog/AudioLogEvents.cs` still exposes static `Action<string>` channels for `logId`.
- `ModdingAPI/HectonEventBus.cs` remains a synchronous managed dispatcher rooted in `abstract class HectonEvent`, not a `NativeQueue<T>` late-flush path.

### Interpretation

- The project does not currently have one event architecture. It has at least three:
  - legacy static `Action` buses with managed/string payloads
  - synchronous managed `HectonEventBus`
  - isolated queue-based lanes in selected newer systems
- This is the core reason the event mandate is currently only partially real in the codebase.
