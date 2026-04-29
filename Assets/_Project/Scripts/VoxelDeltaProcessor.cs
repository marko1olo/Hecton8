using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Caves
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelModifiedCell
    {
        public half Density;
        public byte MaterialId;
        public byte Flags;
        public ushort Reserved;
    }

    /// <summary>
    /// Owns carved voxel-cell deltas, save/load projection, and deferred carve batching for runtime voxel volumes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonVoxelEngine))]
    public sealed class VoxelDeltaProcessor : MonoBehaviour, ISaveable, IUpdatable, ILateFrameTickable
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
        private const float SphereVolumeFactor = 4f / 3f * math.PI;
        private const byte DefaultMaterialId = 0;
        private const byte DeltaModeAdditive = 1 << 0;
        private const int NativeSnapshotMagic = unchecked((int)0x48584432);
        private static readonly ProfilerMarker _carveScheduleProfilerMarker = new ProfilerMarker("H8.VoxelDelta.ScheduleCarve");
        private static readonly ProfilerMarker _carveCommitProfilerMarker = new ProfilerMarker("H8.VoxelDelta.CommitCarve");

        [Header("Debris Aftermath")]
        [Tooltip("Optional dropped-item payload spawned from carved voxel mass. Leave empty to disable persistent debris aftermath.")]
        [SerializeField] private ItemData carveDebrisItem;
        [Tooltip("Debris entities spawned per cubic meter of removed sphere volume.")]
        [SerializeField, Min(0f)] private float carveDebrisPerCubicMeter = 0.3f;
        [Tooltip("Upper bound on debris entities emitted from a single carve commit.")]
        [SerializeField, Range(0, 16)] private int carveDebrisMaxCount = 8;
        [Tooltip("Impulse magnitude applied to each debris entity when the carve aftermath hydrates nearby.")]
        [SerializeField, Min(0f)] private float carveDebrisImpulse = 2.5f;

        private HectonVoxelEngine _engine;
        private bool _saveRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;

        // COLD ALLOC: Dictionary<ChunkAddress, ChunkDeltaState>[InitialChunkRegistryCapacity] â€” persistent voxel delta chunk registry â€” owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, ChunkDeltaState> _chunkStates = new Dictionary<ChunkAddress, ChunkDeltaState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] â€” live voxel volume registry for load-time rebuild dispatch â€” owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _registeredVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] â€” pending volume rebuild queue after loaded delta application â€” owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _pendingRebuildVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: PendingCarveRequest[InitialPendingCarveCapacity] â€” deferred plasma-cut carve staging buffer â€” owner: VoxelDeltaProcessor
        private readonly PendingCarveRequest[] _pendingCarves = new PendingCarveRequest[InitialPendingCarveCapacity];
        private int _pendingCarveCount;
        private JobHandle _scheduledCarveHandle;
        private bool _scheduledCarveRunning;
        private PendingCarveRequest _scheduledCarveRequest;
        // COLD ALLOC: NativeArray<CarveCellWrite>[capacity] â€” staged Burst carve results before managed delta-chunk commit â€” owner: VoxelDeltaProcessor
        private NativeArray<CarveCellWrite> _scheduledCarveWrites;

        public int SavePriority => 40;

        public int LoadPriority => 30;

        private void OnEnable()
        {
            _engine = GetComponent<HectonVoxelEngine>();

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                TryRegisterSaveService();
                return;
            }

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = true;
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = true;
            }

            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            DisposeScheduledCarveBuffers();
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
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
            TrySchedulePendingCarve();
            FlushPendingRebuilds();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryCommitScheduledCarve();
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
                if (!_scheduledCarveRunning)
                    TrySchedulePendingCarve();

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

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!_scheduledCarveRunning)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                ExplicitRadiusMeters = radius,
                MaterialId = materialId,
                DeltaFlags = 0
            };
        }

        /// <summary>
        /// Applies an explicit additive weld stamp in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteHitPoint">Absolute-universe impact point.</param>
        /// <param name="radius">Requested weld radius in meters.</param>
        /// <param name="strength">Smooth-union strength scalar.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteWeld(HectonVoxelVolume volume, Vector3 absoluteHitPoint, float radius, float strength, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!_scheduledCarveRunning)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                ExplicitRadiusMeters = radius,
                ExplicitBlendStrength = math.max(volume.VoxelSize, strength),
                MaterialId = materialId,
                DeltaFlags = DeltaModeAdditive
            };
        }

        /// <summary>
        /// Builds a persistent native delta map for the provided volume bounds.
        /// Caller owns disposal of the returned map.
        /// </summary>
        /// <param name="volume">Target volume.</param>
        /// <param name="modifiedCells">Merged delta map covering the volume bounds.</param>
        /// <returns>True when persistent deltas overlap the target volume.</returns>
        public bool TryBuildDeltaMapForVolume(HectonVoxelVolume volume, out NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells)
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

            modifiedCells = new NativeParallelHashMap<int3, VoxelModifiedCell>(estimatedCount, Allocator.Persistent);

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

                                modifiedCells.TryAdd(cell, new VoxelModifiedCell
                                {
                                    Density = BitsToHalf(state.SdfValueBits[flatIndex]),
                                    MaterialId = state.MaterialIds[flatIndex],
                                    Flags = state.CellFlags[flatIndex]
                                });
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
                    chunkDto.cellFlags[i] = state.CellFlags[i];
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
                        state.CellFlags[i] = chunk.cellFlags != null && chunk.cellFlags.Length == ChunkCellCount
                            ? chunk.cellFlags[i]
                            : (byte)0;
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

                        SetCell(ref state, localIndex, ClampToHalf(cell.sdfValue), cell.materialId, cell.flags);
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
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>())
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
                Version = NativeSnapshotMagic,
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

                void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                UnsafeUtility.MemCpy(snapshotPtr + cursor, flagsPtr, ChunkCellCount * UnsafeUtility.SizeOf<byte>());
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

            int legacyHeaderBytes = UnsafeUtility.SizeOf<LegacyNativeSnapshotHeader>();
            if (snapshot.Length < legacyHeaderBytes)
            {
                error = "Voxel delta snapshot is truncated.";
                return false;
            }

            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int minimumHeaderBytes;
            bool snapshotHasFlags;
            NativeSnapshotHeader header;

            if (snapshot.Length >= UnsafeUtility.SizeOf<NativeSnapshotHeader>())
            {
                NativeSnapshotHeader versionedHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
                if (versionedHeader.Version == NativeSnapshotMagic)
                {
                    header = versionedHeader;
                    minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
                    snapshotHasFlags = true;
                }
                else
                {
                    LegacyNativeSnapshotHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacyNativeSnapshotHeader>(snapshotPtr, 0);
                    header = new NativeSnapshotHeader
                    {
                        Version = 1,
                        ChunkCount = legacyHeader.ChunkCount,
                        TotalDirtyCellCount = legacyHeader.TotalDirtyCellCount
                    };
                    minimumHeaderBytes = legacyHeaderBytes;
                    snapshotHasFlags = false;
                }
            }
            else
            {
                LegacyNativeSnapshotHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacyNativeSnapshotHeader>(snapshotPtr, 0);
                header = new NativeSnapshotHeader
                {
                    Version = 1,
                    ChunkCount = legacyHeader.ChunkCount,
                    TotalDirtyCellCount = legacyHeader.TotalDirtyCellCount
                };
                minimumHeaderBytes = legacyHeaderBytes;
                snapshotHasFlags = false;
            }

            if (header.ChunkCount < 0 || header.TotalDirtyCellCount < 0)
            {
                error = "Voxel delta snapshot header is invalid.";
                return false;
            }

            int cursor = minimumHeaderBytes;
            int dirtyMaskByteLength = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
            int sdfByteLength = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
            int materialByteLength = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            int flagsByteLength = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
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

                int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength + (snapshotHasFlags ? flagsByteLength : 0);
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

                if (snapshotHasFlags)
                {
                    void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                    UnsafeUtility.MemCpy(flagsPtr, snapshotPtr + cursor, flagsByteLength);
                    cursor += flagsByteLength;
                }
                else
                {
                    void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                    UnsafeUtility.MemClear(flagsPtr, flagsByteLength);
                }
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

        private void TrySchedulePendingCarve()
        {
            if (_scheduledCarveRunning || _pendingCarveCount <= 0)
                return;

            PendingCarveRequest request = _pendingCarves[0];
            for (int i = 1; i < _pendingCarveCount; i++)
                _pendingCarves[i - 1] = _pendingCarves[i];

            _pendingCarveCount--;
            HectonVoxelVolume volume = request.Volume;
            if (volume == null || !volume.HasRuntimeData)
                return;

            float radius = ResolveCarveRadius(in request, volume);
            if (radius <= 0f)
                return;

            float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
            float blendRadius = math.max(voxelSize, radius * 0.35f);
            float outerRadius = radius + blendRadius;
            int3 minCell = new int3(
                Mathf.FloorToInt((request.AbsoluteHitPoint.x - outerRadius) / voxelSize),
                Mathf.FloorToInt((request.AbsoluteHitPoint.y - outerRadius) / voxelSize),
                Mathf.FloorToInt((request.AbsoluteHitPoint.z - outerRadius) / voxelSize));
            int3 maxCell = new int3(
                Mathf.FloorToInt((request.AbsoluteHitPoint.x + outerRadius) / voxelSize),
                Mathf.FloorToInt((request.AbsoluteHitPoint.y + outerRadius) / voxelSize),
                Mathf.FloorToInt((request.AbsoluteHitPoint.z + outerRadius) / voxelSize));

            int3 span = (maxCell - minCell) + 1;
            int candidateCount = math.max(0, span.x) * math.max(0, span.y) * math.max(0, span.z);
            if (candidateCount <= 0)
                return;

            EnsureScheduledCarveWriteCapacity(candidateCount);
            _scheduledCarveRequest = request;

            CarveSdfJob carveJob = new CarveSdfJob
            {
                MinCell = minCell,
                Span = span,
                VoxelSize = voxelSize,
                Radius = radius,
                BlendRadius = blendRadius,
                BlendStrength = ResolveBlendStrength(in request, voxelSize),
                Center = new float3(request.AbsoluteHitPoint.x, request.AbsoluteHitPoint.y, request.AbsoluteHitPoint.z),
                MaterialId = request.MaterialId,
                DeltaFlags = request.DeltaFlags,
                Writes = _scheduledCarveWrites
            };

            using (_carveScheduleProfilerMarker.Auto())
            {
                _scheduledCarveHandle = carveJob.Schedule(candidateCount, 64);
                _scheduledCarveRunning = true;
            }
        }

        private void TryCommitScheduledCarve()
        {
            if (!_scheduledCarveRunning || !_scheduledCarveHandle.IsCompleted)
                return;

            using (_carveCommitProfilerMarker.Auto())
            {
                _scheduledCarveHandle.Complete();
                _scheduledCarveHandle = default;
                _scheduledCarveRunning = false;

                HectonVoxelVolume volume = _scheduledCarveRequest.Volume;
                if (volume == null || !volume.HasRuntimeData)
                    return;

                float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
                for (int i = 0; i < _scheduledCarveWrites.Length; i++)
                {
                    CarveCellWrite write = _scheduledCarveWrites[i];
                    if (write.IsActive == 0)
                        continue;

                    int3 chunkCoord = FloorDiv(write.AbsoluteCell, ChunkResolution);
                    ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, voxelSize);
                    if (!TryComputeLocalCellIndex(write.AbsoluteCell, state.ChunkCoord, out uint localIndex))
                        continue;

                    half resolvedValue = BitsToHalf(write.SdfValueBits);
                    if ((write.DeltaFlags & DeltaModeAdditive) != 0)
                    {
                        float currentDensity;
                        if (!TryResolveCurrentCellDensity(volume, in state, localIndex, write.AbsoluteCell, voxelSize, out currentDensity))
                            currentDensity = 0f;

                        resolvedValue = ClampToHalf(SmoothMaxExp(currentDensity, (float)resolvedValue, math.max(voxelSize, write.BlendStrength)));
                    }

                    SetCell(ref state, localIndex, resolvedValue, write.MaterialId, write.DeltaFlags);
                }

                EnqueueVolumeRebuild(volume);
                if ((_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0)
                    EmitCarveDebris(in _scheduledCarveRequest, ResolveCarveRadius(in _scheduledCarveRequest, volume));
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

        private static float ResolveBlendStrength(in PendingCarveRequest request, float voxelSize)
        {
            return request.ExplicitBlendStrength > 0f
                ? math.max(voxelSize, request.ExplicitBlendStrength)
                : math.max(voxelSize, request.ExplicitRadiusMeters * 0.35f);
        }

        private static bool TryResolveCurrentCellDensity(
            HectonVoxelVolume volume,
            in ChunkDeltaState state,
            uint localIndex,
            int3 absoluteCell,
            float voxelSize,
            out float density)
        {
            if (IsDirty(in state, localIndex))
            {
                density = (float)BitsToHalf(state.SdfValueBits[(int)localIndex]);
                return true;
            }

            if (volume != null)
            {
                Vector3 absoluteCellCenter = new Vector3(
                    (absoluteCell.x + 0.5f) * voxelSize,
                    (absoluteCell.y + 0.5f) * voxelSize,
                    (absoluteCell.z + 0.5f) * voxelSize);
                Vector3 runtimeCellCenter = HectonFloatingOrigin.ToRuntimePosition(absoluteCellCenter);
                if (volume.TrySampleDensity(runtimeCellCenter, out density))
                    return true;
            }

            density = 0f;
            return false;
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

        private static void SetCell(ref ChunkDeltaState state, uint localIndex, half value, byte materialId, byte cellFlags)
        {
            int flatIndex = (int)localIndex;
            bool isDirty = IsDirty(in state, localIndex);
            if (!isDirty)
            {
                SetDirtyBit(ref state, localIndex);
                state.SdfValueBits[flatIndex] = HalfToBits(value);
                state.CellFlags[flatIndex] = cellFlags;
            }
            else
            {
                byte existingFlags = state.CellFlags[flatIndex];
                bool additive = (cellFlags & DeltaModeAdditive) != 0;
                bool existingAdditive = (existingFlags & DeltaModeAdditive) != 0;
                float existingValue = (float)BitsToHalf(state.SdfValueBits[flatIndex]);
                float nextValue = (float)value;

                if (additive == existingAdditive)
                {
                    float mergedValue = additive
                        ? math.max(existingValue, nextValue)
                        : math.min(existingValue, nextValue);
                    state.SdfValueBits[flatIndex] = HalfToBits(ClampToHalf(mergedValue));
                }
                else
                {
                    state.SdfValueBits[flatIndex] = HalfToBits(value);
                    state.CellFlags[flatIndex] = cellFlags;
                }
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

        private static float SmoothMaxExp(float a, float b, float k)
        {
            k = math.max(k, 0.0001f);
            float maxValue = math.max(a, b);
            float expA = math.exp(-math.clamp(k * (maxValue - a), 0f, 60f));
            float expB = math.exp(-math.clamp(k * (maxValue - b), 0f, 60f));
            return maxValue + math.log(expA + expB) / k;
        }

        private float ResolveCarveRadius(in PendingCarveRequest request, HectonVoxelVolume volume)
        {
            if (request.ExplicitRadiusMeters > 0f)
                return math.max(math.max(volume.VoxelSize * 1.25f, MinCarveRadiusMeters), request.ExplicitRadiusMeters);

            float baseRadius = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            return math.clamp(baseRadius + request.AccumulatedDamage * 0.08f, baseRadius, math.max(baseRadius, MaxCarveRadiusMeters));
        }

        private void EmitCarveDebris(in PendingCarveRequest request, float radius)
        {
            if (carveDebrisItem == null || carveDebrisPerCubicMeter <= 0f || carveDebrisMaxCount <= 0 || radius <= 0f)
                return;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null)
                return;

            float removedVolume = SphereVolumeFactor * radius * radius * radius;
            int spawnCount = math.clamp((int)math.round(removedVolume * carveDebrisPerCubicMeter), 0, carveDebrisMaxCount);
            if (spawnCount <= 0)
                return;

            uint state = (uint)math.hash(new int4(
                (int)math.round(request.AbsoluteHitPoint.x * 10f),
                (int)math.round(request.AbsoluteHitPoint.y * 10f),
                (int)math.round(request.AbsoluteHitPoint.z * 10f),
                math.max(1, (int)math.round(radius * 100f))));

            float spawnRadius = math.max(radius * 0.35f, MinRuntimeVoxelSize);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 direction = NextBurstDirection(ref state);
                float distance01 = NextBurst01(ref state);
                float impulse01 = NextBurst01(ref state);
                Vector3 absoluteSpawnPosition = request.AbsoluteHitPoint + new Vector3(direction.x, direction.y, direction.z) * (spawnRadius * distance01);
                Vector3 runtimeSpawnPosition = HectonFloatingOrigin.ToRuntimePosition(absoluteSpawnPosition);
                Vector3 burstImpulse = new Vector3(direction.x, direction.y, direction.z) * math.lerp(carveDebrisImpulse * 0.55f, carveDebrisImpulse, impulse01);
                Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(runtimeSpawnPosition);
                float3 currentImpulse3 = new float3(sampledCurrent.x, sampledCurrent.y, sampledCurrent.z) * math.max(0.25f, carveDebrisImpulse * 0.35f);
                Vector3 currentImpulse = math.all(math.isfinite(currentImpulse3))
                    ? new Vector3(currentImpulse3.x, currentImpulse3.y, currentImpulse3.z)
                    : Vector3.zero;
                Vector3 initialImpulse = burstImpulse + currentImpulse;
                registry.TryRegisterDroppedItem(carveDebrisItem, 1, runtimeSpawnPosition, initialImpulse);
            }
        }

        private static float NextBurst01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float3 NextBurstDirection(ref uint state)
        {
            float z = math.lerp(-1f, 1f, NextBurst01(ref state));
            float angle = NextBurst01(ref state) * (math.PI * 2f);
            float radial = math.sqrt(math.max(0f, 1f - (z * z)));
            return new float3(radial * math.cos(angle), z, radial * math.sin(angle));
        }

        private void EnsureScheduledCarveWriteCapacity(int requiredCount)
        {
            if (_scheduledCarveWrites.IsCreated && _scheduledCarveWrites.Length >= requiredCount)
                return;

            if (_scheduledCarveWrites.IsCreated)
            {
                _scheduledCarveWrites.Dispose();
                _scheduledCarveWrites = default;
            }

            // COLD ALLOC: NativeArray<CarveCellWrite>[requiredCount] â€” staged carve-write buffer for deferred voxel SDF mutation commits â€” owner: VoxelDeltaProcessor
            _scheduledCarveWrites = new NativeArray<CarveCellWrite>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void DisposeScheduledCarveBuffers()
        {
            JobHandle dependency = _scheduledCarveRunning ? _scheduledCarveHandle : default;
            if (_scheduledCarveWrites.IsCreated)
            {
                if (_scheduledCarveRunning)
                    _scheduledCarveWrites.Dispose(dependency);
                else
                    _scheduledCarveWrites.Dispose();

                _scheduledCarveWrites = default;
            }

            _scheduledCarveHandle = default;
            _scheduledCarveRunning = false;
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
            public float ExplicitRadiusMeters;
            public float ExplicitBlendStrength;
            public byte MaterialId;
            public byte DeltaFlags;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct CarveSdfJob : IJobParallelFor
        {
            public int3 MinCell;
            public int3 Span;
            public float VoxelSize;
            public float Radius;
            public float BlendRadius;
            public float BlendStrength;
            public float3 Center;
            public byte MaterialId;
            public byte DeltaFlags;
            public NativeArray<CarveCellWrite> Writes;

            public void Execute(int index)
            {
                int spanXY = Span.x * Span.y;
                int localZ = index / spanXY;
                int remainder = index - (localZ * spanXY);
                int localY = remainder / Span.x;
                int localX = remainder - (localY * Span.x);
                int3 absoluteCell = MinCell + new int3(localX, localY, localZ);
                float3 cellCenter = (new float3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5f) * VoxelSize;
                float sphereDistance = math.distance(cellCenter, Center) - Radius;
                if (sphereDistance >= BlendRadius)
                {
                    Writes[index] = default;
                    return;
                }

                float densityValue = (DeltaFlags & DeltaModeAdditive) != 0
                    ? math.clamp(-sphereDistance, -8f, 8f)
                    : math.clamp(sphereDistance, -8f, 8f);

                Writes[index] = new CarveCellWrite
                {
                    AbsoluteCell = absoluteCell,
                    SdfValueBits = (ushort)math.f32tof16(densityValue),
                    MaterialId = MaterialId,
                    DeltaFlags = DeltaFlags,
                    BlendStrength = BlendStrength,
                    IsActive = 1
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CarveCellWrite
        {
            public int3 AbsoluteCell;
            public ushort SdfValueBits;
            public float BlendStrength;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte IsActive;
            public byte Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeSnapshotHeader
        {
            public int Version;
            public int ChunkCount;
            public int TotalDirtyCellCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct LegacyNativeSnapshotHeader
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
            public NativeArray<byte> CellFlags;

            public ChunkDeltaState(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                DirtyMaskWords = new NativeArray<uint>(ChunkDirtyMaskWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                SdfValueBits = new NativeArray<ushort>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                MaterialIds = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CellFlags = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            public void Dispose()
            {
                if (DirtyMaskWords.IsCreated)
                    DirtyMaskWords.Dispose();

                if (SdfValueBits.IsCreated)
                    SdfValueBits.Dispose();

                if (MaterialIds.IsCreated)
                    MaterialIds.Dispose();

                if (CellFlags.IsCreated)
                    CellFlags.Dispose();
            }
        }
    }
}
