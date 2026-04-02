# Input Runtime Hardening - 2026-04-02

## Scope

This pass targeted the play-mode teardown errors that were still appearing after runtime smoke work:

- `Map must be contained in state`
- `Map index on InputActionMap is out of range`

## Root Cause

File:
- `Assets/_Project/Scripts/Input/InputManager.cs`

The project kept cached `InputActionMap` references alive across play-mode teardown.

That was normally fine during active gameplay, but during editor stop / domain transition the following paths were still possible:

1. Scene systems called `SwitchToPlayerInput()`, `SwitchToUIInput()`, `EnableUIInput()`, or `DisablePlayerInput()` while Unity was already tearing down Input System state.
2. `InputManager.OnDestroy()` also tried to disable the cached maps directly.
3. The cached map references were no longer guaranteed to be backed by valid Input System state, which produced the runtime errors above.

## Fix

File:
- `Assets/_Project/Scripts/Input/InputManager.cs`

Applied hardening:

- Added `_inputMapsInitialized` runtime gate so public API no longer touches maps before a valid action-map setup exists.
- Replaced direct `Enable()` / `Disable()` calls with safe wrappers:
  - `SafeEnableActionMap(...)`
  - `SafeDisableActionMap(...)`
- Wrapped stale-map failure cases (`InvalidOperationException`, `ArgumentOutOfRangeException`) and invalidated the cached map reference instead of letting teardown spam the console.
- Hardened `GetAction(...)` so stale map lookups fail gracefully and return `null`.
- Reset cached action/map/asset references explicitly during `OnDestroy()` after safe shutdown.

## Verification

- Unity recompiles without new `Error`.
- Performed a clean `play -> stop` cycle after the fix.
- Result: console stayed clean; the previous Input System teardown errors no longer appeared.

## Practical Impact

- Editor play-mode stop is now quieter and safer.
- UI/gameplay systems can still call the public input-switching API during shutdown without detonating stale `InputActionMap` state.
- Runtime smoke investigation can continue without Input System teardown noise masking the next real blocker.

## Follow-up Hardening

Later in the same 2026-04-02 wave, a concrete teardown stack was reproduced through:

- `PauseMenuController.OnDisable()`
- `PauseMenuController.ApplyClosedState()`
- `InputManager.SwitchToPlayerInput()`

That follow-up exposed two remaining weak spots:

1. `PauseMenuController` always tried to restore player input during `OnDisable()`, even when the runtime was already shutting down.
2. `InputManager.SafeEnableActionMap(...)` and `SafeDisableActionMap(...)` still handled only a narrow subset of stale Input System exceptions.

Additional fix:

- `PauseMenuController`
  - `Awake()` now closes without forcing a player-input restore.
  - `OnDisable()` restores player input only when runtime input maps are still valid.
- `InputManager`
  - exposed `CanSwitchActionMaps`
  - guarded public map switching with that runtime state
  - broadened stale-map protection to handle the remaining teardown exceptions
  - invalidates `_inputMapsInitialized` once both cached maps are gone

Follow-up verification:

- Cleared the console.
- Ran another clean `play -> stop` cycle.
- Result: no `Map must be contained in state` and no `Map index on InputActionMap is out of range` entries were produced.
