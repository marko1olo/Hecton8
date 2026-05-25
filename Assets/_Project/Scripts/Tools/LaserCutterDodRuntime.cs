namespace Hecton8.Tools
{
    using System;
    using System.IO;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.Tools.ToolKinematics.Contracts;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public static class LaserCutterDodRuntime
    {
        private static int s_x001LaserCutterDodRuntimeSignalPushDropCount;
        private const uint BlackBoxDumpMagic = 0x53483235u; // SH25
        private const uint BlackBoxDumpVersion = 1u;
        private const int BlackBoxDumpHeaderBytes = 32;
        private const int LaserCutTelemetryEntrySizeBytes = 128;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_225.bin";

        private static IDataVault _dataVault;
        private static VaultGenerationHandle<LaserCutRequestDTO> _requestsHandle;
        private static VaultGenerationHandle<LaserCutRequestMetaDTO> _requestMetasHandle;
        private static VaultGenerationHandle<int> _requestCountHandle;
        private static VaultGenerationHandle<VoxelSonarSdfRaycastHit> _sdfProbeHitsHandle;
        private static VaultGenerationHandle<LaserCutHitDTO> _hitResultsHandle;
        private static VaultGenerationHandle<LaserCutDeformationStateDTO> _deformationHandle;
        private static VaultGenerationHandle<LaserCutBatteryDrainRequest> _batteryDrainHandle;
        private static VaultGenerationHandle<LaserCutGlowDecalRequestDTO> _glowDecalHandle;
        private static VaultGenerationHandle<LaserCutImpactVfxDTO> _impactVfxHandle;
        private static VaultGenerationHandle<LaserCutCooldownDTO> _cooldownHandle;
        private static VaultGenerationHandle<LaserCutTelemetryEntry> _telemetryRingHandle;
        private static VaultGenerationHandle<int> _telemetryCursorHandle;
        private static VaultGenerationHandle<LaserCutterTuningDTO> _tuningHandle;
        private static VaultGenerationHandle<LaserCutterSpecDTO> _specHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;
        private static VaultGenerationHandle<LaserCutterCountersDTO> _countersHandle;
        private static VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;

        private static Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _cachedVoxelSdfReadModel;
        private static JobHandle _scheduledSdfProbeHandle;
        private static bool _scheduledSdfProbeActive;
        private static int _scheduledSdfProbeCount;
        private static JobHandle _scheduledEvaluationHandle;
        private static bool _scheduledEvaluationActive;
        private static int _scheduledEvaluationCount;
        private static uint _scheduledEvaluationCursorBase;
        private static uint _requestSequence;
        private static uint _lastDumpFrame;
        private static float _cachedGlobalQualityWeight = 1f;
        private static double3 _cachedPresentationOriginAup;
        private static bool _hasCachedPresentationOriginAup;
        private static double3 _scheduledSdfProbePresentationOriginAup;
        private static bool _hasScheduledSdfProbePresentationOriginAup;
        private static double3 _scheduledEvaluationPresentationOriginAup;
        private static bool _hasScheduledEvaluationPresentationOriginAup;

        public static bool EnsureInitialized(IDataVault vault)
        {
            if (vault == null)
            {
                ReleaseVaultHandles(_dataVault);
                ClearHandles();
                _dataVault = null;
                return false;
            }

            if (!ReferenceEquals(_dataVault, vault))
            {
                ReleaseVaultHandles(_dataVault);
                ClearHandles();
                _dataVault = vault;
            }

            bool ready = BindSchedulerBuffers(out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);
            if (ready)
            {
                CacheVoxelSdfReadModel(GlobalRegistry.VoxelSonarSdf);
                CacheScalabilityStateHandle();
                RefreshCachedGlobalQualityWeight();
                EnsureTuningSeeded();
            }

            return ready;
        }

        public static void CachePresentationOriginAup(double3 originAup)
        {
            if (!math.all(math.isfinite(originAup)))
            {
                ClearPresentationOriginAup();
                return;
            }

            _cachedPresentationOriginAup = originAup;
            _hasCachedPresentationOriginAup = true;
        }

        internal static void CacheVoxelSdfReadModel(IVoxelSonarSdfReadModel readModel)
        {
            _cachedVoxelSdfReadModel = readModel;
        }

        public static void ClearPresentationOriginAup()
        {
            _cachedPresentationOriginAup = double3.zero;
            _hasCachedPresentationOriginAup = false;
            ClearScheduledSdfProbePresentationOrigin();
            ClearScheduledEvaluationPresentationOrigin();
        }

        public static bool TryGetPresentationOriginForGizmo(out double3 originAup)
        {
            return TryReadPresentationOriginAup(out originAup);
        }

        public static bool QueueLiveRequest(
            double3 originAup,
            float3 direction,
            float cuttingPower,
            float maximumDistance,
            uint toolHashID,
            uint parentEntityID,
            uint frame)
        {
            if (_dataVault == null ||
                _scheduledSdfProbeActive ||
                _scheduledEvaluationActive ||
                !BindCoreBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<int> _,
                    out NativeArray<LaserCutterCountersDTO> counters,
                    allowAcquire: false) ||
                !requests.IsCreated ||
                !requestMetas.IsCreated ||
                !requestCount.IsCreated ||
                requestCount.Length <= 0)
            {
                return false;
            }

            int capacity = math.min(requests.Length, requestMetas.Length);
            int index = math.clamp(requestCount[0], 0, capacity);
            if (index >= capacity)
            {
                IncrementSuppressed(counters, frame);
                return false;
            }

            float3 safeDirection = SafeNormalize(direction, new float3(0f, 0f, 1f));
            uint sequence = unchecked(++_requestSequence);
            requests[index] = new LaserCutRequestDTO
            {
                RayOriginAUP = originAup,
                RayDirection = safeDirection,
                CuttingPower = math.saturate(cuttingPower),
                MaximumDistance = math.max(0.01f, maximumDistance),
                ToolHashID = toolHashID == 0u ? LaserCutterDodConstants.LaserCutterHash : toolHashID,
                ParentEntityID = parentEntityID
            };

            requestMetas[index] = new LaserCutRequestMetaDTO
            {
                Frame = frame,
                Flags = LaserCutterDodConstants.RequestFlagValid,
                RequestSequence = sequence,
                CooldownUntilFrame = 0u,
                LastAppliedFrame = 0u,
                Reserved0 = 0u,
                StateHash = Mix(1469598103934665603UL, sequence),
                Reserved1 = 0UL,
                Reserved2 = 0UL,
                Reserved3 = 0UL,
                Reserved4 = 0UL
            };

            requestCount[0] = index + 1;
            if (counters.IsCreated && counters.Length > 0)
            {
                LaserCutterCountersDTO counter = counters[0];
                counter.RequestCount = requestCount[0];
                counter.LastFrame = frame;
                counter.LastSequence = sequence;
                counter.StateHash = Mix(counter.StateHash == 0UL ? 1469598103934665603UL : counter.StateHash, sequence);
                counters[0] = counter;
            }

            return true;
        }

        public static bool GenerateMockCutterTriggers(int count, double3 originAup, uint frame, uint seed)
        {
#if UNITY_EDITOR
            if (_dataVault == null ||
                _scheduledSdfProbeActive ||
                _scheduledEvaluationActive ||
                !BindCoreBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<int> _,
                    out NativeArray<LaserCutterCountersDTO> counters,
                    allowAcquire: false) ||
                !requests.IsCreated ||
                !requestMetas.IsCreated ||
                !requestCount.IsCreated ||
                requestCount.Length <= 0)
            {
                return false;
            }

            int safeCount = math.clamp(count, 0, math.min(requests.Length, requestMetas.Length));
            if (safeCount <= 0)
                return false;

            GenerateMockCutterTriggersJob job = new GenerateMockCutterTriggersJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                OriginAUP = originAup,
                Frame = frame,
                ToolHashID = LaserCutterDodConstants.LaserCutterHash,
                ParentEntityID = 0u,
                Seed = seed,
                MaximumDistanceMeters = ReadTuningOrDefaultNoAcquire().DefaultMaxDistanceMeters
            };
            JobHandle mockHandle = job.Schedule(safeCount, LaserCutterDodConstants.MinCommandsPerJob);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, mockHandle);
            // COLD SYNC JOB: deterministic mock trigger rows must be visible to the immediate editor/CI caller.
            DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true);

            requestCount[0] = safeCount;

            if (counters.IsCreated && counters.Length > 0)
            {
                LaserCutterCountersDTO counter = counters[0];
                counter.RequestCount = safeCount;
                counter.LastFrame = frame;
                counter.LastSequence = safeCount > 0 ? requestMetas[safeCount - 1].RequestSequence : counter.LastSequence;
                counters[0] = counter;
            }

            return true;
