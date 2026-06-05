# 3109 Full UI / Player Movement Owner

Status: `STATIC_SOURCE_REVIEW / PENDING PLAYMODE + PROFILER PROOF`

Evidence class: `STATIC_SOURCE`. No runtime acceptance.

## Current Verdict

Full UI/player movement remains blocked. Static source still indicates a scene-authored tagged `Player` shell with `HectonWorldShellController1428` can win over the production `Player.prefab` route. Do not claim movement, HUD, interaction prompts, PDA, camera, or playable slice acceptance until Play Mode proves the active owner.

## Mandates Followed

- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`

## Static Facts Reconfirmed

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` has `m_TagString: Player`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` binds `Hecton8.World.HectonWorldShellController1428`.
- Static scene search found no reference to:
  - `Assets/_Project/Prefabs/Player.prefab` GUID `1c4db7a430141e5408e01b6ce4ed19d7`
  - `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` GUID `e286dd44e529d8b4498750dd0abbbfd8`
  - `Assets/_Project/Prefabs/HUD_Internal.prefab` GUID `949b94e6d99fdd44ea13e320d0784005`
- `GameBootstrapper.ResolveSceneActivationReferences` can resolve a tagged scene `Player` into `playerObject`.
- `GameBootstrapper.SpawnPlayerAsync` publishes an existing `playerObject` when no spawner path wins.
- `GameBootstrapper.PublishPlayerRuntimeReference` publishes `playerObject` to `BootstrapState.CurrentPlayerObject`.
- `PlayerRuntimeContextService` binds from `BootstrapState.CurrentPlayerObject` and expects production player components.
- `HectonWorldShellController1428` owns shell movement in `Tick`, reads `Keyboard.current`/`Mouse.current`, and has legacy `Input.GetKey` / `Input.GetAxisRaw` fallbacks.
- `SuitHUDV4CanvasOverlay` disables cached `GraphicRaycaster` under stencil suppression but can re-enable it on release; runtime readback is required.
- `HUD_Internal.prefab` still serializes `forceScreenSpaceOverlay: 1`.

## State Matrix

| State | Items |
|---|---|
| Implemented in source; runtime wiring unproven | Production `HectonPlayerMovement`, `InputDispatcher`, production `Player.prefab` stack, event-driven interaction UI with `SetCharArray`. |
| Implemented but wiring unknown | Runtime production player spawn, runtime HUD spawn, `PlayerRuntimeContextService` component binding, PDA/focus route, HUD projection/world-space state. |
| Diagnostic/fake/unsafe | `HectonWorldShellController1428`, standalone scene camera until owner proven, `HUD_Internal.forceScreenSpaceOverlay`, legacy `Hecton8.UI.InteractionUI`. |
| Missing proof | Walk, swim, ascend/descend, surface/shore transition, camera/look, prompt, PDA/pause, GC/profiler evidence. |
| Forbidden acceptance | Scene-shell direct input as production, interactive gameplay `ScreenSpaceOverlay`, hot string HUD paths, scene YAML mutation, runtime claims from static source. |

## Minimum Runtime Readback Checklist

Read-only Play Mode readback must capture:

1. Active scene after bootstrap handoff.
2. `BootstrapState.CurrentPlayerObject`: name, scene path, active state, tag, instance id.
3. Prefab-instance proof or scene-authored proof for active player.
4. Active player components: `HectonPlayerMovement`, `PlayerInteraction`, `PlayerPDA`, `PlayerToolManager`, `PlayerInventory`, `PlayerFlashlight`, `VisorHUDController`, `SuitHUDPresentationController`, `HUDNotification`.
5. Any enabled `HectonWorldShellController1428`, especially on tagged `Player`.
6. `PlayerRuntimeContextService` movement/camera/tool/inventory/survival/HUD binding flags.
7. Active gameplay camera owner.
8. Active HUD/visor/interaction canvases and render modes.
9. Enabled `GraphicRaycaster` count on gameplay HUD canvases.
10. Whether `Hecton8.Interaction.InteractionUI` or `Hecton8.UI.InteractionUI` exists/enabled.
11. `InputDispatcher` active state and snapshot changes.
12. Movement exercises: dry/shore walk, surface swim, underwater swim, ascend, descend, surface/shore transition.
13. UI/focus exercises: right-mouse look, hover/interact prompt, tool state, PDA open/close, pause/focus return.
14. GCMonitor/profiler: input/HUD/prompt hot path target 0 B/frame; frame spikes named.

Proof packet target: `Docs/Screenshots/HectonProofPackets/h8_3109_{session}/` with manifest, copied Unity log, screenshots/capture, runtime readback table, and GC/profiler evidence.

## Safe Implementation Order

1. Read-only Play Mode readback. No scene mutation.
2. If production player already owns runtime, document the route and run movement/UI proof.
3. If shell wins, stop acceptance and prepare a scoped Unity-owner fix:
   - add/verify `HectonPlayerSpawner` route for `Player.prefab`;
   - prevent shell from being publishable as production player or classify it as temporary shell;
   - ensure `BootstrapState.CurrentPlayerObject` publishes the production player;
   - verify `PlayerRuntimeContextService` binds production components.
4. Re-run readback after the fix.
5. Test movement transitions only after owner proof.
6. Test HUD/prompt/PDA/focus only after player owner proof.
7. Run GC/profiler after functional proof.
8. Classify/decommission shell and legacy interaction UI only as a separate scoped cleanup after active runtime absence is proven.

## Minimum Playable Movement Slice

- Walk/interior/shore movement.
- Surface swim.
- Underwater swim.
- Ascend/descend.
- Surface/shore transition.
- Look/camera route readability.
- Interaction prompt while moving.
- Tool aim/use only if a real tool route is active.
- PDA/pause/UI focus enter and return.

## Minimum UI Slice

- Oxygen.
- Depth.
- Pressure/hull/suit risk.
- Route/return cue.
- Interaction prompt.
- Tool state.
- Warning/advisory.
- Explicit owner distinction: visor/PDA/cockpit cannot invent survival, route, tool, or movement truth.

## Zero-GC Risk Targets

- `HectonWorldShellController1428` direct input route.
- Direct Unity input polling in gameplay paths.
- `TMP_Text.text`, `SetText(string)`, interpolation, `string.Format`, `.ToString()` in HUD/update paths.
- `SetActive` toggles in active HUD paths.
- `Camera.main`, scene searches, uncached `GetComponent` in hot paths.
- `GraphicRaycaster` per-frame gameplay HUD path.

## Low / Middle / High / Ultra

- Low: production input snapshot, stable movement, readable oxygen/depth/pressure/route/tool/prompt, no direct shell input, no hot string allocation.
- Middle: richer visor material response and warning cadence only after low readability survives.
- High: richer swim/camera/tool feedback and better HUD material depth without changing command truth.
- Ultra: sensory density through continuous `GlobalQualityWeight`; no separate gameplay truth, DTO, save, or authority route.

## Process Gate

Build gate was not clean: CPU reported `100` and active `dotnet` process existed. No `dotnet build` launched.

Unity MCP resources were not mounted in this session. No Play Mode or editor readback was executed.

## Regression Model

- CPU: no runtime work added; future fix must avoid hot scene searches and duplicate player/HUD owners.
- GC: no runtime proof; target remains 0 B/frame for input/HUD/prompt hot paths.
- Memory: HUD canvas/RT/raycaster state unproven; memory claim remains pending.
- Cadence: critical prompt/cursor can be 60Hz; oxygen/depth/pressure 10Hz; slow diagnostics/event states 2Hz/event-driven.
- Correctness: shell publication is the primary correctness risk. It can make movement, UI, tools, interaction, PDA, and runtime context all bind to the wrong owner.

## Rejection Gates

- No movement acceptance while shell owns motion.
- No HUD acceptance while interactive gameplay canvas is `ScreenSpaceOverlay`.
- No prompt acceptance while legacy string UI owns runtime prompt.
- No GC/perf acceptance without GCMonitor/profiler proof.
- No scene YAML mutation.

## Next Action

Wait for clean Unity/proof lane. Execute the read-only Play Mode checklist before any code or scene change.
