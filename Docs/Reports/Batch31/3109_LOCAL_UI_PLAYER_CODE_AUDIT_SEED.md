# 3109 Local UI/Player Code Audit Seed

Status: STATIC AUDIT SEED / NO RUNTIME ACCEPTANCE

## Mandates Followed

- `UI_Diegetic_Physical_Interfaces.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `ui.md`
- `player.md`
- `input.md`
- `camera.md`
- `gameplay.md`

## Current Static Owner Map

Player movement:

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- Size: 13,929 lines.
- Implements `IUpdatable`, `IFixedTickable`, `IColdTickable`, `ILateFrameTickable`, `IOriginShiftListener`, `IGlobalRegistryHotSwapListener`, `IPlayerMovementContracts`, `IPlayerKinematicsMovementRuntime`, and multiple presentation/service sinks at `Assets/_Project/Scripts/HectonPlayerMovement.cs:57`.
- Locomotion labels exist at `Assets/_Project/Scripts/HectonPlayerMovement.cs:213`: `ShallowWadeWalk`, `SurfaceSwim`, `UnderwaterSwim`, `ExosuitLocomotion`; current mode state is at `Assets/_Project/Scripts/HectonPlayerMovement.cs:1423`.
- Static read confirms surface swim and underwater swim logic paths exist.
- Main movement phase methods exist at `Assets/_Project/Scripts/HectonPlayerMovement.cs:7114` (`Tick`), `Assets/_Project/Scripts/HectonPlayerMovement.cs:7289` (`LateFrameTick`), and `Assets/_Project/Scripts/HectonPlayerMovement.cs:8871` (`FixedTick`).
- Locomotion mode resolution is at `Assets/_Project/Scripts/HectonPlayerMovement.cs:9470` and returns dry interior, exosuit, dry ground, shallow wade, surface swim, or underwater swim.
- Kinematic repair probe path exists in movement at `Assets/_Project/Scripts/HectonPlayerMovement.cs:12134` and delegates to the motor at `Assets/_Project/Scripts/HectonPlayerMovement.cs:12149`.
- Motor-side kinematic repair probe entry exists at `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:730`. Runtime behavior and disabled/scaffold status remain unproven.
- Player camera rig exists and registers tick/late-frame at `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs:107`, `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs:113`, and `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs:284`.

Input:

- `Assets/_Project/Scripts/Core/InputDispatcher.cs`
- Size: 4,689 lines.
- Owns cached `InputAction` refs and frame input publication.
- `PlayerInputState` in `Assets/_Project/Scripts/Core/PlayerInputState.cs` is explicit-layout 64 bytes.
- `InputDispatcher` class and ABI guard exist at `Assets/_Project/Scripts/Core/InputDispatcher.cs:35`, `Assets/_Project/Scripts/Core/InputDispatcher.cs:96`, and `Assets/_Project/Scripts/Core/InputDispatcher.cs:1083`.
- Cached `InputAction` fields for move/look/jump/interact/PDA/pause/tools exist at `Assets/_Project/Scripts/Core/InputDispatcher.cs:205` through `Assets/_Project/Scripts/Core/InputDispatcher.cs:224`.
- Input owner phase methods exist at `Assets/_Project/Scripts/Core/InputDispatcher.cs:600`, `Assets/_Project/Scripts/Core/InputDispatcher.cs:608`, and `Assets/_Project/Scripts/Core/InputDispatcher.cs:613`.
- Snapshot read API exists at `Assets/_Project/Scripts/Core/InputDispatcher.cs:651`.
- Unity Input System reads are centralized in static helper methods at `Assets/_Project/Scripts/Core/InputDispatcher.cs:2988`, `Assets/_Project/Scripts/Core/InputDispatcher.cs:2993`, and `Assets/_Project/Scripts/Core/InputDispatcher.cs:2998`.
- Fast scan did not find `Input.GetKey/GetAxis/GetButton` in selected input/player files.
- Unity `InputAction.ReadValue` / `IsPressed` is used in owner boundary, which is the expected owner route, pending runtime GC proof.
- Subagent scout confirmed `HectonPlayerInputHandler` reads `IInputService`, not direct Unity `Input.Get*`.

UI:

- `Assets/_Project/Scripts/UI/SurvivalHUDController.cs`
- `Assets/_Project/Scripts/UI/InteractionUI.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs`
- `Assets/_Project/Scripts/UI/TMP_TextRegistry.cs`
- `Assets/_Project/Scripts/UI/TmpTextNoAlloc.cs`
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`
- `Assets/_Project/Scripts/Interaction/InteractionUI.cs`
- `Assets/_Project/Scripts/Interaction/InteractableRegistry.cs`
- `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs`
- `Assets/_Project/Prefabs/Player.prefab:967` binds `Hecton8.Interaction.PlayerInteraction`.
- `Assets/_Project/Prefabs/Player.prefab:1017` binds `Hecton8.Gameplay.HectonPlayerMovement`.
- `Assets/_Project/Prefabs/Player.prefab:1587` binds `Hecton8.UI.PlayerPDA`.
- `Assets/_Project/Prefabs/Player.prefab:2051` binds `PlayerSwimPresentationController`.
- `Assets/_Project/Prefabs/Player.prefab:2254` binds `PlayerSwimBlockoutRig`.
- `Assets/_Project/Prefabs/Player.prefab:2899` binds `NASAPunk.Visor.VisorHUDController`.
- `Assets/_Project/Prefabs/Player.prefab:3690` binds `NASAPunk.Visor.SuitHUDPresentationController`.
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2390` is currently `m_RenderMode: 0` (`ScreenSpaceOverlay`).
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2414` binds `Hecton8.UI.SuitHUDV4CanvasOverlay`.
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:4543` binds `Hecton8.Interaction.InteractionUI`.
- `Assets/_Project/Prefabs/HUD_Internal.prefab:44` binds `NASAPunk.Visor.SuitHUDScreenCompositor`; `Assets/_Project/Prefabs/HUD_Internal.prefab:53` sets `forceScreenSpaceOverlay: 1`.
- Active scene text scan finds a `Player` scene object at `Assets/_Project/Scenes/02_HECTON_WORLD.unity:70226`, but most script bindings are prefab/override driven and need Unity readback before acceptance.

Fast scan result:

- Selected files did not show direct `Update/LateUpdate/FixedUpdate` MonoBehaviour hot methods in the first filter.
- UI text paths use `SetCharArray` in `InteractionUI`, `TmpTextNoAlloc`, and `SuitHUDV4CanvasOverlay`.
- Canvas visibility mostly uses `CanvasGroup`.
- Subagent scout found a cleaner event-driven interaction UI under `Hecton8.Interaction` and a less clean legacy prompt path under `Hecton8.UI.InteractionUI`.

## Static Red Flags

### Player Movement God-Object Risk

`HectonPlayerMovement.cs` is a 13,929-line multi-domain runtime file. It owns or touches input, camera, movement, water, exosuit, audio events, UI/presentation signals, kinematic repair, sonar, inventory load, AUP, brine, and diagnostics.

This is not automatically a compile failure, but it is a high-risk integration surface. Future work must not expand it blindly.

Required 3109 audit:

- name which methods are actual movement authority;
- name which methods are presentation-only;
- identify safe narrow patch points;
- avoid new public API until existing contracts are mapped.

### UI Prompt String Allocation Risk

`InteractionUI` writes hot prompt presentation through `TMP_Text.SetCharArray`, but it still has cached string construction paths:

- legacy `Hecton8.UI.InteractionUI` has `OnPromptChanged` string event at `Assets/_Project/Scripts/UI/InteractionUI.cs:99`;
- it invokes the event at `Assets/_Project/Scripts/UI/InteractionUI.cs:625`;
- it allocates cached strings at `Assets/_Project/Scripts/UI/InteractionUI.cs:946` and `Assets/_Project/Scripts/UI/InteractionUI.cs:960`;
- it registers as `ILateFrameTickable` at `Assets/_Project/Scripts/UI/InteractionUI.cs:980`.

The cleaner `Hecton8.Interaction.InteractionUI` is event-driven, registers to `InteractionEvents` at `Assets/_Project/Scripts/Interaction/InteractionUI.cs:55`, and uses `SetCharArray` at `Assets/_Project/Scripts/Interaction/InteractionUI.cs:207`.

These are marked cold prompt-cache allocations. That is acceptable only if triggered on enable/language/input-display changes, not every `LateFrameTick`.

Required 3109 audit:

- prove `new string(...)` is not hit during normal prompt polling;
- check `OnPromptChanged?.Invoke(eventPrompt)` event listeners for allocation/side effects;
- decide whether `CurrentPrompt` string exposure is safe or should remain diagnostic/cold only.

### UI Runtime Proof Missing

Static code exists, but no current proof shows:

- oxygen UI;
- depth UI;
- pressure/hull/suit risk;
- interaction prompt;
- route cue;
- tool state;
- PDA/pause route;
- UI-on/off policy in the same route.

Reject any "UI is done" claim until h8_1475+ captures and GC/profiler evidence exist.

### Movement Runtime Proof Missing

Static code exists, but no current proof shows:

- walking/interior or shoreline movement;
- surface swim;
- underwater swim;
- ascend/descend;
- shore/surface transition;
- camera route readability;
- interaction while moving;
- input device matrix.

Reject any "movement works" claim until Play Mode capture and GC/profiler evidence exist.

### Scene Wiring Unknown

Existing code is not enough. Some prefab wiring is known, but active scene readback is still missing.

Known prefab bindings:

- `Player.prefab` contains `PlayerInteraction`, `HectonPlayerMovement`, PDA, swim presentation, `VisorHUDController`, and `SuitHUDPresentationController`.
- `Suit_HUD_Canvas.prefab` contains `SuitHUDV4CanvasOverlay` and the cleaner `Hecton8.Interaction.InteractionUI`.
- `HUD_Internal.prefab` contains `SuitHUDScreenCompositor` and currently forces screen-space overlay.

Still unknown until Unity readback:

- whether `InputDispatcher` and `PlayerRuntimeContextService` are live in `02_HECTON_WORLD`;
- whether the scene instance of `Player` matches prefab values after the 93k-line scene churn;
- whether the HUD render path is projection/world-space during gameplay or overlay fallback;
- whether `Hecton8.UI.InteractionUI` exists anywhere active as duplicate prompt owner;
- whether `HectonPlayerMotor` is present and connected to `HectonPlayerMovement` on the active player instance.

### HUD Render Path Risk

`SuitHUDV4CanvasOverlay` and `SuitHUDScreenCompositor` contain screen-space overlay fallback/force flags.

- `SuitHUDV4CanvasOverlay` can set `RenderMode.ScreenSpaceOverlay` at `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:2475` and `RenderMode.WorldSpace` at `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:2502`.
- It validates world-space/projection state at `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:7226` through `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:7238`.
- `SuitHUDScreenCompositor` owns `forceScreenSpaceOverlay` at `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs:31` and applies it at `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs:288`.
- `HUD_Internal.prefab` sets `forceScreenSpaceOverlay: 1` at `Assets/_Project/Prefabs/HUD_Internal.prefab:53`.

This may be acceptable for preview/editor/noninteractive bridge paths, but it must not be accepted as interactive first-party gameplay HUD without scene-value proof.

### Interaction Route Duplication Risk

Two interaction UI paths appear to exist:

- `Hecton8.Interaction.InteractionUI`: event-driven, cleaner primary candidate.
- `Hecton8.UI.InteractionUI`: late-frame camera prompt probe, string UnityEvents, generic prompt templates.

Required 3109 audit:

- determine which one is active in 02_HECTON_WORLD/player prefab;
- lock the primary route;
- retire or explicitly demote the duplicate route only after scene wiring proof.

Static result so far:

- `Suit_HUD_Canvas.prefab` binds `Hecton8.Interaction.InteractionUI`, not the legacy `Hecton8.UI.InteractionUI`.
- A raw scene scan has not yet proven the absence of the legacy `Hecton8.UI.InteractionUI` in active scene instances.
- The legacy UI path includes string UnityEvent exposure; it must remain suspect until proven inactive or converted to a cold/diagnostic-only path.

### Kinematic Repair / Snap Blocker

`ScheduleKinematicRepairTargetProbe` is not a claimable complete feature yet. It needs classification before movement acceptance:

- accepted removal;
- temporary scaffold;
- missing owner;
- blocked dependency.

Static anchor:

- movement calls the route from `Tick` at `Assets/_Project/Scripts/HectonPlayerMovement.cs:7222`;
- movement delegates to motor at `Assets/_Project/Scripts/HectonPlayerMovement.cs:12149`;
- motor method entry is `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:730`.

Until Play Mode proves snap/repair target behavior or the route is explicitly scoped out of the first slice, player movement remains `PENDING VERIFICATION`.

## Prefab Placement Gate From 3107 Scout

Static scout result:

- candidate geology: `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`, 49 prefabs, all scanned with `LODGroup`, `BoxCollider`, no `MeshCollider`, no placeholder marker, no missing scripts;
- candidate flora/coral: `Assets/_Project/Prefabs/Nature/Flora/Baked`, 89 `GEN_` baked starter prefabs, all scanned with `LODGroup`, no colliders, no placeholder marker, no missing scripts;
- caution hardware/debris: `Assets/_Project/Prefabs/Construction/Final`, 10 prefabs, but generated/final audits still flag primitive-mesh debt;
- rejected visible placement: `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`;
- rejected visible placement: `Assets/_Project/Prefabs/WorldProceduralProxy`.

Placement is blocked until base water/terrain/sky pass and target object families pass mesh/material/LOD/collider/proof gates. Random scatter would launder bad art into the route.

## Immediate 3109 Work Order

1. Read the task file `taskslocal/batch31_night_visual_recovery/3109_FULL_UI_PLAYER_MOVEMENT_OWNER.txt`.
2. Build a method-level map of `HectonPlayerMovement`:
   - input read;
   - camera presentation;
   - FixedTick authority;
   - swim/shore transition;
   - dry/interior walk;
   - exosuit delegation;
   - signal publication;
   - black-box/crash telemetry.
3. Build a UI route map:
   - SurvivalHUD;
   - SuitHUDV4;
   - VisorHUD;
   - InteractionUI;
   - PDA/pause.
4. Produce a minimum runtime proof checklist for 1475:
   - one 60-second movement script/repro;
   - UI on/off route captures;
   - GCMonitor output;
   - input snapshot evidence;
   - locomotion mode transitions.
5. Prove scene wiring before code edits.
6. Do not implement until the method map identifies a safe patch point.

## Low / Middle / High / Ultra

- Low: stable movement, clean input snapshot, readable oxygen/depth/pressure UI, no string hot allocations, no camera hiding route.
- Middle: richer visor material/prompt feedback, haptic priority basics, better swim/camera response.
- High: better physical micro-motion, stronger tool/camera feedback, denser HUD material response.
- Ultra: sensory layering only after low-tier control/readability/GC proof remains intact.

## Disposition

`PENDING VERIFICATION`.

Static code exists. Runtime/play/profiler evidence is absent.
