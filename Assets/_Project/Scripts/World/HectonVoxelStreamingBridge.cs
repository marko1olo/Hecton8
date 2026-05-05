using System;
using System.Collections.Generic;
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
    public sealed class HectonVoxelStreamingBridge : MonoBehaviour, ITickable, ISlowTickable
    {
        private sealed class PendingRequestState : IDisposable
        {
            public CancellationTokenSource Cancellation;

            public void Dispose()
            {
                if (Cancellation == null)
                    return;

                Cancellation.Dispose();
                Cancellation = null;
            }
        }

        private struct CaveEntranceRequest
        {
            public long Key;
            public int HoleId;
            public AbsoluteUniversePosition AbsolutePosition;
            public float Radius;
            public float Priority;
            public uint Seed;
        }

        private sealed class ChunkFadeState
        {
            public GameObject Volume;
            public Renderer Renderer;
            public Material OriginalMaterial;
            public Material RuntimeMaterial;
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

        private readonly Dictionary<long, GameObject> _activeVolumes = new Dictionary<long, GameObject>(16);
        private readonly Dictionary<long, CaveEntranceRequest> _desiredRequests = new Dictionary<long, CaveEntranceRequest>(32);
        private readonly Dictionary<long, PendingRequestState> _pendingRequests = new Dictionary<long, PendingRequestState>(16);
        // COLD ALLOC: Dictionary<long, ChunkFadeState>[16] - temporary streamed voxel chunk dissolve states - owner: HectonVoxelStreamingBridge
        private readonly Dictionary<long, ChunkFadeState> _chunkFadeStates = new Dictionary<long, ChunkFadeState>(16);
        private readonly List<CaveEntranceRequest> _launchQueue = new List<CaveEntranceRequest>(16);
        private readonly List<long> _keyScratch = new List<long>(16);
        private static readonly int ChunkDissolveFadeId = Shader.PropertyToID("_ChunkDissolveFade");
        private const uint ChunkFadeRendererMissingWarningHash = 0xD0B2923Bu;
        private const uint ChunkFadeMaterialMissingWarningHash = 0xBBEEF2CDu;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            maxRuntimeVolumes = Mathf.Max(1, maxRuntimeVolumes);
            maxAsyncLaunchesPerTick = Mathf.Max(1, maxAsyncLaunchesPerTick);
            requestDistance = Mathf.Max(25f, requestDistance);
            retentionDistance = Mathf.Max(requestDistance, retentionDistance);
            caveVerticalOffset = Mathf.Max(1f, caveVerticalOffset);
            fallbackCaveHeight = Mathf.Max(4f, fallbackCaveHeight);
            chunkFadeInDuration = Mathf.Max(0.05f, chunkFadeInDuration);
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureLifetimeCancellation();
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            CancelAllPendingRequests();
            CancelLifetimeCancellation();
            _launchQueue.Clear();
            _desiredRequests.Clear();
            DespawnAllVolumes();
        }

        private void OnDestroy()
        {
            TryUnregister();
            CancelAllPendingRequests();
            CancelLifetimeCancellation();
            _launchQueue.Clear();
            _desiredRequests.Clear();
            DespawnAllVolumes();
        }

        /// <summary>
        /// Launches bounded async voxel cave requests prepared during the last slow tick.
        /// </summary>
        public void Tick(float dt)
        {
            ResolveReferences();
            TickChunkFade(dt);

            if (voxelEngine == null || _launchQueue.Count <= 0 || _activeVolumes.Count >= maxRuntimeVolumes)
                return;

            int launchCount = Mathf.Min(maxAsyncLaunchesPerTick, _launchQueue.Count);
            for (int i = 0; i < launchCount; i++)
            {
                CaveEntranceRequest request = _launchQueue[i];
                if (_activeVolumes.ContainsKey(request.Key) || _pendingRequests.ContainsKey(request.Key))
                    continue;

                PendingRequestState pending = CreatePendingRequestState();
                _pendingRequests[request.Key] = pending;
                _ = SpawnCaveAsync(request, pending);
            }

            if (launchCount > 0)
                _launchQueue.RemoveRange(0, launchCount);
        }

        /// <summary>
        /// Rebuilds cave-entrance streaming intent from the current terrain-hole snapshot.
        /// </summary>
        public void SlowTick()
        {
            ResolveReferences();
            RebuildDesiredRequests();
            CancelStalePendingRequests();
            DespawnStaleVolumes();
            RebuildLaunchQueue();
        }

        private async Awaitable SpawnCaveAsync(CaveEntranceRequest request, PendingRequestState pendingState)
        {
            CancellationToken token = pendingState != null && pendingState.Cancellation != null
                ? pendingState.Cancellation.Token
                : EnsureLifetimeCancellation().Token;

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

                if (!isActiveAndEnabled || !_desiredRequests.ContainsKey(request.Key))
                {
                    voxelEngine.DespawnVolume(volume);
                    return;
                }

                volume.name = $"VoxelCave_{request.Key}";
                _activeVolumes[request.Key] = volume;
                RegisterChunkFade(request.Key, volume);
                if (vegetationBridge != null)
                    vegetationBridge.RegisterArtificialStructure(ResolveVolumeBounds(volume, caveCenter, request.Radius), StructureType.VoxelCave);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_pendingRequests.TryGetValue(request.Key, out PendingRequestState current) && ReferenceEquals(current, pendingState))
                    _pendingRequests.Remove(request.Key);

                pendingState?.Dispose();
            }
        }

        private void RebuildDesiredRequests()
        {
            _desiredRequests.Clear();
            if (vegetationBridge == null || voxelEngine == null)
                return;

            if (!vegetationBridge.TryGetTerrainHoleStreamingPayload(out var holes, out int holeCount) || !holes.IsCreated || holeCount <= 0)
                return;

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;
            float requestDistanceSq = requestDistance * requestDistance;
            for (int i = 0; i < holeCount; i++)
            {
                TerrainHoleStreamingRecord hole = holes[i];
                if (hole.SourceType != TerrainHoleSourceType.CaveEntrance)
                    continue;

                AbsoluteUniversePosition absoluteHolePosition = AbsoluteUniversePosition.FromRuntimePosition(hole.Position);
                long requestKey = hole.HoleId != 0
                    ? hole.HoleId
                    : BuildHoleKey(absoluteHolePosition, hole.Radius);
                float distanceSq = (hole.Position - playerPosition).sqrMagnitude;
                if (distanceSq > requestDistanceSq && !_activeVolumes.ContainsKey(requestKey))
                    continue;

                CaveEntranceRequest request = new CaveEntranceRequest
                {
                    Key = requestKey,
                    HoleId = hole.HoleId,
                    AbsolutePosition = absoluteHolePosition,
                    Radius = Mathf.Max(4f, hole.Radius),
                    Priority = distanceSq,
                    Seed = BuildHoleSeed(absoluteHolePosition, hole.Radius)
                };
                _desiredRequests[request.Key] = request;
                if (_desiredRequests.Count >= maxRuntimeVolumes * 2)
                    break;
            }
        }

        private void CancelStalePendingRequests()
        {
            if (_pendingRequests.Count <= 0)
                return;

            _keyScratch.Clear();
            Dictionary<long, PendingRequestState>.Enumerator enumerator = _pendingRequests.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!_desiredRequests.ContainsKey(enumerator.Current.Key))
                    _keyScratch.Add(enumerator.Current.Key);
            }

            for (int i = 0; i < _keyScratch.Count; i++)
            {
                long key = _keyScratch[i];
                if (!_pendingRequests.TryGetValue(key, out PendingRequestState pending))
                    continue;

                pending.Cancellation?.Cancel();
                _pendingRequests.Remove(key);
                pending.Dispose();
            }
        }

        private void DespawnStaleVolumes()
        {
            if (_activeVolumes.Count <= 0)
                return;

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;
            float retentionDistanceSq = retentionDistance * retentionDistance;
            _keyScratch.Clear();
            Dictionary<long, GameObject>.Enumerator enumerator = _activeVolumes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long key = enumerator.Current.Key;
                if (!_desiredRequests.TryGetValue(key, out CaveEntranceRequest request))
                {
                    _keyScratch.Add(key);
                    continue;
                }

                if ((ResolveRuntimePosition(in request) - playerPosition).sqrMagnitude > retentionDistanceSq)
                    _keyScratch.Add(key);
            }

            for (int i = 0; i < _keyScratch.Count; i++)
            {
                long key = _keyScratch[i];
                if (!_activeVolumes.TryGetValue(key, out GameObject volume))
                    continue;

                ClearChunkFadeState(key, clearRenderer: true);

                if (voxelEngine != null && volume != null)
                    voxelEngine.DespawnVolume(volume);

                _activeVolumes.Remove(key);
            }
        }

        private void RebuildLaunchQueue()
        {
            _launchQueue.Clear();
            if (_activeVolumes.Count >= maxRuntimeVolumes)
                return;

            Dictionary<long, CaveEntranceRequest>.Enumerator enumerator = _desiredRequests.GetEnumerator();
            while (enumerator.MoveNext())
            {
                CaveEntranceRequest request = enumerator.Current.Value;
                if (_activeVolumes.ContainsKey(request.Key) || _pendingRequests.ContainsKey(request.Key))
                    continue;

                InsertQueuedRequest(request);
            }
        }

        private void InsertQueuedRequest(CaveEntranceRequest request)
        {
            int insertIndex = _launchQueue.Count;
            while (insertIndex > 0 && request.Priority < _launchQueue[insertIndex - 1].Priority)
                insertIndex--;

            _launchQueue.Insert(insertIndex, request);
        }

        private void DespawnAllVolumes()
        {
            if (voxelEngine == null || _activeVolumes.Count <= 0)
            {
                ClearAllChunkFadeStates(clearRenderers: true);
                _activeVolumes.Clear();
                return;
            }

            _keyScratch.Clear();
            Dictionary<long, GameObject>.Enumerator enumerator = _activeVolumes.GetEnumerator();
            while (enumerator.MoveNext())
                _keyScratch.Add(enumerator.Current.Key);

            for (int i = 0; i < _keyScratch.Count; i++)
            {
                long key = _keyScratch[i];
                ClearChunkFadeState(key, clearRenderer: true);
                if (_activeVolumes.TryGetValue(key, out GameObject volume) && volume != null)
                    voxelEngine.DespawnVolume(volume);

                _activeVolumes.Remove(key);
            }
        }

        private void RegisterChunkFade(long key, GameObject volume)
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

            Material runtimeMaterial = new Material(material); // COLD ALLOC: Material[1] - temporary first-party voxel dissolve material restored after fade - owner: HectonVoxelStreamingBridge
            runtimeMaterial.SetFloat(ChunkDissolveFadeId, 0f);
            renderer.sharedMaterial = runtimeMaterial;

            _chunkFadeStates[key] = new ChunkFadeState // COLD ALLOC: ChunkFadeState[1] - per-active streamed voxel chunk dissolve state - owner: HectonVoxelStreamingBridge
            {
                Volume = volume,
                Renderer = renderer,
                OriginalMaterial = material,
                RuntimeMaterial = runtimeMaterial,
                Elapsed = 0f
            };
        }

        private void TickChunkFade(float dt)
        {
            if (_chunkFadeStates.Count <= 0)
                return;

            float safeDt = Mathf.Max(0f, dt);
            float duration = Mathf.Max(0.05f, chunkFadeInDuration);
            _keyScratch.Clear();

            Dictionary<long, ChunkFadeState>.Enumerator enumerator = _chunkFadeStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long key = enumerator.Current.Key;
                ChunkFadeState state = enumerator.Current.Value;
                if (state == null || state.Renderer == null || state.RuntimeMaterial == null)
                {
                    _keyScratch.Add(key);
                    continue;
                }

                state.Elapsed += safeDt;
                float fade01 = Mathf.Clamp01(state.Elapsed / duration);
                state.RuntimeMaterial.SetFloat(ChunkDissolveFadeId, fade01);

                if (fade01 >= 0.999f)
                    _keyScratch.Add(key);
            }

            for (int i = 0; i < _keyScratch.Count; i++)
                ClearChunkFadeState(_keyScratch[i], clearRenderer: true);
        }

        private void ClearChunkFadeState(long key, bool clearRenderer)
        {
            if (!_chunkFadeStates.TryGetValue(key, out ChunkFadeState state))
                return;

            if (clearRenderer && state != null && state.Renderer != null)
                state.Renderer.sharedMaterial = state.OriginalMaterial;

            if (state != null && state.RuntimeMaterial != null)
                Destroy(state.RuntimeMaterial);

            _chunkFadeStates.Remove(key);
        }

        private static void PublishChunkFadeWarning(uint warningHash, long key, float scalar)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                unchecked((uint)key),
                Mathf.Max(0f, scalar));
        }

        private void ClearAllChunkFadeStates(bool clearRenderers)
        {
            if (_chunkFadeStates.Count <= 0)
                return;

            _keyScratch.Clear();
            Dictionary<long, ChunkFadeState>.Enumerator enumerator = _chunkFadeStates.GetEnumerator();
            while (enumerator.MoveNext())
                _keyScratch.Add(enumerator.Current.Key);

            for (int i = 0; i < _keyScratch.Count; i++)
                ClearChunkFadeState(_keyScratch[i], clearRenderers);
        }

        private void ResolveReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }
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
        }

        private PendingRequestState CreatePendingRequestState()
        {
            CancellationTokenSource lifetime = EnsureLifetimeCancellation();
            return new PendingRequestState
            {
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)
            };
        }

        private CancellationTokenSource EnsureLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                _lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

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
            if (_pendingRequests.Count <= 0)
                return;

            _keyScratch.Clear();
            Dictionary<long, PendingRequestState>.Enumerator enumerator = _pendingRequests.GetEnumerator();
            while (enumerator.MoveNext())
                _keyScratch.Add(enumerator.Current.Key);

            for (int i = 0; i < _keyScratch.Count; i++)
            {
                long key = _keyScratch[i];
                if (!_pendingRequests.TryGetValue(key, out PendingRequestState pending))
                    continue;

                pending.Cancellation?.Cancel();
                _pendingRequests.Remove(key);
                pending.Dispose();
            }
        }

        private Bounds ResolveVolumeBounds(GameObject volume, Vector3 center, float radius)
        {
            if (volume != null && volume.TryGetComponent(out Renderer renderer))
                return renderer.bounds;

            return new Bounds(center, new Vector3(radius * 2f, fallbackCaveHeight, radius * 2f));
        }

        private static Vector3 ResolveRuntimePosition(in CaveEntranceRequest request)
        {
            float3 runtimePosition = request.AbsolutePosition.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static long BuildHoleKey(in AbsoluteUniversePosition position, float radius)
        {
            unchecked
            {
                int localX = Mathf.RoundToInt(position.LocalX * 10f);
                int localY = Mathf.RoundToInt(position.LocalY * 10f);
                int localZ = Mathf.RoundToInt(position.LocalZ * 10f);
                int r = Mathf.RoundToInt(radius * 10f);
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
                hash = (hash ^ (uint)Mathf.RoundToInt(position.LocalX * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.LocalY * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.LocalZ * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(radius * 10f)) * 16777619u;
                return hash;
            }
        }
    }
}
