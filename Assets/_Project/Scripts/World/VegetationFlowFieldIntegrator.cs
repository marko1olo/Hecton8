using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
            out NativeArray<float2>.ReadOnly flowVectors,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            bool hasFlowField = TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.EcosystemFlowFieldHandle,
                BufferID.VegetationEcosystemFlowField,
                _ecosystemThreatGridCellCount,
                out flowVectors);
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemFlowFieldCenter;
            cellSize = threatGridCellSize;
            return _flowFieldInitialized &&
                   hasFlowField &&
                   HasCompleteEcosystemSquareGridState(flowVectors.Length) &&
                   cellSize > 0f &&
                   math.isfinite(cellSize) &&
                   IsFinite(gridCenter);
        }

        public bool TryUploadEcosystemFlowFieldPayload(GraphicsBuffer destination, int count)
        {
            if (destination == null ||
                count <= 0 ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemFlowFieldHandle,
                    BufferID.VegetationEcosystemFlowField,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<float2> currentFlowField) ||
                !HasCompleteEcosystemSquareGridState(currentFlowField.Length))
            {
                return false;
            }

            int uploadCount = math.min(count, currentFlowField.Length);
            if (uploadCount <= 0)
                return false;

            GraphicsBufferUploadUtility.UploadNativeArray(destination, currentFlowField, uploadCount);
            return true;
        }

        /// <summary>
        /// Registers one short-lived wake impulse that will be folded into the next abyssal flow-field solve.
        /// </summary>
        public void RegisterSwarmWakeImpulse(Vector3 positionWS, Vector3 flowVectorWS, float radiusMeters, float lifetimeSeconds)
        {
            if (!IsFinite(positionWS) ||
                !IsFinite(flowVectorWS) ||
                radiusMeters <= 0f ||
                lifetimeSeconds <= 0f ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(lifetimeSeconds))
            {
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulse = default;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
                return;
            }

            float strength = EstimateLength3D(flowVectorWS);
            if (strength <= 0.0001f)
            {
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulse = default;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
                return;
            }

            _swarmWakeImpulse = new SwarmWakeImpulse
            {
                Position = new float3(positionWS.x, positionWS.y, positionWS.z),
                Radius = math.max(0.1f, radiusMeters),
                FlowVector = new float3(flowVectorWS.x, flowVectorWS.y, flowVectorWS.z),
                Strength = strength
            };
            _swarmWakeImpulseCount = 1;
            _swarmWakeImpulseExpireTime = ResolveVegetationRuntimeSeconds() + math.max(0.1f, lifetimeSeconds);
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

        private void AdvanceVegetationRuntimeClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _vegetationRuntimeSeconds = math.min(VegetationRuntimeClockMaxSeconds, _vegetationRuntimeSeconds + deltaTime);
        }

        private float ResolveVegetationRuntimeSeconds()
        {
            return _vegetationRuntimeSeconds;
        }

        /// <summary>
        /// Returns the current abyssal thermal-grid payload and metadata for survival and environment consumers.
        /// </summary>
        public bool TryGetAbyssalThermalGridPayload(
            out NativeArray<float>.ReadOnly temperatures,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            bool hasThermalGrid = TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.AbyssalThermalGridHandle,
                BufferID.VegetationAbyssalThermalGrid,
                _abyssalThermalGridCellCount,
                out temperatures);
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalThermalGridInitialized &&
                   hasThermalGrid &&
                   HasCompleteAbyssalGridState(temperatures.Length);
        }

        /// <summary>
        /// Returns the current 3D abyssal flow-volume payload and metadata for current-driven deep-ocean consumers.
        /// </summary>
        public bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3>.ReadOnly flowVectors,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            bool hasFlowVolume = TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.AbyssalFlowVolumeHandle,
                BufferID.VegetationAbyssalFlowVolume,
                _abyssalThermalGridCellCount,
                out flowVectors);
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalFlowVolumeInitialized &&
                   hasFlowVolume &&
                   HasCompleteAbyssalGridState(flowVectors.Length);
        }

        /// <summary>
        /// Returns the current read-only abyssal flow volume and ring-buffer metadata for Burst consumers.
        /// </summary>
        public bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3>.ReadOnly flowVolume,
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
            bool hasFlowVolume = TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.AbyssalFlowVolumeHandle,
                BufferID.VegetationAbyssalFlowVolume,
                _abyssalThermalGridCellCount,
                out flowVolume);
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
                   hasFlowVolume &&
                   HasCompleteAbyssalGridState(flowVolume.Length) &&
                   resolutionXZ > 1 &&
                   resolutionY > 1 &&
                   depthMeters > 0f &&
                   math.isfinite(surfaceY) &&
                   math.isfinite(depthMeters);
        }

        /// <summary>
        /// Returns the current mega-wreck section streaming payload for composite-structure consumers.
        /// </summary>

        private bool TryPrepareFlowFieldSamplingSnapshot(
            ref IDataVault readPinVault,
            ref uint readPinMask,
            out NativeArray<VegetationDensityChunkRecord> flowChunks,
            out NativeArray<float3> flowDensityGrid,
            out NativeArray<float2> threatAttractorGrid)
        {
            flowChunks = default;
            flowDensityGrid = default;
            threatAttractorGrid = default;

            if (!EnsureThreatGridBuffers())
                return false;
            if (!TryPrepareThreatSamplingJobSnapshot(
                    true,
                    FlowFieldPinDensityChunks,
                    FlowFieldPinDensityGrid,
                    FlowFieldPinThreatAttractorGrid,
                    ref readPinVault,
                    ref readPinMask,
                    out flowChunks,
                    out threatAttractorGrid,
                    out flowDensityGrid))
                return false;
            return true;
        }

        private bool TryPrepareThreatSamplingJobSnapshot(
            bool includeDensityGrid,
            uint chunksPinBit,
            uint densityGridPinBit,
            uint threatAttractorGridPinBit,
            ref IDataVault readPinVault,
            ref uint readPinMask,
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float2> threatAttractorGrid,
            out NativeArray<float3> densityGrid)
        {
            chunks = default;
            threatAttractorGrid = default;
            densityGrid = default;

            int chunkCount = _threatSamplingChunkCount;
            if (chunkCount <= 0)
                return true;

            long gridLengthLong = (long)chunkCount * DensityGridCellCount;
            if (gridLengthLong <= 0L ||
                gridLengthLong > int.MaxValue ||
                !TryPinDensityQueryJobSnapshot(
                    includeDensityGrid,
                    true,
                    chunksPinBit,
                    densityGridPinBit,
                    threatAttractorGridPinBit,
                    ref readPinVault,
                    ref readPinMask,
                    out chunks,
                    out densityGrid,
                    out threatAttractorGrid,
                    out int densityChunkCount,
                    out int densityGridLength) ||
                densityChunkCount < chunkCount ||
                densityGridLength < gridLengthLong ||
                !chunks.IsCreated ||
                chunks.Length < chunkCount ||
                !threatAttractorGrid.IsCreated ||
                threatAttractorGrid.Length < gridLengthLong ||
                (includeDensityGrid && (!densityGrid.IsCreated || densityGrid.Length < gridLengthLong)))
            {
                RecordVegetationMemoryTelemetry(
                    BufferID.Unknown,
                    0,
                    chunkCount,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return false;
            }

            return true;
        }

        private void CompleteThreatPropagationJob(bool forceComplete)
        {
            if (!_threatPropagationScheduled)
                return;

            if (!forceComplete && !_threatPropagationJob.Handle.IsCompleted)
                return;

            ThreatPropagationPendingJob pending = _threatPropagationJob;
            if (!TryCompleteVegetationSimulationJob(ref pending.Handle, forceComplete))
                return;

            try
            {
                if (pending.Cancelled)
                    return;

                bool permanentEchoChanged = InvalidateChunksForNewPermanentEchoes(pending.Staging);
                bool hasCommitSnapshot = StageThreatPropagationCommitSnapshot(
                    pending.Staging,
                    _ecosystemThreatGridCellCount,
                    _ecosystemThreatVoxelCellCount);
                ReleaseThreatPropagationStagingWriteLock(pending.StagingVault);
                pending.StagingVault = null;
                pending.Staging = default;
                bool published = hasCommitSnapshot &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.EcosystemThreatGridHandle,
                        BufferID.VegetationEcosystemThreatGrid,
                        _threatPropagationCommitThreat,
                        _ecosystemThreatGridCellCount) &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.EcosystemThreatGridCompressedHandle,
                        BufferID.VegetationEcosystemThreatGridCompressed,
                        _threatPropagationCommitCompressed,
                        _ecosystemThreatGridCellCount) &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.EcosystemThreatEchoHandle,
                        BufferID.VegetationEcosystemThreatEcho,
                        _threatPropagationCommitEcho,
                        _ecosystemThreatGridCellCount) &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.EcosystemThreatVoxelHandle,
                        BufferID.VegetationEcosystemThreatVoxel,
                        _threatPropagationCommitVoxel,
                        _ecosystemThreatVoxelCellCount);
                if (!published)
                    return;

                _ecosystemThreatGridCenter = pending.TargetCenter;
                _ecosystemThreatVoxelOrigin = pending.VoxelOrigin;
                _lastThreatPropagationTime = ResolveVegetationRuntimeSeconds();
                _threatGridInitialized = true;
                if (permanentEchoChanged)
                    RefreshResidency();
                UpdateThreatHotspot();
            }
            finally
            {
                ReleaseThreatPropagationPendingJob(ref pending);
                _threatPropagationJob = default;
                _threatPropagationScheduled = false;
            }
        }

        private void CompleteFlowFieldJob(bool forceComplete)
        {
            if (!_flowFieldScheduled)
                return;

            if (!forceComplete && !_flowFieldJob.Handle.IsCompleted)
                return;

            FlowFieldPendingJob pending = _flowFieldJob;
            if (!TryCompleteVegetationSimulationJob(ref pending.Handle, forceComplete))
                return;

            try
            {
                if (pending.Cancelled)
                    return;

                ReleaseFlowFieldReadPins(pending.ReadPinVault, pending.ReadPinMask);
                pending.ReadPinVault = null;
                pending.ReadPinMask = 0u;
                bool hasCommitSnapshot = StageFlowFieldCommitSnapshot(
                    pending.Staging,
                    _ecosystemThreatGridCellCount);
                ReleaseFlowFieldStagingWriteLock(pending.StagingVault);
                pending.StagingVault = null;
                pending.Staging = default;
                if (!hasCommitSnapshot ||
                    !TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.EcosystemFlowFieldHandle,
                        BufferID.VegetationEcosystemFlowField,
                        _flowFieldCommitFlow,
                        _ecosystemThreatGridCellCount))
                {
                    return;
                }

                _ecosystemFlowFieldCenter = pending.FlowCenter;
                _lastFlowFieldSolveTime = pending.RuntimeTime;
                _flowFieldInitialized = true;
            }
            finally
            {
                ReleaseFlowFieldPendingJob(ref pending);
                _flowFieldJob = default;
                _flowFieldScheduled = false;
            }
        }

        private void CompleteThermalGridJob(bool forceComplete)
        {
            if (!_abyssalThermalGridScheduled)
                return;

            if (!forceComplete && !_thermalGridJob.Handle.IsCompleted)
                return;

            ThermalGridPendingJob pending = _thermalGridJob;
            if (!TryCompleteVegetationSimulationJob(ref pending.Handle, forceComplete))
                return;

            try
            {
                if (pending.Cancelled)
                    return;

                ReleaseThermalGridReadPins(pending.ReadPinVault, pending.ReadPinMask);
                pending.ReadPinVault = null;
                pending.ReadPinMask = 0u;
                bool biolumeSurge = pending.CanComparePreviousFlowVolume &&
                    DetectBiolumeSurgeCluster3D(
                        pending.Staging,
                        _abyssalThermalGridCellCount,
                        _abyssalThermalGridResolutionXZ,
                        _abyssalThermalGridResolutionY,
                        BiolumeSurgeVelocityDeltaThreshold);
                bool hasCommitSnapshot = StageThermalGridCommitSnapshot(
                    pending.Staging,
                    _abyssalThermalGridCellCount);
                ReleaseThermalGridStagingWriteLock(pending.StagingVault);
                pending.StagingVault = null;
                pending.Staging = default;
                bool published = hasCommitSnapshot &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.AbyssalThermalGridHandle,
                        BufferID.VegetationAbyssalThermalGrid,
                        _thermalGridCommitThermal,
                        _abyssalThermalGridCellCount) &&
                    TryCopyVegetationMemorySnapshot(
                        ref _nativeMemory.AbyssalFlowVolumeHandle,
                        BufferID.VegetationAbyssalFlowVolume,
                        _thermalGridCommitFlowVolume,
                        _abyssalThermalGridCellCount);
                if (!published)
                    return;

                _abyssalThermalGridCenter = pending.ThermalCenter;
                _scheduledAbyssalThermalGridCenter = pending.ThermalCenter;
                _lastThermalGridSolveTime = pending.RuntimeTime;
                _abyssalThermalGridInitialized = true;
                _abyssalFlowVolumeInitialized = true;
                if (biolumeSurge)
                    TryRegisterBiolumeSurge(BiolumeSurgeDurationSeconds);
            }
            finally
            {
                ReleaseThermalGridPendingJob(ref pending);
                _thermalGridJob = default;
                _abyssalThermalGridScheduled = false;
            }
        }

        private static bool TryCompleteVegetationSimulationJob(ref JobHandle handle, bool forceComplete)
        {
            if (!forceComplete)
                return DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private void ReleaseThreatPropagationPendingJob(ref ThreatPropagationPendingJob pending)
        {
            ReleaseThreatPropagationStagingWriteLock(pending.StagingVault);
            ReleaseThreatPropagationReadPins(pending.ReadPinVault, pending.ReadPinMask);
            pending.Staging = default;
            pending.StagingVault = null;
            pending.ReadPinVault = null;
            pending.ReadPinMask = 0u;
            pending.Handle = default;
        }

        private void ReleaseFlowFieldPendingJob(ref FlowFieldPendingJob pending)
        {
            ReleaseFlowFieldStagingWriteLock(pending.StagingVault);
            ReleaseFlowFieldReadPins(pending.ReadPinVault, pending.ReadPinMask);
            pending.Staging = default;
            pending.StagingVault = null;
            pending.ReadPinVault = null;
            pending.ReadPinMask = 0u;
            pending.Handle = default;
        }

        private void ReleaseThermalGridPendingJob(ref ThermalGridPendingJob pending)
        {
            ReleaseThermalGridStagingWriteLock(pending.StagingVault);
            ReleaseThermalGridReadPins(pending.ReadPinVault, pending.ReadPinMask);
            pending.Staging = default;
            pending.StagingVault = null;
            pending.ReadPinVault = null;
            pending.ReadPinMask = 0u;
            pending.Handle = default;
        }

        private void CancelVegetationSimulationJobsForResidencyClear()
        {
            if (_threatPropagationScheduled)
            {
                ThreatPropagationPendingJob pending = _threatPropagationJob;
                pending.Cancelled = true;
                _threatPropagationJob = pending;
            }

            if (_flowFieldScheduled)
            {
                FlowFieldPendingJob pending = _flowFieldJob;
                pending.Cancelled = true;
                _flowFieldJob = pending;
            }

            if (_abyssalThermalGridScheduled)
            {
                ThermalGridPendingJob pending = _thermalGridJob;
                pending.Cancelled = true;
                _thermalGridJob = pending;
            }
        }

        private bool TryCopyVegetationMemorySnapshot<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> source,
            int count)
            where T : struct
        {
            if (!source.IsCreated ||
                count < 0 ||
                source.Length < count)
            {
                return false;
            }

            if (!TryAcquireVegetationMemoryBuffer(
                    ref handle,
                    bufferId,
                    math.max(1, count),
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<T> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < count)
                    return false;

                if (count > 0)
                    NativeArray<T>.Copy(source, destination, count);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in handle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool TryCopyVegetationMemorySnapshot<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            T[] source,
            int count)
            where T : struct
        {
            if (source == null ||
                count < 0 ||
                source.Length < count)
            {
                return false;
            }

            if (!TryAcquireVegetationMemoryBuffer(
                    ref handle,
                    bufferId,
                    math.max(1, count),
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<T> destination))
            {
                return false;
            }

            try
            {
                if (!destination.IsCreated || destination.Length < count)
                    return false;

                for (int i = 0; i < count; i++)
                    destination[i] = source[i];
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in handle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool EnsureThreatPropagationStagingHandles(int gridCount, int voxelCount)
        {
            int capacity = math.max(math.max(1, gridCount), math.max(1, voxelCount));
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            int stagingPointSize = UnsafeUtility.SizeOf<ThreatPropagationStagingPoint>();
            if (stagingPointSize != 16 || (stagingPointSize & 7) != 0)
                return false;

            _nativeMemory.ThreatPropagationStagingHandle =
                vault.EnsureGenerationHandle<ThreatPropagationStagingPoint>(
                    BufferID.VegetationThreatPropagationStagingPacked,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                in _nativeMemory.ThreatPropagationStagingHandle,
                BufferID.VegetationThreatPropagationStagingPacked);
        }

        private bool TryAcquireThreatPropagationStagingBuffer(
            int gridCount,
            int voxelCount,
            out IDataVault vault,
            out NativeArray<ThreatPropagationStagingPoint> staging)
        {
            vault = null;
            staging = default;
            if (!EnsureThreatPropagationStagingHandles(gridCount, voxelCount))
                return false;

            int capacity = math.max(math.max(1, gridCount), math.max(1, voxelCount));
            return TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.ThreatPropagationStagingHandle,
                BufferID.VegetationThreatPropagationStagingPacked,
                capacity,
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out staging);
        }

        private void ReleaseThreatPropagationStagingWriteLock(IDataVault vault)
        {
            if (vault == null ||
                !IsExactVegetationMemoryHandle(
                    in _nativeMemory.ThreatPropagationStagingHandle,
                    BufferID.VegetationThreatPropagationStagingPacked))
            {
                return;
            }

            vault.ReleaseWriteLock(
                in _nativeMemory.ThreatPropagationStagingHandle,
                VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private bool TryPinThreatPropagationCurrentInputs(
            out IDataVault vault,
            out bool threatGridPinned,
            out bool threatEchoPinned)
        {
            vault = _vegetationMemoryVault;
            threatGridPinned = false;
            threatEchoPinned = false;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryLockBuffer(
                    BufferID.VegetationEcosystemThreatGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId))
            {
                return false;
            }

            threatGridPinned = true;
            if (!vault.TryLockBuffer(
                    BufferID.VegetationEcosystemThreatEcho,
                    VegetationMemorySovereigntyConstants.OwnerSystemId))
            {
                ReleaseThreatPropagationCurrentInputPins(vault, threatGridPinned, threatEchoPinned);
                threatGridPinned = false;
                return false;
            }

            threatEchoPinned = true;
            return true;
        }

        private static void ReleaseThreatPropagationCurrentInputPins(
            IDataVault vault,
            bool threatGridPinned,
            bool threatEchoPinned)
        {
            if (vault == null)
                return;

            if (threatEchoPinned)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationEcosystemThreatEcho,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if (threatGridPinned)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationEcosystemThreatGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool TryPinThreatPropagationReadBuffer(
            BufferID bufferId,
            uint pinBit,
            ref IDataVault readPinVault,
            ref uint readPinMask)
        {
            return TryPinVegetationReadBuffer(bufferId, pinBit, ref readPinVault, ref readPinMask);
        }

        private static void ReleaseThreatPropagationReadPins(IDataVault vault, uint pinMask)
        {
            if (vault == null || pinMask == 0u)
                return;

            if ((pinMask & ThreatPropagationPinArtificialStructures) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationArtificialStructureRecords,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & ThreatPropagationPinDensityChunks) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationDensityQueryChunks,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & ThreatPropagationPinDensityGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationDensityQueryGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & ThreatPropagationPinThreatAttractorGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationThreatAttractorGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool TryStageThreatPropagationCurrentInputs(
            NativeArray<ThreatPropagationStagingPoint> staging,
            int gridCount,
            int voxelCount)
        {
            if (!TryPinThreatPropagationCurrentInputs(
                    out IDataVault threatCurrentInputVault,
                    out bool threatGridPinned,
                    out bool threatEchoPinned))
            {
                return false;
            }

            try
            {
                return TryReadVegetationMemoryBuffer(
                           in _nativeMemory.EcosystemThreatGridHandle,
                           BufferID.VegetationEcosystemThreatGrid,
                           gridCount,
                           out NativeArray<float> currentThreat) &&
                       TryReadVegetationMemoryBuffer(
                           in _nativeMemory.EcosystemThreatEchoHandle,
                           BufferID.VegetationEcosystemThreatEcho,
                           gridCount,
                           out NativeArray<byte> currentEchoFlags) &&
                       StageThreatPropagationPreviousState(
                           currentThreat,
                           currentEchoFlags,
                           gridCount,
                           voxelCount,
                           staging);
            }
            finally
            {
                ReleaseThreatPropagationCurrentInputPins(threatCurrentInputVault, threatGridPinned, threatEchoPinned);
            }
        }

        private static bool StageThreatPropagationPreviousState(
            NativeArray<float> currentThreat,
            NativeArray<byte> currentEchoFlags,
            int gridCount,
            int voxelCount,
            NativeArray<ThreatPropagationStagingPoint> staging)
        {
            int capacity = math.max(gridCount, voxelCount);
            if (!currentThreat.IsCreated ||
                !currentEchoFlags.IsCreated ||
                !staging.IsCreated ||
                gridCount < 0 ||
                voxelCount < 0 ||
                currentThreat.Length < gridCount ||
                currentEchoFlags.Length < gridCount ||
                staging.Length < capacity)
            {
                return false;
            }

            for (int i = 0; i < gridCount; i++)
            {
                ThreatPropagationStagingPoint point = staging[i];
                point.PreviousThreat = currentThreat[i];
                point.NextThreat = 0f;
                point.PreviousEcho = currentEchoFlags[i];
                point.NextCompressed = 0;
                point.NextEcho = 0;
                point.Voxel = 0;
                point.Padding = 0u;
                staging[i] = point;
            }

            for (int i = gridCount; i < voxelCount; i++)
            {
                ThreatPropagationStagingPoint point = staging[i];
                point.Voxel = 0;
                point.Padding = 0u;
                staging[i] = point;
            }

            return true;
        }

        private bool StageThreatPropagationCommitSnapshot(
            NativeArray<ThreatPropagationStagingPoint> staging,
            int gridCount,
            int voxelCount)
        {
            int capacity = math.max(gridCount, voxelCount);
            if (!staging.IsCreated ||
                gridCount < 0 ||
                voxelCount < 0 ||
                staging.Length < capacity)
            {
                return false;
            }

            if (_threatPropagationCommitThreat.Length < gridCount ||
                _threatPropagationCommitCompressed.Length < gridCount ||
                _threatPropagationCommitEcho.Length < gridCount ||
                _threatPropagationCommitVoxel.Length < voxelCount)
            {
                return false;
            }

            for (int i = 0; i < gridCount; i++)
            {
                ThreatPropagationStagingPoint point = staging[i];
                _threatPropagationCommitThreat[i] = point.NextThreat;
                _threatPropagationCommitCompressed[i] = point.NextCompressed;
                _threatPropagationCommitEcho[i] = point.NextEcho;
            }

            for (int i = 0; i < voxelCount; i++)
                _threatPropagationCommitVoxel[i] = staging[i].Voxel;

            return true;
        }

        private bool EnsureFlowFieldStagingHandles(int gridCount)
        {
            int capacity = math.max(1, gridCount * 2);
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            int stagingPointSize = UnsafeUtility.SizeOf<FlowFieldStagingPoint>();
            if (stagingPointSize != 16 || (stagingPointSize & 7) != 0)
                return false;

            _nativeMemory.FlowFieldStagingHandle =
                vault.EnsureGenerationHandle<FlowFieldStagingPoint>(
                    BufferID.VegetationFlowFieldStagingPacked,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                in _nativeMemory.FlowFieldStagingHandle,
                BufferID.VegetationFlowFieldStagingPacked);
        }

        private bool TryAcquireFlowFieldStagingBuffer(
            int gridCount,
            out IDataVault vault,
            out NativeArray<FlowFieldStagingPoint> staging)
        {
            vault = null;
            staging = default;
            if (!EnsureFlowFieldStagingHandles(gridCount))
                return false;

            return TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.FlowFieldStagingHandle,
                BufferID.VegetationFlowFieldStagingPacked,
                math.max(1, gridCount * 2),
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out staging);
        }

        private void ReleaseFlowFieldStagingWriteLock(IDataVault vault)
        {
            if (vault == null ||
                !IsExactVegetationMemoryHandle(
                    in _nativeMemory.FlowFieldStagingHandle,
                    BufferID.VegetationFlowFieldStagingPacked))
            {
                return;
            }

            vault.ReleaseWriteLock(
                in _nativeMemory.FlowFieldStagingHandle,
                VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private bool TryPinFlowFieldReadBuffer(BufferID bufferId, uint pin, ref IDataVault vault, ref uint pinMask)
        {
            return TryPinVegetationReadBuffer(bufferId, pin, ref vault, ref pinMask);
        }

        private static void ReleaseFlowFieldReadPins(IDataVault vault, uint pinMask)
        {
            if (vault == null)
                return;

            if ((pinMask & FlowFieldPinNavNodes) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & FlowFieldPinThreatGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationEcosystemThreatGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & FlowFieldPinDensityChunks) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationDensityQueryChunks,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & FlowFieldPinDensityGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationDensityQueryGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & FlowFieldPinThreatAttractorGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationThreatAttractorGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool StageFlowFieldCommitSnapshot(
            NativeArray<FlowFieldStagingPoint> staging,
            int gridCount)
        {
            if (!staging.IsCreated ||
                gridCount < 0 ||
                staging.Length < gridCount * 2 ||
                _flowFieldCommitFlow.Length < gridCount)
            {
                return false;
            }

            int outputOffset = gridCount;
            for (int i = 0; i < gridCount; i++)
                _flowFieldCommitFlow[i] = staging[outputOffset + i].Flow;

            return true;
        }

        private bool EnsureThermalGridStagingHandles(int cellCount)
        {
            int capacity = math.max(1, cellCount * 2);
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            int stagingPointSize = UnsafeUtility.SizeOf<ThermalGridStagingPoint>();
            if (stagingPointSize != 32 || (stagingPointSize & 7) != 0)
                return false;

            _nativeMemory.ThermalGridStagingHandle =
                vault.EnsureGenerationHandle<ThermalGridStagingPoint>(
                    BufferID.VegetationThermalGridStagingPacked,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                in _nativeMemory.ThermalGridStagingHandle,
                BufferID.VegetationThermalGridStagingPacked);
        }

        private bool TryAcquireThermalGridStagingBuffer(
            int cellCount,
            out IDataVault vault,
            out NativeArray<ThermalGridStagingPoint> staging)
        {
            vault = null;
            staging = default;
            if (!EnsureThermalGridStagingHandles(cellCount))
                return false;

            return TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.ThermalGridStagingHandle,
                BufferID.VegetationThermalGridStagingPacked,
                math.max(1, cellCount * 2),
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out staging);
        }

        private void ReleaseThermalGridStagingWriteLock(IDataVault vault)
        {
            if (vault == null ||
                !IsExactVegetationMemoryHandle(
                    in _nativeMemory.ThermalGridStagingHandle,
                    BufferID.VegetationThermalGridStagingPacked))
            {
                return;
            }

            vault.ReleaseWriteLock(
                in _nativeMemory.ThermalGridStagingHandle,
                VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private bool TryPinThermalGridReadBuffer(BufferID bufferId, uint pin, ref IDataVault vault, ref uint pinMask)
        {
            return TryPinVegetationReadBuffer(bufferId, pin, ref vault, ref pinMask);
        }

        private static void ReleaseThermalGridReadPins(IDataVault vault, uint pinMask)
        {
            if (vault == null)
                return;

            if ((pinMask & ThermalGridPinPreviousFlowVolume) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationAbyssalFlowVolume,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & ThermalGridPinDensityChunks) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationDensityQueryChunks,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((pinMask & ThermalGridPinThreatAttractorGrid) != 0u)
            {
                vault.TryUnlockBuffer(
                    BufferID.VegetationThreatAttractorGrid,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool StageThermalGridCommitSnapshot(
            NativeArray<ThermalGridStagingPoint> staging,
            int cellCount)
        {
            if (!staging.IsCreated ||
                cellCount < 0 ||
                staging.Length < cellCount * 2 ||
                _thermalGridCommitThermal.Length < cellCount ||
                _thermalGridCommitFlowVolume.Length < cellCount)
            {
                return false;
            }

            int outputOffset = cellCount;
            for (int i = 0; i < cellCount; i++)
            {
                _thermalGridCommitThermal[i] = staging[i].Thermal;
                _thermalGridCommitFlowVolume[i] = staging[outputOffset + i].Flow;
            }

            return true;
        }

        private static bool DetectBiolumeSurgeCluster3D(
            NativeArray<ThermalGridStagingPoint> staging,
            int cellCount,
            int horizontalResolution,
            int verticalResolution,
            float velocityDeltaThreshold)
        {
            if (!staging.IsCreated ||
                cellCount <= 0 ||
                staging.Length < cellCount * 2 ||
                horizontalResolution <= 2 ||
                verticalResolution <= 2)
            {
                return false;
            }

            int cellsPerLayer = horizontalResolution * horizontalResolution;
            int requiredLength = cellsPerLayer * verticalResolution;
            if (requiredLength <= 0 || requiredLength > cellCount)
                return false;

            int outputOffset = cellCount;
            float velocityDeltaThresholdSq = velocityDeltaThreshold * velocityDeltaThreshold;
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
                                    previousMaxSpeedSq = math.max(previousMaxSpeedSq, math.lengthsq(staging[sampleIndex].PreviousFlow));
                                    currentMaxSpeedSq = math.max(currentMaxSpeedSq, math.lengthsq(staging[outputOffset + sampleIndex].Flow));
                                }
                            }
                        }

                        if (math.abs(currentMaxSpeedSq - previousMaxSpeedSq) > velocityDeltaThresholdSq)
                            return true;
                    }
                }
            }

            return false;
        }

        private void ScheduleThreatPropagationJob()
        {
            if (_threatPropagationScheduled || _flowFieldScheduled || _abyssalThermalGridScheduled)
                return;

            if (!EnsureThreatGridBuffers())
                return;
            if (!HasValidThreatGridConfiguration())
                return;

            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            Vector3 targetCenter = hasPlayerRuntimePosition
                ? playerRuntimePosition
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);
            Vector3 previousCenter = _threatGridInitialized ? _ecosystemThreatGridCenter : targetCenter;
            ResolveThreatSignalSnapshot(out Vector3 emissionPosition, out float emissionRadius, out float emissionStrength);
            IDataVault threatReadPinVault = null;
            uint threatReadPinMask = 0u;
            if (!TryPrepareThreatSamplingJobSnapshot(
                    true,
                    ThreatPropagationPinDensityChunks,
                    ThreatPropagationPinDensityGrid,
                    ThreatPropagationPinThreatAttractorGrid,
                    ref threatReadPinVault,
                    ref threatReadPinMask,
                    out NativeArray<VegetationDensityChunkRecord> threatChunks,
                    out NativeArray<float2> threatAttractorGrid,
                    out NativeArray<float3> densityGrid))
            {
                ReleaseThreatPropagationReadPins(threatReadPinVault, threatReadPinMask);
                return;
            }

            float currentTime = ResolveVegetationRuntimeSeconds();
            float deltaTime = 0.5f;
            if (_lastThreatPropagationTime > float.NegativeInfinity)
                deltaTime = math.clamp(currentTime - _lastThreatPropagationTime, 0.05f, 5f);

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int shiftX = (int)math.round((targetCenter.x - previousCenter.x) * inverseThreatGridCellSize);
            int shiftZ = (int)math.round((targetCenter.z - previousCenter.z) * inverseThreatGridCellSize);
            float halfExtent = (_ecosystemThreatGridResolution - 1) * 0.5f * threatGridCellSize;
            Vector3 voxelOrigin = new Vector3(
                targetCenter.x - halfExtent,
                waterLevel - thermalGridDepthMeters,
                targetCenter.z - halfExtent);

            NativeArray<ArtificialStructureRecord> artificialStructures = default;
            int artificialStructureCountForJob = 0;
            int artificialStructureCount = math.max(0, _artificialStructureCount);
            if (artificialStructureCount > 0 &&
                (!TryPinThreatPropagationReadBuffer(
                    BufferID.VegetationArtificialStructureRecords,
                    ThreatPropagationPinArtificialStructures,
                    ref threatReadPinVault,
                    ref threatReadPinMask) ||
                 !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.ArtificialStructureRecordsHandle,
                    BufferID.VegetationArtificialStructureRecords,
                    artificialStructureCount,
                    out artificialStructures)))
            {
                ReleaseThreatPropagationReadPins(threatReadPinVault, threatReadPinMask);
                return;
            }

            if (artificialStructures.IsCreated)
                artificialStructureCountForJob = math.min(artificialStructureCount, artificialStructures.Length);

            if (!TryAcquireThreatPropagationStagingBuffer(
                    _ecosystemThreatGridCellCount,
                    _ecosystemThreatVoxelCellCount,
                    out IDataVault threatStagingVault,
                    out NativeArray<ThreatPropagationStagingPoint> threatStaging))
            {
                ReleaseThreatPropagationReadPins(threatReadPinVault, threatReadPinMask);
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationThreatPropagationStagingPacked,
                    _nativeMemory.ThreatPropagationStagingHandle.Generation,
                    _ecosystemThreatGridCellCount,
                    threatStaging.IsCreated ? threatStaging.Length : 0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return;
            }

            if (!TryStageThreatPropagationCurrentInputs(
                    threatStaging,
                    _ecosystemThreatGridCellCount,
                    _ecosystemThreatVoxelCellCount))
            {
                ReleaseThreatPropagationReadPins(threatReadPinVault, threatReadPinMask);
                ReleaseThreatPropagationStagingWriteLock(threatStagingVault);
                return;
            }

            var job = new ThreatPropagationJob
            {
                Staging = threatStaging,
                ThreatChunks = threatChunks,
                ThreatAttractorGrid = threatAttractorGrid,
                ArtificialStructures = artificialStructures,
                ArtificialStructureCount = artificialStructureCountForJob,
                ArtificialStructureHash = default,
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

            var voxelJob = new ThreatVoxelizationJob
            {
                Staging = threatStaging,
                DensityChunks = threatChunks,
                DensityGrid = densityGrid,
                ThreatAttractorGrid = threatAttractorGrid,
                ChunkHash = default,
                ArtificialStructures = artificialStructures,
                ArtificialStructureCount = artificialStructureCountForJob,
                ArtificialStructureHash = default,
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

            JobHandle threatHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);
            JobHandle voxelHandle = voxelJob.Schedule(_ecosystemThreatVoxelCellCount, DefaultJobBatchSize, threatHandle);
            _threatPropagationJob = new ThreatPropagationPendingJob
            {
                ThreatChunks = threatChunks,
                ThreatAttractorGrid = threatAttractorGrid,
                DensityGrid = densityGrid,
                ArtificialStructures = artificialStructures,
                Staging = threatStaging,
                StagingVault = threatStagingVault,
                ReadPinVault = threatReadPinVault,
                ReadPinMask = threatReadPinMask,
                TargetCenter = targetCenter,
                VoxelOrigin = voxelOrigin,
                Handle = voxelHandle
            };
            _threatPropagationScheduled = true;
        }

        private bool EnsureFlowFieldBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (_ecosystemThreatGridCellCount <= 0)
                return false;

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemFlowFieldHandle,
                    BufferID.VegetationEcosystemFlowField,
                    _ecosystemThreatGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.EcosystemFlowFieldHandle,
                        BufferID.VegetationEcosystemFlowField,
                        _ecosystemThreatGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }

                _flowFieldInitialized = false;
            }

            EnsureFlowFieldCommitCaches();
            return true;
        }

        private void EnsureFlowFieldCommitCaches()
        {
            EnsureFloat2Capacity(ref _flowFieldCommitFlow, _ecosystemThreatGridCellCount);
        }

        private bool EnsureThermalGridBuffers()
        {
            if (_abyssalThermalGridCellCount <= 0)
                InitializeThermalGridMetadata();

            if (_abyssalThermalGridCellCount <= 0)
                return false;

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalThermalGridHandle,
                    BufferID.VegetationAbyssalThermalGrid,
                    _abyssalThermalGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.AbyssalThermalGridHandle,
                        BufferID.VegetationAbyssalThermalGrid,
                        _abyssalThermalGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }

                _abyssalThermalGridInitialized = false;
                _abyssalThermalGridRingOffsetX = 0;
                _abyssalThermalGridRingOffsetY = 0;
                _abyssalThermalGridRingOffsetZ = 0;
            }

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalFlowVolumeHandle,
                    BufferID.VegetationAbyssalFlowVolume,
                    _abyssalThermalGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.AbyssalFlowVolumeHandle,
                        BufferID.VegetationAbyssalFlowVolume,
                        _abyssalThermalGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }

                _abyssalFlowVolumeInitialized = false;
            }

            EnsureThermalGridCommitCaches();
            return true;
        }

        private void EnsureThermalGridCommitCaches()
        {
            EnsureFloatCapacity(ref _thermalGridCommitThermal, _abyssalThermalGridCellCount);
            EnsureFloat3Capacity(ref _thermalGridCommitFlowVolume, _abyssalThermalGridCellCount);
        }

        private void ScheduleThreatSpatialVisualSolvePhase()
        {
            if (!_threatGridInitialized)
            {
                ScheduleThreatPropagationJob();
                _threatSpatialSolveCursor = 1;
                return;
            }

            byte phase = _threatSpatialSolveCursor;
            _threatSpatialSolveCursor = (byte)((phase + 1) % 3);
            if (phase == 0)
            {
                ScheduleThreatPropagationJob();
                return;
            }

            float currentTime = ResolveVegetationRuntimeSeconds();
            if (phase == 1)
            {
                if (CanRefreshFlowFieldSolve(currentTime))
                    ScheduleFlowFieldJob();
                return;
            }

            if (CanRefreshThermalGridSolve(currentTime))
                ScheduleThermalGridJob();
        }

        private bool CanRefreshFlowFieldSolve(float currentTime)
        {
            if (!_flowFieldInitialized)
                return true;

            float interval = math.lerp(2f, 0.35f, ResolveVegetationVisualQualityWeight());
            return currentTime - _lastFlowFieldSolveTime >= interval;
        }

        private bool CanRefreshThermalGridSolve(float currentTime)
        {
            if (!_abyssalThermalGridInitialized || !_abyssalFlowVolumeInitialized)
                return true;

            float interval = math.lerp(4f, 0.75f, ResolveVegetationVisualQualityWeight());
            return currentTime - _lastThermalGridSolveTime >= interval;
        }

        private static float ResolveVegetationVisualQualityWeight()
        {
            float rawQuality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, rawQuality, math.isfinite(rawQuality)));
        }

        private void ScheduleFlowFieldJob()
        {
            if (_flowFieldScheduled || _threatPropagationScheduled)
                return;

            if (!EnsureFlowFieldBuffers())
                return;
            if (_swarmWakeImpulseCount > 0 &&
                (!float.IsFinite(_swarmWakeImpulseExpireTime) || ResolveVegetationRuntimeSeconds() > _swarmWakeImpulseExpireTime))
            {
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulse = default;
            }

            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            Vector3 flowCenter = _threatGridInitialized
                ? _ecosystemThreatGridCenter
                : (hasPlayerRuntimePosition ? playerRuntimePosition : Vector3.zero);
            IDataVault flowReadPinVault = null;
            uint flowReadPinMask = 0u;
            if (!TryPrepareFlowFieldSamplingSnapshot(
                    ref flowReadPinVault,
                    ref flowReadPinMask,
                    out NativeArray<VegetationDensityChunkRecord> flowChunks,
                    out NativeArray<float3> flowDensityGrid,
                    out NativeArray<float2> threatAttractorGrid))
            {
                ReleaseFlowFieldReadPins(flowReadPinVault, flowReadPinMask);
                return;
            }

            if (!TryAcquireFlowFieldStagingBuffer(
                    _ecosystemThreatGridCellCount,
                    out IDataVault flowStagingVault,
                    out NativeArray<FlowFieldStagingPoint> flowStaging))
            {
                ReleaseFlowFieldReadPins(flowReadPinVault, flowReadPinMask);
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationEcosystemFlowField,
                    _nativeMemory.EcosystemFlowFieldHandle.Generation,
                    _ecosystemThreatGridCellCount,
                    flowStaging.IsCreated ? flowStaging.Length : 0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return;
            }

            bool success = false;
            NativeArray<Vector3> navNodes = default;
            int navNodeCount = 0;
            JobHandle flowStageHandle = default;
            try
            {
                if (!TryPinFlowFieldReadBuffer(
                        BufferID.VegetationEcosystemThreatGrid,
                        FlowFieldPinThreatGrid,
                        ref flowReadPinVault,
                        ref flowReadPinMask) ||
                    !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridHandle,
                    BufferID.VegetationEcosystemThreatGrid,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<float> currentThreatGrid))
                {
                    return;
                }

                var stageThreatJob = new StageFlowFieldThreatJob
                {
                    ThreatGrid = currentThreatGrid,
                    Staging = flowStaging,
                    GridCount = _ecosystemThreatGridCellCount
                };
                flowStageHandle = stageThreatJob.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);

                if (_abyssalNavNodeCount > 0 &&
                    IsExactVegetationMemoryHandle(
                        in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                        BufferID.VegetationAbyssalNavNodeSnapshot) &&
                    TryPinFlowFieldReadBuffer(
                        BufferID.VegetationAbyssalNavNodeSnapshot,
                        FlowFieldPinNavNodes,
                        ref flowReadPinVault,
                        ref flowReadPinMask))
                {
                    if (TryReadVegetationMemoryBuffer(
                            in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                            BufferID.VegetationAbyssalNavNodeSnapshot,
                            _abyssalNavNodeCount,
                            out navNodes))
                    {
                        navNodeCount = math.min(_abyssalNavNodeCount, navNodes.Length);
                    }
                    else
                    {
                        flowReadPinVault.TryUnlockBuffer(
                            BufferID.VegetationAbyssalNavNodeSnapshot,
                            VegetationMemorySovereigntyConstants.OwnerSystemId);
                        flowReadPinMask &= ~FlowFieldPinNavNodes;
                    }
                }

                if (navNodeCount > 0)
                {
                    var stageNavJob = new StageFlowFieldNavSupportJob
                    {
                        NavNodes = navNodes,
                        Staging = flowStaging,
                        NodeCount = navNodeCount,
                        GridResolution = _ecosystemThreatGridResolution,
                        StencilRadius = math.max(0, flowFieldNavStencilRadiusCells),
                        CellSize = threatGridCellSize,
                        GridCenter = new float3(flowCenter.x, flowCenter.y, flowCenter.z)
                    };
                    flowStageHandle = stageNavJob.Schedule(flowStageHandle);
                }

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
                Staging = flowStaging,
                FlowChunks = flowChunks,
                FlowDensityGrid = flowDensityGrid,
                ThreatAttractorGrid = threatAttractorGrid,
                ChunkHash = default,
                ExternalWakeImpulse = _swarmWakeImpulse,
                GridResolution = _ecosystemThreatGridResolution,
                ChunkCount = _threatSamplingChunkCount,
                ExternalWakeImpulseCount = _swarmWakeImpulseCount,
                OutputOffset = _ecosystemThreatGridCellCount,
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

            float runtimeTime = ResolveVegetationRuntimeSeconds();
            JobHandle flowHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize, flowStageHandle);
            _flowFieldJob = new FlowFieldPendingJob
            {
                FlowChunks = flowChunks,
                FlowDensityGrid = flowDensityGrid,
                ThreatAttractorGrid = threatAttractorGrid,
                Staging = flowStaging,
                StagingVault = flowStagingVault,
                ReadPinVault = flowReadPinVault,
                ReadPinMask = flowReadPinMask,
                FlowCenter = flowCenter,
                RuntimeTime = runtimeTime,
                Handle = flowHandle
            };
            _flowFieldScheduled = true;
                success = true;
            }
            finally
            {
                if (!success)
                {
                    ReleaseFlowFieldStagingWriteLock(flowStagingVault);
                    ReleaseFlowFieldReadPins(flowReadPinVault, flowReadPinMask);
                }
            }
        }

        private void ScheduleThermalGridJob()
        {
            if (_abyssalThermalGridScheduled || _threatPropagationScheduled || _flowFieldScheduled)
                return;

            if (!EnsureThermalGridBuffers())
                return;
            IDataVault thermalReadPinVault = null;
            uint thermalReadPinMask = 0u;
            if (!TryPrepareThreatSamplingJobSnapshot(
                    false,
                    ThermalGridPinDensityChunks,
                    0u,
                    ThermalGridPinThreatAttractorGrid,
                    ref thermalReadPinVault,
                    ref thermalReadPinMask,
                    out NativeArray<VegetationDensityChunkRecord> threatChunks,
                    out NativeArray<float2> threatAttractorGrid,
                    out NativeArray<float3> densityGrid))
            {
                ReleaseThermalGridReadPins(thermalReadPinVault, thermalReadPinMask);
                return;
            }

            if (!TryAcquireThermalGridStagingBuffer(
                    _abyssalThermalGridCellCount,
                    out IDataVault thermalStagingVault,
                    out NativeArray<ThermalGridStagingPoint> thermalStaging))
            {
                ReleaseThermalGridReadPins(thermalReadPinVault, thermalReadPinMask);
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationAbyssalThermalGrid,
                    _nativeMemory.AbyssalThermalGridHandle.Generation,
                    _abyssalThermalGridCellCount,
                    thermalStaging.IsCreated ? thermalStaging.Length : 0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return;
            }

            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float2 weatherDirectionXZ = DominantAxisOrDefault(weatherSnapshot.CurrentMeta.GlobalBaseVector.xz, new float2(0f, 1f));

            Vector3 thermalCenter = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition)
                ? new Vector3(playerRuntimePosition.x, waterLevel - (thermalGridDepthMeters * 0.5f), playerRuntimePosition.z)
                : (_abyssalThermalGridInitialized
                    ? _abyssalThermalGridCenter
                    : new Vector3(0f, waterLevel - (thermalGridDepthMeters * 0.5f), 0f));

            NativeArray<float3> previousFlowVolume = default;
            bool canComparePreviousFlowVolume =
                _abyssalFlowVolumeInitialized &&
                _abyssalThermalGridResolutionXZ > 2 &&
                _abyssalThermalGridResolutionY > 2 &&
                (thermalCenter - _abyssalThermalGridCenter).sqrMagnitude <=
                (thermalGridHorizontalCellSize * thermalGridHorizontalCellSize);
            if (canComparePreviousFlowVolume)
            {
                if (TryPinThermalGridReadBuffer(
                        BufferID.VegetationAbyssalFlowVolume,
                        ThermalGridPinPreviousFlowVolume,
                        ref thermalReadPinVault,
                        ref thermalReadPinMask))
                {
                    canComparePreviousFlowVolume = TryReadVegetationMemoryBuffer(
                            in _nativeMemory.AbyssalFlowVolumeHandle,
                            BufferID.VegetationAbyssalFlowVolume,
                            _abyssalThermalGridCellCount,
                            out previousFlowVolume);
                    if (!canComparePreviousFlowVolume)
                    {
                        thermalReadPinVault.TryUnlockBuffer(
                            BufferID.VegetationAbyssalFlowVolume,
                            VegetationMemorySovereigntyConstants.OwnerSystemId);
                        thermalReadPinMask &= ~ThermalGridPinPreviousFlowVolume;
                    }
                }
                else
                {
                    canComparePreviousFlowVolume = false;
                }
            }

            bool success = false;
            try
            {
                var stagePreviousJob = new StageThermalGridPreviousFlowJob
                {
                    PreviousFlowVolume = previousFlowVolume,
                    Staging = thermalStaging,
                    CellCount = _abyssalThermalGridCellCount,
                    HasPreviousFlow = canComparePreviousFlowVolume ? 1 : 0
                };
                JobHandle stageHandle = stagePreviousJob.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize);

                var job = new BuildAbyssalThermalGridJob
                {
                    Staging = thermalStaging,
                    ThreatChunks = threatChunks,
                    ThreatAttractorGrid = threatAttractorGrid,
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
                    Staging = thermalStaging,
                    ExternalWakeImpulse = _swarmWakeImpulse,
                    OutputOffset = _abyssalThermalGridCellCount,
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

                float runtimeTime = ResolveVegetationRuntimeSeconds();
                JobHandle thermalHandle = job.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize, stageHandle);
                JobHandle flowVolumeHandle = flowVolumeJob.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize, thermalHandle);
                _thermalGridJob = new ThermalGridPendingJob
                {
                    ThreatChunks = threatChunks,
                    ThreatAttractorGrid = threatAttractorGrid,
                    DensityGrid = densityGrid,
                    Staging = thermalStaging,
                    StagingVault = thermalStagingVault,
                    ReadPinVault = thermalReadPinVault,
                    ReadPinMask = thermalReadPinMask,
                    ThermalCenter = thermalCenter,
                    RuntimeTime = runtimeTime,
                    CanComparePreviousFlowVolume = canComparePreviousFlowVolume,
                    Handle = flowVolumeHandle
                };
                _abyssalThermalGridScheduled = true;
                success = true;
            }
            finally
            {
                if (!success)
                {
                    ReleaseThermalGridStagingWriteLock(thermalStagingVault);
                    ReleaseThermalGridReadPins(thermalReadPinVault, thermalReadPinMask);
                }
            }
        }

        private WeatherRuntimeSnapshot ResolveWeatherSnapshot()
        {
            IWeatherService weatherService = _weatherService;
            if (weatherService == null || !weatherService.IsInitialized)
                return default;

            return weatherService.GetRuntimeSnapshot();
        }

        private void TryRegisterBiolumeSurge(float durationSeconds)
        {
            if (_weatherService is GlobalWeatherDirector weatherDirector && weatherDirector.IsInitialized)
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
                float flashlight01 = NoiseSystem.PlayerNoiseSignal.IsFlashlightOn(in signal) ? 1f : 0f;
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
            if (_externalThreatPulseHoldTimer <= 0f ||
                _externalThreatPulseStrength <= 0f ||
                _externalThreatPulseRadius <= 0f ||
                !math.isfinite(_externalThreatPulseHoldTimer) ||
                !math.isfinite(_externalThreatPulseStrength) ||
                !math.isfinite(_externalThreatPulseRadius) ||
                !IsFinite(_externalThreatPulsePosition))
            {
                return;
            }

            emissionPosition = _externalThreatPulsePosition;
            emissionRadius = math.max(emissionRadius, _externalThreatPulseRadius);
            emissionStrength = math.max(emissionStrength, _externalThreatPulseStrength);
        }

        private void UpdateThreatHotspot()
        {
            _currentThreatHotspotLevel = 0f;
            _currentThreatHotspotPosition = IsFinite(_ecosystemThreatGridCenter) ? _ecosystemThreatGridCenter : Vector3.zero;
            if (!TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridHandle,
                    BufferID.VegetationEcosystemThreatGrid,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<float> threatGrid) ||
                _ecosystemThreatGridResolution <= 0 ||
                _ecosystemThreatGridCellCount <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(_ecosystemThreatGridCenter) ||
                !TryResolveSquareGridCellCount(
                    _ecosystemThreatGridResolution,
                    threatGrid.Length,
                    out int threatGridCellCount) ||
                _ecosystemThreatGridCellCount < threatGridCellCount)
            {
                return;
            }

            int bestIndex = -1;
            float bestThreat = 0f;
            for (int i = 0; i < threatGridCellCount; i++)
            {
                float threat = threatGrid[i];
                if (!math.isfinite(threat) || threat <= bestThreat)
                    continue;

                bestThreat = threat;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int bestX = bestIndex % _ecosystemThreatGridResolution;
            int bestZ = bestIndex / _ecosystemThreatGridResolution;
            float hotspotY = _ecosystemThreatGridCenter.y;
            if (TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) &&
                IsFinite(playerRuntimePosition))
            {
                hotspotY = playerRuntimePosition.y;
            }

            _currentThreatHotspotLevel = bestThreat;
            _currentThreatHotspotPosition = new Vector3(
                _ecosystemThreatGridCenter.x + ((bestX - halfExtent) * threatGridCellSize),
                hotspotY,
                _ecosystemThreatGridCenter.z + ((bestZ - halfExtent) * threatGridCellSize));
        }

        private NativeArray<float> GetThreatGridFloatView()
        {
            if (!_threatGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridHandle,
                    BufferID.VegetationEcosystemThreatGrid,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<float> threatGrid) ||
                !HasCompleteEcosystemSquareGridState(threatGrid.Length))
            {
                return default;
            }

            return threatGrid;
        }

        private NativeArray<byte> GetThreatGridCompressedView()
        {
            if (!_threatGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridCompressedHandle,
                    BufferID.VegetationEcosystemThreatGridCompressed,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<byte> threatGrid) ||
                !HasCompleteEcosystemSquareGridState(threatGrid.Length))
            {
                return default;
            }

            return threatGrid;
        }

        private NativeArray<byte> GetThreatGridEchoView()
        {
            if (!_threatGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatEchoHandle,
                    BufferID.VegetationEcosystemThreatEcho,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<byte> echoFlags) ||
                !HasCompleteEcosystemSquareGridState(echoFlags.Length))
            {
                return default;
            }

            return echoFlags;
        }

        private NativeArray<byte> GetThreatVoxelView()
        {
            Vector3Int gridDimensions = new Vector3Int(
                _ecosystemThreatGridResolution,
                _ecosystemThreatGridResolutionY,
                _ecosystemThreatGridResolution);
            if (!_threatGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatVoxelHandle,
                    BufferID.VegetationEcosystemThreatVoxel,
                    _ecosystemThreatVoxelCellCount,
                    out NativeArray<byte> voxels) ||
                !TryResolveVoxelGridCellCount(gridDimensions, voxels.Length, out int voxelCellCount) ||
                _ecosystemThreatVoxelCellCount < voxelCellCount)
            {
                return default;
            }

            return voxels;
        }

        private NativeArray<float2> GetFlowFieldView()
        {
            if (!_flowFieldInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemFlowFieldHandle,
                    BufferID.VegetationEcosystemFlowField,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<float2> flowField) ||
                !HasCompleteEcosystemSquareGridState(flowField.Length) ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(_ecosystemFlowFieldCenter))
            {
                return default;
            }

            return flowField;
        }

        private NativeArray<float> GetAbyssalThermalGridView()
        {
            if (!_abyssalThermalGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalThermalGridHandle,
                    BufferID.VegetationAbyssalThermalGrid,
                    _abyssalThermalGridCellCount,
                    out NativeArray<float> thermalGrid) ||
                !HasCompleteAbyssalGridState(thermalGrid.Length))
            {
                return default;
            }

            return thermalGrid;
        }

        private NativeArray<float3> GetAbyssalFlowVolumeView()
        {
            if (!_abyssalFlowVolumeInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalFlowVolumeHandle,
                    BufferID.VegetationAbyssalFlowVolume,
                    _abyssalThermalGridCellCount,
                    out NativeArray<float3> flowVolume) ||
                !HasCompleteAbyssalGridState(flowVolume.Length))
            {
                return default;
            }

            return flowVolume;
        }

        private NativeArray<byte> GetThreatGridByteView(NativeArray<byte> threatGrid)
        {
            if (!_threatGridInitialized ||
                !threatGrid.IsCreated ||
                !HasCompleteEcosystemSquareGridState(threatGrid.Length))
            {
                return default;
            }

            return threatGrid;
        }

        private NativeArray<Vector3>.ReadOnly GetAbyssalAnchorNativeView()
        {
            int safeCount = ResolveAbyssalAnchorViewCount();
            return safeCount > 0 &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalAnchorPositionsHandle,
                       BufferID.VegetationAbyssalAnchorPositions,
                       safeCount,
                       out NativeArray<Vector3>.ReadOnly anchors)
                ? anchors
                : default;
        }

        private NativeArray<AbsoluteUniversePosition>.ReadOnly GetAbyssalAnchorAupNativeView()
        {
            int safeCount = ResolveAbyssalAnchorAupViewCount();
            return safeCount > 0 &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalAnchorAupPositionsHandle,
                       BufferID.VegetationAbyssalAnchorAupPositions,
                       safeCount,
                       out NativeArray<AbsoluteUniversePosition>.ReadOnly anchors)
                ? anchors
                : default;
        }

        private int ResolveAbyssalAnchorViewCount()
        {
            if (_abyssalAnchorCount <= 0 ||
                _abyssalAnchorPositions == null ||
                _abyssalAnchorPositions.Length <= 0)
            {
                return 0;
            }

            int safeCount = math.min(_abyssalAnchorCount, _abyssalAnchorPositions.Length);
            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalAnchorPositionsHandle,
                    BufferID.VegetationAbyssalAnchorPositions,
                    safeCount,
                    out NativeArray<Vector3>.ReadOnly anchors))
            {
                return 0;
            }

            return math.max(0, math.min(safeCount, anchors.Length));
        }

        private int ResolveAbyssalAnchorAupViewCount()
        {
            int safeCount = ResolveAbyssalAnchorViewCount();
            if (safeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalAnchorAupPositionsHandle,
                    BufferID.VegetationAbyssalAnchorAupPositions,
                    safeCount,
                    out NativeArray<AbsoluteUniversePosition>.ReadOnly anchors))
            {
                return 0;
            }

            return math.max(0, math.min(safeCount, anchors.Length));
        }

        private NativeArray<Vector3>.ReadOnly GetAbyssalNavNodeSnapshotNativeView()
        {
            int safeCount = ResolveAbyssalNavNodeViewCount();
            return safeCount > 0 &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                       BufferID.VegetationAbyssalNavNodeSnapshot,
                       safeCount,
                       out NativeArray<Vector3>.ReadOnly nodes)
                ? nodes
                : default;
        }

        private int ResolveAbyssalNavNodeViewCount()
        {
            if (_abyssalNavNodeCount <= 0 ||
                _abyssalNavNodeSnapshot == null ||
                _abyssalNavNodeSnapshot.Length <= 0)
            {
                return 0;
            }

            int safeCount = math.min(_abyssalNavNodeCount, _abyssalNavNodeSnapshot.Length);
            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    safeCount,
                    out NativeArray<Vector3>.ReadOnly nodes))
            {
                return 0;
            }

            return math.max(0, math.min(safeCount, nodes.Length));
        }

        private int ResolveAbyssalNavNodeTypeViewCount()
        {
            int safeCount = ResolveAbyssalNavNodeViewCount();
            if (safeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    safeCount,
                    out NativeArray<byte>.ReadOnly nodeTypes))
            {
                return 0;
            }

            return math.max(0, math.min(safeCount, nodeTypes.Length));
        }

        private int ResolveAbyssalConduitViewCount()
        {
            int safeCount = ResolveAbyssalNavNodeViewCount();
            if (safeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitVectorsHandle,
                    BufferID.VegetationAbyssalNavConduitVectors,
                    safeCount,
                    out NativeArray<Vector3>.ReadOnly conduitVectors) ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    safeCount,
                    out NativeArray<float>.ReadOnly conduitStrengths))
            {
                return 0;
            }

            safeCount = math.min(safeCount, conduitVectors.Length);
            safeCount = math.min(safeCount, conduitStrengths.Length);
            return math.max(0, safeCount);
        }

        private int ResolveAbyssalNavGraphViewCount()
        {
            int safeCount = ResolveAbyssalConduitViewCount();
            if (safeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    safeCount,
                    out NativeArray<byte>.ReadOnly nodeTypes))
            {
                return 0;
            }

            safeCount = math.min(safeCount, nodeTypes.Length);
            return math.max(0, safeCount);
        }

        private NativeArray<Vector3>.ReadOnly GetAbyssalPathReadOnlyView()
        {
            return _abyssalPathCount > 0 &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalPathSnapshotHandle,
                       BufferID.VegetationAbyssalPathSnapshot,
                       _abyssalPathCount,
                       out NativeArray<Vector3>.ReadOnly path)
                ? path
                : default;
        }

        private int ResolveAbyssalPathViewCount()
        {
            if (_abyssalPathCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalPathSnapshotHandle,
                    BufferID.VegetationAbyssalPathSnapshot,
                    _abyssalPathCount,
                    out NativeArray<Vector3>.ReadOnly path))
            {
                return 0;
            }

            return math.max(0, math.min(_abyssalPathCount, path.Length));
        }

        private bool HasCompleteEcosystemSquareGridState(int payloadLength)
        {
            return _ecosystemThreatGridCellCount > 0 &&
                   TryResolveSquareGridCellCount(
                       _ecosystemThreatGridResolution,
                       payloadLength,
                       out int threatGridCellCount) &&
                   _ecosystemThreatGridCellCount >= threatGridCellCount;
        }

        private bool HasCompleteAbyssalGridState(int payloadLength)
        {
            return TryResolveAbyssalGridCellCount(
                       _abyssalThermalGridResolutionXZ,
                       _abyssalThermalGridResolutionY,
                       payloadLength,
                       out _) &&
                   thermalGridHorizontalCellSize > 0f &&
                   thermalGridVerticalCellSize > 0f &&
                   math.isfinite(thermalGridHorizontalCellSize) &&
                   math.isfinite(thermalGridVerticalCellSize) &&
                   IsFinite(_abyssalThermalGridCenter);
        }

        private static bool TryResolveSquareGridCellCount(int resolution, int payloadLength, out int cellCount)
        {
            cellCount = 0;
            long expectedLength = (long)resolution * resolution;
            if (resolution <= 0 ||
                expectedLength <= 0L ||
                expectedLength > int.MaxValue ||
                payloadLength < expectedLength)
            {
                return false;
            }

            cellCount = (int)expectedLength;
            return true;
        }

        private static bool TryResolveVoxelGridCellCount(Vector3Int dimensions, int payloadLength, out int cellCount)
        {
            cellCount = 0;
            if (dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return false;
            }

            long expectedLength = (long)dimensions.x * dimensions.y * dimensions.z;
            if (expectedLength <= 0L ||
                expectedLength > int.MaxValue ||
                payloadLength < expectedLength)
            {
                return false;
            }

            cellCount = (int)expectedLength;
            return true;
        }

        private static bool TryResolveAbyssalGridCellCount(int horizontalResolution, int verticalResolution, int payloadLength, out int cellCount)
        {
            cellCount = 0;
            long expectedLength = (long)horizontalResolution * horizontalResolution * verticalResolution;
            if (horizontalResolution <= 0 ||
                verticalResolution <= 0 ||
                expectedLength <= 0L ||
                expectedLength > int.MaxValue ||
                payloadLength < expectedLength)
            {
                return false;
            }

            cellCount = (int)expectedLength;
            return true;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateAnchoredVegetationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SandMask;
            [ReadOnly, NoAlias] public NativeArray<byte> RockMask;
            [ReadOnly, NoAlias] public NativeArray<ushort> HeightSamples;
            [ReadOnly, NoAlias] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly, NoAlias] public NativeArray<byte> ThreatEchoFlags;
            [ReadOnly, NoAlias] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [NoAlias] public NativeArray<JobInstanceRecord> Output;
            public int TerrainHoleCount;
            public int ArtificialStructureCount;
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

                Output[index] = default;
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
                    ArtificialStructureCount <= 0 ||
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

                if (!ArtificialStructureHash.IsCreated)
                    return IntersectsBaseModuleAabbLinear(aabbMin, aabbMax);

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

            private bool IntersectsBaseModuleAabbLinear(float3 aabbMin, float3 aabbMax)
            {
                int count = math.min(ArtificialStructureCount, ArtificialStructures.Length);
                for (int structureIndex = 0; structureIndex < count; structureIndex++)
                {
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
                    if (structureIndex < 0 ||
                        structureIndex >= ArtificialStructureCount ||
                        structureIndex >= ArtificialStructures.Length)
                    {
                        continue;
                    }

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
                    ArtificialStructureCount <= 0 ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                if (!ArtificialStructureHash.IsCreated)
                    return TryResolveMetalAttachmentSurfaceLinear(samplePosition, ref surfaceY);

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
                    if (structureIndex < 0 ||
                        structureIndex >= ArtificialStructureCount ||
                        structureIndex >= ArtificialStructures.Length)
                    {
                        continue;
                    }

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

            private bool TryResolveMetalAttachmentSurfaceLinear(float3 samplePosition, ref float surfaceY)
            {
                bool found = false;
                int count = math.min(ArtificialStructureCount, ArtificialStructures.Length);
                for (int structureIndex = 0; structureIndex < count; structureIndex++)
                {
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateFloatingVegetationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SandMask;
            [ReadOnly, NoAlias] public NativeArray<byte> RockMask;
            [ReadOnly, NoAlias] public NativeArray<ushort> HeightSamples;
            [ReadOnly, NoAlias] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [NoAlias] public NativeArray<JobInstanceRecord> Output;
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

                Output[index] = default;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SampleBiomassDensityJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> Positions;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly, NoAlias] public NativeArray<float3> DensityGrid;
            [WriteOnly, NoAlias] public NativeArray<float> Output;
            public int ChunkCount;
            public int TypeMask;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                Output[index] = SampleDensityAtPosition(Positions[index], TypeMask, Chunks, DensityGrid, ChunkCount);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct VegetationDensityQueryJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Vector3> Positions;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly, NoAlias] public NativeArray<float3> DensityGrid;
            [WriteOnly, NoAlias] public NativeArray<float> Output;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThreatPropagationJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ThreatPropagationStagingPoint> Staging;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly, NoAlias] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly, NoAlias] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            public int GridResolution;
            public int ThreatChunkCount;
            public int ArtificialStructureCount;
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
                if (!Staging.IsCreated || index < 0 || index >= Staging.Length || GridResolution <= 0)
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

                ThreatPropagationStagingPoint point = Staging[index];
                point.NextThreat = nextThreat;
                point.NextCompressed = EncodeThreat(nextThreat);
                point.NextEcho = nextEcho;
                Staging[index] = point;
            }

            private float SampleShiftedThreat(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!Staging.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0f;
                }

                return Staging[(previousZ * GridResolution) + previousX].PreviousThreat;
            }

            private byte SampleShiftedEcho(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!Staging.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0;
                }

                return Staging[(previousZ * GridResolution) + previousX].PreviousEcho;
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
                if (!ArtificialStructures.IsCreated || ArtificialStructureCount <= 0)
                    return threat;

                if (!ArtificialStructureHash.IsCreated)
                    return ApplyArtificialStructureInfluenceLinear(samplePosition, threat);

                float suppression = 0f;
                float attraction = 0f;
                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return threat;

                do
                {
                    if (structureIndex >= 0 &&
                        structureIndex < ArtificialStructureCount &&
                        structureIndex < ArtificialStructures.Length)
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

            private float ApplyArtificialStructureInfluenceLinear(float3 samplePosition, float threat)
            {
                float suppression = 0f;
                float attraction = 0f;
                int count = math.min(ArtificialStructureCount, ArtificialStructures.Length);
                for (int structureIndex = 0; structureIndex < count; structureIndex++)
                {
                    ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                    if (samplePosition.x < structure.MinX ||
                        samplePosition.x > structure.MaxX ||
                        samplePosition.y < structure.MinY ||
                        samplePosition.y > structure.MaxY ||
                        samplePosition.z < structure.MinZ ||
                        samplePosition.z > structure.MaxZ)
                    {
                        continue;
                    }

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

                float adjusted = threat * math.saturate(1f - suppression);
                if (attraction > 0f)
                    adjusted = math.saturate(adjusted + attraction);

                return adjusted;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ThreatVoxelizationJob : IJobParallelFor
        {
            private const byte SolidThreat = 255;

            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ThreatPropagationStagingPoint> Staging;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly, NoAlias] public NativeArray<float3> DensityGrid;
            [ReadOnly, NoAlias] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ChunkHash;
            [ReadOnly, NoAlias] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            public int GridResolutionXZ;
            public int GridResolutionY;
            public int ArtificialStructureCount;
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
                if (!Staging.IsCreated ||
                    index < 0 ||
                    index >= Staging.Length ||
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
                float threat = Staging.IsCreated && columnIndex >= 0 && columnIndex < Staging.Length
                    ? math.saturate(Staging[columnIndex].NextThreat)
                    : 0f;
                byte encodedThreat = EncodeOpenThreat(threat);
                float obstacle = SampleObstacle(samplePosition);
                bool isSolid = obstacle >= ObstacleHardThreshold || IsInsideBlockingStructure(columnIndex, samplePosition);
                ThreatPropagationStagingPoint point = Staging[index];
                point.Voxel = isSolid ? SolidThreat : encodedThreat;
                Staging[index] = point;
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
                if (!ArtificialStructures.IsCreated || ArtificialStructureCount <= 0 || columnIndex < 0)
                {
                    return false;
                }

                if (!ArtificialStructureHash.IsCreated)
                    return IsInsideBlockingStructureLinear(position);

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(columnIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex >= 0 &&
                        structureIndex < ArtificialStructureCount &&
                        structureIndex < ArtificialStructures.Length)
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

            private bool IsInsideBlockingStructureLinear(float3 position)
            {
                int count = math.min(ArtificialStructureCount, ArtificialStructures.Length);
                for (int structureIndex = 0; structureIndex < count; structureIndex++)
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

                return false;
            }

            private static byte EncodeOpenThreat(float threat)
            {
                return (byte)math.clamp((int)math.round(math.saturate(threat) * 254f), 0, 254);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SwarmWakeImpulse
        {
            [FieldOffset(0)]
            public float3 Position;

            [FieldOffset(12)]
            public float Radius;

            [FieldOffset(16)]
            public float3 FlowVector;

            [FieldOffset(28)]
            public float Strength;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StageFlowFieldThreatJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> ThreatGrid;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<FlowFieldStagingPoint> Staging;
            public int GridCount;

            public void Execute(int index)
            {
                if (!ThreatGrid.IsCreated ||
                    !Staging.IsCreated ||
                    index < 0 ||
                    index >= GridCount ||
                    index >= ThreatGrid.Length ||
                    index >= Staging.Length)
                {
                    return;
                }

                FlowFieldStagingPoint point = Staging[index];
                point.Threat = ThreatGrid[index];
                point.NavSupport = 0f;
                point.Flow = float2.zero;
                Staging[index] = point;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StageFlowFieldNavSupportJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<Vector3> NavNodes;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<FlowFieldStagingPoint> Staging;
            public int NodeCount;
            public int GridResolution;
            public int StencilRadius;
            public float CellSize;
            public float3 GridCenter;

            public void Execute()
            {
                if (!NavNodes.IsCreated ||
                    !Staging.IsCreated ||
                    NodeCount <= 0 ||
                    GridResolution <= 0 ||
                    CellSize <= 0f ||
                    !math.isfinite(CellSize) ||
                    !math.all(math.isfinite(GridCenter)))
                {
                    return;
                }

                int gridCount = GridResolution * GridResolution;
                if (gridCount <= 0 || Staging.Length < gridCount)
                    return;

                int halfExtent = GridResolution >> 1;
                int stencilRadius = math.max(0, StencilRadius);
                float inverseThreatGridCellSize = math.rcp(math.max(0.0001f, CellSize));
                float supportRadius = math.max(1f, stencilRadius + 0.25f);
                float inverseSupportRadiusSq = math.rcp(math.max(1f, supportRadius * supportRadius));
                int safeNodeCount = math.min(NodeCount, NavNodes.Length);
                for (int i = 0; i < safeNodeCount; i++)
                {
                    Vector3 nodeVector = NavNodes[i];
                    float3 node = new float3(nodeVector.x, nodeVector.y, nodeVector.z);
                    if (!math.all(math.isfinite(node)))
                        continue;

                    int centerX = (int)math.round((node.x - GridCenter.x) * inverseThreatGridCellSize) + halfExtent;
                    int centerZ = (int)math.round((node.z - GridCenter.z) * inverseThreatGridCellSize) + halfExtent;
                    if (centerX < 0 || centerZ < 0 || centerX >= GridResolution || centerZ >= GridResolution)
                        continue;

                    for (int offsetZ = -stencilRadius; offsetZ <= stencilRadius; offsetZ++)
                    {
                        int cellZ = centerZ + offsetZ;
                        if (cellZ < 0 || cellZ >= GridResolution)
                            continue;

                        for (int offsetX = -stencilRadius; offsetX <= stencilRadius; offsetX++)
                        {
                            int cellX = centerX + offsetX;
                            if (cellX < 0 || cellX >= GridResolution)
                                continue;

                            float distanceSq = (offsetX * offsetX) + (offsetZ * offsetZ);
                            float support01 = 1f - math.saturate(distanceSq * inverseSupportRadiusSq);
                            int index = (cellZ * GridResolution) + cellX;
                            FlowFieldStagingPoint point = Staging[index];
                            point.NavSupport = math.max(point.NavSupport, math.saturate(support01));
                            Staging[index] = point;
                        }
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalFlowFieldJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<FlowFieldStagingPoint> Staging;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> FlowChunks;
            [ReadOnly, NoAlias] public NativeArray<float3> FlowDensityGrid;
            [ReadOnly, NoAlias] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ChunkHash;
            public SwarmWakeImpulse ExternalWakeImpulse;
            public int GridResolution;
            public int ChunkCount;
            public int ExternalWakeImpulseCount;
            public int OutputOffset;
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
                if (!Staging.IsCreated ||
                    index < 0 ||
                    index >= OutputOffset ||
                    OutputOffset < 0 ||
                    OutputOffset + index >= Staging.Length ||
                    GridResolution <= 0)
                {
                    return;
                }

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
                FlowFieldStagingPoint point = Staging[OutputOffset + index];
                point.Flow = DominantAxisOrDefault(combined, float2.zero) * resolvedSpeed;
                Staging[OutputOffset + index] = point;
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
                if (!Staging.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return Staging[(cellZ * GridResolution) + cellX].Threat;
            }

            private float SampleNavSupport(int cellX, int cellZ)
            {
                if (!Staging.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return math.saturate(Staging[(cellZ * GridResolution) + cellX].NavSupport);
            }

            private float2 SampleWakeFlow(float3 position)
            {
                if (ExternalWakeImpulseCount <= 0)
                    return float2.zero;

                float2 wake = float2.zero;
                SwarmWakeImpulse impulse = ExternalWakeImpulse;
                if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                    return wake;

                float radius = math.max(impulse.Radius, 0.001f);
                float2 planarDelta = new float2(position.x - impulse.Position.x, position.z - impulse.Position.z);
                float planarDistanceSq = math.lengthsq(planarDelta);
                float radiusSq = radius * radius;
                if (planarDistanceSq > radiusSq)
                    return wake;

                float inverseRadiusSq = math.rcp(radiusSq);
                float planarGate = math.saturate(1f - (planarDistanceSq * inverseRadiusSq));
                if (planarGate <= 0f)
                    return wake;

                float verticalGate = math.saturate(1f - (math.abs(position.y - impulse.Position.y) * math.rcp(radius)));
                float weight = planarGate * planarGate * verticalGate * impulse.Strength;
                wake += DominantAxisOrDefault(new float2(impulse.FlowVector.x, impulse.FlowVector.z), float2.zero) * weight;

                return wake;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StageThermalGridPreviousFlowJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> PreviousFlowVolume;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ThermalGridStagingPoint> Staging;
            public int CellCount;
            public int HasPreviousFlow;

            public void Execute(int index)
            {
                if (!Staging.IsCreated ||
                    index < 0 ||
                    index >= CellCount ||
                    index >= Staging.Length)
                {
                    return;
                }

                ThermalGridStagingPoint point = Staging[index];
                point.PreviousFlow = HasPreviousFlow != 0 &&
                                     PreviousFlowVolume.IsCreated &&
                                     index < PreviousFlowVolume.Length
                    ? PreviousFlowVolume[index]
                    : float3.zero;
                point.Thermal = 0f;
                point.Flow = float3.zero;
                point.Padding = 0u;
                Staging[index] = point;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalThermalGridJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly, NoAlias] public NativeArray<float2> ThreatAttractorGrid;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ThermalGridStagingPoint> Staging;
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
                if (!Staging.IsCreated ||
                    index < 0 ||
                    index >= Staging.Length ||
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
                if (physicalIndex < 0 || physicalIndex >= Staging.Length)
                    return;

                ThermalGridStagingPoint point = Staging[physicalIndex];
                point.Thermal = baseTemperature + pocketHeat;
                Staging[physicalIndex] = point;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildAbyssalFlowVolumeJob : IJobParallelFor
        {
            private const float ThermoclineHalfBandMeters = 8f;
            private const float ThermoclineVerticalAttenuation = 0.1f;
            private const float SurfaceStormLayerDepthMeters = 50f;
            private const float StormSurfaceTurbulenceStrength = 0.4f;

            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ThermalGridStagingPoint> Staging;
            public SwarmWakeImpulse ExternalWakeImpulse;
            public int OutputOffset;
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
                if (!Staging.IsCreated ||
                    index < 0 ||
                    index >= OutputOffset ||
                    OutputOffset < 0 ||
                    OutputOffset + index >= Staging.Length ||
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
                if (physicalIndex < 0 || physicalIndex >= OutputOffset)
                    return;

                float localTemperature = Staging[physicalIndex].Thermal;
                float aboveTemperature = Staging[GetPhysicalIndex(cellX, math.max(0, layer - 1), cellZ)].Thermal;
                float belowTemperature = Staging[GetPhysicalIndex(cellX, math.min(VerticalResolution - 1, layer + 1), cellZ)].Thermal;

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

                ThermalGridStagingPoint outputPoint = Staging[OutputOffset + physicalIndex];
                outputPoint.Flow = flow;
                Staging[OutputOffset + physicalIndex] = outputPoint;
            }

            private float3 SampleWakeImpulse(float3 position)
            {
                if (ExternalWakeImpulseCount <= 0)
                    return float3.zero;

                float3 wake = float3.zero;
                SwarmWakeImpulse impulse = ExternalWakeImpulse;
                if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                    return wake;

                float radius = math.max(impulse.Radius, 0.001f);
                float3 delta = position - impulse.Position;
                float distanceSq = math.lengthsq(delta);
                float radiusSq = radius * radius;
                if (distanceSq > radiusSq)
                    return wake;

                float weight = math.saturate(1f - (distanceSq * math.rcp(radiusSq)));
                if (weight <= 0f)
                    return wake;

                wake += DominantAxisOrDefault(impulse.FlowVector, float3.zero) * (weight * weight * impulse.Strength);

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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct NativeAStarJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<Vector3> Nodes;
            [ReadOnly, NoAlias] public NativeArray<byte> NodeTypes;
            [ReadOnly, NoAlias] public NativeArray<Vector3> ConduitVectors;
            [ReadOnly, NoAlias] public NativeArray<float> ConduitStrengths;
            [ReadOnly, NoAlias] public NativeArray<float> ThreatGrid;
            [ReadOnly, NoAlias] public NativeArray<byte> ThreatVoxelGrid;
            [ReadOnly, NoAlias] public NativeArray<PredatorFearNodeSnapshot> PredatorFearNodes;
            [NoAlias] public NativeArray<AbyssalPathStagingPoint> PathStaging;
            public int PathCapacity;
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
                    !PathStaging.IsCreated ||
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
                    ResetPath();
                    return;
                }

                ResetPath();
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
                    ResetScratchNode(i);
                }

                WriteGScore(StartNode, 0f);
                float startHeuristic = HeuristicCost(StartNode);
                if (!math.isfinite(startHeuristic))
                    return;

                WriteFScore(StartNode, startHeuristic);
                HeapPushOrDecrease(StartNode, ref heapCount);

                int expandedNodes = 0;
                bool foundPath = StartNode == EndNode;
                while (heapCount > 0 && expandedNodes < MaxExpandedNodes)
                {
                    int current = HeapPop(ref heapCount);
                    if (current < 0)
                        break;

                    if (ReadClosedFlag(current) != 0)
                        continue;

                    WriteClosedFlag(current, 1);
                    expandedNodes++;
                    if (current == EndNode)
                    {
                        foundPath = true;
                        break;
                    }

                    float3 currentNode = ToFloat3(Nodes[current]);
                    if (!math.all(math.isfinite(currentNode)))
                        continue;
                    float currentGScore = ReadGScore(current);
                    if (!math.isfinite(currentGScore))
                        continue;

                    for (int neighbor = 0; neighbor < nodeCount; neighbor++)
                    {
                        if (neighbor == current || ReadClosedFlag(neighbor) != 0)
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
                        float tentativeG = currentGScore + traversalCost + math.max(0f, threatPenalty - conduitThreatReduction) + conduitPenalty;
                        if (tentativeG >= ReadGScore(neighbor) || !math.isfinite(tentativeG))
                            continue;

                        float neighborHeuristic = HeuristicCost(neighbor);
                        float resolvedFScore = tentativeG + neighborHeuristic;
                        if (!math.isfinite(neighborHeuristic) || !math.isfinite(resolvedFScore))
                            continue;

                        WriteParent(neighbor, current);
                        WriteGScore(neighbor, tentativeG);
                        WriteFScore(neighbor, resolvedFScore);
                        HeapPushOrDecrease(neighbor, ref heapCount);
                    }
                }

                if (!foundPath)
                    return;

                if (!TryAppendPath(new Vector3(EndPosition.x, EndPosition.y, EndPosition.z)))
                {
                    ClearPathCountPreserveFlags();
                    return;
                }

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
                        ResetPath();
                        return;
                    }

                    if (!TryAppendPath(new Vector3(node.x, node.y, node.z)))
                    {
                        ClearPathCountPreserveFlags();
                        return;
                    }

                    if (nodeIndex == StartNode)
                    {
                        reachedStartNode = true;
                        break;
                    }

                    int parentIndex = ReadParent(nodeIndex);
                    if (parentIndex < 0 || parentIndex >= nodeCount)
                    {
                        nodeIndex = -1;
                        break;
                    }

                    nodeIndex = parentIndex;
                }

                if (!reachedStartNode)
                {
                    ResetPath();
                    return;
                }

                if (!TryAppendPath(new Vector3(StartPosition.x, StartPosition.y, StartPosition.z)))
                {
                    ClearPathCountPreserveFlags();
                    return;
                }

                ReversePath();
            }

            private bool HasCompleteAStarWorkspace(int nodeCount)
            {
                int requiredPathCapacity = math.min(nodeCount, MaxPathReconstructionIterations) + 2;
                return PathStaging.IsCreated &&
                       PathStaging.Length >= nodeCount &&
                       ResolvePathCapacity() >= requiredPathCapacity;
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
                int count = GetPathCount();
                for (int i = 0; i < count >> 1; i++)
                {
                    int swapIndex = count - 1 - i;
                    AbyssalPathStagingPoint left = PathStaging[i];
                    AbyssalPathStagingPoint right = PathStaging[swapIndex];
                    Vector3 temp = left.Raw;
                    left.Raw = right.Raw;
                    right.Raw = temp;
                    PathStaging[i] = left;
                    PathStaging[swapIndex] = right;
                }
            }

            private void ResetPath()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return;

                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.RawCount = 0;
                meta.RawFlags = 0;
                PathStaging[0] = meta;
            }

            private bool TryAppendPath(Vector3 value)
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return false;
                }

                int pathCapacity = ResolvePathCapacity();
                if (pathCapacity <= 0)
                    return false;

                int count = math.max(0, PathStaging[0].RawCount);
                if (count >= pathCapacity)
                {
                    AbyssalPathStagingPoint overflowMeta = PathStaging[0];
                    overflowMeta.RawFlags |= AbyssalPathOverflowFlag;
                    PathStaging[0] = overflowMeta;
                    return false;
                }

                AbyssalPathStagingPoint entry = PathStaging[count];
                entry.Raw = value;
                PathStaging[count] = entry;
                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.RawCount = count + 1;
                PathStaging[0] = meta;
                return true;
            }

            private void ClearPathCountPreserveFlags()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return;

                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.RawCount = 0;
                PathStaging[0] = meta;
            }

            private int GetPathCount()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return 0;
                }

                return math.clamp(PathStaging[0].RawCount, 0, ResolvePathCapacity());
            }

            private int ResolvePathCapacity()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return 0;

                int requestedCapacity = PathCapacity > 0 ? PathCapacity : PathStaging.Length;
                return math.clamp(requestedCapacity, 0, PathStaging.Length);
            }

            private void ResetScratchNode(int index)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.Parent = -1;
                scratch.HeapNode = -1;
                scratch.HeapPosition = -1;
                scratch.ClosedFlag = 0;
                scratch.GScore = float.PositiveInfinity;
                scratch.FScore = float.PositiveInfinity;
                PathStaging[index] = scratch;
            }

            private int ReadParent(int index)
            {
                return PathStaging[index].Parent;
            }

            private void WriteParent(int index, int value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.Parent = value;
                PathStaging[index] = scratch;
            }

            private float ReadGScore(int index)
            {
                return PathStaging[index].GScore;
            }

            private void WriteGScore(int index, float value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.GScore = value;
                PathStaging[index] = scratch;
            }

            private float ReadFScore(int index)
            {
                return PathStaging[index].FScore;
            }

            private void WriteFScore(int index, float value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.FScore = value;
                PathStaging[index] = scratch;
            }

            private byte ReadClosedFlag(int index)
            {
                return PathStaging[index].ClosedFlag;
            }

            private void WriteClosedFlag(int index, byte value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.ClosedFlag = value;
                PathStaging[index] = scratch;
            }

            private int ReadHeapNode(int index)
            {
                return PathStaging[index].HeapNode;
            }

            private void WriteHeapNode(int index, int value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.HeapNode = value;
                PathStaging[index] = scratch;
            }

            private int ReadHeapPosition(int index)
            {
                return PathStaging[index].HeapPosition;
            }

            private void WriteHeapPosition(int index, int value)
            {
                AbyssalPathStagingPoint scratch = PathStaging[index];
                scratch.HeapPosition = value;
                PathStaging[index] = scratch;
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
                int heapIndex = ReadHeapPosition(nodeIndex);
                if (heapIndex >= 0)
                {
                    SiftUp(heapIndex);
                    return;
                }

                if (!PathStaging.IsCreated || heapCount >= PathStaging.Length)
                    return;

                WriteHeapNode(heapCount, nodeIndex);
                WriteHeapPosition(nodeIndex, heapCount);
                SiftUp(heapCount);
                heapCount++;
            }

            private int HeapPop(ref int heapCount)
            {
                if (heapCount <= 0)
                    return -1;

                int root = ReadHeapNode(0);
                heapCount--;
                int lastNode = ReadHeapNode(heapCount);
                WriteHeapNode(heapCount, -1);
                WriteHeapPosition(root, -1);
                if (heapCount > 0)
                {
                    WriteHeapNode(0, lastNode);
                    WriteHeapPosition(lastNode, 0);
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
                    int node = ReadHeapNode(index);
                    int parentNode = ReadHeapNode(parentIndex);
                    if (ReadFScore(node) >= ReadFScore(parentNode))
                        break;

                    WriteHeapNode(index, parentNode);
                    WriteHeapNode(parentIndex, node);
                    WriteHeapPosition(node, parentIndex);
                    WriteHeapPosition(parentNode, index);
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
                    if (right < heapCount && ReadFScore(ReadHeapNode(right)) < ReadFScore(ReadHeapNode(left)))
                        smallest = right;

                    if (ReadFScore(ReadHeapNode(index)) <= ReadFScore(ReadHeapNode(smallest)))
                        break;

                    int node = ReadHeapNode(index);
                    int smallestNode = ReadHeapNode(smallest);
                    WriteHeapNode(index, smallestNode);
                    WriteHeapNode(smallest, node);
                    WriteHeapPosition(node, smallest);
                    WriteHeapPosition(smallestNode, index);
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct NavPortal
        {
            [FieldOffset(0)] public float3 Left;
            [FieldOffset(12)] public float3 Right;
            [FieldOffset(24)] public float WidthSq;
            [FieldOffset(28)] public float Reserved;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StringPullPathJob : IJob
        {
            private const float FunnelEpsilon = 0.00001f;
            private const float DdaEpsilon = 0.000001f;
            private const byte SolidThreatVoxel = 255;

            [NoAlias] public NativeArray<AbyssalPathStagingPoint> PathStaging;
            [ReadOnly, NoAlias] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly, NoAlias] public NativeArray<float3> DensityGrid;
            [ReadOnly, NoAlias] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly, NoAlias] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [ReadOnly, NoAlias] public NativeArray<byte> ThreatVoxelGrid;
            public int ChunkCount;
            public int TerrainHoleCount;
            public int ArtificialStructureCount;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public int NavPassabilityLength;
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
            public int PathCapacity;

            public void Execute()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return;
                }

                ResetOutputPath();
                int pathCount = GetInputPathCount();
                if (pathCount <= 0)
                    return;

                if (ResolvePathCapacity() < pathCount || !HasFiniteInputPath(pathCount))
                    return;

                if (pathCount <= 2)
                {
                    for (int i = 0; i < pathCount; i++)
                    {
                        if (!TryAppendOutputPath(ReadRawPathPoint(i)))
                        {
                            ClearOutputPathCountPreserveFlags();
                            return;
                        }
                    }

                    return;
                }

                if (!TryAppendOutputPath(ReadRawPathPoint(0)))
                    return;

                int apexIndex = 0;
                int leftIndex = 0;
                int rightIndex = 0;
                float3 apex = ToFloat3(ReadRawPathPoint(0));
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
                            if (!TryAppendOutputPath(ReadRawPathPoint(emitIndex)))
                            {
                                ClearOutputPathCountPreserveFlags();
                                return;
                            }

                            apexIndex = emitIndex;
                            apex = ToFloat3(ReadRawPathPoint(apexIndex));
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
                            if (!TryAppendOutputPath(ReadRawPathPoint(emitIndex)))
                            {
                                ClearOutputPathCountPreserveFlags();
                                return;
                            }

                            apexIndex = emitIndex;
                            apex = ToFloat3(ReadRawPathPoint(apexIndex));
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

                Vector3 endPoint = ReadRawPathPoint(pathCount - 1);
                int outputCount = GetOutputPathCount();
                if (outputCount == 0 || !Approximately(ReadResultPathPoint(outputCount - 1), endPoint))
                {
                    if (!TryAppendOutputPath(endPoint))
                    {
                        ClearOutputPathCountPreserveFlags();
                        return;
                    }
                }

                CompactPathByVoxelLineOfSight();
            }

            private bool HasFiniteInputPath(int pathCount)
            {
                if (!PathStaging.IsCreated || pathCount <= 0)
                    return false;

                for (int i = 0; i < pathCount; i++)
                {
                    if (!math.all(math.isfinite(ToFloat3(ReadRawPathPoint(i)))))
                        return false;
                }

                return true;
            }

            private int GetInputPathCount()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return 0;
                }

                return math.clamp(PathStaging[0].RawCount, 0, ResolvePathCapacity());
            }

            private int GetOutputPathCount()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return 0;
                }

                return math.clamp(PathStaging[0].ResultCount, 0, ResolvePathCapacity());
            }

            private void ResetOutputPath()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return;

                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.ResultCount = 0;
                meta.ResultFlags = 0;
                PathStaging[0] = meta;
            }

            private bool TryAppendOutputPath(Vector3 value)
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                {
                    return false;
                }

                int count = math.max(0, PathStaging[0].ResultCount);
                if (count >= ResolvePathCapacity())
                {
                    AbyssalPathStagingPoint overflowMeta = PathStaging[0];
                    overflowMeta.ResultFlags |= AbyssalPathOverflowFlag;
                    PathStaging[0] = overflowMeta;
                    return false;
                }

                AbyssalPathStagingPoint entry = PathStaging[count];
                entry.Result = value;
                PathStaging[count] = entry;
                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.ResultCount = count + 1;
                PathStaging[0] = meta;
                return true;
            }

            private void ClearOutputPathCountPreserveFlags()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return;

                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.ResultCount = 0;
                PathStaging[0] = meta;
            }

            private Vector3 ReadRawPathPoint(int index)
            {
                return PathStaging[index].Raw;
            }

            private Vector3 ReadResultPathPoint(int index)
            {
                return PathStaging[index].Result;
            }

            private void WriteResultPathPoint(int index, Vector3 value)
            {
                AbyssalPathStagingPoint entry = PathStaging[index];
                entry.Result = value;
                PathStaging[index] = entry;
            }

            private void SetOutputPathCount(int count)
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return;

                AbyssalPathStagingPoint meta = PathStaging[0];
                meta.ResultCount = math.clamp(count, 0, ResolvePathCapacity());
                PathStaging[0] = meta;
            }

            private int ResolvePathCapacity()
            {
                if (!PathStaging.IsCreated || PathStaging.Length <= 0)
                    return 0;

                int requestedCapacity = PathCapacity > 0 ? PathCapacity : PathStaging.Length;
                return math.clamp(requestedCapacity, 0, PathStaging.Length);
            }

            private NavPortal BuildPortal(int index, out float3 portalAxis)
            {
                Vector3 centerValue = ReadRawPathPoint(index);
                float3 center = ToFloat3(centerValue);
                int inputCount = GetInputPathCount();
                if (index <= 0 || index >= inputCount - 1)
                {
                    portalAxis = ResolvePortalAxis(index);
                    return BuildNavPortal(center, center);
                }

                float3 previous = ToFloat3(ReadRawPathPoint(index - 1));
                float3 next = ToFloat3(ReadRawPathPoint(index + 1));
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
                    ArtificialStructureCount <= 0 ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                if (!ArtificialStructureHash.IsCreated)
                    return IsInsideBlockingStructureLinear(position);

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

            private bool IsInsideBlockingStructureLinear(float3 position)
            {
                int count = math.min(ArtificialStructureCount, ArtificialStructures.Length);
                for (int structureIndex = 0; structureIndex < count; structureIndex++)
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
                int outputCount = GetOutputPathCount();
                if (outputCount <= 2 || !HasAnyVoxelGrid())
                {
                    return;
                }

                int sourceLength = outputCount;
                int lastIndex = sourceLength - 1;
                int maxPortalLookAhead = math.max(1, MaxPortalLookAhead);
                int anchorIndex = 0;
                int writeIndex = 0;

                int compactionIterations = 0;
                while (anchorIndex < lastIndex && compactionIterations < MaxPathCompactionIterations)
                {
                    compactionIterations++;
                    Vector3 anchorPoint = ReadResultPathPoint(anchorIndex);
                    WriteResultPathPoint(writeIndex, anchorPoint);
                    writeIndex++;

                    int farthestVisibleIndex = anchorIndex + 1;
                    int candidateLimit = math.min(lastIndex, anchorIndex + maxPortalLookAhead);
                    for (int candidateIndex = farthestVisibleIndex + 1; candidateIndex <= candidateLimit; candidateIndex++)
                    {
                        if (!HasVoxelLineOfSight(ToFloat3(anchorPoint), ToFloat3(ReadResultPathPoint(candidateIndex))))
                            break;

                        farthestVisibleIndex = candidateIndex;
                    }

                    anchorIndex = farthestVisibleIndex;
                }

                if (anchorIndex < lastIndex)
                {
                    for (int remainingIndex = anchorIndex; remainingIndex <= lastIndex && writeIndex < sourceLength; remainingIndex++)
                    {
                        Vector3 remainingPoint = ReadResultPathPoint(remainingIndex);
                        if (writeIndex == 0 || !Approximately(ReadResultPathPoint(writeIndex - 1), remainingPoint))
                        {
                            WriteResultPathPoint(writeIndex, remainingPoint);
                            writeIndex++;
                        }
                    }

                    SetOutputPathCount(writeIndex);
                    return;
                }

                Vector3 finalPoint = ReadResultPathPoint(lastIndex);
                if (writeIndex == 0 || !Approximately(ReadResultPathPoint(writeIndex - 1), finalPoint))
                {
                    WriteResultPathPoint(writeIndex, finalPoint);
                    writeIndex++;
                }

                SetOutputPathCount(writeIndex);
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
                    if (flatIndex < 0 ||
                        flatIndex >= NavPassabilityLength ||
                        flatIndex >= PathStaging.Length)
                    {
                        return SolidThreatVoxel;
                    }

                    return PathStaging[flatIndex].ScratchFlags;
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
                return PathStaging.IsCreated &&
                       HasCompleteVoxelGridLength(NavPassabilityLength, NavPassabilityDimensions) &&
                       HasUsableUniformVoxelCellSize(NavPassabilityCellSize);
            }

            private static bool HasCompleteVoxelGrid(NativeArray<byte> grid, int3 dimensions)
            {
                return grid.IsCreated && HasCompleteVoxelGridLength(grid.Length, dimensions);
            }

            private static bool HasCompleteVoxelGridLength(int gridLength, int3 dimensions)
            {
                if (gridLength <= 0 ||
                    dimensions.x <= 0 ||
                    dimensions.y <= 0 ||
                    dimensions.z <= 0)
                {
                    return false;
                }

                long expectedLength = (long)dimensions.x * dimensions.y * dimensions.z;
                return expectedLength > 0L &&
                       expectedLength <= int.MaxValue &&
                       gridLength >= expectedLength;
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
                int inputCount = GetInputPathCount();
                int clampedIndex = math.clamp(index, 0, math.max(0, inputCount - 1));
                int previousIndex = math.max(0, clampedIndex - 1);
                int nextIndex = math.min(math.max(0, inputCount - 1), clampedIndex + 1);
                float3 previous = ToFloat3(ReadRawPathPoint(previousIndex));
                float3 next = ToFloat3(ReadRawPathPoint(nextIndex));
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
