# Source Routing Core Systems Family Audit

Date: 2026-06-05
Worker: Source Routing Audit Worker L - Core/Signals/Save/AI/Modding
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC only

## Evidence Boundary

This report used static source inventory, static document reads, and exact relative-path string comparison only.

It did not run Unity, importers, Play Mode, dotnet build, tests, player builds, profilers, GCMonitor, Memory Profiler, Frame Debugger, shader import, scene validation, asset mutation, or code edits.

Static text proves text/source presence only. It does not prove compile health, runtime behavior, scene wiring, GC, profiler cost, save/load continuity, platform behavior, signal integration, DataVault safety, visual quality, or first-20 route behavior.

## Mandates And Authority Files Read

Mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`

Stable docs and bibles:

- `AGENTS.md` evidence/static-doc rules and global authority rules
- `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `systems.md`
- `performance.md`
- `telemetry.md`
- `persistence.md`
- `ai.md`
- `creatures.md`
- `ecosystem.md`
- `narrative.md`
- `modding.md`
- `networking.md`
- `localization.md`

`Docs/Actual Domains of Project.txt` was checked and was missing. The narrow task domain was inferred from the supplied source scopes.

## Source Scope

Inspected source folders:

- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/Global`
- `Assets/_Project/Scripts/Bootstrap`
- `Assets/_Project/Scripts/SaveSystem`
- `Assets/_Project/Scripts/Optimization`
- `Assets/_Project/Scripts/AI`
- `Assets/_Project/Scripts/Fauna`
- `Assets/_Project/Scripts/Ecosystem`
- `Assets/_Project/Scripts/Narrative`
- `Assets/_Project/Scripts/Networking`
- `Assets/_Project/Scripts/ModdingAPI`
- `Assets/_Project/Scripts/Plugins`
- `Assets/_Project/Scripts/Compatibility`
- `Assets/_Project/Scripts/AtlasSignal`
- `Assets/_Project/Scripts/AudioLog`
- `Assets/_Project/Scripts/Quest`
- `Assets/_Project/Scripts/Progression`
- `Assets/_Project/Scripts/Cartography`
- `Assets/_Project/Scripts/Economy`
- `Assets/_Project/Scripts/Logistics`
- `Assets/_Project/Scripts/Items`

Loose root source included only scripts under `Assets/_Project/Scripts/*.cs` matching these families: `Hecton`, `Save`, `Loc` / `Localization`, `I*`, `Registry`, `Event`, `Signal`, `Telemetry`, `Replay`, `Bootstrap`.

## Counts And Exact Anchor Coverage

Exact anchor means the full relative path string, for example `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, appears in `SOURCE_SYSTEMS_REALITY_MAP.md` and/or `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.

Folder-level rows, shortened paths such as `Core/SystemDispatcher.cs`, and broad echelon rows do not count as exact anchors in this audit.

| Scope | Scripts | Exact in Source Map | Exact in Matrix | Exact in either | Missing exact anchor |
|---|---:|---:|---:|---:|---:|
| `AI` | 39 | 0 | 0 | 0 | 39 |
| `AtlasSignal` | 5 | 0 | 0 | 0 | 5 |
| `AudioLog` | 5 | 0 | 0 | 0 | 5 |
| `Bootstrap` | 9 | 0 | 0 | 0 | 9 |
| `Cartography` | 2 | 0 | 0 | 0 | 2 |
| `Compatibility` | 2 | 0 | 0 | 0 | 2 |
| `Core` | 268 | 3 | 12 | 12 | 256 |
| `Economy` | 9 | 0 | 1 | 1 | 8 |
| `Ecosystem` | 23 | 0 | 0 | 0 | 23 |
| `Fauna` | 30 | 0 | 0 | 0 | 30 |
| `Global` | 12 | 0 | 0 | 0 | 12 |
| `Items` | 1 | 0 | 0 | 0 | 1 |
| `Logistics` | 1 | 0 | 0 | 0 | 1 |
| `ModdingAPI` | 26 | 0 | 8 | 8 | 18 |
| `Narrative` | 14 | 0 | 0 | 0 | 14 |
| `Networking` | 3 | 0 | 3 | 3 | 0 |
| `Optimization` | 30 | 0 | 8 | 8 | 22 |
| `Plugins` | 30 | 3 | 3 | 3 | 27 |
| `Progression` | 4 | 0 | 0 | 0 | 4 |
| `Quest` | 12 | 0 | 0 | 0 | 12 |
| `SaveSystem` | 15 | 0 | 0 | 0 | 15 |

Loose root family counts use duplicate membership. A file may count in more than one family. The unique loose-root union is listed separately.

| Loose root family | Scripts | Exact in Source Map | Exact in Matrix | Exact in either | Missing exact anchor |
|---|---:|---:|---:|---:|---:|
| `Event` | 10 | 0 | 0 | 0 | 10 |
| `Hecton` | 38 | 1 | 3 | 3 | 35 |
| `I*` | 12 | 0 | 0 | 0 | 12 |
| `Loc` / `Localization` | 14 | 1 | 1 | 1 | 13 |
| `Registry` | 8 | 0 | 0 | 0 | 8 |
| `Save` | 22 | 3 | 5 | 5 | 17 |
| `Telemetry` | 1 | 0 | 0 | 0 | 1 |
| `ROOT_UNION_UNIQUE` | 98 | 5 | 9 | 9 | 89 |

Total requested unique source surface:

| Unique inspected scripts | Exact in Source Map | Exact in Matrix | Exact in either | Missing exact anchor |
|---:|---:|---:|---:|---:|
| 638 | 11 | 44 | 44 | 594 |

## Exact Anchors Found In Requested Scope

| Relative path | Source Map | Matrix |
|---|---:|---:|
| `Assets/_Project/Scripts/Core/BatteryChargerLogisticsBridge.cs` | no | yes |
| `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs` | yes | yes |
| `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs` | yes | yes |
| `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | yes | yes |
| `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs` | no | yes |
| `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs` | no | yes |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs` | no | yes |
| `Assets/_Project/Scripts/Core/PlayerInputState.cs` | no | yes |
| `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs` | no | yes |
| `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs` | no | yes |
| `Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs` | no | yes |
| `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs` | no | yes |
| `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs` | no | yes |
| `Assets/_Project/Scripts/HectonSurvivalSystem.cs` | no | yes |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | no | yes |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs` | yes | yes |
| `Assets/_Project/Scripts/LocalizationManager.cs` | yes | yes |
| `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs` | no | yes |
| `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs` | no | yes |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` | no | yes |
| `Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs` | no | yes |
| `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/AssetRecord.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/RenderTexturePool.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/VRAMMonitor.cs` | no | yes |
| `Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs` | no | yes |
| `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs` | yes | yes |
| `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs` | yes | yes |
| `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs` | yes | yes |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs` | no | yes |
| `Assets/_Project/Scripts/SaveManager.cs` | no | yes |
| `Assets/_Project/Scripts/SaveSidecarStorage.cs` | yes | yes |
| `Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs` | yes | yes |
| `Assets/_Project/Scripts/SaveThumbnailSystem.cs` | yes | yes |

## Top 25 Missing Exact Anchors By Risk

Owner bible is marked `CANDIDATE` unless direct ownership was stated by a read source/doc. Proof class is the artifact class required later. This report does not create those proof artifacts.

| # | Missing exact relative path | Static risk | Likely owner bible | Required proof class |
|---:|---|---|---|---|
| 1 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | Bootstrap and initialization spine lacks exact full-path anchor. Shortened source map wording exists, but exact routing is missing. | `systems.md` / `bootstrap.md` CANDIDATE | UNITY_CONSOLE, PLAYMODE boot, profiler/GC, route owner packet |
| 2 | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | Dispatcher phase owner is central to every runtime cadence and SignalBus/DataVault timing rule. | `systems.md` CANDIDATE | STATIC_SOURCE route packet, UNITY_CONSOLE, profiler/GC |
| 3 | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | Global authority surface. Exact anchor missing despite registry doctrine forbidding hot polling and hidden authority growth. | `systems.md` CANDIDATE | STATIC_SOURCE route packet, compile/import, registry access audit |
| 4 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` | Contract/interface surface affects cold DI, execution phases, and public dependency shape. | `systems.md` CANDIDATE | STATIC_SOURCE API audit, compile/import |
| 5 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | Data sovereignty owner. Exact route missing for buffer ownership, relocation, stale handles, and locks. | `systems.md` / `performance.md` CANDIDATE | STATIC_SOURCE route packet, DataVault relocation test, profiler/GC |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | SystemID and memory sentinel surface. Missing exact anchor risks unowned native allocation claims. | `performance.md` / `telemetry.md` CANDIDATE | STATIC_SOURCE layout audit, memory sentinel artifact |
| 7 | `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs` | Listed as high risk by source map addendum, but still lacks exact anchor in the two compared docs. | `data.md` / `performance.md` CANDIDATE | STATIC_SOURCE DTO/layout proof, compile/import |
| 8 | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` | First-party hot broadcast implementation has no exact full-path routing anchor. | `systems.md` / `telemetry.md` CANDIDATE | STATIC_SOURCE lane inventory, compile/import, signal overflow proof |
| 9 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` | Listed high risk. Payload lane remainder can hide duplicate or unmanaged-layout drift. | `systems.md` / `data.md` CANDIDATE | STATIC_SOURCE layout/duplicate scan, compile/import |
| 10 | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs` | Legacy `GlobalSignals` bridge lifecycle needs owner, drain phase, overflow, and telemetry counter. | `systems.md` / `telemetry.md` CANDIDATE | STATIC_SOURCE bridge-lane audit, route card if retained |
| 11 | `Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs` | Black-box telemetry spine lacks exact anchor. Critical because reports cannot rely on log spam. | `telemetry.md` CANDIDATE | STATIC_SOURCE schema audit, dump artifact, profiler/GC |
| 12 | `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` | Persistence bible names this class, but exact path is missing in the two routing docs. Save paging is player-state critical. | `persistence.md` DIRECT STATIC_DOC | save/load roundtrip, WAL/corruption artifact, GC/profiler |
| 13 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | Save/network hash boundary affects corruption and rollback claims. | `persistence.md` / `networking.md` CANDIDATE | STATIC_SOURCE DTO/hash audit, save/load and desync proof |
| 14 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | Entity persistence layout can affect save identity and migration. | `persistence.md` CANDIDATE | STATIC_SOURCE layout audit, migration/roundtrip artifact |
| 15 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | Voxel deltas affect world scars and save identity. | `persistence.md` / `systems.md` CANDIDATE | STATIC_SOURCE layout audit, voxel save/load proof |
| 16 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | Creature cognition route is gameplay/fairness critical and DataVault/job heavy. | `ai.md` / `creatures.md` CANDIDATE | encounter capture, cognition telemetry, profiler/GC |
| 17 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | Multi-lane fauna behavior owner lacks exact routing. Risk: monolithic creature truth and hot registry drift. | `creatures.md` / `ai.md` CANDIDATE | STATIC_SOURCE route packet, Play Mode encounter proof |
| 18 | `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs` | Fauna simulation pool/residency owner lacks exact routing. | `creatures.md` / `ecosystem.md` CANDIDATE | spawn integration, deterministic ordering, profiler/GC |
| 19 | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs` | AI pathing route can violate SDF/navgrid and hot physics restrictions if not explicitly routed. | `ai.md` CANDIDATE | path request artifact, deterministic path proof, profiler/GC |
| 20 | `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs` | Macro ecology truth and biomass cadence need exact owner/proof mapping. | `ecosystem.md` CANDIDATE | biome table, migration cadence artifact, profiler/GC |
| 21 | `Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs` | Narrative POI trigger route can become hidden mission truth or localized string authority if not routed. | `narrative.md` / `localization.md` CANDIDATE | scene evidence capture, quest/save state proof |
| 22 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | Quest DAG DataVault/source route appears in broad docs only; exact path missing. | `narrative.md` CANDIDATE | quest state id audit, save/load, Play Mode route proof |
| 23 | `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs` | Audio log save/playback/evidence channel is route-facing narrative proof, but exact anchor is absent. | `narrative.md` / `localization.md` CANDIDATE | playback/save fragment artifact, subtitle/localization proof |
| 24 | `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs` | Legacy asset manager is dangerous under envelope-only modding law; exact anchor missing. | `modding.md` CANDIDATE | static sandbox validator, runtime playbook if reachable |
| 25 | `Assets/_Project/Scripts/Plugins/Crest/CrestOceanRuntimeAdapter.cs` | Crest adapter companion to anchored ocean kinematics route is unanchored. Third-party bridge law requires exact quarantine route. | `systems.md` / `water.md` CANDIDATE | bridge-route review, Unity import, Frame Debugger/profiler |

## Overclaim And Proof-Risk Notes

- `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` correctly label themselves as static/source-oriented and not runtime proof.
- `SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md` correctly says no unqualified runtime proof claim was found in the stable routing docs it reviewed.
- The main risk is not false runtime wording in the two routing docs. The risk is false confidence from broad family/echelon rows. For this requested scope, 594 of 638 unique inspected scripts lack exact full relative-path anchors.
- `SOURCE_SYSTEMS_REALITY_MAP.md` contains useful family-level Wave 2 rows, but those are not enough for systems where owner, dispatcher phase, SignalBus lane, GlobalSignals bridge status, DataVault buffers, and proof artifact must be named per concrete source owner.
- Several critical files are mentioned by shortened paths or root-bible anchors elsewhere, but fail the exact full-path anchor requirement in this audit. Examples: `Bootstrap/GameBootstrapper.cs`, `Core/SystemDispatcher.cs`, `SaveSystem/H8BinaryWorldPager.cs`, `AudioLog/AudioLogSystem.cs`.
- Modding risk is specific: exact anchors exist for `HectonAPI`, `HectonEventBus`, command dispatcher, loader, runtime state, and world persistence, but missing exact anchors remain for legacy or public-surface-adjacent files such as `ModAssetManager.cs`, `IModResourceProxy.cs`, `ModEventContracts.cs`, `ModLocalizationBridge.cs`, and UI/editor mod tools.
- Plugin bridge risk is specific: exact anchors exist for `OceanKinematicsVaultRuntime.cs`, `MapMagicRuntimeBridge.cs`, and `SteamManager.cs`, but companion Crest/MapMagic contracts, jobs, adapters, debug tools, and node files remain unanchored.

## Recommended Patch Rows For Later Controller Integration

Do not apply these rows from this report. They are controller integration candidates only.

| Target shared doc | Recommended row / patch intent | Candidate source anchors |
|---|---|---|
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add "Core execution spine exact anchors" row. Owner: core runtime; proof: boot/import/profiler/GC. | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`; `Assets/_Project/Scripts/Core/SystemDispatcher.cs`; `Assets/_Project/Scripts/Core/GlobalRegistry.cs`; `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` |
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add "Signal runtime and legacy bridge exact anchors" row. Owner: core signals; proof: lane inventory, duplicate scan, overflow/cadence proof. | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs`; `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Expand Echelon 1 Core/Memory with exact DataVault/memory contract anchors. | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`; `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`; `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`; `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs` |
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add SaveSystem exact anchors beyond root `SaveManager`/`SaveBinaryStorage`. | `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`; `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs`; `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`; `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Add AI exact anchors under Echelon 3 for cognition, sensory, pathing, and ecosystem AI. | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs`; `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`; `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs`; `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Add Fauna exact anchors under Echelon 3 with proof gaps for cognition, spawn, damage, kinematics, and IK. | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`; `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`; `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs`; `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Add Ecosystem exact anchors under Echelon 3 for macro math, migration, nutrient/carrion, and population. | `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`; `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`; `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs`; `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs` |
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add Narrative/Quest/AudioLog exact anchors for evidence routes and save/progression proof classes. | `Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs`; `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`; `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs`; `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Add ModdingAPI "legacy quarantine / envelope-only companion files" exact anchors. | `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`; `Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs`; `Assets/_Project/Scripts/ModdingAPI/ModEventContracts.cs`; `Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs` |
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add Plugins companion bridge exact anchors for Crest/MapMagic files not covered by the three current exact anchors. | `Assets/_Project/Scripts/Plugins/Crest/CrestOceanRuntimeAdapter.cs`; `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsContracts.cs`; `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsJobs.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` |
| `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Add Optimization exact anchors for remaining RT manager and asset lifecycle files. | `Assets/_Project/Scripts/Optimization/CameraRTManager.cs`; `Assets/_Project/Scripts/Optimization/UIRTManager.cs`; `Assets/_Project/Scripts/Optimization/VisorRTManager.cs`; `Assets/_Project/Scripts/Optimization/PostFXRTManager.cs` |
| `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` | Add small-family exact anchor row for AtlasSignal, Progression, Cartography, Economy, Logistics, Items. | `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`; `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs`; `Assets/_Project/Scripts/Cartography/CartographyGraphBaker.cs`; `Assets/_Project/Scripts/Items/ItemData.cs` |

## GlobalQualityWeight Documentation Consequence

These are documentation/proof consequences only. They are not runtime quality switches.

| Planning band | Consequence for later routing patches |
|---|---|
| Low / compact | Exact anchors must at least name owner, phase, fallback, failure mode, and proof artifact class for hot/critical systems. |
| Middle | Family rows should split large scopes (`Core`, `AI`, `Fauna`, `Ecosystem`, `ModdingAPI`, `Plugins`) so agents do not route by broad folder label. |
| High | Optional diagnostic, visual, and telemetry fidelity lanes need separate proof rows, not hidden expansion of gameplay truth. |
| Ultra | Extra proof depth can be added for visual overkill and diagnostics, but must not change authority route, DTO layout, save identity, or mod/network/public API claims. |

## Regression Model

CPU: No runtime CPU path changed. Static PowerShell reads and text comparisons only.

GC: No game runtime GC path changed. No Unity process was run. This report does not prove `0 B/frame`.

Memory: No runtime memory path changed. Shell process memory was transient and not a project runtime artifact.

Cadence: No dispatcher phase, job cadence, SignalBus lane, DataVault buffer, or GlobalQualityWeight behavior changed.

Correctness: The report can be wrong only in static coverage interpretation or path comparison. It cannot prove source correctness. It explicitly marks all runtime claims PENDING VERIFICATION.

## Verification

Required command:

`git diff --check -- Docs/Reports/DocumentationCompleteness_20260605/SOURCE_ROUTING_CORE_SYSTEMS_FAMILY_AUDIT_20260605.md`

Status: PENDING until executed after file creation.

Executed: 2026-06-05. Result: PASS, no whitespace errors reported.
New-file check detail: the file was temporarily marked with `git add --intent-to-add` so the exact command could inspect the untracked report, then the index marker was cleared with `git reset -- <path>`. Git printed an LF-to-CRLF normalization warning only; no whitespace error lines were reported.

## Final Static Finding

The requested source families are not exact-anchor complete. The routing docs are useful for broad read order, but exact full relative-path coverage for this scope is 44 of 638 unique inspected scripts. The highest-risk missing anchors are core execution, global registry, signal lanes, DataVault/memory, SaveSystem, AI/Fauna/Ecosystem, narrative/quest/audio evidence, modding quarantine, and plugin bridge companion files.

Runtime status remains PENDING VERIFICATION.
