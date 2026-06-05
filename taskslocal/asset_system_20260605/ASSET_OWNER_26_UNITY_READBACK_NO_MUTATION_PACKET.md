# Asset Owner 26 - Unity Readback No-Mutation Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE` / `STATIC_IMAGE_QA` routing only.
Workspace: `c:\hades\Hecton8`.

This packet assigns a future Unity owner readback pass for product-face source gate failures and `h8_1475` readiness. It is not Unity proof, runtime proof, visual proof, profiler proof, GC proof, memory proof, or acceptance.

## Boundary

No Unity execution was performed by this packet writer.

The future Unity owner may inspect and capture only. The pass is readback-first and no-mutation:

- No `SaveScene`.
- No `EditorSceneManager.MarkSceneDirty`.
- No `EditorUtility.SetDirty`.
- No `AssetDatabase.SaveAssets`.
- No scene save.
- No project save.
- No prefab apply/revert.
- No material, prefab, scene, shader, importer, Addressables, ProjectSettings, package, or code mutation.
- No raw YAML edit.
- No temporary files under `Assets/`.
- No Crest runtime wrapper, material clone, material instantiation, or override script.

If Unity creates or exposes a dirty object during readback, stop and report the object path. Do not save it.

First-20 route moment: bright first surface exit with readable production player/HUD, sky/Aegir, ocean/Crest surface, shoreline, photic terrain, and product-face tools/resources/transport sources.

Route blocker removed: none yet. This packet defines the required no-mutation proof route for the `h8_1475` proof packet.

## Authority And Mandates Followed

Required authority/report reads used for this packet:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
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
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/Reports/Batch31/PLAYER_HUD_BOOTSTRAP_BINDING_BLOCKER_20260605.md`
- `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`
- `Docs/Reports/Batch31/SKY_TEXTURE_SLOT_RESOLUTION_20260605.md`

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`

## Source Gate State

`ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md` reports three failed Unity batchmode source gates:

- Product-face material/texture gate: `FAILED`.
- Product-face prefab quality gate: `FAILED`.
- Sky/ocean source primitive gate: `FAILED`.

Known hard blockers:

