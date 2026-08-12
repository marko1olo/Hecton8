# Status 3109 - Full UI / Player Movement

Status: `STATIC VERIFIED / PENDING PLAYMODE + PROFILER PROOF`

Active blocker: `02_HECTON_WORLD` static source still suggests a tagged scene-authored `Player` shell with `HectonWorldShellController1428` can be resolved by `GameBootstrapper` before the production `Player.prefab` stack is proven active.

## Mandates Followed

- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`

## Done This Pass

- Re-read active 3109 authority: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, `quality.md`, `gameplay.md`, `player.md`, `input.md`, `camera.md`, `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `survival.md`, `tools.md`, `accessibility.md`, task file, blocker report, and current 3109 report.
- Rechecked process gate: CPU load reported `100`; active `dotnet` process found. No build launched.
- Checked Unity MCP availability through MCP resources. No Unity MCP resources mounted in this session; no Play Mode readback executed.
- Reconfirmed static blocker:
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity` contains `m_TagString: Player`.
  - Same scene binds `Hecton8.World.HectonWorldShellController1428`.
  - Same scene does not statically reference production `Player.prefab`, `Suit_HUD_Canvas.prefab`, or `HUD_Internal.prefab` GUIDs.
- Read narrow code paths:
  - `GameBootstrapper.ResolveSceneActivationReferences` can resolve a tagged scene `Player`.
  - `GameBootstrapper.SpawnPlayerAsync` returns early to existing `playerObject` when no spawner path wins.
  - `GameBootstrapper.PublishPlayerRuntimeReference` publishes the current `playerObject` through `BootstrapState`.
  - `PlayerRuntimeContextService` binds from `BootstrapState.CurrentPlayerObject`.
  - `HectonWorldShellController1428` ticks shell movement and reads direct keyboard/mouse or legacy input.
  - `SuitHUDV4CanvasOverlay` can re-enable a cached `GraphicRaycaster` when stencil suppression releases; runtime readback is required.
- Updated `Docs/Reports/Batch31/3109_FULL_UI_PLAYER_MOVEMENT_OWNER.md`.
- Updated `Docs/AgentLogs/Rationale_3109.md`.
- Updated `Docs/AgentLogs/LOG_3109.md`.

## State Matrix

`Implemented and likely wired`:
- Production movement owner exists in `HectonPlayerMovement`.
- Input snapshot owner exists in `InputDispatcher`.
- Production prefab contains movement, interaction, PDA, tool, visor/HUD presentation stack.
- Event-driven interaction UI path exists and uses `SetCharArray`.

`Implemented but wiring unknown`:
- `Player.prefab` runtime spawn/bind in `02_HECTON_WORLD`.
- `Suit_HUD_Canvas.prefab` and `HUD_Internal.prefab` runtime activation.
- `PlayerRuntimeContextService` binding to production player instead of shell.
- HUD projection/world-space route after stencil suppression transitions.
- PDA/pause/focus ownership under runtime movement.

`Diagnostic/fake/unsafe`:
- `HectonWorldShellController1428` scene shell movement.
- Scene-authored standalone `Main Camera` until runtime camera ownership is read back.
- `HUD_Internal.prefab` serialized `forceScreenSpaceOverlay: 1` until proven diagnostic/bridge-only.
- Legacy `Hecton8.UI.InteractionUI` until active scene absence is proven.

`Missing proof`:
- Walk/interior/shore movement.
- Surface swim.
- Underwater swim.
- Ascend/descend.
- Shore/surface transition.
- Interaction prompt visibility while moving.
- PDA/pause focus handoff.
- GCMonitor/profiler proof for input/HUD/prompt hot paths.

`Forbidden unless explicitly isolated`:
- Accepting scene-shell direct input as production movement.
- Interactive first-party gameplay HUD as `ScreenSpaceOverlay`.
- Runtime string prompt/formatting paths in HUD.
- Scene YAML mutation to fix player/HUD binding.
- Any movement/UI acceptance without Play Mode + GC/profiler evidence.

## Runtime Readback Checklist

Read-only Play Mode proof must capture:

1. Active scene after bootstrap handoff.
2. `BootstrapState.CurrentPlayerObject`: name, scene path, active state, tag, instance id.
3. Whether current player is an instance of `Assets/_Project/Prefabs/Player.prefab` or a scene-authored YAML object.
4. Active player components: `HectonPlayerMovement`, `PlayerInteraction`, `PlayerPDA`, `PlayerToolManager`, `PlayerInventory`, `PlayerFlashlight`, `VisorHUDController`, `SuitHUDPresentationController`, `HUDNotification`.
5. Whether `HectonWorldShellController1428` exists/enabled on the active player or any tagged `Player`.
6. `PlayerRuntimeContextService` binding flags for movement, camera, tool, inventory, survival, HUD notification.
7. All active cameras controlling gameplay view; identify scene `Main Camera` versus production player/visor camera.
8. All active HUD/visor/interaction canvases and render modes.
9. Count of enabled `GraphicRaycaster` on gameplay HUD canvases.
10. Whether `Hecton8.Interaction.InteractionUI` or `Hecton8.UI.InteractionUI` exists/enabled.
11. Input owner: `InputDispatcher` active state, current snapshot changes, no direct gameplay polling path owning motion.
12. Exercise route: dry/shore walk, surface swim, underwater swim, ascend, descend, shore/surface transition, right-mouse look, hover/interact prompt, tool swap/use if real route exists, PDA open/close, pause/focus return.
13. Capture predicates: HUD visible, oxygen/depth/pressure/route/tool/warning state visible or explicit unavailable owner fault shown, prompt appears/disappears, PDA visible, quickbar/tool state visible, movement owner changes pose.
14. GCMonitor/profiler: 0 B/frame target for hot input/HUD/prompt route; frame spikes named if present.

Proof packet target: `Docs/Screenshots/HectonProofPackets/h8_3109_{session}/` with manifest, copied Unity log, runtime readback table, screenshots/capture, and profiler/GC evidence.

## Safe Implementation Order

1. Runtime readback only. Do not mutate scene.
2. If production player is already active, document exact bootstrap route and run movement/UI proof.
3. If scene shell wins, stop acceptance and prepare a scoped Unity-owner fix:
   - add/verify a scene or bootstrap `HectonPlayerSpawner` route for `Player.prefab`;
   - ensure `GameBootstrapper` rejects the shell as publishable player or classifies it as temporary runtime shell;
   - publish only production player through `BootstrapState`;
   - keep `PlayerRuntimeContextService` binding to production player components.
4. Re-run runtime readback after any fix.
5. Only after production player/HUD binding is proven, test locomotion transitions and UI focus.
6. Only after movement/UI repro passes, run GC/profiler proof.
7. Only after proof, classify legacy UI and shell cleanup as separate scoped tasks.

## Low / Middle / High / Ultra Consequences

- Low: stable production input snapshot, readable oxygen/depth/pressure/route/tool/prompt, no direct shell input, no string hot allocations.
- Middle: richer visor material and warning response only after low route remains readable.
- High: richer swim/camera/tool feedback, longer HUD/camera polish, no gameplay truth change.
- Ultra: sensory density through continuous `GlobalQualityWeight`; no separate player/HUD authority route.

## Next

- Wait for process gate or Unity owner lane to become clean.
- Execute read-only Play Mode readback.
- Do not claim movement/UI works until proof packet exists.
