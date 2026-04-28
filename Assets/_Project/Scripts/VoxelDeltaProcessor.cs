using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Owns carved voxel-cell deltas, save/load projection, and deferred carve batching for runtime voxel volumes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonVoxelEngine))]
    public sealed class VoxelDeltaProcessor : MonoBehaviour, ISaveable, IUpdatable
    {
        private const int ChunkResolution = 32;
        private const int ChunkCellCount = VoxelDeltaChunkDTO.CellCount;
        private const int ChunkDirtyMaskWordCount = VoxelDeltaChunkDTO.DirtyMaskWordCount;
        private const int InitialChunkRegistryCapacity = 256;
        private const int InitialVolumeRegistryCapacity = 16;
        private const int InitialPendingCarveCapacity = 32;
        private const int MortonSignedOffset = 1 << 20;
        private const float MinRuntimeVoxelSize = 0.25f;
        private const float MinCarveRadiusMeters = 0.9f;
        private const float MaxCarveRadiusMeters = 4f;
        private const byte DefaultMaterialId = 0;

        private HectonVoxelEngine _engine;
        private bool _saveRegistered;
        private bool _dispatcherRegistered;

        // COLD ALLOC: Dictionary<ChunkAddress, ChunkDeltaState>[InitialChunkRegistryCapacity] — persistent voxel delta chunk registry — owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, ChunkDeltaState> _chunkStates = new Dictionary<ChunkAddress, ChunkDeltaState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] — live voxel volume registry for load-time rebuild dispatch — owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _registeredVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] — pending volume rebuild queue after loaded delta application — owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _pendingRebuildVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: PendingCarveRequest[InitialPendingCarveCapacity] — deferred plasma-cut carve staging buffer — owner: VoxelDeltaProcessor
        private readonly PendingCarveRequest[] _pendingCarves = new PendingCarveRequest[InitialPendingCarveCapacity];
        private int _pendingCarveCount;

        public int SavePriority => 40;

        public int LoadPriority => 30;

        private void OnEnable()
        {
            _engine = GetComponent<HectonVoxelEngine>();
            SystemDispatcher.EnsureRuntimeInstance();

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = true;
            }

            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_saveRegistered && GlobalRegistry.Save != null)
            {
                GlobalRegistry.Save.Unregister(this);
                _saveRegistered = false;
            }

            _pendingCarveCount = 0;
            _pendingRebuildVolumes.Clear();
            _registeredVolumes.Clear();
            DisposeChunkStates();
        }

        /// <summary>
        /// Flushes staged carve requests and deferred load-time rebuild requests on the registry dispatcher lane.
        /// </summary>
        /// <param name="deltaTime">Unused dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            TryRegisterSaveService();
            FlushPendingCarves();
            FlushPendingRebuilds();
        }

        /// <summary>
        /// Registers a live voxel volume for load-time delta rebuild dispatch.
        /// </summary>
        /// <param name="volume">Runtime volume.</param>
        public void RegisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                if (ReferenceEquals(_registeredVolumes[i], volume))
                    return;
            }

            _registeredVolumes.Add(volume);
            if (HasOverlappingDelta(volume))
                volume.RequestDeltaRebuild();
        }

        /// <summary>
        /// Unregisters a live voxel volume from delta rebuild dispatch.
        /// </summary>
        /// <param name="volume">Runtime volume.</param>
        public void UnregisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            RemoveVolume(_registeredVolumes, volume);
            RemoveVolume(_pendingRebuildVolumes, volume);
        }

        /// <summary>
        /// Stages a plasma-cut carve request for batch processing on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="runtimeHitPoint">Runtime-space hit position.</param>
        /// <param name="damage">Accumulated plasma damage.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void StagePlasmaCut(HectonVoxelVolume volume, Vector3 runtimeHitPoint, float damage, byte materialId = DefaultMaterialId)
        {
            if (volume == null || damage <= 0f || !volume.HasRuntimeData)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeHitPoint);
            float mergeDistance = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            float mergeDistanceSq = mergeDistance * mergeDistance;

            for (int i = 0; i < _pendingCarveCount; i++)
            {
                PendingCarveRequest existing = _pendingCarves[i];
                if (!ReferenceEquals(existing.Volume, volume))
                    continue;

                if ((existing.AbsoluteHitPoint - absoluteHitPoint).sqrMagnitude > mergeDistanceSq)
                    continue;

                existing.AbsoluteHitPoint = Vector3.Lerp(existing.AbsoluteHitPoint, absoluteHitPoint, 0.5f);
                existing.AccumulatedDamage += damage;
                existing.MaterialId = materialId;
                _pendingCarves[i] = existing;
                return;
            }

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                ApplyPendingCarve(_pendingCarves[0]);
                for (int i = 1; i < _pendingCarveCount; i++)
                    _pendingCarves[i - 1] = _pendingCarves[i];

                _pendingCarveCount = _pendingCarves.Length - 1;
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                AccumulatedDamage = damage,
                MaterialId = materialId
            };
        }

        /// <summary>
        /// Applies an explicit crater carve immediately and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="radius">Requested crater radius in meters.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateCrater(HectonVoxelVolume volume, Vector3 runtimeHitPoint, float radius, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeHitPoint);
            ApplyImmediateAbsoluteCrater(volume, absoluteHitPoint, radius, materialId);
        }

        /// <summary>
        /// Applies an explicit crater carve in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteHitPoint">Absolute-universe impact point.</param>
        /// <param name="radius">Requested crater radius in meters.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteCrater(HectonVoxelVolume volume, Vector3 absoluteHitPoint, float radius, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            ApplyCarve(volume, absoluteHitPoint, radius, materialId);
            EnqueueVolumeRebuild(volume);
        }

        /// <summary>
        /// Builds a persistent native delta map for the provided volume bounds.
        /// Caller owns disposal of the returned map.
        /// </summary>
        /// <param name="volume">Target volume.</param>
        /// <param name="modifiedCells">Merged delta map covering the volume bounds.</param>
        /// <returns>True when persistent deltas overlap the target volume.</returns>
        public bool TryBuildDeltaMapForVolume(HectonVoxelVolume volume, out NativeParallelHashMap<int3, half> modifiedCells)
        {
            modifiedCells = default;
            if (volume == null || !volume.HasRuntimeData || _chunkStates.Count == 0)
                return false;

            ResolveVolumeCellBounds(volume, out int3 minCell, out int3 maxCell, out int3 minChunk, out int3 maxChunk);
            int estimatedCount = 0;

            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        ChunkAddress address = new ChunkAddress(new int3(x, y, z), volume.VoxelSize);
                        if (_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            estimatedCount += CountDirtyCells(in state);
                    }
                }
            }

            if (estimatedCount <= 0)
                return false;

            modifiedCells = new NativeParallelHashMap<int3, half>(estimatedCount, Allocator.Persistent);

            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        ChunkAddress address = new ChunkAddress(new int3(x, y, z), volume.VoxelSize);
                        if (!_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            continue;

                        for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)
                        {
                            uint dirtyWord = state.DirtyMaskWords[wordIndex];
                            if (dirtyWord == 0u)
                                continue;

                            int baseIndex = wordIndex << 5;
                            for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                            {
                                uint bitMask = 1u << bitIndex;
                                if ((dirtyWord & bitMask) == 0u)
                                    continue;

                                int flatIndex = baseIndex + bitIndex;
                                int3 cell = AbsoluteCellFromLocalIndex(state.ChunkCoord, flatIndex);
                                if (math.any(cell < minCell) || math.any(cell > maxCell))
                                    continue;

                                modifiedCells.TryAdd(cell, BitsToHalf(state.SdfValueBits[flatIndex]));
                            }
                        }
                    }
                }
            }

            if (modifiedCells.Count() <= 0)
            {
                modifiedCells.Dispose();
                modifiedCells = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copies the current voxel delta snapshot into the save DTO.
        /// </summary>
        /// <param name="data">Target save container.</param>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.voxelDeltaPersistence.EnsureCapacity(_chunkStates.Count);
            data.voxelDeltaPersistence.chunkCount = 0;
            data.voxelDeltaPersistence.totalCellCount = 0;

            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator enumerator = _chunkStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, ChunkDeltaState> pair = enumerator.Current;
                ChunkDeltaState state = pair.Value;
                int cellCount = CountDirtyCells(in state);
                if (cellCount <= 0)
                    continue;

                int chunkIndex = data.voxelDeltaPersistence.chunkCount;
                VoxelDeltaChunkDTO chunkDto = data.voxelDeltaPersistence.chunks[chunkIndex];
                chunkDto.chunkX = pair.Key.ChunkCoord.x;
                chunkDto.chunkY = pair.Key.ChunkCoord.y;
                chunkDto.chunkZ = pair.Key.ChunkCoord.z;
                chunkDto.voxelSize = pair.Key.VoxelSize;
                chunkDto.EnsureCapacity(cellCount);
                chunkDto.cellCount = cellCount;

                for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                    chunkDto.dirtyMaskWords[i] = state.DirtyMaskWords[i];

                for (int i = 0; i < ChunkCellCount; i++)
                {
                    chunkDto.sdfValueBits[i] = state.SdfValueBits[i];
                    chunkDto.materialIds[i] = state.MaterialIds[i];
                }

                chunkDto.cells = Array.Empty<VoxelDeltaCellDTO>();
                data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
                data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
                data.voxelDeltaPersistence.totalCellCount += cellCount;
            }

            for (int i = data.voxelDeltaPersistence.chunkCount; i < data.voxelDeltaPersistence.chunks.Length; i++)
            {
                VoxelDeltaChunkDTO staleChunk = data.voxelDeltaPersistence.chunks[i];
                staleChunk.EnsureCapacity(0);
                data.voxelDeltaPersistence.chunks[i] = staleChunk;
            }
        }

        /// <summary>
        /// Restores voxel delta chunks from the loaded save DTO.
        /// </summary>
        /// <param name="data">Loaded save container.</param>
        public void LoadFromSaveData(SaveData data)
        {
            DisposeChunkStates();
            _pendingRebuildVolumes.Clear();

            if (data == null || data.voxelDeltaPersistence.chunkCount <= 0 || data.voxelDeltaPersistence.chunks == null)
                return;

            int chunkCount = math.min(data.voxelDeltaPersistence.chunkCount, data.voxelDeltaPersistence.chunks.Length);
            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                VoxelDeltaChunkDTO chunk = data.voxelDeltaPersistence.chunks[chunkIndex];
                bool hasDenseStorage = HasDenseStorage(in chunk);
                int denseCellCount = hasDenseStorage ? CountDirtyCells(chunk.dirtyMaskWords) : 0;
                int legacyCellCount = chunk.cells != null
                    ? math.min(chunk.cellCount, chunk.cells.Length)
                    : 0;

                if (denseCellCount <= 0 && legacyCellCount <= 0)
                    continue;

                ChunkDeltaState state = GetOrCreateChunkState(
                    new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ),
                    chunk.voxelSize);

                if (hasDenseStorage && denseCellCount > 0)
                {
                    for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                        state.DirtyMaskWords[i] = chunk.dirtyMaskWords[i];

                    for (int i = 0; i < ChunkCellCount; i++)
                    {
                        state.SdfValueBits[i] = chunk.sdfValueBits[i];
                        state.MaterialIds[i] = chunk.materialIds[i];
                    }
                }
                else
                {
                    for (int cellIndex = 0; cellIndex < legacyCellCount; cellIndex++)
                    {
                        VoxelDeltaCellDTO cell = chunk.cells[cellIndex];
                        int3 absoluteCell = MortonDecodeSigned(cell.universeKey);
                        if (!TryComputeLocalCellIndex(absoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        SetCell(ref state, localIndex, ClampToHalf(cell.sdfValue), cell.materialId, true);
                    }
                }
            }

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }
        }

        public unsafe NativeArray<byte> CaptureNativeSnapshot(Allocator allocator)
        {
            if (_chunkStates.Count <= 0)
                return default;

            int chunkCount = 0;
            int totalDirtyCellCount = 0;
            int bytesPerChunk = UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>()
                + (ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<ushort>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>());
            int totalBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator countEnumerator = _chunkStates.GetEnumerator();
            while (countEnumerator.MoveNext())
            {
                ChunkDeltaState state = countEnumerator.Current.Value;
                int cellCount = CountDirtyCells(in state);
                if (cellCount <= 0)
                    continue;

                chunkCount++;
                totalDirtyCellCount += cellCount;
                totalBytes += bytesPerChunk;
            }

            countEnumerator.Dispose();
            if (chunkCount <= 0)
                return default;

            NativeArray<byte> snapshot = new NativeArray<byte>(totalBytes, allocator, NativeArrayOptions.UninitializedMemory);
            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int cursor = 0;

            NativeSnapshotHeader header = new NativeSnapshotHeader
            {
                ChunkCount = chunkCount,
                TotalDirtyCellCount = totalDirtyCellCount
            };

            UnsafeUtility.CopyStructureToPtr(ref header, snapshotPtr);
            cursor += UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator writeEnumerator = _chunkStates.GetEnumerator();
            while (writeEnumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, ChunkDeltaState> pair = writeEnumerator.Current;
                ChunkDeltaState state = pair.Value;
                int dirtyCellCount = CountDirtyCells(in state);
                if (dirtyCellCount <= 0)
                    continue;

                NativeSnapshotChunkHeader chunkHeader = new NativeSnapshotChunkHeader
                {
                    ChunkX = state.ChunkCoord.x,
                    ChunkY = state.ChunkCoord.y,
                    ChunkZ = state.ChunkCoord.z,
                    VoxelSize = state.VoxelSize,
                    DirtyCellCount = dirtyCellCount
                };

                UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + cursor);
                cursor += UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();

                void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.DirtyMaskWords);
                UnsafeUtility.MemCpy(snapshotPtr + cursor, dirtyMaskPtr, ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>());
                cursor += ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();

                void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.SdfValueBits);
                UnsafeUtility.MemCpy(snapshotPtr + cursor, sdfPtr, ChunkCellCount * UnsafeUtility.SizeOf<ushort>());
                cursor += ChunkCellCount * UnsafeUtility.SizeOf<ushort>();

                void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.MaterialIds);
                UnsafeUtility.MemCpy(snapshotPtr + cursor, materialPtr, ChunkCellCount * UnsafeUtility.SizeOf<byte>());
                cursor += ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            }

            writeEnumerator.Dispose();
            return snapshot;
        }

        public unsafe bool TryLoadNativeSnapshot(NativeArray<byte> snapshot, out string error)
        {
            error = string.Empty;

            DisposeChunkStates();
            _pendingRebuildVolumes.Clear();

            if (!snapshot.IsCreated || snapshot.Length <= 0)
            {
                RequestRebuildsForLoadedState();
                return true;
            }

            int minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
            if (snapshot.Length < minimumHeaderBytes)
            {
                error = "Voxel delta snapshot is truncated.";
                return false;
            }

            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            NativeSnapshotHeader header = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
            if (header.ChunkCount < 0 || header.TotalDirtyCellCount < 0)
            {
                error = "Voxel delta snapshot header is invalid.";
                return false;
            }

            int cursor = minimumHeaderBytes;
            int dirtyMaskByteLength = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
            int sdfByteLength = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
            int materialByteLength = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            int chunkHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();
            int loadedDirtyCellCount = 0;

            for (int chunkIndex = 0; chunkIndex < header.ChunkCount; chunkIndex++)
            {
                if (cursor > snapshot.Length - chunkHeaderBytes)
                {
                    error = "Voxel delta chunk header exceeds the snapshot bounds.";
                    return false;
                }

                NativeSnapshotChunkHeader chunkHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeader>(snapshotPtr + cursor, 0);
                cursor += chunkHeaderBytes;

                if (chunkHeader.VoxelSize <= 0f || chunkHeader.DirtyCellCount < 0)
                {
                    error = "Voxel delta chunk header contains invalid values.";
                    return false;
                }

                loadedDirtyCellCount += chunkHeader.DirtyCellCount;

                int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength;
                if (cursor > snapshot.Length - chunkPayloadBytes)
                {
                    error = "Voxel delta chunk payload exceeds the snapshot bounds.";
                    return false;
                }

                ChunkDeltaState state = GetOrCreateChunkState(new int3(chunkHeader.ChunkX, chunkHeader.ChunkY, chunkHeader.ChunkZ), chunkHeader.VoxelSize);

                void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.DirtyMaskWords);
                UnsafeUtility.MemCpy(dirtyMaskPtr, snapshotPtr + cursor, dirtyMaskByteLength);
                cursor += dirtyMaskByteLength;

                void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.SdfValueBits);
                UnsafeUtility.MemCpy(sdfPtr, snapshotPtr + cursor, sdfByteLength);
                cursor += sdfByteLength;

                void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.MaterialIds);
                UnsafeUtility.MemCpy(materialPtr, snapshotPtr + cursor, materialByteLength);
                cursor += materialByteLength;
            }

            if (cursor != snapshot.Length)
            {
                error = "Voxel delta snapshot contains unread trailing bytes.";
                return false;
            }

            if (loadedDirtyCellCount != header.TotalDirtyCellCount)
            {
                error = "Voxel delta snapshot dirty-cell count does not match the header.";
                return false;
            }

            RequestRebuildsForLoadedState();
            return true;
        }

        private void TryRegisterSaveService()
        {
            if (_saveRegistered || GlobalRegistry.Save == null)
                return;

            GlobalRegistry.Save.Register(this);
            _saveRegistered = true;
        }

        private void FlushPendingCarves()
        {
            for (int i = 0; i < _pendingCarveCount; i++)
                ApplyPendingCarve(_pendingCarves[i]);

            _pendingCarveCount = 0;
        }

        private void FlushPendingRebuilds()
        {
            for (int i = _pendingRebuildVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume volume = _pendingRebuildVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasRuntimeData)
                {
                    _pendingRebuildVolumes.RemoveAt(i);
                    continue;
                }

                volume.RequestDeltaRebuild();
                _pendingRebuildVolumes.RemoveAt(i);
            }
        }

        private void ApplyPendingCarve(PendingCarveRequest request)
        {
            HectonVoxelVolume volume = request.Volume;
            if (volume == null || !volume.HasRuntimeData)
                return;

            float baseRadius = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            float radius = math.clamp(baseRadius + request.AccumulatedDamage * 0.08f, baseRadius, math.max(baseRadius, MaxCarveRadiusMeters));
            ApplyCarve(volume, request.AbsoluteHitPoint, radius, request.MaterialId);
            EnqueueVolumeRebuild(volume);
        }

        private void ApplyCarve(HectonVoxelVolume volume, Vector3 absoluteHitPoint, float radius, byte materialId)
        {
            float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
            float blendRadius = math.max(voxelSize, radius * 0.35f);
            float outerRadius = radius + blendRadius;

            int3 minCell = new int3(
                Mathf.FloorToInt((absoluteHitPoint.x - outerRadius) / voxelSize),
                Mathf.FloorToInt((absoluteHitPoint.y - outerRadius) / voxelSize),
                Mathf.FloorToInt((absoluteHitPoint.z - outerRadius) / voxelSize));
            int3 maxCell = new int3(
                Mathf.FloorToInt((absoluteHitPoint.x + outerRadius) / voxelSize),
                Mathf.FloorToInt((absoluteHitPoint.y + outerRadius) / voxelSize),
                Mathf.FloorToInt((absoluteHitPoint.z + outerRadius) / voxelSize));

            for (int z = minCell.z; z <= maxCell.z; z++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int x = minCell.x; x <= maxCell.x; x++)
                    {
                        int3 absoluteCell = new int3(x, y, z);
                        float3 cellCenter = (new float3(x, y, z) + 0.5f) * voxelSize;
                        float craterDistance = math.distance(cellCenter, (float3)absoluteHitPoint) - radius;
                        if (craterDistance >= blendRadius)
                            continue;

                        int3 chunkCoord = FloorDiv(absoluteCell, ChunkResolution);
                        ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, voxelSize);
                        float clampedValue = math.clamp(craterDistance, -8f, 8f);
                        half newValue = ClampToHalf(clampedValue);
                        if (!TryComputeLocalCellIndex(absoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        SetCell(ref state, localIndex, newValue, materialId, true);
                    }
                }
            }
        }

        private void RequestRebuildsForLoadedState()
        {
            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }
        }

        private bool HasOverlappingDelta(HectonVoxelVolume volume)
        {
            if (volume == null || _chunkStates.Count == 0)
                return false;

            ResolveVolumeCellBounds(volume, out _, out _, out int3 minChunk, out int3 maxChunk);
            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        if (_chunkStates.TryGetValue(new ChunkAddress(new int3(x, y, z), volume.VoxelSize), out ChunkDeltaState state) &&
                            CountDirtyCells(in state) > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private void ResolveVolumeCellBounds(
            HectonVoxelVolume volume,
            out int3 minCell,
            out int3 maxCell,
            out int3 minChunk,
            out int3 maxChunk)
        {
            float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
            float halfExtent = volume.GridDimension * voxelSize * 0.5f;
            Vector3 absoluteCenter = volume.GenerationAbsoluteUniversePosition;
            Vector3 minAbsolute = absoluteCenter - new Vector3(halfExtent, halfExtent, halfExtent);
            Vector3 maxAbsolute = absoluteCenter + new Vector3(halfExtent, halfExtent, halfExtent);

            minCell = new int3(
                Mathf.FloorToInt(minAbsolute.x / voxelSize),
                Mathf.FloorToInt(minAbsolute.y / voxelSize),
                Mathf.FloorToInt(minAbsolute.z / voxelSize));
            maxCell = new int3(
                Mathf.FloorToInt(maxAbsolute.x / voxelSize),
                Mathf.FloorToInt(maxAbsolute.y / voxelSize),
                Mathf.FloorToInt(maxAbsolute.z / voxelSize));
            minChunk = FloorDiv(minCell, ChunkResolution);
            maxChunk = FloorDiv(maxCell, ChunkResolution);
        }

        private void EnqueueVolumeRebuild(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            for (int i = 0; i < _pendingRebuildVolumes.Count; i++)
            {
                if (ReferenceEquals(_pendingRebuildVolumes[i], volume))
                    return;
            }

            _pendingRebuildVolumes.Add(volume);
        }

        private ChunkDeltaState GetOrCreateChunkState(int3 chunkCoord, float voxelSize)
        {
            ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
            if (_chunkStates.TryGetValue(address, out ChunkDeltaState existing))
                return existing;

            _chunkStates.EnsureCapacity(_chunkStates.Count + 1);
            ChunkDeltaState created = new ChunkDeltaState(chunkCoord, voxelSize);
            _chunkStates.Add(address, created);
            return created;
        }

        private static int CountDirtyCells(in ChunkDeltaState state)
        {
            if (!state.DirtyMaskWords.IsCreated)
                return 0;

            int dirtyCount = 0;
            for (int i = 0; i < state.DirtyMaskWords.Length; i++)
                dirtyCount += math.countbits(state.DirtyMaskWords[i]);

            return dirtyCount;
        }

        private static int CountDirtyCells(uint[] dirtyMaskWords)
        {
            if (dirtyMaskWords == null)
                return 0;

            int dirtyCount = 0;
            int wordCount = math.min(dirtyMaskWords.Length, ChunkDirtyMaskWordCount);
            for (int i = 0; i < wordCount; i++)
                dirtyCount += math.countbits(dirtyMaskWords[i]);

            return dirtyCount;
        }

        private static bool HasDenseStorage(in VoxelDeltaChunkDTO chunk)
        {
            return chunk.dirtyMaskWords != null &&
                   chunk.dirtyMaskWords.Length == ChunkDirtyMaskWordCount &&
                   chunk.sdfValueBits != null &&
                   chunk.sdfValueBits.Length == ChunkCellCount &&
                   chunk.materialIds != null &&
                   chunk.materialIds.Length == ChunkCellCount;
        }

        private static bool TryComputeLocalCellIndex(int3 absoluteCell, int3 chunkCoord, out uint localIndex)
        {
            int3 localCell = absoluteCell - (chunkCoord * ChunkResolution);
            if (localCell.x < 0 || localCell.x >= ChunkResolution ||
                localCell.y < 0 || localCell.y >= ChunkResolution ||
                localCell.z < 0 || localCell.z >= ChunkResolution)
            {
                localIndex = 0u;
                return false;
            }

            localIndex = (uint)(localCell.x | (localCell.y << 5) | (localCell.z << 10));
            return true;
        }

        private static int3 AbsoluteCellFromLocalIndex(int3 chunkCoord, int flatIndex)
        {
            int localX = flatIndex & (ChunkResolution - 1);
            int localY = (flatIndex >> 5) & (ChunkResolution - 1);
            int localZ = flatIndex >> 10;
            return (chunkCoord * ChunkResolution) + new int3(localX, localY, localZ);
        }

        private static bool IsDirty(in ChunkDeltaState state, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            return (state.DirtyMaskWords[wordIndex] & bitMask) != 0u;
        }

        private static void SetDirtyBit(ref ChunkDeltaState state, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            state.DirtyMaskWords[wordIndex] |= bitMask;
        }

        private static void SetCell(ref ChunkDeltaState state, uint localIndex, half value, byte materialId, bool preserveMinimum)
        {
            int flatIndex = (int)localIndex;
            bool isDirty = IsDirty(in state, localIndex);
            if (!isDirty)
            {
                SetDirtyBit(ref state, localIndex);
                state.SdfValueBits[flatIndex] = HalfToBits(value);
            }
            else if (!preserveMinimum || (float)BitsToHalf(state.SdfValueBits[flatIndex]) > (float)value)
            {
                state.SdfValueBits[flatIndex] = HalfToBits(value);
            }

            state.MaterialIds[flatIndex] = materialId;
        }

        private static ushort HalfToBits(half value)
        {
            return UnsafeUtility.As<half, ushort>(ref value);
        }

        private static half BitsToHalf(ushort bits)
        {
            return UnsafeUtility.As<ushort, half>(ref bits);
        }

        private void DisposeChunkStates()
        {
            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator enumerator = _chunkStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.Dispose();

            _chunkStates.Clear();
        }

        private static void RemoveVolume(List<HectonVoxelVolume> list, HectonVoxelVolume volume)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], volume))
                    continue;

                int last = list.Count - 1;
                list[i] = list[last];
                list.RemoveAt(last);
                break;
            }
        }

        private static half ClampToHalf(float value)
        {
            return (half)math.clamp(value, -8f, 8f);
        }

        private static int3 FloorDiv(int3 value, int divisor)
        {
            return new int3(FloorDiv(value.x, divisor), FloorDiv(value.y, divisor), FloorDiv(value.z, divisor));
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder < 0) ^ (divisor < 0)))
                quotient--;

            return quotient;
        }

        private static ulong MortonEncodeSigned(int x, int y, int z)
        {
            ulong ux = (uint)(x + MortonSignedOffset);
            ulong uy = (uint)(y + MortonSignedOffset);
            ulong uz = (uint)(z + MortonSignedOffset);
            return ExpandBits(ux) | (ExpandBits(uy) << 1) | (ExpandBits(uz) << 2);
        }

        private static int3 MortonDecodeSigned(ulong morton)
        {
            int x = (int)CompactBits(morton) - MortonSignedOffset;
            int y = (int)CompactBits(morton >> 1) - MortonSignedOffset;
            int z = (int)CompactBits(morton >> 2) - MortonSignedOffset;
            return new int3(x, y, z);
        }

        private static ulong ExpandBits(ulong value)
        {
            value = (value | (value << 32)) & 0x001F00000000FFFFUL;
            value = (value | (value << 16)) & 0x001F0000FF0000FFUL;
            value = (value | (value << 8)) & 0x100F00F00F00F00FUL;
            value = (value | (value << 4)) & 0x10C30C30C30C30C3UL;
            value = (value | (value << 2)) & 0x1249249249249249UL;
            return value;
        }

        private static ulong CompactBits(ulong value)
        {
            value &= 0x1249249249249249UL;
            value = (value ^ (value >> 2)) & 0x10C30C30C30C30C3UL;
            value = (value ^ (value >> 4)) & 0x100F00F00F00F00FUL;
            value = (value ^ (value >> 8)) & 0x001F0000FF0000FFUL;
            value = (value ^ (value >> 16)) & 0x001F00000000FFFFUL;
            value = (value ^ (value >> 32)) & 0x1FFFFFUL;
            return value;
        }

        private struct PendingCarveRequest
        {
            public HectonVoxelVolume Volume;
            public Vector3 AbsoluteHitPoint;
            public float AccumulatedDamage;
            public byte MaterialId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeSnapshotHeader
        {
            public int ChunkCount;
            public int TotalDirtyCellCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeSnapshotChunkHeader
        {
            public int ChunkX;
            public int ChunkY;
            public int ChunkZ;
            public float VoxelSize;
            public int DirtyCellCount;
        }

        private readonly struct ChunkAddress : IEquatable<ChunkAddress>
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            private readonly int _voxelSizeBits;

            public ChunkAddress(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                _voxelSizeBits = math.asint(voxelSize);
            }

            public bool Equals(ChunkAddress other)
            {
                return ChunkCoord.Equals(other.ChunkCoord) && _voxelSizeBits == other._voxelSizeBits;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkAddress other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked((ChunkCoord.GetHashCode() * 397) ^ _voxelSizeBits);
            }
        }

        private struct ChunkDeltaState : IDisposable
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            public NativeArray<uint> DirtyMaskWords;
            public NativeArray<ushort> SdfValueBits;
            public NativeArray<byte> MaterialIds;

            public ChunkDeltaState(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                DirtyMaskWords = new NativeArray<uint>(ChunkDirtyMaskWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                SdfValueBits = new NativeArray<ushort>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                MaterialIds = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            public void Dispose()
            {
                if (DirtyMaskWords.IsCreated)
                    DirtyMaskWords.Dispose();

                if (SdfValueBits.IsCreated)
                    SdfValueBits.Dispose();

                if (MaterialIds.IsCreated)
                    MaterialIds.Dispose();
            }
        }
    }
}
