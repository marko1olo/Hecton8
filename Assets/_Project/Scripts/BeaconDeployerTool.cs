using Hecton8.Core;
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

        [Header("Deployment")]
        [SerializeField] private float deployRange = 12f;
        [SerializeField] private float deployCooldown = 0.25f;
        [SerializeField] private float retractRange = 6f;
        [SerializeField] private int maxActiveBeacons = 24;
        [SerializeField] private LayerMask deploymentMask = ~0;
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
        private readonly RaycastHit[] _deploymentHits = new RaycastHit[1]; // COLD ALLOC: beacon placement resolves a single hit per use.
        private int _cachedNearestAssessmentFrame = -1;
        private bool _cachedNearestAssessmentValid;
        private string _cachedNearestLabel;
        private float _cachedNearestDistance;
        private BeaconAssessment _cachedNearestAssessment;
        private int _cachedOperationalTextFrame = -1;
        private string _cachedOperationalSummary;
        private string _cachedOperationalDirective;

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
                ToolHitUtility.ShowInfo($"BEACON DEPLOYED - {assessment.BuildHudMessage(label)} // GRID {BeaconNetworkSystem.Instance.ActiveCount}");
                FieldOperationLogSystem.RecordOperation(
                    "BEACON",
                    "FIELD BEACON DEPLOYED",
                    $"{label} established at {spawnPosition.x:0.0}, {spawnPosition.y:0.0}, {spawnPosition.z:0.0}. {assessment.Summary} Recommendation: {assessment.Recommendation}. Active marker count: {BeaconNetworkSystem.Instance.ActiveCount}.",
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
                    ToolHitUtility.ShowWarning("BEACON NET - NO ACTIVE MARKERS");
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
                    ToolHitUtility.ShowInfo($"NEAREST BEACON - {assessment.BuildHudMessage(nearestSnapshot.Label)} // {nearestDistance:0.0}M // GRID {BeaconNetworkSystem.Instance.ActiveCount}");
                    FieldOperationLogSystem.RecordOperation(
                        "BEACON",
                        "BEACON GRID CHECK",
                        $"{nearestSnapshot.Label} is the nearest active field marker at {nearestDistance:0.0} m. {assessment.Summary} Recommendation: {assessment.Recommendation}. Close within {retractRange:0.0} m to retract.",
                        "INFO");
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                return;
            }

            if (BeaconNetworkSystem.TryRetractNearest(_cachedTransform.position, out BeaconRuntime nearest, out float distance))
            {
                Vector3 position = nearest.transform.position;
                string label = nearest.Label;
                ToolHitUtility.ShowInfo($"BEACON RETRACTED - {label} // GRID {BeaconNetworkSystem.Instance.ActiveCount}");
                FieldOperationLogSystem.RecordOperation(
                    "BEACON",
                    "FIELD BEACON RETRACTED",
                    $"{label} was retracted from {position.x:0.0}, {position.y:0.0}, {position.z:0.0} at {distance:0.0} m. Active marker count: {BeaconNetworkSystem.Instance.ActiveCount}.",
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
                    $"{label} aligned with authored route guidance. {routeAssessment.Summary}",
                    routeAssessment.Recommendation);
            }

            if (BeaconNetworkSystem.Instance == null || BeaconNetworkSystem.Instance.ActiveCount <= 1)
            {
                return new BeaconAssessment(
                    "ANCHOR",
                    $"{label} is acting as the first navigation anchor in the current sector.",
                    "Build the network outward from this point.");
            }

            if (!TryGetNearestNeighbor(spawnPosition, label, out BeaconNetworkSystem.BeaconSnapshot nearest, out float nearestDistance))
            {
                return new BeaconAssessment(
                    "ANCHOR",
                    $"{label} could not resolve a neighbor and is acting as a standalone anchor.",
                    "Confirm line of travel before extending the grid.");
            }

            string role = ClassifyRole(nearestDistance);
            string summary = $"{label} extends the grid from {nearest.Label} by {nearestDistance:0.0} m.";
            string recommendation = role switch
            {
                "LOCAL MARK" => "Use it to tag dense loot, cave turns, or salvage clusters.",
                "RELAY" => "Use it to bridge a travel lane or a return path.",
                _ => "Use it as a frontier marker for deep progression or retreat routing."
            };
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryGetDeploymentHit(out RaycastHit hit)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cachedTransform.position,
                _cachedTransform.forward,
                _deploymentHits,
                deployRange,
                deploymentMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                hit = _deploymentHits[0];
                return true;
            }

            hit = default;
            return false;
        }

        private BeaconAssessment BuildExistingBeaconAssessment(BeaconNetworkSystem.BeaconSnapshot snapshot, float distance)
        {
            if (TryReadRouteMarkerAssessment(snapshot.Position, out BeaconAssessment routeAssessment))
            {
                string routeRecommendation = distance <= retractRange
                    ? "You are inside recovery distance and can retract or reposition this route marker now."
                    : routeAssessment.Recommendation;

                return new BeaconAssessment(
                    routeAssessment.Role,
                    $"{snapshot.Label} sits on authored route guidance. {routeAssessment.Summary}",
                    routeRecommendation);
            }

            string role = ClassifyRole(distance);
            string summary = role switch
            {
                "LOCAL MARK" => $"{snapshot.Label} is a close-range marker for nearby loot, turns, or hazards.",
                "RELAY" => $"{snapshot.Label} is holding a mid-range travel lane through the sector.",
                _ => $"{snapshot.Label} is acting as a frontier marker deeper into the field."
            };
            string recommendation = distance <= retractRange
                ? "You are inside recovery distance and can retract it now."
                : "Leave it active unless you are collapsing this route.";
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryReadRouteMarkerAssessment(Vector3 position, out BeaconAssessment assessment)
        {
            assessment = default;

            if (!FieldTargetSemantics.TryFindNearestRouteMarker(position, 5f, out FieldTargetDescriptor nearest, out _))
                return false;

            assessment = new BeaconAssessment(
                FieldTargetSemantics.BuildRouteRoleLabel(nearest.Role),
                FieldTargetSemantics.BuildDescriptorSummary(nearest, $"{nearest.name} is the nearest authored route guide."),
                FieldTargetSemantics.BuildRouteRecommendation(nearest.Role));
            return true;
        }

        private string ClassifyRole(float distance)
        {
            if (distance <= 12f)
                return "LOCAL MARK";
            if (distance <= 35f)
                return "RELAY";
            return "FRONTIER";
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
            label = "NO BEACON";
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
                _cachedOperationalSummary = $"BEACON TOOL // GRID {activeCount} // CYCLING {_cooldown:0.0}S";
                _cachedOperationalDirective = "Wait for deployment hardware to reset.";
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            if (TryGetNearestAssessmentCached(out string label, out float distance, out BeaconAssessment assessment))
            {
                _cachedOperationalSummary = $"BEACON TOOL // {assessment.Role} // {label} {distance:0.0}M";
                _cachedOperationalDirective = assessment.Recommendation;
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            _cachedOperationalSummary = $"BEACON TOOL // GRID {activeCount} // READY";
            _cachedOperationalDirective = "Primary deploys a route marker. Secondary checks or retracts the nearest beacon.";
            _cachedOperationalTextFrame = currentFrame;
        }
    }
}
