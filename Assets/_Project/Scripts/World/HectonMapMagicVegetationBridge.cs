using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
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
    public sealed partial class HectonMapMagicVegetationBridge : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IMapMagicTerrainTileEventListener
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct PredatorFearNodeSnapshot
        {
            public float3 Position;
            public float Radius;
            public float Weight;
            public int SpeciesId;
            public float Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private struct AbyssalPathTelemetryEntry
        {
            public int Frame;
            public int RawCount;
            public int OutputCount;
            public int PortalLookAhead;
            public int MaxDdaSamples;
            public float FunnelMs;
            public float StartX;
            public float StartY;
            public float StartZ;
            public float EndX;
            public float EndY;
            public float EndZ;
            public uint Flags;
            public uint Sequence;
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
        private const float DefaultWaterLevel = 4900f;
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
        private const int InitialTileCapacity = 32;
        private const int TileCacheLruCapacity = 64;
        private const int InitialChunkCapacity = 256;
        private const int InitialChunkArrayCapacity = 64;
        private const int InitialCorruptedChunkCapacity = 512;
        private const int ChunkPoolBytesPerInstance = 128;
        private const int MinimumNativePoolBudgetMb = 64;
        private const int DensityGridResolution = VegetationMath.DensityGridResolution;
        private const int DensityGridCellCount = DensityGridResolution * DensityGridResolution;
        private const float DensityQuerySeedScale = 2f;
        private const int VegetationAudioProbeCount = 5;
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
        private const int LowTierAbyssalPathPortalLookAhead = 4;
        private const int MidTierAbyssalPathPortalLookAhead = 8;
        private const int HighTierAbyssalPathPortalLookAhead = 16;
        private const int LowTierAbyssalPathDdaSamples = 32;
        private const int MidTierAbyssalPathDdaSamples = 64;
        private const int AbyssalPathTelemetryFrameCount = 300;
        private const int DefaultMaxAbyssalNavNodeCapacity = 8192;
        private const int DefaultMaxAbyssalPathWaypointCapacity = 8192;
        private const int DefaultAbyssalNavHashEntriesPerNode = 4;
        private const int FixedThreatSamplingHashCapacity = 65536;
        private const int FixedArtificialStructureHashCapacity = 65536;
        private const uint AbyssalPathTelemetryContextHash = 0x41504154u;
        private const uint AbyssalPathOverBudgetHash = 0x46554E4Cu;
        private const uint AbyssalPathNanFaultHash = 0x4E414E46u;
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
        [SerializeField, Min(4900f)]
        [Tooltip("Project water surface level. Contract fixes this at Y=4900.")]
        private float waterLevel = DefaultWaterLevel;

        [SerializeField, Min(4600f)]
        [Tooltip("Minimum terrain height that still accepts organic kelp placement. Runtime clamp enforces the 200m depth cap.")]
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
        public readonly struct TerrainHeightSamplePayload
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
            public NativeArray<ushort> HeightSamples { get; }

            /// <summary>World-space minimum corner of the terrain tile.</summary>
            public Vector3 TerrainPosition { get; }

            /// <summary>World-space size of the terrain tile.</summary>
            public Vector3 TerrainSize { get; }

            /// <summary>Heightmap resolution used by the active native payload.</summary>
            public int HeightmapResolution { get; }

            /// <summary>Authoritative cache revision for the resolved tile.</summary>
            public int CacheRevision { get; }

            public bool IsValid =>
                HeightSamples.IsCreated &&
                HeightmapResolution > 1 &&
                HeightSamples.Length >= HeightmapResolution * HeightmapResolution;
        }

        private struct TileNativeCacheBuffer
        {
            public NativeArray<byte> SandMaskNative;
            public NativeArray<byte> RockMaskNative;
            public NativeArray<ushort> HeightSamplesNative;
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
            public bool PendingRemoval;
            public AsyncGPUReadbackRequest HeightReadbackRequest;
            public int HolesResolution;
            public bool TerrainHolesDirty;
            public bool TerrainHolesJobScheduled;
            public JobHandle TerrainHolesJobHandle;
            public NativeArray<bool> TerrainHoleMaskNative;
            public bool[,] TerrainHoleMaskManaged;
            public TileNativeCacheBuffer PrimaryCacheBuffer;
            public TileNativeCacheBuffer SecondaryCacheBuffer;
        }

        private struct DeferredTileCacheDisposal
        {
            public AsyncGPUReadbackRequest Request;
            public TileNativeCacheBuffer PrimaryCacheBuffer;
            public TileNativeCacheBuffer SecondaryCacheBuffer;
        }

        // COLD ALLOC: List<DeferredTileCacheDisposal>[8] - deferred terrain height-readback cache disposals that avoid main-thread GPU stalls during teardown - owner: HectonMapMagicVegetationBridge
        private static readonly List<DeferredTileCacheDisposal> s_DeferredTileCacheDisposals = new List<DeferredTileCacheDisposal>(8);

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

        private struct TerrainHoleRecord
        {
            public int HoleId;
            public float Y;
            public float X;
            public float Z;
            public float Radius;
            public float RadiusSq;
            public TerrainHoleSourceType SourceType;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct TerrainHoleMaskBuildJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            public int TerrainHoleCount;
            public int Resolution;
            public float TerrainOriginX;
            public float TerrainOriginZ;
            public float TerrainSizeX;
            public float TerrainSizeZ;
            public NativeArray<bool> Output;

            public void Execute(int index)
            {
                if (!Output.IsCreated || Resolution <= 0)
                    return;

                int x = index % Resolution;
                int y = index / Resolution;
                float normalizedX = Resolution > 1 ? (float)x / (Resolution - 1) : 0f;
                float normalizedZ = Resolution > 1 ? (float)y / (Resolution - 1) : 0f;
                float worldX = TerrainOriginX + (normalizedX * TerrainSizeX);
                float worldZ = TerrainOriginZ + (normalizedZ * TerrainSizeZ);
                bool hasTerrain = true;
                int holeCount = math.min(TerrainHoleCount, TerrainHoles.Length);
                for (int i = 0; i < holeCount; i++)
                {
                    TerrainHoleRecord hole = TerrainHoles[i];
                    if (hole.SourceType != TerrainHoleSourceType.CaveEntrance)
                        continue;

                    float dx = worldX - hole.X;
                    float dz = worldZ - hole.Z;
                    if ((dx * dx) + (dz * dz) > hole.RadiusSq)
                        continue;

                    hasTerrain = false;
                    break;
                }

                Output[index] = hasTerrain;
            }
        }

        private struct ChunkSliceMoveRecord
        {
            public int SourceOffset;
            public int DestinationOffset;
            public int Count;
        }

        private struct ActiveAggregateCopyRecord
        {
            public int SourceOffset;
            public int DestinationOffset;
            public int Count;
            public byte PoolSet;

            public ActiveAggregateCopyRecord(int sourceOffset, int destinationOffset, int count, byte poolSet)
            {
                SourceOffset = sourceOffset;
                DestinationOffset = destinationOffset;
                Count = count;
                PoolSet = poolSet;
            }
        }

        private struct ArtificialStructureRecord
        {
            public float MinX;
            public float MinY;
            public float MinZ;
            public float MaxX;
            public float MaxY;
            public float MaxZ;
            public byte Type;
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
        public struct MegaWreckStreamSection
        {
            public int WreckId;
            public int SectionSeed;
            public int SectionX;
            public int SectionZ;
            public Vector3 WorldCenter;
            public Vector3 WorldSize;
            public Vector3 LocalCenter;
            public Vector3 LocalSize;
        }

        private sealed class ChunkBuildJobState
        {
            public ChunkKey Key;
            public long TileKey;
            public int TileCacheRevision;
            public byte GrassLodTier;
            public byte CorruptionState;
            public ChunkPayload PayloadHeader;
            public JobHandle Handle;
            public NativeArray<JobInstanceRecord> GrassRecords;
            public NativeArray<JobInstanceRecord> FloatingRecords;
            public NativeArray<JobInstanceRecord> KelpRecords;
            public bool CancelRequested;
        }

        private struct ChunkAbyssalNavPayload
        {
            public NativeArray<Vector3> Nodes;
            public NativeArray<Vector3> ConduitVectors;
            public NativeArray<float> ConduitStrengths;
            public NativeArray<byte> NodeTypes;
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

        // COLD ALLOC: Dictionary<long, TileRuntimeState>[32] - MapMagic tile state cache for chunk streaming - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<long, TileRuntimeState> _tileStates = new Dictionary<long, TileRuntimeState>(InitialTileCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkPayload>[256] - streamed virtual chunk cache - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkPayload> _chunkPayloads = new Dictionary<ChunkKey, ChunkPayload>(InitialChunkCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkBuildJobState>[256] - in-flight Burst chunk generation jobs - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkBuildJobState> _chunkBuildJobs = new Dictionary<ChunkKey, ChunkBuildJobState>(InitialChunkCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkAbyssalNavPayload>[256] - finalized per-chunk abyssal navigation node cache - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkAbyssalNavPayload> _chunkAbyssalNavPayloads = new Dictionary<ChunkKey, ChunkAbyssalNavPayload>(InitialChunkCapacity);
        // COLD ALLOC: Dictionary<ChunkKey, ChunkMegaWreckPayload>[256] - finalized per-chunk mega-wreck streaming section cache - owner: HectonMapMagicVegetationBridge
        private readonly Dictionary<ChunkKey, ChunkMegaWreckPayload> _chunkMegaWreckPayloads = new Dictionary<ChunkKey, ChunkMegaWreckPayload>(InitialChunkCapacity);
        // COLD ALLOC: HashSet<ChunkKey>[512] - bounded runtime corrupted-chunk lookup - owner: HectonMapMagicVegetationBridge
        private readonly HashSet<ChunkKey> _corruptedChunkKeys = new HashSet<ChunkKey>(InitialCorruptedChunkCapacity);
        // COLD ALLOC: List<ChunkKey>[64] - insertion-order corruption state tracking for bounded eviction - owner: HectonMapMagicVegetationBridge
        private readonly List<ChunkKey> _corruptedChunkOrder = new List<ChunkKey>(InitialChunkArrayCapacity);
        // COLD ALLOC: List<PersistentArtificialStructureRecord>[32] - player/runtime-authored artificial structure registry - owner: HectonMapMagicVegetationBridge
        private readonly List<PersistentArtificialStructureRecord> _persistentArtificialStructures = new List<PersistentArtificialStructureRecord>(32);
        // COLD ALLOC: List<ChunkKey>[64] - eviction staging for non-resident chunk payloads - owner: HectonMapMagicVegetationBridge
        private readonly List<ChunkKey> _evictionKeys = new List<ChunkKey>(InitialChunkArrayCapacity);
        // COLD ALLOC: List<ChunkKey>[64] - in-flight chunk job scratch list for completion/eviction - owner: HectonMapMagicVegetationBridge
        private readonly List<ChunkKey> _jobScratchKeys = new List<ChunkKey>(InitialChunkArrayCapacity);
        // COLD ALLOC: List<TerrainHoleRecord>[16] - terrain-hole distance-eviction scratch cache - owner: HectonMapMagicVegetationBridge
        private readonly List<TerrainHoleRecord> _terrainHoleEvictionScratch = new List<TerrainHoleRecord>(16);
        // COLD ALLOC: List<long>[16] - deferred tile-removal staging while GPU height readbacks finish without blocking the main thread - owner: HectonMapMagicVegetationBridge
        private readonly List<long> _tileStateRemovalScratchKeys = new List<long>(16);
        private MapMagicTerrainTileSnapshot[] _startupBootstrapTiles = Array.Empty<MapMagicTerrainTileSnapshot>();

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
        private GraphicsBuffer _predatorFearNodeBuffer;
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
        private bool _originShiftListenerRegistered;
        private bool _activeSetDirty = true;
        private bool _insideLateFrameJobSwap;

        public static float GlobalVegetationAudioDensity { get; private set; }
        public static VegetationAcousticType GlobalVegetationAcousticType { get; private set; }
        public static bool GlobalArtificialInteriorActive { get; private set; }
        public static StructureType GlobalArtificialInteriorType { get; private set; }
        public static int GlobalArtificialInteriorId { get; private set; } = int.MinValue;
        public static Bounds GlobalArtificialInteriorBounds { get; private set; }
        public static Vector3 GlobalTotalUniverseOffset { get; private set; }
        public static double3 GlobalTotalUniverseOffsetDouble { get; private set; }
        internal static HectonMapMagicVegetationBridge ActiveRuntimeInstance => GlobalRegistry.MapMagicVegetation;
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
        private bool _artificialStructureHashSwapPending;
        private bool _threatSamplingChunkHashSwapPending;
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
        private bool _canopyGridInitialized;
        private bool _abyssalThermalGridInitialized;
        private bool _abyssalFlowVolumeInitialized;
        private bool _abyssalThermalGridScheduled;
        private bool _abyssalPathScheduled;
        private bool _hlodCullScheduled;
        private bool _poolDefragScheduled;
        private JobHandle _threatPropagationHandle;
        private JobHandle _flowFieldHandle;
        private JobHandle _abyssalThermalGridHandle;
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
        private float _swarmWakeImpulseExpireTime = float.NegativeInfinity;
        private Vector3 _canopyGridCenter;
        private Vector3 _abyssalThermalGridCenter;
        private Vector3 _scheduledAbyssalThermalGridCenter;
        private Vector3 _abyssalNavGraphOrigin;
        private float _lastThreatPropagationTime = float.NegativeInfinity;
        private float _currentThreatHotspotLevel;
        private Vector3 _currentThreatHotspotPosition;
        private Vector3 _externalThreatPulsePosition;
        private Vector3 _totalUniverseOffset;
        private double3 _totalUniverseOffsetDouble;
        private float _externalThreatPulseRadius;
        private float _externalThreatPulseStrength;
        private float _externalThreatPulseHoldTimer;
        private float _idleNativePoolTimer;
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
        private NativeArray<AbyssalPathTelemetryEntry> _abyssalPathTelemetry;
        private int _abyssalPathTelemetryCursor;
        private int _abyssalPathTelemetryWrittenCount;
        private uint _abyssalPathTelemetrySequence;
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

        private void Awake()
        {
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            RebuildFloraTemplateRuntimeDescriptors();
            if (_surfaceNativeBufferSource == null)
                _surfaceNativeBufferSource = new IndirectVegetationNativeBufferSource(this, false); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - surface native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

            if (_underwaterNativeBufferSource == null)
                _underwaterNativeBufferSource = new IndirectVegetationNativeBufferSource(this, true); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - underwater native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

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
            initialTerrainHoleCapacity = math.max(4, initialTerrainHoleCapacity);
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
            // COLD ALLOC: TerrainHoleRecord[initialTerrainHoleCapacity] - persistent cave-entrance suppression registry - owner: HectonMapMagicVegetationBridge
            _terrainHoleRecords = new TerrainHoleRecord[math.max(4, initialTerrainHoleCapacity)];
            // COLD ALLOC: TerrainHoleStreamingRecord[initialTerrainHoleCapacity] - terrain-hole streaming snapshot growth cache - owner: HectonMapMagicVegetationBridge
            _terrainHoleStreamingRecords = new TerrainHoleStreamingRecord[math.max(4, initialTerrainHoleCapacity)];
            PreallocateAbyssalNavigationBuffers();
            EnsureThreatSamplingChunkHashBuffersCapacity(FixedThreatSamplingHashCapacity);
            EnsureArtificialStructureHashBuffersCapacity(FixedArtificialStructureHashCapacity);
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
                GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);

                return;
            }

            _runtimeTeardownComplete = false;
            GlobalRegistry.RegisterMapMagicVegetationRuntime(this);
            InitializeChunkPools();
            PreallocateAbyssalNavigationBuffers();
            EnsureThreatSamplingChunkHashBuffersCapacity(FixedThreatSamplingHashCapacity);
            EnsureArtificialStructureHashBuffersCapacity(FixedArtificialStructureHashCapacity);
            InitializeLargeFloraCollisionProxyState();
            BindRendererSources();
            ResolveRuntimeDependencies();
            TrySubscribeEvents();
            TryRegister();
            QueueDeferredStartupWork();
        }

        private void Start()
        {
            if (!_runtimeLifecycleActive)
                return;

            BindRendererSources();
            TryWarmupLargeFloraCollisionProxyPool();
            TryRegister();
        }

        private void OnDisable()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);

                return;
            }

            CompleteThreatPropagationJob(forceComplete: true);
            CompleteFlowFieldJob(forceComplete: true);
            CompleteThermalGridJob(forceComplete: true);
            CompleteAbyssalPathJob(forceComplete: true);
            CompleteHLODCullJob(forceComplete: true);
            CompleteNativePoolDefragIfReady(forceComplete: true);
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
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
            DisposeLargeFloraCollisionProxyState();
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _nativeMemory.Dispose();
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            ResetDeferredStartupWork();
            GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);
            _runtimeTeardownComplete = true;
        }

        private void OnDestroy()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);

                return;
            }

            CompleteThreatPropagationJob(forceComplete: true);
            CompleteFlowFieldJob(forceComplete: true);
            CompleteThermalGridJob(forceComplete: true);
            CompleteAbyssalPathJob(forceComplete: true);
            CompleteHLODCullJob(forceComplete: true);
            CompleteNativePoolDefragIfReady(forceComplete: true);
            TryUnregister();
            TryUnsubscribeEvents();
            DisposeAllChunkBuildJobs();
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
            DisposeLargeFloraCollisionProxyState();
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _nativeMemory.Dispose();
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            _totalUniverseOffset = Vector3.zero;
            _totalUniverseOffsetDouble = double3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffsetDouble = double3.zero;
            GlobalRegistry.UnregisterMapMagicVegetationRuntime(this);
            _runtimeTeardownComplete = true;
            _runtimeLifecycleActive = false;
        }

        /// <summary>
        /// Polls in-flight chunk generation jobs and binds finished payloads without blocking the schedule path.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            TryDisposeDeferredTileCacheReadbacks();
            float clampedDt = math.max(0f, dt);
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

            if (TryProgressDeferredStartupWork())
                return;

            UpdatePlayerMotionState(dt);
            UpdateNativePoolDefragState(dt);
            UpdateLargeFloraCollisionProxies(clampedDt);

            if (TryValidateResidentTileCaches())
            {
                RefreshResidency();
                ScheduleHLODVisibilityCullJob();
                return;
            }

            if (_chunkBuildJobs.Count == 0 && !_activeSetDirty)
            {
                TryScheduleNativePoolDefrag();
                ScheduleHLODVisibilityCullJob();
                return;
            }

            if (_activeSetDirty)
            {
                if (RebuildAndBindActiveBuffers())
                    _activeSetDirty = false;
            }

            TryScheduleNativePoolDefrag();
            ScheduleHLODVisibilityCullJob();
        }

        /// <summary>
        /// Re-evaluates active chunk residency and incrementally scans missing virtual chunks.
        /// </summary>
        public void SlowTick()
        {
            TryDisposeDeferredTileCacheReadbacks();

            if (TryProgressDeferredStartupWork())
                return;

            ResolveRuntimeDependencies();
            TryWarmupLargeFloraCollisionProxyPool();
            RefreshResidency();
            SyncMegaWreckInteriorTerrainHoles();
            EvictDistantTerrainHoles();
            TryScheduleTerrainHoleJobs();
            RebuildHLODRegistrySnapshot();
            if (CanRefreshThreatSpatialSnapshots())
            {
                RebuildArtificialStructureThreatSnapshot();
                PrepareThreatSamplingSnapshot();
                CommitThreatSpatialSnapshotBufferSwaps();
                if (!_threatPropagationScheduled)
                    ScheduleThreatPropagationJob();
                if (!_flowFieldScheduled)
                    ScheduleFlowFieldJob();
                if (!_abyssalThermalGridScheduled)
                    ScheduleThermalGridJob();
            }
            UpdateVegetationAudioHandoff();
            LogNativePoolFragmentationIfDue();
        }

        /// <summary>
        /// Recovers completed vegetation jobs inside the dispatcher-owned late-frame barrier.
        /// </summary>
        public void LateFrameTick()
        {
            _insideLateFrameJobSwap = true;
            try
            {
                TryDisposeDeferredTileCacheReadbacks();
                int completedCount = FinalizeCompletedChunkBuilds();
                if (completedCount > 0)
                    EnforceChunkPoolMemoryGuard();

                bool selectionChanged = completedCount > 0 && SyncSelectedChunksFromDesired();
                if (completedCount > 0 || selectionChanged || _activeSetDirty)
                {
                    if (RebuildAndBindActiveBuffers())
                        _activeSetDirty = false;
                }

                CompleteAbyssalPathJob(forceComplete: false);
                CompleteHLODCullJob(forceComplete: false);
                CompleteNativePoolDefragIfReady(forceComplete: false);
                FinalizeCompletedTerrainHoleJobs();
                CompleteThreatPropagationJob(forceComplete: false);
                CompleteFlowFieldJob(forceComplete: false);
                CompleteThermalGridJob(forceComplete: false);
            }
            finally
            {
                _insideLateFrameJobSwap = false;
            }
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
        public NativeArray<Matrix4x4> ActiveSurfaceMatricesNative => _surfaceAggregateFrontBuffers.Matrices;

        /// <summary>Active surface metadata cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<HectonVegetationInstanceData> ActiveSurfaceMetadataNative => _surfaceAggregateFrontBuffers.Metadata;

        /// <summary>Active surface type cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<int> ActiveSurfaceTypesNative => _surfaceAggregateFrontBuffers.Types;

        /// <summary>Active surface semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int> ActiveSurfaceSemanticTypesNative => _surfaceAggregateFrontBuffers.SemanticTypes;

        /// <summary>Active surface biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte> ActiveSurfaceBiomeLayersNative => _surfaceAggregateFrontBuffers.BiomeLayers;

        /// <summary>Active surface flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2> ActiveSurfaceFlowDirectionsNative => _surfaceAggregateFrontBuffers.FlowDirections;

        /// <summary>Active surface 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3> ActiveSurfaceFlowVectorsNative => _surfaceAggregateFrontBuffers.FlowVectors;

        /// <summary>Active underwater matrix cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<Matrix4x4> ActiveUnderwaterMatricesNative => _underwaterAggregateFrontBuffers.Matrices;

        /// <summary>Accumulated floating-origin offset applied at render/query time instead of rewriting chunk-pool matrices.</summary>
        public Vector3 TotalUniverseOffset => _totalUniverseOffset;

        /// <summary>Accumulated vegetation universe offset in double precision for AUP reconstruction.</summary>
        public double3 TotalUniverseOffsetDouble => _totalUniverseOffsetDouble;

        /// <summary>Converts current runtime-local coordinates into stable universe coordinates.</summary>
        public static Vector3 ToUniverseSpace(Vector3 runtimePosition) => ToVector3(ToUniverseSpaceDouble3(runtimePosition));

        /// <summary>Converts current runtime-local coordinates into stable universe coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToUniverseSpaceDouble3(Vector3 runtimePosition)
        {
            return ToDouble3(runtimePosition) - GlobalTotalUniverseOffsetDouble;
        }

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates.</summary>
        public static Vector3 ToRuntimeSpace(Vector3 stableUniversePosition) => ToVector3(ToRuntimeSpaceDouble3(stableUniversePosition));

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates.</summary>
        public static Vector3 ToRuntimeSpace(double3 universePosition) => ToVector3(ToRuntimeSpaceDouble3(universePosition));

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToRuntimeSpaceDouble3(Vector3 stableUniversePosition)
        {
            return ToRuntimeSpaceDouble3(ToDouble3(stableUniversePosition));
        }

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates without reducing the bridge offset to float first.</summary>
        public static double3 ToRuntimeSpaceDouble3(double3 universePosition)
        {
            return universePosition + GlobalTotalUniverseOffsetDouble;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }

                HectonPlayerMovement movement = runtimeContext.PlayerMovement;
                if (movement != null)
                {
                    playerAup = movement.PredictedAup;
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                HectonPlayerMovement movement = playerContext.PlayerMovement;
                if (movement != null)
                {
                    playerAup = movement.PredictedAup;
                    return true;
                }
            }

            playerAup = default;
            return false;
        }

        private static bool TryResolvePlayerRuntimePositionFromAup(out Vector3 runtimePosition)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                runtimePosition = default;
                return false;
            }

            float3 resolved = playerAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(resolved)))
            {
                runtimePosition = default;
                return false;
            }

            runtimePosition = new Vector3(resolved.x, resolved.y, resolved.z);
            return true;
        }

        /// <summary>Active underwater metadata cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<HectonVegetationInstanceData> ActiveUnderwaterMetadataNative => _underwaterAggregateFrontBuffers.Metadata;

        /// <summary>Active underwater type cache in persistent native memory for direct GraphicsBuffer upload handoff.</summary>
        public NativeArray<int> ActiveUnderwaterTypesNative => _underwaterAggregateFrontBuffers.Types;

        /// <summary>Active underwater semantic-type cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<int> ActiveUnderwaterSemanticTypesNative => _underwaterAggregateFrontBuffers.SemanticTypes;

        /// <summary>Active underwater biome-layer cache in persistent native memory for AI/ocean handoff.</summary>
        public NativeArray<byte> ActiveUnderwaterBiomeLayersNative => _underwaterAggregateFrontBuffers.BiomeLayers;

        /// <summary>Active underwater flow-direction cache in persistent native memory for ocean/renderer handoff.</summary>
        public NativeArray<Vector2> ActiveUnderwaterFlowDirectionsNative => _underwaterAggregateFrontBuffers.FlowDirections;

        /// <summary>Active underwater 3D flow-vector cache in persistent native memory for abyssal current consumers.</summary>
        public NativeArray<Vector3> ActiveUnderwaterFlowVectorsNative => _underwaterAggregateFrontBuffers.FlowVectors;

        /// <summary>Active resident abyssal anchor positions for sonar/acoustic consumers.</summary>
        public Vector3[] ActiveAbyssalAnchors => _abyssalAnchorPositions;

        /// <summary>Active resident abyssal anchor positions in persistent native memory for direct readback.</summary>
        public NativeArray<Vector3> ActiveAbyssalAnchorsNative => _nativeMemory.AbyssalAnchorPositionsNative;

        /// <summary>Active resident abyssal anchor positions as AUP in persistent native memory for acoustic consumers.</summary>
        public NativeArray<AbsoluteUniversePosition> ActiveAbyssalAnchorAupsNative => _nativeMemory.AbyssalAnchorAupPositionsNative;

        /// <summary>Number of active surface instances.</summary>
        public int ActiveSurfaceInstanceCount => _surfaceFrontCount;

        /// <summary>Number of active underwater instances.</summary>
        public int ActiveUnderwaterInstanceCount => _underwaterFrontCount;

        /// <summary>Incremented whenever the surface active aggregate is rebuilt or cleared.</summary>
        public int ActiveSurfaceAggregateRevision => _surfaceActiveAggregateRevision;

        /// <summary>Incremented whenever the underwater active aggregate is rebuilt or cleared.</summary>
        public int ActiveUnderwaterAggregateRevision => _underwaterActiveAggregateRevision;

        /// <summary>Number of active resident abyssal anchors currently exported by the bridge.</summary>
        public int ActiveAbyssalAnchorCount => _abyssalAnchorCount;

        /// <summary>Immutable managed snapshot of the current abyssal safe-navigation nodes.</summary>
        public Vector3[] ActiveAbyssalNavNodes => _abyssalNavNodeSnapshot;

        /// <summary>Immutable native snapshot of the current abyssal safe-navigation nodes.</summary>
        public NativeArray<Vector3> ActiveAbyssalNavNodesNative => _nativeMemory.AbyssalNavNodeSnapshotNative;

        /// <summary>Number of active abyssal safe-navigation nodes currently exported by the bridge.</summary>
        public int ActiveAbyssalNavNodeCount => _abyssalNavNodeCount;

        /// <summary>Current ecosystem threat grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float> EcosystemThreatGrid => GetThreatGridFloatView();

        /// <summary>Compressed ecosystem threat grid used by AI/flow-field consumers that do not need float precision.</summary>
        public NativeArray<byte> EcosystemThreatGridCompressed => _nativeMemory.EcosystemThreatGridCompressedCurrentNative;

        /// <summary>Permanent threat-echo flags aligned to the compressed ecosystem threat grid. 1 means the cell never decays below the echo floor.</summary>
        public NativeArray<byte> EcosystemThreatEchoFlags => _nativeMemory.EcosystemThreatEchoCurrentNative;

        /// <summary>Current ecosystem threat grid resolution in cells along one axis.</summary>
        public int EcosystemThreatGridResolution => _ecosystemThreatGridResolution;

        /// <summary>Current ecosystem threat grid center in world space.</summary>
        public Vector3 EcosystemThreatGridCenter => _ecosystemThreatGridCenter;

        /// <summary>Current abyssal flow-field. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float2> EcosystemFlowField => _nativeMemory.EcosystemFlowFieldCurrentNative;

        /// <summary>Current abyssal flow-field center in world space.</summary>
        public Vector3 EcosystemFlowFieldCenter => _ecosystemFlowFieldCenter;

        /// <summary>Current abyssal thermal grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float> AbyssalThermalGrid => _nativeMemory.AbyssalThermalGridNative;

        /// <summary>Current 3D abyssal flow volume. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float3> AbyssalFlowVolume => _nativeMemory.AbyssalFlowVolumeCurrentNative;

        /// <summary>Current abyssal thermal-grid center in world space.</summary>
        public Vector3 AbyssalThermalGridCenter => _abyssalThermalGridCenter;

        /// <summary>Current mega-wreck section streaming payload. Treat as read-only and reacquire after each rebuild.</summary>
        public NativeArray<MegaWreckStreamSection> MegaWreckStreamSections => _nativeMemory.MegaWreckStreamSnapshotNative;

        /// <summary>Current immutable HLOD registry payload for large distant structures.</summary>
        public NativeArray<HLODData> HLODRegistry => _nativeMemory.HlodRegistrySnapshotNative;

        /// <summary>Current immutable visible HLOD payload after frustum and distance culling.</summary>
        public NativeArray<HLODData> VisibleHLODRegistry => _nativeMemory.VisibleHlodSnapshotNative;

        /// <summary>Current ecosystem threat hotspot level from the last completed propagation step.</summary>
        public float CurrentThreatHotspotLevel => _currentThreatHotspotLevel;

        /// <summary>Current ecosystem threat hotspot position from the last completed propagation step.</summary>
        public Vector3 CurrentThreatHotspotPosition => _currentThreatHotspotPosition;

        /// <summary>Latest native abyssal path result. Treat as read-only and reacquire after each completed path solve.</summary>
        public NativeArray<Vector3> ActiveAbyssalPathNative => _nativeMemory.AbyssalPathSnapshotNative;

        /// <summary>Number of valid waypoints in the latest completed abyssal path result.</summary>
        public int ActiveAbyssalPathCount => _abyssalPathCount;

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
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid surface instances written into the arrays.</returns>
        public int CopyActiveSurfaceInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return CopyActiveInstances(_surfaceAggregateFrontBuffers.Matrices, _surfaceAggregateFrontBuffers.Types, _surfaceFrontCount, ref matrices, ref types);
        }

        /// <summary>
        /// Copies the active underwater matrices and vegetation type ids into caller-owned arrays.
        /// </summary>
        /// <param name="matrices">Caller-owned matrix array that will be grown only on capacity miss.</param>
        /// <param name="types">Caller-owned vegetation type array that will be grown only on capacity miss.</param>
        /// <returns>Number of valid underwater instances written into the arrays.</returns>
        public int CopyActiveUnderwaterInstances(ref Matrix4x4[] matrices, ref int[] types)
        {
            return CopyActiveInstances(_underwaterAggregateFrontBuffers.Matrices, _underwaterAggregateFrontBuffers.Types, _underwaterFrontCount, ref matrices, ref types);
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
                _surfaceAggregateFrontBuffers.Matrices,
                _surfaceAggregateFrontBuffers.Metadata,
                _surfaceAggregateFrontBuffers.Types,
                _surfaceFrontCount,
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
                _underwaterAggregateFrontBuffers.Matrices,
                _underwaterAggregateFrontBuffers.Metadata,
                _underwaterAggregateFrontBuffers.Types,
                _underwaterFrontCount,
                ref matrices,
                ref metadata,
                ref types);
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
            matrices = _surfaceAggregateFrontBuffers.Matrices;
            metadata = _surfaceAggregateFrontBuffers.Metadata;
            types = _surfaceAggregateFrontBuffers.Types;
            count = _surfaceFrontCount;
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
            flowDirections = _surfaceAggregateFrontBuffers.FlowDirections;
            count = _surfaceFrontCount;
            return count > 0 && flowDirections.IsCreated;
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

            TouchTileCacheState(state);
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

        private static bool TryBuildHeightSamplePayload(TileRuntimeState state, out TerrainHeightSamplePayload payload)
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
            return payload.IsValid;
        }

        /// <summary>
        /// Returns the current surface semantic payload as native memory for AI and deep-biome consumers.
        /// </summary>
        public bool TryGetActiveSurfaceSemanticPayload(
            out NativeArray<int> semanticTypes,
            out NativeArray<byte> biomeLayers,
            out int count)
        {
            semanticTypes = _surfaceAggregateFrontBuffers.SemanticTypes;
            biomeLayers = _surfaceAggregateFrontBuffers.BiomeLayers;
            count = _surfaceFrontCount;
            return count > 0 && semanticTypes.IsCreated && biomeLayers.IsCreated;
        }

        /// <summary>
        /// Returns the current surface 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveSurfaceFlowVectorPayload(out NativeArray<Vector3> flowVectors, out int count)
        {
            flowVectors = _surfaceAggregateFrontBuffers.FlowVectors;
            count = _surfaceFrontCount;
            return count > 0 && flowVectors.IsCreated;
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
            matrices = _underwaterAggregateFrontBuffers.Matrices;
            metadata = _underwaterAggregateFrontBuffers.Metadata;
            types = _underwaterAggregateFrontBuffers.Types;
            count = _underwaterFrontCount;
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
            flowDirections = _underwaterAggregateFrontBuffers.FlowDirections;
            count = _underwaterFrontCount;
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
            semanticTypes = _underwaterAggregateFrontBuffers.SemanticTypes;
            biomeLayers = _underwaterAggregateFrontBuffers.BiomeLayers;
            count = _underwaterFrontCount;
            return count > 0 && semanticTypes.IsCreated && biomeLayers.IsCreated;
        }

        /// <summary>
        /// Returns the current underwater 3D flow-vector payload as native memory for ocean-current consumers.
        /// </summary>
        public bool TryGetActiveUnderwaterFlowVectorPayload(out NativeArray<Vector3> flowVectors, out int count)
        {
            flowVectors = _underwaterAggregateFrontBuffers.FlowVectors;
            count = _underwaterFrontCount;
            return count > 0 && flowVectors.IsCreated;
        }

        /// <summary>
        /// Returns the current resident abyssal-anchor positions as native memory for sonar/acoustic consumers.
        /// </summary>
        public bool TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int count)
        {
            anchors = _nativeMemory.AbyssalAnchorPositionsNative;
            count = _abyssalAnchorCount;
            return count > 0 && anchors.IsCreated;
        }

        /// <summary>
        /// Returns the current resident abyssal-anchor positions as AUP native memory for sonar/acoustic consumers.
        /// </summary>
        public bool TryGetActiveAbyssalAnchorAupPayload(out NativeArray<AbsoluteUniversePosition> anchors, out int count)
        {
            anchors = _nativeMemory.AbyssalAnchorAupPositionsNative;
            count = _abyssalAnchorCount;
            return count > 0 && anchors.IsCreated;
        }

        /// <summary>
        /// Returns the current immutable abyssal-nav-node snapshot as native memory for pathfinding consumers.
        /// </summary>
        public bool TryGetActiveAbyssalNavNodePayload(out NativeArray<Vector3> nodes, out int count)
        {
            nodes = _nativeMemory.AbyssalNavNodeSnapshotNative;
            count = _abyssalNavNodeCount;
            return count > 0 && nodes.IsCreated;
        }

        /// <summary>
        /// Returns the immutable current-conductor metadata aligned to the abyssal nav-node snapshot.
        /// </summary>
        public bool TryGetAbyssalCurrentConduitPayload(
            out NativeArray<Vector3> conduitVectors,
            out NativeArray<float> conduitStrengths,
            out int count)
        {
            conduitVectors = _nativeMemory.AbyssalNavConduitVectorsSnapshotNative;
            conduitStrengths = _nativeMemory.AbyssalNavConduitStrengthSnapshotNative;
            count = _abyssalNavNodeCount;
            return count > 0 &&
                   conduitVectors.IsCreated &&
                   conduitStrengths.IsCreated;
        }

        /// <summary>
        /// Returns the current native abyssal nav-graph payload, including immutable node snapshots and a spatial hash for fast nearest-node lookup.
        /// </summary>
        public bool TryGetNativeAbyssalNavGraph(
            out NativeArray<Vector3> nodes,
            out NativeArray<byte> nodeTypes,
            out NativeArray<Vector3> conduitVectors,
            out NativeArray<float> conduitStrengths,
            out NativeParallelMultiHashMap<int, int> spatialHash,
            out int count,
            out float cellSize,
            out Vector3 origin)
        {
            nodes = _nativeMemory.AbyssalNavNodeSnapshotNative;
            nodeTypes = _nativeMemory.AbyssalNavNodeTypesSnapshotNative;
            conduitVectors = _nativeMemory.AbyssalNavConduitVectorsSnapshotNative;
            conduitStrengths = _nativeMemory.AbyssalNavConduitStrengthSnapshotNative;
            spatialHash = _nativeMemory.AbyssalNavGraphHashNative;
            count = _abyssalNavNodeCount;
            cellSize = abyssalNavGraphCellSize;
            origin = _abyssalNavGraphOrigin;
            return count > 0 &&
                   nodes.IsCreated &&
                   nodeTypes.IsCreated &&
                   conduitVectors.IsCreated &&
                   conduitStrengths.IsCreated &&
                   spatialHash.IsCreated;
        }

        /// <summary>
        /// Returns the current ecosystem threat grid payload and metadata for external consumers.
        /// </summary>
        public bool TryGetEcosystemThreatGridPayload(
            out NativeArray<float> threatLevels,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            threatLevels = GetThreatGridFloatView();
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   threatLevels.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Returns the compressed ecosystem threat grid payload and metadata for low-cost AI consumers.
        /// </summary>
        public bool TryGetCompressedEcosystemThreatGridPayload(
            out NativeArray<byte> threatLevels,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            threatLevels = _nativeMemory.EcosystemThreatGridCompressedCurrentNative;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   threatLevels.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Returns the 3D byte voxel threat snapshot used by Burst DDA line-of-sight.
        /// Layout: [x + y * width + z * width * height].
        /// </summary>
        public bool TryGetEcosystemThreatVoxelPayload(
            out NativeArray<byte> threatVoxels,
            out Vector3Int gridDimensions,
            out Vector3 gridOrigin,
            out Vector3 voxelCellSize)
        {
            threatVoxels = _nativeMemory.EcosystemThreatVoxelCurrentNative;
            gridDimensions = new Vector3Int(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution);
            gridOrigin = _ecosystemThreatVoxelOrigin;
            voxelCellSize = new Vector3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize);
            return _threatGridInitialized &&
                   threatVoxels.IsCreated &&
                   gridDimensions.x > 0 &&
                   gridDimensions.y > 0 &&
                   gridDimensions.z > 0 &&
                   voxelCellSize.x > 0f &&
                   voxelCellSize.y > 0f &&
                   voxelCellSize.z > 0f;
        }

        /// <summary>
        /// Returns the permanent threat-echo flags aligned to the compressed ecosystem threat grid.
        /// </summary>
        public bool TryGetEcosystemThreatEchoPayload(
            out NativeArray<byte> echoFlags,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            echoFlags = _nativeMemory.EcosystemThreatEchoCurrentNative;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   echoFlags.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// Returns the current abyssal flow-field payload and metadata for external consumers.
        /// </summary>
        public bool TryGetMegaWreckStreamPayload(out NativeArray<MegaWreckStreamSection> sections, out int count)
        {
            sections = _nativeMemory.MegaWreckStreamSnapshotNative;
            count = _megaWreckStreamCount;
            return count > 0 && sections.IsCreated;
        }

        /// <summary>
        /// Returns the current HLOD registry payload for large persistent structures and mega-wreck silhouettes.
        /// </summary>
        public bool TryGetTerrainHoleStreamingPayload(out NativeArray<TerrainHoleStreamingRecord> holes, out int count)
        {
            holes = _nativeMemory.TerrainHoleStreamingRecordsNative;
            count = _terrainHoleCount;
            return count > 0 && holes.IsCreated;
        }

        /// <summary>
        /// Returns the current global canopy-height grid for audio and light-occlusion consumers.
        /// </summary>
        public bool TryGetCanopyHeightGridPayload(out NativeArray<float> canopyHeights, out int gridResolution, out Vector3 gridCenter, out float cellSize)
        {
            canopyHeights = _nativeMemory.CanopyHeightGridNative;
            gridResolution = _canopyGridResolution;
            gridCenter = _canopyGridCenter;
            cellSize = canopyGridCellSize;
            return _canopyGridInitialized &&
                   canopyHeights.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Returns the immutable abyssal-nav node classifications aligned to the active node snapshot.
        /// </summary>
        public bool TryGetActiveAbyssalNavNodeTypePayload(out NativeArray<byte> nodeTypes, out int count)
        {
            nodeTypes = _nativeMemory.AbyssalNavNodeTypesSnapshotNative;
            count = _abyssalNavNodeCount;
            return count > 0 && nodeTypes.IsCreated;
        }

        /// <summary>
        /// Returns the currently active artificial interior state that suppresses exterior biome effects while the player remains inside a streamed mega-wreck interior.
        /// </summary>
        public bool TryGetActiveArtificialInteriorState(out ArtificialInteriorState state)
        {
            state = _activeArtificialInteriorState;
            return state.IsActive;
        }
        /// Samples biomass density immediately on the main thread from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleBiomassDensityImmediate(Vector3 positionWS, int typeMask = DensityTypeMaskAll)
        {
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
                return 0f;

            if (!_nativeMemory.DensityQueryChunksNative.IsCreated || !_nativeMemory.DensityQueryGridNative.IsCreated || _densityQueryChunkCount <= 0)
                return 0f;

            float3 position = new float3(positionWS.x, positionWS.y, positionWS.z);
            return ApplyDensityTypeMask(
                SampleDensityChannelsAtPosition(position, _nativeMemory.DensityQueryChunksNative, _nativeMemory.DensityQueryGridNative, _densityQueryChunkCount),
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
                !_nativeMemory.AbyssalFlowVolumeCurrentNative.IsCreated ||
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
                _nativeMemory.AbyssalFlowVolumeCurrentNative.Length < expectedFlowVolumeLength)
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

            float3 sample000 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x0, y0, z0)];
            float3 sample100 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x1, y0, z0)];
            float3 sample010 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x0, y0, z1)];
            float3 sample110 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x1, y0, z1)];
            float3 sample001 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x0, y1, z0)];
            float3 sample101 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x1, y1, z0)];
            float3 sample011 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x0, y1, z1)];
            float3 sample111 = _nativeMemory.AbyssalFlowVolumeCurrentNative[GetThermalGridPhysicalIndex(x1, y1, z1)];
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
            if (_nativeMemory.DensityQueryChunksNative.IsCreated && _nativeMemory.DensityQueryGridNative.IsCreated && _densityQueryChunkCount > 0)
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    _nativeMemory.DensityQueryChunksNative,
                    _nativeMemory.DensityQueryGridNative,
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
            if (_nativeMemory.DensityQueryChunksNative.IsCreated && _nativeMemory.DensityQueryGridNative.IsCreated && _densityQueryChunkCount > 0)
            {
                densityChannels = SampleDensityChannelsAtPosition(
                    new float3(position.x, position.y, position.z),
                    _nativeMemory.DensityQueryChunksNative,
                    _nativeMemory.DensityQueryGridNative,
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
            if (!_nativeMemory.DensityQueryChunksNative.IsCreated ||
                !_nativeMemory.DensityQueryGridNative.IsCreated ||
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
                Chunks = _nativeMemory.DensityQueryChunksNative,
                DensityGrid = _nativeMemory.DensityQueryGridNative,
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
            if (!_nativeMemory.DensityQueryChunksNative.IsCreated ||
                !_nativeMemory.DensityQueryGridNative.IsCreated ||
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
                Chunks = _nativeMemory.DensityQueryChunksNative,
                DensityGrid = _nativeMemory.DensityQueryGridNative,
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

        private void EnsureThreatGridBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (!HasValidThreatGridConfiguration())
                return;

            if (!_nativeMemory.EcosystemThreatGridCurrentNative.IsCreated || _nativeMemory.EcosystemThreatGridCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCurrentNative);
                // COLD ALLOC: NativeArray<float>[_ecosystemThreatGridCellCount] - ecosystem threat-grid front buffer for read-only sampling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatGridCurrentNative = new NativeArray<float>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatGridCurrentNative, nameof(_nativeMemory.EcosystemThreatGridCurrentNative));
                _threatGridInitialized = false;
            }

            if (!_nativeMemory.EcosystemThreatGridNextNative.IsCreated || _nativeMemory.EcosystemThreatGridNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridNextNative);
                // COLD ALLOC: NativeArray<float>[_ecosystemThreatGridCellCount] - ecosystem threat-grid back buffer for diffusion writes - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatGridNextNative = new NativeArray<float>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatGridNextNative, nameof(_nativeMemory.EcosystemThreatGridNextNative));
            }

            if (!_nativeMemory.EcosystemThreatGridCompressedCurrentNative.IsCreated || _nativeMemory.EcosystemThreatGridCompressedCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCompressedCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - compressed threat-grid front mirror for low-cost consumers - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatGridCompressedCurrentNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatGridCompressedCurrentNative, nameof(_nativeMemory.EcosystemThreatGridCompressedCurrentNative));
            }

            if (!_nativeMemory.EcosystemThreatGridCompressedNextNative.IsCreated || _nativeMemory.EcosystemThreatGridCompressedNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCompressedNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - compressed threat-grid back mirror for diffusion writes - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatGridCompressedNextNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatGridCompressedNextNative, nameof(_nativeMemory.EcosystemThreatGridCompressedNextNative));
            }

            if (!_nativeMemory.EcosystemThreatVoxelCurrentNative.IsCreated || _nativeMemory.EcosystemThreatVoxelCurrentNative.Length != _ecosystemThreatVoxelCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatVoxelCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatVoxelCellCount] - 3D byte voxel threat snapshot front buffer used by AI DDA line-of-sight - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatVoxelCurrentNative = new NativeArray<byte>(_ecosystemThreatVoxelCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatVoxelCurrentNative, nameof(_nativeMemory.EcosystemThreatVoxelCurrentNative));
            }

            if (!_nativeMemory.EcosystemThreatVoxelNextNative.IsCreated || _nativeMemory.EcosystemThreatVoxelNextNative.Length != _ecosystemThreatVoxelCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatVoxelNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatVoxelCellCount] - 3D byte voxel threat snapshot back buffer written by Burst voxelization - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatVoxelNextNative = new NativeArray<byte>(_ecosystemThreatVoxelCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatVoxelNextNative, nameof(_nativeMemory.EcosystemThreatVoxelNextNative));
            }

            if (!_nativeMemory.EcosystemThreatEchoCurrentNative.IsCreated || _nativeMemory.EcosystemThreatEchoCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatEchoCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - permanent threat-echo flags aligned to the active threat grid - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatEchoCurrentNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatEchoCurrentNative, nameof(_nativeMemory.EcosystemThreatEchoCurrentNative));
            }

            if (!_nativeMemory.EcosystemThreatEchoNextNative.IsCreated || _nativeMemory.EcosystemThreatEchoNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.EcosystemThreatEchoNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - back buffer for threat-echo propagation/shift - owner: HectonMapMagicVegetationBridge
                _nativeMemory.EcosystemThreatEchoNextNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.EcosystemThreatEchoNextNative, nameof(_nativeMemory.EcosystemThreatEchoNextNative));
            }
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

        private void EnsureCanopyGridBuffer()
        {
            if (_canopyGridCellCount <= 0)
                InitializeCanopyGridMetadata();

            if (!_nativeMemory.CanopyHeightGridNative.IsCreated || _nativeMemory.CanopyHeightGridNative.Length != _canopyGridCellCount)
            {
                DisposeNativeArray(ref _nativeMemory.CanopyHeightGridNative);
                // COLD ALLOC: NativeArray<float>[_canopyGridCellCount] - global canopy-height mask for audio/light roof queries - owner: HectonMapMagicVegetationBridge
                _nativeMemory.CanopyHeightGridNative = new NativeArray<float>(_canopyGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_nativeMemory.CanopyHeightGridNative, nameof(_nativeMemory.CanopyHeightGridNative));
                _canopyGridInitialized = false;
            }
        }

        private void PrepareThreatSamplingSnapshot()
        {
            _threatSamplingChunkCount = 0;
            if (_densityQueryChunkCount <= 0 ||
                !_nativeMemory.DensityQueryChunksNative.IsCreated ||
                !_nativeMemory.ThreatAttractorGridNative.IsCreated)
            {
                EnsureThreatSamplingChunkHashBuffersCapacity(1);
                _nativeMemory.ThreatSamplingChunkHashBackNative.Clear();
                _threatSamplingChunkHashSwapPending = true;

                return;
            }

            _threatSamplingChunkCount = _densityQueryChunkCount;
            EnsureDensityChunkRecordCapacity(ref _nativeMemory.ThreatSamplingChunksNative, _threatSamplingChunkCount);
            EnsureFloat2NativeCapacity(ref _nativeMemory.ThreatSamplingAttractorGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            NativeArray<VegetationDensityChunkRecord>.Copy(_nativeMemory.DensityQueryChunksNative, _nativeMemory.ThreatSamplingChunksNative, _threatSamplingChunkCount);
            NativeArray<float2>.Copy(_nativeMemory.ThreatAttractorGridNative, _nativeMemory.ThreatSamplingAttractorGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            bool hasPlayerRuntimePosition = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition);
            Vector3 hashCenter = _threatGridInitialized
                ? _ecosystemThreatGridCenter
                : (hasPlayerRuntimePosition ? playerRuntimePosition : Vector3.zero);
            RebuildThreatSamplingChunkHash(hashCenter);
        }

        private void RebuildThreatSamplingChunkHash(Vector3 gridCenter)
        {
            if (_threatSamplingChunkCount <= 0 ||
                !_nativeMemory.ThreatSamplingChunksNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter))
            {
                EnsureThreatSamplingChunkHashBuffersCapacity(1);
                _nativeMemory.ThreatSamplingChunkHashBackNative.Clear();
                _threatSamplingChunkHashSwapPending = true;

                return;
            }

            int hashCapacity = 0;
            for (int i = 0; i < _threatSamplingChunkCount; i++)
                hashCapacity += EstimateThreatSamplingChunkHashEntries(_nativeMemory.ThreatSamplingChunksNative[i], gridCenter);

            if (!EnsureThreatSamplingChunkHashBuffersCapacity(hashCapacity))
            {
                _threatSamplingChunkHashSwapPending = true;
                return;
            }

            for (int i = 0; i < _threatSamplingChunkCount; i++)
                StampThreatSamplingChunkHash(_nativeMemory.ThreatSamplingChunksNative[i], gridCenter, i);
            _threatSamplingChunkHashSwapPending = true;
        }

        private int EstimateThreatSamplingChunkHashEntries(VegetationDensityChunkRecord chunk, Vector3 gridCenter)
        {
            if (_ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter) ||
                !math.isfinite(chunk.MinX) ||
                !math.isfinite(chunk.MaxX) ||
                !math.isfinite(chunk.MinZ) ||
                !math.isfinite(chunk.MaxZ))
            {
                return 0;
            }

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = math.max(chunk.MinX, minGridX);
            float maxX = math.min(chunk.MaxX, maxGridX);
            float minZ = math.max(chunk.MinZ, minGridZ);
            float maxZ = math.min(chunk.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return 0;

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int minCellX = math.clamp((int)math.floor((minX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = math.clamp((int)math.floor((maxX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = math.clamp((int)math.floor((minZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = math.clamp((int)math.floor((maxZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            return math.max(0, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));
        }

        private void StampThreatSamplingChunkHash(VegetationDensityChunkRecord chunk, Vector3 gridCenter, int chunkIndex)
        {
            if (!_nativeMemory.ThreatSamplingChunkHashBackNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter) ||
                !math.isfinite(chunk.MinX) ||
                !math.isfinite(chunk.MaxX) ||
                !math.isfinite(chunk.MinZ) ||
                !math.isfinite(chunk.MaxZ))
            {
                return;
            }

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = math.max(chunk.MinX, minGridX);
            float maxX = math.min(chunk.MaxX, maxGridX);
            float minZ = math.max(chunk.MinZ, minGridZ);
            float maxZ = math.min(chunk.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return;

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int minCellX = math.clamp((int)math.floor((minX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = math.clamp((int)math.floor((maxX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = math.clamp((int)math.floor((minZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = math.clamp((int)math.floor((maxZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _ecosystemThreatGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    _nativeMemory.ThreatSamplingChunkHashBackNative.Add(rowOffset + cellX, chunkIndex);
            }
        }

        private void RebuildArtificialStructureThreatSnapshot()
        {
            Vector3 targetCenter = TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition)
                ? playerRuntimePosition
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);

            int targetCount = _persistentArtificialStructures.Count + _megaWreckStreamCount;
            if (targetCount <= 0)
            {
                _artificialStructureCount = 0;
                EnsureArtificialStructureHashBuffersCapacity(1);
                _nativeMemory.ArtificialStructureHashBackNative.Clear();
                _artificialStructureHashSwapPending = true;

                return;
            }

            EnsureNativeCapacity(ref _nativeMemory.ArtificialStructureRecordsNative, targetCount);
            int estimatedHashEntries = 0;
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
                estimatedHashEntries += EstimateArtificialStructureHashEntries(_persistentArtificialStructures[i].Bounds, targetCenter);

            for (int i = 0; i < _megaWreckStreamCount; i++)
                estimatedHashEntries += EstimateArtificialStructureHashEntries(GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]), targetCenter);

            bool hashReady = EnsureArtificialStructureHashBuffersCapacity(estimatedHashEntries);

            int writeIndex = 0;
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                WriteArtificialStructureRecord(structure.Bounds, structure.Type, targetCenter, writeIndex, hashReady);
                writeIndex++;
            }

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                WriteArtificialStructureRecord(
                    GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]),
                    StructureType.MegaWreck,
                    targetCenter,
                    writeIndex,
                    hashReady);
                writeIndex++;
            }

            _artificialStructureCount = writeIndex;
            _artificialStructureHashSwapPending = true;
        }

        private void WriteArtificialStructureRecord(Bounds bounds, StructureType type, Vector3 gridCenter, int writeIndex, bool stampHash)
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
            _nativeMemory.ArtificialStructureRecordsNative[writeIndex] = record;
            if (stampHash)
                StampArtificialStructureHash(record, gridCenter, writeIndex);
        }

        private int EstimateArtificialStructureHashEntries(Bounds bounds, Vector3 gridCenter)
        {
            if (_ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter) ||
                !IsFinite(bounds.min) ||
                !IsFinite(bounds.max))
            {
                return 0;
            }

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = math.max(bounds.min.x, minGridX);
            float maxX = math.min(bounds.max.x, maxGridX);
            float minZ = math.max(bounds.min.z, minGridZ);
            float maxZ = math.min(bounds.max.z, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return 0;

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int minCellX = math.clamp((int)math.floor((minX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = math.clamp((int)math.floor((maxX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = math.clamp((int)math.floor((minZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = math.clamp((int)math.floor((maxZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            return math.max(0, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));
        }

        private void StampArtificialStructureHash(ArtificialStructureRecord record, Vector3 gridCenter, int recordIndex)
        {
            if (!_nativeMemory.ArtificialStructureHashBackNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter) ||
                !math.isfinite(record.MinX) ||
                !math.isfinite(record.MaxX) ||
                !math.isfinite(record.MinZ) ||
                !math.isfinite(record.MaxZ))
            {
                return;
            }

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = math.max(record.MinX, minGridX);
            float maxX = math.min(record.MaxX, maxGridX);
            float minZ = math.max(record.MinZ, minGridZ);
            float maxZ = math.min(record.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return;

            float inverseThreatGridCellSize = math.rcp(threatGridCellSize);
            int minCellX = math.clamp((int)math.floor((minX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = math.clamp((int)math.floor((maxX - minGridX) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = math.clamp((int)math.floor((minZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = math.clamp((int)math.floor((maxZ - minGridZ) * inverseThreatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _ecosystemThreatGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    _nativeMemory.ArtificialStructureHashBackNative.Add(rowOffset + cellX, recordIndex);
            }
        }

        private bool EnsureThreatSamplingChunkHashBuffersCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (!_nativeMemory.ThreatSamplingChunkHashFrontNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[FixedThreatSamplingHashCapacity] - fixed threat-sampling chunk spatial hash front buffer for Burst readers - owner: HectonMapMagicVegetationBridge
                _nativeMemory.ThreatSamplingChunkHashFrontNative = new NativeParallelMultiHashMap<int, int>(FixedThreatSamplingHashCapacity, Allocator.Persistent);
                RegisterTrackedNativeParallelMultiHashMap(_nativeMemory.ThreatSamplingChunkHashFrontNative, nameof(_nativeMemory.ThreatSamplingChunkHashFrontNative));
            }

            if (!_nativeMemory.ThreatSamplingChunkHashBackNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[FixedThreatSamplingHashCapacity] - fixed threat-sampling chunk spatial hash back buffer for SlowTick rebuilds - owner: HectonMapMagicVegetationBridge
                _nativeMemory.ThreatSamplingChunkHashBackNative = new NativeParallelMultiHashMap<int, int>(FixedThreatSamplingHashCapacity, Allocator.Persistent);
                RegisterTrackedNativeParallelMultiHashMap(_nativeMemory.ThreatSamplingChunkHashBackNative, nameof(_nativeMemory.ThreatSamplingChunkHashBackNative));
            }

            _nativeMemory.ThreatSamplingChunkHashBackNative.Clear();
            if (_nativeMemory.ThreatSamplingChunkHashBackNative.Capacity < safeCapacity)
            {
                return false;
            }

            return true;
        }

        private bool EnsureArtificialStructureHashBuffersCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (!_nativeMemory.ArtificialStructureHashFrontNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[FixedArtificialStructureHashCapacity] - fixed artificial-structure threat hash front buffer for Burst readers - owner: HectonMapMagicVegetationBridge
                _nativeMemory.ArtificialStructureHashFrontNative = new NativeParallelMultiHashMap<int, int>(FixedArtificialStructureHashCapacity, Allocator.Persistent);
                RegisterTrackedNativeParallelMultiHashMap(_nativeMemory.ArtificialStructureHashFrontNative, nameof(_nativeMemory.ArtificialStructureHashFrontNative));
            }

            if (!_nativeMemory.ArtificialStructureHashBackNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[FixedArtificialStructureHashCapacity] - fixed artificial-structure threat hash back buffer for SlowTick rebuilds - owner: HectonMapMagicVegetationBridge
                _nativeMemory.ArtificialStructureHashBackNative = new NativeParallelMultiHashMap<int, int>(FixedArtificialStructureHashCapacity, Allocator.Persistent);
                RegisterTrackedNativeParallelMultiHashMap(_nativeMemory.ArtificialStructureHashBackNative, nameof(_nativeMemory.ArtificialStructureHashBackNative));
            }

            _nativeMemory.ArtificialStructureHashBackNative.Clear();
            if (_nativeMemory.ArtificialStructureHashBackNative.Capacity < safeCapacity)
            {
                return false;
            }

            return true;
        }

        private void SwapThreatSamplingChunkHashBuffers()
        {
            NativeParallelMultiHashMap<int, int> hashSwap = _nativeMemory.ThreatSamplingChunkHashFrontNative;
            _nativeMemory.ThreatSamplingChunkHashFrontNative = _nativeMemory.ThreatSamplingChunkHashBackNative;
            _nativeMemory.ThreatSamplingChunkHashBackNative = hashSwap;
        }

        private void SwapArtificialStructureHashBuffers()
        {
            NativeParallelMultiHashMap<int, int> hashSwap = _nativeMemory.ArtificialStructureHashFrontNative;
            _nativeMemory.ArtificialStructureHashFrontNative = _nativeMemory.ArtificialStructureHashBackNative;
            _nativeMemory.ArtificialStructureHashBackNative = hashSwap;
        }

        private void CommitThreatSpatialSnapshotBufferSwaps()
        {
            if (!CanRefreshThreatSpatialSnapshots())
                return;

            if (_artificialStructureHashSwapPending)
            {
                SwapArtificialStructureHashBuffers();
                _artificialStructureHashSwapPending = false;
            }

            if (_threatSamplingChunkHashSwapPending)
            {
                SwapThreatSamplingChunkHashBuffers();
                _threatSamplingChunkHashSwapPending = false;
            }
        }

        private void RefreshArtificialStructureSnapshotIfIdle()
        {
            if (!CanRefreshThreatSpatialSnapshots())
                return;

            RebuildArtificialStructureThreatSnapshot();
            CommitThreatSpatialSnapshotBufferSwaps();
        }

        private void GetThreatGridBounds(Vector3 gridCenter, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float halfExtent = (_ecosystemThreatGridResolution - 1) * 0.5f * threatGridCellSize;
            minX = gridCenter.x - halfExtent;
            maxX = gridCenter.x + halfExtent;
            minZ = gridCenter.z - halfExtent;
            maxZ = gridCenter.z + halfExtent;
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

            _chunkMegaWreckPayloads[key] = payload;
        }

        private void RemoveChunkMegaWreckPayload(ChunkKey key)
        {
            _chunkMegaWreckPayloads.Remove(key);
        }

        /// Applies a world-space origin offset to cached bounds and metadata only.
        /// Vegetation instance matrices stay in local chunk space and are shifted on GPU.
        /// </summary>
        public void ApplyWorldOffsetToAllChunks(Vector3 offset)
        {
            ApplyWorldOffsetToAllChunks(offset, _totalUniverseOffsetDouble - ToDouble3(offset));
        }

        private void ApplyWorldOffsetToAllChunks(Vector3 offset, double3 newTotalUniverseOffsetDouble)
        {
            if (offset.sqrMagnitude <= 0.000001f)
                return;

            Vector3 appliedOffset = -offset;
            DisposeAllChunkBuildJobs();
            CompleteThreatPropagationJob(forceComplete: true);
            CompleteFlowFieldJob(forceComplete: true);
            CompleteThermalGridJob(forceComplete: true);
            CompleteAbyssalPathJob(forceComplete: true);
            CompleteHLODCullJob(forceComplete: true);
            _totalUniverseOffsetDouble = math.all(math.isfinite(newTotalUniverseOffsetDouble))
                ? newTotalUniverseOffsetDouble
                : _totalUniverseOffsetDouble + ToDouble3(appliedOffset);
            _totalUniverseOffset = ToVector3(_totalUniverseOffsetDouble);
            GlobalTotalUniverseOffset = _totalUniverseOffset;
            GlobalTotalUniverseOffsetDouble = _totalUniverseOffsetDouble;
            RebaseLargeFloraCollisionProxyRuntimePositions();

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
                _evictionKeys.Add(payloadEnumerator.Current.Key);

            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                ShiftChunkPayloadBounds(ref payload, appliedOffset);
                _chunkPayloads[key] = payload;
            }

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

            if (_persistentArtificialStructures.Count > 0)
            {
                for (int i = 0; i < _persistentArtificialStructures.Count; i++)
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
            if (_activeArtificialInteriorState.IsActive)
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

            _activeSetDirty = true;
            RefreshResidency();
        }

        void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!isActiveAndEnabled || !snapshot.IsValid)
                return;

            ResolveRuntimeDependencies();
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

            ResolveRuntimeDependencies();
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
            Debug.Log(
                $"[HectonMapMagicVegetationBridge] NativePoolFragmentationPercent={NativePoolFragmentationPercent:0.0} UsedBytes={_chunkPayloadUsedBytes} GuardBytes={ChunkPayloadGuardBytes}",
                this);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogLoopGuardHit(string loopName, int maxIterations)
        {
#if UNITY_EDITOR
            Debug.LogError($"[HectonMapMagicVegetationBridge] Loop guard hit: {loopName} after {maxIterations} iterations.");
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

        private static Matrix4x4 ApplyMatrixTranslationOffset(Matrix4x4 matrix, Vector3 offset)
        {
            matrix.m03 += offset.x;
            matrix.m13 += offset.y;
            matrix.m23 += offset.z;
            return matrix;
        }

        private static Matrix4x4 ConvertMatrixToStableUniverseSpace(Matrix4x4 matrix, double3 universeOffset)
        {
            return ApplyMatrixTranslationOffset(matrix, ToVector3(-universeOffset));
        }

        private Vector3 ResolveRuntimePosition(Matrix4x4 matrix)
        {
            double3 runtimePosition = new double3(matrix.m03, matrix.m13, matrix.m23) + _totalUniverseOffsetDouble;
            return ToVector3(runtimePosition);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static NativeArray<JobInstanceRecord> AllocateJobRecordArray(int count)
        {
            if (count <= 0)
                return default;

            NativeArray<JobInstanceRecord> records = new NativeArray<JobInstanceRecord>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterTrackedNativeArray(records, nameof(JobInstanceRecord));
            return records;
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

            jobState.CancelRequested = true;
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
            {
                ChunkKey key = _jobScratchKeys[i];
                if (!_chunkBuildJobs.TryGetValue(key, out ChunkBuildJobState jobState) || jobState == null)
                    continue;

                DispatcherJobSwap.TryComplete(ref jobState.Handle, forceComplete: true);
                DisposeJobState(jobState);
                _chunkBuildJobs.Remove(key);
            }
        }

        private static void DisposeJobState(ChunkBuildJobState jobState)
        {
            if (jobState == null)
                return;

            DisposeNativeArray(ref jobState.GrassRecords, jobState.Handle);
            DisposeNativeArray(ref jobState.FloatingRecords, jobState.Handle);
            DisposeNativeArray(ref jobState.KelpRecords, jobState.Handle);
            jobState.Handle = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (dependency.IsCompleted)
                array.Dispose();
            else
                array.Dispose(dependency);

            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency, string label = null)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            if (!string.IsNullOrEmpty(label))
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);

            if (dependency.IsCompleted)
                list.Dispose();
            else
                list.Dispose(dependency);

            list = default;
        }

        private static void RegisterTrackedNativeList<T>(NativeList<T> list, string label)
            where T : unmanaged
        {
            NativeMemorySentinel.RegisterNativeList(list, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> map,
            JobHandle dependency,
            string label = null)
            where TKey : unmanaged
            , IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            if (!string.IsNullOrEmpty(label))
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner, label);

            if (dependency.IsCompleted)
                map.Dispose();
            else
                map.Dispose(dependency);

            map = default;
        }

        private static void RegisterTrackedNativeParallelMultiHashMap<TKey, TValue>(
            NativeParallelMultiHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged
            , IEquatable<TKey>
            where TValue : unmanaged
        {
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(map, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static JobHandle CombineOptionalHandles(JobHandle current, JobHandle next)
        {
            if (current.Equals(default))
                return next;

            if (next.Equals(default))
                return current;

            return JobHandle.CombineDependencies(current, next);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                {
                    IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
                    _cachedViewCamera = playerContext != null ? playerContext.PlayerCamera : null;
                }
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
                {
                    if (priority >= _pendingChunkPriorities[i])
                        return;

                    DequeuePendingChunkAt(i);
                    break;
                }
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
                RemoveChunkAbyssalNavPayload(_evictionKeys[i]);
                RemoveChunkMegaWreckPayload(_evictionKeys[i]);
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

            EnsureMatrixCapacity(ref matrices, count);
            EnsureIntCapacity(ref types, count);
            for (int i = 0; i < count; i++)
                matrices[i] = ApplyMatrixTranslationOffset(sourceMatrices[i], _totalUniverseOffset);
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

            EnsureMatrixCapacity(ref matrices, count);
            EnsureVegetationDataCapacity(ref metadata, count);
            EnsureIntCapacity(ref types, count);
            for (int i = 0; i < count; i++)
                matrices[i] = ApplyMatrixTranslationOffset(sourceMatrices[i], _totalUniverseOffset);
            CopyNativeToManaged(sourceMetadata, 0, metadata, 0, count);
            CopyNativeToManaged(sourceTypes, 0, types, 0, count);
            return count;
        }

        private static void EnsureActiveAggregateBufferCapacity(ref ActiveAggregateNativeBufferSet buffers, int requiredCount)
        {
            EnsureInactiveNativeCapacity(ref buffers.Matrices, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.Metadata, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.Types, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.SemanticTypes, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.BiomeLayers, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.FlowDirections, requiredCount);
            EnsureInactiveNativeCapacity(ref buffers.FlowVectors, requiredCount);
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
                default,
                out readBuffer);
        }

        private static bool TryAcquireNativeReadBuffer(
            ActiveAggregateNativeBufferSet buffers,
            int count,
            int bufferIndex,
            Bounds drawBounds,
            bool hasExplicitBounds,
            JobHandle producerHandle,
            out HectonIndirectVegetationNativeReadBuffer readBuffer)
        {
            if (count <= 0 ||
                !buffers.Matrices.IsCreated ||
                !buffers.Metadata.IsCreated ||
                buffers.Matrices.Length < count ||
                buffers.Metadata.Length < count)
            {
                readBuffer = default;
                return false;
            }

            readBuffer = new HectonIndirectVegetationNativeReadBuffer(
                buffers.Matrices,
                buffers.Metadata,
                count,
                bufferIndex,
                producerHandle,
                hasExplicitBounds,
                drawBounds);
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

        private static void DisposeActiveAggregateBufferSet(ref ActiveAggregateNativeBufferSet buffers)
        {
            buffers.Dispose();
        }

        private bool TryBootstrapExistingTiles()
        {
            if (_startupBootstrapTileCount > 0 || _startupBootstrapTileCursor > 0)
                return true;

            if (mapMagicBridge == null)
                return false;

            EnsureStartupBootstrapTileSnapshotCapacity(64);
            _startupBootstrapTileCount = mapMagicBridge.CopyTerrainTileSnapshotsTo(_startupBootstrapTiles);
            while (_startupBootstrapTileCount >= _startupBootstrapTiles.Length && _startupBootstrapTiles.Length < TileCacheLruCapacity * 4)
            {
                EnsureStartupBootstrapTileSnapshotCapacity(_startupBootstrapTiles.Length * 2);
                _startupBootstrapTileCount = mapMagicBridge.CopyTerrainTileSnapshotsTo(_startupBootstrapTiles);
            }

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

            ResolveRuntimeDependencies();

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
            if (_startupBootstrapTileCount <= 0 || _startupBootstrapTiles == null)
                return 0;

            int safeBatchSize = math.max(1, batchSize);
            int processedCount = 0;
            while (_startupBootstrapTileCursor < _startupBootstrapTileCount &&
                   processedCount < safeBatchSize)
            {
                MapMagicTerrainTileSnapshot snapshot = _startupBootstrapTiles[_startupBootstrapTileCursor++];
                if (!snapshot.IsValid || IsForeignTile(in snapshot))
                    continue;

                UpsertTileState(in snapshot);
                processedCount++;
            }

            return processedCount;
        }

        private void ReleaseDeferredStartupTileSnapshot()
        {
            _startupBootstrapTileCount = 0;
            _startupBootstrapTileCursor = 0;
        }

        private void EnsureStartupBootstrapTileSnapshotCapacity(int requiredCapacity)
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (_startupBootstrapTiles != null && _startupBootstrapTiles.Length >= safeCapacity)
                return;

            _startupBootstrapTiles = new MapMagicTerrainTileSnapshot[safeCapacity]; // COLD ALLOC: MapMagicTerrainTileSnapshot[startup] - deferred plugin tile snapshot bootstrap cache - owner: HectonMapMagicVegetationBridge
        }

        private void UpsertTileState(in MapMagicTerrainTileSnapshot snapshot)
        {
            if (!snapshot.IsValid)
                return;

            UnityEngine.Terrain terrain = snapshot.Terrain;
            if (terrain == null || terrain.terrainData == null)
            {
                RemoveTileState(snapshot.TileX, snapshot.TileZ);
                return;
            }

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (!TryResolveLayerIndices(terrainData, out LayerIndices indices))
            {
                RemoveTileState(snapshot.TileX, snapshot.TileZ);
                return;
            }

            long tileKey = PackTileCoord(snapshot.TileX, snapshot.TileZ);
            int oldChunkCountX = 0;
            int oldChunkCountZ = 0;
            if (_tileStates.TryGetValue(tileKey, out TileRuntimeState existingState) && existingState != null)
            {
                oldChunkCountX = existingState.ChunkCountX;
                oldChunkCountZ = existingState.ChunkCountZ;
            }

            TileRuntimeState state = existingState ?? new TileRuntimeState();
            state.TileX = snapshot.TileX;
            state.TileZ = snapshot.TileZ;
            state.Terrain = terrain;
            state.TerrainData = terrainData;
            RefreshTerrainTextureCaches(state, terrainData);
            state.TerrainPosition = terrain.GetPosition();
            state.TerrainSize = terrainData.size;
            state.AlphamapResolution = terrainData.alphamapResolution;
            state.HeightmapResolution = terrainData.heightmapResolution;
            state.HolesResolution = terrainData.holesResolution;
            state.ChunkCountX = math.max(1, (int)math.ceil(state.TerrainSize.x / DefaultVirtualChunkSize));
            state.ChunkCountZ = math.max(1, (int)math.ceil(state.TerrainSize.z / DefaultVirtualChunkSize));
            state.LayerIndices = indices;
            EnsureTileTerrainHoleMaskCapacity(state);
            MarkTileTerrainHolesDirty(state);

            InvalidateTileChunks(snapshot.TileX, snapshot.TileZ, math.max(oldChunkCountX, state.ChunkCountX), math.max(oldChunkCountZ, state.ChunkCountZ));
            CacheTileMasks(state, terrainData);
            _tileStates[tileKey] = state;
        }

        private void ResolveRuntimeDependencies()
        {
            if (mapMagicBridge == null)
                mapMagicBridge = GlobalRegistry.MapMagic;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform != null && (_playerRigidbody == null || _playerRigidbody.transform != playerTransform))
                playerTransform.TryGetComponent(out _playerRigidbody);
        }

        private void UpdatePlayerMotionState(float dt)
        {
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 currentPosition))
            {
                _playerVelocity = Vector3.zero;
                _hasLastPlayerPosition = false;
                return;
            }

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


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _isRegistered =
                GlobalRegistry.Updatables.Contains(this) &&
                GlobalRegistry.SlowTickables.Contains(this) &&
                SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
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

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            ApplyWorldOffsetToAllChunks(shiftData.ShiftOffset, -shiftData.NewTotalOffsetDouble);
            if (_predatorFearNodeCount > 0)
            {
                float3 offset = -shiftData.ShiftOffset;
                for (int i = 0; i < _predatorFearNodeCount; i++)
                {
                    PredatorFearNodeState node = _predatorFearNodes[i];
                    node.Position += offset;
                    _predatorFearNodes[i] = node;
                }

                if (!_abyssalPathScheduled)
                    SyncPredatorFearNodeSnapshot(_predatorFearSimulationTime);
            }
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
                _corruptedChunkKeys.Clear();
                _corruptedChunkOrder.Clear();
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
                if (_nativeMemory.AbyssalPathRawResultNative.IsCreated)
                    _nativeMemory.AbyssalPathRawResultNative.Clear();
                if (_nativeMemory.AbyssalPathResultNative.IsCreated)
                    _nativeMemory.AbyssalPathResultNative.Clear();
            }
        }

        private void InitializeChunkPools()
        {
            if (_surfaceChunkPool.Matrices.IsCreated && _underwaterChunkPool.Matrices.IsCreated)
                return;

            int totalBudgetBytes = math.max(MinimumNativePoolBudgetMb, nativePoolBudgetMb) * 1024 * 1024;
            int totalCapacity = math.max(1024, totalBudgetBytes / ChunkPoolBytesPerInstance);
            int surfaceCapacity = math.clamp((int)math.round(totalCapacity * surfacePoolShare), 1024, totalCapacity - 1);
            int underwaterCapacity = math.max(1024, totalCapacity - surfaceCapacity);
            InitializeChunkPool(ref _surfaceChunkPool, surfaceCapacity, ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount);
            InitializeChunkPool(ref _underwaterChunkPool, underwaterCapacity, ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount);
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
                Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
                while (enumerator.MoveNext())
                    ReleaseChunkPayloadStorage(enumerator.Current.Value);
            }

            DisposeAllChunkAbyssalNavPayloads();
            _chunkPayloads.Clear();
            _chunkAbyssalNavPayloads.Clear();
            _chunkMegaWreckPayloads.Clear();
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
            RegisterTrackedNativeArray(pool.Matrices, nameof(pool.Matrices));
            RegisterTrackedNativeArray(pool.Metadata, nameof(pool.Metadata));
            RegisterTrackedNativeArray(pool.Types, nameof(pool.Types));
            RegisterTrackedNativeArray(pool.SemanticTypes, nameof(pool.SemanticTypes));
            RegisterTrackedNativeArray(pool.BiomeLayers, nameof(pool.BiomeLayers));
            RegisterTrackedNativeArray(pool.EdgeDistances, nameof(pool.EdgeDistances));
            RegisterTrackedNativeArray(pool.FlowDirections, nameof(pool.FlowDirections));
            RegisterTrackedNativeArray(pool.FlowVectors, nameof(pool.FlowVectors));
            pool.Capacity = capacity;
            EnsurePoolBlockCapacity(ref freeBlocks, 1);
            freeBlocks[0] = new PoolBlock { Offset = 0, Length = capacity };
            freeBlockCount = 1;
        }

        private static void DisposeChunkPool(ref NativeChunkPool pool)
        {
            pool.Dispose();
        }

        private void DisposeActiveNativeAggregates()
        {
            DisposeActiveAggregateBufferSet(ref _surfaceAggregateFrontBuffers);
            DisposeActiveAggregateBufferSet(ref _surfaceAggregateBackBuffers);
            DisposeActiveAggregateBufferSet(ref _underwaterAggregateFrontBuffers);
            DisposeActiveAggregateBufferSet(ref _underwaterAggregateBackBuffers);
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
            DisposeNativeArray(ref _nativeMemory.AbyssalAnchorPositionsNative);
            DisposeNativeArray(ref _nativeMemory.AbyssalAnchorAupPositionsNative);
            DisposeNativeArray(ref _nativeMemory.AbyssalNavNodeSnapshotNative);
            DisposeNativeArray(ref _nativeMemory.AbyssalNavConduitVectorsSnapshotNative);
            DisposeNativeArray(ref _nativeMemory.AbyssalNavConduitStrengthSnapshotNative);
            DisposeNativeArray(ref _nativeMemory.AbyssalNavNodeTypesSnapshotNative);
            DisposeNativeArray(ref _nativeMemory.MegaWreckStreamSnapshotNative);
            DisposeNativeParallelMultiHashMap(ref _nativeMemory.AbyssalNavGraphHashNative, default, nameof(_nativeMemory.AbyssalNavGraphHashNative));
            DisposeNativeList(ref _nativeMemory.AbyssalNavNodes, default, nameof(_nativeMemory.AbyssalNavNodes));
            _abyssalAnchorCount = 0;
            _abyssalNavNodeCount = 0;
            _megaWreckStreamCount = 0;
            _abyssalNavGraphOrigin = Vector3.zero;
        }

        private void DisposeDensityQuerySnapshot()
        {
            DisposeNativeArray(ref _nativeMemory.DensityQueryChunksNative);
            DisposeNativeArray(ref _nativeMemory.DensityQueryGridNative);
            DisposeNativeArray(ref _nativeMemory.ThreatAttractorGridNative);
            DisposeNativeArray(ref _nativeMemory.DensityQueryChunksScratchNative);
            DisposeNativeArray(ref _nativeMemory.DensityQueryGridScratchNative);
            DisposeNativeArray(ref _nativeMemory.ThreatAttractorGridScratchNative);
            _densityQueryChunkCount = 0;
            _densityQueryChunkLookup.Clear();
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
            EnsureTileNativeCacheBufferCapacity(state, writeBufferIndex, sampleCount, heightSampleCount);
            RefreshTerrainTextureCaches(state, terrainData);
            CaptureTileCacheSignature(
                state.AlphamapTextureCache,
                state.HeightTextureCache,
                out state.AlphamapTextureCount,
                out state.CombinedAlphamapHash,
                out state.CombinedAlphamapUpdateCount,
                out state.HeightmapHash,
                out state.HeightmapUpdateCount);

            Texture2D[] alphamapTextures = state.AlphamapTextureCache;
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
                        sandMask += SampleTerrainLayerMask(alphamapTextures, state.LayerIndices.Sand, writeIndex);
                    if (state.LayerIndices.GreenSand >= 0)
                        sandMask += SampleTerrainLayerMask(alphamapTextures, state.LayerIndices.GreenSand, writeIndex);

                    float rockMask = 0f;
                    if (state.LayerIndices.Rock >= 0)
                        rockMask = SampleTerrainLayerMask(alphamapTextures, state.LayerIndices.Rock, writeIndex);

                    writeBuffer.SandMaskNative[writeIndex] = PackMask01(sandMask);
                    writeBuffer.RockMaskNative[writeIndex] = PackMask01(rockMask);
                    writeIndex++;
                }
            }

            Texture heightTexture = state.HeightTextureCache;
            if (heightTexture == null)
                return false;

            terrainData.SyncHeightmap();
            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(ref writeBuffer.HeightSamplesNative, heightTexture, 0, TextureFormat.R16);
            state.PendingCacheBufferIndex = writeBufferIndex;
            state.HeightReadbackRequest = request;
            state.HeightReadbackPending = true;
            if (writeBufferIndex == 0)
                state.PrimaryCacheBuffer = writeBuffer;
            else
                state.SecondaryCacheBuffer = writeBuffer;

            return false;
        }

        private static float SampleTerrainLayerMask(Texture2D[] alphamapTextures, int layerIndex, int sampleIndex)
        {
            if (layerIndex < 0 || alphamapTextures == null)
                return 0f;

            int textureIndex = layerIndex >> 2;
            if ((uint)textureIndex >= (uint)alphamapTextures.Length)
                return 0f;

            Texture2D texture = alphamapTextures[textureIndex];
            if (texture == null || !texture.isReadable)
                return 0f;

            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            if (!pixels.IsCreated || (uint)sampleIndex >= (uint)pixels.Length)
                return 0f;

            Color32 pixel = pixels[sampleIndex];
            byte channel = (byte)((layerIndex & 3) switch
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

        private static void RefreshTerrainTextureCaches(TileRuntimeState state, UnityEngine.TerrainData terrainData)
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
            ReleaseBuffer(ref _predatorFearNodeBuffer);
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
            JobHandle disposeHandle = default;
            if (_threatPropagationScheduled)
                disposeHandle = _threatPropagationHandle;
            if (_flowFieldScheduled)
                disposeHandle = CombineOptionalHandles(disposeHandle, _flowFieldHandle);
            if (_abyssalThermalGridScheduled)
                disposeHandle = CombineOptionalHandles(disposeHandle, _abyssalThermalGridHandle);
            if (_abyssalPathScheduled)
                disposeHandle = CombineOptionalHandles(disposeHandle, _abyssalPathHandle);

            DisposeNativeArray(ref _nativeMemory.ThreatSamplingChunksNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.ThreatSamplingAttractorGridNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridNextNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCompressedCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatGridCompressedNextNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatVoxelCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatVoxelNextNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatEchoCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemThreatEchoNextNative, disposeHandle);
            DisposeNativeParallelMultiHashMap(ref _nativeMemory.ThreatSamplingChunkHashFrontNative, disposeHandle, nameof(_nativeMemory.ThreatSamplingChunkHashFrontNative));
            DisposeNativeParallelMultiHashMap(ref _nativeMemory.ThreatSamplingChunkHashBackNative, default, nameof(_nativeMemory.ThreatSamplingChunkHashBackNative));

            _threatSamplingChunkCount = 0;
            _threatPropagationHandle = default;
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
            _threatSamplingChunkHashSwapPending = false;
        }

        private void DisposeFlowFieldState()
        {
            JobHandle disposeHandle = _flowFieldScheduled ? _flowFieldHandle : default;
            DisposeNativeArray(ref _nativeMemory.FlowSamplingDensityGridNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.FlowNavSupportGridNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemFlowFieldCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.EcosystemFlowFieldNextNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.SwarmWakeImpulseNative, disposeHandle);
            _flowFieldHandle = default;
            _flowFieldScheduled = false;
            _flowFieldInitialized = false;
            _swarmWakeImpulseCount = 0;
            _swarmWakeImpulseExpireTime = float.NegativeInfinity;
            _ecosystemFlowFieldCenter = Vector3.zero;
            _scheduledFlowFieldCenter = Vector3.zero;
        }

        private void DisposeCanopyGridState()
        {
            DisposeNativeArray(ref _nativeMemory.CanopyHeightGridNative);
            _canopyGridInitialized = false;
            _canopyGridCenter = Vector3.zero;
        }

        private void DisposeThermalGridState()
        {
            JobHandle disposeHandle = _abyssalThermalGridScheduled ? _abyssalThermalGridHandle : default;
            DisposeNativeArray(ref _nativeMemory.AbyssalThermalGridNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalThermalGridNextNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalFlowVolumeCurrentNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalFlowVolumeNextNative, disposeHandle);
            _abyssalThermalGridHandle = default;
            _abyssalThermalGridScheduled = false;
            _abyssalThermalGridInitialized = false;
            _abyssalFlowVolumeInitialized = false;
            _abyssalThermalGridCenter = Vector3.zero;
            _scheduledAbyssalThermalGridCenter = Vector3.zero;
            _abyssalThermalGridRingOffsetX = 0;
            _abyssalThermalGridRingOffsetY = 0;
            _abyssalThermalGridRingOffsetZ = 0;
        }

        private void DisposeArtificialStructureSnapshot()
        {
            DisposeNativeArray(ref _nativeMemory.ArtificialStructureRecordsNative);
            JobHandle disposeHandle = default;
            if (_threatPropagationScheduled)
                disposeHandle = _threatPropagationHandle;
            if (_abyssalPathScheduled)
                disposeHandle = CombineOptionalHandles(disposeHandle, _abyssalPathHandle);

            DisposeNativeParallelMultiHashMap(ref _nativeMemory.ArtificialStructureHashFrontNative, disposeHandle, nameof(_nativeMemory.ArtificialStructureHashFrontNative));
            DisposeNativeParallelMultiHashMap(ref _nativeMemory.ArtificialStructureHashBackNative, default, nameof(_nativeMemory.ArtificialStructureHashBackNative));

            _artificialStructureCount = 0;
            _artificialStructureHashSwapPending = false;
        }

        private void DisposePoolDefragState()
        {
            JobHandle surfaceDisposeHandle = _poolDefragScheduled && _surfaceDefragMoveCount > 0
                ? _surfacePoolDefragHandle
                : default;
            JobHandle underwaterDisposeHandle = _poolDefragScheduled && _underwaterDefragMoveCount > 0
                ? _underwaterPoolDefragHandle
                : default;
            DisposeNativeArray(ref _nativeMemory.SurfaceDefragMovesNative, surfaceDisposeHandle);
            DisposeNativeArray(ref _nativeMemory.UnderwaterDefragMovesNative, underwaterDisposeHandle);
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
            if (_chunkMegaWreckPayloads.Count <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkMegaWreckPayload>.Enumerator enumerator = _chunkMegaWreckPayloads.GetEnumerator();
            while (enumerator.MoveNext())
                _evictionKeys.Add(enumerator.Current.Key);

            for (int keyIndex = 0; keyIndex < _evictionKeys.Count; keyIndex++)
            {
                ChunkKey key = _evictionKeys[keyIndex];
                if (!_chunkMegaWreckPayloads.TryGetValue(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                for (int sectionIndex = 0; sectionIndex < payload.Count; sectionIndex++)
                {
                    MegaWreckStreamSection section = payload.Sections[sectionIndex];
                    section.WorldCenter += offset;
                    payload.Sections[sectionIndex] = section;
                }

                _chunkMegaWreckPayloads[key] = payload;
            }
        }

        private void ShiftMegaWreckSnapshot(Vector3 offset)
        {
            if (_megaWreckStreamCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                section.WorldCenter += offset;
                _megaWreckStreamSnapshot[i] = section;
                if (_nativeMemory.MegaWreckStreamSnapshotNative.IsCreated && i < _nativeMemory.MegaWreckStreamSnapshotNative.Length)
                    _nativeMemory.MegaWreckStreamSnapshotNative[i] = section;
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
            if (!_nativeMemory.CanopyHeightGridNative.IsCreated || _canopyGridResolution <= 0 || canopyGridCellSize <= 0f)
                return float.NegativeInfinity;

            float halfExtent = (_canopyGridResolution - 1) * 0.5f * canopyGridCellSize;
            float localX = worldX - (_canopyGridCenter.x - halfExtent);
            float localZ = worldZ - (_canopyGridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return float.NegativeInfinity;

            int cellX = math.clamp((int)math.round(localX / canopyGridCellSize), 0, _canopyGridResolution - 1);
            int cellZ = math.clamp((int)math.round(localZ / canopyGridCellSize), 0, _canopyGridResolution - 1);
            return _nativeMemory.CanopyHeightGridNative[(cellZ * _canopyGridResolution) + cellX];
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
