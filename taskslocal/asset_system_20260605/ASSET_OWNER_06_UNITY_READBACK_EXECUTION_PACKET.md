# Asset Owner 06 - Unity Readback Execution Packet

Owner role: Asset Worker 3219 - Unity Readback Execution Packet Owner.
Workspace: `c:\hades\Hecton8`.
Write scope of this file: execution packet only. This is not proof.

## Boundary

This packet converts static asset-front blockers into one ordered Unity readback sequence for a future Unity owner. It does not prove Unity import state, runtime binding, visual quality, Addressables residency, memory, frame time, audio mix, or 0 B/frame GC.

No current execution is authorized by this packet authoring pass:

- No Unity launch.
- No dotnet/build/importer/Play Mode run.
- No `Assets/` edits.
- No prefab/material/scene apply.
- No project settings edits.
- No temporary files under `Assets/`.

First-20-minutes route moment: bright surface exit, Aegir/sky/moons, ocean/shoreline foam, photic terrain/flora, suit oxygen HUD, first-exit audio, and asset residency blockers.

## Authority And Mandates Followed

- `AGENTS.md`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `rendering.md`
- `water.md`
- `terrain.md`
- `ui.md`
- `audio.md`
- `streaming.md`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Static source reports used:

- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/Reports/AssetSystem_20260605/MATERIAL_READBACK_PREFLIGHT_STATIC_BLOCKERS_3215_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md`
- `Docs/Reports/AssetSystem_20260605/UI_SPRITE_ROUTE_STATIC_TABLE_3216_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/Reports/AssetSystem_20260605/MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md`
- `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md`

Mandatory visual-reference digest use:

- Every future visual readback step for sky/Aegir/moons, Crest/ocean/foam, terrain/geology, flora/proxy, UI oxygen, and Addressables-covered visual assets must compare captures against `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`.
- Missing comparison keeps the route `PENDING VERIFICATION`. The digest does not replace Unity readback, Frame Debugger, Console, memory, or profiler proof.

## Hard Gate Before Unity Owner Starts

Do not start this readback if any gate fails:

- CPU total is above 50 percent.
- Any `Unity`, `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, or `PackageManager` process is active/busy.
- Unity import/compilation spinner is active.
- Unity Console has compile errors on open.
- Project prompts for automatic upgrade/import repair that would mutate assets.
- Target scene is already dirty or Unity prompts to save unrelated scene changes.
- Frame Debugger is unavailable for required material/pass proof.
- Disk writes would land under `Assets/`.

Suggested process gate commands for the future Unity owner:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time'
Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,ShaderCompiler,PackageManager -ErrorAction SilentlyContinue
```

Required result before Unity readback: CPU <= 50 percent and no busy Unity/build/import/shader/package process.

## Proof Artifact Naming

Use only these artifact roots:

- Screenshots: `Docs/Screenshots/HectonProofPackets/`
- Reports/readback tables: `Docs/Reports/AssetSystem_20260605/`

Use this name pattern:

- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_06_<step>_<view>_<YYYYMMDD_HHMMSS>.png`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_UNITY_READBACK_<step>_<YYYYMMDD_HHMMSS>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_FRAME_DEBUGGER_<step>_<YYYYMMDD_HHMMSS>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_CONSOLE_<YYYYMMDD_HHMMSS>.txt`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_ADDRESSABLES_READBACK_<YYYYMMDD_HHMMSS>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_VISUAL_REFERENCE_COMPARISON_<YYYYMMDD_HHMMSS>.md`

Do not save screenshots, logs, exports, or temporary captures under `Assets/`.

## Global No-Save Rules

Apply to every step:

- Do not press `Apply` on prefabs.
- Do not press `Apply All` or `Revert All`.
- Do not save scenes.
- Do not save project.
- Do not edit material slots, texture slots, shader assignments, import settings, AudioClip settings, Addressables settings, SpriteAtlas settings, or prefab references.
- Do not create Crest runtime wrappers, material clones, override scripts, or temporary materials.
- Do not raw-edit `.mat`, `.prefab`, `.unity`, `.asset`, `.meta`, or Addressables YAML.
- If Unity marks a scene, material, prefab, import setting, or Addressables asset dirty during readback, stop and record the dirty object path in the report. Do not save.

