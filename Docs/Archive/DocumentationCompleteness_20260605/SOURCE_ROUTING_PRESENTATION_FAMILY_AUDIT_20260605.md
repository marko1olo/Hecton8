# Source Routing Presentation Family Audit

Date: 2026-06-05
Status: PENDING VERIFICATION
Role: Source Routing Audit Worker I - UI/Visor/Audio/Presentation
Evidence class: STATIC_SOURCE / STATIC_DOC only

## Evidence Boundary

This report used static source listing, static text reads, and exact relative-path text comparison only.

It did not run Unity, importers, Play Mode, dotnet, tests, player builds, profiler, GCMonitor, Memory Profiler, Frame Debugger, RenderGraph Viewer, shader import, scene validation, or asset mutation.

Static source proves source presence only. Static docs prove text presence only. They do not prove compile health, runtime wiring, visual quality, hot-path GC, audio underrun state, GPU cost, RenderGraph correctness, shader variant readiness, save/load, scene binding, platform readiness, or first-20 route readiness.

First-20 route blocker removed by this audit: presentation-family source routing gaps are identified so later controller integration can assign exact owner docs and proof classes for opening HUD, scanner/sonar, warning audio, water/lighting/render features, VFX, PDA, and menu surfaces.

## Mandates And Bibles Read

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`

Stable docs and root bibles read:

- `AGENTS.md` evidence/static-doc rules by targeted search
- `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `UI_MENU_SCREEN_STANDARDS.md`
- `audio.md`
- `sonar.md`
- `rendering.md`
- `lighting.md`
- `vfx.md`
- `presentation.md`

Commands used:

