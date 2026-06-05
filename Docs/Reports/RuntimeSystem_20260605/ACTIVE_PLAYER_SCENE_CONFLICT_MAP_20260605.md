# Active Player Scene Conflict Map - 2026-06-05

ID: `RUNTIME_OWNER_03_ACTIVE_PLAYER_SCENE_CONFLICT_MAP_WORKER`
Status: `PENDING_VERIFICATION`
Evidence class: `STATIC_SCENE_YAML_SCAN + STATIC_PREFAB_YAML_SCAN + STATIC_PRIOR_REPORT_SCAN + STATIC_DOC`
Unity/runtime/build/profiler claims: `NONE`

No Unity launch, Play Mode, player build, profiler, GCMonitor, screenshot, source edit, asset edit, scene save, prefab edit, or YAML mutation was performed.

## Scope

This file maps the active scene-local `Player` serialized in `Assets/_Project/Scenes/02_HECTON_WORLD.unity` against the production `Assets/_Project/Prefabs/Player.prefab` route. It does not repair the conflict.

Static YAML evidence is not runtime proof, but the scene-local enabled shell is a blocker until Unity readback proves the object is not the active production player route or removes it through an approved Unity API mutation path.

## Inputs Read

- `Docs/Reports/RuntimeSystem_20260605/SHELL_HUD_SCENE_BINDING_ESCALATION_20260605.md`
- `Docs/Reports/RuntimeSystem_20260605/SHELL_HUD_SCENE_BINDING_ESCALATION_20260605.csv`
- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md`
- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.csv`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET.md`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` targeted line ranges and searches only
- `Assets/_Project/Prefabs/Player.prefab` targeted line ranges and searches only
- `Assets/_Project/Prefabs/Player.prefab.meta`
- `Assets/_Project/Prefabs/HUD_Internal.prefab` targeted line range only
- `player.md`, `input.md`, `systems.md`

## Mandates Followed

- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Primary Static Finding

`02_HECTON_WORLD.unity` contains exactly one static `m_Name: Player`, exactly one static `m_TagString: Player`, and exactly one static `HectonWorldShellController1428` hit. That object is scene-local, active, tagged `Player`, and has an enabled shell movement/camera controller.

Scene evidence:

- lines `70213-70218`: `GameObject` fileID `1568927387`, with `m_CorrespondingSourceObject: {fileID: 0}`, `m_PrefabInstance: {fileID: 0}`, and `m_PrefabAsset: {fileID: 0}`.
- lines `70220-70225`: component list contains Transform `1568927388`, shell `1568927389`, `FakeRadarBlipController` `1568927392`, `DeepPsychosisController` `1568927391`, and `PlayerStressVFX` `1568927390`.
- lines `70227-70232`: `m_Name: Player`, `m_TagString: Player`, `m_IsActive: 1`.
- lines `70241-70246`: scene-local player transform is at `{-2.4, 15.72, 126.55}`, scaled `0.72`, parented to fileID `344018309`.
- lines `70248-70260`: `Hecton8.World.HectonWorldShellController1428` is serialized on the same object, `m_Enabled: 1`, with `cameraRig: {fileID: 1505808849}`.
- lines `70261-70264`: shell movement values are serialized: `moveSpeed: 8`, `verticalSpeed: 4.5`, `lookSpeed: 0.11`, `idleDriftMeters: 0.08`.

The prior static source audit records that `HectonWorldShellController1428` implements `IUpdatable`, writes transform/camera state in `Tick`, and polls direct Unity input APIs. If this scene-local object is active at runtime, it conflicts with the production input, movement, and camera ownership route.

## Scene Camera Binding

The scene shell's `cameraRig` reference resolves statically to the scene `Main Camera` transform:

- lines `67070-67087`: scene `Main Camera` object is active, tagged `MainCamera`, with components `1505808849` Transform and `1505808848` Camera.
- lines `67096-67145`: scene `Main Camera` Camera component is enabled.
- lines `67147-67160`: Transform fileID `1505808849` belongs to the scene `Main Camera`.
- line `70260`: shell `cameraRig` references fileID `1505808849`.

This is static binding evidence only. Unity readback must prove whether the shell writes this camera during the active route and whether any production camera owner is also active.

## Production Player Prefab Route

`Assets/_Project/Prefabs/Player.prefab` is a production-looking candidate route, but no targeted scene search found its asset GUID in `02_HECTON_WORLD.unity`.

Prefab evidence:

