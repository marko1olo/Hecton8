# Unity Readback P0 Field Matrix - 2026-06-05

Status: `STATIC_CONTROLLER_MATRIX / PENDING UNITY READBACK`
Evidence class: `STATIC_SYNTHESIS`

No Unity, build, import, Play Mode, profiler, scene, prefab, material, Addressables, project-setting, runtime source, or raw YAML mutation was performed.

## Purpose

When the process gate turns green, Unity owner must collect no-mutation readback before any repair or h8_1475 capture. This matrix consolidates the current P0 fields so the owner does not start another visual A/B loop.

## Process / MCP / Editor Gate

- three CPU samples, 10 seconds apart, all under 50 percent;
- blocker process table: no `Unity.ILPP.Runner`, `UnityShaderCompiler`, `UnityPackageManager`, `AssetImportWorker`, `dotnet`, `csc`, `MSBuild`;
- MCP endpoint/session/tool state;
- editor compiling/importing/domain reload/play/saving state;
- console error/warning table;
- dirty scene/prefab state before and after.

## Player / HUD / Movement

- active tagged/named `Player` objects: hierarchy path, scene, active state, tag, layer, prefab source GUID, scene-local flag, parent, enabled components;
- `BootstrapState.CurrentPlayerObject`: null/stale/path, prefab source, shell vs production prefab;
- enabled player route components: `HectonWorldShellController1428`, `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, `Rigidbody`, `CapsuleCollider`, swim presentation, PDA/survival/save owners;
- dispatcher registrations: input, movement, motor, camera, interaction, HUD, shell; phase/lane/priority/count/object;
- `InputDispatcher.ActiveRuntimeInstance`, registered `IInputService`, `PlayerInputState.MoveDelta`, `LookDelta`, `VerticalDelta`, `ActionsBitmask`, scheme hash;
- walk/surface swim/underwater swim/ascend/descend mode state, immersion ratio, water surface, vertical input, motor acceleration/pose, Rigidbody velocity;
- main camera owner, shell camera write status, prefab camera status, HUD render camera, render textures;
- HUD render mode, `forceScreenSpaceOverlay`, projection/world-space carrier, raycast/interactivity, player refs;
- active interaction prompt class, prompt carrier, look target signal, interact input consumption, PDA/pause suppression.

## Surface / Crest / Terrain / Sky

- `H8_WORLD_CREST_OCEAN_RUNTIME_1428`: active state, layer, transform, sea level, OceanRenderer material asset path/GUID, serialized `_material`, water body culling, extents, min/max scale, LOD count/resolution/downsample, foam/depth/shadow flags, underwater material, foam/normals/caustics slots, `HideAndDontSave`/`H8_TEMP_*` material use;
- terrain route: active terrain objects, material template, splat/control/mask/normal slots, size, pixel error, basemap distance, draw instanced, MapMagic graph/generation state, active tile size/height, terrain zero-height risk;
- shoreline/island renderers: active state, material paths, wet basalt slots, foam bounds, material/renderQueue/ZWrite;
- sky/celestial: `RenderSettings.skybox`, `Mat_HectonSky` texture slots, cloud deck renderers/materials, `HectonCelestialEngine` sky material if present, Aegir object active/material/mesh/component state, Aegir `_MainTex/_DetailTex/_StormTex` and scalar values.

## Historical Surface Comparison Anchors

Use these only for no-mutation readback/A-B comparison. They are not raw revert instructions:

- `857689d2b`: Apr 28 sky/Crest/scene candidate; compare `02_HECTON_WORLD.unity`, `Assets/Crest/Crest/Materials/Ocean.mat`, `Assets/_Project/Art/Materials/Mat_HectonSky.mat`, and `Hecton_AegirHazeOverlay.shader`.
- `06cff4605`, `2442f78e9`, `474759516`: first-week-May MapMagic graph candidates for `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`.
- `fd33b9521`, `7073862dd`: May 1 scene placement candidates.
- `b81b0da81^`: pre-divergence terrain/sky shader comparison point.
- Zeno static anchor audit expands the readback target: `857689d2b` must cover Crest renderer/material GUIDs, sea level/extents/LOD/foam/normals/underwater slots, skybox/Mat_HectonSky slots, Aegir material/mesh/atmosphere, and reflection/lighting state; first-week-May MapMagic anchors must cover erosion/anomaly/splatmap links and active generation state; `474759516` must cover whether HectonSurfacePainter/HectonWaterGrid/OceanKinematicsRuntimeService candidates are active, not run or assign them; `fd33b9521`/`7073862dd` must cover scene placement, sky-follow camera, Crest depth cache, celestial/weather route, and player swim/water transition witness; `b81b0da81^`/`e94c11c4d` must cover pre-divergence sky/Aegir/cloud/celestial, TerrainMaster, OceanRainRippleDecal, and ocean-kinematics ownership.
- Hubble static forensics refines the critical questions:
  - `857689d2b` `Ocean_Crest.prefab`: verify active `OceanRenderer._material` GUID `9def92...`, `_minScale 8`, `_maxScale 256`, `_lodDataResolution 256`, depth/foam/shadow flags, and `OceanDepthCache` state.
  - `857689d2b` `Ocean.mat`: verify active `_Underwater`, `_Foam`, `Foam2.png`, `WaveNormals.png`, caustics, diffuse/subsurface scalars, and whether runtime/temp/1428 material replaced it.
  - `857689d2b` `Mat_HectonSky.mat`: verify cloud/star slots, `_SunElevation`, `_NightBlend`, sky/haze values, and Aegir halo fields.
  - `2442f78e9`/`474759516`: verify biome, hydraulic erosion, splatmap, anomaly, slope, sediment, and water-transition links.
  - `b81b0da81`: verify whether May 10 night/orange sky and packed-control terrain divergence is active.
- Wegener static matrix adds:
  - verify `Ocean.mat`, `Ocean-Underwater.mat`, `MAT_H8_SurfaceCrestOcean_1428`, `MAT_H8SurfaceOceanRead_1428`, `waterSurfaceLevel`, and `waterLevelFallback` together; scene static says `waterSurfaceLevel: 0` while underwater fallback is `14.02`;
  - verify current Crest package route is vendored `Assets/Crest` plus first-party plugin scripts because `Packages/com.waveharmonic.crest/package.json` is missing;
  - verify scene skybox GUID resolves to `Assets/_Project/Art/Materials/Mat_HectonSky.mat`, not newer `MAT_AegirSky_Master`;
  - verify `ACTUAL TERRAIN.asset` mixed old `Hecton8.Core` and current `Hecton8.Plugins` MapMagic entries against current owner scripts under `Assets/_Project/Scripts/Plugins/MapMagic`;
  - resolve `WaterTransitionHandler` through player prefab/script GUID or owner binding because it does not text-hit in `02_HECTON_WORLD.unity`.

Readback must answer:

- whether the current Crest material/renderer route matches or diverges from the Apr 28 candidate;
- whether the serialized MapMagic graph is the dirty diagnostic bypass state or the restored production-intent erosion/anomaly route;
- whether current sky/Aegir/cloud slots match the mandatory bright surface reference route or stale/null slots;
- whether any `H8_TEMP_*`, `editor_only_unsaved`, green haze, water-card, or h8_1914 diagnostic object/material is active.

## Proof Tool Quarantine

- `H8VisualProofCapture1912.cs` is diagnostic rejection tooling only while it mutates Crest/MapMagic/Terrain, creates temp probes/materials, pumps MapMagic generation, or writes `editor_only_unsaved` metadata.
- h8_1475 must use a separate no-mutation harness. It must hard-reject scene save, scene dirty mark, renderer enable/disable, transform mutation, material assignment, MapMagic refresh/generation pumping, temp water cards, temp haze, and raw MCP PNG substitution.
- `ACTUAL TERRAIN.asset` is a dirty production graph state until Unity graph readback/reintegration proves production-intent links. Do not raw-edit YAML.

## Audio / Addressables

- `MusicDirectorConfig_Global.asset`: `_musicMixerGroup`, `_stingerMixerGroup`;
- `PFB_HectonMusicDirectorRoot.prefab`: `MusicVoice_0`, `MusicVoice_1`, `MusicStinger` `OutputAudioMixerGroup`;
- all `AudioSource.outputAudioMixerGroup` on runtime music/player routes;
- `Player.prefab` direct clip refs and owning component names;
- `Underwater Ambient.wav` and `dive_splash.wav` import readback: `loadType`, compression, quality, sample rate, force mono, preload, background load, platform overrides, duration, channels, imported size;
- Addressables settings/groups/schemas/labels/entries/catalog/load mode/owner key/ref count/release ledger/active handle counts.

## VFX / Telemetry

- DataVault audit current result and runtime/editor surface classification;
- Biolum black-box ring and dump scratch handles, owner, capacity 300, disposal state, dump path;
- MarineSnow DataVault rewrite state for mock wake/propwash buffers;
- MarineSnow and PlasmaBeam fault dump route: `NativeFaultDumpWriter.CreateTransientPayload`, `TryWriteAll`, managed file-write allocation path, forced dump artifact availability;
- 300-frame black-box rings for input, movement, HUD/focus, VFX telemetry where applicable.

## Rejection

- any repair before these readbacks;
- any h8_1475 scenic screenshot before active player/HUD/tool proof;
- raw MCP PNG substitution;
- diagnostic h8_1914 probe substitution;
- scene/material/prefab save during readback;
- green haze, slab water, or decorative flora/rocks used to hide base route failure.

Final status: `PENDING UNITY READBACK`.
