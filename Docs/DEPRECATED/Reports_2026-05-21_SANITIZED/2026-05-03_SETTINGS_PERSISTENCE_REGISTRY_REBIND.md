<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-03 Settings Persistence Registry Rebind
Date: 2026-05-07

Status: PENDING VERIFICATION

## Mandates Followed

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## What Was Wrong

`SettingsManager` cached `GlobalRegistry.UserOptions` once during `Awake()`.
That made settings persistence dependent on Unity object startup order.
If a scene-authored settings owner awakened before `UserOptionsPersistence` was registered by bootstrap, `LoadAllSettings()` read defaults and `SaveInt` / `SaveFloat` / `SaveBool` returned without writing.

`SettingsManager.TryGetInstance()` also returned the private `_instance` cache even after a settings owner had been registered into `GlobalRegistry.Settings`.
That kept a secondary source of truth alive after the settings service slot was added.

Teardown also depended only on the local `_serviceRegistered` flag.
If bootstrap or another owner registered the same `SettingsManager` through `GlobalRegistry.RegisterSettingsRuntime(settingsManager)`, teardown could miss the registry slot if the local flag was stale.

## What I Did

- `SettingsManager` now implements `IGlobalRegistryHotSwapListener`.
- `OnEnable()` registers the settings owner into `GlobalRegistry.Settings`, registers a hot-swap listener, and refreshes the `UserOptionsPersistence` reference.
- `OnDisable()` and `OnDestroy()` unregister the hot-swap listener and settings slot.
- `TryGetInstance()` now returns `GlobalRegistry.Settings` first, then falls back to `_instance`.
- `LoadInt`, `LoadFloat`, `LoadBool`, `SaveInt`, `SaveFloat`, and `SaveBool` refresh the current `GlobalRegistry.UserOptions` reference before using persistence.
- `RefreshPersistenceFromRegistry()` reloads and reapplies cached settings when a previously missing or replaced persistence owner appears.
- `GameBootstrapper.EnsureSettingsRuntimeRegistered()` now calls `settingsManager.RefreshPersistenceFromRegistry()` after the UI phase registers the settings runtime.

## Evidence

- Diff check:
  - `git diff --check -- Assets/_Project/Scripts/UI/SettingsManager.cs Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - Result: exit code `0`; CRLF normalization warnings only.
- Compile check:
  - `dotnet build .\Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false /clp:Summary /v:minimal`
  - Result: `Build succeeded.`
  - Warnings: `0`
  - Errors: `0`

No Unity Play Mode was launched.
No MCP console log was available.
No settings-panel scene flow, save/reload roundtrip, or PlayerPrefs persistence proof was captured.
No GCMonitor or profiler numbers were captured.

## Regression Model

CPU: added work is lifecycle/bootstrap/user-action only. The repeated persistence refresh in load/save helpers is a single `GlobalRegistry.UserOptions` read plus `ReferenceEquals`; those methods are settings UI operations, not frame HUD readouts.

GC: no new managed collections, LINQ, coroutine, closure, or per-frame string path was added. Measured 0 B/frame proof is absent.

Memory: one boolean field was added to `SettingsManager`; no new native containers, textures, render targets, or persistent managed buffers were added.

Cadence: no `Tick`, `Update`, `LateUpdate`, `FixedUpdate`, coroutine, or dispatcher lane was added. Bootstrap UI phase now performs one explicit persistence refresh after settings service registration.

Correctness: settings persistence no longer depends only on the `Awake()` timing of `GlobalRegistry.UserOptions`. Registry slot truth is preferred for settings access and teardown.

## Failure Modes

- `GlobalRegistry` service rebound events are queued only for replacement/clear paths, not first registration from null; the bootstrap UI-phase refresh covers the main first-registration path, but custom scene startup outside bootstrap still needs runtime proof.
- If a disabled settings owner is manually registered and never enabled, hot-swap listener registration will not happen. `GameBootstrapper` refresh still covers bootstrap-owned registration.
- Runtime behavior still depends on scene wiring for `SettingsPanel`, `AudioMixer`, URP `Volume`, and player camera ownership.
- Dirty worktree state means this report is not a clean-PR boundary.

STATUS: PENDING VERIFICATION
