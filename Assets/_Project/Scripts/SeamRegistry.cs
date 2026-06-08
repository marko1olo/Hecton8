using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Caves;
using Hecton8.SaveSystem;
using Unity.Mathematics;
using UnityEngine;
using float2 = Unity.Mathematics.float2;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4029)]
    public sealed class SeamRegistry : MonoBehaviour, ISaveable, IGlobalRegistryHotSwapListener
    {
        [Header("Settings")]
        [SerializeField] private int initialCapacity = 128;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRegisteredSeamCount;
        [SerializeField] private int _debugRegisteredCaveEntranceCount;
        [SerializeField] private long _debugLastRuntimeKey;
        [SerializeField] private float _debugLastAbsoluteMinSeamHeight;
        [SerializeField] private float _debugLastAbsoluteMaxSeamHeight;
        [SerializeField] private int _debugLastChunkOverlapCount;

        private Dictionary<long, ProceduralGeologySeamStateDTO> _recordsByRuntimeKey;
        private Dictionary<long, ProceduralGeologyCaveEntranceDTO> _caveEntrancesByRuntimeKey;
        private Dictionary<long, float2> _seamHeightsByChunk;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private bool _saveRegistered;
        private bool _hotSwapRegistered;

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
            // COLD ALLOC: Dictionary<long, ProceduralGeologyCaveEntranceDTO>[capacity] - deterministic cave-mouth persistence keyed by runtime key - owner: SeamRegistry
            _caveEntrancesByRuntimeKey = new Dictionary<long, ProceduralGeologyCaveEntranceDTO>(capacity);
            // COLD ALLOC: Dictionary<long, float2>[capacity] - terrain chunk seam min/max bounds lookup in AUP frame - owner: SeamRegistry
            _seamHeightsByChunk = new Dictionary<long, float2>(capacity);
            UpdateDiagnostics(0L, 0f, 0f);
            EnsureGapDitherRenderer();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            if (isActiveAndEnabled)
                TryRegisterSaveParticipant();
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveService = null;
            _saveRegistered = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void CacheRegistryServicesCold()
        {
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        /// <summary>
        /// Upserts the current seam transition so save/load and runtime chunk lookups stay aligned.
        /// </summary>
        public void Upsert(in WorldGenerativeGeologySeamPlan plan)
        {
            if (plan.runtimeKey == 0L || !plan.hasTerrainSample || _recordsByRuntimeKey == null || _seamHeightsByChunk == null)
                return;

            float absoluteSeamHeight = VoxelSeamDirector.ComputeTargetSnapHeight(plan.absoluteTerrainHeight);
            ProceduralGeologySeamStateDTO state = new ProceduralGeologySeamStateDTO
            {
                runtimeKey = plan.runtimeKey,
                chunkX = plan.chunkX,
                chunkZ = plan.chunkZ,
                absoluteTerrainHeight = plan.absoluteTerrainHeight,
                absoluteSeamHeight = absoluteSeamHeight,
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
            UpsertCaveEntrance(plan);
            RebuildChunkSeamHeight(new int2(plan.chunkX, plan.chunkZ));
            UpdateDiagnostics(plan.runtimeKey, absoluteSeamHeight, absoluteSeamHeight);
        }

        /// <summary>
        /// Removes a seam record and repairs the chunk height map if another seam still owns the chunk.
        /// </summary>
        public bool Remove(long runtimeKey)
        {
            if (runtimeKey == 0L || _recordsByRuntimeKey == null || !_recordsByRuntimeKey.TryGetValue(runtimeKey, out ProceduralGeologySeamStateDTO removed))
                return false;

            _recordsByRuntimeKey.Remove(runtimeKey);
            _caveEntrancesByRuntimeKey?.Remove(runtimeKey);
            RefreshChunkSeamHeight(new int2(removed.chunkX, removed.chunkZ), runtimeKey);
            UpdateDiagnostics(runtimeKey, removed.absoluteSeamHeight, removed.absoluteSeamHeight);
            return true;
        }

        /// <summary>
        /// Returns the registered seam height for a terrain chunk when present.
        /// </summary>
        public bool TryGetChunkSeamHeight(int chunkX, int chunkZ, out float absoluteSeamHeight)
        {
            absoluteSeamHeight = 0f;
            if (_seamHeightsByChunk == null || !_seamHeightsByChunk.TryGetValue(PackChunkKey(chunkX, chunkZ), out float2 seamBounds))
                return false;

            absoluteSeamHeight = seamBounds.x;
            return true;
        }

        /// <summary>
        /// Returns the registered seam height bounds for a terrain chunk when present.
        /// X = min seam height, Y = max seam height.
        /// </summary>
        public bool TryGetChunkSeamBounds(int chunkX, int chunkZ, out float2 absoluteSeamBounds)
        {
            absoluteSeamBounds = default;
            return _seamHeightsByChunk != null && _seamHeightsByChunk.TryGetValue(PackChunkKey(chunkX, chunkZ), out absoluteSeamBounds);
        }

        /// <summary>
        /// Copies active seam records into a caller-owned list without allocating.
        /// </summary>
        public void CopyStatesTo(List<ProceduralGeologySeamStateDTO> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (_recordsByRuntimeKey == null)
                return;

            Dictionary<long, ProceduralGeologySeamStateDTO>.Enumerator enumerator = _recordsByRuntimeKey.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (destination.Count >= destination.Capacity)
                    break;

                destination.Add(enumerator.Current.Value);
            }

            enumerator.Dispose();
        }

        /// <summary>
        /// Copies active cave-mouth records into a caller-owned list without allocating.
        /// </summary>
        public void CopyCaveEntrancesTo(List<ProceduralGeologyCaveEntranceDTO> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (_caveEntrancesByRuntimeKey == null)
                return;

            Dictionary<long, ProceduralGeologyCaveEntranceDTO>.Enumerator enumerator = _caveEntrancesByRuntimeKey.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (destination.Count >= destination.Capacity)
                    break;

                destination.Add(enumerator.Current.Value);
            }

            enumerator.Dispose();
        }

        /// <summary>
        /// Clears all tracked seam state.
        /// </summary>
        public void ClearAll()
        {
            _recordsByRuntimeKey?.Clear();
            _caveEntrancesByRuntimeKey?.Clear();
            _seamHeightsByChunk?.Clear();

            UpdateDiagnostics(0L, 0f, 0f);
        }

        public void PopulateSaveData(SaveData data)
        {
            ref ProceduralWorldStateDTO dto = ref data.proceduralWorldState;
            dto.EnsureCapacity();

            int seamIndex = 0;
            int caveEntranceIndex = 0;
            if (_recordsByRuntimeKey != null)
            {
                Dictionary<long, ProceduralGeologySeamStateDTO>.Enumerator enumerator = _recordsByRuntimeKey.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (seamIndex >= ProceduralWorldStateDTO.MaxGeologySeamStates)
                    {
                        Hecton8.Core.H8Debug.LogWarning($"[SeamRegistry] Max seam states ({ProceduralWorldStateDTO.MaxGeologySeamStates}) reached. Extra entries were not saved.");
                        break;
                    }

                    dto.geologySeamStates[seamIndex++] = enumerator.Current.Value;
                }

                enumerator.Dispose();
            }

            if (_caveEntrancesByRuntimeKey != null)
            {
                Dictionary<long, ProceduralGeologyCaveEntranceDTO>.Enumerator enumerator = _caveEntrancesByRuntimeKey.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (caveEntranceIndex >= ProceduralWorldStateDTO.MaxGeologyCaveEntrances)
                    {
                        Hecton8.Core.H8Debug.LogWarning($"[SeamRegistry] Max cave entrance states ({ProceduralWorldStateDTO.MaxGeologyCaveEntrances}) reached. Extra entries were not saved.");
                        break;
                    }

                    dto.geologyCaveEntrances[caveEntranceIndex++] = enumerator.Current.Value;
                }

                enumerator.Dispose();
            }

            dto.geologySeamStateCount = seamIndex;
            dto.geologyCaveEntranceCount = caveEntranceIndex;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ProceduralWorldStateDTO dto = data.proceduralWorldState;
            ClearAll();

            if (_recordsByRuntimeKey == null || _seamHeightsByChunk == null || dto.geologySeamStates == null)
                return;

            int seamCount = Mathf.Min(dto.geologySeamStateCount, dto.geologySeamStates.Length);
            for (int i = 0; i < seamCount; i++)
            {
                ProceduralGeologySeamStateDTO state = dto.geologySeamStates[i];
                if (state.runtimeKey == 0L)
                    continue;

                _recordsByRuntimeKey[state.runtimeKey] = state;
                RebuildChunkSeamHeight(new int2(state.chunkX, state.chunkZ));
                UpdateDiagnostics(state.runtimeKey, state.absoluteSeamHeight, state.absoluteSeamHeight);
            }

            int caveEntranceCount = Mathf.Min(dto.geologyCaveEntranceCount, dto.geologyCaveEntrances != null ? dto.geologyCaveEntrances.Length : 0);
            for (int i = 0; i < caveEntranceCount; i++)
            {
                ProceduralGeologyCaveEntranceDTO entrance = dto.geologyCaveEntrances[i];
                if (entrance.runtimeKey == 0L)
                    continue;

                _caveEntrancesByRuntimeKey[entrance.runtimeKey] = entrance;
            }
        }

        private void RefreshChunkSeamHeight(int2 chunkKey, long removedRuntimeKey)
        {
            if (_seamHeightsByChunk == null)
                return;

            RebuildChunkSeamHeight(chunkKey, removedRuntimeKey);
        }

        private void RebuildChunkSeamHeight(int2 chunkKey, long ignoredRuntimeKey = 0L)
        {
            if (_seamHeightsByChunk == null)
                return;

            float minSeamHeight = float.MaxValue;
            float maxSeamHeight = float.MinValue;
            int overlapCount = 0;
            if (_recordsByRuntimeKey != null)
            {
                Dictionary<long, ProceduralGeologySeamStateDTO>.Enumerator enumerator = _recordsByRuntimeKey.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<long, ProceduralGeologySeamStateDTO> pair = enumerator.Current;
                    if (pair.Key == ignoredRuntimeKey)
                        continue;

                    ProceduralGeologySeamStateDTO state = pair.Value;
                    if (state.chunkX != chunkKey.x || state.chunkZ != chunkKey.y)
                        continue;

                    overlapCount++;
                    if (state.absoluteSeamHeight < minSeamHeight)
                        minSeamHeight = state.absoluteSeamHeight;
                    if (state.absoluteSeamHeight > maxSeamHeight)
                        maxSeamHeight = state.absoluteSeamHeight;
                }

                enumerator.Dispose();
            }

            _debugLastChunkOverlapCount = overlapCount;
            long packedChunkKey = PackChunkKey(chunkKey.x, chunkKey.y);
            if (overlapCount > 0)
            {
                _seamHeightsByChunk[packedChunkKey] = new float2(minSeamHeight, maxSeamHeight);
                _debugLastAbsoluteMinSeamHeight = minSeamHeight;
                _debugLastAbsoluteMaxSeamHeight = maxSeamHeight;
            }
            else
                _seamHeightsByChunk.Remove(packedChunkKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long PackChunkKey(int chunkX, int chunkZ)
        {
            return ((long)chunkX << 32) ^ (uint)chunkZ;
        }

        private void UpsertCaveEntrance(in WorldGenerativeGeologySeamPlan plan)
        {
            if (_caveEntrancesByRuntimeKey == null)
                return;

            if (!VoxelSeamDirector.ShouldCreateCaveMouth(plan.hasTerrainSample, plan.slopeDegrees, plan.caveBlendMode))
            {
                _caveEntrancesByRuntimeKey.Remove(plan.runtimeKey);
                return;
            }

            Vector3 absoluteSurfacePosition = new Vector3(
                plan.absoluteUniversePosition.x,
                plan.absoluteTerrainHeight,
                plan.absoluteUniversePosition.z);
            CaveEntrance entrance = VoxelSeamDirector.BuildCaveEntrance(
                absoluteSurfacePosition,
                plan.absoluteVoxelVolumeCenter,
                plan.voxelVolumeSize,
                plan.caveBlendWeight,
                plan.seamBlendRadius,
                plan.suggestedTerrainCut);

            _caveEntrancesByRuntimeKey[plan.runtimeKey] = new ProceduralGeologyCaveEntranceDTO
            {
                runtimeKey = plan.runtimeKey,
                surfacePositionX = entrance.surfacePosition.x,
                surfacePositionY = entrance.surfacePosition.y,
                surfacePositionZ = entrance.surfacePosition.z,
                inwardDirectionX = entrance.inwardDirection.x,
                inwardDirectionY = entrance.inwardDirection.y,
                inwardDirectionZ = entrance.inwardDirection.z,
                radius = entrance.radius,
                funnelLength = entrance.funnelLength,
                innerRadius = entrance.innerRadius
            };
        }

        private void EnsureGapDitherRenderer()
        {
            if (gameObject.TryGetComponent(out SeamGapDitherRenderer renderer))
            {
                renderer.SetSeamRegistry(this);
                return;
            }

            renderer = gameObject.AddComponent<SeamGapDitherRenderer>();
            renderer.SetSeamRegistry(this);
        }

        private void UpdateDiagnostics(long runtimeKey, float absoluteMinSeamHeight, float absoluteMaxSeamHeight)
        {
            _debugRegisteredSeamCount = _recordsByRuntimeKey != null ? _recordsByRuntimeKey.Count : 0;
            _debugRegisteredCaveEntranceCount = _caveEntrancesByRuntimeKey != null ? _caveEntrancesByRuntimeKey.Count : 0;
            _debugLastRuntimeKey = runtimeKey;
            _debugLastAbsoluteMinSeamHeight = absoluteMinSeamHeight;
            _debugLastAbsoluteMaxSeamHeight = absoluteMaxSeamHeight;
        }
    }
}
