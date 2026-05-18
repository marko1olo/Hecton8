<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Foundation Guard Scan
Date: 2026-05-07
Status: PENDING VERIFICATION

- Generated: 2026-05-06 13:50:28
- Project root: C:\hades\Hecton8
- Scope: Assets/_Project/Scripts/**/*.cs
- Status: PENDING VERIFICATION

## Guard Results

| Guard | Count | Meaning |
|---|---:|---|
| Global registry self-registration sites | 518 | Informational. Broad GlobalRegistry.Register*(this) and Renderables.Register(this) scan. Must use registry truth-state checks, not blind flags. |
| Blind registry flag drift | 0 | Must be 0. Pattern: Register*(this, ...) followed by _field = true. |
| Origin shift listener blind flag drift | 0 | Must be 0. Pattern: HectonFloatingOrigin.RegisterListener(this) followed by _field = true. |
| Synchronous job .Run( sites | 10 | Must be 0. Synchronous JobSystem barriers are no longer allowed in first-party runtime source. |
| Hot-path synchronous job .Run( review sites | 0 | Must be 0. Secondary classifier for stale sync job barriers in gameplay cadence. |
| Completion .Complete( text hits | 35 | Review queue. Custom dispatcher completions must be classified separately from JobHandle.Complete. |
| Guarded dispatcher completion sites | 1 | Source-level IsCompleted/swap-window helper pattern, not runtime proof. |
| UnsafeUtility.MemCpy outside guard | 0 | Must be 0 outside UnsafeMemoryCopyGuard. |
| Legacy PlayerSignalEvents.On* subscriptions | 0 | Must be 0 after NativeQueue/listener migration. |
| Direct raw-array listener dispatch | 0 | Must be 0. Pattern rawArray[i].On* bypasses null-slot guards during event flush. |
| GlobalRegistry.Input nullable misuse | 0 | Must be 0. Input service is a null-object fallback; use IsInitialized and direct service calls. |
| Direct InputManager.Instance sites | 0 | Review queue. Runtime owners should prefer GlobalRegistry.Input/InputBinding; bootstrap-owned native binding paths must be documented. |
| Hot-path direct InputManager.Instance review sites | 0 | Must be 0. One-hop source classifier for stale singleton reads in gameplay cadence. |
| Optimization singleton residue | 0 | Must be 0. Optimization/VRAM services are registry-owned; no private _instance, static Instance, DDOL, or SINGLETON comments. |
| Unauthorized Unity loop methods | 0 | Must be 0 outside SystemDispatcher/Core-approved exceptions. Gameplay cadence belongs to ITickable/dispatcher lanes. |
| Legacy coroutine sites | 22 | Must be 0 outside Editor. Coroutines bypass controlled tick lanes and allocate state. |
| Forbidden runtime asset API sites | 0 | Must be 0 outside Editor. Forbids Resources.Load and Camera.main. |
| Release-reachable direct hot-path Debug.Log sites | 0 | Must be 0. Debug.Log/Warning/Error directly inside gameplay cadence is forbidden outside UNITY_EDITOR/DEVELOPMENT_BUILD guards. |
| Release-reachable one-hop Debug.Log review sites | 0 | Review queue. Conservative same-file call classifier; owner review required before promotion to hard gate. |
| Broad physics layer masks outside Editor | 0 | Must be 0. Forbids LayerMask=-1, ~0, and direct all-layer masks in Physics/RaycastCommand lines. |
| Runtime Find API text hits outside Editor folder | 9 | Review queue. Bootstrap cold paths must be documented; gameplay hot paths must be removed. |
| One-hop hot-path callee names | 2573 | Audit classifier only. Used to mark .Run/.Complete/MemCpy/Find hits inside methods called by hot members. |

## Blind Registry Flag Hits

- none

## Origin Shift Listener Blind Flag Hits

- none

## Synchronous Job Run Sites

- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:185 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - return ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:186 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:187 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:188 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs"), ".Complete(", ".Run(");
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:150 [MethodBodyIsBridgeOnly; cold/unknown review] - body.IndexOf(".Run()", StringComparison.Ordinal) < 0 &&
- Assets\_Project\Scripts\Editor\OmegaAutonomySmokeTestRunner.cs:21 [Run; cold/unknown review] - passed = OmegaAutonomySmokeTester.Run(out json);
- Assets\_Project\Scripts\Editor\SpaceEngine098TerrainSmokeTestRunner.cs:21 [Run; cold/unknown review] - passed = SpaceEngine098TerrainSmokeTester.Run(out json);
- Assets\_Project\Scripts\Editor\SpaceEngineResearchSmokeTester.cs:328 [AuditRecentScope; cold/unknown review] - result.JobBarrierTokenCount += CountToken(text, ".Complete()") + CountToken(text, ".Run()");
- Assets\_Project\Scripts\Editor\TechArtPipelineSmokeTestAutoRunner.cs:42 [RunRequestedSmokeTest; cold/unknown review] - passed = TechArtPipelineSmokeTester.Run(out json);
- Assets\_Project\Scripts\Editor\TechArtPipelineSmokeTester.cs:177 [CheckForensicStaticRules; cold/unknown review] - !FileContains(BridgePath, ".Run()");

## Completion Text Hits

- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:116 [TryRunBurstSubtractionBoundsStress; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:185 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - return ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:186 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:187 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"), ".Complete(", ".Run(") &&
- Assets\_Project\Scripts\SavePersistenceOmegaSmokeTester.cs:188 [TryRunRuntimeBarrierSourceAudit; cold/unknown review] - ContainsNone(ReadProjectFile("Assets/_Project/Scripts/SaveIndexedSectorBoundsMath.cs"), ".Complete(", ".Run(");
- Assets\_Project\Scripts\ThermalMeltSmokeTester.cs:131 [ValidateDirtyBlendJob; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\ThermalMeltSmokeTester.cs:174 [ValidateAupRaymarchJob; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\ThermalMeltSmokeTester.cs:206 [ValidateLocalizedNavPatchJob; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\VoxelDeformationSmokeTester.cs:226 [ValidatePureVoidNavGrid; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\VoxelDeformationSmokeTester.cs:267 [ValidateVoidChunkBoundsEarlyExit; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\VoxelDeformationSmokeTester.cs:280 [ValidateVoidChunkBoundsEarlyExit; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\VoxelDeformationSmokeTester.cs:335 [ValidateVertexAmbientOcclusion; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Audio\PlayerCriticalBufferJobs.cs:27 [Clear; cold/unknown review] - job.Schedule(safeCount, 256).Complete();
- Assets\_Project\Scripts\Audio\PlayerCriticalMetallicGrainBank.cs:25 [Generate; cold/unknown review] - job.Schedule(grainBank.Length, 64).Complete();
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:73 [Run; cold/unknown review] - AppendCheck("hot DSP block has no JobHandle completion", !ExtractMethodBody(renderer, "private void MixAndFilterBlock").Contains(".Complete()"), ref passedCount, ref failedCount, checks);
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:149 [MethodBodyIsBridgeOnly; cold/unknown review] - body.IndexOf(".Complete()", StringComparison.Ordinal) < 0 &&
- Assets\_Project\Scripts\Editor\AnomalySmokeTester.cs:161 [RunSmoke; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Editor\AnomalyTestHarness.cs:123 [RunPerfectBowlAssertion; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Editor\AnomalyTestHarness.cs:176 [RunCliffOverhangAssertion; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Editor\AnomalyTestHarness.cs:260 [RunFeatureDetectionAssertion; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Editor\AnomalyTestHarness.cs:316 [RunSeamStitchAssertion; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\Editor\AnomalyTestHarness.cs:391 [RunSdfInjectionAssertion; cold/unknown review] - fissureHandle.Complete();
- Assets\_Project\Scripts\Editor\SpaceEngineResearchSmokeTester.cs:328 [AuditRecentScope; cold/unknown review] - result.JobBarrierTokenCount += CountToken(text, ".Complete()") + CountToken(text, ".Run()");
- Assets\_Project\Scripts\Editor\TechArtPipelineSmokeTester.cs:175 [CheckForensicStaticRules; cold/unknown review] - bool noJobBarrier = !FileContains(BridgePath, ".Complete()") &&
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:657 [EvaluateSignal; cold/unknown review] - signalEvaluationHandle.Complete();
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:1327 [RefreshStateMetadata; cold/unknown review] - checksumHandle.Complete();
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:142 [RunFogBlendSmokeTest; cold/unknown review] - job.Schedule(1, 1).Complete();
- Assets\_Project\Scripts\World\DispatcherJobSwap.cs:71 [TryComplete; guarded review: guarded by DispatcherJobSwap.TryComplete IsCompleted/swap-window contract] - handle.Complete();
- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:242 [Generate; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\HectonBiomeMatrixMapMagicPostProcessNode.cs:141 [Generate; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\HectonBrinePoolMeshGenerator.cs:93 [<unknown>; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:148 [Generate; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:163 [Generate; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:67 [RunSlopeCavitySplatmapSmoke; cold/unknown review] - handle.Complete();
- Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:84 [RunSlopeCavitySplatmapSmoke; cold/unknown review] - handle.Complete();

## Unsafe MemCpy Outside Guard

- none

## Legacy PlayerSignalEvents Subscriptions

- none

## Direct Raw-Array Listener Dispatch

- none

## GlobalRegistry.Input Nullable Misuse

- none

## Direct InputManager.Instance Sites

- none

## Optimization Singleton Residue

- none

## Unauthorized Unity Loop Methods

- none

## Legacy Coroutine Sites

- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:109 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:115 [Outlets; cold/unknown review] - yield return brineMaskOut;
- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:116 [Outlets; cold/unknown review] - yield return deepestPointsOut;
- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:117 [Outlets; cold/unknown review] - yield return pillarCoordinatesOut;
- Assets\_Project\Scripts\World\HectonAnomalyMapMagicNode.cs:118 [Outlets; cold/unknown review] - yield return fissureMaskOut;
- Assets\_Project\Scripts\World\HectonHydraulicErosionMapMagicNode.cs:187 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonHydraulicErosionMapMagicNode.cs:193 [Outlets; cold/unknown review] - yield return erodedHeightOut;
- Assets\_Project\Scripts\World\HectonHydraulicErosionMapMagicNode.cs:194 [Outlets; cold/unknown review] - yield return sedimentMaskOut;
- Assets\_Project\Scripts\World\HectonHydraulicErosionMapMagicNode.cs:195 [Outlets; cold/unknown review] - yield return wearMaskOut;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:52 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:57 [Outlets; cold/unknown review] - yield return heightOut;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:181 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:186 [Outlets; cold/unknown review] - yield return heightOut;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:322 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonSpaceEngine098MapMagicNodes.cs:327 [Outlets; cold/unknown review] - yield return heightOut;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:66 [Inlets; cold/unknown review] - yield return heightIn;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:67 [Inlets; cold/unknown review] - yield return sedimentIn;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:72 [Outlets; cold/unknown review] - yield return sandOut;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:73 [Outlets; cold/unknown review] - yield return rockOut;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:74 [Outlets; cold/unknown review] - yield return siltOut;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:75 [Outlets; cold/unknown review] - yield return cavityOut;
- Assets\_Project\Scripts\World\HectonTerrainSplatmapMapMagicNode.cs:76 [Outlets; cold/unknown review] - yield return slopeWeightOut;

## Forbidden Runtime Asset API Sites

- none

## Release-Reachable Direct Hot-Path Debug Log Sites

- none

## Release-Reachable One-Hop Debug Log Review Sites

- none

## Broad Physics Layer Masks Outside Editor

- none

## Runtime Find API Text Hits Outside Editor Folder

- Assets\_Project\Scripts\ModalWindow.cs:294 [EnsureInstanceAvailable; cold/unknown review] - ModalWindow candidate = FindAnyObjectByType<ModalWindow>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\SaveManager.cs:856 [ResolveLoadingScreenController; cold/unknown review] - _cachedLoadingScreenController = FindAnyObjectByType<LoadingScreenController>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:121 [AutoResolve; cold/unknown review] - : UnityEngine.Object.FindAnyObjectByType<HectonCelestialEngine>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:126 [AutoResolve; cold/unknown review] - : UnityEngine.Object.FindAnyObjectByType<EclipseGameplaySystem>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:129 [AutoResolve; cold/unknown review] - depthCacheBootstrap = UnityEngine.Object.FindAnyObjectByType<HectonCrestOceanDepthCacheBootstrap>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:132 [AutoResolve; cold/unknown review] - ecosystemDirector = UnityEngine.Object.FindAnyObjectByType<EcosystemDirector>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:135 [AutoResolve; cold/unknown review] - biolumController = UnityEngine.Object.FindAnyObjectByType<HectonBiolumController>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:140 [AutoResolve; cold/unknown review] - : UnityEngine.Object.FindAnyObjectByType<SpatialAudioManager>(FindObjectsInactive.Include);
- Assets\_Project\Scripts\Dev\CelestialSyncSmokeTester.cs:145 [AutoResolve; cold/unknown review] - : UnityEngine.Object.FindAnyObjectByType<RandomEventSystem>(FindObjectsInactive.Include);

## Failure Policy

- Exit code 1 when blind registry flag drift, origin-shift listener blind flag drift, synchronous job .Run( sites, raw MemCpy outside guard, legacy PlayerSignalEvents.On* subscriptions, direct raw-array listener dispatch, GlobalRegistry.Input nullable misuse, hot-path direct InputManager.Instance access, optimization singleton residue, unauthorized Unity loops, legacy coroutines, forbidden runtime asset APIs, release-reachable direct hot-path Debug.Log sites, or broad physics layer masks are found.
- .Run( is now a hard defect after the first-party runtime inventory reached zero. Use scheduled jobs with dispatcher-owned swap windows, direct bounded kernels, or documented cold async lanes.
- .Complete( remains an inventory signal until method ownership proves a hot-path stall or the site is promoted to a stricter owner-specific guard.
- One-hop hot-path classification is conservative. A marked site still needs owner-level review before runtime refactor.
- DispatcherJobSwap.TryComplete handle.Complete() is classified as guarded when the source-level IsCompleted/swap-window contract is present; this is not Play Mode proof.
- UnsafeUtility.MemCpy outside UnsafeMemoryCopyGuard is a hard defect; this script reports it explicitly for CI promotion.
- Legacy PlayerSignalEvents.On* subscriptions are stale after listener/NativeQueue migration and must remain at 0.
- Direct raw-array listener dispatch is a hard defect. Event lanes must copy the slot to a local listener and null-check before calling On*.
- GlobalRegistry.Input is a null-object fallback service. Null checks and null-conditional calls are stale service-contract drift and must remain at 0.
- Direct InputManager.Instance access is an inventory signal in cold paths and a hard defect when classified as hot-path. Prefer GlobalRegistry.Input/InputBinding for gameplay consumers; keep bootstrap/native-binding exceptions explicit.
- Optimization singleton residue is a hard defect after the registry ownership purge. Optimization/VRAM services must resolve through GlobalRegistry slots only.
- Unauthorized Unity loop methods are hard defects outside dispatcher-approved files. Use ITickable/IFixedTickable/ISlowTickable registration instead.
- Legacy coroutine sites are hard defects outside Editor. Use dispatcher state machines or Unity 6 Awaitable only in approved cold async lanes.
- Resources.Load and Camera.main are hard defects outside Editor. Use Addressables/registered services/cached camera references.
- Release-reachable direct hot-path Debug.Log/LogWarning/LogError sites are hard defects. Release-reachable one-hop Debug.Log hits are a review queue until owner-level analysis proves the call runs in gameplay cadence.
- Broad physics masks are hard defects. Use owner-specific cached masks such as TerrainLayerMask | VoxelCaveLayerMask; do not raycast against every layer.
- Runtime Find API hits are classified by folder/method. Bootstrap cold paths require documentation; gameplay hot paths require replacement.
- This scan is source-only. It does not prove Play Mode, GC, frame time, memory retention, or Unity console health.
