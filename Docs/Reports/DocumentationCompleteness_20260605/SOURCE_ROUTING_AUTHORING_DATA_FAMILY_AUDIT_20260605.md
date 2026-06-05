# Source Routing Authoring Data Family Audit

Date: 2026-06-05
Worker: Source Routing Audit Worker J - Editor/Authoring/Data/Tools
Status: STATIC_SOURCE / STATIC_DOC ONLY - RUNTIME PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC

## Evidence Boundary

This report used static source reads, static documentation reads, and filesystem counts only.

No Unity Editor, import, Play Mode, dotnet build, tests, player build, profiler, GCMonitor, Frame Debugger, Memory Profiler, shader import, asset mutation, prefab mutation, scene mutation, or runtime verification was run.

Static text search proves text/source presence only. It does not prove compile health, runtime behavior, scene wiring, authoring tool execution, bake validity, Data Monolith readiness, 0 B/frame GC, frame time, visual quality, save/load continuity, platform readiness, or player route acceptance.

Mandates and authority files read:

- `AGENTS.md` evidence/static-doc/reporting rules.
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`.
- `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md`.
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`.
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- `authoring.md`.
- `data.md`.
- `3dmodel.md`.
- `PROCEDURAL_ASSET_PIPELINE.md`.
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`.
- `3DMODEL_TEXTURES_MATERIALS.md`.
- `performance.md`.
- `quality.md`.

Commands used:

- `rg -n "STATIC|evidence|Evidence|runtime readiness|Unity Console|profiler|No metrics|REPORTING|technical report|REGRESSION MODEL|SOURCE|documentation|docs|QUALITY_GATES|proof" AGENTS.md`
- `Get-Content -Raw .agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Get-Content -Raw <requested docs/root bibles>`
- PowerShell filesystem counts under the requested source scopes.
- PowerShell exact string comparison of repository-relative `.cs` paths against `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Targeted `Get-Content -TotalCount` reads for representative missing anchors.

## Scope Definition

Inspected source folders:

- `Assets/_Project/Scripts/Editor`
- `Assets/_Project/Scripts/Data`
- `Assets/_Project/Scripts/Tools`
- `Assets/_Project/Scripts/QA`
- `Assets/_Project/Scripts/Build`
- `Assets/_Project/Scripts/BuildTools`
- `Assets/_Project/Scripts/Dev`
- `Assets/_Project/Scripts/Meta`

Inspected loose-root source family:

- `Assets/_Project/Scripts/*.cs` matching `Profile`, `Data`, `SO`, `Procedural`, `Generator`, `Baker`, `Validator`, `Import`, `Export`, `SmokeTester`, `Test`, or `Tool`.

Exact anchor definition:

- A file is exact-anchored only if the full repository-relative path string, for example `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`, appears in `SOURCE_SYSTEMS_REALITY_MAP.md` or `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Short mentions such as `Data/Monolith/H8StaticDataArena.cs`, folder anchors, or family rows are useful routing hints, but they are not counted as exact relative-path anchors in this report.

## Folder Counts And Exact Anchor Coverage

| Scope | Scripts | Exact anchors in `SOURCE_SYSTEMS_REALITY_MAP.md` | Exact anchors in `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Exact anchors in either doc | Missing exact anchors |
|---|---:|---:|---:|---:|---:|
| `Assets/_Project/Scripts/Build` | 2 | 0 | 0 | 0 | 2 |
| `Assets/_Project/Scripts/BuildTools` | 1 | 0 | 1 | 1 | 0 |
| `Assets/_Project/Scripts/Data` | 9 | 0 | 1 | 1 | 8 |
| `Assets/_Project/Scripts/Dev` | 11 | 0 | 0 | 0 | 11 |
| `Assets/_Project/Scripts/Editor` | 408 | 3 | 6 | 7 | 401 |
| `Assets/_Project/Scripts/Meta` | 9 | 0 | 0 | 0 | 9 |
| `Assets/_Project/Scripts/QA` | 15 | 1 | 4 | 4 | 11 |
| `Assets/_Project/Scripts/Tools` | 31 | 0 | 0 | 0 | 31 |