## Ordered Readback Sequence

### 1. Scene, Sky, Aegir, Moons

Purpose: prove effective scene skybox and visible celestial material bindings before any sky/Aegir art claim.

Exact paths and objects:

- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
  - `m_SkyboxMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `m_SkyboxMaterial: {fileID: 0}`. Confirm fallback path only.
- `Assets/_Project/Scenes/01_ORBIT.unity`
  - `m_SkyboxMaterial` -> `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - `m_SkyboxMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `skyMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `_skyMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `daySkybox` -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - `nightSkybox` -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
  - `blendedSkyboxMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - Aegir haze renderer -> `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat`
  - Aegir impostor override -> `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
  - Aegir sky renderer -> `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
- Materials to inspect:
  - `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
  - `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
  - `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
  - `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`
  - `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`
  - `Assets/_Project/Art/Materials/World/MAT_SurfaceGasGiant_1428.mat`
  - `Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat`
  - `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat`
  - `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat`
  - `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat`

Read-only actions:

- Open target scenes one at a time.
- Record effective skybox material, renderer material, shader name, texture slot values, null slots, missing shader warnings, and missing texture warnings.
- Specifically read `Mat_HectonSky.mat` slots `_MainCloudTex`, `_HighCloudTex`, and `_MainCloudAtlas`.
- Specifically read Aegir slots using `clouds0_diff.png`, `Aegir_storms.png`, `oblakajip.png`, `oblaka!.png`, `TX_H8AegirGasGiantBakedDisc_1428.png`, and prologue cloud sources where present.
- Inspect moon materials for terrain/rock texture reuse and whether those moon renderers are actually visible in the route scene.

Proof artifacts needed:

- `ASSET_OWNER_06_scene_sky_aegir_game_surface_<timestamp>.png`: Game View surface sky/Aegir/moons.
- `ASSET_OWNER_06_scene_sky_aegir_scene_selected_<timestamp>.png`: Scene View with selected sky/Aegir/moon object or material.
- `ASSET_OWNER_06_UNITY_READBACK_scene_sky_aegir_<timestamp>.md`: table of scene object, material, shader, slots, null/missing refs, active/inactive status.
- `ASSET_OWNER_06_FRAME_DEBUGGER_scene_sky_aegir_<timestamp>.md`: skybox pass, Aegir/cloud renderers, moon renderers if visible.
- Console export after scene load.
- Digest comparison notes for surface sky/Aegir/moons against the mandatory image-read digest.

Reject conditions:

- Surface sky, Aegir, moons, or clouds are dark, muddy, soft/toy-like, missing, or below Subnautica-level floor.
- `Mat_HectonSky.mat` active slots remain null or missing without explicit fallback proof.
- `MAT_AegirSky_Master.mat` or other active Aegir shader/material cannot resolve in Unity.
- `TX_H8AegirGasGiantBakedDisc_1428.png` is used as final hero Aegir without replacement/proof.
- Moon route visibly uses generic rock/basalt terrain texture as hero celestial art.
- Any screenshot hides weak sky/celestial art through darkness, fog, bloom, or exposure crush.
- Future capture lacks mandatory digest comparison for `BEST ILLUST` surface composition, Aegir/gas-giant scale, cloud layering, and ocean/shore context.

No-save/no-apply rules:

- Do not assign cloud, skybox, Aegir, moon, or shader slots.
- Do not fix missing GUIDs during readback.
- Do not save scene lighting changes.

### 2. Crest, Ocean, Foam, Shoreline Contact

Purpose: prove active Crest material slots and whether rejected foam/contact sources contribute to visible surface/shoreline water.

Exact paths and objects:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - `oceanUnderwaterMaterial` -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - Crest foam input renderer -> `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
  - `RegisterFoamInput` component
  - `ShapeGerstnerBatched` component
  - Spectrum -> `Assets/_Project/Art/Materials/World/Photic1457/SPEC_H8_SurfaceReadableWaves_1457.asset`
  - `UnderwaterRenderer` with `_copyOceanMaterialParamsEachFrame: 1`
