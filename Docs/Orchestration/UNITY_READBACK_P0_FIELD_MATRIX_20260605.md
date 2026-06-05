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