- `rg --files Assets/_Project/Scripts/<scope> -g '*.cs'`
- PowerShell exact path comparison against `SOURCE_SYSTEMS_REALITY_MAP.md` plus `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- PowerShell loose-root family matching under `Assets/_Project/Scripts/*.cs`
- `rg -n "\b(class|struct|interface|enum)\b|ScriptableRendererFeature|RecordRenderGraph|NativeArray|GraphicsBuffer|TMP_Text|SetCharArray" <selected source anchors>`

## Inspected Folder Counts

Exact anchor means the full relative path, for example `Assets/_Project/Scripts/UI/SubtitleManager.cs`, appears in either `SOURCE_SYSTEMS_REALITY_MAP.md` or `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.

| Scope | Scripts | Exact anchors | Missing exact anchors |
|---|---:|---:|---:|
| `Assets/_Project/Scripts/UI` | 147 | 9 | 138 |
| `Assets/_Project/Scripts/Visor` | 46 | 5 | 41 |
| `Assets/_Project/Scripts/Audio` | 53 | 7 | 46 |
| `Assets/_Project/Scripts/PDA` | 8 | 0 | 8 |
| `Assets/_Project/Scripts/VFX` | 35 | 2 | 33 |
| `Assets/_Project/Scripts/Rendering` | 27 | 0 | 27 |
| `Assets/_Project/Scripts/Graphics` | 22 | 7 | 15 |
| `Assets/_Project/Scripts/Lighting` | 13 | 3 | 10 |
| Folder total | 351 | 33 | 318 |

Subfolder distribution:

| Scope | Scripts |
|---|---:|
| `Assets/_Project/Scripts/UI/ROOT` | 113 |
| `Assets/_Project/Scripts/UI/Editor` | 14 |
| `Assets/_Project/Scripts/UI/TerminalOS` | 7 |
| `Assets/_Project/Scripts/UI/Diegetic` | 4 |
| `Assets/_Project/Scripts/UI/Navigation` | 3 |
| `Assets/_Project/Scripts/UI/VR` | 2 |
| `Assets/_Project/Scripts/UI/Localization` | 2 |
| `Assets/_Project/Scripts/UI/Tools` | 1 |
| `Assets/_Project/Scripts/UI/TopographicalSonar` | 1 |
| `Assets/_Project/Scripts/Visor/ROOT` | 44 |
| `Assets/_Project/Scripts/Visor/Editor` | 2 |
| `Assets/_Project/Scripts/Audio/Editor` | 20 |
| `Assets/_Project/Scripts/Audio/ROOT` | 19 |
| `Assets/_Project/Scripts/Audio/Synthesis` | 10 |
| `Assets/_Project/Scripts/Audio/AdaptiveStem` | 1 |
| `Assets/_Project/Scripts/Audio/Echolocation` | 1 |
| `Assets/_Project/Scripts/Audio/Prologue` | 1 |
| `Assets/_Project/Scripts/Audio/Virtualization` | 1 |
| `Assets/_Project/Scripts/PDA/ROOT` | 7 |
| `Assets/_Project/Scripts/PDA/Editor` | 1 |
| `Assets/_Project/Scripts/VFX/ROOT` | 11 |
| `Assets/_Project/Scripts/VFX/JacobianFoam` | 6 |
| `Assets/_Project/Scripts/VFX/Parasites` | 5 |
| `Assets/_Project/Scripts/VFX/Bioluminescence` | 3 |
| `Assets/_Project/Scripts/VFX/Debris` | 3 |
| `Assets/_Project/Scripts/VFX/Editor` | 2 |
| `Assets/_Project/Scripts/VFX/PlasmaBeam` | 2 |
| `Assets/_Project/Scripts/VFX/Materials` | 1 |
| `Assets/_Project/Scripts/VFX/Sonar` | 1 |
| `Assets/_Project/Scripts/VFX/Wakes` | 1 |
| `Assets/_Project/Scripts/Rendering/WaterOptics` | 7 |
| `Assets/_Project/Scripts/Rendering/AbyssalCaustics` | 5 |
| `Assets/_Project/Scripts/Rendering/OceanSinglePass` | 5 |
| `Assets/_Project/Scripts/Rendering/ROOT` | 4 |
| `Assets/_Project/Scripts/Rendering/BilateralDrs` | 3 |
| `Assets/_Project/Scripts/Rendering/Scatter` | 2 |
| `Assets/_Project/Scripts/Rendering/Editor` | 1 |
| `Assets/_Project/Scripts/Graphics/Materials` | 10 |
| `Assets/_Project/Scripts/Graphics/Culling` | 9 |
| `Assets/_Project/Scripts/Graphics/Caustics` | 1 |
| `Assets/_Project/Scripts/Graphics/Scalability` | 1 |
| `Assets/_Project/Scripts/Graphics/VR` | 1 |
| `Assets/_Project/Scripts/Lighting/Editor` | 5 |
| `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling` | 3 |
| `Assets/_Project/Scripts/Lighting/ROOT` | 3 |
| `Assets/_Project/Scripts/Lighting/Shafts` | 2 |

## Loose Root Counts

Loose root scope is `Assets/_Project/Scripts/*.cs`, matched by the requested UI/PDA/Visor/Audio/HUD/Menu/Canvas/Subtitle/Sonar/Scanner/Render/Light/VFX families. Family rows are non-unique because one script can match more than one family. The unique row deduplicates paths.

| Loose-root family | Scripts | Exact anchors | Missing exact anchors |
|---|---:|---:|---:|
| `LooseRoot:UI` | 14 | 1 | 13 |
| `LooseRoot:PDA` | 2 | 0 | 2 |
| `LooseRoot:Visor` | 1 | 0 | 1 |
| `LooseRoot:Audio` | 8 | 0 | 8 |
| `LooseRoot:SonarScanner` | 7 | 1 | 6 |
| `LooseRoot:Rendering` | 5 | 0 | 5 |
| `LooseRoot:Lighting` | 4 | 0 | 4 |
| `LooseRoot:VFX` | 1 | 0 | 1 |
| `LooseRoot:UniquePresentationAdjacent` | 42 | 2 | 40 |

Loose-root exact anchors found:

- `Assets/_Project/Scripts/LocalizationManager.cs`
- `Assets/_Project/Scripts/ScannableTarget.cs`

Loose-root presentation-adjacent scripts without exact anchors include:

- `Assets/_Project/Scripts/AcousticZoneController.cs`
- `Assets/_Project/Scripts/BeaconDeployerTool.cs`
- `Assets/_Project/Scripts/BeaconNetworkSystem.cs`
- `Assets/_Project/Scripts/BeaconRuntime.cs`
- `Assets/_Project/Scripts/CameraJuiceProcessor.cs`
- `Assets/_Project/Scripts/HectonFabricatorUI.cs`
- `Assets/_Project/Scripts/HectonInventoryUI.cs`
- `Assets/_Project/Scripts/HectonScanMarkerSystem.cs`
- `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`
- `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs`
- `Assets/_Project/Scripts/HUDNotification.cs`
- `Assets/_Project/Scripts/HUDQuickBar.cs`
- `Assets/_Project/Scripts/LightDetectionSystem.cs`
- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/MainMenuInputRoutingGuard.cs`
- `Assets/_Project/Scripts/PDAInventoryTab.cs`
- `Assets/_Project/Scripts/PlayerFootstepAudio.cs`
- `Assets/_Project/Scripts/PlayerPDA.cs`
- `Assets/_Project/Scripts/PlayerThrusterAudio.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`
- `Assets/_Project/Scripts/SpatialAudioManager.cs`
- `Assets/_Project/Scripts/UIRuntimeSmokeTester.cs`

## Exact Anchors Found In Shared Routing Docs

`UI` exact anchors:

- `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs`
- `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`
- `Assets/_Project/Scripts/UI/FontStreamingManager.cs`
- `Assets/_Project/Scripts/UI/LoadingScreenController.cs`
- `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs`
- `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`
- `Assets/_Project/Scripts/UI/SettingsManager.cs`
- `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs`

`Visor` exact anchors:

- `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs`
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`

`Audio` exact anchors:

- `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`
- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`
- `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs`
- `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs`

`VFX` exact anchors:

- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- `Assets/_Project/Scripts/VFX/PropwashGpuContracts.cs`

`Graphics` exact anchors:

- `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs`
- `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`
- `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`
- `Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs`

`Lighting` exact anchors:

- `Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs`
- `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`
- `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs`

No exact anchors were found for:

- `Assets/_Project/Scripts/PDA/**/*.cs`
- `Assets/_Project/Scripts/Rendering/**/*.cs`

## Top 20 Missing Exact Anchors By Risk

Owner bible is marked `CANDIDATE` unless the exact source owner is already fully established by the two shared routing docs. These entries are patch candidates, not runtime acceptance.

| # | Missing exact anchor | Risk | CANDIDATE owner bible / route | Proof class needed |
|---:|---|---|---|---|
| 1 | `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | Sonar/visor presentation has static source and root-bible mention, but no exact shared routing anchor. It contains bounded event queues and sonar/acoustic interfaces. | `sonar.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `VISOR_AR_STENCIL_RENDERER.md` | STATIC_SOURCE now; later Unity import, active-sonar HUD capture, UI GC, Frame Debugger/RenderGraph, profiler |
| 2 | `Assets/_Project/Scripts/UI/DiegeticPDAController.cs` | PDA physical panel/controller route is high-risk UI truth presentation. Static source registers `ILateFrameTickable` on UI priority and implements panel interaction. | `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `SHINOBU_348_SCREEN_SPACE_PDA_PROJECTOR_ROUTE_CARD.md` | STATIC_SOURCE now; later Play Mode panel binding, compact readability screenshot, UI GC/profiler |
| 3 | `Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassRuntime.cs` | Ocean presentation runtime owns double graphics buffers and published constant/wake buffers. No exact shared routing anchor despite surface/water visual floor. | `rendering.md`, `water.md`, `presentation.md` | STATIC_SOURCE now; later Frame Debugger/RenderGraph, visual capture, VRAM/frame/profiler, GC |
| 4 | `Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs` | RenderGraph ocean feature consumes `OceanSinglePassRuntime` buffers and writes render passes. Missing anchor blocks exact render proof routing. | `rendering.md`, `water.md` | STATIC_SOURCE now; later RenderGraph Viewer, Frame Debugger, compact/high ocean capture, GPU timing |
| 5 | `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs` | Water optics DTO/telemetry/runtime bridge has graphics buffers and shutdown surface. No exact route for water readability proof. | `rendering.md`, `water.md`, `presentation.md` | STATIC_SOURCE now; later Frame Debugger, visual capture, profiler/GC, black-box/dump proof if faulted |
| 6 | `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs` | RenderGraph dry-volume/stencil restore feature is exact-presentation critical and only short-mentioned, not exact-routed. | `rendering.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `VISOR_AR_STENCIL_RENDERER.md` | STATIC_SOURCE now; later RenderGraph Viewer, Frame Debugger, route readability capture |
| 7 | `Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs` | Depth fog feature has `RecordRenderGraph` and double `GraphicsBuffer` constants. Fog can hide route/asset weakness if proof is missing. | `rendering.md`, `presentation.md`, `lighting.md` | STATIC_SOURCE now; later compact no-black-screen capture, Frame Debugger/RenderGraph, profiler |
| 8 | `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs` | Sonar point-cloud render feature implements late/slow tick plus RenderGraph pass. It crosses sonar, UI, rendering, and performance proof boundaries. | `sonar.md`, `rendering.md`, `ui.md` | STATIC_SOURCE now; later sonar UI capture, Frame Debugger/RenderGraph, GC/profiler |
| 9 | `Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs` | Fluid visual advection feature has `RecordRenderGraph` and hot-swap listener. Missing exact route risks confusing VFX fake with fluid truth. | `rendering.md`, `vfx.md`, `water.md` | STATIC_SOURCE now; later RenderGraph, visual capture, profiler/GPU, truth-boundary review |
| 10 | `Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs` | DRS/upscaler feature controls presentation scalability. No exact routing means quality/VRAM proof can be misassigned. | `rendering.md`, `presentation.md`, `platform.md` | STATIC_SOURCE now; later compact/high DRS capture, Frame Debugger/RenderGraph, profiler/VRAM |
| 11 | `Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs` | Caustics feature is route/beauty-critical and consumes runtime buffers. Static docs name caustics generally, not this exact route. | `rendering.md`, `lighting.md`, `water.md` | STATIC_SOURCE now; later Frame Debugger/RenderGraph, visual capture, GPU timing |
| 12 | `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs` | Foam GPU runtime uses late/cold ticks, double graphics buffers, and `GlobalQualityWeight`. Missing exact VFX route and proof owner. | `vfx.md`, `rendering.md`, `water.md` | STATIC_SOURCE now; later VFX pool/buffer proof, Frame Debugger/GPU profiler, compact/high capture |
| 13 | `Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs` | Parasite swarm GPU runtime has many double graphics buffers and DataVault target reads. Risk: VFX presentation vs ecology/AI truth boundary. | `vfx.md`, `creatures.md`, `ecosystem.md` | STATIC_SOURCE now; later VFX owner/cause proof, GPU/profiler, gameplay-truth boundary review |
| 14 | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` | Carve debris compute renderer uses late/slow ticks and many graphics buffers. Risk: voxel/tool consequence VFX without exact owner route. | `vfx.md`, `voxels.md`, `tools.md`, `rendering.md` | STATIC_SOURCE now; later event owner proof, pool/overflow proof, GPU/profiler, capture |
| 15 | `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs` | Native audio bridge layout/status surface is not exact-routed. Audio release risk if native/DSP boundaries are unproven. | `audio.md`, `sonar.md`, `AUDIO_DSP_PIPELINE.md` | STATIC_SOURCE now; later DSP profiler, underrun telemetry, GC, device capture |
| 16 | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs` | SPSC audio ring buffer has native arrays and telemetry dump structures. Missing exact route blocks audio-thread proof assignment. | `audio.md`, `AUDIO_DSP_PIPELINE.md` | STATIC_SOURCE now; later underrun proof, ring race audit, GC/profiler, shutdown/disposal proof |
| 17 | `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` | Vocal playback runtime uses DataVault views, cold/updatable/slow ticks, and `GlobalQualityWeight`. Root audio bible flags mock/recovery risks. | `audio.md`, `ADAPTIVE_STEM_AUDIO_MIXER.md`, `VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md` | STATIC_SOURCE now; later authored bank/mixer proof, DSP/GC/profiler, subtitle route |
| 18 | `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs` | Voice virtualization jobs own sort/selection math and quality-weighted behavior. Missing exact route risks hidden audio correctness/perf debt. | `audio.md`, `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt` | STATIC_SOURCE now; later profiler/audio-device proof, no-underrun proof, deterministic virtual voice audit |
| 19 | `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs` | PDA marker registry is `ISaveable`, origin-shift listener, and hot-swap listener. No PDA exact anchors exist in the shared routing docs. | `ui.md`, `sonar.md`, `persistence.md` | STATIC_SOURCE now; later save/load marker roundtrip, map/PDA UI capture, GC/profiler |
| 20 | `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs` | Shadow culling runtime uses double graphics buffers and DataVault views. Lighting docs anchor dynamic point light director but not this shadow route. | `lighting.md`, `rendering.md`, `presentation.md` | STATIC_SOURCE now; later Frame Debugger, shadow eligibility proof, GPU/profiler, compact no-black-screen capture |

## Overclaim And Proof-Risk Notes

- The two shared routing docs are honest about `STATIC_SOURCE` and runtime-pending boundaries. No direct runtime-readiness overclaim was found in the inspected routing docs.
- Exact anchor sparsity remains high: 351 inspected folder scripts, 33 exact anchors. Broad folder rows are useful read-order hints, not owner/proof routing for large runtime families.
- `Rendering` has zero exact anchors in the two shared routing docs despite 27 scripts, multiple RenderGraph features, graphics buffer runtimes, water optics, ocean, caustics, DRS, and scatter owners.
- `PDA` has zero exact anchors despite `PDAMarkerRegistry` being saveable and origin-shift aware.
- `Visor/SpectrumSystem.cs`, `UI/DiegeticPDAController.cs`, and `Visor/HectonDryVolumeFeature.cs` have short-path or basename mentions, but not exact shared route anchors. Short mentions are not enough for automated source-owner routing.
- Root bibles now contain live anchor notes for several presentation systems. Those notes are useful but do not replace exact route rows in `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Audio source contains native/DSP bridge and ring-buffer surfaces outside the current exact anchor list. Static text cannot prove audio-thread safety, no underruns, or no GC.
- Lighting docs exact-anchor `DynamicPointLightCullingDirector.cs`, but the sibling contracts/jobs are unanchored. Later patch rows should route the full family, not only the director class.
- VFX GPU runtimes are mostly unanchored. Static source shows graphics buffers and quality-weighted GPU presentation; runtime cost, pool overflow behavior, and visual capture remain unproven.
- Any later report that upgrades these findings above `STATIC_SOURCE` without Unity/player/profiler/Frame Debugger artifacts violates `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`.

