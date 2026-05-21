using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
            int layer,
            bool isPreyTag = false,
            Rigidbody rigidbody = null)
        {
            Transform = transform;
            Owner = owner;
            Rigidbody = rigidbody;
            Position = position;
            DistanceSqr = distanceSqr;
            Kind = kind;
            SignalRole = signalRole;
            SpeciesId = speciesId;
            Layer = layer;
            AbsolutePosition = default;
            HasAbsolutePosition = false;
            IsPreyTag = isPreyTag;
        }

        public SpatialQueryHit(
            Transform transform,
            Component owner,
            Vector3 position,
            AbsoluteUniversePosition absolutePosition,
            float distanceSqr,
            SpatialTargetKind kind,
            FieldTargetRole signalRole,
            int speciesId,
            int layer,
            bool isPreyTag = false,
            Rigidbody rigidbody = null)
        {
            Transform = transform;
            Owner = owner;
            Rigidbody = rigidbody;
            Position = position;
            DistanceSqr = distanceSqr;
            Kind = kind;
            SignalRole = signalRole;
            SpeciesId = speciesId;
            Layer = layer;
            AbsolutePosition = absolutePosition;
            HasAbsolutePosition = true;
            IsPreyTag = isPreyTag;
        }

        public Transform Transform { get; }
        public Component Owner { get; }
        public Rigidbody Rigidbody { get; }
        public Vector3 Position { get; }
        public AbsoluteUniversePosition AbsolutePosition { get; }
        public AbsoluteUniversePosition PositionAup => AbsolutePosition;
        public bool HasAbsolutePosition { get; }
        public float DistanceSqr { get; }
        public SpatialTargetKind Kind { get; }
        public FieldTargetRole SignalRole { get; }
        public int SpeciesId { get; }
        public int Layer { get; }
        public bool IsPreyTag { get; }
    }

    /// <summary>
    /// Compatibility facade over the native AUP-aware broadphase.
    /// Existing callers keep the old API while all candidate enumeration routes through HectonSpatialHash.
    /// </summary>
    internal static class WorldSpatialHashGrid
    {
        private struct Entry
        {
            public Transform Transform;
            public Component Owner;
            public Rigidbody Rigidbody;
            public Vector3 RuntimePosition;
            public AbsoluteUniversePosition AbsolutePosition;
            public SpatialTargetKind Kind;
            public FieldTargetRole SignalRole;
            public int SpeciesId;
            public int Layer;
            public byte IsPreyTag;
            public float3 HalfExtents;
            public int PayloadId;
            public ulong EntityFlags;
            public byte IsResidentInNativeHash;
        }

        private struct TransientSignalEntry
        {
            public Vector3 RuntimePosition;
            public AbsoluteUniversePosition AbsolutePosition;
            public double ExpireTimestamp;
            public FieldTargetRole SignalRole;
            public int SourceSpeciesId;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ValidateAupIntegrityJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<double3> AbsolutePositions;
            [ReadOnly, NoAlias] public NativeArray<float3> RuntimePositions;
            public double3 CommittedTotalOffset;
            [WriteOnly, NoAlias] public NativeArray<byte> InvalidMask;

            public void Execute(int index)
            {
                float3 runtime = RuntimePositions[index];
                double3 reconstructedAbsolute = new double3(runtime.x, runtime.y, runtime.z) + CommittedTotalOffset;
                double3 delta = reconstructedAbsolute - AbsolutePositions[index];
                InvalidMask[index] = math.lengthsq(delta) <= 0.01d ? (byte)0 : (byte)1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct FarUnloadCandidatesJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<double3> AbsolutePositions;
            [ReadOnly, NoAlias] public NativeArray<byte> EligibilityMask;
            public double3 PlayerAbsolutePosition;
            public double MaxDistanceSq;
            [WriteOnly, NoAlias] public NativeArray<byte> UnloadMask;

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
        private const int MaxSpatialMaintenanceEntryCapacity = 8192;
        private const int MaxQueryHandleCapacity = 256;
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
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const Allocator DataVaultExemptWorldSpatialQueryAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptWorldSpatialMaintenanceAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptWorldSpatialAcousticAllocator = Allocator.Persistent;

        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Query");
        private static readonly ProfilerMarker _maintenanceProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Maintenance");
        private static readonly ProfilerMarker _validationProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Validation");
        private static readonly ProfilerMarker _farUnloadProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.FarUnload");
        private static readonly ProfilerMarker _acousticDensityProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.AcousticDensity");

        // COLD ALLOC: Dictionary<int,Entry>(256) — runtime metadata registry layered over the native AUP spatial hash — owner: WorldSpatialHashGrid
        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(MaxSpatialMaintenanceEntryCapacity);
        // COLD ALLOC: Dictionary<ulong,int>(256) - full EntityId to latest spatial handle reverse lookup - owner: WorldSpatialHashGrid
        private static readonly Dictionary<ulong, int> _handleByTransformId = new Dictionary<ulong, int>(MaxSpatialMaintenanceEntryCapacity);
        // COLD ALLOC: int[8192] - deferred far-unload handle scratch for dynamic native-hash eviction - owner: WorldSpatialHashGrid
        private static readonly int[] _farUnloadHandleScratch = new int[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: int[8192] - main-thread origin-shift key scratch, not a job payload - owner: WorldSpatialHashGrid
        private static readonly int[] _originShiftHandles = new int[MaxSpatialMaintenanceEntryCapacity];

        private static readonly TransientSignalEntry[] _transientSignals = new TransientSignalEntry[MaxTransientSignalCount]; // COLD ALLOC: TransientSignalEntry[16] - transient PDA sonar signal ring - owner: WorldSpatialHashGrid

        private static HectonSpatialHash _nativeHash;
        private static NativeList<int> _queryHandles;
        private static NativeArray<double3> _validationAbsolutePositions;
        private static NativeArray<float3> _validationRuntimePositions;
        private static NativeArray<byte> _validationInvalidMask;
        private static JobHandle _validationHandle;
        private static bool _validationScheduled;
        private static int _validationCount;
        private static NativeArray<int> _farUnloadHandles;
        private static NativeArray<double3> _farUnloadAbsolutePositions;
        private static NativeArray<byte> _farUnloadEligibilityMask;
        private static NativeArray<byte> _farUnloadResultMask;
        private static JobHandle _farUnloadHandle;
        private static bool _farUnloadScheduled;
        private static int _farUnloadCount;
        private static int _farUnloadHandleScratchCount;
        private static NativeArray<float> _acousticDensityMap;
        private static int _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
        private static int _transientSignalWriteIndex;
        private static AbsoluteUniversePosition _lastFarUnloadPlayerAup;
        private static bool _hasLastFarUnloadPlayerAup;
        private static int _lastValidationFrame = -ValidationCadenceFrames;
        private static bool _lastResultBufferSaturated;

        internal static int ActiveEntityCount => _nativeHash != null ? _nativeHash.EntryCount : _entries.Count;
        internal static HectonSpatialHash.QueryStats LastNativeQueryStats => _nativeHash != null ? _nativeHash.LastQueryStats : default;
        internal static bool LastNativeQuerySaturated => _nativeHash != null && _nativeHash.LastQueryStats.IsSaturated;
        internal static bool LastResultBufferSaturated => _lastResultBufferSaturated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearRuntimeState();
        }

        internal static void ClearRuntimeState()
        {
            _entries.Clear();
            _handleByTransformId.Clear();
            JobHandle teardownDependency = JobHandle.CombineDependencies(
                CancelValidationForTeardown(),
                CancelFarUnloadForTeardown());
            teardownDependency = DisposeValidationBuffers(teardownDependency);
            teardownDependency = DisposeFarUnloadBuffers(teardownDependency);
            JobHandle.ScheduleBatchedJobs();
            DispatcherJobSwap.TryComplete(ref teardownDependency, forceComplete: true);
            DisposeAcousticDensityMap();
            _farUnloadHandleScratchCount = 0;
            if (_queryHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(WorldSpatialHashGrid), nameof(_queryHandles));
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
            _farUnloadHandleScratchCount = 0;
            _hasLastFarUnloadPlayerAup = false;
            _lastValidationFrame = -ValidationCadenceFrames;
            _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
            _transientSignalWriteIndex = 0;
            _lastResultBufferSaturated = false;
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

        public static int RegisterScannable(ScannableFragment fragment)
        {
            return Register(fragment, fragment != null ? fragment.transform : null, SpatialTargetKind.Scannable, FieldTargetRole.Generic, 0);
        }

        public static int RegisterModule(ModuleMarker marker)
        {
            FieldTargetRole role = marker != null ? marker.SpatialRole : FieldTargetRole.Generic;
            return Register(marker, marker != null ? marker.transform : null, SpatialTargetKind.Module, role, 0);
        }

        public static void UpdateSignalRole(int handle, FieldTargetRole signalRole)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry))
                return;

            entry.SignalRole = signalRole;
            _entries[handle] = entry;
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
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry))
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
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry))
                return;

            if (entry.Transform == null)
            {
                Unregister(handle);
                return;
            }

            UpdateNativeEntry(handle, entry);
        }

        public static bool TryGetAbsolutePosition(int handle, out AbsoluteUniversePosition position)
        {
            position = default;
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry) || !IsFiniteAup(in entry.AbsolutePosition))
                return false;

            position = entry.AbsolutePosition;
            return true;
        }

        public static void SetResourceHalfExtents(int handle, Vector3 halfExtents)
        {
            SetResourceHalfExtents(handle, (float3)halfExtents);
        }

        public static void SetResourceHalfExtents(int handle, float3 halfExtents)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out Entry entry))
                return;

            entry.HalfExtents = math.max(halfExtents, 0f);
            _entries[handle] = entry;
            UpdateNativeEntry(handle, entry);
        }

        public static void Unregister(int handle)
        {
            if (handle <= 0)
                return;

            if (!_entries.TryGetValue(handle, out Entry entry))
                return;

            RemoveTransformHandle(handle, entry.Transform);
            if (_nativeHash != null && entry.IsResidentInNativeHash != 0)
                _nativeHash.Unregister(handle);
            else if (_nativeHash != null)
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
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f)
            {
                ResetQueryTelemetry();
                return false;
            }

            bool found = false;
            double bestDistanceSqr = (double)radius * radius;
            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                ResetQueryTelemetry();
                return false;
            }

            int handleCount = CollectCandidateHandles(origin, radius, SpatialTargetKind.Bioform);
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
                AbsoluteUniversePosition candidateAup = entry.AbsolutePosition;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                hit = new SpatialQueryHit(
                    candidateTransform,
                    entry.Owner,
                    position,
                    candidateAup,
                    ClampDistanceSqrToFloat(distanceSqr),
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0,
                    entry.Rigidbody);
                found = true;
            }

            return found;
        }

        public static bool TryGetNearestAggressiveBioform(
            Vector3 origin,
            float radius,
            int layerMask,
            Transform ignoreTransform,
            out SpatialQueryHit hit)
        {
            if (!IsFiniteRuntimePosition(origin))
            {
                hit = default;
                ResetQueryTelemetry();
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                hit = default;
                ResetQueryTelemetry();
                return false;
            }

            return TryGetNearestAggressiveBioform(
                origin,
                in originAup,
                radius,
                layerMask,
                ignoreTransform,
                out hit);
        }

        public static bool TryGetNearestAggressiveBioform(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            float radius,
            int layerMask,
            Transform ignoreTransform,
            out SpatialQueryHit hit)
        {
            hit = default;
            if (!IsFiniteRuntimePosition(origin) || !IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
            {
                ResetQueryTelemetry();
                return false;
            }

            bool found = false;
            double bestDistanceSqr = (double)radius * radius;
            int handleCount = CollectCandidateHandles(origin, radius, SpatialTargetKind.Bioform);
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

                if (!(entry.Owner is FaunaBrain brain) || !brain.isAggressive)
                    continue;

                Vector3 position = entry.RuntimePosition;
                AbsoluteUniversePosition candidateAup = entry.AbsolutePosition;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                hit = new SpatialQueryHit(
                    candidateTransform,
                    entry.Owner,
                    position,
                    candidateAup,
                    ClampDistanceSqrToFloat(distanceSqr),
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0,
                    entry.Rigidbody);
                found = true;
            }

            return found;
        }

        public static void BuildSonarSnapshot(Vector3 origin, float radius, out SpatialSonarSnapshot snapshot)
        {
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f)
            {
                ResetQueryTelemetry();
                snapshot = default;
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                ResetQueryTelemetry();
                snapshot = default;
                return;
            }

            BuildSonarSnapshot(origin, in originAup, radius, out snapshot);
        }

        internal static void BuildSonarSnapshot(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            float radius,
            out SpatialSonarSnapshot snapshot)
        {
            if (!IsFiniteRuntimePosition(origin) || !IsFiniteAup(in originAup) || !math.isfinite(radius) || radius <= 0f)
            {
                ResetQueryTelemetry();
                snapshot = default;
                return;
            }

            int resourceCount = 0;
            int bioformCount = 0;
            int signalCount = 0;

            bool hasNearestResource = false;
            bool hasNearestBioform = false;
            bool hasNearestSignal = false;
            double nearestResourceDistanceSqr = double.PositiveInfinity;
            double nearestBioformDistanceSqr = double.PositiveInfinity;
            double nearestSignalDistanceSqr = double.PositiveInfinity;
            float nearestResourceDistanceMeters = 0f;
            float nearestBioformDistanceMeters = 0f;
            float nearestSignalDistanceMeters = 0f;
            FieldTargetRole nearestSignalRole = FieldTargetRole.Generic;
            double radiusSqr = (double)radius * radius;

            int handleCount = CollectCandidateHandles(origin, radius, SpatialTargetKind.Resource | SpatialTargetKind.Bioform | SpatialTargetKind.Signal | SpatialTargetKind.Module);
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

                Vector3 position = entry.RuntimePosition;
                AbsoluteUniversePosition candidateAup = entry.AbsolutePosition;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > radiusSqr)
                    continue;

                SpatialTargetKind kind = entry.Kind;
                if ((kind & SpatialTargetKind.Resource) != 0)
                {
                    resourceCount++;
                    if (distanceSqr < nearestResourceDistanceSqr)
                    {
                        nearestResourceDistanceSqr = distanceSqr;
                        nearestResourceDistanceMeters = ApproximateAupDistanceMeters(in candidateAup, in originAup);
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
                        nearestBioformDistanceMeters = ApproximateAupDistanceMeters(in candidateAup, in originAup);
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
                    nearestSignalDistanceMeters = ApproximateAupDistanceMeters(in candidateAup, in originAup);
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

                AbsoluteUniversePosition signalAup = signalEntry.AbsolutePosition;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in signalAup, in originAup);
                if (distanceSqr > radiusSqr)
                    continue;

                signalCount++;
                if (distanceSqr < nearestSignalDistanceSqr)
                {
                    nearestSignalDistanceSqr = distanceSqr;
                    nearestSignalDistanceMeters = ApproximateAupDistanceMeters(in signalAup, in originAup);
                    nearestSignalRole = signalEntry.SignalRole;
                    hasNearestSignal = true;
                }
            }

            snapshot = new SpatialSonarSnapshot(
                resourceCount,
                bioformCount,
                signalCount,
                hasNearestResource,
                hasNearestResource ? ClampDistanceToHud(nearestResourceDistanceMeters) : 0,
                hasNearestBioform,
                hasNearestBioform ? ClampDistanceToHud(nearestBioformDistanceMeters) : 0,
                hasNearestSignal,
                hasNearestSignal ? ClampDistanceToHud(nearestSignalDistanceMeters) : 0,
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
            ResetQueryTelemetry();
            if (!IsFiniteRuntimePosition(origin) || IsInvalidContactQuery(radius, kindMask, results))
                return 0;

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return 0;

            return CollectContactsNonAllocChecked(origin, in originAup, radius, kindMask, interactionFilter, results);
        }

        internal static int CollectContactsNonAlloc(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            float radius,
            SpatialTargetKind kindMask,
            SpatialInteractionFlags interactionFilter,
            SpatialQueryHit[] results)
        {
            ResetQueryTelemetry();
            if (!IsFiniteRuntimePosition(origin) || !IsFiniteAup(in originAup) || IsInvalidContactQuery(radius, kindMask, results))
                return 0;

            return CollectContactsNonAllocChecked(origin, in originAup, radius, kindMask, (ulong)interactionFilter, results);
        }

        private static int CollectContactsNonAllocChecked(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            float radius,
            SpatialTargetKind kindMask,
            ulong interactionFilter,
            SpatialQueryHit[] results)
        {
            int count = 0;
            double radiusSqr = (double)radius * radius;
            int handleCount = CollectCandidateHandles(origin, radius, kindMask, interactionFilter);
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
                Vector3 position = entry.RuntimePosition;
                AbsoluteUniversePosition candidateAup = entry.AbsolutePosition;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSqr > radiusSqr)
                    continue;

                results[count] = new SpatialQueryHit(
                    candidateTransform,
                    entry.Owner,
                    position,
                    candidateAup,
                    ClampDistanceSqrToFloat(distanceSqr),
                    entry.Kind,
                    entry.SignalRole,
                    entry.SpeciesId,
                    entry.Layer,
                    entry.IsPreyTag != 0,
                    entry.Rigidbody);
                count++;

                if (count >= results.Length)
                {
                    _lastResultBufferSaturated = i + 1 < handleCount;
                    break;
                }
            }

            return count;
        }

        private static bool IsInvalidContactQuery(float radius, SpatialTargetKind kindMask, SpatialQueryHit[] results)
        {
            return results == null ||
                   results.Length == 0 ||
                   kindMask == SpatialTargetKind.None ||
                   !math.isfinite(radius) ||
                   radius <= 0f;
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
            if (IsInvalidTransientEvent(worldPosition, radiusMeters, intensity, lifetimeSeconds, eventType, temperature))
                return;

            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return;

            RegisterTransientEvent(
                worldPosition,
                in positionAup,
                radiusMeters,
                intensity,
                lifetimeSeconds,
                eventType,
                eventFlags,
                signalRole,
                sourceSpeciesId,
                temperature);
        }

        internal static void RegisterTransientEvent(
            Vector3 worldPosition,
            in AbsoluteUniversePosition positionAup,
            float radiusMeters,
            float intensity,
            float lifetimeSeconds,
            SpatialTransientEventType eventType,
            SpatialInteractionFlags eventFlags = SpatialInteractionFlags.None,
            FieldTargetRole signalRole = FieldTargetRole.Generic,
            int sourceSpeciesId = 0,
            float temperature = 0f)
        {
            if (IsInvalidTransientEvent(worldPosition, radiusMeters, intensity, lifetimeSeconds, eventType, temperature) ||
                !IsFiniteAup(in positionAup))
                return;

            EnsureInitialized();
            double currentTimestamp = Time.unscaledTimeAsDouble;
            if (!IsFiniteDouble(currentTimestamp))
                return;

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
                TrackTransientSignal(worldPosition, in positionAup, expirationTimestamp, signalRole, sourceSpeciesId);
        }

        private static bool IsInvalidTransientEvent(
            Vector3 worldPosition,
            float radiusMeters,
            float intensity,
            float lifetimeSeconds,
            SpatialTransientEventType eventType,
            float temperature)
        {
            float3 worldPositionFloat3 = worldPosition;
            return !math.all(math.isfinite(worldPositionFloat3)) ||
                   !math.isfinite(radiusMeters) ||
                   !math.isfinite(intensity) ||
                   !math.isfinite(lifetimeSeconds) ||
                   !math.isfinite(temperature) ||
                   radiusMeters <= 0f ||
                   intensity <= 0f ||
                   lifetimeSeconds <= 0f ||
                   eventType == SpatialTransientEventType.None;
        }

        /// <summary>
        /// Clears one transient signal source immediately, used by mimic fauna once the false beacon has served its ambush role.
        /// </summary>
        public static void ClearTransientSignal(FieldTargetRole signalRole, int sourceSpeciesId)
        {
            uint sourceKey = ComposeTransientSignalSourceKey(signalRole, sourceSpeciesId);
            double currentTimestamp = Time.unscaledTimeAsDouble;
            if (!IsFiniteDouble(currentTimestamp))
                return;

            if (sourceKey != 0u)
            {
                if (_nativeHash != null)
                {
                    _nativeHash.ClearTransientEvents((uint)SpatialTransientEventType.AcousticImpulse, sourceKey, currentTimestamp);
                    _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
                }
            }

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
            densityMap = _acousticDensityMap;
            dimensions = new Vector3Int(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis);
            return _acousticDensityMap.IsCreated;
        }

        public static bool IsHandleCurrent(int handle)
        {
            return handle > 0 && _nativeHash != null && _nativeHash.IsCurrentHandle(handle);
        }

        public static bool QueryTemperatureGradient(
            Vector3 origin,
            float radiusMeters,
            out float temperatureDeltaCelsius,
            out Vector3 gradient)
        {
            temperatureDeltaCelsius = 0f;
            gradient = Vector3.zero;
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return false;

            if (_nativeHash == null)
                return false;

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return false;

            bool hasGradient = _nativeHash.QueryTemperatureGradient(
                in originAup,
                radiusMeters,
                Time.unscaledTimeAsDouble,
                out temperatureDeltaCelsius,
                out double3 gradientAup);
            float3 gradientLocal = AupPrecisionMath.DowncastLocalDelta(gradientAup, float3.zero);
            gradient = new Vector3(gradientLocal.x, gradientLocal.y, gradientLocal.z);
            return hasGradient;
        }

        internal static void SlowTickMaintenance(float deltaTime)
        {
            if (_nativeHash == null)
                return;

            _nativeHash.DecayTransientEvents(
                Time.unscaledTimeAsDouble,
                deltaTime,
                (uint)SpatialTransientEventType.AcousticImpulse,
                AcousticTransientDecayScale,
                AcousticTransientMinimumIntensity);
        }

        internal static void LateFrameMaintenance(int frameCount)
        {
            if (_nativeHash == null)
                return;

            using (_maintenanceProfilerMarker.Auto())
            {
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
            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            if (!IsFiniteRuntimePosition(runtimeOffset))
            {
                ClearAcousticDensityMapForOriginShift();
                return;
            }

            if (_nativeHash == null)
            {
                ClearAcousticDensityMapForOriginShift();
                RebaseTransientSignalRuntimePositions(runtimeOffset);
                return;
            }

            EnsureInitialized();
            ClearAcousticDensityMapForOriginShift();

            int count = _entries.Count;
            RebaseTransientSignalRuntimePositions(runtimeOffset);
            if (count <= 0)
                return;

            int writeIndex = 0;
            Dictionary<int, Entry>.Enumerator enumerator = _entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, Entry> pair = enumerator.Current;
                Entry entry = pair.Value;
                if (writeIndex >= _originShiftHandles.Length)
                    break;
                if (entry.Transform == null || !IsFiniteRuntimePosition(entry.RuntimePosition))
                    continue;

                _originShiftHandles[writeIndex] = pair.Key;
                writeIndex++;
            }

            if (writeIndex <= 0)
                return;

            for (int i = 0; i < writeIndex; i++)
            {
                int handle = _originShiftHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                    continue;

                Vector3 shiftedRuntimePosition = entry.RuntimePosition + runtimeOffset;
                if (!IsFiniteRuntimePosition(shiftedRuntimePosition))
                    continue;

                entry.RuntimePosition = shiftedRuntimePosition;
                _entries[handle] = entry;
            }

        }

        private static void EnsureInitialized()
        {
            if (_nativeHash == null)
                _nativeHash = new HectonSpatialHash(
                    MaxSpatialMaintenanceEntryCapacity,
                    MaxSpatialMaintenanceEntryCapacity * 4,
                    CellSizeMeters,
                    NativeMemoryLifetime);

            if (!_queryHandles.IsCreated)
            {
                _queryHandles = new NativeList<int>(MaxQueryHandleCapacity, DataVaultExemptWorldSpatialQueryAllocator);
                NativeMemorySentinel.RegisterNativeList(
                    _queryHandles,
                    nameof(WorldSpatialHashGrid),
                    nameof(_queryHandles),
                    NativeMemoryLifetime);
            }
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
            if (_entries.Count >= MaxSpatialMaintenanceEntryCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[WorldSpatialHashGrid] Entry capacity exceeded. Runtime buffer growth is forbidden.");
#endif
                return 0;
            }

            Vector3 runtimePosition = targetTransform.position;
            if (!IsFiniteRuntimePosition(runtimePosition) || !IsFiniteFloat3(halfExtents))
                return 0;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return 0;

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
                Rigidbody = ResolveCachedRigidbody(owner, targetTransform),
                RuntimePosition = runtimePosition,
                AbsolutePosition = positionAup,
                Kind = kind,
                SignalRole = signalRole,
                SpeciesId = speciesId,
                Layer = targetTransform.gameObject.layer,
                IsPreyTag = kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey") ? (byte)1 : (byte)0,
                HalfExtents = safeHalfExtents,
                PayloadId = 0,
                EntityFlags = entityFlags,
                IsResidentInNativeHash = 1
            };
            _handleByTransformId[ResolveTransformEntityKey(targetTransform)] = handle;
            return handle;
        }

        private static Rigidbody ResolveCachedRigidbody(Component owner, Transform targetTransform)
        {
            Rigidbody body = null;
            if (owner != null && owner.TryGetComponent(out body))
                return body;

            if (targetTransform != null && targetTransform.TryGetComponent(out body))
                return body;

            return null;
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
            entry.IsPreyTag = entry.Kind == SpatialTargetKind.Bioform && targetTransform.CompareTag("Prey") ? (byte)1 : (byte)0;
            Vector3 runtimePosition = targetTransform.position;
            if (!IsFiniteRuntimePosition(runtimePosition) || !IsFiniteFloat3(entry.HalfExtents))
            {
                Unregister(handle);
                return;
            }

            entry.RuntimePosition = runtimePosition;
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
            {
                Unregister(handle);
                return;
            }

            entry.AbsolutePosition = positionAup;
            if (entry.EntityFlags == 0UL)
                entry.EntityFlags = ResolveEntityFlags(entry.Kind);
            if (!_nativeHash.TryUpdateEntry(handle, positionAup, entry.HalfExtents, (int)entry.Kind, entry.EntityFlags, entry.PayloadId))
            {
                Unregister(handle);
                return;
            }

            entry.IsResidentInNativeHash = 1;
            _entries[handle] = entry;
        }

        private static int FindHandle(Transform targetTransform)
        {
            if (targetTransform == null)
                return 0;

            ulong transformId = ResolveTransformEntityKey(targetTransform);
            if (!_handleByTransformId.TryGetValue(transformId, out int handle))
                return 0;

            if (!_entries.TryGetValue(handle, out Entry entry) || !ReferenceEquals(entry.Transform, targetTransform))
            {
                _handleByTransformId.Remove(transformId);
                return 0;
            }

            return handle;
        }

        private static void RemoveTransformHandle(int handle, Transform targetTransform)
        {
            if (targetTransform == null)
                return;

            ulong transformId = ResolveTransformEntityKey(targetTransform);
            if (_handleByTransformId.TryGetValue(transformId, out int mappedHandle) && mappedHandle == handle)
                _handleByTransformId.Remove(transformId);
        }

        private static ulong ResolveTransformEntityKey(Transform targetTransform)
        {
            return targetTransform != null
                ? EntityId.ToULong(targetTransform.GetEntityId())
                : 0UL;
        }

        private static void DropNativeOnlyHandle(int handle)
        {
            if (handle <= 0 || _nativeHash == null)
                return;

            _nativeHash.Unregister(handle);
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask, ulong interactionFilter = 0UL)
        {
            ResetQueryTelemetry();
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f || kindMask == SpatialTargetKind.None)
                return 0;

            if (_nativeHash == null || _entries.Count == 0)
                return 0;

            EnsureInitialized();
            using (_queryProfilerMarker.Auto())
            {
                if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                    return 0;

                return _nativeHash.CollectSphere(originAup, radius, (int)kindMask, interactionFilter, _queryHandles);
            }
        }

        private static void ResetQueryTelemetry()
        {
            _lastResultBufferSaturated = false;
            if (_nativeHash != null)
                _nativeHash.ClearLastQueryStats();
        }

        private static bool IsEntryQueryEligible(Entry entry)
        {
            if (entry.Transform == null || entry.Owner == null)
                return false;

            GameObject targetObject = entry.Transform.gameObject;
            if (targetObject == null || !targetObject.activeInHierarchy)
                return false;

            if (entry.Owner is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;

            return !(entry.Owner is FaunaBrain faunaBrain) || !faunaBrain.IsDead;
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            float3 value = position;
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteDouble3(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteDouble(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.all(math.isfinite(new float3(position.LocalX, position.LocalY, position.LocalZ)));
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
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

        private static void RebaseTransientSignalRuntimePositions(Vector3 runtimeOffset)
        {
            for (int i = 0; i < _transientSignals.Length; i++)
            {
                TransientSignalEntry signal = _transientSignals[i];
                if (signal.ExpireTimestamp <= 0d)
                    continue;

                if (!IsFiniteRuntimePosition(signal.RuntimePosition))
                    continue;

                Vector3 shiftedRuntimePosition = signal.RuntimePosition + runtimeOffset;
                if (!IsFiniteRuntimePosition(shiftedRuntimePosition))
                    continue;

                signal.RuntimePosition = shiftedRuntimePosition;
                _transientSignals[i] = signal;
            }
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask, uint interactionFilter)
        {
            return CollectCandidateHandles(origin, radius, kindMask, (ulong)interactionFilter);
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
                if (entry.Transform == null)
                    continue;
                if (writeIndex >= _validationAbsolutePositions.Length)
                    break;

                Vector3 runtimePosition = entry.Transform.position;
                if (!IsFiniteRuntimePosition(runtimePosition))
                    continue;

                if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                    continue;

                _validationRuntimePositions[writeIndex] = runtimePosition;
                _validationAbsolutePositions[writeIndex] = positionAup.ToAbsoluteDouble3();
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
                    CommittedTotalOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                    InvalidMask = _validationInvalidMask
                }.Schedule(writeIndex, 64);
                _validationScheduled = true;
                _lastValidationFrame = currentFrame;
            }
        }

        private static void ConsumeCompletedValidation()
        {
            if (!DispatcherJobSwap.TryComplete(ref _validationHandle, forceComplete: false))
                return;

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
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement == null)
                return;

            AbsoluteUniversePosition playerAup = playerMovement.CurrentAup;
            if (!IsFiniteAup(in playerAup))
                return;

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
                if (entry.Transform == null)
                    continue;
                if (writeIndex >= _farUnloadHandles.Length)
                    break;

                Vector3 runtimePosition = entry.Transform.position;
                if (!IsFiniteRuntimePosition(runtimePosition))
                    continue;

                entry.RuntimePosition = runtimePosition;
                if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition entryAup))
                    continue;

                entry.AbsolutePosition = entryAup;
                _farUnloadHandles[writeIndex] = pair.Key;
                _farUnloadAbsolutePositions[writeIndex] = entryAup.ToAbsoluteDouble3();
                _farUnloadEligibilityMask[writeIndex] = IsFarUnloadEligible(entry) ? (byte)1 : (byte)0;
                writeIndex++;
            }

            double3 currentTotalOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            for (int i = 0; i < writeIndex; i++)
            {
                int handle = _farUnloadHandles[i];
                if (!_entries.TryGetValue(handle, out Entry entry))
                    continue;

                entry.RuntimePosition = new Vector3(
                    (float)(_farUnloadAbsolutePositions[i].x - currentTotalOffset.x),
                    (float)(_farUnloadAbsolutePositions[i].y - currentTotalOffset.y),
                    (float)(_farUnloadAbsolutePositions[i].z - currentTotalOffset.z));
                entry.AbsolutePosition = AbsoluteUniversePosition.FromAbsolutePosition(_farUnloadAbsolutePositions[i]);
                _entries[handle] = entry;
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
            if (!DispatcherJobSwap.TryComplete(ref _farUnloadHandle, forceComplete: false))
                return;

            _farUnloadScheduled = false;
            _farUnloadHandleScratchCount = 0;

            for (int i = 0; i < _farUnloadCount; i++)
            {
                if (_farUnloadResultMask[i] == 0)
                    continue;

                if (_farUnloadHandleScratchCount >= _farUnloadHandleScratch.Length)
                    break;

                _farUnloadHandleScratch[_farUnloadHandleScratchCount] = _farUnloadHandles[i];
                _farUnloadHandleScratchCount++;
            }

            for (int i = 0; i < _farUnloadHandleScratchCount; i++)
            {
                int handle = _farUnloadHandleScratch[i];
                if (!_entries.TryGetValue(handle, out Entry entry) || entry.IsResidentInNativeHash == 0)
                    continue;

                if (entry.Transform != null)
                {
                    Vector3 runtimePosition = entry.Transform.position;
                    if (IsFiniteRuntimePosition(runtimePosition))
                    {
                        entry.RuntimePosition = runtimePosition;
                        if (TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition entryAup))
                            entry.AbsolutePosition = entryAup;
                    }
                }

                _nativeHash.Evict(handle);
                entry.IsResidentInNativeHash = 0;
                _entries[handle] = entry;
            }

            _farUnloadCount = 0;
            _farUnloadHandleScratchCount = 0;
        }

        private static void EnsureValidationCapacity(int requiredCapacity)
        {
            if (_validationAbsolutePositions.IsCreated &&
                _validationRuntimePositions.IsCreated &&
                _validationInvalidMask.IsCreated)
                return;

            DisposeValidationBuffers();
            _validationAbsolutePositions = new NativeArray<double3>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            _validationRuntimePositions = new NativeArray<float3>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            _validationInvalidMask = new NativeArray<byte>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _validationAbsolutePositions,
                nameof(WorldSpatialHashGrid),
                nameof(_validationAbsolutePositions),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _validationRuntimePositions,
                nameof(WorldSpatialHashGrid),
                nameof(_validationRuntimePositions),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _validationInvalidMask,
                nameof(WorldSpatialHashGrid),
                nameof(_validationInvalidMask),
                NativeMemoryLifetime);
        }

        private static void DisposeValidationBuffers()
        {
            if (_validationScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _validationHandle, forceComplete: true);
                _validationScheduled = false;
            }

            if (_validationAbsolutePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationAbsolutePositions);
                _validationAbsolutePositions.Dispose();
                _validationAbsolutePositions = default;
            }

            if (_validationRuntimePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationRuntimePositions);
                _validationRuntimePositions.Dispose();
                _validationRuntimePositions = default;
            }

            if (_validationInvalidMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationInvalidMask);
                _validationInvalidMask.Dispose();
                _validationInvalidMask = default;
            }

            _validationCount = 0;
        }

        private static JobHandle CancelValidationForTeardown()
        {
            if (!_validationScheduled)
                return _validationHandle;

            JobHandle dependency = _validationHandle;
            _validationHandle = default;
            _validationScheduled = false;
            _validationCount = 0;
            return dependency;
        }

        private static JobHandle DisposeValidationBuffers(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;

            if (_validationAbsolutePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationAbsolutePositions);
                disposeHandle = _validationAbsolutePositions.Dispose(disposeHandle);
                _validationAbsolutePositions = default;
            }

            if (_validationRuntimePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationRuntimePositions);
                disposeHandle = _validationRuntimePositions.Dispose(disposeHandle);
                _validationRuntimePositions = default;
            }

            if (_validationInvalidMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationInvalidMask);
                disposeHandle = _validationInvalidMask.Dispose(disposeHandle);
                _validationInvalidMask = default;
            }

            _validationCount = 0;
            return disposeHandle;
        }

        private static void EnsureFarUnloadCapacity(int requiredCapacity)
        {
            if (_farUnloadHandles.IsCreated &&
                _farUnloadAbsolutePositions.IsCreated &&
                _farUnloadEligibilityMask.IsCreated &&
                _farUnloadResultMask.IsCreated)
                return;

            DisposeFarUnloadBuffers();
            _farUnloadHandles = new NativeArray<int>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            _farUnloadAbsolutePositions = new NativeArray<double3>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            _farUnloadEligibilityMask = new NativeArray<byte>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            _farUnloadResultMask = new NativeArray<byte>(MaxSpatialMaintenanceEntryCapacity, DataVaultExemptWorldSpatialMaintenanceAllocator, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _farUnloadHandles,
                nameof(WorldSpatialHashGrid),
                nameof(_farUnloadHandles),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _farUnloadAbsolutePositions,
                nameof(WorldSpatialHashGrid),
                nameof(_farUnloadAbsolutePositions),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _farUnloadEligibilityMask,
                nameof(WorldSpatialHashGrid),
                nameof(_farUnloadEligibilityMask),
                NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(
                _farUnloadResultMask,
                nameof(WorldSpatialHashGrid),
                nameof(_farUnloadResultMask),
                NativeMemoryLifetime);
        }

        private static void DisposeFarUnloadBuffers()
        {
            if (_farUnloadScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _farUnloadHandle, forceComplete: true);
                _farUnloadScheduled = false;
            }

            if (_farUnloadHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadHandles);
                _farUnloadHandles.Dispose();
                _farUnloadHandles = default;
            }

            if (_farUnloadAbsolutePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadAbsolutePositions);
                _farUnloadAbsolutePositions.Dispose();
                _farUnloadAbsolutePositions = default;
            }

            if (_farUnloadEligibilityMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadEligibilityMask);
                _farUnloadEligibilityMask.Dispose();
                _farUnloadEligibilityMask = default;
            }

            if (_farUnloadResultMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadResultMask);
                _farUnloadResultMask.Dispose();
                _farUnloadResultMask = default;
            }

            _farUnloadCount = 0;
        }

        private static JobHandle CancelFarUnloadForTeardown()
        {
            if (!_farUnloadScheduled)
                return _farUnloadHandle;

            JobHandle dependency = _farUnloadHandle;
            _farUnloadHandle = default;
            _farUnloadScheduled = false;
            _farUnloadCount = 0;
            return dependency;
        }

        private static JobHandle DisposeFarUnloadBuffers(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;

            if (_farUnloadHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadHandles);
                disposeHandle = _farUnloadHandles.Dispose(disposeHandle);
                _farUnloadHandles = default;
            }

            if (_farUnloadAbsolutePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadAbsolutePositions);
                disposeHandle = _farUnloadAbsolutePositions.Dispose(disposeHandle);
                _farUnloadAbsolutePositions = default;
            }

            if (_farUnloadEligibilityMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadEligibilityMask);
                disposeHandle = _farUnloadEligibilityMask.Dispose(disposeHandle);
                _farUnloadEligibilityMask = default;
            }

            if (_farUnloadResultMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_farUnloadResultMask);
                disposeHandle = _farUnloadResultMask.Dispose(disposeHandle);
                _farUnloadResultMask = default;
            }

            _farUnloadCount = 0;
            return disposeHandle;
        }

        private static void EnsureAcousticDensityMap()
        {
            if (_acousticDensityMap.IsCreated && _acousticDensityMap.Length == AcousticDensityMapCellCount)
                return;

            DisposeAcousticDensityMap();
            // COLD ALLOC: NativeArray<float>[512] - 8x8x8 transient acoustic density payload - owner: WorldSpatialHashGrid
            _acousticDensityMap = new NativeArray<float>(AcousticDensityMapCellCount, DataVaultExemptWorldSpatialAcousticAllocator, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _acousticDensityMap,
                nameof(WorldSpatialHashGrid),
                nameof(_acousticDensityMap),
                NativeMemoryLifetime);
        }

        private static void DisposeAcousticDensityMap()
        {
            if (_acousticDensityMap.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticDensityMap);
                _acousticDensityMap.Dispose();
                _acousticDensityMap = default;
            }
        }

        private static void BuildAcousticDensityMap(int currentFrame)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement == null)
                return;

            AbsoluteUniversePosition listenerAup = playerMovement.CurrentAup;
            if (!IsFiniteAup(in listenerAup))
                return;

            float3 listenerRuntime = listenerAup.ToRuntimeFloat3();
            Vector3 listenerPosition = new Vector3(listenerRuntime.x, listenerRuntime.y, listenerRuntime.z);
            if (!IsFiniteRuntimePosition(listenerPosition))
                return;

            EnsureAcousticDensityMap();
            using (_acousticDensityProfilerMarker.Auto())
            {
                double currentTimestamp = Time.unscaledTimeAsDouble;
                if (!IsFiniteDouble(currentTimestamp))
                    return;

                _nativeHash.BuildAcousticDensityMap(
                    in listenerAup,
                    AcousticDensityMapRadiusMeters,
                    currentTimestamp,
                    _acousticDensityMap,
                    new int3(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis),
                    (uint)SpatialTransientEventType.AcousticImpulse);
                _lastAcousticDensityFrame = currentFrame;
            }
        }

        private static void TrackTransientSignal(
            Vector3 runtimePosition,
            in AbsoluteUniversePosition positionAup,
            double expirationTimestamp,
            FieldTargetRole signalRole,
            int sourceSpeciesId)
        {
            _transientSignals[_transientSignalWriteIndex] = new TransientSignalEntry
            {
                RuntimePosition = runtimePosition,
                AbsolutePosition = positionAup,
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
            if (entry.IsResidentInNativeHash == 0)
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

        private static float ApproximateAupDistanceMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double approximateDistance = AbsoluteUniversePosition.ApproximateDistanceMetersClamped(in a, in b);
            return approximateDistance >= float.MaxValue ? float.MaxValue : (float)approximateDistance;
        }

        private static int ClampDistanceToHud(float distanceMeters)
        {
            float clampedDistance = math.clamp(distanceMeters, 0f, Hecton8.UI.HudNumericStringCache.MaxIntegerValue);
            return (int)(clampedDistance + 0.5f);
        }

        private static float ClampDistanceSqrToFloat(double distanceSqr)
        {
            return distanceSqr >= float.MaxValue ? float.MaxValue : (float)distanceSqr;
        }
    }
}
