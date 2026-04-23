using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        private const int InitialChunkRegistryCapacity = 256;
        private const int InitialVolumeRegistryCapacity = 16;
        private const int InitialPendingCarveCapacity = 32;
        private const int InitialChunkCellCapacity = 256;
        private const int ChunkByteBudget = 32 * 1024 * 1024;
        private const int ApproxDeltaBytesPerCell = 14;
        private const int MaxCellsPerChunk = ChunkByteBudget / ApproxDeltaBytesPerCell;
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

            TryRegisterSaveManager();
        }

        private void OnDisable()
        {
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_saveRegistered && SaveManager.Instance != null)
            {
                SaveManager.Instance.Unregister(this);
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
            TryRegisterSaveManager();
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
                            estimatedCount += state.ModifiedCells.Count();
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

                        NativeParallelHashMap<int3, half>.Enumerator enumerator = state.ModifiedCells.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;
                            int3 cell = current.Key;
                            if (math.any(cell < minCell) || math.any(cell > maxCell))
                                continue;

                            modifiedCells.TryAdd(cell, current.Value);
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
                int cellCount = state.ModifiedCells.Count();
                if (cellCount <= 0)
                    continue;

                NativeArray<SaveBinaryStorage.DeltaCell> packedCells =
                    new NativeArray<SaveBinaryStorage.DeltaCell>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                try
                {
                    JobHandle packHandle = new PackDeltaCellsJob
                    {
                        ModifiedCells = state.ModifiedCells,
                        ModifiedMaterials = state.ModifiedMaterials,
                        PackedCells = packedCells
                    }.Schedule();

                    packHandle.Complete();

                    int chunkIndex = data.voxelDeltaPersistence.chunkCount;
                    VoxelDeltaChunkDTO chunkDto = data.voxelDeltaPersistence.chunks[chunkIndex];
                    chunkDto.chunkX = pair.Key.ChunkCoord.x;
                    chunkDto.chunkY = pair.Key.ChunkCoord.y;
                    chunkDto.chunkZ = pair.Key.ChunkCoord.z;
                    chunkDto.voxelSize = pair.Key.VoxelSize;
                    chunkDto.EnsureCapacity(cellCount);
                    chunkDto.cellCount = cellCount;

                    for (int i = 0; i < cellCount; i++)
                    {
                        SaveBinaryStorage.DeltaCell packedCell = packedCells[i];
                        chunkDto.cells[i] = new VoxelDeltaCellDTO
                        {
                            universeKey = packedCell.UniverseKey,
                            sdfValue = packedCell.SdfValue,
                            materialId = packedCell.MaterialId,
                            flags = packedCell.Flags,
                            metadata = packedCell.Metadata,
                            reserved = packedCell.Reserved
                        };
                    }

                    data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
                    data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
                    data.voxelDeltaPersistence.totalCellCount += cellCount;
                }
                finally
                {
                    if (packedCells.IsCreated)
                        packedCells.Dispose();
                }
            }

            for (int i = data.voxelDeltaPersistence.chunkCount; i < data.voxelDeltaPersistence.chunks.Length; i++)
            {
                VoxelDeltaChunkDTO staleChunk = data.voxelDeltaPersistence.chunks[i];
                staleChunk.cellCount = 0;
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
                int cellCount = chunk.cells != null ? math.min(chunk.cellCount, chunk.cells.Length) : 0;
                if (cellCount <= 0)
                    continue;

                ChunkDeltaState state = GetOrCreateChunkState(
                    new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ),
                    chunk.voxelSize);

                EnsureChunkCapacity(state, state.ModifiedCells.Count() + cellCount);

                for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
                {
                    VoxelDeltaCellDTO cell = chunk.cells[cellIndex];
                    int3 absoluteCell = MortonDecodeSigned(cell.universeKey);
                    state.ModifiedCells[absoluteCell] = ClampToHalf(cell.sdfValue);
                    state.ModifiedMaterials[absoluteCell] = cell.materialId;
                }
            }

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }
        }

        private void TryRegisterSaveManager()
        {
            if (_saveRegistered || SaveManager.Instance == null)
                return;

            SaveManager.Instance.Register(this);
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
                        if (state.ModifiedCells.Count() >= MaxCellsPerChunk)
                            continue;

                        float clampedValue = math.clamp(craterDistance, -8f, 8f);
                        half newValue = ClampToHalf(clampedValue);

                        if (state.ModifiedCells.TryGetValue(absoluteCell, out half existingValue))
                        {
                            if ((float)existingValue > clampedValue)
                                state.ModifiedCells[absoluteCell] = newValue;
                        }
                        else
                        {
                            EnsureChunkCapacity(state, state.ModifiedCells.Count() + 1);
                            state.ModifiedCells.TryAdd(absoluteCell, newValue);
                        }

                        if (state.ModifiedMaterials.TryGetValue(absoluteCell, out _))
                        {
                            state.ModifiedMaterials[absoluteCell] = materialId;
                        }
                        else
                        {
                            EnsureChunkCapacity(state, state.ModifiedMaterials.Count() + 1);
                            state.ModifiedMaterials.TryAdd(absoluteCell, materialId);
                        }
                    }
                }
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
                        if (_chunkStates.ContainsKey(new ChunkAddress(new int3(x, y, z), volume.VoxelSize)))
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
            ChunkDeltaState created = new ChunkDeltaState(chunkCoord, voxelSize, InitialChunkCellCapacity);
            _chunkStates.Add(address, created);
            return created;
        }

        private static void EnsureChunkCapacity(ChunkDeltaState state, int requiredCount)
        {
            if (!state.ModifiedCells.IsCreated || !state.ModifiedMaterials.IsCreated)
                return;

            int newCapacity = state.ModifiedCells.Capacity;
            if (requiredCount <= newCapacity)
                return;

            while (newCapacity < requiredCount)
                newCapacity <<= 1;

            state.ModifiedCells.Capacity = newCapacity;
            state.ModifiedMaterials.Capacity = newCapacity;
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
            public NativeParallelHashMap<int3, half> ModifiedCells;
            public NativeParallelHashMap<int3, byte> ModifiedMaterials;

            public ChunkDeltaState(int3 chunkCoord, float voxelSize, int capacity)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                ModifiedCells = new NativeParallelHashMap<int3, half>(capacity, Allocator.Persistent);
                ModifiedMaterials = new NativeParallelHashMap<int3, byte>(capacity, Allocator.Persistent);
            }

            public void Dispose()
            {
                if (ModifiedCells.IsCreated)
                    ModifiedCells.Dispose();

                if (ModifiedMaterials.IsCreated)
                    ModifiedMaterials.Dispose();
            }
        }

        [BurstCompile]
        private struct PackDeltaCellsJob : IJob
        {
            [ReadOnly] public NativeParallelHashMap<int3, half> ModifiedCells;
            [ReadOnly] public NativeParallelHashMap<int3, byte> ModifiedMaterials;
            [WriteOnly] public NativeArray<SaveBinaryStorage.DeltaCell> PackedCells;

            public void Execute()
            {
                int index = 0;
                NativeParallelHashMap<int3, half>.Enumerator enumerator = ModifiedCells.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    byte materialId = 0;
                    if (ModifiedMaterials.IsCreated)
                        ModifiedMaterials.TryGetValue(current.Key, out materialId);

                    PackedCells[index++] = new SaveBinaryStorage.DeltaCell
                    {
                        UniverseKey = MortonEncodeSigned(current.Key.x, current.Key.y, current.Key.z),
                        SdfValue = (float)current.Value,
                        MaterialId = materialId,
                        Flags = 0,
                        Metadata = 0,
                        Reserved = 0u
                    };
                }
            }
        }
    }
}
