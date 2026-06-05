# Domain Architecture Coverage Matrix

Date: 2026-06-02
Status: STATIC_DOC / SOURCE-ORIENTED COVERAGE
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM

Purpose: map domain ranges to active architecture docs.

Use this before changing an assigned domain.

This is not runtime proof.

## Use Rule

1. Identify the assigned domain from the current task owner, prompt, route card, or `Docs/PROJECT_ATLAS.md`.
2. Read `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`.
3. Read `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`.
4. Read `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`.
5. Read `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`.
6. Read the matching echelon below.
7. Read source anchors before editing.
8. If source and doc disagree, patch the doc with evidence.

## Source Anchor Rule

The direct folders below are routing hints, not complete ownership proof. Static source listing on 2026-06-02 shows a large active mixed-domain surface directly under `Assets/_Project/Scripts/*.cs`: save, crafting, fabricator, survival, tether, voxel/fluid/world, localization, tools, HUD/PDA bridges, and smoke/profiler helpers.

Before editing any domain, check both:

- the echelon folder anchors listed below;
- loose root scripts under `Assets/_Project/Scripts` whose class names match the domain.

If root-level source and a domain doc disagree, source wins and the doc must be downgraded to `STATIC_SOURCE` / `PENDING VERIFICATION` until runtime proof exists.

## Wave 2 Family Routing Overlay

Status: STATIC_SOURCE only. These overlays sharpen read-order for large source families from the 2026-06-05 source coverage audit. They do not prove compile, Unity import, Play Mode, profiler, GC, visual quality, save/load, player build, or platform readiness.

