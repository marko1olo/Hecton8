using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldFaunaSpawnRegistry : MonoBehaviour
    {
        [System.Serializable]
        public struct Anchor
        {
            public long runtimeKey;
            public Vector3 position;
            public float radius;
            public WorldChunkCoordinate chunkCoord;
            public WorldMacroZoneCoordinate macroZoneCoord;
            public WorldStreamingLayer streamingLayer;
            public string familyId;
            public bool isLargeThreatZone;
        }

        [SerializeField] private int _debugOrdinaryAnchorCount;
        [SerializeField] private int _debugLargeThreatZoneCount;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;

        private readonly Dictionary<long, Anchor> _ordinaryAnchors = new Dictionary<long, Anchor>(128);
        private readonly Dictionary<long, Anchor> _largeThreatZones = new Dictionary<long, Anchor>(32);
        private readonly Dictionary<long, List<Anchor>> _ordinaryAnchorsByChunk = new Dictionary<long, List<Anchor>>(128);
        private readonly Dictionary<long, List<Anchor>> _largeThreatZonesByMacroZone = new Dictionary<long, List<Anchor>>(32);
        private readonly Stack<List<Anchor>> _anchorBucketPool = new Stack<List<Anchor>>(64);

        internal static WorldFaunaSpawnRegistry ActiveRuntimeInstance { get; private set; }

        public int OrdinaryAnchorCount => _ordinaryAnchors.Count;
        public int LargeThreatZoneCount => _largeThreatZones.Count;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SetProceduralStateRegistry(WorldProceduralStateRegistry registry)
        {
            proceduralStateRegistry = registry;
        }

        public void Clear()
        {
            _ordinaryAnchors.Clear();
            _largeThreatZones.Clear();
            ReleaseBucketDictionary(_ordinaryAnchorsByChunk);
            ReleaseBucketDictionary(_largeThreatZonesByMacroZone);
            UpdateDiagnostics();
        }

        public void ReplaceProceduralAnchors(IReadOnlyList<Anchor> anchors)
        {
            _ordinaryAnchors.Clear();
            _largeThreatZones.Clear();
            ReleaseBucketDictionary(_ordinaryAnchorsByChunk);
            ReleaseBucketDictionary(_largeThreatZonesByMacroZone);

            if (anchors != null)
            {
                for (int i = 0; i < anchors.Count; i++)
                {
                    Anchor anchor = anchors[i];
                    if (anchor.isLargeThreatZone)
                    {
                        _largeThreatZones[anchor.runtimeKey] = anchor;
                        AddAnchorToBucket(_largeThreatZonesByMacroZone, ComposeMacroZoneKey(anchor.macroZoneCoord), anchor);
                    }
                    else
                    {
                        _ordinaryAnchors[anchor.runtimeKey] = anchor;
                        AddAnchorToBucket(_ordinaryAnchorsByChunk, ComposeChunkKey(anchor.chunkCoord), anchor);
                    }
                }
            }

            UpdateDiagnostics();
        }

        public bool TryGetOrdinaryAnchor(
            Vector3 observerPosition,
            WorldChunkCoordinate observerChunk,
            int maxChunkDistance,
            out Anchor anchor)
        {
            return TryGetNearestOrdinaryAnchor(observerPosition, observerChunk, maxChunkDistance, out anchor);
        }

        public bool TryGetLargeThreatZone(
            Vector3 observerPosition,
            WorldMacroZoneCoordinate observerMacroZone,
            int maxMacroZoneDistance,
            out Anchor anchor)
        {
            float bestDistanceSqr = float.MaxValue;
            bool hasBest = false;
            anchor = default;
            ResolveProceduralStateRegistry();

            for (int dz = -maxMacroZoneDistance; dz <= maxMacroZoneDistance; dz++)
            {
                int zoneZ = observerMacroZone.z + dz;
                for (int dx = -maxMacroZoneDistance; dx <= maxMacroZoneDistance; dx++)
                {
                    long bucketKey = ComposeMacroZoneKey(new WorldMacroZoneCoordinate(observerMacroZone.x + dx, zoneZ));
                    if (!_largeThreatZonesByMacroZone.TryGetValue(bucketKey, out List<Anchor> bucket) || bucket == null)
                        continue;

                    int count = bucket.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Anchor candidate = bucket[i];
                        if (!IsAnchorAvailable(candidate))
                            continue;

                        float distanceSqr = (candidate.position - observerPosition).sqrMagnitude;
                        if (distanceSqr >= bestDistanceSqr)
                            continue;

                        bestDistanceSqr = distanceSqr;
                        anchor = candidate;
                        hasBest = true;
                    }
                }
            }

            return hasBest;
        }

        private bool TryGetNearestOrdinaryAnchor(
            Vector3 observerPosition,
            WorldChunkCoordinate observerChunk,
            int maxChunkDistance,
            out Anchor anchor)
        {
            float bestDistanceSqr = float.MaxValue;
            bool hasBest = false;
            anchor = default;
            ResolveProceduralStateRegistry();

            for (int dz = -maxChunkDistance; dz <= maxChunkDistance; dz++)
            {
                int chunkZ = observerChunk.z + dz;
                for (int dx = -maxChunkDistance; dx <= maxChunkDistance; dx++)
                {
                    long bucketKey = ComposeChunkKey(new WorldChunkCoordinate(observerChunk.x + dx, chunkZ));
                    if (!_ordinaryAnchorsByChunk.TryGetValue(bucketKey, out List<Anchor> bucket) || bucket == null)
                        continue;

                    int count = bucket.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Anchor candidate = bucket[i];
                        if (!IsAnchorAvailable(candidate))
                            continue;

                        float distanceSqr = (candidate.position - observerPosition).sqrMagnitude;
                        if (distanceSqr >= bestDistanceSqr)
                            continue;

                        bestDistanceSqr = distanceSqr;
                        anchor = candidate;
                        hasBest = true;
                    }
                }
            }

            return hasBest;
        }

        private void UpdateDiagnostics()
        {
            _debugOrdinaryAnchorCount = _ordinaryAnchors.Count;
            _debugLargeThreatZoneCount = _largeThreatZones.Count;
        }

        private bool IsAnchorAvailable(in Anchor anchor)
        {
            return proceduralStateRegistry == null
                || proceduralStateRegistry.IsFaunaAnchorAvailable(anchor.runtimeKey, anchor.isLargeThreatZone);
        }

        private void AddAnchorToBucket(Dictionary<long, List<Anchor>> bucketMap, long bucketKey, in Anchor anchor)
        {
            if (!bucketMap.TryGetValue(bucketKey, out List<Anchor> bucket) || bucket == null)
            {
                bucket = GetPooledBucket();
                bucketMap[bucketKey] = bucket;
            }

            bucket.Add(anchor);
        }

        private void ReleaseBucketDictionary(Dictionary<long, List<Anchor>> bucketMap)
        {
            Dictionary<long, List<Anchor>>.ValueCollection.Enumerator enumerator = bucketMap.Values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                List<Anchor> bucket = enumerator.Current;
                if (bucket == null)
                    continue;

                bucket.Clear();
                _anchorBucketPool.Push(bucket);
            }

            bucketMap.Clear();
        }

        private List<Anchor> GetPooledBucket()
        {
            if (_anchorBucketPool.Count > 0)
                return _anchorBucketPool.Pop();

            return new List<Anchor>(4); // COLD ALLOC: per-bucket fauna anchor cache
        }

        private void ResolveProceduralStateRegistry()
        {
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralStateRegistry(ref proceduralStateRegistry);
        }

        private static long ComposeChunkKey(WorldChunkCoordinate chunkCoord)
        {
            return ((long)chunkCoord.x << 32) ^ (uint)chunkCoord.z;
        }

        private static long ComposeMacroZoneKey(WorldMacroZoneCoordinate macroZoneCoord)
        {
            return ((long)macroZoneCoord.x << 32) ^ (uint)macroZoneCoord.z;
        }
    }
}
