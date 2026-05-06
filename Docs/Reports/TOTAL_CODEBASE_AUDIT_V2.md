# TOTAL CODEBASE AUDIT V2

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: `Assets/_Project/Scripts/`, `.agents-skills/`, `Assets/_Project/Scripts/Hecton8.Core.asmdef`

This is a static compliance audit.
It is not play-mode proof and not profiler proof.
No runtime gameplay code was edited.

## 2026-05-04 Current-State Note

This `2026-04-30` audit is historical static inventory. Do not cite its `Unauthorized Unity loop methods = 10`, `.Complete()` count, C# file count, or violation tables as current. The latest foundation guard source gate is `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`, regenerated at `2026-05-04 23:33:55`, and currently reports unauthorized Unity loop methods `0`, `.Run(` sites `0`, `.Complete(` text hits `5`, `UnsafeUtility.MemCpy outside guard` `0`, and runtime Find API review hits `8`.

## Mandate Ingestion

`.agents-skills/` was scanned and read as source input.

| Metric | Value |
|---|---:|
| Mandate files | 52 |
| Mandate lines | 24,133 |
| `[FORBID]` markers | 225 |
| Primary mandates applied | `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`, `ARCH_Global_Registry_ServiceLocator_DI_Init` |

## Codebase Metrics

| Metric | Value |
|---|---:|
| C# files scanned | 1,014 |
| C# LOC scanned | 533,316 |
| `.Complete()` hits | 141 |
| `.IsCompleted` hits | 120 |
| `new Material(...)` hits | 55 |
| Direct hot-path `new Material(...)` hits | 0 |
| `MaterialPropertyBlock` hits | 216 |
| `GraphicsBuffer` hits | 402 |
| `BatchRendererGroup` hits | 93 |
| `DrawMeshInstanced*` hits | 12 |
| IJob/IJobParallelFor-like structs | 127 |
| Fully explicit Burst jobs | 7 |
| Burst explicit-attribute violations | 120 |
| Unauthorized Unity loop methods | 10 historical in this 2026-04-30 scan; current May 4 guard reports 0 |
| Direct hot-path string/GC findings | 4 |
| Core asmdef ACL violations | CRITICAL |
| SPSC critical violations found | 0 |
| SPSC review findings | 2 |

## Top 3 Worst Violations

