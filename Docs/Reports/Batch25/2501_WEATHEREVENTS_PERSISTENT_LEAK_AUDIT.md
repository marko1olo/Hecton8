# Batch25 2501 WeatherEvents Persistent Leak Audit

Status: PENDING UNITY OWNER VERIFICATION
Scope: static source/log audit only. No Unity, no build, no source edits.
Agent: 2501
Date: 2026-06-04

## Finding

The leak warning was real in the latest available Unity-owner log, not only controller prose.

Evidence:
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log:3034` reports `Leak Detected : Persistent allocates 4 individual allocations`.
- The stack points to `NativeQueue<WeatherEventPayload>.ctor` through `Hecton8.Environment.WeatherEvents.EnsureInitialized()`, then `WeatherEvents.Register(...)`, then `Hecton8.Celestial.HectonCelestialEngine.OnEnable()`.
- A second reload in the same log repeats the same leak at `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log:4011`.
- The orchestration handoff also records the owner report: `Docs/Orchestration/ORCHESTRATOR_DAY_20260604.md:728`.

## Exact Owner

Type/file:
- `Hecton8.Environment.WeatherEvents`
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs`

Allocator owner:
- `WeatherEvents` owns two static `NativeQueue<WeatherEventPayload>` lanes:
  - `_pendingEvents`
  - `_nextFrameEvents`
- Allocator: `Allocator.Persistent` through `DataVaultExemptSignalLaneAllocator`.
- Current source allocation sites:
  - `WeatherEvents.EnsureInitialized()` creates `_pendingEvents`.
  - `WeatherEvents.EnsureInitialized()` creates `_nextFrameEvents`.
- Sentinel registration exists in current source through `NativeMemorySentinel.RegisterNativeQueue(...)`.

Payload/listener:
- `WeatherEventPayload` is unmanaged explicit-layout payload, 128 bytes.
- `IWeatherEventListener` is managed listener interface.
- Current live listeners found:
  - `HectonCelestialEngine`
  - `HectonGIRelaySystem`
- Producers/prewarm callers found:
  - `GlobalWeatherDirector`
  - `HectonSurfaceWeatherDirector`

## HectonCelestialEngine Route

`HectonCelestialEngine.OnEnable()` registers only during Play Mode:

- `GlobalRegistry.RegisterCelestialEngineRuntime(this)`
- `RefreshColdRuntimeDependencies()`
- `TryResolveCelestialRuntimeBuffers()`
- `BiomeMatrixEvents.Unregister(this); BiomeMatrixEvents.Register(this);`
- `WeatherEvents.Unregister(this); WeatherEvents.Register(this);`
- `TryRegisterToTickManager()`
- `TryRegisterLateFrameTickable()`

The leak stack is specifically:

`HectonCelestialEngine.OnEnable()` -> `WeatherEvents.Register(this)` -> `WeatherEvents.EnsureInitialized()` -> `new NativeQueue<WeatherEventPayload>(Allocator.Persistent)`.

`HectonCelestialEngine.OnDisable()` and `OnDestroy()` both unregister from `WeatherEvents`. That handles listener references. It does not own or dispose the static native queues; `WeatherEvents` owns them.

## Classification

Primary classification: static lane not cleared before editor domain reload / assembly reload.

Details:
- This is not a missing `HectonCelestialEngine.OnDisable()` listener unregister. The source has unregister calls in both `OnDisable()` and `OnDestroy()`.
- This is not evidence of duplicate `OnEnable` listener registration. `WeatherEvents.RegisterImmediate()` checks `_listeners.Contains(listener)` before adding a listener.
- This is not a false positive from static docs. The newest inspected Unity log contains the actual native leak detector stack.
- The four allocations match two `NativeQueue<WeatherEventPayload>` instances, each backing queue producing multiple native allocations.