#else
            return false;
#endif
        }

        public static bool TryScheduleSdfProbeBatch(int layerMask, QueryTriggerInteraction queryTriggerInteraction, uint frame)
        {
            if (_scheduledSdfProbeActive ||
                _scheduledEvaluationActive)
            {
                return false;
            }

            if (_dataVault == null)
                return false;

            if (!TryReadPresentationOriginAup(out double3 presentationOriginAup))
            {
                SuppressQueuedRequests(frame);
                return false;
            }

            RefreshCachedGlobalQualityWeight();

            if (!BindSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<VoxelSonarSdfRaycastHit> sdfHits,
                    out NativeArray<LaserCutCooldownDTO> cooldowns,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    allowAcquire: false))
            {
                return false;
            }

            int scheduledCount = math.clamp(requestCount[0], 0, math.min(math.min(requests.Length, requestMetas.Length), sdfHits.Length));
            scheduledCount = math.min(scheduledCount, cooldowns.Length);
            if (scheduledCount <= 0)
                return false;

            if (!TryReadCutterSdfSnapshot(
                    presentationOriginAup,
                    requests,
                    scheduledCount,
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out int3 gridDimensions,
                    out float3 volumeOrigin,
                    out float3 cellSize,
                    out float sdfRange))
            {
                SuppressQueuedRequests(frame);
                return false;
            }

            LaserCutterTuningDTO tuning = ReadTuningOrDefaultNoAcquire();
            uint cooldownFrames = (uint)math.max(1f, math.isfinite(tuning.CooldownFrames) ? tuning.CooldownFrames : 1f);
            ManageCutterCooldownJob cooldownJob = new ManageCutterCooldownJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                Cooldowns = cooldowns,
                Frame = frame,
                CooldownFrames = cooldownFrames
            };

            BuildCutterSdfProbeHitsJob buildJob = new BuildCutterSdfProbeHitsJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                SdfHits = sdfHits,
                EncodedSdf = encodedSdf,
                PresentationOriginAUP = presentationOriginAup,
                GridDimensions = gridDimensions,
                VolumeOrigin = volumeOrigin,
                CellSize = cellSize,
                SdfRange = sdfRange,
                StepMeters = ResolveCutterSdfStepMeters(sdfRange, in cellSize),
                MaxSteps = ResolveCutterSdfMaxSteps(_cachedGlobalQualityWeight),
                LayerMask = layerMask,
                VoxelLayerMask = HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask
            };

            JobHandle cooldownHandle = cooldownJob.Schedule(scheduledCount, LaserCutterDodConstants.MinCommandsPerJob);
            _scheduledSdfProbeHandle = buildJob.Schedule(scheduledCount, LaserCutterDodConstants.MinCommandsPerJob, cooldownHandle);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _scheduledSdfProbeHandle);
            _scheduledSdfProbeActive = true;
            _scheduledSdfProbeCount = scheduledCount;
            _scheduledSdfProbePresentationOriginAup = presentationOriginAup;
            _hasScheduledSdfProbePresentationOriginAup = true;
            return true;
        }

        public static bool TryCompleteScheduledSdfProbesAndEvaluate(float heat01)
        {
            if (_scheduledEvaluationActive)
                return TryFinalizeScheduledEvaluation();

            if (!_scheduledSdfProbeActive)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledSdfProbeHandle))
                return false;

            if (!TryReadScheduledSdfProbePresentationOrigin(out double3 presentationOriginAup))
            {
                SuppressQueuedRequests(ResolveCurrentFrameId());
                _scheduledSdfProbeActive = false;
                _scheduledSdfProbeCount = 0;
                ClearScheduledSdfProbePresentationOrigin();
                return false;
            }

            if (!BindSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<VoxelSonarSdfRaycastHit> sdfHits,
                    out _,
                    out NativeArray<LaserCutHitDTO> hitResults,
                    out NativeArray<LaserCutDeformationStateDTO> deformations,
                    out NativeArray<LaserCutBatteryDrainRequest> batteryDrains,
                    out NativeArray<LaserCutGlowDecalRequestDTO> decals,
                    out NativeArray<LaserCutImpactVfxDTO> impactVfx,
                    out NativeArray<LaserCutTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    allowAcquire: false))
            {
                _scheduledSdfProbeActive = false;
                _scheduledSdfProbeCount = 0;
                ClearScheduledSdfProbePresentationOrigin();
                return false;
            }

            int count = math.clamp(_scheduledSdfProbeCount, 0, math.min(math.min(requests.Length, requestMetas.Length), sdfHits.Length));
            if (count <= 0)
            {
                requestCount[0] = 0;
                _scheduledSdfProbeActive = false;
                _scheduledSdfProbeCount = 0;
                ClearScheduledSdfProbePresentationOrigin();
                return false;
            }

            uint cursorBase = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? (uint)math.max(0, telemetryCursor[0]) : 0u;
            LaserCutterTuningDTO tuning = ReadTuningOrDefaultNoAcquire();
            EvaluateCutterProbeHitsJob evaluateJob = new EvaluateCutterProbeHitsJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                ProbeHits = sdfHits,
                HitResults = hitResults,
                DeformationStates = deformations,
                BatteryDrainRequests = batteryDrains,
                GlowDecalRequests = decals,
                ImpactVfxRequests = impactVfx,
                TelemetryRing = telemetry,
                PresentationOriginAUP = presentationOriginAup,
                TelemetryCursorBase = cursorBase,
                GlobalQualityWeight = _cachedGlobalQualityWeight,
                Heat01 = heat01,
                DentRadiusMinMeters = tuning.DentRadiusMinMeters,
                DentRadiusMaxMeters = tuning.DentRadiusMaxMeters,
                GlowLifetimeSeconds = tuning.GlowLifetimeSeconds,
                BatteryWattsAtPowerOne = tuning.BatteryWattsAtPowerOne,
                SparkIntensityScale = tuning.SparkIntensityScale,
                LowSparkCount = tuning.LowSparkCount,
                UltraSparkCount = tuning.UltraSparkCount
            };
            _scheduledEvaluationHandle = evaluateJob.Schedule(count, LaserCutterDodConstants.MinCommandsPerJob);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _scheduledEvaluationHandle);
            _scheduledEvaluationActive = true;
            _scheduledEvaluationCount = count;
            _scheduledEvaluationCursorBase = cursorBase;
            _scheduledEvaluationPresentationOriginAup = presentationOriginAup;
            _hasScheduledEvaluationPresentationOriginAup = true;
            _scheduledSdfProbeActive = false;
            _scheduledSdfProbeCount = 0;
            ClearScheduledSdfProbePresentationOrigin();
            return false;
        }

        private static bool TryFinalizeScheduledEvaluation()
        {
            if (!_scheduledEvaluationActive)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledEvaluationHandle))
                return false;

            if (!TryReadScheduledEvaluationPresentationOrigin(out double3 presentationOriginAup))
            {
                SuppressQueuedRequests(ResolveCurrentFrameId());
                _scheduledEvaluationActive = false;
                _scheduledEvaluationCount = 0;
                _scheduledEvaluationCursorBase = 0u;
                ClearScheduledEvaluationPresentationOrigin();
                return false;
            }

            if (!BindSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> _,
                    out NativeArray<int> requestCount,
                    out NativeArray<VoxelSonarSdfRaycastHit> sdfHits,
                    out _,
                    out _,
                    out _,
                    out NativeArray<LaserCutBatteryDrainRequest> batteryDrains,
                    out _,
                    out NativeArray<LaserCutImpactVfxDTO> impactVfx,
                    out NativeArray<LaserCutTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    allowAcquire: false))
            {
                _scheduledEvaluationActive = false;
                _scheduledEvaluationCount = 0;
                _scheduledEvaluationCursorBase = 0u;
                ClearScheduledEvaluationPresentationOrigin();
                return false;
            }

            int count = math.clamp(_scheduledEvaluationCount, 0, math.min(requests.Length, sdfHits.Length));
            if (count <= 0)
            {
                requestCount[0] = 0;
                _scheduledEvaluationActive = false;
                _scheduledEvaluationCount = 0;
                _scheduledEvaluationCursorBase = 0u;
                ClearScheduledEvaluationPresentationOrigin();
                return false;
            }

            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = (int)((_scheduledEvaluationCursorBase + (uint)count) % (uint)LaserCutterDodConstants.BlackBoxFrameCount);

            PublishDrainSignals(batteryDrains, count);
            PublishImpactSignals(impactVfx, count, presentationOriginAup);
            DumpOnNonFinite(telemetry, telemetryCursor);
            requestCount[0] = 0;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;
            ClearScheduledEvaluationPresentationOrigin();
            return true;
        }

        public static void StageGpuSparkSignal(double3 hitAup, float3 normal, float heat01, float cuttingPower01, uint toolHashID, uint parentEntityID, uint frame)
        {
            LaserCutterTuningDTO tuning = ReadTuningOrDefaultNoAcquire();
            float quality = Smooth01(_cachedGlobalQualityWeight);
            float intensity = math.saturate((0.35f + heat01 * 0.65f) * (0.55f + cuttingPower01 * 0.45f));
            float lowSparkCount = math.max(0f, math.isfinite(tuning.LowSparkCount) ? tuning.LowSparkCount : LaserCutterDodConstants.LowSparkCount);
            float ultraSparkCount = math.max(lowSparkCount, math.isfinite(tuning.UltraSparkCount) ? tuning.UltraSparkCount : LaserCutterDodConstants.UltraSparkCount);
            float sparkScale = math.max(0f, math.isfinite(tuning.SparkIntensityScale) ? tuning.SparkIntensityScale : 1f);
            ushort quantity = (ushort)math.clamp(
                (int)math.round(math.lerp(lowSparkCount, ultraSparkCount, quality) * intensity * sparkScale),
                0,
                ushort.MaxValue);

            if (quantity == 0)
                return;

            if (!TryReadPresentationOriginAup(out double3 presentationOriginAup))
                return;

            PublishGpuSparkSignals(hitAup, normal, intensity, quantity, heat01, toolHashID, parentEntityID, frame, presentationOriginAup, true);
        }

        private static void PublishGpuSparkSignals(double3 hitAup, float3 normal, float intensity01, ushort quantity, float heat01, uint toolHashID, uint parentEntityID, uint frame, double3 presentationOriginAup, bool stageImpactVfx)
        {
            float intensity = math.saturate(intensity01);
            DebrisSpawnSignal debris = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(hitAup),
                SpeciesHash = LaserCutterDodConstants.SparkSpeciesHash,
                SourceEntityId = parentEntityID,
                Intensity01 = intensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindSparks,
                Flags = DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard,
                Quantity = quantity
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref s_x001LaserCutterDodRuntimeSignalPushDropCount);

            VfxSparkRequestSignal spark = new VfxSparkRequestSignal
            {
                HitPoint = AupPrecisionMath.LocalDeltaFloat3(hitAup, presentationOriginAup, float3.zero),
                Normal = SafeNormalize(normal, new float3(0f, 1f, 0f)),
                MaterialHash = LaserCutterDodConstants.LaserCutterHash,
                ToolHash = toolHashID == 0u ? LaserCutterDodConstants.LaserCutterHash : toolHashID,
                Intensity01 = intensity,
                Frame = frame
            };
            SignalBus<VfxSparkRequestSignal>.TryPushTracked(in spark, ref s_x001LaserCutterDodRuntimeSignalPushDropCount);

            if (stageImpactVfx)
                StageImpactVfxIfBound(hitAup, normal, intensity, heat01, quantity, toolHashID, frame);
        }

        public static bool TryGetLatestTelemetry(out LaserCutTelemetryEntry entry)
        {
            entry = default;
            if (_dataVault == null ||
                !ReadBoundBuffer(
                    LaserCutterDodConstants.BlackBoxFrameCount,
                    ref _telemetryRingHandle,
                    out NativeArray<LaserCutTelemetryEntry> telemetry) ||
                !ReadBoundBuffer(
                    1,
                    ref _telemetryCursorHandle,
                    out NativeArray<int> cursor) ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                telemetry.Length <= 0 ||
                cursor.Length <= 0)
            {
                return false;
            }

            int index = cursor[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;

            entry = telemetry[index];
            return entry.LayoutMagic == LaserCutterDodConstants.LayoutMagic;
        }

        public static bool TryGetTuning(out LaserCutterTuningDTO tuning)
        {
            tuning = default;
            if (_dataVault == null ||
                !ReadBoundBuffer(
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length <= 0 ||
                tuningBuffer[0].VersionHash == 0UL)
            {
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TrySetTuning(in LaserCutterTuningDTO tuning)
        {
            if (_dataVault == null ||
                !BindOrAcquireBuffer(
                    LaserCutterDodConstants.TuningBuffer,
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            LaserCutterTuningDTO sanitized = tuning;
            sanitized.MinimumPower01 = math.saturate(sanitized.MinimumPower01);
            sanitized.DefaultMaxDistanceMeters = math.max(0.01f, sanitized.DefaultMaxDistanceMeters);
            sanitized.DentRadiusMinMeters = math.max(0f, sanitized.DentRadiusMinMeters);
            sanitized.DentRadiusMaxMeters = math.max(sanitized.DentRadiusMinMeters, sanitized.DentRadiusMaxMeters);
            sanitized.GlowLifetimeSeconds = math.max(0f, sanitized.GlowLifetimeSeconds);
            sanitized.BatteryWattsAtPowerOne = math.max(0f, sanitized.BatteryWattsAtPowerOne);
            sanitized.CooldownFrames = math.max(1f, sanitized.CooldownFrames);
            sanitized.SparkIntensityScale = math.max(0f, sanitized.SparkIntensityScale);
            sanitized.LowSparkCount = math.max(0f, sanitized.LowSparkCount);
            sanitized.UltraSparkCount = math.max(sanitized.LowSparkCount, sanitized.UltraSparkCount);
            sanitized.GlobalQualityWeight = math.saturate(sanitized.GlobalQualityWeight);
            sanitized.VersionHash = sanitized.VersionHash == 0UL ? 0x53484C4354554E45UL : sanitized.VersionHash;
            tuningBuffer[0] = sanitized;
            _cachedGlobalQualityWeight = sanitized.GlobalQualityWeight;
            return true;
        }

        public static bool TryGetRequestForGizmo(int index, out LaserCutRequestDTO request)
        {
            request = default;
            return TryGetRequestForGizmo(index, out request, out _);
        }

        public static bool TryGetRequestForGizmo(int index, out LaserCutRequestDTO request, out LaserCutRequestMetaDTO meta)
        {
            request = default;
            meta = default;
            if (_dataVault == null ||
                !ReadCoreBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out _,
                    out _) ||
                !requests.IsCreated ||
                !requestMetas.IsCreated ||
                !requestCount.IsCreated ||
                requestCount.Length <= 0 ||
                index < 0 ||
                index >= requestCount[0] ||
                index >= requests.Length ||
                index >= requestMetas.Length)
            {
                return false;
            }

            request = requests[index];
            meta = requestMetas[index];
            return (meta.Flags & LaserCutterDodConstants.RequestFlagValid) != 0u;
        }

        public static bool TryGetHitForGizmo(int index, out LaserCutHitDTO hit)
        {
            hit = default;
            if (_dataVault == null ||
                !ReadBoundBuffer(
                    LaserCutterDodConstants.MaxHitResults,
                    ref _hitResultsHandle,
                    out NativeArray<LaserCutHitDTO> hitResults) ||
                !hitResults.IsCreated ||
                index < 0 ||
                index >= hitResults.Length)
            {
                return false;
            }

            hit = hitResults[index];
            return (hit.Flags & LaserCutterDodConstants.ResultFlagHit) != 0u;
        }

        private static bool TryAcquireSpecBufferForCsvIngest(out NativeArray<LaserCutterSpecDTO> specs)
        {
            specs = default;
            return _dataVault != null &&
                   BindOrAcquireBuffer(
                       LaserCutterDodConstants.SpecBuffer,
                       LaserCutterDodConstants.CsvSpecCapacity,
                       ref _specHandle,
                       out specs);
        }

        private static bool TryAcquireCsvScratchForCsvIngest(out NativeArray<byte> scratch)
        {
            scratch = default;
            return _dataVault != null &&
                   BindOrAcquireBuffer(
                       LaserCutterDodConstants.CsvScratchBuffer,
                       LaserCutterDodConstants.CsvScratchByteCapacity,
                       ref _csvScratchHandle,
                       out scratch);
        }

        private static bool BindCoreBuffers(
            out NativeArray<LaserCutRequestDTO> requests,
            out NativeArray<LaserCutRequestMetaDTO> requestMetas,
            out NativeArray<int> requestCount,
            out NativeArray<int> telemetryCursor,
            out NativeArray<LaserCutterCountersDTO> counters,
            bool allowAcquire = true)
        {
            requests = default;
            requestMetas = default;
            requestCount = default;
            telemetryCursor = default;
            counters = default;
            return BindOrAcquireBuffer(LaserCutterDodConstants.RequestsBuffer, LaserCutterDodConstants.MaxRequests, ref _requestsHandle, out requests, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.RequestMetaBuffer, LaserCutterDodConstants.MaxRequests, ref _requestMetasHandle, out requestMetas, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.RequestCountBuffer, 1, ref _requestCountHandle, out requestCount, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.TelemetryCursorBuffer, 1, ref _telemetryCursorHandle, out telemetryCursor, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.CountersBuffer, 1, ref _countersHandle, out counters, allowAcquire);
        }

        private static bool BindSchedulerBuffers(
            out NativeArray<LaserCutRequestDTO> requests,
            out NativeArray<LaserCutRequestMetaDTO> requestMetas,
            out NativeArray<int> requestCount,
            out NativeArray<VoxelSonarSdfRaycastHit> sdfHits,
            out NativeArray<LaserCutCooldownDTO> cooldowns,
            out NativeArray<LaserCutHitDTO> hitResults,
            out NativeArray<LaserCutDeformationStateDTO> deformations,
            out NativeArray<LaserCutBatteryDrainRequest> batteryDrains,
            out NativeArray<LaserCutGlowDecalRequestDTO> decals,
            out NativeArray<LaserCutImpactVfxDTO> impactVfx,
            out NativeArray<LaserCutTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            bool allowAcquire = true)
        {
            sdfHits = default;
            cooldowns = default;
            hitResults = default;
            deformations = default;
            batteryDrains = default;
            decals = default;
            impactVfx = default;
            telemetry = default;
            return BindCoreBuffers(out requests, out requestMetas, out requestCount, out telemetryCursor, out _, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.SdfProbeHitsBuffer, LaserCutterDodConstants.MaxRequests, ref _sdfProbeHitsHandle, out sdfHits, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.CooldownBuffer, LaserCutterDodConstants.MaxRequests, ref _cooldownHandle, out cooldowns, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.HitResultsBuffer, LaserCutterDodConstants.MaxHitResults, ref _hitResultsHandle, out hitResults, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.DeformationBuffer, LaserCutterDodConstants.MaxHitResults, ref _deformationHandle, out deformations, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.BatteryDrainBuffer, LaserCutterDodConstants.MaxHitResults, ref _batteryDrainHandle, out batteryDrains, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.GlowDecalBuffer, LaserCutterDodConstants.MaxHitResults, ref _glowDecalHandle, out decals, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.ImpactVfxBuffer, LaserCutterDodConstants.MaxHitResults, ref _impactVfxHandle, out impactVfx, allowAcquire) &&
                   BindOrAcquireBuffer(LaserCutterDodConstants.TelemetryRingBuffer, LaserCutterDodConstants.BlackBoxFrameCount, ref _telemetryRingHandle, out telemetry, allowAcquire);
        }

        private static bool ReadCoreBuffers(
            out NativeArray<LaserCutRequestDTO> requests,
            out NativeArray<LaserCutRequestMetaDTO> requestMetas,
            out NativeArray<int> requestCount,
            out NativeArray<int> telemetryCursor,
            out NativeArray<LaserCutterCountersDTO> counters)
        {
            requests = default;
            requestMetas = default;
            requestCount = default;
            telemetryCursor = default;
            counters = default;
            return ReadBoundBuffer(LaserCutterDodConstants.MaxRequests, ref _requestsHandle, out requests) &&
                   ReadBoundBuffer(LaserCutterDodConstants.MaxRequests, ref _requestMetasHandle, out requestMetas) &&
                   ReadBoundBuffer(1, ref _requestCountHandle, out requestCount) &&
                   ReadBoundBuffer(1, ref _telemetryCursorHandle, out telemetryCursor) &&
                   ReadBoundBuffer(1, ref _countersHandle, out counters);
        }

        private static bool ReadBoundBuffer<T>(int requiredLength, ref VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool BindOrAcquireBuffer<T>(BufferID bufferId, int requiredLength, ref VaultGenerationHandle<T> handle, out NativeArray<T> buffer, bool allowAcquire = true)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!allowAcquire)
                return false;

            if (IsHandleCreated(in handle))
            {
                vault.ReleaseBuffer(in handle);
                handle = default;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);
            if (!IsHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            return true;
        }

        private static LaserCutterTuningDTO ReadTuningOrDefaultNoAcquire()
        {
            if (_dataVault != null &&
                ReadBoundBuffer(
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer) &&
                tuningBuffer.IsCreated &&
                tuningBuffer.Length > 0 &&
                tuningBuffer[0].VersionHash != 0UL)
            {
                return tuningBuffer[0];
            }

            return CreateDefaultTuning();
        }

        private static void EnsureTuningSeeded()
        {
            if (!BindOrAcquireBuffer(
                    LaserCutterDodConstants.TuningBuffer,
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length <= 0)
            {
                return;
            }

            LaserCutterTuningDTO tuning = tuningBuffer[0];
            if (tuning.VersionHash == 0UL)
            {
                tuning = CreateDefaultTuning();
                tuningBuffer[0] = tuning;
            }

            _cachedGlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : _cachedGlobalQualityWeight);
        }

        private static LaserCutterTuningDTO CreateDefaultTuning()
        {
            return new LaserCutterTuningDTO
            {
                MinimumPower01 = 0.05f,
                DefaultMaxDistanceMeters = 6f,
                DentRadiusMinMeters = 0.045f,
                DentRadiusMaxMeters = 0.32f,
                GlowLifetimeSeconds = 0.9f,
                BatteryWattsAtPowerOne = 180f,
                CooldownFrames = 2f,
                SparkIntensityScale = 1f,
                LowSparkCount = LaserCutterDodConstants.LowSparkCount,
                UltraSparkCount = LaserCutterDodConstants.UltraSparkCount,
                GlobalQualityWeight = _cachedGlobalQualityWeight,
                Flags = 0u,
                VersionHash = 0x53484C4354554E45UL,
                Reserved0 = 0UL
            };
        }

        private static void StageImpactVfxIfBound(double3 hitAup, float3 normal, float intensity01, float heat01, ushort quantity, uint toolHashID, uint frame)
        {
            if (_dataVault == null ||
                !BindOrAcquireBuffer(
                    LaserCutterDodConstants.ImpactVfxBuffer,
                    LaserCutterDodConstants.MaxHitResults,
                    ref _impactVfxHandle,
                    out NativeArray<LaserCutImpactVfxDTO> impactVfx,
                    allowAcquire: false) ||
                !BindCoreBuffers(out _, out _, out NativeArray<int> requestCount, out _, out _, allowAcquire: false) ||
                !impactVfx.IsCreated ||
                !requestCount.IsCreated ||
                requestCount.Length <= 0)
            {
                return;
            }

            int index = math.clamp(requestCount[0], 0, impactVfx.Length - 1);
            impactVfx[index] = new LaserCutImpactVfxDTO
            {
                CenterAUP = hitAup,
                Normal = SafeNormalize(normal, new float3(0f, 1f, 0f)),
                Intensity01 = math.saturate(intensity01),
                SparkCount = quantity,
                Heat01 = math.saturate(heat01),
                ToolHashID = toolHashID == 0u ? LaserCutterDodConstants.LaserCutterHash : toolHashID,
                Frame = frame,
                Flags = LaserCutterDodConstants.ResultFlagGpuSparkOnly,
                SpeciesHash = LaserCutterDodConstants.SparkSpeciesHash
            };
        }

        private static void PublishDrainSignals(NativeArray<LaserCutBatteryDrainRequest> batteryDrains, int count)
        {
            if (!batteryDrains.IsCreated)
                return;

            int safeCount = math.min(count, batteryDrains.Length);
            for (int i = 0; i < safeCount; i++)
            {
                LaserCutBatteryDrainRequest request = batteryDrains[i];
                if (request.Watts <= 0f || (request.Flags & LaserCutterDodConstants.ResultFlagBatteryDrainQueued) == 0u)
                    continue;

                PowerDrainSignal drain = new PowerDrainSignal
                {
                    ConsumerHash = request.ToolHashID,
                    NetworkHash = request.ParentEntityID,
                    Watts = request.Watts,
                    Progress01 = request.Progress01,
                    Frame = request.Frame,
                    Reason = 0,
                    Flags = 0
                };
                SignalBus<PowerDrainSignal>.TryPushTracked(in drain, ref s_x001LaserCutterDodRuntimeSignalPushDropCount);
            }
        }

        private static void PublishImpactSignals(NativeArray<LaserCutImpactVfxDTO> impactVfx, int count, double3 presentationOriginAup)
        {
            if (!impactVfx.IsCreated)
                return;

            int safeCount = math.min(count, impactVfx.Length);
            for (int i = 0; i < safeCount; i++)
            {
                LaserCutImpactVfxDTO request = impactVfx[i];
                if (request.Intensity01 <= 0f || request.SparkCount == 0u)
                    continue;

                ushort quantity = request.SparkCount > ushort.MaxValue ? ushort.MaxValue : (ushort)request.SparkCount;
                PublishGpuSparkSignals(
                    request.CenterAUP,
                    request.Normal,
                    request.Intensity01,
                    quantity,
                    request.Heat01,
                    request.ToolHashID,
                    0u,
                    request.Frame,
                    presentationOriginAup,
                    false);
            }
        }

        private static void DumpOnNonFinite(NativeArray<LaserCutTelemetryEntry> telemetry, NativeArray<int> telemetryCursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            uint frame = ResolveCurrentFrameId();
            if (_lastDumpFrame == frame)
                return;

            for (int i = 0; i < telemetry.Length; i++)
            {
                if ((telemetry[i].Flags & LaserCutterDodConstants.ResultFlagNonFinite) == 0u)
                    continue;

                int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0
                    ? math.clamp(telemetryCursor[0], 0, telemetry.Length)
                    : 0;
                DumpBlackBox(telemetry, cursor);
                _lastDumpFrame = frame;
                return;
            }
        }

        private static unsafe void DumpBlackBox(NativeArray<LaserCutTelemetryEntry> telemetry, int telemetryCursor)
        {
            try
            {
                int entrySize = UnsafeUtility.SizeOf<LaserCutTelemetryEntry>();
                if (entrySize != LaserCutTelemetryEntrySizeBytes)
                    return;

                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    return;

                Directory.CreateDirectory(directory);

                int entryCount = math.min(telemetry.Length, LaserCutterDodConstants.BlackBoxFrameCount);
                int cursor = math.clamp(telemetryCursor, 0, entryCount);
                int payloadBytes = entryCount * entrySize;
                Span<byte> header = stackalloc byte[BlackBoxDumpHeaderBytes];
                WriteUIntLittleEndian(header.Slice(0, 4), BlackBoxDumpMagic);
                WriteUIntLittleEndian(header.Slice(4, 4), BlackBoxDumpVersion);
                WriteUIntLittleEndian(header.Slice(8, 4), ResolveCurrentFrameId());
                WriteUIntLittleEndian(header.Slice(12, 4), (uint)entryCount);
                WriteUIntLittleEndian(header.Slice(16, 4), (uint)entrySize);
                WriteUIntLittleEndian(header.Slice(20, 4), (uint)cursor);
                WriteUIntLittleEndian(header.Slice(24, 4), _requestSequence);
                WriteUIntLittleEndian(header.Slice(28, 4), (uint)payloadBytes);

                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(header);
                    WriteTelemetryBlock(stream, source, cursor, entryCount - cursor, entrySize);
                    WriteTelemetryBlock(stream, source, 0, cursor, entrySize);
                    stream.Flush(true);
                }
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogException(ex);
            }
        }

        private static unsafe void WriteTelemetryBlock(FileStream stream, byte* source, int start, int count, int entrySize)
        {
            if (count <= 0)
                return;

            stream.Write(new ReadOnlySpan<byte>(source + start * entrySize, count * entrySize));
        }

        private static void WriteUIntLittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private static void RefreshCachedGlobalQualityWeight()
        {
            IDataVault vault = _dataVault;
            if (vault != null &&
                IsHandleCreated(in _scalabilityStateHandle) &&
                vault.TryResolveHandle(in _scalabilityStateHandle, out NativeArray<ScalabilityStateDTO> states) &&
                states.IsCreated &&
                states.Length > 0 &&
                math.isfinite(states[0].GlobalQualityWeight))
            {
                _cachedGlobalQualityWeight = math.saturate(states[0].GlobalQualityWeight);
                return;
            }

            _cachedGlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
        }

        private static bool TryReadCutterSdfSnapshot(
            double3 presentationOriginAup,
            NativeArray<LaserCutRequestDTO> requests,
            int requestCount,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange)
        {
            encodedSdf = default;
            gridDimensions = default;
            volumeOrigin = default;
            cellSize = default;
            sdfRange = 0f;

            if (!requests.IsCreated ||
                requestCount <= 0 ||
                !math.all(math.isfinite(presentationOriginAup)))
            {
                return false;
            }

            float3 runtimeOrigin = float3.zero;
            LaserCutRequestDTO request = requests[0];
            if (math.all(math.isfinite(request.RayOriginAUP)))
                runtimeOrigin = AupPrecisionMath.LocalDeltaFloat3(request.RayOriginAUP, presentationOriginAup, float3.zero);

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel readModel = _cachedVoxelSdfReadModel;
            return readModel != null &&
                   readModel.TryReadNearestSonarSdf(
                       runtimeOrigin,
                       out encodedSdf,
                       out gridDimensions,
                       out volumeOrigin,
                       out cellSize,
                       out sdfRange) &&
                   encodedSdf.IsCreated &&
                   math.all(gridDimensions > 1) &&
                   math.all(math.isfinite(volumeOrigin)) &&
                   math.all(math.isfinite(cellSize)) &&
                   math.isfinite(sdfRange) &&
                   sdfRange > 0.0001f;
        }

        private static float ResolveCutterSdfStepMeters(float sdfRange, in float3 cellSize)
        {
            float quality = Smooth01(_cachedGlobalQualityWeight);
            float3 safeCell = math.max(math.abs(cellSize), new float3(0.0001f));
            float cellStep = math.cmin(safeCell) * math.lerp(2.0f, 0.55f, quality);
            float rangeStep = math.max(0.025f, sdfRange * math.lerp(0.24f, 0.08f, quality));
            return math.max(0.025f, math.min(cellStep, rangeStep));
        }

        private static int ResolveCutterSdfMaxSteps(float qualityWeight)
        {
            float quality = Smooth01(qualityWeight);
            return math.clamp((int)math.round(math.lerp(24f, 96f, quality)), 16, 128);
        }

        private static bool TryReadPresentationOriginAup(out double3 originAup)
        {
            originAup = _cachedPresentationOriginAup;
            if (_hasCachedPresentationOriginAup && math.all(math.isfinite(originAup)))
                return true;

            originAup = double3.zero;
            return false;
        }

        private static bool TryReadScheduledSdfProbePresentationOrigin(out double3 originAup)
        {
            originAup = _scheduledSdfProbePresentationOriginAup;
            if (_hasScheduledSdfProbePresentationOriginAup && math.all(math.isfinite(originAup)))
                return true;

            originAup = double3.zero;
            return false;
        }

        private static bool TryReadScheduledEvaluationPresentationOrigin(out double3 originAup)
        {
            originAup = _scheduledEvaluationPresentationOriginAup;
            if (_hasScheduledEvaluationPresentationOriginAup && math.all(math.isfinite(originAup)))
                return true;

            originAup = double3.zero;
            return false;
        }

        private static void ClearScheduledSdfProbePresentationOrigin()
        {
            _scheduledSdfProbePresentationOriginAup = double3.zero;
            _hasScheduledSdfProbePresentationOriginAup = false;
        }

        private static void ClearScheduledEvaluationPresentationOrigin()
        {
            _scheduledEvaluationPresentationOriginAup = double3.zero;
            _hasScheduledEvaluationPresentationOriginAup = false;
        }

        private static void CacheScalabilityStateHandle()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _scalabilityStateHandle = default;
                return;
            }

            if (IsHandleCreated(in _scalabilityStateHandle) &&
                vault.TryResolveHandle(in _scalabilityStateHandle, out NativeArray<ScalabilityStateDTO> states) &&
                states.IsCreated &&
                states.Length > 0)
            {
                return;
            }

            _scalabilityStateHandle = vault.TryGetGenerationHandle(
                BufferID.ShinobuScalabilityState,
                out VaultGenerationHandle<ScalabilityStateDTO> handle)
                ? handle
                : default;
        }

        private static void IncrementSuppressed(NativeArray<LaserCutterCountersDTO> counters, uint frame)
        {
            if (!counters.IsCreated || counters.Length <= 0)
                return;

            LaserCutterCountersDTO counter = counters[0];
            counter.SuppressedCount++;
            counter.LastFrame = frame;
            counters[0] = counter;
        }

        private static void SuppressQueuedRequests(uint frame)
        {
            if (!BindCoreBuffers(
                    out _,
                    out _,
                    out NativeArray<int> requestCount,
                    out _,
                    out NativeArray<LaserCutterCountersDTO> counters,
                    allowAcquire: false) ||
                !requestCount.IsCreated ||
                requestCount.Length <= 0)
            {
                return;
            }

            if (requestCount[0] > 0)
                IncrementSuppressed(counters, frame);

            requestCount[0] = 0;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ClearHandles()
        {
            _requestsHandle = default;
            _requestMetasHandle = default;
            _requestCountHandle = default;
            _sdfProbeHitsHandle = default;
            _hitResultsHandle = default;
            _deformationHandle = default;
            _batteryDrainHandle = default;
            _glowDecalHandle = default;
            _impactVfxHandle = default;
            _cooldownHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _specHandle = default;
            _csvScratchHandle = default;
            _countersHandle = default;
            _scalabilityStateHandle = default;
            _cachedVoxelSdfReadModel = null;
            _scheduledSdfProbeHandle = default;
            _scheduledSdfProbeActive = false;
            _scheduledSdfProbeCount = 0;
            _scheduledEvaluationHandle = default;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;
            _cachedGlobalQualityWeight = 1f;
            _cachedPresentationOriginAup = double3.zero;
            _hasCachedPresentationOriginAup = false;
            ClearScheduledSdfProbePresentationOrigin();
            ClearScheduledEvaluationPresentationOrigin();
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            DispatcherJobFence.TryComplete(ref _scheduledSdfProbeHandle, forceComplete: true);
            DispatcherJobFence.TryComplete(ref _scheduledEvaluationHandle, forceComplete: true);
            _scheduledSdfProbeActive = false;
            _scheduledSdfProbeCount = 0;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;

            ReleaseVaultHandle(vault, ref _requestsHandle);
            ReleaseVaultHandle(vault, ref _requestMetasHandle);
            ReleaseVaultHandle(vault, ref _requestCountHandle);
            ReleaseVaultHandle(vault, ref _sdfProbeHitsHandle);
            ReleaseVaultHandle(vault, ref _hitResultsHandle);
            ReleaseVaultHandle(vault, ref _deformationHandle);
            ReleaseVaultHandle(vault, ref _batteryDrainHandle);
            ReleaseVaultHandle(vault, ref _glowDecalHandle);
            ReleaseVaultHandle(vault, ref _impactVfxHandle);
            ReleaseVaultHandle(vault, ref _cooldownHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _specHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _countersHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 0f);
            return math.smoothstep(0f, 1f, t);
        }

        private static uint ResolveCurrentFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
        }
    }
}
