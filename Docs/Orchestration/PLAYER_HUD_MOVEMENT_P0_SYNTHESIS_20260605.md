# Player HUD Movement P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / PENDING UNITY READBACK`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_SOURCE + STATIC_SCENE_YAML + STATIC_PREFAB_YAML`
Subagent source: Chandrasekhar, Russell, and Avicenna static audits.

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, or raw YAML edit was performed by this synthesis.

## CORRECTION 2026-07-29 — the scene evidence in this document is invalid

Retested with `python -B Tools/SceneGuidReachability.py` (byte-aware, control-validated). Three facts kill
the scene half of this synthesis:

1. **`02_HECTON_WORLD.unity` is a BINARY scene.** It has no `%YAML` header, contains `m_Script` zero times,
   and contains the string `m_PrefabAsset` zero times. It has 2100 newline bytes in total, so the line
   citations below (`:70213-70345`, `:2390-2428`) cannot refer to it. `Assets/_Recovery/0 (2).unity` is
   `%YAML`, has 78,446 lines and does contain `HectonWorldShellController1428` — `_Recovery/` is gitignored
   and holds full scene copies. The scene evidence in this document was, on the balance of the byte
   evidence, read from a `_Recovery/` text copy and attributed to the live world scene.
2. **`Player.prefab` IS referenced by the live world scene.** GUID `1c4db7a430141e5408e01b6ce4ed19d7`
   occurs once in `02_HECTON_WORLD.unity` as a genuine `FileIdentifier` external-reference entry
   (offset 566551, type 3, nibble-swapped byte order). Every "GUID absent from the scene" line below is
   false for this GUID. A text search cannot match a binary scene and returns a confident false negative.
3. **The `HectonPlayerMovement` / `PlayerInteraction` scene-absence observation was read backwards.** Both
   GUIDs resolve only to `Assets/_Project/Prefabs/Player.prefab`. A component carried by a prefab
   *instance* emits no scene-level entry unless overridden, so their absence from the scene is the
   *expected signature of a correct prefab instance*, not evidence against one.

What survives: `HUD_Internal.prefab` (`949b94e6d99fdd44ea13e320d0784005`) and `Suit_HUD_Canvas.prefab`
(`e286dd44e529d8b4498750dd0abbbfd8`) are genuinely absent from every live scene and prefab, confirmed by
the same byte-aware scan. The prefab-internal null-reference findings were read from real text prefabs and
are untouched by this correction.

Still unresolved and NOT claimed here: whether the scene's reference to `Player.prefab` is a
`PrefabInstance.m_SourcePrefab` binding or a serialized field pointing at the prefab asset. Byte evidence
cannot separate those two; that needs Unity readback. So the document's *conclusion* is not proven false —
only the evidence it rested on is.

Also stale: `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` no longer exists anywhere
under `Assets` (the class name survives in the binary scene's type tree and as a string in
`BootstrapState.cs`), and `Tools/ValidatePlayerRouteStaticEvidence.py --require-production-static` now
returns `blockers=1` (`missing-file`), not the `blockers=19 notes=4` recorded below. That validator's own
scene checks are text substring tests against this binary scene and can never evaluate true — see
`Docs/AgentLogs/2026-07-29_REACHABILITY_CLAIM_RETEST.md`.

## Verdict

The production player/HUD/movement route is not proven active. ~~Static scene evidence says `02_HECTON_WORLD` does not instantiate the production `Player.prefab`, `HUD_Internal.prefab`, or `Suit_HUD_Canvas.prefab`.~~ **CORRECTED 2026-07-29: the `Player.prefab` half is false — its GUID is present in the binary world scene. The `HUD_Internal.prefab` and `Suit_HUD_Canvas.prefab` half holds.** The active scene object named `Player` is a local shell with stress/radar/audio presentation components, not movement/input/camera authority.

Any scenic capture without active production player, HUD, tool, movement, and input proof is rejected.

## P0 Blockers

