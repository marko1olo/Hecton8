#if UNITY_EDITOR
namespace Hecton8.Tools.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public static class Cutter_Raycast_Inquisition
    {
        private const string ReportFileName = "CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_225.json";

        [MenuItem("Hecton8/Tools/Cutter Raycast Inquisition")]
        public static void RunMenu()
        {
            string reportPath = RunToFile();
            Debug.Log("[SHINOBU_225] Cutter raycast inquisition wrote " + reportPath);
        }

        public static string RunToFile()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return string.Empty;

            string sourceRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            int cutterSyncRaycasts = 0;
            int cutterParticleSystems = 0;
            int cutterInstantiateSites = 0;
            int cutterMeshMutationSites = 0;
            int dodRequestDefinitions = 0;
            int dodRequestMetaDefinitions = 0;
            int raycastCommandBatchSites = 0;
            int noAliasAttributeHits = 0;
            int hotResolveNoAcquireHits = 0;
            int shaderLieDtos = 0;
            int gpuSparkSignals = 0;
            int burstWorkEstimateHits = 0;
            int smoothQualityHits = 0;
            int pureReadAccessorHits = 0;
            int legacyGlobalSignalsPublishSites = 0;
            int typedSignalBusSites = 0;
            int unityTimeSites = 0;
            int dispatcherFrameSnapshotSites = 0;
            int globalRegistryServiceReadSites = 0;
            int legacyStringBridgeHits = 0;
            int laserCutterNewStringBridgeSites = 0;
            int hotManagedIterationSites = 0;
            int hotManagedTextAllocationSites = 0;
            int completedFenceFinalizeSites = 0;
            int liveDiagnosisReadSites = 0;
            int managedDiagnosisSeveritySites = 0;
            int blackBoxBinaryWriterSites = 0;
            int rawBlackBoxSpanWriterHits = 0;
            int prematureAupFloatHashSites = 0;
            int directPowerRuntimeDependencySites = 0;
            int directLogisticsGridRuntimeDependencySites = 0;
            int wfcRuntimeDataVaultRegistrySites = 0;
            int wfcRuntimePropertyAccessorSites = 0;
            int cutterPropertyAccessorSites = 0;
            int laserEventLegacyGlobalInitSites = 0;
            int laserEventHotEnsureSites = 0;
            int sargassumRuntimeRegistrationSites = 0;
            int dodHotSchedulerEnsureSites = 0;
            int laserHotComponentDiscoverySites = 0;
            int wfcRouteSnapshotScanSites = 0;
            int dodRuntimeDataVaultRegistrySites = 0;
            int explicitSecondaryDiagnosisComponentLookupSites = 0;
            int originBridgeReadSites = 0;
            int dodRuntimeDirectOriginSites = 0;
            int dodDebugGizmoDirectOriginSites = 0;
            int dodRuntimeOriginZeroFallbackSites = 0;
            int dodRuntimeOriginFailClosedSites = 0;
            int mockForceCompleteSites = 0;
            int mockForceCompleteFenceHits = 0;

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.EndsWith("/Tools/Editor/Cutter_Raycast_Inquisition.cs", StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(file);
                bool cutterRelated = normalized.EndsWith("/LaserCutter.cs", StringComparison.Ordinal) ||
                                     normalized.IndexOf("/Tools/LaserCutterDod", StringComparison.Ordinal) >= 0 ||
                                     normalized.EndsWith("/Tools/WfcLaserCutRuntime.cs", StringComparison.Ordinal) ||
                                     normalized.EndsWith("/Tools/Editor/LaserCutterPhysicsTunerWindow.cs", StringComparison.Ordinal) ||
                                     normalized.EndsWith("/Gameplay/SealedDoor.cs", StringComparison.Ordinal) ||
                                     normalized.EndsWith("/Gameplay/SargassumCutResponder.cs", StringComparison.Ordinal);
                if (!cutterRelated)
                    continue;

                cutterSyncRaycasts += Count(text, "Physics.Raycast(") + Count(text, "Physics.RaycastAll(") + Count(text, "Physics.RaycastNonAlloc(");
                cutterParticleSystems += Count(text, "ParticleSystem");
                cutterInstantiateSites += Count(text, "Instantiate(");
                cutterMeshMutationSites += Count(text, ".vertices") + Count(text, "SetVertices(") + Count(text, "RecalculateNormals(");
                dodRequestDefinitions += Count(text, "LaserCutRequestDTO");
                dodRequestMetaDefinitions += Count(text, "LaserCutRequestMetaDTO");
                raycastCommandBatchSites += Count(text, "RaycastCommand.ScheduleBatch");
                noAliasAttributeHits += Count(text, "NoAlias");
                hotResolveNoAcquireHits += Count(text, "allowAcquire: false");
                shaderLieDtos += Count(text, "LaserCutDeformationStateDTO") + Count(text, "LaserCutGlowDecalRequestDTO");
                gpuSparkSignals += Count(text, "DebrisSpawnSignal") + Count(text, "VfxSparkRequestSignal") + Count(text, "LaserCutImpactVfxDTO");
                burstWorkEstimateHits += Count(text, "BurstWorkEstimateMicros");
                smoothQualityHits += Count(text, "math.smoothstep");
                originBridgeReadSites += Count(text, "GlobalSignals.CurrentRuntimeOriginAup");
                legacyGlobalSignalsPublishSites += Count(text, "GlobalSignals.Publish");
                typedSignalBusSites += Count(text, "SignalBus<");
                unityTimeSites += Count(text, "Time.time") +
                                  Count(text, "Time.frameCount") +
                                  Count(text, "Time.deltaTime") +
                                  Count(text, "Time.fixedDeltaTime");
                dispatcherFrameSnapshotSites += Count(text, "TimeSliceScheduler.CurrentFrameId");
                globalRegistryServiceReadSites += Count(text, "GlobalRegistry.Audio") +
                                                  Count(text, "GlobalRegistry.Input") +
                                                  Count(text, "GlobalRegistry.InteractionSignals") +
                                                  Count(text, "GlobalRegistry.HabitatDeconstruction") +
                                                  Count(text, "GlobalRegistry.SargassumCut") +
                                                  Count(text, "GlobalRegistry.Localization");
                legacyStringBridgeHits += Count(text, "BuildLegacyOperational") + Count(text, "new string(buffer.Buffer");
                completedFenceFinalizeSites += Count(text, "TryFinalizeCompleted");
                liveDiagnosisReadSites += Count(text, "ReadDiagnosisNow");
                managedDiagnosisSeveritySites += Count(text, "public string severity") +
                                                 Count(text, "out string severity") +
                                                 Count(text, "_cachedDiagnosis.severity");
                blackBoxBinaryWriterSites += Count(text, "BinaryWriter");
                rawBlackBoxSpanWriterHits += Count(text, "NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry)") + Count(text, "new ReadOnlySpan<byte>(source");
                prematureAupFloatHashSites += Count(text, "(float)hitAup.") + Count(text, "math.asint((float)hitAup");
                directPowerRuntimeDependencySites += Count(text, "using Hecton8.Power") +
                                                     Count(text, "WfcOutpostGridRegistry") +
                                                     Count(text, "WfcOutpostGridLease");
                directLogisticsGridRuntimeDependencySites += Count(text, "using Hecton8.Logistics.Grid.Contracts") +
                                                             Count(text, "WfcOutpostGridConstants");
                if (normalized.EndsWith("/Tools/WfcLaserCutRuntime.cs", StringComparison.Ordinal))
                {
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public static bool TryApplyDoorCut");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public static bool TryApplyDoorCut");
                    wfcRuntimeDataVaultRegistrySites += Count(text, "GlobalRegistry.DataVault");
                    wfcRuntimePropertyAccessorSites += Count(text, "public static uint DoorsCutCount =>");
                    wfcRouteSnapshotScanSites += ContainsWithinMethod(text, "public static bool TryApplyDoorCut", "GetFrameSnapshot(") ? 1 : 0;
                    wfcRouteSnapshotScanSites += ContainsWithinMethod(text, "public static bool TryApplyDoorCut", "RefreshActiveGridFromSignals(") ? 1 : 0;
                    wfcRouteSnapshotScanSites += ContainsWithinMethod(text, "public static bool TryApplyDoorCut", "RefreshSystemStressFromSignals(") ? 1 : 0;
                }
                if (normalized.EndsWith("/LaserCutter.cs", StringComparison.Ordinal))
                {
                    laserCutterNewStringBridgeSites += Count(text, "new string(buffer.Buffer");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public override void UsePrimary");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public override void ToolTick");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "private void ApplyCutDamage");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "private bool TryApplyWfcDoorCut");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "private void ProcessDeconstructMode");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "private void BuildDiagnosisFromHit");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public override void UsePrimary");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public override void ToolTick");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "private void ApplyCutDamage");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "private bool TryApplyWfcDoorCut");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "private void ProcessDeconstructMode");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "private void BuildDiagnosisFromHit");
                    cutterPropertyAccessorSites += Count(text, "public float HeatLevel =>");
                    cutterPropertyAccessorSites += Count(text, "public bool IsOverheated =>");
                    cutterPropertyAccessorSites += Count(text, "public static int PendingCount =>");
                    cutterPropertyAccessorSites += Count(text, "public int Count => _count");
                    laserEventLegacyGlobalInitSites += Count(text, "GlobalSignals.InitializeAllQueues");
                    laserEventHotEnsureSites += ContainsWithinMethod(text, "private static void Enqueue", "EnsureInitialized(") ? 1 : 0;
                    laserHotComponentDiscoverySites += ContainsWithinMethod(text, "private bool TryApplyWfcDoorCut", "TryGetComponent(") ? 1 : 0;
                    laserHotComponentDiscoverySites += ContainsWithinMethod(text, "private bool TryApplyWfcDoorCut", "GetComponentInParent<SealedDoor>") ? 1 : 0;
                    laserHotComponentDiscoverySites += ContainsWithinMethod(text, "private void ProcessDeconstructMode", "TryGetComponent(") ? 1 : 0;
                    laserHotComponentDiscoverySites += ContainsWithinMethod(text, "private void ProcessDeconstructMode", "GetComponentInParent<BaseModule>") ? 1 : 0;
                    explicitSecondaryDiagnosisComponentLookupSites += ContainsWithinMethod(text, "private void BuildDiagnosisFromHit", "TryGetComponent(") ? 1 : 0;
                    explicitSecondaryDiagnosisComponentLookupSites += ContainsWithinMethod(text, "private void BuildDiagnosisFromHit", "GetComponentInParent") ? 1 : 0;
                }
                if (normalized.EndsWith("/Gameplay/SargassumCutResponder.cs", StringComparison.Ordinal))
                {
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public void RegisterCut");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public void RegisterCut");
                    sargassumRuntimeRegistrationSites += Count(text, "GlobalRegistry.Dispatcher") +
                                                          Count(text, "TryRegisterUpdatable") +
                                                          Count(text, "UnregisterUpdatable") +
                                                          Count(text, "IUpdatable") +
                                                          Count(text, "ITickable");
                }
                if (normalized.EndsWith("/Gameplay/SealedDoor.cs", StringComparison.Ordinal))
                {
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public void ApplyWfcOutpostLaserCutProgress");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public void ApplyCutDamage");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public void ApplyCutting(float amount, Vector3 hitPoint)");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public void ApplyWfcOutpostLaserCutProgress");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public void ApplyCutDamage");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public void ApplyCutting(float amount, Vector3 hitPoint)");
                    cutterPropertyAccessorSites += Count(text, "public DoorState State =>");
                    cutterPropertyAccessorSites += Count(text, "public float CurrentProgress =>");
                    cutterPropertyAccessorSites += Count(text, "public float ProgressNormalized =>");
                    cutterPropertyAccessorSites += Count(text, "public bool IsOpened =>");
                    cutterPropertyAccessorSites += Count(text, "public bool CanBeCut =>");
                }
                if (normalized.EndsWith("/Tools/LaserCutterDodRuntime.cs", StringComparison.Ordinal))
                {
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public static bool TryScheduleRaycastBatch");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public static bool TryCompleteScheduledRaycastsAndEvaluate");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "private static bool TryFinalizeScheduledEvaluation");
                    hotManagedIterationSites += CountManagedIterationWithinMethod(text, "public static void StageGpuSparkSignal");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public static bool TryScheduleRaycastBatch");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public static bool TryCompleteScheduledRaycastsAndEvaluate");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "private static bool TryFinalizeScheduledEvaluation");
                    hotManagedTextAllocationSites += CountManagedTextAllocationWithinMethod(text, "public static void StageGpuSparkSignal");
                    dodHotSchedulerEnsureSites += ContainsWithinMethod(text, "public static bool TryScheduleRaycastBatch", "EnsureInitialized(") ? 1 : 0;
                    dodRuntimeDataVaultRegistrySites += Count(text, "GlobalRegistry.DataVault");
                    dodRuntimeDirectOriginSites += Count(text, "HectonFloatingOrigin.CurrentTotalOffsetDouble");
                    dodRuntimeOriginZeroFallbackSites += Count(text, "private static double3 ReadPresentationOriginAup") + Count(text, "ReadPresentationOriginAup()");
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "public static bool TryScheduleRaycastBatch", "TryReadPresentationOriginAup") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "public static bool TryScheduleRaycastBatch", "SuppressQueuedRequests(frame)") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "public static bool TryCompleteScheduledRaycastsAndEvaluate", "TryReadScheduledRaycastPresentationOrigin") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "public static bool TryCompleteScheduledRaycastsAndEvaluate", "SuppressQueuedRequests(ResolveCurrentFrameId())") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "private static bool TryFinalizeScheduledEvaluation", "TryReadScheduledEvaluationPresentationOrigin") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "private static bool TryFinalizeScheduledEvaluation", "SuppressQueuedRequests(ResolveCurrentFrameId())") ? 1 : 0;
                    dodRuntimeOriginFailClosedSites += ContainsWithinMethod(text, "public static void StageGpuSparkSignal", "TryReadPresentationOriginAup") ? 1 : 0;
                    mockForceCompleteSites += ContainsWithinMethod(text, "public static bool GenerateMockCutterTriggers", "forceComplete: true") ? 1 : 0;
                    mockForceCompleteFenceHits += ContainsWithinMethod(text, "public static bool GenerateMockCutterTriggers", "#if UNITY_EDITOR || DEVELOPMENT_BUILD") ? 1 : 0;
                    pureReadAccessorHits += IsPureNoAcquireReader(text, "TryGetLatestTelemetry") ? 1 : 0;
                    pureReadAccessorHits += IsPureNoAcquireReader(text, "TryGetTuning") ? 1 : 0;
                    pureReadAccessorHits += IsPureNoAcquireReader(text, "TryGetPresentationOriginForGizmo") ? 1 : 0;
                    pureReadAccessorHits += IsPureNoAcquireReader(text, "TryGetRequestForGizmo") ? 1 : 0;
                    pureReadAccessorHits += IsPureNoAcquireReader(text, "TryGetHitForGizmo") ? 1 : 0;
                }
                if (normalized.EndsWith("/Tools/LaserCutterDodDebugGizmo.cs", StringComparison.Ordinal))
                {
                    dodDebugGizmoDirectOriginSites += Count(text, "HectonFloatingOrigin.CurrentTotalOffsetDouble");
                }
            }

            bool layoutOk = LaserCutterDodLayoutValidator.Validate(out uint layoutFaults);
            string reportDirectory = Path.Combine(projectRoot.FullName, "Docs", "Reports");
            Directory.CreateDirectory(reportDirectory);
            string reportPath = Path.Combine(reportDirectory, ReportFileName);
            File.WriteAllText(
                reportPath,
                BuildJson(
                    cutterSyncRaycasts,
                    cutterParticleSystems,
                    cutterInstantiateSites,
                    cutterMeshMutationSites,
                    dodRequestDefinitions,
                    dodRequestMetaDefinitions,
                    raycastCommandBatchSites,
                    noAliasAttributeHits,
                    hotResolveNoAcquireHits,
                    shaderLieDtos,
                    gpuSparkSignals,
                    burstWorkEstimateHits,
                    smoothQualityHits,
                    pureReadAccessorHits,
                    legacyGlobalSignalsPublishSites,
                    typedSignalBusSites,
                    unityTimeSites,
                    dispatcherFrameSnapshotSites,
                    globalRegistryServiceReadSites,
                    legacyStringBridgeHits,
                    completedFenceFinalizeSites,
                    liveDiagnosisReadSites,
                    managedDiagnosisSeveritySites,
                    blackBoxBinaryWriterSites,
                    rawBlackBoxSpanWriterHits,
                    prematureAupFloatHashSites,
                    directPowerRuntimeDependencySites,
                    directLogisticsGridRuntimeDependencySites,
                    wfcRuntimeDataVaultRegistrySites,
                    wfcRuntimePropertyAccessorSites,
                    cutterPropertyAccessorSites,
                    laserEventLegacyGlobalInitSites,
                    laserEventHotEnsureSites,
                    sargassumRuntimeRegistrationSites,
                    dodHotSchedulerEnsureSites,
                    laserHotComponentDiscoverySites,
                    wfcRouteSnapshotScanSites,
                    dodRuntimeDataVaultRegistrySites,
                    explicitSecondaryDiagnosisComponentLookupSites,
                    originBridgeReadSites,
                    dodRuntimeDirectOriginSites,
                    dodDebugGizmoDirectOriginSites,
                    dodRuntimeOriginZeroFallbackSites,
                    dodRuntimeOriginFailClosedSites,
                    laserCutterNewStringBridgeSites,
                    hotManagedIterationSites,
                    hotManagedTextAllocationSites,
                    mockForceCompleteSites,
                    mockForceCompleteFenceHits,
                    layoutOk,
                    layoutFaults),
                Encoding.UTF8);
            return reportPath;
        }

        private static string BuildJson(
            int cutterSyncRaycasts,
            int cutterParticleSystems,
            int cutterInstantiateSites,
            int cutterMeshMutationSites,
            int dodRequestDefinitions,
            int dodRequestMetaDefinitions,
            int raycastCommandBatchSites,
            int noAliasAttributeHits,
            int hotResolveNoAcquireHits,
            int shaderLieDtos,
            int gpuSparkSignals,
            int burstWorkEstimateHits,
            int smoothQualityHits,
            int pureReadAccessorHits,
            int legacyGlobalSignalsPublishSites,
            int typedSignalBusSites,
            int unityTimeSites,
            int dispatcherFrameSnapshotSites,
            int globalRegistryServiceReadSites,
            int legacyStringBridgeHits,
            int completedFenceFinalizeSites,
            int liveDiagnosisReadSites,
            int managedDiagnosisSeveritySites,
            int blackBoxBinaryWriterSites,
            int rawBlackBoxSpanWriterHits,
            int prematureAupFloatHashSites,
            int directPowerRuntimeDependencySites,
            int directLogisticsGridRuntimeDependencySites,
            int wfcRuntimeDataVaultRegistrySites,
            int wfcRuntimePropertyAccessorSites,
            int cutterPropertyAccessorSites,
            int laserEventLegacyGlobalInitSites,
            int laserEventHotEnsureSites,
            int sargassumRuntimeRegistrationSites,
            int dodHotSchedulerEnsureSites,
            int laserHotComponentDiscoverySites,
            int wfcRouteSnapshotScanSites,
            int dodRuntimeDataVaultRegistrySites,
            int explicitSecondaryDiagnosisComponentLookupSites,
            int originBridgeReadSites,
            int dodRuntimeDirectOriginSites,
            int dodDebugGizmoDirectOriginSites,
            int dodRuntimeOriginZeroFallbackSites,
            int dodRuntimeOriginFailClosedSites,
            int laserCutterNewStringBridgeSites,
            int hotManagedIterationSites,
            int hotManagedTextAllocationSites,
            int mockForceCompleteSites,
            int mockForceCompleteFenceHits,
            bool layoutOk,
            uint layoutFaults)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_225\",");
            builder.AppendLine("  \"scanner\": \"Cutter_Raycast_Inquisition\",");
            builder.AppendLine("  \"generated_utc\": \"" + DateTime.UtcNow.ToString("O") + "\",");
            builder.AppendLine("  \"cutter_sync_raycast_sites\": " + cutterSyncRaycasts + ",");
            builder.AppendLine("  \"cutter_particle_system_references\": " + cutterParticleSystems + ",");
            builder.AppendLine("  \"cutter_instantiate_sites\": " + cutterInstantiateSites + ",");
            builder.AppendLine("  \"cutter_mesh_mutation_sites\": " + cutterMeshMutationSites + ",");
            builder.AppendLine("  \"dod_request_definition_hits\": " + dodRequestDefinitions + ",");
            builder.AppendLine("  \"dod_request_meta_definition_hits\": " + dodRequestMetaDefinitions + ",");
            builder.AppendLine("  \"raycast_command_batch_sites\": " + raycastCommandBatchSites + ",");
            builder.AppendLine("  \"noalias_attribute_hits\": " + noAliasAttributeHits + ",");
            builder.AppendLine("  \"hot_resolve_allow_acquire_false_hits\": " + hotResolveNoAcquireHits + ",");
            builder.AppendLine("  \"shader_lie_dto_hits\": " + shaderLieDtos + ",");
            builder.AppendLine("  \"gpu_spark_signal_hits\": " + gpuSparkSignals + ",");
            builder.AppendLine("  \"burst_work_estimate_hits\": " + burstWorkEstimateHits + ",");
            builder.AppendLine("  \"smooth_quality_curve_hits\": " + smoothQualityHits + ",");
            builder.AppendLine("  \"pure_read_accessor_count\": " + pureReadAccessorHits + ",");
            builder.AppendLine("  \"legacy_global_signals_publish_sites\": " + legacyGlobalSignalsPublishSites + ",");
            builder.AppendLine("  \"typed_signal_bus_sites\": " + typedSignalBusSites + ",");
            builder.AppendLine("  \"unity_time_sites\": " + unityTimeSites + ",");
            builder.AppendLine("  \"dispatcher_frame_snapshot_sites\": " + dispatcherFrameSnapshotSites + ",");
            builder.AppendLine("  \"global_registry_service_read_sites\": " + globalRegistryServiceReadSites + ",");
            builder.AppendLine("  \"legacy_string_bridge_hits\": " + legacyStringBridgeHits + ",");
            builder.AppendLine("  \"completed_fence_finalize_sites\": " + completedFenceFinalizeSites + ",");
            builder.AppendLine("  \"live_diagnosis_read_sites\": " + liveDiagnosisReadSites + ",");
            builder.AppendLine("  \"managed_diagnosis_severity_sites\": " + managedDiagnosisSeveritySites + ",");
            builder.AppendLine("  \"blackbox_binary_writer_sites\": " + blackBoxBinaryWriterSites + ",");
            builder.AppendLine("  \"raw_blackbox_span_writer_hits\": " + rawBlackBoxSpanWriterHits + ",");
            builder.AppendLine("  \"premature_aup_float_hash_sites\": " + prematureAupFloatHashSites + ",");
            builder.AppendLine("  \"direct_power_runtime_dependency_sites\": " + directPowerRuntimeDependencySites + ",");
            builder.AppendLine("  \"direct_logistics_grid_runtime_dependency_sites\": " + directLogisticsGridRuntimeDependencySites + ",");
            builder.AppendLine("  \"wfc_runtime_datavault_registry_sites\": " + wfcRuntimeDataVaultRegistrySites + ",");
            builder.AppendLine("  \"wfc_runtime_property_accessor_sites\": " + wfcRuntimePropertyAccessorSites + ",");
            builder.AppendLine("  \"cutter_property_accessor_sites\": " + cutterPropertyAccessorSites + ",");
            builder.AppendLine("  \"laser_event_legacy_global_init_sites\": " + laserEventLegacyGlobalInitSites + ",");
            builder.AppendLine("  \"laser_event_hot_ensure_sites\": " + laserEventHotEnsureSites + ",");
            builder.AppendLine("  \"sargassum_runtime_registration_sites\": " + sargassumRuntimeRegistrationSites + ",");
            builder.AppendLine("  \"dod_hot_scheduler_ensure_sites\": " + dodHotSchedulerEnsureSites + ",");
            builder.AppendLine("  \"laser_hot_component_discovery_sites\": " + laserHotComponentDiscoverySites + ",");
            builder.AppendLine("  \"wfc_route_snapshot_scan_sites\": " + wfcRouteSnapshotScanSites + ",");
            builder.AppendLine("  \"dod_runtime_datavault_registry_sites\": " + dodRuntimeDataVaultRegistrySites + ",");
            builder.AppendLine("  \"explicit_secondary_diagnosis_component_lookup_sites\": " + explicitSecondaryDiagnosisComponentLookupSites + ",");
            builder.AppendLine("  \"origin_bridge_read_sites\": " + originBridgeReadSites + ",");
            builder.AppendLine("  \"dod_runtime_direct_origin_sites\": " + dodRuntimeDirectOriginSites + ",");
            builder.AppendLine("  \"dod_debug_gizmo_direct_origin_sites\": " + dodDebugGizmoDirectOriginSites + ",");
            builder.AppendLine("  \"dod_runtime_origin_zero_fallback_sites\": " + dodRuntimeOriginZeroFallbackSites + ",");
            builder.AppendLine("  \"dod_runtime_origin_fail_closed_sites\": " + dodRuntimeOriginFailClosedSites + ",");
            builder.AppendLine("  \"laser_cutter_new_string_bridge_sites\": " + laserCutterNewStringBridgeSites + ",");
            builder.AppendLine("  \"hot_managed_iteration_sites\": " + hotManagedIterationSites + ",");
            builder.AppendLine("  \"hot_managed_text_allocation_sites\": " + hotManagedTextAllocationSites + ",");
            builder.AppendLine("  \"mock_force_complete_sites\": " + mockForceCompleteSites + ",");
            builder.AppendLine("  \"mock_force_complete_compile_fence_hits\": " + mockForceCompleteFenceHits + ",");
            builder.AppendLine("  \"pure_read_accessors\": \"TryGetLatestTelemetry,TryGetTuning,TryGetPresentationOriginForGizmo,TryGetRequestForGizmo,TryGetHitForGizmo contain no EnsureInitialized and no allowAcquire:true\",");
            builder.AppendLine("  \"dod_scheduler_cold_boot_boundary\": \"TryScheduleRaycastBatch must fail closed when IDataVault was not cold-bound; it must not call EnsureInitialized from the scheduling route\",");
            builder.AppendLine("  \"laser_target_registry_boundary\": \"TryApplyWfcDoorCut and ProcessDeconstructMode resolve collider ownership through LaserCutterTargetRegistry, not TryGetComponent/GetComponentInParent from the beam route\",");
            builder.AppendLine("  \"explicit_secondary_diagnosis_boundary\": \"BuildDiagnosisFromHit resolves salvage module ownership through LaserCutterTargetRegistry and does not run TryGetComponent/GetComponentInParent\",");
            builder.AppendLine("  \"wfc_owner_phase_context_boundary\": \"WFC grid/stress SignalBus snapshots are refreshed by owner phase before cutter hit application; TryApplyDoorCut reads cached context only\",");
            builder.AppendLine("  \"laser_cut_request_layout_contract\": \"64 bytes offsets 0,24,36,40,44,48 with explicit padding at 52,56,60\",");
            builder.AppendLine("  \"laser_cut_request_meta_layout_contract\": \"64 bytes offsets Frame=0,Flags=4,RequestSequence=8,CooldownUntilFrame=12\",");
            builder.AppendLine("  \"gpu_spark_count_curve\": \"math.smoothstep(GlobalQualityWeight) over tuning LowSparkCount=0 to UltraSparkCount=500\",");
            builder.AppendLine("  \"spark_signal_source\": \"post-evaluation signals forward LaserCutImpactVfxDTO.SparkCount directly; live helper computes direct tool presentation quantity from the same tuning row\",");
            builder.AppendLine("  \"telemetry_tail_contract\": \"BatteryWatts@120,BurstWorkEstimateMicros@124\",");
            builder.AppendLine("  \"tuning_runtime_consumed\": \"DentRadiusMinMeters,DentRadiusMaxMeters,GlowLifetimeSeconds,BatteryWattsAtPowerOne,SparkIntensityScale,LowSparkCount,UltraSparkCount\",");
            builder.AppendLine("  \"cold_boot_bind_scope\": \"EnsureInitialized binds scheduler/result/telemetry/request/meta lanes and seeds tuning before hot no-acquire scheduling\",");
            builder.AppendLine("  \"time_authority\": \"runtime and editor mock frame ids read TimeSliceScheduler.CurrentFrameId with cold fallback frame=1; Unity Time.* count must stay zero in scanned cutter route\",");
            builder.AppendLine("  \"legacy_signal_boundary\": \"GlobalSignals.Publish count must stay zero; hot payloads use typed SignalBus<T> lanes\",");
            builder.AppendLine("  \"global_registry_boundary\": \"service read sites are cold dependency caches only; hot methods consume cached interfaces and no-acquire Vault handles\",");
            builder.AppendLine("  \"job_finalize_boundary\": \"TryFinalizeCompleted sites are dispatcher/fence finalizers after IsCompleted, not arbitrary mid-frame Complete calls\",");
            builder.AppendLine("  \"legacy_string_boundary\": \"BuildLegacyOperational* remains a cold base-tool compatibility bridge; HUD/PDA path uses fixed-buffer writers\",");
            builder.AppendLine("  \"diagnosis_read_boundary\": \"operational summary/directive writers must not call live raycast/component diagnosis; explicit secondary fire is the owner action\",");
            builder.AppendLine("  \"blackbox_dump_boundary\": \"fault dump must write raw LaserCutTelemetryEntry spans, not BinaryWriter field loops\",");
            builder.AppendLine("  \"aup_hash_boundary\": \"state/material proof hashes must mix double AUP bits or local AUP deltas, never cast absolute hitAup to float\",");
            builder.AppendLine("  \"wfc_compile_wall_boundary\": \"WfcLaserCutRuntime must not import Hecton8.Power or Hecton8.Logistics.Grid.Contracts, must not call WfcOutpostGridRegistry, and must not query GlobalRegistry.DataVault from the WFC hot route\",");
            builder.AppendLine("  \"wfc_property_boundary\": \"WfcLaserCutRuntime must not expose dead static property accessors for cutter-adjacent runtime counters; telemetry rows are the proof route\",");
            builder.AppendLine("  \"cutter_property_boundary\": \"LaserCutter and SealedDoor cutter-adjacent runtime state must use explicit Read* methods or owner-private math, not public property facades\",");
            builder.AppendLine("  \"event_lane_cold_boot_boundary\": \"LaserCutterEvents may configure SignalBus only from cold listener/source registration; Enqueue and FlushPending must not call EnsureInitialized or GlobalSignals.InitializeAllQueues\",");
            builder.AppendLine("  \"sargassum_responder_boundary\": \"SargassumCutResponder must not register IUpdatable from the cut impulse; the cut mask owner is SargassumCutManager and debris cooldown is frame-stamped\",");
            builder.AppendLine("  \"dod_runtime_boot_boundary\": \"LaserCutterDodRuntime must not query GlobalRegistry.DataVault; boot receives explicit IDataVault from LaserCutter lifecycle or editor facade\",");
            builder.AppendLine("  \"aup_origin_boundary\": \"scanned cutter routes must not call GlobalSignals.CurrentRuntimeOriginAup; owner phases cache HectonFloatingOrigin.CurrentTotalOffsetDouble into local AUP snapshots\",");
            builder.AppendLine("  \"dod_runtime_origin_boundary\": \"LaserCutterDodRuntime must consume cached presentation origin snapshots, carry scheduled batch origins, fail closed on missing origin, and must not read HectonFloatingOrigin.CurrentTotalOffsetDouble directly\",");
            builder.AppendLine("  \"dod_debug_gizmo_origin_boundary\": \"LaserCutterDodDebugGizmo must consume TryGetPresentationOriginForGizmo and fail closed when the owner-phase snapshot is absent; it must not read HectonFloatingOrigin.CurrentTotalOffsetDouble directly\",");
            builder.AppendLine("  \"hot_managed_code_boundary\": \"UsePrimary/ToolTick/cut application/WFC hit/DOD schedule-evaluate-VFX routes must not contain foreach, LINQ, string.Format, string interpolation, or new string allocation\",");
            builder.AppendLine("  \"mock_generator_boundary\": \"GenerateMockCutterTriggers may force-complete only inside UNITY_EDITOR || DEVELOPMENT_BUILD because it is a deterministic editor/CI stress facade, not a shipping hot route\",");
            builder.AppendLine("  \"compile_attempt\": \"static inquisition only; guarded Hecton8.Core.csproj build previously failed on external dependencies and generated project coverage omits DOD/WFC/editor files\",");
            builder.AppendLine("  \"laser_cut_request_layout_ok\": " + (layoutOk ? "true" : "false") + ",");
            builder.AppendLine("  \"laser_cut_request_layout_faults\": " + layoutFaults + ",");
            builder.AppendLine("  \"verdict\": \"" + ResolveVerdict(cutterSyncRaycasts, cutterParticleSystems, cutterInstantiateSites, cutterMeshMutationSites, legacyGlobalSignalsPublishSites, unityTimeSites, liveDiagnosisReadSites, managedDiagnosisSeveritySites, blackBoxBinaryWriterSites, prematureAupFloatHashSites, directPowerRuntimeDependencySites, directLogisticsGridRuntimeDependencySites, wfcRuntimeDataVaultRegistrySites, wfcRuntimePropertyAccessorSites, cutterPropertyAccessorSites, laserEventLegacyGlobalInitSites, laserEventHotEnsureSites, sargassumRuntimeRegistrationSites, dodHotSchedulerEnsureSites, laserHotComponentDiscoverySites, wfcRouteSnapshotScanSites, dodRuntimeDataVaultRegistrySites, explicitSecondaryDiagnosisComponentLookupSites, originBridgeReadSites, dodRuntimeDirectOriginSites, dodDebugGizmoDirectOriginSites, dodRuntimeOriginZeroFallbackSites, hotManagedIterationSites, hotManagedTextAllocationSites, mockForceCompleteSites, mockForceCompleteFenceHits, layoutOk) + "\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string ResolveVerdict(int syncRaycasts, int particleSystems, int instantiateSites, int meshMutationSites, int legacyGlobalSignalsPublishSites, int unityTimeSites, int liveDiagnosisReadSites, int managedDiagnosisSeveritySites, int blackBoxBinaryWriterSites, int prematureAupFloatHashSites, int directPowerRuntimeDependencySites, int directLogisticsGridRuntimeDependencySites, int wfcRuntimeDataVaultRegistrySites, int wfcRuntimePropertyAccessorSites, int cutterPropertyAccessorSites, int laserEventLegacyGlobalInitSites, int laserEventHotEnsureSites, int sargassumRuntimeRegistrationSites, int dodHotSchedulerEnsureSites, int laserHotComponentDiscoverySites, int wfcRouteSnapshotScanSites, int dodRuntimeDataVaultRegistrySites, int explicitSecondaryDiagnosisComponentLookupSites, int originBridgeReadSites, int dodRuntimeDirectOriginSites, int dodDebugGizmoDirectOriginSites, int dodRuntimeOriginZeroFallbackSites, int hotManagedIterationSites, int hotManagedTextAllocationSites, int mockForceCompleteSites, int mockForceCompleteFenceHits, bool layoutOk)
        {
            if (!layoutOk)
                return "FAIL: LaserCutRequestDTO layout contract broken.";
            if (syncRaycasts > 0)
                return "FAIL: synchronous cutter Physics.Raycast pattern remains.";
            if (particleSystems > 0)
                return "FAIL: cutter ParticleSystem pattern remains.";
            if (instantiateSites > 0)
                return "FAIL: cutter Instantiate pattern remains.";
            if (legacyGlobalSignalsPublishSites > 0)
                return "FAIL: legacy GlobalSignals.Publish remains in the scanned cutter route.";
            if (unityTimeSites > 0)
                return "FAIL: Unity Time.* authority remains in the scanned cutter route.";
            if (liveDiagnosisReadSites > 0)
                return "FAIL: operational read route can still trigger live diagnosis.";
            if (managedDiagnosisSeveritySites > 0)
                return "FAIL: managed diagnosis severity string remains in cutter state.";
            if (blackBoxBinaryWriterSites > 0)
                return "FAIL: black-box dump still uses BinaryWriter field loops.";
            if (prematureAupFloatHashSites > 0)
                return "FAIL: absolute hitAup is still cast to float in cutter proof/hash math.";
            if (directPowerRuntimeDependencySites > 0)
                return "FAIL: scanned cutter route still has direct Hecton8.Power runtime dependency text.";
            if (directLogisticsGridRuntimeDependencySites > 0)
                return "FAIL: scanned cutter route still has direct Logistics.Grid runtime dependency text.";
            if (wfcRuntimeDataVaultRegistrySites > 0)
                return "FAIL: WFC laser cut runtime still queries GlobalRegistry.DataVault.";
            if (wfcRuntimePropertyAccessorSites > 0)
                return "FAIL: WfcLaserCutRuntime still exposes dead static property accessors instead of telemetry proof rows.";
            if (cutterPropertyAccessorSites > 0)
                return "FAIL: cutter-adjacent runtime state still exposes public property facades instead of explicit Read* methods or owner-private math.";
            if (laserEventLegacyGlobalInitSites > 0)
                return "FAIL: LaserCutterEvents still initializes legacy GlobalSignals queues.";
            if (laserEventHotEnsureSites > 0)
                return "FAIL: LaserCutterEvents.Enqueue can still run cold EnsureInitialized work.";
            if (sargassumRuntimeRegistrationSites > 0)
                return "FAIL: SargassumCutResponder still registers dispatcher/updatable state from cut impulse.";
            if (dodHotSchedulerEnsureSites > 0)
                return "FAIL: TryScheduleRaycastBatch can still run cold EnsureInitialized work.";
            if (laserHotComponentDiscoverySites > 0)
                return "FAIL: active laser route still discovers components through TryGetComponent/GetComponentInParent.";
            if (wfcRouteSnapshotScanSites > 0)
                return "FAIL: WFC cutter hit route still scans SignalBus snapshots instead of cached owner-phase context.";
            if (dodRuntimeDataVaultRegistrySites > 0)
                return "FAIL: LaserCutterDodRuntime still has an implicit GlobalRegistry.DataVault fallback.";
            if (explicitSecondaryDiagnosisComponentLookupSites > 0)
                return "FAIL: explicit secondary diagnosis still discovers components through TryGetComponent/GetComponentInParent.";
            if (originBridgeReadSites > 0)
                return "FAIL: GlobalSignals.CurrentRuntimeOriginAup remains in scanned cutter AUP conversion.";
            if (dodRuntimeDirectOriginSites > 0)
                return "FAIL: LaserCutterDodRuntime still reads HectonFloatingOrigin.CurrentTotalOffsetDouble directly instead of cached owner-phase origin.";
            if (dodDebugGizmoDirectOriginSites > 0)
                return "FAIL: LaserCutterDodDebugGizmo still reads HectonFloatingOrigin.CurrentTotalOffsetDouble directly instead of cached DOD presentation origin.";
            if (dodRuntimeOriginZeroFallbackSites > 0)
                return "FAIL: LaserCutterDodRuntime can still fall back to zero presentation origin instead of failing closed.";
            if (hotManagedIterationSites > 0)
                return "FAIL: hot cutter route still contains foreach/LINQ-style managed iteration text.";
            if (hotManagedTextAllocationSites > 0)
                return "FAIL: hot cutter route still contains managed text allocation/formatting text.";
            if (mockForceCompleteSites > 0 && mockForceCompleteFenceHits < mockForceCompleteSites)
                return "FAIL: mock cutter trigger force-complete is not compile-fenced to editor/development.";
            if (meshMutationSites > 0)
                return "REVIEW: cutter-related mesh mutation text remains.";
            return "PASS: cutter path has deferred raycast/DOD evidence and no direct sync raycast, prefab spawn, ParticleSystem, legacy GlobalSignals publish/init, GlobalSignals origin bridge read, direct DOD runtime floating-origin read, direct DOD debug-gizmo floating-origin read, zero-origin presentation fallback, Unity Time clock, live diagnosis read, BinaryWriter dump, premature AUP float hash, direct Power/Grid runtime dependency, WFC hot DataVault registry query, dead WFC runtime property accessor, cutter-adjacent public property facade, DOD runtime DataVault registry fallback, hot event EnsureInitialized, sargassum cut impulse dispatcher registration, hot DOD scheduler EnsureInitialized, active-route component discovery, explicit secondary diagnosis component lookup, hot managed iteration/text allocation, unfenced mock force-complete, WFC route snapshot scan, mesh mutation, or request-padding metadata text.";
        }

        private static int CountManagedIterationWithinMethod(string text, string methodSignature)
        {
            return CountWithinMethod(text, methodSignature, "foreach") +
                   CountWithinMethod(text, methodSignature, "System.Linq") +
                   CountWithinMethod(text, methodSignature, "Enumerable.") +
                   CountWithinMethod(text, methodSignature, ".Select(") +
                   CountWithinMethod(text, methodSignature, ".Where(") +
                   CountWithinMethod(text, methodSignature, ".ToList(") +
                   CountWithinMethod(text, methodSignature, ".ToArray(");
        }

        private static int CountManagedTextAllocationWithinMethod(string text, string methodSignature)
        {
            return CountWithinMethod(text, methodSignature, "string.Format") +
                   CountWithinMethod(text, methodSignature, "new string(") +
                   CountWithinMethod(text, methodSignature, "$\"");
        }

        private static bool IsPureNoAcquireReader(string text, string methodName)
        {
            int index = text.IndexOf(methodName, StringComparison.Ordinal);
            if (index < 0)
                return false;

            int length = Math.Min(520, text.Length - index);
            string window = text.Substring(index, length);
            return window.IndexOf("EnsureInitialized()", StringComparison.Ordinal) < 0 &&
                   window.IndexOf("allowAcquire: true", StringComparison.Ordinal) < 0;
        }

        private static int Count(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                index = text.IndexOf(pattern, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += pattern.Length;
            }

            return count;
        }

        private static bool ContainsWithinMethod(string text, string methodSignature, string pattern)
        {
            int start = text.IndexOf(methodSignature, StringComparison.Ordinal);
            if (start < 0)
                return false;

            int firstBrace = text.IndexOf('{', start);
            if (firstBrace < 0)
                return false;

            int depth = 0;
            for (int i = firstBrace; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int length = i - firstBrace;
                        return text.IndexOf(pattern, firstBrace, length, StringComparison.Ordinal) >= 0;
                    }
                }
            }

            return false;
        }

        private static int CountWithinMethod(string text, string methodSignature, string pattern)
        {
            int start = text.IndexOf(methodSignature, StringComparison.Ordinal);
            if (start < 0)
                return 0;

            int firstBrace = text.IndexOf('{', start);
            if (firstBrace < 0)
                return 0;

            int depth = 0;
            for (int i = firstBrace; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int length = i - firstBrace;
                        int count = 0;
                        int index = firstBrace;
                        int endExclusive = firstBrace + length;
                        while (index < endExclusive)
                        {
                            index = text.IndexOf(pattern, index, length - (index - firstBrace), StringComparison.Ordinal);
                            if (index < 0)
                                return count;

                            count++;
                            index += pattern.Length;
                        }

                        return count;
                    }
                }
            }

            return 0;
        }
    }
}
#endif