- Materials and textures:
  - `Assets/Crest/Crest/Materials/Ocean.mat`
  - `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
  - `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`
  - `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
  - `Assets/Crest/Crest/Textures/Foam2.png`
  - `Assets/Crest/Crest/Textures/foam.png`
  - `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png`
  - `Assets/Crest/Crest/Textures/Caustics/Caustics_tex_color.png`
  - `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset`

Read-only actions:

- Inspect active OceanRenderer/prefab instance material references.
- Read active ocean material, underwater material, foam texture, normals texture, caustics texture, foam toggles, wave foam scalars, and `RegisterFoamInput` material.
- Confirm whether `MAT_H8_SurfaceCrestOcean_1428.mat` is active or only a candidate.
- Confirm active users of `MAT_SurfaceSplashFoamDirty_1428.mat` and whether `Assets/Crest/Crest/Textures/foam.png` contributes to visible shoreline/ocean contact.
- Confirm no Crest material wrapper, clone, or override script is introduced.

Proof artifacts needed:

- `ASSET_OWNER_06_crest_foam_game_surface_<timestamp>.png`: Game View ocean surface/shoreline foam.
- `ASSET_OWNER_06_crest_foam_scene_selected_<timestamp>.png`: Scene View with OceanRenderer/foam input selected.
- `ASSET_OWNER_06_UNITY_READBACK_crest_foam_<timestamp>.md`: active material/slot/scalar table.
- `ASSET_OWNER_06_FRAME_DEBUGGER_crest_foam_<timestamp>.md`: Crest ocean/foam passes and visible foam/contact draw.
- Console export after scene load.
- Digest comparison notes for ocean/shoreline/foam/contact against the mandatory image-read digest.

Reject conditions:

- `foam.png` appears as visible repeated turquoise shoreline/waterline art.
- Foam/contact hides weak terrain, repeats obviously, or looks like a flat sheet.
- Crest material slots are missing/null, or active material cannot be resolved.
- First-party runtime wrapper, clone, material override, or custom runtime Crest patch exists as part of the readback route.
- Surface/ocean screenshots are dark, flat, muddy, or below the water/terrain visual floor.
- Future capture lacks mandatory digest comparison for shoreline whitewater, wet contact, transparent shallow water, and readable ocean surface.

No-save/no-apply rules:

- Do not assign Crest materials.
- Do not edit Crest assets.
- Do not clone or instantiate Crest materials.
- Do not adjust foam scalars or waves.

### 3. Terrain And Geology

Purpose: prove active terrain receiver/material route and geology candidate material state before terrain or rock promotion.

Exact paths and objects:

- Scene:
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - Terrain component material/template used by visible photic/surface route.
- Materials and shaders:
  - `Assets/_Project/Art/Materials/Mat_Terrain.mat`
  - `Assets/_Project/Art/Shaders/TerrainMaster.shader`
  - `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
  - `Assets/_Project/Art/Materials/terrain.mat`
  - `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`
  - `Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`
  - `Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader`
- Texture/source anchors:
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png`
  - `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_Color.jpg`
- Candidate prefab pool:
  - `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`

Read-only actions:

- Read terrain material/template on active terrain receiver.
- Read `Mat_Terrain.mat` shader and slot values for basalt, flow normal, sand, masks, and texture arrays if present.
- Read `Mat_TriplanarRock.mat` and `terrain.mat` only to classify stale/candidate/missing routes.
- Read wet basalt candidate material slots and shader validity.
- Select sampled `ProceduralFinals` rock prefab instances or prefab assets only for Inspector readback; record LODGroup, renderer materials, collider types, and shadergraph/material validity.

Proof artifacts needed:

- `ASSET_OWNER_06_terrain_geology_game_photic_<timestamp>.png`: Game View photic terrain/geology route.
- `ASSET_OWNER_06_terrain_geology_scene_selected_<timestamp>.png`: Scene View selected terrain receiver/material or rock prefab.
- `ASSET_OWNER_06_UNITY_READBACK_terrain_geology_<timestamp>.md`: terrain receiver/material/shader/slot and sampled rock prefab table.
- `ASSET_OWNER_06_FRAME_DEBUGGER_terrain_geology_<timestamp>.md`: terrain material draw, rock material draw if visible, SetPass/batch notes from Stats if available.
- Console export after scene load.
- Digest comparison notes for terrain/geology against the mandatory image-read digest.