1. `Assets/_Project/Scenes/02_HECTON_WORLD.unity:70213-70345` contains an active scene-local `Player` with `m_PrefabAsset: {fileID: 0}`, tag `Player`, and enabled `HectonWorldShellController1428`.
2. The active scene-local `Player` has `HectonWorldShellController1428`, `PlayerStressVFX`, `DeepPsychosisController`, and `FakeRadarBlipController`. It does not statically show `HectonPlayerMovement`, `PlayerInteraction`, `Rigidbody`, production camera rig, visor HUD owner, or HUD projection owner.
3. Current `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs:8-15` is a legacy marker only. It has serialized movement-looking fields, but no `Update`, dispatcher tick, input read, camera write, transform write, or authority route. It cannot satisfy walking/swimming/camera/input acceptance.
4. ~~Static scene search does not find these production GUIDs in `02_HECTON_WORLD.unity`: `Player.prefab` `1c4db7a430141e5408e01b6ce4ed19d7`, `HUD_Internal.prefab` `949b94e6d99fdd44ea13e320d0784005`, `Suit_HUD_Canvas.prefab` `e286dd44e529d8b4498750dd0abbbfd8`, `HectonPlayerMovement` `6d195933dec89b14ebbfa47a621ac549`, or `PlayerInteraction` `215f6ea2a912636499ffc2dda9bdfb9d`.~~ **RETRACTED 2026-07-29 — "static scene search" was a text search against a binary scene.** Byte-aware retest: `Player.prefab` **IS present** in `02_HECTON_WORLD.unity`; `HectonPlayerMovement` and `PlayerInteraction` resolve to `Player.prefab` and are prefab-borne, so their scene absence is expected rather than incriminating; only `HUD_Internal.prefab` and `Suit_HUD_Canvas.prefab` are genuinely absent.
5. `Assets/_Project/Prefabs/Player.prefab` contains intended production pieces (`PlayerInteraction`, `Rigidbody`, `HectonPlayerMovement`, swim presentation/blockout, visor HUD, HUD camera, and HUD presentation scripts), but static evidence proves candidate status only.
6. `Assets/_Project/Scripts/HectonPlayerMovement.cs` registers through dispatcher/`GlobalRegistry` and consumes `IInputService` snapshots. `Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs:13-54` is a zero-allocation snapshot reader. This is the intended route, but it is not proven active in the scene.
7. `Assets/_Project/Scripts/Core/InputDispatcher.cs` is the intended frame-cached input service, with cached state return at `:651-654`, pre-simulation capture around `:2822-2895`, and block-mask application around `:2944-2964`.
8. `Assets/_Project/Prefabs/Player.prefab` has candidate HUD/PDA nulls: `PlayerPDA` has `pdaPanel: {fileID: 0}` and `pdaCanvasGroup: {fileID: 0}` around lines `1589-1591`; `SuitHUDPresentationController` has `overlayModernHud: {fileID: 0}`, `projectedModernHud: {fileID: 0}`, `canvasOverlay: {fileID: 0}`, `projectionSourceOverlay: {fileID: 0}`, and `screenCompositor: {fileID: 0}` around lines `3692-3704`.
9. Candidate `Player.prefab` HUD camera path is not statically accepted: `VisorHUDController` has refs, but `HUD_Render_Camera` is disabled in subagent static readback and the presentation controller debug label says `ModernProjectedSharedRT -> FallbackOverlay`.
10. `Assets/_Project/Prefabs/HUD_Internal.prefab:42-53` has disabled `SuitHUDScreenCompositor`, null `targetCanvas`, null `visorController`, and `forceScreenSpaceOverlay: 1`.
11. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2390-2426` is `ScreenSpaceOverlay`, has `SuitHUDV4CanvasOverlay`, null `projectionCamera`, null `survival`, null `playerMovement`, and null `underwaterVisuals`. It can be a bridge candidate only, not a proven diegetic HUD.
12. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` binds `Hecton8.Interaction.InteractionUI`, while `Assets/_Project/Scripts/UI/InteractionUI.cs` also exists. Prompt ownership needs readback.
13. `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` has fallback prompt `"OPEN HATCH"` in static subagent readback. If a target does not provide text, prompt semantics leak wrong diegetic instruction.
14. Russell static audit confirmed the route-critical HUD/PDA/pause/save code has several correct zero-GC text pieces, but they remain source-candidate only because scene-active proof is absent.
15. `Assets/_Project/Prefabs/Player.prefab:1589-1601` has `PlayerPDA` panel/group/tab refs null, so the PDA source can refuse to open if those refs are not injected elsewhere.
16. `Assets/_Project/Prefabs/HUD_Internal.prefab:42-53` has compositor disabled/null-bound and `forceScreenSpaceOverlay: 1`.
17. `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2390-2428` is overlay-mode and null-bound for projection camera, survival, movement, and underwater visuals.
18. Pause/save source exists through `Assets/_Project/Scripts/UI/PauseMenuController.cs` and `Assets/_Project/Scripts/SaveManager.cs`, but no active `PauseMenuController`, save UI, save artifact, or runtime save/load proof exists.
19. Tool suppression is incomplete by static proof: `PlayerFlashlight` direct toggle guard exists, but static audit found a route where `StepFromEquipmentOwner` calls `IsGameplayInputBlockedByMenu()` and ignores the result. Tool-wide menu suppression needs source/readback proof.

## Avicenna Static Recheck - 2026-06-06

Avicenna rechecked the first-20 route after the latest orchestration refresh. The verdict did not improve:

- `02_HECTON_WORLD.unity` still statically shows an active scene-local `Player`, not a production prefab instance.
- The scene-local `Player` is bound to `HectonWorldShellController1428`; that source explicitly identifies itself as a legacy shell marker and does not satisfy production movement/input/camera authority.
- `Player.prefab` contains plausible production pieces (`HectonPlayerMovement`, `Rigidbody`, `CapsuleCollider`, `PlayerInteraction`, `PlayerPDA`, `VisorHUDController`), but the production prefab GUID is not statically proven in `02_HECTON_WORLD`.
- `HUD_Internal.prefab` has compositor disabled/null-bound fields, and `Suit_HUD_Canvas.prefab` remains Screen Space Overlay with null projection/player/survival refs.
- `PlayerPDA`, pause/save UI, and SaveManager source candidates exist, but scene-active/runtime proof is absent.

Result: h8_1475 is blocked by runtime authority, not only visual quality. Higher-tier visor polish, cinematic camera work, or surface screenshots are invalid until the same production player/HUD route is proven active.

