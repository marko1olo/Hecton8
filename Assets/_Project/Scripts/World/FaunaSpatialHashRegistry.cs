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
            public AbsoluteUniversePosition PositionAup;
            public Vector3 RuntimePosition;
            public bool IsPreyTag;
        }

        private const double CellSizeMeters = 20d;
        private const float DensityCapCellSizeMeters = 2f;
        private const float DensityCapCellRadiusSqr = DensityCapCellSizeMeters * DensityCapCellSizeMeters;
        private const float DensityPenaltyMinimumDistanceSqr = 0.04f;
        private const int DensityCapMaxBoidsPerCell = 8;
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
            in AbsoluteUniversePosition originAup,
            float radius,
            int layerMask,
            Component ignoreOwner,
            int excludedSpeciesId,
            bool requirePreyTag,
            out SpatialQueryHit hit)
        {
            hit = default;
            int handleCount = CollectCandidateHandles(in originAup, radius, SpatialTargetKind.Bioform);
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

                if (ignoreOwner != null && entry.Owner == ignoreOwner)
                    continue;

                if (!MatchesLayer(entry.Layer, layerMask))
                    continue;

                if (excludedSpeciesId >= 0 && entry.SpeciesId == excludedSpeciesId)
                    continue;

                if (requirePreyTag && !entry.IsPreyTag)
                    continue;

                float distanceSqr = (float)math.min(
                    AbsoluteUniversePosition.DistanceSq(in entry.PositionAup, in originAup),
                    float.MaxValue);
                if (distanceSqr > bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                hit = new SpatialQueryHit(
                    entry.Transform,
                    entry.Owner,
                    entry.RuntimePosition,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer);
                found = true;
            }

            return found;
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
            in AbsoluteUniversePosition originAup,
            float radius,
            SpatialTargetKind kindMask,
            SpatialQueryHit[] results)
        {
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None)
                return 0;

            int handleCount = CollectCandidateHandles(in originAup, radius, kindMask);
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

                float distanceSqr = (float)math.min(
                    AbsoluteUniversePosition.DistanceSq(in entry.PositionAup, in originAup),
                    float.MaxValue);
                if (distanceSqr > maxDistanceSqr)
                    continue;

                results[count] = new SpatialQueryHit(
                    entry.Transform,
                    entry.Owner,
                    entry.RuntimePosition,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer);
                count++;
            }

            return count;
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

        public static bool TryResolveDensityPenalty(int handle, out Vector3 penaltyDirection, out int densityCount)
        {
            penaltyDirection = default;
            densityCount = 0;
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry sourceEntry) || sourceEntry == null)
                return false;

            if (!IsEntryQueryEligible(sourceEntry))
                return false;

            int handleCount = CollectCandidateHandles(in sourceEntry.PositionAup, DensityCapCellSizeMeters, SpatialTargetKind.Bioform);
            float3 penalty = float3.zero;
            for (int i = 0; i < handleCount; i++)
            {
                int candidateHandle = _queryHandles[i];
                if (!_entries.TryGetValue(candidateHandle, out Entry candidateEntry) || candidateEntry == null)
                    continue;

                if (!IsEntryQueryEligible(candidateEntry))
                {
                    Unregister(candidateHandle);
                    continue;
                }

                FaunaBrain candidateBrain = candidateEntry.Owner as FaunaBrain;
                if (candidateBrain == null || !candidateBrain.IsFlockingRuntime)
                    continue;

                double aupDistanceSq = AUPMath.AUPDistanceSq(in candidateEntry.PositionAup, in sourceEntry.PositionAup);
                if (aupDistanceSq > DensityCapCellRadiusSqr)
                    continue;

                densityCount++;
                if (candidateHandle == handle)
                    continue;

                float3 awayFromNeighbor = AUPMath.AUPDirection(in candidateEntry.PositionAup, in sourceEntry.PositionAup);
                float safeDistanceSqr = math.max((float)math.min(aupDistanceSq, float.MaxValue), DensityPenaltyMinimumDistanceSqr);
                penalty += awayFromNeighbor * math.rcp(safeDistanceSqr);
            }

            if (densityCount <= DensityCapMaxBoidsPerCell || math.lengthsq(penalty) <= 0.0001f)
                return false;

            float overflow01 = math.saturate((densityCount - DensityCapMaxBoidsPerCell) / (float)DensityCapMaxBoidsPerCell);
            float3 resolvedPenalty = math.normalizesafe(penalty, float3.zero) * (1f + overflow01 * 2.5f);
            penaltyDirection = new Vector3(resolvedPenalty.x, resolvedPenalty.y, resolvedPenalty.z);
            return penaltyDirection.sqrMagnitude > 0.0001f;
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
                Layer = targetTransform.gameObject.layer,
                PositionAup = positionAup,
                RuntimePosition = targetTransform.position,
                IsPreyTag = kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey")
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
            entry.PositionAup = positionAup;
            entry.RuntimePosition = targetTransform.position;
            entry.IsPreyTag = entry.Kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey");
            _nativeHash.UpdateEntry(handle, positionAup, float3.zero, (int)entry.Kind, ResolveEntityFlags(entry.Kind), 0);
        }

        private static int CollectCandidateHandles(in AbsoluteUniversePosition originAup, float radius, SpatialTargetKind kindMask)
        {
            EnsureInitialized();
            return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, _queryHandles);
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask)
        {
            EnsureInitialized();
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            return CollectCandidateHandles(in originAup, radius, kindMask);
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
