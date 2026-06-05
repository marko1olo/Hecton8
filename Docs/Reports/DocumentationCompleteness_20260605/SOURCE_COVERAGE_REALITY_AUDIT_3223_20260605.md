# Source Coverage Reality Audit 3223

Date: 2026-06-05
Status: PENDING VERIFICATION
Worker: Documentation Worker 3223
Evidence class: STATIC_SOURCE / STATIC_DOC

## Evidence Boundary

This audit used static file reads and `rg` source sampling only. It did not run Unity, importers, Play Mode, dotnet, builds, tests, player builds, profilers, GCMonitor, Memory Profiler, shader import, or scene validation.

Static text search proves text/source presence only. It does not prove runtime behavior, compile health, scene wiring, profiler cost, GC, save/load continuity, platform behavior, shader behavior, visual quality, or player route behavior.

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`

Stable docs read:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` headings / proof-language skim only

Commands / source probes used:

- `Get-Content AGENTS.md -TotalCount 220`
- `Get-Content <required stable docs>`
- `Select-String Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md -Pattern headings/proof terms`
- `rg --files Assets/_Project/Scripts -g '*.cs'`
- `rg --files Assets/_Project -g '*.asmdef'`
- PowerShell grouping of `rg --files Assets/_Project/Scripts -g '*.cs'` by first folder under `Assets/_Project/Scripts`
- PowerShell exact/relative path text comparison against `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `rg -n "\b(public|internal|private)?\s*(sealed\s+|static\s+|partial\s+|readonly\s+)*\b(class|struct|interface|enum)\b" <selected anchors>`

## Script Inventory

Current static source count:

| Surface | Count |
|---|---:|
| `Assets/_Project/Scripts/**/*.cs` | 2545 |
| `Assets/_Project/**/*.asmdef` | 171 |

Top-folder script counts:

| Top folder | Scripts |
|---|---:|
| `Editor` | 408 |
| `ROOT` | 341 |
| `World` | 282 |
| `Core` | 268 |
| `Gameplay` | 169 |
| `UI` | 147 |
| `Physics` | 85 |
| `Construction` | 74 |
| `Audio` | 53 |
| `Visor` | 46 |
| `AI` | 39 |
| `Physiology` | 39 |
| `VFX` | 35 |
| `Tools` | 31 |
| `Optimization` | 30 |
| `Plugins` | 30 |
| `Fauna` | 30 |
| `Interaction` | 27 |
| `Rendering` | 27 |
| `Atmosphere` | 26 |
| `ModdingAPI` | 26 |
| `Ecosystem` | 23 |
| `Graphics` | 22 |
| `Power` | 22 |
| `Inventory` | 17 |
| `QA` | 15 |
| `Thermodynamics` | 15 |
| `SaveSystem` | 15 |
| `Narrative` | 14 |
| `Animation` | 14 |
| `Lighting` | 13 |
| `Habitat` | 12 |
| `Quest` | 12 |
| `Global` | 12 |
| `Vehicles` | 11 |
| `Dev` | 11 |
| `Environment` | 9 |
| `Data` | 9 |
| `Bootstrap` | 9 |
| `Meta` | 9 |
| `Economy` | 9 |
| `PDA` | 8 |
| `Input` | 7 |
| `Equipment` | 6 |
| `AudioLog` | 5 |
| `AtlasSignal` | 5 |
| `Scavenging` | 4 |
| `Prologue` | 4 |
| `Player` | 4 |
| `Progression` | 4 |
| `Networking` | 3 |
| `Cartography` | 2 |
| `Compatibility` | 2 |
| `Build` | 2 |
| `BuildTools` | 1 |
| `Logistics` | 1 |
| `Items` | 1 |

Loose root script summary under `Assets/_Project/Scripts/*.cs`:

| Root family | Count |
|---|---:|
| `World*` | 68 |
| profile/data/SO-like names | 54 |
| `Hecton*` | 38 |
| `*SmokeTester` | 26 |
| `Save*` | 21 |
| `*Tool` | 15 |
| localization / `Loc*` | 12 |
| interfaces `I*` | 12 |
| `Cave*` | 11 |
| `Player*` | 10 |
| `Base*` / `Module*` | 8 |
| `Voxel*` | 6 |
| `Tool*` | 5 |
| `Fabricator*` / `Fabrication*` | 5 |
| `Thermal*` | 4 |
| `Crafting*` | 4 |
| `Submarine*` | 4 |
| `Biome*` | 3 |
| `PDA*` | 1 |

Root interpretation: `Assets/_Project/Scripts/*.cs` is still an active mixed-domain source bin. The current docs correctly warn that folder anchors are only routing hints, but 341 loose scripts are too many for folder-only routing when the task needs an owner, dispatcher phase, signal lane, DataVault handle, and proof artifact.

## Coverage Comparison

Literal relative-path anchor coverage in `SOURCE_SYSTEMS_REALITY_MAP.md` plus `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`:

| Metric | Count |
|---|---:|
| Relative path anchors in `SOURCE_SYSTEMS_REALITY_MAP.md` | 285 |
| Relative path anchors in `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | 152 |
| Scripts with a relative path anchor in either doc | 288 |
| Scripts without a relative path anchor in either doc | 2257 |

This is not a claim that 2257 scripts are undocumented. Many are covered by folder/echelon text. It is a finding that exact source-owner routing is sparse for a 2545-script project.

Highest missing relative-path coverage by folder:

| Top folder | Scripts | Relative anchors | Missing relative anchors |
|---|---:|---:|---:|
| `Editor` | 408 | 12 | 396 |
| `ROOT` | 341 | 24 | 317 |
| `Core` | 268 | 15 | 253 |
| `World` | 282 | 29 | 253 |
| `Gameplay` | 169 | 23 | 146 |
| `UI` | 147 | 6 | 141 |
| `Physics` | 85 | 24 | 61 |
| `Construction` | 74 | 22 | 52 |
| `Audio` | 53 | 4 | 49 |
| `Visor` | 46 | 5 | 41 |
| `Physiology` | 39 | 0 | 39 |
| `VFX` | 35 | 4 | 31 |
| `Plugins` | 30 | 0 | 30 |
| `Tools` | 31 | 3 | 28 |
| `Rendering` | 27 | 0 | 27 |
| `AI` | 39 | 14 | 25 |
| `Fauna` | 30 | 9 | 21 |
| `Atmosphere` | 26 | 6 | 20 |
| `Ecosystem` | 23 | 3 | 20 |
| `Optimization` | 30 | 12 | 18 |

### SOURCE_SYSTEMS_REALITY_MAP

Useful coverage:

- Correctly states static source is not runtime proof.
- Correctly records 2545 scripts and 171 asmdefs in the 2026-06-05 addendum.
- Correctly names broad live surfaces: bootstrap, save, world streaming, voxel, scatter, KCC, interaction, inventory, construction, flooding, power, combat, vehicles, physics, fauna, AI, ecosystem, atmosphere, thermodynamics, narrative, UI, optimization, networking, modding, QA, and editor tooling.
- Correctly records a high-risk source shortlist.

Coverage problems:

- It is a concise reality map, not enough to route every active family. It uses system rows, not exact owner rows.
- It does not split the 408-script `Editor` surface into authoring, bakers, importers, validators, smoke runners, and tuner windows.
- It does not split the 341 loose root scripts into owner families with dispatcher phase/proof boundaries.
- It lists several high-risk classes but does not assign each one to a stable owner doc, proof class, and next evidence artifact.
- It under-describes `Physiology`, `Plugins`, `Lighting`, `Rendering`, `Audio`, `Visor`, `UI/Navigation`, `Core/Bridge`, and `Core/Diagnostics`.

### DOMAIN_ARCHITECTURE_COVERAGE_MATRIX

Useful coverage:

- Provides a workable echelon route map.
- Explicitly says direct folders are routing hints, not ownership proof.
- Correctly names proof gaps for major product domains.
- Good first pass for agents deciding which architecture docs to read.

Coverage problems:

- Echelon coverage is too coarse for large folders. `Editor`, `Core`, `World`, `UI`, and loose root all exceed 140 missing exact anchors.
- `Physiology` is only a broad source anchor in Combat Physiology; its 39-script runtime/editor cluster has no exact route row.
- `Plugins` has no exact anchor despite live Crest, MapMagic, and Steam bridge code under first-party script quarantine.
- `Rendering`, `Lighting`, and many `Visor` render features need specific render/proof owners. They cannot be safely routed by `Presentation UX` alone.
- `Audio` has four relative anchors for 53 scripts and lacks exact routing for acoustic propagation, echolocation raymarch, audio virtualization, native rings, and granular synthesis.

## Runtime / Proof Claim Risk

No unqualified runtime proof claim was found in `SOURCE_SYSTEMS_REALITY_MAP.md`, `PROJECT_RUNTIME_TOPOLOGY.md`, `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`, or `QUALITY_GATES.md`. Those docs repeatedly state source-only or proof-gap boundaries.

Risk remains in `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` wording if copied without its boundary language:

- Line 96: Data Monolith scoped validator uses `PASS`; the row scopes it to Python schema/payload proof only.
- Line 138: "Latest reviewed green full-solution CLI pass is stale after later source edits."
- Lines 234, 243, 254: "Latest verified audit" appears in rows scoped to static/CLI report artifacts.
- Line 255: "Closed gap" and "build passed" appear for a scoped borrowed-view classifier build.

Interpretation: those ledger rows cite artifacts and line 148 limits their meaning. The risk is agent misuse, not a discovered unscoped runtime proof claim in the stable routing docs.

## Top 15 Coverage Gaps

| # | Gap | Exact source anchors | Suggested stable-doc owner |
|---:|---|---|---|
| 1 | Editor authoring/baker surface is too large for the current "Editor Build / Platform / SDK Tooling" row. It misses texture, material, validation, smoke, and tuning editors. | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs:20`; `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureImportAndMaterialPipeline.cs:15` | `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 9 plus `TECH_ART_PBR_SURFACE_DOCTRINE.md` / procedural asset docs |
| 2 | Offline world geometry bakers are source-present but not routed as a stable authoring-to-runtime payload family. | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs:38`; `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs:31` | `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` plus `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 2 |
| 3 | Loose root world/cave generation remains under-routed despite 341 root scripts. | `Assets/_Project/Scripts/HectonWorldGenerator.cs:462`; `Assets/_Project/Scripts/WorldGeneratedPrimitiveFactory.cs:6`; `Assets/_Project/Scripts/CaveGraphGenerator.cs:59` | `SOURCE_SYSTEMS_REALITY_MAP.md` root-script family section plus `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` |
| 4 | Physiology has zero relative anchors in the two routing docs despite DataVault-heavy runtime systems. | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:21`; `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:22`; `Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:20` | `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 5 plus survival/physiology route docs |
| 5 | First-party `Plugins` quarantine has no exact routing for Crest, MapMagic, or Steam bridge code. | `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs:15`; `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs:57`; `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs:18` | `SOURCE_SYSTEMS_REALITY_MAP.md` third-party bridge row plus `THIRD_PARTY_POISON.md` / water / terrain docs |
| 6 | UI navigation and instrument surfaces are collapsed into broad presentation language. | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:180`; `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs:14`; `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs:16` | `ZERO_GC_UI_PIPELINE.md`, Presentation UX echelon, sonar/UI docs |
| 7 | Visor/URP render-feature family is larger than the current exact anchors. | `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:23`; `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs:17`; `Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs:21` | `VISOR_AR_STENCIL_RENDERER.md`, rendering/shader docs, Presentation UX echelon |
| 8 | Audio runtime is under-described outside named music/warning/procedural renderer anchors. | `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:12`; `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs:12`; `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:187` | `AUDIO_DSP_PIPELINE.md`, `ADAPTIVE_STEM_AUDIO_MIXER.md`, audio route docs |
| 9 | Core bridge and telemetry/analytics surfaces are not separately routed from generic Core. | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:15`; `Assets/_Project/Scripts/Core/DodReplayRecorder.cs:318`; `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:662` | `PROJECT_RUNTIME_TOPOLOGY.md` Core Source Spine plus telemetry/replay docs |
| 10 | Save support surface extends beyond `SaveManager` and `SaveBinaryStorage`; sidecars, thumbnails, and maintenance need routing. | `Assets/_Project/Scripts/SaveSidecarStorage.cs:10`; `Assets/_Project/Scripts/SaveThumbnailSystem.cs:21`; `Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs:6` | `SAVE_PAGING_PROTOCOL.md` plus Source Systems static data/save row |
| 11 | World anomaly, sargassum, and readability routes are visible in source but not assigned enough proof owners. | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs:189`; `Assets/_Project/Scripts/World/SargassumCutManager.cs:21`; `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs:16` | World/Terrain echelon plus world/readability/visual route docs |
| 12 | Lighting/GI/day-night relay has no exact relative anchors in the two routing docs. | `Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:93`; `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:195`; `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:55` | Atmosphere Celestial / Presentation UX echelons plus lighting/rendering docs |
| 13 | Graphics material response and caustics source is not fully routed to material/shader proof owners. | `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:125`; `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs:176`; `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:11` | `TECH_ART_PBR_SURFACE_DOCTRINE.md`, shader/material docs, rendering docs |
| 14 | QA/headless and documentation-smoke surfaces exist, but `QUALITY_GATES.md` does not map them to current commands/artifact outputs. | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:18`; `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:17`; `Assets/_Project/Scripts/AutomationSmokeTester.cs:10` | `QUALITY_GATES.md` plus `PROJECT_RUNTIME_TOPOLOGY.md` QA row |
| 15 | Settings/localization/subtitle route is split between root and UI without a precise coverage owner. | `Assets/_Project/Scripts/UI/SettingsManager.cs:21`; `Assets/_Project/Scripts/LocalizationManager.cs:57`; `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:105` | `ZERO_GC_UI_PIPELINE.md`, localization/settings docs, Presentation UX echelon |

## GlobalQualityWeight Documentation Consequences

Documentation/proof planning must stay continuous. Named tiers below are planning labels only; they must not become binary pass/fail quality switches.

| GlobalQualityWeight range | Documentation/proof consequence |
|---|---|
| Low, near `0.0` | Stable docs need minimum route facts for every hot system: owner, phase, source anchor, fallback cadence, proof artifact type, and failure mode. Static docs must identify which visual fakes preserve route belief on compact hardware. |
| Middle, around `0.33` to `0.66` | Docs need source-family maps for large folders and loose root scripts so agents can pick the right owner without reading unrelated docs. Proof plans must cover cadence, GC, memory, and correctness for default route moments. |
| High, around `0.66` to `0.90` | Docs need optional fidelity/proof lanes for richer visuals, denser presentation, higher-quality shader/material routes, and deeper telemetry. These must still read as the same route, not a separate architecture. |
| Ultra, near `1.0` | Docs need overkill proof budgets for presentation, visuals, diagnostics, and route captures without changing gameplay truth ownership, DTO layout, save identity, or authority route. Extra proof depth belongs in artifacts, not bloated stable docs. |

## Regression Model For This Audit

CPU: No runtime CPU path changed. Static shell text reads and `rg` scans only.

GC: No game runtime GC path changed. No Unity process was run. Audit text does not prove 0 B/frame.

Memory: No runtime memory path changed. Shell process memory was transient and not a project runtime artifact.

Cadence: No dispatcher cadence, tick lane, job cadence, or quality cadence changed. Documentation finding only.

Correctness: Main risk is false confidence from coarse docs. Mitigation in this report is explicit STATIC_SOURCE / STATIC_DOC labeling, exact source anchors, and PENDING VERIFICATION status.

## Final Status

PENDING VERIFICATION.

The stable architecture docs are sufficient for coarse domain read-order routing. They are not sufficient for exact source-owner routing of the current 2545-script surface without additional source inspection, especially for `Editor`, loose `ROOT`, `Core`, `World`, `UI`, `Physiology`, `Plugins`, `Lighting`, `Audio`, `Visor`, and graphics/material systems.