- Product-face player, tools, resources, transport, construction, shell, sky/ocean/depth, and diagnostic targets still contain placeholder, blockout, package-default, forbidden route, missing PBR role, or missing channel-semantics failures.
- `Assets/_Project/Prefabs/Player.prefab` contains product-face renderers using blockout material and URP package default `Lit.mat` routes.
- `Assets/_Project/Prefabs/Tools/Held/*` and `Assets/_Project/Prefabs/Items/Tools/*` use placeholder tool materials and/or built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Resources/Pickups/*` and `Assets/_Project/Prefabs/Transport/*` use Unity built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Sky_System.prefab` has active visible primitive sky dome risk: `Sky_System/Sphere` uses Unity built-in primitive mesh `Sphere`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab` has product-face primitive risk: `SargassumMicroFaunaBoids.boidMesh` points to Unity built-in primitive mesh `Plane`.
- Existing MCP screenshots are rejected as acceptance artifacts because no valid `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` manifest/checksum/log packet exists.

## Hard Gate Before Unity Readback Starts

Do not start Unity readback unless all gates are green:

- CPU total is `<= 50 percent`.
- No active or busy `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, `VBCSCompiler`, or package/import/build process.
- Unity is closed or idle.
- No import spinner.
- No script compilation.
- No shader compilation.
- No package resolution.
- No player build.
- No existing Unity save prompt.
- No dirty scene/prefab/material/project prompt.
- No automatic upgrade/import repair prompt.

Suggested process gate commands for the future Unity owner:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time'
Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,ShaderCompiler,AssetImportWorker,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue
```

Required result: CPU `<= 50 percent` and no busy Unity/build/import/shader/compiler process. If the result is ambiguous, abort. Do not launch Unity to "check anyway".

## h8_1475 Proof Packet Contract

Acceptance remains `PENDING_VERIFICATION` until the future Unity owner creates a complete packet:

- Root: `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/`
- `manifest.json`
- `manifest.sha256`
- copied Unity log for the session
- no-mutation readback report
- console export
- canonical screenshots listed below
- Frame Debugger or Stats report where required
- explicit dirty-state audit

The `manifest.json` must include:

- session id;
- Unity editor version;
- active scene sequence;
- process gate values;
- artifact list with relative paths;
- screenshot names and camera/source labels;
- copied Unity log path;
- console export path;
- readback report path;
- Frame Debugger/Stats report paths;
- dirty-state result;
- mutation result: `NO_MUTATION` or `ABORTED_BEFORE_MUTATION`;
- acceptance state: `PENDING_VERIFICATION`, never `ACCEPTED`.

`manifest.sha256` must hash `manifest.json`. Do not invent hashes. Generate the hash from the actual file.

## Global No-Mutation Rules

Apply to every readback step:

- Use Inspector/API readback only.
- Capture screenshots and write reports only under `Docs/`.
- Never write under `Assets/`.
- Never fix a missing slot during this pass.
- Never bind candidate textures.
- Never assign a material.
- Never replace a mesh.
- Never edit terrain layers.
- Never alter Crest settings.
- Never change canvas render modes.
- Never disable a shell, proxy, default route, primitive, or blockout object to make the screenshot look better.
- Never accept "not visible from this angle" as proof that a route is safe. Record active state, renderer enabled state, material, mesh, and route visibility.

## Required Readback Fields

Every table row in the no-mutation readback report must include:

- domain;
- scene or asset path;
- scene object path if applicable;
- active scene name;
- active state;
- component type;
- renderer enabled state if applicable;
- mesh name;
- mesh asset path or built-in primitive classification;
- material asset path;
- shader name;
- texture slot name;
- texture asset path or null/missing;
- GUID if Unity exposes it without mutation;
- property effective/ignored/stale classification;
- LODGroup presence and level count for product-face renderers;
- collider type and whether it is visual, trigger, proxy, or gameplay collision;
- Addressables group/key only if visible in Inspector without creating settings;
- product-face classification: player, HUD, sky, Aegir, ocean, terrain, tool, resource, transport, proxy, blockout, default route, primitive;
- evidence class;
- screenshot/report artifact path;
- rejection note or residual risk.

Do not omit nulls, stale rows, package-default routes, blockout material routes, built-in primitive mesh IDs, disabled active objects, or hidden proxy routes.

## Ordered Readback Sequence

### 1. Active Player And HUD Binding

Purpose: prove whether `02_HECTON_WORLD` binds the production player/HUD graph or the scene-authored shell route.

Readback targets:

- Active scene after handoff.
- `BootstrapState.CurrentPlayerObject` name, scene, active state, tag, and instance/source classification.
- Whether current player is a prefab instance of `Assets/_Project/Prefabs/Player.prefab`.
- Scene-authored `Player` object in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- `HectonWorldShellController1428` enabled state.
- Production player component presence:
  - `Hecton8.Gameplay.HectonPlayerMovement`
  - `Hecton8.Interaction.PlayerInteraction`
  - `Hecton8.UI.PlayerPDA`
  - `PlayerToolManager`
  - `PlayerInventory`
  - `PlayerFlashlight`
  - `VisorHUDController`
  - `HUDNotification`
  - `ToolLoadoutProvisioner`
- `PlayerRuntimeContextService` binding state and any player movement/camera/tool/inventory/survival flags exposed without mutation.
- Active HUD, visor, PDA, interaction, quickbar, notification, and oxygen canvases.
- Canvas render modes and world-space projection state.
- `GraphicRaycaster` enabled count on gameplay HUD canvases.
- Whether `Hecton8.UI.InteractionUI` or `Hecton8.Interaction.InteractionUI` exists/enabled.

Required captures:

- Game View: HUD visible in first-person world view.
- Game View: PDA open if it can be opened without mutation and without saving state.
- Scene View: selected active player object and component stack.
- Inspector screenshot: representative HUD canvas render mode and `GraphicRaycaster` state.

Abort if:

- Unity requires scene save after entering/exiting Play Mode.
- Production player binding cannot be read without changing scene state.
- Play Mode readback causes compile/import/build activity.
- A runtime exception or console error appears after scene handoff.

Reject if:

- Scene shell owns active player movement.
- Production player prefab is not bound and no explicit owner route proves replacement.
- Gameplay HUD remains `ScreenSpaceOverlay` as an interactive first-party route without projection proof.
- HUD proof omits source owner for oxygen/pressure/tool state.

### 2. Sky, Aegir, Clouds, Moons

Purpose: prove active sky/celestial material route and stale cloud slot state without guessing slot fixes.

Readback targets:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- active `RenderSettings.skybox`
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceGasGiant_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat`
- `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_*.mat`

Required slot readback:

- `Mat_HectonSky.mat` `_MainCloudTex`
- `Mat_HectonSky.mat` `_HighCloudTex`
- `Mat_HectonSky.mat` `_MainCloudAtlas`
- `Mat_HectonSky.mat` `_StarTex`
- `Mat_HectonSky.mat` `_StarTwinkleLUT`
- `Mat_HectonSky.mat` `_BakedStarCubemap`
- `Mat_HectonSky_CloudOverlay.mat` `_MainCloudTex`
- Aegir band/cloud/disc slots exposed by the shader
- moon albedo/normal/mask slots where applicable

Texture candidates are source context only, not assignments:

- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod1.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod2.png`
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`
- `Assets/_Project/Art/TEXTURES/clouds.png`
- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`

Required captures:

- Game View: bright surface sky/Aegir/ocean context.
- Scene View: selected sky/Aegir/cloud/moon renderer.
- Inspector screenshot: active skybox material and stale/effective slots.
- Frame Debugger or Stats report: skybox and visible Aegir/cloud/moon passes if available.

Reject if:

- `_HighCloudTex` or `_MainCloudAtlas` is claimed fixed without Unity material/shader effective-property readback.
- Active sky/Aegir is dark, muddy, smeared, toy-like, or hidden by post/fog/exposure.
- Orbit/prologue material state is used as proof for `02_HECTON_WORLD`.
- Moons visibly rely on generic terrain/rock/basalt maps as hero celestial art without accepted route proof.

### 3. Crest, Ocean, Foam, Micro-Fauna Primitive Risk

Purpose: prove active Crest/ocean materials, stale wave-data slots, foam/contact route, and built-in primitive mesh risk without altering Crest assets.

Readback targets:

- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- active OceanRenderer object in `02_HECTON_WORLD`
- `Assets/Crest/Crest/Materials/Ocean.mat`
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`
- `SargassumMicroFaunaBoids.boidMesh`
- Crest input exceptions:
  - `Ocean_Crest/SargassumWaveDampingInput`
  - `Ocean_Crest/SargassumFoamDampingInput`
  - `Ocean_Crest/SargassumOilFilmInput`

Required slot/readback fields:

- effective ocean material asset path;
- underwater material asset path;
- normals, foam, caustics texture slots;
- `_WD_*` wave-data slots and whether they are shader-effective, runtime-populated, or stale serialized rows;
- whether `MAT_H8_SurfaceCrestOcean_1428.mat` is active or only a candidate;
- whether `SargassumMicroFaunaBoids.boidMesh` is Unity built-in `Plane`;
- whether accepted hidden input primitives are non-visual data inputs only.

Required captures:

- Game View: bright ocean surface and shoreline/waterline.
- Game View: underwater 0-5 m view if Play Mode readback is already active and safe.
- Scene View: selected OceanRenderer and foam/input object.
- Inspector screenshot: `SargassumMicroFaunaBoids.boidMesh`.
- Frame Debugger or Stats report: ocean/foam/contact draws, material instance count, SetPass/batches.

Reject if:

- Crest material state is claimed from static YAML only.
- `SargassumMicroFaunaBoids.boidMesh` remains built-in `Plane` as visible product-face micro-fauna route.
- `_WD_*` slots are patched or replaced with artist textures during readback.
- `foam.png` is visible as final repeated shoreline/contact art.
- Any Crest material clone/wrapper/override route exists or is introduced.

### 4. Terrain, Geology, Active Material Route

Purpose: prove active terrain/material route and classify stale terrain materials without guessing GUID fixes.

Readback targets:

- active terrain receiver(s) in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- terrain material/template actually used in the visible route
- `Assets/_Project/Art/Materials/Mat_Terrain.mat`
- `Assets/_Project/Art/Materials/terrain.mat`
- `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- `Assets/_Project/Art/Shaders/TerrainMaster.shader`
- `Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader`
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`
- `Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`

Required slot/readback fields:

- active terrain material path;
- shader path/name;
- basalt/sand/wetness/normal/mask slots;
- missing GUID/null slot state;
- whether `terrain.mat` or `Mat_TriplanarRock.mat` is stale, active, or unused;
- visible terrain renderer/material state in route captures;
- terrain material route relation to MapMagic if exposed without mutation.

Required captures:

- Game View: photic terrain/shoreline from gameplay height.
- Scene View: selected terrain receiver/material.
- Inspector screenshot: active terrain material and slot state.
- Frame Debugger or Stats report: terrain draw material, SetPass/batches if available.

Reject if:

- visible route uses stale `terrain.mat` or `Mat_TriplanarRock.mat` with unresolved shader/GUID slots.
- terrain is dark, random-noise, low-poly, blurry, flat, or hiding weak material work behind water/fog.
- material route would be rejected if accepted without active terrain receiver proof.

### 5. Proxy, Null, Blockout, Package-Default, Primitive Product-Face Routes

Purpose: produce one route-wide table of every product-face blocker that remains source-visible or runtime-visible.

Readback target categories:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Tools/Held/*`
- `Assets/_Project/Prefabs/Items/Tools/*`
- `Assets/_Project/Prefabs/Resources/Pickups/*`
- `Assets/_Project/Prefabs/Transport/*`
- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- active `02_HECTON_WORLD` scene instances using product-face renderers
- `Assets/_Project/Prefabs/WorldProceduralProxy`
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/*`

Required classification buckets:

- `NULL_MATERIAL`
- `MISSING_TEXTURE_SLOT`
- `MISSING_CHANNEL_SEMANTICS`
- `BLOCKOUT_MATERIAL`
- `PLACEHOLDER_MATERIAL`
- `PACKAGE_DEFAULT_MATERIAL`
- `UNITY_BUILTIN_PRIMITIVE_MESH`
- `PROXY_MATERIAL_VISIBLE`
- `PROCEDURAL_PLACEHOLDER_VISIBLE`
- `LOD_MISSING_OR_INSUFFICIENT`
- `COLLIDER_PROXY_MISSING`
- `CANDIDATE_ONLY_NOT_ACTIVE`

Product-face primitive mesh target rows must include:

- prefab or scene path;
- object path;
- component field;
- mesh name;
- built-in primitive type;
- renderer visibility;
- active state;
- material path;
- whether the primitive is visual, non-visual input, collider proxy, or unknown.

Required captures:

- Inspector screenshot for representative player/tool/resource/transport primitive or blockout route.
- Inspector screenshot for `Sky_System/Sphere`.
- Inspector screenshot for `SargassumMicroFaunaBoids.boidMesh`.
- Scene/Game screenshot only if the route is visible in the active first-20 view.

Reject if:

- Any visible product-face renderer keeps Unity built-in primitive mesh as final visual route without authored/generated replacement proof.
- Package-default `Lit.mat`, null material, blockout material, placeholder material, or proxy material remains on visible product-face route.
- LOD/collider proof is absent for product-face replacement candidates.

## Required Canonical Screenshots

The `h8_1475` packet must include, at minimum:

- `h8_1475_surface_sky_aegir_ocean_hud_game.png`
- `h8_1475_surface_shoreline_waterline_game.png`
- `h8_1475_photic_terrain_route_game.png`
- `h8_1475_underwater_0_5m_route_game.png`
- `h8_1475_player_hud_binding_scene_selected.png`
- `h8_1475_sky_aegir_slots_inspector.png`
- `h8_1475_crest_ocean_slots_inspector.png`
- `h8_1475_terrain_material_slots_inspector.png`
- `h8_1475_product_face_primitive_targets_inspector.png`

If any view is impossible to capture safely without mutation or process contention, the packet must include an `ABORTED_<view>.md` note instead of a fake screenshot.

## Abort Conditions

Abort immediately and write only a concise abort report under `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_26_ABORT_<YYYYMMDD_HHMMSS>.md` if any condition occurs:

- CPU rises above `50 percent`.
- `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, `VBCSCompiler`, package, import, or build process becomes busy.
- Unity asks to save a scene, prefab, project, material, import setting, or Addressables asset.
- Unity marks any scene/material/prefab/importer/Addressables/project setting dirty during readback.
- Unity tries to auto-create Addressables settings or package/import metadata.
- A compile/import/shader error starts.
- Console contains errors after scene load or Play Mode handoff.
- Frame Debugger/Stats cannot be opened for a required render proof step.
- Required target object/material/mesh cannot be found in Unity readback.
- Any needed artifact would write under `Assets/`.
- A readback action would require changing active scene state beyond safe Play Mode/session inspection.

Abort report must include:

- process gate state;
- current Unity state;
- last safe readback step;
- exact abort reason;
- dirty object path if any;
- console error summary if any;
- statement that no save/apply/mutation was performed.

## Readback Result Status Rules

- Until a complete `h8_1475` proof packet exists, status remains `PENDING_VERIFICATION`.
- Static source gates remain failed until Unity readback proves a replacement or a non-visual exception.
- Runtime/visual acceptance cannot be claimed from existing MCP screenshots.
- Screenshots prove visual state only, not frame time, GC, memory, residency, save/load, or platform readiness.
- Frame Debugger/Stats prove render route only, not gameplay acceptance.
- No `ACCEPTED`, `COMPLETE`, `READY`, `AAA`, `OPTIMIZED`, or `0 B/frame` wording is allowed without matching artifacts.

## Regression Model For Future Unity Owner

- CPU: no runtime changes are authorized by this packet. Readback must not add scripts, polling, wrappers, material instances, importer loops, or scene searches into gameplay. Runtime CPU claims require profiler artifacts.
- GC: no gameplay path changes are authorized. HUD/player readback cannot claim zero-GC without GCMonitor or Profiler evidence.
- Memory/VRAM: source reachability and texture slot presence are not residency proof. `h8_1475` must record texture memory, total reserved memory, and VRAM pressure state if runtime visual readiness is claimed.
- Cadence: no update cadence changes are authorized. Quality/cadence claims remain pending unless measured.
- Correctness: one fact, one owner, one route, one proof artifact. Product-face source gates stay failed when owner/route/proof is missing.
- Visual: surface, sky, Aegir, ocean surface, shoreline, photic terrain, HUD, and first-person route captures must meet the Subnautica-level floor. Darkness/fog/post cannot hide weak art.

## Continuous GlobalQualityWeight Consequences To Record

- Low / compact near `0.0`: fewer secondary visual layers, lower residency, cheaper reflections, conservative foam/contact detail, fewer particles, and reduced diagnostic depth. Still requires readable ocean color, sky/Aegir silhouette, terrain route, HUD/instrument legibility, product-face material identity, and no ugly mode.
- Middle around `0.35`: expected player lane. Requires active production player/HUD, route-owned sky/ocean/terrain/material stacks, no proxy/default/primitive product-face contamination, and stable first-20 screenshots.
- High around `0.7`: spend budget on richer cloud/Aegir detail, stronger waterline breakup, denser near-field geology/flora, cleaner HUD material response, and longer LOD residency after proof.
- Ultra near `1.0`: visual overkill through layered atmosphere, richer surface sparkle, denser route dressing, better material detail, stronger cockpit/visor sensory polish, and capture-grade composition. Gameplay truth, save identity, DTO layout, collision truth, Crest ownership, and public readiness state do not change.

## Future Owner Outputs

Required no-mutation outputs:

- `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/manifest.json`
- `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/manifest.sha256`
- `Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/UnityLog.txt`
- canonical `h8_1475_*.png` screenshots listed above
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_<YYYYMMDD_HHMMSS>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_26_FRAME_DEBUGGER_STATS_<YYYYMMDD_HHMMSS>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_26_CONSOLE_<YYYYMMDD_HHMMSS>.txt`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_26_DIRTY_STATE_AUDIT_<YYYYMMDD_HHMMSS>.md`

Final status for this packet: `PENDING_VERIFICATION`.
