using System;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Environment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4035)]
    public sealed class BiomeMatrixDirector : MonoBehaviour, ISlowTickable
    {
        public static event Action<HectonBiomeMatrixProfile> OnMatrixBiomeChanged;
        public static event Action<int, float> OnDepthTierChanged;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonBiomeMatrixCatalog matrixCatalog;

        [Header("World Framing")]
        [SerializeField] private float surfaceOffsetMeters = 0f;
        [SerializeField] private Vector3 worldOrigin = Vector3.zero;
        [SerializeField] private float regionDeadZone = 24f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugTier = 1;
        [SerializeField] private string _debugRegion = "North";
        [SerializeField] private string _debugBiomeName = "None";
        [SerializeField] private int _debugMatrixIndex = -1;
        [SerializeField] private bool _debugPlaceholder;
        [SerializeField] private string _debugFamilyId = "None";
        [SerializeField] private string _debugFamilyLabel = "None";
        [SerializeField] private string _debugAtmosphereMood = "None";
        [SerializeField] private string _debugPrimaryResourceTheme = "None";
        [SerializeField] private string _debugNavigationStyle = "None";
        [SerializeField] private string _debugAtmosphereProfile = "None";
        [SerializeField] private string _debugFaunaFamily = "None";
        [SerializeField] private string _debugThreatStyle = "None";
        [SerializeField] private string _debugRecommendedLoadout = "None";
        [SerializeField] private string _debugResourcePlan = "None";
        [SerializeField] private string _debugResourceChannels = "None";
        [SerializeField] private string _debugEarlyFarmReason = "None";
        [SerializeField] private string _debugLateReturnReason = "None";
        [SerializeField] private string _debugExtractionStyle = "None";
        [SerializeField] private string _debugPocketResource = "None";
        [SerializeField] private string _debugNodeResource = "None";
        [SerializeField] private string _debugSafePocketResource = "None";
        [SerializeField] private string _debugRareObjectiveResource = "None";
        [SerializeField] private int _debugLoosePickupWeight;
        [SerializeField] private int _debugNodeExtractionWeight;
        [SerializeField] private int _debugSalvageRecoveryWeight;
        [SerializeField] private int _debugCommonResourcePull;
        [SerializeField] private int _debugUncommonResourcePull;
        [SerializeField] private int _debugRareResourcePull;
        [SerializeField] private string _debugLandmarkPlan = "None";
        [SerializeField] private string _debugDominantLandmarkRole = "None";
        [SerializeField] private string _debugRouteUse = "None";
        [SerializeField] private string _debugEmotionalRead = "None";
        [SerializeField] private string _debugSpatialPattern = "None";
        [SerializeField] private string _debugResourcePocketPattern = "None";
        [SerializeField] private string _debugNodeClusterPattern = "None";
        [SerializeField] private string _debugSafePocketPattern = "None";
        [SerializeField] private string _debugRouteAnchorPattern = "None";
        [SerializeField] private string _debugRareObjectivePattern = "None";
        [SerializeField] private string _debugExplorationLoop = "None";
        [SerializeField] private string _debugWhyPlayerComesHere = "None";
        [SerializeField] private int _debugRouteClarity;
        [SerializeField] private int _debugSafePocketFrequency;
        [SerializeField] private int _debugRareRewardPull;
        [SerializeField] private int _debugEncounterPressure;
        [SerializeField] private int _debugHazardPressure;
        [SerializeField] private string _debugVisitPurpose = "None";
        [SerializeField] private string _debugCommonRewardHook = "None";
        [SerializeField] private string _debugRareRewardHook = "None";
        [SerializeField] private string _debugLandmarkIdentity = "None";
        [SerializeField] private string _debugSafePocketIdentity = "None";
        [SerializeField] private string _debugRiskSummary = "None";
        [SerializeField] private string _debugExtractionFocus = "None";
        [SerializeField] private string _debugLandmarkGuidance = "None";
        [SerializeField] private int _debugLoosePickupBias;
        [SerializeField] private int _debugNodeExtractionBias;
        [SerializeField] private int _debugSalvageBias;
        [SerializeField] private int _debugCommonResourceBias;
        [SerializeField] private int _debugUncommonResourceBias;
        [SerializeField] private int _debugRareResourceBias;
        [SerializeField] private int _debugRoutePressure;
        [SerializeField] private int _debugLandmarkStrengthValue;
        [SerializeField] private int _debugRewardPullValue;
        [SerializeField] private int _debugSurvivalPressure;
        [SerializeField] private string _debugPrimaryClusterFocus = "None";
        [SerializeField] private string _debugSecondaryClusterFocus = "None";
        [SerializeField] private string _debugPrimaryStructureFocus = "None";
        [SerializeField] private string _debugSecondaryStructureFocus = "None";
        [SerializeField] private string _debugFaunaMoodValue = "None";

        private bool _registeredToTickManager;
        private HectonBiomeMatrixProfile _currentProfile;
        private int _currentDepthTier = 1;
        private float _currentDepthMeters;

        internal static BiomeMatrixDirector ActiveRuntimeInstance { get; private set; }

        public HectonBiomeMatrixProfile CurrentProfile => _currentProfile;
        public HectonBiomeFamilyProfile CurrentFamilyProfile => _currentProfile != null ? _currentProfile.familyProfile : null;
        public HectonBiomeMatrixCatalog MatrixCatalog => matrixCatalog;
        public int CurrentDepthTier => _currentDepthTier;
        public float CurrentDepthMeters => _currentDepthMeters;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
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

            EvaluateMatrix(forcePublish: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            EvaluateMatrix(forcePublish: false);
        }

        /// <summary>
        /// Forces immediate biome matrix evaluation for the current player position.
        /// </summary>
        public void ForceRefresh()
        {
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
        }

        public void SetMatrixCatalog(HectonBiomeMatrixCatalog catalog)
        {
            matrixCatalog = catalog;
        }

        private void EvaluateMatrix(bool forcePublish)
        {
            ResolveReferences();

            if (playerTransform == null || matrixCatalog == null)
            {
                UpdateDiagnostics(null, 1, HectonBiomeMatrixProfile.CardinalRegion.North);
                return;
            }

            float depth = surfaceOffsetMeters - playerTransform.position.y;
            int tier = ResolveDepthTier(depth);
            HectonBiomeMatrixProfile.CardinalRegion region = ResolveRegion(playerTransform.position);
            HectonBiomeMatrixProfile next = matrixCatalog.Resolve(tier, region);
            bool depthTierChanged = forcePublish || tier != _currentDepthTier;

            _currentDepthMeters = depth;
            _currentDepthTier = tier;

            if (depthTierChanged)
                OnDepthTierChanged?.Invoke(_currentDepthTier, _currentDepthMeters);

            if (forcePublish || next != _currentProfile)
            {
                _currentProfile = next;
                OnMatrixBiomeChanged?.Invoke(_currentProfile);
            }

            UpdateDiagnostics(_currentProfile, tier, region);
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private int ResolveDepthTier(float depth)
        {
            if (depth <= 0f)
                return 1;
            if (depth <= 300f)
                return 2;
            if (depth <= 600f)
                return 3;
            if (depth <= 1000f)
                return 4;
            if (depth <= 1500f)
                return 5;
            if (depth <= 2000f)
                return 6;
            if (depth <= 2500f)
                return 7;
            if (depth <= 3000f)
                return 8;
            if (depth <= 3500f)
                return 9;

            if (depth >= 14000f)
                return 27;

            float clamped = Mathf.Clamp(depth, 3500f, 14000f);
            float normalized = (clamped - 3500f) / 10500f;
            int tier = 10 + Mathf.FloorToInt(normalized * 17f);
            return Mathf.Clamp(tier, 10, 26);
        }

        private HectonBiomeMatrixProfile.CardinalRegion ResolveRegion(Vector3 position)
        {
            Vector3 delta = position - worldOrigin;
            delta.y = 0f;

            if (Mathf.Abs(delta.x) <= regionDeadZone && Mathf.Abs(delta.z) <= regionDeadZone)
                return HectonBiomeMatrixProfile.CardinalRegion.North;

            if (Mathf.Abs(delta.z) >= Mathf.Abs(delta.x))
                return delta.z >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.North : HectonBiomeMatrixProfile.CardinalRegion.South;

            return delta.x >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.East : HectonBiomeMatrixProfile.CardinalRegion.West;
        }

        private void UpdateDiagnostics(
            HectonBiomeMatrixProfile profile,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            _debugTier = tier;
            _debugRegion = region.ToString();
            _debugBiomeName = profile != null ? profile.biomeName : "None";
            _debugMatrixIndex = profile != null ? profile.matrixIndex : -1;
            _debugPlaceholder = profile != null && profile.isPlaceholder;
            _debugFamilyId = profile != null ? profile.familyId : "None";
            _debugFamilyLabel = profile != null && profile.familyProfile != null ? profile.familyProfile.familyLabel : "None";
            _debugAtmosphereMood = profile != null && profile.familyProfile != null ? profile.familyProfile.atmosphereMood : "None";
            _debugPrimaryResourceTheme = profile != null && profile.familyProfile != null ? profile.familyProfile.primaryResourceTheme : "None";
            _debugNavigationStyle = profile != null && profile.familyProfile != null ? profile.familyProfile.navigationStyle : "None";
            _debugAtmosphereProfile = profile != null && profile.familyProfile != null && profile.familyProfile.atmosphereProfile != null ? profile.familyProfile.atmosphereProfile.name : "None";
            _debugFaunaFamily = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.familyLabel : "None";
            _debugThreatStyle = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.threatStyle : "None";
            _debugRecommendedLoadout = profile != null && profile.familyProfile != null && profile.familyProfile.recommendedLoadoutPreset != null ? profile.familyProfile.recommendedLoadoutPreset.presetName : "None";
            _debugResourcePlan = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.profileLabel : "None";
            _debugResourceChannels = profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.profileLabel : "None";
            _debugEarlyFarmReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.earlyReasonToFarm : "None";
            _debugLateReturnReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.lateReasonToReturn : "None";
            _debugExtractionStyle = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.extractionStyle : "None";
            _debugPocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.resourcePocketItem : null);
            _debugNodeResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.nodeClusterItem : null);
            _debugSafePocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.safePocketItem : null);
            _debugRareObjectiveResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.rareObjectiveRewardItem : null);
            _debugLoosePickupWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.loosePickupWeight : 0;
            _debugNodeExtractionWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.nodeExtractionWeight : 0;
            _debugSalvageRecoveryWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.salvageRecoveryWeight : 0;
            _debugCommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.commonResourcePull : 0;
            _debugUncommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.uncommonResourcePull : 0;
            _debugRareResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.rareResourcePull : 0;
            _debugLandmarkPlan = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.profileLabel : "None";
            _debugDominantLandmarkRole = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.dominantLandmarkRole : "None";
            _debugRouteUse = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.routeUse : "None";
            _debugEmotionalRead = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.emotionalRead : "None";
            _debugSpatialPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.profileLabel : "None";
            _debugResourcePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.resourcePocketPattern : "None";
            _debugNodeClusterPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.nodeClusterPattern : "None";
            _debugSafePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.safePocketPattern : "None";
            _debugRouteAnchorPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.routeAnchorPattern : "None";
            _debugRareObjectivePattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.rareObjectivePattern : "None";
            _debugExplorationLoop = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.explorationLoop : "None";
            _debugWhyPlayerComesHere = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.whyPlayerComesHere : "None";
            _debugRouteClarity = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.routeClarity : 0;
            _debugSafePocketFrequency = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.safePocketFrequency : 0;
            _debugRareRewardPull = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.rareRewardPull : 0;
            _debugEncounterPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.encounterPressure : 0;
            _debugHazardPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.hazardPressure : 0;
            _debugVisitPurpose = profile != null ? profile.visitPurpose : "None";
            _debugCommonRewardHook = profile != null ? profile.commonRewardHook : "None";
            _debugRareRewardHook = profile != null ? profile.rareRewardHook : "None";
            _debugLandmarkIdentity = profile != null ? profile.landmarkIdentity : "None";
            _debugSafePocketIdentity = profile != null ? profile.safePocketIdentity : "None";
            _debugRiskSummary = profile != null ? profile.riskSummary : "None";
            _debugExtractionFocus = profile != null ? profile.extractionFocus : "None";
            _debugLandmarkGuidance = profile != null ? profile.landmarkGuidance : "None";
            _debugLoosePickupBias = profile != null ? profile.loosePickupBias : 0;
            _debugNodeExtractionBias = profile != null ? profile.nodeExtractionBias : 0;
            _debugSalvageBias = profile != null ? profile.salvageBias : 0;
            _debugCommonResourceBias = profile != null ? profile.commonResourceBias : 0;
            _debugUncommonResourceBias = profile != null ? profile.uncommonResourceBias : 0;
            _debugRareResourceBias = profile != null ? profile.rareResourceBias : 0;
            _debugRoutePressure = profile != null ? profile.routePressure : 0;
            _debugLandmarkStrengthValue = profile != null ? profile.landmarkStrength : 0;
            _debugRewardPullValue = profile != null ? profile.rewardPull : 0;
            _debugSurvivalPressure = profile != null ? profile.survivalPressure : 0;
            _debugPrimaryClusterFocus = profile != null ? profile.primaryClusterFocus.ToString() : "None";
            _debugSecondaryClusterFocus = profile != null ? profile.secondaryClusterFocus.ToString() : "None";
            _debugPrimaryStructureFocus = profile != null ? profile.primaryStructureFocus.ToString() : "None";
            _debugSecondaryStructureFocus = profile != null ? profile.secondaryStructureFocus.ToString() : "None";
            _debugFaunaMoodValue = profile != null ? profile.faunaMood.ToString() : "None";
        }

        private static string GetItemLabel(Hecton8.Items.ItemData item)
        {
            if (item == null)
                return "None";

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }
    }
}
