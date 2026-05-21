#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class BuilderHolographyTunerWindow : EditorWindow
    {
        private const int HistogramBucketCount = 16;
        private readonly VisualElement[] _histogramBars = new VisualElement[HistogramBucketCount];
        private readonly int[] _buckets = new int[HistogramBucketCount];
        private Label _layoutLabel;
        private Label _telemetryLabel;
        private Slider _magneticRadius;
        private Slider _gridTolerance;
        private Slider _qualityOverride;

        [MenuItem("HECTON-8/Construction/Builder Tool X-Ray")]
        public static void Open()
        {
            BuilderHolographyTunerWindow window = GetWindow<BuilderHolographyTunerWindow>();
            window.titleContent = new GUIContent("Builder Tool X-Ray");
            window.minSize = new Vector2(420f, 300f);
        }

        [MenuItem("HECTON-8/Construction/Builder Holography/Run Static Audit")]
        public static void RunStaticAuditMenu()
        {
            BuilderHolographyStaticAudit.WriteReport();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _layoutLabel = new Label();
            _telemetryLabel = new Label();
            root.Add(_layoutLabel);
            root.Add(_telemetryLabel);

            _magneticRadius = new Slider("MagneticRadius", 0.25f, 6f);
            _gridTolerance = new Slider("GridSnapTolerance", 0.001f, 0.5f);
            _qualityOverride = new Slider("GlobalQualityWeight", 0f, 1f);
            _magneticRadius.RegisterValueChangedCallback(evt => MutateTuning(evt.newValue, null, null));
            _gridTolerance.RegisterValueChangedCallback(evt => MutateTuning(null, evt.newValue, null));
            _qualityOverride.RegisterValueChangedCallback(evt => MutateTuning(null, null, evt.newValue));
            root.Add(_magneticRadius);
            root.Add(_gridTolerance);
            root.Add(_qualityOverride);

            VisualElement histogram = new VisualElement();
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.height = 96f;
            histogram.style.marginTop = 8f;
            root.Add(histogram);
            for (int i = 0; i < HistogramBucketCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 20f;
                bar.style.marginRight = 2f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.backgroundColor = new Color(0.08f, 1f, 0.72f, 0.85f);
                _histogramBars[i] = bar;
                histogram.Add(bar);
            }

            Button auditButton = new Button(BuilderHolographyStaticAudit.WriteReport) { text = "Write MEMORY_OPTIMIZATION_REPORT.json" };
            root.Add(auditButton);
            EditorApplication.update += EditorUpdate;
            RefreshUi();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            RefreshUi();
        }

        private void MutateTuning(float? magneticRadius, float? gridTolerance, float? quality)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.ConstructionSocketTuning,
                    1,
                    SystemID.Construction,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<ConstructionSocketTuningDTO> tuningHandle,
                    out NativeArray<ConstructionSocketTuningDTO> tuningBuffer))
                return;

            try
            {
                ConstructionSocketTuningDTO tuning = tuningBuffer[0];
                if (magneticRadius.HasValue)
                    tuning.SnappingRadius = math.max(0.001f, magneticRadius.Value);
                if (gridTolerance.HasValue)
                    tuning.DearLieShrinkMeters = math.clamp(gridTolerance.Value, 0f, 1f);
                if (quality.HasValue)
                    tuning.GlobalQualityWeight = math.saturate(quality.Value);
                tuningBuffer[0] = tuning;
            }
            finally
            {
                vault.ReleaseWriteLock(in tuningHandle, SystemID.CoreDiagnostics);
            }
        }

        private void RefreshUi()
        {
            bool layoutOk = ShinobuSocketConstructionRuntime.ValidateStructLayout() &&
                            UnsafeUtility.SizeOf<BuilderGhostStateDTO>() == ShinobuSocketConstructionRuntime.BuilderGhostStateSizeBytes &&
                            ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition)) == 64;
            if (_layoutLabel != null)
                _layoutLabel.text = layoutOk ? "Layout: PASS 128B AUP@64" : "Layout: FAIL";

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                if (_telemetryLabel != null)
                    _telemetryLabel.text = "Telemetry: Vault unavailable";
                return;
            }

            if (TryReadExistingVaultView(
                    vault,
                    BufferID.ConstructionSocketTuning,
                    out NativeArray<ConstructionSocketTuningDTO> tuningBuffer) &&
                tuningBuffer.Length > 0)
            {
                ConstructionSocketTuningDTO tuning = tuningBuffer[0];
                _magneticRadius?.SetValueWithoutNotify(tuning.SnappingRadius);
                _gridTolerance?.SetValueWithoutNotify(tuning.DearLieShrinkMeters);
                _qualityOverride?.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            }

            if (!TryReadExistingVaultView(
                    vault,
                    ShinobuSocketConstructionRuntime.BuilderGhostTelemetryBufferId,
                    out NativeArray<HolographyTelemetryEntry> holographyTelemetry) ||
                holographyTelemetry.Length <= 0)
                return;

            int validRows = 0;
            float maxMicroseconds = 0f;
            for (int i = 0; i < HistogramBucketCount; i++)
                _buckets[i] = 0;

            int count = math.min(holographyTelemetry.Length, ShinobuSocketConstructionRuntime.TelemetryCapacity);
            for (int i = 0; i < count; i++)
            {
                HolographyTelemetryEntry entry = holographyTelemetry[i];
                if (entry.Frame == 0u && entry.PrefabHashID == 0u)
                    continue;

                validRows++;
                float us = math.max(0f, entry.SolverMicroseconds);
                maxMicroseconds = math.max(maxMicroseconds, us);
                int bucket = math.clamp((int)math.floor(us / 32f), 0, HistogramBucketCount - 1);
                _buckets[bucket]++;
            }

            if (_telemetryLabel != null)
                _telemetryLabel.text = "Telemetry: rows=" + validRows + " maxUs=" + maxMicroseconds.ToString("0.00");

            int maxBucket = 1;
            for (int i = 0; i < HistogramBucketCount; i++)
                maxBucket = math.max(maxBucket, _buckets[i]);

            for (int i = 0; i < HistogramBucketCount; i++)
            {
                VisualElement bar = _histogramBars[i];
                if (bar == null)
                    continue;

                bar.style.height = math.lerp(4f, 92f, _buckets[i] / (float)maxBucket);
                bar.style.backgroundColor = i >= 15
                    ? new Color(1f, 0.18f, 0.12f, 0.9f)
                    : new Color(0.08f, 1f, 0.72f, 0.85f);
            }
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                vault.TryReadHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existingHandle;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    required,
                    owner,
                    options);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;

            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }
    }

    public static class BuilderHolographyStaticAudit
    {
        private const string ReportPath = "Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json";

        public static void WriteReport()
        {
            bool layoutPass = ShinobuSocketConstructionRuntime.ValidateStructLayout() &&
                              UnsafeUtility.SizeOf<BuilderGhostStateDTO>() == 128 &&
                              UnsafeUtility.AlignOf<BuilderGhostStateDTO>() >= 8 &&
                              ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition)) == 64 &&
                              ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.ValidationFlags)) == 92;

            string root = Directory.GetCurrentDirectory();
            string playerBuilder = Read(root, "Assets/_Project/Scripts/PlayerBuilder.cs");
            string builderStatusOverlay = Read(root, "Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs");
            string pdaConstructionTab = Read(root, "Assets/_Project/Scripts/UI/PDAConstructionTab.cs");
            string legacyPreviewScriptPath = "Assets/_Project/Scripts/Placement" + "Ghost.cs";
            bool legacyPreviewScriptRemoved = !File.Exists(Path.Combine(root, legacyPreviewScriptPath));
            string legacyPreviewScript = legacyPreviewScriptRemoved ? string.Empty : Read(root, legacyPreviewScriptPath);
            string previewBatch = Read(root, "Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs");
            string pipePreview = Read(root, "Assets/_Project/Scripts/Construction/VRPipeBlueprintPreview.cs");
            string habitatConstruction = Read(root, "Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs");
            string constructionManager = Read(root, "Assets/_Project/Scripts/ConstructionManager.cs");
            string binaryLayoutManifest = Read(root, "Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs");
            string socketJobs = Read(root, "Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs");
            string socketData = Read(root, "Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs");
            string modularValidator = Read(root, "Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs");
            string voxelVolume = Read(root, "Assets/_Project/Scripts/HectonVoxelVolume.cs");
            string shader = Read(root, "Assets/_Project/Shaders/Hecton_ConstructionDearLieHologram.shader");
            string coreProject = Read(root, "Hecton8.Core.csproj");
            string builderHolographyTools = Read(root, "Assets/_Project/Scripts/Editor/BuilderHolographyTools.cs");
            string constructionSocketEditorTools = Read(root, "Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs");
            bool noGhostInstantiate = NoRuntimeGhostConsumerRoute(root);
            string physxOverlapToken = "OverlapBox" + "NonAlloc";
            bool noPhysxOverlap = legacyPreviewScriptRemoved || !legacyPreviewScript.Contains(physxOverlapToken);
            string setDataToken = ".Set" + "Data(";
            string meshInstancedToken = "DrawMesh" + "Instanced";
            string matrixArrayToken = "Matrix4x4" + "[]";
            string matricesToken = "_mat" + "rices";
            string socketAlignmentToken = "TryResolveSocket" + "Alignment(";
            string candidateObjectToken = "candidate" + "Ghost";
            string latestVaultToken = "TryGetLatest" + "Created";
            string ensureAndResolveToken = "TryEnsureAndResolve" + "Buffers";
            bool activeBuildReadinessReadOnly = ActiveBuildReadinessIsCached(playerBuilder);
            bool noHotVaultEnsureInPreviewBatch = !previewBatch.Contains(ensureAndResolveToken) &&
                                                 previewBatch.Contains("TryReadCachedBuffers") &&
                                                 previewBatch.Contains("TryBindVaultCold");
            bool noHotVaultEnsureInPipePreview = !pipePreview.Contains(ensureAndResolveToken) &&
                                                 pipePreview.Contains("TryReadCachedBuffers") &&
                                                 pipePreview.Contains("EnsureBuffersCold");
            bool noSetData = !previewBatch.Contains(setDataToken) && !pipePreview.Contains(setDataToken);
            bool noPipeMeshInstancing = !pipePreview.Contains(meshInstancedToken) &&
                                        !pipePreview.Contains(matrixArrayToken) &&
                                        !pipePreview.Contains(matricesToken);
            bool noObjectAlignmentRoute = !habitatConstruction.Contains(socketAlignmentToken) &&
                                          !habitatConstruction.Contains(candidateObjectToken);
            bool noLegacyGhostPrefabAssets = NoLegacyGhostPrefabAssets(root);
            bool noNonZeroBuildableGhostPrefabReferences = NoNonZeroGhostPrefabRefs(root);
            bool noRuntimeVaultLatestFallback = !previewBatch.Contains(latestVaultToken) &&
                                                !pipePreview.Contains(latestVaultToken);
            string managedPlacementEventAllocation = "new BaseModule" + "PlacedEvent";
            string managedPlacementEventPublish = "HectonEventBus.Publish(new BaseModule" + "PlacedEvent";
            bool noManagedPlacementEventAllocation = !playerBuilder.Contains(managedPlacementEventAllocation) &&
                                                     !playerBuilder.Contains(managedPlacementEventPublish);
            bool noBindRuntimeManagerAllocation = !MethodContains(playerBuilder, "BindRuntimeReferences", "new HabitatConstructionManager(") &&
                                                  playerBuilder.Contains("EnsureHabitatConstructionManagerCold");
            bool noProjectFileLegacyGhostCompileInclude = !coreProject.Contains("Placement" + "Ghost.cs");
            bool targetSocketCommitDirectPath = playerBuilder.Contains("TryMarkShinobuTargetSocketOccupiedDirect");
            bool noSameFrameSocketSnapReadback = NoSameFrameSocketSnapReadback(playerBuilder);
            bool activeBuildResourceReadOnly = playerBuilder.Contains("HasResourcesForActiveBuildable => _cachedHasResourcesForActiveBuildable");
            bool noQualityScaledSocketTruth = !socketJobs.Contains("ResolveCandidateBudget(") &&
                                              !socketJobs.Contains("ResolveSearchRadius(");
            string terrainProbeQualityCall = "ResolveTerrain" + "ProbeCount(settings." + "GlobalQualityWeight)";
            string terrainProbeQualitySignature = "ResolveTerrain" + "ProbeCount(fl" + "oat";
            bool noQualityScaledTerrainProbeTruth = !playerBuilder.Contains(terrainProbeQualityCall) &&
                                                    !modularValidator.Contains(terrainProbeQualitySignature) &&
                                                    modularValidator.Contains("public static int ResolveTerrainProbeCount()") &&
                                                    MethodContains(modularValidator, "ResolveTerrainProbeCount", "return TerrainProbeTruthCount;");
            bool socketTruthHelpersMaxOnly = MethodContains(socketData, "ResolveCandidateBudget", "return safeMax;") &&
                                             MethodContains(socketData, "ResolveSearchRadius", "return high;");
            string socketCandidateQualitySignature = "ResolveCandidateBudget(fl" + "oat quality";
            string socketRadiusQualitySignature = "ResolveSearchRadius(fl" + "oat quality";
            string deletedPreviewToken = "Blueprint" + "PreviewInstance";
            bool socketTruthHelpersNoQualityParameter = socketData.Contains("ResolveCandidateBudget(int minBudget, int maxBudget)") &&
                                                        socketData.Contains("ResolveSearchRadius(float lowMeters, float ultraMeters)") &&
                                                        !socketData.Contains(socketCandidateQualitySignature) &&
                                                        !socketData.Contains(socketRadiusQualitySignature);
            bool noDeletedPreviewLayoutGate = !modularValidator.Contains(deletedPreviewToken) &&
                                              !binaryLayoutManifest.Contains(deletedPreviewToken) &&
                                              modularValidator.Contains("BuilderGhostIndirectArgsSizeBytes") &&
                                              binaryLayoutManifest.Contains("BuilderGhostIndirectArgsDTO");
            bool dumpPathsOwnedByShinobu228 = socketData.Contains("Dump_SHINOBU_228.bin") &&
                                              socketData.Contains("Dump_SHINOBU_228_Holography.bin") &&
                                              !socketData.Contains("Dump_SHINOBU_217.bin") &&
                                              !socketData.Contains("Dump_SHINOBU_217_Holography.bin");
            bool noRecordHolographyTinyJob = !socketJobs.Contains("RecordHolographyTelemetryJob");
            bool noPreviewFinalizeGraphicsAllocation = !MethodContains(previewBatch, "TryFinalizePendingBuildAndUpload", "EnsureGraphicsBuffers(") &&
                                                       !MethodContains(pipePreview, "TryFinalizePendingBuildAndUpload", "EnsureGraphicsBuffers(") &&
                                                       previewBatch.Contains("HasGraphicsBuffers()") &&
                                                       pipePreview.Contains("HasGraphicsBuffers()");
            bool holographyTelemetryHeartbeat = previewBatch.Contains("RecordActiveTelemetryHeartbeat()") &&
                                                MethodContains(previewBatch, "LateFrameTick", "RecordActiveTelemetryHeartbeat();");
            bool habitatIntegrityGraphVaultOwned = habitatConstruction.Contains("IntegrityNodeBufferId = (BufferID)70949") &&
                                                   habitatConstruction.Contains("IntegrityWriteScratchBufferId = (BufferID)70956") &&
                                                   habitatConstruction.Contains("IntegrityConnectionBufferId = (BufferID)70957") &&
                                                   habitatConstruction.Contains("IntegritySocketLookupBufferId = (BufferID)70958") &&
                                                   habitatConstruction.Contains("VaultGenerationHandle<IntegrityNodeRecord>") &&
                                                   habitatConstruction.Contains("VaultGenerationHandle<SocketLookupSlot>") &&
                                                   habitatConstruction.Contains("AdjacencyCounts") &&
                                                   habitatConstruction.Contains("SocketLookupSlot") &&
                                                   !habitatConstruction.Contains("NativeArray<IntegrityNodeRecord> _nodeBuffer") &&
                                                   !habitatConstruction.Contains("_adjacencyCountBuffer") &&
                                                   !habitatConstruction.Contains("_adjacencyWriteBuffer");
            string managedGraphListToken = "List<" + "int2>";
            string managedSocketDictionaryToken = "Dict" + "ionary<" + "SocketKey";
            bool habitatManagedGraphCollectionsRemoved = !habitatConstruction.Contains(managedGraphListToken) &&
                                                         !habitatConstruction.Contains(managedSocketDictionaryToken) &&
                                                         !habitatConstruction.Contains("SocketMatchEntry");
            bool noPipeReadCacheMutation = !MethodContains(pipePreview, "ResolvePointRuntime", "CacheRuntimeReferences(") &&
                                           !MethodContains(pipePreview, "ResolvePointAup", "CacheRuntimeReferences(");
            bool noPipeXrMethodGroupEventSubscription = !pipePreview.Contains("XRActiveChanged += HandleXRActiveChanged") &&
                                                        !pipePreview.Contains("XRActiveChanged -= HandleXRActiveChanged") &&
                                                        pipePreview.Contains("_xrActiveChangedHandler");
            bool noPipeLegacyVaultBufferHandle = !pipePreview.Contains("VaultBufferHandle<") &&
                                                 !pipePreview.Contains("ResolveBuffer(") &&
                                                 !pipePreview.Contains("GetBufferHandle<") &&
                                                 !pipePreview.Contains(".Resolve(vault)") &&
                                                 pipePreview.Contains("VaultGenerationHandle<BuilderGhostStateDTO>") &&
                                                 pipePreview.Contains("TryResolveHandle(in _stateHandle");
            bool noPipeGlobalSignalsOrigin = !pipePreview.Contains("GlobalSignals.") &&
                                             !pipePreview.Contains("CurrentRuntimeOriginAup") &&
                                             pipePreview.Contains("HectonFloatingOrigin.CurrentTotalOffsetDouble");
            bool noPipeUnityFrameCount = !pipePreview.Contains("Time.frameCount") &&
                                         pipePreview.Contains("CapturePreviewFrameId");
            bool noPipeManagedPointArrays = !pipePreview.Contains("AbsoluteUniversePosition[]") &&
                                            !pipePreview.Contains("bool[] _hasRuntimePoint") &&
                                            pipePreview.Contains("_runtimePointAup0") &&
                                            pipePreview.Contains("TryGetRuntimePointAup");
            bool noLegacyPreviewMeshFields = !previewBatch.Contains("private Mesh previewMesh") &&
                                             !previewBatch.Contains(deletedPreviewToken) &&
                                             !pipePreview.Contains("private Mesh segmentMesh");
            bool noHotConstructionRegistryVault = !habitatConstruction.Contains("GlobalRegistry.DataVault");
            bool noPerScheduleIntegrityGraphRebuild = habitatConstruction.Contains("_hasExistingGraphCache") &&
                                                      habitatConstruction.Contains("EnsureExistingGraphCache") &&
                                                      habitatConstruction.Contains("IndexCandidateSockets") &&
                                                      !MethodContains(habitatConstruction, "BuildValidationGraph", "ResolveBuildableData(moduleObject)") &&
                                                      !MethodContains(habitatConstruction, "BuildValidationGraph", "IndexSockets(");
            bool noHotValidationGraphVaultResize = MethodContains(habitatConstruction, "BuildValidationGraph", "HasValidationGraphCapacity(") &&
                                                   !MethodContains(habitatConstruction, "BuildValidationGraph", "EnsureNodeCapacity(") &&
                                                   MethodContains(habitatConstruction, "BuildAdjacency", "adjacencyCount > _adjacencyCapacity") &&
                                                   !habitatConstruction.Contains("EnsureAdjacencyCapacity(");
            bool builderUsesReadOnlyVoxelSdf = !playerBuilder.Contains("HectonVoxelVolume.TrySampleRuntimeSdfDensity(") &&
                                               playerBuilder.Contains("HectonVoxelVolume.TryReadRuntimeSdfDensity(") &&
                                               MethodContains(voxelVolume, "TryReadRuntimeSdfDensity", "continue;") &&
                                               !MethodContains(voxelVolume, "TryReadRuntimeSdfDensity", "RemoveAt(");
            string inventoryPlacementBufferToken = "_inventory" + "PlacementBuffer";
            string inventoryPlacementArrayToken = "PlayerInventory.Item" + "Placement[]";
            string inventoryPlacementsCopyToken = "Get" + "Placements(";
            bool noManagedInventoryPlacementSnapshot = !habitatConstruction.Contains(inventoryPlacementBufferToken) &&
                                                       !habitatConstruction.Contains(inventoryPlacementArrayToken) &&
                                                       !MethodContains(habitatConstruction, "HasBuildResources", inventoryPlacementsCopyToken) &&
                                                       MethodContains(habitatConstruction, "HasBuildResources", "GetItemIDsReadOnly()") &&
                                                       MethodContains(habitatConstruction, "HasBuildResources", "GetCraftLockedCountsReadOnly()");
            string costHashBufferToken = "_cost" + "HashBuffer";
            string costRemainingBufferToken = "_cost" + "RemainingBuffer";
            string costRemovedBufferToken = "_cost" + "RemovedBuffer";
            string costItemBufferToken = "_cost" + "ItemBuffer";
            bool noManagedCostTransactionArrays = !habitatConstruction.Contains(costHashBufferToken) &&
                                                  !habitatConstruction.Contains(costRemainingBufferToken) &&
                                                  !habitatConstruction.Contains(costRemovedBufferToken) &&
                                                  !habitatConstruction.Contains(costItemBufferToken) &&
                                                  MethodContains(habitatConstruction, "HasBuildResources", "stackalloc int[MaxCostCapacity]") &&
                                                  MethodContains(habitatConstruction, "ConsumeBuildResources", "stackalloc int[MaxCostCapacity]");
            bool groupedBuildCostResourceRows = MethodContains(habitatConstruction, "PrepareCostBuffers", "FindCostGroupIndex(costHashes") &&
                                                MethodContains(habitatConstruction, "PrepareCostBuffers", "TryAccumulateCostAmount(costRemaining") &&
                                                MethodContains(habitatConstruction, "PrepareCostBuffers", "preparedCount >= costHashes.Length") &&
                                                MethodContains(habitatConstruction, "TryAccumulateCostAmount", "int.MaxValue - amount");
            string countTotalToken = "Count" + "Total(";
            bool groupedBuildCostPresentationRows = MethodContains(playerBuilder, "WriteCostDigest", "PrepareBuildCostDigestGroups(") &&
                                                    MethodContains(playerBuilder, "WriteCostDigest", "CountAvailableTotal(costHashes[i])") &&
                                                    !MethodContains(playerBuilder, "WriteCostDigest", countTotalToken) &&
                                                    MethodContains(builderStatusOverlay, "BuildCostSummary", "PrepareBuildCostDigestGroups(") &&
                                                    MethodContains(builderStatusOverlay, "BuildCostSummary", "CountAvailableTotal(costHashes[i])") &&
                                                    !MethodContains(builderStatusOverlay, "BuildCostSummary", countTotalToken) &&
                                                    MethodContains(pdaConstructionTab, "HasCost", "PrepareBuildCostDigestGroups(") &&
                                                    MethodContains(pdaConstructionTab, "HasCost", "CountAvailableTotal(costHashes[i])") &&
                                                    MethodContains(pdaConstructionTab, "TryAppendCostDigest", "PrepareBuildCostDigestGroups(") &&
                                                    MethodContains(pdaConstructionTab, "TryAppendCostDigest", "CountAvailableTotal(costHashes[i])") &&
                                                    MethodContains(pdaConstructionTab, "TryAppendShortCost", "costAmounts[i]") &&
                                                    !MethodContains(pdaConstructionTab, "TryAppendCostDigest", countTotalToken);
            bool noHabitatGameObjectSocketIndexOverload = !habitatConstruction.Contains("IndexSockets(int moduleIndex, GameObject");
            string spawnedModulesInterfaceToken = "IRead" + "OnlyList<GameObject>";
            string playerRuntimeEnsureToken = "PlayerRuntimeContextService." + "EnsureRuntimeInstance";
            string environmentRuntimeEnsureToken = "EnvironmentRuntimeContextService." + "EnsureRuntimeInstance";
            string bootstrapperToken = "Game" + "Bootstrapper";
            string playerTransformFallbackToken = "TryGetCurrent" + "PlayerTransform";
            string hudActiveToken = "HUDNotification." + "TryGetActive";
            string constructionRuntimeToken = "GlobalRegistry." + "ConstructionRuntime";
            string editorTriggerProbe = "On" + "Trigger";
            string editorSphereProbe = "Sphere" + "Collider";
            string editorPhysicsProbe = "Phys" + "ics.";
            string editorSphereOverlapProbe = "Overlap" + "Sphere" + "NonAlloc";
            string editorJointProbe = "Fixed" + "Joint";
            string editorSpawnProbe = "Instan" + "tiate(";
            string editorTeardownProbe = "Dest" + "roy(";
            string editorObjectCreateProbe = "new Game" + "Object";
            bool auditProbeStringsSplit = !builderHolographyTools.Contains(deletedPreviewToken) &&
                                          !builderHolographyTools.Contains(physxOverlapToken) &&
                                          !builderHolographyTools.Contains(setDataToken) &&
                                          !builderHolographyTools.Contains(meshInstancedToken) &&
                                          !builderHolographyTools.Contains(latestVaultToken) &&
                                          !builderHolographyTools.Contains(spawnedModulesInterfaceToken) &&
                                          !builderHolographyTools.Contains(playerRuntimeEnsureToken) &&
                                          !builderHolographyTools.Contains(environmentRuntimeEnsureToken) &&
                                          !builderHolographyTools.Contains(bootstrapperToken) &&
                                          !builderHolographyTools.Contains(playerTransformFallbackToken) &&
                                          !builderHolographyTools.Contains(hudActiveToken) &&
                                          !builderHolographyTools.Contains(constructionRuntimeToken) &&
                                          !builderHolographyTools.Contains(managedGraphListToken) &&
                                          !builderHolographyTools.Contains(managedSocketDictionaryToken) &&
                                          !constructionSocketEditorTools.Contains(editorTriggerProbe) &&
                                          !constructionSocketEditorTools.Contains(editorSphereProbe) &&
                                          !constructionSocketEditorTools.Contains(editorPhysicsProbe) &&
                                          !constructionSocketEditorTools.Contains(editorSphereOverlapProbe) &&
                                          !constructionSocketEditorTools.Contains(physxOverlapToken) &&
                                          !constructionSocketEditorTools.Contains(editorJointProbe) &&
                                          !constructionSocketEditorTools.Contains(editorSpawnProbe) &&
                                          !constructionSocketEditorTools.Contains(editorTeardownProbe) &&
                                          !constructionSocketEditorTools.Contains(editorObjectCreateProbe);
            bool noSpawnedModulesInterfaceValidation = !habitatConstruction.Contains(spawnedModulesInterfaceToken) &&
                                                       !habitatConstruction.Contains("SpawnedModules") &&
                                                       habitatConstruction.Contains("GetSpawnedModuleAt(") &&
                                                       constructionManager.Contains("internal GameObject GetSpawnedModuleAt");
            bool validationResetNonBlocking = !MethodContains(habitatConstruction, "ResetValidation", "CompletePendingValidation") &&
                                              MethodContains(habitatConstruction, "ResetValidation", "_discardValidationResult = true") &&
                                              MethodContains(habitatConstruction, "TryConsumeCompletedValidation", "bool discardResult = _discardValidationResult") &&
                                              MethodContains(habitatConstruction, "CompletePendingValidationForTeardown", "DispatcherJobSwap.TryComplete");
            bool noPlayerBuilderContextOwnerCreation = playerBuilder.Contains("ResolvePlayerRuntimeContext") &&
                                                       playerBuilder.Contains("ResolveEnvironmentRuntimeContext") &&
                                                       !playerBuilder.Contains(playerRuntimeEnsureToken) &&
                                                       !playerBuilder.Contains(environmentRuntimeEnsureToken) &&
                                                       !MethodContains(playerBuilder, "BindRuntimeReferences", "EnsureRuntimeInstance(");
            bool pdaConstructionRegistryColdCached = pdaConstructionTab.Contains("IGlobalRegistryHotSwapListener") &&
                                                     pdaConstructionTab.Contains("CacheRegistryServicesCold") &&
                                                     pdaConstructionTab.Contains("ApplyCachedPlayerContext") &&
                                                     pdaConstructionTab.Contains("ApplyCachedEnvironmentContext") &&
                                                     !MethodContains(pdaConstructionTab, "Tick", "AutoResolve(") &&
                                                     !pdaConstructionTab.Contains(bootstrapperToken) &&
                                                     !pdaConstructionTab.Contains(playerTransformFallbackToken) &&
                                                     !pdaConstructionTab.Contains(hudActiveToken) &&
                                                     !pdaConstructionTab.Contains(constructionRuntimeToken);
            bool noSnapFinalizeSceneTransformApply = !MethodContains(playerBuilder, "TryApplyShinobuVaultSnapResult", "modules[") &&
                                                     !MethodContains(playerBuilder, "TryApplyShinobuVaultSnapResult", ".transform");
            bool indirect = previewBatch.Contains("DrawProceduralIndirect") &&
                            pipePreview.Contains("DrawProceduralIndirect") &&
                            shader.Contains("StructuredBuffer<BuilderGhostStateRaw>");
            bool lockBuffer = (previewBatch.Contains("LockBufferForWrite") || previewBatch.Contains("GraphicsBufferUploadUtility.UploadNativeArray")) &&
                              (pipePreview.Contains("LockBufferForWrite") || pipePreview.Contains("GraphicsBufferUploadUtility.UploadNativeArray"));

            StringBuilder builder = new StringBuilder(1536);
            builder.AppendLine("{");
            AppendBool(builder, "layoutPass", layoutPass, true);
            AppendBool(builder, "noGhostInstantiationInPreview", noGhostInstantiate, true);
            AppendBool(builder, "legacyPreviewScriptRemoved", legacyPreviewScriptRemoved, true);
            AppendBool(builder, "noPlacement" + "GhostPhysxOverlap", noPhysxOverlap, true);
            AppendBool(builder, "noLegacyGhostPrefabAssets", noLegacyGhostPrefabAssets, true);
            AppendBool(builder, "noNonZeroBuildableGhostPrefabReferences", noNonZeroBuildableGhostPrefabReferences, true);
            AppendBool(builder, "noProjectFileLegacy" + "GhostCompileInclude", noProjectFileLegacyGhostCompileInclude, true);
            AppendBool(builder, "noGraphicsBufferSetData", noSetData, true);
            AppendBool(builder, "noVRPipeMeshInstancing", noPipeMeshInstancing, true);
            AppendBool(builder, "noLegacyObjectAlignmentRoute", noObjectAlignmentRoute, true);
            AppendBool(builder, "noRuntimeVaultLatestFallback", noRuntimeVaultLatestFallback, true);
            AppendBool(builder, "noManagedPlacementEventAllocation", noManagedPlacementEventAllocation, true);
            AppendBool(builder, "noBindRuntimeManagerAllocation", noBindRuntimeManagerAllocation, true);
            AppendBool(builder, "targetSocketCommitDirectPath", targetSocketCommitDirectPath, true);
            AppendBool(builder, "noSameFrameSocketSnapReadback", noSameFrameSocketSnapReadback, true);
            AppendBool(builder, "activeBuildReadinessReadOnly", activeBuildReadinessReadOnly, true);
            AppendBool(builder, "activeBuildResourceReadOnly", activeBuildResourceReadOnly, true);
            AppendBool(builder, "noHotVaultEnsureInPreviewBatch", noHotVaultEnsureInPreviewBatch, true);
            AppendBool(builder, "noHotVaultEnsureInPipePreview", noHotVaultEnsureInPipePreview, true);
            AppendBool(builder, "noQualityScaledSocketTruth", noQualityScaledSocketTruth, true);
            AppendBool(builder, "noQualityScaledTerrainProbeTruth", noQualityScaledTerrainProbeTruth, true);
            AppendBool(builder, "socketTruthHelpersMaxOnly", socketTruthHelpersMaxOnly, true);
            AppendBool(builder, "socketTruthHelpersNoQualityParameter", socketTruthHelpersNoQualityParameter, true);
            AppendBool(builder, "noDeletedPreviewLayoutGate", noDeletedPreviewLayoutGate, true);
            AppendBool(builder, "dumpPathsOwnedByShinobu228", dumpPathsOwnedByShinobu228, true);
            AppendBool(builder, "noRecordHolographyTinyJob", noRecordHolographyTinyJob, true);
            AppendBool(builder, "noPreviewFinalizeGraphicsAllocation", noPreviewFinalizeGraphicsAllocation, true);
            AppendBool(builder, "holographyTelemetryHeartbeat", holographyTelemetryHeartbeat, true);
            AppendBool(builder, "habitatIntegrityGraphVaultOwned", habitatIntegrityGraphVaultOwned, true);
            AppendBool(builder, "habitatManagedGraphCollectionsRemoved", habitatManagedGraphCollectionsRemoved, true);
            AppendBool(builder, "noPipeReadCacheMutation", noPipeReadCacheMutation, true);
            AppendBool(builder, "noPipeXrMethodGroupEventSubscription", noPipeXrMethodGroupEventSubscription, true);
            AppendBool(builder, "noPipeLegacyVaultBufferHandle", noPipeLegacyVaultBufferHandle, true);
            AppendBool(builder, "noPipeGlobalSignalsOrigin", noPipeGlobalSignalsOrigin, true);
            AppendBool(builder, "noPipeUnityFrameCount", noPipeUnityFrameCount, true);
            AppendBool(builder, "noPipeManagedPointArrays", noPipeManagedPointArrays, true);
            AppendBool(builder, "noLegacyPreviewMeshFields", noLegacyPreviewMeshFields, true);
            AppendBool(builder, "noHotConstructionRegistryVault", noHotConstructionRegistryVault, true);
            AppendBool(builder, "noPerScheduleIntegrityGraphRebuild", noPerScheduleIntegrityGraphRebuild, true);
            AppendBool(builder, "noHotValidationGraphVaultResize", noHotValidationGraphVaultResize, true);
            AppendBool(builder, "builderUsesReadOnlyVoxelSdf", builderUsesReadOnlyVoxelSdf, true);
            AppendBool(builder, "noManagedInventoryPlacementSnapshot", noManagedInventoryPlacementSnapshot, true);
            AppendBool(builder, "noManagedCostTransactionArrays", noManagedCostTransactionArrays, true);
            AppendBool(builder, "groupedBuildCostResourceRows", groupedBuildCostResourceRows, true);
            AppendBool(builder, "groupedBuildCostPresentationRows", groupedBuildCostPresentationRows, true);
            AppendBool(builder, "noHabitatGameObjectSocketIndexOverload", noHabitatGameObjectSocketIndexOverload, true);
            AppendBool(builder, "noSpawnedModulesInterfaceValidation", noSpawnedModulesInterfaceValidation, true);
            AppendBool(builder, "validationResetNonBlocking", validationResetNonBlocking, true);
            AppendBool(builder, "noPlayerBuilderContextOwnerCreation", noPlayerBuilderContextOwnerCreation, true);
            AppendBool(builder, "pdaConstructionRegistryColdCached", pdaConstructionRegistryColdCached, true);
            AppendBool(builder, "auditProbeStringsSplit", auditProbeStringsSplit, true);
            AppendBool(builder, "noSnapFinalizeSceneTransformApply", noSnapFinalizeSceneTransformApply, true);
            AppendBool(builder, "drawProceduralIndirect", indirect, true);
            AppendBool(builder, "lockBufferUpload", lockBuffer, true);
            builder.Append("  \"builderGhostStateSize\": ").Append(UnsafeUtility.SizeOf<BuilderGhostStateDTO>()).AppendLine(",");
            builder.Append("  \"builderGhostAupOffset\": ").Append(ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition))).AppendLine(",");
            builder.Append("  \"builderGhostAlign\": ").Append(UnsafeUtility.AlignOf<BuilderGhostStateDTO>()).AppendLine();
            builder.AppendLine("}");

            string absolutePath = Path.Combine(root, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            UpsertReportSection(absolutePath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static string Read(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool NoLegacyGhostPrefabAssets(string root)
        {
            string directory = Path.Combine(root, "Assets/_Project/Prefabs/Construction/Ghosts");
            if (!Directory.Exists(directory))
                return true;

            string search = "PFB_" + "Ghost_*.prefab";
            return Directory.GetFiles(directory, search, SearchOption.TopDirectoryOnly).Length == 0;
        }

        private static bool NoNonZeroGhostPrefabRefs(string root)
        {
            string directory = Path.Combine(root, "Assets/_Project");
            if (!Directory.Exists(directory))
                return true;

            string legacyPreviewFieldToken = "ghost" + "Prefab:";
            string zeroReferenceToken = legacyPreviewFieldToken + " {fileID: 0";
            string[] patterns = { "*.asset", "*.prefab", "*.unity" };
            for (int patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
            {
                string[] files = Directory.GetFiles(directory, patterns[patternIndex], SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string text = NormalizeSource(File.ReadAllText(files[i]));
                    int index = text.IndexOf(legacyPreviewFieldToken, StringComparison.Ordinal);
                    while (index >= 0)
                    {
                        int lineEnd = text.IndexOf('\n', index);
                        if (lineEnd < 0)
                            lineEnd = text.Length;

                        string line = text.Substring(index, lineEnd - index);
                        if (!line.Contains(zeroReferenceToken))
                            return false;

                        index = text.IndexOf(legacyPreviewFieldToken, lineEnd, StringComparison.Ordinal);
                    }
                }
            }

            return true;
        }

        private static bool NoRuntimeGhostConsumerRoute(string root)
        {
            string directory = Path.Combine(root, "Assets/_Project/Scripts");
            if (!Directory.Exists(directory))
                return true;

            string legacyPreviewFieldToken = "ghost" + "Prefab";
            string activePreviewFieldToken = "activeBuildable." + legacyPreviewFieldToken;
            string placementGhostToken = "Placement" + "Ghost";
            string prefabGhostToken = "PFB_" + "Ghost";
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsEditorPath(files[i]))
                    continue;

                string text = NormalizeSource(File.ReadAllText(files[i]));
                if (text.Contains(placementGhostToken) ||
                    text.Contains(prefabGhostToken) ||
                    text.Contains(activePreviewFieldToken))
                {
                    return false;
                }

                if (text.Contains(legacyPreviewFieldToken) && ContainsSpawnOrAcquireRoute(text))
                    return false;
            }

            return true;
        }

        private static bool ContainsSpawnOrAcquireRoute(string text)
        {
            string instantiateToken = "Instan" + "tiate(";
            string spawnToken = ".Sp" + "awn(";
            string poolSpawnToken = "pool.Sp" + "awn(";
            return text.Contains(instantiateToken) ||
                   text.Contains(spawnToken) ||
                   text.Contains(poolSpawnToken) ||
                   (text.Contains("Acquire") && text.Contains("Proxy"));
        }

        private static bool ActiveBuildReadinessIsCached(string playerBuilder)
        {
            string source = NormalizeSource(playerBuilder);
            return source.Contains("ActiveBuildReadiness => _cachedBuildReadiness") &&
                   source.Contains("RefreshActiveBuildReadiness()") &&
                   source.Contains("ComputeActiveBuildReadinessSnapshot(") &&
                   !source.Contains("GetActiveBuildReadiness(") &&
                   !MethodContains(source, "ComputeActiveBuildReadinessSnapshot", "UpdatePlacementValidityState(");
        }

        private static bool MethodContains(string source, string methodName, string token)
        {
            int methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
            if (methodIndex < 0)
                return false;

            int bodyStart = source.IndexOf('{', methodIndex);
            if (bodyStart < 0)
                return false;

            int bodyEnd = FindMatchingBrace(source, bodyStart);
            if (bodyEnd <= bodyStart)
                return false;

            return source.IndexOf(token, bodyStart, bodyEnd - bodyStart, StringComparison.Ordinal) >= 0;
        }

        private static bool IsEditorPath(string path)
        {
            string normalized = NormalizeSource(path).Replace('\\', '/');
            return normalized.Contains("/Editor/");
        }

        private static string NormalizeSource(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static bool NoSameFrameSocketSnapReadback(string playerBuilder)
        {
            const string scheduleToken = "_shinobuSocketSnapHandle = selectJob.Schedule";
            const string finalizeToken = "TryFinalizeShinobuSocketSnap(";
            const string cachedReturnToken = "return TryUseCachedShinobuSocketSnap";
            int scheduleIndex = playerBuilder.IndexOf(scheduleToken, StringComparison.Ordinal);
            if (scheduleIndex < 0)
                return false;

            int cachedReturnIndex = playerBuilder.IndexOf(cachedReturnToken, scheduleIndex, StringComparison.Ordinal);
            if (cachedReturnIndex < 0)
                return false;

            int finalizeIndex = playerBuilder.IndexOf(finalizeToken, scheduleIndex, StringComparison.Ordinal);
            return finalizeIndex < 0 || finalizeIndex > cachedReturnIndex;
        }

        private static void AppendBool(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void UpsertReportSection(string absolutePath, string sectionJson)
        {
            if (File.Exists(absolutePath))
            {
                string existing = File.ReadAllText(absolutePath);
                int sectionKey = existing.IndexOf("\"SHINOBU_228\"", StringComparison.Ordinal);
                if (sectionKey >= 0)
                {
                    int objectStart = existing.IndexOf('{', sectionKey);
                    int objectEnd = FindMatchingBrace(existing, objectStart);
                    if (objectStart > 0 && objectEnd > objectStart)
                    {
                        string prefix = existing.Substring(0, objectStart);
                        string suffix = existing.Substring(objectEnd + 1);
                        File.WriteAllText(absolutePath, prefix + sectionJson.TrimEnd() + suffix);
                        return;
                    }

                    WriteSidecarReport(absolutePath, sectionJson);
                    return;
                }

                int rootStart = existing.IndexOf('{');
                int rootEnd = FindMatchingBrace(existing, rootStart);
                bool rootEndsFile = rootEnd > rootStart && existing.Substring(rootEnd + 1).Trim().Length == 0;
                if (rootStart >= 0 && rootEndsFile)
                {
                    string prefix = existing.Substring(0, rootEnd).TrimEnd();
                    string suffix = existing.Substring(rootEnd);
                    bool hasExistingProperties = prefix.Length > 0 && prefix[prefix.Length - 1] != '{';
                    StringBuilder merged = new StringBuilder(existing.Length + sectionJson.Length + 32);
                    merged.Append(prefix);
                    if (hasExistingProperties)
                        merged.Append(',');
                    merged.AppendLine();
                    merged.Append("  \"SHINOBU_228\": ").Append(sectionJson.TrimEnd()).AppendLine();
                    merged.Append(suffix);
                    File.WriteAllText(absolutePath, merged.ToString());
                    return;
                }

                WriteSidecarReport(absolutePath, sectionJson);
                return;
            }

            File.WriteAllText(absolutePath, "{\n  \"SHINOBU_228\": " + sectionJson.TrimEnd() + "\n}\n");
        }

        private static void WriteSidecarReport(string absolutePath, string sectionJson)
        {
            string sidecarPath = Path.ChangeExtension(absolutePath, ".SHINOBU_228.json");
            File.WriteAllText(sidecarPath, "{\n  \"SHINOBU_228\": " + sectionJson.TrimEnd() + "\n}\n");
            Debug.LogError("[SHINOBU_228] MEMORY_OPTIMIZATION_REPORT.json merge failed; wrote sidecar report without overwriting shared report: " + sidecarPath);
        }

        private static int FindMatchingBrace(string text, int objectStart)
        {
            if (string.IsNullOrEmpty(text) || objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }
    }

    public static class BuilderHolographyProfileCsv
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint MagneticRadiusHash = 0x1670F141u;
        private const uint GridSnapToleranceHash = 0x77770C0Cu;
        private const uint GlobalQualityWeightHash = 0xC74CE627u;

        public static bool TryIngest(ReadOnlySpan<byte> bytes, NativeArray<ConstructionSocketTuningDTO> tuning)
        {
            if (bytes.Length <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            ConstructionSocketTuningDTO dto = tuning[0];
            int cursor = 0;
            bool any = false;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, cursor - lineStart);
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                int separator = FindSeparator(line);
                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                uint keyHash = HashAsciiLower(Trim(line.Slice(0, separator)));
                if (!TryParseFloat(Trim(line.Slice(separator + 1)), out float value))
                    continue;

                switch (keyHash)
                {
                    case MagneticRadiusHash:
                        dto.SnappingRadius = math.max(0.001f, value);
                        any = true;
                        break;
                    case GridSnapToleranceHash:
                        dto.DearLieShrinkMeters = math.clamp(value, 0f, 1f);
                        any = true;
                        break;
                    case GlobalQualityWeightHash:
                        dto.GlobalQualityWeight = math.saturate(value);
                        any = true;
                        break;
                }
            }

            if (any)
                tuning[0] = dto;

            return any;
        }

        private static int FindSeparator(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=' || b == (byte)';')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhitespace(byte b)
        {
            return b == (byte)' ' || b == (byte)'\t';
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> span)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            if (span.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < span.Length)
            {
                byte b = span[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = (integer * 10f) + (b - (byte)'0');
                any = true;
                index++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length)
                {
                    byte b = span[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    scale *= 0.1f;
                    fraction += (b - (byte)'0') * scale;
                    any = true;
                    index++;
                }
            }

            if (!any)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }
    }
}
#endif
