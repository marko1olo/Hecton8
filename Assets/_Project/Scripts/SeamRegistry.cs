using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4029)]
    public sealed class SeamRegistry : MonoBehaviour, ISaveable
    {
        [Header("Settings")]
        [SerializeField] private int initialCapacity = 128;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRegisteredSeamCount;
        [SerializeField] private long _debugLastRuntimeKey;
        [SerializeField] private float _debugLastAbsoluteSeamHeight;

        private Dictionary<long, ProceduralGeologySeamStateDTO> _recordsByRuntimeKey;
        private NativeParallelHashMap<int2, float> _seamHeightsByChunk;

        internal static SeamRegistry ActiveRuntimeInstance { get; private set; }

        /// <summary>Save priority sits after core procedural placement state.</summary>
        public int SavePriority => 56;

        /// <summary>Load priority sits after core procedural placement state.</summary>
        public int LoadPriority => 56;

        /// <summary>Number of tracked seam records.</summary>
        public int RegisteredSeamCount => _recordsByRuntimeKey != null ? _recordsByRuntimeKey.Count : 0;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            int capacity = Mathf.Clamp(initialCapacity, 32, ProceduralWorldStateDTO.MaxGeologySeamStates);
            // COLD ALLOC: Dictionary<long, ProceduralGeologySeamStateDTO>[capacity] - persistent seam save registry keyed by runtime key - owner: SeamRegistry
            _recordsByRuntimeKey = new Dictionary<long, ProceduralGeologySeamStateDTO>(capacity);
            // COLD ALLOC: NativeParallelHashMap<int2, float>[capacity] - terrain chunk seam height lookup in AUP frame - owner: SeamRegistry
            _seamHeightsByChunk = new NativeParallelHashMap<int2, float>(capacity, Allocator.Persistent);
            UpdateDiagnostics(0L, 0f);
        }

        private void OnEnable()
        {
            GlobalRegistry.Save?.Register(this);
        }

        private void OnDisable()
        {
            GlobalRegistry.Save?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_seamHeightsByChunk.IsCreated)
                _seamHeightsByChunk.Dispose();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        /// <summary>
        /// Upserts the current seam transition so save/load and runtime chunk lookups stay aligned.
        /// </summary>
        public void Upsert(in WorldGenerativeGeologySeamPlan plan)
        {
            if (plan.runtimeKey == 0L || !plan.hasTerrainSample || _recordsByRuntimeKey == null || !_seamHeightsByChunk.IsCreated)
                return;

            ProceduralGeologySeamStateDTO state = new ProceduralGeologySeamStateDTO
            {
                runtimeKey = plan.runtimeKey,
                chunkX = plan.chunkX,
                chunkZ = plan.chunkZ,
                absoluteTerrainHeight = plan.absoluteTerrainHeight,
                absoluteSeamHeight = VoxelSeamDirector.ComputeTargetSnapHeight(plan.absoluteTerrainHeight),
                seamBlendRadius = plan.seamBlendRadius,
                terrainBlendWeight = plan.terrainBlendWeight,
                caveBlendWeight = plan.caveBlendWeight,
                absolutePositionX = plan.absoluteUniversePosition.x,
                absolutePositionY = plan.absoluteUniversePosition.y,
                absolutePositionZ = plan.absoluteUniversePosition.z,
                absoluteVoxelCenterX = plan.absoluteVoxelVolumeCenter.x,
                absoluteVoxelCenterY = plan.absoluteVoxelVolumeCenter.y,
                absoluteVoxelCenterZ = plan.absoluteVoxelVolumeCenter.z
            };

            _recordsByRuntimeKey[plan.runtimeKey] = state;
            _seamHeightsByChunk[new int2(plan.chunkX, plan.chunkZ)] = state.absoluteSeamHeight;
            UpdateDiagnostics(plan.runtimeKey, state.absoluteSeamHeight);
        }

        /// <summary>
        /// Removes a seam record and repairs the chunk height map if another seam still owns the chunk.
        /// </summary>
        public bool Remove(long runtimeKey)
        {
            if (runtimeKey == 0L || _recordsByRuntimeKey == null || !_recordsByRuntimeKey.TryGetValue(runtimeKey, out ProceduralGeologySeamStateDTO removed))
                return false;

            _recordsByRuntimeKey.Remove(runtimeKey);
            RefreshChunkSeamHeight(new int2(removed.chunkX, removed.chunkZ), runtimeKey);
            UpdateDiagnostics(runtimeKey, removed.absoluteSeamHeight);
            return true;
        }

        /// <summary>
        /// Returns the registered seam height for a terrain chunk when present.
        /// </summary>
        public bool TryGetChunkSeamHeight(int chunkX, int chunkZ, out float absoluteSeamHeight)
        {
            absoluteSeamHeight = 0f;
            return _seamHeightsByChunk.IsCreated && _seamHeightsByChunk.TryGetValue(new int2(chunkX, chunkZ), out absoluteSeamHeight);
        }

        /// <summary>
        /// Clears all tracked seam state.
        /// </summary>
        public void ClearAll()
        {
            _recordsByRuntimeKey?.Clear();
            if (_seamHeightsByChunk.IsCreated)
                _seamHeightsByChunk.Clear();

            UpdateDiagnostics(0L, 0f);
        }

        public void PopulateSaveData(SaveData data)
        {
            ref ProceduralWorldStateDTO dto = ref data.proceduralWorldState;
            dto.EnsureCapacity();

            int seamIndex = 0;
            if (_recordsByRuntimeKey != null)
            {
                Dictionary<long, ProceduralGeologySeamStateDTO>.Enumerator enumerator = _recordsByRuntimeKey.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (seamIndex >= ProceduralWorldStateDTO.MaxGeologySeamStates)
                    {
                        Debug.LogWarning($"[SeamRegistry] Max seam states ({ProceduralWorldStateDTO.MaxGeologySeamStates}) reached. Extra entries were not saved.");
                        break;
                    }

                    dto.geologySeamStates[seamIndex++] = enumerator.Current.Value;
                }

                enumerator.Dispose();
            }

            dto.geologySeamStateCount = seamIndex;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ProceduralWorldStateDTO dto = data.proceduralWorldState;
            ClearAll();

            if (_recordsByRuntimeKey == null || !_seamHeightsByChunk.IsCreated || dto.geologySeamStates == null)
                return;

            int seamCount = Mathf.Min(dto.geologySeamStateCount, dto.geologySeamStates.Length);
            for (int i = 0; i < seamCount; i++)
            {
                ProceduralGeologySeamStateDTO state = dto.geologySeamStates[i];
                if (state.runtimeKey == 0L)
                    continue;

                _recordsByRuntimeKey[state.runtimeKey] = state;
                _seamHeightsByChunk[new int2(state.chunkX, state.chunkZ)] = state.absoluteSeamHeight;
                UpdateDiagnostics(state.runtimeKey, state.absoluteSeamHeight);
            }
        }

        private void RefreshChunkSeamHeight(int2 chunkKey, long removedRuntimeKey)
        {
            if (!_seamHeightsByChunk.IsCreated)
                return;

            float replacementHeight = 0f;
            bool foundReplacement = false;
            if (_recordsByRuntimeKey != null)
            {
                Dictionary<long, ProceduralGeologySeamStateDTO>.Enumerator enumerator = _recordsByRuntimeKey.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<long, ProceduralGeologySeamStateDTO> pair = enumerator.Current;
                    if (pair.Key == removedRuntimeKey)
                        continue;

                    ProceduralGeologySeamStateDTO state = pair.Value;
                    if (state.chunkX != chunkKey.x || state.chunkZ != chunkKey.y)
                        continue;

                    replacementHeight = state.absoluteSeamHeight;
                    foundReplacement = true;
                    break;
                }

                enumerator.Dispose();
            }

            if (foundReplacement)
                _seamHeightsByChunk[chunkKey] = replacementHeight;
            else
                _seamHeightsByChunk.Remove(chunkKey);
        }

        private void UpdateDiagnostics(long runtimeKey, float absoluteSeamHeight)
        {
            _debugRegisteredSeamCount = _recordsByRuntimeKey != null ? _recordsByRuntimeKey.Count : 0;
            _debugLastRuntimeKey = runtimeKey;
            _debugLastAbsoluteSeamHeight = absoluteSeamHeight;
        }
    }
}
