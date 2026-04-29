using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using MapMagic.Products;
using MapMagic.Terrains;
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
    public sealed class HectonMapMagicVegetationBridge : MonoBehaviour, ITickable, ISlowTickable, IOriginShiftListener
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
        private const float DefaultKelpMinHeight = 4600f;
        private const float DefaultVirtualChunkSize = 100f;
        private const float CameraResolveRetryInterval = 1f;
        private const float CacheValidationInterval = 0.5f;
        private const int CacheValidationTileBudget = 2;
        private const int StartupBootstrapTileBatchSize = 2;
        private const int DefaultJobBatchSize = 32;
        private const int InitialTileCapacity = 32;
        private const int TileCacheLruCapacity = 64;
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
        private const float DefaultThreatGridRadius = 1000f;
        private const float DefaultThreatGridCellSize = 10f;
        private const int DefaultPredatorFearNodeCapacity = 32;
        private const float DefaultPredatorFearSectorSizeMeters = 1000f;
        private const float DefaultPredatorFearLifetimeSeconds = 900f;
        private const float DefaultAbyssalNavGraphCellSize = 64f;
        private const int InvalidTerrainHoleId = 0;
        private const float DefaultTerrainHoleEvictionDistance = 3000f;
        private const float DefaultThermalGridRadius = 1000f;
        private const float DefaultThermalGridHorizontalCellSize = 50f;
        private const float DefaultThermalGridVerticalCellSize = 250f;
        private const float BiolumeSurgeDurationSeconds = 4f;
        private const float BiolumeSurgeVelocityDeltaThreshold = 8f;
        private const float DefaultThermalGridDepthMeters = 4000f;
        private const float AbyssalFlowNoiseStartDepthMeters = 2000f;
        private const int MaxTileCacheLruIterations = 512;
        private const int MaxChunkPoolEvictionIterations = 2048;
        private const int MaxPathReconstructionIterations = 4096;
        private const int MaxHeapRebalanceIterations = 4096;
        private const int MaxThreatDdaSteps = 4096;
        private const int MaxPathCompactionIterations = 4096;
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
            public TerrainTile Tile;
            public Terrain Terrain;
            public TerrainData TerrainData;
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

        private struct ActiveAggregateNativeBufferSet
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;
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
        private readonly HashSet<ChunkKey> _corruptedChunkKeys = new HashSet<ChunkKey>();
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
        private TerrainTile[] _startupBootstrapTiles = Array.Empty<TerrainTile>();

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
        private bool _activeSetDirty = true;

        public static float GlobalVegetationAudioDensity { get; private set; }
        public static VegetationAcousticType GlobalVegetationAcousticType { get; private set; }
        public static bool GlobalArtificialInteriorActive { get; private set; }
        public static StructureType GlobalArtificialInteriorType { get; private set; }
        public static int GlobalArtificialInteriorId { get; private set; } = int.MinValue;
        public static Bounds GlobalArtificialInteriorBounds { get; private set; }
        public static Vector3 GlobalTotalUniverseOffset { get; private set; }
        internal static HectonMapMagicVegetationBridge ActiveRuntimeInstance => _activeRuntimeInstance;

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
        private NativeArray<VegetationDensityChunkRecord> _densityQueryChunksNative;
        private NativeArray<float3> _densityQueryGridNative;
        private NativeArray<float2> _threatAttractorGridNative;
        private NativeArray<VegetationDensityChunkRecord> _densityQueryChunksScratchNative;
        private NativeArray<float3> _densityQueryGridScratchNative;
        private NativeArray<float2> _threatAttractorGridScratchNative;
        private NativeArray<VegetationDensityChunkRecord> _threatSamplingChunksNative;
        private NativeArray<float2> _threatSamplingAttractorGridNative;
        private NativeArray<float3> _flowSamplingDensityGridNative;
        private NativeArray<float> _flowNavSupportGridNative;
        private NativeArray<float> _ecosystemThreatGridCurrentNative;
        private NativeArray<float> _ecosystemThreatGridNextNative;
        private NativeArray<byte> _ecosystemThreatGridCompressedCurrentNative;
        private NativeArray<byte> _ecosystemThreatGridCompressedNextNative;
        private NativeArray<byte> _ecosystemThreatVoxelCurrentNative;
        private NativeArray<byte> _ecosystemThreatVoxelNextNative;
        private NativeArray<byte> _ecosystemThreatEchoCurrentNative;
        private NativeArray<byte> _ecosystemThreatEchoNextNative;
        private NativeArray<float2> _ecosystemFlowFieldCurrentNative;
        private NativeArray<float2> _ecosystemFlowFieldNextNative;
        private NativeArray<SwarmWakeImpulse> _swarmWakeImpulseNative;
        private NativeArray<float> _abyssalThermalGridNative;
        private NativeArray<float> _abyssalThermalGridNextNative;
        private NativeArray<float3> _abyssalFlowVolumeCurrentNative;
        private NativeArray<float3> _abyssalFlowVolumeNextNative;
        private NativeArray<float> _canopyHeightGridNative;
        private NativeArray<TerrainHoleRecord> _terrainHoleRecordsNative;
        private NativeArray<TerrainHoleStreamingRecord> _terrainHoleStreamingRecordsNative;
        private NativeArray<ArtificialStructureRecord> _artificialStructureRecordsNative;
        private NativeParallelMultiHashMap<int, int> _artificialStructureHashFrontNative;
        private NativeParallelMultiHashMap<int, int> _artificialStructureHashBackNative;
        private NativeParallelMultiHashMap<int, int> _threatSamplingChunkHashFrontNative;
        private NativeParallelMultiHashMap<int, int> _threatSamplingChunkHashBackNative;
        private bool _artificialStructureHashSwapPending;
        private bool _threatSamplingChunkHashSwapPending;
        private Vector3[] _abyssalAnchorPositions = Array.Empty<Vector3>();
        private NativeArray<Vector3> _abyssalAnchorPositionsNative;
        private Vector3[] _abyssalNavNodeSnapshot = Array.Empty<Vector3>();
        private NativeArray<Vector3> _abyssalNavNodeSnapshotNative;
        private NativeArray<Vector3> _abyssalNavConduitVectorsSnapshotNative;
        private NativeArray<float> _abyssalNavConduitStrengthSnapshotNative;
        private NativeArray<byte> _abyssalNavNodeTypesSnapshotNative;
        private NativeParallelMultiHashMap<int, int> _abyssalNavGraphHashNative;
        private NativeList<Vector3> _abyssalNavNodes;
        private Vector3[] _abyssalPathSnapshot = Array.Empty<Vector3>();
        private NativeArray<Vector3> _abyssalPathSnapshotNative;
        private NativeList<Vector3> _abyssalPathRawResultNative;
        private NativeList<Vector3> _abyssalPathResultNative;
        private NativeArray<int> _abyssalPathParentsNative;
        private NativeArray<float> _abyssalPathGScoreNative;
        private NativeArray<float> _abyssalPathFScoreNative;
        private NativeArray<byte> _abyssalPathClosedFlagsNative;
        private NativeArray<int> _abyssalPathHeapNodesNative;
        private NativeArray<int> _abyssalPathHeapPositionsNative;
        private NativeArray<PredatorFearNodeSnapshot> _predatorFearNodesSnapshotNative;
        private HLODData[] _hlodRegistrySnapshot = Array.Empty<HLODData>();
        private HLODData[] _visibleHlodSnapshot = Array.Empty<HLODData>();
        private NativeArray<HLODData> _hlodRegistrySnapshotNative;
        private NativeArray<HLODData> _visibleHlodSnapshotNative;
        private NativeArray<byte> _hlodVisibleFlagsNative;
        private NativeArray<float4> _hlodFrustumPlanesNative;
        private NativeChunkPool _surfaceDefragScratchPool;
        private NativeChunkPool _underwaterDefragScratchPool;
        private NativeArray<ChunkSliceMoveRecord> _surfaceDefragMovesNative;
        private NativeArray<ChunkSliceMoveRecord> _underwaterDefragMovesNative;
        private NativeArray<ActiveAggregateCopyRecord> _surfaceAggregateCopyRecordsNative;
        private NativeArray<ActiveAggregateCopyRecord> _underwaterAggregateCopyRecordsNative;
        private MegaWreckStreamSection[] _megaWreckStreamSnapshot = Array.Empty<MegaWreckStreamSection>();
        private NativeArray<MegaWreckStreamSection> _megaWreckStreamSnapshotNative;
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
        private static HectonMapMagicVegetationBridge _activeRuntimeInstance;

        private void Awake()
        {
            _totalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            if (_surfaceNativeBufferSource == null)
                _surfaceNativeBufferSource = new IndirectVegetationNativeBufferSource(this, false); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - surface native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

            if (_underwaterNativeBufferSource == null)
                _underwaterNativeBufferSource = new IndirectVegetationNativeBufferSource(this, true); // COLD ALLOC: IndirectVegetationNativeBufferSource[1] - underwater native vegetation renderer seam - owner: HectonMapMagicVegetationBridge

            residentRadius = Mathf.Clamp(residentRadius, 150f, 200f);
            residentHysteresisScale = Mathf.Clamp(residentHysteresisScale, 1f, 1.5f);
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
            initialTerrainHoleCapacity = Mathf.Max(4, initialTerrainHoleCapacity);
            nativePoolDefragIdleSeconds = Mathf.Max(1f, nativePoolDefragIdleSeconds);
            nativePoolDefragThresholdPercent = Mathf.Clamp(nativePoolDefragThresholdPercent, 1f, 100f);
            nativePoolDefragIdleSpeedThreshold = Mathf.Max(0.01f, nativePoolDefragIdleSpeedThreshold);
            abyssalPathSmoothingSampleSpacing = Mathf.Max(0.5f, abyssalPathSmoothingSampleSpacing);
            abyssalPathSmoothingObstacleThreshold = Mathf.Clamp01(abyssalPathSmoothingObstacleThreshold);
            abyssalPathSmoothingKelpWeight = Mathf.Max(0f, abyssalPathSmoothingKelpWeight);
            abyssalPathSmoothingSargassumWeight = Mathf.Max(0f, abyssalPathSmoothingSargassumWeight);
            abyssalPathSmoothingMaxSamples = Mathf.Clamp(abyssalPathSmoothingMaxSamples, 8, 256);
            hlodMinimumDistance = Mathf.Max(1f, hlodMinimumDistance);
            hlodMaximumDistance = Mathf.Max(hlodMinimumDistance, hlodMaximumDistance);
            hlodMinimumStructureSize = Mathf.Max(1f, hlodMinimumStructureSize);
            hlodFrustumPadding = Mathf.Max(0f, hlodFrustumPadding);
            canopyGridRadius = Mathf.Max(100f, canopyGridRadius);
            canopyGridCellSize = Mathf.Max(1f, canopyGridCellSize);
            canopySargassumThickness = Mathf.Max(0f, canopySargassumThickness);
            canopyStructureThickness = Mathf.Max(0f, canopyStructureThickness);
            threatWhirlpoolThreshold = Mathf.Clamp01(threatWhirlpoolThreshold);
            threatWhirlpoolRadius = Mathf.Max(1f, threatWhirlpoolRadius);
            threatWhirlpoolStrength = Mathf.Max(0f, threatWhirlpoolStrength);
            threatGridRadius = DefaultThreatGridRadius;
            threatGridCellSize = DefaultThreatGridCellSize;
            threatDiffusion = Mathf.Clamp01(threatDiffusion);
            threatDecayPerSecond = Mathf.Max(0.01f, threatDecayPerSecond);
            threatNoiseDepositPerSecond = Mathf.Max(0f, threatNoiseDepositPerSecond);
            threatFlashlightDepositPerSecond = Mathf.Max(0f, threatFlashlightDepositPerSecond);
            threatPulseDepositPerSecond = Mathf.Max(0f, threatPulseDepositPerSecond);
            threatEmissionRadiusMin = Mathf.Max(1f, threatEmissionRadiusMin);
            threatEmissionRadiusMax = Mathf.Max(threatEmissionRadiusMin, threatEmissionRadiusMax);
            threatSargassumRetentionBoost = Mathf.Clamp01(threatSargassumRetentionBoost);
            threatTechnoJungleRetentionBoost = Mathf.Clamp01(threatTechnoJungleRetentionBoost);
            threatSargassumAccumulationBoost = Mathf.Max(0f, threatSargassumAccumulationBoost);
            threatTechnoJungleAccumulationBoost = Mathf.Max(0f, threatTechnoJungleAccumulationBoost);
            flowFieldThreatBias = Mathf.Max(0f, flowFieldThreatBias);
            flowFieldPlayerBias = Mathf.Max(0f, flowFieldPlayerBias);
            flowFieldHotspotBias = Mathf.Max(0f, flowFieldHotspotBias);
            flowFieldObstacleAvoidBias = Mathf.Max(0f, flowFieldObstacleAvoidBias);
            flowFieldNavSupportBias = Mathf.Max(0f, flowFieldNavSupportBias);
            flowFieldKelpObstacleWeight = Mathf.Max(0f, flowFieldKelpObstacleWeight);
            flowFieldSargassumObstacleWeight = Mathf.Max(0f, flowFieldSargassumObstacleWeight);
            flowFieldTechnoObstacleWeight = Mathf.Max(0f, flowFieldTechnoObstacleWeight);
            flowFieldObstacleSoftThreshold = Mathf.Clamp01(flowFieldObstacleSoftThreshold);
            flowFieldObstacleHardThreshold = Mathf.Clamp(flowFieldObstacleHardThreshold, flowFieldObstacleSoftThreshold, 1f);
            flowFieldHotspotMinimumThreat = Mathf.Clamp01(flowFieldHotspotMinimumThreat);
            flowFieldNavStencilRadiusCells = Mathf.Clamp(flowFieldNavStencilRadiusCells, 0, 3);
            artificialStructureThreatSuppression = Mathf.Clamp01(artificialStructureThreatSuppression);
            artificialStructureHazardAttraction = Mathf.Clamp01(artificialStructureHazardAttraction);
            abyssalNavNodeStepMeters = Mathf.Max(4f, abyssalNavNodeStepMeters);
            abyssalNavNodeHoverHeight = Mathf.Max(0.5f, abyssalNavNodeHoverHeight);
            abyssalNavNodeObstacleRadius = Mathf.Max(1f, abyssalNavNodeObstacleRadius);
            abyssalNavNodeObstacleVerticalWindow = Mathf.Max(0.5f, abyssalNavNodeObstacleVerticalWindow);
            abyssalNavNodeMaxObstacleDensity = Mathf.Max(0f, abyssalNavNodeMaxObstacleDensity);
            abyssalNavNodeMaxCurrentMagnitude = Mathf.Max(0f, abyssalNavNodeMaxCurrentMagnitude);
            abyssalNavNodeMinimumDeepAffinity = Mathf.Max(0f, abyssalNavNodeMinimumDeepAffinity);
            abyssalPathNeighborRadius = Mathf.Max(4f, abyssalPathNeighborRadius);
            abyssalNavGraphCellSize = Mathf.Max(4f, abyssalNavGraphCellSize);
            abyssalPathVerticalTolerance = Mathf.Max(1f, abyssalPathVerticalTolerance);
            abyssalPathThreatPenaltyWeight = Mathf.Max(0f, abyssalPathThreatPenaltyWeight);
            abyssalConduitStartDepth = Mathf.Max(0f, abyssalConduitStartDepth);
            abyssalConduitMinimumFlowMagnitude = Mathf.Max(0f, abyssalConduitMinimumFlowMagnitude);
            abyssalConduitVerticalToleranceBonus = Mathf.Max(0f, abyssalConduitVerticalToleranceBonus);
            abyssalConduitMisalignmentPenalty = Mathf.Max(0f, abyssalConduitMisalignmentPenalty);
            abyssalConduitAlignmentReward = Mathf.Clamp01(abyssalConduitAlignmentReward);
            abyssalPathRetargetDistance = Mathf.Max(0f, abyssalPathRetargetDistance);
            abyssalPathMaxExpandedNodes = Mathf.Clamp(abyssalPathMaxExpandedNodes, 64, 8192);
            abyssalInteriorTraversalCostMultiplier = Mathf.Clamp(abyssalInteriorTraversalCostMultiplier, 1f, 2f);
            permanentThreatEchoFloor = Mathf.Clamp01(permanentThreatEchoFloor);
            permanentThreatEchoThreshold = Mathf.Clamp(permanentThreatEchoThreshold, Mathf.Max(0.3f, permanentThreatEchoFloor), 1f);
            predatorSpawnThreatBonusMultiplier = Mathf.Clamp(predatorSpawnThreatBonusMultiplier, 0f, 3f);
            predatorFearNodeCapacity = Mathf.Clamp(predatorFearNodeCapacity, 4, 128);
            predatorFearLifetimeSeconds = Mathf.Max(120f, predatorFearLifetimeSeconds);
            predatorFearSectorSizeMeters = Mathf.Max(100f, predatorFearSectorSizeMeters);
            predatorFearNodeRadiusMeters = Mathf.Max(1f, predatorFearNodeRadiusMeters);
            predatorFearPathPenaltyWeight = Mathf.Clamp(predatorFearPathPenaltyWeight, 0f, 4f);
            predatorFearCognitionPressureScale = Mathf.Clamp01(predatorFearCognitionPressureScale);
            permanentEchoTechnoJungleThresholdBias = Mathf.Clamp01(permanentEchoTechnoJungleThresholdBias);
            permanentEchoDeadZoneKeepBoost = Mathf.Clamp01(permanentEchoDeadZoneKeepBoost);
            thermalGridRadius = Mathf.Max(100f, thermalGridRadius);
            thermalGridHorizontalCellSize = Mathf.Max(5f, thermalGridHorizontalCellSize);
            thermalGridVerticalCellSize = Mathf.Max(10f, thermalGridVerticalCellSize);
            thermalGridDepthMeters = Mathf.Max(500f, thermalGridDepthMeters);
            thermalDepthFalloffExponent = Mathf.Max(0.25f, thermalDepthFalloffExponent);
            thermalThermoclineDepth = Mathf.Clamp(thermalThermoclineDepth, 0f, thermalGridDepthMeters);
            thermalHotPocketBoostCelsius = Mathf.Max(0f, thermalHotPocketBoostCelsius);
            thermalHotPocketNoiseScale = Mathf.Max(0.0001f, thermalHotPocketNoiseScale);
            thermalHotPocketThreshold = Mathf.Clamp01(thermalHotPocketThreshold);
            thermalColonyPocketStrength = Mathf.Max(0f, thermalColonyPocketStrength);
            thermalDeadZonePocketStrength = Mathf.Max(0f, thermalDeadZonePocketStrength);
            megaWreckInteriorHolePadding = Mathf.Max(0f, megaWreckInteriorHolePadding);
            megaWreckInteriorMinimumHoleRadius = Mathf.Max(1f, megaWreckInteriorMinimumHoleRadius);
            deepColdPocketTemperatureThresholdCelsius = Mathf.Clamp(deepColdPocketTemperatureThresholdCelsius, -10f, 10f);
            deepColdPocketStressMultiplierMax = Mathf.Clamp(deepColdPocketStressMultiplierMax, 1f, 4f);
            maxTrackedCorruptedChunks = Mathf.Max(32, maxTrackedCorruptedChunks);
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
            _terrainHoleRecords = new TerrainHoleRecord[Mathf.Max(4, initialTerrainHoleCapacity)];
            // COLD ALLOC: TerrainHoleStreamingRecord[initialTerrainHoleCapacity] - terrain-hole streaming snapshot growth cache - owner: HectonMapMagicVegetationBridge
            _terrainHoleStreamingRecords = new TerrainHoleStreamingRecord[Mathf.Max(4, initialTerrainHoleCapacity)];
            InitializeChunkPools();
            _runtimeLifecycleActive = true;
            _runtimeTeardownComplete = false;
        }

        private void OnEnable()
        {
            if (!_runtimeLifecycleActive)
            {
                if (_activeRuntimeInstance == this)
                    _activeRuntimeInstance = null;

                return;
            }

            _runtimeTeardownComplete = false;
            _activeRuntimeInstance = this;
            InitializeChunkPools();
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
            TryRegister();
        }

        private void OnDisable()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                if (_activeRuntimeInstance == this)
                    _activeRuntimeInstance = null;

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
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            _totalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            ResetDeferredStartupWork();
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
            _runtimeTeardownComplete = true;
        }

        private void OnDestroy()
        {
            if (!_runtimeLifecycleActive || _runtimeTeardownComplete)
            {
                if (_activeRuntimeInstance == this)
                    _activeRuntimeInstance = null;

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
            ClearRendererBindings();
            ReleaseBuffers();
            ResetActiveState(clearChunkCache: true);
            DisposeChunkPools();
            DisposeChunkPool(ref _surfaceDefragScratchPool);
            DisposeChunkPool(ref _underwaterDefragScratchPool);
            _tileStates.Clear();
            ClearArtificialInteriorState();
            ClearVegetationAudioHandoff();
            _totalUniverseOffset = Vector3.zero;
            GlobalTotalUniverseOffset = Vector3.zero;
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
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
            float clampedDt = Mathf.Max(0f, dt);
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
                _externalThreatPulseHoldTimer = Mathf.Max(0f, _externalThreatPulseHoldTimer - dt);

            if (TryProgressDeferredStartupWork())
                return;

            CompleteAbyssalPathJob(forceComplete: false);
            CompleteHLODCullJob(forceComplete: false);
            UpdatePlayerMotionState(dt);
            UpdateNativePoolDefragState(dt);
            CompleteNativePoolDefragIfReady(forceComplete: false);

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

            int completedCount = FinalizeCompletedChunkBuilds();
            if (completedCount > 0)
                EnforceChunkPoolMemoryGuard();

            bool selectionChanged = completedCount > 0 && SyncSelectedChunksFromDesired();
            if (completedCount > 0 || selectionChanged || _activeSetDirty)
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

            CompleteAbyssalPathJob(forceComplete: false);
            ResolveRuntimeDependencies();
            RefreshResidency();
            SyncMegaWreckInteriorTerrainHoles();
            EvictDistantTerrainHoles();
            CompleteThreatPropagationJob(forceComplete: false);
            CompleteFlowFieldJob(forceComplete: false);
            CompleteThermalGridJob(forceComplete: false);
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

        /// <summary>Active surface instance matrix buffer currently owned by this bridge.</summary>
        public GraphicsBuffer SurfaceInstanceMatrixBuffer => _surfaceInstanceBuffer;

        /// <summary>Active surface instance metadata buffer currently owned by this bridge.</summary>
        public GraphicsBuffer SurfaceInstanceDataBuffer => _surfaceInstanceDataBuffer;

        /// <summary>Active underwater instance matrix buffer currently owned by this bridge.</summary>
        public GraphicsBuffer UnderwaterInstanceMatrixBuffer => _underwaterInstanceBuffer;

        /// <summary>Active underwater instance metadata buffer currently owned by this bridge.</summary>
        public GraphicsBuffer UnderwaterInstanceDataBuffer => _underwaterInstanceDataBuffer;

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

        /// <summary>Converts current runtime-local coordinates into stable universe coordinates.</summary>
        public static Vector3 ToUniverseSpace(Vector3 runtimePosition) => runtimePosition - GlobalTotalUniverseOffset;

        /// <summary>Converts stable universe coordinates into current runtime-local coordinates.</summary>
        public static Vector3 ToRuntimeSpace(Vector3 universePosition) => universePosition + GlobalTotalUniverseOffset;

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
        public NativeArray<Vector3> ActiveAbyssalAnchorsNative => _abyssalAnchorPositionsNative;

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
        public NativeArray<Vector3> ActiveAbyssalNavNodesNative => _abyssalNavNodeSnapshotNative;

        /// <summary>Number of active abyssal safe-navigation nodes currently exported by the bridge.</summary>
        public int ActiveAbyssalNavNodeCount => _abyssalNavNodeCount;

        /// <summary>Current ecosystem threat grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float> EcosystemThreatGrid => GetThreatGridFloatView();

        /// <summary>Compressed ecosystem threat grid used by AI/flow-field consumers that do not need float precision.</summary>
        public NativeArray<byte> EcosystemThreatGridCompressed => _ecosystemThreatGridCompressedCurrentNative;

        /// <summary>Permanent threat-echo flags aligned to the compressed ecosystem threat grid. 1 means the cell never decays below the echo floor.</summary>
        public NativeArray<byte> EcosystemThreatEchoFlags => _ecosystemThreatEchoCurrentNative;

        /// <summary>Current ecosystem threat grid resolution in cells along one axis.</summary>
        public int EcosystemThreatGridResolution => _ecosystemThreatGridResolution;

        /// <summary>Current ecosystem threat grid center in world space.</summary>
        public Vector3 EcosystemThreatGridCenter => _ecosystemThreatGridCenter;

        /// <summary>Current abyssal flow-field. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float2> EcosystemFlowField => _ecosystemFlowFieldCurrentNative;

        /// <summary>Current abyssal flow-field center in world space.</summary>
        public Vector3 EcosystemFlowFieldCenter => _ecosystemFlowFieldCenter;

        /// <summary>Current abyssal thermal grid. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float> AbyssalThermalGrid => _abyssalThermalGridNative;

        /// <summary>Current 3D abyssal flow volume. Treat as read-only and reacquire after each SlowTick.</summary>
        public NativeArray<float3> AbyssalFlowVolume => _abyssalFlowVolumeCurrentNative;

        /// <summary>Current abyssal thermal-grid center in world space.</summary>
        public Vector3 AbyssalThermalGridCenter => _abyssalThermalGridCenter;

        /// <summary>Current mega-wreck section streaming payload. Treat as read-only and reacquire after each rebuild.</summary>
        public NativeArray<MegaWreckStreamSection> MegaWreckStreamSections => _megaWreckStreamSnapshotNative;

        /// <summary>Current immutable HLOD registry payload for large distant structures.</summary>
        public NativeArray<HLODData> HLODRegistry => _hlodRegistrySnapshotNative;

        /// <summary>Current immutable visible HLOD payload after frustum and distance culling.</summary>
        public NativeArray<HLODData> VisibleHLODRegistry => _visibleHlodSnapshotNative;

        /// <summary>Current ecosystem threat hotspot level from the last completed propagation step.</summary>
        public float CurrentThreatHotspotLevel => _currentThreatHotspotLevel;

        /// <summary>Current ecosystem threat hotspot position from the last completed propagation step.</summary>
        public Vector3 CurrentThreatHotspotPosition => _currentThreatHotspotPosition;

        /// <summary>Latest native abyssal path result. Treat as read-only and reacquire after each completed path solve.</summary>
        public NativeArray<Vector3> ActiveAbyssalPathNative => _abyssalPathSnapshotNative;

        /// <summary>Number of valid waypoints in the latest completed abyssal path result.</summary>
        public int ActiveAbyssalPathCount => _abyssalPathCount;

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
            if (playerTransform == null || !TryFindPlayerTileState(playerTransform.position, out TileRuntimeState state) || state == null)
                return false;

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
            anchors = _abyssalAnchorPositionsNative;
            count = _abyssalAnchorCount;
            return count > 0 && anchors.IsCreated;
        }

        /// <summary>
        /// Returns the current immutable abyssal-nav-node snapshot as native memory for pathfinding consumers.
        /// </summary>
        public bool TryGetActiveAbyssalNavNodePayload(out NativeArray<Vector3> nodes, out int count)
        {
            nodes = _abyssalNavNodeSnapshotNative;
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
            conduitVectors = _abyssalNavConduitVectorsSnapshotNative;
            conduitStrengths = _abyssalNavConduitStrengthSnapshotNative;
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
            nodes = _abyssalNavNodeSnapshotNative;
            nodeTypes = _abyssalNavNodeTypesSnapshotNative;
            conduitVectors = _abyssalNavConduitVectorsSnapshotNative;
            conduitStrengths = _abyssalNavConduitStrengthSnapshotNative;
            spatialHash = _abyssalNavGraphHashNative;
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
            threatLevels = _ecosystemThreatGridCompressedCurrentNative;
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
            threatVoxels = _ecosystemThreatVoxelCurrentNative;
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
            echoFlags = _ecosystemThreatEchoCurrentNative;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemThreatGridCenter;
            cellSize = threatGridCellSize;
            return _threatGridInitialized &&
                   echoFlags.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Records a temporary species-scoped predator fear sector at the snapped AUP ecosystem cell center.
        /// </summary>
        public void RegisterPredatorFearNode(int speciesId, Vector3 worldPosition, float normalizedDamage)
        {
            if (speciesId == 0 || normalizedDamage < 0.3f)
                return;

            EnsurePredatorFearMemoryBuffers();
            float currentTime = _predatorFearSimulationTime;
            CompactPredatorFearNodes(currentTime);

            float normalizedWeight = Mathf.Clamp01((normalizedDamage - 0.3f) / 0.7f);
            if (normalizedWeight <= 0f)
                return;

            float3 sectorCenter = ResolvePredatorFearSectorCenter(worldPosition);
            float sectorRadius = Mathf.Max(1f, predatorFearNodeRadiusMeters);
            float expireTime = currentTime + Mathf.Max(120f, predatorFearLifetimeSeconds);

            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId != speciesId)
                    continue;

                float2 delta = new float2(node.Position.x - sectorCenter.x, node.Position.z - sectorCenter.z);
                if (math.lengthsq(delta) > 1f)
                    continue;

                node.Position = sectorCenter;
                node.Radius = Mathf.Max(node.Radius, sectorRadius);
                node.Weight = Mathf.Max(node.Weight, normalizedWeight);
                node.ExpireTime = Mathf.Max(node.ExpireTime, expireTime);
                _predatorFearNodes[i] = node;
                if (!_abyssalPathScheduled)
                    SyncPredatorFearNodeSnapshot(currentTime);
                return;
            }

            int writeIndex = _predatorFearNodeCount < _predatorFearNodes.Length
                ? _predatorFearNodeCount
                : FindWeakestPredatorFearNodeIndex(currentTime);

            if (writeIndex < 0)
                writeIndex = 0;

            _predatorFearNodes[writeIndex] = new PredatorFearNodeState
            {
                Position = sectorCenter,
                Radius = sectorRadius,
                Weight = normalizedWeight,
                ExpireTime = expireTime,
                SpeciesId = speciesId
            };

            _predatorFearNodeCount = Mathf.Min(_predatorFearNodes.Length, Mathf.Max(_predatorFearNodeCount, writeIndex + 1));
            if (!_abyssalPathScheduled)
                SyncPredatorFearNodeSnapshot(currentTime);
        }

        /// <summary>
        /// Samples the current species-scoped predator fear pressure at a world position.
        /// </summary>
        public float SamplePredatorFearPressure(Vector3 worldPosition, int speciesId)
        {
            if (speciesId == 0 || _predatorFearNodeCount <= 0)
                return 0f;

            float currentTime = _predatorFearSimulationTime;
            float pressure = 0f;
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            float3 position = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId != speciesId || node.ExpireTime <= currentTime)
                    continue;

                float2 delta = new float2(position.x - node.Position.x, position.z - node.Position.z);
                float radius = math.max(node.Radius, 1f);
                float gate = 1f - math.saturate(math.length(delta) / radius);
                if (gate <= 0f)
                    continue;

                float freshness = math.saturate((node.ExpireTime - currentTime) / lifetime);
                pressure = math.max(pressure, node.Weight * freshness * gate);
            }

            return Mathf.Clamp01(pressure * predatorFearCognitionPressureScale);
        }

        private void EnsurePredatorFearMemoryBuffers()
        {
            int safeCapacity = Mathf.Clamp(predatorFearNodeCapacity, 4, 128);
            if (_predatorFearNodes == null || _predatorFearNodes.Length != safeCapacity)
            {
                // COLD ALLOC: PredatorFearNodeState[safeCapacity] - bounded predator fear-sector memory aligned to ecosystem threat routing - owner: HectonMapMagicVegetationBridge
                PredatorFearNodeState[] resized = new PredatorFearNodeState[safeCapacity];
                int copyCount = Mathf.Min(_predatorFearNodeCount, resized.Length);
                if (_predatorFearNodes != null && copyCount > 0)
                    Array.Copy(_predatorFearNodes, resized, copyCount);

                _predatorFearNodes = resized;
                _predatorFearNodeCount = copyCount;
            }

            if (!_predatorFearNodesSnapshotNative.IsCreated || _predatorFearNodesSnapshotNative.Length != safeCapacity)
            {
                DisposeNativeArray(ref _predatorFearNodesSnapshotNative);
                // COLD ALLOC: NativeArray<PredatorFearNodeSnapshot>[safeCapacity] - path-job snapshot of predator fear memory - owner: HectonMapMagicVegetationBridge
                _predatorFearNodesSnapshotNative = new NativeArray<PredatorFearNodeSnapshot>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void CompactPredatorFearNodes(float currentTime)
        {
            if (_predatorFearNodeCount <= 0 || _predatorFearNodes == null)
            {
                _predatorFearNodeCount = 0;
                return;
            }

            int writeIndex = 0;
            for (int i = 0; i < _predatorFearNodeCount; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                if (node.SpeciesId == 0 || node.ExpireTime <= currentTime || node.Weight <= 0f)
                    continue;

                if (writeIndex != i)
                    _predatorFearNodes[writeIndex] = node;

                writeIndex++;
            }

            _predatorFearNodeCount = writeIndex;
        }

        private int FindWeakestPredatorFearNodeIndex(float currentTime)
        {
            if (_predatorFearNodes == null || _predatorFearNodes.Length == 0)
                return -1;

            int weakestIndex = 0;
            float weakestScore = float.MaxValue;
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            int count = Mathf.Min(_predatorFearNodes.Length, Mathf.Max(_predatorFearNodeCount, 1));
            for (int i = 0; i < count; i++)
            {
                PredatorFearNodeState node = _predatorFearNodes[i];
                float freshness = Mathf.Clamp01((node.ExpireTime - currentTime) / lifetime);
                float score = node.SpeciesId == 0 ? -1f : node.Weight * freshness;
                if (score < weakestScore)
                {
                    weakestScore = score;
                    weakestIndex = i;
                }
            }

            return weakestIndex;
        }

        private float3 ResolvePredatorFearSectorCenter(Vector3 worldPosition)
        {
            float sectorSize = Mathf.Max(100f, predatorFearSectorSizeMeters);
            return new float3(
                Mathf.Round(worldPosition.x / sectorSize) * sectorSize,
                worldPosition.y,
                Mathf.Round(worldPosition.z / sectorSize) * sectorSize);
        }

        private void SyncPredatorFearNodeSnapshot(float currentTime)
        {
            if (!_predatorFearNodesSnapshotNative.IsCreated)
                return;

            CompactPredatorFearNodes(currentTime);
            float lifetime = Mathf.Max(120f, predatorFearLifetimeSeconds);
            int safeLength = _predatorFearNodesSnapshotNative.Length;
            int activeCount = Mathf.Min(_predatorFearNodeCount, safeLength);
            for (int i = 0; i < safeLength; i++)
            {
                PredatorFearNodeSnapshot snapshot = default;
                if (i < activeCount)
                {
                    PredatorFearNodeState node = _predatorFearNodes[i];
                    float freshness = Mathf.Clamp01((node.ExpireTime - currentTime) / lifetime);
                    snapshot.Position = node.Position;
                    snapshot.Radius = node.Radius;
                    snapshot.Weight = node.Weight * freshness;
                    snapshot.SpeciesId = node.SpeciesId;
                    snapshot.Padding = 0f;
                }

                _predatorFearNodesSnapshotNative[i] = snapshot;
            }
        }

        /// <summary>
        /// Returns the current abyssal flow-field payload and metadata for external consumers.
        /// </summary>
        public bool TryGetEcosystemFlowFieldPayload(
            out NativeArray<float2> flowVectors,
            out int gridResolution,
            out Vector3 gridCenter,
            out float cellSize)
        {
            flowVectors = _ecosystemFlowFieldCurrentNative;
            gridResolution = _ecosystemThreatGridResolution;
            gridCenter = _ecosystemFlowFieldCenter;
            cellSize = threatGridCellSize;
            return _flowFieldInitialized &&
                   flowVectors.IsCreated &&
                   gridResolution > 0 &&
                   cellSize > 0f;
        }

        /// <summary>
        /// Registers one short-lived wake impulse that will be folded into the next abyssal flow-field solve.
        /// </summary>
        public void RegisterSwarmWakeImpulse(Vector3 positionWS, Vector3 flowVectorWS, float radiusMeters, float lifetimeSeconds)
        {
            EnsureFlowFieldBuffers();
            float strength = flowVectorWS.magnitude;
            if (strength <= 0.0001f)
            {
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
                if (_swarmWakeImpulseNative.IsCreated)
                    _swarmWakeImpulseNative[0] = default;
                return;
            }

            _swarmWakeImpulseNative[0] = new SwarmWakeImpulse
            {
                Position = new float3(positionWS.x, positionWS.y, positionWS.z),
                Radius = math.max(0.1f, radiusMeters),
                FlowVector = new float3(flowVectorWS.x, flowVectorWS.y, flowVectorWS.z),
                Strength = strength
            };
            _swarmWakeImpulseCount = 1;
            _swarmWakeImpulseExpireTime = Time.unscaledTime + math.max(0.1f, lifetimeSeconds);
        }

        /// <summary>
        /// Returns the current abyssal thermal-grid payload and metadata for survival and environment consumers.
        /// </summary>
        public bool TryGetAbyssalThermalGridPayload(
            out NativeArray<float> temperatures,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            temperatures = _abyssalThermalGridNative;
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalThermalGridInitialized &&
                   temperatures.IsCreated &&
                   horizontalResolution > 0 &&
                   verticalResolution > 0 &&
                   horizontalCellSize > 0f &&
                   verticalCellSize > 0f;
        }

        /// <summary>
        /// Returns the current 3D abyssal flow-volume payload and metadata for current-driven deep-ocean consumers.
        /// </summary>
        public bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3> flowVectors,
            out int horizontalResolution,
            out int verticalResolution,
            out Vector3 gridCenter,
            out float horizontalCellSize,
            out float verticalCellSize)
        {
            flowVectors = _abyssalFlowVolumeCurrentNative;
            horizontalResolution = _abyssalThermalGridResolutionXZ;
            verticalResolution = _abyssalThermalGridResolutionY;
            gridCenter = _abyssalThermalGridCenter;
            horizontalCellSize = thermalGridHorizontalCellSize;
            verticalCellSize = thermalGridVerticalCellSize;
            return _abyssalFlowVolumeInitialized &&
                   flowVectors.IsCreated &&
                   horizontalResolution > 0 &&
                   verticalResolution > 0 &&
                   horizontalCellSize > 0f &&
                   verticalCellSize > 0f;
        }

        /// <summary>
        /// Returns the current mega-wreck section streaming payload for composite-structure consumers.
        /// </summary>
        public bool TryGetMegaWreckStreamPayload(out NativeArray<MegaWreckStreamSection> sections, out int count)
        {
            sections = _megaWreckStreamSnapshotNative;
            count = _megaWreckStreamCount;
            return count > 0 && sections.IsCreated;
        }

        /// <summary>
        /// Returns the current HLOD registry payload for large persistent structures and mega-wreck silhouettes.
        /// </summary>
        public bool TryGetHLODRegistryPayload(out NativeArray<HLODData> entries, out int count)
        {
            entries = _hlodRegistrySnapshotNative;
            count = _hlodRegistryCount;
            return count > 0 && entries.IsCreated;
        }

        /// <summary>
        /// Returns the current frustum-culled HLOD payload for distant rendering consumers.
        /// </summary>
        public bool TryGetVisibleHLODPayload(out NativeArray<HLODData> entries, out int count)
        {
            CompleteHLODCullJob(forceComplete: false);
            entries = _visibleHlodSnapshotNative;
            count = _visibleHlodCount;
            return count > 0 && entries.IsCreated;
        }

        /// <summary>
        /// Returns the current terrain-hole streaming payload for cave and interior streaming consumers.
        /// </summary>
        public bool TryGetTerrainHoleStreamingPayload(out NativeArray<TerrainHoleStreamingRecord> holes, out int count)
        {
            holes = _terrainHoleStreamingRecordsNative;
            count = _terrainHoleCount;
            return count > 0 && holes.IsCreated;
        }

        /// <summary>
        /// Returns the current global canopy-height grid for audio and light-occlusion consumers.
        /// </summary>
        public bool TryGetCanopyHeightGridPayload(out NativeArray<float> canopyHeights, out int gridResolution, out Vector3 gridCenter, out float cellSize)
        {
            canopyHeights = _canopyHeightGridNative;
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
            nodeTypes = _abyssalNavNodeTypesSnapshotNative;
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

        /// <summary>
        /// Returns the current threat level at the provided world-space position without allocations.
        /// </summary>
        public float GetThreatLevel(Vector3 position)
        {
            if (!_threatGridInitialized || !_ecosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
                return 0f;

            return SampleThreatGridAtPosition(position, _ecosystemThreatGridCenter, threatGridCellSize, _ecosystemThreatGridResolution, _ecosystemThreatGridCurrentNative);
        }

        internal void ApplyExternalThreatPulse(Vector3 position, float radius, float strength, float holdDuration)
        {
            float resolvedRadius = Mathf.Max(0f, radius);
            float resolvedStrength = Mathf.Max(0f, strength);
            float resolvedHoldDuration = Mathf.Max(0.01f, holdDuration);
            if (resolvedRadius <= 0f || resolvedStrength <= 0f)
                return;

            bool overwritePulse =
                _externalThreatPulseHoldTimer <= 0f ||
                resolvedStrength >= _externalThreatPulseStrength;
            if (overwritePulse)
            {
                _externalThreatPulsePosition = position;
                _externalThreatPulseRadius = resolvedRadius;
                _externalThreatPulseStrength = resolvedStrength;
            }
            else
            {
                _externalThreatPulseRadius = Mathf.Max(_externalThreatPulseRadius, resolvedRadius);
                _externalThreatPulseStrength = Mathf.Max(_externalThreatPulseStrength, resolvedStrength);
            }

            _externalThreatPulseHoldTimer = Mathf.Max(_externalThreatPulseHoldTimer, resolvedHoldDuration);
        }

        /// <summary>
        /// Returns the highest stamped canopy obstacle Y at the given world-space XZ coordinate.
        /// </summary>
        public float GetCanopyHeightAt(float worldX, float worldZ)
        {
            if (!_canopyGridInitialized || !_canopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
                return float.NegativeInfinity;

            return SampleCanopyHeightAtPosition(worldX, worldZ);
        }

        /// <summary>
        /// Registers a persistent artificial structure bounds for threat damping and interior-aware navigation.
        /// </summary>
        public void RegisterArtificialStructure(Bounds bounds, StructureType type)
        {
            if (bounds.size.sqrMagnitude <= 0.0001f)
                return;

            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord existing = _persistentArtificialStructures[i];
                if (existing.Type != type)
                    continue;

                if ((existing.Bounds.center - center).sqrMagnitude > 0.25f)
                    continue;

                if ((existing.Bounds.size - size).sqrMagnitude > 0.25f)
                    continue;

                existing.Bounds = bounds;
                _persistentArtificialStructures[i] = existing;
                return;
            }

            _persistentArtificialStructures.Add(new PersistentArtificialStructureRecord
            {
                StructureId = _nextArtificialStructureId++,
                Bounds = bounds,
                Type = type
            });
        }

        /// <summary>
        /// Returns a multiplicative predator spawn-weight modifier derived from the local threat field.
        /// 1.0 = neutral, 4.0 = +300% predator weight at maximum threat.
        /// </summary>
        public float GetSpawnWeightModifier(Vector3 position)
        {
            float threat = GetThreatLevel(position);
            if (threat <= 0f)
                return 1f;

            return 1f + (Mathf.Clamp01(threat) * predatorSpawnThreatBonusMultiplier);
        }

        /// <summary>
        /// Returns true when the provided world-space position falls inside a permanent threat-echo cell.
        /// </summary>
        public bool HasPermanentThreatEcho(Vector3 position)
        {
            if (!_threatGridInitialized ||
                !_ecosystemThreatEchoCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            return SampleThreatEchoFlagAtPosition(position, _ecosystemThreatGridCenter, threatGridCellSize, _ecosystemThreatGridResolution, _ecosystemThreatEchoCurrentNative) != 0;
        }

        /// <summary>
        /// Returns a local techno-jungle regrowth modifier derived from permanent threat echoes.
        /// 0 = no extra regrowth pressure, 1 = full echo-driven bio-cable boost.
        /// </summary>
        public float GetTechnoJungleEchoInfluence(Vector3 position)
        {
            return HasPermanentThreatEcho(position) ? 1f : 0f;
        }

        /// <summary>
        /// Returns the current abyssal flow direction at the provided world-space position without allocations.
        /// </summary>
        public Vector3 GetFlowDirection(Vector3 position)
        {
            if (!_flowFieldInitialized || !_ecosystemFlowFieldCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
            {
                if (playerTransform == null)
                    return Vector3.zero;

                Vector3 toPlayer = playerTransform.position - position;
                toPlayer.y = 0f;
                return toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.zero;
            }

            float2 flow = SampleFlowFieldAtPosition(position, _ecosystemFlowFieldCenter, threatGridCellSize, _ecosystemThreatGridResolution, _ecosystemFlowFieldCurrentNative);
            return new Vector3(flow.x, 0f, flow.y);
        }

        /// <summary>
        /// Returns the strongest nearby abyssal conductor vector sampled from the immutable nav-graph snapshot.
        /// </summary>
        public Vector3 GetAbyssalConduitVector(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 ||
                !_abyssalNavConduitVectorsSnapshotNative.IsCreated ||
                !_abyssalNavConduitStrengthSnapshotNative.IsCreated)
            {
                return Vector3.zero;
            }

            int nodeIndex = FindNearestAbyssalNavNodeIndex(position);
            if (nodeIndex < 0 ||
                nodeIndex >= _abyssalNavConduitVectorsSnapshotNative.Length ||
                nodeIndex >= _abyssalNavConduitStrengthSnapshotNative.Length)
            {
                return Vector3.zero;
            }

            Vector3 conduitVector = _abyssalNavConduitVectorsSnapshot[nodeIndex];
            float conduitStrength = _abyssalNavConduitStrengthSnapshot[nodeIndex];
            return conduitStrength > 0f ? conduitVector * conduitStrength : Vector3.zero;
        }

        /// <summary>
        /// Returns the resolved water temperature in Celsius at the provided world-space position without allocations.
        /// </summary>
        public float GetWaterTemperature(Vector3 position)
        {
            if (!_abyssalThermalGridInitialized ||
                !_abyssalThermalGridNative.IsCreated ||
                _abyssalThermalGridResolutionXZ <= 0 ||
                _abyssalThermalGridResolutionY <= 0)
            {
                return thermalSurfaceTemperatureCelsius;
            }

            return SampleThermalGridAtPosition(position);
        }

        /// <summary>
        /// Returns a runtime-only cold-stress multiplier derived from abyssal thermal pockets.
        /// 1.0 means neutral water; values above 1 amplify suit heating drain and cold damage.
        /// </summary>
        public float GetDeepColdStressMultiplier(Vector3 position)
        {
            float localTemperature = GetWaterTemperature(position);
            if (localTemperature >= deepColdPocketTemperatureThresholdCelsius)
                return 1f;

            float depth01 = Mathf.InverseLerp(deepColdPocketTemperatureThresholdCelsius, thermalAbyssTemperatureCelsius, localTemperature);
            return Mathf.Lerp(1f, deepColdPocketStressMultiplierMax, Mathf.Clamp01(depth01));
        }

        /// <summary>
        /// Resolves the authored prefab backing a published mega-wreck section payload.
        /// </summary>
        public bool TryResolveMegaWreckPrefab(int wreckId, out GameObject prefab)
        {
            prefab = null;
            if (megaWreckDefinitions == null || megaWreckDefinitions.Length == 0)
                return false;

            for (int i = 0; i < megaWreckDefinitions.Length; i++)
            {
                if (megaWreckDefinitions[i].WreckId != wreckId)
                    continue;

                prefab = megaWreckDefinitions[i].Prefab;
                return prefab != null;
            }

            return false;
        }

        /// <summary>
        /// Finds the strongest threat hotspot inside the requested distance band around the player.
        /// </summary>
        public bool TryGetThreatHotspot(
            float minimumThreatLevel,
            float minimumDistanceFromPlayer,
            float maximumDistanceFromPlayer,
            out Vector3 hotspotPosition,
            out float hotspotThreatLevel)
        {
            hotspotPosition = _currentThreatHotspotPosition;
            hotspotThreatLevel = 0f;
            if (!_threatGridInitialized ||
                !_ecosystemThreatGridCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                playerTransform == null)
            {
                return false;
            }

            float minDistanceSq = Mathf.Max(0f, minimumDistanceFromPlayer) * Mathf.Max(0f, minimumDistanceFromPlayer);
            float maxDistance = Mathf.Max(minimumDistanceFromPlayer, maximumDistanceFromPlayer);
            float maxDistanceSq = maxDistance * maxDistance;
            int halfExtent = _ecosystemThreatGridResolution >> 1;
            Vector3 playerPosition = playerTransform.position;
            float bestThreat = minimumThreatLevel;
            Vector3 bestPosition = default;

            for (int z = 0; z < _ecosystemThreatGridResolution; z++)
            {
                float localZ = (z - halfExtent) * threatGridCellSize;
                for (int x = 0; x < _ecosystemThreatGridResolution; x++)
                {
                    int index = (z * _ecosystemThreatGridResolution) + x;
                    float threat = _ecosystemThreatGridCurrentNative[index];
                    if (threat <= bestThreat)
                        continue;

                    float localX = (x - halfExtent) * threatGridCellSize;
                    Vector3 candidate = new Vector3(
                        _ecosystemThreatGridCenter.x + localX,
                        playerPosition.y,
                        _ecosystemThreatGridCenter.z + localZ);

                    Vector3 delta = candidate - playerPosition;
                    float distanceSq = (delta.x * delta.x) + (delta.z * delta.z);
                    if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                        continue;

                    bestThreat = threat;
                    bestPosition = candidate;
                }
            }

            if (bestThreat <= minimumThreatLevel)
                return false;

            hotspotPosition = bestPosition;
            hotspotThreatLevel = bestThreat;
            return true;
        }

        /// <summary>
        /// Returns the latest completed abyssal path payload produced by the native A* solver.
        /// </summary>
        public bool TryGetLatestAbyssalPathPayload(out NativeArray<Vector3> path, out int count)
        {
            CompleteAbyssalPathJob(forceComplete: false);
            path = _abyssalPathSnapshotNative;
            count = _abyssalPathCount;
            return count > 0 && path.IsCreated;
        }

        /// <summary>
        /// Builds an immediate non-allocating 3D voxel route through the active cave portal graph.
        /// This is restricted to pure cave-voxel traversal and is intended for fauna steering guidance.
        /// </summary>
        public bool TryBuildImmediateAbyssalVoxelRoute(Vector3 startPosition, Vector3 endPosition, Vector3[] outputWaypoints, out int waypointCount)
        {
            waypointCount = 0;
            if (outputWaypoints == null || outputWaypoints.Length < 2)
                return false;

            float3 startProbe = new float3(startPosition.x, startPosition.y, startPosition.z);
            float3 endProbe = new float3(endPosition.x, endPosition.y, endPosition.z);
            if (!VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(startProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample startSample) ||
                !VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(endProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample endSample) ||
                startSample.Mode != VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel ||
                endSample.Mode != VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc(startProbe, endProbe, outputWaypoints, out waypointCount);
        }

        /// <summary>
        /// Schedules a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, out JobHandle handle)
        {
            return TryScheduleAbyssalPath(startPosition, endPosition, 0, out handle);
        }

        /// <summary>
        /// Schedules a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// Species-aware predator fear penalties are applied when <paramref name="traversalSpeciesId"/> is non-zero.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, int traversalSpeciesId, out JobHandle handle)
        {
            handle = default;
            CompleteAbyssalPathJob(forceComplete: false);
            if (_abyssalPathScheduled ||
                _abyssalNavNodeCount <= 0 ||
                !_abyssalNavNodeSnapshotNative.IsCreated)
            {
                return false;
            }

            EnsurePredatorFearMemoryBuffers();
            SyncPredatorFearNodeSnapshot(_predatorFearSimulationTime);

            float3 startProbe = new float3(startPosition.x, startPosition.y, startPosition.z);
            float3 endProbe = new float3(endPosition.x, endPosition.y, endPosition.z);
            bool hasStartHybridSample = VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(startProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample startHybridSample);
            bool hasEndHybridSample = VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(endProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample endHybridSample);
            bool hasStartTerrainHeight = startHybridSample.HasTerrainHeight != 0 || TryGetCachedTerrainHeight(startPosition.x, startPosition.z, out startHybridSample.TerrainHeight);
            bool hasEndTerrainHeight = endHybridSample.HasTerrainHeight != 0 || TryGetCachedTerrainHeight(endPosition.x, endPosition.z, out endHybridSample.TerrainHeight);
            VoxelDynamicNavGridRuntime.HybridNavigationMode startNavMode = hasStartHybridSample ? startHybridSample.Mode : VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            VoxelDynamicNavGridRuntime.HybridNavigationMode endNavMode = hasEndHybridSample ? endHybridSample.Mode : VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool startUsesHeightmap = startNavMode == VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool endUsesHeightmap = endNavMode == VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool startUsesVoxel = !startUsesHeightmap;
            bool endUsesVoxel = !endUsesHeightmap;

            Vector3 resolvedStartPosition = startPosition;
            if (startUsesHeightmap && hasStartTerrainHeight)
                resolvedStartPosition.y = math.max(startPosition.y, startHybridSample.TerrainHeight + abyssalNavNodeHoverHeight);

            Vector3 resolvedEndPosition = endPosition;
            if (endUsesHeightmap && hasEndTerrainHeight)
                resolvedEndPosition.y = math.max(endPosition.y, endHybridSample.TerrainHeight + abyssalNavNodeHoverHeight);

            int startNode = FindNearestAbyssalNavNodeIndex(resolvedStartPosition);
            int endNode = FindNearestAbyssalNavNodeIndex(resolvedEndPosition);
            if (startNode < 0 || endNode < 0)
                return false;

            bool canReuseLastAbyssalTarget = startUsesHeightmap && endUsesHeightmap;
            if (canReuseLastAbyssalTarget &&
                _hasLastAbyssalPathTarget &&
                _lastAbyssalPathEndNode >= 0 &&
                _lastAbyssalPathEndNode < _abyssalNavNodeCount &&
                (resolvedEndPosition - _lastAbyssalPathTargetPosition).sqrMagnitude < (abyssalPathRetargetDistance * abyssalPathRetargetDistance))
            {
                endNode = _lastAbyssalPathEndNode;
            }

            EnsureAbyssalPathBuffers(_abyssalNavNodeCount);
            if (_abyssalPathRawResultNative.IsCreated)
                _abyssalPathRawResultNative.Clear();
            if (_abyssalPathResultNative.IsCreated)
                _abyssalPathResultNative.Clear();
            _abyssalPathCount = 0;

            JobHandle pathSourceHandle = default;
            bool scheduledMacroVoxelRoute = false;
            if (startUsesVoxel &&
                endUsesVoxel &&
                VoxelDynamicNavGridRuntime.TryBuildMacroPortalRoute(startProbe, endProbe, _abyssalPathRawResultNative))
            {
                scheduledMacroVoxelRoute = true;
            }

            if (!scheduledMacroVoxelRoute)
            {
                var astarJob = new NativeAStarJob
                {
                    Nodes = _abyssalNavNodeSnapshotNative,
                    NodeTypes = _abyssalNavNodeTypesSnapshotNative,
                    ConduitVectors = _abyssalNavConduitVectorsSnapshotNative,
                    ConduitStrengths = _abyssalNavConduitStrengthSnapshotNative,
                    ThreatGrid = _ecosystemThreatGridCurrentNative,
                    ThreatVoxelGrid = _ecosystemThreatVoxelCurrentNative,
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    ThreatVoxelDimensions = new int3(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution),
                    ThreatVoxelOrigin = new float3(_ecosystemThreatVoxelOrigin.x, _ecosystemThreatVoxelOrigin.y, _ecosystemThreatVoxelOrigin.z),
                    ThreatVoxelCellSize = new float3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize),
                    WaterLevel = waterLevel,
                    Parents = _abyssalPathParentsNative,
                    GScore = _abyssalPathGScoreNative,
                    FScore = _abyssalPathFScoreNative,
                    ClosedFlags = _abyssalPathClosedFlagsNative,
                    HeapNodes = _abyssalPathHeapNodesNative,
                    HeapPositions = _abyssalPathHeapPositionsNative,
                    Path = _abyssalPathRawResultNative,
                    PredatorFearNodes = _predatorFearNodesSnapshotNative,
                    PredatorFearNodeCount = _predatorFearNodeCount,
                    TraversalSpeciesId = traversalSpeciesId,
                    PredatorFearPenaltyWeight = predatorFearPathPenaltyWeight,
                    StartNode = startNode,
                    EndNode = endNode,
                    StartPosition = new float3(resolvedStartPosition.x, resolvedStartPosition.y, resolvedStartPosition.z),
                    EndPosition = new float3(resolvedEndPosition.x, resolvedEndPosition.y, resolvedEndPosition.z),
                    NeighborRadius = abyssalPathNeighborRadius,
                    VerticalTolerance = abyssalPathVerticalTolerance,
                    ThreatPenaltyWeight = abyssalPathThreatPenaltyWeight,
                    ConduitStartDepth = abyssalConduitStartDepth,
                    ConduitVerticalToleranceBonus = abyssalConduitVerticalToleranceBonus,
                    ConduitMisalignmentPenalty = abyssalConduitMisalignmentPenalty,
                    ConduitAlignmentReward = abyssalConduitAlignmentReward,
                    InteriorTraversalCostMultiplier = abyssalInteriorTraversalCostMultiplier,
                    MaxExpandedNodes = abyssalPathMaxExpandedNodes
                };

                pathSourceHandle = astarJob.Schedule();
            }

            NativeArray<byte> navPassabilityGrid = default;
            int3 navPassabilityDimensions = int3.zero;
            float3 navPassabilityOrigin = float3.zero;
            float navPassabilityCellSize = 0f;

            if (startUsesVoxel &&
                !VoxelDynamicNavGridRuntime.TryGetContainingPassabilityPayload(
                    startProbe,
                    out navPassabilityGrid,
                    out navPassabilityDimensions,
                    out navPassabilityOrigin,
                    out navPassabilityCellSize))
            {
                VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                    startProbe,
                    out navPassabilityGrid,
                    out navPassabilityDimensions,
                    out navPassabilityOrigin,
                    out navPassabilityCellSize);
            }

            if (!navPassabilityGrid.IsCreated &&
                endUsesVoxel &&
                !VoxelDynamicNavGridRuntime.TryGetContainingPassabilityPayload(
                    endProbe,
                    out navPassabilityGrid,
                    out navPassabilityDimensions,
                    out navPassabilityOrigin,
                    out navPassabilityCellSize))
            {
                VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                    endProbe,
                    out navPassabilityGrid,
                    out navPassabilityDimensions,
                    out navPassabilityOrigin,
                    out navPassabilityCellSize);
            }

            var smoothingJob = new StringPullPathJob
            {
                InputPath = _abyssalPathRawResultNative.AsDeferredJobArray(),
                DensityChunks = _densityQueryChunksNative,
                DensityGrid = _densityQueryGridNative,
                ChunkCount = _densityQueryChunkCount,
                TerrainHoles = _terrainHoleRecordsNative,
                TerrainHoleCount = _terrainHoleCount,
                ArtificialStructures = _artificialStructureRecordsNative,
                ArtificialStructureHash = _artificialStructureHashFrontNative,
                NavPassabilityGrid = navPassabilityGrid,
                ThreatVoxelGrid = _ecosystemThreatVoxelCurrentNative,
                ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                ThreatGridCellSize = threatGridCellSize,
                ThreatGridResolution = _ecosystemThreatGridResolution,
                NavPassabilityDimensions = navPassabilityDimensions,
                NavPassabilityOrigin = navPassabilityOrigin,
                NavPassabilityCellSize = navPassabilityCellSize,
                ThreatVoxelDimensions = new int3(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution),
                ThreatVoxelOrigin = new float3(_ecosystemThreatVoxelOrigin.x, _ecosystemThreatVoxelOrigin.y, _ecosystemThreatVoxelOrigin.z),
                ThreatVoxelCellSize = new float3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize),
                SampleSpacing = abyssalPathSmoothingSampleSpacing,
                MaxSamplesPerSegment = abyssalPathSmoothingMaxSamples,
                KelpWeight = abyssalPathSmoothingKelpWeight,
                SargassumWeight = abyssalPathSmoothingSargassumWeight,
                DensityObstacleThreshold = abyssalPathSmoothingObstacleThreshold,
                OutputPath = _abyssalPathResultNative
            };

            _abyssalPathHandle = smoothingJob.Schedule(pathSourceHandle);
            _abyssalPathScheduled = true;
            _lastAbyssalPathEndNode = canReuseLastAbyssalTarget && !scheduledMacroVoxelRoute ? endNode : -1;
            _lastAbyssalPathTargetPosition = resolvedEndPosition;
            _hasLastAbyssalPathTarget = canReuseLastAbyssalTarget && !scheduledMacroVoxelRoute;
            handle = _abyssalPathHandle;
            return true;
        }

        /// <summary>
        /// Applies an arbitrary caller-owned surface flow-vector field to the active surface payload without binding to a specific ocean backend.
        /// </summary>
        public bool TryApplyExternalSurfaceFlowVectorField(NativeArray<Vector3> flowVectors, int count)
        {
            if (count <= 0 ||
                count != _surfaceFrontCount ||
                !flowVectors.IsCreated ||
                !_surfaceAggregateFrontBuffers.FlowDirections.IsCreated ||
                !_surfaceAggregateFrontBuffers.FlowVectors.IsCreated)
            {
                return false;
            }

            int safeCount = Mathf.Min(
                count,
                Mathf.Min(_surfaceAggregateFrontBuffers.FlowDirections.Length, _surfaceAggregateFrontBuffers.FlowVectors.Length));
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 flowVector = flowVectors[i];
                Vector2 flowDirection = NormalizeFlowDirection(new Vector2(flowVector.x, flowVector.z));
                _surfaceAggregateFrontBuffers.FlowVectors[i] = flowVector;
                _surfaceAggregateFrontBuffers.FlowDirections[i] = flowDirection;
            }

            return true;
        }

        /// <summary>
        /// Marks streamed chunks intersecting the requested zone as corrupted and invalidates their payloads for async rebuild.
        /// </summary>
        public void CorruptZone(Vector3 worldPos, float radius)
        {
            if (_tileStates.Count <= 0)
                return;

            float clampedRadius = Mathf.Max(1f, radius);
            float radiusSq = clampedRadius * clampedRadius;
            bool changed = false;

            Dictionary<long, TileRuntimeState>.Enumerator tileEnumerator = _tileStates.GetEnumerator();
            while (tileEnumerator.MoveNext())
            {
                TileRuntimeState state = tileEnumerator.Current.Value;
                if (state == null || state.ChunkCountX <= 0 || state.ChunkCountZ <= 0)
                    continue;

                for (int chunkZ = 0; chunkZ < state.ChunkCountZ; chunkZ++)
                {
                    for (int chunkX = 0; chunkX < state.ChunkCountX; chunkX++)
                    {
                        GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
                        if (!DoesChunkBoundsIntersectCircle(minX, maxX, minZ, maxZ, worldPos.x, worldPos.z, radiusSq))
                            continue;

                        ChunkKey key = new ChunkKey(state.TileX, state.TileZ, chunkX, chunkZ);
                        bool isTrackedCorruption = IsChunkCorrupted(key) || MarkChunkCorrupted(key);
                        if (!isTrackedCorruption)
                            continue;

                        changed = true;
                        changed |= InvalidateChunkForCorruption(key);
                        if (TryGetDesiredChunkPriority(key, out float priority))
                            EnqueuePendingChunk(key, Mathf.Min(-1f, priority - 1f));
                    }
                }
            }

            if (changed)
                _activeSetDirty = true;
        }

        /// <summary>
        /// Samples biomass density immediately on the main thread from the current resident chunk-density snapshot.
        /// </summary>
        public float SampleBiomassDensityImmediate(Vector3 positionWS, int typeMask = DensityTypeMaskAll)
        {
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
                return 0f;

            if (!_densityQueryChunksNative.IsCreated || !_densityQueryGridNative.IsCreated || _densityQueryChunkCount <= 0)
                return 0f;

            float3 position = new float3(positionWS.x, positionWS.y, positionWS.z);
            return ApplyDensityTypeMask(
                SampleDensityChannelsAtPosition(position, _densityQueryChunksNative, _densityQueryGridNative, _densityQueryChunkCount),
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
        /// Returns a zero-allocation binary concealment state at the given world-space position.
        /// 0 = exposed, 1 = hidden by dense grass/sargassum cover while local threat remains low.
        /// </summary>
        public float GetPlayerVisibilityModifier(Vector3 position)
        {
            if (IsInsideRegisteredTerrainHole(position.x, position.z))
                return 0f;

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

        private void InitializeThreatGridMetadata()
        {
            int resolution = Mathf.RoundToInt((threatGridRadius * 2f) / Mathf.Max(1f, threatGridCellSize)) + 1;
            if ((resolution & 1) == 0)
                resolution++;

            _ecosystemThreatGridResolution = Mathf.Max(3, resolution);
            _ecosystemThreatGridCellCount = _ecosystemThreatGridResolution * _ecosystemThreatGridResolution;
            _ecosystemThreatGridResolutionY = Mathf.Max(2, Mathf.RoundToInt(thermalGridDepthMeters / Mathf.Max(1f, thermalGridVerticalCellSize)) + 1);
            long voxelCellCount = (long)_ecosystemThreatGridCellCount * _ecosystemThreatGridResolutionY;
            _ecosystemThreatVoxelCellCount = voxelCellCount > 0L && voxelCellCount <= int.MaxValue
                ? (int)voxelCellCount
                : 0;
        }

        private void InitializeCanopyGridMetadata()
        {
            int resolution = Mathf.RoundToInt((canopyGridRadius * 2f) / Mathf.Max(1f, canopyGridCellSize)) + 1;
            if ((resolution & 1) == 0)
                resolution++;

            _canopyGridResolution = Mathf.Max(3, resolution);
            _canopyGridCellCount = _canopyGridResolution * _canopyGridResolution;
        }

        private void InitializeThermalGridMetadata()
        {
            int horizontalResolution = Mathf.RoundToInt((thermalGridRadius * 2f) / Mathf.Max(1f, thermalGridHorizontalCellSize)) + 1;
            if ((horizontalResolution & 1) == 0)
                horizontalResolution++;

            int verticalResolution = Mathf.RoundToInt(thermalGridDepthMeters / Mathf.Max(1f, thermalGridVerticalCellSize)) + 1;
            _abyssalThermalGridResolutionXZ = Mathf.Max(3, horizontalResolution);
            _abyssalThermalGridResolutionY = Mathf.Max(2, verticalResolution);
            _abyssalThermalGridCellCount = _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionY;
        }

        private void EnsureThreatGridBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (!HasValidThreatGridConfiguration())
                return;

            if (!_ecosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatGridCurrentNative);
                // COLD ALLOC: NativeArray<float>[_ecosystemThreatGridCellCount] - ecosystem threat-grid front buffer for read-only sampling - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatGridCurrentNative = new NativeArray<float>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _threatGridInitialized = false;
            }

            if (!_ecosystemThreatGridNextNative.IsCreated || _ecosystemThreatGridNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatGridNextNative);
                // COLD ALLOC: NativeArray<float>[_ecosystemThreatGridCellCount] - ecosystem threat-grid back buffer for diffusion writes - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatGridNextNative = new NativeArray<float>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatGridCompressedCurrentNative.IsCreated || _ecosystemThreatGridCompressedCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatGridCompressedCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - compressed threat-grid front mirror for low-cost consumers - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatGridCompressedCurrentNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatGridCompressedNextNative.IsCreated || _ecosystemThreatGridCompressedNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatGridCompressedNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - compressed threat-grid back mirror for diffusion writes - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatGridCompressedNextNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatVoxelCurrentNative.IsCreated || _ecosystemThreatVoxelCurrentNative.Length != _ecosystemThreatVoxelCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatVoxelCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatVoxelCellCount] - 3D byte voxel threat snapshot front buffer used by AI DDA line-of-sight - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatVoxelCurrentNative = new NativeArray<byte>(_ecosystemThreatVoxelCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatVoxelNextNative.IsCreated || _ecosystemThreatVoxelNextNative.Length != _ecosystemThreatVoxelCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatVoxelNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatVoxelCellCount] - 3D byte voxel threat snapshot back buffer written by Burst voxelization - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatVoxelNextNative = new NativeArray<byte>(_ecosystemThreatVoxelCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatEchoCurrentNative.IsCreated || _ecosystemThreatEchoCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatEchoCurrentNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - permanent threat-echo flags aligned to the active threat grid - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatEchoCurrentNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_ecosystemThreatEchoNextNative.IsCreated || _ecosystemThreatEchoNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemThreatEchoNextNative);
                // COLD ALLOC: NativeArray<byte>[_ecosystemThreatGridCellCount] - back buffer for threat-echo propagation/shift - owner: HectonMapMagicVegetationBridge
                _ecosystemThreatEchoNextNative = new NativeArray<byte>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private bool HasValidThreatGridConfiguration()
        {
            return _ecosystemThreatGridResolution > 0 &&
                   _ecosystemThreatGridResolutionY > 0 &&
                   _ecosystemThreatGridCellCount > 0 &&
                   _ecosystemThreatVoxelCellCount > 0 &&
                   threatGridCellSize > 0f &&
                   thermalGridVerticalCellSize > 0f;
        }

        private void EnsureCanopyGridBuffer()
        {
            if (_canopyGridCellCount <= 0)
                InitializeCanopyGridMetadata();

            if (!_canopyHeightGridNative.IsCreated || _canopyHeightGridNative.Length != _canopyGridCellCount)
            {
                DisposeNativeArray(ref _canopyHeightGridNative);
                // COLD ALLOC: NativeArray<float>[_canopyGridCellCount] - global canopy-height mask for audio/light roof queries - owner: HectonMapMagicVegetationBridge
                _canopyHeightGridNative = new NativeArray<float>(_canopyGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _canopyGridInitialized = false;
            }
        }

        private void PrepareThreatSamplingSnapshot()
        {
            _threatSamplingChunkCount = 0;
            if (_densityQueryChunkCount <= 0 ||
                !_densityQueryChunksNative.IsCreated ||
                !_threatAttractorGridNative.IsCreated)
            {
                EnsureThreatSamplingChunkHashBuffersCapacity(1);
                _threatSamplingChunkHashBackNative.Clear();
                _threatSamplingChunkHashSwapPending = true;

                return;
            }

            _threatSamplingChunkCount = _densityQueryChunkCount;
            EnsureDensityChunkRecordCapacity(ref _threatSamplingChunksNative, _threatSamplingChunkCount);
            EnsureFloat2NativeCapacity(ref _threatSamplingAttractorGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            NativeArray<VegetationDensityChunkRecord>.Copy(_densityQueryChunksNative, _threatSamplingChunksNative, _threatSamplingChunkCount);
            NativeArray<float2>.Copy(_threatAttractorGridNative, _threatSamplingAttractorGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            Vector3 hashCenter = _threatGridInitialized
                ? _ecosystemThreatGridCenter
                : (playerTransform != null ? playerTransform.position : Vector3.zero);
            RebuildThreatSamplingChunkHash(hashCenter);
        }

        private void RebuildThreatSamplingChunkHash(Vector3 gridCenter)
        {
            if (_threatSamplingChunkCount <= 0 ||
                !_threatSamplingChunksNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f)
            {
                EnsureThreatSamplingChunkHashBuffersCapacity(1);
                _threatSamplingChunkHashBackNative.Clear();
                _threatSamplingChunkHashSwapPending = true;

                return;
            }

            int hashCapacity = 0;
            for (int i = 0; i < _threatSamplingChunkCount; i++)
                hashCapacity += EstimateThreatSamplingChunkHashEntries(_threatSamplingChunksNative[i], gridCenter);

            EnsureThreatSamplingChunkHashBuffersCapacity(hashCapacity);

            for (int i = 0; i < _threatSamplingChunkCount; i++)
                StampThreatSamplingChunkHash(_threatSamplingChunksNative[i], gridCenter, i);
            _threatSamplingChunkHashSwapPending = true;
        }

        private int EstimateThreatSamplingChunkHashEntries(VegetationDensityChunkRecord chunk, Vector3 gridCenter)
        {
            if (_ecosystemThreatGridResolution <= 0 || threatGridCellSize <= 0f)
                return 0;

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = Mathf.Max(chunk.MinX, minGridX);
            float maxX = Mathf.Min(chunk.MaxX, maxGridX);
            float minZ = Mathf.Max(chunk.MinZ, minGridZ);
            float maxZ = Mathf.Min(chunk.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return 0;

            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.FloorToInt((maxX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.FloorToInt((maxZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            return Mathf.Max(0, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));
        }

        private void StampThreatSamplingChunkHash(VegetationDensityChunkRecord chunk, Vector3 gridCenter, int chunkIndex)
        {
            if (!_threatSamplingChunkHashBackNative.IsCreated || _ecosystemThreatGridResolution <= 0 || threatGridCellSize <= 0f)
                return;

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = Mathf.Max(chunk.MinX, minGridX);
            float maxX = Mathf.Min(chunk.MaxX, maxGridX);
            float minZ = Mathf.Max(chunk.MinZ, minGridZ);
            float maxZ = Mathf.Min(chunk.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return;

            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.FloorToInt((maxX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.FloorToInt((maxZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _ecosystemThreatGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    _threatSamplingChunkHashBackNative.Add(rowOffset + cellX, chunkIndex);
            }
        }

        private void RebuildArtificialStructureThreatSnapshot()
        {
            Vector3 targetCenter = playerTransform != null
                ? playerTransform.position
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);

            int targetCount = _persistentArtificialStructures.Count + _megaWreckStreamCount;
            if (targetCount <= 0)
            {
                _artificialStructureCount = 0;
                EnsureArtificialStructureHashBuffersCapacity(1);
                _artificialStructureHashBackNative.Clear();
                _artificialStructureHashSwapPending = true;

                return;
            }

            EnsureNativeCapacity(ref _artificialStructureRecordsNative, targetCount);
            int estimatedHashEntries = 0;
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
                estimatedHashEntries += EstimateArtificialStructureHashEntries(_persistentArtificialStructures[i].Bounds, targetCenter);

            for (int i = 0; i < _megaWreckStreamCount; i++)
                estimatedHashEntries += EstimateArtificialStructureHashEntries(GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]), targetCenter);

            EnsureArtificialStructureHashBuffersCapacity(estimatedHashEntries);

            int writeIndex = 0;
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                WriteArtificialStructureRecord(structure.Bounds, structure.Type, targetCenter, writeIndex);
                writeIndex++;
            }

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                WriteArtificialStructureRecord(
                    GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]),
                    StructureType.MegaWreck,
                    targetCenter,
                    writeIndex);
                writeIndex++;
            }

            _artificialStructureCount = writeIndex;
            _artificialStructureHashSwapPending = true;
        }

        private void WriteArtificialStructureRecord(Bounds bounds, StructureType type, Vector3 gridCenter, int writeIndex)
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
            _artificialStructureRecordsNative[writeIndex] = record;
            StampArtificialStructureHash(record, gridCenter, writeIndex);
        }

        private int EstimateArtificialStructureHashEntries(Bounds bounds, Vector3 gridCenter)
        {
            if (_ecosystemThreatGridResolution <= 0 || threatGridCellSize <= 0f)
                return 0;

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = Mathf.Max(bounds.min.x, minGridX);
            float maxX = Mathf.Min(bounds.max.x, maxGridX);
            float minZ = Mathf.Max(bounds.min.z, minGridZ);
            float maxZ = Mathf.Min(bounds.max.z, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return 0;

            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.FloorToInt((maxX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.FloorToInt((maxZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            return Mathf.Max(0, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));
        }

        private void StampArtificialStructureHash(ArtificialStructureRecord record, Vector3 gridCenter, int recordIndex)
        {
            if (!_artificialStructureHashBackNative.IsCreated || _ecosystemThreatGridResolution <= 0 || threatGridCellSize <= 0f)
                return;

            GetThreatGridBounds(gridCenter, out float minGridX, out float maxGridX, out float minGridZ, out float maxGridZ);
            float minX = Mathf.Max(record.MinX, minGridX);
            float maxX = Mathf.Min(record.MaxX, maxGridX);
            float minZ = Mathf.Max(record.MinZ, minGridZ);
            float maxZ = Mathf.Min(record.MaxZ, maxGridZ);
            if (minX > maxX || minZ > maxZ)
                return;

            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.FloorToInt((maxX - minGridX) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.FloorToInt((maxZ - minGridZ) / threatGridCellSize), 0, _ecosystemThreatGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _ecosystemThreatGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    _artificialStructureHashBackNative.Add(rowOffset + cellX, recordIndex);
            }
        }

        private void EnsureThreatSamplingChunkHashBuffersCapacity(int requiredCapacity)
        {
            int safeCapacity = Mathf.Max(1, requiredCapacity);
            if (!_threatSamplingChunkHashFrontNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - threat-sampling chunk spatial hash front buffer for Burst readers - owner: HectonMapMagicVegetationBridge
                _threatSamplingChunkHashFrontNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
            }

            if (!_threatSamplingChunkHashBackNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - threat-sampling chunk spatial hash back buffer for SlowTick rebuilds - owner: HectonMapMagicVegetationBridge
                _threatSamplingChunkHashBackNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
            }
            else if (_threatSamplingChunkHashBackNative.Capacity < safeCapacity)
            {
                _threatSamplingChunkHashBackNative.Capacity = safeCapacity;
            }

            _threatSamplingChunkHashBackNative.Clear();
        }

        private void EnsureArtificialStructureHashBuffersCapacity(int requiredCapacity)
        {
            int safeCapacity = Mathf.Max(1, requiredCapacity);
            if (!_artificialStructureHashFrontNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - artificial-structure threat hash front buffer for Burst readers - owner: HectonMapMagicVegetationBridge
                _artificialStructureHashFrontNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
            }

            if (!_artificialStructureHashBackNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - artificial-structure threat hash back buffer for SlowTick rebuilds - owner: HectonMapMagicVegetationBridge
                _artificialStructureHashBackNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
            }
            else if (_artificialStructureHashBackNative.Capacity < safeCapacity)
            {
                _artificialStructureHashBackNative.Capacity = safeCapacity;
            }

            _artificialStructureHashBackNative.Clear();
        }

        private void SwapThreatSamplingChunkHashBuffers()
        {
            NativeParallelMultiHashMap<int, int> hashSwap = _threatSamplingChunkHashFrontNative;
            _threatSamplingChunkHashFrontNative = _threatSamplingChunkHashBackNative;
            _threatSamplingChunkHashBackNative = hashSwap;
        }

        private void SwapArtificialStructureHashBuffers()
        {
            NativeParallelMultiHashMap<int, int> hashSwap = _artificialStructureHashFrontNative;
            _artificialStructureHashFrontNative = _artificialStructureHashBackNative;
            _artificialStructureHashBackNative = hashSwap;
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

        private void PrepareFlowFieldSamplingSnapshot(Vector3 flowCenter)
        {
            EnsureThreatGridBuffers();
            EnsureFloat3Capacity(ref _flowSamplingDensityGridNative, Mathf.Max(1, _threatSamplingChunkCount * DensityGridCellCount));
            EnsureFloatNativeCapacity(ref _flowNavSupportGridNative, Mathf.Max(1, _ecosystemThreatGridCellCount));
            ClearFloatGrid(_flowNavSupportGridNative, _ecosystemThreatGridCellCount);

            if (_threatSamplingChunkCount <= 0 ||
                !_densityQueryGridNative.IsCreated ||
                _densityQueryChunkCount <= 0)
            {
                return;
            }

            NativeArray<float3>.Copy(_densityQueryGridNative, _flowSamplingDensityGridNative, _threatSamplingChunkCount * DensityGridCellCount);
            BuildFlowFieldNavSupportGrid(flowCenter);
        }

        private void CompleteThreatPropagationJob(bool forceComplete)
        {
            if (!_threatPropagationScheduled)
                return;

            if (!forceComplete && !_threatPropagationHandle.IsCompleted)
                return;

            _threatPropagationHandle.Complete();
            NativeArray<float> threatSwap = _ecosystemThreatGridCurrentNative;
            _ecosystemThreatGridCurrentNative = _ecosystemThreatGridNextNative;
            _ecosystemThreatGridNextNative = threatSwap;
            NativeArray<byte> threatCompressedSwap = _ecosystemThreatGridCompressedCurrentNative;
            _ecosystemThreatGridCompressedCurrentNative = _ecosystemThreatGridCompressedNextNative;
            _ecosystemThreatGridCompressedNextNative = threatCompressedSwap;
            NativeArray<byte> threatVoxelSwap = _ecosystemThreatVoxelCurrentNative;
            _ecosystemThreatVoxelCurrentNative = _ecosystemThreatVoxelNextNative;
            _ecosystemThreatVoxelNextNative = threatVoxelSwap;
            NativeArray<byte> echoSwap = _ecosystemThreatEchoCurrentNative;
            _ecosystemThreatEchoCurrentNative = _ecosystemThreatEchoNextNative;
            _ecosystemThreatEchoNextNative = echoSwap;
            _ecosystemThreatGridCenter = _scheduledThreatGridCenter;
            _ecosystemThreatVoxelOrigin = _scheduledThreatVoxelOrigin;
            _threatGridInitialized = true;
            _threatPropagationScheduled = false;
            if (InvalidateChunksForNewPermanentEchoes())
                RefreshResidency();
            UpdateThreatHotspot();
        }

        private void ScheduleThreatPropagationJob()
        {
            if (_threatPropagationScheduled)
                return;

            EnsureThreatGridBuffers();
            if (!HasValidThreatGridConfiguration())
                return;

            Vector3 targetCenter = playerTransform != null
                ? playerTransform.position
                : (_threatGridInitialized ? _ecosystemThreatGridCenter : Vector3.zero);
            Vector3 previousCenter = _threatGridInitialized ? _ecosystemThreatGridCenter : targetCenter;
            ResolveThreatSignalSnapshot(out Vector3 emissionPosition, out float emissionRadius, out float emissionStrength);

            float deltaTime = 0.5f;
            if (_lastThreatPropagationTime > float.NegativeInfinity)
                deltaTime = Mathf.Clamp(Time.time - _lastThreatPropagationTime, 0.05f, 5f);

            int shiftX = Mathf.RoundToInt((targetCenter.x - previousCenter.x) / threatGridCellSize);
            int shiftZ = Mathf.RoundToInt((targetCenter.z - previousCenter.z) / threatGridCellSize);
            float halfExtent = (_ecosystemThreatGridResolution - 1) * 0.5f * threatGridCellSize;
            Vector3 voxelOrigin = new Vector3(
                targetCenter.x - halfExtent,
                waterLevel - thermalGridDepthMeters,
                targetCenter.z - halfExtent);

            var job = new ThreatPropagationJob
            {
                CurrentThreat = _ecosystemThreatGridCurrentNative,
                NextThreat = _ecosystemThreatGridNextNative,
                NextThreatCompressed = _ecosystemThreatGridCompressedNextNative,
                CurrentEchoFlags = _ecosystemThreatEchoCurrentNative,
                NextEchoFlags = _ecosystemThreatEchoNextNative,
                ThreatChunks = _threatSamplingChunksNative,
                ThreatAttractorGrid = _threatSamplingAttractorGridNative,
                ArtificialStructures = _artificialStructureRecordsNative,
                ArtificialStructureHash = _artificialStructureHashFrontNative,
                GridResolution = _ecosystemThreatGridResolution,
                ThreatChunkCount = _threatSamplingChunkCount,
                CellSize = threatGridCellSize,
                DeltaTime = deltaTime,
                Diffusion = threatDiffusion,
                DecayPerSecond = threatDecayPerSecond,
                SargassumRetentionBoost = threatSargassumRetentionBoost,
                TechnoJungleRetentionBoost = threatTechnoJungleRetentionBoost,
                SargassumAccumulationBoost = threatSargassumAccumulationBoost,
                TechnoJungleAccumulationBoost = threatTechnoJungleAccumulationBoost,
                StructureThreatSuppression = artificialStructureThreatSuppression,
                StructureHazardAttraction = artificialStructureHazardAttraction,
                PermanentEchoFloor = permanentThreatEchoFloor,
                PermanentEchoThreshold = permanentThreatEchoThreshold,
                EmissionPosition = new float3(emissionPosition.x, emissionPosition.y, emissionPosition.z),
                GridCenter = new float3(targetCenter.x, targetCenter.y, targetCenter.z),
                EmissionRadius = emissionRadius,
                EmissionStrength = emissionStrength,
                ShiftX = shiftX,
                ShiftZ = shiftZ
            };

            _scheduledThreatGridCenter = targetCenter;
            _scheduledThreatVoxelOrigin = voxelOrigin;
            _lastThreatPropagationTime = Time.time;
            JobHandle propagationHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);
            var voxelJob = new ThreatVoxelizationJob
            {
                ThreatGrid = _ecosystemThreatGridNextNative,
                DensityChunks = _threatSamplingChunksNative,
                DensityGrid = _densityQueryGridNative,
                ThreatAttractorGrid = _threatSamplingAttractorGridNative,
                ChunkHash = _threatSamplingChunkHashFrontNative,
                ArtificialStructures = _artificialStructureRecordsNative,
                ArtificialStructureHash = _artificialStructureHashFrontNative,
                Output = _ecosystemThreatVoxelNextNative,
                GridResolutionXZ = _ecosystemThreatGridResolution,
                GridResolutionY = _ecosystemThreatGridResolutionY,
                CellSizeXZ = threatGridCellSize,
                CellSizeY = thermalGridVerticalCellSize,
                GridOrigin = new float3(voxelOrigin.x, voxelOrigin.y, voxelOrigin.z),
                GridCenter = new float3(targetCenter.x, targetCenter.y, targetCenter.z),
                KelpObstacleWeight = flowFieldKelpObstacleWeight,
                SargassumObstacleWeight = flowFieldSargassumObstacleWeight,
                TechnoObstacleWeight = flowFieldTechnoObstacleWeight,
                ObstacleHardThreshold = flowFieldObstacleHardThreshold
            };
            _threatPropagationHandle = voxelJob.Schedule(_ecosystemThreatVoxelCellCount, DefaultJobBatchSize, propagationHandle);
            _threatPropagationScheduled = true;
        }

        private void EnsureFlowFieldBuffers()
        {
            if (_ecosystemThreatGridCellCount <= 0)
                InitializeThreatGridMetadata();

            if (!_ecosystemFlowFieldCurrentNative.IsCreated || _ecosystemFlowFieldCurrentNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemFlowFieldCurrentNative);
                // COLD ALLOC: NativeArray<float2>[_ecosystemThreatGridCellCount] - abyssal flow-field front buffer for read-only navigation sampling - owner: HectonMapMagicVegetationBridge
                _ecosystemFlowFieldCurrentNative = new NativeArray<float2>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _flowFieldInitialized = false;
            }

            if (!_ecosystemFlowFieldNextNative.IsCreated || _ecosystemFlowFieldNextNative.Length != _ecosystemThreatGridCellCount)
            {
                DisposeNativeArray(ref _ecosystemFlowFieldNextNative);
                // COLD ALLOC: NativeArray<float2>[_ecosystemThreatGridCellCount] - abyssal flow-field back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _ecosystemFlowFieldNextNative = new NativeArray<float2>(_ecosystemThreatGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_swarmWakeImpulseNative.IsCreated)
            {
                // COLD ALLOC: NativeArray<SwarmWakeImpulse>[1] - single-slot boid wake impulse injected into abyssal flow-field solves - owner: HectonMapMagicVegetationBridge
                _swarmWakeImpulseNative = new NativeArray<SwarmWakeImpulse>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _swarmWakeImpulseCount = 0;
                _swarmWakeImpulseExpireTime = float.NegativeInfinity;
            }
        }

        private void EnsureThermalGridBuffers()
        {
            if (_abyssalThermalGridCellCount <= 0)
                InitializeThermalGridMetadata();

            if (!_abyssalThermalGridNative.IsCreated || _abyssalThermalGridNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _abyssalThermalGridNative);
                // COLD ALLOC: NativeArray<float>[_abyssalThermalGridCellCount] - abyssal thermal-grid front buffer for stable sampling - owner: HectonMapMagicVegetationBridge
                _abyssalThermalGridNative = new NativeArray<float>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _abyssalThermalGridInitialized = false;
                _abyssalThermalGridRingOffsetX = 0;
                _abyssalThermalGridRingOffsetY = 0;
                _abyssalThermalGridRingOffsetZ = 0;
            }

            if (!_abyssalThermalGridNextNative.IsCreated || _abyssalThermalGridNextNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _abyssalThermalGridNextNative);
                // COLD ALLOC: NativeArray<float>[_abyssalThermalGridCellCount] - abyssal thermal-grid back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _abyssalThermalGridNextNative = new NativeArray<float>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_abyssalFlowVolumeCurrentNative.IsCreated || _abyssalFlowVolumeCurrentNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _abyssalFlowVolumeCurrentNative);
                // COLD ALLOC: NativeArray<float3>[_abyssalThermalGridCellCount] - abyssal 3D flow-volume front buffer for deep-current sampling - owner: HectonMapMagicVegetationBridge
                _abyssalFlowVolumeCurrentNative = new NativeArray<float3>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _abyssalFlowVolumeInitialized = false;
            }

            if (!_abyssalFlowVolumeNextNative.IsCreated || _abyssalFlowVolumeNextNative.Length != _abyssalThermalGridCellCount)
            {
                DisposeNativeArray(ref _abyssalFlowVolumeNextNative);
                // COLD ALLOC: NativeArray<float3>[_abyssalThermalGridCellCount] - abyssal 3D flow-volume back buffer for Burst writes - owner: HectonMapMagicVegetationBridge
                _abyssalFlowVolumeNextNative = new NativeArray<float3>(_abyssalThermalGridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void CompleteFlowFieldJob(bool forceComplete)
        {
            if (!_flowFieldScheduled)
                return;

            if (!forceComplete && !_flowFieldHandle.IsCompleted)
                return;

            _flowFieldHandle.Complete();
            NativeArray<float2> flowSwap = _ecosystemFlowFieldCurrentNative;
            _ecosystemFlowFieldCurrentNative = _ecosystemFlowFieldNextNative;
            _ecosystemFlowFieldNextNative = flowSwap;
            _ecosystemFlowFieldCenter = _scheduledFlowFieldCenter;
            _flowFieldInitialized = true;
            _flowFieldScheduled = false;
        }

        private void CompleteThermalGridJob(bool forceComplete)
        {
            if (!_abyssalThermalGridScheduled)
                return;

            if (!forceComplete && !_abyssalThermalGridHandle.IsCompleted)
                return;

            _abyssalThermalGridHandle.Complete();
            bool canComparePreviousFlowVolume =
                _abyssalFlowVolumeInitialized &&
                _abyssalFlowVolumeCurrentNative.IsCreated &&
                _abyssalFlowVolumeNextNative.IsCreated &&
                _abyssalThermalGridResolutionXZ > 2 &&
                _abyssalThermalGridResolutionY > 2 &&
                (_scheduledAbyssalThermalGridCenter - _abyssalThermalGridCenter).sqrMagnitude <=
                (thermalGridHorizontalCellSize * thermalGridHorizontalCellSize);
            bool shouldTriggerBiolumeSurge = canComparePreviousFlowVolume &&
                                             DetectBiolumeSurgeCluster3D(
                                                 _abyssalFlowVolumeCurrentNative,
                                                 _abyssalFlowVolumeNextNative,
                                                 _abyssalThermalGridResolutionXZ,
                                                 _abyssalThermalGridResolutionY,
                                                 BiolumeSurgeVelocityDeltaThreshold);
            NativeArray<float> thermalSwap = _abyssalThermalGridNative;
            _abyssalThermalGridNative = _abyssalThermalGridNextNative;
            _abyssalThermalGridNextNative = thermalSwap;
            NativeArray<float3> flowVolumeSwap = _abyssalFlowVolumeCurrentNative;
            _abyssalFlowVolumeCurrentNative = _abyssalFlowVolumeNextNative;
            _abyssalFlowVolumeNextNative = flowVolumeSwap;
            _abyssalThermalGridCenter = _scheduledAbyssalThermalGridCenter;
            _abyssalThermalGridInitialized = true;
            _abyssalFlowVolumeInitialized = true;
            _abyssalThermalGridScheduled = false;

            if (shouldTriggerBiolumeSurge)
                TryRegisterBiolumeSurge(BiolumeSurgeDurationSeconds);
        }

        private void ScheduleFlowFieldJob()
        {
            if (_flowFieldScheduled)
                return;

            EnsureFlowFieldBuffers();
            if (_swarmWakeImpulseCount > 0 &&
                (!float.IsFinite(_swarmWakeImpulseExpireTime) || Time.unscaledTime > _swarmWakeImpulseExpireTime))
            {
                _swarmWakeImpulseCount = 0;
                if (_swarmWakeImpulseNative.IsCreated)
                    _swarmWakeImpulseNative[0] = default;
            }

            Vector3 flowCenter = _threatGridInitialized
                ? _ecosystemThreatGridCenter
                : (playerTransform != null ? playerTransform.position : Vector3.zero);
            PrepareFlowFieldSamplingSnapshot(flowCenter);

            Vector3 playerPosition = playerTransform != null ? playerTransform.position : flowCenter;
            Vector3 hotspotPosition = _currentThreatHotspotLevel >= flowFieldHotspotMinimumThreat
                ? _currentThreatHotspotPosition
                : playerPosition;
            float hotspotThreatLevel = _currentThreatHotspotLevel >= flowFieldHotspotMinimumThreat
                ? _currentThreatHotspotLevel
                : 0f;
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float2 weatherDirectionXZ = math.normalizesafe(weatherSnapshot.CurrentMeta.GlobalBaseVector.xz, new float2(0f, 1f));

            var job = new BuildAbyssalFlowFieldJob
            {
                ThreatGrid = _ecosystemThreatGridCurrentNative,
                FlowChunks = _threatSamplingChunksNative,
                FlowDensityGrid = _flowSamplingDensityGridNative,
                ThreatAttractorGrid = _threatSamplingAttractorGridNative,
                ChunkHash = _threatSamplingChunkHashFrontNative,
                NavSupportGrid = _flowNavSupportGridNative,
                ExternalWakeImpulses = _swarmWakeImpulseNative,
                Output = _ecosystemFlowFieldNextNative,
                GridResolution = _ecosystemThreatGridResolution,
                ChunkCount = _threatSamplingChunkCount,
                ExternalWakeImpulseCount = _swarmWakeImpulseCount,
                CellSize = threatGridCellSize,
                GridCenter = new float3(flowCenter.x, flowCenter.y, flowCenter.z),
                PlayerPosition = new float3(playerPosition.x, playerPosition.y, playerPosition.z),
                HotspotPosition = new float3(hotspotPosition.x, hotspotPosition.y, hotspotPosition.z),
                HotspotThreatLevel = hotspotThreatLevel,
                WeatherStateMask = (uint)weatherSnapshot.StateMask,
                WeatherDirectionXZ = weatherDirectionXZ,
                WeatherCurrentSpeed = math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale),
                WeatherIntensity = math.max(0f, weatherSnapshot.WeatherIntensity),
                ThreatBias = flowFieldThreatBias,
                PlayerBias = flowFieldPlayerBias,
                HotspotBias = flowFieldHotspotBias,
                ObstacleAvoidBias = flowFieldObstacleAvoidBias,
                NavSupportBias = flowFieldNavSupportBias,
                KelpObstacleWeight = flowFieldKelpObstacleWeight,
                SargassumObstacleWeight = flowFieldSargassumObstacleWeight,
                TechnoObstacleWeight = flowFieldTechnoObstacleWeight,
                ObstacleSoftThreshold = flowFieldObstacleSoftThreshold,
                ObstacleHardThreshold = flowFieldObstacleHardThreshold
            };

            _scheduledFlowFieldCenter = flowCenter;
            _flowFieldHandle = job.Schedule(_ecosystemThreatGridCellCount, DefaultJobBatchSize);
            _flowFieldScheduled = true;
        }

        private void ScheduleThermalGridJob()
        {
            if (_abyssalThermalGridScheduled)
                return;

            EnsureThermalGridBuffers();
            WeatherRuntimeSnapshot weatherSnapshot = ResolveWeatherSnapshot();
            float2 weatherDirectionXZ = math.normalizesafe(weatherSnapshot.CurrentMeta.GlobalBaseVector.xz, new float2(0f, 1f));

            Vector3 thermalCenter = playerTransform != null
                ? new Vector3(playerTransform.position.x, waterLevel - (thermalGridDepthMeters * 0.5f), playerTransform.position.z)
                : (_abyssalThermalGridInitialized
                    ? _abyssalThermalGridCenter
                    : new Vector3(0f, waterLevel - (thermalGridDepthMeters * 0.5f), 0f));

            var job = new BuildAbyssalThermalGridJob
            {
                Output = _abyssalThermalGridNextNative,
                ThreatChunks = _threatSamplingChunksNative,
                ThreatAttractorGrid = _threatSamplingAttractorGridNative,
                ChunkCount = _threatSamplingChunkCount,
                HorizontalResolution = _abyssalThermalGridResolutionXZ,
                VerticalResolution = _abyssalThermalGridResolutionY,
                HorizontalCellSize = thermalGridHorizontalCellSize,
                VerticalCellSize = thermalGridVerticalCellSize,
                WaterLevel = waterLevel,
                GridDepthMeters = thermalGridDepthMeters,
                GridCenter = new float3(thermalCenter.x, thermalCenter.y, thermalCenter.z),
                SurfaceTemperatureCelsius = thermalSurfaceTemperatureCelsius,
                AbyssTemperatureCelsius = thermalAbyssTemperatureCelsius,
                ThermoclineDepth = thermalThermoclineDepth,
                DepthFalloffExponent = thermalDepthFalloffExponent,
                ColonyBiomeStartDepth = colonyBiomeStartDepth,
                DeadZoneStartDepth = deadZoneStartDepth,
                HotPocketBoostCelsius = thermalHotPocketBoostCelsius,
                HotPocketNoiseScale = thermalHotPocketNoiseScale,
                HotPocketThreshold = thermalHotPocketThreshold,
                ColonyPocketStrength = thermalColonyPocketStrength,
                DeadZonePocketStrength = thermalDeadZonePocketStrength,
                RingOffsetX = _abyssalThermalGridRingOffsetX,
                RingOffsetY = _abyssalThermalGridRingOffsetY,
                RingOffsetZ = _abyssalThermalGridRingOffsetZ
            };

            var flowVolumeJob = new BuildAbyssalFlowVolumeJob
            {
                ThermalGrid = _abyssalThermalGridNextNative,
                ExternalWakeImpulses = _swarmWakeImpulseNative,
                Output = _abyssalFlowVolumeNextNative,
                HorizontalResolution = _abyssalThermalGridResolutionXZ,
                VerticalResolution = _abyssalThermalGridResolutionY,
                RingOffsetX = _abyssalThermalGridRingOffsetX,
                RingOffsetY = _abyssalThermalGridRingOffsetY,
                RingOffsetZ = _abyssalThermalGridRingOffsetZ,
                ExternalWakeImpulseCount = _swarmWakeImpulseCount,
                HorizontalCellSize = thermalGridHorizontalCellSize,
                VerticalCellSize = thermalGridVerticalCellSize,
                WaterLevel = waterLevel,
                GridDepthMeters = thermalGridDepthMeters,
                ThermoclineDepthMeters = 120f,
                WeatherStateMask = (uint)weatherSnapshot.StateMask,
                WeatherDirectionXZ = weatherDirectionXZ,
                WeatherCurrentSpeed = math.max(0f, weatherSnapshot.CurrentMeta.GlobalScale),
                WeatherIntensity = math.max(0f, weatherSnapshot.WeatherIntensity),
                ThermalIntensity = math.max(0f, weatherSnapshot.CurrentMeta.ThermalIntensity),
                GridCenter = new float3(thermalCenter.x, thermalCenter.y, thermalCenter.z)
            };

            _scheduledAbyssalThermalGridCenter = thermalCenter;
            JobHandle thermalHandle = job.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize);
            _abyssalThermalGridHandle = flowVolumeJob.Schedule(_abyssalThermalGridCellCount, DefaultJobBatchSize, thermalHandle);
            _abyssalThermalGridScheduled = true;
        }

        private static WeatherRuntimeSnapshot ResolveWeatherSnapshot()
        {
            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService == null || !weatherService.IsInitialized)
                return default;

            return weatherService.GetRuntimeSnapshot();
        }

        private static bool DetectBiolumeSurgeCluster3D(
            NativeArray<float3> previousField,
            NativeArray<float3> currentField,
            int horizontalResolution,
            int verticalResolution,
            float velocityDeltaThreshold)
        {
            if (!previousField.IsCreated ||
                !currentField.IsCreated ||
                horizontalResolution <= 2 ||
                verticalResolution <= 2)
            {
                return false;
            }

            int cellsPerLayer = horizontalResolution * horizontalResolution;
            int requiredLength = cellsPerLayer * verticalResolution;
            if (previousField.Length < requiredLength || currentField.Length < requiredLength)
                return false;

            for (int cellY = 1; cellY < verticalResolution - 1; cellY++)
            {
                int layerOffset = cellY * cellsPerLayer;
                for (int cellZ = 1; cellZ < horizontalResolution - 1; cellZ++)
                {
                    int rowOffset = layerOffset + (cellZ * horizontalResolution);
                    for (int cellX = 1; cellX < horizontalResolution - 1; cellX++)
                    {
                        float previousMaxSpeed = 0f;
                        float currentMaxSpeed = 0f;
                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            int sampleLayerOffset = (cellY + offsetY) * cellsPerLayer;
                            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                            {
                                int sampleRowOffset = sampleLayerOffset + ((cellZ + offsetZ) * horizontalResolution);
                                for (int offsetX = -1; offsetX <= 1; offsetX++)
                                {
                                    int sampleIndex = sampleRowOffset + cellX + offsetX;
                                    previousMaxSpeed = math.max(previousMaxSpeed, math.length(previousField[sampleIndex]));
                                    currentMaxSpeed = math.max(currentMaxSpeed, math.length(currentField[sampleIndex]));
                                }
                            }
                        }

                        if (math.abs(currentMaxSpeed - previousMaxSpeed) > velocityDeltaThreshold)
                            return true;
                    }
                }
            }

            return false;
        }

        private static void TryRegisterBiolumeSurge(float durationSeconds)
        {
            if (GlobalRegistry.Weather is GlobalWeatherDirector weatherDirector && weatherDirector.IsInitialized)
                weatherDirector.RegisterBiolumeSurge(durationSeconds);
        }

        private void ResolveThreatSignalSnapshot(out Vector3 emissionPosition, out float emissionRadius, out float emissionStrength)
        {
            emissionPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            emissionRadius = 0f;
            emissionStrength = 0f;

            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal signal))
            {
                emissionPosition = signal.Position;
                float movementSpeed = Mathf.Sqrt(Mathf.Max(0f, signal.MovementSpeedSqr));
                float movement01 = Mathf.InverseLerp(0.5f, 8.5f, movementSpeed);
                float tool01 = Mathf.Clamp01(signal.ToolUseNoise01);
                float transport01 = Mathf.Clamp01(signal.TransportBoost01 * Mathf.Max(1f, signal.TransportSignature));
                float flashlight01 = signal.FlashlightOn ? 1f : 0f;
                float radius01 = Mathf.Clamp01(Mathf.Max(Mathf.Max(movement01, tool01), Mathf.Max(signal.TransportBoost01, flashlight01 * 0.7f)));
                emissionRadius = Mathf.Lerp(threatEmissionRadiusMin, threatEmissionRadiusMax, radius01);
                emissionStrength =
                    (movement01 * threatNoiseDepositPerSecond) +
                    ((tool01 + transport01) * threatPulseDepositPerSecond) +
                    (flashlight01 * threatFlashlightDepositPerSecond);
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            if (playerTransform == null)
            {
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            float fallbackMovement01 = Mathf.InverseLerp(0.5f, 8.5f, _playerVelocity.magnitude);
            if (fallbackMovement01 <= 0f)
            {
                ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
                return;
            }

            emissionRadius = Mathf.Lerp(threatEmissionRadiusMin, threatEmissionRadiusMax, fallbackMovement01);
            emissionStrength = fallbackMovement01 * threatNoiseDepositPerSecond;
            ApplyExternalThreatPulseToSnapshot(ref emissionPosition, ref emissionRadius, ref emissionStrength);
        }

        private void ApplyExternalThreatPulseToSnapshot(ref Vector3 emissionPosition, ref float emissionRadius, ref float emissionStrength)
        {
            if (_externalThreatPulseHoldTimer <= 0f || _externalThreatPulseStrength <= 0f || _externalThreatPulseRadius <= 0f)
                return;

            emissionPosition = _externalThreatPulsePosition;
            emissionRadius = Mathf.Max(emissionRadius, _externalThreatPulseRadius);
            emissionStrength = Mathf.Max(emissionStrength, _externalThreatPulseStrength);
        }

        private void UpdateThreatHotspot()
        {
            _currentThreatHotspotLevel = 0f;
            _currentThreatHotspotPosition = _ecosystemThreatGridCenter;
            if (!_ecosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridResolution <= 0)
                return;

            int bestIndex = -1;
            float bestThreat = 0f;
            for (int i = 0; i < _ecosystemThreatGridCellCount; i++)
            {
                float threat = _ecosystemThreatGridCurrentNative[i];
                if (threat <= bestThreat)
                    continue;

                bestThreat = threat;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int bestX = bestIndex % _ecosystemThreatGridResolution;
            int bestZ = bestIndex / _ecosystemThreatGridResolution;
            _currentThreatHotspotLevel = bestThreat;
            _currentThreatHotspotPosition = new Vector3(
                _ecosystemThreatGridCenter.x + ((bestX - halfExtent) * threatGridCellSize),
                playerTransform != null ? playerTransform.position.y : _ecosystemThreatGridCenter.y,
                _ecosystemThreatGridCenter.z + ((bestZ - halfExtent) * threatGridCellSize));
        }

        private NativeArray<float> GetThreatGridFloatView()
        {
            if (!_threatGridInitialized || !_ecosystemThreatGridCurrentNative.IsCreated || _ecosystemThreatGridCellCount <= 0)
                return default;

            return _ecosystemThreatGridCurrentNative;
        }

        private ChunkAbyssalNavPayload BuildChunkAbyssalNavPayload(ChunkKey key, ChunkBuildJobState jobState, ChunkPayload payload)
        {
            ChunkAbyssalNavPayload navPayload = default;
            if (jobState == null || !payload.HasUnderwater)
                return navPayload;

            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) ||
                !SliceContainsDeepBiome(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount))
            {
                return navPayload;
            }

            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            int sampleCountX = Mathf.Max(1, Mathf.FloorToInt(chunkWidth / abyssalNavNodeStepMeters));
            int sampleCountZ = Mathf.Max(1, Mathf.FloorToInt(chunkDepth / abyssalNavNodeStepMeters));
            int holeNodeCount = CountTerrainHolesIntersectingChunk(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ);
            int maxNodeCount = sampleCountX * sampleCountZ + holeNodeCount;
            if (maxNodeCount <= 0)
                return navPayload;

            NativeArray<Vector3> nodes = default;
            NativeArray<Vector3> conduitVectors = default;
            NativeArray<float> conduitStrengths = default;
            NativeArray<byte> nodeTypes = default;
            bool hasExistingPayload = _chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload existingPayload);
            bool reusedExistingPayload = hasExistingPayload &&
                existingPayload.Nodes.IsCreated &&
                existingPayload.Nodes.Length >= maxNodeCount;
            if (reusedExistingPayload)
            {
                nodes = existingPayload.Nodes;
                if (existingPayload.ConduitVectors.IsCreated && existingPayload.ConduitVectors.Length >= maxNodeCount)
                {
                    conduitVectors = existingPayload.ConduitVectors;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.ConduitVectors);
                }

                if (existingPayload.ConduitStrengths.IsCreated && existingPayload.ConduitStrengths.Length >= maxNodeCount)
                {
                    conduitStrengths = existingPayload.ConduitStrengths;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.ConduitStrengths);
                }

                if (existingPayload.NodeTypes.IsCreated && existingPayload.NodeTypes.Length >= maxNodeCount)
                {
                    nodeTypes = existingPayload.NodeTypes;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.NodeTypes);
                }
            }
            else if (hasExistingPayload && existingPayload.Nodes.IsCreated)
            {
                DisposeChunkAbyssalNavPayload(ref existingPayload);
            }

            EnsureInactiveNativeCapacity(ref nodes, maxNodeCount);

            EnsureInactiveNativeCapacity(ref conduitVectors, maxNodeCount);

            EnsureInactiveNativeCapacity(ref conduitStrengths, maxNodeCount);

            EnsureInactiveNativeCapacity(ref nodeTypes, maxNodeCount);

            float stepX = chunkWidth / sampleCountX;
            float stepZ = chunkDepth / sampleCountZ;
            int writeIndex = 0;
            for (int sampleZ = 0; sampleZ < sampleCountZ; sampleZ++)
            {
                float worldZ = payload.MinZ + ((sampleZ + 0.5f) * stepZ);
                for (int sampleX = 0; sampleX < sampleCountX; sampleX++)
                {
                    float worldX = payload.MinX + ((sampleX + 0.5f) * stepX);
                    if (!TrySampleCachedTerrainHeight(state, heightSamples, worldX, worldZ, out float terrainY))
                        continue;

                    Vector3 candidate = new Vector3(worldX, terrainY + abyssalNavNodeHoverHeight, worldZ);
                    if (!TryResolveAbyssalNavNodeCandidate(candidate, payload, out Vector3 conduitVector, out float conduitStrength, out NavNodeType nodeType))
                        continue;

                    nodes[writeIndex] = candidate;
                    conduitVectors[writeIndex] = conduitVector;
                    conduitStrengths[writeIndex] = conduitStrength;
                    nodeTypes[writeIndex] = (byte)nodeType;
                    writeIndex++;
                }
            }

            if (holeNodeCount > 0)
            {
                for (int i = 0; i < _terrainHoleCount; i++)
                {
                    TerrainHoleRecord hole = _terrainHoleRecords[i];
                    if (!DoesChunkBoundsIntersectCircle(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ, hole.X, hole.Z, hole.RadiusSq) ||
                        !TrySampleCachedTerrainHeight(state, heightSamples, hole.X, hole.Z, out float terrainY))
                    {
                        continue;
                    }

                    Vector3 holeNode = new Vector3(hole.X, terrainY + abyssalNavNodeHoverHeight, hole.Z);
                    nodes[writeIndex] = holeNode;
                    conduitVectors[writeIndex] = Vector3.zero;
                    conduitStrengths[writeIndex] = 0f;
                    nodeTypes[writeIndex] = (byte)NavNodeType.Interior;
                    writeIndex++;
                }
            }

            if (writeIndex <= 0)
            {
                if (!reusedExistingPayload)
                {
                    DisposeNativeArray(ref nodes);
                    DisposeNativeArray(ref conduitVectors);
                    DisposeNativeArray(ref conduitStrengths);
                    DisposeNativeArray(ref nodeTypes);
                }

                return navPayload;
            }

            navPayload.Nodes = nodes;
            navPayload.ConduitVectors = conduitVectors;
            navPayload.ConduitStrengths = conduitStrengths;
            navPayload.NodeTypes = nodeTypes;
            navPayload.Count = writeIndex;
            return navPayload;
        }

        private bool TryResolveAbyssalNavNodeCandidate(
            Vector3 candidate,
            ChunkPayload payload,
            out Vector3 conduitVector,
            out float conduitStrength,
            out NavNodeType nodeType)
        {
            conduitVector = Vector3.zero;
            conduitStrength = 0f;
            nodeType = NavNodeType.Water;
            if (IsInsideRegisteredTerrainHole(candidate.x, candidate.z))
            {
                nodeType = NavNodeType.Interior;
                return true;
            }

            if (TryResolveArtificialStructureAtPosition(candidate, out _))
            {
                nodeType = NavNodeType.Interior;
                return true;
            }

            float obstacleRadiusSq = abyssalNavNodeObstacleRadius * abyssalNavNodeObstacleRadius;
            float maxVerticalDelta = abyssalNavNodeObstacleVerticalWindow;
            float obstacleWeight = 0f;
            float deepAffinity = 0f;
            float flowMagnitudeSum = 0f;
            Vector3 flowVectorSum = Vector3.zero;
            int contributingSamples = 0;
            NativeChunkPool underwaterPool = ResolveChunkPool(isSurface: false, payload);
            int end = Mathf.Min(underwaterPool.Matrices.Length, payload.UnderwaterOffset + payload.UnderwaterCount);
            for (int poolIndex = Mathf.Max(0, payload.UnderwaterOffset); poolIndex < end; poolIndex++)
            {
                Vector3 position = ResolveRuntimePosition(underwaterPool.Matrices[poolIndex]);
                float dx = position.x - candidate.x;
                float dz = position.z - candidate.z;
                float horizontalDistanceSq = (dx * dx) + (dz * dz);
                if (horizontalDistanceSq > obstacleRadiusSq)
                    continue;

                float verticalDelta = Mathf.Abs(position.y - candidate.y);
                if (verticalDelta > maxVerticalDelta)
                    continue;

                byte biomeLayer = underwaterPool.BiomeLayers[poolIndex];
                int semanticType = underwaterPool.SemanticTypes[poolIndex];
                float semanticWeight = ResolveAbyssalNavObstacleWeight(semanticType, biomeLayer);
                if (semanticWeight <= 0f)
                    continue;

                obstacleWeight += semanticWeight;
                if (biomeLayer >= (byte)VegetationBiomeLayer.ColonyGraveyard)
                    deepAffinity += semanticWeight;

                Vector3 flowVector = underwaterPool.FlowVectors[poolIndex];
                flowMagnitudeSum += flowVector.magnitude;
                flowVectorSum += flowVector;
                contributingSamples++;
                if (obstacleWeight > abyssalNavNodeMaxObstacleDensity)
                    return false;
            }

            if (deepAffinity < abyssalNavNodeMinimumDeepAffinity)
                return false;

            float averageCurrentMagnitude = contributingSamples > 0
                ? flowMagnitudeSum / contributingSamples
                : 0f;
            if (averageCurrentMagnitude > abyssalNavNodeMaxCurrentMagnitude)
                return false;

            float depthMeters = Mathf.Max(0f, waterLevel - candidate.y);
            if (depthMeters < abyssalConduitStartDepth ||
                averageCurrentMagnitude < abyssalConduitMinimumFlowMagnitude ||
                contributingSamples <= 0)
            {
                return true;
            }

            if (flowVectorSum.sqrMagnitude <= 0.0001f)
                return true;

            conduitVector = flowVectorSum.normalized;
            if (abyssalNavNodeMaxCurrentMagnitude <= abyssalConduitMinimumFlowMagnitude)
            {
                conduitStrength = 1f;
                return true;
            }

            conduitStrength = Mathf.Clamp01(
                (averageCurrentMagnitude - abyssalConduitMinimumFlowMagnitude) /
                Mathf.Max(0.01f, abyssalNavNodeMaxCurrentMagnitude - abyssalConduitMinimumFlowMagnitude));
            return true;
        }

        private bool TryResolveArtificialStructureAtPosition(Vector3 position, out StructureType type)
        {
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (!structure.Bounds.Contains(position))
                    continue;

                type = structure.Type;
                return true;
            }

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                Bounds bounds = GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]);
                if (!bounds.Contains(position))
                    continue;

                type = StructureType.MegaWreck;
                return true;
            }

            type = default;
            return false;
        }

        private static bool TrySampleCachedTerrainHeight(TileRuntimeState state, NativeArray<ushort> heightSamples, float worldX, float worldZ, out float terrainHeight)
        {
            terrainHeight = 0f;
            if (state == null || !heightSamples.IsCreated || state.HeightmapResolution <= 1)
                return false;

            float localX = worldX - state.TerrainPosition.x;
            float localZ = worldZ - state.TerrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > state.TerrainSize.x || localZ > state.TerrainSize.z)
                return false;

            float normalizedX = Mathf.Clamp01(localX / Mathf.Max(0.01f, state.TerrainSize.x));
            float normalizedZ = Mathf.Clamp01(localZ / Mathf.Max(0.01f, state.TerrainSize.z));
            terrainHeight = state.TerrainPosition.y + SampleHeight(
                normalizedX,
                normalizedZ,
                new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z),
                state.HeightmapResolution,
                heightSamples);
            return true;
        }

        private static bool SliceContainsDeepBiome(NativeChunkPool pool, int offset, int count)
        {
            if (!pool.BiomeLayers.IsCreated || count <= 0)
                return false;

            int end = Mathf.Min(pool.BiomeLayers.Length, offset + count);
            for (int poolIndex = Mathf.Max(0, offset); poolIndex < end; poolIndex++)
            {
                if (pool.BiomeLayers[poolIndex] >= (byte)VegetationBiomeLayer.ColonyGraveyard)
                    return true;
            }

            return false;
        }

        private static float ResolveAbyssalNavObstacleWeight(int semanticType, byte biomeLayer)
        {
            switch ((VegetationSemanticType)semanticType)
            {
                case VegetationSemanticType.ColonyCable:
                    return 0.45f;
                case VegetationSemanticType.ColonyHullPlating:
                case VegetationSemanticType.ColonySupportBeam:
                    return 0.75f;
                case VegetationSemanticType.DeadZoneMassiveStructure:
                    return 1f;
                default:
                    return biomeLayer >= (byte)VegetationBiomeLayer.ColonyGraveyard ? 0.2f : 0f;
            }
        }

        private void CacheChunkAbyssalNavPayload(ChunkKey key, ChunkAbyssalNavPayload payload)
        {
            if (payload.Count <= 0 || !payload.Nodes.IsCreated)
            {
                RemoveChunkAbyssalNavPayload(key);
                return;
            }

            _chunkAbyssalNavPayloads[key] = payload;
        }

        private void RemoveChunkAbyssalNavPayload(ChunkKey key)
        {
            if (_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload))
                DisposeChunkAbyssalNavPayload(ref payload);

            _chunkAbyssalNavPayloads.Remove(key);
        }

        private void DisposeAllChunkAbyssalNavPayloads()
        {
            if (_chunkAbyssalNavPayloads.Count <= 0)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkAbyssalNavPayload>.Enumerator enumerator = _chunkAbyssalNavPayloads.GetEnumerator();
            while (enumerator.MoveNext())
                _evictionKeys.Add(enumerator.Current.Key);

            for (int i = 0; i < _evictionKeys.Count; i++)
                RemoveChunkAbyssalNavPayload(_evictionKeys[i]);
        }

        private static void DisposeChunkAbyssalNavPayload(ref ChunkAbyssalNavPayload payload)
        {
            DisposeNativeArray(ref payload.Nodes);
            DisposeNativeArray(ref payload.ConduitVectors);
            DisposeNativeArray(ref payload.ConduitStrengths);
            DisposeNativeArray(ref payload.NodeTypes);
            payload.Count = 0;
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

                float minX = Mathf.Max(payload.MinX, wreckBounds.min.x);
                float maxX = Mathf.Min(payload.MaxX, wreckBounds.max.x);
                float minZ = Mathf.Max(payload.MinZ, wreckBounds.min.z);
                float maxZ = Mathf.Min(payload.MaxZ, wreckBounds.max.z);
                if (maxX <= minX || maxZ <= minZ)
                    continue;

                Vector3 worldCenter = new Vector3((minX + maxX) * 0.5f, wreckBounds.center.y, (minZ + maxZ) * 0.5f);
                Vector3 worldSize = new Vector3(maxX - minX, wreckBounds.size.y, maxZ - minZ);
                Vector3 localCenter = worldCenter - wreckBounds.center;
                Vector3 localSize = worldSize;
                int sectionX = Mathf.FloorToInt((worldCenter.x - wreckBounds.min.x) / DefaultVirtualChunkSize);
                int sectionZ = Mathf.FloorToInt((worldCenter.z - wreckBounds.min.z) / DefaultVirtualChunkSize);
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

        /// <summary>
        /// Registers a persistent world-space terrain hole that suppresses vegetation generation inside the provided radius.
        /// </summary>
        public void RegisterTerrainHole(Vector3 position, float radius)
        {
            RegisterTerrainHoleHandle(position, radius);
        }

        /// <summary>
        /// Registers a persistent world-space terrain hole and returns a stable runtime handle for later removal.
        /// </summary>
        public int RegisterTerrainHoleHandle(Vector3 position, float radius)
        {
            if (radius <= 0f)
                return InvalidTerrainHoleId;

            float clampedRadius = Mathf.Max(0.5f, radius);
            float duplicateDistanceSq = Mathf.Max(0.25f, clampedRadius * 0.15f);
            duplicateDistanceSq *= duplicateDistanceSq;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord existing = _terrainHoleRecords[i];
                float dx = existing.X - position.x;
                float dz = existing.Z - position.z;
                if ((dx * dx) + (dz * dz) > duplicateDistanceSq)
                    continue;

                existing.X = position.x;
                existing.Y = position.y;
                existing.Z = position.z;
                existing.Radius = clampedRadius;
                existing.RadiusSq = clampedRadius * clampedRadius;
                existing.SourceType = TerrainHoleSourceType.CaveEntrance;
                _terrainHoleRecords[i] = existing;
                SyncTerrainHoleNativeCache();
                InvalidateChunksIntersectingHole(position, clampedRadius);
                RefreshResidency();
                return existing.HoleId;
            }

            EnsureTerrainHoleCapacity(_terrainHoleCount + 1);
            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount + 1,
                    transientCount);
            }

            _terrainHoleRecords[_persistentTerrainHoleCount] = new TerrainHoleRecord
            {
                HoleId = _nextTerrainHoleId++,
                Y = position.y,
                X = position.x,
                Z = position.z,
                Radius = clampedRadius,
                RadiusSq = clampedRadius * clampedRadius,
                SourceType = TerrainHoleSourceType.CaveEntrance
            };
            _persistentTerrainHoleCount++;
            _terrainHoleCount++;
            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingHole(position, clampedRadius);
            RefreshResidency();
            return _terrainHoleRecords[_persistentTerrainHoleCount - 1].HoleId;
        }

        /// <summary>
        /// Unregisters a persistent terrain hole by approximate world-space location and initiates vegetation rebuild so the area can regrow.
        /// </summary>
        public bool UnregisterTerrainHole(Vector3 position, float radius)
        {
            if (_persistentTerrainHoleCount <= 0)
                return false;

            float clampedRadius = Mathf.Max(0.5f, radius);
            float duplicateDistanceSq = Mathf.Max(0.25f, clampedRadius * 0.15f);
            duplicateDistanceSq *= duplicateDistanceSq;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord existing = _terrainHoleRecords[i];
                float dx = existing.X - position.x;
                float dz = existing.Z - position.z;
                if ((dx * dx) + (dz * dz) > duplicateDistanceSq)
                    continue;

                if (Mathf.Abs(existing.Radius - clampedRadius) > Mathf.Max(0.5f, clampedRadius * 0.35f))
                    continue;

                return UnregisterTerrainHole(existing.HoleId);
            }

            return false;
        }

        /// <summary>
        /// Unregisters a persistent terrain hole by stable runtime handle and initiates vegetation rebuild so the area can regrow.
        /// </summary>
        public bool UnregisterTerrainHole(int holeId)
        {
            if (holeId == InvalidTerrainHoleId || _persistentTerrainHoleCount <= 0)
                return false;

            int persistentIndex = -1;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                if (_terrainHoleRecords[i].HoleId != holeId)
                    continue;

                persistentIndex = i;
                break;
            }

            if (persistentIndex < 0)
                return false;

            TerrainHoleRecord removed = _terrainHoleRecords[persistentIndex];
            int persistentTailCount = _persistentTerrainHoleCount - persistentIndex - 1;
            if (persistentTailCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    persistentIndex + 1,
                    _terrainHoleRecords,
                    persistentIndex,
                    persistentTailCount);
            }

            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount - 1,
                    transientCount);
            }

            _persistentTerrainHoleCount--;
            _terrainHoleCount--;
            if (_terrainHoleCount >= 0 && _terrainHoleCount < _terrainHoleRecords.Length)
                _terrainHoleRecords[_terrainHoleCount] = default;

            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingHole(new Vector3(removed.X, removed.Y, removed.Z), removed.Radius);
            RefreshResidency();
            return true;
        }

        /// <summary>
        /// Clears all registered terrain holes in one cold-path operation.
        /// </summary>
        public void ClearTerrainHoles()
        {
            if (_terrainHoleCount <= 0)
                return;

            _terrainHoleCount = 0;
            _persistentTerrainHoleCount = 0;
            _megaWreckInteriorMaskHash = 0;
            _nextTerrainHoleId = 1;
            ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            ClearAllResidency();
            RefreshResidency();
        }

        private void SyncMegaWreckInteriorTerrainHoles()
        {
            int currentTransientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (playerTransform == null ||
                _megaWreckStreamCount <= 0 ||
                _megaWreckStreamSnapshot == null)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            int wreckId = FindMegaWreckInteriorWreckId(playerPosition);
            if (wreckId == int.MinValue)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            int matchingSectionCount = CountMegaWreckSections(wreckId);
            if (matchingSectionCount <= 0)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            EnsureTerrainHoleCapacity(_persistentTerrainHoleCount + matchingSectionCount);
            int newHash = ComputeMegaWreckInteriorMaskHash(wreckId);
            if (currentTransientCount == matchingSectionCount && _megaWreckInteriorMaskHash == newHash)
                return;

            if (currentTransientCount > 0)
                InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, currentTransientCount);

            int writeIndex = _persistentTerrainHoleCount;
            bool hasInteriorBounds = false;
            Bounds interiorBounds = default;
            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                if (section.WreckId != wreckId)
                    continue;

                Bounds sectionBounds = GetMegaWreckSectionBounds(section);
                if (!hasInteriorBounds)
                {
                    interiorBounds = sectionBounds;
                    hasInteriorBounds = true;
                }
                else
                {
                    interiorBounds.Encapsulate(sectionBounds);
                }

                float horizontalHalfExtent = Mathf.Sqrt((section.WorldSize.x * section.WorldSize.x) + (section.WorldSize.z * section.WorldSize.z)) * 0.5f;
                float radius = Mathf.Max(megaWreckInteriorMinimumHoleRadius, horizontalHalfExtent + megaWreckInteriorHolePadding);
                _terrainHoleRecords[writeIndex] = new TerrainHoleRecord
                {
                    HoleId = InvalidTerrainHoleId,
                    Y = section.WorldCenter.y,
                    X = section.WorldCenter.x,
                    Z = section.WorldCenter.z,
                    Radius = radius,
                    RadiusSq = radius * radius,
                    SourceType = TerrainHoleSourceType.MegaWreckInterior
                };
                writeIndex++;
            }

            _terrainHoleCount = writeIndex;
            _megaWreckInteriorMaskHash = newHash;
            if (hasInteriorBounds)
                SetArtificialInteriorState(StructureType.MegaWreck, wreckId, interiorBounds);
            else
                ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, _terrainHoleCount - _persistentTerrainHoleCount);
            RefreshResidency();
        }

        private void ClearTransientMegaWreckInteriorHoles(int currentTransientCount)
        {
            if (currentTransientCount <= 0 && _megaWreckInteriorMaskHash == 0)
                return;

            if (currentTransientCount > 0)
                InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, currentTransientCount);

            _terrainHoleCount = _persistentTerrainHoleCount;
            _megaWreckInteriorMaskHash = 0;
            ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            RefreshResidency();
        }

        private void EvictDistantTerrainHoles()
        {
            if (playerTransform == null || _persistentTerrainHoleCount <= 0)
                return;

            float maxDistanceSq = DefaultTerrainHoleEvictionDistance * DefaultTerrainHoleEvictionDistance;
            Vector3 playerPosition = playerTransform.position;
            _terrainHoleEvictionScratch.Clear();

            int writeIndex = 0;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord hole = _terrainHoleRecords[i];
                float dx = hole.X - playerPosition.x;
                float dz = hole.Z - playerPosition.z;
                if ((dx * dx) + (dz * dz) > maxDistanceSq)
                {
                    _terrainHoleEvictionScratch.Add(hole);
                    continue;
                }

                if (writeIndex != i)
                    _terrainHoleRecords[writeIndex] = hole;

                writeIndex++;
            }

            if (_terrainHoleEvictionScratch.Count <= 0)
                return;

            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    writeIndex,
                    transientCount);
            }

            int previousTerrainHoleCount = _terrainHoleCount;
            _persistentTerrainHoleCount = writeIndex;
            _terrainHoleCount = writeIndex + transientCount;
            for (int i = _terrainHoleCount; i < previousTerrainHoleCount; i++)
                _terrainHoleRecords[i] = default;

            SyncTerrainHoleNativeCache();
            for (int i = 0; i < _terrainHoleEvictionScratch.Count; i++)
            {
                TerrainHoleRecord removed = _terrainHoleEvictionScratch[i];
                InvalidateChunksIntersectingHole(new Vector3(removed.X, removed.Y, removed.Z), removed.Radius);
            }

            RefreshResidency();
        }

        private void SetArtificialInteriorState(StructureType type, int structureId, Bounds bounds)
        {
            _activeArtificialInteriorState = new ArtificialInteriorState
            {
                IsActive = true,
                Type = type,
                StructureId = structureId,
                Bounds = bounds
            };
            GlobalArtificialInteriorActive = true;
            GlobalArtificialInteriorType = type;
            GlobalArtificialInteriorId = structureId;
            GlobalArtificialInteriorBounds = bounds;
        }

        private void ClearArtificialInteriorState()
        {
            _activeArtificialInteriorState = default;
            GlobalArtificialInteriorActive = false;
            GlobalArtificialInteriorType = default;
            GlobalArtificialInteriorId = int.MinValue;
            GlobalArtificialInteriorBounds = default;
        }

        /// <summary>
        /// Applies a world-space origin offset to cached bounds and metadata only.
        /// Vegetation instance matrices stay in local chunk space and are shifted on GPU.
        /// </summary>
        public void ApplyWorldOffsetToAllChunks(Vector3 offset)
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
            _totalUniverseOffset += appliedOffset;
            GlobalTotalUniverseOffset = _totalUniverseOffset;

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
            bool readbackChanged = FinalizePendingTileHeightReadbacks();
            if (_tileStates.Count == 0 || playerTransform == null)
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

            if (Time.unscaledTime < _nextCacheValidationTime)
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

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
            {
                EnforceTileCacheLruBudget();
                return changed || readbackChanged;
            }

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

            EnforceTileCacheLruBudget();
            return changed || readbackChanged;
        }

        private bool FinalizePendingTileHeightReadbacks()
        {
            if (_tileStates.Count <= 0)
                return false;

            bool changed = false;
            _tileStateRemovalScratchKeys.Clear();
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                if (state.PendingRemoval)
                {
                    if (!state.HeightReadbackPending || TryFinalizeTileHeightReadback(state))
                        _tileStateRemovalScratchKeys.Add(enumerator.Current.Key);

                    continue;
                }

                if (!state.HeightReadbackPending)
                    continue;

                if (TryFinalizeTileHeightReadback(state))
                {
                    InvalidateTileChunks(state.TileX, state.TileZ, state.ChunkCountX, state.ChunkCountZ);
                    changed = true;
                }
            }

            enumerator.Dispose();

            for (int i = 0; i < _tileStateRemovalScratchKeys.Count; i++)
                FinalizeDeferredTileRemoval(_tileStateRemovalScratchKeys[i]);

            return changed;
        }

        private static bool TryFinalizeTileHeightReadback(TileRuntimeState state)
        {
            if (state == null || !state.HeightReadbackPending)
                return false;

            if (!state.HeightReadbackRequest.done)
                return false;

            bool completedSuccessfully = !state.HeightReadbackRequest.hasError;
            state.HeightReadbackPending = false;
            if (!completedSuccessfully)
            {
                state.HeightmapHash = 0;
                state.HeightmapUpdateCount = 0u;
                return false;
            }

            state.ActiveCacheBufferIndex = state.PendingCacheBufferIndex;
            TouchTileCacheState(state);
            unchecked
            {
                state.CacheRevision++;
            }

            return true;
        }

        private bool CanRefreshThreatSpatialSnapshots()
        {
            return !_threatPropagationScheduled &&
                   !_flowFieldScheduled &&
                   !_abyssalThermalGridScheduled &&
                   !_abyssalPathScheduled;
        }

        private void FinalizeDeferredTileRemoval(long tileKey)
        {
            if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null)
                return;

            if (state.HeightReadbackPending)
                return;

            DisposeTileNativeCaches(state);
            _tileStates.Remove(tileKey);
            _activeSetDirty = true;
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
                if (candidate == null || candidate.PendingRemoval)
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
            out NativeArray<ushort> heightSamples)
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
            TouchTileCacheState(state);
            return true;
        }

        private static void TouchTileCacheState(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.LastAccessFrame = unchecked((uint)Mathf.Max(0, Time.frameCount));
        }

        private static bool HasActiveTileCache(TileRuntimeState state)
        {
            if (state == null)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            return buffer.SandMaskNative.IsCreated &&
                   buffer.RockMaskNative.IsCreated &&
                   buffer.HeightSamplesNative.IsCreated;
        }

        private static void EvictTileCache(TileRuntimeState state)
        {
            if (state == null)
                return;

            DisposeTileNativeCaches(state);
            state.AlphamapTextureCache = null;
            state.HeightTextureCache = null;
            state.AlphamapTextureCount = 0;
            state.CombinedAlphamapHash = 0;
            state.CombinedAlphamapUpdateCount = 0u;
            state.HeightmapHash = 0;
            state.HeightmapUpdateCount = 0u;
            state.CacheRevision = 0;
            state.LastAccessFrame = 0u;
        }

        private long ResolveProtectedTileKey()
        {
            if (playerTransform == null || !TryFindPlayerTileState(playerTransform.position, out TileRuntimeState playerTileState) || playerTileState == null)
                return long.MinValue;

            return PackTileCoord(playerTileState.TileX, playerTileState.TileZ);
        }

        private void EnforceTileCacheLruBudget()
        {
            if (_tileStates.Count <= TileCacheLruCapacity)
                return;

            long protectedTileKey = ResolveProtectedTileKey();
            int lruIterations = 0;
            while (lruIterations < MaxTileCacheLruIterations)
            {
                lruIterations++;
                int residentCacheCount = 0;
                long evictionKey = long.MinValue;
                uint oldestAccessFrame = uint.MaxValue;

                Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    long tileKey = enumerator.Current.Key;
                    TileRuntimeState state = enumerator.Current.Value;
                    if (!HasActiveTileCache(state))
                        continue;

                    residentCacheCount++;
                    if (tileKey == protectedTileKey || state == null || state.HeightReadbackPending)
                        continue;

                    if (state.LastAccessFrame <= oldestAccessFrame)
                    {
                        oldestAccessFrame = state.LastAccessFrame;
                        evictionKey = tileKey;
                    }
                }

                if (residentCacheCount <= TileCacheLruCapacity || evictionKey == long.MinValue)
                    return;

                if (_tileStates.TryGetValue(evictionKey, out TileRuntimeState evictionState))
                    EvictTileCache(evictionState);
            }

            LogLoopGuardHit(nameof(EnforceTileCacheLruBudget), MaxTileCacheLruIterations);
        }

        private static bool HasTileCacheSignatureChanged(TileRuntimeState state, TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return false;

            RefreshTerrainTextureCaches(state, terrainData);
            CaptureTileCacheSignature(
                state.AlphamapTextureCache,
                state.HeightTextureCache,
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

            if (!HasActiveTileCache(state))
                return CacheTileMasks(state, state.TerrainData);

            if (!HasTileCacheSignatureChanged(state, state.TerrainData))
                return false;

            return CacheTileMasks(state, state.TerrainData);
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
                if (RebuildAndBindActiveBuffers())
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
                        bool shouldBeCorrupted = IsChunkCorrupted(key);
                        bool hasPayload = _chunkPayloads.TryGetValue(key, out ChunkPayload payload);
                        bool hasInFlightJob = _chunkBuildJobs.TryGetValue(key, out _);
                        bool corruptionMismatch = hasPayload && payload.IsCorrupted != shouldBeCorrupted;
                        if ((!hasPayload || corruptionMismatch) && !hasInFlightJob)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                        else if (hasPayload && !payload.IsCorrupted && payload.GrassLodTier != desiredGrassLodTier && !hasInFlightJob)
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

        private bool RebuildAndBindActiveBuffers()
        {
            CompleteAbyssalPathJob(forceComplete: false);
            RebuildDensityQuerySnapshot();
            RebuildAbyssalAnchorSnapshot();
            RebuildAbyssalNavNodeSnapshot();
            RebuildMegaWreckStreamSnapshot();
            RebuildCanopyHeightGrid();
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

            if ((totalSurfaceCount > 0 && !TryPrepareRendererWriteBuffer(ref _surfaceBackReaderHandle)) ||
                (totalUnderwaterCount > 0 && !TryPrepareRendererWriteBuffer(ref _underwaterBackReaderHandle)))
            {
                return false;
            }

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
                EnsureActiveAggregateBufferCapacity(ref _surfaceAggregateBackBuffers, totalSurfaceCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasSurface)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.SurfaceCount;
                    CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: true, payload),
                        payload.SurfaceOffset,
                        _surfaceAggregateBackBuffers,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                _surfaceBackCount = totalSurfaceCount;
                _surfaceBackDrawBounds = surfaceBounds;
                _hasSurfaceBackBounds = hasSurfaceBounds;
                SwapActiveAggregateBuffers(ref _surfaceAggregateFrontBuffers, ref _surfaceAggregateBackBuffers);
                SwapAggregateReadState(
                    ref _surfaceFrontCount,
                    ref _surfaceBackCount,
                    ref _surfaceFrontDrawBounds,
                    ref _surfaceBackDrawBounds,
                    ref _hasSurfaceFrontBounds,
                    ref _hasSurfaceBackBounds,
                    ref _surfaceFrontReaderHandle,
                    ref _surfaceBackReaderHandle,
                    ref _surfaceFrontBufferIndex,
                    ref _surfaceBackBufferIndex);
                _surfaceActiveAggregateRevision++;
            }
            else
            {
                _surfaceFrontCount = 0;
                _hasSurfaceFrontBounds = false;
                _surfaceActiveAggregateRevision++;
            }

            if (totalUnderwaterCount > 0)
            {
                EnsureActiveAggregateBufferCapacity(ref _underwaterAggregateBackBuffers, totalUnderwaterCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.UnderwaterCount;
                    CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: false, payload),
                        payload.UnderwaterOffset,
                        _underwaterAggregateBackBuffers,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                DistortAggregateFlowVectorsByThreat(_underwaterAggregateBackBuffers, totalUnderwaterCount);
                _underwaterBackCount = totalUnderwaterCount;
                _underwaterBackDrawBounds = underwaterBounds;
                _hasUnderwaterBackBounds = hasUnderwaterBounds;
                SwapActiveAggregateBuffers(ref _underwaterAggregateFrontBuffers, ref _underwaterAggregateBackBuffers);
                SwapAggregateReadState(
                    ref _underwaterFrontCount,
                    ref _underwaterBackCount,
                    ref _underwaterFrontDrawBounds,
                    ref _underwaterBackDrawBounds,
                    ref _hasUnderwaterFrontBounds,
                    ref _hasUnderwaterBackBounds,
                    ref _underwaterFrontReaderHandle,
                    ref _underwaterBackReaderHandle,
                    ref _underwaterFrontBufferIndex,
                    ref _underwaterBackBufferIndex);
                _underwaterActiveAggregateRevision++;
            }
            else
            {
                _underwaterFrontCount = 0;
                _hasUnderwaterFrontBounds = false;
                _underwaterActiveAggregateRevision++;
            }

            return true;
        }

        private bool ScheduleChunkBuild(TileRuntimeState state, ChunkKey key, long tileKey, byte grassLodTier)
        {
            if (state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out NativeArray<ushort> heightSamples) ||
                state.AlphamapResolution <= 0 ||
                state.HeightmapResolution <= 1)
            {
                return false;
            }

            if (!_terrainHoleRecordsNative.IsCreated)
                SyncTerrainHoleNativeCache();

            ChunkPayload payloadHeader = CreateChunkPayloadHeader(state, key.ChunkX, key.ChunkZ);
            payloadHeader.GrassLodTier = grassLodTier;
            bool isCorrupted = IsChunkCorrupted(key);
            payloadHeader.CorruptionState = isCorrupted ? (byte)1 : (byte)0;

            GetChunkBounds(state, key.ChunkX, key.ChunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float chunkWidth = math.max(0.01f, maxX - minX);
            float chunkDepth = math.max(0.01f, maxZ - minZ);
            float grassStep = GetGrassStepForTier(grassLodTier);
            int grassCountX = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkWidth / grassStep));
            int grassCountZ = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkDepth / grassStep));
            int kelpCountX = Mathf.Max(1, Mathf.CeilToInt(chunkWidth / kelpStepMeters));
            int kelpCountZ = Mathf.Max(1, Mathf.CeilToInt(chunkDepth / kelpStepMeters));
            int floatingCountX = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkWidth / floatingStepMeters));
            int floatingCountZ = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkDepth / floatingStepMeters));

            ChunkBuildJobState jobState = new ChunkBuildJobState
            {
                Key = key,
                TileKey = tileKey,
                TileCacheRevision = state.CacheRevision,
                GrassLodTier = grassLodTier,
                CorruptionState = payloadHeader.CorruptionState,
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
                    TerrainHoles = _terrainHoleRecordsNative,
                    ThreatEchoFlags = _ecosystemThreatEchoCurrentNative,
                    TerrainHoleCount = _terrainHoleCount,
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
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    EchoTechnoJungleThresholdBias = 0f,
                    EchoDeadZoneKeepBoost = 0f,
                    IgnorePlacementMasks = 0,
                    CorruptionMode = 0,
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
                    TerrainHoles = _terrainHoleRecordsNative,
                    ThreatEchoFlags = _ecosystemThreatEchoCurrentNative,
                    TerrainHoleCount = _terrainHoleCount,
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
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    EchoTechnoJungleThresholdBias = permanentEchoTechnoJungleThresholdBias,
                    EchoDeadZoneKeepBoost = permanentEchoDeadZoneKeepBoost,
                    IgnorePlacementMasks = isCorrupted ? 1 : 0,
                    CorruptionMode = isCorrupted ? 1 : 0,
                    EnableVerticalBiomeRewrite = isCorrupted ? 0 : 1,
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
                    TerrainHoles = _terrainHoleRecordsNative,
                    TerrainHoleCount = _terrainHoleCount,
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
                if (!jobState.CancelRequested && IsJobStateCurrent(jobState))
                {
                    ReleaseChunkPayloadStorage(key);
                    ChunkPayload payload = BuildChunkPayloadFromJob(jobState);
                    _chunkPayloads[key] = payload;
                    CacheChunkAbyssalNavPayload(key, BuildChunkAbyssalNavPayload(key, jobState, payload));
                    CacheChunkMegaWreckPayload(key, BuildChunkMegaWreckPayload(payload));
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
            payload.CorruptionState = jobState.CorruptionState;

            int grassCount = CountValidRecords(jobState.GrassRecords);
            int floatingCount = CountValidRecords(jobState.FloatingRecords);
            int kelpCount = CountValidRecords(jobState.KelpRecords);
            int surfaceCount = grassCount + floatingCount;

            if (surfaceCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: true, surfaceCount, out int surfaceOffset, out bool useScratchPool))
                {
                    payload.SurfaceOffset = surfaceOffset;
                    payload.SurfaceCount = surfaceCount;
                    payload.SurfaceEdgeOffset = surfaceOffset;
                    payload.SurfacePoolSet = useScratchPool ? (byte)1 : (byte)0;
                    int writeIndex = surfaceOffset;
                    if (useScratchPool)
                    {
                        WriteJobRecordsToPool(jobState.GrassRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffset);
                        WriteJobRecordsToPool(jobState.FloatingRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffset);
                    }
                    else
                    {
                        WriteJobRecordsToPool(jobState.GrassRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffset);
                        WriteJobRecordsToPool(jobState.FloatingRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffset);
                    }
                }
            }

            if (kelpCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: false, kelpCount, out int underwaterOffset, out bool useScratchPool))
                {
                    payload.UnderwaterOffset = underwaterOffset;
                    payload.UnderwaterCount = kelpCount;
                    payload.UnderwaterEdgeOffset = underwaterOffset;
                    payload.UnderwaterPoolSet = useScratchPool ? (byte)1 : (byte)0;
                    int writeIndex = underwaterOffset;
                    if (useScratchPool)
                        WriteJobRecordsToPool(jobState.KelpRecords, ref _underwaterDefragScratchPool, ref writeIndex, _totalUniverseOffset);
                    else
                        WriteJobRecordsToPool(jobState.KelpRecords, ref _underwaterChunkPool, ref writeIndex, _totalUniverseOffset);
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
            ref int writeIndex,
            Vector3 universeOffset)
        {
            if (!source.IsCreated)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                JobInstanceRecord record = source[i];
                if (record.IsValid == 0)
                    continue;

                pool.Matrices[writeIndex] = ConvertMatrixToStableUniverseSpace(ToMatrix4x4(record.Matrix), universeOffset);
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
            ActiveAggregateNativeBufferSet destinationBuffers,
            int destinationOffset,
            int copyCount)
        {
            NativeArray<Matrix4x4>.Copy(pool.Matrices, sourceOffset, destinationBuffers.Matrices, destinationOffset, copyCount);
            NativeArray<HectonVegetationInstanceData>.Copy(pool.Metadata, sourceOffset, destinationBuffers.Metadata, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.Types, sourceOffset, destinationBuffers.Types, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.SemanticTypes, sourceOffset, destinationBuffers.SemanticTypes, destinationOffset, copyCount);
            NativeArray<byte>.Copy(pool.BiomeLayers, sourceOffset, destinationBuffers.BiomeLayers, destinationOffset, copyCount);
            NativeArray<Vector2>.Copy(pool.FlowDirections, sourceOffset, destinationBuffers.FlowDirections, destinationOffset, copyCount);
            NativeArray<Vector3>.Copy(pool.FlowVectors, sourceOffset, destinationBuffers.FlowVectors, destinationOffset, copyCount);
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
                ClearThreatAttractorGridCells(_threatAttractorGridScratchNative, gridOffset, DensityGridCellCount);
                AccumulateChunkDensityGrid(payload, ref _densityQueryGridScratchNative, gridOffset);
                AccumulateChunkThreatAttractorGrid(payload, ref _threatAttractorGridScratchNative, gridOffset);

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

        private void RebuildAbyssalAnchorSnapshot()
        {
            int anchorCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                    continue;

                anchorCount += CountSemanticType(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount, (int)VegetationSemanticType.DeadZoneMassiveStructure);
            }

            _abyssalAnchorCount = anchorCount;
            if (anchorCount <= 0)
                return;

            EnsureVector3Capacity(ref _abyssalAnchorPositions, anchorCount);
            EnsureVector3NativeCapacity(ref _abyssalAnchorPositionsNative, anchorCount);
            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                    continue;

                CopySemanticAnchorPositions(
                    ResolveChunkPool(isSurface: false, payload),
                    payload.UnderwaterOffset,
                    payload.UnderwaterCount,
                    (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    _abyssalAnchorPositions,
                    _abyssalAnchorPositionsNative,
                    _totalUniverseOffset,
                    ref writeIndex);
            }
        }

        private void RebuildAbyssalNavNodeSnapshot()
        {
            InvalidateAbyssalPathState();
            int nodeCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                nodeCount += payload.Count;
            }

            _abyssalNavNodeCount = nodeCount;
            if (nodeCount <= 0)
            {
                if (_abyssalNavNodes.IsCreated)
                    _abyssalNavNodes.Clear();

                _abyssalNavGraphOrigin = Vector3.zero;
                if (_abyssalNavGraphHashNative.IsCreated)
                    _abyssalNavGraphHashNative.Clear();
                return;
            }

            if (_abyssalNavNodes.IsCreated)
                _abyssalNavNodes.Clear();
            if (_abyssalNavGraphHashNative.IsCreated)
                _abyssalNavGraphHashNative.Clear();
            EnsureAbyssalNavNodeListCapacity(nodeCount);
            EnsureVector3Capacity(ref _abyssalNavNodeSnapshot, nodeCount);
            EnsureVector3Capacity(ref _abyssalNavConduitVectorsSnapshot, nodeCount);
            EnsureFloatCapacity(ref _abyssalNavConduitStrengthSnapshot, nodeCount);
            EnsureByteCapacity(ref _abyssalNavNodeTypesSnapshot, nodeCount);
            EnsureVector3NativeCapacity(ref _abyssalNavNodeSnapshotNative, nodeCount);
            EnsureVector3NativeCapacity(ref _abyssalNavConduitVectorsSnapshotNative, nodeCount);
            EnsureFloatNativeCapacity(ref _abyssalNavConduitStrengthSnapshotNative, nodeCount);
            EnsureByteNativeCapacity(ref _abyssalNavNodeTypesSnapshotNative, nodeCount);
            EnsureAbyssalNavGraphHashCapacity(nodeCount * 4);

            bool hasOrigin = false;
            Vector3 minNode = default;

            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                for (int nodeIndex = 0; nodeIndex < payload.Count; nodeIndex++)
                {
                    Vector3 node = payload.Nodes[nodeIndex];
                    Vector3 conduitVector = payload.ConduitVectors.IsCreated && nodeIndex < payload.ConduitVectors.Length
                        ? payload.ConduitVectors[nodeIndex]
                        : Vector3.zero;
                    float conduitStrength = payload.ConduitStrengths.IsCreated && nodeIndex < payload.ConduitStrengths.Length
                        ? payload.ConduitStrengths[nodeIndex]
                        : 0f;
                    byte nodeType = payload.NodeTypes.IsCreated && nodeIndex < payload.NodeTypes.Length
                        ? payload.NodeTypes[nodeIndex]
                        : (byte)NavNodeType.Water;
                    _abyssalNavNodes.AddNoResize(node);
                    _abyssalNavNodeSnapshot[writeIndex] = node;
                    _abyssalNavConduitVectorsSnapshot[writeIndex] = conduitVector;
                    _abyssalNavConduitStrengthSnapshot[writeIndex] = conduitStrength;
                    _abyssalNavNodeTypesSnapshot[writeIndex] = nodeType;
                    _abyssalNavNodeSnapshotNative[writeIndex] = node;
                    _abyssalNavConduitVectorsSnapshotNative[writeIndex] = conduitVector;
                    _abyssalNavConduitStrengthSnapshotNative[writeIndex] = conduitStrength;
                    _abyssalNavNodeTypesSnapshotNative[writeIndex] = nodeType;
                    if (!hasOrigin)
                    {
                        minNode = node;
                        hasOrigin = true;
                    }
                    else
                    {
                        minNode.x = Mathf.Min(minNode.x, node.x);
                        minNode.y = Mathf.Min(minNode.y, node.y);
                        minNode.z = Mathf.Min(minNode.z, node.z);
                    }
                    writeIndex++;
                }
            }

            _abyssalNavGraphOrigin = hasOrigin ? minNode : Vector3.zero;
            if (_abyssalNavGraphHashNative.IsCreated)
            {
                _abyssalNavGraphHashNative.Clear();
                for (int i = 0; i < _abyssalNavNodeCount; i++)
                {
                    int key = ComputeAbyssalNavGraphHashKey(_abyssalNavNodeSnapshot[i], _abyssalNavGraphOrigin, abyssalNavGraphCellSize);
                    _abyssalNavGraphHashNative.Add(key, i);
                }
            }
        }

        private void RebuildMegaWreckStreamSnapshot()
        {
            int sectionCount = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkMegaWreckPayloads.TryGetValue(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                sectionCount += payload.Count;
            }

            _megaWreckStreamCount = sectionCount;
            if (sectionCount <= 0)
                return;

            EnsureMegaWreckSectionCapacity(ref _megaWreckStreamSnapshot, sectionCount);
            EnsureNativeCapacity(ref _megaWreckStreamSnapshotNative, sectionCount);
            int writeIndex = 0;
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                ChunkKey key = _selectedChunkKeys[i];
                if (!_chunkMegaWreckPayloads.TryGetValue(key, out ChunkMegaWreckPayload payload) || payload.Count <= 0 || payload.Sections == null)
                    continue;

                for (int sectionIndex = 0; sectionIndex < payload.Count; sectionIndex++)
                {
                    MegaWreckStreamSection section = payload.Sections[sectionIndex];
                    _megaWreckStreamSnapshot[writeIndex] = section;
                    _megaWreckStreamSnapshotNative[writeIndex] = section;
                    writeIndex++;
                }
            }
        }

        private void RebuildHLODRegistrySnapshot()
        {
            CompleteHLODCullJob(forceComplete: false);
            if (_hlodCullScheduled)
                return;

            Vector3 viewerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            int registryCount = 0;
            if (megaWreckDefinitions != null)
            {
                for (int i = 0; i < megaWreckDefinitions.Length; i++)
                {
                    if (ShouldRegisterHLOD(megaWreckDefinitions[i].Center, megaWreckDefinitions[i].Size, viewerPosition))
                        registryCount++;
                }
            }

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (ShouldRegisterHLOD(structure.Bounds.center, structure.Bounds.size, viewerPosition))
                    registryCount++;
            }

            _hlodRegistryCount = registryCount;
            if (registryCount <= 0)
            {
                _visibleHlodCount = 0;
                return;
            }

            EnsureHLODDataCapacity(ref _hlodRegistrySnapshot, registryCount);
            EnsureNativeCapacity(ref _hlodRegistrySnapshotNative, registryCount);

            int writeIndex = 0;
            if (megaWreckDefinitions != null)
            {
                for (int i = 0; i < megaWreckDefinitions.Length; i++)
                {
                    MegaWreckDefinition definition = megaWreckDefinitions[i];
                    if (!ShouldRegisterHLOD(definition.Center, definition.Size, viewerPosition))
                        continue;

                    HLODData entry = new HLODData
                    {
                        StructureId = definition.WreckId,
                        Type = StructureType.MegaWreck,
                        Center = definition.Center,
                        Size = definition.Size,
                        Fade01 = 0f
                    };
                    _hlodRegistrySnapshot[writeIndex] = entry;
                    _hlodRegistrySnapshotNative[writeIndex] = entry;
                    writeIndex++;
                }
            }

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (!ShouldRegisterHLOD(structure.Bounds.center, structure.Bounds.size, viewerPosition))
                    continue;

                HLODData entry = new HLODData
                {
                    StructureId = structure.StructureId,
                    Type = structure.Type,
                    Center = structure.Bounds.center,
                    Size = structure.Bounds.size,
                    Fade01 = 0f
                };
                _hlodRegistrySnapshot[writeIndex] = entry;
                _hlodRegistrySnapshotNative[writeIndex] = entry;
                writeIndex++;
            }
        }

        private bool ShouldRegisterHLOD(Vector3 center, Vector3 size, Vector3 viewerPosition)
        {
            float largestAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (largestAxis < hlodMinimumStructureSize)
                return false;

            double distanceSq = ComputeAupDistanceSq(center, viewerPosition);
            float maxDistance = hlodMaximumDistance + (largestAxis * 0.5f);
            double maxDistanceSq = maxDistance * maxDistance;
            return distanceSq <= maxDistanceSq;
        }

        private void ScheduleHLODVisibilityCullJob()
        {
            if (_hlodCullScheduled || _hlodRegistryCount <= 0 || !_hlodRegistrySnapshotNative.IsCreated)
                return;

            Camera activeViewCamera = ResolveActiveViewCamera();
            if (activeViewCamera == null)
            {
                _visibleHlodCount = 0;
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(activeViewCamera, _viewFrustumPlanes);
            EnsureFloat4NativeCapacity(ref _hlodFrustumPlanesNative, 6);
            for (int i = 0; i < 6; i++)
            {
                Plane plane = _viewFrustumPlanes[i];
                _hlodFrustumPlanesNative[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }

            EnsureByteNativeCapacity(ref _hlodVisibleFlagsNative, _hlodRegistryCount);
            Vector3 viewerPosition = playerTransform != null ? playerTransform.position : activeViewCamera.transform.position;
            float fullyVisibleDistance = Mathf.Max(hlodMinimumDistance, residentRadius + 1f);
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _hlodRegistrySnapshotNative[i];
                double distanceSq = ComputeAupDistanceSq(viewerPosition, entry.Center);
                float distance = distanceSq > 0d ? (float)math.sqrt(distanceSq) : 0f;
                entry.Fade01 = ComputeHLODFade01(distance, residentRadius, fullyVisibleDistance);
                _hlodRegistrySnapshot[i] = entry;
                _hlodRegistrySnapshotNative[i] = entry;
            }

            var job = new CullHLODInstancesJob
            {
                Registry = _hlodRegistrySnapshotNative,
                FrustumPlanes = _hlodFrustumPlanesNative,
                VisibleFlags = _hlodVisibleFlagsNative,
                ViewerPosition = new float3(viewerPosition.x, viewerPosition.y, viewerPosition.z),
                MinimumDistanceSq = residentRadius * residentRadius,
                MaximumDistanceSq = hlodMaximumDistance * hlodMaximumDistance,
                FrustumPadding = hlodFrustumPadding
            };

            _hlodCullHandle = job.Schedule(_hlodRegistryCount, 16);
            _hlodCullScheduled = true;
        }

        private void CompleteHLODCullJob(bool forceComplete)
        {
            if (!_hlodCullScheduled)
                return;

            if (!forceComplete && !_hlodCullHandle.IsCompleted)
                return;

            _hlodCullHandle.Complete();
            _hlodCullScheduled = false;

            int visibleCount = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                if (_hlodVisibleFlagsNative[i] != 0)
                    visibleCount++;
            }

            _visibleHlodCount = visibleCount;
            if (visibleCount <= 0)
                return;

            EnsureHLODDataCapacity(ref _visibleHlodSnapshot, visibleCount);
            EnsureNativeCapacity(ref _visibleHlodSnapshotNative, visibleCount);
            int writeIndex = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                if (_hlodVisibleFlagsNative[i] == 0)
                    continue;

                HLODData entry = _hlodRegistrySnapshotNative[i];
                _visibleHlodSnapshot[writeIndex] = entry;
                _visibleHlodSnapshotNative[writeIndex] = entry;
                writeIndex++;
            }
        }

        private static float ComputeHLODFade01(float distance, float lod0Radius, float fullyVisibleDistance)
        {
            float fadeStart = Mathf.Max(0f, lod0Radius);
            float fadeEnd = Mathf.Max(fadeStart + 1f, fullyVisibleDistance);
            return Mathf.Clamp01((distance - fadeStart) / (fadeEnd - fadeStart));
        }

        private static double ComputeAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition a = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionA);
            AbsoluteUniversePosition b = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionB);
            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private void RebuildCanopyHeightGrid()
        {
            EnsureCanopyGridBuffer();
            _canopyGridCenter = playerTransform != null ? playerTransform.position : _ecosystemThreatGridCenter;
            if (!_canopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
            {
                _canopyGridInitialized = false;
                return;
            }

            for (int i = 0; i < _canopyGridCellCount; i++)
                _canopyHeightGridNative[i] = float.NegativeInfinity;

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                Bounds bounds = GetMegaWreckSectionBounds(section);
                StampCanopyBounds(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.max.y);
            }

            StampCanopyFromChunkPool(useStructuralThickness: false);
            StampCanopyFromChunkPool(useStructuralThickness: true);
            _canopyGridInitialized = true;
        }

        private void StampCanopyFromChunkPool(bool useStructuralThickness)
        {
            if (_selectedChunkCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                    continue;

                int offset = useStructuralThickness ? payload.UnderwaterOffset : payload.SurfaceOffset;
                int count = useStructuralThickness ? payload.UnderwaterCount : payload.SurfaceCount;
                if (count <= 0)
                    continue;

                NativeChunkPool pool = ResolveChunkPool(isSurface: !useStructuralThickness, payload);
                if (!pool.Matrices.IsCreated || !pool.SemanticTypes.IsCreated || !pool.Metadata.IsCreated)
                    continue;

                int end = Mathf.Min(pool.Matrices.Length, offset + count);
                for (int poolIndex = Mathf.Max(0, offset); poolIndex < end; poolIndex++)
                {
                    int semanticType = pool.SemanticTypes[poolIndex];
                    if (useStructuralThickness)
                    {
                        if (semanticType != (int)VegetationSemanticType.ColonyHullPlating &&
                            semanticType != (int)VegetationSemanticType.ColonySupportBeam &&
                            semanticType != (int)VegetationSemanticType.DeadZoneMassiveStructure)
                        {
                            continue;
                        }
                    }
                    else if (semanticType != (int)VegetationSemanticType.FloatingSargassum)
                    {
                        continue;
                    }

                    Vector3 position = ResolveRuntimePosition(pool.Matrices[poolIndex]);
                    HectonVegetationInstanceData metadata = pool.Metadata[poolIndex];
                    float halfExtent = Mathf.Max(2f, metadata.WidthScale * (useStructuralThickness ? canopyStructureThickness : canopySargassumThickness));
                    float canopyTopY = position.y + Mathf.Max(metadata.HeightScale, useStructuralThickness ? canopyStructureThickness : canopySargassumThickness);
                    StampCanopyBounds(
                        position.x - halfExtent,
                        position.x + halfExtent,
                        position.z - halfExtent,
                        position.z + halfExtent,
                        canopyTopY);
                }
            }
        }

        private void StampCanopyBounds(float minX, float maxX, float minZ, float maxZ, float canopyY)
        {
            if (!_canopyHeightGridNative.IsCreated || _canopyGridResolution <= 0)
                return;

            int halfExtent = _canopyGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _canopyGridCenter.x) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _canopyGridCenter.z) / canopyGridCellSize) + halfExtent, 0, _canopyGridResolution - 1);
            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                int rowOffset = cellZ * _canopyGridResolution;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = rowOffset + cellX;
                    if (canopyY > _canopyHeightGridNative[index])
                        _canopyHeightGridNative[index] = canopyY;
                }
            }
        }

        private void DistortAggregateFlowVectorsByThreat(ActiveAggregateNativeBufferSet buffers, int count)
        {
            if (!_threatGridInitialized ||
                !_ecosystemThreatGridCurrentNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                count <= 0 ||
                threatWhirlpoolStrength <= 0f ||
                _currentThreatHotspotLevel < threatWhirlpoolThreshold)
            {
                return;
            }

            float radiusSq = threatWhirlpoolRadius * threatWhirlpoolRadius;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = ResolveRuntimePosition(buffers.Matrices[i]);
                float localThreat = GetThreatLevel(position);
                if (localThreat < threatWhirlpoolThreshold)
                    continue;

                Vector3 radial = position - _currentThreatHotspotPosition;
                float radialSq = (radial.x * radial.x) + (radial.z * radial.z);
                if (radialSq <= 0.0001f || radialSq > radiusSq)
                    continue;

                float swirl01 = Mathf.Clamp01((localThreat - threatWhirlpoolThreshold) / Mathf.Max(0.01f, 1f - threatWhirlpoolThreshold));
                swirl01 *= 1f - Mathf.Clamp01(Mathf.Sqrt(radialSq) / threatWhirlpoolRadius);
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x).normalized;
                Vector3 baseFlow = buffers.FlowVectors[i];
                Vector3 distortedFlow = Vector3.Lerp(baseFlow, tangent * Mathf.Max(baseFlow.magnitude, 1f), swirl01 * threatWhirlpoolStrength);
                Vector2 distortedDirection = NormalizeFlowDirection(new Vector2(distortedFlow.x, distortedFlow.z));
                buffers.FlowVectors[i] = distortedFlow;
                buffers.FlowDirections[i] = distortedDirection;
            }
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
                    ResolveChunkPool(isSurface: true, payload),
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
                    ResolveChunkPool(isSurface: false, payload),
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

        private void AccumulateChunkThreatAttractorGrid(ChunkPayload payload, ref NativeArray<float2> destination, int gridOffset)
        {
            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            float cellArea = (chunkWidth / DensityGridResolution) * (chunkDepth / DensityGridResolution);
            float safeCellArea = Mathf.Max(0.0001f, cellArea);

            if (payload.HasSurface)
            {
                float grassArea = GetGrassStepForTier(payload.GrassLodTier);
                grassArea *= grassArea;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: true, payload),
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

            if (payload.HasUnderwater)
            {
                float kelpArea = kelpStepMeters * kelpStepMeters;
                AccumulateChunkThreatAttractorGridFromSlice(
                    ResolveChunkPool(isSurface: false, payload),
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
                float x = pool.Matrices[poolIndex].m03 + _totalUniverseOffset.x;
                float z = pool.Matrices[poolIndex].m23 + _totalUniverseOffset.z;
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

        private void AccumulateChunkThreatAttractorGridFromSlice(
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
            ref NativeArray<float2> destination,
            int gridOffset)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);
            float inverseWidth = 1f / width;
            float inverseDepth = 1f / depth;
            for (int i = 0; i < count; i++)
            {
                int poolIndex = offset + i;
                float x = pool.Matrices[poolIndex].m03 + _totalUniverseOffset.x;
                float z = pool.Matrices[poolIndex].m23 + _totalUniverseOffset.z;
                if (x < minX || x > maxX || z < minZ || z > maxZ)
                    continue;

                float normalizedX = Mathf.Clamp01((x - minX) * inverseWidth) * (DensityGridResolution - 1);
                float normalizedZ = Mathf.Clamp01((z - minZ) * inverseDepth) * (DensityGridResolution - 1);
                int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = Mathf.Min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = Mathf.Min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                int type = pool.Types[poolIndex];
                int semanticType = pool.SemanticTypes[poolIndex];
                float representedArea = ResolveRepresentedArea(type, grassRepresentedArea, kelpRepresentedArea, sargassumRepresentedArea);
                float edgeCompensation = ResolveEdgeCompensation(pool.EdgeDistances[poolIndex]);
                float densityWeight = (representedArea / cellArea) * edgeCompensation;
                float2 channel = ResolveThreatAttractorChannel(semanticType, densityWeight);
                if (math.lengthsq(channel) <= 0.000001f)
                    continue;

                AddThreatAttractorCell(ref destination, gridOffset, cellX, cellZ, channel * ((1f - fracX) * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, cellZ, channel * (fracX * (1f - fracZ)));
                AddThreatAttractorCell(ref destination, gridOffset, cellX, nextCellZ, channel * ((1f - fracX) * fracZ));
                AddThreatAttractorCell(ref destination, gridOffset, nextCellX, nextCellZ, channel * (fracX * fracZ));
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

        private static float2 ResolveThreatAttractorChannel(int semanticType, float densityWeight)
        {
            switch ((VegetationSemanticType)semanticType)
            {
                case VegetationSemanticType.FloatingSargassum:
                    return new float2(densityWeight, 0f);
                case VegetationSemanticType.ColonyCable:
                case VegetationSemanticType.ColonyHullPlating:
                case VegetationSemanticType.ColonySupportBeam:
                    return new float2(0f, densityWeight);
                case VegetationSemanticType.DeadZoneMassiveStructure:
                    return new float2(0f, densityWeight * 0.35f);
                default:
                    return float2.zero;
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

        private static void AddThreatAttractorCell(ref NativeArray<float2> destination, int gridOffset, int cellX, int cellZ, float2 value)
        {
            int index = gridOffset + (cellZ * DensityGridResolution) + cellX;
            destination[index] = destination[index] + value;
        }

        private static void ClearDensityGridCells(NativeArray<float3> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float3.zero;
        }

        private static void ClearThreatAttractorGridCells(NativeArray<float2> destination, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
                destination[startIndex + i] = float2.zero;
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

            NativeArray<float2> attractorSwap = _threatAttractorGridNative;
            _threatAttractorGridNative = _threatAttractorGridScratchNative;
            _threatAttractorGridScratchNative = attractorSwap;
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

        private static float3 SampleDensityChannelsAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            if (!chunkHash.IsCreated)
                return SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount);

            int cellIndex = ComputeThreatGridCellIndex(position, gridCenter, cellSize, gridResolution);
            if (cellIndex < 0)
                return float3.zero;

            float3 density = float3.zero;
            NativeParallelMultiHashMapIterator<int> iterator;
            int chunkIndex;
            if (!chunkHash.TryGetFirstValue(cellIndex, out chunkIndex, out iterator))
                return density;

            do
            {
                if (chunkIndex < 0 || chunkIndex >= chunkCount || chunkIndex >= chunks.Length)
                    continue;

                VegetationDensityChunkRecord chunk = chunks[chunkIndex];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                density += SampleChunkDensityChannels(position.x, position.z, chunk, densityGrid);
            }
            while (chunkHash.TryGetNextValue(out chunkIndex, ref iterator));

            return density;
        }

        private static float2 SampleThreatAttractorAtPosition(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            int chunkCount)
        {
            float2 attractor = float2.zero;
            for (int i = 0; i < chunkCount; i++)
            {
                VegetationDensityChunkRecord chunk = chunks[i];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                float width = math.max(0.01f, chunk.MaxX - chunk.MinX);
                float depth = math.max(0.01f, chunk.MaxZ - chunk.MinZ);
                float normalizedX = math.saturate((position.x - chunk.MinX) / width) * (DensityGridResolution - 1);
                float normalizedZ = math.saturate((position.z - chunk.MinZ) / depth) * (DensityGridResolution - 1);
                int cellX = math.clamp((int)math.floor(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = math.clamp((int)math.floor(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = math.min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = math.min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                float2 sample00 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + cellX];
                float2 sample10 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + nextCellX];
                float2 sample01 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + cellX];
                float2 sample11 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + nextCellX];
                float2 sampleX0 = math.lerp(sample00, sample10, fracX);
                float2 sampleX1 = math.lerp(sample01, sample11, fracX);
                attractor += math.lerp(sampleX0, sampleX1, fracZ);
            }

            return attractor;
        }

        private static float2 SampleThreatAttractorAtPositionHashed(
            float3 position,
            NativeArray<VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            if (!chunkHash.IsCreated)
                return SampleThreatAttractorAtPosition(position, chunks, attractorGrid, chunkCount);

            int cellIndex = ComputeThreatGridCellIndex(position, gridCenter, cellSize, gridResolution);
            if (cellIndex < 0)
                return float2.zero;

            float2 attractor = float2.zero;
            NativeParallelMultiHashMapIterator<int> iterator;
            int chunkIndex;
            if (!chunkHash.TryGetFirstValue(cellIndex, out chunkIndex, out iterator))
                return attractor;

            do
            {
                if (chunkIndex < 0 || chunkIndex >= chunkCount || chunkIndex >= chunks.Length)
                    continue;

                VegetationDensityChunkRecord chunk = chunks[chunkIndex];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                float width = math.max(0.01f, chunk.MaxX - chunk.MinX);
                float depth = math.max(0.01f, chunk.MaxZ - chunk.MinZ);
                float normalizedX = math.saturate((position.x - chunk.MinX) / width) * (DensityGridResolution - 1);
                float normalizedZ = math.saturate((position.z - chunk.MinZ) / depth) * (DensityGridResolution - 1);
                int cellX = math.clamp((int)math.floor(normalizedX), 0, DensityGridResolution - 1);
                int cellZ = math.clamp((int)math.floor(normalizedZ), 0, DensityGridResolution - 1);
                int nextCellX = math.min(cellX + 1, DensityGridResolution - 1);
                int nextCellZ = math.min(cellZ + 1, DensityGridResolution - 1);
                float fracX = normalizedX - cellX;
                float fracZ = normalizedZ - cellZ;

                float2 sample00 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + cellX];
                float2 sample10 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + nextCellX];
                float2 sample01 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + cellX];
                float2 sample11 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + nextCellX];
                float2 sampleX0 = math.lerp(sample00, sample10, fracX);
                float2 sampleX1 = math.lerp(sample01, sample11, fracX);
                attractor += math.lerp(sampleX0, sampleX1, fracZ);
            }
            while (chunkHash.TryGetNextValue(out chunkIndex, ref iterator));

            return attractor;
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
            if (IsInsideRegisteredTerrainHole(positionWS.x, positionWS.z))
            {
                sample = default;
                return false;
            }

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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLoopGuardHit(string loopName, int maxIterations)
        {
            Debug.LogError($"[HectonMapMagicVegetationBridge] Loop guard hit: {loopName} after {maxIterations} iterations.");
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

        private static Matrix4x4 ApplyMatrixTranslationOffset(Matrix4x4 matrix, Vector3 offset)
        {
            matrix.m03 += offset.x;
            matrix.m13 += offset.y;
            matrix.m23 += offset.z;
            return matrix;
        }

        private static Matrix4x4 ConvertMatrixToStableUniverseSpace(Matrix4x4 matrix, Vector3 universeOffset)
        {
            return ApplyMatrixTranslationOffset(matrix, -universeOffset);
        }

        private Vector3 ResolveRuntimePosition(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03 + _totalUniverseOffset.x, matrix.m13 + _totalUniverseOffset.y, matrix.m23 + _totalUniverseOffset.z);
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

                jobState.Handle.Complete();
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

            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            if (dependency.IsCompleted)
                array.Dispose();
            else
                array.Dispose(dependency);

            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            if (dependency.IsCompleted)
                list.Dispose();
            else
                list.Dispose(dependency);

            list = default;
        }

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> map,
            JobHandle dependency)
            where TKey : unmanaged
            , IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            if (dependency.IsCompleted)
                map.Dispose();
            else
                map.Dispose(dependency);

            map = default;
        }

        private static JobHandle CombineOptionalHandles(JobHandle current, JobHandle next)
        {
            if (current.Equals(default))
                return next;

            if (next.Equals(default))
                return current;

            return JobHandle.CombineDependencies(current, next);
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct GenerateAnchoredVegetationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> SandMask;
            [ReadOnly] public NativeArray<byte> RockMask;
            [ReadOnly] public NativeArray<ushort> HeightSamples;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly] public NativeArray<byte> ThreatEchoFlags;
            public NativeArray<JobInstanceRecord> Output;
            public int TerrainHoleCount;
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
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public float EchoTechnoJungleThresholdBias;
            public float EchoDeadZoneKeepBoost;
            public int IgnorePlacementMasks;
            public int CorruptionMode;
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
                if (IsInsideTerrainHoleStatic(sampleX, sampleZ, TerrainHoles, TerrainHoleCount))
                    return;

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
                        IgnorePlacementMasks,
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
                bool hasPermanentEcho = SampleThreatEchoAtWorldPosition(sampleX, sampleZ, ThreatGridCenter, ThreatGridCellSize, ThreatGridResolution, ThreatEchoFlags) != 0;
                byte biomeLayer;
                int semanticType;

                if (CorruptionMode != 0)
                {
                    biomeLayer = (byte)VegetationBiomeLayer.DeadZone;
                    semanticType = DeadZoneSemanticType;
                    float corruptionScale = math.lerp(7.5f, 14.5f, Hash01(seed ^ 0x94D049BBu));
                    scale *= corruptionScale;
                    heightScale *= corruptionScale;
                    widthScale *= math.lerp(2.8f, 5f, Hash01(seed ^ 0xC13FA9A9u));
                }
                else
                {
                    biomeLayer = (byte)VegetationBiomeLayer.OrganicShelf;
                    semanticType = OrganicSemanticType;

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
                            float technoThreshold = math.max(0f, TechnoJungleThreshold - (hasPermanentEcho ? EchoTechnoJungleThresholdBias : 0f));
                            if (!TryEvaluateTechnoJungle(
                                    sampleX,
                                    sampleZ,
                                    seed,
                                    flowDirection,
                                    technoThreshold,
                                    TechnoJungleCellSize,
                                    TechnoJungleSecondaryCellSize,
                                    TechnoJungleWallWidth,
                                    TechnoJungleWarpMeters,
                                    TechnoJungleFlowAnisotropy,
                                    out float technoOccupancy))
                            {
                                return;
                            }

                            semanticType = hasPermanentEcho
                                ? ColonyCableSemanticType
                                : ResolveColonySemanticTypeStatic(
                                    seed,
                                    ColonyCableSemanticType,
                                    ColonyHullSemanticType,
                                    ColonyBeamSemanticType);
                            heightScale *= math.lerp(0.9f, 1.35f, technoOccupancy);
                            widthScale *= math.lerp(0.95f, hasPermanentEcho ? 1.45f : 1.2f, technoOccupancy);
                        }
                        else if (biomeLayer == (byte)VegetationBiomeLayer.DeadZone)
                        {
                            float deadZoneDepth = math.max(0f, WaterLevel - worldY);
                            float deadZoneDepthT = math.saturate((deadZoneDepth - DeadZoneStartDepth) / 2000f);
                            float deadZoneThreshold = math.max(0f, TechnoJungleThreshold - (hasPermanentEcho ? EchoTechnoJungleThresholdBias * 0.75f : 0f));
                            if (!TryEvaluateTechnoJungle(
                                    sampleX,
                                    sampleZ,
                                    seed ^ 0x51ED270Bu,
                                    flowDirection,
                                    deadZoneThreshold,
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
                            if (hasPermanentEcho)
                                keepChance = math.saturate(keepChance + EchoDeadZoneKeepBoost);
                            if (Hash01(seed ^ 0xC13FA9A9u) > keepChance)
                                return;

                            semanticType = DeadZoneSemanticType;
                            float deadZoneScale = math.lerp(4.5f, 12f, math.max(deadZoneDepthT, Hash01(seed ^ 0x94D049BBu)));
                            scale *= deadZoneScale;
                            heightScale *= deadZoneScale;
                            widthScale *= math.lerp(2.1f, 4.4f, math.max(deadZoneOccupancy, deadZoneDepthT));
                        }
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
            [ReadOnly] public NativeArray<ushort> HeightSamples;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            public NativeArray<JobInstanceRecord> Output;
            public int TerrainHoleCount;
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
                if (IsInsideTerrainHoleStatic(sampleX, sampleZ, TerrainHoles, TerrainHoleCount))
                    return;

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
                        0,
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
        private struct ThreatPropagationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> CurrentThreat;
            [ReadOnly] public NativeArray<byte> CurrentEchoFlags;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [WriteOnly] public NativeArray<float> NextThreat;
            [WriteOnly] public NativeArray<byte> NextThreatCompressed;
            [WriteOnly] public NativeArray<byte> NextEchoFlags;
            public int GridResolution;
            public int ThreatChunkCount;
            public int ShiftX;
            public int ShiftZ;
            public float CellSize;
            public float DeltaTime;
            public float Diffusion;
            public float DecayPerSecond;
            public float SargassumRetentionBoost;
            public float TechnoJungleRetentionBoost;
            public float SargassumAccumulationBoost;
            public float TechnoJungleAccumulationBoost;
            public float StructureThreatSuppression;
            public float StructureHazardAttraction;
            public float PermanentEchoFloor;
            public float PermanentEchoThreshold;
            public float3 GridCenter;
            public float3 EmissionPosition;
            public float EmissionRadius;
            public float EmissionStrength;

            public void Execute(int index)
            {
                if (!NextThreat.IsCreated || index < 0 || index >= NextThreat.Length || GridResolution <= 0)
                    return;

                int cellX = index % GridResolution;
                int cellZ = index / GridResolution;
                float previousThreat = SampleShiftedThreat(cellX, cellZ);
                float neighborAverage = SampleNeighborAverage(cellX, cellZ, previousThreat);
                float diffusionWeight = math.saturate(Diffusion * DeltaTime);
                float diffusedThreat = math.lerp(previousThreat, neighborAverage, diffusionWeight);

                int halfExtent = GridResolution >> 1;
                float worldX = GridCenter.x + ((cellX - halfExtent) * CellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * CellSize);
                float3 samplePosition = new float3(worldX, GridCenter.y, worldZ);
                byte hadPermanentEcho = SampleShiftedEcho(cellX, cellZ);
                float2 attractor = ThreatChunkCount > 0 && ThreatChunks.IsCreated && ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPosition(samplePosition, ThreatChunks, ThreatAttractorGrid, ThreatChunkCount)
                    : float2.zero;

                float retentionBoost = math.saturate((attractor.x * SargassumRetentionBoost) + (attractor.y * TechnoJungleRetentionBoost));
                float decayRate = math.max(0f, DecayPerSecond) * (1f - retentionBoost);
                float retention = math.exp(-decayRate * math.max(0f, DeltaTime));
                float propagatedThreat = diffusedThreat * retention;

                float localDeposit = 0f;
                if (EmissionStrength > 0f && EmissionRadius > 0f)
                {
                    float2 delta = new float2(worldX - EmissionPosition.x, worldZ - EmissionPosition.z);
                    float distance = math.length(delta);
                    if (distance <= EmissionRadius)
                    {
                        float falloff = 1f - math.saturate(distance / math.max(0.01f, EmissionRadius));
                        float accumulationBoost = 1f + (attractor.x * SargassumAccumulationBoost) + (attractor.y * TechnoJungleAccumulationBoost);
                        localDeposit = EmissionStrength * DeltaTime * falloff * accumulationBoost;
                    }
                }

                float nextThreat = math.saturate(propagatedThreat + localDeposit);
                nextThreat = ApplyArtificialStructureInfluence(index, samplePosition, nextThreat);
                byte nextEcho = hadPermanentEcho;
                if (nextThreat >= PermanentEchoThreshold)
                    nextEcho = 1;

                if (nextEcho != 0)
                    nextThreat = math.max(nextThreat, PermanentEchoFloor);

                NextThreat[index] = nextThreat;
                if (NextThreatCompressed.IsCreated && index < NextThreatCompressed.Length)
                    NextThreatCompressed[index] = EncodeThreat(nextThreat);
                if (NextEchoFlags.IsCreated && index < NextEchoFlags.Length)
                    NextEchoFlags[index] = nextEcho;
            }

            private float SampleShiftedThreat(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!CurrentThreat.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0f;
                }

                return CurrentThreat[(previousZ * GridResolution) + previousX];
            }

            private byte SampleShiftedEcho(int x, int z)
            {
                int previousX = x + ShiftX;
                int previousZ = z + ShiftZ;
                if (!CurrentEchoFlags.IsCreated ||
                    previousX < 0 ||
                    previousZ < 0 ||
                    previousX >= GridResolution ||
                    previousZ >= GridResolution)
                {
                    return 0;
                }

                return CurrentEchoFlags[(previousZ * GridResolution) + previousX];
            }

            private float SampleNeighborAverage(int x, int z, float centerThreat)
            {
                float weightedSum = centerThreat * 4f;
                float totalWeight = 4f;
                AccumulateThreatSample(x - 1, z, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x, z - 1, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x, z + 1, 2f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x - 1, z - 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z - 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x - 1, z + 1, 1f, ref weightedSum, ref totalWeight);
                AccumulateThreatSample(x + 1, z + 1, 1f, ref weightedSum, ref totalWeight);
                return weightedSum / math.max(1f, totalWeight);
            }

            private void AccumulateThreatSample(int x, int z, float weight, ref float weightedSum, ref float totalWeight)
            {
                if (x < 0 || z < 0 || x >= GridResolution || z >= GridResolution)
                    return;

                weightedSum += SampleShiftedThreat(x, z) * weight;
                totalWeight += weight;
            }

            private static byte EncodeThreat(float threat)
            {
                return (byte)math.clamp((int)math.round(math.saturate(threat) * 255f), 0, 255);
            }

            private float ApplyArtificialStructureInfluence(int cellIndex, float3 samplePosition, float threat)
            {
                if (!ArtificialStructureHash.IsCreated || !ArtificialStructures.IsCreated)
                    return threat;

                float suppression = 0f;
                float attraction = 0f;
                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return threat;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        if (samplePosition.x >= structure.MinX &&
                            samplePosition.x <= structure.MaxX &&
                            samplePosition.y >= structure.MinY &&
                            samplePosition.y <= structure.MaxY &&
                            samplePosition.z >= structure.MinZ &&
                            samplePosition.z <= structure.MaxZ)
                        {
                            switch ((StructureType)structure.Type)
                            {
                                case StructureType.BaseModule:
                                    suppression = math.max(suppression, StructureThreatSuppression);
                                    break;

                                case StructureType.HazardEmitter:
                                    attraction = math.max(attraction, StructureHazardAttraction);
                                    break;

                                case StructureType.MegaWreck:
                                    attraction = math.max(attraction, StructureHazardAttraction * 0.5f);
                                    break;

                                case StructureType.VoxelCave:
                                    suppression = math.max(suppression, StructureThreatSuppression * 0.35f);
                                    break;
                            }
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                float adjusted = threat * math.saturate(1f - suppression);
                if (attraction > 0f)
                    adjusted = math.saturate(adjusted + attraction);

                return adjusted;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct ThreatVoxelizationJob : IJobParallelFor
        {
            private const byte SolidThreat = 255;

            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ChunkHash;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [WriteOnly] public NativeArray<byte> Output;
            public int GridResolutionXZ;
            public int GridResolutionY;
            public float CellSizeXZ;
            public float CellSizeY;
            public float3 GridOrigin;
            public float3 GridCenter;
            public float KelpObstacleWeight;
            public float SargassumObstacleWeight;
            public float TechnoObstacleWeight;
            public float ObstacleHardThreshold;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    GridResolutionXZ <= 0 ||
                    GridResolutionY <= 0)
                {
                    return;
                }

                int cellsPerSlice = GridResolutionXZ * GridResolutionY;
                int cellZ = index / cellsPerSlice;
                int sliceIndex = index - (cellZ * cellsPerSlice);
                int cellY = sliceIndex / GridResolutionXZ;
                int cellX = sliceIndex - (cellY * GridResolutionXZ);
                float3 voxelCenterOffset = new float3(
                    (cellX + 0.5f) * CellSizeXZ,
                    (cellY + 0.5f) * CellSizeY,
                    (cellZ + 0.5f) * CellSizeXZ);
                float3 samplePosition = new float3(
                    GridOrigin.x + voxelCenterOffset.x,
                    GridOrigin.y + voxelCenterOffset.y,
                    GridOrigin.z + voxelCenterOffset.z);

                int columnIndex = (cellZ * GridResolutionXZ) + cellX;
                float threat = ThreatGrid.IsCreated && columnIndex >= 0 && columnIndex < ThreatGrid.Length
                    ? math.saturate(ThreatGrid[columnIndex])
                    : 0f;
                byte encodedThreat = EncodeOpenThreat(threat);
                float obstacle = SampleObstacle(samplePosition);
                bool isSolid = obstacle >= ObstacleHardThreshold || IsInsideBlockingStructure(columnIndex, samplePosition);
                Output[index] = isSolid ? SolidThreat : encodedThreat;
            }

            private float SampleObstacle(float3 position)
            {
                if (DensityChunks.IsCreated &&
                    DensityGrid.IsCreated &&
                    ChunkHash.IsCreated)
                {
                    float3 density = SampleDensityChannelsAtPositionHashed(
                        position,
                        DensityChunks,
                        DensityGrid,
                        ChunkHash,
                        GridCenter,
                        CellSizeXZ,
                        GridResolutionXZ,
                        DensityChunks.Length);
                    float2 attractor = ThreatAttractorGrid.IsCreated
                        ? SampleThreatAttractorAtPositionHashed(
                            position,
                            DensityChunks,
                            ThreatAttractorGrid,
                            ChunkHash,
                            GridCenter,
                            CellSizeXZ,
                            GridResolutionXZ,
                            DensityChunks.Length)
                        : float2.zero;
                    float obstacle = (density.y * KelpObstacleWeight) +
                                     (density.z * (SargassumObstacleWeight * 0.35f)) +
                                     (attractor.x * SargassumObstacleWeight) +
                                     (attractor.y * TechnoObstacleWeight);
                    return math.saturate(obstacle);
                }

                return 0f;
            }

            private bool IsInsideBlockingStructure(int columnIndex, float3 position)
            {
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    columnIndex < 0)
                {
                    return false;
                }

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(columnIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        if (position.x >= structure.MinX &&
                            position.x <= structure.MaxX &&
                            position.y >= structure.MinY &&
                            position.y <= structure.MaxY &&
                            position.z >= structure.MinZ &&
                            position.z <= structure.MaxZ)
                        {
                            return true;
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return false;
            }

            private static byte EncodeOpenThreat(float threat)
            {
                return (byte)math.clamp((int)math.round(math.saturate(threat) * 254f), 0, 254);
            }
        }

        private struct SwarmWakeImpulse
        {
            public float3 Position;
            public float Radius;
            public float3 FlowVector;
            public float Strength;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct BuildAbyssalFlowFieldJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> FlowChunks;
            [ReadOnly] public NativeArray<float3> FlowDensityGrid;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ChunkHash;
            [ReadOnly] public NativeArray<float> NavSupportGrid;
            [ReadOnly] public NativeArray<SwarmWakeImpulse> ExternalWakeImpulses;
            [WriteOnly] public NativeArray<float2> Output;
            public int GridResolution;
            public int ChunkCount;
            public int ExternalWakeImpulseCount;
            public float CellSize;
            public float3 GridCenter;
            public float3 PlayerPosition;
            public float3 HotspotPosition;
            public float HotspotThreatLevel;
            public uint WeatherStateMask;
            public float2 WeatherDirectionXZ;
            public float WeatherCurrentSpeed;
            public float WeatherIntensity;
            public float ThreatBias;
            public float PlayerBias;
            public float HotspotBias;
            public float ObstacleAvoidBias;
            public float NavSupportBias;
            public float KelpObstacleWeight;
            public float SargassumObstacleWeight;
            public float TechnoObstacleWeight;
            public float ObstacleSoftThreshold;
            public float ObstacleHardThreshold;

            public void Execute(int index)
            {
                if (!Output.IsCreated || index < 0 || index >= Output.Length || GridResolution <= 0)
                    return;

                int cellX = index % GridResolution;
                int cellZ = index / GridResolution;
                int halfExtent = GridResolution >> 1;
                float worldX = GridCenter.x + ((cellX - halfExtent) * CellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * CellSize);
                float3 position = new float3(worldX, GridCenter.y, worldZ);

                float2 threatGradient = math.normalizesafe(ComputeThreatGradient(cellX, cellZ), float2.zero);
                float2 toPlayer = math.normalizesafe(new float2(PlayerPosition.x - worldX, PlayerPosition.z - worldZ), threatGradient);
                float hotspotBlend = math.saturate(HotspotThreatLevel);
                float2 toHotspot = math.normalizesafe(new float2(HotspotPosition.x - worldX, HotspotPosition.z - worldZ), toPlayer);
                float2 seekDir = math.normalizesafe(
                    (threatGradient * ThreatBias) +
                    (toPlayer * PlayerBias * (1f - hotspotBlend)) +
                    (toHotspot * HotspotBias * hotspotBlend),
                    toPlayer);

                float centerObstacle = SampleObstacle(position);
                float2 obstacleGradient = ComputeObstacleGradient(position);
                float obstacleFactor = math.saturate((centerObstacle - ObstacleSoftThreshold) / math.max(0.0001f, ObstacleHardThreshold - ObstacleSoftThreshold));
                float2 avoidanceDir = math.normalizesafe(-obstacleGradient, new float2(0f, 0f));

                float navSupport = SampleNavSupport(cellX, cellZ);
                float2 roadDir = math.normalizesafe(ComputeNavGradient(cellX, cellZ), seekDir);
                float2 wakeDir = SampleWakeFlow(position);
                float2 weatherBias = ResolveWeatherBias();

                float2 combined = seekDir;
                combined += roadDir * NavSupportBias * navSupport;
                combined += wakeDir;
                combined += weatherBias;
                combined += avoidanceDir * ObstacleAvoidBias * math.max(obstacleFactor, centerObstacle);
                if (centerObstacle >= ObstacleHardThreshold && navSupport <= 0.001f)
                    combined = avoidanceDir * math.max(1f, ObstacleAvoidBias);

                float resolvedSpeed = ResolveFlowSpeedMetersPerSecond(wakeDir);
                Output[index] = math.normalizesafe(combined, float2.zero) * resolvedSpeed;
            }

            private float2 ResolveWeatherBias()
            {
                if (math.lengthsq(WeatherDirectionXZ) <= 0.0001f)
                    return float2.zero;

                return WeatherDirectionXZ * (ResolveWeatherBiasMultiplier() * math.max(0.05f, WeatherCurrentSpeed));
            }

            private float ResolveWeatherBiasMultiplier()
            {
                float stateBlend = math.max(0.15f, WeatherIntensity);
                if ((WeatherStateMask & (uint)WeatherState.ThermoclineActive) != 0u ||
                    (WeatherStateMask & (uint)WeatherState.HaloclineActive) != 0u)
                {
                    return 1.35f * stateBlend;
                }

                if ((WeatherStateMask & (uint)WeatherState.Storm) != 0u)
                    return 1f * stateBlend;

                if ((WeatherStateMask & (uint)WeatherState.Calm) != 0u)
                    return 0.15f;

                return 0f;
            }

            private float ResolveFlowSpeedMetersPerSecond(float2 wakeDir)
            {
                float baseSpeed = math.max(0.05f, WeatherCurrentSpeed * math.max(0.35f, WeatherIntensity));
                float wakeSpeed = math.length(wakeDir);
                float hotspotSpeed = math.saturate(HotspotThreatLevel) * 0.85f;
                return math.min(20f, baseSpeed + wakeSpeed + hotspotSpeed);
            }

            private float2 ComputeThreatGradient(int cellX, int cellZ)
            {
                return new float2(
                    SampleThreat(cellX + 1, cellZ) - SampleThreat(cellX - 1, cellZ),
                    SampleThreat(cellX, cellZ + 1) - SampleThreat(cellX, cellZ - 1));
            }

            private float2 ComputeNavGradient(int cellX, int cellZ)
            {
                return new float2(
                    SampleNavSupport(cellX + 1, cellZ) - SampleNavSupport(cellX - 1, cellZ),
                    SampleNavSupport(cellX, cellZ + 1) - SampleNavSupport(cellX, cellZ - 1));
            }

            private float2 ComputeObstacleGradient(float3 position)
            {
                float3 offsetX = new float3(CellSize, 0f, 0f);
                float3 offsetZ = new float3(0f, 0f, CellSize);
                return new float2(
                    SampleObstacle(position + offsetX) - SampleObstacle(position - offsetX),
                    SampleObstacle(position + offsetZ) - SampleObstacle(position - offsetZ));
            }

            private float SampleObstacle(float3 position)
            {
                if (ChunkCount <= 0 || !FlowChunks.IsCreated || !FlowDensityGrid.IsCreated)
                    return 0f;

                float3 density = SampleDensityChannelsAtPositionHashed(
                    position,
                    FlowChunks,
                    FlowDensityGrid,
                    ChunkHash,
                    GridCenter,
                    CellSize,
                    GridResolution,
                    ChunkCount);
                float2 attractor = ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPositionHashed(
                        position,
                        FlowChunks,
                        ThreatAttractorGrid,
                        ChunkHash,
                        GridCenter,
                        CellSize,
                        GridResolution,
                        ChunkCount)
                    : float2.zero;
                float obstacle = (density.y * KelpObstacleWeight) +
                                 (density.z * (SargassumObstacleWeight * 0.35f)) +
                                 (attractor.x * SargassumObstacleWeight) +
                                 (attractor.y * TechnoObstacleWeight);
                return math.saturate(obstacle);
            }

            private float SampleThreat(int cellX, int cellZ)
            {
                if (!ThreatGrid.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return ThreatGrid[(cellZ * GridResolution) + cellX];
            }

            private float SampleNavSupport(int cellX, int cellZ)
            {
                if (!NavSupportGrid.IsCreated || cellX < 0 || cellZ < 0 || cellX >= GridResolution || cellZ >= GridResolution)
                    return 0f;

                return math.saturate(NavSupportGrid[(cellZ * GridResolution) + cellX]);
            }

            private float2 SampleWakeFlow(float3 position)
            {
                if (!ExternalWakeImpulses.IsCreated || ExternalWakeImpulseCount <= 0)
                    return float2.zero;

                float2 wake = float2.zero;
                for (int i = 0; i < ExternalWakeImpulseCount; i++)
                {
                    SwarmWakeImpulse impulse = ExternalWakeImpulses[i];
                    if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                        continue;

                    float2 planarDelta = new float2(position.x - impulse.Position.x, position.z - impulse.Position.z);
                    float planarDistance = math.length(planarDelta);
                    float planarGate = math.saturate(1f - (planarDistance / math.max(impulse.Radius, 0.001f)));
                    if (planarGate <= 0f)
                        continue;

                    float verticalGate = math.saturate(1f - (math.abs(position.y - impulse.Position.y) / math.max(impulse.Radius, 0.001f)));
                    float weight = planarGate * planarGate * verticalGate * impulse.Strength;
                    wake += math.normalizesafe(new float2(impulse.FlowVector.x, impulse.FlowVector.z), float2.zero) * weight;
                }

                return wake;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct BuildAbyssalThermalGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> ThreatChunks;
            [ReadOnly] public NativeArray<float2> ThreatAttractorGrid;
            [NativeDisableParallelForRestriction]
            [WriteOnly] public NativeArray<float> Output;
            public int ChunkCount;
            public int HorizontalResolution;
            public int VerticalResolution;
            public int RingOffsetX;
            public int RingOffsetY;
            public int RingOffsetZ;
            public float HorizontalCellSize;
            public float VerticalCellSize;
            public float WaterLevel;
            public float GridDepthMeters;
            public float SurfaceTemperatureCelsius;
            public float AbyssTemperatureCelsius;
            public float ThermoclineDepth;
            public float DepthFalloffExponent;
            public float ColonyBiomeStartDepth;
            public float DeadZoneStartDepth;
            public float HotPocketBoostCelsius;
            public float HotPocketNoiseScale;
            public float HotPocketThreshold;
            public float ColonyPocketStrength;
            public float DeadZonePocketStrength;
            public float3 GridCenter;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    HorizontalResolution <= 0 ||
                    VerticalResolution <= 0)
                {
                    return;
                }

                int cellsPerLayer = HorizontalResolution * HorizontalResolution;
                int layer = index / cellsPerLayer;
                int rem = index - (layer * cellsPerLayer);
                int cellZ = rem / HorizontalResolution;
                int cellX = rem - (cellZ * HorizontalResolution);
                int halfExtent = HorizontalResolution >> 1;

                float worldX = GridCenter.x + ((cellX - halfExtent) * HorizontalCellSize);
                float worldY = WaterLevel - (layer * VerticalCellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * HorizontalCellSize);
                float depthMeters = math.clamp(WaterLevel - worldY, 0f, GridDepthMeters);
                float baseTemperature = ResolveBaseTemperature(depthMeters);
                float pocketHeat = ResolvePocketHeat(new float3(worldX, worldY, worldZ), depthMeters);
                int physicalIndex = GetPhysicalIndex(cellX, layer, cellZ);
                Output[physicalIndex] = baseTemperature + pocketHeat;
            }

            private float ResolveBaseTemperature(float depthMeters)
            {
                float normalizedDepth = math.saturate(depthMeters / math.max(1f, GridDepthMeters));
                float thermocline01 = ThermoclineDepth <= 0.01f
                    ? normalizedDepth
                    : math.saturate(depthMeters / math.max(1f, ThermoclineDepth)) * 0.24f;

                if (depthMeters > ThermoclineDepth)
                {
                    float remainingDepth = math.max(1f, GridDepthMeters - ThermoclineDepth);
                    float deep01 = math.saturate((depthMeters - ThermoclineDepth) / remainingDepth);
                    thermocline01 = 0.24f + (math.pow(deep01, math.max(0.25f, DepthFalloffExponent)) * 0.76f);
                }

                thermocline01 = math.max(thermocline01, normalizedDepth * 0.18f);
                return math.lerp(SurfaceTemperatureCelsius, AbyssTemperatureCelsius, math.saturate(thermocline01));
            }

            private float ResolvePocketHeat(float3 position, float depthMeters)
            {
                float2 attractor = ChunkCount > 0 && ThreatChunks.IsCreated && ThreatAttractorGrid.IsCreated
                    ? SampleThreatAttractorAtPosition(position, ThreatChunks, ThreatAttractorGrid, ChunkCount)
                    : float2.zero;
                float colony01 = math.saturate((depthMeters - ColonyBiomeStartDepth) / math.max(1f, DeadZoneStartDepth - ColonyBiomeStartDepth));
                colony01 *= math.saturate(attractor.y * ColonyPocketStrength);

                float deadZone01 = math.saturate((depthMeters - DeadZoneStartDepth) / math.max(1f, GridDepthMeters - DeadZoneStartDepth));
                deadZone01 *= DeadZonePocketStrength;

                float pocketNoise = SampleValueNoise(
                    ((position.x + (position.y * 0.37f)) * HotPocketNoiseScale) + 13.17f,
                    ((position.z - (position.y * 0.19f)) * HotPocketNoiseScale) + 29.41f,
                    0x91E10DA5u);
                float pocketMask = math.saturate((pocketNoise - HotPocketThreshold) / math.max(0.0001f, 1f - HotPocketThreshold));
                float pocketBias = math.max(colony01, deadZone01);
                return HotPocketBoostCelsius * pocketMask * pocketBias;
            }

            private int GetPhysicalIndex(int x, int y, int z)
            {
                int wrappedX = WrapIndex(x + RingOffsetX, HorizontalResolution);
                int wrappedY = WrapIndex(y + RingOffsetY, VerticalResolution);
                int wrappedZ = WrapIndex(z + RingOffsetZ, HorizontalResolution);
                return (wrappedY * HorizontalResolution * HorizontalResolution) + (wrappedZ * HorizontalResolution) + wrappedX;
            }

            private static int WrapIndex(int value, int length)
            {
                if (length <= 0)
                    return 0;

                int wrapped = value % length;
                return wrapped < 0 ? wrapped + length : wrapped;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct BuildAbyssalFlowVolumeJob : IJobParallelFor
        {
            private const float ThermoclineHalfBandMeters = 8f;
            private const float ThermoclineVerticalAttenuation = 0.1f;
            private const float SurfaceStormLayerDepthMeters = 50f;
            private const float StormSurfaceTurbulenceStrength = 0.4f;

            [ReadOnly] public NativeArray<float> ThermalGrid;
            [ReadOnly] public NativeArray<SwarmWakeImpulse> ExternalWakeImpulses;
            [NativeDisableParallelForRestriction]
            [WriteOnly] public NativeArray<float3> Output;
            public int HorizontalResolution;
            public int VerticalResolution;
            public int RingOffsetX;
            public int RingOffsetY;
            public int RingOffsetZ;
            public int ExternalWakeImpulseCount;
            public float HorizontalCellSize;
            public float VerticalCellSize;
            public float WaterLevel;
            public float GridDepthMeters;
            public float ThermoclineDepthMeters;
            public uint WeatherStateMask;
            public float2 WeatherDirectionXZ;
            public float WeatherCurrentSpeed;
            public float WeatherIntensity;
            public float ThermalIntensity;
            public float3 GridCenter;

            public void Execute(int index)
            {
                if (!Output.IsCreated ||
                    !ThermalGrid.IsCreated ||
                    index < 0 ||
                    index >= Output.Length ||
                    index >= ThermalGrid.Length ||
                    HorizontalResolution <= 0 ||
                    VerticalResolution <= 0)
                {
                    return;
                }

                int cellsPerLayer = HorizontalResolution * HorizontalResolution;
                int layer = index / cellsPerLayer;
                int rem = index - (layer * cellsPerLayer);
                int cellZ = rem / HorizontalResolution;
                int cellX = rem - (cellZ * HorizontalResolution);
                int halfExtent = HorizontalResolution >> 1;

                float worldX = GridCenter.x + ((cellX - halfExtent) * HorizontalCellSize);
                float worldY = WaterLevel - (layer * VerticalCellSize);
                float worldZ = GridCenter.z + ((cellZ - halfExtent) * HorizontalCellSize);
                float depthMeters = math.clamp(WaterLevel - worldY, 0f, GridDepthMeters);
                int physicalIndex = GetPhysicalIndex(cellX, layer, cellZ);
                float localTemperature = ThermalGrid[physicalIndex];
                float aboveTemperature = ThermalGrid[GetPhysicalIndex(cellX, math.max(0, layer - 1), cellZ)];
                float belowTemperature = ThermalGrid[GetPhysicalIndex(cellX, math.min(VerticalResolution - 1, layer + 1), cellZ)];

                float2 weatherDirection = math.normalizesafe(WeatherDirectionXZ, new float2(0f, 1f));
                float2 horizontalCurrent = weatherDirection * WeatherCurrentSpeed;
                float verticalCurrent = (aboveTemperature - belowTemperature) * math.max(0.05f, ThermalIntensity);
                float thermalOffset = localTemperature - belowTemperature;
                verticalCurrent += thermalOffset * 0.02f;

                if ((WeatherStateMask & (uint)WeatherState.Storm) != 0u)
                {
                    float surfaceLayer01 = 1f - math.saturate(depthMeters / math.max(SurfaceStormLayerDepthMeters, 0.0001f));
                    float stormBiasScale = WeatherCurrentSpeed * math.max(0.35f, WeatherIntensity);
                    horizontalCurrent += weatherDirection * stormBiasScale;
                    if (surfaceLayer01 > 0.0001f)
                    {
                        float noiseX = (HectonMapMagicVegetationBridge.SampleValueNoise((worldX * 0.11f) + 17.3f, (worldZ * 0.11f) + 11.1f, 0x6D2B79F5u) * 2f) - 1f;
                        float noiseZ = (HectonMapMagicVegetationBridge.SampleValueNoise((worldX * 0.13f) - 5.7f, (worldZ * 0.13f) + 23.9f, 0xB5297A4Du) * 2f) - 1f;
                        horizontalCurrent += new float2(noiseX, noiseZ) *
                                             (StormSurfaceTurbulenceStrength * surfaceLayer01 * math.max(0.1f, WeatherIntensity));
                    }
                }

                float3 flow = new float3(horizontalCurrent.x, verticalCurrent, horizontalCurrent.y);
                flow += SampleWakeImpulse(new float3(worldX, worldY, worldZ));

                if ((WeatherStateMask & ((uint)WeatherState.ThermoclineActive | (uint)WeatherState.HaloclineActive)) != 0u)
                {
                    float thermoclineBand01 = 1f - math.saturate(math.abs(depthMeters - ThermoclineDepthMeters) / math.max(ThermoclineHalfBandMeters, 0.0001f));
                    if (thermoclineBand01 > 0.0001f)
                        flow.y = math.lerp(flow.y, flow.y * ThermoclineVerticalAttenuation, thermoclineBand01);
                }

                Output[physicalIndex] = flow;
            }

            private float3 SampleWakeImpulse(float3 position)
            {
                if (!ExternalWakeImpulses.IsCreated || ExternalWakeImpulseCount <= 0)
                    return float3.zero;

                float3 wake = float3.zero;
                for (int i = 0; i < ExternalWakeImpulseCount; i++)
                {
                    SwarmWakeImpulse impulse = ExternalWakeImpulses[i];
                    if (impulse.Radius <= 0.0001f || impulse.Strength <= 0.0001f)
                        continue;

                    float3 delta = position - impulse.Position;
                    float distance = math.length(delta);
                    float weight = math.saturate(1f - (distance / math.max(impulse.Radius, 0.001f)));
                    if (weight <= 0f)
                        continue;

                    wake += math.normalizesafe(impulse.FlowVector, float3.zero) * (weight * weight * impulse.Strength);
                }

                return wake;
            }

            private int GetPhysicalIndex(int x, int y, int z)
            {
                int wrappedX = WrapIndex(x + RingOffsetX, HorizontalResolution);
                int wrappedY = WrapIndex(y + RingOffsetY, VerticalResolution);
                int wrappedZ = WrapIndex(z + RingOffsetZ, HorizontalResolution);
                return (wrappedY * HorizontalResolution * HorizontalResolution) + (wrappedZ * HorizontalResolution) + wrappedX;
            }

            private static int WrapIndex(int value, int length)
            {
                if (length <= 0)
                    return 0;

                int wrapped = value % length;
                return wrapped < 0 ? wrapped + length : wrapped;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct NativeAStarJob : IJob
        {
            [ReadOnly] public NativeArray<Vector3> Nodes;
            [ReadOnly] public NativeArray<byte> NodeTypes;
            [ReadOnly] public NativeArray<Vector3> ConduitVectors;
            [ReadOnly] public NativeArray<float> ConduitStrengths;
            [ReadOnly] public NativeArray<float> ThreatGrid;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            [ReadOnly] public NativeArray<PredatorFearNodeSnapshot> PredatorFearNodes;
            public NativeArray<int> Parents;
            public NativeArray<float> GScore;
            public NativeArray<float> FScore;
            public NativeArray<byte> ClosedFlags;
            public NativeArray<int> HeapNodes;
            public NativeArray<int> HeapPositions;
            public NativeList<Vector3> Path;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public float WaterLevel;
            public int StartNode;
            public int EndNode;
            public float3 StartPosition;
            public float3 EndPosition;
            public float NeighborRadius;
            public float VerticalTolerance;
            public float ThreatPenaltyWeight;
            public float PredatorFearPenaltyWeight;
            public float ConduitStartDepth;
            public float ConduitVerticalToleranceBonus;
            public float ConduitMisalignmentPenalty;
            public float ConduitAlignmentReward;
            public float InteriorTraversalCostMultiplier;
            public int MaxExpandedNodes;
            public int PredatorFearNodeCount;
            public int TraversalSpeciesId;

            public void Execute()
            {
                if (!Nodes.IsCreated ||
                    Nodes.Length <= 0 ||
                    !Path.IsCreated ||
                    StartNode < 0 ||
                    EndNode < 0 ||
                    StartNode >= Nodes.Length ||
                    EndNode >= Nodes.Length)
                {
                    if (Path.IsCreated)
                        Path.Clear();
                    return;
                }

                Path.Clear();
                int nodeCount = Nodes.Length;
                float neighborRadiusSq = NeighborRadius * NeighborRadius;
                int heapCount = 0;

                for (int i = 0; i < nodeCount; i++)
                {
                    Parents[i] = -1;
                    GScore[i] = float.PositiveInfinity;
                    FScore[i] = float.PositiveInfinity;
                    ClosedFlags[i] = 0;
                    HeapPositions[i] = -1;
                }

                GScore[StartNode] = 0f;
                FScore[StartNode] = HeuristicCost(StartNode);
                HeapPushOrDecrease(StartNode, ref heapCount);

                int expandedNodes = 0;
                bool foundPath = StartNode == EndNode;
                while (heapCount > 0 && expandedNodes < MaxExpandedNodes)
                {
                    int current = HeapPop(ref heapCount);
                    if (current < 0)
                        break;

                    if (ClosedFlags[current] != 0)
                        continue;

                    ClosedFlags[current] = 1;
                    expandedNodes++;
                    if (current == EndNode)
                    {
                        foundPath = true;
                        break;
                    }

                    float3 currentNode = ToFloat3(Nodes[current]);
                    for (int neighbor = 0; neighbor < nodeCount; neighbor++)
                    {
                        if (neighbor == current || ClosedFlags[neighbor] != 0)
                            continue;

                        float3 neighborNode = ToFloat3(Nodes[neighbor]);
                        float verticalDelta = neighborNode.y - currentNode.y;
                        float3 delta = neighborNode - currentNode;
                        float distanceSq = math.lengthsq(delta);
                        if (distanceSq <= 0.000001f || distanceSq > neighborRadiusSq)
                            continue;

                        float distance = math.sqrt(distanceSq);
                        float conduitStrength = ResolveConduitStrength(current, neighbor, currentNode, neighborNode, delta, distance, out float conduitAlignment, out float verticalBonus);
                        float allowedVertical = VerticalTolerance + verticalBonus;
                        if ((verticalDelta * verticalDelta) > (allowedVertical * allowedVertical))
                            continue;

                        float threatPenalty = math.saturate(SampleThreatAtWorldPosition(neighborNode)) * ThreatPenaltyWeight;
                        float conduitPenalty = conduitStrength * ((1f - conduitAlignment) * ConduitMisalignmentPenalty);
                        float conduitThreatReduction = threatPenalty * conduitStrength * conduitAlignment * math.saturate(ConduitAlignmentReward);
                        float traversalMultiplier = math.max(1f, ResolveTraversalMultiplier(current, neighbor));
                        float traversalCost = distance * traversalMultiplier;
                        float tentativeG = GScore[current] + traversalCost + math.max(0f, threatPenalty - conduitThreatReduction) + conduitPenalty;
                        if (tentativeG >= GScore[neighbor])
                            continue;

                        Parents[neighbor] = current;
                        GScore[neighbor] = tentativeG;
                        FScore[neighbor] = tentativeG + HeuristicCost(neighbor);
                        HeapPushOrDecrease(neighbor, ref heapCount);
                    }
                }

                if (!foundPath)
                    return;

                Path.AddNoResize(new Vector3(EndPosition.x, EndPosition.y, EndPosition.z));
                int nodeIndex = EndNode;
                int pathIterations = 0;
                while (nodeIndex >= 0 && pathIterations < MaxPathReconstructionIterations)
                {
                    pathIterations++;
                    float3 node = ToFloat3(Nodes[nodeIndex]);
                    Path.AddNoResize(new Vector3(node.x, node.y, node.z));
                    if (nodeIndex == StartNode)
                        break;

                    nodeIndex = Parents[nodeIndex];
                }

                Path.AddNoResize(new Vector3(StartPosition.x, StartPosition.y, StartPosition.z));
                ReversePath();
            }

            private void ReversePath()
            {
                int count = Path.Length;
                for (int i = 0; i < count >> 1; i++)
                {
                    int swapIndex = count - 1 - i;
                    Vector3 temp = Path[i];
                    Path[i] = Path[swapIndex];
                    Path[swapIndex] = temp;
                }
            }

            private float HeuristicCost(int nodeIndex)
            {
                float3 node = ToFloat3(Nodes[nodeIndex]);
                float3 goal = ToFloat3(Nodes[EndNode]);
                float horizontalDistance = math.length(node.xz - goal.xz);
                float verticalPenalty = math.abs(node.y - goal.y) * 1.85f;
                return horizontalDistance + verticalPenalty;
            }

            private float ResolveConduitStrength(
                int currentIndex,
                int neighborIndex,
                float3 currentNode,
                float3 neighborNode,
                float3 delta,
                float distance,
                out float conduitAlignment,
                out float verticalBonus)
            {
                conduitAlignment = 0f;
                verticalBonus = 0f;
                float depthMeters = math.max(0f, WaterLevel - math.min(currentNode.y, neighborNode.y));
                if (depthMeters < ConduitStartDepth ||
                    !ConduitVectors.IsCreated ||
                    !ConduitStrengths.IsCreated ||
                    currentIndex >= ConduitVectors.Length ||
                    neighborIndex >= ConduitVectors.Length ||
                    currentIndex >= ConduitStrengths.Length ||
                    neighborIndex >= ConduitStrengths.Length ||
                    distance <= 0.0001f)
                {
                    return 0f;
                }

                float currentStrength = math.saturate(ConduitStrengths[currentIndex]);
                float neighborStrength = math.saturate(ConduitStrengths[neighborIndex]);
                float combinedStrength = math.max(currentStrength, neighborStrength);
                if (combinedStrength <= 0.0001f)
                    return 0f;

                float3 conduitVector = (ToFloat3(ConduitVectors[currentIndex]) * currentStrength) + (ToFloat3(ConduitVectors[neighborIndex]) * neighborStrength);
                if (math.lengthsq(conduitVector) <= 0.0001f)
                    return 0f;

                float3 edgeDirection = delta / distance;
                float3 conduitDirection = math.normalize(conduitVector);
                conduitAlignment = math.saturate((math.dot(edgeDirection, conduitDirection) * 0.5f) + 0.5f);
                verticalBonus = ConduitVerticalToleranceBonus * combinedStrength * conduitAlignment * math.abs(conduitDirection.y);
                return combinedStrength;
            }

            private float SampleThreatAtWorldPosition(float3 position)
            {
                float voxelThreat = SampleThreatVoxelAtWorldPosition(position);
                float predatorFearThreat = SamplePredatorFearAtWorldPosition(position);

                if (!ThreatGrid.IsCreated || ThreatGridResolution <= 0 || ThreatGridCellSize <= 0f)
                    return math.max(voxelThreat, predatorFearThreat);

                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float localX = position.x - (ThreatGridCenter.x - halfExtent);
                float localZ = position.z - (ThreatGridCenter.z - halfExtent);
                if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                    return voxelThreat;

                float cellCoordX = localX / ThreatGridCellSize;
                float cellCoordZ = localZ / ThreatGridCellSize;
                int x0 = math.clamp((int)math.floor(cellCoordX), 0, ThreatGridResolution - 1);
                int z0 = math.clamp((int)math.floor(cellCoordZ), 0, ThreatGridResolution - 1);
                int x1 = math.min(x0 + 1, ThreatGridResolution - 1);
                int z1 = math.min(z0 + 1, ThreatGridResolution - 1);
                float tx = math.saturate(cellCoordX - x0);
                float tz = math.saturate(cellCoordZ - z0);

                float h00 = ThreatGrid[(z0 * ThreatGridResolution) + x0];
                float h10 = ThreatGrid[(z0 * ThreatGridResolution) + x1];
                float h01 = ThreatGrid[(z1 * ThreatGridResolution) + x0];
                float h11 = ThreatGrid[(z1 * ThreatGridResolution) + x1];
                float hx0 = math.lerp(h00, h10, tx);
                float hx1 = math.lerp(h01, h11, tx);
                float surfaceThreat = math.lerp(hx0, hx1, tz);
                return math.max(math.max(surfaceThreat, voxelThreat), predatorFearThreat);
            }

            private float SamplePredatorFearAtWorldPosition(float3 position)
            {
                if (!PredatorFearNodes.IsCreated || PredatorFearNodeCount <= 0 || TraversalSpeciesId == 0 || PredatorFearPenaltyWeight <= 0f)
                    return 0f;

                float strongest = 0f;
                int count = math.min(PredatorFearNodeCount, PredatorFearNodes.Length);
                for (int i = 0; i < count; i++)
                {
                    PredatorFearNodeSnapshot node = PredatorFearNodes[i];
                    if (node.SpeciesId != TraversalSpeciesId || node.Weight <= 0f)
                        continue;

                    float radius = math.max(node.Radius, 1f);
                    float2 delta = new float2(position.x - node.Position.x, position.z - node.Position.z);
                    float gate = 1f - math.saturate(math.length(delta) / radius);
                    if (gate <= 0f)
                        continue;

                    strongest = math.max(strongest, node.Weight * gate);
                }

                return math.saturate(strongest * PredatorFearPenaltyWeight);
            }

            private float SampleThreatVoxelAtWorldPosition(float3 position)
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return 0f;
                }

                float3 local = position - ThreatVoxelOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                    return 0f;

                int3 voxel = new int3(
                    (int)math.floor(local.x / math.max(ThreatVoxelCellSize.x, 0.001f)),
                    (int)math.floor(local.y / math.max(ThreatVoxelCellSize.y, 0.001f)),
                    (int)math.floor(local.z / math.max(ThreatVoxelCellSize.z, 0.001f)));
                if (voxel.x < 0 || voxel.y < 0 || voxel.z < 0 ||
                    voxel.x >= ThreatVoxelDimensions.x ||
                    voxel.y >= ThreatVoxelDimensions.y ||
                    voxel.z >= ThreatVoxelDimensions.z)
                {
                    return 0f;
                }

                int flatIndex = voxel.x + (voxel.y * ThreatVoxelDimensions.x) + (voxel.z * ThreatVoxelDimensions.x * ThreatVoxelDimensions.y);
                if (flatIndex < 0 || flatIndex >= ThreatVoxelGrid.Length)
                    return 0f;

                byte encoded = ThreatVoxelGrid[flatIndex];
                return encoded >= 255 ? 1f : (encoded / 254f);
            }

            private float ResolveTraversalMultiplier(int currentIndex, int neighborIndex)
            {
                if (!NodeTypes.IsCreated ||
                    currentIndex < 0 ||
                    neighborIndex < 0 ||
                    currentIndex >= NodeTypes.Length ||
                    neighborIndex >= NodeTypes.Length)
                {
                    return 1f;
                }

                if (NodeTypes[currentIndex] == (byte)NavNodeType.Interior ||
                    NodeTypes[neighborIndex] == (byte)NavNodeType.Interior)
                {
                    return math.max(1f, InteriorTraversalCostMultiplier);
                }

                return 1f;
            }

            private void HeapPushOrDecrease(int nodeIndex, ref int heapCount)
            {
                int heapIndex = HeapPositions[nodeIndex];
                if (heapIndex >= 0)
                {
                    SiftUp(heapIndex);
                    return;
                }

                if (!HeapNodes.IsCreated || heapCount >= HeapNodes.Length)
                    return;

                HeapNodes[heapCount] = nodeIndex;
                HeapPositions[nodeIndex] = heapCount;
                SiftUp(heapCount);
                heapCount++;
            }

            private int HeapPop(ref int heapCount)
            {
                if (heapCount <= 0)
                    return -1;

                int root = HeapNodes[0];
                heapCount--;
                int lastNode = HeapNodes[heapCount];
                HeapNodes[heapCount] = -1;
                HeapPositions[root] = -1;
                if (heapCount > 0)
                {
                    HeapNodes[0] = lastNode;
                    HeapPositions[lastNode] = 0;
                    SiftDown(0, heapCount);
                }

                return root;
            }

            private void SiftUp(int index)
            {
                int heapIterations = 0;
                while (index > 0 && heapIterations < MaxHeapRebalanceIterations)
                {
                    heapIterations++;
                    int parentIndex = (index - 1) >> 1;
                    int node = HeapNodes[index];
                    int parentNode = HeapNodes[parentIndex];
                    if (FScore[node] >= FScore[parentNode])
                        break;

                    HeapNodes[index] = parentNode;
                    HeapNodes[parentIndex] = node;
                    HeapPositions[node] = parentIndex;
                    HeapPositions[parentNode] = index;
                    index = parentIndex;
                }
            }

            private void SiftDown(int index, int heapCount)
            {
                int heapIterations = 0;
                while (heapIterations < MaxHeapRebalanceIterations)
                {
                    heapIterations++;
                    int left = (index << 1) + 1;
                    if (left >= heapCount)
                        break;

                    int right = left + 1;
                    int smallest = left;
                    if (right < heapCount && FScore[HeapNodes[right]] < FScore[HeapNodes[left]])
                        smallest = right;

                    if (FScore[HeapNodes[index]] <= FScore[HeapNodes[smallest]])
                        break;

                    int node = HeapNodes[index];
                    int smallestNode = HeapNodes[smallest];
                    HeapNodes[index] = smallestNode;
                    HeapNodes[smallest] = node;
                    HeapPositions[node] = smallest;
                    HeapPositions[smallestNode] = index;
                    index = smallest;
                }
            }

            private static float3 ToFloat3(Vector3 value)
            {
                return new float3(value.x, value.y, value.z);
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct StringPullPathJob : IJob
        {
            private const float FunnelEpsilon = 0.00001f;
            private const float DdaEpsilon = 0.000001f;
            private const byte SolidThreatVoxel = 255;

            [ReadOnly] public NativeArray<Vector3> InputPath;
            [ReadOnly] public NativeArray<VegetationDensityChunkRecord> DensityChunks;
            [ReadOnly] public NativeArray<float3> DensityGrid;
            [ReadOnly] public NativeArray<TerrainHoleRecord> TerrainHoles;
            [ReadOnly] public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ArtificialStructureHash;
            [ReadOnly] public NativeArray<byte> NavPassabilityGrid;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            public NativeList<Vector3> OutputPath;
            public int ChunkCount;
            public int TerrainHoleCount;
            public float3 ThreatGridCenter;
            public float ThreatGridCellSize;
            public int ThreatGridResolution;
            public int3 NavPassabilityDimensions;
            public float3 NavPassabilityOrigin;
            public float NavPassabilityCellSize;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public float SampleSpacing;
            public int MaxSamplesPerSegment;
            public float KelpWeight;
            public float SargassumWeight;
            public float DensityObstacleThreshold;

            public void Execute()
            {
                if (!OutputPath.IsCreated)
                    return;

                OutputPath.Clear();
                if (!InputPath.IsCreated || InputPath.Length <= 0)
                    return;

                int pathCount = InputPath.Length;
                if (pathCount <= 2)
                {
                    for (int i = 0; i < pathCount; i++)
                        OutputPath.AddNoResize(InputPath[i]);

                    return;
                }

                OutputPath.AddNoResize(InputPath[0]);

                int apexIndex = 0;
                int leftIndex = 0;
                int rightIndex = 0;
                float3 apex = ToFloat3(InputPath[0]);
                float3 left = apex;
                float3 right = apex;
                float3 fallbackAxis = ResolvePortalAxis(0);

                for (int portalIndex = 1; portalIndex < pathCount; portalIndex++)
                {
                    BuildPortal(portalIndex, out float3 portalLeft, out float3 portalRight, out float3 portalAxis);
                    float3 windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    bool swapPortalWinding = ScalarTripleProduct(windingAxis, portalLeft - apex, portalRight - apex) < 0f;
                    float3 originalPortalLeft = portalLeft;
                    portalLeft = math.select(portalLeft, portalRight, swapPortalWinding);
                    portalRight = math.select(portalRight, originalPortalLeft, swapPortalWinding);

                    windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    if (ScalarTripleProduct(windingAxis, right - apex, portalRight - apex) <= FunnelEpsilon)
                    {
                        bool tightenRight = IsDegenerateRay(apex, right) || ScalarTripleProduct(windingAxis, left - apex, portalRight - apex) > FunnelEpsilon;
                        if (tightenRight)
                        {
                            right = math.select(right, portalRight, tightenRight);
                            rightIndex = math.select(rightIndex, portalIndex, tightenRight);
                            fallbackAxis = math.select(fallbackAxis, portalAxis, tightenRight);
                        }
                        else
                        {
                            int emitIndex = math.max(apexIndex + 1, leftIndex);
                            OutputPath.AddNoResize(InputPath[emitIndex]);
                            apexIndex = emitIndex;
                            apex = ToFloat3(InputPath[apexIndex]);
                            left = apex;
                            right = apex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;
                            fallbackAxis = ResolvePortalAxis(apexIndex);
                            portalIndex = apexIndex;
                            continue;
                        }
                    }

                    windingAxis = ResolveWindingAxis(apex, left, right, portalLeft, portalRight, portalAxis, fallbackAxis);
                    if (ScalarTripleProduct(windingAxis, left - apex, portalLeft - apex) >= -FunnelEpsilon)
                    {
                        bool tightenLeft = IsDegenerateRay(apex, left) || ScalarTripleProduct(windingAxis, right - apex, portalLeft - apex) < -FunnelEpsilon;
                        if (tightenLeft)
                        {
                            left = math.select(left, portalLeft, tightenLeft);
                            leftIndex = math.select(leftIndex, portalIndex, tightenLeft);
                            fallbackAxis = math.select(fallbackAxis, portalAxis, tightenLeft);
                        }
                        else
                        {
                            int emitIndex = math.max(apexIndex + 1, rightIndex);
                            OutputPath.AddNoResize(InputPath[emitIndex]);
                            apexIndex = emitIndex;
                            apex = ToFloat3(InputPath[apexIndex]);
                            left = apex;
                            right = apex;
                            leftIndex = apexIndex;
                            rightIndex = apexIndex;
                            fallbackAxis = ResolvePortalAxis(apexIndex);
                            portalIndex = apexIndex;
                            continue;
                        }
                    }

                    if (portalIndex == pathCount - 1)
                        break;
                }

                Vector3 endPoint = InputPath[pathCount - 1];
                if (OutputPath.Length == 0 || !Approximately(OutputPath[OutputPath.Length - 1], endPoint))
                    OutputPath.AddNoResize(endPoint);

                CompactPathByVoxelLineOfSight();
            }

            private void BuildPortal(int index, out float3 leftPortal, out float3 rightPortal, out float3 portalAxis)
            {
                Vector3 centerValue = InputPath[index];
                float3 center = ToFloat3(centerValue);
                if (index <= 0 || index >= InputPath.Length - 1)
                {
                    leftPortal = center;
                    rightPortal = center;
                    portalAxis = ResolvePortalAxis(index);
                    return;
                }

                float3 previous = ToFloat3(InputPath[index - 1]);
                float3 next = ToFloat3(InputPath[index + 1]);
                float3 prevDirection = math.normalizesafe(center - previous, new float3(0f, 0f, 1f));
                float3 nextDirection = math.normalizesafe(next - center, prevDirection);
                portalAxis = math.normalizesafe(prevDirection + nextDirection, nextDirection);
                float3 cornerNormal = math.normalizesafe(math.cross(prevDirection, nextDirection), ResolvePerpendicular(portalAxis));
                float3 side = math.normalizesafe(math.cross(cornerNormal, portalAxis), ResolvePerpendicular(portalAxis));
                float obstacle = SampleObstacle(centerValue);
                float obstacleT = math.saturate(obstacle / math.max(0.01f, DensityObstacleThreshold));
                float maxHalfWidth = math.max(0.9f, SampleSpacing * 1.6f);
                float minHalfWidth = math.max(0.35f, SampleSpacing * 0.55f);
                float halfWidth = math.lerp(maxHalfWidth, minHalfWidth, obstacleT);
                leftPortal = center + (side * halfWidth);
                rightPortal = center - (side * halfWidth);
            }

            private float SampleObstacle(Vector3 positionValue)
            {
                float3 position = ToFloat3(positionValue);
                if (IsInsideTerrainHoleStatic(position.x, position.z, TerrainHoles, TerrainHoleCount))
                    return 0f;

                float obstacle = 0f;
                if (IsInsideBlockingStructure(position))
                    obstacle = math.max(obstacle, DensityObstacleThreshold);

                float3 density = SampleDensityChannelsAtPosition(position, DensityChunks, DensityGrid, ChunkCount);
                obstacle = math.max(obstacle, (density.y * KelpWeight) + (density.z * SargassumWeight));
                return obstacle;
            }

            private bool IsInsideBlockingStructure(float3 position)
            {
                if (!ArtificialStructures.IsCreated ||
                    !ArtificialStructureHash.IsCreated ||
                    ThreatGridResolution <= 0 ||
                    ThreatGridCellSize <= 0f)
                {
                    return false;
                }

                int cellIndex = ComputeThreatGridCellIndex(position);
                if (cellIndex < 0)
                    return false;

                NativeParallelMultiHashMapIterator<int> iterator;
                int structureIndex;
                if (!ArtificialStructureHash.TryGetFirstValue(cellIndex, out structureIndex, out iterator))
                    return false;

                do
                {
                    if (structureIndex >= 0 && structureIndex < ArtificialStructures.Length)
                    {
                        ArtificialStructureRecord structure = ArtificialStructures[structureIndex];
                        StructureType structureType = (StructureType)structure.Type;
                        if ((structureType == StructureType.BaseModule || structureType == StructureType.MegaWreck) &&
                            position.x >= structure.MinX &&
                            position.x <= structure.MaxX &&
                            position.y >= structure.MinY &&
                            position.y <= structure.MaxY &&
                            position.z >= structure.MinZ &&
                            position.z <= structure.MaxZ)
                        {
                            return true;
                        }
                    }

                }
                while (ArtificialStructureHash.TryGetNextValue(out structureIndex, ref iterator));

                return false;
            }

            private int ComputeThreatGridCellIndex(float3 position)
            {
                float halfExtent = (ThreatGridResolution - 1) * 0.5f * ThreatGridCellSize;
                float localX = position.x - (ThreatGridCenter.x - halfExtent);
                float localZ = position.z - (ThreatGridCenter.z - halfExtent);
                if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                    return -1;

                int cellX = math.clamp((int)math.floor(localX / ThreatGridCellSize), 0, ThreatGridResolution - 1);
                int cellZ = math.clamp((int)math.floor(localZ / ThreatGridCellSize), 0, ThreatGridResolution - 1);
                return (cellZ * ThreatGridResolution) + cellX;
            }

            private void CompactPathByVoxelLineOfSight()
            {
                if (OutputPath.Length <= 2 || !HasAnyVoxelGrid())
                {
                    return;
                }

                int sourceLength = OutputPath.Length;
                int lastIndex = sourceLength - 1;
                int anchorIndex = 0;
                int writeIndex = 0;

                int compactionIterations = 0;
                while (anchorIndex < lastIndex && compactionIterations < MaxPathCompactionIterations)
                {
                    compactionIterations++;
                    Vector3 anchorPoint = OutputPath[anchorIndex];
                    OutputPath[writeIndex] = anchorPoint;
                    writeIndex++;

                    int farthestVisibleIndex = anchorIndex + 1;
                    for (int candidateIndex = farthestVisibleIndex + 1; candidateIndex <= lastIndex; candidateIndex++)
                    {
                        if (!HasVoxelLineOfSight(ToFloat3(anchorPoint), ToFloat3(OutputPath[candidateIndex])))
                            break;

                        farthestVisibleIndex = candidateIndex;
                    }

                    anchorIndex = farthestVisibleIndex;
                }

                Vector3 finalPoint = OutputPath[lastIndex];
                if (writeIndex == 0 || !Approximately(OutputPath[writeIndex - 1], finalPoint))
                {
                    OutputPath[writeIndex] = finalPoint;
                    writeIndex++;
                }

                OutputPath.Length = writeIndex;
            }

            private bool HasVoxelLineOfSight(float3 start, float3 end)
            {
                float3 delta = end - start;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= DdaEpsilon)
                    return true;

                if (!TryWorldToVoxel(start, out int3 currentVoxel) ||
                    !TryWorldToVoxel(end, out int3 targetVoxel))
                {
                    return true;
                }

                float3 activeVoxelOrigin = GetActiveVoxelOrigin();
                float3 activeVoxelCellSize = GetActiveVoxelCellSize();
                int3 activeVoxelDimensions = GetActiveVoxelDimensions();
                float3 rayDirection = delta * math.rsqrt(distanceSq);
                bool3 positiveMask = rayDirection >= 0f;
                bool3 activeAxisMask = math.abs(rayDirection) > DdaEpsilon;
                int3 step = math.select(new int3(-1, -1, -1), new int3(1, 1, 1), positiveMask);
                float3 cellMin = activeVoxelOrigin + (new float3(currentVoxel.x, currentVoxel.y, currentVoxel.z) * activeVoxelCellSize);
                float3 voxelBoundary = cellMin + math.select(float3.zero, activeVoxelCellSize, positiveMask);
                float3 safeAbsDirection = math.max(math.abs(rayDirection), new float3(DdaEpsilon, DdaEpsilon, DdaEpsilon));
                float3 rayDirectionInverse = 1f / safeAbsDirection;
                float3 tMax = math.abs((voxelBoundary - start) * rayDirectionInverse);
                float3 tDelta = activeVoxelCellSize * rayDirectionInverse;
                tMax = math.select(new float3(1000000f, 1000000f, 1000000f), tMax, activeAxisMask);
                tDelta = math.select(new float3(1000000f, 1000000f, 1000000f), tDelta, activeAxisMask);

                int maxSteps = math.min(activeVoxelDimensions.x + activeVoxelDimensions.y + activeVoxelDimensions.z + 1, MaxThreatDdaSteps);
                for (int stepIndex = 0; stepIndex < maxSteps; stepIndex++)
                {
                    if (SampleVoxel(currentVoxel) >= SolidThreatVoxel)
                        return false;

                    if (math.all(currentVoxel == targetVoxel))
                        return true;

                    bool3 axisMask = (tMax <= tMax.yzx) & (tMax <= tMax.zxy);
                    tMax += math.select(float3.zero, tDelta, axisMask);
                    currentVoxel += math.select(int3.zero, step, axisMask);
                    if (!IsVoxelInside(currentVoxel))
                        return true;
                }

                return true;
            }

            private bool TryWorldToVoxel(float3 worldPosition, out int3 voxel)
            {
                float3 activeVoxelOrigin = GetActiveVoxelOrigin();
                float3 activeVoxelCellSize = GetActiveVoxelCellSize();
                float3 local = worldPosition - activeVoxelOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                {
                    voxel = int3.zero;
                    return false;
                }

                int3 candidate = new int3(
                    (int)math.floor(local.x / math.max(activeVoxelCellSize.x, DdaEpsilon)),
                    (int)math.floor(local.y / math.max(activeVoxelCellSize.y, DdaEpsilon)),
                    (int)math.floor(local.z / math.max(activeVoxelCellSize.z, DdaEpsilon)));
                if (!IsVoxelInside(candidate))
                {
                    voxel = int3.zero;
                    return false;
                }

                voxel = candidate;
                return true;
            }

            private bool IsVoxelInside(int3 voxel)
            {
                int3 activeVoxelDimensions = GetActiveVoxelDimensions();
                return voxel.x >= 0 &&
                       voxel.y >= 0 &&
                       voxel.z >= 0 &&
                       voxel.x < activeVoxelDimensions.x &&
                       voxel.y < activeVoxelDimensions.y &&
                       voxel.z < activeVoxelDimensions.z;
            }

            private byte SampleVoxel(int3 voxel)
            {
                if (HasNavPassabilityGrid())
                {
                    int flatIndex = FlattenThreatVoxelIndex(voxel, NavPassabilityDimensions);
                    if (flatIndex < 0 || flatIndex >= NavPassabilityGrid.Length)
                        return 0;

                    return NavPassabilityGrid[flatIndex];
                }

                int legacyFlatIndex = FlattenThreatVoxelIndex(voxel, ThreatVoxelDimensions);
                if (legacyFlatIndex < 0 || legacyFlatIndex >= ThreatVoxelGrid.Length)
                    return 0;

                return ThreatVoxelGrid[legacyFlatIndex];
            }

            private static int FlattenThreatVoxelIndex(int3 voxel, int3 dimensions)
            {
                return voxel.x + (voxel.y * dimensions.x) + (voxel.z * dimensions.x * dimensions.y);
            }

            private bool HasAnyVoxelGrid()
            {
                return HasNavPassabilityGrid() ||
                       (ThreatVoxelGrid.IsCreated &&
                        ThreatVoxelDimensions.x > 0 &&
                        ThreatVoxelDimensions.y > 0 &&
                        ThreatVoxelDimensions.z > 0);
            }

            private bool HasNavPassabilityGrid()
            {
                return NavPassabilityGrid.IsCreated &&
                       NavPassabilityDimensions.x > 0 &&
                       NavPassabilityDimensions.y > 0 &&
                       NavPassabilityDimensions.z > 0 &&
                       NavPassabilityCellSize > 0f;
            }

            private int3 GetActiveVoxelDimensions()
            {
                return HasNavPassabilityGrid() ? NavPassabilityDimensions : ThreatVoxelDimensions;
            }

            private float3 GetActiveVoxelOrigin()
            {
                return HasNavPassabilityGrid() ? NavPassabilityOrigin : ThreatVoxelOrigin;
            }

            private float3 GetActiveVoxelCellSize()
            {
                return HasNavPassabilityGrid()
                    ? new float3(NavPassabilityCellSize, NavPassabilityCellSize, NavPassabilityCellSize)
                    : ThreatVoxelCellSize;
            }

            private float3 ResolvePortalAxis(int index)
            {
                int clampedIndex = math.clamp(index, 0, InputPath.Length - 1);
                int previousIndex = math.max(0, clampedIndex - 1);
                int nextIndex = math.min(InputPath.Length - 1, clampedIndex + 1);
                float3 previous = ToFloat3(InputPath[previousIndex]);
                float3 next = ToFloat3(InputPath[nextIndex]);
                return math.normalizesafe(next - previous, new float3(0f, 0f, 1f));
            }

            private static float3 ToFloat3(Vector3 value)
            {
                return new float3(value.x, value.y, value.z);
            }

            private static float3 ResolvePerpendicular(float3 axis)
            {
                float3 reference = math.abs(axis.y) < 0.9f
                    ? new float3(0f, 1f, 0f)
                    : new float3(1f, 0f, 0f);
                return math.normalizesafe(math.cross(reference, axis), new float3(0f, 0f, 1f));
            }

            private static float3 ResolveWindingAxis(
                float3 apex,
                float3 left,
                float3 right,
                float3 portalLeft,
                float3 portalRight,
                float3 portalAxis,
                float3 fallbackAxis)
            {
                float3 portalCenterDirection = math.normalizesafe((((portalLeft + portalRight) * 0.5f) - apex), portalAxis);
                if (math.lengthsq(portalCenterDirection) > FunnelEpsilon)
                    return portalCenterDirection;

                float3 wedgeCenterDirection = math.normalizesafe((((left + right) * 0.5f) - apex), portalAxis);
                if (math.lengthsq(wedgeCenterDirection) > FunnelEpsilon)
                    return wedgeCenterDirection;

                return math.normalizesafe(portalAxis, math.normalizesafe(fallbackAxis, new float3(0f, 0f, 1f)));
            }

            private static float ScalarTripleProduct(float3 axis, float3 b, float3 c)
            {
                return math.dot(axis, math.cross(b, c));
            }

            private static bool IsDegenerateRay(float3 apex, float3 point)
            {
                return math.lengthsq(point - apex) <= FunnelEpsilon;
            }

            private static bool Approximately(Vector3 a, Vector3 b)
            {
                float3 delta = ToFloat3(a) - ToFloat3(b);
                return math.lengthsq(delta) <= 0.0001f;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct CullHLODInstancesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<HLODData> Registry;
            [ReadOnly] public NativeArray<float4> FrustumPlanes;
            [WriteOnly] public NativeArray<byte> VisibleFlags;
            public float3 ViewerPosition;
            public float MinimumDistanceSq;
            public float MaximumDistanceSq;
            public float FrustumPadding;

            public void Execute(int index)
            {
                if (!VisibleFlags.IsCreated || index < 0 || index >= VisibleFlags.Length || !Registry.IsCreated || index >= Registry.Length)
                    return;

                HLODData entry = Registry[index];
                float3 center = new float3(entry.Center.x, entry.Center.y, entry.Center.z);
                float3 delta = center - ViewerPosition;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq < MinimumDistanceSq || distanceSq > MaximumDistanceSq)
                {
                    VisibleFlags[index] = 0;
                    return;
                }

                float3 extents = new float3(
                    math.max(0.5f, entry.Size.x * 0.5f + FrustumPadding),
                    math.max(0.5f, entry.Size.y * 0.5f + FrustumPadding),
                    math.max(0.5f, entry.Size.z * 0.5f + FrustumPadding));
                if (!IsVisible(center, extents))
                {
                    VisibleFlags[index] = 0;
                    return;
                }

                VisibleFlags[index] = 1;
            }

            private bool IsVisible(float3 center, float3 extents)
            {
                if (!FrustumPlanes.IsCreated || FrustumPlanes.Length < 6)
                    return true;

                for (int i = 0; i < 6; i++)
                {
                    float4 plane = FrustumPlanes[i];
                    float3 normal = plane.xyz;
                    float projectedRadius = math.dot(math.abs(normal), extents);
                    float signedDistance = math.dot(normal, center) + plane.w;
                    if (signedDistance + projectedRadius < 0f)
                        return false;
                }

                return true;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
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

            float normalizedX = math.saturate(localX / math.max(0.01f, terrainSize.x));
            float normalizedZ = math.saturate(localZ / math.max(0.01f, terrainSize.z));
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
            float dx = math.max(0.001f, (x1 - x0) * (terrainSize.x / math.max(1f, heightResolution - 1)));
            float dz = math.max(0.001f, (z1 - z0) * (terrainSize.z / math.max(1f, heightResolution - 1)));
            float heightScale = terrainSize.y * (1f / 65535f);
            float hLeft = heights[(centerZ * heightResolution) + x0] * heightScale;
            float hRight = heights[(centerZ * heightResolution) + x1] * heightScale;
            float hDown = heights[(z0 * heightResolution) + centerX] * heightScale;
            float hUp = heights[(z1 * heightResolution) + centerX] * heightScale;
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

        private static void ShiftChunkPayloadBounds(ref ChunkPayload payload, Vector3 offset)
        {
            payload.MinX += offset.x;
            payload.MaxX += offset.x;
            payload.MinZ += offset.z;
            payload.MaxZ += offset.z;
            payload.WorldBounds.center += offset;
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
                bytes += (long)buffer.HeightSamplesNative.Length * sizeof(ushort);

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

        private void UpdateNativePoolDefragState(float dt)
        {
            if (dt <= 0f || playerTransform == null)
            {
                _idleNativePoolTimer = 0f;
                return;
            }

            Vector2 planarVelocity = new Vector2(_playerVelocity.x, _playerVelocity.z);
            if (planarVelocity.sqrMagnitude <= (nativePoolDefragIdleSpeedThreshold * nativePoolDefragIdleSpeedThreshold))
            {
                _idleNativePoolTimer += dt;
                return;
            }

            _idleNativePoolTimer = 0f;
        }

        private void TryScheduleNativePoolDefrag()
        {
            if (_poolDefragScheduled ||
                _idleNativePoolTimer < nativePoolDefragIdleSeconds ||
                _chunkBuildJobs.Count > 0 ||
                _activeSetDirty ||
                ComputeNativePoolFragmentationPercent() < nativePoolDefragThresholdPercent)
            {
                return;
            }

            _surfaceDefragMoveCount = BuildPoolDefragPlan(_surfaceChunkPool, isSurface: true, ref _surfaceDefragKeys, ref _surfaceDefragOffsets, ref _surfaceDefragMovesNative, out _surfaceDefragCompactUsedCount);
            _underwaterDefragMoveCount = BuildPoolDefragPlan(_underwaterChunkPool, isSurface: false, ref _underwaterDefragKeys, ref _underwaterDefragOffsets, ref _underwaterDefragMovesNative, out _underwaterDefragCompactUsedCount);
            if (_surfaceDefragMoveCount <= 0 && _underwaterDefragMoveCount <= 0)
                return;

            EnsureDefragScratchPoolCapacity(ref _surfaceDefragScratchPool, _surfaceChunkPool.Capacity);
            EnsureDefragScratchPoolCapacity(ref _underwaterDefragScratchPool, _underwaterChunkPool.Capacity);
            InitializeDefragScratchFreeList(
                ref _surfaceDefragScratchFreeBlocks,
                ref _surfaceDefragScratchFreeBlockCount,
                _surfaceDefragScratchPool.Capacity,
                _surfaceDefragCompactUsedCount);
            InitializeDefragScratchFreeList(
                ref _underwaterDefragScratchFreeBlocks,
                ref _underwaterDefragScratchFreeBlockCount,
                _underwaterDefragScratchPool.Capacity,
                _underwaterDefragCompactUsedCount);

            _surfacePoolDefragHandle = SchedulePoolDefrag(_surfaceChunkPool, _surfaceDefragScratchPool, _surfaceDefragMovesNative, _surfaceDefragMoveCount);
            _underwaterPoolDefragHandle = SchedulePoolDefrag(_underwaterChunkPool, _underwaterDefragScratchPool, _underwaterDefragMovesNative, _underwaterDefragMoveCount);
            _poolDefragScheduled = true;
            _idleNativePoolTimer = 0f;
        }

        private void CompleteNativePoolDefragIfReady(bool forceComplete)
        {
            if (!_poolDefragScheduled)
                return;

            bool surfaceReady = _surfaceDefragMoveCount <= 0 || _surfacePoolDefragHandle.IsCompleted;
            bool underwaterReady = _underwaterDefragMoveCount <= 0 || _underwaterPoolDefragHandle.IsCompleted;
            if (!forceComplete && (!surfaceReady || !underwaterReady))
                return;

            if (_surfaceDefragMoveCount > 0)
                _surfacePoolDefragHandle.Complete();
            if (_underwaterDefragMoveCount > 0)
                _underwaterPoolDefragHandle.Complete();

            if (_surfaceDefragMoveCount > 0)
            {
                SwapChunkPools(ref _surfaceChunkPool, ref _surfaceDefragScratchPool);
                ApplyPoolDefragOffsets(_surfaceDefragKeys, _surfaceDefragOffsets, _surfaceDefragMoveCount, isSurface: true);
                SwapPoolFreeLists(
                    ref _surfacePoolFreeBlocks,
                    ref _surfacePoolFreeBlockCount,
                    ref _surfaceDefragScratchFreeBlocks,
                    ref _surfaceDefragScratchFreeBlockCount);
                ResetPayloadPoolSetFlags(isSurface: true);
            }

            if (_underwaterDefragMoveCount > 0)
            {
                SwapChunkPools(ref _underwaterChunkPool, ref _underwaterDefragScratchPool);
                ApplyPoolDefragOffsets(_underwaterDefragKeys, _underwaterDefragOffsets, _underwaterDefragMoveCount, isSurface: false);
                SwapPoolFreeLists(
                    ref _underwaterPoolFreeBlocks,
                    ref _underwaterPoolFreeBlockCount,
                    ref _underwaterDefragScratchFreeBlocks,
                    ref _underwaterDefragScratchFreeBlockCount);
                ResetPayloadPoolSetFlags(isSurface: false);
            }

            _surfacePoolDefragHandle = default;
            _underwaterPoolDefragHandle = default;
            _poolDefragScheduled = false;
            _surfaceDefragMoveCount = 0;
            _underwaterDefragMoveCount = 0;
            _surfaceDefragCompactUsedCount = 0;
            _underwaterDefragCompactUsedCount = 0;
            if (isActiveAndEnabled)
                _activeSetDirty = !RebuildAndBindActiveBuffers();
        }

        private int BuildPoolDefragPlan(
            NativeChunkPool pool,
            bool isSurface,
            ref ChunkKey[] keys,
            ref int[] destinationOffsets,
            ref NativeArray<ChunkSliceMoveRecord> movesNative,
            out int compactUsedCount)
        {
            compactUsedCount = 0;
            if (!pool.Matrices.IsCreated || _chunkPayloads.Count <= 0)
                return 0;

            EnsureChunkKeyCapacity(ref keys, _chunkPayloads.Count);
            EnsureIntCapacity(ref destinationOffsets, _chunkPayloads.Count);
            EnsureNativeCapacity(ref movesNative, _chunkPayloads.Count);

            int moveCount = 0;
            int nextOffset = 0;
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                ChunkPayload payload = enumerator.Current.Value;
                int sourceOffset = isSurface ? payload.SurfaceOffset : payload.UnderwaterOffset;
                int count = isSurface ? payload.SurfaceCount : payload.UnderwaterCount;
                if (count <= 0)
                    continue;

                keys[moveCount] = key;
                destinationOffsets[moveCount] = nextOffset;
                movesNative[moveCount] = new ChunkSliceMoveRecord
                {
                    SourceOffset = sourceOffset,
                    DestinationOffset = nextOffset,
                    Count = count
                };
                nextOffset += count;
                moveCount++;
            }

            compactUsedCount = nextOffset;
            return moveCount;
        }

        private static JobHandle SchedulePoolDefrag(
            NativeChunkPool sourcePool,
            NativeChunkPool destinationPool,
            NativeArray<ChunkSliceMoveRecord> moves,
            int moveCount)
        {
            if (moveCount <= 0)
                return default;

            var job = new DefragPoolJob
            {
                Moves = moves,
                MoveCount = moveCount,
                SourceMatrices = sourcePool.Matrices,
                SourceMetadata = sourcePool.Metadata,
                SourceTypes = sourcePool.Types,
                SourceSemanticTypes = sourcePool.SemanticTypes,
                SourceBiomeLayers = sourcePool.BiomeLayers,
                SourceEdgeDistances = sourcePool.EdgeDistances,
                SourceFlowDirections = sourcePool.FlowDirections,
                SourceFlowVectors = sourcePool.FlowVectors,
                DestinationMatrices = destinationPool.Matrices,
                DestinationMetadata = destinationPool.Metadata,
                DestinationTypes = destinationPool.Types,
                DestinationSemanticTypes = destinationPool.SemanticTypes,
                DestinationBiomeLayers = destinationPool.BiomeLayers,
                DestinationEdgeDistances = destinationPool.EdgeDistances,
                DestinationFlowDirections = destinationPool.FlowDirections,
                DestinationFlowVectors = destinationPool.FlowVectors
            };

            return job.Schedule();
        }

        private void ApplyPoolDefragOffsets(ChunkKey[] keys, int[] offsets, int moveCount, bool isSurface)
        {
            for (int i = 0; i < moveCount; i++)
            {
                ChunkKey key = keys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                if (isSurface)
                    payload.SurfaceOffset = offsets[i];
                else
                    payload.UnderwaterOffset = offsets[i];

                _chunkPayloads[key] = payload;
            }
        }

        private static int ComputeUsedCompactCount(Dictionary<ChunkKey, ChunkPayload> payloads, bool isSurface)
        {
            int maxUsed = 0;
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = payloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkPayload payload = enumerator.Current.Value;
                int offset = isSurface ? payload.SurfaceOffset : payload.UnderwaterOffset;
                int count = isSurface ? payload.SurfaceCount : payload.UnderwaterCount;
                maxUsed = Mathf.Max(maxUsed, offset + count);
            }

            return maxUsed;
        }

        private static void ResetPoolFreeList(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int capacity, int usedCount)
        {
            EnsurePoolBlockCapacity(ref freeBlocks, 1);
            int clampedUsed = Mathf.Clamp(usedCount, 0, capacity);
            int freeLength = Mathf.Max(0, capacity - clampedUsed);
            if (freeLength <= 0)
            {
                freeBlockCount = 0;
                if (freeBlocks.Length > 0)
                    freeBlocks[0] = default;
                return;
            }

            freeBlocks[0] = new PoolBlock
            {
                Offset = clampedUsed,
                Length = freeLength
            };
            freeBlockCount = 1;
        }

        private static void InitializeDefragScratchFreeList(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int capacity, int compactUsedCount)
        {
            ResetPoolFreeList(ref freeBlocks, ref freeBlockCount, capacity, compactUsedCount);
        }

        private static void EnsureDefragScratchPoolCapacity(ref NativeChunkPool scratchPool, int capacity)
        {
            if (capacity <= 0)
                return;

            if (scratchPool.Matrices.IsCreated && scratchPool.Capacity == capacity)
                return;

            PoolBlock[] scratchBlocks = null;
            int scratchBlockCount = 0;
            InitializeChunkPool(ref scratchPool, capacity, ref scratchBlocks, ref scratchBlockCount);
        }

        private static void SwapChunkPools(ref NativeChunkPool a, ref NativeChunkPool b)
        {
            NativeChunkPool temp = a;
            a = b;
            b = temp;
        }

        private static void SwapPoolFreeLists(ref PoolBlock[] a, ref int aCount, ref PoolBlock[] b, ref int bCount)
        {
            PoolBlock[] blocks = a;
            a = b;
            b = blocks;

            int count = aCount;
            aCount = bCount;
            bCount = count;
        }

        private void ResetPayloadPoolSetFlags(bool isSurface)
        {
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                ChunkPayload payload = enumerator.Current.Value;
                if (isSurface)
                {
                    if (payload.SurfaceCount <= 0)
                        continue;

                    payload.SurfacePoolSet = 0;
                }
                else
                {
                    if (payload.UnderwaterCount <= 0)
                        continue;

                    payload.UnderwaterPoolSet = 0;
                }

                _chunkPayloads[key] = payload;
            }
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
                    _cachedViewCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
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
            long guardBytes = Mathf.Max(MinimumNativePoolBudgetMb, nativePoolGuardMb) * 1024L * 1024L;
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

        private static bool TryPrepareRendererWriteBuffer(ref JobHandle readerHandle)
        {
            if (!readerHandle.Equals(default) && !readerHandle.IsCompleted)
                return false;

            readerHandle.Complete();
            readerHandle = default;
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
            DisposeNativeArray(ref buffers.Matrices);
            DisposeNativeArray(ref buffers.Metadata);
            DisposeNativeArray(ref buffers.Types);
            DisposeNativeArray(ref buffers.SemanticTypes);
            DisposeNativeArray(ref buffers.BiomeLayers);
            DisposeNativeArray(ref buffers.FlowDirections);
            DisposeNativeArray(ref buffers.FlowVectors);
        }

        private bool TryBootstrapExistingTiles()
        {
            if (_startupBootstrapTileCount > 0 || _startupBootstrapTileCursor > 0)
                return true;

            if (mapMagicBridge == null || mapMagicBridge.RuntimeMapMagicObject == null)
                return false;

            _startupBootstrapTiles = mapMagicBridge.RuntimeMapMagicObject.GetComponentsInChildren<TerrainTile>(true); // COLD ALLOC: TerrainTile[] deferred bootstrap snapshot for already-applied tiles - owner: HectonMapMagicVegetationBridge
            _startupBootstrapTileCount = _startupBootstrapTiles != null ? _startupBootstrapTiles.Length : 0;
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

            int safeBatchSize = Mathf.Max(1, batchSize);
            int processedCount = 0;
            while (_startupBootstrapTileCursor < _startupBootstrapTileCount &&
                   processedCount < safeBatchSize)
            {
                TerrainTile tile = _startupBootstrapTiles[_startupBootstrapTileCursor++];
                if (tile == null || ResolveMainTerrain(tile) == null || IsForeignTile(tile))
                    continue;

                UpsertTileState(tile);
                processedCount++;
            }

            return processedCount;
        }

        private void ReleaseDeferredStartupTileSnapshot()
        {
            _startupBootstrapTiles = Array.Empty<TerrainTile>();
            _startupBootstrapTileCount = 0;
            _startupBootstrapTileCursor = 0;
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
            RefreshTerrainTextureCaches(state, terrainData);
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


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _isRegistered = false;
        }

        private void TrySubscribeEvents()
        {
            if (_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied += HandleTileApplied;
            TerrainTile.OnTileMoved += HandleTileMoved;
            HectonFloatingOrigin.RegisterListener(this);
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeEvents()
        {
            if (!_eventsSubscribed)
                return;

            TerrainTile.OnTileApplied -= HandleTileApplied;
            TerrainTile.OnTileMoved -= HandleTileMoved;
            HectonFloatingOrigin.UnregisterListener(this);
            _eventsSubscribed = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled)
                return;

            ApplyWorldOffsetToAllChunks(shiftData.ShiftOffset);
            if (_predatorFearNodeCount > 0)
            {
                float3 offset = shiftData.ShiftOffset;
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
                if (_abyssalPathRawResultNative.IsCreated)
                    _abyssalPathRawResultNative.Clear();
                if (_abyssalPathResultNative.IsCreated)
                    _abyssalPathResultNative.Clear();
            }
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
            _chunkPayloadUsedBytes = Math.Max(0L, _chunkPayloadUsedBytes - GetChunkPayloadStorageBytes(payload));

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

        private void EnsureTerrainHoleCapacity(int requiredCount)
        {
            if (_terrainHoleRecords != null && _terrainHoleRecords.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(4, requiredCount));
            // COLD ALLOC: TerrainHoleRecord[nextCapacity] - terrain-hole registry growth - owner: HectonMapMagicVegetationBridge
            TerrainHoleRecord[] expanded = new TerrainHoleRecord[nextCapacity];
            if (_terrainHoleRecords != null && _terrainHoleCount > 0)
                Array.Copy(_terrainHoleRecords, expanded, _terrainHoleCount);

            _terrainHoleRecords = expanded;
        }

        private static void EnsureTerrainHoleStreamingCapacity(ref TerrainHoleStreamingRecord[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(4, requiredCount));
            // COLD ALLOC: TerrainHoleStreamingRecord[nextCapacity] - terrain-hole streaming snapshot growth - owner: HectonMapMagicVegetationBridge
            TerrainHoleStreamingRecord[] expanded = new TerrainHoleStreamingRecord[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
        }

        private void SyncTerrainHoleNativeCache()
        {
            if (_terrainHoleCount <= 0)
            {
                if (_terrainHoleRecordsNative.IsCreated)
                {
                    if (_terrainHoleRecordsNative.Length == 0)
                        return;

                    _terrainHoleRecordsNative.Dispose();
                }

                // COLD ALLOC: NativeArray<TerrainHoleRecord>[0] - keeps terrain-hole job input valid when no holes are registered - owner: HectonMapMagicVegetationBridge
                _terrainHoleRecordsNative = new NativeArray<TerrainHoleRecord>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                if (_terrainHoleStreamingRecordsNative.IsCreated)
                {
                    if (_terrainHoleStreamingRecordsNative.Length != 0)
                        _terrainHoleStreamingRecordsNative.Dispose();
                }

                // COLD ALLOC: NativeArray<TerrainHoleStreamingRecord>[0] - keeps terrain-hole streaming payload valid when no holes are registered - owner: HectonMapMagicVegetationBridge
                _terrainHoleStreamingRecordsNative = new NativeArray<TerrainHoleStreamingRecord>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                return;
            }

            EnsureNativeCapacity(ref _terrainHoleRecordsNative, _terrainHoleCount);
            EnsureTerrainHoleStreamingCapacity(ref _terrainHoleStreamingRecords, _terrainHoleCount);
            EnsureNativeCapacity(ref _terrainHoleStreamingRecordsNative, _terrainHoleCount);
            for (int i = 0; i < _terrainHoleCount; i++)
            {
                _terrainHoleRecordsNative[i] = _terrainHoleRecords[i];
                TerrainHoleRecord hole = _terrainHoleRecords[i];
                TerrainHoleStreamingRecord streamingRecord = new TerrainHoleStreamingRecord
                {
                    HoleId = hole.HoleId,
                    Position = new Vector3(hole.X, hole.Y, hole.Z),
                    Radius = hole.Radius,
                    SourceType = hole.SourceType
                };
                _terrainHoleStreamingRecords[i] = streamingRecord;
                _terrainHoleStreamingRecordsNative[i] = streamingRecord;
            }
        }

        private void InvalidateChunksIntersectingHole(Vector3 position, float radius)
        {
            float radiusSq = radius * radius;
            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                if (DoesChunkIntersectHole(payloadEnumerator.Current.Value, position.x, position.z, radiusSq))
                    _evictionKeys.Add(payloadEnumerator.Current.Key);
            }

            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    ReleaseChunkPayloadStorage(payload);

                _chunkPayloads.Remove(key);
                RemoveChunkAbyssalNavPayload(key);
                RemoveChunkMegaWreckPayload(key);
            }

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator jobEnumerator = _chunkBuildJobs.GetEnumerator();
            while (jobEnumerator.MoveNext())
            {
                ChunkBuildJobState jobState = jobEnumerator.Current.Value;
                if (jobState == null || !DoesChunkIntersectHole(jobState.PayloadHeader, position.x, position.z, radiusSq))
                    continue;

                _jobScratchKeys.Add(jobEnumerator.Current.Key);
            }

            for (int i = 0; i < _jobScratchKeys.Count; i++)
            {
                ChunkKey key = _jobScratchKeys[i];
                CancelChunkBuildJob(key);
            }

            _activeSetDirty = true;
        }

        private void InvalidateChunksIntersectingTerrainHoleRange(int startIndex, int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                int holeIndex = startIndex + i;
                if (holeIndex < 0 || holeIndex >= _terrainHoleCount)
                    break;

                TerrainHoleRecord hole = _terrainHoleRecords[holeIndex];
                InvalidateChunksIntersectingHole(new Vector3(hole.X, 0f, hole.Z), hole.Radius);
            }
        }

        private static bool DoesChunkIntersectHole(ChunkPayload payload, float holeX, float holeZ, float radiusSq)
        {
            float clampedX = Mathf.Clamp(holeX, payload.MinX, payload.MaxX);
            float clampedZ = Mathf.Clamp(holeZ, payload.MinZ, payload.MaxZ);
            float dx = holeX - clampedX;
            float dz = holeZ - clampedZ;
            return (dx * dx) + (dz * dz) <= radiusSq;
        }

        private static int CountSemanticType(NativeChunkPool pool, int offset, int count, int semanticType)
        {
            if (!pool.SemanticTypes.IsCreated || !pool.Matrices.IsCreated || count <= 0)
                return 0;

            int resolvedCount = 0;
            int end = math.min(pool.SemanticTypes.Length, offset + count);
            for (int i = math.max(0, offset); i < end; i++)
            {
                if (pool.SemanticTypes[i] == semanticType)
                    resolvedCount++;
            }

            return resolvedCount;
        }

        private static void CopySemanticAnchorPositions(
            NativeChunkPool pool,
            int offset,
            int count,
            int semanticType,
            Vector3[] managedPositions,
            NativeArray<Vector3> nativePositions,
            Vector3 universeOffset,
            ref int writeIndex)
        {
            if (!pool.SemanticTypes.IsCreated || !pool.Matrices.IsCreated || count <= 0)
                return;

            int end = math.min(pool.SemanticTypes.Length, offset + count);
            for (int i = math.max(0, offset); i < end; i++)
            {
                if (pool.SemanticTypes[i] != semanticType)
                    continue;

                Vector3 position = new Vector3(
                    pool.Matrices[i].m03 + universeOffset.x,
                    pool.Matrices[i].m13 + universeOffset.y,
                    pool.Matrices[i].m23 + universeOffset.z);
                managedPositions[writeIndex] = position;
                nativePositions[writeIndex] = position;
                writeIndex++;
            }
        }

        private bool TryAllocateChunkSliceForWrite(bool isSurface, int count, out int offset, out bool useScratchPool)
        {
            offset = -1;
            useScratchPool = false;

            if (_poolDefragScheduled)
            {
                if (isSurface && _surfaceDefragMoveCount > 0)
                {
                    useScratchPool = TryAllocateChunkSlice(ref _surfaceDefragScratchFreeBlocks, ref _surfaceDefragScratchFreeBlockCount, count, out offset);
                    if (useScratchPool)
                        return true;
                }

                if (!isSurface && _underwaterDefragMoveCount > 0)
                {
                    useScratchPool = TryAllocateChunkSlice(ref _underwaterDefragScratchFreeBlocks, ref _underwaterDefragScratchFreeBlockCount, count, out offset);
                    if (useScratchPool)
                        return true;
                }
            }

            return isSurface
                ? TryAllocateChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, count, out offset)
                : TryAllocateChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, count, out offset);
        }

        private NativeChunkPool ResolveChunkPool(bool isSurface, ChunkPayload payload)
        {
            if (isSurface)
                return payload.SurfacePoolSet == 0 ? _surfaceChunkPool : _surfaceDefragScratchPool;

            return payload.UnderwaterPoolSet == 0 ? _underwaterChunkPool : _underwaterDefragScratchPool;
        }

        private void FreeChunkSliceForPayload(bool isSurface, ChunkPayload payload)
        {
            if (isSurface)
            {
                if (payload.SurfaceCount <= 0)
                    return;

                if (payload.SurfacePoolSet == 0)
                    FreeChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, payload.SurfaceOffset, payload.SurfaceCount);
                else
                    FreeChunkSlice(ref _surfaceDefragScratchFreeBlocks, ref _surfaceDefragScratchFreeBlockCount, payload.SurfaceOffset, payload.SurfaceCount);

                return;
            }

            if (payload.UnderwaterCount <= 0)
                return;

            if (payload.UnderwaterPoolSet == 0)
                FreeChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, payload.UnderwaterOffset, payload.UnderwaterCount);
            else
                FreeChunkSlice(ref _underwaterDefragScratchFreeBlocks, ref _underwaterDefragScratchFreeBlockCount, payload.UnderwaterOffset, payload.UnderwaterCount);
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
            int insertWatchdog = freeBlockCount + 1;
            while (insertIndex > 0 &&
                   offset < freeBlocks[insertIndex - 1].Offset &&
                   insertWatchdog-- > 0)
            {
                freeBlocks[insertIndex] = freeBlocks[insertIndex - 1];
                insertIndex--;
            }

            freeBlocks[insertIndex] = new PoolBlock { Offset = offset, Length = count };
            freeBlockCount++;

            int mergeIndex = Mathf.Max(0, insertIndex - 1);
            int mergeWatchdog = freeBlockCount + 1;
            while (mergeIndex < freeBlockCount - 1 && mergeWatchdog-- > 0)
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
            EnsureFloat2NativeCapacity(ref _threatAttractorGridNative, chunkCount * DensityGridCellCount);
            EnsureFloat2NativeCapacity(ref _threatAttractorGridScratchNative, chunkCount * DensityGridCellCount);
        }

        private void DisposeAllTileNativeCaches()
        {
            FinalizePendingTileHeightReadbacks();
            TryDisposeDeferredTileCacheReadbacks();
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
                QueueDeferredTileCacheDisposal(enumerator.Current.Value);

            TryDisposeDeferredTileCacheReadbacks();
        }

        private void DisposeTerrainHoleCache()
        {
            DisposeNativeArray(ref _terrainHoleRecordsNative);
            DisposeNativeArray(ref _terrainHoleStreamingRecordsNative);
            _terrainHoleCount = 0;
            _persistentTerrainHoleCount = 0;
            _megaWreckInteriorMaskHash = 0;
            _nextTerrainHoleId = 1;
        }

        private static void DisposeTileNativeCaches(TileRuntimeState state)
        {
            if (state == null)
                return;

            DisposeTileNativeCacheBuffer(ref state.PrimaryCacheBuffer);
            DisposeTileNativeCacheBuffer(ref state.SecondaryCacheBuffer);
            state.ActiveCacheBufferIndex = 0;
            state.PendingCacheBufferIndex = 0;
            state.HeightReadbackPending = false;
            state.HeightReadbackRequest = default;
        }

        private static void QueueDeferredTileCacheDisposal(TileRuntimeState state)
        {
            if (state == null)
                return;

            if (state.HeightReadbackPending && !state.HeightReadbackRequest.done)
            {
                s_DeferredTileCacheDisposals.Add(new DeferredTileCacheDisposal
                {
                    Request = state.HeightReadbackRequest,
                    PrimaryCacheBuffer = state.PrimaryCacheBuffer,
                    SecondaryCacheBuffer = state.SecondaryCacheBuffer
                });

                state.PrimaryCacheBuffer = default;
                state.SecondaryCacheBuffer = default;
                state.ActiveCacheBufferIndex = 0;
                state.PendingCacheBufferIndex = 0;
                state.HeightReadbackPending = false;
                state.HeightReadbackRequest = default;
                return;
            }

            DisposeTileNativeCaches(state);
        }

        private static void TryDisposeDeferredTileCacheReadbacks()
        {
            for (int i = s_DeferredTileCacheDisposals.Count - 1; i >= 0; i--)
            {
                DeferredTileCacheDisposal disposal = s_DeferredTileCacheDisposals[i];
                if (!disposal.Request.done)
                    continue;

                DisposeTileNativeCacheBuffer(ref disposal.PrimaryCacheBuffer);
                DisposeTileNativeCacheBuffer(ref disposal.SecondaryCacheBuffer);
                s_DeferredTileCacheDisposals.RemoveAt(i);
            }
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
                buffer.HeightSamplesNative = new NativeArray<ushort>(heightSampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (bufferIndex == 0)
                state.PrimaryCacheBuffer = buffer;
            else
                state.SecondaryCacheBuffer = buffer;
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
            DisposeNativeArray(ref _abyssalAnchorPositionsNative);
            DisposeNativeArray(ref _abyssalNavNodeSnapshotNative);
            DisposeNativeArray(ref _abyssalNavConduitVectorsSnapshotNative);
            DisposeNativeArray(ref _abyssalNavConduitStrengthSnapshotNative);
            DisposeNativeArray(ref _abyssalNavNodeTypesSnapshotNative);
            DisposeNativeArray(ref _megaWreckStreamSnapshotNative);
            if (_abyssalNavGraphHashNative.IsCreated)
                _abyssalNavGraphHashNative.Dispose();
            if (_abyssalNavNodes.IsCreated)
                _abyssalNavNodes.Dispose();
            _abyssalAnchorCount = 0;
            _abyssalNavNodeCount = 0;
            _megaWreckStreamCount = 0;
            _abyssalNavGraphOrigin = Vector3.zero;
        }

        private void DisposeDensityQuerySnapshot()
        {
            DisposeNativeArray(ref _densityQueryChunksNative);
            DisposeNativeArray(ref _densityQueryGridNative);
            DisposeNativeArray(ref _threatAttractorGridNative);
            DisposeNativeArray(ref _densityQueryChunksScratchNative);
            DisposeNativeArray(ref _densityQueryGridScratchNative);
            DisposeNativeArray(ref _threatAttractorGridScratchNative);
            _densityQueryChunkCount = 0;
            _densityQueryChunkLookup.Clear();
        }

        private bool CacheTileMasks(TileRuntimeState state, TerrainData terrainData)
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

        private static void RefreshTerrainTextureCaches(TileRuntimeState state, TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return;

            int textureCount = Mathf.Max(0, terrainData.alphamapTextureCount);
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

            renderer.ClearSource();
        }

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _surfaceInstanceBuffer);
            ReleaseBuffer(ref _surfaceInstanceDataBuffer);
            ReleaseBuffer(ref _underwaterInstanceBuffer);
            ReleaseBuffer(ref _underwaterInstanceDataBuffer);
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

            DisposeNativeArray(ref _threatSamplingChunksNative, disposeHandle);
            DisposeNativeArray(ref _threatSamplingAttractorGridNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatGridCurrentNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatGridNextNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatGridCompressedCurrentNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatGridCompressedNextNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatVoxelCurrentNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatVoxelNextNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatEchoCurrentNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemThreatEchoNextNative, disposeHandle);
            DisposeNativeParallelMultiHashMap(ref _threatSamplingChunkHashFrontNative, disposeHandle);
            DisposeNativeParallelMultiHashMap(ref _threatSamplingChunkHashBackNative, default);

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
            DisposeNativeArray(ref _flowSamplingDensityGridNative, disposeHandle);
            DisposeNativeArray(ref _flowNavSupportGridNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemFlowFieldCurrentNative, disposeHandle);
            DisposeNativeArray(ref _ecosystemFlowFieldNextNative, disposeHandle);
            DisposeNativeArray(ref _swarmWakeImpulseNative, disposeHandle);
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
            DisposeNativeArray(ref _canopyHeightGridNative);
            _canopyGridInitialized = false;
            _canopyGridCenter = Vector3.zero;
        }

        private void DisposeThermalGridState()
        {
            JobHandle disposeHandle = _abyssalThermalGridScheduled ? _abyssalThermalGridHandle : default;
            DisposeNativeArray(ref _abyssalThermalGridNative, disposeHandle);
            DisposeNativeArray(ref _abyssalThermalGridNextNative, disposeHandle);
            DisposeNativeArray(ref _abyssalFlowVolumeCurrentNative, disposeHandle);
            DisposeNativeArray(ref _abyssalFlowVolumeNextNative, disposeHandle);
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
            DisposeNativeArray(ref _artificialStructureRecordsNative);
            JobHandle disposeHandle = default;
            if (_threatPropagationScheduled)
                disposeHandle = _threatPropagationHandle;
            if (_abyssalPathScheduled)
                disposeHandle = CombineOptionalHandles(disposeHandle, _abyssalPathHandle);

            DisposeNativeParallelMultiHashMap(ref _artificialStructureHashFrontNative, disposeHandle);
            DisposeNativeParallelMultiHashMap(ref _artificialStructureHashBackNative, default);

            _artificialStructureCount = 0;
            _artificialStructureHashSwapPending = false;
        }

        private void EnsureAbyssalNavGraphHashCapacity(int requiredCapacity)
        {
            int safeCapacity = Mathf.Max(1, requiredCapacity);
            if (!_abyssalNavGraphHashNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - spatial hash for immutable abyssal nav-node lookup - owner: HectonMapMagicVegetationBridge
                _abyssalNavGraphHashNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
                return;
            }

            if (_abyssalNavGraphHashNative.Capacity < safeCapacity)
                _abyssalNavGraphHashNative.Capacity = safeCapacity;
            else if (_abyssalNavGraphHashNative.Capacity > safeCapacity * 4)
                _abyssalNavGraphHashNative.Capacity = safeCapacity;
        }

        private void EnsureAbyssalPathBuffers(int nodeCount)
        {
            int requiredCount = Mathf.Max(1, nodeCount);
            EnsureNativeCapacity(ref _abyssalPathParentsNative, requiredCount);
            EnsureFloatNativeCapacity(ref _abyssalPathGScoreNative, requiredCount);
            EnsureFloatNativeCapacity(ref _abyssalPathFScoreNative, requiredCount);
            EnsureByteNativeCapacity(ref _abyssalPathClosedFlagsNative, requiredCount);
            EnsureNativeCapacity(ref _abyssalPathHeapNodesNative, requiredCount);
            EnsureNativeCapacity(ref _abyssalPathHeapPositionsNative, requiredCount);
            EnsureVector3Capacity(ref _abyssalPathSnapshot, requiredCount + 2);
            EnsureVector3NativeCapacity(ref _abyssalPathSnapshotNative, requiredCount + 2);

            if (!_abyssalPathRawResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[requiredCount+2] - raw abyssal A* path before Burst string-pulling - owner: HectonMapMagicVegetationBridge
                _abyssalPathRawResultNative = new NativeList<Vector3>(requiredCount + 2, Allocator.Persistent);
            }
            else if (_abyssalPathRawResultNative.Capacity < requiredCount + 2)
            {
                _abyssalPathRawResultNative.Capacity = requiredCount + 2;
            }
            else if (_abyssalPathRawResultNative.Capacity > (requiredCount + 2) * 4)
            {
                _abyssalPathRawResultNative.Capacity = requiredCount + 2;
            }

            if (!_abyssalPathResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[requiredCount+2] - latest smoothed abyssal path waypoint result - owner: HectonMapMagicVegetationBridge
                _abyssalPathResultNative = new NativeList<Vector3>(requiredCount + 2, Allocator.Persistent);
            }
            else if (_abyssalPathResultNative.Capacity < requiredCount + 2)
            {
                _abyssalPathResultNative.Capacity = requiredCount + 2;
            }
            else if (_abyssalPathResultNative.Capacity > (requiredCount + 2) * 4)
            {
                _abyssalPathResultNative.Capacity = requiredCount + 2;
            }
        }

        private void CompleteAbyssalPathJob(bool forceComplete)
        {
            if (!_abyssalPathScheduled)
                return;

            if (!forceComplete && !_abyssalPathHandle.IsCompleted)
                return;

            _abyssalPathHandle.Complete();
            _abyssalPathScheduled = false;
            _abyssalPathCount = _abyssalPathResultNative.IsCreated ? _abyssalPathResultNative.Length : 0;
            if (_abyssalPathCount <= 0)
                return;

            EnsureVector3Capacity(ref _abyssalPathSnapshot, _abyssalPathCount);
            EnsureVector3NativeCapacity(ref _abyssalPathSnapshotNative, _abyssalPathCount);
            for (int i = 0; i < _abyssalPathCount; i++)
            {
                Vector3 waypoint = _abyssalPathResultNative[i];
                _abyssalPathSnapshot[i] = waypoint;
                _abyssalPathSnapshotNative[i] = waypoint;
            }
        }

        private void DisposeAbyssalPathState()
        {
            JobHandle disposeHandle = _abyssalPathScheduled ? _abyssalPathHandle : default;
            DisposeNativeArray(ref _abyssalPathSnapshotNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathParentsNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathGScoreNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathFScoreNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathClosedFlagsNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathHeapNodesNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathHeapPositionsNative, disposeHandle);
            DisposeNativeArray(ref _predatorFearNodesSnapshotNative, disposeHandle);
            DisposeNativeList(ref _abyssalPathRawResultNative, disposeHandle);
            DisposeNativeList(ref _abyssalPathResultNative, disposeHandle);
            _abyssalPathHandle = default;
            _abyssalPathScheduled = false;
            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
            _hasLastAbyssalPathTarget = false;
        }

        private void DisposeHLODRegistryState()
        {
            JobHandle disposeHandle = _hlodCullScheduled ? _hlodCullHandle : default;
            DisposeNativeArray(ref _hlodRegistrySnapshotNative, disposeHandle);
            DisposeNativeArray(ref _visibleHlodSnapshotNative, disposeHandle);
            DisposeNativeArray(ref _hlodVisibleFlagsNative, disposeHandle);
            DisposeNativeArray(ref _hlodFrustumPlanesNative, disposeHandle);
            _hlodCullHandle = default;
            _hlodCullScheduled = false;
            _hlodRegistryCount = 0;
            _visibleHlodCount = 0;
        }

        private void DisposePoolDefragState()
        {
            JobHandle surfaceDisposeHandle = _poolDefragScheduled && _surfaceDefragMoveCount > 0
                ? _surfacePoolDefragHandle
                : default;
            JobHandle underwaterDisposeHandle = _poolDefragScheduled && _underwaterDefragMoveCount > 0
                ? _underwaterPoolDefragHandle
                : default;
            DisposeNativeArray(ref _surfaceDefragMovesNative, surfaceDisposeHandle);
            DisposeNativeArray(ref _underwaterDefragMovesNative, underwaterDisposeHandle);
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

        private void InvalidateAbyssalPathState()
        {
            if (_abyssalPathScheduled)
                CompleteAbyssalPathJob(forceComplete: true);

            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
            _hasLastAbyssalPathTarget = false;
            if (_abyssalPathRawResultNative.IsCreated)
                _abyssalPathRawResultNative.Clear();
            if (_abyssalPathResultNative.IsCreated)
                _abyssalPathResultNative.Clear();
        }

        private void EnsureAbyssalNavNodeListCapacity(int requiredCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            if (!_abyssalNavNodes.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[nextCapacity] - active abyssal safe-node snapshot list for pathfinding consumers - owner: HectonMapMagicVegetationBridge
                _abyssalNavNodes = new NativeList<Vector3>(nextCapacity, Allocator.Persistent);
                return;
            }

            if (_abyssalNavNodes.Capacity < nextCapacity)
                _abyssalNavNodes.Capacity = nextCapacity;
            else if (_abyssalNavNodes.Capacity > nextCapacity * 4)
                _abyssalNavNodes.Capacity = nextCapacity;
        }

        private void ShiftChunkAbyssalNavPayloads(Vector3 offset)
        {
            if (_chunkAbyssalNavPayloads.Count <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkAbyssalNavPayload>.Enumerator enumerator = _chunkAbyssalNavPayloads.GetEnumerator();
            while (enumerator.MoveNext())
                _evictionKeys.Add(enumerator.Current.Key);

            for (int keyIndex = 0; keyIndex < _evictionKeys.Count; keyIndex++)
            {
                ChunkKey key = _evictionKeys[keyIndex];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                for (int nodeIndex = 0; nodeIndex < payload.Count; nodeIndex++)
                    payload.Nodes[nodeIndex] += offset;

                _chunkAbyssalNavPayloads[key] = payload;
            }
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

        private void ShiftAbyssalNavSnapshots(Vector3 offset)
        {
            if (_abyssalNavNodeCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                _abyssalNavNodeSnapshot[i] += offset;
                if (_abyssalNavNodeSnapshotNative.IsCreated && i < _abyssalNavNodeSnapshotNative.Length)
                    _abyssalNavNodeSnapshotNative[i] = _abyssalNavNodeSnapshot[i];
            }

            if (_abyssalNavNodes.IsCreated)
            {
                for (int i = 0; i < _abyssalNavNodeCount; i++)
                    _abyssalNavNodes[i] = _abyssalNavNodeSnapshot[i];
            }
        }

        private void ShiftHLODRegistrySnapshots(Vector3 offset)
        {
            if (offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _hlodRegistrySnapshot[i];
                entry.Center += offset;
                _hlodRegistrySnapshot[i] = entry;
                if (_hlodRegistrySnapshotNative.IsCreated && i < _hlodRegistrySnapshotNative.Length)
                    _hlodRegistrySnapshotNative[i] = entry;
            }

            for (int i = 0; i < _visibleHlodCount; i++)
            {
                HLODData entry = _visibleHlodSnapshot[i];
                entry.Center += offset;
                _visibleHlodSnapshot[i] = entry;
                if (_visibleHlodSnapshotNative.IsCreated && i < _visibleHlodSnapshotNative.Length)
                    _visibleHlodSnapshotNative[i] = entry;
            }
        }

        private void ShiftAbyssalPathSnapshot(Vector3 offset)
        {
            if (_abyssalPathCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalPathCount; i++)
            {
                _abyssalPathSnapshot[i] += offset;
                if (_abyssalPathSnapshotNative.IsCreated && i < _abyssalPathSnapshotNative.Length)
                    _abyssalPathSnapshotNative[i] = _abyssalPathSnapshot[i];
            }

            if (_abyssalPathResultNative.IsCreated)
            {
                for (int i = 0; i < _abyssalPathCount && i < _abyssalPathResultNative.Length; i++)
                    _abyssalPathResultNative[i] = _abyssalPathSnapshot[i];
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
                if (_megaWreckStreamSnapshotNative.IsCreated && i < _megaWreckStreamSnapshotNative.Length)
                    _megaWreckStreamSnapshotNative[i] = section;
            }
        }

        private void ShiftThermalGridRing(Vector3 offset)
        {
            if (_abyssalThermalGridResolutionXZ <= 0 || _abyssalThermalGridResolutionY <= 0)
                return;

            int shiftX = Mathf.RoundToInt(offset.x / Mathf.Max(1f, thermalGridHorizontalCellSize));
            int shiftY = Mathf.RoundToInt(-offset.y / Mathf.Max(1f, thermalGridVerticalCellSize));
            int shiftZ = Mathf.RoundToInt(offset.z / Mathf.Max(1f, thermalGridHorizontalCellSize));
            _abyssalThermalGridRingOffsetX = PositiveModulo(_abyssalThermalGridRingOffsetX + shiftX, _abyssalThermalGridResolutionXZ);
            _abyssalThermalGridRingOffsetY = PositiveModulo(_abyssalThermalGridRingOffsetY + shiftY, _abyssalThermalGridResolutionY);
            _abyssalThermalGridRingOffsetZ = PositiveModulo(_abyssalThermalGridRingOffsetZ + shiftZ, _abyssalThermalGridResolutionXZ);
        }

        private void BuildFlowFieldNavSupportGrid(Vector3 gridCenter)
        {
            if (!_flowNavSupportGridNative.IsCreated || _ecosystemThreatGridResolution <= 0 || _abyssalNavNodeCount <= 0)
                return;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int stencilRadius = Mathf.Max(0, flowFieldNavStencilRadiusCells);
            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                Vector3 node = _abyssalNavNodeSnapshot[i];
                int centerX = Mathf.RoundToInt((node.x - gridCenter.x) / threatGridCellSize) + halfExtent;
                int centerZ = Mathf.RoundToInt((node.z - gridCenter.z) / threatGridCellSize) + halfExtent;
                if (centerX < 0 || centerZ < 0 || centerX >= _ecosystemThreatGridResolution || centerZ >= _ecosystemThreatGridResolution)
                    continue;

                for (int offsetZ = -stencilRadius; offsetZ <= stencilRadius; offsetZ++)
                {
                    int cellZ = centerZ + offsetZ;
                    if (cellZ < 0 || cellZ >= _ecosystemThreatGridResolution)
                        continue;

                    for (int offsetX = -stencilRadius; offsetX <= stencilRadius; offsetX++)
                    {
                        int cellX = centerX + offsetX;
                        if (cellX < 0 || cellX >= _ecosystemThreatGridResolution)
                            continue;

                        float distance = Mathf.Sqrt((offsetX * offsetX) + (offsetZ * offsetZ));
                        float support01 = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, stencilRadius + 0.25f));
                        int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                        float clampedSupport = Mathf.Clamp01(support01);
                        if (_flowNavSupportGridNative[index] < clampedSupport)
                            _flowNavSupportGridNative[index] = clampedSupport;
                    }
                }
            }
        }

        private bool MarkChunkCorrupted(ChunkKey key)
        {
            if (_corruptedChunkKeys.Contains(key))
                return false;

            TrimCorruptionStateToBudget();
            if (_corruptedChunkKeys.Count >= maxTrackedCorruptedChunks)
                return false;

            _corruptedChunkKeys.Add(key);
            _corruptedChunkOrder.Add(key);
            return true;
        }

        private bool IsChunkCorrupted(ChunkKey key)
        {
            return _corruptedChunkKeys.Contains(key);
        }

        private void ClearCorruptionStateForTile(int tileX, int tileZ)
        {
            for (int i = _corruptedChunkOrder.Count - 1; i >= 0; i--)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (key.TileX != tileX || key.TileZ != tileZ)
                    continue;

                _corruptedChunkKeys.Remove(key);
                _corruptedChunkOrder.RemoveAt(i);
            }
        }

        private void TrimCorruptionStateToBudget()
        {
            if (_corruptedChunkKeys.Count < maxTrackedCorruptedChunks)
                return;

            for (int i = 0; i < _corruptedChunkOrder.Count && _corruptedChunkKeys.Count >= maxTrackedCorruptedChunks; i++)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (_chunkPayloads.ContainsKey(key) ||
                    _chunkBuildJobs.ContainsKey(key) ||
                    ContainsDesiredChunk(key))
                {
                    continue;
                }

                _corruptedChunkKeys.Remove(key);
                _corruptedChunkOrder.RemoveAt(i);
                i--;
            }
        }

        private bool InvalidateChunkForCorruption(ChunkKey key)
        {
            bool changed = false;
            if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
            {
                ReleaseChunkPayloadStorage(payload);
                _chunkPayloads.Remove(key);
                RemoveChunkAbyssalNavPayload(key);
                RemoveChunkMegaWreckPayload(key);
                changed = true;
            }

            if (_chunkBuildJobs.ContainsKey(key))
            {
                CancelChunkBuildJob(key);
                changed = true;
            }

            return changed;
        }

        private bool InvalidateChunksForNewPermanentEchoes()
        {
            if (!_ecosystemThreatEchoCurrentNative.IsCreated ||
                !_ecosystemThreatEchoNextNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            bool changed = false;
            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                ChunkPayload payload = payloadEnumerator.Current.Value;
                if (!HasNewPermanentEchoInBounds(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ))
                    continue;

                _evictionKeys.Add(payloadEnumerator.Current.Key);
            }

            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ChunkKey key = _evictionKeys[i];
                changed |= InvalidateChunkForCorruption(key);
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator jobEnumerator = _chunkBuildJobs.GetEnumerator();
            while (jobEnumerator.MoveNext())
            {
                ChunkBuildJobState jobState = jobEnumerator.Current.Value;
                if (jobState == null ||
                    !HasNewPermanentEchoInBounds(jobState.PayloadHeader.MinX, jobState.PayloadHeader.MaxX, jobState.PayloadHeader.MinZ, jobState.PayloadHeader.MaxZ))
                {
                    continue;
                }

                _jobScratchKeys.Add(jobEnumerator.Current.Key);
            }

            for (int i = 0; i < _jobScratchKeys.Count; i++)
            {
                ChunkKey key = _jobScratchKeys[i];
                CancelChunkBuildJob(key);
                changed = true;
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            if (changed)
                _activeSetDirty = true;

            return changed;
        }

        private bool HasNewPermanentEchoInBounds(float minX, float maxX, float minZ, float maxZ)
        {
            if (!_ecosystemThreatEchoCurrentNative.IsCreated ||
                !_ecosystemThreatEchoNextNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f)
            {
                return false;
            }

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                    if (_ecosystemThreatEchoCurrentNative[index] == 0 || _ecosystemThreatEchoNextNative[index] != 0)
                        continue;

                    return true;
                }
            }

            return false;
        }

        private static float SampleThreatGridAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<float> threatGrid)
        {
            if (!threatGrid.IsCreated || resolution <= 0 || cellSize <= 0f)
                return 0f;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0f;

            float normalizedX = Mathf.Clamp(localX / cellSize, 0f, resolution - 1);
            float normalizedZ = Mathf.Clamp(localZ / cellSize, 0f, resolution - 1);
            int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, resolution - 1);
            int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, resolution - 1);
            int nextCellX = Mathf.Min(cellX + 1, resolution - 1);
            int nextCellZ = Mathf.Min(cellZ + 1, resolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float sample00 = threatGrid[(cellZ * resolution) + cellX];
            float sample10 = threatGrid[(cellZ * resolution) + nextCellX];
            float sample01 = threatGrid[(nextCellZ * resolution) + cellX];
            float sample11 = threatGrid[(nextCellZ * resolution) + nextCellX];
            float sampleX0 = Mathf.Lerp(sample00, sample10, fracX);
            float sampleX1 = Mathf.Lerp(sample01, sample11, fracX);
            return Mathf.Lerp(sampleX0, sampleX1, fracZ);
        }

        private static int ComputeThreatGridCellIndex(float3 position, float3 gridCenter, float cellSize, int resolution)
        {
            if (resolution <= 0 || cellSize <= 0f)
                return -1;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return -1;

            int cellX = math.clamp((int)math.floor(localX / cellSize), 0, resolution - 1);
            int cellZ = math.clamp((int)math.floor(localZ / cellSize), 0, resolution - 1);
            return (cellZ * resolution) + cellX;
        }

        private float SampleCanopyHeightAtPosition(float worldX, float worldZ)
        {
            if (!_canopyHeightGridNative.IsCreated || _canopyGridResolution <= 0 || canopyGridCellSize <= 0f)
                return float.NegativeInfinity;

            float halfExtent = (_canopyGridResolution - 1) * 0.5f * canopyGridCellSize;
            float localX = worldX - (_canopyGridCenter.x - halfExtent);
            float localZ = worldZ - (_canopyGridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return float.NegativeInfinity;

            int cellX = Mathf.Clamp(Mathf.RoundToInt(localX / canopyGridCellSize), 0, _canopyGridResolution - 1);
            int cellZ = Mathf.Clamp(Mathf.RoundToInt(localZ / canopyGridCellSize), 0, _canopyGridResolution - 1);
            return _canopyHeightGridNative[(cellZ * _canopyGridResolution) + cellX];
        }

        private static byte SampleThreatEchoFlagAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<byte> echoFlags)
        {
            if (!echoFlags.IsCreated || resolution <= 0 || cellSize <= 0f)
                return 0;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0;

            int cellX = Mathf.Clamp(Mathf.RoundToInt(localX / cellSize), 0, resolution - 1);
            int cellZ = Mathf.Clamp(Mathf.RoundToInt(localZ / cellSize), 0, resolution - 1);
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
            if (!echoFlags.IsCreated || resolution <= 0 || cellSize <= 0f)
                return 0;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = worldX - (gridCenter.x - halfExtent);
            float localZ = worldZ - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return 0;

            int cellX = math.clamp((int)math.round(localX / cellSize), 0, resolution - 1);
            int cellZ = math.clamp((int)math.round(localZ / cellSize), 0, resolution - 1);
            return echoFlags[(cellZ * resolution) + cellX];
        }

        private static float2 SampleFlowFieldAtPosition(
            Vector3 position,
            Vector3 gridCenter,
            float cellSize,
            int resolution,
            NativeArray<float2> flowField)
        {
            if (!flowField.IsCreated || resolution <= 0 || cellSize <= 0f)
                return float2.zero;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return float2.zero;

            float normalizedX = Mathf.Clamp(localX / cellSize, 0f, resolution - 1);
            float normalizedZ = Mathf.Clamp(localZ / cellSize, 0f, resolution - 1);
            int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, resolution - 1);
            int cellZ = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, resolution - 1);
            int nextCellX = Mathf.Min(cellX + 1, resolution - 1);
            int nextCellZ = Mathf.Min(cellZ + 1, resolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float2 sample00 = flowField[(cellZ * resolution) + cellX];
            float2 sample10 = flowField[(cellZ * resolution) + nextCellX];
            float2 sample01 = flowField[(nextCellZ * resolution) + cellX];
            float2 sample11 = flowField[(nextCellZ * resolution) + nextCellX];
            float2 sampleX0 = math.lerp(sample00, sample10, fracX);
            float2 sampleX1 = math.lerp(sample01, sample11, fracX);
            return math.normalizesafe(math.lerp(sampleX0, sampleX1, fracZ), float2.zero);
        }

        private float SampleThermalGridAtPosition(Vector3 position)
        {
            if (!_abyssalThermalGridNative.IsCreated ||
                _abyssalThermalGridResolutionXZ <= 0 ||
                _abyssalThermalGridResolutionY <= 0 ||
                thermalGridHorizontalCellSize <= 0f ||
                thermalGridVerticalCellSize <= 0f)
            {
                return thermalSurfaceTemperatureCelsius;
            }

            float halfExtent = (_abyssalThermalGridResolutionXZ - 1) * 0.5f * thermalGridHorizontalCellSize;
            float minX = _abyssalThermalGridCenter.x - halfExtent;
            float minZ = _abyssalThermalGridCenter.z - halfExtent;
            float maxY = waterLevel;
            float minY = waterLevel - thermalGridDepthMeters;
            if (position.x < minX || position.z < minZ || position.x > minX + (halfExtent * 2f) || position.z > minZ + (halfExtent * 2f))
                return thermalSurfaceTemperatureCelsius;

            float clampedY = Mathf.Clamp(position.y, minY, maxY);
            float normalizedX = Mathf.Clamp((position.x - minX) / thermalGridHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedZ = Mathf.Clamp((position.z - minZ) / thermalGridHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedY = Mathf.Clamp((maxY - clampedY) / thermalGridVerticalCellSize, 0f, _abyssalThermalGridResolutionY - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, _abyssalThermalGridResolutionXZ - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, _abyssalThermalGridResolutionXZ - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(normalizedY), 0, _abyssalThermalGridResolutionY - 1);
            int x1 = Mathf.Min(x0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int z1 = Mathf.Min(z0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int y1 = Mathf.Min(y0 + 1, _abyssalThermalGridResolutionY - 1);
            float fracX = normalizedX - x0;
            float fracZ = normalizedZ - z0;
            float fracY = normalizedY - y0;

            float sample000 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x0, y0, z0)];
            float sample100 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x1, y0, z0)];
            float sample010 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x0, y0, z1)];
            float sample110 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x1, y0, z1)];
            float sample001 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x0, y1, z0)];
            float sample101 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x1, y1, z0)];
            float sample011 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x0, y1, z1)];
            float sample111 = _abyssalThermalGridNative[GetThermalGridPhysicalIndex(x1, y1, z1)];
            float sampleX00 = Mathf.Lerp(sample000, sample100, fracX);
            float sampleX10 = Mathf.Lerp(sample010, sample110, fracX);
            float sampleX01 = Mathf.Lerp(sample001, sample101, fracX);
            float sampleX11 = Mathf.Lerp(sample011, sample111, fracX);
            float sampleZ0 = Mathf.Lerp(sampleX00, sampleX10, fracZ);
            float sampleZ1 = Mathf.Lerp(sampleX01, sampleX11, fracZ);
            return Mathf.Lerp(sampleZ0, sampleZ1, fracY);
        }

        private int GetThermalGridPhysicalIndex(int x, int y, int z)
        {
            int wrappedX = PositiveModulo(x + _abyssalThermalGridRingOffsetX, _abyssalThermalGridResolutionXZ);
            int wrappedY = PositiveModulo(y + _abyssalThermalGridRingOffsetY, _abyssalThermalGridResolutionY);
            int wrappedZ = PositiveModulo(z + _abyssalThermalGridRingOffsetZ, _abyssalThermalGridResolutionXZ);
            return (wrappedY * _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionXZ) +
                   (wrappedZ * _abyssalThermalGridResolutionXZ) +
                   wrappedX;
        }

        private int FindNearestAbyssalNavNodeIndex(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 || !_abyssalNavNodeSnapshotNative.IsCreated)
                return -1;

            if (TryFindNearestAbyssalNavNodeIndexFromHash(position, out int hashedIndex))
                return hashedIndex;

            int bestIndex = -1;
            float bestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                Vector3 candidate = _abyssalNavNodeSnapshot[i];
                float distanceSq = (candidate - position).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
            }

            return bestIndex;
        }

        private bool TryFindNearestAbyssalNavNodeIndexFromHash(Vector3 position, out int bestIndex)
        {
            bestIndex = -1;
            if (!_abyssalNavGraphHashNative.IsCreated ||
                _abyssalNavNodeCount <= 0 ||
                abyssalNavGraphCellSize <= 0f)
            {
                return false;
            }

            int baseCellX = Mathf.FloorToInt((position.x - _abyssalNavGraphOrigin.x) / abyssalNavGraphCellSize);
            int baseCellY = Mathf.FloorToInt((position.y - _abyssalNavGraphOrigin.y) / abyssalNavGraphCellSize);
            int baseCellZ = Mathf.FloorToInt((position.z - _abyssalNavGraphOrigin.z) / abyssalNavGraphCellSize);
            float bestDistanceSq = float.PositiveInfinity;
            int searchRadiusCells = Mathf.Clamp(Mathf.CeilToInt(abyssalPathNeighborRadius / Mathf.Max(1f, abyssalNavGraphCellSize)), 1, 3);
            for (int radius = 0; radius <= searchRadiusCells; radius++)
            {
                bool foundAny = false;
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
                    {
                        for (int offsetX = -radius; offsetX <= radius; offsetX++)
                        {
                            int key = HashSpatialCell(baseCellX + offsetX, baseCellY + offsetY, baseCellZ + offsetZ);
                            if (!_abyssalNavGraphHashNative.TryGetFirstValue(key, out int nodeIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                                continue;

                            do
                            {
                                if ((uint)nodeIndex >= _abyssalNavNodeCount)
                                    continue;

                                foundAny = true;
                                Vector3 candidate = _abyssalNavNodeSnapshot[nodeIndex];
                                float distanceSq = (candidate - position).sqrMagnitude;
                                if (distanceSq >= bestDistanceSq)
                                    continue;

                                bestDistanceSq = distanceSq;
                                bestIndex = nodeIndex;
                            }
                            while (_abyssalNavGraphHashNative.TryGetNextValue(out nodeIndex, ref iterator));
                        }
                    }
                }

                if (foundAny && bestIndex >= 0)
                    return true;
            }

            return false;
        }

        private static int ComputeAbyssalNavGraphHashKey(Vector3 position, Vector3 origin, float cellSize)
        {
            float safeCellSize = Mathf.Max(0.01f, cellSize);
            int cellX = Mathf.FloorToInt((position.x - origin.x) / safeCellSize);
            int cellY = Mathf.FloorToInt((position.y - origin.y) / safeCellSize);
            int cellZ = Mathf.FloorToInt((position.z - origin.z) / safeCellSize);
            return HashSpatialCell(cellX, cellY, cellZ);
        }

        private static int HashSpatialCell(int x, int y, int z)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)y) * 16777619u;
                hash = (hash ^ (uint)z) * 16777619u;
                return (int)hash;
            }
        }

        private static bool DoesChunkBoundsIntersectCircle(float minX, float maxX, float minZ, float maxZ, float centerX, float centerZ, float radiusSq)
        {
            float clampedX = Mathf.Clamp(centerX, minX, maxX);
            float clampedZ = Mathf.Clamp(centerZ, minZ, maxZ);
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
            return encoded * (1f / 255f);
        }

        private static byte EncodeThreatByte(float threat)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(threat) * 255f), 0, 255);
        }

        private static void ClearByteGrid(NativeArray<byte> destination, int count)
        {
            if (!destination.IsCreated || count <= 0)
                return;

            int end = Mathf.Min(count, destination.Length);
            for (int i = 0; i < end; i++)
                destination[i] = 0;
        }

        private static void ClearFloatGrid(NativeArray<float> destination, int count)
        {
            if (!destination.IsCreated || count <= 0)
                return;

            int end = Mathf.Min(count, destination.Length);
            for (int i = 0; i < end; i++)
                destination[i] = 0f;
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

        private static void EnsureHLODDataCapacity(ref HLODData[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: HLODData[nextCapacity] - HLOD registry snapshot growth - owner: HectonMapMagicVegetationBridge
            HLODData[] expanded = new HLODData[nextCapacity];
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

        private static void EnsureFloatNativeCapacity(ref NativeArray<float> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat2NativeCapacity(ref NativeArray<float2> cache, int requiredCount)
        {
            EnsureNativeCapacity(ref cache, requiredCount);
        }

        private static void EnsureFloat4NativeCapacity(ref NativeArray<float4> cache, int requiredCount)
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
            // COLD ALLOC: NativeArray<T>[nextCapacity] - native snapshot/cache growth for streamed vegetation data - owner: HectonMapMagicVegetationBridge
            NativeArray<T> expanded = new NativeArray<T>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cache.IsCreated)
            {
                if (cache.Length > 0)
                    NativeArray<T>.Copy(cache, expanded, cache.Length);

                cache.Dispose();
            }

            cache = expanded;
        }

        private static void EnsureInactiveNativeCapacity<T>(ref NativeArray<T> cache, int requiredCount)
            where T : struct
        {
            if (requiredCount <= 0)
                return;

            if (cache.IsCreated && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: NativeArray<T>[nextCapacity] - inactive back-buffer growth for streamed vegetation data - owner: HectonMapMagicVegetationBridge
            NativeArray<T> expanded = new NativeArray<T>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cache.IsCreated)
                cache.Dispose();

            cache = expanded;
        }

        private static void CopyNativeToManaged<T>(NativeArray<T> source, int sourceIndex, T[] destination, int destinationIndex, int copyCount)
            where T : struct
        {
            for (int i = 0; i < copyCount; i++)
                destination[destinationIndex + i] = source[sourceIndex + i];
        }

        private static MegaWreckStreamSection[] AllocateMegaWreckSectionPayloadArray(int count)
        {
            // COLD ALLOC: MegaWreckStreamSection[count] - per-chunk mega-wreck section cache finalized from streamed payloads - owner: HectonMapMagicVegetationBridge
            return new MegaWreckStreamSection[count];
        }

        private static void EnsureMegaWreckSectionCapacity(ref MegaWreckStreamSection[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            // COLD ALLOC: MegaWreckStreamSection[nextCapacity] - active mega-wreck stream snapshot growth - owner: HectonMapMagicVegetationBridge
            MegaWreckStreamSection[] expanded = new MegaWreckStreamSection[nextCapacity];
            if (cache != null && cache.Length > 0)
                Array.Copy(cache, expanded, cache.Length);

            cache = expanded;
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
            if (length <= 0)
                return 0;

            int wrapped = value % length;
            return wrapped < 0 ? wrapped + length : wrapped;
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
