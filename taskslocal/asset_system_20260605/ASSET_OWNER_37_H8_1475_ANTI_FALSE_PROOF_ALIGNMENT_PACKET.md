# ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET

Status: `EXECUTION PACKET / PENDING VERIFICATION`.
Evidence class: `STATIC_DOC + STATIC_IMAGE_REJECTION + STATIC_RUNTIME_CONFLICT`.
Owner: future h8_1475 Unity proof owner.

This packet prevents false h8_1475 acceptance. It is not visual acceptance, Unity proof, runtime proof, profiler proof, or repair authorization.

## Objective

Block screenshot-only proof that hides active runtime authority failures. h8_1475 must prove the actual first-20 player route: production player, movement, swim, camera, HUD/visor, interaction, foreground tools, water, shoreline, terrain, Aegir/sky, and route readability.

First-20 route blocker targeted for removal: current diagnostic screenshots can show a landscape while the active player may still be the scene-local shell and the interactive HUD/tool/product-face route remains unproven.

## Required Source Reads

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `player.md`
- `ui.md`
- `input.md`
- `world.md`
- `water.md`
- `rendering.md`
- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.md`
- `Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md`
- `Docs/Reports/RuntimeSystem_20260605/ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md`
- `Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md`
- `Docs/AssetAudit/SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md`
- `Docs/Orchestration/H8_1475_PROOF_TOOL_INTEGRITY_SYNTHESIS_20260605.md`
- `Tools/ProofGate/README.md`
- `Tools/ProofGate/validate_proof_packet.py`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET.md`

## Current Rejection Facts

- Latest inspected MCP surface frames show rectangular horizon/sea slabs, flat dark or acid-turquoise water planes, disconnected island chunks, noisy black/acid terrain, weak Aegir integration, and blockout-looking foreground/tool geometry.
- `02_HECTON_WORLD.unity` static conflict map reports an active scene-local `Player` with enabled `HectonWorldShellController1428`.
- Production `Player.prefab` has `HectonPlayerMovement`, `PlayerInteraction`, Rigidbody, camera, visor, and HUD bindings, but its GUID was not proven active in the targeted scene scan.
- `HUD_Internal.prefab` keeps latent `forceScreenSpaceOverlay: 1`; interactive gameplay HUD acceptance requires approved diegetic/projection/world-space route proof, not a convenient overlay.
- Current h8_1475 proof packet must include active player/HUD/product-face fields. A beauty shot without those fields is a false proof.
- `H8VisualProofCapture1912.cs` has diagnostic capture paths that create or mutate editor-only visual state before capture. Current source contains `CaptureSurfaceCrestRecoveryProbeAndExit()`, which carries `surface_actual_terrain_crest_recovery_probe_editor_only_unsaved`, configures temp horizon haze, mutates MapMagic/Crest serialized fields through `ApplyModifiedPropertiesWithoutUndo()`, and creates `HideAndDontSave` temp materials. These methods are diagnostic probes, not canonical proof lanes.
- The old 1914 water probe metadata records `H8_TEMP_SurfaceWaterReadabilityProbe_1428=MISSING`, but current `H8VisualProofCapture1912.cs` no longer references the deleted water-readability shader path. Do not assign a current source blocker from stale capture metadata.

## Anti-False-Proof Rules