| Source family | Enter through | Static exemplar anchors | Evidence class | Failure mode / proof artifact class |
|---|---|---|---|---|
| Editor authoring, bakers, validators, tuners | Echelon 9 plus `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`, `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, `Docs/QUALITY_GATES.md` | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs`; `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs`; `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs` | STATIC_SOURCE only | Failure mode: generic Editor row hides baker/import/validator/tuner ownership. Proof class: tool output, importer report, validation report, CI/player-build artifact. |
| Loose root mixed-domain scripts | `SOURCE_SYSTEMS_REALITY_MAP.md` loose-root rule, then the matching echelon by concrete owner | `Assets/_Project/Scripts/HectonWorldGenerator.cs`; `Assets/_Project/Scripts/SaveSidecarStorage.cs`; `Assets/_Project/Scripts/LocalizationManager.cs`; `Assets/_Project/Scripts/AutomationSmokeTester.cs` | STATIC_SOURCE only | Failure mode: subfolder-only routing skips root owners. Proof class: exact owner source read plus domain-specific static/runtime artifact. |
| Physiology runtime cluster | Echelon 5 plus `survival.md` and `physics.md` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs` | STATIC_SOURCE only | Failure mode: physiology lost inside combat or UI trauma. Proof class: survival/pressure route artifact, layout audit, black-box/profiler/GC artifact. |
| Plugins bridge / quarantine | Echelon 9 plus `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md`; `water.md` and `terrain.md` only through approved bridge routes | `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`; `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs` | STATIC_SOURCE only | Failure mode: bridge code treated as permission for direct third-party runtime usage. Proof class: quarantine audit, bridge route review, package/import/runtime artifact. |
| UI navigation and instruments | Echelon 8 plus `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`, `ui.md`, `sonar.md` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs`; `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`; `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs` | STATIC_SOURCE only | Failure mode: presentation component becomes gameplay truth or allocates in HUD paths. Proof class: UI GC, route capture, profiler/frame artifact. |
| Visor render features | Echelon 8 plus `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md`, `rendering.md`, `shaders.md` | `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs`; `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs`; `Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs` | STATIC_SOURCE only | Failure mode: URP feature source treated as render proof. Proof class: shader import log, Frame Debugger/RenderGraph, capture, VRAM/frame artifact. |
| Audio propagation, echolocation, synthesis | Echelon 8 plus `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`, `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md`, `audio.md` | `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`; `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`; `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | STATIC_SOURCE only | Failure mode: broad audio route hides DSP/thread/native proof. Proof class: audio profiler/device capture, DSP queue audit, GC/profiler artifact. |
| Lighting / GI / light shafts | Echelon 7 plus `lighting.md`, `rendering.md`, `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md` | `Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs`; `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`; `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs` | STATIC_SOURCE only | Failure mode: lighting source treated as baked/probe/visual readiness. Proof class: Frame Debugger, probe/lightmap artifact, visual capture, profiler/frame artifact. |
| Graphics material response and caustics | Echelon 7/8 plus `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`, `rendering.md`, `shaders.md` | `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`; `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`; `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs` | STATIC_SOURCE only | Failure mode: material source treated as shader/import/visual proof. Proof class: material/shader import audit, Frame Debugger, capture, VRAM/frame artifact. |
| Core bridge, diagnostics, replay | Echelon 1 plus `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` Core Source Spine, `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`, `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md` | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs`; `Assets/_Project/Scripts/Core/DodReplayRecorder.cs`; `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs` | STATIC_SOURCE only | Failure mode: diagnostics or bridge code becomes hidden authority/proof inflation. Proof class: replay hash, telemetry export, black-box dump, runtime artifact. |
| Save sidecars, thumbnails, maintenance | Echelon 1 plus `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` and `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` Data And Persistence | `Assets/_Project/Scripts/SaveSidecarStorage.cs`; `Assets/_Project/Scripts/SaveThumbnailSystem.cs`; `Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs` | STATIC_SOURCE only | Failure mode: support files treated as save/load correctness. Proof class: save/load roundtrip, corruption recovery, thumbnail/sidecar artifact. |
| World anomaly, sargassum, readability | Echelon 2 plus `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, `world.md`, `terrain.md` | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`; `Assets/_Project/Scripts/World/SargassumCutManager.cs`; `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs` | STATIC_SOURCE only | Failure mode: anomaly/readability source treated as route readability or visual proof. Proof class: route capture, gameplay readability artifact, profiler/GC/frame artifact. |
| QA and headless harnesses | Echelon 9 plus `Docs/QUALITY_GATES.md` and `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` QA row | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs`; `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs`; `Assets/_Project/Scripts/AutomationSmokeTester.cs` | STATIC_SOURCE only | Failure mode: harness source treated as executed validation. Proof class: fresh headless/QA CSV, black-box artifact, CI log. |
| Settings, localization, subtitles | Echelon 8 plus `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`, `settings.md`, `localization.md`, `ui.md` | `Assets/_Project/Scripts/UI/SettingsManager.cs`; `Assets/_Project/Scripts/LocalizationManager.cs`; `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs` | STATIC_SOURCE only | Failure mode: settings/localization/subtitle source treated as persistence/font/UI-GC proof. Proof class: settings roundtrip, locale/font/subtitle capture, UI GC/profiler artifact. |

## 2026-06-05 Shared Source Routing Patch Overlay

Status: STATIC_SOURCE / STATIC_DOC only - RUNTIME PENDING.

The rows below route the four 2026-06-05 source-routing family audits into grouped exact-anchor families. Folder anchors and echelon membership are not exact source-owner routing. These rows assign owner read-order and proof classes only; runtime/import/build/profiler/platform/Data Monolith/first-20 status remains pending until fresh artifacts exist.

| Source-routing group | Enter through | Static exemplar anchors | Required proof artifact class | Runtime status |
|---|---|---|---|---|
| Core execution / bootstrap / dispatcher / GlobalRegistry / contracts | Echelon 1 plus `systems.md`, `bootstrap.md`, `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`; `Assets/_Project/Scripts/Core/SystemDispatcher.cs`; `Assets/_Project/Scripts/Core/GlobalRegistry.cs`; `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` | Boot/import artifact, registry access audit, dispatcher phase proof, profiler/GC artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| SignalBus / GlobalSignals bridge / payload-layout / telemetry black-box | Echelon 1 plus `systems.md`, `telemetry.md`, `data.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`; `Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs` | Lane inventory, duplicate-name scan, payload layout/offset audit, overflow/cadence artifact, black-box dump artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| GlobalDataVault / H8Memory / memory-contract surfaces | Echelon 1 plus `systems.md`, `performance.md`, `data.md`, `telemetry.md` | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`; `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`; `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`; `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs` | DataVault relocation/stale-handle test, memory sentinel report, DTO/layout audit, profiler/GC artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| SaveSystem / persistence / Merkle / entity and voxel delta / migration | Echelon 1 plus `persistence.md`, `data.md`, `math.md`, `networking.md`, `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` | `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`; `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs`; `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`; `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs`; `Assets/_Project/Scripts/SaveDataMigration_AupV8.cs`; `Assets/_Project/Scripts/SaveDataMigration.cs` | Save/load roundtrip, corruption/WAL artifact, checksum/Merkle report, migration fixture, layout audit, profiler/GC artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| Data Monolith authoring/runtime split | Echelon 1 and Echelon 9 plus `authoring.md`, `data.md`, `quality.md`, `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`, `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md` | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`; `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`; `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs`; `Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs`; `Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs`; `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCorruptionFuzzer.cs`; `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithBatchAudit.cs` | `static_data.h8bin` payload artifact, bake/schema/hash report, binary readback, fuzzer/audit output, import/boot artifact, runtime owner proof. | STATIC_SOURCE / STATIC_DOC only; payload/import/runtime state PENDING VERIFICATION. |
| Tools runtime and authoring parser split | Echelon 4 and Echelon 9 plus `tools.md`, `authoring.md`, `data.md`, `performance.md` | `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`; `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs`; `Assets/_Project/Scripts/Tools/LaserCutterSpecsCsvParser.cs`; `Assets/_Project/Scripts/Tools/EquipmentHardwareSpecsCsvParser.cs` | Tool repro artifact, DataVault handle audit, black-box dump, schema validation, authoring output artifact, runtime-parser absence proof, profiler/GC artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| UI / PDA / Visor / HUD / sonar / scanner / subtitles / menu presentation | Echelon 8 plus `ui.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `UI_MENU_SCREEN_STANDARDS.md`, `sonar.md`, `localization.md`, `settings.md`, `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md` | `Assets/_Project/Scripts/UI/DiegeticPDAController.cs`; `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs`; `Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs`; `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`; `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`; `Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs`; `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs`; `Assets/_Project/Scripts/UI/SubtitleManager.cs`; `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`; `Assets/_Project/Scripts/UI/SettingsManager.cs`; `Assets/_Project/Scripts/MainMenuController.cs` | UI GC/profiler artifact, PDA panel binding, sonar/scanner capture, settings/localization/subtitle capture, save/load marker roundtrip, Frame Debugger/RenderGraph artifact where rendering is touched. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| Audio / VFX / graphics / lighting / rendering / water / third-party bridge companion files | Echelon 7, Echelon 8, and Echelon 9 plus `audio.md`, `vfx.md`, `rendering.md`, `lighting.md`, `water.md`, `presentation.md`, `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` | `Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassRuntime.cs`; `Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs`; `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs`; `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs`; `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`; `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs`; `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingContracts.cs`; `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs`; `Assets/_Project/Scripts/Plugins/Crest/CrestOceanRuntimeAdapter.cs`; `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsContracts.cs`; `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsJobs.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` | Bridge-route review, shader/import artifact, Frame Debugger/RenderGraph artifact, audio profiler/device capture, visual capture, VRAM/frame/profiler artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| World / terrain / streaming / voxel / biome / procedural scatter / wreckage / vegetation | Echelon 2 plus `world.md`, `terrain.md`, `voxels.md`, `streaming.md`, `water.md`, `performance.md`, `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` | `Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs`; `Assets/_Project/Scripts/World/HectonWorldStreamingTypes.cs`; `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`; `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs`; `Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs`; `Assets/_Project/Scripts/World/ScatterHybridRuntimeEntryPoint.cs`; `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs`; `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs`; `Assets/_Project/Scripts/World/VegetationMemorySovereigntyRuntime.cs` | Addressables/residency artifact, deterministic seed proof, SDF/navgrid route run, scatter/backend parity, bake/import artifact, visual capture, save/load artifact where stateful, profiler/GC/VRAM artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| Player / physics / survival / construction / interaction / vehicles / power / thermodynamics / atmosphere | Echelon 4, Echelon 5, Echelon 6, and Echelon 7 plus `player.md`, `physics.md`, `survival.md`, `construction.md`, `tools.md`, `vehicles.md`, `logistics.md`, `atmosphere.md`, `water.md` | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`; `Assets/_Project/Scripts/Player/Movement/ZeroGMovementRuntime.cs`; `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs`; `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs`; `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`; `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs`; `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs`; `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs`; `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs`; `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`; `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs`; `Assets/_Project/Scripts/Environment/Fluids/OceanAdapterVaultRoute.cs` | Controller/device route, force-apply ownership audit, SDF/nonalloc collision proof, airlock/survival route artifact, graph/pipe/power coupling, black-box dump, save/load artifact where stateful, profiler/GC artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |
| AI / Fauna / Ecosystem / Narrative / Quest / AudioLog / Modding / Plugins companion gaps | Echelon 3, Echelon 8, and Echelon 9 plus `ai.md`, `creatures.md`, `ecosystem.md`, `narrative.md`, `localization.md`, `modding.md`, `Docs/Modding/README.md`, `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs`; `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`; `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs`; `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`; `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`; `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`; `Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs`; `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs`; `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`; `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`; `Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs`; `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`; `Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs` | Encounter/path request artifact, deterministic cognition ordering, ecosystem migration/cadence artifact, quest/save route proof, audio-log playback/localization proof, mod envelope validator/runtime playbook proof, plugin quarantine artifact. | STATIC_SOURCE / STATIC_DOC only; PENDING VERIFICATION. |

## Echelon 1: Core And Memory

Domains: `1-10`.

Runtime surface:

- boot
- memory
- save
- Data Monolith
- AUP
- telemetry
- scalability
- platform
- dispatcher

Architecture docs:

- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`
- `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHITECTURE/ARENA_ALLOCATOR_2_0.md`
- `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`