## Hooke Static Bootstrap Route Recheck - 2026-06-06

Hooke deepened the player bootstrap/spawn route. The result is worse than "scene prefab not found":

- `Player.prefab` GUID is `1c4db7a430141e5408e01b6ce4ed19d7`.
- ~~Static search found no runtime/bootstrap/scene reference to that GUID.~~ **RETRACTED 2026-07-29 — the scene half is false.** That GUID is a real external-reference entry in `02_HECTON_WORLD.unity` (binary, nibble-swapped). The runtime/bootstrap half of the sentence is untested by this correction.
- `GameBootstrapper` has serialized `playerSpawner`, `playerObject`, `playerController`, and `playerRigidbody`, but no player prefab field.
- `GameBootstrapper` resolves the current player from `BootstrapState` or scene tag and publishes it; its spawn step calls `HectonPlayerSpawner.SpawnPlayerAsync(...)` or repositions an existing `playerObject`. It does not instantiate `Player.prefab`.
- `HectonPlayerSpawner` has a `Rigidbody playerRigidbody` inspector reference, no prefab field. Its spawn path teleports an existing Rigidbody/motor/root.
- `PlayerRuntimeContextService.TryBindPlayerRoot(...)` can bind an existing production root only after that root exists; it cannot instantiate the prefab.
- `GameBootstrapper.IsTemporaryRuntimeShellObject(...)` rejects temp/staging/preview names, but a scene object named `Player` is not excluded. Tag lookup can therefore accept the shell.
- `SceneInstantiationGate.MarkPlayerInstantiated(playerObject)` can be satisfied by any non-null accepted player object unless Unity readback validates required production components.

Static implication: if Unity readback confirms only the scene-local shell exists, repair must be a cold bootstrap/scene prefab binding through Unity tooling, not a patch to `HectonWorldShellController1428` pretending it is production movement.

## Static Validator - 2026-06-06

Added `Tools/ValidatePlayerRouteStaticEvidence.py` as a static blocker guard. It is not runtime proof.

Validation:

- Initial `python -m unittest Tools/test_validate_player_route_static_evidence.py` ran 3 tests OK.
- Hardened recheck `python -B -m unittest Tools.test_validate_player_route_static_evidence` ran 4 tests OK.
- Hardened `python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static` rejects current project evidence with 25 blockers:
  - `scene-shell-player`;
  - `scene-production-prefab-instance-exact`;
  - `scene-missing-production-prefab-guid`;
  - `scene-missing-hud-internal-prefab-guid`;
  - `scene-missing-suit-hud-prefab-guid`;
  - `scene-missing-player-movement-guid`;
  - `scene-missing-player-interaction-guid`;
  - `runtime-missing-production-prefab-guid`;
  - `spawner-existing-rigidbody-route`;
  - `bootstrap-tagged-shell-acceptance-route`;
  - `bootstrap-mark-player-instantiated-without-production-validation`;
  - `bootstrap-publish-player-without-production-validation`;
  - `spawner-bootstrap-transform-rigidbody-fallback-route`;
  - `player-prefab-pda-null-panel-route`;
  - `player-prefab-pda-null-tab-route`;
  - `player-prefab-pause-menu-null-route`;
  - `player-prefab-swim-contract-null-route`;
  - `player-prefab-hud-presentation-null-route`;
  - `player-prefab-hud-render-camera-disabled-or-unbound`;
  - `player-prefab-hud-extension-null-route`;
  - `hud-internal-compositor-disabled`;
  - `hud-internal-compositor-null-route`;
  - `hud-internal-force-overlay-route`;
  - `suit-hud-canvas-overlay-render-mode`;
  - `suit-hud-canvas-null-runtime-route`.

Result: Owner03 now has an automated static guard against claiming production player route from the current scene/bootstrap/spawner/HUD/PDA/pause/swim shape. The guard still does not prove runtime readiness. Active prefab source identity, enabled runtime component list, dispatcher registrations, input map/block-mask behavior, PDA/pause/save opening, HUD render texture output, prompt text correctness, Rigidbody movement, camera writes, GC/perf, save artifacts, and 300-frame telemetry require Unity readback, Play Mode, profiler, or player-build evidence.

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
- PDA panel/group/tabs, `controlsRebindUI`, active `PauseMenuController`, `SaveManager.IsInitialized`, `SaveManager.IsBusy`, save slot button count/interactable state, save artifact path, and input block mask while PDA/pause/save UI is open.
- prefab identity for each Player candidate: `PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot`, source GUID, instance status, and whether it matches `1c4db7a430141e5408e01b6ce4ed19d7`;
- `GameBootstrapper.playerObject`, `playerRigidbody`, `playerController`, `playerSpawner`, `BootstrapState.CurrentPlayerObject`, `GlobalRegistry.Player`, `PlayerRuntimeContextService` bound object/movement/rigidbody/PDA/camera, and `SceneInstantiationGate._playerInstantiated`;
- active object/component list for every object named or tagged `Player`, every `HectonPlayerMovement` root, and every `HectonWorldShellController1428` root.

## Static UI Pieces That Are Not Enough