- Proof packet must exist under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` with `manifest.json`, `manifest.sha256`, `UnityEditor_h8_1475_{session}.log`, console/readback support artifacts, and the six exact ProofGate production screenshots: `01_surface_coast_aegir_ui_off.png`, `02_shoreline_close_1m.png`, `03_underwater_0_5m.png`, `04_underwater_20_50m_route.png`, `05_aegir_celestial_long.png`, and `06_regression_low_oblique.png`.
- Proof packet must satisfy the current ProofGate six-view production screenshot contract before it can proceed to human visual review.
- Visual source promotion rows from VSPQ are required route inputs. They do not count as import, material, screenshot, Frame Debugger, memory, or runtime acceptance.
- Active production player proof is mandatory. A shell camera, scene-local debug player, or unbound `Main Camera` shot is rejected.
- HUD/visor/cockpit proof is mandatory. Interactive `ScreenSpaceOverlay` is rejected unless a documented approved bridge proves why it is gameplay-correct and not a shortcut.
- Foreground/tool/product-face proof is mandatory. Landscape-only screenshots cannot prove first-person route readiness.
- Water, shoreline, Aegir, terrain, flora, UI, cockpit, and medium-depth route must compare against the current mandatory VREF signals.
- Reject rectangular horizon slabs, flat water planes, disconnected island chunks, noisy black/acid terrain, giant-sphere-only Aegir, low-detail foreground tools, proxy materials, null/default materials, and primitive-visible meshes.
- Reject darkness, fog, bloom, vignette, DoF, or post-process camouflage used to hide weak surface, shoreline, water, terrain, sky, UI, tool, or route art.
- Premium approximation is allowed only if it looks premium and preserves player belief. Flat cheap fakes are rejected.
- Reject any capture method that creates temp water/haze objects, mutates Crest/OceanRenderer serialized fields, disables route renderers, saves `02_HECTON_WORLD.unity`, or carries `editor_only_unsaved` metadata while being presented as h8_1475 acceptance.
- Reject proof that depends on a missing diagnostic shader path or silently falls back after a probe object fails to instantiate.

## Required h8_1475 Proof Fields

- Process gate: green CPU/compiler/import/package/tooling state.
- MCP/editor state: no compiling, importing, domain reload, save prompt, dirty mutation, or Play Mode mismatch.
- Active player: object path, source, scene, prefab relation, enabled components, active camera, and `BootstrapState.CurrentPlayerObject` or equivalent context.
- Runtime route: `HectonPlayerMovement`, `PlayerInteraction`, PDA/pause/quickbar/tool owners, input owner, save/load owner, black-box/profiler/GC route.
- HUD route: `Suit_HUD_Canvas`, `HUD_Internal`, `SuitHUDScreenCompositor`, `SuitHUDV4CanvasOverlay`, `VisorHUDController`, render mode, active binding, render texture, oxygen/power/hull/depth interaction prompt.
- Foreground/tool route: held tool mesh/material, tool animation/pose, readable silhouette, no blockout material, no built-in primitive visible as product face.
- Visual route: surface water, shoreline contact, terrain material, Aegir/cloud/moon, photic shallows, medium-depth route, route landmarks, flora/coral/geology, UI/cockpit readability.
- Technical proof: Unity console, Frame Debugger/Stats where applicable, GCMonitor/profiler, memory/VRAM, ProofGate six screenshots, manifest/checksum, and static ProofGate output.

## Numbered Tasks

1. Read the latest `ORCHESTRATOR_NIGHT_20260605.md` tail and confirm current process gate, latest visual rejection, and current h8_1475 blocker state.
2. Read `RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET.md`. If process/MCP/editor gate is not green, stop before h8_1475 capture.
3. Confirm proof folder path, session id, ProofGate manifest schema, six exact screenshot names, Unity log copy path `UnityEditor_h8_1475_{session}.log`, and checksum path before opening Unity proof tools.
4. Read active player source through Unity. If the active player is scene-local shell, duplicate authority, null, or unknown, stop with `H8_1475_REJECT_PLAYER_AUTHORITY`.
5. Read production `Player.prefab` relation. It must be active or explicitly bound by bootstrap runtime context; static prefab existence is not enough.
6. Checkpoint 0: write player-authority verdict. No screenshots can be accepted before this checkpoint passes.
7. Read camera stack: player camera, Main Camera, HUD camera/render texture, camera rig source, and any compositor camera. Reject unbound scenic cameras.
8. Read HUD/visor stack and render mode. Reject active interactive `ScreenSpaceOverlay` unless an approved bridge route is proven by docs and readback.
9. Read input owner and direct-input consumers. Reject `HectonWorldShellController1428` or any direct shell input as the h8_1475 control source.
10. Read interaction/quickbar/tool/PDA/pause owners. Reject proof if interact prompt, held tool, PDA/pause, or quickbar cannot be bound to the active player.
11. Read foreground tool mesh/material/pose route. Reject blockout geometry, transparent debug slabs, primitive mesh, null/default material, or missing hand/tool product-face.
12. Checkpoint 1: write runtime-HUD-tool verdict. No landscape-only capture can pass this checkpoint.
13. Capture canonical surface/shoreline/photic/medium-depth/cockpit shots only after checkpoints 0 and 1 pass.
14. For every shot, compare against VREF requirements: water volume, shoreline contact, terrain geology, Aegir/clouds, route density, UI/cockpit readability, and negative-space navigation.
15. Reject shots with rectangular slabs, acid water, black/noisy terrain, disconnected chunks, smear-only Aegir, empty underwater routes, or post-process camouflage.
16. Collect Unity console, Stats/Frame Debugger, GCMonitor/profiler, memory/VRAM, and dirty-state before/after evidence.
17. Build manifest/checksum and copied log, then run `Tools/ProofGate/validate_proof_packet.py --strict`. If any listed artifact is missing or ProofGate rejects the packet, classify packet as `INCOMPLETE_PROOF_PACKET` or `REJECTED_PROOFGATE`.
18. Checkpoint 2: final h8_1475 disposition. Allowed labels: `REJECTED`, `INCOMPLETE_PROOF_PACKET`, `BLOCKED_BY_RUNTIME_AUTHORITY`, `BLOCKED_BY_VISUAL_FLOOR`, or `PENDING_HUMAN_REVIEW`. Do not write `ACCEPTED` without all evidence and user approval.

## Low / Middle / High / Ultra Consequences

- Low: must still prove readable production player route, HUD, water, shoreline, terrain silhouette, Aegir, and foreground tool. Low cannot use flat water or overlay HUD as a shortcut.
- Middle: must add credible shoreline contact, photic route density, interaction prompt, and stable swim/walk proof.
- High: must buy stronger material detail, denser route flora/geology, richer sky/Aegir/clouds, and cleaner tool/cockpit product-face.
- Ultra: adds capture density and visual overkill only after low/middle proof passes. It must not use a different gameplay truth owner.

## Regression Model

- CPU: no runtime claim from this packet. Future h8_1475 capture must include process gate and profiler/Stats evidence.
- GC: no `0 B/frame` claim from static docs. Future proof needs GCMonitor or profiler.
- Memory/VRAM: no residency claim. Future proof needs memory/VRAM evidence.
- Cadence: proof must not hide shell input, overlay HUD, or tool blockout behind a camera-only route.
- Correctness: active player, HUD, input, camera, interaction, and foreground tool ownership must be proven before visual review.
- Visual: latest diagnostic screenshots are rejected; new proof must beat them and compare against VREF.

Final status: `PENDING VERIFICATION`.
