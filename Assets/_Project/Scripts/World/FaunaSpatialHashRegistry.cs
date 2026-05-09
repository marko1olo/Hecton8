using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
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
        [StructLayout(LayoutKind.Sequential)]
        private struct Entry
        {
            public AbsoluteUniversePosition PositionAup;
            public Vector3 RuntimePosition;
            public Transform Transform;
            public Rigidbody Body;
            public Component Owner;
            public SpatialTargetKind Kind;
            public FieldTargetRole SignalRole;
            public int SpeciesId;
            public int Layer;
            public byte IsPreyTag;
        }

        private const double CellSizeMeters = 50d;
        private const float DensityCapCellSizeMeters = 2f;
        private const float DensityCapCellRadiusSqr = DensityCapCellSizeMeters * DensityCapCellSizeMeters;
        private const float DensityPenaltyMinimumDistanceSqr = 0.04f;
        private const int DensityCapMaxBoidsPerCell = 8;
        private const int MaxEntryCapacity = 1024;
        private const int DefaultQueryCapacity = 128;
        private const float AdjacentQueryCompleteRadiusMeters = 50f;
        private const float AdjacentQueryCompleteRadiusSqr = AdjacentQueryCompleteRadiusMeters * AdjacentQueryCompleteRadiusMeters;
        private const string NativeMemoryOwner = nameof(FaunaSpatialHashRegistry);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        // COLD ALLOC: Dictionary<int,Entry>[1024] — fauna-only AUP spatial metadata registry layered over HectonSpatialHash — owner: FaunaSpatialHashRegistry
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(MaxEntryCapacity);

        private static HectonSpatialHash _nativeHash;
        private static NativeList<int> _queryHandles;
        private static bool _lastResultBufferSaturated;
        private static bool _lastAdjacentRadiusClamped;

        public static HectonSpatialHash.QueryStats LastNativeQueryStats => _nativeHash != null ? _nativeHash.LastQueryStats : default;
        public static bool LastNativeQuerySaturated => _nativeHash != null && _nativeHash.LastQueryStats.IsSaturated;
        public static bool LastResultBufferSaturated => _lastResultBufferSaturated;
        public static bool LastAdjacentRadiusClamped => _lastAdjacentRadiusClamped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            _lastResultBufferSaturated = false;
            _lastAdjacentRadiusClamped = false;

            if (_queryHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_queryHandles));
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
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry))
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

            if (!_entries.Remove(handle))
                return;

            if (_nativeHash != null)
                _nativeHash.Unregister(handle);
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
            ResetQueryTelemetry();
            if (!IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
                return false;

            int handleCount = CollectCandidateHandles(in originAup, radius, SpatialTargetKind.Bioform);
            bool found = false;
            float bestDistanceSqr = radius * radius;

            for (int i = 0; i < handleCount; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

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

                if (requirePreyTag && entry.IsPreyTag == 0)
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
                    entry.PositionAup,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
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
            ResetQueryTelemetry();
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f)
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            int handleCount = CollectCandidateHandles(in originAup, radius, SpatialTargetKind.Bioform);
            bool found = false;
            double bestDistanceSqr = (double)radius * radius;

            for (int i = 0; i < handleCount; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

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

                if (requirePreyTag && entry.IsPreyTag == 0)
                    continue;

                Vector3 position = entry.RuntimePosition;
                AbsoluteUniversePosition candidateAup = entry.PositionAup;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                hit = new SpatialQueryHit(
                    candidateTransform,
                    entry.Owner,
                    position,
                    candidateAup,
                    (float)math.min(distanceSqr, float.MaxValue),
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
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
            ResetQueryTelemetry();
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None || !IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
                return 0;

            int handleCount = CollectCandidateHandles(in originAup, radius, kindMask);
            int count = 0;
            float maxDistanceSqr = radius * radius;

            for (int i = 0; i < handleCount && count < results.Length; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

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
                    entry.PositionAup,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
                count++;
                if (count >= results.Length && i + 1 < handleCount)
                    _lastResultBufferSaturated = true;
            }

            return count;
        }

        public static int CollectAdjacentContactsNonAlloc(
            in AbsoluteUniversePosition originAup,
            float radius,
            SpatialTargetKind kindMask,
            SpatialQueryHit[] results)
        {
            ResetQueryTelemetry();
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None || !IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
                return 0;

            int handleCount = CollectAdjacentCandidateHandles(in originAup, kindMask);
            int count = 0;
            float maxDistanceSqr = ResolveAdjacentQueryDistanceSqr(radius);

            for (int i = 0; i < handleCount && count < results.Length; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

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
                    entry.PositionAup,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
                count++;
                if (count >= results.Length && i + 1 < handleCount)
                    _lastResultBufferSaturated = true;
            }

            return count;
        }

        public static bool TryGetNearestAdjacentBioform(
            in AbsoluteUniversePosition originAup,
            float radius,
            int layerMask,
            Component ignoreOwner,
            int excludedSpeciesId,
            bool requirePreyTag,
            out SpatialQueryHit hit)
        {
            hit = default;
            ResetQueryTelemetry();
            if (!IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
                return false;

            int handleCount = CollectAdjacentCandidateHandles(in originAup, SpatialTargetKind.Bioform);
            bool found = false;
            float bestDistanceSqr = ResolveAdjacentQueryDistanceSqr(radius);

            for (int i = 0; i < handleCount; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

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

                if (requirePreyTag && entry.IsPreyTag == 0)
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
                    entry.PositionAup,
                    distanceSqr,
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
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
            ResetQueryTelemetry();
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None || !IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f)
                return 0;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            int handleCount = CollectCandidateHandles(in originAup, radius, kindMask);
            int count = 0;
            double maxDistanceSqr = (double)radius * radius;

            for (int i = 0; i < handleCount && count < results.Length; i++)
            {
                int handle = _queryHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                {
                    DropNativeOnlyHandle(handle);
                    continue;
                }

                if (!IsEntryQueryEligible(entry))
                {
                    Unregister(handle);
                    continue;
                }

                Transform targetTransform = entry.Transform;
                Vector3 position = entry.RuntimePosition;
                AbsoluteUniversePosition candidateAup = entry.PositionAup;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > maxDistanceSqr)
                    continue;

                results[count] = new SpatialQueryHit(
                    targetTransform,
                    entry.Owner,
                    position,
                    candidateAup,
                    (float)math.min(distanceSqr, float.MaxValue),
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0);
                count++;
                if (count >= results.Length && i + 1 < handleCount)
                    _lastResultBufferSaturated = true;
            }

            return count;
        }

        public static bool TryResolveDensityPenalty(int handle, out Vector3 penaltyDirection, out int densityCount)
        {
            penaltyDirection = default;
            densityCount = 0;
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry sourceEntry))
                return false;

            if (!IsEntryQueryEligible(sourceEntry))
                return false;

            int handleCount = CollectCandidateHandles(in sourceEntry.PositionAup, DensityCapCellSizeMeters, SpatialTargetKind.Bioform);
            float3 penalty = float3.zero;
            for (int i = 0; i < handleCount; i++)
            {
                int candidateHandle = _queryHandles[i];
                if (!_entries.TryGetValue(candidateHandle, out Entry candidateEntry))
                {
                    DropNativeOnlyHandle(candidateHandle);
                    continue;
                }

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

            float penaltyLengthSq = math.lengthsq(penalty);
            if (densityCount <= DensityCapMaxBoidsPerCell || penaltyLengthSq <= 0.0001f)
                return false;

            float overflow01 = math.saturate((densityCount - DensityCapMaxBoidsPerCell) / (float)DensityCapMaxBoidsPerCell);
            float3 resolvedPenalty = penalty * math.rsqrt(math.max(penaltyLengthSq, 0.0001f)) * (1f + overflow01 * 2.5f);
            penaltyDirection = new Vector3(resolvedPenalty.x, resolvedPenalty.y, resolvedPenalty.z);
            return penaltyDirection.sqrMagnitude > 0.0001f;
        }

        private static void EnsureInitialized()
        {
            if (_nativeHash == null)
                _nativeHash = new HectonSpatialHash(MaxEntryCapacity, MaxEntryCapacity * 4, CellSizeMeters);

            if (!_queryHandles.IsCreated)
            {
                // COLD ALLOC: NativeList<int>[DefaultQueryCapacity] - fauna AUP query handle scratch buffer - owner: FaunaSpatialHashRegistry
                _queryHandles = new NativeList<int>(DefaultQueryCapacity, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeList(_queryHandles, NativeMemoryOwner, nameof(_queryHandles), NativeMemoryLifetime);
            }
        }

        private static void DropNativeOnlyHandle(int handle)
        {
            if (handle <= 0 || _nativeHash == null)
                return;

            _nativeHash.Unregister(handle);
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
            if (_entries.Count >= MaxEntryCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[FaunaSpatialHashRegistry] Entry capacity exceeded. Runtime registry growth is forbidden.");
#endif
                return 0;
            }

            targetTransform.TryGetComponent(out Rigidbody body);
            if (!TryResolveEntryLogicPose(owner, targetTransform, body, out AbsoluteUniversePosition positionAup, out Vector3 runtimePosition))
                return 0;

            int handle = _nativeHash.Register(positionAup, float3.zero, (int)kind, ResolveEntityFlags(kind), 0);
            if (handle <= 0)
                return 0;

            _entries[handle] = new Entry
            {
                Transform = targetTransform,
                Body = body,
                Owner = owner,
                Kind = kind,
                SignalRole = signalRole,
                SpeciesId = speciesId,
                Layer = targetTransform.gameObject.layer,
                PositionAup = positionAup,
                RuntimePosition = runtimePosition,
                IsPreyTag = kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey") ? (byte)1 : (byte)0
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

            if (!TryResolveEntryLogicPose(entry.Owner, targetTransform, entry.Body, out AbsoluteUniversePosition positionAup, out Vector3 runtimePosition))
            {
                Unregister(handle);
                return;
            }

            entry.Layer = targetTransform.gameObject.layer;
            entry.PositionAup = positionAup;
            entry.RuntimePosition = runtimePosition;
            entry.IsPreyTag = entry.Kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey") ? (byte)1 : (byte)0;
            if (!_nativeHash.TryUpdateEntry(handle, positionAup, float3.zero, (int)entry.Kind, ResolveEntityFlags(entry.Kind), 0))
            {
                Unregister(handle);
                return;
            }

            _entries[handle] = entry;
        }

        private static bool TryResolveEntryLogicPose(
            Component owner,
            Transform targetTransform,
            Rigidbody body,
            out AbsoluteUniversePosition positionAup,
            out Vector3 runtimePosition)
        {
            if (owner is FaunaBrain brain && brain.TryResolveLogicAup(out positionAup))
            {
                if (!IsFiniteAup(in positionAup))
                {
                    runtimePosition = default;
                    return false;
                }

                float3 runtime = positionAup.ToRuntimeFloat3();
                runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                return IsFiniteRuntimePosition(runtimePosition);
            }

            if (targetTransform == null)
            {
                positionAup = default;
                runtimePosition = default;
                return false;
            }

            runtimePosition = body != null ? body.position : targetTransform.position;
            if (!IsFiniteRuntimePosition(runtimePosition))
            {
                positionAup = default;
                return false;
            }

            positionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return true;
        }

        private static int CollectCandidateHandles(in AbsoluteUniversePosition originAup, float radius, SpatialTargetKind kindMask)
        {
            if (!IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f || kindMask == SpatialTargetKind.None)
                return 0;

            if (_nativeHash == null || _entries.Count == 0)
                return 0;

            EnsureInitialized();
            return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, _queryHandles);
        }

        private static int CollectAdjacentCandidateHandles(in AbsoluteUniversePosition originAup, SpatialTargetKind kindMask)
        {
            if (!IsFiniteAup(in originAup) || kindMask == SpatialTargetKind.None)
                return 0;

            if (_nativeHash == null || _entries.Count == 0)
                return 0;

            EnsureInitialized();
            return _nativeHash.CollectAdjacentCells(originAup, (int)kindMask, _queryHandles);
        }

        private static void ResetQueryTelemetry()
        {
            _lastResultBufferSaturated = false;
            _lastAdjacentRadiusClamped = false;
            if (_nativeHash != null)
                _nativeHash.ClearLastQueryStats();
        }

        private static float ResolveAdjacentQueryDistanceSqr(float radius)
        {
            float safeRadius = math.isfinite(radius) ? math.max(0f, radius) : 0f;
            if (safeRadius > AdjacentQueryCompleteRadiusMeters)
            {
                _lastAdjacentRadiusClamped = true;
                return AdjacentQueryCompleteRadiusSqr;
            }

            return safeRadius * safeRadius;
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            float3 value = position;
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.all(math.isfinite(new float3(position.LocalX, position.LocalY, position.LocalZ)));
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
            if (entry.Transform == null || entry.Owner == null)
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
