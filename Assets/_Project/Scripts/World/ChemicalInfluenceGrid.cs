using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static Hecton8.Core.UnityMathematicsExtensions;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6440)]
    [AddComponentMenu("Hecton8/World/Chemical Influence Grid")]
    public sealed unsafe class ChemicalInfluenceGrid : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IChemicalInfluenceReadModel
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

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct ChemicalCellDTO
        {
            [FieldOffset(0)] public float BloodConcentration;
            [FieldOffset(4)] public float PheromoneConcentration;
            [FieldOffset(8)] public float ToxinConcentration;
            [FieldOffset(12)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalEmitterDTO
        {
            [FieldOffset(0)] public double3 Aup;
            [FieldOffset(24)] public float4 Channels;
            [FieldOffset(40)] public float RadiusMeters;
            [FieldOffset(44)] public float LifetimeSeconds;
            [FieldOffset(48)] public uint ProfileHash;
            [FieldOffset(52)] public uint Flags;
            [FieldOffset(56)] public uint SpawnFrame;
            [FieldOffset(60)] public uint SourceHash;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalDefoliantZoneDTO
        {
            [FieldOffset(0)] public double3 CenterAup;
            [FieldOffset(24)] public float RadiusMeters;
            [FieldOffset(28)] public float Intensity;
            [FieldOffset(32)] public uint Flags;
            [FieldOffset(36)] public uint SourceHash;
            [FieldOffset(40)] private ulong _pad0;
            [FieldOffset(48)] private ulong _pad1;
            [FieldOffset(56)] private ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalTuningDTO
        {
            [FieldOffset(0)] public double SimulationTickDelta;
            [FieldOffset(8)] public float BaseDiffusionRate;
            [FieldOffset(12)] public float AdvectionStrength;
            [FieldOffset(16)] public float DissipationRate;
            [FieldOffset(20)] public float EmitterRadiusScale;
            [FieldOffset(24)] public float GlobalQualityWeight;
            [FieldOffset(28)] public float MaxChannelIntensity;
            [FieldOffset(32)] public uint Revision;
            [FieldOffset(36)] public uint Flags;
            [FieldOffset(40)] public int Iterations;
            [FieldOffset(44)] public float CellSizeMeters;
            [FieldOffset(48)] private ulong _pad0;
            [FieldOffset(56)] private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalTelemetryEntry
        {
            [FieldOffset(0)] public double3 GridOriginAup;
            [FieldOffset(24)] public float MaxBlood;
            [FieldOffset(28)] public float SolverMicros;
            [FieldOffset(32)] public uint Frame;
            [FieldOffset(36)] public int ActiveEmitters;
            [FieldOffset(40)] public int MockEmitters;
            [FieldOffset(44)] public int Iterations;
            [FieldOffset(48)] public uint StateHash;
            [FieldOffset(52)] public uint Flags;
            [FieldOffset(56)] public float GlobalQualityWeight;
            [FieldOffset(60)] public int GridShiftManhattan;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalAtomicCounterDTO
        {
            [FieldOffset(0)] public int MaxBloodBits;
            [FieldOffset(4)] public int ActiveEmitterCount;
            [FieldOffset(8)] public int MockEmitterCount;
            [FieldOffset(12)] public int JacobiIterations;
            [FieldOffset(16)] public int NaNFlag;
            [FieldOffset(20)] public int StateHash;
            [FieldOffset(24)] public int ActiveCellCount;
            [FieldOffset(28)] private int _pad0;
            [FieldOffset(32)] private ulong _pad1;
            [FieldOffset(40)] private ulong _pad2;
            [FieldOffset(48)] private ulong _pad3;
            [FieldOffset(56)] private ulong _pad4;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalEmitterProfileDTO
        {
            [FieldOffset(0)] public uint ProfileHash;
            [FieldOffset(4)] public float BloodMultiplier;
            [FieldOffset(8)] public float PheromoneMultiplier;
            [FieldOffset(12)] public float ToxinMultiplier;
            [FieldOffset(16)] public float RadiusMultiplier;
            [FieldOffset(20)] public float DissipationMultiplier;
            [FieldOffset(24)] public uint Flags;
            [FieldOffset(28)] public uint SourceHash;
            [FieldOffset(32)] private ulong _pad0;
            [FieldOffset(40)] private ulong _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalSampleRequestDTO
        {
            [FieldOffset(0)] public double3 Aup;
            [FieldOffset(24)] public uint EntityId;
            [FieldOffset(28)] public uint Flags;
            [FieldOffset(32)] private ulong _pad0;
            [FieldOffset(40)] private ulong _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ChemicalSampleResultDTO
        {
            [FieldOffset(0)] public float4 Channels;
            [FieldOffset(16)] public float BloodScalar;
            [FieldOffset(20)] public float FearScalar;
            [FieldOffset(24)] public uint EntityId;
            [FieldOffset(28)] public uint Flags;
            [FieldOffset(32)] private ulong _pad0;
            [FieldOffset(40)] private ulong _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        private const string RuntimeRootName = "[ChemicalInfluenceGrid]";
        private const string VaultOwnerName = nameof(ChemicalInfluenceGrid);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_CHEMISTRY_SURGEON.bin";
        private const string TelemetryDumpPayloadLabel = "chemicalInfluenceTelemetryDumpPayload";
        private const string ProfileCsvRelativePath = "_Project/Data/chemical_emitter_profiles.csv";
        private const int DefaultBreadcrumbCapacity = 64;
        private const int MaxDefoliantDeadZones = 64;
        private const int MaxActiveEmitters = 160;
        private const int MaxMockEmitters = 8;
        private const int TelemetryFrameCount = 300;
        private const int ProfileTableCapacity = 64;
        private const int CsvScratchBytes = 65536;
        private const int GridAxisX = 48;
        private const int GridAxisY = 16;
        private const int GridAxisZ = 48;
        private const int GridSliceStride = GridAxisX * GridAxisZ;
        private const int ChemicalCellCount = GridAxisX * GridAxisY * GridAxisZ;
        private const float DefaultCellSizeMeters = 8f;
        private const float DefaultMaximumChannelIntensity = 32f;
        private const float DefaultBreadcrumbRadiusMeters = 28f;
        private const float MinimumRadiusMeters = 0.25f;
        private const float MaxChemicalRadiusMeters = DefaultCellSizeMeters * GridAxisX;
        private const float MinimumSubmarineVelocitySqr = 0.25f;
        private const float ChemicalTransientRadiusMeters = 18f;
        private const float ChemicalTransientLifetimeSeconds = 12f;
        private const float DefaultDefoliantDeadZoneRadiusMeters = 30f;
        private const float BreadcrumbMergeDistanceMeters = 8f;
        private const float GridSampleEpsilon = 0.0001f;
        private const double DefaultSimulationTickDelta = 1.0d / 60.0d;
        private const uint EmitterFlagBlood = 1u << 0;
        private const uint EmitterFlagPheromone = 1u << 1;
        private const uint EmitterFlagToxin = 1u << 2;
        private const uint EmitterFlagDefoliant = 1u << 3;
        private const uint EmitterFlagMock = 1u << 4;
        private const uint CellFlagOccluded = 1u << 0;
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint ChemicalSourceHash = 0x53483138u;
        private const ulong ChemicalDumpMagic = 0x3833315F4D454843UL;

        private static readonly int3 GridDimensions = new int3(GridAxisX, GridAxisY, GridAxisZ);
        private static readonly int3 GridHalfExtents = new int3(GridAxisX / 2, GridAxisY / 2, GridAxisZ / 2);
        private static readonly BufferID GridFrontBufferId = BufferID.ChemicalInfluenceGrid_GridFrontBufferId;
        private static readonly BufferID GridBackBufferId = BufferID.ChemicalInfluenceGrid_GridBackBufferId;
        private static readonly BufferID PublishedGridBufferId = BufferID.ShinobuMetabolismData_ChemicalPublishedGridReadbackBuffer;
        private static readonly BufferID OverlayGridBufferId = BufferID.ShinobuMetabolismData_ChemicalOverlayGridReadbackBuffer;
        private static readonly BufferID BreadcrumbBufferId = BufferID.ChemicalInfluenceGrid_BreadcrumbBufferId;
        private static readonly BufferID PendingEmitterBufferId = BufferID.ChemicalInfluenceGrid_PendingEmitterBufferId;
        private static readonly BufferID PendingEmitterCountBufferId = BufferID.ChemicalInfluenceGrid_PendingEmitterCountBufferId;
        private static readonly BufferID ActiveEmitterBufferId = BufferID.ChemicalInfluenceGrid_ActiveEmitterBufferId;
        private static readonly BufferID ActiveEmitterCountBufferId = BufferID.ChemicalInfluenceGrid_ActiveEmitterCountBufferId;
        private static readonly BufferID MockEmitterBufferId = BufferID.ChemicalInfluenceGrid_MockEmitterBufferId;
        private static readonly BufferID MockEmitterCountBufferId = BufferID.ChemicalInfluenceGrid_MockEmitterCountBufferId;
        private static readonly BufferID TuningBufferId = BufferID.ShinobuMetabolismData_ChemicalTuningReadbackBuffer;
        private static readonly BufferID TelemetryRingBufferId = BufferID.ShinobuMetabolismData_ChemicalTelemetryReadbackBuffer;
        private static readonly BufferID TelemetryCursorBufferId = BufferID.ShinobuMetabolismData_ChemicalTelemetryCursorReadbackBuffer;
        private static readonly BufferID AtomicCounterBufferId = BufferID.ChemicalInfluenceGrid_AtomicCounterBufferId;
        private static readonly BufferID DefoliantZoneBufferId = BufferID.ChemicalInfluenceGrid_DefoliantZoneBufferId;
        private static readonly BufferID CsvScratchBufferId = BufferID.ChemicalInfluenceGrid_CsvScratchBufferId;
        private static readonly BufferID EmitterProfileTableBufferId = BufferID.ChemicalInfluenceGrid_EmitterProfileTableBufferId;
        private static readonly BufferID EmitterProfileCountBufferId = BufferID.ChemicalInfluenceGrid_EmitterProfileCountBufferId;
        private static readonly ulong SimulationMutationGuardMask =
            MutationGuardBit(GridFrontBufferId) |
            MutationGuardBit(GridBackBufferId) |
            MutationGuardBit(PublishedGridBufferId) |
            MutationGuardBit(OverlayGridBufferId) |
            MutationGuardBit(PendingEmitterBufferId) |
            MutationGuardBit(PendingEmitterCountBufferId) |
            MutationGuardBit(ActiveEmitterBufferId) |
            MutationGuardBit(ActiveEmitterCountBufferId) |
            MutationGuardBit(MockEmitterBufferId) |
            MutationGuardBit(MockEmitterCountBufferId) |
            MutationGuardBit(TelemetryRingBufferId) |
            MutationGuardBit(TelemetryCursorBufferId) |
            MutationGuardBit(AtomicCounterBufferId) |
            MutationGuardBit(DefoliantZoneBufferId);

        private static ChemicalInfluenceGrid _activeRuntimeInstance;

        [Header("Chemical Grid")]
        [SerializeField, Range(8, 64)] private int breadcrumbCapacity = DefaultBreadcrumbCapacity;
        [SerializeField, Min(0.25f)] private float breadcrumbDropIntervalSeconds = 5f;
        [SerializeField, Min(1f)] private float breadcrumbLifetimeSeconds = 90f;
        [SerializeField, Min(1f)] private float breadcrumbRadiusMeters = 28f;
        [SerializeField, Min(0.1f)] private float maximumChannelIntensity = DefaultMaximumChannelIntensity;
        [SerializeField, Min(0.001f)] private float baseDiffusionRate = 0.18f;
        [SerializeField, Min(0f)] private float advectionStrength = 0.72f;
        [SerializeField, Min(0f)] private float dissipationRate = 0.028f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugBreadcrumbCount;
        [SerializeField] private int _debugPendingWriteCount;
        [SerializeField] private int _debugScentGridActiveCellCount;
        [SerializeField] private int _debugActiveEmitterCount;
        [SerializeField] private int _debugMockEmitterCount;
        [SerializeField] private int _debugJacobiIterations;
        [SerializeField] private float _debugMaxBlood;
        [SerializeField] private float _debugLastSolverMicros;
        [SerializeField] private Vector3 _debugLastBreadcrumbPosition;

        private IDataVault _dataVault;
        private VaultGenerationHandle<ChemicalCellDTO> _frontCellHandle;
        private VaultGenerationHandle<ChemicalCellDTO> _backCellHandle;
        private VaultGenerationHandle<float4> _publishedGridHandle;
        private VaultGenerationHandle<float4> _overlayGridHandle;
        private VaultGenerationHandle<ChemicalBreadcrumbWaypoint> _breadcrumbsHandle;
        private VaultGenerationHandle<ChemicalEmitterDTO> _pendingEmitterHandle;
        private VaultGenerationHandle<int> _pendingEmitterCountHandle;
        private VaultGenerationHandle<ChemicalEmitterDTO> _activeEmitterHandle;
        private VaultGenerationHandle<int> _activeEmitterCountHandle;
        private VaultGenerationHandle<ChemicalEmitterDTO> _mockEmitterHandle;
        private VaultGenerationHandle<int> _mockEmitterCountHandle;
        private VaultGenerationHandle<ChemicalTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ChemicalTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ChemicalAtomicCounterDTO> _atomicCounterHandle;
        private VaultGenerationHandle<ChemicalDefoliantZoneDTO> _defoliantZoneHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<ChemicalEmitterProfileDTO> _profileTableHandle;
        private VaultGenerationHandle<int> _profileCountHandle;
        private bool _registeredUpdate;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _registeredReadModel;
        private bool _runtimeInitialized;
        private bool _buffersReady;
        private bool _gridHasOrigin;
        private bool _hasScheduledWork;
        private bool _pendingDuplicateDestroy;
        private bool _scheduledSwapAfterFinalize;
        private bool _scheduledBuffersLocked;
        private IDataVault _scheduledBuffersGuardVault;
        private JobHandle _scheduledHandle;
        private long _scheduledStartTicks;
        private uint _simulationFrameCounter;
        private int _lastScheduledFrame = -1024;
        private int _publishedFrameId = -1;
        private int _breadcrumbCount;
        private int _breadcrumbWriteCursor;
        private int _pendingEmitterWriteCursor;
        private int _defoliantDeadZoneCount;
        private int _scheduledTelemetryIndex = -1;
        private int _scheduledGridShiftManhattan;
        private int3 _gridOriginCell;
        private int3 _scheduledOriginCell;
        private double3 _gridOriginAup;
        private double3 _scheduledOriginAup;
        private float3 _publishedRuntimeOrigin;
        private float3 _scheduledRuntimeOrigin;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject runtimeRoot = new GameObject(RuntimeRootName);
            return runtimeRoot.AddComponent<ChemicalInfluenceGrid>();
#else
            return null;
#endif
        }

        internal static void BeginAiFrame(int frameId)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance != null)
                instance.PublishFrame(frameId);
        }

        private static bool TryGetReadableRuntime(out ChemicalInfluenceGrid instance)
        {
            instance = _activeRuntimeInstance;
            return instance != null && instance._buffersReady;
        }

        internal static bool TryGetPublishedSnapshot(
            out NativeArray<float4>.ReadOnly frontGrid,
            out NativeArray<float4>.ReadOnly overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                frontGrid = default;
                overlayGrid = default;
                dimensions = int3.zero;
                origin = float3.zero;
                cellSize = new float3(DefaultCellSizeMeters);
                return false;
            }

            return instance.TryGetPublishedSnapshotInternal(out frontGrid, out overlayGrid, out dimensions, out origin, out cellSize);
        }

        internal static bool TryGetActivePublishedSnapshot(
            out NativeArray<float4>.ReadOnly frontGrid,
            out NativeArray<float4>.ReadOnly overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                frontGrid = default;
                overlayGrid = default;
                dimensions = int3.zero;
                origin = float3.zero;
                cellSize = new float3(DefaultCellSizeMeters);
                return false;
            }

            return instance.TryGetPublishedSnapshotInternal(out frontGrid, out overlayGrid, out dimensions, out origin, out cellSize);
        }

        internal static bool TryGetPublishedBreadcrumbs(
            out NativeArray<ChemicalBreadcrumbWaypoint>.ReadOnly breadcrumbs,
            out int count,
            out float followStepMeters)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                breadcrumbs = default;
                count = 0;
                followStepMeters = math.max(1f, DefaultCellSizeMeters * 0.5f);
                return false;
            }

            NativeArray<ChemicalBreadcrumbWaypoint> mutableBreadcrumbs =
                instance.OpenChemicalVaultArray(ref instance._breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            breadcrumbs = mutableBreadcrumbs.IsCreated ? mutableBreadcrumbs.AsReadOnly() : default;
            count = instance._breadcrumbCount;
            followStepMeters = math.max(1f, instance.breadcrumbRadiusMeters * 0.5f);
            return breadcrumbs.IsCreated && count > 0;
        }

        internal static bool TrySampleNormalizedChannels(Vector3 worldPosition, out float4 normalizedChannels)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                normalizedChannels = float4.zero;
                return false;
            }

            return instance.TrySampleNormalizedChannelsInternal(
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                out normalizedChannels);
        }

        internal static bool TrySampleScentGrid01(Vector3 worldPosition, out float scent01)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                scent01 = 0f;
                return false;
            }

            return instance.TrySampleScentGrid01Internal(worldPosition, out scent01);
        }

        internal static bool TryFindNearestScentWaypoint(
            Vector3 worldPosition,
            ChemicalChannel channel,
            out ChemicalBreadcrumbWaypoint waypoint,
            out float distanceMeters,
            out float intensity01)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                waypoint = default;
                distanceMeters = 0f;
                intensity01 = 0f;
                return false;
            }

            return instance.TryFindNearestScentWaypointInternal(
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                channel,
                out waypoint,
                out distanceMeters,
                out intensity01);
        }

        internal static void QueueBloodScent(Vector3 worldPosition, float intensity = 1f)
        {
            if (!TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity))
                return;

            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.DropBreadcrumb(worldPosition, new float4(clampedIntensity, 0f, 0f, 0f), ChemicalChannel.Blood);
            instance.QueueChemicalEmitter(worldPosition, new float4(clampedIntensity, 0f, 0f, 0f), instance.breadcrumbRadiusMeters, EmitterFlagBlood, HashAscii("PlayerBleeding"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueExhaustScent(Vector3 worldPosition, float intensity = 1f)
        {
            if (!TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity))
                return;

            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.DropBreadcrumb(worldPosition, new float4(0f, clampedIntensity, 0f, 0f), ChemicalChannel.Exhaust);
            instance.QueueChemicalEmitter(worldPosition, new float4(0f, clampedIntensity, 0f, 0f), instance.breadcrumbRadiusMeters, EmitterFlagPheromone, HashAscii("ExhaustTrail"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueFearPheromone(Vector3 worldPosition, float intensity)
        {
            if (!TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity))
                return;

            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.DropBreadcrumb(worldPosition, new float4(0f, 0f, clampedIntensity, 0f), ChemicalChannel.Fear);
            instance.QueueChemicalEmitter(worldPosition, new float4(0f, 0f, clampedIntensity, 0f), instance.breadcrumbRadiusMeters, EmitterFlagPheromone, HashAscii("FearPheromone"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueToxicityBurst(Vector3 worldPosition, float intensity)
        {
            if (!TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity))
                return;

            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, clampedIntensity), ChemicalChannel.Toxicity);
            instance.QueueChemicalEmitter(worldPosition, new float4(0f, 0f, 0f, clampedIntensity), instance.breadcrumbRadiusMeters, EmitterFlagToxin, HashAscii("ToxinBurst"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueDefoliantBurst(Vector3 worldPosition, float intensity)
        {
            if (!TryResolveChemicalQueueInput(worldPosition, intensity, out float clampedIntensity))
                return;

            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, -clampedIntensity), ChemicalChannel.Toxicity);
            instance.QueueChemicalEmitter(worldPosition, new float4(0f, 0f, 0f, -clampedIntensity), instance.breadcrumbRadiusMeters, EmitterFlagDefoliant, HashAscii("DefoliantBurst"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueDefoliantDeadZone(Vector3 worldPosition, float radiusMeters = DefaultDefoliantDeadZoneRadiusMeters, float intensity = DefaultMaximumChannelIntensity)
        {
            if (!IsFiniteRuntimePosition(worldPosition) ||
                !math.isfinite(radiusMeters) ||
                radiusMeters <= 0f ||
                !math.isfinite(intensity) ||
                intensity <= 0f)
            {
                return;
            }

            float safeRadius = NormalizeChemicalRadius(radiusMeters);
            if (safeRadius <= 0f)
                return;

            float clampedIntensity = math.max(0f, intensity);
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return;

            instance.RegisterDefoliantDeadZone(worldPosition, safeRadius, clampedIntensity);
            instance.DropBreadcrumb(worldPosition, new float4(0f, 0f, 0f, -math.max(1f, clampedIntensity)), ChemicalChannel.Toxicity, safeRadius);
            instance.QueueChemicalEmitter(worldPosition, new float4(0f, 0f, 0f, -clampedIntensity), safeRadius, EmitterFlagDefoliant, HashAscii("DefoliantDeadZone"));
            RegisterChemicalTransient(worldPosition, clampedIntensity);

            DestructibleOrganicManager organicManager = null;
            WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref organicManager);
            if (organicManager != null)
                organicManager.ApplyDefoliantDeadZone(worldPosition, safeRadius);
        }

        private static bool TryResolveChemicalQueueInput(Vector3 worldPosition, float intensity, out float clampedIntensity)
        {
            clampedIntensity = 0f;
            if (!IsFiniteRuntimePosition(worldPosition) ||
                !math.isfinite(intensity) ||
                intensity <= 0f)
            {
                return false;
            }

            clampedIntensity = math.max(0f, intensity);
            return true;
        }

        internal static bool IsInsidePermanentDefoliantDeadZone(Vector3 worldPosition)
        {
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            if (instance == null || !TryResolveAupFromRuntimeOrigin(worldPosition, out double3 absolutePosition))
                return false;

            return instance != null &&
                   instance.IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(absolutePosition);
        }

        internal static bool IsInsidePermanentDefoliantDeadZoneAbsolute(Vector3 absolutePosition)
        {
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            return instance != null && instance.IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(global::Hecton8.World.AUPMath.ToDouble3(absolutePosition));
        }

        public static bool TryGetTuningSnapshot(out ChemicalTuningDTO tuning)
        {
            if (!TryGetReadableRuntime(out ChemicalInfluenceGrid instance))
            {
                tuning = default;
                return false;
            }

            NativeArray<ChemicalTuningDTO> tuningBuffer = instance.OpenChemicalVaultArray(ref instance._tuningHandle, TuningBufferId, 1);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
            {
                tuning = default;
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TrySetTuningFromEditor(float baseDiffusion, float advection, float dissipation, float qualityWeight)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            if (instance == null)
                return false;

            instance.InitializeRuntime();
            if (!instance._buffersReady)
                return false;

            NativeArray<ChemicalTuningDTO> tuningBuffer = instance.OpenChemicalVaultArray(ref instance._tuningHandle, TuningBufferId, 1);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return false;

            ChemicalTuningDTO tuning = tuningBuffer[0];
            tuning.BaseDiffusionRate = FiniteAtLeast(baseDiffusion, 0.18f, 0.001f);
            tuning.AdvectionStrength = FiniteAtLeast(advection, 0.72f, 0f);
            tuning.DissipationRate = FiniteAtLeast(dissipation, 0.028f, 0f);
            tuning.GlobalQualityWeight = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
            tuning.Revision++;
            tuningBuffer[0] = tuning;
            return true;
        }

        public static bool TryGetLatestTelemetry(out ChemicalTelemetryEntry telemetry)
        {
            ChemicalInfluenceGrid instance = _activeRuntimeInstance;
            if (instance == null || !instance._buffersReady)
            {
                telemetry = default;
                return false;
            }

            NativeArray<ChemicalTelemetryEntry> ring = instance.OpenChemicalVaultArray(ref instance._telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount);
            NativeArray<int> cursor = instance.OpenChemicalVaultArray(ref instance._telemetryCursorHandle, TelemetryCursorBufferId, 1);
            if (!ring.IsCreated || ring.Length == 0 || !cursor.IsCreated || cursor.Length == 0)
            {
                telemetry = default;
                return false;
            }

            int index = cursor[0] - 1;
            if (index < 0)
                index += ring.Length;
            if ((uint)index >= (uint)ring.Length)
                index = 0;

            telemetry = ring[index];
            return true;
        }

#if UNITY_EDITOR
        public static bool TryReloadEmitterProfilesFromDefaultPath()
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.InitializeRuntime();
            return instance.TryLoadEmitterProfilesFromCsv();
        }
#endif

        private static void RegisterChemicalTransient(Vector3 worldPosition, float intensity)
        {
            if (!IsFiniteRuntimePosition(worldPosition) ||
                !math.isfinite(intensity) ||
                intensity <= 0f)
            {
                return;
            }

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
            CacheRegistryServicesCold();
            InitializeRuntime();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InitializeRuntime();
            TryRegisterChemicalReadModel();
            TryRegisterUpdate();
            TryRegisterSlowTick();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            CompleteScheduledWorkForTeardown();
            TryUnregisterUpdate();
            TryUnregisterSlowTick();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterChemicalReadModel();
            ResetRuntimeStateForDisable();
        }

        private void OnDestroy()
        {
            CompleteScheduledWorkForTeardown();
            TryUnregisterUpdate();
            TryUnregisterSlowTick();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterChemicalReadModel();
            ResetRuntimeStateForDisable();

            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;
            }
        }

        public bool TryReadNormalizedChannels(Vector3 runtimePosition, out float4 normalizedChannels)
        {
            normalizedChannels = float4.zero;
            if (_activeRuntimeInstance != this || !_buffersReady)
                return false;

            return TrySampleNormalizedChannelsInternal(
                new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                out normalizedChannels);
        }

        public bool TryFindNearestBloodWaypoint(
            Vector3 runtimePosition,
            out float distanceMeters,
            out float intensity01)
        {
            distanceMeters = 0f;
            intensity01 = 0f;
            if (_activeRuntimeInstance != this || !_buffersReady)
                return false;

            return TryFindNearestScentWaypointInternal(
                new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                ChemicalChannel.Blood,
                out _,
                out distanceMeters,
                out intensity01);
        }

        public bool TryReadAttractantGradient(
            Vector3 runtimePosition,
            float now,
            out float bloodSignal01,
            out float exhaustSignal01,
            out float3 bloodGradient,
            out float3 exhaustGradient)
        {
            bloodSignal01 = 0f;
            exhaustSignal01 = 0f;
            bloodGradient = float3.zero;
            exhaustGradient = float3.zero;
            if (_activeRuntimeInstance != this || !_buffersReady)
                return false;

            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = OpenChemicalVaultArray(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            if (!breadcrumbs.IsCreated || _breadcrumbCount <= 0)
                return false;

            float3 center = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(center)))
                return false;

            int safeCount = math.min(_breadcrumbCount, breadcrumbs.Length);
            float3 bloodGradientWeighted = float3.zero;
            float3 exhaustGradientWeighted = float3.zero;
            float bloodWeight = 0f;
            float exhaustWeight = 0f;
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[i];
                if (waypoint.ExpiresAt <= now || waypoint.RadiusMeters <= 0f)
                    continue;

                float radius = NormalizeChemicalRadius(waypoint.RadiusMeters);
                if (radius <= 0f)
                    continue;

                float3 delta = waypoint.RuntimePosition - center;
                float distanceSq = math.lengthsq(delta);
                float radiusSq = math.max(1f, radius * radius);
                if (!math.isfinite(distanceSq) || distanceSq > radiusSq)
                    continue;

                float falloff = SmoothStep01(1f - math.saturate(distanceSq * math.rcp(radiusSq)));
                float blood = math.saturate(waypoint.Channels.x * falloff);
                float exhaust = math.saturate(waypoint.Channels.y * falloff);
                float3 direction = distanceSq > 0.000001f
                    ? delta * math.rsqrt(math.max(distanceSq, 0.000001f))
                    : float3.zero;

                bloodSignal01 = math.max(bloodSignal01, blood);
                exhaustSignal01 = math.max(exhaustSignal01, exhaust);
                if (blood > 0.0001f)
                {
                    bloodGradientWeighted += direction * blood;
                    bloodWeight += blood;
                }

                if (exhaust > 0.0001f)
                {
                    exhaustGradientWeighted += direction * exhaust;
                    exhaustWeight += exhaust;
                }
            }

            if (bloodWeight > 0f)
                bloodGradient = NormalizeOrZero(bloodGradientWeighted * math.rcp(math.max(bloodWeight, 0.0001f)));
            if (exhaustWeight > 0f)
                exhaustGradient = NormalizeOrZero(exhaustGradientWeighted * math.rcp(math.max(exhaustWeight, 0.0001f)));

            return bloodSignal01 > 0.0001f || exhaustSignal01 > 0.0001f;
        }

        public void Tick(float deltaTime)
        {
            InitializeRuntime();
            if (!_buffersReady || _activeRuntimeInstance != this)
                return;

            TryFinalizeScheduledWork();
            int frame = ResolveDeterministicFrameId(true);
            int frameStride = ResolveFrameStride(ResolveGlobalQualityWeight());
            if (_hasScheduledWork || frame - _lastScheduledFrame < frameStride)
                return;

            CollectPersistentRuntimeEmissions();
            ScheduleSimulation(frame);
        }

        public void SlowTick()
        {
            InitializeRuntime();
            int frame = ResolveDeterministicFrameId(true);
            PublishFrame(frame);
            PruneExpiredBreadcrumbs(ResolveSimulationSeconds(frame));
            RefreshRuntimePositions();
            UpdateDebugState();
        }

        public void LateFrameTick()
        {
            if (_pendingDuplicateDestroy)
            {
                _pendingDuplicateDestroy = false;
                if (_activeRuntimeInstance != this)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            TryFinalizeScheduledWork();
        }

        private void EnsureSingletonOwnership()
        {
            if (_activeRuntimeInstance != null && _activeRuntimeInstance != this)
            {
                _pendingDuplicateDestroy = true;
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
            breadcrumbDropIntervalSeconds = FiniteAtLeast(breadcrumbDropIntervalSeconds, 5f, 0.25f);
            breadcrumbLifetimeSeconds = FiniteAtLeast(breadcrumbLifetimeSeconds, 90f, 1f);
            breadcrumbRadiusMeters = math.max(1f, NormalizeChemicalRadius(FiniteAtLeast(breadcrumbRadiusMeters, DefaultBreadcrumbRadiusMeters, 1f)));
            maximumChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
            baseDiffusionRate = FiniteAtLeast(baseDiffusionRate, 0.18f, 0.001f);
            advectionStrength = FiniteAtLeast(advectionStrength, 0.72f, 0f);
            dissipationRate = FiniteAtLeast(dissipationRate, 0.028f, 0f);

            _runtimeInitialized = true;
            ValidateStructLayouts();
            _buffersReady = TryInitializeVaultBuffers();
            if (_buffersReady)
            {
                InitializeGridOrigin(ResolveFocusAup());
                InitializeTuningBuffer();
                InitializeDefaultEmitterProfiles();
#if UNITY_EDITOR
                TryLoadEmitterProfilesFromCsv();
#endif
                UpdateDebugState();
            }
        }

        private bool TryResolveDataVault()
        {
            return _dataVault != null;
        }

        private bool TryInitializeVaultBuffers()
        {
            if (!TryResolveDataVault())
                return false;

            NativeArray<ChemicalCellDTO> frontCells = default;
            NativeArray<ChemicalCellDTO> backCells = default;
            NativeArray<float4> publishedGrid = default;
            NativeArray<float4> overlayGrid = default;
            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = default;
            NativeArray<ChemicalEmitterDTO> pendingEmitters = default;
            NativeArray<int> pendingEmitterCount = default;
            NativeArray<ChemicalEmitterDTO> activeEmitters = default;
            NativeArray<int> activeEmitterCount = default;
            NativeArray<ChemicalEmitterDTO> mockEmitters = default;
            NativeArray<int> mockEmitterCount = default;
            NativeArray<ChemicalTelemetryEntry> telemetryRing = default;
            NativeArray<int> telemetryCursor = default;
            NativeArray<ChemicalAtomicCounterDTO> counters = default;
            NativeArray<ChemicalDefoliantZoneDTO> defoliantZones = default;
            NativeArray<ChemicalEmitterProfileDTO> profileTable = default;
            NativeArray<int> profileCount = default;
            bool created =
                OpenOrAcquireChemicalVaultBuffer(ref _frontCellHandle, GridFrontBufferId, ChemicalCellCount, NativeArrayOptions.UninitializedMemory, out frontCells) &&
                OpenOrAcquireChemicalVaultBuffer(ref _backCellHandle, GridBackBufferId, ChemicalCellCount, NativeArrayOptions.UninitializedMemory, out backCells) &&
                OpenOrAcquireChemicalVaultBuffer(ref _publishedGridHandle, PublishedGridBufferId, ChemicalCellCount, NativeArrayOptions.UninitializedMemory, out publishedGrid) &&
                OpenOrAcquireChemicalVaultBuffer(ref _overlayGridHandle, OverlayGridBufferId, ChemicalCellCount, NativeArrayOptions.UninitializedMemory, out overlayGrid) &&
                OpenOrAcquireChemicalVaultBuffer(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity, NativeArrayOptions.UninitializedMemory, out breadcrumbs) &&
                OpenOrAcquireChemicalVaultBuffer(ref _pendingEmitterHandle, PendingEmitterBufferId, MaxActiveEmitters, NativeArrayOptions.UninitializedMemory, out pendingEmitters) &&
                OpenOrAcquireChemicalVaultBuffer(ref _pendingEmitterCountHandle, PendingEmitterCountBufferId, 1, NativeArrayOptions.UninitializedMemory, out pendingEmitterCount) &&
                OpenOrAcquireChemicalVaultBuffer(ref _activeEmitterHandle, ActiveEmitterBufferId, MaxActiveEmitters, NativeArrayOptions.UninitializedMemory, out activeEmitters) &&
                OpenOrAcquireChemicalVaultBuffer(ref _activeEmitterCountHandle, ActiveEmitterCountBufferId, 1, NativeArrayOptions.UninitializedMemory, out activeEmitterCount) &&
                OpenOrAcquireChemicalVaultBuffer(ref _mockEmitterHandle, MockEmitterBufferId, MaxMockEmitters, NativeArrayOptions.UninitializedMemory, out mockEmitters) &&
                OpenOrAcquireChemicalVaultBuffer(ref _mockEmitterCountHandle, MockEmitterCountBufferId, 1, NativeArrayOptions.UninitializedMemory, out mockEmitterCount) &&
                OpenOrAcquireChemicalVaultBuffer(ref _tuningHandle, TuningBufferId, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireChemicalVaultBuffer(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out telemetryRing) &&
                OpenOrAcquireChemicalVaultBuffer(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1, NativeArrayOptions.UninitializedMemory, out telemetryCursor) &&
                OpenOrAcquireChemicalVaultBuffer(ref _atomicCounterHandle, AtomicCounterBufferId, 1, NativeArrayOptions.UninitializedMemory, out counters) &&
                OpenOrAcquireChemicalVaultBuffer(ref _defoliantZoneHandle, DefoliantZoneBufferId, MaxDefoliantDeadZones, NativeArrayOptions.UninitializedMemory, out defoliantZones) &&
                OpenOrAcquireChemicalVaultBuffer(ref _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireChemicalVaultBuffer(ref _profileTableHandle, EmitterProfileTableBufferId, ProfileTableCapacity, NativeArrayOptions.UninitializedMemory, out profileTable) &&
                OpenOrAcquireChemicalVaultBuffer(ref _profileCountHandle, EmitterProfileCountBufferId, 1, NativeArrayOptions.UninitializedMemory, out profileCount);
            if (!created)
                return false;

            ColdZeroVaultBuffersJob zeroJob = new ColdZeroVaultBuffersJob
            {
                FrontCells = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(frontCells),
                BackCells = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(backCells),
                PublishedGrid = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(publishedGrid),
                OverlayGrid = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(overlayGrid),
                Breadcrumbs = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(breadcrumbs),
                PendingEmitters = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(pendingEmitters),
                ActiveEmitters = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeEmitters),
                MockEmitters = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockEmitters),
                TelemetryRing = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryRing),
                DefoliantZones = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(defoliantZones),
                ProfileTable = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(profileTable),
                Counters = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters),
                PendingCount = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(pendingEmitterCount),
                ActiveCount = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeEmitterCount),
                MockCount = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockEmitterCount),
                TelemetryCursor = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryCursor),
                ProfileCount = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(profileCount),
                FrontCellsBytes = ChemicalCellCount * UnsafeUtility.SizeOf<ChemicalCellDTO>(),
                BackCellsBytes = ChemicalCellCount * UnsafeUtility.SizeOf<ChemicalCellDTO>(),
                PublishedBytes = ChemicalCellCount * UnsafeUtility.SizeOf<float4>(),
                OverlayBytes = ChemicalCellCount * UnsafeUtility.SizeOf<float4>(),
                BreadcrumbBytes = DefaultBreadcrumbCapacity * UnsafeUtility.SizeOf<ChemicalBreadcrumbWaypoint>(),
                PendingEmitterBytes = MaxActiveEmitters * UnsafeUtility.SizeOf<ChemicalEmitterDTO>(),
                ActiveEmitterBytes = MaxActiveEmitters * UnsafeUtility.SizeOf<ChemicalEmitterDTO>(),
                MockEmitterBytes = MaxMockEmitters * UnsafeUtility.SizeOf<ChemicalEmitterDTO>(),
                TelemetryBytes = TelemetryFrameCount * UnsafeUtility.SizeOf<ChemicalTelemetryEntry>(),
                DefoliantBytes = MaxDefoliantDeadZones * UnsafeUtility.SizeOf<ChemicalDefoliantZoneDTO>(),
                ProfileBytes = ProfileTableCapacity * UnsafeUtility.SizeOf<ChemicalEmitterProfileDTO>(),
                CounterBytes = UnsafeUtility.SizeOf<ChemicalAtomicCounterDTO>(),
                CountBytes = UnsafeUtility.SizeOf<int>()
            };
            JobHandle zeroHandle = zeroJob.Schedule();
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobSwap.TryComplete(ref zeroHandle, true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            return true;
        }

        private bool OpenOrAcquireChemicalVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (OpenChemicalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return OpenChemicalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AISensory,
                options);
            return OpenChemicalVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private NativeArray<T> OpenChemicalVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return OpenChemicalVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool OpenChemicalVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsChemicalVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsChemicalVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.AISensory &&
                   handle.Generation != 0u;
        }

        private bool TryOpenExistingVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != (uint)bufferId ||
                handle.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void InitializeTuningBuffer()
        {
            NativeArray<ChemicalTuningDTO> tuningBuffer = OpenChemicalVaultArray(ref _tuningHandle, TuningBufferId, 1);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            ChemicalTuningDTO tuning = tuningBuffer[0];
            if (tuning.Revision != 0u &&
                math.isfinite(tuning.BaseDiffusionRate) &&
                math.isfinite(tuning.AdvectionStrength) &&
                math.isfinite(tuning.DissipationRate))
            {
                return;
            }

            tuningBuffer[0] = CreateDefaultTuning();
        }

        private ChemicalTuningDTO CreateDefaultTuning()
        {
            ChemicalTuningDTO tuning = default;
            tuning.SimulationTickDelta = DefaultSimulationTickDelta;
            tuning.BaseDiffusionRate = FiniteAtLeast(baseDiffusionRate, 0.18f, 0.001f);
            tuning.AdvectionStrength = FiniteAtLeast(advectionStrength, 0.72f, 0f);
            tuning.DissipationRate = FiniteAtLeast(dissipationRate, 0.028f, 0f);
            tuning.EmitterRadiusScale = 1f;
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.MaxChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
            tuning.Revision = 1u;
            tuning.Flags = 0u;
            tuning.Iterations = ResolveJacobiIterations(tuning.GlobalQualityWeight);
            tuning.CellSizeMeters = DefaultCellSizeMeters;
            return tuning;
        }

        private void InitializeDefaultEmitterProfiles()
        {
            NativeArray<ChemicalEmitterProfileDTO> profiles = OpenChemicalVaultArray(ref _profileTableHandle, EmitterProfileTableBufferId, ProfileTableCapacity);
            NativeArray<int> count = OpenChemicalVaultArray(ref _profileCountHandle, EmitterProfileCountBufferId, 1);
            if (!profiles.IsCreated || profiles.Length < 5 || !count.IsCreated || count.Length == 0)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int written = 0;
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("PlayerBleeding"), 1.25f, 0f, 0f, 1.1f, 1f, EmitterFlagBlood));
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("ExhaustTrail"), 0f, 1f, 0f, 1.35f, 0.8f, EmitterFlagPheromone));
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("FearPheromone"), 0f, 1.2f, 0f, 0.85f, 1.1f, EmitterFlagPheromone));
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("ToxinBurst"), 0f, 0f, 1f, 1f, 0.9f, EmitterFlagToxin));
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("DefoliantBurst"), 0f, 0f, 1.1f, 1.2f, 0.7f, EmitterFlagDefoliant));
            WriteProfile(profiles, ref written, CreateProfile(HashAscii("DefoliantDeadZone"), 0f, 0f, 1.4f, 1.45f, 0.6f, EmitterFlagDefoliant));
            count[0] = written;
        }

        private static ChemicalEmitterProfileDTO CreateProfile(uint hash, float blood, float pheromone, float toxin, float radius, float dissipation, uint flags)
        {
            ChemicalEmitterProfileDTO profile = default;
            profile.ProfileHash = hash;
            profile.BloodMultiplier = blood;
            profile.PheromoneMultiplier = pheromone;
            profile.ToxinMultiplier = toxin;
            profile.RadiusMultiplier = FiniteAtLeast(radius, 1f, 0.001f);
            profile.DissipationMultiplier = FiniteAtLeast(dissipation, 1f, 0.001f);
            profile.Flags = flags;
            profile.SourceHash = ChemicalSourceHash;
            return profile;
        }

        private static void WriteProfile(NativeArray<ChemicalEmitterProfileDTO> profiles, ref int written, ChemicalEmitterProfileDTO profile)
        {
            if (!profiles.IsCreated || profiles.Length == 0 || profile.ProfileHash == 0u)
                return;

            int capacity = profiles.Length;
            int slot = (int)(profile.ProfileHash % (uint)capacity);
            for (int i = 0; i < capacity; i++)
            {
                int index = (slot + i) % capacity;
                ChemicalEmitterProfileDTO existing = profiles[index];
                if (existing.ProfileHash != 0u && existing.ProfileHash != profile.ProfileHash)
                    continue;

                if (existing.ProfileHash == 0u)
                    written = math.min(capacity, written + 1);

                profiles[index] = profile;
                return;
            }
        }

