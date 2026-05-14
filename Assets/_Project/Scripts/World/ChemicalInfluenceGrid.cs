using Hecton8.Bootstrap;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6440)]
    [AddComponentMenu("Hecton8/World/Chemical Influence Grid")]
    public sealed class ChemicalInfluenceGrid : MonoBehaviour, ISlowTickable
    {
        internal enum ChemicalChannel : int
        {
            Blood = 0,
            Exhaust = 1,
            Fear = 2,
            Toxicity = 3,
        }

        internal struct ChemicalBreadcrumbWaypoint
        {
            public float3 AbsolutePosition;
            public double3 AbsolutePositionDouble;
            public float3 RuntimePosition;
            public float4 Channels;
            public float RadiusMeters;
            public float SpawnTime;
            public float ExpiresAt;
        }

        private const string RuntimeRootName = "[ChemicalInfluenceGrid]";
        private const string NativeMemoryOwner = nameof(ChemicalInfluenceGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const int DefaultBreadcrumbCapacity = 64;
        private const int MaxDefoliantDeadZones = 64;
        private const float DefaultMaximumChannelIntensity = 32f;
        private const float MinimumRadiusMeters = 0.25f;
        private const float MinimumSubmarineVelocitySqr = 0.25f;
        private const float MinimumTransportSignal = 0.05f;
        private const float ChemicalTransientRadiusMeters = 18f;
        private const float ChemicalTransientLifetimeSeconds = 12f;
        private const float DefaultDefoliantDeadZoneRadiusMeters = 30f;
        private const float BreadcrumbMergeDistanceMeters = 8f;
        private const int ScentGridAxis = 64;
        private const int ScentGridCellCount = ScentGridAxis * ScentGridAxis;
        private const float ScentGridCellSizeMeters = 50f;
        private const float ScentGridDissipateIntervalSeconds = 1f;
        private const int ScentGridHalfAxis = ScentGridAxis / 2;

        private static ChemicalInfluenceGrid _activeRuntimeInstance;

        [Header("Breadcrumbs")]
        [SerializeField, Range(8, 64)] private int breadcrumbCapacity = DefaultBreadcrumbCapacity;
        [SerializeField, Min(0.25f)] private float breadcrumbDropIntervalSeconds = 5f;
        [SerializeField, Min(1f)] private float breadcrumbLifetimeSeconds = 90f;
        [SerializeField, Min(1f)] private float breadcrumbRadiusMeters = 28f;
        [SerializeField, Min(0.1f)] private float maximumChannelIntensity = DefaultMaximumChannelIntensity;

        [Header("Diagnostics")]
        [SerializeField] private int _debugBreadcrumbCount;
        [SerializeField] private int _debugPendingWriteCount;
        [SerializeField] private int _debugScentGridActiveCellCount;
        [SerializeField] private Vector3 _debugLastBreadcrumbPosition;

        // COLD ALLOC: Vector4[64] - permanent defoliant dead-zone registry in absolute-universe space - owner: ChemicalInfluenceGrid
        private readonly Vector4[] _defoliantDeadZones = new Vector4[MaxDefoliantDeadZones];
        private readonly double3[] _defoliantDeadZoneCentersDouble = new double3[MaxDefoliantDeadZones];

        private NativeArray<ChemicalBreadcrumbWaypoint> _breadcrumbs;
        private NativeArray<byte> _scentGrid;
        private bool _registeredSlowTick;
        private bool _runtimeInitialized;
        private int _breadcrumbCount;
        private int _breadcrumbWriteCursor;
        private int2 _scentGridOriginCell;
        private bool _scentGridHasOrigin;
        private int _publishedFrameId = -1;
        private float _nextScentGridDissipateTime;
        private int _defoliantDeadZoneCount;
        private Transform _cachedPlayerTransform;
        private HectonSurvivalSystem _cachedPlayerSurvival;

        public static ChemicalInfluenceGrid ActiveRuntimeInstance => _activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRuntimeInstance = null;
        }

        public static ChemicalInfluenceGrid EnsureRuntimeInstance()
        {
            if (_activeRuntimeInstance != null)
                return _activeRuntimeInstance;

            GameObject runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - runtime-owned breadcrumb service root - owner: ChemicalInfluenceGrid
            return runtimeRoot.AddComponent<ChemicalInfluenceGrid>();
        }

        internal static void BeginAiFrame(int frameId)
        {
            EnsureRuntimeInstance().PublishFrame(frameId);
        }

        internal static bool TryGetPublishedSnapshot(
            out NativeArray<float4> frontGrid,
            out NativeArray<float4> overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            EnsureRuntimeInstance().PublishFrame(Time.frameCount);
            frontGrid = default;
            overlayGrid = default;
            dimensions = int3.zero;
            origin = float3.zero;
            cellSize = new float3(1f, 1f, 1f);
            return false;
        }

        internal static bool TryGetActivePublishedSnapshot(
            out NativeArray<float4> frontGrid,
            out NativeArray<float4> overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            frontGrid = default;
            overlayGrid = default;
            dimensions = int3.zero;
            origin = float3.zero;
            cellSize = new float3(1f, 1f, 1f);
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            if (instance == null)
                return false;

            instance.PublishFrame(Time.frameCount);
            return false;
        }

        internal static bool TryGetPublishedBreadcrumbs(
            out NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs,
            out int count,
            out float followStepMeters)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            breadcrumbs = instance._breadcrumbs;
            count = instance._breadcrumbCount;
            followStepMeters = math.max(1f, instance.breadcrumbRadiusMeters * 0.5f);
            return breadcrumbs.IsCreated && count > 0;
        }

        internal static bool TrySampleNormalizedChannels(Vector3 worldPosition, out float4 normalizedChannels)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            return instance.TrySampleNormalizedChannelsInternal(
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                out normalizedChannels);
        }

        internal static bool TrySampleScentGrid01(Vector3 worldPosition, out float scent01)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            return instance.TrySampleScentGrid01Internal(worldPosition, out scent01);
        }

        internal static bool TryFindNearestScentWaypoint(
            Vector3 worldPosition,
            ChemicalChannel channel,
            out ChemicalBreadcrumbWaypoint waypoint,
            out float distanceMeters,
            out float intensity01)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            return instance.TryFindNearestScentWaypointInternal(
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                channel,
                out waypoint,
                out distanceMeters,
                out intensity01);
        }

        internal static void QueueBloodScent(Vector3 worldPosition, float intensity = 1f)
        {
            float clampedIntensity = math.max(0f, intensity);
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.DropBreadcrumb(worldPosition, new float4(clampedIntensity, 0f, 0f, 0f), ChemicalChannel.Blood);
            instance.WriteScentGridCell(worldPosition, clampedIntensity);
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueExhaustScent(Vector3 worldPosition, float intensity = 1f)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().DropBreadcrumb(worldPosition, new float4(0f, clampedIntensity, 0f, 0f), ChemicalChannel.Exhaust);
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueFearPheromone(Vector3 worldPosition, float intensity)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().DropBreadcrumb(worldPosition, new float4(0f, 0f, clampedIntensity, 0f), ChemicalChannel.Fear);
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueToxicityBurst(Vector3 worldPosition, float intensity)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, clampedIntensity), ChemicalChannel.Toxicity);
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueDefoliantBurst(Vector3 worldPosition, float intensity)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, -clampedIntensity), ChemicalChannel.Toxicity);
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueDefoliantDeadZone(Vector3 worldPosition, float radiusMeters = DefaultDefoliantDeadZoneRadiusMeters, float intensity = DefaultMaximumChannelIntensity)
        {
            float safeRadius = math.max(MinimumRadiusMeters, radiusMeters);
            float clampedIntensity = math.max(0f, intensity);
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.RegisterDefoliantDeadZone(worldPosition, safeRadius);
            instance.DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, -math.max(1f, clampedIntensity)), ChemicalChannel.Toxicity, safeRadius);
            RegisterChemicalTransient(worldPosition, clampedIntensity);

            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.ApplyDefoliantDeadZone(worldPosition, safeRadius);
        }

        internal static bool IsInsidePermanentDefoliantDeadZone(Vector3 worldPosition)
        {
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            return instance != null &&
                   instance.IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPosition));
        }

        internal static bool IsInsidePermanentDefoliantDeadZoneAbsolute(Vector3 absolutePosition)
        {
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            return instance != null && instance.IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(ToDouble3(absolutePosition));
        }

        private static void RegisterChemicalTransient(Vector3 worldPosition, float intensity)
        {
            if (intensity <= 0f)
                return;

            WorldSpatialHashGrid.RegisterTransientEvent(
                worldPosition,
                ChemicalTransientRadiusMeters,
                math.saturate(intensity),
                ChemicalTransientLifetimeSeconds,
                SpatialTransientEventType.ChemicalCloud,
                SpatialInteractionFlags.ChemicalReceiver);
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
            InitializeRuntime();
        }

        private void OnEnable()
        {
            InitializeRuntime();
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            DisposeBuffers();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            DisposeBuffers();

            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
        }

        public void SlowTick()
        {
            InitializeRuntime();
            PublishFrame(Time.frameCount);
            TryDissipateScentGrid(Time.time);
            CollectPersistentRuntimeEmissions();
            PruneExpiredBreadcrumbs(Time.time);
            RefreshRuntimePositions();
            UpdateDebugState();
        }

        private void EnsureSingletonOwnership()
        {
            if (_activeRuntimeInstance != null && _activeRuntimeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            _activeRuntimeInstance = this;
        }

        private void InitializeRuntime()
        {
            if (_runtimeInitialized)
                return;

            EnsureSingletonOwnership();
            if (_activeRuntimeInstance != this)
                return;

            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(this);

            breadcrumbCapacity = Mathf.Clamp(breadcrumbCapacity, 8, DefaultBreadcrumbCapacity);
            breadcrumbDropIntervalSeconds = Mathf.Max(0.25f, breadcrumbDropIntervalSeconds);
            breadcrumbLifetimeSeconds = Mathf.Max(1f, breadcrumbLifetimeSeconds);
            breadcrumbRadiusMeters = Mathf.Max(1f, breadcrumbRadiusMeters);
            maximumChannelIntensity = Mathf.Max(0.1f, maximumChannelIntensity);
            InitializeBuffers();
            _runtimeInitialized = true;
            UpdateDebugState();
        }

        private void InitializeBuffers()
        {
            if (_breadcrumbs.IsCreated)
                return;

            _breadcrumbs = new NativeArray<ChemicalBreadcrumbWaypoint>(
                math.max(8, math.min(DefaultBreadcrumbCapacity, breadcrumbCapacity)),
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ChemicalBreadcrumbWaypoint>[<=64] - AUP scent breadcrumb ring with hard SlowTick loop cap - owner: ChemicalInfluenceGrid
            NativeMemorySentinel.RegisterNativeArray(_breadcrumbs, NativeMemoryOwner, nameof(_breadcrumbs), NativeMemoryLifetime);
            _scentGrid = new NativeArray<byte>(
                ScentGridCellCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[4096] - 50m low-res blood scent grid - owner: ChemicalInfluenceGrid
            NativeMemorySentinel.RegisterNativeArray(_scentGrid, NativeMemoryOwner, nameof(_scentGrid), NativeMemoryLifetime);
            _nextScentGridDissipateTime = Time.time + ScentGridDissipateIntervalSeconds;
        }

        private void PublishFrame(int frameId)
        {
            InitializeRuntime();
            if (_activeRuntimeInstance != this)
                return;
            if (_publishedFrameId == frameId)
                return;

            _publishedFrameId = frameId;
            PruneExpiredBreadcrumbs(Time.time);
            RefreshRuntimePositions();
        }

        private void CollectPersistentRuntimeEmissions()
        {
            if (TryResolvePlayerSurvival(out Transform playerTransform, out HectonSurvivalSystem playerSurvival) &&
                playerTransform != null &&
                playerSurvival != null &&
                playerSurvival.IsBleeding)
            {
                DropBreadcrumb(playerTransform.position, new float4(1f, 0f, 0f, 0f), ChemicalChannel.Blood);
            }

            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal playerNoise) &&
                playerNoise.TransportBoost01 >= MinimumTransportSignal)
            {
                DropBreadcrumb(playerNoise.Position, new float4(0f, 1f, 0f, 0f), ChemicalChannel.Exhaust);
            }

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            if (submarine != null &&
                submarine.PlatformTransform != null &&
                submarine.HullRigidbody != null &&
                submarine.HullRigidbody.linearVelocity.sqrMagnitude >= MinimumSubmarineVelocitySqr)
            {
                DropBreadcrumb(submarine.PlatformTransform.position, new float4(0f, 1f, 0f, 0f), ChemicalChannel.Exhaust);
            }
        }

        private bool TryResolvePlayerSurvival(out Transform playerTransform, out HectonSurvivalSystem playerSurvival)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (_cachedPlayerTransform != playerTransform)
            {
                _cachedPlayerTransform = playerTransform;
                _cachedPlayerSurvival = null;
                if (_cachedPlayerTransform != null)
                    _cachedPlayerTransform.TryGetComponent(out _cachedPlayerSurvival);
            }

            playerSurvival = _cachedPlayerSurvival;
            return playerTransform != null && playerSurvival != null;
        }

        private void DropBreadcrumb(Vector3 worldPosition, float4 channels, ChemicalChannel primaryChannel, float radiusOverrideMeters = 0f)
        {
            InitializeRuntime();
            if (!_breadcrumbs.IsCreated)
                return;

            float now = Time.time;
            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPosition);
            float3 absolute = ToFloat3(absolutePosition);
            float safeRadius = math.max(1f, radiusOverrideMeters > 0f ? radiusOverrideMeters : breadcrumbRadiusMeters);
            int mergeIndex = FindMergeCandidate(absolutePosition, primaryChannel, now);
            float4 clampedChannels = ClampChemicalChannels(channels, maximumChannelIntensity);
            if (mergeIndex >= 0)
            {
                ChemicalBreadcrumbWaypoint merged = _breadcrumbs[mergeIndex];
                merged.AbsolutePosition = absolute;
                merged.AbsolutePositionDouble = absolutePosition;
                merged.RuntimePosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
                merged.Channels = ClampChemicalChannels(merged.Channels + clampedChannels, maximumChannelIntensity);
                merged.RadiusMeters = math.max(merged.RadiusMeters, safeRadius);
                merged.SpawnTime = now;
                merged.ExpiresAt = now + breadcrumbLifetimeSeconds;
                _breadcrumbs[mergeIndex] = merged;
                _debugLastBreadcrumbPosition = worldPosition;
                return;
            }

            int writeIndex = ResolveWriteIndex(now);
            _breadcrumbs[writeIndex] = new ChemicalBreadcrumbWaypoint
            {
                AbsolutePosition = absolute,
                AbsolutePositionDouble = absolutePosition,
                RuntimePosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                Channels = clampedChannels,
                RadiusMeters = safeRadius,
                SpawnTime = now,
                ExpiresAt = now + breadcrumbLifetimeSeconds
            };

            if (_breadcrumbCount < _breadcrumbs.Length)
                _breadcrumbCount++;

            _breadcrumbWriteCursor = (_breadcrumbWriteCursor + 1) % _breadcrumbs.Length;
            _debugLastBreadcrumbPosition = worldPosition;
        }

        private void WriteScentGridCell(Vector3 worldPosition, float intensity)
        {
            InitializeRuntime();
            if (!_scentGrid.IsCreated || intensity <= 0f)
                return;

            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPosition);
            int2 cell = ResolveScentGridCell(absolutePosition);
            EnsureScentGridContainsCell(cell);
            if (!TryResolveScentGridIndex(cell, out int index))
                return;

            int writeValue = math.clamp((int)math.round(math.saturate(intensity) * byte.MaxValue), 1, byte.MaxValue);
            _scentGrid[index] = (byte)math.min(byte.MaxValue, _scentGrid[index] + writeValue);
        }

        private void TryDissipateScentGrid(float now)
        {
            if (now < _nextScentGridDissipateTime)
                return;

            _nextScentGridDissipateTime = now + ScentGridDissipateIntervalSeconds;
            DissipateScentGrid();
        }

        private void DissipateScentGrid()
        {
            if (!_scentGrid.IsCreated)
                return;

            int activeCells = 0;
            for (int i = 0; i < _scentGrid.Length; i++)
            {
                byte value = _scentGrid[i];
                if (value == 0)
                    continue;

                value--;
                _scentGrid[i] = value;
                if (value > 0)
                    activeCells++;
            }

            _debugScentGridActiveCellCount = activeCells;
        }

        private bool TrySampleScentGrid01Internal(Vector3 worldPosition, out float scent01)
        {
            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPosition);
            return TrySampleScentGrid01AbsoluteInternal(absolutePosition, out scent01);
        }

        private bool TrySampleScentGrid01AbsoluteInternal(double3 absolutePosition, out float scent01)
        {
            scent01 = 0f;
            if (!_scentGrid.IsCreated || !_scentGridHasOrigin)
                return false;

            int2 cell = ResolveScentGridCell(absolutePosition);
            if (!TryResolveScentGridIndex(cell, out int index))
                return false;

            byte value = _scentGrid[index];
            if (value == 0)
                return false;

            scent01 = value * (1f / byte.MaxValue);
            return true;
        }

        private void EnsureScentGridContainsCell(int2 cell)
        {
            if (!_scentGridHasOrigin)
            {
                _scentGridOriginCell = cell - new int2(ScentGridHalfAxis, ScentGridHalfAxis);
                _scentGridHasOrigin = true;
                return;
            }

            int2 local = cell - _scentGridOriginCell;
            if (local.x >= 0 && local.y >= 0 && local.x < ScentGridAxis && local.y < ScentGridAxis)
                return;

            RecenterScentGrid(cell - new int2(ScentGridHalfAxis, ScentGridHalfAxis));
        }

        private void RecenterScentGrid(int2 newOriginCell)
        {
            if (!_scentGrid.IsCreated)
                return;

            for (int i = 0; i < _scentGrid.Length; i++)
                _scentGrid[i] = 0;

            _scentGridOriginCell = newOriginCell;
            _scentGridHasOrigin = true;
        }

        private bool TryResolveScentGridIndex(int2 cell, out int index)
        {
            if (!_scentGridHasOrigin)
            {
                index = -1;
                return false;
            }

            int2 local = cell - _scentGridOriginCell;
            if (local.x < 0 || local.y < 0 || local.x >= ScentGridAxis || local.y >= ScentGridAxis)
            {
                index = -1;
                return false;
            }

            index = local.x + local.y * ScentGridAxis;
            return true;
        }

        private static int2 ResolveScentGridCell(double3 absolutePosition)
        {
            double inverseCellSize = 1d / ScentGridCellSizeMeters;
            return new int2(
                FastFloorToInt(absolutePosition.x * inverseCellSize),
                FastFloorToInt(absolutePosition.z * inverseCellSize));
        }

        private int FindMergeCandidate(double3 absolutePosition, ChemicalChannel primaryChannel, float now)
        {
            int safeCount = math.min(_breadcrumbCount, _breadcrumbs.Length);
            double mergeDistanceSq = (double)BreadcrumbMergeDistanceMeters * BreadcrumbMergeDistanceMeters;
            int channelIndex = (int)primaryChannel;
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = _breadcrumbs[i];
                if (waypoint.ExpiresAt <= now)
                    continue;

                if (math.abs(GetChannel(waypoint.Channels, channelIndex)) <= 0f)
                    continue;

                if (now - waypoint.SpawnTime < breadcrumbDropIntervalSeconds &&
                    math.lengthsq(ResolveWaypointAbsolutePositionDouble(in waypoint) - absolutePosition) <= mergeDistanceSq)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveWriteIndex(float now)
        {
            if (_breadcrumbCount < _breadcrumbs.Length)
                return _breadcrumbCount;

            int safeLength = _breadcrumbs.Length;
            for (int i = 0; i < safeLength; i++)
            {
                int index = (_breadcrumbWriteCursor + i) % safeLength;
                if (_breadcrumbs[index].ExpiresAt <= now)
                    return index;
            }

            return _breadcrumbWriteCursor;
        }

        private void PruneExpiredBreadcrumbs(float now)
        {
            if (!_breadcrumbs.IsCreated || _breadcrumbCount <= 0)
                return;

            int write = 0;
            int safeCount = math.min(_breadcrumbCount, _breadcrumbs.Length);
            for (int read = 0; read < safeCount; read++)
            {
                ChemicalBreadcrumbWaypoint waypoint = _breadcrumbs[read];
                if (waypoint.ExpiresAt <= now)
                    continue;

                if (write != read)
                    _breadcrumbs[write] = waypoint;
                write++;
            }

            for (int i = write; i < safeCount; i++)
                _breadcrumbs[i] = default;

            _breadcrumbCount = write;
            if (_breadcrumbs.Length > 0)
                _breadcrumbWriteCursor = write % _breadcrumbs.Length;
        }

        private void RefreshRuntimePositions()
        {
            if (!_breadcrumbs.IsCreated)
                return;

            int safeCount = math.min(_breadcrumbCount, _breadcrumbs.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = _breadcrumbs[i];
                Vector3 runtime = HectonFloatingOrigin.ToRuntimePosition(ResolveWaypointAbsolutePositionDouble(in waypoint));
                waypoint.RuntimePosition = new float3(runtime.x, runtime.y, runtime.z);
                _breadcrumbs[i] = waypoint;
            }
        }

        private void RegisterDefoliantDeadZone(Vector3 worldPosition, float radiusMeters)
        {
            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPosition);
            float safeRadius = math.max(MinimumRadiusMeters, radiusMeters);
            double mergeRadiusSq = (double)safeRadius * safeRadius;
            for (int i = 0; i < _defoliantDeadZoneCount; i++)
            {
                Vector4 zone = _defoliantDeadZones[i];
                double3 zoneCenter = ResolveDefoliantDeadZoneCenterDouble(i, zone);
                if (math.lengthsq(zoneCenter - absolutePosition) > mergeRadiusSq)
                    continue;

                double3 mergedCenter = (zoneCenter + absolutePosition) * 0.5d;
                _defoliantDeadZoneCentersDouble[i] = mergedCenter;
                _defoliantDeadZones[i] = new Vector4(
                    (float)mergedCenter.x,
                    (float)mergedCenter.y,
                    (float)mergedCenter.z,
                    Mathf.Max(zone.w, safeRadius));
                return;
            }

            int writeIndex = _defoliantDeadZoneCount < _defoliantDeadZones.Length
                ? _defoliantDeadZoneCount++
                : _defoliantDeadZones.Length - 1;
            _defoliantDeadZones[writeIndex] = new Vector4(
                (float)absolutePosition.x,
                (float)absolutePosition.y,
                (float)absolutePosition.z,
                safeRadius);
            _defoliantDeadZoneCentersDouble[writeIndex] = absolutePosition;
        }

        private bool TrySampleNormalizedChannelsInternal(float3 worldPosition, out float4 normalizedChannels)
        {
            normalizedChannels = float4.zero;
            Vector3 runtimePosition = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            bool insideDeadZone = IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(absolutePosition);
            float4 accumulated = float4.zero;
            bool hasSample = false;
            float now = Time.time;
            double3 queryAbsolute = absolutePosition;

            if (_breadcrumbs.IsCreated)
            {
                int safeCount = math.min(_breadcrumbCount, _breadcrumbs.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    ChemicalBreadcrumbWaypoint waypoint = _breadcrumbs[i];
                    if (waypoint.ExpiresAt <= now || waypoint.RadiusMeters <= 0f)
                        continue;

                    float radius = math.max(MinimumRadiusMeters, waypoint.RadiusMeters);
                    double distanceSq = math.lengthsq(ResolveWaypointAbsolutePositionDouble(in waypoint) - queryAbsolute);
                    double radiusSq = (double)radius * radius;
                    if (distanceSq > radiusSq)
                        continue;

                    float distanceSq01 = math.saturate((float)(distanceSq / radiusSq));
                    float falloff = SmoothStep01(1f - distanceSq01);
                    accumulated += waypoint.Channels * falloff;
                    hasSample = true;
                }
            }

            if (TrySampleScentGrid01AbsoluteInternal(absolutePosition, out float scentGrid01))
            {
                accumulated.x = math.max(accumulated.x, scentGrid01 * maximumChannelIntensity);
                hasSample = true;
            }

            if (!hasSample && !insideDeadZone)
                return false;

            float inverseMaxIntensity = 1f / math.max(0.1f, maximumChannelIntensity);
            float4 normalized = accumulated * inverseMaxIntensity;
            normalizedChannels = new float4(
                math.saturate(normalized.x),
                math.saturate(normalized.y),
                math.saturate(normalized.z),
                insideDeadZone ? -1f : math.clamp(normalized.w, -1f, 1f));
            return true;
        }

        private bool TryFindNearestScentWaypointInternal(
            float3 worldPosition,
            ChemicalChannel channel,
            out ChemicalBreadcrumbWaypoint nearestWaypoint,
            out float distanceMeters,
            out float intensity01)
        {
            nearestWaypoint = default;
            distanceMeters = 0f;
            intensity01 = 0f;
            if (!_breadcrumbs.IsCreated || _breadcrumbCount <= 0)
                return false;

            Vector3 runtimePosition = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
            double3 queryAbsolute = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            int channelIndex = (int)channel;
            int safeCount = math.min(_breadcrumbCount, _breadcrumbs.Length);
            float now = Time.time;
            double bestDistanceSq = double.MaxValue;
            float bestIntensity = 0f;
            bool found = false;

            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = _breadcrumbs[i];
                if (waypoint.ExpiresAt <= now || waypoint.RadiusMeters <= 0f)
                    continue;

                float channelSignal = GetChannel(waypoint.Channels, channelIndex);
                if (channelSignal <= 0f)
                    continue;

                float radius = math.max(MinimumRadiusMeters, waypoint.RadiusMeters);
                double distanceSq = math.lengthsq(ResolveWaypointAbsolutePositionDouble(in waypoint) - queryAbsolute);
                double radiusSq = (double)radius * radius;
                if (distanceSq > radiusSq || distanceSq >= bestDistanceSq)
                    continue;

                float distanceSq01 = math.saturate((float)(distanceSq / radiusSq));
                bestIntensity = math.saturate(channelSignal * SmoothStep01(1f - distanceSq01) / math.max(0.1f, maximumChannelIntensity));
                bestDistanceSq = distanceSq;
                nearestWaypoint = waypoint;
                found = true;
            }

            if (!found)
                return false;

            distanceMeters = bestDistanceSq > 0d ? (float)(bestDistanceSq * math.rsqrt(bestDistanceSq)) : 0f;
            intensity01 = bestIntensity;
            return true;
        }

        private bool IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(Vector3 absolutePosition)
        {
            return IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(ToDouble3(absolutePosition));
        }

        private bool IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(double3 absolutePosition)
        {
            for (int i = 0; i < _defoliantDeadZoneCount; i++)
            {
                Vector4 zone = _defoliantDeadZones[i];
                double radiusSq = (double)zone.w * zone.w;
                double3 zoneCenter = ResolveDefoliantDeadZoneCenterDouble(i, zone);
                if (math.lengthsq(absolutePosition - zoneCenter) <= radiusSq)
                    return true;
            }

            return false;
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static int FastFloorToInt(double value)
        {
            if (!math.isfinite(value))
                return 0;

            if (value >= int.MaxValue)
                return int.MaxValue;

            if (value <= int.MinValue)
                return int.MinValue;

            return (int)math.floor(value);
        }

        private static double3 ResolveWaypointAbsolutePositionDouble(in ChemicalBreadcrumbWaypoint waypoint)
        {
            if (math.all(math.isfinite(waypoint.AbsolutePositionDouble)) &&
                (math.any(waypoint.AbsolutePositionDouble != double3.zero) ||
                 math.all(waypoint.AbsolutePosition == float3.zero)))
                return waypoint.AbsolutePositionDouble;

            return ToDouble3(waypoint.AbsolutePosition);
        }

        private double3 ResolveDefoliantDeadZoneCenterDouble(int index, Vector4 legacyZone)
        {
            if (index >= 0 &&
                index < _defoliantDeadZoneCentersDouble.Length &&
                math.all(math.isfinite(_defoliantDeadZoneCentersDouble[index])) &&
                (math.any(_defoliantDeadZoneCentersDouble[index] != double3.zero) ||
                 (legacyZone.x == 0f && legacyZone.y == 0f && legacyZone.z == 0f)))
                return _defoliantDeadZoneCentersDouble[index];

            return new double3(legacyZone.x, legacyZone.y, legacyZone.z);
        }

        private static float4 ClampChemicalChannels(float4 value, float maxChannelIntensity)
        {
            float safeMax = math.max(0.1f, maxChannelIntensity);
            return new float4(
                math.clamp(value.x, 0f, safeMax),
                math.clamp(value.y, 0f, safeMax),
                math.clamp(value.z, 0f, safeMax),
                math.clamp(value.w, -safeMax, safeMax));
        }

        private static float GetChannel(float4 value, int channelIndex)
        {
            switch (channelIndex)
            {
                case 0: return value.x;
                case 1: return value.y;
                case 2: return value.z;
                default: return value.w;
            }
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void DisposeBuffers()
        {
            if (_breadcrumbs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_breadcrumbs);
                _breadcrumbs.Dispose();
                _breadcrumbs = default;
            }

            if (_scentGrid.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scentGrid);
                _scentGrid.Dispose();
                _scentGrid = default;
            }

            _breadcrumbCount = 0;
            _breadcrumbWriteCursor = 0;
            _scentGridOriginCell = int2.zero;
            _scentGridHasOrigin = false;
            _publishedFrameId = -1;
            _nextScentGridDissipateTime = 0f;
            _debugScentGridActiveCellCount = 0;
            _runtimeInitialized = false;
            _cachedPlayerTransform = null;
            _cachedPlayerSurvival = null;
        }

        private void UpdateDebugState()
        {
            _debugBreadcrumbCount = _breadcrumbCount;
            _debugPendingWriteCount = _breadcrumbs.IsCreated
                ? math.max(0, _breadcrumbs.Length - _breadcrumbCount)
                : 0;
        }
    }
}
