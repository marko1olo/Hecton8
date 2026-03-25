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
- Binding overrides are persisted through `PlayerPrefs`.
- Overrides API is exposed through `InputManager`:
  - `SaveBindingOverridesAsJson()`
  - `LoadBindingOverridesFromJson(string json)`
  - `ClearBindingOverrides()`

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
Last Updated: 2026-03-25
Version: 2.0
