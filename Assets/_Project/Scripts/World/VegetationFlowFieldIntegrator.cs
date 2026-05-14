using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        public bool TryGetEcosystemFlowFieldPayload(
            out NativeArray<float2> flowVectors,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            flowVectors = _nativeMemory.EcosystemFlowFieldCurrentNative;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemFlowFieldCenter;
            cellSize = threatGridCellSize;
            return _flowFieldInitialized &&
                   flowVectors.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Registers one short-lived wake impulse that will be folded into the next abyssal flow-field solve.
        /// </summary>
        public void RegisterSwarmWakeImpulse(Vector3 positionWS, Vector3 flowVectorWS, float radiusMeters, float lifetimeSeconds)
        {
            EnsureFlowFieldBuffers();
            float strength = EstimateLength3D(flowVectorWS);
            if (strength <= 0.0001f)
            {
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
                if (_nativeMemory.SwarmWakeImpulseNative.IsCreated)
                    _nativeMemory.SwarmWakeImpulseNative[0] = default;
                return;
            }

            _nativeMemory.SwarmWakeImpulseNative[0] = new SwarmWakeImpulse
            {
                Position = new float3(positionWS.x, positionWS.y, positionWS.z),
                Radius = math.max(0.1f, radiusMeters),
                FlowVector = new float3(flowVectorWS.x, flowVectorWS.y, flowVectorWS.z),
                Strength = strength
            };
            _swarmWakeImpulseCount = 1;
            _swarmWakeImpulseExpireTime = Time.unscaledTime + math.max(0.1f, lifetimeSeconds);
        }

        private static float2 DominantAxisOrDefault(float2 value, float2 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(value)))
                return math.all(math.isfinite(fallback)) ? fallback : float2.zero;

            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            return ax >= ay
                ? new float2(math.select(-1f, 1f, value.x >= 0f), 0f)
                : new float2(0f, math.select(-1f, 1f, value.y >= 0f));
        }

        private static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(value)))
                return math.all(math.isfinite(fallback)) ? fallback : float3.zero;

            float3 absValue = math.abs(value);
            if (absValue.x >= absValue.y && absValue.x >= absValue.z)
                return new float3(math.select(-1f, 1f, value.x >= 0f), 0f, 0f);

            if (absValue.y >= absValue.z)
                return new float3(0f, math.select(-1f, 1f, value.y >= 0f), 0f);

            return new float3(0f, 0f, math.select(-1f, 1f, value.z >= 0f));
        }

        private static float ApproxMagnitude2(float2 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float hi = math.max(ax, ay);
            float lo = math.min(ax, ay);
            return hi + (lo * 0.375f);
        }

        private static float InverseLerpSpeedSq(float minSpeed, float maxSpeed, float speedSq)
        {
            float minSq = minSpeed * minSpeed;
            float maxSq = maxSpeed * maxSpeed;
            float inverseRangeSq = math.rcp(math.max(0.000001f, maxSq - minSq));
            return math.saturate((math.max(0f, speedSq) - minSq) * inverseRangeSq);
        }

        private static float LerpClamped(float a, float b, float t)
        {
            return math.lerp(a, b, math.saturate(t));
        }

        private static float ResolveCheapRetention(float decay)
        {
            decay = math.max(0f, decay);
            float decaySq = decay * decay;
            return math.saturate(math.rcp(1f + decay + (decaySq * 0.48f) + (decaySq * decay * 0.235f)));
        }

        /// <summary>
        /// Returns the current abyssal thermal-grid payload and metadata for survival and environment consumers.
        /// </summary>
        public bool TryGetAbyssalThermalGridPayload(
            out NativeArray<float> temperatures,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            temperatures = _nativeMemory.AbyssalThermalGridNative;
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalThermalGridInitialized &&
                   temperatures.IsCreated &&
                   horizontalResolution > 0 &&
                   verticalResolution > 0 &&
                   horizontalCellSize > 0f &&
                   verticalCellSize > 0f;
        }

        /// <summary>
        /// Returns the current 3D abyssal flow-volume payload and metadata for current-driven deep-ocean consumers.
        /// </summary>
        public bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3> flowVectors,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            flowVectors = _nativeMemory.AbyssalFlowVolumeCurrentNative;
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalFlowVolumeInitialized &&
                   flowVectors.IsCreated &&
                   horizontalResolution > 0 &&
                   verticalResolution > 0 &&
                   horizontalCellSize > 0f &&
                   verticalCellSize > 0f;
        }

        /// <summary>
        /// Returns the current read-only abyssal flow volume and ring-buffer metadata for Burst consumers.
        /// </summary>
        public bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3> flowVolume,
            out Vector3 center,
            out int resolutionXZ,
            out int resolutionY,
            out int ringOffsetX,
            out int ringOffsetY,
            out int ringOffsetZ,
            out float horizontalCellSize,
            out float verticalCellSize,
            out float surfaceY,
            out float depthMeters)
        {
            flowVolume = _nativeMemory.AbyssalFlowVolumeCurrentNative;
            center = _abyssalThermalGridCenter;
            resolutionXZ = _abyssalThermalGridResolutionXZ;
            resolutionY = _abyssalThermalGridResolutionY;
            ringOffsetX = _abyssalThermalGridRingOffsetX;
            ringOffsetY = _abyssalThermalGridRingOffsetY;
            ringOffsetZ = _abyssalThermalGridRingOffsetZ;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            surfaceY = waterLevel;
            depthMeters = thermalGridDepthMeters;

            return _abyssalFlowVolumeInitialized &&
                   flowVolume.IsCreated &&
                   resolutionXZ > 1 &&
                   resolutionY > 1 &&
                   horizontalCellSize > 0f &&
                   verticalCellSize > 0f;
        }

        /// <summary>
        /// Returns the current mega-wreck section streaming payload for composite-structure consumers.
        /// </summary>

        private void PrepareFlowFieldSamplingSnapshot(Vector3 flowCenter)
        {
            EnsureThreatGridBuffers();
            EnsureFloat3Capacity(ref _nativeMemory.FlowSamplingDensityGridNative, math.max(1, _threatSamplingChunkCount * DensityGridCellCount));
            EnsureFloatNativeCapacity(ref _nativeMemory.FlowNavSupportGridNative, math.max(1, _ecosystemThreatGridCellCount));
            ClearFloatGrid(_nativeMemory.FlowNavSupportGridNative, _ecosystemThreatGridCellCount);

            if (_threatSamplingChunkCount <= 0 ||
                !_nativeMemory.DensityQueryGridNative.IsCreated ||
                _densityQueryChunkCount <= 0)
            {
                return;
            }

            NativeArray<float3>.Copy(_nativeMemory.DensityQueryGridNative, _nativeMemory.FlowSamplingDensityGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            BuildFlowFieldNavSupportGrid(flowCenter);
        }

        private void CompleteThreatPropagationJob(bool forceComplete)
        {
            if (!_threatPropagationScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _threatPropagationHandle, forceComplete))
                return;

            NativeArray<float> threatSwap = _nativeMemory.EcosystemThreatGridCurrentNative;
            _nativeMemory.EcosystemThreatGridCurrentNative = _nativeMemory.EcosystemThreatGridNextNative;
            _nativeMemory.EcosystemThreatGridNextNative = threatSwap;
            NativeArray<byte> threatCompressedSwap = _nativeMemory.EcosystemThreatGridCompressedCurrentNative;
            _nativeMemory.EcosystemThreatGridCompressedCurrentNative = _nativeMemory.EcosystemThreatGridCompressedNextNative;
            _nativeMemory.EcosystemThreatGridCompressedNextNative = threatCompressedSwap;
            NativeArray<byte> threatVoxelSwap = _nativeMemory.EcosystemThreatVoxelCurrentNative;
            _nativeMemory.EcosystemThreatVoxelCurrentNative = _nativeMemory.EcosystemThreatVoxelNextNative;
            _nativeMemory.EcosystemThreatVoxelNextNative = threatVoxelSwap;
            NativeArray<byte> echoSwap = _nativeMemory.EcosystemThreatEchoCurrentNative;
            _nativeMemory.EcosystemThreatEchoCurrentNative = _nativeMemory.EcosystemThreatEchoNextNative;
            _nativeMemory.EcosystemThreatEchoNextNative = echoSwap;
            _ecosystemThreatGridCenter = _scheduledThreatGridCenter;
            _ecosystemThreatVoxelOrigin = _scheduledThreatVoxelOrigin;
            _threatGridInitialized = true;
            _threatPropagationScheduled = false;
            if (InvalidateChunksForNewPermanentEchoes())
                RefreshResidency();
            UpdateThreatHotspot();
        }

        private void ScheduleThreatPropagationJob()
        {
            if (_threatPropagationScheduled)
                return;

            EnsureThreatGridBuffers();
            if (!HasValidThreatGridConfiguration())
                return;

            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            Vector3 targetCenter = hasPlayerRuntimePosition
                ? playerRuntimePosition
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);
            Vector3 previousCenter = _threatGridInitialized ? _ecosystemThreatGridCenter : targetCenter;
            ResolveThreatSignalSnapshot(out Vector3 emissionPosition, out float emissionRadius, out float emissionStrength);

            float deltaTime = 0.5f;
            if (_lastThreatPropagationTime > float.NegativeInfinity)
                deltaTime = math.clamp(Time.time - _lastThreatPropagationTime, 0.05f, 5f);

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int shiftX = (int)math.round((targetCenter.x - previousCenter.x) * inverseThreatGridCellSize);
            int shiftZ = (int)math.round((targetCenter.z - previousCenter.z) * inverseThreatGridCellSize);
            float halfExtent = (_ecosystemThreatGridResolution - 1) * 0.5f * threatGridCellSize;
            Vector3 voxelOrigin = new Vector3(
                targetCenter.x - halfExtent,
                waterLevel - thermalGridDepthMeters,
                targetCenter.z - halfExtent);

            var job = new ThreatPropagationJob
            {
                CurrentThreat = _nativeMemory.EcosystemThreatGridCurrentNative,
                NextThreat = _nativeMemory.EcosystemThreatGridNextNative,
                NextThreatCompressed = _nativeMemory.EcosystemThreatGridCompressedNextNative,
                CurrentEchoFlags = _nativeMemory.EcosystemThreatEchoCurrentNative,
                NextEchoFlags = _nativeMemory.EcosystemThreatEchoNextNative,
                ThreatChunks = _nativeMemory.ThreatSamplingChunksNative,
                ThreatAttractorGrid = _nativeMemory.ThreatSamplingAttractorGridNative,
                ArtificialStructures = _nativeMemory.ArtificialStructureRecordsNative,
                ArtificialStructureHash = _nativeMemory.ArtificialStructureHashFrontNative,
                GridResolution = _ecosystemThreatGridResolution,
                ThreatChunkCount = _threatSamplingChunkCount,
                CellSize = threatGridCellSize,
                DeltaTime = deltaTime,
                Diffusion = threatDiffusion,
                DecayPerSecond = threatDecayPerSecond,
                SargassumRetentionBoost = threatSargassumRetentionBoost,
                TechnoJungleRetentionBoost = threatTechnoJungleRetentionBoost,
                SargassumAccumulationBoost = threatSargassumAccumulationBoost,
                TechnoJungleAccumulationBoost = threatTechnoJungleAccumulationBoost,
                StructureThreatSuppression = artificialStructureThreatSuppression,
                StructureHazardAttraction = artificialStructureHazardAttraction,
                PermanentEchoFloor = permanentThreatEchoFloor,
                PermanentEchoThreshold = permanentThreatEchoThreshold,
                EmissionPosition = new float3(emissionPosition.x, emissionPosition.y, emissionPosition.z),
                GridCenter = new float3(targetCenter.x, targetCenter.y, targetCenter.z),
                EmissionRadius = emissionRadius,
                EmissionStrength = emissionStrength,
                ShiftX = shiftX,
                ShiftZ = shiftZ
            };

            _scheduledThreatGridCenter = targetCenter;
            _scheduledThreatVoxelOrigin = voxelOrigin;
            _lastThreatPropagationTime = Time.time;
            JobHandle propagationHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);
            var voxelJob = new ThreatVoxelizationJob
            {
                ThreatGrid = _nativeMemory.EcosystemThreatGridNextNative,
                DensityChunks = _nativeMemory.ThreatSamplingChunksNative,
                DensityGrid = _nativeMemory.DensityQueryGridNative,
                ThreatAttractorGrid = _nativeMemory.ThreatSamplingAttractorGridNative,
                ChunkHash = _nativeMemory.ThreatSamplingChunkHashFrontNative,
                ArtificialStructures = _nativeMemory.ArtificialStructureRecordsNative,
                ArtificialStructureHash = _nativeMemory.ArtificialStructureHashFrontNative,
                Output = _nativeMemory.EcosystemThreatVoxelNextNative,
                GridResolutionXZ = _ecosystemThreatGridResolution,
                GridResolutionY = _ecosystemThreatGridResolutionY,
                CellSizeXZ = threatGridCellSize,
                CellSizeY = thermalGridVerticalCellSize,
                GridOrigin = new float3(voxelOrigin.x, voxelOrigin.y, voxelOrigin.z),
                GridCenter = new float3(targetCenter.x, targetCenter.y, targetCenter.z),
                KelpObstacleWeight = flowFieldKelpObstacleWeight,
                SargassumObstacleWeight = flowFieldSargassumObstacleWeight,
                TechnoObstacleWeight = flowFieldTechnoObstacleWeight,
                ObstacleHardThreshold = flowFieldObstacleHardThreshold
            };
            _threatPropagationHandle = voxelJob.Schedule(_ecosystemThreatVoxelCellCount, DefaultJobBatchSize, propagationHandle);
            _threatPropagationScheduled = true;
        }

        private void EnsureFlowFieldBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (!_nativeMemory.EcosystemFlowFieldCurrentNative.IsCreated || _nativeMemory.EcosystemFlowFieldCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemFlowFieldCurrentNative);
                // COLD ALLOC: NativeArray<float2>[_ecosystemThreatGridCellCount] - abyssal flow-field front buffer for read-only navigation sampling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemFlowFieldCurrentNative = new NativeArray<float2>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.EcosystemFlowFieldCurrentNative, NativeMemoryOwner, nameof(_nativeMemory.EcosystemFlowFieldCurrentNative), NativeMemoryLifetime);
                _flowFieldInitialized = false;
            }

            if (!_nativeMemory.EcosystemFlowFieldNextNative.IsCreated || _nativeMemory.EcosystemFlowFieldNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemFlowFieldNextNative);
                // COLD ALLOC: NativeArray<float2>[_ecosystemThreatGridCellCount] - abyssal flow-field back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemFlowFieldNextNative = new NativeArray<float2>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.EcosystemFlowFieldNextNative, NativeMemoryOwner, nameof(_nativeMemory.EcosystemFlowFieldNextNative), NativeMemoryLifetime);
            }

            if (!_nativeMemory.SwarmWakeImpulseNative.IsCreated)
            {
                // COLD ALLOC: NativeArray<SwarmWakeImpulse>[1] - single-slot boid wake impulse injected into abyssal flow-field solves - owner: HectonMapMagicVegetationBridge
                _nativeMemory.SwarmWakeImpulseNative = new NativeArray<SwarmWakeImpulse>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.SwarmWakeImpulseNative, NativeMemoryOwner, nameof(_nativeMemory.SwarmWakeImpulseNative), NativeMemoryLifetime);
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
            }
        }

        private void EnsureThermalGridBuffers()
        {
            if (_abyssalThermalGridCellCount <= 0)
                InitializeThermalGridMetadata();

            if (!_nativeMemory.AbyssalThermalGridNative.IsCreated || _nativeMemory.AbyssalThermalGridNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.AbyssalThermalGridNative);
                // COLD ALLOC: NativeArray<float>[_abyssalThermalGridCellCount] - abyssal thermal-grid front buffer for stable sampling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalThermalGridNative = new NativeArray<float>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.AbyssalThermalGridNative, NativeMemoryOwner, nameof(_nativeMemory.AbyssalThermalGridNative), NativeMemoryLifetime);
                _abyssalThermalGridInitialized = false;
                _abyssalThermalGridRingOffsetX = 0;
                _abyssalThermalGridRingOffsetY = 0;
                _abyssalThermalGridRingOffsetZ = 0;
            }

            if (!_nativeMemory.AbyssalThermalGridNextNative.IsCreated || _nativeMemory.AbyssalThermalGridNextNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.AbyssalThermalGridNextNative);
                // COLD ALLOC: NativeArray<float>[_abyssalThermalGridCellCount] - abyssal thermal-grid back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalThermalGridNextNative = new NativeArray<float>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.AbyssalThermalGridNextNative, NativeMemoryOwner, nameof(_nativeMemory.AbyssalThermalGridNextNative), NativeMemoryLifetime);
            }

            if (!_nativeMemory.AbyssalFlowVolumeCurrentNative.IsCreated || _nativeMemory.AbyssalFlowVolumeCurrentNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.AbyssalFlowVolumeCurrentNative);
                // COLD ALLOC: NativeArray<float3>[_abyssalThermalGridCellCount] - abyssal 3D flow-volume front buffer for deep-current sampling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalFlowVolumeCurrentNative = new NativeArray<float3>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.AbyssalFlowVolumeCurrentNative, NativeMemoryOwner, nameof(_nativeMemory.AbyssalFlowVolumeCurrentNative), NativeMemoryLifetime);
                _abyssalFlowVolumeInitialized = false;
            }

            if (!_nativeMemory.AbyssalFlowVolumeNextNative.IsCreated || _nativeMemory.AbyssalFlowVolumeNextNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.AbyssalFlowVolumeNextNative);
                // COLD ALLOC: NativeArray<float3>[_abyssalThermalGridCellCount] - abyssal 3D flow-volume back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalFlowVolumeNextNative = new NativeArray<float3>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_nativeMemory.AbyssalFlowVolumeNextNative, NativeMemoryOwner, nameof(_nativeMemory.AbyssalFlowVolumeNextNative), NativeMemoryLifetime);
            }
        }

        private void CompleteFlowFieldJob(bool forceComplete)
        {
            if (!_flowFieldScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _flowFieldHandle, forceComplete))
                return;

            NativeArray<float2> flowSwap = _nativeMemory.EcosystemFlowFieldCurrentNative;
            _nativeMemory.EcosystemFlowFieldCurrentNative = _nativeMemory.EcosystemFlowFieldNextNative;
            _nativeMemory.EcosystemFlowFieldNextNative = flowSwap;
            _ecosystemFlowFieldCenter = _scheduledFlowFieldCenter;
            _flowFieldInitialized = true;
            _flowFieldScheduled = false;
        }

        private void CompleteThermalGridJob(bool forceComplete)
        {
            if (!_abyssalThermalGridScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _abyssalThermalGridHandle, forceComplete))
                return;

            bool canComparePreviousFlowVolume =
                _abyssalFlowVolumeInitialized &&
                _nativeMemory.AbyssalFlowVolumeCurrentNative.IsCreated &&
                _nativeMemory.AbyssalFlowVolumeNextNative.IsCreated &&
                _abyssalThermalGridResolutionXZ > 2 &&
                _abyssalThermalGridResolutionY > 2 &&
                (_scheduledAbyssalThermalGridCenter - _abyssalThermalGridCenter).sqrMagnitude <=
                (thermalGridHorizontalCellSize * thermalGridHorizontalCellSize);
            bool shouldTriggerBiolumeSurge = canComparePreviousFlowVolume &&
                                             DetectBiolumeSurgeCluster3D(
                                                 _nativeMemory.AbyssalFlowVolumeCurrentNative,
                                                 _nativeMemory.AbyssalFlowVolumeNextNative,
                                                 _abyssalThermalGridResolutionXZ,
                                                 _abyssalThermalGridResolutionY,
                                                 BiolumeSurgeVelocityDeltaThreshold);
            NativeArray<float> thermalSwap = _nativeMemory.AbyssalThermalGridNative;
            _nativeMemory.AbyssalThermalGridNative = _nativeMemory.AbyssalThermalGridNextNative;
            _nativeMemory.AbyssalThermalGridNextNative = thermalSwap;
            NativeArray<float3> flowVolumeSwap = _nativeMemory.AbyssalFlowVolumeCurrentNative;
            _nativeMemory.AbyssalFlowVolumeCurrentNative = _nativeMemory.AbyssalFlowVolumeNextNative;
            _nativeMemory.AbyssalFlowVolumeNextNative = flowVolumeSwap;
            _abyssalThermalGridCenter = _scheduledAbyssalThermalGridCenter;
            _abyssalThermalGridInitialized = true;
            _abyssalFlowVolumeInitialized = true;
            _abyssalThermalGridScheduled = false;

            if (shouldTriggerBiolumeSurge)
                TryRegisterBiolumeSurge(BiolumeSurgeDurationSeconds);
        }

        private void ScheduleFlowFieldJob()
        {
            if (_flowFieldScheduled)
                return;

            EnsureFlowFieldBuffers();
            if (_swarmWakeImpulseCount > 0 &&
                (!float.IsFinite(_swarmWakeImpulseExpireTime) || Time.unscaledTime > _swarmWakeImpulseExpireTime))
            {
                _swarmWakeImpulseCount = 0;
                if (_nativeMemory.SwarmWakeImpulseNative.IsCreated)
                    _nativeMemory.SwarmWakeImpulseNative[0] = default;
            }

            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            Vector3 flowCenter = _threatGridInitialized
                ? _ecosystemThreatGridCenter
                : (hasPlayerRuntimePosition ? playerRuntimePosition : Vector3.zero);
            PrepareFlowFieldSamplingSnapshot(flowCenter);

            Vector3 playerPosition = hasPlayerRuntimePosition ? playerRuntimePosition : flowCenter;
            Vector3 hotspotPosition = _currentThreatHotspotLevel >= flowFieldHotspotMinimumThreat
                ? _currentThreatHotspotPosition
                : playerPosition;
            float hotspotThreatLevel = _currentThreatHotspotLevel >= flowFieldHotspotMinimumThreat
                ? _currentThreatHotspotLevel
                : 0f;
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float2 weatherDirectionXZ = DominantAxisOrDefault(weatherSnapshot.CurrentMeta.GlobalBaseVector.xz, new float2(0f, 1f));

            var job = new BuildAbyssalFlowFieldJob
            {
                ThreatGrid = _nativeMemory.EcosystemThreatGridCurrentNative,
                FlowChunks = _nativeMemory.ThreatSamplingChunksNative,
                FlowDensityGrid = _nativeMemory.FlowSamplingDensityGridNative,
                ThreatAttractorGrid = _nativeMemory.ThreatSamplingAttractorGridNative,
                ChunkHash = _nativeMemory.ThreatSamplingChunkHashFrontNative,
                NavSupportGrid = _nativeMemory.FlowNavSupportGridNative,
                ExternalWakeImpulses = _nativeMemory.SwarmWakeImpulseNative,
                Output = _nativeMemory.EcosystemFlowFieldNextNative,
                GridResolution = _ecosystemThreatGridResolution,
                ChunkCount = _threatSamplingChunkCount,
                ExternalWakeImpulseCount = _swarmWakeImpulseCount,
                CellSize = threatGridCellSize,
                GridCenter = new float3(flowCenter.x, flowCenter.y, flowCenter.z),
                PlayerPosition = new float3(playerPosition.x, playerPosition.y, playerPosition.z),
                HotspotPosition = new float3(hotspotPosition.x, hotspotPosition.y, hotspotPosition.z),
                HotspotThreatLevel = hotspotThreatLevel,
                WeatherStateMask = (uint)weatherSnapshot.StateMask,
                WeatherDirectionXZ = weatherDirectionXZ,
                WeatherCurrentSpeed = math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale),
                WeatherIntensity = math.max(0f, weatherSnapshot.WeatherIntensity),
                ThreatBias = flowFieldThreatBias,
                PlayerBias = flowFieldPlayerBias,
                HotspotBias = flowFieldHotspotBias,
                ObstacleAvoidBias = flowFieldObstacleAvoidBias,
                NavSupportBias = flowFieldNavSupportBias,
                KelpObstacleWeight = flowFieldKelpObstacleWeight,
                SargassumObstacleWeight = flowFieldSargassumObstacleWeight,
                TechnoObstacleWeight = flowFieldTechnoObstacleWeight,
                ObstacleSoftThreshold = flowFieldObstacleSoftThreshold,
                ObstacleHardThreshold = flowFieldObstacleHardThreshold
            };

            _scheduledFlowFieldCenter = flowCenter;
            _flowFieldHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);
            _flowFieldScheduled = true;
        }

        private void ScheduleThermalGridJob()
        {
            if (_abyssalThermalGridScheduled)
                return;

            EnsureThermalGridBuffers();
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float2 weatherDirectionXZ = DominantAxisOrDefault(weatherSnapshot.CurrentMeta.GlobalBaseVector.xz, new float2(0f, 1f));

            Vector3 thermalCenter = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition)
                ? new Vector3(playerRuntimePosition.x, waterLevel - (thermalGridDepthMeters * 0.5f), playerRuntimePosition.z)
                : (_abyssalThermalGridInitialized
                    ? _abyssalThermalGridCenter
                    : new Vector3(0f, waterLevel - (thermalGridDepthMeters * 0.5f), 0f));

            var job = new BuildAbyssalThermalGridJob
            {
                Output = _nativeMemory.AbyssalThermalGridNextNative,
                ThreatChunks = _nativeMemory.ThreatSamplingChunksNative,
                ThreatAttractorGrid = _nativeMemory.ThreatSamplingAttractorGridNative,
                ChunkCount = _threatSamplingChunkCount,
                HorizontalResolution = _abyssalThermalGridResolutionXZ,
                VerticalResolution = _abyssalThermalGridResolutionY,
                HorizontalCellSize = thermalGridHorizontalCellSize,
                VerticalCellSize = thermalGridVerticalCellSize,
                WaterLevel = waterLevel,
                GridDepthMeters = thermalGridDepthMeters,
                GridCenter = new float3(thermalCenter.x, thermalCenter.y, thermalCenter.z),
                SurfaceTemperatureCelsius = thermalSurfaceTemperatureCelsius,
                AbyssTemperatureCelsius = thermalAbyssTemperatureCelsius,
                ThermoclineDepth = thermalThermoclineDepth,
                DepthFalloffExponent = thermalDepthFalloffExponent,
                ColonyBiomeStartDepth = colonyBiomeStartDepth,
                DeadZoneStartDepth = deadZoneStartDepth,
                HotPocketBoostCelsius = thermalHotPocketBoostCelsius,
                HotPocketNoiseScale = thermalHotPocketNoiseScale,
                HotPocketThreshold = thermalHotPocketThreshold,
                ColonyPocketStrength = thermalColonyPocketStrength,
                DeadZonePocketStrength = thermalDeadZonePocketStrength,
                RingOffsetX = _abyssalThermalGridRingOffsetX,
                RingOffsetY = _abyssalThermalGridRingOffsetY,
                RingOffsetZ = _abyssalThermalGridRingOffsetZ
            };

            var flowVolumeJob = new BuildAbyssalFlowVolumeJob
            {
                ThermalGrid = _nativeMemory.AbyssalThermalGridNextNative,
                ExternalWakeImpulses = _nativeMemory.SwarmWakeImpulseNative,
                Output = _nativeMemory.AbyssalFlowVolumeNextNative,
                HorizontalResolution = _abyssalThermalGridResolutionXZ,
                VerticalResolution = _abyssalThermalGridResolutionY,
                RingOffsetX = _abyssalThermalGridRingOffsetX,
                RingOffsetY = _abyssalThermalGridRingOffsetY,
                RingOffsetZ = _abyssalThermalGridRingOffsetZ,
                ExternalWakeImpulseCount = _swarmWakeImpulseCount,
                HorizontalCellSize = thermalGridHorizontalCellSize,
                VerticalCellSize = thermalGridVerticalCellSize,
                WaterLevel = waterLevel,
                GridDepthMeters = thermalGridDepthMeters,
                ThermoclineDepthMeters = 120f,
                WeatherStateMask = (uint)weatherSnapshot.StateMask,
                WeatherDirectionXZ = weatherDirectionXZ,
                WeatherCurrentSpeed = math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale),
                WeatherIntensity = math.max(0f, weatherSnapshot.WeatherIntensity),
                ThermalIntensity = math.max(0f, weatherSnapshot.CurrentMeta.ThermalIntensity),
                GridCenter = new float3(thermalCenter.x, thermalCenter.y, thermalCenter.z)
            };

            _scheduledAbyssalThermalGridCenter = thermalCenter;
            JobHandle thermalHandle = job.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize);
            _abyssalThermalGridHandle = flowVolumeJob.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize, thermalHandle);
            _abyssalThermalGridScheduled = true;
        }

        private static WeatherRuntimeSnapshot ResolveWeatherSnapshot()
        {
            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService == null || !weatherService.IsInitialized)
                return default;

            return weatherService.GetRuntimeSnapshot();
        }

        private static bool DetectBiolumeSurgeCluster3D(
            NativeArray<float3> previousField,
            NativeArray<float3> currentField,
            int horizontalResolution,
            int verticalResolution,
            float velocityDeltaThreshold)
        {
            if (!previousField.IsCreated ||
                !currentField.IsCreated ||
                horizontalResolution <= 2 ||
                verticalResolution <= 2)
            {
                return false;
            }

            int cellsPerLayer = horizontalResolution * horizontalResolution;
            int requiredLength = cellsPerLayer * verticalResolution;
            if (previousField.Length < requiredLength || currentField.Length < requiredLength)
                return false;

            for (int cellY = 1; cellY < verticalResolution - 1; cellY++)
            {
                int layerOffset = cellY * cellsPerLayer;
                for (int cellZ = 1; cellZ < horizontalResolution - 1; cellZ++)
                {
                    int rowOffset = layerOffset + (cellZ * horizontalResolution);
                    for (int cellX = 1; cellX < horizontalResolution - 1; cellX++)
                    {
                        float previousMaxSpeedSq = 0f;
                        float currentMaxSpeedSq = 0f;
                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            int sampleLayerOffset = (cellY + offsetY) * cellsPerLayer;
                            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                            {
                                int sampleRowOffset = sampleLayerOffset + ((cellZ + offsetZ) * horizontalResolution);
                                for (int offsetX = -1; offsetX <= 1; offsetX++)
                                {
                                    int sampleIndex = sampleRowOffset + cellX + offsetX;
                                    previousMaxSpeedSq = math.max(previousMaxSpeedSq, math.lengthsq(previousField[sampleIndex]));
                                    currentMaxSpeedSq = math.max(currentMaxSpeedSq, math.lengthsq(currentField[sampleIndex]));
                                }
                            }
                        }

                        float velocityDeltaThresholdSq = velocityDeltaThreshold * velocityDeltaThreshold;
                        if (math.abs(currentMaxSpeedSq - previousMaxSpeedSq) > velocityDeltaThresholdSq)
                            return true;
                    }
                }
            }

            return false;
        }

        private static void TryRegisterBiolumeSurge(float durationSeconds)
        {
            if (GlobalRegistry.Weather is GlobalWeatherDirector weatherDirector && weatherDirector.IsInitialized)
                weatherDirector.RegisterBiolumeSurge(durationSeconds);
        }

        private void ResolveThreatSignalSnapshot(out Vector3 emissionPosition, out float emissionRadius, out float emissionStrength)
        {
            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            emissionPosition = hasPlayerRuntimePosition ? playerRuntimePosition : Vector3.zero;
            emissionRadius = 0f;
            emissionStrength = 0f;

            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal signal))
            {
                emissionPosition = signal.Position;
                float movement01 = InverseLerpSpeedSq(0.5f, 8.5f, signal.MovementSpeedSqr);
                float tool01 = math.saturate(signal.ToolUseNoise01);
                float transport01 = math.saturate(signal.TransportBoost01 * math.max(1f, signal.TransportSignature));
                float flashlight01 = signal.FlashlightOn ? 1f : 0f;
                float radius01 = math.saturate(math.max(math.max(movement01, tool01), math.max(signal.TransportBoost01, flashlight01 * 0.7f)));
                emissionRadius = LerpClamped(threatEmissionRadiusMin, threatEmissionRadiusMax, radius01);
                emissionStrength =
                    (movement01 * threatNoiseDepositPerSecond) +
                    ((tool01 + transport01) * threatPulseDepositPerSecond) +
                    (flashlight01 * threatFlashlightDepositPerSecond);
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            if (!hasPlayerRuntimePosition)
            {
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            float fallbackMovement01 = InverseLerpSpeedSq(0.5f, 8.5f, _playerVelocity.sqrMagnitude);
            if (fallbackMovement01 <= 0f)
            {
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            emissionRadius = LerpClamped(threatEmissionRadiusMin, threatEmissionRadiusMax, fallbackMovement01);
            emissionStrength = fallbackMovement01 * threatNoiseDepositPerSecond;
            ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
        }

        private void ApplyExternalThreatPulseToSnapshot(ref Vector3 emissionPosition, ref float emissionRadius, ref float emissionStrength)
        {
            if (_externalThreatPulseHoldTimer <= 0f || _externalThreatPulseStrength <= 0f || _externalThreatPulseRadius <= 0f)
                return;

            emissionPosition = _externalThreatPulsePosition;
            emissionRadius = math.max(emissionRadius, _externalThreatPulseRadius);
            emissionStrength = math.max(emissionStrength, _externalThreatPulseStrength);
        }

        private void UpdateThreatHotspot()
        {
            _currentThreatHotspotLevel = 0f;
            _currentThreatHotspotPosition = _ecosystemThreatGridCenter;
            if (!_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
                return;

            int bestIndex = -1;
            float bestThreat = 0f;
            for (int i = 0; i < _ecosystemThreatGridCellCount; i++)
            {
                float threat = _nativeMemory.EcosystemThreatGridCurrentNative[i];
                if (threat <= bestThreat)
                    continue;

                bestThreat = threat;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int bestX = bestIndex % _ecosystemThreatGridResolution;
            int bestZ = bestIndex / _ecosystemThreatGridResolution;
            _currentThreatHotspotLevel = bestThreat;
            _currentThreatHotspotPosition = new Vector3(
                _ecosystemThreatGridCenter.x + ((bestX - halfExtent) * threatGridCellSize),
                TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) ? playerRuntimePosition.y : _ecosystemThreatGridCenter.y,
                _ecosystemThreatGridCenter.z + ((bestZ - halfExtent) * threatGridCellSize));
        }

        private NativeArray<float> GetThreatGridFloatView()
        {
            if (!_threatGridInitialized || !_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridCellCount <= 0)
                return default;

            return _nativeMemory.EcosystemThreatGridCurrentNative;
        }

        private static float2 SampleFlowFieldAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<float2> flowField)
        {
            if (!flowField.IsCreated ||
                resolution <= 0 ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                !IsFinite(position) ||
                !IsFinite(gridCenter))
            {
                return float2.zero;
            }

            long expectedLength = (long)resolution * resolution;
            if (expectedLength <= 0L || expectedLength > int.MaxValue || flowField.Length < expectedLength)
            {
                return float2.zero;
            }

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            if (!math.isfinite(halfExtent))
            {
                return float2.zero;
            }

            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return float2.zero;

            float inverseCellSize = math.rcp(cellSize);
            float normalizedX = math.clamp(localX * inverseCellSize, 0f, resolution - 1);
            float normalizedZ = math.clamp(localZ * inverseCellSize, 0f, resolution - 1);
            int cellX = math.clamp((int)math.floor(normalizedX), 0, resolution - 1);
            int cellZ = math.clamp((int)math.floor(normalizedZ), 0, resolution - 1);
            int nextCellX = math.min(cellX + 1, resolution - 1);
            int nextCellZ = math.min(cellZ + 1, resolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float2 sample00 = flowField[(cellZ * resolution) + cellX];
            float2 sample10 = flowField[(cellZ * resolution) + nextCellX];
            float2 sample01 = flowField[(nextCellZ * resolution) + cellX];
            float2 sample11 = flowField[(nextCellZ * resolution) + nextCellX];
            float2 sampleX0 = math.lerp(sample00, sample10, fracX);
            float2 sampleX1 = math.lerp(sample01, sample11, fracX);
            return DominantAxisOrDefault(math.lerp(sampleX0, sampleX1, fracZ), float2.zero);
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateAnchoredVegetationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> SandMask;
            [ReadOnly] public NativeArray<byte> RockMask;
            [ReadOnly] public NativeArray<ushort> HeightSamples;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly] public NativeArray<byte> ThreatEchoFlags;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            public NativeArray<JobInstanceRecord> Output;
            public int TerrainHoleCount;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int AlphamapResolution;
            public int HeightResolution;
            public float MinX;
            public float MinZ;
            public float MaxX;
            public float MaxZ;
            public float StepX;
            public float StepZ;
            public int SampleCountX;
            public int TileX;
            public int TileZ;
            public int ChunkX;
            public int ChunkZ;
            public int SampleSeedOffset;
            public float JitterFraction;
            public byte SandMaskThreshold;
            public byte RockMaskThreshold;
            public float MinimumNormalY;
            public float NormalOffset;
            public float MinWorldYExclusive;
            public float MaxWorldYExclusive;
            public float EdgeDitherDistance;
            public float ScaleMin;
            public float ScaleMax;
            public float HeightScaleMin;
            public float HeightScaleMax;
            public float WidthScaleMin;
            public float WidthScaleMax;
            public int TypeId;
            public int OrganicSemanticType;
            public int ColonyCableSemanticType;
            public int ColonyHullSemanticType;
            public int ColonyBeamSemanticType;
            public int DeadZoneSemanticType;
            public float WaterLevel;
            public float ColonyBiomeStartDepth;
            public float DeadZoneStartDepth;
            public float VerticalBiomeBlendBand;
            public float TechnoJungleThreshold;
            public float TechnoJungleCellSize;
            public float TechnoJungleSecondaryCellSize;
            public float TechnoJungleWallWidth;
            public float TechnoJungleWarpMeters;
            public float TechnoJungleFlowAnisotropy;
            public float DeadZoneStructureChance;
            public float DeadZoneDensityScale;
            public float AbyssalFlowNoiseScale;
            public float AbyssalFlowNoiseStrength;
            public float AbyssalFlowVerticalStrength;
            public int ApplyOrganicKelpPlacementRules;
            public float OrganicKelpMaxDepthBelowSurface;
            public float OrganicKelpMinimumNormalY;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public float EchoTechnoJungleThresholdBias;
            public float EchoDeadZoneKeepBoost;
            public int IgnorePlacementMasks;
            public int CorruptionMode;
            public int EnableVerticalBiomeRewrite;
            public uint ScaleSalt;
            public uint WidthSalt;
            public float ScaleJitter;
            public uint RotationSalt;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                int x = index % SampleCountX;
                int z = index / SampleCountX;
                uint seed = BuildSampleSeed(TileX, TileZ, (ChunkX << 16) + x + SampleSeedOffset, (ChunkZ << 16) + z + SampleSeedOffset);
                float sampleX = BuildJitteredCoordinate(MinX, StepX, x, JitterFraction, seed);
                float sampleZ = BuildJitteredCoordinate(MinZ, StepZ, z, JitterFraction, seed ^ 0x9E3779B9u);
                if (IsInsideTerrainHoleStatic(sampleX, sampleZ, TerrainHoles, TerrainHoleCount))
                    return;

                if (!TrySampleTerrainPlacement(
                        sampleX,
                        sampleZ,
                        seed,
                        TerrainPosition,
                        TerrainSize,
                        AlphamapResolution,
                        HeightResolution,
                        SandMaskThreshold,
                        RockMaskThreshold,
                        MinimumNormalY,
                        IgnorePlacementMasks,
                        SandMask,
                        RockMask,
                        HeightSamples,
                        out float worldY,
                        out float3 normal,
                        out float variation))
                {
                    return;
                }

                if (worldY <= MinWorldYExclusive || worldY >= MaxWorldYExclusive)
                    return;

                if (!TryPassChunkEdgeDither(sampleX, sampleZ, MinX, MaxX, MinZ, MaxZ, EdgeDitherDistance, seed, out float edgeDistance))
                    return;

                bool isMetalSurface = TryResolveMetalAttachmentSurface(new float3(sampleX, worldY, sampleZ), out float metalSurfaceY);
                if (isMetalSurface)
                {
                    worldY = math.max(worldY, metalSurfaceY);
                    normal = new float3(0f, 1f, 0f);
                }

                float scaleLerp = Hash01(seed ^ ScaleSalt);
                float scale = math.lerp(ScaleMin, ScaleMax, scaleLerp);
                float scaleJitter = math.lerp(1f - ScaleJitter, 1f + ScaleJitter, Hash01(seed ^ (ScaleSalt ^ 0x27D4EB2Fu)));
                scale *= scaleJitter;
                float heightScale = math.lerp(HeightScaleMin, HeightScaleMax, scaleLerp);
                float widthScale = math.lerp(WidthScaleMin, WidthScaleMax, Hash01(seed ^ WidthSalt));
                float3 position = new float3(sampleX, worldY, sampleZ) + (normal * NormalOffset);
                float2 flowDirection = ResolveSlopeFlowDirection(normal, seed);
                float3 flowVector = new float3(flowDirection.x, 0f, flowDirection.y);
                bool hasPermanentEcho = SampleThreatEchoAtWorldPosition(sampleX, sampleZ, ThreatGridCenter, ThreatGridCellSize, ThreatGridResolution, ThreatEchoFlags) != 0;
                byte biomeLayer;
                int semanticType;

                if (CorruptionMode != 0)
                {
                    biomeLayer = (byte)VegetationBiomeLayer.DeadZone;
                    semanticType = DeadZoneSemanticType;
                    float corruptionScale = math.lerp(7.5f, 14.5f, Hash01(seed ^ 0x94D049BBu));
                    scale *= corruptionScale;
                    heightScale *= corruptionScale;
                    widthScale *= math.lerp(2.8f, 5f, Hash01(seed ^ 0xC13FA9A9u));
                }
                else
                {
                    biomeLayer = (byte)VegetationBiomeLayer.OrganicShelf;
                    semanticType = OrganicSemanticType;

                    if (EnableVerticalBiomeRewrite != 0)
                    {
                        if (isMetalSurface)
                        {
                            biomeLayer = (byte)VegetationBiomeLayer.ColonyGraveyard;
                            semanticType = ResolveColonySemanticTypeStatic(
                                seed,
                                ColonyCableSemanticType,
                                ColonyHullSemanticType,
                                ColonyBeamSemanticType);
                            heightScale *= math.lerp(0.95f, 1.2f, Hash01(seed ^ 0x4F6CDD1Du));
                            widthScale *= math.lerp(1.05f, 1.28f, Hash01(seed ^ 0x61C88647u));
                        }
                        else
                        {
                            biomeLayer = ResolveBiomeLayerStatic(
                                WaterLevel,
                                worldY,
                                ColonyBiomeStartDepth,
                                DeadZoneStartDepth,
                                VerticalBiomeBlendBand,
                                seed);

                            if (biomeLayer == (byte)VegetationBiomeLayer.ColonyGraveyard)
                            {
                                float technoThreshold = math.max(0f, TechnoJungleThreshold - (hasPermanentEcho ? EchoTechnoJungleThresholdBias : 0f));
                                if (!TryEvaluateTechnoJungle(
                                        sampleX,
                                        sampleZ,
                                        seed,
                                        flowDirection,
                                        technoThreshold,
                                        TechnoJungleCellSize,
                                        TechnoJungleSecondaryCellSize,
                                        TechnoJungleWallWidth,
                                        TechnoJungleWarpMeters,
                                        TechnoJungleFlowAnisotropy,
                                        out float technoOccupancy))
                                {
                                    return;
                                }

                                heightScale *= math.lerp(0.9f, 1.2f, technoOccupancy);
                                widthScale *= math.lerp(0.95f, 1.12f, technoOccupancy);
                            }
                            else if (biomeLayer == (byte)VegetationBiomeLayer.DeadZone)
                            {
                                float deadZoneDepth = math.max(0f, WaterLevel - worldY);
                                float deadZoneDepthT = math.saturate((deadZoneDepth - DeadZoneStartDepth) * 0.0005f);
                                float deadZoneThreshold = math.max(0f, TechnoJungleThreshold - (hasPermanentEcho ? EchoTechnoJungleThresholdBias * 0.75f : 0f));
                                if (!TryEvaluateTechnoJungle(
                                        sampleX,
                                        sampleZ,
                                        seed ^ 0x51ED270Bu,
                                        flowDirection,
                                        deadZoneThreshold,
                                        TechnoJungleCellSize * 1.6f,
                                        TechnoJungleSecondaryCellSize * 1.35f,
                                        TechnoJungleWallWidth * 1.4f,
                                        TechnoJungleWarpMeters * 0.8f,
                                        math.max(0.2f, TechnoJungleFlowAnisotropy * 0.7f),
                                        out float deadZoneOccupancy))
                                {
                                    return;
                                }

                                float keepChance = math.saturate(
                                    math.lerp(DeadZoneDensityScale, DeadZoneDensityScale * 0.18f, deadZoneDepthT) *
                                    math.max(DeadZoneStructureChance, deadZoneOccupancy * math.lerp(1f, 0.45f, deadZoneDepthT)));
                                if (hasPermanentEcho)
                                    keepChance = math.saturate(keepChance + EchoDeadZoneKeepBoost);
                                if (Hash01(seed ^ 0xC13FA9A9u) > keepChance)
                                    return;

                                float deadZoneScale = math.lerp(4.5f, 12f, math.max(deadZoneDepthT, Hash01(seed ^ 0x94D049BBu)));
                                scale *= deadZoneScale;
                                heightScale *= deadZoneScale;
                                widthScale *= math.lerp(2.1f, 4.4f, math.max(deadZoneOccupancy, deadZoneDepthT));
                            }
                        }
                    }
                }

                float depthBelowSurface = math.max(0f, WaterLevel - position.y);
                if (ApplyOrganicKelpPlacementRules != 0 && semanticType == OrganicSemanticType)
                {
                    if (depthBelowSurface > OrganicKelpMaxDepthBelowSurface ||
                        normal.y < OrganicKelpMinimumNormalY)
                    {
                        return;
                    }
                }

                if (IntersectsBaseModuleAabb(position, scale, heightScale, widthScale))
                    return;

                flowVector = ApplyAbyssalFlowNoiseStatic(
                    flowVector,
                    position,
                    depthBelowSurface,
                    ColonyBiomeStartDepth,
                    AbyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength,
                    seed);
                flowDirection = DominantAxisOrDefault(new float2(flowVector.x, flowVector.z), flowDirection);
                int rotationSector = ResolveOctantSector(variation, seed, RotationSalt);
                quaternion rotation = BuildAlignedRotation(normal, rotationSector);
                Output[index] = new JobInstanceRecord
                {
                    Matrix = float4x4.TRS(position, rotation, new float3(scale, scale, scale)),
                    HeightScale = heightScale,
                    WidthScale = widthScale,
                    Variation = variation,
                    EdgeDistance = edgeDistance,
                    FlowDirection = flowDirection,
                    FlowVector = flowVector,
                    Type = TypeId,
                    SemanticType = semanticType,
                    BiomeLayer = biomeLayer,
                    IsValid = 1
                };
            }

            private bool IntersectsBaseModuleAabb(float3 position, float scale, float heightScale, float widthScale)
            {
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                float uniformScale = math.max(0.001f, math.abs(scale));
                float radius = math.max(0.25f, uniformScale * math.max(0.25f, math.abs(widthScale)));
                float height = math.max(0.25f, uniformScale * math.max(0.25f, math.abs(heightScale)));
                float3 aabbMin = new float3(position.x - radius, position.y - 0.05f, position.z - radius);
                float3 aabbMax = new float3(position.x + radius, position.y + height, position.z + radius);

                if (!TryComputeStructureCellRange(aabbMin, aabbMax, out int minCellX, out int maxCellX, out int minCellZ, out int maxCellZ))
                    return false;

                for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                {
                    int rowOffset = cellZ * ThreatGridResolution;
                    for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    {
                        if (IntersectsBaseModuleAabbInCell(rowOffset + cellX, aabbMin, aabbMax))
                            return true;
                    }
                }

                return false;
            }

            private bool IntersectsBaseModuleAabbInCell(int cellIndex, float3 aabbMin, float3 aabbMax)
            {
                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex < 0 || structureIndex >= ArtificialStructures.Length)
                        continue;

                    ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                    if ((StructureType)structure.Type != StructureType.BaseModule)
                        continue;

                    if (aabbMax.x >= structure.MinX &&
                        aabbMin.x <= structure.MaxX &&
                        aabbMax.y >= structure.MinY &&
                        aabbMin.y <= structure.MaxY &&
                        aabbMax.z >= structure.MinZ &&
                        aabbMin.z <= structure.MaxZ)
                    {
                        return true;
                    }
                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return false;
            }

            private bool TryComputeStructureCellRange(
                float3 aabbMin,
                float3 aabbMax,
                out int minCellX,
                out int maxCellX,
                out int minCellZ,
                out int maxCellZ)
            {
                if (ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f ||
                    !math.isfinite(ThreatGridCellSize) ||
                    !math.all(math.isfinite(ThreatGridCenter)) ||
                    !math.all(math.isfinite(aabbMin)) ||
                    !math.all(math.isfinite(aabbMax)))
                {
                    minCellX = 0;
                    maxCellX = 0;
                    minCellZ = 0;
                    maxCellZ = 0;
                    return false;
                }

                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float minGridX = ThreatGridCenter.x - halfExtent;
                float maxGridX = ThreatGridCenter.x + halfExtent;
                float minGridZ = ThreatGridCenter.z - halfExtent;
                float maxGridZ = ThreatGridCenter.z + halfExtent;
                if (aabbMax.x < minGridX || aabbMin.x > maxGridX || aabbMax.z < minGridZ || aabbMin.z > maxGridZ)
                {
                    minCellX = 0;
                    maxCellX = 0;
                    minCellZ = 0;
                    maxCellZ = 0;
                    return false;
                }

                float inverseThreatGridCellSize = math.rcp(ThreatGridCellSize);
                minCellX = math.clamp((int)math.floor((aabbMin.x - minGridX) * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                maxCellX = math.clamp((int)math.floor((aabbMax.x - minGridX) * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                minCellZ = math.clamp((int)math.floor((aabbMin.z - minGridZ) * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                maxCellZ = math.clamp((int)math.floor((aabbMax.z - minGridZ) * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                return minCellX <= maxCellX && minCellZ <= maxCellZ;
            }

            private bool TryResolveMetalAttachmentSurface(float3 samplePosition, out float surfaceY)
            {
                surfaceY = samplePosition.y;
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                int cellIndex = ComputeStructureGridCellIndex(samplePosition);
                if (cellIndex < 0)
                    return false;

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return false;

                bool found = false;
                do
                {
                    if (structureIndex < 0 || structureIndex >= ArtificialStructures.Length)
                        continue;

                    ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                    StructureType structureType = (StructureType)structure.Type;
                    if (structureType != StructureType.BaseModule && structureType != StructureType.MegaWreck)
                        continue;

                    if (samplePosition.x < structure.MinX ||
                        samplePosition.x > structure.MaxX ||
                        samplePosition.z < structure.MinZ ||
                        samplePosition.z > structure.MaxZ)
                    {
                        continue;
                    }

                    surfaceY = math.max(surfaceY, structure.MaxY);
                    found = true;
                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return found;
            }

            private int ComputeStructureGridCellIndex(float3 position)
            {
                if (ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f ||
                    !math.isfinite(ThreatGridCellSize) ||
                    !math.all(math.isfinite(ThreatGridCenter)) ||
                    !math.all(math.isfinite(position)))
                {
                    return -1;
                }

                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float localX = position.x - (ThreatGridCenter.x - halfExtent);
                float localZ = position.z - (ThreatGridCenter.z - halfExtent);
                if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                    return -1;

                float inverseThreatGridCellSize = math.rcp(ThreatGridCellSize);
                int cellX = math.clamp((int)math.floor(localX * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                int cellZ = math.clamp((int)math.floor(localZ * inverseThreatGridCellSize), 0, ThreatGridResolution - 1);
                return (cellZ * ThreatGridResolution) + cellX;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateFloatingVegetationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> SandMask;
            [ReadOnly] public NativeArray<byte> RockMask;
            [ReadOnly] public NativeArray<ushort> HeightSamples;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            public NativeArray<JobInstanceRecord> Output;
            public int TerrainHoleCount;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int AlphamapResolution;
            public int HeightResolution;
            public float MinX;
            public float MinZ;
            public float MaxX;
            public float MaxZ;
            public float StepX;
            public float StepZ;
            public int SampleCountX;
            public int TileX;
            public int TileZ;
            public int ChunkX;
            public int ChunkZ;
            public int SampleSeedOffset;
            public float JitterFraction;
            public byte SandMaskThreshold;
            public byte RockMaskThreshold;
            public float MinimumNormalY;
            public float WaterLevel;
            public float FloatingSurfaceOffset;
            public float FloatingSurfaceBand;
            public float EdgeDitherDistance;
            public float ScaleMin;
            public float ScaleMax;
            public float FloatingPatchThreshold;
            public float FloatingPatchNoiseScale;
            public float FloatingCellSize;
            public float FloatingSecondaryCellSize;
            public float FloatingWallWidth;
            public float FloatingWarpMeters;
            public float2 FloatingFlowDirection;
            public float FloatingFlowAnisotropy;
            public float ScaleJitter;
            public uint RotationSalt;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                int x = index % SampleCountX;
                int z = index / SampleCountX;
                uint seed = BuildSampleSeed(TileX, TileZ, (ChunkX << 16) + x + SampleSeedOffset, (ChunkZ << 16) + z + SampleSeedOffset);
                float sampleX = BuildJitteredCoordinate(MinX, StepX, x, JitterFraction, seed);
                float sampleZ = BuildJitteredCoordinate(MinZ, StepZ, z, JitterFraction, seed ^ 0x94D049BBu);
                if (IsInsideTerrainHoleStatic(sampleX, sampleZ, TerrainHoles, TerrainHoleCount))
                    return;

                if (!TrySampleTerrainPlacement(
                        sampleX,
                        sampleZ,
                        seed,
                        TerrainPosition,
                        TerrainSize,
                        AlphamapResolution,
                        HeightResolution,
                        SandMaskThreshold,
                        RockMaskThreshold,
                        MinimumNormalY,
                        0,
                        SandMask,
                        RockMask,
                        HeightSamples,
                        out float worldY,
                        out _,
                        out float variation))
                {
                    return;
                }

                if (math.abs(worldY - WaterLevel) > FloatingSurfaceBand)
                    return;

                if (!TryPassChunkEdgeDither(sampleX, sampleZ, MinX, MaxX, MinZ, MaxZ, EdgeDitherDistance, seed, out float edgeDistance))
                    return;

                if (!TryEvaluateFloatingLabyrinth(
                        sampleX,
                        sampleZ,
                        seed,
                        FloatingPatchThreshold,
                        FloatingPatchNoiseScale,
                        FloatingCellSize,
                        FloatingSecondaryCellSize,
                        FloatingWallWidth,
                        FloatingWarpMeters,
                        FloatingFlowDirection,
                        FloatingFlowAnisotropy,
                        out float occupancy))
                {
                    return;
                }

                float scaleLerp = Hash01(seed ^ 0xD1B54A35u);
                float scale = math.lerp(ScaleMin, ScaleMax, scaleLerp);
                float scaleJitter = math.lerp(1f - ScaleJitter, 1f + ScaleJitter, Hash01(seed ^ 0x27D4EB2Fu));
                scale *= scaleJitter;
                float heightScale = math.lerp(0.35f, 0.9f, occupancy) * math.lerp(0.85f, 1.05f, scaleLerp);
                float widthScale = math.lerp(0.8f, 1.25f, math.max(Hash01(seed ^ 0xA24BAEDCu), occupancy));
                float3 position = new float3(sampleX, WaterLevel + FloatingSurfaceOffset, sampleZ);
                float2 flowDirection = DominantAxisOrDefault(FloatingFlowDirection, new float2(1f, 0f));
                float3 flowVector = new float3(flowDirection.x, 0f, flowDirection.y);
                int rotationSector = ResolveOctantSector(variation, seed, RotationSalt);
                float2 yawDirection = ResolveOctantDirection(rotationSector);
                quaternion rotation = quaternion.LookRotationSafe(new float3(yawDirection.x, 0f, yawDirection.y), new float3(0f, 1f, 0f));
                Output[index] = new JobInstanceRecord
                {
                    Matrix = float4x4.TRS(position, rotation, new float3(scale, scale, scale)),
                    HeightScale = heightScale,
                    WidthScale = widthScale,
                    Variation = variation,
                    EdgeDistance = edgeDistance,
                    FlowDirection = flowDirection,
                    FlowVector = flowVector,
                    Type = (int)HectonVegetationInstanceType.Sargassum,
                    SemanticType = (int)VegetationSemanticType.FloatingSargassum,
                    BiomeLayer = (byte)VegetationBiomeLayer.OrganicShelf,
                    IsValid = 1
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SampleBiomassDensityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public int TypeMask;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                Output[index] = SampleDensityAtPosition(Positions[index], TypeMask, Chunks, DensityGrid, ChunkCount);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct VegetationDensityQueryJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> Positions;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public float GrassVisibilityWeight;
            public float KelpVisibilityWeight;
            public float SargassumVisibilityWeight;
            public float WaterLevel;
            public float FloatingSurfaceOffset;
            public float SargassumVisibilityBand;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                Vector3 position = Positions[index];
                float3 densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    Chunks,
                    DensityGrid,
                    ChunkCount);
                Output[index] = EvaluateVisibilityModifierStatic(
                    position.y,
                    densityChannels,
                    GrassVisibilityWeight,
                    KelpVisibilityWeight,
                    SargassumVisibilityWeight,
                    WaterLevel,
                    FloatingSurfaceOffset,
                    SargassumVisibilityBand);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThreatPropagationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> CurrentThreat;
            [ReadOnly] public NativeArray<byte> CurrentEchoFlags;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [WriteOnly] public NativeArray<float> NextThreat;
            [WriteOnly] public NativeArray<byte> NextThreatCompressed;
            [WriteOnly] public NativeArray<byte> NextEchoFlags;
            public int GridResolution;
            public int ThreatChunkCount;
            public int ShiftX;
            public int ShiftZ;
            public float CellSize;
            public float DeltaTime;
            public float Diffusion;
            public float DecayPerSecond;
            public float SargassumRetentionBoost;
            public float TechnoJungleRetentionBoost;
            public float SargassumAccumulationBoost;
            public float TechnoJungleAccumulationBoost;
            public float StructureThreatSuppression;
            public float StructureHazardAttraction;
            public float PermanentEchoFloor;
            public float PermanentEchoThreshold;
            public float3 GridCenter;
            public float3 EmissionPosition;
            public float EmissionRadius;
            public float EmissionStrength;

            public void Execute(int index)
            {
                if (!NextThreat.IsCreated || index < 0 || index >= NextThreat.Length || GridResolution <= 0)
                    return;

                int cellX = index % GridResolution;
                int cellZ = index / GridResolution;
                float previousThreat = SampleShiftedThreat(cellX, cellZ);
                float neighborAverage = SampleNeighborAverage(cellX, cellZ, previousThreat);
                float diffusionWeight = math.saturate(Diffusion * DeltaTime);
                float diffusedThreat = math.lerp(previousThreat, neighborAverage, diffusionWeight);

                int halfExtent = GridResolution >> 1;
                float worldX = GridCenter.x + ((cellX - halfExtent) * CellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * CellSize);
                float3 samplePosition = new float3(worldX, GridCenter.y, worldZ);
                byte hadPermanentEcho = SampleShiftedEcho(cellX, cellZ);
                float2 attractor = ThreatChunkCount > 0 && ThreatChunks.IsCreated && ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPosition(samplePosition, ThreatChunks, ThreatAttractorGrid, ThreatChunkCount)
                    : float2.zero;

                float retentionBoost = math.saturate((attractor.x * SargassumRetentionBoost) + (attractor.y * TechnoJungleRetentionBoost));
                float decayRate = math.max(0f, DecayPerSecond) * (1f - retentionBoost);
                float retention = ResolveCheapRetention(decayRate * math.max(0f, DeltaTime));
                float propagatedThreat = diffusedThreat * retention;

                float localDeposit = 0f;
                if (EmissionStrength > 0f && EmissionRadius > 0f)
                {
                    float2 delta = new float2(worldX - EmissionPosition.x, worldZ - EmissionPosition.z);
                    float emissionRadius = math.max(0.01f, EmissionRadius);
                    float distanceSq = math.lengthsq(delta);
                    float emissionRadiusSq = emissionRadius * emissionRadius;
                    if (distanceSq <= emissionRadiusSq)
                    {
                        float falloff = 1f - math.saturate(distanceSq * math.rcp(emissionRadiusSq));
                        float accumulationBoost = 1f + (attractor.x * SargassumAccumulationBoost) + (attractor.y * TechnoJungleAccumulationBoost);
                        localDeposit = EmissionStrength * DeltaTime * falloff * accumulationBoost;
                    }
                }

                float nextThreat = math.saturate(propagatedThreat + localDeposit);
                nextThreat = ApplyArtificialStructureInfluence(index, samplePosition, nextThreat);
                byte nextEcho = hadPermanentEcho;
                if (nextThreat >= PermanentEchoThreshold)
                    nextEcho = 1;

                if (nextEcho != 0)
                    nextThreat = math.max(nextThreat, PermanentEchoFloor);

                NextThreat[index] = nextThreat;
                if (NextThreatCompressed.IsCreated && index < NextThreatCompressed.Length)
                    NextThreatCompressed[index] = EncodeThreat(nextThreat);
                if (NextEchoFlags.IsCreated && index < NextEchoFlags.Length)
                    NextEchoFlags[index] = nextEcho;
            }

            private float SampleShiftedThreat(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!CurrentThreat.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0f;
                }

                return CurrentThreat[(previousZ * GridResolution) + previousX];
            }

            private byte SampleShiftedEcho(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!CurrentEchoFlags.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0;
                }

                return CurrentEchoFlags[(previousZ * GridResolution) + previousX];
            }

            private float SampleNeighborAverage(int x, int z, float centerThreat)
            {
                float weightedSum = centerThreat * 4f;
                float totalWeight = 4f;
                AccumulateThreatSample(x - 1, z, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x, z - 1, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x, z + 1, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x - 1, z - 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z - 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x - 1, z + 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z + 1, 1f, ref weightedSum, ref totalWeight);
                return weightedSum * math.rcp(math.max(1f, totalWeight));
            }

            private void AccumulateThreatSample(int x, int z, float weight, ref float weightedSum, ref float totalWeight)
            {
                if (x < 0 || z < 0 || x >= GridResolution || z >= GridResolution)
                    return;

                weightedSum += SampleShiftedThreat(x, z) * weight;
                totalWeight += weight;
            }

            private static byte EncodeThreat(float threat)
            {
                return (byte)math.clamp((int)math.round(math.saturate(threat) * 255f), 0, 255);
            }

            private float ApplyArtificialStructureInfluence(int cellIndex, float3 samplePosition, float threat)
            {
                if (!ArtificialStructureHash.IsCreated || !ArtificialStructures.IsCreated)
                    return threat;

                float suppression = 0f;
                float attraction = 0f;
                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return threat;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        if (samplePosition.x >= structure.MinX &&
                            samplePosition.x <= structure.MaxX &&
                            samplePosition.y >= structure.MinY &&
                            samplePosition.y <= structure.MaxY &&
                            samplePosition.z >= structure.MinZ &&
                            samplePosition.z <= structure.MaxZ)
                        {
                            switch ((StructureType)structure.Type)
                            {
                                case StructureType.BaseModule:
                                    suppression = math.max(suppression, StructureThreatSuppression);
                                    break;

                                case StructureType.HazardEmitter:
                                    attraction = math.max(attraction, StructureHazardAttraction);
                                    break;

                                case StructureType.MegaWreck:
                                    attraction = math.max(attraction, StructureHazardAttraction * 0.5f);
                                    break;

                                case StructureType.VoxelCave:
                                    suppression = math.max(suppression, StructureThreatSuppression * 0.35f);
                                    break;
                            }
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                float adjusted = threat * math.saturate(1f - suppression);
                if (attraction > 0f)
                    adjusted = math.saturate(adjusted + attraction);

                return adjusted;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThreatVoxelizationJob : IJobParallelFor
        {
            private const byte SolidThreat = 255;

            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ChunkHash;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [WriteOnly] public NativeArray<byte> Output;
            public int GridResolutionXZ;
            public int GridResolutionY;
            public float CellSizeXZ;
            public float CellSizeY;
            public float3 GridOrigin;
            public float3 GridCenter;
            public float KelpObstacleWeight;
            public float SargassumObstacleWeight;
            public float TechnoObstacleWeight;
            public float ObstacleHardThreshold;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    GridResolutionXZ <= 0 ||
                    GridResolutionY <= 0)
                {
                    return;
                }

                int cellsPerSlice = GridResolutionXZ * GridResolutionY;
                int cellZ = index / cellsPerSlice;
                int sliceIndex = index - (cellZ * cellsPerSlice);
                int cellY = sliceIndex / GridResolutionXZ;
                int cellX = sliceIndex - (cellY * GridResolutionXZ);
                float3 voxelCenterOffset = new float3(
                    (cellX + 0.5f) * CellSizeXZ,
                    (cellY + 0.5f) * CellSizeY,
                    (cellZ + 0.5f) * CellSizeXZ);
                float3 samplePosition = new float3(
                    GridOrigin.x + voxelCenterOffset.x,
                    GridOrigin.y + voxelCenterOffset.y,
                    GridOrigin.z + voxelCenterOffset.z);

                int columnIndex = (cellZ * GridResolutionXZ) + cellX;
                float threat = ThreatGrid.IsCreated && columnIndex >= 0 && columnIndex < ThreatGrid.Length
                    ? math.saturate(ThreatGrid[columnIndex])
                    : 0f;
                byte encodedThreat = EncodeOpenThreat(threat);
                float obstacle = SampleObstacle(samplePosition);
                bool isSolid = obstacle >= ObstacleHardThreshold || IsInsideBlockingStructure(columnIndex, samplePosition);
                Output[index] = isSolid ? SolidThreat : encodedThreat;
            }

            private float SampleObstacle(float3 position)
            {
                if (DensityChunks.IsCreated &&
                    DensityGrid.IsCreated &&
                    ChunkHash.IsCreated)
                {
                    float3 density = SampleDensityChannelsAtPositionHashed(
                        position,
                        DensityChunks,
                        DensityGrid,
                        ChunkHash,
                        GridCenter,
                        CellSizeXZ,
                        GridResolutionXZ,
                        DensityChunks.Length);
                    float2 attractor = ThreatAttractorGrid.IsCreated
                        ? SampleThreatAttractorAtPositionHashed(
                            position,
                            DensityChunks,
                            ThreatAttractorGrid,
                            ChunkHash,
                            GridCenter,
                            CellSizeXZ,
                            GridResolutionXZ,
                            DensityChunks.Length)
                        : float2.zero;
                    float obstacle = (density.y * KelpObstacleWeight) +
                                     (density.z * (SargassumObstacleWeight * 0.35f)) +
                                     (attractor.x * SargassumObstacleWeight) +
                                     (attractor.y * TechnoObstacleWeight);
                    return math.saturate(obstacle);
                }

                return 0f;
            }

            private bool IsInsideBlockingStructure(int columnIndex, float3 position)
            {
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    columnIndex < 0)
                {
                    return false;
                }

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(columnIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        if (position.x >= structure.MinX &&
                            position.x <= structure.MaxX &&
                            position.y >= structure.MinY &&
                            position.y <= structure.MaxY &&
                            position.z >= structure.MinZ &&
                            position.z <= structure.MaxZ)
                        {
                            return true;
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return false;
            }

            private static byte EncodeOpenThreat(float threat)
            {
                return (byte)math.clamp((int)math.round(math.saturate(threat) * 254f), 0, 254);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SwarmWakeImpulse
        {
            public float3 Position;
            public float Radius;
            public float3 FlowVector;
            public float Strength;
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalFlowFieldJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> FlowChunks;
            [ReadOnly] public NativeArray<float3> FlowDensityGrid;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ChunkHash;
            [ReadOnly] public NativeArray<float> NavSupportGrid;
            [ReadOnly] public NativeArray<SwarmWakeImpulse> ExternalWakeImpulses;
            [WriteOnly] public NativeArray<float2> Output;
            public int GridResolution;
            public int ChunkCount;
            public int ExternalWakeImpulseCount;
            public float CellSize;
            public float3 GridCenter;
            public float3 PlayerPosition;
            public float3 HotspotPosition;
            public float HotspotThreatLevel;
            public uint WeatherStateMask;
            public float2 WeatherDirectionXZ;
            public float WeatherCurrentSpeed;
            public float WeatherIntensity;
            public float ThreatBias;
            public float PlayerBias;
            public float HotspotBias;
            public float ObstacleAvoidBias;
            public float NavSupportBias;
            public float KelpObstacleWeight;
            public float SargassumObstacleWeight;
            public float TechnoObstacleWeight;
            public float ObstacleSoftThreshold;
            public float ObstacleHardThreshold;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length || GridResolution <= 0)
                    return;

                int cellX = index % GridResolution;
                int cellZ = index / GridResolution;
                int halfExtent = GridResolution >> 1;
                float worldX = GridCenter.x + ((cellX - halfExtent) * CellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * CellSize);
                float3 position = new float3(worldX, GridCenter.y, worldZ);

                float2 threatGradient = DominantAxisOrDefault(ComputeThreatGradient(cellX, cellZ), float2.zero);
                float2 toPlayer = DominantAxisOrDefault(new float2(PlayerPosition.x - worldX, PlayerPosition.z - worldZ), threatGradient);
                float hotspotBlend = math.saturate(HotspotThreatLevel);
                float2 toHotspot = DominantAxisOrDefault(new float2(HotspotPosition.x - worldX, HotspotPosition.z - worldZ), toPlayer);
                float2 seekDir = DominantAxisOrDefault(
                    (threatGradient * ThreatBias) +
                    (toPlayer * PlayerBias * (1f - hotspotBlend)) +
                    (toHotspot * HotspotBias * hotspotBlend),
                    toPlayer);

                float centerObstacle = SampleObstacle(position);
                float2 obstacleGradient = ComputeObstacleGradient(position);
                float obstacleRange = math.max(0.0001f, ObstacleHardThreshold - ObstacleSoftThreshold);
                float obstacleFactor = math.saturate((centerObstacle - ObstacleSoftThreshold) * math.rcp(obstacleRange));
                float2 avoidanceDir = DominantAxisOrDefault(-obstacleGradient, new float2(0f, 0f));

                float navSupport = SampleNavSupport(cellX, cellZ);
                float2 roadDir = DominantAxisOrDefault(ComputeNavGradient(cellX, cellZ), seekDir);
                float2 wakeDir = SampleWakeFlow(position);
                float2 weatherBias = ResolveWeatherBias();

                float2 combined = seekDir;
                combined += roadDir * NavSupportBias * navSupport;
                combined += wakeDir;
                combined += weatherBias;
                combined += avoidanceDir * ObstacleAvoidBias * math.max(obstacleFactor, centerObstacle);
                if (centerObstacle >= ObstacleHardThreshold && navSupport <= 0.001f)
                    combined = avoidanceDir * math.max(1f, ObstacleAvoidBias);

                float resolvedSpeed = ResolveFlowSpeedMetersPerSecond(wakeDir);
                Output[index] = DominantAxisOrDefault(combined, float2.zero) * resolvedSpeed;
            }

            private float2 ResolveWeatherBias()
            {
                if (math.lengthsq(WeatherDirectionXZ) <= 0.0001f)
                    return float2.zero;

                return WeatherDirectionXZ * (ResolveWeatherBiasMultiplier() * math.max(0.05f, WeatherCurrentSpeed));
            }

            private float ResolveWeatherBiasMultiplier()
            {
                float stateBlend = math.max(0.15f, WeatherIntensity);
                if ((WeatherStateMask & (uint)WeatherState.ThermoclineActive) != 0u ||
                    (WeatherStateMask & (uint)WeatherState.HaloclineActive) != 0u)
                {
                    return 1.35f * stateBlend;
                }

                if ((WeatherStateMask & (uint)WeatherState.Storm) != 0u)
                    return 1f * stateBlend;

                if ((WeatherStateMask & (uint)WeatherState.Calm) != 0u)
                    return 0.15f;

                return 0f;
            }

            private float ResolveFlowSpeedMetersPerSecond(float2 wakeDir)
            {
                float baseSpeed = math.max(0.05f, WeatherCurrentSpeed * math.max(0.35f, WeatherIntensity));
                float wakeSpeed = ApproxMagnitude2(wakeDir);
                float hotspotSpeed = math.saturate(HotspotThreatLevel) * 0.85f;
                return math.min(20f, baseSpeed + wakeSpeed + hotspotSpeed);
            }

            private float2 ComputeThreatGradient(int cellX, int cellZ)
            {
                return new float2(
                    SampleThreat(cellX + 1, cellZ) - SampleThreat(cellX - 1, cellZ),
                    SampleThreat(cellX, cellZ + 1) - SampleThreat(cellX, cellZ - 1));
            }

            private float2 ComputeNavGradient(int cellX, int cellZ)
            {
                return new float2(
                    SampleNavSupport(cellX + 1, cellZ) - SampleNavSupport(cellX - 1, cellZ),
                    SampleNavSupport(cellX, cellZ + 1) - SampleNavSupport(cellX, cellZ - 1));
            }

            private float2 ComputeObstacleGradient(float3 position)
            {
                float3 offsetX = new float3(CellSize, 0f, 0f);
                float3 offsetZ = new float3(0f, 0f, CellSize);
                return new float2(
                    SampleObstacle(position + offsetX) - SampleObstacle(position - offsetX),
                    SampleObstacle(position + offsetZ) - SampleObstacle(position - offsetZ));
            }

            private float SampleObstacle(float3 position)
            {
                if (ChunkCount <= 0 || !FlowChunks.IsCreated || !FlowDensityGrid.IsCreated)
                    return 0f;

                float3 density = SampleDensityChannelsAtPositionHashed(
                    position,
                    FlowChunks,
                    FlowDensityGrid,
                    ChunkHash,
                    GridCenter,
                    CellSize,
                    GridResolution,
                    ChunkCount);
                float2 attractor = ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPositionHashed(
                        position,
                        FlowChunks,
                        ThreatAttractorGrid,
                        ChunkHash,
                        GridCenter,
                        CellSize,
                        GridResolution,
                        ChunkCount)
                    : float2.zero;
                float obstacle = (density.y * KelpObstacleWeight) +
                                 (density.z * (SargassumObstacleWeight * 0.35f)) +
                                 (attractor.x * SargassumObstacleWeight) +
                                 (attractor.y * TechnoObstacleWeight);
                return math.saturate(obstacle);
            }

            private float SampleThreat(int cellX, int cellZ)
            {
                if (!ThreatGrid.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return ThreatGrid[(cellZ * GridResolution) + cellX];
            }

            private float SampleNavSupport(int cellX, int cellZ)
            {
                if (!NavSupportGrid.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return math.saturate(NavSupportGrid[(cellZ * GridResolution) + cellX]);
            }

            private float2 SampleWakeFlow(float3 position)
            {
                if (!ExternalWakeImpulses.IsCreated || ExternalWakeImpulseCount <= 0)
                    return float2.zero;

                float2 wake = float2.zero;
                for (int i = 0; i < ExternalWakeImpulseCount; i++)
                {
                    SwarmWakeImpulse impulse = ExternalWakeImpulses[i];
                    if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                        continue;

                    float radius = math.max(impulse.Radius, 0.001f);
                    float2 planarDelta = new float2(position.x - impulse.Position.x, position.z - impulse.Position.z);
                    float planarDistanceSq = math.lengthsq(planarDelta);
                    float radiusSq = radius * radius;
                    if (planarDistanceSq > radiusSq)
                        continue;

                    float inverseRadiusSq = math.rcp(radiusSq);
                    float planarGate = math.saturate(1f - (planarDistanceSq * inverseRadiusSq));
                    if (planarGate <= 0f)
                        continue;

                    float verticalGate = math.saturate(1f - (math.abs(position.y - impulse.Position.y) * math.rcp(radius)));
                    float weight = planarGate * planarGate * verticalGate * impulse.Strength;
                    wake += DominantAxisOrDefault(new float2(impulse.FlowVector.x, impulse.FlowVector.z), float2.zero) * weight;
                }

                return wake;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalThermalGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [NativeDisableParallelForRestriction]
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public int HorizontalResolution;
            public int VerticalResolution;
            public int RingOffsetX;
            public int RingOffsetY;
            public int RingOffsetZ;
            public float HorizontalCellSize;
            public float VerticalCellSize;
            public float WaterLevel;
            public float GridDepthMeters;
            public float SurfaceTemperatureCelsius;
            public float AbyssTemperatureCelsius;
            public float ThermoclineDepth;
            public float DepthFalloffExponent;
            public float ColonyBiomeStartDepth;
            public float DeadZoneStartDepth;
            public float HotPocketBoostCelsius;
            public float HotPocketNoiseScale;
            public float HotPocketThreshold;
            public float ColonyPocketStrength;
            public float DeadZonePocketStrength;
            public float3 GridCenter;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    HorizontalResolution <= 0 ||
                    VerticalResolution <= 0)
                {
                    return;
                }

                int cellsPerLayer = HorizontalResolution * HorizontalResolution;
                int layer = index / cellsPerLayer;
                int rem = index - (layer * cellsPerLayer);
                int cellZ = rem / HorizontalResolution;
                int cellX = rem - (cellZ * HorizontalResolution);
                int halfExtent = HorizontalResolution >> 1;

                float worldX = GridCenter.x + ((cellX - halfExtent) * HorizontalCellSize);
                float worldY = WaterLevel - (layer * VerticalCellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * HorizontalCellSize);
                float depthMeters = math.clamp(WaterLevel - worldY, 0f, GridDepthMeters);
                float baseTemperature = ResolveBaseTemperature(depthMeters);
                float pocketHeat = ResolvePocketHeat(new float3(worldX, worldY, worldZ), depthMeters);
                int physicalIndex = GetPhysicalIndex(cellX, layer, cellZ);
                Output[physicalIndex] = baseTemperature + pocketHeat;
            }

            private float ResolveBaseTemperature(float depthMeters)
            {
                float inverseGridDepth = math.rcp(math.max(1f, GridDepthMeters));
                float normalizedDepth = math.saturate(depthMeters * inverseGridDepth);
                float thermocline01 = ThermoclineDepth <= 0.01f
                    ? normalizedDepth
                    : math.saturate(depthMeters * math.rcp(math.max(1f, ThermoclineDepth))) * 0.24f;

                if (depthMeters > ThermoclineDepth)
                {
                    float remainingDepth = math.max(1f, GridDepthMeters - ThermoclineDepth);
                    float deep01 = math.saturate((depthMeters - ThermoclineDepth) * math.rcp(remainingDepth));
                    thermocline01 = 0.24f + (ApproximateDepthFalloff01(deep01, DepthFalloffExponent) * 0.76f);
                }

                thermocline01 = math.max(thermocline01, normalizedDepth * 0.18f);
                return math.lerp(SurfaceTemperatureCelsius, AbyssTemperatureCelsius, math.saturate(thermocline01));
            }

            private static float ApproximateDepthFalloff01(float value, float exponent)
            {
                float t = math.saturate(value);
                float slowCurve = t * t;
                float fastCurve = t * (2f - t);
                float highExponentWeight = math.saturate((exponent - 1f) * 0.5f);
                float lowExponentWeight = math.saturate(1f - exponent);
                float shaped = math.lerp(t, slowCurve, highExponentWeight);
                return math.lerp(shaped, fastCurve, lowExponentWeight);
            }

            private float ResolvePocketHeat(float3 position, float depthMeters)
            {
                float2 attractor = ChunkCount > 0 && ThreatChunks.IsCreated && ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPosition(position, ThreatChunks, ThreatAttractorGrid, ChunkCount)
                    : float2.zero;
                float colony01 = math.saturate((depthMeters - ColonyBiomeStartDepth) * math.rcp(math.max(1f, DeadZoneStartDepth - ColonyBiomeStartDepth)));
                colony01 *= math.saturate(attractor.y * ColonyPocketStrength);

                float deadZone01 = math.saturate((depthMeters - DeadZoneStartDepth) * math.rcp(math.max(1f, GridDepthMeters - DeadZoneStartDepth)));
                deadZone01 *= DeadZonePocketStrength;

                float pocketNoise = SampleValueNoise(
                    ((position.x + (position.y * 0.37f)) * HotPocketNoiseScale) + 13.17f,
                    ((position.z - (position.y * 0.19f)) * HotPocketNoiseScale) + 29.41f,
                    0x91E10DA5u);
                float pocketMask = math.saturate((pocketNoise - HotPocketThreshold) * math.rcp(math.max(0.0001f, 1f - HotPocketThreshold)));
                float pocketBias = math.max(colony01, deadZone01);
                return HotPocketBoostCelsius * pocketMask * pocketBias;
            }

            private int GetPhysicalIndex(int x, int y, int z)
            {
                int wrappedX = WrapIndex(x + RingOffsetX, HorizontalResolution);
                int wrappedY = WrapIndex(y + RingOffsetY, VerticalResolution);
                int wrappedZ = WrapIndex(z + RingOffsetZ, HorizontalResolution);
                return (wrappedY * HorizontalResolution * HorizontalResolution) + (wrappedZ * HorizontalResolution) + wrappedX;
            }

            private static int WrapIndex(int value, int length)
            {
                if (length <= 0)
                    return 0;

                int wrapped = value % length;
                return wrapped < 0 ? wrapped + length : wrapped;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalFlowVolumeJob : IJobParallelFor
        {
            private const float ThermoclineHalfBandMeters = 8f;
            private const float ThermoclineVerticalAttenuation = 0.1f;
            private const float SurfaceStormLayerDepthMeters = 50f;
            private const float StormSurfaceTurbulenceStrength = 0.4f;

            [ReadOnly] public NativeArray<float> ThermalGrid;
            [ReadOnly] public NativeArray<SwarmWakeImpulse> ExternalWakeImpulses;
            [NativeDisableParallelForRestriction]
            [WriteOnly] public NativeArray<float3> Output;
            public int HorizontalResolution;
            public int VerticalResolution;
            public int RingOffsetX;
            public int RingOffsetY;
            public int RingOffsetZ;
            public int ExternalWakeImpulseCount;
            public float HorizontalCellSize;
            public float VerticalCellSize;
            public float WaterLevel;
            public float GridDepthMeters;
            public float ThermoclineDepthMeters;
            public uint WeatherStateMask;
            public float2 WeatherDirectionXZ;
            public float WeatherCurrentSpeed;
            public float WeatherIntensity;
            public float ThermalIntensity;
            public float3 GridCenter;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    !ThermalGrid.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    index >= ThermalGrid.Length ||
                    HorizontalResolution <= 0 ||
                    VerticalResolution <= 0)
                {
                    return;
                }

                int cellsPerLayer = HorizontalResolution * HorizontalResolution;
                int layer = index / cellsPerLayer;
                int rem = index - (layer * cellsPerLayer);
                int cellZ = rem / HorizontalResolution;
                int cellX = rem - (cellZ * HorizontalResolution);
                int halfExtent = HorizontalResolution >> 1;

                float worldX = GridCenter.x + ((cellX - halfExtent) * HorizontalCellSize);
                float worldY = WaterLevel - (layer * VerticalCellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * HorizontalCellSize);
                float depthMeters = math.clamp(WaterLevel - worldY, 0f, GridDepthMeters);
                int physicalIndex = GetPhysicalIndex(cellX, layer, cellZ);
                float localTemperature = ThermalGrid[physicalIndex];
                float aboveTemperature = ThermalGrid[GetPhysicalIndex(cellX, math.max(0, layer - 1), cellZ)];
                float belowTemperature = ThermalGrid[GetPhysicalIndex(cellX, math.min(VerticalResolution - 1, layer + 1), cellZ)];

                float2 weatherDirection = DominantAxisOrDefault(WeatherDirectionXZ, new float2(0f, 1f));
                float2 horizontalCurrent = weatherDirection * WeatherCurrentSpeed;
                float verticalCurrent = (aboveTemperature - belowTemperature) * math.max(0.05f, ThermalIntensity);
                float thermalOffset = localTemperature - belowTemperature;
                verticalCurrent += thermalOffset * 0.02f;

                if ((WeatherStateMask & (uint)WeatherState.Storm) != 0u)
                {
                    float surfaceLayer01 = 1f - math.saturate(depthMeters * math.rcp(math.max(SurfaceStormLayerDepthMeters, 0.0001f)));
                    float stormBiasScale = WeatherCurrentSpeed * math.max(0.35f, WeatherIntensity);
                    horizontalCurrent += weatherDirection * stormBiasScale;
                    if (surfaceLayer01 > 0.0001f)
                    {
                        float noiseX = (HectonMapMagicVegetationBridge.SampleValueNoise((worldX * 0.11f) + 17.3f, (worldZ * 0.11f) + 11.1f, 0x6D2B79F5u) * 2f) - 1f;
                        float noiseZ = (HectonMapMagicVegetationBridge.SampleValueNoise((worldX * 0.13f) - 5.7f, (worldZ * 0.13f) + 23.9f, 0xB5297A4Du) * 2f) - 1f;
                        horizontalCurrent += new float2(noiseX, noiseZ) *
                                             (StormSurfaceTurbulenceStrength * surfaceLayer01 * math.max(0.1f, WeatherIntensity));
                    }
                }

                float3 flow = new float3(horizontalCurrent.x, verticalCurrent, horizontalCurrent.y);
                flow += SampleWakeImpulse(new float3(worldX, worldY, worldZ));

                if ((WeatherStateMask & ((uint)WeatherState.ThermoclineActive | (uint)WeatherState.HaloclineActive)) != 0u)
                {
                    float thermoclineBand01 = 1f - math.saturate(math.abs(depthMeters - ThermoclineDepthMeters) * math.rcp(math.max(ThermoclineHalfBandMeters, 0.0001f)));
                    if (thermoclineBand01 > 0.0001f)
                        flow.y = math.lerp(flow.y, flow.y * ThermoclineVerticalAttenuation, thermoclineBand01);
                }

                Output[physicalIndex] = flow;
            }

            private float3 SampleWakeImpulse(float3 position)
            {
                if (!ExternalWakeImpulses.IsCreated || ExternalWakeImpulseCount <= 0)
                    return float3.zero;

                float3 wake = float3.zero;
                for (int i = 0; i < ExternalWakeImpulseCount; i++)
                {
                    SwarmWakeImpulse impulse = ExternalWakeImpulses[i];
                    if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                        continue;

                    float radius = math.max(impulse.Radius, 0.001f);
                    float3 delta = position - impulse.Position;
                    float distanceSq = math.lengthsq(delta);
                    float radiusSq = radius * radius;
                    if (distanceSq > radiusSq)
                        continue;

                    float weight = math.saturate(1f - (distanceSq * math.rcp(radiusSq)));
                    if (weight <= 0f)
                        continue;

                    wake += DominantAxisOrDefault(impulse.FlowVector, float3.zero) * (weight * weight * impulse.Strength);
                }

                return wake;
            }

            private int GetPhysicalIndex(int x, int y, int z)
            {
                int wrappedX = WrapIndex(x + RingOffsetX, HorizontalResolution);
                int wrappedY = WrapIndex(y + RingOffsetY, VerticalResolution);
                int wrappedZ = WrapIndex(z + RingOffsetZ, HorizontalResolution);
                return (wrappedY * HorizontalResolution * HorizontalResolution) + (wrappedZ * HorizontalResolution) + wrappedX;
            }

            private static int WrapIndex(int value, int length)
            {
                if (length <= 0)
                    return 0;

                int wrapped = value % length;
                return wrapped < 0 ? wrapped + length : wrapped;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct NativeAStarJob : IJob
        {
            [ReadOnly] public NativeArray<Vector3> Nodes;
            [ReadOnly] public NativeArray<byte> NodeTypes;
            [ReadOnly] public NativeArray<Vector3> ConduitVectors;
            [ReadOnly] public NativeArray<float> ConduitStrengths;
            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            [ReadOnly] public NativeArray<PredatorFearNodeSnapshot> PredatorFearNodes;
            public NativeArray<int> Parents;
            public NativeArray<float> GScore;
            public NativeArray<float> FScore;
            public NativeArray<byte> ClosedFlags;
            public NativeArray<int> HeapNodes;
            public NativeArray<int> HeapPositions;
            public NativeList<Vector3> Path;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public float WaterLevel;
            public int StartNode;
            public int EndNode;
            public float3 StartPosition;
            public float3 EndPosition;
            public float NeighborRadius;
            public float VerticalTolerance;
            public float ThreatPenaltyWeight;
            public float PredatorFearPenaltyWeight;
            public float ConduitStartDepth;
            public float ConduitVerticalToleranceBonus;
            public float ConduitMisalignmentPenalty;
            public float ConduitAlignmentReward;
            public float InteriorTraversalCostMultiplier;
            public int MaxExpandedNodes;
            public int PredatorFearNodeCount;
            public int TraversalSpeciesId;

            public void Execute()
            {
                if (!Nodes.IsCreated ||
                    Nodes.Length <= 0 ||
                    !Path.IsCreated ||
                    StartNode < 0 ||
                    EndNode < 0 ||
                    StartNode >= Nodes.Length ||
                    EndNode >= Nodes.Length ||
                    MaxExpandedNodes <= 0 ||
                    NeighborRadius <= 0f ||
                    !math.isfinite(NeighborRadius) ||
                    !math.isfinite(VerticalTolerance) ||
                    !math.all(math.isfinite(StartPosition)) ||
                    !math.all(math.isfinite(EndPosition)))
                {
                    if (Path.IsCreated)
                        Path.Clear();
                    return;
                }

                Path.Clear();
                int nodeCount = Nodes.Length;
                if (!HasCompleteAStarWorkspace(nodeCount))
                    return;

                if (!math.all(math.isfinite(ToFloat3(Nodes[StartNode]))) ||
                    !math.all(math.isfinite(ToFloat3(Nodes[EndNode]))))
                {
                    return;
                }

                float neighborRadiusSq = NeighborRadius * NeighborRadius;
                if (!math.isfinite(neighborRadiusSq))
                    return;

                float threatPenaltyWeight = SanitizeNonNegative(ThreatPenaltyWeight);
                float conduitMisalignmentPenalty = SanitizeNonNegative(ConduitMisalignmentPenalty);
                float conduitAlignmentReward = SanitizeSaturate(ConduitAlignmentReward);
                int heapCount = 0;

                for (int i = 0; i < nodeCount; i++)
                {
                    Parents[i] = -1;
                    GScore[i] = float.PositiveInfinity;
                    FScore[i] = float.PositiveInfinity;
                    ClosedFlags[i] = 0;
                    HeapPositions[i] = -1;
                }

                GScore[StartNode] = 0f;
                float startHeuristic = HeuristicCost(StartNode);
                if (!math.isfinite(startHeuristic))
                    return;

                FScore[StartNode] = startHeuristic;
                HeapPushOrDecrease(StartNode, ref heapCount);

                int expandedNodes = 0;
                bool foundPath = StartNode == EndNode;
                while (heapCount > 0 && expandedNodes < MaxExpandedNodes)
                {
                    int current = HeapPop(ref heapCount);
                    if (current < 0)
                        break;

                    if (ClosedFlags[current] != 0)
                        continue;

                    ClosedFlags[current] = 1;
                    expandedNodes++;
                    if (current == EndNode)
                    {
                        foundPath = true;
                        break;
                    }

                    float3 currentNode = ToFloat3(Nodes[current]);
                    if (!math.all(math.isfinite(currentNode)))
                        continue;
                    if (!math.isfinite(GScore[current]))
                        continue;

                    for (int neighbor = 0; neighbor < nodeCount; neighbor++)
                    {
                        if (neighbor == current || ClosedFlags[neighbor] != 0)
                            continue;

                        float3 neighborNode = ToFloat3(Nodes[neighbor]);
                        if (!math.all(math.isfinite(neighborNode)))
                            continue;

                        float verticalDelta = neighborNode.y - currentNode.y;
                        float3 delta = neighborNode - currentNode;
                        float distanceSq = math.lengthsq(delta);
                        if (distanceSq <= 0.000001f ||
                            distanceSq > neighborRadiusSq ||
                            !math.isfinite(distanceSq))
                        {
                            continue;
                        }

                        float distance = EstimateLength3D(delta);
                        if (distance <= 0.0001f || !math.isfinite(distance))
                            continue;

                        float conduitStrength = ResolveConduitStrength(current, neighbor, currentNode, neighborNode, delta, distance, out float conduitAlignment, out float verticalBonus);
                        float allowedVertical = math.max(0f, VerticalTolerance + verticalBonus);
                        if ((verticalDelta * verticalDelta) > (allowedVertical * allowedVertical))
                            continue;

                        float threatPenalty = math.saturate(SampleThreatAtWorldPosition(neighborNode)) * threatPenaltyWeight;
                        float conduitPenalty = conduitStrength * ((1f - conduitAlignment) * conduitMisalignmentPenalty);
                        float conduitThreatReduction = threatPenalty * conduitStrength * conduitAlignment * conduitAlignmentReward;
                        float traversalMultiplier = math.max(1f, ResolveTraversalMultiplier(current, neighbor));
                        float traversalCost = distance * traversalMultiplier;
                        float tentativeG = GScore[current] + traversalCost + math.max(0f, threatPenalty - conduitThreatReduction) + conduitPenalty;
                        if (tentativeG >= GScore[neighbor] || !math.isfinite(tentativeG))
                            continue;

                        float neighborHeuristic = HeuristicCost(neighbor);
                        float resolvedFScore = tentativeG + neighborHeuristic;
                        if (!math.isfinite(neighborHeuristic) || !math.isfinite(resolvedFScore))
                            continue;

                        Parents[neighbor] = current;
                        GScore[neighbor] = tentativeG;
                        FScore[neighbor] = resolvedFScore;
                        HeapPushOrDecrease(neighbor, ref heapCount);
                    }
                }

                if (!foundPath)
                    return;

                Path.AddNoResize(new Vector3(EndPosition.x, EndPosition.y, EndPosition.z));
                int nodeIndex = EndNode;
                int pathIterations = 0;
                int reconstructionLimit = math.min(nodeCount, MaxPathReconstructionIterations);
                bool reachedStartNode = false;
                while (nodeIndex >= 0 && pathIterations < reconstructionLimit)
                {
                    pathIterations++;
                    float3 node = ToFloat3(Nodes[nodeIndex]);
                    if (!math.all(math.isfinite(node)))
                    {
                        Path.Clear();
                        return;
                    }

                    Path.AddNoResize(new Vector3(node.x, node.y, node.z));
                    if (nodeIndex == StartNode)
                    {
                        reachedStartNode = true;
                        break;
                    }

                    int parentIndex = Parents[nodeIndex];
                    if (parentIndex < 0 || parentIndex >= nodeCount)
                    {
                        nodeIndex = -1;
                        break;
                    }

                    nodeIndex = parentIndex;
                }

                if (!reachedStartNode)
                {
                    Path.Clear();
                    return;
                }

                Path.AddNoResize(new Vector3(StartPosition.x, StartPosition.y, StartPosition.z));
                ReversePath();
            }

            private bool HasCompleteAStarWorkspace(int nodeCount)
            {
                int requiredPathCapacity = math.min(nodeCount, MaxPathReconstructionIterations) + 2;
                return Parents.IsCreated &&
                       GScore.IsCreated &&
                       FScore.IsCreated &&
                       ClosedFlags.IsCreated &&
                       HeapNodes.IsCreated &&
                       HeapPositions.IsCreated &&
                       Path.IsCreated &&
                       Path.Capacity >= requiredPathCapacity &&
                       Parents.Length >= nodeCount &&
                       GScore.Length >= nodeCount &&
                       FScore.Length >= nodeCount &&
                       ClosedFlags.Length >= nodeCount &&
                       HeapNodes.Length >= nodeCount &&
                       HeapPositions.Length >= nodeCount;
            }

            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }

            private static float SanitizeSaturate(float value)
            {
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            private void ReversePath()
            {
                int count = Path.Length;
                for (int i = 0; i < count >> 1; i++)
                {
                    int swapIndex = count - 1 - i;
                    Vector3 temp = Path[i];
                    Path[i] = Path[swapIndex];
                    Path[swapIndex] = temp;
                }
            }

            private float HeuristicCost(int nodeIndex)
            {
                float3 node = ToFloat3(Nodes[nodeIndex]);
                float3 goal = ToFloat3(Nodes[EndNode]);
                float horizontalDistance = EstimateLength2D(node.xz - goal.xz);
                float verticalPenalty = math.abs(node.y - goal.y) * 1.85f;
                return horizontalDistance + verticalPenalty;
            }

            private float ResolveConduitStrength(
                int currentIndex,
                int neighborIndex,
                float3 currentNode,
                float3 neighborNode,
                float3 delta,
                float distance,
                out float conduitAlignment,
                out float verticalBonus)
            {
                conduitAlignment = 0f;
                verticalBonus = 0f;
                if (currentIndex < 0 ||
                    neighborIndex < 0 ||
                    !math.all(math.isfinite(currentNode)) ||
                    !math.all(math.isfinite(neighborNode)))
                {
                    return 0f;
                }

                if (!math.isfinite(WaterLevel) || !math.isfinite(ConduitStartDepth))
                    return 0f;

                float conduitStartDepth = math.max(0f, ConduitStartDepth);
                float depthMeters = math.max(0f, WaterLevel - math.min(currentNode.y, neighborNode.y));
                if (depthMeters < conduitStartDepth ||
                    !ConduitVectors.IsCreated ||
                    !ConduitStrengths.IsCreated ||
                    currentIndex >= ConduitVectors.Length ||
                    neighborIndex >= ConduitVectors.Length ||
                    currentIndex >= ConduitStrengths.Length ||
                    neighborIndex >= ConduitStrengths.Length ||
                    distance <= 0.0001f ||
                    !math.isfinite(distance) ||
                    !math.all(math.isfinite(delta)))
                {
                    return 0f;
                }

                float currentStrengthValue = ConduitStrengths[currentIndex];
                float neighborStrengthValue = ConduitStrengths[neighborIndex];
                if (!math.isfinite(currentStrengthValue))
                    currentStrengthValue = 0f;
                if (!math.isfinite(neighborStrengthValue))
                    neighborStrengthValue = 0f;

                float currentStrength = math.saturate(currentStrengthValue);
                float neighborStrength = math.saturate(neighborStrengthValue);
                float combinedStrength = math.max(currentStrength, neighborStrength);
                if (combinedStrength <= 0.0001f)
                    return 0f;

                float3 currentConduit = ToFloat3(ConduitVectors[currentIndex]);
                float3 neighborConduit = ToFloat3(ConduitVectors[neighborIndex]);
                if (!math.all(math.isfinite(currentConduit)) ||
                    !math.all(math.isfinite(neighborConduit)))
                {
                    return 0f;
                }

                float3 conduitVector = (currentConduit * currentStrength) + (neighborConduit * neighborStrength);
                if (math.lengthsq(conduitVector) <= 0.0001f)
                    return 0f;

                float edgeLengthSq = math.lengthsq(delta);
                if (edgeLengthSq <= 0.000001f || !math.isfinite(edgeLengthSq))
                    return 0f;

                float3 edgeDirection = delta * math.rsqrt(edgeLengthSq);
                float3 conduitDirection = DominantAxisOrDefault(conduitVector, edgeDirection);
                conduitAlignment = math.saturate((math.dot(edgeDirection, conduitDirection) * 0.5f) + 0.5f);
                verticalBonus = SanitizeNonNegative(ConduitVerticalToleranceBonus) * combinedStrength * conduitAlignment * math.abs(conduitDirection.y);
                return combinedStrength;
            }

            private float SampleThreatAtWorldPosition(float3 position)
            {
                if (!math.all(math.isfinite(position)))
                    return 1f;

                float voxelThreat = SampleThreatVoxelAtWorldPosition(position);
                float predatorFearThreat = SamplePredatorFearAtWorldPosition(position);

                if (!ThreatGrid.IsCreated ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f ||
                    !math.isfinite(ThreatGridCellSize) ||
                    !math.all(math.isfinite(ThreatGridCenter)) ||
                    !HasCompleteThreatGrid())
                {
                    return math.max(voxelThreat, predatorFearThreat);
                }

                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float localX = position.x - (ThreatGridCenter.x - halfExtent);
                float localZ = position.z - (ThreatGridCenter.z - halfExtent);
                if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                    return math.max(voxelThreat, predatorFearThreat);

                float inverseThreatGridCellSize = math.rcp(math.max(ThreatGridCellSize, 0.0001f));
                float cellCoordX = localX * inverseThreatGridCellSize;
                float cellCoordZ = localZ * inverseThreatGridCellSize;
                int x0 = math.clamp((int)math.floor(cellCoordX), 0, ThreatGridResolution - 1);
                int z0 = math.clamp((int)math.floor(cellCoordZ), 0, ThreatGridResolution - 1);
                int x1 = math.min(x0 + 1, ThreatGridResolution - 1);
                int z1 = math.min(z0 + 1, ThreatGridResolution - 1);
                float tx = math.saturate(cellCoordX - x0);
                float tz = math.saturate(cellCoordZ - z0);

                float h00 = ThreatGrid[(z0 * ThreatGridResolution) + x0];
                float h10 = ThreatGrid[(z0 * ThreatGridResolution) + x1];
                float h01 = ThreatGrid[(z1 * ThreatGridResolution) + x0];
                float h11 = ThreatGrid[(z1 * ThreatGridResolution) + x1];
                float hx0 = math.lerp(h00, h10, tx);
                float hx1 = math.lerp(h01, h11, tx);
                float surfaceThreat = math.lerp(hx0, hx1, tz);
                return math.max(math.max(surfaceThreat, voxelThreat), predatorFearThreat);
            }

            private float SamplePredatorFearAtWorldPosition(float3 position)
            {
                if (!PredatorFearNodes.IsCreated ||
                    PredatorFearNodeCount <= 0 ||
                    TraversalSpeciesId == 0 ||
                    PredatorFearPenaltyWeight <= 0f ||
                    !math.isfinite(PredatorFearPenaltyWeight))
                {
                    return 0f;
                }

                float strongest = 0f;
                int count = math.min(PredatorFearNodeCount, PredatorFearNodes.Length);
                for (int i = 0; i < count; i++)
                {
                    PredatorFearNodeSnapshot node = PredatorFearNodes[i];
                    if (node.SpeciesId != TraversalSpeciesId ||
                        node.Weight <= 0f ||
                        !math.isfinite(node.Weight) ||
                        !math.isfinite(node.Radius) ||
                        !math.all(math.isfinite(node.Position)))
                    {
                        continue;
                    }

                    float radius = math.max(node.Radius, 1f);
                    float2 delta = new float2(position.x - node.Position.x, position.z - node.Position.z);
                    float gate = 1f - math.saturate(EstimateLength2D(delta) * math.rcp(radius));
                    if (gate <= 0f)
                        continue;

                    strongest = math.max(strongest, node.Weight * gate);
                }

                return math.saturate(strongest * PredatorFearPenaltyWeight);
            }

            private float SampleThreatVoxelAtWorldPosition(float3 position)
            {
                if (!ThreatVoxelGrid.IsCreated)
                    return 0f;

                if (!math.all(math.isfinite(position)) ||
                    !math.all(math.isfinite(ThreatVoxelOrigin)) ||
                    !HasUsableThreatVoxelCellSize() ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0 ||
                    !HasCompleteThreatVoxelGrid())
                {
                    return 1f;
                }

                float3 local = position - ThreatVoxelOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                    return 0f;

                float3 inverseCellSize = math.rcp(math.max(ThreatVoxelCellSize, new float3(0.0001f, 0.0001f, 0.0001f)));
                int3 voxel = new int3(
                    (int)math.floor(local.x * inverseCellSize.x),
                    (int)math.floor(local.y * inverseCellSize.y),
                    (int)math.floor(local.z * inverseCellSize.z));
                if (voxel.x < 0 || voxel.y < 0 || voxel.z < 0 ||
                    voxel.x >= ThreatVoxelDimensions.x ||
                    voxel.y >= ThreatVoxelDimensions.y ||
                    voxel.z >= ThreatVoxelDimensions.z)
                {
                    return 0f;
                }

                int flatIndex = voxel.x + (voxel.y * ThreatVoxelDimensions.x) + (voxel.z * ThreatVoxelDimensions.x * ThreatVoxelDimensions.y);
                if (flatIndex < 0 || flatIndex >= ThreatVoxelGrid.Length)
                    return 1f;

                byte encoded = ThreatVoxelGrid[flatIndex];
                return encoded >= 255 ? 1f : encoded * math.rcp(254f);
            }

            private float ResolveTraversalMultiplier(int currentIndex, int neighborIndex)
            {
                if (!NodeTypes.IsCreated ||
                    currentIndex < 0 ||
                    neighborIndex < 0 ||
                    currentIndex >= NodeTypes.Length ||
                    neighborIndex >= NodeTypes.Length)
                {
                    return 1f;
                }

                if (NodeTypes[currentIndex] == (byte)NavNodeType.Interior ||
                    NodeTypes[neighborIndex] == (byte)NavNodeType.Interior)
                {
                    return math.max(1f, InteriorTraversalCostMultiplier);
                }

                return 1f;
            }

            private void HeapPushOrDecrease(int nodeIndex, ref int heapCount)
            {
                int heapIndex = HeapPositions[nodeIndex];
                if (heapIndex >= 0)
                {
                    SiftUp(heapIndex);
                    return;
                }

                if (!HeapNodes.IsCreated || heapCount >= HeapNodes.Length)
                    return;

                HeapNodes[heapCount] = nodeIndex;
                HeapPositions[nodeIndex] = heapCount;
                SiftUp(heapCount);
                heapCount++;
            }

            private int HeapPop(ref int heapCount)
            {
                if (heapCount <= 0)
                    return -1;

                int root = HeapNodes[0];
                heapCount--;
                int lastNode = HeapNodes[heapCount];
                HeapNodes[heapCount] = -1;
                HeapPositions[root] = -1;
                if (heapCount > 0)
                {
                    HeapNodes[0] = lastNode;
                    HeapPositions[lastNode] = 0;
                    SiftDown(0, heapCount);
                }

                return root;
            }

            private void SiftUp(int index)
            {
                int heapIterations = 0;
                while (index > 0 && heapIterations < MaxHeapRebalanceIterations)
                {
                    heapIterations++;
                    int parentIndex = (index - 1) >> 1;
                    int node = HeapNodes[index];
                    int parentNode = HeapNodes[parentIndex];
                    if (FScore[node] >= FScore[parentNode])
                        break;

                    HeapNodes[index] = parentNode;
                    HeapNodes[parentIndex] = node;
                    HeapPositions[node] = parentIndex;
                    HeapPositions[parentNode] = index;
                    index = parentIndex;
                }
            }

            private void SiftDown(int index, int heapCount)
            {
                int heapIterations = 0;
                while (heapIterations < MaxHeapRebalanceIterations)
                {
                    heapIterations++;
                    int left = (index << 1) + 1;
                    if (left >= heapCount)
                        break;

                    int right = left + 1;
                    int smallest = left;
                    if (right < heapCount && FScore[HeapNodes[right]] < FScore[HeapNodes[left]])
                        smallest = right;

                    if (FScore[HeapNodes[index]] <= FScore[HeapNodes[smallest]])
                        break;

                    int node = HeapNodes[index];
                    int smallestNode = HeapNodes[smallest];
                    HeapNodes[index] = smallestNode;
                    HeapNodes[smallest] = node;
                    HeapPositions[node] = smallest;
                    HeapPositions[smallestNode] = index;
                    index = smallest;
                }
            }

            private static float3 ToFloat3(Vector3 value)
            {
                return new float3(value.x, value.y, value.z);
            }

            private static float EstimateLength2D(float2 value)
            {
                float ax = math.abs(value.x);
                float ay = math.abs(value.y);
                float max = math.max(ax, ay);
                float min = math.min(ax, ay);
                return max + (min * 0.375f);
            }

            private static float EstimateLength3D(float3 value)
            {
                float ax = math.abs(value.x);
                float ay = math.abs(value.y);
                float az = math.abs(value.z);
                float max = math.max(ax, math.max(ay, az));
                float min = math.min(ax, math.min(ay, az));
                float mid = ax + ay + az - max - min;
                return max + (mid * 0.375f) + (min * 0.25f);
            }

            private bool HasCompleteThreatGrid()
            {
                if (!ThreatGrid.IsCreated || ThreatGridResolution <= 0)
                    return false;

                long resolution = ThreatGridResolution;
                long expectedLength = resolution * resolution;
                return expectedLength > 0L &&
                       expectedLength <= int.MaxValue &&
                       ThreatGrid.Length >= expectedLength;
            }

            private bool HasCompleteThreatVoxelGrid()
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return false;
                }

                long expectedLength = (long)ThreatVoxelDimensions.x * ThreatVoxelDimensions.y * ThreatVoxelDimensions.z;
                return expectedLength > 0L &&
                       expectedLength <= int.MaxValue &&
                       ThreatVoxelGrid.Length >= expectedLength;
            }

            private bool HasUsableThreatVoxelCellSize()
            {
                return math.all(math.isfinite(ThreatVoxelCellSize)) &&
                       ThreatVoxelCellSize.x > 0.000001f &&
                       ThreatVoxelCellSize.y > 0.000001f &&
                       ThreatVoxelCellSize.z > 0.000001f;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
        private struct NavPortal
        {
            public float3 Left;
            public float3 Right;
            public float WidthSq;
            public float Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StringPullPathJob : IJob
        {
            private const float FunnelEpsilon = 0.00001f;
            private const float DdaEpsilon = 0.000001f;
            private const byte SolidThreatVoxel = 255;

            [ReadOnly] public NativeArray<Vector3> InputPath;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [ReadOnly] public NativeArray<byte> NavPassabilityGrid;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            public NativeList<Vector3> OutputPath;
            public int ChunkCount;
            public int TerrainHoleCount;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public int3 NavPassabilityDimensions;
            public float3 NavPassabilityOrigin;
            public float NavPassabilityCellSize;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public float SampleSpacing;
            public int MaxSamplesPerSegment;
            public int MaxPortalLookAhead;
            public float KelpWeight;
            public float SargassumWeight;
            public float DensityObstacleThreshold;

            public void Execute()
            {
                if (!OutputPath.IsCreated)
                    return;

                OutputPath.Clear();
                if (!InputPath.IsCreated || InputPath.Length <= 0)
                    return;

                int pathCount = InputPath.Length;
                if (OutputPath.Capacity < pathCount || !HasFiniteInputPath(pathCount))
                    return;

                if (pathCount <= 2)
                {
                    for (int i = 0; i < pathCount; i++)
                        OutputPath.AddNoResize(InputPath[i]);

                    return;
                }

                OutputPath.AddNoResize(InputPath[0]);

                int apexIndex = 0;
                int leftIndex = 0;
                int rightIndex = 0;
                float3 apex = ToFloat3(InputPath[0]);
                float3 left = apex;
                float3 right = apex;
                float3 fallbackAxis = ResolvePortalAxis(0);

                for (int portalIndex = 1; portalIndex < pathCount; portalIndex++)
                {
                    NavPortal portal = BuildPortal(portalIndex, out float3 portalAxis);
                    float3 portalLeft = portal.Left;
                    float3 portalRight = portal.Right;
                    float3 windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    bool swapPortalWinding = ScalarTripleProduct(windingAxis, portalLeft - apex, portalRight - apex) < 0f;
                    float3 originalPortalLeft = portalLeft;
                    portalLeft = math.select(portalLeft, portalRight, swapPortalWinding);
                    portalRight = math.select(portalRight, originalPortalLeft, swapPortalWinding);

                    windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    if (ScalarTripleProduct(windingAxis, right - apex, portalRight - apex) <= FunnelEpsilon)
                    {
                        bool tightenRight = IsDegenerateRay(apex, right) || ScalarTripleProduct(windingAxis, left - apex, portalRight - apex) > FunnelEpsilon;
                        if (tightenRight)
                        {
                            right = math.select(right, portalRight, tightenRight);
                            rightIndex = math.select(rightIndex, portalIndex, tightenRight);
                            fallbackAxis = math.select(fallbackAxis, portalAxis, tightenRight);
                        }
                        else
                        {
                            int emitIndex = math.max(apexIndex + 1, leftIndex);
                            OutputPath.AddNoResize(InputPath[emitIndex]);
                            apexIndex = emitIndex;
                            apex = ToFloat3(InputPath[apexIndex]);
                            left = apex;
                            right = apex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;
                            fallbackAxis = ResolvePortalAxis(apexIndex);
                            portalIndex = apexIndex;
                            continue;
                        }
                    }

                    windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    if (ScalarTripleProduct(windingAxis, left - apex, portalLeft - apex) >= -FunnelEpsilon)
                    {
                        bool tightenLeft = IsDegenerateRay(apex, left) || ScalarTripleProduct(windingAxis, right - apex, portalLeft - apex) < -FunnelEpsilon;
                        if (tightenLeft)
                        {
                            left = math.select(left, portalLeft, tightenLeft);
                            leftIndex = math.select(leftIndex, portalIndex, tightenLeft);
                            fallbackAxis = math.select(fallbackAxis, portalAxis, tightenLeft);
                        }
                        else
                        {
                            int emitIndex = math.max(apexIndex + 1, rightIndex);
                            OutputPath.AddNoResize(InputPath[emitIndex]);
                            apexIndex = emitIndex;
                            apex = ToFloat3(InputPath[apexIndex]);
                            left = apex;
                            right = apex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;
                            fallbackAxis = ResolvePortalAxis(apexIndex);
                            portalIndex = apexIndex;
                            continue;
                        }
                    }

                    if (portalIndex == pathCount - 1)
                        break;
                }

                Vector3 endPoint = InputPath[pathCount - 1];
                if (OutputPath.Length == 0 || !Approximately(OutputPath[OutputPath.Length - 1], endPoint))
                    OutputPath.AddNoResize(endPoint);

                CompactPathByVoxelLineOfSight();
            }

            private bool HasFiniteInputPath(int pathCount)
            {
                if (!InputPath.IsCreated || pathCount <= 0)
                    return false;

                for (int i = 0; i < pathCount; i++)
                {
                    if (!math.all(math.isfinite(ToFloat3(InputPath[i]))))
                        return false;
                }

                return true;
            }

            private NavPortal BuildPortal(int index, out float3 portalAxis)
            {
                Vector3 centerValue = InputPath[index];
                float3 center = ToFloat3(centerValue);
                if (index <= 0 || index >= InputPath.Length - 1)
                {
                    portalAxis = ResolvePortalAxis(index);
                    return BuildNavPortal(center, center);
                }

                float3 previous = ToFloat3(InputPath[index - 1]);
                float3 next = ToFloat3(InputPath[index + 1]);
                float3 prevDirection = NormalizeRsqrtOrFallback(center - previous, new float3(0f, 0f, 1f));
                float3 nextDirection = NormalizeRsqrtOrFallback(next - center, prevDirection);
                portalAxis = NormalizeRsqrtOrFallback(prevDirection + nextDirection, nextDirection);
                float3 cornerNormal = NormalizeRsqrtOrFallback(math.cross(prevDirection, nextDirection), ResolvePerpendicular(portalAxis));
                float3 side = NormalizeRsqrtOrFallback(math.cross(cornerNormal, portalAxis), ResolvePerpendicular(portalAxis));
                float obstacle = SampleObstacle(centerValue);
                float obstacleT = math.saturate(obstacle * math.rcp(math.max(0.01f, DensityObstacleThreshold)));
                float maxHalfWidth = math.max(0.9f, SampleSpacing * 1.6f);
                float minHalfWidth = math.max(0.35f, SampleSpacing * 0.55f);
                float halfWidth = math.lerp(maxHalfWidth, minHalfWidth, obstacleT);
                return BuildNavPortal(center + (side * halfWidth), center - (side * halfWidth));
            }

            private float SampleObstacle(Vector3 positionValue)
            {
                float3 position = ToFloat3(positionValue);
                if (IsInsideTerrainHoleStatic(position.x, position.z, TerrainHoles, TerrainHoleCount))
                    return 0f;

                float obstacle = 0f;
                if (IsInsideBlockingStructure(position))
                    obstacle = math.max(obstacle, DensityObstacleThreshold);

                float3 density = SampleDensityChannelsAtPosition(position, DensityChunks, DensityGrid, ChunkCount);
                obstacle = math.max(obstacle, (density.y * KelpWeight) + (density.z * SargassumWeight));
                return obstacle;
            }

            private bool IsInsideBlockingStructure(float3 position)
            {
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                int cellIndex = ComputeThreatGridCellIndex(position);
                if (cellIndex < 0)
                    return false;

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        StructureType structureType = (StructureType)structure.Type;
                        if ((structureType == StructureType.BaseModule || structureType == StructureType.MegaWreck) &&
                            position.x >= structure.MinX &&
                            position.x <= structure.MaxX &&
                            position.y >= structure.MinY &&
                            position.y <= structure.MaxY &&
                            position.z >= structure.MinZ &&
                            position.z <= structure.MaxZ)
                        {
                            return true;
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return false;
            }

            private int ComputeThreatGridCellIndex(float3 position)
            {
                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float localX = position.x - (ThreatGridCenter.x - halfExtent);
                float localZ = position.z - (ThreatGridCenter.z - halfExtent);
                if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                    return -1;

                float inverseCellSize = math.rcp(math.max(ThreatGridCellSize, DdaEpsilon));
                int cellX = math.clamp((int)math.floor(localX * inverseCellSize), 0, ThreatGridResolution - 1);
                int cellZ = math.clamp((int)math.floor(localZ * inverseCellSize), 0, ThreatGridResolution - 1);
                return (cellZ * ThreatGridResolution) + cellX;
            }

            private void CompactPathByVoxelLineOfSight()
            {
                if (OutputPath.Length <= 2 || !HasAnyVoxelGrid())
                {
                    return;
                }

                int sourceLength = OutputPath.Length;
                int lastIndex = sourceLength - 1;
                int maxPortalLookAhead = math.max(1, MaxPortalLookAhead);
                int anchorIndex = 0;
                int writeIndex = 0;

                int compactionIterations = 0;
                while (anchorIndex < lastIndex && compactionIterations < MaxPathCompactionIterations)
                {
                    compactionIterations++;
                    Vector3 anchorPoint = OutputPath[anchorIndex];
                    OutputPath[writeIndex] = anchorPoint;
                    writeIndex++;

                    int farthestVisibleIndex = anchorIndex + 1;
                    int candidateLimit = math.min(lastIndex, anchorIndex + maxPortalLookAhead);
                    for (int candidateIndex = farthestVisibleIndex + 1; candidateIndex <= candidateLimit; candidateIndex++)
                    {
                        if (!HasVoxelLineOfSight(ToFloat3(anchorPoint), ToFloat3(OutputPath[candidateIndex])))
                            break;

                        farthestVisibleIndex = candidateIndex;
                    }

                    anchorIndex = farthestVisibleIndex;
                }

                if (anchorIndex < lastIndex)
                {
                    for (int remainingIndex = anchorIndex; remainingIndex <= lastIndex && writeIndex < sourceLength; remainingIndex++)
                    {
                        Vector3 remainingPoint = OutputPath[remainingIndex];
                        if (writeIndex == 0 || !Approximately(OutputPath[writeIndex - 1], remainingPoint))
                        {
                            OutputPath[writeIndex] = remainingPoint;
                            writeIndex++;
                        }
                    }

                    OutputPath.Length = writeIndex;
                    return;
                }

                Vector3 finalPoint = OutputPath[lastIndex];
                if (writeIndex == 0 || !Approximately(OutputPath[writeIndex - 1], finalPoint))
                {
                    OutputPath[writeIndex] = finalPoint;
                    writeIndex++;
                }

                OutputPath.Length = writeIndex;
            }

            private bool HasVoxelLineOfSight(float3 start, float3 end)
            {
                float3 delta = end - start;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= DdaEpsilon)
                    return true;

                if (!TryWorldToVoxel(start, out int3 currentVoxel) ||
                    !TryWorldToVoxel(end, out int3 targetVoxel))
                {
                    return false;
                }

                float3 activeVoxelOrigin = GetActiveVoxelOrigin();
                float3 activeVoxelCellSize = GetActiveVoxelCellSize();
                int3 activeVoxelDimensions = GetActiveVoxelDimensions();
                float3 rayDirection = NormalizeRsqrtOrFallback(delta, new float3(1f, 0f, 0f));
                bool3 positiveMask = rayDirection >= 0f;
                bool3 activeAxisMask = math.abs(rayDirection) > DdaEpsilon;
                int3 step = math.select(new int3(-1, -1, -1), new int3(1, 1, 1), positiveMask);
                float3 cellMin = activeVoxelOrigin + (new float3(currentVoxel.x, currentVoxel.y, currentVoxel.z) * activeVoxelCellSize);
                float3 voxelBoundary = cellMin + math.select(float3.zero, activeVoxelCellSize, positiveMask);
                float3 safeAbsDirection = math.max(math.abs(rayDirection), new float3(DdaEpsilon, DdaEpsilon, DdaEpsilon));
                float3 rayDirectionInverse = math.rcp(safeAbsDirection);
                float3 tMax = math.abs((voxelBoundary - start) * rayDirectionInverse);
                float3 tDelta = activeVoxelCellSize * rayDirectionInverse;
                tMax = math.select(new float3(1000000f, 1000000f, 1000000f), tMax, activeAxisMask);
                tDelta = math.select(new float3(1000000f, 1000000f, 1000000f), tDelta, activeAxisMask);

                int sampleStepCap = math.clamp(MaxSamplesPerSegment, 1, MaxThreatDdaSteps);
                long gridTraversalCap = (long)activeVoxelDimensions.x + activeVoxelDimensions.y + activeVoxelDimensions.z + 1L;
                int gridStepCap = gridTraversalCap > MaxThreatDdaSteps ? MaxThreatDdaSteps : (int)gridTraversalCap;
                int maxSteps = math.min(gridStepCap, sampleStepCap);
                for (int stepIndex = 0; stepIndex < maxSteps; stepIndex++)
                {
                    if (SampleVoxel(currentVoxel) >= SolidThreatVoxel)
                        return false;

                    if (math.all(currentVoxel == targetVoxel))
                        return true;

                    bool3 axisMask = (tMax <= tMax.yzx) & (tMax <= tMax.zxy);
                    tMax += math.select(float3.zero, tDelta, axisMask);
                    currentVoxel += math.select(int3.zero, step, axisMask);
                    if (!IsVoxelInside(currentVoxel))
                        return false;
                }

                return false;
            }

            private bool TryWorldToVoxel(float3 worldPosition, out int3 voxel)
            {
                float3 activeVoxelOrigin = GetActiveVoxelOrigin();
                float3 activeVoxelCellSize = GetActiveVoxelCellSize();
                if (!math.all(math.isfinite(worldPosition)) ||
                    !math.all(math.isfinite(activeVoxelOrigin)) ||
                    !HasUsableVoxelCellSize(activeVoxelCellSize))
                {
                    voxel = int3.zero;
                    return false;
                }

                float3 local = worldPosition - activeVoxelOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                {
                    voxel = int3.zero;
                    return false;
                }

                float inverseCellSizeX = math.rcp(math.max(activeVoxelCellSize.x, DdaEpsilon));
                float inverseCellSizeY = math.rcp(math.max(activeVoxelCellSize.y, DdaEpsilon));
                float inverseCellSizeZ = math.rcp(math.max(activeVoxelCellSize.z, DdaEpsilon));
                int3 candidate = new int3(
                    (int)math.floor(local.x * inverseCellSizeX),
                    (int)math.floor(local.y * inverseCellSizeY),
                    (int)math.floor(local.z * inverseCellSizeZ));
                if (!IsVoxelInside(candidate))
                {
                    voxel = int3.zero;
                    return false;
                }

                voxel = candidate;
                return true;
            }

            private bool IsVoxelInside(int3 voxel)
            {
                int3 activeVoxelDimensions = GetActiveVoxelDimensions();
                return voxel.x >= 0 &&
                       voxel.y >= 0 &&
                       voxel.z >= 0 &&
                       voxel.x < activeVoxelDimensions.x &&
                       voxel.y < activeVoxelDimensions.y &&
                       voxel.z < activeVoxelDimensions.z;
            }

            private byte SampleVoxel(int3 voxel)
            {
                if (HasNavPassabilityGrid())
                {
                    int flatIndex = FlattenThreatVoxelIndex(voxel, NavPassabilityDimensions);
                    if (flatIndex < 0 || flatIndex >= NavPassabilityGrid.Length)
                        return SolidThreatVoxel;

                    return NavPassabilityGrid[flatIndex];
                }

                int legacyFlatIndex = FlattenThreatVoxelIndex(voxel, ThreatVoxelDimensions);
                if (legacyFlatIndex < 0 || legacyFlatIndex >= ThreatVoxelGrid.Length)
                    return SolidThreatVoxel;

                return ThreatVoxelGrid[legacyFlatIndex];
            }

            private static int FlattenThreatVoxelIndex(int3 voxel, int3 dimensions)
            {
                return voxel.x + (voxel.y * dimensions.x) + (voxel.z * dimensions.x * dimensions.y);
            }

            private bool HasAnyVoxelGrid()
            {
                return HasNavPassabilityGrid() ||
                       (HasCompleteVoxelGrid(ThreatVoxelGrid, ThreatVoxelDimensions) &&
                        HasUsableVoxelCellSize(ThreatVoxelCellSize));
            }

            private bool HasNavPassabilityGrid()
            {
                return HasCompleteVoxelGrid(NavPassabilityGrid, NavPassabilityDimensions) &&
                       HasUsableUniformVoxelCellSize(NavPassabilityCellSize);
            }

            private static bool HasCompleteVoxelGrid(NativeArray<byte> grid, int3 dimensions)
            {
                if (!grid.IsCreated ||
                    dimensions.x <= 0 ||
                    dimensions.y <= 0 ||
                    dimensions.z <= 0)
                {
                    return false;
                }

                long expectedLength = (long)dimensions.x * dimensions.y * dimensions.z;
                return expectedLength > 0L &&
                       expectedLength <= int.MaxValue &&
                       grid.Length >= expectedLength;
            }

            private static bool HasUsableUniformVoxelCellSize(float cellSize)
            {
                return cellSize > DdaEpsilon &&
                       math.isfinite(cellSize);
            }

            private static bool HasUsableVoxelCellSize(float3 cellSize)
            {
                return math.all(math.isfinite(cellSize)) &&
                       cellSize.x > DdaEpsilon &&
                       cellSize.y > DdaEpsilon &&
                       cellSize.z > DdaEpsilon;
            }

            private int3 GetActiveVoxelDimensions()
            {
                return HasNavPassabilityGrid() ? NavPassabilityDimensions : ThreatVoxelDimensions;
            }

            private float3 GetActiveVoxelOrigin()
            {
                return HasNavPassabilityGrid() ? NavPassabilityOrigin : ThreatVoxelOrigin;
            }

            private float3 GetActiveVoxelCellSize()
            {
                return HasNavPassabilityGrid()
                    ? new float3(NavPassabilityCellSize, NavPassabilityCellSize, NavPassabilityCellSize)
                    : ThreatVoxelCellSize;
            }

            private float3 ResolvePortalAxis(int index)
            {
                int clampedIndex = math.clamp(index, 0, InputPath.Length - 1);
                int previousIndex = math.max(0, clampedIndex - 1);
                int nextIndex = math.min(InputPath.Length - 1, clampedIndex + 1);
                float3 previous = ToFloat3(InputPath[previousIndex]);
                float3 next = ToFloat3(InputPath[nextIndex]);
                return NormalizeRsqrtOrFallback(next - previous, new float3(0f, 0f, 1f));
            }

            private static float3 ToFloat3(Vector3 value)
            {
                return new float3(value.x, value.y, value.z);
            }

            private static float3 ResolvePerpendicular(float3 axis)
            {
                float3 reference = math.abs(axis.y) < 0.9f
                    ? new float3(0f, 1f, 0f)
                    : new float3(1f, 0f, 0f);
                return NormalizeRsqrtOrFallback(math.cross(reference, axis), new float3(0f, 0f, 1f));
            }

            private static float3 ResolveWindingAxis(
                float3 apex,
                float3 left,
                float3 right,
                float3 portalLeft,
                float3 portalRight,
                float3 portalAxis,
                float3 fallbackAxis)
            {
                float3 portalCenterDirection = ((portalLeft + portalRight) * 0.5f) - apex;
                if (math.lengthsq(portalCenterDirection) > FunnelEpsilon)
                    return NormalizeRsqrtOrFallback(portalCenterDirection, portalAxis);

                float3 wedgeCenterDirection = ((left + right) * 0.5f) - apex;
                if (math.lengthsq(wedgeCenterDirection) > FunnelEpsilon)
                    return NormalizeRsqrtOrFallback(wedgeCenterDirection, portalAxis);

                return NormalizeRsqrtOrFallback(portalAxis, NormalizeRsqrtOrFallback(fallbackAxis, new float3(0f, 0f, 1f)));
            }

            private static float ScalarTripleProduct(float3 axis, float3 b, float3 c)
            {
                return (axis.x * ((b.y * c.z) - (b.z * c.y))) +
                       (axis.y * ((b.z * c.x) - (b.x * c.z))) +
                       (axis.z * ((b.x * c.y) - (b.y * c.x)));
            }

            private static NavPortal BuildNavPortal(float3 left, float3 right)
            {
                if (!math.all(math.isfinite(left)))
                    left = float3.zero;
                if (!math.all(math.isfinite(right)))
                    right = left;

                float widthSq = math.lengthsq(right - left);
                if (!math.isfinite(widthSq))
                    widthSq = FunnelEpsilon;

                return new NavPortal
                {
                    Left = left,
                    Right = right,
                    WidthSq = math.max(widthSq, FunnelEpsilon),
                    Reserved = 0f
                };
            }

            private static float3 NormalizeRsqrtOrFallback(float3 value, float3 fallback)
            {
                float lengthSq = math.lengthsq(value);
                if (lengthSq > FunnelEpsilon && math.all(math.isfinite(value)))
                    return value * math.rsqrt(lengthSq);

                float fallbackLengthSq = math.lengthsq(fallback);
                if (fallbackLengthSq > FunnelEpsilon && math.all(math.isfinite(fallback)))
                    return fallback * math.rsqrt(fallbackLengthSq);

                return new float3(0f, 0f, 1f);
            }

            private static bool IsDegenerateRay(float3 apex, float3 point)
            {
                return math.lengthsq(point - apex) <= FunnelEpsilon;
            }

            private static bool Approximately(Vector3 a, Vector3 b)
            {
                float3 delta = ToFloat3(a) - ToFloat3(b);
                return math.lengthsq(delta) <= 0.0001f;
            }
        }
    }
}
