using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
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
    /// Compatibility facade over the native AUP-aware broadphase.
    /// Existing callers keep the old API while all candidate enumeration routes through HectonSpatialHash.
    /// </summary>
    internal static class WorldSpatialHashGrid
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct ValidateAupIntegrityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> AbsolutePositions;
            [ReadOnly] public NativeArray<float3> RuntimePositions;
            public float3 CurrentTotalOffset;
            [WriteOnly] public NativeArray<byte> InvalidMask;

            public void Execute(int index)
            {
                float3 reconstructedAbsolute = RuntimePositions[index] + CurrentTotalOffset;
                float3 delta = reconstructedAbsolute - AbsolutePositions[index];
                InvalidMask[index] = math.lengthsq(delta) <= 0.01f ? (byte)0 : (byte)1;
            }
        }

        private const double CellSizeMeters = 20d;
        private const int DefaultEntryCapacity = 256;
        private const int DefaultQueryCapacity = 256;
        private const int ValidationCadenceFrames = 300;

        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Query");
        private static readonly ProfilerMarker _maintenanceProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Maintenance");
        private static readonly ProfilerMarker _validationProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Validation");

        // COLD ALLOC: Dictionary<int,Entry>(256) — runtime metadata registry layered over the native AUP spatial hash — owner: WorldSpatialHashGrid
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(DefaultEntryCapacity);

        private static HectonSpatialHash _nativeHash;
        private static NativeList<int> _queryHandles;
        private static NativeArray<float3> _validationAbsolutePositions;
        private static NativeArray<float3> _validationRuntimePositions;
        private static NativeArray<byte> _validationInvalidMask;
        private static JobHandle _validationHandle;
        private static bool _validationScheduled;
        private static int _validationCount;
        private static int _lastValidationFrame = -ValidationCadenceFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            DisposeValidationBuffers();
            if (_queryHandles.IsCreated)
            {
                _queryHandles.Dispose();
                _queryHandles = default;
            }

            _nativeHash?.Dispose();
            _nativeHash = null;
            _validationHandle = default;
            _validationScheduled = false;
            _validationCount = 0;
            _lastValidationFrame = -ValidationCadenceFrames;
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

            UpdateNativeEntry(handle, entry);
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
            return TryGetNearestMatch(
                origin,
                radius,
                SpatialTargetKind.Bioform,
                layerMask,
                ignoreTransform,
                entry =>
                {
                    if (excludedSpeciesId >= 0 && entry.SpeciesId == excludedSpeciesId)
                        return false;

                    if (!requirePreyTag)
                        return true;

                    Transform candidateTransform = entry.Transform;
                    return candidateTransform != null && candidateTransform.CompareTag("Prey");
                },
                out hit);
        }

        public static bool TryGetNearestAggressiveBioform(
            Vector3 origin,
            float radius,
            int layerMask,
            Transform ignoreTransform,
            out SpatialQueryHit hit)
        {
            hit = default;
            return TryGetNearestMatch(
                origin,
                radius,
                SpatialTargetKind.Bioform,
                layerMask,
                ignoreTransform,
                entry =>
                {
                    if (!(entry.Owner is FaunaBrain brain))
                        return false;

                    return brain.isAggressive;
                },
                out hit);
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

            int handleCount = CollectCandidateHandles(origin, radius, SpatialTargetKind.Resource | SpatialTargetKind.Bioform | SpatialTargetKind.Signal | SpatialTargetKind.Module);
            for (int i = 0; i < handleCount; i++)
            {
                if (!_entries.TryGetValue(_queryHandles[i], out Entry entry) || entry == null)
                    continue;

                Transform candidateTransform = entry.Transform;
                if (candidateTransform == null)
                    continue;

                Vector3 position = candidateTransform.position;
                float distanceSqr = (position - origin).sqrMagnitude;
                if (distanceSqr > radiusSqr)
                    continue;

                SpatialTargetKind kind = entry.Kind;
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
            int handleCount = CollectCandidateHandles(origin, radius, kindMask);
            for (int i = 0; i < handleCount; i++)
            {
                if (!_entries.TryGetValue(_queryHandles[i], out Entry entry) || entry == null)
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
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer);
                count++;

                if (count >= results.Length)
                    break;
            }

            return count;
        }

        internal static void LateFrameMaintenance(int frameCount)
        {
            EnsureInitialized();
            using (_maintenanceProfilerMarker.Auto())
            {
                if (_validationScheduled && _validationHandle.IsCompleted)
                    ConsumeCompletedValidation();

                if (!_validationScheduled && frameCount - _lastValidationFrame >= ValidationCadenceFrames)
                    ScheduleValidation(frameCount);
            }
        }

        internal static void HandleOriginShift(in OriginShiftEventData shiftData)
        {
            EnsureInitialized();
            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, Entry> pair = enumerator.Current;
                Entry entry = pair.Value;
                if (entry == null || entry.Transform == null)
                    continue;

                UpdateNativeEntry(pair.Key, entry);
            }
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
            int handle = _nativeHash.Register(positionAup, float3.zero, (int)kind, 0);
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
            _nativeHash.UpdateEntry(handle, positionAup, float3.zero, (int)entry.Kind, 0);
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

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask)
        {
            EnsureInitialized();
            using (_queryProfilerMarker.Auto())
            {
                AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
                return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, _queryHandles);
            }
        }

        private static bool TryGetNearestMatch(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            int layerMask,
            Transform ignoreTransform,
            System.Predicate<Entry> predicate,
            out SpatialQueryHit hit)
        {
            hit = default;
            bool found = false;
            float bestDistanceSqr = radius * radius;
            int handleCount = CollectCandidateHandles(origin, radius, kindMask);
            for (int i = 0; i < handleCount; i++)
            {
                if (!_entries.TryGetValue(_queryHandles[i], out Entry entry) || entry == null)
                    continue;

                Transform candidateTransform = entry.Transform;
                if (candidateTransform == null || candidateTransform == ignoreTransform)
                    continue;

                if (!MatchesLayer(entry.Layer, layerMask))
                    continue;

                if (predicate != null && !predicate(entry))
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

        private static void ScheduleValidation(int frameCount)
        {
            EnsureInitialized();
            int count = _entries.Count;
            if (count <= 0)
            {
                _lastValidationFrame = frameCount;
                return;
            }

            EnsureValidationCapacity(count);
            int writeIndex = 0;
            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Entry entry = enumerator.Current.Value;
                if (entry == null || entry.Transform == null)
                    continue;

                Vector3 runtimePosition = entry.Transform.position;
                Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
                _validationRuntimePositions[writeIndex] = runtimePosition;
                _validationAbsolutePositions[writeIndex] = absolutePosition;
                writeIndex++;
            }

            if (writeIndex <= 0)
            {
                _lastValidationFrame = frameCount;
                return;
            }

            using (_validationProfilerMarker.Auto())
            {
                _validationCount = writeIndex;
                _validationHandle = new ValidateAupIntegrityJob
                {
                    AbsolutePositions = _validationAbsolutePositions,
                    RuntimePositions = _validationRuntimePositions,
                    CurrentTotalOffset = HectonFloatingOrigin.CurrentTotalOffset,
                    InvalidMask = _validationInvalidMask
                }.Schedule(writeIndex, 64);
                _validationScheduled = true;
                _lastValidationFrame = frameCount;
            }
        }

        private static void ConsumeCompletedValidation()
        {
            _validationHandle.Complete();
            _validationHandle = default;
            _validationScheduled = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < _validationCount; i++)
            {
                if (_validationInvalidMask[i] == 0)
                    continue;

                UnityEngine.Debug.LogError("[WorldSpatialHashGrid] AUP integrity validation failed. Runtime/AUP spatial coherence diverged.");
                break;
            }
#endif

            _validationCount = 0;
        }

        private static void EnsureValidationCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (_validationAbsolutePositions.IsCreated && _validationAbsolutePositions.Length >= safeCapacity)
                return;

            DisposeValidationBuffers();
            _validationAbsolutePositions = new NativeArray<float3>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _validationRuntimePositions = new NativeArray<float3>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _validationInvalidMask = new NativeArray<byte>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeValidationBuffers()
        {
            if (_validationScheduled)
            {
                _validationHandle.Complete();
                _validationScheduled = false;
            }

            if (_validationAbsolutePositions.IsCreated)
            {
                _validationAbsolutePositions.Dispose();
                _validationAbsolutePositions = default;
            }

            if (_validationRuntimePositions.IsCreated)
            {
                _validationRuntimePositions.Dispose();
                _validationRuntimePositions = default;
            }

            if (_validationInvalidMask.IsCreated)
            {
                _validationInvalidMask.Dispose();
                _validationInvalidMask = default;
            }

            _validationCount = 0;
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
    }
}