- `Player.prefab.meta` line `2`: prefab GUID is `1c4db7a430141e5408e01b6ce4ed19d7`.
- targeted `rg` search for `1c4db7a430141e5408e01b6ce4ed19d7` in `02_HECTON_WORLD.unity`: no hits.
- `Player.prefab` lines `880-914`: root component list includes production player components.
- lines `915-920`: prefab root name `Player`, tag `Player`, active.
- lines `958-980`: enabled `Hecton8.Interaction.PlayerInteraction`, `reachDistance: 3.5`, `targetProbeInterval: 0.1`, `interactableMask.m_Bits: 8`, and `playerCamera: {fileID: 2887721374667799330}`.
- lines `981-1008`: root `Rigidbody` exists, `m_UseGravity: 0`, `m_IsKinematic: 0`, `m_Interpolate: 1`, `m_Constraints: 112`, `m_CollisionDetection: 3`.
- lines `1008-1021`: enabled `Hecton8.Gameplay.HectonPlayerMovement`, with `playerCamera: {fileID: 198848781283951679}` and `waterSurfaceY: 4900`.

Prefab camera/HUD evidence:

- lines `280-295`: prefab child `Main Camera` is active and tagged `MainCamera`.
- lines `296-320`: prefab `Main Camera` Transform is fileID `198848781283951679`, parented under the prefab root.
- lines `321-328`: prefab `Main Camera` Camera component is fileID `2887721374667799330`, enabled.
- lines `2890-2906`: enabled `NASAPunk.Visor.VisorHUDController`; `_hudCamera` references fileID `4738717749101629275`, `_referenceCamera` references fileID `2887721374667799330`, and `_sharedRenderTexture` is assigned.
- lines `3540-3553`: prefab `HUD_Render_Camera` object is active on layer `6`.
- lines `3569-3619`: `HUD_Render_Camera` Camera component fileID `4738717749101629275` is serialized `m_Enabled: 0`, with culling mask `131072`.
- lines `3671-3676`: enabled `HectonSuitHUDExtensions` references `hudCamera: {fileID: 4738717749101629275}`.
- lines `3688-3705`: enabled `NASAPunk.Visor.SuitHUDPresentationController`, `presentationMode: 2`, `visorController` bound, `overlayPresentationCamera` points at the main camera component, `visorProjectionCamera` points at `HUD_Render_Camera`, and `screenCompositor: {fileID: 0}`.

This proves candidate production bindings exist in the prefab. It does not prove the prefab is instantiated, active, or selected by `BootstrapState.CurrentPlayerObject` in `02_HECTON_WORLD`.

## Secondary HUD_Internal Blocker

`HUD_Internal` is secondary to the active player scene conflict. It is not the main scene player map.

Static HUD evidence:

- `HUD_Internal.prefab` lines `14-19`: prefab object `HUD_Internal`, active.
- lines `35-46`: `NASAPunk.Visor.SuitHUDScreenCompositor` exists.
- line `42`: compositor is serialized disabled.
- line `53`: `forceScreenSpaceOverlay: 1`.
- targeted scene and `Player.prefab` search found `HUD_Internal` and `forceScreenSpaceOverlay` only in `HUD_Internal.prefab`, not in `02_HECTON_WORLD.unity` or `Player.prefab`.

This remains a latent overlay blocker. If Unity readback proves it is enabled, cloned, or driving interactive gameplay HUD, it blocks HUD acceptance. If it is inactive or a noninteractive bridge, it stays secondary.

## Conflict Map

| Subject | Static evidence | Scene object status | Prefab route status | Conflict risk | Required Unity readback | Next owner | Status |
|---|---|---|---|---|---|---|---|
| Scene-local Player | `02_HECTON_WORLD.unity` lines `70213-70232` | Active scene-local `Player`, tag `Player`, not prefab instance | Production prefab not proven in scene | Active scene object can win player identity before prefab route | Active tagged player object, hierarchy path, prefab source, `BootstrapState.CurrentPlayerObject` | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| Scene shell movement/camera | lines `70248-70264` | Enabled `HectonWorldShellController1428` on scene-local `Player` | Production movement exists only as prefab candidate | Duplicate or winning movement/input/camera authority | Enabled components, dispatcher lane, shell registration, transform/camera writes | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| Scene shell cameraRig | lines `67070-67160`, `70260` | Shell references scene `Main Camera` Transform `1505808849` | Prefab owns separate child camera route | Shell may move scene camera while production camera route is absent or inactive | Active camera owner, camera write owner, visual-sync phase | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| Production Player.prefab | `Player.prefab.meta` GUID no scene hits; prefab lines `915-920` | No static scene instance by prefab GUID | Active tagged prefab root exists as asset | Production route may not be loaded in active scene | Prefab instance source, spawn/bootstrap route, active root object | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| PlayerInteraction | prefab lines `958-980` | Not visible on scene-local `Player` YAML block | Enabled prefab component with camera ref | Interact route may be absent while shell player is active | Active interaction component, input snapshot source, prompt route | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| Rigidbody | prefab lines `981-1008` | Not visible on scene-local `Player` YAML block | Prefab root has dynamic Rigidbody | Scene shell player may bypass production body/motor truth | Active Rigidbody, motor owner, collision mode, save/load position source | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| HectonPlayerMovement | prefab lines `1008-1021` | Not visible on scene-local `Player` YAML block | Enabled prefab movement owner candidate | Production movement may not run or may coexist with shell | Dispatcher phase, input source, movement owner, shell coexistence | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| Production cameras/HUD | prefab lines `280-328`, `2890-2906`, `3540-3705` | Scene shell uses scene camera, not prefab camera | Prefab has main camera, HUD camera, visor/presentation bindings | HUD/camera route may be absent from scene or duplicated | Active camera stack, HUD camera state, visor controller, presentation mode | Unity/runtime readback owner | `PENDING_VERIFICATION` |
| HUD_Internal latent overlay | `HUD_Internal.prefab` lines `14-19`, `35-53` | No scene hit in targeted search | Not bound in `Player.prefab`; standalone prefab blocker | Secondary HUD overlay risk if enabled/cloned | Active HUD stack, render mode, interactivity, clone status | Unity/runtime readback owner | `PENDING_VERIFICATION` |

