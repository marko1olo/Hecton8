# Player / HUD Bootstrap Binding Blocker - 2026-06-05

Status: STATIC INTEGRATION / NO RUNTIME ACCEPTANCE

Evidence class: `STATIC_SOURCE`, `SUBAGENT_STATIC_REPORT`.

Mandates followed:

- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Verdict

Static source suggests the scene-authored `Player` shell wins over the production `Player.prefab` in `02_HECTON_WORLD`.

This blocks full UI/movement acceptance. Do not polish movement/UI claims until runtime proves the production player and HUD graph are bound, or a Unity owner replaces the shell route safely.

## Target Prefab GUIDs

- `Assets/_Project/Prefabs/Player.prefab` -> `1c4db7a430141e5408e01b6ce4ed19d7`
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` -> `e286dd44e529d8b4498750dd0abbbfd8`
- `Assets/_Project/Prefabs/HUD_Internal.prefab` -> `949b94e6d99fdd44ea13e320d0784005`

Static GUID search found no references to these GUIDs under `Assets` or `ProjectSettings`. That means there is no static proof of scene, Addressables, or serialized bootstrap binding to these exact prefabs.

## Bootstrap Route Facts

- `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs` delegates bootstrap to `GameBootstrapper`.
- `GameBootstrapper` has serialized `playerSpawner`, `playerObject`, `playerController`, and `playerRigidbody`.
- `GameBootstrapper` resolves a tagged `Player` from the active scene if no player object exists.
- `GameBootstrapper.SpawnPlayerAsync` uses an already-resolved `HectonPlayerSpawner`; otherwise it positions/publishes an existing `playerObject`.
- `GameBootstrapper` initializes UI from serialized `uiAddressablePrefabs` if present, but no static reference proves `Suit_HUD_Canvas.prefab` or `HUD_Internal.prefab` is listed.

## Scene Reality

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` contains a scene-authored `GameObject` named `Player`.
- That scene object is tagged `Player`.
- The component evidence points at `HectonWorldShellController1428`, not the production player prefab stack.
- `HectonWorldShellController1428` ticks shell movement directly and reads input through `Keyboard.current`, `Mouse.current`, and legacy `Input.GetKey/GetAxisRaw` fallbacks.

## Production Player Prefab Reality

`Assets/_Project/Prefabs/Player.prefab` contains the production stack:

- `Hecton8.Interaction.PlayerInteraction`
- `Hecton8.Gameplay.HectonPlayerMovement`
- `PlayerToolManager`
- `Hecton8.UI.PlayerPDA`
- `ToolLoadoutProvisioner`
- visor/HUD presentation components

Static issue: `ToolLoadoutProvisioner` startup provisioning flags are disabled in the prefab snapshot.

## Runtime Context Risk

`PlayerRuntimeContextService` reads `BootstrapState.CurrentPlayerObject` and expects production player components. If bootstrap publishes the scene shell, runtime context may bind to an object that lacks movement, interaction, PDA, tools, and visor graph.

## UI / HUD Risk

- `Suit_HUD_Canvas.prefab` is serialized as `ScreenSpaceOverlay`, but `SuitHUDV4CanvasOverlay` forces runtime projection canvas to `WorldSpace` during play.
- Editor/non-playing paths may use overlay; runtime readback must classify this.
- Cached `GraphicRaycaster` is disabled for projection, but code can re-enable it when releasing stencil suppression. Needs Play Mode readback.
- Two interaction UI candidates exist:
  - polling-style `Hecton8.UI.InteractionUI`
  - event-driven `Hecton8.Interaction.InteractionUI`
- Static evidence does not prove either is active in `02_HECTON_WORLD`.

## Unity Owner Checklist

Read back in Play Mode:

1. active scene after handoff;
2. `BootstrapState.CurrentPlayerObject.name`, scene, active state, and tag;
3. whether current player is a prefab instance of `Player.prefab` or scene-authored YAML object;
4. components on bound player: `HectonPlayerMovement`, `PlayerInteraction`, `PlayerPDA`, `PlayerToolManager`, `PlayerInventory`, `PlayerFlashlight`, `VisorHUDController`, `HUDNotification`;
5. whether `HectonWorldShellController1428` remains enabled on active player;
6. `PlayerRuntimeContextService` binding state and movement/camera/tool/inventory/survival flags;
7. all active HUD/visor/interaction canvases and their render modes;
8. `GraphicRaycaster` enabled count on gameplay HUD canvases;
9. whether `Hecton8.UI.InteractionUI` or `Hecton8.Interaction.InteractionUI` exists/enabled.

Exercise:

- WASD/vertical movement;
- right-mouse look;
- dry/shore movement if available;
- surface swim;
- underwater swim;
- ascend/descend;
- hover/interact target;
- tool swap;
- PDA open/close;
- flashlight toggle.

Capture predicates:

- prompt visible/disappears correctly;
- HUD visible;
- PDA visible;
- quickbar visible;
- canvas render modes;
- world-space projection state;
- movement owner component actually changing pose.

## Rejection Gates

- No claim that full movement works while scene shell owns motion.
- No claim that production HUD works without runtime readback.
- No interactive gameplay `ScreenSpaceOverlay` acceptance.
- No raw scene YAML fix.
- No hot-path input/UI string/GC acceptance without profiler/GC proof.

## Current Disposition

`PENDING VERIFICATION`.
