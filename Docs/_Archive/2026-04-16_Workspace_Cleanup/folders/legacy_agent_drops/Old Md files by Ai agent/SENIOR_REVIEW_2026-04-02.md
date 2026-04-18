**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Senior Review - 2026-04-02

## Scope

- Reviewed first-party runtime/UI code under `Assets/_Project/Scripts`.
- Prioritized confirmed runtime issues over broad refactors.
- Avoided touching files that already had active user edits unless required.

## Fixed

### 1. Builder status overlay could hide itself permanently

File: `Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs`

- The overlay used `_self.gameObject.SetActive(shouldShow)` inside its refresh loop.
- When the component lives on the same GameObject as the overlay root, hiding the object also disables the component.
- Result: the overlay can stop updating and never come back when the Builder Tool is re-equipped.

Resolution:

- Kept the object active and switched visibility through `CanvasGroup.alpha` only.
- This preserves the refresh loop and allows the overlay to recover correctly.

### 2. PDA barter action buttons could restore stale colors

File: `Assets/_Project/Scripts/UI/PDABarterTab.cs`

- Offer cards update their button background color every refresh based on current offer state.
- `PDABarterActionButton` cached its normal/hover colors only once during `Init`.
- Result: after state changes, pointer exit could restore the old color instead of the current one.

Resolution:

- Added `SetVisualState(Color normal)` to the button helper.
- Re-applied the current visual state after every card refresh.

### 3. Editor runtime profiler produced warning spam during long violation streaks

Files:

- `Assets/_Project/Editor/PlayModePerformanceMonitor.cs`
- `Assets/_Project/Editor/PlayModeOptimizationAudit.cs`

- The editor profiler emitted a warning for every violating sample window.
- In practice this floods the console and hides higher-signal diagnostics.
- The optimization audit also treated warning severity as the source of truth for budget violations.

Resolution:

- Added budget evaluation as an explicit helper in `PlayModePerformanceMonitor`.
- Repeated violating windows now keep logging their metrics, but warning severity is throttled instead of repeating every window.
- Updated the audit to count runtime budget violations from the profiler payload itself rather than relying on log severity.

### 4. Two runtime UI tabs used editor-only API without compile guards

Files:

- `Assets/_Project/Scripts/UI/PDAConstructionTab.cs`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`

- Both files referenced `UnityEditor.EditorApplication` inside `OnValidate()` without `#if UNITY_EDITOR`.
- This is harmless inside the Editor, but it is a player-build risk pattern in runtime assemblies.

Resolution:

- Wrapped the editor-only checks in `#if UNITY_EDITOR` guards.

### 5. Floating origin had a namespace collision compile blocker

File:

- `Assets/_Project/Scripts/HectonFloatingOrigin.cs`

- The script called `Physics.SyncTransforms()` from inside a project that also defines the `Hecton8.Physics` namespace.
- During compile, that unqualified symbol resolved incorrectly and produced a hard compiler error.

Resolution:

- Switched the call to `UnityEngine.Physics.SyncTransforms()` explicitly.

### 6. Flow field runtime/editor contracts drifted apart and blocked compile

Files:

- `Assets/_Project/Scripts/FlowFieldVisualizer.cs`
- `Assets/_Project/Scripts/CurrentVolume.cs`
- `Assets/_Project/Scripts/Editor/FlowFieldVisualizerTests.cs`

- The visualizer/tests expected a public `Recalculate()` entry point and a configurable minimum flow threshold.
- `FlowFieldVisualizer` also needed per-volume current sampling, but the only available sampler on `CurrentVolume` was private.
- The jobified global-current path had compile/runtime-shape issues around temporary native container lifetime and vector conversion.

Resolution:

