// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelVolume.cs — Project HECTON-8 Voxel Volume Component         ║
// ║  Unity 6 | Simple component for cave volumes                             ║
// ║  v1.0 — Basic volume marker                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.World;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime subtractive crater stamp applied to the voxel SDF field.
    /// Stored on the generated volume and replayed during async rebuilds.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VoxelCraterStamp
    {
        [FieldOffset(0)] public double3 position;
        [FieldOffset(24)] public float radius;
        [FieldOffset(28)] public float blendRadius;
    }

    /// <summary>
    /// Runtime physics/collider bake gate for voxel chunk interaction safety.
    /// </summary>
    public enum VoxelBakeState : byte
    {
        Idle = 0,
        Pending = 1,
        Baking = 2,
        Complete = 3
    }

    /// <summary>
    /// Scanner-grade CPU SDF raymarch hit resolved without Unity Physics.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct VoxelSdfRaycastHit
    {
        [FieldOffset(0)] public Vector3 Point;
        [FieldOffset(12)] public Vector3 Normal;
        [FieldOffset(24)] public float Distance;
        [FieldOffset(28)] public float Density;
        [FieldOffset(32)] public byte Hit;
        [FieldOffset(33)] private byte _pad0;
        [FieldOffset(34)] private byte _pad1;
        [FieldOffset(35)] private byte _pad2;
        [FieldOffset(36)] private byte _pad3;
        [FieldOffset(37)] private byte _pad4;
        [FieldOffset(38)] private byte _pad5;
        [FieldOffset(39)] private byte _pad6;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VoxelSdfRaymarchJob : IJob
    {
        private const int MaxLegacyRaymarchSteps = 2048;

        [ReadOnly] public NativeArray<byte>.ReadOnly EncodedSdf;
        public int3 GridDimensions;
        public float3 VolumeOrigin;
        public float3 CellSize;
        public float SdfRange;
        public float3 Origin;
        public float3 Direction;
        public float MaxDistance;
        public float StepMeters;
        public NativeArray<VoxelSdfRaycastHit> Result;

        public void Execute()
        {
            bool canWriteResult = Result.IsCreated && Result.Length > 0;
            if (canWriteResult)
                Result[0] = default;

            if (!EncodedSdf.IsCreated ||
                !canWriteResult ||
                GridDimensions.x <= 1 ||
                GridDimensions.y <= 1 ||
                GridDimensions.z <= 1 ||
                SdfRange <= 0f ||
                MaxDistance <= 0f)
            {
                return;
            }

            float3 direction = NormalizeSafe(Direction, new float3(0f, 0f, 1f));
            if (!TryResolveRaymarchInterval(
                    MaxDistance,
                    Origin,
                    direction,
                    VolumeOrigin,
                    CellSize,
                    GridDimensions,
                    out float startDistance,
                    out float endDistance))
            {
                return;
            }

            float segmentDistance = math.max(0f, endDistance - startDistance);
            float requestedStep = math.max(0.05f, StepMeters);
            float step = ResolveStep(segmentDistance, requestedStep);
            int stepCount = ResolveStepCount(segmentDistance, step);
            float previousDensity = 0f;
            float previousDistance = 0f;
            float3 previousPosition = Origin;
            bool hasPrevious = false;
            for (int i = 0; i <= stepCount; i++)
            {
                float distance = math.min(endDistance, startDistance + i * step);
                float3 position = Origin + direction * distance;
                float density = Sample(position);
                bool nearSurface = math.abs(density) <= 0.0001f;
                bool crossedSurface =
                    hasPrevious &&
                    ((previousDensity < -0.0001f && density >= 0.0001f) ||
                     (previousDensity > 0.0001f && density <= -0.0001f));

                if (nearSurface || crossedSurface)
                {
                    float resolvedDistance = distance;
                    float3 resolvedPoint = position;
                    if (crossedSurface)
                    {
                        float previousAbsDensity = math.abs(previousDensity);
                        float currentAbsDensity = math.abs(density);
                        float t = math.saturate(previousAbsDensity * math.rcp(math.max(0.0001f, previousAbsDensity + currentAbsDensity)));
                        resolvedDistance = math.lerp(previousDistance, distance, t);
                        resolvedPoint = math.lerp(previousPosition, position, t);
                    }

                    Result[0] = new VoxelSdfRaycastHit
                    {
                        Point = new Vector3(resolvedPoint.x, resolvedPoint.y, resolvedPoint.z),
                        Normal = new Vector3(0f, 1f, 0f),
                        Distance = math.max(0f, resolvedDistance),
                        Density = 0f,
                        Hit = 1
                    };
                    return;
                }

                previousDensity = density;
                previousDistance = distance;
                previousPosition = position;
                hasPrevious = true;
                if (distance >= endDistance)
                    break;
            }
        }

        private static bool TryResolveRaymarchInterval(
            float maxDistance,
            float3 origin,
            float3 direction,
            float3 volumeOrigin,
            float3 cellSize,
            int3 gridDimensions,
            out float startDistance,
            out float endDistance)
        {
            startDistance = 0f;
            endDistance = 0f;
            if (!math.isfinite(maxDistance) || maxDistance <= 0f || !math.all(math.isfinite(direction)))
                return false;

            float3 safeCell = math.max(math.abs(cellSize), new float3(0.0001f));
            float3 gridSpan = safeCell * math.max((float3)(gridDimensions - new int3(1)), new float3(1f));
            float3 boundsMin = volumeOrigin;
            float3 boundsMax = volumeOrigin + gridSpan;
            float tMin = 0f;
            float tMax = maxDistance;
            if (!TryUpdateAxisInterval(origin.x, direction.x, boundsMin.x, boundsMax.x, ref tMin, ref tMax) ||
                !TryUpdateAxisInterval(origin.y, direction.y, boundsMin.y, boundsMax.y, ref tMin, ref tMax) ||
                !TryUpdateAxisInterval(origin.z, direction.z, boundsMin.z, boundsMax.z, ref tMin, ref tMax))
            {
                return false;
            }

            startDistance = math.max(0f, tMin);
            endDistance = math.min(maxDistance, tMax);
            return math.isfinite(startDistance) &&
                   math.isfinite(endDistance) &&
                   endDistance >= startDistance;
        }

        private static bool TryUpdateAxisInterval(
            float origin,
            float direction,
            float boundsMin,
            float boundsMax,
            ref float tMin,
            ref float tMax)
        {
            if (!math.isfinite(origin) ||
                !math.isfinite(direction) ||
                !math.isfinite(boundsMin) ||
                !math.isfinite(boundsMax))
            {
                return false;
            }

            float min = math.min(boundsMin, boundsMax);
            float max = math.max(boundsMin, boundsMax);
            if (math.abs(direction) <= 0.0001f)
                return origin >= min && origin <= max;

            float inverseDirection = math.rcp(direction);
            float t0 = (min - origin) * inverseDirection;
            float t1 = (max - origin) * inverseDirection;
            float axisMin = math.min(t0, t1);
            float axisMax = math.max(t0, t1);
            tMin = math.max(tMin, axisMin);
            tMax = math.min(tMax, axisMax);
            return tMax >= tMin && tMax >= 0f;
        }

        private static float ResolveStep(float maxDistance, float requestedStep)
        {
            float capStep = maxDistance * math.rcp(MaxLegacyRaymarchSteps);
            return math.max(requestedStep, math.isfinite(capStep) ? capStep : requestedStep);
        }

        private static int ResolveStepCount(float maxDistance, float step)
        {
            float rawStepCount = math.ceil(maxDistance * math.rcp(math.max(0.0001f, step)));
            if (!math.isfinite(rawStepCount) || rawStepCount >= MaxLegacyRaymarchSteps)
                return MaxLegacyRaymarchSteps;

            return math.max(1, (int)rawStepCount);
        }

        private float Sample(float3 worldPosition)
        {
            float3 safeCell = math.max(CellSize, new float3(0.0001f));
            float3 sample = (worldPosition - VolumeOrigin) / safeCell;
            sample = math.clamp(sample, float3.zero, new float3(GridDimensions.x - 1.001f, GridDimensions.y - 1.001f, GridDimensions.z - 1.001f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, GridDimensions.x - 1);
            int y1 = math.min(y0 + 1, GridDimensions.y - 1);
            int z1 = math.min(z0 + 1, GridDimensions.z - 1);
            float tx = sample.x - x0;
            float ty = sample.y - y0;
            float tz = sample.z - z0;

            float c000 = DecodeAt(x0, y0, z0);
            float c100 = DecodeAt(x1, y0, z0);
            float c010 = DecodeAt(x0, y1, z0);
            float c110 = DecodeAt(x1, y1, z0);
            float c001 = DecodeAt(x0, y0, z1);
            float c101 = DecodeAt(x1, y0, z1);
            float c011 = DecodeAt(x0, y1, z1);
            float c111 = DecodeAt(x1, y1, z1);
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            return math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f
                ? value / math.max(ApproxMagnitude(value), 0.0001f)
                : fallback;
        }

        private static float ApproxMagnitude(float3 value)
        {
            float3 axis = math.abs(value);
            float maxAxis = math.cmax(axis);
            float minAxis = math.cmin(axis);
            float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
        }

        private float DecodeAt(int x, int y, int z)
        {
            int index = x + GridDimensions.x * (y + GridDimensions.y * z);
            if (index < 0 || index >= EncodedSdf.Length)
                return 0f;

            return ((EncodedSdf[index] / 255f) * 2f - 1f) * SdfRange;
        }
    }

    /// <summary>
    /// Simple component attached to generated cave volume GameObjects.
    /// Provides a way to identify and manage cave volumes in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonVoxelVolume : MonoBehaviour, IVoxelSonarSdfSampleSource, IVoxelRepairWeldTarget, IVoxelPlasmaCutTarget, IGlobalRegistryHotSwapListener
    {
        private const string CaveDressingRootName = "_CaveDressing";
        private const string EntranceQualityRootName = "_EntranceQualityZone";
        private const string EntranceMarkersRootName = "_EntranceMarkers";
        private const string ColliderChunkRootName = "_ColliderChunks";
        private const int MaxCraterStampCount = 16;
        private const int ResourceCraterCollapseThreshold = 5;
        private const int CollapseImpulseColliderCapacity = 48;
        private const int CollapseImpulseBodyCapacity = 32;
        private const int MaxTerrainHoleHandleCount = 8;
        private const int MaxColliderChunkCount = 8;
        private const float MinColliderChunkProxySize = 0.01f;
        private const int MaxPlasmaCutSteps = 24;
        private const int MaxQueuedRebuildPassesPerKick = 4;
        private const int MaxMagmaVeinBurnSamplesPerSegment = 16;
        private const int MaxRegisteredPublishedVolumes = 256;
        private const int MaxLegacyRaymarchSteps = 2048;
        private const string NativeMemoryOwner = nameof(HectonVoxelVolume);
        private const float MinRuntimeEntranceSnapshotRadius = 0.1f;
        private const float MaxRuntimeEntranceSnapshotRadius = 32f;
        private const float MinRuntimeEntranceSnapshotFunnelLength = 0.25f;
        private const float MaxRuntimeEntranceSnapshotFunnelLength = 128f;
        private const float MinRuntimeEntranceSnapshotInnerRadius = 0.05f;
        private const float MinRuntimeGraphRadius = 0.1f;
        private const float MaxRuntimeGraphRadius = 256f;
        private const float MaxRuntimeGraphBlendRadius = 96f;
        private const float MaxRuntimeTunnelScale = 8f;
        private const float MaxRuntimeTunnelWarpAmount = 64f;
        private const float MaxRuntimeGraphNoiseScale = 8f;
        private const float MaxRuntimeGraphNoiseAmplitude = 8f;
        private const float ResourceCraterClusterRadiusMeters = 20f;
        private const float CollapseBoxHorizontalPaddingMeters = 4f;
        private const float CollapseImpulseVerticalBias = 0.45f;
        private const float MinPlasmaCutPower = 0.02f;
        private const float PlasmaCutAttenuationPerMeter = 1f;
        private const byte DefaultDeltaMaterialId = 0;
        private const byte SedimentDeltaMaterialId = 1;
        private const byte MagmaDeltaMaterialId = 2;
        private const byte DefaultPublishedSonarAudioMaterialId = 2;
        private const int PublishedSonarMaxGridDimension = 129;
        private const int PublishedSonarMaxPointCount = PublishedSonarMaxGridDimension * PublishedSonarMaxGridDimension * PublishedSonarMaxGridDimension;
        private const int PublishedSonarVaultPayloadCapacity = PublishedSonarMaxPointCount;
        private const int PublishedSonarEncodeWaitWatchdogFrames = 240;
        internal const ulong PublishedSonarPayloadReadGuardMask =
            (1UL << ((int)BufferID.VoxelSdfPayloadDescriptor & 31)) |
            (1UL << ((int)BufferID.VoxelSdfTexture3D & 31)) |
            (1UL << ((int)BufferID.VoxelSdfAudioMaterialIds & 31));
        private static readonly int _CollapseImpulseLayerMask = HectonLayerMasks.MountedSweepLayerMask;
        private const float OrganicRootMoundMinimumOverlapMeters = 0.25f;
        private const float OrganicRootMoundSeabedProbeStepMeters = 0.5f;
        private const int OrganicRootMoundSeabedProbeSteps = 16;
        private const string ColliderChunkRuntimeName = "ColliderChunk";
        private const string ColliderChunkProxyRuntimeName = "ColliderChunkProxy";

        // Intrusive published-SDF registry: no managed container allocation on runtime setup.
        private static HectonVoxelVolume s_activePublishedHead;
        private static int s_activePublishedVolumeCount;
        private static int s_publishedSonarVaultPublishInFlight;
        private static int s_publishedSonarPayloadReadGuardGate;
        private static IDataVault s_publishedSonarPayloadReadGuardVault;
        private static ulong s_publishedSonarPayloadReadGuardMask;
        private static int s_publishedSonarPayloadReadGuardRefCount;
        private static readonly uint _SeismicShockwaveEventLaneDropWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelVolume.SeismicShockwaveEventLaneDrop"));
        private static readonly uint _SeismicShockwaveEventLaneContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonVoxelVolume.SeismicShockwaveEventLane"));

        private HectonVoxelVolume _publishedNext;
        private HectonVoxelVolume _publishedPrev;
        private bool _publishedRegistered;
        internal HectonVoxelVolume _deltaRegisteredNext;
        internal HectonVoxelVolume _deltaRegisteredPrev;
        internal HectonVoxelVolume _deltaPendingRebuildNext;
        internal HectonVoxelVolume _deltaPendingRebuildPrev;
        internal bool _deltaRegistered;
        internal bool _deltaPendingRebuildRegistered;
        internal HectonVoxelVolume _leakSentinelNext;
        internal HectonVoxelVolume _leakSentinelPrev;
        internal int _leakDestroyRequestedFrame;
        internal byte _leakSentinelState;

        internal readonly struct PublishedSonarSdfReadLease
        {
            internal readonly HectonVoxelVolume Owner;
            internal readonly IDataVault Vault;
            internal readonly int Version;
            internal readonly uint SdfGeneration;
            internal readonly uint AudioMaterialGeneration;
            internal readonly ulong MutationGuardMask;

            internal PublishedSonarSdfReadLease(
                HectonVoxelVolume owner,
                IDataVault vault,
                int version,
                uint sdfGeneration,
                uint audioMaterialGeneration,
                ulong mutationGuardMask)
            {
                Owner = owner;
                Vault = vault;
                Version = version;
                SdfGeneration = sdfGeneration;
                AudioMaterialGeneration = audioMaterialGeneration;
                MutationGuardMask = mutationGuardMask;
            }

            internal bool IsValid => Owner != null && Vault != null && Version > 0 && SdfGeneration != 0u && MutationGuardMask != 0UL;
            internal bool HasAudioMaterialLock => AudioMaterialGeneration != 0u;
        }

        private HectonVoxelEngine _engine;
        private VoxelDeltaProcessor _deltaProcessor;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private DestructibleOrganicManager _cachedOrganicManager;
        private CaveNode[] _nodes = Array.Empty<CaveNode>();
        private CaveTunnel[] _tunnels = Array.Empty<CaveTunnel>();
        private CaveEntrance[] _entrances = Array.Empty<CaveEntrance>();
        private CaveStructure[] _structures = Array.Empty<CaveStructure>();
        private FixedList4096Bytes<VoxelCraterStamp> _craterStamps;
        private FixedList4096Bytes<VoxelCraterStamp> _resourceCraterClusterStamps;
        private SpatialQueryHit[] _collapseImpulseContacts = Array.Empty<SpatialQueryHit>();
        private Rigidbody[] _collapseImpulseBodies = Array.Empty<Rigidbody>();
        private int _craterStampCount;
        private int _resourceCraterClusterCount;
        private int _runtimeStamp;
        private int _seismicShockwaveEventLaneDropCount;
        private IDataVault _cachedDataVault;
        private IPhysicsService _physicsService;
        private bool _hotSwapRegistered;
        private bool _runtimeDataReady;
        private bool _rebuildQueued;
        private bool _rebuildRunning;
        private bool _lastCollapseClusterValid;
        private uint _seed;
        private int _gridDimension;
        private float _voxelSize;
        private int _lodLevel;
        private bool _buildCollider;
        private CaveGenerationParams _caveParams;
        private Vector3 _generationAbsoluteUniversePosition;
        private double3 _generationAbsoluteUniversePositionDouble;
        private double3 _lastCollapseAbsoluteCenter;
        private FixedList128Bytes<int> _terrainHoleHandles;
        private int _terrainHoleHandleCount;
        private Transform _colliderChunkRoot;
        private MeshCollider[] _colliderChunkColliders = new MeshCollider[MaxColliderChunkCount]; // COLD ALLOC: fixed collider chunk registry - owner: HectonVoxelVolume
        private BoxCollider[] _colliderChunkBakeProxies = new BoxCollider[MaxColliderChunkCount]; // COLD ALLOC: fixed collider bake proxy registry - owner: HectonVoxelVolume
        private Mesh[] _colliderChunkMeshes = new Mesh[MaxColliderChunkCount]; // COLD ALLOC: fixed live collider mesh registry - owner: HectonVoxelVolume
        private Mesh[] _colliderChunkBakeMeshes = new Mesh[MaxColliderChunkCount]; // COLD ALLOC: fixed staged collider bake mesh registry - owner: HectonVoxelVolume
        private bool[] _colliderChunkBakeMeshReady = new bool[MaxColliderChunkCount]; // COLD ALLOC: staged mesh Physics.BakeMesh completion flags - owner: HectonVoxelVolume
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _rootMeshCollider;
        private VoxelBakeState _bakeState;
        private Vector3Int _publishedSonarGridDimensions;
        private Vector3 _publishedSonarOrigin;
        private Vector3 _publishedSonarCellSize;
        private float _publishedSonarSdfRange;
        private int _publishedSonarVersion;
        private int _publishedSonarSnapshotPublishInFlight;
        private int _publishedSonarPublishAbortRequested;

        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        /// <summary>Deterministic seed used to generate this volume.</summary>
        public uint Seed => _seed;

        internal MeshFilter CachedMeshFilter => _meshFilter;
        internal MeshRenderer CachedMeshRenderer => _meshRenderer;
        internal MeshCollider CachedRootMeshCollider => _rootMeshCollider;

        /// <summary>Absolute-universe center captured when this volume payload was built.</summary>
        public Vector3 GenerationAbsoluteUniversePosition => _generationAbsoluteUniversePosition;

        /// <summary>Double-precision absolute-universe center captured when this volume payload was built.</summary>
        public double3 GenerationAbsoluteUniversePositionDouble => _generationAbsoluteUniversePositionDouble;

        /// <summary>Voxel grid resolution used by this runtime volume.</summary>
        public int GridDimension => _gridDimension;

        /// <summary>Voxel step size used by this runtime volume.</summary>
        public float VoxelSize => _voxelSize;

        private static float ApproxMagnitude(float3 value)
        {
            float3 axis = math.abs(value);
            float maxAxis = math.cmax(axis);
            float minAxis = math.cmin(axis);
            float midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
        }

        private static float ApproxMagnitude(Vector3 value)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            float az = Mathf.Abs(value.z);
            float maxAxis = Mathf.Max(ax, Mathf.Max(ay, az));
            float minAxis = Mathf.Min(ax, Mathf.Min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.25f;
        }

        private static Vector3 NormalizeApprox(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f
                ? value / Mathf.Max(ApproxMagnitude(value), 0.0001f)
                : fallback;
        }

        /// <summary>
        /// Resolves the nearest voxel-corner world position for a raycast hit on this volume.
        /// The dominant hit-normal axis is preserved so cable bends snap onto the struck voxel face
        /// instead of drifting across the polygon midpoint.
        /// </summary>
        public bool TryResolveNearestVoxelCorner(Vector3 worldPosition, Vector3 worldNormal, out Vector3 cornerWorld)
        {
            cornerWorld = worldPosition;
            if (_gridDimension <= 0 || _voxelSize <= 0f)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            Transform cachedTransform = transform;
            Vector3 localPoint = cachedTransform.InverseTransformPoint(worldPosition);
            Vector3 localNormal = cachedTransform.InverseTransformDirection(worldNormal);
            float voxelStep = Mathf.Max(0.0001f, _voxelSize);
            Vector3 relative = (localPoint - localBounds.min) / voxelStep;
            int dominantAxis = ResolveDominantAxis(localNormal);

            float cornerX = ResolveCornerCoordinate(relative.x, dominantAxis == 0 ? localNormal.x : 0f, _gridDimension);
            float cornerY = ResolveCornerCoordinate(relative.y, dominantAxis == 1 ? localNormal.y : 0f, _gridDimension);
            float cornerZ = ResolveCornerCoordinate(relative.z, dominantAxis == 2 ? localNormal.z : 0f, _gridDimension);

            Vector3 localCorner = localBounds.min + new Vector3(
                cornerX * voxelStep,
                cornerY * voxelStep,
                cornerZ * voxelStep);
            cornerWorld = cachedTransform.TransformPoint(localCorner);
            return true;
        }

        /// <summary>
        /// Tentacle / appendage helper alias for the nearest voxel-corner query.
        /// Kept on the runtime volume owner so gameplay code does not reach into voxel build internals.
        /// </summary>
        public bool TryGetNearestCorner(Vector3 worldPosition, Vector3 worldNormal, out Vector3 cornerWorld)
        {
            return TryResolveNearestVoxelCorner(worldPosition, worldNormal, out cornerWorld);
        }

        /// <summary>LOD level used to build this runtime volume.</summary>
        public int LODLevel => _lodLevel;

        /// <summary>Whether collider rebuilds should be emitted with the mesh.</summary>
        public bool BuildCollider => _buildCollider;

        /// <summary>Current immutable cave-generation parameter snapshot.</summary>
        public CaveGenerationParams CaveParams => _caveParams;

        /// <summary>Captured room graph used for crater rebuilds.</summary>
        public CaveNode[] Nodes => _nodes;

        /// <summary>Captured tunnel graph used for crater rebuilds.</summary>
        public CaveTunnel[] Tunnels => _tunnels;

        /// <summary>Captured entrance graph used for crater rebuilds.</summary>
        public CaveEntrance[] Entrances => _entrances;

        /// <summary>Captured solid cave-structure graph used for crater rebuilds.</summary>
        public CaveStructure[] Structures => _structures;

        /// <summary>Active crater stamp count inside the fixed crater registry.</summary>
        public int CraterStampCount => _craterStampCount;

        /// <summary>Reads a bounded subtractive crater stamp without exposing a managed array.</summary>
        public bool TryGetCraterStamp(int index, out VoxelCraterStamp stamp)
        {
            if (index < 0 || index >= _craterStampCount || index >= _craterStamps.Length)
            {
                stamp = default;
                return false;
            }

            stamp = _craterStamps[index];
            return true;
        }

        /// <summary>Generation stamp used to reject stale async rebuild completions.</summary>
        public int RuntimeStamp => _runtimeStamp;

        /// <summary>Number of visible random seismic event-lane drops from this volume.</summary>
        public int SeismicShockwaveEventLaneDropCount => _seismicShockwaveEventLaneDropCount;

        /// <summary>Whether this pooled volume currently has enough data to rebuild itself.</summary>
        public bool HasRuntimeData => _runtimeDataReady;

        /// <summary>Current bake gate state used for collider and interaction locking.</summary>
        public VoxelBakeState BakeState => _bakeState;

        /// <summary>Published PDA sonar snapshot revision.</summary>
        public int PublishedSonarVersion => _publishedSonarVersion;

        public static bool TryRaymarchAnyPublishedSdf(
            Vector3 runtimeOrigin,
            Vector3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out HectonVoxelVolume volume,
            out VoxelSdfRaycastHit hit)
        {
            volume = null;
            hit = default;
            float bestDistance = float.MaxValue;
            bool resolved = false;
            for (HectonVoxelVolume candidate = s_activePublishedHead; candidate != null; candidate = candidate._publishedNext)
            {
                if (candidate == null || !candidate._runtimeDataReady)
                    continue;

                if (!candidate.TryRaymarchPublishedSdf(runtimeOrigin, runtimeDirection, maxDistance, stepMeters, out VoxelSdfRaycastHit candidateHit) ||
                    candidateHit.Hit == 0 ||
                    candidateHit.Distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = candidateHit.Distance;
                hit = candidateHit;
                volume = candidate;
                resolved = true;
            }

            return resolved;
        }

        internal static bool TryAcquireClosestPublishedSonarSdfPayloadReadLease(
            Vector3 runtimeOrigin,
            out HectonVoxelVolume volume,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out NativeArray<byte>.ReadOnly audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version,
            out PublishedSonarSdfReadLease lease)
        {
            volume = null;
            encodedSdf = default;
            audioMaterialIds = default;
            gridDimensions = default;
            volumeOrigin = default;
            voxelCellSize = default;
            sdfRange = 0f;
            version = 0;
            lease = default;

            float bestDistanceSq = float.MaxValue;
            bool resolved = false;
            PublishedSonarSdfReadLease bestLease = default;
            HectonVoxelVolume bestVolume = null;

            for (HectonVoxelVolume candidate = s_activePublishedHead; candidate != null; candidate = candidate._publishedNext)
            {
                if (candidate == null || !candidate._runtimeDataReady)
                    continue;

                if (!candidate.TryAcquirePublishedSonarSdfPayloadReadLease(
                        out NativeArray<byte>.ReadOnly candidateSdf,
                        out NativeArray<byte>.ReadOnly candidateMaterialIds,
                        out Vector3Int candidateDimensions,
                        out Vector3 candidateOrigin,
                        out Vector3 candidateCellSize,
                        out float candidateSdfRange,
                        out int candidateVersion,
                        out PublishedSonarSdfReadLease candidateLease))
                {
                    continue;
                }

                bool keepCandidate = false;
                try
                {
                    Vector3 center = candidateOrigin + new Vector3(
                        candidateCellSize.x * math.max(0, candidateDimensions.x - 1) * 0.5f,
                        candidateCellSize.y * math.max(0, candidateDimensions.y - 1) * 0.5f,
                        candidateCellSize.z * math.max(0, candidateDimensions.z - 1) * 0.5f);
                    float distanceSq = (center - runtimeOrigin).sqrMagnitude;
                    if (distanceSq >= bestDistanceSq)
                        continue;

                    if (resolved && bestVolume != null)
                        bestVolume.ReleasePublishedSonarSdfPayloadReadLease(in bestLease);

                    bestDistanceSq = distanceSq;
                    bestLease = candidateLease;
                    bestVolume = candidate;
                    encodedSdf = candidateSdf;
                    audioMaterialIds = candidateMaterialIds;
                    gridDimensions = candidateDimensions;
                    volumeOrigin = candidateOrigin;
                    voxelCellSize = candidateCellSize;
                    sdfRange = candidateSdfRange;
                    version = candidateVersion;
                    resolved = true;
                    keepCandidate = true;
                }
                finally
                {
                    if (!keepCandidate)
                        candidate.ReleasePublishedSonarSdfPayloadReadLease(in candidateLease);
                }
            }

            if (!resolved)
                return false;

            volume = bestVolume;
            lease = bestLease;
            return true;
        }

        public static bool TryDepositAdditiveSdfSphere(Vector3 absoluteCenter, float radiusMeters, float strengthMeters)
        {
            return TryDepositAdditiveSdfSphere(
                new double3(absoluteCenter.x, absoluteCenter.y, absoluteCenter.z),
                radiusMeters,
                strengthMeters);
        }

        public static bool TryDepositAdditiveSdfSphere(double3 absoluteCenter, float radiusMeters, float strengthMeters)
        {
            float radius = Mathf.Max(0.05f, radiusMeters);
            float strength = Mathf.Max(0.05f, strengthMeters);
            HectonVoxelVolume candidate = s_activePublishedHead;
            while (candidate != null)
            {
                HectonVoxelVolume next = candidate._publishedNext;
                if (candidate == null || !candidate._runtimeDataReady)
                {
                    RemovePublishedVolumeNode(candidate);
                    candidate = next;
                    continue;
                }

                float halfExtent = candidate._gridDimension * candidate._voxelSize * 0.5f;
                float acceptedRadius = halfExtent + radius;
                double acceptedRadiusSq = (double)acceptedRadius * acceptedRadius;
                if (math.lengthsq(candidate._generationAbsoluteUniversePositionDouble - absoluteCenter) > acceptedRadiusSq ||
                    candidate._deltaProcessor == null)
                {
                    candidate = next;
                    continue;
                }

                candidate.SetBakeState(VoxelBakeState.Pending);
                candidate._deltaProcessor.ApplyImmediateAbsoluteWeld(candidate, absoluteCenter, radius, strength, MagmaDeltaMaterialId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Samples published SDF density from an absolute-universe position without reducing AUP precision before origin subtraction.
        /// </summary>
        public static bool GetSDFDensity(double3 aupPosition, out float density)
        {
            density = 0f;
            if (!math.all(math.isfinite(aupPosition)))
                return false;

            Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(aupPosition);
            return TrySampleRuntimeSdfDensity(runtimePosition, out density);
        }

        /// <summary>
        /// Samples published SDF density at a runtime-space point that has already passed through floating-origin localization.
        /// </summary>
        public static bool TrySampleRuntimeSdfDensity(Vector3 runtimePosition, out float density)
        {
            return TryReadRuntimeSdfDensity(runtimePosition, out density);
        }

        /// <summary>
        /// Reads published SDF density without mutating the active-volume registry.
        /// Stale-volume cleanup belongs to publish/unpublish owner phases, not validation reads.
        /// </summary>
        public static bool TryReadRuntimeSdfDensity(Vector3 runtimePosition, out float density)
        {
            density = 0f;
            if (!IsFinite(runtimePosition))
                return false;

            for (HectonVoxelVolume candidate = s_activePublishedHead; candidate != null; candidate = candidate._publishedNext)
            {
                if (candidate == null || !candidate._runtimeDataReady)
                    continue;

                if (candidate.TrySampleDensity(runtimePosition, out density))
                    return true;
            }

            return false;
        }

        private static void RegisterPublishedVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            if (volume._publishedRegistered)
                return;

            if (s_activePublishedVolumeCount >= MaxRegisteredPublishedVolumes)
            {
                HectonVoxelVolume eviction = SelectPublishedVolumeEvictionCandidate(volume);
                if (eviction == null)
                    return;

                RemovePublishedVolumeNode(eviction);
            }

            if (s_activePublishedVolumeCount >= MaxRegisteredPublishedVolumes)
                return;

            volume._publishedPrev = null;
            volume._publishedNext = s_activePublishedHead;
            volume._publishedRegistered = true;
            if (s_activePublishedHead != null)
                s_activePublishedHead._publishedPrev = volume;

            s_activePublishedHead = volume;
            s_activePublishedVolumeCount++;
        }

        private static void UnregisterPublishedVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            RemovePublishedVolumeNode(volume);
        }

        private static HectonVoxelVolume SelectPublishedVolumeEvictionCandidate(HectonVoxelVolume incoming)
        {
            for (HectonVoxelVolume candidate = s_activePublishedHead; candidate != null; candidate = candidate._publishedNext)
            {
                if (candidate == null || !candidate._runtimeDataReady)
                    return candidate;
            }

            if (incoming == null || s_activePublishedVolumeCount <= 0)
                return null;

            double3 incomingPosition = incoming._generationAbsoluteUniversePositionDouble;
            double farthestDistanceSq = -1d;
            HectonVoxelVolume farthest = null;
            for (HectonVoxelVolume candidate = s_activePublishedHead; candidate != null; candidate = candidate._publishedNext)
            {
                if (candidate == null)
                    return candidate;

                double distanceSq = math.lengthsq(candidate._generationAbsoluteUniversePositionDouble - incomingPosition);
                if (distanceSq <= farthestDistanceSq)
                    continue;

                farthestDistanceSq = distanceSq;
                farthest = candidate;
            }

            return farthest;
        }

        private static void RemovePublishedVolumeNode(HectonVoxelVolume volume)
        {
            if (volume == null || !volume._publishedRegistered)
                return;

            HectonVoxelVolume prev = volume._publishedPrev;
            HectonVoxelVolume next = volume._publishedNext;
            if (prev != null)
                prev._publishedNext = next;
            else if (ReferenceEquals(s_activePublishedHead, volume))
                s_activePublishedHead = next;

            if (next != null)
                next._publishedPrev = prev;

            volume._publishedPrev = null;
            volume._publishedNext = null;
            volume._publishedRegistered = false;
            if (s_activePublishedVolumeCount > 0)
                s_activePublishedVolumeCount--;
        }

        /// <summary>
        /// Samples the published runtime SDF payload at one runtime-space position.
        /// Positive densities indicate denser solid mass; negative densities indicate cavity/open water.
        /// </summary>
        /// <param name="worldPosition">Runtime-space sample position.</param>
        /// <param name="density">Decoded signed density in authored world-space units.</param>
        /// <param name="density01">Normalized solid-density strength mapped to 0..1.</param>
        /// <returns>True when the runtime sonar payload is available and the point can be sampled.</returns>
        public bool TrySampleDensity(Vector3 worldPosition, out float density, out float density01)
        {
            density = 0f;
            density01 = 0f;

            IDataVault vault = _cachedDataVault;
            if (!TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryReadPublishedSonarVaultPayload(
                        out NativeArray<byte>.ReadOnly encodedSdf,
                        out _,
                        out Vector3Int gridDimensions,
                        out Vector3 volumeOrigin,
                        out Vector3 voxelCellSize,
                        out float sdfRange,
                        out _,
                        requireAudioMaterial: false))
                {
                    return false;
                }

                return TrySamplePublishedDensity(
                    encodedSdf,
                    gridDimensions,
                    volumeOrigin,
                    voxelCellSize,
                    sdfRange,
                    worldPosition,
                    out density,
                    out density01);
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        /// <summary>
        /// Samples the published runtime SDF payload and returns only the decoded density.
        /// </summary>
        public bool TrySampleDensity(Vector3 worldPosition, out float density)
        {
            return TrySampleDensity(worldPosition, out density, out _);
        }

        public bool TrySampleSonarSdf(
            float3 runtimePosition,
            out float density,
            out float density01)
        {
            density = 0f;
            density01 = 0f;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            Vector3 worldPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return TrySampleDensity(worldPosition, out density, out density01);
        }

        /// <summary>
        /// Samples the published sonar material atlas at the nearest runtime-space SDF node.
        /// </summary>
        public bool TrySamplePublishedSonarAudioMaterialId(Vector3 worldPosition, out byte audioMaterialId)
        {
            audioMaterialId = DefaultPublishedSonarAudioMaterialId;

            IDataVault vault = _cachedDataVault;
            if (!TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryReadPublishedSonarVaultPayload(
                        out _,
                        out NativeArray<byte>.ReadOnly audioMaterialIds,
                        out Vector3Int gridDimensions,
                        out Vector3 volumeOrigin,
                        out Vector3 voxelCellSize,
                        out _,
                        out _,
                        requireSdf: false))
                {
                    return false;
                }

                float cellSizeX = Mathf.Max(0.0001f, voxelCellSize.x);
                float cellSizeY = Mathf.Max(0.0001f, voxelCellSize.y);
                float cellSizeZ = Mathf.Max(0.0001f, voxelCellSize.z);
                // R96 FIX: containment before nearest-node clamp (see IsWithinPublishedBounds).
                if (!IsWithinPublishedBounds(worldPosition, gridDimensions, volumeOrigin, cellSizeX, cellSizeY, cellSizeZ))
                    return false;

                float invCellSizeX = math.rcp(cellSizeX);
                float invCellSizeY = math.rcp(cellSizeY);
                float invCellSizeZ = math.rcp(cellSizeZ);
                int maxX = gridDimensions.x - 1;
                int maxY = gridDimensions.y - 1;
                int maxZ = gridDimensions.z - 1;
                int x = Mathf.Clamp((int)(((worldPosition.x - volumeOrigin.x) * invCellSizeX) + 0.5f), 0, maxX);
                int y = Mathf.Clamp((int)(((worldPosition.y - volumeOrigin.y) * invCellSizeY) + 0.5f), 0, maxY);
                int z = Mathf.Clamp((int)(((worldPosition.z - volumeOrigin.z) * invCellSizeZ) + 0.5f), 0, maxZ);
                int index = x + (gridDimensions.x * (y + (gridDimensions.y * z)));
                if ((uint)index >= (uint)audioMaterialIds.Length)
                    return false;

                audioMaterialId = audioMaterialIds[index];
                return true;
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        /// <summary>
        /// Raymarches the published SDF snapshot in runtime space and returns the first open-to-solid crossing.
        /// This path bypasses Unity Physics for scanner tools.
        /// </summary>
        public bool TryRaymarchPublishedSdf(
            Vector3 runtimeOrigin,
            Vector3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSdfRaycastHit hit)
        {
            hit = default;
            if (maxDistance <= 0f)
            {
                return false;
            }

            IDataVault vault = _cachedDataVault;
            if (!TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryReadPublishedSonarVaultPayload(
                        out NativeArray<byte>.ReadOnly encodedSdf,
                        out _,
                        out Vector3Int gridDimensions,
                        out Vector3 volumeOrigin,
                        out Vector3 voxelCellSize,
                        out float sdfRange,
                        out _,
                        requireAudioMaterial: false))
                {
                    return false;
                }

                Vector3 direction = NormalizeApprox(runtimeDirection, Vector3.forward);
                if (!TryResolveLegacyRaymarchInterval(
                        maxDistance,
                        runtimeOrigin,
                        direction,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        out float startDistance,
                        out float endDistance))
                {
                    return false;
                }

                float segmentDistance = Mathf.Max(0f, endDistance - startDistance);
                float requestedStep = Mathf.Max(0.05f, stepMeters);
                float step = ResolveLegacyRaymarchStep(segmentDistance, requestedStep);
                int stepCount = ResolveLegacyRaymarchStepCount(segmentDistance, step);
                float previousDensity = 0f;
                float previousDistance = 0f;
                bool hasPrevious = false;
                Vector3 previousPosition = runtimeOrigin;
                for (int i = 0; i <= stepCount; i++)
                {
                    float distance = Mathf.Min(endDistance, startDistance + i * step);
                    Vector3 position = runtimeOrigin + direction * distance;
                    if (!TrySamplePublishedDensity(
                            encodedSdf,
                            gridDimensions,
                            volumeOrigin,
                            voxelCellSize,
                            sdfRange,
                            position,
                            out float density,
                            out _))
                    {
                        continue;
                    }

                    bool nearSurface = Mathf.Abs(density) <= 0.0001f;
                    bool crossedSurface =
                        hasPrevious &&
                        ((previousDensity < -0.0001f && density >= 0.0001f) ||
                         (previousDensity > 0.0001f && density <= -0.0001f));

                    if (nearSurface || crossedSurface)
                    {
                        float resolvedDistance = distance;
                        Vector3 resolvedPoint = position;
                        if (crossedSurface)
                        {
                            float previousAbsDensity = Mathf.Abs(previousDensity);
                            float currentAbsDensity = Mathf.Abs(density);
                            float t = Mathf.Clamp01(previousAbsDensity / Mathf.Max(0.0001f, previousAbsDensity + currentAbsDensity));
                            resolvedDistance = Mathf.Lerp(previousDistance, distance, t);
                            resolvedPoint = previousPosition + (position - previousPosition) * t;
                        }

                        hit = new VoxelSdfRaycastHit
                        {
                            Point = resolvedPoint,
                            Normal = -direction,
                            Distance = Mathf.Max(0f, resolvedDistance),
                            Density = 0f,
                            Hit = 1
                        };
                        return true;
                    }

                    previousDensity = density;
                    previousDistance = distance;
                    previousPosition = position;
                    hasPrevious = true;
                    if (distance >= endDistance)
                        break;
                }

                return false;
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        private bool TryResolveLegacyRaymarchInterval(
            float maxDistance,
            Vector3 runtimeOrigin,
            Vector3 direction,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            out float startDistance,
            out float endDistance)
        {
            startDistance = 0f;
            endDistance = 0f;
            if (float.IsNaN(maxDistance) ||
                float.IsInfinity(maxDistance) ||
                maxDistance <= 0f ||
                !IsFinite(direction))
            {
                return false;
            }

            Vector3 safeCell = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(voxelCellSize.x)),
                Mathf.Max(0.0001f, Mathf.Abs(voxelCellSize.y)),
                Mathf.Max(0.0001f, Mathf.Abs(voxelCellSize.z)));
            Vector3 gridSpan = new Vector3(
                safeCell.x * Mathf.Max(1, gridDimensions.x - 1),
                safeCell.y * Mathf.Max(1, gridDimensions.y - 1),
                safeCell.z * Mathf.Max(1, gridDimensions.z - 1));
            Vector3 boundsMin = volumeOrigin;
            Vector3 boundsMax = volumeOrigin + gridSpan;
            float tMin = 0f;
            float tMax = maxDistance;
            if (!TryUpdateLegacyAxisInterval(runtimeOrigin.x, direction.x, boundsMin.x, boundsMax.x, ref tMin, ref tMax) ||
                !TryUpdateLegacyAxisInterval(runtimeOrigin.y, direction.y, boundsMin.y, boundsMax.y, ref tMin, ref tMax) ||
                !TryUpdateLegacyAxisInterval(runtimeOrigin.z, direction.z, boundsMin.z, boundsMax.z, ref tMin, ref tMax))
            {
                return false;
            }

            startDistance = Mathf.Max(0f, tMin);
            endDistance = Mathf.Min(maxDistance, tMax);
            return IsFinite(startDistance) &&
                   IsFinite(endDistance) &&
                   endDistance >= startDistance;
        }

        private static bool TryUpdateLegacyAxisInterval(
            float origin,
            float direction,
            float boundsMin,
            float boundsMax,
            ref float tMin,
            ref float tMax)
        {
            if (!IsFinite(origin) || !IsFinite(direction) || !IsFinite(boundsMin) || !IsFinite(boundsMax))
                return false;

            float min = Mathf.Min(boundsMin, boundsMax);
            float max = Mathf.Max(boundsMin, boundsMax);
            if (Mathf.Abs(direction) <= 0.0001f)
                return origin >= min && origin <= max;

            float inverseDirection = 1f / direction;
            float t0 = (min - origin) * inverseDirection;
            float t1 = (max - origin) * inverseDirection;
            float axisMin = Mathf.Min(t0, t1);
            float axisMax = Mathf.Max(t0, t1);
            tMin = Mathf.Max(tMin, axisMin);
            tMax = Mathf.Min(tMax, axisMax);
            return tMax >= tMin && tMax >= 0f;
        }

        private static float ResolveLegacyRaymarchStep(float maxDistance, float requestedStep)
        {
            float capStep = maxDistance / MaxLegacyRaymarchSteps;
            return float.IsNaN(capStep) || float.IsInfinity(capStep)
                ? requestedStep
                : Mathf.Max(requestedStep, capStep);
        }

        private static int ResolveLegacyRaymarchStepCount(float maxDistance, float stepMeters)
        {
            float rawStepCount = Mathf.Ceil(maxDistance / Mathf.Max(0.0001f, stepMeters));
            if (float.IsNaN(rawStepCount) || float.IsInfinity(rawStepCount) || rawStepCount >= MaxLegacyRaymarchSteps)
                return MaxLegacyRaymarchSteps;

            return Mathf.Max(1, (int)rawStepCount);
        }

        /// <summary>
        /// Samples the nearest published SDF grid node and resolves a cheap outward-facing surface normal.
        /// </summary>
        /// <param name="worldPosition">Runtime-space probe position near the target surface.</param>
        /// <param name="probeDistance">Reserved for API compatibility; nearest-grid sampling ignores it.</param>
        /// <param name="surfaceNormal">Resolved outward-facing normal.</param>
        /// <returns>True when a stable gradient could be resolved from the published SDF payload.</returns>
        public bool TrySampleSurfaceNormal(Vector3 worldPosition, float probeDistance, out Vector3 surfaceNormal)
        {
            surfaceNormal = Vector3.up;

            if (!TrySampleNearestPublishedGradient(worldPosition, out Vector3 gradient))
                return false;

            if (gradient.sqrMagnitude <= 0.000001f)
                return false;

            surfaceNormal = -NormalizeApprox(gradient, Vector3.up);
            return true;
        }

        private bool TrySampleNearestPublishedGradient(Vector3 worldPosition, out Vector3 gradient)
        {
            gradient = Vector3.zero;

            IDataVault vault = _cachedDataVault;
            if (!TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryReadPublishedSonarVaultPayload(
                        out NativeArray<byte>.ReadOnly encodedSdf,
                        out _,
                        out Vector3Int gridDimensions,
                        out Vector3 volumeOrigin,
                        out Vector3 voxelCellSize,
                        out float sdfRange,
                        out _,
                        requireAudioMaterial: false))
                {
                    return false;
                }

                float cellSizeX = Mathf.Max(0.0001f, voxelCellSize.x);
                float cellSizeY = Mathf.Max(0.0001f, voxelCellSize.y);
                float cellSizeZ = Mathf.Max(0.0001f, voxelCellSize.z);
                // R96 FIX: containment before gradient sampling (see IsWithinPublishedBounds).
                if (!IsWithinPublishedBounds(worldPosition, gridDimensions, volumeOrigin, cellSizeX, cellSizeY, cellSizeZ))
                    return false;

                int maxX = gridDimensions.x - 1;
                int maxY = gridDimensions.y - 1;
                int maxZ = gridDimensions.z - 1;
                int x = Mathf.Clamp((int)(((worldPosition.x - volumeOrigin.x) / cellSizeX) + 0.5f), 0, maxX);
                int y = Mathf.Clamp((int)(((worldPosition.y - volumeOrigin.y) / cellSizeY) + 0.5f), 0, maxY);
                int z = Mathf.Clamp((int)(((worldPosition.z - volumeOrigin.z) / cellSizeZ) + 0.5f), 0, maxZ);
                float center = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x, y, z);

                gradient = new Vector3(
                    x < maxX
                        ? (DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x + 1, y, z) - center) / cellSizeX
                        : (center - DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x - 1, y, z)) / cellSizeX,
                    y < maxY
                        ? (DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x, y + 1, z) - center) / cellSizeY
                        : (center - DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x, y - 1, z)) / cellSizeY,
                    z < maxZ
                        ? (DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x, y, z + 1) - center) / cellSizeZ
                        : (center - DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x, y, z - 1)) / cellSizeZ);
                return true;
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        /// <summary>
        /// Resolves a burrow route that stays inside solid density until a final breach point near the prey.
        /// </summary>
        public bool TryResolveBurrowAmbushRoute(
            Vector3 predatorWorldPosition,
            Vector3 preyWorldPosition,
            float seabedTriggerDistanceMeters,
            float breachOffsetMeters,
            out Vector3 solidAnchorWorldPosition,
            out Vector3 breachWorldPosition)
        {
            solidAnchorWorldPosition = default;
            breachWorldPosition = default;

            if (!_runtimeDataReady ||
                _gridDimension <= 0 ||
                _voxelSize <= 0f ||
                !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
            {
                return false;
            }

            Transform cachedTransform = transform;
            if (!TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out _,
                    out PublishedSonarSdfReadLease sdfLease))
            {
                return false;
            }

            try
            {
                if (!TryResolveNearestSolidDistance(
                        preyWorldPosition,
                        localBounds,
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        out float preySolidDistance) ||
                    preySolidDistance > Mathf.Max(1f, seabedTriggerDistanceMeters))
                {
                    return false;
                }

                Vector3 preyLocal = cachedTransform.InverseTransformPoint(preyWorldPosition);
                if (!TryResolveTopSolidAnchor(
                        cachedTransform,
                        localBounds,
                        preyLocal.x,
                        preyLocal.z,
                        Mathf.Max(_voxelSize, breachOffsetMeters),
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        out Vector3 localSolidAnchor))
                {
                    return false;
                }

                Vector3 localBreach = localSolidAnchor + new Vector3(0f, Mathf.Max(_voxelSize, breachOffsetMeters), 0f);
                Vector3 candidateSolidAnchor = cachedTransform.TransformPoint(localSolidAnchor);
                Vector3 candidateBreach = cachedTransform.TransformPoint(localBreach);
                if (!HasSolidDensityPath(
                        predatorWorldPosition,
                        candidateSolidAnchor,
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange))
                {
                    return false;
                }

                solidAnchorWorldPosition = candidateSolidAnchor;
                breachWorldPosition = candidateBreach;
                return true;
            }
            finally
            {
                ReleasePublishedSonarSdfPayloadReadLease(in sdfLease);
            }
        }

        private static int ResolveDominantAxis(Vector3 normal)
        {
            Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                return 0;

            return absNormal.y >= absNormal.z ? 1 : 2;
        }

        private static bool TrySamplePublishedDensity(
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            float sdfRange,
            Vector3 worldPosition,
            out float density,
            out float density01)
        {
            density = 0f;
            density01 = 0f;
            if (!encodedSdf.IsCreated ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1 ||
                sdfRange <= 0f)
            {
                return false;
            }

            float cellSizeX = Mathf.Max(0.0001f, voxelCellSize.x);
            float cellSizeY = Mathf.Max(0.0001f, voxelCellSize.y);
            float cellSizeZ = Mathf.Max(0.0001f, voxelCellSize.z);

            // R96 FIX (SDF read-model containment): reject positions outside the published volume
            // bounds (half-cell tolerance) instead of clamping them onto the border. The payload is
            // a shared single-slot vault snapshot — with 2+ active volumes, a clamped out-of-bounds
            // read silently returned the LAST PUBLISHER's border density for a query anywhere in
            // the world (wrong-volume corruption for sonar, burrow AI, and SDF-collision consumers).
            // Out-of-bounds now fails safe (false); callers own the no-data path per the fail-safe
            // query contract.
            if (!IsWithinPublishedBounds(worldPosition, gridDimensions, volumeOrigin, cellSizeX, cellSizeY, cellSizeZ))
                return false;

            float sampleX = Mathf.Clamp(
                (worldPosition.x - volumeOrigin.x) / cellSizeX,
                0f,
                gridDimensions.x - 1.001f);
            float sampleY = Mathf.Clamp(
                (worldPosition.y - volumeOrigin.y) / cellSizeY,
                0f,
                gridDimensions.y - 1.001f);
            float sampleZ = Mathf.Clamp(
                (worldPosition.z - volumeOrigin.z) / cellSizeZ,
                0f,
                gridDimensions.z - 1.001f);

            density = DecodePublishedDensity(encodedSdf, gridDimensions, sdfRange, sampleX, sampleY, sampleZ);
            density01 = Mathf.Clamp01(Mathf.Max(0f, density) / sdfRange);
            return true;
        }

        /// <summary>
        /// True when the runtime-space position lies inside the published grid AABB expanded by half
        /// a cell per axis. Guards every published-payload sample against reading a foreign volume's
        /// clamped border density (the payload slot is shared across published volumes).
        /// </summary>
        private static bool IsWithinPublishedBounds(
            Vector3 worldPosition,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            float cellSizeX,
            float cellSizeY,
            float cellSizeZ)
        {
            float spanX = cellSizeX * Mathf.Max(1, gridDimensions.x - 1);
            float spanY = cellSizeY * Mathf.Max(1, gridDimensions.y - 1);
            float spanZ = cellSizeZ * Mathf.Max(1, gridDimensions.z - 1);
            float tolX = cellSizeX * 0.5f;
            float tolY = cellSizeY * 0.5f;
            float tolZ = cellSizeZ * 0.5f;
            return worldPosition.x >= volumeOrigin.x - tolX && worldPosition.x <= volumeOrigin.x + spanX + tolX &&
                   worldPosition.y >= volumeOrigin.y - tolY && worldPosition.y <= volumeOrigin.y + spanY + tolY &&
                   worldPosition.z >= volumeOrigin.z - tolZ && worldPosition.z <= volumeOrigin.z + spanZ + tolZ;
        }

        private static float DecodePublishedDensity(
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            float sdfRange,
            float sampleX,
            float sampleY,
            float sampleZ)
        {
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int z0 = Mathf.FloorToInt(sampleZ);

            int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
            int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
            int z1 = Mathf.Min(z0 + 1, gridDimensions.z - 1);

            float tx = sampleX - x0;
            float ty = sampleY - y0;
            float tz = sampleZ - z0;

            float c000 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y0, z0);
            float c100 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y0, z0);
            float c010 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y1, z0);
            float c110 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y1, z0);
            float c001 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y0, z1);
            float c101 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y0, z1);
            float c011 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x0, y1, z1);
            float c111 = DecodePublishedDensityAt(encodedSdf, gridDimensions, sdfRange, x1, y1, z1);

            Span<float> cornerValues = stackalloc float[]
            {
                c000, c100, c010, c110,
                c001, c101, c011, c111
            };
            return Hecton8.PureLogic.Systems.VoxelSdfTrilinearInterpolationCalculator.Compute(cornerValues, tx, ty, tz);
        }

        private static float DecodePublishedDensityAt(
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            float sdfRange,
            int x,
            int y,
            int z)
        {
            int index = x +
                        (gridDimensions.x * (y + (gridDimensions.y * z)));
            if ((uint)index >= (uint)encodedSdf.Length)
                return 0f;

            float normalized = (encodedSdf[index] / 255f) * 2f - 1f;
            return normalized * sdfRange;
        }

        private static float ResolveCornerCoordinate(float coordinate, float signedFaceAxis, int gridDimension)
        {
            float cornerIndex;
            if (signedFaceAxis > 0.0001f)
            {
                cornerIndex = Mathf.Ceil(coordinate);
            }
            else if (signedFaceAxis < -0.0001f)
            {
                cornerIndex = Mathf.Floor(coordinate);
            }
            else
            {
                cornerIndex = Mathf.Round(coordinate);
            }

            return Mathf.Clamp(cornerIndex, 0f, gridDimension);
        }

        /// <summary>
        /// Resets cave-owned runtime children so pooled volumes do not leak
        /// previous cave dressing or entrance readability state into the next build.
        /// </summary>
        public void PrepareForReuse()
        {
            UnregisterPublishedVolume(this);
            _deltaProcessor?.UnregisterVolume(this);
            UnregisterTerrainHoles();
            ClearPublishedSonarSdf();
            PrewarmColliderChunkHierarchy();
            caveKey = 0L;
            generationPosition = Vector3.zero;
            preset = null;
            _engine = null;
            _deltaProcessor = null;
            _generationAbsoluteUniversePosition = Vector3.zero;
            _generationAbsoluteUniversePositionDouble = double3.zero;
            _nodes = Array.Empty<CaveNode>();
            _tunnels = Array.Empty<CaveTunnel>();
            _entrances = Array.Empty<CaveEntrance>();
            _structures = Array.Empty<CaveStructure>();
            _craterStamps.Clear();
            _resourceCraterClusterStamps.Clear();
            _collapseImpulseContacts = Array.Empty<SpatialQueryHit>();
            _collapseImpulseBodies = Array.Empty<Rigidbody>();
            _craterStampCount = 0;
            _resourceCraterClusterCount = 0;
            _lastCollapseClusterValid = false;
            _lastCollapseAbsoluteCenter = double3.zero;
            _runtimeDataReady = false;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _seed = 0u;
            _gridDimension = 0;
            _voxelSize = 0f;
            _lodLevel = 0;
            _buildCollider = true;
            _caveParams = default;
            _terrainHoleHandles.Clear();
            _terrainHoleHandleCount = 0;
            _runtimeStamp++;
            CacheRuntimeComponentsCold();
            SetBakeState(VoxelBakeState.Idle);

            ToggleChildRoot(CaveDressingRootName, false);
            ToggleChildRoot(EntranceQualityRootName, false);
            ToggleChildRoot(EntranceMarkersRootName, false);
        }

        /// <summary>
        /// Ensures the pooled collider chunk hierarchy exists and can serve the requested chunk count.
        /// </summary>
        private void EnsureColliderChunkCapacity(int chunkCount)
        {
            int clampedCount = Mathf.Clamp(chunkCount, 1, MaxColliderChunkCount);
            _colliderChunkRoot = GetOrCreateRuntimeRoot(ColliderChunkRootName);

            for (int i = 0; i < clampedCount; i++)
            {
                if (_colliderChunkColliders[i] != null)
                {
                    _colliderChunkColliders[i].gameObject.layer = HectonLayerMasks.VoxelCave;
                    _colliderChunkColliders[i].enabled = false;
                }

                if (_colliderChunkBakeProxies[i] == null)
                {
                    GameObject proxyObject = new GameObject(ColliderChunkProxyRuntimeName); // COLD ALLOC: GameObject[1] - isolated async bake proxy collider - owner: HectonVoxelVolume
                    proxyObject.layer = HectonLayerMasks.VoxelProxy;
                    Transform proxyTransform = proxyObject.transform;
                    proxyTransform.SetParent(_colliderChunkRoot, false);
                    proxyTransform.localPosition = Vector3.zero;
                    proxyTransform.localRotation = Quaternion.identity;
                    proxyTransform.localScale = Vector3.one;

                    BoxCollider proxy = proxyObject.AddComponent<BoxCollider>();
                    proxy.enabled = false;
                    proxy.isTrigger = false;
                    _colliderChunkBakeProxies[i] = proxy;
                }
                else
                {
                    _colliderChunkBakeProxies[i].gameObject.layer = HectonLayerMasks.VoxelProxy;
                }
            }

            if (!_colliderChunkRoot.gameObject.activeSelf)
                _colliderChunkRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Fills the fixed collider chunk hierarchy during pool preparation, then parks it disabled.
        /// Runtime collider splitting must not create GameObjects or add components.
        /// </summary>
        public void PrewarmColliderChunkHierarchy()
        {
            EnsureColliderChunkCapacity(MaxColliderChunkCount);
            ResetColliderChunks(false);
        }

        /// <summary>
        /// Validates the prewarmed fixed collider hierarchy without allocating Unity objects.
        /// </summary>
        public bool TryUsePrewarmedColliderChunkCapacity(int chunkCount)
        {
            int clampedCount = Mathf.Clamp(chunkCount, 1, MaxColliderChunkCount);
            if (_colliderChunkRoot == null ||
                _colliderChunkBakeProxies == null ||
                _colliderChunkBakeProxies.Length < clampedCount ||
                _colliderChunkColliders == null ||
                _colliderChunkColliders.Length < clampedCount)
            {
                return false;
            }

            for (int i = 0; i < _colliderChunkBakeProxies.Length; i++)
            {
                BoxCollider proxy = _colliderChunkBakeProxies[i];
                bool withinRequestedCapacity = i < clampedCount;
                if (proxy == null && withinRequestedCapacity)
                    return false;

                MeshCollider collider = i < _colliderChunkColliders.Length ? _colliderChunkColliders[i] : null;
                if (collider != null)
                {
                    collider.enabled = false;
                    collider.gameObject.layer = HectonLayerMasks.VoxelCave;
                }

                if (proxy == null)
                    continue;

                proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;
                proxy.center = SanitizeColliderChunkProxyCenter(proxy.center, Vector3.zero);
                proxy.size = SanitizeColliderChunkProxySize(proxy.size, Vector3.one * MinColliderChunkProxySize);
                if (!withinRequestedCapacity && proxy.enabled)
                    proxy.enabled = false;
            }

            if (!_colliderChunkRoot.gameObject.activeSelf)
                _colliderChunkRoot.gameObject.SetActive(true);

            return true;
        }

        /// <summary>
        /// Returns the pooled child MeshCollider for the requested distributed collision chunk.
        /// </summary>
        public MeshCollider GetColliderChunkCollider(int index)
        {
            if (_colliderChunkColliders == null ||
                (uint)index >= (uint)_colliderChunkColliders.Length)
                return null;

            return _colliderChunkColliders[index];
        }

        /// <summary>
        /// Enables the primitive runtime collision proxy for one distributed collision chunk.
        /// </summary>
        internal void ConfigureColliderChunkBakeProxy(int index, Vector3 center, Vector3 size)
        {
            if (_colliderChunkBakeProxies == null ||
                (uint)index >= (uint)_colliderChunkBakeProxies.Length)
                return;

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy == null)
                return;

            Vector3 safeCenter = SanitizeColliderChunkProxyCenter(center, proxy.center);
            Vector3 safeSize = SanitizeColliderChunkProxySize(size, proxy.size);
            proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;
            if (!proxy.gameObject.activeSelf)
                proxy.gameObject.SetActive(true);

            proxy.center = safeCenter;
            proxy.size = safeSize;
            proxy.enabled = true;
        }

        /// <summary>
        /// Returns the isolated primitive collision proxy for one distributed collision chunk.
        /// </summary>
        internal BoxCollider GetColliderChunkBakeProxy(int index)
        {
            if (_colliderChunkBakeProxies == null ||
                (uint)index >= (uint)_colliderChunkBakeProxies.Length)
                return null;

            return _colliderChunkBakeProxies[index];
        }

        /// <summary>
        /// True when a staged collider chunk mesh exists and its PhysX bake has completed,
        /// so the deferred upload drain may commit it to the pooled MeshCollider.
        /// </summary>
        internal bool IsDeferredColliderChunkUploadReady(int index)
        {
            if (_colliderChunkBakeMeshes == null ||
                (uint)index >= (uint)_colliderChunkBakeMeshes.Length)
                return false;

            return _colliderChunkBakeMeshReady[index] && _colliderChunkBakeMeshes[index] != null;
        }

        internal Mesh GetColliderChunkBakeMesh(int index)
        {
            if (_colliderChunkBakeMeshes == null ||
                (uint)index >= (uint)_colliderChunkBakeMeshes.Length)
                return null;

            return _colliderChunkBakeMeshes[index];
        }

        /// <summary>
        /// Marks a staged collider chunk mesh as baked and commit-ready. The mesh must be the
        /// one obtained from <see cref="GetOrCreateColliderChunkBakeMesh"/> for the same index,
        /// already filled and already run through Physics.BakeMesh off the main thread.
        /// </summary>
        internal bool AssignColliderChunkBakeMesh(int index, Mesh mesh)
        {
            if (mesh == null ||
                _colliderChunkBakeMeshes == null ||
                (uint)index >= (uint)_colliderChunkBakeMeshes.Length)
                return false;

            if (!ReferenceEquals(_colliderChunkBakeMeshes[index], mesh))
            {
                Mesh previous = _colliderChunkBakeMeshes[index];
                if (previous != null)
                {
                    previous.Clear(false);
                    if (!global::HectonVoxelEngine.ReleaseVoxelPhysicsBakeMesh(previous))
                        DestroyOwnedObject(previous);
                }

                _colliderChunkBakeMeshes[index] = mesh;
            }

            _colliderChunkBakeMeshReady[index] = true;
            return true;
        }

        /// <summary>
        /// Disables the primitive runtime collision proxy for one collider chunk.
        /// </summary>
        internal void DisableColliderChunkBakeProxy(int index)
        {
            if (_colliderChunkBakeProxies == null ||
                (uint)index >= (uint)_colliderChunkBakeProxies.Length)
                return;

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy == null)
                return;

            proxy.enabled = false;
            ResetColliderChunkBakeProxyShape(index);
        }

        /// <summary>
        /// Disables all primitive runtime collision proxies owned by this volume.
        /// </summary>
        internal void DisableColliderChunkBakeProxies()
        {
            if (_colliderChunkBakeProxies == null)
                return;

            for (int i = 0; i < _colliderChunkBakeProxies.Length; i++)
            {
                DisableColliderChunkBakeProxy(i);
            }
        }

        /// <summary>
        /// Disables runtime collider presentation for the cinematic-fake path without mutating
        /// MeshCollider.sharedMesh on the deformation frame.
        /// </summary>
        internal void DisableColliderChunksForCinematicFake()
        {
            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            int proxyCount = _colliderChunkBakeProxies != null ? _colliderChunkBakeProxies.Length : 0;
            int chunkCount = Mathf.Max(colliderCount, proxyCount);
            for (int i = 0; i < chunkCount; i++)
            {
                MeshCollider collider = i < colliderCount ? _colliderChunkColliders[i] : null;
                if (collider != null)
                {
                    collider.enabled = false;
                    if (collider.gameObject.activeSelf)
                        collider.gameObject.SetActive(false);
                }

                DisableColliderChunkBakeProxy(i);
                ResetColliderChunkBakeProxyShape(i);
            }

            ClearColliderChunkBakeMeshes();
        }

        /// <summary>
        /// Runtime collider mesh allocation is disabled; primitive proxies own voxel collision.
        /// </summary>
        public Mesh GetOrCreateColliderChunkMesh(int index)
        {
            return null;
        }

        /// <summary>
        /// Returns the staged bake mesh for one collider chunk, acquiring a pooled mesh from
        /// the engine physics-bake pool on first use. Returns null when the pool is exhausted
        /// (the pool publishes its own telemetry warning); the box proxy then stays active as
        /// the degraded collision route for this chunk.
        /// </summary>
        internal Mesh GetOrCreateColliderChunkBakeMesh(int index)
        {
            if (_colliderChunkBakeMeshes == null ||
                (uint)index >= (uint)_colliderChunkBakeMeshes.Length)
                return null;

            Mesh mesh = _colliderChunkBakeMeshes[index];
            if (mesh == null)
            {
                mesh = global::HectonVoxelEngine.AcquireVoxelPhysicsBakeMesh();
                if (mesh == null)
                    return null;

                _colliderChunkBakeMeshes[index] = mesh;
            }

            _colliderChunkBakeMeshReady[index] = false;
            return mesh;
        }

        /// <summary>
        /// Enables the primitive runtime collision proxy for the requested chunk. Runtime PhysX mesh publication is disabled.
        /// </summary>
        internal bool EnableColliderChunkProxy(int index)
        {
            if (_colliderChunkBakeProxies == null ||
                (uint)index >= (uint)_colliderChunkBakeProxies.Length)
            {
                return false;
            }

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy == null)
                return false;

            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            if (index < colliderCount)
            {
                MeshCollider collider = _colliderChunkColliders[index];
                if (collider != null)
                    collider.enabled = false;
            }

            proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;
            if (!proxy.gameObject.activeSelf)
                proxy.gameObject.SetActive(true);

            proxy.center = SanitizeColliderChunkProxyCenter(proxy.center, Vector3.zero);
            proxy.size = SanitizeColliderChunkProxySize(proxy.size, Vector3.one * MinColliderChunkProxySize);
            proxy.enabled = true;
            return true;
        }

        /// <summary>
        /// Publishes the staged, pre-baked collider chunk mesh to the pooled MeshCollider and
        /// retires the primitive box proxy for that chunk. Called from the budgeted deferred
        /// upload drain; the PhysX cook already happened off the main thread, so the
        /// sharedMesh assignment here reuses the baked data by instance id.
        /// </summary>
        internal bool CommitDeferredColliderChunkUpload(int index)
        {
            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;
            if (index < 0 || index >= colliderCount || index >= bakeMeshCount)
                return false;

            Mesh stagedMesh = _colliderChunkBakeMeshes[index];
            if (stagedMesh == null || !_colliderChunkBakeMeshReady[index] || stagedMesh.vertexCount < 3)
                return false;

            MeshCollider collider = _colliderChunkColliders[index];
            if (collider == null)
                return false;

            _colliderChunkBakeMeshes[index] = null;
            _colliderChunkBakeMeshReady[index] = false;

            Mesh previousLiveMesh = _colliderChunkMeshes != null && index < _colliderChunkMeshes.Length
                ? _colliderChunkMeshes[index]
                : null;

            collider.gameObject.layer = HectonLayerMasks.VoxelCave;
            if (!collider.gameObject.activeSelf)
                collider.gameObject.SetActive(true);

            collider.sharedMesh = stagedMesh;
            if (_colliderChunkMeshes != null && index < _colliderChunkMeshes.Length)
                _colliderChunkMeshes[index] = stagedMesh;

            if (previousLiveMesh != null && !ReferenceEquals(previousLiveMesh, stagedMesh))
            {
                previousLiveMesh.Clear(false);
                if (!global::HectonVoxelEngine.ReleaseVoxelPhysicsBakeMesh(previousLiveMesh))
                    DestroyOwnedObject(previousLiveMesh);
            }

            // voxels.md: physics interaction is blocked until collider bake is complete.
            collider.enabled = _bakeState == VoxelBakeState.Complete;
            DisableColliderChunkBakeProxy(index);

            if (collider.enabled)
            {
                var pos = transform.position;
                float chunkSizeX = _gridDimension > 0 && _voxelSize > 0f ? _gridDimension * _voxelSize : 100f;
                float chunkSizeZ = chunkSizeX;
                float3 size = new float3(chunkSizeX, chunkSizeX, chunkSizeZ);
                float3 minCorner = new float3(pos.x - chunkSizeX * 0.5f, pos.y, pos.z - chunkSizeZ * 0.5f);
                WorldChunkPhysicsBakedSignal signal = new WorldChunkPhysicsBakedSignal
                {
                    ChunkX = (int)math.floor(pos.x / chunkSizeX),
                    ChunkZ = (int)math.floor(pos.z / chunkSizeZ),
                    TerrainEntityHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                    Frame = (uint)UnityEngine.Time.frameCount,
                    TerrainPosition = minCorner,
                    TerrainSize = size,
                    Flags = WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagHeightmapSynced
                };
                WorldChunkPhysicsBakedEvents.TryPublish(in signal);
            }

            return true;
        }

        /// <summary>
        /// Clears staged collider bake meshes without touching the currently published live collider meshes.
        /// </summary>
        internal void ClearColliderChunkBakeMeshes()
        {
            if (_colliderChunkBakeMeshes == null)
                return;

            for (int i = 0; i < _colliderChunkBakeMeshes.Length; i++)
            {
                _colliderChunkBakeMeshReady[i] = false;
                Mesh mesh = _colliderChunkBakeMeshes[i];
                if (mesh != null)
                    mesh.Clear(false);
            }
        }

        /// <summary>
        /// Releases ownership of a staged collider mesh after fail-closed proxy activation.
        /// </summary>
        /// <param name="index">Collider chunk index.</param>
        internal void DetachColliderChunkBakeMesh(int index)
        {
            if (index < 0)
                return;

            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            if (index < colliderCount)
            {
                MeshCollider collider = _colliderChunkColliders[index];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            EnableColliderChunkProxy(index);
            int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;
            if (index < bakeMeshCount)
            {
                _colliderChunkBakeMeshes[index] = null;
                _colliderChunkBakeMeshReady[index] = false;
            }
        }

        /// <summary>
        /// Releases a staged collider bake mesh when no deferred bake teardown owns it.
        /// </summary>
        internal void ReleaseColliderChunkBakeMesh(int index)
        {
            if (index < 0)
                return;

            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            if (index < colliderCount)
            {
                MeshCollider collider = _colliderChunkColliders[index];
                if (collider != null)
                    collider.enabled = false;
            }

            EnableColliderChunkProxy(index);
            int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;
            if (index >= bakeMeshCount)
                return;

            _colliderChunkBakeMeshReady[index] = false;
            Mesh bakeMesh = _colliderChunkBakeMeshes[index];
            _colliderChunkBakeMeshes[index] = null;
            if (bakeMesh == null)
                return;

            bakeMesh.Clear(false);
            if (!global::HectonVoxelEngine.ReleaseVoxelPhysicsBakeMesh(bakeMesh))
                DestroyOwnedObject(bakeMesh);
        }

        /// <summary>
        /// Clears all pooled collider chunks. When destroyMeshes is true the mesh instances are destroyed permanently.
        /// </summary>
        public void ResetColliderChunks(bool destroyMeshes)
        {
            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            int proxyCount = _colliderChunkBakeProxies != null ? _colliderChunkBakeProxies.Length : 0;
            int meshCount = _colliderChunkMeshes != null ? _colliderChunkMeshes.Length : 0;
            int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;
            int chunkCount = Mathf.Max(Mathf.Max(colliderCount, proxyCount), Mathf.Max(meshCount, bakeMeshCount));
            for (int i = 0; i < chunkCount; i++)
            {
                if (i < bakeMeshCount)
                    _colliderChunkBakeMeshReady[i] = false;

                MeshCollider collider = i < colliderCount ? _colliderChunkColliders[i] : null;
                if (collider != null)
                {
                    collider.enabled = false;
                    if (destroyMeshes)
                        collider.sharedMesh = null;
                    if (collider.gameObject.activeSelf)
                        collider.gameObject.SetActive(false);
                }

                DisableColliderChunkBakeProxy(i);
                ResetColliderChunkBakeProxyShape(i);

                Mesh mesh = i < meshCount ? _colliderChunkMeshes[i] : null;
                Mesh bakeMesh = i < bakeMeshCount ? _colliderChunkBakeMeshes[i] : null;
                if (mesh != null)
                {
                    if (global::HectonVoxelEngine.ReleaseVoxelPhysicsBakeMesh(mesh))
                    {
                        _colliderChunkMeshes[i] = null;
                    }
                    else if (destroyMeshes)
                    {
                        DestroyOwnedObject(mesh);
                        _colliderChunkMeshes[i] = null;
                    }
                    else
                    {
                        mesh.Clear(false);
                    }
                }

                if (bakeMesh != null)
                {
                    if (global::HectonVoxelEngine.ReleaseVoxelPhysicsBakeMesh(bakeMesh))
                    {
                        _colliderChunkBakeMeshes[i] = null;
                    }
                    else if (destroyMeshes)
                    {
                        DestroyOwnedObject(bakeMesh);
                        _colliderChunkBakeMeshes[i] = null;
                    }
                    else
                    {
                        bakeMesh.Clear(false);
                    }
                }
            }

            if (_colliderChunkRoot != null && _colliderChunkRoot.gameObject.activeSelf)
                _colliderChunkRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Enables collider chunks in the inclusive range [0, activeCount) and disables the rest.
        /// </summary>
        public void SetActiveColliderChunkCount(int activeCount)
        {
            if (_colliderChunkBakeProxies == null)
                return;

            int clampedActive = Mathf.Clamp(activeCount, 0, _colliderChunkBakeProxies.Length);
            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            for (int i = 0; i < _colliderChunkBakeProxies.Length; i++)
            {
                BoxCollider proxy = _colliderChunkBakeProxies[i];
                if (proxy == null)
                    continue;

                bool shouldBeActive = i < clampedActive;
                proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;
                if (!shouldBeActive && proxy.enabled)
                    proxy.enabled = false;

                if (proxy.gameObject.activeSelf != shouldBeActive)
                    proxy.gameObject.SetActive(shouldBeActive);

                if (i < colliderCount)
                {
                    MeshCollider collider = _colliderChunkColliders[i];
                    if (collider != null)
                        collider.enabled = false;
                }
            }

            if (_colliderChunkRoot != null)
                _colliderChunkRoot.gameObject.SetActive(clampedActive > 0);

            RefreshBakePresentation();
        }

        /// <summary>
        /// Captures the immutable cave generation payload needed to rebuild this
        /// pooled volume after runtime SDF edits such as crater carving.
        /// </summary>
        public void ConfigureRuntimeData(
            HectonVoxelEngine engine,
            uint seed,
            Vector3 worldCenter,
            Vector3 absoluteUniverseOffset,
            double3 absoluteUniverseOffsetDouble,
            CavePreset cavePreset,
            int gridDimension,
            float voxelSize,
            int lodLevel,
            CaveGenerationParams caveParams,
            NativeArray<CaveNode> nodes,
            NativeArray<CaveTunnel> tunnels,
            NativeArray<CaveEntrance> entrances,
            NativeArray<CaveStructure> structures,
            bool buildCollider)
        {
            _engine = engine;
            _deltaProcessor = engine != null ? engine.DeltaProcessor : null;
            _seed = seed;
            generationPosition = worldCenter;
            _generationAbsoluteUniversePositionDouble = new double3(worldCenter.x, worldCenter.y, worldCenter.z) + absoluteUniverseOffsetDouble;
            _generationAbsoluteUniversePosition = HectonFloatingOrigin.ToRuntimePosition(_generationAbsoluteUniversePositionDouble);
            preset = cavePreset;
            _gridDimension = gridDimension;
            _voxelSize = voxelSize;
            _lodLevel = Mathf.Max(0, lodLevel);
            _caveParams = caveParams;
            _buildCollider = buildCollider;

            // COLD ALLOC: CaveNode[nodes.Length] - runtime room graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            CaveNode[] nodeSnapshots = new CaveNode[nodes.Length];
            int validNodeCount = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                CaveNode source = nodes[i];
                if (!TryBuildRuntimeNodeSnapshot(in source, absoluteUniverseOffset, out CaveNode snapshot))
                    continue;

                nodeSnapshots[validNodeCount++] = snapshot;
            }
            if (validNodeCount != nodeSnapshots.Length)
                Array.Resize(ref nodeSnapshots, validNodeCount);
            _nodes = nodeSnapshots;

            // COLD ALLOC: CaveTunnel[tunnels.Length] - runtime tunnel graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            CaveTunnel[] tunnelSnapshots = new CaveTunnel[tunnels.Length];
            int validTunnelCount = 0;
            for (int i = 0; i < tunnels.Length; i++)
            {
                CaveTunnel source = tunnels[i];
                if (!TryBuildRuntimeTunnelSnapshot(in source, absoluteUniverseOffset, out CaveTunnel snapshot))
                    continue;

                tunnelSnapshots[validTunnelCount++] = snapshot;
            }
            if (validTunnelCount != tunnelSnapshots.Length)
                Array.Resize(ref tunnelSnapshots, validTunnelCount);
            _tunnels = tunnelSnapshots;

            // COLD ALLOC: CaveEntrance[entrances.Length] - runtime entrance snapshot for terrain-hole/skirt rebuilds - owner: HectonVoxelVolume
            CaveEntrance[] entranceSnapshots = new CaveEntrance[entrances.Length];
            int validEntranceCount = 0;
            for (int i = 0; i < entrances.Length; i++)
            {
                CaveEntrance source = entrances[i];
                if (!TryBuildRuntimeEntranceSnapshot(in source, absoluteUniverseOffset, out CaveEntrance snapshot))
                    continue;

                entranceSnapshots[validEntranceCount++] = snapshot;
            }
            if (validEntranceCount != entranceSnapshots.Length)
                Array.Resize(ref entranceSnapshots, validEntranceCount);
            _entrances = entranceSnapshots;

            // COLD ALLOC: CaveStructure[structures.Length] - runtime structure snapshot for crater rebuilds - owner: HectonVoxelVolume
            CaveStructure[] structureSnapshots = new CaveStructure[structures.Length];
            int validStructureCount = 0;
            for (int i = 0; i < structures.Length; i++)
            {
                CaveStructure source = structures[i];
                if (!TryBuildRuntimeStructureSnapshot(in source, absoluteUniverseOffset, out CaveStructure snapshot))
                    continue;

                structureSnapshots[validStructureCount++] = snapshot;
            }
            if (validStructureCount != structureSnapshots.Length)
                Array.Resize(ref structureSnapshots, validStructureCount);
            _structures = structureSnapshots;

            _craterStamps.Clear();
            _resourceCraterClusterStamps.Clear();

            if (_collapseImpulseContacts.Length != CollapseImpulseColliderCapacity)
            {
                // COLD ALLOC: SpatialQueryHit[CollapseImpulseColliderCapacity] - collapse impulse registered-contact buffer - owner: HectonVoxelVolume
                _collapseImpulseContacts = new SpatialQueryHit[CollapseImpulseColliderCapacity];
            }

            if (_collapseImpulseBodies.Length != CollapseImpulseBodyCapacity)
            {
                // COLD ALLOC: Rigidbody[CollapseImpulseBodyCapacity] - collapse impulse dedupe buffer - owner: HectonVoxelVolume
                _collapseImpulseBodies = new Rigidbody[CollapseImpulseBodyCapacity];
            }

            _terrainHoleHandles.Clear();

            _craterStampCount = 0;
            _resourceCraterClusterCount = 0;
            _lastCollapseClusterValid = false;
            _lastCollapseAbsoluteCenter = double3.zero;
            _terrainHoleHandleCount = 0;
            _runtimeDataReady = true;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _runtimeStamp++;
            CacheRuntimeComponentsCold();
            SetBakeState(VoxelBakeState.Complete);
            RegisterPublishedVolume(this);
            InteractableRegistry.RegisterTree(this);
            _deltaProcessor?.RegisterVolume(this);
        }

        /// <summary>
        /// Publishes a compact encoded SDF snapshot for diegetic PDA sonar map rendering.
        /// </summary>
        internal async Awaitable<bool> PublishSonarSdfSnapshotAsync(
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            NativeArray<float> smoothDensityField,
            CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _publishedSonarSnapshotPublishInFlight, 1, 0) != 0)
                return false;

            Volatile.Write(ref _publishedSonarPublishAbortRequested, 0);
            try
            {
                int totalPointCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;
                if (totalPointCount <= 0 ||
                    (uint)totalPointCount > (uint)PublishedSonarMaxPointCount ||
                    !smoothDensityField.IsCreated ||
                    smoothDensityField.Length < totalPointCount)
                {
                    ClearPublishedSonarSdf();
                    return false;
                }

                if (!IsFinite(volumeOrigin) ||
                    !IsFinite(voxelCellSize) ||
                    voxelCellSize.x <= 0f ||
                    voxelCellSize.y <= 0f ||
                    voxelCellSize.z <= 0f)
                {
                    ClearPublishedSonarSdf();
                    return false;
                }

                IDataVault vault = _cachedDataVault;
                if (vault == null || !TryEnsurePublishedSonarVaultPayloadCapacity(vault))
                    return false;

                float sdfRange = Mathf.Max(
                    0.25f,
                    Mathf.Max(voxelCellSize.x, Mathf.Max(voxelCellSize.y, voxelCellSize.z)) * 8f);
                if (!math.isfinite(sdfRange) || sdfRange <= 0f)
                    return false;

                int nextVersion = _publishedSonarVersion + 1;
                bool payloadPublished = await TryPublishSonarSdfVaultPayloadAsync(
                    totalPointCount,
                    gridDimensions,
                    volumeOrigin,
                    voxelCellSize,
                    sdfRange,
                    nextVersion,
                    smoothDensityField,
                    cancellationToken);
                if (!payloadPublished)
                {
                    return false;
                }

                _publishedSonarGridDimensions = gridDimensions;
                _publishedSonarOrigin = volumeOrigin;
                _publishedSonarCellSize = voxelCellSize;
                _publishedSonarSdfRange = sdfRange;
                _publishedSonarVersion = nextVersion;
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _publishedSonarSnapshotPublishInFlight, 0);
            }
        }

        private async Awaitable<bool> TryPublishSonarSdfVaultPayloadAsync(
            int totalPointCount,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            float sdfRange,
            int version,
            NativeArray<float> smoothDensityField,
            CancellationToken cancellationToken)
        {
            if (totalPointCount <= 0 ||
                !smoothDensityField.IsCreated ||
                smoothDensityField.Length < totalPointCount)
            {
                return false;
            }

            IDataVault vault = _cachedDataVault;
            if (vault == null)
                return false;

            if (Interlocked.CompareExchange(ref s_publishedSonarVaultPublishInFlight, 1, 0) != 0)
                return false;

            VaultGenerationHandle<byte> sdfHandle = default;
            VaultGenerationHandle<byte> audioMaterialHandle = default;
            JobHandle encodeHandle = default;
            bool encodeScheduled = false;
            ulong writeGuardMask = 0UL;
            try
            {
                if (!TryResolvePublishedSonarDescriptorOrigin(volumeOrigin, out Vector3 descriptorOrigin))
                {
                    TryClearSonarSdfVaultDescriptor(version, volumeOrigin);
                    return false;
                }

                if (!TryResolvePublishedSonarVaultPayloadHandles(
                        vault,
                        out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle,
                        out sdfHandle,
                        out audioMaterialHandle))
                {
                    return false;
                }

                if (!TryAcquirePublishedSonarPayloadWriteGuard(vault, out writeGuardMask))
                    return false;

                if (!vault.TryResolveHandle(in sdfHandle, out NativeArray<byte> vaultSdf) ||
                    !vault.TryResolveHandle(in audioMaterialHandle, out NativeArray<byte> vaultAudioMaterialIds) ||
                    !vaultSdf.IsCreated ||
                    !vaultAudioMaterialIds.IsCreated ||
                    vaultSdf.Length < totalPointCount ||
                    vaultAudioMaterialIds.Length < totalPointCount)
                {
                    return false;
                }

                if (!TryInvalidatePublishedSonarVaultDescriptorGuarded(vault, in descriptorHandle))
                    return false;

                float inverseRange = 1f / sdfRange;
                encodeHandle = new PublishedSonarSdfEncodeJob
                {
                    SmoothDensityField = smoothDensityField,
                    EncodedSdf = vaultSdf,
                    AudioMaterialIds = vaultAudioMaterialIds,
                    InverseRange = inverseRange,
                    DefaultAudioMaterialId = DefaultPublishedSonarAudioMaterialId
                }.Schedule(totalPointCount, 256);
                encodeScheduled = true;

                bool encodeCancellationRequested = false;
                bool encodeTimedOut = false;
                int encodeWaitFrames = 0;
                while (!encodeHandle.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested ||
                        Volatile.Read(ref _publishedSonarPublishAbortRequested) != 0)
                    {
                        encodeCancellationRequested = true;
                        break;
                    }

                    if (encodeWaitFrames++ >= PublishedSonarEncodeWaitWatchdogFrames)
                    {
                        encodeTimedOut = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Hecton8.Core.H8Debug.LogError("[HectonVoxelVolume] Published sonar SDF encode watchdog elapsed; forcing completion and dropping descriptor publish.");
#endif
                        break;
                    }

                    await AwaitableDebtMonitor.NextFrameAsync();
                }

                if (encodeHandle.IsCompleted)
                    DispatcherJobFence.TryFinalizeCompleted(ref encodeHandle);
                else
                    DispatcherJobFence.TryComplete(ref encodeHandle, forceComplete: true);

                encodeScheduled = false;

                uint sdfGeneration = sdfHandle.Generation;
                uint audioMaterialGeneration = audioMaterialHandle.Generation;
                if (encodeCancellationRequested ||
                    encodeTimedOut ||
                    cancellationToken.IsCancellationRequested ||
                    sdfGeneration == 0u ||
                    audioMaterialGeneration == 0u ||
                    Volatile.Read(ref _publishedSonarPublishAbortRequested) != 0)
                {
                    return false;
                }

                if (!vault.TryResolveHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors))
                    return false;

                if (descriptors.IsCreated && descriptors.Length > 0)
                {
                    VoxelSdfPayloadDescriptorDTO descriptor = default;
                    descriptor.VolumeOrigin = new float3(descriptorOrigin.x, descriptorOrigin.y, descriptorOrigin.z);
                    descriptor.GridDimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z);
                    descriptor.VoxelCellSize = new float3(
                        math.max(0.0001f, math.abs(voxelCellSize.x)),
                        math.max(0.0001f, math.abs(voxelCellSize.y)),
                        math.max(0.0001f, math.abs(voxelCellSize.z)));
                    descriptor.SdfRangeMeters = math.max(0.0001f, math.isfinite(sdfRange) ? sdfRange : 0f);
                    descriptor.ByteCount = totalPointCount;
                    descriptor.BufferId = unchecked((uint)(int)BufferID.VoxelSdfTexture3D);
                    descriptor.BufferGeneration = sdfGeneration;
                    descriptor.SdfVersion = unchecked((uint)math.max(0, version));
                    descriptor.OwnerSystemId = (uint)SystemID.WorldStreaming;
                    descriptor.Flags = VoxelSdfPayloadDescriptorDTO.FlagValid;
                    descriptor.AudioMaterialByteCount = totalPointCount;
                    descriptor.AudioMaterialBufferId = unchecked((uint)(int)BufferID.VoxelSdfAudioMaterialIds);
                    descriptor.AudioMaterialBufferGeneration = audioMaterialGeneration;
                    descriptors[0] = descriptor;
                }

                return true;
            }
            finally
            {
                if (encodeScheduled && !encodeHandle.IsCompleted)
                    DispatcherJobFence.TryComplete(ref encodeHandle, forceComplete: true);

                if (writeGuardMask != 0UL)
                    ReleasePublishedSonarPayloadWriteGuard(vault, writeGuardMask);

                Interlocked.Exchange(ref s_publishedSonarVaultPublishInFlight, 0);
            }
        }

        private static bool TryInvalidatePublishedSonarVaultDescriptor(
            IDataVault vault,
            in VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle)
        {
            if (vault == null ||
                !TryAcquirePublishedSonarWriteLock(vault, in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors))
            {
                return false;
            }

            try
            {
                if (descriptors.IsCreated && descriptors.Length > 0)
                    descriptors[0] = default;

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in descriptorHandle, SystemID.WorldStreaming);
            }
        }

        private static bool TryInvalidatePublishedSonarVaultDescriptorGuarded(
            IDataVault vault,
            in VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryResolveHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors))
            {
                return false;
            }

            if (descriptors.IsCreated && descriptors.Length > 0)
                descriptors[0] = default;

            return !vault.IsCompactionFenceActive;
        }

        private static bool TryAcquirePublishedSonarPayloadWriteGuard(IDataVault vault, out ulong guardMask)
        {
            guardMask = PublishedSonarPayloadReadGuardMask;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                guardMask == 0UL ||
                !vault.TryAcquireMutationGuard(guardMask))
            {
                guardMask = 0UL;
                return false;
            }

            bool keepGuard = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                {
                    vault.ReleaseMutationGuard(guardMask);
                    guardMask = 0UL;
                }
            }
        }

        private static void ReleasePublishedSonarPayloadWriteGuard(IDataVault vault, ulong guardMask)
        {
            if (vault == null || guardMask == 0UL)
                return;

            vault.ReleaseMutationGuard(guardMask);
        }

        private static bool TryAcquirePublishedSonarWriteLock<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out buffer))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (!vault.IsCompactionFenceActive)
                {
                    keepLock = true;
                    return true;
                }

                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
                    buffer = default;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PublishedSonarSdfEncodeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> SmoothDensityField;
            [WriteOnly] public NativeArray<byte> EncodedSdf;
            [WriteOnly] public NativeArray<byte> AudioMaterialIds;
            public float InverseRange;
            public byte DefaultAudioMaterialId;

            public void Execute(int index)
            {
                float normalized = math.clamp(SmoothDensityField[index] * InverseRange, -1f, 1f);
                float encoded = (normalized * 0.5f + 0.5f) * 255f;
                EncodedSdf[index] = (byte)math.clamp((int)(encoded + 0.5f), 0, 255);
                AudioMaterialIds[index] = DefaultAudioMaterialId;
            }
        }

        internal static bool TryEnsurePublishedSonarVaultPayloadCapacity(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle = vault.EnsureGenerationHandle<VoxelSdfPayloadDescriptorDTO>(
                BufferID.VoxelSdfPayloadDescriptor,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            if (vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<byte> sdfHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.VoxelSdfTexture3D,
                PublishedSonarVaultPayloadCapacity,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            if (vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<byte> audioMaterialHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.VoxelSdfAudioMaterialIds,
                PublishedSonarVaultPayloadCapacity,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            if (vault.IsCompactionFenceActive)
                return false;

            return descriptorHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) &&
                   sdfHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfTexture3D) &&
                   audioMaterialHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfAudioMaterialIds) &&
                   TryResolvePublishedSonarVaultPayloadHandles(vault, out _, out _, out _);
        }

        private static bool TryResolvePublishedSonarVaultPayloadHandles(
            IDataVault vault,
            out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle,
            out VaultGenerationHandle<byte> sdfHandle,
            out VaultGenerationHandle<byte> audioMaterialHandle)
        {
            descriptorHandle = default;
            sdfHandle = default;
            audioMaterialHandle = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool resolved =
                vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(BufferID.VoxelSdfPayloadDescriptor, out descriptorHandle) &&
                descriptorHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) &&
                vault.TryReadOnlyHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO>.ReadOnly descriptors) &&
                descriptors.IsCreated &&
                descriptors.Length >= 1 &&
                vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out sdfHandle) &&
                sdfHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfTexture3D) &&
                vault.TryReadOnlyHandle(in sdfHandle, out NativeArray<byte>.ReadOnly sdf) &&
                sdf.IsCreated &&
                sdf.Length >= PublishedSonarVaultPayloadCapacity &&
                vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfAudioMaterialIds, out audioMaterialHandle) &&
                audioMaterialHandle.BufferID == unchecked((uint)(int)BufferID.VoxelSdfAudioMaterialIds) &&
                vault.TryReadOnlyHandle(in audioMaterialHandle, out NativeArray<byte>.ReadOnly audioMaterialIds) &&
                audioMaterialIds.IsCreated &&
                audioMaterialIds.Length >= PublishedSonarVaultPayloadCapacity;

            return resolved && !vault.IsCompactionFenceActive;
        }

        private bool TryReadPublishedSonarVaultPayload(
            out NativeArray<byte>.ReadOnly encodedSdf,
            out NativeArray<byte>.ReadOnly audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version,
            bool requireSdf = true,
            bool requireAudioMaterial = true)
        {
            encodedSdf = default;
            audioMaterialIds = default;
            gridDimensions = default;
            volumeOrigin = default;
            voxelCellSize = default;
            sdfRange = 0f;
            version = 0;

            if (!requireSdf && !requireAudioMaterial)
                return false;

            IDataVault vault = _cachedDataVault;
            if (!_runtimeDataReady ||
                vault == null ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(
                    BufferID.VoxelSdfPayloadDescriptor,
                    out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                descriptorHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) ||
                !vault.TryReadOnlyHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO>.ReadOnly descriptors) ||
                !descriptors.IsCreated ||
                descriptors.Length <= 0)
            {
                return false;
            }

            VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
            if (vault.IsCompactionFenceActive ||
                (descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) == 0u ||
                descriptor.BufferId != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                descriptor.AudioMaterialBufferId != unchecked((uint)(int)BufferID.VoxelSdfAudioMaterialIds) ||
                descriptor.ByteCount <= 0 ||
                descriptor.AudioMaterialByteCount <= 0 ||
                descriptor.GridDimensions.x <= 1 ||
                descriptor.GridDimensions.y <= 1 ||
                descriptor.GridDimensions.z <= 1 ||
                descriptor.SdfRangeMeters <= 0f ||
                !math.all(math.isfinite(descriptor.VolumeOrigin)) ||
                !math.all(math.isfinite(descriptor.VoxelCellSize)) ||
                !math.isfinite(descriptor.SdfRangeMeters))
            {
                return false;
            }

            int expectedCount = descriptor.GridDimensions.x *
                                descriptor.GridDimensions.y *
                                descriptor.GridDimensions.z;
            if (expectedCount <= 0 ||
                descriptor.ByteCount < expectedCount ||
                descriptor.AudioMaterialByteCount < expectedCount)
            {
                return false;
            }

            if (requireSdf &&
                (vault.IsCompactionFenceActive ||
                 !vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out VaultGenerationHandle<byte> sdfHandle) ||
                 sdfHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                 sdfHandle.Generation != descriptor.BufferGeneration ||
                 !vault.TryReadOnlyHandle(in sdfHandle, out encodedSdf) ||
                 !encodedSdf.IsCreated ||
                 encodedSdf.Length < expectedCount))
            {
                encodedSdf = default;
                return false;
            }

            if (requireAudioMaterial &&
                (vault.IsCompactionFenceActive ||
                 !vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfAudioMaterialIds, out VaultGenerationHandle<byte> audioMaterialHandle) ||
                 audioMaterialHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfAudioMaterialIds) ||
                 audioMaterialHandle.Generation != descriptor.AudioMaterialBufferGeneration ||
                 !vault.TryReadOnlyHandle(in audioMaterialHandle, out audioMaterialIds) ||
                 !audioMaterialIds.IsCreated ||
                 audioMaterialIds.Length < expectedCount))
            {
                encodedSdf = default;
                audioMaterialIds = default;
                return false;
            }

            gridDimensions = new Vector3Int(
                descriptor.GridDimensions.x,
                descriptor.GridDimensions.y,
                descriptor.GridDimensions.z);
            volumeOrigin = new Vector3(
                descriptor.VolumeOrigin.x,
                descriptor.VolumeOrigin.y,
                descriptor.VolumeOrigin.z);
            voxelCellSize = new Vector3(
                math.max(0.0001f, descriptor.VoxelCellSize.x),
                math.max(0.0001f, descriptor.VoxelCellSize.y),
                math.max(0.0001f, descriptor.VoxelCellSize.z));
            sdfRange = descriptor.SdfRangeMeters;
            version = descriptor.SdfVersion > int.MaxValue ? int.MaxValue : (int)descriptor.SdfVersion;
            return !vault.IsCompactionFenceActive;
        }

        private bool TryResolvePublishedSonarDescriptorOrigin(Vector3 capturedRuntimeOrigin, out Vector3 runtimeOrigin)
        {
            runtimeOrigin = default;
            if (!IsFinite(capturedRuntimeOrigin) ||
                !IsFinite(generationPosition) ||
                !math.all(math.isfinite(_generationAbsoluteUniversePositionDouble)) ||
                !TryResolveCurrentRuntimeOriginAbsolute(out double3 currentOriginAbsolute))
            {
                return false;
            }

            double3 capturedOffset = _generationAbsoluteUniversePositionDouble -
                                     new double3(generationPosition.x, generationPosition.y, generationPosition.z);
            double3 absoluteOrigin = new double3(capturedRuntimeOrigin.x, capturedRuntimeOrigin.y, capturedRuntimeOrigin.z) + capturedOffset;
            double3 rebased = absoluteOrigin - currentOriginAbsolute;
            if (!math.all(math.isfinite(rebased)))
                return false;

            runtimeOrigin = ToVector3(rebased);
            return IsFinite(runtimeOrigin);
        }

        private bool TryGetPublishedSonarSdfPayload(
            out NativeArray<byte>.ReadOnly encodedSdf,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version)
        {
            return TryReadPublishedSonarVaultPayload(
                out encodedSdf,
                out _,
                out gridDimensions,
                out volumeOrigin,
                out voxelCellSize,
                out sdfRange,
                out version,
                requireAudioMaterial: false);
        }

        internal bool TryAcquirePublishedSonarSdfPayloadReadLease(
            out NativeArray<byte>.ReadOnly encodedSdf,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version,
            out PublishedSonarSdfReadLease lease)
        {
            encodedSdf = default;
            gridDimensions = default;
            volumeOrigin = default;
            voxelCellSize = default;
            sdfRange = 0f;
            version = 0;
            lease = default;

            IDataVault vault = _cachedDataVault;
            if (!_runtimeDataReady ||
                vault == null ||
                !TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            bool acquired = false;
            try
            {
                acquired = TryGetPublishedSonarSdfPayload(
                    out encodedSdf,
                    out gridDimensions,
                    out volumeOrigin,
                    out voxelCellSize,
                    out sdfRange,
                    out version);
                if (!acquired)
                    return false;

                if (!vault.TryGetBufferGeneration(BufferID.VoxelSdfTexture3D, out uint sdfGeneration) ||
                    sdfGeneration == 0u)
                {
                    acquired = false;
                    return false;
                }

                lease = new PublishedSonarSdfReadLease(
                    this,
                    vault,
                    version,
                    sdfGeneration,
                    0u,
                    readGuardMask);
                return true;
            }
            finally
            {
                if (!acquired)
                    ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        internal bool TryAcquirePublishedSonarSdfPayloadReadLease(
            out NativeArray<byte>.ReadOnly encodedSdf,
            out NativeArray<byte>.ReadOnly audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version,
            out PublishedSonarSdfReadLease lease)
        {
            encodedSdf = default;
            audioMaterialIds = default;
            gridDimensions = default;
            volumeOrigin = default;
            voxelCellSize = default;
            sdfRange = 0f;
            version = 0;
            lease = default;

            IDataVault vault = _cachedDataVault;
            if (!_runtimeDataReady ||
                vault == null ||
                !TryAcquirePublishedSonarPayloadReadGuard(vault, out ulong readGuardMask))
            {
                return false;
            }

            bool acquired = false;
            try
            {
                acquired = TryGetPublishedSonarSdfPayload(
                    out encodedSdf,
                    out audioMaterialIds,
                    out gridDimensions,
                    out volumeOrigin,
                    out voxelCellSize,
                    out sdfRange,
                    out version);
                if (!acquired)
                    return false;

                if (!vault.TryGetBufferGeneration(BufferID.VoxelSdfTexture3D, out uint sdfGeneration) ||
                    !vault.TryGetBufferGeneration(BufferID.VoxelSdfAudioMaterialIds, out uint audioMaterialGeneration) ||
                    sdfGeneration == 0u ||
                    audioMaterialGeneration == 0u)
                {
                    acquired = false;
                    return false;
                }

                lease = new PublishedSonarSdfReadLease(
                    this,
                    vault,
                    version,
                    sdfGeneration,
                    audioMaterialGeneration,
                    readGuardMask);
                return true;
            }
            finally
            {
                if (!acquired)
                    ReleasePublishedSonarPayloadReadGuard(vault, readGuardMask);
            }
        }

        internal void ReleasePublishedSonarSdfPayloadReadLease(in PublishedSonarSdfReadLease lease)
        {
            if (!lease.IsValid || !ReferenceEquals(lease.Owner, this))
                return;

            ReleasePublishedSonarPayloadReadGuard(lease.Vault, lease.MutationGuardMask);
        }

        private static bool TryAcquirePublishedSonarPayloadReadGuard(IDataVault vault, out ulong guardMask)
        {
            guardMask = PublishedSonarPayloadReadGuardMask;
            if (vault == null || vault.IsCompactionFenceActive || guardMask == 0UL)
            {
                guardMask = 0UL;
                return false;
            }

            if (!TryEnterPublishedSonarPayloadReadGuardGate())
            {
                guardMask = 0UL;
                return false;
            }

            try
            {
                int refCount = s_publishedSonarPayloadReadGuardRefCount;
                if (refCount > 0)
                {
                    if (!ReferenceEquals(s_publishedSonarPayloadReadGuardVault, vault) ||
                        s_publishedSonarPayloadReadGuardMask != guardMask ||
                        refCount == int.MaxValue ||
                        vault.IsCompactionFenceActive)
                    {
                        guardMask = 0UL;
                        return false;
                    }

                    s_publishedSonarPayloadReadGuardRefCount = refCount + 1;
                    Thread.MemoryBarrier();
                    return true;
                }

                if (!vault.TryAcquireMutationGuard(guardMask))
                {
                    guardMask = 0UL;
                    return false;
                }

                if (vault.IsCompactionFenceActive)
                {
                    vault.ReleaseMutationGuard(guardMask);
                    guardMask = 0UL;
                    return false;
                }

                s_publishedSonarPayloadReadGuardVault = vault;
                s_publishedSonarPayloadReadGuardMask = guardMask;
                s_publishedSonarPayloadReadGuardRefCount = 1;
                Thread.MemoryBarrier();
                return true;
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuardGate();
            }
        }

        internal static void ReleasePublishedSonarPayloadReadGuard(IDataVault vault, ulong guardMask)
        {
            if (vault == null || guardMask == 0UL)
                return;

            EnterPublishedSonarPayloadReadGuardGate();
            try
            {
                int refCount = s_publishedSonarPayloadReadGuardRefCount;
                if (refCount <= 0 ||
                    !ReferenceEquals(s_publishedSonarPayloadReadGuardVault, vault) ||
                    s_publishedSonarPayloadReadGuardMask != guardMask)
                {
                    return;
                }

                int nextRefCount = refCount - 1;
                s_publishedSonarPayloadReadGuardRefCount = nextRefCount;
                if (nextRefCount > 0)
                    return;

                s_publishedSonarPayloadReadGuardVault = null;
                s_publishedSonarPayloadReadGuardMask = 0UL;
                Thread.MemoryBarrier();
                vault.ReleaseMutationGuard(guardMask);
            }
            finally
            {
                ReleasePublishedSonarPayloadReadGuardGate();
            }
        }

        private static bool TryEnterPublishedSonarPayloadReadGuardGate()
        {
            if (Interlocked.CompareExchange(ref s_publishedSonarPayloadReadGuardGate, 1, 0) != 0)
                return false;

            Thread.MemoryBarrier();
            return true;
        }

        private static void EnterPublishedSonarPayloadReadGuardGate()
        {
            SpinWait spin = default;
            while (Interlocked.CompareExchange(ref s_publishedSonarPayloadReadGuardGate, 1, 0) != 0)
                spin.SpinOnce();

            Thread.MemoryBarrier();
        }

        private static void ReleasePublishedSonarPayloadReadGuardGate()
        {
            Thread.MemoryBarrier();
            Volatile.Write(ref s_publishedSonarPayloadReadGuardGate, 0);
        }

        private bool TryGetPublishedSonarSdfPayload(
            out NativeArray<byte>.ReadOnly encodedSdf,
            out NativeArray<byte>.ReadOnly audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version)
        {
            return TryReadPublishedSonarVaultPayload(
                out encodedSdf,
                out audioMaterialIds,
                out gridDimensions,
                out volumeOrigin,
                out voxelCellSize,
                out sdfRange,
                out version);
        }

        /// <summary>
        /// Returns true when the provided async rebuild token still matches the current pooled runtime payload.
        /// </summary>
        public bool MatchesRuntimeStamp(int stamp)
        {
            return _runtimeStamp == stamp;
        }

        /// <summary>
        /// Adds a subtractive crater stamp to the volume SDF and schedules an async rebuild.
        /// Call this when large fauna or cargo impacts should gouge the cave wall.
        /// </summary>
        public void CarveCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return;

            if (_deltaProcessor != null)
            {
                SetBakeState(VoxelBakeState.Pending);
                _deltaProcessor.ApplyImmediateCrater(this, pos, radius, DefaultDeltaMaterialId);
                return;
            }

            if (!TryResolveRuntimeAbsoluteDouble(pos, out double3 absolutePosition))
                return;

            AppendCraterStamp(absolutePosition, radius, true);
        }

        /// <summary>
        /// Adds a subtractive abyssal crater stamp and queues async mesh rebuild.
        /// Alias kept explicit for gameplay callers that operate in abyssal terms.
        /// </summary>
        public void CarveAbyssalCrater(Vector3 pos, float radius)
        {
            CarveCrater(pos, radius);
        }

        /// <summary>
        /// Applies a security-gated mod SDF operation to this runtime volume.
        /// The dispatcher owns bounds/protected-sector validation before this call.
        /// </summary>
        internal bool TryApplyModSdfModify(Vector3 runtimeCenter, float radius, bool additive)
        {
            if (!_runtimeDataReady || _deltaProcessor == null || radius <= 0f || !IsFinite(runtimeCenter))
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            float safeRadius = Mathf.Max(_voxelSize, radius);
            Bounds runtimeBounds = BuildRuntimeAabb(transform, localBounds);
            if (runtimeBounds.SqrDistance(runtimeCenter) > safeRadius * safeRadius)
                return false;

            if (!TryResolveRuntimeAbsoluteDouble(runtimeCenter, out double3 absoluteCenter))
                return false;

            SetBakeState(VoxelBakeState.Pending);
            if (additive)
                _deltaProcessor.ApplyImmediateAbsoluteWeld(this, absoluteCenter, safeRadius, safeRadius, DefaultDeltaMaterialId);
            else
                _deltaProcessor.ApplyImmediateAbsoluteCrater(this, absoluteCenter, safeRadius, DefaultDeltaMaterialId);

            return true;
        }

        /// <summary>
        /// Applies a persistent additive organic root mound through the voxel delta owner.
        /// </summary>
        internal void ApplyOrganicRootMound(Vector3 pos, float radius, float strength)
        {
            if (!_runtimeDataReady || radius <= 0f || strength <= 0f || _deltaProcessor == null)
                return;

            if (!TryResolveRuntimeAbsoluteDouble(pos, out double3 absolutePosition))
                return;

            SetBakeState(VoxelBakeState.Pending);
            float resolvedRadius = ResolveOrganicRootMoundWeldRadius(pos, radius);
            _deltaProcessor.ApplyImmediateAbsoluteWeld(this, absolutePosition, resolvedRadius, strength, DefaultDeltaMaterialId);
        }

        private float ResolveOrganicRootMoundWeldRadius(Vector3 runtimePosition, float authoredRadius)
        {
            float safeRadius = Mathf.Max(0.01f, authoredRadius);
            if (!TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out _,
                    out PublishedSonarSdfReadLease sdfLease))
            {
                return safeRadius;
            }

            try
            {
                if (!TrySamplePublishedDensity(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        runtimePosition,
                        out float densityAtRoot,
                        out _))
                {
                    return safeRadius;
                }

                if (densityAtRoot >= 0f)
                    return safeRadius;

                float distanceToSeabed = Mathf.Abs(densityAtRoot);
                for (int i = 1; i <= OrganicRootMoundSeabedProbeSteps; i++)
                {
                    float probeDistance = i * OrganicRootMoundSeabedProbeStepMeters;
                    Vector3 probePosition = runtimePosition + Vector3.down * probeDistance;
                    if (!TrySamplePublishedDensity(
                            encodedSdf,
                            gridDimensions,
                            volumeOrigin,
                            voxelCellSize,
                            sdfRange,
                            probePosition,
                            out float probeDensity,
                            out _))
                    {
                        continue;
                    }

                    if (probeDensity >= 0f)
                    {
                        distanceToSeabed = Mathf.Min(distanceToSeabed, probeDistance);
                        break;
                    }
                }

                return Mathf.Max(safeRadius, distanceToSeabed + OrganicRootMoundMinimumOverlapMeters);
            }
            finally
            {
                ReleasePublishedSonarSdfPayloadReadLease(in sdfLease);
            }
        }

        /// <summary>
        /// Executes the authoritative persistent resource-depletion crater pass.
        /// Kept explicit so tombstoned geology callers route through the volume owner instead of the delta processor.
        /// </summary>
        public void ApplyPersistentResourceCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return;

            if (!TryResolveRuntimeAbsoluteDouble(pos, out double3 absolutePosition))
                return;

            CarveCrater(pos, radius);
            TryTriggerResourceCraterClusterCollapse(absolutePosition, radius);
        }

        /// <summary>
        /// Applies a parasite-triggered subtractive box collapse through the persistent voxel delta lane.
        /// </summary>
        internal bool ApplyParasiteCollapseBox(Vector3 runtimeCenter, Vector3 halfExtents)
        {
            if (!_runtimeDataReady || _deltaProcessor == null)
                return false;

            Vector3 safeHalfExtents = new Vector3(
                Mathf.Max(_voxelSize, Mathf.Abs(halfExtents.x)),
                Mathf.Max(_voxelSize, Mathf.Abs(halfExtents.y)),
                Mathf.Max(_voxelSize, Mathf.Abs(halfExtents.z)));
            if (safeHalfExtents.sqrMagnitude <= 0.0001f)
                return false;

            if (!TryResolveRuntimeAbsoluteDouble(runtimeCenter, out double3 absoluteCenter))
                return false;

            SetBakeState(VoxelBakeState.Pending);
            _deltaProcessor.ApplyImmediateAbsoluteBoxCrater(this, absoluteCenter, safeHalfExtents, DefaultDeltaMaterialId);

            float impulseRadius = Mathf.Max(
                safeHalfExtents.x,
                Mathf.Max(safeHalfExtents.y, safeHalfExtents.z)) * 1.8f;
            float impulseMagnitude = Mathf.Clamp(safeHalfExtents.y * 4f + safeHalfExtents.x + safeHalfExtents.z, 12f, 48f);
            ApplyCollapseImpulse(runtimeCenter, safeHalfExtents, impulseRadius, impulseMagnitude);
            return true;
        }

        /// <summary>
        /// Applies the sediment-layer depletion-rot crater path for soft ore veins.
        /// Basalt-profile nodes must keep using <see cref="ApplyPersistentResourceCrater"/>.
        /// </summary>
        public void ApplyPersistentResourceSedimentRotCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return;

            if (!TryResolveRuntimeAbsoluteDouble(pos, out double3 absolutePosition))
                return;

            if (_deltaProcessor != null)
            {
                SetBakeState(VoxelBakeState.Pending);
                _deltaProcessor.ApplyImmediateCrater(this, pos, radius, SedimentDeltaMaterialId);
            }
            else
            {
                AppendCraterStamp(absolutePosition, radius, true);
            }

            TryTriggerResourceCraterClusterCollapse(absolutePosition, radius);
        }

        /// <summary>
        /// Applies a subtractive meteor-impact sphere stamp through the authoritative voxel delta owner.
        /// </summary>
        /// <param name="pos">Runtime-space seabed impact point.</param>
        /// <param name="radius">Sphere carve radius in meters.</param>
        /// <returns>True when the volume accepted the impact stamp.</returns>
        public bool TryApplyExtraterrestrialImpactCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return false;

            CarveCrater(pos, radius);
            return true;
        }

        /// <summary>
        /// Applies an additive magma-vein CSG capsule chain through the persistent voxel delta owner.
        /// </summary>
        /// <param name="splinePoints">Caller-owned runtime-space spline point buffer.</param>
        /// <param name="pointCount">Valid point count inside <paramref name="splinePoints"/>.</param>
        /// <param name="radiusMeters">Magma capsule radius in meters.</param>
        /// <param name="burnRadiusMeters">Organic burn radius around each accepted segment.</param>
        /// <returns>Accepted segment count.</returns>
        public int ApplyMagmaVeinSpline(Vector3[] splinePoints, int pointCount, float radiusMeters, float burnRadiusMeters)
        {
            if (!_runtimeDataReady ||
                _deltaProcessor == null ||
                splinePoints == null ||
                pointCount < 2 ||
                radiusMeters <= 0f)
            {
                return 0;
            }

            int safePointCount = Mathf.Min(pointCount, splinePoints.Length);
            if (safePointCount < 2)
                return 0;

            float safeRadius = Mathf.Max(_voxelSize, radiusMeters);
            float safeBurnRadius = Mathf.Max(safeRadius, burnRadiusMeters);
            DestructibleOrganicManager organicManager = _cachedOrganicManager;
            int acceptedSegments = 0;
            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localVolumeBounds))
                return 0;

            Bounds runtimeVolumeBounds = BuildRuntimeAabb(transform, localVolumeBounds);
            runtimeVolumeBounds.Expand((safeRadius + _voxelSize) * 2f);
            SetBakeState(VoxelBakeState.Pending);

            for (int i = 1; i < safePointCount; i++)
            {
                Vector3 start = splinePoints[i - 1];
                Vector3 end = splinePoints[i];
                if (!IsFinite(start) || !IsFinite(end))
                    continue;

                Vector3 segment = end - start;
                float segmentLengthSq = segment.sqrMagnitude;
                if (segmentLengthSq <= _voxelSize * _voxelSize * 0.01f)
                    continue;

                Bounds segmentBounds = BuildRuntimeSegmentAabb(start, end, safeRadius);
                if (!segmentBounds.Intersects(runtimeVolumeBounds))
                    continue;

                if (!TryResolveRuntimeAbsoluteDouble(start, out double3 absoluteStart) ||
                    !TryResolveRuntimeAbsoluteDouble(end, out double3 absoluteEnd))
                {
                    continue;
                }

                _deltaProcessor.ApplyImmediateAbsoluteCapsuleWeld(
                    this,
                    absoluteStart,
                    absoluteEnd,
                    safeRadius,
                    safeRadius,
                    MagmaDeltaMaterialId);

                if (organicManager != null && safeBurnRadius > 0f)
                {
                    float segmentLengthApprox = math.cmax(math.abs(new float3(segment.x, segment.y, segment.z)));
                    BurnFloraAlongMagmaSegment(organicManager, start, end, segmentLengthApprox, safeBurnRadius);
                }

                acceptedSegments++;
            }

            return acceptedSegments;
        }

        /// <summary>
        /// Resolves ceiling-adjacent crater anchors around the supplied epicenter and pushes them
        /// through the authoritative crater/delta pipeline.
        /// </summary>
        /// <param name="runtimeEpicenter">Runtime-space event epicenter near the player.</param>
        /// <param name="stampCount">Number of collapse stamps to attempt.</param>
        /// <param name="scatterRadius">Horizontal scatter radius around the epicenter.</param>
        /// <param name="ceilingSearchDepth">Vertical search window from the top of the volume downward.</param>
        /// <param name="craterRadiusMin">Minimum crater radius in meters.</param>
        /// <param name="craterRadiusMax">Maximum crater radius in meters.</param>
        /// <param name="stableSeed">Deterministic seed used for stamp jitter.</param>
        /// <param name="appliedStampCount">Count of crater stamps accepted by the volume.</param>
        /// <returns>True when at least one ceiling stamp was applied.</returns>
        public bool TryApplySeismicShockwave(
            Vector3 runtimeEpicenter,
            int stampCount,
            float scatterRadius,
            float ceilingSearchDepth,
            float craterRadiusMin,
            float craterRadiusMax,
            uint stableSeed,
            out int appliedStampCount)
        {
            appliedStampCount = 0;
            if (!_runtimeDataReady || _bakeState != VoxelBakeState.Complete || _gridDimension <= 0 || _voxelSize <= 0f)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            Transform cachedTransform = transform;
            Vector3 localEpicenter = cachedTransform.InverseTransformPoint(runtimeEpicenter);
            int clampedStampCount = Mathf.Clamp(stampCount, 1, 8);
            float clampedScatterRadius = Mathf.Max(_voxelSize, scatterRadius);
            float clampedSearchDepth = Mathf.Max(_voxelSize * 2f, ceilingSearchDepth);
            float minRadius = Mathf.Max(_voxelSize * 0.9f, craterRadiusMin);
            float maxRadius = Mathf.Max(minRadius, craterRadiusMax);

            for (int stampIndex = 0; stampIndex < clampedStampCount; stampIndex++)
            {
                int scatterOctant = (int)(Hash01(stableSeed, stampIndex, 3) * 7.999f);
                float2 scatterDirection = ResolveSeismicScatterDirection(scatterOctant);
                float radialDistance = Hash01(stableSeed, stampIndex, 11) * clampedScatterRadius;
                float localX = Mathf.Clamp(
                    localEpicenter.x + scatterDirection.x * radialDistance,
                    localBounds.min.x + _voxelSize,
                    localBounds.max.x - _voxelSize);
                float localZ = Mathf.Clamp(
                    localEpicenter.z + scatterDirection.y * radialDistance,
                    localBounds.min.z + _voxelSize,
                    localBounds.max.z - _voxelSize);
                float craterRadius = math.lerp(minRadius, maxRadius, Hash01(stableSeed, stampIndex, 23));

                if (!TryResolveSeismicCollapseAnchor(
                        cachedTransform,
                        localBounds,
                        localX,
                        localZ,
                        clampedSearchDepth,
                        craterRadius,
                        out Vector3 localAnchor))
                {
                    continue;
                }

                CarveCrater(cachedTransform.TransformPoint(localAnchor), craterRadius);
                appliedStampCount++;
            }

            return appliedStampCount > 0;
        }

        /// <summary>
        /// Samples a trench line in absolute-universe space and converts each intersecting column into subtractive
        /// crater stamps owned by the authoritative voxel delta pipeline.
        /// </summary>
        /// <param name="absoluteEpicenter">Absolute-universe event epicenter used for linear trench depth falloff.</param>
        /// <param name="absoluteStart">Absolute-universe trench start position.</param>
        /// <param name="absoluteEnd">Absolute-universe trench end position.</param>
        /// <param name="trenchDepth">Peak trench depth at the center line.</param>
        /// <param name="trenchSlope">Meters of depth loss per meter away from the center line.</param>
        /// <param name="sampleSpacing">Meters between longitudinal trench samples.</param>
        /// <param name="appliedStampCount">Count of accepted crater stamps.</param>
        /// <param name="displacedVolumeCubicMeters">Estimated displaced solid mass volume from accepted stamps.</param>
        /// <returns>True when at least one trench stamp was applied to this volume.</returns>
        public bool TryApplySeismicTrench(
            Vector3 absoluteEpicenter,
            Vector3 absoluteStart,
            Vector3 absoluteEnd,
            float trenchDepth,
            float trenchSlope,
            float sampleSpacing,
            out int appliedStampCount,
            out float displacedVolumeCubicMeters)
        {
            appliedStampCount = 0;
            displacedVolumeCubicMeters = 0f;
            _ = absoluteEpicenter;
            _ = absoluteStart;
            _ = absoluteEnd;
            _ = trenchDepth;
            _ = trenchSlope;
            _ = sampleSpacing;
            // SHINOBU_241: macroscopic trench CSG is baked offline and loaded as immutable voxel data.
            // Runtime seismic events must not synthesize line-sampled canyon stamps.
            return false;
        }

        private static float EstimateSeismicCraterDisplacedVolume(float craterRadius, float cutDepth)
        {
            float radius = Mathf.Max(0f, craterRadius);
            float depth = Mathf.Clamp(cutDepth, 0f, radius * 2f);
            if (radius <= 0f || depth <= 0f)
                return 0f;

            return Mathf.PI * depth * depth * (radius - depth / 3f);
        }

        private static float2 ResolveSeismicScatterDirection(int octant)
        {
            switch (octant & 7)
            {
                case 0: return new float2(1f, 0f);
                case 1: return new float2(0.70710677f, 0.70710677f);
                case 2: return new float2(0f, 1f);
                case 3: return new float2(-0.70710677f, 0.70710677f);
                case 4: return new float2(-1f, 0f);
                case 5: return new float2(-0.70710677f, -0.70710677f);
                case 6: return new float2(0f, -1f);
                default: return new float2(0.70710677f, -0.70710677f);
            }
        }

        /// <summary>
        /// Marches a bounded DDA cut path through the runtime voxel volume and converts the traversed cells
        /// into subtractive crater stamps owned by the authoritative rebuild pipeline.
        /// </summary>
        /// <param name="absoluteHitPoint">Absolute-universe entry point on the volume surface.</param>
        /// <param name="direction">Runtime beam direction.</param>
        /// <param name="normalizedPower">Normalized beam power [0..1].</param>
        /// <param name="maxDistance">Maximum authored beam range.</param>
        /// <returns>True when at least one voxel cell was converted into a crater stamp.</returns>
        public bool ApplyPlasmaCutDda(
            Vector3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            return ApplyPlasmaCutDda(global::Hecton8.World.AUPMath.ToDouble3(absoluteHitPoint), direction, normalizedPower, maxDistance);
        }

        public bool ApplyPlasmaCutDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            if (!_runtimeDataReady || _gridDimension <= 0 || _voxelSize <= 0f || _bakeState != VoxelBakeState.Complete)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            float clampedPower = math.saturate(normalizedPower);
            if (clampedPower < MinPlasmaCutPower)
                return false;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(absoluteHitPoint);
            Transform cachedTransform = transform;
            Vector3 localDirection = cachedTransform.InverseTransformDirection(direction);
            if (localDirection.sqrMagnitude < 0.0001f)
                return false;

            localDirection = NormalizeApprox(localDirection, Vector3.forward);

            Vector3 localStart = cachedTransform.InverseTransformPoint(runtimeHitPoint) + localDirection * (_voxelSize * 0.55f);
            if (!localBounds.Contains(localStart))
            {
                localStart += localDirection * (_voxelSize * 0.55f);
                if (!localBounds.Contains(localStart))
                    return false;
            }

            Vector3 relative = localStart - localBounds.min;
            int3 voxel = (int3)math.floor(new float3(relative.x, relative.y, relative.z) / _voxelSize);
            if (!IsVoxelIndexInBounds(voxel))
                return false;

            int3 step = new int3(
                ResolveStep(localDirection.x),
                ResolveStep(localDirection.y),
                ResolveStep(localDirection.z));
            float3 start = new float3(localStart.x, localStart.y, localStart.z);
            float3 dir = new float3(localDirection.x, localDirection.y, localDirection.z);
            BoundaryDistanceArgs xArgs = new BoundaryDistanceArgs { Min = localBounds.min.x, Start = start.x, Direction = dir.x, VoxelIndex = voxel.x, Step = step.x, VoxelSize = _voxelSize };
            BoundaryDistanceArgs yArgs = new BoundaryDistanceArgs { Min = localBounds.min.y, Start = start.y, Direction = dir.y, VoxelIndex = voxel.y, Step = step.y, VoxelSize = _voxelSize };
            BoundaryDistanceArgs zArgs = new BoundaryDistanceArgs { Min = localBounds.min.z, Start = start.z, Direction = dir.z, VoxelIndex = voxel.z, Step = step.z, VoxelSize = _voxelSize };
            float3 tMax = new float3(
                ResolveBoundaryDistance(in xArgs),
                ResolveBoundaryDistance(in yArgs),
                ResolveBoundaryDistance(in zArgs));
            float3 tDelta = new float3(
                ResolveDeltaDistance(dir.x, _voxelSize),
                ResolveDeltaDistance(dir.y, _voxelSize),
                ResolveDeltaDistance(dir.z, _voxelSize));

            float travel = 0f;
            float maxTravel = math.max(_voxelSize, math.min(maxDistance, _voxelSize * MaxPlasmaCutSteps));
            float remainingPower = clampedPower;
            float stampRadius = math.max(_voxelSize * 0.6f, _voxelSize * math.lerp(0.75f, 1.1f, clampedPower));
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 committedOffset))
                return false;

            bool modified = false;

            if (_deltaProcessor != null)
                SetBakeState(VoxelBakeState.Pending);

            for (int stepIndex = 0; stepIndex < MaxPlasmaCutSteps; stepIndex++)
            {
                if (!IsVoxelIndexInBounds(voxel) || remainingPower < MinPlasmaCutPower || travel > maxTravel)
                    break;

                Vector3 localCenter = localBounds.min + new Vector3(
                    (voxel.x + 0.5f) * _voxelSize,
                    (voxel.y + 0.5f) * _voxelSize,
                    (voxel.z + 0.5f) * _voxelSize);
                Vector3 worldCenter = cachedTransform.TransformPoint(localCenter);
                double3 absoluteCenter = new double3(worldCenter.x, worldCenter.y, worldCenter.z) + committedOffset;
                if (_deltaProcessor != null)
                {
                    _deltaProcessor.ApplyImmediateAbsoluteLaserCrater(this, absoluteCenter, stampRadius * remainingPower, direction, DefaultDeltaMaterialId);
                    modified = true;
                }
                else
                {
                    modified |= AppendCraterStamp(absoluteCenter, stampRadius * remainingPower, false);
                }

                float nextTravel;
                int axis = ResolveMarchAxis(tMax, out nextTravel);
                float segmentLength = math.max(_voxelSize * 0.25f, nextTravel - travel);
                remainingPower *= ApproximateExpNegPositive(segmentLength * PlasmaCutAttenuationPerMeter);
                travel = nextTravel;
                if (travel > maxTravel)
                    break;

                switch (axis)
                {
                    case 0:
                        voxel.x += step.x;
                        tMax.x += tDelta.x;
                        break;
                    case 1:
                        voxel.y += step.y;
                        tMax.y += tDelta.y;
                        break;
                    default:
                        voxel.z += step.z;
                        tMax.z += tDelta.z;
                        break;
                }
            }

            if (modified && _deltaProcessor == null)
                QueueRebuild();

            return modified;
        }

        /// <summary>
        /// Marches a bounded DDA repair path through the runtime voxel volume and deposits additive weld deltas
        /// into the authoritative persistent delta pipeline.
        /// </summary>
        /// <param name="absoluteHitPoint">Absolute-universe entry point on the volume surface.</param>
        /// <param name="direction">Runtime beam direction.</param>
        /// <param name="normalizedPower">Normalized repair power [0..1].</param>
        /// <param name="maxDistance">Maximum authored beam range.</param>
        /// <returns>True when at least one voxel cell was converted into an additive weld stamp.</returns>
        public bool ApplyRepairWeldDda(
            Vector3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            return ApplyRepairWeldDda(global::Hecton8.World.AUPMath.ToDouble3(absoluteHitPoint), direction, normalizedPower, maxDistance);
        }

        public bool ApplyRepairWeldDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            if (_deltaProcessor == null || !_runtimeDataReady || _gridDimension <= 0 || _voxelSize <= 0f || _bakeState != VoxelBakeState.Complete)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            float clampedPower = math.saturate(normalizedPower);
            if (clampedPower < MinPlasmaCutPower)
                return false;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(absoluteHitPoint);
            Transform cachedTransform = transform;
            Vector3 localDirection = cachedTransform.InverseTransformDirection(direction);
            if (localDirection.sqrMagnitude < 0.0001f)
                return false;

            localDirection = NormalizeApprox(localDirection, Vector3.forward);

            Vector3 localStart = cachedTransform.InverseTransformPoint(runtimeHitPoint) + localDirection * (_voxelSize * 0.55f);
            if (!localBounds.Contains(localStart))
            {
                localStart += localDirection * (_voxelSize * 0.55f);
                if (!localBounds.Contains(localStart))
                    return false;
            }

            Vector3 relative = localStart - localBounds.min;
            int3 voxel = (int3)math.floor(new float3(relative.x, relative.y, relative.z) / _voxelSize);
            if (!IsVoxelIndexInBounds(voxel))
                return false;

            int3 step = new int3(
                ResolveStep(localDirection.x),
                ResolveStep(localDirection.y),
                ResolveStep(localDirection.z));
            float3 start = new float3(localStart.x, localStart.y, localStart.z);
            float3 dir = new float3(localDirection.x, localDirection.y, localDirection.z);
            BoundaryDistanceArgs xArgs = new BoundaryDistanceArgs { Min = localBounds.min.x, Start = start.x, Direction = dir.x, VoxelIndex = voxel.x, Step = step.x, VoxelSize = _voxelSize };
            BoundaryDistanceArgs yArgs = new BoundaryDistanceArgs { Min = localBounds.min.y, Start = start.y, Direction = dir.y, VoxelIndex = voxel.y, Step = step.y, VoxelSize = _voxelSize };
            BoundaryDistanceArgs zArgs = new BoundaryDistanceArgs { Min = localBounds.min.z, Start = start.z, Direction = dir.z, VoxelIndex = voxel.z, Step = step.z, VoxelSize = _voxelSize };
            float3 tMax = new float3(
                ResolveBoundaryDistance(in xArgs),
                ResolveBoundaryDistance(in yArgs),
                ResolveBoundaryDistance(in zArgs));
            float3 tDelta = new float3(
                ResolveDeltaDistance(dir.x, _voxelSize),
                ResolveDeltaDistance(dir.y, _voxelSize),
                ResolveDeltaDistance(dir.z, _voxelSize));

            float travel = 0f;
            float maxTravel = math.max(_voxelSize, math.min(maxDistance, _voxelSize * MaxPlasmaCutSteps));
            float remainingPower = clampedPower;
            float stampRadius = math.max(_voxelSize * 0.55f, _voxelSize * math.lerp(0.65f, 1f, clampedPower));
            float stampStrength = math.max(_voxelSize, stampRadius * 0.45f);
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 committedOffset))
                return false;

            bool modified = false;

            SetBakeState(VoxelBakeState.Pending);

            for (int stepIndex = 0; stepIndex < MaxPlasmaCutSteps; stepIndex++)
            {
                if (!IsVoxelIndexInBounds(voxel) || remainingPower < MinPlasmaCutPower || travel > maxTravel)
                    break;

                Vector3 localCenter = localBounds.min + new Vector3(
                    (voxel.x + 0.5f) * _voxelSize,
                    (voxel.y + 0.5f) * _voxelSize,
                    (voxel.z + 0.5f) * _voxelSize);
                Vector3 worldCenter = cachedTransform.TransformPoint(localCenter);
                double3 absoluteCenter = new double3(worldCenter.x, worldCenter.y, worldCenter.z) + committedOffset;
                _deltaProcessor.ApplyImmediateAbsoluteWeld(
                    this,
                    absoluteCenter,
                    stampRadius * remainingPower,
                    stampStrength * remainingPower,
                    DefaultDeltaMaterialId);
                modified = true;

                float nextTravel;
                int axis = ResolveMarchAxis(tMax, out nextTravel);
                float segmentLength = math.max(_voxelSize * 0.25f, nextTravel - travel);
                remainingPower *= ApproximateExpNegPositive(segmentLength * PlasmaCutAttenuationPerMeter);
                travel = nextTravel;
                if (travel > maxTravel)
                    break;

                switch (axis)
                {
                    case 0:
                        voxel.x += step.x;
                        tMax.x += tDelta.x;
                        break;
                    case 1:
                        voxel.y += step.y;
                        tMax.y += tDelta.y;
                        break;
                    default:
                        voxel.z += step.z;
                        tMax.z += tDelta.z;
                        break;
                }
            }

            return modified;
        }

        bool IVoxelRepairWeldTarget.TryApplyRepairWeldDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            return ApplyRepairWeldDda(absoluteHitPoint, direction, normalizedPower, maxDistance);
        }

        bool IVoxelPlasmaCutTarget.TryApplyPlasmaCutDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            return ApplyPlasmaCutDda(absoluteHitPoint, direction, normalizedPower, maxDistance);
        }

        /// <summary>
        /// Deposits an additive noise-jittered ore vein into the authoritative voxel delta pipeline.
        /// This is used by large resource deposits that must live inside solid rock instead of sitting on top of the seabed.
        /// </summary>
        public bool TryApplyEmbeddedOreVein(
            Vector3 absoluteStart,
            Vector3 absoluteDirection,
            float lengthMeters,
            float radiusMeters,
            float noiseAmplitudeMeters,
            int stampCount,
            uint stableSeed)
        {
            if (_deltaProcessor == null || !_runtimeDataReady || _gridDimension <= 0 || _voxelSize <= 0f || _bakeState != VoxelBakeState.Complete)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            Vector3 direction = NormalizeApprox(absoluteDirection, Vector3.down);
            float resolvedLength = Mathf.Max(_voxelSize * 2f, lengthMeters);
            float resolvedRadius = Mathf.Max(_voxelSize * 0.75f, radiusMeters);
            float resolvedStrength = Mathf.Max(_voxelSize, resolvedRadius * 0.55f);
            float jitterAmplitude = Mathf.Max(0f, noiseAmplitudeMeters);
            int resolvedStampCount = Mathf.Clamp(stampCount, 2, 24);

            Vector3 tangentA = Vector3.Cross(direction, Vector3.up);
            if (tangentA.sqrMagnitude <= 0.0001f)
                tangentA = Vector3.Cross(direction, Vector3.right);
            tangentA = NormalizeApprox(tangentA, Vector3.right);
            Vector3 tangentB = Vector3.Cross(direction, tangentA);
            tangentB = NormalizeApprox(tangentB, Vector3.forward);

            bool modified = false;
            Transform cachedTransform = transform;
            for (int stampIndex = 0; stampIndex < resolvedStampCount; stampIndex++)
            {
                float t = resolvedStampCount <= 1 ? 0f : stampIndex / (float)(resolvedStampCount - 1);
                float longitudinalOffset = (t - 0.5f) * resolvedLength;
                int jitterOctant = (int)(Hash01(stableSeed, stampIndex, 41) * 7.999f);
                float2 jitterDirection = ResolveSeismicScatterDirection(jitterOctant);
                float radialScale = (Hash01(stableSeed, stampIndex, 53) * 2f) - 1f;
                Vector3 jitter = (tangentA * jitterDirection.x + tangentB * jitterDirection.y) * (jitterAmplitude * radialScale);
                double3 absoluteSample = global::Hecton8.World.AUPMath.ToDouble3(absoluteStart + direction * longitudinalOffset + jitter);
                Vector3 runtimeSample = HectonFloatingOrigin.ToRuntimePosition(absoluteSample);
                Vector3 localSample = cachedTransform.InverseTransformPoint(runtimeSample);
                if (!localBounds.Contains(localSample))
                    continue;

                _deltaProcessor.ApplyImmediateAbsoluteWeld(this, absoluteSample, resolvedRadius, resolvedStrength, DefaultDeltaMaterialId);
                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Tracks a persistent terrain-hole handle so cave unload can restore vegetation generation.
        /// </summary>
        public void TrackTerrainHoleHandle(int holeHandle)
        {
            if (holeHandle <= 0)
                return;

            if (_terrainHoleHandleCount < 0 ||
                _terrainHoleHandleCount > _terrainHoleHandles.Length ||
                _terrainHoleHandleCount > MaxTerrainHoleHandleCount)
            {
                global::HectonVoxelEngine.RecordVoxelRegistryCorruptionForAgent1304(
                    _terrainHoleHandleCount,
                    _terrainHoleHandles.Length,
                    MaxTerrainHoleHandleCount);
                _terrainHoleHandles.Clear();
                _terrainHoleHandleCount = 0;
            }

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                if (_terrainHoleHandles[i] == holeHandle)
                    return;
            }

            if (_terrainHoleHandleCount >= MaxTerrainHoleHandleCount ||
                _terrainHoleHandles.Length >= _terrainHoleHandles.Capacity)
                return;

            if (_terrainHoleHandleCount < _terrainHoleHandles.Length)
                _terrainHoleHandles[_terrainHoleHandleCount] = holeHandle;
            else
                _terrainHoleHandles.AddNoResize(holeHandle);

            _terrainHoleHandleCount++;
        }

        /// <summary>
        /// Ensures a named direct child root exists and is active.
        /// Reused by cave readability/detail systems to avoid duplicate runtime roots.
        /// </summary>
        public Transform GetOrCreateRuntimeRoot(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return null;

            Transform child = transform.Find(childName);
            if (child != null)
            {
                if (!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
            return child;
        }

        internal void RequestDeltaRebuild()
        {
            if (!_runtimeDataReady)
                return;

            QueueRebuild();
        }

        private void ToggleChildRoot(string childName, bool active)
        {
            if (string.IsNullOrEmpty(childName))
                return;

            Transform child = transform.Find(childName);
            if (child == null || child.gameObject.activeSelf == active)
                return;

            child.gameObject.SetActive(active);
        }

        private void CacheRuntimeComponentsCold()
        {
            if (_meshFilter == null)
                TryGetComponent(out _meshFilter);

            if (_meshRenderer == null)
                TryGetComponent(out _meshRenderer);

            if (_rootMeshCollider == null)
                TryGetComponent(out _rootMeshCollider);
        }

        private void SetBakeState(VoxelBakeState state)
        {
            if (_bakeState == state)
                return;

            _bakeState = state;
            RefreshBakePresentation();
        }

        private void RefreshBakePresentation()
        {
            if (!Application.isPlaying)
                CacheRuntimeComponentsCold();

            bool visualsStable = _bakeState == VoxelBakeState.Complete;
            // R95 FIX (voxels.md: "physics interaction is blocked until collider bake is complete"):
            // Pending/Baking previously counted as collision-allowed. Latent today (no collider mesh is
            // assigned before completion), but the gate must match the law before mesh upload returns.
            bool collisionAllowed = _bakeState == VoxelBakeState.Complete;
            if (_meshRenderer != null)
            {
                Material targetMaterial = null;
                if (_engine != null && visualsStable)
                    targetMaterial = _engine.voxelMaterial;
                else if (_engine != null)
                    targetMaterial = _engine.ResolvedVoxelBakeGhostMaterial != null
                        ? _engine.ResolvedVoxelBakeGhostMaterial
                        : _engine.voxelMaterial;

                if (targetMaterial != null && _meshRenderer.sharedMaterial != targetMaterial)
                    _meshRenderer.sharedMaterial = targetMaterial;
            }

            if (_rootMeshCollider != null)
                _rootMeshCollider.enabled = collisionAllowed && _rootMeshCollider.sharedMesh != null;

            bool activeColliderPresent = (_rootMeshCollider != null && _rootMeshCollider.enabled);
            int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;
            for (int i = 0; i < colliderCount; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider == null)
                    continue;

                collider.enabled = collisionAllowed && collider.sharedMesh != null && collider.gameObject.activeSelf;
                if (collider.enabled)
                    activeColliderPresent = true;
            }

            if (collisionAllowed && activeColliderPresent)
            {
                PublishPhysicsBakedSignalsOnComplete();
            }
        }

        private void PublishPhysicsBakedSignalsOnComplete()
        {
            if (_bakeState != VoxelBakeState.Complete)
                return;

            var pos = transform.position;
            float chunkSizeX = _gridDimension > 0 && _voxelSize > 0f ? _gridDimension * _voxelSize : 100f;
            float chunkSizeZ = chunkSizeX;
            float3 size = new float3(chunkSizeX, chunkSizeX, chunkSizeZ);
            float3 minCorner = new float3(pos.x - chunkSizeX * 0.5f, pos.y, pos.z - chunkSizeZ * 0.5f);

            WorldChunkPhysicsBakedSignal signal = new WorldChunkPhysicsBakedSignal
            {
                ChunkX = (int)math.floor(pos.x / chunkSizeX),
                ChunkZ = (int)math.floor(pos.z / chunkSizeZ),
                TerrainEntityHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                Frame = (uint)UnityEngine.Time.frameCount,
                TerrainPosition = minCorner,
                TerrainSize = size,
                Flags = WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagHeightmapSynced
            };
            WorldChunkPhysicsBakedEvents.TryPublish(in signal);
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
            VoxelDynamicNavGridRuntime.QueueDirtyVolume(this);
            if (_bakeState == VoxelBakeState.Complete || _bakeState == VoxelBakeState.Idle)
                SetBakeState(VoxelBakeState.Pending);

            if (_rebuildRunning)
                return;

            _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
        }

        private async Awaitable ProcessQueuedRebuildsAsync(int expectedRuntimeStamp)
        {
            if (_rebuildRunning)
                return;

            bool rescheduleNextFrame = false;
            _rebuildRunning = true;
            try
            {
                int rebuildWatchdog = MaxQueuedRebuildPassesPerKick;
                while (_rebuildQueued &&
                       MatchesRuntimeStamp(expectedRuntimeStamp) &&
                       rebuildWatchdog-- > 0)
                {
                    _rebuildQueued = false;
                    SetBakeState(VoxelBakeState.Baking);
                    HectonVoxelEngine engine = _engine;
                    if (engine == null)
                        return;

                    if (!await engine.RebuildVolumeAsync(this, expectedRuntimeStamp))
                    {
                        if (MatchesRuntimeStamp(expectedRuntimeStamp))
                        {
                            _rebuildQueued = true;
                            SetBakeState(VoxelBakeState.Pending);
                            await AwaitableDebtMonitor.NextFrameAsync();
                            rescheduleNextFrame = true;
                        }

                        return;
                    }

                    SetBakeState(_rebuildQueued ? VoxelBakeState.Pending : VoxelBakeState.Complete);
                }

                if (_rebuildQueued && MatchesRuntimeStamp(expectedRuntimeStamp))
                {
                    SetBakeState(VoxelBakeState.Pending);
                    await AwaitableDebtMonitor.NextFrameAsync();
                    rescheduleNextFrame = true;
                }
            }
            finally
            {
                VoxelBakeState stateBeforeRelease = _bakeState;
                if (stateBeforeRelease == VoxelBakeState.Baking &&
                    MatchesRuntimeStamp(expectedRuntimeStamp))
                {
                    _rebuildQueued = true;
                    SetBakeState(VoxelBakeState.Pending);
                    global::HectonVoxelEngine.RecordVoxelRebuildFailClosedForAgent1304(
                        expectedRuntimeStamp,
                        (int)stateBeforeRelease,
                        1);
                }

                _rebuildRunning = false;
            }

            if (rescheduleNextFrame && MatchesRuntimeStamp(_runtimeStamp))
                _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
        }

        private void UnregisterTerrainHoles()
        {
            if (_terrainHoleHandleCount <= 0)
                return;

            if (_terrainHoleHandleCount > _terrainHoleHandles.Length ||
                _terrainHoleHandleCount > MaxTerrainHoleHandleCount)
            {
                global::HectonVoxelEngine.RecordVoxelRegistryCorruptionForAgent1304(
                    _terrainHoleHandleCount,
                    _terrainHoleHandles.Length,
                    MaxTerrainHoleHandleCount);
                _terrainHoleHandles.Clear();
                _terrainHoleHandleCount = 0;
                return;
            }

            HectonMapMagicVegetationBridge vegetationBridge = _cachedVegetationBridge;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge))
            {
                _cachedVegetationBridge = null;
                _terrainHoleHandles.Clear();
                _terrainHoleHandleCount = 0;
                return;
            }
            _cachedVegetationBridge = vegetationBridge;

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                int holeHandle = _terrainHoleHandles[i];
                if (holeHandle <= 0)
                    continue;

                vegetationBridge.UnregisterTerrainHole(holeHandle);
                _terrainHoleHandles[i] = 0;
            }

            _terrainHoleHandles.Clear();
            _terrainHoleHandleCount = 0;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            CacheRuntimeComponentsCold();
            TryRegisterHotSwapListener();
            VoxelVolumeLeakSentinel.RegisterVolume(this);
            TryEnsurePublishedSonarVaultPayloadCapacity(_cachedDataVault);
            InteractableRegistry.RegisterTree(this);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            ClearEventLaneDiagnostics();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            InteractableRegistry.InvalidateTree(this);
            ClearEventLaneDiagnostics();
            VoxelVolumeLeakSentinel.FinalizeVolume(this);
            UnregisterPublishedVolume(this);
            _deltaProcessor?.UnregisterVolume(this);
            VoxelDynamicNavGridRuntime.UnregisterVolume(this);
            UnregisterTerrainHoles();
            ResetColliderChunks(true);
            ClearPublishedSonarSdf();
            _runtimeStamp++;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                VoxelVolumeLeakSentinel.RebindDispatcher(previousService, currentService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime)
            {
                if (_engine == null || ReferenceEquals(_engine, previousService))
                    _engine = currentService as HectonVoxelEngine;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicVegetationRuntime)
            {
                _cachedVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DestructibleOrganicRuntime)
            {
                _cachedOrganicManager = currentService as DestructibleOrganicManager;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _cachedDataVault = currentService as IDataVault;
            TryEnsurePublishedSonarVaultPayloadCapacity(_cachedDataVault);
        }

        private void CacheRegistryServicesCold()
        {
            _cachedDataVault = GlobalRegistry.DataVault;
            _physicsService = GlobalRegistry.Physics;
            if (_engine == null)
                _engine = GlobalRegistry.VoxelEngine;

            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
            _cachedOrganicManager = GlobalRegistry.OrganicToolHits as DestructibleOrganicManager;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ClearPublishedSonarSdf()
        {
            int descriptorVersion = _publishedSonarVersion;
            Vector3 descriptorOrigin = _publishedSonarOrigin;

            Volatile.Write(ref _publishedSonarPublishAbortRequested, 1);
            _publishedSonarGridDimensions = default;
            _publishedSonarOrigin = Vector3.zero;
            _publishedSonarCellSize = Vector3.zero;
            _publishedSonarSdfRange = 0f;
            _publishedSonarVersion = 0;
            TryClearSonarSdfVaultDescriptor(descriptorVersion, descriptorOrigin);
        }

        private void TryClearSonarSdfVaultDescriptor(int expectedVersion, Vector3 expectedCapturedRuntimeOrigin)
        {
            if (expectedVersion <= 0 ||
                !TryResolvePublishedSonarDescriptorOrigin(expectedCapturedRuntimeOrigin, out Vector3 expectedDescriptorOrigin))
            {
                return;
            }

            IDataVault vault = _cachedDataVault;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(
                    BufferID.VoxelSdfPayloadDescriptor,
                    out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                !TryAcquirePublishedSonarWriteLock(vault, in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors))
            {
                return;
            }

            try
            {
                if (descriptors.IsCreated && descriptors.Length > 0)
                {
                    VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
                    float3 expectedOrigin = new float3(
                        expectedDescriptorOrigin.x,
                        expectedDescriptorOrigin.y,
                        expectedDescriptorOrigin.z);

                    if ((descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) != 0u &&
                        descriptor.SdfVersion == unchecked((uint)expectedVersion) &&
                        math.all(math.isfinite(descriptor.VolumeOrigin)) &&
                        math.distancesq(descriptor.VolumeOrigin, expectedOrigin) <= 0.0001f)
                    {
                        descriptors[0] = default;
                    }
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in descriptorHandle, SystemID.WorldStreaming);
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(obj);
            else Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private static int ResolveStep(float axis)
        {
            if (axis > 0.0001f)
                return 1;

            return axis < -0.0001f ? -1 : 0;
        }

        private struct BoundaryDistanceArgs
        {
            public float Min;
            public float Start;
            public float Direction;
            public int VoxelIndex;
            public int Step;
            public float VoxelSize;
        }

        private static float ResolveBoundaryDistance(in BoundaryDistanceArgs args)
        {
            if (args.Step == 0 || Mathf.Abs(args.Direction) < 0.0001f)
                return float.PositiveInfinity;

            float nextBoundary = args.Min + ((args.Step > 0 ? args.VoxelIndex + 1 : args.VoxelIndex) * args.VoxelSize);
            return (nextBoundary - args.Start) / args.Direction;
        }

        private static float ResolveDeltaDistance(float direction, float voxelSize)
        {
            if (Mathf.Abs(direction) < 0.0001f)
                return float.PositiveInfinity;

            return voxelSize / Mathf.Abs(direction);
        }

        private static int ResolveMarchAxis(float3 tMax, out float nextTravel)
        {
            if (tMax.x <= tMax.y && tMax.x <= tMax.z)
            {
                nextTravel = tMax.x;
                return 0;
            }

            if (tMax.y <= tMax.z)
            {
                nextTravel = tMax.y;
                return 1;
            }

            nextTravel = tMax.z;
            return 2;
        }

        private bool IsVoxelIndexInBounds(int3 voxel)
        {
            return voxel.x >= 0 && voxel.x < _gridDimension &&
                   voxel.y >= 0 && voxel.y < _gridDimension &&
                   voxel.z >= 0 && voxel.z < _gridDimension;
        }

        private void TryTriggerResourceCraterClusterCollapse(double3 absolutePosition, float radius)
        {
            if (_collapseImpulseContacts.Length != CollapseImpulseColliderCapacity ||
                _collapseImpulseBodies.Length != CollapseImpulseBodyCapacity)
            {
                return;
            }

            if (_resourceCraterClusterCount < 0 ||
                _resourceCraterClusterCount > _resourceCraterClusterStamps.Length)
            {
                _resourceCraterClusterStamps.Clear();
                _resourceCraterClusterCount = 0;
                _lastCollapseClusterValid = false;
                return;
            }

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            if (_resourceCraterClusterCount >= MaxCraterStampCount)
            {
                for (int i = 1; i < _resourceCraterClusterCount; i++)
                    _resourceCraterClusterStamps[i - 1] = _resourceCraterClusterStamps[i];

                _resourceCraterClusterCount = MaxCraterStampCount - 1;
            }

            VoxelCraterStamp clusterStamp = new VoxelCraterStamp
            {
                position = absolutePosition,
                radius = clampedRadius,
                blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f)
            };
            if (_resourceCraterClusterStamps.Length > _resourceCraterClusterCount)
                _resourceCraterClusterStamps[_resourceCraterClusterCount] = clusterStamp;
            else
                _resourceCraterClusterStamps.Add(clusterStamp);

            _resourceCraterClusterCount++;

            if (!TryResolveResourceCraterCluster(
                    absolutePosition,
                    out double3 collapseAbsoluteCenter,
                    out Vector3 collapseHalfExtents,
                    out int clusterCount))
            {
                return;
            }

            if (_lastCollapseClusterValid &&
                math.lengthsq(_lastCollapseAbsoluteCenter - collapseAbsoluteCenter) <=
                (double)ResourceCraterClusterRadiusMeters * ResourceCraterClusterRadiusMeters)
            {
                return;
            }

            _lastCollapseClusterValid = true;
            _lastCollapseAbsoluteCenter = collapseAbsoluteCenter;
            ExecuteResourceCraterClusterCollapse(collapseAbsoluteCenter, collapseHalfExtents, clusterCount);
        }

        private bool TryResolveResourceCraterCluster(
            double3 absolutePosition,
            out double3 collapseAbsoluteCenter,
            out Vector3 collapseHalfExtents,
            out int clusterCount)
        {
            collapseAbsoluteCenter = absolutePosition;
            collapseHalfExtents = default;
            clusterCount = 0;
            double clusterRadiusSq = (double)ResourceCraterClusterRadiusMeters * ResourceCraterClusterRadiusMeters;
            double3 min = new double3(double.MaxValue, double.MaxValue, double.MaxValue);
            double3 max = new double3(double.MinValue, double.MinValue, double.MinValue);
            float largestRadius = 0f;

            for (int i = 0; i < _resourceCraterClusterCount; i++)
            {
                VoxelCraterStamp stamp = _resourceCraterClusterStamps[i];
                double3 clusterDelta = stamp.position - absolutePosition;
                if (math.lengthsq(clusterDelta) > clusterRadiusSq)
                    continue;

                clusterCount++;
                largestRadius = Mathf.Max(largestRadius, stamp.radius);
                double3 radiusVector = new double3(stamp.radius, stamp.radius, stamp.radius);
                min = math.min(min, stamp.position - radiusVector);
                max = math.max(max, stamp.position + radiusVector);
            }

            if (clusterCount <= ResourceCraterCollapseThreshold)
                return false;

            collapseAbsoluteCenter = (min + max) * 0.5d;
            double3 span = max - min;
            collapseHalfExtents = new Vector3(
                (float)math.max(6d, span.x * 0.5d + CollapseBoxHorizontalPaddingMeters),
                Mathf.Max(3f, largestRadius),
                (float)math.max(6d, span.z * 0.5d + CollapseBoxHorizontalPaddingMeters));
            return true;
        }

        private void ExecuteResourceCraterClusterCollapse(double3 absoluteCenter, Vector3 halfExtents, int clusterCount)
        {
            Vector3 runtimeCenter = HectonFloatingOrigin.ToRuntimePosition(absoluteCenter);
            if (_deltaProcessor != null)
            {
                SetBakeState(VoxelBakeState.Pending);
                _deltaProcessor.ApplyImmediateAbsoluteBoxCrater(this, absoluteCenter, halfExtents, DefaultDeltaMaterialId);
            }

            float majorHorizontalExtent = Mathf.Max(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.z));
            float minorHorizontalExtent = Mathf.Min(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.z));
            float impulseRadius = Mathf.Max(
                ResourceCraterClusterRadiusMeters,
                (majorHorizontalExtent + minorHorizontalExtent * 0.5f) * 1.35f);
            float impulseMagnitude = Mathf.Clamp(clusterCount * 3f + halfExtents.y * 2f, 12f, 48f);
            ApplyCollapseImpulse(runtimeCenter, halfExtents, impulseRadius, impulseMagnitude);

            SeismicShockwaveEvent shockwaveEvent = new SeismicShockwaveEvent(
                runtimeCenter,
                impulseRadius,
                impulseMagnitude,
                clusterCount);
            TryRaiseSeismicShockwaveEvent(in shockwaveEvent);
        }

        private void TryRaiseSeismicShockwaveEvent(in SeismicShockwaveEvent shockwaveEvent)
        {
            if (RandomEventEvents.TryRaiseSeismicShockwave(in shockwaveEvent))
                return;

            ReportSeismicShockwaveEventLaneDropIfBackpressured();
        }

        private void ReportSeismicShockwaveEventLaneDropIfBackpressured()
        {
            if (RandomEventEvents.PendingCount <= 0)
                return;

            _seismicShockwaveEventLaneDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _SeismicShockwaveEventLaneDropWarningHash,
                _SeismicShockwaveEventLaneContextHash ^ unchecked((uint)_runtimeStamp),
                math.max(1, _seismicShockwaveEventLaneDropCount));
        }

        private void ClearEventLaneDiagnostics()
        {
            _seismicShockwaveEventLaneDropCount = 0;
        }

        private static Vector3 ResolveCollapseTrenchDirection(Vector3 absoluteCenter)
        {
            uint seedA = unchecked((uint)CastBiasInt(absoluteCenter.x * 0.25f));
            uint seedB = unchecked((uint)CastBiasInt(absoluteCenter.z * 0.25f));
            uint state = seedA * 747796405u + seedB * 2891336453u + 0xB87F321Du;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;

            float2 direction = ResolveSeismicScatterDirection((int)(state & 7u));
            return new Vector3(direction.x, 0f, direction.y);
        }

        private static int CastBiasInt(float value)
        {
            return value >= 0f ? (int)(value + 0.5f) : (int)(value - 0.5f);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute)
        {
            originAbsolute = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            originAbsolute = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAbsolute));
        }

        private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFinite(runtimePosition) || !TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute))
                return false;

            absolutePosition = originAbsolute + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(absolutePosition));
        }

        private void ApplyCollapseImpulse(Vector3 runtimeCenter, Vector3 halfExtents, float impulseRadius, float impulseMagnitude)
        {
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            Vector3 queryHalfExtents = halfExtents + Vector3.one * 2f;
            float queryRadius = Mathf.Max(1f, queryHalfExtents.magnitude);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                runtimeCenter,
                queryRadius,
                kindMask,
                _collapseImpulseContacts);
            if (hitCount <= 0)
                return;

            int bodyCount = 0;
            int contactLimit = Mathf.Min(hitCount, _collapseImpulseContacts.Length);
            for (int i = 0; i < contactLimit; i++)
            {
                SpatialQueryHit hit = _collapseImpulseContacts[i];
                _collapseImpulseContacts[i] = default;
                if (!LayerMatchesMask(hit.Layer, _CollapseImpulseLayerMask))
                    continue;

                Vector3 delta = hit.Position - runtimeCenter;
                if (math.abs(delta.x) > queryHalfExtents.x ||
                    math.abs(delta.y) > queryHalfExtents.y ||
                    math.abs(delta.z) > queryHalfExtents.z)
                {
                    continue;
                }

                Rigidbody body = hit.Rigidbody;
                if (body == null || body.isKinematic || ContainsCollapseBody(body, bodyCount))
                    continue;

                _collapseImpulseBodies[bodyCount++] = body;
                if (bodyCount >= _collapseImpulseBodies.Length)
                    break;
            }

            float safeRadius = Mathf.Max(1f, impulseRadius);
            float safeRadiusSq = safeRadius * safeRadius;
            for (int i = 0; i < bodyCount; i++)
            {
                Rigidbody body = _collapseImpulseBodies[i];
                _collapseImpulseBodies[i] = null;
                if (body == null || body.isKinematic)
                    continue;

                Vector3 bodyCenter = body.worldCenterOfMass;
                Vector3 inward = runtimeCenter - bodyCenter;
                float distanceSq = inward.sqrMagnitude;
                if (distanceSq > safeRadiusSq)
                    continue;

                float3 impulseDirection;
                if (distanceSq > 0.0001f)
                {
                    float invDistance = 1f / math.max(ApproxMagnitude(inward), 0.0001f);
                    impulseDirection = new float3(inward.x * invDistance, inward.y * invDistance, inward.z * invDistance);
                }
                else
                {
                    impulseDirection = new float3(0f, -1f, 0f);
                }

                impulseDirection.y -= CollapseImpulseVerticalBias;
                float impulseDirectionLengthSq = math.lengthsq(impulseDirection);
                if (impulseDirectionLengthSq <= 0.0001f)
                    impulseDirection = new float3(0f, -1f, 0f);
                else
                    impulseDirection /= math.max(ApproxMagnitude(impulseDirection), 0.0001f);

                float distance01 = 1f - math.saturate(distanceSq / safeRadiusSq);
                float resolvedImpulse = impulseMagnitude * distance01 * distance01;
                Vector3 force = new Vector3(impulseDirection.x, impulseDirection.y, impulseDirection.z) * resolvedImpulse;
                _physicsService?.QueueForce(body, force, ForceMode.Impulse);
            }
        }

        private bool ContainsCollapseBody(Rigidbody candidate, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_collapseImpulseBodies[i] == candidate)
                    return true;
            }

            return false;
        }

        private static bool LayerMatchesMask(int layer, int mask)
        {
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
        }

        private static void BurnFloraAlongMagmaSegment(
            DestructibleOrganicManager organicManager,
            Vector3 start,
            Vector3 end,
            float segmentLength,
            float burnRadius)
        {
            if (organicManager == null || segmentLength <= 0f || burnRadius <= 0f)
                return;

            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(segmentLength / Mathf.Max(0.5f, burnRadius)),
                1,
                MaxMagmaVeinBurnSamplesPerSegment);
            Vector3 segment = end - start;
            Vector3 sampleStep = segment / sampleCount;
            Vector3 samplePosition = start;
            for (int i = 0; i <= sampleCount; i++)
            {
                organicManager.ApplyDefoliantDeadZone(samplePosition, burnRadius);
                samplePosition += sampleStep;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(float4 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float SaturateFinite(float value)
        {
            return IsFinite(value) ? math.saturate(value) : 0f;
        }

        private static float4 SaturateFinite(float4 value)
        {
            return IsFinite(value) ? math.saturate(value) : float4.zero;
        }

        private static Vector3 SanitizeColliderChunkProxyCenter(Vector3 center, Vector3 fallback)
        {
            if (IsFinite(center))
                return center;

            return IsFinite(fallback) ? fallback : Vector3.zero;
        }

        private void ResetColliderChunkBakeProxyShape(int index)
        {
            if (_colliderChunkBakeProxies == null ||
                (uint)index >= (uint)_colliderChunkBakeProxies.Length)
                return;

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy == null)
                return;

            proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;
            proxy.center = Vector3.zero;
            proxy.size = Vector3.one * MinColliderChunkProxySize;
        }

        private static Vector3 SanitizeColliderChunkProxySize(Vector3 size, Vector3 fallback)
        {
            return new Vector3(
                SanitizeColliderChunkProxySizeAxis(size.x, fallback.x),
                SanitizeColliderChunkProxySizeAxis(size.y, fallback.y),
                SanitizeColliderChunkProxySizeAxis(size.z, fallback.z));
        }

        private static float SanitizeColliderChunkProxySizeAxis(float value, float fallback)
        {
            if (IsFinite(value))
            {
                float magnitude = math.abs(value);
                if (magnitude >= MinColliderChunkProxySize)
                    return magnitude;
            }

            float fallbackMagnitude = IsFinite(fallback) ? math.abs(fallback) : 0f;
            return math.max(MinColliderChunkProxySize, fallbackMagnitude);
        }

        private static bool TryNormalizeFinite(float3 value, out float3 normalized)
        {
            normalized = default;
            if (!IsFinite(value))
                return false;

            float lengthSq = math.lengthsq(value);
            if (!IsFinite(lengthSq) || lengthSq <= 0.0001f)
                return false;

            normalized = value * math.rsqrt(lengthSq);
            return IsFinite(normalized);
        }

        private static bool TryResolveRuntimeSnapshotOffset(Vector3 absoluteUniverseOffset, out float3 offset)
        {
            offset = default;
            if (!IsFinite(absoluteUniverseOffset))
                return false;

            offset = new float3(absoluteUniverseOffset.x, absoluteUniverseOffset.y, absoluteUniverseOffset.z);
            return IsFinite(offset);
        }

        private static float ClampFinite(float value, float fallback, float minimum, float maximum)
        {
            return IsFinite(value) ? math.clamp(value, minimum, maximum) : fallback;
        }

        private static float3 ClampFinite(float3 value, float3 fallback, float minimum, float maximum)
        {
            return IsFinite(value) ? math.clamp(value, new float3(minimum), new float3(maximum)) : fallback;
        }

        private static bool TryBuildRuntimeNodeSnapshot(
            in CaveNode source,
            Vector3 absoluteUniverseOffset,
            out CaveNode snapshot)
        {
            snapshot = default;
            if (!TryResolveRuntimeSnapshotOffset(absoluteUniverseOffset, out float3 offset) ||
                !IsFinite(source.position) ||
                !IsFinite(source.radii))
            {
                return false;
            }

            if (math.cmin(source.radii) <= 0f)
                return false;

            float3 radii = ClampFinite(source.radii, new float3(MinRuntimeGraphRadius), MinRuntimeGraphRadius, MaxRuntimeGraphRadius);
            float3 position = source.position + offset;
            if (!IsFinite(position))
                return false;

            snapshot = source;
            snapshot.position = position;
            snapshot.radii = radii;
            snapshot.blendRadius = ClampFinite(source.blendRadius, MinRuntimeGraphRadius, MinRuntimeGraphRadius, MaxRuntimeGraphBlendRadius);
            snapshot.noiseScale = ClampFinite(source.noiseScale, 1f, 0.1f, MaxRuntimeGraphNoiseScale);
            snapshot.noiseAmplitude = ClampFinite(source.noiseAmplitude, 0f, 0f, MaxRuntimeGraphNoiseAmplitude);
            return true;
        }

        private static bool TryBuildRuntimeTunnelSnapshot(
            in CaveTunnel source,
            Vector3 absoluteUniverseOffset,
            out CaveTunnel snapshot)
        {
            snapshot = default;
            if (!TryResolveRuntimeSnapshotOffset(absoluteUniverseOffset, out float3 offset) ||
                !IsFinite(source.pointA) ||
                !IsFinite(source.pointB) ||
                !IsFinite(source.radiusA) ||
                !IsFinite(source.radiusB) ||
                source.radiusA <= 0f ||
                source.radiusB <= 0f)
            {
                return false;
            }

            float3 pointA = source.pointA + offset;
            float3 pointB = source.pointB + offset;
            if (!IsFinite(pointA) || !IsFinite(pointB))
                return false;

            snapshot = source;
            snapshot.pointA = pointA;
            snapshot.pointB = pointB;
            snapshot.radiusA = ClampFinite(source.radiusA, MinRuntimeGraphRadius, MinRuntimeGraphRadius, MaxRuntimeGraphRadius);
            snapshot.radiusB = ClampFinite(source.radiusB, MinRuntimeGraphRadius, MinRuntimeGraphRadius, MaxRuntimeGraphRadius);
            snapshot.blendRadius = ClampFinite(source.blendRadius, MinRuntimeGraphRadius, MinRuntimeGraphRadius, MaxRuntimeGraphBlendRadius);
            snapshot.heightScale = ClampFinite(source.heightScale, 1f, 0.1f, MaxRuntimeTunnelScale);
            snapshot.widthScale = ClampFinite(source.widthScale, 1f, 0.1f, MaxRuntimeTunnelScale);
            snapshot.warpAmount = ClampFinite(source.warpAmount, 0f, 0f, MaxRuntimeTunnelWarpAmount);
            return true;
        }

        private static bool TryBuildRuntimeStructureSnapshot(
            in CaveStructure source,
            Vector3 absoluteUniverseOffset,
            out CaveStructure snapshot)
        {
            snapshot = default;
            if (!TryResolveRuntimeSnapshotOffset(absoluteUniverseOffset, out float3 offset) ||
                !IsFinite(source.position) ||
                !IsFinite(source.size))
            {
                return false;
            }

            if (math.cmax(source.size) <= 0f)
                return false;

            float3 position = source.position + offset;
            if (!IsFinite(position))
                return false;

            float3 pointB = IsFinite(source.pointB) ? source.pointB + offset : position;
            if (!IsFinite(pointB))
                pointB = position;

            float3 size = ClampFinite(source.size, new float3(MinRuntimeGraphRadius), MinRuntimeGraphRadius, MaxRuntimeGraphRadius);
            snapshot = source;
            snapshot.position = position;
            snapshot.pointB = pointB;
            snapshot.size = size;
            snapshot.blendRadius = ClampFinite(source.blendRadius, MinRuntimeGraphRadius, MinRuntimeGraphRadius, MaxRuntimeGraphBlendRadius);
            snapshot.noiseAmount = ClampFinite(source.noiseAmount, 0f, 0f, MaxRuntimeGraphNoiseAmplitude);
            return true;
        }

        private static bool TryBuildRuntimeEntranceSnapshot(
            in CaveEntrance source,
            Vector3 absoluteUniverseOffset,
            out CaveEntrance snapshot)
        {
            snapshot = default;
            if (!TryResolveRuntimeSnapshotOffset(absoluteUniverseOffset, out float3 offset) ||
                !IsFinite(source.surfacePosition) ||
                !IsFinite(source.inwardDirection) ||
                !IsFinite(source.radius) ||
                !IsFinite(source.funnelLength) ||
                source.radius <= 0f ||
                source.funnelLength <= 0f ||
                !TryNormalizeFinite(source.inwardDirection, out float3 inwardDirection))
            {
                return false;
            }

            float3 surfacePosition = source.surfacePosition + offset;
            if (!IsFinite(surfacePosition))
                return false;

            snapshot = source;
            snapshot.surfacePosition = surfacePosition;
            snapshot.inwardDirection = inwardDirection;
            snapshot.radius = math.clamp(source.radius, MinRuntimeEntranceSnapshotRadius, MaxRuntimeEntranceSnapshotRadius);
            snapshot.funnelLength = math.clamp(source.funnelLength, MinRuntimeEntranceSnapshotFunnelLength, MaxRuntimeEntranceSnapshotFunnelLength);
            float innerRadiusFallback = math.max(snapshot.radius * 0.6f, MinRuntimeEntranceSnapshotInnerRadius);
            snapshot.innerRadius = IsFinite(source.innerRadius) && source.innerRadius > 0f
                ? math.clamp(source.innerRadius, MinRuntimeEntranceSnapshotInnerRadius, math.max(snapshot.radius, innerRadiusFallback))
                : innerRadiusFallback;
            snapshot.terrainNormalBlend = SaturateFinite(source.terrainNormalBlend);
            if (snapshot.terrainNormalBlend > 0f && TryNormalizeFinite(source.terrainNormal, out float3 terrainNormal))
            {
                snapshot.terrainNormal = terrainNormal;
            }
            else
            {
                snapshot.terrainNormal = new float3(0f, 1f, 0f);
                snapshot.terrainNormalBlend = 0f;
            }

            if (IsFinite(source.terrainSplatColor))
            {
                snapshot.terrainSplatColor = SaturateFinite(source.terrainSplatColor);
                snapshot.terrainSplatBlend = SaturateFinite(source.terrainSplatBlend);
            }
            else
            {
                snapshot.terrainSplatColor = float4.zero;
                snapshot.terrainSplatBlend = 0f;
            }
            return true;
        }

        private static Bounds BuildRuntimeSegmentAabb(Vector3 start, Vector3 end, float radius)
        {
            Vector3 min = Vector3.Min(start, end);
            Vector3 max = Vector3.Max(start, end);
            float padding = Mathf.Max(0f, radius);
            Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
            bounds.Expand(padding * 2f);
            return bounds;
        }

        private static Bounds BuildRuntimeAabb(Transform cachedTransform, Bounds localBounds)
        {
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            Bounds runtimeBounds = new Bounds(cachedTransform.TransformPoint(new Vector3(min.x, min.y, min.z)), Vector3.zero);
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(max.x, min.y, min.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(min.x, max.y, min.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(max.x, max.y, min.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(min.x, min.y, max.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(max.x, min.y, max.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(min.x, max.y, max.z)));
            runtimeBounds.Encapsulate(cachedTransform.TransformPoint(new Vector3(max.x, max.y, max.z)));
            return runtimeBounds;
        }

        private bool AppendCraterStamp(double3 absolutePosition, float radius, bool queueRebuild)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return false;

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            float blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f);
            if (_craterStampCount < 0 || _craterStampCount > _craterStamps.Length)
            {
                _craterStamps.Clear();
                _craterStampCount = 0;
                return false;
            }

            for (int i = 0; i < _craterStampCount; i++)
            {
                VoxelCraterStamp existing = _craterStamps[i];
                float mergeDistance = existing.radius + clampedRadius * 0.35f;
                double3 mergeDelta = existing.position - absolutePosition;
                if (math.lengthsq(mergeDelta) > (double)mergeDistance * mergeDistance)
                    continue;

                existing.position = (existing.position + absolutePosition) * 0.5d;
                existing.radius = Mathf.Max(existing.radius, clampedRadius);
                existing.blendRadius = Mathf.Max(existing.blendRadius, blendRadius);
                _craterStamps[i] = existing;

                if (queueRebuild)
                    QueueRebuild();

                return true;
            }

            if (_craterStampCount >= MaxCraterStampCount)
            {
                for (int i = 1; i < _craterStampCount; i++)
                    _craterStamps[i - 1] = _craterStamps[i];

                _craterStampCount = MaxCraterStampCount - 1;
            }

            VoxelCraterStamp craterStamp = new VoxelCraterStamp
            {
                position = absolutePosition,
                radius = clampedRadius,
                blendRadius = blendRadius
            };
            if (_craterStamps.Length > _craterStampCount)
                _craterStamps[_craterStampCount] = craterStamp;
            else
                _craterStamps.Add(craterStamp);

            _craterStampCount++;

            if (queueRebuild)
                QueueRebuild();

            return true;
        }

        private bool TryResolveSeismicCollapseAnchor(
            Transform cachedTransform,
            Bounds localBounds,
            float localX,
            float localZ,
            float ceilingSearchDepth,
            float craterRadius,
            out Vector3 localAnchor)
        {
            localAnchor = default;
            float sampleStep = Mathf.Max(_voxelSize * 0.75f, 0.5f);
            float startY = localBounds.max.y - _voxelSize;
            float minY = Mathf.Max(localBounds.min.y + _voxelSize, startY - ceilingSearchDepth);
            bool hasPreviousSample = false;
            bool previousSolid = false;

            if (!TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out _,
                    out PublishedSonarSdfReadLease sdfLease))
            {
                return false;
            }

            try
            {
                for (float sampleY = startY; sampleY >= minY; sampleY -= sampleStep)
                {
                    Vector3 worldSample = cachedTransform.TransformPoint(new Vector3(localX, sampleY, localZ));
                    if (!TrySamplePublishedDensity(
                            encodedSdf,
                            gridDimensions,
                            volumeOrigin,
                            voxelCellSize,
                            sdfRange,
                            worldSample,
                            out float density,
                            out _))
                    {
                        continue;
                    }

                    bool currentSolid = density > 0f;
                    if (hasPreviousSample && previousSolid && !currentSolid)
                    {
                        float anchorY = Mathf.Clamp(
                            sampleY + sampleStep * 0.35f + craterRadius * 0.15f,
                            localBounds.min.y + _voxelSize,
                            localBounds.max.y - _voxelSize * 0.5f);
                        localAnchor = new Vector3(localX, anchorY, localZ);
                        return true;
                    }

                    previousSolid = currentSolid;
                    hasPreviousSample = true;
                }

                return false;
            }
            finally
            {
                ReleasePublishedSonarSdfPayloadReadLease(in sdfLease);
            }
        }

        private bool TryResolveTopSolidAnchor(
            Transform cachedTransform,
            Bounds localBounds,
            float localX,
            float localZ,
            float cutDepth,
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            float sdfRange,
            out Vector3 localAnchor)
        {
            localAnchor = default;
            float sampleStep = Mathf.Max(_voxelSize * 0.75f, 0.5f);
            float startY = localBounds.max.y - _voxelSize * 0.5f;
            float minY = localBounds.min.y + _voxelSize * 0.5f;

            for (float sampleY = startY; sampleY >= minY; sampleY -= sampleStep)
            {
                Vector3 worldSample = cachedTransform.TransformPoint(new Vector3(localX, sampleY, localZ));
                if (!TrySamplePublishedDensity(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        worldSample,
                        out float density,
                        out _))
                {
                    continue;
                }

                if (density <= 0f)
                    continue;

                float anchorY = Mathf.Clamp(
                    sampleY - cutDepth * 0.5f,
                    localBounds.min.y + _voxelSize,
                    localBounds.max.y - _voxelSize * 0.5f);
                localAnchor = new Vector3(localX, anchorY, localZ);
                return true;
            }

            return false;
        }

        private bool TryResolveNearestSolidDistance(
            Vector3 worldPosition,
            Bounds localBounds,
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            float sdfRange,
            out float distanceMeters)
        {
            distanceMeters = float.PositiveInfinity;
            Transform cachedTransform = transform;
            Vector3 localPoint = cachedTransform.InverseTransformPoint(worldPosition);
            float sampleStep = Mathf.Max(_voxelSize * 0.75f, 0.5f);
            float maxSearchDistance = Mathf.Max(_voxelSize * 8f, 12f);

            for (float offset = 0f; offset <= maxSearchDistance; offset += sampleStep)
            {
                Vector3 upSample = localPoint + new Vector3(0f, offset, 0f);
                if (localBounds.Contains(upSample) &&
                    TrySamplePublishedDensity(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        cachedTransform.TransformPoint(upSample),
                        out float upDensity,
                        out _) &&
                    upDensity > 0f)
                {
                    distanceMeters = offset;
                    return true;
                }

                if (offset <= 0f)
                    continue;

                Vector3 downSample = localPoint - new Vector3(0f, offset, 0f);
                if (localBounds.Contains(downSample) &&
                    TrySamplePublishedDensity(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        cachedTransform.TransformPoint(downSample),
                        out float downDensity,
                        out _) &&
                    downDensity > 0f)
                {
                    distanceMeters = offset;
                    return true;
                }
            }

            return false;
        }

        private bool HasSolidDensityPath(
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            NativeArray<byte>.ReadOnly encodedSdf,
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            float sdfRange)
        {
            Vector3 pathDelta = endWorldPosition - startWorldPosition;
            float dominantAxisLength = math.cmax(math.abs(new float3(pathDelta.x, pathDelta.y, pathDelta.z)));
            float sampleStep = math.max(_voxelSize * 0.75f, 0.5f);
            int sampleCount = math.max(2, (int)math.ceil(dominantAxisLength / sampleStep));
            Vector3 sampleDelta = pathDelta / sampleCount;
            Vector3 samplePosition = startWorldPosition;

            for (int i = 0; i <= sampleCount; i++)
            {
                if (!TrySamplePublishedDensity(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        voxelCellSize,
                        sdfRange,
                        samplePosition,
                        out float density,
                        out _) ||
                    density <= 0f)
                {
                    return false;
                }

                samplePosition += sampleDelta;
            }

            return true;
        }

        private static float Hash01(uint seed, int index, int salt)
        {
            unchecked
            {
                uint hash = seed;
                hash ^= (uint)index * 2246822519u;
                hash ^= (uint)salt * 3266489917u;
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}

namespace Hecton8.World
{
    /// <summary>
    /// Intrusive voxel lifecycle sentry. Tracks volumes that were explicitly
    /// destroyed, then reports if Unity never finalizes them within 300 frames.
    /// </summary>
    internal static class VoxelVolumeLeakSentinel
    {
        private const int FinalizeDeadlineFrames = 300;
        private const byte StateFree = 0;
        private const byte StateAlive = 1;
        private const byte StateDestroyPending = 2;
        private const byte StateReported = 3;
        private static readonly uint _CriticalMemoryLeakHash = unchecked((uint)Hecton.Localization.LocHash.Compute("CRITICAL_MEMORY_LEAK"));
        private static readonly uint _VoxelVolumeLeakContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelVolumeLeakSentinel"));

        private static readonly LeakSentinelLateFrameDriver s_driver = new LeakSentinelLateFrameDriver();
        private static Hecton8.Caves.HectonVoxelVolume s_head;
        private static Hecton8.Core.SystemDispatcher s_registeredDispatcher;
        private static bool s_driverRegistered;
        private static int s_pendingDestroyCount;
        private static int s_trackedVolumeCount;

        internal static void RegisterVolume(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            if (volume._leakSentinelState == StateFree)
                AddNode(volume);

            if (volume._leakSentinelState == StateDestroyPending)
                s_pendingDestroyCount--;

            volume._leakDestroyRequestedFrame = 0;
            volume._leakSentinelState = StateAlive;
        }

        internal static void MarkDestroyRequested(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            if (volume._leakSentinelState == StateFree)
                AddNode(volume);

            if (volume._leakSentinelState != StateDestroyPending)
                s_pendingDestroyCount++;

            volume._leakDestroyRequestedFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            volume._leakSentinelState = StateDestroyPending;
            EnsureDriverRegistered();
        }

        internal static void RebindDispatcher(object previousService, object currentService)
        {
            if (!s_driverRegistered)
            {
                if (s_pendingDestroyCount > 0 && currentService != null)
                    EnsureDriverRegistered();
                return;
            }

            if (ReferenceEquals(currentService, s_registeredDispatcher))
                return;

            if (previousService != null && !ReferenceEquals(previousService, s_registeredDispatcher))
                return;

            Hecton8.Core.SystemDispatcher.UnregisterLateFrameTickableDirect(
                s_driver,
                Hecton8.Core.PriorityLayer.Environment);
            s_registeredDispatcher = null;
            s_driverRegistered = false;

            if (s_pendingDestroyCount > 0 && currentService != null)
                EnsureDriverRegistered();
        }

        internal static void MarkReleasedToPool(Hecton8.Caves.HectonVoxelVolume volume)
        {
            FinalizeVolume(volume);
        }

        internal static void FinalizeVolume(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null || volume._leakSentinelState == StateFree)
                return;

            if (volume._leakSentinelState == StateDestroyPending)
                s_pendingDestroyCount--;

            RemoveNode(volume);
            TryUnregisterDriver();
        }

        private static void Pump()
        {
            if (s_pendingDestroyCount <= 0)
            {
                TryUnregisterDriver();
                return;
            }

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            Hecton8.Caves.HectonVoxelVolume volume = s_head;
            while (!ReferenceEquals(volume, null))
            {
                Hecton8.Caves.HectonVoxelVolume next = volume._leakSentinelNext;
                if (volume._leakSentinelState != StateDestroyPending)
                {
                    volume = next;
                    continue;
                }

                if (volume == null)
                {
                    RemoveNode(volume);
                    s_pendingDestroyCount--;
                    volume = next;
                    continue;
                }

                int pendingFrames = frame - volume._leakDestroyRequestedFrame;
                if (pendingFrames < FinalizeDeadlineFrames)
                {
                    volume = next;
                    continue;
                }

                volume._leakSentinelState = StateReported;
                s_pendingDestroyCount--;
                Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                    _CriticalMemoryLeakHash,
                    _VoxelVolumeLeakContextHash,
                    unchecked((uint)pendingFrames));
                volume = next;
            }

            TryUnregisterDriver();
        }

        private static void AddNode(Hecton8.Caves.HectonVoxelVolume volume)
        {
            volume._leakSentinelPrev = null;
            volume._leakSentinelNext = s_head;
            if (!ReferenceEquals(s_head, null))
                s_head._leakSentinelPrev = volume;

            s_head = volume;
            s_trackedVolumeCount++;
        }

        private static void RemoveNode(Hecton8.Caves.HectonVoxelVolume volume)
        {
            Hecton8.Caves.HectonVoxelVolume prev = volume._leakSentinelPrev;
            Hecton8.Caves.HectonVoxelVolume next = volume._leakSentinelNext;
            if (!ReferenceEquals(prev, null))
                prev._leakSentinelNext = next;
            else if (ReferenceEquals(s_head, volume))
                s_head = next;

            if (!ReferenceEquals(next, null))
                next._leakSentinelPrev = prev;

            volume._leakSentinelPrev = null;
            volume._leakSentinelNext = null;
            volume._leakDestroyRequestedFrame = 0;
            volume._leakSentinelState = StateFree;
            if (s_trackedVolumeCount > 0)
                s_trackedVolumeCount--;
        }

        private static void EnsureDriverRegistered()
        {
            Hecton8.Core.SystemDispatcher dispatcher = Hecton8.Core.GlobalRegistry.Dispatcher;
            if (s_driverRegistered ||
                !UnityEngine.Application.isPlaying ||
                dispatcher == null)
            {
                return;
            }

            s_driverRegistered = Hecton8.Core.GlobalRegistry.TryRegisterLateFrameTickable(
                s_driver,
                Hecton8.Core.PriorityLayer.Environment);
            if (s_driverRegistered)
                s_registeredDispatcher = dispatcher;
        }

        private static void TryUnregisterDriver()
        {
            if (!s_driverRegistered ||
                s_pendingDestroyCount > 0)
            {
                return;
            }

            Hecton8.Core.SystemDispatcher.UnregisterLateFrameTickableDirect(
                s_driver,
                Hecton8.Core.PriorityLayer.Environment);
            s_registeredDispatcher = null;
            s_driverRegistered = false;
        }

        private sealed class LeakSentinelLateFrameDriver : Hecton8.Core.ILateFrameTickable
        {
            public void LateFrameTick()
            {
                Pump();
            }
        }
    }
}
