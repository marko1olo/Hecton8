# Player HUD Movement P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / PENDING UNITY READBACK`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_SOURCE + STATIC_SCENE_YAML + STATIC_PREFAB_YAML`

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, or raw YAML edit was performed by this synthesis.

## Verdict

The production player/HUD/movement route is not proven active. The current scene can still be driven by a scene-local shell that bypasses intended input, movement, and camera ownership.

Any scenic capture without active production player, HUD, tool, movement, and input proof is rejected.

## P0 Blockers

1. `Assets/_Project/Scenes/02_HECTON_WORLD.unity` contains an active scene-local `Player` with `m_PrefabAsset: {fileID: 0}`, tag `Player`, and enabled `HectonWorldShellController1428`.
2. Static search does not find the production `Assets/_Project/Prefabs/Player.prefab` GUID referenced in the scene.
3. `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` reads direct input:
   - `Keyboard.current`
   - `Mouse.current`
   - fallback `Input.GetKey`
   - `Input.GetAxisRaw`
   It then writes player transform and camera rig transform. If active, this violates input owner, movement owner, and camera owner routes.
4. `Assets/_Project/Prefabs/Player.prefab` contains intended production pieces (`PlayerInteraction`, `Rigidbody`, `HectonPlayerMovement`, swim presentation, visor HUD, HUD render camera, diegetic projection mesh), but static evidence proves only candidate status, not active runtime status.
5. `Assets/_Project/Scripts/HectonPlayerMovement.cs` registers through `GlobalRegistry`, and `Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs` reads `IInputService` snapshots. Static scan found no direct input polling in intended movement/input/HUD files, but the active shell can bypass them.
6. `Assets/_Project/Prefabs/HUD_Internal.prefab` has disabled compositor plus `forceScreenSpaceOverlay: 1`.
7. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` is `ScreenSpaceOverlay` with null projection/player refs in static prefab evidence.
8. `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` has runtime projection guards, so HUD state is a readback blocker, not a proven active failure.
9. Interaction prompt ownership is ambiguous: standalone HUD prefab binds `Hecton8.Interaction.InteractionUI`, while another `Hecton8.UI.InteractionUI` exists.

## Required Unity Readback

- active `Player` objects: hierarchy path, active state, scene, tag/layer, prefab source GUID, scene-local flag, parent, enabled components;
- `BootstrapState.CurrentPlayerObject`: null/stale/path, prefab source, shell vs production prefab;
- enabled player components: `HectonWorldShellController1428`, `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, `Rigidbody`, `CapsuleCollider`, swim presentation, PDA/survival/save owners;
- dispatcher registrations: input, movement, motor, camera rig, interaction, HUD, shell; phase/lane/priority/count/object;
- input state: `InputDispatcher.ActiveRuntimeInstance`, registered `IInputService`, `PlayerInputState.MoveDelta`, `LookDelta`, `VerticalDelta`, `ActionsBitmask`, scheme hash during walk/swim/ascend/descend;
- movement/swim: walking/surface/underwater mode, immersion ratio, water surface, vertical input, intended movement, motor acceleration/pose, Rigidbody velocity;
- camera/HUD: active main camera owner, shell camera write status, prefab camera status, HUD render camera, render textures, overlay vs world/projection mode, `forceScreenSpaceOverlay`, raycast/interactivity, player refs;
- interaction/prompt: active prompt class, prompt container/label, render carrier, look target signal, interact input consumption, PDA/pause suppression;
- telemetry/proof: 300-frame rings for input/movement/HUD/focus plus GC 0 B/frame profiler proof.

## Low / Middle / High / Ultra

- Low: shell/overlay route cannot ship; production player authority must be proved.
- Middle: prove input -> movement -> motor -> camera -> HUD -> interaction.
- High: richer visor/camera effects only after owner proof.
- Ultra: diagnostics/visual overkill may scale, but authority and save identity must not change.

Final status: `P0 BLOCKED / STATIC ONLY`.
