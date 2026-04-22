using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using UnityEngine;

namespace Hecton8.World
{
    [System.Flags]
    internal enum SpatialTargetKind
    {
        None = 0,
        Resource = 1 << 0,
        Bioform = 1 << 1,
        Signal = 1 << 2,
        Pickup = 1 << 3,
        Scannable = 1 << 4,
        Module = 1 << 5
    }

    internal readonly struct SpatialQueryHit
    {
        public SpatialQueryHit(
            Transform transform,
            Component owner,
            Vector3 position,
            float distanceSqr,
            SpatialTargetKind kind,
            FieldTargetRole signalRole,
            int speciesId,
            int layer)
        {
            Transform = transform;
            Owner = owner;
            Position = position;
            DistanceSqr = distanceSqr;
            Kind = kind;
            SignalRole = signalRole;
            SpeciesId = speciesId;
            Layer = layer;
        }

        public Transform Transform { get; }
        public Component Owner { get; }
        public Vector3 Position { get; }
        public float DistanceSqr { get; }
        public SpatialTargetKind Kind { get; }
        public FieldTargetRole SignalRole { get; }
        public int SpeciesId { get; }
        public int Layer { get; }
    }

    /// <summary>
    /// Global runtime spatial registry for zero-GC mathematical queries over resources, fauna, and authored field signals.
    /// </summary>
    internal static class WorldSpatialHashGrid
    {
        private const float CellSize = 20f;
        private const int CoordinateBits = 21;
        private const int CoordinateBias = 1 << 20;
        private const long CoordinateMask = (1L << CoordinateBits) - 1L;

        private sealed class Entry
        {
            public Transform Transform;
            public Component Owner;
            public SpatialTargetKind Kind;
            public FieldTargetRole SignalRole;
            public int SpeciesId;
            public int Layer;
            public long CellKey;
        }

        // COLD ALLOC: Dictionary<int, Entry>(256) — runtime spatial entry registry — owner: WorldSpatialHashGrid
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(256);
        // COLD ALLOC: Dictionary<long, List<int>>(128) — runtime spatial cell buckets — owner: WorldSpatialHashGrid
        private static readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>(128);
        private static int _nextHandle = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            _cells.Clear();
            _nextHandle = 1;
        }

        public static int RegisterResource(ResourceNode node)
        {
            return Register(node, node != null ? node.transform : null, SpatialTargetKind.Resource, FieldTargetRole.ResourceNodeActive, 0);
        }

        public static int RegisterBioform(FaunaBrain brain)
        {
            int speciesId = 0;
            if (brain != null && brain.SpeciesProfile != null)
                speciesId = brain.SpeciesProfile.speciesID;

            return Register(brain, brain != null ? brain.transform : null, SpatialTargetKind.Bioform, FieldTargetRole.Generic, speciesId);
        }

        public static int RegisterSignal(FieldTargetDescriptor descriptor)
        {
            FieldTargetRole role = descriptor != null ? descriptor.Role : FieldTargetRole.Generic;
            return Register(descriptor, descriptor != null ? descriptor.transform : null, SpatialTargetKind.Signal, role, 0);
        }

        public static int RegisterSignal(Component owner, Transform targetTransform, FieldTargetRole signalRole)
        {
            return Register(owner, targetTransform, SpatialTargetKind.Signal, signalRole, 0);
        }

        public static int RegisterSignal(DeployableFlare flare)
        {
            return Register(flare, flare != null ? flare.transform : null, SpatialTargetKind.Signal, FieldTargetRole.Generic, 0);
        }

        public static int RegisterPickup(PickupItem pickup)
        {
            return Register(pickup, pickup != null ? pickup.transform : null, SpatialTargetKind.Pickup, FieldTargetRole.Generic, 0);
        }

        public static int RegisterScannable(ScannableTarget scannable)
        {
            return Register(scannable, scannable != null ? scannable.transform : null, SpatialTargetKind.Scannable, FieldTargetRole.Generic, 0);
        }

        public static int RegisterModule(ModuleMarker marker)
        {
            FieldTargetRole role = marker != null ? marker.SpatialRole : FieldTargetRole.Generic;
            return Register(marker, marker != null ? marker.transform : null, SpatialTargetKind.Module, role, 0);
        }