- `TMP_Text.SetCharArray` usage exists in route-critical PDA/pause/save/interaction candidates.
- `InputDispatcher` supports UI/player map switching and block masks.
- `PauseMenuController` can build pause/save UI at runtime.
- `SaveManager` has temp/primary/backup commit source.

These are implementation candidates, not runtime readiness. Acceptance still requires active scene wiring, no-mutation readback, Play Mode/profiler proof, and save/load artifacts.

## Low / Middle / High / Ultra

- Low: shell/overlay route cannot ship; production player authority must be proved.
- Middle: prove input -> movement -> motor -> camera -> HUD -> interaction.
- High: richer visor/camera effects only after owner proof.
- Ultra: diagnostics/visual overkill may scale, but authority and save identity must not change.

Final status: `P0 BLOCKED / STATIC ONLY`.

## Poincare Repair Map - 2026-06-06

Poincare completed a static source repair map with no edits.

Source-only repair candidates:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`: reject scene/tagged shell publication unless required production player components/prefab identity are proven.
- `Assets/_Project/Scripts/Bootstrap/SceneInstantiationGate.cs`: do not mark player instantiated from a generic non-null object.
- `Assets/_Project/Scripts/Bootstrap/BootstrapState.cs`: do not publish non-production player authority as current player.
- `Assets/_Project/Scripts/HectonPlayerSpawner.cs`: existing-Rigidbody/bootstrap-transform fallback is not a production prefab spawn route.

Unity API work still required:

- active scene `Player` identity and component readback;
- `Player.prefab` instance/source identity;
- `HUD_Internal` compositor state and refs;
- `Suit_HUD_Canvas` render mode/projection/player/survival refs;
- walking, swimming, ascend/descend, camera, interaction, PDA, pause, save/load, HUD render texture, GC/profiler, and 300-frame telemetry proof.

Raw YAML repair is rejected. A source-only patch can remove false acceptance routes, but it cannot claim the production first-20 route is playable.

Latest guard remains unchanged: `python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static` returns `PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=25 notes=2`.

## Cursor 137 Source-Only Prefab Route Prep - 2026-06-06

Evidence class: `STATIC_SOURCE / STATIC_VALIDATOR_TEST`.

`Assets/_Project/Scripts/HectonPlayerSpawner.cs` now exposes a serialized `productionPlayerPrefab` route for production `Player.prefab` GUID `1c4db7a430141e5408e01b6ce4ed19d7`. If the inspector reference, `GlobalRegistry.Player`, and `GameBootstrapper` transform route all fail, the spawner can cold-instantiate that prefab and then revalidates the result through `ProductionPlayerAuthorityUtility` before accepting the Rigidbody/movement route.

`Tools/ValidatePlayerRouteStaticEvidence.py` now treats runtime production prefab evidence as valid only when source has a real prefab route: `productionPlayerPrefab` field, `Instantiate(productionPlayerPrefab)`, and production-authority acceptance. A bare GUID string is rejected by `Tools/test_validate_player_route_static_evidence.py::test_guid_string_without_prefab_source_route_is_rejected`.

`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` no longer uses the hatch-specific fallback prompt `OPEN HATCH`; the default look-target fallback is now `INTERACT`. The validator rejects reintroducing `DefaultLookTargetPrompt = "OPEN HATCH"` as `player-interaction-hatch-fallback-prompt`, because objects without `IInteractableTextProvider` text must not inherit hatch semantics.

Current result:

- `python -B -m unittest Tools.test_validate_player_route_static_evidence` ran 7 tests OK.
- `python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static` rejects with `PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=19 notes=4`.
- Removed source-only blocker: `runtime-missing-production-prefab-guid`.
- Removed prompt leak: hatch-specific default prompt is no longer present in current `PlayerInteraction.cs`.
- Remaining blockers are scene/HUD/prefab wiring and prefab null-route blockers: scene-local shell, missing production prefab scene instance, missing HUD prefab scene refs, missing movement/interaction scene refs, PDA/pause/swim/HUD null refs, disabled HUD camera/compositor, forced overlay, and null Suit HUD runtime refs.

This does not prove compile/import, prefab assignment, active runtime player, HUD operation, movement, camera, PDA/pause/save, zero-GC, or h8_1475 acceptance. Unity compile/readback remains blocked by the process/CPU gate.

## Cursor 158 First-20 Player/UI/Movement Static Audit - 2026-06-06

Evidence class: `STATIC_SOURCE / STATIC_DOC`. Schrodinger `019e9b5e-4464-7742-8b3c-42326aeb826e` completed read-only audit. No Unity, import, build, Play Mode, profiler, scene, prefab, or asset mutation.

Production owners found:

- `GameBootstrapper.cs` owns bootstrap phases, player phase, UI phase, save/load handoff, and scene activation.
- `HectonPlayerSpawner.cs` has the production player prefab GUID and shallow-water/nearshore spawn search route.
- `BootstrapState.cs` requires movement authority, interaction authority, and `Rigidbody`.
- `HectonPlayerMovement.cs` owns swim/walk locomotion, water state, depth, dispatcher registration, and black-box dump routes.
- `HectonPlayerMotor.cs` routes acceleration/velocity through KCC or physics service.
- `InputDispatcher.cs` owns deterministic input state, typed `PlayerInputSignal`, tool trigger signals, block masks, buffered input APIs, and a 300-frame input black box.
- `PlayerInteraction.cs` consumes typed interact commands from `SignalBus<PlayerInputSignal>`.
- `SuitHUDV4CanvasOverlay.cs`, `DiegeticVisorHudMesh.cs`, and `DiegeticVisorLensRuntime.cs` are the current HUD/visor candidates.
- `FirstHourDirector.cs` owns route milestones, guidance, save/load fields, quest events, crafting, interaction, scan, and contextual hints.

Current blockers:

- Scene-flow docs had first-20 `01_ORBIT` dependency while root production handoff is direct. Controller patched first-20 docs to treat `01_ORBIT` as standalone/YELLOW prologue route, not mandatory first-20 proof.
- First-20 timing is not proven: contract requires swim -> resource -> tool interaction -> craft/repair/build inside the first route, while `FirstHourDirector.cs` comments place `FirstCraft` at 25-40 minutes and `FirstModule` at 70-90 minutes.
- Production player/prefab/scene wiring remains unverified because Unity readback is blocked.
- `HectonPlayerMovement.cs` still has cold fallback component-add/camera-rig routes that need Unity readback before acceptance.
- `PlayerBufferedAction` currently covers `Jump` and `Dash`; interact/tool/PDA/inventory/pause/death acknowledgement buffering needs formal route or documented bitmask consumption.
- `InputManager.GetAction(string...)` is a cold/UI-binding escape only; hot paths must stay on cached actions, bitmasks, or signals.
- HUD route remains ambiguous: gameplay prompts/state must be diegetic/world-space/visor; `ScreenSpaceOverlay` is not accepted as final interactive HUD unless an approved fallback/loading/debug bridge is named.
- `HectonPlayerMotor` falls back to `_body.MovePosition` when KCC is absent. KCC-active route or physics-owner fallback sweep needs proof.
- Save/load source exists, but no proof confirms player pose, inventory, hazard, opened/looted/scanned state, and route flags roundtrip.

Safe order:

1. Keep first-20 scene-flow docs aligned to direct root handoff until prologue route is GREEN.
2. Adjust route gating so resource, useful tool interaction, craft/repair/build, fair hazard, and save/load are reachable in the first-20 target.
3. Extend/formalize discrete input buffering for interact/tool/PDA/inventory/pause/death acknowledgement.
4. Lock production HUD route: diegetic visor/world-space for gameplay; overlay only for noninteractive fallback/loading/debug.
5. Prove or repair production player prefab ownership through Unity API, not YAML.
6. Prove KCC is active or add owner-correct fallback collision/sweep before `MovePosition`.
7. Bind one route chain: resource world object -> inventory -> tool interaction -> recipe/repair/build -> hazard response -> save/load flags.
8. Green-gate proof must include boot, spawn, swim/walk, interact, tool, craft/repair/build, hazard, save/load, Console, GC, profiler, screenshots, and player/HUD/tool witness.

Low/Middle/High/Ultra:

- Low: authored components, deterministic input, fixed buffers, low-cadence HUD, and route-readable movement. Overlay-only HUD or fallback spawning is rejected.
- Middle: stable diegetic HUD, first-20 timing, and swim/walk/tool flow.
- High: richer visor/water/control feedback after authority proof.
- Ultra: visual overkill only after gameplay and save/load proof; extra layers without route proof are noise.

## 2026-06-06 Kant Static Blocker Map

Evidence class: `STATIC_SOURCE / STATIC_SCENE_TEXT / STATIC_PREFAB_TEXT`. No Unity, import, build, Play Mode, profiler, source/prefab/scene mutation, raw YAML edit, delete, restore, stage, or commit.

Kant reran `python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static` and confirmed current result:

```text
PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=19 notes=4
```

Scene/player authority blockers:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` contains a scene-local active `Player` shell with no production prefab binding.
- ~~Production `Player.prefab` GUID `1c4db7a430141e5408e01b6ce4ed19d7` is absent from the scene.~~ **RETRACTED 2026-07-29: it is PRESENT** — one `FileIdentifier` entry at offset 566551 of the binary scene, nibble-swapped. The validator that produced this line tests it with a text substring search that can never match a binary scene.
- ~~`HUD_Internal`, `Suit_HUD_Canvas`, movement, and interaction GUIDs are also absent from the active scene text.~~ **PARTLY HELD 2026-07-29:** `HUD_Internal` and `Suit_HUD_Canvas` are genuinely absent from every live scene and prefab. The movement and interaction GUIDs live in `Player.prefab` and are prefab-borne, so their scene absence is the expected signature of a prefab instance and carries no blocker weight.
- Source owners exist: `HectonPlayerSpawner.cs`, `BootstrapState.cs`, `SceneInstantiationGate.cs`, and `GameBootstrapper.cs`; static blocker is scene/prefab binding absence, not missing owner source.

