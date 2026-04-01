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

## Verified

- Unity Editor state was ready before and after the change.
- Script refresh/compile completed with `0` console errors.
- Existing warnings remain in third-party/editor code and were not introduced by these fixes.

## Not Fixed In This Pass

- There are still multiple known runtime `Find*` usages and UI `Update()` loops across the project.
- There is a broader project-wide pattern of runtime scripts referencing `UnityEditor.EditorApplication`; only the directly touched UI files were guarded in this pass.
- Those areas need a dedicated pass because several related files are already in active work or require broader behavioral validation.
