using System;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Streams vegetation data from MapMagic tiles into virtual 100x100 meter chunks
    /// and keeps only the player-near residency ring bound to the indirect renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HectonMapMagicVegetationBridge : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IMapMagicTerrainTileEventListener, IAbyssalFlowVolumeReadModel, ITerrainHeightSampleReadModel, IVegetationThreatReadModel, IVegetationThreatPulseSink, IGlobalRegistryHotSwapListener
    {
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct PredatorFearNodeSnapshot
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public float Radius;
            [FieldOffset(16)]
            public float Weight;
            [FieldOffset(20)]
            public int SpeciesId;
            [FieldOffset(24)]
            public float Padding;
            [FieldOffset(28)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AbyssalPathTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public int RawCount;
            [FieldOffset(8)]
            public int OutputCount;
            [FieldOffset(12)]
            public int PortalLookAhead;
            [FieldOffset(16)]
            public int MaxDdaSamples;
            [FieldOffset(20)]
            public float FunnelMs;
            [FieldOffset(24)]
            public float StartX;
            [FieldOffset(28)]
            public float StartY;
            [FieldOffset(32)]
            public float StartZ;
            [FieldOffset(36)]
            public float EndX;
            [FieldOffset(40)]
            public float EndY;
            [FieldOffset(44)]
            public float EndZ;
            [FieldOffset(48)]
            public uint Flags;
            [FieldOffset(52)]
            public uint Sequence;
            [FieldOffset(56)]
            public uint Padding0;
            [FieldOffset(60)]
            public uint Padding1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AbyssalPathStagingPoint
        {
            [FieldOffset(0)]
            public Vector3 Raw;
            [FieldOffset(12)]
            public Vector3 Result;
            [FieldOffset(24)]
            public int RawCount;
            [FieldOffset(28)]
            public int ResultCount;
            [FieldOffset(32)]
            public int RawFlags;
            [FieldOffset(36)]
            public int ResultFlags;
            [FieldOffset(40)]
            public int Parent;
            [FieldOffset(44)]
            public int HeapNode;
            [FieldOffset(48)]
            public int HeapPosition;
            [FieldOffset(52)]
            public byte ClosedFlag;
            [FieldOffset(53)]
            public byte ScratchFlags;
            [FieldOffset(54)]
            private ushort _pad0;
            [FieldOffset(56)]
            public float GScore;
            [FieldOffset(60)]
            public float FScore;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ThreatPropagationStagingPoint
        {
            [FieldOffset(0)]
            public float PreviousThreat;
            [FieldOffset(4)]
            public float NextThreat;
            [FieldOffset(8)]
            public byte PreviousEcho;
            [FieldOffset(9)]
            public byte NextCompressed;
            [FieldOffset(10)]
            public byte NextEcho;
            [FieldOffset(11)]
            public byte Voxel;
            [FieldOffset(12)]
            public uint Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct FlowFieldStagingPoint
        {
            [FieldOffset(0)]
            public float Threat;
            [FieldOffset(4)]
            public float NavSupport;
            [FieldOffset(8)]
            public float2 Flow;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ThermalGridStagingPoint
        {
            [FieldOffset(0)]
            public float3 PreviousFlow;
            [FieldOffset(12)]
            public float Thermal;
            [FieldOffset(16)]
            public float3 Flow;
            [FieldOffset(28)]
            public uint Padding;
        }

        private struct PredatorFearNodeState
        {
            public float3 Position;
            public float Radius;
            public float Weight;
            public float ExpireTime;
            public int SpeciesId;
        }

        private const string SandLayerName = "L_Sand";
        private const string GreenSandLayerName = "L_sandGreen";
        private const string RockLayerName = "L_Rocks";
        private const string Batch34ClaySiltLayerName = "L_B34_3408_ClaySiltTurbiditySlope";
        private const string Batch34ShellSandLayerName = "L_B34_3401_PhoticLimestoneRubbleShelf";
        private const string Batch34RootMatLayerName = "L_B34_3402_ShallowSeagrassRootMat";
        private const string Batch34SerpentiniteLayerName = "L_B34_3406_SerpentiniteFaultRock";
        private const string Batch34BrineSaltLayerName = "L_B34_3403_BrineCanyonSaltCrustSilt";
        private const string Batch34ManganeseLayerName = "L_B34_3404_AbyssalManganeseNodulePlain";
        private const string Batch34MethaneHydrateLayerName = "L_B34_3405_MethaneHydrateCrackVein";
        private const string Batch34LimestoneCaveLayerName = "L_B34_3409_LimestoneCaveCeilingMineralDrip";
        private const float DefaultWaterLevel = 14.02f;
        private const float WaterLevelResyncEpsilonMeters = 0.05f;
        private const float OrganicKelpMaxDepthBelowSurfaceMeters = 200f;
        private const float OrganicKelpMaxSlopeNormalY = 0.8660254f;
        private const float DefaultKelpMinHeight = DefaultWaterLevel - OrganicKelpMaxDepthBelowSurfaceMeters;
        private const float DefaultVirtualChunkSize = 100f;
        private const float CameraResolveRetryInterval = 1f;
        private const float CacheValidationInterval = 0.5f;
        private const int CacheValidationTileBudget = 2;
        private const int StartupBootstrapTileBatchSize = 2;
        private const int TerrainHoleJobBatchSize = 64;
        private const int DefaultJobBatchSize = 32;
        private const int MaxConcurrentChunkBuildJobs = 4;
        private const int MaxPublicDensityQuerySnapshotLeases = 4;
        private const int InitialTileCapacity = 32;
        private const int TileCacheLruCapacity = 64;
        private const int StartupBootstrapTileSnapshotCapacity = TileCacheLruCapacity * 4;
        private const int MinimumTerrainHoleRuntimeCapacity = TileCacheLruCapacity * 2;
        private const int TileNativeCacheSlotCapacity = TileCacheLruCapacity * 4;
        private const int TerrainHoleTileScheduleBudgetPerSlowTick = 1;
        private const int TileNativeCacheBufferStride = 8;
        private const int TileNativeCachePrimaryOffset = 0;
        private const int TileNativeCacheSecondaryOffset = 3;
        private const int TileNativeCacheSandOffset = 0;
        private const int TileNativeCacheRockOffset = 1;
        private const int TileNativeCacheHeightOffset = 2;
        private const int TileNativeTerrainHoleMaskOffset = 6;
        private const int InitialChunkCapacity = 256;
        private const int InitialChunkArrayCapacity = 64;
        private const int InitialCorruptedChunkCapacity = 512;
        private const int MaxPersistentArtificialStructureRecords = 256;
        private const int ChunkPoolBytesPerInstance = 128;
        private const int ActiveAggregateDirtyPageSize = 256;
        private const BufferID SurfaceAggregateFrontMatrixDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_SurfaceAggregateFrontMatrixDirtyPagesId;
        private const BufferID SurfaceAggregateFrontMetadataDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_SurfaceAggregateFrontMetadataDirtyPagesId;
        private const BufferID SurfaceAggregateBackMatrixDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_SurfaceAggregateBackMatrixDirtyPagesId;
        private const BufferID SurfaceAggregateBackMetadataDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_SurfaceAggregateBackMetadataDirtyPagesId;
        private const BufferID UnderwaterAggregateFrontMatrixDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_UnderwaterAggregateFrontMatrixDirtyPagesId;
        private const BufferID UnderwaterAggregateFrontMetadataDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_UnderwaterAggregateFrontMetadataDirtyPagesId;
        private const BufferID UnderwaterAggregateBackMatrixDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_UnderwaterAggregateBackMatrixDirtyPagesId;
        private const BufferID UnderwaterAggregateBackMetadataDirtyPagesId = BufferID.HectonMapMagicVegetationBridge_UnderwaterAggregateBackMetadataDirtyPagesId;
        private const uint FlowFieldPinThreatGrid = 1u << 0;
        private const uint FlowFieldPinNavNodes = 1u << 1;
        private const uint FlowFieldPinDensityChunks = 1u << 2;
        private const uint FlowFieldPinDensityGrid = 1u << 3;
        private const uint FlowFieldPinThreatAttractorGrid = 1u << 4;
        private const uint ThermalGridPinPreviousFlowVolume = 1u << 0;
        private const uint ThermalGridPinDensityChunks = 1u << 1;
        private const uint ThermalGridPinThreatAttractorGrid = 1u << 2;
        private const uint ThreatPropagationPinArtificialStructures = 1u << 0;
        private const uint ThreatPropagationPinDensityChunks = 1u << 1;
        private const uint ThreatPropagationPinDensityGrid = 1u << 2;
        private const uint ThreatPropagationPinThreatAttractorGrid = 1u << 3;
        private const uint ChunkBuildPinArtificialStructures = 1u << 0;
        private const uint ChunkBuildPinThreatEcho = 1u << 1;
        private const uint ChunkBuildPinTerrainHoles = 1u << 2;
        private const uint ChunkBuildPinTileSandMask = 1u << 3;
        private const uint ChunkBuildPinTileRockMask = 1u << 4;
        private const uint ChunkBuildPinTileHeightSamples = 1u << 5;
        private const int MinimumNativePoolBudgetMb = 64;
        private const double MaxRuntimeFloatCoordinate = 1048576.0;
        private const int DensityGridResolution = VegetationMath.DensityGridResolution;
        private const int DensityGridCellCount = DensityGridResolution * DensityGridResolution;
        private const float DensityQuerySeedScale = 2f;
        private const int VegetationAudioProbeCount = 5;
        private const uint KccVelocityVegetationMaxAgeFrames = 12u;
        internal const int DensityTypeMaskGrass = 1 << 0;
        internal const int DensityTypeMaskKelp = 1 << 1;
        internal const int DensityTypeMaskSargassum = 1 << 2;
        private const int DensityTypeMaskAll = DensityTypeMaskGrass | DensityTypeMaskKelp | DensityTypeMaskSargassum;
        private const byte FloraSubstrateSandBit = 1 << 0;
        private const byte FloraSubstrateRockBit = 1 << 1;
        private const float DefaultThreatGridRadius = 1000f;
        private const float DefaultThreatGridCellSize = 10f;
        private const int DefaultPredatorFearNodeCapacity = 32;
        private const float DefaultPredatorFearSectorSizeMeters = 1000f;
        private const float DefaultPredatorFearLifetimeSeconds = 900f;
        private const float DefaultAbyssalNavGraphCellSize = 64f;
        private const int InvalidTerrainHoleId = 0;
        private const int InvalidArtificialStructureId = 0;
        private const float DefaultTerrainHoleEvictionDistance = 3000f;
        private const float DefaultThermalGridRadius = 1000f;
        private const float DefaultThermalGridHorizontalCellSize = 50f;
        private const float DefaultThermalGridVerticalCellSize = 250f;
        private const float BiolumeSurgeDurationSeconds = 4f;
        private const float BiolumeSurgeVelocityDeltaThreshold = 8f;
        private const float VegetationRuntimeClockMaxSeconds = 16777215f;
        private const float DefaultThermalGridDepthMeters = 4000f;
        private const float AbyssalFlowNoiseStartDepthMeters = 2000f;
        private const float ScatterMinimumSurfaceNormalUpDot = OrganicKelpMaxSlopeNormalY;
        private const float ScatterMinimumSurfaceNormalUpDotSq = ScatterMinimumSurfaceNormalUpDot * ScatterMinimumSurfaceNormalUpDot;
        private const int MaxTileCacheLruIterations = 512;
        private const int MaxChunkPoolEvictionIterations = 2048;
        private const int MaxPathReconstructionIterations = 4096;
        private const int MaxHeapRebalanceIterations = 4096;
        private const int MaxThreatDdaSteps = 4096;
        private const int MaxPathCompactionIterations = 4096;
        private const int AbyssalPathOverflowFlag = 1;
        private const uint AbyssalPathPinPredatorFear = 1u << 0;
        private const uint AbyssalPathPinNavNodes = 1u << 1;
        private const uint AbyssalPathPinNavNodeTypes = 1u << 2;
        private const uint AbyssalPathPinConduitVectors = 1u << 3;
        private const uint AbyssalPathPinConduitStrengths = 1u << 4;
        private const uint AbyssalPathPinThreatGrid = 1u << 5;
        private const uint AbyssalPathPinThreatVoxel = 1u << 6;
        private const uint AbyssalPathPinArtificialStructures = 1u << 7;
        private const uint AbyssalPathPinTerrainHoles = 1u << 8;
        private const uint AbyssalPathPinDensityChunks = 1u << 9;
        private const uint AbyssalPathPinDensityGrid = 1u << 10;
        private const uint AbyssalPathPinThreatAttractorGrid = 1u << 11;
        private const int LowTierAbyssalPathPortalLookAhead = 4;
        private const int MidTierAbyssalPathPortalLookAhead = 8;
        private const int HighTierAbyssalPathPortalLookAhead = 16;
        private const int LowTierAbyssalPathDdaSamples = 32;
        private const int MidTierAbyssalPathDdaSamples = 64;
        private const int AbyssalPathTelemetryFrameCount = 300;
        private const int AbyssalPathTelemetryDumpHeaderBytes = 20;
        private const int AbyssalPathTelemetryDumpRowBytes = 56;
        private const int AbyssalPathTelemetryDumpPayloadBytes =
            AbyssalPathTelemetryDumpHeaderBytes + AbyssalPathTelemetryFrameCount * AbyssalPathTelemetryDumpRowBytes;
        private const int DefaultMaxAbyssalNavNodeCapacity = 8192;
        private const int DefaultMaxAbyssalPathWaypointCapacity = 8192;
        private const uint AbyssalPathTelemetryContextHash = 0x41504154u;
        private const uint AbyssalPathOverBudgetHash = 0x46554E4Cu;
        private const uint AbyssalPathNanFaultHash = 0x4E414E46u;
        private const BufferID AbyssalPathTelemetryBufferId = BufferID.AbyssalPathTelemetryRing;
        private const SystemID AbyssalPathTelemetryOwner = SystemID.WorldStreaming;
        private const string NativeMemoryOwner = nameof(HectonMapMagicVegetationBridge);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int _ShaderVegetationAudioDensityId = Shader.PropertyToID("_HectonVegetationAudioDensity");
        private static readonly int _ShaderVegetationAudioAcousticTypeId = Shader.PropertyToID("_HectonVegetationAudioAcousticType");
        private const float PlayerVisibilityDenseCoverThreshold = 0.32f;
        private const float PlayerVisibilityThreatExposureThreshold = 0.45f;

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
        private IPlayerRuntimeContext _playerRuntimeContext;

        [SerializeField]
        [Tooltip("Indirect renderer used for surface grass and floating sargassum.")]
        private HectonIndirectVegetationRenderer surfaceRenderer;

        [SerializeField]
        [Tooltip("Indirect renderer used for underwater kelp.")]
        private HectonIndirectVegetationRenderer underwaterRenderer;

        [SerializeField]
        [Tooltip("Authored flora templates used to stamp harvest/shader descriptors into streamed indirect vegetation instances.")]
        private FloraDataTemplate[] floraTemplates;

        [SerializeField]
        [Tooltip("Gameplay camera used for frustum culling. If null, resolved from the player hierarchy.")]
        private Camera viewCamera;

        [Header("Streaming")]
        [SerializeField, Range(150f, 200f)]
        [Tooltip("Residency radius in meters for virtual vegetation chunks.")]
        private float residentRadius = 180f;

        [SerializeField, Range(1f, 1.5f)]
        [Tooltip("Eviction hysteresis multiplier applied to already resident chunks so residency does not thrash at the boundary.")]
        private float residentHysteresisScale = 1.2f;

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
        [SerializeField]
        [Tooltip("Project water surface level. Runtime syncs from the active MapMagic terrain bridge when available.")]
        private float waterLevel = DefaultWaterLevel;

        [SerializeField]
        [Tooltip("Minimum terrain height that still accepts organic kelp placement. Runtime clamp enforces the 200m depth cap below the active water surface.")]
        private float kelpMinHeight = DefaultKelpMinHeight;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum combined sand mask required for any vegetation sample.")]
        private float sandMaskThreshold = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum rock mask that blocks a vegetation sample.")]
        private float rockMaskThreshold = 0.5f;

        [SerializeField, Range(OrganicKelpMaxSlopeNormalY, 1f)]
        [Tooltip("Minimum allowed terrain normal Y for ground-anchored vegetation. Runtime never drops below the 30-degree slope gate.")]
        private float minimumNormalY = OrganicKelpMaxSlopeNormalY;

        [SerializeField, Min(0f)]
        [Tooltip("Offset along the sampled terrain normal for anchored vegetation.")]
        private float normalOffset = 0.04f;

#pragma warning disable CS0414
        [SerializeField, Min(1f)]
        [Tooltip("Reserved for deferred scatter snap ray batches; runtime snap currently uses resident terrain cache to avoid main-thread job stalls.")]
        private float scatterSnapRaycastElevationMeters = 24f;

        [SerializeField, Min(1f)]
        [Tooltip("Reserved for deferred scatter snap ray batches; runtime snap currently uses resident terrain cache to avoid main-thread job stalls.")]
        private float scatterSnapRaycastDistanceMeters = 96f;
#pragma warning restore CS0414

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

        [Header("Ecosystem Threat Matrix")]
        [SerializeField, Min(100f)]
        [Tooltip("World-space radius in meters covered by the ecosystem threat grid around the player.")]
        private float threatGridRadius = DefaultThreatGridRadius;

        [SerializeField, Min(1f)]
        [Tooltip("Cell size in meters used by the ecosystem threat grid.")]
        private float threatGridCellSize = DefaultThreatGridCellSize;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction of local threat blended toward the neighbor average every propagation step.")]
        private float threatDiffusion = 0.22f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Threat decay rate per second applied after diffusion so abandoned water calms down.")]
        private float threatDecayPerSecond = 0.28f;

        [SerializeField, Min(0f)]
        [Tooltip("Base threat deposit per second emitted by locomotion noise around the player.")]
        private float threatNoiseDepositPerSecond = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional threat deposit per second emitted while the player's flashlight is active.")]
        private float threatFlashlightDepositPerSecond = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional threat deposit per second injected by tool pulses and transport signatures.")]
        private float threatPulseDepositPerSecond = 0.9f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum emission radius in meters used when the player is generating threat.")]
        private float threatEmissionRadiusMin = 18f;

        [SerializeField, Min(1f)]
        [Tooltip("Maximum emission radius in meters used when noise, transport, or flashlight are strong.")]
        private float threatEmissionRadiusMax = 120f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum extra retention applied inside dense floating sargassum cells.")]
        private float threatSargassumRetentionBoost = 0.28f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum extra retention applied inside dense colony techno-jungle cells.")]
        private float threatTechnoJungleRetentionBoost = 0.34f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional local threat amplification applied inside dense floating sargassum cells.")]
        private float threatSargassumAccumulationBoost = 0.65f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional local threat amplification applied inside dense colony techno-jungle cells.")]
        private float threatTechnoJungleAccumulationBoost = 0.9f;

        [Header("Abyssal Flow Field")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Weight applied to local threat gradients when building the abyssal flow-field.")]
        private float flowFieldThreatBias = 0.85f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Fallback attraction weight toward the player when no strong hotspot is available.")]
        private float flowFieldPlayerBias = 0.55f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Attraction weight toward the strongest threat hotspot when it exists.")]
        private float flowFieldHotspotBias = 1.1f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Weight applied to obstacle avoidance gradients so vectors slide around dense clutter.")]
        private float flowFieldObstacleAvoidBias = 1.15f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Weight applied to abyssal nav-node support so the field prefers known safe corridors.")]
        private float flowFieldNavSupportBias = 0.7f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Obstacle weight contributed by dense kelp volume when computing the flow-field.")]
        private float flowFieldKelpObstacleWeight = 0.45f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Obstacle weight contributed by dense floating sargassum when computing the flow-field.")]
        private float flowFieldSargassumObstacleWeight = 0.72f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Obstacle weight contributed by colony/Dead Zone techno structures when computing the flow-field.")]
        private float flowFieldTechnoObstacleWeight = 1.35f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Obstacle intensity where avoidance starts blending into the preferred seek vector.")]
        private float flowFieldObstacleSoftThreshold = 0.32f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Obstacle intensity above which non-supported cells are treated as effectively impassable.")]
        private float flowFieldObstacleHardThreshold = 0.82f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum hotspot threat required before the flow-field prefers the hotspot over direct player pursuit.")]
        private float flowFieldHotspotMinimumThreat = 0.12f;

        [SerializeField, Range(0, 3)]
        [Tooltip("Cell-radius stencil used when stamping abyssal nav nodes into the flow-field support grid.")]
        private int flowFieldNavStencilRadiusCells = 1;

        [Header("Corruption")]
        [SerializeField, Min(32)]
        [Tooltip("Maximum runtime corrupted-chunk states retained across the currently streamed tile set.")]
        private int maxTrackedCorruptedChunks = 512;

        [Header("Artificial Structures")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Threat suppression applied when the cellular threat grid overlaps player-built safe structures.")]
        private float artificialStructureThreatSuppression = 0.42f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Threat attraction injected by hostile artificial structures so special AI can bias toward them.")]
        private float artificialStructureHazardAttraction = 0.2f;

        [Header("Abyssal Nav Nodes")]
        [SerializeField, Min(4f)]
        [Tooltip("Coarse sampling step in meters used when extracting abyssal safe nodes from finalized deep chunks.")]
        private float abyssalNavNodeStepMeters = 20f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Hover height above sampled terrain used for abyssal safe nodes.")]
        private float abyssalNavNodeHoverHeight = 10f;

        [SerializeField, Min(1f)]
        [Tooltip("Horizontal radius in meters used to score nearby obstacles around an abyssal safe node candidate.")]
        private float abyssalNavNodeObstacleRadius = 14f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Maximum vertical delta in meters for obstacles counted against an abyssal safe node candidate.")]
        private float abyssalNavNodeObstacleVerticalWindow = 16f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum weighted obstacle density allowed for a valid abyssal safe node.")]
        private float abyssalNavNodeMaxObstacleDensity = 1.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum average current magnitude allowed before a candidate is rejected as an abnormal current lane.")]
        private float abyssalNavNodeMaxCurrentMagnitude = 1.65f;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum nearby deep-biome affinity required before a candidate is accepted as an abyssal nav node.")]
        private float abyssalNavNodeMinimumDeepAffinity = 0.35f;

        [Header("Abyssal Pathfinding")]
        [SerializeField, Min(4f)]
        [Tooltip("Maximum horizontal edge length in meters used when linking abyssal nav nodes for native A* search.")]
        private float abyssalPathNeighborRadius = 34f;

        [SerializeField, Min(4f)]
        [Tooltip("Spatial-hash cell size in meters used by the native abyssal nav graph so nearest-node lookup does not degenerate into a full scan.")]
        private float abyssalNavGraphCellSize = DefaultAbyssalNavGraphCellSize;

        [SerializeField, Min(1f)]
        [Tooltip("Maximum vertical delta in meters allowed when linking abyssal nav nodes for native A* search.")]
        private float abyssalPathVerticalTolerance = 22f;

        [SerializeField, Min(0f)]
        [Tooltip("Traversal penalty applied to low-threat water so predators prefer routes biased toward fresh player noise.")]
        private float abyssalPathThreatPenaltyWeight = 10f;

        [SerializeField, Min(0f)]
        [Tooltip("Depth in meters below the waterline where abyssal current conductors start biasing native A* links.")]
        private float abyssalConduitStartDepth = 3000f;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum averaged current magnitude required before a deep nav node is promoted into a current conductor.")]
        private float abyssalConduitMinimumFlowMagnitude = 0.9f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional vertical edge tolerance granted when a link is strongly aligned with a deep current conductor.")]
        private float abyssalConduitVerticalToleranceBonus = 28f;

        [SerializeField, Min(0f)]
        [Tooltip("Cost penalty applied when a deep path edge fights against the local current conductor direction.")]
        private float abyssalConduitMisalignmentPenalty = 7f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum fractional reward applied when a deep path edge aligns with a current conductor.")]
        private float abyssalConduitAlignmentReward = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Retarget distance in meters required before the abyssal A* endpoint is allowed to jump to a new node.")]
        private float abyssalPathRetargetDistance = 30f;

        [SerializeField, Range(64, 8192)]
        [Tooltip("Hard cap on node expansions for a single native abyssal A* solve.")]
        private int abyssalPathMaxExpandedNodes = 2048;

        [SerializeField, Range(256, 32768)]
        [Tooltip("Fixed persistent capacity for abyssal nav nodes. Runtime overflow is truncated; containers are never expanded in play.")]
        private int maxAbyssalNavNodeCapacity = DefaultMaxAbyssalNavNodeCapacity;

        [SerializeField, Range(64, 32768)]
        [Tooltip("Fixed persistent capacity for raw and smoothed abyssal path waypoints. Runtime overflow fails closed instead of resizing.")]
        private int maxAbyssalPathWaypointCapacity = DefaultMaxAbyssalPathWaypointCapacity;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Traversal cost multiplier applied to interior abyssal nav nodes so caves and wreck interiors can use a distinct pathing weight.")]
        private float abyssalInteriorTraversalCostMultiplier = 1f;

        [Header("Threat Echoes")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum retained threat level in cells that have permanently echoed a peak disturbance.")]
        private float permanentThreatEchoFloor = 0.3f;

        [SerializeField, Range(0.9f, 1f)]
        [Tooltip("Threat saturation threshold that promotes a cell into a permanent echo.")]
        private float permanentThreatEchoThreshold = 0.999f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Additional predator spawn-weight multiplier contributed by local threat. 1.0 threat yields +300% chance when set to 3.")]
        private float predatorSpawnThreatBonusMultiplier = 3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Threshold reduction applied to techno-jungle occupancy checks inside permanent echo cells so extra bio-cables regrow there.")]
        private float permanentEchoTechnoJungleThresholdBias = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Extra dead-zone keep chance injected when a permanent echo overlaps a dead-zone structure candidate.")]
        private float permanentEchoDeadZoneKeepBoost = 0.18f;

        [Header("Predator Fear Memory")]
        [SerializeField, Range(4, 128)]
        [Tooltip("Maximum active predator fear sectors retained in the bridge-owned navigation memory.")]
        private int predatorFearNodeCapacity = DefaultPredatorFearNodeCapacity;

        [SerializeField, Min(120f)]
        [Tooltip("Lifetime in seconds for predator fear sectors before they decay out of the pathing memory.")]
        private float predatorFearLifetimeSeconds = DefaultPredatorFearLifetimeSeconds;

        [SerializeField, Min(100f)]
        [Tooltip("Sector size in meters used when snapping fear writes onto the AUP ecosystem grid.")]
        private float predatorFearSectorSizeMeters = DefaultPredatorFearSectorSizeMeters;

        [SerializeField, Min(1f)]
        [Tooltip("Horizontal radius in meters sampled around a snapped predator fear sector.")]
        private float predatorFearNodeRadiusMeters = 500f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional A* cost weight injected for predators traversing their remembered kill sectors.")]
        private float predatorFearPathPenaltyWeight = 1.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Immediate cognition pressure scale sampled from active predator fear sectors.")]
        private float predatorFearCognitionPressureScale = 0.85f;

        [Header("Abyssal Thermal Grid")]
        [SerializeField, Min(100f)]
        [Tooltip("Horizontal radius in meters covered by the 3D abyssal thermal grid around the player.")]
        private float thermalGridRadius = DefaultThermalGridRadius;

        [SerializeField, Min(5f)]
        [Tooltip("Horizontal cell size in meters used by the abyssal thermal grid.")]
        private float thermalGridHorizontalCellSize = DefaultThermalGridHorizontalCellSize;

        [SerializeField, Min(10f)]
        [Tooltip("Vertical layer height in meters used by the abyssal thermal grid.")]
        private float thermalGridVerticalCellSize = DefaultThermalGridVerticalCellSize;

        [SerializeField, Min(500f)]
        [Tooltip("Maximum sampled water depth in meters covered by the abyssal thermal grid.")]
        private float thermalGridDepthMeters = DefaultThermalGridDepthMeters;

        [SerializeField, Range(-5f, 40f)]
        [Tooltip("Resolved surface-water temperature in Celsius used as the thermal-grid warm anchor.")]
        private float thermalSurfaceTemperatureCelsius = 23f;

        [SerializeField, Range(-10f, 20f)]
        [Tooltip("Resolved abyssal baseline temperature in Celsius at the deepest sampled layer.")]
        private float thermalAbyssTemperatureCelsius = -1.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Meters below the waterline where the thermal falloff starts accelerating toward abyssal cold.")]
        private float thermalThermoclineDepth = 350f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Exponent applied after the thermocline so deep water falls off faster than a linear blend.")]
        private float thermalDepthFalloffExponent = 1.45f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Maximum additional Celsius injected by colony-reactor and black-smoker thermal pockets.")]
        private float thermalHotPocketBoostCelsius = 18f;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Low-frequency noise scale used to carve deterministic thermal pockets inside colony and dead-zone depths.")]
        private float thermalHotPocketNoiseScale = 0.0019f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Noise threshold above which a thermal pocket starts contributing heat.")]
        private float thermalHotPocketThreshold = 0.63f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Multiplier applied to colony graveyard heat pockets derived from techno-jungle attractors.")]
        private float thermalColonyPocketStrength = 0.95f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Multiplier applied to deep dead-zone smoker pockets below the colony graveyard band.")]
        private float thermalDeadZonePocketStrength = 1.2f;

        [Header("Mega Wreck Streaming")]
        [SerializeField]
        [Tooltip("Deterministic mega-wreck definitions that publish per-chunk section requests for giant composite prefabs.")]
        private MegaWreckDefinition[] megaWreckDefinitions = Array.Empty<MegaWreckDefinition>();

        [SerializeField, Min(0f)]
        [Tooltip("Additional horizontal padding applied when converting active mega-wreck interior sections into terrain-hole clutter masks.")]
        private float megaWreckInteriorHolePadding = 8f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum terrain-hole radius used when masking vegetation inside active mega-wreck interiors.")]
        private float megaWreckInteriorMinimumHoleRadius = 18f;

        [SerializeField, Range(0f, 10f)]
        [Tooltip("Water temperature threshold in Celsius below which the survival system treats the cell as a deep-cold pocket.")]
        private float deepColdPocketTemperatureThresholdCelsius = 1.5f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum thermal-stress multiplier exported when the player sits in the coldest abyssal pocket.")]
        private float deepColdPocketStressMultiplierMax = 2.25f;

        [Header("Abyssal Flow")]
        [SerializeField, Min(0.0001f)]
        [Tooltip("3D simplex-noise scale used to perturb deep flow vectors below the 2000 m abyssal noise gate.")]
        private float abyssalFlowNoiseScale = 0.0035f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Horizontal chaos weight injected into deep flow vectors below the 2000 m abyssal noise gate.")]
        private float abyssalFlowNoiseStrength = 1.15f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Vertical vortex weight injected into deep flow vectors below the 2000 m abyssal noise gate.")]
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

        [Header("Terrain Holes")]
        [SerializeField, Min(4)]
        [Tooltip("Initial grow-only capacity for cave-entrance terrain-hole records registered by external systems.")]
        private int initialTerrainHoleCapacity = 16;

        [Header("Native Pool Defragmentation")]
        [SerializeField, Min(1f)]
        [Tooltip("Seconds the player must remain nearly stationary before native pool defragmentation is allowed to run.")]
        private float nativePoolDefragIdleSeconds = 5f;

        [SerializeField, Range(1f, 100f)]
        [Tooltip("Minimum combined pool fragmentation percent required before the background defrag job is scheduled.")]
        private float nativePoolDefragThresholdPercent = 18f;

        [SerializeField, Range(0.01f, 2f)]
        [Tooltip("Maximum planar speed in m/s still treated as idle for background pool defragmentation.")]
        private float nativePoolDefragIdleSpeedThreshold = 0.2f;

        [Header("Abyssal Path Smoothing")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Distance in meters between LOS obstacle probes used by the Burst string-pulling pass.")]
        private float abyssalPathSmoothingSampleSpacing = 10f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Combined biomass obstacle threshold above which LOS smoothing treats the segment as blocked.")]
        private float abyssalPathSmoothingObstacleThreshold = 0.42f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Weight applied to kelp density while evaluating LOS blockage during path smoothing.")]
        private float abyssalPathSmoothingKelpWeight = 1.1f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Weight applied to sargassum density while evaluating LOS blockage during path smoothing.")]
        private float abyssalPathSmoothingSargassumWeight = 0.9f;

        [SerializeField, Range(8, 256)]
        [Tooltip("Hard cap on LOS probe count per segment in the Burst string-pulling job.")]
        private int abyssalPathSmoothingMaxSamples = 96;

        [Header("HLOD Registry")]
        [SerializeField, Min(1f)]
        [Tooltip("Minimum distance in meters before large structures move into HLOD-only rendering.")]
        private float hlodMinimumDistance = 1000f;

        [SerializeField, Min(100f)]
        [Tooltip("Maximum distance in meters at which large-structure HLODs stay registered for rendering.")]
        private float hlodMaximumDistance = 5000f;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum largest-axis size in meters required for a persistent artificial structure to enter the HLOD registry.")]
        private float hlodMinimumStructureSize = 24f;

        [SerializeField, Min(0f)]
        [Tooltip("Padding added to HLOD AABB extents during frustum culling to avoid silhouette popping at the plane edge.")]
        private float hlodFrustumPadding = 12f;

        [Header("Global Canopy Mask")]
        [SerializeField, Min(100f)]
        [Tooltip("World-space radius in meters covered by the global canopy-height mask around the player.")]
        private float canopyGridRadius = DefaultThreatGridRadius;

        [SerializeField, Min(1f)]
        [Tooltip("Cell size in meters used by the global canopy-height mask.")]
        private float canopyGridCellSize = DefaultThreatGridCellSize;

        [SerializeField, Min(0f)]
        [Tooltip("Approximate roof thickness added on top of floating sargassum canopy instances.")]
        private float canopySargassumThickness = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Approximate roof thickness added on top of abyssal structural vegetation when stamping the canopy mask.")]
        private float canopyStructureThickness = 12f;

        [Header("Threat-Driven Currents")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Threat threshold above which underwater flow vectors begin twisting into a whirlpool around the strongest hotspot.")]
        private float threatWhirlpoolThreshold = 0.7f;

        [SerializeField, Min(1f)]
        [Tooltip("Maximum hotspot radius in meters affected by threat-driven abyssal whirlpools.")]
        private float threatWhirlpoolRadius = 180f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Blend weight applied when twisting underwater flow vectors around a high-threat hotspot.")]
        private float threatWhirlpoolStrength = 0.85f;

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
            public int Sand0;
            public int Sand1;
            public int Sand2;
            public int Sand3;
            public int Rock0;
            public int Rock1;
            public int Rock2;
            public int Rock3;
            public int Rock4;
            public int Rock5;
        }

        private readonly ref struct TerrainLayerMaskSampler
        {
            public readonly NativeArray<Color32> Pixels;
            public readonly int Channel;
            public readonly byte Valid;

            public TerrainLayerMaskSampler(NativeArray<Color32> pixels, int channel)
            {
                Pixels = pixels;
                Channel = channel;
                Valid = 1;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public readonly int TileX;
            public readonly int TileZ;
            public readonly int ChunkX;
            public readonly int ChunkZ;

            public ChunkKey(int tileX, int tileZ, int chunkX, int chunkZ)
            {
                TileX = tileX;
                TileZ = tileZ;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
            }

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

        /// <summary>
        /// Read-only active terrain height texture contract for GPU consumers that need the current player tile heightmap.
        /// </summary>
        public readonly struct TerrainHeightTexturePayload
        {
            public TerrainHeightTexturePayload(
                Texture heightTexture,
                Vector3 terrainPosition,
                Vector3 terrainSize,
                int heightmapResolution,
                int cacheRevision)
            {
                HeightTexture = heightTexture;
                TerrainPosition = terrainPosition;
                TerrainSize = terrainSize;
                HeightmapResolution = heightmapResolution;
                CacheRevision = cacheRevision;
            }

            /// <summary>Current active terrain height texture for the resolved tile.</summary>
            public Texture HeightTexture { get; }

            /// <summary>World-space minimum corner of the terrain tile.</summary>
            public Vector3 TerrainPosition { get; }

            /// <summary>World-space size of the terrain tile.</summary>
            public Vector3 TerrainSize { get; }

            /// <summary>Heightmap resolution used by the active texture payload.</summary>
            public int HeightmapResolution { get; }

            /// <summary>Authoritative cache revision for the resolved tile.</summary>
            public int CacheRevision { get; }
        }

        /// <summary>
        /// Read-only 16-bit terrain height payload for AI, physics, and terrain jobs.
        /// The vegetation bridge owns the backing NativeArray; consumers must treat it as an alias.
        /// </summary>
        public readonly ref struct TerrainHeightSamplePayload
        {
            public TerrainHeightSamplePayload(
                NativeArray<ushort> heightSamples,
                Vector3 terrainPosition,
                Vector3 terrainSize,
                int heightmapResolution,
                int cacheRevision)
            {
                HeightSamples = heightSamples;
                TerrainPosition = terrainPosition;
                TerrainSize = terrainSize;
                HeightmapResolution = heightmapResolution;
                CacheRevision = cacheRevision;
            }

            /// <summary>R16 height samples, row-major, resolution squared.</summary>
            public readonly NativeArray<ushort> HeightSamples;

            /// <summary>World-space minimum corner of the terrain tile.</summary>
            public readonly Vector3 TerrainPosition;

            /// <summary>World-space size of the terrain tile.</summary>
            public readonly Vector3 TerrainSize;

            /// <summary>Heightmap resolution used by the active native payload.</summary>
            public readonly int HeightmapResolution;

            /// <summary>Authoritative cache revision for the resolved tile.</summary>
            public readonly int CacheRevision;

            public static bool IsValid(in TerrainHeightSamplePayload payload)
            {
                return payload.HeightSamples.IsCreated &&
                       payload.HeightmapResolution > 1 &&
                       payload.HeightSamples.Length >= payload.HeightmapResolution * payload.HeightmapResolution;
            }
        }

        private struct TileNativeCacheBuffer
        {
            public VaultGenerationHandle<byte> SandMaskHandle;
            public VaultGenerationHandle<byte> RockMaskHandle;
            public VaultGenerationHandle<ushort> HeightSamplesHandle;
            public int SampleCount;
            public int HeightSampleCount;
        }

        private sealed class TileRuntimeState
        {
            public int TileX;
            public int TileZ;
            public UnityEngine.Terrain Terrain;
            public UnityEngine.TerrainData TerrainData;
            public Texture2D[] AlphamapTextureCache;
            public Texture HeightTextureCache;
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
            public int PendingCacheBufferIndex;
            public uint LastAccessFrame;
            public bool HeightReadbackPending;
            public bool HeightReadbackRepairRequested;
            public bool HeightReadbackDisposalDeferred;
            public bool TileCacheDisposalDeferred;
            public bool TileCacheEvictionDeferred;
            public bool PendingRemoval;
            public AsyncGPUReadbackRequest HeightReadbackRequest;
            public NativeArray<ushort> HeightReadbackData;
            public int HeightReadbackRepairSampleCount;
            public int HolesResolution;
            public bool TerrainHolesDirty;
            public bool[,] TerrainHoleMaskManaged;
            public VaultGenerationHandle<byte> TerrainHoleMaskHandle;
            public int TerrainHoleMaskCount;
            public int TileNativeCacheSlot = -1;
            public TileNativeCacheBuffer PrimaryCacheBuffer;
            public TileNativeCacheBuffer SecondaryCacheBuffer;
        }

        private struct DeferredTileCacheDisposal
        {
            public AsyncGPUReadbackRequest Request;
            public TileRuntimeState State;
        }

        private static readonly DeferredTileCacheDisposal[] s_DeferredTileCacheDisposals = new DeferredTileCacheDisposal[TileNativeCacheSlotCapacity];
        private static int s_DeferredTileCacheDisposalCount;

        /// <summary>
        /// Shared Voronoi/Worley parameters used by vegetation generation and external sargassum systems.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 40)]
        public readonly struct FloatingLabyrinthConfig
        {
            public readonly Vector2 FlowDirection;
            public readonly float PatchThreshold;
            public readonly float PatchNoiseScale;
            public readonly float CellSize;
            public readonly float SecondaryCellSize;
            public readonly float WallWidth;
            public readonly float WarpMeters;
            public readonly float FlowAnisotropy;
            private readonly uint _pad0;

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
                _pad0 = 0u;
            }
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

        internal static bool IsColonyCoralSemanticType(VegetationSemanticType semanticType)
        {
            switch (semanticType)
            {
                case VegetationSemanticType.ColonyCable:
                case VegetationSemanticType.ColonyHullPlating:
                case VegetationSemanticType.ColonySupportBeam:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsSolidStructureSemanticType(VegetationSemanticType semanticType)
        {
            return IsColonyCoralSemanticType(semanticType) ||
                   semanticType == VegetationSemanticType.DeadZoneMassiveStructure;
        }

        /// <summary>
        /// Zero-allocation density query result for gameplay physics, locomotion and audio systems.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        public readonly struct VegetationDensitySample
        {
            public readonly float Density;
            public readonly HectonVegetationInstanceType Type;
            public readonly VegetationSemanticType SemanticType;
            public readonly VegetationBiomeLayer BiomeLayer;
            public readonly VegetationAcousticType AcousticType;
            public readonly byte HasVegetation;
            private readonly uint _pad0;

            public VegetationDensitySample(
                bool hasVegetation,
                HectonVegetationInstanceType type,
                VegetationSemanticType semanticType,
                VegetationBiomeLayer biomeLayer,
                VegetationAcousticType acousticType,
                float density)
            {
                HasVegetation = hasVegetation ? (byte)1 : (byte)0;
                Type = type;
                SemanticType = semanticType;
                BiomeLayer = biomeLayer;
                AcousticType = acousticType;
                Density = density;
                _pad0 = 0u;
            }
        }

        private struct ChunkPayload
        {
            public int SurfaceOffset;
            public int SurfaceCount;
            public int SurfaceEdgeOffset;
            public byte SurfacePoolSet;
            public int UnderwaterOffset;
            public int UnderwaterCount;
            public int UnderwaterEdgeOffset;
            public byte UnderwaterPoolSet;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
            public Bounds WorldBounds;
            public byte GrassLodTier;
            public byte CorruptionState;

            public bool HasSurface => SurfaceCount > 0;
            public bool HasUnderwater => UnderwaterCount > 0;
            public bool IsCorrupted => CorruptionState != 0;
        }

        private sealed class FixedTileStateMap
        {
            private readonly long[] _keys;
            private readonly TileRuntimeState[] _values;
            private int _count;

            public FixedTileStateMap(int capacity)
            {
                int safeCapacity = math.max(1, capacity);
                // COLD ALLOC: long[safeCapacity] - fixed tile-state key table - owner: HectonMapMagicVegetationBridge
                _keys = new long[safeCapacity];
                // COLD ALLOC: TileRuntimeState[safeCapacity] - fixed tile-state shell table - owner: HectonMapMagicVegetationBridge
                _values = new TileRuntimeState[safeCapacity];
                for (int i = 0; i < safeCapacity; i++)
                {
                    // COLD ALLOC: TileRuntimeState - preallocated reusable tile shell - owner: HectonMapMagicVegetationBridge
                    _values[i] = new TileRuntimeState();
                }
            }

            public int Count => _count;

            public int Capacity => _values.Length;

            public bool TryGetValue(long key, out TileRuntimeState value)
            {
                int index = FindIndex(key);
                if (index >= 0)
                {
                    value = _values[index];
                    return true;
                }

                value = null;
                return false;
            }

            public bool TryAcquireOrCreate(long key, out TileRuntimeState value)
            {
                int index = FindIndex(key);
                if (index >= 0)
                {
                    value = _values[index];
                    return true;
                }

                if (_count >= _values.Length)
                {
                    value = null;
                    return false;
                }

                value = _values[_count];
                ResetTileRuntimeState(value);
                _keys[_count] = key;
                _count++;
                return true;
            }

            public bool Remove(long key)
            {
                int index = FindIndex(key);
                if (index < 0)
                    return false;

                RemoveAt(index);
                return true;
            }

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                {
                    _keys[i] = 0L;
                    ResetTileRuntimeState(_values[i]);
                }

                _count = 0;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            private int FindIndex(long key)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_keys[i] == key)
                        return i;
                }

                return -1;
            }

            private void RemoveAt(int index)
            {
                TileRuntimeState removed = _values[index];
                ResetTileRuntimeState(removed);
                int last = _count - 1;
                if (index != last)
                {
                    _keys[index] = _keys[last];
                    _values[index] = _values[last];
                    _values[last] = removed;
                }

                _keys[last] = 0L;
                _count = last;
            }

            public struct Enumerator
            {
                private readonly FixedTileStateMap _map;
                private int _index;
                private TileStateEntry _current;

                public Enumerator(FixedTileStateMap map)
                {
                    _map = map;
                    _index = -1;
                    _current = default;
                }

                public TileStateEntry Current => _current;

                public bool MoveNext()
                {
                    int next = _index + 1;
                    if (_map == null || next >= _map._count)
                        return false;

                    _index = next;
                    _current = new TileStateEntry(_map._keys[next], _map._values[next]);
                    return true;
                }

                public void Dispose()
                {
                }
            }

            public readonly struct TileStateEntry
            {
                public readonly long Key;
                public readonly TileRuntimeState Value;

                public TileStateEntry(long key, TileRuntimeState value)
                {
                    Key = key;
                    Value = value;
                }
            }
        }

        private sealed class FixedChunkPayloadMap
        {
            private readonly ChunkKey[] _keys;
            private readonly ChunkPayload[] _values;
            private int _count;

            public FixedChunkPayloadMap(int capacity)
            {
                int safeCapacity = math.max(1, capacity);
                // COLD ALLOC: ChunkKey[safeCapacity] - fixed chunk-payload key table - owner: HectonMapMagicVegetationBridge
                _keys = new ChunkKey[safeCapacity];
                // COLD ALLOC: ChunkPayload[safeCapacity] - fixed chunk-payload value table - owner: HectonMapMagicVegetationBridge
                _values = new ChunkPayload[safeCapacity];
            }

            public int Count => _count;

            public int Capacity => _values.Length;

            public bool ContainsKey(ChunkKey key)
            {
                return FindIndex(key) >= 0;
            }

            public bool TryGetValue(ChunkKey key, out ChunkPayload value)
            {
                int index = FindIndex(key);
                if (index >= 0)
                {
                    value = _values[index];
                    return true;
                }

                value = default;
                return false;
            }

            public bool Set(ChunkKey key, ChunkPayload value)
            {
                int index = FindIndex(key);
                if (index >= 0)
                {
                    _values[index] = value;
                    return true;
                }

                if (_count >= _values.Length)
                    return false;

                _keys[_count] = key;
                _values[_count] = value;
                _count++;
                return true;
            }

            public ChunkPayload this[ChunkKey key]
            {
                set => Set(key, value);
            }

            public bool Remove(ChunkKey key)
            {
                int index = FindIndex(key);
                if (index < 0)
                    return false;

                RemoveAt(index);
                return true;
            }

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                {
                    _keys[i] = default;
                    _values[i] = default;
                }

                _count = 0;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            private int FindIndex(ChunkKey key)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_keys[i].Equals(key))
                        return i;
                }

                return -1;
            }

            private void RemoveAt(int index)
            {
                int last = _count - 1;
                if (index != last)
                {
                    _keys[index] = _keys[last];
                    _values[index] = _values[last];
                }

                _keys[last] = default;
                _values[last] = default;
                _count = last;
            }

            public struct Enumerator
            {
                private readonly FixedChunkPayloadMap _map;
                private int _index;
                private ChunkPayloadEntry _current;

                public Enumerator(FixedChunkPayloadMap map)
                {
                    _map = map;
                    _index = -1;
                    _current = default;
                }

                public ChunkPayloadEntry Current => _current;

                public bool MoveNext()
                {
                    int next = _index + 1;
                    if (_map == null || next >= _map._count)
                        return false;

                    _index = next;
                    _current = new ChunkPayloadEntry(_map._keys[next], _map._values[next]);
                    return true;
                }

                public void Dispose()
                {
                }
            }

            public readonly struct ChunkPayloadEntry
            {
                public readonly ChunkKey Key;
                public readonly ChunkPayload Value;

                public ChunkPayloadEntry(ChunkKey key, ChunkPayload value)
                {
                    Key = key;
                    Value = value;
                }
            }
        }

        private static void ResetTileRuntimeState(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.TileX = 0;
            state.TileZ = 0;
            state.Terrain = null;
            state.TerrainData = null;
            state.AlphamapTextureCache = null;
            state.HeightTextureCache = null;
            state.TerrainPosition = default;
            state.TerrainSize = default;
            state.AlphamapResolution = 0;
            state.HeightmapResolution = 0;
            state.ChunkCountX = 0;
            state.ChunkCountZ = 0;
            state.LayerIndices = default;
            state.CacheRevision = 0;
            state.AlphamapTextureCount = 0;
            state.CombinedAlphamapHash = 0;
            state.CombinedAlphamapUpdateCount = 0u;
            state.HeightmapHash = 0;
            state.HeightmapUpdateCount = 0u;
            state.ActiveCacheBufferIndex = 0;
            state.PendingCacheBufferIndex = 0;
            state.LastAccessFrame = 0u;
            state.HeightReadbackPending = false;
            state.HeightReadbackRepairRequested = false;
            state.HeightReadbackDisposalDeferred = false;
            state.TileCacheDisposalDeferred = false;
            state.TileCacheEvictionDeferred = false;
            state.PendingRemoval = false;
            state.HeightReadbackRequest = default;
            state.HeightReadbackRepairSampleCount = 0;
            DisposeTileHeightReadbackData(state);
            state.HolesResolution = 0;
            state.TerrainHolesDirty = false;
            state.TerrainHoleMaskManaged = null;
            state.TerrainHoleMaskHandle = default;
            state.TerrainHoleMaskCount = 0;
            state.TileNativeCacheSlot = -1;
            state.PrimaryCacheBuffer = default;
            state.SecondaryCacheBuffer = default;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct VegetationDensityChunkRecord
        {
            [FieldOffset(0)]
            public float MinX;

            [FieldOffset(4)]
            public float MaxX;

            [FieldOffset(8)]
            public float MinZ;

            [FieldOffset(12)]
            public float MaxZ;

            [FieldOffset(16)]
            public int GridOffset;

            [FieldOffset(20)]
            public byte GrassLodTier;

            [FieldOffset(21)]
            private byte _pad0;

            [FieldOffset(22)]
            private ushort _pad1;
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct TerrainHoleRecord
        {
            [FieldOffset(0)]
            public float X;
            [FieldOffset(4)]
            public float Y;
            [FieldOffset(8)]
            public float Z;
            [FieldOffset(12)]
            public float Radius;
            [FieldOffset(16)]
            public float RadiusSq;
            [FieldOffset(20)]
            public int HoleId;
            [FieldOffset(24)]
            public TerrainHoleSourceType SourceType;
            [FieldOffset(25)]
            private byte _pad0;
            [FieldOffset(26)]
            private byte _pad1;
            [FieldOffset(27)]
            private byte _pad2;
            [FieldOffset(28)]
            private byte _pad3;
            [FieldOffset(29)]
            private byte _pad4;
            [FieldOffset(30)]
            private byte _pad5;
            [FieldOffset(31)]
            private byte _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ChunkSliceMoveRecord
        {
            [FieldOffset(0)]
            public int SourceOffset;
            [FieldOffset(4)]
            public int DestinationOffset;
            [FieldOffset(8)]
            public int Count;
            [FieldOffset(12)]
            private byte _pad0;
            [FieldOffset(13)]
            private byte _pad1;
            [FieldOffset(14)]
            private byte _pad2;
            [FieldOffset(15)]
            private byte _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ActiveAggregateCopyRecord
        {
            [FieldOffset(0)]
            public int SourceOffset;
            [FieldOffset(4)]
            public int DestinationOffset;
            [FieldOffset(8)]
            public int Count;
            [FieldOffset(12)]
            public byte PoolSet;
            [FieldOffset(13)]
            private byte _pad0;
            [FieldOffset(14)]
            private byte _pad1;
            [FieldOffset(15)]
            private byte _pad2;

            public ActiveAggregateCopyRecord(int sourceOffset, int destinationOffset, int count, byte poolSet)
            {
                SourceOffset = sourceOffset;
                DestinationOffset = destinationOffset;
                Count = count;
                PoolSet = poolSet;
                _pad0 = 0;
                _pad1 = 0;
                _pad2 = 0;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ArtificialStructureRecord
        {
            [FieldOffset(0)]
            public float MinX;
            [FieldOffset(4)]
            public float MinY;
            [FieldOffset(8)]
            public float MinZ;
            [FieldOffset(12)]
            public float MaxX;
            [FieldOffset(16)]
            public float MaxY;
            [FieldOffset(20)]
            public float MaxZ;
            [FieldOffset(24)]
            public byte Type;
            [FieldOffset(25)]
            private byte _pad0;
            [FieldOffset(26)]
            private ushort _pad1;
            [FieldOffset(28)]
            private uint _pad2;
        }

        private struct PersistentArtificialStructureRecord
        {
            public int StructureId;
            public Bounds Bounds;
            public StructureType Type;
        }

        [Serializable]
        private struct MegaWreckDefinition
        {
            [Tooltip("Stable runtime id used by section payloads and external streamers.")]
            public int WreckId;
            [Tooltip("Concrete mega-wreck prefab resolved by downstream streamers from the published section payload.")]
            public GameObject Prefab;
            [Tooltip("World-space center of the mega-wreck bounds.")]
            public Vector3 Center;
            [Tooltip("World-space size of the mega-wreck bounds.")]
            public Vector3 Size;
            [Tooltip("Stable seed used to derive deterministic section ids and seam-safe chunk partitions.")]
            public int Seed;

            public MegaWreckDefinition(int wreckId, GameObject prefab, Vector3 center, Vector3 size, int seed)
            {
                WreckId = wreckId;
                Prefab = prefab;
                Center = center;
                Size = size;
                Seed = seed;
            }
        }

        /// <summary>
        /// Immutable mega-wreck section request published for chunk-level hierarchical streaming.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct MegaWreckStreamSection
        {
            [FieldOffset(0)]
            public int WreckId;
            [FieldOffset(4)]
            public int SectionSeed;
            [FieldOffset(8)]
            public int SectionX;
            [FieldOffset(12)]
            public int SectionZ;
            [FieldOffset(16)]
            public Vector3 WorldCenter;
            [FieldOffset(28)]
            public Vector3 WorldSize;
            [FieldOffset(40)]
            public Vector3 LocalCenter;
            [FieldOffset(52)]
            public Vector3 LocalSize;
        }

        private struct ChunkBuildJobState
        {
            public ChunkKey Key;
            public long TileKey;
            public int TileCacheRevision;
            public byte GrassLodTier;
            public byte CorruptionState;
            public ChunkPayload PayloadHeader;
        }

        private struct ChunkBuildPendingJob
        {
            public bool Active;
            public ChunkBuildJobState JobState;
            public bool Cancelled;
            public NativeArray<byte> SandMaskSnapshot;
            public NativeArray<byte> RockMaskSnapshot;
            public NativeArray<ushort> HeightSamplesSnapshot;
            public NativeArray<JobInstanceRecord> GrassRecords;
            public NativeArray<JobInstanceRecord> FloatingRecords;
            public NativeArray<JobInstanceRecord> KelpRecords;
            public NativeArray<TerrainHoleRecord> TerrainHoles;
            public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            public NativeArray<byte> ThreatEchoFlags;
            public IDataVault ReadPinVault;
            public uint ReadPinMask;
            public BufferID TileSandMaskBufferId;
            public BufferID TileRockMaskBufferId;
            public BufferID TileHeightSamplesBufferId;
            public JobHandle Handle;
        }

        private struct DensityQuerySnapshotLease
        {
            public bool Active;
            public int ChunkCapacity;
            public int GridCapacity;
            public NativeArray<VegetationDensityChunkRecord> Chunks;
            public NativeArray<float3> DensityGrid;
            public NativeArray<float2> ThreatAttractorGrid;
            public JobHandle Handle;
        }

        private struct ThreatPropagationPendingJob
        {
            public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            public NativeArray<float2> ThreatAttractorGrid;
            public NativeArray<float3> DensityGrid;
            public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            public NativeArray<ThreatPropagationStagingPoint> Staging;
            public IDataVault StagingVault;
            public IDataVault ReadPinVault;
            public uint ReadPinMask;
            public Vector3 TargetCenter;
            public Vector3 VoxelOrigin;
            public bool Cancelled;
            public JobHandle Handle;
        }

        private struct FlowFieldPendingJob
        {
            public NativeArray<VegetationDensityChunkRecord> FlowChunks;
            public NativeArray<float3> FlowDensityGrid;
            public NativeArray<float2> ThreatAttractorGrid;
            public NativeArray<FlowFieldStagingPoint> Staging;
            public IDataVault StagingVault;
            public IDataVault ReadPinVault;
            public uint ReadPinMask;
            public Vector3 FlowCenter;
            public float RuntimeTime;
            public bool Cancelled;
            public JobHandle Handle;
        }

        private struct ThermalGridPendingJob
        {
            public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            public NativeArray<float2> ThreatAttractorGrid;
            public NativeArray<float3> DensityGrid;
            public NativeArray<ThermalGridStagingPoint> Staging;
            public IDataVault StagingVault;
            public IDataVault ReadPinVault;
            public uint ReadPinMask;
            public Vector3 ThermalCenter;
            public float RuntimeTime;
            public bool CanComparePreviousFlowVolume;
            public bool Cancelled;
            public JobHandle Handle;
        }

        private struct AbyssalPathPendingJob
        {
            public NativeArray<AbyssalPathStagingPoint> PathStaging;
            public int PathCapacity;
            public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            public NativeArray<float3> DensityGrid;
            public NativeArray<float2> ThreatAttractorGrid;
            public NativeArray<TerrainHoleRecord> TerrainHoles;
            public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            public NativeArray<byte> ThreatVoxelGrid;
            public IDataVault PathStagingVault;
            public IDataVault ReadPinVault;
            public uint ReadPinMask;
            public Vector3 TargetPosition;
            public int EndNode;
            public long ScheduleTicks;
            public bool CanReuseLastTarget;
            public bool ScheduledMacroVoxelRoute;
            public bool Cancelled;
            public JobHandle Handle;
        }

        private struct AbyssalPathCommitRecord
        {
            public float FunnelMs;
            public int RawCount;
            public int OutputCount;
            public Vector3 Start;
            public Vector3 End;
            public uint Flags;
            public bool Finite;
        }

        private struct ChunkAbyssalNavPayload
        {
            public Vector3[] Nodes;
            public Vector3[] ConduitVectors;
            public float[] ConduitStrengths;
            public byte[] NodeTypes;
            public int Count;
        }

        private struct ChunkMegaWreckPayload
        {
            public MegaWreckStreamSection[] Sections;
            public int Count;
        }

        private sealed class IndirectVegetationNativeBufferSource :
            IHectonIndirectVegetationBufferSource,
            IHectonIndirectVegetationNativeBufferSource
        {
            private readonly HectonMapMagicVegetationBridge _owner;
            private readonly bool _underwater;

            public IndirectVegetationNativeBufferSource(HectonMapMagicVegetationBridge owner, bool underwater)
            {
                _owner = owner;
                _underwater = underwater;
            }

            public GraphicsBuffer InstanceMatrixBuffer => null;

            public GraphicsBuffer InstanceDataBuffer => null;

            public int InstanceCount => _underwater ? _owner._underwaterFrontCount : _owner._surfaceFrontCount;

            public bool HasExplicitBounds => _underwater ? _owner._hasUnderwaterFrontBounds : _owner._hasSurfaceFrontBounds;

            public Bounds DrawBounds => _underwater ? _owner._underwaterFrontDrawBounds : _owner._surfaceFrontDrawBounds;

            public bool TryAcquireNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer)
            {
                return _underwater
                    ? _owner.TryAcquireUnderwaterNativeReadBuffer(out readBuffer)
                    : _owner.TryAcquireSurfaceNativeReadBuffer(out readBuffer);
            }

            public void ReleaseNativeReadBuffer(in HectonIndirectVegetationNativeReadBuffer readBuffer, JobHandle readerHandle)
            {
                if (_underwater)
                    _owner.ReleaseUnderwaterNativeReadBuffer(readBuffer, readerHandle);
                else
                    _owner.ReleaseSurfaceNativeReadBuffer(readBuffer, readerHandle);
            }
        }

        // COLD ALLOC: FixedTileStateMap[64] - fixed MapMagic tile state cache with preallocated TileRuntimeState shells - owner: HectonMapMagicVegetationBridge
        private readonly FixedTileStateMap _tileStates = new FixedTileStateMap(TileCacheLruCapacity);
        // COLD ALLOC: FixedChunkPayloadMap[256] - fixed streamed virtual chunk cache without managed hash-table growth - owner: HectonMapMagicVegetationBridge
        private readonly FixedChunkPayloadMap _chunkPayloads = new FixedChunkPayloadMap(InitialChunkCapacity);
        // COLD ALLOC: ChunkKey/ChunkAbyssalNavPayload[256] - fixed per-chunk abyssal navigation node cache - owner: HectonMapMagicVegetationBridge
        private readonly ChunkKey[] _chunkAbyssalNavPayloadKeys = new ChunkKey[InitialChunkCapacity];
        private readonly ChunkAbyssalNavPayload[] _chunkAbyssalNavPayloads = new ChunkAbyssalNavPayload[InitialChunkCapacity];
        private int _chunkAbyssalNavPayloadCount;
        // COLD ALLOC: ChunkKey/ChunkMegaWreckPayload[256] - fixed per-chunk mega-wreck streaming section cache - owner: HectonMapMagicVegetationBridge
        private readonly ChunkKey[] _chunkMegaWreckPayloadKeys = new ChunkKey[InitialChunkCapacity];
        private readonly ChunkMegaWreckPayload[] _chunkMegaWreckPayloads = new ChunkMegaWreckPayload[InitialChunkCapacity];
        private int _chunkMegaWreckPayloadCount;
        // COLD ALLOC: ChunkBuildPendingJob[MaxConcurrentChunkBuildJobs] - fixed in-flight vegetation chunk jobs finalized by LateFrameTick - owner: HectonMapMagicVegetationBridge
        private readonly ChunkBuildPendingJob[] _chunkBuildJobs = new ChunkBuildPendingJob[MaxConcurrentChunkBuildJobs];
        // COLD ALLOC: DensityQuerySnapshotLease[MaxPublicDensityQuerySnapshotLeases] - fixed external density job leases reclaimed by JobHandle completion - owner: HectonMapMagicVegetationBridge
        private readonly DensityQuerySnapshotLease[] _densityQuerySnapshotLeases = new DensityQuerySnapshotLease[MaxPublicDensityQuerySnapshotLeases];
        // COLD ALLOC: NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs] - prewarmed surface grass output banks borrowed by chunk-build jobs - owner: HectonMapMagicVegetationBridge
        private NativeArray<JobInstanceRecord>[] _chunkBuildGrassRecordBanks = Array.Empty<NativeArray<JobInstanceRecord>>();
        // COLD ALLOC: NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs] - prewarmed floating vegetation output banks borrowed by chunk-build jobs - owner: HectonMapMagicVegetationBridge
        private NativeArray<JobInstanceRecord>[] _chunkBuildFloatingRecordBanks = Array.Empty<NativeArray<JobInstanceRecord>>();
        // COLD ALLOC: NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs] - prewarmed kelp output banks borrowed by chunk-build jobs - owner: HectonMapMagicVegetationBridge
        private NativeArray<JobInstanceRecord>[] _chunkBuildKelpRecordBanks = Array.Empty<NativeArray<JobInstanceRecord>>();
        private int _chunkBuildGrassRecordCapacity;
        private int _chunkBuildFloatingRecordCapacity;
        private int _chunkBuildKelpRecordCapacity;
        // COLD ALLOC: ChunkKey[512] - bounded runtime corrupted-chunk registry - owner: HectonMapMagicVegetationBridge
        private readonly ChunkKey[] _corruptedChunkOrder = new ChunkKey[InitialCorruptedChunkCapacity];
        private int _corruptedChunkCount;
        // COLD ALLOC: PersistentArtificialStructureRecord[256] - player/runtime-authored artificial structure registry - owner: HectonMapMagicVegetationBridge
        private readonly PersistentArtificialStructureRecord[] _persistentArtificialStructures = new PersistentArtificialStructureRecord[MaxPersistentArtificialStructureRecords];
        private int _persistentArtificialStructureCount;
        // COLD ALLOC: ChunkKey[2048] - fixed eviction staging for non-resident chunk payloads - owner: HectonMapMagicVegetationBridge
        private readonly ChunkKey[] _evictionKeys = new ChunkKey[MaxChunkPoolEvictionIterations];
        private int _evictionKeyCount;
        // COLD ALLOC: long[64] - deferred tile-removal staging while GPU height readbacks finish without blocking the main thread - owner: HectonMapMagicVegetationBridge
        private readonly long[] _tileStateRemovalScratchKeys = new long[TileCacheLruCapacity];
        private int _tileStateRemovalScratchKeyCount;
        // COLD ALLOC: MapMagicTerrainTileSnapshot[256] - fixed deferred plugin tile snapshot bootstrap cache - owner: HectonMapMagicVegetationBridge
        private readonly MapMagicTerrainTileSnapshot[] _startupBootstrapTiles = new MapMagicTerrainTileSnapshot[StartupBootstrapTileSnapshotCapacity];
        private readonly long[] _tileNativeCacheSlotKeys = new long[TileNativeCacheSlotCapacity];
        private readonly bool[] _tileNativeCacheSlotUsed = new bool[TileNativeCacheSlotCapacity];

        private ChunkKey[] _desiredChunkKeys;
        private float[] _desiredChunkDistances;
        private ChunkKey[] _selectedChunkKeys;
        private bool[] _selectedChunkVisibility;
        private ChunkKey[] _pendingChunkKeys;
        private float[] _pendingChunkPriorities;
        private ChunkKey[] _surfaceDefragKeys;
        private int[] _surfaceDefragOffsets;
        private ChunkKey[] _underwaterDefragKeys;
        private int[] _underwaterDefragOffsets;
        private TerrainHoleRecord[] _terrainHoleRecords = Array.Empty<TerrainHoleRecord>();
        private TerrainHoleStreamingRecord[] _terrainHoleStreamingRecords = Array.Empty<TerrainHoleStreamingRecord>();
        private Vector3[] _abyssalNavConduitVectorsSnapshot = Array.Empty<Vector3>();
        private float[] _abyssalNavConduitStrengthSnapshot = Array.Empty<float>();
        private byte[] _abyssalNavNodeTypesSnapshot = Array.Empty<byte>();
        private ActiveAggregateNativeBufferSet _surfaceAggregateFrontBuffers;
        private ActiveAggregateNativeBufferSet _surfaceAggregateBackBuffers;
        private ActiveAggregateNativeBufferSet _underwaterAggregateFrontBuffers;
        private ActiveAggregateNativeBufferSet _underwaterAggregateBackBuffers;
        private int _surfaceFrontBufferIndex;
        private int _surfaceBackBufferIndex = 1;
        private int _underwaterFrontBufferIndex;
        private int _underwaterBackBufferIndex = 1;
        private int _surfaceFrontCount;
        private int _surfaceBackCount;
        private int _underwaterFrontCount;
        private int _underwaterBackCount;
        private int _surfaceActiveAggregateRevision;
        private int _underwaterActiveAggregateRevision;
        private Bounds _surfaceFrontDrawBounds;
        private Bounds _surfaceBackDrawBounds;
        private Bounds _underwaterFrontDrawBounds;
        private Bounds _underwaterBackDrawBounds;
        private bool _hasSurfaceFrontBounds;
        private bool _hasSurfaceBackBounds;
        private bool _hasUnderwaterFrontBounds;
        private bool _hasUnderwaterBackBounds;
        private JobHandle _surfaceFrontReaderHandle;
        private JobHandle _surfaceBackReaderHandle;
        private JobHandle _underwaterFrontReaderHandle;
        private JobHandle _underwaterBackReaderHandle;
        private IndirectVegetationNativeBufferSource _surfaceNativeBufferSource;
        private IndirectVegetationNativeBufferSource _underwaterNativeBufferSource;
        private GraphicsBuffer _surfaceInstanceBuffer;
        private GraphicsBuffer _surfaceInstanceDataBuffer;
        private GraphicsBuffer _underwaterInstanceBuffer;
        private GraphicsBuffer _underwaterInstanceDataBuffer;
        private GraphicsBuffer _predatorFearNodeBufferA;
        private GraphicsBuffer _predatorFearNodeBufferB;
        private GraphicsBuffer _activePredatorFearNodeBuffer;
        private int _predatorFearNodeBufferWriteIndex;
        private int _pendingPredatorFearShaderActiveCount;
        private bool _pendingPredatorFearShaderUpload;
        private NativeChunkPool _surfaceChunkPool;
        private NativeChunkPool _underwaterChunkPool;
        private PoolBlock[] _surfacePoolFreeBlocks;
        private PoolBlock[] _underwaterPoolFreeBlocks;
        private PoolBlock[] _surfaceDefragScratchFreeBlocks;
        private PoolBlock[] _underwaterDefragScratchFreeBlocks;
        private int _surfacePoolFreeBlockCount;
        private int _underwaterPoolFreeBlockCount;
        private int _surfaceDefragScratchFreeBlockCount;
        private int _underwaterDefragScratchFreeBlockCount;
        private int _desiredChunkCount;
        private int _selectedChunkCount;
        private int _pendingChunkCount;
        private int _surfaceActiveCount;
        private int _underwaterActiveCount;
        private int _densityQueryChunkCount;
        private NativeArray<VegetationDensityChunkRecord> _densityQueryScratchChunks;
        private NativeArray<float3> _densityQueryScratchDensityGrid;
        private NativeArray<float2> _densityQueryScratchThreatAttractorGrid;
        private long _chunkPayloadUsedBytes;
        private Bounds _surfaceDrawBounds;
        private Bounds _underwaterDrawBounds;
        private float _vegetationAudioDensity;
        private VegetationAcousticType _vegetationAudioAcousticType;
        private float _lastPublishedVegetationAudioDensity = float.NegativeInfinity;
        private VegetationAcousticType _lastPublishedVegetationAudioAcousticType = (VegetationAcousticType)byte.MaxValue;
        private bool _vegetationAudioHandoffPublishRequested;
        private bool _vegetationAudioHandoffForcePublish;
        private float _pendingVegetationAudioDensity;
        private VegetationAcousticType _pendingVegetationAudioAcousticType;
        private float _nextNativePoolFragmentationLogTime = float.NegativeInfinity;
        private byte _sandMaskThresholdByte;
        private byte _rockMaskThresholdByte;
        private Vector2 _floatingFlowDirectionNormalized;
        private IWeatherService _weatherService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private WorldMacroGeologyParams _vegetationMacroGeologyParamsCache;
        private WorldTerrainMesoDetailParams _vegetationMesoDetailParamsCache;
        private int _vegetationMacroGeologyRuntimeSeedCache = int.MinValue;
        private float _vegetationMacroGeologyWaterLevelCache = float.NaN;
        private bool _vegetationMacroGeologyParamsCached;
        private Vector3 _playerVelocity;
        private Vector3 _lastPlayerPosition;
        private bool _hasLastPlayerPosition;
        private Camera _cachedViewCamera;
        private Camera _cachedLocalCamera;
        private float _nextCameraResolveTime = float.NegativeInfinity;
        private float _nextCacheValidationTime = float.NegativeInfinity;
        private int _cacheValidationChunkCursor;
        private bool _isRegistered;
        private bool _registeredHotSwapListener;
        private bool _eventsSubscribed;
        private bool _originShiftListenerRegistered;
        private bool _activeSetDirty = true;
        private bool _insideLateFrameJobSwap;
        private bool _deferredTileCacheDisposalRequested;
        private bool _deferredStartupProgressRequested;
        private bool _residentTileCacheValidationRequested;
        private bool _activeBufferRebuildRequested;

        public static float GlobalVegetationAudioDensity { get; private set; }
        public static VegetationAcousticType GlobalVegetationAcousticType { get; private set; }
        public static bool GlobalArtificialInteriorActive { get; private set; }
        public static StructureType GlobalArtificialInteriorType { get; private set; }
        public static int GlobalArtificialInteriorId { get; private set; } = int.MinValue;
        public static Bounds GlobalArtificialInteriorBounds { get; private set; }
        public static Vector3 GlobalTotalUniverseOffset { get; private set; }
        public static double3 GlobalTotalUniverseOffsetDouble { get; private set; }
        private static HectonMapMagicVegetationBridge s_activeRuntimeInstance;
        internal static HectonMapMagicVegetationBridge ActiveRuntimeInstance => s_activeRuntimeInstance;
        public FloraDataTemplate[] FloraTemplates => floraTemplates;

        internal bool TryFillTerrainHeightGridFromNativeCache(
            float originX,
            float originZ,
            int sampleCountX,
            int sampleCountZ,
            float step,
            NativeArray<float> outHeights,
            float fallbackHeight)
        {
            if (!outHeights.IsCreated || sampleCountX <= 0 || sampleCountZ <= 0)
                return false;

            int requiredLength = sampleCountX * sampleCountZ;
            if (outHeights.Length < requiredLength)
                return false;

            bool sampledAny = false;
            TileRuntimeState activeState = null;
            NativeArray<ushort> activeHeightSamples = default;
            float activeMinX = 0f;
            float activeMaxX = 0f;
            float activeMinZ = 0f;
            float activeMaxZ = 0f;

            for (int z = 0; z < sampleCountZ; z++)
            {
                float worldZ = originZ + (z * step);
                for (int x = 0; x < sampleCountX; x++)
                {
                    float worldX = originX + (x * step);
                    int sampleIndex = x + (z * sampleCountX);

                    if (activeState != null &&
                        activeHeightSamples.IsCreated &&
                        worldX >= activeMinX && worldX <= activeMaxX &&
                        worldZ >= activeMinZ && worldZ <= activeMaxZ &&
                        TrySampleCachedTerrainHeight(activeState, activeHeightSamples, worldX, worldZ, out float cachedHeight))
                    {
                        outHeights[sampleIndex] = cachedHeight;
                        sampledAny = true;
                        continue;
                    }

                    activeState = null;
                    activeHeightSamples = default;

                    if (TryFindTileStateAtPosition(new Vector3(worldX, 0f, worldZ), out TileRuntimeState resolvedState) &&
                        TryGetActiveTileCache(resolvedState, out _, out _, out NativeArray<ushort> resolvedHeightSamples) &&
                        TrySampleCachedTerrainHeight(resolvedState, resolvedHeightSamples, worldX, worldZ, out float resolvedHeight))
                    {
                        activeState = resolvedState;
                        activeHeightSamples = resolvedHeightSamples;
                        activeMinX = resolvedState.TerrainPosition.x;
                        activeMaxX = resolvedState.TerrainPosition.x + resolvedState.TerrainSize.x;
                        activeMinZ = resolvedState.TerrainPosition.z;
                        activeMaxZ = resolvedState.TerrainPosition.z + resolvedState.TerrainSize.z;
                        outHeights[sampleIndex] = resolvedHeight;
                        sampledAny = true;
                        continue;
                    }

                    outHeights[sampleIndex] = fallbackHeight;
                }
            }

            return sampledAny;
        }

        internal bool TryFillTerrainHeightGridFromNativeCacheAUP(
            Vector3 absoluteOrigin,
            int sampleCountX,
            int sampleCountZ,
            float step,
            NativeArray<float> outHeights,
            float fallbackHeight)
        {
            Vector3 runtimeOrigin = HectonFloatingOrigin.ToRuntimePosition(absoluteOrigin);
            return TryFillTerrainHeightGridFromNativeCache(
                runtimeOrigin.x,
                runtimeOrigin.z,
                sampleCountX,
                sampleCountZ,
                step,
                outHeights,
                fallbackHeight);
        }

        // COLD ALLOC: Plane[6] - cached frustum plane array reused for no-alloc chunk visibility tests - owner: HectonMapMagicVegetationBridge
        private readonly Plane[] _viewFrustumPlanes = new Plane[6];
        private ChunkKey[] _densityQueryChunkKeys;
        private VegetationNativeMemory _nativeMemory;
        private Vector3[] _abyssalAnchorPositions = Array.Empty<Vector3>();
        private Vector3[] _abyssalNavNodeSnapshot = Array.Empty<Vector3>();
        private Vector3[] _abyssalPathSnapshot = Array.Empty<Vector3>();
        private HLODData[] _hlodRegistrySnapshot = Array.Empty<HLODData>();
        private HLODData[] _visibleHlodSnapshot = Array.Empty<HLODData>();
        private NativeChunkPool _surfaceDefragScratchPool;
        private NativeChunkPool _underwaterDefragScratchPool;
        private MegaWreckStreamSection[] _megaWreckStreamSnapshot = Array.Empty<MegaWreckStreamSection>();
        private int _terrainHoleCount;
        private int _persistentTerrainHoleCount;
        private int _abyssalAnchorCount;
        private int _abyssalNavNodeCount;
        private int _abyssalPathCount;
        private int _megaWreckStreamCount;
        private int _megaWreckInteriorMaskHash;
        private int _artificialStructureCount;
        private int _hlodRegistryCount;
        private int _visibleHlodCount;
        private int _predatorFearNodeCount;
        private int _ecosystemThreatGridResolution;
        private int _ecosystemThreatGridCellCount;
        private int _ecosystemThreatGridResolutionY;
        private int _ecosystemThreatVoxelCellCount;
        private int _canopyGridResolution;
        private int _canopyGridCellCount;
        private int _abyssalThermalGridResolutionXZ;
        private int _abyssalThermalGridResolutionY;
        private int _abyssalThermalGridCellCount;
        private int _abyssalThermalGridRingOffsetX;
        private int _abyssalThermalGridRingOffsetY;
        private int _abyssalThermalGridRingOffsetZ;
        private int _threatSamplingChunkCount;
        private bool _threatGridInitialized;
        private bool _threatPropagationScheduled;
        private bool _flowFieldInitialized;
        private bool _flowFieldScheduled;
        private int _swarmWakeImpulseCount;
        private SwarmWakeImpulse _swarmWakeImpulse;
        // COLD ALLOC: float2[_ecosystemThreatGridCellCount] - lock-flattened flow-field commit snapshot - owner: HectonMapMagicVegetationBridge
        private float2[] _flowFieldCommitFlow = Array.Empty<float2>();
        // COLD ALLOC: float[_ecosystemThreatGridCellCount] - lock-flattened threat propagation commit snapshot - owner: HectonMapMagicVegetationBridge
        private float[] _threatPropagationCommitThreat = Array.Empty<float>();
        // COLD ALLOC: byte[_ecosystemThreatGridCellCount] - lock-flattened compressed threat commit snapshot - owner: HectonMapMagicVegetationBridge
        private byte[] _threatPropagationCommitCompressed = Array.Empty<byte>();
        // COLD ALLOC: byte[_ecosystemThreatGridCellCount] - lock-flattened threat echo commit snapshot - owner: HectonMapMagicVegetationBridge
        private byte[] _threatPropagationCommitEcho = Array.Empty<byte>();
        // COLD ALLOC: byte[_ecosystemThreatVoxelCellCount] - lock-flattened threat voxel commit snapshot - owner: HectonMapMagicVegetationBridge
        private byte[] _threatPropagationCommitVoxel = Array.Empty<byte>();
        // COLD ALLOC: float[_abyssalThermalGridCellCount] - lock-flattened thermal grid commit snapshot - owner: HectonMapMagicVegetationBridge
        private float[] _thermalGridCommitThermal = Array.Empty<float>();
        // COLD ALLOC: float3[_abyssalThermalGridCellCount] - lock-flattened flow volume commit snapshot - owner: HectonMapMagicVegetationBridge
        private float3[] _thermalGridCommitFlowVolume = Array.Empty<float3>();
        private bool _canopyGridInitialized;
        private bool _abyssalThermalGridInitialized;
        private bool _abyssalFlowVolumeInitialized;
        private bool _abyssalThermalGridScheduled;
        private bool _abyssalPathScheduled;
        private bool _hlodCullScheduled;
        private bool _poolDefragScheduled;
        private ThreatPropagationPendingJob _threatPropagationJob;
        private FlowFieldPendingJob _flowFieldJob;
        private ThermalGridPendingJob _thermalGridJob;
        private AbyssalPathPendingJob _abyssalPathJob;
        private JobHandle _abyssalPathHandle;
        private JobHandle _hlodCullHandle;
        private JobHandle _surfacePoolDefragHandle;
        private JobHandle _underwaterPoolDefragHandle;
        private JobHandle _aggregateRebuildHandle;
        private Vector3 _ecosystemThreatGridCenter;
        private Vector3 _scheduledThreatGridCenter;
        // COLD ALLOC: PredatorFearNodeState[DefaultPredatorFearNodeCapacity] - bounded predator fear-sector memory aligned to ecosystem threat routing - owner: HectonMapMagicVegetationBridge
        private PredatorFearNodeState[] _predatorFearNodes = Array.Empty<PredatorFearNodeState>();
        private float _predatorFearSimulationTime;
        private float _predatorFearPruneTimer;
        private Vector3 _ecosystemThreatVoxelOrigin;
        private Vector3 _scheduledThreatVoxelOrigin;
        private Vector3 _ecosystemFlowFieldCenter;
        private Vector3 _scheduledFlowFieldCenter;
        private float _vegetationRuntimeSeconds;
        private float _swarmWakeImpulseExpireTime = float.NegativeInfinity;
        private Vector3 _canopyGridCenter;
        private Vector3 _abyssalThermalGridCenter;
        private Vector3 _scheduledAbyssalThermalGridCenter;
        private Vector3 _abyssalNavGraphOrigin;
        private float _lastThreatPropagationTime = float.NegativeInfinity;
        private float _lastFlowFieldSolveTime = float.NegativeInfinity;
        private float _lastThermalGridSolveTime = float.NegativeInfinity;
        private float _currentThreatHotspotLevel;
        private Vector3 _currentThreatHotspotPosition;
        private byte _threatSpatialSolveCursor;

        private Vector3 _externalThreatPulsePosition;
        private Vector3 _totalUniverseOffset;
        private double3 _totalUniverseOffsetDouble;
        private Vector3 _pendingWorldOffset;
        private double3 _pendingWorldOffsetDouble;
        private float _externalThreatPulseRadius;
        private float _externalThreatPulseStrength;
        private float _externalThreatPulseHoldTimer;
        private float _idleNativePoolTimer;
        private bool _hasPendingWorldOffset;
        private int _nextTerrainHoleId = 1;
        private int _nextArtificialStructureId = 1;
        private int _surfaceDefragMoveCount;
        private int _underwaterDefragMoveCount;
        private int _surfaceDefragCompactUsedCount;
        private int _underwaterDefragCompactUsedCount;
        private int _surfaceAggregateCopyRecordCount;
        private int _underwaterAggregateCopyRecordCount;
        private int _lastAbyssalPathEndNode = -1;
        private Vector3 _lastAbyssalPathTargetPosition;
        private int _abyssalPathTelemetryCursor;
        private int _abyssalPathTelemetryWrittenCount;
        private uint _abyssalPathTelemetrySequence;
        private VaultGenerationHandle<AbyssalPathTelemetryEntry> _abyssalPathTelemetryHandle;
        private NativeArray<byte> _abyssalPathTelemetryDumpPayload;
        private IDataVault _abyssalPathTelemetryVault;
        private int _lastAbyssalPathPortalLookAhead;
        private int _lastAbyssalPathMaxSamples;
        private bool _abyssalPathTelemetryDumpedForFault;
        private bool _hasLastAbyssalPathTarget;
        private bool _aggregateRebuildScheduled;
        private bool _surfaceAggregateSwapPending;
        private bool _underwaterAggregateSwapPending;
        private int _startupBootstrapTileCount;
        private int _startupBootstrapTileCursor;
        private bool _startupTerrainHoleSyncPending = true;
        private bool _startupTileBootstrapPending = true;
        private bool _startupResidencyPending = true;
        private bool _runtimeLifecycleActive;
        private bool _runtimeTeardownComplete;
        private ArtificialInteriorState _activeArtificialInteriorState;
        private FloraDataTemplate.RuntimeDescriptor[] _floraTemplateRuntimeDescriptors = Array.Empty<FloraDataTemplate.RuntimeDescriptor>();
        private static readonly int _PredatorFearNodeBufferId = Shader.PropertyToID("_HectonPredatorFearNodes");
        private static readonly int _PredatorFearNodeCountId = Shader.PropertyToID("_HectonPredatorFearNodeCount");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_activeRuntimeInstance = null;
            WorldRuntimeReferenceUtility.InvalidateHectonMapMagicVegetationBridgeCache(null);
            PredatorCognitionDomain.ClearVegetationThreatVoxelSource(null);
            DroneFleetManager.ClearVegetationBridge(null);
        }

        private void PublishActiveRuntimeInstance()
        {
            GlobalRegistry.RegisterMapMagicVegetationRuntime(this);
            s_activeRuntimeInstance = this;
            PredatorCognitionDomain.BindVegetationThreatVoxelSource(this);
            DroneFleetManager.BindVegetationBridge(this);
        }

        private void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            PredatorCognitionDomain.ClearVegetationThreatVoxelSource(this);
            DroneFleetManager.ClearVegetationBridge(this);
            WorldRuntimeReferenceUtility.InvalidateHectonMapMagicVegetationBridgeCache(this);

            if (ReferenceEquals(GlobalRegistry.MapMagicVegetation, this))
                GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);
        }

        private void Awake()
        {
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            _vegetationRuntimeSeconds = 0f;
            RebuildFloraTemplateRuntimeDescriptors();
            if (_surfaceNativeBufferSource == null)
                _surfaceNativeBufferSource = new IndirectVegetationNativeBufferSource(this, false); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - surface native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

            if (_underwaterNativeBufferSource == null)
                _underwaterNativeBufferSource = new IndirectVegetationNativeBufferSource(this, true); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - underwater native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

            CacheOceanKinematicsService(Application.isPlaying ? GlobalRegistry.OceanKinematics : null);
            SyncWaterSurfaceLevelFromRuntime();
            residentRadius = math.clamp(residentRadius, 150f, 200f);
            residentHysteresisScale = math.clamp(residentHysteresisScale, 1f, 1.5f);
            maxChunkBuildsPerSlowTick = math.max(1, maxChunkBuildsPerSlowTick);
            predictiveLeadSeconds = math.max(0f, predictiveLeadSeconds);
            predictiveLeadMaxMeters = math.max(0f, predictiveLeadMaxMeters);
            rearResidencyScale = math.clamp(rearResidencyScale, 0.2f, 1f);
            lateralResidencyScale = math.clamp(lateralResidencyScale, 0.5f, 1.25f);
            predictiveMinSpeed = math.max(0f, predictiveMinSpeed);
            forwardPriorityBoost = math.max(0f, forwardPriorityBoost);
            rearPriorityPenalty = math.max(0f, rearPriorityPenalty);
            nativePoolBudgetMb = math.max(MinimumNativePoolBudgetMb, nativePoolBudgetMb);
            nativePoolGuardMb = math.max(MinimumNativePoolBudgetMb, nativePoolGuardMb);
            vegetationAudioProbeRadius = math.max(0.5f, vegetationAudioProbeRadius);
            surfacePoolShare = math.clamp(surfacePoolShare, 0.5f, 0.9f);
            minimumNormalY = math.max(math.saturate(minimumNormalY), OrganicKelpMaxSlopeNormalY);
            kelpMinHeight = math.clamp(kelpMinHeight, waterLevel - OrganicKelpMaxDepthBelowSurfaceMeters, waterLevel);
            grassStepMeters = math.clamp(grassStepMeters, 1f, 2f);
            grassFarStepMeters = math.clamp(grassFarStepMeters, 4f, 5f);
            grassHighDensityRadius = math.max(25f, grassHighDensityRadius);
            kelpStepMeters = math.clamp(kelpStepMeters, 5f, 10f);
            floatingStepMeters = math.max(5f, floatingStepMeters);
            grassScaleRange = NormalizeScaleRange(grassScaleRange);
            kelpScaleRange = NormalizeScaleRange(kelpScaleRange);
            floatingScaleRange = NormalizeScaleRange(floatingScaleRange);
            grassVisibilityWeight = math.clamp(grassVisibilityWeight, 0f, 1.5f);
            kelpVisibilityWeight = math.clamp(kelpVisibilityWeight, 0f, 1.5f);
            sargassumVisibilityWeight = math.clamp(sargassumVisibilityWeight, 0f, 1.5f);
            sargassumVisibilityBand = math.max(0.25f, sargassumVisibilityBand);
            proceduralScaleJitter = math.clamp(proceduralScaleJitter, 0f, 0.5f);
            floatingCellSize = math.max(6f, floatingCellSize);
            floatingSecondaryCellSize = math.max(4f, floatingSecondaryCellSize);
            floatingWallWidth = math.clamp(floatingWallWidth, 1f, 6f);
            floatingWarpMeters = math.max(0f, floatingWarpMeters);
            floatingFlowAnisotropy = math.clamp(floatingFlowAnisotropy, 0.2f, 1f);
            floatingFlowDirection = NormalizeFlowDirection(floatingFlowDirection);
            edgeDitherDistance = math.max(0f, edgeDitherDistance);
            initialTerrainHoleCapacity = math.max(MinimumTerrainHoleRuntimeCapacity, initialTerrainHoleCapacity);
            nativePoolDefragIdleSeconds = math.max(1f, nativePoolDefragIdleSeconds);
            nativePoolDefragThresholdPercent = math.clamp(nativePoolDefragThresholdPercent, 1f, 100f);
            nativePoolDefragIdleSpeedThreshold = math.max(0.01f, nativePoolDefragIdleSpeedThreshold);
            abyssalPathSmoothingSampleSpacing = math.max(0.5f, abyssalPathSmoothingSampleSpacing);
            abyssalPathSmoothingObstacleThreshold = math.saturate(abyssalPathSmoothingObstacleThreshold);
            abyssalPathSmoothingKelpWeight = math.max(0f, abyssalPathSmoothingKelpWeight);
            abyssalPathSmoothingSargassumWeight = math.max(0f, abyssalPathSmoothingSargassumWeight);
            abyssalPathSmoothingMaxSamples = math.clamp(abyssalPathSmoothingMaxSamples, 8, 256);
            hlodMinimumDistance = math.max(1f, hlodMinimumDistance);
            hlodMaximumDistance = math.max(hlodMinimumDistance, hlodMaximumDistance);
            hlodMinimumStructureSize = math.max(1f, hlodMinimumStructureSize);
            hlodFrustumPadding = math.max(0f, hlodFrustumPadding);
            canopyGridRadius = math.max(100f, canopyGridRadius);
            canopyGridCellSize = math.max(1f, canopyGridCellSize);
            canopySargassumThickness = math.max(0f, canopySargassumThickness);
            canopyStructureThickness = math.max(0f, canopyStructureThickness);
            threatWhirlpoolThreshold = math.saturate(threatWhirlpoolThreshold);
            threatWhirlpoolRadius = math.max(1f, threatWhirlpoolRadius);
            threatWhirlpoolStrength = math.max(0f, threatWhirlpoolStrength);
            threatGridRadius = DefaultThreatGridRadius;
            threatGridCellSize = DefaultThreatGridCellSize;
            threatDiffusion = math.saturate(threatDiffusion);
            threatDecayPerSecond = math.max(0.01f, threatDecayPerSecond);
            threatNoiseDepositPerSecond = math.max(0f, threatNoiseDepositPerSecond);
            threatFlashlightDepositPerSecond = math.max(0f, threatFlashlightDepositPerSecond);
            threatPulseDepositPerSecond = math.max(0f, threatPulseDepositPerSecond);
            threatEmissionRadiusMin = math.max(1f, threatEmissionRadiusMin);
            threatEmissionRadiusMax = math.max(threatEmissionRadiusMin, threatEmissionRadiusMax);
            threatSargassumRetentionBoost = math.saturate(threatSargassumRetentionBoost);
            threatTechnoJungleRetentionBoost = math.saturate(threatTechnoJungleRetentionBoost);
            threatSargassumAccumulationBoost = math.max(0f, threatSargassumAccumulationBoost);
            threatTechnoJungleAccumulationBoost = math.max(0f, threatTechnoJungleAccumulationBoost);
            flowFieldThreatBias = math.max(0f, flowFieldThreatBias);
            flowFieldPlayerBias = math.max(0f, flowFieldPlayerBias);
            flowFieldHotspotBias = math.max(0f, flowFieldHotspotBias);
            flowFieldObstacleAvoidBias = math.max(0f, flowFieldObstacleAvoidBias);
            flowFieldNavSupportBias = math.max(0f, flowFieldNavSupportBias);
            flowFieldKelpObstacleWeight = math.max(0f, flowFieldKelpObstacleWeight);
            flowFieldSargassumObstacleWeight = math.max(0f, flowFieldSargassumObstacleWeight);
            flowFieldTechnoObstacleWeight = math.max(0f, flowFieldTechnoObstacleWeight);
            flowFieldObstacleSoftThreshold = math.saturate(flowFieldObstacleSoftThreshold);
            flowFieldObstacleHardThreshold = math.clamp(flowFieldObstacleHardThreshold, flowFieldObstacleSoftThreshold, 1f);
            flowFieldHotspotMinimumThreat = math.saturate(flowFieldHotspotMinimumThreat);
            flowFieldNavStencilRadiusCells = math.clamp(flowFieldNavStencilRadiusCells, 0, 3);
            artificialStructureThreatSuppression = math.saturate(artificialStructureThreatSuppression);
            artificialStructureHazardAttraction = math.saturate(artificialStructureHazardAttraction);
            abyssalNavNodeStepMeters = math.max(4f, abyssalNavNodeStepMeters);
            abyssalNavNodeHoverHeight = math.max(0.5f, abyssalNavNodeHoverHeight);
            abyssalNavNodeObstacleRadius = math.max(1f, abyssalNavNodeObstacleRadius);
            abyssalNavNodeObstacleVerticalWindow = math.max(0.5f, abyssalNavNodeObstacleVerticalWindow);
            abyssalNavNodeMaxObstacleDensity = math.max(0f, abyssalNavNodeMaxObstacleDensity);
            abyssalNavNodeMaxCurrentMagnitude = math.max(0f, abyssalNavNodeMaxCurrentMagnitude);
            abyssalNavNodeMinimumDeepAffinity = math.max(0f, abyssalNavNodeMinimumDeepAffinity);
            abyssalPathNeighborRadius = math.max(4f, abyssalPathNeighborRadius);
            abyssalNavGraphCellSize = math.max(4f, abyssalNavGraphCellSize);
            abyssalPathVerticalTolerance = math.max(1f, abyssalPathVerticalTolerance);
            abyssalPathThreatPenaltyWeight = math.max(0f, abyssalPathThreatPenaltyWeight);
            abyssalConduitStartDepth = math.max(0f, abyssalConduitStartDepth);
            abyssalConduitMinimumFlowMagnitude = math.max(0f, abyssalConduitMinimumFlowMagnitude);
            abyssalConduitVerticalToleranceBonus = math.max(0f, abyssalConduitVerticalToleranceBonus);
            abyssalConduitMisalignmentPenalty = math.max(0f, abyssalConduitMisalignmentPenalty);
            abyssalConduitAlignmentReward = math.saturate(abyssalConduitAlignmentReward);
            abyssalPathRetargetDistance = math.max(0f, abyssalPathRetargetDistance);
            abyssalPathMaxExpandedNodes = math.clamp(abyssalPathMaxExpandedNodes, 64, 8192);
            maxAbyssalNavNodeCapacity = math.clamp(maxAbyssalNavNodeCapacity, 256, 32768);
            maxAbyssalPathWaypointCapacity = math.clamp(maxAbyssalPathWaypointCapacity, 64, 32768);
            abyssalInteriorTraversalCostMultiplier = math.clamp(abyssalInteriorTraversalCostMultiplier, 1f, 2f);
            permanentThreatEchoFloor = math.saturate(permanentThreatEchoFloor);
            permanentThreatEchoThreshold = math.clamp(permanentThreatEchoThreshold, math.max(0.3f, permanentThreatEchoFloor), 1f);
            predatorSpawnThreatBonusMultiplier = math.clamp(predatorSpawnThreatBonusMultiplier, 0f, 3f);
            predatorFearNodeCapacity = math.clamp(predatorFearNodeCapacity, 4, 128);
            predatorFearLifetimeSeconds = math.max(120f, predatorFearLifetimeSeconds);
            predatorFearSectorSizeMeters = math.max(100f, predatorFearSectorSizeMeters);
            predatorFearNodeRadiusMeters = math.max(1f, predatorFearNodeRadiusMeters);
            predatorFearPathPenaltyWeight = math.clamp(predatorFearPathPenaltyWeight, 0f, 4f);
            predatorFearCognitionPressureScale = math.saturate(predatorFearCognitionPressureScale);
            permanentEchoTechnoJungleThresholdBias = math.saturate(permanentEchoTechnoJungleThresholdBias);
            permanentEchoDeadZoneKeepBoost = math.saturate(permanentEchoDeadZoneKeepBoost);
            thermalGridRadius = math.max(100f, thermalGridRadius);
            thermalGridHorizontalCellSize = math.max(5f, thermalGridHorizontalCellSize);
            thermalGridVerticalCellSize = math.max(10f, thermalGridVerticalCellSize);
            thermalGridDepthMeters = math.max(500f, thermalGridDepthMeters);
            thermalDepthFalloffExponent = math.max(0.25f, thermalDepthFalloffExponent);
            thermalThermoclineDepth = math.clamp(thermalThermoclineDepth, 0f, thermalGridDepthMeters);
            thermalHotPocketBoostCelsius = math.max(0f, thermalHotPocketBoostCelsius);
            thermalHotPocketNoiseScale = math.max(0.0001f, thermalHotPocketNoiseScale);
            thermalHotPocketThreshold = math.saturate(thermalHotPocketThreshold);
            thermalColonyPocketStrength = math.max(0f, thermalColonyPocketStrength);
            thermalDeadZonePocketStrength = math.max(0f, thermalDeadZonePocketStrength);
            megaWreckInteriorHolePadding = math.max(0f, megaWreckInteriorHolePadding);
            megaWreckInteriorMinimumHoleRadius = math.max(1f, megaWreckInteriorMinimumHoleRadius);
            deepColdPocketTemperatureThresholdCelsius = math.clamp(deepColdPocketTemperatureThresholdCelsius, -10f, 10f);
            deepColdPocketStressMultiplierMax = math.clamp(deepColdPocketStressMultiplierMax, 1f, 4f);
            maxTrackedCorruptedChunks = math.max(32, maxTrackedCorruptedChunks);
            _floatingFlowDirectionNormalized = floatingFlowDirection;
            _sandMaskThresholdByte = PackMask01(sandMaskThreshold);
            _rockMaskThresholdByte = PackMask01(rockMaskThreshold);
            InitializeThreatGridMetadata();
            InitializeCanopyGridMetadata();
            InitializeThermalGridMetadata();

            if (!Application.isPlaying)
            {
                _runtimeLifecycleActive = false;
                _runtimeTeardownComplete = false;
                ResetDeferredStartupWork();
                return;
            }

            RefreshColdRuntimeDependencies();

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
            EnsureDensityQueryScratchCapacity(InitialChunkArrayCapacity);
            EnsureDensityQuerySnapshotLeaseBankCapacity(
                InitialChunkArrayCapacity,
                includeDensityGrid: true,
                includeThreatAttractorGrid: false);
            EnsureChunkBuildRecordBanks();
            // COLD ALLOC: TerrainHoleRecord[initialTerrainHoleCapacity] - persistent cave-entrance suppression registry - owner: HectonMapMagicVegetationBridge
            _terrainHoleRecords = new TerrainHoleRecord[math.max(4, initialTerrainHoleCapacity)];
            // COLD ALLOC: TerrainHoleStreamingRecord[initialTerrainHoleCapacity] - terrain-hole streaming snapshot growth cache - owner: HectonMapMagicVegetationBridge
            _terrainHoleStreamingRecords = new TerrainHoleStreamingRecord[math.max(4, initialTerrainHoleCapacity)];
            CacheVegetationMemoryVaultCold();
            EnsureVegetationMemoryTelemetryCold();
            EnsureHLODSnapshotCapacityCold();
            PreallocateAbyssalNavigationBuffers();
            InitializeChunkPools();
            _runtimeLifecycleActive = true;
            _runtimeTeardownComplete = false;
        }

        public bool TryGetFloraTemplateRuntimeDescriptor(int templateIndex, out FloraDataTemplate.RuntimeDescriptor descriptor)
        {
            if (_floraTemplateRuntimeDescriptors == null ||
                templateIndex < 0 ||
                templateIndex >= _floraTemplateRuntimeDescriptors.Length)
            {
                descriptor = default;
                return false;
            }

            descriptor = _floraTemplateRuntimeDescriptors[templateIndex];
            return true;
        }

        private void RebuildFloraTemplateRuntimeDescriptors()
        {
            if (floraTemplates == null || floraTemplates.Length == 0)
            {
                _floraTemplateRuntimeDescriptors = Array.Empty<FloraDataTemplate.RuntimeDescriptor>();
                return;
            }

            // COLD ALLOC: FloraDataTemplate.RuntimeDescriptor[floraTemplates.Length] - immutable flora authoring runtime cache for indirect vegetation stamping - owner: HectonMapMagicVegetationBridge
            FloraDataTemplate.RuntimeDescriptor[] descriptors = new FloraDataTemplate.RuntimeDescriptor[floraTemplates.Length];
            for (int i = 0; i < floraTemplates.Length; i++)
                descriptors[i] = floraTemplates[i] != null ? floraTemplates[i].BuildRuntimeDescriptor() : default;

            _floraTemplateRuntimeDescriptors = descriptors;
        }

        private void OnEnable()
        {
            if (!_runtimeLifecycleActive)
            {
                ClearActiveRuntimeInstance();

                return;
            }

            _runtimeTeardownComplete = false;
            PublishActiveRuntimeInstance();
            CacheVegetationMemoryVaultCold();
            EnsureVegetationMemoryTelemetryCold();
            InitializeChunkPools();
            EnsureChunkBuildRecordBanks();
            EnsureDensityQueryScratchCapacity(InitialChunkArrayCapacity);
            EnsureDensityQuerySnapshotLeaseBankCapacity(
                InitialChunkArrayCapacity,
                includeDensityGrid: true,
                includeThreatAttractorGrid: false);
            EnsureHLODSnapshotCapacityCold();
            PreallocateAbyssalNavigationBuffers();
            BindRendererSources();
            CacheWeatherService(GlobalRegistry.Weather);
            CacheOceanKinematicsService(GlobalRegistry.OceanKinematics);
            RefreshColdRuntimeDependencies();
            TryRegisterHotSwapListener();
            TrySubscribeEvents();
            TryRegister();
            QueueDeferredStartupWork();
        }

        private void Start()
        {
            if (!_runtimeLifecycleActive)
                return;

            BindRendererSources();
            TryRegister();
        }

        private void OnDisable()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                ClearActiveRuntimeInstance();

                return;
            }

            ClearActiveRuntimeInstance();
            CompleteThreatPropagationJob(forceComplete: true);
            CompleteFlowFieldJob(forceComplete: true);
            CompleteThermalGridJob(forceComplete: true);
            CompleteAbyssalPathJob(forceComplete: true);
            CompleteHLODCullJob(forceComplete: true);
            CompleteNativePoolDefragIfReady(forceComplete: true);
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
            DisposeChunkBuildRecordBanks();
            DisposeAllTileNativeCaches();
            DisposeTerrainHoleCache();
            DisposeActiveNativeAggregates();
            DisposeDensityQuerySnapshot();
            DisposeThreatGridState();
            DisposeFlowFieldState();
            DisposeThermalGridState();
            DisposeAbyssalPathState();
            DisposeHLODRegistryState();
            DisposeArtificialStructureSnapshot();
            DisposePoolDefragState();
            DisposeCanopyGridState();
            ClearRendererBindings();
            ReleaseBuffers();
            ReleaseVegetationMemoryTelemetryResources();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _nativeMemory.Dispose();
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            ClearVegetationMacroGeologyParamsCache();
            CacheOceanKinematicsService(null);
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            ResetDeferredStartupWork();
            _runtimeTeardownComplete = true;
        }

        private void OnDestroy()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                ClearActiveRuntimeInstance();

                return;
            }

            ClearActiveRuntimeInstance();
            CompleteThreatPropagationJob(forceComplete: true);
            CompleteFlowFieldJob(forceComplete: true);
            CompleteThermalGridJob(forceComplete: true);
            CompleteAbyssalPathJob(forceComplete: true);
            CompleteHLODCullJob(forceComplete: true);
            CompleteNativePoolDefragIfReady(forceComplete: true);
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
            DisposeChunkBuildRecordBanks();
            DisposeAllTileNativeCaches();
            DisposeTerrainHoleCache();
            DisposeActiveNativeAggregates();
            DisposeDensityQuerySnapshot();
            DisposeThreatGridState();
            DisposeFlowFieldState();
            DisposeThermalGridState();
            DisposeAbyssalPathState();
            DisposeHLODRegistryState();
            DisposeArtificialStructureSnapshot();
            DisposePoolDefragState();
            DisposeCanopyGridState();
            ClearRendererBindings();
            ReleaseBuffers();
            ReleaseVegetationMemoryTelemetryResources();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _nativeMemory.Dispose();
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            ClearVegetationMacroGeologyParamsCache();
            CacheOceanKinematicsService(null);
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            _runtimeTeardownComplete = true;
            _runtimeLifecycleActive = false;
        }

        /// <summary>
        /// Polls in-flight chunk generation jobs and binds finished payloads without blocking the schedule path.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            QueueDeferredTileCacheDisposal();
            SyncWaterSurfaceLevelFromRuntime();
            float clampedDt = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            AdvanceVegetationRuntimeClock(clampedDt);
            _predatorFearSimulationTime += clampedDt;
            _predatorFearPruneTimer += clampedDt;
            if (_predatorFearPruneTimer >= 1f)
            {
                _predatorFearPruneTimer = 0f;
                CompactPredatorFearNodes(_predatorFearSimulationTime);
                if (!_abyssalPathScheduled)
                    SyncPredatorFearNodeSnapshot(_predatorFearSimulationTime);
            }

            if (_externalThreatPulseHoldTimer > 0f)
                _externalThreatPulseHoldTimer = math.max(0f, _externalThreatPulseHoldTimer - dt);

            if (QueueDeferredStartupProgressIfPending())
                return;

            UpdatePlayerMotionState(dt);
            UpdateNativePoolDefragState(dt);

            if (QueueResidentTileCacheValidation())
            {
                ScheduleHLODVisibilityCullJob();
                return;
            }

            if (!_activeSetDirty)
            {
                TryScheduleNativePoolDefrag();
                ScheduleHLODVisibilityCullJob();
                return;
            }

            if (_activeSetDirty)
            {
                _activeBufferRebuildRequested = true;
                ScheduleHLODVisibilityCullJob();
                return;
            }

            TryScheduleNativePoolDefrag();
            ScheduleHLODVisibilityCullJob();
        }

        /// <summary>
        /// Re-evaluates active chunk residency and incrementally scans missing virtual chunks.
        /// </summary>
        public void SlowTick()
        {
            QueueDeferredTileCacheDisposal();

            if (QueueDeferredStartupProgressIfPending())
                return;

            RefreshResidency();
            SyncMegaWreckInteriorTerrainHoles();
            EvictDistantTerrainHoles();
            TryScheduleTerrainHoleJobs();
            RebuildHLODRegistrySnapshot();
            FlushTileHeightReadbackRepairsSlow();
            QueueResidentTileCacheValidation();
            if (CanRefreshThreatSpatialSnapshots())
            {
                RebuildArtificialStructureThreatSnapshot();
                PrepareThreatSamplingSnapshot();
                ScheduleThreatSpatialVisualSolvePhase();
            }
            UpdateVegetationAudioHandoff();
            LogNativePoolFragmentationIfDue();
        }

        /// <summary>
        /// Recovers completed vegetation jobs inside the dispatcher-owned late-frame barrier.
        /// </summary>
        public void LateFrameTick()
        {
            // L19 hop2 LIVE: batch peel LateFrameTick - multi-job Completes + buffer rebuild +
            // tile-cache disposal hang/assert headless after VERBSWEEP (IUpdatable hang audit HIGH).
            if (UnityEngine.Application.isBatchMode)
                return;

            _insideLateFrameJobSwap = true;
            try
            {
                if (_deferredTileCacheDisposalRequested)
                {
                    _deferredTileCacheDisposalRequested = false;
                    TryDisposeDeferredTileCacheReadbacks();
                    TryFinalizeDeferredTileCacheDisposals();
                }

                if (_deferredStartupProgressRequested)
                {
                    _deferredStartupProgressRequested = false;
                    TryProgressDeferredStartupWork();
                }

                if (_residentTileCacheValidationRequested)
                {
                    _residentTileCacheValidationRequested = false;
                    if (TryValidateResidentTileCaches())
                    {
                        RefreshResidency();
                        _activeBufferRebuildRequested = true;
                    }
                }

                int completedCount = FinalizeCompletedChunkBuilds();
                if (TryFinalizeDeferredTileCacheDisposals())
                    _activeBufferRebuildRequested = true;
                if (completedCount > 0)
                    EnforceChunkPoolMemoryGuard();

                bool selectionChanged = completedCount > 0 && SyncSelectedChunksFromDesired();
                if (completedCount > 0 || selectionChanged || _activeSetDirty || _activeBufferRebuildRequested)
                {
                    if (RebuildAndBindActiveBuffers())
                    {
                        _activeSetDirty = false;
                        _activeBufferRebuildRequested = false;
                    }
                }

                FlushVegetationAudioHandoffVisualSync();
                FlushPredatorFearShaderPayloadVisualSync();
                CompleteAbyssalPathJob(forceComplete: false);
                CompleteHLODCullJob(forceComplete: false);
                CompleteNativePoolDefragIfReady(forceComplete: false);
                CompleteThreatPropagationJob(forceComplete: false);
                CompleteFlowFieldJob(forceComplete: false);
                CompleteThermalGridJob(forceComplete: false);
            }
            finally
            {
                _insideLateFrameJobSwap = false;
            }

            TryApplyPendingWorldOffset();
        }


        /// <summary>Active surface instance matrix buffer currently owned by this bridge.</summary>
        public GraphicsBuffer SurfaceInstanceMatrixBuffer => _surfaceInstanceBuffer;

        /// <summary>Active surface instance metadata buffer currently owned by this bridge.</summary>
        public GraphicsBuffer SurfaceInstanceDataBuffer => _surfaceInstanceDataBuffer;

        /// <summary>Active underwater instance matrix buffer currently owned by this bridge.</summary>
        public GraphicsBuffer UnderwaterInstanceMatrixBuffer => _underwaterInstanceBuffer;

        /// <summary>Active underwater instance metadata buffer currently owned by this bridge.</summary>
        public GraphicsBuffer UnderwaterInstanceDataBuffer => _underwaterInstanceDataBuffer;

        internal void BindReactivePhaseSeedBuffer(bool underwater, GraphicsBuffer buffer)
        {
            HectonIndirectVegetationRenderer targetRenderer = underwater ? underwaterRenderer : surfaceRenderer;
            if (targetRenderer == null)
                return;

            targetRenderer.BindFloraPhaseSeedBuffer(buffer);
        }

        /// <summary>Active surface matrix cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<Matrix4x4>.ReadOnly ActiveSurfaceMatricesNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.MatricesHandle, _surfaceFrontCount);

        /// <summary>Active surface metadata cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<HectonVegetationInstanceData>.ReadOnly ActiveSurfaceMetadataNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.MetadataHandle, _surfaceFrontCount);

        /// <summary>Active surface type cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<int>.ReadOnly ActiveSurfaceTypesNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.TypesHandle, _surfaceFrontCount);

        /// <summary>Active surface semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int>.ReadOnly ActiveSurfaceSemanticTypesNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.SemanticTypesHandle, _surfaceFrontCount);

        /// <summary>Active surface biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte>.ReadOnly ActiveSurfaceBiomeLayersNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.BiomeLayersHandle, _surfaceFrontCount);

        /// <summary>Active surface flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2>.ReadOnly ActiveSurfaceFlowDirectionsNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.FlowDirectionsHandle, _surfaceFrontCount);

        /// <summary>Active surface 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3>.ReadOnly ActiveSurfaceFlowVectorsNative =>
            ReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.FlowVectorsHandle, _surfaceFrontCount);

        /// <summary>Active underwater matrix cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<Matrix4x4>.ReadOnly ActiveUnderwaterMatricesNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.MatricesHandle, _underwaterFrontCount);

        /// <summary>Accumulated floating-origin offset applied at render/query time instead of rewriting chunk-pool matrices.</summary>
        public Vector3 TotalUniverseOffset => _totalUniverseOffset;

        /// <summary>Accumulated vegetation universe offset in double precision for AUP reconstruction.</summary>
        public double3 TotalUniverseOffsetDouble => _totalUniverseOffsetDouble;

        /// <summary>Converts current runtime-local coordinates into stable universe coordinates.</summary>
        public static Vector3 ToUniverseSpace(Vector3 runtimePosition) => ToVector3(ToUniverseSpaceDouble3(runtimePosition));

        /// <summary>Converts current runtime-local coordinates into stable universe coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToUniverseSpaceDouble3(Vector3 runtimePosition)
        {
            return global::Hecton8.World.AUPMath.ToDouble3(runtimePosition) - GlobalTotalUniverseOffsetDouble;
        }

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates.</summary>
        public static Vector3 ToRuntimeSpace(Vector3 stableUniversePosition) => ToVector3(ToRuntimeSpaceDouble3(stableUniversePosition));

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates.</summary>
        public static Vector3 ToRuntimeSpace(double3 universePosition) => ToVector3(ToRuntimeSpaceDouble3(universePosition));

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToRuntimeSpaceDouble3(Vector3 stableUniversePosition)
        {
            return ToRuntimeSpaceDouble3(global::Hecton8.World.AUPMath.ToDouble3(stableUniversePosition));
        }

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToRuntimeSpaceDouble3(double3 universePosition)
        {
            return universePosition + GlobalTotalUniverseOffsetDouble;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
            if (runtimeContext != null)
            {
                if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    playerAup = movementState.PredictedAup;
                    return playerAup.IsFinite();
                }
            }

            return false;
        }

        private bool TryResolvePlayerRuntimePositionFromAup(out Vector3 runtimePosition)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                runtimePosition = default;
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 resolved = AUPMath.ResolveCameraRelative(in playerAup, in originAup);
            if (!math.all(math.isfinite(resolved)))
            {
                runtimePosition = default;
                return false;
            }

            runtimePosition = new Vector3(resolved.x, resolved.y, resolved.z);
            return true;
        }

        /// <summary>Active underwater metadata cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<HectonVegetationInstanceData>.ReadOnly ActiveUnderwaterMetadataNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.MetadataHandle, _underwaterFrontCount);

        /// <summary>Active underwater type cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<int>.ReadOnly ActiveUnderwaterTypesNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.TypesHandle, _underwaterFrontCount);

        /// <summary>Active underwater semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int>.ReadOnly ActiveUnderwaterSemanticTypesNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.SemanticTypesHandle, _underwaterFrontCount);

        /// <summary>Active underwater biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte>.ReadOnly ActiveUnderwaterBiomeLayersNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.BiomeLayersHandle, _underwaterFrontCount);

        /// <summary>Active underwater flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2>.ReadOnly ActiveUnderwaterFlowDirectionsNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.FlowDirectionsHandle, _underwaterFrontCount);

        /// <summary>Active underwater 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3>.ReadOnly ActiveUnderwaterFlowVectorsNative =>
            ReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.FlowVectorsHandle, _underwaterFrontCount);

        /// <summary>Active resident abyssal anchor positions for sonar/acoustic consumers.</summary>
        public Vector3[] ActiveAbyssalAnchors => _abyssalAnchorPositions;

        /// <summary>Active resident abyssal anchor positions in persistent native memory for direct readback.</summary>
        public NativeArray<Vector3>.ReadOnly ActiveAbyssalAnchorsNative
        {
            get
            {
                return GetAbyssalAnchorNativeView();
            }
        }

        /// <summary>Active resident abyssal anchor positions as AUP in persistent native memory for acoustic consumers.</summary>
        public NativeArray<AbsoluteUniversePosition>.ReadOnly ActiveAbyssalAnchorAupsNative
        {
            get
            {
                return GetAbyssalAnchorAupNativeView();
            }
        }

        /// <summary>Number of active surface instances.</summary>
        public int ActiveSurfaceInstanceCount => _surfaceFrontCount;

        /// <summary>Number of active underwater instances.</summary>
        public int ActiveUnderwaterInstanceCount => _underwaterFrontCount;

        /// <summary>Incremented whenever the surface active aggregate is rebuilt or cleared.</summary>
        public int ActiveSurfaceAggregateRevision => _surfaceActiveAggregateRevision;

        /// <summary>Incremented whenever the underwater active aggregate is rebuilt or cleared.</summary>
        public int ActiveUnderwaterAggregateRevision => _underwaterActiveAggregateRevision;

        /// <summary>Number of active resident abyssal anchors currently exported by the bridge.</summary>
        public int ActiveAbyssalAnchorCount => ResolveAbyssalAnchorViewCount();

        /// <summary>Immutable managed snapshot of the current abyssal safe-navigation nodes.</summary>
        public Vector3[] ActiveAbyssalNavNodes => _abyssalNavNodeSnapshot;

        /// <summary>Immutable native snapshot of the current abyssal safe-navigation nodes.</summary>
        public NativeArray<Vector3>.ReadOnly ActiveAbyssalNavNodesNative
        {
            get
            {
                return GetAbyssalNavNodeSnapshotNativeView();
            }
        }

        /// <summary>Number of active abyssal safe-navigation nodes currently exported by the bridge.</summary>
        public int ActiveAbyssalNavNodeCount => ResolveAbyssalNavNodeViewCount();

        /// <summary>Current ecosystem threat grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float>.ReadOnly EcosystemThreatGrid
        {
            get
            {
                NativeArray<float> grid = GetThreatGridFloatView();
                return grid.IsCreated ? grid.AsReadOnly() : default;
            }
        }

        /// <summary>Compressed ecosystem threat grid used by AI/flow-field consumers that do not need float precision.</summary>
        public NativeArray<byte>.ReadOnly EcosystemThreatGridCompressed
        {
            get
            {
                NativeArray<byte> grid = GetThreatGridCompressedView();
                return grid.IsCreated ? grid.AsReadOnly() : default;
            }
        }

        /// <summary>Permanent threat-echo flags aligned to the compressed ecosystem threat grid. 1 means the cell never decays below the echo floor.</summary>
        public NativeArray<byte>.ReadOnly EcosystemThreatEchoFlags
        {
            get
            {
                NativeArray<byte> echoFlags = GetThreatGridEchoView();
                return echoFlags.IsCreated ? echoFlags.AsReadOnly() : default;
            }
        }

        /// <summary>Current ecosystem threat grid resolution in cells along one axis.</summary>
        public int EcosystemThreatGridResolution => _ecosystemThreatGridResolution;

        /// <summary>Current ecosystem threat grid center in world space.</summary>
        public Vector3 EcosystemThreatGridCenter => _ecosystemThreatGridCenter;

        /// <summary>Current abyssal flow-field. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float2>.ReadOnly EcosystemFlowField
        {
            get
            {
                NativeArray<float2> flowField = GetFlowFieldView();
                return flowField.IsCreated ? flowField.AsReadOnly() : default;
            }
        }

        /// <summary>Current abyssal flow-field center in world space.</summary>
        public Vector3 EcosystemFlowFieldCenter => _ecosystemFlowFieldCenter;

        /// <summary>Current abyssal thermal grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float>.ReadOnly AbyssalThermalGrid
        {
            get
            {
                NativeArray<float> grid = GetAbyssalThermalGridView();
                return grid.IsCreated ? grid.AsReadOnly() : default;
            }
        }

        /// <summary>Current 3D abyssal flow volume. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float3>.ReadOnly AbyssalFlowVolume
        {
            get
            {
                NativeArray<float3> volume = GetAbyssalFlowVolumeView();
                return volume.IsCreated ? volume.AsReadOnly() : default;
            }
        }

        /// <summary>Current abyssal thermal-grid center in world space.</summary>
        public Vector3 AbyssalThermalGridCenter => _abyssalThermalGridCenter;

        /// <summary>Current mega-wreck section streaming payload. Treat as read-only and reacquire after each rebuild.</summary>
        public NativeArray<MegaWreckStreamSection>.ReadOnly MegaWreckStreamSections
        {
            get
            {
                return TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.MegaWreckStreamSnapshotHandle,
                    BufferID.VegetationMegaWreckStreamSnapshot,
                    _megaWreckStreamCount,
                    out NativeArray<MegaWreckStreamSection>.ReadOnly sections)
                    ? sections
                    : default;
            }
        }

        /// <summary>Current immutable HLOD registry payload for large distant structures.</summary>
        public NativeArray<HLODData>.ReadOnly HLODRegistry =>
            _hlodRegistryCount > 0 &&
            TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.HlodRegistrySnapshotHandle,
                BufferID.VegetationHlodRegistrySnapshot,
                _hlodRegistryCount,
                out NativeArray<HLODData>.ReadOnly registry)
                ? registry
                : default;

        /// <summary>Current immutable visible HLOD payload after frustum and distance culling.</summary>
        public NativeArray<HLODData>.ReadOnly VisibleHLODRegistry =>
            _visibleHlodCount > 0 &&
            TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.VisibleHlodSnapshotHandle,
                BufferID.VegetationVisibleHlodSnapshot,
                _visibleHlodCount,
                out NativeArray<HLODData>.ReadOnly visible)
                ? visible
                : default;

        /// <summary>Current ecosystem threat hotspot level from the last completed propagation step.</summary>
        public float CurrentThreatHotspotLevel => _currentThreatHotspotLevel;

        /// <summary>Current ecosystem threat hotspot position from the last completed propagation step.</summary>
        public Vector3 CurrentThreatHotspotPosition => _currentThreatHotspotPosition;

        /// <summary>Latest native abyssal path result. Treat as read-only and reacquire after each completed path solve.</summary>
        public NativeArray<Vector3>.ReadOnly ActiveAbyssalPathNative
        {
            get
            {
                return GetAbyssalPathReadOnlyView();
            }
        }

        /// <summary>Number of valid waypoints in the latest completed abyssal path result.</summary>
        public int ActiveAbyssalPathCount => ResolveAbyssalPathViewCount();

        /// <summary>Explicit surface draw bounds used for the current indirect payload.</summary>
        public Bounds ActiveSurfaceDrawBounds => _surfaceDrawBounds;

        /// <summary>Explicit underwater draw bounds used for the current indirect payload.</summary>
        public Bounds ActiveUnderwaterDrawBounds => _underwaterDrawBounds;

        /// <summary>Current live chunk-payload occupancy in bytes across both persistent native pools.</summary>
        public long ChunkPayloadUsedBytes => _chunkPayloadUsedBytes;

        /// <summary>Hard occupancy guard in bytes used for aggressive far-chunk eviction.</summary>
        public long ChunkPayloadGuardBytes => math.max(MinimumNativePoolBudgetMb, nativePoolGuardMb) * 1024L * 1024L;

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
        /// <param name="matrices">Caller-owned matrix array with capacity for the active count.</param>
        /// <param name="types">Caller-owned vegetation type array with capacity for the active count.</param>
        /// <returns>Number of valid surface instances written into the arrays.</returns>
        public int CopyActiveSurfaceInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.MatricesHandle, _surfaceFrontCount, out NativeArray<Matrix4x4> sourceMatrices) &&
                   TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.TypesHandle, _surfaceFrontCount, out NativeArray<int> sourceTypes)
                ? CopyActiveInstances(sourceMatrices, sourceTypes, _surfaceFrontCount, ref matrices, ref types)
                : 0;
        }

        /// <summary>
        /// Copies the active underwater matrices and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array with capacity for the active count.</param>
        /// <param name="types">Caller-owned vegetation type array with capacity for the active count.</param>
        /// <returns>Number of valid underwater instances written into the arrays.</returns>
        public int CopyActiveUnderwaterInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.MatricesHandle, _underwaterFrontCount, out NativeArray<Matrix4x4> sourceMatrices) &&
                   TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.TypesHandle, _underwaterFrontCount, out NativeArray<int> sourceTypes)
                ? CopyActiveInstances(sourceMatrices, sourceTypes, _underwaterFrontCount, ref matrices, ref types)
                : 0;
        }

        /// <summary>
        /// Copies the active surface matrices, metadata payloads, and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array with capacity for the active count.</param>
        /// <param name="metadata">Caller-owned metadata array with capacity for the active count.</param>
        /// <param name="types">Caller-owned vegetation type array with capacity for the active count.</param>
        /// <returns>Number of valid surface instances written into the arrays.</returns>
        public int CopyActiveSurfacePayload(
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            return TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.MatricesHandle, _surfaceFrontCount, out NativeArray<Matrix4x4> sourceMatrices) &&
                   TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.MetadataHandle, _surfaceFrontCount, out NativeArray<HectonVegetationInstanceData> sourceMetadata) &&
                   TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.TypesHandle, _surfaceFrontCount, out NativeArray<int> sourceTypes)
                ? CopyActivePayload(
                    sourceMatrices,
                    sourceMetadata,
                    sourceTypes,
                    _surfaceFrontCount,
                    ref matrices,
                    ref metadata,
                    ref types)
                : 0;
        }

        /// <summary>
        /// Copies the active underwater matrices, metadata payloads, and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array with capacity for the active count.</param>
        /// <param name="metadata">Caller-owned metadata array with capacity for the active count.</param>
        /// <param name="types">Caller-owned vegetation type array with capacity for the active count.</param>
        /// <returns>Number of valid underwater instances written into the arrays.</returns>
        public int CopyActiveUnderwaterPayload(
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            return TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.MatricesHandle, _underwaterFrontCount, out NativeArray<Matrix4x4> sourceMatrices) &&
                   TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.MetadataHandle, _underwaterFrontCount, out NativeArray<HectonVegetationInstanceData> sourceMetadata) &&
                   TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.TypesHandle, _underwaterFrontCount, out NativeArray<int> sourceTypes)
                ? CopyActivePayload(
                    sourceMatrices,
                    sourceMetadata,
                    sourceTypes,
                    _underwaterFrontCount,
                    ref matrices,
                    ref metadata,
                    ref types)
                : 0;
        }

        /// <summary>
        /// Returns the current surface payload as native memory ready for direct GraphicsBuffer upload handoff.
        /// </summary>
        public bool TryGetActiveSurfaceNativePayload(
            out NativeArray<Matrix4x4> matrices,
            out NativeArray<HectonVegetationInstanceData> metadata,
            out NativeArray<int> types,
            out int count)
        {
            matrices = default;
            metadata = default;
            types = default;
            count = 0;
            int activeCount = _surfaceFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.MatricesHandle, activeCount, out matrices) ||
                !TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.MetadataHandle, activeCount, out metadata) ||
                !TryReadAggregateBuffer(in _surfaceAggregateFrontBuffers.TypesHandle, activeCount, out types) ||
                !matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated)
            {
                matrices = default;
                metadata = default;
                types = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current surface flow payload as native memory ready for ocean/renderer consumption.
        /// </summary>
        public bool TryGetActiveSurfaceFlowPayload(out NativeArray<Vector2>.ReadOnly flowDirections, out int count)
        {
            count = 0;
            int activeCount = _surfaceFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.FlowDirectionsHandle, activeCount, out flowDirections))
            {
                flowDirections = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current player-tile height texture payload for GPU consumers that need direct heightmap sampling.
        /// </summary>
        public bool TryGetActiveHeightTexturePayload(out TerrainHeightTexturePayload payload)
        {
            payload = default;
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) ||
                !TryFindPlayerTileState(playerRuntimePosition, out TileRuntimeState state) ||
                state == null)
            {
                return false;
            }

            Texture heightTexture = state.HeightTextureCache;
            if (heightTexture == null || state.HeightmapResolution <= 1)
                return false;

            if (!TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) || !heightSamples.IsCreated)
                return false;

            payload = new TerrainHeightTexturePayload(
                heightTexture,
                state.TerrainPosition,
                state.TerrainSize,
                state.HeightmapResolution,
                state.CacheRevision);
            return true;
        }

        /// <summary>
        /// Returns the current player-tile R16 height payload for AI and physics jobs.
        /// </summary>
        public bool TryGetActiveHeightSamplePayload(out TerrainHeightSamplePayload payload)
        {
            payload = default;
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) ||
                !TryFindPlayerTileState(playerRuntimePosition, out TileRuntimeState state) ||
                state == null)
            {
                return false;
            }

            return TryBuildHeightSamplePayload(state, out payload);
        }

        public bool TryGetActiveTerrainHeightSamplePayload(out TerrainHeightSamplePayloadDTO payload)
        {
            payload = default;
            if (!TryGetActiveHeightSamplePayload(out TerrainHeightSamplePayload source))
                return false;

            payload = new TerrainHeightSamplePayloadDTO(
                source.HeightSamples,
                source.TerrainPosition,
                source.TerrainSize,
                source.HeightmapResolution,
                source.CacheRevision);
            return TerrainHeightSamplePayloadDTO.IsValid(in payload);
        }

        /// <summary>
        /// Returns the R16 height payload for the terrain tile containing the requested world-space position.
        /// </summary>
        public bool TryGetHeightSamplePayload(float worldX, float worldZ, out TerrainHeightSamplePayload payload)
        {
            payload = default;
            if (!TryFindTileStateAtPosition(new Vector3(worldX, 0f, worldZ), out TileRuntimeState state) ||
                state == null)
            {
                return false;
            }

            return TryBuildHeightSamplePayload(state, out payload);
        }

        public bool TryGetTerrainHeightSamplePayload(float worldX, float worldZ, out TerrainHeightSamplePayloadDTO payload)
        {
            payload = default;
            if (!TryGetHeightSamplePayload(worldX, worldZ, out TerrainHeightSamplePayload source))
                return false;

            payload = new TerrainHeightSamplePayloadDTO(
                source.HeightSamples,
                source.TerrainPosition,
                source.TerrainSize,
                source.HeightmapResolution,
                source.CacheRevision);
            return TerrainHeightSamplePayloadDTO.IsValid(in payload);
        }

        private bool TryBuildHeightSamplePayload(TileRuntimeState state, out TerrainHeightSamplePayload payload)
        {
            payload = default;
            if (state == null ||
                state.HeightmapResolution <= 1 ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) ||
                !heightSamples.IsCreated)
            {
                return false;
            }

            payload = new TerrainHeightSamplePayload(
                heightSamples,
                state.TerrainPosition,
                state.TerrainSize,
                state.HeightmapResolution,
                state.CacheRevision);
            return TerrainHeightSamplePayload.IsValid(in payload);
        }

        /// <summary>
        /// Returns the current surface semantic payload as native memory for AI and deep-biome consumers.
        /// </summary>
        public bool TryGetActiveSurfaceSemanticPayload(
            out NativeArray<int>.ReadOnly semanticTypes,
            out NativeArray<byte>.ReadOnly biomeLayers,
            out int count)
        {
            semanticTypes = default;
            biomeLayers = default;
            count = 0;
            int activeCount = _surfaceFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.SemanticTypesHandle, activeCount, out semanticTypes) ||
                !TryReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.BiomeLayersHandle, activeCount, out biomeLayers))
            {
                semanticTypes = default;
                biomeLayers = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current surface 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveSurfaceFlowVectorPayload(out NativeArray<Vector3>.ReadOnly flowVectors, out int count)
        {
            count = 0;
            int activeCount = _surfaceFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _surfaceAggregateFrontBuffers.FlowVectorsHandle, activeCount, out flowVectors))
            {
                flowVectors = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current underwater payload as native memory ready for direct GraphicsBuffer upload handoff.
        /// </summary>
        public bool TryGetActiveUnderwaterNativePayload(
            out NativeArray<Matrix4x4> matrices,
            out NativeArray<HectonVegetationInstanceData> metadata,
            out NativeArray<int> types,
            out int count)
        {
            matrices = default;
            metadata = default;
            types = default;
            count = 0;
            int activeCount = _underwaterFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.MatricesHandle, activeCount, out matrices) ||
                !TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.MetadataHandle, activeCount, out metadata) ||
                !TryReadAggregateBuffer(in _underwaterAggregateFrontBuffers.TypesHandle, activeCount, out types) ||
                !matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated)
            {
                matrices = default;
                metadata = default;
                types = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current underwater flow payload as native memory ready for ocean/renderer consumption.
        /// </summary>
        public bool TryGetActiveUnderwaterFlowPayload(out NativeArray<Vector2>.ReadOnly flowDirections, out int count)
        {
            count = 0;
            int activeCount = _underwaterFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.FlowDirectionsHandle, activeCount, out flowDirections))
            {
                flowDirections = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current underwater semantic payload as native memory for AI and deep-biome consumers.
        /// </summary>
        public bool TryGetActiveUnderwaterSemanticPayload(
            out NativeArray<int>.ReadOnly semanticTypes,
            out NativeArray<byte>.ReadOnly biomeLayers,
            out int count)
        {
            semanticTypes = default;
            biomeLayers = default;
            count = 0;
            int activeCount = _underwaterFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.SemanticTypesHandle, activeCount, out semanticTypes) ||
                !TryReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.BiomeLayersHandle, activeCount, out biomeLayers))
            {
                semanticTypes = default;
                biomeLayers = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current underwater 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveUnderwaterFlowVectorPayload(out NativeArray<Vector3>.ReadOnly flowVectors, out int count)
        {
            count = 0;
            int activeCount = _underwaterFrontCount;
            if (activeCount <= 0 ||
                !TryReadAggregateBufferReadOnly(in _underwaterAggregateFrontBuffers.FlowVectorsHandle, activeCount, out flowVectors))
            {
                flowVectors = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current resident abyssal-anchor positions as native memory for sonar/acoustic consumers.
        /// </summary>
        public bool TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3>.ReadOnly anchors, out int count)
        {
            count = ResolveAbyssalAnchorViewCount();
            anchors = count > 0 ? GetAbyssalAnchorNativeView() : default;
            return count > 0 && anchors.IsCreated;
        }

        /// <summary>
        /// Returns the current resident abyssal-anchor positions as AUP native memory for sonar/acoustic consumers.
        /// </summary>
        public bool TryGetActiveAbyssalAnchorAupPayload(out NativeArray<AbsoluteUniversePosition>.ReadOnly anchors, out int count)
        {
            count = ResolveAbyssalAnchorAupViewCount();
            anchors = count > 0 ? GetAbyssalAnchorAupNativeView() : default;
            return count > 0 && anchors.IsCreated;
        }

        /// <summary>
        /// Returns the current immutable abyssal-nav-node snapshot as native memory for pathfinding consumers.
        /// </summary>
        public bool TryGetActiveAbyssalNavNodePayload(out NativeArray<Vector3>.ReadOnly nodes, out int count)
        {
            count = ResolveAbyssalNavNodeViewCount();
            nodes = count > 0 ? GetAbyssalNavNodeSnapshotNativeView() : default;
            return count > 0 && nodes.IsCreated;
        }

        /// <summary>
        /// Returns the immutable current-conductor metadata aligned to the abyssal nav-node snapshot.
        /// </summary>
        public bool TryGetAbyssalCurrentConduitPayload(
            out NativeArray<Vector3>.ReadOnly conduitVectors,
            out NativeArray<float>.ReadOnly conduitStrengths,
            out int count)
        {
            conduitVectors = default;
            conduitStrengths = default;
            count = ResolveAbyssalConduitViewCount();
            bool hasConduits = count > 0 &&
                               TryReadOnlyVegetationMemoryBuffer(
                                   in _nativeMemory.AbyssalNavConduitVectorsHandle,
                                   BufferID.VegetationAbyssalNavConduitVectors,
                                   count,
                                   out conduitVectors) &&
                               TryReadOnlyVegetationMemoryBuffer(
                                   in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                                   BufferID.VegetationAbyssalNavConduitStrengths,
                                   count,
                                   out conduitStrengths);
            if (!hasConduits)
            {
                conduitVectors = default;
                conduitStrengths = default;
            }

            return hasConduits;
        }

        /// <summary>
        /// Returns the current native abyssal nav-graph payload, including immutable node snapshots and graph metadata.
        /// </summary>
        public bool TryGetNativeAbyssalNavGraph(
            out NativeArray<Vector3>.ReadOnly nodes,
            out NativeArray<byte>.ReadOnly nodeTypes,
            out NativeArray<Vector3>.ReadOnly conduitVectors,
            out NativeArray<float>.ReadOnly conduitStrengths,
            out NativeParallelMultiHashMap<int, int> spatialHash,
            out int count,
            out float cellSize,
            out Vector3 origin)
        {
            nodes = default;
            nodeTypes = default;
            conduitVectors = default;
            conduitStrengths = default;
            spatialHash = default;
            count = ResolveAbyssalNavGraphViewCount();
            cellSize = abyssalNavGraphCellSize;
            origin = _abyssalNavGraphOrigin;
            bool hasPayload = count > 0 &&
                              TryReadOnlyVegetationMemoryBuffer(
                                  in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                                  BufferID.VegetationAbyssalNavNodeSnapshot,
                                  count,
                                  out nodes) &&
                              TryReadOnlyVegetationMemoryBuffer(
                                  in _nativeMemory.AbyssalNavNodeTypesHandle,
                                  BufferID.VegetationAbyssalNavNodeTypes,
                                  count,
                                  out nodeTypes) &&
                              TryReadOnlyVegetationMemoryBuffer(
                                  in _nativeMemory.AbyssalNavConduitVectorsHandle,
                                  BufferID.VegetationAbyssalNavConduitVectors,
                                  count,
                                  out conduitVectors) &&
                              TryReadOnlyVegetationMemoryBuffer(
                                  in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                                  BufferID.VegetationAbyssalNavConduitStrengths,
                                  count,
                                  out conduitStrengths) &&
                              cellSize > 0f &&
                              math.isfinite(cellSize) &&
                              IsFinite(origin);
            if (!hasPayload)
            {
                nodes = default;
                nodeTypes = default;
                conduitVectors = default;
                conduitStrengths = default;
            }

            return hasPayload;
        }

        /// <summary>
        /// Returns the current ecosystem threat grid payload and metadata for external consumers.
        /// </summary>
        public bool TryGetEcosystemThreatGridPayload(
            out NativeArray<float>.ReadOnly threatLevels,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            NativeArray<float> threatView = GetThreatGridFloatView();
            threatLevels = threatView.IsCreated ? threatView.AsReadOnly() : default;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   threatView.IsCreated &&
                   HasCompleteEcosystemSquareGridState(threatView.Length) &&
                   cellSize > 0f &&
                   math.isfinite(cellSize) &&
                   IsFinite(gridCenter);
        }

        /// <summary>
        /// Returns the compressed ecosystem threat grid payload and metadata for low-cost AI consumers.
        /// </summary>
        public bool TryGetCompressedEcosystemThreatGridPayload(
            out NativeArray<byte>.ReadOnly threatLevels,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            NativeArray<byte> threatView = GetThreatGridCompressedView();
            threatLevels = threatView.IsCreated ? threatView.AsReadOnly() : default;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   threatView.IsCreated &&
                   HasCompleteEcosystemSquareGridState(threatView.Length) &&
                   cellSize > 0f &&
                   math.isfinite(cellSize) &&
                   IsFinite(gridCenter);
        }

        /// <summary>
        /// Returns the 3D byte voxel threat snapshot used by Burst DDA line-of-sight.
        /// Layout: [x + y * width + z * width * height].
        /// </summary>
        public bool TryGetEcosystemThreatVoxelPayload(
            out NativeArray<byte>.ReadOnly threatVoxels,
            out Vector3Int gridDimensions,
            out Vector3 gridOrigin,
            out Vector3 voxelCellSize)
        {
            NativeArray<byte> threatVoxelView = GetThreatVoxelView();
            threatVoxels = threatVoxelView.IsCreated ? threatVoxelView.AsReadOnly() : default;
            gridDimensions = new Vector3Int(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution);
            gridOrigin = _ecosystemThreatVoxelOrigin;
            voxelCellSize = new Vector3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize);
            return _threatGridInitialized &&
                   threatVoxels.IsCreated &&
                   TryResolveVoxelGridCellCount(gridDimensions, threatVoxels.Length, out int threatVoxelCellCount) &&
                   _ecosystemThreatVoxelCellCount >= threatVoxelCellCount &&
                   voxelCellSize.x > 0f &&
                   voxelCellSize.y > 0f &&
                   voxelCellSize.z > 0f &&
                   IsFinite(gridOrigin) &&
                   IsFinite(voxelCellSize);
        }

        /// <summary>
        /// Returns the permanent threat-echo flags aligned to the compressed ecosystem threat grid.
        /// </summary>
        public bool TryGetEcosystemThreatEchoPayload(
            out NativeArray<byte>.ReadOnly echoFlags,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            NativeArray<byte> echoView = GetThreatGridEchoView();
            echoFlags = echoView.IsCreated ? echoView.AsReadOnly() : default;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   echoView.IsCreated &&
                   HasCompleteEcosystemSquareGridState(echoView.Length) &&
                   cellSize > 0f &&
                   math.isfinite(cellSize) &&
                   IsFinite(gridCenter);
        }

        /// Returns the current abyssal flow-field payload and metadata for external consumers.
        /// </summary>
        public bool TryGetMegaWreckStreamPayload(out NativeArray<MegaWreckStreamSection>.ReadOnly sections, out int count)
        {
            sections = default;
            count = 0;
            int activeCount = _megaWreckStreamCount;
            if (activeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.MegaWreckStreamSnapshotHandle,
                    BufferID.VegetationMegaWreckStreamSnapshot,
                    activeCount,
                    out sections))
            {
                sections = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current HLOD registry payload for large persistent structures and mega-wreck silhouettes.
        /// </summary>
        public bool TryGetTerrainHoleStreamingPayload(out NativeArray<TerrainHoleStreamingRecord>.ReadOnly holes, out int count)
        {
            holes = default;
            count = 0;
            int activeCount = _terrainHoleCount;
            if (activeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.TerrainHoleStreamingRecordsHandle,
                    BufferID.VegetationTerrainHoleStreamingRecords,
                    activeCount,
                    out holes))
            {
                holes = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the current global canopy-height grid for audio and light-occlusion consumers.
        /// </summary>
        public bool TryGetCanopyHeightGridPayload(out NativeArray<float>.ReadOnly canopyHeights, out int gridResolution, out Vector3 gridCenter, out float cellSize)
        {
            canopyHeights = default;
            gridResolution = 0;
            gridCenter = Vector3.zero;
            cellSize = 0f;
            int activeGridResolution = _canopyGridResolution;
            float activeCellSize = canopyGridCellSize;
            if (!_canopyGridInitialized ||
                activeGridResolution <= 0 ||
                activeCellSize <= 0f ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.CanopyHeightGridHandle,
                    BufferID.VegetationCanopyHeightGrid,
                    _canopyGridCellCount,
                    out canopyHeights))
            {
                canopyHeights = default;
                return false;
            }

            gridResolution = activeGridResolution;
            gridCenter = _canopyGridCenter;
            cellSize = activeCellSize;
            return true;
        }

        /// <summary>
        /// Returns the immutable abyssal-nav node classifications aligned to the active node snapshot.
        /// </summary>
        public bool TryGetActiveAbyssalNavNodeTypePayload(out NativeArray<byte>.ReadOnly nodeTypes, out int count)
        {
            nodeTypes = default;
            count = 0;
            int activeCount = ResolveAbyssalNavNodeTypeViewCount();
            if (activeCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    activeCount,
                    out nodeTypes))
            {
                nodeTypes = default;
                return false;
            }

            count = activeCount;
            return true;
        }

        /// <summary>
        /// Returns the currently active artificial interior state that suppresses exterior biome effects while the player remains inside a streamed mega-wreck interior.
        /// </summary>
        public bool TryGetActiveArtificialInteriorState(out ArtificialInteriorState state)
        {
            state = _activeArtificialInteriorState;
            return state.IsActive != 0;
        }
        /// Samples biomass density immediately on the main thread from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleBiomassDensityImmediate(Vector3 positionWS, int typeMask = DensityTypeMaskAll)
        {
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
                return 0f;

            if (!TryReadDensityQuerySnapshot(
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid))
            {
                return 0f;
            }

            float3 position = new float3(positionWS.x, positionWS.y, positionWS.z);
            return ApplyDensityTypeMask(
                SampleDensityChannelsAtPosition(position, chunks, densityGrid, _densityQueryChunkCount),
                typeMask);
        }

        /// <summary>
        /// Samples terrain height from the active native tile cache without allocating.
        /// </summary>
        public bool TryGetCachedTerrainHeight(float worldX, float worldZ, out float terrainHeight)
        {
            terrainHeight = 0f;
            if (!TryFindTileStateAtPosition(new Vector3(worldX, 0f, worldZ), out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples))
            {
                return false;
            }

            return TrySampleCachedTerrainHeight(state, heightSamples, worldX, worldZ, out terrainHeight);
        }

        /// <summary>
        /// Samples the current abyssal flow volume at a world-space position without allocations.
        /// </summary>
        public bool TrySampleAbyssalFlow(Vector3 position, out Vector3 flowVector)
        {
            flowVector = Vector3.zero;
            if (!_abyssalFlowVolumeInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalFlowVolumeHandle,
                    BufferID.VegetationAbyssalFlowVolume,
                    _abyssalThermalGridCellCount,
                    out NativeArray<float3> flowVolume) ||
                _abyssalThermalGridResolutionXZ <= 0 ||
                _abyssalThermalGridResolutionY <= 0 ||
                thermalGridHorizontalCellSize <= 0f ||
                thermalGridVerticalCellSize <= 0f ||
                !math.isfinite(thermalGridHorizontalCellSize) ||
                !math.isfinite(thermalGridVerticalCellSize) ||
                thermalGridDepthMeters <= 0f ||
                !math.isfinite(thermalGridDepthMeters) ||
                !math.isfinite(waterLevel) ||
                !IsFinite(position) ||
                !IsFinite(_abyssalThermalGridCenter))
            {
                return false;
            }

            long expectedFlowVolumeLength = (long)_abyssalThermalGridResolutionXZ *
                                            _abyssalThermalGridResolutionXZ *
                                            _abyssalThermalGridResolutionY;
            if (expectedFlowVolumeLength <= 0L ||
                expectedFlowVolumeLength > int.MaxValue ||
                flowVolume.Length < expectedFlowVolumeLength)
            {
                return false;
            }

            float halfExtent = (_abyssalThermalGridResolutionXZ - 1) * 0.5f * thermalGridHorizontalCellSize;
            if (!math.isfinite(halfExtent))
            {
                return false;
            }

            float minX = _abyssalThermalGridCenter.x - halfExtent;
            float minZ = _abyssalThermalGridCenter.z - halfExtent;
            float maxY = waterLevel;
            float minY = waterLevel - thermalGridDepthMeters;
            if (position.x < minX || position.z < minZ || position.x > minX + (halfExtent * 2f) || position.z > minZ + (halfExtent * 2f))
                return false;

            float clampedY = math.clamp(position.y, minY, maxY);
            float inverseHorizontalCellSize = math.rcp(thermalGridHorizontalCellSize);
            float inverseVerticalCellSize = math.rcp(thermalGridVerticalCellSize);
            float normalizedX = math.clamp((position.x - minX) * inverseHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedZ = math.clamp((position.z - minZ) * inverseHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedY = math.clamp((maxY - clampedY) * inverseVerticalCellSize, 0f, _abyssalThermalGridResolutionY - 1);
            int x0 = math.clamp((int)math.floor(normalizedX), 0, _abyssalThermalGridResolutionXZ - 1);
            int z0 = math.clamp((int)math.floor(normalizedZ), 0, _abyssalThermalGridResolutionXZ - 1);
            int y0 = math.clamp((int)math.floor(normalizedY), 0, _abyssalThermalGridResolutionY - 1);
            int x1 = math.min(x0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int z1 = math.min(z0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int y1 = math.min(y0 + 1, _abyssalThermalGridResolutionY - 1);
            float fracX = normalizedX - x0;
            float fracZ = normalizedZ - z0;
            float fracY = normalizedY - y0;

            float3 sample000 = flowVolume[GetThermalGridPhysicalIndex(x0, y0, z0)];
            float3 sample100 = flowVolume[GetThermalGridPhysicalIndex(x1, y0, z0)];
            float3 sample010 = flowVolume[GetThermalGridPhysicalIndex(x0, y0, z1)];
            float3 sample110 = flowVolume[GetThermalGridPhysicalIndex(x1, y0, z1)];
            float3 sample001 = flowVolume[GetThermalGridPhysicalIndex(x0, y1, z0)];
            float3 sample101 = flowVolume[GetThermalGridPhysicalIndex(x1, y1, z0)];
            float3 sample011 = flowVolume[GetThermalGridPhysicalIndex(x0, y1, z1)];
            float3 sample111 = flowVolume[GetThermalGridPhysicalIndex(x1, y1, z1)];
            float3 sampleX00 = math.lerp(sample000, sample100, fracX);
            float3 sampleX10 = math.lerp(sample010, sample110, fracX);
            float3 sampleX01 = math.lerp(sample001, sample101, fracX);
            float3 sampleX11 = math.lerp(sample011, sample111, fracX);
            float3 sampleZ0 = math.lerp(sampleX00, sampleX10, fracZ);
            float3 sampleZ1 = math.lerp(sampleX01, sampleX11, fracZ);
            float3 sampledFlow = math.lerp(sampleZ0, sampleZ1, fracY);
            if (!math.all(math.isfinite(sampledFlow)))
            {
                return false;
            }

            flowVector = new Vector3(sampledFlow.x, sampledFlow.y, sampledFlow.z);
            return true;
        }

        /// <summary>
        /// Tests cached terrain normal.y against a threshold from finite height gradients without allocating.
        /// </summary>
        public bool TryPassTerrainNormalYThreshold(Vector3 position, float sampleDistance, float minimumNormalY)
        {
            float resolvedSampleDistance = math.max(0.5f, sampleDistance);
            if (!TryGetCachedTerrainHeight(position.x + resolvedSampleDistance, position.z, out float heightPosX) ||
                !TryGetCachedTerrainHeight(position.x - resolvedSampleDistance, position.z, out float heightNegX) ||
                !TryGetCachedTerrainHeight(position.x, position.z + resolvedSampleDistance, out float heightPosZ) ||
                !TryGetCachedTerrainHeight(position.x, position.z - resolvedSampleDistance, out float heightNegZ))
            {
                return false;
            }

            float inverseSampleDiameter = math.rcp(resolvedSampleDistance * 2f);
            float gradientX = (heightPosX - heightNegX) * inverseSampleDiameter;
            float gradientZ = (heightPosZ - heightNegZ) * inverseSampleDiameter;
            float normalLengthSq = 1f + (gradientX * gradientX) + (gradientZ * gradientZ);
            float safeMinimumNormalY = math.saturate(minimumNormalY);
            float minimumNormalYSq = safeMinimumNormalY * safeMinimumNormalY;
            return normalLengthSq * minimumNormalYSq <= 1f;
        }

        private static float FastGradientMagnitude(float magnitudeSq)
        {
            float x = math.max(0f, magnitudeSq);
            float safe = math.max(x, 0.000000000001f);
            int estimateBits = (math.asint(safe) >> 1) + 0x1FBD1DF5;
            float estimate = math.asfloat(estimateBits);
            return math.select(0f, 0.5f * (estimate + safe / math.max(estimate, 0.000000000001f)), x > 0f);
        }

        /// <summary>
        /// Resolves the dominant cached substrate mask under a runtime-space flora query position.
        /// </summary>
        public bool TrySampleFloraSubstrate(Vector3 position, out WorldProceduralPlacementRule.FloraSubstrateMask substrate)
        {
            substrate = WorldProceduralPlacementRule.FloraSubstrateMask.None;
            if (IsInsideRegisteredTerrainHole(position.x, position.z))
                return false;

            if (TrySampleMacroGeologyFloraSubstrate(position, out substrate))
                return true;

            if (!TryFindTileStateAtPosition(position, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out _))
            {
                return false;
            }

            float localX = position.x - state.TerrainPosition.x;
            float localZ = position.z - state.TerrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > state.TerrainSize.x || localZ > state.TerrainSize.z || state.AlphamapResolution <= 0)
                return false;

            float normalizedX = math.saturate(localX / math.max(0.01f, state.TerrainSize.x));
            float normalizedZ = math.saturate(localZ / math.max(0.01f, state.TerrainSize.z));
            int alphaX = math.clamp((int)math.floor(normalizedX * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            int alphaZ = math.clamp((int)math.floor(normalizedZ * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            int maskIndex = (alphaZ * state.AlphamapResolution) + alphaX;
            if (maskIndex < 0 || maskIndex >= sandMask.Length)
                return false;

            byte sandValue = sandMask[maskIndex];
            byte rockValue = rockMask.IsCreated && maskIndex < rockMask.Length ? rockMask[maskIndex] : (byte)0;
            byte packedSubstrate = PackFloraSubstrateBits(
                sandValue,
                rockValue,
                _sandMaskThresholdByte,
                _rockMaskThresholdByte);

            if ((packedSubstrate & FloraSubstrateSandBit) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Sand;

            if ((packedSubstrate & FloraSubstrateRockBit) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Rock;

            if (substrate == WorldProceduralPlacementRule.FloraSubstrateMask.None)
                substrate = sandValue >= rockValue
                    ? WorldProceduralPlacementRule.FloraSubstrateMask.Sand
                    : WorldProceduralPlacementRule.FloraSubstrateMask.Rock;

            return true;
        }

        private bool TrySampleMacroGeologyFloraSubstrate(Vector3 position, out WorldProceduralPlacementRule.FloraSubstrateMask substrate)
        {
            substrate = WorldProceduralPlacementRule.FloraSubstrateMask.None;
            if (!TryGetVegetationMacroGeologyParams(out WorldMacroGeologyParams macroParams, out WorldTerrainMesoDetailParams mesoParams))
                return false;

            WorldMacroGeologySample macro = WorldMacroGeologyFields.Evaluate(position.x, position.z, in macroParams);
            if (!math.isfinite(macro.HeightMeters))
                return false;

            WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(
                in macro,
                position.x,
                position.z,
                macroParams.Seed);
            WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(
                in macro,
                position.x,
                position.z,
                in mesoParams);
            weights = WorldTerrainSurfaceMaterialResolver.ApplyMesoDetailBias(weights, in meso);
            WorldTerrainDetailEligibilityFlags eligibility =
                WorldTerrainMesoDetailFields.ResolveEligibilityFlags(in macro, in meso, in weights);
            WorldTerrainSurfaceMaterialClass dominantMaterial =
                WorldTerrainSurfaceMaterialResolver.ResolveDominant(in weights);

            substrate = ScatterCandidateEvaluator.ResolveFloraSubstrateFromTerrainDetail(
                eligibility,
                dominantMaterial,
                in weights);
            return substrate != WorldProceduralPlacementRule.FloraSubstrateMask.None;
        }

        private bool TryGetVegetationMacroGeologyParams(
            out WorldMacroGeologyParams macroParams,
            out WorldTerrainMesoDetailParams mesoParams)
        {
            int runtimeWorldSeed = 0;
            if (global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int activeRuntimeWorldSeed))
                runtimeWorldSeed = activeRuntimeWorldSeed;

            float resolvedWaterLevel = math.isfinite(waterLevel) ? waterLevel : DefaultWaterLevel;
            if (_vegetationMacroGeologyParamsCached &&
                _vegetationMacroGeologyRuntimeSeedCache == runtimeWorldSeed &&
                math.abs(_vegetationMacroGeologyWaterLevelCache - resolvedWaterLevel) <= WaterLevelResyncEpsilonMeters)
            {
                macroParams = _vegetationMacroGeologyParamsCache;
                mesoParams = _vegetationMesoDetailParamsCache;
                return true;
            }

            macroParams = WorldMacroGeologyParams.CreateDefault(
                WorldMacroGeologyFields.CombineWorldSeed(
                    unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed),
                    runtimeWorldSeed));
            macroParams.WaterSurfaceY = resolvedWaterLevel;
            mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(macroParams.Seed);
            mesoParams.PreviewExtentMeters = WorldTerrainDetailContracts.MesoProofExtentMeters;

            _vegetationMacroGeologyParamsCache = macroParams;
            _vegetationMesoDetailParamsCache = mesoParams;
            _vegetationMacroGeologyRuntimeSeedCache = runtimeWorldSeed;
            _vegetationMacroGeologyWaterLevelCache = resolvedWaterLevel;
            _vegetationMacroGeologyParamsCached = true;
            return true;
        }

        private void ClearVegetationMacroGeologyParamsCache()
        {
            _vegetationMacroGeologyParamsCached = false;
            _vegetationMacroGeologyRuntimeSeedCache = int.MinValue;
            _vegetationMacroGeologyWaterLevelCache = float.NaN;
        }

        private static byte PackFloraSubstrateBits(byte sandValue, byte rockValue, byte sandThreshold, byte rockThreshold)
        {
            byte packed = 0;
            if (sandValue > sandThreshold)
                packed |= FloraSubstrateSandBit;
            if (rockValue > rockThreshold)
                packed |= FloraSubstrateRockBit;
            return packed;
        }

        /// <summary>
        /// Snaps a runtime-space scatter placement to the resident terrain cache without blocking on physics jobs.
        /// </summary>
        public bool TrySnapScatterPlacement(
            Vector3 position,
            float surfaceOffset,
            float maxTiltAngleDegrees,
            float yawDegrees,
            out Vector3 snappedPosition,
            out Quaternion snappedRotation)
        {
            snappedPosition = position;
            snappedRotation = Quaternion.identity;

            Vector3 surfacePoint;
            Vector3 surfaceNormal;
            if (!TrySampleScatterSurfaceFromCachedTerrain(position, out surfacePoint, out surfaceNormal))
            {
                return false;
            }

            if (!IsScatterSurfaceNormalSpawnable(surfaceNormal))
                return false;

            Vector3 surfaceUp = ResolveScatterSurfaceUpCheat(surfaceNormal);
            int yawSector = QuantizeYawDegreesToOctant(yawDegrees);
            Vector3 tangentForward = ResolveScatterSurfaceTangent(surfaceUp, yawSector);
            snappedPosition = surfacePoint + (surfaceUp * math.max(0f, surfaceOffset));
            snappedRotation = Quaternion.LookRotation(tangentForward, surfaceUp);
            return true;
        }

        /// <summary>
        /// Returns the dominant vegetation type and current density at a world-space position without allocations.
        /// </summary>
        public VegetationDensitySample GetVegetationDensity(Vector3 position)
        {
            float3 densityChannels = float3.zero;
            if (TryReadDensityQuerySnapshot(
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid))
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    chunks,
                    densityGrid,
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
        /// Returns a zero-allocation binary concealment state at the given world-space position.
        /// 0 = exposed, 1 = hidden by dense grass/sargassum cover while local threat remains low.
        /// </summary>
        public float GetPlayerVisibilityModifier(Vector3 position)
        {
            if (IsInsideRegisteredTerrainHole(position.x, position.z))
                return 0f;

            float3 densityChannels = float3.zero;
            if (TryReadDensityQuerySnapshot(
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid))
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    chunks,
                    densityGrid,
                    _densityQueryChunkCount);
            }

            if (math.lengthsq(densityChannels) <= 0.000001f &&
                TryResolveVegetationTypeFromCachedMasks(position, out HectonVegetationInstanceType fallbackType))
            {
                densityChannels = ResolveFallbackVisibilityChannels(position, fallbackType);
            }

            float grassCover = math.saturate(densityChannels.x * grassVisibilityWeight);
            float sargassumCover = math.saturate(
                densityChannels.z *
                sargassumVisibilityWeight *
                EvaluateSargassumVerticalConcealment(position.y));
            float threat = GetThreatLevel(position);
            if (threat >= PlayerVisibilityThreatExposureThreshold)
                return 0f;

            return (grassCover + sargassumCover) >= PlayerVisibilityDenseCoverThreshold ? 1f : 0f;
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
            if (!positions.IsCreated ||
                positions.Length <= 0 ||
                !outputVisibility.IsCreated ||
                outputVisibility.Length < positions.Length)
            {
                return false;
            }

            if (!TryPrepareDensityQueryJobSnapshot(
                    true,
                    false,
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid,
                    out NativeArray<float2> threatAttractorGrid,
                    out int densitySnapshotLease) ||
                !chunks.IsCreated ||
                !densityGrid.IsCreated)
            {
                ReleaseDensityQuerySnapshotLease(densitySnapshotLease);
                return false;
            }

            var job = new VegetationDensityQueryJob
            {
                Positions = positions,
                Output = outputVisibility,
                Chunks = chunks,
                DensityGrid = densityGrid,
                ChunkCount = _densityQueryChunkCount,
                GrassVisibilityWeight = grassVisibilityWeight,
                KelpVisibilityWeight = kelpVisibilityWeight,
                SargassumVisibilityWeight = sargassumVisibilityWeight,
                WaterLevel = waterLevel,
                FloatingSurfaceOffset = floatingSurfaceOffset,
                SargassumVisibilityBand = sargassumVisibilityBand
            };

            handle = job.Schedule(positions.Length, DefaultJobBatchSize);
            MarkDensityQuerySnapshotLeaseScheduled(densitySnapshotLease, handle);
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
            if (!positions.IsCreated ||
                positions.Length <= 0 ||
                !outputDensities.IsCreated ||
                outputDensities.Length < positions.Length)
            {
                return false;
            }

            if (!TryPrepareDensityQueryJobSnapshot(
                    true,
                    false,
                    out NativeArray<VegetationDensityChunkRecord> chunks,
                    out NativeArray<float3> densityGrid,
                    out NativeArray<float2> threatAttractorGrid,
                    out int densitySnapshotLease) ||
                !chunks.IsCreated ||
                !densityGrid.IsCreated)
            {
                ReleaseDensityQuerySnapshotLease(densitySnapshotLease);
                return false;
            }

            var job = new SampleBiomassDensityJob
            {
                Positions = positions,
                Output = outputDensities,
                Chunks = chunks,
                DensityGrid = densityGrid,
                ChunkCount = _densityQueryChunkCount,
                TypeMask = typeMask
            };

            handle = job.Schedule(positions.Length, DefaultJobBatchSize);
            MarkDensityQuerySnapshotLeaseScheduled(densitySnapshotLease, handle);
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
            if (!averageOutput.IsCreated || averageOutput.Length < 1)
                return false;

            if (!TryScheduleBiomassDensitySample(positions, perPointDensities, out JobHandle sampleHandle, typeMask))
                return false;

            var averageJob = new ReduceAverageDensityJob
            {
                Input = perPointDensities,
                Output = averageOutput
            };

            handle = averageJob.Schedule(sampleHandle);
            return true;
        }

        private void InitializeThreatGridMetadata()
        {
            int resolution = (int)math.round((threatGridRadius * 2f) * math.rcp(math.max(1f, threatGridCellSize))) + 1;
            if ((resolution & 1) == 0)
                resolution++;

            _ecosystemThreatGridResolution = math.max(3, resolution);
            _ecosystemThreatGridCellCount = _ecosystemThreatGridResolution * _ecosystemThreatGridResolution;
            _ecosystemThreatGridResolutionY = math.max(2, (int)math.round(thermalGridDepthMeters * math.rcp(math.max(1f, thermalGridVerticalCellSize))) + 1);
            long voxelCellCount = (long)_ecosystemThreatGridCellCount * _ecosystemThreatGridResolutionY;
            _ecosystemThreatVoxelCellCount = voxelCellCount > 0L && voxelCellCount <= int.MaxValue
                ? (int)voxelCellCount
                : 0;
        }

        private void InitializeCanopyGridMetadata()
        {
            int resolution = (int)math.round((canopyGridRadius * 2f) / math.max(1f, canopyGridCellSize)) + 1;
            if ((resolution & 1) == 0)
                resolution++;

            _canopyGridResolution = math.max(3, resolution);
            _canopyGridCellCount = _canopyGridResolution * _canopyGridResolution;
        }

        private bool EnsureThreatGridBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (!HasValidThreatGridConfiguration())
                return false;

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridHandle,
                    BufferID.VegetationEcosystemThreatGrid,
                    _ecosystemThreatGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.EcosystemThreatGridHandle,
                        BufferID.VegetationEcosystemThreatGrid,
                        _ecosystemThreatGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }

                _threatGridInitialized = false;
            }

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatGridCompressedHandle,
                    BufferID.VegetationEcosystemThreatGridCompressed,
                    _ecosystemThreatGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.EcosystemThreatGridCompressedHandle,
                        BufferID.VegetationEcosystemThreatGridCompressed,
                        _ecosystemThreatGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }
            }

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatVoxelHandle,
                    BufferID.VegetationEcosystemThreatVoxel,
                    _ecosystemThreatVoxelCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.EcosystemThreatVoxelHandle,
                        BufferID.VegetationEcosystemThreatVoxel,
                        _ecosystemThreatVoxelCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }
            }

            if (!TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatEchoHandle,
                    BufferID.VegetationEcosystemThreatEcho,
                    _ecosystemThreatGridCellCount,
                    out _))
            {
                if (!EnsureVegetationMemoryBufferReleased(
                        ref _nativeMemory.EcosystemThreatEchoHandle,
                        BufferID.VegetationEcosystemThreatEcho,
                        _ecosystemThreatGridCellCount,
                        NativeArrayOptions.ClearMemory))
                {
                    return false;
                }
            }

            EnsureThreatPropagationCommitCaches();
            return true;
        }

        private void EnsureThreatPropagationCommitCaches()
        {
            EnsureFloatCapacity(ref _threatPropagationCommitThreat, _ecosystemThreatGridCellCount);
            EnsureByteCapacity(ref _threatPropagationCommitCompressed, _ecosystemThreatGridCellCount);
            EnsureByteCapacity(ref _threatPropagationCommitEcho, _ecosystemThreatGridCellCount);
            EnsureByteCapacity(ref _threatPropagationCommitVoxel, _ecosystemThreatVoxelCellCount);
        }

        private bool HasValidThreatGridConfiguration()
        {
            return _ecosystemThreatGridResolution > 0 &&
                   _ecosystemThreatGridResolutionY > 0 &&
                   _ecosystemThreatGridCellCount > 0 &&
                   _ecosystemThreatVoxelCellCount > 0 &&
                   threatGridCellSize > 0f &&
                   thermalGridVerticalCellSize > 0f &&
                   math.isfinite(threatGridCellSize) &&
                   math.isfinite(thermalGridVerticalCellSize);
        }

        private bool TryAcquireCanopyGridBuffer(out IDataVault vault, out NativeArray<float> canopyGrid)
        {
            vault = null;
            canopyGrid = default;
            if (_canopyGridCellCount <= 0)
                InitializeCanopyGridMetadata();

            if (_canopyGridCellCount <= 0)
                return false;

            bool acquired = TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.CanopyHeightGridHandle,
                BufferID.VegetationCanopyHeightGrid,
                _canopyGridCellCount,
                NativeArrayOptions.ClearMemory,
                out vault,
                out canopyGrid);

            if (acquired)
                return true;

            if (_nativeMemory.CanopyHeightGridHandle.BufferID == 0u)
                _canopyGridInitialized = false;

            return false;
        }

        private void PrepareThreatSamplingSnapshot()
        {
            _threatSamplingChunkCount = 0;
            if (_densityQueryChunkCount <= 0 ||
                !TryReadDensityThreatAttractorSnapshot(
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            _threatSamplingChunkCount = _densityQueryChunkCount;
        }

        private void RebuildArtificialStructureThreatSnapshot()
        {
            Vector3 targetCenter = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition)
                ? playerRuntimePosition
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);

            int targetCount = _persistentArtificialStructureCount + _megaWreckStreamCount;
            if (targetCount <= 0)
            {
                _artificialStructureCount = 0;
                ReleaseVegetationMemoryBuffer(ref _nativeMemory.ArtificialStructureRecordsHandle);

                return;
            }

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.ArtificialStructureRecordsHandle,
                    BufferID.VegetationArtificialStructureRecords,
                    targetCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault recordsVault,
                    out NativeArray<ArtificialStructureRecord> artificialStructureRecords))
            {
                _artificialStructureCount = 0;
                return;
            }

            try
            {
                int writeIndex = 0;
                for (int i = 0; i < _persistentArtificialStructureCount; i++)
                {
                    PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                    WriteArtificialStructureRecord(ref artificialStructureRecords, structure.Bounds, structure.Type, writeIndex);
                    writeIndex++;
                }

                for (int i = 0; i < _megaWreckStreamCount; i++)
                {
                    WriteArtificialStructureRecord(
                        ref artificialStructureRecords,
                        GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]),
                        StructureType.MegaWreck,
                        writeIndex);
                    writeIndex++;
                }

                _artificialStructureCount = writeIndex;
            }
            finally
            {
                recordsVault.ReleaseWriteLock(
                    in _nativeMemory.ArtificialStructureRecordsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void WriteArtificialStructureRecord(
            ref NativeArray<ArtificialStructureRecord> destination,
            Bounds bounds,
            StructureType type,
            int writeIndex)
        {
            ArtificialStructureRecord record = new ArtificialStructureRecord
            {
                MinX = bounds.min.x,
                MinY = bounds.min.y,
                MinZ = bounds.min.z,
                MaxX = bounds.max.x,
                MaxY = bounds.max.y,
                MaxZ = bounds.max.z,
                Type = (byte)type
            };
            destination[writeIndex] = record;
        }

        private bool TryReadArtificialStructureSnapshot(out NativeArray<ArtificialStructureRecord> records)
        {
            records = default;
            if (_artificialStructureCount <= 0)
                return true;

            return TryReadVegetationMemoryBuffer(
                in _nativeMemory.ArtificialStructureRecordsHandle,
                BufferID.VegetationArtificialStructureRecords,
                _artificialStructureCount,
                out records);
        }

        private void RefreshArtificialStructureSnapshotIfIdle()
        {
            if (!CanRefreshThreatSpatialSnapshots())
                return;

            RebuildArtificialStructureThreatSnapshot();
        }

        private static Bounds GetMegaWreckSectionBounds(MegaWreckStreamSection section)
        {
            return new Bounds(section.WorldCenter, section.WorldSize);
        }

        private ChunkMegaWreckPayload BuildChunkMegaWreckPayload(ChunkPayload payload)
        {
            ChunkMegaWreckPayload wreckPayload = default;
            if (megaWreckDefinitions == null || megaWreckDefinitions.Length == 0)
                return wreckPayload;

            Bounds chunkBounds = payload.WorldBounds;
            int sectionCount = 0;
            for (int i = 0; i < megaWreckDefinitions.Length; i++)
            {
                MegaWreckDefinition definition = megaWreckDefinitions[i];
                if (definition.Prefab == null || definition.Size.x <= 0f || definition.Size.z <= 0f)
                    continue;

                Bounds wreckBounds = new Bounds(definition.Center, definition.Size);
                if (wreckBounds.Intersects(chunkBounds))
                    sectionCount++;
            }

            if (sectionCount <= 0)
                return wreckPayload;

            MegaWreckStreamSection[] sections = AllocateMegaWreckSectionPayloadArray(sectionCount);
            int writeIndex = 0;
            for (int i = 0; i < megaWreckDefinitions.Length; i++)
            {
                MegaWreckDefinition definition = megaWreckDefinitions[i];
                if (definition.Prefab == null || definition.Size.x <= 0f || definition.Size.z <= 0f)
                    continue;

                Bounds wreckBounds = new Bounds(definition.Center, definition.Size);
                if (!wreckBounds.Intersects(chunkBounds))
                    continue;

                float minX = math.max(payload.MinX, wreckBounds.min.x);
                float maxX = math.min(payload.MaxX, wreckBounds.max.x);
                float minZ = math.max(payload.MinZ, wreckBounds.min.z);
                float maxZ = math.min(payload.MaxZ, wreckBounds.max.z);
                if (maxX <= minX || maxZ <= minZ)
                    continue;

                Vector3 worldCenter = new Vector3((minX + maxX) * 0.5f, wreckBounds.center.y, (minZ + maxZ) * 0.5f);
                Vector3 worldSize = new Vector3(maxX - minX, wreckBounds.size.y, maxZ - minZ);
                Vector3 localCenter = worldCenter - wreckBounds.center;
                Vector3 localSize = worldSize;
                int sectionX = (int)math.floor((worldCenter.x - wreckBounds.min.x) / DefaultVirtualChunkSize);
                int sectionZ = (int)math.floor((worldCenter.z - wreckBounds.min.z) / DefaultVirtualChunkSize);
                int sectionSeed = ComputeMegaWreckSectionSeed(definition.Seed, sectionX, sectionZ);
                sections[writeIndex] = new MegaWreckStreamSection
                {
                    WreckId = definition.WreckId,
                    SectionSeed = sectionSeed,
                    SectionX = sectionX,
                    SectionZ = sectionZ,
                    WorldCenter = worldCenter,
                    WorldSize = worldSize,
                    LocalCenter = localCenter,
                    LocalSize = localSize
                };
                writeIndex++;
            }

            if (writeIndex <= 0)
                return default;

            wreckPayload.Sections = sections;
            wreckPayload.Count = writeIndex;
            return wreckPayload;
        }

        private int FindMegaWreckInteriorWreckId(Vector3 position)
        {
            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                Bounds bounds = new Bounds(section.WorldCenter, section.WorldSize);
                if (bounds.Contains(position))
                    return section.WreckId;
            }

            return int.MinValue;
        }

        private int CountMegaWreckSections(int wreckId)
        {
            int count = 0;
            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                if (_megaWreckStreamSnapshot[i].WreckId == wreckId)
                    count++;
            }

            return count;
        }

        private int ComputeMegaWreckInteriorMaskHash(int wreckId)
        {
            unchecked
            {
                int hash = (wreckId * 486187739) ^ _megaWreckStreamCount;
                for (int i = 0; i < _megaWreckStreamCount; i++)
                {
                    MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                    if (section.WreckId != wreckId)
                        continue;

                    hash = (hash * 16777619) ^ section.SectionSeed;
                    hash = (hash * 16777619) ^ section.SectionX;
                    hash = (hash * 16777619) ^ section.SectionZ;
                }

                return hash;
            }
        }

        private void CacheChunkMegaWreckPayload(ChunkKey key, ChunkMegaWreckPayload payload)
        {
            if (payload.Count <= 0 || payload.Sections == null)
            {
                RemoveChunkMegaWreckPayload(key);
                return;
            }

            SetChunkMegaWreckPayload(key, payload);
        }

        private void RemoveChunkMegaWreckPayload(ChunkKey key)
        {
            int index = FindChunkMegaWreckPayloadIndex(key);
            if (index < 0)
                return;

            RemoveChunkMegaWreckPayloadAt(index);
        }

        private int FindChunkMegaWreckPayloadIndex(ChunkKey key)
        {
            for (int i = 0; i < _chunkMegaWreckPayloadCount; i++)
            {
                if (_chunkMegaWreckPayloadKeys[i].Equals(key))
                    return i;
            }

            return -1;
        }

        private bool TryGetChunkMegaWreckPayload(ChunkKey key, out ChunkMegaWreckPayload payload)
        {
            int index = FindChunkMegaWreckPayloadIndex(key);
            if (index >= 0)
            {
                payload = _chunkMegaWreckPayloads[index];
                return true;
            }

            payload = default;
            return false;
        }

        private void SetChunkMegaWreckPayload(ChunkKey key, ChunkMegaWreckPayload payload)
        {
            int index = FindChunkMegaWreckPayloadIndex(key);
            if (index >= 0)
            {
                _chunkMegaWreckPayloads[index] = payload;
                return;
            }

            if (_chunkMegaWreckPayloadCount >= _chunkMegaWreckPayloads.Length)
            {
                RecordChunkQueueCapacityExceeded(_chunkMegaWreckPayloads.Length, _chunkMegaWreckPayloadCount);
                payload.Sections = null;
                payload.Count = 0;
                return;
            }

            _chunkMegaWreckPayloadKeys[_chunkMegaWreckPayloadCount] = key;
            _chunkMegaWreckPayloads[_chunkMegaWreckPayloadCount] = payload;
            _chunkMegaWreckPayloadCount++;
        }

        private void RemoveChunkMegaWreckPayloadAt(int index)
        {
            if ((uint)index >= (uint)_chunkMegaWreckPayloadCount)
                return;

            int last = _chunkMegaWreckPayloadCount - 1;
            if (index != last)
            {
                _chunkMegaWreckPayloadKeys[index] = _chunkMegaWreckPayloadKeys[last];
                _chunkMegaWreckPayloads[index] = _chunkMegaWreckPayloads[last];
            }

            _chunkMegaWreckPayloadKeys[last] = default;
            _chunkMegaWreckPayloads[last] = default;
            _chunkMegaWreckPayloadCount = last;
        }

        private void ClearChunkMegaWreckPayloads()
        {
            for (int i = 0; i < _chunkMegaWreckPayloadCount; i++)
            {
                _chunkMegaWreckPayloadKeys[i] = default;
                _chunkMegaWreckPayloads[i] = default;
            }

            _chunkMegaWreckPayloadCount = 0;
        }

        private bool SetChunkPayload(ChunkKey key, ChunkPayload payload)
        {
            if (_chunkPayloads.Set(key, payload))
                return true;

            RecordChunkQueueCapacityExceeded(_chunkPayloads.Capacity, _chunkPayloads.Count);
            return false;
        }

        /// Applies a world-space origin offset to cached bounds and metadata only.
        /// Vegetation instance matrices stay in local chunk space and are shifted on GPU.
        /// </summary>
        public void ApplyWorldOffsetToAllChunks(Vector3 offset)
        {
            TryApplyWorldOffsetToAllChunks(offset, _totalUniverseOffsetDouble - global::Hecton8.World.AUPMath.ToDouble3(offset), refreshResidency: true);
        }

        private bool TryApplyWorldOffsetToAllChunks(Vector3 offset, double3 newTotalUniverseOffsetDouble, bool refreshResidency)
        {
            float offsetSqrMagnitude = offset.sqrMagnitude;
            if (!IsFiniteVector(offset) ||
                !math.isfinite(offsetSqrMagnitude) ||
                !math.all(math.isfinite(newTotalUniverseOffsetDouble)))
            {
                ClearPendingWorldOffset();
                return false;
            }

            if (offsetSqrMagnitude <= 0.000001f)
                return true;

            if (HasAsyncWorldJobsInFlight())
            {
                QueuePendingWorldOffset(offset, newTotalUniverseOffsetDouble);
                return false;
            }

            ApplyWorldOffsetToAllChunksImmediate(offset, newTotalUniverseOffsetDouble, refreshResidency);
            return true;
        }

        private void QueuePendingWorldOffset(Vector3 offset, double3 newTotalUniverseOffsetDouble)
        {
            Vector3 accumulatedOffset = _hasPendingWorldOffset ? _pendingWorldOffset + offset : offset;
            if (!IsFiniteVector(accumulatedOffset) ||
                !math.all(math.isfinite(newTotalUniverseOffsetDouble)))
            {
                ClearPendingWorldOffset();
                return;
            }

            _pendingWorldOffset = accumulatedOffset;
            _pendingWorldOffsetDouble = newTotalUniverseOffsetDouble;
            _hasPendingWorldOffset = true;
        }

        private void TryApplyPendingWorldOffset()
        {
            if (!_hasPendingWorldOffset || HasAsyncWorldJobsInFlight())
                return;

            Vector3 pendingOffset = _pendingWorldOffset;
            double3 pendingTotalOffset = _pendingWorldOffsetDouble;
            _pendingWorldOffset = default;
            _pendingWorldOffsetDouble = default;
            _hasPendingWorldOffset = false;
            if (!IsFiniteVector(pendingOffset) ||
                pendingOffset.sqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(pendingTotalOffset)))
            {
                return;
            }

            ApplyWorldOffsetToAllChunksImmediate(pendingOffset, pendingTotalOffset, refreshResidency: false);
        }

        private void ClearPendingWorldOffset()
        {
            _pendingWorldOffset = default;
            _pendingWorldOffsetDouble = default;
            _hasPendingWorldOffset = false;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private bool HasAsyncWorldJobsInFlight()
        {
            if (_threatPropagationScheduled ||
                _flowFieldScheduled ||
                _abyssalThermalGridScheduled ||
                _abyssalPathScheduled ||
                _hlodCullScheduled ||
                _poolDefragScheduled)
            {
                return true;
            }

            return HasActiveChunkBuildJobs();
        }

        private bool HasActiveChunkBuildJobs()
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (_chunkBuildJobs[i].Active)
                    return true;
            }

            return false;
        }

        private void ApplyWorldOffsetToAllChunksImmediate(Vector3 offset, double3 newTotalUniverseOffsetDouble, bool refreshResidency)
        {
            float offsetSqrMagnitude = offset.sqrMagnitude;
            if (!IsFiniteVector(offset) ||
                !math.isfinite(offsetSqrMagnitude) ||
                !math.all(math.isfinite(newTotalUniverseOffsetDouble)) ||
                offsetSqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 appliedOffset = -offset;
            _totalUniverseOffsetDouble = newTotalUniverseOffsetDouble;
            _totalUniverseOffset = ToVector3(_totalUniverseOffsetDouble);
            GlobalTotalUniverseOffset = _totalUniverseOffset;
            GlobalTotalUniverseOffsetDouble = _totalUniverseOffsetDouble;

            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                if (!TryAddEvictionScratch(payloadEnumerator.Current.Key))
                    break;
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                ShiftChunkPayloadBounds(ref payload, appliedOffset);
                SetChunkPayload(key, payload);
            }

            FixedTileStateMap.Enumerator tileEnumerator = _tileStates.GetEnumerator();
            while (tileEnumerator.MoveNext())
            {
                TileRuntimeState state = tileEnumerator.Current.Value;
                if (state == null)
                    continue;

                state.TerrainPosition += appliedOffset;
            }

            if (_hasLastPlayerPosition)
                _lastPlayerPosition += appliedOffset;

            if (_terrainHoleCount > 0)
            {
                for (int i = 0; i < _terrainHoleCount; i++)
                {
                    TerrainHoleRecord hole = _terrainHoleRecords[i];
                    hole.Y += appliedOffset.y;
                    hole.X += appliedOffset.x;
                    hole.Z += appliedOffset.z;
                    _terrainHoleRecords[i] = hole;
                }

                SyncTerrainHoleNativeCache();
            }

            if (megaWreckDefinitions != null && megaWreckDefinitions.Length > 0)
            {
                for (int i = 0; i < megaWreckDefinitions.Length; i++)
                {
                    MegaWreckDefinition definition = megaWreckDefinitions[i];
                    definition.Center += appliedOffset;
                    megaWreckDefinitions[i] = definition;
                }
            }

            if (_persistentArtificialStructureCount > 0)
            {
                for (int i = 0; i < _persistentArtificialStructureCount; i++)
                {
                    PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                    Bounds bounds = structure.Bounds;
                    bounds.center += appliedOffset;
                    structure.Bounds = bounds;
                    _persistentArtificialStructures[i] = structure;
                }

            }

            if (_surfaceDrawBounds.size.sqrMagnitude > 0f)
                _surfaceDrawBounds.center += appliedOffset;
            if (_underwaterDrawBounds.size.sqrMagnitude > 0f)
                _underwaterDrawBounds.center += appliedOffset;

            ShiftChunkAbyssalNavPayloads(appliedOffset);
            ShiftChunkMegaWreckPayloads(appliedOffset);
            ShiftAbyssalNavSnapshots(appliedOffset);
            ShiftHLODRegistrySnapshots(appliedOffset);
            ShiftAbyssalPathSnapshot(appliedOffset);
            ShiftMegaWreckSnapshot(appliedOffset);
            if (_activeArtificialInteriorState.IsActive != 0)
            {
                Bounds shiftedBounds = _activeArtificialInteriorState.Bounds;
                shiftedBounds.center += appliedOffset;
                SetArtificialInteriorState(_activeArtificialInteriorState.Type, _activeArtificialInteriorState.StructureId, shiftedBounds);
            }
            if (_threatGridInitialized)
                _ecosystemThreatGridCenter += appliedOffset;
            _scheduledThreatGridCenter += appliedOffset;
            if (_flowFieldInitialized)
                _ecosystemFlowFieldCenter += appliedOffset;
            _scheduledFlowFieldCenter += appliedOffset;
            if (_canopyGridInitialized)
                _canopyGridCenter += appliedOffset;
            if (_abyssalThermalGridInitialized)
                _abyssalThermalGridCenter += appliedOffset;
            _scheduledAbyssalThermalGridCenter += appliedOffset;
            _currentThreatHotspotPosition += appliedOffset;
            ShiftPredatorFearNodes(appliedOffset);

            _activeSetDirty = true;
            if (refreshResidency)
            {
                RefreshResidency();
            }
            else
            {
                _activeBufferRebuildRequested = true;
            }
        }

        private void ShiftPredatorFearNodes(Vector3 appliedOffset)
        {
            if (_predatorFearNodeCount <= 0)
                return;

            float3 offset = new float3(appliedOffset.x, appliedOffset.y, appliedOffset.z);
            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                node.Position += offset;
                _predatorFearNodes[i] = node;
            }

            if (!_abyssalPathScheduled)
                SyncPredatorFearNodeSnapshot(_predatorFearSimulationTime);
        }

        void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!isActiveAndEnabled || !snapshot.IsValid)
                return;

            if (IsForeignTile(in snapshot))
                return;

            UpsertTileState(in snapshot);
            _activeSetDirty = true;
            RefreshResidency();
        }

        void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileMoved(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!isActiveAndEnabled || !snapshot.IsValid)
                return;

            if (IsForeignTile(in snapshot))
                return;

            RemoveTileState(snapshot.TileX, snapshot.TileZ);
            RefreshResidency();
        }

        private void LogNativePoolFragmentationIfDue()
        {
#if UNITY_EDITOR
            if (Time.unscaledTime < _nextNativePoolFragmentationLogTime)
                return;

            _nextNativePoolFragmentationLogTime = Time.unscaledTime + 30f;
            Hecton8.Core.H8Debug.Log(
                "[HectonMapMagicVegetationBridge] Native pool fragmentation telemetry sampled.",
                this);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogLoopGuardHit(string loopName, int maxIterations)
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("[HectonMapMagicVegetationBridge] Loop guard hit.");
#endif
        }

        private bool TryResolveVegetationTypeFromCachedMasks(Vector3 positionWS, out HectonVegetationInstanceType type)
        {
            type = HectonVegetationInstanceType.Grass;
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
                return false;

            if (!TryFindTileStateAtPosition(positionWS, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out NativeArray<ushort> heightSamples))
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
                    0,
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
                        math.max(0.2f, technoJungleFlowAnisotropy * 0.7f),
                        out float deadZoneOccupancy))
                {
                    float keepChance = math.saturate(deadZoneDensityScale * math.max(deadZoneStructureChance, deadZoneOccupancy));
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
            int sampleX = (int)math.round(worldX * DensityQuerySeedScale);
            int sampleZ = (int)math.round(worldZ * DensityQuerySeedScale);
            return BuildSampleSeed(tileX, tileZ, sampleX, sampleZ);
        }

        private static uint BuildArbitraryWorldSeed(float worldX, float worldY, float worldZ)
        {
            int sampleX = (int)math.round(worldX * DensityQuerySeedScale);
            int sampleY = (int)math.round(worldY * 0.25f);
            int sampleZ = (int)math.round(worldZ * DensityQuerySeedScale);
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

        private Matrix4x4 ApplyVegetationRuntimeOffset(Matrix4x4 matrix)
        {
            return ApplyMatrixTranslationOffsetDouble(matrix, _totalUniverseOffsetDouble);
        }

        private static Matrix4x4 ApplyMatrixTranslationOffsetDouble(Matrix4x4 matrix, double3 offset)
        {
            double3 translated = new double3(matrix.m03, matrix.m13, matrix.m23) + offset;
            matrix.m03 = ClampDoubleToRuntimeFloat(translated.x);
            matrix.m13 = ClampDoubleToRuntimeFloat(translated.y);
            matrix.m23 = ClampDoubleToRuntimeFloat(translated.z);
            return matrix;
        }

        private static Matrix4x4 ConvertMatrixToStableUniverseSpace(Matrix4x4 matrix, double3 universeOffset)
        {
            return ApplyMatrixTranslationOffsetDouble(matrix, -universeOffset);
        }

        private Vector3 ResolveRuntimePosition(Matrix4x4 matrix)
        {
            double3 runtimePosition = new double3(matrix.m03, matrix.m13, matrix.m23) + _totalUniverseOffsetDouble;
            return ToVector3(runtimePosition);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3(
                ClampDoubleToRuntimeFloat(value.x),
                ClampDoubleToRuntimeFloat(value.y),
                ClampDoubleToRuntimeFloat(value.z));
        }

        private static float ClampDoubleToRuntimeFloat(double value)
        {
            if (!math.isfinite(value))
                return 0f;

            return (float)math.clamp(value, -MaxRuntimeFloatCoordinate, MaxRuntimeFloatCoordinate);
        }

        private bool EnsureChunkBuildRecordBanks()
        {
            if ((UnsafeUtility.SizeOf<JobInstanceRecord>() & 7) != 0)
                return false;

            int grassCapacity = ResolveChunkBuildRecordCapacity(1f);
            int sparseCapacity = ResolveChunkBuildRecordCapacity(5f);
            if (AreChunkBuildRecordBanksReady(
                    _chunkBuildGrassRecordBanks,
                    _chunkBuildFloatingRecordBanks,
                    _chunkBuildKelpRecordBanks,
                    grassCapacity,
                    sparseCapacity,
                    sparseCapacity))
            {
                return true;
            }

            DisposeChunkBuildRecordBanks();
            _chunkBuildGrassRecordBanks = new NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs];
            _chunkBuildFloatingRecordBanks = new NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs];
            _chunkBuildKelpRecordBanks = new NativeArray<JobInstanceRecord>[MaxConcurrentChunkBuildJobs];
            _chunkBuildGrassRecordCapacity = grassCapacity;
            _chunkBuildFloatingRecordCapacity = sparseCapacity;
            _chunkBuildKelpRecordCapacity = sparseCapacity;

            for (int i = 0; i < MaxConcurrentChunkBuildJobs; i++)
            {
                if (!TryAllocateChunkBuildRecordBank(ref _chunkBuildGrassRecordBanks[i], grassCapacity) ||
                    !TryAllocateChunkBuildRecordBank(ref _chunkBuildFloatingRecordBanks[i], sparseCapacity) ||
                    !TryAllocateChunkBuildRecordBank(ref _chunkBuildKelpRecordBanks[i], sparseCapacity))
                {
                    DisposeChunkBuildRecordBanks();
                    return false;
                }
            }

            return true;
        }

        private static int ResolveChunkBuildRecordCapacity(float minimumStepMeters)
        {
            int axisCount = math.max(1, (int)math.ceil(DefaultVirtualChunkSize / math.max(0.01f, minimumStepMeters)));
            long capacity = (long)axisCount * axisCount;
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        private static bool AreChunkBuildRecordBanksReady(
            NativeArray<JobInstanceRecord>[] grassBanks,
            NativeArray<JobInstanceRecord>[] floatingBanks,
            NativeArray<JobInstanceRecord>[] kelpBanks,
            int grassCapacity,
            int floatingCapacity,
            int kelpCapacity)
        {
            if (grassBanks == null ||
                floatingBanks == null ||
                kelpBanks == null ||
                grassBanks.Length < MaxConcurrentChunkBuildJobs ||
                floatingBanks.Length < MaxConcurrentChunkBuildJobs ||
                kelpBanks.Length < MaxConcurrentChunkBuildJobs)
            {
                return false;
            }

            for (int i = 0; i < MaxConcurrentChunkBuildJobs; i++)
            {
                if (!grassBanks[i].IsCreated ||
                    grassBanks[i].Length < grassCapacity ||
                    !floatingBanks[i].IsCreated ||
                    floatingBanks[i].Length < floatingCapacity ||
                    !kelpBanks[i].IsCreated ||
                    kelpBanks[i].Length < kelpCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryAllocateChunkBuildRecordBank(
            ref NativeArray<JobInstanceRecord> records,
            int capacity)
        {
            try
            {
                records = H8Memory.Allocate<JobInstanceRecord>(
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!records.IsCreated || records.Length < capacity)
                {
                    DisposeChunkBuildRecordBank(ref records);
                    return false;
                }

                return true;
            }
            catch
            {
                DisposeChunkBuildRecordBank(ref records);
                throw;
            }
        }

        private bool TryAcquireChunkBuildRecordArrays(
            int slot,
            int grassCount,
            int floatingCount,
            int kelpCount,
            out NativeArray<JobInstanceRecord> grassRecords,
            out NativeArray<JobInstanceRecord> floatingRecords,
            out NativeArray<JobInstanceRecord> kelpRecords)
        {
            grassRecords = default;
            floatingRecords = default;
            kelpRecords = default;
            if ((uint)slot >= (uint)MaxConcurrentChunkBuildJobs ||
                grassCount < 0 ||
                floatingCount < 0 ||
                kelpCount < 0 ||
                _chunkBuildGrassRecordCapacity <= 0 ||
                _chunkBuildFloatingRecordCapacity <= 0 ||
                _chunkBuildKelpRecordCapacity <= 0 ||
                !AreChunkBuildRecordBanksReady(
                    _chunkBuildGrassRecordBanks,
                    _chunkBuildFloatingRecordBanks,
                    _chunkBuildKelpRecordBanks,
                    _chunkBuildGrassRecordCapacity,
                    _chunkBuildFloatingRecordCapacity,
                    _chunkBuildKelpRecordCapacity) ||
                grassCount > _chunkBuildGrassRecordCapacity ||
                floatingCount > _chunkBuildFloatingRecordCapacity ||
                kelpCount > _chunkBuildKelpRecordCapacity)
            {
                return false;
            }

            grassRecords = grassCount > 0 ? _chunkBuildGrassRecordBanks[slot].GetSubArray(0, grassCount) : default;
            floatingRecords = floatingCount > 0 ? _chunkBuildFloatingRecordBanks[slot].GetSubArray(0, floatingCount) : default;
            kelpRecords = kelpCount > 0 ? _chunkBuildKelpRecordBanks[slot].GetSubArray(0, kelpCount) : default;
            return true;
        }

        private void DisposeChunkBuildRecordBanks()
        {
            DisposeChunkBuildRecordBanks(ref _chunkBuildGrassRecordBanks);
            DisposeChunkBuildRecordBanks(ref _chunkBuildFloatingRecordBanks);
            DisposeChunkBuildRecordBanks(ref _chunkBuildKelpRecordBanks);
            _chunkBuildGrassRecordCapacity = 0;
            _chunkBuildFloatingRecordCapacity = 0;
            _chunkBuildKelpRecordCapacity = 0;
        }

        private static void DisposeChunkBuildRecordBanks(ref NativeArray<JobInstanceRecord>[] banks)
        {
            if (banks == null)
            {
                banks = Array.Empty<NativeArray<JobInstanceRecord>>();
                return;
            }

            for (int i = 0; i < banks.Length; i++)
                DisposeChunkBuildRecordBank(ref banks[i]);

            banks = Array.Empty<NativeArray<JobInstanceRecord>>();
        }

        private static void DisposeChunkBuildRecordBank(ref NativeArray<JobInstanceRecord> records)
        {
            H8Memory.Release(ref records, VegetationMemorySovereigntyConstants.OwnerSystemId);
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
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (!_chunkBuildJobs[i].Active || !_chunkBuildJobs[i].JobState.Key.Equals(key))
                    continue;

                MarkChunkBuildJobCancelled(i);
                return;
            }
        }

        private bool HasChunkBuildJobsForTile(int tileX, int tileZ)
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (!_chunkBuildJobs[i].Active)
                    continue;

                ChunkKey key = _chunkBuildJobs[i].JobState.Key;
                if (key.TileX == tileX && key.TileZ == tileZ)
                    return true;
            }

            return false;
        }

        private void CompleteAndReleaseChunkBuildJobsForTile(int tileX, int tileZ)
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (!_chunkBuildJobs[i].Active)
                    continue;

                ChunkKey key = _chunkBuildJobs[i].JobState.Key;
                if (key.TileX == tileX && key.TileZ == tileZ)
                    MarkChunkBuildJobCancelled(i);
            }
        }

        private void CancelAllChunkBuildJobs()
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (_chunkBuildJobs[i].Active)
                    MarkChunkBuildJobCancelled(i);
            }
        }

        private void MarkChunkBuildJobCancelled(int slot)
        {
            if ((uint)slot >= (uint)_chunkBuildJobs.Length || !_chunkBuildJobs[slot].Active)
                return;

            ChunkBuildPendingJob pending = _chunkBuildJobs[slot];
            pending.Cancelled = true;
            _chunkBuildJobs[slot] = pending;
        }

        private void DisposeAllChunkBuildJobs()
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (_chunkBuildJobs[i].Active)
                    CompleteAndReleaseChunkBuildJob(i);
            }
        }

        private void CompleteAndReleaseChunkBuildJob(int slot)
        {
            ChunkBuildPendingJob pending = _chunkBuildJobs[slot];
            DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: true);
            ReleaseChunkBuildPendingJob(ref pending);
            _chunkBuildJobs[slot] = default;
        }

        private static void ReleaseChunkBuildPendingJob(ref ChunkBuildPendingJob pending)
        {
            pending.SandMaskSnapshot = default;
            pending.RockMaskSnapshot = default;
            pending.HeightSamplesSnapshot = default;
            pending.GrassRecords = default;
            pending.FloatingRecords = default;
            pending.KelpRecords = default;
            ReleaseChunkBuildReadPins(
                pending.ReadPinVault,
                pending.ReadPinMask,
                pending.TileSandMaskBufferId,
                pending.TileRockMaskBufferId,
                pending.TileHeightSamplesBufferId);
            pending.TileSandMaskBufferId = 0;
            pending.TileRockMaskBufferId = 0;
            pending.TileHeightSamplesBufferId = 0;
            pending.ReadPinVault = null;
            pending.ReadPinMask = 0u;
            pending.Handle = default;
            pending.Active = false;
        }

        private static void ReleaseChunkBuildReadPins(
            IDataVault vault,
            uint pinMask,
            BufferID tileSandMaskBufferId = 0,
            BufferID tileRockMaskBufferId = 0,
            BufferID tileHeightSamplesBufferId = 0)
        {
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinArtificialStructures, BufferID.VegetationArtificialStructureRecords);
            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinThreatEcho, BufferID.VegetationEcosystemThreatEcho);
            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinTerrainHoles, BufferID.VegetationTerrainHoleRecords);
            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinTileSandMask, tileSandMaskBufferId);
            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinTileRockMask, tileRockMaskBufferId);
            TryUnlockChunkBuildReadPin(vault, pinMask, ChunkBuildPinTileHeightSamples, tileHeightSamplesBufferId);
        }

        private static void TryUnlockChunkBuildReadPin(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u && bufferId != 0u)
                vault.TryUnlockBuffer(bufferId, VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Exception firstException = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (Exception exception)
            {
                firstException = exception;
            }

            try
            {
                array.Dispose();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }
            finally
            {
                array = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static unsafe void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Exception firstException = null;

            if (dependency.IsCompleted)
            {
                DispatcherJobFence.TryComplete(ref dependency, forceComplete: true);
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }

                try
                {
                    array.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
            }
            else
            {
                JobHandle disposeHandle = array.Dispose(dependency);
                if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))
                    throw new InvalidOperationException("MapMagic vegetation native array disposal did not complete before sentinel unregister.");

                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            array = default;

            if (firstException != null)
                throw firstException;
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static JobHandle CombineOptionalHandles(JobHandle current, JobHandle next)
        {
            if (current.Equals(default))
                return next;

            if (next.Equals(default))
                return current;

            return JobHandle.CombineDependencies(current, next);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DefragPoolJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkSliceMoveRecord> Moves;
            public int MoveCount;
            [ReadOnly] public NativeArray<Matrix4x4> SourceMatrices;
            [ReadOnly] public NativeArray<HectonVegetationInstanceData> SourceMetadata;
            [ReadOnly] public NativeArray<int> SourceTypes;
            [ReadOnly] public NativeArray<int> SourceSemanticTypes;
            [ReadOnly] public NativeArray<byte> SourceBiomeLayers;
            [ReadOnly] public NativeArray<float> SourceEdgeDistances;
            [ReadOnly] public NativeArray<Vector2> SourceFlowDirections;
            [ReadOnly] public NativeArray<Vector3> SourceFlowVectors;
            public NativeArray<Matrix4x4> DestinationMatrices;
            public NativeArray<HectonVegetationInstanceData> DestinationMetadata;
            public NativeArray<int> DestinationTypes;
            public NativeArray<int> DestinationSemanticTypes;
            public NativeArray<byte> DestinationBiomeLayers;
            public NativeArray<float> DestinationEdgeDistances;
            public NativeArray<Vector2> DestinationFlowDirections;
            public NativeArray<Vector3> DestinationFlowVectors;

            public void Execute()
            {
                if (!Moves.IsCreated || MoveCount <= 0 || !SourceMatrices.IsCreated || !DestinationMatrices.IsCreated)
                    return;

                for (int moveIndex = 0; moveIndex < MoveCount; moveIndex++)
                {
                    ChunkSliceMoveRecord move = Moves[moveIndex];
                    int sourceEnd = move.SourceOffset + move.Count;
                    int destinationEnd = move.DestinationOffset + move.Count;
                    if (move.Count <= 0 ||
                        move.SourceOffset < 0 ||
                        move.DestinationOffset < 0 ||
                        sourceEnd > SourceMatrices.Length ||
                        destinationEnd > DestinationMatrices.Length)
                    {
                        continue;
                    }

                    for (int i = 0; i < move.Count; i++)
                    {
                        int sourceIndex = move.SourceOffset + i;
                        int destinationIndex = move.DestinationOffset + i;
                        DestinationMatrices[destinationIndex] = SourceMatrices[sourceIndex];
                        DestinationMetadata[destinationIndex] = SourceMetadata[sourceIndex];
                        DestinationTypes[destinationIndex] = SourceTypes[sourceIndex];
                        DestinationSemanticTypes[destinationIndex] = SourceSemanticTypes[sourceIndex];
                        DestinationBiomeLayers[destinationIndex] = SourceBiomeLayers[sourceIndex];
                        DestinationEdgeDistances[destinationIndex] = SourceEdgeDistances[sourceIndex];
                        DestinationFlowDirections[destinationIndex] = SourceFlowDirections[sourceIndex];
                        DestinationFlowVectors[destinationIndex] = SourceFlowVectors[sourceIndex];
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            int ignorePlacementMasks,
            NativeArray<byte> sandMask,
            NativeArray<byte> rockMask,
            NativeArray<ushort> heightSamples,
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

            float terrainSizeInvX = math.rcp(math.max(0.01f, terrainSize.x));
            float terrainSizeInvZ = math.rcp(math.max(0.01f, terrainSize.z));
            float normalizedX = math.saturate(localX * terrainSizeInvX);
            float normalizedZ = math.saturate(localZ * terrainSizeInvZ);
            int alphaX = math.clamp((int)math.floor(normalizedX * alphamapResolution), 0, alphamapResolution - 1);
            int alphaZ = math.clamp((int)math.floor(normalizedZ * alphamapResolution), 0, alphamapResolution - 1);
            int maskIndex = (alphaZ * alphamapResolution) + alphaX;
            if (maskIndex < 0 || maskIndex >= sandMask.Length)
                return false;

            if (ignorePlacementMasks == 0 && sandMask[maskIndex] <= sandMaskThreshold)
                return false;

            if (ignorePlacementMasks == 0 && rockMask.IsCreated && maskIndex < rockMask.Length && rockMask[maskIndex] > rockMaskThreshold)
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
                return ResolveOctantDirection((int)((seed ^ 0xB5297A4Du) & 7u));
            }

            return ResolveOctantDirectionFromVector(downhill.x, downhill.y);
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
            float2 normalizedFlow = ResolveOctantDirectionFromVector(flowDirection.x, flowDirection.y);
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
            float noiseStartDepth = math.max(colonyBiomeStartDepth, AbyssalFlowNoiseStartDepthMeters);
            if (depthBelowSurface <= noiseStartDepth)
                return baseFlow;

            float influence = math.saturate((depthBelowSurface - noiseStartDepth) / math.max(1f, noiseStartDepth));
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

        private bool IsInsideRegisteredTerrainHole(float worldX, float worldZ)
        {
            for (int i = 0; i < _terrainHoleCount; i++)
            {
                TerrainHoleRecord hole = _terrainHoleRecords[i];
                float dx = worldX - hole.X;
                float dz = worldZ - hole.Z;
                if ((dx * dx) + (dz * dz) <= hole.RadiusSq)
                    return true;
            }

            return false;
        }

        private bool TrySampleScatterSurfaceFromCachedTerrain(Vector3 position, out Vector3 point, out Vector3 normal)
        {
            point = position;
            normal = Vector3.up;
            if (!TryFindTileStateAtPosition(position, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) ||
                !heightSamples.IsCreated ||
                state.HeightmapResolution <= 1)
            {
                return false;
            }

            float localX = position.x - state.TerrainPosition.x;
            float localZ = position.z - state.TerrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > state.TerrainSize.x || localZ > state.TerrainSize.z)
                return false;

            float normalizedX = math.saturate(localX / math.max(0.01f, state.TerrainSize.x));
            float normalizedZ = math.saturate(localZ / math.max(0.01f, state.TerrainSize.z));
            float3 terrainSize = new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z);
            float terrainY = state.TerrainPosition.y + SampleHeight(normalizedX, normalizedZ, terrainSize, state.HeightmapResolution, heightSamples);
            float3 sampledNormal = SampleNormal(normalizedX, normalizedZ, terrainSize, state.HeightmapResolution, heightSamples);
            point = new Vector3(position.x, terrainY, position.z);
            normal = new Vector3(sampledNormal.x, sampledNormal.y, sampledNormal.z);
            return true;
        }

        private static bool IsScatterSurfaceNormalSpawnable(Vector3 normal)
        {
            float lengthSq = normal.sqrMagnitude;
            if (lengthSq <= 0.0001f)
                return false;

            return normal.y > 0f && (normal.y * normal.y) >= ScatterMinimumSurfaceNormalUpDotSq * lengthSq;
        }

        private static Vector3 ResolveScatterSurfaceUpCheat(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.0001f ? normal : Vector3.up;
        }

        private static int QuantizeYawDegreesToOctant(float yawDegrees)
        {
            float wrapped = yawDegrees - (math.floor(yawDegrees / 360f) * 360f);
            return (int)math.floor((wrapped + 22.5f) * 0.0222222228f) & 7;
        }

        private static Vector3 ResolveScatterSurfaceTangent(Vector3 surfaceUp, int yawSector)
        {
            float2 octant = ResolveOctantDirection(yawSector);
            Vector3 authoredForward = new Vector3(octant.x, 0f, octant.y);
            Vector3 tangent = authoredForward - (surfaceUp * Vector3.Dot(authoredForward, surfaceUp));
            if (tangent.sqrMagnitude > 0.000001f)
                return tangent;

            tangent = Vector3.Cross(surfaceUp, Vector3.right);
            if (tangent.sqrMagnitude > 0.000001f)
                return tangent;

            tangent = Vector3.Cross(surfaceUp, Vector3.forward);
            return tangent.sqrMagnitude > 0.000001f ? tangent : Vector3.forward;
        }

        private static bool IsInsideTerrainHoleStatic(float worldX, float worldZ, NativeArray<TerrainHoleRecord> holes, int holeCount)
        {
            if (!holes.IsCreated || holeCount <= 0)
                return false;

            int count = math.min(holeCount, holes.Length);
            for (int i = 0; i < count; i++)
            {
                TerrainHoleRecord hole = holes[i];
                float dx = worldX - hole.X;
                float dz = worldZ - hole.Z;
                if ((dx * dx) + (dz * dz) <= hole.RadiusSq)
                    return true;
            }

            return false;
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

            float nearestDistance = FastGradientMagnitude(math.max(0f, nearestDistanceSq));
            float secondNearestDistance = FastGradientMagnitude(math.max(nearestDistanceSq, secondNearestDistanceSq));
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
            float cellular = 1f - math.saturate(FastGradientMagnitude(math.lengthsq(delta)) * 1.15f);
            float broad = SampleValueNoise(worldX * 0.21f, worldZ * 0.21f, seed ^ 0x9E3779B9u);
            float detail = SampleValueNoise((worldX * 0.83f) + 19.37f, (worldZ * 0.83f) + 41.11f, seed ^ 0x68E31DA4u);
            return math.saturate((cellular * 0.5f) + (broad * 0.3f) + (detail * 0.2f));
        }

        private static float SampleHeight(
            float normalizedX,
            float normalizedZ,
            float3 terrainSize,
            int heightResolution,
            NativeArray<ushort> heights)
        {
            float sampleX = normalizedX * (heightResolution - 1);
            float sampleZ = normalizedZ * (heightResolution - 1);
            int x0 = math.clamp((int)math.floor(sampleX), 0, heightResolution - 1);
            int z0 = math.clamp((int)math.floor(sampleZ), 0, heightResolution - 1);
            int x1 = math.min(x0 + 1, heightResolution - 1);
            int z1 = math.min(z0 + 1, heightResolution - 1);
            float tx = sampleX - x0;
            float tz = sampleZ - z0;
            float heightScale = terrainSize.y * (1f / 65535f);
            float h00 = heights[(z0 * heightResolution) + x0] * heightScale;
            float h10 = heights[(z0 * heightResolution) + x1] * heightScale;
            float h01 = heights[(z1 * heightResolution) + x0] * heightScale;
            float h11 = heights[(z1 * heightResolution) + x1] * heightScale;
            float bottom = math.lerp(h00, h10, tx);
            float top = math.lerp(h01, h11, tx);
            return math.lerp(bottom, top, tz);
        }

        private static float3 SampleNormal(
            float normalizedX,
            float normalizedZ,
            float3 terrainSize,
            int heightResolution,
            NativeArray<ushort> heights)
        {
            float sampleX = normalizedX * (heightResolution - 1);
            float sampleZ = normalizedZ * (heightResolution - 1);
            int centerX = math.clamp((int)math.round(sampleX), 0, heightResolution - 1);
            int centerZ = math.clamp((int)math.round(sampleZ), 0, heightResolution - 1);
            int x0 = math.max(0, centerX - 1);
            int x1 = math.min(heightResolution - 1, centerX + 1);
            int z0 = math.max(0, centerZ - 1);
            int z1 = math.min(heightResolution - 1, centerZ + 1);
            float invHeightResolutionMinusOne = math.rcp(math.max(1f, heightResolution - 1));
            float dx = math.max(0.001f, (x1 - x0) * terrainSize.x * invHeightResolutionMinusOne);
            float dz = math.max(0.001f, (z1 - z0) * terrainSize.z * invHeightResolutionMinusOne);
            float heightScale = terrainSize.y * (1f / 65535f);
            float hLeft = heights[(centerZ * heightResolution) + x0] * heightScale;
            float hRight = heights[(centerZ * heightResolution) + x1] * heightScale;
            float hDown = heights[(z0 * heightResolution) + centerX] * heightScale;
            float hUp = heights[(z1 * heightResolution) + centerX] * heightScale;
            float3 tangentX = new float3(dx, hRight - hLeft, 0f);
            float3 tangentZ = new float3(0f, hUp - hDown, dz);
            return ResolveDominantFloat3(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
        }

        private static quaternion BuildAlignedRotation(float3 normal, int sector)
        {
            return ResolveOctantYawRotation(sector);
        }

        private static int ResolveOctantSector(float variation, uint seed, uint salt)
        {
            int baseSector = (int)(math.saturate(variation) * 7.999f);
            int jitterSector = (int)(Hash01(seed ^ salt) * 7.999f);
            return (baseSector + jitterSector) & 7;
        }

        private static float2 ResolveOctantDirection(int sector)
        {
            switch (sector & 7)
            {
                case 0:
                    return new float2(1f, 0f);
                case 1:
                    return new float2(0.70710677f, 0.70710677f);
                case 2:
                    return new float2(0f, 1f);
                case 3:
                    return new float2(-0.70710677f, 0.70710677f);
                case 4:
                    return new float2(-1f, 0f);
                case 5:
                    return new float2(-0.70710677f, -0.70710677f);
                case 6:
                    return new float2(0f, -1f);
                default:
                    return new float2(0.70710677f, -0.70710677f);
            }
        }

        private static float2 ResolveOctantDirectionFromVector(float x, float y)
        {
            float absX = math.abs(x);
            float absY = math.abs(y);
            if (absX <= 0.000001f && absY <= 0.000001f)
                return new float2(1f, 0f);

            float signX = x < 0f ? -1f : 1f;
            float signY = y < 0f ? -1f : 1f;
            float minor = math.min(absX, absY);
            float major = math.max(absX, absY);
            if (minor * 2f >= major)
                return new float2(signX * 0.70710677f, signY * 0.70710677f);

            return absX >= absY ? new float2(signX, 0f) : new float2(0f, signY);
        }

        private static quaternion ResolveOctantYawRotation(int sector)
        {
            switch (sector & 7)
            {
                case 0:
                    return new quaternion(0f, 0f, 0f, 1f);
                case 1:
                    return new quaternion(0f, 0.38268343f, 0f, 0.9238795f);
                case 2:
                    return new quaternion(0f, 0.70710677f, 0f, 0.70710677f);
                case 3:
                    return new quaternion(0f, 0.9238795f, 0f, 0.38268343f);
                case 4:
                    return new quaternion(0f, 1f, 0f, 0f);
                case 5:
                    return new quaternion(0f, 0.9238795f, 0f, -0.38268343f);
                case 6:
                    return new quaternion(0f, 0.70710677f, 0f, -0.70710677f);
                default:
                    return new quaternion(0f, 0.38268343f, 0f, -0.9238795f);
            }
        }

        private static float3 ResolveDominantFloat3(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            if (math.max(math.max(ax, ay), az) <= 0.000001f)
                return fallback;

            if (ay >= ax && ay >= az)
                return new float3(0f, value.y < 0f ? -1f : 1f, 0f);

            return ax >= az
                ? new float3(value.x < 0f ? -1f : 1f, 0f, 0f)
                : new float3(0f, 0f, value.z < 0f ? -1f : 1f);
        }

        private static void ShiftChunkPayloadBounds(ref ChunkPayload payload, Vector3 offset)
        {
            payload.MinX += offset.x;
            payload.MaxX += offset.x;
            payload.MinZ += offset.z;
            payload.MaxZ += offset.z;
            payload.WorldBounds.center += offset;
        }

        private static float BuildJitteredCoordinate(float min, float step, int index, float jitterFraction, uint seed)
        {
            return VegetationMath.BuildJitteredCoordinate(min, step, index, jitterFraction, seed);
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
            return VegetationMath.TryEvaluateFloatingLabyrinth(
                worldX,
                worldZ,
                seed,
                floatingPatchThreshold,
                floatingPatchNoiseScale,
                floatingCellSize,
                floatingSecondaryCellSize,
                floatingWallWidth,
                floatingWarpMeters,
                floatingFlowDirection,
                floatingFlowAnisotropy,
                out occupancy);
        }

        private static float2 SampleFloatingWarp(float2 world, float floatingPatchNoiseScale, float floatingWarpMeters)
        {
            return VegetationMath.SampleFloatingWarp(world, floatingPatchNoiseScale, floatingWarpMeters);
        }

        private static float EvaluateVoronoiEdgeDistance(float2 position, float cellSize, uint salt, out float variation)
        {
            return VegetationMath.EvaluateVoronoiEdgeDistance(position, cellSize, salt, out variation);
        }

        private static float SampleValueNoise(float x, float z, uint salt)
        {
            return VegetationMath.SampleValueNoise(x, z, salt);
        }

        private bool TryResolveLayerIndices(UnityEngine.TerrainData terrainData, out LayerIndices indices)
        {
            indices = default;
            ResetLayerIndices(ref indices);

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
                if (IsSandLikeTerrainLayerName(layerName))
                {
                    AssignNextSandLayerIndex(ref indices, i);
                    continue;
                }

                if (IsRockLikeTerrainLayerName(layerName))
                {
                    AssignNextRockLayerIndex(ref indices, i);
                }
            }

            return HasAnyTerrainLayerIndex(in indices);
        }

        private static void ResetLayerIndices(ref LayerIndices indices)
        {
            indices.Sand0 = -1;
            indices.Sand1 = -1;
            indices.Sand2 = -1;
            indices.Sand3 = -1;
            indices.Rock0 = -1;
            indices.Rock1 = -1;
            indices.Rock2 = -1;
            indices.Rock3 = -1;
            indices.Rock4 = -1;
            indices.Rock5 = -1;
        }

        private static bool HasAnyTerrainLayerIndex(in LayerIndices indices)
        {
            return indices.Sand0 >= 0 ||
                   indices.Sand1 >= 0 ||
                   indices.Sand2 >= 0 ||
                   indices.Sand3 >= 0 ||
                   indices.Rock0 >= 0 ||
                   indices.Rock1 >= 0 ||
                   indices.Rock2 >= 0 ||
                   indices.Rock3 >= 0 ||
                   indices.Rock4 >= 0 ||
                   indices.Rock5 >= 0;
        }

        private static void AssignNextSandLayerIndex(ref LayerIndices indices, int layerIndex)
        {
            if (indices.Sand0 < 0)
                indices.Sand0 = layerIndex;
            else if (indices.Sand1 < 0)
                indices.Sand1 = layerIndex;
            else if (indices.Sand2 < 0)
                indices.Sand2 = layerIndex;
            else if (indices.Sand3 < 0)
                indices.Sand3 = layerIndex;
        }

        private static void AssignNextRockLayerIndex(ref LayerIndices indices, int layerIndex)
        {
            if (indices.Rock0 < 0)
                indices.Rock0 = layerIndex;
            else if (indices.Rock1 < 0)
                indices.Rock1 = layerIndex;
            else if (indices.Rock2 < 0)
                indices.Rock2 = layerIndex;
            else if (indices.Rock3 < 0)
                indices.Rock3 = layerIndex;
            else if (indices.Rock4 < 0)
                indices.Rock4 = layerIndex;
            else if (indices.Rock5 < 0)
                indices.Rock5 = layerIndex;
        }

        private static bool IsSandLikeTerrainLayerName(string layerName)
        {
            return string.Equals(layerName, SandLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, GreenSandLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34ClaySiltLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34ShellSandLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34RootMatLayerName, StringComparison.Ordinal);
        }

        private static bool IsRockLikeTerrainLayerName(string layerName)
        {
            return string.Equals(layerName, RockLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34SerpentiniteLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34BrineSaltLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34ManganeseLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34MethaneHydrateLayerName, StringComparison.Ordinal) ||
                   string.Equals(layerName, Batch34LimestoneCaveLayerName, StringComparison.Ordinal);
        }

        private Camera RefreshActiveViewCameraCache()
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

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            Camera playerCamera = playerRuntimeContext != null ? playerRuntimeContext.PlayerCamera : null;
            if (playerCamera != null &&
                playerCamera.isActiveAndEnabled &&
                playerCamera.gameObject.activeInHierarchy)
            {
                _cachedViewCamera = playerCamera;
                viewCamera = playerCamera;
                _nextCameraResolveTime = float.NegativeInfinity;
                return _cachedViewCamera;
            }

            Camera localCamera = _cachedLocalCamera;
            if (localCamera != null &&
                localCamera.isActiveAndEnabled &&
                localCamera.gameObject.activeInHierarchy)
            {
                _cachedViewCamera = localCamera;
                viewCamera = localCamera;
                _nextCameraResolveTime = float.NegativeInfinity;
                return _cachedViewCamera;
            }

            if (Time.unscaledTime < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = Time.unscaledTime + CameraResolveRetryInterval;
            return null;
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
            float farRadius = math.max(grassHighDensityRadius + 1f, residentRadius);
            float farRadiusSqr = farRadius * farRadius;
            float distance01 = math.saturate((distanceSqr - nearRadiusSqr) / math.max(1f, farRadiusSqr - nearRadiusSqr));
            float smoothedDistance01 = distance01 * distance01 * (3f - (2f * distance01));
            float qualityWeight = ResolveGrassQualityWeight();
            float tierScale = math.lerp(1.25f, 0.65f, qualityWeight);
            int encodedTier = (int)math.round(math.saturate(smoothedDistance01 * tierScale) * byte.MaxValue);
            return (byte)math.clamp(encodedTier, 0, byte.MaxValue);
        }

        private float GetGrassStepForTier(byte grassLodTier)
        {
            float nearStep = math.max(0.05f, grassStepMeters);
            float farStep = math.max(nearStep, grassFarStepMeters);
            float qualityWeight = ResolveGrassQualityWeight();
            float tier01 = (grassLodTier * (1f / byte.MaxValue)) * math.lerp(1f, 0.55f, qualityWeight);
            return math.lerp(nearStep, farStep, math.saturate(tier01));
        }

        private static float ResolveGrassQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return float.IsFinite(quality) ? math.saturate(quality) : 0f;
        }

        private void UploadChannel(
            HectonIndirectVegetationRenderer renderer,
            ref GraphicsBuffer matrixBuffer,
            ref GraphicsBuffer dataBuffer,
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

            EnsureStructuredBuffer<Matrix4x4>(ref matrixBuffer, count);
            EnsureStructuredBuffer<HectonVegetationInstanceData>(ref dataBuffer, count);
            if (matrixBuffer == null || dataBuffer == null)
            {
                ClearChannel(renderer);
                return;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(matrixBuffer, matrices, count);
            GraphicsBufferUploadUtility.UploadNativeArray(dataBuffer, metadata, count);
            renderer.BindInstanceBuffer(matrixBuffer, count);
            renderer.BindInstanceDataBuffer(dataBuffer);
            renderer.SetDrawBounds(bounds);
        }

        private void EnsureStructuredBuffer<T>(ref GraphicsBuffer buffer, int count) where T : struct
        {
            if (count <= 0)
            {
                ReleaseBuffer(ref buffer);
                return;
            }

            if (buffer != null && buffer.count >= count)
                return;

            ReleaseBuffer(ref buffer);
            buffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>(count); // COLD ALLOC: GraphicsBuffer[count] - streamed vegetation structured payload - owner: HectonMapMagicVegetationBridge
        }

        private ChunkPayload CreateChunkPayloadHeader(TileRuntimeState state, int chunkX, int chunkZ)
        {
            GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float sizeX = math.max(0.01f, maxX - minX);
            float sizeZ = math.max(0.01f, maxZ - minZ);

            ChunkPayload payload = default;
            payload.MinX = minX;
            payload.MaxX = maxX;
            payload.MinZ = minZ;
            payload.MaxZ = maxZ;
            payload.WorldBounds = new Bounds(
                new Vector3(centerX, state.TerrainPosition.y + (state.TerrainSize.y * 0.5f), centerZ),
                new Vector3(sizeX, math.max(1f, state.TerrainSize.y), sizeZ));
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

            alphaStartX = math.clamp((int)math.floor((localMinX / state.TerrainSize.x) * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            alphaStartZ = math.clamp((int)math.floor((localMinZ / state.TerrainSize.z) * state.AlphamapResolution), 0, state.AlphamapResolution - 1);
            int alphaEndX = math.clamp((int)math.ceil((localMaxX / state.TerrainSize.x) * state.AlphamapResolution), alphaStartX + 1, state.AlphamapResolution);
            int alphaEndZ = math.clamp((int)math.ceil((localMaxZ / state.TerrainSize.z) * state.AlphamapResolution), alphaStartZ + 1, state.AlphamapResolution);
            alphaWidth = math.max(1, alphaEndX - alphaStartX);
            alphaHeight = math.max(1, alphaEndZ - alphaStartZ);
            return alphaWidth > 0 && alphaHeight > 0;
        }

        private static int GetChunkRangeStart(float worldMin, float terrainMin, int chunkCount)
        {
            int index = (int)math.floor((worldMin - terrainMin) / DefaultVirtualChunkSize);
            return math.clamp(index, 0, chunkCount - 1);
        }

        private static int GetChunkRangeEnd(float worldMax, float terrainMin, int chunkCount)
        {
            int index = (int)math.floor((worldMax - terrainMin) / DefaultVirtualChunkSize);
            return math.clamp(index, 0, chunkCount - 1);
        }

        private static void GetChunkBounds(TileRuntimeState state, int chunkX, int chunkZ, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float worldMinX = state.TerrainPosition.x + (chunkX * DefaultVirtualChunkSize);
            float worldMinZ = state.TerrainPosition.z + (chunkZ * DefaultVirtualChunkSize);
            float worldMaxX = math.min(worldMinX + DefaultVirtualChunkSize, state.TerrainPosition.x + state.TerrainSize.x);
            float worldMaxZ = math.min(worldMinZ + DefaultVirtualChunkSize, state.TerrainPosition.z + state.TerrainSize.z);
            minX = worldMinX;
            maxX = worldMaxX;
            minZ = worldMinZ;
            maxZ = worldMaxZ;
        }

        private void ClearEvictionScratch()
        {
            _evictionKeyCount = 0;
        }

        private bool TryAddEvictionScratch(ChunkKey key)
        {
            if (_evictionKeyCount >= _evictionKeys.Length)
            {
                RecordChunkQueueCapacityExceeded(_evictionKeys.Length, _evictionKeyCount);
                return false;
            }

            _evictionKeys[_evictionKeyCount++] = key;
            return true;
        }

        private void RecordChunkQueueCapacityExceeded(int capacity, int count)
        {
            RecordVegetationMemoryTelemetry(
                BufferID.Unknown,
                0,
                capacity,
                count,
                0,
                0f,
                VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                VegetationMemoryTelemetryPhase.SlowTick,
                VegetationMemorySovereigntyConstants.FlagCapacity,
                default);
        }

        private void InsertDesiredChunk(ChunkKey key, float distanceSqr)
        {
            int capacity = math.min(
                _desiredChunkKeys != null ? _desiredChunkKeys.Length : 0,
                _desiredChunkDistances != null ? _desiredChunkDistances.Length : 0);
            if (capacity <= 0)
            {
                RecordChunkQueueCapacityExceeded(capacity, _desiredChunkCount);
                return;
            }

            if (_desiredChunkCount > capacity)
                _desiredChunkCount = capacity;

            if (_desiredChunkCount >= capacity)
            {
                if (distanceSqr >= _desiredChunkDistances[capacity - 1])
                {
                    RecordChunkQueueCapacityExceeded(capacity, _desiredChunkCount);
                    return;
                }

                _desiredChunkCount = capacity - 1;
                _desiredChunkKeys[_desiredChunkCount] = default;
                _desiredChunkDistances[_desiredChunkCount] = float.PositiveInfinity;
                RecordChunkQueueCapacityExceeded(capacity, capacity);
            }

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
            int capacity = math.min(
                _pendingChunkKeys != null ? _pendingChunkKeys.Length : 0,
                _pendingChunkPriorities != null ? _pendingChunkPriorities.Length : 0);
            if (capacity <= 0)
            {
                RecordChunkQueueCapacityExceeded(capacity, _pendingChunkCount);
                return;
            }

            if (_pendingChunkCount > capacity)
                _pendingChunkCount = capacity;

            for (int i = 0; i < _pendingChunkCount; i++)
            {
                if (_pendingChunkKeys[i].Equals(key))
                {
                    if (priority >= _pendingChunkPriorities[i])
                        return;

                    DequeuePendingChunkAt(i);
                    break;
                }
            }

            if (_pendingChunkCount >= capacity)
            {
                if (priority >= _pendingChunkPriorities[capacity - 1])
                {
                    RecordChunkQueueCapacityExceeded(capacity, _pendingChunkCount);
                    return;
                }

                _pendingChunkCount = capacity - 1;
                _pendingChunkKeys[_pendingChunkCount] = default;
                _pendingChunkPriorities[_pendingChunkCount] = float.PositiveInfinity;
                RecordChunkQueueCapacityExceeded(capacity, capacity);
            }

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

            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                if (!ContainsDesiredChunk(key) && !TryAddEvictionScratch(key))
                    break;
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ReleaseChunkPayloadStorage(_evictionKeys[i]);
                _chunkPayloads.Remove(_evictionKeys[i]);
                RemoveChunkAbyssalNavPayload(_evictionKeys[i]);
                RemoveChunkMegaWreckPayload(_evictionKeys[i]);
                CancelChunkBuildJob(_evictionKeys[i]);
            }

        }

        private void EnforceChunkPoolMemoryGuard()
        {
            long guardBytes = math.max(MinimumNativePoolBudgetMb, nativePoolGuardMb) * 1024L * 1024L;
            if (_chunkPayloadUsedBytes <= guardBytes || _desiredChunkCount <= 0)
                return;

            bool evictedPayload = false;
            int evictionIterations = 0;
            while (_chunkPayloadUsedBytes > guardBytes &&
                   evictionIterations < MaxChunkPoolEvictionIterations &&
                   TryFindChunkPoolEvictionVictim(out int victimIndex, out ChunkKey victimKey))
            {
                evictionIterations++;
                bool hadPayload = _chunkPayloads.TryGetValue(victimKey, out ChunkPayload payload);
                if (hadPayload)
                {
                    ReleaseChunkPayloadStorage(payload);
                    _chunkPayloads.Remove(victimKey);
                    RemoveChunkAbyssalNavPayload(victimKey);
                    RemoveChunkMegaWreckPayload(victimKey);
                }

                CancelChunkBuildJob(victimKey);
                RemoveDesiredChunkAt(victimIndex);
                evictedPayload |= hadPayload;
            }

            if (_chunkPayloadUsedBytes > guardBytes && evictionIterations >= MaxChunkPoolEvictionIterations)
                LogLoopGuardHit(nameof(EnforceChunkPoolMemoryGuard), MaxChunkPoolEvictionIterations);

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

            Vector2 playerPositionXZ = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition)
                ? new Vector2(playerRuntimePosition.x, playerRuntimePosition.z)
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
            float planarVelocitySq = planarVelocity.sqrMagnitude;
            if (planarVelocitySq >= predictiveMinSpeed * predictiveMinSpeed)
            {
                float2 octant = ResolveOctantDirectionFromVector(planarVelocity.x, planarVelocity.y);
                return new Vector2(octant.x, octant.y);
            }

            return Vector2.right;
        }

        private static float EvaluateChunkEvictionScore(Vector2 playerPositionXZ, Vector2 forward, ChunkPayload payload)
        {
            float centerX = (payload.MinX + payload.MaxX) * 0.5f;
            float centerZ = (payload.MinZ + payload.MaxZ) * 0.5f;
            Vector2 toChunk = new Vector2(centerX - playerPositionXZ.x, centerZ - playerPositionXZ.y);
            float distanceSqr = toChunk.sqrMagnitude;
            float longitudinal = Vector2.Dot(toChunk, forward);
            float behindMeters = math.max(0f, -longitudinal);
            float aheadMeters = math.max(0f, longitudinal);
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
            float clampedX = math.clamp(x, minX, maxX);
            float clampedZ = math.clamp(z, minZ, maxZ);
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
            float lateral = math.abs(Vector2.Dot(toChunk, right));

            bool insideResidency;
            if (longitudinal >= 0f)
            {
                float normalizedForward = longitudinal / math.max(0.01f, forwardRadius);
                float normalizedLateral = lateral / math.max(0.01f, lateralRadius);
                insideResidency = (normalizedForward * normalizedForward) + (normalizedLateral * normalizedLateral) <= 1f;
            }
            else
            {
                float normalizedRear = longitudinal / math.max(0.01f, rearRadius);
                float normalizedLateral = lateral / math.max(0.01f, lateralRadius);
                insideResidency = (normalizedRear * normalizedRear) + (normalizedLateral * normalizedLateral) <= 1f;
            }

            if (!insideResidency)
                return false;

            priority -= math.max(0f, longitudinal) * forwardPriorityBoost;
            priority += math.max(0f, -longitudinal) * rearPriorityPenalty;
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
            return VegetationMath.BuildJitteredCoordinate(min, step, index, jitterFraction, seed);
        }

        private static Vector2 NormalizeScaleRange(Vector2 range)
        {
            float min = math.max(0.01f, math.min(range.x, range.y));
            float max = math.max(min, math.max(range.x, range.y));
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

        private static BufferID ResolveTileNativeCacheBufferId(int slotIndex, int bufferIndex, int laneOffset)
        {
            int normalizedBufferOffset = bufferIndex == 0
                ? TileNativeCachePrimaryOffset
                : TileNativeCacheSecondaryOffset;
            int bufferId = (int)BufferID.VegetationTileNativeCacheDynamicBase +
                           (slotIndex * TileNativeCacheBufferStride) +
                           normalizedBufferOffset +
                           laneOffset;
            return unchecked((BufferID)bufferId);
        }

        private static BufferID ResolveTileTerrainHoleMaskBufferId(int slotIndex)
        {
            int bufferId = (int)BufferID.VegetationTileNativeCacheDynamicBase +
                           (slotIndex * TileNativeCacheBufferStride) +
                           TileNativeTerrainHoleMaskOffset;
            return unchecked((BufferID)bufferId);
        }

        private bool EnsureTileNativeCacheSlot(TileRuntimeState state, long tileKey)
        {
            if (state == null)
                return false;

            int existingSlot = state.TileNativeCacheSlot;
            if ((uint)existingSlot < (uint)TileNativeCacheSlotCapacity &&
                _tileNativeCacheSlotUsed[existingSlot] &&
                _tileNativeCacheSlotKeys[existingSlot] == tileKey)
            {
                return true;
            }

            for (int i = 0; i < TileNativeCacheSlotCapacity; i++)
            {
                if (_tileNativeCacheSlotUsed[i])
                    continue;

                _tileNativeCacheSlotUsed[i] = true;
                _tileNativeCacheSlotKeys[i] = tileKey;
                state.TileNativeCacheSlot = i;
                return true;
            }

            RecordVegetationMemoryTelemetry(
                BufferID.VegetationTileNativeCacheDynamicBase,
                0u,
                TileNativeCacheSlotCapacity,
                _tileStates.Count,
                0,
                0f,
                VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                VegetationMemoryTelemetryPhase.SlowTick,
                VegetationMemorySovereigntyConstants.FlagCapacity,
                default);
            return false;
        }

        private void ReleaseTileNativeCacheSlot(TileRuntimeState state)
        {
            if (state == null)
                return;

            int slot = state.TileNativeCacheSlot;
            if ((uint)slot < (uint)TileNativeCacheSlotCapacity)
            {
                _tileNativeCacheSlotUsed[slot] = false;
                _tileNativeCacheSlotKeys[slot] = 0L;
            }

            state.TileNativeCacheSlot = -1;
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
                    RemoveChunkAbyssalNavPayload(key);
                    RemoveChunkMegaWreckPayload(key);
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
                ClearCorruptionStateForTile(tileX, tileZ);
                state.PendingRemoval = true;

                if (state.HeightReadbackPending && !TryFinalizeTileHeightReadback(state))
                    return;

                FinalizeDeferredTileRemoval(tileKey);
                return;
            }

            ClearCorruptionStateForTile(tileX, tileZ);
            _tileStates.Remove(tileKey);
            _activeSetDirty = true;
        }

        private int CopyActiveInstances(
            NativeArray<Matrix4x4> sourceMatrices,
            NativeArray<int> sourceTypes,
            int count,
            ref Matrix4x4[] matrices,
            ref int[] types)
        {
            if (count <= 0 || !sourceMatrices.IsCreated || !sourceTypes.IsCreated)
                return 0;

            if (matrices == null || types == null || matrices.Length < count || types.Length < count)
                return 0;

            for (int i = 0; i < count; i++)
                matrices[i] = ApplyVegetationRuntimeOffset(sourceMatrices[i]);
            CopyNativeToManaged(sourceTypes, 0, types, 0, count);
            return count;
        }

        private int CopyActivePayload(
            NativeArray<Matrix4x4> sourceMatrices,
            NativeArray<HectonVegetationInstanceData> sourceMetadata,
            NativeArray<int> sourceTypes,
            int count,
            ref Matrix4x4[] matrices,
            ref HectonVegetationInstanceData[] metadata,
            ref int[] types)
        {
            if (count <= 0 || !sourceMatrices.IsCreated || !sourceMetadata.IsCreated || !sourceTypes.IsCreated)
                return 0;

            if (matrices == null ||
                metadata == null ||
                types == null ||
                matrices.Length < count ||
                metadata.Length < count ||
                types.Length < count)
            {
                return 0;
            }

            for (int i = 0; i < count; i++)
                matrices[i] = ApplyVegetationRuntimeOffset(sourceMatrices[i]);
            CopyNativeToManaged(sourceMetadata, 0, metadata, 0, count);
            CopyNativeToManaged(sourceTypes, 0, types, 0, count);
            return count;
        }

        private bool EnsureActiveAggregateBufferCapacity(
            ref ActiveAggregateNativeBufferSet buffers,
            BufferID matrixBufferId,
            BufferID matrixDirtyPageBufferId,
            BufferID metadataDirtyPageBufferId,
            int requiredCount)
        {
            if (requiredCount <= 0)
                return false;

            int requiredDirtyPageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                requiredCount,
                ActiveAggregateDirtyPageSize);
            bool ready =
                EnsureAggregateBuffer(ref buffers.MatricesHandle, matrixBufferId, requiredCount) &&
                EnsureAggregateBuffer(ref buffers.MetadataHandle, NextBufferID(matrixBufferId, 1), requiredCount) &&
                EnsureAggregateBuffer(ref buffers.TypesHandle, NextBufferID(matrixBufferId, 2), requiredCount) &&
                EnsureAggregateBuffer(ref buffers.SemanticTypesHandle, NextBufferID(matrixBufferId, 3), requiredCount) &&
                EnsureAggregateBuffer(ref buffers.BiomeLayersHandle, NextBufferID(matrixBufferId, 4), requiredCount) &&
                EnsureAggregateBuffer(ref buffers.FlowDirectionsHandle, NextBufferID(matrixBufferId, 5), requiredCount) &&
                EnsureAggregateBuffer(ref buffers.FlowVectorsHandle, NextBufferID(matrixBufferId, 6), requiredCount) &&
                EnsureAggregateDirtyPageBuffer(ref buffers.MatrixDirtyPagesHandle, matrixDirtyPageBufferId, requiredDirtyPageCount) &&
                EnsureAggregateDirtyPageBuffer(ref buffers.MetadataDirtyPagesHandle, metadataDirtyPageBufferId, requiredDirtyPageCount);
            if (ready)
            {
                buffers.Capacity = math.max(buffers.Capacity, requiredCount);
                buffers.DirtyPageCapacity = math.max(buffers.DirtyPageCapacity, requiredDirtyPageCount);
            }
            return ready;
        }

        private bool EnsureAggregateDirtyPageBuffer(
            ref VaultGenerationHandle<byte> handle,
            BufferID initialBufferId,
            int requiredCount)
        {
            if (requiredCount <= 0)
                return false;

            BufferID bufferId = handle.BufferID != 0u
                ? unchecked((BufferID)(int)handle.BufferID)
                : initialBufferId;
            if (handle.BufferID != 0u &&
                TryReadOnlyVegetationMemoryBuffer(
                    in handle,
                    bufferId,
                    requiredCount,
                    out NativeArray<byte>.ReadOnly _))
            {
                return true;
            }

            return EnsureVegetationMemoryBufferReleased(
                ref handle,
                bufferId,
                requiredCount,
                NativeArrayOptions.ClearMemory);
        }

        private bool EnsureAggregateBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID initialBufferId,
            int requiredCount)
            where T : struct
        {
            BufferID bufferId = handle.BufferID != 0u
                ? unchecked((BufferID)(int)handle.BufferID)
                : initialBufferId;
            if (handle.BufferID != 0u &&
                TryReadOnlyVegetationMemoryBuffer(
                    in handle,
                    bufferId,
                    requiredCount,
                    out NativeArray<T>.ReadOnly _))
            {
                return true;
            }

            return EnsureVegetationMemoryBufferReleased(
                ref handle,
                bufferId,
                requiredCount,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool EnsureVegetationMemoryBufferReleased<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCount,
            NativeArrayOptions options)
            where T : struct
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref handle,
                    bufferId,
                    requiredCount,
                    options,
                    out IDataVault vault,
                    out NativeArray<T> _))
            {
                return false;
            }

            try
            {
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in handle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool EnsureChunkPoolCapacity(
            ref NativeChunkPool pool,
            BufferID matrixBufferId,
            int requiredCount)
        {
            if (requiredCount <= 0)
                return false;

            bool ready =
                EnsureAggregateBuffer(ref pool.MatricesHandle, matrixBufferId, requiredCount) &&
                EnsureAggregateBuffer(ref pool.MetadataHandle, NextBufferID(matrixBufferId, 1), requiredCount) &&
                EnsureAggregateBuffer(ref pool.TypesHandle, NextBufferID(matrixBufferId, 2), requiredCount) &&
                EnsureAggregateBuffer(ref pool.SemanticTypesHandle, NextBufferID(matrixBufferId, 3), requiredCount) &&
                EnsureAggregateBuffer(ref pool.BiomeLayersHandle, NextBufferID(matrixBufferId, 4), requiredCount) &&
                EnsureAggregateBuffer(ref pool.EdgeDistancesHandle, NextBufferID(matrixBufferId, 5), requiredCount) &&
                EnsureAggregateBuffer(ref pool.FlowDirectionsHandle, NextBufferID(matrixBufferId, 6), requiredCount) &&
                EnsureAggregateBuffer(ref pool.FlowVectorsHandle, NextBufferID(matrixBufferId, 7), requiredCount);
            if (ready)
                pool.Capacity = requiredCount;
            return ready;
        }

        private bool TryReadChunkPoolView(in NativeChunkPool pool, int requiredCount, out NativeChunkPoolView view)
        {
            view = default;
            if (requiredCount <= 0 ||
                pool.Capacity < requiredCount ||
                !TryReadAggregateBuffer(in pool.MatricesHandle, requiredCount, out NativeArray<Matrix4x4> matrices) ||
                !TryReadAggregateBuffer(in pool.MetadataHandle, requiredCount, out NativeArray<HectonVegetationInstanceData> metadata) ||
                !TryReadAggregateBuffer(in pool.TypesHandle, requiredCount, out NativeArray<int> types) ||
                !TryReadAggregateBuffer(in pool.SemanticTypesHandle, requiredCount, out NativeArray<int> semanticTypes) ||
                !TryReadAggregateBuffer(in pool.BiomeLayersHandle, requiredCount, out NativeArray<byte> biomeLayers) ||
                !TryReadAggregateBuffer(in pool.EdgeDistancesHandle, requiredCount, out NativeArray<float> edgeDistances) ||
                !TryReadAggregateBuffer(in pool.FlowDirectionsHandle, requiredCount, out NativeArray<Vector2> flowDirections) ||
                !TryReadAggregateBuffer(in pool.FlowVectorsHandle, requiredCount, out NativeArray<Vector3> flowVectors))
            {
                return false;
            }

            view = new NativeChunkPoolView(
                matrices,
                metadata,
                types,
                semanticTypes,
                biomeLayers,
                edgeDistances,
                flowDirections,
                flowVectors,
                pool.Capacity);
            return true;
        }

        private bool TryReadAggregateBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return requiredCount > 0 &&
                   handle.BufferID != 0u &&
                   TryReadVegetationMemoryBuffer(
                       in handle,
                       unchecked((BufferID)(int)handle.BufferID),
                       requiredCount,
                       out buffer);
        }

        private bool TryAcquireAggregateWriteBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            int requiredCount,
            out IDataVault vault,
            out NativeArray<T> buffer)
            where T : struct
        {
            vault = null;
            buffer = default;
            return requiredCount > 0 &&
                   handle.BufferID != 0u &&
                   TryAcquireVegetationMemoryBuffer(
                       ref handle,
                       unchecked((BufferID)(int)handle.BufferID),
                       requiredCount,
                       NativeArrayOptions.UninitializedMemory,
                       out vault,
                       out buffer);
        }

        private bool TryReadAggregateBufferReadOnly<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return requiredCount > 0 &&
                   handle.BufferID != 0u &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in handle,
                       unchecked((BufferID)(int)handle.BufferID),
                       requiredCount,
                       out buffer);
        }

        private NativeArray<T>.ReadOnly ReadAggregateBufferReadOnly<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount)
            where T : struct
        {
            return TryReadAggregateBufferReadOnly(in handle, requiredCount, out NativeArray<T>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private static BufferID ResolveSurfaceAggregateMatrixBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? BufferID.VegetationSurfaceAggregateFrontMatrices
                : BufferID.VegetationSurfaceAggregateBackMatrices;
        }

        private static BufferID ResolveUnderwaterAggregateMatrixBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? BufferID.VegetationUnderwaterAggregateFrontMatrices
                : BufferID.VegetationUnderwaterAggregateBackMatrices;
        }

        private static BufferID ResolveSurfaceAggregateMatrixDirtyPageBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? SurfaceAggregateFrontMatrixDirtyPagesId
                : SurfaceAggregateBackMatrixDirtyPagesId;
        }

        private static BufferID ResolveSurfaceAggregateMetadataDirtyPageBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? SurfaceAggregateFrontMetadataDirtyPagesId
                : SurfaceAggregateBackMetadataDirtyPagesId;
        }

        private static BufferID ResolveUnderwaterAggregateMatrixDirtyPageBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? UnderwaterAggregateFrontMatrixDirtyPagesId
                : UnderwaterAggregateBackMatrixDirtyPagesId;
        }

        private static BufferID ResolveUnderwaterAggregateMetadataDirtyPageBufferId(int bufferIndex)
        {
            return (bufferIndex & 1) == 0
                ? UnderwaterAggregateFrontMetadataDirtyPagesId
                : UnderwaterAggregateBackMetadataDirtyPagesId;
        }

        private static BufferID NextBufferID(BufferID baseId, int offset)
        {
            return unchecked((BufferID)((int)baseId + offset));
        }

        private static void SwapActiveAggregateBuffers(ref ActiveAggregateNativeBufferSet front, ref ActiveAggregateNativeBufferSet back)
        {
            ActiveAggregateNativeBufferSet previousFront = front;
            front = back;
            back = previousFront;
        }

        private bool TryPrepareRendererWriteBuffer(ref JobHandle readerHandle)
        {
            if (readerHandle.Equals(default))
                return true;

            if (!readerHandle.IsCompleted)
                return false;

            if (!_insideLateFrameJobSwap)
                return false;

            DispatcherJobSwap.TryComplete(ref readerHandle, forceComplete: false);
            return true;
        }

        private static void SwapAggregateReadState(
            ref int frontCount,
            ref int backCount,
            ref Bounds frontBounds,
            ref Bounds backBounds,
            ref bool hasFrontBounds,
            ref bool hasBackBounds,
            ref JobHandle frontReaderHandle,
            ref JobHandle backReaderHandle,
            ref int frontBufferIndex,
            ref int backBufferIndex)
        {
            (frontCount, backCount) = (backCount, frontCount);
            (frontBounds, backBounds) = (backBounds, frontBounds);
            (hasFrontBounds, hasBackBounds) = (hasBackBounds, hasFrontBounds);
            (frontReaderHandle, backReaderHandle) = (backReaderHandle, frontReaderHandle);
            (frontBufferIndex, backBufferIndex) = (backBufferIndex, frontBufferIndex);
        }

        private void BindRendererSources()
        {
            if (surfaceRenderer != null && _surfaceNativeBufferSource != null)
                surfaceRenderer.BindSource(_surfaceNativeBufferSource);

            if (underwaterRenderer != null && _underwaterNativeBufferSource != null)
                underwaterRenderer.BindSource(_underwaterNativeBufferSource);
        }

        private bool TryAcquireSurfaceNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            return TryAcquireNativeReadBuffer(
                _surfaceAggregateFrontBuffers,
                _surfaceFrontCount,
                _surfaceFrontBufferIndex,
                _surfaceFrontDrawBounds,
                _hasSurfaceFrontBounds,
                _surfaceActiveAggregateRevision,
                default,
                out readBuffer);
        }

        private bool TryAcquireUnderwaterNativeReadBuffer(out HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            return TryAcquireNativeReadBuffer(
                _underwaterAggregateFrontBuffers,
                _underwaterFrontCount,
                _underwaterFrontBufferIndex,
                _underwaterFrontDrawBounds,
                _hasUnderwaterFrontBounds,
                _underwaterActiveAggregateRevision,
                default,
                out readBuffer);
        }

        private bool TryAcquireNativeReadBuffer(
            ActiveAggregateNativeBufferSet buffers,
            int count,
            int bufferIndex,
            Bounds drawBounds,
            bool hasExplicitBounds,
            int contentRevision,
            JobHandle producerHandle,
            out HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            if (count <= 0 ||
                !TryReadAggregateBuffer(in buffers.MatricesHandle, count, out NativeArray<Matrix4x4> matrices) ||
                !TryReadAggregateBuffer(in buffers.MetadataHandle, count, out NativeArray<HectonVegetationInstanceData> metadata) ||
                matrices.Length < count ||
                metadata.Length < count)
            {
                readBuffer = default;
                return false;
            }

            NativeArray<byte> matrixDirtyPages = default;
            NativeArray<byte> metadataDirtyPages = default;
            int dirtyPageSize = 0;
            int dirtyPageCount = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                count,
                ActiveAggregateDirtyPageSize);
            if (dirtyPageCount > 0 &&
                TryReadAggregateBuffer(in buffers.MatrixDirtyPagesHandle, dirtyPageCount, out matrixDirtyPages) &&
                TryReadAggregateBuffer(in buffers.MetadataDirtyPagesHandle, dirtyPageCount, out metadataDirtyPages))
            {
                dirtyPageSize = ActiveAggregateDirtyPageSize;
            }

            readBuffer = new HectonIndirectVegetationNativeReadBuffer(
                matrices,
                metadata,
                count,
                bufferIndex,
                producerHandle,
                hasExplicitBounds,
                drawBounds,
                contentRevision,
                matrixDirtyPages,
                metadataDirtyPages,
                dirtyPageSize);
            return true;
        }

        private void ReleaseSurfaceNativeReadBuffer(in HectonIndirectVegetationNativeReadBuffer readBuffer, JobHandle readerHandle)
        {
            ReleaseNativeReadBuffer(
                readBuffer,
                readerHandle,
                _surfaceFrontBufferIndex,
                ref _surfaceFrontReaderHandle,
                _surfaceBackBufferIndex,
                ref _surfaceBackReaderHandle);
        }

        private void ReleaseUnderwaterNativeReadBuffer(in HectonIndirectVegetationNativeReadBuffer readBuffer, JobHandle readerHandle)
        {
            ReleaseNativeReadBuffer(
                readBuffer,
                readerHandle,
                _underwaterFrontBufferIndex,
                ref _underwaterFrontReaderHandle,
                _underwaterBackBufferIndex,
                ref _underwaterBackReaderHandle);
        }

        private static void ReleaseNativeReadBuffer(
            in HectonIndirectVegetationNativeReadBuffer readBuffer,
            JobHandle readerHandle,
            int frontBufferIndex,
            ref JobHandle frontReaderHandle,
            int backBufferIndex,
            ref JobHandle backReaderHandle)
        {
            if (readBuffer.BufferIndex == frontBufferIndex)
            {
                frontReaderHandle = JobHandle.CombineDependencies(frontReaderHandle, readerHandle);
                return;
            }

            if (readBuffer.BufferIndex == backBufferIndex)
                backReaderHandle = JobHandle.CombineDependencies(backReaderHandle, readerHandle);
        }

        private void DisposeActiveAggregateBufferSet(ref ActiveAggregateNativeBufferSet buffers)
        {
            ReleaseVegetationMemoryBuffer(ref buffers.MatricesHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.MetadataHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.TypesHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.SemanticTypesHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.BiomeLayersHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.FlowDirectionsHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.FlowVectorsHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.MatrixDirtyPagesHandle);
            ReleaseVegetationMemoryBuffer(ref buffers.MetadataDirtyPagesHandle);
            buffers.Dispose();
        }

        private bool TryBootstrapExistingTiles()
        {
            if (_startupBootstrapTileCount > 0 || _startupBootstrapTileCursor > 0)
                return true;

            if (mapMagicBridge == null)
                return false;

            _startupBootstrapTileCount = mapMagicBridge.CopyTerrainTileSnapshotsTo(_startupBootstrapTiles);
            _startupBootstrapTileCount = math.clamp(_startupBootstrapTileCount, 0, _startupBootstrapTiles.Length);

            _startupBootstrapTileCursor = 0;
            return true;
        }

        private void QueueDeferredStartupWork()
        {
            _startupTerrainHoleSyncPending = true;
            _startupTileBootstrapPending = true;
            _startupResidencyPending = true;
            ReleaseDeferredStartupTileSnapshot();
        }

        private void ResetDeferredStartupWork()
        {
            _startupTerrainHoleSyncPending = true;
            _startupTileBootstrapPending = true;
            _startupResidencyPending = true;
            ReleaseDeferredStartupTileSnapshot();
        }

        private void QueueDeferredTileCacheDisposal()
        {
            _deferredTileCacheDisposalRequested = true;
        }

        private bool QueueDeferredStartupProgressIfPending()
        {
            bool hasPendingStartupWork = _startupTerrainHoleSyncPending ||
                                         _startupTileBootstrapPending ||
                                         _startupResidencyPending;
            if (!hasPendingStartupWork)
                return false;

            _deferredStartupProgressRequested = true;
            return true;
        }

        private bool QueueResidentTileCacheValidation()
        {
            bool dueForValidation = _tileStates.Count > 0 && Time.unscaledTime >= _nextCacheValidationTime;
            if (!dueForValidation && !HasPendingTileHeightReadbackOrRemoval())
                return false;

            _residentTileCacheValidationRequested = true;
            return true;
        }

        private bool HasPendingTileHeightReadbackOrRemoval()
        {
            if (_tileStates.Count <= 0)
                return false;

            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                if (state.PendingRemoval ||
                    state.HeightReadbackPending ||
                    state.HeightReadbackRepairRequested ||
                    state.HeightReadbackDisposalDeferred ||
                    state.TileCacheDisposalDeferred)
                    return true;
            }

            return false;
        }

        private bool TryProgressDeferredStartupWork()
        {
            bool hasPendingStartupWork = _startupTerrainHoleSyncPending ||
                                         _startupTileBootstrapPending ||
                                         _startupResidencyPending;
            if (!hasPendingStartupWork)
                return false;

            if (Application.isPlaying &&
                BootstrapState.HasActiveInstance &&
                !BootstrapState.IsGameReady)
            {
                return true;
            }

            if (_startupTerrainHoleSyncPending)
            {
                SyncTerrainHoleNativeCache();
                _startupTerrainHoleSyncPending = false;
                return true;
            }

            if (_startupTileBootstrapPending)
            {
                if (!TryBootstrapExistingTiles())
                    return true;

                if (_startupBootstrapTileCount <= 0)
                {
                    _startupTileBootstrapPending = false;
                    ReleaseDeferredStartupTileSnapshot();
                    return true;
                }

                int processedCount = ProcessDeferredStartupTileBatch(StartupBootstrapTileBatchSize);
                if (processedCount > 0)
                    _startupResidencyPending = true;

                if (_startupBootstrapTileCursor >= _startupBootstrapTileCount)
                {
                    _startupTileBootstrapPending = false;
                    ReleaseDeferredStartupTileSnapshot();
                }

                return true;
            }

            if (_startupResidencyPending)
            {
                RefreshResidency();
                _startupResidencyPending = false;
                return true;
            }

            return false;
        }

        private int ProcessDeferredStartupTileBatch(int batchSize)
        {
            if (_startupBootstrapTileCount <= 0)
                return 0;

            int safeBatchSize = math.max(1, batchSize);
            int processedCount = 0;
            while (_startupBootstrapTileCursor < _startupBootstrapTileCount &&
                   processedCount < safeBatchSize)
            {
                MapMagicTerrainTileSnapshot snapshot = _startupBootstrapTiles[_startupBootstrapTileCursor++];
                if (!snapshot.IsValid || IsForeignTile(in snapshot))
                    continue;

                UpsertTileStateDeferredStartup(in snapshot);
                processedCount++;
            }

            return processedCount;
        }

        private void ReleaseDeferredStartupTileSnapshot()
        {
            _startupBootstrapTileCount = 0;
            _startupBootstrapTileCursor = 0;
        }

        private void UpsertTileState(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!TryPrepareTileState(
                    in snapshot,
                    out TileRuntimeState state,
                    out UnityEngine.TerrainData terrainData,
                    out long tileKey,
                    out int oldChunkCountX,
                    out int oldChunkCountZ,
                    out bool hadExistingTileState))
            {
                return;
            }

            RefreshTerrainTextureCachesCold(state, terrainData);
            EnsureTileTerrainHoleMaskCapacityCold(state);
            EnsureTileHeightReadbackData(state, terrainData.heightmapResolution * terrainData.heightmapResolution);
            FinalizeTileStateUpsert(in snapshot, state, terrainData, oldChunkCountX, oldChunkCountZ);
        }

        private void UpsertTileStateDeferredStartup(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!TryPrepareTileState(
                    in snapshot,
                    out TileRuntimeState state,
                    out UnityEngine.TerrainData terrainData,
                    out long tileKey,
                    out int oldChunkCountX,
                    out int oldChunkCountZ,
                    out bool hadExistingTileState))
            {
                return;
            }

            if (!TryRefreshTerrainTextureCachesHot(state, terrainData))
            {
                if (!hadExistingTileState)
                    _tileStates.Remove(tileKey);
                return;
            }

            if (!HasTileHeightReadbackData(state, terrainData.heightmapResolution * terrainData.heightmapResolution))
            {
                QueueTileHeightReadbackRepair(state, terrainData.heightmapResolution * terrainData.heightmapResolution);
                if (!hadExistingTileState)
                    _tileStates.Remove(tileKey);
                return;
            }

            TryPrepareTileTerrainHoleMaskHot(state);
            FinalizeTileStateUpsert(in snapshot, state, terrainData, oldChunkCountX, oldChunkCountZ);
        }

        private bool TryPrepareTileState(
            in MapMagicTerrainTileSnapshot snapshot,
            out TileRuntimeState state,
            out UnityEngine.TerrainData terrainData,
            out long tileKey,
            out int oldChunkCountX,
            out int oldChunkCountZ,
            out bool hadExistingTileState)
        {
            state = null;
            terrainData = null;
            tileKey = 0L;
            oldChunkCountX = 0;
            oldChunkCountZ = 0;
            hadExistingTileState = false;

            if (!snapshot.IsValid)
                return false;

            UnityEngine.Terrain terrain = snapshot.Terrain;
            if (terrain == null || terrain.terrainData == null)
            {
                RemoveTileState(snapshot.TileX, snapshot.TileZ);
                return false;
            }

            terrainData = terrain.terrainData;
            if (!TryResolveLayerIndices(terrainData, out LayerIndices indices))
            {
                RemoveTileState(snapshot.TileX, snapshot.TileZ);
                return false;
            }

            tileKey = PackTileCoord(snapshot.TileX, snapshot.TileZ);
            hadExistingTileState = _tileStates.TryGetValue(tileKey, out TileRuntimeState existingState) && existingState != null;
            if (hadExistingTileState)
            {
                oldChunkCountX = existingState.ChunkCountX;
                oldChunkCountZ = existingState.ChunkCountZ;
            }

            if (!_tileStates.TryAcquireOrCreate(tileKey, out state) || state == null)
            {
                RecordChunkQueueCapacityExceeded(_tileStates.Capacity, _tileStates.Count);
                return false;
            }

            state.TileX = snapshot.TileX;
            state.TileZ = snapshot.TileZ;
            state.Terrain = terrain;
            state.TerrainData = terrainData;
            state.TerrainPosition = terrain.GetPosition();
            state.TerrainSize = terrainData.size;
            state.AlphamapResolution = terrainData.alphamapResolution;
            state.HeightmapResolution = terrainData.heightmapResolution;
            state.HolesResolution = terrainData.holesResolution;
            state.ChunkCountX = math.max(1, (int)math.ceil(state.TerrainSize.x / DefaultVirtualChunkSize));
            state.ChunkCountZ = math.max(1, (int)math.ceil(state.TerrainSize.z / DefaultVirtualChunkSize));
            state.LayerIndices = indices;
            if (!EnsureTileNativeCacheSlot(state, tileKey))
            {
                if (!hadExistingTileState)
                    _tileStates.Remove(tileKey);
                return false;
            }

            return true;
        }

        private void FinalizeTileStateUpsert(
            in MapMagicTerrainTileSnapshot snapshot,
            TileRuntimeState state,
            UnityEngine.TerrainData terrainData,
            int oldChunkCountX,
            int oldChunkCountZ)
        {
            MarkTileTerrainHolesDirty(state);

            InvalidateTileChunks(snapshot.TileX, snapshot.TileZ, math.max(oldChunkCountX, state.ChunkCountX), math.max(oldChunkCountZ, state.ChunkCountZ));
            CacheTileMasks(state, terrainData);
        }

        private void RefreshColdRuntimeDependencies()
        {
            if (mapMagicBridge == null || !mapMagicBridge.isActiveAndEnabled)
            {
                mapMagicBridge = null;
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            }
            CacheOceanKinematicsService(GlobalRegistry.OceanKinematics);
            SyncWaterSurfaceLevelFromRuntime();

            if (_cachedLocalCamera == null)
                TryGetComponent(out _cachedLocalCamera);

            if (playerTransform == null)
                CachePlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || playerContext.PlayerTransform == null)
                return;

            _playerRuntimeContext = playerContext;
            playerTransform = playerContext.PlayerTransform;
        }

        private void ClearPlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || playerContext.PlayerTransform == null)
                return;

            if (ReferenceEquals(playerTransform, playerContext.PlayerTransform))
                playerTransform = null;
            if (ReferenceEquals(_playerRuntimeContext, playerContext))
                _playerRuntimeContext = null;
        }

        private void UpdatePlayerMotionState(float dt)
        {
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 currentPosition))
            {
                _playerVelocity = Vector3.zero;
                _hasLastPlayerPosition = false;
                return;
            }

            if (CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityVegetationMaxAgeFrames, out Vector3 kccVelocity))
            {
                _playerVelocity = kccVelocity;
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

        private bool IsForeignTile(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!snapshot.IsValid)
                return true;

            if (mapMagicBridge == null || snapshot.Provider == null)
                return false;

            return !ReferenceEquals(snapshot.Provider, mapMagicBridge);
        }

        private void TryRegister()
        {
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            bool slowRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            bool lateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _isRegistered = updateRegistered && slowRegistered && lateRegistered;
            if (!_isRegistered)
            {
                if (lateRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                if (slowRegistered)
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                if (updateRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            }
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _isRegistered = false;
        }

        private void TrySubscribeEvents()
        {
            if (!_eventsSubscribed)
            {
                MapMagicTerrainTileEvents.Register(this);
                _eventsSubscribed = true;
            }

            if (!_originShiftListenerRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void TryUnsubscribeEvents()
        {
            if (_eventsSubscribed)
            {
                MapMagicTerrainTileEvents.Unregister(this);
                _eventsSubscribed = false;
            }

            if (_originShiftListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftListenerRegistered = false;
            }
        }

        private void CacheWeatherService(IWeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        private void CacheOceanKinematicsService(IHectonOceanKinematicsService oceanKinematicsService)
        {
            _oceanKinematicsService = oceanKinematicsService;
        }

        private void SyncWaterSurfaceLevelFromRuntime()
        {
            if (TryResolveOceanWaterLevel(out float oceanWaterLevel))
            {
                ApplyResolvedWaterLevel(oceanWaterLevel);
                return;
            }

            SyncWaterSurfaceLevelFromTerrainBridge();
        }

        private bool TryResolveOceanWaterLevel(out float resolvedWaterLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterLevel(oceanKinematics.SeaLevel, out resolvedWaterLevel))
            {
                return true;
            }

            resolvedWaterLevel = DefaultWaterLevel;
            return false;
        }

        private void SyncWaterSurfaceLevelFromTerrainBridge()
        {
            MapMagicBridge bridge = mapMagicBridge;
            if (!WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge))
                return;

            mapMagicBridge = bridge;
            float bridgedWaterLevel = bridge.WaterSurfaceLevel;
            if (!TryResolveWaterLevel(bridgedWaterLevel, out float resolvedWaterLevel))
                return;

            ApplyResolvedWaterLevel(resolvedWaterLevel);
        }

        private void ApplyResolvedWaterLevel(float resolvedWaterLevel)
        {
            bool waterLevelChanged =
                !math.isfinite(waterLevel) ||
                math.abs(waterLevel - resolvedWaterLevel) > WaterLevelResyncEpsilonMeters;

            waterLevel = resolvedWaterLevel;
            float kelpDepthFloor = waterLevel - OrganicKelpMaxDepthBelowSurfaceMeters;
            if (!math.isfinite(kelpMinHeight) || kelpMinHeight > waterLevel)
                kelpMinHeight = kelpDepthFloor;
            else
                kelpMinHeight = math.clamp(kelpMinHeight, kelpDepthFloor, waterLevel);

            if (waterLevelChanged)
                ClearVegetationMacroGeologyParamsCache();

            if (!waterLevelChanged || !_runtimeLifecycleActive)
                return;

            CancelAllChunkBuildJobs();
            ClearChunkPayloadCache();
            _activeSetDirty = true;
            _activeBufferRebuildRequested = true;
            _startupResidencyPending = true;
        }

        private static bool TryResolveOceanWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Weather:
                    CacheWeatherService(currentService as IWeatherService);
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    CacheOceanKinematicsService(currentService as IHectonOceanKinematicsService);
                    SyncWaterSurfaceLevelFromRuntime();
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(mapMagicBridge, previousService))
                        mapMagicBridge = null;
                    mapMagicBridge = currentService as MapMagicBridge;
                    WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
                    SyncWaterSurfaceLevelFromRuntime();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    ClearPlayerRuntimeContext(previousService as IPlayerRuntimeContext);
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindVegetationMemoryVault(currentService as IDataVault);
                    break;
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            TryApplyWorldOffsetToAllChunks(shiftData.ShiftOffset, -shiftData.NewTotalOffsetDouble, refreshResidency: true);
        }

        private void ClearAllResidency()
        {
            bool hasClearableJobs = HasActiveChunkBuildJobs() ||
                _threatPropagationScheduled ||
                _flowFieldScheduled ||
                _abyssalThermalGridScheduled ||
                _abyssalPathScheduled;
            if (_selectedChunkCount == 0 && _pendingChunkCount == 0 && !_activeSetDirty && !hasClearableJobs)
                return;

            CancelAsyncWorldJobsForResidencyClear();
            ResetActiveState(clearChunkCache: true);
            ClearRendererBindings();
            ReleaseBuffers();
            _activeSetDirty = false;
        }

        private void CancelAsyncWorldJobsForResidencyClear()
        {
            CancelAllChunkBuildJobs();
            CancelVegetationSimulationJobsForResidencyClear();
            InvalidateAbyssalPathState();
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
            _abyssalAnchorCount = 0;
            _abyssalNavNodeCount = 0;
            _abyssalPathCount = 0;
            _megaWreckStreamCount = 0;
            _hlodRegistryCount = 0;
            _visibleHlodCount = 0;
            _canopyGridInitialized = false;
            _canopyGridCenter = Vector3.zero;
            _surfaceDrawBounds = default;
            _underwaterDrawBounds = default;

            if (clearChunkCache)
            {
                ClearChunkPayloadCache();
                _corruptedChunkCount = 0;
                _threatGridInitialized = false;
                _flowFieldInitialized = false;
                _abyssalThermalGridInitialized = false;
                _abyssalFlowVolumeInitialized = false;
                _abyssalThermalGridCenter = Vector3.zero;
                _scheduledAbyssalThermalGridCenter = Vector3.zero;
                _currentThreatHotspotLevel = 0f;
                _currentThreatHotspotPosition = Vector3.zero;
                _externalThreatPulsePosition = Vector3.zero;
                _externalThreatPulseRadius = 0f;
                _externalThreatPulseStrength = 0f;
                _externalThreatPulseHoldTimer = 0f;
                _idleNativePoolTimer = 0f;
                _poolDefragScheduled = false;
                _hlodCullScheduled = false;
            }
        }

        private void EnsureHLODSnapshotCapacityCold()
        {
            int megaWreckCapacity = megaWreckDefinitions != null ? megaWreckDefinitions.Length : 0;
            int snapshotCapacity = math.max(1, megaWreckCapacity + MaxPersistentArtificialStructureRecords);
            EnsureHLODDataCapacity(ref _hlodRegistrySnapshot, snapshotCapacity);
            EnsureHLODDataCapacity(ref _visibleHlodSnapshot, snapshotCapacity);
        }

        private void InitializeChunkPools()
        {
            if (_surfaceChunkPool.IsCreated && _underwaterChunkPool.IsCreated)
                return;

            int totalBudgetBytes = math.max(MinimumNativePoolBudgetMb, nativePoolBudgetMb) * 1024 * 1024;
            int totalCapacity = math.max(1024, totalBudgetBytes / ChunkPoolBytesPerInstance);
            int surfaceCapacity = math.clamp((int)math.round(totalCapacity * surfacePoolShare), 1024, totalCapacity - 1);
            int underwaterCapacity = math.max(1024, totalCapacity - surfaceCapacity);
            InitializeChunkPool(
                ref _surfaceChunkPool,
                BufferID.VegetationSurfaceChunkPoolMatrices,
                surfaceCapacity,
                ref _surfacePoolFreeBlocks,
                ref _surfacePoolFreeBlockCount);
            InitializeChunkPool(
                ref _underwaterChunkPool,
                BufferID.VegetationUnderwaterChunkPoolMatrices,
                underwaterCapacity,
                ref _underwaterPoolFreeBlocks,
                ref _underwaterPoolFreeBlockCount);
            RecordVegetationMemoryTelemetry(
                VegetationMemorySovereigntyConstants.TelemetryRingBufferId,
                _vegetationMemoryTelemetryHandle.Generation,
                totalCapacity,
                surfaceCapacity + underwaterCapacity,
                0,
                0f,
                VegetationMemoryTelemetryCode.ColdBootRegistered,
                VegetationMemoryTelemetryPhase.ColdBoot,
                VegetationMemorySovereigntyConstants.FlagColdBoot,
                default);
        }

        private void DisposeChunkPools()
        {
            DisposeChunkPool(ref _surfaceChunkPool);
            DisposeChunkPool(ref _underwaterChunkPool);
            _surfacePoolFreeBlocks = null;
            _underwaterPoolFreeBlocks = null;
            _surfaceDefragScratchFreeBlocks = null;
            _underwaterDefragScratchFreeBlocks = null;
            _surfacePoolFreeBlockCount = 0;
            _underwaterPoolFreeBlockCount = 0;
            _surfaceDefragScratchFreeBlockCount = 0;
            _underwaterDefragScratchFreeBlockCount = 0;
            _chunkPayloadUsedBytes = 0L;
        }

        private void ClearChunkPayloadCache()
        {
            if (_chunkPayloads.Count > 0)
            {
                FixedChunkPayloadMap.Enumerator enumerator = _chunkPayloads.GetEnumerator();
                while (enumerator.MoveNext())
                    ReleaseChunkPayloadStorage(enumerator.Current.Value);
            }

            DisposeAllChunkAbyssalNavPayloads();
            ClearChunkMegaWreckPayloads();
            _chunkPayloads.Clear();
            _chunkPayloadUsedBytes = 0L;
        }

        private void ReleaseChunkPayloadStorage(ChunkPayload payload)
        {
            _chunkPayloadUsedBytes -= GetChunkPayloadStorageBytes(payload);
            if (_chunkPayloadUsedBytes < 0L)
                _chunkPayloadUsedBytes = 0L;

            FreeChunkSliceForPayload(isSurface: true, payload);
            FreeChunkSliceForPayload(isSurface: false, payload);
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

        private void InitializeChunkPool(
            ref NativeChunkPool pool,
            BufferID matrixBufferId,
            int capacity,
            ref PoolBlock[] freeBlocks,
            ref int freeBlockCount)
        {
            if (pool.IsCreated && pool.Capacity == capacity)
                return;

            DisposeChunkPool(ref pool);
            if (!EnsureChunkPoolCapacity(ref pool, matrixBufferId, capacity))
                return;

            EnsurePoolBlockCapacity(ref freeBlocks, 1);
            freeBlocks[0] = new PoolBlock { Offset = 0, Length = capacity };
            freeBlockCount = 1;
        }

        private void DisposeChunkPool(ref NativeChunkPool pool)
        {
            ReleaseVegetationMemoryBuffer(ref pool.MatricesHandle);
            ReleaseVegetationMemoryBuffer(ref pool.MetadataHandle);
            ReleaseVegetationMemoryBuffer(ref pool.TypesHandle);
            ReleaseVegetationMemoryBuffer(ref pool.SemanticTypesHandle);
            ReleaseVegetationMemoryBuffer(ref pool.BiomeLayersHandle);
            ReleaseVegetationMemoryBuffer(ref pool.EdgeDistancesHandle);
            ReleaseVegetationMemoryBuffer(ref pool.FlowDirectionsHandle);
            ReleaseVegetationMemoryBuffer(ref pool.FlowVectorsHandle);
            pool.Dispose();
        }

        private void DisposeActiveNativeAggregates()
        {
            DisposeActiveAggregateBufferSet(ref _surfaceAggregateFrontBuffers);
            DisposeActiveAggregateBufferSet(ref _surfaceAggregateBackBuffers);
            DisposeActiveAggregateBufferSet(ref _underwaterAggregateFrontBuffers);
            DisposeActiveAggregateBufferSet(ref _underwaterAggregateBackBuffers);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.SurfaceAggregateCopyRecordsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.UnderwaterAggregateCopyRecordsHandle);
            _surfaceFrontBufferIndex = 0;
            _surfaceBackBufferIndex = 1;
            _underwaterFrontBufferIndex = 0;
            _underwaterBackBufferIndex = 1;
            _surfaceFrontCount = 0;
            _surfaceBackCount = 0;
            _underwaterFrontCount = 0;
            _underwaterBackCount = 0;
            _surfaceFrontDrawBounds = default;
            _surfaceBackDrawBounds = default;
            _underwaterFrontDrawBounds = default;
            _underwaterBackDrawBounds = default;
            _hasSurfaceFrontBounds = false;
            _hasSurfaceBackBounds = false;
            _hasUnderwaterFrontBounds = false;
            _hasUnderwaterBackBounds = false;
            _surfaceFrontReaderHandle = default;
            _surfaceBackReaderHandle = default;
            _underwaterFrontReaderHandle = default;
            _underwaterBackReaderHandle = default;
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalAnchorPositionsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalAnchorAupPositionsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalNavNodeSnapshotHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalNavConduitVectorsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalNavConduitStrengthsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalNavNodeTypesHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.MegaWreckStreamSnapshotHandle);
            _abyssalAnchorCount = 0;
            _abyssalNavNodeCount = 0;
            _megaWreckStreamCount = 0;
            _abyssalNavGraphOrigin = Vector3.zero;
        }

        private void DisposeDensityQuerySnapshot()
        {
            DisposeDensityQuerySnapshotLeases();
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.DensityQueryChunksHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.DensityQueryGridHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.ThreatAttractorGridHandle);
            DisposeDensityQueryScratch();
            _densityQueryChunkCount = 0;
        }

        private bool CacheTileMasks(TileRuntimeState state, UnityEngine.TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return false;

            int alphamapResolution = terrainData.alphamapResolution;
            if (alphamapResolution <= 0)
                return false;

            int heightResolution = terrainData.heightmapResolution;
            if (heightResolution <= 1)
                return false;

            long tileKey = PackTileCoord(state.TileX, state.TileZ);
            if (!EnsureTileNativeCacheSlot(state, tileKey))
                return false;

            if (state.HeightReadbackDisposalDeferred || state.TileCacheDisposalDeferred)
                return false;

            if (state.HeightReadbackPending)
            {
                if (!TryFinalizeTileHeightReadback(state))
                    return false;

                InvalidateTileChunks(state.TileX, state.TileZ, state.ChunkCountX, state.ChunkCountZ);
                _activeSetDirty = true;
            }

            state.AlphamapResolution = alphamapResolution;
            state.HeightmapResolution = heightResolution;

            int sampleCount = alphamapResolution * alphamapResolution;
            int heightSampleCount = heightResolution * heightResolution;
            int writeBufferIndex = state.ActiveCacheBufferIndex == 0 ? 1 : 0;
            if (!EnsureTileNativeCacheBufferCapacity(state, writeBufferIndex, sampleCount, heightSampleCount))
                return false;

            if (!TryRefreshTerrainTextureCachesHot(state, terrainData))
                return false;

            CaptureTileCacheSignature(
                state.AlphamapTextureCache,
                state.HeightTextureCache,
                out state.AlphamapTextureCount,
                out state.CombinedAlphamapHash,
                out state.CombinedAlphamapUpdateCount,
                out state.HeightmapHash,
                out state.HeightmapUpdateCount);

            Texture2D[] alphamapTextures = state.AlphamapTextureCache;
            TerrainLayerMaskSampler sandSampler0 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Sand0);
            TerrainLayerMaskSampler sandSampler1 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Sand1);
            TerrainLayerMaskSampler sandSampler2 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Sand2);
            TerrainLayerMaskSampler sandSampler3 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Sand3);
            TerrainLayerMaskSampler rockSampler0 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock0);
            TerrainLayerMaskSampler rockSampler1 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock1);
            TerrainLayerMaskSampler rockSampler2 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock2);
            TerrainLayerMaskSampler rockSampler3 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock3);
            TerrainLayerMaskSampler rockSampler4 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock4);
            TerrainLayerMaskSampler rockSampler5 = CreateTerrainLayerMaskSampler(alphamapTextures, state.LayerIndices.Rock5);
            TileNativeCacheBuffer writeBuffer = writeBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;
            BufferID sandBufferId = unchecked((BufferID)(int)writeBuffer.SandMaskHandle.BufferID);
            BufferID rockBufferId = unchecked((BufferID)(int)writeBuffer.RockMaskHandle.BufferID);
            if (!WriteTileSandMask(
                    ref writeBuffer.SandMaskHandle,
                    sandBufferId,
                    sampleCount,
                    alphamapResolution,
                    in sandSampler0,
                    in sandSampler1,
                    in sandSampler2,
                    in sandSampler3))
                return false;

            if (!WriteTileRockMask(
                    ref writeBuffer.RockMaskHandle,
                    rockBufferId,
                    sampleCount,
                    alphamapResolution,
                    in rockSampler0,
                    in rockSampler1,
                    in rockSampler2,
                    in rockSampler3,
                    in rockSampler4,
                    in rockSampler5))
                return false;

            Texture heightTexture = state.HeightTextureCache;
            if (heightTexture == null)
                return false;

            if (!HasTileHeightReadbackData(state, heightSampleCount))
            {
                QueueTileHeightReadbackRepair(state, heightSampleCount);
                return false;
            }

            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(
                ref state.HeightReadbackData,
                heightTexture,
                0,
                TextureFormat.R16);
            if (request.hasError)
                return false;

            state.PendingCacheBufferIndex = writeBufferIndex;
            state.HeightReadbackRequest = request;
            state.HeightReadbackPending = true;
            if (writeBufferIndex == 0)
                state.PrimaryCacheBuffer = writeBuffer;
            else
                state.SecondaryCacheBuffer = writeBuffer;

            return false;
        }

        private static void EnsureTileHeightReadbackData(TileRuntimeState state, int sampleCount)
        {
            if (state == null || state.HeightReadbackDisposalDeferred)
                return;

            int requiredCount = Mathf.NextPowerOfTwo(math.max(1, sampleCount));
            if (state.HeightReadbackData.IsCreated && state.HeightReadbackData.Length >= requiredCount)
            {
                state.HeightReadbackRepairRequested = false;
                state.HeightReadbackRepairSampleCount = 0;
                return;
            }

            DisposeTileHeightReadbackData(state);
            state.HeightReadbackData = H8Memory.Allocate<ushort>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<ushort>[tile height samples] - async vegetation tile height readback target - owner: HectonMapMagicVegetationBridge
            if (!state.HeightReadbackData.IsCreated)
            {
                state.HeightReadbackRepairRequested = true;
                state.HeightReadbackRepairSampleCount = requiredCount;
                return;
            }

            state.HeightReadbackRepairRequested = false;
            state.HeightReadbackRepairSampleCount = 0;
        }

        private static bool HasTileHeightReadbackData(TileRuntimeState state, int sampleCount)
        {
            if (state == null || state.HeightReadbackDisposalDeferred)
                return false;

            int requiredCount = Mathf.NextPowerOfTwo(math.max(1, sampleCount));
            return state.HeightReadbackData.IsCreated && state.HeightReadbackData.Length >= requiredCount;
        }

        private static void QueueTileHeightReadbackRepair(TileRuntimeState state, int sampleCount)
        {
            if (state == null || state.HeightReadbackDisposalDeferred)
                return;

            state.HeightReadbackRepairRequested = true;
            state.HeightReadbackRepairSampleCount = math.max(state.HeightReadbackRepairSampleCount, sampleCount);
        }

        private void FlushTileHeightReadbackRepairsSlow()
        {
            if (_tileStates.Count <= 0)
                return;

            bool repairedAny = false;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null ||
                    !state.HeightReadbackRepairRequested ||
                    state.HeightReadbackPending ||
                    state.HeightReadbackDisposalDeferred)
                    continue;

                int repairSampleCount = state.HeightReadbackRepairSampleCount;
                if (!HasTileHeightReadbackData(state, repairSampleCount))
                {
                    state.HeightReadbackRepairRequested = false;
                    state.HeightReadbackRepairSampleCount = 0;
                    continue;
                }

                state.HeightReadbackRepairRequested = false;
                state.HeightReadbackRepairSampleCount = 0;
                repairedAny = true;
            }

            if (repairedAny)
                _residentTileCacheValidationRequested = true;
        }

        private static void DisposeTileHeightReadbackData(TileRuntimeState state)
        {
            if (state == null)
                return;

            if (state.HeightReadbackDisposalDeferred)
                return;

            ReleaseTileHeightReadbackData(state);
        }

        private static void ReleaseTileHeightReadbackData(TileRuntimeState state)
        {
            if (state == null)
                return;

            if (state.HeightReadbackData.IsCreated)
            {
                H8Memory.Release(
                    ref state.HeightReadbackData,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (state.HeightReadbackData.IsCreated)
                    return;
            }

            state.HeightReadbackRepairRequested = false;
            state.HeightReadbackRepairSampleCount = 0;
            state.HeightReadbackDisposalDeferred = false;
            state.HeightReadbackData = default;
        }

        private bool WriteTileSandMask(
            ref VaultGenerationHandle<byte> sandMaskHandle,
            BufferID sandBufferId,
            int sampleCount,
            int alphamapResolution,
            in TerrainLayerMaskSampler sandSampler0,
            in TerrainLayerMaskSampler sandSampler1,
            in TerrainLayerMaskSampler sandSampler2,
            in TerrainLayerMaskSampler sandSampler3)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref sandMaskHandle,
                    sandBufferId,
                    sampleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<byte> sandMaskWrite))
            {
                return false;
            }

            try
            {
                int writeIndex = 0;
                for (int z = 0; z < alphamapResolution; z++)
                {
                    for (int x = 0; x < alphamapResolution; x++)
                    {
                        float sandMask =
                            SampleTerrainLayerMask(in sandSampler0, writeIndex) +
                            SampleTerrainLayerMask(in sandSampler1, writeIndex) +
                            SampleTerrainLayerMask(in sandSampler2, writeIndex) +
                            SampleTerrainLayerMask(in sandSampler3, writeIndex);
                        sandMaskWrite[writeIndex] = PackMask01(sandMask);
                        writeIndex++;
                    }
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in sandMaskHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteTileRockMask(
            ref VaultGenerationHandle<byte> rockMaskHandle,
            BufferID rockBufferId,
            int sampleCount,
            int alphamapResolution,
            in TerrainLayerMaskSampler rockSampler0,
            in TerrainLayerMaskSampler rockSampler1,
            in TerrainLayerMaskSampler rockSampler2,
            in TerrainLayerMaskSampler rockSampler3,
            in TerrainLayerMaskSampler rockSampler4,
            in TerrainLayerMaskSampler rockSampler5)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref rockMaskHandle,
                    rockBufferId,
                    sampleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<byte> rockMaskWrite))
            {
                return false;
            }

            try
            {
                int writeIndex = 0;
                for (int z = 0; z < alphamapResolution; z++)
                {
                    for (int x = 0; x < alphamapResolution; x++)
                    {
                        float rockMask =
                            SampleTerrainLayerMask(in rockSampler0, writeIndex) +
                            SampleTerrainLayerMask(in rockSampler1, writeIndex) +
                            SampleTerrainLayerMask(in rockSampler2, writeIndex) +
                            SampleTerrainLayerMask(in rockSampler3, writeIndex) +
                            SampleTerrainLayerMask(in rockSampler4, writeIndex) +
                            SampleTerrainLayerMask(in rockSampler5, writeIndex);
                        rockMaskWrite[writeIndex] = PackMask01(rockMask);
                        writeIndex++;
                    }
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in rockMaskHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private static TerrainLayerMaskSampler CreateTerrainLayerMaskSampler(Texture2D[] alphamapTextures, int layerIndex)
        {
            if (layerIndex < 0 || alphamapTextures == null)
                return default;

            int textureIndex = layerIndex >> 2;
            if ((uint)textureIndex >= (uint)alphamapTextures.Length)
                return default;

            Texture2D texture = alphamapTextures[textureIndex];
            if (texture == null || !texture.isReadable)
                return default;

            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            if (!pixels.IsCreated)
                return default;

            return new TerrainLayerMaskSampler(pixels, layerIndex & 3);
        }

        private static float SampleTerrainLayerMask(in TerrainLayerMaskSampler sampler, int sampleIndex)
        {
            if (sampler.Valid == 0 || (uint)sampleIndex >= (uint)sampler.Pixels.Length)
                return 0f;

            Color32 pixel = sampler.Pixels[sampleIndex];
            byte channel = (byte)(sampler.Channel switch
            {
                0 => pixel.r,
                1 => pixel.g,
                2 => pixel.b,
                _ => pixel.a
            });

            return channel * (1f / 255f);
        }

        private static void CaptureTileCacheSignature(
            Texture2D[] alphamapTextures,
            Texture heightTexture,
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

            if (heightTexture == null)
                return;

            heightmapHash = heightTexture.imageContentsHash.GetHashCode();
            heightmapUpdateCount = heightTexture.updateCount;
        }

        private static void RefreshTerrainTextureCachesCold(TileRuntimeState state, UnityEngine.TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return;

            int textureCount = math.max(0, terrainData.alphamapTextureCount);
            if (textureCount == 0)
            {
                state.AlphamapTextureCache = null;
            }
            else
            {
                Texture2D[] cachedTextures = state.AlphamapTextureCache;
                if (cachedTextures == null || cachedTextures.Length != textureCount)
                {
                    // COLD ALLOC: Texture2D[textureCount] - per-tile alpha texture cache handles for zero-GC mask sampling - owner: HectonMapMagicVegetationBridge
                    cachedTextures = new Texture2D[textureCount];
                    state.AlphamapTextureCache = cachedTextures;
                }

                for (int i = 0; i < textureCount; i++)
                    cachedTextures[i] = terrainData.GetAlphamapTexture(i);
            }

            state.HeightTextureCache = terrainData.heightmapTexture;
        }

        private static bool TryRefreshTerrainTextureCachesHot(TileRuntimeState state, UnityEngine.TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return false;

            int textureCount = math.max(0, terrainData.alphamapTextureCount);
            if (textureCount == 0)
            {
                state.AlphamapTextureCache = null;
                state.HeightTextureCache = terrainData.heightmapTexture;
                return true;
            }

            Texture2D[] cachedTextures = state.AlphamapTextureCache;
            if (cachedTextures == null || cachedTextures.Length != textureCount)
                return false;

            for (int i = 0; i < textureCount; i++)
                cachedTextures[i] = terrainData.GetAlphamapTexture(i);

            state.HeightTextureCache = terrainData.heightmapTexture;
            return true;
        }

        private void ClearRendererBindings()
        {
            _surfaceFrontCount = 0;
            _surfaceBackCount = 0;
            _underwaterFrontCount = 0;
            _underwaterBackCount = 0;
            _hasSurfaceFrontBounds = false;
            _hasSurfaceBackBounds = false;
            _hasUnderwaterFrontBounds = false;
            _hasUnderwaterBackBounds = false;
            _surfaceFrontReaderHandle = default;
            _surfaceBackReaderHandle = default;
            _underwaterFrontReaderHandle = default;
            _underwaterBackReaderHandle = default;
            ClearChannel(surfaceRenderer);
            ClearChannel(underwaterRenderer);
        }

        private static void ClearChannel(HectonIndirectVegetationRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.BindFloraPhaseSeedBuffer(null);
            renderer.ClearSource();
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _surfaceInstanceBuffer);
            ReleaseBuffer(ref _surfaceInstanceDataBuffer);
            ReleaseBuffer(ref _underwaterInstanceBuffer);
            ReleaseBuffer(ref _underwaterInstanceDataBuffer);
            ReleaseBuffer(ref _predatorFearNodeBufferA);
            ReleaseBuffer(ref _predatorFearNodeBufferB);
            _activePredatorFearNodeBuffer = null;
            _predatorFearNodeBufferWriteIndex = 0;
            _pendingPredatorFearShaderUpload = false;
            _pendingPredatorFearShaderActiveCount = 0;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void DisposeThreatGridState()
        {
            CompleteThreatPropagationJob(forceComplete: true);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.EcosystemThreatGridHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.EcosystemThreatGridCompressedHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.EcosystemThreatVoxelHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.EcosystemThreatEchoHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.ThreatPropagationStagingHandle);

            _threatSamplingChunkCount = 0;
            _threatPropagationScheduled = false;
            _threatGridInitialized = false;
            _currentThreatHotspotLevel = 0f;
            _currentThreatHotspotPosition = Vector3.zero;
            _externalThreatPulsePosition = Vector3.zero;
            _externalThreatPulseRadius = 0f;
            _externalThreatPulseStrength = 0f;
            _externalThreatPulseHoldTimer = 0f;
            _ecosystemThreatGridCenter = Vector3.zero;
            _scheduledThreatGridCenter = Vector3.zero;
            _ecosystemThreatVoxelOrigin = Vector3.zero;
            _scheduledThreatVoxelOrigin = Vector3.zero;
            _lastThreatPropagationTime = float.NegativeInfinity;
            _threatSpatialSolveCursor = 0;
        }

        private void DisposeFlowFieldState()
        {
            CompleteFlowFieldJob(forceComplete: true);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.FlowFieldStagingHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.EcosystemFlowFieldHandle);
            _flowFieldScheduled = false;
            _flowFieldInitialized = false;
            _swarmWakeImpulseCount = 0;
            _swarmWakeImpulse = default;
            _swarmWakeImpulseExpireTime = float.NegativeInfinity;
            _ecosystemFlowFieldCenter = Vector3.zero;
            _scheduledFlowFieldCenter = Vector3.zero;
            _lastFlowFieldSolveTime = float.NegativeInfinity;
        }

        private void DisposeCanopyGridState()
        {
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.CanopyHeightGridHandle);
            _canopyGridInitialized = false;
            _canopyGridCenter = Vector3.zero;
        }

        private void DisposeThermalGridState()
        {
            CompleteThermalGridJob(forceComplete: true);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.ThermalGridStagingHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalThermalGridHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalFlowVolumeHandle);
            _abyssalThermalGridScheduled = false;
            _abyssalThermalGridInitialized = false;
            _abyssalFlowVolumeInitialized = false;
            _abyssalThermalGridCenter = Vector3.zero;
            _scheduledAbyssalThermalGridCenter = Vector3.zero;
            _lastThermalGridSolveTime = float.NegativeInfinity;
            _abyssalThermalGridRingOffsetX = 0;
            _abyssalThermalGridRingOffsetY = 0;
            _abyssalThermalGridRingOffsetZ = 0;
        }

        private void DisposeArtificialStructureSnapshot()
        {
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.ArtificialStructureRecordsHandle);

            _artificialStructureCount = 0;
        }

        private void DisposePoolDefragState()
        {
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.SurfaceDefragMovesHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.UnderwaterDefragMovesHandle);
            _surfacePoolDefragHandle = default;
            _underwaterPoolDefragHandle = default;
            _poolDefragScheduled = false;
            _surfaceDefragMoveCount = 0;
            _underwaterDefragMoveCount = 0;
            _surfaceDefragCompactUsedCount = 0;
            _underwaterDefragCompactUsedCount = 0;
            _surfaceDefragScratchFreeBlocks = null;
            _underwaterDefragScratchFreeBlocks = null;
            _surfaceDefragScratchFreeBlockCount = 0;
            _underwaterDefragScratchFreeBlockCount = 0;
        }

        private void ShiftChunkMegaWreckPayloads(Vector3 offset)
        {
            if (_chunkMegaWreckPayloadCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int keyIndex = 0; keyIndex < _chunkMegaWreckPayloadCount; keyIndex++)
            {
                ChunkMegaWreckPayload payload = _chunkMegaWreckPayloads[keyIndex];
                if (payload.Count <= 0 || payload.Sections == null)
                    continue;

                for (int sectionIndex = 0; sectionIndex < payload.Count; sectionIndex++)
                {
                    MegaWreckStreamSection section = payload.Sections[sectionIndex];
                    section.WorldCenter += offset;
                    payload.Sections[sectionIndex] = section;
                }

                _chunkMegaWreckPayloads[keyIndex] = payload;
            }
        }

        private void ShiftMegaWreckSnapshot(Vector3 offset)
        {
            if (_megaWreckStreamCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            bool hasNativeSections = TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.MegaWreckStreamSnapshotHandle,
                BufferID.VegetationMegaWreckStreamSnapshot,
                _megaWreckStreamCount,
                NativeArrayOptions.UninitializedMemory,
                out IDataVault vault,
                out NativeArray<MegaWreckStreamSection> nativeSections);

            try
            {
                for (int i = 0; i < _megaWreckStreamCount; i++)
                {
                    MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                    section.WorldCenter += offset;
                    _megaWreckStreamSnapshot[i] = section;
                    if (hasNativeSections && i < nativeSections.Length)
                        nativeSections[i] = section;
                }
            }
            finally
            {
                if (hasNativeSections)
                {
                    vault.ReleaseWriteLock(
                        in _nativeMemory.MegaWreckStreamSnapshotHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                }
            }
        }

        private static float SampleThreatGridAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<float> threatGrid)
        {
            if (!threatGrid.IsCreated ||
                resolution <= 0 ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                !IsFinite(position) ||
                !IsFinite(gridCenter))
            {
                return 0f;
            }

            long expectedLength = (long)resolution * resolution;
            if (expectedLength <= 0L || expectedLength > int.MaxValue || threatGrid.Length < expectedLength)
            {
                return 0f;
            }

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            if (!math.isfinite(halfExtent))
            {
                return 0f;
            }

            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0f;

            float inverseCellSize = math.rcp(cellSize);
            float normalizedX = math.clamp(localX * inverseCellSize, 0f, resolution - 1);
            float normalizedZ = math.clamp(localZ * inverseCellSize, 0f, resolution - 1);
            int cellX = math.clamp((int)math.floor(normalizedX), 0, resolution - 1);
            int cellZ = math.clamp((int)math.floor(normalizedZ), 0, resolution - 1);
            int nextCellX = math.min(cellX + 1, resolution - 1);
            int nextCellZ = math.min(cellZ + 1, resolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float sample00 = threatGrid[(cellZ * resolution) + cellX];
            float sample10 = threatGrid[(cellZ * resolution) + nextCellX];
            float sample01 = threatGrid[(nextCellZ * resolution) + cellX];
            float sample11 = threatGrid[(nextCellZ * resolution) + nextCellX];
            float sampleX0 = math.lerp(sample00, sample10, fracX);
            float sampleX1 = math.lerp(sample01, sample11, fracX);
            float sampledThreat = math.lerp(sampleX0, sampleX1, fracZ);
            return math.select(0f, sampledThreat, math.isfinite(sampledThreat));
        }

        private static int ComputeThreatGridCellIndex(float3 position, float3 gridCenter, float cellSize, int resolution)
        {
            return VegetationMath.ComputeThreatGridCellIndex(position, gridCenter, cellSize, resolution);
        }

        private float SampleCanopyHeightAtPosition(float worldX, float worldZ)
        {
            if (_canopyGridResolution <= 0 ||
                canopyGridCellSize <= 0f ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.CanopyHeightGridHandle,
                    BufferID.VegetationCanopyHeightGrid,
                    _canopyGridCellCount,
                    out NativeArray<float>.ReadOnly canopyGrid))
            {
                return float.NegativeInfinity;
            }

            float halfExtent = (_canopyGridResolution - 1) * 0.5f * canopyGridCellSize;
            float localX = worldX - (_canopyGridCenter.x - halfExtent);
            float localZ = worldZ - (_canopyGridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return float.NegativeInfinity;

            int cellX = math.clamp((int)math.round(localX / canopyGridCellSize), 0, _canopyGridResolution - 1);
            int cellZ = math.clamp((int)math.round(localZ / canopyGridCellSize), 0, _canopyGridResolution - 1);
            return canopyGrid[(cellZ * _canopyGridResolution) + cellX];
        }

        private static byte SampleThreatEchoFlagAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<byte> echoFlags)
        {
            if (!echoFlags.IsCreated ||
                resolution <= 0 ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                !IsFinite(position) ||
                !IsFinite(gridCenter))
            {
                return 0;
            }

            long expectedLength = (long)resolution * resolution;
            if (expectedLength <= 0L || expectedLength > int.MaxValue || echoFlags.Length < expectedLength)
            {
                return 0;
            }

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            if (!math.isfinite(halfExtent))
            {
                return 0;
            }

            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0;

            float inverseCellSize = math.rcp(cellSize);
            int cellX = math.clamp((int)math.round(localX * inverseCellSize), 0, resolution - 1);
            int cellZ = math.clamp((int)math.round(localZ * inverseCellSize), 0, resolution - 1);
            return echoFlags[(cellZ * resolution) + cellX];
        }

        private static byte SampleThreatEchoAtWorldPosition(
            float worldX,
            float worldZ,
            float3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<byte> echoFlags)
        {
            if (!echoFlags.IsCreated ||
                resolution <= 0 ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                !math.isfinite(worldX) ||
                !math.isfinite(worldZ) ||
                !math.all(math.isfinite(gridCenter)))
            {
                return 0;
            }

            long expectedLength = (long)resolution * resolution;
            if (expectedLength <= 0L || expectedLength > int.MaxValue || echoFlags.Length < expectedLength)
            {
                return 0;
            }

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            if (!math.isfinite(halfExtent))
            {
                return 0;
            }

            float localX = worldX - (gridCenter.x - halfExtent);
            float localZ = worldZ - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0;

            float inverseCellSize = math.rcp(cellSize);
            int cellX = math.clamp((int)math.round(localX * inverseCellSize), 0, resolution - 1);
            int cellZ = math.clamp((int)math.round(localZ * inverseCellSize), 0, resolution - 1);
            return echoFlags[(cellZ * resolution) + cellX];
        }

        private static bool DoesChunkBoundsIntersectCircle(float minX, float maxX, float minZ, float maxZ, float centerX, float centerZ, float radiusSq)
        {
            float clampedX = math.clamp(centerX, minX, maxX);
            float clampedZ = math.clamp(centerZ, minZ, maxZ);
            float dx = centerX - clampedX;
            float dz = centerZ - clampedZ;
            return (dx * dx) + (dz * dz) <= radiusSq;
        }

        private int CountTerrainHolesIntersectingChunk(float minX, float maxX, float minZ, float maxZ)
        {
            if (_terrainHoleCount <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _terrainHoleCount; i++)
            {
                TerrainHoleRecord hole = _terrainHoleRecords[i];
                if (DoesChunkBoundsIntersectCircle(minX, maxX, minZ, maxZ, hole.X, hole.Z, hole.RadiusSq))
                    count++;
            }

            return count;
        }

        private static float DecodeThreatByte(byte encoded)
        {
            return VegetationMath.DecodeThreatByte(encoded);
        }

        private static byte EncodeThreatByte(float threat)
        {
            return VegetationMath.EncodeThreatByte(threat);
        }

        private static void ClearByteGrid(NativeArray<byte> destination, int count)
        {
            VegetationMath.ClearByteGrid(destination, count);
        }

        private static void ClearFloatGrid(NativeArray<float> destination, int count)
        {
            VegetationMath.ClearFloatGrid(destination, count);
        }

        private static int ComputeMegaWreckSectionSeed(int baseSeed, int sectionX, int sectionZ)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)baseSeed) * 16777619u;
                hash = (hash ^ (uint)sectionX) * 16777619u;
                hash = (hash ^ (uint)sectionZ) * 16777619u;
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static int PositiveModulo(int value, int length)
        {
            return VegetationMath.PositiveModulo(value, length);
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
            return VegetationMath.BuildCellSeed(cellX, cellZ, salt);
        }

        private static byte PackMask01(float value)
        {
            return VegetationMath.PackMask01(value);
        }

        private static Vector2 NormalizeFlowDirection(Vector2 direction)
        {
            float2 octant = ResolveOctantDirectionFromVector(direction.x, direction.y);
            return new Vector2(octant.x, octant.y);
        }

        private static float Hash01(uint seed)
        {
            return VegetationMath.Hash01(seed);
        }
    }
}