Required Unity readback:

- active `Player` objects: hierarchy path, tag/layer, active state, prefab asset path/GUID, prefab instance status, and components;
- `GameBootstrapper.playerObject`, `playerRigidbody`, `playerController`, `playerSpawner`;
- `HectonPlayerSpawner.productionPlayerPrefab`, `playerRigidbody`;
- `BootstrapState.CurrentPlayerObject`, `GlobalRegistry.Player`, `PlayerRuntimeContextService` bound player/movement/rigidbody/PDA/camera/visor/survival;
- `SceneInstantiationGate._playerInstantiated` and `LastFailureReason`.

Input / movement / interaction static state:

- Source owners exist: `InputDispatcher.cs`, `HectonPlayerMovement.cs`, `HectonPlayerMotor.cs`, and `PlayerInteraction.cs`.
- Dispatcher DTO, movement authority marker, interaction authority marker, black-box rings, and menu input blocking are present.
- Remaining blocker is active binding/proof: dispatcher registrations, `InputDispatcher.ActiveRuntimeInstance`, live input deltas/bitmask/block mask, movement Rigidbody/CapsuleCollider/camera/motor/swim contract/water state, KCC ownership versus Rigidbody fallback, and `PlayerInteraction` mask/camera/target/prompt/signal route.

Player prefab UI blockers:

