# HectonPlayerMovement Input Migration Plan

Migrate the legacy input polling in [HectonPlayerMovement.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs) to the new Unity Input System via the [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) singleton. This migration aims for "Master Grade" implementation with zero GC allocations and robust error handling.

## User Review Required

> [!IMPORTANT]
> This change will make the `useNewInputSystem` flag functional. Ensure that an [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) instance exists in your scene and is correctly configured with the `HectonInputActions` asset before testing.

## Phase 1.5: Interaction, Tools & UI Migration
Migrate [PlayerFlashlight](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerFlashlight.cs#66-353), [PlayerPDA](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs#71-617), [PlayerInteraction](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Interaction/PlayerInteraction.cs#53-436), [PlayerToolManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerToolManager.cs#33-559), [HectonFabricatorUI](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonFabricatorUI.cs#89-276), [PlayerBuilder](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerBuilder.cs#45-734), [LaserCutter](file:///c:/hades/Hecton8/Assets/_Project/Scripts/LaserCutter.cs#49-667), and [HectonInventoryUI](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonInventoryUI.cs#41-663) to the new [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) system.

### Core Systems

#### [MODIFY] [InputManager.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs)
- Add any missing discrete events if necessary.
- Ensure [OnCancel](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#305-306) is robustly mapped to `Escape`.

#### [MODIFY] [PlayerFlashlight.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerFlashlight.cs)
- Replace `UnityEngine.Input.GetKeyDown(flashlightKey)` with a subscription to `InputManager.Instance.OnFlashlight`.
- Remove `flashlightKey` and `controlScheme` configuration.

#### [MODIFY] [PlayerInteraction.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Interaction/PlayerInteraction.cs)
- Replace legacy polling in [Tick()](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs#288-307) with `InputManager.Instance.OnInteract` event subscription.
- Remove `interactKey` and `controlScheme`.
- Update `ActiveInteractKey` property to reflect [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384)'s current binding.

#### [MODIFY] [InteractionUI.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Interaction/InteractionUI.cs)
- Update [ResolveInteractPrefix()](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Interaction/InteractionUI.cs#142-151) to fetch the binding string from [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) instead of `PlayerInteraction.ActiveInteractKey`.

### Tools & Equipment

#### [MODIFY] [PlayerToolManager.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerToolManager.cs)
- Replace `SlotKeys` polling with `OnToolSlot1-4` events.
- Use `InputManager.Instance.IsPrimaryActionHeld` and `IsSecondaryActionHeld` properties for tool firing (Zero-GC polling).

#### [MODIFY] [PlayerBuilder.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerBuilder.cs)
- Subscribe to [OnPrimaryAction](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#296-298) for module placement.
- Subscribe to [OnSecondaryAction](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#299-300) for module rotation.
- Remove legacy `UnityEngine.Input.GetButton` polling from [ToolTick](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerTool.cs#290-297).

#### [MODIFY] [LaserCutter.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/LaserCutter.cs)
- Replace `Input.GetKey(deconstructModifier)` with a centralized input check (e.g., `InputManager.Instance.IsDeconstructHeld` or similar).

### User Interface

#### [MODIFY] [PlayerPDA.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs)
- Replace `GetKeyDown` for [PDA](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs#51-70) toggle with [OnPDA](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#287-288) event.
- Replace `Escape`/`Backspace` polling with [OnCancel](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#305-306) and `OnBack` events.
- Implement [SwitchToUIInput()](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#339-344) and [SwitchToPlayerInput()](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#333-338) calls when opening/closing.

#### [MODIFY] [HectonFabricatorUI.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonFabricatorUI.cs)
- Replace `GetKeyDown` for navigation (W/S, Arrows) and submission (Space/Enter) with [OnNavigate](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#302-304) and [OnSubmit](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#304-305) events.
- Implement proper [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) mode switching.

#### [MODIFY] [HectonInventoryUI.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonInventoryUI.cs)
- Subscribe to [OnInventory](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#288-289) for toggling visibility.
- Subscribe to [OnNavigate](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#302-304) for grid selection.
- Subscribe to [OnCancel](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#305-306) for closing.
- Remove legacy `Input.GetKeyDown` polling.

## Proposed Changes

### Gameplay Logic

#### [MODIFY] [HectonPlayerMovement.cs](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs)

- **Dependency Management**: Cache a reference to `InputManager.Instance` in [Awake](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#145-158) or [Start](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Interaction/PlayerInteraction.cs#230-250).
- **Event Subscriptions**:
    - Subscribe to [OnJump](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#277-278) and [OnSprint](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#279-280) events in [OnEnable](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs#376-381).
    - Unsubscribe in [OnDisable](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs#264-283) to prevent memory leaks and ensure clean lifecycle management.
- **Input Polling Refactor**:
    - Update the internal input cache variables (`_moveInput`, `_lookInput`, etc.) in the [Tick()](file:///c:/hades/Hecton8/Assets/_Project/Scripts/PlayerPDA.cs#288-307) method by checking the `useNewInputSystem` flag.
    - If `true`, fetch values from `InputManager.Instance.MoveInput` and `InputManager.Instance.LookInput`.
    - Ensure discrete states like `IsJumping` and `IsSprinting` are synchronized with the [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) states.
- **Documentation**: Add standard XML documentation to all modified methods and fields.
- **Zero GC Enforcement**: Use cached delegates for event subscriptions if necessary, though [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384)'s current design is already optimized.

## Verification Plan

### Automated Verification
- **Console Monitoring**: Execute `read_console` post-application to ensure no `NullReferenceException` occurs during initialization.
- **Compilation Check**: Confirm the project compiles successfully without warnings related to the input system.

### Manual Verification
1. **Scene Setup**: Ensure the [InputManager](file:///c:/hades/Hecton8/Assets/_Project/Scripts/Input/InputManager.cs#22-384) is active in the hierarchy.
2. **Flag Activation**: Select the [HectonPlayer](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs#30-1417) and ensure `useNewInputSystem` is enabled on the [HectonPlayerMovement](file:///c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs#30-1417) component.
3. **Control Test**:
    - Movement (WASD/Gamepad Left Stick)
    - Looking (Mouse/Gamepad Right Stick)
    - Jumping (Space/Gamepad Bottom Button)
    - Sprinting (Shift/Gamepad Left Stick Press)
4. **Backward Compatibility**: Disable `useNewInputSystem` and verify functionality remains correct using legacy input.