## First-20 Route Impact

This conflict blocks acceptance for the first-20 route moment `world load -> walk/swim/orient -> interact -> HUD warning/prompt -> save/load return`.

- Walk: scene shell may move a transform/camera without production body, stance, input snapshot, or save identity.
- Swim: production `HectonPlayerMovement` surface/swim parameters exist in the prefab, but the active scene-local `Player` does not statically show that component.
- Interact: production `PlayerInteraction` exists in the prefab, but the active scene-local `Player` does not statically show it.
- HUD: prefab visor/HUD route exists, but the scene shell camera and absent prefab instance make active HUD ownership unproven; `HUD_Internal` remains a secondary overlay blocker.
- Save/load return: scene-local shell player identity versus prefab player identity can corrupt restored position/state ownership until `BootstrapState.CurrentPlayerObject` and active save owner are read back.

## GlobalQualityWeight Limits

`GlobalQualityWeight` is continuous. Low/Middle/High/Ultra below are review labels, not binary branches.

- Low: player authority must still be production-owned. Compact-tier cannot accept shell movement, missing interaction, or overlay-only gameplay HUD. It may reduce cadence and presentation richness only after owner proof exists.
- Middle: movement, swim, interaction, camera, HUD, PDA/pause, and save/load return must share one proven owner chain and dispatcher phase order.
- High: saved budget may improve visor material response, camera visual sync, haptics, warning degradation, and prompt presentation only in `VISUAL_SYNC`.
- Ultra: visual overkill only. Quality weight must not change player authority, action ids, DTO layout, save identity, HUD fact ownership, or production route selection.

Allowed scaling: cadence, optional diagnostics, smoothing depth, HUD material richness, haptic layering, and presentation density.

Rejected scaling: owner route, action semantics, movement truth, interaction result, oxygen/depth facts, save layout, DTO identity, black-box field identity.

## Required Unity Readback

Future Unity/runtime owner must read back without raw YAML mutation:

1. Active tagged `Player` object path, prefab source, scene-local status, and `BootstrapState.CurrentPlayerObject`.
2. Enabled components on the active `Player`, including shell, production movement, interaction, Rigidbody, motor, camera, survival, HUD, and save owners.
3. Dispatcher registration for `HectonWorldShellController1428`, `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, HUD, PDA, pause, and input.
4. Active input source: prove `InputDispatcher` is the only gameplay input polling owner and shell direct input is not in the active production route.
5. Active camera owner and phase: prove camera writes are presentation-only and not movement truth.
6. Active HUD stack: `HUD_Internal`, `Suit_HUD_Canvas`, `SuitHUDV4CanvasOverlay`, `InteractionUI`, `VisorHUDController`, and `SuitHUDPresentationController`.
7. HUD render modes and interactivity: reject any active interactive first-party gameplay HUD using `ScreenSpaceOverlay`.
8. GC/profiler proof for movement, input, interaction probe, HUD text, PDA/pause navigation, camera visual sync, and telemetry.
9. Save/load return proof for restored player position/state and route identity.
10. Black-box 300-frame coverage for input/player/UI critical state.

## Mutation Guard

Do not propose raw YAML edits. Future mutation must happen only through Unity API or an approved editor-script route after a clean process gate and readback. If the scene-local shell is active in production, do not patch movement inside the shell. Replace or disable the shell through the production player route after the active owner is proven.

## Regression Model

- CPU: static mapping only. Runtime risk is duplicate movement/input/camera work if shell and production route coexist.
- GC: static mapping only. No `0 B/frame` claim exists. Shell direct input and HUD text require runtime GC proof.
- Memory: static mapping only. Prefab HUD render texture and `HUD_Internal` compositor status require active-stack readback.
- Cadence: scene shell `IUpdatable` route conflicts with required phase-owned input, simulation, post-simulation, and visual-sync player route.
- Correctness: active scene-local `Player` can defeat production prefab identity, interaction, Rigidbody, movement, camera, HUD, and save/load ownership until Unity readback proves otherwise.

Final status: `PENDING_VERIFICATION`.