- `PlayerPDA` panel/canvas/tabs/rebind refs are null.
- `PauseMenuController` route is null.
- `_swimContract` is null.
- `HUD_Render_Camera` is disabled or unbound.
- `SuitHUDPresentationController` overlay/projected/compositor refs are null.
- `HectonSuitHUDExtensions` HUD/flashlight refs are null.

HUD / visor / PDA / pause blockers:

- `HUD_Internal.prefab` compositor is disabled, target/visor refs are null, and forced overlay is enabled.
- `Suit_HUD_Canvas.prefab` is Screen Space Overlay with null projection/player/survival refs.
- Deprecated `InteractionUI` object exists and needs readback before prompt-route claims.
- Pause/save source exists, but active route is not proven.

Hard no-blind-edit list:

- Do not raw-YAML bind the production prefab.
- Do not convert the legacy scene shell into production movement authority.
- Do not bypass `ProductionPlayerAuthorityUtility`.
- Do not set interaction mask to Everything/Nothing to silence warnings.
- Do not assume KCC is active.
- Do not force overlay as final gameplay HUD.
- Do not delete deprecated prompt UI until readback proves it unused.
- Do not claim save/load from source presence.

First-20 impact: blocks `boot -> world load -> swim/orient -> interact -> HUD/visor/PDA/pause/save`. Low/Middle/High/Ultra are all blocked at the same authority seam until production prefab binding and HUD projection refs are proven.

Updated status: `PLAYER_ROUTE_STATIC_REJECTED / PRODUCTION_PREFAB_BINDING_ABSENT / PENDING UNITY READBACK`.

## 2026-06-06 Aquinas Static Binding Refinement

Evidence class: `STATIC_SOURCE / STATIC_SCENE_TEXT / STATIC_PREFAB_TEXT / STATIC_VALIDATOR`. No Unity, import, build, Play Mode, profiler, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

Aquinas confirmed the same hard state with a narrower repair order:

- Fresh `ValidatePlayerRouteStaticEvidence.py --require-production-static` remains rejected with 18 blockers and 5 notes.
- Production guard/source exists: `HectonPlayerSpawner`, `GameBootstrapper`, `BootstrapState`, `HectonPlayerMovement`, and `PlayerInteraction`.
- The failure is active binding, not missing source: scene-local `Player` shell remains, production `Player.prefab` GUID `1c4db7a430141e5408e01b6ce4ed19d7` is absent from scene text, and HUD/movement/interaction scene bindings are absent.
- `Player.prefab` still carries PDA/pause/swim/HUD nulls; `HUD_Internal` and `Suit_HUD_Canvas` remain disabled/overlay/null-route candidates, not production HUD proof.

Unity repair sequence:

1. Repair prefab sources first: `Player.prefab`, `HUD_Internal.prefab`, `Suit_HUD_Canvas.prefab`, PDA, pause, HUD camera/compositor, flashlight, swim contract, and tool route refs.
2. Instantiate or bind linked production prefab instances through Unity API. Do not raw-YAML bind and do not convert the scene shell into production authority.
3. Rebind world/runtime references away from shell only after readback proves all consumers and production source refs.
4. Rerun static validator. If the scene contains linked prefab instances whose components are serialized through prefab source rather than expanded scene YAML, update validator only to follow prefab source identity; do not weaken production binding requirements.
5. Only after static route is green or superseded by stronger Unity readback, run Play Mode proof for walk, swim, ascend/descend, camera, interact, PDA, pause, save/load, prompt, HUD/input, Console, GC, profiler, and player/HUD/tool witness.

Static delta classification: not accepted repair. The previous shell marker blocker moved to a note (`scene-shell-player: not detected by static marker scan`), while exact production prefab scene binding, HUD prefab refs, movement/interaction refs, PDA/pause/swim/HUD/compositor/flashlight, and ScreenSpaceOverlay blockers remain.

Updated status: `PLAYER_ROUTE_STATIC_REJECTED_18B_5N / ACTIVE_BINDING_NOT_SOURCE_ABSENCE / PENDING UNITY API REPAIR`.

## 2026-06-06 Fresh 19/4 Controller Refresh

Evidence class: `STATIC_SOURCE / STATIC_SCENE_TEXT / STATIC_PREFAB_TEXT / STATIC_VALIDATOR`. No Unity, import, build, Play Mode, profiler, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