- Restored the missing public surface on `FlowFieldVisualizer` (`Recalculate()`, `MinFlowStrength`).
- Added a safe public sampling bridge on `CurrentVolume` so the visualizer no longer reaches through private API.
- Fixed the global-current accumulation path to use explicit `float3` -> `Vector3` conversion and deterministic `NativeArray` disposal.
- Added the missing `UnityEngine.TestTools` import so the editor tests compile against `LogAssert`.

### 7. Survival API regressions broke dependent gameplay scripts

Files:

- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/HectonDirectorAI.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs`

- Runtime callers still expected a public energy percentage and an imperative energy-drain API.
- `HectonSurvivalSystem` only exposed normalized energy and had an internal passive-drain method name collision.
- Several dependent files also missed the gameplay namespace import needed after recent code movement.

Resolution:

- Added `EnergyPercent` and restored a public `DrainEnergy(float amount)` API with clamping and dirty-state updates.
- Renamed the internal passive drain path to avoid signature ambiguity.
- Restored missing `using Hecton8.Gameplay;` imports in affected runtime callers.

### 8. Voxel generation contained merge-artifact compile blockers

File:

- `Assets/_Project/Scripts/HectonVoxelEngine.cs`

- The async generation path had duplicated local declarations for `shiftAtStart`.
- One branch also referenced a non-existent `shiftAtStartData` symbol.

Resolution:

- Removed the duplicate local declarations.
- Unified the code path on the valid `shiftAtStart` variable.

### 9. Base AI had hard compile issues in the shared runtime layer

File:

- `Assets/_Project/Scripts/HectonBaseAI.cs`

- The type declaration contained a malformed `IPoolable` interface token.
- One field initializer was syntactically broken.
- Atmosphere reads in the current implementation needed to bind against the actual runtime API shape.

Resolution:

- Corrected the interface list so the class implements `IPoolable` cleanly.
- Fixed the broken field initializer.
- Bound the hazard reads to the current atmosphere properties used elsewhere in the project.

### 10. Two visor HUD runtime scripts polled scene-wide searches too aggressively

Files:

- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs`

- Both scripts auto-resolved references through scene-wide search helpers whenever references were missing.
- In practice that pattern could re-run every frame and become a noisy runtime `Find*` hotspot.

Resolution:

- Converted auto-resolve to an on-demand path with `force` support for lifecycle/editor hooks.
- Added `NeedsAutoResolve()` guards plus a 1-second retry interval so scene-wide searches only run while references are actually missing.

### 11. Additional HUD/visor runtime overlays still retried auto-resolve too often

Files:

- `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`

- These scripts still performed reference auto-resolution from `Update()` / `LateUpdate()` paths.
- In the failure case that meant repeated `FindFirstObjectByType`, `FindObjectsByType`, or hierarchy searches every frame while links were missing.
- Because the whole stack runs in HUD/visor presentation, this is exactly the kind of low-grade polling cost that quietly accumulates.

Resolution:

- Moved all three scripts to the same guarded pattern used in the previous visor pass.
- Added `NeedsAutoResolve()` checks, a `force` path for lifecycle validation, and a 1-second retry interval.
- Kept behavior intact when references genuinely need to be reacquired, but removed the per-frame retry pressure.

## Verified

- Unity Editor state was ready before and after the change.
- Script refresh/compile completed with no blocking errors; the current console contains warnings only.
- Runtime first-party guard audit returned `NO_UNGUARDED_UNITYEDITOR_CALLS` across `Assets/_Project/Scripts` outside `Editor/`.
- Existing warnings remain in third-party/editor code and were not introduced by these fixes.

## Not Fixed In This Pass

- There are still multiple known runtime `Find*` usages and UI `Update()` loops across the project.
- Some runtime-heavy files already had substantial in-flight edits (`FlowFieldVisualizer.cs`, `HectonBaseAI.cs`), so this pass stayed focused on compile safety, API restoration, and high-signal polling reduction rather than broad refactors.
- The remaining `Find*` / polling cleanup needs a dedicated behavioral pass because several systems still need scene validation, not just compile validation.
