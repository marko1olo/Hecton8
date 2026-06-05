# PLAYER UI MOVEMENT STATIC ANCHOR AUDIT 20260605

ID: RUNTIME_OWNER_02_PLAYER_UI_STATIC_ANCHOR_AUDIT
Evidence class: STATIC_SOURCE_SCAN + STATIC_PREFAB_YAML_SCAN + STATIC_DOC
Status: PENDING VERIFICATION
Unity/runtime/build/profiler claims: NONE

## What Was Wrong

- `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` is a shell/controller risk. Static source shows it implements `IUpdatable`, writes transform/camera state in `Tick`, polls `Keyboard.current`/`Mouse.current`, and has legacy `Input.GetKey`, `Input.GetMouseButton`, and `Input.GetAxisRaw` fallbacks. If active in the production route, it violates the input owner and movement authority rules.
- `Assets/_Project/Prefabs/HUD_Internal.prefab` has `forceScreenSpaceOverlay: 1` on `NASAPunk.Visor.SuitHUDScreenCompositor`. Static YAML cannot prove it is inactive. If active as gameplay HUD, it conflicts with the diegetic/visor UI floor.
- The candidate route is dense and multi-owner. Static source shows production-looking anchors for movement, motor, camera, interaction, HUD, input, pause, PDA, signals, and black-box paths, but static evidence cannot prove active scene binding, dispatcher registration, ownership order, GC, text clipping, save/load restore, or player route operation.
- `Assets/_Project/Scripts/UI/InteractionUI.cs` and `Assets/_Project/Scripts/Interaction/InteractionUI.cs` both exist. The interaction namespace version appears bound in `Suit_HUD_Canvas.prefab`; the UI namespace version remains a duplicate/suspect route until Unity readback proves which path is active.

## What Was Scanned

Task packet:
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET.md`

Root/domain bibles, targeted:
- `player.md`
- `ui.md`
- `input.md`
- `systems.md`
- `performance.md`

Mandates followed:
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

Candidate anchors scanned:
- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`
- `Assets/_Project/Prefabs/HUD_Internal.prefab`
- all source anchors listed in the CSV report.

## Static Findings

- All candidate anchors named in the packet exist.
- `Player.prefab` static YAML contains candidate production bindings including `Hecton8.Interaction.PlayerInteraction`, `Hecton8.Gameplay.HectonPlayerMovement`, `NASAPunk.Visor.VisorHUDController`, and `NASAPunk.Visor.SuitHUDPresentationController`.
- `Suit_HUD_Canvas.prefab` static YAML contains `Hecton8.UI.SuitHUDV4CanvasOverlay` and `Hecton8.Interaction.InteractionUI`.
- `HUD_Internal.prefab` static YAML contains `NASAPunk.Visor.SuitHUDScreenCompositor` and `forceScreenSpaceOverlay: 1`.
- `HectonPlayerMovement` static source shows dispatcher interfaces, `GlobalQualityWeight` reads, profiler markers, `SignalBus` presentation signals, player kinematics telemetry writes, and dump path `Docs/AgentLogs/Dump_PLAYER_KINEMATICS.bin`.
- `InputDispatcher` static source shows `InputBlackBoxCapacity = 300`, `SignalBus<InputStateSignal>`, `SignalBus<PlayerInputSignal>`, haptic request consumption, and dump path `Docs/AgentLogs/Dump_INPUT_DETERMINISM.bin`.
- Scanned HUD text anchors showed `SetCharArray` hits and no static hits for `.text =`, `SetText(`, `ToString()`, or `String.Format` in the narrowed HUD file set.
- `DiegeticPDAController` has `GraphicRaycaster.Raycast(...)` static hit. This is UI raycast API, not Physics raycast, but runtime cadence/alloc proof remains required.

## Hard Blockers

1. `HectonWorldShellController1428.cs` must be proven inactive or removed/replaced by the production player owner in a later runtime task. Static hard evidence:
   - line 10: `public sealed class HectonWorldShellController1428 : MonoBehaviour, IUpdatable`
   - line 47: `public void Tick(float deltaTime)`
   - line 57: writes `_transform.rotation`
   - lines 66-68: writes `_transform.position`
   - line 115: `Keyboard.current`
   - line 153: `Mouse.current`
   - lines 132-142: `Input.GetKey`
   - line 161: `Input.GetMouseButton`
   - lines 164-165: `Input.GetAxisRaw`
2. `HUD_Internal.prefab` must be proven non-production or replaced safely if it drives gameplay HUD. Static hard evidence:
   - line 46: `NASAPunk.Visor.SuitHUDScreenCompositor`
   - line 53: `forceScreenSpaceOverlay: 1`
3. No static scan can prove `Player.prefab`, `Suit_HUD_Canvas.prefab`, `InputDispatcher`, `PlayerRuntimeContextService`, or the production camera/motor are active in `02_HECTON_WORLD`.

## Next Owner

Next owner: Unity/runtime readback owner for player/UI binding.

Required next proof:
- active tagged `Player` object and prefab source;
- active `BootstrapState.CurrentPlayerObject`;
- enabled components on production player versus shell controller;
- dispatcher registrations for movement, motor, camera, interaction, HUD, PDA, pause, rebinding, and input;
- HUD render mode/readback for `HUD_Internal` and `Suit_HUD_Canvas`;
- input device matrix and direct-input blocker status;
- 300-frame and 60-second GC/profiler proof for input, movement, interaction, HUD text, PDA/pause navigation, and camera visual sync;
- screenshot proof at 720p, 16:9, 16:10, 21:9, and 4:3;
- save/load restore proof for player route state;
- black-box dump manifest for input/player/UI critical state.

## Regression Model

- CPU: Static source shows candidate profiler/dispatcher routes, but no runtime timing exists. Any active shell path is suspect because it performs direct input reads and transform writes in `Tick`.
- GC: HUD text static scan is favorable for `SetCharArray`, but no GCMonitor/Profiler data exists. Status remains PENDING VERIFICATION.
- Memory: Input and movement black-box paths reference DataVault/native buffers and dump payloads, but no memory slope or allocation proof exists.
- Cadence: Production anchors expose dispatcher interfaces; shell route uses `IUpdatable` environment lane and direct polling. Active route phase order must be proved in Unity.
- Correctness: Production ownership appears plausible statically. Runtime authority remains unproven until the active player, input owner, movement owner, camera owner, HUD owner, and shell blocker are read back in Play Mode/player run.

## Low/Middle/High/Ultra Consequences

- Low: Must prove production player route, readable movement, zero-GC HUD text, and no active shell direct input. No cheap overlay-only HUD acceptance.
- Middle: Must prove full route UI and input semantics through `InputDispatcher` snapshots and production interaction prompt.
- High: May spend saved budget on richer visor/camera/haptic presentation only after owner/cadence proof.
- Ultra: Visual-sync overkill only. No gameplay truth, action ids, DTO layout, or route ownership may change by quality weight.

## Status

PENDING VERIFICATION. This audit created static evidence only. It did not run Unity, Play Mode, build, profiler, GCMonitor, screenshots, save/load, or scene readback.
