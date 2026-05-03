using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// AUP-native fauna sensing registry layered directly over <see cref="HectonSpatialHash"/>.
    /// Scope is intentionally limited to the fauna AI stack so legacy scanner/gameplay callers can be migrated independently.
    /// </summary>
    internal static class FaunaSpatialHashRegistry
    {
        private sealed class Entry
        {
            public Transform Transform;
            public Component Owner;
            public SpatialTargetKind Kind;
            public FieldTargetRole SignalRole;
            public int SpeciesId;
            public int Layer;
        }

        private const double CellSizeMeters = 20d;
        private const int DefaultEntryCapacity = 128;
        private const int DefaultQueryCapacity = 128;

        // COLD ALLOC: Dictionary<int,Entry>[128] — fauna-only AUP spatial metadata registry layered over HectonSpatialHash — owner: FaunaSpatialHashRegistry
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(DefaultEntryCapacity);

        private static HectonSpatialHash _nativeHash;
        private static NativeList<int> _queryHandles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();

            if (_queryHandles.IsCreated)
            {
                _queryHandles.Dispose();
                _queryHandles = default;
            }

            _nativeHash?.Dispose();
            _nativeHash = null;
        }

        public static int RegisterBioform(FaunaBrain brain)
        {
            return Register(brain, brain != null ? brain.transform : null, SpatialTargetKind.Bioform, FieldTargetRole.Generic, brain != null ? brain.SpeciesId : 0);
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

        public static void Refresh(int handle)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            if (!IsEntryQueryEligible(entry))
            {
                Unregister(handle);
                return;
            }

            UpdateNativeEntry(handle, entry);
        }

        public static void Unregister(int handle)
        {
            if (handle <= 0)
                return;

            EnsureInitialized();
            _nativeHash.Unregister(handle);
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
            hit = default;
            int handleCount = CollectCandidateHandles(origin, radius, SpatialTargetKind.Bioform);
            bool found = false;
            float bestDistanceSqr = radius * radius;

            for (int i = 0; i < handleCount; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                    continue;

                if (!IsEntryQueryEligible(entry))
                {
                    Unregister(handle);
                    continue;
                }

                Transform candidateTransform = entry.Transform;
                if (candidateTransform == ignoreTransform)
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
                hit = new SpatialQueryHit(
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

            return found;
        }

        public static int CollectContactsNonAlloc(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            SpatialQueryHit[] results)
        {
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None)
                return 0;

            int handleCount = CollectCandidateHandles(origin, radius, kindMask);
            int count = 0;
            float maxDistanceSqr = radius * radius;

            for (int i = 0; i < handleCount && count < results.Length; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                    continue;

                if (!IsEntryQueryEligible(entry))
                {
                    Unregister(handle);
                    continue;
                }

                Transform targetTransform = entry.Transform;
                Vector3 position = targetTransform.position;
                float distanceSqr = (position - origin).sqrMagnitude;
                if (distanceSqr > maxDistanceSqr)
                    continue;

                results[count] = new SpatialQueryHit(
                    targetTransform,
                    entry.Owner,
                    position,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer);
                count++;
            }

            return count;
        }

        private static void EnsureInitialized()
        {
            if (_nativeHash == null)
                _nativeHash = new HectonSpatialHash(DefaultEntryCapacity, DefaultEntryCapacity * 4, CellSizeMeters);

            if (!_queryHandles.IsCreated)
                _queryHandles = new NativeList<int>(DefaultQueryCapacity, Allocator.Persistent);
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

            EnsureInitialized();
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(targetTransform.position);
            int handle = _nativeHash.Register(positionAup, float3.zero, (int)kind, ResolveEntityFlags(kind), 0);
            if (handle <= 0)
                return 0;

            _entries[handle] = new Entry
            {
                Transform = targetTransform,
                Owner = owner,
                Kind = kind,
                SignalRole = signalRole,
                SpeciesId = speciesId,
                Layer = targetTransform.gameObject.layer
            };
            return handle;
        }

        private static void UpdateNativeEntry(int handle, Entry entry)
        {
            Transform targetTransform = entry.Transform;
            if (targetTransform == null)
            {
                Unregister(handle);
                return;
            }

            entry.Layer = targetTransform.gameObject.layer;
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(targetTransform.position);
            _nativeHash.UpdateEntry(handle, positionAup, float3.zero, (int)entry.Kind, ResolveEntityFlags(entry.Kind), 0);
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask)
        {
            EnsureInitialized();
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, _queryHandles);
        }

        private static ulong ResolveEntityFlags(SpatialTargetKind kind)
        {
            ulong flags = 0UL;
            if ((kind & SpatialTargetKind.Bioform) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Bioform | SpatialInteractionFlags.AcousticReceiver | SpatialInteractionFlags.ChemicalReceiver | SpatialInteractionFlags.ThermalReceiver);
            if ((kind & SpatialTargetKind.Signal) != 0)
                flags |= (ulong)SpatialInteractionFlags.Signal;
            if ((kind & SpatialTargetKind.Pickup) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Pickup | SpatialInteractionFlags.Interactable);
            return flags;
        }

        private static bool MatchesLayer(int layer, int layerMask)
        {
            return layerMask == 0 || (layerMask & (1 << layer)) != 0;
        }

        private static bool IsEntryQueryEligible(Entry entry)
        {
            if (entry == null || entry.Transform == null || entry.Owner == null)
                return false;

            if (!entry.Transform.gameObject.activeInHierarchy)
                return false;

            if (entry.Owner is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;

            if (entry.Owner is FaunaBrain faunaBrain && faunaBrain.IsDead)
                return false;

            return true;
        }
    }
}
