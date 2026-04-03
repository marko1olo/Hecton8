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

        public int OrdinaryAnchorCount => _ordinaryAnchors.Count;
        public int LargeThreatZoneCount => _largeThreatZones.Count;

        public void SetProceduralStateRegistry(WorldProceduralStateRegistry registry)
        {
            proceduralStateRegistry = registry;
        }

        public void Clear()
        {
            _ordinaryAnchors.Clear();
            _largeThreatZones.Clear();
            UpdateDiagnostics();
        }

        public void ReplaceProceduralAnchors(IReadOnlyList<Anchor> anchors)
        {
            _ordinaryAnchors.Clear();
            _largeThreatZones.Clear();

            if (anchors != null)
            {
                for (int i = 0; i < anchors.Count; i++)
                {
                    Anchor anchor = anchors[i];
                    if (anchor.isLargeThreatZone)
                        _largeThreatZones[anchor.runtimeKey] = anchor;
                    else
                        _ordinaryAnchors[anchor.runtimeKey] = anchor;
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

            foreach (KeyValuePair<long, Anchor> pair in _largeThreatZones)
            {
                Anchor candidate = pair.Value;
                if (!IsAnchorAvailable(candidate))
                    continue;
                if (candidate.macroZoneCoord.ChebyshevDistanceTo(observerMacroZone) > maxMacroZoneDistance)
                    continue;

                float distanceSqr = (candidate.position - observerPosition).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                anchor = candidate;
                hasBest = true;
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

            foreach (KeyValuePair<long, Anchor> pair in _ordinaryAnchors)
            {
                Anchor candidate = pair.Value;
                if (!IsAnchorAvailable(candidate))
                    continue;
                int dx = Mathf.Abs(candidate.chunkCoord.x - observerChunk.x);
                int dz = Mathf.Abs(candidate.chunkCoord.z - observerChunk.z);
                if (Mathf.Max(dx, dz) > maxChunkDistance)
                    continue;

                float distanceSqr = (candidate.position - observerPosition).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                anchor = candidate;
                hasBest = true;
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

        private void ResolveProceduralStateRegistry()
        {
            WorldRuntimeReferenceUtility.TryResolveSceneObject(ref proceduralStateRegistry);
        }
    }
}
