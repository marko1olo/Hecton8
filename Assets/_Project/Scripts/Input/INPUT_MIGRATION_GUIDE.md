# Hecton-8 Input System Migration Guide

## Overview
This guide documents the migration from Unity's legacy Input Manager to the Unity Input System architecture used in Hecton-8.

## Status
Phase 2 gameplay and UI migration is complete.

## Implemented Architecture

### Input Core
- `InputManager` is the single runtime entry point for input reads and events.
- Player and UI action maps are supported with map switching (`SwitchToPlayerInput`, `SwitchToUIInput`).
- All high-frequency access is event-driven or cached-value polling from `InputAction`.

### Rebinding
- `RebindingManager` provides interactive rebinding with cancel support.
- Binding overrides are persisted through bounded `controls.json` bytes under the Hecton persistent path policy.
- `ControlRemapper` is the active save/load route; Unity Input System JSON helpers remain inert compatibility stubs.
- `IInputBindingService.SaveOverrides`, `LoadOverrides`, and `ClearOverrides` return `bool`; UI must not show success when these calls fail closed.
- `OnOverridesSaveFailed` is raised after automatic rebind persistence fails and runtime binding overrides have been restored.
- Missing `controls.json` means default bindings: `LoadOverrides` clears runtime overrides and returns success, while malformed files fail without mutating current bindings.
- `controls.json` parsing is root-object scoped: nested/spoofed `bindings`, duplicate record fields, missing binding ids, and trailing root garbage fail before runtime overrides are cleared.
- `UserOptionsPersistence.TrySave()` is the checked options save route; legacy `Save()` remains a compatibility wrapper and records `LastSaveSucceeded`.
- Load is transactional: if a post-validation apply step fails, the previous runtime overrides are restored before failure is reported.
- Concurrent load attempts fail closed before runtime overrides are cleared; the shared rollback snapshot is protected by an interlocked lease.
- Interactive rebind start is fail-closed: if Unity Input System cannot prepare/disable/start the action, the previous enabled state is restored and no start event is emitted.
- Conflict confirmation disables the previous conflicting binding by applying an empty override path before saving.
- Disabled bindings are serialized explicitly as `"path":""`; `null` means no override and must not be written.
- Runtime override clearing is exposed through `InputManager.TryClearBindingOverrides()`; the legacy `ClearBindingOverrides()` wrapper is inert success-blind compatibility only.
- UI and `ControlRemapper` must use the `TryClearBindingOverrides()` route so failed runtime clear requests do not publish false success or destroy current overrides.

### Runtime Controls UI
- `PDAControlsRebindUI` implements the runtime rebinding workflow for the PDA Controls tab.
- Supports:
  - Row navigation via `OnNavigate`
  - Rebind start via `OnSubmit`
  - Rebind cancel via `OnCancel`
  - Reset selected binding via `OnTabNext`
  - Reset all overrides via `OnTabPrevious`
- Includes auto-generation of default rows when inspector rows are empty.

### Live Binding Hint Refresh
- Interaction and crafting hints update immediately after rebind/reset:
  - `InteractionUI`
  - `HectonFabricatorUI`

## Migration Outcome
- Runtime legacy `UnityEngine.Input` polling has been removed from `_Project/Scripts` gameplay flow.
- Input-dependent gameplay scripts now consume `InputManager` events/state.
- UI systems are wired to the same unified input source.

## Scene Integration Notes
- `PDAControlsRebindUI` must be attached to the Controls tab object (or a suitable PDA UI object).
- For automatic text binding in UI hierarchy, use child naming convention:
  - `Label_<ActionName>`
  - `Binding_<ActionName>`
  - `Selected_<ActionName>`
- If these objects are absent, rebinding still functions; only visual fields remain unbound.

## Validation Checklist
- Verify project compiles without input-related errors.
- Verify PDA Controls tab can:
  - navigate rows
  - start/cancel rebind
  - reset selected binding
  - reset all bindings
- Verify interaction and fabricator hint labels update immediately after rebinding.
- Verify overrides persist after restart.

## Known Gaps
- Gamepad profile tuning may still require per-device balancing.
- Full manual playtest pass is still recommended for movement and all UI paths.

---
Last Updated: 2026-05-27
Version: 2.3
