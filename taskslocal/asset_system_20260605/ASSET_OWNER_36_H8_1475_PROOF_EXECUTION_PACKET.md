# ASSET_OWNER_36 - H8_1475 Proof Execution Packet

ID: `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET_WRITER`
Role: Unity `h8_1475` no-mutation proof execution task writer.
Project: `C:\hades\Hecton8`
Status: `DISTRIBUTABLE_TASK_PACKET / UNITY_NOT_RUN_BY_PACKET_WRITER`
Evidence class: `STATIC_DOC / STATIC_REPORT_SYNTHESIS`

This packet assigns a future Unity proof owner to execute the `h8_1475` readback and visual proof pass. It is not Unity proof, runtime proof, visual acceptance, profiler proof, GC proof, memory proof, or build proof.

## Objective

Create a complete no-mutation `h8_1475` proof packet that proves, or rejects with evidence, the first-20-minutes product-face route: production player/HUD, bright surface sky/Aegir, Crest ocean, shoreline/waterline, photic terrain, shallow underwater view, and product-face prefab/source blockers.

First-20 route moment: bright first surface exit with readable production player/HUD, sky/Aegir, ocean surface, shoreline, photic terrain, shallow underwater volume, tools/resources/transport source quality, and diegetic route readability.

Route blocker targeted: missing canonical `h8_1475` proof packet. Current raw MCP PNGs and static reports reject product-face visual promotion.

## Evidence Basis

Read before execution:

- `taskslocal/asset_system_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.md`
- `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.csv`
- `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.md`
- `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.md`
- `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.md`
- `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`
- `Docs/Reports/RuntimeSystem_20260605/ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md`
- `Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md`
- `Docs/AssetAudit/SURFACE_WATER_RECOVERY_PROBE_1914_STATIC_REVIEW_20260605.md`

Existing source-gate state:

- Product-face material/texture gate: `FAILED`.
- Product-face prefab quality gate: `FAILED`.
- Sky/ocean source primitive gate: `FAILED`.
- `h8_1475` proof packet: absent.
- `Docs/Screenshots/HectonProofPackets/` proof root: missing or not yet populated by accepted `h8_1475` session.
- `Docs/Screenshots/MCP/*.png`: diagnostic only. Raw MCP PNGs are explicitly rejected as acceptance proof.
- `H8VisualProofCapture1912.cs` contains diagnostic/editor-mutating capture paths. Any method that carries `editor_only_unsaved`, creates temp water/haze state, mutates Crest/OceanRenderer serialized fields, disables scene renderers, or saves the scene is rejected as canonical h8_1475 proof tooling.
- Current `H8VisualProofCapture1912.cs` no longer references the old deleted water-readability shader path. The remaining proof-tool blocker is stronger: current diagnostic paths still create temporary haze/material state, mutate MapMagic/Crest serialized fields, carry `editor_only_unsaved` metadata, and include a separate scene-save quarantine path. Any future stale or missing `Assets/...` path in proof tooling remains a blocker.

Known blockers to verify through Unity readback, not mutate:

