using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class WorldProceduralFieldSampler : MonoBehaviour, IBiomeMatrixEventListener, IBiomePhysicsInfluenceReadModel, IGlobalRegistryHotSwapListener, IMapMagicTerrainTileEventListener
    {
        private const string PatternLabelSedimentResources = "SedimentResources";
        private const string PatternLabelFertileShallows = "FertileShallows";
        private const string PatternLabelReefNavigation = "ReefNavigation";
        private const string PatternLabelIndustrialService = "IndustrialService";
        private const string PatternLabelBrineToxic = "BrineToxic";
        private const string PatternLabelVolcanicPressure = "VolcanicPressure";
        private const string PatternLabelRiftHazard = "RiftHazard";
        private const string PatternLabelAbyssSparse = "AbyssSparse";
        private const string PatternLabelLandmarkCorridor = "LandmarkCorridor";
        private const string PatternLabelNone = "None";
        private const string PatternLabelFallbackOnly = "FallbackOnly";
        private const string MatrixLabelFallbackOnly = "FallbackOnly";
        private const string SeafloorSourceNoneLabel = "None";
        private const string SeafloorSourceMapMagicLabel = "MapMagicHeight";
        private const string SeafloorSourceTerrainProviderLabel = "TerrainProviderHeight";
        private const string SeafloorSourceSceneProbeLegacyLabel = "SceneProbeLegacy";
        private const string SeafloorSourceMacroGeologyLabel = "MacroGeologyFallback";
        private const string SeafloorSourceFallbackLabel = "FallbackSynthetic";
        private const int MaxSeafloorHeightCacheEntries = 4096;
        private const int MaxSeafloorHeightCacheMask = MaxSeafloorHeightCacheEntries - 1;
        private const int NoiseLookupResolution = 512;
        private const int NoiseLookupMask = NoiseLookupResolution - 1;
        private const int MaxBiomeInfluenceGridCellsMx350 = 4096;
        private const int MaxZoneAnchorSnapshotCount = 64;
        private const int MaxBiomeMatrixBakeCount = 160;
        private const int MaxBiomeFamilyBakeCount = 48;
        private const int MaxCaveEntranceHintBakeCount = 64;
        private const string NativeMemoryOwner = nameof(WorldProceduralFieldSampler);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const NativeAllocationLifetime NativeMemoryTempJobLifetime = NativeAllocationLifetime.TempJob;
        private const SystemID OwnerSystemId = SystemID.WorldProceduralFieldSampler;
        private const int EmptyNativeArrayCapacity = 1;
        private const float NoiseLookupValueScale = 1f / ushort.MaxValue;
        private const float DefaultWaterSurfaceLevelY = 14.02f;
        private const uint SamplingJobPinZones = 1u << 0;
        private const uint SamplingJobPinBiomeMatrices = 1u << 1;
        private const uint SamplingJobPinBiomeMatrixIndex = 1u << 2;
        private const uint SamplingJobPinBiomeFamilies = 1u << 3;
        private const uint SamplingJobPinCaveEntranceHints = 1u << 4;
        private const uint SamplingJobPinNoiseLookup = 1u << 5;
        private static readonly uint _biomeInfluenceGridCapacityWarningHash =
            unchecked((uint)LocHash.Compute("WorldProceduralFieldSampler.BiomeInfluenceGridCapacity"));
        private static readonly uint _fieldSamplerTelemetryContextHash =
            unchecked((uint)LocHash.Compute("WorldProceduralFieldSampler"));
        private GraphicsBuffer _biomeInfluenceGraphicsBufferA;
        private GraphicsBuffer _biomeInfluenceGraphicsBufferB;
        private GraphicsBuffer _activeBiomeInfluenceGraphicsBuffer;
        private int _biomeInfluenceGraphicsBufferWriteIndex;
        private int _biomeInfluenceGraphicsBufferCapacity;
#if UNITY_EDITOR
        private static bool _assemblyReloadHookRegistered;
#endif

        public enum SeafloorSource
        {
            None,
            MapMagicHeight,
            SceneProbeLegacy,
            MacroGeologyFallback,
            FallbackSynthetic,
            TerrainProviderHeight
        }

        [System.Flags]
        public enum BiomeFamilyFlags : ulong
        {
            None = 0UL,
            Sediment = 1UL << 0,
            Drift = 1UL << 1,
            Silt = 1UL << 2,
            Granite = 1UL << 3,
            Brine = 1UL << 4,
            Chemo = 1UL << 5,
            Saline = 1UL << 6,
            Volcanic = 1UL << 7,
            Tectonic = 1UL << 8,
            Glass = 1UL << 9,
            Magma = 1UL << 10,
            Basalt = 1UL << 11,
            Metallic = 1UL << 12,
            Industrial = 1UL << 13,
            Service = 1UL << 14,
            Rift = 1UL << 15,
            Void = 1UL << 16,
            Hadal = 1UL << 17,
            Reef = 1UL << 18,
            Littoral = 1UL << 19,
            Crystal = 1UL << 20,
            Fossil = 1UL << 21,
            Coral = 1UL << 22,
            Kelp = 1UL << 23,
            Growth = 1UL << 24
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct ZoneData
        {
            [FieldOffset(0)]
            public float2 PositionXZ;
            [FieldOffset(8)]
            public float ActivationRadius;
            [FieldOffset(12)]
            public float HoldRadius;
            [FieldOffset(16)]
            public float EdgeBlendDistance;
            [FieldOffset(20)]
            public float EdgeNoiseScale;
            [FieldOffset(24)]
            public float EdgeNoiseStrength;
            [FieldOffset(28)]
            public float2 EdgeNoiseOffset;
            [FieldOffset(36)]
            public int Priority;
            [FieldOffset(40)]
            public int Kind;
            [FieldOffset(44)]
            public int Tier;
            [FieldOffset(48)]
            public int DominantMatrixDataIndex;
            [FieldOffset(52)]
            public int DominantFamilyDataIndex;
            [FieldOffset(56)]
            public int RouteCritical;
            [FieldOffset(60)]
            private int _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct BiomeMatrixData
        {
            [FieldOffset(0)]
            public int MatrixIndex;
            [FieldOffset(4)]
            public int FamilyDataIndex;
            [FieldOffset(8)]
            public float MinDepthMeters;
            [FieldOffset(12)]
            public float MaxDepthMeters;
            [FieldOffset(16)]
            public int LoosePickupBias;
            [FieldOffset(20)]
            public int NodeExtractionBias;
            [FieldOffset(24)]
            public int SalvageBias;
            [FieldOffset(28)]
            public int CommonResourceBias;
            [FieldOffset(32)]
            public int UncommonResourceBias;
            [FieldOffset(36)]
            public int RareResourceBias;
            [FieldOffset(40)]
            public int RoutePressure;
            [FieldOffset(44)]
            public int LandmarkStrength;
            [FieldOffset(48)]
            public int RewardPull;
            [FieldOffset(52)]
            public int SurvivalPressure;
            [FieldOffset(56)]
            public int IsPlaceholder;
            [FieldOffset(60)]
            public int VolumetricRole;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct BiomeFamilyData
        {
            [FieldOffset(0)]
            public int FamilyInstanceId;
            [FieldOffset(4)]
            private int _pad0;
            [FieldOffset(8)]
            public BiomeFamilyFlags Flags;
        }

        [System.Flags]
        public enum BiomeInfluenceFlags : byte
        {
            None = 0,
            Placeholder = 1 << 0,
            TransitionEdge = 1 << 1,
            Hazard = 1 << 2,
            PreviewOverride = 1 << 3,
            VolumetricDepth = 1 << 4,
            SargassumCanopy = 1 << 5,
            ThermalVent = 1 << 6,
            Invalid = 1 << 7
        }

        private enum VolumetricBiomeRole : int
        {
            None = 0,
            AbyssalSilt = 1,
            VolcanicHadal = 2,
            MetallicHadal = 3,
            SedimentDrift = 4,
            CrystalGrowth = 5
        }

        public const float BiomeBorderOverlapMeters = 50f;

        public static float EvaluateBiomeBorderSmoothstepBlend01(float distanceFromBorderMeters, float overlapMeters = BiomeBorderOverlapMeters)
        {
            float safeOverlap = math.max(0.0001f, overlapMeters);
            float t = math.saturate(1f - math.abs(distanceFromBorderMeters) / safeOverlap);
            return 0.5f * t * t * (3f - 2f * t);
        }

        public static byte EvaluateBiomeBorderSmoothstepBlend255(float distanceFromBorderMeters, float overlapMeters = BiomeBorderOverlapMeters)
        {
            float blend01 = EvaluateBiomeBorderSmoothstepBlend01(distanceFromBorderMeters, overlapMeters);
            return (byte)math.clamp((int)math.floor(blend01 * 255f + 0.5f), 0, 255);
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct BiomeInfluenceCell
        {
            [FieldOffset(0)]
            public uint Packed;
            [FieldOffset(4)]
            private uint _pad0;

            public byte PrimaryVisualFamilyId => HectonBiomeVisualFamilyUtility.ExtractPrimaryVisualFamilyId(Packed);
            public byte SecondaryVisualFamilyId => HectonBiomeVisualFamilyUtility.ExtractSecondaryVisualFamilyId(Packed);
            public byte Blend255 => HectonBiomeVisualFamilyUtility.ExtractBlend255(Packed);
            public byte Flags => HectonBiomeVisualFamilyUtility.ExtractFlags(Packed);
            public uint GpuPacked => HectonBiomeVisualFamilyUtility.ExtractGpuPacked(Packed);
            public byte PrimaryBiomeId => PrimaryVisualFamilyId;
            public byte SecondaryBiomeId => SecondaryVisualFamilyId;

            public static byte ExtractPrimaryVisualFamilyId(in BiomeInfluenceCell cell)
            {
                return HectonBiomeVisualFamilyUtility.ExtractPrimaryVisualFamilyId(cell.Packed);
            }

            public static byte ExtractSecondaryVisualFamilyId(in BiomeInfluenceCell cell)
            {
                return HectonBiomeVisualFamilyUtility.ExtractSecondaryVisualFamilyId(cell.Packed);
            }

            public static byte ExtractBlend255(in BiomeInfluenceCell cell)
            {
                return HectonBiomeVisualFamilyUtility.ExtractBlend255(cell.Packed);
            }

            public static byte ExtractFlags(in BiomeInfluenceCell cell)
            {
                return HectonBiomeVisualFamilyUtility.ExtractFlags(cell.Packed);
            }

            public static uint ExtractGpuPacked(in BiomeInfluenceCell cell)
            {
                return HectonBiomeVisualFamilyUtility.ExtractGpuPacked(cell.Packed);
            }

            public static BiomeInfluenceCell Create(byte primaryVisualFamilyId, byte secondaryVisualFamilyId, byte blend255, byte flags)
            {
                return new BiomeInfluenceCell
                {
                    Packed = HectonBiomeVisualFamilyUtility.PackCell(
                        primaryVisualFamilyId,
                        secondaryVisualFamilyId,
                        blend255,
                        flags)
                };
            }

            public static BiomeInfluenceCell CreateFromBiomeIds(byte primaryBiomeId, byte secondaryBiomeId, byte blend255, byte flags)
            {
                return new BiomeInfluenceCell
                {
                    Packed = HectonBiomeVisualFamilyUtility.PackCellFromBiomeIds(
                        primaryBiomeId,
                        secondaryBiomeId,
                        blend255,
                        flags)
                };
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 72)]
        public struct CellInputData
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public float CenterHeight;
            [FieldOffset(16)]
            public float NorthHeight;
            [FieldOffset(20)]
            public float SouthHeight;
            [FieldOffset(24)]
            public float EastHeight;
            [FieldOffset(28)]
            public float WestHeight;
            [FieldOffset(32)]
            public float WaterSurface;
            [FieldOffset(36)]
            public int BiomeIndex;
            [FieldOffset(40)]
            public int BiomeMatrixId;
            [FieldOffset(44)]
            public int SecondaryBiomeMatrixId;
            [FieldOffset(48)]
            public int BiomeBlend255;
            [FieldOffset(52)]
            public int MapMagicBiomeDataValid;
            [FieldOffset(56)]
            public int CellX;
            [FieldOffset(60)]
            public int CellZ;
            [FieldOffset(64)]
            public int SeafloorSource;
            [FieldOffset(68)]
            public int IsValid;
        }

        [StructLayout(LayoutKind.Explicit, Size = 328)]
        public struct CellOutputData
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public int CellX;
            [FieldOffset(16)]
            public int CellZ;
            [FieldOffset(20)]
            public float SampleY;
            [FieldOffset(24)]
            public float SeafloorHeight;
            [FieldOffset(28)]
            public float DepthMeters;
            [FieldOffset(32)]
            public float SlopeDegrees;
            [FieldOffset(36)]
            public float Curvature;
            [FieldOffset(40)]
            public float RidgeSignal;
            [FieldOffset(44)]
            public float CanyonSignal;
            [FieldOffset(48)]
            public float CaveProximity;
            [FieldOffset(52)]
            public float CompositionPotential;
            [FieldOffset(56)]
            public float ZoneWeight;
            [FieldOffset(60)]
            public float TerrainNoise;
            [FieldOffset(64)]
            public float DetailNoise;
            [FieldOffset(68)]
            public float SedimentFieldNoise;
            [FieldOffset(72)]
            public float FertileFieldNoise;
            [FieldOffset(76)]
            public float ReefFieldNoise;
            [FieldOffset(80)]
            public float IndustrialFieldNoise;
            [FieldOffset(84)]
            public float HazardFieldNoise;
            [FieldOffset(88)]
            public float LandmarkFieldNoise;
            [FieldOffset(92)]
            public float BasinFieldNoise;
            [FieldOffset(96)]
            public float RuggedBiomeNoise;
            [FieldOffset(100)]
            public float FertileBiomeNoise;
            [FieldOffset(104)]
            public float ThermalBiomeNoise;
            [FieldOffset(108)]
            public float MetallicBiomeNoise;
            [FieldOffset(112)]
            public float CrystalBiomeNoise;
            [FieldOffset(116)]
            public float VoidBiomeNoise;
            [FieldOffset(120)]
            public float ReefBiomeNoise;
            [FieldOffset(124)]
            public float BasinMacroNoise;
            [FieldOffset(128)]
            public float ReefMacroNoise;
            [FieldOffset(132)]
            public float ServiceMacroNoise;
            [FieldOffset(136)]
            public float RiftMacroNoise;
            [FieldOffset(140)]
            public float CoralPatternNoise;
            [FieldOffset(144)]
            public float CaveNoise;
            [FieldOffset(148)]
            public float CompositionNoise;
            [FieldOffset(152)]
            public float RuggedBias;
            [FieldOffset(156)]
            public float FertileBias;
            [FieldOffset(160)]
            public float HazardBias;
            [FieldOffset(164)]
            public float ServiceBias;
            [FieldOffset(168)]
            public float ResourceBias;
            [FieldOffset(172)]
            public float ShelterBias;
            [FieldOffset(176)]
            public float LandmarkBias;
            [FieldOffset(180)]
            public float RockDensityHeat;
            [FieldOffset(184)]
            public float KelpDensityHeat;
            [FieldOffset(188)]
            public float FloraDensityHeat;
            [FieldOffset(192)]
            public float CoralDensityHeat;
            [FieldOffset(196)]
            public float BioDensityHeat;
            [FieldOffset(200)]
            public float DebrisDensityHeat;
            [FieldOffset(204)]
            public float RuinDensityHeat;
            [FieldOffset(208)]
            public float CaveDensityHeat;
            [FieldOffset(212)]
            public float LandmarkStrengthHeat;
            [FieldOffset(216)]
            public float FaunaDensityHeat;
            [FieldOffset(220)]
            public float HazardDensityHeat;
            [FieldOffset(224)]
            public float ResourceDensityHeat;
            [FieldOffset(228)]
            public float ShelterDensityHeat;
            [FieldOffset(232)]
            public float ServiceDensityHeat;
            [FieldOffset(236)]
            public float GenericHeat;
            [FieldOffset(240)]
            public float SecondaryHeight;
            [FieldOffset(244)]
            public float SecondaryDepthMeters;
            [FieldOffset(248)]
            public float SecondaryCaveProximity;
            [FieldOffset(252)]
            public float SecondaryDomainWeight;
            [FieldOffset(256)]
            public int BiomeIndex;
            [FieldOffset(260)]
            public int ZoneDataIndex;
            [FieldOffset(264)]
            public int BiomeMatrixDataIndex;
            [FieldOffset(268)]
            public int PreviousBiomeMatrixDataIndex;
            [FieldOffset(272)]
            public int SecondaryBiomeMatrixDataIndex;
            [FieldOffset(276)]
            public int MapMagicBiomeBlend255;
            [FieldOffset(280)]
            public int BiomeFamilyDataIndex;
            [FieldOffset(284)]
            private uint _pad0;
            [FieldOffset(288)]
            public ulong BiomeFamilyFlags;
            [FieldOffset(296)]
            public int ResolvedZoneKind;
            [FieldOffset(300)]
            public int ResolvedPattern;
            [FieldOffset(304)]
            public int PreviewOverrideActive;
            [FieldOffset(308)]
            public int VolumetricOverrideActive;
            [FieldOffset(312)]
            public int SecondarySampleValid;
            [FieldOffset(316)]
            public int SeafloorSource;
            [FieldOffset(320)]
            public int IsValid;
            [FieldOffset(324)]
            public uint BiomeInfluencePacked;
        }

        public struct FieldSample
        {
            public Vector3 position;
            public float seafloorHeight;
            public float depthMeters;
            public float slopeDegrees;
            public float curvature;
            public float ridgeSignal;
            public float canyonSignal;
            public float caveProximity;
            public float compositionPotential;
            public int biomeIndex;
            public int zoneDataIndex;
            public int biomeMatrixDataIndex;
            public int biomeFamilyDataIndex;
            public ulong biomeFamilyFlags;
            public HectonBiomeMatrixProfile biomeProfile;
            public HectonBiomeMatrixProfile secondaryBiomeProfile;
            public HectonBiomeFamilyProfile biomeFamily;
            public HectonBiomeFamilyProfile secondaryBiomeFamily;
            public BiomeInfluenceCell biomeInfluence;
            public WorldZoneAnchor zone;
            public float zoneWeight;
            public WorldZoneAnchor.ZoneKind resolvedZoneKind;
            public WorldProceduralPattern resolvedPattern;
            public byte isPreviewOverride;
            public WorldTerrainDetailRuntimeSample terrainDetailSample;
            public WorldMacroGeologySample macroGeologySample;
            public WorldTerrainMesoDetailSample terrainMesoDetail;
            public WorldTerrainSurfaceMaterialWeights terrainSurfaceMaterialWeights;
            public WorldTerrainSurfaceMaterialClass terrainSurfaceMaterialClass;
            public WorldTerrainDetailEligibilityFlags terrainDetailEligibilityFlags;
            public float4 terrainMaterialControl1;
            public float4 terrainMaterialControl2;
            public int verticalDomainIndex;
            public float verticalDomainWeight;
            public byte isSecondaryDomain;
            public SeafloorSource seafloorSource;
            public byte hasTerrainDetailSample;
            public byte isValid;
        }

        public struct PatternHeatContext
        {
            public float SedimentField;
            public float FertileField;
            public float ReefField;
            public float IndustrialField;
            public float HazardField;
            public float LandmarkField;
            public float ShelterField;
            public float AbyssField;
            public float RuggedBias;
            public float TerrainNoise;
            public float DetailNoise;
        }

        public struct CellSamplingContext
        {
            public float TerrainNoise;
            public float DetailNoise;
            public float SedimentFieldNoise;
            public float FertileFieldNoise;
            public float ReefFieldNoise;
            public float IndustrialFieldNoise;
            public float HazardFieldNoise;
            public float LandmarkFieldNoise;
            public float BasinFieldNoise;
            public float RuggedBiomeNoise;
            public float FertileBiomeNoise;
            public float ThermalBiomeNoise;
            public float MetallicBiomeNoise;
            public float CrystalBiomeNoise;
            public float VoidBiomeNoise;
            public float ReefBiomeNoise;
            public float BasinMacroNoise;
            public float ReefMacroNoise;
            public float ServiceMacroNoise;
            public float RiftMacroNoise;
            public float CoralPatternNoise;
            public float CaveNoise;
            public float CompositionNoise;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        private ITerrainProvider _terrainProviderRuntime;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Fallback Biome Families")]
        [SerializeField] private HectonBiomeFamilyProfile littoralKarstFamily;
        [SerializeField] private HectonBiomeFamilyProfile fossilReefFamily;
        [SerializeField] private HectonBiomeFamilyProfile sedimentDriftFamily;
        [SerializeField] private HectonBiomeFamilyProfile abyssalSiltFamily;
        [SerializeField] private HectonBiomeFamilyProfile graniteEscarpmentFamily;
        [SerializeField] private HectonBiomeFamilyProfile tectonicSpineFamily;
        [SerializeField] private HectonBiomeFamilyProfile riftSpineFamily;
        [SerializeField] private HectonBiomeFamilyProfile riftVoidFamily;
        [SerializeField] private HectonBiomeFamilyProfile volcanicGlassFamily;
        [SerializeField] private HectonBiomeFamilyProfile volcanicHadalFamily;
        [SerializeField] private HectonBiomeFamilyProfile metallicHadalFamily;
        [SerializeField] private HectonBiomeFamilyProfile chemosyntheticBrineFamily;
        [SerializeField] private HectonBiomeFamilyProfile crystalGrowthFamily;

        [Header("Sampling")]
        [SerializeField] private float slopeProbeMeters = 4f;
        [SerializeField] private float fieldNoiseScale = 0.0035f;
        [SerializeField] private float detailNoiseScale = 0.0125f;
        [SerializeField, Min(0f)] private float steepSlopeGradientCheatThresholdDegrees = 45f;
        [SerializeField, Min(0f)] private float steepSlopeGradientCheatMaxDropMeters = 1.25f;

        [Header("Macro Geology Fallback")]
        [SerializeField] private uint macroGeologyAuthoringSeed = unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed);
        [SerializeField, Min(WorldMacroGeologyFields.MinimumWorldExtentMeters)] private float macroGeologyWorldExtentMeters = WorldMacroGeologyFields.MinimumWorldExtentMeters;
        [SerializeField, Min(128f)] private float macroGeologyChunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters;
        [SerializeField, Min(8f)] private float macroGeologyDetailProbeMeters = 120f;

        [Header("Preview Overrides")]
        [SerializeField] private bool forcePatternPreviewOverride;
        [SerializeField] private WorldProceduralPattern previewPatternOverride = WorldProceduralPattern.SedimentResources;
        [SerializeField] private bool limitPatternOverrideToFallback = true;
        [SerializeField] private bool forceMatrixBiomePreviewOverride;
        [SerializeField] private HectonBiomeMatrixProfile previewMatrixBiomeOverride;
        [SerializeField] private bool limitMatrixBiomeOverrideToFallback = true;

        [Header("Diagnostics")]
        [Tooltip("Ostavlyay vyklyuchennym v obychnom runtime. Zhivye inspector-diagnostiki sampler-a dorogie i nuzhny tolko dlya tochechnoy otladki.")]
        [SerializeField] private bool enableLiveRuntimeDiagnostics;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugZoneDirectorReady;
        [SerializeField] private bool _debugBiomeDirectorReady;
        [SerializeField] private string _debugLastZone = "None";
        [SerializeField] private string _debugLastBiomeProfile = "None";
        [SerializeField] private string _debugLastBiomeFamily = "None";
        [SerializeField] private string _debugLastPattern = "None";
        [SerializeField] private string _debugPatternOverride = "None";
        [SerializeField] private string _debugPreviewBiomeOverride = "None";
        [SerializeField] private string _debugPreviewMatrixOverride = "None";
        [SerializeField] private string _debugPreviewZoneOverride = "None";
        [SerializeField] private string _debugLastHeatmap = "None";
        [SerializeField] private string _debugLastHeightSource = "None";
        [SerializeField] private float _debugLastHeatmapValue;
        [SerializeField] private float _debugLastDepth;
        [SerializeField] private float _debugLastSlope;
        [SerializeField] private float _debugLastCurvature;
        [SerializeField] private float _debugLastCaveProximity;
        [SerializeField] private float _debugLastCompositionPotential;
        [SerializeField] private int _debugBiomeCacheMisses;

        // COLD ALLOC: WorldZoneAnchor[64] - active anchor snapshot - owner: WorldProceduralFieldSampler
        private readonly WorldZoneAnchor[] _anchors = new WorldZoneAnchor[MaxZoneAnchorSnapshotCount];
        // COLD ALLOC: WorldZoneAnchor[64] - zone bake snapshot - owner: WorldProceduralFieldSampler
        private readonly WorldZoneAnchor[] _zoneBakeList = new WorldZoneAnchor[MaxZoneAnchorSnapshotCount];
        // COLD ALLOC: HectonBiomeMatrixProfile[160] - biome matrix bake snapshot - owner: WorldProceduralFieldSampler
        private readonly HectonBiomeMatrixProfile[] _biomeMatrixBakeList = new HectonBiomeMatrixProfile[MaxBiomeMatrixBakeCount];
        // COLD ALLOC: HectonBiomeFamilyProfile[48] - biome family bake snapshot - owner: WorldProceduralFieldSampler
        private readonly HectonBiomeFamilyProfile[] _biomeFamilyBakeList = new HectonBiomeFamilyProfile[MaxBiomeFamilyBakeCount];
        // COLD ALLOC: CaveEntranceHint[64] - cave entrance hint bake snapshot - owner: WorldProceduralFieldSampler
        private readonly WorldCaveDirector.CaveEntranceHint[] _caveEntranceHintBakeList = new WorldCaveDirector.CaveEntranceHint[MaxCaveEntranceHintBakeCount];
        private int _anchorCount;
        private int _zoneBakeCount;
        private int _biomeMatrixBakeCount;
        private int _biomeFamilyBakeCount;
        private int _caveEntranceHintBakeCount;
        // COLD ALLOC: Vector2Int[4096] - fixed seafloor cache keys - owner: WorldProceduralFieldSampler
        private readonly Vector2Int[] _seafloorHeightCacheKeys = new Vector2Int[MaxSeafloorHeightCacheEntries];
        // COLD ALLOC: CachedHeightSample[4096] - fixed seafloor cache payloads - owner: WorldProceduralFieldSampler
        private readonly CachedHeightSample[] _seafloorHeightCacheValues = new CachedHeightSample[MaxSeafloorHeightCacheEntries];
        // COLD ALLOC: byte[4096] - fixed seafloor cache occupancy flags - owner: WorldProceduralFieldSampler
        private readonly byte[] _seafloorHeightCacheOccupied = new byte[MaxSeafloorHeightCacheEntries];
        private int _seafloorHeightCacheCount;
        private IDataVault _dataVault;
        private IDataVault _samplingJobPinVault;
        private uint _samplingJobPinMask;
        private VaultGenerationHandle<ZoneData> _burstZoneDataHandle;
        private VaultGenerationHandle<BiomeMatrixData> _burstBiomeMatrixDataHandle;
        private VaultGenerationHandle<int> _burstBiomeMatrixIdToDataIndexHandle;
        private VaultGenerationHandle<BiomeFamilyData> _burstBiomeFamilyDataHandle;
        private VaultGenerationHandle<CaveEntranceHintData> _burstCaveEntranceHintsHandle;
        private VaultGenerationHandle<ushort> _noiseLookupTableHandle;
        private int _burstZoneDataCount;
        private int _burstBiomeMatrixDataCount;
        private int _burstBiomeFamilyDataCount;
        private int _burstCaveEntranceHintCount;
        private bool _isDataDirty = true;
        private int _lastActiveAnchorVersion = -1;
        private int _lastCaveEntranceHintVersion = -1;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;
        private bool _samplingFramePrepared;
        private int _samplingFrameId;
        private WorldCaveDirector _worldCaveDirector;
        private JobHandle _lastSamplingJobHandle;
        private bool _hasPendingSamplingJob;
        private bool _samplingJobBuffersPinned;
        private bool _hotSwapRegistered;
        private bool _worldZoneListenerRegistered;
        private bool _worldCaveListenerRegistered;

        private static WorldProceduralFieldSampler s_activeRuntimeInstance;

        internal static WorldProceduralFieldSampler ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_activeRuntimeInstance = null;
        }

        private struct CachedHeightSample
        {
            public float Height;
            public SeafloorSource Source;
            public int SamplingFrameId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct CaveEntranceHintData
        {
            [FieldOffset(0)]
            public float3 SurfacePosition;
            [FieldOffset(12)]
            public float3 InteriorPosition;
            [FieldOffset(24)]
            public float EntranceRadius;
            [FieldOffset(28)]
            public float InfluenceRadius;
        }

        private struct LocalTerrainContext
        {
            public float CenterHeight;
            public float NorthHeight;
            public float SouthHeight;
            public float EastHeight;
            public float WestHeight;
            public float SlopeDegrees;
            public float Curvature;
            public SeafloorSource CenterSource;
        }

        private struct CellHeightContext
        {
            public float CenterHeight;
            public float NorthHeight;
            public float SouthHeight;
            public float EastHeight;
            public float WestHeight;
            public SeafloorSource CenterSource;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CellSamplingJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<CellInputData> CellInputs;
            [ReadOnly, NoAlias] public NativeArray<ZoneData> Zones;
            [ReadOnly, NoAlias] public NativeArray<BiomeMatrixData> BiomeMatrices;
            [ReadOnly, NoAlias] public NativeArray<int> BiomeMatrixIdToDataIndex;
            [ReadOnly, NoAlias] public NativeArray<BiomeFamilyData> BiomeFamilies;
            [ReadOnly, NoAlias] public NativeArray<CaveEntranceHintData> CaveEntranceHints;
            [ReadOnly, NoAlias] public NativeArray<ushort> NoiseLookupTable;
            [WriteOnly, NoAlias] public NativeArray<CellOutputData> CellOutputs;
            [WriteOnly, NoAlias] public NativeArray<BiomeInfluenceCell> BiomeInfluences;

            public float SlopeProbeMeters;
            public float FieldNoiseScale;
            public float DetailNoiseScale;
            public int ForcePreviewPatternOverride;
            public int LimitPreviewPatternOverrideToFallback;
            public int PreviewPatternOverride;
            public int CurrentBiomeMatrixDataIndex;
            public int CurrentBiomeFamilyDataIndex;
            public int PreviewMatrixBiomeDataIndex;
            public int PreviewMatrixBiomeFamilyDataIndex;
            public int CurrentZoneDataIndex;
            public int ZoneCount;
            public int BiomeMatrixCount;
            public int BiomeFamilyCount;
            public int CaveEntranceHintCount;
            public int LittoralKarstFamilyIndex;
            public int FossilReefFamilyIndex;
            public int SedimentDriftFamilyIndex;
            public int AbyssalSiltFamilyIndex;
            public int GraniteEscarpmentFamilyIndex;
            public int TectonicSpineFamilyIndex;
            public int RiftSpineFamilyIndex;
            public int RiftVoidFamilyIndex;
            public int VolcanicGlassFamilyIndex;
            public int VolcanicHadalFamilyIndex;
            public int MetallicHadalFamilyIndex;
            public int ChemosyntheticBrineFamilyIndex;
            public int CrystalGrowthFamilyIndex;

            public void Execute(int index)
            {
                CellInputData input = CellInputs[index];
                if (input.IsValid == 0)
                {
                    CellOutputData invalidOutput = CreateInvalidCellOutput(input, CurrentBiomeMatrixDataIndex, CurrentBiomeFamilyDataIndex);
                    BiomeInfluenceCell invalidInfluence = BiomeInfluenceCell.Create(0, 0, 0, (byte)BiomeInfluenceFlags.Invalid);
                    invalidOutput.BiomeInfluencePacked = invalidInfluence.Packed;
                    CellOutputs[index] = invalidOutput;
                    BiomeInfluences[index] = invalidInfluence;
                    return;
                }

                CellOutputData output = BuildCellOutput(
                    input,
                    Zones,
                    BiomeMatrices,
                    BiomeMatrixIdToDataIndex,
                    BiomeFamilies,
                    CaveEntranceHints,
                    NoiseLookupTable,
                    SlopeProbeMeters,
                    FieldNoiseScale,
                    DetailNoiseScale,
                    ForcePreviewPatternOverride,
                    LimitPreviewPatternOverrideToFallback,
                    PreviewPatternOverride,
                    CurrentBiomeMatrixDataIndex,
                    CurrentBiomeFamilyDataIndex,
                    PreviewMatrixBiomeDataIndex,
                    PreviewMatrixBiomeFamilyDataIndex,
                    CurrentZoneDataIndex,
                    ZoneCount,
                    BiomeMatrixCount,
                    BiomeFamilyCount,
                    CaveEntranceHintCount,
                    LittoralKarstFamilyIndex,
                    FossilReefFamilyIndex,
                    SedimentDriftFamilyIndex,
                    AbyssalSiltFamilyIndex,
                    GraniteEscarpmentFamilyIndex,
                    TectonicSpineFamilyIndex,
                    RiftSpineFamilyIndex,
                    RiftVoidFamilyIndex,
                    VolcanicGlassFamilyIndex,
                    VolcanicHadalFamilyIndex,
                    MetallicHadalFamilyIndex,
                    ChemosyntheticBrineFamilyIndex,
                    CrystalGrowthFamilyIndex);

                BiomeInfluenceCell influence = BuildBiomeInfluenceCell(
                    output,
                    BiomeMatrices,
                    BiomeMatrixCount,
                    BiomeFamilies,
                    BiomeFamilyCount,
                    CurrentBiomeMatrixDataIndex);
                output.BiomeInfluencePacked = influence.Packed;
                CellOutputs[index] = output;
                BiomeInfluences[index] = influence;
            }
        }

        private static CellOutputData CreateInvalidCellOutput(
            in CellInputData input,
            int currentBiomeMatrixDataIndex,
            int currentBiomeFamilyDataIndex)
        {
            return new CellOutputData
            {
                Position = input.Position,
                CellX = input.CellX,
                CellZ = input.CellZ,
                SampleY = input.Position.y,
                BiomeIndex = input.BiomeIndex,
                SeafloorSource = input.SeafloorSource,
                ZoneDataIndex = -1,
                BiomeMatrixDataIndex = currentBiomeMatrixDataIndex,
                PreviousBiomeMatrixDataIndex = -1,
                SecondaryBiomeMatrixDataIndex = -1,
                BiomeFamilyDataIndex = currentBiomeFamilyDataIndex,
                ResolvedZoneKind = (int)WorldZoneAnchor.ZoneKind.Generic,
                ResolvedPattern = (int)WorldProceduralPattern.SedimentResources,
                PreviewOverrideActive = 0,
                SecondarySampleValid = 0,
                IsValid = 0
            };
        }

        private static CellOutputData BuildCellOutput(
            in CellInputData input,
            NativeArray<ZoneData> zones,
            NativeArray<BiomeMatrixData> biomeMatrices,
            NativeArray<int> biomeMatrixIdToDataIndex,
            NativeArray<BiomeFamilyData> biomeFamilies,
            NativeArray<CaveEntranceHintData> caveEntranceHints,
            NativeArray<ushort> noiseLookupTable,
            float slopeProbeMeters,
            float fieldNoiseScale,
            float detailNoiseScale,
            int forcePreviewPatternOverride,
            int limitPreviewPatternOverrideToFallback,
            int previewPatternOverride,
            int currentBiomeMatrixDataIndex,
            int currentBiomeFamilyDataIndex,
            int previewMatrixBiomeDataIndex,
            int previewMatrixBiomeFamilyDataIndex,
            int currentZoneDataIndex,
            int zoneCount,
            int biomeMatrixCount,
            int biomeFamilyCount,
            int caveEntranceHintCount,
            int littoralKarstFamilyIndex,
            int fossilReefFamilyIndex,
            int sedimentDriftFamilyIndex,
            int abyssalSiltFamilyIndex,
            int graniteEscarpmentFamilyIndex,
            int tectonicSpineFamilyIndex,
            int riftSpineFamilyIndex,
            int riftVoidFamilyIndex,
            int volcanicGlassFamilyIndex,
            int volcanicHadalFamilyIndex,
            int metallicHadalFamilyIndex,
            int chemosyntheticBrineFamilyIndex,
            int crystalGrowthFamilyIndex)
        {
            float probe = math.max(0.0001f, slopeProbeMeters);
            float dx = (input.EastHeight - input.WestHeight) / (probe * 2f);
            float dz = (input.NorthHeight - input.SouthHeight) / (probe * 2f);
            float gradient = FastLength2D(dx, dz);
            float slopeDegrees = math.degrees(MathLodApproximation.ApproxAtanFast(gradient));
            float curvature = (input.WestHeight + input.EastHeight + input.NorthHeight + input.SouthHeight - (input.CenterHeight * 4f)) / math.max(0.0001f, probe * probe);
            curvature = math.clamp(curvature / 0.85f, -1f, 1f);

            CellOutputData output = new CellOutputData
            {
                Position = new float3(input.Position.x, input.CenterHeight, input.Position.z),
                CellX = input.CellX,
                CellZ = input.CellZ,
                SampleY = input.Position.y,
                SeafloorHeight = input.CenterHeight,
                DepthMeters = math.max(0f, input.WaterSurface - input.CenterHeight),
                SlopeDegrees = slopeDegrees,
                Curvature = curvature,
                BiomeIndex = input.BiomeIndex,
                SeafloorSource = input.SeafloorSource,
                ZoneDataIndex = -1,
                BiomeMatrixDataIndex = ResolveBiomeMatrixDataIndexFromMatrixId(
                    input.BiomeMatrixId,
                    biomeMatrixIdToDataIndex,
                    currentBiomeMatrixDataIndex),
                PreviousBiomeMatrixDataIndex = -1,
                SecondaryBiomeMatrixDataIndex = ResolveBiomeMatrixDataIndexFromMatrixId(
                    input.SecondaryBiomeMatrixId,
                    biomeMatrixIdToDataIndex,
                    -1),
                MapMagicBiomeBlend255 = math.clamp(input.BiomeBlend255, 0, 255),
                BiomeFamilyDataIndex = currentBiomeFamilyDataIndex,
                ResolvedZoneKind = (int)WorldZoneAnchor.ZoneKind.Generic,
                ResolvedPattern = (int)WorldProceduralPattern.SedimentResources,
                PreviewOverrideActive = 0,
                IsValid = 1
            };

            FillNoiseContext(ref output, noiseLookupTable, fieldNoiseScale, detailNoiseScale);
            output.ZoneDataIndex = ResolveZoneDataIndex(output.Position.xz, zones, zoneCount, currentZoneDataIndex, out output.ZoneWeight);
            if (output.ZoneDataIndex >= 0)
            {
                ZoneData zoneData = zones[output.ZoneDataIndex];
                if (input.MapMagicBiomeDataValid == 0)
                    output.BiomeMatrixDataIndex = zoneData.DominantMatrixDataIndex;
                output.BiomeFamilyDataIndex = zoneData.DominantFamilyDataIndex;
                output.ResolvedZoneKind = zoneData.Kind;
            }
            else
            {
                output.ResolvedZoneKind = (int)ResolveFallbackZoneKind(
                        output.DepthMeters,
                        output.SlopeDegrees,
                        output.FertileBiomeNoise,
                        output.ThermalBiomeNoise,
                        output.MetallicBiomeNoise,
                        output.VoidBiomeNoise);
                if (output.BiomeFamilyDataIndex < 0)
                {
                    output.BiomeFamilyDataIndex = ResolveFallbackBiomeFamilyIndex(
                        output.DepthMeters,
                        output.SlopeDegrees,
                        (WorldZoneAnchor.ZoneKind)output.ResolvedZoneKind,
                        output,
                        littoralKarstFamilyIndex,
                        fossilReefFamilyIndex,
                        sedimentDriftFamilyIndex,
                        abyssalSiltFamilyIndex,
                        graniteEscarpmentFamilyIndex,
                        tectonicSpineFamilyIndex,
                        riftSpineFamilyIndex,
                        riftVoidFamilyIndex,
                        volcanicGlassFamilyIndex,
                        volcanicHadalFamilyIndex,
                        metallicHadalFamilyIndex,
                        chemosyntheticBrineFamilyIndex,
                        crystalGrowthFamilyIndex);
                }
            }

            BiomeEvaluationContext ruggedContext = new BiomeEvaluationContext
            {
                ZoneDataIndex = output.ZoneDataIndex,
                ResolvedZoneKind = output.ResolvedZoneKind,
                BiomeFamilyDataIndex = output.BiomeFamilyDataIndex,
                Zones = zones,
                ZoneCount = zoneCount,
                BiomeMatrices = biomeMatrices,
                BiomeMatrixCount = biomeMatrixCount,
                BiomeFamilies = biomeFamilies,
                BiomeFamilyCount = biomeFamilyCount
            };
            output.RuggedBias = EvaluateRuggedBiomeBias(in ruggedContext);
            output.FertileBias = EvaluateFertileBiomeBias(output.ZoneDataIndex, output.ResolvedZoneKind, output.BiomeFamilyDataIndex, zones, zoneCount, biomeFamilies, biomeFamilyCount);
            output.HazardBias = EvaluateHazardBias(output.ZoneDataIndex, output.ResolvedZoneKind, zones, zoneCount, biomeMatrices, biomeMatrixCount);
            output.ServiceBias = EvaluateServiceBias(output.ZoneDataIndex, output.ResolvedZoneKind, zones, zoneCount);
            output.ResourceBias = EvaluateResourceBias(output.ZoneDataIndex, output.ResolvedZoneKind, zones, zoneCount, biomeMatrices, biomeMatrixCount);
            output.ShelterBias = EvaluateShelterBias(output.ZoneDataIndex, output.ResolvedZoneKind, zones, zoneCount);
            output.LandmarkBias = EvaluateLandmarkBias(output.ZoneDataIndex, output.ResolvedZoneKind, zones, zoneCount, biomeMatrices, biomeMatrixCount);
            output.RidgeSignal = math.saturate(math.max(0f, output.Curvature) * 0.62f + math.saturate((output.SlopeDegrees - 8f) / 36f) * 0.26f + output.RuggedBias * 0.12f);
            output.CanyonSignal = math.saturate(math.max(0f, -output.Curvature) * 0.58f + math.saturate((output.SlopeDegrees - 10f) / 34f) * 0.22f + output.HazardBias * 0.20f);
            output.CaveProximity = math.saturate(
                math.saturate((output.SlopeDegrees - 8f) / 40f) * 0.22f +
                math.saturate((output.DepthMeters - 120f) / 780f) * 0.10f +
                output.RuggedBias * 0.24f +
                output.HazardBias * 0.18f +
                output.LandmarkBias * 0.14f +
                output.CaveNoise * 0.12f);
            output.CompositionPotential = math.saturate(
                math.saturate((output.SlopeDegrees - 6f) / 42f) * 0.16f +
                math.abs(output.Curvature) * 0.18f +
                output.RidgeSignal * 0.20f +
                output.CanyonSignal * 0.18f +
                output.CaveProximity * 0.18f +
                output.CompositionNoise * 0.10f);
            ResolveSecondaryDomain(
                ref output,
                input.WaterSurface,
                caveEntranceHints,
                caveEntranceHintCount);
            bool applyPreviewPatternOverride = forcePreviewPatternOverride != 0
                && (limitPreviewPatternOverrideToFallback == 0 || output.SeafloorSource == (int)SeafloorSource.FallbackSynthetic);

            if (applyPreviewPatternOverride)
            {
                WorldProceduralPattern previewPattern = (WorldProceduralPattern)previewPatternOverride;
                output.ResolvedPattern = previewPatternOverride;
                output.ResolvedZoneKind = (int)ResolvePreviewPatternZoneKind(previewPattern);
                output.PreviewOverrideActive = 1;
                output.BiomeFamilyDataIndex = ResolvePreviewPatternBiomeFamilyIndex(
                    previewPattern,
                    output.DepthMeters,
                    output.SlopeDegrees,
                    output.BiomeFamilyDataIndex,
                    sedimentDriftFamilyIndex,
                    littoralKarstFamilyIndex,
                    fossilReefFamilyIndex,
                    abyssalSiltFamilyIndex,
                    graniteEscarpmentFamilyIndex,
                    tectonicSpineFamilyIndex,
                    riftSpineFamilyIndex,
                    riftVoidFamilyIndex,
                    volcanicGlassFamilyIndex,
                    volcanicHadalFamilyIndex,
                    metallicHadalFamilyIndex,
                    chemosyntheticBrineFamilyIndex,
                    crystalGrowthFamilyIndex);
            }
            else
            {
                output.ResolvedPattern = (int)ResolvePattern(output, zones, zoneCount, biomeMatrices, biomeMatrixCount, biomeFamilies, biomeFamilyCount);
            }

            if (previewMatrixBiomeDataIndex >= 0)
            {
                output.BiomeMatrixDataIndex = previewMatrixBiomeDataIndex;
                output.PreviewOverrideActive = 1;
                if (previewMatrixBiomeFamilyDataIndex >= 0)
                    output.BiomeFamilyDataIndex = previewMatrixBiomeFamilyDataIndex;
            }

            int volumetricBiomeMatrixIndex = ResolveVolumetricBiomeMatrixDataIndex(
                input.Position.y,
                math.max(output.DepthMeters, math.max(0f, input.WaterSurface - input.Position.y)),
                output.BiomeMatrixDataIndex,
                output.BiomeFamilyDataIndex,
                biomeMatrices,
                biomeMatrixCount);
            if (volumetricBiomeMatrixIndex >= 0 && volumetricBiomeMatrixIndex != output.BiomeMatrixDataIndex)
            {
                output.PreviousBiomeMatrixDataIndex = output.BiomeMatrixDataIndex;
                output.BiomeMatrixDataIndex = volumetricBiomeMatrixIndex;
                output.VolumetricOverrideActive = 1;
                if (volumetricBiomeMatrixIndex < biomeMatrixCount)
                    output.BiomeFamilyDataIndex = biomeMatrices[volumetricBiomeMatrixIndex].FamilyDataIndex;
            }

            output.BiomeFamilyFlags = (ulong)ResolveBiomeFamilyFlags(output.BiomeFamilyDataIndex, biomeFamilies, biomeFamilyCount);
            ComputeHeatChannels(ref output, biomeMatrices, biomeMatrixCount);
            ApplyTectonicSpineSteepSlopeHeatBias(ref output);
            return output;
        }

        private static BiomeInfluenceCell BuildBiomeInfluenceCell(
            in CellOutputData output,
            NativeArray<BiomeMatrixData> biomeMatrices,
            int biomeMatrixCount,
            NativeArray<BiomeFamilyData> biomeFamilies,
            int biomeFamilyCount,
            int currentBiomeMatrixDataIndex)
        {
            byte flags = 0;
            if (output.IsValid == 0)
                flags |= (byte)BiomeInfluenceFlags.Invalid;
            if (output.PreviewOverrideActive != 0)
                flags |= (byte)BiomeInfluenceFlags.PreviewOverride;
            if (output.VolumetricOverrideActive != 0)
                flags |= (byte)BiomeInfluenceFlags.VolumetricDepth;
            if (output.HazardBias >= 0.65f)
                flags |= (byte)BiomeInfluenceFlags.Hazard;
            BiomeFamilyFlags familyFlags = ResolveBiomeFamilyFlags(output.BiomeFamilyDataIndex, biomeFamilies, biomeFamilyCount);
            if ((familyFlags & BiomeFamilyFlags.Volcanic) != 0 && (familyFlags & BiomeFamilyFlags.Hadal) != 0)
                flags |= (byte)BiomeInfluenceFlags.ThermalVent;
            if ((familyFlags & BiomeFamilyFlags.Silt) != 0 && output.DepthMeters >= 160f && output.DepthMeters <= 260f)
                flags |= (byte)BiomeInfluenceFlags.SargassumCanopy;

            byte primaryBiomeId = ResolveBiomeInfluenceMatrixId(
                output.BiomeMatrixDataIndex,
                biomeMatrices,
                biomeMatrixCount,
                ref flags);
            byte secondaryBiomeId = 0;
            byte blend255 = 0;

            if (output.VolumetricOverrideActive != 0)
            {
                int secondaryIndex = output.PreviousBiomeMatrixDataIndex >= 0
                    ? output.PreviousBiomeMatrixDataIndex
                    : currentBiomeMatrixDataIndex;
                secondaryBiomeId = ResolveBiomeInfluenceMatrixId(
                    secondaryIndex,
                    biomeMatrices,
                    biomeMatrixCount,
                    ref flags);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = 255;
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }
            else if (output.SecondaryBiomeMatrixDataIndex >= 0 && output.MapMagicBiomeBlend255 > 0)
            {
                secondaryBiomeId = ResolveBiomeInfluenceMatrixId(
                    output.SecondaryBiomeMatrixDataIndex,
                    biomeMatrices,
                    biomeMatrixCount,
                    ref flags);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = (byte)math.clamp(output.MapMagicBiomeBlend255, 0, 255);
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }
            else if (output.ZoneDataIndex >= 0 && output.ZoneWeight > 0.001f && output.ZoneWeight < 0.999f)
            {
                secondaryBiomeId = ResolveBiomeInfluenceMatrixId(
                    currentBiomeMatrixDataIndex,
                    biomeMatrices,
                    biomeMatrixCount,
                    ref flags);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = (byte)math.round(math.saturate(1f - output.ZoneWeight) * 255f);
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }

            return BiomeInfluenceCell.CreateFromBiomeIds(primaryBiomeId, secondaryBiomeId, blend255, flags);
        }

        private static byte ResolveBiomeInfluenceMatrixId(
            int biomeMatrixDataIndex,
            NativeArray<BiomeMatrixData> biomeMatrices,
            int biomeMatrixCount,
            ref byte flags)
        {
            if (biomeMatrixDataIndex < 0 ||
                biomeMatrixDataIndex >= biomeMatrixCount ||
                !biomeMatrices.IsCreated)
            {
                return 0;
            }

            BiomeMatrixData matrixData = biomeMatrices[biomeMatrixDataIndex];
            if (matrixData.IsPlaceholder != 0)
                flags |= (byte)BiomeInfluenceFlags.Placeholder;

            return matrixData.MatrixIndex > 0 && matrixData.MatrixIndex <= 255
                ? (byte)matrixData.MatrixIndex
                : (byte)0;
        }

        private static int ResolveVolumetricBiomeMatrixDataIndex(
            float sampleY,
            float depthMeters,
            int currentBiomeMatrixDataIndex,
            int preferredFamilyDataIndex,
            NativeArray<BiomeMatrixData> biomeMatrices,
            int biomeMatrixCount)
        {
            if (!biomeMatrices.IsCreated || biomeMatrixCount <= 0)
                return currentBiomeMatrixDataIndex;

            float layerDepthMeters = math.max(depthMeters, math.max(0f, -sampleY));
            int targetRole = ResolveRequiredVolumetricRole(layerDepthMeters);
            if (targetRole == (int)VolumetricBiomeRole.None &&
                IsBiomeDepthMatch(layerDepthMeters, currentBiomeMatrixDataIndex, biomeMatrices, biomeMatrixCount))
            {
                return currentBiomeMatrixDataIndex;
            }

            int bestIndex = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < biomeMatrixCount; i++)
            {
                BiomeMatrixData candidate = biomeMatrices[i];
                bool depthMatch = IsDepthWithinBand(layerDepthMeters, candidate.MinDepthMeters, candidate.MaxDepthMeters);
                if (targetRole == (int)VolumetricBiomeRole.None && !depthMatch)
                    continue;

                int score = candidate.FamilyDataIndex == preferredFamilyDataIndex ? 1000 : 0;
                if (targetRole != (int)VolumetricBiomeRole.None)
                {
                    int roleScore = ResolveVolumetricRoleScore(targetRole, candidate.VolumetricRole);
                    if (roleScore <= 0)
                        continue;

                    score += roleScore;
                }

                if (depthMatch)
                    score += 300;

                if (candidate.IsPlaceholder == 0)
                    score += 100;

                float bandSize = math.max(0.001f, candidate.MaxDepthMeters - candidate.MinDepthMeters);
                score += (int)math.round(50f / math.min(50f, bandSize));

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex >= 0 ? bestIndex : currentBiomeMatrixDataIndex;
        }

        private static int ResolveRequiredVolumetricRole(float layerDepthMeters)
        {
            if (layerDepthMeters >= 2000f)
                return (int)VolumetricBiomeRole.VolcanicHadal;

            if (layerDepthMeters >= 900f)
                return (int)VolumetricBiomeRole.AbyssalSilt;

            return (int)VolumetricBiomeRole.None;
        }

        private static int ResolveVolumetricRoleScore(int targetRole, int candidateRole)
        {
            if (targetRole == candidateRole)
                return 2200;

            if (targetRole == (int)VolumetricBiomeRole.VolcanicHadal &&
                candidateRole == (int)VolumetricBiomeRole.MetallicHadal)
            {
                return 1600;
            }

            if (targetRole == (int)VolumetricBiomeRole.AbyssalSilt &&
                candidateRole == (int)VolumetricBiomeRole.SedimentDrift)
            {
                return 900;
            }

            return 0;
        }

        private static bool IsBiomeDepthMatch(
            float depthMeters,
            int biomeMatrixDataIndex,
            NativeArray<BiomeMatrixData> biomeMatrices,
            int biomeMatrixCount)
        {
            if (biomeMatrixDataIndex < 0 || biomeMatrixDataIndex >= biomeMatrixCount || !biomeMatrices.IsCreated)
                return false;

            BiomeMatrixData current = biomeMatrices[biomeMatrixDataIndex];
            return IsDepthWithinBand(depthMeters, current.MinDepthMeters, current.MaxDepthMeters);
        }

        private static bool IsDepthWithinBand(float depthMeters, float minDepthMeters, float maxDepthMeters)
        {
            float minDepth = math.min(minDepthMeters, maxDepthMeters);
            float maxDepth = math.max(minDepthMeters, maxDepthMeters);
            if (maxDepth <= 0f && minDepth <= 0f)
                return true;

            return depthMeters >= minDepth && depthMeters <= maxDepth;
        }

        private static BiomeFamilyFlags ResolveBiomeFamilyFlags(
            int biomeFamilyDataIndex,
            NativeArray<BiomeFamilyData> biomeFamilies,
            int biomeFamilyCount)
        {
            if (!biomeFamilies.IsCreated ||
                biomeFamilyDataIndex < 0 ||
                biomeFamilyDataIndex >= biomeFamilyCount)
            {
                return BiomeFamilyFlags.None;
            }

            return biomeFamilies[biomeFamilyDataIndex].Flags;
        }

        private static int ResolveBiomeMatrixDataIndexFromMatrixId(
            int matrixBiomeId,
            NativeArray<int> biomeMatrixIdToDataIndex,
            int fallbackDataIndex)
        {
            if (!biomeMatrixIdToDataIndex.IsCreated ||
                matrixBiomeId <= 0 ||
                matrixBiomeId >= biomeMatrixIdToDataIndex.Length)
            {
                return fallbackDataIndex;
            }

            int dataIndex = biomeMatrixIdToDataIndex[matrixBiomeId];
            return dataIndex >= 0 ? dataIndex : fallbackDataIndex;
        }

        private static void ResolveSecondaryDomain(
            ref CellOutputData output,
            float waterSurface,
            NativeArray<CaveEntranceHintData> caveEntranceHints,
            int caveEntranceHintCount)
        {
            output.SecondarySampleValid = 0;
            output.SecondaryHeight = 0f;
            output.SecondaryDepthMeters = 0f;
            output.SecondaryCaveProximity = 0f;
            output.SecondaryDomainWeight = 0f;

            if (!caveEntranceHints.IsCreated || caveEntranceHintCount <= 0)
                return;

            float2 cellXZ = output.Position.xz;
            int bestHintIndex = -1;
            float bestHintWeight = 0f;

            for (int i = 0; i < caveEntranceHintCount; i++)
            {
                CaveEntranceHintData hint = caveEntranceHints[i];
                float influenceRadius = math.max(0.01f, hint.InfluenceRadius);
                float2 interiorXZ = hint.InteriorPosition.xz;
                float distanceSqr = math.lengthsq(cellXZ - interiorXZ);
                if (distanceSqr > influenceRadius * influenceRadius)
                    continue;

                float verticalDelta = output.SeafloorHeight - hint.InteriorPosition.y;
                if (verticalDelta < 2f)
                    continue;

                float radialWeight = 1f - math.saturate(distanceSqr / (influenceRadius * influenceRadius));
                float verticalWeight = math.saturate(verticalDelta / math.max(4f, hint.EntranceRadius + hint.InfluenceRadius));
                float combinedWeight = radialWeight * verticalWeight;
                if (combinedWeight <= bestHintWeight)
                    continue;

                bestHintIndex = i;
                bestHintWeight = combinedWeight;
            }

            if (bestHintIndex < 0)
                return;

            CaveEntranceHintData bestHint = caveEntranceHints[bestHintIndex];
            output.SecondaryHeight = bestHint.InteriorPosition.y;
            output.SecondaryDepthMeters = math.max(0f, waterSurface - output.SecondaryHeight);
            output.SecondaryCaveProximity = math.saturate(math.max(output.CaveProximity, 0.72f + (bestHintWeight * 0.28f)));
            output.SecondaryDomainWeight = bestHintWeight;
            output.SecondarySampleValid = 1;
        }

        private static void FillNoiseContext(ref CellOutputData output, NativeArray<ushort> noiseLookupTable, float fieldNoiseScale, float detailNoiseScale)
        {
            float x = output.Position.x;
            float z = output.Position.z;
            output.TerrainNoise = SampleNoiseLookup01(noiseLookupTable, x, z, fieldNoiseScale);
            output.DetailNoise = SampleNoiseLookup01(noiseLookupTable, x + 91.7f, z - 33.4f, detailNoiseScale);
            output.SedimentFieldNoise = SampleNoiseLookup01(noiseLookupTable, x - 218.6f, z + 57.4f, fieldNoiseScale * 0.74f);
            output.FertileFieldNoise = SampleNoiseLookup01(noiseLookupTable, x + 127.8f, z - 146.2f, detailNoiseScale * 0.78f);
            output.ReefFieldNoise = SampleNoiseLookup01(noiseLookupTable, x + 314.4f, z + 88.5f, detailNoiseScale * 0.58f);
            output.IndustrialFieldNoise = SampleNoiseLookup01(noiseLookupTable, x - 401.1f, z - 203.6f, fieldNoiseScale * 0.82f);
            output.HazardFieldNoise = SampleNoiseLookup01(noiseLookupTable, x + 261.7f, z - 318.3f, detailNoiseScale * 0.94f);
            output.LandmarkFieldNoise = SampleNoiseLookup01(noiseLookupTable, x - 83.2f, z + 367.9f, fieldNoiseScale * 0.62f);
            output.BasinFieldNoise = SampleNoiseLookup01(noiseLookupTable, x + 452.5f, z + 121.3f, detailNoiseScale * 0.66f);
            output.RuggedBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x + 173.4f, z - 117.2f, fieldNoiseScale * 0.9f);
            output.FertileBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x - 91.6f, z + 44.3f, fieldNoiseScale * 1.15f);
            output.ThermalBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x + 304.2f, z + 281.4f, detailNoiseScale * 0.92f);
            output.MetallicBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x - 211.5f, z + 96.7f, detailNoiseScale * 0.88f);
            output.CrystalBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x + 67.4f, z - 248.6f, detailNoiseScale * 0.84f);
            output.VoidBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x - 403.1f, z - 365.8f, fieldNoiseScale * 0.66f);
            output.ReefBiomeNoise = SampleNoiseLookup01(noiseLookupTable, x + 149.7f, z - 71.9f, detailNoiseScale * 0.9f);
            output.BasinMacroNoise = SampleNoiseLookup01(noiseLookupTable, x - 512.4f, z + 188.6f, fieldNoiseScale * 0.22f);
            output.ReefMacroNoise = SampleNoiseLookup01(noiseLookupTable, x + 417.2f, z - 153.3f, fieldNoiseScale * 0.24f);
            output.ServiceMacroNoise = SampleNoiseLookup01(noiseLookupTable, x - 286.5f, z + 407.8f, fieldNoiseScale * 0.21f);
            output.RiftMacroNoise = SampleNoiseLookup01(noiseLookupTable, x + 598.1f, z - 487.2f, fieldNoiseScale * 0.19f);
            output.CoralPatternNoise = SampleNoiseLookup01(noiseLookupTable, x + 153.4f, z - 74.7f, detailNoiseScale * 0.86f);
            output.CaveNoise = SampleNoiseLookup01(noiseLookupTable, x - 141.7f, z + 208.3f, fieldNoiseScale * 0.78f);
            output.CompositionNoise = SampleNoiseLookup01(noiseLookupTable, x + 387.2f, z - 291.4f, detailNoiseScale * 0.56f);
        }

        private static float SampleNoiseLookup01(NativeArray<ushort> noiseLookupTable, float x, float z, float scale)
        {
            if (!noiseLookupTable.IsCreated || noiseLookupTable.Length <= 0)
                return 0.5f;

            float safeScale = math.max(0.0001f, scale);
            float sampleX = math.frac(x * safeScale) * NoiseLookupResolution;
            float sampleZ = math.frac(z * safeScale) * NoiseLookupResolution;

            int x0 = (int)math.floor(sampleX) & NoiseLookupMask;
            int z0 = (int)math.floor(sampleZ) & NoiseLookupMask;
            int x1 = (x0 + 1) & NoiseLookupMask;
            int z1 = (z0 + 1) & NoiseLookupMask;
            float tx = sampleX - math.floor(sampleX);
            float tz = sampleZ - math.floor(sampleZ);

            float a = noiseLookupTable[z0 * NoiseLookupResolution + x0] * NoiseLookupValueScale;
            float b = noiseLookupTable[z0 * NoiseLookupResolution + x1] * NoiseLookupValueScale;
            float c = noiseLookupTable[z1 * NoiseLookupResolution + x0] * NoiseLookupValueScale;
            float d = noiseLookupTable[z1 * NoiseLookupResolution + x1] * NoiseLookupValueScale;

            float top = math.lerp(a, b, tx);
            float bottom = math.lerp(c, d, tx);
            return math.lerp(top, bottom, tz);
        }

        private static float NoiseTo01(float value)
        {
            return math.clamp((value * 0.5f) + 0.5f, 0f, 1f);
        }

        private static float FastLength2D(float x, float z)
        {
            float ax = math.abs(x);
            float az = math.abs(z);
            float maxAxis = math.max(ax, az);
            float minAxis = math.min(ax, az);
            return maxAxis + (minAxis * 0.375f);
        }

        private static float FastLength2D(float2 value)
        {
            return FastLength2D(value.x, value.y);
        }

        private static int ResolveZoneDataIndex(float2 positionXZ, NativeArray<ZoneData> zones, int zoneCount, int currentZoneDataIndex, out float zoneWeight)
        {
            int bestIndex = -1;
            float bestWeight = 0f;
            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; i < zoneCount; i++)
            {
                ZoneData zone = zones[i];
                float2 delta = zone.PositionXZ - positionXZ;
                float distanceSqr = math.lengthsq(delta);
                float distance = FastLength2D(delta);
                float noiseRadiusMultiplier = EvaluateZoneNoiseRadiusMultiplier(positionXZ, zone);
                float blend = math.max(4f, zone.EdgeBlendDistance);
                float activationWeight = EvaluateRadiusWeightFromDistance(distance, zone.ActivationRadius * noiseRadiusMultiplier, blend);
                if (activationWeight <= 0.001f)
                    continue;

                if (bestIndex < 0 || activationWeight > bestWeight || (math.abs(activationWeight - bestWeight) <= 0.0001f && distanceSqr < bestDistanceSqr))
                {
                    bestIndex = i;
                    bestWeight = activationWeight;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (bestIndex < 0 && currentZoneDataIndex >= 0 && currentZoneDataIndex < zoneCount)
            {
                ZoneData currentZone = zones[currentZoneDataIndex];
                float fallbackDistance = FastLength2D(positionXZ - currentZone.PositionXZ);
                float fallbackBlend = math.max(4f, currentZone.EdgeBlendDistance);
                float fallbackMultiplier = EvaluateZoneNoiseRadiusMultiplier(positionXZ, currentZone);
                bestWeight = EvaluateRadiusWeightFromDistance(fallbackDistance, currentZone.ActivationRadius * fallbackMultiplier, fallbackBlend);
                bestIndex = currentZoneDataIndex;
            }

            zoneWeight = math.max(0f, bestWeight);
            return bestWeight > 0.001f ? bestIndex : -1;
        }

        private static float EvaluateZoneNoiseRadiusMultiplier(float2 positionXZ, in ZoneData zone)
        {
            float scale = math.max(0.0001f, zone.EdgeNoiseScale);
            float2 sample = (positionXZ * scale) + zone.EdgeNoiseOffset;
            float coarse = noise.snoise(sample);
            float fine = noise.snoise(sample * 2.73f + new float2(19.31f, -41.77f));
            float shard = noise.snoise(sample * 6.11f + new float2(-7.13f, 83.29f));
            float centered = coarse * 0.62f + fine * 0.28f + shard * 0.10f;
            return math.clamp(1f + centered * zone.EdgeNoiseStrength, 0.65f, 1.45f);
        }

        private static float EvaluateRadiusWeightFromDistance(float distance, float noisyRadius, float blend)
        {
            float innerRadius = math.max(0f, noisyRadius - blend);
            if (distance <= innerRadius)
                return 1f;

            if (distance >= noisyRadius)
                return 0f;

            return 1f - math.unlerp(innerRadius, noisyRadius, distance);
        }

        private static WorldZoneAnchor.ZoneKind ResolveFallbackZoneKind(
            float depthMeters,
            float slopeDegrees,
            float fertileNoise,
            float thermalNoise,
            float metallicNoise,
            float voidNoise)
        {
            float shallow01 = 1f - math.clamp(depthMeters / 220f, 0f, 1f);
            float deep01 = math.clamp((depthMeters - 180f) / 900f, 0f, 1f);
            float steep01 = math.clamp((slopeDegrees - 10f) / 38f, 0f, 1f);
            float resourceScore = math.clamp((shallow01 * 0.4f) + (fertileNoise * 0.6f), 0f, 1f);
            float serviceScore = math.clamp((metallicNoise * 0.55f) + (thermalNoise * 0.45f), 0f, 1f);
            float hazardScore = math.clamp((deep01 * 0.4f) + (steep01 * 0.25f) + (voidNoise * 0.35f), 0f, 1f);

            if (serviceScore > 0.74f)
                return thermalNoise > 0.58f ? WorldZoneAnchor.ZoneKind.Power : WorldZoneAnchor.ZoneKind.Service;

            if (hazardScore > 0.72f)
                return deep01 > 0.6f ? WorldZoneAnchor.ZoneKind.Progression : WorldZoneAnchor.ZoneKind.Combat;

            if (resourceScore > 0.7f)
                return fertileNoise > 0.64f ? WorldZoneAnchor.ZoneKind.Resources : WorldZoneAnchor.ZoneKind.Fabrication;

            if (steep01 > 0.55f || deep01 > 0.38f)
                return WorldZoneAnchor.ZoneKind.Navigation;

            return WorldZoneAnchor.ZoneKind.Resources;
        }

        private static int ResolveFallbackBiomeFamilyIndex(
            float depthMeters,
            float slopeDegrees,
            WorldZoneAnchor.ZoneKind zoneKindHint,
            in CellOutputData output,
            int littoralKarstFamilyIndex,
            int fossilReefFamilyIndex,
            int sedimentDriftFamilyIndex,
            int abyssalSiltFamilyIndex,
            int graniteEscarpmentFamilyIndex,
            int tectonicSpineFamilyIndex,
            int riftSpineFamilyIndex,
            int riftVoidFamilyIndex,
            int volcanicGlassFamilyIndex,
            int volcanicHadalFamilyIndex,
            int metallicHadalFamilyIndex,
            int chemosyntheticBrineFamilyIndex,
            int crystalGrowthFamilyIndex)
        {
            float depth01 = math.clamp(depthMeters / 1200f, 0f, 1f);
            float steep01 = math.clamp((slopeDegrees - 8f) / 40f, 0f, 1f);
            float shallow01 = 1f - math.clamp(depthMeters / 220f, 0f, 1f);
            float resourceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Resources || zoneKindHint == WorldZoneAnchor.ZoneKind.Fabrication ? 1f : zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation ? 0.55f : 0f;
            float serviceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Service || zoneKindHint == WorldZoneAnchor.ZoneKind.Power ? 1f : 0f;
            float hazardZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Combat || zoneKindHint == WorldZoneAnchor.ZoneKind.Progression ? 1f : 0f;
            float navigationZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation ? 1f : 0f;

            float fertileScore = math.clamp(((output.FertileBiomeNoise * 0.65f) + (output.ReefBiomeNoise * 0.35f)) - (resourceZoneBias * 0.08f) - (serviceZoneBias * 0.16f) - (hazardZoneBias * 0.18f) + (navigationZoneBias * 0.08f), 0f, 1f);
            float ruggedScore = math.clamp((output.RuggedBiomeNoise * 0.55f) + (steep01 * 0.45f), 0f, 1f);
            float thermalScore = math.clamp((output.ThermalBiomeNoise * 0.75f) + (depth01 * 0.25f), 0f, 1f);
            float metallicScore = math.clamp((output.MetallicBiomeNoise * 0.7f) + (depth01 * 0.3f), 0f, 1f);
            float voidScore = math.clamp((output.VoidBiomeNoise * 0.7f) + (depth01 * 0.3f), 0f, 1f);
            float sedimentScore = math.clamp(((1f - ruggedScore) * 0.24f) + ((1f - thermalScore) * 0.14f) + (resourceZoneBias * 0.22f) + (shallow01 * 0.08f) + (output.FertileBiomeNoise * 0.12f) + (output.ReefBiomeNoise * 0.04f), 0f, 1f);
            float serviceScore = math.clamp((thermalScore * 0.34f) + (metallicScore * 0.34f) + (serviceZoneBias * 0.24f) + (depth01 * 0.08f), 0f, 1f);
            float hazardScore = math.clamp((ruggedScore * 0.28f) + (thermalScore * 0.16f) + (voidScore * 0.18f) + (hazardZoneBias * 0.26f) + (depth01 * 0.12f), 0f, 1f);
            float reefScore = math.clamp((fertileScore * 0.46f) + (output.ReefBiomeNoise * 0.28f) + (shallow01 * 0.14f) + (navigationZoneBias * 0.12f), 0f, 1f);
            float sedimentContinuity = math.clamp((resourceZoneBias * 0.28f) + (output.BasinMacroNoise * 0.24f) + ((1f - ruggedScore) * 0.12f) + ((1f - thermalScore) * 0.1f) + (shallow01 * 0.08f) + (depth01 * 0.06f) - (serviceZoneBias * 0.08f) - (hazardZoneBias * 0.1f), 0f, 1f);
            float reefContinuity = math.clamp((reefScore * 0.42f) + (output.ReefMacroNoise * 0.24f) + (fertileScore * 0.14f) + (navigationZoneBias * 0.08f) - (resourceZoneBias * 0.16f) - (serviceZoneBias * 0.08f) - (hazardZoneBias * 0.1f), 0f, 1f);
            float serviceContinuity = math.clamp((serviceScore * 0.46f) + (output.ServiceMacroNoise * 0.22f) + (metallicScore * 0.12f) + (thermalScore * 0.08f), 0f, 1f);
            float hazardContinuity = math.clamp((hazardScore * 0.48f) + (output.RiftMacroNoise * 0.24f) + (voidScore * 0.12f), 0f, 1f);

            if (depthMeters <= 180f)
            {
                if (serviceZoneBias > 0.58f && serviceContinuity > 0.62f)
                    return ChooseFamilyIndex(volcanicGlassFamilyIndex, tectonicSpineFamilyIndex, chemosyntheticBrineFamilyIndex);
                if (hazardZoneBias > 0.6f && hazardContinuity > 0.62f)
                    return ChooseFamilyIndex(riftSpineFamilyIndex, graniteEscarpmentFamilyIndex, volcanicGlassFamilyIndex);
                if (resourceZoneBias > 0.42f && sedimentContinuity > 0.56f)
                    return ChooseFamilyIndex(sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex, littoralKarstFamilyIndex);
                if (reefContinuity > 0.82f && output.CrystalBiomeNoise < 0.76f)
                    return ChooseFamilyIndex(fossilReefFamilyIndex, littoralKarstFamilyIndex, sedimentDriftFamilyIndex);
                if (output.CrystalBiomeNoise > 0.82f && reefContinuity > 0.7f && resourceZoneBias < 0.38f)
                    return ChooseFamilyIndex(crystalGrowthFamilyIndex, fossilReefFamilyIndex, littoralKarstFamilyIndex);
                if (sedimentScore > 0.62f || sedimentContinuity > 0.58f)
                    return ChooseFamilyIndex(sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex, littoralKarstFamilyIndex);
                if (ruggedScore > 0.7f)
                    return ChooseFamilyIndex(graniteEscarpmentFamilyIndex, tectonicSpineFamilyIndex, volcanicGlassFamilyIndex);
                if (resourceZoneBias > 0.35f)
                    return ChooseFamilyIndex(sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex, littoralKarstFamilyIndex);

                return shallow01 > 0.55f
                    ? ChooseFamilyIndex(littoralKarstFamilyIndex, sedimentDriftFamilyIndex, fossilReefFamilyIndex)
                    : ChooseFamilyIndex(sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex, abyssalSiltFamilyIndex);
            }

            if (depthMeters <= 600f)
            {
                if (serviceContinuity > 0.72f)
                    return ChooseFamilyIndex(volcanicGlassFamilyIndex, chemosyntheticBrineFamilyIndex, tectonicSpineFamilyIndex);
                if (hazardContinuity > 0.72f)
                    return ChooseFamilyIndex(riftSpineFamilyIndex, tectonicSpineFamilyIndex, graniteEscarpmentFamilyIndex);
                if ((sedimentScore > 0.68f && resourceZoneBias > 0.4f) || sedimentContinuity > 0.6f)
                    return ChooseFamilyIndex(abyssalSiltFamilyIndex, sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex);
                if (fertileScore > 0.66f && reefContinuity > 0.7f && resourceZoneBias < 0.34f)
                    return ChooseFamilyIndex(crystalGrowthFamilyIndex, fossilReefFamilyIndex, sedimentDriftFamilyIndex);
                if (metallicScore > 0.72f)
                    return ChooseFamilyIndex(chemosyntheticBrineFamilyIndex, metallicHadalFamilyIndex, abyssalSiltFamilyIndex);

                return ChooseFamilyIndex(abyssalSiltFamilyIndex, sedimentDriftFamilyIndex, graniteEscarpmentFamilyIndex);
            }

            if (voidScore > 0.76f && ruggedScore > 0.62f)
                return ChooseFamilyIndex(riftVoidFamilyIndex, volcanicHadalFamilyIndex, riftSpineFamilyIndex);
            if (thermalScore > 0.74f)
                return ChooseFamilyIndex(volcanicHadalFamilyIndex, chemosyntheticBrineFamilyIndex, volcanicGlassFamilyIndex);
            if (metallicScore > 0.72f)
                return ChooseFamilyIndex(metallicHadalFamilyIndex, chemosyntheticBrineFamilyIndex, abyssalSiltFamilyIndex);
            if (ruggedScore > 0.66f)
                return ChooseFamilyIndex(riftSpineFamilyIndex, tectonicSpineFamilyIndex, graniteEscarpmentFamilyIndex);
            if (fertileScore > 0.6f && output.CrystalBiomeNoise > 0.68f)
                return ChooseFamilyIndex(crystalGrowthFamilyIndex, chemosyntheticBrineFamilyIndex, abyssalSiltFamilyIndex);

            return ChooseFamilyIndex(abyssalSiltFamilyIndex, sedimentDriftFamilyIndex, riftVoidFamilyIndex);
        }

        private static int ChooseFamilyIndex(int firstChoice, int secondChoice, int thirdChoice)
        {
            if (firstChoice >= 0)
                return firstChoice;
            if (secondChoice >= 0)
                return secondChoice;
            return thirdChoice;
        }

        public struct BiomeEvaluationContext
        {
            public int ZoneDataIndex;
            public int ResolvedZoneKind;
            public int BiomeFamilyDataIndex;
            public NativeArray<ZoneData> Zones;
            public int ZoneCount;
            public NativeArray<BiomeMatrixData> BiomeMatrices;
            public int BiomeMatrixCount;
            public NativeArray<BiomeFamilyData> BiomeFamilies;
            public int BiomeFamilyCount;
        }

        private static float EvaluateRuggedBiomeBias(in BiomeEvaluationContext context)
        {
            if (context.ZoneDataIndex >= 0 && context.ZoneDataIndex < context.ZoneCount)
            {
                ZoneData zoneData = context.Zones[context.ZoneDataIndex];
                float familyBias = ContainsFamilyFlags(zoneData.DominantFamilyDataIndex, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass, context.BiomeFamilies, context.BiomeFamilyCount);
                if (zoneData.DominantMatrixDataIndex < 0 || zoneData.DominantMatrixDataIndex >= context.BiomeMatrixCount)
                    return math.lerp(0.25f, 1f, familyBias);

                BiomeMatrixData biomeData = context.BiomeMatrices[zoneData.DominantMatrixDataIndex];
                float rugged = math.clamp((biomeData.LandmarkStrength + biomeData.RoutePressure) / 10f, 0f, 1f);
                return math.clamp((rugged * 0.65f) + (familyBias * 0.35f), 0f, 1f);
            }

            float fallbackFamilyBias = ContainsFamilyFlags(context.BiomeFamilyDataIndex, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass, context.BiomeFamilies, context.BiomeFamilyCount);
            if (fallbackFamilyBias > 0f)
                return math.lerp(0.25f, 1f, fallbackFamilyBias);

            return context.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation || context.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Progression ? 0.56f : 0.38f;
        }

        private static float EvaluateFertileBiomeBias(int zoneDataIndex, int resolvedZoneKind, int biomeFamilyDataIndex, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeFamilyData> biomeFamilies, int biomeFamilyCount)
        {
            float familyBias = ContainsFamilyFlags(biomeFamilyDataIndex, BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Reef | BiomeFamilyFlags.Fossil | BiomeFamilyFlags.Crystal | BiomeFamilyFlags.Coral | BiomeFamilyFlags.Kelp | BiomeFamilyFlags.Growth, biomeFamilies, biomeFamilyCount);
            float zoneBias = EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Fabrication, (int)WorldZoneAnchor.ZoneKind.Navigation);
            return math.clamp((familyBias * 0.72f) + (zoneBias * 0.28f), 0f, 1f);
        }

        private static float EvaluateHazardBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Combat, (int)WorldZoneAnchor.ZoneKind.Progression, (int)WorldZoneAnchor.ZoneKind.Power);
            if (zoneDataIndex < 0 || zoneDataIndex >= zoneCount)
                return zoneBias;

            int matrixIndex = zones[zoneDataIndex].DominantMatrixDataIndex;
            if (matrixIndex < 0 || matrixIndex >= biomeMatrixCount)
                return zoneBias;

            BiomeMatrixData biomeData = biomeMatrices[matrixIndex];
            float biomeBias = math.clamp(math.max(biomeData.SurvivalPressure, biomeData.RoutePressure) / 5f, 0f, 1f);
            return math.clamp((zoneBias * 0.55f) + (biomeBias * 0.45f), 0f, 1f);
        }

        private static float EvaluateServiceBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount)
        {
            return EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Service, (int)WorldZoneAnchor.ZoneKind.Power, (int)WorldZoneAnchor.ZoneKind.Construction, (int)WorldZoneAnchor.ZoneKind.Progression);
        }

        private static float EvaluateResourceBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Resources, (int)WorldZoneAnchor.ZoneKind.Navigation, (int)WorldZoneAnchor.ZoneKind.Fabrication);
            if (zoneDataIndex < 0 || zoneDataIndex >= zoneCount)
                return zoneBias;

            int matrixIndex = zones[zoneDataIndex].DominantMatrixDataIndex;
            if (matrixIndex < 0 || matrixIndex >= biomeMatrixCount)
                return zoneBias;

            BiomeMatrixData biomeData = biomeMatrices[matrixIndex];
            float biomeBias = math.clamp(math.max(biomeData.CommonResourceBias, biomeData.UncommonResourceBias) / 5f, 0f, 1f);
            return math.clamp((zoneBias * 0.6f) + (biomeBias * 0.4f), 0f, 1f);
        }

        private static float EvaluateShelterBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount)
        {
            return EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Fabrication, (int)WorldZoneAnchor.ZoneKind.Navigation, (int)WorldZoneAnchor.ZoneKind.Resources, (int)WorldZoneAnchor.ZoneKind.Service);
        }

        private static float EvaluateLandmarkBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, resolvedZoneKind, zones, zoneCount, (int)WorldZoneAnchor.ZoneKind.Navigation, (int)WorldZoneAnchor.ZoneKind.Progression, (int)WorldZoneAnchor.ZoneKind.Combat);
            if (zoneDataIndex < 0 || zoneDataIndex >= zoneCount)
                return zoneBias;

            int matrixIndex = zones[zoneDataIndex].DominantMatrixDataIndex;
            if (matrixIndex < 0 || matrixIndex >= biomeMatrixCount)
                return zoneBias;

            BiomeMatrixData biomeData = biomeMatrices[matrixIndex];
            float biomeBias = math.clamp(math.max(biomeData.LandmarkStrength, biomeData.RewardPull) / 5f, 0f, 1f);
            return math.clamp((zoneBias * 0.45f) + (biomeBias * 0.55f), 0f, 1f);
        }

        private static float EvaluateZoneBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, int primaryKind, int secondaryKind)
        {
            int effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, resolvedZoneKind, zones, zoneCount);
            return effectiveKind == primaryKind || effectiveKind == secondaryKind ? 1f : 0.26f;
        }

        private static float EvaluateZoneBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, int primaryKind, int secondaryKind, int tertiaryKind)
        {
            int effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, resolvedZoneKind, zones, zoneCount);
            return effectiveKind == primaryKind || effectiveKind == secondaryKind || effectiveKind == tertiaryKind ? 1f : 0.26f;
        }

        private static float EvaluateZoneBias(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount, int primaryKind, int secondaryKind, int tertiaryKind, int quaternaryKind)
        {
            int effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, resolvedZoneKind, zones, zoneCount);
            return effectiveKind == primaryKind || effectiveKind == secondaryKind || effectiveKind == tertiaryKind || effectiveKind == quaternaryKind ? 1f : 0.26f;
        }

        private static int ResolveEffectiveZoneKind(int zoneDataIndex, int resolvedZoneKind, NativeArray<ZoneData> zones, int zoneCount)
        {
            if (zoneDataIndex >= 0 && zoneDataIndex < zoneCount)
                return zones[zoneDataIndex].Kind;

            return resolvedZoneKind;
        }

        private static float ContainsFamilyFlags(int familyDataIndex, BiomeFamilyFlags flags, NativeArray<BiomeFamilyData> biomeFamilies, int biomeFamilyCount)
        {
            if (familyDataIndex < 0 || familyDataIndex >= biomeFamilyCount)
                return 0f;

            return (biomeFamilies[familyDataIndex].Flags & flags) != 0 ? 1f : 0f;
        }

        private static WorldProceduralPattern ResolvePattern(CellOutputData output, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount, NativeArray<BiomeFamilyData> biomeFamilies, int biomeFamilyCount)
        {
            float shallow01 = 1f - math.clamp(output.DepthMeters / 220f, 0f, 1f);
            float deep01 = math.clamp((output.DepthMeters - 180f) / 900f, 0f, 1f);
            float steep01 = math.clamp((output.SlopeDegrees - 10f) / 36f, 0f, 1f);
            float sedimentTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Sediment | BiomeFamilyFlags.Drift | BiomeFamilyFlags.Silt | BiomeFamilyFlags.Granite, biomeFamilies, biomeFamilyCount);
            float brineTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Brine | BiomeFamilyFlags.Chemo | BiomeFamilyFlags.Saline, biomeFamilies, biomeFamilyCount);
            float volcanicTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Glass | BiomeFamilyFlags.Magma | BiomeFamilyFlags.Basalt, biomeFamilies, biomeFamilyCount);
            float industrialTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Metallic | BiomeFamilyFlags.Industrial | BiomeFamilyFlags.Service, biomeFamilies, biomeFamilyCount);
            float riftTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Void | BiomeFamilyFlags.Hadal, biomeFamilies, biomeFamilyCount);
            float softWaterTokenBias = ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Reef | BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Fossil | BiomeFamilyFlags.Crystal | BiomeFamilyFlags.Coral | BiomeFamilyFlags.Kelp | BiomeFamilyFlags.Growth, biomeFamilies, biomeFamilyCount);

            if (softWaterTokenBias > 0.5f &&
                output.FertileBias > 0.58f &&
                output.ServiceBias < 0.78f &&
                output.HazardBias < 0.78f)
            {
                return output.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation || output.LandmarkBias > 0.72f || output.CoralPatternNoise > 0.68f
                    ? WorldProceduralPattern.ReefNavigation
                    : WorldProceduralPattern.FertileShallows;
            }

            if (output.LandmarkBias > 0.82f && (steep01 > 0.42f || output.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation || output.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Progression))
                return WorldProceduralPattern.LandmarkCorridor;
            if (brineTokenBias > 0.55f && (output.ServiceBias > 0.46f || output.HazardBias > 0.42f))
                return WorldProceduralPattern.BrineToxic;
            if (volcanicTokenBias > 0.55f && (steep01 > 0.34f || output.LandmarkBias > 0.5f || output.HazardBias > 0.42f))
                return WorldProceduralPattern.VolcanicPressure;
            if (output.ServiceBias > 0.82f)
                return WorldProceduralPattern.IndustrialService;
            if (output.HazardBias > 0.82f)
                return volcanicTokenBias > 0.46f ? WorldProceduralPattern.VolcanicPressure : WorldProceduralPattern.RiftHazard;
            if (sedimentTokenBias > 0.5f && (output.ResourceBias > 0.58f || output.ShelterBias > 0.58f))
                return WorldProceduralPattern.SedimentResources;
            if (output.DepthMeters > 820f && output.FertileBias < 0.44f && output.ShelterBias < 0.5f && output.ServiceBias < 0.62f)
                return WorldProceduralPattern.AbyssSparse;
            if (output.FertileBias > 0.74f)
                return output.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation || output.LandmarkBias > 0.72f || output.CoralPatternNoise > 0.72f
                    ? WorldProceduralPattern.ReefNavigation
                    : WorldProceduralPattern.FertileShallows;
            if (output.ResourceBias > 0.68f || output.ShelterBias > 0.64f)
                return WorldProceduralPattern.SedimentResources;
            if (brineTokenBias > 0.5f)
                return WorldProceduralPattern.BrineToxic;
            if (volcanicTokenBias > 0.5f)
                return WorldProceduralPattern.VolcanicPressure;
            if (industrialTokenBias > 0.5f)
                return WorldProceduralPattern.IndustrialService;
            if (riftTokenBias > 0.5f)
                return output.HazardBias > 0.58f ? WorldProceduralPattern.RiftHazard : WorldProceduralPattern.LandmarkCorridor;
            if (softWaterTokenBias > 0.5f)
                return output.ResolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation ? WorldProceduralPattern.ReefNavigation : WorldProceduralPattern.FertileShallows;
            if (deep01 > 0.7f)
                return WorldProceduralPattern.AbyssSparse;
            if (output.LandmarkBias > 0.68f)
                return WorldProceduralPattern.LandmarkCorridor;

            return shallow01 > 0.45f ? WorldProceduralPattern.SedimentResources : WorldProceduralPattern.AbyssSparse;
        }

        private static void ComputeHeatChannels(ref CellOutputData output, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            output.RockDensityHeat = ResolveChannelHeat(0, in output, biomeMatrices, biomeMatrixCount);
            output.KelpDensityHeat = ResolveChannelHeat(1, in output, biomeMatrices, biomeMatrixCount);
            output.FloraDensityHeat = ResolveChannelHeat(2, in output, biomeMatrices, biomeMatrixCount);
            output.CoralDensityHeat = ResolveChannelHeat(3, in output, biomeMatrices, biomeMatrixCount);
            output.BioDensityHeat = ResolveChannelHeat(4, in output, biomeMatrices, biomeMatrixCount);
            output.DebrisDensityHeat = ResolveChannelHeat(5, in output, biomeMatrices, biomeMatrixCount);
            output.RuinDensityHeat = ResolveChannelHeat(6, in output, biomeMatrices, biomeMatrixCount);
            output.CaveDensityHeat = ResolveChannelHeat(7, in output, biomeMatrices, biomeMatrixCount);
            output.LandmarkStrengthHeat = ResolveChannelHeat(8, in output, biomeMatrices, biomeMatrixCount);
            output.FaunaDensityHeat = ResolveChannelHeat(9, in output, biomeMatrices, biomeMatrixCount);
            output.HazardDensityHeat = ResolveChannelHeat(10, in output, biomeMatrices, biomeMatrixCount);
            output.ResourceDensityHeat = ResolveChannelHeat(11, in output, biomeMatrices, biomeMatrixCount);
            output.ShelterDensityHeat = ResolveChannelHeat(12, in output, biomeMatrices, biomeMatrixCount);
            output.ServiceDensityHeat = ResolveChannelHeat(13, in output, biomeMatrices, biomeMatrixCount);
            output.GenericHeat = ResolveChannelHeat(14, in output, biomeMatrices, biomeMatrixCount);
        }

        private static void ApplyTectonicSpineSteepSlopeHeatBias(ref CellOutputData output)
        {
            BiomeFamilyFlags flags = (BiomeFamilyFlags)output.BiomeFamilyFlags;
            if ((flags & BiomeFamilyFlags.Tectonic) == 0 || output.SlopeDegrees < 45f)
                return;

            float slope01 = math.saturate((output.SlopeDegrees - 45f) / 30f);
            output.RockDensityHeat = math.max(output.RockDensityHeat, 0.74f + slope01 * 0.20f);
            output.DebrisDensityHeat = math.max(output.DebrisDensityHeat, 0.80f + slope01 * 0.18f);
            output.LandmarkStrengthHeat = math.max(output.LandmarkStrengthHeat, 0.62f + slope01 * 0.18f);
            output.HazardDensityHeat = math.max(output.HazardDensityHeat, 0.68f + slope01 * 0.18f);
            output.CanyonSignal = math.max(output.CanyonSignal, 0.72f + slope01 * 0.18f);
            output.CompositionPotential = math.max(output.CompositionPotential, 0.74f + slope01 * 0.16f);
        }

        private static float ResolveChannelHeat(int channelIndex, in CellOutputData output, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            float shallow01 = 1f - math.clamp(output.DepthMeters / 220f, 0f, 1f);
            float midDepth01 = 1f - math.clamp(math.abs(output.DepthMeters - 260f) / 320f, 0f, 1f);
            float deep01 = math.clamp((output.DepthMeters - 180f) / 900f, 0f, 1f);
            float abyss01 = math.clamp((output.DepthMeters - 900f) / 1800f, 0f, 1f);
            float flat01 = 1f - math.clamp(output.SlopeDegrees / 28f, 0f, 1f);
            float steep01 = math.clamp((output.SlopeDegrees - 8f) / 40f, 0f, 1f);
            float biomeMatrixBonus = EvaluateBiomeMatrixChannelBonus(channelIndex, output.BiomeMatrixDataIndex, biomeMatrices, biomeMatrixCount);

            float baseValue = channelIndex switch
            {
                0 => 0.24f + steep01 * 0.34f + deep01 * 0.16f + output.RuggedBias * 0.16f + output.TerrainNoise * 0.16f,
                1 => shallow01 * 0.44f + flat01 * 0.18f + output.FertileBias * 0.2f + output.TerrainNoise * 0.18f,
                2 => shallow01 * 0.34f + flat01 * 0.12f + output.FertileBias * 0.3f + output.DetailNoise * 0.24f,
                3 => shallow01 * 0.24f + midDepth01 * 0.24f + flat01 * 0.14f + output.FertileBias * 0.22f + output.TerrainNoise * 0.16f,
                4 => output.FertileBias * 0.36f + shallow01 * 0.16f + output.ShelterBias * 0.16f + output.DetailNoise * 0.2f + (1f - output.HazardBias) * 0.12f,
                5 => output.ServiceBias * 0.34f + midDepth01 * 0.16f + output.TerrainNoise * 0.22f + output.DetailNoise * 0.14f + output.RuggedBias * 0.14f,
                6 => output.ServiceBias * 0.38f + deep01 * 0.12f + output.TerrainNoise * 0.2f + output.LandmarkBias * 0.18f + flat01 * 0.12f,
                7 => steep01 * 0.34f + output.RuggedBias * 0.22f + deep01 * 0.18f + output.TerrainNoise * 0.18f + output.HazardBias * 0.08f,
                8 => steep01 * 0.24f + output.LandmarkBias * 0.34f + abyss01 * 0.1f + output.TerrainNoise * 0.18f + output.RuggedBias * 0.14f,
                9 => output.FertileBias * 0.34f + shallow01 * 0.16f + output.ShelterBias * 0.22f + output.DetailNoise * 0.16f + (1f - steep01) * 0.12f,
                10 => output.HazardBias * 0.42f + deep01 * 0.12f + steep01 * 0.14f + output.TerrainNoise * 0.18f + output.LandmarkBias * 0.14f,
                11 => output.ResourceBias * 0.34f + deep01 * 0.08f + output.TerrainNoise * 0.2f + output.RuggedBias * 0.18f + output.DetailNoise * 0.2f,
                12 => output.ShelterBias * 0.34f + flat01 * 0.26f + shallow01 * 0.08f + output.FertileBias * 0.12f + output.DetailNoise * 0.2f,
                13 => output.ServiceBias * 0.44f + output.TerrainNoise * 0.2f + output.RuggedBias * 0.1f + flat01 * 0.1f + output.LandmarkBias * 0.16f,
                _ => output.TerrainNoise * 0.55f + output.DetailNoise * 0.45f
            };
            baseValue = math.clamp(baseValue + biomeMatrixBonus, 0f, 1f);

            float sedimentField = math.clamp(output.ResourceBias * 0.32f + output.ShelterBias * 0.18f + flat01 * 0.16f + output.TerrainNoise * 0.14f + output.SedimentFieldNoise * 0.20f, 0f, 1f);
            float fertileField = math.clamp(output.FertileBias * 0.34f + shallow01 * 0.16f + output.DetailNoise * 0.12f + output.FertileFieldNoise * 0.22f + output.ShelterBias * 0.08f + (1f - output.HazardBias) * 0.08f, 0f, 1f);
            float reefField = math.clamp(output.FertileBias * 0.24f + output.LandmarkBias * 0.14f + shallow01 * 0.10f + output.ReefFieldNoise * 0.24f + flat01 * 0.08f + output.DetailNoise * 0.12f + midDepth01 * 0.08f, 0f, 1f);
            float industrialField = math.clamp(output.ServiceBias * 0.34f + output.IndustrialFieldNoise * 0.28f + output.TerrainNoise * 0.10f + output.RuggedBias * 0.08f + deep01 * 0.08f + output.LandmarkBias * 0.12f, 0f, 1f);
            float hazardField = math.clamp(output.HazardBias * 0.38f + steep01 * 0.12f + deep01 * 0.12f + output.HazardFieldNoise * 0.24f + output.RuggedBias * 0.14f, 0f, 1f);
            float landmarkField = math.clamp(output.LandmarkBias * 0.34f + steep01 * 0.16f + output.LandmarkFieldNoise * 0.26f + output.RuggedBias * 0.10f + deep01 * 0.08f + reefField * 0.06f, 0f, 1f);
            float shelterField = math.clamp(output.ShelterBias * 0.34f + flat01 * 0.18f + fertileField * 0.14f + output.BasinFieldNoise * 0.18f + output.DetailNoise * 0.16f, 0f, 1f);
            float abyssField = math.clamp(abyss01 * 0.44f + hazardField * 0.16f + output.RuggedBias * 0.12f + output.TerrainNoise * 0.12f + output.IndustrialFieldNoise * 0.08f + (1f - fertileField) * 0.08f, 0f, 1f);

            PatternHeatContext heatContext = new PatternHeatContext
            {
                SedimentField = sedimentField,
                FertileField = fertileField,
                ReefField = reefField,
                IndustrialField = industrialField,
                HazardField = hazardField,
                LandmarkField = landmarkField,
                ShelterField = shelterField,
                AbyssField = abyssField,
                RuggedBias = output.RuggedBias,
                TerrainNoise = output.TerrainNoise,
                DetailNoise = output.DetailNoise
            };
            float shapedValue = ResolvePatternShapedHeat(channelIndex, output.ResolvedPattern, in heatContext);
            shapedValue = math.clamp(shapedValue + biomeMatrixBonus * 0.92f, 0f, 1f);
            float blend = ResolvePatternFieldBlend((SeafloorSource)output.SeafloorSource, output.ZoneDataIndex >= 0);
            return math.clamp(math.lerp(baseValue, shapedValue, blend), 0f, 1f);
        }

        // Bounds of the industrial evidence field that shallow photic water can actually produce.
        // industrialField = ServiceBias * 0.34 + IndustrialFieldNoise * 0.28 + TerrainNoise * 0.10
        //                 + RuggedBias * 0.08 + deep01 * 0.08 + LandmarkBias * 0.12
        // Outside a Service/Power/Construction/Progression zone EvaluateServiceBias returns the 0.26
        // non-match floor (EvaluateZoneBias), deep01 is 0 above 180 m, and soft-water families carry a
        // low RuggedBias, so the reachable band on the first shallow route is roughly 0.14 .. 0.52 and
        // never approaches 1. A flat multiplier against that band is what made every technogenic
        // channel unreachable on the photic route.
        private const float ShallowTechnogenicTraceOnset = 0.20f;
        private const float ShallowTechnogenicTraceSaturation = 0.48f;

        /// <summary>
        /// Shallow-shelf technogenic trace curve for the two photic patterns (FertileShallows,
        /// ReefNavigation).
        ///
        /// world.md:35 and VISION_LOCKS.md:70 require the shallows to carry technogenic history "in
        /// places" - colony traces, wreck fragments, route hardware, pipes, cables, salvage cuts -
        /// and world.md:42 lists industrial intrusion as one of the five layers an area must have.
        /// TASTE.md "The Ocean Contains Structure" rejects a featureless seabed with no navigational
        /// history.
        ///
        /// A flat multiplier cannot express "in places". Scaling the reachable shallow industrial
        /// band (see the constants above) by 0.22 - 0.26 caps the channel near 0.13, below every
        /// technogenic floor authored against these channels (0.30 rule.debris.scatter and
        /// rule.service.scar, 0.32 rule.route.power, 0.36 rule.ruin.cluster.medium, 0.40
        /// rule.debris.field), so the content was solicited and could never place. Raising the whole
        /// curve instead would spread scrap evenly over open shelf water, which world.md:18 and
        /// world.md:160 reject.
        ///
        /// So this is a contrast ramp, not a gain: below the onset the shelf stays cleaner than the
        /// old linear scale left it, and only the top of the industrial band climbs to
        /// <paramref name="tracePeak"/>.
        ///
        /// Peaks are sized against the EFFECTIVE gate, not the raw channel. The scatter path
        /// multiplies this channel by an authored per-domain pattern affinity before comparing it to
        /// the rule floor (WorldProceduralScatterDirector.cs GetPatternHeatScale, :11165 onward,
        /// applied at :11299 and consumed at WorldProceduralScatterDirectorSamplingPipeline.cs:711):
        /// on FertileShallows Debris is 0.72 and ServiceScar/PowerRoute are 0.68, on ReefNavigation
        /// 0.82 and 0.78, while RuinModule is unlisted and therefore 1.0. A peak chosen against the
        /// bare floor is silently discounted by 18 - 32 % and fails the gate anyway, which is half of
        /// why this route carried no technogenic content. Effective ceilings here are 0.36 debris /
        /// 0.38 service on FertileShallows and 0.34 / 0.38 on ReefNavigation: above the 0.30 and 0.32
        /// floors, deliberately below the 0.40 rule.debris.field floor so dense wreck strips stay off
        /// the photic shelf, and below the effective SedimentResources band (0.42 debris,
        /// 0.48 * 1.02 service) and far below IndustrialService (0.90 * 1.18, 0.96 * 1.18). The
        /// industrial ladder still reads shelf &lt; sediment flat &lt; service water.
        ///
        /// The raw shelf peaks sit numerically above the SedimentResources shaping constants; that is
        /// the affinity discount being paid back, and it is also the honest shape of the two places.
        /// The shelf carries concentrated evidence - this ramp only saturates in the top of the
        /// industrial band, a small fraction of shelf cells - while the sediment flat carries a broad
        /// linear industrial presence across its whole field.
        /// </summary>
        private static float ShapeShallowTechnogenicTrace(float industrialField, float tracePeak)
        {
            float trace = math.smoothstep(
                ShallowTechnogenicTraceOnset,
                ShallowTechnogenicTraceSaturation,
                industrialField);
            return trace * tracePeak;
        }

        private static float ResolvePatternShapedHeat(int channelIndex, int resolvedPattern, in PatternHeatContext context)
        {
            return (WorldProceduralPattern)resolvedPattern switch
            {
                WorldProceduralPattern.FertileShallows => channelIndex switch
                {
                    0 => 0.18f + context.SedimentField * 0.22f + context.RuggedBias * 0.12f + context.ShelterField * 0.08f,
                    1 => context.FertileField * 0.92f,
                    2 => context.FertileField * 0.84f,
                    3 => context.ReefField * 0.90f,
                    4 => context.FertileField * 0.62f + context.ShelterField * 0.24f,
                    5 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.50f),
                    6 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.34f) + context.LandmarkField * 0.12f,
                    7 => context.LandmarkField * 0.28f + context.HazardField * 0.16f,
                    8 => context.LandmarkField * 0.48f + context.ReefField * 0.12f,
                    9 => context.FertileField * 0.56f + context.ShelterField * 0.30f,
                    10 => context.HazardField * 0.26f,
                    11 => context.SedimentField * 0.40f + context.FertileField * 0.18f,
                    12 => context.ShelterField * 0.78f,
                    13 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.56f),
                    _ => context.FertileField * 0.58f + context.SedimentField * 0.14f
                },
                WorldProceduralPattern.ReefNavigation => channelIndex switch
                {
                    0 => 0.20f + context.SedimentField * 0.18f + context.RuggedBias * 0.12f,
                    1 => context.FertileField * 0.72f + context.ReefField * 0.14f,
                    2 => context.FertileField * 0.70f + context.ReefField * 0.12f,
                    3 => context.ReefField * 0.94f,
                    4 => context.FertileField * 0.44f + context.ShelterField * 0.22f,
                    5 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.42f),
                    6 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.32f) + context.LandmarkField * 0.14f,
                    7 => context.LandmarkField * 0.38f + context.HazardField * 0.18f,
                    8 => context.LandmarkField * 0.68f + context.ReefField * 0.16f,
                    9 => context.FertileField * 0.42f + context.ShelterField * 0.18f,
                    10 => context.HazardField * 0.28f,
                    11 => context.SedimentField * 0.32f + context.LandmarkField * 0.12f,
                    12 => context.ShelterField * 0.54f + context.ReefField * 0.12f,
                    13 => ShapeShallowTechnogenicTrace(context.IndustrialField, 0.49f),
                    _ => context.ReefField * 0.56f + context.LandmarkField * 0.18f
                },
                WorldProceduralPattern.SedimentResources => channelIndex switch
                {
                    0 => 0.18f + context.SedimentField * 0.86f + context.RuggedBias * 0.12f,
                    1 => context.FertileField * 0.24f + context.ShelterField * 0.10f,
                    2 => context.FertileField * 0.14f + context.ShelterField * 0.08f,
                    3 => context.ReefField * 0.14f + context.FertileField * 0.06f,
                    4 => context.ShelterField * 0.52f + context.FertileField * 0.12f,
                    5 => context.IndustrialField * 0.42f + context.HazardField * 0.08f,
                    6 => context.IndustrialField * 0.44f + context.LandmarkField * 0.22f + context.SedimentField * 0.08f,
                    7 => context.HazardField * 0.30f + context.LandmarkField * 0.30f + context.RuggedBias * 0.18f + context.SedimentField * 0.06f,
                    8 => context.LandmarkField * 0.58f + context.SedimentField * 0.14f + context.RuggedBias * 0.08f,
                    9 => context.ShelterField * 0.42f + context.FertileField * 0.14f,
                    10 => context.HazardField * 0.34f,
                    11 => context.SedimentField * 0.92f,
                    12 => context.ShelterField * 0.88f,
                    13 => context.IndustrialField * 0.48f + context.SedimentField * 0.08f + context.LandmarkField * 0.06f,
                    _ => context.SedimentField * 0.62f + context.ShelterField * 0.18f
                },
                WorldProceduralPattern.IndustrialService => channelIndex switch
                {
                    0 => 0.18f + context.SedimentField * 0.34f + context.RuggedBias * 0.10f,
                    1 => context.FertileField * 0.18f,
                    2 => context.FertileField * 0.16f,
                    3 => context.ReefField * 0.14f,
                    4 => context.ShelterField * 0.24f,
                    5 => context.IndustrialField * 0.90f,
                    6 => context.IndustrialField * 0.76f + context.LandmarkField * 0.12f,
                    7 => context.HazardField * 0.22f + context.LandmarkField * 0.18f + context.IndustrialField * 0.12f,
                    8 => context.LandmarkField * 0.44f + context.IndustrialField * 0.22f,
                    9 => context.HazardField * 0.16f + context.ShelterField * 0.14f,
                    10 => context.HazardField * 0.46f + context.IndustrialField * 0.12f,
                    11 => context.SedimentField * 0.26f + context.IndustrialField * 0.12f,
                    12 => context.ShelterField * 0.22f,
                    13 => context.IndustrialField * 0.96f,
                    _ => context.IndustrialField * 0.64f + context.LandmarkField * 0.14f
                },
                WorldProceduralPattern.BrineToxic => channelIndex switch
                {
                    0 => 0.16f + context.SedimentField * 0.28f + context.IndustrialField * 0.18f + context.RuggedBias * 0.08f,
                    1 => context.FertileField * 0.08f,
                    2 => context.FertileField * 0.10f,
                    3 => context.ReefField * 0.08f,
                    4 => context.FertileField * 0.16f + context.ShelterField * 0.12f + context.HazardField * 0.08f,
                    5 => context.IndustrialField * 0.82f,
                    6 => context.IndustrialField * 0.58f + context.LandmarkField * 0.14f,
                    7 => context.HazardField * 0.24f + context.LandmarkField * 0.18f + context.IndustrialField * 0.12f,
                    8 => context.LandmarkField * 0.36f + context.IndustrialField * 0.18f,
                    9 => context.FertileField * 0.12f + context.HazardField * 0.14f,
                    10 => context.HazardField * 0.54f + context.IndustrialField * 0.12f,
                    11 => context.SedimentField * 0.24f + context.IndustrialField * 0.14f,
                    12 => context.ShelterField * 0.18f,
                    13 => context.IndustrialField * 0.82f,
                    _ => context.IndustrialField * 0.62f + context.HazardField * 0.10f
                },
                WorldProceduralPattern.VolcanicPressure => channelIndex switch
                {
                    0 => 0.20f + context.SedimentField * 0.46f + context.RuggedBias * 0.18f + context.HazardField * 0.10f,
                    1 => context.FertileField * 0.06f,
                    2 => context.FertileField * 0.08f,
                    3 => context.ReefField * 0.06f,
                    4 => context.FertileField * 0.10f + context.HazardField * 0.10f + context.AbyssField * 0.06f,
                    5 => context.IndustrialField * 0.34f + context.HazardField * 0.16f,
                    6 => context.IndustrialField * 0.42f + context.LandmarkField * 0.18f + context.HazardField * 0.12f,
                    7 => context.LandmarkField * 0.48f + context.HazardField * 0.28f + context.RuggedBias * 0.10f,
                    8 => context.LandmarkField * 0.86f + context.HazardField * 0.10f,
                    9 => context.HazardField * 0.18f + context.AbyssField * 0.10f,
                    10 => context.HazardField * 0.76f,
                    11 => context.SedimentField * 0.22f + context.HazardField * 0.10f,
                    12 => context.ShelterField * 0.14f,
                    13 => context.IndustrialField * 0.42f + context.HazardField * 0.10f,
                    _ => context.LandmarkField * 0.52f + context.HazardField * 0.16f + context.SedimentField * 0.12f
                },
                WorldProceduralPattern.RiftHazard => channelIndex switch
                {
                    0 => 0.18f + context.HazardField * 0.36f + context.RuggedBias * 0.18f + context.SedimentField * 0.16f,
                    1 => context.FertileField * 0.10f,
                    2 => context.FertileField * 0.12f,
                    3 => context.ReefField * 0.10f,
                    4 => context.HazardField * 0.24f + context.AbyssField * 0.10f,
                    5 => context.IndustrialField * 0.36f + context.HazardField * 0.12f,
                    6 => context.IndustrialField * 0.42f + context.HazardField * 0.18f + context.LandmarkField * 0.10f,
                    7 => context.HazardField * 0.82f,
                    8 => context.LandmarkField * 0.52f + context.HazardField * 0.16f,
                    9 => context.HazardField * 0.48f + context.AbyssField * 0.18f,
                    10 => context.HazardField * 0.98f,
                    11 => context.SedimentField * 0.24f + context.HazardField * 0.10f,
                    12 => context.ShelterField * 0.18f,
                    13 => context.IndustrialField * 0.34f,
                    _ => context.HazardField * 0.64f + context.IndustrialField * 0.14f
                },
                WorldProceduralPattern.AbyssSparse => channelIndex switch
                {
                    0 => 0.20f + context.AbyssField * 0.44f + context.RuggedBias * 0.16f + context.SedimentField * 0.18f,
                    1 => context.FertileField * 0.06f,
                    2 => context.FertileField * 0.08f,
                    3 => context.ReefField * 0.08f,
                    4 => context.AbyssField * 0.18f + context.ShelterField * 0.10f,
                    5 => context.IndustrialField * 0.18f + context.AbyssField * 0.08f,
                    6 => context.IndustrialField * 0.22f + context.LandmarkField * 0.18f,
                    7 => context.HazardField * 0.22f + context.LandmarkField * 0.22f,
                    8 => context.LandmarkField * 0.48f + context.AbyssField * 0.12f,
                    9 => context.AbyssField * 0.16f,
                    10 => context.HazardField * 0.24f + context.AbyssField * 0.12f,
                    11 => context.SedimentField * 0.18f + context.AbyssField * 0.08f,
                    12 => context.ShelterField * 0.14f,
                    13 => context.IndustrialField * 0.16f,
                    _ => context.AbyssField * 0.52f + context.LandmarkField * 0.12f
                },
                WorldProceduralPattern.LandmarkCorridor => channelIndex switch
                {
                    0 => 0.22f + context.SedimentField * 0.26f + context.RuggedBias * 0.18f,
                    1 => context.FertileField * 0.24f,
                    2 => context.FertileField * 0.22f + context.LandmarkField * 0.08f,
                    3 => context.ReefField * 0.28f,
                    4 => context.ShelterField * 0.22f + context.FertileField * 0.10f,
                    5 => context.IndustrialField * 0.26f,
                    6 => context.IndustrialField * 0.34f + context.LandmarkField * 0.24f,
                    7 => context.LandmarkField * 0.84f,
                    8 => context.LandmarkField * 0.98f,
                    9 => context.ShelterField * 0.18f + context.HazardField * 0.10f,
                    10 => context.HazardField * 0.34f + context.LandmarkField * 0.08f,
                    11 => context.SedimentField * 0.22f + context.LandmarkField * 0.10f,
                    12 => context.ShelterField * 0.28f,
                    13 => context.IndustrialField * 0.26f + context.LandmarkField * 0.10f,
                    _ => context.LandmarkField * 0.74f + context.SedimentField * 0.10f
                },
                _ => (context.TerrainNoise * 0.55f) + (context.DetailNoise * 0.45f)
            };
        }

        private static int ResolvePreviewPatternBiomeFamilyIndex(
            WorldProceduralPattern pattern,
            float depthMeters,
            float slopeDegrees,
            int currentBiomeFamilyIndex,
            int sedimentDriftFamilyIndex,
            int littoralKarstFamilyIndex,
            int fossilReefFamilyIndex,
            int abyssalSiltFamilyIndex,
            int graniteEscarpmentFamilyIndex,
            int tectonicSpineFamilyIndex,
            int riftSpineFamilyIndex,
            int riftVoidFamilyIndex,
            int volcanicGlassFamilyIndex,
            int volcanicHadalFamilyIndex,
            int metallicHadalFamilyIndex,
            int chemosyntheticBrineFamilyIndex,
            int crystalGrowthFamilyIndex)
        {
            int fallback = currentBiomeFamilyIndex >= 0 ? currentBiomeFamilyIndex : sedimentDriftFamilyIndex;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => littoralKarstFamilyIndex >= 0
                    ? littoralKarstFamilyIndex
                    : crystalGrowthFamilyIndex >= 0 ? crystalGrowthFamilyIndex : fallback,
                WorldProceduralPattern.ReefNavigation => fossilReefFamilyIndex >= 0
                    ? fossilReefFamilyIndex
                    : crystalGrowthFamilyIndex >= 0 ? crystalGrowthFamilyIndex : fallback,
                WorldProceduralPattern.SedimentResources => depthMeters > 220f && graniteEscarpmentFamilyIndex >= 0
                    ? graniteEscarpmentFamilyIndex
                    : sedimentDriftFamilyIndex >= 0 ? sedimentDriftFamilyIndex : fallback,
                WorldProceduralPattern.IndustrialService => tectonicSpineFamilyIndex >= 0
                    ? tectonicSpineFamilyIndex
                    : metallicHadalFamilyIndex >= 0 ? metallicHadalFamilyIndex : fallback,
                WorldProceduralPattern.BrineToxic => chemosyntheticBrineFamilyIndex >= 0
                    ? chemosyntheticBrineFamilyIndex
                    : metallicHadalFamilyIndex >= 0 ? metallicHadalFamilyIndex : fallback,
                WorldProceduralPattern.VolcanicPressure => depthMeters > 240f && volcanicHadalFamilyIndex >= 0
                    ? volcanicHadalFamilyIndex
                    : volcanicGlassFamilyIndex >= 0 ? volcanicGlassFamilyIndex : fallback,
                WorldProceduralPattern.RiftHazard => depthMeters > 240f && riftVoidFamilyIndex >= 0
                    ? riftVoidFamilyIndex
                    : riftSpineFamilyIndex >= 0 ? riftSpineFamilyIndex : fallback,
                WorldProceduralPattern.AbyssSparse => abyssalSiltFamilyIndex >= 0
                    ? abyssalSiltFamilyIndex
                    : metallicHadalFamilyIndex >= 0 ? metallicHadalFamilyIndex : fallback,
                WorldProceduralPattern.LandmarkCorridor => slopeDegrees > 10f && graniteEscarpmentFamilyIndex >= 0
                    ? graniteEscarpmentFamilyIndex
                    : fossilReefFamilyIndex >= 0 ? fossilReefFamilyIndex : fallback,
                _ => fallback
            };
        }

        private static float ResolvePatternFieldBlend(SeafloorSource source, bool hasZone)
        {
            return source switch
            {
                SeafloorSource.FallbackSynthetic => hasZone ? 0.66f : 0.78f,
                SeafloorSource.MacroGeologyFallback => hasZone ? 0.42f : 0.56f,
                SeafloorSource.SceneProbeLegacy => hasZone ? 0.28f : 0.42f,
                SeafloorSource.TerrainProviderHeight => hasZone ? 0.18f : 0.34f,
                SeafloorSource.MapMagicHeight => hasZone ? 0.18f : 0.34f,
                _ => 0.2f
            };
        }

        private static float EvaluateBiomeMatrixChannelBonus(int channelIndex, int biomeMatrixDataIndex, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount)
        {
            if (biomeMatrixDataIndex < 0 || biomeMatrixDataIndex >= biomeMatrixCount)
                return 0f;

            BiomeMatrixData biomeData = biomeMatrices[biomeMatrixDataIndex];
            float loosePickup = math.clamp(biomeData.LoosePickupBias / 5f, 0f, 1f);
            float node = math.clamp(biomeData.NodeExtractionBias / 5f, 0f, 1f);
            float salvage = math.clamp(biomeData.SalvageBias / 5f, 0f, 1f);
            float common = math.clamp(biomeData.CommonResourceBias / 5f, 0f, 1f);
            float uncommon = math.clamp(biomeData.UncommonResourceBias / 5f, 0f, 1f);
            float rare = math.clamp(biomeData.RareResourceBias / 5f, 0f, 1f);
            float route = math.clamp(biomeData.RoutePressure / 5f, 0f, 1f);
            float landmark = math.clamp(biomeData.LandmarkStrength / 5f, 0f, 1f);
            float reward = math.clamp(biomeData.RewardPull / 5f, 0f, 1f);
            float survival = math.clamp(biomeData.SurvivalPressure / 5f, 0f, 1f);
            float resource = math.clamp((common * 0.45f) + (uncommon * 0.35f) + (rare * 0.2f), 0f, 1f);
            float salvageRead = math.clamp((salvage * 0.62f) + (node * 0.38f), 0f, 1f);
            float landmarkRead = math.clamp((landmark * 0.64f) + (route * 0.36f), 0f, 1f);
            float hazardRead = math.clamp((survival * 0.58f) + (route * 0.26f) + (rare * 0.16f), 0f, 1f);
            float shelterRead = math.clamp((survival * 0.68f) + (loosePickup * 0.16f) + ((1f - hazardRead) * 0.16f), 0f, 1f);
            float faunaRead = math.clamp((common * 0.34f) + (reward * 0.18f) + ((1f - survival) * 0.48f), 0f, 1f);

            return channelIndex switch
            {
                0 => landmarkRead * 0.08f + node * 0.04f,
                1 => faunaRead * 0.05f + shelterRead * 0.03f,
                2 => faunaRead * 0.06f + reward * 0.04f,
                3 => faunaRead * 0.07f + landmarkRead * 0.03f,
                4 => faunaRead * 0.11f + reward * 0.04f,
                5 => salvageRead * 0.12f,
                6 => salvageRead * 0.10f + landmarkRead * 0.04f,
                7 => landmarkRead * 0.10f + hazardRead * 0.04f,
                8 => landmarkRead * 0.13f + reward * 0.04f,
                9 => faunaRead * 0.12f - hazardRead * 0.03f,
                10 => hazardRead * 0.11f,
                11 => resource * 0.12f + reward * 0.05f,
                12 => shelterRead * 0.12f,
                13 => salvageRead * 0.10f + node * 0.05f,
                _ => 0f
            };
        }

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
            RefreshColdReferences(force: true);
        }

        private void OnEnable()
        {
            PublishActiveRuntimeInstance();
            RegisterRuntimeDependencyListeners();
            RefreshColdReferences(force: true);
            BiomeMatrixEvents.Register(this);
            // R97 FIX: seafloor height cache entries (MapMagicHeight / MacroGeologyFallback) were
            // never invalidated when a real terrain tile streamed in or moved — gameplay depth truth
            // stayed on fallback heights indefinitely (terrain.md failure path "stale height/chunk
            // handle"). Tile apply/move events now clear the cache.
            MapMagicTerrainTileEvents.Register(this);
            _isDataDirty = true;
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
        }

        private void OnDisable()
        {
            MapMagicTerrainTileEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
            UnregisterRuntimeDependencyListeners();
            ClearActiveRuntimeInstance();
            CompletePendingSamplingJobForBarrier();
            DisposeBurstData();
            ReleaseBiomeInfluenceGraphicsBuffer();
            _isDataDirty = true;
            _samplingFramePrepared = false;
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        private void OnDestroy()
        {
            MapMagicTerrainTileEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
            UnregisterRuntimeDependencyListeners();
            CompletePendingSamplingJobForBarrier();
            DisposeBurstData();
            ReleaseBiomeInfluenceGraphicsBuffer();
            _isDataDirty = true;
            ClearActiveRuntimeInstance();
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        private void PublishActiveRuntimeInstance()
        {
            GlobalRegistry.RegisterProceduralFieldSampler(this);
            s_activeRuntimeInstance = this;
        }

        private void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            if (ReferenceEquals(GlobalRegistry.ProceduralFieldSampler, this))
                GlobalRegistry.UnregisterProceduralFieldSampler(this);
        }

#if UNITY_EDITOR
        private static void EnsureAssemblyReloadHook()
        {
            if (_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
            _assemblyReloadHookRegistered = true;
        }

        private static void ReleaseAssemblyReloadHook()
        {
            if (!_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _assemblyReloadHookRegistered = false;
        }

        private static void HandleBeforeAssemblyReload()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void HandleEditorQuitting()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void TeardownActiveRuntimeInstanceForEditorReload()
        {
            WorldProceduralFieldSampler activeInstance = ActiveRuntimeInstance;
            if (activeInstance == null)
                return;

            activeInstance.PrepareForEditorReload();
            activeInstance.ClearActiveRuntimeInstance();
        }
#endif

        internal void PrepareForEditorReload()
        {
            BiomeMatrixEvents.Unregister(this);
            CompletePendingSamplingJobForBarrier();
            DisposeBurstData();
            _isDataDirty = true;
            _samplingFramePrepared = false;
        }

        public void BeginScatterSamplingFrame()
        {
            PrepareBurstData();
            _samplingFrameId++;
            _samplingFramePrepared = true;
            if (enableLiveRuntimeDiagnostics)
            {
                _debugBiomeCacheMisses = 0;
            }
        }

        public void EndScatterSamplingFrame()
        {
            _samplingFramePrepared = false;
        }

        public void MarkScatterSamplingJobCompleted()
        {
            // R97 FIX: previously dropped the handle and released vault buffer pins with no
            // completion check — a caller invoking this early would free pinned buffers while
            // CellSamplingJob was still reading them (use-after-release on vault regrow).
            // TryComplete is a no-op when the job already finished; otherwise it forces the
            // barrier exactly like CompletePendingSamplingJobForBarrier does.
            if (_hasPendingSamplingJob)
                DispatcherJobSwap.TryComplete(ref _lastSamplingJobHandle, true);

            _lastSamplingJobHandle = default;
            _hasPendingSamplingJob = false;
            ReleaseSamplingJobBufferPins();
        }

        public void MarkBurstDataDirty()
        {
            _isDataDirty = true;
            ClearSeafloorHeightCache();
        }

        /// <summary>
        /// R97: a MapMagic tile just applied real heights — cached MapMagic/macro-fallback samples
        /// for that region are stale. Tile events are rare (stream-in/move), so a full cache clear
        /// is cheaper and safer than per-tile range invalidation.
        /// </summary>
        void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
            ClearSeafloorHeightCache();
        }

        /// <summary>R97: a pooled tile moved to a new grid cell — its old heights are stale everywhere.</summary>
        void IMapMagicTerrainTileEventListener.OnMapMagicTerrainTileMoved(in MapMagicTerrainTileSnapshot snapshot)
        {
            ClearSeafloorHeightCache();
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile _)
        {
            _isDataDirty = true;
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HandleMatrixBiomeChanged(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        public bool TryBuildCellInput(Vector3 position, int cellX, int cellZ, out CellInputData input)
        {
            input = default;
            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            if (!TryGetCellHeightContext(position, out CellHeightContext terrainContext))
                return false;

            TryResolveBiomeReadout(
                position.x,
                position.z,
                out int biomeIndex,
                out int biomeMatrixId,
                out int secondaryBiomeMatrixId,
                out int biomeBlend255,
                out int mapMagicBiomeDataValid);

            float waterSurface = ResolveWaterSurfaceLevel(
                math.max(position.y + 120f, terrainContext.CenterHeight + 50f));

            input = new CellInputData
            {
                Position = new float3(position.x, position.y, position.z),
                CenterHeight = terrainContext.CenterHeight,
                NorthHeight = terrainContext.NorthHeight,
                SouthHeight = terrainContext.SouthHeight,
                EastHeight = terrainContext.EastHeight,
                WestHeight = terrainContext.WestHeight,
                WaterSurface = waterSurface,
                BiomeIndex = biomeIndex,
                BiomeMatrixId = biomeMatrixId,
                SecondaryBiomeMatrixId = secondaryBiomeMatrixId,
                BiomeBlend255 = biomeBlend255,
                MapMagicBiomeDataValid = mapMagicBiomeDataValid,
                CellX = cellX,
                CellZ = cellZ,
                SeafloorSource = (int)terrainContext.CenterSource,
                IsValid = 1
            };
            return true;
        }

        public JobHandle ScheduleCellSamplingJob(
            NativeArray<CellInputData> cellInputs,
            NativeArray<CellOutputData> cellOutputs,
            NativeArray<BiomeInfluenceCell> biomeInfluences,
            int cellCount)
        {
            if (_isDataDirty ||
                !TryResolveSamplingData(
                    out NativeArray<ZoneData> zoneData,
                    out NativeArray<BiomeMatrixData> biomeMatrixData,
                    out NativeArray<int> biomeMatrixIdToDataIndex,
                    out NativeArray<BiomeFamilyData> biomeFamilyData,
                    out NativeArray<CaveEntranceHintData> caveEntranceHints,
                    out NativeArray<ushort> noiseLookupTable))
            {
                PrepareBurstData();
                if (!TryResolveSamplingData(
                        out zoneData,
                        out biomeMatrixData,
                        out biomeMatrixIdToDataIndex,
                        out biomeFamilyData,
                        out caveEntranceHints,
                        out noiseLookupTable))
                {
                    _lastSamplingJobHandle = default;
                    _hasPendingSamplingJob = false;
                    return default;
                }
            }

            if (cellCount <= 0 ||
                !TryPinSamplingJobBuffers(
                    out zoneData,
                    out biomeMatrixData,
                    out biomeMatrixIdToDataIndex,
                    out biomeFamilyData,
                    out caveEntranceHints,
                    out noiseLookupTable))
            {
                _lastSamplingJobHandle = default;
                _hasPendingSamplingJob = false;
                return default;
            }

            CellSamplingJob job = new CellSamplingJob
            {
                CellInputs = cellInputs,
                Zones = zoneData,
                BiomeMatrices = biomeMatrixData,
                BiomeMatrixIdToDataIndex = biomeMatrixIdToDataIndex,
                BiomeFamilies = biomeFamilyData,
                CaveEntranceHints = caveEntranceHints,
                NoiseLookupTable = noiseLookupTable,
                CellOutputs = cellOutputs,
                BiomeInfluences = biomeInfluences,
                SlopeProbeMeters = slopeProbeMeters,
                FieldNoiseScale = fieldNoiseScale,
                DetailNoiseScale = detailNoiseScale,
                ForcePreviewPatternOverride = forcePatternPreviewOverride ? 1 : 0,
                LimitPreviewPatternOverrideToFallback = limitPatternOverrideToFallback ? 1 : 0,
                PreviewPatternOverride = (int)previewPatternOverride,
                CurrentBiomeMatrixDataIndex = ResolveBiomeMatrixDataIndex(biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null),
                CurrentBiomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(biomeMatrixDirector != null ? biomeMatrixDirector.CurrentFamilyProfile : null),
                PreviewMatrixBiomeDataIndex = ResolveBiomeMatrixDataIndex(ResolvePreviewMatrixBiomeOverride(SeafloorSource.FallbackSynthetic)),
                PreviewMatrixBiomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(previewMatrixBiomeOverride != null ? previewMatrixBiomeOverride.familyProfile : null),
                CurrentZoneDataIndex = ResolveZoneDataIndex(worldZoneDirector != null ? worldZoneDirector.CurrentZone : null),
                ZoneCount = _burstZoneDataCount,
                BiomeMatrixCount = _burstBiomeMatrixDataCount,
                BiomeFamilyCount = _burstBiomeFamilyDataCount,
                CaveEntranceHintCount = _burstCaveEntranceHintCount,
                LittoralKarstFamilyIndex = ResolveBiomeFamilyDataIndex(littoralKarstFamily),
                FossilReefFamilyIndex = ResolveBiomeFamilyDataIndex(fossilReefFamily),
                SedimentDriftFamilyIndex = ResolveBiomeFamilyDataIndex(sedimentDriftFamily),
                AbyssalSiltFamilyIndex = ResolveBiomeFamilyDataIndex(abyssalSiltFamily),
                GraniteEscarpmentFamilyIndex = ResolveBiomeFamilyDataIndex(graniteEscarpmentFamily),
                TectonicSpineFamilyIndex = ResolveBiomeFamilyDataIndex(tectonicSpineFamily),
                RiftSpineFamilyIndex = ResolveBiomeFamilyDataIndex(riftSpineFamily),
                RiftVoidFamilyIndex = ResolveBiomeFamilyDataIndex(riftVoidFamily),
                VolcanicGlassFamilyIndex = ResolveBiomeFamilyDataIndex(volcanicGlassFamily),
                VolcanicHadalFamilyIndex = ResolveBiomeFamilyDataIndex(volcanicHadalFamily),
                MetallicHadalFamilyIndex = ResolveBiomeFamilyDataIndex(metallicHadalFamily),
                ChemosyntheticBrineFamilyIndex = ResolveBiomeFamilyDataIndex(chemosyntheticBrineFamily),
                CrystalGrowthFamilyIndex = ResolveBiomeFamilyDataIndex(crystalGrowthFamily)
            };

            _samplingJobBuffersPinned = true;

            try
            {
                JobHandle handle = job.Schedule(cellCount, math.max(1, math.min(32, cellCount / 8)));
                _lastSamplingJobHandle = handle;
                _hasPendingSamplingJob = true;
                return handle;
            }
            catch
            {
                _lastSamplingJobHandle = default;
                _hasPendingSamplingJob = false;
                ReleaseSamplingJobBufferPins();
                throw;
            }
        }

        public bool TryUploadPackedBiomeInfluenceGrid(
            NativeArray<uint> packedCells,
            int cellCount,
            out GraphicsBuffer buffer,
            out int bufferCapacity)
        {
            buffer = null;
            bufferCapacity = 0;
            if (!packedCells.IsCreated || cellCount <= 0)
                return false;

            int safeCount = math.min(cellCount, packedCells.Length);
            if (safeCount <= 0 || !EnsureBiomeInfluenceGraphicsBufferCapacity(safeCount))
                return false;

            GraphicsBuffer writeBuffer = ResolveBiomeInfluenceWriteBuffer();
            if (writeBuffer == null)
                return false;

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, packedCells, safeCount);
            _activeBiomeInfluenceGraphicsBuffer = writeBuffer;
            _biomeInfluenceGraphicsBufferWriteIndex ^= 1;
            buffer = _activeBiomeInfluenceGraphicsBuffer;
            bufferCapacity = _biomeInfluenceGraphicsBufferCapacity;
            return buffer != null && buffer.IsValid();
        }

        private bool EnsureBiomeInfluenceGraphicsBufferCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return false;

            if (_biomeInfluenceGraphicsBufferA != null &&
                _biomeInfluenceGraphicsBufferA.IsValid() &&
                _biomeInfluenceGraphicsBufferB != null &&
                _biomeInfluenceGraphicsBufferB.IsValid() &&
                _biomeInfluenceGraphicsBufferCapacity >= requiredCapacity)
            {
                if (_activeBiomeInfluenceGraphicsBuffer == null)
                    _activeBiomeInfluenceGraphicsBuffer = _biomeInfluenceGraphicsBufferA;
                return true;
            }

            ReleaseBiomeInfluenceGraphicsBuffer();
            _biomeInfluenceGraphicsBufferCapacity = ResolvePowerOfTwoCapacity(requiredCapacity);
            if (Application.isPlaying && _biomeInfluenceGraphicsBufferCapacity > MaxBiomeInfluenceGridCellsMx350)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _biomeInfluenceGridCapacityWarningHash,
                    _fieldSamplerTelemetryContextHash,
                    _biomeInfluenceGraphicsBufferCapacity);
            }

            // COLD ALLOC: GraphicsBuffer[_biomeInfluenceGraphicsBufferCapacity] A/B - packed 8-family biome influence grid upload owned by WorldProceduralFieldSampler.
            _biomeInfluenceGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(_biomeInfluenceGraphicsBufferCapacity);
            _biomeInfluenceGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(_biomeInfluenceGraphicsBufferCapacity);
            _activeBiomeInfluenceGraphicsBuffer = _biomeInfluenceGraphicsBufferA;
            _biomeInfluenceGraphicsBufferWriteIndex = 0;
            return _biomeInfluenceGraphicsBufferA != null &&
                   _biomeInfluenceGraphicsBufferA.IsValid() &&
                   _biomeInfluenceGraphicsBufferB != null &&
                   _biomeInfluenceGraphicsBufferB.IsValid();
        }

        private GraphicsBuffer ResolveBiomeInfluenceWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _biomeInfluenceGraphicsBufferWriteIndex == 0
                ? _biomeInfluenceGraphicsBufferA
                : _biomeInfluenceGraphicsBufferB;
            if (writeBuffer != null && writeBuffer.IsValid())
                return writeBuffer;

            GraphicsBuffer fallback = ReferenceEquals(_activeBiomeInfluenceGraphicsBuffer, _biomeInfluenceGraphicsBufferA)
                ? _biomeInfluenceGraphicsBufferB
                : _biomeInfluenceGraphicsBufferA;
            return fallback != null && fallback.IsValid() ? fallback : null;
        }

        private void ReleaseBiomeInfluenceGraphicsBuffer()
        {
            if (_biomeInfluenceGraphicsBufferA != null)
            {
                _biomeInfluenceGraphicsBufferA.Release();
                _biomeInfluenceGraphicsBufferA = null;
            }

            if (_biomeInfluenceGraphicsBufferB != null)
            {
                _biomeInfluenceGraphicsBufferB.Release();
                _biomeInfluenceGraphicsBufferB = null;
            }

            _activeBiomeInfluenceGraphicsBuffer = null;
            _biomeInfluenceGraphicsBufferWriteIndex = 0;
            _biomeInfluenceGraphicsBufferCapacity = 0;
        }

        internal bool TryPrewarmSamplingJob()
        {
            PrepareBurstData();

            // COLD SYNC JOB: prewarm Burst compilation and worker setup before player activation so the first runtime scatter pass does not absorb one-time compilation debt.
            NativeArray<CellInputData> warmupInputs = default;
            NativeArray<CellOutputData> warmupOutputs = default;
            NativeArray<BiomeInfluenceCell> warmupInfluences = default;
            try
            {
                warmupInputs = new NativeArray<CellInputData>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CellInputData>[1] - Burst prewarm input lane - owner: WorldProceduralFieldSampler
                RegisterTrackedNativeArray(warmupInputs, nameof(warmupInputs), NativeMemoryTempJobLifetime);
                warmupOutputs = new NativeArray<CellOutputData>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CellOutputData>[1] - Burst prewarm output lane - owner: WorldProceduralFieldSampler
                RegisterTrackedNativeArray(warmupOutputs, nameof(warmupOutputs), NativeMemoryTempJobLifetime);
                warmupInfluences = new NativeArray<BiomeInfluenceCell>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<BiomeInfluenceCell>[1] - Burst prewarm biome influence lane - owner: WorldProceduralFieldSampler
                RegisterTrackedNativeArray(warmupInfluences, nameof(warmupInfluences), NativeMemoryTempJobLifetime);

                BeginScatterSamplingFrame();
                warmupInputs[0] = new CellInputData
                {
                    Position = new float3(0f, 0f, 0f),
                    CenterHeight = 0f,
                    NorthHeight = 0f,
                    SouthHeight = 0f,
                    EastHeight = 0f,
                    WestHeight = 0f,
                    WaterSurface = 64f,
                    BiomeIndex = 0,
                    BiomeMatrixId = 0,
                    CellX = 0,
                    CellZ = 0,
                    SeafloorSource = (int)SeafloorSource.FallbackSynthetic,
                    IsValid = 1
                };

                JobHandle warmupHandle = ScheduleCellSamplingJob(warmupInputs, warmupOutputs, warmupInfluences, 1);
                DispatcherJobSwap.TryComplete(ref warmupHandle, true);
                _lastSamplingJobHandle = default;
                _hasPendingSamplingJob = false;
                return true;
            }
            finally
            {
                EndScatterSamplingFrame();
                DisposeTrackedNativeArray(ref warmupInputs);
                DisposeTrackedNativeArray(ref warmupOutputs);
                DisposeTrackedNativeArray(ref warmupInfluences);
            }
        }

        public bool TryBuildFieldSample(in CellOutputData output, out FieldSample sample)
        {
            return TryBuildFieldSample(output, 0, out sample);
        }

        public bool TryResolveBiomeMatrixProfileById(int matrixBiomeId, out HectonBiomeMatrixProfile profile)
        {
            profile = null;
            if (matrixBiomeId <= 0)
                return false;

            if (_isDataDirty)
                PrepareBurstData();

            int count = _biomeMatrixBakeCount;
            for (int i = 0; i < count; i++)
            {
                HectonBiomeMatrixProfile candidate = _biomeMatrixBakeList[i];
                if (candidate == null || candidate.matrixIndex != matrixBiomeId)
                    continue;

                profile = candidate;
                return true;
            }

            return false;
        }

        public int GetFieldSampleDomainCount(in CellOutputData output)
        {
            if (output.IsValid == 0)
                return 0;

            return output.SecondarySampleValid != 0 ? 2 : 1;
        }

        public bool TryBuildFieldSample(in CellOutputData output, int domainIndex, out FieldSample sample)
        {
            sample = default;
            if (output.IsValid == 0 || domainIndex < 0)
                return false;

            if (domainIndex > 0 && output.SecondarySampleValid == 0)
                return false;

            bool secondaryDomain = domainIndex == 1;
            float domainHeight = secondaryDomain ? output.SecondaryHeight : output.SeafloorHeight;
            float domainDepth = secondaryDomain ? output.SecondaryDepthMeters : output.DepthMeters;
            float domainCaveProximity = secondaryDomain ? math.max(output.CaveProximity, output.SecondaryCaveProximity) : output.CaveProximity;
            float domainCompositionPotential = secondaryDomain
                ? math.saturate(
                    math.saturate((output.SlopeDegrees - 6f) / 42f) * 0.16f +
                    math.abs(output.Curvature) * 0.18f +
                    output.RidgeSignal * 0.20f +
                    output.CanyonSignal * 0.18f +
                    domainCaveProximity * 0.18f +
                    output.CompositionNoise * 0.10f)
                : output.CompositionPotential;

            sample = new FieldSample
            {
                position = new Vector3(output.Position.x, domainHeight, output.Position.z),
                seafloorHeight = domainHeight,
                depthMeters = domainDepth,
                slopeDegrees = output.SlopeDegrees,
                curvature = output.Curvature,
                ridgeSignal = output.RidgeSignal,
                canyonSignal = output.CanyonSignal,
                caveProximity = domainCaveProximity,
                compositionPotential = domainCompositionPotential,
                biomeIndex = output.BiomeIndex,
                zoneDataIndex = output.ZoneDataIndex,
                biomeMatrixDataIndex = output.BiomeMatrixDataIndex,
                biomeFamilyDataIndex = output.BiomeFamilyDataIndex,
                biomeFamilyFlags = output.BiomeFamilyFlags,
                biomeProfile = output.BiomeMatrixDataIndex >= 0 && output.BiomeMatrixDataIndex < _biomeMatrixBakeCount ? _biomeMatrixBakeList[output.BiomeMatrixDataIndex] : null,
                secondaryBiomeProfile = ResolveSecondaryBiomeProfile(output),
                biomeFamily = output.BiomeFamilyDataIndex >= 0 && output.BiomeFamilyDataIndex < _biomeFamilyBakeCount ? _biomeFamilyBakeList[output.BiomeFamilyDataIndex] : null,
                secondaryBiomeFamily = ResolveSecondaryBiomeFamily(output),
                biomeInfluence = new BiomeInfluenceCell { Packed = output.BiomeInfluencePacked },
                zone = output.ZoneDataIndex >= 0 && output.ZoneDataIndex < _zoneBakeCount ? _zoneBakeList[output.ZoneDataIndex] : null,
                zoneWeight = output.ZoneWeight,
                resolvedZoneKind = (WorldZoneAnchor.ZoneKind)output.ResolvedZoneKind,
                resolvedPattern = (WorldProceduralPattern)output.ResolvedPattern,
                isPreviewOverride = output.PreviewOverrideActive != 0 ? (byte)1 : (byte)0,
                verticalDomainIndex = domainIndex,
                verticalDomainWeight = secondaryDomain ? output.SecondaryDomainWeight : 1f,
                isSecondaryDomain = secondaryDomain ? (byte)1 : (byte)0,
                seafloorSource = (SeafloorSource)output.SeafloorSource,
                isValid = 1
            };
            if (TryBuildTerrainDetailRuntimeSample(
                    output.Position.x,
                    output.Position.z,
                    out WorldTerrainDetailRuntimeSample terrainDetailSample))
            {
                AssignTerrainDetailSample(ref sample, in terrainDetailSample);
            }

            return true;
        }

        private HectonBiomeMatrixProfile ResolveSecondaryBiomeProfile(in CellOutputData output)
        {
            int secondaryIndex = ResolveSecondaryBiomeMatrixDataIndex(in output);
            return secondaryIndex >= 0 && secondaryIndex < _biomeMatrixBakeCount
                ? _biomeMatrixBakeList[secondaryIndex]
                : null;
        }

        private HectonBiomeFamilyProfile ResolveSecondaryBiomeFamily(in CellOutputData output)
        {
            HectonBiomeMatrixProfile profile = ResolveSecondaryBiomeProfile(in output);
            if (profile != null && profile.familyProfile != null)
                return profile.familyProfile;

            int secondaryIndex = ResolveSecondaryBiomeMatrixDataIndex(in output);
            if (secondaryIndex < 0 ||
                !TryReadVaultBuffer(BufferID.WorldProceduralFieldBiomeMatrices, in _burstBiomeMatrixDataHandle, out NativeArray<BiomeMatrixData>.ReadOnly biomeMatrixData) ||
                secondaryIndex >= _burstBiomeMatrixDataCount)
            {
                return null;
            }

            int familyIndex = biomeMatrixData[secondaryIndex].FamilyDataIndex;
            return familyIndex >= 0 && familyIndex < _biomeFamilyBakeCount
                ? _biomeFamilyBakeList[familyIndex]
                : null;
        }

        private static int ResolveSecondaryBiomeMatrixDataIndex(in CellOutputData output)
        {
            if (HectonBiomeVisualFamilyUtility.ExtractBlend255(output.BiomeInfluencePacked) == 0)
                return -1;

            if (output.VolumetricOverrideActive != 0)
                return output.PreviousBiomeMatrixDataIndex;

            return output.SecondaryBiomeMatrixDataIndex;
        }

        public float EvaluateHeatmap(string heatmapChannel, in CellOutputData output, WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule)
        {
            string resolvedChannel = string.IsNullOrWhiteSpace(heatmapChannel)
                ? family != null ? family.heatmapChannel : string.Empty
                : heatmapChannel;
            return EvaluateHeatmap(
                ResolveHeatmapChannelIndex(resolvedChannel),
                output,
                family != null ? family.placementMode : WorldPrefabFamilyProfile.PlacementMode.Scatter,
                rule != null && !string.IsNullOrWhiteSpace(rule.gameplayIntent)
                    ? 0.95f + math.saturate(rule.densityScale * 0.12f)
                    : 1f);
        }

        public float EvaluateHeatmap(
            int heatmapChannelIndex,
            in CellOutputData output,
            WorldPrefabFamilyProfile.PlacementMode placementMode,
            float densityScaleFactor)
        {
            float value = heatmapChannelIndex switch
            {
                0 => output.RockDensityHeat,
                1 => output.KelpDensityHeat,
                2 => output.FloraDensityHeat,
                3 => output.CoralDensityHeat,
                4 => output.BioDensityHeat,
                5 => output.DebrisDensityHeat,
                6 => output.RuinDensityHeat,
                7 => output.CaveDensityHeat,
                8 => output.LandmarkStrengthHeat,
                9 => output.FaunaDensityHeat,
                10 => output.HazardDensityHeat,
                11 => output.ResourceDensityHeat,
                12 => output.ShelterDensityHeat,
                13 => output.ServiceDensityHeat,
                _ => output.GenericHeat
            };

            value *= placementMode switch
            {
                WorldPrefabFamilyProfile.PlacementMode.Landmark => math.lerp(0.8f, 1.2f, math.saturate(output.LandmarkBias)),
                WorldPrefabFamilyProfile.PlacementMode.Cluster => 1.05f,
                WorldPrefabFamilyProfile.PlacementMode.Patch => 1.08f,
                WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => math.lerp(0.85f, 1.15f, math.saturate(output.HazardBias)),
                _ => 1f
            };

            return math.saturate(value * densityScaleFactor);
        }

        public static int ResolveHeatmapChannelIndex(string heatmapChannel)
        {
            if (string.IsNullOrWhiteSpace(heatmapChannel))
                return -1;

            return heatmapChannel switch
            {
                "rock_density" => 0,
                "kelp_density" => 1,
                "flora_density" => 2,
                "coral_density" => 3,
                "bio_density" => 4,
                "debris_density" => 5,
                "ruin_density" => 6,
                "cave_density" => 7,
                "landmark_strength" => 8,
                "fauna_density" => 9,
                "hazard_density" => 10,
                "resource_density" => 11,
                "shelter_density" => 12,
                "service_density" => 13,
                _ => -1
            };
        }

        private void EnsureNoiseLookupTable()
        {
            int requiredLength = NoiseLookupResolution * NoiseLookupResolution;
            if (TryResolveVaultBuffer(BufferID.WorldProceduralFieldNoiseLookup, in _noiseLookupTableHandle, out NativeArray<ushort> existing) &&
                existing.Length == requiredLength)
            {
                return;
            }

            if (!TryEnsureVaultBufferCapacity(
                    ref _noiseLookupTableHandle,
                    BufferID.WorldProceduralFieldNoiseLookup,
                    requiredLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ushort> noiseLookupTable))
            {
                return;
            }

            for (int z = 0; z < NoiseLookupResolution; z++)
            {
                float v = z / (float)NoiseLookupResolution;
                int rowOffset = z * NoiseLookupResolution;
                for (int x = 0; x < NoiseLookupResolution; x++)
                {
                    float u = x / (float)NoiseLookupResolution;
                    float broad = EvaluateTileableNoise01(u, v, 6.5f);
                    float detail = EvaluateTileableNoise01(math.frac(u + 0.317f), math.frac(v + 0.143f), 13.75f);
                    float combined = math.saturate((broad * 0.65f) + (detail * 0.35f));
                    noiseLookupTable[rowOffset + x] = (ushort)math.round(combined * ushort.MaxValue);
                }
            }
        }

        private static float EvaluateTileableNoise01(float u, float v, float frequency)
        {
            float2 sample = new float2(u * frequency, v * frequency);
            float2 sampleXWrap = new float2((u - 1f) * frequency, v * frequency);
            float2 sampleYWrap = new float2(u * frequency, (v - 1f) * frequency);
            float2 sampleXYWrap = new float2((u - 1f) * frequency, (v - 1f) * frequency);

            float a = NoiseTo01(noise.snoise(sample));
            float b = NoiseTo01(noise.snoise(sampleXWrap));
            float c = NoiseTo01(noise.snoise(sampleYWrap));
            float d = NoiseTo01(noise.snoise(sampleXYWrap));

            float top = math.lerp(a, b, u);
            float bottom = math.lerp(c, d, u);
            return math.lerp(top, bottom, v);
        }

        public void PrepareBurstData()
        {
            // L19 hop2 LIVE: ACCESS_VIOLATION inside TryEnsureVaultBufferCapacity(BiomeFamilyData)
            // during PrepareBurstData under -batchmode. Soft-disable vault/burst buffer prep so
            // headless hop probes can reach input validation. Interactive paths unchanged.
            if (Application.isBatchMode)
                return;

            CompletePendingSamplingJobForBarrier();
            RefreshCachedDependencyDiagnostics();
            EnsureNoiseLookupTable();

            int activeAnchorVersion = WorldZoneAnchor.ActiveAnchorVersion;
            if (activeAnchorVersion != _lastActiveAnchorVersion)
            {
                _lastActiveAnchorVersion = activeAnchorVersion;
                _isDataDirty = true;
            }

            int caveEntranceHintVersion = _worldCaveDirector != null ? _worldCaveDirector.EntranceHintVersion : -1;
            if (caveEntranceHintVersion != _lastCaveEntranceHintVersion)
            {
                _lastCaveEntranceHintVersion = caveEntranceHintVersion;
                _isDataDirty = true;
            }

            if (!_isDataDirty)
                return;

            RefreshActiveAnchorsSnapshot();

            System.Array.Clear(_zoneBakeList, 0, _zoneBakeList.Length);
            System.Array.Clear(_biomeMatrixBakeList, 0, _biomeMatrixBakeCount);
            System.Array.Clear(_biomeFamilyBakeList, 0, _biomeFamilyBakeCount);
            System.Array.Clear(_caveEntranceHintBakeList, 0, _caveEntranceHintBakeCount);
            _zoneBakeCount = 0;
            _biomeMatrixBakeCount = 0;
            _biomeFamilyBakeCount = 0;
            _caveEntranceHintBakeCount = 0;

            RegisterFamilyForBake(littoralKarstFamily);
            RegisterFamilyForBake(fossilReefFamily);
            RegisterFamilyForBake(sedimentDriftFamily);
            RegisterFamilyForBake(abyssalSiltFamily);
            RegisterFamilyForBake(graniteEscarpmentFamily);
            RegisterFamilyForBake(tectonicSpineFamily);
            RegisterFamilyForBake(riftSpineFamily);
            RegisterFamilyForBake(riftVoidFamily);
            RegisterFamilyForBake(volcanicGlassFamily);
            RegisterFamilyForBake(volcanicHadalFamily);
            RegisterFamilyForBake(metallicHadalFamily);
            RegisterFamilyForBake(chemosyntheticBrineFamily);
            RegisterFamilyForBake(crystalGrowthFamily);

            RegisterMatrixForBake(previewMatrixBiomeOverride);

            HectonBiomeMatrixCatalog matrixCatalog = biomeMatrixDirector != null ? biomeMatrixDirector.MatrixCatalog : null;
            HectonBiomeMatrixProfile[] matrixProfiles = matrixCatalog != null ? matrixCatalog.Profiles : null;
            if (matrixProfiles != null)
            {
                for (int i = 0; i < matrixProfiles.Length; i++)
                    RegisterMatrixForBake(matrixProfiles[i]);
            }

            for (int i = 0; i < _anchorCount; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                RegisterMatrixForBake(anchor.DominantMatrixBiome);
                RegisterFamilyForBake(anchor.DominantBiomeFamily);
            }

            if (_worldCaveDirector != null)
                _caveEntranceHintBakeCount = _worldCaveDirector.CopyEntranceHintsTo(_caveEntranceHintBakeList);

            if (!TryEnsureVaultBufferCapacity(
                    ref _burstBiomeFamilyDataHandle,
                    BufferID.WorldProceduralFieldBiomeFamilies,
                    _biomeFamilyBakeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<BiomeFamilyData> biomeFamilyData))
            {
                return;
            }

            _burstBiomeFamilyDataCount = _biomeFamilyBakeCount;
            for (int i = 0; i < _biomeFamilyBakeCount; i++)
            {
                HectonBiomeFamilyProfile family = _biomeFamilyBakeList[i];
                biomeFamilyData[i] = new BiomeFamilyData
                {
                    FamilyInstanceId = family != null ? unchecked((int)EntityId.ToULong(family.GetEntityId())) : 0,
                    Flags = TokenizeFamilyFlags(family)
                };
            }

            if (!TryEnsureVaultBufferCapacity(
                    ref _burstBiomeMatrixDataHandle,
                    BufferID.WorldProceduralFieldBiomeMatrices,
                    _biomeMatrixBakeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<BiomeMatrixData> biomeMatrixData) ||
                !TryEnsureVaultBufferCapacity(
                    ref _burstBiomeMatrixIdToDataIndexHandle,
                    BufferID.WorldProceduralFieldBiomeMatrixIndex,
                    256,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<int> biomeMatrixIdToDataIndex))
            {
                return;
            }

            for (int i = 0; i < biomeMatrixIdToDataIndex.Length; i++)
                biomeMatrixIdToDataIndex[i] = -1;

            _burstBiomeMatrixDataCount = _biomeMatrixBakeCount;
            for (int i = 0; i < _biomeMatrixBakeCount; i++)
            {
                HectonBiomeMatrixProfile profile = _biomeMatrixBakeList[i];
                int matrixIndex = profile != null ? profile.matrixIndex : -1;
                biomeMatrixData[i] = new BiomeMatrixData
                {
                    MatrixIndex = matrixIndex,
                    FamilyDataIndex = ResolveBiomeFamilyDataIndex(profile != null ? profile.familyProfile : null),
                    MinDepthMeters = profile != null ? profile.minDepthMeters : 0f,
                    MaxDepthMeters = profile != null ? profile.maxDepthMeters : 0f,
                    LoosePickupBias = profile != null ? profile.loosePickupBias : 0,
                    NodeExtractionBias = profile != null ? profile.nodeExtractionBias : 0,
                    SalvageBias = profile != null ? profile.salvageBias : 0,
                    CommonResourceBias = profile != null ? profile.commonResourceBias : 0,
                    UncommonResourceBias = profile != null ? profile.uncommonResourceBias : 0,
                    RareResourceBias = profile != null ? profile.rareResourceBias : 0,
                    RoutePressure = profile != null ? profile.routePressure : 0,
                    LandmarkStrength = profile != null ? profile.landmarkStrength : 0,
                    RewardPull = profile != null ? profile.rewardPull : 0,
                    SurvivalPressure = profile != null ? profile.survivalPressure : 0,
                    IsPlaceholder = profile != null && profile.isPlaceholder ? 1 : 0,
                    VolumetricRole = ResolveVolumetricBiomeRole(profile)
                };

                if (matrixIndex > 0 && matrixIndex < biomeMatrixIdToDataIndex.Length)
                    biomeMatrixIdToDataIndex[matrixIndex] = i;
            }

            if (!TryEnsureVaultBufferCapacity(
                    ref _burstZoneDataHandle,
                    BufferID.WorldProceduralFieldZones,
                    _anchorCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ZoneData> zoneData))
            {
                return;
            }

            _burstZoneDataCount = 0;
            for (int i = 0; i < _anchorCount; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                if (_zoneBakeCount >= _zoneBakeList.Length)
                    break;

                int zoneDataIndex = _burstZoneDataCount++;
                _zoneBakeList[_zoneBakeCount] = anchor;
                _zoneBakeCount++;
                zoneData[zoneDataIndex] = new ZoneData
                {
                    PositionXZ = new float2(anchor.transform.position.x, anchor.transform.position.z),
                    ActivationRadius = anchor.ActivationRadius,
                    HoldRadius = anchor.HoldRadius,
                    EdgeBlendDistance = anchor.EdgeBlendDistance,
                    EdgeNoiseScale = anchor.EdgeNoiseScale,
                    EdgeNoiseStrength = anchor.EdgeNoiseStrength,
                    EdgeNoiseOffset = new float2(anchor.EdgeNoiseOffset.x, anchor.EdgeNoiseOffset.y),
                    Priority = anchor.Priority,
                    Kind = (int)anchor.Kind,
                    Tier = (int)anchor.Tier,
                    DominantMatrixDataIndex = ResolveBiomeMatrixDataIndex(anchor.DominantMatrixBiome),
                    DominantFamilyDataIndex = ResolveBiomeFamilyDataIndex(anchor.DominantBiomeFamily),
                    RouteCritical = anchor.RouteCritical ? 1 : 0
                };
            }

            if (!TryEnsureVaultBufferCapacity(
                    ref _burstCaveEntranceHintsHandle,
                    BufferID.WorldProceduralFieldCaveEntranceHints,
                    _caveEntranceHintBakeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<CaveEntranceHintData> caveEntranceHints))
            {
                _burstCaveEntranceHintCount = 0;
                return;
            }

            _burstCaveEntranceHintCount = 0;
            for (int i = 0; i < _caveEntranceHintBakeCount; i++)
            {
                WorldCaveDirector.CaveEntranceHint hint = _caveEntranceHintBakeList[i];
                if (!TryBuildCaveEntranceHintData(in hint, out CaveEntranceHintData hintData))
                    continue;

                caveEntranceHints[_burstCaveEntranceHintCount++] = hintData;
            }

            _isDataDirty = false;
        }

        private static bool TryBuildCaveEntranceHintData(
            in WorldCaveDirector.CaveEntranceHint hint,
            out CaveEntranceHintData hintData)
        {
            hintData = default;
            float3 surfacePosition = hint.SurfacePosition;
            float3 interiorPosition = hint.InteriorPosition;
            if (!IsFinite(surfacePosition) ||
                !IsFinite(interiorPosition) ||
                !math.isfinite(hint.EntranceRadius) ||
                !math.isfinite(hint.InfluenceRadius) ||
                hint.EntranceRadius <= 0f ||
                hint.InfluenceRadius <= 0f)
            {
                return false;
            }

            hintData = new CaveEntranceHintData
            {
                SurfacePosition = surfacePosition,
                InteriorPosition = interiorPosition,
                EntranceRadius = math.clamp(hint.EntranceRadius, 0.01f, 24f),
                InfluenceRadius = math.clamp(hint.InfluenceRadius, 0.01f, 128f)
            };
            return true;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public CellSamplingContext PrecomputeCellContext(Vector3 position)
        {
            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            return new CellSamplingContext
            {
                TerrainNoise = EvaluateNoise01(position.x, position.z, fieldNoiseScale),
                DetailNoise = EvaluateNoise01(position.x + 91.7f, position.z - 33.4f, detailNoiseScale),
                SedimentFieldNoise = EvaluateNoise01(position.x - 218.6f, position.z + 57.4f, fieldNoiseScale * 0.74f),
                FertileFieldNoise = EvaluateNoise01(position.x + 127.8f, position.z - 146.2f, detailNoiseScale * 0.78f),
                ReefFieldNoise = EvaluateNoise01(position.x + 314.4f, position.z + 88.5f, detailNoiseScale * 0.58f),
                IndustrialFieldNoise = EvaluateNoise01(position.x - 401.1f, position.z - 203.6f, fieldNoiseScale * 0.82f),
                HazardFieldNoise = EvaluateNoise01(position.x + 261.7f, position.z - 318.3f, detailNoiseScale * 0.94f),
                LandmarkFieldNoise = EvaluateNoise01(position.x - 83.2f, position.z + 367.9f, fieldNoiseScale * 0.62f),
                BasinFieldNoise = EvaluateNoise01(position.x + 452.5f, position.z + 121.3f, detailNoiseScale * 0.66f),
                RuggedBiomeNoise = EvaluateNoise01(position.x + 173.4f, position.z - 117.2f, fieldNoiseScale * 0.9f),
                FertileBiomeNoise = EvaluateNoise01(position.x - 91.6f, position.z + 44.3f, fieldNoiseScale * 1.15f),
                ThermalBiomeNoise = EvaluateNoise01(position.x + 304.2f, position.z + 281.4f, detailNoiseScale * 0.92f),
                MetallicBiomeNoise = EvaluateNoise01(position.x - 211.5f, position.z + 96.7f, detailNoiseScale * 0.88f),
                CrystalBiomeNoise = EvaluateNoise01(position.x + 67.4f, position.z - 248.6f, detailNoiseScale * 0.84f),
                VoidBiomeNoise = EvaluateNoise01(position.x - 403.1f, position.z - 365.8f, fieldNoiseScale * 0.66f),
                ReefBiomeNoise = EvaluateNoise01(position.x + 149.7f, position.z - 71.9f, detailNoiseScale * 0.9f),
                BasinMacroNoise = EvaluateNoise01(position.x - 512.4f, position.z + 188.6f, fieldNoiseScale * 0.22f),
                ReefMacroNoise = EvaluateNoise01(position.x + 417.2f, position.z - 153.3f, fieldNoiseScale * 0.24f),
                ServiceMacroNoise = EvaluateNoise01(position.x - 286.5f, position.z + 407.8f, fieldNoiseScale * 0.21f),
                RiftMacroNoise = EvaluateNoise01(position.x + 598.1f, position.z - 487.2f, fieldNoiseScale * 0.19f),
                CoralPatternNoise = EvaluateNoise01(position.x + 153.4f, position.z - 74.7f, detailNoiseScale * 0.86f),
                CaveNoise = EvaluateNoise01(position.x - 141.7f, position.z + 208.3f, fieldNoiseScale * 0.78f),
                CompositionNoise = EvaluateNoise01(position.x + 387.2f, position.z - 291.4f, detailNoiseScale * 0.56f)
            };
        }

        public bool TrySampleSeafloor(Vector3 position, in CellSamplingContext cellContext, out FieldSample sample)
        {
            sample = default;
            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            if (!TryGetLocalTerrainContext(position, out LocalTerrainContext terrainContext))
            {
                if (ShouldUpdateDiagnostics())
                    UpdateDiagnostics(default, "None", 0f);
                return false;
            }

            float seafloorHeight = terrainContext.CenterHeight;
            SeafloorSource seafloorSource = terrainContext.CenterSource;

            int biomeIndex = 0;
            int matrixBiomeId = ResolveCurrentMatrixBiomeId();
            HectonBiomeMatrixProfile mapMagicSecondaryProfile = null;
            byte mapMagicBlend255 = 0;
            if (mapMagicBridge != null)
            {
                int mapMagicMatrixBiomeId;
                int mapMagicAlphamapLayer;
                if (mapMagicBridge.TryGetMatrixBiomeInfluence(
                        position.x,
                        position.z,
                        out mapMagicMatrixBiomeId,
                        out int secondaryMatrixBiomeId,
                        out byte resolvedBlend255,
                        out mapMagicAlphamapLayer,
                        out _))
                {
                    matrixBiomeId = mapMagicMatrixBiomeId;
                    biomeIndex = mapMagicAlphamapLayer;
                    mapMagicSecondaryProfile = ResolveBiomeMatrixProfileById(secondaryMatrixBiomeId);
                    mapMagicBlend255 = resolvedBlend255;
                }
                else if (mapMagicBridge.TryGetMatrixBiomeId(position.x, position.z, out mapMagicMatrixBiomeId, out mapMagicAlphamapLayer))
                {
                    matrixBiomeId = mapMagicMatrixBiomeId;
                    biomeIndex = mapMagicAlphamapLayer;
                }
                else
                {
                    mapMagicBridge.TryGetBiomeIndex(position.x, position.z, out biomeIndex);
                }
            }

            float waterSurface = ResolveWaterSurfaceLevel(
                math.max(position.y + 120f, seafloorHeight + 50f));
            float depthMeters = math.max(0f, waterSurface - seafloorHeight);
            float slopeDegrees = terrainContext.SlopeDegrees;
            float curvature = terrainContext.Curvature;
            WorldZoneAnchor zone = ResolveZone(new Vector3(position.x, seafloorHeight, position.z), out float zoneWeight);
            int zoneDataIndex = ResolveZoneDataIndex(zone);
            HectonBiomeMatrixProfile biomeProfile = ResolveBiomeMatrixProfileById(matrixBiomeId);
            if (biomeProfile == null)
                biomeProfile = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
            HectonBiomeFamilyProfile biomeFamily = zone != null
                ? zone.DominantBiomeFamily
                : biomeMatrixDirector != null
                    ? biomeMatrixDirector.CurrentFamilyProfile
                    : null;
            WorldZoneAnchor.ZoneKind resolvedZoneKind = zone != null
                ? zone.Kind
                : ResolveFallbackZoneKind(position, depthMeters, slopeDegrees, cellContext);
            if (biomeFamily == null)
                biomeFamily = ResolveFallbackBiomeFamily(position, depthMeters, slopeDegrees, resolvedZoneKind, cellContext);
            int biomeMatrixDataIndex = ResolveBiomeMatrixDataIndex(biomeProfile);
            int biomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(biomeFamily);
            float ridgeSignal = EvaluateRidgeSignal(curvature, slopeDegrees, zoneDataIndex, zone);
            float canyonSignal = EvaluateCanyonSignal(curvature, slopeDegrees, zoneDataIndex, zone);
            float caveProximity = EvaluateCaveProximity(depthMeters, slopeDegrees, zoneDataIndex, zone, resolvedZoneKind, cellContext.CaveNoise);
            float compositionPotential = EvaluateCompositionPotential(slopeDegrees, curvature, ridgeSignal, canyonSignal, caveProximity, cellContext.CompositionNoise);
            WorldProceduralPattern resolvedPattern;
            if (!TryApplyPreviewPatternContextOverride(
                    seafloorSource,
                    depthMeters,
                    slopeDegrees,
                    ref biomeFamily,
                    ref resolvedZoneKind,
                    out resolvedPattern))
            {
                biomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(biomeFamily);
                resolvedPattern = ResolvePattern(position, depthMeters, slopeDegrees, biomeFamily, biomeFamilyDataIndex, zone, zoneDataIndex, resolvedZoneKind, cellContext);
                resolvedPattern = ResolvePreviewPatternOverride(resolvedPattern, seafloorSource);
            }

            HectonBiomeMatrixProfile previewMatrixProfile = ResolvePreviewMatrixBiomeOverride(seafloorSource);
            bool previewOverrideApplied = forcePatternPreviewOverride && (!limitPatternOverrideToFallback || seafloorSource == SeafloorSource.FallbackSynthetic);
            if (previewMatrixProfile != null)
            {
                biomeProfile = previewMatrixProfile;
                previewOverrideApplied = true;
                if (previewMatrixProfile.familyProfile != null)
                    biomeFamily = previewMatrixProfile.familyProfile;
            }
            else
            {
                biomeProfile = ResolveEffectiveBiomeProfile(
                    biomeProfile,
                    biomeFamily,
                    seafloorSource,
                    resolvedPattern);
            }

            bool volumetricOverrideApplied = false;
            HectonBiomeMatrixProfile previousBiomeProfile = biomeProfile;
            float sampleDepthMeters = math.max(depthMeters, math.max(0f, waterSurface - position.y));
            HectonBiomeMatrixProfile volumetricProfile = ResolveVolumetricBiomeProfile(
                position.y,
                sampleDepthMeters,
                biomeFamily,
                biomeProfile,
                out volumetricOverrideApplied);
            if (volumetricProfile != null)
            {
                biomeProfile = volumetricProfile;
                if (volumetricProfile.familyProfile != null)
                    biomeFamily = volumetricProfile.familyProfile;
            }

            biomeMatrixDataIndex = ResolveBiomeMatrixDataIndex(biomeProfile);
            biomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(biomeFamily);
            float hazardBias = EvaluateHazardBias(zoneDataIndex, zone, resolvedZoneKind);

            sample = new FieldSample
            {
                position = new Vector3(position.x, seafloorHeight, position.z),
                seafloorHeight = seafloorHeight,
                depthMeters = depthMeters,
                slopeDegrees = slopeDegrees,
                curvature = curvature,
                ridgeSignal = ridgeSignal,
                canyonSignal = canyonSignal,
                caveProximity = caveProximity,
                compositionPotential = compositionPotential,
                biomeIndex = biomeIndex,
                zoneDataIndex = zoneDataIndex,
                biomeMatrixDataIndex = biomeMatrixDataIndex,
                biomeFamilyDataIndex = biomeFamilyDataIndex,
                biomeFamilyFlags = (ulong)TokenizeFamilyFlags(biomeFamily),
                biomeProfile = biomeProfile,
                secondaryBiomeProfile = ResolveManagedSecondaryBiomeProfile(
                    volumetricOverrideApplied,
                    previousBiomeProfile,
                    mapMagicSecondaryProfile,
                    mapMagicBlend255),
                biomeFamily = biomeFamily,
                secondaryBiomeFamily = ResolveManagedSecondaryBiomeFamily(
                    volumetricOverrideApplied,
                    previousBiomeProfile,
                    mapMagicSecondaryProfile,
                    mapMagicBlend255),
                biomeInfluence = BuildManagedBiomeInfluenceCell(
                    biomeProfile,
                    biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null,
                    zoneDataIndex,
                    zoneWeight,
                    previewOverrideApplied,
                    hazardBias,
                    volumetricOverrideApplied,
                    volumetricOverrideApplied ? previousBiomeProfile : null,
                    mapMagicSecondaryProfile,
                    mapMagicBlend255),
                zone = zone,
                zoneWeight = zoneWeight,
                resolvedZoneKind = resolvedZoneKind,
                resolvedPattern = resolvedPattern,
                isPreviewOverride = previewOverrideApplied ? (byte)1 : (byte)0,
                seafloorSource = seafloorSource,
                isValid = 1
            };
            if (TryBuildTerrainDetailRuntimeSample(
                    position.x,
                    position.z,
                    out WorldTerrainDetailRuntimeSample terrainDetailSample))
            {
                AssignTerrainDetailSample(ref sample, in terrainDetailSample);
            }

            if (ShouldUpdateDiagnostics())
                UpdateDiagnostics(sample, "sample", 0f);
            return true;
        }

        private static void AssignTerrainDetailSample(
            ref FieldSample sample,
            in WorldTerrainDetailRuntimeSample terrainDetailSample)
        {
            sample.terrainDetailSample = terrainDetailSample;
            sample.macroGeologySample = terrainDetailSample.Macro;
            sample.terrainMesoDetail = terrainDetailSample.Meso;
            sample.terrainSurfaceMaterialWeights = terrainDetailSample.MaterialWeights;
            sample.terrainSurfaceMaterialClass = terrainDetailSample.DominantMaterial;
            sample.terrainDetailEligibilityFlags = terrainDetailSample.EligibilityFlags;
            sample.terrainMaterialControl1 = terrainDetailSample.Control1;
            sample.terrainMaterialControl2 = terrainDetailSample.Control2;
            sample.hasTerrainDetailSample = terrainDetailSample.IsValid ? (byte)1 : (byte)0;
        }

        public bool TrySampleBiomeInfluence(
            Vector3 position,
            out BiomeInfluenceCell influence,
            out HectonBiomeMatrixProfile primaryProfile,
            out HectonBiomeMatrixProfile secondaryProfile)
        {
            influence = default;
            primaryProfile = null;
            secondaryProfile = null;

            CellSamplingContext context = PrecomputeCellContext(position);
            if (!TrySampleSeafloor(position, in context, out FieldSample sample))
                return false;

            influence = sample.biomeInfluence;
            primaryProfile = sample.biomeProfile;
            secondaryProfile = sample.biomeInfluence.Blend255 != 0 ? sample.secondaryBiomeProfile : null;

            return primaryProfile != null || influence.PrimaryVisualFamilyId != 0;
        }

        public bool TryResolveBiomeInfluenceProfiles(
            in BiomeInfluenceCell influence,
            out HectonBiomeMatrixProfile primaryProfile,
            out HectonBiomeMatrixProfile secondaryProfile)
        {
            primaryProfile = null;
            secondaryProfile = null;
            return false;
        }

        public bool TrySampleBiomePhysicsInfluence(Vector3 position, out float buoyancyMultiplier)
        {
            buoyancyMultiplier = 1f;

            if (!TrySampleBiomeInfluence(
                    position,
                    out BiomeInfluenceCell influence,
                    out HectonBiomeMatrixProfile primaryProfile,
                    out HectonBiomeMatrixProfile secondaryProfile))
            {
                return false;
            }

            float primaryMultiplier = ResolveBiomeBuoyancyMultiplier(primaryProfile);
            if (secondaryProfile == null || influence.Blend255 == 0)
            {
                buoyancyMultiplier = primaryMultiplier;
                return true;
            }

            float secondaryMultiplier = ResolveBiomeBuoyancyMultiplier(secondaryProfile);
            float blend = influence.Blend255 * (1f / 255f);
            buoyancyMultiplier = math.lerp(primaryMultiplier, secondaryMultiplier, math.saturate(blend));
            return true;
        }

        private HectonBiomeMatrixProfile ResolveBiomeMatrixProfileById(int matrixBiomeId)
        {
            if (matrixBiomeId <= 0 || biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null)
                return null;

            return biomeMatrixDirector.MatrixCatalog.GetByMatrixIndex(matrixBiomeId);
        }

        private static BiomeInfluenceCell BuildManagedBiomeInfluenceCell(
            HectonBiomeMatrixProfile primaryProfile,
            HectonBiomeMatrixProfile currentProfile,
            int zoneDataIndex,
            float zoneWeight,
            bool previewOverrideApplied,
            float hazardBias,
            bool volumetricOverrideApplied,
            HectonBiomeMatrixProfile volumetricSecondaryProfile,
            HectonBiomeMatrixProfile mapMagicSecondaryProfile,
            byte mapMagicBlend255)
        {
            byte flags = 0;
            if (primaryProfile != null && primaryProfile.isPlaceholder)
                flags |= (byte)BiomeInfluenceFlags.Placeholder;
            if (previewOverrideApplied)
                flags |= (byte)BiomeInfluenceFlags.PreviewOverride;
            if (volumetricOverrideApplied)
                flags |= (byte)BiomeInfluenceFlags.VolumetricDepth;
            if (hazardBias >= 0.65f)
                flags |= (byte)BiomeInfluenceFlags.Hazard;
            if (IsManagedFamilyMatch(primaryProfile, "biome.family.volcanic_hadal"))
                flags |= (byte)BiomeInfluenceFlags.ThermalVent;
            if (IsManagedFamilyMatch(primaryProfile, "biome.family.abyssal_silt"))
                flags |= (byte)BiomeInfluenceFlags.SargassumCanopy;

            byte primaryBiomeId = ResolveManagedBiomeId(primaryProfile);
            byte secondaryBiomeId = 0;
            byte blend255 = 0;
            if (volumetricOverrideApplied)
            {
                secondaryBiomeId = ResolveManagedBiomeId(volumetricSecondaryProfile != null ? volumetricSecondaryProfile : currentProfile);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = 255;
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }
            else if (mapMagicSecondaryProfile != null && mapMagicBlend255 > 0)
            {
                secondaryBiomeId = ResolveManagedBiomeId(mapMagicSecondaryProfile);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = mapMagicBlend255;
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }
            else if (zoneDataIndex >= 0 && zoneWeight > 0.001f && zoneWeight < 0.999f)
            {
                secondaryBiomeId = ResolveManagedBiomeId(currentProfile);
                if (secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId)
                {
                    blend255 = (byte)(int)math.round(math.saturate(1f - zoneWeight) * 255f);
                    flags |= (byte)BiomeInfluenceFlags.TransitionEdge;
                }
                else
                {
                    secondaryBiomeId = 0;
                }
            }

            return BiomeInfluenceCell.CreateFromBiomeIds(primaryBiomeId, secondaryBiomeId, blend255, flags);
        }

        private static HectonBiomeMatrixProfile ResolveManagedSecondaryBiomeProfile(
            bool volumetricOverrideApplied,
            HectonBiomeMatrixProfile previousBiomeProfile,
            HectonBiomeMatrixProfile mapMagicSecondaryProfile,
            byte mapMagicBlend255)
        {
            if (volumetricOverrideApplied)
                return previousBiomeProfile;

            return mapMagicBlend255 != 0 ? mapMagicSecondaryProfile : null;
        }

        private static HectonBiomeFamilyProfile ResolveManagedSecondaryBiomeFamily(
            bool volumetricOverrideApplied,
            HectonBiomeMatrixProfile previousBiomeProfile,
            HectonBiomeMatrixProfile mapMagicSecondaryProfile,
            byte mapMagicBlend255)
        {
            HectonBiomeMatrixProfile profile = ResolveManagedSecondaryBiomeProfile(
                volumetricOverrideApplied,
                previousBiomeProfile,
                mapMagicSecondaryProfile,
                mapMagicBlend255);
            return profile != null ? profile.familyProfile : null;
        }

        private static float ResolveBiomeBuoyancyMultiplier(HectonBiomeMatrixProfile profile)
        {
            return profile != null ? profile.BuoyancyMultiplier : 1f;
        }

        private static bool IsManagedFamilyMatch(HectonBiomeMatrixProfile profile, string expectedFamilyId)
        {
            if (profile == null || string.IsNullOrEmpty(expectedFamilyId))
                return false;

            if (string.Equals(profile.familyId, expectedFamilyId, System.StringComparison.OrdinalIgnoreCase))
                return true;

            HectonBiomeFamilyProfile family = profile.familyProfile;
            return family != null && string.Equals(family.familyId, expectedFamilyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static byte ResolveManagedBiomeId(HectonBiomeMatrixProfile profile)
        {
            if (profile == null || profile.matrixIndex <= 0 || profile.matrixIndex > 255)
                return 0;

            return (byte)profile.matrixIndex;
        }

        public bool TryResolveSeafloorSource(Vector3 position, out SeafloorSource seafloorSource)
        {
            seafloorSource = SeafloorSource.None;

            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            return TryResolveSeafloorHeight(position, out _, out seafloorSource);
        }

        public bool TrySampleTerrainDetail(Vector3 position, out WorldTerrainDetailRuntimeSample sample)
        {
            return TryBuildTerrainDetailRuntimeSample(position.x, position.z, out sample);
        }

        public float EvaluateHeatmap(
            string heatmapChannel,
            in FieldSample sample,
            in CellSamplingContext cellContext,
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule)
        {
            string channel = string.IsNullOrWhiteSpace(heatmapChannel)
                ? family != null && !string.IsNullOrWhiteSpace(family.heatmapChannel) ? family.heatmapChannel : "generic"
                : heatmapChannel;

            float depth01 = math.saturate(sample.depthMeters / 800f);
            float shallow01 = 1f - math.saturate(sample.depthMeters / 220f);
            float midDepth01 = 1f - math.saturate(math.abs(sample.depthMeters - 260f) / 320f);
            float deep01 = math.saturate((sample.depthMeters - 180f) / 900f);
            float abyss01 = math.saturate((sample.depthMeters - 900f) / 1800f);
            float flat01 = 1f - math.saturate(sample.slopeDegrees / 28f);
            float steep01 = math.saturate((sample.slopeDegrees - 8f) / 40f);
            float terrainNoise = cellContext.TerrainNoise;
            float detailNoise = cellContext.DetailNoise;
            float ruggedBias = EvaluateRuggedBiomeBias(sample.zoneDataIndex, sample.zone);
            float fertileBias = EvaluateFertileBiomeBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind, sample.biomeFamilyDataIndex, sample.biomeFamily);
            float hazardBias = EvaluateHazardBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind);
            float serviceBias = EvaluateServiceBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind);
            float resourceBias = EvaluateResourceBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind);
            float shelterBias = EvaluateShelterBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind);
            float landmarkBias = EvaluateLandmarkBias(sample.zoneDataIndex, sample.zone, sample.resolvedZoneKind);
            float biomeMatrixBonus = EvaluateBiomeMatrixChannelBonus(channel, sample.biomeMatrixDataIndex, sample.biomeProfile);

            float value = channel switch
            {
                "rock_density" => 0.24f + steep01 * 0.34f + deep01 * 0.16f + ruggedBias * 0.16f + terrainNoise * 0.16f,
                "kelp_density" => shallow01 * 0.44f + flat01 * 0.18f + fertileBias * 0.2f + terrainNoise * 0.18f,
                "flora_density" => shallow01 * 0.34f + flat01 * 0.12f + fertileBias * 0.3f + detailNoise * 0.24f,
                "coral_density" => shallow01 * 0.24f + midDepth01 * 0.24f + flat01 * 0.14f + fertileBias * 0.22f + terrainNoise * 0.16f,
                "bio_density" => fertileBias * 0.36f + shallow01 * 0.16f + shelterBias * 0.16f + detailNoise * 0.2f + (1f - hazardBias) * 0.12f,
                "debris_density" => serviceBias * 0.34f + midDepth01 * 0.16f + terrainNoise * 0.22f + detailNoise * 0.14f + ruggedBias * 0.14f,
                "ruin_density" => serviceBias * 0.38f + deep01 * 0.12f + terrainNoise * 0.2f + landmarkBias * 0.18f + flat01 * 0.12f,
                "cave_density" => steep01 * 0.34f + ruggedBias * 0.22f + deep01 * 0.18f + terrainNoise * 0.18f + hazardBias * 0.08f,
                "landmark_strength" => steep01 * 0.24f + landmarkBias * 0.34f + abyss01 * 0.1f + terrainNoise * 0.18f + ruggedBias * 0.14f,
                "fauna_density" => fertileBias * 0.34f + shallow01 * 0.16f + shelterBias * 0.22f + detailNoise * 0.16f + (1f - steep01) * 0.12f,
                "hazard_density" => hazardBias * 0.42f + deep01 * 0.12f + steep01 * 0.14f + terrainNoise * 0.18f + landmarkBias * 0.14f,
                "resource_density" => resourceBias * 0.34f + deep01 * 0.08f + terrainNoise * 0.2f + ruggedBias * 0.18f + detailNoise * 0.2f,
                "shelter_density" => shelterBias * 0.34f + flat01 * 0.26f + shallow01 * 0.08f + fertileBias * 0.12f + detailNoise * 0.2f,
                "service_density" => serviceBias * 0.44f + terrainNoise * 0.2f + ruggedBias * 0.1f + flat01 * 0.1f + landmarkBias * 0.16f,
                _ => terrainNoise * 0.55f + detailNoise * 0.45f
            };
            value = math.saturate(value + biomeMatrixBonus);

            float patternShapedValue = EvaluatePatternShapedHeat(
                channel,
                sample,
                cellContext,
                shallow01,
                midDepth01,
                deep01,
                abyss01,
                flat01,
                steep01,
                ruggedBias,
                fertileBias,
                hazardBias,
                serviceBias,
                resourceBias,
                shelterBias,
                landmarkBias);
            patternShapedValue = math.saturate(patternShapedValue + biomeMatrixBonus * 0.92f);
            value = math.lerp(value, patternShapedValue, math.saturate(ResolvePatternFieldBlend(sample.seafloorSource, sample.zone)));
            value = ApplyManagedTectonicSpineSteepSlopeHeatBias(value, channel, sample);

            if (family != null)
            {
                value *= family.placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => math.lerp(0.8f, 1.2f, math.saturate(landmarkBias)),
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => 1.05f,
                    WorldPrefabFamilyProfile.PlacementMode.Patch => 1.08f,
                    WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => math.lerp(0.85f, 1.15f, math.saturate(hazardBias)),
                    _ => 1f
                };
            }

            if (rule != null && !string.IsNullOrWhiteSpace(rule.gameplayIntent))
                value *= 0.95f + math.saturate(rule.densityScale * 0.12f);

            value = math.saturate(value);
            if (ShouldUpdateDiagnostics())
                UpdateDiagnostics(sample, channel, value);
            return value;
        }

        private static float ApplyManagedTectonicSpineSteepSlopeHeatBias(
            float value,
            string channel,
            in FieldSample sample)
        {
            if (((BiomeFamilyFlags)sample.biomeFamilyFlags & BiomeFamilyFlags.Tectonic) == 0 ||
                sample.slopeDegrees < 45f)
            {
                return value;
            }

            float slope01 = math.saturate((sample.slopeDegrees - 45f) / 30f);
            return channel switch
            {
                "rock_density" => math.max(value, 0.74f + slope01 * 0.20f),
                "debris_density" => math.max(value, 0.80f + slope01 * 0.18f),
                "landmark_strength" => math.max(value, 0.62f + slope01 * 0.18f),
                "hazard_density" => math.max(value, 0.68f + slope01 * 0.18f),
                _ => value
            };
        }

        private bool ShouldUpdateDiagnostics()
        {
#if UNITY_EDITOR
            return enableLiveRuntimeDiagnostics;
#else
            return false;
#endif
        }

        private float EvaluatePatternShapedHeat(
            string channel,
            in FieldSample sample,
            in CellSamplingContext cellContext,
            float shallow01,
            float midDepth01,
            float deep01,
            float abyss01,
            float flat01,
            float steep01,
            float ruggedBias,
            float fertileBias,
            float hazardBias,
            float serviceBias,
            float resourceBias,
            float shelterBias,
            float landmarkBias)
        {
            float terrainNoise = cellContext.TerrainNoise;
            float detailNoise = cellContext.DetailNoise;
            float sedimentNoise = cellContext.SedimentFieldNoise;
            float fertileNoise = cellContext.FertileFieldNoise;
            float reefNoise = cellContext.ReefFieldNoise;
            float industrialNoise = cellContext.IndustrialFieldNoise;
            float hazardNoise = cellContext.HazardFieldNoise;
            float landmarkNoise = cellContext.LandmarkFieldNoise;
            float basinNoise = cellContext.BasinFieldNoise;

            float sedimentField = math.saturate(
                resourceBias * 0.32f +
                shelterBias * 0.18f +
                flat01 * 0.16f +
                terrainNoise * 0.14f +
                sedimentNoise * 0.20f);
            float fertileField = math.saturate(
                fertileBias * 0.34f +
                shallow01 * 0.16f +
                detailNoise * 0.12f +
                fertileNoise * 0.22f +
                shelterBias * 0.08f +
                (1f - hazardBias) * 0.08f);
            float reefField = math.saturate(
                fertileBias * 0.24f +
                landmarkBias * 0.14f +
                shallow01 * 0.10f +
                reefNoise * 0.24f +
                flat01 * 0.08f +
                detailNoise * 0.12f +
                midDepth01 * 0.08f);
            float industrialField = math.saturate(
                serviceBias * 0.34f +
                industrialNoise * 0.28f +
                terrainNoise * 0.10f +
                ruggedBias * 0.08f +
                deep01 * 0.08f +
                landmarkBias * 0.12f);
            float hazardField = math.saturate(
                hazardBias * 0.38f +
                steep01 * 0.12f +
                deep01 * 0.12f +
                hazardNoise * 0.24f +
                ruggedBias * 0.14f);
            float landmarkField = math.saturate(
                landmarkBias * 0.34f +
                steep01 * 0.16f +
                landmarkNoise * 0.26f +
                ruggedBias * 0.10f +
                deep01 * 0.08f +
                reefField * 0.06f);
            float shelterField = math.saturate(
                shelterBias * 0.34f +
                flat01 * 0.18f +
                fertileField * 0.14f +
                basinNoise * 0.18f +
                detailNoise * 0.16f);
            float abyssField = math.saturate(
                abyss01 * 0.44f +
                hazardField * 0.16f +
                ruggedBias * 0.12f +
                terrainNoise * 0.12f +
                industrialNoise * 0.08f +
                (1f - fertileField) * 0.08f);

            float shapedValue = sample.resolvedPattern switch
            {
                WorldProceduralPattern.FertileShallows => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.22f + ruggedBias * 0.12f + flat01 * 0.08f,
                    "kelp_density" => fertileField * 0.92f,
                    "flora_density" => fertileField * 0.84f,
                    "coral_density" => reefField * 0.90f,
                    "bio_density" => fertileField * 0.62f + shelterField * 0.24f,
                    "debris_density" => ShapeShallowTechnogenicTrace(industrialField, 0.50f),
                    "ruin_density" => ShapeShallowTechnogenicTrace(industrialField, 0.34f) + landmarkField * 0.12f,
                    "cave_density" => landmarkField * 0.28f + hazardField * 0.16f,
                    "landmark_strength" => landmarkField * 0.48f + reefField * 0.12f,
                    "fauna_density" => fertileField * 0.56f + shelterField * 0.30f,
                    "hazard_density" => hazardField * 0.26f,
                    "resource_density" => sedimentField * 0.40f + fertileField * 0.18f,
                    "shelter_density" => shelterField * 0.78f,
                    "service_density" => ShapeShallowTechnogenicTrace(industrialField, 0.56f),
                    _ => fertileField * 0.58f + sedimentField * 0.14f
                },
                WorldProceduralPattern.ReefNavigation => channel switch
                {
                    "rock_density" => 0.20f + sedimentField * 0.18f + ruggedBias * 0.12f,
                    "kelp_density" => fertileField * 0.72f + reefField * 0.14f,
                    "flora_density" => fertileField * 0.70f + reefField * 0.12f,
                    "coral_density" => reefField * 0.94f,
                    "bio_density" => fertileField * 0.44f + shelterField * 0.22f,
                    "debris_density" => ShapeShallowTechnogenicTrace(industrialField, 0.42f),
                    "ruin_density" => ShapeShallowTechnogenicTrace(industrialField, 0.32f) + landmarkField * 0.14f,
                    "cave_density" => landmarkField * 0.38f + hazardField * 0.18f,
                    "landmark_strength" => landmarkField * 0.68f + reefField * 0.16f,
                    "fauna_density" => fertileField * 0.42f + shelterField * 0.18f,
                    "hazard_density" => hazardField * 0.28f,
                    "resource_density" => sedimentField * 0.32f + landmarkField * 0.12f,
                    "shelter_density" => shelterField * 0.54f + reefField * 0.12f,
                    "service_density" => ShapeShallowTechnogenicTrace(industrialField, 0.49f),
                    _ => reefField * 0.56f + landmarkField * 0.18f
                },
                WorldProceduralPattern.SedimentResources => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.86f + ruggedBias * 0.12f,
                    "kelp_density" => fertileField * 0.24f + shelterField * 0.10f,
                    "flora_density" => fertileField * 0.14f + shelterField * 0.08f,
                    "coral_density" => reefField * 0.14f + fertileField * 0.06f,
                    "bio_density" => shelterField * 0.52f + fertileField * 0.12f,
                    "debris_density" => industrialField * 0.42f + hazardField * 0.08f,
                    "ruin_density" => industrialField * 0.44f + landmarkField * 0.22f + sedimentField * 0.08f,
                    "cave_density" => hazardField * 0.30f + landmarkField * 0.30f + ruggedBias * 0.18f + sedimentField * 0.06f,
                    "landmark_strength" => landmarkField * 0.58f + sedimentField * 0.14f + ruggedBias * 0.08f,
                    "fauna_density" => shelterField * 0.42f + fertileField * 0.14f,
                    "hazard_density" => hazardField * 0.34f,
                    "resource_density" => sedimentField * 0.92f,
                    "shelter_density" => shelterField * 0.88f,
                    "service_density" => industrialField * 0.48f + sedimentField * 0.08f + landmarkField * 0.06f,
                    _ => sedimentField * 0.62f + shelterField * 0.18f
                },
                WorldProceduralPattern.IndustrialService => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.34f + ruggedBias * 0.10f,
                    "kelp_density" => fertileField * 0.18f,
                    "flora_density" => fertileField * 0.16f,
                    "coral_density" => reefField * 0.14f,
                    "bio_density" => shelterField * 0.24f,
                    "debris_density" => industrialField * 0.90f,
                    "ruin_density" => industrialField * 0.76f + landmarkField * 0.12f,
                    "cave_density" => hazardField * 0.22f + landmarkField * 0.18f + industrialField * 0.12f,
                    "landmark_strength" => landmarkField * 0.44f + industrialField * 0.22f,
                    "fauna_density" => hazardField * 0.16f + shelterField * 0.14f,
                    "hazard_density" => hazardField * 0.46f + industrialField * 0.12f,
                    "resource_density" => sedimentField * 0.26f + industrialField * 0.12f,
                    "shelter_density" => shelterField * 0.22f,
                    "service_density" => industrialField * 0.96f,
                    _ => industrialField * 0.64f + landmarkField * 0.14f
                },
                WorldProceduralPattern.BrineToxic => channel switch
                {
                    "rock_density" => 0.16f + sedimentField * 0.28f + industrialField * 0.18f + ruggedBias * 0.08f,
                    "kelp_density" => fertileField * 0.08f,
                    "flora_density" => fertileField * 0.10f,
                    "coral_density" => reefField * 0.08f,
                    "bio_density" => fertileField * 0.16f + shelterField * 0.12f + hazardField * 0.08f,
                    "debris_density" => industrialField * 0.82f,
                    "ruin_density" => industrialField * 0.58f + landmarkField * 0.14f,
                    "cave_density" => hazardField * 0.24f + landmarkField * 0.18f + industrialField * 0.12f,
                    "landmark_strength" => landmarkField * 0.36f + industrialField * 0.18f,
                    "fauna_density" => fertileField * 0.12f + hazardField * 0.14f,
                    "hazard_density" => hazardField * 0.54f + industrialField * 0.12f,
                    "resource_density" => sedimentField * 0.24f + industrialField * 0.14f,
                    "shelter_density" => shelterField * 0.18f,
                    "service_density" => industrialField * 0.82f,
                    _ => industrialField * 0.62f + hazardField * 0.10f
                },
                WorldProceduralPattern.VolcanicPressure => channel switch
                {
                    "rock_density" => 0.20f + sedimentField * 0.46f + ruggedBias * 0.18f + hazardField * 0.10f,
                    "kelp_density" => fertileField * 0.06f,
                    "flora_density" => fertileField * 0.08f,
                    "coral_density" => reefField * 0.06f,
                    "bio_density" => fertileField * 0.10f + hazardField * 0.10f + abyssField * 0.06f,
                    "debris_density" => industrialField * 0.34f + hazardField * 0.16f,
                    "ruin_density" => industrialField * 0.42f + landmarkField * 0.18f + hazardField * 0.12f,
                    "cave_density" => landmarkField * 0.48f + hazardField * 0.28f + ruggedBias * 0.10f,
                    "landmark_strength" => landmarkField * 0.86f + hazardField * 0.10f,
                    "fauna_density" => hazardField * 0.18f + abyssField * 0.10f,
                    "hazard_density" => hazardField * 0.76f,
                    "resource_density" => sedimentField * 0.22f + hazardField * 0.10f,
                    "shelter_density" => shelterField * 0.14f,
                    "service_density" => industrialField * 0.42f + hazardField * 0.10f,
                    _ => landmarkField * 0.52f + hazardField * 0.16f + sedimentField * 0.12f
                },
                WorldProceduralPattern.RiftHazard => channel switch
                {
                    "rock_density" => 0.18f + hazardField * 0.36f + ruggedBias * 0.18f + sedimentField * 0.16f,
                    "kelp_density" => fertileField * 0.10f,
                    "flora_density" => fertileField * 0.12f,
                    "coral_density" => reefField * 0.10f,
                    "bio_density" => hazardField * 0.24f + abyssField * 0.10f,
                    "debris_density" => industrialField * 0.36f + hazardField * 0.12f,
                    "ruin_density" => industrialField * 0.42f + hazardField * 0.18f + landmarkField * 0.10f,
                    "cave_density" => hazardField * 0.82f,
                    "landmark_strength" => landmarkField * 0.52f + hazardField * 0.16f,
                    "fauna_density" => hazardField * 0.48f + abyssField * 0.18f,
                    "hazard_density" => hazardField * 0.98f,
                    "resource_density" => sedimentField * 0.24f + hazardField * 0.10f,
                    "shelter_density" => shelterField * 0.18f,
                    "service_density" => industrialField * 0.34f,
                    _ => hazardField * 0.64f + industrialField * 0.14f
                },
                WorldProceduralPattern.AbyssSparse => channel switch
                {
                    "rock_density" => 0.20f + abyssField * 0.44f + ruggedBias * 0.16f + sedimentField * 0.18f,
                    "kelp_density" => fertileField * 0.06f,
                    "flora_density" => fertileField * 0.08f,
                    "coral_density" => reefField * 0.08f,
                    "bio_density" => abyssField * 0.18f + shelterField * 0.10f,
                    "debris_density" => industrialField * 0.18f + abyssField * 0.08f,
                    "ruin_density" => industrialField * 0.22f + landmarkField * 0.18f,
                    "cave_density" => hazardField * 0.22f + landmarkField * 0.22f,
                    "landmark_strength" => landmarkField * 0.48f + abyssField * 0.12f,
                    "fauna_density" => abyssField * 0.16f,
                    "hazard_density" => hazardField * 0.24f + abyssField * 0.12f,
                    "resource_density" => sedimentField * 0.18f + abyssField * 0.08f,
                    "shelter_density" => shelterField * 0.14f,
                    "service_density" => industrialField * 0.16f,
                    _ => abyssField * 0.52f + landmarkField * 0.12f
                },
                WorldProceduralPattern.LandmarkCorridor => channel switch
                {
                    "rock_density" => 0.22f + sedimentField * 0.26f + ruggedBias * 0.18f,
                    "kelp_density" => fertileField * 0.24f,
                    "flora_density" => fertileField * 0.22f + landmarkField * 0.08f,
                    "coral_density" => reefField * 0.28f,
                    "bio_density" => shelterField * 0.22f + fertileField * 0.10f,
                    "debris_density" => industrialField * 0.26f,
                    "ruin_density" => industrialField * 0.34f + landmarkField * 0.24f,
                    "cave_density" => landmarkField * 0.84f,
                    "landmark_strength" => landmarkField * 0.98f,
                    "fauna_density" => shelterField * 0.18f + hazardField * 0.10f,
                    "hazard_density" => hazardField * 0.34f + landmarkField * 0.08f,
                    "resource_density" => sedimentField * 0.22f + landmarkField * 0.10f,
                    "shelter_density" => shelterField * 0.28f,
                    "service_density" => industrialField * 0.26f + landmarkField * 0.10f,
                    _ => landmarkField * 0.74f + sedimentField * 0.10f
                },
                _ => terrainNoise * 0.55f + detailNoise * 0.45f
            };

            return math.saturate(shapedValue);
        }

        private static float ResolvePatternFieldBlend(SeafloorSource source, WorldZoneAnchor zone)
        {
            return source switch
            {
                SeafloorSource.FallbackSynthetic => zone == null ? 0.78f : 0.66f,
                SeafloorSource.MacroGeologyFallback => zone == null ? 0.56f : 0.42f,
                SeafloorSource.SceneProbeLegacy => zone == null ? 0.42f : 0.28f,
                SeafloorSource.TerrainProviderHeight => zone == null ? 0.34f : 0.18f,
                SeafloorSource.MapMagicHeight => zone == null ? 0.34f : 0.18f,
                _ => 0.2f
            };
        }

        private bool TryResolveSeafloorHeight(Vector3 position, out float seafloorHeight, out SeafloorSource seafloorSource)
        {
            Vector2Int cacheKey = GetHeightCacheKey(position.x, position.z);
            if (TryReadSeafloorHeightCache(cacheKey, out CachedHeightSample cachedSample))
            {
                bool staleFallbackSample = cachedSample.Source == SeafloorSource.FallbackSynthetic &&
                                           cachedSample.SamplingFrameId != _samplingFrameId;
                if (!staleFallbackSample)
                {
                    seafloorHeight = cachedSample.Height;
                    seafloorSource = cachedSample.Source;
                    return true;
                }
            }

            bool resolved = TryResolveSeafloorHeightUncached(position, out seafloorHeight, out seafloorSource);
            if (resolved)
            {
                WriteSeafloorHeightCache(cacheKey, seafloorHeight, seafloorSource);
            }

            return resolved;
        }

        private bool TryResolveBiomeIndex(float x, float z, out int biomeIndex)
        {
            return TryResolveBiomeReadout(x, z, out biomeIndex, out _);
        }

        private bool TryResolveMatrixBiomeId(float x, float z, out int biomeMatrixId)
        {
            TryResolveBiomeReadout(x, z, out _, out biomeMatrixId);
            return biomeMatrixId > 0;
        }

        private bool TryResolveBiomeReadout(float x, float z, out int biomeIndex, out int biomeMatrixId)
        {
            return TryResolveBiomeReadout(
                x,
                z,
                out biomeIndex,
                out biomeMatrixId,
                out _,
                out _,
                out _);
        }

        private bool TryResolveBiomeReadout(
            float x,
            float z,
            out int biomeIndex,
            out int biomeMatrixId,
            out int secondaryBiomeMatrixId,
            out int biomeBlend255,
            out int mapMagicBiomeDataValid)
        {
            biomeMatrixId = ResolveCurrentMatrixBiomeId();
            biomeIndex = biomeMatrixId;
            secondaryBiomeMatrixId = 0;
            biomeBlend255 = 0;
            mapMagicBiomeDataValid = 0;

            if (mapMagicBridge != null)
            {
                int mapMagicMatrixBiomeId;
                int alphamapLayer;
                if (mapMagicBridge.TryGetMatrixBiomeInfluence(
                        x,
                        z,
                        out mapMagicMatrixBiomeId,
                        out secondaryBiomeMatrixId,
                        out byte blend255,
                        out alphamapLayer,
                        out _))
                {
                    biomeMatrixId = mapMagicMatrixBiomeId;
                    biomeIndex = alphamapLayer;
                    biomeBlend255 = blend255;
                    mapMagicBiomeDataValid = 1;
                }
                else if (mapMagicBridge.TryGetMatrixBiomeId(x, z, out mapMagicMatrixBiomeId, out alphamapLayer))
                {
                    biomeMatrixId = mapMagicMatrixBiomeId;
                    biomeIndex = alphamapLayer;
                    mapMagicBiomeDataValid = 1;
                }
                else if (!mapMagicBridge.SandboxProceduralTerrainOnly &&
                         mapMagicBridge.TryGetBiomeIndex(x, z, out int mapMagicBiomeIndex))
                {
                    biomeIndex = mapMagicBiomeIndex;
                }
            }

            if (enableLiveRuntimeDiagnostics)
                _debugBiomeCacheMisses++;

            return biomeIndex > 0 || biomeMatrixId > 0;
        }

        private int ResolveCurrentMatrixBiomeId()
        {
            HectonBiomeMatrixProfile profile = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
            return profile != null && profile.matrixIndex > 0 ? profile.matrixIndex : 0;
        }

        private bool TryResolveSeafloorHeightUncached(Vector3 position, out float seafloorHeight, out SeafloorSource seafloorSource)
        {
            seafloorHeight = 0f;
            seafloorSource = SeafloorSource.None;

            // R97 FIX: finite guard added — the provider lane below already had it; a NaN/Inf from a
            // bad MapMagic payload on this primary lane was cached as depth truth and poisoned
            // DepthMeters/slope/pattern outputs downstream.
            if (mapMagicBridge != null &&
                mapMagicBridge.TryGetHeight(position.x, position.z, out seafloorHeight) &&
                math.isfinite(seafloorHeight))
            {
                seafloorSource = SeafloorSource.MapMagicHeight;
                return true;
            }

            ITerrainProvider terrainProvider = ResolveTerrainProviderRuntime();
            if (terrainProvider != null &&
                terrainProvider.TryGetHeight(position.x, position.z, out seafloorHeight) &&
                math.isfinite(seafloorHeight))
            {
                seafloorSource = SeafloorSource.TerrainProviderHeight;
                return true;
            }

            // R97 FIX: the synthetic-lane fallback surface was derived from the QUERY's Y
            // (position.y + 120), but the height cache is keyed by XZ only — the first caller's
            // altitude got frozen as terrain truth for that column (query-order-dependent depth).
            // The provider/bridge lanes inside ResolveWaterSurfaceLevel still win when present;
            // only the last-resort constant is now Y-independent and deterministic.
            float fallbackSurface = ResolveWaterSurfaceLevel(DefaultWaterSurfaceLevelY);
            if (TryResolveMacroGeologyFallbackHeight(position.x, position.z, out seafloorHeight))
            {
                seafloorSource = SeafloorSource.MacroGeologyFallback;
                return true;
            }

            seafloorHeight = fallbackSurface - EstimateFallbackDepth(position.x, position.z);

            seafloorSource = SeafloorSource.FallbackSynthetic;
            return true;
        }

        private bool TryResolveMacroGeologyFallbackHeight(float x, float z, out float seafloorHeight)
        {
            WorldMacroGeologyParams parameters = BuildMacroGeologyParams();

            WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(x, z, in parameters);
            if (!math.isfinite(sample.HeightMeters))
            {
                seafloorHeight = 0f;
                return false;
            }

            seafloorHeight = sample.HeightMeters;
            return true;
        }

        private WorldMacroGeologyParams BuildMacroGeologyParams()
        {
            int runtimeWorldSeed = 0;
            if (global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int activeRuntimeWorldSeed))
                runtimeWorldSeed = activeRuntimeWorldSeed;

            WorldMacroGeologyParams parameters = WorldMacroGeologyParams.CreateDefault(
                WorldMacroGeologyFields.CombineWorldSeed(macroGeologyAuthoringSeed, runtimeWorldSeed));
            parameters.WorldExtentMeters = math.max(
                WorldMacroGeologyFields.MinimumWorldExtentMeters,
                macroGeologyWorldExtentMeters);
            parameters.ChunkSizeMeters = math.max(128f, macroGeologyChunkSizeMeters);
            parameters.DetailProbeMeters = math.max(8f, macroGeologyDetailProbeMeters);
            parameters.WaterSurfaceY = 0f;
            return parameters;
        }

        private bool TryBuildTerrainDetailRuntimeSample(
            float x,
            float z,
            out WorldTerrainDetailRuntimeSample sample)
        {
            sample = default;
            WorldMacroGeologyParams parameters = BuildMacroGeologyParams();
            WorldMacroGeologySample macro = WorldMacroGeologyFields.Evaluate(x, z, in parameters);
            if (!math.isfinite(macro.HeightMeters))
                return false;

            WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(
                in macro,
                x,
                z,
                parameters.Seed);
            WorldTerrainMesoDetailParams mesoParams = WorldTerrainMesoDetailFields.CreateDefaultParams(parameters.Seed);
            mesoParams.PreviewExtentMeters = WorldTerrainDetailContracts.MesoProofExtentMeters;
            WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(
                in macro,
                x,
                z,
                in mesoParams);
            weights = WorldTerrainSurfaceMaterialResolver.ApplyMesoDetailBias(weights, in meso);
            WorldTerrainDetailEligibilityFlags eligibility =
                WorldTerrainMesoDetailFields.ResolveEligibilityFlags(in macro, in meso, in weights);
            var splats = WorldTerrainSurfaceMaterialResolver.ResolveControlSplats(in weights);

            sample = new WorldTerrainDetailRuntimeSample
            {
                Macro = macro,
                Meso = meso,
                MaterialWeights = weights,
                DominantMaterial = WorldTerrainSurfaceMaterialResolver.ResolveDominant(in weights),
                EligibilityFlags = eligibility,
                Control1 = splats.Control1,
                Control2 = splats.Control2,
                MacroArtifactVersion = WorldMacroGeologyFields.ArtifactVersion,
                SurfaceMaterialContractVersion = WorldTerrainSurfaceMaterialResolver.ContractVersion,
                MesoDetailContractVersion = WorldTerrainMesoDetailFields.ContractVersion,
                DetailEligibilityContractVersion = WorldTerrainDetailContracts.ContractVersion
            };
            return sample.IsValid;
        }

        private float ResolveWaterSurfaceLevel(float fallbackWaterSurfaceLevel)
        {
            if (mapMagicBridge != null && TryResolveWaterSurfaceLevel(mapMagicBridge.WaterSurfaceLevel, out float waterSurfaceLevel))
                return waterSurfaceLevel;

            ITerrainProvider terrainProvider = ResolveTerrainProviderRuntime();
            if (terrainProvider != null && TryResolveWaterSurfaceLevel(terrainProvider.WaterSurfaceLevel, out waterSurfaceLevel))
                return waterSurfaceLevel;

            return TryResolveWaterSurfaceLevel(fallbackWaterSurfaceLevel, out float fallbackSurfaceLevel)
                ? fallbackSurfaceLevel
                : DefaultWaterSurfaceLevelY;
        }

        private ITerrainProvider ResolveTerrainProviderRuntime()
        {
            if (IsTerrainProviderAvailable(_terrainProviderRuntime))
                return _terrainProviderRuntime;

            ITerrainProvider registryProvider = GlobalRegistry.Terrain;
            if (IsTerrainProviderAvailable(registryProvider))
            {
                _terrainProviderRuntime = registryProvider;
                return _terrainProviderRuntime;
            }

            if (mapMagicBridge != null && mapMagicBridge.IsAvailable)
            {
                _terrainProviderRuntime = mapMagicBridge;
                return _terrainProviderRuntime;
            }

            _terrainProviderRuntime = null;
            return null;
        }

        private static bool IsTerrainProviderAvailable(ITerrainProvider terrainProvider)
        {
            if (terrainProvider is Object unityObject && unityObject == null)
                return false;

            if (terrainProvider is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;

            return terrainProvider != null && terrainProvider.IsAvailable;
        }

        private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) > 0.0001f &&
                math.abs(candidateWaterSurfaceLevel) <= 1000f)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private bool TryGetLocalTerrainContext(Vector3 position, out LocalTerrainContext terrainContext)
        {
            terrainContext = default;
            if (!TryGetCellHeightContext(position, out CellHeightContext cellHeightContext))
                return false;

            float probe = math.max(1f, slopeProbeMeters);
            float dx = (cellHeightContext.EastHeight - cellHeightContext.WestHeight) / (probe * 2f);
            float dz = (cellHeightContext.NorthHeight - cellHeightContext.SouthHeight) / (probe * 2f);
            float gradient = FastLength2D(dx, dz);
            float slopeDegrees = math.degrees(MathLodApproximation.ApproxAtanFast(gradient));
            float curvature = (cellHeightContext.WestHeight + cellHeightContext.EastHeight + cellHeightContext.NorthHeight + cellHeightContext.SouthHeight - (cellHeightContext.CenterHeight * 4f)) / math.max(0.0001f, probe * probe);

            terrainContext = new LocalTerrainContext
            {
                CenterHeight = cellHeightContext.CenterHeight,
                NorthHeight = cellHeightContext.NorthHeight,
                SouthHeight = cellHeightContext.SouthHeight,
                EastHeight = cellHeightContext.EastHeight,
                WestHeight = cellHeightContext.WestHeight,
                SlopeDegrees = slopeDegrees,
                Curvature = math.clamp(curvature / 0.85f, -1f, 1f),
                CenterSource = cellHeightContext.CenterSource
            };
            return true;
        }

        private bool TryGetCellHeightContext(Vector3 position, out CellHeightContext terrainContext)
        {
            terrainContext = default;
            if (!TryResolveSeafloorHeight(position, out float centerHeight, out SeafloorSource centerSource))
                return false;

            float probe = math.max(1f, slopeProbeMeters);
            if (!TryResolveSeafloorHeight(new Vector3(position.x, centerHeight, position.z + probe), out float northHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x, centerHeight, position.z - probe), out float southHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x + probe, centerHeight, position.z), out float eastHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x - probe, centerHeight, position.z), out float westHeight, out _))
            {
                return false;
            }

            float slopeDegrees = CalculateSlopeDegrees(centerHeight, northHeight, southHeight, eastHeight, westHeight, probe);
            if ((centerSource == SeafloorSource.MapMagicHeight ||
                 centerSource == SeafloorSource.TerrainProviderHeight) &&
                steepSlopeGradientCheatMaxDropMeters > 0f &&
                slopeDegrees >= steepSlopeGradientCheatThresholdDegrees)
            {
                centerHeight = ResolveSteepGradientContactCheat(
                    centerHeight,
                    northHeight,
                    southHeight,
                    eastHeight,
                    westHeight,
                    slopeDegrees,
                    steepSlopeGradientCheatThresholdDegrees,
                    steepSlopeGradientCheatMaxDropMeters);
            }

            terrainContext = new CellHeightContext
            {
                CenterHeight = centerHeight,
                NorthHeight = northHeight,
                SouthHeight = southHeight,
                EastHeight = eastHeight,
                WestHeight = westHeight,
                CenterSource = centerSource
            };
            return true;
        }

        private static float CalculateSlopeDegrees(float centerHeight, float northHeight, float southHeight, float eastHeight, float westHeight, float probeMeters)
        {
            float probe = math.max(0.0001f, probeMeters);
            float dx = (eastHeight - westHeight) / (probe * 2f);
            float dz = (northHeight - southHeight) / (probe * 2f);
            return math.degrees(MathLodApproximation.ApproxAtanFast(FastLength2D(dx, dz)));
        }

        private static float ResolveSteepGradientContactCheat(
            float centerHeight,
            float northHeight,
            float southHeight,
            float eastHeight,
            float westHeight,
            float slopeDegrees,
            float thresholdDegrees,
            float maxDropMeters)
        {
            float lowerNeighbor = math.min(math.min(northHeight, southHeight), math.min(eastHeight, westHeight));
            float availableDrop = math.max(0f, centerHeight - lowerNeighbor);
            if (availableDrop <= 0f)
                return centerHeight;

            float slope01 = math.saturate((slopeDegrees - thresholdDegrees) / math.max(0.0001f, 78f - thresholdDegrees));
            float drop = math.min(math.max(0f, maxDropMeters), availableDrop * math.lerp(0.35f, 0.75f, math.saturate(slope01)));
            return centerHeight - drop;
        }

        private float EstimateFallbackDepth(float x, float z)
        {
            float broad = EvaluateNoise01(x + 311.1f, z - 177.4f, fieldNoiseScale * 0.55f);
            float detail = EvaluateNoise01(x - 91.6f, z + 441.2f, detailNoiseScale * 0.7f);
            float depth = math.lerp(70f, 240f, math.saturate((broad * 0.7f) + (detail * 0.3f)));
            return math.clamp(depth, 40f, 320f);
        }

        private float EvaluateRidgeSignal(float curvature, float slopeDegrees, int zoneDataIndex, WorldZoneAnchor zone)
        {
            float slope01 = math.saturate((slopeDegrees - 8f) / 36f);
            float rugged = EvaluateRuggedBiomeBias(zoneDataIndex, zone);
            return math.saturate(math.max(0f, curvature) * 0.62f + slope01 * 0.26f + rugged * 0.12f);
        }

        private float EvaluateCanyonSignal(float curvature, float slopeDegrees, int zoneDataIndex, WorldZoneAnchor zone)
        {
            float slope01 = math.saturate((slopeDegrees - 10f) / 34f);
            float hazard = EvaluateHazardBias(zoneDataIndex, zone, zone != null ? zone.Kind : WorldZoneAnchor.ZoneKind.Generic);
            return math.saturate(math.max(0f, -curvature) * 0.58f + slope01 * 0.22f + hazard * 0.20f);
        }

        private float EvaluateCaveProximity(
            float depthMeters,
            float slopeDegrees,
            int zoneDataIndex,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind resolvedZoneKind,
            float caveNoise)
        {
            float slope01 = math.saturate((slopeDegrees - 8f) / 40f);
            float deep01 = math.saturate((depthMeters - 120f) / 780f);
            float rugged = EvaluateRuggedBiomeBias(zoneDataIndex, zone);
            float hazard = EvaluateHazardBias(zoneDataIndex, zone, resolvedZoneKind);
            float landmark = EvaluateLandmarkBias(zoneDataIndex, zone, resolvedZoneKind);
            return math.saturate(
                slope01 * 0.22f +
                deep01 * 0.10f +
                rugged * 0.24f +
                hazard * 0.18f +
                landmark * 0.14f +
                caveNoise * 0.12f);
        }

        private float EvaluateCompositionPotential(
            float slopeDegrees,
            float curvature,
            float ridgeSignal,
            float canyonSignal,
            float caveProximity,
            float variation)
        {
            float slope01 = math.saturate((slopeDegrees - 6f) / 42f);
            return math.saturate(
                slope01 * 0.16f +
                math.abs(curvature) * 0.18f +
                ridgeSignal * 0.20f +
                canyonSignal * 0.18f +
                caveProximity * 0.18f +
                variation * 0.10f);
        }

        private WorldZoneAnchor ResolveZone(Vector3 position, out float zoneWeight)
        {
            WorldZoneAnchor best = null;
            float bestWeight = 0f;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _anchorCount; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                anchor.EvaluatePlayerState(
                    position,
                    out float distanceSqr,
                    out float weight,
                    out _,
                    out _,
                    out _);

                if (weight <= 0.001f)
                    continue;

                if (best == null ||
                    weight > bestWeight ||
                    (math.abs(weight - bestWeight) <= 0.000001f && distanceSqr < bestDistanceSqr))
                {
                    best = anchor;
                    bestWeight = weight;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (best == null)
                best = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;

            zoneWeight = best != null ? math.max(bestWeight, best.EvaluateActivationWeight(position)) : 0f;
            return best;
        }

        private void RefreshActiveAnchorsSnapshot()
        {
            _anchorCount = WorldZoneAnchor.CopyActiveAnchorsTo(_anchors, MaxZoneAnchorSnapshotCount);
        }

        private float EvaluateNoise01(float x, float z, float scale)
        {
            EnsureNoiseLookupTable();
            return TryResolveVaultBuffer(BufferID.WorldProceduralFieldNoiseLookup, in _noiseLookupTableHandle, out NativeArray<ushort> noiseLookupTable)
                ? SampleNoiseLookup01(noiseLookupTable, x, z, scale)
                : 0f;
        }

        private HectonBiomeFamilyProfile ResolveFallbackBiomeFamily(
            Vector3 position,
            float depthMeters,
            float slopeDegrees,
            WorldZoneAnchor.ZoneKind zoneKindHint,
            in CellSamplingContext cellContext)
        {
            float ruggedNoise = cellContext.RuggedBiomeNoise;
            float fertileNoise = cellContext.FertileBiomeNoise;
            float thermalNoise = cellContext.ThermalBiomeNoise;
            float metallicNoise = cellContext.MetallicBiomeNoise;
            float crystalNoise = cellContext.CrystalBiomeNoise;
            float voidNoise = cellContext.VoidBiomeNoise;
            float reefNoise = cellContext.ReefBiomeNoise;
            float basinMacroNoise = cellContext.BasinMacroNoise;
            float reefMacroNoise = cellContext.ReefMacroNoise;
            float serviceMacroNoise = cellContext.ServiceMacroNoise;
            float riftMacroNoise = cellContext.RiftMacroNoise;

            float depth01 = math.saturate(depthMeters / 1200f);
            float steep01 = math.saturate((slopeDegrees - 8f) / 40f);
            float shallow01 = 1f - math.saturate(depthMeters / 220f);
            float resourceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Resources || zoneKindHint == WorldZoneAnchor.ZoneKind.Fabrication
                ? 1f
                : zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation
                    ? 0.55f
                    : 0f;
            float serviceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Service || zoneKindHint == WorldZoneAnchor.ZoneKind.Power
                ? 1f
                : 0f;
            float hazardZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Combat || zoneKindHint == WorldZoneAnchor.ZoneKind.Progression
                ? 1f
                : 0f;
            float navigationZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation ? 1f : 0f;

            float fertileScore = math.saturate(
                ((fertileNoise * 0.65f) + (reefNoise * 0.35f))
                - (resourceZoneBias * 0.08f)
                - (serviceZoneBias * 0.16f)
                - (hazardZoneBias * 0.18f)
                + (navigationZoneBias * 0.08f));
            float ruggedScore = math.saturate((ruggedNoise * 0.55f) + (steep01 * 0.45f));
            float thermalScore = math.saturate((thermalNoise * 0.75f) + (depth01 * 0.25f));
            float metallicScore = math.saturate((metallicNoise * 0.7f) + (depth01 * 0.3f));
            float voidScore = math.saturate((voidNoise * 0.7f) + (depth01 * 0.3f));
            float sedimentScore = math.saturate(
                ((1f - ruggedScore) * 0.24f)
                + ((1f - thermalScore) * 0.14f)
                + (resourceZoneBias * 0.22f)
                + (shallow01 * 0.08f)
                + (fertileNoise * 0.12f)
                + (reefNoise * 0.04f));
            float serviceScore = math.saturate(
                (thermalScore * 0.34f)
                + (metallicScore * 0.34f)
                + (serviceZoneBias * 0.24f)
                + (depth01 * 0.08f));
            float hazardScore = math.saturate(
                (ruggedScore * 0.28f)
                + (thermalScore * 0.16f)
                + (voidScore * 0.18f)
                + (hazardZoneBias * 0.26f)
                + (depth01 * 0.12f));
            float reefScore = math.saturate(
                (fertileScore * 0.46f)
                + (reefNoise * 0.28f)
                + (shallow01 * 0.14f)
                + (navigationZoneBias * 0.12f));
            float sedimentContinuity = math.saturate(
                (resourceZoneBias * 0.28f)
                + (basinMacroNoise * 0.24f)
                + ((1f - ruggedScore) * 0.12f)
                + ((1f - thermalScore) * 0.1f)
                + (shallow01 * 0.08f)
                + (depth01 * 0.06f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float reefContinuity = math.saturate(
                (reefScore * 0.42f)
                + (reefMacroNoise * 0.24f)
                + (fertileScore * 0.14f)
                + (navigationZoneBias * 0.08f)
                - (resourceZoneBias * 0.16f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float serviceContinuity = math.saturate(
                (serviceScore * 0.46f)
                + (serviceMacroNoise * 0.22f)
                + (metallicScore * 0.12f)
                + (thermalScore * 0.08f));
            float hazardContinuity = math.saturate(
                (hazardScore * 0.48f)
                + (riftMacroNoise * 0.24f)
                + (voidScore * 0.12f));

            if (depthMeters <= 180f)
            {
                if (serviceZoneBias > 0.58f && serviceContinuity > 0.62f)
                    return ChooseFamily(volcanicGlassFamily, tectonicSpineFamily, chemosyntheticBrineFamily);

                if (hazardZoneBias > 0.6f && hazardContinuity > 0.62f)
                    return ChooseFamily(riftSpineFamily, graniteEscarpmentFamily, volcanicGlassFamily);

                if (resourceZoneBias > 0.42f && sedimentContinuity > 0.56f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                if (reefContinuity > 0.82f && crystalNoise < 0.76f)
                    return ChooseFamily(fossilReefFamily, littoralKarstFamily, sedimentDriftFamily);

                if (crystalNoise > 0.82f && reefContinuity > 0.7f && resourceZoneBias < 0.38f)
                    return ChooseFamily(crystalGrowthFamily, fossilReefFamily, littoralKarstFamily);

                if (sedimentScore > 0.62f || sedimentContinuity > 0.58f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                if (ruggedScore > 0.7f)
                    return ChooseFamily(graniteEscarpmentFamily, tectonicSpineFamily, volcanicGlassFamily);

                if (resourceZoneBias > 0.35f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                return shallow01 > 0.55f
                    ? ChooseFamily(littoralKarstFamily, sedimentDriftFamily, fossilReefFamily)
                    : ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, abyssalSiltFamily);
            }

            if (depthMeters <= 600f)
            {
                if (serviceContinuity > 0.72f)
                    return ChooseFamily(volcanicGlassFamily, chemosyntheticBrineFamily, tectonicSpineFamily);

                if (hazardContinuity > 0.72f)
                    return ChooseFamily(riftSpineFamily, tectonicSpineFamily, graniteEscarpmentFamily);

                if ((sedimentScore > 0.68f && resourceZoneBias > 0.4f) || sedimentContinuity > 0.6f)
                    return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, graniteEscarpmentFamily);

                if (fertileScore > 0.66f && reefContinuity > 0.7f && resourceZoneBias < 0.34f)
                    return ChooseFamily(crystalGrowthFamily, fossilReefFamily, sedimentDriftFamily);

                if (metallicScore > 0.72f)
                    return ChooseFamily(chemosyntheticBrineFamily, metallicHadalFamily, abyssalSiltFamily);

                return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, graniteEscarpmentFamily);
            }

            if (voidScore > 0.76f && ruggedScore > 0.62f)
                return ChooseFamily(riftVoidFamily, volcanicHadalFamily, riftSpineFamily);

            if (thermalScore > 0.74f)
                return ChooseFamily(volcanicHadalFamily, chemosyntheticBrineFamily, volcanicGlassFamily);

            if (metallicScore > 0.72f)
                return ChooseFamily(metallicHadalFamily, chemosyntheticBrineFamily, abyssalSiltFamily);

            if (ruggedScore > 0.66f)
                return ChooseFamily(riftSpineFamily, tectonicSpineFamily, graniteEscarpmentFamily);

            if (fertileScore > 0.6f && crystalNoise > 0.68f)
                return ChooseFamily(crystalGrowthFamily, chemosyntheticBrineFamily, abyssalSiltFamily);

            return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, riftVoidFamily);
        }

        private WorldZoneAnchor.ZoneKind ResolveFallbackZoneKind(Vector3 position, float depthMeters, float slopeDegrees, in CellSamplingContext cellContext)
        {
            float shallow01 = 1f - math.saturate(depthMeters / 220f);
            float deep01 = math.saturate((depthMeters - 180f) / 900f);
            float steep01 = math.saturate((slopeDegrees - 10f) / 38f);
            float fertileNoise = cellContext.FertileBiomeNoise;
            float thermalNoise = cellContext.ThermalBiomeNoise;
            float metallicNoise = cellContext.MetallicBiomeNoise;
            float voidNoise = cellContext.VoidBiomeNoise;

            float resourceScore = math.saturate((shallow01 * 0.4f) + (fertileNoise * 0.6f));
            float serviceScore = math.saturate((metallicNoise * 0.55f) + (thermalNoise * 0.45f));
            float hazardScore = math.saturate((deep01 * 0.4f) + (steep01 * 0.25f) + (voidNoise * 0.35f));

            if (serviceScore > 0.74f)
                return thermalNoise > 0.58f ? WorldZoneAnchor.ZoneKind.Power : WorldZoneAnchor.ZoneKind.Service;

            if (hazardScore > 0.72f)
                return deep01 > 0.6f ? WorldZoneAnchor.ZoneKind.Progression : WorldZoneAnchor.ZoneKind.Combat;

            if (resourceScore > 0.7f)
                return fertileNoise > 0.64f ? WorldZoneAnchor.ZoneKind.Resources : WorldZoneAnchor.ZoneKind.Fabrication;

            if (steep01 > 0.55f || deep01 > 0.38f)
                return WorldZoneAnchor.ZoneKind.Navigation;

            return WorldZoneAnchor.ZoneKind.Resources;
        }

        private WorldProceduralPattern ResolvePattern(
            Vector3 position,
            float depthMeters,
            float slopeDegrees,
            HectonBiomeFamilyProfile biomeFamily,
            int biomeFamilyDataIndex,
            WorldZoneAnchor zone,
            int zoneDataIndex,
            WorldZoneAnchor.ZoneKind resolvedZoneKind,
            in CellSamplingContext cellContext)
        {
            float shallow01 = 1f - math.saturate(depthMeters / 220f);
            float deep01 = math.saturate((depthMeters - 180f) / 900f);
            float steep01 = math.saturate((slopeDegrees - 10f) / 36f);
            float fertileBias = EvaluateFertileBiomeBias(zoneDataIndex, zone, resolvedZoneKind, biomeFamilyDataIndex, biomeFamily);
            float hazardBias = EvaluateHazardBias(zoneDataIndex, zone, resolvedZoneKind);
            float serviceBias = EvaluateServiceBias(zoneDataIndex, zone, resolvedZoneKind);
            float resourceBias = EvaluateResourceBias(zoneDataIndex, zone, resolvedZoneKind);
            float shelterBias = EvaluateShelterBias(zoneDataIndex, zone, resolvedZoneKind);
            float landmarkBias = EvaluateLandmarkBias(zoneDataIndex, zone, resolvedZoneKind);
            float coralNoise = cellContext.CoralPatternNoise;
            float sedimentTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Sediment | BiomeFamilyFlags.Drift | BiomeFamilyFlags.Silt | BiomeFamilyFlags.Granite);
            float brineTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Brine | BiomeFamilyFlags.Chemo | BiomeFamilyFlags.Saline);
            float volcanicTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Glass | BiomeFamilyFlags.Magma | BiomeFamilyFlags.Basalt);
            float industrialTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Metallic | BiomeFamilyFlags.Industrial | BiomeFamilyFlags.Service);
            float riftTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Void | BiomeFamilyFlags.Hadal);
            float softWaterTokenBias = ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Reef | BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Fossil | BiomeFamilyFlags.Crystal | BiomeFamilyFlags.Coral | BiomeFamilyFlags.Kelp | BiomeFamilyFlags.Growth);

            if (softWaterTokenBias > 0.5f &&
                fertileBias > 0.58f &&
                serviceBias < 0.78f &&
                hazardBias < 0.78f)
            {
                return resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation || landmarkBias > 0.72f || coralNoise > 0.68f
                    ? WorldProceduralPattern.ReefNavigation
                    : WorldProceduralPattern.FertileShallows;
            }

            if (landmarkBias > 0.82f && (steep01 > 0.42f || resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation || resolvedZoneKind == WorldZoneAnchor.ZoneKind.Progression))
                return WorldProceduralPattern.LandmarkCorridor;

            if (brineTokenBias > 0.55f && (serviceBias > 0.46f || hazardBias > 0.42f))
                return WorldProceduralPattern.BrineToxic;

            if (volcanicTokenBias > 0.55f && (steep01 > 0.34f || landmarkBias > 0.5f || hazardBias > 0.42f))
                return WorldProceduralPattern.VolcanicPressure;

            if (serviceBias > 0.82f)
                return WorldProceduralPattern.IndustrialService;

            if (hazardBias > 0.82f)
                return volcanicTokenBias > 0.46f ? WorldProceduralPattern.VolcanicPressure : WorldProceduralPattern.RiftHazard;

            if (sedimentTokenBias > 0.5f && (resourceBias > 0.58f || shelterBias > 0.58f))
                return WorldProceduralPattern.SedimentResources;

            if (depthMeters > 820f && fertileBias < 0.44f && shelterBias < 0.5f && serviceBias < 0.62f)
                return WorldProceduralPattern.AbyssSparse;

            if (fertileBias > 0.74f)
            {
                if (resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation || landmarkBias > 0.72f || coralNoise > 0.72f)
                    return WorldProceduralPattern.ReefNavigation;

                return WorldProceduralPattern.FertileShallows;
            }

            if (resourceBias > 0.68f || shelterBias > 0.64f)
                return WorldProceduralPattern.SedimentResources;

            if (brineTokenBias > 0.5f)
                return WorldProceduralPattern.BrineToxic;

            if (volcanicTokenBias > 0.5f)
                return WorldProceduralPattern.VolcanicPressure;

            if (industrialTokenBias > 0.5f)
                return WorldProceduralPattern.IndustrialService;

            if (riftTokenBias > 0.5f)
                return hazardBias > 0.58f ? WorldProceduralPattern.RiftHazard : WorldProceduralPattern.LandmarkCorridor;

            if (softWaterTokenBias > 0.5f)
                return resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation ? WorldProceduralPattern.ReefNavigation : WorldProceduralPattern.FertileShallows;

            if (deep01 > 0.7f)
                return WorldProceduralPattern.AbyssSparse;

            if (landmarkBias > 0.68f)
                return WorldProceduralPattern.LandmarkCorridor;

            return shallow01 > 0.45f
                ? WorldProceduralPattern.SedimentResources
                : WorldProceduralPattern.AbyssSparse;
        }

        // R97 FIX (Zero-GC): was `params HectonBiomeFamilyProfile[]` — every fallback biome-family
        // resolve allocated a managed array in the sample hot path. All 22 call sites pass exactly
        // three candidates; the Burst twin (ChooseFamilyIndex) already uses the allocation-free shape.
        private static HectonBiomeFamilyProfile ChooseFamily(
            HectonBiomeFamilyProfile option0,
            HectonBiomeFamilyProfile option1,
            HectonBiomeFamilyProfile option2)
        {
            if (option0 != null)
                return option0;
            if (option1 != null)
                return option1;
            return option2;
        }


        private float EvaluateZoneBias(
            int zoneDataIndex,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind? zoneKindHint,
            WorldZoneAnchor.ZoneKind primaryKind,
            WorldZoneAnchor.ZoneKind secondaryKind)
        {
            WorldZoneAnchor.ZoneKind effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, zone, zoneKindHint);
            return effectiveKind == primaryKind || effectiveKind == secondaryKind ? 1f : 0.26f;
        }

        private float EvaluateZoneBias(
            int zoneDataIndex,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind? zoneKindHint,
            WorldZoneAnchor.ZoneKind primaryKind,
            WorldZoneAnchor.ZoneKind secondaryKind,
            WorldZoneAnchor.ZoneKind tertiaryKind)
        {
            WorldZoneAnchor.ZoneKind effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, zone, zoneKindHint);
            return effectiveKind == primaryKind || effectiveKind == secondaryKind || effectiveKind == tertiaryKind ? 1f : 0.26f;
        }

        private float EvaluateZoneBias(
            int zoneDataIndex,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind? zoneKindHint,
            WorldZoneAnchor.ZoneKind primaryKind,
            WorldZoneAnchor.ZoneKind secondaryKind,
            WorldZoneAnchor.ZoneKind tertiaryKind,
            WorldZoneAnchor.ZoneKind quaternaryKind)
        {
            WorldZoneAnchor.ZoneKind effectiveKind = ResolveEffectiveZoneKind(zoneDataIndex, zone, zoneKindHint);
            return effectiveKind == primaryKind
                || effectiveKind == secondaryKind
                || effectiveKind == tertiaryKind
                || effectiveKind == quaternaryKind
                ? 1f
                : 0.26f;
        }

        private WorldZoneAnchor.ZoneKind ResolveEffectiveZoneKind(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            if (TryGetZoneData(zoneDataIndex, out ZoneData zoneData))
                return (WorldZoneAnchor.ZoneKind)zoneData.Kind;

            if (zone != null)
                return zone.Kind;

            return zoneKindHint ?? WorldZoneAnchor.ZoneKind.Generic;
        }

        private float ContainsFamilyFlags(int familyDataIndex, HectonBiomeFamilyProfile fallbackFamily, BiomeFamilyFlags flags)
        {
            if (TryGetBiomeFamilyData(familyDataIndex, out BiomeFamilyData familyData))
                return (familyData.Flags & flags) != 0 ? 1f : 0f;

            if (fallbackFamily == null)
                return 0f;

            return (TokenizeFamilyFlags(fallbackFamily) & flags) != 0 ? 1f : 0f;
        }

        private float EvaluateRuggedBiomeBias(int zoneDataIndex, WorldZoneAnchor zone)
        {
            if (TryGetZoneData(zoneDataIndex, out ZoneData zoneData))
            {
                float familyBias = ContainsFamilyFlags(zoneData.DominantFamilyDataIndex, zone != null ? zone.DominantBiomeFamily : null, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass);
                if (!TryGetBiomeMatrixData(zoneData.DominantMatrixDataIndex, out BiomeMatrixData biomeData))
                    return math.lerp(0.25f, 1f, math.saturate(familyBias));

                float rugged = math.saturate((biomeData.LandmarkStrength + biomeData.RoutePressure) / 10f);
                return math.saturate((rugged * 0.65f) + (familyBias * 0.35f));
            }

            if (zone == null)
                return 0.38f;

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float fallbackFamilyBias = ContainsFamilyFlags(-1, zone.DominantBiomeFamily, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass);
            if (biome == null)
                return math.lerp(0.25f, 1f, math.saturate(fallbackFamilyBias));

            float fallbackRugged = math.saturate((biome.landmarkStrength + biome.routePressure) / 10f);
            return math.saturate((fallbackRugged * 0.65f) + (fallbackFamilyBias * 0.35f));
        }

        private float EvaluateFertileBiomeBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint, int familyDataIndex, HectonBiomeFamilyProfile family)
        {
            float familyBias = ContainsFamilyFlags(familyDataIndex, family, BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Reef | BiomeFamilyFlags.Fossil | BiomeFamilyFlags.Crystal | BiomeFamilyFlags.Coral | BiomeFamilyFlags.Kelp | BiomeFamilyFlags.Growth);
            float zoneBias = EvaluateZoneBias(zoneDataIndex, zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneKind.Navigation);
            return math.saturate((familyBias * 0.72f) + (zoneBias * 0.28f));
        }

        private float EvaluateHazardBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power);
            if (!TryGetZoneData(zoneDataIndex, out ZoneData zoneData) || !TryGetBiomeMatrixData(zoneData.DominantMatrixDataIndex, out BiomeMatrixData biomeData))
            {
                if (zone == null)
                    return zoneBias;

                HectonBiomeMatrixProfile fallbackBiome = zone.DominantMatrixBiome;
                if (fallbackBiome == null)
                    return zoneBias;

                float fallbackBiomeBias = math.saturate(math.max(fallbackBiome.survivalPressure, fallbackBiome.routePressure) / 5f);
                return math.saturate((zoneBias * 0.55f) + (fallbackBiomeBias * 0.45f));
            }

            float biomeBias = math.saturate(math.max(biomeData.SurvivalPressure, biomeData.RoutePressure) / 5f);
            return math.saturate((zoneBias * 0.55f) + (biomeBias * 0.45f));
        }

        private float EvaluateServiceBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            return EvaluateZoneBias(
                zoneDataIndex,
                zone,
                zoneKindHint,
                WorldZoneAnchor.ZoneKind.Service,
                WorldZoneAnchor.ZoneKind.Power,
                WorldZoneAnchor.ZoneKind.Construction,
                WorldZoneAnchor.ZoneKind.Progression);
        }

        private float EvaluateResourceBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication);
            if (!TryGetZoneData(zoneDataIndex, out ZoneData zoneData) || !TryGetBiomeMatrixData(zoneData.DominantMatrixDataIndex, out BiomeMatrixData biomeData))
            {
                if (zone == null)
                    return zoneBias;

                HectonBiomeMatrixProfile fallbackBiome = zone.DominantMatrixBiome;
                if (fallbackBiome == null)
                    return zoneBias;

                float fallbackBiomeBias = math.saturate(math.max(fallbackBiome.commonResourceBias, fallbackBiome.uncommonResourceBias) / 5f);
                return math.saturate((zoneBias * 0.6f) + (fallbackBiomeBias * 0.4f));
            }

            float biomeBias = math.saturate(math.max(biomeData.CommonResourceBias, biomeData.UncommonResourceBias) / 5f);
            return math.saturate((zoneBias * 0.6f) + (biomeBias * 0.4f));
        }

        private float EvaluateShelterBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            return EvaluateZoneBias(
                zoneDataIndex,
                zone,
                zoneKindHint,
                WorldZoneAnchor.ZoneKind.Fabrication,
                WorldZoneAnchor.ZoneKind.Navigation,
                WorldZoneAnchor.ZoneKind.Resources,
                WorldZoneAnchor.ZoneKind.Service);
        }

        private float EvaluateLandmarkBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            float zoneBias = EvaluateZoneBias(zoneDataIndex, zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat);
            if (!TryGetZoneData(zoneDataIndex, out ZoneData zoneData) || !TryGetBiomeMatrixData(zoneData.DominantMatrixDataIndex, out BiomeMatrixData biomeData))
            {
                if (zone == null)
                    return zoneBias;

                HectonBiomeMatrixProfile fallbackBiome = zone.DominantMatrixBiome;
                if (fallbackBiome == null)
                    return zoneBias;

                float fallbackBiomeBias = math.saturate(math.max(fallbackBiome.landmarkStrength, fallbackBiome.rewardPull) / 5f);
                return math.saturate((zoneBias * 0.45f) + (fallbackBiomeBias * 0.55f));
            }

            float biomeBias = math.saturate(math.max(biomeData.LandmarkStrength, biomeData.RewardPull) / 5f);
            return math.saturate((zoneBias * 0.45f) + (biomeBias * 0.55f));
        }

        private WorldProceduralPattern ResolvePreviewPatternOverride(
            WorldProceduralPattern resolvedPattern,
            SeafloorSource source)
        {
            if (!forcePatternPreviewOverride)
                return resolvedPattern;

            if (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return resolvedPattern;

            return previewPatternOverride;
        }

        private bool TryApplyPreviewPatternContextOverride(
            SeafloorSource source,
            float depthMeters,
            float slopeDegrees,
            ref HectonBiomeFamilyProfile biomeFamily,
            ref WorldZoneAnchor.ZoneKind resolvedZoneKind,
            out WorldProceduralPattern resolvedPattern)
        {
            resolvedPattern = WorldProceduralPattern.SedimentResources;

            if (!forcePatternPreviewOverride)
                return false;

            if (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return false;

            resolvedPattern = previewPatternOverride;
            resolvedZoneKind = ResolvePreviewPatternZoneKind(previewPatternOverride);
            biomeFamily = ResolvePreviewPatternBiomeFamily(previewPatternOverride, depthMeters, slopeDegrees, biomeFamily);
            return true;
        }

        private HectonBiomeFamilyProfile ResolvePreviewPatternBiomeFamily(
            WorldProceduralPattern pattern,
            float depthMeters,
            float slopeDegrees,
            HectonBiomeFamilyProfile currentBiomeFamily)
        {
            HectonBiomeFamilyProfile fallback = currentBiomeFamily;
            if (fallback == null)
                fallback = sedimentDriftFamily;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => littoralKarstFamily != null
                    ? littoralKarstFamily
                    : crystalGrowthFamily != null ? crystalGrowthFamily : fallback,
                WorldProceduralPattern.ReefNavigation => fossilReefFamily != null
                    ? fossilReefFamily
                    : crystalGrowthFamily != null ? crystalGrowthFamily : fallback,
                WorldProceduralPattern.SedimentResources => depthMeters > 220f && graniteEscarpmentFamily != null
                    ? graniteEscarpmentFamily
                    : sedimentDriftFamily != null ? sedimentDriftFamily : fallback,
                WorldProceduralPattern.IndustrialService => tectonicSpineFamily != null
                    ? tectonicSpineFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.BrineToxic => chemosyntheticBrineFamily != null
                    ? chemosyntheticBrineFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.VolcanicPressure => depthMeters > 240f && volcanicHadalFamily != null
                    ? volcanicHadalFamily
                    : volcanicGlassFamily != null ? volcanicGlassFamily : fallback,
                WorldProceduralPattern.RiftHazard => depthMeters > 240f && riftVoidFamily != null
                    ? riftVoidFamily
                    : riftSpineFamily != null ? riftSpineFamily : fallback,
                WorldProceduralPattern.AbyssSparse => abyssalSiltFamily != null
                    ? abyssalSiltFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.LandmarkCorridor => slopeDegrees > 10f && graniteEscarpmentFamily != null
                    ? graniteEscarpmentFamily
                    : fossilReefFamily != null ? fossilReefFamily : fallback,
                _ => fallback
            };
        }

        private static WorldZoneAnchor.ZoneKind ResolvePreviewPatternZoneKind(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => WorldZoneAnchor.ZoneKind.Resources,
                WorldProceduralPattern.ReefNavigation => WorldZoneAnchor.ZoneKind.Navigation,
                WorldProceduralPattern.SedimentResources => WorldZoneAnchor.ZoneKind.Resources,
                WorldProceduralPattern.IndustrialService => WorldZoneAnchor.ZoneKind.Service,
                WorldProceduralPattern.BrineToxic => WorldZoneAnchor.ZoneKind.Combat,
                WorldProceduralPattern.VolcanicPressure => WorldZoneAnchor.ZoneKind.Progression,
                WorldProceduralPattern.RiftHazard => WorldZoneAnchor.ZoneKind.Combat,
                WorldProceduralPattern.AbyssSparse => WorldZoneAnchor.ZoneKind.Progression,
                WorldProceduralPattern.LandmarkCorridor => WorldZoneAnchor.ZoneKind.Navigation,
                _ => WorldZoneAnchor.ZoneKind.Generic
            };
        }

        private static string ResolvePreviewBiomeLabel(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return "None";

            if (!string.IsNullOrWhiteSpace(biomeFamily.familyLabel))
                return biomeFamily.familyLabel;

            if (!string.IsNullOrWhiteSpace(biomeFamily.familyId))
                return biomeFamily.familyId;

            return biomeFamily.name;
        }

        private HectonBiomeMatrixProfile ResolveEffectiveBiomeProfile(
            HectonBiomeMatrixProfile currentProfile,
            HectonBiomeFamilyProfile biomeFamily,
            SeafloorSource source,
            WorldProceduralPattern resolvedPattern)
        {
            if (currentProfile != null && (!forcePatternPreviewOverride || (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)))
                return currentProfile;

            if (forcePatternPreviewOverride && (!limitPatternOverrideToFallback || source == SeafloorSource.FallbackSynthetic))
            {
                HectonBiomeMatrixProfile previewProfile = ResolvePreviewPatternBiomeProfile(previewPatternOverride, biomeFamily);
                if (previewProfile != null)
                    return previewProfile;
            }

            HectonBiomeMatrixProfile representativeProfile = ResolveRepresentativeBiomeProfileForFamily(biomeFamily);
            return representativeProfile != null ? representativeProfile : currentProfile;
        }

        private HectonBiomeMatrixProfile ResolveVolumetricBiomeProfile(
            float sampleY,
            float depthMeters,
            HectonBiomeFamilyProfile biomeFamily,
            HectonBiomeMatrixProfile currentProfile,
            out bool overrideApplied)
        {
            overrideApplied = false;
            float layerDepthMeters = math.max(depthMeters, math.max(0f, -sampleY));
            VolumetricBiomeRole requiredRole = (VolumetricBiomeRole)ResolveRequiredVolumetricRole(layerDepthMeters);
            if (requiredRole == VolumetricBiomeRole.None &&
                currentProfile != null &&
                IsManagedDepthWithinBand(layerDepthMeters, currentProfile.minDepthMeters, currentProfile.maxDepthMeters))
            {
                return currentProfile;
            }

            if (biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null || biomeMatrixDirector.MatrixCatalog.Profiles == null)
                return currentProfile;

            HectonBiomeMatrixProfile[] profiles = biomeMatrixDirector.MatrixCatalog.Profiles;
            HectonBiomeMatrixProfile best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile candidate = profiles[i];
                if (candidate == null)
                    continue;

                bool depthMatch = IsManagedDepthWithinBand(layerDepthMeters, candidate.minDepthMeters, candidate.maxDepthMeters);
                if (requiredRole == VolumetricBiomeRole.None && !depthMatch)
                    continue;

                int score = IsSameBiomeFamily(candidate, biomeFamily) ? 1000 : 0;
                if (requiredRole != VolumetricBiomeRole.None)
                {
                    int roleScore = ResolveManagedVolumetricRoleScore(requiredRole, ResolveVolumetricBiomeRole(candidate));
                    if (roleScore <= 0)
                        continue;

                    score += roleScore;
                }

                if (depthMatch)
                    score += 300;

                if (!candidate.isPlaceholder)
                    score += 100;

                score += candidate.rewardPull + candidate.landmarkStrength + candidate.survivalPressure;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            if (best != null && best != currentProfile)
            {
                overrideApplied = true;
                return best;
            }

            return currentProfile;
        }

        private static int ResolveVolumetricBiomeRole(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return (int)VolumetricBiomeRole.None;

            if (IsBiomeFamilyId(profile, "biome.family.abyssal_silt"))
                return (int)VolumetricBiomeRole.AbyssalSilt;
            if (IsBiomeFamilyId(profile, "biome.family.volcanic_hadal"))
                return (int)VolumetricBiomeRole.VolcanicHadal;
            if (IsBiomeFamilyId(profile, "biome.family.metallic_hadal"))
                return (int)VolumetricBiomeRole.MetallicHadal;
            if (IsBiomeFamilyId(profile, "biome.family.sediment_drift"))
                return (int)VolumetricBiomeRole.SedimentDrift;
            if (IsBiomeFamilyId(profile, "biome.family.crystal_growth"))
                return (int)VolumetricBiomeRole.CrystalGrowth;

            return (int)VolumetricBiomeRole.None;
        }

        private static int ResolveManagedVolumetricRoleScore(VolumetricBiomeRole targetRole, int candidateRole)
        {
            return ResolveVolumetricRoleScore((int)targetRole, candidateRole);
        }

        private static bool IsBiomeFamilyId(HectonBiomeMatrixProfile profile, string familyId)
        {
            if (profile == null || string.IsNullOrEmpty(familyId))
                return false;

            if (!string.IsNullOrEmpty(profile.familyId) &&
                string.Equals(profile.familyId, familyId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            HectonBiomeFamilyProfile family = profile.familyProfile;
            return family != null &&
                   !string.IsNullOrEmpty(family.familyId) &&
                   string.Equals(family.familyId, familyId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManagedDepthWithinBand(float depthMeters, float minDepthMeters, float maxDepthMeters)
        {
            float minDepth = math.min(minDepthMeters, maxDepthMeters);
            float maxDepth = math.max(minDepthMeters, maxDepthMeters);
            if (maxDepth <= 0f && minDepth <= 0f)
                return true;

            return depthMeters >= minDepth && depthMeters <= maxDepth;
        }

        private static bool IsSameBiomeFamily(HectonBiomeMatrixProfile profile, HectonBiomeFamilyProfile family)
        {
            if (profile == null || family == null)
                return false;

            if (profile.familyProfile == family)
                return true;

            return !string.IsNullOrEmpty(profile.familyId) &&
                   !string.IsNullOrEmpty(family.familyId) &&
                   string.Equals(profile.familyId, family.familyId, System.StringComparison.Ordinal);
        }

        private HectonBiomeMatrixProfile ResolvePreviewMatrixBiomeOverride(SeafloorSource source)
        {
            if (!forceMatrixBiomePreviewOverride || previewMatrixBiomeOverride == null)
                return null;

            if (limitMatrixBiomeOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return null;

            return previewMatrixBiomeOverride;
        }

        private HectonBiomeMatrixProfile ResolvePreviewPatternBiomeProfile(
            WorldProceduralPattern pattern,
            HectonBiomeFamilyProfile biomeFamily)
        {
            HectonBiomeFamilyProfile targetFamily = ResolvePreviewPatternBiomeFamily(pattern, 0f, 0f, biomeFamily);
            return ResolveRepresentativeBiomeProfileForFamily(targetFamily);
        }

        private HectonBiomeMatrixProfile ResolveRepresentativeBiomeProfileForFamily(HectonBiomeFamilyProfile targetFamily)
        {
            if (targetFamily == null || biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null || biomeMatrixDirector.MatrixCatalog.Profiles == null)
                return null;

            HectonBiomeMatrixProfile best = null;
            int bestScore = int.MinValue;
            HectonBiomeMatrixProfile fallback = null;
            HectonBiomeMatrixProfile[] profiles = biomeMatrixDirector.MatrixCatalog.Profiles;
            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                if (profile.familyProfile != targetFamily && !string.Equals(profile.familyId, targetFamily.familyId, System.StringComparison.Ordinal))
                    continue;

                int score = (profile.rewardPull * 3) + (profile.landmarkStrength * 2) + profile.commonResourceBias + profile.uncommonResourceBias + profile.rareResourceBias;
                if (!profile.isPlaceholder && score > bestScore)
                {
                    best = profile;
                    bestScore = score;
                }

                fallback ??= profile;
            }

            return best != null ? best : fallback;
        }

        private float EvaluateBiomeMatrixChannelBonus(string channel, int biomeMatrixDataIndex, HectonBiomeMatrixProfile biomeProfile)
        {
            int loosePickupBias = biomeProfile != null ? biomeProfile.loosePickupBias : 0;
            int nodeExtractionBias = biomeProfile != null ? biomeProfile.nodeExtractionBias : 0;
            int salvageBias = biomeProfile != null ? biomeProfile.salvageBias : 0;
            int commonResourceBias = biomeProfile != null ? biomeProfile.commonResourceBias : 0;
            int uncommonResourceBias = biomeProfile != null ? biomeProfile.uncommonResourceBias : 0;
            int rareResourceBias = biomeProfile != null ? biomeProfile.rareResourceBias : 0;
            int routePressure = biomeProfile != null ? biomeProfile.routePressure : 0;
            int landmarkStrength = biomeProfile != null ? biomeProfile.landmarkStrength : 0;
            int rewardPull = biomeProfile != null ? biomeProfile.rewardPull : 0;
            int survivalPressure = biomeProfile != null ? biomeProfile.survivalPressure : 0;

            if (TryGetBiomeMatrixData(biomeMatrixDataIndex, out BiomeMatrixData biomeData))
            {
                loosePickupBias = biomeData.LoosePickupBias;
                nodeExtractionBias = biomeData.NodeExtractionBias;
                salvageBias = biomeData.SalvageBias;
                commonResourceBias = biomeData.CommonResourceBias;
                uncommonResourceBias = biomeData.UncommonResourceBias;
                rareResourceBias = biomeData.RareResourceBias;
                routePressure = biomeData.RoutePressure;
                landmarkStrength = biomeData.LandmarkStrength;
                rewardPull = biomeData.RewardPull;
                survivalPressure = biomeData.SurvivalPressure;
            }

            if (loosePickupBias <= 0 &&
                nodeExtractionBias <= 0 &&
                salvageBias <= 0 &&
                commonResourceBias <= 0 &&
                uncommonResourceBias <= 0 &&
                rareResourceBias <= 0 &&
                routePressure <= 0 &&
                landmarkStrength <= 0 &&
                rewardPull <= 0 &&
                survivalPressure <= 0)
            {
                return 0f;
            }

            float loosePickup = NormalizeMatrixBias(loosePickupBias);
            float node = NormalizeMatrixBias(nodeExtractionBias);
            float salvage = NormalizeMatrixBias(salvageBias);
            float common = NormalizeMatrixBias(commonResourceBias);
            float uncommon = NormalizeMatrixBias(uncommonResourceBias);
            float rare = NormalizeMatrixBias(rareResourceBias);
            float route = NormalizeMatrixBias(routePressure);
            float landmark = NormalizeMatrixBias(landmarkStrength);
            float reward = NormalizeMatrixBias(rewardPull);
            float survival = NormalizeMatrixBias(survivalPressure);
            float resource = math.saturate((common * 0.45f) + (uncommon * 0.35f) + (rare * 0.2f));
            float salvageRead = math.saturate((salvage * 0.62f) + (node * 0.38f));
            float landmarkRead = math.saturate((landmark * 0.64f) + (route * 0.36f));
            float hazardRead = math.saturate((survival * 0.58f) + (route * 0.26f) + (rare * 0.16f));
            float shelterRead = math.saturate((survival * 0.68f) + (loosePickup * 0.16f) + ((1f - hazardRead) * 0.16f));
            float faunaRead = math.saturate((common * 0.34f) + (reward * 0.18f) + ((1f - survival) * 0.48f));

            return channel switch
            {
                "rock_density" => landmarkRead * 0.08f + node * 0.04f,
                "kelp_density" => faunaRead * 0.05f + shelterRead * 0.03f,
                "flora_density" => faunaRead * 0.06f + reward * 0.04f,
                "coral_density" => faunaRead * 0.07f + landmarkRead * 0.03f,
                "bio_density" => faunaRead * 0.11f + reward * 0.04f,
                "debris_density" => salvageRead * 0.12f,
                "ruin_density" => salvageRead * 0.10f + landmarkRead * 0.04f,
                "cave_density" => landmarkRead * 0.10f + hazardRead * 0.04f,
                "landmark_strength" => landmarkRead * 0.13f + reward * 0.04f,
                "fauna_density" => faunaRead * 0.12f - hazardRead * 0.03f,
                "hazard_density" => hazardRead * 0.11f,
                "resource_density" => resource * 0.12f + reward * 0.05f,
                "shelter_density" => shelterRead * 0.12f,
                "service_density" => salvageRead * 0.1f + node * 0.05f,
                _ => 0f
            };
        }

        private static float NormalizeMatrixBias(int value)
        {
            return math.saturate(value / 5f);
        }

        private void RegisterMatrixForBake(HectonBiomeMatrixProfile profile)
        {
            if (profile == null || ResolveBiomeMatrixDataIndex(profile) >= 0 || _biomeMatrixBakeCount >= _biomeMatrixBakeList.Length)
                return;

            int index = _biomeMatrixBakeCount;
            _biomeMatrixBakeList[index] = profile;
            _biomeMatrixBakeCount++;
            RegisterFamilyForBake(profile.familyProfile);
        }

        private void RegisterFamilyForBake(HectonBiomeFamilyProfile family)
        {
            if (family == null || ResolveBiomeFamilyDataIndex(family) >= 0 || _biomeFamilyBakeCount >= _biomeFamilyBakeList.Length)
                return;

            int index = _biomeFamilyBakeCount;
            _biomeFamilyBakeList[index] = family;
            _biomeFamilyBakeCount++;
        }

        private bool TryEnsureVaultBufferCapacity<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCapacity,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            int safeCapacity = requiredCapacity > 0
                ? ResolvePowerOfTwoCapacity(requiredCapacity)
                : EmptyNativeArrayCapacity;

            if (!TryCacheDataVaultCold())
                return false;

            if (handle.BufferID == unchecked((uint)(int)bufferId) &&
                handle.SystemID == unchecked((uint)OwnerSystemId) &&
                handle.Generation != 0u &&
                _dataVault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= safeCapacity)
            {
                return true;
            }

            handle = _dataVault.EnsureGenerationHandle<T>(
                bufferId,
                safeCapacity,
                OwnerSystemId,
                options);

            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == unchecked((uint)OwnerSystemId) &&
                   handle.Generation != 0u &&
                   _dataVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= safeCapacity;
        }

        private bool TryCacheDataVaultCold()
        {
            if (_dataVault != null)
                return true;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault != null;
        }

        private bool TryResolveVaultBuffer<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            return TryResolveVaultBuffer(_dataVault, bufferId, in handle, out buffer);
        }

        private static bool TryResolveVaultBuffer<T>(IDataVault vault, BufferID bufferId, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsWorldProceduralFieldHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private bool TryReadVaultBuffer<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   IsWorldProceduralFieldHandle(in handle, bufferId) &&
                   _dataVault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool IsWorldProceduralFieldHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == unchecked((uint)OwnerSystemId) &&
                   handle.Generation != 0u;
        }

        private static ulong WorldProceduralFieldMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryResolveSamplingData(
            out NativeArray<ZoneData> zones,
            out NativeArray<BiomeMatrixData> biomeMatrices,
            out NativeArray<int> biomeMatrixIdToDataIndex,
            out NativeArray<BiomeFamilyData> biomeFamilies,
            out NativeArray<CaveEntranceHintData> caveEntranceHints,
            out NativeArray<ushort> noiseLookupTable)
        {
            zones = default;
            biomeMatrices = default;
            biomeMatrixIdToDataIndex = default;
            biomeFamilies = default;
            caveEntranceHints = default;
            noiseLookupTable = default;

            return TryResolveSamplingData(_dataVault, out zones, out biomeMatrices, out biomeMatrixIdToDataIndex, out biomeFamilies, out caveEntranceHints, out noiseLookupTable);
        }

        private bool TryResolveSamplingData(
            IDataVault vault,
            out NativeArray<ZoneData> zones,
            out NativeArray<BiomeMatrixData> biomeMatrices,
            out NativeArray<int> biomeMatrixIdToDataIndex,
            out NativeArray<BiomeFamilyData> biomeFamilies,
            out NativeArray<CaveEntranceHintData> caveEntranceHints,
            out NativeArray<ushort> noiseLookupTable)
        {
            zones = default;
            biomeMatrices = default;
            biomeMatrixIdToDataIndex = default;
            biomeFamilies = default;
            caveEntranceHints = default;
            noiseLookupTable = default;

            return TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldZones, in _burstZoneDataHandle, out zones) &&
                   TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldBiomeMatrices, in _burstBiomeMatrixDataHandle, out biomeMatrices) &&
                   TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldBiomeMatrixIndex, in _burstBiomeMatrixIdToDataIndexHandle, out biomeMatrixIdToDataIndex) &&
                   TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldBiomeFamilies, in _burstBiomeFamilyDataHandle, out biomeFamilies) &&
                   TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldCaveEntranceHints, in _burstCaveEntranceHintsHandle, out caveEntranceHints) &&
                   TryResolveVaultBuffer(vault, BufferID.WorldProceduralFieldNoiseLookup, in _noiseLookupTableHandle, out noiseLookupTable);
        }

        private bool TryPinSamplingJobBuffers(
            out NativeArray<ZoneData> zones,
            out NativeArray<BiomeMatrixData> biomeMatrices,
            out NativeArray<int> biomeMatrixIdToDataIndex,
            out NativeArray<BiomeFamilyData> biomeFamilies,
            out NativeArray<CaveEntranceHintData> caveEntranceHints,
            out NativeArray<ushort> noiseLookupTable)
        {
            zones = default;
            biomeMatrices = default;
            biomeMatrixIdToDataIndex = default;
            biomeFamilies = default;
            caveEntranceHints = default;
            noiseLookupTable = default;

            IDataVault vault = _samplingJobPinVault;
            if (_samplingJobBuffersPinned)
            {
                if (vault != null &&
                    TryResolveSamplingData(vault, out zones, out biomeMatrices, out biomeMatrixIdToDataIndex, out biomeFamilies, out caveEntranceHints, out noiseLookupTable))
                {
                    return true;
                }

                if (!_hasPendingSamplingJob)
                    ReleaseSamplingJobBufferPins();
                zones = default;
                biomeMatrices = default;
                biomeMatrixIdToDataIndex = default;
                biomeFamilies = default;
                caveEntranceHints = default;
                noiseLookupTable = default;
                return false;
            }

            vault = _dataVault;
            if (vault == null ||
                !TryResolveSamplingData(vault, out zones, out biomeMatrices, out biomeMatrixIdToDataIndex, out biomeFamilies, out caveEntranceHints, out noiseLookupTable))
            {
                zones = default;
                biomeMatrices = default;
                biomeMatrixIdToDataIndex = default;
                biomeFamilies = default;
                caveEntranceHints = default;
                noiseLookupTable = default;
                return false;
            }

            bool acquired = false;
            try
            {
                _samplingJobPinVault = vault;
                acquired = true;
                if (!TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldZones, SamplingJobPinZones) ||
                    !TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldBiomeMatrices, SamplingJobPinBiomeMatrices) ||
                    !TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldBiomeMatrixIndex, SamplingJobPinBiomeMatrixIndex) ||
                    !TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldBiomeFamilies, SamplingJobPinBiomeFamilies) ||
                    !TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldCaveEntranceHints, SamplingJobPinCaveEntranceHints) ||
                    !TryLockSamplingJobBuffer(vault, BufferID.WorldProceduralFieldNoiseLookup, SamplingJobPinNoiseLookup))
                {
                    zones = default;
                    biomeMatrices = default;
                    biomeMatrixIdToDataIndex = default;
                    biomeFamilies = default;
                    caveEntranceHints = default;
                    noiseLookupTable = default;
                    return false;
                }

                if (!TryResolveSamplingData(
                        vault,
                        out zones,
                        out biomeMatrices,
                        out biomeMatrixIdToDataIndex,
                        out biomeFamilies,
                        out caveEntranceHints,
                        out noiseLookupTable))
                {
                    return false;
                }

                _samplingJobBuffersPinned = true;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseSamplingJobBufferPins();
                    zones = default;
                    biomeMatrices = default;
                    biomeMatrixIdToDataIndex = default;
                    biomeFamilies = default;
                    caveEntranceHints = default;
                    noiseLookupTable = default;
                }
            }
        }

        private void ReleaseSamplingJobBufferPins()
        {
            IDataVault vault = _samplingJobPinVault;
            uint pinMask = _samplingJobPinMask;
            _samplingJobPinVault = null;
            _samplingJobPinMask = 0u;
            _samplingJobBuffersPinned = false;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinNoiseLookup, BufferID.WorldProceduralFieldNoiseLookup);
            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinCaveEntranceHints, BufferID.WorldProceduralFieldCaveEntranceHints);
            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinBiomeFamilies, BufferID.WorldProceduralFieldBiomeFamilies);
            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinBiomeMatrixIndex, BufferID.WorldProceduralFieldBiomeMatrixIndex);
            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinBiomeMatrices, BufferID.WorldProceduralFieldBiomeMatrices);
            TryUnlockSamplingJobBuffer(vault, pinMask, SamplingJobPinZones, BufferID.WorldProceduralFieldZones);
        }

        private bool TryLockSamplingJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_samplingJobPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_samplingJobPinVault != null && !ReferenceEquals(_samplingJobPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, OwnerSystemId))
            {
                return false;
            }

            _samplingJobPinVault = vault;
            _samplingJobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockSamplingJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystemId);
        }

        private static int ResolvePowerOfTwoCapacity(int requiredCapacity)
        {
            int capacity = 1;
            while (capacity < requiredCapacity && capacity < 1073741824)
                capacity <<= 1;

            return capacity < requiredCapacity ? requiredCapacity : capacity;
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            RegisterTrackedNativeArray(array, label, NativeMemoryLifetime);
        }

        private static void RegisterTrackedNativeArray<T>(
            NativeArray<T> array,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            if (!array.IsCreated)
                return;

            int sentinelId = NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                lifetime);
            if (sentinelId <= 0)
                throw new System.InvalidOperationException($"NativeMemorySentinel rejected field sampler native array registration for {label}.");
        }

        private static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private void DisposeBurstData()
        {
            CompletePendingSamplingJobForBarrier();

            ReleaseVaultHandle(ref _burstZoneDataHandle);
            ReleaseVaultHandle(ref _burstBiomeMatrixDataHandle);
            ReleaseVaultHandle(ref _burstBiomeMatrixIdToDataIndexHandle);
            ReleaseVaultHandle(ref _burstBiomeFamilyDataHandle);
            ReleaseVaultHandle(ref _burstCaveEntranceHintsHandle);
            ReleaseVaultHandle(ref _noiseLookupTableHandle);

            _burstZoneDataCount = 0;
            _burstBiomeMatrixDataCount = 0;
            _burstBiomeFamilyDataCount = 0;
            _burstCaveEntranceHintCount = 0;
        }

        private void ReleaseVaultHandle<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u &&
                handle.SystemID == unchecked((uint)OwnerSystemId) &&
                _dataVault != null)
            {
                _dataVault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void CompletePendingSamplingJobForBarrier()
        {
            if (!_hasPendingSamplingJob)
            {
                ReleaseSamplingJobBufferPins();
                return;
            }

            DispatcherJobSwap.TryComplete(ref _lastSamplingJobHandle, true);
            _hasPendingSamplingJob = false;
            ReleaseSamplingJobBufferPins();
        }

        private bool TryGetZoneData(int zoneDataIndex, out ZoneData zoneData)
        {
            if (TryReadVaultBuffer(BufferID.WorldProceduralFieldZones, in _burstZoneDataHandle, out NativeArray<ZoneData>.ReadOnly zoneDataBuffer) &&
                zoneDataIndex >= 0 &&
                zoneDataIndex < _burstZoneDataCount)
            {
                zoneData = zoneDataBuffer[zoneDataIndex];
                return true;
            }

            zoneData = default;
            return false;
        }

        private bool TryGetBiomeMatrixData(int biomeMatrixDataIndex, out BiomeMatrixData biomeData)
        {
            if (TryReadVaultBuffer(BufferID.WorldProceduralFieldBiomeMatrices, in _burstBiomeMatrixDataHandle, out NativeArray<BiomeMatrixData>.ReadOnly biomeMatrixData) &&
                biomeMatrixDataIndex >= 0 &&
                biomeMatrixDataIndex < _burstBiomeMatrixDataCount)
            {
                biomeData = biomeMatrixData[biomeMatrixDataIndex];
                return true;
            }

            biomeData = default;
            return false;
        }

        private bool TryGetBiomeFamilyData(int biomeFamilyDataIndex, out BiomeFamilyData familyData)
        {
            if (TryReadVaultBuffer(BufferID.WorldProceduralFieldBiomeFamilies, in _burstBiomeFamilyDataHandle, out NativeArray<BiomeFamilyData>.ReadOnly biomeFamilyData) &&
                biomeFamilyDataIndex >= 0 &&
                biomeFamilyDataIndex < _burstBiomeFamilyDataCount)
            {
                familyData = biomeFamilyData[biomeFamilyDataIndex];
                return true;
            }

            familyData = default;
            return false;
        }

        private int ResolveZoneDataIndex(WorldZoneAnchor zone)
        {
            if (zone == null)
                return -1;

            for (int i = 0; i < _zoneBakeCount; i++)
            {
                if (ReferenceEquals(_zoneBakeList[i], zone))
                    return i;
            }

            return -1;
        }

        private int ResolveBiomeMatrixDataIndex(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return -1;

            for (int i = 0; i < _biomeMatrixBakeCount; i++)
            {
                if (ReferenceEquals(_biomeMatrixBakeList[i], biomeProfile))
                    return i;
            }

            return -1;
        }

        private int ResolveBiomeFamilyDataIndex(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return -1;

            for (int i = 0; i < _biomeFamilyBakeCount; i++)
            {
                if (ReferenceEquals(_biomeFamilyBakeList[i], biomeFamily))
                    return i;
            }

            return -1;
        }

        private static BiomeFamilyFlags TokenizeFamilyFlags(HectonBiomeFamilyProfile family)
        {
            if (family == null)
                return BiomeFamilyFlags.None;

            BiomeFamilyFlags flags = BiomeFamilyFlags.None;
            AppendFamilyFlags(ref flags, family.familyId);
            AppendFamilyFlags(ref flags, family.familyLabel);
            return flags;
        }

        private static void AppendFamilyFlags(ref BiomeFamilyFlags flags, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            AppendFamilyFlagIfPresent(ref flags, text, "sediment", BiomeFamilyFlags.Sediment);
            AppendFamilyFlagIfPresent(ref flags, text, "drift", BiomeFamilyFlags.Drift);
            AppendFamilyFlagIfPresent(ref flags, text, "silt", BiomeFamilyFlags.Silt);
            AppendFamilyFlagIfPresent(ref flags, text, "granite", BiomeFamilyFlags.Granite);
            AppendFamilyFlagIfPresent(ref flags, text, "brine", BiomeFamilyFlags.Brine);
            AppendFamilyFlagIfPresent(ref flags, text, "chemo", BiomeFamilyFlags.Chemo);
            AppendFamilyFlagIfPresent(ref flags, text, "saline", BiomeFamilyFlags.Saline);
            AppendFamilyFlagIfPresent(ref flags, text, "volcanic", BiomeFamilyFlags.Volcanic);
            AppendFamilyFlagIfPresent(ref flags, text, "tectonic", BiomeFamilyFlags.Tectonic);
            AppendFamilyFlagIfPresent(ref flags, text, "glass", BiomeFamilyFlags.Glass);
            AppendFamilyFlagIfPresent(ref flags, text, "magma", BiomeFamilyFlags.Magma);
            AppendFamilyFlagIfPresent(ref flags, text, "basalt", BiomeFamilyFlags.Basalt);
            AppendFamilyFlagIfPresent(ref flags, text, "metallic", BiomeFamilyFlags.Metallic);
            AppendFamilyFlagIfPresent(ref flags, text, "industrial", BiomeFamilyFlags.Industrial);
            AppendFamilyFlagIfPresent(ref flags, text, "service", BiomeFamilyFlags.Service);
            AppendFamilyFlagIfPresent(ref flags, text, "rift", BiomeFamilyFlags.Rift);
            AppendFamilyFlagIfPresent(ref flags, text, "void", BiomeFamilyFlags.Void);
            AppendFamilyFlagIfPresent(ref flags, text, "hadal", BiomeFamilyFlags.Hadal);
            AppendFamilyFlagIfPresent(ref flags, text, "reef", BiomeFamilyFlags.Reef);
            AppendFamilyFlagIfPresent(ref flags, text, "littoral", BiomeFamilyFlags.Littoral);
            AppendFamilyFlagIfPresent(ref flags, text, "crystal", BiomeFamilyFlags.Crystal);
            AppendFamilyFlagIfPresent(ref flags, text, "fossil", BiomeFamilyFlags.Fossil);
            AppendFamilyFlagIfPresent(ref flags, text, "coral", BiomeFamilyFlags.Coral);
            AppendFamilyFlagIfPresent(ref flags, text, "kelp", BiomeFamilyFlags.Kelp);
            AppendFamilyFlagIfPresent(ref flags, text, "growth", BiomeFamilyFlags.Growth);
        }

        private static void AppendFamilyFlagIfPresent(ref BiomeFamilyFlags flags, string text, string token, BiomeFamilyFlags flag)
        {
            if (text.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                flags |= flag;
        }

        private bool NeedsAutoResolve()
        {
            return playerTransform == null ||
                   !HasTerrainHeightProvider() ||
                   worldZoneDirector == null ||
                   biomeMatrixDirector == null;
        }

        private bool HasTerrainHeightProvider()
        {
            if (mapMagicBridge != null && mapMagicBridge.isActiveAndEnabled)
                return true;

            return IsTerrainProviderAvailable(_terrainProviderRuntime) ||
                   IsTerrainProviderAvailable(GlobalRegistry.Terrain);
        }

        private static Vector2Int GetHeightCacheKey(float x, float z)
        {
            return new Vector2Int(
                (int)math.round(x * 100f),
                (int)math.round(z * 100f));
        }

        private static int GetSeafloorHeightCacheIndex(Vector2Int key)
        {
            unchecked
            {
                uint hash = ((uint)key.x * 73856093u) ^ ((uint)key.y * 19349663u);
                hash ^= hash >> 16;
                return (int)(hash & MaxSeafloorHeightCacheMask);
            }
        }

        private bool TryReadSeafloorHeightCache(Vector2Int key, out CachedHeightSample sample)
        {
            int startIndex = GetSeafloorHeightCacheIndex(key);
            for (int probe = 0; probe < MaxSeafloorHeightCacheEntries; probe++)
            {
                int index = (startIndex + probe) & MaxSeafloorHeightCacheMask;
                if (_seafloorHeightCacheOccupied[index] == 0)
                {
                    sample = default;
                    return false;
                }

                if (_seafloorHeightCacheKeys[index] == key)
                {
                    sample = _seafloorHeightCacheValues[index];
                    return true;
                }
            }

            sample = default;
            return false;
        }

        private void WriteSeafloorHeightCache(Vector2Int key, float height, SeafloorSource source)
        {
            CachedHeightSample sample;
            sample.Height = height;
            sample.Source = source;
            sample.SamplingFrameId = _samplingFrameId;

            if (_seafloorHeightCacheCount >= MaxSeafloorHeightCacheEntries)
            {
                ClearSeafloorHeightCache();
            }

            int startIndex = GetSeafloorHeightCacheIndex(key);
            for (int probe = 0; probe < MaxSeafloorHeightCacheEntries; probe++)
            {
                int index = (startIndex + probe) & MaxSeafloorHeightCacheMask;
                if (_seafloorHeightCacheOccupied[index] == 0)
                {
                    _seafloorHeightCacheKeys[index] = key;
                    _seafloorHeightCacheValues[index] = sample;
                    _seafloorHeightCacheOccupied[index] = 1;
                    _seafloorHeightCacheCount++;
                    return;
                }

                if (_seafloorHeightCacheKeys[index] == key)
                {
                    _seafloorHeightCacheValues[index] = sample;
                    return;
                }
            }

            ClearSeafloorHeightCache();
            int fallbackIndex = startIndex & MaxSeafloorHeightCacheMask;
            _seafloorHeightCacheKeys[fallbackIndex] = key;
            _seafloorHeightCacheValues[fallbackIndex] = sample;
            _seafloorHeightCacheOccupied[fallbackIndex] = 1;
            _seafloorHeightCacheCount = 1;
        }

        private void ClearSeafloorHeightCache()
        {
            if (_seafloorHeightCacheCount <= 0)
                return;

            System.Array.Clear(_seafloorHeightCacheOccupied, 0, _seafloorHeightCacheOccupied.Length);
            _seafloorHeightCacheCount = 0;
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
                    RebindPlayerContext(previousService as IPlayerRuntimeContext, currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    if (ReferenceEquals(mapMagicBridge, previousService))
                        mapMagicBridge = null;
                    if (currentService is MapMagicBridge currentMapMagicBridge)
                        mapMagicBridge = currentMapMagicBridge;
                    else
                        WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
                    if (ReferenceEquals(_terrainProviderRuntime, previousService) || _terrainProviderRuntime == null)
                        _terrainProviderRuntime = mapMagicBridge;
                    ClearSeafloorHeightCache();
                    _isDataDirty = true;
                    break;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_terrainProviderRuntime, previousService))
                        _terrainProviderRuntime = null;
                    _terrainProviderRuntime = currentService as ITerrainProvider;
                    if (currentService is MapMagicBridge terrainMapMagicBridge)
                        mapMagicBridge = terrainMapMagicBridge;
                    else if (mapMagicBridge == null)
                        WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
                    ClearSeafloorHeightCache();
                    _isDataDirty = true;
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    biomeMatrixDirector = currentService as BiomeMatrixDirector;
                    ClearSeafloorHeightCache();
                    _isDataDirty = true;
                    break;
                case GlobalRegistryServiceSlot.WorldSeedProvider:
                    ClearSeafloorHeightCache();
                    _isDataDirty = true;
                    break;
            }

            RefreshCachedDependencyDiagnostics();
        }

        private void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            CompletePendingSamplingJobForBarrier();
            DisposeBurstData();
            ReleaseBiomeInfluenceGraphicsBuffer();
            _dataVault = currentVault;
            _isDataDirty = true;
            _samplingFramePrepared = false;
            ClearSeafloorHeightCache();
        }

        private void RegisterRuntimeDependencyListeners()
        {
            if (!_hotSwapRegistered && Application.isPlaying)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (!_worldZoneListenerRegistered)
            {
                WorldZoneDirector.ActiveRuntimeInstanceChanged += HandleWorldZoneDirectorChanged;
                _worldZoneListenerRegistered = true;
            }

            if (!_worldCaveListenerRegistered)
            {
                WorldCaveDirector.ActiveRuntimeInstanceChanged += HandleWorldCaveDirectorChanged;
                _worldCaveListenerRegistered = true;
            }
        }

        private void UnregisterRuntimeDependencyListeners()
        {
            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            if (_worldZoneListenerRegistered)
            {
                WorldZoneDirector.ActiveRuntimeInstanceChanged -= HandleWorldZoneDirectorChanged;
                _worldZoneListenerRegistered = false;
            }

            if (_worldCaveListenerRegistered)
            {
                WorldCaveDirector.ActiveRuntimeInstanceChanged -= HandleWorldCaveDirectorChanged;
                _worldCaveListenerRegistered = false;
            }
        }

        private void HandleWorldZoneDirectorChanged(WorldZoneDirector director)
        {
            worldZoneDirector = director;
            ClearSeafloorHeightCache();
            _isDataDirty = true;
            RefreshCachedDependencyDiagnostics();
        }

        private void HandleWorldCaveDirectorChanged(WorldCaveDirector director)
        {
            _worldCaveDirector = director;
            _lastCaveEntranceHintVersion = -1;
            _isDataDirty = true;
            RefreshCachedDependencyDiagnostics();
        }

        private void RebindPlayerContext(IPlayerRuntimeContext previousContext, IPlayerRuntimeContext currentContext)
        {
            if (previousContext != null && ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                playerTransform = null;

            if (currentContext != null && playerTransform == null)
                playerTransform = currentContext.PlayerTransform;

            ClearSeafloorHeightCache();
            _isDataDirty = true;
        }

        private void RefreshColdReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
            {
                RefreshCachedDependencyDiagnostics();
                return;
            }

            float now = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + math.max(0f, autoResolveRetryInterval);

            if (playerTransform == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
                if (playerTransform == null)
                    WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            }

            if (mapMagicBridge == null || !mapMagicBridge.isActiveAndEnabled)
            {
                mapMagicBridge = null;
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            }

            if (!IsTerrainProviderAvailable(_terrainProviderRuntime))
            {
                _terrainProviderRuntime = GlobalRegistry.Terrain;
                if (!IsTerrainProviderAvailable(_terrainProviderRuntime) && mapMagicBridge != null && mapMagicBridge.IsAvailable)
                    _terrainProviderRuntime = mapMagicBridge;
            }

            if (worldZoneDirector == null || !worldZoneDirector.isActiveAndEnabled)
            {
                worldZoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            }

            if (biomeMatrixDirector == null || !biomeMatrixDirector.isActiveAndEnabled)
            {
                biomeMatrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            }

            if (_worldCaveDirector == null || !_worldCaveDirector.isActiveAndEnabled)
            {
                _worldCaveDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(ref _worldCaveDirector);
            }

            RefreshCachedDependencyDiagnostics();
        }

        private void RefreshCachedDependencyDiagnostics()
        {
            _debugBridgeReady = (mapMagicBridge != null && mapMagicBridge.isActiveAndEnabled) ||
                                IsTerrainProviderAvailable(_terrainProviderRuntime) ||
                                IsTerrainProviderAvailable(GlobalRegistry.Terrain);
            _debugZoneDirectorReady = worldZoneDirector != null;
            _debugBiomeDirectorReady = biomeMatrixDirector != null;
        }

        private void OnValidate()
        {
            _isDataDirty = true;
        }

        private void UpdateDiagnostics(FieldSample sample, string channel, float value)
        {
#if UNITY_EDITOR
            _debugLastZone = sample.zone != null
                ? sample.zone.ZoneLabel
                : GetZoneKindLabel(sample.resolvedZoneKind);
            _debugLastBiomeProfile = sample.biomeProfile != null ? sample.biomeProfile.biomeName : "None";
            _debugLastBiomeFamily = sample.biomeFamily != null ? sample.biomeFamily.familyLabel : "None";
            _debugLastPattern = sample.isValid != 0 ? GetPatternLabel(sample.resolvedPattern) : PatternLabelNone;
            _debugPatternOverride = forcePatternPreviewOverride
                ? limitPatternOverrideToFallback
                    ? PatternLabelFallbackOnly
                    : GetPatternLabel(previewPatternOverride)
                : "None";
            _debugPreviewBiomeOverride = forcePatternPreviewOverride
                ? ResolvePreviewBiomeLabel(ResolvePreviewPatternBiomeFamily(previewPatternOverride, sample.depthMeters, sample.slopeDegrees, sample.biomeFamily))
                : "None";
            _debugPreviewMatrixOverride = forceMatrixBiomePreviewOverride && previewMatrixBiomeOverride != null
                ? limitMatrixBiomeOverrideToFallback
                    ? MatrixLabelFallbackOnly
                    : previewMatrixBiomeOverride.biomeName
                : forcePatternPreviewOverride
                    ? ResolvePreviewPatternBiomeProfile(previewPatternOverride, sample.biomeFamily) != null
                        ? ResolvePreviewPatternBiomeProfile(previewPatternOverride, sample.biomeFamily).biomeName
                        : "None"
                    : "None";
            _debugPreviewZoneOverride = forcePatternPreviewOverride
                ? GetZoneKindLabel(ResolvePreviewPatternZoneKind(previewPatternOverride))
                : "None";
            _debugLastHeatmap = string.IsNullOrWhiteSpace(channel) ? "None" : channel;
            _debugLastHeightSource = GetSeafloorSourceLabel(sample.seafloorSource);
            _debugLastHeatmapValue = value;
            _debugLastDepth = sample.depthMeters;
            _debugLastSlope = sample.slopeDegrees;
            _debugLastCurvature = sample.curvature;
            _debugLastCaveProximity = sample.caveProximity;
            _debugLastCompositionPotential = sample.compositionPotential;
#endif
        }

        private static string GetPatternLabel(WorldProceduralPattern pattern)
        {
            switch (pattern)
            {
                case WorldProceduralPattern.SedimentResources:
                    return PatternLabelSedimentResources;
                case WorldProceduralPattern.FertileShallows:
                    return PatternLabelFertileShallows;
                case WorldProceduralPattern.ReefNavigation:
                    return PatternLabelReefNavigation;
                case WorldProceduralPattern.IndustrialService:
                    return PatternLabelIndustrialService;
                case WorldProceduralPattern.BrineToxic:
                    return PatternLabelBrineToxic;
                case WorldProceduralPattern.VolcanicPressure:
                    return PatternLabelVolcanicPressure;
                case WorldProceduralPattern.RiftHazard:
                    return PatternLabelRiftHazard;
                case WorldProceduralPattern.AbyssSparse:
                    return PatternLabelAbyssSparse;
                case WorldProceduralPattern.LandmarkCorridor:
                    return PatternLabelLandmarkCorridor;
                default:
                    return PatternLabelNone;
            }
        }

        private static string GetSeafloorSourceLabel(SeafloorSource source)
        {
            switch (source)
            {
                case SeafloorSource.MapMagicHeight:
                    return SeafloorSourceMapMagicLabel;
                case SeafloorSource.TerrainProviderHeight:
                    return SeafloorSourceTerrainProviderLabel;
                case SeafloorSource.SceneProbeLegacy:
                    return SeafloorSourceSceneProbeLegacyLabel;
                case SeafloorSource.MacroGeologyFallback:
                    return SeafloorSourceMacroGeologyLabel;
                case SeafloorSource.FallbackSynthetic:
                    return SeafloorSourceFallbackLabel;
                default:
                    return SeafloorSourceNoneLabel;
            }
        }

        private static string GetZoneKindLabel(WorldZoneAnchor.ZoneKind zoneKind)
        {
            switch (zoneKind)
            {
                case WorldZoneAnchor.ZoneKind.Resources:
                    return "Resources";
                case WorldZoneAnchor.ZoneKind.Fabrication:
                    return "Fabrication";
                case WorldZoneAnchor.ZoneKind.Trial:
                    return "Trial";
                case WorldZoneAnchor.ZoneKind.Construction:
                    return "Construction";
                case WorldZoneAnchor.ZoneKind.Power:
                    return "Power";
                case WorldZoneAnchor.ZoneKind.Service:
                    return "Service";
                case WorldZoneAnchor.ZoneKind.Progression:
                    return "Progression";
                case WorldZoneAnchor.ZoneKind.Combat:
                    return "Combat";
                case WorldZoneAnchor.ZoneKind.Navigation:
                    return "Navigation";
                default:
                    return "Generic";
            }
        }
    }
}