Reject conditions:

- Active terrain uses stale/missing shader, null material, unresolved texture, or broad direct generated source import.
- Terrain looks like random noise, flat dressing, low-poly filler, blurry broad terrain, or dark abyss terrain in surface/photic route.
- Generated Batch31/Gemini basalt/sand sources are treated as final imported material art.
- `ProceduralFinals` rocks lack material/shader import validity or show bad LOD/collider route.
- Terrain/material variety is achieved by adding unbounded independent Texture2D bindings instead of route-owned arrays/validated virtual texturing.
- Future capture lacks mandatory digest comparison for bright coastline, shallow substrate, cliff/water, and medium-depth material identity.

No-save/no-apply rules:

- Do not assign terrain materials.
- Do not edit terrain layers.
- Do not alter MapMagic, terrain settings, shader slots, texture import, or prefab colliders.

### 4. Flora Proxy Materials

Purpose: prove or reject visible route contamination from `WorldProceduralProxy` flora/coral/kelp materials.

Exact paths and objects:

- Scene:
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Direct active proxy material refs:
  - `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
  - `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat`
  - `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat`
  - `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat`
- Candidate/reject prefab pools:
  - `Assets/_Project/Prefabs/Nature/Flora/Baked`
  - `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows`
  - `Assets/_Project/Prefabs/WorldProceduralProxy`
  - `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`

Read-only actions:

- Locate scene objects using the four direct proxy materials.
- Record renderer object path, material, shader, texture slots, visibility, camera-route relevance, and distance from route if available.
- Inspect whether any proxy material appears in Game View/Scene View route slice.
- Sample `Nature/Flora/Baked` and `BioForge/Shallows` prefabs for LODGroup/material/collider readback only.
- Do not promote proxy pools; record only whether they are visible and what proof is missing.

Proof artifacts needed:

- `ASSET_OWNER_06_flora_proxy_game_photic_<timestamp>.png`: Game View photic flora/proxy area.
- `ASSET_OWNER_06_flora_proxy_scene_selected_<timestamp>.png`: Scene View with selected proxy-material object.
- `ASSET_OWNER_06_UNITY_READBACK_flora_proxy_<timestamp>.md`: object/material/visibility table plus sampled candidate prefab table.
- `ASSET_OWNER_06_FRAME_DEBUGGER_flora_proxy_<timestamp>.md`: proxy material draw if visible.
- Console export after scene load.
- Digest comparison notes for flora/coral/kelp density and silhouette against the mandatory image-read digest.

Reject conditions:

- Any `WorldProceduralProxy` material is camera-visible in product-face route without final material proof.
- `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` prefabs are used as visible placement content.
- `Nature/Flora/Baked` remains assigned to proxy-labeled materials without final material ownership/proof.
- `BioForge/Shallows/PorousRock` uses MeshCollider as visible/decorative collision route without explicit collider proxy proof.
- Flora/coral silhouette collapses, lacks LOD proof, or uses alpha blend where dither/alpha clip is required.
- Future capture lacks mandatory digest comparison for dense photic organic dressing and medium-depth silhouette/biolum anchors.

No-save/no-apply rules:

- Do not rebind materials.
- Do not edit prefab instances.
- Do not add LODGroups or colliders.
- Do not hide/delete route objects.

### 5. UI Oxygen Sprites

Purpose: prove current suit HUD oxygen binding and classify detailed icon versus mask/silhouette route.

Exact paths and objects:

- Prefab:
  - `Assets/_Project/Prefabs/UI/Suit_HUD_Canvas.prefab` if present in project.
  - If the prefab path differs, find the `Suit_HUD_Canvas.prefab` asset in Project search and record exact path. Do not edit it.
- Sprites:
  - `Assets/_Project/Art/Sprites/ui/OXYGEN.png`
  - `Assets/_Project/Art/Sprites/oxygen-tank.png`
  - `Assets/_Project/Art/Sprites/cardiogram.png`
  - `Assets/_Project/Art/Sprites/ring.png`
  - `Assets/_Project/Art/Sprites/thunder.png`
- SpriteAtlas state:
  - Strict scan found no `.spriteatlas` or `.spriteatlasv2` under `Assets/_Project`; Unity owner must confirm via Project search/Addressables settings readback.

Read-only actions:

- Inspect `Suit_HUD_Canvas.prefab` Image/Sprite refs for oxygen HUD elements.
- Record whether active oxygen sprite is `oxygen-tank.png`, `ui/OXYGEN.png`, or another asset.
- Read importer settings for the listed sprites: texture type, sRGB, mipmaps, compression, alpha transparency, packing tag/atlas participation, and Addressables key/group if visible.
- Confirm whether `oxygen-tank.png` is used as a mask/tint control or incorrectly shown as final black oxygen icon.
- Do not run a UI binding stress test in this packet unless a later owner has explicit Play Mode scope.

Proof artifacts needed:

- `ASSET_OWNER_06_ui_oxygen_prefab_inspector_<timestamp>.png`: Inspector with oxygen Image/Sprite binding visible.
- `ASSET_OWNER_06_ui_oxygen_hud_preview_<timestamp>.png`: HUD/Game or Prefab preview showing oxygen visual.
- `ASSET_OWNER_06_UNITY_READBACK_ui_oxygen_<timestamp>.md`: sprite binding/import/atlas table.
- Console export after prefab/scenes load.
- Digest comparison notes for HUD oxygen readability against bright surface/photic, medium/deep, and cockpit/visor reference contexts.

Reject conditions:

- `oxygen-tank.png` is presented as the final colored oxygen icon without explicit mask/tint proof.
- `ui/OXYGEN.png` is claimed HUD-ready without prefab binding/import/atlas/residency proof.
- No atlas owner exists for standalone 1024 UI source candidates.
- Runtime HUD path uses allocation-prone text/sprite churn, scene searches, or `TMP_Text.text` for repeated oxygen readout updates in later proof.
- Oxygen HUD is unreadable at compact target scale or appears as a black/empty icon.
- Future capture lacks mandatory digest comparison for oxygen icon/readout readability in user-visible route contexts.

No-save/no-apply rules:

- Do not replace sprite bindings.
- Do not create SpriteAtlas assets.
- Do not change import settings.
- Do not save prefab.

### 6. Audio Config And Prefab References

Purpose: prove current audio config blockers, direct prefab clip refs, mixer refs, and cue lifecycle gaps without changing import settings.

Exact paths and objects:

- Config/profile assets:
  - `Assets/_Project/Data/Audio/Music/MusicDirectorConfig_Global.asset`
  - `Assets/_Project/Data/Audio/Music/Profiles/*.asset`
  - `Assets/_Project/Data/Audio/Logs/AudioLog_chen_m_datapad_01.asset` if present.
- Prefab:
  - `Assets/_Project/Prefabs/Player.prefab`
- Audio assets and ledgers:
  - `Assets/_Project/Audio/Music for Game/*.ogg`
  - `Assets/_Project/Audio/Ambient/spaceship sounds - ambient.mp3`
  - `Assets/_Project/Audio/Atmos *.wav`
  - `Assets/_Project/Audio/Underwater Ambient.wav`
  - `Assets/_Project/Audio/Breathing/*`
  - `Assets/_Project/Audio/Movement/swimming - underwater.ogg`
  - `Assets/_Project/Audio/Movement/swimming -onwater.wav`
  - `Assets/_Project/Audio/UI/*`
  - `Assets/_Project/Audio/SFX/*`
  - `Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_EN.wav`
  - `Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_RU.wav`
  - `Docs/Audio/audio_asset_ledger.csv`
  - `Docs/Audio/audio_remediation_matrix_20260605.csv`

Read-only actions:

- Inspect `MusicDirectorConfig_Global.asset` for `_musicMixerGroup` and `_stingerMixerGroup`.
- Inspect MusicDirector profile refs and active candidate cues only as serialized route evidence.
- Inspect `Player.prefab` for direct `AudioClip` refs; record component/object path, clip path, clip duration/import load type if visible, and whether owner exception exists.
- Inspect VO stub/audio log refs and mark placeholder status.
- Read import settings only; do not change Vorbis/ADPCM/load type/compression/streaming flags.
- Do not claim listening, mix, MusicDirector cadence, DSPGraph, or 0 B/frame proof from static Inspector readback.

Proof artifacts needed:

- `ASSET_OWNER_06_audio_config_inspector_<timestamp>.png`: config showing mixer group fields.
- `ASSET_OWNER_06_audio_player_prefab_refs_<timestamp>.png`: Player prefab Inspector showing representative direct clip refs.
- `ASSET_OWNER_06_UNITY_READBACK_audio_config_prefab_refs_<timestamp>.md`: mixer/profile/direct-clip/import-readback table.
- Console export after asset/prefab readback.

Reject conditions:

- `_musicMixerGroup` or `_stingerMixerGroup` remains null in active `MusicDirectorConfig_Global.asset`.
- Direct `Player.prefab` AudioClip refs remain without owner/load/release exception proof.
- Generic constant music beds would be rejected if treated as accepted first-exit audio without MusicDirector gating/listening proof.
- UI/warning cue is inaudible or weak without warning bank/ducking/haptic/UI pairing proof.
- VO stubs are treated as final VO duration, localization, loudness, subtitle timing, or delivery proof.
- Managed audio callback/native DSP route is claimed without profiler/DSPGraph evidence.

No-save/no-apply rules:

- Do not assign mixer groups.
- Do not remove direct clip refs.
- Do not edit AudioClip import settings.
- Do not create Addressables entries or cue IDs.
- Do not save prefabs or audio assets.

### 7. Addressables Settings And Data

Purpose: prove current Addressables settings/group/key/catalog state and separate it from static source reachability.

Exact paths and objects:

- `Assets/AddressableAssetsData`
- `ServerData`
- `Library/com.unity.addressables` read only if Unity owner deliberately records local cache state as non-source evidence.
- Editor/source capability refs only as context, not proof:
  - `Assets/_Project/Scripts/Editor`
  - `Assets/_Project/Scripts/ItemCatalog.cs`
  - `Assets/_Project/Scripts/Core/Content/Editor`
- Coverage target categories:
  - Foam/contact: `Assets/Crest/Crest/Textures/foam.png`, `Assets/Crest/Crest/Textures/Foam2.png`, `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`, `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
  - Sky/Aegir/cloud: `Mat_HectonSky.mat`, `MAT_AegirSky_Master.mat`, `MAT_AegirGasGiant_Impostor_1428.mat`, cloud/Aegir textures
  - Terrain wet basalt/sand: `Mat_Terrain.mat`, wet basalt/sand texture stacks, candidate terrain materials
  - Flora/proxy materials: imported flora stacks, four proxy material refs, `Nature/Flora/Baked`
  - Audio: all classes in `Docs/Audio/audio_asset_ledger.csv`
  - Prefab candidate pools: `Nature/Rocks/ProceduralFinals`, `Nature/Flora/Baked`, `BioForge/Shallows`, and rejected proxy/placeholder pools

Read-only actions:

- In Unity Addressables Groups window, record whether settings asset exists, group list, profile, schemas, labels, entries, addresses, GUIDs, build/load paths, and `AssetLoadMode`.
- Verify whether `Assets/AddressableAssetsData` remains empty on disk after opening Unity. If Unity creates settings or marks them dirty, stop and record; do not save.
- For each target category, record group/key presence or absence.
- Record whether audio ledger rows still show `PENDING_ADDRESSABLES` and whether Unity has any matching group/key evidence.
- Do not build catalog or bundles.
- Do not create settings.

Proof artifacts needed:

- `ASSET_OWNER_06_addressables_groups_window_<timestamp>.png`: Addressables Groups window or absence/error state.
- `ASSET_OWNER_06_UNITY_READBACK_addressables_settings_<timestamp>.md`: settings/groups/profiles/schemas/entries/load-mode table.
- `ASSET_OWNER_06_ADDRESSABLES_READBACK_<timestamp>.md`: coverage matrix for the seven target categories.
- Console export after opening Addressables window.

Reject conditions:

- No Addressables settings asset, group, profile, schema, catalog, or entry evidence exists.
- Heavy terrain/ocean/sky/flora/audio/prefab candidates are claimed residency-safe without group/key/handle/release/memory proof.
- `AllPackedAssetsAndDependencies` is used for heavy biome, texture, audio, terrain, or HLOD groups without written resident memory budget.
- Direct prefab AudioClip refs are treated as valid streaming ownership.
- Static material/scene reachability is treated as Addressables residency proof.

No-save/no-apply rules:

- Do not create Addressables settings.
- Do not create or rename groups.
- Do not add entries, labels, profiles, schemas, or keys.
- Do not build player content.
- Do not save generated Addressables files.

## Stop Conditions

Stop immediately and write only a concise failure note under `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_06_STOP_<YYYYMMDD_HHMMSS>.md` if any condition occurs:

- CPU rises above 50 percent during readback.
- Unity compile/import is active.
- `dotnet`, `csc`, `Unity.ILPP.Runner`, `ShaderCompiler`, or `PackageManager` becomes active/busy.
- Unity shows a dirty scene warning or save prompt.
- Any material required by the current step is missing/null in Unity.
- Unity Console reports errors after scene load or asset selection.
- Frame Debugger is unavailable for a step that requires pass/draw proof.
- Unity tries to auto-create or mutate Addressables settings.
- Project prompts to upgrade, repair, import, reserialize, or save assets.
- Any artifact would need to be written under `Assets/`.

## Readback Result Status Rules

- Until the future Unity owner produces the artifacts above, status is `PENDING VERIFICATION`.
- Static text/YAML/CSV evidence remains `STATIC_SOURCE` only.
- Screenshots are visual evidence, not runtime performance proof.
- Frame Debugger is render route evidence, not memory/residency proof.
- Addressables settings/group/key evidence is lifecycle setup evidence, not loaded handle, release, or GPU residency proof.
- Runtime readiness still requires Unity Console, Play Mode/profiler/GCMonitor where relevant, Memory Profiler/VRAM evidence, and reviewed screenshots.
- Reviewed screenshots must include `ASSET_OWNER_06_VISUAL_REFERENCE_COMPARISON_<timestamp>.md` for user-visible water, terrain, sky/Aegir, flora, UI, and route VFX contexts.

## Scalability Consequences To Record In Future Readback

- Low/compact: surface sky, Aegir, ocean, shoreline foam, photic terrain, flora silhouettes, oxygen UI, and warning/breath audio must remain readable and premium. Reduce density, mip residency, reflections, bank breadth, and secondary layers smoothly. Do not use flat/dark fallback art.
- Middle: route-owned PBR stacks, clean foam/contact masks, atlas-owned UI icons, MusicDirector/mixer ownership, and Addressables group/key coverage must be present before content breadth increases.
- High: spend spare budget on richer cloud/Aegir detail, stronger water response, longer LOD residency, denser near-field geology/flora, and stronger audio transitions after proof.
- Ultra: visual overkill may add hero Aegir/cloud residency, richer shoreline breakup, stronger material layering, and denser route dressing only after memory/frame proof. It must not change gameplay truth ownership, save identity, DTO layout, or asset owner route.

## Regression Model For Future Unity Owner

- CPU: readback must not add runtime scripts, polling, material wrappers, import loops, or scene searches. Any runtime change requires profiler proof.
- GC: readback itself must make no gameplay path. Future HUD/audio/material/runtime changes require 0 B/frame proof before acceptance.
- Memory/VRAM: source reachability is not residency. Future owner must record texture memory, total reserved memory, loaded handle count, release ledger, and VRAM pressure behavior.
- Cadence: no runtime cadence is accepted from this packet. UI, audio, streaming, and material updates need owner phase/cadence proof if changed later.
- Correctness: every claim needs one fact, one owner, one route, one proof artifact. If owner/route/proof is missing, the route remains blocked.
