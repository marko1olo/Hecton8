using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {

        private void RebuildDensityQuerySnapshot()
        {
            if (_selectedChunkCount <= 0)
            {
                _densityQueryChunkCount = 0;
                return;
            }

            EnsureDensityQueryCapacity(_selectedChunkCount);
            bool hasPreviousDensitySnapshot = TryReadDensityQuerySnapshot(
                out NativeArray<VegetationDensityChunkRecord> previousChunks,
                out NativeArray<float3> previousDensityGrid);
            int scratchChunkCapacity = _selectedChunkCount;
            long scratchGridCapacityLong = (long)scratchChunkCapacity * DensityGridCellCount;
            if (scratchGridCapacityLong <= 0L || scratchGridCapacityLong > int.MaxValue)
            {
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationDensityQueryScratch,
                    0u,
                    scratchChunkCapacity,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return;
            }

            int scratchGridCapacity = (int)scratchGridCapacityLong;
            if (!EnsureDensityQueryScratchCapacity(scratchChunkCapacity) ||
                !_densityQueryScratchChunks.IsCreated ||
                _densityQueryScratchChunks.Length < scratchChunkCapacity ||
                !_densityQueryScratchDensityGrid.IsCreated ||
                _densityQueryScratchDensityGrid.Length < scratchGridCapacity ||
                !_densityQueryScratchThreatAttractorGrid.IsCreated ||
                _densityQueryScratchThreatAttractorGrid.Length < scratchGridCapacity)
            {
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationDensityQueryScratch,
                    0u,
                    scratchGridCapacity,
                    math.min(
                        _densityQueryScratchDensityGrid.IsCreated ? _densityQueryScratchDensityGrid.Length : 0,
                        _densityQueryScratchThreatAttractorGrid.IsCreated ? _densityQueryScratchThreatAttractorGrid.Length : 0),
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return;
            }

            NativeArray<VegetationDensityChunkRecord> scratchChunks = _densityQueryScratchChunks;
            NativeArray<float3> scratchDensityGrid = _densityQueryScratchDensityGrid;
            NativeArray<float2> scratchThreatAttractorGrid = _densityQueryScratchThreatAttractorGrid;

            int nextChunkCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                int gridOffset = nextChunkCount * DensityGridCellCount;
                ClearDensityGridCells(scratchDensityGrid, gridOffset, DensityGridCellCount);
                ClearThreatAttractorGridCells(scratchThreatAttractorGrid, gridOffset, DensityGridCellCount);
                AccumulateChunkDensityGrid(payload, ref scratchDensityGrid, gridOffset);
                AccumulateChunkThreatAttractorGrid(payload, ref scratchThreatAttractorGrid, gridOffset);

                VegetationDensityChunkRecord record = new VegetationDensityChunkRecord
                {
                    MinX = payload.MinX,
                    MaxX = payload.MaxX,
                    MinZ = payload.MinZ,
                    MaxZ = payload.MaxZ,
                    GridOffset = gridOffset,
                    GrassLodTier = payload.GrassLodTier
                };

                int previousIndex = FindDensityQueryChunkIndex(key);
                if (previousIndex >= 0)
                {
                    if (hasPreviousDensitySnapshot &&
                        (uint)previousIndex < (uint)previousChunks.Length)
                    {
                        VegetationDensityChunkRecord previousRecord = previousChunks[previousIndex];
                        if (previousRecord.GrassLodTier != payload.GrassLodTier)
                            BlendDensityGrid(previousDensityGrid, previousRecord.GridOffset, scratchDensityGrid, gridOffset, DensityGridCellCount, 0.35f);
                    }
                }

                scratchChunks[nextChunkCount] = record;
                _densityQueryChunkKeys[nextChunkCount] = key;
                nextChunkCount++;
            }

                if (nextChunkCount > 0)
                {
                    int gridCopyLength = nextChunkCount * DensityGridCellCount;
                    if (!TryPublishDensityQuerySnapshot(
                            scratchChunks,
                            scratchDensityGrid,
                            scratchThreatAttractorGrid,
                            nextChunkCount,
                            gridCopyLength))
                    {
                        _densityQueryChunkCount = 0;
                        return;
                    }
                }

            for (int i = nextChunkCount; i < _densityQueryChunkCount; i++)
                _densityQueryChunkKeys[i] = default;

            _densityQueryChunkCount = nextChunkCount;
        }

        private bool EnsureDensityQueryScratchCapacity(int chunkCapacity)
        {
            if (chunkCapacity <= 0)
                return false;

            int nextChunkCapacity = Mathf.NextPowerOfTwo(math.max(InitialChunkArrayCapacity, chunkCapacity));
            long gridCapacityLong = (long)nextChunkCapacity * DensityGridCellCount;
            if (gridCapacityLong <= 0L || gridCapacityLong > int.MaxValue)
                return false;

            int nextGridCapacity = (int)gridCapacityLong;
            if (_densityQueryScratchChunks.IsCreated &&
                _densityQueryScratchChunks.Length >= nextChunkCapacity &&
                _densityQueryScratchDensityGrid.IsCreated &&
                _densityQueryScratchDensityGrid.Length >= nextGridCapacity &&
                _densityQueryScratchThreatAttractorGrid.IsCreated &&
                _densityQueryScratchThreatAttractorGrid.Length >= nextGridCapacity)
            {
                return true;
            }

            DisposeDensityQueryScratch();
            try
            {
                // COLD ALLOC: VegetationDensityChunkRecord[nextChunkCapacity] - persistent density-query rebuild scratch - owner: HectonMapMagicVegetationBridge
                _densityQueryScratchChunks = AllocateDensityQueryNativeArray<VegetationDensityChunkRecord>(
                    nextChunkCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(_densityQueryScratchChunks));
                // COLD ALLOC: float3[nextGridCapacity] - persistent density-query grid rebuild scratch - owner: HectonMapMagicVegetationBridge
                _densityQueryScratchDensityGrid = AllocateDensityQueryNativeArray<float3>(
                    nextGridCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(_densityQueryScratchDensityGrid));
                // COLD ALLOC: float2[nextGridCapacity] - persistent threat-attractor rebuild scratch - owner: HectonMapMagicVegetationBridge
                _densityQueryScratchThreatAttractorGrid = AllocateDensityQueryNativeArray<float2>(
                    nextGridCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(_densityQueryScratchThreatAttractorGrid));
            }
            catch
            {
                DisposeDensityQueryScratch();
                throw;
            }

            return _densityQueryScratchChunks.IsCreated &&
                   _densityQueryScratchChunks.Length >= nextChunkCapacity &&
                   _densityQueryScratchDensityGrid.IsCreated &&
                   _densityQueryScratchDensityGrid.Length >= nextGridCapacity &&
                   _densityQueryScratchThreatAttractorGrid.IsCreated &&
                   _densityQueryScratchThreatAttractorGrid.Length >= nextGridCapacity;
        }

        private void DisposeDensityQueryScratch()
        {
            DisposeDensityQueryNativeArray(ref _densityQueryScratchThreatAttractorGrid);
            DisposeDensityQueryNativeArray(ref _densityQueryScratchDensityGrid);
            DisposeDensityQueryNativeArray(ref _densityQueryScratchChunks);
        }

        private int FindDensityQueryChunkIndex(ChunkKey key)
        {
            for (int i = 0; i < _densityQueryChunkCount; i++)
            {
                if (_densityQueryChunkKeys[i].Equals(key))
                    return i;
            }

            return -1;
        }

        private bool TryPublishDensityQuerySnapshot(
            NativeArray<VegetationDensityChunkRecord> scratchChunks,
            NativeArray<float3> scratchDensityGrid,
            NativeArray<float2> scratchThreatAttractorGrid,
            int chunkCount,
            int gridLength)
        {
            if (chunkCount <= 0 ||
                gridLength <= 0 ||
                !scratchChunks.IsCreated ||
                !scratchDensityGrid.IsCreated ||
                !scratchThreatAttractorGrid.IsCreated ||
                scratchChunks.Length < chunkCount ||
                scratchDensityGrid.Length < gridLength ||
                scratchThreatAttractorGrid.Length < gridLength)
            {
                return false;
            }

            return CopyDensityQueryChunksToVault(scratchChunks, chunkCount) &&
                   CopyDensityQueryGridToVault(scratchDensityGrid, gridLength) &&
                   CopyThreatAttractorGridToVault(scratchThreatAttractorGrid, gridLength);
        }

        private bool CopyDensityQueryChunksToVault(
            NativeArray<VegetationDensityChunkRecord> scratchChunks,
            int chunkCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.DensityQueryChunksHandle,
                    BufferID.VegetationDensityQueryChunks,
                    chunkCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<VegetationDensityChunkRecord> chunks))
            {
                return false;
            }

            try
            {
                NativeArray<VegetationDensityChunkRecord>.Copy(scratchChunks, chunks, chunkCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.DensityQueryChunksHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyDensityQueryGridToVault(
            NativeArray<float3> scratchDensityGrid,
            int gridLength)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.DensityQueryGridHandle,
                    BufferID.VegetationDensityQueryGrid,
                    gridLength,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<float3> densityGrid))
            {
                return false;
            }

            try
            {
                NativeArray<float3>.Copy(scratchDensityGrid, densityGrid, gridLength);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.DensityQueryGridHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyThreatAttractorGridToVault(
            NativeArray<float2> scratchThreatAttractorGrid,
            int gridLength)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.ThreatAttractorGridHandle,
                    BufferID.VegetationThreatAttractorGrid,
                    gridLength,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<float2> threatAttractorGrid))
            {
                return false;
            }

            try
            {
                NativeArray<float2>.Copy(scratchThreatAttractorGrid, threatAttractorGrid, gridLength);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.ThreatAttractorGridHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool TryResolveDensityQueryLengths(out int chunkCount, out int gridLength)
        {
            chunkCount = 0;
            gridLength = 0;
            int currentChunkCount = _densityQueryChunkCount;
            long requiredGridLength = (long)currentChunkCount * DensityGridCellCount;
            if (currentChunkCount <= 0 ||
                requiredGridLength <= 0L ||
                requiredGridLength > int.MaxValue)
            {
                return false;
            }

            chunkCount = currentChunkCount;
            gridLength = (int)requiredGridLength;
            return true;
        }

        private bool TryReadDensityQuerySnapshot(
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float3> densityGrid)
        {
            chunks = default;
            densityGrid = default;
            return TryResolveDensityQueryLengths(out int chunkCount, out int gridLength) &&
                   TryReadVegetationMemoryBuffer(
                       in _nativeMemory.DensityQueryChunksHandle,
                       BufferID.VegetationDensityQueryChunks,
                       chunkCount,
                       out chunks) &&
                   TryReadVegetationMemoryBuffer(
                       in _nativeMemory.DensityQueryGridHandle,
                       BufferID.VegetationDensityQueryGrid,
                       gridLength,
                       out densityGrid);
        }

        private bool TryReadDensityThreatAttractorSnapshot(
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float3> densityGrid,
            out NativeArray<float2> threatAttractorGrid)
        {
            chunks = default;
            densityGrid = default;
            threatAttractorGrid = default;
            return TryReadDensityQuerySnapshot(out chunks, out densityGrid) &&
                   TryResolveDensityQueryLengths(out _, out int gridLength) &&
                   TryReadVegetationMemoryBuffer(
                       in _nativeMemory.ThreatAttractorGridHandle,
                       BufferID.VegetationThreatAttractorGrid,
                       gridLength,
                       out threatAttractorGrid);
        }

        private bool TryPrepareDensityQueryJobSnapshot(
            bool includeDensityGrid,
            bool includeThreatAttractorGrid,
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float3> densityGrid,
            out NativeArray<float2> threatAttractorGrid,
            out int leaseIndex)
        {
            chunks = default;
            densityGrid = default;
            threatAttractorGrid = default;
            leaseIndex = -1;
            if (!TryResolveDensityQueryLengths(out int chunkCount, out int gridLength))
                return true;

            if (!TryAcquireDensityQuerySnapshotLease(
                    chunkCount,
                    gridLength,
                    includeDensityGrid,
                    includeThreatAttractorGrid,
                    out leaseIndex,
                    out chunks,
                    out densityGrid,
                    out threatAttractorGrid))
            {
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationDensityQueryChunks,
                    _nativeMemory.DensityQueryChunksHandle.Generation,
                    chunkCount + (includeDensityGrid ? gridLength : 0) + (includeThreatAttractorGrid ? gridLength : 0),
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return false;
            }

            NativeArray<VegetationDensityChunkRecord> sourceChunks = default;
            NativeArray<float3> sourceDensityGrid = default;
            NativeArray<float2> sourceThreatAttractorGrid = default;
            if (!TryReadVegetationMemoryBuffer(
                    in _nativeMemory.DensityQueryChunksHandle,
                    BufferID.VegetationDensityQueryChunks,
                    chunkCount,
                    out sourceChunks) ||
                (includeDensityGrid &&
                 !TryReadVegetationMemoryBuffer(
                     in _nativeMemory.DensityQueryGridHandle,
                     BufferID.VegetationDensityQueryGrid,
                     gridLength,
                     out sourceDensityGrid)) ||
                (includeThreatAttractorGrid &&
                 !TryReadVegetationMemoryBuffer(
                     in _nativeMemory.ThreatAttractorGridHandle,
                     BufferID.VegetationThreatAttractorGrid,
                     gridLength,
                     out sourceThreatAttractorGrid)))
            {
                ReleaseDensityQuerySnapshotLease(leaseIndex);
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationDensityQueryChunks,
                    _nativeMemory.DensityQueryChunksHandle.Generation,
                    chunkCount,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.VaultResolveFailed,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagStaleHandle,
                    default);
                return false;
            }

            NativeArray<VegetationDensityChunkRecord>.Copy(sourceChunks, chunks, chunkCount);
            if (includeDensityGrid)
                NativeArray<float3>.Copy(sourceDensityGrid, densityGrid, gridLength);
            if (includeThreatAttractorGrid)
                NativeArray<float2>.Copy(sourceThreatAttractorGrid, threatAttractorGrid, gridLength);
            return true;
        }

        private bool TryAcquireDensityQuerySnapshotLease(
            int chunkCount,
            int gridLength,
            bool includeDensityGrid,
            bool includeThreatAttractorGrid,
            out int leaseIndex,
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float3> densityGrid,
            out NativeArray<float2> threatAttractorGrid)
        {
            leaseIndex = -1;
            chunks = default;
            densityGrid = default;
            threatAttractorGrid = default;
            if (chunkCount <= 0 ||
                gridLength <= 0)
            {
                return false;
            }

            int selectedChunkCapacity = _selectedChunkKeys != null ? _selectedChunkKeys.Length : 0;
            long selectedGridCapacity = (long)selectedChunkCapacity * DensityGridCellCount;
            if (selectedChunkCapacity <= 0 ||
                chunkCount > selectedChunkCapacity ||
                gridLength > selectedGridCapacity)
            {
                return false;
            }

            ReclaimDensityQuerySnapshotLeases();
            for (int i = 0; i < _densityQuerySnapshotLeases.Length; i++)
            {
                DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[i];
                if (lease.Active)
                    continue;

                if (!IsDensityQuerySnapshotLeaseReady(
                        in lease,
                        chunkCount,
                        gridLength,
                        includeDensityGrid,
                        includeThreatAttractorGrid))
                    continue;

                lease.Active = true;
                lease.Handle = default;
                _densityQuerySnapshotLeases[i] = lease;
                leaseIndex = i;
                chunks = lease.Chunks.GetSubArray(0, chunkCount);
                densityGrid = includeDensityGrid ? lease.DensityGrid.GetSubArray(0, gridLength) : default;
                threatAttractorGrid = includeThreatAttractorGrid ? lease.ThreatAttractorGrid.GetSubArray(0, gridLength) : default;
                return true;
            }

            return false;
        }

        private bool EnsureDensityQuerySnapshotLeaseBankCapacity(
            int chunkCount,
            bool includeDensityGrid,
            bool includeThreatAttractorGrid)
        {
            if (chunkCount <= 0)
                return false;

            long gridLengthLong = (long)chunkCount * DensityGridCellCount;
            if (gridLengthLong <= 0L || gridLengthLong > int.MaxValue)
                return false;

            int gridLength = (int)gridLengthLong;
            bool allReady = true;
            for (int i = 0; i < _densityQuerySnapshotLeases.Length; i++)
            {
                DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[i];
                if (lease.Active)
                {
                    allReady = false;
                    continue;
                }

                if (!EnsureDensityQuerySnapshotLeaseCapacity(
                        ref lease,
                        chunkCount,
                        gridLength,
                        includeDensityGrid,
                        includeThreatAttractorGrid))
                {
                    allReady = false;
                }

                _densityQuerySnapshotLeases[i] = lease;
            }

            return allReady;
        }

        private static bool IsDensityQuerySnapshotLeaseReady(
            in DensityQuerySnapshotLease lease,
            int chunkCount,
            int gridLength,
            bool includeDensityGrid,
            bool includeThreatAttractorGrid)
        {
            return lease.Chunks.IsCreated &&
                   lease.ChunkCapacity >= chunkCount &&
                   (!includeDensityGrid || (lease.DensityGrid.IsCreated && lease.GridCapacity >= gridLength)) &&
                   (!includeThreatAttractorGrid || (lease.ThreatAttractorGrid.IsCreated && lease.GridCapacity >= gridLength));
        }

        private bool EnsureDensityQuerySnapshotLeaseCapacity(
            ref DensityQuerySnapshotLease lease,
            int chunkCount,
            int gridLength,
            bool includeDensityGrid,
            bool includeThreatAttractorGrid)
        {
            int chunkCapacity = Mathf.NextPowerOfTwo(math.max(InitialChunkArrayCapacity, chunkCount));
            int gridCapacity = Mathf.NextPowerOfTwo(math.max(DensityGridCellCount, gridLength));
            if (lease.Chunks.IsCreated &&
                lease.ChunkCapacity >= chunkCapacity &&
                (!includeDensityGrid || (lease.DensityGrid.IsCreated && lease.GridCapacity >= gridCapacity)) &&
                (!includeThreatAttractorGrid || (lease.ThreatAttractorGrid.IsCreated && lease.GridCapacity >= gridCapacity)))
            {
                return true;
            }

            DisposeDensityQuerySnapshotLeaseArrays(ref lease);
            try
            {
                lease.ChunkCapacity = chunkCapacity;
                lease.GridCapacity = gridCapacity;
                // COLD ALLOC: VegetationDensityChunkRecord[chunkCapacity] - public density query lease bank - owner: HectonMapMagicVegetationBridge
                lease.Chunks = AllocateDensityQueryNativeArray<VegetationDensityChunkRecord>(
                    chunkCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(_densityQuerySnapshotLeases));

                if (includeDensityGrid)
                {
                    // COLD ALLOC: float3[gridCapacity] - public visibility/biomass density lease grid - owner: HectonMapMagicVegetationBridge
                    lease.DensityGrid = AllocateDensityQueryNativeArray<float3>(
                        gridCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(_densityQuerySnapshotLeases));
                }

                if (includeThreatAttractorGrid)
                {
                    // COLD ALLOC: float2[gridCapacity] - public threat-attractor density lease grid - owner: HectonMapMagicVegetationBridge
                    lease.ThreatAttractorGrid = AllocateDensityQueryNativeArray<float2>(
                        gridCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(_densityQuerySnapshotLeases));
                }
            }
            catch
            {
                DisposeDensityQuerySnapshotLeaseArrays(ref lease);
                throw;
            }

            bool ready = lease.Chunks.IsCreated &&
                         lease.Chunks.Length >= chunkCount &&
                         (!includeDensityGrid || (lease.DensityGrid.IsCreated && lease.DensityGrid.Length >= gridLength)) &&
                         (!includeThreatAttractorGrid || (lease.ThreatAttractorGrid.IsCreated && lease.ThreatAttractorGrid.Length >= gridLength));
            if (!ready)
                DisposeDensityQuerySnapshotLeaseArrays(ref lease);

            return ready;
        }

        private void MarkDensityQuerySnapshotLeaseScheduled(int leaseIndex, JobHandle handle)
        {
            if ((uint)leaseIndex >= (uint)_densityQuerySnapshotLeases.Length)
                return;

            DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[leaseIndex];
            if (!lease.Active)
                return;

            lease.Handle = handle;
            _densityQuerySnapshotLeases[leaseIndex] = lease;
        }

        private void ReleaseDensityQuerySnapshotLease(int leaseIndex)
        {
            if ((uint)leaseIndex >= (uint)_densityQuerySnapshotLeases.Length)
                return;

            DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[leaseIndex];
            lease.Active = false;
            lease.Handle = default;
            _densityQuerySnapshotLeases[leaseIndex] = lease;
        }

        private void ReclaimDensityQuerySnapshotLeases()
        {
            for (int i = 0; i < _densityQuerySnapshotLeases.Length; i++)
            {
                DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[i];
                if (!lease.Active ||
                    !DispatcherJobFence.TryFinalizeCompleted(ref lease.Handle))
                {
                    continue;
                }

                lease.Active = false;
                _densityQuerySnapshotLeases[i] = lease;
            }
        }

        private void DisposeDensityQuerySnapshotLeases()
        {
            for (int i = 0; i < _densityQuerySnapshotLeases.Length; i++)
            {
                DensityQuerySnapshotLease lease = _densityQuerySnapshotLeases[i];
                DisposeDensityQuerySnapshotLeaseArrays(ref lease);
                lease.Active = false;
                lease.ChunkCapacity = 0;
                lease.GridCapacity = 0;
                lease.Handle = default;
                _densityQuerySnapshotLeases[i] = lease;
            }
        }

        private static void DisposeDensityQuerySnapshotLeaseArrays(ref DensityQuerySnapshotLease lease)
        {
            DisposeDensityQueryNativeArray(ref lease.ThreatAttractorGrid, lease.Handle);
            DisposeDensityQueryNativeArray(ref lease.DensityGrid, lease.Handle);
            DisposeDensityQueryNativeArray(ref lease.Chunks, lease.Handle);
            lease.Active = false;
            lease.Handle = default;
        }

        private static NativeArray<T> AllocateDensityQueryNativeArray<T>(
            int length,
            NativeArrayOptions options,
            string label) where T : struct
        {
            NativeArray<T> array = H8Memory.Allocate<T>(
                length,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                options);
            if (!array.IsCreated)
                throw new InvalidOperationException($"{nameof(HectonMapMagicVegetationBridge)} density query native allocation failed for {label}.");

            return array;
        }

        private static void DisposeDensityQueryNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            H8Memory.Release(ref array, VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private static void DisposeDensityQueryNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            JobHandle disposeHandle = H8Memory.Release(ref array, dependency, VegetationMemorySovereigntyConstants.OwnerSystemId);
            if (!array.IsCreated)
                DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);
        }

        private bool TryPinDensityQueryJobSnapshot(
            bool includeDensityGrid,
            bool includeThreatAttractorGrid,
            uint chunksPinBit,
            uint densityGridPinBit,
            uint threatAttractorGridPinBit,
            ref IDataVault readPinVault,
            ref uint readPinMask,
            out NativeArray<VegetationDensityChunkRecord> chunks,
            out NativeArray<float3> densityGrid,
            out NativeArray<float2> threatAttractorGrid,
            out int chunkCount,
            out int gridLength)
        {
            chunks = default;
            densityGrid = default;
            threatAttractorGrid = default;
            chunkCount = 0;
            gridLength = 0;
            if (!TryResolveDensityQueryLengths(out chunkCount, out gridLength))
                return true;

            bool resolved =
                TryPinVegetationReadBuffer(
                    BufferID.VegetationDensityQueryChunks,
                    chunksPinBit,
                    ref readPinVault,
                    ref readPinMask) &&
                TryReadVegetationMemoryBuffer(
                    in _nativeMemory.DensityQueryChunksHandle,
                    BufferID.VegetationDensityQueryChunks,
                    chunkCount,
                    out chunks) &&
                (!includeDensityGrid ||
                 (TryPinVegetationReadBuffer(
                      BufferID.VegetationDensityQueryGrid,
                      densityGridPinBit,
                      ref readPinVault,
                      ref readPinMask) &&
                  TryReadVegetationMemoryBuffer(
                      in _nativeMemory.DensityQueryGridHandle,
                      BufferID.VegetationDensityQueryGrid,
                      gridLength,
                      out densityGrid))) &&
                (!includeThreatAttractorGrid ||
                 (TryPinVegetationReadBuffer(
                      BufferID.VegetationThreatAttractorGrid,
                      threatAttractorGridPinBit,
                      ref readPinVault,
                      ref readPinMask) &&
                  TryReadVegetationMemoryBuffer(
                      in _nativeMemory.ThreatAttractorGridHandle,
                      BufferID.VegetationThreatAttractorGrid,
                      gridLength,
                      out threatAttractorGrid)));

            if (resolved)
                return true;

            int actualLength = (chunks.IsCreated ? chunks.Length : 0) +
                               (densityGrid.IsCreated ? densityGrid.Length : 0) +
                               (threatAttractorGrid.IsCreated ? threatAttractorGrid.Length : 0);
            RecordVegetationMemoryTelemetry(
                BufferID.VegetationDensityQueryChunks,
                _nativeMemory.DensityQueryChunksHandle.Generation,
                chunkCount + (includeDensityGrid ? gridLength : 0) + (includeThreatAttractorGrid ? gridLength : 0),
                actualLength,
                0,
                0f,
                VegetationMemoryTelemetryCode.VaultResolveFailed,
                VegetationMemoryTelemetryPhase.SlowTick,
                VegetationMemorySovereigntyConstants.FlagStaleHandle,
                default);
            chunks = default;
            densityGrid = default;
            threatAttractorGrid = default;
            chunkCount = 0;
            gridLength = 0;
            return false;
        }

        private void RebuildAbyssalAnchorSnapshot()
        {
            int anchorCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                    continue;

                anchorCount += CountSemanticType(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount, (int)VegetationSemanticType.DeadZoneMassiveStructure);
            }

            _abyssalAnchorCount = anchorCount;
            if (anchorCount <= 0)
                return;

            EnsureVector3Capacity(ref _abyssalAnchorPositions, anchorCount);
            if (!WriteAbyssalAnchorPositions(anchorCount, out int resolvedAnchorCount))
            {
                _abyssalAnchorCount = 0;
                return;
            }

            _abyssalAnchorCount = resolvedAnchorCount;
            if (resolvedAnchorCount <= 0)
                return;

            if (!WriteAbyssalAnchorAupPositions(resolvedAnchorCount))
                _abyssalAnchorCount = 0;
        }

        private bool WriteAbyssalAnchorPositions(int anchorCount, out int resolvedAnchorCount)
        {
            resolvedAnchorCount = 0;
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalAnchorPositionsHandle,
                    BufferID.VegetationAbyssalAnchorPositions,
                    anchorCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<Vector3> nativeAnchorPositions))
            {
                return false;
            }

            try
            {
                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;

                    CopySemanticAnchorPositions(
                        ResolveChunkPool(isSurface: false, payload),
                        payload.UnderwaterOffset,
                        payload.UnderwaterCount,
                        (int)VegetationSemanticType.DeadZoneMassiveStructure,
                        _abyssalAnchorPositions,
                        nativeAnchorPositions,
                        _totalUniverseOffsetDouble,
                        ref writeIndex);
                }

                resolvedAnchorCount = math.min(anchorCount, writeIndex);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalAnchorPositionsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteAbyssalAnchorAupPositions(int anchorCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalAnchorAupPositionsHandle,
                    BufferID.VegetationAbyssalAnchorAupPositions,
                    anchorCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<AbsoluteUniversePosition> nativeAnchorAupPositions))
            {
                return false;
            }

            try
            {
                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount && writeIndex < anchorCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;

                    CopySemanticAnchorAupPositions(
                        ResolveChunkPool(isSurface: false, payload),
                        payload.UnderwaterOffset,
                        payload.UnderwaterCount,
                        (int)VegetationSemanticType.DeadZoneMassiveStructure,
                        nativeAnchorAupPositions,
                        _totalUniverseOffsetDouble,
                        anchorCount,
                        ref writeIndex);
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalAnchorAupPositionsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void RebuildAbyssalNavNodeSnapshot()
        {
            InvalidateAbyssalPathState();
            int nodeCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!TryGetChunkAbyssalNavPayload(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || payload.Nodes == null)
                    continue;

                nodeCount += math.min(payload.Count, payload.Nodes.Length);
            }

            int fixedNodeCapacity = ResolveMaxAbyssalNavNodeCapacity();
            if (nodeCount > fixedNodeCapacity)
                nodeCount = fixedNodeCapacity;

            _abyssalNavNodeCount = nodeCount;
            if (nodeCount <= 0)
            {
                _abyssalNavGraphOrigin = Vector3.zero;
                return;
            }

            if (!EnsureAbyssalNavNodeListCapacity(nodeCount))
            {
                _abyssalNavNodeCount = 0;
                return;
            }

            EnsureVector3Capacity(ref _abyssalNavNodeSnapshot, fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalNavConduitVectorsSnapshot, fixedNodeCapacity);
            EnsureFloatCapacity(ref _abyssalNavConduitStrengthSnapshot, fixedNodeCapacity);
            EnsureByteCapacity(ref _abyssalNavNodeTypesSnapshot, fixedNodeCapacity);
            if (!EnsureAbyssalNavSnapshotHandles(fixedNodeCapacity))
            {
                _abyssalNavNodeCount = 0;
                return;
            }

            bool hasOrigin = false;
            Vector3 minNode = default;

            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount && writeIndex < nodeCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!TryGetChunkAbyssalNavPayload(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || payload.Nodes == null)
                    continue;

                int payloadNodeCount = math.min(payload.Count, payload.Nodes.Length);
                for (int nodeIndex = 0; nodeIndex < payloadNodeCount && writeIndex < nodeCount; nodeIndex++)
                {
                    Vector3 node = payload.Nodes[nodeIndex];
                    if (!IsFinite(node))
                        continue;

                    Vector3 conduitVector = payload.ConduitVectors != null && nodeIndex < payload.ConduitVectors.Length
                        ? payload.ConduitVectors[nodeIndex]
                        : Vector3.zero;
                    float conduitStrength = payload.ConduitStrengths != null && nodeIndex < payload.ConduitStrengths.Length
                        ? payload.ConduitStrengths[nodeIndex]
                        : 0f;
                    if (!IsFinite(conduitVector))
                        conduitVector = Vector3.zero;
                    if (!math.isfinite(conduitStrength))
                        conduitStrength = 0f;

                    byte nodeType = payload.NodeTypes != null && nodeIndex < payload.NodeTypes.Length
                        ? payload.NodeTypes[nodeIndex]
                        : (byte)NavNodeType.Water;
                    _abyssalNavNodeSnapshot[writeIndex] = node;
                    _abyssalNavConduitVectorsSnapshot[writeIndex] = conduitVector;
                    _abyssalNavConduitStrengthSnapshot[writeIndex] = conduitStrength;
                    _abyssalNavNodeTypesSnapshot[writeIndex] = nodeType;
                    if (!hasOrigin)
                    {
                        minNode = node;
                        hasOrigin = true;
                    }
                    else
                    {
                        minNode.x = Mathf.Min(minNode.x, node.x);
                        minNode.y = Mathf.Min(minNode.y, node.y);
                        minNode.z = Mathf.Min(minNode.z, node.z);
                    }
                    writeIndex++;
                }
            }

            _abyssalNavNodeCount = writeIndex;
            _abyssalNavGraphOrigin = hasOrigin ? minNode : Vector3.zero;
            if (!TryMirrorAbyssalNavSnapshotsToVault(_abyssalNavNodeCount))
            {
                _abyssalNavNodeCount = 0;
                return;
            }
        }

        private void RebuildMegaWreckStreamSnapshot()
        {
            int sectionCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!TryGetChunkMegaWreckPayload(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                sectionCount += payload.Count;
            }

            _megaWreckStreamCount = sectionCount;
            if (sectionCount <= 0)
                return;

            EnsureMegaWreckSectionCapacity(ref _megaWreckStreamSnapshot, sectionCount);
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.MegaWreckStreamSnapshotHandle,
                    BufferID.VegetationMegaWreckStreamSnapshot,
                    sectionCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<MegaWreckStreamSection> nativeSections))
            {
                _megaWreckStreamCount = 0;
                return;
            }

            int writeIndex = 0;
            try
            {
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    ChunkKey key = _selectedChunkKeys[i];
                    if (!TryGetChunkMegaWreckPayload(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                        continue;

                    for (int sectionIndex = 0; sectionIndex < payload.Count; sectionIndex++)
                    {
                        MegaWreckStreamSection section = payload.Sections[sectionIndex];
                        _megaWreckStreamSnapshot[writeIndex] = section;
                        nativeSections[writeIndex] = section;
                        writeIndex++;
                    }
                }
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.MegaWreckStreamSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void RebuildCanopyHeightGrid()
        {
            _canopyGridCenter = playerTransform != null ? playerTransform.position : _ecosystemThreatGridCenter;
            if (_canopyGridResolution <= 0 ||
                !TryAcquireCanopyGridBuffer(out IDataVault vault, out NativeArray<float> canopyGrid))
            {
                _canopyGridInitialized = false;
                return;
            }

            try
            {
                for (int i = 0; i < _canopyGridCellCount; i++)
                    canopyGrid[i] = float.NegativeInfinity;

                for (int i = 0; i < _megaWreckStreamCount; i++)
                {
                    MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                    Bounds bounds = GetMegaWreckSectionBounds(section);
                    StampCanopyBounds(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.max.y, canopyGrid);
                }

                StampCanopyFromChunkPool(useStructuralThickness: false, canopyGrid);
                StampCanopyFromChunkPool(useStructuralThickness: true, canopyGrid);
                _canopyGridInitialized = true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.CanopyHeightGridHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void StampCanopyFromChunkPool(bool useStructuralThickness, NativeArray<float> canopyGrid)
        {
            if (_selectedChunkCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                    continue;

                int offset = useStructuralThickness ? payload.UnderwaterOffset : payload.SurfaceOffset;
                int count = useStructuralThickness ? payload.UnderwaterCount : payload.SurfaceCount;
                if (count <= 0)
                    continue;

                NativeChunkPool pool = ResolveChunkPool(isSurface: !useStructuralThickness, payload);
                int requiredPoolCount = offset + count;
                if (offset < 0 ||
                    requiredPoolCount < offset ||
                    !TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
                {
                    continue;
                }

                int end = Mathf.Min(poolView.Capacity, requiredPoolCount);
                for (int poolIndex = Mathf.Max(0, offset); poolIndex < end; poolIndex++)
                {
                    int semanticType = poolView.SemanticTypes[poolIndex];
                    if (useStructuralThickness)
                    {
                        if (semanticType != (int)VegetationSemanticType.ColonyHullPlating &&
                            semanticType != (int)VegetationSemanticType.ColonySupportBeam &&
                            semanticType != (int)VegetationSemanticType.DeadZoneMassiveStructure)
                        {
                            continue;
                        }
                    }
                    else if (semanticType != (int)VegetationSemanticType.FloatingSargassum)
                    {
                        continue;
                    }

                    Vector3 position = ResolveRuntimePosition(poolView.Matrices[poolIndex]);
                    HectonVegetationInstanceData metadata = poolView.Metadata[poolIndex];
                    float halfExtent = Mathf.Max(2f, metadata.WidthScale * (useStructuralThickness ? canopyStructureThickness : canopySargassumThickness));
                    float canopyTopY = position.y + Mathf.Max(metadata.HeightScale, useStructuralThickness ? canopyStructureThickness : canopySargassumThickness);
                    StampCanopyBounds(
                        position.x - halfExtent,
                        position.x + halfExtent,
                        position.z - halfExtent,
                        position.z + halfExtent,
                        canopyTopY,
                        canopyGrid);
                }
            }
        }

        private void StampCanopyBounds(float minX, float maxX, float minZ, float maxZ, float canopyY, NativeArray<float> canopyGrid)
        {
            if (!canopyGrid.IsCreated || _canopyGridResolution <= 0)
                return;

            int halfExtent = _canopyGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _canopyGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = rowOffset + cellX;
                    if (canopyY > canopyGrid[index])
                        canopyGrid[index] = canopyY;
                }
            }
        }

        private void DistortAggregateFlowVectorsByThreat(ref ActiveAggregateNativeBufferSet buffers, int count)
        {
            if (!_threatGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridHandle,
                    BufferID.VegetationEcosystemThreatGrid,
                    _ecosystemThreatGridCellCount,
                    out _) ||
                _ecosystemThreatGridResolution <= 0 ||
                count <= 0 ||
                threatWhirlpoolStrength <= 0f ||
                _currentThreatHotspotLevel < threatWhirlpoolThreshold)
            {
                return;
            }

            if (!TryReadAggregateBuffer(in buffers.MatricesHandle, count, out NativeArray<Matrix4x4> matrices))
                return;

            if (!DistortAggregateFlowVectorsByThreatOneLock(ref buffers, count, matrices))
                return;

            WriteAggregateFlowDirectionsFromThreatOneLock(ref buffers, count, matrices);
        }

        private bool DistortAggregateFlowVectorsByThreatOneLock(
            ref ActiveAggregateNativeBufferSet buffers,
            int count,
            NativeArray<Matrix4x4> matrices)
        {
            if (!TryAcquireAggregateWriteBuffer(ref buffers.FlowVectorsHandle, count, out IDataVault vault, out NativeArray<Vector3> flowVectors))
                return false;

            try
            {
                float radiusSq = threatWhirlpoolRadius * threatWhirlpoolRadius;
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = ResolveRuntimePosition(matrices[i]);
                    float localThreat = GetThreatLevel(position);
                    if (localThreat < threatWhirlpoolThreshold)
                        continue;

                    Vector3 radial = position - _currentThreatHotspotPosition;
                    float radialSq = (radial.x * radial.x) + (radial.z * radial.z);
                    if (radialSq <= 0.0001f || radialSq > radiusSq)
                        continue;

                    float swirl01 = Mathf.Clamp01((localThreat - threatWhirlpoolThreshold) / Mathf.Max(0.01f, 1f - threatWhirlpoolThreshold));
                    swirl01 *= 1f - Mathf.Clamp01(radialSq / radiusSq);
                    Vector3 tangent = NormalizeVector3Fast(new Vector3(-radial.z, 0f, radial.x), Vector3.forward);
                    Vector3 baseFlow = flowVectors[i];
                    float fakeMagnitude = Mathf.Max(EstimateLength3D(baseFlow), 1f);
                    float blend = Mathf.Clamp01(swirl01 * threatWhirlpoolStrength);
                    Vector3 distortedFlow = baseFlow + ((tangent * fakeMagnitude) - baseFlow) * blend;
                    flowVectors[i] = distortedFlow;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in buffers.FlowVectorsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void WriteAggregateFlowDirectionsFromThreatOneLock(
            ref ActiveAggregateNativeBufferSet buffers,
            int count,
            NativeArray<Matrix4x4> matrices)
        {
            if (!TryReadAggregateBuffer(in buffers.FlowVectorsHandle, count, out NativeArray<Vector3> flowVectors) ||
                !TryAcquireAggregateWriteBuffer(ref buffers.FlowDirectionsHandle, count, out IDataVault vault, out NativeArray<Vector2> flowDirections))
            {
                return;
            }

            try
            {
                float radiusSq = threatWhirlpoolRadius * threatWhirlpoolRadius;
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = ResolveRuntimePosition(matrices[i]);
                    float localThreat = GetThreatLevel(position);
                    if (localThreat < threatWhirlpoolThreshold)
                        continue;

                    Vector3 radial = position - _currentThreatHotspotPosition;
                    float radialSq = (radial.x * radial.x) + (radial.z * radial.z);
                    if (radialSq <= 0.0001f || radialSq > radiusSq)
                        continue;

                    Vector3 distortedFlow = flowVectors[i];
                    flowDirections[i] = NormalizeFlowDirection(new Vector2(distortedFlow.x, distortedFlow.z));
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in buffers.FlowDirectionsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void AccumulateChunkDensityGrid(ChunkPayload payload, ref NativeArray<float3> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.SurfaceCount > 0)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkDensityGridFromSlice(
                    ResolveChunkPool(isSurface: true, payload),
                    payload.SurfaceOffset,
                    payload.SurfaceCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassArea,
                    kelpStepMeters * kelpStepMeters,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }

            if (payload.UnderwaterCount > 0)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkDensityGridFromSlice(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassStepMeters * grassStepMeters,
                    kelpArea,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }
        }

        private void AccumulateChunkThreatAttractorGrid(ChunkPayload payload, ref NativeArray<float2> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.HasSurface)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: true, payload),
                    payload.SurfaceOffset,
                    payload.SurfaceCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassArea,
                    kelpStepMeters * kelpStepMeters,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }

            if (payload.HasUnderwater)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassStepMeters * grassStepMeters,
                    kelpArea,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }
        }

        private void AccumulateChunkDensityGridFromSlice(
            NativeChunkPool pool,
            int offset,
            int count,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellArea,
            float grassRepresentedArea,
            float kelpRepresentedArea,
            float sargassumRepresentedArea,
            ref NativeArray<float3> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            double inverseWidth = 1.0 / width;
            double inverseDepth = 1.0 / depth;
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset ||
                !TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                double xDouble = poolView.Matrices[poolIndex].m03 + _totalUniverseOffsetDouble.x;
                double zDouble = poolView.Matrices[poolIndex].m23 + _totalUniverseOffsetDouble.z;
                double localX = xDouble - minX;
                double localZ = zDouble - minZ;
                if (localX < 0.0 || localX > width || localZ < 0.0 || localZ > depth)
                    continue;

                int type = poolView.Types[poolIndex];
                float normalizedX = (float)math.saturate(localX * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = (float)math.saturate(localZ * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(poolView.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float3 channel = ResolveDensityChannel(type, densityWeight);
                AddDensityCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddDensityCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
            }
        }

        private void AccumulateChunkThreatAttractorGridFromSlice(
            NativeChunkPool pool,
            int offset,
            int count,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellArea,
            float grassRepresentedArea,
            float kelpRepresentedArea,
            float sargassumRepresentedArea,
            ref NativeArray<float2> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            double inverseWidth = 1.0 / width;
            double inverseDepth = 1.0 / depth;
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset ||
                !TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                double xDouble = poolView.Matrices[poolIndex].m03 + _totalUniverseOffsetDouble.x;
                double zDouble = poolView.Matrices[poolIndex].m23 + _totalUniverseOffsetDouble.z;
                double localX = xDouble - minX;
                double localZ = zDouble - minZ;
                if (localX < 0.0 || localX > width || localZ < 0.0 || localZ > depth)
                    continue;

                float normalizedX = (float)math.saturate(localX * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = (float)math.saturate(localZ * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                int type = poolView.Types[poolIndex];
                int semanticType = poolView.SemanticTypes[poolIndex];
                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(poolView.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float2 channel = ResolveThreatAttractorChannel(semanticType, densityWeight);
                if (math.lengthsq(channel) <= 0.000001f)
                    continue;

                AddThreatAttractorCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
            }
        }

        private static float ResolveRepresentedArea(int type, float grassArea, float kelpArea, float sargassumArea)
        {
            switch ((HectonVegetationInstanceType)type)
            {
                case HectonVegetationInstanceType.Grass:
                    return grassArea;
                case HectonVegetationInstanceType.GiantKelp:
                    return kelpArea;
                case HectonVegetationInstanceType.Sargassum:
                    return sargassumArea;
                default:
                    return grassArea;
            }
        }

        private float ResolveEdgeCompensation(float edgeDistance)
        {
            if (edgeDitherDistance <= 0f || edgeDistance >= edgeDitherDistance)
                return 1f;

            float normalized = Mathf.Clamp01(edgeDistance / Mathf.Max(0.01f, edgeDitherDistance));
            return 1f / Mathf.Max(0.35f, normalized);
        }

        private static float3 ResolveDensityChannel(int type, float densityWeight)
        {
            return VegetationMath.ResolveDensityChannel(type, densityWeight);
        }

        private static float2 ResolveThreatAttractorChannel(int semanticType, float densityWeight)
        {
            return VegetationMath.ResolveThreatAttractorChannel(semanticType, densityWeight);
        }

        private float EvaluateVisibilityModifier(Vector3 position, float3 densityChannels)
        {
            return EvaluateVisibilityModifierStatic(
                position.y,
                densityChannels,
                grassVisibilityWeight,
                kelpVisibilityWeight,
                sargassumVisibilityWeight,
                waterLevel,
                floatingSurfaceOffset,
                sargassumVisibilityBand);
        }

        private float3 ResolveFallbackVisibilityChannels(Vector3 position, HectonVegetationInstanceType type)
        {
            switch (type)
            {
                case HectonVegetationInstanceType.Grass:
                    return new float3(0.18f, 0f, 0f);
                case HectonVegetationInstanceType.GiantKelp:
                    return new float3(0f, 0.24f, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new float3(0f, 0f, 0.28f * EvaluateSargassumVerticalConcealment(position.y));
                default:
                    return float3.zero;
            }
        }

        private float EvaluateSargassumVerticalConcealment(float worldY)
        {
            return EvaluateSargassumVerticalConcealmentStatic(worldY, waterLevel, floatingSurfaceOffset, sargassumVisibilityBand);
        }

        private static float EvaluateVisibilityModifierStatic(
            float worldY,
            float3 densityChannels,
            float grassWeight,
            float kelpWeight,
            float sargassumWeight,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            return VegetationMath.EvaluateVisibilityModifier(
                worldY,
                densityChannels,
                grassWeight,
                kelpWeight,
                sargassumWeight,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
        }

        private static float EvaluateSargassumVerticalConcealmentStatic(
            float worldY,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            return VegetationMath.EvaluateSargassumVerticalConcealment(
                worldY,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
        }

        private static void AddDensityCell(ref NativeArray<float3> destination, int gridOffset, int cellX, int cellZ, float3 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void AddThreatAttractorCell(ref NativeArray<float2> destination, int gridOffset, int cellX, int cellZ, float2 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void ClearDensityGridCells(NativeArray<float3> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float3.zero;
        }

        private static void ClearThreatAttractorGridCells(NativeArray<float2> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float2.zero;
        }

        private static void BlendDensityGrid(
            NativeArray<float3> previous,
            int previousOffset,
            NativeArray<float3> current,
            int currentOffset,
            int count,
            float previousWeight)
        {
            float currentWeight = 1f - previousWeight;
            for (int i = 0; i < count; i++)
                current[currentOffset + i] = (previous[previousOffset + i] * previousWeight) + (current[currentOffset + i] * currentWeight);
        }

        private static float SampleDensityAtPosition(
            float3 position,
            int typeMask,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return VegetationMath.SampleDensityAtPosition(position, typeMask, chunks, densityGrid, chunkCount);
        }

        private static float3 SampleDensityChannelsAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return VegetationMath.SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount);
        }

        /// <summary>
        /// Samples only macro-flora biomass density (kelp plus sargassum) from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleMacroFloraDensityImmediate(Vector3 positionWS)
        {
            return SampleBiomassDensityImmediate(positionWS, DensityTypeMaskKelp | DensityTypeMaskSargassum);
        }

        private static float3 SampleDensityChannelsAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            return VegetationMath.SampleDensityChannelsAtPositionHashed(
                position,
                chunks,
                densityGrid,
                chunkHash,
                gridCenter,
                cellSize,
                gridResolution,
                chunkCount);
        }

        private static float2 SampleThreatAttractorAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            int chunkCount)
        {
            return VegetationMath.SampleThreatAttractorAtPosition(position, chunks, attractorGrid, chunkCount);
        }

        private static float2 SampleThreatAttractorAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            return VegetationMath.SampleThreatAttractorAtPositionHashed(
                position,
                chunks,
                attractorGrid,
                chunkHash,
                gridCenter,
                cellSize,
                gridResolution,
                chunkCount);
        }

        private static float3 SampleChunkDensityChannels(
            float worldX,
            float worldZ,
            VegetationDensityChunkRecord chunk,
            NativeArray<float3> densityGrid)
        {
            return VegetationMath.SampleChunkDensityChannels(worldX, worldZ, chunk, densityGrid);
        }

        private static float ApplyDensityTypeMask(float3 sample, int typeMask)
        {
            return VegetationMath.ApplyDensityTypeMask(sample, typeMask);
        }

        private bool TryBuildDensitySample(
            Vector3 positionWS,
            float3 densityChannels,
            out VegetationDensitySample sample)
        {
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
            {
                sample = default;
                return false;
            }

            if (TryResolveDominantDensitySample(densityChannels, out HectonVegetationInstanceType type, out float density))
            {
                uint seed = ResolveWorldQuerySeed(positionWS);
                VegetationBiomeLayer biomeLayer = ResolveBiomeLayer(positionWS.y, seed);
                sample = new VegetationDensitySample(
                    true,
                    type,
                    ResolveSemanticType(type, biomeLayer, seed),
                    biomeLayer,
                    ResolveAcousticType(type, density),
                    density);
                return true;
            }

            sample = default;
            return false;
        }

        private bool TryResolveDominantDensitySample(
            float3 densityChannels,
            out HectonVegetationInstanceType type,
            out float density)
        {
            density = math.max(densityChannels.x, math.max(densityChannels.y, densityChannels.z));
            if (density <= 0f)
            {
                type = HectonVegetationInstanceType.Grass;
                return false;
            }

            if (densityChannels.z >= densityChannels.x && densityChannels.z >= densityChannels.y)
            {
                type = HectonVegetationInstanceType.Sargassum;
            }
            else if (densityChannels.y >= densityChannels.x)
            {
                type = HectonVegetationInstanceType.GiantKelp;
            }
            else
            {
                type = HectonVegetationInstanceType.Grass;
            }

            return true;
        }

        private static VegetationAcousticType ResolveAcousticType(HectonVegetationInstanceType type, float density)
        {
            if (density <= 0f)
                return VegetationAcousticType.Silence;

            return type == HectonVegetationInstanceType.Sargassum
                ? VegetationAcousticType.SargassumBubbles
                : VegetationAcousticType.VegetationRustle;
        }

        private uint ResolveWorldQuerySeed(Vector3 positionWS)
        {
            if (TryFindTileStateAtPosition(positionWS, out TileRuntimeState state) && state != null)
                return BuildDensityQuerySeed(state.TileX, state.TileZ, positionWS.x, positionWS.z);

            return BuildArbitraryWorldSeed(positionWS.x, positionWS.y, positionWS.z);
        }

        private VegetationBiomeLayer ResolveBiomeLayer(float worldY, uint seed)
        {
            float depth = math.max(0f, waterLevel - worldY);
            float halfBand = math.max(1f, verticalBiomeBlendBand * 0.5f);
            float firstBlendStart = colonyBiomeStartDepth - halfBand;
            float firstBlendEnd = colonyBiomeStartDepth + halfBand;
            if (depth <= firstBlendStart)
                return VegetationBiomeLayer.OrganicShelf;

            if (depth < firstBlendEnd)
            {
                float transition = math.saturate((depth - firstBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0x6E624EB7u) < transition
                    ? VegetationBiomeLayer.ColonyGraveyard
                    : VegetationBiomeLayer.OrganicShelf;
            }

            float secondBlendStart = deadZoneStartDepth - halfBand;
            float secondBlendEnd = deadZoneStartDepth + halfBand;
            if (depth <= secondBlendStart)
                return VegetationBiomeLayer.ColonyGraveyard;

            if (depth < secondBlendEnd)
            {
                float transition = math.saturate((depth - secondBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0xB5297A4Du) < transition
                    ? VegetationBiomeLayer.DeadZone
                    : VegetationBiomeLayer.ColonyGraveyard;
            }

            return VegetationBiomeLayer.DeadZone;
        }

        private static VegetationSemanticType ResolveSemanticType(
            HectonVegetationInstanceType renderType,
            VegetationBiomeLayer biomeLayer,
            uint seed)
        {
            switch (renderType)
            {
                case HectonVegetationInstanceType.Grass:
                    return VegetationSemanticType.OrganicGrass;
                case HectonVegetationInstanceType.Sargassum:
                    return VegetationSemanticType.FloatingSargassum;
                case HectonVegetationInstanceType.GiantKelp:
                    switch (biomeLayer)
                    {
                        case VegetationBiomeLayer.ColonyGraveyard:
                        {
                            float selector = Hash01(seed ^ 0x165667B1u);
                            if (selector < 0.34f)
                                return VegetationSemanticType.ColonyCable;
                            if (selector < 0.67f)
                                return VegetationSemanticType.ColonyHullPlating;

                            return VegetationSemanticType.ColonySupportBeam;
                        }
                        case VegetationBiomeLayer.DeadZone:
                            return VegetationSemanticType.DeadZoneMassiveStructure;
                        default:
                            return VegetationSemanticType.OrganicKelp;
                    }
                default:
                    return VegetationSemanticType.OrganicGrass;
            }
        }

        private void UpdateVegetationAudioHandoff()
        {
            if (playerTransform == null)
            {
                PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: false);
                return;
            }

            float3 averagedChannels = SampleVegetationAudioDensity(playerTransform.position);
            float totalDensity = math.saturate(averagedChannels.x + averagedChannels.y + averagedChannels.z);
            VegetationAcousticType acousticType = VegetationAcousticType.Silence;

            if (TryResolveDominantDensitySample(averagedChannels, out HectonVegetationInstanceType dominantType, out float dominantDensity))
                acousticType = ResolveAcousticType(dominantType, dominantDensity);

            PublishVegetationAudioHandoff(totalDensity, acousticType, force: false);
        }

        private float3 SampleVegetationAudioDensity(Vector3 origin)
        {
            if (!TryReadDensityQuerySnapshot(
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid))
                return float3.zero;

            Vector3 forward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float3 sum = float3.zero;

            sum += SampleDensityChannelsAtPosition(new float3(origin.x, origin.y, origin.z), chunks, densityGrid, _densityQueryChunkCount);
            Vector3 offset = forward * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), chunks, densityGrid, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), chunks, densityGrid, _densityQueryChunkCount);
            offset = right * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), chunks, densityGrid, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), chunks, densityGrid, _densityQueryChunkCount);

            return sum / (float)VegetationAudioProbeCount;
        }

        private void PublishVegetationAudioHandoff(float density, VegetationAcousticType acousticType, bool force)
        {
            _vegetationAudioDensity = Mathf.Clamp01(density);
            _vegetationAudioAcousticType = acousticType;
            GlobalVegetationAudioDensity = _vegetationAudioDensity;
            GlobalVegetationAcousticType = acousticType;
            _pendingVegetationAudioDensity = _vegetationAudioDensity;
            _pendingVegetationAudioAcousticType = acousticType;
            _vegetationAudioHandoffForcePublish |= force;
            _vegetationAudioHandoffPublishRequested = true;
        }

        private void FlushVegetationAudioHandoffVisualSync()
        {
            if (!_vegetationAudioHandoffPublishRequested)
                return;

            _vegetationAudioHandoffPublishRequested = false;
            bool force = _vegetationAudioHandoffForcePublish;
            _vegetationAudioHandoffForcePublish = false;
            float density = _pendingVegetationAudioDensity;
            VegetationAcousticType acousticType = _pendingVegetationAudioAcousticType;

            Shader.SetGlobalFloat(_ShaderVegetationAudioDensityId, density);
            Shader.SetGlobalFloat(_ShaderVegetationAudioAcousticTypeId, (float)acousticType);

            if (!force &&
                Mathf.Abs(_lastPublishedVegetationAudioDensity - density) <= 0.01f &&
                _lastPublishedVegetationAudioAcousticType == acousticType)
            {
                return;
            }

            _lastPublishedVegetationAudioDensity = density;
            _lastPublishedVegetationAudioAcousticType = acousticType;

            if (vegetationAudioMixer == null)
                return;

            if (!string.IsNullOrEmpty(vegetationDensityMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, density);

            if (!string.IsNullOrEmpty(vegetationAcousticTypeMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationAcousticTypeMixerParameter, (float)acousticType);
        }

        private void ClearVegetationAudioHandoff()
        {
            PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: true);
            FlushVegetationAudioHandoffVisualSync();
        }
    }
}
