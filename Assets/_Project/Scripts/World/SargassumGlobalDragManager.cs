using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Builds a coarse world-space density field for floating sargassum and serves zero-allocation drag queries.
    /// Also bakes a density texture used by Crest damping and GPU micro-fauna.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-104)]
    public sealed class SargassumGlobalDragManager : MonoBehaviour, ITickable, ISlowTickable, ISargassumMassiveDisplacementReceiver, IOriginShiftListener, ISaveable
    {
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_SargassumGlobalDriftOffset");
        private static readonly int _SinkTextureId = Shader.PropertyToID("_SargassumBuoyancySinkRT");
        private static readonly int _SinkWorldRectId = Shader.PropertyToID("_SargassumBuoyancySinkWorldRect");
        private static readonly int _MaxSinkDepthId = Shader.PropertyToID("_SargassumBuoyancySinkDepth");
        private static readonly int _ScavengerMatricesId = Shader.PropertyToID("_HectonScavengerMatrices");

        private const uint NestingHashSeed = 0x7F4A7C15u;
        private const int InitialCellCapacity = 4096;
        private const int DefaultDensityTextureResolution = 128;
        private const int MaxDisruptionZoneCapacity = 16;
        private const int MaxEditorValidateDepth = 2;
        private const float MinDensityThreshold = 0.025f;
        private const int ExternalSiteChunkSizeMeters = 64;
        private const float ExternalSiteQuantizationMeters = 0.5f;
        private const byte CollapseChunkBurstSpawnedFlag = 1 << 0;

        internal struct SargassumFieldSample
        {
            public bool HasInfluence;
            public float SpeedMultiplier;
            public float DragMultiplier;
            public float Density01;
            public Vector3 AnchorWS;
            public float Entanglement01;
            public float Window01;
            public float Occlusion01;
        }

        public struct EntanglementStrainSignal
        {
            public int SourceInstanceId;
            public Vector3 PositionWS;
            public Vector3 AnchorWS;
            public float Tension01;
            public float EscapeIntent01;
            public float Shake01;
        }

        public struct MassiveDisplacementSignal
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float Duration;
            public float ExtremePanicRadiusWS;
        }

        private struct CellData
        {
            public float Density;
            public float MinY;
            public float MaxY;
        }

        private struct NestedAttachmentState
        {
            public Vector3 SampleSpaceAnchorWS;
            public Vector3 RenderOffsetWS;
            public Vector3 ReleaseVelocityWS;
            public Quaternion Rotation;
            public float UniformScale;
            public float ReleaseLifetime;
            public float ReleaseAge;
            public int PrototypeIndex;
            public bool Released;
        }

        private enum DisruptionZoneMode : byte
        {
            CutCollapse = 1,
            MassiveDisplacement = 2
        }

        private struct DisruptionZoneState
        {
            public Vector3 SampleSpaceCenterWS;
            public float RadiusWS;
            public float Strength01;
            public float SinkDepthWS;
            public float RampDuration;
            public float HoldDuration;
            public float FadeDuration;
            public float Age;
            public byte Mode;
            public byte Flags;
        }

        private struct DisruptionSample
        {
            public float Suppression01;
            public float SinkDepthWS;
        }

        private struct ScavengerHostState
        {
            public SargassumCollapseChunk Chunk;
            public Vector3 AnchorWS;
            public float SettledTime;
            public float Consumed01;
            public uint Seed;
        }

        private struct ExternalScavengerSiteState
        {
            public Vector3 AnchorWS;
            public float RadiusWS;
            public float RemainingTime;
            public uint Seed;
        }

        private struct DensitySourceData
        {
            public float3 OriginWS;
            public float Scale;
        }

        private struct DensityContributionData
        {
            public float Density;
            public float MinY;
            public float MaxY;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct BuildDensityContributionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<DensitySourceData> Sources;
            public NativeParallelMultiHashMap<long, DensityContributionData>.ParallelWriter Contributions;
            public float CellSize;
            public float InverseCellSize;
            public float BaseInfluenceRadius;
            public float BaseVerticalHalfExtent;
            public float DistancePower;

            public void Execute(int index)
            {
                DensitySourceData source = Sources[index];
                float influenceRadius = math.max(BaseInfluenceRadius * source.Scale, CellSize * 0.35f);
                float verticalHalfExtent = math.max(BaseVerticalHalfExtent * source.Scale, 0.5f);
                float minY = source.OriginWS.y - verticalHalfExtent;
                float maxY = source.OriginWS.y + verticalHalfExtent;

                int minCellX = (int)math.floor((source.OriginWS.x - influenceRadius) * InverseCellSize);
                int maxCellX = (int)math.floor((source.OriginWS.x + influenceRadius) * InverseCellSize);
                int minCellZ = (int)math.floor((source.OriginWS.z - influenceRadius) * InverseCellSize);
                int maxCellZ = (int)math.floor((source.OriginWS.z + influenceRadius) * InverseCellSize);
                float safeDistancePower = math.max(1f, DistancePower);

                for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                {
                    for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    {
                        float cellCenterX = (cellX + 0.5f) * CellSize;
                        float cellCenterZ = (cellZ + 0.5f) * CellSize;
                        float2 planarDelta = new float2(source.OriginWS.x - cellCenterX, source.OriginWS.z - cellCenterZ);
                        float planarDistance = math.length(planarDelta);
                        if (planarDistance > influenceRadius)
                            continue;

                        float normalized = 1f - planarDistance / math.max(influenceRadius, 0.001f);
                        float density = math.pow(math.saturate(normalized), safeDistancePower);
                        if (density <= 0.0001f)
                            continue;

                        long key = ((long)cellX << 32) ^ (uint)cellZ;
                        Contributions.Add(key, new DensityContributionData
                        {
                            Density = density,
                            MinY = minY,
                            MaxY = maxY
                        });
                    }
                }
            }
        }

        [Serializable]
        private struct NestedAttachmentPrototype
        {
            [Tooltip("Instanced mesh rendered for this nesting archetype.")]
            public Mesh Mesh;
            [Tooltip("Shared asset material used by the instanced render path.")]
            public Material Material;
            [Tooltip("Relative selection weight for deterministic prototype picking.")]
            public int Weight;
            [Tooltip("Uniform scale range applied to this nesting archetype.")]
            public Vector2 UniformScaleRange;
            [Tooltip("Vertical offset range applied above the sampled canopy anchor.")]
            public Vector2 VerticalOffsetRange;
            [Tooltip("Minimum local density required before this archetype may spawn.")]
            public float DensityThreshold;
            [Tooltip("Maximum canopy window openness allowed for this archetype.")]
            public float WindowThreshold;
            [Tooltip("Shadow mode for this archetype.")]
            public ShadowCastingMode ShadowCastingMode;
        }

        private static SargassumGlobalDragManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnEntanglementStrain = null;
            OnMassiveDisplacement = null;
        }

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Primary runtime source for active floating sargassum matrices coming from MapMagic residency.")]
        private HectonMapMagicVegetationBridge mapMagicVegetationBridge;

        [Header("── Spatial Hash ──────────────────")]
        [SerializeField, Min(2f)]
        [Tooltip("XZ cell size used by the coarse world-space density field.")]
        private float cellSize = 6f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Base planar influence radius contributed by one sargassum instance at unit scale.")]
        private float baseInfluenceRadius = 3.2f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Vertical half-extent used to detect that the player body intersects a floating sargassum layer.")]
        private float baseVerticalHalfExtent = 2.15f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Density normalization applied after sampling nearby cells.")]
        private float densityNormalization = 0.42f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("How aggressively cell contributions decay with planar distance.")]
        private float distancePower = 1.4f;

        [Header("── Canopy Lighting ──────────────────")]
        [SerializeField]
        [Tooltip("Fallback patch threshold used only if the MapMagic bridge is unavailable. Runtime should stay synced from ActiveFloatingLabyrinthConfig.")]
        private float fallbackPatchThreshold = HectonVegetationConstants.FloatingPatchThreshold;

        [SerializeField]
        [Tooltip("Fallback world-flow direction used only if the MapMagic bridge is unavailable.")]
        private Vector2 canopyFlowDirection = HectonVegetationConstants.FloatingFlowDirection;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Fallback cross-flow anisotropy used only if the MapMagic bridge is unavailable.")]
        private float canopyFlowAnisotropy = HectonVegetationConstants.FloatingFlowAnisotropy;

        [SerializeField, Min(4f)]
        [Tooltip("Fallback primary cell size used only if the MapMagic bridge is unavailable.")]
        private float canopyPrimaryCellSize = HectonVegetationConstants.FloatingPrimaryCellSize;

        [SerializeField, Min(4f)]
        [Tooltip("Fallback secondary cell size used only if the MapMagic bridge is unavailable.")]
        private float canopySecondaryCellSize = HectonVegetationConstants.FloatingSecondaryCellSize;

        [SerializeField, Min(0.25f)]
        [Tooltip("Fallback wall width used only if the MapMagic bridge is unavailable.")]
        private float canopyWallWidth = HectonVegetationConstants.FloatingWallWidth;

        [SerializeField, Min(0f)]
        [Tooltip("Fallback warp amplitude used only if the MapMagic bridge is unavailable.")]
        private float canopyWarpMeters = HectonVegetationConstants.FloatingWarpMeters;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Fallback patch noise scale used only if the MapMagic bridge is unavailable.")]
        private float canopyWarpNoiseScale = HectonVegetationConstants.FloatingPatchNoiseScale;

        [Header("── Density Texture ──────────────────")]
        [SerializeField, Range(64, 256)]
        [Tooltip("Resolution of the baked density texture consumed by Crest damping and GPU micro-fauna.")]
        private int densityTextureResolution = DefaultDensityTextureResolution;

        [SerializeField, Min(0f)]
        [Tooltip("Extra world-space padding added around the streamed field bounds when baking the density texture.")]
        private float densityTextureBoundsPadding = 24f;

        [Header("── Global Drift ──────────────────")]
        [SerializeField]
        [Tooltip("World-space drift offset applied by ocean systems. Physics samples subtract it to stay aligned with the visual shader drift.")]
        private Vector3 _globalDriftOffset;

        [Header("── Nesting Attachments ──────────────────")]
        [SerializeField]
        [Tooltip("Instanced archetypes used for rare debris or crates tangled into dense canopy walls. Future ecosystems can swap these without changing the renderer path.")]
        private NestedAttachmentPrototype[] nestingPrototypes;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("Chance in percent for one dense canopy node to trap a debris nest during field rebuild. The authored default is 1%.")]
        private float denseNodeNestChancePercent = 1f;

        [SerializeField, Range(4, 128)]
        [Tooltip("Hard cap for active instanced nesting attachments in the current field.")]
        private int maxNestedAttachmentCount = 48;

        [SerializeField, Range(4, 24)]
        [Tooltip("Maximum deterministic rejection attempts per nested attachment spawn.")]
        private int maxNestedAttachmentAttempts = 12;

        [SerializeField, Range(0.1f, 2.5f)]
        [Tooltip("Sampling radius used when validating dense nesting anchors.")]
        private float nestingSampleRadius = 0.95f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum dense-node value required before a debris nest candidate is allowed to trap objects.")]
        private float nestingNodeDensityThreshold = 0.72f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum canopy window openness allowed for debris nests. Lower values keep trapped objects inside the thickest labyrinth knots.")]
        private float nestingNodeWindowThreshold = 0.14f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Additional cut-query radius used when deciding whether a trapped debris nest has been severed free.")]
        private float nestingCutReleaseRadius = 1.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum recent-cut weight required before a trapped debris nest releases and starts falling out of the canopy.")]
        private float nestingCutReleaseThreshold = 0.22f;

        [SerializeField, Range(0.2f, 8f)]
        [Tooltip("Seconds a released debris nest keeps rendering while falling away from the severed canopy knot.")]
        private float nestingReleaseLifetime = 2.8f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Horizontal burst speed applied when a debris nest is severed out of the canopy.")]
        private float nestingReleaseHorizontalSpeed = 1.15f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Initial downward speed applied when a debris nest is severed out of the canopy.")]
        private float nestingReleaseDownwardSpeed = 0.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional downward acceleration applied while a severed debris nest is falling out of the canopy.")]
        private float nestingReleaseGravity = 0.72f;

        [Header("── Destruction & Buoyancy Collapse ──────────────────")]
        [SerializeField, Range(1, MaxDisruptionZoneCapacity)]
        [Tooltip("Hard cap for persistent collapse or leviathan disruption zones applied over the current canopy field.")]
        private int maxDisruptionZoneCount = 8;

        [SerializeField, Range(2f, 18f)]
        [Tooltip("Local query radius used when measuring whether clustered cuts have punctured too much canopy in one knot.")]
        private float buoyancyCollapseQueryRadius = 7.5f;

        [SerializeField, Min(1f)]
        [Tooltip("Weighted recent-cut area required before a dense canopy knot loses buoyancy and begins to sink.")]
        private float buoyancyCollapseAreaThreshold = 42f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Strongest overlapping cut required before a clustered puncture is allowed to trigger a buoyancy collapse zone.")]
        private float buoyancyCollapseCutStrengthThreshold = 0.38f;

        [SerializeField, Range(2f, 18f)]
        [Tooltip("World-space radius of a collapse zone registered after too many cuts puncture one dense cluster.")]
        private float buoyancyCollapseRadius = 6.5f;

        [SerializeField, Range(10f, 20f)]
        [Tooltip("Seconds required for a collapsed canopy knot to reach its full sink offset.")]
        private float buoyancyCollapseSinkDuration = 14f;

        [SerializeField, Range(1f, 24f)]
        [Tooltip("Maximum downward sink depth applied to a collapsed canopy knot before it is treated as locally drowned.")]
        private float buoyancyCollapseSinkDepth = 9.5f;

        [SerializeField]
        [Tooltip("Pooled rigidbody chunk spawned when a catastrophic cut cluster collapses a large canopy knot.")]
        private GameObject collapseChunkPrefab;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Multiplier applied to the base collapse-area threshold before a collapse escalates into pooled falling chunks.")]
        private float collapseChunkAreaThresholdMultiplier = 1.85f;

        [SerializeField, Range(1, 6)]
        [Tooltip("Maximum chunk count spawned by one catastrophic canopy collapse burst.")]
        private int collapseChunkSpawnCount = 3;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Planar spawn radius used when scattering pooled collapse chunks around the failing canopy knot.")]
        private float collapseChunkSpawnRadius = 2.6f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Initial lateral velocity applied to catastrophic collapse chunks.")]
        private float collapseChunkHorizontalSpeed = 1.9f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Initial downward velocity applied to catastrophic collapse chunks.")]
        private float collapseChunkDownwardSpeed = 1.25f;

        [SerializeField, Range(0.5f, 24f)]
        [Tooltip("Seconds catastrophic collapse chunks remain active before they are returned to the object pool.")]
        private float collapseChunkLifetime = 18f;

        [SerializeField, Range(0.25f, 3f)]
        [Tooltip("Minimum uniform scale applied to pooled collapse chunks.")]
        private float collapseChunkScaleMin = 0.85f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Maximum uniform scale applied to pooled collapse chunks.")]
        private float collapseChunkScaleMax = 1.65f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Impact speed required before a falling collapse chunk is allowed to fracture again on collision.")]
        private float collapseCascadeImpactThreshold = 3.8f;

        [SerializeField, Range(1, 3)]
        [Tooltip("Maximum recursive fragment depth allowed when collapse chunks keep tearing on impact.")]
        private int collapseCascadeMaxDepth = 2;

        [SerializeField, Range(1, 4)]
        [Tooltip("Maximum secondary chunk count spawned by one collapse-chunk impact.")]
        private int collapseCascadeSpawnCount = 2;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Uniform scale multiplier applied per cascade depth so secondary fragments stay smaller than the parent chunk.")]
        private float collapseCascadeScaleMultiplier = 0.62f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Velocity multiplier applied to secondary cascade fragments.")]
        private float collapseCascadeVelocityMultiplier = 0.9f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Strength written into the global cut mask when a leviathan or submarine punches through the canopy.")]
        private float massiveDisplacementCutStrength = 1f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Seconds required for a massive displacement tear to reach full local density suppression.")]
        private float massiveDisplacementRampDuration = 0.65f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Seconds used to fade a massive displacement tear out after its requested duration expires.")]
        private float massiveDisplacementFadeDuration = 2.4f;

        [SerializeField, Range(50f, 96f)]
        [Tooltip("Minimum flee radius broadcast to GPU boids when a leviathan or submarine rips through the canopy.")]
        private float massiveDisplacementExtremePanicRadius = HectonVegetationConstants.BoidMassiveDisplacementPanicRadius;

        [Header("── Procedural Scavengers ──────────────────")]
        [SerializeField]
        [Tooltip("Optional authored mesh rendered for bottom scavengers feeding on fallen collapse chunks.")]
        private Mesh scavengerMesh;

        [SerializeField]
        [Tooltip("Optional authored material used for scavenger rendering. If missing, a first-party fallback material is created from the scavenger shader.")]
        private Material scavengerMaterial;

        [SerializeField]
        [Tooltip("First-party fallback shader used to build the hidden scavenger material when the authored material is missing.")]
        private Shader scavengerFallbackShader;

        [SerializeField, Range(2, 24)]
        [Tooltip("Maximum number of settled collapse chunks tracked as active scavenger hosts.")]
        private int maxScavengerHostCount = 12;

        [SerializeField, Range(4, 32)]
        [Tooltip("Maximum scavenger instance count spawned around one settled collapse chunk.")]
        private int scavengersPerHost = 12;

        [SerializeField, Range(30f, 90f)]
        [Tooltip("Seconds a fallen chunk must hang on the seabed before scavengers begin feeding.")]
        private float scavengerActivationDelay = 30f;

        [SerializeField, Range(4f, 30f)]
        [Tooltip("Seconds required for a full scavenger swarm to consume one collapse chunk once feeding begins.")]
        private float scavengerConsumeDuration = 12f;

        [SerializeField, Range(0.2f, 4f)]
        [Tooltip("Horizontal feeding radius used when scattering scavengers around one fallen chunk.")]
        private float scavengerOrbitRadius = 1.1f;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("Vertical local offset applied to scavenger swarms relative to the chunk anchor.")]
        private float scavengerVerticalOffset = -0.12f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Vertical bob amplitude applied to scavenger motion.")]
        private float scavengerBobAmplitude = 0.06f;

        [SerializeField, Range(0.02f, 0.4f)]
        [Tooltip("Uniform scale range minimum for one scavenger instance.")]
        private float scavengerScaleMin = 0.08f;

        [SerializeField, Range(0.04f, 0.6f)]
        [Tooltip("Uniform scale range maximum for one scavenger instance.")]
        private float scavengerScaleMax = 0.16f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Hard cap for temporary corpse or carcass sites that feed the same bottom-scavenger renderer without requiring a collapse chunk host.")]
        private int maxExternalScavengerSiteCount = 6;

        [SerializeField, Range(4f, 90f)]
        [Tooltip("Default lifetime applied to externally registered corpse sites before the scavenger swarm clears them away.")]
        private float externalScavengerSiteDuration = 26f;

        [Header("── Gameplay Response ──────────────────")]
        [SerializeField, Range(0.3f, 1f)]
        [Tooltip("Maximum swim-speed multiplier when the sampled density reaches full saturation.")]
        private float minSpeedMultiplier = 0.58f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum drag multiplier when the sampled density reaches full saturation.")]
        private float maxDragMultiplier = 2.35f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Directional relief applied to extra drag when movement aligns with the canopy flow axis. 0.1 means along-flow travel only keeps 10% of the extra drag, while cross-flow keeps 100%.")]
        private float alongFlowDragScale = 0.1f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Minimum density required before the sticky field upgrades from drag into an entangling snare.")]
        private float entanglementMinDensity = 0.28f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Player or scooter speed below which the sticky field begins to snare and pull back toward the local mass center.")]
        private float entanglementSpeedThreshold = 1.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum normalized entanglement tension resolved by the global field sample.")]
        private float maxEntanglementStrength = 0.92f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField]
        [Tooltip("Number of active sargassum instances seen in the current source payload.")]
        private int _debugTrackedInstances;

        [SerializeField]
        [Tooltip("Number of occupied density cells after the last rebuild.")]
        private int _debugOccupiedCells;

        [SerializeField]
        [Tooltip("Approximate world-space bounds of the current density field.")]
        private Bounds _debugFieldBounds;

        [SerializeField]
        [Tooltip("World-space density rect encoded as minX, minZ, invSizeX, invSizeZ.")]
        private Vector4 _debugDensityFieldWorldRect;

        [SerializeField]
        [Tooltip("Total active instanced attachments rendered inside the current canopy field.")]
        private int _debugNestedAttachmentCount;

        [SerializeField]
        [Tooltip("Bounds used for nesting attachment draw calls.")]
        private Bounds _debugNestedAttachmentBounds;

        [SerializeField]
        [Tooltip("Count of active trapped debris nests retained in CPU state before prototype batching.")]
        private int _debugNestedStateCount;

        [SerializeField]
        [Tooltip("Active canopy disruption zone count currently influencing density, sinking, and megafauna tears.")]
        private int _debugActiveDisruptionZones;

        [SerializeField]
        [Tooltip("Maximum sink depth currently encoded into the global buoyancy sink texture.")]
        private float _debugMaxSinkDepth;

        [SerializeField]
        [Tooltip("Number of settled collapse chunks currently tracked as scavenger hosts.")]
        private int _debugScavengerHostCount;

        [SerializeField]
        [Tooltip("Total scavenger BRG instances rendered this frame.")]
        private int _debugScavengerInstanceCount;

        [SerializeField]
        [Tooltip("Bounds used for the current scavenger BRG draw call.")]
        private Bounds _debugScavengerBounds;

        // COLD ALLOC: Dictionary<long, CellData>[4096] - coarse sargassum density field keyed by XZ cell hash - owner: SargassumGlobalDragManager
        private readonly Dictionary<long, CellData> _densityCells = new Dictionary<long, CellData>(InitialCellCapacity);
        // COLD ALLOC: Dictionary<long, CellData>[4096] - transient origin-shift rebin scratch used to preserve canopy sampling until the next Burst rebuild - owner: SargassumGlobalDragManager
        private readonly Dictionary<long, CellData> _densityCellsShiftScratch = new Dictionary<long, CellData>(InitialCellCapacity);
        private byte[] _densityTextureRaw;
        private Texture2D _densityFieldTexture;
        private byte[] _sinkTextureRaw;
        private Texture2D _sinkFieldTexture;
        private Matrix4x4[][] _nestedMatricesByPrototype;
        private int[] _nestedMatrixCounts;
        private NestedAttachmentState[] _nestedAttachmentStates;
        private DisruptionZoneState[] _disruptionZones;
        private ScavengerHostState[] _scavengerHosts;
        private ExternalScavengerSiteState[] _externalScavengerSites;
        private Matrix4x4[] _scavengerMatrices;
        private NativeArray<Matrix4x4> _scavengerMatricesNative;
        private GraphicsBuffer _scavengerMatrixBuffer;
        private BatchRendererGroup _scavengerBatchRendererGroup;
        private NativeArray<MetadataValue> _scavengerBatchMetadata;
        private GraphicsBuffer _scavengerBatchHandleBuffer;
        private BatchID _scavengerBatchId;
        private BatchMeshID _scavengerBatchMeshId;
        private BatchMaterialID _scavengerBatchMaterialId;
        private Mesh _registeredScavengerMesh;
        private Material _registeredScavengerMaterial;
        private Material _scavengerBrgMaterial;
        private Material _scavengerBrgMaterialSource;
        private bool _ownsScavengerBrgMaterial;
        private int _nestedPrototypeCapacity;
        private int _activeNestedPrototypeCount;
        private int _activeNestedAttachmentStateCount;
        private int _activeDisruptionZoneCount;
        private int _activeScavengerHostCount;
        private int _activeExternalScavengerSiteCount;
        private int _scavengerInstanceCapacity;
        private NestedAttachmentPrototype[] _activeNestingPrototypes;
        private NestedAttachmentPrototype[] _fallbackNestingPrototypes;
        private Mesh _fallbackAttachmentMesh;
        private Material _fallbackCrateMaterial;
        private Material _fallbackScrapMaterial;
        private Mesh _fallbackScavengerMesh;
        private Material _fallbackScavengerMaterial;
        private Shader _fallbackLitShader;
        private Bounds _scavengerDrawBounds;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _saveRegistered;
        private int _editorValidateDepth;
        private bool _hasFieldData;
        private float _inverseCellSize;
        private int _fieldRevision;
        private HectonMapMagicVegetationBridge.FloatingLabyrinthConfig _fallbackLabyrinthConfig;
        private Vector4 _densityFieldWorldRect;
        private NativeArray<DensitySourceData> _densityBuildSources;
        private NativeParallelMultiHashMap<long, DensityContributionData> _densityContributions;
        private JobHandle _densityBuildHandle;
        private bool _densityBuildScheduled;
        private int _pendingDensityTrackedInstances;
        private Bounds _pendingDensityFieldBounds;
        private bool _pendingDensityFieldBoundsInitialized;

        /// <summary>
        /// Active singleton instance.
        /// </summary>
        public static SargassumGlobalDragManager Instance => _instance;
        public int SavePriority => 45;
        public int LoadPriority => 45;

        /// <summary>
        /// Raised when an entangled actor is actively fighting the canopy snare strongly enough to generate tension cues.
        /// </summary>
        public static event Action<EntanglementStrainSignal> OnEntanglementStrain;

        /// <summary>
        /// Raised when a leviathan-scale object tears or clears a local canopy region.
        /// </summary>
        public static event Action<MassiveDisplacementSignal> OnMassiveDisplacement;

        /// <summary>
        /// True while the manager has at least one active sargassum density cell.
        /// </summary>
        public bool HasFieldData => _hasFieldData;

        /// <summary>
        /// Monotonic revision incremented every time the field and density texture are rebuilt.
        /// </summary>
        public int FieldRevision => _fieldRevision;

        /// <summary>
        /// Baked density texture consumed by Crest damping and GPU micro-fauna.
        /// </summary>
        public Texture2D DensityFieldTexture => _densityFieldTexture;

        /// <summary>
        /// Baked buoyancy sink texture consumed by the sargassum canopy shader.
        /// </summary>
        public Texture2D SinkFieldTexture => _sinkFieldTexture;

        /// <summary>
        /// World-space density rect encoded as minX, minZ, invSizeX, invSizeZ in non-drifted sampling space.
        /// </summary>
        public Vector4 DensityFieldWorldRect => _densityFieldWorldRect;

        /// <summary>
        /// Gets or sets the global drift offset applied to both the sargassum shader and drag sampling space.
        /// </summary>
        public Vector3 GlobalDriftOffset
        {
            get => _globalDriftOffset;
            set
            {
                if (_globalDriftOffset == value)
                    return;

                _globalDriftOffset = value;
                PublishShaderGlobals();
            }
        }

        /// <summary>
        /// Broadcasts an active entanglement-strain cue to interested runtime systems.
        /// </summary>
        public static void RaiseEntanglementStrain(EntanglementStrainSignal signal)
        {
            OnEntanglementStrain?.Invoke(signal);
        }

        /// <summary>
        /// Broadcasts a canopy-clearing massive displacement cue to interested runtime systems.
        /// </summary>
        /// <param name="signal">Displacement payload.</param>
        public static void RaiseMassiveDisplacement(MassiveDisplacementSignal signal)
        {
            OnMassiveDisplacement?.Invoke(signal);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("[SargassumGlobalDragManager] Duplicate instance detected. Destroying the newer component.", this);
                Destroy(this);
                return;
            }

            _instance = this;
            SanitizeSettings();
            ResolveBridge();
            ResolveActiveNestingPrototypes();
            CreateDensityTexture();
            EnsureDisruptionStorage();
            EnsureScavengerStorage();
            EnsureScavengerRenderResources();
            ClearField();
            ClearDisruptionZones();
            ClearSinkTexture();
            PublishShaderGlobals();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                SanitizeSettings();
                return;
            }

            ResolveActiveNestingPrototypes();
            EnsureDisruptionStorage();
            EnsureScavengerStorage();
            EnsureScavengerRenderResources();
            HectonFloatingOrigin.RegisterListener(this);
            PublishShaderGlobals();
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ClearField();
            ClearNestedAttachments();
            ClearDisruptionZones();
            ClearDensityTexture();
            ClearSinkTexture();
            ClearScavengerHosts();
            ReleaseDensityBuildStorage();

            if (!Application.isPlaying)
            {
                ReleaseNestedAttachmentStorage();
                ReleaseFallbackNestingResources();
                ReleaseDensityTexture();
                ReleaseSinkTexture();
                ReleaseScavengerResources();
            }

            Shader.SetGlobalVector(_GlobalDriftOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_SinkWorldRectId, Vector4.zero);
            Shader.SetGlobalFloat(_MaxSinkDepthId, 0f);
            Shader.SetGlobalTexture(_SinkTextureId, Texture2D.blackTexture);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ReleaseNestedAttachmentStorage();
            ReleaseFallbackNestingResources();
            ReleaseDensityTexture();
            ReleaseSinkTexture();
            ReleaseScavengerResources();
            ReleaseDensityBuildStorage();
            Shader.SetGlobalVector(_GlobalDriftOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_SinkWorldRectId, Vector4.zero);
            Shader.SetGlobalFloat(_MaxSinkDepthId, 0f);
            Shader.SetGlobalTexture(_SinkTextureId, Texture2D.blackTexture);

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Rebuilds the active sargassum density field from the current MapMagic residency payload.
        /// </summary>
        public void SlowTick()
        {
            ResolveBridge();
            RebuildDensityField();
            EvaluateBuoyancyCollapseZones();
            RefreshDynamicTextures(incrementRevision: true);
            RebuildNestedAttachments();
            PublishShaderGlobals();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Draws rare nested attachments tangled into the current dense canopy field.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            bool texturesChanged = UpdateDisruptionZones(dt);
            if (texturesChanged)
            {
                RefreshDynamicTextures(incrementRevision: false);
                PublishShaderGlobals();
            }

            UpdateScavengerHosts(dt);

            if (!_hasFieldData || _activeNestedPrototypeCount <= 0 || _nestedMatricesByPrototype == null || _nestedMatrixCounts == null || _activeNestingPrototypes == null)
            {
                DrawScavengers();
                return;
            }

            UpdateNestedAttachmentBatches(dt);

            for (int prototypeIndex = 0; prototypeIndex < _activeNestedPrototypeCount; prototypeIndex++)
            {
                if ((uint)prototypeIndex >= (uint)_activeNestingPrototypes.Length)
                    break;

                NestedAttachmentPrototype prototype = _activeNestingPrototypes[prototypeIndex];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                int matrixCount = _nestedMatrixCounts[prototypeIndex];
                if (matrixCount <= 0)
                    continue;

                RenderParams renderParams = new RenderParams(prototype.Material)
                {
                    worldBounds = _debugNestedAttachmentBounds,
                    shadowCastingMode = prototype.ShadowCastingMode,
                    receiveShadows = true,
                    layer = gameObject.layer,
                    lightProbeUsage = LightProbeUsage.Off
                };
                Graphics.RenderMeshInstanced(renderParams, prototype.Mesh, 0, _nestedMatricesByPrototype[prototypeIndex], matrixCount);
            }

            DrawScavengers();
        }

        /// <summary>
        /// Samples sticky drag at the given world position.
        /// </summary>
        /// <param name="positionWS">World-space sample position.</param>
        /// <param name="radius">Approximate body radius used for overlap with the density field.</param>
        /// <param name="speedMultiplier">Resolved speed multiplier when intersecting active sargassum density.</param>
        /// <param name="dragMultiplier">Resolved drag multiplier when intersecting active sargassum density.</param>
        /// <param name="density01">Resolved normalized density in the 0..1 range.</param>
        /// <returns>True when the sample intersects meaningful sargassum density.</returns>
        public bool SampleInfluence(
            Vector3 positionWS,
            float radius,
            out float speedMultiplier,
            out float dragMultiplier,
            out float density01)
        {
            bool hasSample = SampleInfluence(positionWS, radius, Vector3.zero, out speedMultiplier, out dragMultiplier, out density01);
            return hasSample;
        }

        public bool SampleInfluence(
            Vector3 positionWS,
            float radius,
            Vector3 movementVelocityWS,
            out float speedMultiplier,
            out float dragMultiplier,
            out float density01)
        {
            bool hasSample = SampleDetailedInfluence(positionWS, radius, movementVelocityWS, entanglementSpeedThreshold + 1f, out SargassumFieldSample sample);
            speedMultiplier = sample.SpeedMultiplier;
            dragMultiplier = sample.DragMultiplier;
            density01 = sample.Density01;
            return hasSample;
        }

        /// <summary>
        /// Returns the current baked density texture and source world rect.
        /// </summary>
        /// <param name="texture">Current density texture.</param>
        /// <param name="worldRect">Current world rect encoded as minX, minZ, invSizeX, invSizeZ.</param>
        /// <returns>True when a valid density texture exists.</returns>
        public bool TryGetDensityFieldTexture(out Texture2D texture, out Vector4 worldRect)
        {
            texture = _densityFieldTexture;
            worldRect = _densityFieldWorldRect;
            return _hasFieldData &&
                   texture != null &&
                   _densityFieldWorldRect.z > 0f &&
                   _densityFieldWorldRect.w > 0f;
        }

        /// <summary>
        /// Returns the current buoyancy sink texture and source world rect.
        /// </summary>
        /// <param name="texture">Current sink texture.</param>
        /// <param name="worldRect">Current world rect encoded as minX, minZ, invSizeX, invSizeZ.</param>
        /// <returns>True when a valid sink texture exists.</returns>
        public bool TryGetSinkFieldTexture(out Texture2D texture, out Vector4 worldRect)
        {
            texture = _sinkFieldTexture;
            worldRect = _densityFieldWorldRect;
            return _hasFieldData &&
                   texture != null &&
                   _densityFieldWorldRect.z > 0f &&
                   _densityFieldWorldRect.w > 0f;
        }

        /// <summary>
        /// Registers a leviathan-scale or submarine-scale canopy displacement.
        /// </summary>
        /// <param name="position">World-space center of the tear.</param>
        /// <param name="radius">World-space canopy tear radius.</param>
        /// <param name="duration">Lifetime of the tear cue in seconds.</param>
        public void RegisterMassiveDisplacement(Vector3 position, float radius, float duration)
        {
            float clampedRadius = Mathf.Max(1f, radius);
            float clampedDuration = Mathf.Max(0.25f, duration);
            float extremePanicRadius = Mathf.Max(massiveDisplacementExtremePanicRadius, clampedRadius * 3f);

            RegisterOrReinforceDisruptionZone(
                position - _globalDriftOffset,
                clampedRadius,
                1f,
                0f,
                massiveDisplacementRampDuration,
                clampedDuration,
                massiveDisplacementFadeDuration,
                DisruptionZoneMode.MassiveDisplacement);

            SargassumCutManager cutManager = SargassumCutManager.Instance;
            if (cutManager != null)
                cutManager.RegisterExternalCut(position, clampedRadius, massiveDisplacementCutStrength, Vector3.up, 1.15f);

            RaiseMassiveDisplacement(new MassiveDisplacementSignal
            {
                PositionWS = position,
                RadiusWS = clampedRadius,
                Duration = clampedDuration,
                ExtremePanicRadiusWS = extremePanicRadius
            });

            RefreshDynamicTextures(incrementRevision: false);
            PublishShaderGlobals();
        }

        void ISargassumMassiveDisplacementReceiver.RegisterMassiveDisplacement(Vector3 position, float radius, float duration)
        {
            RegisterMassiveDisplacement(position, radius, duration);
        }

        internal bool SampleDetailedInfluence(
            Vector3 positionWS,
            float radius,
            float currentSpeed,
            out SargassumFieldSample sample)
        {
            return SampleDetailedInfluence(positionWS, radius, Vector3.zero, currentSpeed, out sample);
        }

        internal bool SampleDetailedInfluence(
            Vector3 positionWS,
            float radius,
            Vector3 movementVelocityWS,
            float currentSpeed,
            out SargassumFieldSample sample)
        {
            sample = default;
            sample.SpeedMultiplier = 1f;
            sample.DragMultiplier = 1f;
            sample.AnchorWS = positionWS;
            sample.Window01 = 1f;

            if (!_hasFieldData || _densityCells.Count == 0)
                return false;

            Vector3 sampledPositionWS = positionWS - _globalDriftOffset;
            float effectiveRadius = Mathf.Max(0.1f, radius);
            int minCellX = Mathf.FloorToInt((sampledPositionWS.x - effectiveRadius) * _inverseCellSize);
            int maxCellX = Mathf.FloorToInt((sampledPositionWS.x + effectiveRadius) * _inverseCellSize);
            int minCellZ = Mathf.FloorToInt((sampledPositionWS.z - effectiveRadius) * _inverseCellSize);
            int maxCellZ = Mathf.FloorToInt((sampledPositionWS.z + effectiveRadius) * _inverseCellSize);
            float accumulatedDensity = 0f;
            float weightedCenterX = 0f;
            float weightedCenterY = 0f;
            float weightedCenterZ = 0f;
            float accumulatedWeight = 0f;
            float strongestContribution = 0f;
            Vector3 strongestCenterWS = sampledPositionWS;

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long key = PackCellKey(cellX, cellZ);
                    if (!_densityCells.TryGetValue(key, out CellData cell))
                        continue;

                    if (sampledPositionWS.y + effectiveRadius < cell.MinY || sampledPositionWS.y - effectiveRadius > cell.MaxY)
                        continue;

                    float cellCenterX = (cellX + 0.5f) * cellSize;
                    float cellCenterZ = (cellZ + 0.5f) * cellSize;
                    float planarDistance = Vector2.Distance(
                        new Vector2(sampledPositionWS.x, sampledPositionWS.z),
                        new Vector2(cellCenterX, cellCenterZ));
                    float planarWeight = 1f - planarDistance / Mathf.Max(cellSize + effectiveRadius, 0.001f);
                    if (planarWeight <= 0f)
                        continue;

                    float weightedContribution = cell.Density * Mathf.Pow(planarWeight, Mathf.Max(1f, distancePower));
                    accumulatedDensity += weightedContribution;
                    accumulatedWeight += weightedContribution;
                    weightedCenterX += cellCenterX * weightedContribution;
                    weightedCenterY += ((cell.MinY + cell.MaxY) * 0.5f) * weightedContribution;
                    weightedCenterZ += cellCenterZ * weightedContribution;

                    if (weightedContribution > strongestContribution)
                    {
                        strongestContribution = weightedContribution;
                        strongestCenterWS.x = cellCenterX;
                        strongestCenterWS.y = (cell.MinY + cell.MaxY) * 0.5f;
                        strongestCenterWS.z = cellCenterZ;
                    }
                }
            }

            DisruptionSample disruption = SampleDisruptionNoDrift(sampledPositionWS);
            sample.Density01 = Mathf.Clamp01(accumulatedDensity * densityNormalization);
            if (disruption.Suppression01 > 0f)
                sample.Density01 *= 1f - disruption.Suppression01;

            if (sample.Density01 < MinDensityThreshold)
            {
                sample.Density01 = 0f;
                return false;
            }

            Vector3 centroidWS = accumulatedWeight > 0.0001f
                ? new Vector3(
                    weightedCenterX / accumulatedWeight,
                    weightedCenterY / accumulatedWeight,
                    weightedCenterZ / accumulatedWeight)
                : strongestCenterWS;
            sample.AnchorWS = Vector3.Lerp(centroidWS, strongestCenterWS, 0.65f) + _globalDriftOffset;
            sample.AnchorWS.y -= disruption.SinkDepthWS;
            sample.SpeedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, sample.Density01);
            float isotropicDragMultiplier = Mathf.Lerp(1f, maxDragMultiplier, sample.Density01);
            float directionalDragScale = ResolveDirectionalDragScale(movementVelocityWS);
            sample.DragMultiplier = 1f + (isotropicDragMultiplier - 1f) * directionalDragScale;
            sample.Window01 = EvaluateCanopyWindow01(sampledPositionWS);
            sample.Occlusion01 = Mathf.Clamp01(sample.Density01 * (1f - sample.Window01 * 0.78f));
            sample.Entanglement01 = ResolveEntanglement01(sample.Density01, currentSpeed);
            sample.HasInfluence = true;
            return true;
        }

        private void ResolveBridge()
        {
            if (mapMagicVegetationBridge != null)
                return;

            mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            ShiftDensityCellsRuntimeOffset(runtimeOffset);
            ShiftBoundsCenter(ref _debugFieldBounds, runtimeOffset);
            ShiftBoundsCenter(ref _pendingDensityFieldBounds, runtimeOffset);
            ShiftBoundsCenter(ref _debugNestedAttachmentBounds, runtimeOffset);
            ShiftBoundsCenter(ref _scavengerDrawBounds, runtimeOffset);
            ShiftBoundsCenter(ref _debugScavengerBounds, runtimeOffset);
            ShiftWorldRectXY(ref _densityFieldWorldRect, runtimeOffset);
            ShiftWorldRectXY(ref _debugDensityFieldWorldRect, runtimeOffset);

            if (_disruptionZones != null)
            {
                for (int i = 0; i < _activeDisruptionZoneCount; i++)
                {
                    DisruptionZoneState zone = _disruptionZones[i];
                    zone.SampleSpaceCenterWS += runtimeOffset;
                    _disruptionZones[i] = zone;
                }
            }

            if (_nestedAttachmentStates != null)
            {
                for (int i = 0; i < _activeNestedAttachmentStateCount; i++)
                {
                    NestedAttachmentState state = _nestedAttachmentStates[i];
                    state.SampleSpaceAnchorWS += runtimeOffset;
                    _nestedAttachmentStates[i] = state;
                }
            }

            if (_scavengerHosts != null)
            {
                for (int i = 0; i < _activeScavengerHostCount; i++)
                {
                    ScavengerHostState host = _scavengerHosts[i];
                    host.AnchorWS += runtimeOffset;
                    _scavengerHosts[i] = host;
                }
            }

            if (_externalScavengerSites != null)
            {
                for (int i = 0; i < _externalScavengerSites.Length; i++)
                {
                    ExternalScavengerSiteState site = _externalScavengerSites[i];
                    site.AnchorWS += runtimeOffset;
                    _externalScavengerSites[i] = site;
                }
            }

            PublishShaderGlobals();
        }

        private void ShiftDensityCellsRuntimeOffset(Vector3 runtimeOffset)
        {
            if (_densityCells.Count <= 0)
                return;

            _densityCellsShiftScratch.Clear();
            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long key = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
                cell.MinY += runtimeOffset.y;
                cell.MaxY += runtimeOffset.y;

                int cellX = (int)(key >> 32);
                int cellZ = (int)key;
                float shiftedCenterX = ((cellX + 0.5f) * cellSize) + runtimeOffset.x;
                float shiftedCenterZ = ((cellZ + 0.5f) * cellSize) + runtimeOffset.z;
                int shiftedCellX = Mathf.FloorToInt(shiftedCenterX * _inverseCellSize);
                int shiftedCellZ = Mathf.FloorToInt(shiftedCenterZ * _inverseCellSize);
                long shiftedKey = PackCellKey(shiftedCellX, shiftedCellZ);

                if (_densityCellsShiftScratch.TryGetValue(shiftedKey, out CellData existing))
                {
                    existing.Density += cell.Density;
                    existing.MinY = Mathf.Min(existing.MinY, cell.MinY);
                    existing.MaxY = Mathf.Max(existing.MaxY, cell.MaxY);
                    _densityCellsShiftScratch[shiftedKey] = existing;
                }
                else
                {
                    _densityCellsShiftScratch.Add(shiftedKey, cell);
                }
            }

            _densityCells.Clear();
            Dictionary<long, CellData>.Enumerator shiftedEnumerator = _densityCellsShiftScratch.GetEnumerator();
            while (shiftedEnumerator.MoveNext())
                _densityCells.Add(shiftedEnumerator.Current.Key, shiftedEnumerator.Current.Value);

            _debugOccupiedCells = _densityCells.Count;
            _hasFieldData = _densityCells.Count > 0;
        }

        private static void ShiftBoundsCenter(ref Bounds bounds, Vector3 runtimeOffset)
        {
            if (bounds.size.sqrMagnitude <= 0.0001f)
                return;

            bounds.center += runtimeOffset;
        }

        private static void ShiftWorldRectXY(ref Vector4 worldRect, Vector3 runtimeOffset)
        {
            if (worldRect.z <= 0f || worldRect.w <= 0f)
                return;

            worldRect.x += runtimeOffset.x;
            worldRect.y += runtimeOffset.z;
        }

        private void RebuildDensityField()
        {
            if (_densityBuildScheduled)
            {
                if (!_densityBuildHandle.IsCompleted)
                    return;

                _densityBuildHandle.Complete();
                _densityBuildScheduled = false;
                ApplyDensityFieldBuildResults();
                _densityBuildHandle = default;
            }

            if (mapMagicVegetationBridge == null)
            {
                ClearField();
                return;
            }

            if (!mapMagicVegetationBridge.TryGetActiveSurfaceNativePayload(
                    out NativeArray<Matrix4x4> matrices,
                    out _,
                    out NativeArray<int> types,
                    out int activeCount) ||
                activeCount <= 0)
            {
                ClearField();
                return;
            }

            Bounds fieldBounds = default;
            bool boundsInitialized = false;
            int trackedInstances = 0;
            Vector3 universeOffset = mapMagicVegetationBridge.TotalUniverseOffset;
            EnsureDensityBuildSourceCapacity(activeCount);

            int sourceCount = 0;
            int estimatedContributionCount = 0;

            for (int i = 0; i < activeCount; i++)
            {
                if ((HectonVegetationInstanceType)types[i] != HectonVegetationInstanceType.Sargassum)
                    continue;

                Matrix4x4 matrix = matrices[i];
                Vector3 origin = new Vector3(matrix.m03, matrix.m13, matrix.m23) + universeOffset;
                float scale = ExtractUniformScale(matrix);
                float influenceRadius = Mathf.Max(baseInfluenceRadius * scale, cellSize * 0.35f);
                float verticalHalfExtent = Mathf.Max(baseVerticalHalfExtent * scale, 0.5f);

                _densityBuildSources[sourceCount++] = new DensitySourceData
                {
                    OriginWS = origin,
                    Scale = scale
                };

                int minCellX = Mathf.FloorToInt((origin.x - influenceRadius) * _inverseCellSize);
                int maxCellX = Mathf.FloorToInt((origin.x + influenceRadius) * _inverseCellSize);
                int minCellZ = Mathf.FloorToInt((origin.z - influenceRadius) * _inverseCellSize);
                int maxCellZ = Mathf.FloorToInt((origin.z + influenceRadius) * _inverseCellSize);
                estimatedContributionCount += Mathf.Max(1, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));

                Bounds instanceBounds = new Bounds(origin, new Vector3(influenceRadius * 2f, verticalHalfExtent * 2f, influenceRadius * 2f));
                if (!boundsInitialized)
                {
                    fieldBounds = instanceBounds;
                    boundsInitialized = true;
                }
                else
                {
                    fieldBounds.Encapsulate(instanceBounds);
                }

                trackedInstances++;
            }

            if (sourceCount <= 0)
            {
                ClearField();
                return;
            }

            EnsureDensityContributionCapacity(estimatedContributionCount);
            _densityContributions.Clear();

            BuildDensityContributionJob buildJob = new BuildDensityContributionJob
            {
                Sources = _densityBuildSources.GetSubArray(0, sourceCount),
                Contributions = _densityContributions.AsParallelWriter(),
                CellSize = cellSize,
                InverseCellSize = _inverseCellSize,
                BaseInfluenceRadius = baseInfluenceRadius,
                BaseVerticalHalfExtent = baseVerticalHalfExtent,
                DistancePower = distancePower
            };

            _densityBuildHandle = buildJob.Schedule(sourceCount, math.max(1, math.min(32, sourceCount / 4)));
            _densityBuildScheduled = true;
            _pendingDensityTrackedInstances = trackedInstances;
            _pendingDensityFieldBounds = fieldBounds;
            _pendingDensityFieldBoundsInitialized = boundsInitialized;
        }

        private void ApplyDensityFieldBuildResults()
        {
            ClearField();

            var contributionEnumerator = _densityContributions.GetEnumerator();
            while (contributionEnumerator.MoveNext())
            {
                long key = contributionEnumerator.Current.Key;
                DensityContributionData contribution = contributionEnumerator.Current.Value;
                if (_densityCells.TryGetValue(key, out CellData existing))
                {
                    existing.Density += contribution.Density;
                    existing.MinY = Mathf.Min(existing.MinY, contribution.MinY);
                    existing.MaxY = Mathf.Max(existing.MaxY, contribution.MaxY);
                    _densityCells[key] = existing;
                }
                else
                {
                    _densityCells.Add(
                        key,
                        new CellData
                        {
                            Density = contribution.Density,
                            MinY = contribution.MinY,
                            MaxY = contribution.MaxY
                        });
                }
            }

            _hasFieldData = _densityCells.Count > 0;
            _debugTrackedInstances = _pendingDensityTrackedInstances;
            _debugOccupiedCells = _densityCells.Count;
            _debugFieldBounds = _pendingDensityFieldBoundsInitialized ? _pendingDensityFieldBounds : default;
        }

        private void ClearField()
        {
            _densityCells.Clear();
            _hasFieldData = false;
            _debugTrackedInstances = 0;
            _debugOccupiedCells = 0;
            _debugFieldBounds = default;
            _densityFieldWorldRect = Vector4.zero;
            _debugDensityFieldWorldRect = Vector4.zero;
        }

        private void EnsureDensityBuildSourceCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, Mathf.NextPowerOfTwo(requiredCount));
            if (_densityBuildSources.IsCreated && _densityBuildSources.Length >= safeCount)
                return;

            if (_densityBuildSources.IsCreated)
                _densityBuildSources.Dispose();

            // COLD ALLOC: NativeArray<DensitySourceData>[capacity] - transient Burst source staging for canopy density rebuilds - owner: SargassumGlobalDragManager
            _densityBuildSources = new NativeArray<DensitySourceData>(safeCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void EnsureDensityContributionCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, Mathf.NextPowerOfTwo(requiredCount));
            if (_densityContributions.IsCreated && _densityContributions.Capacity >= safeCount)
                return;

            if (_densityContributions.IsCreated)
                _densityContributions.Dispose();

            // COLD ALLOC: NativeParallelMultiHashMap<long,DensityContributionData>[capacity] - Burst-built cell contribution fanout before dictionary compaction - owner: SargassumGlobalDragManager
            _densityContributions = new NativeParallelMultiHashMap<long, DensityContributionData>(safeCount, Allocator.Persistent);
        }

        private void ReleaseDensityBuildStorage()
        {
            JobHandle dependency = _densityBuildScheduled ? _densityBuildHandle : default;
            DisposeNativeArray(ref _densityBuildSources, dependency);
            DisposeNativeParallelMultiHashMap(ref _densityContributions, dependency);
            _densityBuildHandle = default;
            _densityBuildScheduled = false;
            _pendingDensityTrackedInstances = 0;
            _pendingDensityFieldBounds = default;
            _pendingDensityFieldBoundsInitialized = false;
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

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> hashMap,
            JobHandle dependency)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!hashMap.IsCreated)
                return;

            if (dependency.IsCompleted)
                hashMap.Dispose();
            else
                hashMap.Dispose(dependency);

            hashMap = default;
        }

        private void ClearNestedAttachments()
        {
            _activeNestedPrototypeCount = 0;
            _activeNestedAttachmentStateCount = 0;
            _debugNestedAttachmentCount = 0;
            _debugNestedAttachmentBounds = default;
            _debugNestedStateCount = 0;

            if (_nestedMatrixCounts == null)
                return;

            for (int i = 0; i < _nestedMatrixCounts.Length; i++)
                _nestedMatrixCounts[i] = 0;
        }

        private void EnsureDisruptionStorage()
        {
            int requiredCapacity = Mathf.Clamp(maxDisruptionZoneCount, 1, MaxDisruptionZoneCapacity);
            if (_disruptionZones != null && _disruptionZones.Length == requiredCapacity)
                return;

            // COLD ALLOC: DisruptionZoneState[capacity] - persistent canopy destruction zones - owner: SargassumGlobalDragManager
            _disruptionZones = new DisruptionZoneState[requiredCapacity];
            _activeDisruptionZoneCount = 0;
            _debugActiveDisruptionZones = 0;
        }

        private void ClearDisruptionZones()
        {
            _activeDisruptionZoneCount = 0;
            _debugActiveDisruptionZones = 0;
            _debugMaxSinkDepth = 0f;

            if (_disruptionZones == null)
                return;

            Array.Clear(_disruptionZones, 0, _disruptionZones.Length);
        }

        private bool UpdateDisruptionZones(float dt)
        {
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return false;

            bool changed = false;
            float deltaTime = Mathf.Max(0f, dt);
            int index = 0;
            while (index < _activeDisruptionZoneCount)
            {
                DisruptionZoneState zone = _disruptionZones[index];
                float previousStrength01 = EvaluateDisruptionZone01(zone);
                zone.Age += deltaTime;

                float lifeTime = zone.Mode == (byte)DisruptionZoneMode.CutCollapse
                    ? float.MaxValue
                    : zone.HoldDuration + zone.FadeDuration;

                if (zone.Age >= lifeTime)
                {
                    int lastIndex = _activeDisruptionZoneCount - 1;
                    _disruptionZones[index] = _disruptionZones[lastIndex];
                    _disruptionZones[lastIndex] = default;
                    _activeDisruptionZoneCount = lastIndex;
                    changed = true;
                    continue;
                }

                float currentStrength01 = EvaluateDisruptionZone01(zone);
                if (Mathf.Abs(currentStrength01 - previousStrength01) > 0.0005f)
                    changed = true;

                _disruptionZones[index] = zone;
                index++;
            }

            _debugActiveDisruptionZones = _activeDisruptionZoneCount;
            return changed;
        }

        private void EvaluateBuoyancyCollapseZones()
        {
            SargassumCutManager cutManager = SargassumCutManager.Instance;
            if (cutManager == null || _densityCells.Count == 0)
                return;

            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long cellKey = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
                float normalizedDensity = Mathf.Clamp01(cell.Density * densityNormalization);
                if (normalizedDensity < nestingNodeDensityThreshold)
                    continue;

                int cellX = (int)(cellKey >> 32);
                int cellZ = (int)cellKey;
                Vector3 cellCenterWS = new Vector3(
                    (cellX + 0.5f) * cellSize + _globalDriftOffset.x,
                    (cell.MinY + cell.MaxY) * 0.5f + _globalDriftOffset.y,
                    (cellZ + 0.5f) * cellSize + _globalDriftOffset.z);

                if (!cutManager.SampleRecentCutArea(cellCenterWS, buoyancyCollapseQueryRadius, out float accumulatedCutAreaWS, out float strongestCut01))
                    continue;

                if (accumulatedCutAreaWS < buoyancyCollapseAreaThreshold || strongestCut01 < buoyancyCollapseCutStrengthThreshold)
                    continue;

                float collapseStrength = Mathf.Clamp01(accumulatedCutAreaWS / Mathf.Max(buoyancyCollapseAreaThreshold, 0.001f));
                float sinkDepth = Mathf.Lerp(buoyancyCollapseSinkDepth * 0.45f, buoyancyCollapseSinkDepth, collapseStrength);
                int zoneIndex = RegisterOrReinforceDisruptionZone(
                    cellCenterWS - _globalDriftOffset,
                    buoyancyCollapseRadius,
                    collapseStrength,
                    sinkDepth,
                    buoyancyCollapseSinkDuration,
                    0f,
                    0f,
                    DisruptionZoneMode.CutCollapse);
                TrySpawnCollapseChunks(zoneIndex, cellCenterWS, collapseStrength, accumulatedCutAreaWS);
            }
        }

        private int RegisterOrReinforceDisruptionZone(
            Vector3 sampleSpaceCenterWS,
            float radiusWS,
            float strength01,
            float sinkDepthWS,
            float rampDuration,
            float holdDuration,
            float fadeDuration,
            DisruptionZoneMode mode)
        {
            EnsureDisruptionStorage();

            float clampedRadius = Mathf.Max(cellSize * 0.5f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength01);
            float clampedRamp = Mathf.Max(0.01f, rampDuration);
            float clampedHold = Mathf.Max(0f, holdDuration);
            float clampedFade = Mathf.Max(0f, fadeDuration);
            int zoneIndex = FindMatchingDisruptionZoneIndex(sampleSpaceCenterWS, clampedRadius, mode);

            if (zoneIndex < 0)
            {
                if (_activeDisruptionZoneCount >= (_disruptionZones != null ? _disruptionZones.Length : 0))
                    return -1;

                zoneIndex = _activeDisruptionZoneCount++;
            }

            DisruptionZoneState zone = _disruptionZones[zoneIndex];
            bool isExistingZone = zone.Mode != 0;
            if (zone.Mode == 0)
                zone.SampleSpaceCenterWS = sampleSpaceCenterWS;
            else
                zone.SampleSpaceCenterWS = Vector3.Lerp(zone.SampleSpaceCenterWS, sampleSpaceCenterWS, 0.35f);

            zone.RadiusWS = Mathf.Max(zone.RadiusWS, clampedRadius);
            zone.Strength01 = Mathf.Max(zone.Strength01, clampedStrength);
            zone.SinkDepthWS = Mathf.Max(zone.SinkDepthWS, Mathf.Max(0f, sinkDepthWS));
            zone.RampDuration = Mathf.Min(zone.RampDuration > 0f ? zone.RampDuration : clampedRamp, clampedRamp);
            zone.HoldDuration = Mathf.Max(zone.HoldDuration, clampedHold);
            zone.FadeDuration = Mathf.Max(zone.FadeDuration, clampedFade);
            zone.Mode = (byte)mode;
            if (!isExistingZone || mode == DisruptionZoneMode.MassiveDisplacement)
                zone.Age = 0f;
            _disruptionZones[zoneIndex] = zone;
            _debugActiveDisruptionZones = _activeDisruptionZoneCount;
            return zoneIndex;
        }

        private int FindMatchingDisruptionZoneIndex(Vector3 sampleSpaceCenterWS, float radiusWS, DisruptionZoneMode mode)
        {
            if (_disruptionZones == null)
                return -1;

            float mergeDistance = Mathf.Max(cellSize, radiusWS * 0.85f);
            float mergeDistanceSq = mergeDistance * mergeDistance;
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                if (_disruptionZones[i].Mode != (byte)mode)
                    continue;

                Vector3 delta = _disruptionZones[i].SampleSpaceCenterWS - sampleSpaceCenterWS;
                delta.y = 0f;
                if (delta.sqrMagnitude <= mergeDistanceSq)
                    return i;
            }

            return -1;
        }

        private static float EvaluateDisruptionZone01(DisruptionZoneState zone)
        {
            float ramp = Mathf.Clamp01(zone.Age / Mathf.Max(zone.RampDuration, 0.01f));
            if (zone.Mode == (byte)DisruptionZoneMode.CutCollapse)
                return zone.Strength01 * ramp;

            float fade = zone.Age <= zone.HoldDuration
                ? 1f
                : 1f - Mathf.Clamp01((zone.Age - zone.HoldDuration) / Mathf.Max(zone.FadeDuration, 0.01f));
            return zone.Strength01 * ramp * fade;
        }

        private void TrySpawnCollapseChunks(int zoneIndex, Vector3 zoneCenterWS, float collapseStrength, float accumulatedCutAreaWS)
        {
            if (zoneIndex < 0 ||
                collapseChunkPrefab == null ||
                _disruptionZones == null ||
                zoneIndex >= _activeDisruptionZoneCount ||
                _activeDisruptionZoneCount <= 0)
            {
                return;
            }

            float catastrophicAreaThreshold = buoyancyCollapseAreaThreshold * Mathf.Max(1f, collapseChunkAreaThresholdMultiplier);
            if (accumulatedCutAreaWS < catastrophicAreaThreshold)
                return;

            ObjectPoolManager poolManager = ObjectPoolManager.Instance;
            if (poolManager == null)
                return;

            DisruptionZoneState zone = _disruptionZones[zoneIndex];
            if ((zone.Flags & CollapseChunkBurstSpawnedFlag) != 0)
                return;

            int spawnCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(1f, collapseChunkSpawnCount, Mathf.Clamp01(collapseStrength))),
                1,
                collapseChunkSpawnCount);

            for (int chunkIndex = 0; chunkIndex < spawnCount; chunkIndex++)
            {
                float angle = HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x85A308D3u) * Mathf.PI * 2f;
                float radialT = Mathf.Sqrt(HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x13198A2Eu));
                float radius = collapseChunkSpawnRadius * radialT;
                Vector3 spawnOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 spawnPosition = zoneCenterWS + spawnOffset;
                spawnPosition.y += Mathf.Lerp(0.25f, 1.1f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x03707344u));

                Vector3 lateralDirection = spawnOffset.sqrMagnitude > 0.0001f ? spawnOffset.normalized : Vector3.right;
                Vector3 linearVelocity = new Vector3(
                    lateralDirection.x * collapseChunkHorizontalSpeed,
                    -collapseChunkDownwardSpeed,
                    lateralDirection.z * collapseChunkHorizontalSpeed);
                Vector3 angularVelocity = new Vector3(
                    Mathf.Lerp(-2.8f, 2.8f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xA4093822u)),
                    Mathf.Lerp(-1.9f, 1.9f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x299F31D0u)),
                    Mathf.Lerp(-2.4f, 2.4f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x082EFA98u)));
                float uniformScale = Mathf.Lerp(
                    collapseChunkScaleMin,
                    collapseChunkScaleMax,
                    HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xEC4E6C89u));

                GameObject chunkInstance = poolManager.Spawn(
                    collapseChunkPrefab,
                    spawnPosition,
                    Quaternion.Euler(
                        Mathf.Lerp(-18f, 18f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x452821E6u)),
                        Mathf.Lerp(0f, 360f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x38D01377u)),
                        Mathf.Lerp(-18f, 18f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xBE5466CFu))));
                if (chunkInstance == null)
                    continue;

                if (chunkInstance.TryGetComponent(out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime, 0);
                }
                else
                {
                    chunkInstance.transform.localScale = chunkInstance.transform.localScale * uniformScale;
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
                    {
                        chunkRigidbody.isKinematic = false;
                        chunkRigidbody.WakeUp();
                        WriteSafeVelocities(chunkRigidbody, linearVelocity, angularVelocity);
                    }

                    poolManager.Despawn(chunkInstance, collapseChunkLifetime);
                }
            }

            zone.Flags |= CollapseChunkBurstSpawnedFlag;
            _disruptionZones[zoneIndex] = zone;
        }

        internal void RegisterCollapseChunkImpact(Vector3 impactPointWS, Vector3 impactNormalWS, float impactSpeed, int fragmentDepth)
        {
            if (collapseChunkPrefab == null ||
                fragmentDepth > collapseCascadeMaxDepth ||
                impactSpeed < collapseCascadeImpactThreshold)
            {
                return;
            }

            ObjectPoolManager poolManager = ObjectPoolManager.Instance;
            if (poolManager == null)
                return;

            float severity01 = Mathf.Clamp01((impactSpeed - collapseCascadeImpactThreshold) / Mathf.Max(collapseCascadeImpactThreshold, 0.001f));
            Vector3 sampleSpaceImpact = impactPointWS - _globalDriftOffset;
            float radiusScale = Mathf.Pow(collapseCascadeScaleMultiplier, Mathf.Max(0, fragmentDepth));
            RegisterOrReinforceDisruptionZone(
                sampleSpaceImpact,
                Mathf.Max(cellSize * 0.35f, buoyancyCollapseRadius * radiusScale),
                Mathf.Lerp(0.2f, 0.55f, severity01),
                Mathf.Lerp(buoyancyCollapseSinkDepth * 0.18f, buoyancyCollapseSinkDepth * 0.42f, severity01),
                Mathf.Lerp(buoyancyCollapseSinkDuration * 0.35f, buoyancyCollapseSinkDuration * 0.65f, severity01),
                0f,
                0f,
                DisruptionZoneMode.CutCollapse);

            Vector3 safeNormal = impactNormalWS.sqrMagnitude > 0.0001f ? impactNormalWS.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(safeNormal, Vector3.up);
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector3.Cross(safeNormal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(safeNormal, tangent).normalized;
            int spawnCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(1f, collapseCascadeSpawnCount, severity01)),
                1,
                collapseCascadeSpawnCount);

            for (int chunkIndex = 0; chunkIndex < spawnCount; chunkIndex++)
            {
                float angle = HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 1), 0x6C8E9CF5u) * Mathf.PI * 2f;
                float radialT = Mathf.Sqrt(HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 5), 0x5D588B65u));
                Vector3 lateralDirection =
                    tangent * Mathf.Cos(angle) +
                    bitangent * Mathf.Sin(angle);
                Vector3 spawnPosition = impactPointWS + safeNormal * 0.18f + lateralDirection * (collapseChunkSpawnRadius * 0.35f * radialT);
                Vector3 linearVelocity =
                    safeNormal * (collapseChunkHorizontalSpeed * collapseCascadeVelocityMultiplier * Mathf.Lerp(0.45f, 0.95f, severity01)) +
                    lateralDirection * (collapseChunkHorizontalSpeed * collapseCascadeVelocityMultiplier * Mathf.Lerp(0.35f, 0.9f, severity01)) +
                    Vector3.down * (collapseChunkDownwardSpeed * Mathf.Lerp(0.55f, 1.1f, severity01));
                Vector3 angularVelocity = new Vector3(
                    Mathf.Lerp(-3.4f, 3.4f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 9), 0x8CB92BA7u)),
                    Mathf.Lerp(-2.8f, 2.8f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 13), 0x4CF5AD43u)),
                    Mathf.Lerp(-3.1f, 3.1f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 17), 0x9D12A0F1u)));
                float parentScaleMultiplier = Mathf.Pow(collapseCascadeScaleMultiplier, Mathf.Max(1, fragmentDepth));
                float uniformScale = Mathf.Lerp(collapseChunkScaleMin, collapseChunkScaleMax, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 21), 0x3D8E1129u)) * parentScaleMultiplier;

                GameObject chunkInstance = poolManager.Spawn(
                    collapseChunkPrefab,
                    spawnPosition,
                    Quaternion.LookRotation(lateralDirection.sqrMagnitude > 0.0001f ? lateralDirection : Vector3.forward, safeNormal));
                if (chunkInstance == null)
                    continue;

                if (chunkInstance.TryGetComponent(out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime * 0.7f, fragmentDepth);
                }
                else
                {
                    chunkInstance.transform.localScale = chunkInstance.transform.localScale * uniformScale;
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
                    {
                        chunkRigidbody.isKinematic = false;
                        chunkRigidbody.WakeUp();
                        WriteSafeVelocities(chunkRigidbody, linearVelocity, angularVelocity);
                    }

                    poolManager.Despawn(chunkInstance, collapseChunkLifetime * 0.7f);
                }
            }
        }

        private DisruptionSample SampleDisruptionNoDrift(Vector3 sampledPositionWS)
        {
            DisruptionSample sample = default;
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return sample;

            Vector2 sampleXZ = new Vector2(sampledPositionWS.x, sampledPositionWS.z);
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                DisruptionZoneState zone = _disruptionZones[i];
                float zone01 = EvaluateDisruptionZone01(zone);
                if (zone01 <= 0f || zone.RadiusWS <= 0f)
                    continue;

                Vector2 deltaXZ = sampleXZ - new Vector2(zone.SampleSpaceCenterWS.x, zone.SampleSpaceCenterWS.z);
                float distance = deltaXZ.magnitude;
                if (distance >= zone.RadiusWS)
                    continue;

                float radial01 = 1f - distance / Mathf.Max(zone.RadiusWS, 0.001f);
                float influence01 = zone01 * radial01 * radial01;
                if (influence01 > sample.Suppression01)
                    sample.Suppression01 = influence01;

                float sinkDepthWS = zone.SinkDepthWS * influence01;
                if (sinkDepthWS > sample.SinkDepthWS)
                    sample.SinkDepthWS = sinkDepthWS;
            }

            return sample;
        }

        private void PublishShaderGlobals()
        {
            Shader.SetGlobalVector(_GlobalDriftOffsetId, _globalDriftOffset);
            Shader.SetGlobalTexture(_SinkTextureId, _sinkFieldTexture != null ? _sinkFieldTexture : Texture2D.blackTexture);
            Shader.SetGlobalVector(_SinkWorldRectId, _densityFieldWorldRect);
            Shader.SetGlobalFloat(_MaxSinkDepthId, _debugMaxSinkDepth);
        }

        private void ResolveActiveNestingPrototypes()
        {
            if (nestingPrototypes != null && nestingPrototypes.Length > 0)
            {
                _activeNestingPrototypes = nestingPrototypes;
                return;
            }

            EnsureFallbackNestingResources();
            _activeNestingPrototypes = _fallbackNestingPrototypes;
        }

        private void EnsureFallbackNestingResources()
        {
            if (_fallbackAttachmentMesh == null)
                _fallbackAttachmentMesh = BuildFallbackAttachmentMesh();

            if (_fallbackCrateMaterial == null)
            {
                Shader litShader = ResolveFallbackLitShader();
                if (litShader != null)
                {
                    _fallbackCrateMaterial = new Material(litShader)
                    {
                        name = "__SargassumNestCrateFallback",
                        hideFlags = HideFlags.HideAndDontSave
                    }; // COLD ALLOC: Material[1] - runtime crate fallback for canopy nesting - owner: SargassumGlobalDragManager
                    _fallbackCrateMaterial.enableInstancing = true;
                    _fallbackCrateMaterial.SetColor("_BaseColor", new Color(0.42f, 0.29f, 0.17f, 1f));
                    _fallbackCrateMaterial.SetColor("_Color", new Color(0.42f, 0.29f, 0.17f, 1f));
                }
            }

            if (_fallbackScrapMaterial == null)
            {
                Shader litShader = ResolveFallbackLitShader();
                if (litShader != null)
                {
                    _fallbackScrapMaterial = new Material(litShader)
                    {
                        name = "__SargassumNestScrapFallback",
                        hideFlags = HideFlags.HideAndDontSave
                    }; // COLD ALLOC: Material[1] - runtime scrap fallback for canopy nesting - owner: SargassumGlobalDragManager
                    _fallbackScrapMaterial.enableInstancing = true;
                    _fallbackScrapMaterial.SetColor("_BaseColor", new Color(0.28f, 0.34f, 0.38f, 1f));
                    _fallbackScrapMaterial.SetColor("_Color", new Color(0.28f, 0.34f, 0.38f, 1f));
                }
            }

            if (_fallbackNestingPrototypes != null)
                return;

            _fallbackNestingPrototypes = new[]
            {
                new NestedAttachmentPrototype
                {
                    Mesh = _fallbackAttachmentMesh,
                    Material = _fallbackCrateMaterial,
                    Weight = 1,
                    UniformScaleRange = new Vector2(0.72f, 1.08f),
                    VerticalOffsetRange = new Vector2(-0.15f, 0.25f),
                    DensityThreshold = 0.62f,
                    WindowThreshold = 0.18f,
                    ShadowCastingMode = ShadowCastingMode.Off
                },
                new NestedAttachmentPrototype
                {
                    Mesh = _fallbackAttachmentMesh,
                    Material = _fallbackScrapMaterial,
                    Weight = 2,
                    UniformScaleRange = new Vector2(0.54f, 0.92f),
                    VerticalOffsetRange = new Vector2(-0.28f, 0.18f),
                    DensityThreshold = 0.56f,
                    WindowThreshold = 0.22f,
                    ShadowCastingMode = ShadowCastingMode.Off
                }
            }; // COLD ALLOC: NestedAttachmentPrototype[2] - runtime fallback nesting archetypes - owner: SargassumGlobalDragManager
        }

        private void ReleaseFallbackNestingResources()
        {
            _activeNestingPrototypes = null;
            _fallbackNestingPrototypes = null;

            if (_fallbackCrateMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackCrateMaterial);
                _fallbackCrateMaterial = null;
            }

            if (_fallbackScrapMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScrapMaterial);
                _fallbackScrapMaterial = null;
            }

            if (_fallbackAttachmentMesh != null)
            {
                DestroyOwnedRuntimeObject(_fallbackAttachmentMesh);
                _fallbackAttachmentMesh = null;
            }
        }

        private void RebuildNestedAttachments()
        {
            EnsureNestedAttachmentStorage();
            ClearNestedAttachments();

            if (!_hasFieldData ||
                _activeNestingPrototypes == null ||
                _activeNestingPrototypes.Length == 0 ||
                _nestedMatricesByPrototype == null ||
                _nestedMatrixCounts == null ||
                _debugFieldBounds.size.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float denseNodeChance = Mathf.Clamp01(denseNodeNestChancePercent * 0.01f);
            if (denseNodeChance <= 0f)
                return;

            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (_activeNestedAttachmentStateCount >= maxNestedAttachmentCount)
                    break;

                long cellKey = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
                float normalizedDensity = Mathf.Clamp01(cell.Density * densityNormalization);
                if (normalizedDensity < nestingNodeDensityThreshold)
                    continue;

                int cellX = (int)(cellKey >> 32);
                int cellZ = (int)cellKey;
                float chance = HashToFloat01((uint)cellX, (uint)cellZ, 0x6A09E667u);
                if (chance > denseNodeChance)
                    continue;

                Vector3 cellCenterWS = new Vector3(
                    (cellX + 0.5f) * cellSize + _globalDriftOffset.x,
                    (cell.MinY + cell.MaxY) * 0.5f + _globalDriftOffset.y,
                    (cellZ + 0.5f) * cellSize + _globalDriftOffset.z);
                if (!SampleDetailedInfluence(cellCenterWS, nestingSampleRadius, 0f, out SargassumFieldSample fieldSample))
                    continue;

                if (fieldSample.Window01 > nestingNodeWindowThreshold)
                    continue;

                int prototypeIndex = ResolveNestedPrototypeIndex(fieldSample, cellX, cellZ);
                if (prototypeIndex < 0)
                    continue;

                NestedAttachmentPrototype prototype = _activeNestingPrototypes[prototypeIndex];
                float minScale = prototype.UniformScaleRange.x > 0f ? prototype.UniformScaleRange.x : 0.72f;
                float maxScale = prototype.UniformScaleRange.y > minScale ? prototype.UniformScaleRange.y : minScale * 1.18f;
                float uniformScale = Mathf.Lerp(
                    Mathf.Max(0.1f, minScale),
                    Mathf.Max(minScale, maxScale),
                    HashToFloat01((uint)cellX, (uint)cellZ, 0x91E10DA5u));
                float verticalOffset = Mathf.Lerp(
                    prototype.VerticalOffsetRange.x,
                    prototype.VerticalOffsetRange.y,
                    HashToFloat01((uint)cellX, (uint)cellZ, 0x52A3F15Du));
                float yaw = HashToFloat01((uint)cellX, (uint)cellZ, 0xD23F4195u) * 360f;
                float pitch = Mathf.Lerp(-10f, 10f, HashToFloat01((uint)cellX, (uint)cellZ, 0x3AF21D8Bu));
                float roll = Mathf.Lerp(-14f, 14f, HashToFloat01((uint)cellX, (uint)cellZ, 0xF13A0BCDu));
                Vector3 sampleSpaceAnchorWS = fieldSample.AnchorWS - _globalDriftOffset;
                sampleSpaceAnchorWS.y = Mathf.Clamp(cellCenterWS.y + verticalOffset - _globalDriftOffset.y, cell.MinY, cell.MaxY);

                _nestedAttachmentStates[_activeNestedAttachmentStateCount] = new NestedAttachmentState
                {
                    SampleSpaceAnchorWS = sampleSpaceAnchorWS,
                    RenderOffsetWS = Vector3.zero,
                    ReleaseVelocityWS = Vector3.zero,
                    Rotation = Quaternion.Euler(pitch, yaw, roll),
                    UniformScale = uniformScale,
                    ReleaseLifetime = nestingReleaseLifetime,
                    ReleaseAge = 0f,
                    PrototypeIndex = prototypeIndex,
                    Released = false
                };
                _activeNestedAttachmentStateCount++;
            }

            _activeNestedPrototypeCount = _activeNestingPrototypes.Length;
            _debugNestedStateCount = _activeNestedAttachmentStateCount;
            UpdateNestedAttachmentBatches(0f);
        }

        private void EnsureNestedAttachmentStorage()
        {
            ResolveActiveNestingPrototypes();
            int prototypeCount = _activeNestingPrototypes != null ? _activeNestingPrototypes.Length : 0;
            int requiredCapacity = Mathf.Clamp(maxNestedAttachmentCount, 4, 128);
            if (_nestedMatricesByPrototype != null &&
                _nestedMatrixCounts != null &&
                _nestedAttachmentStates != null &&
                _nestedMatricesByPrototype.Length == prototypeCount &&
                _nestedPrototypeCapacity == requiredCapacity)
            {
                return;
            }

            _nestedPrototypeCapacity = requiredCapacity;
            // COLD ALLOC: Matrix4x4[prototypeCount][capacity] - persistent instanced nesting matrices by archetype - owner: SargassumGlobalDragManager
            _nestedMatricesByPrototype = new Matrix4x4[prototypeCount][];
            // COLD ALLOC: int[prototypeCount] - per-archetype instanced nesting counts - owner: SargassumGlobalDragManager
            _nestedMatrixCounts = new int[prototypeCount];
            for (int i = 0; i < prototypeCount; i++)
            {
                // COLD ALLOC: Matrix4x4[capacity] - instanced nesting batch storage for one archetype - owner: SargassumGlobalDragManager
                _nestedMatricesByPrototype[i] = new Matrix4x4[requiredCapacity];
            }
            // COLD ALLOC: NestedAttachmentState[capacity] - persistent trapped-debris state for cut-release simulation - owner: SargassumGlobalDragManager
            _nestedAttachmentStates = new NestedAttachmentState[requiredCapacity];
        }

        private void ReleaseNestedAttachmentStorage()
        {
            _nestedMatricesByPrototype = null;
            _nestedMatrixCounts = null;
            _nestedAttachmentStates = null;
            _nestedPrototypeCapacity = 0;
            _activeNestedPrototypeCount = 0;
            _activeNestedAttachmentStateCount = 0;
        }

        internal bool RegisterSettledCollapseChunk(SargassumCollapseChunk chunk)
        {
            if (chunk == null)
                return false;

            EnsureScavengerStorage();

            for (int i = 0; i < _activeScavengerHostCount; i++)
            {
                if (_scavengerHosts[i].Chunk == chunk)
                    return true;
            }

            if (_activeScavengerHostCount >= _scavengerHosts.Length)
                return false;

            int slot = _activeScavengerHostCount;
            _scavengerHosts[slot] = new ScavengerHostState
            {
                Chunk = chunk,
                AnchorWS = chunk.GetScavengerAnchorWS(),
                SettledTime = 0f,
                Consumed01 = 0f,
                Seed = unchecked((uint)EntityId.ToULong(chunk.GetEntityId())) ^ NestingHashSeed
            };
            _activeScavengerHostCount++;
            return true;
        }

        internal void UnregisterSettledCollapseChunk(SargassumCollapseChunk chunk)
        {
            if (chunk == null || _scavengerHosts == null)
                return;

            for (int i = 0; i < _activeScavengerHostCount; i++)
            {
                if (_scavengerHosts[i].Chunk != chunk)
                    continue;

                _scavengerHosts[i].Chunk = null;
                return;
            }
        }

        internal void RegisterExternalScavengerSite(Vector3 anchorWS, float radiusWS, float duration)
        {
            EnsureScavengerStorage();
            if (_externalScavengerSites == null || _externalScavengerSites.Length == 0)
                return;

            float clampedRadius = Mathf.Clamp(radiusWS, 0.2f, scavengerOrbitRadius * 3f);
            float clampedDuration = duration > 0f ? duration : externalScavengerSiteDuration;
            int targetIndex = -1;
            float weakestRemaining = float.MaxValue;

            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                ExternalScavengerSiteState site = _externalScavengerSites[i];
                if (site.RemainingTime <= 0f)
                {
                    targetIndex = i;
                    break;
                }

                float mergeRadius = Mathf.Max(site.RadiusWS, clampedRadius);
                if ((site.AnchorWS - anchorWS).sqrMagnitude <= mergeRadius * mergeRadius)
                {
                    targetIndex = i;
                    break;
                }

                if (site.RemainingTime < weakestRemaining)
                {
                    weakestRemaining = site.RemainingTime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            _externalScavengerSites[targetIndex] = new ExternalScavengerSiteState
            {
                AnchorWS = anchorWS,
                RadiusWS = clampedRadius,
                RemainingTime = clampedDuration,
                Seed = BuildDeterministicAnchorSeed(anchorWS, _globalDriftOffset, (uint)targetIndex * 0x45D9F3Bu)
            };
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            if (_externalScavengerSites == null || _externalScavengerSites.Length == 0)
            {
                data.externalScavengerSites = null;
                return;
            }

            int activeCount = 0;
            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                if (_externalScavengerSites[i].RemainingTime > 0f)
                    activeCount++;
            }

            if (activeCount <= 0)
            {
                data.externalScavengerSites = null;
                return;
            }

            // COLD ALLOC: ExternalScavengerSiteDTO[activeCount] - quantized corpse/carcass site persistence payload - owner: SargassumGlobalDragManager
            ExternalScavengerSiteDTO[] snapshot = new ExternalScavengerSiteDTO[activeCount];
            int writeIndex = 0;
            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                ExternalScavengerSiteState site = _externalScavengerSites[i];
                if (site.RemainingTime <= 0f)
                    continue;

                snapshot[writeIndex++] = QuantizeExternalSite(site);
            }

            data.externalScavengerSites = snapshot;
        }

        public void LoadFromSaveData(SaveData data)
        {
            EnsureScavengerStorage();
            if (_externalScavengerSites == null)
                return;

            Array.Clear(_externalScavengerSites, 0, _externalScavengerSites.Length);
            _activeExternalScavengerSiteCount = 0;

            ExternalScavengerSiteDTO[] snapshot = data != null ? data.externalScavengerSites : null;
            if (snapshot == null || snapshot.Length == 0)
                return;

            int writeIndex = 0;
            int restoreCount = Math.Min(snapshot.Length, _externalScavengerSites.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                ExternalScavengerSiteDTO dto = snapshot[i];
                if (!dto.IsValid)
                    continue;

                _externalScavengerSites[writeIndex++] = DequantizeExternalSite(dto);
            }

            _activeExternalScavengerSiteCount = writeIndex;
        }

        private static ExternalScavengerSiteDTO QuantizeExternalSite(ExternalScavengerSiteState site)
        {
            AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(site.AnchorWS);
            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in anchorAup, ExternalSiteChunkSizeMeters);
            double3 absolutePosition = anchorAup.ToAbsoluteDouble3();
            double3 chunkCenter = new double3(
                (chunkId.x * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (chunkId.y * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (chunkId.z * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d));

            return new ExternalScavengerSiteDTO
            {
                chunkX = chunkId.x,
                chunkY = chunkId.y,
                chunkZ = chunkId.z,
                offsetX = QuantizeChunkOffset(absolutePosition.x - chunkCenter.x),
                offsetY = QuantizeChunkOffset(absolutePosition.y - chunkCenter.y),
                offsetZ = QuantizeChunkOffset(absolutePosition.z - chunkCenter.z),
                quantizedRadius = (byte)math.clamp(math.round(site.RadiusWS / ExternalSiteQuantizationMeters), 1f, byte.MaxValue),
                remainingTime = math.max(0f, site.RemainingTime),
                seed = site.Seed
            };
        }

        private static ExternalScavengerSiteState DequantizeExternalSite(ExternalScavengerSiteDTO dto)
        {
            double3 chunkCenter = new double3(
                (dto.chunkX * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (dto.chunkY * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (dto.chunkZ * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d));

            double3 absolutePosition = new double3(
                chunkCenter.x + (dto.offsetX * ExternalSiteQuantizationMeters),
                chunkCenter.y + (dto.offsetY * ExternalSiteQuantizationMeters),
                chunkCenter.z + (dto.offsetZ * ExternalSiteQuantizationMeters));
            float3 runtimePosition = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition).ToRuntimeFloat3();

            return new ExternalScavengerSiteState
            {
                AnchorWS = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                RadiusWS = math.max(ExternalSiteQuantizationMeters, dto.quantizedRadius * ExternalSiteQuantizationMeters),
                RemainingTime = math.max(0f, dto.remainingTime),
                Seed = dto.seed
            };
        }

        private static sbyte QuantizeChunkOffset(double offsetMeters)
        {
            float quantized = math.round((float)(offsetMeters / ExternalSiteQuantizationMeters));
            return (sbyte)math.clamp(quantized, -127f, 127f);
        }

        private void EnsureScavengerStorage()
        {
            int hostCapacity = Mathf.Clamp(maxScavengerHostCount, 2, 24);
            int externalSiteCapacity = Mathf.Clamp(maxExternalScavengerSiteCount, 1, 16);
            int instanceCapacity = Mathf.Max(1, hostCapacity * Mathf.Clamp(scavengersPerHost, 4, 32));
            if (_scavengerHosts != null &&
                _externalScavengerSites != null &&
                _scavengerMatrices != null &&
                _scavengerHosts.Length == hostCapacity &&
                _externalScavengerSites.Length == externalSiteCapacity &&
                _scavengerInstanceCapacity == instanceCapacity)
            {
                return;
            }

            ReleaseScavengerBuffers();
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;
            // COLD ALLOC: ScavengerHostState[hostCapacity] - tracked settled collapse chunks feeding the scavenger BRG renderer - owner: SargassumGlobalDragManager
            _scavengerHosts = new ScavengerHostState[hostCapacity];
            // COLD ALLOC: ExternalScavengerSiteState[externalSiteCapacity] - corpse/carcass scavenger targets without live chunk owners - owner: SargassumGlobalDragManager
            _externalScavengerSites = new ExternalScavengerSiteState[externalSiteCapacity];
            // COLD ALLOC: Matrix4x4[instanceCapacity] - CPU-side transform payload for bottom scavenger instances - owner: SargassumGlobalDragManager
            _scavengerMatrices = new Matrix4x4[instanceCapacity];
            _scavengerMatricesNative = new NativeArray<Matrix4x4>(instanceCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[instanceCapacity] - Burst-readable scavenger BRG culling payload mirror - owner: SargassumGlobalDragManager
            _scavengerInstanceCapacity = instanceCapacity;
        }

        private void EnsureScavengerRenderResources()
        {
            EnsureScavengerStorage();

            if (scavengerMesh == null && _fallbackScavengerMesh == null)
                _fallbackScavengerMesh = BuildFallbackScavengerMesh();

            if (scavengerMaterial == null && _fallbackScavengerMaterial == null)
            {
                Shader shader = scavengerFallbackShader;
                if (shader != null)
                {
                    _fallbackScavengerMaterial = new Material(shader)
                    {
                        name = "__CollapseScavengerIndirectFallback",
                        hideFlags = HideFlags.HideAndDontSave,
                        enableInstancing = true
                    }; // COLD ALLOC: Material[1] - scavenger fallback material bound to first-party shader - owner: SargassumGlobalDragManager
                }
            }

            if (_scavengerMatrixBuffer == null)
                _scavengerMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(Mathf.Max(1, _scavengerInstanceCapacity)); // COLD ALLOC: GraphicsBuffer[instanceCapacity] - scavenger BRG matrices - owner: SargassumGlobalDragManager

            if (_scavengerBatchRendererGroup == null)
            {
                _scavengerBatchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformScavengerCulling,
                    userContext = IntPtr.Zero
                });

                _scavengerBatchMetadata = new NativeArray<MetadataValue>(0, Allocator.Persistent); // COLD ALLOC: NativeArray<MetadataValue>[0] - BRG metadata placeholder for scavenger scatter - owner: SargassumGlobalDragManager
                _scavengerBatchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for scavenger scatter - owner: SargassumGlobalDragManager
                _scavengerBatchId = _scavengerBatchRendererGroup.AddBatch(_scavengerBatchMetadata, _scavengerBatchHandleBuffer.bufferHandle);
            }
        }

        private void ReleaseScavengerBuffers()
        {
            if (_scavengerMatrixBuffer != null)
            {
                _scavengerMatrixBuffer.Release();
                _scavengerMatrixBuffer = null;
            }

            if (_scavengerMatricesNative.IsCreated)
            {
                _scavengerMatricesNative.Dispose();
                _scavengerMatricesNative = default;
            }
        }

        private void ReleaseScavengerResources()
        {
            ReleaseScavengerBuffers();
            _scavengerHosts = null;
            _externalScavengerSites = null;
            _scavengerMatrices = null;
            _scavengerInstanceCapacity = 0;
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;

            if (_fallbackScavengerMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScavengerMaterial);
                _fallbackScavengerMaterial = null;
            }

            if (_fallbackScavengerMesh != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScavengerMesh);
                _fallbackScavengerMesh = null;
            }

            if (_scavengerBatchRendererGroup != null)
            {
                if (!_scavengerBatchId.Equals(default))
                    _scavengerBatchRendererGroup.RemoveBatch(_scavengerBatchId);
                if (!_scavengerBatchMeshId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMesh(_scavengerBatchMeshId);
                if (!_scavengerBatchMaterialId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMaterial(_scavengerBatchMaterialId);
                _scavengerBatchRendererGroup.Dispose();
                _scavengerBatchRendererGroup = null;
                _scavengerBatchId = default;
                _scavengerBatchMeshId = default;
                _scavengerBatchMaterialId = default;
                _registeredScavengerMesh = null;
                _registeredScavengerMaterial = null;
            }

            if (_scavengerBatchHandleBuffer != null)
            {
                _scavengerBatchHandleBuffer.Release();
                _scavengerBatchHandleBuffer = null;
            }

            if (_scavengerBatchMetadata.IsCreated)
                _scavengerBatchMetadata.Dispose();

            ReleaseScavengerBrgMaterial();
        }

        private void ClearScavengerHosts()
        {
            if (_scavengerHosts != null)
            {
                for (int i = 0; i < _activeScavengerHostCount; i++)
                {
                    SargassumCollapseChunk chunk = _scavengerHosts[i].Chunk;
                    if (chunk != null)
                        chunk.ClearScavengerHostRegistration();
                }

                Array.Clear(_scavengerHosts, 0, _scavengerHosts.Length);
            }

            if (_externalScavengerSites != null)
                Array.Clear(_externalScavengerSites, 0, _externalScavengerSites.Length);

            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;
            _debugScavengerHostCount = 0;
            _debugScavengerInstanceCount = 0;
            _debugScavengerBounds = default;
            _scavengerDrawBounds = default;

        }

        private void UpdateScavengerHosts(float dt)
        {
            if (_scavengerHosts == null || _activeScavengerHostCount <= 0)
            {
                _debugScavengerHostCount = 0;
                _debugScavengerInstanceCount = 0;
                _debugScavengerBounds = default;
                UploadScavengerInstances(0);
                return;
            }

            EnsureScavengerRenderResources();

            int writeHostIndex = 0;
            int matrixCount = 0;
            bool boundsInitialized = false;
            Bounds drawBounds = default;
            float safeDeltaTime = Mathf.Max(0f, dt);

            for (int readHostIndex = 0; readHostIndex < _activeScavengerHostCount; readHostIndex++)
            {
                ScavengerHostState host = _scavengerHosts[readHostIndex];
                if (host.Chunk == null || !host.Chunk.CanHostScavengers)
                    continue;

                host.AnchorWS = host.Chunk.GetScavengerAnchorWS();
                host.SettledTime += safeDeltaTime;
                if (host.SettledTime >= scavengerActivationDelay)
                {
                    float consumeDelta01 = safeDeltaTime / Mathf.Max(0.1f, scavengerConsumeDuration);
                    host.Chunk.ApplyScavengerConsumptionDelta(consumeDelta01);
                    if (host.Chunk == null || !host.Chunk.CanHostScavengers)
                        continue;

                    host.Consumed01 = Mathf.Clamp01(host.Consumed01 + consumeDelta01);
                    int instanceCount = Mathf.Clamp(
                        Mathf.RoundToInt(Mathf.Lerp(scavengersPerHost, 2f, host.Consumed01)),
                        1,
                        scavengersPerHost);
                    float activeTime = host.SettledTime - scavengerActivationDelay;
                    float consumeBias = 1f - host.Consumed01 * 0.35f;

                    for (int instanceIndex = 0; instanceIndex < instanceCount && matrixCount < _scavengerInstanceCapacity; instanceIndex++)
                    {
                        float phase01 = (instanceIndex + 0.5f) / instanceCount;
                        float radialHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 1), 0xA53C91E5u);
                        float scaleHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 7), 0x9E3779B9u);
                        float bobHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 13), 0x7F4A7C15u);
                        float angle = phase01 * Mathf.PI * 2f + activeTime * Mathf.Lerp(0.55f, 1.1f, radialHash);
                        float radius = scavengerOrbitRadius * Mathf.Lerp(0.35f, 1f, radialHash) * consumeBias;
                        Vector3 orbitOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        float bob = Mathf.Sin(activeTime * 4.2f + bobHash * Mathf.PI * 2f) * scavengerBobAmplitude;
                        Vector3 positionWS = host.AnchorWS + orbitOffset + new Vector3(0f, scavengerVerticalOffset + bob, 0f);
                        Vector3 forward = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        if (forward.sqrMagnitude <= 0.0001f)
                            forward = Vector3.forward;

                        float scale = Mathf.Lerp(scavengerScaleMin, scavengerScaleMax, scaleHash);
                        _scavengerMatrices[matrixCount] = Matrix4x4.TRS(positionWS, Quaternion.LookRotation(forward, Vector3.up), Vector3.one * scale);
                        if (_scavengerMatricesNative.IsCreated && matrixCount < _scavengerMatricesNative.Length)
                            _scavengerMatricesNative[matrixCount] = _scavengerMatrices[matrixCount];
                        matrixCount++;

                        Bounds instanceBounds = new Bounds(positionWS, Vector3.one * Mathf.Max(0.2f, scale * 2.8f));
                        if (!boundsInitialized)
                        {
                            drawBounds = instanceBounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            drawBounds.Encapsulate(instanceBounds);
                        }
                    }
                }

                _scavengerHosts[writeHostIndex] = host;
                writeHostIndex++;
            }

            int writeExternalSiteIndex = 0;
            if (_externalScavengerSites != null)
            {
                for (int readExternalSiteIndex = 0; readExternalSiteIndex < _externalScavengerSites.Length; readExternalSiteIndex++)
                {
                    ExternalScavengerSiteState site = _externalScavengerSites[readExternalSiteIndex];
                    if (site.RemainingTime <= 0f)
                        continue;

                    site.RemainingTime = Mathf.Max(0f, site.RemainingTime - safeDeltaTime);
                    if (site.RemainingTime <= 0f)
                        continue;

                    int instanceCount = Mathf.Clamp(scavengersPerHost / 2, 1, scavengersPerHost);
                    float life01 = 1f - site.RemainingTime / Mathf.Max(0.1f, externalScavengerSiteDuration);
                    float activeTime = (externalScavengerSiteDuration - site.RemainingTime) * 0.85f;
                    for (int instanceIndex = 0; instanceIndex < instanceCount && matrixCount < _scavengerInstanceCapacity; instanceIndex++)
                    {
                        float phase01 = (instanceIndex + 0.5f) / instanceCount;
                        float radialHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 3), 0x6D2B79F5u);
                        float scaleHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 11), 0xA53C91E5u);
                        float bobHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 19), 0x9E3779B9u);
                        float angle = phase01 * Mathf.PI * 2f + activeTime * Mathf.Lerp(0.35f, 0.9f, radialHash);
                        float radius = site.RadiusWS * Mathf.Lerp(0.2f, 1f, radialHash) * Mathf.Lerp(1f, 0.55f, life01);
                        Vector3 orbitOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        float bob = Mathf.Sin(activeTime * 3.4f + bobHash * Mathf.PI * 2f) * scavengerBobAmplitude;
                        Vector3 positionWS = site.AnchorWS + orbitOffset + new Vector3(0f, scavengerVerticalOffset + bob, 0f);
                        Vector3 forward = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        if (forward.sqrMagnitude <= 0.0001f)
                            forward = Vector3.forward;

                        float scale = Mathf.Lerp(scavengerScaleMin, scavengerScaleMax, scaleHash) * Mathf.Lerp(0.7f, 1f, 1f - life01);
                        _scavengerMatrices[matrixCount] = Matrix4x4.TRS(positionWS, Quaternion.LookRotation(forward, Vector3.up), Vector3.one * scale);
                        if (_scavengerMatricesNative.IsCreated && matrixCount < _scavengerMatricesNative.Length)
                            _scavengerMatricesNative[matrixCount] = _scavengerMatrices[matrixCount];
                        matrixCount++;

                        Bounds instanceBounds = new Bounds(positionWS, Vector3.one * Mathf.Max(0.2f, scale * 2.8f));
                        if (!boundsInitialized)
                        {
                            drawBounds = instanceBounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            drawBounds.Encapsulate(instanceBounds);
                        }
                    }

                    _externalScavengerSites[writeExternalSiteIndex] = site;
                    writeExternalSiteIndex++;
                }
            }

            for (int clearIndex = writeHostIndex; clearIndex < _activeScavengerHostCount; clearIndex++)
                _scavengerHosts[clearIndex] = default;

            if (_externalScavengerSites != null)
            {
                for (int clearIndex = writeExternalSiteIndex; clearIndex < _externalScavengerSites.Length; clearIndex++)
                    _externalScavengerSites[clearIndex] = default;
            }

            _activeScavengerHostCount = writeHostIndex;
            _activeExternalScavengerSiteCount = writeExternalSiteIndex;
            _debugScavengerHostCount = writeHostIndex + writeExternalSiteIndex;
            _debugScavengerInstanceCount = matrixCount;
            _scavengerDrawBounds = boundsInitialized ? drawBounds : default;
            _debugScavengerBounds = _scavengerDrawBounds;
            UploadScavengerInstances(matrixCount);
        }

        private void UploadScavengerInstances(int instanceCount)
        {
            if (_scavengerMatrixBuffer == null)
                return;
            if (instanceCount > 0)
                GraphicsBufferUploadUtility.UploadArray(_scavengerMatrixBuffer, _scavengerMatrices, instanceCount);
        }

        private void DrawScavengers()
        {
            if (_debugScavengerInstanceCount <= 0 || _scavengerMatrixBuffer == null || _scavengerBatchRendererGroup == null)
                return;

            Mesh activeMesh = scavengerMesh != null ? scavengerMesh : _fallbackScavengerMesh;
            Material activeMaterial = EnsureScavengerBrgMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;

            activeMaterial.SetBuffer(_ScavengerMatricesId, _scavengerMatrixBuffer);
            SyncScavengerBatchRegistration(activeMesh, activeMaterial);
            _scavengerBatchRendererGroup.SetBatchBuffer(_scavengerBatchId, _scavengerMatrixBuffer.bufferHandle);
            _scavengerBatchRendererGroup.SetGlobalBounds(_scavengerDrawBounds);
        }

        private Material EnsureScavengerBrgMaterial()
        {
            Material sourceMaterial = scavengerMaterial != null ? scavengerMaterial : _fallbackScavengerMaterial;
            if (sourceMaterial == null)
                return null;

            if (_scavengerBrgMaterial != null && _scavengerBrgMaterialSource == sourceMaterial)
                return _scavengerBrgMaterial;

            ReleaseScavengerBrgMaterial();
            _scavengerBrgMaterial = new Material(sourceMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "__CollapseScavengerBrgMaterial",
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - BRG-local scavenger material clone for per-renderer buffer binding - owner: SargassumGlobalDragManager
            _scavengerBrgMaterialSource = sourceMaterial;
            _ownsScavengerBrgMaterial = true;
            return _scavengerBrgMaterial;
        }

        private void ReleaseScavengerBrgMaterial()
        {
            if (!_ownsScavengerBrgMaterial || _scavengerBrgMaterial == null)
            {
                _scavengerBrgMaterial = null;
                _scavengerBrgMaterialSource = null;
                _ownsScavengerBrgMaterial = false;
                return;
            }

            DestroyOwnedRuntimeObject(_scavengerBrgMaterial);
            _scavengerBrgMaterial = null;
            _scavengerBrgMaterialSource = null;
            _ownsScavengerBrgMaterial = false;
        }

        private static void DestroyOwnedRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private void SyncScavengerBatchRegistration(Mesh activeMesh, Material activeMaterial)
        {
            if (_scavengerBatchRendererGroup == null)
                return;

            if (_registeredScavengerMesh != activeMesh)
            {
                if (!_scavengerBatchMeshId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMesh(_scavengerBatchMeshId);

                _scavengerBatchMeshId = activeMesh != null ? _scavengerBatchRendererGroup.RegisterMesh(activeMesh) : default;
                _registeredScavengerMesh = activeMesh;
            }

            if (_registeredScavengerMaterial != activeMaterial)
            {
                if (!_scavengerBatchMaterialId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMaterial(_scavengerBatchMaterialId);

                _scavengerBatchMaterialId = activeMaterial != null ? _scavengerBatchRendererGroup.RegisterMaterial(activeMaterial) : default;
                _registeredScavengerMaterial = activeMaterial;
            }
        }

        private JobHandle OnPerformScavengerCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            int instanceCount = _debugScavengerInstanceCount;
            if (instanceCount <= 0 ||
                _scavengerBatchId.Equals(default) ||
                _scavengerBatchMeshId.Equals(default) ||
                _scavengerBatchMaterialId.Equals(default))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, _scavengerDrawBounds))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            bool canCullPerInstance = _scavengerMatricesNative.IsCreated && _scavengerMatricesNative.Length >= instanceCount;
            NativeArray<byte> visibilityMask = new NativeArray<byte>(instanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float4> cullingPlanes = default;
            int planeCount = 0;
            if (canCullPerInstance)
            {
                planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                if (planeCount > 0)
                {
                    cullingPlanes = new NativeArray<float4>(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes[planeIndex] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    }
                }
            }

            unsafe
            {
                BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(instanceCount, 1, 1);
                JobHandle visibilityHandle = new HectonBatchRendererGroupUtility.BuildMatrixVisibilityMaskJob
                {
                    Matrices = _scavengerMatricesNative,
                    CullingPlanes = cullingPlanes,
                    VisibilityMask = visibilityMask,
                    InstanceCount = instanceCount,
                    PlaneCount = planeCount,
                    EnableCpuCulling = canCullPerInstance,
                    GlobalOffset = float3.zero,
                    RadiusScale = 1.5f,
                    MinRadius = 0.25f
                }.Schedule(instanceCount, 64);

                JobHandle finalizeHandle = new HectonBatchRendererGroupUtility.FinalizeSingleDrawCommandOutputJob
                {
                    VisibilityMask = visibilityMask,
                    InstanceCount = instanceCount,
                    BatchId = _scavengerBatchId,
                    MeshId = _scavengerBatchMeshId,
                    MaterialId = _scavengerBatchMaterialId,
                    Layer = gameObject.layer,
                    SubMeshIndex = 0,
                    ShadowCastingMode = ShadowCastingMode.Off,
                    ReceiveShadows = false,
                    MotionMode = MotionVectorGenerationMode.Camera,
                    VisibleInstances = output.visibleInstances,
                    DrawCommands = output.drawCommands,
                    DrawRanges = output.drawRanges,
                    OutputCommands = HectonBatchRendererGroupUtility.GetDirectDrawOutputPointer(cullingOutput)
                }.Schedule(visibilityHandle);

                JobHandle disposeHandle = visibilityMask.Dispose(finalizeHandle);
                if (cullingPlanes.IsCreated)
                    disposeHandle = cullingPlanes.Dispose(disposeHandle);

                return disposeHandle;
            }
        }

        private Mesh BuildFallbackScavengerMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "__CollapseScavengerFallback",
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Mesh[1] - first-party fallback scavenger mesh - owner: SargassumGlobalDragManager

            Vector3[] vertices =
            {
                new Vector3(0f, 0f, 0.28f),
                new Vector3(0f, 0f, -0.28f),
                new Vector3(-0.16f, 0f, 0f),
                new Vector3(0.16f, 0f, 0f),
                new Vector3(0f, 0.08f, 0f),
                new Vector3(0f, -0.05f, 0f)
            }; // COLD ALLOC: Vector3[6] - fallback scavenger octahedron vertices - owner: SargassumGlobalDragManager

            int[] triangles =
            {
                0, 4, 3,
                0, 3, 5,
                0, 5, 2,
                0, 2, 4,
                1, 3, 4,
                1, 5, 3,
                1, 2, 5,
                1, 4, 2
            }; // COLD ALLOC: int[24] - fallback scavenger octahedron triangles - owner: SargassumGlobalDragManager

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private int ResolveNestedPrototypeIndex(SargassumFieldSample fieldSample, int sampleX, int sampleZ)
        {
            if (_activeNestingPrototypes == null || _activeNestingPrototypes.Length == 0)
                return -1;

            int totalWeight = 0;
            for (int i = 0; i < _activeNestingPrototypes.Length; i++)
            {
                NestedAttachmentPrototype prototype = _activeNestingPrototypes[i];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                float densityThreshold = prototype.DensityThreshold > 0f ? Mathf.Clamp01(prototype.DensityThreshold) : 0.56f;
                float windowThreshold = prototype.WindowThreshold > 0f ? Mathf.Clamp01(prototype.WindowThreshold) : 0.18f;
                if (fieldSample.Density01 < densityThreshold)
                    continue;

                if (fieldSample.Window01 > windowThreshold)
                    continue;

                totalWeight += Mathf.Max(1, prototype.Weight);
            }

            if (totalWeight <= 0)
                return -1;

            float pick = HashToFloat01((uint)sampleX, (uint)sampleZ, 0x64A8B875u) * totalWeight;
            float cursor = 0f;
            for (int i = 0; i < _activeNestingPrototypes.Length; i++)
            {
                NestedAttachmentPrototype prototype = _activeNestingPrototypes[i];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                float densityThreshold = prototype.DensityThreshold > 0f ? Mathf.Clamp01(prototype.DensityThreshold) : 0.56f;
                float windowThreshold = prototype.WindowThreshold > 0f ? Mathf.Clamp01(prototype.WindowThreshold) : 0.18f;
                if (fieldSample.Density01 < densityThreshold)
                    continue;

                if (fieldSample.Window01 > windowThreshold)
                    continue;

                cursor += Mathf.Max(1, prototype.Weight);
                if (pick <= cursor)
                    return i;
            }

            return -1;
        }

        private void UpdateNestedAttachmentBatches(float dt)
        {
            if (_nestedAttachmentStates == null || _nestedMatrixCounts == null || _nestedMatricesByPrototype == null)
                return;

            for (int i = 0; i < _nestedMatrixCounts.Length; i++)
                _nestedMatrixCounts[i] = 0;

            SargassumCutManager cutManager = SargassumCutManager.Instance;
            bool boundsInitialized = false;
            Bounds drawBounds = default;
            int totalVisible = 0;

            for (int stateIndex = 0; stateIndex < _activeNestedAttachmentStateCount; stateIndex++)
            {
                NestedAttachmentState state = _nestedAttachmentStates[stateIndex];
                if ((uint)state.PrototypeIndex >= (uint)_activeNestingPrototypes.Length)
                    continue;

                DisruptionSample disruption = SampleDisruptionNoDrift(state.SampleSpaceAnchorWS);
                Vector3 worldPosition = state.SampleSpaceAnchorWS + _globalDriftOffset + state.RenderOffsetWS;
                if (!state.Released && disruption.SinkDepthWS > 0f)
                    worldPosition.y -= disruption.SinkDepthWS;
                if (!state.Released &&
                    cutManager != null &&
                    cutManager.SampleRecentCut01(worldPosition, nestingCutReleaseRadius, out float cut01) &&
                    cut01 >= nestingCutReleaseThreshold)
                {
                    Vector3 releaseDirection = new Vector3(
                        Mathf.Lerp(-1f, 1f, HashToFloat01((uint)stateIndex, 0xB5C0FBCFu, 0x51ED270Bu)),
                        0f,
                        Mathf.Lerp(-1f, 1f, HashToFloat01((uint)stateIndex, 0x9E3779B9u, 0xA54FF53Au)));
                    if (releaseDirection.sqrMagnitude <= 0.0001f)
                        releaseDirection = Vector3.right;

                    releaseDirection.Normalize();
                    state.Released = true;
                    state.ReleaseAge = 0f;
                    state.ReleaseVelocityWS = new Vector3(
                        releaseDirection.x * nestingReleaseHorizontalSpeed,
                        -nestingReleaseDownwardSpeed,
                        releaseDirection.z * nestingReleaseHorizontalSpeed);
                }

                if (state.Released)
                {
                    state.ReleaseAge += Mathf.Max(0f, dt);
                    if (state.ReleaseAge >= state.ReleaseLifetime)
                    {
                        _nestedAttachmentStates[stateIndex] = state;
                        continue;
                    }

                    state.ReleaseVelocityWS.y -= nestingReleaseGravity * Mathf.Max(0f, dt);
                    state.RenderOffsetWS += state.ReleaseVelocityWS * Mathf.Max(0f, dt);
                    worldPosition = state.SampleSpaceAnchorWS + _globalDriftOffset + state.RenderOffsetWS;
                }

                int prototypeIndex = state.PrototypeIndex;
                int matrixCount = _nestedMatrixCounts[prototypeIndex];
                if (matrixCount >= _nestedMatricesByPrototype[prototypeIndex].Length)
                {
                    _nestedAttachmentStates[stateIndex] = state;
                    continue;
                }

                Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, state.Rotation, Vector3.one * state.UniformScale);
                _nestedMatricesByPrototype[prototypeIndex][matrixCount] = matrix;
                _nestedMatrixCounts[prototypeIndex] = matrixCount + 1;
                totalVisible++;

                Bounds instanceBounds = new Bounds(worldPosition, Vector3.one * Mathf.Max(1f, state.UniformScale * 2.6f));
                if (!boundsInitialized)
                {
                    drawBounds = instanceBounds;
                    boundsInitialized = true;
                }
                else
                {
                    drawBounds.Encapsulate(instanceBounds);
                }

                _nestedAttachmentStates[stateIndex] = state;
            }

            _debugNestedAttachmentCount = totalVisible;
            _debugNestedAttachmentBounds = boundsInitialized ? drawBounds : default;
        }

        private void OnValidate()
        {
            _editorValidateDepth++;
            if (_editorValidateDepth > MaxEditorValidateDepth)
            {
                _editorValidateDepth--;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("SargassumGlobalDragManager editor validation watchdog tripped.", this);
#endif
                return;
            }

            try
            {
#if UNITY_EDITOR
                if (collapseChunkPrefab == null)
                    collapseChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab");
                if (scavengerFallbackShader == null)
                    scavengerFallbackShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/Hecton_CollapseScavengerIndirect.shader");
#endif
                SanitizeSettings();
                if (!Application.isPlaying)
                {
                    _activeNestingPrototypes = nestingPrototypes != null && nestingPrototypes.Length > 0
                        ? nestingPrototypes
                        : null;
                    _activeNestedPrototypeCount = 0;
                    _activeNestedAttachmentStateCount = 0;
                    _activeScavengerHostCount = 0;
                    _activeExternalScavengerSiteCount = 0;
                    _debugNestedAttachmentCount = 0;
                    _debugNestedStateCount = 0;
                    _debugActiveDisruptionZones = 0;
                    return;
                }

                ResolveActiveNestingPrototypes();
                EnsureNestedAttachmentStorage();
                EnsureDisruptionStorage();
                EnsureScavengerStorage();
                CreateDensityTexture();
                ClearNestedAttachments();
                ClearScavengerHosts();
                ClearDensityTexture();
                ClearSinkTexture();
                PublishShaderGlobals();
            }
            finally
            {
                _editorValidateDepth--;
            }
        }

        private Shader ResolveFallbackLitShader()
        {
            if (_fallbackLitShader != null)
                return _fallbackLitShader;

            RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Material defaultMaterial = renderPipeline != null ? renderPipeline.defaultMaterial : null;
            _fallbackLitShader = defaultMaterial != null ? defaultMaterial.shader : null;
            return _fallbackLitShader;
        }

        private void TryRegister()
        {
            if (Application.isPlaying && !_saveRegistered)
            {
                Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
                _saveRegistered = true;
            }

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_saveRegistered)
            {
                Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
                _saveRegistered = false;
            }
        }

        private static float ExtractUniformScale(Matrix4x4 matrix)
        {
            Vector3 axisX = new Vector3(matrix.m00, matrix.m10, matrix.m20);
            Vector3 axisY = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            Vector3 axisZ = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            return Mathf.Max(0.1f, Mathf.Max(axisX.magnitude, Mathf.Max(axisY.magnitude, axisZ.magnitude)));
        }

        private static long PackCellKey(int cellX, int cellZ)
        {
            return ((long)cellX << 32) ^ (uint)cellZ;
        }

        private void SanitizeSettings()
        {
            cellSize = Mathf.Max(2f, cellSize);
            _inverseCellSize = 1f / cellSize;
            densityTextureResolution = Mathf.Clamp(densityTextureResolution, 64, 256);
            densityTextureBoundsPadding = Mathf.Max(0f, densityTextureBoundsPadding);
            maxNestedAttachmentCount = Mathf.Clamp(maxNestedAttachmentCount, 4, 128);
            maxNestedAttachmentAttempts = Mathf.Clamp(maxNestedAttachmentAttempts, 4, 24);
            denseNodeNestChancePercent = Mathf.Clamp(denseNodeNestChancePercent, 0f, 5f);
            nestingSampleRadius = Mathf.Clamp(nestingSampleRadius, 0.1f, 2.5f);
            nestingNodeDensityThreshold = Mathf.Clamp01(nestingNodeDensityThreshold);
            nestingNodeWindowThreshold = Mathf.Clamp01(nestingNodeWindowThreshold);
            nestingCutReleaseRadius = Mathf.Clamp(nestingCutReleaseRadius, 0.1f, 4f);
            nestingCutReleaseThreshold = Mathf.Clamp01(nestingCutReleaseThreshold);
            nestingReleaseLifetime = Mathf.Clamp(nestingReleaseLifetime, 0.2f, 8f);
            nestingReleaseHorizontalSpeed = Mathf.Clamp(nestingReleaseHorizontalSpeed, 0f, 6f);
            nestingReleaseDownwardSpeed = Mathf.Clamp(nestingReleaseDownwardSpeed, 0f, 4f);
            nestingReleaseGravity = Mathf.Clamp(nestingReleaseGravity, 0f, 4f);
            maxDisruptionZoneCount = Mathf.Clamp(maxDisruptionZoneCount, 1, MaxDisruptionZoneCapacity);
            buoyancyCollapseQueryRadius = Mathf.Clamp(buoyancyCollapseQueryRadius, 2f, 18f);
            buoyancyCollapseAreaThreshold = Mathf.Max(1f, buoyancyCollapseAreaThreshold);
            buoyancyCollapseCutStrengthThreshold = Mathf.Clamp01(buoyancyCollapseCutStrengthThreshold);
            buoyancyCollapseRadius = Mathf.Clamp(buoyancyCollapseRadius, 2f, 18f);
            buoyancyCollapseSinkDuration = Mathf.Clamp(buoyancyCollapseSinkDuration, 10f, 20f);
            buoyancyCollapseSinkDepth = Mathf.Clamp(buoyancyCollapseSinkDepth, 1f, 24f);
            collapseChunkAreaThresholdMultiplier = Mathf.Clamp(collapseChunkAreaThresholdMultiplier, 1f, 4f);
            collapseChunkSpawnCount = Mathf.Clamp(collapseChunkSpawnCount, 1, 6);
            collapseChunkSpawnRadius = Mathf.Clamp(collapseChunkSpawnRadius, 0.5f, 8f);
            collapseChunkHorizontalSpeed = Mathf.Clamp(collapseChunkHorizontalSpeed, 0f, 8f);
            collapseChunkDownwardSpeed = Mathf.Clamp(collapseChunkDownwardSpeed, 0f, 8f);
            collapseChunkLifetime = Mathf.Clamp(collapseChunkLifetime, 0.5f, 24f);
            collapseChunkScaleMin = Mathf.Clamp(collapseChunkScaleMin, 0.25f, 3f);
            collapseChunkScaleMax = Mathf.Max(collapseChunkScaleMin, Mathf.Clamp(collapseChunkScaleMax, 0.5f, 4f));
            collapseCascadeImpactThreshold = Mathf.Clamp(collapseCascadeImpactThreshold, 0.5f, 12f);
            collapseCascadeMaxDepth = Mathf.Clamp(collapseCascadeMaxDepth, 1, 3);
            collapseCascadeSpawnCount = Mathf.Clamp(collapseCascadeSpawnCount, 1, 4);
            collapseCascadeScaleMultiplier = Mathf.Clamp(collapseCascadeScaleMultiplier, 0.2f, 1f);
            collapseCascadeVelocityMultiplier = Mathf.Clamp(collapseCascadeVelocityMultiplier, 0.5f, 2f);
            massiveDisplacementCutStrength = Mathf.Clamp(massiveDisplacementCutStrength, 0.1f, 2f);
            massiveDisplacementRampDuration = Mathf.Clamp(massiveDisplacementRampDuration, 0.1f, 4f);
            massiveDisplacementFadeDuration = Mathf.Clamp(massiveDisplacementFadeDuration, 0.1f, 8f);
            massiveDisplacementExtremePanicRadius = Mathf.Clamp(massiveDisplacementExtremePanicRadius, 50f, 96f);
            maxScavengerHostCount = Mathf.Clamp(maxScavengerHostCount, 2, 24);
            scavengersPerHost = Mathf.Clamp(scavengersPerHost, 4, 32);
            scavengerActivationDelay = Mathf.Clamp(scavengerActivationDelay, 30f, 90f);
            scavengerConsumeDuration = Mathf.Clamp(scavengerConsumeDuration, 4f, 30f);
            scavengerOrbitRadius = Mathf.Clamp(scavengerOrbitRadius, 0.2f, 4f);
            scavengerVerticalOffset = Mathf.Clamp(scavengerVerticalOffset, -1f, 1f);
            scavengerBobAmplitude = Mathf.Clamp(scavengerBobAmplitude, 0f, 0.5f);
            scavengerScaleMin = Mathf.Clamp(scavengerScaleMin, 0.02f, 0.4f);
            scavengerScaleMax = Mathf.Max(scavengerScaleMin, Mathf.Clamp(scavengerScaleMax, 0.04f, 0.6f));
            maxExternalScavengerSiteCount = Mathf.Clamp(maxExternalScavengerSiteCount, 1, 16);
            externalScavengerSiteDuration = Mathf.Clamp(externalScavengerSiteDuration, 4f, 90f);
            fallbackPatchThreshold = Mathf.Clamp01(fallbackPatchThreshold);
            canopyFlowAnisotropy = Mathf.Clamp(canopyFlowAnisotropy, 0.2f, 1f);
            canopyPrimaryCellSize = Mathf.Max(4f, canopyPrimaryCellSize);
            canopySecondaryCellSize = Mathf.Max(4f, canopySecondaryCellSize);
            canopyWallWidth = Mathf.Max(0.25f, canopyWallWidth);
            canopyWarpMeters = Mathf.Max(0f, canopyWarpMeters);
            canopyWarpNoiseScale = Mathf.Max(0.0001f, canopyWarpNoiseScale);
            entanglementMinDensity = Mathf.Clamp01(entanglementMinDensity);
            entanglementSpeedThreshold = Mathf.Max(0.25f, entanglementSpeedThreshold);
            maxEntanglementStrength = Mathf.Clamp01(maxEntanglementStrength);
            alongFlowDragScale = Mathf.Clamp(alongFlowDragScale, 0.1f, 1f);
            _fallbackLabyrinthConfig = new HectonMapMagicVegetationBridge.FloatingLabyrinthConfig(
                fallbackPatchThreshold,
                canopyWarpNoiseScale,
                canopyPrimaryCellSize,
                canopySecondaryCellSize,
                canopyWallWidth,
                canopyWarpMeters,
                NormalizeFlowDirection(canopyFlowDirection),
                canopyFlowAnisotropy);
        }

        private float ResolveDirectionalDragScale(Vector3 movementVelocityWS)
        {
            Vector2 movementXZ = new Vector2(movementVelocityWS.x, movementVelocityWS.z);
            float movementMagnitudeSq = movementXZ.sqrMagnitude;
            if (movementMagnitudeSq <= 0.0001f)
                return 1f;

            HectonMapMagicVegetationBridge.FloatingLabyrinthConfig config = ResolveLabyrinthConfig();
            Vector2 flowDirection = NormalizeFlowDirection(config.FlowDirection);
            if (flowDirection.sqrMagnitude <= 0.0001f)
                return 1f;

            movementXZ /= Mathf.Sqrt(movementMagnitudeSq);
            float alongFlow01 = Mathf.Abs(Vector2.Dot(movementXZ, flowDirection));
            float crossFlow01 = 1f - alongFlow01;
            float anisotropy = Mathf.Clamp01(config.FlowAnisotropy);
            float directionalBlend = Mathf.Lerp(1f, crossFlow01, anisotropy);
            return Mathf.Lerp(alongFlowDragScale, 1f, directionalBlend);
        }

        private float ResolveEntanglement01(float density01, float currentSpeed)
        {
            float densityGate = Mathf.InverseLerp(entanglementMinDensity, 1f, density01);
            if (densityGate <= 0f)
                return 0f;

            float speedGate = 1f - Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, entanglementSpeedThreshold));
            if (speedGate <= 0f)
                return 0f;

            return densityGate * speedGate * maxEntanglementStrength;
        }

        private float EvaluateCanopyWindow01(Vector3 sampledPositionWS)
        {
            float occupancy = EvaluateCanopyOccupancy(sampledPositionWS.x, sampledPositionWS.z);
            return Mathf.Clamp01(1f - occupancy);
        }

        private float EvaluateCanopyOccupancy(float worldX, float worldZ)
        {
            HectonMapMagicVegetationBridge.FloatingLabyrinthConfig config = ResolveLabyrinthConfig();
            HectonMapMagicVegetationBridge.EvaluateFloatingLabyrinth(
                worldX,
                worldZ,
                HectonVegetationConstants.FloatingLabyrinthSamplingSeed,
                config,
                out float occupancy);
            return Mathf.Clamp01(occupancy);
        }

        private HectonMapMagicVegetationBridge.FloatingLabyrinthConfig ResolveLabyrinthConfig()
        {
            if (mapMagicVegetationBridge != null)
                return mapMagicVegetationBridge.ActiveFloatingLabyrinthConfig;

            return _fallbackLabyrinthConfig;
        }

        private static Vector2 NormalizeFlowDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector2.right;

            direction.Normalize();
            return direction;
        }

        private void CreateDensityTexture()
        {
            int resolution = Mathf.Clamp(densityTextureResolution, 64, 256);
            int texelCount = resolution * resolution;

            if (_densityTextureRaw == null || _densityTextureRaw.Length != texelCount)
            {
                // COLD ALLOC: byte[resolution*resolution] - CPU density texture staging buffer - owner: SargassumGlobalDragManager
                _densityTextureRaw = new byte[texelCount];
            }

            if (_sinkTextureRaw == null || _sinkTextureRaw.Length != texelCount)
            {
                // COLD ALLOC: byte[resolution*resolution] - CPU buoyancy sink texture staging buffer - owner: SargassumGlobalDragManager
                _sinkTextureRaw = new byte[texelCount];
            }

            if (_densityFieldTexture == null ||
                _densityFieldTexture.width != resolution ||
                _densityFieldTexture.height != resolution)
            {
                ReleaseDensityTexture();
                _densityFieldTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
                {
                    name = "__SargassumDensityField",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - baked sargassum density map - owner: SargassumGlobalDragManager
            }

            if (_sinkFieldTexture == null ||
                _sinkFieldTexture.width != resolution ||
                _sinkFieldTexture.height != resolution)
            {
                ReleaseSinkTexture();
                _sinkFieldTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
                {
                    name = "__SargassumBuoyancySink",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - baked sargassum buoyancy sink map - owner: SargassumGlobalDragManager
            }
        }

        private void ReleaseDensityTexture()
        {
            if (_densityFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_densityFieldTexture);
            _densityFieldTexture = null;
        }

        private void ReleaseSinkTexture()
        {
            if (_sinkFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_sinkFieldTexture);
            _sinkFieldTexture = null;
        }

        private void ClearDensityTexture()
        {
            if (_densityFieldTexture == null || _densityTextureRaw == null)
                return;

            Array.Clear(_densityTextureRaw, 0, _densityTextureRaw.Length);
            _densityFieldTexture.LoadRawTextureData(_densityTextureRaw);
            _densityFieldTexture.Apply(false, false);
        }

        private void ClearSinkTexture()
        {
            if (_sinkFieldTexture == null || _sinkTextureRaw == null)
                return;

            Array.Clear(_sinkTextureRaw, 0, _sinkTextureRaw.Length);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _sinkFieldTexture.Apply(false, false);
        }

        private void RefreshDynamicTextures(bool incrementRevision)
        {
            CreateDensityTexture();
            if (_densityFieldTexture == null ||
                _densityTextureRaw == null ||
                _sinkFieldTexture == null ||
                _sinkTextureRaw == null)
            {
                return;
            }

            if (incrementRevision)
                _fieldRevision++;

            if (!_hasFieldData || _debugFieldBounds.size.sqrMagnitude <= 0.0001f)
            {
                _densityFieldWorldRect = Vector4.zero;
                _debugDensityFieldWorldRect = Vector4.zero;
                ClearDensityTexture();
                ClearSinkTexture();
                _debugMaxSinkDepth = 0f;
                return;
            }

            Vector3 boundsCenter = _debugFieldBounds.center;
            float halfExtent = Mathf.Max(_debugFieldBounds.extents.x, _debugFieldBounds.extents.z) + densityTextureBoundsPadding;
            float worldSize = Mathf.Max(halfExtent * 2f, cellSize * 2f);
            float minX = boundsCenter.x - halfExtent;
            float minZ = boundsCenter.z - halfExtent;
            float texelWorldSize = worldSize / _densityFieldTexture.width;
            float sampleRadius = Mathf.Max(texelWorldSize * 0.65f, cellSize * 0.35f);
            float sampleY = Mathf.Clamp(boundsCenter.y, _debugFieldBounds.min.y, _debugFieldBounds.max.y);

            _densityFieldWorldRect = new Vector4(
                minX,
                minZ,
                1f / Mathf.Max(worldSize, 0.001f),
                1f / Mathf.Max(worldSize, 0.001f));
            _debugDensityFieldWorldRect = _densityFieldWorldRect;

            int resolution = _densityFieldTexture.width;
            float maxSinkDepthWS = ResolveMaximumSinkDepthWS();
            for (int y = 0; y < resolution; y++)
            {
                float worldZ = minZ + (y + 0.5f) * texelWorldSize;
                int rowStart = y * resolution;

                for (int x = 0; x < resolution; x++)
                {
                    float worldX = minX + (x + 0.5f) * texelWorldSize;
                    Vector3 sampledPositionWS = new Vector3(worldX, sampleY, worldZ);
                    float density01 = SampleDensity01NoDrift(sampledPositionWS, sampleRadius);
                    float occupancy01 = EvaluateCanopyOccupancy(worldX, worldZ);
                    DisruptionSample disruption = SampleDisruptionNoDrift(sampledPositionWS);
                    float encodedDensity01 = Mathf.Max(density01, occupancy01) * (1f - disruption.Suppression01);
                    _densityTextureRaw[rowStart + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(encodedDensity01 * 255f), 0, 255);
                    float encodedSink01 = maxSinkDepthWS > 0f ? Mathf.Clamp01(disruption.SinkDepthWS / maxSinkDepthWS) : 0f;
                    _sinkTextureRaw[rowStart + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(encodedSink01 * 255f), 0, 255);
                }
            }

            _densityFieldTexture.LoadRawTextureData(_densityTextureRaw);
            _densityFieldTexture.Apply(false, false);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _sinkFieldTexture.Apply(false, false);
            _debugMaxSinkDepth = maxSinkDepthWS;
        }

        private float SampleDensity01NoDrift(Vector3 sampledPositionWS, float radius)
        {
            if (!_hasFieldData || _densityCells.Count == 0)
                return 0f;

            float effectiveRadius = Mathf.Max(0.1f, radius);
            int minCellX = Mathf.FloorToInt((sampledPositionWS.x - effectiveRadius) * _inverseCellSize);
            int maxCellX = Mathf.FloorToInt((sampledPositionWS.x + effectiveRadius) * _inverseCellSize);
            int minCellZ = Mathf.FloorToInt((sampledPositionWS.z - effectiveRadius) * _inverseCellSize);
            int maxCellZ = Mathf.FloorToInt((sampledPositionWS.z + effectiveRadius) * _inverseCellSize);
            float accumulatedDensity = 0f;

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long key = PackCellKey(cellX, cellZ);
                    if (!_densityCells.TryGetValue(key, out CellData cell))
                        continue;

                    if (sampledPositionWS.y + effectiveRadius < cell.MinY || sampledPositionWS.y - effectiveRadius > cell.MaxY)
                        continue;

                    float cellCenterX = (cellX + 0.5f) * cellSize;
                    float cellCenterZ = (cellZ + 0.5f) * cellSize;
                    float planarDistance = Vector2.Distance(
                        new Vector2(sampledPositionWS.x, sampledPositionWS.z),
                        new Vector2(cellCenterX, cellCenterZ));
                    float planarWeight = 1f - planarDistance / Mathf.Max(cellSize + effectiveRadius, 0.001f);
                    if (planarWeight <= 0f)
                        continue;

                    accumulatedDensity += cell.Density * Mathf.Pow(planarWeight, Mathf.Max(1f, distancePower));
                }
            }

            return Mathf.Clamp01(accumulatedDensity * densityNormalization);
        }

        private float ResolveMaximumSinkDepthWS()
        {
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return 0f;

            float maxSinkDepthWS = 0f;
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                if (_disruptionZones[i].SinkDepthWS > maxSinkDepthWS)
                    maxSinkDepthWS = _disruptionZones[i].SinkDepthWS;
            }

            return maxSinkDepthWS;
        }

        private static Mesh BuildFallbackAttachmentMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "__SargassumNestFallbackMesh"
            }; // COLD ALLOC: Mesh[1] - runtime fallback instanced nesting geometry - owner: SargassumGlobalDragManager

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f)
            };

            int[] triangles =
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
            };

            Vector3[] normals =
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down
            };

            Vector2[] uv =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        private static void WriteSafeVelocities(Rigidbody body, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (body == null)
                return;

            body.linearVelocity = HectonPlayerMotor.SafeVelocity(linearVelocity, body.linearVelocity);
            body.angularVelocity = HectonPlayerMotor.SafeVelocity(angularVelocity, body.angularVelocity);
        }

        private static uint BuildDeterministicAnchorSeed(Vector3 anchorWS, Vector3 driftOffset, uint salt)
        {
            Vector3 sampleSpaceAnchorWS = anchorWS - driftOffset;
            int quantizedX = Mathf.RoundToInt(sampleSpaceAnchorWS.x * 100f);
            int quantizedY = Mathf.RoundToInt(sampleSpaceAnchorWS.y * 100f);
            int quantizedZ = Mathf.RoundToInt(sampleSpaceAnchorWS.z * 100f);
            uint hash = salt ^ NestingHashSeed;
            hash = MixSeed(hash, unchecked((uint)quantizedX));
            hash = MixSeed(hash, unchecked((uint)quantizedY));
            hash = MixSeed(hash, unchecked((uint)quantizedZ));
            return hash != 0u ? hash : NestingHashSeed;
        }

        private static uint MixSeed(uint hash, uint value)
        {
            return hash ^ (value + 0x9E3779B9u + (hash << 6) + (hash >> 2));
        }

        private static float HashToFloat01(uint index, uint iteration, uint salt)
        {
            uint value = index * 374761393u + iteration * 668265263u + salt + NestingHashSeed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
