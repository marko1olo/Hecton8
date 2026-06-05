# Player HUD Movement P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / PENDING UNITY READBACK`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_SOURCE + STATIC_SCENE_YAML + STATIC_PREFAB_YAML`

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, or raw YAML edit was performed by this synthesis.

## Verdict

The production player/HUD/movement route is not proven active. Static scene evidence says `02_HECTON_WORLD` does not instantiate the production `Player.prefab`, `HUD_Internal.prefab`, or `Suit_HUD_Canvas.prefab`. The active scene object named `Player` is a local shell with stress/radar/audio presentation components, not movement/input/camera authority.

Any scenic capture without active production player, HUD, tool, movement, and input proof is rejected.

## P0 Blockers

1. `Assets/_Project/Scenes/02_HECTON_WORLD.unity:70213-70345` contains an active scene-local `Player` with `m_PrefabAsset: {fileID: 0}`, tag `Player`, and enabled `HectonWorldShellController1428`.
2. The active scene-local `Player` has `HectonWorldShellController1428`, `PlayerStressVFX`, `DeepPsychosisController`, and `FakeRadarBlipController`. It does not statically show `HectonPlayerMovement`, `PlayerInteraction`, `Rigidbody`, production camera rig, visor HUD owner, or HUD projection owner.
3. Current `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs:8-15` is a legacy marker only. It has serialized movement-looking fields, but no `Update`, dispatcher tick, input read, camera write, transform write, or authority route. It cannot satisfy walking/swimming/camera/input acceptance.
4. Static scene search does not find these production GUIDs in `02_HECTON_WORLD.unity`: `Player.prefab` `1c4db7a430141e5408e01b6ce4ed19d7`, `HUD_Internal.prefab` `949b94e6d99fdd44ea13e320d0784005`, `Suit_HUD_Canvas.prefab` `e286dd44e529d8b4498750dd0abbbfd8`, `HectonPlayerMovement` `6d195933dec89b14ebbfa47a621ac549`, or `PlayerInteraction` `215f6ea2a912636499ffc2dda9bdfb9d`.
5. `Assets/_Project/Prefabs/Player.prefab` contains intended production pieces (`PlayerInteraction`, `Rigidbody`, `HectonPlayerMovement`, swim presentation/blockout, visor HUD, HUD camera, and HUD presentation scripts), but static evidence proves candidate status only.
6. `Assets/_Project/Scripts/HectonPlayerMovement.cs` registers through dispatcher/`GlobalRegistry` and consumes `IInputService` snapshots. `Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs:13-54` is a zero-allocation snapshot reader. This is the intended route, but it is not proven active in the scene.
7. `Assets/_Project/Scripts/Core/InputDispatcher.cs` is the intended frame-cached input service, with cached state return at `:651-654`, pre-simulation capture around `:2822-2895`, and block-mask application around `:2944-2964`.
8. `Assets/_Project/Prefabs/Player.prefab` has candidate HUD/PDA nulls: `PlayerPDA` has `pdaPanel: {fileID: 0}` and `pdaCanvasGroup: {fileID: 0}` around lines `1589-1591`; `SuitHUDPresentationController` has `overlayModernHud: {fileID: 0}`, `projectedModernHud: {fileID: 0}`, `canvasOverlay: {fileID: 0}`, `projectionSourceOverlay: {fileID: 0}`, and `screenCompositor: {fileID: 0}` around lines `3692-3704`.
9. Candidate `Player.prefab` HUD camera path is not statically accepted: `VisorHUDController` has refs, but `HUD_Render_Camera` is disabled in subagent static readback and the presentation controller debug label says `ModernProjectedSharedRT -> FallbackOverlay`.
10. `Assets/_Project/Prefabs/HUD_Internal.prefab:42-53` has disabled `SuitHUDScreenCompositor`, null `targetCanvas`, null `visorController`, and `forceScreenSpaceOverlay: 1`.
11. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2390-2426` is `ScreenSpaceOverlay`, has `SuitHUDV4CanvasOverlay`, null `projectionCamera`, null `survival`, null `playerMovement`, and null `underwaterVisuals`. It can be a bridge candidate only, not a proven diegetic HUD.
12. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` binds `Hecton8.Interaction.InteractionUI`, while `Assets/_Project/Scripts/UI/InteractionUI.cs` also exists. Prompt ownership needs readback.
13. `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` has fallback prompt `"OPEN HATCH"` in static subagent readback. If a target does not provide text, prompt semantics leak wrong diegetic instruction.

## Required Unity Readback

- active `Player` objects: hierarchy path, active state, scene, tag/layer, prefab source GUID, scene-local flag, parent, enabled components;
- runtime owner that instantiates or binds the production `Player.prefab`, if any;
- `BootstrapState.CurrentPlayerObject`: null/stale/path, prefab source, shell vs production prefab;
- enabled player components: `HectonWorldShellController1428`, `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, `Rigidbody`, `CapsuleCollider`, swim presentation, PDA/survival/save owners;
- dispatcher registrations: input, movement, motor, camera rig, interaction, HUD, shell; phase/lane/priority/count/object;
- input state: `InputDispatcher.ActiveRuntimeInstance`, registered `IInputService`, `PlayerInputState.MoveDelta`, `LookDelta`, `VerticalDelta`, `ActionsBitmask`, scheme hash during walk/swim/ascend/descend;
- movement/swim: walking/surface/underwater mode, immersion ratio, water surface, vertical input, intended movement, motor acceleration/pose, Rigidbody velocity;
- camera/HUD: active main camera owner, shell camera write status, prefab camera status, HUD render camera, render textures, overlay vs world/projection mode, `forceScreenSpaceOverlay`, raycast/interactivity, player refs;
- interaction/prompt: `PlayerInteraction.interactableMask`, player camera, active target, prompt hash, prompt source text, active prompt class, prompt container/label, render carrier, look target signal, interact input consumption, PDA/pause suppression;
- telemetry/proof: 300-frame rings for input/movement/HUD/focus plus GC 0 B/frame profiler proof.

## Low / Middle / High / Ultra

- Low: shell/overlay route cannot ship; production player authority must be proved.
- Middle: prove input -> movement -> motor -> camera -> HUD -> interaction.
- High: richer visor/camera effects only after owner proof.
- Ultra: diagnostics/visual overkill may scale, but authority and save identity must not change.

Final status: `P0 BLOCKED / STATIC ONLY`.