1. `Hecton8.Core.asmdef` is not pure core. It directly references `UnityEngine.UI`, `Unity.TextMeshPro`, URP runtime assemblies, GPUInstancer, Den.Tools, MapMagic, Crest, WaveHarmonic Crest, and VolumetricLightBeam. This is a critical Anti-Corruption Layer violation.
2. Burst compliance is structurally weak: 127 job structs were found, only 7 explicitly declare `[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Five job structs have no `[BurstCompile]` attribute at all.
3. Unity loop bypass existed in this 2026-04-30 scan: 10 unauthorized `LateUpdate()` methods outside `SystemDispatcher` and `GameBootstrapper`. Current May 4 guard reports unauthorized Unity loop methods `0`.

## Top 5 Most Dangerous Files

Scoring model: LOC gravity + `.Complete()` pressure + non-compliant job structs + `new Material` hits + `GlobalRegistry` coupling + native surface density.

| Rank | File | Score | LOC | `.Complete()` | Bad Burst jobs | `GlobalRegistry.` | Native hits | Reason |
|---:|---|---:|---:|---:|---:|---:|---:|---|
| 1 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 202.3 | 15,398 | 12 | 14 | 10 | 448 | Platform monolith with terrain/vegetation/jobs/native ownership. |
| 2 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 90.3 | 4,817 | 1 | 13 | 0 | 262 | Voxel mesh/job pipeline with many non-explicit Burst jobs. |
| 3 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 85.1 | 2,127 | 8 | 4 | 3 | 63 | Generation owner with direct completion pressure. |
| 4 | `Assets/_Project/Scripts/SubmarineFluidDynamics.cs` | 81.3 | 3,997 | 5 | 4 | 12 | 108 | Core fluid simulation with jobs and registry coupling. |
| 5 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 76.5 | 1,725 | 1 | 0 | 45 | 8 | Bootstrap authority with high registry coupling. Exempt from loop rule, not exempt from architectural gravity. |

## 1. UPDATE BYPASS AUDIT

Rule used: Unity loops outside dispatcher/bootstrap must migrate to the tick system unless explicitly documented as an exception.

Unauthorized loop count in this 2026-04-30 scan: 10. Current May 4 guard count: `0`.

| File:Line | Method | Status |
|---|---|---|
| `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:336` | `LateUpdate` | MIGRATE or document hard renderer exception. |
| `Assets/_Project/Scripts/Editor/GCSentinel.cs:43` | `LateUpdate` | Editor-only exception candidate, still violates prompt filter. |
| `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:390` | `LateUpdate` | MIGRATE to dispatcher late-frame equivalent. |
| `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs:189` | `LateUpdate` | Interaction service should not bypass dispatcher cadence. |
| `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs:183` | `LateUpdate` | Camera/celestial exception candidate, needs explicit comment or dispatcher path. |
| `Assets/_Project/Scripts/TetherManager.cs:268` | `LateUpdate` | MIGRATE to tick/late-frame owner. |
| `Assets/_Project/Scripts/SkySystemFollowCamera.cs:74` | `LateUpdate` | Camera follow exception candidate, needs explicit comment or dispatcher path. |
| `Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs:140` | `LateUpdate` | UI loop bypass. |
| `Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs:93` | `LateUpdate` | UI loop bypass. |
| `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:1064` | `LateUpdate` | High-risk UI monolith bypass. |

Mandatory conclusion:
- The codebase is not loop-clean.
- `EquipmentInteractionHandler`, `SuitHUDV4CanvasOverlay`, and `GlobalPhysicsStateManager` are the worst loop bypasses because they are system-level owners, not disposable presentation helpers.

## 2. VRAM AND TEXTURE INSTANCING CHECK

Findings:
- `new Material(...)` total: 55.
- Direct hot-path `new Material(...)` inside `Update`, `LateUpdate`, `FixedUpdate`, `Tick`, `FixedTick`, `SlowTick`, `LateFrameTick`, or `ToolTick`: 0.
- Rendering support exists: 216 `MaterialPropertyBlock` hits, 402 `GraphicsBuffer` hits, 93 `BatchRendererGroup` hits.

Important limitation:
- This audit proves no direct lexical hot-path `new Material(...)` hit.
- It does not prove that every material instance has correct lifetime/disposal.
- It does not prove VRAM residency at runtime. That requires Memory Profiler or runtime texture budget telemetry.

Procedural geometry files without an obvious same-file MPB/GPU-buffer/render-indirect marker: 21.
These are review candidates, not automatic defects, because some are editor-only builders or mesh baking tools.

| Candidate file |
|---|
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs` |
| `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` |
| `Assets/_Project/Scripts/WorldGenerativeGeologyMeshBuilder.cs` |
| `Assets/_Project/Scripts/WorldGenerativeGeologyService.cs` |
| `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs` |
| `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` |
| `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs` |
| `Assets/_Project/Scripts/Editor/HectonFBXPostprocessor.cs` |
| `Assets/_Project/Scripts/Editor/SargassumGenerator.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralCoralMeshBuilder.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralFloraBakedStarterGenerator.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralGeologyFinalAuthoring.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralInteriorColonyFinalAuthoring.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs` |
| `Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs` |
| `Assets/_Project/Scripts/UI/LabelSwapScheduler.cs` |
| `Assets/_Project/Scripts/World/HectonProceduralVegetationStripBuilder.cs` |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` |
| `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` |

## 3. BURST COMPATIBILITY VERIFICATION

Strict rule used:
Every IJob structure should explicitly declare:

```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
```

Statistics:

| Metric | Count |
|---|---:|
| Job structs found | 127 |
| Fully compliant | 7 |
| Missing `[BurstCompile]` entirely | 5 |
| Has Burst but missing `FloatMode.Fast` | 34 |
| Has Burst but missing `FloatPrecision.Standard` | 115 |
| Total non-compliant | 120 |

Five job structs missing `[BurstCompile]` entirely:

| File:Line | Struct |
|---|---|
| `Assets/_Project/Scripts/HectonFluidEngine.cs:1934` | `BuoyancyJob` |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2584` | `VoxelMeshBakeJob` |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs:475` | `HectonPhysicsBakeJob` |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:388` | `IndexedBlockDecompressJob` |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:146` | `PublishNodeStatesJob` |

Worst offender files by non-compliant job count:

| File | Count |
|---|---:|
| `HectonMapMagicVegetationBridge.cs` | 14 |
| `HectonVoxelEngine.cs` | 13 |
| `VoxelDynamicNavGridRuntime.cs` | 6 |
| `Power/LogisticsNetworkGraph.cs` | 5 |
| `HectonWorldGenerator.cs` | 4 |
| `PlayerInventory.cs` | 4 |
| `SaveBinaryStorage.cs` | 4 |
| `SubmarineFluidDynamics.cs` | 4 |

## 4. STRING AND GC HOT-PATH PURGE

Scan method:
- Direct lexical scan inside methods named `Update`, `LateUpdate`, `FixedUpdate`, `Tick`, `FixedTick`, `SlowTick`, `LateFrameTick`, or `ToolTick`.
- Patterns: `.ToString(`, string interpolation, `.Where(`, `.Select(`, `.Any(`, `.FirstOrDefault(`, `.ToList(`.
- This does not include transitive helper calls.

Direct findings: 4.

| File:Line | Method | Kind | Code |
|---|---|---|---|
| `Assets/_Project/Scripts/HectonNarrativeDirector.cs:222` | `SlowTick` | Interpolation | `Debug.Log($"[Narrative] New Depth Tier Reached: {currentDepthTier} (Depth: {depth:F1}m)");` |
| `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:837` | `SlowTick` | Interpolation | `$"[WorldScatterRuntime] first-slow-tick bootstrapReady={BootstrapState.IsGameReady} defer={ShouldDeferUntilBootstrapReady()} invalidation={_debugLastScatterInvalidationReason}",` |
| `Assets/_Project/Scripts/Editor/GCSentinel.cs:56` | `LateUpdate` | Interpolation | `$"[GCSentinel] GEN0 GC SPIKE DETECTED | Collections/60f={gen0Delta} | ManagedHeapMB={currentManagedHeapBytes / 1048576f:0.00} | HeapDeltaKB={managedHeapDeltaBytes / 1024f:0.0}");` |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:1529` | `Tick` | Interpolation | `DisableComputeDispatch($"Compute dispatch failure on '{boidCompute.name}'. {exception.Message}");` |

Mandatory conclusion:
- Direct hot-path LINQ was not found by this pass.
- Direct hot-path string interpolation still exists.
- `SargassumMicroFaunaBoids.cs:1529` is the highest runtime risk because it is a `Tick` path and also reads `boidCompute.name`.

## 5. ASMDEF COUPLING ANALYSIS

Target file:
`Assets/_Project/Scripts/Hecton8.Core.asmdef`

Critical finding:
`Hecton8.Core` directly references UI, rendering, and third-party packages.

Offending references:

| Reference | Violation type |
|---|---|
| `Unity.TextMeshPro` | UI dependency in Core |
| `UnityEngine.UI` | Explicit UI dependency in Core |
| `Unity.RenderPipelines.Core.Runtime` | Rendering pipeline dependency in Core |
| `Unity.RenderPipelines.Universal.Runtime` | URP dependency in Core |
| `GPUInstancer` | Third-party dependency in Core |
| `Den.Tools` | Third-party dependency in Core |
| `MapMagic` | Third-party dependency in Core |
| `Crest` | Third-party dependency in Core |
| `WaveHarmonic.Crest` | Third-party dependency in Core |
| `WaveHarmonic.Crest.Shared` | Third-party dependency in Core |
| `VolumetricLightBeam` | Third-party dependency in Core |

Verdict:
- CRITICAL ACL VIOLATION.
- Core is not an anti-corruption boundary.
- Any future asmdef cleanup must be staged because this file likely became a dependency sink.

## 6. SPSC QUEUE INTEGRITY

Primary DSP/SPSC file:
`Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`

Findings:
- `AudioFrameSpscRingBuffer` stores shared indices in `NativeArray<int>`.
- `ReadSharedIndex()` uses `Volatile.Read` (`NativeAudioFrameRingBuffer.cs:232`).
- `WriteSharedIndex()` uses `Volatile.Write` (`NativeAudioFrameRingBuffer.cs:238`).
- Producer write path publishes write index through `WriteSharedIndex(...WriteIndexSlot...)` (`NativeAudioFrameRingBuffer.cs:116-117`).
- Consumer mix path publishes read index through `WriteSharedIndex(...ReadIndexSlot...)` (`NativeAudioFrameRingBuffer.cs:176-177`).

Critical SPSC violations found: 0.

Review findings:
- `PlayerCriticalProceduralAudioRenderer.cs:3388` reads `_impactEventWriteIndex` directly before publishing it with `Volatile.Write` at `3401`. This is probably producer-owned SPSC state, but ownership is implicit.
- `PlayerCriticalProceduralAudioRenderer.cs:3409` reads `_impactEventReadIndex` directly before publishing it with `Volatile.Write` at `3417`. This is probably consumer-owned SPSC state, but ownership is implicit.

Verdict:
- Core audio frame SPSC queue is correctly using Volatile access.
- The impact-event mini-queue should document producer-owned and consumer-owned index access or wrap it in helper methods to avoid future accidental cross-thread naked reads.

## 7. MANDATORY QUEUE OF FIXES

Priority order:

1. Split or decontaminate `Hecton8.Core.asmdef`. It should not directly reference UI, URP, MapMagic, Crest, GPUInstancer, or VolumetricLightBeam.
2. Normalize Burst attributes across all job structs. This is a broad mechanical compliance pass but must be reviewed per job because some jobs may intentionally need deterministic precision instead of `FloatMode.Fast`.
3. Remove unauthorized `LateUpdate()` from `EquipmentInteractionHandler`, `GlobalPhysicsStateManager`, and `SuitHUDV4CanvasOverlay` first. Editor/camera/UI helpers can be documented after core owners are fixed.
4. Remove direct hot-path interpolation in `SargassumMicroFaunaBoids.Tick`, `WorldProceduralScatterDirector.SlowTick`, and `HectonNarrativeDirector.SlowTick`.
5. Review the 21 procedural geometry candidate files for material lifetime, GPU instancing path, and VRAM ownership.

## Appendix A - Full Burst Attribute Violation List

Missing column names:
- `BurstCompile` means the job has no `[BurstCompile]` near the struct declaration.
- `FloatMode.Fast` means `[BurstCompile]` exists or may exist, but does not explicitly declare `FloatMode = FloatMode.Fast`.
- `FloatPrecision.Standard` means `[BurstCompile]` exists or may exist, but does not explicitly declare `FloatPrecision = FloatPrecision.Standard`.

| File:Line | Struct | Missing |
|---|---|---|
| `Assets/_Project/Scripts/CraftingSystem.cs:22` | `EvaluateRecipeAvailabilityJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/CraftingSystem.cs:51` | `BuildDeconstructionYieldJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/EncounterDirector.cs:759` | `EncounterDirectorJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/FlowFieldVisualizer.cs:506` | `FlowSamplingJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonFloatingOrigin.cs:26` | `OriginShiftTranslateJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonFloatingOrigin.cs:40` | `AupDriftCheckJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonFluidEngine.cs:1864` | `WaveQueryJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonFluidEngine.cs:1934` | `BuoyancyJob` | BurstCompile; FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:428` | `VoxelDensityJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1486` | `VoxelColliderChunkClassifyJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1525` | `VoxelMCCountJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1587` | `VoxelMCExtractJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1740` | `VoxelWeldJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1865` | `VoxelNormalJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1954` | `VoxelTerrainSeamSnapJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2011` | `VoxelSeamNormalBlendJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2128` | `VoxelShiftAwareProjectionJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2147` | `VoxelBiomeSampleJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2179` | `VoxelColorJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2268` | `VoxelSpawnPointJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2584` | `VoxelMeshBakeJob` | BurstCompile; FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs:267` | `HectonVertexJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs:391` | `HectonNormalJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs:413` | `HectonColorJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/HectonWorldGenerator.cs:475` | `HectonPhysicsBakeJob` | BurstCompile; FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/PhysicsApplySystem.cs:256` | `ValidateForcePacketsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/PlayerInventory.cs:59` | `InventorySortJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/PlayerInventory.cs:70` | `InventoryRadixSortJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/PlayerInventory.cs:133` | `InventoryMassVolumeJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/PlayerInventory.cs:171` | `InventoryRadioactiveHalfLifeJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/PowerGrid.cs:81` | `ResolveBatteryDispatchJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/ProximityColliderSystem.cs:124` | `DistanceCalcJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:388` | `IndexedBlockDecompressJob` | BurstCompile; FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:425` | `BuildSectorEntityStateSortEntriesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:454` | `RadixSortSectorEntityStateEntriesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:508` | `ExtractSortedSectorEntityStatesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveBinaryStorage.cs:520` | `CompressSectorEntityStateJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SaveManager.cs:132` | `IntegrityScanJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:247` | `AtmosphereStepJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:437` | `FluidTransferJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:507` | `BulkheadTransferDeltaJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:560` | `ApplyBulkheadTransferJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:623` | `FloodMassPropertiesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:63` | `HullDamageDiffusionJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:173` | `HullDentJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/TetherInstance.cs:26` | `BuildVisualCatenaryJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/TetherManager.cs:22` | `TranslateVisualPointsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1403` | `CarveSdfJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs:422` | `CellSamplingJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs:225` | `CanAcceptCandidateJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs:319` | `EvaluateScatterCellCandidateBatchJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs:839` | `EvaluateScatterRescueCandidateBatchJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs:122` | `UpdateMigratorySargassumIslandsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:46` | `AdvanceExtractionJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs:712` | `IntegrityValidationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs:45` | `BuildPipeTopologicalOrderJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:57` | `BuildTubeFramesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:105` | `BuildTubeVerticesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:154` | `BuildTubeIndicesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:57` | `ImportanceScoringJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:103` | `VisualInterpolationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs:68` | `DataOnlyFaunaLodJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs:24` | `SolveSpineJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Gameplay/DebrisManager.cs:740` | `DebrisSimulationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:47` | `EvaluateHazardExposureJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:849` | `BuildFingerSpherecastCommandsJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:890` | `ProcessFingerHitsJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:146` | `PublishNodeStatesJob` | BurstCompile; FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:209` | `EvaluateGraphJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:792` | `InitializePotentialBuffersJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:807` | `RelaxNodePotentialsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:885` | `ApplyPotentialsAndLoadsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Quest/QuestStateManager.cs:1663` | `EvaluateQuestSignalJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:106` | `DurabilityDecayJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/UI/PDAMapTab.cs:42` | `BuildCartographyTextureJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/UI/SonarHoloCompass.cs:54` | `ProjectImpactBlipsJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2782` | `ThermalCrystallizationBoundaryJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:39` | `ChemicalDiffusionJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ChunkLocalOffsetQuantization.cs:41` | `QuantizeChunkLocalOffsetsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ChunkLocalOffsetQuantization.cs:59` | `DequantizeChunkLocalOffsetsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/EcosystemDirector.cs:79` | `LotkaVolterraSolveJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/EcosystemDirector.cs:209` | `PopulationDiffusionJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/EntropyYieldJob.cs:50` | `EntropyYieldJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs:107` | `PopulateCascadePhaseSeedsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs:172` | `ParasiteGrowthJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs:89` | `EvaluateMaturationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:21` | `BuildMatrixVisibilityMaskJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs:70` | `FinalizeSingleDrawCommandOutputJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:362` | `BuildVegetationVisibilityMaskJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:521` | `FinalizeVegetationDrawOutputJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:1165` | `TerrainHoleMaskBuildJob` | FloatMode.Fast; FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:8765` | `GenerateAnchoredVegetationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9090` | `GenerateFloatingVegetationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9223` | `SampleBiomassDensityJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9242` | `VegetationDensityQueryJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9280` | `ThreatPropagationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9485` | `ThreatVoxelizationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9630` | `BuildAbyssalFlowFieldJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9840` | `BuildAbyssalThermalGridJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:9954` | `BuildAbyssalFlowVolumeJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:10088` | `NativeAStarJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:10980` | `CullHLODInstancesJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:11038` | `DefragPoolJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:11096` | `ReduceAverageDensityJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/LODSystemManager.cs:711` | `DistanceCalculationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:316` | `CombineMeshDataJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:401` | `BuildProxyMeshJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:454` | `BuildDamageDecalMeshJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:146` | `BuildDensityContributionJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:224` | `BuildLeviathanNodeJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/ScatterEvaluator.cs:227` | `ScatterCellEvaluationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:69` | `PassabilityBuildJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:82` | `ClearanceDilationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:155` | `ObstacleStampJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:199` | `CopyByteBufferJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:214` | `PartialObstacleResetAndStampJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs:279` | `PartialClearanceDilationJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:143` | `ValidateAupIntegrityJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:159` | `RebuildAbsolutePositionsJob` | FloatPrecision.Standard |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:172` | `FarUnloadCandidatesJob` | FloatPrecision.Standard |

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | No runtime code changed. Static analysis only. |
| GC | No runtime code changed. Audit identifies GC risks but does not remove them. |
| Memory | No runtime memory behavior changed. VRAM claims require runtime profiler proof. |
| Cadence | No dispatcher/tick behavior changed. |
| Correctness | Documentation improves debt visibility. No gameplay correctness change. |

## Verification State

| Check | Result |
|---|---|
| Static scan completed | YES |
| `.agents-skills/` read pass completed | YES |
| Report generated | YES |
| Unity Play Mode launched | NO |
| Runtime profiler proof | NO |
| MCP console after report generation | UNAVAILABLE - `read_console` ping not answered twice |

STATUS: PENDING VERIFICATION