Source anchors:

- `Assets/_Project/Scripts/Bootstrap`
- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/Data/Monolith`
- `Assets/_Project/Scripts/SaveSystem`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/SaveManager.cs`

Default proof gap:

- Compile/import/runtime proof is not implied.
- Native ownership debt remains in the actuality ledger.

## Echelon 2: World And Terrain

Domains: `11-20`.

Runtime surface:

- terrain
- voxel
- scatter
- streaming residency
- HLOD / impostors
- geology
- biomes
- vegetation
- persistent world registry
- flow
- vents
- wreckage
- resource distribution / regrowth

Architecture docs:

- `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md`
- `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`
- `Docs/ARCHITECTURE/TERRAIN_CHUNK_PAGING_SYSTEM_SHINOBU_245.md`
- `Docs/ARCHITECTURE/STATIC_CAVE_SDF_VOLUME_BAKER.md`
- `Docs/ARCHITECTURE/VOXEL_TERRAIN_SEAM_BINDER_SHINOBU_246.md`
- `Docs/ARCHITECTURE/VOXEL_DYNAMIC_NAVGRID_VAULT_ROUTE_1316.md`
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md`
- `Docs/ARCHITECTURE/BIOME_WEIGHT_MAP_BAKER_SHINOBU_243.md`
- `Docs/ARCHITECTURE/PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md`

Source anchors:

- `Assets/_Project/Scripts/World`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs`
- `Assets/_Project/Scripts/World/LODSystemManager.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`
- `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`
- `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/Environment`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/Rendering`
- `Assets/_Project/Scripts/Data`

Default proof gap:

- Static terrain contracts do not prove streamed residency.
- World residency, voxel carve persistence, scatter backend parity, vegetation/wreck/resource visuals, HLOD/impostor output, VRAM/frame hitch, profiler, GC, and Play Mode/player evidence remain pending.

## Echelon 3: Flora Fauna Biota

Domains: `21-30`.

Runtime surface:

- ecosystem
- fauna spatial lookup
- swarm
- predator cognition
- pathing
- procedural IK
- flora
- genetics

Architecture docs:

- `AI_PACING_MODEL.md`
- `AI_POTENTIAL_FIELD_NAVIGATION.md`
- `SHINOBU_302_UTILITY_AI_COGNITION_ROUTE.md`
- `SHINOBU_FLORA_FAUNA_SYMBIOSIS.md`
- `FLORA_PROCEDURAL_SWAY_FIELD.md`
- `MIGRATORY_FLORA_SYSTEM.md`
- `PARASITIC_FAUNA_PARTICLE_SWARMS_SHINOBU_313.md`
- `BIOTA_DENSITY_MAP_BAKER_SHINOBU_308.md`

Source anchors:

- `Assets/_Project/Scripts/AI`
- `Assets/_Project/Scripts/AI/Cognition`
- `Assets/_Project/Scripts/AI/Pathfinding`
- `Assets/_Project/Scripts/AI/Sensory`
- `Assets/_Project/Scripts/AI/Ecosystem`
- `Assets/_Project/Scripts/Fauna`
- `Assets/_Project/Scripts/Ecosystem`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs`
- `Assets/_Project/Scripts/World/VegetationPredatorFearField.cs`
- `Assets/_Project/Scripts/World`
- `Assets/_Project/Scripts/Animation`

