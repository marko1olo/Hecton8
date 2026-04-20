using System;
using System.Collections.Generic;
using Hecton8.Core;
using MapMagic.Products;
using MapMagic.Terrains;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.World
{
    /// <summary>
    /// Streams vegetation data from MapMagic tiles into virtual 100x100 meter chunks
    /// and keeps only the player-near residency ring bound to the indirect renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonMapMagicVegetationBridge : MonoBehaviour, ITickable, ISlowTickable
    {
        private const string SandLayerName = "L_Sand";
        private const string GreenSandLayerName = "L_sandGreen";
        private const string RockLayerName = "L_Rocks";
        private const float DefaultWaterLevel = 4900f;
        private const float DefaultKelpMinHeight = 4600f;
        private const float DefaultVirtualChunkSize = 100f;
        private const float CameraResolveRetryInterval = 1f;
        private const float CacheValidationInterval = 0.5f;
        private const int CacheValidationTileBudget = 2;
        private const int DefaultJobBatchSize = 32;
        private const int InitialTileCapacity = 32;
        private const int InitialChunkCapacity = 256;
        private const int InitialChunkArrayCapacity = 64;
        private const int ChunkPoolBytesPerInstance = 128;
        private const int MinimumNativePoolBudgetMb = 64;
        private const int DensityGridResolution = 8;
        private const int DensityGridCellCount = DensityGridResolution * DensityGridResolution;
        private const float DensityQuerySeedScale = 2f;
        private const int VegetationAudioProbeCount = 5;
        private const int DensityTypeMaskGrass = 1 << 0;
        private const int DensityTypeMaskKelp = 1 << 1;
        private const int DensityTypeMaskSargassum = 1 << 2;
        private const int DensityTypeMaskAll = DensityTypeMaskGrass | DensityTypeMaskKelp | DensityTypeMaskSargassum;
        private static readonly int _ShaderVegetationAudioDensityId = Shader.PropertyToID("_HectonVegetationAudioDensity");
        private static readonly int _ShaderVegetationAudioAcousticTypeId = Shader.PropertyToID("_HectonVegetationAudioAcousticType");

        /// <summary>
        /// Shared salt for the primary Voronoi feature points that define the dominant Langmuir wall lattice.
        /// </summary>
        public const uint PrimaryVoronoiSalt = HectonVegetationConstants.PrimaryVoronoiSalt;

        /// <summary>
        /// Shared salt for the secondary Voronoi layer that breaks up repetitive floating wall silhouettes.
        /// </summary>
        public const uint SecondaryVoronoiSalt = HectonVegetationConstants.SecondaryVoronoiSalt;

        /// <summary>
        /// Shared salt for occupancy variation applied after the combined Voronoi wall field is evaluated.
        /// </summary>
        public const uint OccupancyVariationSalt = HectonVegetationConstants.OccupancyVariationSalt;

        /// <summary>
        /// Shared salt for the X axis of the domain warp noise that distorts floating labyrinth cells.
        /// </summary>
        public const uint WarpXSalt = HectonVegetationConstants.WarpXSalt;

        /// <summary>
        /// Shared salt for the Z axis of the domain warp noise that distorts floating labyrinth cells.
        /// </summary>
        public const uint WarpZSalt = HectonVegetationConstants.WarpZSalt;

        /// <summary>
        /// Shared salt for the secondary feature-point jitter used inside Voronoi cells.
        /// </summary>
        public const uint SecondaryFeatureSalt = HectonVegetationConstants.SecondaryFeatureSalt;

        /// <summary>
        /// Shared salt for per-cell variation when modulating wall occupancy and instance shaping.
        /// </summary>
        public const uint PrimaryVariationSalt = HectonVegetationConstants.PrimaryVariationSalt;

        [Header("References")]
        [SerializeField]
        [Tooltip("Normative MapMagic owner used to filter foreign tile events.")]
        private MapMagicBridge mapMagicBridge;

        [SerializeField]
        [Tooltip("Player transform used to drive chunk residency.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Indirect renderer used for surface grass and floating sargassum.")]
        private HectonIndirectVegetationRenderer surfaceRenderer;

        [SerializeField]
        [Tooltip("Indirect renderer used for underwater kelp.")]
        private HectonIndirectVegetationRenderer underwaterRenderer;

        [SerializeField]
        [Tooltip("Gameplay camera used for frustum culling. If null, resolved from the player hierarchy.")]
        private Camera viewCamera;

        [Header("Streaming")]
        [SerializeField, Range(150f, 200f)]
        [Tooltip("Residency radius in meters for virtual vegetation chunks.")]
        private float residentRadius = 180f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum number of virtual chunks scanned per SlowTick.")]
        private int maxChunkBuildsPerSlowTick = 2;

        [Header("Predictive Streaming")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds of player velocity projected ahead when shaping predictive chunk residency.")]
        private float predictiveLeadSeconds = 3.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum forward extension in meters added ahead of the player by predictive residency.")]
        private float predictiveLeadMaxMeters = 140f;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Rear residency radius scale applied behind the movement vector for aggressive back-face eviction.")]
        private float rearResidencyScale = 0.55f;

        [SerializeField, Range(0.5f, 1.25f)]
        [Tooltip("Lateral residency radius scale applied around the movement vector.")]
        private float lateralResidencyScale = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum planar speed required before the residency volume stretches along velocity.")]
        private float predictiveMinSpeed = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("Priority boost per forward meter when ordering pending chunk jobs.")]
        private float forwardPriorityBoost = 220f;

        [SerializeField, Min(0f)]
        [Tooltip("Priority penalty per rear meter so chunks behind the player fall to the back of the queue.")]
        private float rearPriorityPenalty = 260f;

        [Header("Chunk Pool")]
        [SerializeField, Min(64)]
        [Tooltip("Persistent native memory budget in megabytes reserved for finalized chunk payload storage.")]
        private int nativePoolBudgetMb = 256;

        [SerializeField, Range(0.5f, 0.9f)]
        [Tooltip("Fraction of the persistent chunk pool reserved for surface grass and floating sargassum.")]
        private float surfacePoolShare = 0.78f;

        [SerializeField, Min(64)]
        [Tooltip("Live chunk-payload occupancy guard in megabytes. When exceeded, the farthest resident chunks are evicted aggressively even if still within the residency cone.")]
        private int nativePoolGuardMb = 700;

        [Header("Audio Handoff")]
        [SerializeField]
        [Tooltip("Optional AudioMixer that receives live vegetation density and acoustic-type handoff for ambient blending.")]
        private AudioMixer vegetationAudioMixer;

        [SerializeField]
        [Tooltip("Exposed AudioMixer float parameter that receives the averaged vegetation density around the player.")]
        private string vegetationDensityMixerParameter = "Hecton_VegetationDensity";

        [SerializeField]
        [Tooltip("Exposed AudioMixer float parameter that receives the dominant vegetation acoustic type around the player.")]
        private string vegetationAcousticTypeMixerParameter = "Hecton_VegetationAcousticType";

        [SerializeField, Min(0.5f)]
        [Tooltip("Probe radius in meters used to average vegetation density around the player for audio handoff.")]
        private float vegetationAudioProbeRadius = 3f;

        [Header("World Rules")]
        [SerializeField, Min(4900f)]
        [Tooltip("Project water surface level. Contract fixes this at Y=4900.")]
        private float waterLevel = DefaultWaterLevel;

        [SerializeField, Min(4600f)]
        [Tooltip("Minimum terrain height that still accepts kelp placement.")]
        private float kelpMinHeight = DefaultKelpMinHeight;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum combined sand mask required for any vegetation sample.")]
        private float sandMaskThreshold = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum rock mask that blocks a vegetation sample.")]
        private float rockMaskThreshold = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum allowed terrain normal Y for ground-anchored vegetation.")]
        private float minimumNormalY = 0.7f;

        [SerializeField, Min(0f)]
        [Tooltip("Offset along the sampled terrain normal for anchored vegetation.")]
        private float normalOffset = 0.04f;

        [SerializeField, Min(0f)]
        [Tooltip("Vertical offset applied to floating sargassum over the water plane.")]
        private float floatingSurfaceOffset = 0.15f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Terrain-height band around the water level used for floating patch eligibility.")]
        private float floatingSurfaceBand = 12f;

        [Header("Vertical Biomes")]
        [SerializeField, Min(100f)]
        [Tooltip("Depth below the water surface in meters where organic underwater flora begins blending into colony graveyard debris.")]
        private float colonyBiomeStartDepth = 500f;

        [SerializeField, Min(600f)]
        [Tooltip("Depth below the water surface in meters where colony graveyard debris begins blending into the sparse dead zone.")]
        private float deadZoneStartDepth = 2000f;

        [SerializeField, Min(10f)]
        [Tooltip("Vertical blend band in meters used to probabilistically mix neighboring abyss layers instead of hard cutting them.")]
        private float verticalBiomeBlendBand = 120f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum Voronoi wall occupancy required to keep a colony-graveyard sample inside the techno-jungle band.")]
        private float technoJungleThreshold = 0.52f;

        [SerializeField, Min(6f)]
        [Tooltip("Primary Voronoi cell size for colony graveyard debris clustering.")]
        private float technoJungleCellSize = 20f;

        [SerializeField, Min(4f)]
        [Tooltip("Secondary Voronoi cell size for breaking up repetitive techno-jungle silhouettes.")]
        private float technoJungleSecondaryCellSize = 12f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Maximum distance from a Voronoi edge that still counts as dense colony debris.")]
        private float technoJungleWallWidth = 2.75f;

        [SerializeField, Min(0f)]
        [Tooltip("World-space warp strength in meters applied before techno-jungle Voronoi evaluation.")]
        private float technoJungleWarpMeters = 7f;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Compression across the primary flow direction for colony graveyard Voronoi strips.")]
        private float technoJungleFlowAnisotropy = 0.36f;

        [SerializeField, Range(0.001f, 0.25f)]
        [Tooltip("Base chance for a dead-zone sample to survive as a rare massive structure before Voronoi weighting is applied.")]
        private float deadZoneStructureChance = 0.045f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Density scale applied to dead-zone keep probability after the organic-to-colony transition is complete.")]
        private float deadZoneDensityScale = 0.18f;

        [Header("Abyssal Flow")]
        [SerializeField, Min(0.0001f)]
        [Tooltip("3D simplex-noise scale used to perturb deep flow vectors below the colony threshold.")]
        private float abyssalFlowNoiseScale = 0.0035f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Horizontal chaos weight injected into deep flow vectors below the colony threshold.")]
        private float abyssalFlowNoiseStrength = 1.15f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Vertical vortex weight injected into deep flow vectors below the colony threshold.")]
        private float abyssalFlowVerticalStrength = 0.42f;

        [Header("Density")]
        [SerializeField, Min(1f)]
        [Tooltip("Surface grass scan spacing in meters. Task contract requires 1-2m.")]
        private float grassStepMeters = 1.5f;

        [SerializeField, Min(4f)]
        [Tooltip("Surface grass scan spacing in meters for chunks beyond the near ring.")]
        private float grassFarStepMeters = 4.5f;

        [SerializeField, Min(25f)]
        [Tooltip("Chunks inside this radius keep the high-density grass scan step.")]
        private float grassHighDensityRadius = 50f;

        [SerializeField, Min(5f)]
        [Tooltip("Kelp scan spacing in meters. Task contract requires 5-10m.")]
        private float kelpStepMeters = 7.5f;

        [SerializeField, Min(5f)]
        [Tooltip("Floating sargassum scan spacing in meters.")]
        private float floatingStepMeters = 9f;

        [SerializeField, Range(0f, 0.95f)]
        [Tooltip("Grass jitter fraction inside each sampling cell.")]
        private float grassJitterFraction = 0.45f;

        [SerializeField, Range(0f, 0.95f)]
        [Tooltip("Kelp jitter fraction inside each sampling cell.")]
        private float kelpJitterFraction = 0.4f;

        [SerializeField, Range(0f, 0.95f)]
        [Tooltip("Floating sargassum jitter fraction inside each sampling cell.")]
        private float floatingJitterFraction = 0.75f;

        [Header("Chunk Edge Dither")]
        [SerializeField, Min(0f)]
        [Tooltip("Distance from the virtual chunk edge where spawn probability starts fading to hide hard 100 m seams.")]
        private float edgeDitherDistance = 2f;

        [Header("Floating Patches")]
        [SerializeField, Min(0.001f)]
        [Tooltip("Domain-warp scale applied before Voronoi evaluation for floating sargassum.")]
        private float floatingPatchNoiseScale = HectonVegetationConstants.FloatingPatchNoiseScale;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum Voronoi wall occupancy required to keep a floating sargassum sample.")]
        private float floatingPatchThreshold = HectonVegetationConstants.FloatingPatchThreshold;

        [Header("Floating Labyrinth")]
        [SerializeField, Min(6f)]
        [Tooltip("Primary Voronoi cell size driving Langmuir-like sargassum windrows.")]
        private float floatingCellSize = HectonVegetationConstants.FloatingPrimaryCellSize;

        [SerializeField, Min(4f)]
        [Tooltip("Secondary Voronoi cell size that breaks up repetitive wall silhouettes.")]
        private float floatingSecondaryCellSize = HectonVegetationConstants.FloatingSecondaryCellSize;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Maximum distance from a Voronoi edge that still counts as a dense sargassum wall.")]
        private float floatingWallWidth = HectonVegetationConstants.FloatingWallWidth;

        [SerializeField, Min(0f)]
        [Tooltip("World-space warp strength in meters applied before Voronoi evaluation.")]
        private float floatingWarpMeters = HectonVegetationConstants.FloatingWarpMeters;

        [SerializeField]
        [Tooltip("Primary drift direction used to stretch cells into Langmuir-like strips.")]
        private Vector2 floatingFlowDirection = HectonVegetationConstants.FloatingFlowDirection;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Compression across the flow direction. Lower values create longer windrows.")]
        private float floatingFlowAnisotropy = HectonVegetationConstants.FloatingFlowAnisotropy;

        [Header("Scale Ranges")]
        [SerializeField]
        [Tooltip("Uniform scale range for grass instances.")]
        private Vector2 grassScaleRange = new Vector2(0.7f, 1.15f);

        [SerializeField]
        [Tooltip("Uniform scale range for kelp instances.")]
        private Vector2 kelpScaleRange = new Vector2(1.15f, 2.1f);

        [SerializeField]
        [Tooltip("Uniform scale range for floating sargassum instances.")]
        private Vector2 floatingScaleRange = new Vector2(0.85f, 1.35f);

        [Header("Stealth Coverage")]
        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Grass concealment weight used by the zero-allocation visibility modifier.")]
        private float grassVisibilityWeight = 0.4f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Kelp concealment weight used by the zero-allocation visibility modifier.")]
        private float kelpVisibilityWeight = 0.82f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Sargassum concealment weight used by the zero-allocation visibility modifier.")]
        private float sargassumVisibilityWeight = 0.95f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Vertical band below the floating canopy where sargassum grants its strongest concealment.")]
        private float sargassumVisibilityBand = 4f;

        [Header("Procedural Variety")]
        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Uniform scale jitter amplitude applied around the sampled base scale. 0.2 means +/-20%.")]
        private float proceduralScaleJitter = 0.2f;

        [SerializeField, Range(0f, 360f)]
        [Tooltip("Additional per-instance rotation jitter in degrees applied in Burst jobs.")]
        private float proceduralRotationJitterDegrees = 360f;

        [Header("Draw Bounds")]
        [SerializeField]
        [Tooltip("Extra padding added to aggregated chunk bounds before binding renderers.")]
        private Vector3 drawBoundsPadding = new Vector3(6f, 24f, 6f);

        [Header("View Culling")]
        [SerializeField]
        [Tooltip("Extra bounds padding applied before frustum culling to avoid hard pop on quick turns.")]
        private Vector3 frustumCullPadding = new Vector3(36f, 24f, 36f);

        private struct LayerIndices
        {
            public int Sand;
            public int GreenSand;
            public int Rock;
        }

        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public ChunkKey(int tileX, int tileZ, int chunkX, int chunkZ)
            {
                TileX = tileX;
                TileZ = tileZ;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
            }

            public int TileX { get; }
            public int TileZ { get; }
            public int ChunkX { get; }
            public int ChunkZ { get; }

            public bool Equals(ChunkKey other)
            {
                return TileX == other.TileX &&
                       TileZ == other.TileZ &&
                       ChunkX == other.ChunkX &&
                       ChunkZ == other.ChunkZ;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = TileX;
                    hash = (hash * 397) ^ TileZ;
                    hash = (hash * 397) ^ ChunkX;
                    hash = (hash * 397) ^ ChunkZ;
                    return hash;
                }
            }
        }

        private struct TileNativeCacheBuffer
        {
            public NativeArray<byte> SandMaskNative;
            public NativeArray<byte> RockMaskNative;
            public NativeArray<float> HeightSamplesNative;
        }

        private sealed class TileRuntimeState
        {
            public int TileX;
            public int TileZ;
            public TerrainTile Tile;
            public Terrain Terrain;
            public TerrainData TerrainData;
            public Vector3 TerrainPosition;
            public Vector3 TerrainSize;
            public int AlphamapResolution;
            public int HeightmapResolution;
            public int ChunkCountX;
            public int ChunkCountZ;
            public LayerIndices LayerIndices;
            public int CacheRevision;
            public int AlphamapTextureCount;
            public int CombinedAlphamapHash;
            public uint CombinedAlphamapUpdateCount;
            public int HeightmapHash;
            public uint HeightmapUpdateCount;
            public int ActiveCacheBufferIndex;
            public TileNativeCacheBuffer PrimaryCacheBuffer;
            public TileNativeCacheBuffer SecondaryCacheBuffer;
        }

        /// <summary>
        /// Shared Voronoi/Worley parameters used by vegetation generation and external sargassum systems.
        /// </summary>
        public readonly struct FloatingLabyrinthConfig
        {
            public FloatingLabyrinthConfig(
                float patchThreshold,
                float patchNoiseScale,
                float cellSize,
                float secondaryCellSize,
                float wallWidth,
                float warpMeters,
                Vector2 flowDirection,
                float flowAnisotropy)
            {
                PatchThreshold = patchThreshold;
                PatchNoiseScale = patchNoiseScale;
                CellSize = cellSize;
                SecondaryCellSize = secondaryCellSize;
                WallWidth = wallWidth;
                WarpMeters = warpMeters;
                FlowDirection = flowDirection;
                FlowAnisotropy = flowAnisotropy;
            }

            public float PatchThreshold { get; }
            public float PatchNoiseScale { get; }
            public float CellSize { get; }
            public float SecondaryCellSize { get; }
            public float WallWidth { get; }
            public float WarpMeters { get; }
            public Vector2 FlowDirection { get; }
            public float FlowAnisotropy { get; }
        }

        /// <summary>
        /// Zero-allocation density query result for gameplay physics and locomotion systems.
        /// </summary>
        public enum VegetationAcousticType : byte
        {
            Silence = 0,
            VegetationRustle = 1,
            SargassumBubbles = 2
        }

        /// <summary>
        /// High-level vertical biome layer resolved from depth below the water surface.
        /// </summary>
        public enum VegetationBiomeLayer : byte
        {
            OrganicShelf = 0,
            ColonyGraveyard = 1,
            DeadZone = 2
        }

        /// <summary>
        /// Semantic spawn type exposed to gameplay/AI systems while keeping legacy render categories stable.
        /// </summary>
        public enum VegetationSemanticType : byte
        {
            OrganicGrass = 0,
            OrganicKelp = 1,
            FloatingSargassum = 2,
            ColonyCable = 3,
            ColonyHullPlating = 4,
            ColonySupportBeam = 5,
            DeadZoneMassiveStructure = 6
        }

        /// <summary>
        /// Zero-allocation density query result for gameplay physics, locomotion and audio systems.
        /// </summary>
        public readonly struct VegetationDensitySample
        {
            public VegetationDensitySample(
                bool hasVegetation,
                HectonVegetationInstanceType type,
                VegetationSemanticType semanticType,
                VegetationBiomeLayer biomeLayer,
                VegetationAcousticType acousticType,
                float density)
            {
                HasVegetation = hasVegetation;
                Type = type;
                SemanticType = semanticType;
                BiomeLayer = biomeLayer;
                AcousticType = acousticType;
                Density = density;
            }

            public bool HasVegetation { get; }
            public HectonVegetationInstanceType Type { get; }
            public VegetationSemanticType SemanticType { get; }
            public VegetationBiomeLayer BiomeLayer { get; }
            public VegetationAcousticType AcousticType { get; }
            public float Density { get; }
        }

        private struct ChunkPayload
        {
            public int SurfaceOffset;
            public int SurfaceCount;
            public int SurfaceEdgeOffset;
            public int UnderwaterOffset;
            public int UnderwaterCount;
            public int UnderwaterEdgeOffset;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
            public Bounds WorldBounds;
            public byte GrassLodTier;

            public bool HasSurface => SurfaceCount > 0;
            public bool HasUnderwater => UnderwaterCount > 0;
        }

        private struct PoolBlock
        {
            public int Offset;
            public int Length;
        }

        private struct NativeChunkPool
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<float> EdgeDistances;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;
            public int Capacity;
        }

        public struct VegetationDensityChunkRecord
        {
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
            public int GridOffset;
            public byte GrassLodTier;
        }

        private struct JobInstanceRecord
        {
            public float4x4 Matrix;
            public float HeightScale;
            public float WidthScale;
            public float Variation;
            public float EdgeDistance;
            public float2 FlowDirection;
            public float3 FlowVector;
            public int Type;
            public int SemanticType;
            public byte BiomeLayer;
            public int IsValid;
        }

        private sealed class ChunkBuildJobState
        {
            public ChunkKey Key;
            public long TileKey;
            public int TileCacheRevision;
            public byte GrassLodTier;
            public ChunkPayload PayloadHeader;
            public JobHandle Handle;
            public NativeArray<JobInstanceRecord> GrassRecords;
            public NativeArray<JobInstanceRecord> FloatingRecords;
            public NativeArray<JobInstanceRecord> KelpRecords;
        }

        // COLD ALLOC: Dictionary<long, TileRuntimeState>[32] - MapMagic tile state cache for chunk streaming - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<long, TileRuntimeState> _tileStates = new Dictionary<long, TileRuntimeState>(InitialTileCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkPayload>[256] - streamed virtual chunk cache - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkPayload> _chunkPayloads = new Dictionary<ChunkKey, ChunkPayload>(InitialChunkCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkBuildJobState>[256] - in-flight Burst chunk generation jobs - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkBuildJobState> _chunkBuildJobs = new Dictionary<ChunkKey, ChunkBuildJobState>(InitialChunkCapacity);
        // COLD ALLOC: List<ChunkKey>[64] - eviction staging for non-resident chunk payloads - owner: HectonMapMagicVegetationBridge
        private readonly List<ChunkKey> _evictionKeys = new List<ChunkKey>(InitialChunkArrayCapacity);
        // COLD ALLOC: List<ChunkKey>[64] - in-flight chunk job scratch list for completion/eviction - owner: HectonMapMagicVegetationBridge
        private readonly List<ChunkKey> _jobScratchKeys = new List<ChunkKey>(InitialChunkArrayCapacity);

        private ChunkKey[] _desiredChunkKeys;
        private float[] _desiredChunkDistances;
        private ChunkKey[] _selectedChunkKeys;
        private bool[] _selectedChunkVisibility;
        private ChunkKey[] _pendingChunkKeys;
        private float[] _pendingChunkPriorities;
        private Matrix4x4[] _surfaceAggregateMatrices = Array.Empty<Matrix4x4>();
        private HectonVegetationInstanceData[] _surfaceAggregateData = Array.Empty<HectonVegetationInstanceData>();
        private int[] _surfaceAggregateTypes = Array.Empty<int>();
        private int[] _surfaceAggregateSemanticTypes = Array.Empty<int>();
        private byte[] _surfaceAggregateBiomeLayers = Array.Empty<byte>();
        private Vector2[] _surfaceAggregateFlowDirections = Array.Empty<Vector2>();
        private Vector3[] _surfaceAggregateFlowVectors = Array.Empty<Vector3>();
        private Matrix4x4[] _underwaterAggregateMatrices = Array.Empty<Matrix4x4>();
        private HectonVegetationInstanceData[] _underwaterAggregateData = Array.Empty<HectonVegetationInstanceData>();
        private int[] _underwaterAggregateTypes = Array.Empty<int>();
        private int[] _underwaterAggregateSemanticTypes = Array.Empty<int>();
        private byte[] _underwaterAggregateBiomeLayers = Array.Empty<byte>();
        private Vector2[] _underwaterAggregateFlowDirections = Array.Empty<Vector2>();
        private Vector3[] _underwaterAggregateFlowVectors = Array.Empty<Vector3>();
        private NativeArray<Matrix4x4> _surfaceAggregateMatricesNative;
        private NativeArray<HectonVegetationInstanceData> _surfaceAggregateDataNative;
        private NativeArray<int> _surfaceAggregateTypesNative;
        private NativeArray<int> _surfaceAggregateSemanticTypesNative;
        private NativeArray<byte> _surfaceAggregateBiomeLayersNative;
        private NativeArray<Vector2> _surfaceAggregateFlowDirectionsNative;
        private NativeArray<Vector3> _surfaceAggregateFlowVectorsNative;
        private NativeArray<Matrix4x4> _underwaterAggregateMatricesNative;
        private NativeArray<HectonVegetationInstanceData> _underwaterAggregateDataNative;
        private NativeArray<int> _underwaterAggregateTypesNative;
        private NativeArray<int> _underwaterAggregateSemanticTypesNative;
        private NativeArray<byte> _underwaterAggregateBiomeLayersNative;
        private NativeArray<Vector2> _underwaterAggregateFlowDirectionsNative;
        private NativeArray<Vector3> _underwaterAggregateFlowVectorsNative;
        private ComputeBuffer _surfaceInstanceBuffer;
        private ComputeBuffer _surfaceInstanceDataBuffer;
        private ComputeBuffer _underwaterInstanceBuffer;
        private ComputeBuffer _underwaterInstanceDataBuffer;
        private NativeChunkPool _surfaceChunkPool;
        private NativeChunkPool _underwaterChunkPool;
        private PoolBlock[] _surfacePoolFreeBlocks;
        private PoolBlock[] _underwaterPoolFreeBlocks;
        private int _surfacePoolFreeBlockCount;
        private int _underwaterPoolFreeBlockCount;
        private readonly Dictionary<ChunkKey, int> _densityQueryChunkLookup = new Dictionary<ChunkKey, int>(InitialChunkCapacity);
        private int _desiredChunkCount;
        private int _selectedChunkCount;
        private int _pendingChunkCount;
        private int _surfaceActiveCount;
        private int _underwaterActiveCount;
        private int _densityQueryChunkCount;
        private long _chunkPayloadUsedBytes;
        private Bounds _surfaceDrawBounds;
        private Bounds _underwaterDrawBounds;
        private float _vegetationAudioDensity;
        private VegetationAcousticType _vegetationAudioAcousticType;
        private float _lastPublishedVegetationAudioDensity = float.NegativeInfinity;
        private VegetationAcousticType _lastPublishedVegetationAudioAcousticType = (VegetationAcousticType)byte.MaxValue;
        private float _nextNativePoolFragmentationLogTime = float.NegativeInfinity;
        private byte _sandMaskThresholdByte;
        private byte _rockMaskThresholdByte;
        private Vector2 _floatingFlowDirectionNormalized;
        private Rigidbody _playerRigidbody;
        private Vector3 _playerVelocity;
        private Vector3 _lastPlayerPosition;
        private bool _hasLastPlayerPosition;
        private Camera _cachedViewCamera;
        private float _nextCameraResolveTime = float.NegativeInfinity;
        private float _nextCacheValidationTime = float.NegativeInfinity;
        private int _cacheValidationChunkCursor;
        private bool _isRegistered;
        private bool _eventsSubscribed;
        private bool _activeSetDirty = true;

        public static float GlobalVegetationAudioDensity { get; private set; }
        public static VegetationAcousticType GlobalVegetationAcousticType { get; private set; }

        // COLD ALLOC: Plane[6] - cached frustum plane array reused for no-alloc chunk visibility tests - owner: HectonMapMagicVegetationBridge
        private readonly Plane[] _viewFrustumPlanes = new Plane[6];
        private ChunkKey[] _densityQueryChunkKeys;
        private NativeArray<VegetationDensityChunkRecord> _densityQueryChunksNative;
        private NativeArray<float3> _densityQueryGridNative;
        private NativeArray<VegetationDensityChunkRecord> _densityQueryChunksScratchNative;
        private NativeArray<float3> _densityQueryGridScratchNative;

        private void Awake()
        {
            residentRadius = Mathf.Clamp(residentRadius, 150f, 200f);
            maxChunkBuildsPerSlowTick = Mathf.Max(1, maxChunkBuildsPerSlowTick);
            predictiveLeadSeconds = Mathf.Max(0f, predictiveLeadSeconds);
            predictiveLeadMaxMeters = Mathf.Max(0f, predictiveLeadMaxMeters);
            rearResidencyScale = Mathf.Clamp(rearResidencyScale, 0.2f, 1f);
            lateralResidencyScale = Mathf.Clamp(lateralResidencyScale, 0.5f, 1.25f);
            predictiveMinSpeed = Mathf.Max(0f, predictiveMinSpeed);
            forwardPriorityBoost = Mathf.Max(0f, forwardPriorityBoost);
            rearPriorityPenalty = Mathf.Max(0f, rearPriorityPenalty);
            nativePoolBudgetMb = Mathf.Max(MinimumNativePoolBudgetMb, nativePoolBudgetMb);
            nativePoolGuardMb = Mathf.Max(MinimumNativePoolBudgetMb, nativePoolGuardMb);
            vegetationAudioProbeRadius = Mathf.Max(0.5f, vegetationAudioProbeRadius);
            surfacePoolShare = Mathf.Clamp(surfacePoolShare, 0.5f, 0.9f);
            kelpMinHeight = Mathf.Clamp(kelpMinHeight, 4600f, waterLevel);
            grassStepMeters = Mathf.Clamp(grassStepMeters, 1f, 2f);
            grassFarStepMeters = Mathf.Clamp(grassFarStepMeters, 4f, 5f);
            grassHighDensityRadius = Mathf.Max(25f, grassHighDensityRadius);
            kelpStepMeters = Mathf.Clamp(kelpStepMeters, 5f, 10f);
            floatingStepMeters = Mathf.Max(5f, floatingStepMeters);
            grassScaleRange = NormalizeScaleRange(grassScaleRange);
            kelpScaleRange = NormalizeScaleRange(kelpScaleRange);
            floatingScaleRange = NormalizeScaleRange(floatingScaleRange);
            grassVisibilityWeight = Mathf.Clamp(grassVisibilityWeight, 0f, 1.5f);
            kelpVisibilityWeight = Mathf.Clamp(kelpVisibilityWeight, 0f, 1.5f);
            sargassumVisibilityWeight = Mathf.Clamp(sargassumVisibilityWeight, 0f, 1.5f);
            sargassumVisibilityBand = Mathf.Max(0.25f, sargassumVisibilityBand);
            proceduralScaleJitter = Mathf.Clamp(proceduralScaleJitter, 0f, 0.5f);
            proceduralRotationJitterDegrees = Mathf.Clamp(proceduralRotationJitterDegrees, 0f, 360f);
            floatingCellSize = Mathf.Max(6f, floatingCellSize);
            floatingSecondaryCellSize = Mathf.Max(4f, floatingSecondaryCellSize);
            floatingWallWidth = Mathf.Clamp(floatingWallWidth, 1f, 6f);
            floatingWarpMeters = Mathf.Max(0f, floatingWarpMeters);
            floatingFlowAnisotropy = Mathf.Clamp(floatingFlowAnisotropy, 0.2f, 1f);
            floatingFlowDirection = NormalizeFlowDirection(floatingFlowDirection);
            edgeDitherDistance = Mathf.Max(0f, edgeDitherDistance);
            _floatingFlowDirectionNormalized = floatingFlowDirection;
            _sandMaskThresholdByte = PackMask01(sandMaskThreshold);
            _rockMaskThresholdByte = PackMask01(rockMaskThreshold);

            ResolveRuntimeDependencies();

            // COLD ALLOC: ChunkKey[64] - desired residency chunk cache - owner: HectonMapMagicVegetationBridge
            _desiredChunkKeys = new ChunkKey[InitialChunkArrayCapacity];
            // COLD ALLOC: float[64] - desired residency distance cache - owner: HectonMapMagicVegetationBridge
            _desiredChunkDistances = new float[InitialChunkArrayCapacity];
            // COLD ALLOC: ChunkKey[64] - selected resident chunk cache - owner: HectonMapMagicVegetationBridge
            _selectedChunkKeys = new ChunkKey[InitialChunkArrayCapacity];
            // COLD ALLOC: bool[64] - selected chunk visibility cache reused inside active-buffer rebuilds - owner: HectonMapMagicVegetationBridge
            _selectedChunkVisibility = new bool[InitialChunkArrayCapacity];
            // COLD ALLOC: ChunkKey[64] - pending chunk scan queue - owner: HectonMapMagicVegetationBridge
            _pendingChunkKeys = new ChunkKey[InitialChunkArrayCapacity];
            // COLD ALLOC: float[64] - pending chunk priority queue cache - owner: HectonMapMagicVegetationBridge
            _pendingChunkPriorities = new float[InitialChunkArrayCapacity];
            // COLD ALLOC: ChunkKey[64] - active density-query chunk key cache - owner: HectonMapMagicVegetationBridge
            _densityQueryChunkKeys = new ChunkKey[InitialChunkArrayCapacity];
            InitializeChunkPools();
        }

        private void OnEnable()
        {
            InitializeChunkPools();
            TrySubscribeEvents();
            TryRegister();
            RefreshResidency();
        }

        private void Start()
        {
            TryRegister();
            RefreshResidency();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
            DisposeAllTileNativeCaches();
            DisposeActiveNativeAggregates();
            DisposeDensityQuerySnapshot();
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            _tileStates.Clear();
            ClearVegetationAudioHandoff();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
            DisposeAllTileNativeCaches();
            DisposeActiveNativeAggregates();
            DisposeDensityQuerySnapshot();
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            _tileStates.Clear();
            ClearVegetationAudioHandoff();
        }

        /// <summary>
        /// Polls in-flight chunk generation jobs and binds finished payloads without blocking the schedule path.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            UpdatePlayerMotionState(dt);
            if (TryValidateResidentTileCaches())
            {
                RefreshResidency();
                return;
            }

            if (_chunkBuildJobs.Count == 0 && !_activeSetDirty)
                return;

            int completedCount = FinalizeCompletedChunkBuilds();
            if (completedCount > 0)
                EnforceChunkPoolMemoryGuard();

            bool selectionChanged = completedCount > 0 && SyncSelectedChunksFromDesired();
            if (completedCount > 0 || selectionChanged || _activeSetDirty)
            {
                RebuildAndBindActiveBuffers();
                _activeSetDirty = false;
            }
        }

        /// <summary>
        /// Re-evaluates active chunk residency and incrementally scans missing virtual chunks.
        /// </summary>
        public void SlowTick()
        {
            ResolveRuntimeDependencies();
            TryBootstrapExistingTiles();
            RefreshResidency();
            UpdateVegetationAudioHandoff();
            LogNativePoolFragmentationIfDue();
        }

        /// <summary>Active surface instance matrix buffer currently owned by this bridge.</summary>
        public ComputeBuffer SurfaceInstanceMatrixBuffer => _surfaceInstanceBuffer;

        /// <summary>Active surface instance metadata buffer currently owned by this bridge.</summary>
        public ComputeBuffer SurfaceInstanceDataBuffer => _surfaceInstanceDataBuffer;

        /// <summary>Active underwater instance matrix buffer currently owned by this bridge.</summary>
        public ComputeBuffer UnderwaterInstanceMatrixBuffer => _underwaterInstanceBuffer;

        /// <summary>Active underwater instance metadata buffer currently owned by this bridge.</summary>
        public ComputeBuffer UnderwaterInstanceDataBuffer => _underwaterInstanceDataBuffer;

        /// <summary>Active surface matrix cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public Matrix4x4[] ActiveSurfaceMatrices => _surfaceAggregateMatrices;

        /// <summary>Active surface metadata cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public HectonVegetationInstanceData[] ActiveSurfaceMetadata => _surfaceAggregateData;

        /// <summary>Active surface type cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public int[] ActiveSurfaceTypes => _surfaceAggregateTypes;

        /// <summary>Active surface semantic-type cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public int[] ActiveSurfaceSemanticTypes => _surfaceAggregateSemanticTypes;

        /// <summary>Active surface biome-layer cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public byte[] ActiveSurfaceBiomeLayers => _surfaceAggregateBiomeLayers;

        /// <summary>Active surface flow-direction cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public Vector2[] ActiveSurfaceFlowDirections => _surfaceAggregateFlowDirections;

        /// <summary>Active surface 3D flow-vector cache. Valid entries are in the 0..ActiveSurfaceInstanceCount range.</summary>
        public Vector3[] ActiveSurfaceFlowVectors => _surfaceAggregateFlowVectors;

        /// <summary>Active surface matrix cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<Matrix4x4> ActiveSurfaceMatricesNative => _surfaceAggregateMatricesNative;

        /// <summary>Active surface metadata cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<HectonVegetationInstanceData> ActiveSurfaceMetadataNative => _surfaceAggregateDataNative;

        /// <summary>Active surface type cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<int> ActiveSurfaceTypesNative => _surfaceAggregateTypesNative;

        /// <summary>Active surface semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int> ActiveSurfaceSemanticTypesNative => _surfaceAggregateSemanticTypesNative;

        /// <summary>Active surface biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte> ActiveSurfaceBiomeLayersNative => _surfaceAggregateBiomeLayersNative;

        /// <summary>Active surface flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2> ActiveSurfaceFlowDirectionsNative => _surfaceAggregateFlowDirectionsNative;

        /// <summary>Active surface 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3> ActiveSurfaceFlowVectorsNative => _surfaceAggregateFlowVectorsNative;

        /// <summary>Active underwater matrix cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public Matrix4x4[] ActiveUnderwaterMatrices => _underwaterAggregateMatrices;

        /// <summary>Active underwater metadata cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public HectonVegetationInstanceData[] ActiveUnderwaterMetadata => _underwaterAggregateData;

        /// <summary>Active underwater type cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public int[] ActiveUnderwaterTypes => _underwaterAggregateTypes;

        /// <summary>Active underwater semantic-type cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public int[] ActiveUnderwaterSemanticTypes => _underwaterAggregateSemanticTypes;

        /// <summary>Active underwater biome-layer cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public byte[] ActiveUnderwaterBiomeLayers => _underwaterAggregateBiomeLayers;

        /// <summary>Active underwater flow-direction cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public Vector2[] ActiveUnderwaterFlowDirections => _underwaterAggregateFlowDirections;

        /// <summary>Active underwater 3D flow-vector cache. Valid entries are in the 0..ActiveUnderwaterInstanceCount range.</summary>
        public Vector3[] ActiveUnderwaterFlowVectors => _underwaterAggregateFlowVectors;

        /// <summary>Active underwater matrix cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<Matrix4x4> ActiveUnderwaterMatricesNative => _underwaterAggregateMatricesNative;

        /// <summary>Active underwater metadata cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<HectonVegetationInstanceData> ActiveUnderwaterMetadataNative => _underwaterAggregateDataNative;

        /// <summary>Active underwater type cache in persistent native memory for direct ComputeBuffer.SetData handoff.</summary>
        public NativeArray<int> ActiveUnderwaterTypesNative => _underwaterAggregateTypesNative;

        /// <summary>Active underwater semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int> ActiveUnderwaterSemanticTypesNative => _underwaterAggregateSemanticTypesNative;

        /// <summary>Active underwater biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte> ActiveUnderwaterBiomeLayersNative => _underwaterAggregateBiomeLayersNative;

        /// <summary>Active underwater flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2> ActiveUnderwaterFlowDirectionsNative => _underwaterAggregateFlowDirectionsNative;

        /// <summary>Active underwater 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3> ActiveUnderwaterFlowVectorsNative => _underwaterAggregateFlowVectorsNative;

        /// <summary>Number of active surface instances.</summary>
        public int ActiveSurfaceInstanceCount => _surfaceActiveCount;

        /// <summary>Number of active underwater instances.</summary>
        public int ActiveUnderwaterInstanceCount => _underwaterActiveCount;

        /// <summary>Explicit surface draw bounds used for the current indirect payload.</summary>
        public Bounds ActiveSurfaceDrawBounds => _surfaceDrawBounds;

        /// <summary>Explicit underwater draw bounds used for the current indirect payload.</summary>
        public Bounds ActiveUnderwaterDrawBounds => _underwaterDrawBounds;

        /// <summary>Current live chunk-payload occupancy in bytes across both persistent native pools.</summary>
        public long ChunkPayloadUsedBytes => _chunkPayloadUsedBytes;

        /// <summary>Hard occupancy guard in bytes used for aggressive far-chunk eviction.</summary>
        public long ChunkPayloadGuardBytes => Mathf.Max(MinimumNativePoolBudgetMb, nativePoolGuardMb) * 1024L * 1024L;

        /// <summary>Total bytes currently retained by the double-buffered tile caches.</summary>
        public long TileCacheUsedBytes => ComputeTileCacheUsedBytes();

        /// <summary>Latest published averaged vegetation density for external audio systems.</summary>
        public float CurrentVegetationAudioDensity => _vegetationAudioDensity;

        /// <summary>Latest published vegetation acoustic type for external audio systems.</summary>
        public VegetationAcousticType CurrentVegetationAcousticType => _vegetationAudioAcousticType;

        /// <summary>Combined free-list fragmentation percent across both native chunk pools.</summary>
        public float NativePoolFragmentationPercent => ComputeNativePoolFragmentationPercent();

        /// <summary>Shared Voronoi/Worley configuration consumed by vegetation generation and external sargassum systems.</summary>
        public FloatingLabyrinthConfig ActiveFloatingLabyrinthConfig => new FloatingLabyrinthConfig(
            floatingPatchThreshold,
            floatingPatchNoiseScale,
            floatingCellSize,
            floatingSecondaryCellSize,
            floatingWallWidth,
            floatingWarpMeters,
            _floatingFlowDirectionNormalized,
            floatingFlowAnisotropy);

        /// <summary>
        /// Evaluates the shared Voronoi/Worley labyrinth with an explicit config so external systems can stay bitwise-aligned with the bridge.
        /// </summary>
        public static bool EvaluateFloatingLabyrinth(
            float worldX,
            float worldZ,
            uint seed,
            in FloatingLabyrinthConfig config,
            out float occupancy)
        {
            Vector2 flowDirection = NormalizeFlowDirection(config.FlowDirection);
            return TryEvaluateFloatingLabyrinth(
                worldX,
                worldZ,
                seed,
                config.PatchThreshold,
                config.PatchNoiseScale,
                config.CellSize,
                config.SecondaryCellSize,
                config.WallWidth,
                config.WarpMeters,
                new float2(flowDirection.x, flowDirection.y),
                config.FlowAnisotropy,
                out occupancy);
        }

        /// <summary>
        /// Copies the active surface matrices and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid surface instances written into the arrays.</returns>
        public int CopyActiveSurfaceInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return CopyActiveInstances(_surfaceAggregateMatrices, _surfaceAggregateTypes, _surfaceActiveCount, ref matrices, ref types);
        }

        /// <summary>
        /// Copies the active underwater matrices and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid underwater instances written into the arrays.</returns>
        public int CopyActiveUnderwaterInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return CopyActiveInstances(_underwaterAggregateMatrices, _underwaterAggregateTypes, _underwaterActiveCount, ref matrices, ref types);
        }

        /// <summary>
        /// Copies the active surface matrices, metadata payloads, and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="metadata">Caller-owned metadata array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid surface instances written into the arrays.</returns>
        public int CopyActiveSurfacePayload(
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            return CopyActivePayload(
                _surfaceAggregateMatrices,
                _surfaceAggregateData,
                _surfaceAggregateTypes,
                _surfaceActiveCount,
                ref matrices,
                ref metadata,
                ref types);
        }

        /// <summary>
        /// Copies the active underwater matrices, metadata payloads, and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="metadata">Caller-owned metadata array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid underwater instances written into the arrays.</returns>
        public int CopyActiveUnderwaterPayload(
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            return CopyActivePayload(
                _underwaterAggregateMatrices,
                _underwaterAggregateData,
                _underwaterAggregateTypes,
                _underwaterActiveCount,
                ref matrices,
                ref metadata,
                ref types);
        }

        /// <summary>
        /// Returns the current surface payload as native memory ready for direct ComputeBuffer.SetData handoff.
        /// </summary>
        public bool TryGetActiveSurfaceNativePayload(
            out NativeArray<Matrix4x4> matrices,
            out NativeArray<HectonVegetationInstanceData> metadata,
            out NativeArray<int> types,
            out int count)
        {
            matrices = _surfaceAggregateMatricesNative;
            metadata = _surfaceAggregateDataNative;
            types = _surfaceAggregateTypesNative;
            count = _surfaceActiveCount;
            return count > 0 &&
                   matrices.IsCreated &&
                   metadata.IsCreated &&
                   types.IsCreated;
        }

        /// <summary>
        /// Returns the current surface flow payload as native memory ready for ocean/renderer consumption.
        /// </summary>
        public bool TryGetActiveSurfaceFlowPayload(out NativeArray<Vector2> flowDirections, out int count)
        {
            flowDirections = _surfaceAggregateFlowDirectionsNative;
            count = _surfaceActiveCount;
            return count > 0 && flowDirections.IsCreated;
        }

        /// <summary>
        /// Returns the current surface semantic payload as native memory for AI and deep-biome consumers.
        /// </summary>
        public bool TryGetActiveSurfaceSemanticPayload(
            out NativeArray<int> semanticTypes,
            out NativeArray<byte> biomeLayers,
            out int count)
        {
            semanticTypes = _surfaceAggregateSemanticTypesNative;
            biomeLayers = _surfaceAggregateBiomeLayersNative;
            count = _surfaceActiveCount;
            return count > 0 && semanticTypes.IsCreated && biomeLayers.IsCreated;
        }

        /// <summary>
        /// Returns the current surface 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveSurfaceFlowVectorPayload(out NativeArray<Vector3> flowVectors, out int count)
        {
            flowVectors = _surfaceAggregateFlowVectorsNative;
            count = _surfaceActiveCount;
            return count > 0 && flowVectors.IsCreated;
        }

        /// <summary>
        /// Returns the current underwater payload as native memory ready for direct ComputeBuffer.SetData handoff.
        /// </summary>
        public bool TryGetActiveUnderwaterNativePayload(
            out NativeArray<Matrix4x4> matrices,
            out NativeArray<HectonVegetationInstanceData> metadata,
            out NativeArray<int> types,
            out int count)
        {
            matrices = _underwaterAggregateMatricesNative;
            metadata = _underwaterAggregateDataNative;
            types = _underwaterAggregateTypesNative;
            count = _underwaterActiveCount;
            return count > 0 &&
                   matrices.IsCreated &&
                   metadata.IsCreated &&
                   types.IsCreated;
        }

        /// <summary>
        /// Returns the current underwater flow payload as native memory ready for ocean/renderer consumption.
        /// </summary>
        public bool TryGetActiveUnderwaterFlowPayload(out NativeArray<Vector2> flowDirections, out int count)
        {
            flowDirections = _underwaterAggregateFlowDirectionsNative;
            count = _underwaterActiveCount;
            return count > 0 && flowDirections.IsCreated;
        }

        /// <summary>
        /// Returns the current underwater semantic payload as native memory for AI and deep-biome consumers.
        /// </summary>
        public bool TryGetActiveUnderwaterSemanticPayload(
            out NativeArray<int> semanticTypes,
            out NativeArray<byte> biomeLayers,
            out int count)
        {
            semanticTypes = _underwaterAggregateSemanticTypesNative;
            biomeLayers = _underwaterAggregateBiomeLayersNative;
            count = _underwaterActiveCount;
            return count > 0 && semanticTypes.IsCreated && biomeLayers.IsCreated;
        }

        /// <summary>
        /// Returns the current underwater 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveUnderwaterFlowVectorPayload(out NativeArray<Vector3> flowVectors, out int count)
        {
            flowVectors = _underwaterAggregateFlowVectorsNative;
            count = _underwaterActiveCount;
            return count > 0 && flowVectors.IsCreated;
        }

        /// <summary>
        /// Samples biomass density immediately on the main thread from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleBiomassDensityImmediate(Vector3 positionWS, int typeMask = DensityTypeMaskAll)
        {
            if (!_densityQueryChunksNative.IsCreated || !_densityQueryGridNative.IsCreated || _densityQueryChunkCount <= 0)
                return 0f;

            float3 position = new float3(positionWS.x, positionWS.y, positionWS.z);
            return ApplyDensityTypeMask(
                SampleDensityChannelsAtPosition(position, _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount),
                typeMask);
        }

        /// <summary>
        /// Returns the dominant vegetation type and current density at a world-space position without allocations.
        /// </summary>
        public VegetationDensitySample GetVegetationDensity(Vector3 position)
        {
            float3 densityChannels = float3.zero;
            if (_densityQueryChunksNative.IsCreated && _densityQueryGridNative.IsCreated && _densityQueryChunkCount > 0)
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    _densityQueryChunksNative,
                    _densityQueryGridNative,
                    _densityQueryChunkCount);
            }

            if (TryBuildDensitySample(position, densityChannels, out VegetationDensitySample densitySample))
                return densitySample;

            if (TryResolveVegetationTypeFromCachedMasks(position, out HectonVegetationInstanceType dominantType))
            {
                uint seed = ResolveWorldQuerySeed(position);
                VegetationBiomeLayer biomeLayer = ResolveBiomeLayer(position.y, seed);
                return new VegetationDensitySample(
                    true,
                    dominantType,
                    ResolveSemanticType(dominantType, biomeLayer, seed),
                    biomeLayer,
                    ResolveAcousticType(dominantType, 0f),
                    0f);
            }

            return default;
        }

        internal Vector3 ApplyAbyssalFlowNoise(Vector3 baseFlow, Vector3 position)
        {
            uint seed = ResolveWorldQuerySeed(position);
            float3 noisyFlow = ApplyAbyssalFlowNoiseStatic(
                new float3(baseFlow.x, baseFlow.y, baseFlow.z),
                new float3(position.x, position.y, position.z),
                math.max(0f, waterLevel - position.y),
                colonyBiomeStartDepth,
                abyssalFlowNoiseScale,
                abyssalFlowNoiseStrength,
                abyssalFlowVerticalStrength,
                seed);
            return new Vector3(noisyFlow.x, noisyFlow.y, noisyFlow.z);
        }

        /// <summary>
        /// Returns a zero-allocation concealment coefficient at the given world-space position.
        /// 0 = fully exposed, 1 = strongly hidden by local vegetation cover.
        /// </summary>
        public float GetPlayerVisibilityModifier(Vector3 position)
        {
            float3 densityChannels = float3.zero;
            if (_densityQueryChunksNative.IsCreated && _densityQueryGridNative.IsCreated && _densityQueryChunkCount > 0)
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    _densityQueryChunksNative,
                    _densityQueryGridNative,
                    _densityQueryChunkCount);
            }

            if (math.lengthsq(densityChannels) <= 0.000001f &&
                TryResolveVegetationTypeFromCachedMasks(position, out HectonVegetationInstanceType fallbackType))
            {
                densityChannels = ResolveFallbackVisibilityChannels(position, fallbackType);
            }

            return EvaluateVisibilityModifier(position, densityChannels);
        }

        /// <summary>
        /// Schedules a Burst-compatible visibility query for a batch of world-space positions using the immutable density snapshot.
        /// </summary>
        public bool TryScheduleVisibilityModifierSample(
            NativeArray<Vector3> positions,
            NativeArray<float> outputVisibility,
            out JobHandle handle)
        {
            handle = default;
            if (!_densityQueryChunksNative.IsCreated ||
                !_densityQueryGridNative.IsCreated ||
                _densityQueryChunkCount <= 0 ||
                !positions.IsCreated ||
                !outputVisibility.IsCreated ||
                outputVisibility.Length < positions.Length)
            {
                return false;
            }

            var job = new VegetationDensityQueryJob
            {
                Positions = positions,
                Output = outputVisibility,
                Chunks = _densityQueryChunksNative,
                DensityGrid = _densityQueryGridNative,
                ChunkCount = _densityQueryChunkCount,
                GrassVisibilityWeight = grassVisibilityWeight,
                KelpVisibilityWeight = kelpVisibilityWeight,
                SargassumVisibilityWeight = sargassumVisibilityWeight,
                WaterLevel = waterLevel,
                FloatingSurfaceOffset = floatingSurfaceOffset,
                SargassumVisibilityBand = sargassumVisibilityBand
            };

            handle = job.Schedule(positions.Length, DefaultJobBatchSize);
            return true;
        }

        /// <summary>
        /// Schedules a Burst density readback job for a batch of world-space sample positions.
        /// </summary>
        public bool TryScheduleBiomassDensitySample(
            NativeArray<float3> positions,
            NativeArray<float> outputDensities,
            out JobHandle handle,
            int typeMask = DensityTypeMaskAll)
        {
            handle = default;
            if (!_densityQueryChunksNative.IsCreated ||
                !_densityQueryGridNative.IsCreated ||
                _densityQueryChunkCount <= 0 ||
                !positions.IsCreated ||
                !outputDensities.IsCreated ||
                outputDensities.Length < positions.Length)
            {
                return false;
            }

            var job = new SampleBiomassDensityJob
            {
                Positions = positions,
                Output = outputDensities,
                Chunks = _densityQueryChunksNative,
                DensityGrid = _densityQueryGridNative,
                ChunkCount = _densityQueryChunkCount,
                TypeMask = typeMask
            };

            handle = job.Schedule(positions.Length, DefaultJobBatchSize);
            return true;
        }

        /// <summary>
        /// Schedules a two-stage Burst readback that samples each point and writes the average biomass density into averageOutput[0].
        /// </summary>
        public bool TryScheduleAverageBiomassDensitySample(
            NativeArray<float3> positions,
            NativeArray<float> perPointDensities,
            NativeArray<float> averageOutput,
            out JobHandle handle,
            int typeMask = DensityTypeMaskAll)
        {
            handle = default;
            if (!TryScheduleBiomassDensitySample(positions, perPointDensities, out JobHandle sampleHandle, typeMask))
                return false;

            if (!averageOutput.IsCreated || averageOutput.Length < 1)
                return false;

            var averageJob = new ReduceAverageDensityJob
            {
                Input = perPointDensities,
                Output = averageOutput
            };

            handle = averageJob.Schedule(sampleHandle);
            return true;
        }

        /// <summary>
        /// Applies a world-space origin offset to all finalized chunk payload matrices and cached bounds in one cold-path pass.
        /// </summary>
        public void ApplyWorldOffsetToAllChunks(Vector3 offset)
        {
            if (offset.sqrMagnitude <= 0.000001f)
                return;

            Vector3 appliedOffset = -offset;
            float3 appliedOffset3 = new float3(appliedOffset.x, appliedOffset.y, appliedOffset.z);
            DisposeAllChunkBuildJobs();

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
                _evictionKeys.Add(payloadEnumerator.Current.Key);

            JobHandle shiftHandle = default;
            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                if (payload.SurfaceCount > 0)
                    shiftHandle = ScheduleShiftChunkSlice(_surfaceChunkPool.Matrices, payload.SurfaceOffset, payload.SurfaceCount, appliedOffset3, shiftHandle);

                if (payload.UnderwaterCount > 0)
                    shiftHandle = ScheduleShiftChunkSlice(_underwaterChunkPool.Matrices, payload.UnderwaterOffset, payload.UnderwaterCount, appliedOffset3, shiftHandle);

                ShiftChunkPayloadBounds(ref payload, appliedOffset);
                _chunkPayloads[key] = payload;
            }

            if (_evictionKeys.Count > 0)
                shiftHandle.Complete();

            Dictionary<long, TileRuntimeState>.Enumerator tileEnumerator = _tileStates.GetEnumerator();
            while (tileEnumerator.MoveNext())
            {
                TileRuntimeState state = tileEnumerator.Current.Value;
                if (state == null)
                    continue;

                state.TerrainPosition += appliedOffset;
            }

            if (_hasLastPlayerPosition)
                _lastPlayerPosition += appliedOffset;

            if (_surfaceDrawBounds.size.sqrMagnitude > 0f)
                _surfaceDrawBounds.center += appliedOffset;
            if (_underwaterDrawBounds.size.sqrMagnitude > 0f)
                _underwaterDrawBounds.center += appliedOffset;

            _activeSetDirty = true;
            RefreshResidency();
        }

        private void HandleTileApplied(TerrainTile tile, TileData tileData, StopToken stop)
        {
            if (!isActiveAndEnabled || tile == null || tileData == null || tileData.isDraft)
                return;

            if (stop != null && stop.stop)
                return;

            ResolveRuntimeDependencies();
            if (IsForeignTile(tile))
                return;

            UpsertTileState(tile);
            _activeSetDirty = true;
            RefreshResidency();
        }

        private void HandleTileMoved(TerrainTile tile)
        {
            if (!isActiveAndEnabled || tile == null)
                return;

            ResolveRuntimeDependencies();
            if (IsForeignTile(tile))
                return;

            RemoveTileState(tile.coord.x, tile.coord.z);
            RefreshResidency();
        }

        private bool TryValidateResidentTileCaches()
        {
            if (_tileStates.Count == 0 || playerTransform == null || Time.unscaledTime < _nextCacheValidationTime)
                return false;

            _nextCacheValidationTime = Time.unscaledTime + CacheValidationInterval;
            bool changed = false;
            int remainingBudget = CacheValidationTileBudget;
            long validatedTileA = long.MinValue;
            long validatedTileB = long.MinValue;

            if (TryFindPlayerTileState(playerTransform.position, out TileRuntimeState playerTileState) &&
                playerTileState != null)
            {
                long playerTileKey = PackTileCoord(playerTileState.TileX, playerTileState.TileZ);
                if (TryValidateTileState(playerTileState))
                    changed = true;

                validatedTileA = playerTileKey;
                remainingBudget--;
            }

            if (_selectedChunkCount <= 0 || remainingBudget <= 0)
                return changed;

            int startIndex = _cacheValidationChunkCursor;
            for (int scanned = 0; scanned < _selectedChunkCount && remainingBudget > 0; scanned++)
            {
                int selectedIndex = (startIndex + scanned) % _selectedChunkCount;
                ChunkKey key = _selectedChunkKeys[selectedIndex];
                long tileKey = PackTileCoord(key.TileX, key.TileZ);
                if (tileKey == validatedTileA || tileKey == validatedTileB)
                    continue;

                if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null)
                    continue;

                if (TryValidateTileState(state))
                    changed = true;

                if (validatedTileA == long.MinValue)
                    validatedTileA = tileKey;
                else
                    validatedTileB = tileKey;

                remainingBudget--;
                _cacheValidationChunkCursor = (selectedIndex + 1) % _selectedChunkCount;
            }

            return changed;
        }

        private bool TryFindPlayerTileState(Vector3 playerPosition, out TileRuntimeState state)
        {
            return TryFindTileStateAtPosition(playerPosition, out state);
        }

        private bool TryFindTileStateAtPosition(Vector3 worldPosition, out TileRuntimeState state)
        {
            state = null;
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState candidate = enumerator.Current.Value;
                if (candidate == null)
                    continue;

                Vector3 terrainMin = candidate.TerrainPosition;
                Vector3 terrainMax = candidate.TerrainPosition + candidate.TerrainSize;
                if (worldPosition.x < terrainMin.x || worldPosition.x > terrainMax.x)
                    continue;
                if (worldPosition.z < terrainMin.z || worldPosition.z > terrainMax.z)
                    continue;

                state = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetActiveTileCache(
            TileRuntimeState state,
            out NativeArray<byte> sandMask,
            out NativeArray<byte> rockMask,
            out NativeArray<float> heightSamples)
        {
            sandMask = default;
            rockMask = default;
            heightSamples = default;
            if (state == null)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (!buffer.SandMaskNative.IsCreated ||
                !buffer.RockMaskNative.IsCreated ||
                !buffer.HeightSamplesNative.IsCreated)
            {
                return false;
            }

            sandMask = buffer.SandMaskNative;
            rockMask = buffer.RockMaskNative;
            heightSamples = buffer.HeightSamplesNative;
            return true;
        }

        private static bool HasTileCacheSignatureChanged(TileRuntimeState state, TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return false;

            CaptureTileCacheSignature(
                terrainData,
                out int alphamapTextureCount,
                out int combinedAlphamapHash,
                out uint combinedAlphamapUpdateCount,
                out int heightmapHash,
                out uint heightmapUpdateCount);

            return state.AlphamapTextureCount != alphamapTextureCount ||
                   state.CombinedAlphamapHash != combinedAlphamapHash ||
                   state.CombinedAlphamapUpdateCount != combinedAlphamapUpdateCount ||
                   state.HeightmapHash != heightmapHash ||
                   state.HeightmapUpdateCount != heightmapUpdateCount;
        }

        private bool TryValidateTileState(TileRuntimeState state)
        {
            if (state == null || state.TerrainData == null)
                return false;

            if (!HasTileCacheSignatureChanged(state, state.TerrainData))
                return false;

            InvalidateTileChunks(state.TileX, state.TileZ, state.ChunkCountX, state.ChunkCountZ);
            CacheTileMasks(state, state.TerrainData);
            _activeSetDirty = true;
            return true;
        }

        private void RefreshResidency()
        {
            if (_desiredChunkKeys == null || _desiredChunkDistances == null || _selectedChunkKeys == null || _pendingChunkKeys == null)
                return;

            if (_tileStates.Count == 0 || playerTransform == null)
            {
                ClearAllResidency();
                return;
            }

            TryValidateResidentTileCaches();
            Vector3 playerPosition = playerTransform.position;
            BuildDesiredChunkList(playerPosition);
            TrimPendingQueueToDesired();
            EvictNonResidentChunkPayloads();
            EnforceChunkPoolMemoryGuard();
            TrimPendingQueueToDesired();

            ProcessPendingChunkBuilds();
            bool selectionChanged = SyncSelectedChunksFromDesired();

            if (selectionChanged || _activeSetDirty)
            {
                RebuildAndBindActiveBuffers();
                _activeSetDirty = false;
            }
        }

        private void BuildDesiredChunkList(Vector3 playerPosition)
        {
            _desiredChunkCount = 0;
            for (int i = 0; i < _desiredChunkDistances.Length; i++)
                _desiredChunkDistances[i] = float.PositiveInfinity;

            Vector2 playerPositionXZ = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 planarVelocity = new Vector2(_playerVelocity.x, _playerVelocity.z);
            float planarSpeed = planarVelocity.magnitude;
            bool usePredictiveResidency = planarSpeed >= predictiveMinSpeed;
            Vector2 forward = usePredictiveResidency ? planarVelocity / Mathf.Max(0.0001f, planarSpeed) : Vector2.right;
            Vector2 right = new Vector2(-forward.y, forward.x);
            float forwardRadius = residentRadius + Mathf.Min(predictiveLeadMaxMeters, planarSpeed * predictiveLeadSeconds);
            float rearRadius = residentRadius * rearResidencyScale;
            float lateralRadius = residentRadius * lateralResidencyScale;
            float searchRadius = usePredictiveResidency ? Mathf.Max(forwardRadius, Mathf.Max(rearRadius, lateralRadius)) : residentRadius;
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null || state.TerrainData == null)
                    continue;

                int minChunkX = GetChunkRangeStart(playerPosition.x - searchRadius, state.TerrainPosition.x, state.ChunkCountX);
                int maxChunkX = GetChunkRangeEnd(playerPosition.x + searchRadius, state.TerrainPosition.x, state.ChunkCountX);
                int minChunkZ = GetChunkRangeStart(playerPosition.z - searchRadius, state.TerrainPosition.z, state.ChunkCountZ);
                int maxChunkZ = GetChunkRangeEnd(playerPosition.z + searchRadius, state.TerrainPosition.z, state.ChunkCountZ);

                if (minChunkX > maxChunkX || minChunkZ > maxChunkZ)
                    continue;

                for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                {
                    for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                    {
                        GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
                        if (!TryEvaluateResidencyCandidate(
                                playerPositionXZ,
                                forward,
                                right,
                                usePredictiveResidency,
                                forwardRadius,
                                rearRadius,
                                lateralRadius,
                                minX,
                                maxX,
                                minZ,
                                maxZ,
                                out float distanceSqr,
                                out float priority))
                        {
                            continue;
                        }

                        ChunkKey key = new ChunkKey(state.TileX, state.TileZ, chunkX, chunkZ);
                        InsertDesiredChunk(key, priority);
                        byte desiredGrassLodTier = GetGrassLodTier(distanceSqr);
                        bool hasPayload = _chunkPayloads.TryGetValue(key, out ChunkPayload payload);
                        bool hasInFlightJob = _chunkBuildJobs.TryGetValue(key, out _);
                        if (!hasPayload && !hasInFlightJob)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                        else if (hasPayload && payload.GrassLodTier != desiredGrassLodTier && !hasInFlightJob)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                    }
                }
            }
        }

        private int ProcessPendingChunkBuilds()
        {
            if (_pendingChunkCount <= 0)
                return 0;

            int buildBudget = Mathf.Min(maxChunkBuildsPerSlowTick, _pendingChunkCount);
            int scheduledCount = 0;

            for (int i = 0; i < buildBudget; i++)
            {
                ChunkKey key = _pendingChunkKeys[0];
                DequeuePendingChunkAt(0);

                if (_chunkBuildJobs.ContainsKey(key))
                    continue;

                long tileKey = PackTileCoord(key.TileX, key.TileZ);
                if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null || state.TerrainData == null)
                    continue;

                byte grassLodTier = ResolveGrassLodTier(state, key.ChunkX, key.ChunkZ, playerTransform.position);
                if (!ScheduleChunkBuild(state, key, tileKey, grassLodTier))
                    continue;

                scheduledCount++;
            }

            return scheduledCount;
        }

        private bool SyncSelectedChunksFromDesired()
        {
            bool changed = false;
            int nextSelectedCount = 0;

            for (int i = 0; i < _desiredChunkCount; i++)
            {
                ChunkKey key = _desiredChunkKeys[i];
                if (!_chunkPayloads.ContainsKey(key))
                    continue;

                EnsureChunkKeyCapacity(ref _selectedChunkKeys, nextSelectedCount + 1);
                if (!changed)
                {
                    if (nextSelectedCount >= _selectedChunkCount || !_selectedChunkKeys[nextSelectedCount].Equals(key))
                        changed = true;
                }

                _selectedChunkKeys[nextSelectedCount] = key;
                nextSelectedCount++;
            }

            if (!changed && nextSelectedCount != _selectedChunkCount)
                changed = true;

            for (int i = nextSelectedCount; i < _selectedChunkCount; i++)
                _selectedChunkKeys[i] = default;

            _selectedChunkCount = nextSelectedCount;
            return changed;
        }

        private void RebuildAndBindActiveBuffers()
        {
            RebuildDensityQuerySnapshot();
            EnsureBoolCapacity(ref _selectedChunkVisibility, _selectedChunkCount);
            int totalSurfaceCount = 0;
            int totalUnderwaterCount = 0;
            bool hasSurfaceBounds = false;
            bool hasUnderwaterBounds = false;
            Bounds surfaceBounds = default;
            Bounds underwaterBounds = default;
            Camera activeViewCamera = ResolveActiveViewCamera();
            bool hasViewCamera = activeViewCamera != null;
            if (hasViewCamera)
                GeometryUtility.CalculateFrustumPlanes(activeViewCamera, _viewFrustumPlanes);

            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                {
                    _selectedChunkVisibility[i] = false;
                    continue;
                }

                bool isVisible = !hasViewCamera || IsChunkVisible(payload.WorldBounds);
                _selectedChunkVisibility[i] = isVisible;
                if (!isVisible)
                    continue;

                if (payload.HasSurface)
                {
                    totalSurfaceCount += payload.SurfaceCount;
                    EncapsulateBounds(ref surfaceBounds, ref hasSurfaceBounds, payload.WorldBounds);
                }

                if (payload.HasUnderwater)
                {
                    totalUnderwaterCount += payload.UnderwaterCount;
                    EncapsulateBounds(ref underwaterBounds, ref hasUnderwaterBounds, payload.WorldBounds);
                }
            }

            _surfaceActiveCount = totalSurfaceCount;
            _underwaterActiveCount = totalUnderwaterCount;

            if (hasSurfaceBounds)
            {
                surfaceBounds.Expand(drawBoundsPadding);
                _surfaceDrawBounds = surfaceBounds;
            }
            else
            {
                _surfaceDrawBounds = default;
            }

            if (hasUnderwaterBounds)
            {
                underwaterBounds.Expand(drawBoundsPadding);
                _underwaterDrawBounds = underwaterBounds;
            }
            else
            {
                _underwaterDrawBounds = default;
            }

            if (totalSurfaceCount > 0)
            {
                EnsureMatrixCapacity(ref _surfaceAggregateMatrices, totalSurfaceCount);
                EnsureVegetationDataCapacity(ref _surfaceAggregateData, totalSurfaceCount);
                EnsureIntCapacity(ref _surfaceAggregateTypes, totalSurfaceCount);
                EnsureIntCapacity(ref _surfaceAggregateSemanticTypes, totalSurfaceCount);
                EnsureByteCapacity(ref _surfaceAggregateBiomeLayers, totalSurfaceCount);
                EnsureVector2Capacity(ref _surfaceAggregateFlowDirections, totalSurfaceCount);
                EnsureVector3Capacity(ref _surfaceAggregateFlowVectors, totalSurfaceCount);
                EnsureMatrixNativeCapacity(ref _surfaceAggregateMatricesNative, totalSurfaceCount);
                EnsureVegetationDataNativeCapacity(ref _surfaceAggregateDataNative, totalSurfaceCount);
                EnsureIntNativeCapacity(ref _surfaceAggregateTypesNative, totalSurfaceCount);
                EnsureIntNativeCapacity(ref _surfaceAggregateSemanticTypesNative, totalSurfaceCount);
                EnsureByteNativeCapacity(ref _surfaceAggregateBiomeLayersNative, totalSurfaceCount);
                EnsureVector2NativeCapacity(ref _surfaceAggregateFlowDirectionsNative, totalSurfaceCount);
                EnsureVector3NativeCapacity(ref _surfaceAggregateFlowVectorsNative, totalSurfaceCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasSurface)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.SurfaceCount;
                    CopyChunkSliceToAggregate(
                        _surfaceChunkPool,
                        payload.SurfaceOffset,
                        _surfaceAggregateMatrices,
                        _surfaceAggregateData,
                        _surfaceAggregateTypes,
                        _surfaceAggregateSemanticTypes,
                        _surfaceAggregateBiomeLayers,
                        _surfaceAggregateFlowDirections,
                        _surfaceAggregateFlowVectors,
                        _surfaceAggregateMatricesNative,
                        _surfaceAggregateDataNative,
                        _surfaceAggregateTypesNative,
                        _surfaceAggregateSemanticTypesNative,
                        _surfaceAggregateBiomeLayersNative,
                        _surfaceAggregateFlowDirectionsNative,
                        _surfaceAggregateFlowVectorsNative,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                UploadChannel(
                    surfaceRenderer,
                    ref _surfaceInstanceBuffer,
                    ref _surfaceInstanceDataBuffer,
                    _surfaceAggregateMatricesNative,
                    _surfaceAggregateDataNative,
                    totalSurfaceCount,
                    surfaceBounds);
            }
            else
            {
                ClearChannel(surfaceRenderer);
            }

            if (totalUnderwaterCount > 0)
            {
                EnsureMatrixCapacity(ref _underwaterAggregateMatrices, totalUnderwaterCount);
                EnsureVegetationDataCapacity(ref _underwaterAggregateData, totalUnderwaterCount);
                EnsureIntCapacity(ref _underwaterAggregateTypes, totalUnderwaterCount);
                EnsureIntCapacity(ref _underwaterAggregateSemanticTypes, totalUnderwaterCount);
                EnsureByteCapacity(ref _underwaterAggregateBiomeLayers, totalUnderwaterCount);
                EnsureVector2Capacity(ref _underwaterAggregateFlowDirections, totalUnderwaterCount);
                EnsureVector3Capacity(ref _underwaterAggregateFlowVectors, totalUnderwaterCount);
                EnsureMatrixNativeCapacity(ref _underwaterAggregateMatricesNative, totalUnderwaterCount);
                EnsureVegetationDataNativeCapacity(ref _underwaterAggregateDataNative, totalUnderwaterCount);
                EnsureIntNativeCapacity(ref _underwaterAggregateTypesNative, totalUnderwaterCount);
                EnsureIntNativeCapacity(ref _underwaterAggregateSemanticTypesNative, totalUnderwaterCount);
                EnsureByteNativeCapacity(ref _underwaterAggregateBiomeLayersNative, totalUnderwaterCount);
                EnsureVector2NativeCapacity(ref _underwaterAggregateFlowDirectionsNative, totalUnderwaterCount);
                EnsureVector3NativeCapacity(ref _underwaterAggregateFlowVectorsNative, totalUnderwaterCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.UnderwaterCount;
                    CopyChunkSliceToAggregate(
                        _underwaterChunkPool,
                        payload.UnderwaterOffset,
                        _underwaterAggregateMatrices,
                        _underwaterAggregateData,
                        _underwaterAggregateTypes,
                        _underwaterAggregateSemanticTypes,
                        _underwaterAggregateBiomeLayers,
                        _underwaterAggregateFlowDirections,
                        _underwaterAggregateFlowVectors,
                        _underwaterAggregateMatricesNative,
                        _underwaterAggregateDataNative,
                        _underwaterAggregateTypesNative,
                        _underwaterAggregateSemanticTypesNative,
                        _underwaterAggregateBiomeLayersNative,
                        _underwaterAggregateFlowDirectionsNative,
                        _underwaterAggregateFlowVectorsNative,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                UploadChannel(
                    underwaterRenderer,
                    ref _underwaterInstanceBuffer,
                    ref _underwaterInstanceDataBuffer,
                    _underwaterAggregateMatricesNative,
                    _underwaterAggregateDataNative,
                    totalUnderwaterCount,
                    underwaterBounds);
            }
            else
            {
                ClearChannel(underwaterRenderer);
            }
        }

        private bool ScheduleChunkBuild(TileRuntimeState state, ChunkKey key, long tileKey, byte grassLodTier)
        {
            if (state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out NativeArray<float> heightSamples) ||
                state.AlphamapResolution <= 0 ||
                state.HeightmapResolution <= 1)
            {
                return false;
            }

            ChunkPayload payloadHeader = CreateChunkPayloadHeader(state, key.ChunkX, key.ChunkZ);
            payloadHeader.GrassLodTier = grassLodTier;

            GetChunkBounds(state, key.ChunkX, key.ChunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float chunkWidth = math.max(0.01f, maxX - minX);
            float chunkDepth = math.max(0.01f, maxZ - minZ);
            float grassStep = GetGrassStepForTier(grassLodTier);
            int grassCountX = Mathf.Max(1, Mathf.CeilToInt(chunkWidth / grassStep));
            int grassCountZ = Mathf.Max(1, Mathf.CeilToInt(chunkDepth / grassStep));
            int kelpCountX = Mathf.Max(1, Mathf.CeilToInt(chunkWidth / kelpStepMeters));
            int kelpCountZ = Mathf.Max(1, Mathf.CeilToInt(chunkDepth / kelpStepMeters));
            int floatingCountX = Mathf.Max(1, Mathf.CeilToInt(chunkWidth / floatingStepMeters));
            int floatingCountZ = Mathf.Max(1, Mathf.CeilToInt(chunkDepth / floatingStepMeters));

            ChunkBuildJobState jobState = new ChunkBuildJobState
            {
                Key = key,
                TileKey = tileKey,
                TileCacheRevision = state.CacheRevision,
                GrassLodTier = grassLodTier,
                PayloadHeader = payloadHeader,
                GrassRecords = AllocateJobRecordArray(grassCountX * grassCountZ),
                FloatingRecords = AllocateJobRecordArray(floatingCountX * floatingCountZ),
                KelpRecords = AllocateJobRecordArray(kelpCountX * kelpCountZ)
            };

            float3 terrainPosition = new float3(state.TerrainPosition.x, state.TerrainPosition.y, state.TerrainPosition.z);
            float3 terrainSize = new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z);
            JobHandle grassHandle = default;
            JobHandle kelpHandle = default;
            JobHandle floatingHandle = default;

            if (jobState.GrassRecords.IsCreated && jobState.GrassRecords.Length > 0)
            {
                var grassJob = new GenerateAnchoredVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    Output = jobState.GrassRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / grassCountX,
                    StepZ = chunkDepth / grassCountZ,
                    SampleCountX = grassCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0,
                    JitterFraction = grassJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    NormalOffset = normalOffset,
                    MinWorldYExclusive = waterLevel,
                    MaxWorldYExclusive = float.MaxValue,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = grassScaleRange.x,
                    ScaleMax = grassScaleRange.y,
                    HeightScaleMin = 0.35f,
                    HeightScaleMax = 0.8f,
                    WidthScaleMin = 0.8f,
                    WidthScaleMax = 1.1f,
                    TypeId = (int)HectonVegetationInstanceType.Grass,
                    OrganicSemanticType = (int)VegetationSemanticType.OrganicGrass,
                    ColonyCableSemanticType = (int)VegetationSemanticType.ColonyCable,
                    ColonyHullSemanticType = (int)VegetationSemanticType.ColonyHullPlating,
                    ColonyBeamSemanticType = (int)VegetationSemanticType.ColonySupportBeam,
                    DeadZoneSemanticType = (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    WaterLevel = waterLevel,
                    ColonyBiomeStartDepth = colonyBiomeStartDepth,
                    DeadZoneStartDepth = deadZoneStartDepth,
                    VerticalBiomeBlendBand = verticalBiomeBlendBand,
                    TechnoJungleThreshold = technoJungleThreshold,
                    TechnoJungleCellSize = technoJungleCellSize,
                    TechnoJungleSecondaryCellSize = technoJungleSecondaryCellSize,
                    TechnoJungleWallWidth = technoJungleWallWidth,
                    TechnoJungleWarpMeters = technoJungleWarpMeters,
                    TechnoJungleFlowAnisotropy = technoJungleFlowAnisotropy,
                    DeadZoneStructureChance = deadZoneStructureChance,
                    DeadZoneDensityScale = deadZoneDensityScale,
                    AbyssalFlowNoiseScale = abyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength = abyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength = abyssalFlowVerticalStrength,
                    EnableVerticalBiomeRewrite = 0,
                    ScaleSalt = 0x85EBCA6Bu,
                    WidthSalt = 0xC2B2AE35u,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0xA24BAEDCu
                };

                grassHandle = grassJob.Schedule(jobState.GrassRecords.Length, DefaultJobBatchSize);
            }

            if (jobState.KelpRecords.IsCreated && jobState.KelpRecords.Length > 0)
            {
                var kelpJob = new GenerateAnchoredVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    Output = jobState.KelpRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / kelpCountX,
                    StepZ = chunkDepth / kelpCountZ,
                    SampleCountX = kelpCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0x4000,
                    JitterFraction = kelpJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    NormalOffset = normalOffset,
                    MinWorldYExclusive = float.NegativeInfinity,
                    MaxWorldYExclusive = waterLevel,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = kelpScaleRange.x,
                    ScaleMax = kelpScaleRange.y,
                    HeightScaleMin = 0.25f,
                    HeightScaleMax = 1f,
                    WidthScaleMin = 0.65f,
                    WidthScaleMax = 1.1f,
                    TypeId = (int)HectonVegetationInstanceType.GiantKelp,
                    OrganicSemanticType = (int)VegetationSemanticType.OrganicKelp,
                    ColonyCableSemanticType = (int)VegetationSemanticType.ColonyCable,
                    ColonyHullSemanticType = (int)VegetationSemanticType.ColonyHullPlating,
                    ColonyBeamSemanticType = (int)VegetationSemanticType.ColonySupportBeam,
                    DeadZoneSemanticType = (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    WaterLevel = waterLevel,
                    ColonyBiomeStartDepth = colonyBiomeStartDepth,
                    DeadZoneStartDepth = deadZoneStartDepth,
                    VerticalBiomeBlendBand = verticalBiomeBlendBand,
                    TechnoJungleThreshold = technoJungleThreshold,
                    TechnoJungleCellSize = technoJungleCellSize,
                    TechnoJungleSecondaryCellSize = technoJungleSecondaryCellSize,
                    TechnoJungleWallWidth = technoJungleWallWidth,
                    TechnoJungleWarpMeters = technoJungleWarpMeters,
                    TechnoJungleFlowAnisotropy = technoJungleFlowAnisotropy,
                    DeadZoneStructureChance = deadZoneStructureChance,
                    DeadZoneDensityScale = deadZoneDensityScale,
                    AbyssalFlowNoiseScale = abyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength = abyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength = abyssalFlowVerticalStrength,
                    EnableVerticalBiomeRewrite = 1,
                    ScaleSalt = 0x27D4EB2Fu,
                    WidthSalt = 0x165667B1u,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0x94D049BBu
                };

                kelpHandle = kelpJob.Schedule(jobState.KelpRecords.Length, DefaultJobBatchSize);
            }

            if (jobState.FloatingRecords.IsCreated && jobState.FloatingRecords.Length > 0)
            {
                var floatingJob = new GenerateFloatingVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    Output = jobState.FloatingRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / floatingCountX,
                    StepZ = chunkDepth / floatingCountZ,
                    SampleCountX = floatingCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0x8000,
                    JitterFraction = floatingJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    WaterLevel = waterLevel,
                    FloatingSurfaceOffset = floatingSurfaceOffset,
                    FloatingSurfaceBand = floatingSurfaceBand,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = floatingScaleRange.x,
                    ScaleMax = floatingScaleRange.y,
                    FloatingPatchThreshold = floatingPatchThreshold,
                    FloatingPatchNoiseScale = floatingPatchNoiseScale,
                    FloatingCellSize = floatingCellSize,
                    FloatingSecondaryCellSize = floatingSecondaryCellSize,
                    FloatingWallWidth = floatingWallWidth,
                    FloatingWarpMeters = floatingWarpMeters,
                    FloatingFlowDirection = new float2(_floatingFlowDirectionNormalized.x, _floatingFlowDirectionNormalized.y),
                    FloatingFlowAnisotropy = floatingFlowAnisotropy,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0xC13FA9A9u
                };

                floatingHandle = floatingJob.Schedule(jobState.FloatingRecords.Length, DefaultJobBatchSize);
            }

            jobState.Handle = JobHandle.CombineDependencies(grassHandle, kelpHandle, floatingHandle);
            _chunkBuildJobs[key] = jobState;
            return true;
        }

        private int FinalizeCompletedChunkBuilds()
        {
            if (_chunkBuildJobs.Count == 0)
                return 0;

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator enumerator = _chunkBuildJobs.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkBuildJobState jobState = enumerator.Current.Value;
                if (jobState != null && jobState.Handle.IsCompleted)
                    _jobScratchKeys.Add(enumerator.Current.Key);
            }

            int completedCount = 0;
            for (int i = 0; i < _jobScratchKeys.Count; i++)
            {
                ChunkKey key = _jobScratchKeys[i];
                if (!_chunkBuildJobs.TryGetValue(key, out ChunkBuildJobState jobState) || jobState == null)
                    continue;

                jobState.Handle.Complete();
                if (IsJobStateCurrent(jobState))
                {
                    ReleaseChunkPayloadStorage(key);
                    ChunkPayload payload = BuildChunkPayloadFromJob(jobState);
                    _chunkPayloads[key] = payload;
                    RegisterChunkPayloadStorage(payload);
                    completedCount++;
                }
                else if (TryGetDesiredChunkPriority(key, out float priority))
                {
                    EnqueuePendingChunk(key, priority);
                }

                DisposeJobState(jobState);
                _chunkBuildJobs.Remove(key);
            }

            if (completedCount > 0)
                _activeSetDirty = true;

            return completedCount;
        }

        private bool IsJobStateCurrent(ChunkBuildJobState jobState)
        {
            if (jobState == null)
                return false;

            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) || state == null)
                return false;

            return state.CacheRevision == jobState.TileCacheRevision;
        }

        private ChunkPayload BuildChunkPayloadFromJob(ChunkBuildJobState jobState)
        {
            ChunkPayload payload = jobState.PayloadHeader;
            payload.GrassLodTier = jobState.GrassLodTier;

            int grassCount = CountValidRecords(jobState.GrassRecords);
            int floatingCount = CountValidRecords(jobState.FloatingRecords);
            int kelpCount = CountValidRecords(jobState.KelpRecords);
            int surfaceCount = grassCount + floatingCount;

            if (surfaceCount > 0)
            {
                if (TryAllocateChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, surfaceCount, out int surfaceOffset))
                {
                    payload.SurfaceOffset = surfaceOffset;
                    payload.SurfaceCount = surfaceCount;
                    payload.SurfaceEdgeOffset = surfaceOffset;
                    int writeIndex = surfaceOffset;
                    WriteJobRecordsToPool(jobState.GrassRecords, ref _surfaceChunkPool, ref writeIndex);
                    WriteJobRecordsToPool(jobState.FloatingRecords, ref _surfaceChunkPool, ref writeIndex);
                }
            }

            if (kelpCount > 0)
            {
                if (TryAllocateChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, kelpCount, out int underwaterOffset))
                {
                    payload.UnderwaterOffset = underwaterOffset;
                    payload.UnderwaterCount = kelpCount;
                    payload.UnderwaterEdgeOffset = underwaterOffset;
                    int writeIndex = underwaterOffset;
                    WriteJobRecordsToPool(jobState.KelpRecords, ref _underwaterChunkPool, ref writeIndex);
                }
            }

            return payload;
        }

        private void RegisterChunkPayloadStorage(ChunkPayload payload)
        {
            _chunkPayloadUsedBytes += GetChunkPayloadStorageBytes(payload);
        }

        private static int CountValidRecords(NativeArray<JobInstanceRecord> records)
        {
            if (!records.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].IsValid != 0)
                    count++;
            }

            return count;
        }

        private static void WriteJobRecordsToPool(
            NativeArray<JobInstanceRecord> source,
            ref NativeChunkPool pool,
            ref int writeIndex)
        {
            if (!source.IsCreated)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                JobInstanceRecord record = source[i];
                if (record.IsValid == 0)
                    continue;

                pool.Matrices[writeIndex] = ToMatrix4x4(record.Matrix);
                pool.Metadata[writeIndex] = new HectonVegetationInstanceData(
                    (HectonVegetationInstanceType)record.Type,
                    record.HeightScale,
                    record.WidthScale,
                    record.Variation);
                pool.Types[writeIndex] = record.Type;
                pool.SemanticTypes[writeIndex] = record.SemanticType;
                pool.BiomeLayers[writeIndex] = record.BiomeLayer;
                pool.EdgeDistances[writeIndex] = record.EdgeDistance;
                pool.FlowDirections[writeIndex] = new Vector2(record.FlowDirection.x, record.FlowDirection.y);
                pool.FlowVectors[writeIndex] = new Vector3(record.FlowVector.x, record.FlowVector.y, record.FlowVector.z);
                writeIndex++;
            }
        }

        private static void CopyChunkSliceToAggregate(
            NativeChunkPool pool,
            int sourceOffset,
            Matrix4x4[] managedMatrices,
            HectonVegetationInstanceData[] managedMetadata,
            int[] managedTypes,
            int[] managedSemanticTypes,
            byte[] managedBiomeLayers,
            Vector2[] managedFlowDirections,
            Vector3[] managedFlowVectors,
            NativeArray<Matrix4x4> nativeMatrices,
            NativeArray<HectonVegetationInstanceData> nativeMetadata,
            NativeArray<int> nativeTypes,
            NativeArray<int> nativeSemanticTypes,
            NativeArray<byte> nativeBiomeLayers,
            NativeArray<Vector2> nativeFlowDirections,
            NativeArray<Vector3> nativeFlowVectors,
            int destinationOffset,
            int copyCount)
        {
            NativeArray<Matrix4x4>.Copy(pool.Matrices, sourceOffset, nativeMatrices, destinationOffset, copyCount);
            NativeArray<HectonVegetationInstanceData>.Copy(pool.Metadata, sourceOffset, nativeMetadata, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.Types, sourceOffset, nativeTypes, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.SemanticTypes, sourceOffset, nativeSemanticTypes, destinationOffset, copyCount);
            NativeArray<byte>.Copy(pool.BiomeLayers, sourceOffset, nativeBiomeLayers, destinationOffset, copyCount);
            NativeArray<Vector2>.Copy(pool.FlowDirections, sourceOffset, nativeFlowDirections, destinationOffset, copyCount);
            NativeArray<Vector3>.Copy(pool.FlowVectors, sourceOffset, nativeFlowVectors, destinationOffset, copyCount);
            CopyNativeToManaged(pool.Matrices, sourceOffset, managedMatrices, destinationOffset, copyCount);
            CopyNativeToManaged(pool.Metadata, sourceOffset, managedMetadata, destinationOffset, copyCount);
            CopyNativeToManaged(pool.Types, sourceOffset, managedTypes, destinationOffset, copyCount);
            CopyNativeToManaged(pool.SemanticTypes, sourceOffset, managedSemanticTypes, destinationOffset, copyCount);
            CopyNativeToManaged(pool.BiomeLayers, sourceOffset, managedBiomeLayers, destinationOffset, copyCount);
            CopyNativeToManaged(pool.FlowDirections, sourceOffset, managedFlowDirections, destinationOffset, copyCount);
            CopyNativeToManaged(pool.FlowVectors, sourceOffset, managedFlowVectors, destinationOffset, copyCount);
        }

        private void RebuildDensityQuerySnapshot()
        {
            if (_selectedChunkCount <= 0)
            {
                _densityQueryChunkCount = 0;
                return;
            }

            EnsureDensityQueryCapacity(_selectedChunkCount);
            _densityQueryChunkLookup.Clear();
            for (int i = 0; i < _densityQueryChunkCount; i++)
                _densityQueryChunkLookup[_densityQueryChunkKeys[i]] = i;

            int nextChunkCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                int gridOffset = nextChunkCount * DensityGridCellCount;
                ClearDensityGridCells(_densityQueryGridScratchNative, gridOffset, DensityGridCellCount);
                AccumulateChunkDensityGrid(payload, ref _densityQueryGridScratchNative, gridOffset);

                VegetationDensityChunkRecord record = new VegetationDensityChunkRecord
                {
                    MinX = payload.MinX,
                    MaxX = payload.MaxX,
                    MinZ = payload.MinZ,
                    MaxZ = payload.MaxZ,
                    GridOffset = gridOffset,
                    GrassLodTier = payload.GrassLodTier
                };

                if (_densityQueryChunkLookup.TryGetValue(key, out int previousIndex))
                {
                    VegetationDensityChunkRecord previousRecord = _densityQueryChunksNative[previousIndex];
                    if (previousRecord.GrassLodTier != payload.GrassLodTier)
                        BlendDensityGrid(_densityQueryGridNative, previousRecord.GridOffset, _densityQueryGridScratchNative, gridOffset, DensityGridCellCount, 0.35f);
                }

                _densityQueryChunksScratchNative[nextChunkCount] = record;
                _densityQueryChunkKeys[nextChunkCount] = key;
                nextChunkCount++;
            }

            SwapDensityQueryBuffers();
            for (int i = nextChunkCount; i < _densityQueryChunkCount; i++)
                _densityQueryChunkKeys[i] = default;

            _densityQueryChunkCount = nextChunkCount;
        }

        private void AccumulateChunkDensityGrid(ChunkPayload payload, ref NativeArray<float3> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.SurfaceCount > 0)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkDensityGridFromSlice(
                    _surfaceChunkPool,
                    payload.SurfaceOffset,
                    payload.SurfaceCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassArea,
                    kelpStepMeters * kelpStepMeters,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }

            if (payload.UnderwaterCount > 0)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkDensityGridFromSlice(
                    _underwaterChunkPool,
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    payload.MinX,
                    payload.MaxX,
                    payload.MinZ,
                    payload.MaxZ,
                    safeCellArea,
                    grassStepMeters * grassStepMeters,
                    kelpArea,
                    floatingStepMeters * floatingStepMeters,
                    ref destination,
                    gridOffset);
            }
        }

        private void AccumulateChunkDensityGridFromSlice(
            NativeChunkPool pool,
            int offset,
            int count,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float cellArea,
            float grassRepresentedArea,
            float kelpRepresentedArea,
            float sargassumRepresentedArea,
            ref NativeArray<float3> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            float inverseWidth = 1f / width;
            float inverseDepth = 1f / depth;
            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                Matrix4x4 matrix = pool.Matrices[poolIndex];
                float x = matrix.m03;
                float z = matrix.m23;
                if (x < minX || x > maxX || z < minZ || z > maxZ)
                    continue;

                int type = pool.Types[poolIndex];
                float normalizedX = Mathf.Clamp01((x - minX) * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = Mathf.Clamp01((z - minZ) * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(pool.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float3 channel = ResolveDensityChannel(type, densityWeight);
                AddDensityCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddDensityCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddDensityCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
            }
        }

        private static float ResolveRepresentedArea(int type, float grassArea, float kelpArea, float sargassumArea)
        {
            switch ((HectonVegetationInstanceType)type)
            {
                case HectonVegetationInstanceType.Grass:
                    return grassArea;
                case HectonVegetationInstanceType.GiantKelp:
                    return kelpArea;
                case HectonVegetationInstanceType.Sargassum:
                    return sargassumArea;
                default:
                    return grassArea;
            }
        }

        private float ResolveEdgeCompensation(float edgeDistance)
        {
            if (edgeDitherDistance <= 0f || edgeDistance >= edgeDitherDistance)
                return 1f;

            float normalized = Mathf.Clamp01(edgeDistance / Mathf.Max(0.01f, edgeDitherDistance));
            return 1f / Mathf.Max(0.35f, normalized);
        }

        private static float3 ResolveDensityChannel(int type, float densityWeight)
        {
            switch ((HectonVegetationInstanceType)type)
            {
                case HectonVegetationInstanceType.Grass:
                    return new float3(densityWeight, 0f, 0f);
                case HectonVegetationInstanceType.GiantKelp:
                    return new float3(0f, densityWeight, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new float3(0f, 0f, densityWeight);
                default:
                    return float3.zero;
            }
        }

        private float EvaluateVisibilityModifier(Vector3 position, float3 densityChannels)
        {
            return EvaluateVisibilityModifierStatic(
                position.y,
                densityChannels,
                grassVisibilityWeight,
                kelpVisibilityWeight,
                sargassumVisibilityWeight,
                waterLevel,
                floatingSurfaceOffset,
                sargassumVisibilityBand);
        }

        private float3 ResolveFallbackVisibilityChannels(Vector3 position, HectonVegetationInstanceType type)
        {
            switch (type)
            {
                case HectonVegetationInstanceType.Grass:
                    return new float3(0.18f, 0f, 0f);
                case HectonVegetationInstanceType.GiantKelp:
                    return new float3(0f, 0.24f, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new float3(0f, 0f, 0.28f * EvaluateSargassumVerticalConcealment(position.y));
                default:
                    return float3.zero;
            }
        }

        private float EvaluateSargassumVerticalConcealment(float worldY)
        {
            return EvaluateSargassumVerticalConcealmentStatic(worldY, waterLevel, floatingSurfaceOffset, sargassumVisibilityBand);
        }

        private static float EvaluateVisibilityModifierStatic(
            float worldY,
            float3 densityChannels,
            float grassWeight,
            float kelpWeight,
            float sargassumWeight,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            float grassCover = math.saturate(densityChannels.x * grassWeight);
            float kelpCover = math.saturate(densityChannels.y * kelpWeight);
            float verticalConcealment = EvaluateSargassumVerticalConcealmentStatic(
                worldY,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
            float sargassumCover = math.saturate(densityChannels.z * sargassumWeight * verticalConcealment);
            float combinedDensity = grassCover + kelpCover + sargassumCover;
            return math.saturate(1f - math.exp(-combinedDensity));
        }

        private static float EvaluateSargassumVerticalConcealmentStatic(
            float worldY,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            float canopyY = localWaterLevel + localFloatingSurfaceOffset;
            if (worldY > canopyY)
                return 0.12f;

            float band = math.max(0.25f, localSargassumVisibilityBand);
            float canopyDepth = canopyY - worldY;
            return math.saturate(1f - (canopyDepth / band));
        }

        private static void AddDensityCell(ref NativeArray<float3> destination, int gridOffset, int cellX, int cellZ, float3 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void ClearDensityGridCells(NativeArray<float3> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float3.zero;
        }

        private static void BlendDensityGrid(
            NativeArray<float3> previous,
            int previousOffset,
            NativeArray<float3> current,
            int currentOffset,
            int count,
            float previousWeight)
        {
            float currentWeight = 1f - previousWeight;
            for (int i = 0; i < count; i++)
                current[currentOffset + i] = (previous[previousOffset + i] * previousWeight) + (current[currentOffset + i] * currentWeight);
        }

        private void SwapDensityQueryBuffers()
        {
            NativeArray<VegetationDensityChunkRecord> chunkSwap = _densityQueryChunksNative;
            _densityQueryChunksNative = _densityQueryChunksScratchNative;
            _densityQueryChunksScratchNative = chunkSwap;

            NativeArray<float3> gridSwap = _densityQueryGridNative;
            _densityQueryGridNative = _densityQueryGridScratchNative;
            _densityQueryGridScratchNative = gridSwap;
        }

        private static float SampleDensityAtPosition(
            float3 position,
            int typeMask,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return ApplyDensityTypeMask(SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount), typeMask);
        }

        private static float3 SampleDensityChannelsAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            float3 density = float3.zero;
            for (int i = 0; i < chunkCount; i++)
            {
                VegetationDensityChunkRecord chunk = chunks[i];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                density += SampleChunkDensityChannels(position.x, position.z, chunk, densityGrid);
            }

            return density;
        }

        private static float3 SampleChunkDensityChannels(
            float worldX,
            float worldZ,
            VegetationDensityChunkRecord chunk,
            NativeArray<float3> densityGrid)
        {
            float width = math.max(0.01f, chunk.MaxX - chunk.MinX);
            float depth = math.max(0.01f, chunk.MaxZ - chunk.MinZ);
            float normalizedX = math.saturate((worldX - chunk.MinX) / width) * (DensityGridResolution - 1);
            float normalizedZ = math.saturate((worldZ - chunk.MinZ) / depth) * (DensityGridResolution - 1);
            int cellX = math.clamp((int)math.floor(normalizedX), 0, DensityGridResolution - 1);
            int cellZ = math.clamp((int)math.floor(normalizedZ), 0, DensityGridResolution - 1);
            int nextCellX = math.min(cellX + 1, DensityGridResolution - 1);
            int nextCellZ = math.min(cellZ + 1, DensityGridResolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float3 sample00 = densityGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + cellX];
            float3 sample10 = densityGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + nextCellX];
            float3 sample01 = densityGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + cellX];
            float3 sample11 = densityGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + nextCellX];
            float3 bottom = math.lerp(sample00, sample10, fracX);
            float3 top = math.lerp(sample01, sample11, fracX);
            return math.lerp(bottom, top, fracZ);
        }

        private static float ApplyDensityTypeMask(float3 sample, int typeMask)
        {
            float density = 0f;
            if ((typeMask & DensityTypeMaskGrass) != 0)
                density += sample.x;
            if ((typeMask & DensityTypeMaskKelp) != 0)
                density += sample.y;
            if ((typeMask & DensityTypeMaskSargassum) != 0)
                density += sample.z;

            return density;
        }

        private bool TryBuildDensitySample(
            Vector3 positionWS,
            float3 densityChannels,
            out VegetationDensitySample sample)
        {
            if (TryResolveDominantDensitySample(densityChannels, out HectonVegetationInstanceType type, out float density))
            {
                uint seed = ResolveWorldQuerySeed(positionWS);
                VegetationBiomeLayer biomeLayer = ResolveBiomeLayer(positionWS.y, seed);
                sample = new VegetationDensitySample(
                    true,
                    type,
                    ResolveSemanticType(type, biomeLayer, seed),
                    biomeLayer,
                    ResolveAcousticType(type, density),
                    density);
                return true;
            }

            sample = default;
            return false;
        }

        private bool TryResolveDominantDensitySample(
            float3 densityChannels,
            out HectonVegetationInstanceType type,
            out float density)
        {
            density = math.max(densityChannels.x, math.max(densityChannels.y, densityChannels.z));
            if (density <= 0f)
            {
                type = HectonVegetationInstanceType.Grass;
                return false;
            }

            if (densityChannels.z >= densityChannels.x && densityChannels.z >= densityChannels.y)
            {
                type = HectonVegetationInstanceType.Sargassum;
            }
            else if (densityChannels.y >= densityChannels.x)
            {
                type = HectonVegetationInstanceType.GiantKelp;
            }
            else
            {
                type = HectonVegetationInstanceType.Grass;
            }

            return true;
        }

        private static VegetationAcousticType ResolveAcousticType(HectonVegetationInstanceType type, float density)
        {
            if (density <= 0f)
                return VegetationAcousticType.Silence;

            return type == HectonVegetationInstanceType.Sargassum
                ? VegetationAcousticType.SargassumBubbles
                : VegetationAcousticType.VegetationRustle;
        }

        private uint ResolveWorldQuerySeed(Vector3 positionWS)
        {
            if (TryFindTileStateAtPosition(positionWS, out TileRuntimeState state) && state != null)
                return BuildDensityQuerySeed(state.TileX, state.TileZ, positionWS.x, positionWS.z);

            return BuildArbitraryWorldSeed(positionWS.x, positionWS.y, positionWS.z);
        }

        private VegetationBiomeLayer ResolveBiomeLayer(float worldY, uint seed)
        {
            float depth = math.max(0f, waterLevel - worldY);
            float halfBand = math.max(1f, verticalBiomeBlendBand * 0.5f);
            float firstBlendStart = colonyBiomeStartDepth - halfBand;
            float firstBlendEnd = colonyBiomeStartDepth + halfBand;
            if (depth <= firstBlendStart)
                return VegetationBiomeLayer.OrganicShelf;

            if (depth < firstBlendEnd)
            {
                float transition = math.saturate((depth - firstBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0x6E624EB7u) < transition
                    ? VegetationBiomeLayer.ColonyGraveyard
                    : VegetationBiomeLayer.OrganicShelf;
            }

            float secondBlendStart = deadZoneStartDepth - halfBand;
            float secondBlendEnd = deadZoneStartDepth + halfBand;
            if (depth <= secondBlendStart)
                return VegetationBiomeLayer.ColonyGraveyard;

            if (depth < secondBlendEnd)
            {
                float transition = math.saturate((depth - secondBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0xB5297A4Du) < transition
                    ? VegetationBiomeLayer.DeadZone
                    : VegetationBiomeLayer.ColonyGraveyard;
            }

            return VegetationBiomeLayer.DeadZone;
        }

        private static VegetationSemanticType ResolveSemanticType(
            HectonVegetationInstanceType renderType,
            VegetationBiomeLayer biomeLayer,
            uint seed)
        {
            switch (renderType)
            {
                case HectonVegetationInstanceType.Grass:
                    return VegetationSemanticType.OrganicGrass;
                case HectonVegetationInstanceType.Sargassum:
                    return VegetationSemanticType.FloatingSargassum;
                case HectonVegetationInstanceType.GiantKelp:
                    switch (biomeLayer)
                    {
                        case VegetationBiomeLayer.ColonyGraveyard:
                        {
                            float selector = Hash01(seed ^ 0x165667B1u);
                            if (selector < 0.34f)
                                return VegetationSemanticType.ColonyCable;
                            if (selector < 0.67f)
                                return VegetationSemanticType.ColonyHullPlating;

                            return VegetationSemanticType.ColonySupportBeam;
                        }
                        case VegetationBiomeLayer.DeadZone:
                            return VegetationSemanticType.DeadZoneMassiveStructure;
                        default:
                            return VegetationSemanticType.OrganicKelp;
                    }
                default:
                    return VegetationSemanticType.OrganicGrass;
            }
        }

        private void UpdateVegetationAudioHandoff()
        {
            if (playerTransform == null)
            {
                PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: false);
                return;
            }

            float3 averagedChannels = SampleVegetationAudioDensity(playerTransform.position);
            float totalDensity = math.saturate(averagedChannels.x + averagedChannels.y + averagedChannels.z);
            VegetationAcousticType acousticType = VegetationAcousticType.Silence;

            if (TryResolveDominantDensitySample(averagedChannels, out HectonVegetationInstanceType dominantType, out float dominantDensity))
                acousticType = ResolveAcousticType(dominantType, dominantDensity);

            PublishVegetationAudioHandoff(totalDensity, acousticType, force: false);
        }

        private float3 SampleVegetationAudioDensity(Vector3 origin)
        {
            if (!_densityQueryChunksNative.IsCreated || !_densityQueryGridNative.IsCreated || _densityQueryChunkCount <= 0)
                return float3.zero;

            Vector3 forward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float3 sum = float3.zero;

            sum += SampleDensityChannelsAtPosition(new float3(origin.x, origin.y, origin.z), _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount);
            Vector3 offset = forward * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount);
            offset = right * vegetationAudioProbeRadius;
            sum += SampleDensityChannelsAtPosition(new float3(origin.x + offset.x, origin.y + offset.y, origin.z + offset.z), _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount);
            sum += SampleDensityChannelsAtPosition(new float3(origin.x - offset.x, origin.y - offset.y, origin.z - offset.z), _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount);

            return sum / (float)VegetationAudioProbeCount;
        }

        private void PublishVegetationAudioHandoff(float density, VegetationAcousticType acousticType, bool force)
        {
            _vegetationAudioDensity = Mathf.Clamp01(density);
            _vegetationAudioAcousticType = acousticType;
            GlobalVegetationAudioDensity = _vegetationAudioDensity;
            GlobalVegetationAcousticType = acousticType;

            Shader.SetGlobalFloat(_ShaderVegetationAudioDensityId, _vegetationAudioDensity);
            Shader.SetGlobalFloat(_ShaderVegetationAudioAcousticTypeId, (float)acousticType);

            if (!force &&
                Mathf.Abs(_lastPublishedVegetationAudioDensity - _vegetationAudioDensity) <= 0.01f &&
                _lastPublishedVegetationAudioAcousticType == acousticType)
            {
                return;
            }

            _lastPublishedVegetationAudioDensity = _vegetationAudioDensity;
            _lastPublishedVegetationAudioAcousticType = acousticType;

            if (vegetationAudioMixer == null)
                return;

            if (!string.IsNullOrEmpty(vegetationDensityMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, _vegetationAudioDensity);

            if (!string.IsNullOrEmpty(vegetationAcousticTypeMixerParameter))
                vegetationAudioMixer.SetFloat(vegetationAcousticTypeMixerParameter, (float)acousticType);
        }

        private void ClearVegetationAudioHandoff()
        {
            PublishVegetationAudioHandoff(0f, VegetationAcousticType.Silence, force: true);
        }

        private void LogNativePoolFragmentationIfDue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.unscaledTime < _nextNativePoolFragmentationLogTime)
                return;

            _nextNativePoolFragmentationLogTime = Time.unscaledTime + 30f;
            Debug.Log(
                $"[HectonMapMagicVegetationBridge] NativePoolFragmentationPercent={NativePoolFragmentationPercent:0.0} UsedBytes={_chunkPayloadUsedBytes} GuardBytes={ChunkPayloadGuardBytes}",
                this);
#endif
        }

        private bool TryResolveVegetationTypeFromCachedMasks(Vector3 positionWS, out HectonVegetationInstanceType type)
        {
            type = HectonVegetationInstanceType.Grass;
            if (!TryFindTileStateAtPosition(positionWS, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out NativeArray<float> heightSamples))
            {
                return false;
            }

            uint seed = BuildDensityQuerySeed(state.TileX, state.TileZ, positionWS.x, positionWS.z);
            if (!TrySampleTerrainPlacement(
                    positionWS.x,
                    positionWS.z,
                    seed,
                    new float3(state.TerrainPosition.x, state.TerrainPosition.y, state.TerrainPosition.z),
                    new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z),
                    state.AlphamapResolution,
                    state.HeightmapResolution,
                    _sandMaskThresholdByte,
                    _rockMaskThresholdByte,
                    minimumNormalY,
                    sandMask,
                    rockMask,
                    heightSamples,
                    out float worldY,
                    out _,
                    out _))
            {
                return false;
            }

            if (math.abs(worldY - waterLevel) <= floatingSurfaceBand &&
                TryEvaluateFloatingLabyrinth(
                    positionWS.x,
                    positionWS.z,
                    seed,
                    floatingPatchThreshold,
                    floatingPatchNoiseScale,
                    floatingCellSize,
                    floatingSecondaryCellSize,
                    floatingWallWidth,
                    floatingWarpMeters,
                    new float2(_floatingFlowDirectionNormalized.x, _floatingFlowDirectionNormalized.y),
                    floatingFlowAnisotropy,
                    out _))
            {
                type = HectonVegetationInstanceType.Sargassum;
                return true;
            }

            if (worldY > waterLevel)
            {
                type = HectonVegetationInstanceType.Grass;
                return true;
            }

            VegetationBiomeLayer biomeLayer = ResolveBiomeLayer(worldY, seed);
            if (biomeLayer == VegetationBiomeLayer.ColonyGraveyard)
            {
                if (TryEvaluateTechnoJungle(
                        positionWS.x,
                        positionWS.z,
                        seed,
                        new float2(0f, 1f),
                        technoJungleThreshold,
                        technoJungleCellSize,
                        technoJungleSecondaryCellSize,
                        technoJungleWallWidth,
                        technoJungleWarpMeters,
                        technoJungleFlowAnisotropy,
                        out _))
                {
                    type = HectonVegetationInstanceType.GiantKelp;
                    return true;
                }

                return false;
            }

            if (biomeLayer == VegetationBiomeLayer.DeadZone)
            {
                if (TryEvaluateTechnoJungle(
                        positionWS.x,
                        positionWS.z,
                        seed ^ 0x51ED270Bu,
                        new float2(0f, 1f),
                        technoJungleThreshold,
                        technoJungleCellSize * 1.6f,
                        technoJungleSecondaryCellSize * 1.35f,
                        technoJungleWallWidth * 1.4f,
                        technoJungleWarpMeters * 0.8f,
                        Mathf.Max(0.2f, technoJungleFlowAnisotropy * 0.7f),
                        out float deadZoneOccupancy))
                {
                    float keepChance = Mathf.Clamp01(deadZoneDensityScale * Mathf.Max(deadZoneStructureChance, deadZoneOccupancy));
                    if (Hash01(seed ^ 0xC13FA9A9u) <= keepChance)
                    {
                        type = HectonVegetationInstanceType.GiantKelp;
                        return true;
                    }
                }

                return false;
            }

            type = HectonVegetationInstanceType.GiantKelp;
            return true;
        }

        private static uint BuildDensityQuerySeed(int tileX, int tileZ, float worldX, float worldZ)
        {
            int sampleX = Mathf.RoundToInt(worldX * DensityQuerySeedScale);
            int sampleZ = Mathf.RoundToInt(worldZ * DensityQuerySeedScale);
            return BuildSampleSeed(tileX, tileZ, sampleX, sampleZ);
        }

        private static uint BuildArbitraryWorldSeed(float worldX, float worldY, float worldZ)
        {
            int sampleX = Mathf.RoundToInt(worldX * DensityQuerySeedScale);
            int sampleY = Mathf.RoundToInt(worldY * 0.25f);
            int sampleZ = Mathf.RoundToInt(worldZ * DensityQuerySeedScale);
            return BuildSampleSeed(sampleX, sampleY, sampleZ, sampleX ^ sampleZ);
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 value)
        {
            return new Matrix4x4(
                new Vector4(value.c0.x, value.c0.y, value.c0.z, value.c0.w),
                new Vector4(value.c1.x, value.c1.y, value.c1.z, value.c1.w),
                new Vector4(value.c2.x, value.c2.y, value.c2.z, value.c2.w),
                new Vector4(value.c3.x, value.c3.y, value.c3.z, value.c3.w));
        }

        private static NativeArray<JobInstanceRecord> AllocateJobRecordArray(int count)
        {
            if (count <= 0)
                return default;

            return new NativeArray<JobInstanceRecord>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static Matrix4x4[] AllocateMatrixArray(int count)
        {
            // COLD ALLOC: Matrix4x4[count] - immutable chunk matrix payload finalized from Burst job output - owner: HectonMapMagicVegetationBridge
            return new Matrix4x4[count];
        }

        private static HectonVegetationInstanceData[] AllocateVegetationDataArray(int count)
        {
            // COLD ALLOC: HectonVegetationInstanceData[count] - immutable chunk metadata payload finalized from Burst job output - owner: HectonMapMagicVegetationBridge
            return new HectonVegetationInstanceData[count];
        }

        private static int[] AllocateIntArray(int count)
        {
            // COLD ALLOC: int[count] - immutable chunk vegetation-type payload finalized from Burst job output - owner: HectonMapMagicVegetationBridge
            return new int[count];
        }

        private void CancelChunkBuildJob(ChunkKey key)
        {
            if (!_chunkBuildJobs.TryGetValue(key, out ChunkBuildJobState jobState) || jobState == null)
                return;

            jobState.Handle.Complete();
            DisposeJobState(jobState);
            _chunkBuildJobs.Remove(key);
        }

        private void DisposeAllChunkBuildJobs()
        {
            if (_chunkBuildJobs.Count == 0)
                return;

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator enumerator = _chunkBuildJobs.GetEnumerator();
            while (enumerator.MoveNext())
                _jobScratchKeys.Add(enumerator.Current.Key);

            for (int i = 0; i < _jobScratchKeys.Count; i++)
                CancelChunkBuildJob(_jobScratchKeys[i]);
        }

        private static void DisposeJobState(ChunkBuildJobState jobState)
        {
            if (jobState == null)
                return;

            DisposeNativeArray(ref jobState.GrassRecords);
            DisposeNativeArray(ref jobState.FloatingRecords);
            DisposeNativeArray(ref jobState.KelpRecords);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct GenerateAnchoredVegetationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> SandMask;
            [ReadOnly] public NativeArray<byte> RockMask;
            [ReadOnly] public NativeArray<float> HeightSamples;
            public NativeArray<JobInstanceRecord> Output;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int AlphamapResolution;
            public int HeightResolution;
            public float MinX;
            public float MinZ;
            public float MaxX;
            public float MaxZ;
            public float StepX;
            public float StepZ;
            public int SampleCountX;
            public int TileX;
            public int TileZ;
            public int ChunkX;
            public int ChunkZ;
            public int SampleSeedOffset;
            public float JitterFraction;
            public byte SandMaskThreshold;
            public byte RockMaskThreshold;
            public float MinimumNormalY;
            public float NormalOffset;
            public float MinWorldYExclusive;
            public float MaxWorldYExclusive;
            public float EdgeDitherDistance;
            public float ScaleMin;
            public float ScaleMax;
            public float HeightScaleMin;
            public float HeightScaleMax;
            public float WidthScaleMin;
            public float WidthScaleMax;
            public int TypeId;
            public int OrganicSemanticType;
            public int ColonyCableSemanticType;
            public int ColonyHullSemanticType;
            public int ColonyBeamSemanticType;
            public int DeadZoneSemanticType;
            public float WaterLevel;
            public float ColonyBiomeStartDepth;
            public float DeadZoneStartDepth;
            public float VerticalBiomeBlendBand;
            public float TechnoJungleThreshold;
            public float TechnoJungleCellSize;
            public float TechnoJungleSecondaryCellSize;
            public float TechnoJungleWallWidth;
            public float TechnoJungleWarpMeters;
            public float TechnoJungleFlowAnisotropy;
            public float DeadZoneStructureChance;
            public float DeadZoneDensityScale;
            public float AbyssalFlowNoiseScale;
            public float AbyssalFlowNoiseStrength;
            public float AbyssalFlowVerticalStrength;
            public int EnableVerticalBiomeRewrite;
            public uint ScaleSalt;
            public uint WidthSalt;
            public float ScaleJitter;
            public float RotationJitterRadians;
            public uint RotationSalt;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                int x = index % SampleCountX;
                int z = index / SampleCountX;
                uint seed = BuildSampleSeed(TileX, TileZ, (ChunkX << 16) + x + SampleSeedOffset, (ChunkZ << 16) + z + SampleSeedOffset);
                float sampleX = BuildJitteredCoordinate(MinX, StepX, x, JitterFraction, seed);
                float sampleZ = BuildJitteredCoordinate(MinZ, StepZ, z, JitterFraction, seed ^ 0x9E3779B9u);
                if (!TrySampleTerrainPlacement(
                        sampleX,
                        sampleZ,
                        seed,
                        TerrainPosition,
                        TerrainSize,
                        AlphamapResolution,
                        HeightResolution,
                        SandMaskThreshold,
                        RockMaskThreshold,
                        MinimumNormalY,
                        SandMask,
                        RockMask,
                        HeightSamples,
                        out float worldY,
                        out float3 normal,
                        out float variation))
                {
                    return;
                }

                if (worldY <= MinWorldYExclusive || worldY >= MaxWorldYExclusive)
                    return;

                if (!TryPassChunkEdgeDither(sampleX, sampleZ, MinX, MaxX, MinZ, MaxZ, EdgeDitherDistance, seed, out float edgeDistance))
                    return;

                float scaleLerp = Hash01(seed ^ ScaleSalt);
                float scale = math.lerp(ScaleMin, ScaleMax, scaleLerp);
                float scaleJitter = math.lerp(1f - ScaleJitter, 1f + ScaleJitter, Hash01(seed ^ (ScaleSalt ^ 0x27D4EB2Fu)));
                scale *= scaleJitter;
                float heightScale = math.lerp(HeightScaleMin, HeightScaleMax, scaleLerp);
                float widthScale = math.lerp(WidthScaleMin, WidthScaleMax, Hash01(seed ^ WidthSalt));
                float3 position = new float3(sampleX, worldY, sampleZ) + (normal * NormalOffset);
                float2 flowDirection = ResolveSlopeFlowDirection(normal, seed);
                float3 flowVector = new float3(flowDirection.x, 0f, flowDirection.y);
                byte biomeLayer = (byte)VegetationBiomeLayer.OrganicShelf;
                int semanticType = OrganicSemanticType;

                if (EnableVerticalBiomeRewrite != 0)
                {
                    biomeLayer = ResolveBiomeLayerStatic(
                        WaterLevel,
                        worldY,
                        ColonyBiomeStartDepth,
                        DeadZoneStartDepth,
                        VerticalBiomeBlendBand,
                        seed);

                    if (biomeLayer == (byte)VegetationBiomeLayer.ColonyGraveyard)
                    {
                        if (!TryEvaluateTechnoJungle(
                                sampleX,
                                sampleZ,
                                seed,
                                flowDirection,
                                TechnoJungleThreshold,
                                TechnoJungleCellSize,
                                TechnoJungleSecondaryCellSize,
                                TechnoJungleWallWidth,
                                TechnoJungleWarpMeters,
                                TechnoJungleFlowAnisotropy,
                                out float technoOccupancy))
                        {
                            return;
                        }

                        semanticType = ResolveColonySemanticTypeStatic(
                            seed,
                            ColonyCableSemanticType,
                            ColonyHullSemanticType,
                            ColonyBeamSemanticType);
                        heightScale *= math.lerp(0.9f, 1.35f, technoOccupancy);
                        widthScale *= math.lerp(0.95f, 1.2f, technoOccupancy);
                    }
                    else if (biomeLayer == (byte)VegetationBiomeLayer.DeadZone)
                    {
                        float deadZoneDepth = math.max(0f, WaterLevel - worldY);
                        float deadZoneDepthT = math.saturate((deadZoneDepth - DeadZoneStartDepth) / 2000f);
                        if (!TryEvaluateTechnoJungle(
                                sampleX,
                                sampleZ,
                                seed ^ 0x51ED270Bu,
                                flowDirection,
                                TechnoJungleThreshold,
                                TechnoJungleCellSize * 1.6f,
                                TechnoJungleSecondaryCellSize * 1.35f,
                                TechnoJungleWallWidth * 1.4f,
                                TechnoJungleWarpMeters * 0.8f,
                                math.max(0.2f, TechnoJungleFlowAnisotropy * 0.7f),
                                out float deadZoneOccupancy))
                        {
                            return;
                        }

                        float keepChance = math.saturate(
                            math.lerp(DeadZoneDensityScale, DeadZoneDensityScale * 0.18f, deadZoneDepthT) *
                            math.max(DeadZoneStructureChance, deadZoneOccupancy * math.lerp(1f, 0.45f, deadZoneDepthT)));
                        if (Hash01(seed ^ 0xC13FA9A9u) > keepChance)
                            return;

                        semanticType = DeadZoneSemanticType;
                        float deadZoneScale = math.lerp(4.5f, 12f, math.max(deadZoneDepthT, Hash01(seed ^ 0x94D049BBu)));
                        scale *= deadZoneScale;
                        heightScale *= deadZoneScale;
                        widthScale *= math.lerp(2.1f, 4.4f, math.max(deadZoneOccupancy, deadZoneDepthT));
                    }
                }

                float depthBelowSurface = math.max(0f, WaterLevel - position.y);
                flowVector = ApplyAbyssalFlowNoiseStatic(
                    flowVector,
                    position,
                    depthBelowSurface,
                    ColonyBiomeStartDepth,
                    AbyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength,
                    seed);
                flowDirection = math.normalizesafe(new float2(flowVector.x, flowVector.z), flowDirection);
                float rotationJitter = ((Hash01(seed ^ RotationSalt) * 2f) - 1f) * RotationJitterRadians;
                quaternion rotation = math.mul(BuildAlignedRotation(normal, variation), quaternion.AxisAngle(normal, rotationJitter));
                Output[index] = new JobInstanceRecord
                {
                    Matrix = float4x4.TRS(position, rotation, new float3(scale, scale, scale)),
                    HeightScale = heightScale,
                    WidthScale = widthScale,
                    Variation = variation,
                    EdgeDistance = edgeDistance,
                    FlowDirection = flowDirection,
                    FlowVector = flowVector,
                    Type = TypeId,
                    SemanticType = semanticType,
                    BiomeLayer = biomeLayer,
                    IsValid = 1
                };
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct GenerateFloatingVegetationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> SandMask;
            [ReadOnly] public NativeArray<byte> RockMask;
            [ReadOnly] public NativeArray<float> HeightSamples;
            public NativeArray<JobInstanceRecord> Output;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int AlphamapResolution;
            public int HeightResolution;
            public float MinX;
            public float MinZ;
            public float MaxX;
            public float MaxZ;
            public float StepX;
            public float StepZ;
            public int SampleCountX;
            public int TileX;
            public int TileZ;
            public int ChunkX;
            public int ChunkZ;
            public int SampleSeedOffset;
            public float JitterFraction;
            public byte SandMaskThreshold;
            public byte RockMaskThreshold;
            public float MinimumNormalY;
            public float WaterLevel;
            public float FloatingSurfaceOffset;
            public float FloatingSurfaceBand;
            public float EdgeDitherDistance;
            public float ScaleMin;
            public float ScaleMax;
            public float FloatingPatchThreshold;
            public float FloatingPatchNoiseScale;
            public float FloatingCellSize;
            public float FloatingSecondaryCellSize;
            public float FloatingWallWidth;
            public float FloatingWarpMeters;
            public float2 FloatingFlowDirection;
            public float FloatingFlowAnisotropy;
            public float ScaleJitter;
            public float RotationJitterRadians;
            public uint RotationSalt;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                int x = index % SampleCountX;
                int z = index / SampleCountX;
                uint seed = BuildSampleSeed(TileX, TileZ, (ChunkX << 16) + x + SampleSeedOffset, (ChunkZ << 16) + z + SampleSeedOffset);
                float sampleX = BuildJitteredCoordinate(MinX, StepX, x, JitterFraction, seed);
                float sampleZ = BuildJitteredCoordinate(MinZ, StepZ, z, JitterFraction, seed ^ 0x94D049BBu);
                if (!TrySampleTerrainPlacement(
                        sampleX,
                        sampleZ,
                        seed,
                        TerrainPosition,
                        TerrainSize,
                        AlphamapResolution,
                        HeightResolution,
                        SandMaskThreshold,
                        RockMaskThreshold,
                        MinimumNormalY,
                        SandMask,
                        RockMask,
                        HeightSamples,
                        out float worldY,
                        out _,
                        out float variation))
                {
                    return;
                }

                if (math.abs(worldY - WaterLevel) > FloatingSurfaceBand)
                    return;

                if (!TryPassChunkEdgeDither(sampleX, sampleZ, MinX, MaxX, MinZ, MaxZ, EdgeDitherDistance, seed, out float edgeDistance))
                    return;

                if (!TryEvaluateFloatingLabyrinth(
                        sampleX,
                        sampleZ,
                        seed,
                        FloatingPatchThreshold,
                        FloatingPatchNoiseScale,
                        FloatingCellSize,
                        FloatingSecondaryCellSize,
                        FloatingWallWidth,
                        FloatingWarpMeters,
                        FloatingFlowDirection,
                        FloatingFlowAnisotropy,
                        out float occupancy))
                {
                    return;
                }

                float scaleLerp = Hash01(seed ^ 0xD1B54A35u);
                float scale = math.lerp(ScaleMin, ScaleMax, scaleLerp);
                float scaleJitter = math.lerp(1f - ScaleJitter, 1f + ScaleJitter, Hash01(seed ^ 0x27D4EB2Fu));
                scale *= scaleJitter;
                float heightScale = math.lerp(0.35f, 0.9f, occupancy) * math.lerp(0.85f, 1.05f, scaleLerp);
                float widthScale = math.lerp(0.8f, 1.25f, math.max(Hash01(seed ^ 0xA24BAEDCu), occupancy));
                float3 position = new float3(sampleX, WaterLevel + FloatingSurfaceOffset, sampleZ);
                float2 flowDirection = math.normalizesafe(FloatingFlowDirection, new float2(1f, 0f));
                float3 flowVector = new float3(flowDirection.x, 0f, flowDirection.y);
                float rotationAngle = (variation * math.PI * 2f) + (((Hash01(seed ^ RotationSalt) * 2f) - 1f) * RotationJitterRadians);
                quaternion rotation = quaternion.RotateY(rotationAngle);
                Output[index] = new JobInstanceRecord
                {
                    Matrix = float4x4.TRS(position, rotation, new float3(scale, scale, scale)),
                    HeightScale = heightScale,
                    WidthScale = widthScale,
                    Variation = variation,
                    EdgeDistance = edgeDistance,
                    FlowDirection = flowDirection,
                    FlowVector = flowVector,
                    Type = (int)HectonVegetationInstanceType.Sargassum,
                    SemanticType = (int)VegetationSemanticType.FloatingSargassum,
                    BiomeLayer = (byte)VegetationBiomeLayer.OrganicShelf,
                    IsValid = 1
                };
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct SampleBiomassDensityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public int TypeMask;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                Output[index] = SampleDensityAtPosition(Positions[index], TypeMask, Chunks, DensityGrid, ChunkCount);
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        public struct VegetationDensityQueryJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> Positions;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> Chunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public float GrassVisibilityWeight;
            public float KelpVisibilityWeight;
            public float SargassumVisibilityWeight;
            public float WaterLevel;
            public float FloatingSurfaceOffset;
            public float SargassumVisibilityBand;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length)
                    return;

                Vector3 position = Positions[index];
                float3 densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    Chunks,
                    DensityGrid,
                    ChunkCount);
                Output[index] = EvaluateVisibilityModifierStatic(
                    position.y,
                    densityChannels,
                    GrassVisibilityWeight,
                    KelpVisibilityWeight,
                    SargassumVisibilityWeight,
                    WaterLevel,
                    FloatingSurfaceOffset,
                    SargassumVisibilityBand);
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct ApplyWorldOffsetJob : IJobParallelFor
        {
            public NativeArray<Matrix4x4> Matrices;
            public int StartIndex;
            public float3 Offset;

            public void Execute(int index)
            {
                int matrixIndex = StartIndex + index;
                if (!Matrices.IsCreated || matrixIndex < 0 || matrixIndex >= Matrices.Length)
                    return;

                Matrix4x4 matrix = Matrices[matrixIndex];
                matrix.m03 += Offset.x;
                matrix.m13 += Offset.y;
                matrix.m23 += Offset.z;
                Matrices[matrixIndex] = matrix;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct ReduceAverageDensityJob : IJob
        {
            [ReadOnly] public NativeArray<float> Input;
            [WriteOnly] public NativeArray<float> Output;

            public void Execute()
            {
                if (!Output.IsCreated || Output.Length < 1 || !Input.IsCreated || Input.Length <= 0)
                    return;

                float sum = 0f;
                for (int i = 0; i < Input.Length; i++)
                    sum += Input[i];

                Output[0] = sum / math.max(1, Input.Length);
            }
        }

        private static bool TrySampleTerrainPlacement(
            float worldX,
            float worldZ,
            uint seed,
            float3 terrainPosition,
            float3 terrainSize,
            int alphamapResolution,
            int heightResolution,
            byte sandMaskThreshold,
            byte rockMaskThreshold,
            float minimumNormalY,
            NativeArray<byte> sandMask,
            NativeArray<byte> rockMask,
            NativeArray<float> heightSamples,
            out float worldY,
            out float3 normal,
            out float variation)
        {
            worldY = 0f;
            normal = new float3(0f, 1f, 0f);
            variation = Hash01(seed ^ 0x68E31DA4u);
            if (!sandMask.IsCreated || !heightSamples.IsCreated || alphamapResolution <= 0 || heightResolution <= 1)
                return false;

            float localX = worldX - terrainPosition.x;
            float localZ = worldZ - terrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > terrainSize.x || localZ > terrainSize.z)
                return false;

            float normalizedX = math.saturate(localX / math.max(0.01f, terrainSize.x));
            float normalizedZ = math.saturate(localZ / math.max(0.01f, terrainSize.z));
            int alphaX = math.clamp((int)math.floor(normalizedX * alphamapResolution), 0, alphamapResolution - 1);
            int alphaZ = math.clamp((int)math.floor(normalizedZ * alphamapResolution), 0, alphamapResolution - 1);
            int maskIndex = (alphaZ * alphamapResolution) + alphaX;
            if (maskIndex < 0 || maskIndex >= sandMask.Length)
                return false;

            if (sandMask[maskIndex] <= sandMaskThreshold)
                return false;

            if (rockMask.IsCreated && maskIndex < rockMask.Length && rockMask[maskIndex] > rockMaskThreshold)
                return false;

            worldY = terrainPosition.y + SampleHeight(normalizedX, normalizedZ, terrainSize, heightResolution, heightSamples);
            normal = SampleNormal(normalizedX, normalizedZ, terrainSize, heightResolution, heightSamples);
            return normal.y >= minimumNormalY;
        }

        private static float2 ResolveSlopeFlowDirection(float3 normal, uint seed)
        {
            float2 downhill = new float2(-normal.x, -normal.z);
            if (math.lengthsq(downhill) <= 0.000001f)
            {
                float angle = Hash01(seed ^ 0xB5297A4Du) * math.PI * 2f;
                return new float2(math.cos(angle), math.sin(angle));
            }

            return math.normalizesafe(downhill, new float2(1f, 0f));
        }

        private static byte ResolveBiomeLayerStatic(
            float waterLevel,
            float worldY,
            float colonyBiomeStartDepth,
            float deadZoneStartDepth,
            float verticalBiomeBlendBand,
            uint seed)
        {
            float depth = math.max(0f, waterLevel - worldY);
            float halfBand = math.max(1f, verticalBiomeBlendBand * 0.5f);
            float firstBlendStart = colonyBiomeStartDepth - halfBand;
            float firstBlendEnd = colonyBiomeStartDepth + halfBand;
            if (depth <= firstBlendStart)
                return (byte)VegetationBiomeLayer.OrganicShelf;

            if (depth < firstBlendEnd)
            {
                float transition = math.saturate((depth - firstBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0x6E624EB7u) < transition
                    ? (byte)VegetationBiomeLayer.ColonyGraveyard
                    : (byte)VegetationBiomeLayer.OrganicShelf;
            }

            float secondBlendStart = deadZoneStartDepth - halfBand;
            float secondBlendEnd = deadZoneStartDepth + halfBand;
            if (depth <= secondBlendStart)
                return (byte)VegetationBiomeLayer.ColonyGraveyard;

            if (depth < secondBlendEnd)
            {
                float transition = math.saturate((depth - secondBlendStart) / math.max(0.01f, verticalBiomeBlendBand));
                return Hash01(seed ^ 0xB5297A4Du) < transition
                    ? (byte)VegetationBiomeLayer.DeadZone
                    : (byte)VegetationBiomeLayer.ColonyGraveyard;
            }

            return (byte)VegetationBiomeLayer.DeadZone;
        }

        private static int ResolveColonySemanticTypeStatic(
            uint seed,
            int cableSemanticType,
            int hullSemanticType,
            int beamSemanticType)
        {
            float selector = Hash01(seed ^ 0x165667B1u);
            if (selector < 0.34f)
                return cableSemanticType;
            if (selector < 0.67f)
                return hullSemanticType;

            return beamSemanticType;
        }

        private static bool TryEvaluateTechnoJungle(
            float worldX,
            float worldZ,
            uint seed,
            float2 flowDirection,
            float threshold,
            float cellSize,
            float secondaryCellSize,
            float wallWidth,
            float warpMeters,
            float flowAnisotropy,
            out float occupancy)
        {
            float2 world = new float2(worldX, worldZ);
            float2 normalizedFlow = math.normalizesafe(flowDirection, new float2(1f, 0f));
            float2 crossFlow = new float2(-normalizedFlow.y, normalizedFlow.x);
            float2 flowSpace = new float2(
                math.dot(world, normalizedFlow),
                math.dot(world, crossFlow) * flowAnisotropy);

            float patchNoiseScale = 1f / math.max(0.01f, cellSize * 2.2f);
            float2 warp = SampleFloatingWarp(world, patchNoiseScale, warpMeters);
            float primaryEdgeDistance = EvaluateVoronoiEdgeDistance(flowSpace + warp, cellSize, PrimaryVoronoiSalt ^ 0x7FEB352Du, out float primaryVariation);
            float secondaryEdgeDistance = EvaluateVoronoiEdgeDistance(world + (warp * 0.55f), secondaryCellSize, SecondaryVoronoiSalt ^ 0x846CA68Bu, out float secondaryVariation);
            float primaryWall = 1f - math.saturate(primaryEdgeDistance / math.max(0.01f, wallWidth));
            float secondaryWall = 1f - math.saturate(secondaryEdgeDistance / math.max(0.01f, wallWidth * 0.85f));
            float combinedWall = math.saturate((primaryWall * 0.74f) + (secondaryWall * 0.46f));
            float variation = math.lerp(primaryVariation, secondaryVariation, 0.4f);
            occupancy = combinedWall * math.lerp(0.8f, 1.18f, variation);
            occupancy *= math.lerp(0.92f, 1.08f, Hash01(seed ^ OccupancyVariationSalt ^ 0x27D4EB2Fu));
            return occupancy > threshold;
        }

        private static float3 ApplyAbyssalFlowNoiseStatic(
            float3 baseFlow,
            float3 position,
            float depthBelowSurface,
            float colonyBiomeStartDepth,
            float noiseScale,
            float horizontalStrength,
            float verticalStrength,
            uint seed)
        {
            if (depthBelowSurface <= colonyBiomeStartDepth)
                return baseFlow;

            float influence = math.saturate((depthBelowSurface - colonyBiomeStartDepth) / math.max(1f, colonyBiomeStartDepth));
            float3 sample = position * noiseScale;
            float3 noiseVector = new float3(
                noise.snoise(sample + new float3(11.7f, 3.1f, 19.9f)),
                noise.snoise(sample + new float3(23.3f, 29.1f, 7.7f)),
                noise.snoise(sample + new float3(41.9f, 13.7f, 31.3f)));
            noiseVector += new float3(
                ((Hash01(seed ^ 0xA24BAEDCu) * 2f) - 1f) * 0.15f,
                ((Hash01(seed ^ 0x94D049BBu) * 2f) - 1f) * 0.08f,
                ((Hash01(seed ^ 0xC13FA9A9u) * 2f) - 1f) * 0.15f);

            float3 chaotic = new float3(noiseVector.x, noiseVector.y * verticalStrength, noiseVector.z) * horizontalStrength;
            return baseFlow + (chaotic * influence);
        }

        private static bool TryPassChunkEdgeDither(
            float worldX,
            float worldZ,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float edgeDitherDistance,
            uint seed,
            out float edgeDistance)
        {
            edgeDistance = math.min(math.min(worldX - minX, maxX - worldX), math.min(worldZ - minZ, maxZ - worldZ));
            if (edgeDitherDistance <= 0f)
                return edgeDistance >= 0f;

            if (edgeDistance <= 0f)
                return false;

            if (edgeDistance >= edgeDitherDistance)
                return true;

            float keepChance = math.saturate(edgeDistance / math.max(0.01f, edgeDitherDistance));
            float blueNoiseThreshold = SampleBlueNoiseThreshold(worldX, worldZ, edgeDitherDistance, seed);
            float organicThreshold = SampleOrganicEdgeThreshold(worldX, worldZ, edgeDitherDistance, seed);
            float spatialThreshold = math.saturate(math.lerp(blueNoiseThreshold, organicThreshold, 0.35f));
            keepChance = math.saturate(keepChance + ((organicThreshold - 0.5f) * (1f - keepChance) * 0.55f));
            return spatialThreshold <= keepChance;
        }

        private static float SampleBlueNoiseThreshold(float worldX, float worldZ, float edgeDitherDistance, uint seed)
        {
            float cellSize = math.max(0.85f, edgeDitherDistance * 0.42f);
            float2 domain = new float2(worldX / cellSize, worldZ / cellSize);
            int baseCellX = (int)math.floor(domain.x);
            int baseCellZ = (int)math.floor(domain.y);
            float nearestDistanceSq = float.MaxValue;
            float secondNearestDistanceSq = float.MaxValue;

            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = baseCellX + offsetX;
                    int cellZ = baseCellZ + offsetZ;
                    uint cellSeed = BuildCellSeed(cellX, cellZ, seed ^ 0xD1B54A35u);
                    float2 featurePoint = new float2(cellX + Hash01(cellSeed), cellZ + Hash01(cellSeed ^ 0x94D049BBu));
                    float distanceSq = math.lengthsq(featurePoint - domain);
                    if (distanceSq < nearestDistanceSq)
                    {
                        secondNearestDistanceSq = nearestDistanceSq;
                        nearestDistanceSq = distanceSq;
                    }
                    else if (distanceSq < secondNearestDistanceSq)
                    {
                        secondNearestDistanceSq = distanceSq;
                    }
                }
            }

            float nearestDistance = math.sqrt(math.max(0f, nearestDistanceSq));
            float secondNearestDistance = math.sqrt(math.max(nearestDistanceSq, secondNearestDistanceSq));
            float ringSeparation = math.saturate((secondNearestDistance - nearestDistance) * 1.8f);
            float microVariation = SampleValueNoise((worldX * 1.13f) + 7.31f, (worldZ * 1.13f) + 11.79f, seed ^ 0xC13FA9A9u);
            return math.saturate((ringSeparation * 0.72f) + (microVariation * 0.28f));
        }

        private static float SampleOrganicEdgeThreshold(float worldX, float worldZ, float edgeDitherDistance, uint seed)
        {
            float cellSize = math.max(0.75f, edgeDitherDistance * 0.65f);
            float2 cellPosition = new float2(worldX / cellSize, worldZ / cellSize);
            int cellX = (int)math.floor(cellPosition.x);
            int cellZ = (int)math.floor(cellPosition.y);
            float2 cellFraction = math.frac(cellPosition);
            uint cellSeed = BuildCellSeed(cellX, cellZ, seed ^ 0x51ED270Bu);
            float2 jitter = new float2(Hash01(cellSeed), Hash01(cellSeed ^ 0xA24BAEDCu));
            float2 delta = cellFraction - jitter;
            float cellular = 1f - math.saturate(math.sqrt(math.lengthsq(delta)) * 1.15f);
            float broad = SampleValueNoise(worldX * 0.21f, worldZ * 0.21f, seed ^ 0x9E3779B9u);
            float detail = SampleValueNoise((worldX * 0.83f) + 19.37f, (worldZ * 0.83f) + 41.11f, seed ^ 0x68E31DA4u);
            return math.saturate((cellular * 0.5f) + (broad * 0.3f) + (detail * 0.2f));
        }

        private static float SampleHeight(
            float normalizedX,
            float normalizedZ,
            float3 terrainSize,
            int heightResolution,
            NativeArray<float> heights)
        {
            float sampleX = normalizedX * (heightResolution - 1);
            float sampleZ = normalizedZ * (heightResolution - 1);
            int x0 = math.clamp((int)math.floor(sampleX), 0, heightResolution - 1);
            int z0 = math.clamp((int)math.floor(sampleZ), 0, heightResolution - 1);
            int x1 = math.min(x0 + 1, heightResolution - 1);
            int z1 = math.min(z0 + 1, heightResolution - 1);
            float tx = sampleX - x0;
            float tz = sampleZ - z0;
            float h00 = heights[(z0 * heightResolution) + x0];
            float h10 = heights[(z0 * heightResolution) + x1];
            float h01 = heights[(z1 * heightResolution) + x0];
            float h11 = heights[(z1 * heightResolution) + x1];
            float bottom = math.lerp(h00, h10, tx);
            float top = math.lerp(h01, h11, tx);
            return math.lerp(bottom, top, tz);
        }

        private static float3 SampleNormal(
            float normalizedX,
            float normalizedZ,
            float3 terrainSize,
            int heightResolution,
            NativeArray<float> heights)
        {
            float sampleX = normalizedX * (heightResolution - 1);
            float sampleZ = normalizedZ * (heightResolution - 1);
            int centerX = math.clamp((int)math.round(sampleX), 0, heightResolution - 1);
            int centerZ = math.clamp((int)math.round(sampleZ), 0, heightResolution - 1);
            int x0 = math.max(0, centerX - 1);
            int x1 = math.min(heightResolution - 1, centerX + 1);
            int z0 = math.max(0, centerZ - 1);
            int z1 = math.min(heightResolution - 1, centerZ + 1);
            float dx = math.max(0.001f, (x1 - x0) * (terrainSize.x / math.max(1f, heightResolution - 1)));
            float dz = math.max(0.001f, (z1 - z0) * (terrainSize.z / math.max(1f, heightResolution - 1)));
            float hLeft = heights[(centerZ * heightResolution) + x0];
            float hRight = heights[(centerZ * heightResolution) + x1];
            float hDown = heights[(z0 * heightResolution) + centerX];
            float hUp = heights[(z1 * heightResolution) + centerX];
            float3 tangentX = new float3(dx, hRight - hLeft, 0f);
            float3 tangentZ = new float3(0f, hUp - hDown, dz);
            return math.normalizesafe(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
        }

        private static quaternion BuildAlignedRotation(float3 normal, float variation)
        {
            float3 reference = math.abs(normal.y) > 0.99f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            float3 tangent = math.normalizesafe(math.cross(reference, normal), new float3(1f, 0f, 0f));
            float3 bitangent = math.normalizesafe(math.cross(normal, tangent), new float3(0f, 0f, 1f));
            float angle = variation * math.PI * 2f;
            float3 forward = math.normalizesafe((tangent * math.cos(angle)) + (bitangent * math.sin(angle)), new float3(0f, 0f, 1f));
            return quaternion.LookRotationSafe(forward, normal);
        }

        private static void ShiftChunkPool(ref NativeChunkPool pool, float3 offset)
        {
            if (!pool.Matrices.IsCreated || pool.Capacity <= 0)
                return;

            var job = new ApplyWorldOffsetJob
            {
                Matrices = pool.Matrices,
                Offset = offset
            };

            job.Schedule(pool.Capacity, DefaultJobBatchSize).Complete();
        }

        private static void ShiftChunkPayloadBounds(ref ChunkPayload payload, Vector3 offset)
        {
            payload.MinX += offset.x;
            payload.MaxX += offset.x;
            payload.MinZ += offset.z;
            payload.MaxZ += offset.z;
            payload.WorldBounds.center += offset;
        }

        private static JobHandle ScheduleShiftChunkSlice(
            NativeArray<Matrix4x4> matrices,
            int startIndex,
            int count,
            float3 offset,
            JobHandle dependency)
        {
            if (!matrices.IsCreated || count <= 0 || startIndex < 0 || startIndex + count > matrices.Length)
                return dependency;

            var job = new ApplyWorldOffsetJob
            {
                Matrices = matrices,
                StartIndex = startIndex,
                Offset = offset
            };

            return job.Schedule(count, DefaultJobBatchSize, dependency);
        }

        private float ComputeNativePoolFragmentationPercent()
        {
            float surfacePercent = ComputePoolFragmentationPercent(_surfacePoolFreeBlocks, _surfacePoolFreeBlockCount);
            float underwaterPercent = ComputePoolFragmentationPercent(_underwaterPoolFreeBlocks, _underwaterPoolFreeBlockCount);
            int surfaceCapacity = Mathf.Max(1, _surfaceChunkPool.Capacity);
            int underwaterCapacity = Mathf.Max(1, _underwaterChunkPool.Capacity);
            return ((surfacePercent * surfaceCapacity) + (underwaterPercent * underwaterCapacity)) / (surfaceCapacity + underwaterCapacity);
        }

        private long ComputeTileCacheUsedBytes()
        {
            long bytes = 0L;
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                bytes += GetTileCacheBufferBytes(state.PrimaryCacheBuffer);
                bytes += GetTileCacheBufferBytes(state.SecondaryCacheBuffer);
            }

            return bytes;
        }

        private static long GetTileCacheBufferBytes(TileNativeCacheBuffer buffer)
        {
            long bytes = 0L;
            if (buffer.SandMaskNative.IsCreated)
                bytes += buffer.SandMaskNative.Length;
            if (buffer.RockMaskNative.IsCreated)
                bytes += buffer.RockMaskNative.Length;
            if (buffer.HeightSamplesNative.IsCreated)
                bytes += (long)buffer.HeightSamplesNative.Length * sizeof(float);

            return bytes;
        }

        private static float ComputePoolFragmentationPercent(PoolBlock[] freeBlocks, int freeBlockCount)
        {
            if (freeBlocks == null || freeBlockCount <= 1)
                return 0f;

            int totalFree = 0;
            int largestFree = 0;
            for (int i = 0; i < freeBlockCount; i++)
            {
                int length = Mathf.Max(0, freeBlocks[i].Length);
                totalFree += length;
                if (length > largestFree)
                    largestFree = length;
            }

            if (totalFree <= 0)
                return 0f;

            return (1f - ((float)largestFree / totalFree)) * 100f;
        }

        private static float BuildJitteredCoordinate(float min, float step, int index, float jitterFraction, uint seed)
        {
            float basePosition = min + ((index + 0.5f) * step);
            float jitter = ((Hash01(seed) * 2f) - 1f) * step * jitterFraction;
            return basePosition + jitter;
        }

        private static bool TryEvaluateFloatingLabyrinth(
            float worldX,
            float worldZ,
            uint seed,
            float floatingPatchThreshold,
            float floatingPatchNoiseScale,
            float floatingCellSize,
            float floatingSecondaryCellSize,
            float floatingWallWidth,
            float floatingWarpMeters,
            float2 floatingFlowDirection,
            float floatingFlowAnisotropy,
            out float occupancy)
        {
            float2 world = new float2(worldX, worldZ);
            float2 flowDirection = math.normalizesafe(floatingFlowDirection, new float2(1f, 0f));
            float2 crossFlow = new float2(-flowDirection.y, flowDirection.x);
            float2 flowSpace = new float2(
                math.dot(world, flowDirection),
                math.dot(world, crossFlow) * floatingFlowAnisotropy);

            float2 warp = SampleFloatingWarp(world, floatingPatchNoiseScale, floatingWarpMeters);
            float primaryEdgeDistance = EvaluateVoronoiEdgeDistance(flowSpace + warp, floatingCellSize, PrimaryVoronoiSalt, out float primaryVariation);
            float secondaryEdgeDistance = EvaluateVoronoiEdgeDistance(world + (warp * 0.65f), floatingSecondaryCellSize, SecondaryVoronoiSalt, out float secondaryVariation);
            float primaryWall = 1f - math.saturate(primaryEdgeDistance / math.max(0.01f, floatingWallWidth));
            float secondaryWidth = math.max(0.75f, floatingWallWidth * 0.8f);
            float secondaryWall = 1f - math.saturate(secondaryEdgeDistance / secondaryWidth);
            float combinedWall = math.saturate((primaryWall * 0.72f) + (secondaryWall * 0.4f));
            float cellVariation = math.lerp(primaryVariation, secondaryVariation, 0.35f);
            occupancy = combinedWall * math.lerp(0.82f, 1.14f, cellVariation);
            occupancy *= math.lerp(0.92f, 1.08f, Hash01(seed ^ OccupancyVariationSalt));
            return occupancy > floatingPatchThreshold;
        }

        private static float2 SampleFloatingWarp(float2 world, float floatingPatchNoiseScale, float floatingWarpMeters)
        {
            float sampleX = world.x * floatingPatchNoiseScale;
            float sampleZ = world.y * floatingPatchNoiseScale;
            float warpX = ((SampleValueNoise(sampleX + 11.37f, sampleZ + 47.13f, WarpXSalt) * 2f) - 1f) * floatingWarpMeters;
            float warpZ = ((SampleValueNoise(sampleX + 29.61f, sampleZ + 73.77f, WarpZSalt) * 2f) - 1f) * floatingWarpMeters;
            return new float2(warpX, warpZ);
        }

        private static float EvaluateVoronoiEdgeDistance(float2 position, float cellSize, uint salt, out float variation)
        {
            float inverseCellSize = 1f / math.max(0.01f, cellSize);
            float2 scaled = position * inverseCellSize;
            int baseX = (int)math.floor(scaled.x);
            int baseZ = (int)math.floor(scaled.y);
            float nearestDistanceSqr = float.PositiveInfinity;
            float secondDistanceSqr = float.PositiveInfinity;
            uint nearestSeed = 0u;

            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = baseX + offsetX;
                    int cellZ = baseZ + offsetZ;
                    uint cellSeed = BuildCellSeed(cellX, cellZ, salt);
                    float2 featurePoint = new float2(cellX + Hash01(cellSeed), cellZ + Hash01(cellSeed ^ SecondaryFeatureSalt));
                    float2 delta = featurePoint - scaled;
                    float distanceSqr = math.lengthsq(delta);
                    if (distanceSqr < nearestDistanceSqr)
                    {
                        secondDistanceSqr = nearestDistanceSqr;
                        nearestDistanceSqr = distanceSqr;
                        nearestSeed = cellSeed;
                    }
                    else if (distanceSqr < secondDistanceSqr)
                    {
                        secondDistanceSqr = distanceSqr;
                    }
                }
            }

            float nearestDistance = math.sqrt(nearestDistanceSqr);
            float secondDistance = math.sqrt(secondDistanceSqr);
            variation = Hash01(nearestSeed ^ PrimaryVariationSalt);
            return math.max(0f, (secondDistance - nearestDistance) * cellSize * 0.5f);
        }

        private static float SampleValueNoise(float x, float z, uint salt)
        {
            int minX = (int)math.floor(x);
            int minZ = (int)math.floor(z);
            float fracX = x - minX;
            float fracZ = z - minZ;
            float smoothX = fracX * fracX * (3f - (2f * fracX));
            float smoothZ = fracZ * fracZ * (3f - (2f * fracZ));
            float bottomLeft = Hash01(BuildCellSeed(minX, minZ, salt));
            float bottomRight = Hash01(BuildCellSeed(minX + 1, minZ, salt));
            float topLeft = Hash01(BuildCellSeed(minX, minZ + 1, salt));
            float topRight = Hash01(BuildCellSeed(minX + 1, minZ + 1, salt));
            float bottom = math.lerp(bottomLeft, bottomRight, smoothX);
            float top = math.lerp(topLeft, topRight, smoothX);
            return math.lerp(bottom, top, smoothZ);
        }

        private static Terrain ResolveMainTerrain(TerrainTile tile)
        {
            if (tile.main != null && tile.main.terrain != null)
                return tile.main.terrain;

            if (tile.ActiveTerrain != null)
                return tile.ActiveTerrain;

            return tile.GetTerrain(isDraft: false);
        }

        private bool TryResolveLayerIndices(TerrainData terrainData, out LayerIndices indices)
        {
            indices = default;
            indices.Sand = -1;
            indices.GreenSand = -1;
            indices.Rock = -1;

            if (terrainData == null)
                return false;

            TerrainLayer[] terrainLayers = terrainData.terrainLayers;
            if (terrainLayers == null || terrainLayers.Length == 0)
                return false;

            for (int i = 0; i < terrainLayers.Length; i++)
            {
                TerrainLayer layer = terrainLayers[i];
                if (layer == null)
                    continue;

                string layerName = layer.name;
                if (string.Equals(layerName, SandLayerName, StringComparison.Ordinal))
                {
                    indices.Sand = i;
                    continue;
                }

                if (string.Equals(layerName, GreenSandLayerName, StringComparison.Ordinal))
                {
                    indices.GreenSand = i;
                    continue;
                }

                if (string.Equals(layerName, RockLayerName, StringComparison.Ordinal))
                    indices.Rock = i;
            }

            return indices.Sand >= 0 || indices.GreenSand >= 0;
        }

        private Camera ResolveActiveViewCamera()
        {
            if (_cachedViewCamera != null &&
                _cachedViewCamera.isActiveAndEnabled &&
                _cachedViewCamera.gameObject.activeInHierarchy)
            {
                return _cachedViewCamera;
            }

            if (viewCamera != null &&
                viewCamera.isActiveAndEnabled &&
                viewCamera.gameObject.activeInHierarchy)
            {
                _cachedViewCamera = viewCamera;
                return _cachedViewCamera;
            }

            if (Time.unscaledTime < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = Time.unscaledTime + CameraResolveRetryInterval;
            if (playerTransform != null)
            {
                if (!playerTransform.TryGetComponent(out _cachedViewCamera))
                    _cachedViewCamera = playerTransform.GetComponentInChildren<Camera>(true);
            }

            if (_cachedViewCamera == null && TryGetComponent(out Camera localCamera))
                _cachedViewCamera = localCamera;

            if (_cachedViewCamera != null)
            {
                viewCamera = _cachedViewCamera;
                _nextCameraResolveTime = float.NegativeInfinity;
            }

            return _cachedViewCamera;
        }

        private bool IsChunkVisible(Bounds worldBounds)
        {
            Bounds paddedBounds = worldBounds;
            paddedBounds.Expand(frustumCullPadding);
            return GeometryUtility.TestPlanesAABB(_viewFrustumPlanes, paddedBounds);
        }

        private byte ResolveGrassLodTier(TileRuntimeState state, int chunkX, int chunkZ, Vector3 playerPosition)
        {
            GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float distanceSqr = GetBoundsDistanceSqr(playerPosition.x, playerPosition.z, minX, maxX, minZ, maxZ);
            return GetGrassLodTier(distanceSqr);
        }

        private byte GetGrassLodTier(float distanceSqr)
        {
            float nearRadiusSqr = grassHighDensityRadius * grassHighDensityRadius;
            return distanceSqr <= nearRadiusSqr ? (byte)0 : (byte)1;
        }

        private float GetGrassStepForTier(byte grassLodTier)
        {
            return grassLodTier == 0 ? grassStepMeters : grassFarStepMeters;
        }

        private void UploadChannel(
            HectonIndirectVegetationRenderer renderer,
            ref ComputeBuffer matrixBuffer,
            ref ComputeBuffer dataBuffer,
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            int count,
            Bounds bounds)
        {
            if (renderer == null)
            {
                ReleaseBuffer(ref matrixBuffer);
                ReleaseBuffer(ref dataBuffer);
                return;
            }

            EnsureStructuredBuffer(ref matrixBuffer, count, HectonIndirectVegetationRenderer.InstanceMatrixStride);
            EnsureStructuredBuffer(ref dataBuffer, count, HectonIndirectVegetationRenderer.InstanceDataStride);
            if (matrixBuffer == null || dataBuffer == null)
            {
                ClearChannel(renderer);
                return;
            }

            matrixBuffer.SetData(matrices, 0, 0, count);
            dataBuffer.SetData(metadata, 0, 0, count);
            renderer.BindInstanceBuffer(matrixBuffer, count);
            renderer.BindInstanceDataBuffer(dataBuffer);
            renderer.SetDrawBounds(bounds);
        }

        private void EnsureStructuredBuffer(ref ComputeBuffer buffer, int count, int stride)
        {
            if (count <= 0)
            {
                ReleaseBuffer(ref buffer);
                return;
            }

            if (buffer != null && buffer.count >= count)
                return;

            ReleaseBuffer(ref buffer);
            // COLD ALLOC: ComputeBuffer[count] - streamed vegetation structured payload - owner: HectonMapMagicVegetationBridge
            buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
        }

        private ChunkPayload CreateChunkPayloadHeader(TileRuntimeState state, int chunkX, int chunkZ)
        {
            GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float sizeX = Mathf.Max(0.01f, maxX - minX);
            float sizeZ = Mathf.Max(0.01f, maxZ - minZ);

            ChunkPayload payload = default;
            payload.MinX = minX;
            payload.MaxX = maxX;
            payload.MinZ = minZ;
            payload.MaxZ = maxZ;
            payload.WorldBounds = new Bounds(
                new Vector3(centerX, state.TerrainPosition.y + (state.TerrainSize.y * 0.5f), centerZ),
                new Vector3(sizeX, Mathf.Max(1f, state.TerrainSize.y), sizeZ));
            return payload;
        }

        private static bool TryGetChunkAlphamapRegion(
            TileRuntimeState state,
            int chunkX,
            int chunkZ,
            out int alphaStartX,
            out int alphaStartZ,
            out int alphaWidth,
            out int alphaHeight)
        {
            alphaStartX = 0;
            alphaStartZ = 0;
            alphaWidth = 0;
            alphaHeight = 0;

            if (state == null || state.AlphamapResolution <= 0)
                return false;

            GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float localMinX = minX - state.TerrainPosition.x;
            float localMaxX = maxX - state.TerrainPosition.x;
            float localMinZ = minZ - state.TerrainPosition.z;
            float localMaxZ = maxZ - state.TerrainPosition.z;

            alphaStartX = Mathf.Clamp(Mathf.FloorToInt((localMinX / state.TerrainSize.x) * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            alphaStartZ = Mathf.Clamp(Mathf.FloorToInt((localMinZ / state.TerrainSize.z) * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            int alphaEndX = Mathf.Clamp(Mathf.CeilToInt((localMaxX / state.TerrainSize.x) * state.AlphamapResolution), alphaStartX + 1, state.AlphamapResolution);
            int alphaEndZ = Mathf.Clamp(Mathf.CeilToInt((localMaxZ / state.TerrainSize.z) * state.AlphamapResolution), alphaStartZ + 1, state.AlphamapResolution);
            alphaWidth = Mathf.Max(1, alphaEndX - alphaStartX);
            alphaHeight = Mathf.Max(1, alphaEndZ - alphaStartZ);
            return alphaWidth > 0 && alphaHeight > 0;
        }

        private static int GetChunkRangeStart(float worldMin, float terrainMin, int chunkCount)
        {
            int index = Mathf.FloorToInt((worldMin - terrainMin) / DefaultVirtualChunkSize);
            return Mathf.Clamp(index, 0, chunkCount - 1);
        }

        private static int GetChunkRangeEnd(float worldMax, float terrainMin, int chunkCount)
        {
            int index = Mathf.FloorToInt((worldMax - terrainMin) / DefaultVirtualChunkSize);
            return Mathf.Clamp(index, 0, chunkCount - 1);
        }

        private static void GetChunkBounds(TileRuntimeState state, int chunkX, int chunkZ, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float worldMinX = state.TerrainPosition.x + (chunkX * DefaultVirtualChunkSize);
            float worldMinZ = state.TerrainPosition.z + (chunkZ * DefaultVirtualChunkSize);
            float worldMaxX = Mathf.Min(worldMinX + DefaultVirtualChunkSize, state.TerrainPosition.x + state.TerrainSize.x);
            float worldMaxZ = Mathf.Min(worldMinZ + DefaultVirtualChunkSize, state.TerrainPosition.z + state.TerrainSize.z);
            minX = worldMinX;
            maxX = worldMaxX;
            minZ = worldMinZ;
            maxZ = worldMaxZ;
        }

        private void InsertDesiredChunk(ChunkKey key, float distanceSqr)
        {
            EnsureChunkKeyCapacity(ref _desiredChunkKeys, _desiredChunkCount + 1);
            EnsureFloatCapacity(ref _desiredChunkDistances, _desiredChunkCount + 1);

            int insertIndex = _desiredChunkCount;
            while (insertIndex > 0 && distanceSqr < _desiredChunkDistances[insertIndex - 1])
            {
                _desiredChunkKeys[insertIndex] = _desiredChunkKeys[insertIndex - 1];
                _desiredChunkDistances[insertIndex] = _desiredChunkDistances[insertIndex - 1];
                insertIndex--;
            }

            _desiredChunkKeys[insertIndex] = key;
            _desiredChunkDistances[insertIndex] = distanceSqr;
            _desiredChunkCount++;
        }

        private void EnqueuePendingChunk(ChunkKey key, float priority)
        {
            for (int i = 0; i < _pendingChunkCount; i++)
            {
                if (_pendingChunkKeys[i].Equals(key))
                    return;
            }

            EnsureChunkKeyCapacity(ref _pendingChunkKeys, _pendingChunkCount + 1);
            EnsureFloatCapacity(ref _pendingChunkPriorities, _pendingChunkCount + 1);
            int insertIndex = _pendingChunkCount;
            while (insertIndex > 0 && priority < _pendingChunkPriorities[insertIndex - 1])
            {
                _pendingChunkKeys[insertIndex] = _pendingChunkKeys[insertIndex - 1];
                _pendingChunkPriorities[insertIndex] = _pendingChunkPriorities[insertIndex - 1];
                insertIndex--;
            }

            _pendingChunkKeys[insertIndex] = key;
            _pendingChunkPriorities[insertIndex] = priority;
            _pendingChunkCount++;
        }

        private void DequeuePendingChunkAt(int index)
        {
            if (index < 0 || index >= _pendingChunkCount)
                return;

            for (int i = index; i < _pendingChunkCount - 1; i++)
            {
                _pendingChunkKeys[i] = _pendingChunkKeys[i + 1];
                _pendingChunkPriorities[i] = _pendingChunkPriorities[i + 1];
            }

            _pendingChunkCount--;
            if (_pendingChunkCount >= 0 && _pendingChunkCount < _pendingChunkKeys.Length)
            {
                _pendingChunkKeys[_pendingChunkCount] = default;
                _pendingChunkPriorities[_pendingChunkCount] = float.PositiveInfinity;
            }
        }

        private void TrimPendingQueueToDesired()
        {
            int writeIndex = 0;
            for (int i = 0; i < _pendingChunkCount; i++)
            {
                ChunkKey key = _pendingChunkKeys[i];
                if (!ContainsDesiredChunk(key))
                    continue;

                _pendingChunkKeys[writeIndex] = key;
                _pendingChunkPriorities[writeIndex] = _pendingChunkPriorities[i];
                writeIndex++;
            }

            for (int i = writeIndex; i < _pendingChunkCount; i++)
            {
                _pendingChunkKeys[i] = default;
                _pendingChunkPriorities[i] = float.PositiveInfinity;
            }

            _pendingChunkCount = writeIndex;
        }

        private void EvictNonResidentChunkPayloads()
        {
            if (_chunkPayloads.Count == 0)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                if (!ContainsDesiredChunk(key))
                    _evictionKeys.Add(key);
            }

            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ReleaseChunkPayloadStorage(_evictionKeys[i]);
                _chunkPayloads.Remove(_evictionKeys[i]);
                CancelChunkBuildJob(_evictionKeys[i]);
            }

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator jobEnumerator = _chunkBuildJobs.GetEnumerator();
            while (jobEnumerator.MoveNext())
            {
                ChunkKey key = jobEnumerator.Current.Key;
                if (!ContainsDesiredChunk(key))
                    _jobScratchKeys.Add(key);
            }

            for (int i = 0; i < _jobScratchKeys.Count; i++)
                CancelChunkBuildJob(_jobScratchKeys[i]);
        }

        private void EnforceChunkPoolMemoryGuard()
        {
            long guardBytes = Mathf.Max(MinimumNativePoolBudgetMb, nativePoolGuardMb) * 1024L * 1024L;
            if (_chunkPayloadUsedBytes <= guardBytes || _desiredChunkCount <= 0)
                return;

            bool evictedPayload = false;
            while (_chunkPayloadUsedBytes > guardBytes && TryFindChunkPoolEvictionVictim(out int victimIndex, out ChunkKey victimKey))
            {
                bool hadPayload = _chunkPayloads.TryGetValue(victimKey, out ChunkPayload payload);
                if (hadPayload)
                {
                    ReleaseChunkPayloadStorage(payload);
                    _chunkPayloads.Remove(victimKey);
                }

                CancelChunkBuildJob(victimKey);
                RemoveDesiredChunkAt(victimIndex);
                evictedPayload |= hadPayload;
            }

            TrimPendingQueueToDesired();
            if (evictedPayload)
                _activeSetDirty = true;
        }

        private bool TryFindChunkPoolEvictionVictim(out int victimIndex, out ChunkKey victimKey)
        {
            victimIndex = -1;
            victimKey = default;
            if (_desiredChunkCount <= 0)
                return false;

            Vector2 playerPositionXZ = playerTransform != null
                ? new Vector2(playerTransform.position.x, playerTransform.position.z)
                : Vector2.zero;
            Vector2 forward = ResolveMemoryGuardForward();
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _desiredChunkCount; i++)
            {
                ChunkKey candidateKey = _desiredChunkKeys[i];
                if (!_chunkPayloads.TryGetValue(candidateKey, out ChunkPayload payload))
                    continue;

                float candidateScore = EvaluateChunkEvictionScore(playerPositionXZ, forward, payload);
                if (candidateScore <= bestScore)
                    continue;

                bestScore = candidateScore;
                victimIndex = i;
                victimKey = candidateKey;
            }

            return victimIndex >= 0;
        }

        private Vector2 ResolveMemoryGuardForward()
        {
            Vector2 planarVelocity = new Vector2(_playerVelocity.x, _playerVelocity.z);
            if (planarVelocity.sqrMagnitude >= predictiveMinSpeed * predictiveMinSpeed)
                return planarVelocity.normalized;

            if (playerTransform == null)
                return Vector2.right;

            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return Vector2.right;

            forward.Normalize();
            return new Vector2(forward.x, forward.z);
        }

        private static float EvaluateChunkEvictionScore(Vector2 playerPositionXZ, Vector2 forward, ChunkPayload payload)
        {
            float centerX = (payload.MinX + payload.MaxX) * 0.5f;
            float centerZ = (payload.MinZ + payload.MaxZ) * 0.5f;
            Vector2 toChunk = new Vector2(centerX - playerPositionXZ.x, centerZ - playerPositionXZ.y);
            float distanceSqr = toChunk.sqrMagnitude;
            float longitudinal = Vector2.Dot(toChunk, forward);
            float behindMeters = Mathf.Max(0f, -longitudinal);
            float aheadMeters = Mathf.Max(0f, longitudinal);
            float storageScore = GetChunkPayloadStorageBytes(payload) / (256f * 1024f);
            float behindBias = behindMeters > 0.001f ? 1000000f + (behindMeters * 4096f) : 0f;
            return behindBias + distanceSqr + (storageScore * 512f) - (aheadMeters * 32f);
        }

        private bool ContainsDesiredChunk(ChunkKey key)
        {
            for (int i = 0; i < _desiredChunkCount; i++)
            {
                if (_desiredChunkKeys[i].Equals(key))
                    return true;
            }

            return false;
        }

        private void RemoveDesiredChunkAt(int index)
        {
            if (index < 0 || index >= _desiredChunkCount)
                return;

            for (int i = index; i < _desiredChunkCount - 1; i++)
            {
                _desiredChunkKeys[i] = _desiredChunkKeys[i + 1];
                _desiredChunkDistances[i] = _desiredChunkDistances[i + 1];
            }

            _desiredChunkCount--;
            if (_desiredChunkCount >= 0 && _desiredChunkCount < _desiredChunkKeys.Length)
            {
                _desiredChunkKeys[_desiredChunkCount] = default;
                _desiredChunkDistances[_desiredChunkCount] = float.PositiveInfinity;
            }
        }

        private static float GetBoundsDistanceSqr(float x, float z, float minX, float maxX, float minZ, float maxZ)
        {
            float clampedX = Mathf.Clamp(x, minX, maxX);
            float clampedZ = Mathf.Clamp(z, minZ, maxZ);
            float deltaX = x - clampedX;
            float deltaZ = z - clampedZ;
            return (deltaX * deltaX) + (deltaZ * deltaZ);
        }

        private bool TryEvaluateResidencyCandidate(
            Vector2 playerPositionXZ,
            Vector2 forward,
            Vector2 right,
            bool usePredictiveResidency,
            float forwardRadius,
            float rearRadius,
            float lateralRadius,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            out float distanceSqr,
            out float priority)
        {
            distanceSqr = GetBoundsDistanceSqr(playerPositionXZ.x, playerPositionXZ.y, minX, maxX, minZ, maxZ);
            priority = distanceSqr;
            if (!usePredictiveResidency)
                return distanceSqr <= residentRadius * residentRadius;

            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            Vector2 toChunk = new Vector2(centerX - playerPositionXZ.x, centerZ - playerPositionXZ.y);
            float longitudinal = Vector2.Dot(toChunk, forward);
            float lateral = Mathf.Abs(Vector2.Dot(toChunk, right));

            bool insideResidency;
            if (longitudinal >= 0f)
            {
                float normalizedForward = longitudinal / Mathf.Max(0.01f, forwardRadius);
                float normalizedLateral = lateral / Mathf.Max(0.01f, lateralRadius);
                insideResidency = (normalizedForward * normalizedForward) + (normalizedLateral * normalizedLateral) <= 1f;
            }
            else
            {
                float normalizedRear = longitudinal / Mathf.Max(0.01f, rearRadius);
                float normalizedLateral = lateral / Mathf.Max(0.01f, lateralRadius);
                insideResidency = (normalizedRear * normalizedRear) + (normalizedLateral * normalizedLateral) <= 1f;
            }

            if (!insideResidency)
                return false;

            priority -= Mathf.Max(0f, longitudinal) * forwardPriorityBoost;
            priority += Mathf.Max(0f, -longitudinal) * rearPriorityPenalty;
            return true;
        }

        private bool TryGetDesiredChunkPriority(ChunkKey key, out float priority)
        {
            for (int i = 0; i < _desiredChunkCount; i++)
            {
                if (_desiredChunkKeys[i].Equals(key))
                {
                    priority = _desiredChunkDistances[i];
                    return true;
                }
            }

            priority = 0f;
            return false;
        }

        private static float BuildJitteredWorldCoordinate(float min, float step, int index, float jitterFraction, uint seed)
        {
            float basePosition = min + ((index + 0.5f) * step);
            float jitter = ((Hash01(seed) * 2f) - 1f) * step * jitterFraction;
            return basePosition + jitter;
        }

        private static Vector2 NormalizeScaleRange(Vector2 range)
        {
            float min = Mathf.Max(0.01f, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2(min, max);
        }

        private static void EncapsulateBounds(ref Bounds aggregateBounds, ref bool hasBounds, Bounds chunkBounds)
        {
            if (!hasBounds)
            {
                aggregateBounds = chunkBounds;
                hasBounds = true;
                return;
            }

            aggregateBounds.Encapsulate(chunkBounds.min);
            aggregateBounds.Encapsulate(chunkBounds.max);
        }

        private static long PackTileCoord(int x, int z)
        {
            unchecked
            {
                return ((long)x << 32) ^ (uint)z;
            }
        }

        private void InvalidateTileChunks(int tileX, int tileZ, int chunkCountX, int chunkCountZ)
        {
            for (int chunkZ = 0; chunkZ < chunkCountZ; chunkZ++)
            {
                for (int chunkX = 0; chunkX < chunkCountX; chunkX++)
                {
                    ChunkKey key = new ChunkKey(tileX, tileZ, chunkX, chunkZ);
                    ReleaseChunkPayloadStorage(key);
                    _chunkPayloads.Remove(key);
                    CancelChunkBuildJob(key);
                }
            }

            for (int i = _pendingChunkCount - 1; i >= 0; i--)
            {
                ChunkKey key = _pendingChunkKeys[i];
                if (key.TileX == tileX && key.TileZ == tileZ)
                    DequeuePendingChunkAt(i);
            }
        }

        private void RemoveTileState(int tileX, int tileZ)
        {
            long tileKey = PackTileCoord(tileX, tileZ);
            if (_tileStates.TryGetValue(tileKey, out TileRuntimeState state) && state != null)
            {
                InvalidateTileChunks(tileX, tileZ, state.ChunkCountX, state.ChunkCountZ);
                DisposeTileNativeCaches(state);
            }

            _tileStates.Remove(tileKey);
            _activeSetDirty = true;
        }

        private int CopyActiveInstances(
            Matrix4x4[] sourceMatrices,
            int[] sourceTypes,
            int count,
            ref Matrix4x4[] matrices,
            ref int[] types)
        {
            if (count <= 0)
                return 0;

            EnsureMatrixCapacity(ref matrices, count);
            EnsureIntCapacity(ref types, count);
            Array.Copy(sourceMatrices, 0, matrices, 0, count);
            Array.Copy(sourceTypes, 0, types, 0, count);
            return count;
        }

        private int CopyActivePayload(
            Matrix4x4[] sourceMatrices,
            HectonVegetationInstanceData[] sourceMetadata,
            int[] sourceTypes,
            int count,
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            if (count <= 0)
                return 0;

            EnsureMatrixCapacity(ref matrices, count);
            EnsureVegetationDataCapacity(ref metadata, count);
            EnsureIntCapacity(ref types, count);
            Array.Copy(sourceMatrices, 0, matrices, 0, count);
            Array.Copy(sourceMetadata, 0, metadata, 0, count);
            Array.Copy(sourceTypes, 0, types, 0, count);
            return count;
        }

        private void TryBootstrapExistingTiles()
        {
            if (_tileStates.Count > 0 || mapMagicBridge == null || mapMagicBridge.RuntimeMapMagicObject == null)
                return;

            TerrainTile[] tiles = mapMagicBridge.RuntimeMapMagicObject.GetComponentsInChildren<TerrainTile>(true); // COLD ALLOC: TerrainTile[] bootstrap snapshot for already-applied tiles - owner: HectonMapMagicVegetationBridge
            if (tiles == null || tiles.Length == 0)
                return;

            for (int i = 0; i < tiles.Length; i++)
            {
                TerrainTile tile = tiles[i];
                if (tile == null || ResolveMainTerrain(tile) == null || IsForeignTile(tile))
                    continue;

                UpsertTileState(tile);
            }
        }

        private void UpsertTileState(TerrainTile tile)
        {
            if (tile == null)
                return;

            Terrain terrain = ResolveMainTerrain(tile);
            if (terrain == null || terrain.terrainData == null)
            {
                RemoveTileState(tile.coord.x, tile.coord.z);
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (!TryResolveLayerIndices(terrainData, out LayerIndices indices))
            {
                RemoveTileState(tile.coord.x, tile.coord.z);
                return;
            }

            long tileKey = PackTileCoord(tile.coord.x, tile.coord.z);
            int oldChunkCountX = 0;
            int oldChunkCountZ = 0;
            if (_tileStates.TryGetValue(tileKey, out TileRuntimeState existingState) && existingState != null)
            {
                oldChunkCountX = existingState.ChunkCountX;
                oldChunkCountZ = existingState.ChunkCountZ;
            }

            TileRuntimeState state = existingState ?? new TileRuntimeState();
            state.TileX = tile.coord.x;
            state.TileZ = tile.coord.z;
            state.Tile = tile;
            state.Terrain = terrain;
            state.TerrainData = terrainData;
            state.TerrainPosition = terrain.GetPosition();
            state.TerrainSize = terrainData.size;
            state.AlphamapResolution = terrainData.alphamapResolution;
            state.HeightmapResolution = terrainData.heightmapResolution;
            state.ChunkCountX = Mathf.Max(1, Mathf.CeilToInt(state.TerrainSize.x / DefaultVirtualChunkSize));
            state.ChunkCountZ = Mathf.Max(1, Mathf.CeilToInt(state.TerrainSize.z / DefaultVirtualChunkSize));
            state.LayerIndices = indices;

            InvalidateTileChunks(tile.coord.x, tile.coord.z, Mathf.Max(oldChunkCountX, state.ChunkCountX), Mathf.Max(oldChunkCountZ, state.ChunkCountZ));
            CacheTileMasks(state, terrainData);
            _tileStates[tileKey] = state;
        }

        private void ResolveRuntimeDependencies()
        {
            if (mapMagicBridge == null)
                mapMagicBridge = MapMagicBridge.Instance;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform != null && (_playerRigidbody == null || _playerRigidbody.transform != playerTransform))
                playerTransform.TryGetComponent(out _playerRigidbody);
        }

        private void UpdatePlayerMotionState(float dt)
        {
            if (playerTransform == null)
            {
                _playerVelocity = Vector3.zero;
                _hasLastPlayerPosition = false;
                return;
            }

            Vector3 currentPosition = playerTransform.position;
            if (_playerRigidbody != null)
            {
                _playerVelocity = _playerRigidbody.linearVelocity;
                _lastPlayerPosition = currentPosition;
                _hasLastPlayerPosition = true;
                return;
            }

            if (!_hasLastPlayerPosition || dt <= 0.0001f)
            {
                _lastPlayerPosition = currentPosition;
                _playerVelocity = Vector3.zero;
                _hasLastPlayerPosition = true;
                return;
            }

            _playerVelocity = (currentPosition - _lastPlayerPosition) / dt;
            _lastPlayerPosition = currentPosition;
        }

        private bool IsForeignTile(TerrainTile tile)
        {
            if (tile == null)
                return true;

            if (mapMagicBridge == null || mapMagicBridge.RuntimeMapMagicObject == null)
                return false;

            return tile.mapMagic != mapMagicBridge.RuntimeMapMagicObject;
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            tickManager.Register((ISlowTickable)this);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister((ITickable)this);
                tickManager.Unregister((ISlowTickable)this);
            }

            _isRegistered = false;
        }

        private void TrySubscribeEvents()
        {
            if (_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied += HandleTileApplied;
            TerrainTile.OnTileMoved += HandleTileMoved;
            HectonFloatingOrigin.OnWorldShift += HandleWorldShift;
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeEvents()
        {
            if (!_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied -= HandleTileApplied;
            TerrainTile.OnTileMoved -= HandleTileMoved;
            HectonFloatingOrigin.OnWorldShift -= HandleWorldShift;
            _eventsSubscribed = false;
        }

        private void HandleWorldShift(Vector3 offset)
        {
            if (!isActiveAndEnabled)
                return;

            ApplyWorldOffsetToAllChunks(offset);
        }

        private void ClearAllResidency()
        {
            if (_selectedChunkCount == 0 && _pendingChunkCount == 0 && !_activeSetDirty)
                return;

            DisposeAllChunkBuildJobs();
            ResetActiveState(clearChunkCache: true);
            ClearRendererBindings();
            ReleaseBuffers();
            _activeSetDirty = false;
        }

        private void ResetActiveState(bool clearChunkCache)
        {
            _desiredChunkCount = 0;
            _selectedChunkCount = 0;
            _pendingChunkCount = 0;
            _cacheValidationChunkCursor = 0;
            _surfaceActiveCount = 0;
            _underwaterActiveCount = 0;
            _densityQueryChunkCount = 0;
            _surfaceDrawBounds = default;
            _underwaterDrawBounds = default;

            if (clearChunkCache)
                ClearChunkPayloadCache();
        }

        private void InitializeChunkPools()
        {
            if (_surfaceChunkPool.Matrices.IsCreated && _underwaterChunkPool.Matrices.IsCreated)
                return;

            int totalBudgetBytes = Mathf.Max(MinimumNativePoolBudgetMb, nativePoolBudgetMb) * 1024 * 1024;
            int totalCapacity = Mathf.Max(1024, totalBudgetBytes / ChunkPoolBytesPerInstance);
            int surfaceCapacity = Mathf.Clamp(Mathf.RoundToInt(totalCapacity * surfacePoolShare), 1024, totalCapacity - 1);
            int underwaterCapacity = Mathf.Max(1024, totalCapacity - surfaceCapacity);
            InitializeChunkPool(ref _surfaceChunkPool, surfaceCapacity, ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount);
            InitializeChunkPool(ref _underwaterChunkPool, underwaterCapacity, ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount);
        }

        private void DisposeChunkPools()
        {
            DisposeChunkPool(ref _surfaceChunkPool);
            DisposeChunkPool(ref _underwaterChunkPool);
            _surfacePoolFreeBlocks = null;
            _underwaterPoolFreeBlocks = null;
            _surfacePoolFreeBlockCount = 0;
            _underwaterPoolFreeBlockCount = 0;
            _chunkPayloadUsedBytes = 0L;
        }

        private void ClearChunkPayloadCache()
        {
            if (_chunkPayloads.Count > 0)
            {
                Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
                while (enumerator.MoveNext())
                    ReleaseChunkPayloadStorage(enumerator.Current.Value);
            }

            _chunkPayloads.Clear();
            _chunkPayloadUsedBytes = 0L;
        }

        private void ReleaseChunkPayloadStorage(ChunkPayload payload)
        {
            _chunkPayloadUsedBytes = Math.Max(0L, _chunkPayloadUsedBytes - GetChunkPayloadStorageBytes(payload));

            if (payload.SurfaceCount > 0)
                FreeChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, payload.SurfaceOffset, payload.SurfaceCount);

            if (payload.UnderwaterCount > 0)
                FreeChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, payload.UnderwaterOffset, payload.UnderwaterCount);
        }

        private void ReleaseChunkPayloadStorage(ChunkKey key)
        {
            if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                ReleaseChunkPayloadStorage(payload);
        }

        private static long GetChunkPayloadStorageBytes(ChunkPayload payload)
        {
            return (long)(payload.SurfaceCount + payload.UnderwaterCount) * ChunkPoolBytesPerInstance;
        }

        private static void InitializeChunkPool(
            ref NativeChunkPool pool,
            int capacity,
            ref PoolBlock[] freeBlocks,
            ref int freeBlockCount)
        {
            if (pool.Matrices.IsCreated && pool.Capacity == capacity)
                return;

            DisposeChunkPool(ref pool);
            pool.Matrices = new NativeArray<Matrix4x4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.Metadata = new NativeArray<HectonVegetationInstanceData>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.Types = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.SemanticTypes = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.BiomeLayers = new NativeArray<byte>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.EdgeDistances = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.FlowDirections = new NativeArray<Vector2>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.FlowVectors = new NativeArray<Vector3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pool.Capacity = capacity;
            EnsurePoolBlockCapacity(ref freeBlocks, 1);
            freeBlocks[0] = new PoolBlock { Offset = 0, Length = capacity };
            freeBlockCount = 1;
        }

        private static void DisposeChunkPool(ref NativeChunkPool pool)
        {
            DisposeNativeArray(ref pool.Matrices);
            DisposeNativeArray(ref pool.Metadata);
            DisposeNativeArray(ref pool.Types);
            DisposeNativeArray(ref pool.SemanticTypes);
            DisposeNativeArray(ref pool.BiomeLayers);
            DisposeNativeArray(ref pool.EdgeDistances);
            DisposeNativeArray(ref pool.FlowDirections);
            DisposeNativeArray(ref pool.FlowVectors);
            pool.Capacity = 0;
        }

        private static bool TryAllocateChunkSlice(
            ref PoolBlock[] freeBlocks,
            ref int freeBlockCount,
            int count,
            out int offset)
        {
            offset = -1;
            if (count <= 0)
                return false;

            for (int i = 0; i < freeBlockCount; i++)
            {
                PoolBlock block = freeBlocks[i];
                if (block.Length < count)
                    continue;

                offset = block.Offset;
                block.Offset += count;
                block.Length -= count;
                if (block.Length > 0)
                {
                    freeBlocks[i] = block;
                }
                else
                {
                    for (int shift = i; shift < freeBlockCount - 1; shift++)
                        freeBlocks[shift] = freeBlocks[shift + 1];

                    freeBlockCount--;
                    if (freeBlockCount >= 0 && freeBlockCount < freeBlocks.Length)
                        freeBlocks[freeBlockCount] = default;
                }

                return true;
            }

            return false;
        }

        private static void FreeChunkSlice(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int offset, int count)
        {
            if (count <= 0 || offset < 0)
                return;

            EnsurePoolBlockCapacity(ref freeBlocks, freeBlockCount + 1);
            int insertIndex = freeBlockCount;
            while (insertIndex > 0 && offset < freeBlocks[insertIndex - 1].Offset)
            {
                freeBlocks[insertIndex] = freeBlocks[insertIndex - 1];
                insertIndex--;
            }

            freeBlocks[insertIndex] = new PoolBlock { Offset = offset, Length = count };
            freeBlockCount++;

            int mergeIndex = Mathf.Max(0, insertIndex - 1);
            while (mergeIndex < freeBlockCount - 1)
            {
                PoolBlock current = freeBlocks[mergeIndex];
                PoolBlock next = freeBlocks[mergeIndex + 1];
                if (current.Offset + current.Length < next.Offset)
                {
                    mergeIndex++;
                    continue;
                }

                current.Length = Mathf.Max(current.Length, (next.Offset + next.Length) - current.Offset);
                freeBlocks[mergeIndex] = current;
                for (int shift = mergeIndex + 1; shift < freeBlockCount - 1; shift++)
                    freeBlocks[shift] = freeBlocks[shift + 1];

                freeBlockCount--;
                freeBlocks[freeBlockCount] = default;
            }
        }

        private static void EnsurePoolBlockCapacity(ref PoolBlock[] blocks, int requiredCount)
        {
            if (blocks != null && blocks.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, requiredCount));
            // COLD ALLOC: PoolBlock[nextCapacity] - chunk-pool free-list growth - owner: HectonMapMagicVegetationBridge
            PoolBlock[] expanded = new PoolBlock[nextCapacity];
            if (blocks != null && blocks.Length > 0)
                Array.Copy(blocks, expanded, blocks.Length);

            blocks = expanded;
        }

        private void EnsureDensityQueryCapacity(int chunkCount)
        {
            if (chunkCount <= 0)
                return;

            EnsureChunkKeyCapacity(ref _densityQueryChunkKeys, chunkCount);
            EnsureDensityChunkRecordCapacity(ref _densityQueryChunksNative, chunkCount);
            EnsureDensityChunkRecordCapacity(ref _densityQueryChunksScratchNative, chunkCount);
            EnsureFloat3Capacity(ref _densityQueryGridNative, chunkCount * DensityGridCellCount);
            EnsureFloat3Capacity(ref _densityQueryGridScratchNative, chunkCount * DensityGridCellCount);
        }

        private void DisposeAllTileNativeCaches()
        {
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
                DisposeTileNativeCaches(enumerator.Current.Value);
        }

        private static void DisposeTileNativeCaches(TileRuntimeState state)
        {
            if (state == null)
                return;

            DisposeTileNativeCacheBuffer(ref state.PrimaryCacheBuffer);
            DisposeTileNativeCacheBuffer(ref state.SecondaryCacheBuffer);
            state.ActiveCacheBufferIndex = 0;
        }

        private static void DisposeTileNativeCacheBuffer(ref TileNativeCacheBuffer buffer)
        {
            DisposeNativeArray(ref buffer.SandMaskNative);
            DisposeNativeArray(ref buffer.RockMaskNative);
            DisposeNativeArray(ref buffer.HeightSamplesNative);
        }

        private static void EnsureTileNativeCacheBufferCapacity(
            TileRuntimeState state,
            int bufferIndex,
            int sampleCount,
            int heightSampleCount)
        {
            if (state == null)
                return;

            TileNativeCacheBuffer buffer = bufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (!buffer.SandMaskNative.IsCreated || buffer.SandMaskNative.Length != sampleCount)
            {
                DisposeNativeArray(ref buffer.SandMaskNative);
                buffer.SandMaskNative = new NativeArray<byte>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!buffer.RockMaskNative.IsCreated || buffer.RockMaskNative.Length != sampleCount)
            {
                DisposeNativeArray(ref buffer.RockMaskNative);
                buffer.RockMaskNative = new NativeArray<byte>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!buffer.HeightSamplesNative.IsCreated || buffer.HeightSamplesNative.Length != heightSampleCount)
            {
                DisposeNativeArray(ref buffer.HeightSamplesNative);
                buffer.HeightSamplesNative = new NativeArray<float>(heightSampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (bufferIndex == 0)
                state.PrimaryCacheBuffer = buffer;
            else
                state.SecondaryCacheBuffer = buffer;
        }

        private void DisposeActiveNativeAggregates()
        {
            DisposeNativeArray(ref _surfaceAggregateMatricesNative);
            DisposeNativeArray(ref _surfaceAggregateDataNative);
            DisposeNativeArray(ref _surfaceAggregateTypesNative);
            DisposeNativeArray(ref _surfaceAggregateSemanticTypesNative);
            DisposeNativeArray(ref _surfaceAggregateBiomeLayersNative);
            DisposeNativeArray(ref _surfaceAggregateFlowDirectionsNative);
            DisposeNativeArray(ref _surfaceAggregateFlowVectorsNative);
            DisposeNativeArray(ref _underwaterAggregateMatricesNative);
            DisposeNativeArray(ref _underwaterAggregateDataNative);
            DisposeNativeArray(ref _underwaterAggregateTypesNative);
            DisposeNativeArray(ref _underwaterAggregateSemanticTypesNative);
            DisposeNativeArray(ref _underwaterAggregateBiomeLayersNative);
            DisposeNativeArray(ref _underwaterAggregateFlowDirectionsNative);
            DisposeNativeArray(ref _underwaterAggregateFlowVectorsNative);
        }

        private void DisposeDensityQuerySnapshot()
        {
            DisposeNativeArray(ref _densityQueryChunksNative);
            DisposeNativeArray(ref _densityQueryGridNative);
            DisposeNativeArray(ref _densityQueryChunksScratchNative);
            DisposeNativeArray(ref _densityQueryGridScratchNative);
            _densityQueryChunkCount = 0;
            _densityQueryChunkLookup.Clear();
        }

        private void CacheTileMasks(TileRuntimeState state, TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return;

            int alphamapResolution = terrainData.alphamapResolution;
            if (alphamapResolution <= 0)
                return;

            int heightResolution = terrainData.heightmapResolution;
            if (heightResolution <= 1)
                return;

            state.AlphamapResolution = alphamapResolution;
            state.HeightmapResolution = heightResolution;

            int sampleCount = alphamapResolution * alphamapResolution;
            int heightSampleCount = heightResolution * heightResolution;
            int writeBufferIndex = state.ActiveCacheBufferIndex == 0 ? 1 : 0;
            EnsureTileNativeCacheBufferCapacity(state, writeBufferIndex, sampleCount, heightSampleCount);
            CaptureTileCacheSignature(
                terrainData,
                out state.AlphamapTextureCount,
                out state.CombinedAlphamapHash,
                out state.CombinedAlphamapUpdateCount,
                out state.HeightmapHash,
                out state.HeightmapUpdateCount);
            unchecked
            {
                state.CacheRevision++;
            }

            // COLD ALLOC: float[,,] - full-tile alphamap snapshot captured only when a MapMagic tile changes - owner: HectonMapMagicVegetationBridge
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapResolution, alphamapResolution);
            // COLD ALLOC: float[,] - full-tile height snapshot captured only when a MapMagic tile changes - owner: HectonMapMagicVegetationBridge
            float[,] heights = terrainData.GetHeights(0, 0, heightResolution, heightResolution);
            TileNativeCacheBuffer writeBuffer = writeBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;
            int writeIndex = 0;
            for (int z = 0; z < alphamapResolution; z++)
            {
                for (int x = 0; x < alphamapResolution; x++)
                {
                    float sandMask = 0f;
                    if (state.LayerIndices.Sand >= 0)
                        sandMask += alphamaps[z, x, state.LayerIndices.Sand];
                    if (state.LayerIndices.GreenSand >= 0)
                        sandMask += alphamaps[z, x, state.LayerIndices.GreenSand];

                    float rockMask = 0f;
                    if (state.LayerIndices.Rock >= 0)
                        rockMask = alphamaps[z, x, state.LayerIndices.Rock];

                    writeBuffer.SandMaskNative[writeIndex] = PackMask01(sandMask);
                    writeBuffer.RockMaskNative[writeIndex] = PackMask01(rockMask);
                    writeIndex++;
                }
            }

            writeIndex = 0;
            float heightScale = state.TerrainSize.y;
            for (int z = 0; z < heightResolution; z++)
            {
                for (int x = 0; x < heightResolution; x++)
                {
                    writeBuffer.HeightSamplesNative[writeIndex] = heights[z, x] * heightScale;
                    writeIndex++;
                }
            }

            state.ActiveCacheBufferIndex = writeBufferIndex;
        }

        private static void CaptureTileCacheSignature(
            TerrainData terrainData,
            out int alphamapTextureCount,
            out int combinedAlphamapHash,
            out uint combinedAlphamapUpdateCount,
            out int heightmapHash,
            out uint heightmapUpdateCount)
        {
            alphamapTextureCount = 0;
            combinedAlphamapHash = 17;
            combinedAlphamapUpdateCount = 0u;
            heightmapHash = 0;
            heightmapUpdateCount = 0u;

            if (terrainData == null)
                return;

            Texture2D[] alphamapTextures = terrainData.alphamapTextures;
            if (alphamapTextures != null)
            {
                alphamapTextureCount = alphamapTextures.Length;
                for (int i = 0; i < alphamapTextures.Length; i++)
                {
                    Texture2D texture = alphamapTextures[i];
                    if (texture == null)
                    {
                        combinedAlphamapHash = (combinedAlphamapHash * 31) ^ i;
                        continue;
                    }

                    combinedAlphamapHash = (combinedAlphamapHash * 31) ^ texture.imageContentsHash.GetHashCode();
                    combinedAlphamapUpdateCount ^= texture.updateCount;
                }
            }

            Texture heightTexture = terrainData.heightmapTexture;
            if (heightTexture == null)
                return;

            heightmapHash = heightTexture.imageContentsHash.GetHashCode();
            heightmapUpdateCount = heightTexture.updateCount;
        }

        private void ClearRendererBindings()
        {
            ClearChannel(surfaceRenderer);
            ClearChannel(underwaterRenderer);
        }

        private static void ClearChannel(HectonIndirectVegetationRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.ClearInstanceBuffer();
            renderer.ClearDrawBoundsOverride();
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _surfaceInstanceBuffer);
            ReleaseBuffer(ref _surfaceInstanceDataBuffer);
            ReleaseBuffer(ref _underwaterInstanceBuffer);
            ReleaseBuffer(ref _underwaterInstanceDataBuffer);
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void EnsureChunkKeyCapacity(ref ChunkKey[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: ChunkKey[nextCapacity] - dynamic chunk key cache growth - owner: HectonMapMagicVegetationBridge
            ChunkKey[] expanded = new ChunkKey[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureFloatCapacity(ref float[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: float[nextCapacity] - dynamic float cache growth - owner: HectonMapMagicVegetationBridge
            float[] expanded = new float[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureMatrixCapacity(ref Matrix4x4[] matrixCache, int requiredCount)
        {
            if (matrixCache != null && matrixCache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Matrix4x4[nextCapacity] - streamed matrix cache growth - owner: HectonMapMagicVegetationBridge
            Matrix4x4[] expanded = new Matrix4x4[nextCapacity];
            if (matrixCache != null && matrixCache.Length > 0)
                Array.Copy(matrixCache, expanded, matrixCache.Length);

            matrixCache = expanded;
        }

        private static void EnsureVegetationDataCapacity(ref HectonVegetationInstanceData[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: HectonVegetationInstanceData[nextCapacity] - streamed metadata cache growth - owner: HectonMapMagicVegetationBridge
            HectonVegetationInstanceData[] expanded = new HectonVegetationInstanceData[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureIntCapacity(ref int[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: int[nextCapacity] - streamed vegetation type cache growth - owner: HectonMapMagicVegetationBridge
            int[] expanded = new int[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureByteCapacity(ref byte[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: byte[nextCapacity] - streamed biome-layer cache growth - owner: HectonMapMagicVegetationBridge
            byte[] expanded = new byte[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureVector2Capacity(ref Vector2[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Vector2[nextCapacity] - streamed flow-direction cache growth - owner: HectonMapMagicVegetationBridge
            Vector2[] expanded = new Vector2[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureVector3Capacity(ref Vector3[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: Vector3[nextCapacity] - streamed 3D flow-vector cache growth - owner: HectonMapMagicVegetationBridge
            Vector3[] expanded = new Vector3[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureBoolCapacity(ref bool[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(InitialChunkArrayCapacity, requiredCount));
            // COLD ALLOC: bool[nextCapacity] - selected chunk visibility cache growth - owner: HectonMapMagicVegetationBridge
            bool[] expanded = new bool[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private static void EnsureMatrixNativeCapacity(ref NativeArray<Matrix4x4> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVegetationDataNativeCapacity(ref NativeArray<HectonVegetationInstanceData> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureIntNativeCapacity(ref NativeArray<int> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureByteNativeCapacity(ref NativeArray<byte> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVector2NativeCapacity(ref NativeArray<Vector2> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureVector3NativeCapacity(ref NativeArray<Vector3> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureDensityChunkRecordCapacity(ref NativeArray<VegetationDensityChunkRecord> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat3Capacity(ref NativeArray<float3> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureNativeCapacity<T>(ref NativeArray<T> cache, int requiredCount)
            where T : struct
        {
            if (requiredCount <= 0)
                return;

            if (cache.IsCreated && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            NativeArray<T> expanded = new NativeArray<T>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cache.IsCreated && cache.Length > 0)
            {
                NativeArray<T>.Copy(cache, expanded, cache.Length);
                cache.Dispose();
            }

            cache = expanded;
        }

        private static void CopyNativeToManaged<T>(NativeArray<T> source, int sourceIndex, T[] destination, int destinationIndex, int copyCount)
            where T : struct
        {
            for (int i = 0; i < copyCount; i++)
                destination[destinationIndex + i] = source[sourceIndex + i];
        }

        private static uint BuildSampleSeed(int tileX, int tileZ, int sampleX, int sampleZ)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)tileX) * 16777619u;
                hash = (hash ^ (uint)tileZ) * 16777619u;
                hash = (hash ^ (uint)sampleX) * 16777619u;
                hash = (hash ^ (uint)sampleZ) * 16777619u;
                return hash;
            }
        }

        private static uint BuildCellSeed(int cellX, int cellZ, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)cellX) * 16777619u;
                hash = (hash ^ (uint)cellZ) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash;
            }
        }

        private static byte PackMask01(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value) * 255f), 0, 255);
        }

        private static Vector2 NormalizeFlowDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector2.right;

            direction.Normalize();
            return direction;
        }

        private static float Hash01(uint seed)
        {
            unchecked
            {
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16;
                return (seed & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
