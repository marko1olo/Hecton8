using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class WorldProceduralFieldSampler : MonoBehaviour
    {
        private const string SyntheticZoneLabelPrefix = "Synthetic:";
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
        private const string SeafloorSourceNoneLabel = "None";
        private const string SeafloorSourceMapMagicLabel = "MapMagicHeight";
        private const string SeafloorSourceRaycastLabel = "SceneRaycast";
        private const string SeafloorSourceFallbackLabel = "FallbackSynthetic";
        private const int MaxSeafloorHeightCacheEntries = 4096;
        private const int MaxBiomeIndexCacheEntries = 4096;

        public enum SeafloorSource
        {
            None,
            MapMagicHeight,
            SceneRaycast,
            FallbackSynthetic
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

        public struct ZoneData
        {
            public float2 PositionXZ;
            public float ActivationRadius;
            public float HoldRadius;
            public float EdgeBlendDistance;
            public float EdgeNoiseScale;
            public float EdgeNoiseStrength;
            public float2 EdgeNoiseOffset;
            public int Priority;
            public int Kind;
            public int Tier;
            public int DominantMatrixDataIndex;
            public int DominantFamilyDataIndex;
            public int RouteCritical;
        }

        public struct BiomeMatrixData
        {
            public int MatrixIndex;
            public int FamilyDataIndex;
            public float MinDepthMeters;
            public float MaxDepthMeters;
            public int LoosePickupBias;
            public int NodeExtractionBias;
            public int SalvageBias;
            public int CommonResourceBias;
            public int UncommonResourceBias;
            public int RareResourceBias;
            public int RoutePressure;
            public int LandmarkStrength;
            public int RewardPull;
            public int SurvivalPressure;
            public int IsPlaceholder;
        }

        public struct BiomeFamilyData
        {
            public int FamilyInstanceId;
            public BiomeFamilyFlags Flags;
        }

        public struct CellInputData
        {
            public float3 Position;
            public float CenterHeight;
            public float NorthHeight;
            public float SouthHeight;
            public float EastHeight;
            public float WestHeight;
            public float WaterSurface;
            public int BiomeIndex;
            public int CellX;
            public int CellZ;
            public int SeafloorSource;
            public int IsValid;
        }

        public struct CellOutputData
        {
            public float3 Position;
            public int CellX;
            public int CellZ;
            public float SeafloorHeight;
            public float DepthMeters;
            public float SlopeDegrees;
            public float Curvature;
            public float RidgeSignal;
            public float CanyonSignal;
            public float CaveProximity;
            public float CompositionPotential;
            public float ZoneWeight;
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
            public float RuggedBias;
            public float FertileBias;
            public float HazardBias;
            public float ServiceBias;
            public float ResourceBias;
            public float ShelterBias;
            public float LandmarkBias;
            public float RockDensityHeat;
            public float KelpDensityHeat;
            public float FloraDensityHeat;
            public float CoralDensityHeat;
            public float BioDensityHeat;
            public float DebrisDensityHeat;
            public float RuinDensityHeat;
            public float CaveDensityHeat;
            public float LandmarkStrengthHeat;
            public float FaunaDensityHeat;
            public float HazardDensityHeat;
            public float ResourceDensityHeat;
            public float ShelterDensityHeat;
            public float ServiceDensityHeat;
            public float GenericHeat;
            public int BiomeIndex;
            public int ZoneDataIndex;
            public int BiomeMatrixDataIndex;
            public int BiomeFamilyDataIndex;
            public int ResolvedZoneKind;
            public int ResolvedPattern;
            public int PreviewOverrideActive;
            public int SeafloorSource;
            public int IsValid;
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
            public HectonBiomeMatrixProfile biomeProfile;
            public HectonBiomeFamilyProfile biomeFamily;
            public WorldZoneAnchor zone;
            public float zoneWeight;
            public WorldZoneAnchor.ZoneKind resolvedZoneKind;
            public WorldProceduralPattern resolvedPattern;
            public bool isPreviewOverride;
            public SeafloorSource seafloorSource;
            public bool isValid;
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

        [Header("Preview Overrides")]
        [SerializeField] private bool forcePatternPreviewOverride;
        [SerializeField] private WorldProceduralPattern previewPatternOverride = WorldProceduralPattern.SedimentResources;
        [SerializeField] private bool limitPatternOverrideToFallback = true;
        [SerializeField] private bool forceMatrixBiomePreviewOverride;
        [SerializeField] private HectonBiomeMatrixProfile previewMatrixBiomeOverride;
        [SerializeField] private bool limitMatrixBiomeOverrideToFallback = true;

        [Header("Diagnostics")]
        [Tooltip("Оставляй выключенным в обычном runtime. Живые inspector-диагностики sampler-а дорогие и нужны только для точечной отладки.")]
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
        [SerializeField] private int _debugBiomeCacheHits;
        [SerializeField] private int _debugBiomeCacheMisses;

        private readonly List<WorldZoneAnchor> _anchors = new List<WorldZoneAnchor>(32);
        private readonly List<WorldZoneAnchor> _zoneBakeList = new List<WorldZoneAnchor>(32);
        private readonly List<HectonBiomeMatrixProfile> _biomeMatrixBakeList = new List<HectonBiomeMatrixProfile>(160);
        private readonly List<HectonBiomeFamilyProfile> _biomeFamilyBakeList = new List<HectonBiomeFamilyProfile>(48);
        private readonly Dictionary<WorldZoneAnchor, int> _zoneDataIndexLookup = new Dictionary<WorldZoneAnchor, int>(32);
        private readonly Dictionary<HectonBiomeMatrixProfile, int> _biomeMatrixDataIndexLookup = new Dictionary<HectonBiomeMatrixProfile, int>(160);
        private readonly Dictionary<HectonBiomeFamilyProfile, int> _biomeFamilyDataIndexLookup = new Dictionary<HectonBiomeFamilyProfile, int>(48);
        private readonly Dictionary<Vector2Int, CachedHeightSample> _seafloorHeightCache = new Dictionary<Vector2Int, CachedHeightSample>(1536);
        private readonly Dictionary<Vector2Int, CachedBiomeSample> _biomeIndexCache = new Dictionary<Vector2Int, CachedBiomeSample>(1536);
        private readonly RaycastHit[] _seafloorRaycastHits = new RaycastHit[4]; // COLD ALLOC: reused non-alloc seafloor probes.
        private NativeArray<ZoneData> _burstZoneData;
        private NativeArray<BiomeMatrixData> _burstBiomeMatrixData;
        private NativeArray<BiomeFamilyData> _burstBiomeFamilyData;
        private int _burstZoneDataCount;
        private int _burstBiomeMatrixDataCount;
        private int _burstBiomeFamilyDataCount;
        private bool _isDataDirty = true;
        private int _lastActiveAnchorVersion = -1;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;
        private bool _samplingFramePrepared;
        private int _samplingFrameId;

        private struct CachedHeightSample
        {
            public CachedHeightSample(float height, SeafloorSource source, int samplingFrameId)
            {
                Height = height;
                Source = source;
                SamplingFrameId = samplingFrameId;
            }

            public float Height;
            public SeafloorSource Source;
            public int SamplingFrameId;
        }

        private struct CachedBiomeSample
        {
            public CachedBiomeSample(int biomeIndex, int samplingFrameId)
            {
                BiomeIndex = biomeIndex;
                SamplingFrameId = samplingFrameId;
            }

            public int BiomeIndex;
            public int SamplingFrameId;
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

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
        private struct CellSamplingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CellInputData> CellInputs;
            [ReadOnly] public NativeArray<ZoneData> Zones;
            [ReadOnly] public NativeArray<BiomeMatrixData> BiomeMatrices;
            [ReadOnly] public NativeArray<BiomeFamilyData> BiomeFamilies;
            [WriteOnly] public NativeArray<CellOutputData> CellOutputs;

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
                    CellOutputs[index] = CreateInvalidCellOutput(input, CurrentBiomeMatrixDataIndex, CurrentBiomeFamilyDataIndex);
                    return;
                }

                CellOutputData output = BuildCellOutput(
                    input,
                    Zones,
                    BiomeMatrices,
                    BiomeFamilies,
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

                CellOutputs[index] = output;
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
                BiomeIndex = input.BiomeIndex,
                SeafloorSource = input.SeafloorSource,
                ZoneDataIndex = -1,
                BiomeMatrixDataIndex = currentBiomeMatrixDataIndex,
                BiomeFamilyDataIndex = currentBiomeFamilyDataIndex,
                ResolvedZoneKind = (int)WorldZoneAnchor.ZoneKind.Generic,
                ResolvedPattern = (int)WorldProceduralPattern.SedimentResources,
                PreviewOverrideActive = 0,
                IsValid = 0
            };
        }

        private static CellOutputData BuildCellOutput(
            in CellInputData input,
            NativeArray<ZoneData> zones,
            NativeArray<BiomeMatrixData> biomeMatrices,
            NativeArray<BiomeFamilyData> biomeFamilies,
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
            float gradient = math.sqrt(dx * dx + dz * dz);
            float slopeDegrees = math.degrees(math.atan(gradient));
            float curvature = (input.WestHeight + input.EastHeight + input.NorthHeight + input.SouthHeight - (input.CenterHeight * 4f)) / math.max(0.0001f, probe * probe);
            curvature = math.clamp(curvature / 0.85f, -1f, 1f);

            CellOutputData output = new CellOutputData
            {
                Position = new float3(input.Position.x, input.CenterHeight, input.Position.z),
                CellX = input.CellX,
                CellZ = input.CellZ,
                SeafloorHeight = input.CenterHeight,
                DepthMeters = math.max(0f, input.WaterSurface - input.CenterHeight),
                SlopeDegrees = slopeDegrees,
                Curvature = curvature,
                BiomeIndex = input.BiomeIndex,
                SeafloorSource = input.SeafloorSource,
                ZoneDataIndex = -1,
                BiomeMatrixDataIndex = currentBiomeMatrixDataIndex,
                BiomeFamilyDataIndex = currentBiomeFamilyDataIndex,
                ResolvedZoneKind = (int)WorldZoneAnchor.ZoneKind.Generic,
                ResolvedPattern = (int)WorldProceduralPattern.SedimentResources,
                PreviewOverrideActive = 0,
                IsValid = 1
            };

            FillNoiseContext(ref output, fieldNoiseScale, detailNoiseScale);
            output.ZoneDataIndex = ResolveZoneDataIndex(output.Position.xz, zones, zoneCount, currentZoneDataIndex, out output.ZoneWeight);
            if (output.ZoneDataIndex >= 0)
            {
                ZoneData zoneData = zones[output.ZoneDataIndex];
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

            output.RuggedBias = EvaluateRuggedBiomeBias(output.ZoneDataIndex, output.ResolvedZoneKind, output.BiomeFamilyDataIndex, zones, zoneCount, biomeMatrices, biomeMatrixCount, biomeFamilies, biomeFamilyCount);
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

            ComputeHeatChannels(ref output, biomeMatrices, biomeMatrixCount);
            return output;
        }

        private static void FillNoiseContext(ref CellOutputData output, float fieldNoiseScale, float detailNoiseScale)
        {
            float x = output.Position.x;
            float z = output.Position.z;
            output.TerrainNoise = EvaluateNoise01Job(x, z, fieldNoiseScale);
            output.DetailNoise = EvaluateNoise01Job(x + 91.7f, z - 33.4f, detailNoiseScale);
            output.SedimentFieldNoise = EvaluateNoise01Job(x - 218.6f, z + 57.4f, fieldNoiseScale * 0.74f);
            output.FertileFieldNoise = EvaluateNoise01Job(x + 127.8f, z - 146.2f, detailNoiseScale * 0.78f);
            output.ReefFieldNoise = EvaluateNoise01Job(x + 314.4f, z + 88.5f, detailNoiseScale * 0.58f);
            output.IndustrialFieldNoise = EvaluateNoise01Job(x - 401.1f, z - 203.6f, fieldNoiseScale * 0.82f);
            output.HazardFieldNoise = EvaluateNoise01Job(x + 261.7f, z - 318.3f, detailNoiseScale * 0.94f);
            output.LandmarkFieldNoise = EvaluateNoise01Job(x - 83.2f, z + 367.9f, fieldNoiseScale * 0.62f);
            output.BasinFieldNoise = EvaluateNoise01Job(x + 452.5f, z + 121.3f, detailNoiseScale * 0.66f);
            output.RuggedBiomeNoise = EvaluateNoise01Job(x + 173.4f, z - 117.2f, fieldNoiseScale * 0.9f);
            output.FertileBiomeNoise = EvaluateNoise01Job(x - 91.6f, z + 44.3f, fieldNoiseScale * 1.15f);
            output.ThermalBiomeNoise = EvaluateNoise01Job(x + 304.2f, z + 281.4f, detailNoiseScale * 0.92f);
            output.MetallicBiomeNoise = EvaluateNoise01Job(x - 211.5f, z + 96.7f, detailNoiseScale * 0.88f);
            output.CrystalBiomeNoise = EvaluateNoise01Job(x + 67.4f, z - 248.6f, detailNoiseScale * 0.84f);
            output.VoidBiomeNoise = EvaluateNoise01Job(x - 403.1f, z - 365.8f, fieldNoiseScale * 0.66f);
            output.ReefBiomeNoise = EvaluateNoise01Job(x + 149.7f, z - 71.9f, detailNoiseScale * 0.9f);
            output.BasinMacroNoise = EvaluateNoise01Job(x - 512.4f, z + 188.6f, fieldNoiseScale * 0.22f);
            output.ReefMacroNoise = EvaluateNoise01Job(x + 417.2f, z - 153.3f, fieldNoiseScale * 0.24f);
            output.ServiceMacroNoise = EvaluateNoise01Job(x - 286.5f, z + 407.8f, fieldNoiseScale * 0.21f);
            output.RiftMacroNoise = EvaluateNoise01Job(x + 598.1f, z - 487.2f, fieldNoiseScale * 0.19f);
            output.CoralPatternNoise = EvaluateNoise01Job(x + 153.4f, z - 74.7f, detailNoiseScale * 0.86f);
            output.CaveNoise = EvaluateNoise01Job(x - 141.7f, z + 208.3f, fieldNoiseScale * 0.78f);
            output.CompositionNoise = EvaluateNoise01Job(x + 387.2f, z - 291.4f, detailNoiseScale * 0.56f);
        }

        private static float EvaluateNoise01Job(float x, float z, float scale)
        {
            float s = math.max(0.0001f, scale);
            float a = NoiseTo01(noise.snoise(new float2(x * s, z * s)));
            float b = NoiseTo01(noise.snoise(new float2((x + 127.37f) * (s * 2.2f), (z - 93.11f) * (s * 2.2f))));
            return math.clamp((a * 0.65f) + (b * 0.35f), 0f, 1f);
        }

        private static float NoiseTo01(float value)
        {
            return math.clamp((value * 0.5f) + 0.5f, 0f, 1f);
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
                float distance = math.sqrt(distanceSqr);
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
                float fallbackDistance = math.distance(positionXZ, currentZone.PositionXZ);
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
            float centered = noise.snoise(sample);
            return math.clamp(1f + centered * zone.EdgeNoiseStrength, 0.75f, 1.35f);
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

        private static float EvaluateRuggedBiomeBias(int zoneDataIndex, int resolvedZoneKind, int biomeFamilyDataIndex, NativeArray<ZoneData> zones, int zoneCount, NativeArray<BiomeMatrixData> biomeMatrices, int biomeMatrixCount, NativeArray<BiomeFamilyData> biomeFamilies, int biomeFamilyCount)
        {
            if (zoneDataIndex >= 0 && zoneDataIndex < zoneCount)
            {
                ZoneData zoneData = zones[zoneDataIndex];
                float familyBias = ContainsFamilyFlags(zoneData.DominantFamilyDataIndex, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass, biomeFamilies, biomeFamilyCount);
                if (zoneData.DominantMatrixDataIndex < 0 || zoneData.DominantMatrixDataIndex >= biomeMatrixCount)
                    return math.lerp(0.25f, 1f, familyBias);

                BiomeMatrixData biomeData = biomeMatrices[zoneData.DominantMatrixDataIndex];
                float rugged = math.clamp((biomeData.LandmarkStrength + biomeData.RoutePressure) / 10f, 0f, 1f);
                return math.clamp((rugged * 0.65f) + (familyBias * 0.35f), 0f, 1f);
            }

            float fallbackFamilyBias = ContainsFamilyFlags(biomeFamilyDataIndex, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass, biomeFamilies, biomeFamilyCount);
            if (fallbackFamilyBias > 0f)
                return math.lerp(0.25f, 1f, fallbackFamilyBias);

            return resolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Navigation || resolvedZoneKind == (int)WorldZoneAnchor.ZoneKind.Progression ? 0.56f : 0.38f;
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
            if (ContainsFamilyFlags(output.BiomeFamilyDataIndex, BiomeFamilyFlags.Reef | BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Crystal, biomeFamilies, biomeFamilyCount) > 0.5f)
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

            float shapedValue = ResolvePatternShapedHeat(channelIndex, output.ResolvedPattern, sedimentField, fertileField, reefField, industrialField, hazardField, landmarkField, shelterField, abyssField, output.RuggedBias, output.TerrainNoise, output.DetailNoise);
            shapedValue = math.clamp(shapedValue + biomeMatrixBonus * 0.92f, 0f, 1f);
            float blend = ResolvePatternFieldBlend((SeafloorSource)output.SeafloorSource, output.ZoneDataIndex >= 0);
            return math.clamp(math.lerp(baseValue, shapedValue, blend), 0f, 1f);
        }

        private static float ResolvePatternShapedHeat(int channelIndex, int resolvedPattern, float sedimentField, float fertileField, float reefField, float industrialField, float hazardField, float landmarkField, float shelterField, float abyssField, float ruggedBias, float terrainNoise, float detailNoise)
        {
            return (WorldProceduralPattern)resolvedPattern switch
            {
                WorldProceduralPattern.FertileShallows => channelIndex switch
                {
                    0 => 0.18f + sedimentField * 0.22f + ruggedBias * 0.12f + shelterField * 0.08f,
                    1 => fertileField * 0.92f,
                    2 => fertileField * 0.84f,
                    3 => reefField * 0.90f,
                    4 => fertileField * 0.62f + shelterField * 0.24f,
                    5 => industrialField * 0.26f,
                    6 => industrialField * 0.22f + landmarkField * 0.16f,
                    7 => landmarkField * 0.28f + hazardField * 0.16f,
                    8 => landmarkField * 0.48f + reefField * 0.12f,
                    9 => fertileField * 0.56f + shelterField * 0.30f,
                    10 => hazardField * 0.26f,
                    11 => sedimentField * 0.40f + fertileField * 0.18f,
                    12 => shelterField * 0.78f,
                    13 => industrialField * 0.22f,
                    _ => fertileField * 0.58f + sedimentField * 0.14f
                },
                WorldProceduralPattern.ReefNavigation => channelIndex switch
                {
                    0 => 0.20f + sedimentField * 0.18f + ruggedBias * 0.12f,
                    1 => fertileField * 0.72f + reefField * 0.14f,
                    2 => fertileField * 0.70f + reefField * 0.12f,
                    3 => reefField * 0.94f,
                    4 => fertileField * 0.44f + shelterField * 0.22f,
                    5 => industrialField * 0.24f,
                    6 => industrialField * 0.20f + landmarkField * 0.18f,
                    7 => landmarkField * 0.38f + hazardField * 0.18f,
                    8 => landmarkField * 0.68f + reefField * 0.16f,
                    9 => fertileField * 0.42f + shelterField * 0.18f,
                    10 => hazardField * 0.28f,
                    11 => sedimentField * 0.32f + landmarkField * 0.12f,
                    12 => shelterField * 0.54f + reefField * 0.12f,
                    13 => industrialField * 0.22f,
                    _ => reefField * 0.56f + landmarkField * 0.18f
                },
                WorldProceduralPattern.SedimentResources => channelIndex switch
                {
                    0 => 0.18f + sedimentField * 0.86f + ruggedBias * 0.12f,
                    1 => fertileField * 0.24f + shelterField * 0.10f,
                    2 => fertileField * 0.14f + shelterField * 0.08f,
                    3 => reefField * 0.14f + fertileField * 0.06f,
                    4 => shelterField * 0.52f + fertileField * 0.12f,
                    5 => industrialField * 0.42f + hazardField * 0.08f,
                    6 => industrialField * 0.44f + landmarkField * 0.22f + sedimentField * 0.08f,
                    7 => hazardField * 0.30f + landmarkField * 0.30f + ruggedBias * 0.18f + sedimentField * 0.06f,
                    8 => landmarkField * 0.58f + sedimentField * 0.14f + ruggedBias * 0.08f,
                    9 => shelterField * 0.42f + fertileField * 0.14f,
                    10 => hazardField * 0.34f,
                    11 => sedimentField * 0.92f,
                    12 => shelterField * 0.88f,
                    13 => industrialField * 0.48f + sedimentField * 0.08f + landmarkField * 0.06f,
                    _ => sedimentField * 0.62f + shelterField * 0.18f
                },
                WorldProceduralPattern.IndustrialService => channelIndex switch
                {
                    0 => 0.18f + sedimentField * 0.34f + ruggedBias * 0.10f,
                    1 => fertileField * 0.18f,
                    2 => fertileField * 0.16f,
                    3 => reefField * 0.14f,
                    4 => shelterField * 0.24f,
                    5 => industrialField * 0.90f,
                    6 => industrialField * 0.76f + landmarkField * 0.12f,
                    7 => hazardField * 0.22f + landmarkField * 0.18f + industrialField * 0.12f,
                    8 => landmarkField * 0.44f + industrialField * 0.22f,
                    9 => hazardField * 0.16f + shelterField * 0.14f,
                    10 => hazardField * 0.46f + industrialField * 0.12f,
                    11 => sedimentField * 0.26f + industrialField * 0.12f,
                    12 => shelterField * 0.22f,
                    13 => industrialField * 0.96f,
                    _ => industrialField * 0.64f + landmarkField * 0.14f
                },
                WorldProceduralPattern.BrineToxic => channelIndex switch
                {
                    0 => 0.16f + sedimentField * 0.28f + industrialField * 0.18f + ruggedBias * 0.08f,
                    1 => fertileField * 0.08f,
                    2 => fertileField * 0.10f,
                    3 => reefField * 0.08f,
                    4 => fertileField * 0.16f + shelterField * 0.12f + hazardField * 0.08f,
                    5 => industrialField * 0.82f,
                    6 => industrialField * 0.58f + landmarkField * 0.14f,
                    7 => hazardField * 0.24f + landmarkField * 0.18f + industrialField * 0.12f,
                    8 => landmarkField * 0.36f + industrialField * 0.18f,
                    9 => fertileField * 0.12f + hazardField * 0.14f,
                    10 => hazardField * 0.54f + industrialField * 0.12f,
                    11 => sedimentField * 0.24f + industrialField * 0.14f,
                    12 => shelterField * 0.18f,
                    13 => industrialField * 0.82f,
                    _ => industrialField * 0.62f + hazardField * 0.10f
                },
                WorldProceduralPattern.VolcanicPressure => channelIndex switch
                {
                    0 => 0.20f + sedimentField * 0.46f + ruggedBias * 0.18f + hazardField * 0.10f,
                    1 => fertileField * 0.06f,
                    2 => fertileField * 0.08f,
                    3 => reefField * 0.06f,
                    4 => fertileField * 0.10f + hazardField * 0.10f + abyssField * 0.06f,
                    5 => industrialField * 0.34f + hazardField * 0.16f,
                    6 => industrialField * 0.42f + landmarkField * 0.18f + hazardField * 0.12f,
                    7 => landmarkField * 0.48f + hazardField * 0.28f + ruggedBias * 0.10f,
                    8 => landmarkField * 0.86f + hazardField * 0.10f,
                    9 => hazardField * 0.18f + abyssField * 0.10f,
                    10 => hazardField * 0.76f,
                    11 => sedimentField * 0.22f + hazardField * 0.10f,
                    12 => shelterField * 0.14f,
                    13 => industrialField * 0.42f + hazardField * 0.10f,
                    _ => landmarkField * 0.52f + hazardField * 0.16f + sedimentField * 0.12f
                },
                WorldProceduralPattern.RiftHazard => channelIndex switch
                {
                    0 => 0.18f + hazardField * 0.36f + ruggedBias * 0.18f + sedimentField * 0.16f,
                    1 => fertileField * 0.10f,
                    2 => fertileField * 0.12f,
                    3 => reefField * 0.10f,
                    4 => hazardField * 0.24f + abyssField * 0.10f,
                    5 => industrialField * 0.36f + hazardField * 0.12f,
                    6 => industrialField * 0.42f + hazardField * 0.18f + landmarkField * 0.10f,
                    7 => hazardField * 0.82f,
                    8 => landmarkField * 0.52f + hazardField * 0.16f,
                    9 => hazardField * 0.48f + abyssField * 0.18f,
                    10 => hazardField * 0.98f,
                    11 => sedimentField * 0.24f + hazardField * 0.10f,
                    12 => shelterField * 0.18f,
                    13 => industrialField * 0.34f,
                    _ => hazardField * 0.64f + industrialField * 0.14f
                },
                WorldProceduralPattern.AbyssSparse => channelIndex switch
                {
                    0 => 0.20f + abyssField * 0.44f + ruggedBias * 0.16f + sedimentField * 0.18f,
                    1 => fertileField * 0.06f,
                    2 => fertileField * 0.08f,
                    3 => reefField * 0.08f,
                    4 => abyssField * 0.18f + shelterField * 0.10f,
                    5 => industrialField * 0.18f + abyssField * 0.08f,
                    6 => industrialField * 0.22f + landmarkField * 0.18f,
                    7 => hazardField * 0.22f + landmarkField * 0.22f,
                    8 => landmarkField * 0.48f + abyssField * 0.12f,
                    9 => abyssField * 0.16f,
                    10 => hazardField * 0.24f + abyssField * 0.12f,
                    11 => sedimentField * 0.18f + abyssField * 0.08f,
                    12 => shelterField * 0.14f,
                    13 => industrialField * 0.16f,
                    _ => abyssField * 0.52f + landmarkField * 0.12f
                },
                WorldProceduralPattern.LandmarkCorridor => channelIndex switch
                {
                    0 => 0.22f + sedimentField * 0.26f + ruggedBias * 0.18f,
                    1 => fertileField * 0.24f,
                    2 => fertileField * 0.22f + landmarkField * 0.08f,
                    3 => reefField * 0.28f,
                    4 => shelterField * 0.22f + fertileField * 0.10f,
                    5 => industrialField * 0.26f,
                    6 => industrialField * 0.34f + landmarkField * 0.24f,
                    7 => landmarkField * 0.84f,
                    8 => landmarkField * 0.98f,
                    9 => shelterField * 0.18f + hazardField * 0.10f,
                    10 => hazardField * 0.34f + landmarkField * 0.08f,
                    11 => sedimentField * 0.22f + landmarkField * 0.10f,
                    12 => shelterField * 0.28f,
                    13 => industrialField * 0.26f + landmarkField * 0.10f,
                    _ => landmarkField * 0.74f + sedimentField * 0.10f
                },
                _ => (terrainNoise * 0.55f) + (detailNoise * 0.45f)
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
                SeafloorSource.SceneRaycast => hasZone ? 0.28f : 0.42f,
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

        private void OnEnable()
        {
            BiomeMatrixDirector.OnMatrixBiomeChanged += HandleMatrixBiomeChanged;
            _isDataDirty = true;
        }

        private void OnDisable()
        {
            BiomeMatrixDirector.OnMatrixBiomeChanged -= HandleMatrixBiomeChanged;
            DisposeBurstData();
            _isDataDirty = true;
            _samplingFramePrepared = false;
        }

        private void OnDestroy()
        {
            BiomeMatrixDirector.OnMatrixBiomeChanged -= HandleMatrixBiomeChanged;
            DisposeBurstData();
            _isDataDirty = true;
        }

        public void BeginScatterSamplingFrame()
        {
            PrepareBurstData();
            _samplingFrameId++;
            _samplingFramePrepared = true;
            if (enableLiveRuntimeDiagnostics)
            {
                _debugBiomeCacheHits = 0;
                _debugBiomeCacheMisses = 0;
            }
        }

        public void EndScatterSamplingFrame()
        {
            _samplingFramePrepared = false;
        }

        public void MarkBurstDataDirty()
        {
            _isDataDirty = true;
            _seafloorHeightCache.Clear();
            _biomeIndexCache.Clear();
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile _)
        {
            _isDataDirty = true;
        }

        public bool TryBuildCellInput(Vector3 position, int cellX, int cellZ, out CellInputData input)
        {
            input = default;
            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            if (!TryGetCellHeightContext(position, out CellHeightContext terrainContext))
                return false;

            TryResolveBiomeIndex(position.x, position.z, out int biomeIndex);

            float waterSurface = mapMagicBridge != null
                ? mapMagicBridge.WaterSurfaceLevel
                : Mathf.Max(position.y + 120f, terrainContext.CenterHeight + 50f);

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
                CellX = cellX,
                CellZ = cellZ,
                SeafloorSource = (int)terrainContext.CenterSource,
                IsValid = 1
            };
            return true;
        }

        public JobHandle ScheduleCellSamplingJob(NativeArray<CellInputData> cellInputs, NativeArray<CellOutputData> cellOutputs, int cellCount)
        {
            if (_isDataDirty || !_burstZoneData.IsCreated || !_burstBiomeMatrixData.IsCreated || !_burstBiomeFamilyData.IsCreated)
                PrepareBurstData();

            CellSamplingJob job = new CellSamplingJob
            {
                CellInputs = cellInputs,
                Zones = _burstZoneData,
                BiomeMatrices = _burstBiomeMatrixData,
                BiomeFamilies = _burstBiomeFamilyData,
                CellOutputs = cellOutputs,
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

            return job.Schedule(cellCount, math.max(1, math.min(32, cellCount / 8)));
        }

        public bool TryBuildFieldSample(in CellOutputData output, out FieldSample sample)
        {
            sample = default;
            if (output.IsValid == 0)
                return false;

            sample = new FieldSample
            {
                position = new Vector3(output.Position.x, output.Position.y, output.Position.z),
                seafloorHeight = output.SeafloorHeight,
                depthMeters = output.DepthMeters,
                slopeDegrees = output.SlopeDegrees,
                curvature = output.Curvature,
                ridgeSignal = output.RidgeSignal,
                canyonSignal = output.CanyonSignal,
                caveProximity = output.CaveProximity,
                compositionPotential = output.CompositionPotential,
                biomeIndex = output.BiomeIndex,
                zoneDataIndex = output.ZoneDataIndex,
                biomeMatrixDataIndex = output.BiomeMatrixDataIndex,
                biomeFamilyDataIndex = output.BiomeFamilyDataIndex,
                biomeProfile = output.BiomeMatrixDataIndex >= 0 && output.BiomeMatrixDataIndex < _biomeMatrixBakeList.Count ? _biomeMatrixBakeList[output.BiomeMatrixDataIndex] : null,
                biomeFamily = output.BiomeFamilyDataIndex >= 0 && output.BiomeFamilyDataIndex < _biomeFamilyBakeList.Count ? _biomeFamilyBakeList[output.BiomeFamilyDataIndex] : null,
                zone = output.ZoneDataIndex >= 0 && output.ZoneDataIndex < _zoneBakeList.Count ? _zoneBakeList[output.ZoneDataIndex] : null,
                zoneWeight = output.ZoneWeight,
                resolvedZoneKind = (WorldZoneAnchor.ZoneKind)output.ResolvedZoneKind,
                resolvedPattern = (WorldProceduralPattern)output.ResolvedPattern,
                isPreviewOverride = output.PreviewOverrideActive != 0,
                seafloorSource = (SeafloorSource)output.SeafloorSource,
                isValid = true
            };
            return true;
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
                    ? 0.95f + Mathf.Clamp01(rule.densityScale * 0.12f)
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
                WorldPrefabFamilyProfile.PlacementMode.Landmark => Mathf.Lerp(0.8f, 1.2f, output.LandmarkBias),
                WorldPrefabFamilyProfile.PlacementMode.Cluster => 1.05f,
                WorldPrefabFamilyProfile.PlacementMode.Patch => 1.08f,
                WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => Mathf.Lerp(0.85f, 1.15f, output.HazardBias),
                _ => 1f
            };

            return Mathf.Clamp01(value * densityScaleFactor);
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

        public void PrepareBurstData()
        {
            ResolveReferences();

            int activeAnchorVersion = WorldZoneAnchor.ActiveAnchorVersion;
            if (activeAnchorVersion != _lastActiveAnchorVersion)
            {
                _lastActiveAnchorVersion = activeAnchorVersion;
                _isDataDirty = true;
            }

            if (!_isDataDirty)
                return;

            RefreshActiveAnchorsSnapshot();

            _zoneDataIndexLookup.Clear();
            _biomeMatrixDataIndexLookup.Clear();
            _biomeFamilyDataIndexLookup.Clear();
            _zoneBakeList.Clear();
            _biomeMatrixBakeList.Clear();
            _biomeFamilyBakeList.Clear();

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

            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                RegisterMatrixForBake(anchor.DominantMatrixBiome);
                RegisterFamilyForBake(anchor.DominantBiomeFamily);
            }

            EnsureNativeArrayCapacity(ref _burstBiomeFamilyData, _biomeFamilyBakeList.Count);
            _burstBiomeFamilyDataCount = _biomeFamilyBakeList.Count;
            for (int i = 0; i < _biomeFamilyBakeList.Count; i++)
            {
                HectonBiomeFamilyProfile family = _biomeFamilyBakeList[i];
                _burstBiomeFamilyData[i] = new BiomeFamilyData
                {
                    #pragma warning disable CS0618
                    FamilyInstanceId = family != null ? family.GetInstanceID() : 0,
                    #pragma warning restore CS0618
                    Flags = TokenizeFamilyFlags(family)
                };
            }

            EnsureNativeArrayCapacity(ref _burstBiomeMatrixData, _biomeMatrixBakeList.Count);
            _burstBiomeMatrixDataCount = _biomeMatrixBakeList.Count;
            for (int i = 0; i < _biomeMatrixBakeList.Count; i++)
            {
                HectonBiomeMatrixProfile profile = _biomeMatrixBakeList[i];
                _burstBiomeMatrixData[i] = new BiomeMatrixData
                {
                    MatrixIndex = profile != null ? profile.matrixIndex : -1,
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
                    IsPlaceholder = profile != null && profile.isPlaceholder ? 1 : 0
                };
            }

            EnsureNativeArrayCapacity(ref _burstZoneData, _anchors.Count);
            _burstZoneDataCount = 0;
            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                int zoneDataIndex = _burstZoneDataCount++;
                _zoneDataIndexLookup[anchor] = zoneDataIndex;
                _zoneBakeList.Add(anchor);
                _burstZoneData[zoneDataIndex] = new ZoneData
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

            _isDataDirty = false;
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
            if (mapMagicBridge != null)
                mapMagicBridge.TryGetBiomeIndex(position.x, position.z, out biomeIndex);

            float waterSurface = mapMagicBridge != null
                ? mapMagicBridge.WaterSurfaceLevel
                : Mathf.Max(position.y + 120f, seafloorHeight + 50f);
            float depthMeters = Mathf.Max(0f, waterSurface - seafloorHeight);
            float slopeDegrees = terrainContext.SlopeDegrees;
            float curvature = terrainContext.Curvature;
            WorldZoneAnchor zone = ResolveZone(new Vector3(position.x, seafloorHeight, position.z), out float zoneWeight);
            int zoneDataIndex = ResolveZoneDataIndex(zone);
            HectonBiomeMatrixProfile biomeProfile = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
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
            biomeMatrixDataIndex = ResolveBiomeMatrixDataIndex(biomeProfile);
            biomeFamilyDataIndex = ResolveBiomeFamilyDataIndex(biomeFamily);

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
                biomeProfile = biomeProfile,
                biomeFamily = biomeFamily,
                zone = zone,
                zoneWeight = zoneWeight,
                resolvedZoneKind = resolvedZoneKind,
                resolvedPattern = resolvedPattern,
                isPreviewOverride = previewOverrideApplied,
                seafloorSource = seafloorSource,
                isValid = true
            };

            if (ShouldUpdateDiagnostics())
                UpdateDiagnostics(sample, "sample", 0f);
            return true;
        }

        public bool TryResolveSeafloorSource(Vector3 position, out SeafloorSource seafloorSource)
        {
            seafloorSource = SeafloorSource.None;

            if (!_samplingFramePrepared)
                BeginScatterSamplingFrame();

            return TryResolveSeafloorHeight(position, out _, out seafloorSource);
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

            float depth01 = Mathf.Clamp01(sample.depthMeters / 800f);
            float shallow01 = 1f - Mathf.Clamp01(sample.depthMeters / 220f);
            float midDepth01 = 1f - Mathf.Clamp01(Mathf.Abs(sample.depthMeters - 260f) / 320f);
            float deep01 = Mathf.Clamp01((sample.depthMeters - 180f) / 900f);
            float abyss01 = Mathf.Clamp01((sample.depthMeters - 900f) / 1800f);
            float flat01 = 1f - Mathf.Clamp01(sample.slopeDegrees / 28f);
            float steep01 = Mathf.Clamp01((sample.slopeDegrees - 8f) / 40f);
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
            value = Mathf.Clamp01(value + biomeMatrixBonus);

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
            patternShapedValue = Mathf.Clamp01(patternShapedValue + biomeMatrixBonus * 0.92f);
            value = Mathf.Lerp(value, patternShapedValue, ResolvePatternFieldBlend(sample.seafloorSource, sample.zone));

            if (family != null)
            {
                value *= family.placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => Mathf.Lerp(0.8f, 1.2f, landmarkBias),
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => 1.05f,
                    WorldPrefabFamilyProfile.PlacementMode.Patch => 1.08f,
                    WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => Mathf.Lerp(0.85f, 1.15f, hazardBias),
                    _ => 1f
                };
            }

            if (rule != null && !string.IsNullOrWhiteSpace(rule.gameplayIntent))
                value *= 0.95f + Mathf.Clamp01(rule.densityScale * 0.12f);

            value = Mathf.Clamp01(value);
            if (ShouldUpdateDiagnostics())
                UpdateDiagnostics(sample, channel, value);
            return value;
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

            float sedimentField = Mathf.Clamp01(
                resourceBias * 0.32f +
                shelterBias * 0.18f +
                flat01 * 0.16f +
                terrainNoise * 0.14f +
                sedimentNoise * 0.20f);
            float fertileField = Mathf.Clamp01(
                fertileBias * 0.34f +
                shallow01 * 0.16f +
                detailNoise * 0.12f +
                fertileNoise * 0.22f +
                shelterBias * 0.08f +
                (1f - hazardBias) * 0.08f);
            float reefField = Mathf.Clamp01(
                fertileBias * 0.24f +
                landmarkBias * 0.14f +
                shallow01 * 0.10f +
                reefNoise * 0.24f +
                flat01 * 0.08f +
                detailNoise * 0.12f +
                midDepth01 * 0.08f);
            float industrialField = Mathf.Clamp01(
                serviceBias * 0.34f +
                industrialNoise * 0.28f +
                terrainNoise * 0.10f +
                ruggedBias * 0.08f +
                deep01 * 0.08f +
                landmarkBias * 0.12f);
            float hazardField = Mathf.Clamp01(
                hazardBias * 0.38f +
                steep01 * 0.12f +
                deep01 * 0.12f +
                hazardNoise * 0.24f +
                ruggedBias * 0.14f);
            float landmarkField = Mathf.Clamp01(
                landmarkBias * 0.34f +
                steep01 * 0.16f +
                landmarkNoise * 0.26f +
                ruggedBias * 0.10f +
                deep01 * 0.08f +
                reefField * 0.06f);
            float shelterField = Mathf.Clamp01(
                shelterBias * 0.34f +
                flat01 * 0.18f +
                fertileField * 0.14f +
                basinNoise * 0.18f +
                detailNoise * 0.16f);
            float abyssField = Mathf.Clamp01(
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
                    "debris_density" => industrialField * 0.26f,
                    "ruin_density" => industrialField * 0.22f + landmarkField * 0.16f,
                    "cave_density" => landmarkField * 0.28f + hazardField * 0.16f,
                    "landmark_strength" => landmarkField * 0.48f + reefField * 0.12f,
                    "fauna_density" => fertileField * 0.56f + shelterField * 0.30f,
                    "hazard_density" => hazardField * 0.26f,
                    "resource_density" => sedimentField * 0.40f + fertileField * 0.18f,
                    "shelter_density" => shelterField * 0.78f,
                    "service_density" => industrialField * 0.22f,
                    _ => fertileField * 0.58f + sedimentField * 0.14f
                },
                WorldProceduralPattern.ReefNavigation => channel switch
                {
                    "rock_density" => 0.20f + sedimentField * 0.18f + ruggedBias * 0.12f,
                    "kelp_density" => fertileField * 0.72f + reefField * 0.14f,
                    "flora_density" => fertileField * 0.70f + reefField * 0.12f,
                    "coral_density" => reefField * 0.94f,
                    "bio_density" => fertileField * 0.44f + shelterField * 0.22f,
                    "debris_density" => industrialField * 0.24f,
                    "ruin_density" => industrialField * 0.20f + landmarkField * 0.18f,
                    "cave_density" => landmarkField * 0.38f + hazardField * 0.18f,
                    "landmark_strength" => landmarkField * 0.68f + reefField * 0.16f,
                    "fauna_density" => fertileField * 0.42f + shelterField * 0.18f,
                    "hazard_density" => hazardField * 0.28f,
                    "resource_density" => sedimentField * 0.32f + landmarkField * 0.12f,
                    "shelter_density" => shelterField * 0.54f + reefField * 0.12f,
                    "service_density" => industrialField * 0.22f,
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

            return Mathf.Clamp01(shapedValue);
        }

        private static float ResolvePatternFieldBlend(SeafloorSource source, WorldZoneAnchor zone)
        {
            return source switch
            {
                SeafloorSource.FallbackSynthetic => zone == null ? 0.78f : 0.66f,
                SeafloorSource.SceneRaycast => zone == null ? 0.42f : 0.28f,
                SeafloorSource.MapMagicHeight => zone == null ? 0.34f : 0.18f,
                _ => 0.2f
            };
        }

        private bool TryResolveSeafloorHeight(Vector3 position, out float seafloorHeight, out SeafloorSource seafloorSource)
        {
            Vector2Int cacheKey = GetHeightCacheKey(position.x, position.z);
            if (_seafloorHeightCache.TryGetValue(cacheKey, out CachedHeightSample cachedSample))
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
                TrimSeafloorHeightCacheIfNeeded();
                _seafloorHeightCache[cacheKey] = new CachedHeightSample(seafloorHeight, seafloorSource, _samplingFrameId);
            }

            return resolved;
        }

        private bool TryResolveBiomeIndex(float x, float z, out int biomeIndex)
        {
            biomeIndex = 0;
            Vector2Int cacheKey = GetHeightCacheKey(x, z);
            if (_biomeIndexCache.TryGetValue(cacheKey, out CachedBiomeSample cachedSample) &&
                cachedSample.SamplingFrameId == _samplingFrameId)
            {
                if (enableLiveRuntimeDiagnostics)
                    _debugBiomeCacheHits++;

                biomeIndex = cachedSample.BiomeIndex;
                return true;
            }

            if (enableLiveRuntimeDiagnostics)
                _debugBiomeCacheMisses++;

            if (mapMagicBridge != null)
                mapMagicBridge.TryGetBiomeIndex(x, z, out biomeIndex);

            TrimBiomeIndexCacheIfNeeded();
            _biomeIndexCache[cacheKey] = new CachedBiomeSample(biomeIndex, _samplingFrameId);
            return true;
        }

        private bool TryResolveSeafloorHeightUncached(Vector3 position, out float seafloorHeight, out SeafloorSource seafloorSource)
        {
            seafloorHeight = 0f;
            seafloorSource = SeafloorSource.None;

            if (mapMagicBridge != null && mapMagicBridge.TryGetHeight(position.x, position.z, out seafloorHeight))
            {
                seafloorSource = SeafloorSource.MapMagicHeight;
                return true;
            }

            float waterSurface = mapMagicBridge != null ? mapMagicBridge.WaterSurfaceLevel : Mathf.Max(position.y + 500f, 1000f);
            float rayOriginY = Mathf.Max(waterSurface + 1000f, position.y + 1000f);
            Vector3 origin = new Vector3(position.x, rayOriginY, position.z);
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _seafloorRaycastHits,
                40000f,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = _seafloorRaycastHits[hitIndex];
                if (ShouldIgnoreSeafloorHit(hit))
                    continue;

                seafloorHeight = hit.point.y;
                seafloorSource = SeafloorSource.SceneRaycast;
                return true;
            }

            float fallbackSurface = mapMagicBridge != null ? mapMagicBridge.WaterSurfaceLevel : Mathf.Max(position.y + 120f, 120f);
            seafloorHeight = fallbackSurface - EstimateFallbackDepth(position.x, position.z);
            seafloorSource = SeafloorSource.FallbackSynthetic;
            return true;
        }

        private bool ShouldIgnoreSeafloorHit(in RaycastHit hit)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return true;

            Transform hitTransform = hitCollider.transform;
            if (playerTransform != null &&
                (hitTransform == playerTransform || hitTransform.IsChildOf(playerTransform)))
            {
                return true;
            }

            Rigidbody hitBody = hit.rigidbody;
            if (hitBody == null)
                return false;

            Transform hitBodyTransform = hitBody.transform;
            return playerTransform != null &&
                   (hitBodyTransform == playerTransform || hitBodyTransform.IsChildOf(playerTransform));
        }

        private bool TryGetLocalTerrainContext(Vector3 position, out LocalTerrainContext terrainContext)
        {
            terrainContext = default;
            if (!TryGetCellHeightContext(position, out CellHeightContext cellHeightContext))
                return false;

            float probe = Mathf.Max(1f, slopeProbeMeters);
            float dx = (cellHeightContext.EastHeight - cellHeightContext.WestHeight) / (probe * 2f);
            float dz = (cellHeightContext.NorthHeight - cellHeightContext.SouthHeight) / (probe * 2f);
            float gradient = Mathf.Sqrt(dx * dx + dz * dz);
            float slopeDegrees = Mathf.Atan(gradient) * Mathf.Rad2Deg;
            float curvature = (cellHeightContext.WestHeight + cellHeightContext.EastHeight + cellHeightContext.NorthHeight + cellHeightContext.SouthHeight - (cellHeightContext.CenterHeight * 4f)) / Mathf.Max(0.0001f, probe * probe);

            terrainContext = new LocalTerrainContext
            {
                CenterHeight = cellHeightContext.CenterHeight,
                NorthHeight = cellHeightContext.NorthHeight,
                SouthHeight = cellHeightContext.SouthHeight,
                EastHeight = cellHeightContext.EastHeight,
                WestHeight = cellHeightContext.WestHeight,
                SlopeDegrees = slopeDegrees,
                Curvature = Mathf.Clamp(curvature / 0.85f, -1f, 1f),
                CenterSource = cellHeightContext.CenterSource
            };
            return true;
        }

        private bool TryGetCellHeightContext(Vector3 position, out CellHeightContext terrainContext)
        {
            terrainContext = default;
            if (!TryResolveSeafloorHeight(position, out float centerHeight, out SeafloorSource centerSource))
                return false;

            float probe = Mathf.Max(1f, slopeProbeMeters);
            if (!TryResolveSeafloorHeight(new Vector3(position.x, centerHeight, position.z + probe), out float northHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x, centerHeight, position.z - probe), out float southHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x + probe, centerHeight, position.z), out float eastHeight, out _) ||
                !TryResolveSeafloorHeight(new Vector3(position.x - probe, centerHeight, position.z), out float westHeight, out _))
            {
                return false;
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

        private float EstimateFallbackDepth(float x, float z)
        {
            float broad = EvaluateNoise01(x + 311.1f, z - 177.4f, fieldNoiseScale * 0.55f);
            float detail = EvaluateNoise01(x - 91.6f, z + 441.2f, detailNoiseScale * 0.7f);
            float depth = Mathf.Lerp(70f, 240f, (broad * 0.7f) + (detail * 0.3f));
            return Mathf.Clamp(depth, 40f, 320f);
        }

        private float EvaluateRidgeSignal(float curvature, float slopeDegrees, int zoneDataIndex, WorldZoneAnchor zone)
        {
            float slope01 = Mathf.Clamp01((slopeDegrees - 8f) / 36f);
            float rugged = EvaluateRuggedBiomeBias(zoneDataIndex, zone);
            return Mathf.Clamp01(Mathf.Max(0f, curvature) * 0.62f + slope01 * 0.26f + rugged * 0.12f);
        }

        private float EvaluateCanyonSignal(float curvature, float slopeDegrees, int zoneDataIndex, WorldZoneAnchor zone)
        {
            float slope01 = Mathf.Clamp01((slopeDegrees - 10f) / 34f);
            float hazard = EvaluateHazardBias(zoneDataIndex, zone, zone != null ? zone.Kind : WorldZoneAnchor.ZoneKind.Generic);
            return Mathf.Clamp01(Mathf.Max(0f, -curvature) * 0.58f + slope01 * 0.22f + hazard * 0.20f);
        }

        private float EvaluateCaveProximity(
            float depthMeters,
            float slopeDegrees,
            int zoneDataIndex,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind resolvedZoneKind,
            float caveNoise)
        {
            float slope01 = Mathf.Clamp01((slopeDegrees - 8f) / 40f);
            float deep01 = Mathf.Clamp01((depthMeters - 120f) / 780f);
            float rugged = EvaluateRuggedBiomeBias(zoneDataIndex, zone);
            float hazard = EvaluateHazardBias(zoneDataIndex, zone, resolvedZoneKind);
            float landmark = EvaluateLandmarkBias(zoneDataIndex, zone, resolvedZoneKind);
            return Mathf.Clamp01(
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
            float slope01 = Mathf.Clamp01((slopeDegrees - 6f) / 42f);
            return Mathf.Clamp01(
                slope01 * 0.16f +
                Mathf.Abs(curvature) * 0.18f +
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

            for (int i = 0; i < _anchors.Count; i++)
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
                    (Mathf.Approximately(weight, bestWeight) && distanceSqr < bestDistanceSqr))
                {
                    best = anchor;
                    bestWeight = weight;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (best == null)
                best = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;

            zoneWeight = best != null ? Mathf.Max(bestWeight, best.EvaluateActivationWeight(position)) : 0f;
            return best;
        }

        private void RefreshActiveAnchorsSnapshot()
        {
            WorldZoneAnchor.CopyActiveAnchorsTo(_anchors);
        }

        private float EvaluateNoise01(float x, float z, float scale)
        {
            float s = Mathf.Max(0.0001f, scale);
            float a = Mathf.PerlinNoise(x * s, z * s);
            float b = Mathf.PerlinNoise((x + 127.37f) * (s * 2.2f), (z - 93.11f) * (s * 2.2f));
            return Mathf.Clamp01((a * 0.65f) + (b * 0.35f));
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

            float depth01 = Mathf.Clamp01(depthMeters / 1200f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 8f) / 40f);
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
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

            float fertileScore = Mathf.Clamp01(
                ((fertileNoise * 0.65f) + (reefNoise * 0.35f))
                - (resourceZoneBias * 0.08f)
                - (serviceZoneBias * 0.16f)
                - (hazardZoneBias * 0.18f)
                + (navigationZoneBias * 0.08f));
            float ruggedScore = Mathf.Clamp01((ruggedNoise * 0.55f) + (steep01 * 0.45f));
            float thermalScore = Mathf.Clamp01((thermalNoise * 0.75f) + (depth01 * 0.25f));
            float metallicScore = Mathf.Clamp01((metallicNoise * 0.7f) + (depth01 * 0.3f));
            float voidScore = Mathf.Clamp01((voidNoise * 0.7f) + (depth01 * 0.3f));
            float sedimentScore = Mathf.Clamp01(
                ((1f - ruggedScore) * 0.24f)
                + ((1f - thermalScore) * 0.14f)
                + (resourceZoneBias * 0.22f)
                + (shallow01 * 0.08f)
                + (fertileNoise * 0.12f)
                + (reefNoise * 0.04f));
            float serviceScore = Mathf.Clamp01(
                (thermalScore * 0.34f)
                + (metallicScore * 0.34f)
                + (serviceZoneBias * 0.24f)
                + (depth01 * 0.08f));
            float hazardScore = Mathf.Clamp01(
                (ruggedScore * 0.28f)
                + (thermalScore * 0.16f)
                + (voidScore * 0.18f)
                + (hazardZoneBias * 0.26f)
                + (depth01 * 0.12f));
            float reefScore = Mathf.Clamp01(
                (fertileScore * 0.46f)
                + (reefNoise * 0.28f)
                + (shallow01 * 0.14f)
                + (navigationZoneBias * 0.12f));
            float sedimentContinuity = Mathf.Clamp01(
                (resourceZoneBias * 0.28f)
                + (basinMacroNoise * 0.24f)
                + ((1f - ruggedScore) * 0.12f)
                + ((1f - thermalScore) * 0.1f)
                + (shallow01 * 0.08f)
                + (depth01 * 0.06f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float reefContinuity = Mathf.Clamp01(
                (reefScore * 0.42f)
                + (reefMacroNoise * 0.24f)
                + (fertileScore * 0.14f)
                + (navigationZoneBias * 0.08f)
                - (resourceZoneBias * 0.16f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float serviceContinuity = Mathf.Clamp01(
                (serviceScore * 0.46f)
                + (serviceMacroNoise * 0.22f)
                + (metallicScore * 0.12f)
                + (thermalScore * 0.08f));
            float hazardContinuity = Mathf.Clamp01(
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
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
            float deep01 = Mathf.Clamp01((depthMeters - 180f) / 900f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 10f) / 38f);
            float fertileNoise = cellContext.FertileBiomeNoise;
            float thermalNoise = cellContext.ThermalBiomeNoise;
            float metallicNoise = cellContext.MetallicBiomeNoise;
            float voidNoise = cellContext.VoidBiomeNoise;

            float resourceScore = Mathf.Clamp01((shallow01 * 0.4f) + (fertileNoise * 0.6f));
            float serviceScore = Mathf.Clamp01((metallicNoise * 0.55f) + (thermalNoise * 0.45f));
            float hazardScore = Mathf.Clamp01((deep01 * 0.4f) + (steep01 * 0.25f) + (voidNoise * 0.35f));

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
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
            float deep01 = Mathf.Clamp01((depthMeters - 180f) / 900f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 10f) / 36f);
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

            if (ContainsFamilyFlags(biomeFamilyDataIndex, biomeFamily, BiomeFamilyFlags.Reef | BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Crystal) > 0.5f)
                return resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation ? WorldProceduralPattern.ReefNavigation : WorldProceduralPattern.FertileShallows;

            if (deep01 > 0.7f)
                return WorldProceduralPattern.AbyssSparse;

            if (landmarkBias > 0.68f)
                return WorldProceduralPattern.LandmarkCorridor;

            return shallow01 > 0.45f
                ? WorldProceduralPattern.SedimentResources
                : WorldProceduralPattern.AbyssSparse;
        }

        private static HectonBiomeFamilyProfile ChooseFamily(params HectonBiomeFamilyProfile[] options)
        {
            if (options == null)
                return null;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                    return options[i];
            }

            return null;
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
                    return Mathf.Lerp(0.25f, 1f, familyBias);

                float rugged = Mathf.Clamp01((biomeData.LandmarkStrength + biomeData.RoutePressure) / 10f);
                return Mathf.Clamp01((rugged * 0.65f) + (familyBias * 0.35f));
            }

            if (zone == null)
                return 0.38f;

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float fallbackFamilyBias = ContainsFamilyFlags(-1, zone.DominantBiomeFamily, BiomeFamilyFlags.Rift | BiomeFamilyFlags.Granite | BiomeFamilyFlags.Tectonic | BiomeFamilyFlags.Volcanic | BiomeFamilyFlags.Glass);
            if (biome == null)
                return Mathf.Lerp(0.25f, 1f, fallbackFamilyBias);

            float fallbackRugged = Mathf.Clamp01((biome.landmarkStrength + biome.routePressure) / 10f);
            return Mathf.Clamp01((fallbackRugged * 0.65f) + (fallbackFamilyBias * 0.35f));
        }

        private float EvaluateFertileBiomeBias(int zoneDataIndex, WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint, int familyDataIndex, HectonBiomeFamilyProfile family)
        {
            float familyBias = ContainsFamilyFlags(familyDataIndex, family, BiomeFamilyFlags.Littoral | BiomeFamilyFlags.Reef | BiomeFamilyFlags.Fossil | BiomeFamilyFlags.Crystal | BiomeFamilyFlags.Coral | BiomeFamilyFlags.Kelp | BiomeFamilyFlags.Growth);
            float zoneBias = EvaluateZoneBias(zoneDataIndex, zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneKind.Navigation);
            return Mathf.Clamp01((familyBias * 0.72f) + (zoneBias * 0.28f));
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

                float fallbackBiomeBias = Mathf.Clamp01(Mathf.Max(fallbackBiome.survivalPressure, fallbackBiome.routePressure) / 5f);
                return Mathf.Clamp01((zoneBias * 0.55f) + (fallbackBiomeBias * 0.45f));
            }

            float biomeBias = Mathf.Clamp01(Mathf.Max(biomeData.SurvivalPressure, biomeData.RoutePressure) / 5f);
            return Mathf.Clamp01((zoneBias * 0.55f) + (biomeBias * 0.45f));
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

                float fallbackBiomeBias = Mathf.Clamp01(Mathf.Max(fallbackBiome.commonResourceBias, fallbackBiome.uncommonResourceBias) / 5f);
                return Mathf.Clamp01((zoneBias * 0.6f) + (fallbackBiomeBias * 0.4f));
            }

            float biomeBias = Mathf.Clamp01(Mathf.Max(biomeData.CommonResourceBias, biomeData.UncommonResourceBias) / 5f);
            return Mathf.Clamp01((zoneBias * 0.6f) + (biomeBias * 0.4f));
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

                float fallbackBiomeBias = Mathf.Clamp01(Mathf.Max(fallbackBiome.landmarkStrength, fallbackBiome.rewardPull) / 5f);
                return Mathf.Clamp01((zoneBias * 0.45f) + (fallbackBiomeBias * 0.55f));
            }

            float biomeBias = Mathf.Clamp01(Mathf.Max(biomeData.LandmarkStrength, biomeData.RewardPull) / 5f);
            return Mathf.Clamp01((zoneBias * 0.45f) + (biomeBias * 0.55f));
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
            float resource = Mathf.Clamp01((common * 0.45f) + (uncommon * 0.35f) + (rare * 0.2f));
            float salvageRead = Mathf.Clamp01((salvage * 0.62f) + (node * 0.38f));
            float landmarkRead = Mathf.Clamp01((landmark * 0.64f) + (route * 0.36f));
            float hazardRead = Mathf.Clamp01((survival * 0.58f) + (route * 0.26f) + (rare * 0.16f));
            float shelterRead = Mathf.Clamp01((survival * 0.68f) + (loosePickup * 0.16f) + ((1f - hazardRead) * 0.16f));
            float faunaRead = Mathf.Clamp01((common * 0.34f) + (reward * 0.18f) + ((1f - survival) * 0.48f));

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
            return Mathf.Clamp01(value / 5f);
        }

        private void RegisterMatrixForBake(HectonBiomeMatrixProfile profile)
        {
            if (profile == null || _biomeMatrixDataIndexLookup.ContainsKey(profile))
                return;

            int index = _biomeMatrixBakeList.Count;
            _biomeMatrixDataIndexLookup.Add(profile, index);
            _biomeMatrixBakeList.Add(profile);
            RegisterFamilyForBake(profile.familyProfile);
        }

        private void RegisterFamilyForBake(HectonBiomeFamilyProfile family)
        {
            if (family == null || _biomeFamilyDataIndexLookup.ContainsKey(family))
                return;

            int index = _biomeFamilyBakeList.Count;
            _biomeFamilyDataIndexLookup.Add(family, index);
            _biomeFamilyBakeList.Add(family);
        }

        private static void EnsureNativeArrayCapacity<T>(ref NativeArray<T> array, int requiredCapacity) where T : struct
        {
            if (requiredCapacity <= 0)
            {
                if (!array.IsCreated)
                    array = new NativeArray<T>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                return;
            }

            if (array.IsCreated && array.Length >= requiredCapacity)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<T>(Mathf.NextPowerOfTwo(requiredCapacity), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void DisposeBurstData()
        {
            if (_burstZoneData.IsCreated)
                _burstZoneData.Dispose();
            if (_burstBiomeMatrixData.IsCreated)
                _burstBiomeMatrixData.Dispose();
            if (_burstBiomeFamilyData.IsCreated)
                _burstBiomeFamilyData.Dispose();

            _burstZoneDataCount = 0;
            _burstBiomeMatrixDataCount = 0;
            _burstBiomeFamilyDataCount = 0;
        }

        private bool TryGetZoneData(int zoneDataIndex, out ZoneData zoneData)
        {
            if (_burstZoneData.IsCreated && zoneDataIndex >= 0 && zoneDataIndex < _burstZoneDataCount)
            {
                zoneData = _burstZoneData[zoneDataIndex];
                return true;
            }

            zoneData = default;
            return false;
        }

        private bool TryGetBiomeMatrixData(int biomeMatrixDataIndex, out BiomeMatrixData biomeData)
        {
            if (_burstBiomeMatrixData.IsCreated && biomeMatrixDataIndex >= 0 && biomeMatrixDataIndex < _burstBiomeMatrixDataCount)
            {
                biomeData = _burstBiomeMatrixData[biomeMatrixDataIndex];
                return true;
            }

            biomeData = default;
            return false;
        }

        private bool TryGetBiomeFamilyData(int biomeFamilyDataIndex, out BiomeFamilyData familyData)
        {
            if (_burstBiomeFamilyData.IsCreated && biomeFamilyDataIndex >= 0 && biomeFamilyDataIndex < _burstBiomeFamilyDataCount)
            {
                familyData = _burstBiomeFamilyData[biomeFamilyDataIndex];
                return true;
            }

            familyData = default;
            return false;
        }

        private int ResolveZoneDataIndex(WorldZoneAnchor zone)
        {
            if (zone != null && _zoneDataIndexLookup.TryGetValue(zone, out int zoneDataIndex))
                return zoneDataIndex;

            return -1;
        }

        private int ResolveBiomeMatrixDataIndex(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile != null && _biomeMatrixDataIndexLookup.TryGetValue(biomeProfile, out int biomeMatrixDataIndex))
                return biomeMatrixDataIndex;

            return -1;
        }

        private int ResolveBiomeFamilyDataIndex(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily != null && _biomeFamilyDataIndexLookup.TryGetValue(biomeFamily, out int biomeFamilyDataIndex))
                return biomeFamilyDataIndex;

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
                   mapMagicBridge == null ||
                   worldZoneDirector == null ||
                   biomeMatrixDirector == null;
        }

        private static Vector2Int GetHeightCacheKey(float x, float z)
        {
            return new Vector2Int(
                Mathf.RoundToInt(x * 100f),
                Mathf.RoundToInt(z * 100f));
        }

        private void TrimSeafloorHeightCacheIfNeeded()
        {
            if (_seafloorHeightCache.Count < MaxSeafloorHeightCacheEntries)
                return;

            _seafloorHeightCache.Clear();
        }

        private void TrimBiomeIndexCacheIfNeeded()
        {
            if (_biomeIndexCache.Count < MaxBiomeIndexCacheEntries)
                return;

            _biomeIndexCache.Clear();
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
            {
                _debugBridgeReady = true;
                _debugZoneDirectorReady = true;
                _debugBiomeDirectorReady = true;
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (worldZoneDirector == null)
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref worldZoneDirector);

            if (biomeMatrixDirector == null)
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref biomeMatrixDirector);

            _debugBridgeReady = mapMagicBridge != null;
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
                : SyntheticZoneLabelPrefix + GetZoneKindLabel(sample.resolvedZoneKind);
            _debugLastBiomeProfile = sample.biomeProfile != null ? sample.biomeProfile.biomeName : "None";
            _debugLastBiomeFamily = sample.biomeFamily != null ? sample.biomeFamily.familyLabel : "None";
            _debugLastPattern = sample.isValid ? GetPatternLabel(sample.resolvedPattern) : PatternLabelNone;
            _debugPatternOverride = forcePatternPreviewOverride
                ? limitPatternOverrideToFallback
                    ? $"{previewPatternOverride} (FallbackOnly)"
                    : $"{previewPatternOverride} (Forced)"
                : "None";
            _debugPreviewBiomeOverride = forcePatternPreviewOverride
                ? ResolvePreviewBiomeLabel(ResolvePreviewPatternBiomeFamily(previewPatternOverride, sample.depthMeters, sample.slopeDegrees, sample.biomeFamily))
                : "None";
            _debugPreviewMatrixOverride = forceMatrixBiomePreviewOverride && previewMatrixBiomeOverride != null
                ? limitMatrixBiomeOverrideToFallback
                    ? $"{previewMatrixBiomeOverride.biomeName} (FallbackOnly)"
                    : $"{previewMatrixBiomeOverride.biomeName} (Forced)"
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
                case SeafloorSource.SceneRaycast:
                    return SeafloorSourceRaycastLabel;
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
