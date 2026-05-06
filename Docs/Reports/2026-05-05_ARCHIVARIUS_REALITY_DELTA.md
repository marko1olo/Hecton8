# Archivarius Reality Delta

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: docset sync, source inventory, naming sweep, mandate sync, interface health, March-to-May terrain reality delta

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`

## Source Inventory

| Surface | Current count |
|---|---:|
| `Assets/_Project/**/*.cs` | `1206` |
| `Assets/_Project/Scripts/**/*.cs` | `1165` |
| `Assets/_Project/Scripts` line count | `643074` |
| `.agents-skills` mandates | `52` |
| `Docs/**/*.md` | `429` |
| Active `Docs/**/*.md`, excluding archive/deprecated/obsolete | `216` |
| Active non-report docs, excluding archive/deprecated/obsolete and `Docs/Reports` | `162` |
| `Docs/Reports/*.md` | `54` |
| Scripts created on `2026-05-06` by filesystem timestamp | `6` |
| Scripts modified on `2026-05-06` by filesystem timestamp | `142` |
| Git-untracked entries at latest May 6 rescan | `15` |

## SpaceEngine And Planetary Terrain Authority

Primary SpaceEngine authority now lives here:

- `Docs/SPACE_ENGINE_RESEARCH/`
- `Docs/SPACE_ENGINE_RESEARCH/_extracted/`
- `Docs/SPACE_ENGINE_RESEARCH/TERRAIN_AND_NOISE_098.md`
- `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md`
- `Assets/_Project/Scripts/World/SpaceEngine098/`
- `Assets/_Project/Scripts/World/HectonSpaceEngine098MapMagicNodes.cs`
- `Assets/_Project/Scripts/Dev/SpaceEngine098TerrainSmokeTester.cs`
- `Assets/_Project/Scripts/Editor/SpaceEngine098TerrainSmokeTestRunner.cs`

Primary Planetary Sandbox terrain authority now lives here:

- `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md`
- `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfMapMagicNode.cs`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs`
- `Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs`
- `Assets/_Project/Scripts/World/HectonTerrainSplatmapMapMagicNode.cs`
- `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTestRunner.cs`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs`

Burst kernel ownership classification:

- SpaceEngine kernel owner: SpaceEngine terrain integration domain, source `SpaceEngine098TerrainKernels.cs`.
- Sandbox shelf kernel owner: Planetary Sandbox macro shelf domain, source `HectonSandboxAbyssalShelfJobs.cs`.
- Terrain splatmap kernel owner: Planetary Canvas terrain bridge domain, source `WorldProceduralTerrainSplatmapJobs.cs`.
- No explicit per-agent owner ID was found in source. Ownership is domain-classified, not person-classified.

## Mandate Sync

Today's script additions were cross-referenced against Zero-GC and AUP mandates.

Mandate registry structural scan:

- `.agents-skills` files: `52`.
- Empty mandate files: `0`.
- Zero-GC/allocation-related mandate files by name: `4`.
- AUP/coordinate/submarine/voxel/MapMagic-related mandate files by name: `6`.

Scan result for created-today scripts:

| Pattern | Count | Classification |
|---|---:|---|
| `using System.Linq` | `0` | No LINQ import in created-today scripts |
| `.Where(`, `.Select(`, `.ToList(` | `0` | No basic LINQ operator hit in created-today scripts |
| `transform.position` | `4` | Review items; no >50m distance calculation was proven |
| `Vector3.Distance` | `0` | No hit |
| `math.distance` | `0` | No hit |
| `string.Format` or string interpolation | `7` | Smoke logs, source-token guards, or cold object naming sampled |
| `.Complete(` | `25` | Editor smoke, standalone smoke, MapMagic/generator cold paths, or cold audio init sampled |
| `.Run(` | `12` | Smoke runner names/calls and source-token guards sampled |

Review hits:

- `Assets/_Project/Scripts/Gameplay/CelestialCataclysmSystem.cs`: `ResolvePlayerPosition()` reads player `transform.position` or fallback `transform.position`; no distance calculation was present in the scanned lines.
- `Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs`: raises one structural stress event at `transform.position`; event origin use, not a distance calculation.
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`: sets generated brine pool object transform from computed runtime center; presentation placement, not distance calculation.
- `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs`: schedules a Burst clear and immediately completes; source comment and call-site sample classify it as cold configuration/reset path.
- `Assets/_Project/Scripts/Audio/PlayerCriticalMetallicGrainBank.cs`: schedules a Burst build and immediately completes; source comment and call-site sample classify it as init-only grain-bank bake.
- `Assets/_Project/Scripts/World/HectonTerrainSplatmapMapMagicNode.cs` and `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs`: `.Complete()` hits are MapMagic generation/smoke paths, not dispatcher hot path by source scan.

Critical alerts:

- CRITICAL: Naming discipline is not compliant. Active full path scan found `575` non-ASCII path entries outside archive/deprecated/obsolete folders.
- CRITICAL: Old documentation claim that `Assets/_Project` had `0` Cyrillic/non-ASCII paths is stale against the current full path scan.
- CRITICAL: `GlobalRegistryContracts.cs` has one ghost direct interface: `IRegistryEventListener`.

No critical AUP distance violation was proven in created-today scripts by this scan.
No hot-path LINQ or hot `Tick`/`Update` string allocation was proven in created-today scripts by this scan.
This is source evidence only, not profiler or runtime proof.

## Interface Health

`GlobalRegistryContracts.cs` direct public interface count: `34`.

Interfaces with direct implementors by source-line scan: `33`.

Ghost interface:

| Interface | Direct implementors | Action |
|---|---:|---|
| `IRegistryEventListener` | `0` | Delete if obsolete, or add a real implementor before treating it as a live contract |

Representative implemented contracts:

- `IAudioService`: `SpatialAudioManager`, `GameBootstrapper.NoOpAudioService`
- `IInputService`: `InputDispatcher`, `GlobalRegistry.NoOpInputService`
- `IWorldGenService`: `WorldProceduralScatterDirector`, `WorldGenRegistrySmokeTester.SmokeWorldGenService`
- `IUIService`: `SuitHUDV4CanvasOverlay`
- `IPhysicsService`: `PhysicsApplySystem`
- `IDebrisService`: `DebrisManager`
- `IThermodynamicsService`: `AbyssalThermalManager`
- `IWorldSeedProvider`: `HectonWorldGenerator`

## Naming Sweep

Active ledger: `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md`.

Key facts:

- Non-ASCII path entries under `Assets/_Project` and `Docs`: `637`.
- Non-ASCII path entries excluding archive/deprecated/obsolete folders: `575`.
- Non-ASCII path entries under `Assets/_Project`: `570`.
- Non-ASCII path entries under `Docs`: `67`.
- Non-ASCII content files in active scan scope: `648`.
- `Assets/MapMagic/Map_Graph/New Gen/USE IT.asset` exists and is the legacy hand-authored terrain graph anchor; it is ASCII but semantically non-canonical.
- No path with the literal Russian phrase equivalent to "USE IT ASSET" was found; the concrete graph file is `USE IT.asset`.

## Reality Delta: March To May

March/legacy terrain reality:

- Terrain authority was concentrated in a hand-authored MapMagic graph surface.
- The visible anchor is `Assets/MapMagic/Map_Graph/New Gen/USE IT.asset`.
- Naming did not communicate production ownership, terrain domain, or procedural generation responsibility.
- SpaceEngine terrain research, AUP macro shelf descent, and the 108-biome volumetric matrix were not the active doc/code spine.

May terrain reality:

- SpaceEngine 0.9.8 terrain research has an active corpus in `Docs/SPACE_ENGINE_RESEARCH/`.
- SpaceEngine-derived terrain math has a first-party Burst kernel assembly under `Assets/_Project/Scripts/World/SpaceEngine098/`.
- Planetary Sandbox terrain has AUP-space macro shelf logic with a 15-20 km descent profile documented in the May 5 sandbox surgery log.
- The 108 Biome Matrix is the live conceptual terrain/biome authority through `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` and `HectonBiomeMatrixCatalog`.
- Terrain splatmap generation now has a Burst `IJobParallelFor` path in `WorldProceduralTerrainSplatmapJobs.cs`.
- Current authority has shifted from "edit a graph named USE IT" to source-backed procedural terrain contracts, Burst jobs, smoke logs, and AUP-space generation rules.

## Pre-Edit Untracked Inventory

These were present before this Archivarius documentation edit pass:

- `.codexbuild/macro_shelf_editor_obj/`
- `.codexbuild/omega_v2_bin/`
- `.codexbuild/omega_v2_obj/`
- `.codexbuild/omega-v2/`
- `.codexbuild/persistence_ux_bin/`
- `.codexbuild/persistence_ux_obj/`
- `.codexbuild/persistence_ux2_obj/`
- `Assets/_Project/Art/Shaders/Hecton_PhotophobiaField.compute`
- `Assets/_Project/Art/Shaders/Hecton_PhotophobiaField.compute.meta`
- `Assets/_Project/Plugins.meta`
- `Assets/_Project/Plugins/`
- `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs.meta`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs.meta`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTestRunner.cs`
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTestRunner.cs.meta`
- `Assets/_Project/Scripts/World/HectonAnomalyResourceBinding.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyResourceBinding.cs.meta`
- `Assets/_Project/Scripts/World/HectonTerrainSplatmapMapMagicNode.cs`
- `Assets/_Project/Scripts/World/HectonTerrainSplatmapMapMagicNode.cs.meta`
- `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs`
- `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs.meta`
- `Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs.meta`
- `CodexArtifacts/2026-05-05_ACOUSTIC_POLISH_DIFF.patch`
- `CodexArtifacts/2026-05-05_ASTRODYNAMICS_SPACEENGINE_DIFF.patch`
- `CodexArtifacts/2026-05-05_EROSION_OMEGA_V2_CURRENT_DIFF.patch`
- `CodexArtifacts/2026-05-05_OMEGA_V2_CRUCIBLE_DIFF.patch`
- `CodexArtifacts/2026-05-05_OMEGA_V2_CRUCIBLE_REPEAT_DIFF.patch`
- `CodexArtifacts/2026-05-05_OMEGA_V2_CRUCIBLE_REPEAT_VERIFICATION.json`
- `CodexArtifacts/2026-05-05_OMEGA_V2_CRUCIBLE_VERIFICATION.json`
- `CodexArtifacts/2026-05-05_SOMATIC_MOVEMENT_GIT_DIFF.patch`
- `CodexArtifacts/2026-05-05_TACTILE_FORGE_DIEGETIC_DIFF.patch`
- `CodexArtifacts/csc-core-ecology-polish-2026-05-05.log`
- `CodexArtifacts/csc-core-ecology-polish-repeat-2026-05-05.log`
- `CodexArtifacts/csc-core-omega-v2-2026-05-05.log`
- `CodexArtifacts/csc-core-omega-v2-with-bufferjobs-2026-05-05.log`
- `CodexArtifacts/csc-core-planetary-canvas-2026-05-05.json`
- `CodexArtifacts/csc-core-planetary-canvas-artifact-2026-05-05.json`
- `CodexArtifacts/csc-editor-newfiles-planetary-canvas-2026-05-05.json`
- `CodexArtifacts/csc-editor-planetary-canvas-2026-05-05.json`
- `CodexArtifacts/csc-editor-planetary-canvas-artifact-2026-05-05.json`
- `CodexArtifacts/csc-newfiles-planetary-canvas-2026-05-05.json`
- `CodexArtifacts/csc-targeted-planetary-canvas-2026-05-05.json`
- `CodexArtifacts/csc-world-contracts-omega-v2-2026-05-05.log`
- `CodexArtifacts/dotnet-base-ambience-build.log`
- `CodexArtifacts/dotnet-base-ambience-restore.log`
- `CodexArtifacts/dotnet-crucible-repeat-build.log`
- `CodexArtifacts/dotnet-crucible-v2-build.log`
- `CodexArtifacts/ecology-polish-2026-05-05.diff`
- `CodexArtifacts/ecology-polish-csc/`
- `CodexArtifacts/ecology-polish-csc-repeat/`
- `CodexArtifacts/ecology-polish-repeat-2026-05-05.diff`
- `CodexArtifacts/ecology-polish-repeat-verification-2026-05-05.json`
- `CodexArtifacts/Hecton8.Core.planetary-canvas.dll`
- `CodexArtifacts/Hecton8.Core.planetary-canvas.ref.dll`
- `CodexArtifacts/Hecton8.Core.planetary-canvas-v2.dll`
- `CodexArtifacts/Hecton8.Core.planetary-canvas-v2.ref.dll`
- `CodexArtifacts/hecton8_somatic_movement_2026-05-05.diff`
- `CodexArtifacts/hecton8_somatic_movement_verification_2026-05-05.json`
- `CodexArtifacts/hecton8-core-refs-only.rsp`
- `CodexArtifacts/hecton8-editor-refs-only.rsp`
- `CodexArtifacts/macro-shelf-editor-crucible-build.log`
- `CodexArtifacts/narrative-crucible-verification.json`
- `CodexArtifacts/omega_v2_build_rerun.log`
- `CodexArtifacts/omega-v2-core-build.log`
- `CodexArtifacts/omega-v2-crucible-audit-2026-05-05.diff`
- `CodexArtifacts/omega-v2-crucible-diff.patch`
- `CodexArtifacts/omega-v2-crucible-diff-2026-05-05.patch`
- `CodexArtifacts/omega-v2-crucible-verification-2026-05-05.json`
- `CodexArtifacts/omega-v2-csc/`
- `CodexArtifacts/omega-v2-dotnet-build.log`
- `CodexArtifacts/omega-v2-dotnet-build-final.log`
- `CodexArtifacts/persistence-ux-inventory-delta.mmf`
- `CodexArtifacts/persistence-ux-last-commit.patch`
- `CodexArtifacts/persistence-ux-source-smoke.json`
- `CodexArtifacts/planetary-canvas-complete-diff-2026-05-05.patch`
- `CodexArtifacts/planetary-canvas-editor-targeted.dll`
- `CodexArtifacts/planetary-canvas-graph-integration-2026-05-05.json`
- `CodexArtifacts/planetary-canvas-newfiles-targeted.dll`
- `CodexArtifacts/planetary-canvas-smoke-2026-05-05.json`
- `CodexArtifacts/tactile-forge-build-4.log`
- `CodexArtifacts/tactile-forge-final.diff`
- `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md`

## Verification Boundary

Executed:

- `rg --files` inventory for atlas/classification targets.
- PowerShell C# count, docs count, mandate count, and script timestamp inventory.
- `rg`/PowerShell SpaceEngine, Sandbox, Planetary Canvas, and `USE IT.asset` localization.
- `rg --pcre2` non-ASCII path/content sweep.
- Created-today script scan for Zero-GC/AUP/barrier/string patterns.
- `GlobalRegistryContracts.cs` interface extraction plus source-line implementor scan.

Not executed:

- Unity Play Mode.
- Player build.
- Profiler/GCMonitor.
- Asset rename dependency walk.

Requested `ARCHIVE SYNCHRONIZED` is blocked by `AGENTS.md` status discipline.

STATUS: PENDING VERIFICATION
