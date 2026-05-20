namespace Hecton8.Tools
{
    using System;
    using System.IO;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public static class LaserCutterDodRuntime
    {
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<LaserCutRequestDTO> _requestsHandle;
        private static VaultGenerationHandle<LaserCutRequestMetaDTO> _requestMetasHandle;
        private static VaultGenerationHandle<int> _requestCountHandle;
        private static VaultGenerationHandle<RaycastCommand> _raycastCommandsHandle;
        private static VaultGenerationHandle<RaycastHit> _raycastHitsHandle;
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

        private static JobHandle _scheduledRaycastHandle;
        private static bool _scheduledRaycastActive;
        private static int _scheduledRaycastCount;
        private static JobHandle _scheduledEvaluationHandle;
        private static bool _scheduledEvaluationActive;
        private static int _scheduledEvaluationCount;
        private static uint _scheduledEvaluationCursorBase;
        private static uint _requestSequence;
        private static uint _lastDumpFrame;
        private static float _cachedGlobalQualityWeight = 1f;

        public static bool EnsureInitialized(IDataVault explicitVault = null)
        {
            IDataVault vault = explicitVault ?? GlobalRegistry.DataVault;
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

            bool ready = TryResolveCoreBuffers(out _, out _, out _, out _, out _);
            if (ready)
                RefreshCachedGlobalQualityWeight();

            return ready;
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
                _scheduledRaycastActive ||
                _scheduledEvaluationActive ||
                !TryResolveCoreBuffers(
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
            if (!EnsureInitialized() ||
                _scheduledRaycastActive ||
                _scheduledEvaluationActive ||
                !TryResolveCoreBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<int> _,
                    out NativeArray<LaserCutterCountersDTO> counters) ||
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
                MaximumDistanceMeters = ResolveTuning().DefaultMaxDistanceMeters
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
        }

        public static bool TryScheduleRaycastBatch(int layerMask, QueryTriggerInteraction queryTriggerInteraction, uint frame)
        {
            if (_scheduledRaycastActive ||
                _scheduledEvaluationActive)
            {
                return false;
            }

            if (_dataVault == null && !EnsureInitialized())
                return false;

            RefreshCachedGlobalQualityWeight();

            if (!TryResolveSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out NativeArray<RaycastCommand> commands,
                    out NativeArray<RaycastHit> hits,
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

            int scheduledCount = math.clamp(requestCount[0], 0, math.min(math.min(requests.Length, requestMetas.Length), commands.Length));
            scheduledCount = math.min(scheduledCount, hits.Length);
            scheduledCount = math.min(scheduledCount, cooldowns.Length);
            if (scheduledCount <= 0)
                return false;

            LaserCutterTuningDTO tuning = ResolveTuningNoAcquire();
            uint cooldownFrames = (uint)math.max(1f, math.isfinite(tuning.CooldownFrames) ? tuning.CooldownFrames : 1f);
            ManageCutterCooldownJob cooldownJob = new ManageCutterCooldownJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                Cooldowns = cooldowns,
                Frame = frame,
                CooldownFrames = cooldownFrames
            };

            BuildCutterRaycastsJob buildJob = new BuildCutterRaycastsJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                Commands = commands,
                PresentationOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                LayerMask = layerMask,
                HitTriggers = queryTriggerInteraction == QueryTriggerInteraction.Collide ? (byte)1 : (byte)0
            };

            JobHandle cooldownHandle = cooldownJob.Schedule(scheduledCount, LaserCutterDodConstants.MinCommandsPerJob);
            JobHandle buildHandle = buildJob.Schedule(scheduledCount, LaserCutterDodConstants.MinCommandsPerJob, cooldownHandle);
            _scheduledRaycastHandle = RaycastCommand.ScheduleBatch(
                commands.GetSubArray(0, scheduledCount),
                hits.GetSubArray(0, scheduledCount),
                LaserCutterDodConstants.MinCommandsPerJob,
                buildHandle);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _scheduledRaycastHandle);
            _scheduledRaycastActive = true;
            _scheduledRaycastCount = scheduledCount;
            return true;
        }

        public static bool TryCompleteScheduledRaycastsAndEvaluate(float heat01)
        {
            if (_scheduledEvaluationActive)
                return TryFinalizeScheduledEvaluation();

            if (!_scheduledRaycastActive)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledRaycastHandle))
                return false;

            if (!TryResolveSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> requestMetas,
                    out NativeArray<int> requestCount,
                    out _,
                    out NativeArray<RaycastHit> hits,
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
                _scheduledRaycastActive = false;
                _scheduledRaycastCount = 0;
                return false;
            }

            int count = math.clamp(_scheduledRaycastCount, 0, math.min(math.min(requests.Length, requestMetas.Length), hits.Length));
            if (count <= 0)
            {
                requestCount[0] = 0;
                _scheduledRaycastActive = false;
                _scheduledRaycastCount = 0;
                return false;
            }

            uint cursorBase = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? (uint)math.max(0, telemetryCursor[0]) : 0u;
            LaserCutterTuningDTO tuning = ResolveTuningNoAcquire();
            EvaluateCutterRaycastHitsJob evaluateJob = new EvaluateCutterRaycastHitsJob
            {
                Requests = requests,
                RequestMetas = requestMetas,
                RaycastHits = hits,
                HitResults = hitResults,
                DeformationStates = deformations,
                BatteryDrainRequests = batteryDrains,
                GlowDecalRequests = decals,
                ImpactVfxRequests = impactVfx,
                TelemetryRing = telemetry,
                PresentationOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble,
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
            _scheduledRaycastActive = false;
            _scheduledRaycastCount = 0;
            return false;
        }

        private static bool TryFinalizeScheduledEvaluation()
        {
            if (!_scheduledEvaluationActive)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledEvaluationHandle))
                return false;

            if (!TryResolveSchedulerBuffers(
                    out NativeArray<LaserCutRequestDTO> requests,
                    out NativeArray<LaserCutRequestMetaDTO> _,
                    out NativeArray<int> requestCount,
                    out _,
                    out NativeArray<RaycastHit> hits,
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
                return false;
            }

            int count = math.clamp(_scheduledEvaluationCount, 0, math.min(requests.Length, hits.Length));
            if (count <= 0)
            {
                requestCount[0] = 0;
                _scheduledEvaluationActive = false;
                _scheduledEvaluationCount = 0;
                _scheduledEvaluationCursorBase = 0u;
                return false;
            }

            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = (int)((_scheduledEvaluationCursorBase + (uint)count) % (uint)LaserCutterDodConstants.BlackBoxFrameCount);

            PublishDrainSignals(batteryDrains, count);
            PublishImpactSignals(impactVfx, count);
            DumpOnNonFinite(telemetry);
            requestCount[0] = 0;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;
            return true;
        }

        public static void StageGpuSparkSignal(double3 hitAup, float3 normal, float heat01, float cuttingPower01, uint toolHashID, uint parentEntityID, uint frame)
        {
            LaserCutterTuningDTO tuning = ResolveTuningNoAcquire();
            float quality = Smooth01(_cachedGlobalQualityWeight);
            float intensity = math.saturate((0.35f + heat01 * 0.65f) * (0.55f + cuttingPower01 * 0.45f));
            float lowSparkCount = math.max(0f, math.isfinite(tuning.LowSparkCount) ? tuning.LowSparkCount : LaserCutterDodConstants.LowSparkCount);
            float ultraSparkCount = math.max(lowSparkCount, math.isfinite(tuning.UltraSparkCount) ? tuning.UltraSparkCount : LaserCutterDodConstants.UltraSparkCount);
            float sparkScale = math.max(0f, math.isfinite(tuning.SparkIntensityScale) ? tuning.SparkIntensityScale : 1f);
            ushort quantity = (ushort)math.clamp(
                (int)math.round(math.lerp(lowSparkCount, ultraSparkCount, quality) * intensity * sparkScale),
                0,
                ushort.MaxValue);

            PublishGpuSparkSignals(hitAup, normal, intensity, quantity, heat01, toolHashID, parentEntityID, frame, true);
        }

        private static void PublishGpuSparkSignals(double3 hitAup, float3 normal, float intensity01, ushort quantity, float heat01, uint toolHashID, uint parentEntityID, uint frame, bool stageImpactVfx)
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
            SignalBus<DebrisSpawnSignal>.TryPush(in debris);

            VfxSparkRequestSignal spark = new VfxSparkRequestSignal
            {
                HitPoint = AupPrecisionMath.LocalDeltaFloat3(hitAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero),
                Normal = SafeNormalize(normal, new float3(0f, 1f, 0f)),
                MaterialHash = LaserCutterDodConstants.LaserCutterHash,
                ToolHash = toolHashID == 0u ? LaserCutterDodConstants.LaserCutterHash : toolHashID,
                Intensity01 = intensity,
                Frame = frame
            };
            SignalBus<VfxSparkRequestSignal>.TryPush(in spark);

            if (stageImpactVfx)
                TryStageImpactVfx(hitAup, normal, intensity, heat01, quantity, toolHashID, frame);
        }

        public static bool TryGetLatestTelemetry(out LaserCutTelemetryEntry entry)
        {
            entry = default;
            if (!EnsureInitialized() ||
                !TryResolveOrAcquire(
                    LaserCutterDodConstants.TelemetryRingBuffer,
                    LaserCutterDodConstants.BlackBoxFrameCount,
                    ref _telemetryRingHandle,
                    out NativeArray<LaserCutTelemetryEntry> telemetry) ||
                !TryResolveOrAcquire(
                    LaserCutterDodConstants.TelemetryCursorBuffer,
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
            if (!EnsureInitialized() ||
                !TryResolveOrAcquire(
                    LaserCutterDodConstants.TuningBuffer,
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer[0];
            if (tuning.VersionHash == 0UL)
            {
                tuning = CreateDefaultTuning();
                tuningBuffer[0] = tuning;
            }

            return true;
        }

        public static bool TrySetTuning(in LaserCutterTuningDTO tuning)
        {
            if (!EnsureInitialized() ||
                !TryResolveOrAcquire(
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
            if (!EnsureInitialized() ||
                !TryResolveCoreBuffers(
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
            if (!EnsureInitialized() ||
                !TryResolveOrAcquire(
                    LaserCutterDodConstants.HitResultsBuffer,
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

        internal static bool TryResolveSpecBuffer(out NativeArray<LaserCutterSpecDTO> specs)
        {
            specs = default;
            return EnsureInitialized() &&
                   TryResolveOrAcquire(
                       LaserCutterDodConstants.SpecBuffer,
                       LaserCutterDodConstants.CsvSpecCapacity,
                       ref _specHandle,
                       out specs);
        }

        internal static bool TryResolveCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            return EnsureInitialized() &&
                   TryResolveOrAcquire(
                       LaserCutterDodConstants.CsvScratchBuffer,
                       LaserCutterDodConstants.CsvScratchByteCapacity,
                       ref _csvScratchHandle,
                       out scratch);
        }

        private static bool TryResolveCoreBuffers(
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
            return TryResolveOrAcquire(LaserCutterDodConstants.RequestsBuffer, LaserCutterDodConstants.MaxRequests, ref _requestsHandle, out requests, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.RequestMetaBuffer, LaserCutterDodConstants.MaxRequests, ref _requestMetasHandle, out requestMetas, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.RequestCountBuffer, 1, ref _requestCountHandle, out requestCount, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.TelemetryCursorBuffer, 1, ref _telemetryCursorHandle, out telemetryCursor, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.CountersBuffer, 1, ref _countersHandle, out counters, allowAcquire);
        }

        private static bool TryResolveSchedulerBuffers(
            out NativeArray<LaserCutRequestDTO> requests,
            out NativeArray<LaserCutRequestMetaDTO> requestMetas,
            out NativeArray<int> requestCount,
            out NativeArray<RaycastCommand> commands,
            out NativeArray<RaycastHit> hits,
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
            commands = default;
            hits = default;
            cooldowns = default;
            hitResults = default;
            deformations = default;
            batteryDrains = default;
            decals = default;
            impactVfx = default;
            telemetry = default;
            return TryResolveCoreBuffers(out requests, out requestMetas, out requestCount, out telemetryCursor, out _, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.RaycastCommandsBuffer, LaserCutterDodConstants.MaxRequests, ref _raycastCommandsHandle, out commands, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.RaycastHitsBuffer, LaserCutterDodConstants.MaxRequests, ref _raycastHitsHandle, out hits, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.CooldownBuffer, LaserCutterDodConstants.MaxRequests, ref _cooldownHandle, out cooldowns, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.HitResultsBuffer, LaserCutterDodConstants.MaxHitResults, ref _hitResultsHandle, out hitResults, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.DeformationBuffer, LaserCutterDodConstants.MaxHitResults, ref _deformationHandle, out deformations, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.BatteryDrainBuffer, LaserCutterDodConstants.MaxHitResults, ref _batteryDrainHandle, out batteryDrains, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.GlowDecalBuffer, LaserCutterDodConstants.MaxHitResults, ref _glowDecalHandle, out decals, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.ImpactVfxBuffer, LaserCutterDodConstants.MaxHitResults, ref _impactVfxHandle, out impactVfx, allowAcquire) &&
                   TryResolveOrAcquire(LaserCutterDodConstants.TelemetryRingBuffer, LaserCutterDodConstants.BlackBoxFrameCount, ref _telemetryRingHandle, out telemetry, allowAcquire);
        }

        private static bool TryResolveOrAcquire<T>(BufferID bufferId, int requiredLength, ref VaultGenerationHandle<T> handle, out NativeArray<T> buffer, bool allowAcquire = true)
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

            VaultGenerationHandle<T> acquired = vault.GetGenerationHandle<T>(
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

        private static LaserCutterTuningDTO ResolveTuning()
        {
            return TryGetTuning(out LaserCutterTuningDTO tuning) ? tuning : CreateDefaultTuning();
        }

        private static LaserCutterTuningDTO ResolveTuningNoAcquire()
        {
            if (_dataVault != null &&
                TryResolveOrAcquire(
                    LaserCutterDodConstants.TuningBuffer,
                    1,
                    ref _tuningHandle,
                    out NativeArray<LaserCutterTuningDTO> tuningBuffer,
                    allowAcquire: false) &&
                tuningBuffer.IsCreated &&
                tuningBuffer.Length > 0 &&
                tuningBuffer[0].VersionHash != 0UL)
            {
                return tuningBuffer[0];
            }

            return CreateDefaultTuning();
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

        private static void TryStageImpactVfx(double3 hitAup, float3 normal, float intensity01, float heat01, ushort quantity, uint toolHashID, uint frame)
        {
            if (_dataVault == null ||
                !TryResolveOrAcquire(
                    LaserCutterDodConstants.ImpactVfxBuffer,
                    LaserCutterDodConstants.MaxHitResults,
                    ref _impactVfxHandle,
                    out NativeArray<LaserCutImpactVfxDTO> impactVfx,
                    allowAcquire: false) ||
                !TryResolveCoreBuffers(out _, out _, out NativeArray<int> requestCount, out _, out _, allowAcquire: false) ||
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
                SignalBus<PowerDrainSignal>.TryPush(in drain);
            }
        }

        private static void PublishImpactSignals(NativeArray<LaserCutImpactVfxDTO> impactVfx, int count)
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
                    false);
            }
        }

        private static void DumpOnNonFinite(NativeArray<LaserCutTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            uint frame = unchecked((uint)Time.frameCount);
            if (_lastDumpFrame == frame)
                return;

            for (int i = 0; i < telemetry.Length; i++)
            {
                if ((telemetry[i].Flags & LaserCutterDodConstants.ResultFlagNonFinite) == 0u)
                    continue;

                DumpBlackBox(telemetry);
                _lastDumpFrame = frame;
                return;
            }
        }

        private static void DumpBlackBox(NativeArray<LaserCutTelemetryEntry> telemetry)
        {
            try
            {
                string assetsPath = Application.dataPath;
                DirectoryInfo projectRoot = Directory.GetParent(assetsPath);
                if (projectRoot == null)
                    return;

                string directory = Path.Combine(projectRoot.FullName, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_SHINOBU_225.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(telemetry.Length);
                    writer.Write(_requestSequence);
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        LaserCutTelemetryEntry entry = telemetry[i];
                        writer.Write(entry.RayOriginAUP.x);
                        writer.Write(entry.RayOriginAUP.y);
                        writer.Write(entry.RayOriginAUP.z);
                        writer.Write(entry.HitAUP.x);
                        writer.Write(entry.HitAUP.y);
                        writer.Write(entry.HitAUP.z);
                        writer.Write(entry.RayDirection.x);
                        writer.Write(entry.RayDirection.y);
                        writer.Write(entry.RayDirection.z);
                        writer.Write(entry.DistanceMeters);
                        writer.Write(entry.CuttingPower);
                        writer.Write(entry.QualityWeight);
                        writer.Write(entry.Frame);
                        writer.Write(entry.RequestSequence);
                        writer.Write(entry.ToolHashID);
                        writer.Write(entry.ParentEntityID);
                        writer.Write(entry.ColliderInstanceID);
                        writer.Write(entry.Flags);
                        writer.Write(entry.SparkCount);
                        writer.Write(entry.CooldownUntilFrame);
                        writer.Write(entry.LayoutMagic);
                        writer.Write(entry.Heat01);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.BatteryWatts);
                        writer.Write(entry.BurstWorkEstimateMicros);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SHINOBU_225] Failed to dump cutter black box: " + ex.Message);
            }
        }

        private static void RefreshCachedGlobalQualityWeight()
        {
            IDataVault vault = _dataVault;
            if (vault != null &&
                vault.TryGetGenerationHandle(BufferID.ShinobuScalabilityState, out VaultGenerationHandle<ScalabilityStateDTO> handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<ScalabilityStateDTO> states) &&
                states.IsCreated &&
                states.Length > 0 &&
                math.isfinite(states[0].GlobalQualityWeight))
            {
                _cachedGlobalQualityWeight = math.saturate(states[0].GlobalQualityWeight);
                return;
            }

            _cachedGlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
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

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ClearHandles()
        {
            _requestsHandle = default;
            _requestMetasHandle = default;
            _requestCountHandle = default;
            _raycastCommandsHandle = default;
            _raycastHitsHandle = default;
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
            _scheduledRaycastHandle = default;
            _scheduledRaycastActive = false;
            _scheduledRaycastCount = 0;
            _scheduledEvaluationHandle = default;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;
            _cachedGlobalQualityWeight = 1f;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            DispatcherJobFence.TryComplete(ref _scheduledRaycastHandle, forceComplete: true);
            DispatcherJobFence.TryComplete(ref _scheduledEvaluationHandle, forceComplete: true);
            _scheduledRaycastActive = false;
            _scheduledRaycastCount = 0;
            _scheduledEvaluationActive = false;
            _scheduledEvaluationCount = 0;
            _scheduledEvaluationCursorBase = 0u;

            ReleaseVaultHandle(vault, ref _requestsHandle);
            ReleaseVaultHandle(vault, ref _requestMetasHandle);
            ReleaseVaultHandle(vault, ref _requestCountHandle);
            ReleaseVaultHandle(vault, ref _raycastCommandsHandle);
            ReleaseVaultHandle(vault, ref _raycastHitsHandle);
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

        private static ulong Mix(ulong hash, uint value)
        {
            return (hash ^ value) * 1099511628211UL;
        }
    }
}