        public static void UpdateSignalRole(int handle, FieldTargetRole signalRole)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            entry.SignalRole = signalRole;
        }

        public static void UpdateGridPosition(GameObject obj, Vector3 oldPosition, Vector3 newPosition)
        {
            if (obj == null)
                return;

            int handle = FindHandle(obj.transform);
            if (handle != 0)
                UpdateGridPosition(handle, oldPosition, newPosition);
        }

        public static void UpdateGridPosition(int handle, Vector3 oldPosition, Vector3 newPosition)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            if (entry.Transform == null)
            {
                Unregister(handle);
                return;
            }

            long previousCellKey = GetCellKey(oldPosition);
            long nextCellKey = GetCellKey(newPosition);
            if (previousCellKey == nextCellKey || entry.CellKey == nextCellKey)
                return;

            RemoveFromCell(handle, entry.CellKey);
            AddToCell(handle, nextCellKey);
            entry.CellKey = nextCellKey;
            entry.Layer = entry.Transform.gameObject.layer;
        }

        public static void Refresh(int handle)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            if (entry.Transform == null)
            {
                Unregister(handle);
                return;
            }

            long nextCellKey = GetCellKey(entry.Transform.position);
            if (nextCellKey == entry.CellKey)
                return;

            RemoveFromCell(handle, entry.CellKey);
            AddToCell(handle, nextCellKey);
            entry.CellKey = nextCellKey;
        }

        public static void Unregister(int handle)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            RemoveFromCell(handle, entry.CellKey);
            _entries.Remove(handle);
        }

        public static bool TryGetNearestBioform(
            Vector3 origin,
            float radius,
            int layerMask,
            Transform ignoreTransform,
            int excludedSpeciesId,
            bool requirePreyTag,
            out SpatialQueryHit hit)
        {
            SpatialQueryHit bestHit = default;
            bool found = false;
            float bestDistanceSqr = radius * radius;
            int minCellX = ToCell(origin.x - radius);
            int maxCellX = ToCell(origin.x + radius);
            int minCellY = ToCell(origin.y - radius);
            int maxCellY = ToCell(origin.y + radius);
            int minCellZ = ToCell(origin.z - radius);
            int maxCellZ = ToCell(origin.z + radius);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    for (int z = minCellZ; z <= maxCellZ; z++)
                    {
                        long cellKey = PackCell(x, y, z);
                        if (!_cells.TryGetValue(cellKey, out List<int> bucket))
                            continue;

                        int bucketCount = bucket.Count;
                        for (int i = 0; i < bucketCount; i++)
                        {
                            int handle = bucket[i];
                            if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                                continue;

                            if ((entry.Kind & SpatialTargetKind.Bioform) == 0)
                                continue;

                            Transform candidateTransform = entry.Transform;
                            if (candidateTransform == null || candidateTransform == ignoreTransform)
                                continue;

                            if (!MatchesLayer(entry.Layer, layerMask))
                                continue;

                            if (excludedSpeciesId >= 0 && entry.SpeciesId == excludedSpeciesId)
                                continue;

                            if (requirePreyTag && !candidateTransform.CompareTag("Prey"))
                                continue;

                            Vector3 position = candidateTransform.position;
                            float distanceSqr = (position - origin).sqrMagnitude;
                            if (distanceSqr > bestDistanceSqr)
                                continue;

                            bestDistanceSqr = distanceSqr;
                            bestHit = new SpatialQueryHit(
                                candidateTransform,
                                entry.Owner,
                                position,
                                distanceSqr,
                                entry.Kind,
                                entry.SignalRole,
                                entry.SpeciesId,
                                entry.Layer);
                            found = true;
                        }
                    }
                }
            }

            hit = bestHit;
            return found;
        }

        public static bool TryGetNearestAggressiveBioform(
            Vector3 origin,
            float radius,
            int layerMask,
            Transform ignoreTransform,
            out SpatialQueryHit hit)
        {
            SpatialQueryHit bestHit = default;
            bool found = false;
            float bestDistanceSqr = radius * radius;
            int minCellX = ToCell(origin.x - radius);
            int maxCellX = ToCell(origin.x + radius);
            int minCellY = ToCell(origin.y - radius);
            int maxCellY = ToCell(origin.y + radius);
            int minCellZ = ToCell(origin.z - radius);
            int maxCellZ = ToCell(origin.z + radius);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    for (int z = minCellZ; z <= maxCellZ; z++)
                    {
                        long cellKey = PackCell(x, y, z);
                        if (!_cells.TryGetValue(cellKey, out List<int> bucket))
                            continue;

                        int bucketCount = bucket.Count;
                        for (int i = 0; i < bucketCount; i++)
                        {
                            int handle = bucket[i];
                            if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                                continue;

                            if ((entry.Kind & SpatialTargetKind.Bioform) == 0)
                                continue;

                            Transform candidateTransform = entry.Transform;
                            if (candidateTransform == null || candidateTransform == ignoreTransform)
                                continue;

                            if (!MatchesLayer(entry.Layer, layerMask))
                                continue;

                            FaunaBrain brain = entry.Owner as FaunaBrain;
                            if (brain == null || !brain.isAggressive)
                                continue;

                            Vector3 position = candidateTransform.position;
                            float distanceSqr = (position - origin).sqrMagnitude;
                            if (distanceSqr > bestDistanceSqr)
                                continue;

                            bestDistanceSqr = distanceSqr;
                            bestHit = new SpatialQueryHit(
                                candidateTransform,
                                entry.Owner,
                                position,
                                distanceSqr,
                                entry.Kind,
                                entry.SignalRole,
                                entry.SpeciesId,
                                entry.Layer);
                            found = true;
                        }
                    }
                }
            }

            hit = bestHit;
            return found;
        }

        public static void BuildSonarSnapshot(Vector3 origin, float radius, out SpatialSonarSnapshot snapshot)
        {
            int resourceCount = 0;
            int bioformCount = 0;
            int signalCount = 0;

            bool hasNearestResource = false;
            bool hasNearestBioform = false;
            bool hasNearestSignal = false;
            float nearestResourceDistanceSqr = float.MaxValue;
            float nearestBioformDistanceSqr = float.MaxValue;
            float nearestSignalDistanceSqr = float.MaxValue;
            FieldTargetRole nearestSignalRole = FieldTargetRole.Generic;
            float radiusSqr = radius * radius;
            int minCellX = ToCell(origin.x - radius);
            int maxCellX = ToCell(origin.x + radius);
            int minCellY = ToCell(origin.y - radius);
            int maxCellY = ToCell(origin.y + radius);
            int minCellZ = ToCell(origin.z - radius);
            int maxCellZ = ToCell(origin.z + radius);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    for (int z = minCellZ; z <= maxCellZ; z++)
                    {
                        long cellKey = PackCell(x, y, z);
                        if (!_cells.TryGetValue(cellKey, out List<int> bucket))
                            continue;

                        int bucketCount = bucket.Count;
                        for (int i = 0; i < bucketCount; i++)
                        {
                            int handle = bucket[i];
                            if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                                continue;

                            SpatialTargetKind kind = entry.Kind;
                            if ((kind & (SpatialTargetKind.Resource | SpatialTargetKind.Bioform | SpatialTargetKind.Signal)) == 0)
                                continue;

                            Transform candidateTransform = entry.Transform;
                            if (candidateTransform == null)
                                continue;

                            Vector3 position = candidateTransform.position;
                            float distanceSqr = (position - origin).sqrMagnitude;
                            if (distanceSqr > radiusSqr)
                                continue;

                            if ((kind & SpatialTargetKind.Resource) != 0)
                            {
                                resourceCount++;
                                if (distanceSqr < nearestResourceDistanceSqr)
                                {
                                    nearestResourceDistanceSqr = distanceSqr;
                                    hasNearestResource = true;
                                }

                                continue;
                            }

                            if ((kind & SpatialTargetKind.Bioform) != 0)
                            {
                                bioformCount++;
                                if (distanceSqr < nearestBioformDistanceSqr)
                                {
                                    nearestBioformDistanceSqr = distanceSqr;
                                    hasNearestBioform = true;
                                }

                                continue;
                            }

                            bool isSpectrumSignal =
                                (kind & SpatialTargetKind.Signal) != 0 ||
                                ((kind & SpatialTargetKind.Module) != 0 && IsSpectrumSignalRole(entry.SignalRole));

                            if (!isSpectrumSignal)
                                continue;

                            signalCount++;
                            if (distanceSqr < nearestSignalDistanceSqr)
                            {
                                nearestSignalDistanceSqr = distanceSqr;
                                nearestSignalRole = entry.SignalRole;
                                hasNearestSignal = true;
                            }
                        }
                    }
                }
            }

            snapshot = new SpatialSonarSnapshot(
                resourceCount,
                bioformCount,
                signalCount,
                hasNearestResource,
                hasNearestResource ? ClampDistanceToHud(nearestResourceDistanceSqr) : 0,
                hasNearestBioform,
                hasNearestBioform ? ClampDistanceToHud(nearestBioformDistanceSqr) : 0,
                hasNearestSignal,
                hasNearestSignal ? ClampDistanceToHud(nearestSignalDistanceSqr) : 0,
                nearestSignalRole);
        }

        public static int CollectContactsNonAlloc(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            SpatialQueryHit[] results)
        {
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None)
                return 0;

            int count = 0;
            float radiusSqr = radius * radius;
            int minCellX = ToCell(origin.x - radius);
            int maxCellX = ToCell(origin.x + radius);
            int minCellY = ToCell(origin.y - radius);
            int maxCellY = ToCell(origin.y + radius);
            int minCellZ = ToCell(origin.z - radius);
            int maxCellZ = ToCell(origin.z + radius);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    for (int z = minCellZ; z <= maxCellZ; z++)
                    {
                        long cellKey = PackCell(x, y, z);
                        if (!_cells.TryGetValue(cellKey, out List<int> bucket))
                            continue;

                        int bucketCount = bucket.Count;
                        for (int i = 0; i < bucketCount; i++)
                        {
                            int handle = bucket[i];
                            if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                                continue;

                            SpatialTargetKind kind = entry.Kind;
                            if ((kind & kindMask) == 0)
                                continue;

                            Transform candidateTransform = entry.Transform;
                            if (candidateTransform == null)
                                continue;

                            Vector3 position = candidateTransform.position;
                            float distanceSqr = (position - origin).sqrMagnitude;
                            if (distanceSqr > radiusSqr)
                                continue;

                            results[count] = new SpatialQueryHit(
                                candidateTransform,
                                entry.Owner,
                                position,
                                distanceSqr,
                                kind,
                                entry.SignalRole,
                                entry.SpeciesId,
                                entry.Layer);
                            count++;

                            if (count >= results.Length)
                                return count;
                        }
                    }
                }
            }

            return count;
        }

        private static int Register(
            Component owner,
            Transform targetTransform,
            SpatialTargetKind kind,
            FieldTargetRole signalRole,
            int speciesId)
        {
            if (owner == null || targetTransform == null)
                return 0;

            int handle = _nextHandle++;
            long cellKey = GetCellKey(targetTransform.position);

            Entry entry = new Entry
            {
                Transform = targetTransform,
                Owner = owner,
                Kind = kind,
                SignalRole = signalRole,
                SpeciesId = speciesId,
                Layer = targetTransform.gameObject.layer,
                CellKey = cellKey
            };

            _entries.Add(handle, entry);
            AddToCell(handle, cellKey);
            return handle;
        }

        private static int FindHandle(Transform targetTransform)
        {
            if (targetTransform == null)
                return 0;

            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, Entry> pair = enumerator.Current;
                if (ReferenceEquals(pair.Value.Transform, targetTransform))
                    return pair.Key;
            }

            return 0;
        }

        private static void AddToCell(int handle, long cellKey)
        {
            if (!_cells.TryGetValue(cellKey, out List<int> bucket))
            {
                bucket = new List<int>(8); // COLD ALLOC: List<int>(8) — per-cell spatial bucket — owner: WorldSpatialHashGrid
                _cells.Add(cellKey, bucket);
            }

            bucket.Add(handle);
        }

        private static void RemoveFromCell(int handle, long cellKey)
        {
            if (!_cells.TryGetValue(cellKey, out List<int> bucket))
                return;

            int count = bucket.Count;
            for (int i = 0; i < count; i++)
            {
                if (bucket[i] != handle)
                    continue;

                int lastIndex = count - 1;
                bucket[i] = bucket[lastIndex];
                bucket.RemoveAt(lastIndex);
                break;
            }

            if (bucket.Count == 0)
                _cells.Remove(cellKey);
        }

        private static bool MatchesLayer(int layer, int layerMask)
        {
            return (layerMask & (1 << layer)) != 0;
        }

        private static bool IsSpectrumSignalRole(FieldTargetRole role)
        {
            switch (role)
            {
                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                case FieldTargetRole.HazardProbe:
                case FieldTargetRole.StructureRelay:
                case FieldTargetRole.ExpeditionCheckpoint:
                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                case FieldTargetRole.PowerGeneration:
                case FieldTargetRole.PowerRelay:
                case FieldTargetRole.PowerLoad:
                    return true;
                default:
                    return false;
            }
        }

        private static int ClampDistanceToHud(float distanceSqr)
        {
            int roundedDistance = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Sqrt(distanceSqr)),
                0,
                Hecton8.UI.HudNumericStringCache.MaxIntegerValue);
            return roundedDistance;
        }

        private static int ToCell(float value)
        {
            return Mathf.FloorToInt(value / CellSize);
        }

        private static long GetCellKey(Vector3 position)
        {
            return PackCell(ToCell(position.x), ToCell(position.y), ToCell(position.z));
        }

        private static long PackCell(int x, int y, int z)
        {
            long px = ((long)(x + CoordinateBias) & CoordinateMask) << (CoordinateBits * 2);
            long py = ((long)(y + CoordinateBias) & CoordinateMask) << CoordinateBits;
            long pz = (long)(z + CoordinateBias) & CoordinateMask;
            return px | py | pz;
        }
    }
}