#if UNITY_EDITOR
        private bool TryLoadEmitterProfilesFromCsv()
        {
            if (!_buffersReady)
                return false;

            NativeArray<byte> scratch = OpenChemicalVaultArray(ref _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes);
            NativeArray<ChemicalEmitterProfileDTO> profiles = OpenChemicalVaultArray(ref _profileTableHandle, EmitterProfileTableBufferId, ProfileTableCapacity);
            NativeArray<int> count = OpenChemicalVaultArray(ref _profileCountHandle, EmitterProfileCountBufferId, 1);
            if (!scratch.IsCreated || !profiles.IsCreated || !count.IsCreated || count.Length == 0)
                return false;

            string path = Path.Combine(Application.dataPath, ProfileCsvRelativePath);
            if (!File.Exists(path))
                return false;

            int bytesRead = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int maxBytes = math.min(scratch.Length, (int)math.min(stream.Length, scratch.Length));
                    void* scratchPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                    if (scratchPtr == null || maxBytes <= 0)
                        return false;

                    Span<byte> span = new Span<byte>(scratchPtr, maxBytes);
                    bytesRead = stream.Read(span);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (bytesRead <= 0)
                return false;

            void* csvPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(csvPtr, bytesRead);
            int parsed = ParseEmitterProfilesCsv(csv, profiles);
            if (parsed > 0)
            {
                count[0] = parsed;
                return true;
            }

            return false;
        }

        private static int ParseEmitterProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<ChemicalEmitterProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return 0;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int cursor = 0;
            int written = 0;
            bool headerSkipped = false;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!headerSkipped && StartsWithAscii(line, "profile"))
                {
                    headerSkipped = true;
                    continue;
                }

                headerSkipped = true;
                if (TryParseProfileLine(line, out ChemicalEmitterProfileDTO profile))
                    WriteProfile(profiles, ref written, profile);
            }

            return written;
        }

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out ChemicalEmitterProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> token;
            int cursor = 0;
            if (!TryReadCsvToken(line, ref cursor, out token))
                return false;

            uint profileHash = HashBytes(Trim(token));
            float blood = 1f;
            float pheromone = 1f;
            float toxin = 1f;
            float radius = 1f;
            float dissipation = 1f;
            uint flags = 0u;

            if (TryReadCsvToken(line, ref cursor, out token) && TryParseFloat(Trim(token), out float parsedBlood))
                blood = parsedBlood;
            if (TryReadCsvToken(line, ref cursor, out token) && TryParseFloat(Trim(token), out float parsedPheromone))
                pheromone = parsedPheromone;
            if (TryReadCsvToken(line, ref cursor, out token) && TryParseFloat(Trim(token), out float parsedToxin))
                toxin = parsedToxin;
            if (TryReadCsvToken(line, ref cursor, out token) && TryParseFloat(Trim(token), out float parsedRadius))
                radius = parsedRadius;
            if (TryReadCsvToken(line, ref cursor, out token) && TryParseFloat(Trim(token), out float parsedDissipation))
                dissipation = parsedDissipation;
            if (TryReadCsvToken(line, ref cursor, out token) && TryParseUInt(Trim(token), out uint parsedFlags))
                flags = parsedFlags;

            profile = CreateProfile(profileHash, blood, pheromone, toxin, radius, dissipation, flags);
            return profileHash != 0u;
        }