- `02_HECTON_WORLD.unity` static scene YAML contains an active tagged `Player` with enabled scene-local `HectonWorldShellController1428`; production `Player.prefab` GUID was not found in the scene by static search.
- `Suit_HUD_Canvas.prefab` and `HUD_Internal.prefab` GUIDs were not found in `02_HECTON_WORLD.unity` by static search; `HUD_Internal.prefab` keeps `forceScreenSpaceOverlay: 1` on a disabled compositor and is a latent gameplay HUD blocker if enabled or cloned.
- `Assets/_Project/Prefabs/Player.prefab` has blockout and package-default material routes.
- `Assets/_Project/Prefabs/Tools/Held/*` and `Assets/_Project/Prefabs/Items/Tools/*` have placeholder materials and/or built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Resources/Pickups/*` and `Assets/_Project/Prefabs/Transport/*` have Unity built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Sky_System.prefab` has active visible primitive risk: `Sky_System/Sphere` using Unity built-in `Sphere`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab` has micro-fauna primitive risk: `SargassumMicroFaunaBoids.boidMesh` using Unity built-in `Plane`.
- Crest hidden input primitives are accepted only as narrow non-visual data input candidates, not visual proof.

## Authority Docs

Future Unity owner must obey:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `Docs/QUALITY_GATES.md`
- `rendering.md`
- `water.md`
- `terrain.md`
- `ui.md`
- `player.md`
- `presentation.md`

Read `HECTON8_ORCHESTRATOR.md` only if the future owner is explicitly assigned controller/orchestration work. An ordinary Unity proof owner must not read it.

Do not bulk-read unrelated archives or stale logs. Read only the named evidence files and route bibles needed for the proof pass.

## Owned Scope

Future Unity proof owner may create only proof artifacts under:

- `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_36_H8_1475_*.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_36_H8_1475_*.txt`

No writes are allowed under:

- `Assets/`
- `ProjectSettings/`
- `Packages/`
- `UserSettings/`
- scenes, prefabs, materials, shaders, importers, Addressables settings, package manifests, code files, or shared indexes.

If a future controller assigns a different execution owner ID, keep the same proof folder convention but use that assigned ID in report filenames. Do not infer IDs.

## Proof Folder Convention

Every execution session must create exactly one session root:

`Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`

Minimum required files:

- `manifest.json`
- `manifest.sha256`
- `UnityLog.txt`
- `console_export.txt`
- `no_mutation_readback_report.md`
- `dirty_state_audit.md`
- `frame_debugger_stats.md`
- canonical screenshots listed below, or matching `ABORTED_<view>.md` notes.

`manifest.json` must include:

- session id;
- Unity editor version;
- active scene sequence and scene context per runtime row;
- process gate command outputs and interpretation;
- Unity/editor state before readback;
- artifact list with relative paths;
- screenshot filenames, source camera/view, and capture reason;
- Unity log path;
- console export path;
- no-mutation readback report path;
- dirty-state audit path;
- Frame Debugger/Stats path;
- GCMonitor/profiler artifact paths if collected;
- visual reference comparison path;
- mutation result: `NO_MUTATION` or `ABORTED_BEFORE_MUTATION`;
- acceptance state: `PENDING_VERIFICATION` only.

`manifest.sha256` must be the actual SHA-256 of `manifest.json`. Do not invent hashes.

## Canonical Screenshot Set

Required screenshots:

- `h8_1475_surface_sky_aegir_ocean_hud_game.png`
- `h8_1475_surface_shoreline_waterline_game.png`
- `h8_1475_photic_terrain_route_game.png`
- `h8_1475_underwater_0_5m_route_game.png`
- `h8_1475_player_hud_binding_scene_selected.png`
- `h8_1475_sky_aegir_slots_inspector.png`
- `h8_1475_crest_ocean_slots_inspector.png`
- `h8_1475_terrain_material_slots_inspector.png`
- `h8_1475_product_face_primitive_targets_inspector.png`

Optional but preferred when safe:

- `h8_1475_underwater_20_50m_route_game.png`
- `h8_1475_pda_or_cockpit_hud_readable_game.png`
- `h8_1475_frame_debugger_sky_ocean_terrain.png`
- `h8_1475_stats_overlay_surface_route.png`

If a view cannot be captured safely without mutation, write `ABORTED_<view>.md` in the session root. The abort note must state the failed prerequisite and last safe step.

Raw `Docs/Screenshots/MCP/*.png` files are rejected as acceptance proof. They may be cited only as rejected diagnostic context.

## Global No-Mutation Rules

- No `SaveScene`.
- No `EditorSceneManager.MarkSceneDirty`.
- No `EditorUtility.SetDirty`.
- No `AssetDatabase.SaveAssets`.
- No scene save.
- No project save.
- No prefab apply/revert.
- No material assignment.
- No texture slot binding.
- No mesh replacement.
- No terrain layer edit.
- No Crest setting change.
- No canvas render-mode change.
- No object disable/enable to improve a screenshot.
- No `H8VisualProofCapture1912` diagnostic probe method as canonical proof capture unless the method is separately proven no-mutation and does not carry `editor_only_unsaved` metadata.
- No raw YAML edit.
- No temporary file under `Assets/`.
- No Crest runtime wrapper, material clone, material instantiation, or override script.
- No Addressables settings creation.
- No package/import/project repair prompt accepted by saving.

If Unity exposes a dirty scene, prefab, material, importer, Addressables asset, or project setting during readback, stop and report it. Do not save it.

## Numbered Execution Tasks

1. Read only the authority and evidence files named in this packet. Acceptance proof: note file list in `manifest.json`. Fallback: if any file is missing, record `MISSING_STATIC_INPUT` and continue only with available evidence; do not invent missing facts.

2. Run the pre-Unity process gate before launching or touching Unity:
   - `Get-Counter '\Processor(_Total)\% Processor Time'`
   - `Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,ShaderCompiler,AssetImportWorker,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue`
   Acceptance proof: paste concise command outputs into `manifest.json` or `process_gate.md`. Fallback: abort if CPU is above `50 percent`, ambiguous, or any listed process is busy.

3. Verify Unity/editor state is closed or idle before readback. Acceptance proof: record no import spinner, no compile, no shader compile, no package resolution, no player build, no save prompt, no dirty prompt. Fallback: abort; do not launch Unity to force clarity.

4. Create the session proof root under `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`. Acceptance proof: folder exists with empty draft `manifest.json`. Fallback: if folder creation fails, abort and write only a report under `Docs/Reports/AssetSystem_20260605/`.

5. Start Unity only after the process gate is clean. Load the required context without saving. Acceptance proof: record Unity editor version, active scene, and scene sequence context. Fallback: abort on save prompt, import prompt, compile activity, shader compile, package resolution, or dirty state.

6. Checkpoint 1 - write `process_gate.md` and draft `manifest.json` with tasks 1-5 status. Acceptance proof: `manifest.json` still says `PENDING_VERIFICATION`. Fallback: if any prerequisite failed, stop here and write `ASSET_OWNER_36_H8_1475_ABORT_<timestamp>.md`.

7. Read active player and HUD binding. Capture `BootstrapState.CurrentPlayerObject` name, scene, tag, active state, prefab/source classification, and whether it is an instance of `Assets/_Project/Prefabs/Player.prefab`. Reconcile this against `ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md`: active scene object path, prefab source/GUID, scene-local shell status, production prefab status, and whether `Player.prefab` is instantiated, cloned, or absent. Acceptance proof: rows in `no_mutation_readback_report.md`. Fallback: if the production binding cannot be read without mutation, record `BLOCKED_READBACK`.

8. Read scene-authored player shell state and `HectonWorldShellController1428.enabled`. Acceptance proof: classify active movement, input, and camera owner. Reject if the scene shell owns active player movement/input/camera without accepted owner route. Do not patch input inside `HectonWorldShellController1428`; this packet is proof only.

9. Read production player component stack without adding or fixing anything:
   - `Hecton8.Gameplay.HectonPlayerMovement`
   - `Hecton8.Interaction.PlayerInteraction`
   - `Hecton8.UI.PlayerPDA`
   - `PlayerToolManager`
   - `PlayerInventory`
   - `PlayerFlashlight`
   - `VisorHUDController`
   - `HUDNotification`
   - `ToolLoadoutProvisioner`
   Acceptance proof: component presence/enabled rows. Fallback: missing owner remains blocker.

10. Read active HUD, visor, PDA, interaction, quickbar, notification, oxygen/pressure canvases, render modes, world-space projection state, `HUD_Internal` instantiation/enabled state, `SuitHUDScreenCompositor.forceScreenSpaceOverlay`, `GraphicRaycaster` enabled count, and interaction UI namespace state. Acceptance proof: `h8_1475_player_hud_binding_scene_selected.png` and rows in report. Reject interactive gameplay HUD as `ScreenSpaceOverlay` unless explicit projection proof exists. A disabled prefab flag is not runtime proof; classify it as latent until Unity readback proves absence or noninteractive/debug-only use.

11. Capture Game View surface/HUD proof: `h8_1475_surface_sky_aegir_ocean_hud_game.png`. Acceptance proof: first-person world view with HUD visible and readable. Reject flat overlay posing as cockpit/diegetic proof.

12. Checkpoint 2 - export Console state and update dirty-state audit. Acceptance proof: `console_export.txt` exists and `dirty_state_audit.md` records no dirty scene/prefab/material/project state. Fallback: abort on any Console error after scene handoff, dirty object, save prompt, or mutation risk.

13. Read active sky/Aegir/cloud/moon route. Include `RenderSettings.skybox`, active scene sky/celestial renderers, active state, renderer enabled state, mesh, material path, shader, and whether proof is from `02_HECTON_WORLD`. Acceptance proof: rows in `no_mutation_readback_report.md`. Reject orbit/prologue material state as proof for world route.

14. Read sky material slots and classify effective/ignored/stale/null:
   - `Mat_HectonSky.mat` `_MainCloudTex`
   - `_HighCloudTex`
   - `_MainCloudAtlas`
   - `_StarTex`
   - `_StarTwinkleLUT`
   - `_BakedStarCubemap`
   - `Mat_HectonSky_CloudOverlay.mat` `_MainCloudTex`
   - Aegir band/cloud/disc slots
   - moon albedo/normal/mask slots
   Acceptance proof: `h8_1475_sky_aegir_slots_inspector.png`. Fallback: missing effective slot remains blocker; do not assign candidate textures.

15. Capture bright surface sky/Aegir/ocean route and compare against mandatory references. Acceptance proof: visual comparison section in `h8_1475_visual_reference_comparison.md`, using `H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md`, with reference requirements from `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md` and `VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv` rows `VREF-03`, `VREF-05`, and `VREF-15`: `BEST ILLUST`-level bright coastline/island composition, readable whitewater/ocean surface, dense alien vegetation or route scale cue, huge Aegir/gas-giant read, layered clouds, premium Aegir limb/cloud detail, readable surface, no muddy sphere, no darkness/fog cover. Reject smeared, toy-like, muddy, pasted, empty, or surface-darkened results.

16. Read Crest/ocean route:
   - active `OceanRenderer`;
   - `Assets/_Project/Prefabs/Ocean_Crest.prefab`;
   - effective ocean material;
   - underwater material;
   - normals, foam, caustics slots;
   - `_WD_*` wave-data classification;
   - `MAT_H8_SurfaceCrestOcean_1428.mat` active vs candidate-only state;
   - Crest input exceptions.
   Acceptance proof: `h8_1475_crest_ocean_slots_inspector.png` and report rows. Fallback: Crest material state cannot be accepted from static YAML only.

17. Read `SargassumMicroFaunaBoids.boidMesh`. Acceptance proof: mesh path/name and `UNITY_BUILTIN_PRIMITIVE_MESH` or authored/generated classification. Reject visible product-face micro-fauna route if it remains Unity built-in `Plane`.

18. Capture shoreline/waterline proof: `h8_1475_surface_shoreline_waterline_game.png`. Acceptance proof: readable shoreline contact, foam/contact breakup route, wet terrain edge, and waterline material truth. Reject black terrain edge, repeated `foam.png` as final contact art, fog cover, or flat green/teal sheet.

19. Checkpoint 3 - collect Frame Debugger or Stats route proof for sky/Aegir/ocean/foam/contact. Acceptance proof: `frame_debugger_stats.md` includes skybox and visible Aegir/cloud/moon passes, ocean/foam/contact draws, material instance count, SetPass and batches if available. Fallback: if Frame Debugger/Stats cannot be opened without mutation or process contention, write `ABORTED_frame_debugger_stats.md`; no render-route acceptance.

20. Read active terrain/material route:
   - active terrain receiver path;
   - active terrain material path;
   - shader name/path;
   - basalt, sand, wetness, normal, mask slots;
   - missing GUID/null/stale slot state;
   - `Mat_Terrain.mat`, `terrain.mat`, and `Mat_TriplanarRock.mat` active/stale/unused/candidate-only classification;
   - MapMagic relation only if visible without mutation.
   Acceptance proof: `h8_1475_terrain_material_slots_inspector.png`. Reject active route using stale unresolved terrain materials.

21. Capture photic terrain proof: `h8_1475_photic_terrain_route_game.png`. Acceptance proof: route-readable photic terrain with material truth, scale, and no dark/noisy/flat hide. Reject random noise, crushed silhouettes, primitive blobs, blurry material, or post/fog cover.

22. Capture shallow underwater proof: `h8_1475_underwater_0_5m_route_game.png` if Play Mode/readback is already active and safe. Acceptance proof: readable water volume, ceiling/surface interaction, seabed visibility, route cues, and non-flat water color. Fallback: write `ABORTED_underwater_0_5m_route_game.md` if unsafe. Reject full-screen haze, green slab water, black water, or route-empty view.

23. Read route-wide product-face blockers:
   - `Assets/_Project/Prefabs/Player.prefab`
   - `Assets/_Project/Prefabs/Tools/Held/*`
   - `Assets/_Project/Prefabs/Items/Tools/*`
   - `Assets/_Project/Prefabs/Resources/Pickups/*`
   - `Assets/_Project/Prefabs/Transport/*`
   - `Assets/_Project/Prefabs/Sky_System.prefab`
   - `Assets/_Project/Prefabs/Ocean_Crest.prefab`
   - active `02_HECTON_WORLD` product-face scene instances
   - `Assets/_Project/Prefabs/WorldProceduralProxy`
   - `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
   - `Assets/_Project/Art/Materials/WorldProceduralProxy/*`
   Acceptance proof: classification rows for null, missing slot, missing channel semantics, blockout, placeholder, package default, built-in primitive mesh, visible proxy, visible placeholder, insufficient LOD, collider role, candidate-only state.

24. Capture product-face primitive/blockout/default inspector proof: `h8_1475_product_face_primitive_targets_inspector.png`. Acceptance proof: representative player/tool/resource/transport/sky/ocean blockers visible in inspector. Reject any visible product-face route still using built-in primitive mesh, null material, blockout material, placeholder material, package-default `Lit.mat`, or proxy material without accepted exception.

25. Checkpoint 4 - update `no_mutation_readback_report.md`, `dirty_state_audit.md`, `console_export.txt`, and `manifest.json`. Acceptance proof: every row includes domain, scene/asset path, object path, active scene, active state, component type, renderer enabled state, mesh, material, shader, texture slot, GUID if exposed, effective/ignored/stale classification, LODGroup count, collider role, Addressables group/key only if visible, product-face classification, evidence artifact, rejection note or risk. Fallback: incomplete table keeps packet `PENDING_VERIFICATION`.

26. Collect GCMonitor/profiler boundaries only if already available without mutation or process contention. Acceptance proof: report whether GC/frame-time/memory proof is `ABSENT`, `GCMonitor_CAPTURED`, `PROFILER_CAPTURED`, or `MEMORY_CAPTURED`. Screenshots and Frame Debugger do not prove `0 B/frame`, frame time, memory residency, save/load, or platform readiness. Fallback: mark runtime performance `PENDING_VERIFICATION`.

27. Perform visual-reference comparison against the mandatory reference set recorded in `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605`, the image-read digest `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`, the owner rows in `VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv`, and the fixed template `H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md`. Acceptance proof: `h8_1475_visual_reference_comparison.md` maps screenshots to water volume, shoreline contact, terrain material truth, Aegir/sky hero quality, underwater route density, HUD/cockpit integration, product-face state, and proof packet validity. It must explicitly state which mandatory VREF signals each shot satisfies or fails. Reject any claim supported only by raw MCP PNGs, stale screenshots, static reports, or controller prose.

28. Finalize dirty-state audit. Acceptance proof: `dirty_state_audit.md` states no scene, prefab, material, importer, Addressables, package, or project settings dirty state after readback, or `ABORTED_BEFORE_MUTATION` with exact dirty object path. Fallback: if Unity is dirty, do not save; abort.

29. Finalize `manifest.json`, compute actual `manifest.sha256`, copy the Unity session log to `UnityLog.txt`, and verify every listed artifact exists under the session root. Acceptance proof: no orphan claims in manifest. Fallback: missing artifact keeps acceptance state `PENDING_VERIFICATION`.

30. Checkpoint 5 - write final controller report under `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_<YYYYMMDD_HHMMSS>.md`. Acceptance proof: report states what was wrong, what was read, in-game/editor result, what was verified, what remains rejected, runtime/profiler/GC/memory proof class, and exact blocker triage. Do not use `ACCEPTED`, `COMPLETE`, `READY`, `AAA`, `OPTIMIZED`, or `0 B/frame` unless matching artifacts exist.

## Failure And Abort Rules

Abort immediately and write `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_36_H8_1475_ABORT_<YYYYMMDD_HHMMSS>.md` if any condition occurs:

- CPU rises above `50 percent`.
- `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, `VBCSCompiler`, package/import/build process becomes busy.
- Unity asks to save scene, prefab, material, project, import setting, package, or Addressables asset.
- Unity marks any scene, prefab, material, importer, Addressables setting, or project setting dirty during readback.
- Unity attempts automatic package/import/project repair.
- Compile/import/shader error starts.
- Console contains errors after scene load or Play Mode handoff.
- Frame Debugger/Stats cannot be opened for required render proof and no safe alternate report can be produced.
- Required target object/material/mesh cannot be found through no-mutation readback.
- Any needed artifact would write under `Assets/`.
- Any readback action would require changing active scene state beyond safe Play Mode/session inspection.

Abort report must include:

- process gate state;
- current Unity state;
- last safe task number;
- exact abort reason;
- dirty object path if any;
- console error summary if any;
- statement that no save/apply/mutation was performed.

## Rejection Gates

- Raw MCP PNGs are rejected as acceptance proof.
- Static source reports are not runtime or visual acceptance.
- Game View screenshots prove visual state only, not GC, frame time, memory, residency, save/load, build health, or platform readiness.
- Frame Debugger/Stats prove render route only, not gameplay acceptance.
- Product-face source gates remain failed until Unity readback proves active replacement or non-visual exception.
- No visual pass may hide weak surface, sky, water, terrain, HUD, shoreline, Aegir, or photic route work behind darkness, fog, bloom, post, full-screen haze, or misleading angle.
- No Crest material clone/wrapper/override route is allowed.
- No binary quality switch claim is allowed. Continuous `GlobalQualityWeight` consequences must be recorded for low, middle, high, and ultra without changing gameplay truth or route ownership.
- No dirty state may be saved.
- No fake hashes, fake line numbers, fake microseconds, fake profiler numbers, or "looks good" acceptance.

## Required Blocker Triage Categories

Use these exact categories in `no_mutation_readback_report.md` and final report:

- `PASS_STATIC_ONLY`
- `PASS_UNITY_READBACK`
- `PENDING_VISUAL_REVIEW`
- `PENDING_PROFILER_PROOF`
- `PENDING_GC_PROOF`
- `PENDING_MEMORY_PROOF`
- `BLOCKED_PROCESS_GATE`
- `BLOCKED_DIRTY_STATE`
- `BLOCKED_CONSOLE_ERROR`
- `BLOCKED_READBACK`
- `BLOCKED_FRAME_DEBUGGER_STATS`
- `REJECTED_VISUAL_FLOOR`
- `REJECTED_PRODUCT_FACE_SOURCE`
- `REJECTED_RAW_MCP_PNG`
- `REJECTED_MUTATION_RISK`
- `CANDIDATE_ONLY_NOT_ACTIVE`
- `NON_VISUAL_INPUT_EXCEPTION`

## Regression Model To Report

- CPU: no runtime changes are authorized. Readback must not add scripts, wrappers, polling, scene searches, material instances, or editor utilities under `Assets/`.
- GC: no gameplay path changes are authorized. `0 B/frame` cannot be claimed without GCMonitor or Profiler artifact.
- Memory/VRAM: texture slot/source reachability is not residency proof. Runtime memory readiness requires captured memory artifact.
- Cadence: no cadence changes are authorized.
- Correctness: one fact, one owner, one route, one proof artifact. Missing owner/route/proof keeps blocker alive.
- Visual: surface, sky, Aegir, ocean, shoreline, photic terrain, HUD/cockpit, and shallow underwater route must meet the reference floor. Darkness/fog/post cannot hide weak art.
- Mutation: packet is invalid if readback saves or alters scenes, prefabs, materials, importers, Addressables, packages, code, or project settings.

## Continuous GlobalQualityWeight Consequences To Record

- Low/compact near `0.0`: fewer secondary layers, lower optional detail, cheaper reflections, reduced particle density, and conservative diagnostics. Still requires readable water color, sky/Aegir silhouette, terrain route, HUD/instrument legibility, material identity, and no ugly mode.
- Middle around `0.35`: expected player lane. Requires active production player/HUD, route-owned sky/ocean/terrain/material stacks, no proxy/default/primitive product-face contamination, and stable first-20 screenshots.
- High around `0.7`: spend budget on richer cloud/Aegir detail, stronger waterline breakup, denser near-field geology/flora, cleaner HUD material response, longer LOD residency, and stronger material detail after proof.
- Ultra near `1.0`: visual overkill through layered atmosphere, richer surface sparkle, denser route dressing, better cockpit/visor sensory polish, and capture-grade composition. Gameplay truth, save identity, DTO layout, collision truth, Crest ownership, and public readiness state do not change.

## Final Report Format

Future execution owner final report must use:

- What was wrong.
- What was read/captured.
- In-game/editor result.
- What was verified.
- What remains rejected or pending.
- Files/artifacts created.
- No-mutation statement.

Required final status labels:

- `PENDING_VERIFICATION` if packet is incomplete or lacks runtime/profiler/GC/memory proof.
- `REJECTED / <reason>` for failed visual/source/proof gates.
- `UNITY_READBACK_CAPTURED` only for fields actually captured in Unity.

Do not claim Unity was run unless this future execution packet actually runs it and writes the proof artifacts.
