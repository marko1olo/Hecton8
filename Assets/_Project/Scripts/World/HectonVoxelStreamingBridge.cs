using System;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Bridges streamed terrain-hole cave entrances into async voxel cave generation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4027)]
    public sealed class HectonVoxelStreamingBridge : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private struct CaveEntranceRequest
        {
            public long Key;
            public int HoleId;
            public AbsoluteUniversePosition AbsolutePosition;
            public float Radius;
            public float Priority;
            public uint Seed;
            public uint Generation;
        }

        private struct ChunkFadeState
        {
            public GameObject Volume;
            public Renderer Renderer;
            public Material OriginalMaterial;
            public Material RuntimeMaterial;
            public int RuntimeMaterialPoolIndex;
            public float Elapsed;
        }

        [Header("References")]
        [SerializeField] private HectonMapMagicVegetationBridge vegetationBridge;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CavePreset cavePresetOverride;

        [Header("Streaming")]
        [SerializeField, Min(1)] private int maxRuntimeVolumes = 6;
        [SerializeField, Min(1)] private int maxAsyncLaunchesPerTick = 1;
        [SerializeField, Min(25f)] private float requestDistance = 260f;
        [SerializeField, Min(25f)] private float retentionDistance = 320f;
        [SerializeField, Min(1f)] private float caveVerticalOffset = 22f;
        [SerializeField, Min(4f)] private float fallbackCaveHeight = 80f;

        [Header("Presentation")]
        [SerializeField, Min(0.05f)] private float chunkFadeInDuration = 0.5f;

        private const int MaxRuntimeVolumeCapacity = 32;
        private const int MaxDesiredRequestCapacity = MaxRuntimeVolumeCapacity * 2;
        private const int MaxPendingRequestCapacity = MaxRuntimeVolumeCapacity;
        private const int MaxChunkFadeStateCapacity = MaxRuntimeVolumeCapacity;
        private const int MaxLaunchQueueCapacity = MaxDesiredRequestCapacity;
        private const int MaxKeyScratchCapacity = MaxDesiredRequestCapacity + MaxRuntimeVolumeCapacity;
        private const int MaxPendingChunkFadeCapacity = MaxRuntimeVolumeCapacity;

        // COLD ALLOC: long/GameObject[32] - active streamed voxel cave volumes; no managed collection growth.
        private readonly long[] _activeVolumeKeys = new long[MaxRuntimeVolumeCapacity];
        private readonly GameObject[] _activeVolumes = new GameObject[MaxRuntimeVolumeCapacity];
        private int _activeVolumeCount;
        // COLD ALLOC: CaveEntranceRequest[64] - desired terrain-hole cave requests; no managed collection growth.
        private readonly CaveEntranceRequest[] _desiredRequests = new CaveEntranceRequest[MaxDesiredRequestCapacity];
        private int _desiredRequestCount;
        // COLD ALLOC: long/uint[32] - bounded async launch generation records.
        private readonly long[] _pendingRequestKeys = new long[MaxPendingRequestCapacity];
        private readonly uint[] _pendingRequestGenerations = new uint[MaxPendingRequestCapacity];
        private int _pendingRequestCount;
        private uint _pendingRequestSequence;
        // COLD ALLOC: long/ChunkFadeState[32] - temporary streamed voxel chunk dissolve states.
        private readonly long[] _chunkFadeStateKeys = new long[MaxChunkFadeStateCapacity];
        private readonly ChunkFadeState[] _chunkFadeStates = new ChunkFadeState[MaxChunkFadeStateCapacity];
        private int _chunkFadeStateCount;
        // COLD ALLOC: CaveEntranceRequest[64] - sorted launch queue; no managed collection growth.
        private readonly CaveEntranceRequest[] _launchQueue = new CaveEntranceRequest[MaxLaunchQueueCapacity];
        private int _launchQueueCount;
        // COLD ALLOC: long[96] - bounded key scratch for removals and fade cleanup.
        private readonly long[] _keyScratch = new long[MaxKeyScratchCapacity];
        private int _keyScratchCount;
        // COLD ALLOC: long[32] - deferred despawn queue.
        private readonly long[] _pendingDespawnKeys = new long[MaxRuntimeVolumeCapacity];
        private int _pendingDespawnKeyCount;
        private Unity.Collections.FixedList512Bytes<long> _pendingChunkFadeKeys;
        private static readonly int ChunkDissolveFadeId = Shader.PropertyToID("_ChunkDissolveFade");
        private const uint ChunkFadeRendererMissingWarningHash = 0xD0B2923Bu;
        private const uint ChunkFadeMaterialMissingWarningHash = 0xBBEEF2CDu;
        private const uint ChunkFadeMaterialPoolMissingWarningHash = 0x8D4A6E29u;
        private const uint ChunkFadePendingQueueFullWarningHash = 0x6F9D2C41u;
        // COLD ALLOC: Material[32] - pooled voxel chunk fade material clones; owner: HectonVoxelStreamingBridge.
        private readonly Material[] _chunkFadeMaterialPool = new Material[MaxChunkFadeStateCapacity];
        // COLD ALLOC: bool[32] - pooled fade material occupancy flags; owner: HectonVoxelStreamingBridge.
        private readonly bool[] _chunkFadeMaterialPoolInUse = new bool[MaxChunkFadeStateCapacity];
        private int _chunkFadeMaterialPoolCount;
        private Material _chunkFadePoolSourceMaterial;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private float _chunkFadeDeltaAccumulator;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            maxRuntimeVolumes = Mathf.Clamp(maxRuntimeVolumes, 1, MaxRuntimeVolumeCapacity);
            maxAsyncLaunchesPerTick = Mathf.Max(1, maxAsyncLaunchesPerTick);
            requestDistance = Mathf.Max(25f, requestDistance);
            retentionDistance = Mathf.Max(requestDistance, retentionDistance);
            caveVerticalOffset = Mathf.Max(1f, caveVerticalOffset);
            fallbackCaveHeight = Mathf.Max(4f, fallbackCaveHeight);
            chunkFadeInDuration = Mathf.Max(0.05f, chunkFadeInDuration);
        }

        private void OnEnable()
        {
            RefreshColdReferences();
            EnsureLifetimeCancellation();
            EnsureChunkFadeMaterialPool();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            RefreshColdReferences();
            EnsureChunkFadeMaterialPool();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            CancelAllPendingRequests();
            CancelLifetimeCancellation();
            ClearLaunchQueue();
            ClearPendingChunkFadeRegistrations();
            ClearPendingDespawns();
            ClearDesiredRequests();
            DespawnAllVolumes();
            ReleaseChunkFadeMaterialPool();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            CancelAllPendingRequests();
            CancelLifetimeCancellation();
            ClearLaunchQueue();
            ClearPendingChunkFadeRegistrations();
            ClearPendingDespawns();
            ClearDesiredRequests();
            DespawnAllVolumes();
            ReleaseChunkFadeMaterialPool();
        }

        /// <summary>
        /// Launches bounded async voxel cave requests prepared during the last slow tick.
        /// </summary>
        public void Tick(float dt)
        {
            _chunkFadeDeltaAccumulator += Mathf.Max(0f, dt);

            int runtimeVolumeLimit = ResolveRuntimeVolumeLimit();
            if (voxelEngine == null || _launchQueueCount <= 0 || _activeVolumeCount >= runtimeVolumeLimit)
                return;

            int launchCount = Mathf.Min(maxAsyncLaunchesPerTick, _launchQueueCount);
            for (int i = 0; i < launchCount; i++)
            {
                CaveEntranceRequest request = _launchQueue[i];
                if (ContainsActiveVolume(request.Key) || ContainsPendingRequest(request.Key))
                    continue;

                request.Generation = NextPendingRequestGeneration();
                if (!SetPendingRequest(request.Key, request.Generation))
                    continue;

                _ = SpawnCaveAsync(request);
            }

            if (launchCount > 0)
                RemoveLaunchQueuePrefix(launchCount);
        }

        public void LateFrameTick()
        {
            FlushPendingDespawns();
            FlushPendingChunkFadeRegistrations();
            if (_chunkFadeDeltaAccumulator > 0f)
            {
                float dt = _chunkFadeDeltaAccumulator;
                _chunkFadeDeltaAccumulator = 0f;
                TickChunkFade(dt);
            }
        }

        /// <summary>
        /// Rebuilds cave-entrance streaming intent from the current terrain-hole snapshot.
        /// </summary>
        public void SlowTick()
        {
            RebuildDesiredRequests();
            CancelStalePendingRequests();
            DespawnStaleVolumes();
            RebuildLaunchQueue();
        }

        private async Awaitable SpawnCaveAsync(CaveEntranceRequest request)
        {
            CancellationToken token = _lifetimeCancellation != null
                ? _lifetimeCancellation.Token
                : CancellationToken.None;

            try
            {
                if (voxelEngine == null)
                    return;

                CavePreset preset = cavePresetOverride != null
                    ? cavePresetOverride
                    : voxelEngine.defaultPreset;
                Vector3 runtimePosition = ResolveRuntimePosition(in request);
                Vector3 caveCenter = runtimePosition + (Vector3.down * Mathf.Max(1f, caveVerticalOffset));
                GameObject volume = await voxelEngine.GenerateVolumeAsync(caveCenter, request.Seed, preset, lodLevel: 0, ct: token);
                if (volume == null)
                    return;

                if (!isActiveAndEnabled ||
                    !ContainsDesiredRequest(request.Key) ||
                    !IsPendingRequestGeneration(request.Key, request.Generation))
                {
                    voxelEngine.DespawnVolume(volume);
                    return;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                volume.name = "VoxelCave";
#endif
                if (!SetActiveVolume(request.Key, volume))
                {
                    voxelEngine.DespawnVolume(volume);
                    return;
                }

                RegisterChunkFade(request.Key, volume);
                if (vegetationBridge != null)
                    vegetationBridge.RegisterArtificialStructure(ResolveVolumeBounds(volume, caveCenter, request.Radius), StructureType.VoxelCave);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                RemovePendingRequest(request.Key, request.Generation);
            }
        }

        private void RebuildDesiredRequests()
        {
            ClearDesiredRequests();
            if (vegetationBridge == null || voxelEngine == null)
                return;

            if (!vegetationBridge.TryGetTerrainHoleStreamingPayload(out var holes, out int holeCount) || holes.Length <= 0 || holeCount <= 0)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            double requestDistanceSq = (double)requestDistance * requestDistance;
            for (int i = 0; i < holeCount; i++)
            {
                TerrainHoleStreamingRecord hole = holes[i];
                if (hole.SourceType != TerrainHoleSourceType.CaveEntrance)
                    continue;

                if (!TryResolveAupFromRuntimeOrigin(hole.Position, out AbsoluteUniversePosition absoluteHolePosition))
                    continue;

                long requestKey = hole.HoleId != 0
                    ? hole.HoleId
                    : BuildHoleKey(absoluteHolePosition, hole.Radius);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in absoluteHolePosition, in playerAup);
                if (distanceSq > requestDistanceSq && !ContainsActiveVolume(requestKey))
                    continue;

                CaveEntranceRequest request = new CaveEntranceRequest
                {
                    Key = requestKey,
                    HoleId = hole.HoleId,
                    AbsolutePosition = absoluteHolePosition,
                    Radius = Mathf.Max(4f, hole.Radius),
                    Priority = (float)Math.Min(distanceSq, float.MaxValue),
                    Seed = BuildHoleSeed(absoluteHolePosition, hole.Radius)
                };
                if (!SetDesiredRequest(in request))
                    break;

                int desiredLimit = Mathf.Min(ResolveRuntimeVolumeLimit() * 2, _desiredRequests.Length);
                if (_desiredRequestCount >= desiredLimit)
                    break;
            }
        }

        private void CancelStalePendingRequests()
        {
            if (_pendingRequestCount <= 0)
                return;

            ClearKeyScratch();
            for (int i = 0; i < _pendingRequestCount; i++)
            {
                long key = _pendingRequestKeys[i];
                if (!ContainsDesiredRequest(key))
                    AddKeyScratch(key);
            }

            for (int i = 0; i < _keyScratchCount; i++)
            {
                long key = _keyScratch[i];
                RemovePendingRequest(key);
            }
        }

        private void DespawnStaleVolumes()
        {
            if (_activeVolumeCount <= 0)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            double retentionDistanceSq = (double)retentionDistance * retentionDistance;
            ClearKeyScratch();
            for (int i = 0; i < _activeVolumeCount; i++)
            {
                long key = _activeVolumeKeys[i];
                if (!TryGetDesiredRequest(key, out CaveEntranceRequest request))
                {
                    AddKeyScratch(key);
                    continue;
                }

                if (AbsoluteUniversePosition.DistanceSq(in request.AbsolutePosition, in playerAup) > retentionDistanceSq)
                    AddKeyScratch(key);
            }

            for (int i = 0; i < _keyScratchCount; i++)
            {
                long key = _keyScratch[i];
                if (!TryGetActiveVolume(key, out GameObject volume))
                    continue;

                QueueVolumeDespawn(key);
            }
        }

        private void QueueVolumeDespawn(long key)
        {
            if (_pendingDespawnKeyCount >= _pendingDespawnKeys.Length)
            {
                FlushPendingDespawns();
                if (_pendingDespawnKeyCount >= _pendingDespawnKeys.Length)
                    return;
            }

            _pendingDespawnKeys[_pendingDespawnKeyCount++] = key;
        }

        private void FlushPendingDespawns()
        {
            if (_pendingDespawnKeyCount <= 0)
                return;

            for (int i = 0; i < _pendingDespawnKeyCount; i++)
            {
                long key = _pendingDespawnKeys[i];
                if (!TryGetActiveVolume(key, out GameObject volume))
                    continue;

                ClearChunkFadeState(key, clearRenderer: true);
                if (voxelEngine != null && volume != null)
                    voxelEngine.DespawnVolume(volume);

                RemoveActiveVolume(key);
            }

            ClearPendingDespawns();
        }

        private void RebuildLaunchQueue()
        {
            ClearLaunchQueue();
            if (_activeVolumeCount >= ResolveRuntimeVolumeLimit())
                return;

            for (int i = 0; i < _desiredRequestCount; i++)
            {
                CaveEntranceRequest request = _desiredRequests[i];
                if (ContainsActiveVolume(request.Key) || ContainsPendingRequest(request.Key))
                    continue;

                InsertQueuedRequest(request);
            }
        }

        private void InsertQueuedRequest(CaveEntranceRequest request)
        {
            if (_launchQueueCount >= _launchQueue.Length)
                return;

            int insertIndex = _launchQueueCount;
            while (insertIndex > 0 && request.Priority < _launchQueue[insertIndex - 1].Priority)
            {
                _launchQueue[insertIndex] = _launchQueue[insertIndex - 1];
                insertIndex--;
            }

            _launchQueue[insertIndex] = request;
            _launchQueueCount++;
        }

        private void DespawnAllVolumes()
        {
            if (voxelEngine == null || _activeVolumeCount <= 0)
            {
                ClearAllChunkFadeStates(clearRenderers: true);
                ClearActiveVolumes();
                return;
            }

            ClearKeyScratch();
            for (int i = 0; i < _activeVolumeCount; i++)
                AddKeyScratch(_activeVolumeKeys[i]);

            for (int i = 0; i < _keyScratchCount; i++)
            {
                long key = _keyScratch[i];
                ClearChunkFadeState(key, clearRenderer: true);
                if (TryGetActiveVolume(key, out GameObject volume) && volume != null)
                    voxelEngine.DespawnVolume(volume);

                RemoveActiveVolume(key);
            }
        }

        private void RegisterChunkFade(long key, GameObject volume)
        {
            if (volume == null || chunkFadeInDuration <= 0.0001f)
                return;

            if (_pendingChunkFadeKeys.Length >= MaxPendingChunkFadeCapacity)
            {
                PublishChunkFadeWarning(ChunkFadePendingQueueFullWarningHash, key, 1f);
                return;
            }

            _pendingChunkFadeKeys.AddNoResize(key);
        }

        private void FlushPendingChunkFadeRegistrations()
        {
            int pendingCount = math.min(_pendingChunkFadeKeys.Length, MaxPendingChunkFadeCapacity);
            for (int i = 0; i < pendingCount; i++)
            {
                long key = _pendingChunkFadeKeys[i];
                if (TryGetActiveVolume(key, out GameObject volume))
                    RegisterChunkFadeImmediate(key, volume);
            }

            ClearPendingChunkFadeRegistrations();
        }

        private void RegisterChunkFadeImmediate(long key, GameObject volume)
        {
            if (volume == null || chunkFadeInDuration <= 0.0001f)
                return;

            if (!volume.TryGetComponent(out Renderer renderer) || renderer == null)
            {
                PublishChunkFadeWarning(ChunkFadeRendererMissingWarningHash, key, 1f);
                return;
            }

            Material material = renderer.sharedMaterial;
            if (material == null || !material.HasProperty(ChunkDissolveFadeId))
            {
                PublishChunkFadeWarning(ChunkFadeMaterialMissingWarningHash, key, 1f);
                return;
            }

            ClearChunkFadeState(key, clearRenderer: true);

            if (!TryAcquireChunkFadeMaterial(material, out Material runtimeMaterial, out int runtimeMaterialPoolIndex))
            {
                PublishChunkFadeWarning(ChunkFadeMaterialPoolMissingWarningHash, key, 1f);
                return;
            }

            runtimeMaterial.SetFloat(ChunkDissolveFadeId, 0f);
            renderer.sharedMaterial = runtimeMaterial;

            ChunkFadeState state = new ChunkFadeState
            {
                Volume = volume,
                Renderer = renderer,
                OriginalMaterial = material,
                RuntimeMaterial = runtimeMaterial,
                RuntimeMaterialPoolIndex = runtimeMaterialPoolIndex,
                Elapsed = 0f
            };

            if (!SetChunkFadeState(key, in state))
            {
                renderer.sharedMaterial = material;
                ReleaseChunkFadeMaterial(runtimeMaterialPoolIndex, runtimeMaterial);
            }
        }

        private void TickChunkFade(float dt)
        {
            if (_chunkFadeStateCount <= 0)
                return;

            float safeDt = Mathf.Max(0f, dt);
            float duration = Mathf.Max(0.05f, chunkFadeInDuration);
            float quality01 = ResolveChunkFadeQualityWeight01();
            ClearKeyScratch();

            for (int i = 0; i < _chunkFadeStateCount; i++)
            {
                long key = _chunkFadeStateKeys[i];
                ChunkFadeState state = _chunkFadeStates[i];
                if (state.Renderer == null || state.RuntimeMaterial == null)
                {
                    AddKeyScratch(key);
                    continue;
                }

                state.Elapsed += safeDt;
                float fade01 = Mathf.Clamp01(state.Elapsed / duration);
                float smoothFade01 = fade01 * fade01 * (3f - 2f * fade01);
                state.RuntimeMaterial.SetFloat(ChunkDissolveFadeId, math.lerp(fade01, smoothFade01, quality01));
                _chunkFadeStates[i] = state;

                if (fade01 >= 0.999f)
                    AddKeyScratch(key);
            }

            for (int i = 0; i < _keyScratchCount; i++)
                ClearChunkFadeState(_keyScratch[i], clearRenderer: true);
        }

        private void ClearChunkFadeState(long key, bool clearRenderer)
        {
            int stateIndex = FindChunkFadeStateIndex(key);
            if (stateIndex < 0)
                return;

            ChunkFadeState state = _chunkFadeStates[stateIndex];
            if (clearRenderer &&
                state.Renderer != null &&
                ReferenceEquals(state.Renderer.sharedMaterial, state.RuntimeMaterial))
            {
                state.Renderer.sharedMaterial = state.OriginalMaterial;
            }

            ReleaseChunkFadeMaterial(state.RuntimeMaterialPoolIndex, state.RuntimeMaterial);

            RemoveChunkFadeStateAt(stateIndex);
        }

        private void EnsureChunkFadeMaterialPool()
        {
            if (_chunkFadeMaterialPoolCount > 0 || chunkFadeInDuration <= 0.0001f)
                return;

            if (voxelEngine == null || voxelEngine.voxelVolumePrefab == null)
                return;

            if (!voxelEngine.voxelVolumePrefab.TryGetComponent(out Renderer prefabRenderer) || prefabRenderer == null)
                return;

            Material material = prefabRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(ChunkDissolveFadeId))
                return;

            _chunkFadePoolSourceMaterial = material;
            for (int i = 0; i < _chunkFadeMaterialPool.Length; i++)
            {
                Material runtimeMaterial = new Material(material); // COLD ALLOC: Material[32] - prewarmed voxel fade pool; owner: HectonVoxelStreamingBridge.
                runtimeMaterial.SetFloat(ChunkDissolveFadeId, 1f);
                _chunkFadeMaterialPool[i] = runtimeMaterial;
                _chunkFadeMaterialPoolInUse[i] = false;
            }

            _chunkFadeMaterialPoolCount = _chunkFadeMaterialPool.Length;
        }

        private bool TryAcquireChunkFadeMaterial(Material sourceMaterial, out Material runtimeMaterial, out int poolIndex)
        {
            runtimeMaterial = null;
            poolIndex = -1;

            if (_chunkFadeMaterialPoolCount <= 0 || !ReferenceEquals(sourceMaterial, _chunkFadePoolSourceMaterial))
                return false;

            for (int i = 0; i < _chunkFadeMaterialPoolCount; i++)
            {
                if (_chunkFadeMaterialPoolInUse[i] || _chunkFadeMaterialPool[i] == null)
                    continue;

                _chunkFadeMaterialPoolInUse[i] = true;
                runtimeMaterial = _chunkFadeMaterialPool[i];
                poolIndex = i;
                return true;
            }

            return false;
        }

        private void ReleaseChunkFadeMaterial(int poolIndex, Material runtimeMaterial)
        {
            if (poolIndex < 0 || poolIndex >= _chunkFadeMaterialPoolCount)
                return;

            if (!ReferenceEquals(_chunkFadeMaterialPool[poolIndex], runtimeMaterial))
                return;

            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat(ChunkDissolveFadeId, 1f);

            _chunkFadeMaterialPoolInUse[poolIndex] = false;
        }

        private void ReleaseChunkFadeMaterialPool()
        {
            ClearAllChunkFadeStates(clearRenderers: true);

            for (int i = 0; i < _chunkFadeMaterialPoolCount; i++)
            {
                if (_chunkFadeMaterialPool[i] != null)
                    Destroy(_chunkFadeMaterialPool[i]);

                _chunkFadeMaterialPool[i] = null;
                _chunkFadeMaterialPoolInUse[i] = false;
            }

            _chunkFadeMaterialPoolCount = 0;
            _chunkFadePoolSourceMaterial = null;
        }

        private static void PublishChunkFadeWarning(uint warningHash, long key, float scalar)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                unchecked((uint)key),
                Mathf.Max(0f, scalar));
        }

        private static float ResolveChunkFadeQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private void ClearAllChunkFadeStates(bool clearRenderers)
        {
            while (_chunkFadeStateCount > 0)
                ClearChunkFadeState(_chunkFadeStateKeys[0], clearRenderers);
        }

        private void RefreshColdReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext playerContext) &&
                playerContext != null &&
                playerContext.PlayerMovement != null)
            {
                playerAup = playerContext.PlayerMovement.CurrentAup;
                if (AbsoluteUniversePosition.IsFinite(in playerAup))
                    return true;
            }

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;
            return TryResolveAupFromRuntimeOrigin(playerPosition, out playerAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService != null && isActiveAndEnabled)
                    {
                        _registeredTick = false;
                        _registeredSlowTick = false;
                        _registeredLateFrame = false;
                        TryRegister();
                    }
                    return;

                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    vegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    return;

                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    voxelEngine = currentService as HectonVoxelEngine;
                    ReleaseChunkFadeMaterialPool();
                    if (isActiveAndEnabled)
                        EnsureChunkFadeMaterialPool();
                    return;

                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                    playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
                    return;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private CancellationTokenSource EnsureLifetimeCancellation()
        {
            if (_lifetimeCancellation == null || _lifetimeCancellation.IsCancellationRequested)
                _lifetimeCancellation = new CancellationTokenSource();

            return _lifetimeCancellation;
        }

        private void CancelLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                return;

            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }

        private void CancelAllPendingRequests()
        {
            ClearPendingRequests();
        }

        private void ClearDesiredRequests()
        {
            if (_desiredRequestCount > 0)
                System.Array.Clear(_desiredRequests, 0, _desiredRequestCount);
            _desiredRequestCount = 0;
        }

        private int ResolveRuntimeVolumeLimit()
        {
            return Mathf.Clamp(maxRuntimeVolumes, 1, MaxRuntimeVolumeCapacity);
        }

        private bool SetDesiredRequest(in CaveEntranceRequest request)
        {
            int index = FindDesiredRequestIndex(request.Key);
            if (index >= 0)
            {
                _desiredRequests[index] = request;
                return true;
            }

            if (_desiredRequestCount >= _desiredRequests.Length)
                return false;

            _desiredRequests[_desiredRequestCount++] = request;
            return true;
        }

        private bool ContainsDesiredRequest(long key)
        {
            return FindDesiredRequestIndex(key) >= 0;
        }

        private bool TryGetDesiredRequest(long key, out CaveEntranceRequest request)
        {
            int index = FindDesiredRequestIndex(key);
            if (index < 0)
            {
                request = default;
                return false;
            }

            request = _desiredRequests[index];
            return true;
        }

        private int FindDesiredRequestIndex(long key)
        {
            for (int i = 0; i < _desiredRequestCount; i++)
            {
                if (_desiredRequests[i].Key == key)
                    return i;
            }

            return -1;
        }

        private void ClearActiveVolumes()
        {
            if (_activeVolumeCount > 0)
            {
                System.Array.Clear(_activeVolumeKeys, 0, _activeVolumeCount);
                System.Array.Clear(_activeVolumes, 0, _activeVolumeCount);
            }

            _activeVolumeCount = 0;
        }

        private bool SetActiveVolume(long key, GameObject volume)
        {
            int index = FindActiveVolumeIndex(key);
            if (index >= 0)
            {
                _activeVolumes[index] = volume;
                return true;
            }

            if (_activeVolumeCount >= ResolveRuntimeVolumeLimit())
                return false;

            _activeVolumeKeys[_activeVolumeCount] = key;
            _activeVolumes[_activeVolumeCount] = volume;
            _activeVolumeCount++;
            return true;
        }

        private bool ContainsActiveVolume(long key)
        {
            return FindActiveVolumeIndex(key) >= 0;
        }

        private bool TryGetActiveVolume(long key, out GameObject volume)
        {
            int index = FindActiveVolumeIndex(key);
            if (index < 0)
            {
                volume = null;
                return false;
            }

            volume = _activeVolumes[index];
            return true;
        }

        private bool RemoveActiveVolume(long key)
        {
            int index = FindActiveVolumeIndex(key);
            if (index < 0)
                return false;

            int last = _activeVolumeCount - 1;
            if (index != last)
            {
                _activeVolumeKeys[index] = _activeVolumeKeys[last];
                _activeVolumes[index] = _activeVolumes[last];
            }

            _activeVolumeKeys[last] = 0L;
            _activeVolumes[last] = null;
            _activeVolumeCount = last;
            return true;
        }

        private int FindActiveVolumeIndex(long key)
        {
            for (int i = 0; i < _activeVolumeCount; i++)
            {
                if (_activeVolumeKeys[i] == key)
                    return i;
            }

            return -1;
        }

        private uint NextPendingRequestGeneration()
        {
            unchecked
            {
                _pendingRequestSequence++;
                if (_pendingRequestSequence == 0u)
                    _pendingRequestSequence = 1u;
                return _pendingRequestSequence;
            }
        }

        private bool SetPendingRequest(long key, uint generation)
        {
            int index = FindPendingRequestIndex(key);
            if (index >= 0)
            {
                _pendingRequestGenerations[index] = generation;
                return true;
            }

            if (_pendingRequestCount >= _pendingRequestKeys.Length)
                return false;

            _pendingRequestKeys[_pendingRequestCount] = key;
            _pendingRequestGenerations[_pendingRequestCount] = generation;
            _pendingRequestCount++;
            return true;
        }

        private bool ContainsPendingRequest(long key)
        {
            return FindPendingRequestIndex(key) >= 0;
        }

        private bool IsPendingRequestGeneration(long key, uint generation)
        {
            int index = FindPendingRequestIndex(key);
            return index >= 0 && _pendingRequestGenerations[index] == generation;
        }

        private int FindPendingRequestIndex(long key)
        {
            for (int i = 0; i < _pendingRequestCount; i++)
            {
                if (_pendingRequestKeys[i] == key)
                    return i;
            }

            return -1;
        }

        private bool RemovePendingRequest(long key)
        {
            int index = FindPendingRequestIndex(key);
            if (index < 0)
                return false;

            RemovePendingRequestAt(index);
            return true;
        }

        private bool RemovePendingRequest(long key, uint generation)
        {
            int index = FindPendingRequestIndex(key);
            if (index < 0 || _pendingRequestGenerations[index] != generation)
                return false;

            RemovePendingRequestAt(index);
            return true;
        }

        private void RemovePendingRequestAt(int index)
        {
            if ((uint)index >= (uint)_pendingRequestCount)
                return;

            int last = _pendingRequestCount - 1;
            if (index != last)
            {
                _pendingRequestKeys[index] = _pendingRequestKeys[last];
                _pendingRequestGenerations[index] = _pendingRequestGenerations[last];
            }

            _pendingRequestKeys[last] = 0L;
            _pendingRequestGenerations[last] = 0u;
            _pendingRequestCount = last;
        }

        private void ClearPendingRequests()
        {
            if (_pendingRequestCount > 0)
            {
                System.Array.Clear(_pendingRequestKeys, 0, _pendingRequestCount);
                System.Array.Clear(_pendingRequestGenerations, 0, _pendingRequestCount);
            }

            _pendingRequestCount = 0;
        }

        private bool SetChunkFadeState(long key, in ChunkFadeState state)
        {
            int index = FindChunkFadeStateIndex(key);
            if (index >= 0)
            {
                _chunkFadeStates[index] = state;
                return true;
            }

            if (_chunkFadeStateCount >= _chunkFadeStates.Length)
                return false;

            _chunkFadeStateKeys[_chunkFadeStateCount] = key;
            _chunkFadeStates[_chunkFadeStateCount] = state;
            _chunkFadeStateCount++;
            return true;
        }

        private int FindChunkFadeStateIndex(long key)
        {
            for (int i = 0; i < _chunkFadeStateCount; i++)
            {
                if (_chunkFadeStateKeys[i] == key)
                    return i;
            }

            return -1;
        }

        private void RemoveChunkFadeStateAt(int index)
        {
            if ((uint)index >= (uint)_chunkFadeStateCount)
                return;

            int last = _chunkFadeStateCount - 1;
            if (index != last)
            {
                _chunkFadeStateKeys[index] = _chunkFadeStateKeys[last];
                _chunkFadeStates[index] = _chunkFadeStates[last];
            }

            _chunkFadeStateKeys[last] = 0L;
            _chunkFadeStates[last] = default;
            _chunkFadeStateCount = last;
        }

        private void ClearLaunchQueue()
        {
            if (_launchQueueCount > 0)
                System.Array.Clear(_launchQueue, 0, _launchQueueCount);
            _launchQueueCount = 0;
        }

        private void RemoveLaunchQueuePrefix(int count)
        {
            int removeCount = Mathf.Clamp(count, 0, _launchQueueCount);
            if (removeCount <= 0)
                return;

            int remaining = _launchQueueCount - removeCount;
            for (int i = 0; i < remaining; i++)
                _launchQueue[i] = _launchQueue[i + removeCount];

            System.Array.Clear(_launchQueue, remaining, removeCount);
            _launchQueueCount = remaining;
        }

        private void ClearKeyScratch()
        {
            if (_keyScratchCount > 0)
                System.Array.Clear(_keyScratch, 0, _keyScratchCount);
            _keyScratchCount = 0;
        }

        private bool AddKeyScratch(long key)
        {
            if (_keyScratchCount >= _keyScratch.Length)
                return false;

            _keyScratch[_keyScratchCount++] = key;
            return true;
        }

        private void ClearPendingDespawns()
        {
            if (_pendingDespawnKeyCount > 0)
                System.Array.Clear(_pendingDespawnKeys, 0, _pendingDespawnKeyCount);
            _pendingDespawnKeyCount = 0;
        }

        private void ClearPendingChunkFadeRegistrations()
        {
            _pendingChunkFadeKeys.Clear();
        }

        private Bounds ResolveVolumeBounds(GameObject volume, Vector3 center, float radius)
        {
            if (volume != null && volume.TryGetComponent(out Renderer renderer))
                return renderer.bounds;

            return new Bounds(center, new Vector3(radius * 2f, fallbackCaveHeight, radius * 2f));
        }

        private static Vector3 ResolveRuntimePosition(in CaveEntranceRequest request)
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 runtimePosition = AUPMath.ResolveCameraRelative(in request.AbsolutePosition, in originAup);
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static long BuildHoleKey(in AbsoluteUniversePosition position, float radius)
        {
            unchecked
            {
                int localX = QuantizeTenth(position.LocalX);
                int localY = QuantizeTenth(position.LocalY);
                int localZ = QuantizeTenth(position.LocalZ);
                int r = QuantizeTenth(radius);
                long hash = 1469598103934665603L;
                hash = (hash ^ position.GridX) * 1099511628211L;
                hash = (hash ^ position.GridY) * 1099511628211L;
                hash = (hash ^ position.GridZ) * 1099511628211L;
                hash = (hash ^ localX) * 1099511628211L;
                hash = (hash ^ localY) * 1099511628211L;
                hash = (hash ^ localZ) * 1099511628211L;
                hash = (hash ^ r) * 1099511628211L;
                return hash;
            }
        }

        private static uint BuildHoleSeed(in AbsoluteUniversePosition position, float radius)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)position.GridX) * 16777619u;
                hash = (hash ^ (uint)position.GridY) * 16777619u;
                hash = (hash ^ (uint)position.GridZ) * 16777619u;
                hash = (hash ^ (uint)QuantizeTenth(position.LocalX)) * 16777619u;
                hash = (hash ^ (uint)QuantizeTenth(position.LocalY)) * 16777619u;
                hash = (hash ^ (uint)QuantizeTenth(position.LocalZ)) * 16777619u;
                hash = (hash ^ (uint)QuantizeTenth(radius)) * 16777619u;
                return hash;
            }
        }

        private static int QuantizeTenth(float value)
        {
            float scaled = value * 10f;
            return scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
        }
    }
}
