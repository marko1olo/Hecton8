using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class BeaconDeployerTool : PlayerTool
    {
        private readonly struct BeaconAssessment
        {
            public readonly string Role;
            public readonly string Summary;
            public readonly string Recommendation;

            public BeaconAssessment(string role, string summary, string recommendation)
            {
                Role = role;
                Summary = summary;
                Recommendation = recommendation;
            }

            public string BuildHudMessage(string label)
            {
                return $"{label} - {Role} | {Summary} | {Recommendation}";
            }
        }

        private const string DefaultBeaconPrefix = "BEACON";
        private const string DefaultToolName = "BEACON TOOL";
        private const string DefaultNoActiveMarkers = "BEACON NET - NO ACTIVE MARKERS";
        private const string DefaultNoBeaconLabel = "NO BEACON";

        [Header("Deployment")]
        [SerializeField] private float deployRange = 12f;
        [SerializeField] private float deployCooldown = 0.25f;
        [SerializeField] private float retractRange = 6f;
        [SerializeField] private int maxActiveBeacons = 24;
        [SerializeField] private LayerMask deploymentMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        [SerializeField] private GameObject worldBeaconPrefab;
        [SerializeField] private float feedbackInterval = 0.45f;

        [Header("Fallback Beacon")]
        [SerializeField] private Color beaconColor = new Color(0.25f, 1f, 0.95f, 1f);
        [SerializeField] private Vector3 beaconScale = new Vector3(0.22f, 0.45f, 0.22f);
        [SerializeField] private float fallbackLightRange = 4f;

        private Transform _cachedTransform;
        private float _cooldown;
        private float _nextFeedbackAt;
        [SerializeField] private int _debugActiveBeaconCount;
        private readonly BeaconNetworkSystem.BeaconSnapshot[] _beaconBuffer = new BeaconNetworkSystem.BeaconSnapshot[32];
        private int _cachedNearestAssessmentFrame = -1;
        private bool _cachedNearestAssessmentValid;
        private string _cachedNearestLabel;
        private float _cachedNearestDistance;
        private BeaconAssessment _cachedNearestAssessment;
        private int _cachedOperationalTextFrame = -1;
        private string _cachedOperationalSummary;
        private string _cachedOperationalDirective;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private WorldZoneDirector _worldZoneDirector;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (TryGetDeploymentHit(out RaycastHit hit))
            {
                spawnPosition = hit.point + hit.normal * 0.08f;
                spawnRotation = Quaternion.LookRotation(hit.normal);
            }
            else
            {
                spawnPosition = _cachedTransform.position + _cachedTransform.forward * 4f;
                spawnRotation = Quaternion.identity;
            }

            if (BeaconNetworkSystem.TryDeployBeacon(
                worldBeaconPrefab,
                spawnPosition,
                spawnRotation,
                beaconColor,
                fallbackLightRange,
                beaconScale,
                maxActiveBeacons,
                out BeaconRuntime beacon,
                out string label))
            {
                BeaconAssessment assessment = BuildDeploymentAssessment(spawnPosition, label);
                ToolHitUtility.ShowInfo(string.Format(
                    ResolveLocalized(LocalizationKeys.BEACON_HUD_DEPLOYED, "BEACON DEPLOYED - {0} // GRID {1}"),
                    assessment.BuildHudMessage(label),
                    BeaconNetworkSystem.Instance.ActiveCount));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                    ResolveLocalized(LocalizationKeys.BEACON_LOG_DEPLOYED_TITLE, "FIELD BEACON DEPLOYED"),
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_LOG_DEPLOYED_MESSAGE,
                            "{0} established at {1:0.0}, {2:0.0}, {3:0.0}. {4} Recommendation: {5}. Active marker count: {6}."),
                        label,
                        spawnPosition.x,
                        spawnPosition.y,
                        spawnPosition.z,
                        assessment.Summary,
                        assessment.Recommendation,
                        BeaconNetworkSystem.Instance.ActiveCount),
                    "INFO");
                InvalidateNearestAssessmentCache();
                _cooldown = deployCooldown;
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (BeaconNetworkSystem.Instance == null || BeaconNetworkSystem.Instance.ActiveCount == 0)
            {
                if (Time.time >= _nextFeedbackAt)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.BEACON_HUD_NO_ACTIVE, DefaultNoActiveMarkers));
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                return;
            }

            if (!BeaconNetworkSystem.TryGetNearest(_cachedTransform.position, out BeaconNetworkSystem.BeaconSnapshot nearestSnapshot, out float nearestDistance))
                return;

            if (nearestDistance > retractRange)
            {
                if (Time.time >= _nextFeedbackAt)
                {
                    BeaconAssessment assessment = BuildExistingBeaconAssessment(nearestSnapshot, nearestDistance);
                    ToolHitUtility.ShowInfo(string.Format(
                        ResolveLocalized(LocalizationKeys.BEACON_HUD_NEAREST, "NEAREST BEACON - {0} // {1:0.0}M // GRID {2}"),
                        assessment.BuildHudMessage(nearestSnapshot.Label),
                        nearestDistance,
                        BeaconNetworkSystem.Instance.ActiveCount));
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                        ResolveLocalized(LocalizationKeys.BEACON_LOG_CHECK_TITLE, "BEACON GRID CHECK"),
                        string.Format(
                            ResolveLocalized(
                                LocalizationKeys.BEACON_LOG_CHECK_MESSAGE,
                                "{0} is the nearest active field marker at {1:0.0} m. {2} Recommendation: {3}. Close within {4:0.0} m to retract."),
                            nearestSnapshot.Label,
                            nearestDistance,
                            assessment.Summary,
                            assessment.Recommendation,
                            retractRange),
                        "INFO");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                return;
            }

            if (BeaconNetworkSystem.TryRetractNearest(_cachedTransform.position, out BeaconRuntime nearest, out float distance))
            {
                Vector3 position = nearest.transform.position;
                string label = nearest.Label;
                ToolHitUtility.ShowInfo(string.Format(
                    ResolveLocalized(LocalizationKeys.BEACON_HUD_RETRACTED, "BEACON RETRACTED - {0} // GRID {1}"),
                    label,
                    BeaconNetworkSystem.Instance.ActiveCount));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                    ResolveLocalized(LocalizationKeys.BEACON_LOG_RETRACTED_TITLE, "FIELD BEACON RETRACTED"),
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_LOG_RETRACTED_MESSAGE,
                            "{0} was retracted from {1:0.0}, {2:0.0}, {3:0.0} at {4:0.0} m. Active marker count: {5}."),
                        label,
                        position.x,
                        position.y,
                        position.z,
                        distance,
                        BeaconNetworkSystem.Instance.ActiveCount),
                    "INFO");
                InvalidateNearestAssessmentCache();
                _cooldown = deployCooldown;
            }
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

            _debugActiveBeaconCount = BeaconNetworkSystem.Instance != null
                ? BeaconNetworkSystem.Instance.ActiveCount
                : 0;
        }

        public override string GetOperationalSummary()
        {
            RefreshOperationalTextCache();
            return _cachedOperationalSummary;
        }

        public override string GetOperationalDirective()
        {
            RefreshOperationalTextCache();
            return _cachedOperationalDirective;
        }

        private BeaconAssessment BuildDeploymentAssessment(Vector3 spawnPosition, string label)
        {
            if (TryReadRouteMarkerAssessment(spawnPosition, out BeaconAssessment routeAssessment))
            {
                return new BeaconAssessment(
                    routeAssessment.Role,
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        label,
                        routeAssessment.Summary),
                    routeAssessment.Recommendation);
            }

            if (BeaconNetworkSystem.Instance == null || BeaconNetworkSystem.Instance.ActiveCount <= 1)
            {
                return new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_ANCHOR, "ANCHOR"),
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_FIRST_ANCHOR,
                            "{0} is acting as the first navigation anchor in the current sector."),
                        label),
                    ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_BUILD_OUTWARD, "Build the network outward from this point."));
            }

            if (!TryGetNearestNeighbor(spawnPosition, label, out BeaconNetworkSystem.BeaconSnapshot nearest, out float nearestDistance))
            {
                return new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_ANCHOR, "ANCHOR"),
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_STANDALONE_ANCHOR,
                            "{0} could not resolve a neighbor and is acting as a standalone anchor."),
                        label),
                    ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_CONFIRM_ROUTE, "Confirm line of travel before extending the grid."));
            }

            string role = ClassifyRole(nearestDistance);
            string summary = string.Format(
                ResolveLocalized(
                    LocalizationKeys.BEACON_SUMMARY_EXTENDS_GRID,
                    "{0} extends the grid from {1} by {2:0.0} m."),
                label,
                nearest.Label,
                nearestDistance);
            string recommendation = role switch
            {
                var localMark when localMark == ResolveLocalized(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK")
                    => ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_LOCAL_MARK, "Use it to tag dense loot, cave turns, or salvage clusters."),
                var relay when relay == ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                    => ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_RELAY, "Use it to bridge a travel lane or a return path."),
                _ => ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_FRONTIER, "Use it as a frontier marker for deep progression or retreat routing.")
            };
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryGetDeploymentHit(out RaycastHit hit)
        {
            return TryResolveQueuedRaycast(_cachedTransform.position, _cachedTransform.forward, deployRange, deploymentMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private BeaconAssessment BuildExistingBeaconAssessment(BeaconNetworkSystem.BeaconSnapshot snapshot, float distance)
        {
            if (TryReadRouteMarkerAssessment(snapshot.Position, out BeaconAssessment routeAssessment))
            {
                string routeRecommendation = distance <= retractRange
                    ? ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_RETRACT_ROUTE_NOW, "You are inside recovery distance and can retract or reposition this route marker now.")
                    : routeAssessment.Recommendation;

                return new BeaconAssessment(
                    routeAssessment.Role,
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        snapshot.Label,
                        routeAssessment.Summary),
                    routeRecommendation);
            }

            string role = ClassifyRole(distance);
            string summary = role switch
            {
                var localMark when localMark == ResolveLocalized(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK")
                    => string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_LOCAL_MARK,
                            "{0} is a close-range marker for nearby loot, turns, or hazards."),
                        snapshot.Label),
                var relay when relay == ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                    => string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_RELAY,
                            "{0} is holding a mid-range travel lane through the sector."),
                        snapshot.Label),
                _ => string.Format(
                    ResolveLocalized(
                        LocalizationKeys.BEACON_SUMMARY_FRONTIER,
                        "{0} is acting as a frontier marker deeper into the field."),
                    snapshot.Label)
            };
            string recommendation = distance <= retractRange
                ? ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_RETRACT_NOW, "You are inside recovery distance and can retract it now.")
                : ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_LEAVE_ACTIVE, "Leave it active unless you are collapsing this route.");
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryReadRouteMarkerAssessment(Vector3 position, out BeaconAssessment assessment)
        {
            assessment = default;

            if (!FieldTargetSemantics.TryFindNearestRouteMarker(position, 5f, out FieldTargetDescriptor nearest, out _))
                return false;

            assessment = new BeaconAssessment(
                FieldTargetSemantics.BuildRouteRoleLabel(nearest.Role),
                FieldTargetSemantics.BuildDescriptorSummary(
                    nearest,
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_ALIGNED,
                            "{0} is the nearest authored route guide."),
                        nearest.name)),
                FieldTargetSemantics.BuildRouteRecommendation(nearest.Role));
            return true;
        }

        private string ClassifyRole(float distance)
        {
            if (distance <= 12f)
                return ResolveLocalized(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK");
            if (distance <= 35f)
                return ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY");
            return ResolveLocalized(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER");
        }

        private bool TryGetNearestNeighbor(Vector3 origin, string excludeLabel, out BeaconNetworkSystem.BeaconSnapshot snapshot, out float distance)
        {
            snapshot = default;
            distance = 0f;

            if (BeaconNetworkSystem.Instance == null)
                return false;

            int count = BeaconNetworkSystem.Instance.CopySnapshots(_beaconBuffer);
            float bestSqr = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                BeaconNetworkSystem.BeaconSnapshot candidate = _beaconBuffer[i];
                if (string.IsNullOrWhiteSpace(candidate.Label) ||
                    string.Equals(candidate.Label, excludeLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float sqr = (candidate.Position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    snapshot = candidate;
                    found = true;
                }
            }

            if (!found)
                return false;

            distance = Mathf.Sqrt(bestSqr);
            return true;
        }

        private bool TryReadNearestAssessment(out string label, out float distance, out BeaconAssessment assessment)
        {
            label = ResolveLocalized(LocalizationKeys.BEACON_LABEL_NONE, DefaultNoBeaconLabel);
            distance = 0f;
            assessment = default;

            if (BeaconNetworkSystem.Instance == null || BeaconNetworkSystem.Instance.ActiveCount == 0)
                return false;

            if (!BeaconNetworkSystem.TryGetNearest(_cachedTransform.position, out BeaconNetworkSystem.BeaconSnapshot nearest, out distance))
                return false;

            label = nearest.Label;
            assessment = BuildExistingBeaconAssessment(nearest, distance);
            return true;
        }

        private bool TryGetNearestAssessmentCached(out string label, out float distance, out BeaconAssessment assessment)
        {
            int currentFrame = Time.frameCount;
            if (_cachedNearestAssessmentFrame == currentFrame)
            {
                label = _cachedNearestLabel;
                distance = _cachedNearestDistance;
                assessment = _cachedNearestAssessment;
                return _cachedNearestAssessmentValid;
            }

            bool valid = TryReadNearestAssessment(out label, out distance, out assessment);
            _cachedNearestAssessmentFrame = currentFrame;
            _cachedNearestAssessmentValid = valid;
            _cachedNearestLabel = label;
            _cachedNearestDistance = distance;
            _cachedNearestAssessment = assessment;
            return valid;
        }

        private void InvalidateNearestAssessmentCache()
        {
            _cachedNearestAssessmentFrame = -1;
            _cachedNearestAssessmentValid = false;
            _cachedNearestLabel = null;
            _cachedNearestDistance = 0f;
            _cachedNearestAssessment = default;
            _cachedOperationalTextFrame = -1;
            _cachedOperationalSummary = null;
            _cachedOperationalDirective = null;
        }

        private void RefreshOperationalTextCache()
        {
            int currentFrame = Time.frameCount;
            if (_cachedOperationalTextFrame == currentFrame)
                return;

            int activeCount = BeaconNetworkSystem.Instance != null ? BeaconNetworkSystem.Instance.ActiveCount : 0;
            if (_cooldown > 0f)
            {
                _cachedOperationalSummary = string.Format(
                    ResolveLocalized(LocalizationKeys.BEACON_OPERATIONAL_COOLDOWN, "BEACON TOOL // GRID {0} // CYCLING {1:0.0}S"),
                    activeCount,
                    _cooldown);
                _cachedOperationalDirective = ResolveLocalized(
                    LocalizationKeys.BEACON_OPERATIONAL_COOLDOWN_DIRECTIVE,
                    "Wait for deployment hardware to reset.");
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            if (TryGetNearestAssessmentCached(out string label, out float distance, out BeaconAssessment assessment))
            {
                _cachedOperationalSummary = string.Format(
                    ResolveLocalized(LocalizationKeys.BEACON_OPERATIONAL_NEAREST, "BEACON TOOL // {0} // {1} {2:0.0}M"),
                    assessment.Role,
                    label,
                    distance);
                _cachedOperationalDirective = assessment.Recommendation;
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            ResolveRuntimeContext();
            if (TryBuildContextualReadyAssessment(out BeaconAssessment contextualAssessment))
            {
                _cachedOperationalSummary = string.Format(
                    "BEACON TOOL // {0} // READY",
                    contextualAssessment.Role);
                _cachedOperationalDirective = contextualAssessment.Recommendation;
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            _cachedOperationalSummary = string.Format(
                ResolveLocalized(LocalizationKeys.BEACON_OPERATIONAL_READY, "BEACON TOOL // GRID {0} // READY"),
                activeCount);
            _cachedOperationalDirective = ResolveLocalized(
                LocalizationKeys.BEACON_OPERATIONAL_READY_DIRECTIVE,
                "Primary deploys a route marker. Secondary checks or retracts the nearest beacon.");
            _cachedOperationalTextFrame = currentFrame;
        }

        private void ResolveRuntimeContext()
        {
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);
        }

        private bool TryBuildContextualReadyAssessment(out BeaconAssessment assessment)
        {
            assessment = default;

            WorldZoneAnchor zone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            HectonBiomeMatrixProfile biome = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;

            if (zone != null &&
                zone.RouteCritical &&
                biome != null &&
                biome.survivalPressure >= 3 &&
                !string.IsNullOrWhiteSpace(biome.safePocketIdentity))
            {
                assessment = new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY"),
                    biome.safePocketIdentity,
                    biome.safePocketIdentity);
                return true;
            }

            if (zone != null &&
                (zone.Kind == WorldZoneAnchor.ZoneKind.Navigation || zone.Kind == WorldZoneAnchor.ZoneKind.Progression) &&
                biome != null &&
                !string.IsNullOrWhiteSpace(biome.landmarkGuidance))
            {
                assessment = new BeaconAssessment(
                    zone.RouteCritical
                        ? ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                        : ResolveLocalized(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER"),
                    string.IsNullOrWhiteSpace(zone.GameplayIntent) ? biome.landmarkGuidance : zone.GameplayIntent,
                    biome.landmarkGuidance);
                return true;
            }

            if (biome != null &&
                biome.rewardPull >= 4 &&
                !string.IsNullOrWhiteSpace(biome.rareRewardHook))
            {
                assessment = new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER"),
                    biome.rareRewardHook,
                    biome.rareRewardHook);
                return true;
            }

            if (biome != null &&
                !string.IsNullOrWhiteSpace(biome.commonRewardHook))
            {
                assessment = new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK"),
                    biome.commonRewardHook,
                    biome.commonRewardHook);
                return true;
            }

            return false;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