#endif

        private void InitializeGridOrigin(double3 focusAup)
        {
            float cellSize = ResolveCellSizeMeters();
            int3 centerCell = AupToCell(focusAup, cellSize);
            _gridOriginCell = centerCell - GridHalfExtents;
            _gridOriginAup = CellToAup(_gridOriginCell, cellSize);
            _scheduledOriginCell = _gridOriginCell;
            _scheduledOriginAup = _gridOriginAup;
            _publishedRuntimeOrigin = ToFloat3(_gridOriginAup - HectonFloatingOrigin.CurrentTotalOffsetDouble);
            _scheduledRuntimeOrigin = _publishedRuntimeOrigin;
            _gridHasOrigin = true;
        }

        private void PublishFrame(int frameId)
        {
            InitializeRuntime();
            if (_activeRuntimeInstance != this)
                return;
            if (_publishedFrameId == frameId)
                return;

            TryFinalizeScheduledWork();
            _publishedFrameId = frameId;
            PruneExpiredBreadcrumbs(ResolveSimulationSeconds(frameId));
            RefreshRuntimePositions();
        }

        private bool TryGetPublishedSnapshotInternal(
            out NativeArray<float4>.ReadOnly frontGrid,
            out NativeArray<float4>.ReadOnly overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            NativeArray<float4> mutableFrontGrid = OpenChemicalVaultArray(ref _publishedGridHandle, PublishedGridBufferId, ChemicalCellCount);
            NativeArray<float4> mutableOverlayGrid = OpenChemicalVaultArray(ref _overlayGridHandle, OverlayGridBufferId, ChemicalCellCount);
            frontGrid = mutableFrontGrid.IsCreated ? mutableFrontGrid.AsReadOnly() : default;
            overlayGrid = mutableOverlayGrid.IsCreated ? mutableOverlayGrid.AsReadOnly() : default;
            dimensions = GridDimensions;
            origin = _publishedRuntimeOrigin;
            cellSize = new float3(ResolveCellSizeMeters());
            return frontGrid.IsCreated && overlayGrid.IsCreated && frontGrid.Length >= ChemicalCellCount && overlayGrid.Length >= ChemicalCellCount;
        }

        private void CollectPersistentRuntimeEmissions()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            var playerSurvival = playerContext != null ? playerContext.SurvivalSystem : null;
            if (playerTransform != null &&
                playerSurvival != null &&
                playerSurvival.IsBleeding)
            {
                QueueChemicalEmitter(playerTransform.position, new float4(1f, 0f, 0f, 0f), breadcrumbRadiusMeters, EmitterFlagBlood, HashAscii("PlayerBleeding"));
                DropBreadcrumb(playerTransform.position, new float4(1f, 0f, 0f, 0f), ChemicalChannel.Blood);
            }

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            if (submarine != null &&
                submarine.PlatformTransform != null &&
                submarine.HullRigidbody != null &&
                submarine.HullRigidbody.linearVelocity.sqrMagnitude >= MinimumSubmarineVelocitySqr)
            {
                QueueChemicalEmitter(submarine.PlatformTransform.position, new float4(0f, 1f, 0f, 0f), breadcrumbRadiusMeters, EmitterFlagPheromone, HashAscii("ExhaustTrail"));
                DropBreadcrumb(submarine.PlatformTransform.position, new float4(0f, 1f, 0f, 0f), ChemicalChannel.Exhaust);
            }
        }

        private void QueueChemicalEmitter(Vector3 worldPosition, float4 channels, float radiusMeters, uint flags, uint profileHash)
        {
            InitializeRuntime();
            if (!_buffersReady)
                return;

            NativeArray<ChemicalEmitterDTO> pending = OpenChemicalVaultArray(ref _pendingEmitterHandle, PendingEmitterBufferId, MaxActiveEmitters);
            NativeArray<int> pendingCount = OpenChemicalVaultArray(ref _pendingEmitterCountHandle, PendingEmitterCountBufferId, 1);
            if (!pending.IsCreated || pending.Length == 0 || !pendingCount.IsCreated || pendingCount.Length == 0)
                return;

            float4 clamped = ClampChemicalChannels(channels, maximumChannelIntensity);
            if (math.lengthsq(clamped) <= 0f)
                return;

            ChemicalEmitterProfileDTO profile = ResolveEmitterProfile(profileHash);
            float4 scaledChannels = new float4(
                clamped.x * math.max(0f, profile.BloodMultiplier),
                clamped.y * math.max(0f, profile.PheromoneMultiplier),
                clamped.z * math.max(0f, profile.PheromoneMultiplier),
                clamped.w * math.max(0f, profile.ToxinMultiplier));

            int count = math.clamp(pendingCount[0], 0, pending.Length);
            int index = count < pending.Length ? count : _pendingEmitterWriteCursor % pending.Length;
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out double3 emitterAup))
                return;

            float radiusScale = FiniteAtLeast(profile.RadiusMultiplier, 1f, 0.001f);
            float safeRadius = NormalizeChemicalRadius(radiusMeters * radiusScale);
            if (safeRadius <= 0f)
                return;

            pending[index] = new ChemicalEmitterDTO
            {
                Aup = emitterAup,
                Channels = scaledChannels,
                RadiusMeters = safeRadius,
                LifetimeSeconds = ChemicalTransientLifetimeSeconds * FiniteAtLeast(profile.DissipationMultiplier, 1f, 0.001f),
                ProfileHash = profileHash,
                Flags = flags | profile.Flags,
                SpawnFrame = _simulationFrameCounter,
                SourceHash = ChemicalSourceHash
            };

            if (count < pending.Length)
                pendingCount[0] = count + 1;

            _pendingEmitterWriteCursor = (index + 1) % pending.Length;
        }

        private ChemicalEmitterProfileDTO ResolveEmitterProfile(uint profileHash)
        {
            NativeArray<ChemicalEmitterProfileDTO> profiles = OpenChemicalVaultArray(ref _profileTableHandle, EmitterProfileTableBufferId, ProfileTableCapacity);
            if (profileHash == 0u || !profiles.IsCreated || profiles.Length == 0)
                return CreateProfile(profileHash, 1f, 1f, 1f, 1f, 1f, 0u);

            int capacity = profiles.Length;
            int slot = (int)(profileHash % (uint)capacity);
            for (int i = 0; i < capacity; i++)
            {
                ChemicalEmitterProfileDTO profile = profiles[(slot + i) % capacity];
                if (profile.ProfileHash == profileHash)
                    return profile;
                if (profile.ProfileHash == 0u)
                    break;
            }

            return CreateProfile(profileHash, 1f, 1f, 1f, 1f, 1f, 0u);
        }

        private void DropBreadcrumb(Vector3 worldPosition, float4 channels, ChemicalChannel primaryChannel, float radiusOverrideMeters = 0f)
        {
            InitializeRuntime();
            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = OpenChemicalVaultArray(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            if (!breadcrumbs.IsCreated)
                return;

            float now = ResolveSimulationSeconds();
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out double3 absolutePosition))
                return;

            float3 absolute = ToFloat3(absolutePosition - HectonFloatingOrigin.CurrentTotalOffsetDouble);
            float safeRadius = NormalizeChemicalRadius(radiusOverrideMeters > 0f ? radiusOverrideMeters : breadcrumbRadiusMeters);
            if (safeRadius <= 0f)
                safeRadius = DefaultBreadcrumbRadiusMeters;

            int mergeIndex = FindMergeCandidate(breadcrumbs, absolutePosition, primaryChannel, now);
            float4 clampedChannels = ClampChemicalChannels(channels, maximumChannelIntensity);
            if (mergeIndex >= 0)
            {
                ChemicalBreadcrumbWaypoint merged = breadcrumbs[mergeIndex];
                merged.AbsolutePosition = absolute;
                merged.AbsolutePositionDouble = absolutePosition;
                merged.RuntimePosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
                merged.Channels = ClampChemicalChannels(merged.Channels + clampedChannels, maximumChannelIntensity);
                merged.RadiusMeters = math.max(NormalizeChemicalRadius(merged.RadiusMeters), safeRadius);
                merged.SpawnTime = now;
                merged.ExpiresAt = now + breadcrumbLifetimeSeconds;
                breadcrumbs[mergeIndex] = merged;
                _debugLastBreadcrumbPosition = worldPosition;
                return;
            }

            int writeIndex = ResolveWriteIndex(breadcrumbs, now);
            breadcrumbs[writeIndex] = new ChemicalBreadcrumbWaypoint
            {
                AbsolutePosition = absolute,
                AbsolutePositionDouble = absolutePosition,
                RuntimePosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                Channels = clampedChannels,
                RadiusMeters = safeRadius,
                SpawnTime = now,
                ExpiresAt = now + breadcrumbLifetimeSeconds
            };

            if (_breadcrumbCount < breadcrumbs.Length)
                _breadcrumbCount++;

            _breadcrumbWriteCursor = (_breadcrumbWriteCursor + 1) % breadcrumbs.Length;
            _debugLastBreadcrumbPosition = worldPosition;
        }

        private void ScheduleSimulation(int frame)
        {
            if (!_buffersReady || _hasScheduledWork)
                return;

            double3 focusAup = ResolveFocusAup();
            float quality = ResolveGlobalQualityWeight();
            NativeArray<ChemicalTuningDTO> tuningBuffer = OpenChemicalVaultArray(ref _tuningHandle, TuningBufferId, 1);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            ChemicalTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuning.Iterations = ResolveJacobiIterations(quality);
            tuning.CellSizeMeters = ResolveCellSizeMeters();
            tuning.MaxChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
            tuningBuffer[0] = tuning;
            float cellSize = math.max(GridSampleEpsilon, tuning.CellSizeMeters);
            int3 targetCenterCell = AupToCell(focusAup, cellSize);
            int3 targetOriginCell = targetCenterCell - GridHalfExtents;
            if (!_gridHasOrigin)
                InitializeGridOrigin(focusAup);

            int3 shift = targetOriginCell - _gridOriginCell;
            int shiftMagnitude = math.abs(shift.x) + math.abs(shift.y) + math.abs(shift.z);
            _scheduledOriginCell = targetOriginCell;
            _scheduledOriginAup = CellToAup(targetOriginCell, cellSize);
            _scheduledRuntimeOrigin = ToFloat3(_scheduledOriginAup - HectonFloatingOrigin.CurrentTotalOffsetDouble);
            _scheduledGridShiftManhattan = shiftMagnitude;
            _simulationFrameCounter++;
            uint simulationFrame = _simulationFrameCounter;

            if (!TryLockSimulationBuffers())
                return;

            bool keepSimulationGuard = false;
            try
            {
            NativeArray<ChemicalCellDTO> frontCells = OpenChemicalVaultArray(ref _frontCellHandle, GridFrontBufferId, ChemicalCellCount);
            NativeArray<ChemicalCellDTO> backCells = OpenChemicalVaultArray(ref _backCellHandle, GridBackBufferId, ChemicalCellCount);
            NativeArray<float4> publishedGrid = OpenChemicalVaultArray(ref _publishedGridHandle, PublishedGridBufferId, ChemicalCellCount);
            NativeArray<float4> overlayGrid = OpenChemicalVaultArray(ref _overlayGridHandle, OverlayGridBufferId, ChemicalCellCount);
            NativeArray<ChemicalEmitterDTO> pendingEmitters = OpenChemicalVaultArray(ref _pendingEmitterHandle, PendingEmitterBufferId, MaxActiveEmitters);
            NativeArray<int> pendingEmitterCount = OpenChemicalVaultArray(ref _pendingEmitterCountHandle, PendingEmitterCountBufferId, 1);
            NativeArray<ChemicalEmitterDTO> activeEmitters = OpenChemicalVaultArray(ref _activeEmitterHandle, ActiveEmitterBufferId, MaxActiveEmitters);
            NativeArray<int> activeEmitterCount = OpenChemicalVaultArray(ref _activeEmitterCountHandle, ActiveEmitterCountBufferId, 1);
            NativeArray<ChemicalEmitterDTO> mockEmitters = OpenChemicalVaultArray(ref _mockEmitterHandle, MockEmitterBufferId, MaxMockEmitters);
            NativeArray<int> mockEmitterCount = OpenChemicalVaultArray(ref _mockEmitterCountHandle, MockEmitterCountBufferId, 1);
            NativeArray<ChemicalTelemetryEntry> telemetryRing = OpenChemicalVaultArray(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount);
            NativeArray<int> telemetryCursor = OpenChemicalVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1);
            NativeArray<ChemicalAtomicCounterDTO> counters = OpenChemicalVaultArray(ref _atomicCounterHandle, AtomicCounterBufferId, 1);
            NativeArray<ChemicalDefoliantZoneDTO> zones = OpenChemicalVaultArray(ref _defoliantZoneHandle, DefoliantZoneBufferId, MaxDefoliantDeadZones);
            if (!frontCells.IsCreated || !backCells.IsCreated || !publishedGrid.IsCreated || !overlayGrid.IsCreated ||
                !pendingEmitters.IsCreated || !pendingEmitterCount.IsCreated || !activeEmitters.IsCreated || !activeEmitterCount.IsCreated ||
                !mockEmitters.IsCreated || !mockEmitterCount.IsCreated || !telemetryRing.IsCreated || !telemetryCursor.IsCreated ||
                !counters.IsCreated || !zones.IsCreated)
            {
                UnlockSimulationBuffers();
                return;
            }

            void* frontPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(frontCells);
            void* backPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(backCells);
            void* publishedPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(publishedGrid);
            void* overlayPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(overlayGrid);
            void* pendingPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(pendingEmitters);
            void* pendingCountPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(pendingEmitterCount);
            void* activePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeEmitters);
            void* activeCountPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(activeEmitterCount);
            void* mockPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockEmitters);
            void* mockCountPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockEmitterCount);
            void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryRing);
            void* telemetryCursorPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryCursor);
            void* counterPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
            void* zonesPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(zones);
            NativeArray<byte>.ReadOnly sdf = default;
            int sdfLength = 0;
            if (TryOpenExistingVaultBuffer(BufferID.VoxelSdfTexture3D, 1, out sdf))
            {
                sdfLength = sdf.Length;
            }

            JobHandle dependency = default;
            if (shiftMagnitude > 0)
            {
                ShiftChemicalGridJob shiftJob = new ShiftChemicalGridJob
                {
                    Source = (ChemicalCellDTO*)frontPtr,
                    Destination = (ChemicalCellDTO*)backPtr,
                    Dimensions = GridDimensions,
                    ShiftCells = shift,
                    CellCount = ChemicalCellCount
                };
                dependency = shiftJob.Schedule(dependency);

                CopyChemicalGridJob copyShifted = new CopyChemicalGridJob
                {
                    Source = (ChemicalCellDTO*)backPtr,
                    Destination = (ChemicalCellDTO*)frontPtr,
                    CellCount = ChemicalCellCount
                };
                dependency = copyShifted.Schedule(ChemicalCellCount, 128, dependency);
            }

            PrepareChemicalFrameJob prepareJob = new PrepareChemicalFrameJob
            {
                Counters = (ChemicalAtomicCounterDTO*)counterPtr
            };
            dependency = prepareJob.Schedule(dependency);

            CommitPendingEmittersJob commitJob = new CommitPendingEmittersJob
            {
                PendingEmitters = (ChemicalEmitterDTO*)pendingPtr,
                PendingCount = (int*)pendingCountPtr,
                ActiveEmitters = (ChemicalEmitterDTO*)activePtr,
                ActiveCount = (int*)activeCountPtr,
                Capacity = MaxActiveEmitters
            };
            dependency = commitJob.Schedule(dependency);

            GenerateMockScentSourcesJob mockJob = new GenerateMockScentSourcesJob
            {
                MockEmitters = (ChemicalEmitterDTO*)mockPtr,
                MockCount = (int*)mockCountPtr,
                FocusAup = focusAup,
                SimulationFrame = simulationFrame,
                SectorHash = HashAupCell(targetCenterCell),
                GlobalQualityWeight = quality,
                MaxMockEmitters = MaxMockEmitters
            };
            dependency = mockJob.Schedule(dependency);

            ChemicalInjectionJob injectionJob = new ChemicalInjectionJob
            {
                Grid = (ChemicalCellDTO*)frontPtr,
                ActiveEmitters = (ChemicalEmitterDTO*)activePtr,
                ActiveCount = (int*)activeCountPtr,
                MockEmitters = (ChemicalEmitterDTO*)mockPtr,
                MockCount = (int*)mockCountPtr,
                Counters = (ChemicalAtomicCounterDTO*)counterPtr,
                GridOriginAup = _scheduledOriginAup,
                Dimensions = GridDimensions,
                CellSizeMeters = cellSize,
                MaxChannelIntensity = FiniteAtLeast(tuning.MaxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f),
                EmitterRadiusScale = math.lerp(0.55f, math.max(0.001f, tuning.EmitterRadiusScale), Smooth01(quality)),
                ActiveCapacity = MaxActiveEmitters,
                MockCapacity = MaxMockEmitters
            };
            dependency = injectionJob.Schedule(MaxActiveEmitters + MaxMockEmitters, 1, dependency);

            int iterations = ResolveJacobiIterations(quality);
            ChemicalCellDTO* readPtr = (ChemicalCellDTO*)frontPtr;
            ChemicalCellDTO* writePtr = (ChemicalCellDTO*)backPtr;
            bool finalIsBack = false;
            for (int i = 0; i < iterations; i++)
            {
                ChemicalDiffusionSolverJob solverJob = new ChemicalDiffusionSolverJob
                {
                    Source = readPtr,
                    Destination = writePtr,
                    Sdf = sdf,
                    Counters = (ChemicalAtomicCounterDTO*)counterPtr,
                    Dimensions = GridDimensions,
                    CellCount = ChemicalCellCount,
                    SdfLength = sdfLength,
                    IterationIndex = i,
                    GridOriginAup = _scheduledOriginAup,
                    CellSizeMeters = cellSize,
                    SimulationTickDelta = (float)math.max(DefaultSimulationTickDelta, tuning.SimulationTickDelta),
                    BaseDiffusionRate = math.max(0f, tuning.BaseDiffusionRate),
                    AdvectionStrength = math.max(0f, tuning.AdvectionStrength),
                    DissipationRate = math.max(0f, tuning.DissipationRate),
                    GlobalQualityWeight = quality,
                    SectorHash = HashAupCell(targetCenterCell),
                    TotalIterations = iterations
                };
                dependency = solverJob.Schedule(ChemicalCellCount, 128, dependency);

                ChemicalCellDTO* tmp = readPtr;
                readPtr = writePtr;
                writePtr = tmp;
                finalIsBack = !finalIsBack;
            }

            ChemicalPublishGridJob publishJob = new ChemicalPublishGridJob
            {
                Source = readPtr,
                Published = (float4*)publishedPtr,
                Overlay = (float4*)overlayPtr,
                Counters = (ChemicalAtomicCounterDTO*)counterPtr,
                DefoliantZones = (ChemicalDefoliantZoneDTO*)zonesPtr,
                Dimensions = GridDimensions,
                CellCount = ChemicalCellCount,
                DefoliantZoneCount = _defoliantDeadZoneCount,
                GridOriginAup = _scheduledOriginAup,
                CellSizeMeters = cellSize,
                MaxChannelIntensity = FiniteAtLeast(tuning.MaxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f)
            };
            dependency = publishJob.Schedule(ChemicalCellCount, 128, dependency);

            int telemetryIndex = 0;
            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
            {
                telemetryIndex = telemetryCursor[0];
                if ((uint)telemetryIndex >= (uint)TelemetryFrameCount)
                    telemetryIndex = 0;
            }

            ChemicalTelemetryWriteJob telemetryJob = new ChemicalTelemetryWriteJob
            {
                Telemetry = (ChemicalTelemetryEntry*)telemetryPtr,
                TelemetryCursor = (int*)telemetryCursorPtr,
                Counters = (ChemicalAtomicCounterDTO*)counterPtr,
                GridOriginAup = _scheduledOriginAup,
                Frame = simulationFrame,
                GlobalQualityWeight = quality,
                GridShiftManhattan = shiftMagnitude,
                TelemetryCapacity = TelemetryFrameCount
            };
            dependency = telemetryJob.Schedule(dependency);

            _scheduledTelemetryIndex = telemetryIndex;
            _scheduledSwapAfterFinalize = finalIsBack;
            _scheduledHandle = dependency;
            _hasScheduledWork = true;
            _scheduledStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastScheduledFrame = frame;
            _scheduledBuffersLocked = true;
            keepSimulationGuard = true;
            H8Memory.RegisterActiveJob(SystemID.AISensory, _scheduledHandle);
            }
            finally
            {
                if (!keepSimulationGuard)
                    UnlockSimulationBuffers();
            }
        }

        private void TryFinalizeScheduledWork()
        {
            if (!_hasScheduledWork)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _scheduledHandle))
                return;

            FinishScheduledWorkCompletion();
        }

        private void CompleteScheduledWorkForTeardown()
        {
            if (!_hasScheduledWork)
                return;

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                if (!DispatcherJobSwap.TryComplete(ref _scheduledHandle, true))
                    return;
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            FinishScheduledWorkCompletion();
        }

        private void FinishScheduledWorkCompletion()
        {
            if (_scheduledSwapAfterFinalize)
            {
                VaultGenerationHandle<ChemicalCellDTO> temp = _frontCellHandle;
                _frontCellHandle = _backCellHandle;
                _backCellHandle = temp;
            }

            _gridOriginCell = _scheduledOriginCell;
            _gridOriginAup = _scheduledOriginAup;
            _publishedRuntimeOrigin = _scheduledRuntimeOrigin;
            _gridHasOrigin = true;
            _hasScheduledWork = false;
            _scheduledSwapAfterFinalize = false;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _scheduledStartTicks;
            float micros = (float)(elapsedTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            try
            {
                PatchTelemetryAfterCompletion(micros);
                RefreshDebugCountersFromVault();
            }
            finally
            {
                UnlockSimulationBuffers();
            }
        }

        private void PatchTelemetryAfterCompletion(float solverMicros)
        {
            NativeArray<ChemicalTelemetryEntry> telemetry = OpenChemicalVaultArray(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount);
            NativeArray<ChemicalAtomicCounterDTO> counters = OpenChemicalVaultArray(ref _atomicCounterHandle, AtomicCounterBufferId, 1);
            if (!telemetry.IsCreated || telemetry.Length == 0 || (uint)_scheduledTelemetryIndex >= (uint)telemetry.Length)
                return;

            ChemicalTelemetryEntry entry = telemetry[_scheduledTelemetryIndex];
            entry.SolverMicros = math.max(0f, math.isfinite(solverMicros) ? solverMicros : 0f);
            telemetry[_scheduledTelemetryIndex] = entry;

            if (counters.IsCreated && counters.Length > 0 && counters[0].NaNFlag != 0)
                DumpTelemetryRing();
        }

        private void RefreshDebugCountersFromVault()
        {
            NativeArray<ChemicalAtomicCounterDTO> counters = OpenChemicalVaultArray(ref _atomicCounterHandle, AtomicCounterBufferId, 1);
            if (!counters.IsCreated || counters.Length == 0)
                return;

            ChemicalAtomicCounterDTO counter = counters[0];
            _debugActiveEmitterCount = math.max(0, counter.ActiveEmitterCount);
            _debugMockEmitterCount = math.max(0, counter.MockEmitterCount);
            _debugJacobiIterations = math.max(0, counter.JacobiIterations);
            _debugMaxBlood = math.max(0f, math.asfloat(counter.MaxBloodBits));
            _debugScentGridActiveCellCount = math.max(0, counter.ActiveCellCount);

            NativeArray<ChemicalTelemetryEntry> telemetry = OpenChemicalVaultArray(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount);
            if (telemetry.IsCreated && telemetry.Length > 0 && (uint)_scheduledTelemetryIndex < (uint)telemetry.Length)
                _debugLastSolverMicros = telemetry[_scheduledTelemetryIndex].SolverMicros;
        }

        private void UpdateDebugState()
        {
            RefreshDebugCountersFromVault();
            _debugBreadcrumbCount = math.max(0, _breadcrumbCount);
            _debugPendingWriteCount = math.max(0, _pendingEmitterWriteCursor);
        }

        private bool TrySampleScentGrid01Internal(Vector3 worldPosition, out float scent01)
        {
            bool sampled = TrySampleNormalizedChannelsInternal(new float3(worldPosition.x, worldPosition.y, worldPosition.z), out float4 channels);
            scent01 = sampled ? channels.x : 0f;
            return sampled && scent01 > 0f;
        }

        private bool TrySampleNormalizedChannelsInternal(float3 worldPosition, out float4 normalizedChannels)
        {
            normalizedChannels = float4.zero;
            if (!_buffersReady || !_gridHasOrigin)
                return false;

            NativeArray<float4> published = OpenChemicalVaultArray(ref _publishedGridHandle, PublishedGridBufferId, ChemicalCellCount);
            NativeArray<float4> overlay = OpenChemicalVaultArray(ref _overlayGridHandle, OverlayGridBufferId, ChemicalCellCount);
            if (!published.IsCreated || published.Length < ChemicalCellCount)
                return false;

            Vector3 runtimePosition = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out double3 absolute))
                return false;

            bool insideDeadZone = IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(absolute);
            float3 local = ToFloat3(absolute - _gridOriginAup);
            float cellSize = ResolveCellSizeMeters();
            float3 grid = local * math.rcp(math.max(GridSampleEpsilon, cellSize));
            if (grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                grid.x > GridAxisX - 1 || grid.y > GridAxisY - 1 || grid.z > GridAxisZ - 1)
            {
                if (!insideDeadZone)
                    return false;

                normalizedChannels = new float4(0f, 0f, 0f, -1f);
                return true;
            }

            float quality = ResolveGlobalQualityWeight();
            float sampleBlend = Smooth01(quality);
            float4 nearest = SamplePublishedNearest(published, grid);
            float4 trilinear = sampleBlend > 0f ? SamplePublishedTrilinear(published, grid) : nearest;
            float4 sampledChannels = math.lerp(nearest, trilinear, sampleBlend);
            if (overlay.IsCreated && overlay.Length >= ChemicalCellCount)
            {
                float4 overlaySample = sampleBlend > 0f ? SamplePublishedTrilinear(overlay, grid) : SamplePublishedNearest(overlay, grid);
                sampledChannels.w = math.min(sampledChannels.w, overlaySample.w);
            }

            if (insideDeadZone)
                sampledChannels.w = -1f;

            normalizedChannels = new float4(
                math.saturate(sampledChannels.x),
                math.saturate(sampledChannels.y),
                math.saturate(sampledChannels.z),
                math.clamp(sampledChannels.w, -1f, 1f));
            return math.any(math.abs(normalizedChannels) > new float4(0.0001f));
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

            if (TrySampleNormalizedChannelsInternal(worldPosition, out float4 channels))
            {
                intensity01 = math.saturate(math.abs(GetChannel(channels, (int)channel)));
                if (intensity01 > 0f)
                {
                    Vector3 runtimePosition = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
                    if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out double3 aup))
                        return false;

                    nearestWaypoint = new ChemicalBreadcrumbWaypoint
                    {
                        AbsolutePosition = worldPosition,
                        AbsolutePositionDouble = aup,
                        RuntimePosition = worldPosition,
                        Channels = channels * maximumChannelIntensity,
                        RadiusMeters = ResolveCellSizeMeters() * 2f,
                        SpawnTime = ResolveSimulationSeconds(),
                        ExpiresAt = ResolveSimulationSeconds() + (float)DefaultSimulationTickDelta
                    };
                    return true;
                }
            }

            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = OpenChemicalVaultArray(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            if (!breadcrumbs.IsCreated || _breadcrumbCount <= 0)
                return false;

            Vector3 runtime = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!TryResolveAupFromRuntimeOrigin(runtime, out double3 queryAbsolute))
                return false;

            int channelIndex = (int)channel;
            int safeCount = math.min(_breadcrumbCount, breadcrumbs.Length);
            float now = ResolveSimulationSeconds();
            double bestDistanceSq = double.MaxValue;
            float bestIntensity = 0f;
            bool found = false;

            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[i];
                if (waypoint.ExpiresAt <= now || waypoint.RadiusMeters <= GridSampleEpsilon)
                    continue;

                float channelSignal = GetChannel(waypoint.Channels, channelIndex);
                if (channelSignal <= 0f)
                    continue;

                float radius = NormalizeChemicalRadius(waypoint.RadiusMeters);
                if (radius <= 0f)
                    continue;

                double distanceSq = math.lengthsq(ResolveWaypointAbsolutePositionDouble(in waypoint) - queryAbsolute);
                double radiusSq = (double)radius * radius;
                if (distanceSq > radiusSq || distanceSq >= bestDistanceSq)
                    continue;

                float distanceSq01 = math.saturate((float)(distanceSq / math.max(radiusSq, 0.0001d)));
                float safeMaximumChannelIntensity = FiniteAtLeast(maximumChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
                bestIntensity = math.saturate(channelSignal * SmoothStep01(1f - distanceSq01) / safeMaximumChannelIntensity);
                bestDistanceSq = distanceSq;
                nearestWaypoint = waypoint;
                found = true;
            }

            if (!found)
                return false;

            distanceMeters = bestDistanceSq > 0d ? (float)math.sqrt(bestDistanceSq) : 0f;
            intensity01 = bestIntensity;
            return true;
        }

        private int FindMergeCandidate(NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs, double3 absolutePosition, ChemicalChannel primaryChannel, float now)
        {
            int safeCount = math.min(_breadcrumbCount, breadcrumbs.Length);
            double mergeDistanceSq = (double)BreadcrumbMergeDistanceMeters * BreadcrumbMergeDistanceMeters;
            int channelIndex = (int)primaryChannel;
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[i];
                if (waypoint.ExpiresAt <= now)
                    continue;

                if (math.abs(GetChannel(waypoint.Channels, channelIndex)) <= 0f)
                    continue;

                double3 delta = ResolveWaypointAbsolutePositionDouble(in waypoint) - absolutePosition;
                if (now - waypoint.SpawnTime < breadcrumbDropIntervalSeconds &&
                    math.lengthsq(delta) <= mergeDistanceSq)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveWriteIndex(NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs, float now)
        {
            if (_breadcrumbCount < breadcrumbs.Length)
                return _breadcrumbCount;

            int safeLength = breadcrumbs.Length;
            for (int i = 0; i < safeLength; i++)
            {
                int index = (_breadcrumbWriteCursor + i) % safeLength;
                if (breadcrumbs[index].ExpiresAt <= now)
                    return index;
            }

            return _breadcrumbWriteCursor;
        }

        private void PruneExpiredBreadcrumbs(float now)
        {
            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = OpenChemicalVaultArray(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            if (!breadcrumbs.IsCreated || _breadcrumbCount <= 0)
                return;

            int write = 0;
            int safeCount = math.min(_breadcrumbCount, breadcrumbs.Length);
            for (int read = 0; read < safeCount; read++)
            {
                ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[read];
                if (waypoint.ExpiresAt <= now)
                    continue;

                if (write != read)
                    breadcrumbs[write] = waypoint;
                write++;
            }

            for (int i = write; i < safeCount; i++)
                breadcrumbs[i] = default;

            _breadcrumbCount = write;
            if (breadcrumbs.Length > 0)
                _breadcrumbWriteCursor = write % breadcrumbs.Length;
        }

        private void RefreshRuntimePositions()
        {
            NativeArray<ChemicalBreadcrumbWaypoint> breadcrumbs = OpenChemicalVaultArray(ref _breadcrumbsHandle, BreadcrumbBufferId, DefaultBreadcrumbCapacity);
            if (!breadcrumbs.IsCreated)
                return;

            int safeCount = math.min(_breadcrumbCount, breadcrumbs.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalBreadcrumbWaypoint waypoint = breadcrumbs[i];
                double3 local = ResolveWaypointAbsolutePositionDouble(in waypoint) - HectonFloatingOrigin.CurrentTotalOffsetDouble;
                waypoint.RuntimePosition = ToFloat3(local);
                breadcrumbs[i] = waypoint;
            }
        }

        private void RegisterDefoliantDeadZone(Vector3 worldPosition, float radiusMeters, float intensity)
        {
            InitializeRuntime();
            NativeArray<ChemicalDefoliantZoneDTO> zones = OpenChemicalVaultArray(ref _defoliantZoneHandle, DefoliantZoneBufferId, MaxDefoliantDeadZones);
            if (!zones.IsCreated || zones.Length == 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out double3 absolutePosition))
                return;

            float safeRadius = NormalizeChemicalRadius(radiusMeters);
            if (safeRadius <= 0f)
                return;

            double mergeRadiusSq = (double)safeRadius * safeRadius;
            int safeCount = math.min(_defoliantDeadZoneCount, zones.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalDefoliantZoneDTO zone = zones[i];
                if (math.lengthsq(zone.CenterAup - absolutePosition) > mergeRadiusSq)
                    continue;

                zone.CenterAup = (zone.CenterAup + absolutePosition) * 0.5d;
                zone.RadiusMeters = math.max(NormalizeChemicalRadius(zone.RadiusMeters), safeRadius);
                zone.Intensity = math.max(zone.Intensity, intensity);
                zone.Flags |= EmitterFlagDefoliant;
                zones[i] = zone;
                return;
            }

            int writeIndex = _defoliantDeadZoneCount < zones.Length
                ? _defoliantDeadZoneCount++
                : zones.Length - 1;
            zones[writeIndex] = new ChemicalDefoliantZoneDTO
            {
                CenterAup = absolutePosition,
                RadiusMeters = safeRadius,
                Intensity = math.max(0f, intensity),
                Flags = EmitterFlagDefoliant,
                SourceHash = ChemicalSourceHash
            };
        }

        private bool IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(Vector3 absolutePosition)
        {
            return IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(global::Hecton8.World.AUPMath.ToDouble3(absolutePosition));
        }

        private bool IsInsidePermanentDefoliantDeadZoneAbsoluteInternal(double3 absolutePosition)
        {
            NativeArray<ChemicalDefoliantZoneDTO> zones = OpenChemicalVaultArray(ref _defoliantZoneHandle, DefoliantZoneBufferId, MaxDefoliantDeadZones);
            if (!zones.IsCreated)
                return false;

            int safeCount = math.min(_defoliantDeadZoneCount, zones.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChemicalDefoliantZoneDTO zone = zones[i];
                float radius = NormalizeChemicalRadius(zone.RadiusMeters);
                if (radius <= 0f)
                    continue;

                double radiusSq = (double)radius * radius;
                if (math.lengthsq(absolutePosition - zone.CenterAup) <= radiusSq)
                    return true;
            }

            return false;
        }

        private bool TryLockSimulationBuffers()
        {
            if (_scheduledBuffersLocked)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireMutationGuard(SimulationMutationGuardMask))
            {
                return false;
            }

            _scheduledBuffersGuardVault = vault;
            _scheduledBuffersLocked = true;
            return true;
        }

        private void UnlockSimulationBuffers()
        {
            if (!_scheduledBuffersLocked)
                return;

            IDataVault vault = _scheduledBuffersGuardVault;
            _scheduledBuffersGuardVault = null;
            _scheduledBuffersLocked = false;
            vault?.ReleaseMutationGuard(SimulationMutationGuardMask);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void DumpTelemetryRing()
        {
            NativeArray<ChemicalTelemetryEntry> ring = OpenChemicalVaultArray(ref _telemetryRingHandle, TelemetryRingBufferId, TelemetryFrameCount);
            NativeArray<int> cursor = OpenChemicalVaultArray(ref _telemetryCursorHandle, TelemetryCursorBufferId, 1);
            if (!ring.IsCreated || ring.Length == 0)
                return;

            string root = ResolveProjectRoot();
            string path = Path.Combine(root, DumpRelativePath);
            NativeArray<byte> payload = default;
            try
            {
                int rowBytes = UnsafeUtility.SizeOf<ChemicalTelemetryEntry>();
                int headerBytes = 20;
                int totalBytes = headerBytes + ring.Length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    VaultOwnerName,
                    TelemetryDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteUInt64LittleEndian(bytes, 0, ChemicalDumpMagic);
                WriteInt32LittleEndian(bytes, 8, 1);
                WriteInt32LittleEndian(bytes, 12, ring.Length);
                WriteInt32LittleEndian(bytes, 16, rowBytes);
                int start = cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0;
                if ((uint)start >= (uint)ring.Length)
                    start = 0;

                int writeOffset = headerBytes;
                for (int i = 0; i < ring.Length; i++)
                {
                    int index = start + i;
                    if (index >= ring.Length)
                        index -= ring.Length;

                    ChemicalTelemetryEntry entry = ring[index];
                    WriteChemicalTelemetryEntry(bytes.Slice(writeOffset, rowBytes), in entry);
                    writeOffset += rowBytes;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    VaultOwnerName,
                    TelemetryDumpPayloadLabel);
            }
        }

        private static void WriteChemicalTelemetryEntry(Span<byte> destination, in ChemicalTelemetryEntry entry)
        {
            WriteDoubleLittleEndian(destination, 0, entry.GridOriginAup.x);
            WriteDoubleLittleEndian(destination, 8, entry.GridOriginAup.y);
            WriteDoubleLittleEndian(destination, 16, entry.GridOriginAup.z);
            WriteSingleLittleEndian(destination, 24, entry.MaxBlood);
            WriteSingleLittleEndian(destination, 28, entry.SolverMicros);
            WriteUInt32LittleEndian(destination, 32, entry.Frame);
            WriteInt32LittleEndian(destination, 36, entry.ActiveEmitters);
            WriteInt32LittleEndian(destination, 40, entry.MockEmitters);
            WriteInt32LittleEndian(destination, 44, entry.Iterations);
            WriteUInt32LittleEndian(destination, 48, entry.StateHash);
            WriteUInt32LittleEndian(destination, 52, entry.Flags);
            WriteSingleLittleEndian(destination, 56, entry.GlobalQualityWeight);
            WriteInt32LittleEndian(destination, 60, entry.GridShiftManhattan);
        }

        private static void WriteDoubleLittleEndian(Span<byte> destination, int offset, double value)
        {
            WriteUInt64LittleEndian(destination, offset, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
        }

        private static void WriteUInt64LittleEndian(Span<byte> destination, int offset, ulong value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _activeRuntimeInstance != this || !_buffersReady)
                return;

            NativeArray<float4> published = OpenChemicalVaultArray(ref _publishedGridHandle, PublishedGridBufferId, ChemicalCellCount);
            if (!published.IsCreated || published.Length < ChemicalCellCount)
                return;

            int y = GridAxisY / 2;
            float cellSize = ResolveCellSizeMeters();
            Vector3 size = new Vector3(cellSize, 0.02f, cellSize);
            float debugQuality01 = Smooth01(ResolveGlobalQualityWeight());
            int step = math.clamp((int)math.round(math.lerp(4f, 2f, debugQuality01)), 2, 4);
            for (int z = 0; z < GridAxisZ; z += step)
            {
                for (int x = 0; x < GridAxisX; x += step)
                {
                    int index = ToGridIndex(x, y, z);
                    float4 sample = published[index];
                    float signal = math.saturate(sample.x + sample.y * 0.35f + math.abs(sample.w) * 0.2f);
                    if (signal <= 0.02f)
                        continue;

                    float3 local = new float3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, (z + 0.5f) * cellSize);
                    Vector3 center = new Vector3(
                        _publishedRuntimeOrigin.x + local.x,
                        _publishedRuntimeOrigin.y + local.y,
                        _publishedRuntimeOrigin.z + local.z);
                    Gizmos.color = new Color(math.saturate(sample.x + math.abs(sample.w)), math.saturate(sample.y), math.saturate(sample.z), math.saturate(signal * 0.45f));
                    Gizmos.DrawCube(center, size);
                }
            }
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = false;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void CacheRegistryServicesCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_submarineRuntimeContext == null)
                _submarineRuntimeContext = GlobalRegistry.Submarine;
        }

        private void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            CompleteScheduledWorkForTeardown();
            UnlockSimulationBuffers();
            ResetVaultStateForRebind();
            _dataVault = currentVault;
            if (_dataVault != null && isActiveAndEnabled)
                InitializeRuntime();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryRegisterChemicalReadModel()
        {
            if (_registeredReadModel || _activeRuntimeInstance != this)
                return;

            GlobalRegistry.RegisterChemicalInfluenceReadModel(this);
            _registeredReadModel = ReferenceEquals(GlobalRegistry.ChemicalInfluence, this);
        }

        private void TryUnregisterChemicalReadModel()
        {
            if (!_registeredReadModel)
                return;

            GlobalRegistry.UnregisterChemicalInfluenceReadModel(this);
            _registeredReadModel = false;
        }

        private void ResetRuntimeStateForDisable()
        {
            ResetVaultStateForRebind();
            _dataVault = null;
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
        }

        private void ResetVaultStateForRebind()
        {
            UnlockSimulationBuffers();
            ReleaseVaultHandles(_dataVault);
            _breadcrumbCount = 0;
            _breadcrumbWriteCursor = 0;
            _pendingEmitterWriteCursor = 0;
            _defoliantDeadZoneCount = 0;
            _publishedFrameId = -1;
            _debugScentGridActiveCellCount = 0;
            _runtimeInitialized = false;
            _buffersReady = false;
            _gridHasOrigin = false;
            _scheduledBuffersGuardVault = null;
            _scheduledBuffersLocked = false;
            _hasScheduledWork = false;
            _scheduledSwapAfterFinalize = false;
            _scheduledTelemetryIndex = -1;
            ClearVaultHandles();
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _frontCellHandle);
            ReleaseVaultHandle(vault, ref _backCellHandle);
            ReleaseVaultHandle(vault, ref _publishedGridHandle);
            ReleaseVaultHandle(vault, ref _overlayGridHandle);
            ReleaseVaultHandle(vault, ref _breadcrumbsHandle);
            ReleaseVaultHandle(vault, ref _pendingEmitterHandle);
            ReleaseVaultHandle(vault, ref _pendingEmitterCountHandle);
            ReleaseVaultHandle(vault, ref _activeEmitterHandle);
            ReleaseVaultHandle(vault, ref _activeEmitterCountHandle);
            ReleaseVaultHandle(vault, ref _mockEmitterHandle);
            ReleaseVaultHandle(vault, ref _mockEmitterCountHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _atomicCounterHandle);
            ReleaseVaultHandle(vault, ref _defoliantZoneHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _profileTableHandle);
            ReleaseVaultHandle(vault, ref _profileCountHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.AISensory)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ClearVaultHandles()
        {
            _frontCellHandle = default;
            _backCellHandle = default;
            _publishedGridHandle = default;
            _overlayGridHandle = default;
            _breadcrumbsHandle = default;
            _pendingEmitterHandle = default;
            _pendingEmitterCountHandle = default;
            _activeEmitterHandle = default;
            _activeEmitterCountHandle = default;
            _mockEmitterHandle = default;
            _mockEmitterCountHandle = default;
            _tuningHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _atomicCounterHandle = default;
            _defoliantZoneHandle = default;
            _csvScratchHandle = default;
            _profileTableHandle = default;
            _profileCountHandle = default;
        }

        private double3 ResolveFocusAup()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                double3 playerAup = movementState.PredictedAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(playerAup)))
                    return playerAup;
            }

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            if (submarine != null && submarine.PlatformTransform != null)
            {
                Vector3 runtimePosition = submarine.PlatformTransform.position;
                if (TryResolveAupFromRuntimeOrigin(runtimePosition, out double3 submarineAup))
                    return submarineAup;
            }

            if (_gridHasOrigin)
            {
                float cellSize = ResolveCellSizeMeters();
                return _gridOriginAup + new double3(
                    GridHalfExtents.x * cellSize,
                    GridHalfExtents.y * cellSize,
                    GridHalfExtents.z * cellSize);
            }

            return HectonFloatingOrigin.CurrentTotalOffsetDouble;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
            {
                return false;
            }

            float rx = runtimePosition.x == 0f ? 0.0f : runtimePosition.x;
            float ry = runtimePosition.y == 0f ? 0.0f : runtimePosition.y;
            float rz = runtimePosition.z == 0f ? 0.0f : runtimePosition.z;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(rx, ry, rz));
            if (!resolvedAup.IsFinite())
                return false;

            aup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(aup));
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private int ResolveDeterministicFrameId(bool advanceFallback)
        {
            int arenaFrame = HectonArenaAllocator.CurrentFrameSequence;
            if (arenaFrame > 0)
                return arenaFrame;

            if (!advanceFallback)
                return _publishedFrameId >= 0 ? _publishedFrameId : 0;

            int next = _publishedFrameId + 1;
            return next > 0 ? next : 0;
        }

        private float ResolveSimulationSeconds()
        {
            return ResolveSimulationSeconds(ResolveDeterministicFrameId(false));
        }

        private float ResolveSimulationSeconds(int frameId)
        {
            double tick = DefaultSimulationTickDelta;
            if (_buffersReady && _dataVault != null)
            {
                NativeArray<ChemicalTuningDTO> tuningBuffer = OpenChemicalVaultArray(ref _tuningHandle, TuningBufferId, 1);
                if (tuningBuffer.IsCreated &&
                    tuningBuffer.Length > 0 &&
                    math.isfinite(tuningBuffer[0].SimulationTickDelta) &&
                    tuningBuffer[0].SimulationTickDelta > 0d)
                {
                    ChemicalTuningDTO tuning = tuningBuffer[0];
                    tick = tuning.SimulationTickDelta;
                }
            }

            double safeFrame = math.max(0, frameId);
            return (float)(safeFrame * math.max(DefaultSimulationTickDelta, tick));
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private int ResolveFrameStride(float quality)
        {
            float q = Smooth01(math.saturate(quality));
            float stride = math.lerp(12f, 1f, q);
            return math.max(1, (int)math.round(stride));
        }

        private static int ResolveJacobiIterations(float quality)
        {
            return math.clamp((int)math.lerp(1f, 6f, math.saturate(quality)), 1, 6);
        }

        private float ResolveCellSizeMeters()
        {
            if (_buffersReady)
            {
                NativeArray<ChemicalTuningDTO> tuningBuffer = OpenChemicalVaultArray(ref _tuningHandle, TuningBufferId, 1);
                if (tuningBuffer.IsCreated &&
                    tuningBuffer.Length > 0 &&
                    math.isfinite(tuningBuffer[0].CellSizeMeters) &&
                    tuningBuffer[0].CellSizeMeters > GridSampleEpsilon)
                {
                    ChemicalTuningDTO tuning = tuningBuffer[0];
                    return tuning.CellSizeMeters;
                }
            }

            return DefaultCellSizeMeters;
        }

        private static float4 SamplePublishedNearest(NativeArray<float4> grid, float3 gridPosition)
        {
            int x = math.clamp((int)math.round(gridPosition.x), 0, GridAxisX - 1);
            int y = math.clamp((int)math.round(gridPosition.y), 0, GridAxisY - 1);
            int z = math.clamp((int)math.round(gridPosition.z), 0, GridAxisZ - 1);
            return grid[ToGridIndex(x, y, z)];
        }

        private static float4 SamplePublishedTrilinear(NativeArray<float4> grid, float3 gridPosition)
        {
            double3 clampedD = math.clamp(new double3(gridPosition.x, gridPosition.y, gridPosition.z), double3.zero, new double3(GridAxisX - 1, GridAxisY - 1, GridAxisZ - 1));
            int3 p0 = new int3(FastFloorToInt(clampedD.x), FastFloorToInt(clampedD.y), FastFloorToInt(clampedD.z));
            int3 p1 = math.min(p0 + new int3(1), new int3(GridAxisX - 1, GridAxisY - 1, GridAxisZ - 1));
            float3 t = new float3((float)(clampedD.x - p0.x), (float)(clampedD.y - p0.y), (float)(clampedD.z - p0.z));
            float4 c000 = grid[ToGridIndex(p0.x, p0.y, p0.z)];
            float4 c100 = grid[ToGridIndex(p1.x, p0.y, p0.z)];
            float4 c010 = grid[ToGridIndex(p0.x, p1.y, p0.z)];
            float4 c110 = grid[ToGridIndex(p1.x, p1.y, p0.z)];
            float4 c001 = grid[ToGridIndex(p0.x, p0.y, p1.z)];
            float4 c101 = grid[ToGridIndex(p1.x, p0.y, p1.z)];
            float4 c011 = grid[ToGridIndex(p0.x, p1.y, p1.z)];
            float4 c111 = grid[ToGridIndex(p1.x, p1.y, p1.z)];
            float4 c00 = math.lerp(c000, c100, t.x);
            float4 c10 = math.lerp(c010, c110, t.x);
            float4 c01 = math.lerp(c001, c101, t.x);
            float4 c11 = math.lerp(c011, c111, t.x);
            float4 c0 = math.lerp(c00, c10, t.y);
            float4 c1 = math.lerp(c01, c11, t.y);
            return math.lerp(c0, c1, t.z);
        }

        private static int ToGridIndex(int x, int y, int z)
        {
            int cx = math.clamp(x, 0, GridAxisX - 1);
            int cy = math.clamp(y, 0, GridAxisY - 1);
            int cz = math.clamp(z, 0, GridAxisZ - 1);
            return cx + cz * GridAxisX + cy * GridSliceStride;
        }

        private static int3 AupToCell(double3 aup, float cellSize)
        {
            double inverseCell = 1.0d / math.max(GridSampleEpsilon, cellSize);
            return new int3(
                FastFloorToInt(aup.x * inverseCell),
                FastFloorToInt(aup.y * inverseCell),
                FastFloorToInt(aup.z * inverseCell));
        }

        private static double3 CellToAup(int3 cell, float cellSize)
        {
            return new double3(
                (double)cell.x * cellSize,
                (double)cell.y * cellSize,
                (double)cell.z * cellSize);
        }

        private static double3 ToDouble3(Vector3 value)
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
            {
                return waypoint.AbsolutePositionDouble;
            }

            return new double3(waypoint.AbsolutePosition.x, waypoint.AbsolutePosition.y, waypoint.AbsolutePosition.z);
        }

        private static float4 ClampChemicalChannels(float4 value, float maxChannelIntensity)
        {
            float safeMax = FiniteAtLeast(maxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
            return new float4(
                math.clamp(value.x, 0f, safeMax),
                math.clamp(value.y, 0f, safeMax),
                math.clamp(value.z, 0f, safeMax),
                math.clamp(value.w, -safeMax, safeMax));
        }

        private static float NormalizeChemicalRadius(float radiusMeters)
        {
            if (!math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0f;

            return math.clamp(radiusMeters, MinimumRadiusMeters, MaxChemicalRadiusMeters);
        }

        private static float FiniteAtLeast(float value, float fallback, float minimum)
        {
            float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
            float safeValue = math.select(safeFallback, value, math.isfinite(value));
            return math.max(minimum, safeValue);
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

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static float SmoothStep01(float value)
        {
            return Smooth01(value);
        }

        private static float3 NormalizeOrZero(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(math.max(lengthSq, 0.000001f))
                : float3.zero;
        }

        private static uint HashAupCell(int3 cell)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)cell.x) * 16777619u;
            hash = (hash ^ (uint)cell.y) * 16777619u;
            hash = (hash ^ (uint)cell.z) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static uint HashAscii(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                byte b = (byte)text[i];
                hash = (hash ^ b) * 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

#if UNITY_EDITOR
        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> source, ref int cursor, out ReadOnlySpan<byte> line)
        {
            if (cursor >= source.Length)
            {
                line = default;
                return false;
            }

            int start = cursor;
            while (cursor < source.Length && source[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < source.Length && source[cursor] == (byte)'\n')
                cursor++;
            if (end > start && source[end - 1] == (byte)'\r')
                end--;

            line = source.Slice(start, end - start);
            return true;
        }

        private static bool TryReadCsvToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            if (cursor > line.Length)
            {
                token = default;
                return false;
            }

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            token = line.Slice(start, end - start);
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> value, string prefix)
        {
            if (value.Length < prefix.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                byte a = ToLowerAscii(value[i]);
                byte b = ToLowerAscii((byte)prefix[i]);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            if (token.Length == 0)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                value = value * 10u + (uint)(b - (byte)'0');
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < token.Length)
            {
                byte b = token[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length)
                {
                    byte b = token[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        return false;

                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (integer + fraction * math.rcp(math.max(divisor, 0.0001f)));
            return math.isfinite(value);
        }
#endif

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : dataPath;
        }

        private void ValidateStructLayouts()
        {
            bool valid =
                UnsafeUtility.SizeOf<ChemicalCellDTO>() == 16 &&
#if UNITY_EDITOR
                GetFieldOffset<ChemicalCellDTO>(nameof(ChemicalCellDTO.BloodConcentration)) == 0 &&
                GetFieldOffset<ChemicalCellDTO>(nameof(ChemicalCellDTO.PheromoneConcentration)) == 4 &&
                GetFieldOffset<ChemicalCellDTO>(nameof(ChemicalCellDTO.ToxinConcentration)) == 8 &&
                GetFieldOffset<ChemicalCellDTO>(nameof(ChemicalCellDTO.Flags)) == 12 &&
#endif
                UnsafeUtility.SizeOf<ChemicalEmitterDTO>() == 64 &&
                UnsafeUtility.SizeOf<ChemicalTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ChemicalAtomicCounterDTO>() == 64 &&
                UnsafeUtility.SizeOf<ChemicalDefoliantZoneDTO>() == 64 &&
                UnsafeUtility.SizeOf<ChemicalTuningDTO>() == 64 &&
                UnsafeUtility.SizeOf<ChemicalEmitterProfileDTO>() == 64;

            if (!valid)
                Hecton8.Core.H8Debug.LogError("[SHINOBU_138] Chemical DTO layout validation failed. ARM64 padding contract broken.");
        }

#if UNITY_EDITOR
        private static int GetFieldOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ColdZeroVaultBuffersJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* FrontCells;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* BackCells;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* PublishedGrid;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* OverlayGrid;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* Breadcrumbs;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* PendingEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* ActiveEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* MockEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* TelemetryRing;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* DefoliantZones;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* ProfileTable;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* Counters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* PendingCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* ActiveCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* MockCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* TelemetryCursor;
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* ProfileCount;
            public int FrontCellsBytes;
            public int BackCellsBytes;
            public int PublishedBytes;
            public int OverlayBytes;
            public int BreadcrumbBytes;
            public int PendingEmitterBytes;
            public int ActiveEmitterBytes;
            public int MockEmitterBytes;
            public int TelemetryBytes;
            public int DefoliantBytes;
            public int ProfileBytes;
            public int CounterBytes;
            public int CountBytes;

            public void Execute()
            {
                Clear(FrontCells, FrontCellsBytes);
                Clear(BackCells, BackCellsBytes);
                Clear(PublishedGrid, PublishedBytes);
                Clear(OverlayGrid, OverlayBytes);
                Clear(Breadcrumbs, BreadcrumbBytes);
                Clear(PendingEmitters, PendingEmitterBytes);
                Clear(ActiveEmitters, ActiveEmitterBytes);
                Clear(MockEmitters, MockEmitterBytes);
                Clear(TelemetryRing, TelemetryBytes);
                Clear(DefoliantZones, DefoliantBytes);
                Clear(ProfileTable, ProfileBytes);
                Clear(Counters, CounterBytes);
                Clear(PendingCount, CountBytes);
                Clear(ActiveCount, CountBytes);
                Clear(MockCount, CountBytes);
                Clear(TelemetryCursor, CountBytes);
                Clear(ProfileCount, CountBytes);
            }

            private static void Clear(byte* ptr, int bytes)
            {
                if (ptr != null && bytes > 0)
                    UnsafeUtility.MemClear(ptr, bytes);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct PrepareChemicalFrameJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalAtomicCounterDTO* Counters;

            public void Execute()
            {
                if (Counters == null)
                    return;

                Counters->MaxBloodBits = 0;
                Counters->ActiveEmitterCount = 0;
                Counters->MockEmitterCount = 0;
                Counters->JacobiIterations = 0;
                Counters->NaNFlag = 0;
                Counters->StateHash = 0;
                Counters->ActiveCellCount = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CommitPendingEmittersJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalEmitterDTO* PendingEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* PendingCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalEmitterDTO* ActiveEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveCount;
            public int Capacity;

            public void Execute()
            {
                if (PendingEmitters == null || PendingCount == null || ActiveEmitters == null || ActiveCount == null || Capacity <= 0)
                    return;

                int count = math.clamp(PendingCount[0], 0, Capacity);
                if (count > 0)
                    UnsafeUtility.MemMove(ActiveEmitters, PendingEmitters, count * UnsafeUtility.SizeOf<ChemicalEmitterDTO>());
                ActiveCount[0] = count;
                PendingCount[0] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockScentSourcesJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalEmitterDTO* MockEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* MockCount;
            public double3 FocusAup;
            public uint SimulationFrame;
            public uint SectorHash;
            public float GlobalQualityWeight;
            public int MaxMockEmitters;

            public void Execute()
            {
                if (MockEmitters == null || MockCount == null || MaxMockEmitters <= 0)
                    return;

                float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                int targetCount = math.clamp((int)math.lerp(1f, 3f, Smooth01(q)), 1, math.min(MaxMockEmitters, 3));
                uint seed = (SectorHash ^ (SimulationFrame * 747796405u) ^ 0x9E3779B9u);
                if (seed == 0u)
                    seed = 1u;

                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);
                for (int i = 0; i < targetCount; i++)
                {
                    float angle = (i + rng.NextFloat(0.05f, 0.95f)) * 2.094395102f;
                    float radius = math.lerp(18f, 55f, q) + rng.NextFloat(-4f, 4f);
                    MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
                    double3 offset = new double3(angleCos * radius, rng.NextFloat(-6f, 6f), angleSin * radius);
                    float lane = i == 0 ? 0.12f : i == 1 ? 0.08f : 0.05f;
                    MockEmitters[i] = new ChemicalEmitterDTO
                    {
                        Aup = FocusAup + offset,
                        Channels = new float4(lane * (0.8f + q), lane * 0.35f, lane * 0.18f, 0f),
                        RadiusMeters = math.lerp(18f, 36f, q),
                        LifetimeSeconds = 8f,
                        ProfileHash = HashAsciiBurst((byte)'M', (byte)'O', (byte)'C', (byte)'K'),
                        Flags = EmitterFlagMock | EmitterFlagBlood | EmitterFlagPheromone,
                        SpawnFrame = SimulationFrame,
                        SourceHash = ChemicalSourceHash
                    };
                }

                for (int i = targetCount; i < MaxMockEmitters; i++)
                    MockEmitters[i] = default;

                MockCount[0] = targetCount;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ChemicalInjectionJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Grid;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalEmitterDTO* ActiveEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* ActiveCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalEmitterDTO* MockEmitters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* MockCount;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalAtomicCounterDTO* Counters;
            public double3 GridOriginAup;
            public int3 Dimensions;
            public float CellSizeMeters;
            public float MaxChannelIntensity;
            public float EmitterRadiusScale;
            public int ActiveCapacity;
            public int MockCapacity;

            public void Execute(int index)
            {
                if (Grid == null || ActiveEmitters == null || ActiveCount == null || MockEmitters == null || MockCount == null)
                    return;

                int activeCount = math.clamp(ActiveCount[0], 0, ActiveCapacity);
                int mockCount = math.clamp(MockCount[0], 0, MockCapacity);
                ChemicalEmitterDTO emitter;
                if (index < activeCount)
                {
                    emitter = ActiveEmitters[index];
                }
                else
                {
                    int mockIndex = index - ActiveCapacity;
                    if ((uint)mockIndex >= (uint)mockCount)
                        return;

                    emitter = MockEmitters[mockIndex];
                }

                InjectEmitter(in emitter);
                if (Counters != null)
                {
                    Counters->ActiveEmitterCount = activeCount;
                    Counters->MockEmitterCount = mockCount;
                }
            }

            private void InjectEmitter(in ChemicalEmitterDTO emitter)
            {
                float cellSize = math.max(GridSampleEpsilon, CellSizeMeters);
                float radius = NormalizeChemicalRadius(emitter.RadiusMeters * FiniteAtLeast(EmitterRadiusScale, 1f, 0.001f));
                if (radius <= 0f)
                    return;

                float3 localCenter = ToFloat3Burst(emitter.Aup - GridOriginAup);
                float3 centerGrid = localCenter * math.rcp(cellSize);
                int radiusCells = math.max(1, (int)math.ceil(radius * math.rcp(cellSize)));
                int3 minCell = math.max(new int3(0), (int3)math.floor(centerGrid) - new int3(radiusCells));
                int3 maxCell = math.min(Dimensions - new int3(1), (int3)math.ceil(centerGrid) + new int3(radiusCells));
                float radiusSq = math.max(0.0001f, radius * radius);
                float safeMax = FiniteAtLeast(MaxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);

                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        for (int x = minCell.x; x <= maxCell.x; x++)
                        {
                            float3 cellCenter = (new float3(x, y, z) + 0.5f) * cellSize;
                            float distanceSq = math.lengthsq(cellCenter - localCenter);
                            if (distanceSq > radiusSq)
                                continue;

                            float falloff = Smooth01(1f - math.saturate(distanceSq * math.rcp(radiusSq)));
                            float4 add = emitter.Channels * falloff;
                            int gridIndex = x + z * Dimensions.x + y * Dimensions.x * Dimensions.z;
                            ChemicalCellDTO* cell = Grid + gridIndex;
                            AtomicAddFloat(&cell->BloodConcentration, math.clamp(add.x, 0f, safeMax));
                            AtomicAddFloat(&cell->PheromoneConcentration, math.clamp(add.y + add.z, 0f, safeMax));
                            AtomicAddFloat(&cell->ToxinConcentration, math.clamp(add.w, -safeMax, safeMax));
                            AtomicOrUInt(&cell->Flags, emitter.Flags);
                            if (Counters != null)
                                Interlocked.Increment(ref Counters->ActiveCellCount);
                        }
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ChemicalDiffusionSolverJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Source;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Destination;
            [NoAlias, ReadOnly] public NativeArray<byte>.ReadOnly Sdf;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalAtomicCounterDTO* Counters;
            public int3 Dimensions;
            public int CellCount;
            public int SdfLength;
            public int IterationIndex;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public float SimulationTickDelta;
            public float BaseDiffusionRate;
            public float AdvectionStrength;
            public float DissipationRate;
            public float GlobalQualityWeight;
            public uint SectorHash;
            public int TotalIterations;

            public void Execute(int index)
            {
                if (Source == null || Destination == null || (uint)index >= (uint)CellCount)
                    return;

                int3 c = IndexToCoord(index, Dimensions);
                ChemicalCellDTO center = Source[index];
                bool occluded = IsSolidSdf(index);
                if (occluded)
                {
                    Destination[index] = new ChemicalCellDTO { Flags = CellFlagOccluded };
                    return;
                }

                ChemicalCellDTO n0 = ReadClamped(c + new int3(-1, 0, 0));
                ChemicalCellDTO n1 = ReadClamped(c + new int3(1, 0, 0));
                ChemicalCellDTO n2 = ReadClamped(c + new int3(0, -1, 0));
                ChemicalCellDTO n3 = ReadClamped(c + new int3(0, 1, 0));
                ChemicalCellDTO n4 = ReadClamped(c + new int3(0, 0, -1));
                ChemicalCellDTO n5 = ReadClamped(c + new int3(0, 0, 1));
                float4 centerV = ToVector(center);
                float4 neighborSum = ToVector(n0) + ToVector(n1) + ToVector(n2) + ToVector(n3) + ToVector(n4) + ToVector(n5);

                float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                float qCurve = Smooth01(q);
                float dt = math.max(0.0001f, SimulationTickDelta);
                float diffusionRate = math.saturate(BaseDiffusionRate * math.lerp(0.35f, 1.2f, qCurve) * dt);
                float sumRate = 6f * diffusionRate;
                float4 jacobi = (neighborSum * diffusionRate + centerV) * math.rcp(math.max(0.0001f, sumRate + 1f));
                float driftGate = qCurve;
                float4 advected = centerV;
                if (driftGate > 0.0001f)
                {
                    float3 local = (new float3(c.x, c.y, c.z) + 0.5f) * math.max(GridSampleEpsilon, CellSizeMeters);
                    float3 drift = SampleAbyssalDrift(local, SectorHash, q);
                    float3 previous = (local - drift * (AdvectionStrength * dt * math.lerp(0.25f, 1.5f, qCurve))) * math.rcp(math.max(GridSampleEpsilon, CellSizeMeters)) - 0.5f;
                    float4 nearest = ToVector(ReadNearest(previous));
                    float4 tri = SampleTrilinear(previous);
                    advected = math.lerp(nearest, tri, qCurve);
                }

                float4 result = jacobi + (advected - centerV) * (driftGate * math.saturate(AdvectionStrength) * 0.35f);
                float dissipation = math.saturate(DissipationRate * dt * math.lerp(1.4f, 0.55f, qCurve));
                result *= 1f - dissipation;
                result.x = math.max(0f, result.x);
                result.y = math.max(0f, result.y);
                result.z = math.max(0f, result.z);

                bool finite = math.all(math.isfinite(result));
                if (!finite)
                {
                    result = float4.zero;
                    if (Counters != null)
                        Interlocked.Exchange(ref Counters->NaNFlag, 1);
                }

                ChemicalCellDTO output = new ChemicalCellDTO
                {
                    BloodConcentration = result.x,
                    PheromoneConcentration = result.y,
                    ToxinConcentration = result.w,
                    Flags = center.Flags & ~CellFlagOccluded
                };
                Destination[index] = output;

                if (Counters != null && index == 0)
                    Interlocked.Exchange(ref Counters->JacobiIterations, math.max(1, TotalIterations));
            }

            private ChemicalCellDTO ReadClamped(int3 c)
            {
                c = math.clamp(c, int3.zero, Dimensions - new int3(1));
                return Source[c.x + c.z * Dimensions.x + c.y * Dimensions.x * Dimensions.z];
            }

            private ChemicalCellDTO ReadNearest(float3 p)
            {
                int3 c = new int3(
                    math.clamp((int)math.round(p.x), 0, Dimensions.x - 1),
                    math.clamp((int)math.round(p.y), 0, Dimensions.y - 1),
                    math.clamp((int)math.round(p.z), 0, Dimensions.z - 1));
                return Source[c.x + c.z * Dimensions.x + c.y * Dimensions.x * Dimensions.z];
            }

            private float4 SampleTrilinear(float3 p)
            {
                float3 clamped = math.clamp(p, float3.zero, new float3(Dimensions.x - 1, Dimensions.y - 1, Dimensions.z - 1));
                int3 p0 = new int3((int)math.floor(clamped.x), (int)math.floor(clamped.y), (int)math.floor(clamped.z));
                int3 p1 = math.min(p0 + new int3(1), Dimensions - new int3(1));
                float3 t = clamped - p0;
                float4 c000 = ToVector(Source[p0.x + p0.z * Dimensions.x + p0.y * Dimensions.x * Dimensions.z]);
                float4 c100 = ToVector(Source[p1.x + p0.z * Dimensions.x + p0.y * Dimensions.x * Dimensions.z]);
                float4 c010 = ToVector(Source[p0.x + p0.z * Dimensions.x + p1.y * Dimensions.x * Dimensions.z]);
                float4 c110 = ToVector(Source[p1.x + p0.z * Dimensions.x + p1.y * Dimensions.x * Dimensions.z]);
                float4 c001 = ToVector(Source[p0.x + p1.z * Dimensions.x + p0.y * Dimensions.x * Dimensions.z]);
                float4 c101 = ToVector(Source[p1.x + p1.z * Dimensions.x + p0.y * Dimensions.x * Dimensions.z]);
                float4 c011 = ToVector(Source[p0.x + p1.z * Dimensions.x + p1.y * Dimensions.x * Dimensions.z]);
                float4 c111 = ToVector(Source[p1.x + p1.z * Dimensions.x + p1.y * Dimensions.x * Dimensions.z]);
                float4 c00 = math.lerp(c000, c100, t.x);
                float4 c10 = math.lerp(c010, c110, t.x);
                float4 c01 = math.lerp(c001, c101, t.x);
                float4 c11 = math.lerp(c011, c111, t.x);
                return math.lerp(math.lerp(c00, c10, t.y), math.lerp(c01, c11, t.y), t.z);
            }

            private bool IsSolidSdf(int index)
            {
                if (!Sdf.IsCreated || SdfLength <= 0)
                    return false;

                int sdfIndex = math.abs((index * 1103515245 + (int)SectorHash) % SdfLength);
                int signedValue = (int)Sdf[sdfIndex] - 128;
                return signedValue < 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ChemicalPublishGridJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Source;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float4* Published;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float4* Overlay;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalAtomicCounterDTO* Counters;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalDefoliantZoneDTO* DefoliantZones;
            public int3 Dimensions;
            public int CellCount;
            public int DefoliantZoneCount;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public float MaxChannelIntensity;

            public void Execute(int index)
            {
                if (Source == null || Published == null || Overlay == null || (uint)index >= (uint)CellCount)
                    return;

                ChemicalCellDTO cell = Source[index];
                float safeMax = FiniteAtLeast(MaxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
                float4 normalized = new float4(
                    math.saturate(cell.BloodConcentration * math.rcp(safeMax)),
                    math.saturate(cell.PheromoneConcentration * math.rcp(safeMax)),
                    0f,
                    math.clamp(cell.ToxinConcentration * math.rcp(safeMax), -1f, 1f));
                int3 coord = IndexToCoord(index, Dimensions);
                double3 cellAup = GridOriginAup + new double3(
                    (coord.x + 0.5d) * CellSizeMeters,
                    (coord.y + 0.5d) * CellSizeMeters,
                    (coord.z + 0.5d) * CellSizeMeters);
                float overlayToxin = ResolveDefoliantOverlay(cellAup);
                if (overlayToxin < 0f)
                    normalized.w = math.min(normalized.w, overlayToxin);

                Published[index] = normalized;
                Overlay[index] = new float4(0f, 0f, 0f, overlayToxin);
                if (Counters != null)
                    AtomicMaxFloatBits(&Counters->MaxBloodBits, normalized.x);
            }

            private float ResolveDefoliantOverlay(double3 cellAup)
            {
                if (DefoliantZones == null || DefoliantZoneCount <= 0)
                    return 0f;

                float strongest = 0f;
                int count = math.min(DefoliantZoneCount, MaxDefoliantDeadZones);
                for (int i = 0; i < count; i++)
                {
                    ChemicalDefoliantZoneDTO zone = DefoliantZones[i];
                    float radius = NormalizeChemicalRadius(zone.RadiusMeters);
                    if (radius <= 0f)
                        continue;

                    double radiusSq = (double)radius * radius;
                    double distanceSq = math.lengthsq(cellAup - zone.CenterAup);
                    if (distanceSq > radiusSq)
                        continue;

                    float falloff = Smooth01(1f - math.saturate((float)(distanceSq / math.max(radiusSq, 0.0001d))));
                    float safeMax = FiniteAtLeast(MaxChannelIntensity, DefaultMaximumChannelIntensity, 0.1f);
                    strongest = math.max(strongest, falloff * math.saturate(zone.Intensity * math.rcp(safeMax)));
                }

                return -strongest;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ChemicalTelemetryWriteJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalTelemetryEntry* Telemetry;
            [NoAlias, NativeDisableUnsafePtrRestriction] public int* TelemetryCursor;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalAtomicCounterDTO* Counters;
            public double3 GridOriginAup;
            public uint Frame;
            public float GlobalQualityWeight;
            public int GridShiftManhattan;
            public int TelemetryCapacity;

            public void Execute()
            {
                if (Telemetry == null || TelemetryCursor == null || Counters == null || TelemetryCapacity <= 0)
                    return;

                int cursor = TelemetryCursor[0];
                if ((uint)cursor >= (uint)TelemetryCapacity)
                    cursor = 0;

                ChemicalTelemetryEntry entry = default;
                entry.GridOriginAup = GridOriginAup;
                entry.MaxBlood = math.max(0f, math.asfloat(Counters->MaxBloodBits));
                entry.SolverMicros = 0f;
                entry.Frame = Frame;
                entry.ActiveEmitters = math.max(0, Counters->ActiveEmitterCount);
                entry.MockEmitters = math.max(0, Counters->MockEmitterCount);
                entry.Iterations = math.max(0, Counters->JacobiIterations);
                entry.Flags = Counters->NaNFlag != 0 ? TelemetryFlagNaN : 0u;
                entry.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
                entry.GridShiftManhattan = math.max(0, GridShiftManhattan);
                entry.StateHash = HashTelemetry(entry);
                Telemetry[cursor] = entry;

                cursor++;
                if (cursor >= TelemetryCapacity)
                    cursor = 0;
                TelemetryCursor[0] = cursor;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ShiftChemicalGridJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Source;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Destination;
            public int3 Dimensions;
            public int3 ShiftCells;
            public int CellCount;

            public void Execute()
            {
                if (Source == null || Destination == null || CellCount <= 0)
                    return;

                int cellSize = UnsafeUtility.SizeOf<ChemicalCellDTO>();
                UnsafeUtility.MemClear(Destination, CellCount * cellSize);
                if (math.abs(ShiftCells.x) >= Dimensions.x ||
                    math.abs(ShiftCells.y) >= Dimensions.y ||
                    math.abs(ShiftCells.z) >= Dimensions.z)
                {
                    return;
                }

                int destXStart = math.max(0, -ShiftCells.x);
                int destXEnd = math.min(Dimensions.x, Dimensions.x - ShiftCells.x);
                int copyCount = destXEnd - destXStart;
                if (copyCount <= 0)
                    return;

                for (int y = 0; y < Dimensions.y; y++)
                {
                    int srcY = y + ShiftCells.y;
                    if ((uint)srcY >= (uint)Dimensions.y)
                        continue;

                    for (int z = 0; z < Dimensions.z; z++)
                    {
                        int srcZ = z + ShiftCells.z;
                        if ((uint)srcZ >= (uint)Dimensions.z)
                            continue;

                        int srcIndex = (destXStart + ShiftCells.x) + srcZ * Dimensions.x + srcY * Dimensions.x * Dimensions.z;
                        int dstIndex = destXStart + z * Dimensions.x + y * Dimensions.x * Dimensions.z;
                        UnsafeUtility.MemMove(Destination + dstIndex, Source + srcIndex, copyCount * cellSize);
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CopyChemicalGridJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Source;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalCellDTO* Destination;
            public int CellCount;

            public void Execute(int index)
            {
                if (Source == null || Destination == null || (uint)index >= (uint)CellCount)
                    return;

                Destination[index] = Source[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct SampleChemicalGridJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalSampleRequestDTO* Requests;
            [NoAlias, NativeDisableUnsafePtrRestriction] public ChemicalSampleResultDTO* Results;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float4* PublishedGrid;
            public int RequestCount;
            public int3 Dimensions;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (Requests == null || Results == null || PublishedGrid == null || (uint)index >= (uint)RequestCount)
                    return;

                ChemicalSampleRequestDTO request = Requests[index];
                float3 local = ToFloat3Burst(request.Aup - GridOriginAup);
                float3 grid = local * math.rcp(math.max(GridSampleEpsilon, CellSizeMeters));
                ChemicalSampleResultDTO result = default;
                result.EntityId = request.EntityId;
                if (grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                    grid.x > Dimensions.x - 1 || grid.y > Dimensions.y - 1 || grid.z > Dimensions.z - 1)
                {
                    Results[index] = result;
                    return;
                }

                float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                float4 nearest = SampleFloat4Nearest(PublishedGrid, grid, Dimensions);
                float4 tri = SampleFloat4Trilinear(PublishedGrid, grid, Dimensions);
                float4 channels = math.lerp(nearest, tri, Smooth01(q));
                result.Channels = channels;
                result.BloodScalar = math.saturate(channels.x);
                result.FearScalar = math.saturate(channels.z);
                result.Flags = 1u;
                Results[index] = result;
            }
        }

        private static float4 ToVector(ChemicalCellDTO cell)
        {
            return new float4(
                math.max(0f, math.isfinite(cell.BloodConcentration) ? cell.BloodConcentration : 0f),
                math.max(0f, math.isfinite(cell.PheromoneConcentration) ? cell.PheromoneConcentration : 0f),
                0f,
                math.isfinite(cell.ToxinConcentration) ? cell.ToxinConcentration : 0f);
        }

        private static int3 IndexToCoord(int index, int3 dimensions)
        {
            int y = index / math.max(1, dimensions.x * dimensions.z);
            int rem = index - y * dimensions.x * dimensions.z;
            int z = rem / math.max(1, dimensions.x);
            int x = rem - z * dimensions.x;
            return new int3(x, y, z);
        }

        private static float3 ToFloat3Burst(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static float3 SampleAbyssalDrift(float3 local, uint seed, float quality)
        {
            float q = math.saturate(quality);
            float qCurve = Smooth01(q);
            float3 p = local * math.lerp(0.009f, 0.021f, qCurve) + new float3(
                (seed & 255u) * 0.017f,
                ((seed >> 8) & 255u) * 0.019f,
                ((seed >> 16) & 255u) * 0.023f);
            float3 baseFlow = new float3(
                MathLodApproximation.ApproxSinBhaskara(p.y + p.z * 0.57f),
                MathLodApproximation.ApproxSinBhaskara(p.z * 0.43f + p.x * 0.29f) * 0.28f,
                MathLodApproximation.ApproxCosBhaskara(p.x - p.y * 0.31f));
            float highTap = Smooth01((q - 0.7f) * 3.3333333f);
            float3 overkill = new float3(
                MathLodApproximation.ApproxSinBhaskara(p.x * 2.1f + p.z),
                MathLodApproximation.ApproxCosBhaskara(p.y * 1.7f + p.x),
                MathLodApproximation.ApproxSinBhaskara(p.z * 1.9f - p.y)) * highTap;
            float3 drift = baseFlow + overkill * 0.35f;
            float invLen = math.rsqrt(math.max(0.0001f, math.lengthsq(drift)));
            return drift * invLen;
        }

        private static float4 SampleFloat4Nearest(float4* grid, float3 gridPosition, int3 dimensions)
        {
            int3 c = new int3(
                math.clamp((int)math.round(gridPosition.x), 0, dimensions.x - 1),
                math.clamp((int)math.round(gridPosition.y), 0, dimensions.y - 1),
                math.clamp((int)math.round(gridPosition.z), 0, dimensions.z - 1));
            return grid[c.x + c.z * dimensions.x + c.y * dimensions.x * dimensions.z];
        }

        private static float4 SampleFloat4Trilinear(float4* grid, float3 gridPosition, int3 dimensions)
        {
            float3 clamped = math.clamp(gridPosition, float3.zero, new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1));
            int3 p0 = new int3((int)math.floor(clamped.x), (int)math.floor(clamped.y), (int)math.floor(clamped.z));
                int3 p1 = math.min(p0 + new int3(1), dimensions - new int3(1));
            float3 t = clamped - p0;
            float4 c000 = grid[p0.x + p0.z * dimensions.x + p0.y * dimensions.x * dimensions.z];
            float4 c100 = grid[p1.x + p0.z * dimensions.x + p0.y * dimensions.x * dimensions.z];
            float4 c010 = grid[p0.x + p0.z * dimensions.x + p1.y * dimensions.x * dimensions.z];
            float4 c110 = grid[p1.x + p0.z * dimensions.x + p1.y * dimensions.x * dimensions.z];
            float4 c001 = grid[p0.x + p1.z * dimensions.x + p0.y * dimensions.x * dimensions.z];
            float4 c101 = grid[p1.x + p1.z * dimensions.x + p0.y * dimensions.x * dimensions.z];
            float4 c011 = grid[p0.x + p1.z * dimensions.x + p1.y * dimensions.x * dimensions.z];
            float4 c111 = grid[p1.x + p1.z * dimensions.x + p1.y * dimensions.x * dimensions.z];
            float4 c00 = math.lerp(c000, c100, t.x);
            float4 c10 = math.lerp(c010, c110, t.x);
            float4 c01 = math.lerp(c001, c101, t.x);
            float4 c11 = math.lerp(c011, c111, t.x);
            return math.lerp(math.lerp(c00, c10, t.y), math.lerp(c01, c11, t.y), t.z);
        }

        private static void AtomicAddFloat(float* target, float value)
        {
            if (target == null || !math.isfinite(value) || value == 0f)
                return;

            int* bits = (int*)target;
            int observed;
            int nextBits;
            do
            {
                observed = bits[0];
                float current = math.asfloat(observed);
                if (!math.isfinite(current))
                    current = 0f;
                float next = current + value;
                next = math.select(0f, next, math.isfinite(next));
                nextBits = math.asint(next);
            }
            while (Interlocked.CompareExchange(ref bits[0], nextBits, observed) != observed);
        }

        private static void AtomicMaxFloatBits(int* targetBits, float value)
        {
            if (targetBits == null || !math.isfinite(value))
                return;

            value = math.max(0f, value);
            int observed;
            int nextBits = math.asint(value);
            do
            {
                observed = targetBits[0];
                float current = math.asfloat(observed);
                if (current >= value)
                    return;
            }
            while (Interlocked.CompareExchange(ref targetBits[0], nextBits, observed) != observed);
        }

        private static void AtomicOrUInt(uint* target, uint value)
        {
            if (target == null || value == 0u)
                return;

            int* bits = (int*)target;
            int observed;
            int next;
            do
            {
                observed = bits[0];
                next = observed | (int)value;
            }
            while (Interlocked.CompareExchange(ref bits[0], next, observed) != observed);
        }

        private static uint HashTelemetry(ChemicalTelemetryEntry entry)
        {
            uint hash = 2166136261u;
            hash = (hash ^ entry.Frame) * 16777619u;
            hash = (hash ^ (uint)entry.ActiveEmitters) * 16777619u;
            hash = (hash ^ (uint)entry.MockEmitters) * 16777619u;
            hash = (hash ^ (uint)entry.Iterations) * 16777619u;
            float maxBlood = entry.MaxBlood == 0f ? 0.0f : entry.MaxBlood;
            float globalQuality = entry.GlobalQualityWeight == 0f ? 0.0f : entry.GlobalQualityWeight;
            hash = (hash ^ math.asuint(maxBlood)) * 16777619u;
            hash = (hash ^ math.asuint(globalQuality)) * 16777619u;
            hash = (hash ^ entry.Flags) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static uint HashAsciiBurst(byte a, byte b, byte c, byte d)
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            hash = (hash ^ c) * 16777619u;
            hash = (hash ^ d) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

#region JulesLink_ChemicalDiffusionSolver
        private static void JulesLink_ChemicalDiffusionSolver() { _ = typeof(Hecton8.PureLogic.Systems.ChemicalDiffusionSolver); }
        #endregion

    }
}
