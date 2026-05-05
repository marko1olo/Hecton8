using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Ecosystem;
using Hecton8.Systems.AI;
using Hecton8.UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct EcosystemSectorSaveRecord
    {
        public int2 SectorCoord;
        public uint PackedPopulations;
        public uint PackedAdaptation;
    }

    /// <summary>
    /// Cold-path sector ecosystem table. Population is a deterministic cinematic roll per 1 km sector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class EcosystemDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IEcosystemDirectorService
    {
        internal static EcosystemDirector ActiveRuntimeInstance { get; private set; }

        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const float DefaultFloraGrazingSearchRadiusMeters = 2.75f;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float DefaultLeviathanSectorSpawnChance = 0.15f;
        private const int DefaultGrazerPopulationPerSector = 10;
        private const int DefaultLeviathanPopulationPerSector = 1;
        private const int MinimumSectorCapacity = 16;
        private const float DefaultHostilityPeakHoldSeconds = 18f;
        private const float LogicalLodFullSimDistanceMeters = 40f;
        private const float LogicalLodDataOnlyDistanceMeters = 150f;
        private const float ThermalSpawnTemperatureThresholdCelsius = 40f;
        private const float ThermalSpawnDepthThresholdMeters = 2000f;
        private const float LightFalloffDepthMeters = 2500f;
        private const float PredatorDietValidationRadiusMeters = 500f;
        private const float CorpseSpawnInfluenceRadiusMeters = 100f;
        private const float MinimumCorpseDietInfluence01 = 0.001f;
        private const float CorpseSpawnSelectionScale = 2.6f;
        private const int PredatorSpawnValidationHitCapacity = 64;
        private const int HibernationPopulationSyncsPerColdSolve = 8;
        private const int ApexTerritoryOverlapCandidateCapacity = 16;
        private const int ApexTerritoryOverlapHitCapacity = 64;
        private const float ApexTerritoryOverlapQueryRadiusMeters = 1400f;
        private const float ApexTerritoryOverlapRetreatThreshold01 = 0.30f;
        private const int FloraPredatorAupBufferCapacity = 32;
        private const int FloraPredatorAupHitCapacity = 64;
        private const float FloraPredatorAupQueryRadiusMeters = 700f;
        private const float FloraPredatorStealthRadiusMeters = 15f;
        private const float FloraPredatorStealthDimStrength = 0.82f;
        private const string NativeMemoryOwner = nameof(EcosystemDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private static readonly string[] ThermalSpawnTokens = { "lava", "thermal", "brine", "heat", "volcanic", "smoker" };
        private static readonly string[] SharkSpawnTokens = { "shark", "hunter", "stalker" };
        private static readonly string[] ScavengerSpawnTokens = { "scavenger", "crab", "eel", "carrion", "cleaner" };
        private static readonly uint _FloraPredatorAupSaturationWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.FloraPredatorAupSaturation"));
        private static readonly uint _EcosystemDirectorContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute(nameof(EcosystemDirector)));
        // COLD ALLOC: SpatialQueryHit[64] — non-alloc predator diet validation scratch for spawn gating — owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _predatorSpawnValidationHits = new SpatialQueryHit[PredatorSpawnValidationHitCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc Apex territory candidate query scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _apexTerritoryOverlapHits = new SpatialQueryHit[ApexTerritoryOverlapHitCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc flora predator AUP upload query scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _floraPredatorAupHits = new SpatialQueryHit[FloraPredatorAupHitCapacity];
        private static readonly int _PredatorAUPBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int _PredatorAUPCountId = Shader.PropertyToID("_PredatorAUPCount");
        private static readonly int _PredatorAUPParamsId = Shader.PropertyToID("_PredatorAUPParams");
        private static readonly int _BiolumFlashBangAUPId = Shader.PropertyToID("_BiolumFlashBangAUP");
        private static readonly int _BiolumFlashBangParamsId = Shader.PropertyToID("_BiolumFlashBangParams");

        [StructLayout(LayoutKind.Sequential)]
        private struct SectorPopulationState
        {
            public int2 SectorCoord;
            public float PreyPopulation;
            public float PredatorPopulation;
            public float HarvestPressure;
            public float Fitness;
            public float SpeedMultiplier;
            public float CamouflageIndex;
            public int PreyPopulationRounded;
            public int PredatorPopulationRounded;
            public int BiomeId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ApexTerritorySample
        {
            public float3 Position;
            public float Radius;
            public float MassScore;
            public int BrainIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ApexTerritoryOverlapResult
        {
            public int RetreatBrainIndex;
            public int RivalBrainIndex;
            public float Overlap01;
            public float Padding;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ApexTerritoryOverlapJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ApexTerritorySample> Samples;
            public NativeArray<ApexTerritoryOverlapResult> Results;
            public int Count;
            public float OverlapThreshold01;

            public void Execute(int index)
            {
                ApexTerritoryOverlapResult result = default;
                result.RetreatBrainIndex = -1;
                result.RivalBrainIndex = -1;
                if (index < 0 || index >= Count)
                {
                    Results[index] = result;
                    return;
                }

                ApexTerritorySample sample = Samples[index];
                if (sample.Radius <= 0.001f)
                {
                    Results[index] = result;
                    return;
                }

                float bestOverlap01 = 0f;
                int bestRivalIndex = -1;
                for (int otherIndex = 0; otherIndex < Count; otherIndex++)
                {
                    if (otherIndex == index)
                        continue;

                    ApexTerritorySample rival = Samples[otherIndex];
                    if (rival.Radius <= 0.001f)
                        continue;

                    bool sampleIsSmaller =
                        sample.MassScore < rival.MassScore ||
                        (math.abs(sample.MassScore - rival.MassScore) <= 0.001f &&
                         (sample.Radius < rival.Radius ||
                          (math.abs(sample.Radius - rival.Radius) <= 0.001f && sample.BrainIndex > rival.BrainIndex)));
                    if (!sampleIsSmaller)
                        continue;

                    float overlap01 = ComputeSmallerSphereOverlap01(sample.Position, sample.Radius, rival.Position, rival.Radius);
                    if (overlap01 <= OverlapThreshold01 || overlap01 <= bestOverlap01)
                        continue;

                    bestOverlap01 = overlap01;
                    bestRivalIndex = rival.BrainIndex;
                }

                if (bestRivalIndex >= 0)
                {
                    result.RetreatBrainIndex = sample.BrainIndex;
                    result.RivalBrainIndex = bestRivalIndex;
                    result.Overlap01 = bestOverlap01;
                }

                Results[index] = result;
            }

            private static float ComputeSmallerSphereOverlap01(float3 positionA, float radiusA, float3 positionB, float radiusB)
            {
                float safeRadiusA = math.max(0.001f, radiusA);
                float safeRadiusB = math.max(0.001f, radiusB);
                float centerDistance = math.distance(positionA, positionB);
                float smallerRadius = math.min(safeRadiusA, safeRadiusB);
                float smallerVolume = SphereVolume(smallerRadius);
                if (smallerVolume <= 0.0001f)
                    return 0f;

                if (centerDistance >= safeRadiusA + safeRadiusB)
                    return 0f;

                if (centerDistance <= math.abs(safeRadiusA - safeRadiusB))
                    return 1f;

                float sum = safeRadiusA + safeRadiusB;
                float diff = safeRadiusA - safeRadiusB;
                float cap = sum - centerDistance;
                float numerator = math.PI * cap * cap *
                                  (centerDistance * centerDistance + 2f * centerDistance * sum - 3f * diff * diff);
                float overlapVolume = numerator / math.max(0.001f, 12f * centerDistance);
                return math.saturate(overlapVolume / smallerVolume);
            }

            private static float SphereVolume(float radius)
            {
                return (4f * math.PI * radius * radius * radius) * (1f / 3f);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProbabilityTablePopulationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> FrontStates;
            public NativeArray<SectorPopulationState> BackStates;
            public float LeviathanChance01;
            public float BaselineFitness;
            public int GrazerPopulationPerSector;
            public int LeviathanPopulationPerSector;
            public int MaxPreyPopulation;
            public int MaxPredatorPopulation;

            public void Execute(int index)
            {
                SectorPopulationState state = FrontStates[index];
                ResolveProbabilityTablePopulation(
                    state.SectorCoord,
                    state.BiomeId,
                    LeviathanChance01,
                    GrazerPopulationPerSector,
                    LeviathanPopulationPerSector,
                    out int preyPopulation,
                    out int predatorPopulation);

                preyPopulation = math.clamp(preyPopulation, 0, math.max(0, MaxPreyPopulation));
                predatorPopulation = math.clamp(predatorPopulation, 0, math.max(0, MaxPredatorPopulation));
                state.PreyPopulation = preyPopulation;
                state.PredatorPopulation = predatorPopulation;
                state.HarvestPressure = 0f;
                state.Fitness = math.saturate(BaselineFitness);
                state.SpeedMultiplier = 1f;
                state.CamouflageIndex = 0f;
                state.PreyPopulationRounded = preyPopulation;
                state.PredatorPopulationRounded = predatorPopulation;
                BackStates[index] = state;
            }
        }

        [Header("Sector Runtime")]
        [Tooltip("Maximum number of active 1 km sectors tracked in the cold-path population model.")]
        [SerializeField, Min(MinimumSectorCapacity)] private int maxTrackedSectors = 128;
        [Tooltip("Seconds between ecosystem solves. 10 seconds = 0.1 Hz.")]
        [SerializeField, Min(1f)] private float coldTickIntervalSeconds = 60f;

        [Header("Probability Table")]
        [Tooltip("Deterministic 1 km sector roll chance that the sector presents one leviathan instead of a grazer pod.")]
        [SerializeField, Range(0f, 1f)] private float leviathanSectorSpawnChance = DefaultLeviathanSectorSpawnChance;
        [Tooltip("Fixed grazer count presented when the sector roll is not leviathan.")]
        [SerializeField, Min(0)] private int grazerPopulationPerSector = DefaultGrazerPopulationPerSector;
        [Tooltip("Fixed leviathan count presented when the sector roll hits the apex bucket.")]
        [SerializeField, Min(0)] private int leviathanPopulationPerSector = DefaultLeviathanPopulationPerSector;
        [Tooltip("Upper clamp for prey population values exposed to hibernation and spawn reconciliation.")]
        [SerializeField, Min(1)] private int maxPreyPopulation = DefaultGrazerPopulationPerSector;
        [Tooltip("Upper clamp for predator population values exposed to hibernation and spawn reconciliation.")]
        [SerializeField, Min(1)] private int maxPredatorPopulation = DefaultLeviathanPopulationPerSector;
        [Tooltip("Baseline prey fitness retained only for serialized compatibility; the purge table does not evolve it over time.")]
        [SerializeField, Range(0f, 1f)] private float baselineFitness = 0.08f;
        [Tooltip("Legacy save decode clamp for pre-purge sector adaptation payloads.")]
        [SerializeField, Min(1f)] private float maximumSpeedMultiplier = 1.35f;

        [Header("Biome Hostility")]
        [Tooltip("Normalized hostility applied when the player kills one standard apex predator.")]
        [SerializeField, Range(0.01f, 1f)] private float hostilityPerApexKill = 0.22f;
        [Tooltip("Normalized hostility removed per SlowTick while the biome is calming down.")]
        [SerializeField, Range(0.001f, 0.2f)] private float hostilityDecayPerSlowTick = 0.015f;
        [Tooltip("Minimum director peak-hold duration injected when hostility is elevated.")]
        [SerializeField, Min(0f)] private float hostilityPeakHoldSeconds = DefaultHostilityPeakHoldSeconds;

        [Header("Predator Starvation")]
        [Tooltip("Comfort prey-per-predator ratio. Values below this drive desperation pressure upward.")]
        [SerializeField, Min(1f)] private float starvationComfortPreyPerPredator = 12f;
        [Tooltip("Additional starvation pressure sourced from elevated sector harvest pressure.")]
        [SerializeField, Min(0f)] private float starvationHarvestWeight = 0.65f;
        [Tooltip("Scale applied when converting starvation pressure into director hostility.")]
        [SerializeField, Range(0f, 1f)] private float starvationHostilityWeight = 0.85f;

        [Header("Eclipse Predator Migration")]
        [SerializeField, Min(0f)] private float eclipsePredatorTier0DepthMaxMeters = 40f;
        [SerializeField, Range(0f, 40f)] private float eclipsePredatorTier0TargetDepthMeters = 24f;
        [SerializeField, Range(0f, 1f)] private float eclipsePredatorLightSuppression = 1f;
        [SerializeField, Range(0f, 1f)] private float eclipsePredatorHostilityBoost = 0.35f;
        [SerializeField, Min(1f)] private float eclipsePredatorMigrationRadiusMeters = 1800f;
        [SerializeField, Min(1f)] private float eclipsePredatorMigrationStepMeters = 320f;
        [SerializeField, Min(0.5f)] private float eclipsePredatorMigrationIntervalSeconds = 2f;
        [SerializeField, Range(1f, 6f)] private float eclipsePredatorTier0SelectionBoost = 3f;
        [SerializeField] private float _debugEclipsePredatorMigrationTimeRemaining;
        [SerializeField] private float _debugEclipsePredatorMigrationIntensity;
        [SerializeField] private int _debugEclipsePredatorMigratedCount;

        [Header("Fauna Ecology Chains")]
        [Tooltip("Species IDs treated as herbivores for flora grazing and migration redirection.")]
        [SerializeField] private int[] herbivoreSpeciesIds;
        [Tooltip("Species IDs treated as cleaners that shadow apex fauna and reduce fatigue.")]
        [SerializeField] private int[] cleanerSpeciesIds;
        [Tooltip("Optional apex species IDs that accept cleaner symbiosis. Empty means any leviathan host.")]
        [SerializeField] private int[] cleanerHostSpeciesIds;
        [Tooltip("Hunger threshold that allows herbivore flora seeking to override default wander logic.")]
        [SerializeField, Range(0f, 1f)] private float herbivoreGrazeHungerThreshold = 0.68f;
        [Tooltip("Search radius used when resolving nearby consumable flora for hungry herbivores.")]
        [SerializeField, Min(1f)] private float herbivoreGrazeSearchRadiusMeters = 500f;
        [Tooltip("Distance where herbivores consume the resolved flora instance.")]
        [SerializeField, Min(0.1f)] private float herbivoreConsumeDistanceMeters = 6f;
        [Tooltip("Search radius used by cleaner species when acquiring an apex host to orbit.")]
        [SerializeField, Min(1f)] private float cleanerHostSearchRadiusMeters = 96f;
        [Tooltip("Distance where cleaner fish begin to apply fatigue relief to the host.")]
        [SerializeField, Min(0.1f)] private float cleanerSymbiosisDistanceMeters = 10f;
        [Tooltip("Normalized fatigue relief applied per second while a cleaner remains in symbiotic range.")]
        [SerializeField, Min(0f)] private float cleanerFatigueReliefPerSecond = 0.09f;
        [Tooltip("Hunger threshold that allows scavenger corpse feeding to override default hunt logic.")]
        [SerializeField, Range(0f, 1f)] private float scavengerHungerThreshold = 0.55f;
        [Tooltip("Search radius used when resolving active corpse-resource nodes for scavengers.")]
        [SerializeField, Min(1f)] private float scavengerCorpseSearchRadiusMeters = 240f;
        [Tooltip("Distance where scavengers begin consuming the corpse-resource node.")]
        [SerializeField, Min(0.1f)] private float scavengerConsumeDistanceMeters = 6f;
        [Tooltip("Units per second removed from a corpse-resource node while a scavenger is feeding.")]
        [SerializeField, Min(0.01f)] private float scavengerConsumeUnitsPerSecond = 1.2f;
        [Tooltip("Distance where dropped organic bait locks fauna into a local feeding investigate/sated loop.")]
        [SerializeField, Min(0.1f)] private float baitFeedingDistanceMeters = 5f;

        private NativeArray<SectorPopulationState> _sectorFrontStates;
        private NativeArray<SectorPopulationState> _sectorBackStates;
        private NativeHashMap<long, int> _sectorIndexByKey;
        private NativeArray<ApexTerritorySample> _apexTerritorySamples;
        private NativeArray<ApexTerritoryOverlapResult> _apexTerritoryOverlapResults;
        private NativeArray<float4> _floraPredatorAupUpload;
        private NativeList<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private FaunaBrain[] _apexTerritoryBrains;
        private GraphicsBuffer _floraPredatorAupBuffer;
        private JobHandle _scheduledSolveHandle;
        private JobHandle _scheduledApexTerritoryOverlapHandle;
        private float _coldTickAccumulator;
        private int _activeSectorCount;
        private int _scheduledApexTerritoryOverlapCount;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _solveScheduled;
        private bool _apexTerritoryOverlapScheduled;
        private bool _populationSolvePendingHibernationSync;
        private float _biomeHostility01;
        private float _starvationAggressionPressure01;
        private int _hostilityTier;
        private bool _floraPredatorAupSaturationTelemetryIssued;
        private int _nextHibernationPopulationSyncIndex;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private PersistentWorldRegistry _cachedPersistentWorldRegistry;
        private float _eclipsePredatorMigrationTimer;
        private float _eclipsePredatorMigrationIntensity01;
        private float _eclipsePredatorMigrationAccumulator;

        /// <summary>
        /// True once the runtime-native state is allocated and registered.
        /// </summary>
        public bool IsInitialized => _sectorFrontStates.IsCreated && _sectorBackStates.IsCreated && _sectorIndexByKey.IsCreated;

        /// <summary>
        /// Normalized biome hostility score exposed to UI and pacing systems.
        /// </summary>
        public float BiomeHostility01 => ResolveCombinedHostility01();

        internal FaunaLogicalLodTier ResolveLogicalLodTier(Vector3 observerPosition, Vector3 faunaPosition)
        {
            AbsoluteUniversePosition observerAup = AbsoluteUniversePosition.FromRuntimePosition(observerPosition);
            AbsoluteUniversePosition faunaAup = AbsoluteUniversePosition.FromRuntimePosition(faunaPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in faunaAup);

            if (distanceSq < (LogicalLodFullSimDistanceMeters * LogicalLodFullSimDistanceMeters))
                return FaunaLogicalLodTier.FullSim;

            if (distanceSq <= (LogicalLodDataOnlyDistanceMeters * LogicalLodDataOnlyDistanceMeters))
                return FaunaLogicalLodTier.DataOnly;

            return FaunaLogicalLodTier.Hibernating;
        }

        internal bool TryBuildEnvelope(Vector3 worldPosition, out EcosystemEnvelope envelope)
        {
            float depthMeters = 0f;
            MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;
            if (mapMagicBridge != null)
                depthMeters = math.max(0f, mapMagicBridge.WaterSurfaceLevel - worldPosition.y);

            ResolveRuntimeReferences();

            float temperatureCelsius = _cachedVegetationBridge != null
                ? _cachedVegetationBridge.GetWaterTemperature(worldPosition)
                : 15f;

            float4 normalizedChannels;
            if (!ChemicalInfluenceGrid.TrySampleNormalizedChannels(worldPosition, out normalizedChannels))
                normalizedChannels = float4.zero;

            float lightExposure01 = math.saturate(1f - (depthMeters / math.max(1f, LightFalloffDepthMeters)));
            float eclipseSuppression01 = ResolveEclipsePredatorLightSuppression01(worldPosition);
            if (eclipseSuppression01 > 0f)
                lightExposure01 *= 1f - eclipseSuppression01;

            envelope = new EcosystemEnvelope(
                temperatureCelsius,
                depthMeters,
                lightExposure01,
                normalizedChannels.x,
                normalizedChannels.y,
                normalizedChannels.z,
                ResolveCombinedHostility01());
            return true;
        }

        internal bool TryResolveSpawnWeightMultiplier(CreatureArchetypeData archetype, Vector3 worldPosition, out float selectionMultiplier)
        {
            selectionMultiplier = 1f;
            if (archetype == null)
                return true;

            if (IsPredatorOrApex(archetype) &&
                !CanSupportPredatorSpawn(archetype, worldPosition, PredatorDietValidationRadiusMeters))
            {
                return false;
            }

            TryBuildEnvelope(worldPosition, out EcosystemEnvelope envelope);
            if (RequiresThermalEnvelope(archetype) &&
                (envelope.TemperatureCelsius < ThermalSpawnTemperatureThresholdCelsius ||
                 envelope.DepthMeters < ThermalSpawnDepthThresholdMeters))
            {
                return false;
            }

            float scentPressure01 = math.saturate(math.max(envelope.BloodScent01, envelope.FearScent01));
            if (IsSharkLikePredator(archetype))
            {
                selectionMultiplier = math.lerp(0.65f, 1.85f, scentPressure01);
                if (TryResolveEclipsePredatorTier0SelectionBoost(worldPosition, out float sharkEclipseSelectionBoost))
                    selectionMultiplier *= sharkEclipseSelectionBoost;

                return selectionMultiplier > 0f;
            }

            if (archetype.isAggressive || archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan)
            {
                selectionMultiplier = math.lerp(0.9f, 1.35f, scentPressure01);
            }

            if (IsPredatorOrApex(archetype) &&
                TryResolveEclipsePredatorTier0SelectionBoost(worldPosition, out float eclipseSelectionBoost))
            {
                selectionMultiplier *= eclipseSelectionBoost;
            }

            if (RespondsToCorpseFalls(archetype))
            {
                float corpseInfluence01 = ResolveCombinedCorpseSpawnInfluence01(worldPosition, CorpseSpawnInfluenceRadiusMeters);
                if (corpseInfluence01 > 0f)
                    selectionMultiplier *= math.lerp(1f, CorpseSpawnSelectionScale, corpseInfluence01);
            }

            return selectionMultiplier > 0f;
        }

        internal bool CanSupportPredatorSpawn(CreatureArchetypeData archetype, Vector3 worldPosition, float searchRadiusMeters)
        {
            if (archetype == null || !IsPredatorOrApex(archetype))
                return true;

            FaunaDataTemplate faunaDataTemplate = archetype.faunaDataTemplate;
            uint dietMaskBits = faunaDataTemplate != null ? faunaDataTemplate.DietMaskBits : 0u;
            if (dietMaskBits == 0u)
                return false;

            if ((dietMaskBits & (uint)FaunaDietMask.Carcass) != 0u &&
                ResolveCombinedCorpseSpawnInfluence01(worldPosition, CorpseSpawnInfluenceRadiusMeters) > MinimumCorpseDietInfluence01)
            {
                return true;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                worldPosition,
                searchRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorSpawnValidationHits);

            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = _predatorSpawnValidationHits[i];
                FaunaBrain preyBrain = hit.Owner as FaunaBrain;
                if (preyBrain == null &&
                    (hit.Transform == null || !hit.Transform.TryGetComponent(out preyBrain)))
                {
                    continue;
                }

                FaunaDataTemplate preyDataTemplate = preyBrain.DataTemplate;
                uint preyMaskBits = preyDataTemplate != null ? preyDataTemplate.PreyMaskBits : 0u;
                if (preyMaskBits != 0u && (dietMaskBits & preyMaskBits) != 0u)
                    return true;
            }

            return false;
        }

        internal bool IsHerbivoreSpecies(int speciesId)
        {
            return ContainsSpeciesId(herbivoreSpeciesIds, speciesId);
        }

        internal bool IsCleanerSpecies(int speciesId)
        {
            return ContainsSpeciesId(cleanerSpeciesIds, speciesId);
        }

        internal bool IsCleanerHostSpecies(FaunaBrain hostBrain)
        {
            if (hostBrain == null)
                return false;

            int speciesId = hostBrain.SpeciesId;
            if (cleanerHostSpeciesIds != null && cleanerHostSpeciesIds.Length > 0)
                return ContainsSpeciesId(cleanerHostSpeciesIds, speciesId);

            return hostBrain.SpeciesProfile != null && hostBrain.SpeciesProfile.isLeviathan;
        }

        internal float HerbivoreGrazeHungerThreshold => herbivoreGrazeHungerThreshold;
        internal float HerbivoreGrazeSearchRadiusMeters => herbivoreGrazeSearchRadiusMeters;
        internal float HerbivoreConsumeDistanceMeters => herbivoreConsumeDistanceMeters;
        internal float CleanerHostSearchRadiusMeters => cleanerHostSearchRadiusMeters;
        internal float CleanerSymbiosisDistanceMeters => cleanerSymbiosisDistanceMeters;
        internal float CleanerFatigueReliefPerSecond => cleanerFatigueReliefPerSecond;
        internal float ScavengerHungerThreshold => scavengerHungerThreshold;
        internal float ScavengerConsumeDistanceMeters => scavengerConsumeDistanceMeters;
        internal float ScavengerConsumeUnitsPerSecond => scavengerConsumeUnitsPerSecond;
        internal float BaitFeedingDistanceMeters => baitFeedingDistanceMeters;

        internal bool TryResolveHerbivoreGrazeTarget(Vector3 worldPosition, out Vector3 floraPosition, out uint floraInstanceUid)
        {
            floraPosition = default;
            floraInstanceUid = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestConsumableFlora(worldPosition, herbivoreGrazeSearchRadiusMeters, out floraPosition, out floraInstanceUid);
        }

        internal bool TryConsumeHerbivoreGrazeTarget(uint floraInstanceUid)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null && organicManager.TryConsumeFlora(floraInstanceUid);
        }

        internal bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            return MigrationDirector.TryResolveMigrationTarget(speciesId, origin, out target);
        }

        internal bool TryResolveNearestThermalVentAttractor(
            in AbsoluteUniversePosition queryAup,
            float searchRadiusMeters,
            out Vector3 target,
            out float heat01)
        {
            target = default;
            heat01 = 0f;
            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            return thermalManager != null &&
                   thermalManager.TryResolveNearestActiveVentAttractor(in queryAup, searchRadiusMeters, out target, out heat01);
        }

        internal void RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits);
        }

        internal void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits);
        }

        internal bool TryResolveCorpseScavengeTarget(Vector3 worldPosition, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(worldPosition, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        internal bool TryResolveCorpseScavengeTarget(in AbsoluteUniversePosition queryAup, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(in queryAup, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        internal bool TryConsumeCorpseScavengeTarget(uint corpseNodeId, float consumeUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null && organicManager.TryConsumeCorpseResourceNode(corpseNodeId, consumeUnits);
        }

        internal bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveCorpseDiseaseExposure(in queryAup, currentTimeSeconds, out severity01, out sourcePosition);
        }

        internal bool TryResolveNearestOrganicMass(Vector3 worldPosition, out Vector3 organicPosition)
        {
            organicPosition = default;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            float searchRadius = math.max(scavengerCorpseSearchRadiusMeters, herbivoreGrazeSearchRadiusMeters);
            bool found = false;
            float bestDistanceSq = float.MaxValue;
            if (organicManager.TryResolveNearestCorpseResourceNode(worldPosition, searchRadius, out Vector3 corpsePosition, out _))
            {
                organicPosition = corpsePosition;
                bestDistanceSq = (corpsePosition - worldPosition).sqrMagnitude;
                found = true;
            }

            if (organicManager.TryResolveNearestConsumableFlora(worldPosition, searchRadius, out Vector3 floraPosition, out _))
            {
                float floraDistanceSq = (floraPosition - worldPosition).sqrMagnitude;
                if (floraDistanceSq < bestDistanceSq)
                {
                    organicPosition = floraPosition;
                    bestDistanceSq = floraDistanceSq;
                    found = true;
                }
            }

            return found;
        }

        internal bool TryResolveNearestOrganicMass(in AbsoluteUniversePosition queryAup, out Vector3 organicPosition)
        {
            organicPosition = default;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            Vector3 worldPosition = queryAup.ToRuntimeFloat3();
            float searchRadius = math.max(scavengerCorpseSearchRadiusMeters, herbivoreGrazeSearchRadiusMeters);
            bool found = false;
            float bestDistanceSq = float.MaxValue;
            if (organicManager.TryResolveNearestCorpseResourceNode(in queryAup, searchRadius, out Vector3 corpsePosition, out _))
            {
                organicPosition = corpsePosition;
                bestDistanceSq = (corpsePosition - worldPosition).sqrMagnitude;
                found = true;
            }

            if (organicManager.TryResolveNearestConsumableFlora(worldPosition, searchRadius, out Vector3 floraPosition, out _))
            {
                float floraDistanceSq = (floraPosition - worldPosition).sqrMagnitude;
                if (floraDistanceSq < bestDistanceSq)
                {
                    organicPosition = floraPosition;
                    found = true;
                }
            }

            return found;
        }

        internal bool TryConsumeOrganicMassAtPosition(Vector3 worldPosition, float searchRadius)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            float safeSearchRadius = math.max(0.1f, searchRadius);
            if (organicManager.TryConsumeFloraAtPosition(worldPosition, safeSearchRadius, out _))
                return true;

            if (!organicManager.TryResolveNearestCorpseResourceNode(worldPosition, safeSearchRadius, out _, out uint corpseNodeId))
                return false;

            return organicManager.TryConsumeCorpseResourceNode(corpseNodeId, scavengerConsumeUnitsPerSecond);
        }

        internal void PublishBiolumFlashBang(in AbsoluteUniversePosition flashAup, float currentTimeSeconds, float radiusMeters = 42f)
        {
            Vector3 flashPosition = flashAup.ToRuntimeFloat3();
            Shader.SetGlobalVector(_BiolumFlashBangAUPId, new Vector4(flashPosition.x, flashPosition.y, flashPosition.z, Mathf.Max(0.1f, radiusMeters)));
            Shader.SetGlobalVector(_BiolumFlashBangParamsId, new Vector4(currentTimeSeconds, 0.1f, 4f, 0f));
        }

        internal bool DoesSpeciesRespondToBait(FaunaBrain faunaBrain)
        {
            if (faunaBrain == null || faunaBrain.SpeciesProfile == null)
                return false;

            int speciesId = faunaBrain.SpeciesId;
            return faunaBrain.SpeciesProfile.isScavenger ||
                   IsHerbivoreSpecies(speciesId) ||
                   (faunaBrain.isAggressive && !faunaBrain.SpeciesProfile.isLeviathan);
        }

        internal bool IsApexTombstoned(uint uniqueInstanceUid)
        {
            ResolveRuntimeReferences();
            return _cachedPersistentWorldRegistry != null && _cachedPersistentWorldRegistry.IsTombstoned(uniqueInstanceUid);
        }

        internal void RegisterApexPredatorKill(uint uniqueInstanceUid, Vector3 worldPosition, float hostilityDelta)
        {
            ResolveRuntimeReferences();
            _cachedPersistentWorldRegistry?.TryRegisterFaunaTombstone(uniqueInstanceUid);
            if (_cachedPersistentWorldRegistry != null)
            {
                AbsoluteUniversePosition whaleFallAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
                _cachedPersistentWorldRegistry.TryCacheWhaleFallPoiState(uniqueInstanceUid, unchecked((int)(uniqueInstanceUid & 0x00FFFFFFu)), in whaleFallAup, Time.time);
            }

            ReportApexPredatorKilled(worldPosition, hostilityDelta);
        }

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotSectors.IsCreated)
                return;

            _saveSnapshotSectors.Clear();
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            SyncPendingHibernatedFaunaPopulationRecords();
            for (int sectorIndex = 0; sectorIndex < _activeSectorCount; sectorIndex++)
            {
                SectorPopulationState state = _sectorFrontStates[sectorIndex];
                _saveSnapshotSectors.AddNoResize(new EcosystemSectorSaveRecord
                {
                    SectorCoord = state.SectorCoord,
                    PackedPopulations = PackPopulationCounts(state.PreyPopulationRounded, state.PredatorPopulationRounded),
                    PackedAdaptation = PackAdaptationTraits(state.Fitness, state.SpeedMultiplier, state.CamouflageIndex, maximumSpeedMultiplier)
                });
            }
        }

        internal NativeArray<EcosystemSectorSaveRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotSectors.IsCreated ? _saveSnapshotSectors.AsArray() : default;
        }

        internal unsafe void RestoreFromLoadedRecords(EcosystemSectorSaveRecord[] loadedRecords)
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            _sectorIndexByKey.Clear();
            _coldTickAccumulator = 0f;
            _solveScheduled = false;
            _scheduledSolveHandle = default;

            if (_sectorFrontStates.IsCreated)
            {
                void* frontPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_sectorFrontStates);
                UnsafeUtility.MemClear(frontPtr, _sectorFrontStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            if (_sectorBackStates.IsCreated)
            {
                void* backPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_sectorBackStates);
                UnsafeUtility.MemClear(backPtr, _sectorBackStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            int recordCount = loadedRecords != null ? math.min(loadedRecords.Length, _sectorFrontStates.Length) : 0;
            _activeSectorCount = 0;
            for (int sectorIndex = 0; sectorIndex < recordCount; sectorIndex++)
            {
                EcosystemSectorSaveRecord saveRecord = loadedRecords[sectorIndex];
                int biomeId = ResolveBiomeIdForSector(saveRecord.SectorCoord);
                ResolveProbabilityTablePopulation(
                    saveRecord.SectorCoord,
                    biomeId,
                    leviathanSectorSpawnChance,
                    grazerPopulationPerSector,
                    leviathanPopulationPerSector,
                    out int preyPopulation,
                    out int predatorPopulation);
                SectorPopulationState restoredState = new SectorPopulationState
                {
                    SectorCoord = saveRecord.SectorCoord,
                    PreyPopulation = preyPopulation,
                    PredatorPopulation = predatorPopulation,
                    HarvestPressure = 0f,
                    Fitness = baselineFitness,
                    SpeedMultiplier = 1f,
                    CamouflageIndex = 0f,
                    PreyPopulationRounded = preyPopulation,
                    PredatorPopulationRounded = predatorPopulation,
                    BiomeId = biomeId
                };

                _sectorFrontStates[sectorIndex] = restoredState;
                _sectorBackStates[sectorIndex] = restoredState;
                _sectorIndexByKey.TryAdd(PackSectorKey(saveRecord.SectorCoord), sectorIndex);
                _activeSectorCount++;
            }
        }

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterService();
            DisposeRuntimeState();
        }

        /// <summary>
        /// Explicit bootstrap registration pass for headless/data-only simulation.
        /// </summary>
        internal void InitializeService()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        /// <summary>
        /// Advances the sector population solve at 0.1 Hz using a Burst job.
        /// </summary>
        public void SlowTick()
        {
            if (!IsInitialized)
                return;

            DecayBiomeHostility();
            SyncPendingHibernatedFaunaPopulationRecords();
            EnsurePlayerSectorRegistered();
            TickEclipsePredatorShallowMigration(DefaultSlowTickIntervalSeconds);
            if (TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
            {
                PublishFloraPredatorAupBuffer(playerPosition);
                ScheduleApexTerritoryOverlap(playerPosition);
            }
            else
            {
                Shader.SetGlobalInt(_PredatorAUPCountId, 0);
            }

            _coldTickAccumulator += DefaultSlowTickIntervalSeconds;
            if (_coldTickAccumulator >= coldTickIntervalSeconds)
            {
                if (HasPendingSimulationJob())
                    return;

                _coldTickAccumulator -= coldTickIntervalSeconds;
                SyncPendingHibernatedFaunaPopulationRecords();
                ScheduleSectorSolve();
            }
        }

        public void LateFrameTick()
        {
            CompleteScheduledSimulation(forceComplete: false);
            CompleteScheduledApexTerritoryOverlap(forceComplete: false);
        }

        /// <summary>
        /// Resolves predator/prey counts for the 1 km sector containing the supplied world position.
        /// </summary>
        public bool TryGetSectorPopulation(Vector3 worldPosition, out EcosystemSectorPopulationSample sample)
        {
            sample = default;
            if (!IsInitialized)
                return false;

            if (HasPendingSimulationJob())
                return false;

            int2 sectorCoord = QuantizeSector(worldPosition);
            int slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
            if (slotIndex < 0)
                return false;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            sample.SectorX = state.SectorCoord.x;
            sample.SectorZ = state.SectorCoord.y;
            sample.PreyPopulation = state.PreyPopulationRounded;
            sample.PredatorPopulation = state.PredatorPopulationRounded;
            sample.Fitness = state.Fitness;
            sample.SpeedMultiplier = state.SpeedMultiplier;
            sample.CamouflageIndex = state.CamouflageIndex;
            return true;
        }

        /// <summary>
        /// Registers visible prey consumption for strain only. Sector population is fixed by the cinematic table.
        /// </summary>
        public void ReportPredation(Vector3 worldPosition, int preyConsumed)
        {
            if (!IsInitialized || preyConsumed <= 0)
                return;

            GlobalRegistry.EnvironmentalStrain?.AccumulatePredationStrain(worldPosition, preyConsumed);
        }

        /// <summary>
        /// Applies one herbivore flora-consumption event at the supplied world position and mirrors it into the sector prey-pressure solve.
        /// </summary>
        internal bool TryReportFloraGrazing(Vector3 worldPosition, float searchRadiusMeters = DefaultFloraGrazingSearchRadiusMeters)
        {
            if (!IsInitialized)
                return false;

            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null ||
                !organicManager.TryConsumeFloraAtPosition(worldPosition, Mathf.Max(0.5f, searchRadiusMeters), out _))
            {
                return false;
            }

            ReportPredation(worldPosition, 1);
            return true;
        }

        /// <summary>
        /// Registers one player-attributed apex predator kill and escalates biome hostility.
        /// </summary>
        public void ReportApexPredatorKilled(Vector3 worldPosition, float hostilityDelta)
        {
            if (!IsInitialized)
                return;

            float appliedDelta = math.max(hostilityPerApexKill, hostilityDelta);
            SetBiomeHostility(_biomeHostility01 + appliedDelta);
            ApplyDirectorHostilityPressure();
        }

        /// <summary>
        /// Opens a cold-path eclipse migration window for large predators and clears it when intensity or hold time reaches zero.
        /// </summary>
        public void ApplyEclipsePredatorShallowMigration(float intensity01, float holdSeconds)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0f || holdSeconds <= 0f)
            {
                _eclipsePredatorMigrationTimer = 0f;
                _eclipsePredatorMigrationIntensity01 = 0f;
                _eclipsePredatorMigrationAccumulator = 0f;
                _debugEclipsePredatorMigrationTimeRemaining = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationTimer = math.max(_eclipsePredatorMigrationTimer, holdSeconds);
            _eclipsePredatorMigrationIntensity01 = math.max(_eclipsePredatorMigrationIntensity01, clampedIntensity);
            _eclipsePredatorMigrationAccumulator = math.max(
                _eclipsePredatorMigrationAccumulator,
                eclipsePredatorMigrationIntervalSeconds);
            _debugEclipsePredatorMigrationTimeRemaining = _eclipsePredatorMigrationTimer;
            _debugEclipsePredatorMigrationIntensity = _eclipsePredatorMigrationIntensity01;

            SetBiomeHostility(math.max(_biomeHostility01, clampedIntensity * eclipsePredatorHostilityBoost));
            ApplyDirectorHostilityPressure();
        }

        /// <summary>
        /// Suppresses predator light reaction in the upper forty meters during eclipse migration.
        /// </summary>
        public float ResolveEclipsePredatorLightSuppression01(Vector3 worldPosition)
        {
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
                return 0f;

            float depthMeters = ResolveDepthMeters(worldPosition);
            if (depthMeters > eclipsePredatorTier0DepthMaxMeters)
                return 0f;

            return math.saturate(math.lerp(
                _eclipsePredatorMigrationIntensity01,
                1f,
                eclipsePredatorLightSuppression));
        }

        private void SanitizeSettings()
        {
            maxTrackedSectors = math.max(MinimumSectorCapacity, maxTrackedSectors);
            coldTickIntervalSeconds = math.max(1f, coldTickIntervalSeconds);
            leviathanSectorSpawnChance = math.saturate(leviathanSectorSpawnChance);
            grazerPopulationPerSector = math.max(0, grazerPopulationPerSector);
            leviathanPopulationPerSector = math.max(0, leviathanPopulationPerSector);
            maxPreyPopulation = math.max(math.max(1, grazerPopulationPerSector), maxPreyPopulation);
            maxPredatorPopulation = math.max(math.max(1, leviathanPopulationPerSector), maxPredatorPopulation);
            baselineFitness = math.clamp(baselineFitness, 0f, 1f);
            maximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            hostilityPerApexKill = math.clamp(hostilityPerApexKill, 0.01f, 1f);
            hostilityDecayPerSlowTick = math.clamp(hostilityDecayPerSlowTick, 0.001f, 0.2f);
            hostilityPeakHoldSeconds = math.max(0f, hostilityPeakHoldSeconds);
            starvationComfortPreyPerPredator = math.max(1f, starvationComfortPreyPerPredator);
            starvationHarvestWeight = math.max(0f, starvationHarvestWeight);
            starvationHostilityWeight = math.clamp(starvationHostilityWeight, 0f, 1f);
            eclipsePredatorTier0DepthMaxMeters = math.max(0f, eclipsePredatorTier0DepthMaxMeters);
            eclipsePredatorTier0TargetDepthMeters = math.clamp(
                eclipsePredatorTier0TargetDepthMeters,
                0f,
                math.max(0f, eclipsePredatorTier0DepthMaxMeters));
            eclipsePredatorLightSuppression = math.clamp(eclipsePredatorLightSuppression, 0f, 1f);
            eclipsePredatorHostilityBoost = math.clamp(eclipsePredatorHostilityBoost, 0f, 1f);
            eclipsePredatorMigrationRadiusMeters = math.max(1f, eclipsePredatorMigrationRadiusMeters);
            eclipsePredatorMigrationStepMeters = math.max(1f, eclipsePredatorMigrationStepMeters);
            eclipsePredatorMigrationIntervalSeconds = math.max(0.5f, eclipsePredatorMigrationIntervalSeconds);
            eclipsePredatorTier0SelectionBoost = math.max(1f, eclipsePredatorTier0SelectionBoost);
            herbivoreGrazeHungerThreshold = math.clamp(herbivoreGrazeHungerThreshold, 0f, 1f);
            herbivoreGrazeSearchRadiusMeters = math.max(1f, herbivoreGrazeSearchRadiusMeters);
            herbivoreConsumeDistanceMeters = math.max(0.1f, herbivoreConsumeDistanceMeters);
            cleanerHostSearchRadiusMeters = math.max(1f, cleanerHostSearchRadiusMeters);
            cleanerSymbiosisDistanceMeters = math.max(0.1f, cleanerSymbiosisDistanceMeters);
            cleanerFatigueReliefPerSecond = math.max(0f, cleanerFatigueReliefPerSecond);
            scavengerHungerThreshold = math.clamp(scavengerHungerThreshold, 0f, 1f);
            scavengerCorpseSearchRadiusMeters = math.max(1f, scavengerCorpseSearchRadiusMeters);
            scavengerConsumeDistanceMeters = math.max(0.1f, scavengerConsumeDistanceMeters);
            scavengerConsumeUnitsPerSecond = math.max(0.01f, scavengerConsumeUnitsPerSecond);
            baitFeedingDistanceMeters = math.max(0.1f, baitFeedingDistanceMeters);
        }

        private void ResolveRuntimeReferences()
        {
            _cachedVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
        }

        private static bool RequiresThermalEnvelope(CreatureArchetypeData archetype)
        {
            return MatchesAnyToken(archetype.creatureId, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.gameplayPurpose, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.behaviorTreeHint, ThermalSpawnTokens);
        }

        private static bool IsPredatorOrApex(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return archetype.isAggressive ||
                   archetype.roleType == CreatureRoleType.Hunter ||
                   archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static bool IsSharkLikePredator(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return MatchesAnyToken(archetype.creatureId, SharkSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, SharkSpawnTokens) ||
                   (archetype.isAggressive && archetype.roleType == CreatureRoleType.Hunter);
        }

        private static bool RespondsToCorpseFalls(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            FaunaDataTemplate faunaDataTemplate = archetype.faunaDataTemplate;
            if (faunaDataTemplate != null &&
                (faunaDataTemplate.DietMaskBits & (uint)FaunaDietMask.Carcass) != 0u)
            {
                return true;
            }

            return MatchesAnyToken(archetype.creatureId, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.gameplayPurpose, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.behaviorTreeHint, ScavengerSpawnTokens);
        }

        private static float ResolveCorpseSpawnInfluence01(Vector3 worldPosition, float radiusMeters)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null
                ? organicManager.ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters)
                : 0f;
        }

        private static float ResolveCombinedCorpseSpawnInfluence01(Vector3 worldPosition, float radiusMeters)
        {
            float liveCorpseInfluence01 = ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters);
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            float persistentWhaleFallInfluence01 = registry != null
                ? registry.ResolveWhaleFallSpawnInfluence01(worldPosition, Time.time, radiusMeters)
                : 0f;
            return math.max(liveCorpseInfluence01, persistentWhaleFallInfluence01);
        }

        private static bool MatchesAnyToken(string value, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsSpeciesId(int[] speciesIds, int speciesId)
        {
            if (speciesId == 0 || speciesIds == null)
                return false;

            for (int i = 0; i < speciesIds.Length; i++)
            {
                if (speciesIds[i] == speciesId)
                    return true;
            }

            return false;
        }

        private void DecayBiomeHostility()
        {
            if (_biomeHostility01 <= 0f)
                return;

            SetBiomeHostility(math.max(0f, _biomeHostility01 - hostilityDecayPerSlowTick));
            if (_biomeHostility01 > 0f)
                ApplyDirectorHostilityPressure();
        }

        private void ApplyDirectorHostilityPressure()
        {
            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            float combinedHostility01 = ResolveCombinedHostility01();
            if (director == null || combinedHostility01 <= 0f)
                return;

            float holdSeconds = hostilityPeakHoldSeconds * math.saturate(combinedHostility01 + (_starvationAggressionPressure01 * 0.35f));
            director.ApplyExternalPeakPressure(combinedHostility01, holdSeconds);
        }

        private void SetBiomeHostility(float hostility01)
        {
            float clamped = math.saturate(hostility01);
            if (math.abs(clamped - _biomeHostility01) <= 0.0001f)
                return;

            _biomeHostility01 = clamped;
            RefreshHostilityTier();
        }

        private void RefreshStarvationPressure()
        {
            _starvationAggressionPressure01 = 0f;
            RefreshHostilityTier();
        }

        private void RefreshHostilityTier()
        {
            int tier = ResolveHostilityTier(ResolveCombinedHostility01());
            if (tier == _hostilityTier)
                return;

            _hostilityTier = tier;
            switch (tier)
            {
                case 3:
                    NotificationEvents.PushCritical("BIOME HOSTILITY: EXTREME. THE ABYSS HATES YOU.");
                    break;

                case 2:
                    NotificationEvents.PushWarning("BIOME HOSTILITY: ELEVATED. PREDATOR PEAK EXTENDED.");
                    break;

                case 1:
                    NotificationEvents.PushInfo("BIOME HOSTILITY: RISING.");
                    break;
            }
        }

        private float ResolveCombinedHostility01()
        {
            return math.saturate(math.max(_biomeHostility01, _starvationAggressionPressure01));
        }

        private static int ResolveHostilityTier(float hostility01)
        {
            if (hostility01 >= 0.75f)
                return 3;

            if (hostility01 >= 0.4f)
                return 2;

            return hostility01 >= 0.15f ? 1 : 0;
        }

        private void AllocateRuntimeState()
        {
            if (_sectorFrontStates.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SectorPopulationState>[maxTrackedSectors] - ecosystem sector front buffer for Burst readers - owner: EcosystemDirector
            _sectorFrontStates = new NativeArray<SectorPopulationState>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<SectorPopulationState>[maxTrackedSectors] - ecosystem sector back buffer for Burst writers - owner: EcosystemDirector
            _sectorBackStates = new NativeArray<SectorPopulationState>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeHashMap<long,int>[maxTrackedSectors] - packed sector-key to slot lookup for O(1) cold-path classification - owner: EcosystemDirector
            _sectorIndexByKey = new NativeHashMap<long, int>(maxTrackedSectors, Allocator.Persistent);
            // COLD ALLOC: NativeArray<ApexTerritorySample>[16] - active Apex territory overlap job inputs - owner: EcosystemDirector
            _apexTerritorySamples = new NativeArray<ApexTerritorySample>(ApexTerritoryOverlapCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ApexTerritoryOverlapResult>[16] - active Apex territory overlap job outputs - owner: EcosystemDirector
            _apexTerritoryOverlapResults = new NativeArray<ApexTerritoryOverlapResult>(ApexTerritoryOverlapCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float4>[32] - global flora predator AUP upload staging buffer - owner: EcosystemDirector
            _floraPredatorAupUpload = new NativeArray<float4>(FloraPredatorAupBufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<EcosystemSectorSaveRecord>[maxTrackedSectors] - packed ecosystem persistence snapshot staging buffer - owner: EcosystemDirector
            _saveSnapshotSectors = new NativeList<EcosystemSectorSaveRecord>(maxTrackedSectors, Allocator.Persistent);
            RegisterNativeMemorySentinelAllocations();
            // COLD ALLOC: FaunaBrain[16] - managed Apex brain lookup paired with Burst overlap result indices - owner: EcosystemDirector
            _apexTerritoryBrains = new FaunaBrain[ApexTerritoryOverlapCandidateCapacity];
            _floraPredatorAupBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FloraPredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[32] - global flora predator AUP StructuredBuffer - owner: EcosystemDirector
            Shader.SetGlobalBuffer(_PredatorAUPBufferId, _floraPredatorAupBuffer);
            Shader.SetGlobalInt(_PredatorAUPCountId, 0);
            Shader.SetGlobalVector(_PredatorAUPParamsId, new Vector4(FloraPredatorStealthRadiusMeters, FloraPredatorStealthDimStrength, 0f, 0f));
            _activeSectorCount = 0;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _solveScheduled = false;
            _apexTerritoryOverlapScheduled = false;
            _populationSolvePendingHibernationSync = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _eclipsePredatorMigrationTimer = 0f;
            _eclipsePredatorMigrationIntensity01 = 0f;
            _eclipsePredatorMigrationAccumulator = 0f;
            _debugEclipsePredatorMigrationTimeRemaining = 0f;
            _debugEclipsePredatorMigrationIntensity = 0f;
            _debugEclipsePredatorMigratedCount = 0;
            _hostilityTier = 0;
        }

        private void DisposeRuntimeState()
        {
            JobHandle disposeDependency = _solveScheduled ? _scheduledSolveHandle : default;
            if (_apexTerritoryOverlapScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _scheduledApexTerritoryOverlapHandle);

            UnregisterNativeMemorySentinelAllocations();

            if (_sectorFrontStates.IsCreated)
                _sectorFrontStates.Dispose(disposeDependency);
            if (_sectorBackStates.IsCreated)
                _sectorBackStates.Dispose(disposeDependency);
            if (_sectorIndexByKey.IsCreated)
                _sectorIndexByKey.Dispose(disposeDependency);
            if (_apexTerritorySamples.IsCreated)
                _apexTerritorySamples.Dispose(disposeDependency);
            if (_apexTerritoryOverlapResults.IsCreated)
                _apexTerritoryOverlapResults.Dispose(disposeDependency);
            if (_floraPredatorAupUpload.IsCreated)
                _floraPredatorAupUpload.Dispose(disposeDependency);
            if (_saveSnapshotSectors.IsCreated)
                _saveSnapshotSectors.Dispose(disposeDependency);
            ReleaseBuffer(ref _floraPredatorAupBuffer);
            Shader.SetGlobalInt(_PredatorAUPCountId, 0);

            _sectorFrontStates = default;
            _sectorBackStates = default;
            _sectorIndexByKey = default;
            _apexTerritorySamples = default;
            _apexTerritoryOverlapResults = default;
            _floraPredatorAupUpload = default;
            _saveSnapshotSectors = default;
            _apexTerritoryBrains = null;
            _activeSectorCount = 0;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _solveScheduled = false;
            _apexTerritoryOverlapScheduled = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _hostilityTier = 0;
            _floraPredatorAupSaturationTelemetryIssued = false;
        }

        private void RegisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.RegisterNativeArray(_sectorFrontStates, NativeMemoryOwner, nameof(_sectorFrontStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_sectorBackStates, NativeMemoryOwner, nameof(_sectorBackStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_sectorIndexByKey, NativeMemoryOwner, nameof(_sectorIndexByKey), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexTerritorySamples, NativeMemoryOwner, nameof(_apexTerritorySamples), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexTerritoryOverlapResults, NativeMemoryOwner, nameof(_apexTerritoryOverlapResults), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_floraPredatorAupUpload, NativeMemoryOwner, nameof(_floraPredatorAupUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_saveSnapshotSectors, NativeMemoryOwner, nameof(_saveSnapshotSectors), NativeMemoryLifetime);
        }

        private void UnregisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.UnregisterNativeArray(_sectorFrontStates);
            NativeMemorySentinel.UnregisterNativeArray(_sectorBackStates);
            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, nameof(_sectorIndexByKey));
            NativeMemorySentinel.UnregisterNativeArray(_apexTerritorySamples);
            NativeMemorySentinel.UnregisterNativeArray(_apexTerritoryOverlapResults);
            NativeMemorySentinel.UnregisterNativeArray(_floraPredatorAupUpload);
            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_saveSnapshotSectors));
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterEcosystemDirectorService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.EcosystemDirector, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterEcosystemDirectorService(this);
            _registeredService = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTickable = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrameTickable = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTickable)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrameTickable = false;
        }

        private void EnsurePlayerSectorRegistered()
        {
            if (HasPendingSimulationJob())
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || !playerContext.IsInitialized || playerContext.PlayerTransform == null)
                return;

            ResolveOrCreateSectorSlot(QuantizeSector(playerContext.PlayerTransform.position), seedWithBaseline: true);
        }

        private void EnsureMigrationNeighborSectorsRegistered()
        {
            if (HasPendingSimulationJob())
                return;

            int seedSectorCount = _activeSectorCount;
            for (int sectorIndex = 0; sectorIndex < seedSectorCount && _activeSectorCount < _sectorFrontStates.Length; sectorIndex++)
            {
                SectorPopulationState sourceState = _sectorFrontStates[sectorIndex];
                if (sourceState.PreyPopulationRounded <= 0 && sourceState.PredatorPopulationRounded <= 0)
                    continue;

                int2 sectorCoord = sourceState.SectorCoord;
                ResolveOrCreateSectorSlot(sectorCoord + new int2(1, 0), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(-1, 0), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(0, 1), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(0, -1), seedWithBaseline: false);
            }
        }

        private void TickEclipsePredatorShallowMigration(float dt)
        {
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
            {
                _debugEclipsePredatorMigrationTimeRemaining = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationTimer = math.max(0f, _eclipsePredatorMigrationTimer - math.max(0f, dt));
            _debugEclipsePredatorMigrationTimeRemaining = _eclipsePredatorMigrationTimer;
            _debugEclipsePredatorMigrationIntensity = _eclipsePredatorMigrationIntensity01;

            if (_eclipsePredatorMigrationTimer <= 0f)
            {
                _eclipsePredatorMigrationIntensity01 = 0f;
                _eclipsePredatorMigrationAccumulator = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationAccumulator += dt;
            if (_eclipsePredatorMigrationAccumulator < eclipsePredatorMigrationIntervalSeconds)
                return;

            _eclipsePredatorMigrationAccumulator = 0f;
            if (!TryResolveEclipseTier0Attractor(out Vector3 attractorPosition))
                return;

            PersistentWorldRegistry registry = _cachedPersistentWorldRegistry != null
                ? _cachedPersistentWorldRegistry
                : GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return;

            float stepMeters = eclipsePredatorMigrationStepMeters * math.max(0.1f, _eclipsePredatorMigrationIntensity01);
            int migratedCount = registry.MigrateApexFaunaHibernationStatesToward(
                attractorPosition,
                eclipsePredatorMigrationRadiusMeters,
                stepMeters);
            _debugEclipsePredatorMigratedCount += migratedCount;
        }

        private bool TryResolveEclipseTier0Attractor(out Vector3 attractorPosition)
        {
            attractorPosition = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            attractorPosition = playerTransform.position;
            float waterLevel = ResolveWaterSurfaceLevel(attractorPosition);
            attractorPosition.y = waterLevel - eclipsePredatorTier0TargetDepthMeters;
            return true;
        }

        private bool TryResolveEclipsePredatorTier0SelectionBoost(Vector3 worldPosition, out float selectionBoost)
        {
            selectionBoost = 1f;
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
                return false;

            float depthMeters = ResolveDepthMeters(worldPosition);
            if (depthMeters > eclipsePredatorTier0DepthMaxMeters)
                return false;

            selectionBoost = math.lerp(1f, eclipsePredatorTier0SelectionBoost, _eclipsePredatorMigrationIntensity01);
            return selectionBoost > 1f;
        }

        private static float ResolveDepthMeters(Vector3 worldPosition)
        {
            return math.max(0f, ResolveWaterSurfaceLevel(worldPosition) - worldPosition.y);
        }

        private static float ResolveWaterSurfaceLevel(Vector3 worldPosition)
        {
            MapMagicBridge bridge = MapMagicBridge.Instance;
            if (bridge != null)
                return bridge.WaterSurfaceLevel;

            return worldPosition.y;
        }

        private void ScheduleSectorSolve()
        {
            if (_activeSectorCount <= 0 || _solveScheduled)
                return;

            var solveJob = new ProbabilityTablePopulationJob
            {
                FrontStates = _sectorFrontStates,
                BackStates = _sectorBackStates,
                LeviathanChance01 = leviathanSectorSpawnChance,
                BaselineFitness = baselineFitness,
                GrazerPopulationPerSector = grazerPopulationPerSector,
                LeviathanPopulationPerSector = leviathanPopulationPerSector,
                MaxPreyPopulation = maxPreyPopulation,
                MaxPredatorPopulation = maxPredatorPopulation
            };

            _scheduledSolveHandle = solveJob.Schedule(_activeSectorCount, 16);
            _solveScheduled = true;
        }

        private bool HasPendingSimulationJob()
        {
            return _solveScheduled;
        }

        private void CompleteScheduledSimulation(bool forceComplete)
        {
            CompleteScheduledSolve(forceComplete);
        }

        private void CompleteScheduledSolve(bool forceComplete)
        {
            if (!_solveScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledSolveHandle, forceComplete))
                return;

            NativeArray<SectorPopulationState> swap = _sectorFrontStates;
            _sectorFrontStates = _sectorBackStates;
            _sectorBackStates = swap;
            _solveScheduled = false;
            RefreshStarvationPressure();
            _populationSolvePendingHibernationSync = true;
        }

        private void ScheduleApexTerritoryOverlap(Vector3 queryOrigin)
        {
            if (_apexTerritoryOverlapScheduled ||
                !_apexTerritorySamples.IsCreated ||
                !_apexTerritoryOverlapResults.IsCreated ||
                _apexTerritoryBrains == null)
            {
                return;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                ApexTerritoryOverlapQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _apexTerritoryOverlapHits);
            int sampleCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && sampleCount < ApexTerritoryOverlapCandidateCapacity; hitIndex++)
            {
                SpatialQueryHit hit = _apexTerritoryOverlapHits[hitIndex];
                FaunaBrain brain = hit.Owner as FaunaBrain;
                if (brain == null || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                _apexTerritoryBrains[sampleCount] = brain;
                _apexTerritorySamples[sampleCount] = new ApexTerritorySample
                {
                    Position = new float3(hit.Position.x, hit.Position.y, hit.Position.z),
                    Radius = brain.ApexTerritoryRadiusMeters,
                    MassScore = brain.ApexTerritoryMassScore,
                    BrainIndex = sampleCount
                };
                _apexTerritoryOverlapResults[sampleCount] = default;
                sampleCount++;
            }

            if (sampleCount < 2)
            {
                for (int i = 0; i < sampleCount; i++)
                    _apexTerritoryBrains[i] = null;
                return;
            }

            var overlapJob = new ApexTerritoryOverlapJob
            {
                Samples = _apexTerritorySamples,
                Results = _apexTerritoryOverlapResults,
                Count = sampleCount,
                OverlapThreshold01 = ApexTerritoryOverlapRetreatThreshold01
            };

            _scheduledApexTerritoryOverlapHandle = overlapJob.Schedule(sampleCount, 4);
            _scheduledApexTerritoryOverlapCount = sampleCount;
            _apexTerritoryOverlapScheduled = true;
        }

        private void CompleteScheduledApexTerritoryOverlap(bool forceComplete)
        {
            if (!_apexTerritoryOverlapScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledApexTerritoryOverlapHandle, forceComplete))
                return;

            int count = math.min(_scheduledApexTerritoryOverlapCount, ApexTerritoryOverlapCandidateCapacity);
            for (int i = 0; i < count; i++)
            {
                ApexTerritoryOverlapResult result = _apexTerritoryOverlapResults[i];
                if (result.RetreatBrainIndex < 0 ||
                    result.RetreatBrainIndex >= count ||
                    result.RivalBrainIndex < 0 ||
                    result.RivalBrainIndex >= count ||
                    result.Overlap01 <= ApexTerritoryOverlapRetreatThreshold01)
                {
                    continue;
                }

                FaunaBrain retreatBrain = _apexTerritoryBrains[result.RetreatBrainIndex];
                if (retreatBrain == null || retreatBrain.IsDead)
                    continue;

                retreatBrain.ForceApexRetreat(ToVector3(_apexTerritorySamples[result.RivalBrainIndex].Position));
            }

            for (int i = 0; i < count; i++)
            {
                _apexTerritoryBrains[i] = null;
                _apexTerritoryOverlapResults[i] = default;
            }

            _scheduledApexTerritoryOverlapHandle = default;
            _scheduledApexTerritoryOverlapCount = 0;
            _apexTerritoryOverlapScheduled = false;
        }

        private void PublishFloraPredatorAupBuffer(Vector3 queryOrigin)
        {
            if (!_floraPredatorAupUpload.IsCreated || _floraPredatorAupBuffer == null)
                return;

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                FloraPredatorAupQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _floraPredatorAupHits);
            int uploadCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && uploadCount < FloraPredatorAupBufferCapacity; hitIndex++)
            {
                SpatialQueryHit hit = _floraPredatorAupHits[hitIndex];
                FaunaBrain brain = hit.Owner as FaunaBrain;
                if (brain == null || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                _floraPredatorAupUpload[uploadCount] = new float4(
                    hit.Position.x,
                    hit.Position.y,
                    hit.Position.z,
                    FloraPredatorStealthRadiusMeters);
                uploadCount++;
            }

            if (uploadCount > 0)
                GraphicsBufferUploadUtility.UploadNativeArray(_floraPredatorAupBuffer, _floraPredatorAupUpload, uploadCount);

            bool saturated = hitCount >= FloraPredatorAupHitCapacity || uploadCount >= FloraPredatorAupBufferCapacity;
            if (saturated && !_floraPredatorAupSaturationTelemetryIssued)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _FloraPredatorAupSaturationWarningHash,
                    _EcosystemDirectorContextHash,
                    math.max(hitCount, uploadCount));
                _floraPredatorAupSaturationTelemetryIssued = true;
            }
            else if (!saturated)
            {
                _floraPredatorAupSaturationTelemetryIssued = false;
            }

            Shader.SetGlobalBuffer(_PredatorAUPBufferId, _floraPredatorAupBuffer);
            Shader.SetGlobalInt(_PredatorAUPCountId, uploadCount);
            Shader.SetGlobalVector(_PredatorAUPParamsId, new Vector4(FloraPredatorStealthRadiusMeters, FloraPredatorStealthDimStrength, 0f, 0f));
        }

        private static bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            playerPosition = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            playerPosition = playerTransform.position;
            return true;
        }

        private void SyncPendingHibernatedFaunaPopulationRecords()
        {
            if (!_populationSolvePendingHibernationSync)
                return;

            _populationSolvePendingHibernationSync = false;
            SyncHibernatedFaunaPopulationRecords();
        }

        private void SyncHibernatedFaunaPopulationRecords()
        {
            if (_activeSectorCount <= 0)
                return;

            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;

            if (_cachedPersistentWorldRegistry == null)
                return;

            int syncBudget = math.min(HibernationPopulationSyncsPerColdSolve, _activeSectorCount);
            for (int i = 0; i < syncBudget; i++)
            {
                if (_nextHibernationPopulationSyncIndex >= _activeSectorCount)
                    _nextHibernationPopulationSyncIndex = 0;

                SectorPopulationState state = _sectorFrontStates[_nextHibernationPopulationSyncIndex];
                _cachedPersistentWorldRegistry.ReconcileFaunaHibernationSectorPopulation(
                    state.SectorCoord,
                    state.PreyPopulationRounded,
                    state.PredatorPopulationRounded,
                    maxPreyPopulation,
                    maxPredatorPopulation);

                _nextHibernationPopulationSyncIndex++;
            }
        }

        private int ResolveOrCreateSectorSlot(int2 sectorCoord, bool seedWithBaseline = true)
        {
            long packedKey = PackSectorKey(sectorCoord);
            if (_sectorIndexByKey.TryGetValue(packedKey, out int existingSlot))
                return existingSlot;

            if (_activeSectorCount >= _sectorFrontStates.Length)
                return -1;

            int slotIndex = _activeSectorCount;
            _activeSectorCount++;
            _sectorIndexByKey.TryAdd(packedKey, slotIndex);

            SectorPopulationState seededState = SeedSectorState(sectorCoord, seedWithBaseline);
            _sectorFrontStates[slotIndex] = seededState;
            _sectorBackStates[slotIndex] = seededState;
            return slotIndex;
        }

        private SectorPopulationState SeedSectorState(int2 sectorCoord, bool seedWithBaseline)
        {
            int biomeId = ResolveBiomeIdForSector(sectorCoord);
            ResolveProbabilityTablePopulation(
                sectorCoord,
                biomeId,
                leviathanSectorSpawnChance,
                grazerPopulationPerSector,
                leviathanPopulationPerSector,
                out int preyPopulation,
                out int predatorPopulation);

            SectorPopulationState state = default;
            state.SectorCoord = sectorCoord;
            state.PreyPopulation = preyPopulation;
            state.PredatorPopulation = predatorPopulation;
            state.HarvestPressure = 0f;
            state.Fitness = baselineFitness;
            state.SpeedMultiplier = 1f;
            state.CamouflageIndex = 0f;
            state.PreyPopulationRounded = preyPopulation;
            state.PredatorPopulationRounded = predatorPopulation;
            state.BiomeId = biomeId;
            return state;
        }

        private static void ResolveProbabilityTablePopulation(
            int2 sectorCoord,
            int biomeId,
            float leviathanChance01,
            int grazerPopulationPerSector,
            int leviathanPopulationPerSector,
            out int preyPopulation,
            out int predatorPopulation)
        {
            float roll01 = StableSectorRandom01(sectorCoord, biomeId);
            bool spawnLeviathan = roll01 < math.saturate(leviathanChance01);
            preyPopulation = spawnLeviathan ? 0 : math.max(0, grazerPopulationPerSector);
            predatorPopulation = spawnLeviathan ? math.max(0, leviathanPopulationPerSector) : 0;
        }

        private static float StableSectorRandom01(int2 sectorCoord, int biomeId)
        {
            uint hash = math.hash(new uint3(
                (uint)sectorCoord.x ^ 0x9E3779B9u,
                (uint)sectorCoord.y ^ 0x85EBCA6Bu,
                (uint)biomeId ^ 0xC2B2AE35u));
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static int2 QuantizeSector(Vector3 worldPosition)
        {
            float2 scaled = new float2(worldPosition.x, worldPosition.z) / SectorEdgeLengthMeters;
            return (int2)math.floor(scaled);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static long PackSectorKey(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static int ResolveBiomeIdForSector(int2 sectorCoord)
        {
            uint hash = math.hash(new uint2((uint)sectorCoord.x ^ 0x4D2u, (uint)sectorCoord.y ^ 0x8B3u));
            uint bucket = hash % 10u;
            if (bucket < 2u)
                return 1;
            if (bucket < 5u)
                return 2;

            return 0;
        }

        private static uint PackPopulationCounts(int preyPopulation, int predatorPopulation)
        {
            uint packedPrey = (ushort)math.clamp(preyPopulation, 0, ushort.MaxValue);
            uint packedPredator = (ushort)math.clamp(predatorPopulation, 0, ushort.MaxValue);
            return packedPrey | (packedPredator << 16);
        }

        private static uint PackAdaptationTraits(float fitness, float speedMultiplier, float camouflageIndex, float maximumSpeedMultiplier)
        {
            uint packedFitness = (uint)math.round(math.saturate(fitness) * 255f);
            float safeMaximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            float speed01 = math.saturate((speedMultiplier - 1f) / (safeMaximumSpeedMultiplier - 1f + 0.0001f));
            uint packedSpeed = (uint)math.round(speed01 * 255f);
            uint packedCamouflage = (uint)math.round(math.saturate(camouflageIndex) * 255f);
            return packedFitness | (packedSpeed << 8) | (packedCamouflage << 16);
        }

    }
}