Current disk state:
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs` already has an uncommitted narrow patch by another agent:
  - `UnityEditor` import guarded by `#if UNITY_EDITOR`.
  - `[InitializeOnLoadMethod] RegisterEditorLifecycleCleanup()`.
  - `AssemblyReloadEvents.beforeAssemblyReload`, `EditorApplication.quitting`, and play-mode state cleanup hooks.
  - Hook target calls `ResetStaticState()`, which unregisters sentinel records, disposes both queues, clears listeners, and resets counters.
- I did not alter this patch.

Important timing note:
- In `UnityEditor_visual_audit_restart_1474b.log`, the leak stacks occur before Unity requests later script compilation for changed files. The stack line numbers match the old compiled source, not current disk line numbers.
- Therefore the current `WeatherEvents` cleanup patch is plausible but unverified. It cannot be marked clean without a fresh Unity reload/play-exit artifact after compilation.

## Minimal Safe Fix Plan

Do not patch `HectonCelestialEngine` for this leak. It is a consumer/listener; the static native queue owner is `WeatherEvents`.

Use the current owner-side route already present on disk:
1. Keep `WeatherEvents.ResetStaticState()` as the single disposal owner for `_pendingEvents` and `_nextFrameEvents`.
2. Keep editor lifecycle hooks in `WeatherEvents`:
   - `AssemblyReloadEvents.beforeAssemblyReload`
   - `EditorApplication.quitting`
   - `EditorApplication.playModeStateChanged` for `ExitingPlayMode` and `EnteredEditMode`
3. Add or extend an editor test only if code edits are later authorized:
   - call `WeatherEvents.PrepareCold()`;
   - invoke `ResetStaticState()` by reflection;
   - assert `_pendingEvents.IsCreated == false`;
   - assert `_nextFrameEvents.IsCreated == false`.
4. Unity owner verification required:
   - compile the current source;
   - clear console/log baseline;
   - enter Play Mode through the route that enables `HectonCelestialEngine`;
   - exit Play Mode;
   - trigger one domain reload or script recompilation;
   - confirm no `Leak Detected : Persistent allocates` stack for `WeatherEvents`.

## Rollback Risk

Rollback of the current `WeatherEvents` editor cleanup patch has high risk. It restores the observed persistent NativeQueue leak on assembly reload.

Risk if the patch stays:
- Low runtime risk. The hooks are editor-only except the existing `SubsystemRegistration` reset.
- Medium editor risk if cleanup fires during active dispatch. The hook fires on play-mode exit, editor quit, or assembly reload, not during normal dispatcher late-frame processing. Existing `ResetStaticState()` hard-resets `_isDispatching` and queues.
- Medium compile risk only if editor assembly references are unavailable, but the code is guarded by `#if UNITY_EDITOR` and uses `UnityEditor`.

## Compile Risk

No compile was run by this agent.

Static compile risk in current source:
- `WeatherEvents.cs` added `using UnityEditor` behind `#if UNITY_EDITOR`; this is correct for player builds.
- `[InitializeOnLoadMethod]`, `AssemblyReloadEvents`, `EditorApplication`, and `PlayModeStateChange` are editor-only symbols.
- Existing editor test `WeatherEventsEditTests` already reflects private `ResetStaticState()` and should still compile against current visibility.

## First-20-Minutes Route Impact

This removes a runtime visual proof blocker. Surface/weather/celestial proof cannot graduate while Play/domain reload leaves persistent native queue warnings in the console/log. The current source likely addresses the leak owner, but route acceptance remains blocked until Unity owner produces a clean post-patch reload/play-exit log.

## Scalability Consequences

Low: no gameplay truth change. Static queue cleanup prevents editor/runtime proof contamination on compact validation lanes.

Middle: same event route and bounded capacity remain. No extra per-frame cost.

High: no visual feature is removed. Weather/celestial event delivery remains available.

Ultra: no additional simulation or graphics path is introduced. Verification buys clean proof, not more runtime work.