## Recommended Patch Rows For Later Controller Integration

Do not patch shared docs from this worker report. These are candidate rows for the controller/integrator.

| Candidate source family | Candidate exact anchors | Suggested shared-doc route | Evidence class | Failure mode / proof artifact class |
|---|---|---|---|---|
| PDA physical controller and PDA markers | `Assets/_Project/Scripts/UI/DiegeticPDAController.cs`; `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs`; `Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs` | `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 8 plus `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `sonar.md`, `persistence.md` | STATIC_SOURCE only | Failure mode: PDA UI/persistence route mistaken for runtime save/map proof. Proof class: Play Mode PDA operation, compact screenshot, save/load marker roundtrip, UI GC/profiler. |
| Visor sonar and scanner presentation | `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`; `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`; `Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs`; `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs` | Echelon 8 plus `sonar.md`, `ui.md`, `rendering.md`, `VISOR_AR_STENCIL_RENDERER.md` | STATIC_SOURCE only | Failure mode: sensor presentation becomes omniscient world truth or unproven render/UI route. Proof class: active/passive sonar capture, confidence/stale-state proof, Frame Debugger, GC/profiler. |
| Ocean and water optics rendering | `Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassRuntime.cs`; `Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs`; `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs`; `Assets/_Project/Scripts/Rendering/WaterOptics/HectonWaterOpticsTelemetryFeature.cs` | Echelon 7/8 plus `rendering.md`, `water.md`, `presentation.md` | STATIC_SOURCE only | Failure mode: surface/photic route visual proof inferred from source. Proof class: RenderGraph/Frame Debugger, compact/high screenshots, VRAM/frame/profiler, no muddy/dark surface capture. |
| Visor and post/render features | `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs`; `Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs`; `Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs`; `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs` | Echelon 8 plus `rendering.md`, `presentation.md`, `lighting.md`, `vfx.md` | STATIC_SOURCE only | Failure mode: fog/post hides weak assets or breaks HUD readability. Proof class: RenderGraph/Frame Debugger, compact no-black-screen capture, UI readability capture, profiler. |
| Audio native/DSP/virtualization | `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs`; `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`; `Assets/_Project/Scripts/Audio/AudioVirtualizationJobs.cs`; `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` | Echelon 8 plus `audio.md`, `sonar.md`, `AUDIO_DSP_PIPELINE.md`, `VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md` | STATIC_SOURCE only | Failure mode: source text mistaken for release-safe audio thread/DSP proof. Proof class: audio profiler/device capture, underrun telemetry, GC/profiler, authored bank/mixer binding proof. |
| GPU VFX consequences | `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs`; `Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs`; `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`; `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs` | Echelon 7/8 plus `vfx.md`, `rendering.md`, `tools.md`, `creatures.md` as source-specific | STATIC_SOURCE only | Failure mode: decorative or unowned VFX, GPU cost unproven, particle overflow hidden. Proof class: owner event proof, pool/overflow proof, compact/high capture, GPU/profiler, memory/VRAM. |
| Lighting and shadow culling | `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingContracts.cs`; `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingJobs.cs`; `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs`; `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingJobs.cs` | Echelon 7/9 plus `lighting.md`, `rendering.md`, `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md` | STATIC_SOURCE only | Failure mode: lighting source treated as no-black-screen, shadow, or GPU upload proof. Proof class: Frame Debugger, shadow manifest, compact capture, GPU/profiler, GC. |
| Loose-root legacy UI/audio/presentation bin | `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`; `Assets/_Project/Scripts/SpatialAudioManager.cs`; `Assets/_Project/Scripts/MainMenuController.cs`; `Assets/_Project/Scripts/SaveSlotUI.cs`; `Assets/_Project/Scripts/BeaconNetworkSystem.cs`; `Assets/_Project/Scripts/HectonScanMarkerSystem.cs` | `SOURCE_SYSTEMS_REALITY_MAP.md` loose-root rule plus matching root bible per file | STATIC_SOURCE only | Failure mode: loose root skipped by folder-only routing or treated as deprecated without source proof. Proof class: exact source read, then UI/audio/save/sonar/profiler evidence by owner. |

