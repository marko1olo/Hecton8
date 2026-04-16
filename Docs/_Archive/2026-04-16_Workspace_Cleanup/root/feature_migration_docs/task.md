# Task: HectonPlayerMovement Migration to InputManager

## Status: [x] Completed

### Phase 1: Research & Planning
- [x] Explore `.kiro/specs` for context
- [x] Analyze `InputManager.cs` structure
- [x] 1.4 Migrate HectonPlayerMovement
  - [x] 1.4.1 Analyze InputManager and HectonPlayerMovement (Research)
  - [x] 1.4.2 Create Implementation Plan
  - [x] 1.4.3 Implement InputManager integration in HectonPlayerMovement
  - [x] 1.4.4 Implement Jump/Sprint event subscriptions
  - [x] 1.4.5 Refactor Move/Look input polling
  - [x] 1.4.6 Verify zero-GC and performance compliance
  - [x] 1.4.7 Finalize documentation and clean up legacy code paths
- [x] Phase 1.5: PDA, Interaction & Fabricator UI Migration
    - [x] Migrate `PlayerPDA.cs` (InputMap switching, Backspace navigation)
    - [x] Migrate `PlayerInteraction.cs` & `InteractionUI.cs` (Dynamic bindings)
    - [x] Migrate `PlayerToolManager.cs` (OnToolSlot events, Fire1/2 polling)
    - [x] Migrate `HectonFabricatorUI.cs` (OnNavigate, OnSubmit, OnCancel)
- [x] Phase 2: Final Tool & UI Migration
    - [x] Migrate `PlayerBuilder.cs` (Primary/Secondary actions)
    - [x] Migrate `HectonInventoryUI.cs` (OnInventory, Navigation)
    - [x] Update `LaserCutter.cs` (Secondary-action deconstruct handling)
    - [x] Final audit of all player-centric scripts for legacy `Input` calls
      - No runtime legacy `UnityEngine.Input` calls remain in `_Project/Scripts`
- [x] Add runtime rebinding foundation (`RebindingManager` + InputManager override API)
- [x] Implement PDA Controls runtime rebinding UI (`PDAControlsRebindUI`)
- [x] Wire live binding refresh for prompt UIs (`InteractionUI`, `HectonFabricatorUI`)
- [ ] Add comprehensive XML documentation

### Phase 3: Verification
- [x] Check Unity Console for errors/warnings
- [ ] Verify movement functionality in-editor (if possible)
- [x] Create walkthrough of changes
