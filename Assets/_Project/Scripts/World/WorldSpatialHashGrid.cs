using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Scavenging;
using Unity.Collections;
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
        private static double RuntimeNowSeconds()
        {
            return SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

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

        private const double CellSizeMeters = 20d;
        private const int DefaultEntryCapacity = 256;
        private const int MaxSpatialMaintenanceEntryCapacity = 8192;
        private const int MaxQueryHandleCapacity = 256;
        private const int ValidationCadenceFrames = 300;
        private const float FarUnloadPlayerTravelThresholdMeters = 2000f;
        private const double FarUnloadPlayerTravelThresholdSq = FarUnloadPlayerTravelThresholdMeters * FarUnloadPlayerTravelThresholdMeters;
        private const float FarUnloadDistanceMeters = 2500f;
        private const double FarUnloadDistanceSq = FarUnloadDistanceMeters * FarUnloadDistanceMeters;
        private const float MaxTransientEventRadiusMeters = FarUnloadDistanceMeters;
        private const int AcousticDensityMapAxis = 8;
        private const int AcousticDensityMapCellCount = AcousticDensityMapAxis * AcousticDensityMapAxis * AcousticDensityMapAxis;
        private const int AcousticDensityMapCadenceFrames = 10;
        private const float AcousticDensityMapRadiusMeters = 160f;
        private const BufferID AcousticDensityMapBufferId = BufferID.WorldSpatialAcousticDensityMap;
        private const float AcousticTransientDecayScale = 0.85f;
        private const float AcousticTransientMinimumIntensity = 0.01f;
        private const int SpatialHashCompactionCapacityThreshold = 50000;
        private const int SpatialHashCompactionTargetFloor = DefaultEntryCapacity * 4;
        private const int MaxTransientSignalCount = 16;
        private const int SpatialMetadataBucketCapacity = MaxSpatialMaintenanceEntryCapacity * 2;
        private const int SpatialMetadataBucketMask = SpatialMetadataBucketCapacity - 1;
        private const int EmptyBucket = 0;
        private const int TombstoneBucket = -1;
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Query");
        private static readonly ProfilerMarker _maintenanceProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Maintenance");
        private static readonly ProfilerMarker _validationProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.Validation");
        private static readonly ProfilerMarker _farUnloadProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.FarUnload");
        private static readonly ProfilerMarker _acousticDensityProfilerMarker = new ProfilerMarker("H8.World.SpatialHashFacade.AcousticDensity");

        // COLD ALLOC: int[16384] - open-address handle-to-slot buckets for runtime metadata - owner: WorldSpatialHashGrid
        private static readonly int[] _entryBuckets = new int[SpatialMetadataBucketCapacity];
        // COLD ALLOC: int[8192] - dense spatial metadata handle keys - owner: WorldSpatialHashGrid
        private static readonly int[] _entryHandles = new int[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: Entry[8192] - dense runtime metadata registry layered over the native AUP spatial hash - owner: WorldSpatialHashGrid
        private static readonly Entry[] _entryValues = new Entry[MaxSpatialMaintenanceEntryCapacity];
        private static int _entryCount;
        // COLD ALLOC: int[16384] - open-address EntityId-to-handle buckets - owner: WorldSpatialHashGrid
        private static readonly int[] _transformHandleBuckets = new int[SpatialMetadataBucketCapacity];
        // COLD ALLOC: ulong[8192] - dense EntityId reverse-lookup keys - owner: WorldSpatialHashGrid
        private static readonly ulong[] _transformHandleKeys = new ulong[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: int[8192] - dense EntityId reverse-lookup handles - owner: WorldSpatialHashGrid
        private static readonly int[] _transformHandleValues = new int[MaxSpatialMaintenanceEntryCapacity];
        private static int _transformHandleCount;
        // COLD ALLOC: int[8192] - deferred far-unload handle scratch for dynamic native-hash eviction - owner: WorldSpatialHashGrid
        private static readonly int[] _farUnloadHandleScratch = new int[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: int[8192] - main-thread origin-shift key scratch, not a job payload - owner: WorldSpatialHashGrid
        private static readonly int[] _originShiftHandles = new int[MaxSpatialMaintenanceEntryCapacity];

        private static readonly TransientSignalEntry[] _transientSignals = new TransientSignalEntry[MaxTransientSignalCount]; // COLD ALLOC: TransientSignalEntry[16] - transient PDA sonar signal ring - owner: WorldSpatialHashGrid

        private static HectonSpatialHash _nativeHash;
        // COLD ALLOC: float[512] - acoustic density map staging scratch; heavy density build and texture upload run outside DataVault locks.
        private static readonly float[] _acousticDensityMapScratch = new float[AcousticDensityMapCellCount];
        // COLD ALLOC: int[2048] - facade query result scratch copied from HectonSpatialHash internal native scratch.
        private static readonly int[] _queryHandles = new int[MaxQueryHandleCapacity];
        // COLD ALLOC: double3[8192] - synchronous AUP validation scratch - owner: WorldSpatialHashGrid
        private static readonly double3[] _validationAbsolutePositions = new double3[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: float3[8192] - synchronous runtime-position validation scratch - owner: WorldSpatialHashGrid
        private static readonly float3[] _validationRuntimePositions = new float3[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: byte[8192] - synchronous validation failure mask - owner: WorldSpatialHashGrid
        private static readonly byte[] _validationInvalidMask = new byte[MaxSpatialMaintenanceEntryCapacity];
        private static int _validationCount;
        // COLD ALLOC: int[8192] - synchronous far-unload candidate handles - owner: WorldSpatialHashGrid
        private static readonly int[] _farUnloadHandles = new int[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: double3[8192] - synchronous far-unload absolute positions - owner: WorldSpatialHashGrid
        private static readonly double3[] _farUnloadAbsolutePositions = new double3[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: byte[8192] - synchronous far-unload eligibility mask - owner: WorldSpatialHashGrid
        private static readonly byte[] _farUnloadEligibilityMask = new byte[MaxSpatialMaintenanceEntryCapacity];
        // COLD ALLOC: byte[8192] - synchronous far-unload result mask - owner: WorldSpatialHashGrid
        private static readonly byte[] _farUnloadResultMask = new byte[MaxSpatialMaintenanceEntryCapacity];
        private static int _farUnloadCount;
        private static int _farUnloadHandleScratchCount;
        private static IDataVault _acousticDensityVault;
        private static VaultGenerationHandle<float> _acousticDensityMapHandle;
        private static bool _hasAcousticDensityMap;
        private static int _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
        private static int _transientSignalWriteIndex;
        private static AbsoluteUniversePosition _lastFarUnloadPlayerAup;
        private static bool _hasLastFarUnloadPlayerAup;
        private static int _lastValidationFrame = -ValidationCadenceFrames;
        private static bool _lastResultBufferSaturated;
        private static bool _lastStaleHandleObserved;

        internal static int ActiveEntityCount => _nativeHash != null ? _nativeHash.EntryCount : _entryCount;
        internal static HectonSpatialHash.QueryStats LastNativeQueryStats => _nativeHash != null ? _nativeHash.LastQueryStats : default;
        internal static bool LastNativeQuerySaturated => _nativeHash != null && _nativeHash.LastQueryStats.IsSaturated;
        internal static bool LastResultBufferSaturated => _lastResultBufferSaturated;
        internal static bool LastStaleHandleObserved => _lastStaleHandleObserved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearRuntimeState();
        }

        internal static void ClearRuntimeState()
        {
            ClearEntryMap();
            ClearTransformHandleMap();
            CancelValidationForTeardown();
            CancelFarUnloadForTeardown();
            DisposeAcousticDensityMap();
            _farUnloadHandleScratchCount = 0;
            _nativeHash?.Dispose();
            _nativeHash = null;
            _validationCount = 0;
            _farUnloadCount = 0;
            _farUnloadHandleScratchCount = 0;
            _hasLastFarUnloadPlayerAup = false;
            _lastValidationFrame = -ValidationCadenceFrames;
            _lastAcousticDensityFrame = -AcousticDensityMapCadenceFrames;
            _transientSignalWriteIndex = 0;
            _lastResultBufferSaturated = false;
            _lastStaleHandleObserved = false;
            for (int i = 0; i < _transientSignals.Length; i++)
                _transientSignals[i] = default;
        }

        private static void ClearEntryMap()
        {
            for (int i = 0; i < _entryBuckets.Length; i++)
                _entryBuckets[i] = EmptyBucket;

            for (int i = 0; i < _entryCount; i++)
            {
                _entryHandles[i] = 0;
                _entryValues[i] = default;
            }

            _entryCount = 0;
        }

        private static bool TryGetEntry(int handle, out Entry entry)
        {
            int bucket = FindEntryBucket(handle);
            if (bucket < 0)
            {
                entry = default;
                return false;
            }

            entry = _entryValues[_entryBuckets[bucket] - 1];
            return true;
        }

        private static bool SetEntry(int handle, in Entry entry)
        {
            if (handle <= 0)
                return false;

            int bucket = FindEntryBucket(handle);
            if (bucket >= 0)
            {
                _entryValues[_entryBuckets[bucket] - 1] = entry;
                return true;
            }

            if (_entryCount >= MaxSpatialMaintenanceEntryCapacity)
                return false;

            bucket = FindEntryInsertBucket(handle);
            if (bucket < 0)
            {
                RebuildEntryBuckets();
                bucket = FindEntryInsertBucket(handle);
                if (bucket < 0)
                    return false;
            }

            int slot = _entryCount;
            _entryCount++;
            _entryHandles[slot] = handle;
            _entryValues[slot] = entry;
            _entryBuckets[bucket] = slot + 1;
            return true;
        }

        private static bool RemoveEntry(int handle)
        {
            int bucket = FindEntryBucket(handle);
            if (bucket < 0)
                return false;

            int slot = _entryBuckets[bucket] - 1;
            int lastSlot = _entryCount - 1;
            _entryBuckets[bucket] = TombstoneBucket;

            if (slot != lastSlot)
            {
                int movedHandle = _entryHandles[lastSlot];
                _entryHandles[slot] = movedHandle;
                _entryValues[slot] = _entryValues[lastSlot];

                int movedBucket = FindEntryBucket(movedHandle);
                if (movedBucket >= 0)
                    _entryBuckets[movedBucket] = slot + 1;
                else
                    RebuildEntryBuckets();
            }

            _entryHandles[lastSlot] = 0;
            _entryValues[lastSlot] = default;
            _entryCount--;

            if (_entryCount == 0)
                ClearEntryBucketsOnly();

            return true;
        }

        private static int FindEntryBucket(int handle)
        {
            if (handle <= 0)
                return -1;

            int bucket = HashInt(handle);
            for (int probe = 0; probe < SpatialMetadataBucketCapacity; probe++)
            {
                int slotPlusOne = _entryBuckets[bucket];
                if (slotPlusOne == EmptyBucket)
                    return -1;

                if (slotPlusOne > 0)
                {
                    int slot = slotPlusOne - 1;
                    if (_entryHandles[slot] == handle)
                        return bucket;
                }

                bucket = (bucket + 1) & SpatialMetadataBucketMask;
            }

            return -1;
        }

        private static int FindEntryInsertBucket(int handle)
        {
            int bucket = HashInt(handle);
            int firstTombstone = -1;
            for (int probe = 0; probe < SpatialMetadataBucketCapacity; probe++)
            {
                int slotPlusOne = _entryBuckets[bucket];
                if (slotPlusOne == EmptyBucket)
                    return firstTombstone >= 0 ? firstTombstone : bucket;

                if (slotPlusOne == TombstoneBucket)
                {
                    if (firstTombstone < 0)
                        firstTombstone = bucket;
                }
                else if (_entryHandles[slotPlusOne - 1] == handle)
                {
                    return bucket;
                }

                bucket = (bucket + 1) & SpatialMetadataBucketMask;
            }

            return firstTombstone;
        }

        private static void RebuildEntryBuckets()
        {
            ClearEntryBucketsOnly();
            for (int slot = 0; slot < _entryCount; slot++)
            {
                int bucket = FindEntryInsertBucket(_entryHandles[slot]);
                if (bucket >= 0)
                    _entryBuckets[bucket] = slot + 1;
            }
        }

        private static void ClearEntryBucketsOnly()
        {
            for (int i = 0; i < _entryBuckets.Length; i++)
                _entryBuckets[i] = EmptyBucket;
        }

        private static void ClearTransformHandleMap()
        {
            for (int i = 0; i < _transformHandleBuckets.Length; i++)
                _transformHandleBuckets[i] = EmptyBucket;

            for (int i = 0; i < _transformHandleCount; i++)
            {
                _transformHandleKeys[i] = 0UL;
                _transformHandleValues[i] = 0;
            }

            _transformHandleCount = 0;
        }

        private static bool TryGetTransformHandle(ulong transformId, out int handle)
        {
            int bucket = FindTransformHandleBucket(transformId);
            if (bucket < 0)
            {
                handle = 0;
                return false;
            }

            handle = _transformHandleValues[_transformHandleBuckets[bucket] - 1];
            return true;
        }

        private static bool SetTransformHandle(ulong transformId, int handle)
        {
            int bucket = FindTransformHandleBucket(transformId);
            if (bucket >= 0)
            {
                _transformHandleValues[_transformHandleBuckets[bucket] - 1] = handle;
                return true;
            }

            if (_transformHandleCount >= MaxSpatialMaintenanceEntryCapacity)
                return false;

            bucket = FindTransformHandleInsertBucket(transformId);
            if (bucket < 0)
            {
                RebuildTransformHandleBuckets();
                bucket = FindTransformHandleInsertBucket(transformId);
                if (bucket < 0)
                    return false;
            }

            int slot = _transformHandleCount;
            _transformHandleCount++;
            _transformHandleKeys[slot] = transformId;
            _transformHandleValues[slot] = handle;
            _transformHandleBuckets[bucket] = slot + 1;
            return true;
        }

        private static bool RemoveTransformHandleKey(ulong transformId)
        {
            int bucket = FindTransformHandleBucket(transformId);
            if (bucket < 0)
                return false;

            int slot = _transformHandleBuckets[bucket] - 1;
            int lastSlot = _transformHandleCount - 1;
            _transformHandleBuckets[bucket] = TombstoneBucket;

            if (slot != lastSlot)
            {
                ulong movedKey = _transformHandleKeys[lastSlot];
                _transformHandleKeys[slot] = movedKey;
                _transformHandleValues[slot] = _transformHandleValues[lastSlot];

                int movedBucket = FindTransformHandleBucket(movedKey);
                if (movedBucket >= 0)
                    _transformHandleBuckets[movedBucket] = slot + 1;
                else
                    RebuildTransformHandleBuckets();
            }

            _transformHandleKeys[lastSlot] = 0UL;
            _transformHandleValues[lastSlot] = 0;
            _transformHandleCount--;

            if (_transformHandleCount == 0)
                ClearTransformHandleBucketsOnly();

            return true;
        }

        private static int FindTransformHandleBucket(ulong transformId)
        {
            int bucket = HashUlong(transformId);
            for (int probe = 0; probe < SpatialMetadataBucketCapacity; probe++)
            {
                int slotPlusOne = _transformHandleBuckets[bucket];
                if (slotPlusOne == EmptyBucket)
                    return -1;

                if (slotPlusOne > 0)
                {
                    int slot = slotPlusOne - 1;
                    if (_transformHandleKeys[slot] == transformId)
                        return bucket;
                }

                bucket = (bucket + 1) & SpatialMetadataBucketMask;
            }

            return -1;
        }

        private static int FindTransformHandleInsertBucket(ulong transformId)
        {
            int bucket = HashUlong(transformId);
            int firstTombstone = -1;
            for (int probe = 0; probe < SpatialMetadataBucketCapacity; probe++)
            {
                int slotPlusOne = _transformHandleBuckets[bucket];
                if (slotPlusOne == EmptyBucket)
                    return firstTombstone >= 0 ? firstTombstone : bucket;

                if (slotPlusOne == TombstoneBucket)
                {
                    if (firstTombstone < 0)
                        firstTombstone = bucket;
                }
                else if (_transformHandleKeys[slotPlusOne - 1] == transformId)
                {
                    return bucket;
                }

                bucket = (bucket + 1) & SpatialMetadataBucketMask;
            }

            return firstTombstone;
        }

        private static void RebuildTransformHandleBuckets()
        {
            ClearTransformHandleBucketsOnly();
            for (int slot = 0; slot < _transformHandleCount; slot++)
            {
                int bucket = FindTransformHandleInsertBucket(_transformHandleKeys[slot]);
                if (bucket >= 0)
                    _transformHandleBuckets[bucket] = slot + 1;
            }
        }

        private static void ClearTransformHandleBucketsOnly()
        {
            for (int i = 0; i < _transformHandleBuckets.Length; i++)
                _transformHandleBuckets[i] = EmptyBucket;
        }

        private static int HashInt(int value)
        {
            unchecked
            {
                uint hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352dU;
                hash ^= hash >> 15;
                hash *= 0x846ca68bU;
                hash ^= hash >> 16;
                return (int)(hash & SpatialMetadataBucketMask);
            }
        }

        private static int HashUlong(ulong value)
        {
            unchecked
            {
                ulong hash = value;
                hash ^= hash >> 33;
                hash *= 0xff51afd7ed558ccdUL;
                hash ^= hash >> 33;
                hash *= 0xc4ceb9fe1a85ec53UL;
                hash ^= hash >> 33;
                return (int)((uint)hash & SpatialMetadataBucketMask);
            }
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
            if (handle <= 0 || !TryGetEntry(handle, out Entry entry))
                return;

            entry.SignalRole = signalRole;
            SetEntry(handle, in entry);
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
            if (handle <= 0 || !TryGetEntry(handle, out Entry entry))
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
            if (handle <= 0 || !TryGetEntry(handle, out Entry entry))
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
            if (handle <= 0 || !TryGetEntry(handle, out Entry entry) || !IsFiniteAup(in entry.AbsolutePosition))
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
            if (handle <= 0 || !TryGetEntry(handle, out Entry entry))
                return;

            entry.HalfExtents = math.max(halfExtents, 0f);
            SetEntry(handle, in entry);
            UpdateNativeEntry(handle, entry);
        }

        public static void Unregister(int handle)
        {
            if (handle <= 0)
                return;

            if (!TryGetEntry(handle, out Entry entry))
                return;

            RemoveTransformHandle(handle, entry.Transform);
            if (_nativeHash != null && entry.IsResidentInNativeHash != 0)
                _nativeHash.Unregister(handle);
            else if (_nativeHash != null)
                _nativeHash.ReleaseHandle(handle);

            RemoveEntry(handle);
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
                if (!TryGetEntry(handle, out Entry entry))
                {
                    MarkStaleHandleObserved();
                    continue;
                }

                if (!IsEntryQueryEligible(entry))
                {
                    MarkStaleHandleObserved();
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
                if (!TryGetEntry(handle, out Entry entry))
                {
                    MarkStaleHandleObserved();
                    continue;
                }

                if (!IsEntryQueryEligible(entry))
                {
                    MarkStaleHandleObserved();
                    continue;
                }

                Transform candidateTransform = entry.Transform;
                if (candidateTransform == ignoreTransform)
                    continue;

                if (!MatchesLayer(entry.Layer, layerMask))
                    continue;

                if (!(entry.Owner is IFaunaSpatialContact faunaContact) || !faunaContact.IsAggressiveContact)
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
                if (!TryGetEntry(handle, out Entry entry))
                {
                    MarkStaleHandleObserved();
                    continue;
                }

                if (!IsEntryQueryEligible(entry))
                {
                    MarkStaleHandleObserved();
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

            double currentTimestamp = RuntimeNowSeconds();
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
                if (!TryGetEntry(handle, out Entry entry))
                {
                    MarkStaleHandleObserved();
                    continue;
                }

                if (!IsEntryQueryEligible(entry))
                {
                    MarkStaleHandleObserved();
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

            float safeRadiusMeters = NormalizeTransientEventRadius(radiusMeters);
            if (safeRadiusMeters <= 0f)
                return;

            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return;

            RegisterTransientEvent(
                worldPosition,
                in positionAup,
                safeRadiusMeters,
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

            float safeRadiusMeters = NormalizeTransientEventRadius(radiusMeters);
            if (safeRadiusMeters <= 0f)
                return;

            EnsureInitialized();
            double currentTimestamp = RuntimeNowSeconds();
            if (!IsFiniteDouble(currentTimestamp))
                return;

            double expirationTimestamp = currentTimestamp + lifetimeSeconds;
            uint sourceKey = ComposeTransientSignalSourceKey(signalRole, sourceSpeciesId);
            _nativeHash.RegisterTransientEvent(
                in positionAup,
                safeRadiusMeters,
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

        private static float NormalizeTransientEventRadius(float radiusMeters)
        {
            if (!math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0f;

            return math.min(radiusMeters, MaxTransientEventRadiusMeters);
        }

        /// <summary>
        /// Clears one transient signal source immediately, used by mimic fauna once the false beacon has served its ambush role.
        /// </summary>
        public static void ClearTransientSignal(FieldTargetRole signalRole, int sourceSpeciesId)
        {
            uint sourceKey = ComposeTransientSignalSourceKey(signalRole, sourceSpeciesId);
            double currentTimestamp = RuntimeNowSeconds();
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
            out NativeArray<float>.ReadOnly densityMap,
            out Vector3Int dimensions)
        {
            densityMap = default;
            dimensions = new Vector3Int(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis);
            if (!_hasAcousticDensityMap ||
                !TryResolveAcousticDensityMapReadOnly(out densityMap))
                return false;

            return true;
        }

        public static bool TryUploadAcousticDensityMap(
            Texture2D destination,
            int requestedSampleCount,
            out int uploadedSampleCount,
            out float peakIntensity)
        {
            uploadedSampleCount = 0;
            peakIntensity = 0f;

            if (destination == null ||
                !_hasAcousticDensityMap ||
                requestedSampleCount <= 0)
            {
                return false;
            }

            if (!TryResolveAcousticDensityVault(out IDataVault vault) ||
                !IsAcousticDensityMapHandle(in _acousticDensityMapHandle))
                return false;

            if (!TrySnapshotAcousticDensityMapForUpload(
                    vault,
                    requestedSampleCount,
                    out uploadedSampleCount,
                    out peakIntensity))
            {
                uploadedSampleCount = 0;
                peakIntensity = 0f;
                return false;
            }

            destination.SetPixelData(_acousticDensityMapScratch, 0);
            return true;
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
                RuntimeNowSeconds(),
                out temperatureDeltaCelsius,
                out double3 gradientAup);
            float3 gradientLocal = AupPrecisionMath.DowncastLocalDelta(gradientAup, float3.zero);
            gradient = new Vector3(gradientLocal.x, gradientLocal.y, gradientLocal.z);
            return hasGradient;
        }

        internal static void SlowTickMaintenance(float deltaTime)
        {
            // L19 hop2 LIVE: batch peel SlowTickMaintenance - DecayTransientEvents native path
            // can hang headless after STARTERGRANT (post ISlowTickable loop).
            if (UnityEngine.Application.isBatchMode)
                return;

            if (_nativeHash == null)
                return;

            _nativeHash.DecayTransientEvents(
                RuntimeNowSeconds(),
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
                if (frameCount - _lastValidationFrame >= ValidationCadenceFrames)
                    ScheduleValidation(frameCount);

                TryScheduleFarUnload();

                if (frameCount - _lastAcousticDensityFrame >= AcousticDensityMapCadenceFrames)
                {
                    _nativeHash.PruneExpiredTransientEvents(RuntimeNowSeconds());
                    BuildAcousticDensityMap(frameCount);
                }

                _nativeHash.TrySwapCompletedCompaction();
                _nativeHash.ScheduleCompactionIfOverCapacity(
                    SpatialHashCompactionCapacityThreshold,
                    SpatialHashCompactionTargetFloor,
                    RuntimeNowSeconds());
            }
        }

        internal static void HandleOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude))
            {
                ClearAcousticDensityMapForOriginShift();
                return;
            }

            if (shiftSqrMagnitude <= 0.000001f)
                return;

            Vector3 runtimeOffset = -shiftOffset;
            if (_nativeHash == null)
            {
                ClearAcousticDensityMapForOriginShift();
                RebaseTransientSignalRuntimePositions(runtimeOffset);
                return;
            }

            EnsureInitialized();
            ClearAcousticDensityMapForOriginShift();

            int count = _entryCount;
            RebaseTransientSignalRuntimePositions(runtimeOffset);
            if (count <= 0)
                return;

            int writeIndex = 0;
            for (int i = 0; i < _entryCount; i++)
            {
                Entry entry = _entryValues[i];
                if (writeIndex >= _originShiftHandles.Length)
                    break;
                if (entry.Transform == null || !IsFiniteRuntimePosition(entry.RuntimePosition))
                    continue;

                _originShiftHandles[writeIndex] = _entryHandles[i];
                writeIndex++;
            }

            if (writeIndex <= 0)
                return;

            for (int i = 0; i < writeIndex; i++)
            {
                int handle = _originShiftHandles[i];
                if (!TryGetEntry(handle, out Entry entry))
                    continue;

                Vector3 shiftedRuntimePosition = entry.RuntimePosition + runtimeOffset;
                if (!IsFiniteRuntimePosition(shiftedRuntimePosition))
                    continue;

                entry.RuntimePosition = shiftedRuntimePosition;
                SetEntry(handle, in entry);
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
            if (_entryCount >= MaxSpatialMaintenanceEntryCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[WorldSpatialHashGrid] Entry capacity exceeded. Runtime buffer growth is forbidden.");
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

            Entry entry = new Entry
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
            if (!SetEntry(handle, in entry) ||
                !SetTransformHandle(ResolveTransformEntityKey(targetTransform), handle))
            {
                RemoveEntry(handle);
                _nativeHash.Unregister(handle);
                return 0;
            }

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
            SetEntry(handle, in entry);
        }

        private static int FindHandle(Transform targetTransform)
        {
            if (targetTransform == null)
                return 0;

            ulong transformId = ResolveTransformEntityKey(targetTransform);
            if (!TryGetTransformHandle(transformId, out int handle))
                return 0;

            if (!TryGetEntry(handle, out Entry entry) || !ReferenceEquals(entry.Transform, targetTransform))
            {
                RemoveTransformHandleKey(transformId);
                return 0;
            }

            return handle;
        }

        private static void RemoveTransformHandle(int handle, Transform targetTransform)
        {
            if (targetTransform == null)
                return;

            ulong transformId = ResolveTransformEntityKey(targetTransform);
            if (TryGetTransformHandle(transformId, out int mappedHandle) && mappedHandle == handle)
                RemoveTransformHandleKey(transformId);
        }

        private static ulong ResolveTransformEntityKey(Transform targetTransform)
        {
            return targetTransform != null
                ? EntityId.ToULong(targetTransform.GetEntityId())
                : 0UL;
        }

        private static int CollectCandidateHandles(Vector3 origin, float radius, SpatialTargetKind kindMask, ulong interactionFilter = 0UL)
        {
            ResetQueryTelemetry();
            if (!IsFiniteRuntimePosition(origin) || !math.isfinite(radius) || radius <= 0f || kindMask == SpatialTargetKind.None)
                return 0;

            if (_nativeHash == null || _entryCount == 0)
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
            _lastStaleHandleObserved = false;
            if (_nativeHash != null)
                _nativeHash.ClearLastQueryStats();
        }

        private static void MarkStaleHandleObserved()
        {
            _lastStaleHandleObserved = true;
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

            return !(entry.Owner is IFaunaSpatialContact faunaContact) || !faunaContact.IsDead;
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static void ClearAcousticDensityMapForOriginShift()
        {
            if (_hasAcousticDensityMap)
            {
                IDataVault vault = null;
                bool locked = false;
                try
                {
                    if (!TryResolveAcousticDensityVault(out vault) ||
                        !IsAcousticDensityMapHandle(in _acousticDensityMapHandle))
                        return;

                    if (!vault.TryAcquireWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash, out NativeArray<float> densityMap))
                        return;

                    locked = true;
                    if (!densityMap.IsCreated)
                        return;

                    int cellCount = math.min(densityMap.Length, AcousticDensityMapCellCount);
                    for (int i = 0; i < cellCount; i++)
                        densityMap[i] = 0f;
                }
                finally
                {
                    if (locked)
                        vault.ReleaseWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash);
                }
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
            int count = _entryCount;
            if (count <= 0)
            {
                _lastValidationFrame = currentFrame;
                return;
            }

            int writeIndex = 0;
            for (int i = 0; i < _entryCount; i++)
            {
                Entry entry = _entryValues[i];
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
                double3 committedTotalOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                for (int i = 0; i < writeIndex; i++)
                {
                    float3 runtime = _validationRuntimePositions[i];
                    double3 reconstructedAbsolute = new double3(runtime.x, runtime.y, runtime.z) + committedTotalOffset;
                    double3 delta = reconstructedAbsolute - _validationAbsolutePositions[i];
                    _validationInvalidMask[i] = math.lengthsq(delta) <= 0.01d ? (byte)0 : (byte)1;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                for (int i = 0; i < _validationCount; i++)
                {
                    if (_validationInvalidMask[i] == 0)
                        continue;

                    Hecton8.Core.H8Debug.LogError("[WorldSpatialHashGrid] AUP integrity validation failed. Runtime/AUP spatial coherence diverged.");
                    break;
                }
#endif

                _validationCount = 0;
                _lastValidationFrame = currentFrame;
            }
        }

        private static void TryScheduleFarUnload()
        {
            if (!TryResolveActivePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            if (!IsFiniteAup(in playerAup))
                return;

            if (_hasLastFarUnloadPlayerAup &&
                AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastFarUnloadPlayerAup) < FarUnloadPlayerTravelThresholdSq)
            {
                return;
            }

            EnsureInitialized();
            int count = _entryCount;
            if (count <= 0)
            {
                _lastFarUnloadPlayerAup = playerAup;
                _hasLastFarUnloadPlayerAup = true;
                return;
            }

            int writeIndex = 0;
            for (int i = 0; i < _entryCount; i++)
            {
                Entry entry = _entryValues[i];
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
                _farUnloadHandles[writeIndex] = _entryHandles[i];
                _farUnloadAbsolutePositions[writeIndex] = entryAup.ToAbsoluteDouble3();
                _farUnloadEligibilityMask[writeIndex] = IsFarUnloadEligible(entry) ? (byte)1 : (byte)0;
                writeIndex++;
            }

            double3 currentTotalOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            for (int i = 0; i < writeIndex; i++)
            {
                int handle = _farUnloadHandles[i];
                if (!TryGetEntry(handle, out Entry entry))
                    continue;

                AbsoluteUniversePosition refreshedAup = AbsoluteUniversePosition.FromAbsolutePosition(_farUnloadAbsolutePositions[i]);
                float3 refreshedRuntime = AUPMath.ToRuntimeFloat3(in refreshedAup, currentTotalOffset);
                entry.RuntimePosition = new Vector3(refreshedRuntime.x, refreshedRuntime.y, refreshedRuntime.z);
                entry.AbsolutePosition = refreshedAup;
                SetEntry(handle, in entry);
            }

            _lastFarUnloadPlayerAup = playerAup;
            _hasLastFarUnloadPlayerAup = true;
            if (writeIndex <= 0)
                return;

            using (_farUnloadProfilerMarker.Auto())
            {
                _farUnloadCount = writeIndex;
                double3 playerAbsolutePosition = playerAup.ToAbsoluteDouble3();
                for (int i = 0; i < writeIndex; i++)
                {
                    if (_farUnloadEligibilityMask[i] == 0)
                    {
                        _farUnloadResultMask[i] = 0;
                        continue;
                    }

                    double3 delta = _farUnloadAbsolutePositions[i] - playerAbsolutePosition;
                    _farUnloadResultMask[i] = math.lengthsq(delta) > FarUnloadDistanceSq ? (byte)1 : (byte)0;
                }

                ConsumeCompletedFarUnload();
            }
        }

        private static void ConsumeCompletedFarUnload()
        {
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
                if (!TryGetEntry(handle, out Entry entry) || entry.IsResidentInNativeHash == 0)
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
                SetEntry(handle, in entry);
            }

            _farUnloadCount = 0;
            _farUnloadHandleScratchCount = 0;
        }

        private static void CancelValidationForTeardown()
        {
            _validationCount = 0;
        }

        private static void CancelFarUnloadForTeardown()
        {
            _farUnloadCount = 0;
            _farUnloadHandleScratchCount = 0;
        }

        private static bool EnsureAcousticDensityMap()
        {
            if (!TryResolveAcousticDensityVault(out IDataVault vault) ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (IsAcousticDensityMapHandle(in _acousticDensityMapHandle) &&
                vault.TryResolveHandle(in _acousticDensityMapHandle, out NativeArray<float> existing) &&
                existing.IsCreated &&
                existing.Length >= AcousticDensityMapCellCount)
            {
                return true;
            }

            _acousticDensityMapHandle = vault.EnsureGenerationHandle<float>(
                AcousticDensityMapBufferId,
                AcousticDensityMapCellCount,
                SystemID.WorldSpatialHash,
                NativeArrayOptions.ClearMemory);

            if (!IsAcousticDensityMapHandle(in _acousticDensityMapHandle) ||
                !vault.TryResolveHandle(in _acousticDensityMapHandle, out NativeArray<float> densityMap) ||
                !densityMap.IsCreated ||
                densityMap.Length < AcousticDensityMapCellCount)
            {
                _acousticDensityMapHandle = default;
                _hasAcousticDensityMap = false;
                return false;
            }

            return true;
        }

        private static bool TrySnapshotAcousticDensityMapForUpload(
            IDataVault vault,
            int requestedSampleCount,
            out int uploadedSampleCount,
            out float peakIntensity)
        {
            uploadedSampleCount = 0;
            peakIntensity = 0f;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _acousticDensityMapScratch.Length < AcousticDensityMapCellCount)
            {
                return false;
            }

            bool locked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash, out NativeArray<float> densityMap))
                    return false;

                locked = true;
                if (!densityMap.IsCreated ||
                    densityMap.Length < AcousticDensityMapCellCount)
                {
                    return false;
                }

                int sampleCount = math.min(densityMap.Length, requestedSampleCount);
                if (sampleCount <= 0 || sampleCount != AcousticDensityMapCellCount)
                    return false;

                float peak = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = densityMap[i];
                    _acousticDensityMapScratch[i] = sample;
                    if (sample > peak)
                        peak = sample;
                }

                uploadedSampleCount = sampleCount;
                peakIntensity = math.saturate(peak);
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash);
            }
        }

        private static bool TryPublishAcousticDensityScratchToVault(IDataVault vault)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _acousticDensityMapScratch.Length < AcousticDensityMapCellCount)
            {
                return false;
            }

            bool locked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash, out NativeArray<float> densityMap))
                    return false;

                locked = true;
                if (!densityMap.IsCreated ||
                    densityMap.Length < AcousticDensityMapCellCount)
                {
                    return false;
                }

                for (int i = 0; i < AcousticDensityMapCellCount; i++)
                    densityMap[i] = _acousticDensityMapScratch[i];

                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _acousticDensityMapHandle, SystemID.WorldSpatialHash);
            }
        }

        private static void DisposeAcousticDensityMap()
        {
            ClearAcousticDensityMapForOriginShift();
            _hasAcousticDensityMap = false;
            _acousticDensityMapHandle = default;
            _acousticDensityVault = null;
        }

        private static void BuildAcousticDensityMap(int currentFrame)
        {
            if (!TryResolveActivePlayerAup(out AbsoluteUniversePosition listenerAup))
                return;

            if (!IsFiniteAup(in listenerAup))
                return;

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 listenerRuntime = AUPMath.ResolveCameraRelative(in listenerAup, in runtimeOriginAup);
            Vector3 listenerPosition = new Vector3(listenerRuntime.x, listenerRuntime.y, listenerRuntime.z);
            if (!IsFiniteRuntimePosition(listenerPosition))
                return;

            if (!EnsureAcousticDensityMap())
                return;

            using (_acousticDensityProfilerMarker.Auto())
            {
                double currentTimestamp = RuntimeNowSeconds();
                if (!IsFiniteDouble(currentTimestamp))
                    return;

                IDataVault vault = _acousticDensityVault;
                if (vault == null)
                    return;

                _nativeHash.BuildAcousticDensityMap(
                    in listenerAup,
                    AcousticDensityMapRadiusMeters,
                    currentTimestamp,
                    _acousticDensityMapScratch,
                    new int3(AcousticDensityMapAxis, AcousticDensityMapAxis, AcousticDensityMapAxis),
                    (uint)SpatialTransientEventType.AcousticImpulse);

                if (!TryPublishAcousticDensityScratchToVault(vault))
                    return;

                _hasAcousticDensityMap = true;
                _lastAcousticDensityFrame = currentFrame;
            }
        }

        private static bool TryResolveAcousticDensityVault(out IDataVault vault)
        {
            vault = _acousticDensityVault;
            if (vault != null)
                return true;

            vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _acousticDensityVault = vault;
            return true;
        }

        private static bool TryResolveAcousticDensityMapReadOnly(out NativeArray<float>.ReadOnly densityMap)
        {
            densityMap = default;
            if (!TryResolveAcousticDensityVault(out IDataVault vault) ||
                !IsAcousticDensityMapHandle(in _acousticDensityMapHandle) ||
                !vault.TryReadOnlyHandle(in _acousticDensityMapHandle, out densityMap) ||
                !densityMap.IsCreated ||
                densityMap.Length < AcousticDensityMapCellCount)
            {
                densityMap = default;
                return false;
            }

            return true;
        }

        private static bool IsAcousticDensityMapHandle(in VaultGenerationHandle<float> handle)
        {
            return handle.BufferID == (uint)AcousticDensityMapBufferId &&
                   handle.SystemID == (uint)SystemID.WorldSpatialHash &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveActivePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
            {
                playerAup = default;
                return false;
            }

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                AbsoluteUniversePosition snapshotAup = snapshot.Aup;
                if (snapshotAup.IsFinite())
                {
                    playerAup = snapshotAup;
                    return true;
                }
            }

            if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                AbsoluteUniversePosition predictedAup = movementState.PredictedAup;
                if (predictedAup.IsFinite())
                {
                    playerAup = predictedAup;
                    return true;
                }
            }

            playerAup = default;
            return false;
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