Fresh command:

```text
python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static
PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=19 notes=4
```

Blocker groups:

- Scene binding: active scene still exposes a scene-local shell `Player`; production `Player.prefab` GUID/instance is absent; `HUD_Internal`, `Suit_HUD_Canvas`, `HectonPlayerMovement`, and `PlayerInteraction` GUIDs are absent from scene text.
- Player prefab internals: `PlayerPDA` panel/canvas/tabs/rebind refs are null, pause route is null, `_swimContract` is null, `SuitHUDPresentationController` overlay/projected/compositor refs are null, and `HectonSuitHUDExtensions` HUD/flashlight refs are null.
- HUD carrier: `HUD_Render_Camera` is disabled or unbound; `HUD_Internal` compositor is disabled/null/forced overlay; `Suit_HUD_Canvas` is ScreenSpaceOverlay with null projection/player/survival refs.

Positive static notes remain source-only:

- production prefab source route exists in bootstrap/spawner;
- production player authority guard exists;
- `SpawnPlayerAsync` exists;
- deprecated interaction UI still requires Unity readback before prompt-route claims.

This supersedes the 18/5 count for current state only. It still does not prove UI, walking, swimming, ascend/descend, camera, interaction, PDA, pause, save/load, HUD/input GC, or h8_1475 acceptance.

Current Unity repair order remains:

1. Read back all active Player/HUD objects and shell-bound consumers.
2. Repair prefab source internals first: `Player.prefab`, `HUD_Internal.prefab`, `Suit_HUD_Canvas.prefab`, PDA, pause, swim contract, HUD camera/compositor, flashlight, tool route refs.
3. Bind linked production `Player.prefab` and HUD prefab instances through Unity API only.
4. Rebind world/runtime references away from shell only after readback proves each consumer.
5. Rerun static guard.
6. Run Play Mode proof for walk, swim, ascend/descend, camera, interaction, PDA, pause, save/load, prompt, HUD/input, Console, GC, profiler, and player/HUD/tool witness.

Updated status: `PLAYER_ROUTE_STATIC_REJECTED_19B_4N / FULL_UI_MOVEMENT_BLOCKED / PENDING UNITY API REPAIR`.

## 2026-06-06 Planck Static Binding Split

Evidence class: `STATIC_SOURCE / STATIC_YAML / STATIC_VALIDATOR`. No Unity, import, Console, Play Mode, profiler, GC, screenshot, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

Planck confirmed the hard P0 state and separated probable validator overreach from actual player-route blockers.

Hard blockers that remain non-negotiable:

- `02_HECTON_WORLD` contains a scene-local `Player` shell tagged `Player`, with no prefab source, using `HectonWorldShellController1428`.
- The production `Player.prefab` GUID `1c4db7a430141e5408e01b6ce4ed19d7` is absent from scene text.
- `HUD_Internal.prefab`, `Suit_HUD_Canvas.prefab`, `HectonPlayerMovement`, and `PlayerInteraction` GUIDs are absent from scene text.
- `Player.prefab` contains `HectonPlayerMovement` and `PlayerInteraction`, but prefab source presence is not scene/runtime binding proof.
- `PlayerPDA` panel, canvas group, tab refs, and controls rebind refs are null.
- `HUD_Render_Camera` is disabled and has no target texture.
- `SuitHUDPresentationController`, `HectonSuitHUDExtensions`, `HUD_Internal`, and `Suit_HUD_Canvas` remain disabled/null/forced-overlay/ScreenSpaceOverlay routes rather than production diegetic HUD proof.

Validator caveats to fix without weakening production requirements:

- The pause blocker appears to trip on `Hecton8.Dev.UIRuntimeSmokeTester.pauseMenu`; dev smoke tester nulls must not fail the production pause route.
- The `_swimContract` blocker likely catches inherited root `PlayerBuilder` data. Held tool prefabs appear to carry `PlayerToolSwimContract`; validator should distinguish active held tools/tool-manager slots from root inactive prefab data.
- `SuitAdvisoryController.survival` and `hudNotification` nulls are an extra production risk not currently reported by the validator.
- Refs with proven cold auto-bind source should become `PENDING UNITY READBACK`, not automatic static failure. Refs that must serialize for production remain hard blockers.

Safe Unity API repair sequence:

1. Load `02_HECTON_WORLD` and instantiate or bind `Assets/_Project/Prefabs/Player.prefab` through Unity APIs at the intended shell transform.
2. Keep or remove the scene-local shell only after production authority acceptance is verified. If retained, untag/dev-gate it so it cannot be accepted as player authority.
3. Confirm `GameBootstrapper` and `HectonPlayerSpawner.productionPlayerPrefab` bindings.
4. Bind `PlayerPDA` through a real diegetic PDA shell: panel root, canvas group, eight tabs, controls rebind UI, or a configured `DiegeticPDAController`.
5. Add/bind production `PauseMenuController` or `PauseMenuHost`; ignore dev smoke tester nulls for production validation.
6. Enable and bind HUD projection path: render camera, shared RT, visor controller, compositor, canvas overlay, survival, movement, flashlight, underwater visuals, and profile refs.
7. Convert Suit HUD away from forced `ScreenSpaceOverlay` for production diegetic/projection use.
8. Decide whether root `PlayerBuilder` is an active held tool. If yes, bind `PlayerToolSwimContract`; if no, narrow validator to active held tool prefabs/tool-manager slots.
9. Save through Unity API, rerun static validator, then run Play Mode/profiler/GC/screenshot proof in an allowed lane.