## GlobalQualityWeight Consequences

These are documentation/proof consequences, not binary quality switches.

| Range | Consequence for later routing patches |
|---|---|
| Low, near `0.0` | Route docs must identify compact proof artifacts for readable HUD/PDA, warning audio, no-black-screen lighting, water/ocean readability, VFX hard caps, and cheap visual fakes. |
| Middle, around `0.33` to `0.66` | Route docs need exact owner rows for default gameplay presentation, not only broad folder coverage. Proof plans must include GC, frame, VRAM, audio underrun, and route readability. |
| High, around `0.66` to `0.90` | Optional richer render/VFX/audio/UI paths need proof rows without changing truth owners, DTO layout, warning priority, save identity, or sensor certainty. |
| Ultra, near `1.0` | Overkill presentation must be routed as extra fidelity and capture quality only. It cannot become a separate gameplay truth path or a reason to hide compact proof. |

## Regression Model

CPU: No runtime CPU path changed. Static shell reads and text comparisons only.

GC: No game runtime GC path changed. No Unity process was run. This report does not prove 0 B/frame.

Memory: No runtime memory path changed. No assets or source code were mutated.

Cadence: No dispatcher, render, audio, VFX, UI, or tick cadence changed.

Correctness: Documentation correctness risk is reduced by labeling exact anchor gaps and proof classes. Runtime correctness remains unproven.

Hot path impact: None. Report-only.

Failure modes: Later agents may mistake folder anchors, short path mentions, or root-bible live notes for exact route coverage. Mitigation: use exact relative paths in controller patch rows and keep evidence class `STATIC_SOURCE` until runtime proof exists.

Why kept: The report is additive evidence in the requested report file only. It does not patch shared docs or code.

## Final Status

PENDING VERIFICATION.

Presentation-family source routing is incomplete at exact-anchor level. The two shared routing docs are useful for broad read order, but they do not yet route enough exact anchors for UI/PDA/Visor/Audio/VFX/Rendering/Graphics/Lighting source-owner proof.

Verification command required by prompt:

- `git diff --check -- Docs/Reports/DocumentationCompleteness_20260605/SOURCE_ROUTING_PRESENTATION_FAMILY_AUDIT_20260605.md`