Editor subfamily count:

| Editor subfamily | Scripts |
|---|---:|
| `ROOT_EDITOR` | 290 |
| `Build` | 22 |
| `GeologyForge` | 15 |
| `AITextureControlMapBaker` | 12 |
| `OfflineGeometryBaker` | 11 |
| `DataMonolith` | 10 |
| `GeographySanity` | 9 |
| `HydraulicErosionForge` | 8 |
| `ProceduralGen` | 5 |
| `TextureChannelPacker` | 5 |
| `LegacyStubs` | 4 |
| `ModdingSDK` | 4 |
| Other Editor subfamilies with 1-2 scripts each | 13 |

Exact anchored files in inspected folders:

| Path | Source map | Matrix |
|---|---:|---:|
| `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs` | yes | yes |
| `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs` | yes | yes |
| `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureImportAndMaterialPipeline.cs` | yes | no |
| `Assets/_Project/Scripts/Editor/Build/GraphicsApiMatrixValidator.cs` | no | yes |
| `Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs` | no | yes |
| `Assets/_Project/Scripts/Editor/Build/QuestVulkanRenderPipelineConfigurator.cs` | no | yes |
| `Assets/_Project/Scripts/Editor/Build/XrPlatformReadinessValidator.cs` | no | yes |
| `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs` | no | yes |
| `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs` | no | yes |
| `Assets/_Project/Scripts/QA/QAWatchdogGcAllocationFuzzer1524.cs` | no | yes |
| `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs` | no | yes |
| `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | yes | yes |
| `Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs` | no | yes |

Interpretation:

- `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` provide coarse family routing, but exact owner-path routing is sparse for this audit scope.
- `Editor` is the largest gap: 408 scripts, 7 exact anchors. It contains build validators, data monolith tooling, texture/material bakers, geometry forges, smoke runners, authoring facades, platform scanners, and tuner windows. Treating all of that as one Editor route is not enough for source-owner routing.
- `Tools` has 31 scripts and 0 exact anchors despite DataVault-heavy runtime files, CSV parsers, tool kinematics, upgrade matrix compilation, and verification probes.
- `Data` has 9 scripts and 1 exact anchor. The unanchored set includes Data Monolith runtime arena and DTO/hash/job files.
- `Meta`, `Dev`, and root smoke/profile/data families are not exact-routed enough to support proof claims.

## Loose Root Family Counts

The loose-root family scan found 131 unique `Assets/_Project/Scripts/*.cs` scripts matching the requested family tokens.

Family counts are token-overlap counts. A file can count in more than one family, for example `ToolRuntimeSmokeTester.cs` counts as `Tool`, `SmokeTester`, and `Test`.

| Loose-root family token | Matching scripts | Exact anchors in `SOURCE_SYSTEMS_REALITY_MAP.md` | Exact anchors in `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Exact anchors in either doc | Missing exact anchors |
|---|---:|---:|---:|---:|---:|
| `Data` | 14 | 0 | 0 | 0 | 14 |
| `Generator` | 3 | 2 | 1 | 2 | 1 |
| `Procedural` | 31 | 0 | 1 | 1 | 30 |
| `Profile` | 29 | 0 | 0 | 0 | 29 |
| `SmokeTester` | 26 | 1 | 1 | 1 | 25 |
| `SO` | 13 | 0 | 2 | 2 | 11 |
| `Test` | 26 | 1 | 1 | 1 | 25 |
| `Tool` | 23 | 0 | 0 | 0 | 23 |

Unique loose-root family coverage:

| Unique loose-root family scripts | Exact anchors in `SOURCE_SYSTEMS_REALITY_MAP.md` | Exact anchors in `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Exact anchors in either doc | Missing exact anchors |
|---:|---:|---:|---:|---:|
| 131 | 3 | 5 | 6 | 125 |

Exact anchored loose-root family files:

| Path | Family token hits | Source map | Matrix |
|---|---|---:|---:|
| `Assets/_Project/Scripts/AutomationSmokeTester.cs` | `SmokeTester`, `Test` | yes | yes |
| `Assets/_Project/Scripts/CaveGraphGenerator.cs` | `Generator` | yes | no |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs` | `Generator` | yes | yes |
| `Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs` | `SO` | no | yes |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | `SO` | no | yes |
| `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | `Procedural` | no | yes |

Interpretation:

- Loose root remains an active mixed-domain source bin. It contains data ScriptableObjects, old smoke testers, procedural scatter partials, profiles, tools, generators, save migration records, item/recipe/buildable data, and runtime support objects.
- Folder-only routing will miss active owners. The current matrix already warns about this; exact source routing still needs patch rows later.

## Top 20 Missing Exact Anchors By Risk

Owner bible is marked `CANDIDATE` unless this report directly proves ownership from both source and stable docs. This report does not patch shared docs.

| Risk | Missing exact source anchor | Why it is high risk | Likely owner bible / route owner | Required proof class |
|---:|---|---|---|---|
| 1 | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs` | Bakes authored CSV/JSON and AppliedLore sources into `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; source shows temp/backup write paths and section order. Missing full anchor can make Data Monolith readiness look like generic editor tooling. | CANDIDATE: `authoring.md`, `data.md`, Data Monolith architecture docs | Bake report, schema/hash report, binary readback, import log, boot/runtime owner proof |
| 2 | `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs` | Boot-owned static data arena, DataVault handles, telemetry dump path, platform-specific loading branches. Short mentions exist, but full exact anchor is missing. | CANDIDATE: `data.md`, `performance.md`, bootstrap/data monolith docs | Unity import, boot load artifact, payload checksum, DataVault generation proof, telemetry dump proof |
| 3 | `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs` | DTO/header/section type surface for `.h8bin`; layout drift here can break every consumer even if compiler source exists. | CANDIDATE: `data.md`, `authoring.md` | ABI/layout report, section manifest, checksum/endian proof, Burst/IL2CPP proof if runtime structs cross platform |
| 4 | `Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs` | Job-side reconstruction of creature SoA content from the static data arena; source presence is not runtime or Burst proof. | CANDIDATE: `data.md`, `creatures.md`, `performance.md` | Burst compile/import, layout proof, source data fixture, runtime reconstruction artifact |
| 5 | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCorruptionFuzzer.cs` | Data corruption/fuzzing tool can be misreported as proof if not routed to concrete artifact output. | CANDIDATE: `quality.md`, `authoring.md`, `data.md` | Fresh fuzzer command/run artifact, rejected-case list, checksum/corruption recovery report |
| 6 | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithBatchAudit.cs` | Batch audit tooling touches Data Monolith evidence flow; missing exact anchor weakens controller read-order for future report synthesis. | CANDIDATE: `quality.md`, `authoring.md` | Audit command output, unresolved findings list, evidence-class labels |
| 7 | `Assets/_Project/Scripts/Editor/Build/BuildInfoPreprocess.cs` | Build metadata preprocessing is build-output adjacent. It is missing even though neighboring platform validators are exact-anchored. | CANDIDATE: `release.md`, `platform.md`, `quality.md` | Player build log, build metadata artifact, pre/postprocess output |
| 8 | `Assets/_Project/Scripts/Editor/Build/MachineCodePurityPrebuildScanner.cs` | Matrix mentions it by short path only; full exact anchor missing. This scanner affects platform/build claims and should not be reduced to generic build tooling. | CANDIDATE: `platform.md`, `release.md`, `quality.md` | Scanner output, player-build artifact, unresolved native/machine-code risk list |
| 9 | `Assets/_Project/Scripts/Editor/Build/ThreadAffinityPrebuildScanner.cs` | Thread-affinity scanner can affect platform readiness claims; no full exact anchor. | CANDIDATE: `platform.md`, `performance.md`, `release.md` | Scanner output, offending source list, platform build proof |
| 10 | `Assets/_Project/Scripts/Editor/Build/ShaderPortabilityRiskValidator.cs` | Shader/platform validator impacts rendering/platform claims; exact anchor missing while graphics API validators are anchored. | CANDIDATE: `rendering.md`, `shaders.md`, `platform.md` | Validator output, shader import log, platform render proof |
| 11 | `Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs` | Third-party stripping guard is a contamination/protection boundary. Missing exact route risks false cleanup/build claims. | CANDIDATE: `quality.md`, `release.md`, third-party poison docs | Guard output, package/source contamination report, build artifact |
| 12 | `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs` | DataVault-heavy tool runtime with job handles, SDF probes, DTO handles, signals, quality weight, and black-box dump path. `Tools` has zero exact anchors. | CANDIDATE: `tools.md`, `data.md`, `performance.md` | Unity import, tool repro, DataVault handle audit, profiler/GC, black-box dump |
| 13 | `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs` | Source shows fixed BufferID lanes, telemetry ring, LUT and upgrade DTO compilation. Missing route can hide data/progression/tool authority. | CANDIDATE: `tools.md`, `data.md`, `authoring.md` | DTO layout report, compiler output, telemetry/dump proof, gameplay/tool upgrade repro |
| 14 | `Assets/_Project/Scripts/Tools/LaserCutterSpecsCsvParser.cs` | CSV parser in Tools needs explicit authoring/runtime boundary. Runtime text parsing as gameplay path would violate authoring/performance bibles. | CANDIDATE: `authoring.md`, `tools.md`, `performance.md` | Parser isolation proof, schema report, bake/import output, runtime parser absence proof |
| 15 | `Assets/_Project/Scripts/Tools/EquipmentHardwareSpecsCsvParser.cs` | Equipment CSV route needs schema/version/output owner and player-build exclusion or staged import proof. | CANDIDATE: `authoring.md`, `data.md`, `tools.md` | Schema/hash report, validation report, output artifact, runtime owner proof |
| 16 | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` | Runtime headless stress bot has command-line/env activation, 300-frame black box, H8Memory dump, and result JSON paths. Missing exact anchor risks treating harness presence as executed QA. | CANDIDATE: `quality.md`, `testing.md`, `performance.md` | Fresh headless run artifact, result JSON, dump bin/json, unresolved failure list |
| 17 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | Power-grid stress fuzzer should route to QA evidence and logistics/power proof, not generic QA. | CANDIDATE: `quality.md`, `testing.md`, `logistics.md`, `performance.md` | Fuzzer run artifact, power graph case manifest, profiler/GC if runtime |
| 18 | `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs` | Persists global profile JSON, meta currency, records, upgrades outside slot saves. No exact route in inspected docs. | CANDIDATE: `data.md`, `persistence.md`, `gameplay.md` | Persistence roundtrip, migration/default proof, no-hot-GC route, save/profile ownership proof |
| 19 | `Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs` | Loose-root procedural partial controls scatter state machine sampling/reconcile behavior; only the main partial has an exact anchor. | CANDIDATE: `world.md`, `terrain.md`, `performance.md` | Scatter manifest, deterministic seed proof, route capture, profiler/GC, save/load if stateful |
| 20 | `Assets/_Project/Scripts/SaveDataMigration_AupV8.cs` | Explicit-layout save migration structs for AUP payload prefixes. Missing exact route is risky for save compatibility and platform layout. | CANDIDATE: `data.md`, `persistence.md`, `math.md` | Layout/offset report, migration fixture, checksum proof, load fallback proof |

Other high-risk missing exact anchors not in the top 20:

- `Assets/_Project/Scripts/Build/BuildInfo.cs`
- `Assets/_Project/Scripts/Build/BuildInfoHudPresenter.cs`
- `Assets/_Project/Scripts/Dev/IL2CPPCrashTelemetryDebugMenu.cs`
- `Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs`
- `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs`
- `Assets/_Project/Scripts/Meta/MetaRuntimeInstaller.cs`
- `Assets/_Project/Scripts/BuildableData.cs`
- `Assets/_Project/Scripts/RecipeData.cs`
- `Assets/_Project/Scripts/ItemData.cs`
- `Assets/_Project/Scripts/HectonBiomeMatrixProfile.cs`
- `Assets/_Project/Scripts/CaveBioRootsGenerator.cs`
- `Assets/_Project/Scripts/ToolRuntimeSmokeTester.cs`

## Overclaim And Proof-Risk Notes

- `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` correctly label their source routing as static and runtime-pending. No direct runtime overclaim was found in the two requested routing docs.
- The risk is precision, not dishonesty: many important files are covered only by folder rows, short path mentions, or broad family overlays.
- Exact path absence matters because this repository has large mixed bins: `Editor` has 408 scripts, loose root has 341 total scripts and 131 matching this audit's family filter, and `Tools` has DataVault/job/CSV/runtime code in one folder.
- The Data Monolith compiler and static arena are source-present, but source presence does not prove the active `static_data.h8bin` payload is valid, imported, loaded, or runtime-clean.
- QA/headless source presence does not prove any test ran. Harness code must be paired with fresh result artifacts before claims move above `STATIC_SOURCE`.
- Build/platform validators on disk do not prove player-build readiness. They require command output and player-build artifacts.
- CSV parser and ScriptableObject data source presence is not an accepted authoring bridge unless schema, validation, atomic output, runtime owner, and runtime-parser absence are proved.
- Generated asset/tooling source does not prove generated asset quality. `3dmodel.md`, texture bibles, and `PROCEDURAL_ASSET_PIPELINE.md` require manifests, import settings, LOD/collider/material proof, and render/route captures.
- `GlobalQualityWeight` consequences for this report: no runtime or visual quality behavior changed. Later routing patches must preserve continuous quality semantics by documenting Compact/Middle/High/Ultra proof consequences without turning them into binary quality switches.

## Recommended Patch Rows For Later Controller Integration

Do not apply these rows from this worker report. They are candidate integration rows for a later controller/doc patch.

| Target doc | Candidate row / patch subject | Candidate exact anchors | Owner bible / route | Evidence/proof column text |
|---|---|---|---|---|
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Data Monolith authoring/runtime exact route | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`; `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`; `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs` | `authoring.md`, `data.md` | STATIC_SOURCE only; requires bake report, schema/hash report, binary readback, import/boot proof |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 1 | Static data DTO/hash/job subroute | `Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs`; `Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs` | `data.md`, Data Monolith docs | STATIC_SOURCE only; requires layout/Burst/import/runtime artifact |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Editor DataMonolith audit/fuzzer route | `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithBatchAudit.cs`; `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCorruptionFuzzer.cs` | `quality.md`, `authoring.md` | STATIC_SOURCE only; fuzzer/audit source is not fuzzer/audit execution |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 9 | Build metadata and prebuild scanner route | `Assets/_Project/Scripts/Editor/Build/BuildInfoPreprocess.cs`; `Assets/_Project/Scripts/Editor/Build/MachineCodePurityPrebuildScanner.cs`; `Assets/_Project/Scripts/Editor/Build/ThreadAffinityPrebuildScanner.cs`; `Assets/_Project/Scripts/Editor/Build/ShaderPortabilityRiskValidator.cs`; `Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs` | `release.md`, `platform.md`, `quality.md` | STATIC_SOURCE only; requires scanner output and player-build/CI artifact |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Tools DataVault runtime route | `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs` | `tools.md`, `data.md`, `performance.md` | STATIC_SOURCE only; requires tool repro, DataVault handle audit, profiler/GC |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 4 | Tool CSV/upgrade authoring route | `Assets/_Project/Scripts/Tools/LaserCutterSpecsCsvParser.cs`; `Assets/_Project/Scripts/Tools/EquipmentHardwareSpecsCsvParser.cs`; `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs` | `authoring.md`, `tools.md`, `data.md` | STATIC_SOURCE only; requires schema validation, output artifact, runtime-parser absence proof |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | QA/headless exact harness route | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`; `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs`; `Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs` | `quality.md`, `testing.md`, `performance.md` | STATIC_SOURCE only; harness source does not prove a run; requires result JSON/CSV/dump artifacts |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 9 | Dev smoke/debug route | `Assets/_Project/Scripts/Dev/IL2CPPCrashTelemetryDebugMenu.cs`; `Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs`; `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | `testing.md`, `platform.md`, `quality.md` | STATIC_SOURCE only; dev UI/smoke source cannot be product proof |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Meta/profile persistence route | `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs`; `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs`; `Assets/_Project/Scripts/Meta/MetaRuntimeInstaller.cs` | `data.md`, `persistence.md`, `gameplay.md` | STATIC_SOURCE only; requires profile persistence, ownership, and hot-path proof |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Loose-root ScriptableObject data route | `Assets/_Project/Scripts/BuildableData.cs`; `Assets/_Project/Scripts/RecipeData.cs`; `Assets/_Project/Scripts/ItemData.cs`; `Assets/_Project/Scripts/SuitData.cs` | `authoring.md`, `data.md`, `inventory.md`, `construction.md` | STATIC_SOURCE only; requires SO facade/bake/runtime owner proof, no runtime SO mutation |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 2 | Loose-root procedural scatter partial route | `Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs`; `Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs`; `Assets/_Project/Scripts/WorldProceduralScatterDirectorRuntimeStateContexts.cs` | `world.md`, `terrain.md`, `performance.md` | STATIC_SOURCE only; requires scatter manifest, deterministic seed proof, route capture, profiler/GC |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 2 / Generated asset docs | Loose-root generated cave/biome/profile route | `Assets/_Project/Scripts/CaveBioRootsGenerator.cs`; `Assets/_Project/Scripts/HectonBiomeMatrixProfile.cs`; `Assets/_Project/Scripts/WorldContentProfile.cs` | `3dmodel.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `world.md` | STATIC_SOURCE only; requires generated asset manifest, visual proof, LOD/collider/material proof |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Save migration data route | `Assets/_Project/Scripts/SaveDataMigration_AupV8.cs`; `Assets/_Project/Scripts/SaveDataMigration.cs`; `Assets/_Project/Scripts/SaveMetadata.cs` | `data.md`, `persistence.md`, `math.md` | STATIC_SOURCE only; requires layout report, migration fixture, checksum/load fallback artifact |

## Regression Model

CPU: No runtime CPU path changed. Static shell reads and source counting only.

GC: No game runtime GC path changed. This report does not prove 0 B/frame.

Memory: No game runtime memory path changed. No Unity process or player process was run.

Cadence: No dispatcher phase, tool cadence, build process, QA harness, or authoring pipeline was changed.

Correctness: Main risk is false confidence from broad/family routing. This report mitigates that risk by separating exact anchors from broad hints and marking all runtime claims as pending.

## Final Status

PENDING VERIFICATION.

The requested source scopes are source-present and only partially exact-routed. Current stable docs are adequate for coarse read-order routing, but not for exact authoring/data/tool/QA source-owner completeness. Later doc integration should add targeted exact path rows for Data Monolith, Tools DataVault runtime, tool CSV/upgrade compilation, QA/headless harnesses, build scanners, meta/profile persistence, loose-root SO/data, procedural scatter partials, generated asset helpers, and save migration records.
