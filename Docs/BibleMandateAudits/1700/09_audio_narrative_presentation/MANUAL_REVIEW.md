# Audio / Narrative / Presentation Manual Review

Status: STATIC REVIEW - NO DSP/GPU/PLAYER CAPTURE PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
- `Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs`
- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`
- render/visor feature hotspots from `HOTSPOT_REVIEW.md`
- `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`
- `Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs`
- `Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs`
- `Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs`
- `Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs`
- `Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs`

## What Exists

- Audio, narrative, presentation, cinematics, text, VFX, shaders, and rendering root bibles are routed.
- `NativeAudioFrameRingBuffer` has fixed native/raw bridge buffers and telemetry.
- `HectonBiolumSSGIFeature` uses RenderGraph entry points and cold material creation.
- `CameraJuiceSystem` routes through dispatcher phases and dev-only logging.
- Additional visor volumetric/point-cloud features route presentation through RenderGraph/compute style features, but no capture proof has been run.

## What Is Missing / Not Proven

- No DSP budget, voice budget, mixer snapshot, subtitle/accessibility, or soundscape capture proof was run.
- No RenderGraph/Frame Debugger/GPU profiler proof was run for SSGI, volumetric features, point cloud features, or camera juice stack.
- Narrative evidence-before-text and public capture truth were not runtime-verified in this pass.
- No compact/high visual capture proves volumetric shafts, particulate fog, volumetric light, sonar point cloud, SSGI, and camera juice are readable rather than generic post-process noise.

## Current Classification

- `NativeAudioFrameRingBuffer.cs`: `GREEN_STATIC_RING_BUFFER_SHAPE`, runtime DSP proof pending.
- `HectonBiolumSSGIFeature.cs`: `YELLOW_GPU_COST_PROOF_REQUIRED`.
- `CameraJuiceSystem.cs`: `YELLOW_PROFILER_AND_READABILITY_PROOF_REQUIRED`.
- Visor volumetric/point-cloud features: `YELLOW_RENDERGRAPH_GPU_PROOF_REQUIRED`.

## Required Next Proof

- Audio mixer/DSP capture under alarm, ambience, UI, and creature layers.
- Frame Debugger/RenderGraph/GPU profiler capture for presentation stack.
- Narrative/text proof that player-facing claims are backed by in-game evidence, not marketing copy.
- Compact/high presentation screenshots or captures showing readable abyssal/NASA-punk mood without hiding bad meshes, bad UI, or missing gameplay truth.

## Pass 7 Addendum - Audio Ring Shape

- `NativeAudioFrameRingBuffer.Initialize()` allocates fixed native bridge buffers after resolving DataVault and validates power-of-two capacity. Reinitializing with the same capacity routes to `Clear()`, not resize.
- Static verdict remains `GREEN_STATIC_RING_BUFFER_SHAPE`, but only if initialization is boot/device-change only. DSP and mixer proof are still required before audio acceptance.
## Pass 9 Addendum - Visor Presentation RenderFeature Boundary

- Visor presentation features related to sonar, volumetric shafts, volumetric light, and particulate fog use RenderGraph entry points and cold material recreation paths. This is structurally acceptable only as a renderer-feature lifecycle route, not as gameplay hot-path proof.
- Audio/narrative/presentation acceptance still needs capture-truth labels plus device proof: Frame Debugger/RenderGraph Viewer for the visual pass, GPU profiler for compact/high lanes, and proof that material recreation or shader fallback does not happen during normal presentation cadence.

## Pass 18 Addendum - Audio Callback And Narrative Owner Routes

- `DynamicMusicGranularSynthesizer.OnAudioFilterRead(...)` is a player-runtime managed audio callback that copies DataVault-backed audio into Unity's managed `float[]` output buffer. Static review did not prove GC allocation, but the route still conflicts with the audio mandate's managed callback ban unless it is excluded, transfer-only with waiver/proof, or replaced by native/DSPGraph output.
- `VocalBankPlaybackRuntime.OnAudioFilterRead(...)` is a stronger release blocker because it decodes vocal bank data and writes telemetry/counters inside the managed callback route. This should move to native/DSPGraph/SPSC output or be excluded from release builds.
- `NativeAudioFrameRingBuffer` and `PlayerCriticalProceduralAudioRenderer.RefreshNativeOutputBridge()` remain the preferred static shape: fixed raw bridge buffers, descriptor validation, and native plugin registration. Release proof still requires `HectonAudioKernel` availability, zero underruns, bridge failure counters, and compact-device DSP capture.
- Dynamic music emergency profiles and vocal mock banks are development recovery routes only. Production scenes need authored banks, profiles, mixer bindings, runtime director prefab, object-pool prewarm, and player/listener audio components before gameplay begins.
- `QuestStateManager`, `QuestDagResolverService`, `MetaCampaignService`, `AwaitableDropSequenceDirector`, `AudioLogSystem`, `PDARuntimeInstaller`, `PDAMarkerHUDElement`, and `SoundscapeSystem` mostly read as cold setup, owner-phase, slow-tick, or fault-dump routes. They still require boot-only proof, save/load proof, subtitle/caption proof, black-box dump artifacts, soundscape capture, and no repeated runtime component repair.

Current classification update:

- `DynamicMusicGranularSynthesizer.cs`: `YELLOW_MANAGED_AUDIO_CALLBACK_TRANSFER_BRIDGE_RELEASE_BLOCKED`.
- `VocalBankPlaybackRuntime.cs`: `P0_MANAGED_AUDIO_CALLBACK_DECODE_PATH`.
- `NativeAudioFrameRingBuffer.cs`: `GREEN_STATIC_RING_BUFFER_SHAPE_WITH_NATIVE_PLUGIN_PROOF_REQUIRED`.
- `PlayerCriticalProceduralAudioRenderer.cs`: `GREENISH_NATIVE_BRIDGE_ROUTE_PROOF_REQUIRED`.
- `HectonMusicDirector.cs`: `GREENISH_MUSIC_DIRECTOR_SHAPE_WITH_PREFAB_POOL_PROOF_REQUIRED`.
- `AtmosphericAudioRuntimeInstaller.cs`: `YELLOW_RUNTIME_AUDIO_COMPONENT_REPAIR_PROOF_REQUIRED`.
- Narrative/quest/prologue/audio-log/soundscape routes: `GREENISH_OWNER_OR_COLD_PATH_WITH_PROOF_REQUIRED`.

## Line-Level Addendum - 149 Runtime Suspects

- Added `LINE_LEVEL_CLASSIFICATION.md` for every audio/narrative/PDA/cinematics/public-text runtime suspect line.
- Counts: 89 `LEGAL_EDITOR_OR_DEV_GUARDED`, 34 `LEGAL_COLD_PATH`, 24 `FALSE_POSITIVE`, and 2 registered `RUNTIME_VIOLATION`.
- `MissionMarkerSystem.EnsureRuntimeResources()` is a real production-policy problem: it creates a quest marker `Mesh` and `Material` at runtime from `Awake()`/`OnEnable()`. The flagged `mesh.RecalculateNormals()` line is only one symptom; the method also does runtime vertex/index array setup and `new Material(markerShader)`. This is now tracked by `RB-018`.
- `CarveDebrisComputeRenderer.BuildOctahedronMesh()` remains the cross-routed debris fallback violation under `RB-123`.
- RenderFeature material lines in the audio/presentation scan are false positives for mesh/material mutation: they pass existing material references to draw paths, while lifecycle proof remains under `RB-125`.
- `PDARuntimeInstaller` and `PDAMarkerHUDElement` are cold setup/fail-safe routes, not hot tick scans, but authored player/PDA prefab proof is still required before release claims.
