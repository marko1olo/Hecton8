# PDA / Inventory / Hotbar Plan

## Scope

Build a Subnautica-like PDA shell, tetris inventory view, and 4-slot quick-access bar on top of the existing Hecton systems.

This pass must reuse:
- `PlayerInventory` + `InventoryGrid` for inventory data and save/load
- `PlayerPDA` for open/close, tabs, fade, battery drain, input map switching
- `PlayerToolManager` for 4-slot tool equip logic
- `PDAControlsRebindUI` for the controls tab

This pass must not:
- invent a second inventory backend
- break flat HUD / `HUD_V4_CanvasRoot`
- replace input architecture

## Verified Existing Backbone

- `PlayerInventory`
  - owns `InventoryGrid`
  - already saves / loads via `InventoryDTO`
  - already receives picked items through `InteractionEvents.OnItemCollected`
- `InventoryGrid`
  - already supports multi-cell item placement
  - no rotation
  - no item-instance model
  - no proper stack model beyond `ItemData` metadata
- `PlayerPDA`
  - already handles `open / close / SetActiveTab`
  - already switches input maps
  - already exposes static `PlayerPDA.IsOpen`
  - already supports 3 tabs
- `PlayerToolManager`
  - already owns 4 equip slots
  - already switches on `InputManager.OnToolSlot1..4`
  - already validates tool presence against `PlayerInventory`
- `PDAControlsRebindUI`
  - already fits a PDA controls tab

## Architectural Decision

Authoritative systems:
- Inventory data authority: `PlayerInventory`
- Grid placement authority: `InventoryGrid`
- PDA shell authority: `PlayerPDA`
- Equipped tool authority: `PlayerToolManager`

UI systems to add:
- `PDAEnterpriseUI`
  - scene/UI orchestrator for the PDA panel under the existing canvas
  - owns references to panel root, tab roots, quick-slot widgets, inventory widgets
  - updates visual state only
- small inventory snapshot helpers in `PlayerInventory`
  - expose anchor-item snapshot for UI without mutating the grid

## First Enterprise Pass

### Tabs

Use the existing 3-tab PDA model:
- Tab 0: `Inventory`
- Tab 1: `Equipment`
- Tab 2: `Controls`

### Inventory Tab

Show:
- tetris grid from `InventoryGrid`
- item icon blocks
- item title / dimensions / weight summary
- suit cargo stats

Do not fake stack counts. The current backend does not support true stacks.

### Equipment Tab

Show:
- 4 quick slots
- assigned tool icon/name per slot
- active slot highlight
- empty / unavailable / equipped states

This tab reflects `PlayerToolManager`. It does not replace equip logic.

### Controls Tab

Reuse `PDAControlsRebindUI`.

### Open Paths

- `OnPDA` -> opens/closes PDA, preserves last active tab behavior
- `OnInventory` -> opens PDA directly to Inventory tab

### HUD Quick Bar

Separate from PDA panel:
- compact hotbar strip in normal HUD
- reflects current 4 tool slots
- highlights active slot
- hidden or dimmed when PDA is open

## Constraints / Known Gaps

- The current inventory core has no instance-level data for per-item durability, quantity, metadata overrides, or stack splitting.
- Therefore this pass is a robust UI/shell integration pass, not a full MMO-grade inventory-core rewrite.
- If later we need stack splitting, drag/drop, per-instance upgrades, or consumables with quantities, the next step is a dedicated `InventoryItemInstance` data layer and save migration.

## Implementation Order

1. Add non-alloc inventory snapshot helpers in `PlayerInventory`.
2. Add scene/UI controller script for PDA and hotbar.
3. Wire `PlayerPDA` to also open Inventory tab from `OnInventory`.
4. Create scene hierarchy for PDA root and tabs under the existing UI canvas.
5. Create quick-slot strip under the existing HUD canvas.
6. Attach / wire `PlayerPDA`, `PDAEnterpriseUI`, `PDAControlsRebindUI`.
7. Verify compile, scene refs, input flow, and console.

## Acceptance Criteria

- `Tab` / `I` inventory path opens PDA to Inventory tab without using the old separate inventory overlay.
- PDA open blocks gameplay the same way the current `PlayerPDA` contract already expects.
- Inventory tab reflects the actual `InventoryGrid`.
- Equipment tab reflects the actual `PlayerToolManager`.
- HUD quick bar reflects active slot live.
- Controls tab remains functional.
- Flat HUD remains the only active HUD path.
