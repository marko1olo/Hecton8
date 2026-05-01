using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Modding;
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

    [System.Flags]
    internal enum SpatialTransientEventType : uint
    {
        None = 0u,
        AcousticImpulse = 1u << 0,
        ChemicalCloud = 1u << 1,
        ChemicalScent = ChemicalCloud,
        ThermalGradient = 1u << 2,
        DisturbanceEvent = 1u << 3
    }

    [System.Flags]
    internal enum SpatialInteractionFlags : ulong
    {
        None = 0UL,
        Resource = 1UL << 0,
        Bioform = 1UL << 1,
        Signal = 1UL << 2,
        Pickup = 1UL << 3,
        Scannable = 1UL << 4,
        Module = 1UL << 5,
        AcousticReceiver = 1UL << 6,
        ChemicalReceiver = 1UL << 7,
        ThermalReceiver = 1UL << 8,
        Interactable = 1UL << 9
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

    internal sealed class SpatialHashEntryUnloadedEvent : HectonEvent
    {
        public SpatialHashEntryUnloadedEvent(
            int handle,
            SpatialTargetKind kind,
            Component owner,
            Vector3 runtimePosition,
            double3 absolutePosition,
            int layer)
        {
            Handle = handle;
            Kind = kind;
            Owner = owner;
            RuntimePosition = runtimePosition;
            AbsolutePosition = absolutePosition;
            Layer = layer;
        }

        public int Handle { get; }
        public SpatialTargetKind Kind { get; }
        public Component Owner { get; }
        public Vector3 RuntimePosition { get; }
        public double3 AbsolutePosition { get; }
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
            public Vector3 RuntimePosition;
            public SpatialTargetKind Kind;
            public FieldTargetRole SignalRole;
            public int SpeciesId;
            public int Layer;
            public float3 HalfExtents;
            public int PayloadId;
            public ulong EntityFlags;
            public bool IsResidentInNativeHash;
        }

        private struct TransientSignalEntry
        {
            public Vector3 RuntimePosition;
            public double ExpireTimestamp;
            public FieldTargetRole SignalRole;
            public int SourceSpeciesId;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RebuildAbsolutePositionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> RuntimePositions;
            public float3 CurrentTotalOffset;
            [WriteOnly] public NativeArray<float3> AbsolutePositions;

            public void Execute(int index)
            {
                AbsolutePositions[index] = RuntimePositions[index] + CurrentTotalOffset;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct FarUnloadCandidatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<double3> AbsolutePositions;
            [ReadOnly] public NativeArray<byte> EligibilityMask;
            public double3 PlayerAbsolutePosition;
            public double MaxDistanceSq;
            [WriteOnly] public NativeArray<byte> UnloadMask;

            public void Execute(int index)
            {
                if (EligibilityMask[index] == 0)
                {
                    UnloadMask[index] = 0;
                    return;
                }

                double3 delta = AbsolutePositions[index] - PlayerAbsolutePosition;
                UnloadMask[index] = math.lengthsq(delta) > MaxDistanceSq ? (byte)1 : (byte)0;
            }
        }

        private const double CellSizeMeters = 20d;
        private const int DefaultEntryCapacity = 256;
        private const int DefaultQueryCapacity = 256;
        private const int ValidationCadenceFrames = 300;
        private const float FarUnloadPlayerTravelThresholdMeters = 2000f;
        private const double FarUnloadPlayerTravelThresholdSq = FarUnloadPlayerTravelThresholdMeters * FarUnloadPlayerTravelThresholdMeters;
        private const float FarUnloadDistanceMeters = 2500f;
        private const double FarUnloadDistanceSq = FarUnloadDistanceMeters * FarUnloadDistanceMeters;
        private const int AcousticDensityMapAxis = 8;
        private const int AcousticDensityMapCellCount = AcousticDensityMapAxis * AcousticDensityMapAxis * AcousticDensityMapAxis;
        private const int AcousticDensityMapCadenceFrames = 10;
        private const float AcousticDensityMapRadiusMeters = 160f;
        private const float AcousticTransientDecayScale = 0.85f;
        private const float AcousticTransientMinimumIntensity = 0.01f;
        private const int SpatialHashCompactionCapacityThreshold = 50000;
        private const int SpatialHashCompactionTargetFloor = DefaultEntryCapacity * 4;
        private const int MaxTransientSignalCount = 16;

        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Query");
        private static readonly ProfilerMarker _maintenanceProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Maintenance");
        private static readonly ProfilerMarker _validationProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Validation");
        private static readonly ProfilerMarker _farUnloadProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.FarUnload");
        private static readonly ProfilerMarker _acousticDensityProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.AcousticDensity");

        // COLD ALLOC: Dictionary<int,Entry>(256) — runtime metadata registry layered over the native AUP spatial hash — owner: WorldSpatialHashGrid
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(DefaultEntryCapacity);
        // COLD ALLOC: List<int>[128] â€” deferred far-unload handle scratch for dynamic native-hash eviction â€” owner: WorldSpatialHashGrid
        private static readonly List<int> _farUnloadHandleScratch = new List<int>(128);

        private static readonly TransientSignalEntry[] _transientSignals = new TransientSignalEntry[MaxTransientSignalCount]; // COLD ALLOC: TransientSignalEntry[16] - transient PDA sonar signal ring - owner: WorldSpatialHashGrid

        private static HectonSpatialHash _nativeHash;
        private static NativeList<int> _queryHandles;
        private static NativeArray<float3> _validationAbsolutePositions;
        private static NativeArray<float3> _validationRuntimePositions;
        private static NativeArray<byte> _validationInvalidMask;
        private static JobHandle _validationHandle;
        private static bool _validationScheduled;
        private static int _validationCount;
        private static NativeArray<int> _originShiftHandles;
        private static NativeArray<float3> _originShiftRuntimePositions;
        private static NativeArray<float3> _originShiftAbsolutePositions;
        private static JobHandle _originShiftRefreshHandle;
        private static bool _originShiftRefreshScheduled;
        private static int _originShiftRefreshCount;
        private static NativeArray<int> _farUnloadHandles;
        private static NativeArray<double3> _farUnloadAbsolutePositions;
        private static NativeArray<byte> _farUnloadEligibilityMask;
        private static NativeArray<byte> _farUnloadResultMask;
        private static JobHandle _farUnloadHandle;
        private static bool _farUnloadScheduled;
        private static int _farUnloadCount;
        private static NativeArray<float> _acousticDensityMap;
        private static int _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
        private static int _transientSignalWriteIndex;
        private static AbsoluteUniversePosition _lastFarUnloadPlayerAup;
        private static bool _hasLastFarUnloadPlayerAup;
        private static int _lastValidationFrame = -ValidationCadenceFrames;

        internal static int ActiveEntityCount => _nativeHash != null ? _nativeHash.EntryCount : _entries.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            DisposeValidationBuffers();
            DisposeOriginShiftBuffers();
            DisposeFarUnloadBuffers();
            DisposeAcousticDensityMap();
            _farUnloadHandleScratch.Clear();
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
            _farUnloadHandle = default;
            _farUnloadScheduled = false;
            _farUnloadCount = 0;
            _hasLastFarUnloadPlayerAup = false;
            _lastValidationFrame = -ValidationCadenceFrames;
            _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
            _transientSignalWriteIndex = 0;
            for (int i = 0; i < _transientSignals.Length; i++)
                _transientSignals[i] = default;
        }

        public static int RegisterResource(ResourceNode node)
        {
            return RegisterResource(node, node != null ? (float3)node.SpatialHalfExtents : float3.zero);
        }

        public static int RegisterResource(ResourceNode node, Vector3 halfExtents)
        {
            return RegisterResource(node, (float3)halfExtents);
        }

        public static int RegisterResource(ResourceNode node, float3 halfExtents)
        {
            return Register(
                node,
                node != null ? node.transform : null,
                SpatialTargetKind.Resource,
                FieldTargetRole.ResourceNodeActive,
                0,
                halfExtents);
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

        public static void SetResourceHalfExtents(int handle, Vector3 halfExtents)
        {
            SetResourceHalfExtents(handle, (float3)halfExtents);
        }

        public static void SetResourceHalfExtents(int handle, float3 halfExtents)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            entry.HalfExtents = math.max(halfExtents, 0f);
            _entries[handle] = entry;
            UpdateNativeEntry(handle, entry);
        }

        public static void Unregister(int handle)
        {
            if (handle <= 0)
                return;

            if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                return;

            EnsureInitialized();
            if (entry.IsResidentInNativeHash)
                _nativeHash.Unregister(handle);
            else
                _nativeHash.ReleaseHandle(handle);

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

            double currentTimestamp = Time.unscaledTimeAsDouble;
            for (int i = 0; i < _transientSignals.Length; i++)
            {
                TransientSignalEntry signalEntry = _transientSignals[i];
                if (signalEntry.ExpireTimestamp <= currentTimestamp)
                    continue;

                float distanceSqr = (signalEntry.RuntimePosition - origin).sqrMagnitude;
                if (distanceSqr > radiusSqr)
                    continue;

                signalCount++;
                if (distanceSqr < nearestSignalDistanceSqr)
                {
                    nearestSignalDistanceSqr = distanceSqr;
                    nearestSignalRole = signalEntry.SignalRole;
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
            return CollectContactsNonAlloc(origin, radius, kindMask, SpatialInteractionFlags.None, results);
        }

        public static int CollectContactsNonAlloc(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            SpatialInteractionFlags interactionFilter,
            SpatialQueryHit[] results)
        {
            return CollectContactsNonAlloc(origin, radius, kindMask, (ulong)interactionFilter, results);
        }

        public static int CollectContactsNonAlloc(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            uint interactionFilter,
            SpatialQueryHit[] results)
        {
            return CollectContactsNonAlloc(origin, radius, kindMask, (ulong)interactionFilter, results);
        }

        private static int CollectContactsNonAlloc(
            Vector3 origin,
            float radius,
            SpatialTargetKind kindMask,
            ulong interactionFilter,
            SpatialQueryHit[] results)
        {
            if (results == null || results.Length == 0 || kindMask == SpatialTargetKind.None)
                return 0;

            int count = 0;
            float radiusSqr = radius * radius;
            int handleCount = CollectCandidateHandles(origin, radius, kindMask, interactionFilter);
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

        public static void RegisterTransientEvent(
            Vector3 worldPosition,
            float radiusMeters,
            float intensity,
            float lifetimeSeconds,
            SpatialTransientEventType eventType,
            SpatialInteractionFlags eventFlags = SpatialInteractionFlags.None,
            FieldTargetRole signalRole = FieldTargetRole.Generic,
            int sourceSpeciesId = 0,
            float temperature = 0f)
        {
            if (radiusMeters <= 0f || intensity <= 0f || lifetimeSeconds <= 0f || eventType == SpatialTransientEventType.None)
                return;

            EnsureInitialized();
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            double currentTimestamp = Time.unscaledTimeAsDouble;
            double expirationTimestamp = currentTimestamp + lifetimeSeconds;
            uint sourceKey = ComposeTransientSignalSourceKey(signalRole, sourceSpeciesId);
            _nativeHash.RegisterTransientEvent(
                in positionAup,
                radiusMeters,
                math.saturate(intensity),
                expirationTimestamp,
                (uint)eventType,
                (ulong)eventFlags,
                currentTimestamp,
                sourceKey,
                temperature);

            if (sourceKey != 0u)
                TrackTransientSignal(worldPosition, expirationTimestamp, signalRole, sourceSpeciesId);
        }

        /// <summary>
        /// Clears one transient signal source immediately, used by mimic fauna once the false beacon has served its ambush role.
        /// </summary>
        public static void ClearTransientSignal(FieldTargetRole signalRole, int sourceSpeciesId)
        {
            uint sourceKey = ComposeTransientSignalSourceKey(signalRole, sourceSpeciesId);
            if (sourceKey != 0u)
            {
                EnsureInitialized();
                _nativeHash.ClearTransientEvents((uint)SpatialTransientEventType.AcousticImpulse, sourceKey, Time.unscaledTimeAsDouble);
                _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
            }

            double currentTimestamp = Time.unscaledTimeAsDouble;
            for (int i = 0; i < _transientSignals.Length; i++)
            {
                TransientSignalEntry entry = _transientSignals[i];
                if (entry.ExpireTimestamp <= currentTimestamp)
                    continue;

                if (entry.SignalRole == signalRole && entry.SourceSpeciesId == sourceSpeciesId)
                    _transientSignals[i] = default;
            }
        }

        public static bool TryGetAcousticDensityMap(
            out NativeArray<float> densityMap,
            out Vector3Int dimensions)
        {
            EnsureAcousticDensityMap();
            densityMap = _acousticDensityMap;
            dimensions = new Vector3Int(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis);
            return _acousticDensityMap.IsCreated;
        }

        public static bool IsHandleCurrent(int handle)
        {
            EnsureInitialized();
            return _nativeHash.IsCurrentHandle(handle);
        }

        public static bool QueryTemperatureGradient(
            Vector3 origin,
            float radiusMeters,
            out float temperatureDeltaCelsius,
            out Vector3 gradient)
        {
            EnsureInitialized();
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            bool hasGradient = _nativeHash.QueryTemperatureGradient(
                in originAup,
                radiusMeters,
                Time.unscaledTimeAsDouble,
                out temperatureDeltaCelsius,
                out double3 gradientAup);
            gradient = new Vector3((float)gradientAup.x, (float)gradientAup.y, (float)gradientAup.z);
            return hasGradient;
        }

        internal static void SlowTickMaintenance(float deltaTime)
        {
            EnsureInitialized();
            _nativeHash.DecayTransientEvents(
                Time.unscaledTimeAsDouble,
                deltaTime,
                (uint)SpatialTransientEventType.AcousticImpulse,
                AcousticTransientDecayScale,
                AcousticTransientMinimumIntensity);
        }

        internal static void LateFrameMaintenance(int frameCount)
        {
            EnsureInitialized();
            using (_maintenanceProfilerMarker.Auto())
            {
                if (_originShiftRefreshScheduled && _originShiftRefreshHandle.IsCompleted)
                    ConsumeCompletedOriginShiftRefresh();

                if (_validationScheduled && _validationHandle.IsCompleted)
                    ConsumeCompletedValidation();

                if (_farUnloadScheduled && _farUnloadHandle.IsCompleted)
                    ConsumeCompletedFarUnload();

                if (!_validationScheduled && frameCount - _lastValidationFrame >= ValidationCadenceFrames)
                    ScheduleValidation(frameCount);

                if (!_farUnloadScheduled)
                    TryScheduleFarUnload();

                if (frameCount - _lastAcousticDensityFrame >= AcousticDensityMapCadenceFrames)
                {
                    _nativeHash.PruneExpiredTransientEvents(Time.unscaledTimeAsDouble);
                    BuildAcousticDensityMap(frameCount);
                }

                _nativeHash.TrySwapCompletedCompaction();
                _nativeHash.ScheduleCompactionIfOverCapacity(
                    SpatialHashCompactionCapacityThreshold,
                    SpatialHashCompactionTargetFloor,
                    Time.unscaledTimeAsDouble);
            }
        }

        internal static void HandleOriginShift(in OriginShiftEventData shiftData)
        {
            EnsureInitialized();
            ClearAcousticDensityMapForOriginShift();
            int count = _entries.Count;
            if (count <= 0)
                return;

            EnsureOriginShiftCapacity(count);
            int writeIndex = 0;
            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, Entry> pair = enumerator.Current;
                Entry entry = pair.Value;
                if (entry == null || entry.Transform == null || !entry.IsResidentInNativeHash)
                    continue;

                Vector3 runtimePosition = entry.Transform.position;
                entry.RuntimePosition = runtimePosition;
                _originShiftHandles[writeIndex] = pair.Key;
                _originShiftRuntimePositions[writeIndex] = runtimePosition;
                writeIndex++;
            }

            if (writeIndex <= 0)
                return;

            _originShiftRefreshHandle = new RebuildAbsolutePositionsJob
            {
                RuntimePositions = _originShiftRuntimePositions,
                CurrentTotalOffset = HectonFloatingOrigin.CurrentTotalOffset,
                AbsolutePositions = _originShiftAbsolutePositions
            }.Schedule(writeIndex, 64);
            _originShiftRefreshScheduled = true;
            _originShiftRefreshCount = writeIndex;
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
            int speciesId,
            float3 halfExtents = default)
        {
            if (owner == null || targetTransform == null)
                return 0;

            EnsureInitialized();
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(targetTransform.position);
            float3 safeHalfExtents = math.max(halfExtents, 0f);
            ulong entityFlags = ResolveEntityFlags(kind);
            int handle = _nativeHash.Register(positionAup, safeHalfExtents, (int)kind, entityFlags, 0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Assert(handle > 0, "[WorldSpatialHashGrid] Native spatial hash returned an invalid managed-entry handle.");
#endif
            if (handle <= 0)
                return 0;

            _entries[handle] = new Entry
            {
                Transform = targetTransform,
                Owner = owner,
                RuntimePosition = targetTransform.position,
                Kind = kind,
                SignalRole = signalRole,
                SpeciesId = speciesId,
                Layer = targetTransform.gameObject.layer,
                HalfExtents = safeHalfExtents,
                PayloadId = 0,
                EntityFlags = entityFlags,
                IsResidentInNativeHash = true
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
            entry.RuntimePosition = targetTransform.position;
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(entry.RuntimePosition);
            if (entry.EntityFlags == 0UL)
                entry.EntityFlags = ResolveEntityFlags(entry.Kind);
            _nativeHash.UpdateEntry(handle, positionAup, entry.HalfExtents, (int)entry.Kind, entry.EntityFlags, entry.PayloadId);
            entry.IsResidentInNativeHash = true;
            _entries[handle] = entry;
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

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask, ulong interactionFilter = 0UL)
        {
            EnsureInitialized();
            using (_queryProfilerMarker.Auto())
            {
                AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
                return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, interactionFilter, _queryHandles);
            }
        }

        private static void ClearAcousticDensityMapForOriginShift()
        {
            if (_acousticDensityMap.IsCreated)
            {
                for (int i = 0; i < _acousticDensityMap.Length; i++)
                    _acousticDensityMap[i] = 0f;
            }

            _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask, uint interactionFilter)
        {
            return CollectCandidateHandles(origin, radius, kindMask, (ulong)interactionFilter);
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

        private static void ScheduleValidation(int currentFrame)
        {
            EnsureInitialized();
            int count = _entries.Count;
            if (count <= 0)
            {
                _lastValidationFrame = currentFrame;
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
                _lastValidationFrame = currentFrame;
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
                _lastValidationFrame = currentFrame;
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

        private static void TryScheduleFarUnload()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            if (_hasLastFarUnloadPlayerAup &&
                AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastFarUnloadPlayerAup) < FarUnloadPlayerTravelThresholdSq)
            {
                return;
            }

            EnsureInitialized();
            int count = _entries.Count;
            if (count <= 0)
            {
                _lastFarUnloadPlayerAup = playerAup;
                _hasLastFarUnloadPlayerAup = true;
                return;
            }

            EnsureFarUnloadCapacity(count);
            int writeIndex = 0;
            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, Entry> pair = enumerator.Current;
                Entry entry = pair.Value;
                if (entry == null || entry.Transform == null)
                    continue;

                Vector3 runtimePosition = entry.Transform.position;
                entry.RuntimePosition = runtimePosition;
                AbsoluteUniversePosition entryAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
                _farUnloadHandles[writeIndex] = pair.Key;
                _farUnloadAbsolutePositions[writeIndex] = entryAup.ToAbsoluteDouble3();
                _farUnloadEligibilityMask[writeIndex] = IsFarUnloadEligible(entry) ? (byte)1 : (byte)0;
                writeIndex++;
            }

            _lastFarUnloadPlayerAup = playerAup;
            _hasLastFarUnloadPlayerAup = true;
            if (writeIndex <= 0)
                return;

            using (_farUnloadProfilerMarker.Auto())
            {
                _farUnloadCount = writeIndex;
                _farUnloadHandle = new FarUnloadCandidatesJob
                {
                    AbsolutePositions = _farUnloadAbsolutePositions,
                    EligibilityMask = _farUnloadEligibilityMask,
                    PlayerAbsolutePosition = playerAup.ToAbsoluteDouble3(),
                    MaxDistanceSq = FarUnloadDistanceSq,
                    UnloadMask = _farUnloadResultMask
                }.Schedule(writeIndex, 64);
                _farUnloadScheduled = true;
            }
        }

        private static void ConsumeCompletedFarUnload()
        {
            _farUnloadHandle.Complete();
            _farUnloadHandle = default;
            _farUnloadScheduled = false;
            _farUnloadHandleScratch.Clear();

            for (int i = 0; i < _farUnloadCount; i++)
            {
                if (_farUnloadResultMask[i] == 0)
                    continue;

                _farUnloadHandleScratch.Add(_farUnloadHandles[i]);
            }

            for (int i = 0; i < _farUnloadHandleScratch.Count; i++)
            {
                int handle = _farUnloadHandleScratch[i];
                if (!_entries.TryGetValue(handle, out Entry entry) || entry == null || !entry.IsResidentInNativeHash)
                    continue;

                if (entry.Transform != null)
                    entry.RuntimePosition = entry.Transform.position;

                _nativeHash.Evict(handle);
                entry.IsResidentInNativeHash = false;
                AbsoluteUniversePosition entryAup = AbsoluteUniversePosition.FromRuntimePosition(entry.RuntimePosition);
                HectonEventBus.Publish(new SpatialHashEntryUnloadedEvent(
                    handle,
                    entry.Kind,
                    entry.Owner,
                    entry.RuntimePosition,
                    entryAup.ToAbsoluteDouble3(),
                    entry.Layer));
            }

            _farUnloadCount = 0;
            _farUnloadHandleScratch.Clear();
        }

        private static void ConsumeCompletedOriginShiftRefresh()
        {
            _originShiftRefreshHandle.Complete();
            _originShiftRefreshHandle = default;
            _originShiftRefreshScheduled = false;

            for (int i = 0; i < _originShiftRefreshCount; i++)
            {
                int handle = _originShiftHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry) || entry == null)
                    continue;

                entry.RuntimePosition = _originShiftRuntimePositions[i];
                AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromAbsolutePosition(_originShiftAbsolutePositions[i]);
                if (entry.EntityFlags == 0UL)
                    entry.EntityFlags = ResolveEntityFlags(entry.Kind);
                _nativeHash.UpdateEntry(handle, positionAup, entry.HalfExtents, (int)entry.Kind, entry.EntityFlags, entry.PayloadId);
            }

            _originShiftRefreshCount = 0;
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

        private static void EnsureOriginShiftCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (_originShiftHandles.IsCreated && _originShiftHandles.Length >= safeCapacity)
                return;

            DisposeOriginShiftBuffers();
            _originShiftHandles = new NativeArray<int>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _originShiftRuntimePositions = new NativeArray<float3>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _originShiftAbsolutePositions = new NativeArray<float3>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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

        private static void DisposeOriginShiftBuffers()
        {
            if (_originShiftRefreshScheduled)
            {
                _originShiftRefreshHandle.Complete();
                _originShiftRefreshScheduled = false;
            }

            if (_originShiftHandles.IsCreated)
            {
                _originShiftHandles.Dispose();
                _originShiftHandles = default;
            }

            if (_originShiftRuntimePositions.IsCreated)
            {
                _originShiftRuntimePositions.Dispose();
                _originShiftRuntimePositions = default;
            }

            if (_originShiftAbsolutePositions.IsCreated)
            {
                _originShiftAbsolutePositions.Dispose();
                _originShiftAbsolutePositions = default;
            }

            _originShiftRefreshCount = 0;
        }

        private static void EnsureFarUnloadCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (_farUnloadHandles.IsCreated && _farUnloadHandles.Length >= safeCapacity)
                return;

            DisposeFarUnloadBuffers();
            _farUnloadHandles = new NativeArray<int>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _farUnloadAbsolutePositions = new NativeArray<double3>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _farUnloadEligibilityMask = new NativeArray<byte>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _farUnloadResultMask = new NativeArray<byte>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeFarUnloadBuffers()
        {
            if (_farUnloadScheduled)
            {
                _farUnloadHandle.Complete();
                _farUnloadScheduled = false;
            }

            if (_farUnloadHandles.IsCreated)
            {
                _farUnloadHandles.Dispose();
                _farUnloadHandles = default;
            }

            if (_farUnloadAbsolutePositions.IsCreated)
            {
                _farUnloadAbsolutePositions.Dispose();
                _farUnloadAbsolutePositions = default;
            }

            if (_farUnloadEligibilityMask.IsCreated)
            {
                _farUnloadEligibilityMask.Dispose();
                _farUnloadEligibilityMask = default;
            }

            if (_farUnloadResultMask.IsCreated)
            {
                _farUnloadResultMask.Dispose();
                _farUnloadResultMask = default;
            }

            _farUnloadCount = 0;
        }

        private static void EnsureAcousticDensityMap()
        {
            if (_acousticDensityMap.IsCreated && _acousticDensityMap.Length == AcousticDensityMapCellCount)
                return;

            DisposeAcousticDensityMap();
            // COLD ALLOC: NativeArray<float>[512] - 8x8x8 transient acoustic density payload - owner: WorldSpatialHashGrid
            _acousticDensityMap = new NativeArray<float>(AcousticDensityMapCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeAcousticDensityMap()
        {
            if (_acousticDensityMap.IsCreated)
            {
                _acousticDensityMap.Dispose();
                _acousticDensityMap = default;
            }
        }

        private static void BuildAcousticDensityMap(int currentFrame)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return;

            EnsureAcousticDensityMap();
            using (_acousticDensityProfilerMarker.Auto())
            {
                AbsoluteUniversePosition listenerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
                _nativeHash.BuildAcousticDensityMap(
                    in listenerAup,
                    AcousticDensityMapRadiusMeters,
                    Time.unscaledTimeAsDouble,
                    _acousticDensityMap,
                    new int3(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis),
                    (uint)SpatialTransientEventType.AcousticImpulse);
                _lastAcousticDensityFrame = currentFrame;
            }
        }

        private static void TrackTransientSignal(
            Vector3 runtimePosition,
            double expirationTimestamp,
            FieldTargetRole signalRole,
            int sourceSpeciesId)
        {
            _transientSignals[_transientSignalWriteIndex] = new TransientSignalEntry
            {
                RuntimePosition = runtimePosition,
                ExpireTimestamp = expirationTimestamp,
                SignalRole = signalRole,
                SourceSpeciesId = sourceSpeciesId
            };
            _transientSignalWriteIndex = (_transientSignalWriteIndex + 1) % _transientSignals.Length;
        }

        private static uint ComposeTransientSignalSourceKey(FieldTargetRole signalRole, int sourceSpeciesId)
        {
            if (signalRole == FieldTargetRole.Generic && sourceSpeciesId == 0)
                return 0u;

            unchecked
            {
                uint roleBits = ((uint)signalRole & 0xFFu) << 24;
                uint speciesBits = (uint)sourceSpeciesId & 0x00FFFFFFu;
                return roleBits | speciesBits;
            }
        }

        private static ulong ResolveEntityFlags(SpatialTargetKind kind)
        {
            ulong flags = 0UL;
            if ((kind & SpatialTargetKind.Resource) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Resource | SpatialInteractionFlags.Interactable);
            if ((kind & SpatialTargetKind.Bioform) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Bioform | SpatialInteractionFlags.AcousticReceiver | SpatialInteractionFlags.ChemicalReceiver | SpatialInteractionFlags.ThermalReceiver);
            if ((kind & SpatialTargetKind.Signal) != 0)
                flags |= (ulong)SpatialInteractionFlags.Signal;
            if ((kind & SpatialTargetKind.Pickup) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Pickup | SpatialInteractionFlags.Interactable);
            if ((kind & SpatialTargetKind.Scannable) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Scannable | SpatialInteractionFlags.Interactable);
            if ((kind & SpatialTargetKind.Module) != 0)
                flags |= (ulong)(SpatialInteractionFlags.Module | SpatialInteractionFlags.Interactable);
            return flags;
        }

        private static bool IsFarUnloadEligible(Entry entry)
        {
            if (entry == null || !entry.IsResidentInNativeHash)
                return false;

            SpatialTargetKind dynamicKinds = SpatialTargetKind.Pickup | SpatialTargetKind.Bioform | SpatialTargetKind.Signal;
            return (entry.Kind & dynamicKinds) != 0;
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
                case FieldTargetRole.DistressBeacon:
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
