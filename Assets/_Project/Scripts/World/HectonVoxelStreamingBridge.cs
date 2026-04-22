using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
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
            public Vector3 Position;
            public float Radius;
            public float Priority;
            public uint Seed;
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

        private readonly Dictionary<long, GameObject> _activeVolumes = new Dictionary<long, GameObject>(16);
        private readonly Dictionary<long, CaveEntranceRequest> _desiredRequests = new Dictionary<long, CaveEntranceRequest>(32);
        private readonly Dictionary<long, PendingRequestState> _pendingRequests = new Dictionary<long, PendingRequestState>(16);
        private readonly List<CaveEntranceRequest> _launchQueue = new List<CaveEntranceRequest>(16);
        private readonly List<long> _keyScratch = new List<long>(16);
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
            DespawnAllVolumes();
        }

        private void OnDestroy()
        {
            TryUnregister();
            CancelAllPendingRequests();
            CancelLifetimeCancellation();
            DespawnAllVolumes();
        }

        /// <summary>
        /// Launches bounded async voxel cave requests prepared during the last slow tick.
        /// </summary>
        public void Tick(float dt)
        {
            ResolveReferences();
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
                Vector3 caveCenter = request.Position + (Vector3.down * Mathf.Max(1f, caveVerticalOffset));
                GameObject volume = await voxelEngine.GenerateVolumeAsync(caveCenter, request.Seed, preset, token);
                if (volume == null)
                    return;

                if (!isActiveAndEnabled || !_desiredRequests.ContainsKey(request.Key))
                {
                    voxelEngine.DespawnVolume(volume);
                    return;
                }

                volume.name = $"VoxelCave_{request.Key}";
                _activeVolumes[request.Key] = volume;
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

                float distanceSq = (hole.Position - playerPosition).sqrMagnitude;
                if (distanceSq > requestDistanceSq && !_activeVolumes.ContainsKey(BuildHoleKey(hole.Position, hole.Radius)))
                    continue;

                CaveEntranceRequest request = new CaveEntranceRequest
                {
                    Key = BuildHoleKey(hole.Position, hole.Radius),
                    Position = hole.Position,
                    Radius = Mathf.Max(4f, hole.Radius),
                    Priority = distanceSq,
                    Seed = BuildHoleSeed(hole.Position, hole.Radius)
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

                if ((request.Position - playerPosition).sqrMagnitude > retentionDistanceSq)
                    _keyScratch.Add(key);
            }

            for (int i = 0; i < _keyScratch.Count; i++)
            {
                long key = _keyScratch[i];
                if (!_activeVolumes.TryGetValue(key, out GameObject volume))
                    continue;

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
                if (_activeVolumes.TryGetValue(key, out GameObject volume) && volume != null)
                    voxelEngine.DespawnVolume(volume);

                _activeVolumes.Remove(key);
            }
        }

        private void ResolveReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private void TryRegister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (_registeredTick)
            {
                tickManager.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                tickManager.Unregister((ISlowTickable)this);
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

        private static long BuildHoleKey(Vector3 position, float radius)
        {
            unchecked
            {
                int x = Mathf.RoundToInt(position.x * 10f);
                int y = Mathf.RoundToInt(position.y * 10f);
                int z = Mathf.RoundToInt(position.z * 10f);
                int r = Mathf.RoundToInt(radius * 10f);
                long hash = 1469598103934665603L;
                hash = (hash ^ x) * 1099511628211L;
                hash = (hash ^ y) * 1099511628211L;
                hash = (hash ^ z) * 1099511628211L;
                hash = (hash ^ r) * 1099511628211L;
                return hash;
            }
        }

        private static uint BuildHoleSeed(Vector3 position, float radius)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.x * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.y * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.z * 10f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(radius * 10f)) * 16777619u;
                return hash;
            }
        }
    }
}