Default proof gap:

- Source owners prove fauna simulation/cognition/pathfinding/ecosystem surfaces only.
- Scene spawn integration, gameplay request route, SDF/navgrid wiring, deterministic ordering, swarm visual proof, runtime fault dumps, profiler, GC, and First 20 Minutes impact remain pending.

## Echelon 4: Player Tools Kinematics

Domains: `31-40`.

Runtime surface:

- deterministic input
- KCC
- hydrodynamic KCC
- player kinematics
- hand probes / hand IK
- physical interaction
- physical hand controller
- VR kinematic hand bridge
- VR somatic comfort
- interaction signal queue
- buoyancy
- tether
- tools
- scavenging
- inventory
- crafting
- survival
- auxiliary equipment
- loot magnet
- recycler
- first-hour route
- XR interaction

Architecture docs:

- `KINEMATICS_AUP_INTEGRATION.md`
- `SHINOBU_276_EXOSUIT_6D_KINEMATICS.md`
- `EQUIPMENT_SOA_LAYOUT.md`
- `AUXILIARY_EQUIPMENT_ROUTER_SHINOBU_229.md`
- `SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md`
- `SOA_INVENTORY_QUERY_ENGINE.md`
- `SOA_INVENTORY_ROUTING_NETWORK_SHINOBU_141.md`
- `SHINOBU_317_CRAFTING_FAST_FAIL_ROUTE.md`