Current status remains `PLAYER_ROUTE_STATIC_REJECTED_19B_4N / FULL_UI_MOVEMENT_BLOCKED / VALIDATOR_REFINEMENT_NEEDED / PENDING UNITY API REPAIR`.

## 2026-06-06 Validator Refinement

Evidence class: `STATIC_SOURCE / STATIC_PREFAB_TEXT / STATIC_VALIDATOR`. No Unity, import, Console, Play Mode, profiler, GC, screenshot, scene, prefab, material, raw YAML, delete, restore, stage, or commit action was performed.

The validator caveats from Planck were implemented without weakening production scene/HUD binding requirements:

- Dev `Hecton8.Dev.UIRuntimeSmokeTester.pauseMenu` null is now a note: `player-prefab-dev-pause-smoke-null-note`.
- Root `Hecton8.Building.PlayerBuilder._swimContract` null is now a note: `player-prefab-builder-swim-contract-null-readback-note`.
- `SuitAdvisoryController` serialized survival/HUD nulls are now reported as `player-prefab-suit-advisory-runtime-ref-null-note`; source can cold-resolve, but Unity readback is still required.

Fresh command:

```text
python -B Tools\ValidatePlayerRouteStaticEvidence.py --require-production-static
PLAYER_ROUTE_STATIC_EVIDENCE_REJECTED blockers=17 notes=7
```

The route is still hard P0 blocked: scene-local shell player, missing production `Player.prefab` scene GUID/instance, missing HUD prefab scene refs, missing movement/interaction scene refs, PDA/HUD nulls, disabled/unbound HUD render camera/compositor, forced overlay, and ScreenSpaceOverlay Suit HUD remain rejected.

Verification: `python -B -m unittest Tools.test_validate_player_route_static_evidence` ran 7 tests OK.

Updated status: `PLAYER_ROUTE_STATIC_REJECTED_17B_7N / FULL_UI_MOVEMENT_BLOCKED / PENDING UNITY API REPAIR`.

## 2026-06-06 Poincare Static Unity Repair Plan Refresh

Evidence class: `STATIC_SOURCE / STATIC_PREFAB_TEXT / STATIC_SCENE_TEXT / NO_UNITY_READBACK`.

Future owner touch list:

- scene via Unity API only: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`;
- prefabs: `Assets/_Project/Prefabs/Player.prefab`, `HUD_Internal.prefab`, `Suit_HUD_Canvas.prefab`;
- bootstrap/spawn: `GameBootstrapper.cs`, `HectonPlayerSpawner.cs`, `BootstrapState.cs`, `SceneInstantiationGate.cs`;
- player/input/interaction: movement, motor, input dispatcher/input handler, `PlayerInteraction`;
- HUD/PDA/pause: `PlayerPDA`, `PauseMenuController`, `SuitHUDV4CanvasOverlay`, `VisorHUDController`, `SuitHUDPresentationController`, `SuitHUDScreenCompositor`, `HectonSuitHUDExtensions`, `SuitAdvisoryController`;
- validator after repair: `Tools/ValidatePlayerRouteStaticEvidence.py` and tests.

Unity API sequence:

1. Read back all active/inactive scene objects named or tagged `Player`, all shell/player movement/interaction/HUD/PDA/pause/spawner objects.
2. Load `Player.prefab`, `HUD_Internal.prefab`, `Suit_HUD_Canvas.prefab`; use `PrefabUtility.LoadPrefabContents` and `SerializedObject` for private refs.
3. Repair `Player.prefab` PDA, HUD camera/RT, HUD presentation/compositor refs, suit extension refs, advisory refs, and active-tool swim contract route after readback.
4. Repair `HUD_Internal.prefab` compositor enablement, target canvas, visor controller, shared RT, and remove final forced overlay unless debug-only.
5. Repair `Suit_HUD_Canvas.prefab` away from final `ScreenSpaceOverlay`; bind projection camera/player movement/survival/flashlight/underwater visuals/default profile.
6. Instantiate linked production `Player.prefab` in scene or bind/create `HectonPlayerSpawner.productionPlayerPrefab`.
7. Rebind every shell `playerTransform` consumer to production transform.
8. Only after readback proves consumers are rebound, untag/deactivate/dev-gate the shell; save through Unity API and rerun static guard.

Hard rejects:

- raw YAML edits;
- shell controller as production movement authority;
- two tagged `Player` objects;
- hierarchy-order bootstrap resolution;
- forced overlay final HUD;
- deleting shell or deprecated prompt UI before readback proves unused;
- bypassing `ProductionPlayerAuthorityUtility`.

Proof gates:

- walk, swim, ascend/descend movement state and velocity/depth evidence;
- active camera owner and look delta;
- PDA/pause signals, refs, input block mask, save UI usability;
- interaction mask/camera/target/prompt/typed signal evidence;
- visor/compositor/projection RT active and visible;
- Console clean and profiler/GCMonitor `0 B/frame`.
