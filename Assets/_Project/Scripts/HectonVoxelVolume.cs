// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelVolume.cs — Project HECTON-8 Voxel Volume Component         ║
// ║  Unity 6 | Simple component for cave volumes                             ║
// ║  v1.0 — Basic volume marker                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime subtractive crater stamp applied to the voxel SDF field.
    /// Stored on the generated volume and replayed during async rebuilds.
    /// </summary>
    public struct VoxelCraterStamp
    {
        public Vector3 position;
        public float radius;
        public float blendRadius;
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
    public struct VoxelSdfRaycastHit
    {
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public float Density;
        public byte Hit;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VoxelSdfRaymarchJob : IJob
    {
        [ReadOnly] public NativeArray<byte> EncodedSdf;
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
            if (!EncodedSdf.IsCreated ||
                !Result.IsCreated ||
                Result.Length <= 0 ||
                GridDimensions.x <= 1 ||
                GridDimensions.y <= 1 ||
                GridDimensions.z <= 1 ||
                SdfRange <= 0f ||
                MaxDistance <= 0f)
            {
                return;
            }

            float3 direction = NormalizeSafe(Direction, new float3(0f, 0f, 1f));
            float step = math.max(0.05f, StepMeters);
            float previousDensity = 0f;
            float3 previousPosition = Origin;
            bool hasPrevious = false;
            for (float distance = 0f; distance <= MaxDistance; distance += step)
            {
                float3 position = Origin + direction * distance;
                float density = Sample(position);
                if ((density >= 0f && (!hasPrevious || previousDensity < 0f)) ||
                    (hasPrevious && previousDensity < 0f && density >= 0f))
                {
                    float denom = math.max(0.0001f, density - previousDensity);
                    float t = hasPrevious ? math.saturate(-previousDensity / denom) : 0f;
                    float3 resolvedPoint = math.lerp(previousPosition, position, t);
                    Result[0] = new VoxelSdfRaycastHit
                    {
                        Point = new Vector3(resolvedPoint.x, resolvedPoint.y, resolvedPoint.z),
                        Normal = new Vector3(0f, 1f, 0f),
                        Distance = math.max(0f, distance - step + step * t),
                        Density = density,
                        Hit = 1
                    };
                    return;
                }

                previousDensity = density;
                previousPosition = position;
                hasPrevious = true;
            }
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
    public sealed class HectonVoxelVolume : MonoBehaviour
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
        private const int MaxPlasmaCutSteps = 24;
        private const int MaxQueuedRebuildPassesPerKick = 4;
        private const int MaxMagmaVeinBurnSamplesPerSegment = 16;
        private const string NativeMemoryOwner = nameof(HectonVoxelVolume);
        private const float ResourceCraterClusterRadiusMeters = 20f;
        private const float CollapseBoxHorizontalPaddingMeters = 4f;
        private const float CollapseImpulseVerticalBias = 0.45f;
        private const float MinPlasmaCutPower = 0.02f;
        private const float PlasmaCutAttenuationPerMeter = 1f;
        private const byte DefaultDeltaMaterialId = 0;
        private const byte SedimentDeltaMaterialId = 1;
        private const byte MagmaDeltaMaterialId = 2;
        private const byte DefaultPublishedSonarAudioMaterialId = 2;
        private static readonly int _CollapseImpulseLayerMask = HectonLayerMasks.MountedSweepLayerMask;
        private const float OrganicRootMoundMinimumOverlapMeters = 0.25f;
        private const float OrganicRootMoundSeabedProbeStepMeters = 0.5f;
        private const int OrganicRootMoundSeabedProbeSteps = 16;
        private const string ColliderChunkRuntimeName = "ColliderChunk";
        private const string ColliderChunkProxyRuntimeName = "ColliderChunkProxy";

        // COLD ALLOC: List<HectonVoxelVolume>[32] - scanner SDF raymarch candidates - owner: HectonVoxelVolume
        private static readonly List<HectonVoxelVolume> s_activePublishedVolumes = new List<HectonVoxelVolume>(32);

        private HectonVoxelEngine _engine;
        private VoxelDeltaProcessor _deltaProcessor;
        private CaveNode[] _nodes = Array.Empty<CaveNode>();
        private CaveTunnel[] _tunnels = Array.Empty<CaveTunnel>();
        private CaveEntrance[] _entrances = Array.Empty<CaveEntrance>();
        private CaveStructure[] _structures = Array.Empty<CaveStructure>();
        private VoxelCraterStamp[] _craterStamps = Array.Empty<VoxelCraterStamp>();
        private VoxelCraterStamp[] _resourceCraterClusterStamps = Array.Empty<VoxelCraterStamp>();
        private Collider[] _collapseImpulseColliders = Array.Empty<Collider>();
        private Rigidbody[] _collapseImpulseBodies = Array.Empty<Rigidbody>();
        private int _craterStampCount;
        private int _resourceCraterClusterCount;
        private int _runtimeStamp;
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
        private Vector3 _lastCollapseAbsoluteCenter;
        private int[] _terrainHoleHandles = Array.Empty<int>();
        private int _terrainHoleHandleCount;
        private Transform _colliderChunkRoot;
        private MeshCollider[] _colliderChunkColliders = Array.Empty<MeshCollider>();
        private BoxCollider[] _colliderChunkBakeProxies = Array.Empty<BoxCollider>();
        private Mesh[] _colliderChunkMeshes = Array.Empty<Mesh>();
        private Mesh[] _colliderChunkBakeMeshes = Array.Empty<Mesh>();
        private MeshRenderer _meshRenderer;
        private MeshCollider _rootMeshCollider;
        private VoxelBakeState _bakeState;
        private NativeArray<byte> _publishedSonarSdf;
        private NativeArray<byte> _publishedSonarAudioMaterialIds;
        private Vector3Int _publishedSonarGridDimensions;
        private Vector3 _publishedSonarOrigin;
        private Vector3 _publishedSonarCellSize;
        private float _publishedSonarSdfRange;
        private int _publishedSonarVersion;

        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        /// <summary>Deterministic seed used to generate this volume.</summary>
        public uint Seed => _seed;

        /// <summary>Absolute-universe center captured when this volume payload was built.</summary>
        public Vector3 GenerationAbsoluteUniversePosition => _generationAbsoluteUniversePosition;

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

        /// <summary>Bounded subtractive crater registry replayed during rebuilds.</summary>
        public VoxelCraterStamp[] CraterStamps => _craterStamps;

        /// <summary>Active crater stamp count inside <see cref="CraterStamps"/>.</summary>
        public int CraterStampCount => _craterStampCount;

        /// <summary>Generation stamp used to reject stale async rebuild completions.</summary>
        public int RuntimeStamp => _runtimeStamp;

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
            for (int i = s_activePublishedVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume candidate = s_activePublishedVolumes[i];
                if (candidate == null || !candidate._runtimeDataReady)
                {
                    s_activePublishedVolumes.RemoveAt(i);
                    continue;
                }

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

        internal static bool TryGetClosestPublishedSonarSdfPayload(
            Vector3 runtimeOrigin,
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version)
        {
            encodedSdf = default;
            audioMaterialIds = default;
            gridDimensions = default;
            volumeOrigin = default;
            voxelCellSize = default;
            sdfRange = 0f;
            version = 0;

            float bestDistanceSq = float.MaxValue;
            bool resolved = false;
            for (int i = s_activePublishedVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume candidate = s_activePublishedVolumes[i];
                if (candidate == null || !candidate._runtimeDataReady)
                {
                    s_activePublishedVolumes.RemoveAt(i);
                    continue;
                }

                if (!candidate.TryGetPublishedSonarSdfPayload(
                        out NativeArray<byte> candidateSdf,
                        out NativeArray<byte> candidateMaterialIds,
                        out Vector3Int candidateDimensions,
                        out Vector3 candidateOrigin,
                        out Vector3 candidateCellSize,
                        out float candidateSdfRange,
                        out int candidateVersion))
                {
                    continue;
                }

                Vector3 center = candidateOrigin + new Vector3(
                    candidateCellSize.x * math.max(0, candidateDimensions.x - 1) * 0.5f,
                    candidateCellSize.y * math.max(0, candidateDimensions.y - 1) * 0.5f,
                    candidateCellSize.z * math.max(0, candidateDimensions.z - 1) * 0.5f);
                float distanceSq = (center - runtimeOrigin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                encodedSdf = candidateSdf;
                audioMaterialIds = candidateMaterialIds;
                gridDimensions = candidateDimensions;
                volumeOrigin = candidateOrigin;
                voxelCellSize = candidateCellSize;
                sdfRange = candidateSdfRange;
                version = candidateVersion;
                resolved = true;
            }

            return resolved;
        }

        public static bool TryDepositAdditiveSdfSphere(Vector3 absoluteCenter, float radiusMeters, float strengthMeters)
        {
            float radius = Mathf.Max(0.05f, radiusMeters);
            float strength = Mathf.Max(0.05f, strengthMeters);
            for (int i = s_activePublishedVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume candidate = s_activePublishedVolumes[i];
                if (candidate == null || !candidate._runtimeDataReady)
                {
                    s_activePublishedVolumes.RemoveAt(i);
                    continue;
                }

                float halfExtent = candidate._gridDimension * candidate._voxelSize * 0.5f;
                float acceptedRadius = halfExtent + radius;
                if ((candidate._generationAbsoluteUniversePosition - absoluteCenter).sqrMagnitude > acceptedRadius * acceptedRadius ||
                    candidate._deltaProcessor == null)
                {
                    continue;
                }

                candidate.SetBakeState(VoxelBakeState.Pending);
                candidate._deltaProcessor.ApplyImmediateAbsoluteWeld(candidate, absoluteCenter, radius, strength, MagmaDeltaMaterialId);
                return true;
            }

            return false;
        }

        public static float GetSDFDensity(float3 aupPosition)
        {
            return GetSDFDensity(aupPosition, out float density) ? density : 0f;
        }

        public static bool GetSDFDensity(float3 aupPosition, out float density)
        {
            density = 0f;
            Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(new Vector3(
                aupPosition.x,
                aupPosition.y,
                aupPosition.z));

            for (int i = s_activePublishedVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume candidate = s_activePublishedVolumes[i];
                if (candidate == null || !candidate._runtimeDataReady)
                {
                    s_activePublishedVolumes.RemoveAt(i);
                    continue;
                }

                if (candidate.TrySampleDensity(runtimePosition, out density))
                    return true;
            }

            return false;
        }

        private static void RegisterPublishedVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            for (int i = 0; i < s_activePublishedVolumes.Count; i++)
            {
                if (ReferenceEquals(s_activePublishedVolumes[i], volume))
                    return;
            }

            s_activePublishedVolumes.Add(volume);
        }

        private static void UnregisterPublishedVolume(HectonVoxelVolume volume)
        {
            for (int i = s_activePublishedVolumes.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_activePublishedVolumes[i], volume) || s_activePublishedVolumes[i] == null)
                    s_activePublishedVolumes.RemoveAt(i);
            }
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

            if (!_runtimeDataReady ||
                !_publishedSonarSdf.IsCreated ||
                _publishedSonarGridDimensions.x <= 1 ||
                _publishedSonarGridDimensions.y <= 1 ||
                _publishedSonarGridDimensions.z <= 1 ||
                _publishedSonarSdfRange <= 0f)
            {
                return false;
            }

            float cellSizeX = Mathf.Max(0.0001f, _publishedSonarCellSize.x);
            float cellSizeY = Mathf.Max(0.0001f, _publishedSonarCellSize.y);
            float cellSizeZ = Mathf.Max(0.0001f, _publishedSonarCellSize.z);
            float sampleX = Mathf.Clamp(
                (worldPosition.x - _publishedSonarOrigin.x) / cellSizeX,
                0f,
                _publishedSonarGridDimensions.x - 1.001f);
            float sampleY = Mathf.Clamp(
                (worldPosition.y - _publishedSonarOrigin.y) / cellSizeY,
                0f,
                _publishedSonarGridDimensions.y - 1.001f);
            float sampleZ = Mathf.Clamp(
                (worldPosition.z - _publishedSonarOrigin.z) / cellSizeZ,
                0f,
                _publishedSonarGridDimensions.z - 1.001f);

            density = DecodePublishedDensity(sampleX, sampleY, sampleZ);
            density01 = Mathf.Clamp01(Mathf.Max(0f, density) / _publishedSonarSdfRange);
            return true;
        }

        /// <summary>
        /// Samples the published runtime SDF payload and returns only the decoded density.
        /// </summary>
        public bool TrySampleDensity(Vector3 worldPosition, out float density)
        {
            return TrySampleDensity(worldPosition, out density, out _);
        }

        /// <summary>
        /// Samples the published sonar material atlas at the nearest runtime-space SDF node.
        /// </summary>
        public bool TrySamplePublishedSonarAudioMaterialId(Vector3 worldPosition, out byte audioMaterialId)
        {
            audioMaterialId = DefaultPublishedSonarAudioMaterialId;
            if (!_runtimeDataReady ||
                !_publishedSonarAudioMaterialIds.IsCreated ||
                _publishedSonarGridDimensions.x <= 1 ||
                _publishedSonarGridDimensions.y <= 1 ||
                _publishedSonarGridDimensions.z <= 1)
            {
                return false;
            }

            float cellSizeX = Mathf.Max(0.0001f, _publishedSonarCellSize.x);
            float cellSizeY = Mathf.Max(0.0001f, _publishedSonarCellSize.y);
            float cellSizeZ = Mathf.Max(0.0001f, _publishedSonarCellSize.z);
            float invCellSizeX = math.rcp(cellSizeX);
            float invCellSizeY = math.rcp(cellSizeY);
            float invCellSizeZ = math.rcp(cellSizeZ);
            int maxX = _publishedSonarGridDimensions.x - 1;
            int maxY = _publishedSonarGridDimensions.y - 1;
            int maxZ = _publishedSonarGridDimensions.z - 1;
            int x = Mathf.Clamp((int)(((worldPosition.x - _publishedSonarOrigin.x) * invCellSizeX) + 0.5f), 0, maxX);
            int y = Mathf.Clamp((int)(((worldPosition.y - _publishedSonarOrigin.y) * invCellSizeY) + 0.5f), 0, maxY);
            int z = Mathf.Clamp((int)(((worldPosition.z - _publishedSonarOrigin.z) * invCellSizeZ) + 0.5f), 0, maxZ);
            int index = x + (_publishedSonarGridDimensions.x * (y + (_publishedSonarGridDimensions.y * z)));
            if ((uint)index >= (uint)_publishedSonarAudioMaterialIds.Length)
                return false;

            audioMaterialId = _publishedSonarAudioMaterialIds[index];
            return true;
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
            if (!_runtimeDataReady ||
                !_publishedSonarSdf.IsCreated ||
                _publishedSonarGridDimensions.x <= 1 ||
                _publishedSonarGridDimensions.y <= 1 ||
                _publishedSonarGridDimensions.z <= 1 ||
                maxDistance <= 0f)
            {
                return false;
            }

            Vector3 direction = NormalizeApprox(runtimeDirection, Vector3.forward);
            float step = Mathf.Max(0.05f, stepMeters);
            float previousDensity = 0f;
            bool hasPrevious = false;
            Vector3 previousPosition = runtimeOrigin;
            for (float distance = 0f; distance <= maxDistance; distance += step)
            {
                Vector3 position = runtimeOrigin + direction * distance;
                if (!TrySampleDensity(position, out float density, out _))
                    continue;

                if ((density >= 0f && (!hasPrevious || previousDensity < 0f)) ||
                    (hasPrevious && previousDensity < 0f && density >= 0f))
                {
                    float denom = Mathf.Max(0.0001f, density - previousDensity);
                    float t = hasPrevious ? Mathf.Clamp01(-previousDensity / denom) : 0f;
                    Vector3 resolvedPoint = previousPosition + (position - previousPosition) * t;
                    hit = new VoxelSdfRaycastHit
                    {
                        Point = resolvedPoint,
                        Normal = -direction,
                        Distance = Mathf.Max(0f, distance - step + step * t),
                        Density = density,
                        Hit = 1
                    };
                    return true;
                }

                previousDensity = density;
                previousPosition = position;
                hasPrevious = true;
            }

            return false;
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

            if (!_runtimeDataReady ||
                !_publishedSonarSdf.IsCreated ||
                _publishedSonarGridDimensions.x <= 1 ||
                _publishedSonarGridDimensions.y <= 1 ||
                _publishedSonarGridDimensions.z <= 1 ||
                _publishedSonarSdfRange <= 0f)
            {
                return false;
            }

            float cellSizeX = Mathf.Max(0.0001f, _publishedSonarCellSize.x);
            float cellSizeY = Mathf.Max(0.0001f, _publishedSonarCellSize.y);
            float cellSizeZ = Mathf.Max(0.0001f, _publishedSonarCellSize.z);
            int maxX = _publishedSonarGridDimensions.x - 1;
            int maxY = _publishedSonarGridDimensions.y - 1;
            int maxZ = _publishedSonarGridDimensions.z - 1;
            int x = Mathf.Clamp((int)(((worldPosition.x - _publishedSonarOrigin.x) / cellSizeX) + 0.5f), 0, maxX);
            int y = Mathf.Clamp((int)(((worldPosition.y - _publishedSonarOrigin.y) / cellSizeY) + 0.5f), 0, maxY);
            int z = Mathf.Clamp((int)(((worldPosition.z - _publishedSonarOrigin.z) / cellSizeZ) + 0.5f), 0, maxZ);
            float center = DecodePublishedDensityAt(x, y, z);

            gradient = new Vector3(
                x < maxX
                    ? (DecodePublishedDensityAt(x + 1, y, z) - center) / cellSizeX
                    : (center - DecodePublishedDensityAt(x - 1, y, z)) / cellSizeX,
                y < maxY
                    ? (DecodePublishedDensityAt(x, y + 1, z) - center) / cellSizeY
                    : (center - DecodePublishedDensityAt(x, y - 1, z)) / cellSizeY,
                z < maxZ
                    ? (DecodePublishedDensityAt(x, y, z + 1) - center) / cellSizeZ
                    : (center - DecodePublishedDensityAt(x, y, z - 1)) / cellSizeZ);
            return true;
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
            if (!TryResolveNearestSolidDistance(preyWorldPosition, localBounds, out float preySolidDistance) ||
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
                    out Vector3 localSolidAnchor))
            {
                return false;
            }

            Vector3 localBreach = localSolidAnchor + new Vector3(0f, Mathf.Max(_voxelSize, breachOffsetMeters), 0f);
            Vector3 candidateSolidAnchor = cachedTransform.TransformPoint(localSolidAnchor);
            Vector3 candidateBreach = cachedTransform.TransformPoint(localBreach);
            if (!HasSolidDensityPath(predatorWorldPosition, candidateSolidAnchor))
                return false;

            solidAnchorWorldPosition = candidateSolidAnchor;
            breachWorldPosition = candidateBreach;
            return true;
        }

        private static int ResolveDominantAxis(Vector3 normal)
        {
            Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                return 0;

            return absNormal.y >= absNormal.z ? 1 : 2;
        }

        private float DecodePublishedDensity(float sampleX, float sampleY, float sampleZ)
        {
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int z0 = Mathf.FloorToInt(sampleZ);

            int x1 = Mathf.Min(x0 + 1, _publishedSonarGridDimensions.x - 1);
            int y1 = Mathf.Min(y0 + 1, _publishedSonarGridDimensions.y - 1);
            int z1 = Mathf.Min(z0 + 1, _publishedSonarGridDimensions.z - 1);

            float tx = sampleX - x0;
            float ty = sampleY - y0;
            float tz = sampleZ - z0;

            float c000 = DecodePublishedDensityAt(x0, y0, z0);
            float c100 = DecodePublishedDensityAt(x1, y0, z0);
            float c010 = DecodePublishedDensityAt(x0, y1, z0);
            float c110 = DecodePublishedDensityAt(x1, y1, z0);
            float c001 = DecodePublishedDensityAt(x0, y0, z1);
            float c101 = DecodePublishedDensityAt(x1, y0, z1);
            float c011 = DecodePublishedDensityAt(x0, y1, z1);
            float c111 = DecodePublishedDensityAt(x1, y1, z1);

            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float c0 = math.lerp(c00, c10, ty);
            float c1 = math.lerp(c01, c11, ty);
            return math.lerp(c0, c1, tz);
        }

        private float DecodePublishedDensityAt(int x, int y, int z)
        {
            int index = x +
                        (_publishedSonarGridDimensions.x * (y + (_publishedSonarGridDimensions.y * z)));
            if ((uint)index >= (uint)_publishedSonarSdf.Length)
                return 0f;

            float normalized = (_publishedSonarSdf[index] / 255f) * 2f - 1f;
            return normalized * _publishedSonarSdfRange;
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
            ResetColliderChunks(false);
            caveKey = 0L;
            generationPosition = Vector3.zero;
            preset = null;
            _engine = null;
            _deltaProcessor = null;
            _generationAbsoluteUniversePosition = Vector3.zero;
            _nodes = Array.Empty<CaveNode>();
            _tunnels = Array.Empty<CaveTunnel>();
            _entrances = Array.Empty<CaveEntrance>();
            _structures = Array.Empty<CaveStructure>();
            _craterStamps = Array.Empty<VoxelCraterStamp>();
            _resourceCraterClusterStamps = Array.Empty<VoxelCraterStamp>();
            _collapseImpulseColliders = Array.Empty<Collider>();
            _collapseImpulseBodies = Array.Empty<Rigidbody>();
            _craterStampCount = 0;
            _resourceCraterClusterCount = 0;
            _lastCollapseClusterValid = false;
            _lastCollapseAbsoluteCenter = Vector3.zero;
            _runtimeDataReady = false;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _seed = 0u;
            _gridDimension = 0;
            _voxelSize = 0f;
            _lodLevel = 0;
            _buildCollider = true;
            _caveParams = default;
            _terrainHoleHandles = Array.Empty<int>();
            _terrainHoleHandleCount = 0;
            ClearPublishedSonarSdf();
            _runtimeStamp++;
            CacheRuntimeComponents();
            SetBakeState(VoxelBakeState.Idle);

            ToggleChildRoot(CaveDressingRootName, false);
            ToggleChildRoot(EntranceQualityRootName, false);
            ToggleChildRoot(EntranceMarkersRootName, false);
        }

        /// <summary>
        /// Ensures the pooled collider chunk hierarchy exists and can serve the requested chunk count.
        /// </summary>
        public void EnsureColliderChunkCapacity(int chunkCount)
        {
            int clampedCount = Mathf.Clamp(chunkCount, 1, MaxColliderChunkCount);
            _colliderChunkRoot = GetOrCreateRuntimeRoot(ColliderChunkRootName);

            if (_colliderChunkColliders.Length < clampedCount)
            {
                // COLD ALLOC: MeshCollider[clampedCount] - pooled child collider registry for distributed voxel physics - owner: HectonVoxelVolume
                MeshCollider[] newColliders = new MeshCollider[clampedCount];
                // COLD ALLOC: BoxCollider[clampedCount] - temporary primitive safety proxies while PhysX bakes chunk meshes - owner: HectonVoxelVolume
                BoxCollider[] newBakeProxies = new BoxCollider[clampedCount];
                // COLD ALLOC: Mesh[clampedCount] - pooled collider meshes for distributed voxel physics - owner: HectonVoxelVolume
                Mesh[] newMeshes = new Mesh[clampedCount];
                // COLD ALLOC: Mesh[clampedCount] - staged collider bake meshes for front/back voxel physics publication - owner: HectonVoxelVolume
                Mesh[] newBakeMeshes = new Mesh[clampedCount];
                for (int i = 0; i < _colliderChunkColliders.Length; i++)
                {
                    newColliders[i] = _colliderChunkColliders[i];
                    newBakeProxies[i] = i < _colliderChunkBakeProxies.Length ? _colliderChunkBakeProxies[i] : null;
                    newMeshes[i] = _colliderChunkMeshes[i];
                    newBakeMeshes[i] = _colliderChunkBakeMeshes[i];
                }

                _colliderChunkColliders = newColliders;
                _colliderChunkBakeProxies = newBakeProxies;
                _colliderChunkMeshes = newMeshes;
                _colliderChunkBakeMeshes = newBakeMeshes;
            }

            for (int i = 0; i < clampedCount; i++)
            {
                if (_colliderChunkColliders[i] == null)
                {
                    GameObject childObject = new GameObject(ColliderChunkRuntimeName);
                    childObject.layer = HectonLayerMasks.VoxelCave;
                    Transform child = childObject.transform;
                    child.SetParent(_colliderChunkRoot, false);
                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;

                    MeshCollider collider = childObject.AddComponent<MeshCollider>();
                    collider.enabled = false;
                    _colliderChunkColliders[i] = collider;
                }
                else
                {
                    _colliderChunkColliders[i].gameObject.layer = HectonLayerMasks.VoxelCave;
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
        /// Returns the pooled child MeshCollider for the requested distributed collision chunk.
        /// </summary>
        public MeshCollider GetColliderChunkCollider(int index)
        {
            if (index < 0 || index >= _colliderChunkColliders.Length)
                return null;

            return _colliderChunkColliders[index];
        }

        /// <summary>
        /// Enables a primitive floor proxy while the chunk mesh is being baked by PhysX off-thread.
        /// </summary>
        internal void ConfigureColliderChunkBakeProxy(int index, Vector3 center, Vector3 size)
        {
            if (index < 0 || index >= _colliderChunkBakeProxies.Length)
                return;

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy == null)
                return;

            proxy.center = center;
            proxy.size = new Vector3(
                math.max(0.01f, size.x),
                math.max(0.01f, size.y),
                math.max(0.01f, size.z));
            proxy.enabled = true;
        }

        /// <summary>
        /// Returns the isolated primitive bake proxy for deferred collider publication.
        /// </summary>
        internal BoxCollider GetColliderChunkBakeProxy(int index)
        {
            if (index < 0 || index >= _colliderChunkBakeProxies.Length)
                return null;

            return _colliderChunkBakeProxies[index];
        }

        /// <summary>
        /// Returns true only when the late-frame collider publication has both a target collider and staged baked mesh.
        /// </summary>
        internal bool IsDeferredColliderChunkUploadReady(int index)
        {
            if (index < 0 ||
                index >= _colliderChunkColliders.Length ||
                index >= _colliderChunkBakeMeshes.Length)
            {
                return false;
            }

            return _colliderChunkColliders[index] != null &&
                   _colliderChunkBakeMeshes[index] != null;
        }

        internal Mesh GetColliderChunkBakeMesh(int index)
        {
            if (index < 0 || index >= _colliderChunkBakeMeshes.Length)
                return null;

            return _colliderChunkBakeMeshes[index];
        }

        internal bool AssignColliderChunkBakeMesh(int index, Mesh mesh)
        {
            if (mesh == null || index < 0 || index >= _colliderChunkBakeMeshes.Length)
                return false;

            Mesh existingMesh = _colliderChunkBakeMeshes[index];
            if (existingMesh != null)
                return ReferenceEquals(existingMesh, mesh);

            _colliderChunkBakeMeshes[index] = mesh;
            return true;
        }

        /// <summary>
        /// Disables the temporary PhysX bake proxy for one collider chunk.
        /// </summary>
        internal void DisableColliderChunkBakeProxy(int index)
        {
            if (index < 0 || index >= _colliderChunkBakeProxies.Length)
                return;

            BoxCollider proxy = _colliderChunkBakeProxies[index];
            if (proxy != null)
                proxy.enabled = false;
        }

        /// <summary>
        /// Disables all temporary PhysX bake proxies owned by this volume.
        /// </summary>
        internal void DisableColliderChunkBakeProxies()
        {
            for (int i = 0; i < _colliderChunkBakeProxies.Length; i++)
            {
                BoxCollider proxy = _colliderChunkBakeProxies[i];
                if (proxy != null)
                    proxy.enabled = false;
            }
        }

        /// <summary>
        /// Returns a reusable mesh instance for the requested collider chunk, creating it on first use only.
        /// </summary>
        public Mesh GetOrCreateColliderChunkMesh(int index)
        {
            if (index < 0 || index >= _colliderChunkMeshes.Length)
                return null;

            Mesh mesh = _colliderChunkMeshes[index];
            if (mesh != null)
                return mesh;

            mesh = global::HectonVoxelEngine.AcquireVoxelPhysicsBakeMesh();
            if (mesh == null)
                return null;

            _colliderChunkMeshes[index] = mesh;
            return mesh;
        }

        /// <summary>
        /// Returns a reusable staging mesh for the requested collider chunk.
        /// The staged mesh is never the currently published collider mesh.
        /// </summary>
        internal Mesh GetOrCreateColliderChunkBakeMesh(int index)
        {
            if (index < 0 || index >= _colliderChunkBakeMeshes.Length)
                return null;

            Mesh mesh = _colliderChunkBakeMeshes[index];
            if (mesh != null)
                return mesh;

            mesh = global::HectonVoxelEngine.AcquireVoxelPhysicsBakeMesh();
            if (mesh == null)
                return null;

            _colliderChunkBakeMeshes[index] = mesh;
            return mesh;
        }

        /// <summary>
        /// Queues a staged collider mesh upload for the requested chunk. The actual sharedMesh write is late-frame throttled.
        /// </summary>
        internal bool PublishColliderChunkMesh(int index)
        {
            if (index < 0 ||
                index >= _colliderChunkColliders.Length ||
                index >= _colliderChunkMeshes.Length ||
                index >= _colliderChunkBakeMeshes.Length)
            {
                return false;
            }

            MeshCollider collider = _colliderChunkColliders[index];
            Mesh stagedMesh = _colliderChunkBakeMeshes[index];
            if (collider == null || stagedMesh == null)
                return false;

            bool enqueued = global::HectonVoxelEngine.EnqueueDeferredVoxelColliderUpload(this, index);
            if (!enqueued)
                DisableColliderChunkBakeProxy(index);

            return enqueued;
        }

        /// <summary>
        /// Performs the deferred staged collider mesh upload and swaps the previous live mesh into the bake slot.
        /// </summary>
        internal bool CommitDeferredColliderChunkUpload(int index)
        {
            if (index < 0 ||
                index >= _colliderChunkColliders.Length ||
                index >= _colliderChunkMeshes.Length ||
                index >= _colliderChunkBakeMeshes.Length)
            {
                return false;
            }

            MeshCollider collider = _colliderChunkColliders[index];
            Mesh stagedMesh = _colliderChunkBakeMeshes[index];
            if (collider == null || stagedMesh == null)
            {
                DisableColliderChunkBakeProxy(index);
                return false;
            }

            Mesh previousLiveMesh = _colliderChunkMeshes[index];
            collider.gameObject.SetActive(true);
            collider.enabled = false;
            collider.sharedMesh = stagedMesh;
            _colliderChunkMeshes[index] = stagedMesh;
            _colliderChunkBakeMeshes[index] = previousLiveMesh;
            collider.enabled = true;
            DisableColliderChunkBakeProxy(index);
            RefreshBakePresentation();
            return true;
        }

        /// <summary>
        /// Clears staged collider bake meshes without touching the currently published live collider meshes.
        /// </summary>
        internal void ClearColliderChunkBakeMeshes()
        {
            for (int i = 0; i < _colliderChunkBakeMeshes.Length; i++)
            {
                Mesh mesh = _colliderChunkBakeMeshes[i];
                if (mesh != null)
                    mesh.Clear(false);
            }
        }

        /// <summary>
        /// Releases ownership of a staged collider bake mesh after its PhysX bake has been handed to deferred teardown.
        /// </summary>
        /// <param name="index">Collider chunk index.</param>
        internal void DetachColliderChunkBakeMesh(int index)
        {
            if (index < 0 || index >= _colliderChunkBakeMeshes.Length)
                return;

            if (index < _colliderChunkColliders.Length)
            {
                MeshCollider collider = _colliderChunkColliders[index];
                if (collider != null)
                {
                    collider.enabled = false;
                    collider.sharedMesh = null;
                }
            }

            _colliderChunkBakeMeshes[index] = null;
        }

        /// <summary>
        /// Clears all pooled collider chunks. When destroyMeshes is true the mesh instances are destroyed permanently.
        /// </summary>
        public void ResetColliderChunks(bool destroyMeshes)
        {
            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider != null)
                {
                    collider.sharedMesh = null;
                    collider.enabled = false;
                    DisableColliderChunkBakeProxy(i);
                    if (collider.gameObject.activeSelf)
                        collider.gameObject.SetActive(false);
                }

                Mesh mesh = i < _colliderChunkMeshes.Length ? _colliderChunkMeshes[i] : null;
                Mesh bakeMesh = i < _colliderChunkBakeMeshes.Length ? _colliderChunkBakeMeshes[i] : null;
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
            int clampedActive = Mathf.Clamp(activeCount, 0, _colliderChunkColliders.Length);
            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider == null)
                    continue;

                bool shouldBeActive = i < clampedActive;
                if (collider.gameObject.activeSelf != shouldBeActive)
                    collider.gameObject.SetActive(shouldBeActive);
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
            _generationAbsoluteUniversePosition = worldCenter + absoluteUniverseOffset;
            preset = cavePreset;
            _gridDimension = gridDimension;
            _voxelSize = voxelSize;
            _lodLevel = Mathf.Max(0, lodLevel);
            _caveParams = caveParams;
            _buildCollider = buildCollider;

            // COLD ALLOC: CaveNode[nodes.Length] - runtime room graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _nodes = new CaveNode[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                CaveNode snapshot = nodes[i];
                snapshot.position += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _nodes[i] = snapshot;
            }

            // COLD ALLOC: CaveTunnel[tunnels.Length] - runtime tunnel graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _tunnels = new CaveTunnel[tunnels.Length];
            for (int i = 0; i < tunnels.Length; i++)
            {
                CaveTunnel snapshot = tunnels[i];
                snapshot.pointA += (Unity.Mathematics.float3)absoluteUniverseOffset;
                snapshot.pointB += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _tunnels[i] = snapshot;
            }

            // COLD ALLOC: CaveEntrance[entrances.Length] - runtime entrance snapshot for terrain-hole/skirt rebuilds - owner: HectonVoxelVolume
            _entrances = new CaveEntrance[entrances.Length];
            for (int i = 0; i < entrances.Length; i++)
            {
                CaveEntrance snapshot = entrances[i];
                snapshot.surfacePosition += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _entrances[i] = snapshot;
            }

            // COLD ALLOC: CaveStructure[structures.Length] - runtime structure snapshot for crater rebuilds - owner: HectonVoxelVolume
            _structures = new CaveStructure[structures.Length];
            for (int i = 0; i < structures.Length; i++)
            {
                CaveStructure snapshot = structures[i];
                snapshot.position += (Unity.Mathematics.float3)absoluteUniverseOffset;
                snapshot.pointB += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _structures[i] = snapshot;
            }

            if (_craterStamps.Length != MaxCraterStampCount)
            {
                // COLD ALLOC: VoxelCraterStamp[MaxCraterStampCount] - bounded runtime crater registry - owner: HectonVoxelVolume
                _craterStamps = new VoxelCraterStamp[MaxCraterStampCount];
            }

            if (_resourceCraterClusterStamps.Length != MaxCraterStampCount)
            {
                // COLD ALLOC: VoxelCraterStamp[MaxCraterStampCount] - persistent resource-crater cluster tracker - owner: HectonVoxelVolume
                _resourceCraterClusterStamps = new VoxelCraterStamp[MaxCraterStampCount];
            }

            if (_collapseImpulseColliders.Length != CollapseImpulseColliderCapacity)
            {
                // COLD ALLOC: Collider[CollapseImpulseColliderCapacity] - collapse impulse overlap buffer - owner: HectonVoxelVolume
                _collapseImpulseColliders = new Collider[CollapseImpulseColliderCapacity];
            }

            if (_collapseImpulseBodies.Length != CollapseImpulseBodyCapacity)
            {
                // COLD ALLOC: Rigidbody[CollapseImpulseBodyCapacity] - collapse impulse dedupe buffer - owner: HectonVoxelVolume
                _collapseImpulseBodies = new Rigidbody[CollapseImpulseBodyCapacity];
            }

            if (_terrainHoleHandles.Length != MaxTerrainHoleHandleCount)
            {
                // COLD ALLOC: int[MaxTerrainHoleHandleCount] - stable terrain-hole handle registry for cave entrance lifecycle - owner: HectonVoxelVolume
                _terrainHoleHandles = new int[MaxTerrainHoleHandleCount];
            }

            _craterStampCount = 0;
            _resourceCraterClusterCount = 0;
            _lastCollapseClusterValid = false;
            _lastCollapseAbsoluteCenter = Vector3.zero;
            _terrainHoleHandleCount = 0;
            _runtimeDataReady = true;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _runtimeStamp++;
            CacheRuntimeComponents();
            SetBakeState(VoxelBakeState.Complete);
            RegisterPublishedVolume(this);
            _deltaProcessor?.RegisterVolume(this);
        }

        /// <summary>
        /// Publishes a compact encoded SDF snapshot for diegetic PDA sonar map rendering.
        /// </summary>
        internal void PublishSonarSdfSnapshot(
            Vector3Int gridDimensions,
            Vector3 volumeOrigin,
            Vector3 voxelCellSize,
            NativeArray<float> smoothDensityField)
        {
            int totalPointCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;
            if (totalPointCount <= 0 ||
                !smoothDensityField.IsCreated ||
                smoothDensityField.Length < totalPointCount)
            {
                ClearPublishedSonarSdf();
                return;
            }

            EnsurePublishedSonarCapacity(totalPointCount);
            _publishedSonarGridDimensions = gridDimensions;
            _publishedSonarOrigin = volumeOrigin;
            _publishedSonarCellSize = voxelCellSize;
            _publishedSonarSdfRange = Mathf.Max(
                0.25f,
                Mathf.Max(voxelCellSize.x, Mathf.Max(voxelCellSize.y, voxelCellSize.z)) * 8f);

            float inverseRange = 1f / _publishedSonarSdfRange;
            for (int i = 0; i < totalPointCount; i++)
            {
                float normalized = Mathf.Clamp(smoothDensityField[i] * inverseRange, -1f, 1f);
                float encoded = (normalized * 0.5f + 0.5f) * 255f;
                _publishedSonarSdf[i] = (byte)Mathf.Clamp((int)(encoded + 0.5f), 0, 255);
                _publishedSonarAudioMaterialIds[i] = DefaultPublishedSonarAudioMaterialId;
            }

            _publishedSonarVersion++;
        }

        internal bool TryGetPublishedSonarSdfPayload(
            out NativeArray<byte> encodedSdf,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version)
        {
            encodedSdf = _publishedSonarSdf;
            gridDimensions = _publishedSonarGridDimensions;
            volumeOrigin = _publishedSonarOrigin;
            voxelCellSize = _publishedSonarCellSize;
            sdfRange = _publishedSonarSdfRange;
            version = _publishedSonarVersion;
            return _runtimeDataReady &&
                   _publishedSonarSdf.IsCreated &&
                   gridDimensions.x > 0 &&
                   gridDimensions.y > 0 &&
                   gridDimensions.z > 0 &&
                   _publishedSonarSdf.Length == gridDimensions.x * gridDimensions.y * gridDimensions.z &&
                   sdfRange > 0f;
        }

        internal bool TryGetPublishedSonarSdfPayload(
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> audioMaterialIds,
            out Vector3Int gridDimensions,
            out Vector3 volumeOrigin,
            out Vector3 voxelCellSize,
            out float sdfRange,
            out int version)
        {
            bool resolved = TryGetPublishedSonarSdfPayload(
                out encodedSdf,
                out gridDimensions,
                out volumeOrigin,
                out voxelCellSize,
                out sdfRange,
                out version);
            audioMaterialIds = _publishedSonarAudioMaterialIds;
            return resolved &&
                   _publishedSonarAudioMaterialIds.IsCreated &&
                   _publishedSonarAudioMaterialIds.Length == encodedSdf.Length;
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

            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);
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

            Vector3 absoluteCenter = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeCenter);
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

            SetBakeState(VoxelBakeState.Pending);
            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);
            float resolvedRadius = ResolveOrganicRootMoundWeldRadius(pos, radius);
            _deltaProcessor.ApplyImmediateAbsoluteWeld(this, absolutePosition, resolvedRadius, strength, DefaultDeltaMaterialId);
        }

        private float ResolveOrganicRootMoundWeldRadius(Vector3 runtimePosition, float authoredRadius)
        {
            float safeRadius = Mathf.Max(0.01f, authoredRadius);
            if (!TrySampleDensity(runtimePosition, out float densityAtRoot))
                return safeRadius;

            if (densityAtRoot >= 0f)
                return safeRadius;

            float distanceToSeabed = Mathf.Abs(densityAtRoot);
            for (int i = 1; i <= OrganicRootMoundSeabedProbeSteps; i++)
            {
                float probeDistance = i * OrganicRootMoundSeabedProbeStepMeters;
                Vector3 probePosition = runtimePosition + Vector3.down * probeDistance;
                if (!TrySampleDensity(probePosition, out float probeDensity))
                    continue;

                if (probeDensity >= 0f)
                {
                    distanceToSeabed = Mathf.Min(distanceToSeabed, probeDistance);
                    break;
                }
            }

            return Mathf.Max(safeRadius, distanceToSeabed + OrganicRootMoundMinimumOverlapMeters);
        }

        /// <summary>
        /// Executes the authoritative persistent resource-depletion crater pass.
        /// Kept explicit so tombstoned geology callers route through the volume owner instead of the delta processor.
        /// </summary>
        public void ApplyPersistentResourceCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return;

            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);
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

            Vector3 absoluteCenter = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeCenter);
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

            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);
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
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
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

                Vector3 absoluteStart = HectonFloatingOrigin.ToAbsoluteUniversePosition(start);
                Vector3 absoluteEnd = HectonFloatingOrigin.ToAbsoluteUniversePosition(end);
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
            if (!_runtimeDataReady || _bakeState != VoxelBakeState.Complete || _gridDimension <= 0 || _voxelSize <= 0f)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(absoluteStart);
            Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(absoluteEnd);
            Vector3 runtimeEpicenter = HectonFloatingOrigin.ToRuntimePosition(absoluteEpicenter);
            Vector3 line = runtimeEnd - runtimeStart;
            float lineLengthSq = line.sqrMagnitude;
            if (lineLengthSq <= 0.000001f)
                return false;

            Transform cachedTransform = transform;
            float lineLength = ApproxMagnitude(line);
            Vector3 forward = line / Mathf.Max(lineLength, 0.0001f);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float rightLengthSq = right.sqrMagnitude;
            if (rightLengthSq <= 0.0001f)
                right = Vector3.right;
            else
                right = NormalizeApprox(right, Vector3.right);

            float clampedDepth = Mathf.Max(_voxelSize, trenchDepth);
            float clampedSlope = Mathf.Max(0.05f, trenchSlope);
            float influenceRadius = clampedDepth / clampedSlope;
            float longitudinalStep = Mathf.Max(_voxelSize, sampleSpacing);
            float lateralStep = Mathf.Max(_voxelSize * 0.85f, longitudinalStep * 0.5f);
            int longitudinalCount = Mathf.Clamp(Mathf.CeilToInt(lineLength / longitudinalStep) + 1, 2, 64);
            float epicenterFadeDistance = Mathf.Max(_voxelSize, lineLength * 0.5f + influenceRadius);
            float epicenterFadeDistanceSq = epicenterFadeDistance * epicenterFadeDistance;
            Vector3 runtimeStep = line / (longitudinalCount - 1);
            Vector3 runtimeCenter = runtimeStart;

            for (int sampleIndex = 0; sampleIndex < longitudinalCount; sampleIndex++)
            {
                for (float lateral = -influenceRadius; lateral <= influenceRadius + 0.001f; lateral += lateralStep)
                {
                    Vector3 runtimeColumn = runtimeCenter + right * lateral;
                    float epicenterDistanceSq = (runtimeColumn - runtimeEpicenter).sqrMagnitude;
                    float epicenterDepth = clampedDepth * (1f - math.saturate(epicenterDistanceSq / epicenterFadeDistanceSq));
                    float cutDepth = Mathf.Max(0f, epicenterDepth - Mathf.Abs(lateral) * clampedSlope);
                    if (cutDepth <= 0.0001f)
                        continue;

                    Vector3 localColumn = cachedTransform.InverseTransformPoint(runtimeColumn);
                    if (localColumn.x < localBounds.min.x ||
                        localColumn.x > localBounds.max.x ||
                        localColumn.z < localBounds.min.z ||
                        localColumn.z > localBounds.max.z)
                    {
                        continue;
                    }

                    float craterRadius = Mathf.Max(_voxelSize * 0.85f, cutDepth * 0.55f);
                    if (!TryResolveTopSolidAnchor(
                            cachedTransform,
                            localBounds,
                            localColumn.x,
                            localColumn.z,
                            cutDepth,
                            out Vector3 localAnchor))
                    {
                        continue;
                    }

                    CarveCrater(cachedTransform.TransformPoint(localAnchor), craterRadius);
                    displacedVolumeCubicMeters += EstimateSeismicCraterDisplacedVolume(craterRadius, cutDepth);
                    appliedStampCount++;
                }

                runtimeCenter += runtimeStep;
            }

            return appliedStampCount > 0;
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
            float3 tMax = new float3(
                ResolveBoundaryDistance(localBounds.min.x, start.x, dir.x, voxel.x, step.x, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.y, start.y, dir.y, voxel.y, step.y, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.z, start.z, dir.z, voxel.z, step.z, _voxelSize));
            float3 tDelta = new float3(
                ResolveDeltaDistance(dir.x, _voxelSize),
                ResolveDeltaDistance(dir.y, _voxelSize),
                ResolveDeltaDistance(dir.z, _voxelSize));

            float travel = 0f;
            float maxTravel = math.max(_voxelSize, math.min(maxDistance, _voxelSize * MaxPlasmaCutSteps));
            float remainingPower = clampedPower;
            float stampRadius = math.max(_voxelSize * 0.6f, _voxelSize * math.lerp(0.75f, 1.1f, clampedPower));
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
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
                Vector3 absoluteCenter = worldCenter + committedOffset;
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
            float3 tMax = new float3(
                ResolveBoundaryDistance(localBounds.min.x, start.x, dir.x, voxel.x, step.x, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.y, start.y, dir.y, voxel.y, step.y, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.z, start.z, dir.z, voxel.z, step.z, _voxelSize));
            float3 tDelta = new float3(
                ResolveDeltaDistance(dir.x, _voxelSize),
                ResolveDeltaDistance(dir.y, _voxelSize),
                ResolveDeltaDistance(dir.z, _voxelSize));

            float travel = 0f;
            float maxTravel = math.max(_voxelSize, math.min(maxDistance, _voxelSize * MaxPlasmaCutSteps));
            float remainingPower = clampedPower;
            float stampRadius = math.max(_voxelSize * 0.55f, _voxelSize * math.lerp(0.65f, 1f, clampedPower));
            float stampStrength = math.max(_voxelSize, stampRadius * 0.45f);
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
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
                Vector3 absoluteCenter = worldCenter + committedOffset;
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
                Vector3 absoluteSample = absoluteStart + direction * longitudinalOffset + jitter;
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

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                if (_terrainHoleHandles[i] == holeHandle)
                    return;
            }

            if (_terrainHoleHandleCount >= MaxTerrainHoleHandleCount)
                return;

            _terrainHoleHandles[_terrainHoleHandleCount++] = holeHandle;
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

        private void CacheRuntimeComponents()
        {
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
            CacheRuntimeComponents();

            bool visualsStable = _bakeState == VoxelBakeState.Complete;
            bool collisionAllowed = _bakeState != VoxelBakeState.Idle;
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

            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider == null)
                    continue;

                collider.enabled = collisionAllowed && collider.sharedMesh != null && collider.gameObject.activeSelf;
            }
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
                    HectonVoxelEngine engine = _engine != null ? _engine : HectonVoxelEngine.ActiveRuntimeInstance;
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
            catch (Exception ex)
            {
                SetBakeState(VoxelBakeState.Pending);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(ex, this);
#endif
            }
            finally
            {
                _rebuildRunning = false;
            }

            if (rescheduleNextFrame && MatchesRuntimeStamp(_runtimeStamp))
                _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
        }

        private void UnregisterTerrainHoles()
        {
            if (_terrainHoleHandleCount <= 0)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
            {
                _terrainHoleHandleCount = 0;
                return;
            }

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                int holeHandle = _terrainHoleHandles[i];
                if (holeHandle <= 0)
                    continue;

                vegetationBridge.UnregisterTerrainHole(holeHandle);
                _terrainHoleHandles[i] = 0;
            }

            _terrainHoleHandleCount = 0;
        }

        private void OnEnable()
        {
            VoxelVolumeLeakSentinel.RegisterVolume(this);
        }

        private void OnDestroy()
        {
            VoxelVolumeLeakSentinel.FinalizeVolume(this);
            UnregisterPublishedVolume(this);
            _deltaProcessor?.UnregisterVolume(this);
            VoxelDynamicNavGridRuntime.UnregisterVolume(this);
            UnregisterTerrainHoles();
            ResetColliderChunks(true);
            ClearPublishedSonarSdf();
            _runtimeStamp++;
        }

        private void EnsurePublishedSonarCapacity(int totalPointCount)
        {
            if (_publishedSonarSdf.IsCreated &&
                _publishedSonarAudioMaterialIds.IsCreated &&
                _publishedSonarSdf.Length == totalPointCount &&
                _publishedSonarAudioMaterialIds.Length == totalPointCount)
                return;

            if (_publishedSonarSdf.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_publishedSonarSdf);
                _publishedSonarSdf.Dispose(default);
            }

            if (_publishedSonarAudioMaterialIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_publishedSonarAudioMaterialIds);
                _publishedSonarAudioMaterialIds.Dispose(default);
            }

            _publishedSonarSdf = new NativeArray<byte>(
                totalPointCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[totalPointCount] - published PDA sonar SDF snapshot - owner: HectonVoxelVolume
            NativeMemorySentinel.RegisterNativeArray(
                _publishedSonarSdf,
                NativeMemoryOwner,
                nameof(_publishedSonarSdf),
                NativeAllocationLifetime.Scene);
            _publishedSonarAudioMaterialIds = new NativeArray<byte>(
                totalPointCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[totalPointCount] - published sonar audio material atlas - owner: HectonVoxelVolume
            NativeMemorySentinel.RegisterNativeArray(
                _publishedSonarAudioMaterialIds,
                NativeMemoryOwner,
                nameof(_publishedSonarAudioMaterialIds),
                NativeAllocationLifetime.Scene);
        }

        private void ClearPublishedSonarSdf()
        {
            if (_publishedSonarSdf.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_publishedSonarSdf);
                _publishedSonarSdf.Dispose(default);
            }

            if (_publishedSonarAudioMaterialIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_publishedSonarAudioMaterialIds);
                _publishedSonarAudioMaterialIds.Dispose(default);
            }

            _publishedSonarSdf = default;
            _publishedSonarAudioMaterialIds = default;
            _publishedSonarGridDimensions = default;
            _publishedSonarOrigin = Vector3.zero;
            _publishedSonarCellSize = Vector3.zero;
            _publishedSonarSdfRange = 0f;
            _publishedSonarVersion = 0;
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

        private static float ResolveBoundaryDistance(float min, float start, float direction, int voxelIndex, int step, float voxelSize)
        {
            if (step == 0 || Mathf.Abs(direction) < 0.0001f)
                return float.PositiveInfinity;

            float nextBoundary = min + ((step > 0 ? voxelIndex + 1 : voxelIndex) * voxelSize);
            return (nextBoundary - start) / direction;
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

        private void TryTriggerResourceCraterClusterCollapse(Vector3 absolutePosition, float radius)
        {
            if (_resourceCraterClusterStamps.Length != MaxCraterStampCount ||
                _collapseImpulseColliders.Length != CollapseImpulseColliderCapacity ||
                _collapseImpulseBodies.Length != CollapseImpulseBodyCapacity)
            {
                return;
            }

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            if (_resourceCraterClusterCount >= MaxCraterStampCount)
            {
                for (int i = 1; i < _resourceCraterClusterCount; i++)
                    _resourceCraterClusterStamps[i - 1] = _resourceCraterClusterStamps[i];

                _resourceCraterClusterCount = MaxCraterStampCount - 1;
            }

            _resourceCraterClusterStamps[_resourceCraterClusterCount++] = new VoxelCraterStamp
            {
                position = absolutePosition,
                radius = clampedRadius,
                blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f)
            };

            if (!TryResolveResourceCraterCluster(
                    absolutePosition,
                    out Vector3 collapseAbsoluteCenter,
                    out Vector3 collapseHalfExtents,
                    out int clusterCount))
            {
                return;
            }

            if (_lastCollapseClusterValid &&
                (_lastCollapseAbsoluteCenter - collapseAbsoluteCenter).sqrMagnitude <=
                ResourceCraterClusterRadiusMeters * ResourceCraterClusterRadiusMeters)
            {
                return;
            }

            _lastCollapseClusterValid = true;
            _lastCollapseAbsoluteCenter = collapseAbsoluteCenter;
            ExecuteResourceCraterClusterCollapse(collapseAbsoluteCenter, collapseHalfExtents, clusterCount);
        }

        private bool TryResolveResourceCraterCluster(
            Vector3 absolutePosition,
            out Vector3 collapseAbsoluteCenter,
            out Vector3 collapseHalfExtents,
            out int clusterCount)
        {
            collapseAbsoluteCenter = absolutePosition;
            collapseHalfExtents = default;
            clusterCount = 0;
            float clusterRadiusSq = ResourceCraterClusterRadiusMeters * ResourceCraterClusterRadiusMeters;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            float largestRadius = 0f;

            for (int i = 0; i < _resourceCraterClusterCount; i++)
            {
                VoxelCraterStamp stamp = _resourceCraterClusterStamps[i];
                if ((stamp.position - absolutePosition).sqrMagnitude > clusterRadiusSq)
                    continue;

                clusterCount++;
                largestRadius = Mathf.Max(largestRadius, stamp.radius);
                Vector3 radiusVector = Vector3.one * stamp.radius;
                min = Vector3.Min(min, stamp.position - radiusVector);
                max = Vector3.Max(max, stamp.position + radiusVector);
            }

            if (clusterCount <= ResourceCraterCollapseThreshold)
                return false;

            collapseAbsoluteCenter = (min + max) * 0.5f;
            Vector3 span = max - min;
            collapseHalfExtents = new Vector3(
                Mathf.Max(6f, span.x * 0.5f + CollapseBoxHorizontalPaddingMeters),
                Mathf.Max(3f, largestRadius),
                Mathf.Max(6f, span.z * 0.5f + CollapseBoxHorizontalPaddingMeters));
            return true;
        }

        private void ExecuteResourceCraterClusterCollapse(Vector3 absoluteCenter, Vector3 halfExtents, int clusterCount)
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

            Vector3 trenchDirection = ResolveCollapseTrenchDirection(absoluteCenter);
            float halfTrenchLength = Mathf.Max(2f, impulseRadius * 0.5f);
            SeismicShockwaveEvent shockwaveEvent = new SeismicShockwaveEvent(
                runtimeCenter,
                impulseRadius,
                impulseMagnitude,
                clusterCount,
                absoluteCenter - trenchDirection * halfTrenchLength,
                absoluteCenter + trenchDirection * halfTrenchLength);
            RandomEventEvents.RaiseSeismicShockwave(in shockwaveEvent);
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

        private void ApplyCollapseImpulse(Vector3 runtimeCenter, Vector3 halfExtents, float impulseRadius, float impulseMagnitude)
        {
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                runtimeCenter,
                halfExtents + Vector3.one * 2f,
                _collapseImpulseColliders,
                Quaternion.identity,
                _CollapseImpulseLayerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            int bodyCount = 0;
            int colliderLimit = Mathf.Min(hitCount, _collapseImpulseColliders.Length);
            for (int i = 0; i < colliderLimit; i++)
            {
                Collider hitCollider = _collapseImpulseColliders[i];
                _collapseImpulseColliders[i] = null;
                if (hitCollider == null)
                    continue;

                Rigidbody body = hitCollider.attachedRigidbody;
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
                PhysicsForceRouter.QueueForce(body, force, ForceMode.Impulse);
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

        private bool AppendCraterStamp(Vector3 absolutePosition, float radius, bool queueRebuild)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return false;

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            float blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f);

            for (int i = 0; i < _craterStampCount; i++)
            {
                VoxelCraterStamp existing = _craterStamps[i];
                float mergeDistance = existing.radius + clampedRadius * 0.35f;
                if ((existing.position - absolutePosition).sqrMagnitude > mergeDistance * mergeDistance)
                    continue;

                existing.position = (existing.position + absolutePosition) * 0.5f;
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

            _craterStamps[_craterStampCount++] = new VoxelCraterStamp
            {
                position = absolutePosition,
                radius = clampedRadius,
                blendRadius = blendRadius
            };

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

            for (float sampleY = startY; sampleY >= minY; sampleY -= sampleStep)
            {
                Vector3 worldSample = cachedTransform.TransformPoint(new Vector3(localX, sampleY, localZ));
                if (!TrySampleDensity(worldSample, out float density, out _))
                    continue;

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

        private bool TryResolveTopSolidAnchor(
            Transform cachedTransform,
            Bounds localBounds,
            float localX,
            float localZ,
            float cutDepth,
            out Vector3 localAnchor)
        {
            localAnchor = default;
            float sampleStep = Mathf.Max(_voxelSize * 0.75f, 0.5f);
            float startY = localBounds.max.y - _voxelSize * 0.5f;
            float minY = localBounds.min.y + _voxelSize * 0.5f;

            for (float sampleY = startY; sampleY >= minY; sampleY -= sampleStep)
            {
                Vector3 worldSample = cachedTransform.TransformPoint(new Vector3(localX, sampleY, localZ));
                if (!TrySampleDensity(worldSample, out float density, out _))
                    continue;

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

        private bool TryResolveNearestSolidDistance(Vector3 worldPosition, Bounds localBounds, out float distanceMeters)
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
                    TrySampleDensity(cachedTransform.TransformPoint(upSample), out float upDensity) &&
                    upDensity > 0f)
                {
                    distanceMeters = offset;
                    return true;
                }

                if (offset <= 0f)
                    continue;

                Vector3 downSample = localPoint - new Vector3(0f, offset, 0f);
                if (localBounds.Contains(downSample) &&
                    TrySampleDensity(cachedTransform.TransformPoint(downSample), out float downDensity) &&
                    downDensity > 0f)
                {
                    distanceMeters = offset;
                    return true;
                }
            }

            return false;
        }

        private bool HasSolidDensityPath(Vector3 startWorldPosition, Vector3 endWorldPosition)
        {
            Vector3 pathDelta = endWorldPosition - startWorldPosition;
            float dominantAxisLength = math.cmax(math.abs(new float3(pathDelta.x, pathDelta.y, pathDelta.z)));
            float sampleStep = math.max(_voxelSize * 0.75f, 0.5f);
            int sampleCount = math.max(2, (int)math.ceil(dominantAxisLength / sampleStep));
            Vector3 sampleDelta = pathDelta / sampleCount;
            Vector3 samplePosition = startWorldPosition;

            for (int i = 0; i <= sampleCount; i++)
            {
                if (!TrySampleDensity(samplePosition, out float density) || density <= 0f)
                    return false;

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
    /// Fixed-slot voxel lifecycle sentry. Tracks volumes that were explicitly
    /// destroyed, then reports if Unity never finalizes them within 300 frames.
    /// </summary>
    internal static class VoxelVolumeLeakSentinel
    {
        private const int MaxTrackedVoxelVolumes = 512;
        private const int FinalizeDeadlineFrames = 300;
        private const byte StateFree = 0;
        private const byte StateAlive = 1;
        private const byte StateDestroyPending = 2;
        private const byte StateReported = 3;
        private static readonly uint _CriticalMemoryLeakHash = unchecked((uint)Hecton.Localization.LocHash.Compute("CRITICAL_MEMORY_LEAK"));
        private static readonly uint _VoxelVolumeLeakContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelVolumeLeakSentinel"));

        // COLD ALLOC: HectonVoxelVolume[512] - fixed voxel lifecycle sentry slots - owner: VoxelVolumeLeakSentinel
        private static readonly Hecton8.Caves.HectonVoxelVolume[] s_volumes = new Hecton8.Caves.HectonVoxelVolume[MaxTrackedVoxelVolumes];
        // COLD ALLOC: int[512] - destroy-request frame stamps - owner: VoxelVolumeLeakSentinel
        private static readonly int[] s_destroyRequestedFrame = new int[MaxTrackedVoxelVolumes];
        // COLD ALLOC: byte[512] - slot state flags - owner: VoxelVolumeLeakSentinel
        private static readonly byte[] s_state = new byte[MaxTrackedVoxelVolumes];
        private static readonly LeakSentinelLateFrameDriver s_driver = new LeakSentinelLateFrameDriver();
        private static bool s_driverRegistered;
        private static int s_pendingDestroyCount;

        internal static void RegisterVolume(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            int slot = FindSlot(volume);
            if (slot < 0)
                slot = FindFreeSlot();

            if (slot < 0)
                return;

            if (s_state[slot] == StateDestroyPending)
                s_pendingDestroyCount--;

            s_volumes[slot] = volume;
            s_destroyRequestedFrame[slot] = 0;
            s_state[slot] = StateAlive;
        }

        internal static void MarkDestroyRequested(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            int slot = FindSlot(volume);
            if (slot < 0)
                slot = FindFreeSlot();

            if (slot < 0)
                return;

            if (s_state[slot] != StateDestroyPending)
                s_pendingDestroyCount++;

            s_volumes[slot] = volume;
            s_destroyRequestedFrame[slot] = UnityEngine.Time.frameCount;
            s_state[slot] = StateDestroyPending;
            EnsureDriverRegistered();
        }

        internal static void MarkReleasedToPool(Hecton8.Caves.HectonVoxelVolume volume)
        {
            FinalizeVolume(volume);
        }

        internal static void FinalizeVolume(Hecton8.Caves.HectonVoxelVolume volume)
        {
            int slot = FindSlot(volume);
            if (slot < 0)
                return;

            if (s_state[slot] == StateDestroyPending)
                s_pendingDestroyCount--;

            s_volumes[slot] = null;
            s_destroyRequestedFrame[slot] = 0;
            s_state[slot] = StateFree;
            TryUnregisterDriver();
        }

        private static void Pump()
        {
            if (s_pendingDestroyCount <= 0)
            {
                TryUnregisterDriver();
                return;
            }

            int frame = UnityEngine.Time.frameCount;
            for (int i = 0; i < MaxTrackedVoxelVolumes; i++)
            {
                if (s_state[i] != StateDestroyPending)
                    continue;

                Hecton8.Caves.HectonVoxelVolume volume = s_volumes[i];
                if (volume == null)
                {
                    s_state[i] = StateFree;
                    s_destroyRequestedFrame[i] = 0;
                    s_pendingDestroyCount--;
                    continue;
                }

                if (frame - s_destroyRequestedFrame[i] < FinalizeDeadlineFrames)
                    continue;

                s_state[i] = StateReported;
                s_pendingDestroyCount--;
                Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                    _CriticalMemoryLeakHash,
                    _VoxelVolumeLeakContextHash,
                    unchecked((uint)i));
            }

            TryUnregisterDriver();
        }

        private static int FindSlot(Hecton8.Caves.HectonVoxelVolume volume)
        {
            if (volume == null)
                return -1;

            for (int i = 0; i < MaxTrackedVoxelVolumes; i++)
            {
                if (ReferenceEquals(s_volumes[i], volume))
                    return i;
            }

            return -1;
        }

        private static int FindFreeSlot()
        {
            for (int i = 0; i < MaxTrackedVoxelVolumes; i++)
            {
                if (s_state[i] == StateFree)
                    return i;
            }

            return -1;
        }

        private static void EnsureDriverRegistered()
        {
            if (s_driverRegistered ||
                !UnityEngine.Application.isPlaying ||
                Hecton8.Core.GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            Hecton8.Core.GlobalRegistry.RegisterLateFrameTickable(s_driver, Hecton8.Core.PriorityLayer.Environment);
            s_driverRegistered = true;
        }

        private static void TryUnregisterDriver()
        {
            if (!s_driverRegistered ||
                s_pendingDestroyCount > 0 ||
                Hecton8.Core.GlobalRegistry.Dispatcher == null)
            {
                return;
            }

            Hecton8.Core.GlobalRegistry.UnregisterLateFrameTickable(s_driver, Hecton8.Core.PriorityLayer.Environment);
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