Source anchors:

- `Assets/_Project/Scripts/Core/InputDispatcher.cs`
- `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs`
- `Assets/_Project/Scripts/Core/PlayerInputState.cs`
- `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs`
- `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs`
- `Assets/_Project/Scripts/Gameplay`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs`
- `Assets/_Project/Scripts/Physics/KCC`
- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
- `Assets/_Project/Scripts/Interaction`
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`
- `Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs`
- `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs`
- `Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
- `Assets/_Project/Scripts/Interaction/InteractableRegistry.cs`
- `Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs`
- `Assets/_Project/Scripts/Equipment`
- `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs`
- `Assets/_Project/Scripts/Inventory`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs`
- `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/CraftingSystem.cs`
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs`
- `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Scavenging`
- `Assets/_Project/Scripts/Tools`
- `Assets/_Project/Scripts/Visor`

Default proof gap:

- Starting tool truth is not proven.
- Copper acquisition is still a route proof blocker.
- KCC collision correctness, environment provider wiring, controller/device input, physical grab/force ownership, XR hand bridge, somatic comfort, and queued tool-surface route proof remain pending.
- Boot-to-craft-to-save/load, inventory/fabricator UI feedback, loot pickup route, profiler, and GC proof remain pending.

## Echelon 5: Combat Physiology

Domains: `41-50`.

Runtime surface:

- combat
- armor
- status effects
- physiology
- decompression
- gas
- crush depth
- wounds

Architecture docs:

- `X_008_COMBAT_ARMOR_LUT_ROUTE_CARD.md`
- `SHINOBU_318_ARMOR_PENETRATION_LUT_ROUTE_CARD.md`
- `DECOMPRESSION_SICKNESS_SHINOBU_321.md`
- `SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_ROUTE_CARD.md`
- `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_ROUTE_CARD.md`
- `TRAUMA_GLITCH_SYSTEM.md`
- `SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS_ROUTE_CARD.md`

Source anchors:

- `Assets/_Project/Scripts/Gameplay/Combat`
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs`
- `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`
- `Assets/_Project/Scripts/Gameplay/ToxinHazard.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs`
- `Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs`
- `Assets/_Project/Scripts/Physiology`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/UI`
- `Assets/_Project/Scripts/VFX`
- `Assets/_Project/Scripts/Rendering`

Default proof gap:

- Source owners prove combat/armor/status/ballistics/trauma/radiation/toxin/vehicle-damage surfaces only.
- Weapon and hazard scene route, target registration, vehicle damage-to-hull visual route, HUD/VFX/audio feedback, profiler, GC, and player-build proof remain pending.

## Echelon 6: Habitat Vehicles

Domains: `51-60`.

Runtime surface:

- habitat
- construction
- flooding
- fluid pipes
- bulkheads and hatch locks
- power
- logistics
- deconstruction
- submarine
- drones
- scooter

Architecture docs:

- `BASE_MODULE_CATALOG_SHINOBU_216.md`
- `CONSTRUCTION_SOCKET_CSR_SOLVER_SHINOBU_217.md`
- `CONSTRUCTION_BUILDER_HOLOGRAPHY_SHINOBU_228.md`
- `HABITAT_FLUID_INCURSION.md`
- `BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`
- `SHINOBU_336_MODULE_DECONSTRUCTION_RESOURCE_RETURN_ROUTE_CARD.md`
- `SUBMARINE_OS_MANUAL.md`
- `SHINOBU_332_SUBMARINE_GYRO_ROUTE_CARD.md`
- `SHINOBU_333_SUBMARINE_BALLAST_BUOYANCY_ROUTE_CARD.md`
- `SHINOBU_251_SUBMARINE_ADDED_MASS_ROUTE_CARD.md`
- `DRONE_FLEET_PROTOCOL.md`

Source anchors:

- `Assets/_Project/Scripts/ConstructionManager.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/BaseModuleTemplate.cs`
- `Assets/_Project/Scripts/Construction`
- `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs`
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- `Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs`
- `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs`
- `Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs`
- `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs`
- `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/Habitat`
- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs`
- `Assets/_Project/Scripts/Power`
- `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs`
- `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs`
- `Assets/_Project/Scripts/Power/PowerRelayNode.cs`
- `Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs`
- `Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs`
- `Assets/_Project/Scripts/Logistics`
- `Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs`
- `Assets/_Project/Scripts/Core/BatteryChargerLogisticsBridge.cs`
- `Assets/_Project/Scripts/Vehicles`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineBallastBuoyancyContracts.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs`
- `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`
- `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy`
- `Assets/_Project/Scripts/Physics/Cavitation`
- `Assets/_Project/Scripts/Physics/Cable132`
- `Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs`
- `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs`
- `Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs`
- `Assets/_Project/Scripts/UI`

Default proof gap:

- Do not create a second owner for save truth.
- Do not create a second owner for power or physics truth.
- Current source owners prove implementation surface only. Base placement/deconstruction, module save/load, flood containment, fluid pipe rupture/drainage, force application ownership, vehicle controller scene proof, ballast/docking gameplay, tether/cable gameplay, power/brownout, battery charger inventory, drone repair, UI/VR feedback, profiler/GC, and player-build proof remain pending.

## Echelon 7: Atmosphere Celestial

Domains: `61-68`.

Runtime surface:

- celestial
- tides
- weather
- gas
- thermodynamics
- marine snow
- fog
- light shafts
- GI

Architecture docs:

- `Docs/ARCHITECTURE/SHINOBU_345_CELESTIAL_ORBIT_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/ABYSSAL_THERMODYNAMICS_SOLVER.md`
- `Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`
- `Docs/ARCHITECTURE/ABYSSAL_CAUSTICS_SHINOBU_232.md`
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md`
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`

Source anchors:

- `Assets/_Project/Scripts/Atmosphere`
- `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereEngine.cs`
- `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`
- `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/StormPropagation`
- `Assets/_Project/Scripts/Thermodynamics`
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`
- `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs`
- `Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs`
- `Assets/_Project/Scripts/Environment`
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
- `Assets/_Project/Scripts/Lighting`
- `Assets/_Project/Scripts/Rendering`
- `Assets/_Project/Scripts/VFX`
- `Assets/_Project/Scripts/World`

