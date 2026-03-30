using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4060)]
    public sealed class WorldZoneDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ScatterBudgetController scatterBudgetController;
        [SerializeField] private WorldSliceDirector worldSliceDirector;

        [Header("Diagnostics")]
        [SerializeField] private string _debugCurrentZoneId = "zone.none";
        [SerializeField] private string _debugCurrentZoneLabel = "None";
        [SerializeField] private string _debugCurrentZoneKind = "Generic";
        [SerializeField] private string _debugCurrentZoneTier = "Starter";
        [SerializeField] private string _debugZonePlan = "None";
        [SerializeField] private string _debugHeroFamily = "None";
        [SerializeField] private string _debugNearFamily = "None";
        [SerializeField] private string _debugMidFamily = "None";
        [SerializeField] private string _debugFarFamily = "None";
        [SerializeField] private string _debugResourcePocketFamily = "None";
        [SerializeField] private string _debugNodeClusterFamily = "None";
        [SerializeField] private string _debugSafePocketFamily = "None";
        [SerializeField] private string _debugBuildSocketFamily = "None";
        [SerializeField] private string _debugPowerSpineFamily = "None";
        [SerializeField] private string _debugServiceChokeFamily = "None";
        [SerializeField] private string _debugRouteAnchorFamily = "None";
        [SerializeField] private string _debugHazardGateFamily = "None";
        [SerializeField] private string _debugRareObjectiveFamily = "None";
        [SerializeField] private string _debugDominantBiome = "None";
        [SerializeField] private string _debugDominantBiomeFamily = "None";
        [SerializeField] private string _debugDominantVisitPurpose = "None";
        [SerializeField] private string _debugDominantLandmark = "None";
        [SerializeField] private string _debugDominantRisk = "None";
        [SerializeField] private string _debugDominantEarlyFarm = "None";
        [SerializeField] private string _debugDominantLateReturn = "None";
        [SerializeField] private string _debugDominantLandmarkRole = "None";
        [SerializeField] private string _debugDominantExtraction = "None";
        [SerializeField] private string _debugDominantLandmarkGuidance = "None";
        [SerializeField] private int _debugLoosePickupBias;
        [SerializeField] private int _debugNodeBias;
        [SerializeField] private int _debugSalvageBias;
        [SerializeField] private int _debugCommonBias;
        [SerializeField] private int _debugUncommonBias;
        [SerializeField] private int _debugRareBias;
        [SerializeField] private float _debugEffectiveScavengeScale = 1f;
        [SerializeField] private float _debugEffectiveSpawnScale = 1f;
        [SerializeField] private float _debugEffectiveColliderRadiusScale = 1f;
        [SerializeField] private float _debugEffectiveColliderOpsScale = 1f;
        [SerializeField] private float _debugEffectiveNearSliceScale = 1f;
        [SerializeField] private float _debugEffectiveMidSliceScale = 1f;
        [SerializeField] private int _debugEffectiveNearDensity;
        [SerializeField] private int _debugEffectiveMidDensity;
        [SerializeField] private int _debugEffectiveFarDensity;
        [SerializeField] private string _debugZoneRewardRhythm = "None";
        [SerializeField] private string _debugZoneRouteRhythm = "None";
        [SerializeField] private string _debugZoneSafePocketRhythm = "None";
        [SerializeField] private string _debugNearestZone = "None";
        [SerializeField] private int _debugZoneCount;
        [SerializeField] private bool _debugApplied;

        private readonly List<WorldZoneAnchor> _anchors = new List<WorldZoneAnchor>(32);
        private bool _registeredToTickManager;
        private WorldZoneAnchor _currentZone;

        public WorldZoneAnchor CurrentZone => _currentZone;

        private void Awake()
        {
            ResolvePlayer();
            RefreshAnchors();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            EvaluateZones(forceRefresh: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            EvaluateZones(forceRefresh: false);
        }

        public void RefreshAnchors()
        {
            _anchors.Clear();

            WorldZoneAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldZoneAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldZoneAnchor anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || !anchor.gameObject.scene.IsValid())
                    continue;

                _anchors.Add(anchor);
            }

            _debugZoneCount = _anchors.Count;
        }

        private void EvaluateZones(bool forceRefresh)
        {
            ResolvePlayer();
            if (forceRefresh || _anchors.Count == 0)
                RefreshAnchors();

            if (playerTransform == null)
            {
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            WorldZoneAnchor nearest = null;
            float nearestDistance = float.MaxValue;
            WorldZoneAnchor bestCandidate = null;

            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                float distance = anchor.GetFlatDistance(playerPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = anchor;
                }

                bool insideActivation = anchor.IsInsideActivation(playerPosition);
                bool insideHold = anchor.IsInsideHold(playerPosition);

                if (_currentZone == anchor && insideHold)
                {
                    bestCandidate = anchor;
                    continue;
                }

                if (!insideActivation)
                    continue;

                if (bestCandidate == null || Compare(anchor, bestCandidate, playerPosition) < 0)
                    bestCandidate = anchor;
            }

            _currentZone = bestCandidate ?? nearest;
            ApplyZoneProfile(_currentZone);
            _debugNearestZone = nearest != null ? nearest.ZoneLabel : "None";
            _debugApplied = _currentZone != null;
            UpdateDiagnostics();
        }

        private int Compare(WorldZoneAnchor a, WorldZoneAnchor b, Vector3 playerPosition)
        {
            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return a.GetFlatDistance(playerPosition).CompareTo(b.GetFlatDistance(playerPosition));
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
                return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            if (player != null)
                playerTransform = player.transform;

            if (scatterBudgetController == null)
                scatterBudgetController = FindAnyObjectByType<ScatterBudgetController>();

            if (worldSliceDirector == null)
                worldSliceDirector = FindAnyObjectByType<WorldSliceDirector>();
        }

        private void ApplyZoneProfile(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;

            float scavengeScale = profile != null ? profile.scavengeRadiusScale : 1f;
            float spawnScale = profile != null ? profile.spawnScale : 1f;
            float colliderRadiusScale = profile != null ? profile.colliderRadiusScale : 1f;
            float colliderOpsScale = profile != null ? profile.colliderOpsScale : 1f;
            float nearSliceScale = profile != null ? profile.sliceNearScale : 1f;
            float midSliceScale = profile != null ? profile.sliceMidScale : 1f;

            scavengeScale *= EvaluateBiomeScavengeScale(zone, biome);
            spawnScale *= EvaluateBiomeSpawnScale(zone, biome);
            colliderRadiusScale *= EvaluateBiomeColliderRadiusScale(zone, biome);
            colliderOpsScale *= EvaluateBiomeColliderOpsScale(zone, biome);
            nearSliceScale *= EvaluateBiomeNearSliceScale(zone, biome);
            midSliceScale *= EvaluateBiomeMidSliceScale(zone, biome);

            _debugEffectiveScavengeScale = scavengeScale;
            _debugEffectiveSpawnScale = spawnScale;
            _debugEffectiveColliderRadiusScale = colliderRadiusScale;
            _debugEffectiveColliderOpsScale = colliderOpsScale;
            _debugEffectiveNearSliceScale = nearSliceScale;
            _debugEffectiveMidSliceScale = midSliceScale;

            if (scatterBudgetController != null)
                scatterBudgetController.SetZoneScales(scavengeScale, spawnScale, colliderRadiusScale, colliderOpsScale);

            if (worldSliceDirector != null)
                worldSliceDirector.SetZoneScales(nearSliceScale, midSliceScale);
        }

        private void UpdateDiagnostics()
        {
            _debugZoneCount = _anchors.Count;

            if (_currentZone == null)
            {
                _debugCurrentZoneId = "zone.none";
                _debugCurrentZoneLabel = "None";
                _debugCurrentZoneKind = WorldZoneAnchor.ZoneKind.Generic.ToString();
                _debugCurrentZoneTier = WorldZoneAnchor.ZoneTier.Starter.ToString();
                _debugZonePlan = "None";
                _debugHeroFamily = "None";
                _debugNearFamily = "None";
                _debugMidFamily = "None";
                _debugFarFamily = "None";
                _debugResourcePocketFamily = "None";
                _debugNodeClusterFamily = "None";
                _debugSafePocketFamily = "None";
                _debugBuildSocketFamily = "None";
                _debugPowerSpineFamily = "None";
                _debugServiceChokeFamily = "None";
                _debugRouteAnchorFamily = "None";
                _debugHazardGateFamily = "None";
                _debugRareObjectiveFamily = "None";
                _debugDominantBiome = "None";
                _debugDominantBiomeFamily = "None";
                _debugDominantVisitPurpose = "None";
                _debugDominantLandmark = "None";
                _debugDominantRisk = "None";
                _debugDominantEarlyFarm = "None";
                _debugDominantLateReturn = "None";
                _debugDominantLandmarkRole = "None";
                _debugDominantExtraction = "None";
                _debugDominantLandmarkGuidance = "None";
                _debugLoosePickupBias = 0;
                _debugNodeBias = 0;
                _debugSalvageBias = 0;
                _debugCommonBias = 0;
                _debugUncommonBias = 0;
                _debugRareBias = 0;
                _debugEffectiveScavengeScale = 1f;
                _debugEffectiveSpawnScale = 1f;
                _debugEffectiveColliderRadiusScale = 1f;
                _debugEffectiveColliderOpsScale = 1f;
                _debugEffectiveNearSliceScale = 1f;
                _debugEffectiveMidSliceScale = 1f;
                _debugEffectiveNearDensity = 0;
                _debugEffectiveMidDensity = 0;
                _debugEffectiveFarDensity = 0;
                _debugZoneRewardRhythm = "None";
                _debugZoneRouteRhythm = "None";
                _debugZoneSafePocketRhythm = "None";
                return;
            }

            HectonBiomeMatrixProfile biome = _currentZone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = _currentZone.DominantBiomeFamily;
            WorldZonePlanProfile zonePlan = _currentZone.Profile != null ? _currentZone.Profile.zonePlanProfile : null;
            _debugCurrentZoneId = _currentZone.ZoneId;
            _debugCurrentZoneLabel = _currentZone.ZoneLabel;
            _debugCurrentZoneKind = _currentZone.Kind.ToString();
            _debugCurrentZoneTier = _currentZone.Tier.ToString();
            _debugZonePlan = zonePlan != null
                ? zonePlan.planLabel
                : "None";
            _debugHeroFamily = _currentZone.Profile != null
                && zonePlan != null
                && zonePlan.heroFamily != null
                ? zonePlan.heroFamily.familyLabel
                : "None";
            _debugNearFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.nearInteractiveFamily)
                ? _currentZone.Profile.nearInteractiveFamily
                : "None";
            _debugMidFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.midVisualFamily)
                ? _currentZone.Profile.midVisualFamily
                : "None";
            _debugFarFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.farSilhouetteFamily)
                ? _currentZone.Profile.farSilhouetteFamily
                : "None";
            _debugResourcePocketFamily = zonePlan != null && zonePlan.resourcePocketPlan != null && zonePlan.resourcePocketPlan.family != null ? zonePlan.resourcePocketPlan.family.familyLabel : "None";
            _debugNodeClusterFamily = zonePlan != null && zonePlan.nodeClusterPlan != null && zonePlan.nodeClusterPlan.family != null ? zonePlan.nodeClusterPlan.family.familyLabel : "None";
            _debugSafePocketFamily = zonePlan != null && zonePlan.safePocketPlan != null && zonePlan.safePocketPlan.family != null ? zonePlan.safePocketPlan.family.familyLabel : "None";
            _debugBuildSocketFamily = zonePlan != null && zonePlan.buildSocketPlan != null && zonePlan.buildSocketPlan.family != null ? zonePlan.buildSocketPlan.family.familyLabel : "None";
            _debugPowerSpineFamily = zonePlan != null && zonePlan.powerSpinePlan != null && zonePlan.powerSpinePlan.family != null ? zonePlan.powerSpinePlan.family.familyLabel : "None";
            _debugServiceChokeFamily = zonePlan != null && zonePlan.serviceChokePlan != null && zonePlan.serviceChokePlan.family != null ? zonePlan.serviceChokePlan.family.familyLabel : "None";
            _debugRouteAnchorFamily = zonePlan != null && zonePlan.routeAnchorPlan != null && zonePlan.routeAnchorPlan.family != null ? zonePlan.routeAnchorPlan.family.familyLabel : "None";
            _debugHazardGateFamily = zonePlan != null && zonePlan.hazardGatePlan != null && zonePlan.hazardGatePlan.family != null ? zonePlan.hazardGatePlan.family.familyLabel : "None";
            _debugRareObjectiveFamily = zonePlan != null && zonePlan.rareObjectivePlan != null && zonePlan.rareObjectivePlan.family != null ? zonePlan.rareObjectivePlan.family.familyLabel : "None";
            _debugDominantBiome = biome != null ? biome.biomeName : "None";
            _debugDominantBiomeFamily = biomeFamily != null ? biomeFamily.familyLabel : "None";
            _debugDominantVisitPurpose = biome != null && !string.IsNullOrWhiteSpace(biome.visitPurpose)
                ? biome.visitPurpose
                : "None";
            _debugDominantLandmark = biome != null && !string.IsNullOrWhiteSpace(biome.landmarkIdentity)
                ? biome.landmarkIdentity
                : "None";
            _debugDominantRisk = biome != null && !string.IsNullOrWhiteSpace(biome.riskSummary)
                ? biome.riskSummary
                : "None";
            _debugDominantEarlyFarm = biomeFamily != null && biomeFamily.resourcePlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.resourcePlanProfile.earlyReasonToFarm)
                ? biomeFamily.resourcePlanProfile.earlyReasonToFarm
                : "None";
            _debugDominantLateReturn = biomeFamily != null && biomeFamily.resourcePlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.resourcePlanProfile.lateReasonToReturn)
                ? biomeFamily.resourcePlanProfile.lateReasonToReturn
                : "None";
            _debugDominantLandmarkRole = biomeFamily != null && biomeFamily.landmarkPlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.landmarkPlanProfile.dominantLandmarkRole)
                ? biomeFamily.landmarkPlanProfile.dominantLandmarkRole
                : "None";
            _debugDominantExtraction = biome != null && !string.IsNullOrWhiteSpace(biome.extractionFocus)
                ? biome.extractionFocus
                : "None";
            _debugDominantLandmarkGuidance = biome != null && !string.IsNullOrWhiteSpace(biome.landmarkGuidance)
                ? biome.landmarkGuidance
                : "None";
            _debugLoosePickupBias = biome != null ? biome.loosePickupBias : 0;
            _debugNodeBias = biome != null ? biome.nodeExtractionBias : 0;
            _debugSalvageBias = biome != null ? biome.salvageBias : 0;
            _debugCommonBias = biome != null ? biome.commonResourceBias : 0;
            _debugUncommonBias = biome != null ? biome.uncommonResourceBias : 0;
            _debugRareBias = biome != null ? biome.rareResourceBias : 0;
            _debugEffectiveNearDensity = EvaluateEffectiveDensity(zonePlan != null ? zonePlan.nearPlan.targetDensity : 0, biome, DensityBand.Near);
            _debugEffectiveMidDensity = EvaluateEffectiveDensity(zonePlan != null ? zonePlan.midPlan.targetDensity : 0, biome, DensityBand.Mid);
            _debugEffectiveFarDensity = EvaluateEffectiveDensity(zonePlan != null ? zonePlan.farPlan.targetDensity : 0, biome, DensityBand.Far);
            _debugZoneRewardRhythm = BuildRewardRhythm(biome, biomeFamily);
            _debugZoneRouteRhythm = BuildRouteRhythm(biome, biomeFamily);
            _debugZoneSafePocketRhythm = BuildSafePocketRhythm(biome, biomeFamily);
        }

        private float EvaluateBiomeScavengeScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float extractionPressure = Average(biome.loosePickupBias, biome.nodeExtractionBias, biome.salvageBias);
            float rewardPressure = Average(biome.commonResourceBias, biome.uncommonResourceBias, biome.rareResourceBias);

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Resources)
                return Mathf.Lerp(0.9f, 1.16f, Mathf.InverseLerp(1f, 5f, Mathf.Max(extractionPressure, rewardPressure)));

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Service || zone.Kind == WorldZoneAnchor.ZoneKind.Power)
                return Mathf.Lerp(0.94f, 1.08f, Mathf.InverseLerp(1f, 5f, biome.uncommonResourceBias));

            return 1f;
        }

        private float EvaluateBiomeSpawnScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float value = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => Mathf.Max(biome.commonResourceBias, biome.uncommonResourceBias),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rareResourceBias, biome.rewardPull),
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, 6 - biome.routePressure),
                WorldZoneAnchor.ZoneKind.Combat => Mathf.Max(biome.survivalPressure, biome.routePressure),
                _ => Average(biome.commonResourceBias, biome.rewardPull)
            };

            return Mathf.Lerp(0.9f, 1.14f, Mathf.InverseLerp(1f, 5f, value));
        }

        private float EvaluateBiomeColliderRadiusScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Combat || zone.Kind == WorldZoneAnchor.ZoneKind.Service)
                return Mathf.Lerp(0.96f, 1.12f, Mathf.InverseLerp(1f, 5f, biome.survivalPressure));

            return 1f;
        }

        private float EvaluateBiomeColliderOpsScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Power || zone.Kind == WorldZoneAnchor.ZoneKind.Service)
                return Mathf.Lerp(0.96f, 1.1f, Mathf.InverseLerp(1f, 5f, Mathf.Max(biome.uncommonResourceBias, biome.routePressure)));

            return 1f;
        }

        private float EvaluateBiomeNearSliceScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float emphasis = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => Mathf.Max(biome.loosePickupBias, biome.commonResourceBias),
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, 6 - biome.routePressure),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rewardPull, biome.rareResourceBias),
                _ => Mathf.Max(biome.landmarkStrength, biome.rewardPull)
            };

            return Mathf.Lerp(0.94f, 1.14f, Mathf.InverseLerp(1f, 5f, emphasis));
        }

        private float EvaluateBiomeMidSliceScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float emphasis = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, biome.routePressure),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rareResourceBias, biome.landmarkStrength),
                WorldZoneAnchor.ZoneKind.Combat => Mathf.Max(biome.survivalPressure, biome.routePressure),
                _ => Mathf.Max(biome.landmarkStrength, biome.uncommonResourceBias)
            };

            return Mathf.Lerp(0.96f, 1.18f, Mathf.InverseLerp(1f, 5f, emphasis));
        }

        private int EvaluateEffectiveDensity(int baseDensity, HectonBiomeMatrixProfile biome, DensityBand band)
        {
            if (baseDensity <= 0 || biome == null)
                return Mathf.Max(0, baseDensity);

            int emphasis = band switch
            {
                DensityBand.Near => Mathf.Max(biome.loosePickupBias, biome.commonResourceBias),
                DensityBand.Mid => Mathf.Max(biome.nodeExtractionBias, biome.uncommonResourceBias, biome.landmarkStrength),
                _ => Mathf.Max(biome.rareResourceBias, biome.landmarkStrength, biome.rewardPull)
            };

            float multiplier = Mathf.Lerp(0.8f, 1.35f, Mathf.InverseLerp(1f, 5f, emphasis));
            return Mathf.Max(1, Mathf.RoundToInt(baseDensity * multiplier));
        }

        private string BuildRewardRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.rareObjectivePattern))
                return biomeFamily.spatialPatternProfile.rareObjectivePattern;

            string familyLogic = biomeFamily != null && biomeFamily.resourcePlanProfile != null
                ? biomeFamily.resourcePlanProfile.routeRewardLogic
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(familyLogic))
                return familyLogic;

            if (biome.rareResourceBias >= 4)
                return "Редкие, дорогие заходы за сильной находкой.";
            if (biome.commonResourceBias >= 4 || biome.loosePickupBias >= 4)
                return "Короткие понятные круги с частой окупаемостью.";

            return "Смешанный цикл: мелкая добыча, потом более редкая ценность.";
        }

        private string BuildRouteRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.routeAnchorPattern))
                return biomeFamily.spatialPatternProfile.routeAnchorPattern;

            string guidance = biome.landmarkGuidance;
            if (!string.IsNullOrWhiteSpace(guidance))
                return guidance;

            if (biomeFamily != null && biomeFamily.landmarkPlanProfile != null)
                return biomeFamily.landmarkPlanProfile.routeUse;

            return "Маршрут держится на самых читаемых формах рельефа.";
        }

        private string BuildSafePocketRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.safePocketPattern))
                return biomeFamily.spatialPatternProfile.safePocketPattern;

            if (!string.IsNullOrWhiteSpace(biome.safePocketIdentity))
                return biome.safePocketIdentity;

            if (biomeFamily != null && biomeFamily.landmarkPlanProfile != null)
                return biomeFamily.landmarkPlanProfile.safePocketUse;

            return "Передышка ищется в складках рельефа и за большими формами.";
        }

        private static float Average(params int[] values)
        {
            if (values == null || values.Length == 0)
                return 0f;

            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];

            return total / (float)values.Length;
        }

        private enum DensityBand
        {
            Near,
            Mid,
            Far
        }
    }
}