Default proof gap:

- Source owners prove atmosphere/gas/weather/ocean/thermal surfaces only.
- Base room wiring, gas/player survival route, weather/ocean visual proof, reactor/power/atmosphere coupling, deterministic cheat boundaries, continuous quality load-shed, profiler, GC, device, and player-build proof remain pending.

## Echelon 8: Presentation UX

Domains: `69-78`.

Runtime surface:

- UI
- subtitles
- fonts and loading screens
- terminals
- visor
- PDA
- applied lore route
- narrative POIs
- cartography
- scanning
- audio
- warnings
- foveated render / dynamic resolution
- procedural VFX / propwash / marine snow

Architecture docs:

- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`
- `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md`
- `Docs/ARCHITECTURE/SHINOBU_348_SCREEN_SPACE_PDA_PROJECTOR_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/PDA_ENCYCLOPEDIA_STREAMER.md`
- `Docs/ARCHITECTURE/SHINOBU_226_SCANNER_LORE_DATABASE_SYNC.md`
- `Docs/ARCHITECTURE/SHINOBU_349_AUP_NARRATIVE_POI_TRIGGER_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`
- `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md`
- `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`

Source anchors:

- `Assets/_Project/Scripts/UI`
- `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs`
- `Assets/_Project/Scripts/UI/FontStreamingManager.cs`
- `Assets/_Project/Scripts/UI/LoadingScreenController.cs`
- `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs`
- `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`
- `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs`
- `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs`
- `Assets/_Project/Scripts/ScannableTarget.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/PDA`
- `Assets/_Project/Scripts/Visor`
- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
- `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs`
- `Assets/_Project/Scripts/Narrative`
- `Assets/_Project/Scripts/Cartography`
- `Assets/_Project/Scripts/Audio`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`
- `Assets/_Project/Scripts/AudioLog`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- `Assets/_Project/Scripts/VFX/PropwashGpuContracts.cs`
- `Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs`
- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`
- `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`
- `Assets/_Project/Scripts/Interaction`

Default proof gap:

- Presentation consumes snapshots.
- Presentation does not own gameplay truth.
- Hot registry polling is forbidden.
- AppliedLore scene/prefab hash assignments are not proven; current lore audit text reports `scene_bindings=0`.
- UI GC, audio-thread/device, RenderGraph/Frame Debugger, shader import, visual capture, and VRAM/DRS proof remain pending.

## Echelon 9: Meta Integration

Domains: `79-85`.

Runtime surface:

- haptics
- camera
- physics culling
- asset lifecycle/load dispatch, VRAM pressure, RT lifecycle, DRS/culling telemetry
- rollback snapshots, Merkle hashing, prediction, mock jitter
- envelope-only mod API, sandbox, loader, resource proxy, save/world persistence
- QA watchdogs, endurance harness, GC fuzzer, headless simulation/stress runners
- editor build/preflight/platform/SDK validators
- integration
- docs
- QA
- research

Architecture docs:

- `Docs/ARCHITECTURE/SHINOBU_353_HAPTIC_SYNTHESIS_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/SHINOBU_354_PROCEDURAL_CAMERA_SHAKE_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`
- `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md`
- `Docs/ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md`
- `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`

Source anchors:

- `Assets/_Project/Scripts/Input`
- `Assets/_Project/Scripts/Gameplay`
- `Assets/_Project/Scripts/Optimization`
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
- `Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
- `Assets/_Project/Scripts/Optimization/AssetRecord.cs`
- `Assets/_Project/Scripts/Optimization/VRAMMonitor.cs`
- `Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
- `Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs`
- `Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs`
- `Assets/_Project/Scripts/Optimization/RenderTexturePool.cs`
- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`
- `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs`
- `Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs`
- `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs`
- `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs`
- `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs`
- `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs`
- `Assets/_Project/Scripts/QA/QAWatchdogGcAllocationFuzzer1524.cs`
- `Assets/_Project/Scripts/QA/Headless`
- `Assets/_Project/Scripts/Editor/Build`
- `Assets/_Project/Scripts/Editor/Build/XrPlatformReadinessValidator.cs`
- `Assets/_Project/Scripts/Editor/Build/QuestVulkanRenderPipelineConfigurator.cs`
- `Assets/_Project/Scripts/Editor/Build/GraphicsApiMatrixValidator.cs`
- `Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs`
- `Assets/_Project/Scripts/BuildTools`
- `Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs`
- `Assets/_Project/Scripts/Meta`
- `Docs`
- `Tools`

Default proof gap:

- Optimization, rollback networking, modding, QA/headless, and build/platform tooling are source-present.
- Addressables/RT/VRAM/DRS proof, loopback/device/transport proof, mod envelope/load/save/security proof, fresh QA/headless artifacts, player/platform/CI proof, and profiler/GC proof remain pending.

Default proof gap:

- Meta systems may measure, validate, or present.
- They must not silently change runtime authority.

## Missing-Proof Defaults

- A source path proves visibility only.
- A route card proves intent only.
- Generated docs need validator exit `0`.
- First 20 Minutes route proof is pending.
- Platform packages do not prove readiness.
- `GlobalQualityWeight` remains continuous.
- Named tiers are authoring labels only.
