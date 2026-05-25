using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class BeaconDeployerTool : PlayerTool
    {
        private readonly struct BeaconTextSegment
        {
            public const byte HasStringArg0 = 1 << 0;
            public const byte HasStringArg1 = 1 << 1;
            public const byte HasFloatArg2 = 1 << 2;

            public readonly string Template;
            private readonly string _stringArg0;
            private readonly string _stringArg1;
            private readonly float _floatArg2;
            private readonly byte _argumentMask;

            public BeaconTextSegment(string template)
            {
                Template = template;
                _stringArg0 = null;
                _stringArg1 = null;
                _floatArg2 = 0f;
                _argumentMask = 0;
            }

            private BeaconTextSegment(string template, string stringArg0, string stringArg1, float floatArg2, byte argumentMask)
            {
                Template = template;
                _stringArg0 = stringArg0;
                _stringArg1 = stringArg1;
                _floatArg2 = floatArg2;
                _argumentMask = argumentMask;
            }

            public static BeaconTextSegment FormatString(string template, string arg0)
            {
                return new BeaconTextSegment(template, arg0, null, 0f, HasStringArg0);
            }

            public static BeaconTextSegment FormatStringString(string template, string arg0, string arg1)
            {
                return new BeaconTextSegment(template, arg0, arg1, 0f, HasStringArg0 | HasStringArg1);
            }

            public static BeaconTextSegment FormatStringStringFloat(string template, string arg0, string arg1, float arg2)
            {
                return new BeaconTextSegment(template, arg0, arg1, arg2, HasStringArg0 | HasStringArg1 | HasFloatArg2);
            }

            public bool TryWrite(ref FixedCharBuffer buffer)
            {
                return BeaconDeployerTool.AppendFormattedText(ref buffer, Template, _stringArg0, _stringArg1, _floatArg2, _argumentMask);
            }
        }

        private readonly struct BeaconAssessment
        {
            public readonly string Role;
            public readonly BeaconTextSegment SummaryText;
            public readonly string Recommendation;

            public BeaconAssessment(string role, string summary, string recommendation)
                : this(role, new BeaconTextSegment(summary), recommendation)
            {
            }

            public BeaconAssessment(string role, BeaconTextSegment summary, string recommendation)
            {
                Role = role;
                SummaryText = summary;
                Recommendation = recommendation;
            }

            public string Summary => SummaryText.Template;

            public bool TryWriteHudMessage(ref FixedCharBuffer buffer, string label)
            {
                return AppendText(ref buffer, label) &&
                       AppendText(ref buffer, " - ") &&
                       AppendText(ref buffer, Role) &&
                       AppendText(ref buffer, " | ") &&
                       TryWriteSummary(ref buffer) &&
                       AppendText(ref buffer, " | ") &&
                       AppendText(ref buffer, Recommendation);
            }

            public bool TryWriteSummary(ref FixedCharBuffer buffer) => SummaryText.TryWrite(ref buffer);
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

        private float _cooldown;
        private float _feedbackCooldownRemaining;
        [SerializeField] private int _debugActiveBeaconCount;
        private readonly BeaconNetworkSnapshot[] _beaconBuffer = new BeaconNetworkSnapshot[32];
        private uint _nearestAssessmentEvaluationStamp;
        private uint _cachedNearestAssessmentStamp = uint.MaxValue;
        private bool _cachedNearestAssessmentValid;
        private string _cachedNearestLabel;
        private float _cachedNearestDistance;
        private BeaconAssessment _cachedNearestAssessment;
        private uint _operationalTextEvaluationStamp;
        private uint _cachedOperationalTextStamp = uint.MaxValue;
        private string _cachedOperationalDirective;
        private IBeaconNetworkService _beaconNetwork;
        private ILocalizationTextReadModel _localization;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private WorldZoneDirector _worldZoneDirector;
        private FixedCharBuffer _beaconHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - beacon HUD staging buffer - owner: BeaconDeployerTool
        private FixedCharBuffer _beaconLogBuffer = new FixedCharBuffer(768); // COLD ALLOC: char[768] - beacon operation log staging buffer - owner: BeaconDeployerTool

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheColdDependencies();
            AdvanceEvaluationStamps();
            InvalidateNearestAssessmentCache();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            CacheColdDependencies();
            AdvanceEvaluationStamps();
            InvalidateNearestAssessmentCache();
        }

        public override void OnDespawn()
        {
            _beaconNetwork = null;
            _localization = null;
            _biomeMatrixDirector = null;
            _worldZoneDirector = null;
            _feedbackCooldownRemaining = 0f;
            InvalidateNearestAssessmentCache();
            base.OnDespawn();
        }

        protected override void OnToolRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyBeaconRegistryServiceRebind(serviceSlot, currentService);
        }

        protected override void OnToolRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ApplyBeaconRegistryServiceRebind(serviceSlot, currentService);
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork) ||
                !TryResolvePlayerPose(out Vector3 poseOrigin, out Vector3 poseForward, out _))
            {
                return;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (TryGetDeploymentHit(poseOrigin, poseForward, out InteractionSurfaceHit hit))
            {
                spawnPosition = hit.point + hit.normal * 0.08f;
                spawnRotation = ResolveSafeLookRotation(hit.normal, poseForward);
            }
            else
            {
                spawnPosition = poseOrigin + (poseForward * 4f);
                spawnRotation = ResolveSafeLookRotation(poseForward, Vector3.forward);
            }

            if (beaconNetwork.TryDeployBeaconFromTool(
                worldBeaconPrefab,
                spawnPosition,
                spawnRotation,
                beaconColor,
                fallbackLightRange,
                beaconScale,
                maxActiveBeacons,
                out string label))
            {
                BeaconAssessment assessment = BuildDeploymentAssessment(spawnPosition, label);
                int activeCount = ResolveActiveBeaconCount();
                if (TryWriteBeaconDeployedHud(label, assessment, activeCount))
                    ToolHitUtility.ShowInfo(in _beaconHudBuffer);

                RecordDeploymentLog(label, spawnPosition, assessment, activeCount);
                InvalidateNearestAssessmentCache();
                _cooldown = deployCooldown;
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            if (!TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork) || beaconNetwork.ActiveCount == 0)
            {
                if (TryConsumeFeedbackGate())
                {
                    if (TryWriteNoActiveBeaconHud())
                        ToolHitUtility.ShowWarning(in _beaconHudBuffer);
                }
                return;
            }

            if (!TryResolvePlayerPose(out _, out _, out AbsoluteUniversePosition playerAup) ||
                !beaconNetwork.TryGetNearestFromTool(in playerAup, out BeaconNetworkSnapshot nearestSnapshot, out float nearestDistance))
            {
                return;
            }

            if (nearestDistance > retractRange)
            {
                if (TryConsumeFeedbackGate())
                {
                    BeaconAssessment assessment = BuildExistingBeaconAssessment(nearestSnapshot, nearestDistance);
                    if (TryWriteNearestBeaconHud(nearestSnapshot.Label, assessment, nearestDistance, ResolveActiveBeaconCount()))
                        ToolHitUtility.ShowInfo(in _beaconHudBuffer);

                    RecordBeaconCheckLog(nearestSnapshot.Label, nearestDistance, assessment);
                }
                return;
            }

            if (beaconNetwork.TryRetractNearestFromTool(in playerAup, out float distance))
            {
                Vector3 position = nearestSnapshot.Position;
                string label = nearestSnapshot.Label;
                int activeCount = ResolveActiveBeaconCount();
                if (TryWriteBeaconRetractedHud(label, activeCount))
                    ToolHitUtility.ShowInfo(in _beaconHudBuffer);

                RecordRetractionLog(label, position, distance, activeCount);
                InvalidateNearestAssessmentCache();
                _cooldown = deployCooldown;
            }
        }

        public override void ToolTick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f);

            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - safeDeltaTime);

            if (_feedbackCooldownRemaining > 0f)
                _feedbackCooldownRemaining = math.max(0f, _feedbackCooldownRemaining - safeDeltaTime);

            _debugActiveBeaconCount = _beaconNetwork != null
                ? _beaconNetwork.ActiveCount
                : 0;

            AdvanceEvaluationStamps();
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            return "BEACON TOOL";
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            int activeCount = ResolveActiveBeaconCount();
            if (_cooldown > 0f)
            {
                AppendText(ref buffer, "BEACON TOOL // GRID ");
                buffer.AppendInt(activeCount);
                AppendText(ref buffer, " // CYCLING ");
                buffer.AppendFloat(_cooldown, 1);
                AppendText(ref buffer, "S");
                return;
            }

            if (TryGetNearestAssessmentCached(out string nearestLabel, out float nearestDistance, out BeaconAssessment nearestAssessment))
            {
                AppendText(ref buffer, "BEACON TOOL // ");
                AppendText(ref buffer, nearestAssessment.Role);
                AppendText(ref buffer, " // ");
                AppendText(ref buffer, nearestLabel);
                AppendText(ref buffer, " ");
                buffer.AppendFloat(nearestDistance, 1);
                AppendText(ref buffer, "M");
                return;
            }

            if (TryBuildContextualReadyAssessment(out BeaconAssessment contextualAssessment))
            {
                AppendText(ref buffer, "BEACON TOOL // ");
                AppendText(ref buffer, contextualAssessment.Role);
                AppendText(ref buffer, " // READY");
                return;
            }

            AppendText(ref buffer, "BEACON TOOL // GRID ");
            buffer.AppendInt(activeCount);
            AppendText(ref buffer, " // READY");
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            RefreshOperationalDirectiveCache();
            return _cachedOperationalDirective;
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            RefreshOperationalDirectiveCache();
            AppendText(ref buffer, _cachedOperationalDirective);
        }

        private BeaconAssessment BuildDeploymentAssessment(Vector3 spawnPosition, string label)
        {
            if (TryReadRouteMarkerAssessment(spawnPosition, out BeaconAssessment routeAssessment))
            {
                return new BeaconAssessment(
                    routeAssessment.Role,
                    BeaconTextSegment.FormatStringString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        label,
                        routeAssessment.Summary),
                    routeAssessment.Recommendation);
            }

            if (!TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork) || beaconNetwork.ActiveCount <= 1)
            {
                return new BeaconAssessment(
                    StableText(LocalizationKeys.BEACON_ROLE_ANCHOR, "ANCHOR"),
                    BeaconTextSegment.FormatString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_FIRST_ANCHOR,
                            "{0} is acting as the first navigation anchor in the current sector."),
                        label),
                    StableText(LocalizationKeys.BEACON_RECOMMEND_BUILD_OUTWARD, "Build the network outward from this point."));
            }

            if (!TryGetNearestNeighbor(spawnPosition, label, out BeaconNetworkSnapshot nearest, out float nearestDistance))
            {
                return new BeaconAssessment(
                    StableText(LocalizationKeys.BEACON_ROLE_ANCHOR, "ANCHOR"),
                    BeaconTextSegment.FormatString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_STANDALONE_ANCHOR,
                            "{0} could not resolve a neighbor and is acting as a standalone anchor."),
                        label),
                    StableText(LocalizationKeys.BEACON_RECOMMEND_CONFIRM_ROUTE, "Confirm line of travel before extending the grid."));
            }

            string role = ClassifyRole(nearestDistance);
            BeaconTextSegment summary = BeaconTextSegment.FormatStringStringFloat(
                StableText(
                    LocalizationKeys.BEACON_SUMMARY_EXTENDS_GRID,
                    "{0} extends the grid from {1} by {2:0.0} m."),
                label,
                nearest.Label,
                nearestDistance);
            string recommendation = role switch
            {
                var localMark when localMark == StableText(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK")
                    => StableText(LocalizationKeys.BEACON_RECOMMEND_LOCAL_MARK, "Use it to tag dense loot, cave turns, or salvage clusters."),
                var relay when relay == StableText(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                    => StableText(LocalizationKeys.BEACON_RECOMMEND_RELAY, "Use it to bridge a travel lane or a return path."),
                _ => StableText(LocalizationKeys.BEACON_RECOMMEND_FRONTIER, "Use it as a frontier marker for deep progression or retreat routing.")
            };
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryGetDeploymentHit(Vector3 origin, Vector3 direction, out InteractionSurfaceHit hit)
        {
            return TryResolvePrimarySurfaceHit(origin, direction, deployRange, deploymentMask.value, QueryTriggerInteraction.Ignore, out hit);
        }

        private bool TryWriteBeaconDeployedHud(string label, BeaconAssessment assessment, int activeCount)
        {
            _beaconHudBuffer.Clear();
            return AppendText(ref _beaconHudBuffer, "BEACON DEPLOYED - ") &&
                   assessment.TryWriteHudMessage(ref _beaconHudBuffer, label) &&
                   AppendText(ref _beaconHudBuffer, " // GRID ") &&
                   _beaconHudBuffer.AppendInt(activeCount);
        }

        private bool TryWriteNearestBeaconHud(string label, BeaconAssessment assessment, float distance, int activeCount)
        {
            _beaconHudBuffer.Clear();
            return AppendText(ref _beaconHudBuffer, "NEAREST BEACON - ") &&
                   assessment.TryWriteHudMessage(ref _beaconHudBuffer, label) &&
                   AppendText(ref _beaconHudBuffer, " // ") &&
                   _beaconHudBuffer.AppendFloat(distance, 1) &&
                   AppendText(ref _beaconHudBuffer, "M // GRID ") &&
                   _beaconHudBuffer.AppendInt(activeCount);
        }

        private bool TryWriteBeaconRetractedHud(string label, int activeCount)
        {
            _beaconHudBuffer.Clear();
            return AppendText(ref _beaconHudBuffer, "BEACON RETRACTED - ") &&
                   AppendText(ref _beaconHudBuffer, label) &&
                   AppendText(ref _beaconHudBuffer, " // GRID ") &&
                   _beaconHudBuffer.AppendInt(activeCount);
        }

        private bool TryWriteNoActiveBeaconHud()
        {
            _beaconHudBuffer.Clear();
            return AppendText(ref _beaconHudBuffer, StableText(LocalizationKeys.BEACON_HUD_NO_ACTIVE, DefaultNoActiveMarkers));
        }

        private int ResolveActiveBeaconCount()
        {
            return _beaconNetwork != null
                ? _beaconNetwork.ActiveCount
                : 0;
        }

        private BeaconAssessment BuildExistingBeaconAssessment(BeaconNetworkSnapshot snapshot, float distance)
        {
            if (TryReadRouteMarkerAssessment(snapshot.Position, out BeaconAssessment routeAssessment))
            {
                string routeRecommendation = distance <= retractRange
                    ? StableText(LocalizationKeys.BEACON_RECOMMEND_RETRACT_ROUTE_NOW, "You are inside recovery distance and can retract or reposition this route marker now.")
                    : routeAssessment.Recommendation;

                return new BeaconAssessment(
                    routeAssessment.Role,
                    BeaconTextSegment.FormatStringString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        snapshot.Label,
                        routeAssessment.Summary),
                    routeRecommendation);
            }

            string role = ClassifyRole(distance);
            BeaconTextSegment summary = role switch
            {
                var localMark when localMark == StableText(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK")
                    => BeaconTextSegment.FormatString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_LOCAL_MARK,
                            "{0} is a close-range marker for nearby loot, turns, or hazards."),
                        snapshot.Label),
                var relay when relay == StableText(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                    => BeaconTextSegment.FormatString(
                        StableText(
                            LocalizationKeys.BEACON_SUMMARY_RELAY,
                            "{0} is holding a mid-range travel lane through the sector."),
                        snapshot.Label),
                _ => BeaconTextSegment.FormatString(
                    StableText(
                        LocalizationKeys.BEACON_SUMMARY_FRONTIER,
                        "{0} is acting as a frontier marker deeper into the field."),
                    snapshot.Label)
            };
            string recommendation = distance <= retractRange
                ? StableText(LocalizationKeys.BEACON_RECOMMEND_RETRACT_NOW, "You are inside recovery distance and can retract it now.")
                : StableText(LocalizationKeys.BEACON_RECOMMEND_LEAVE_ACTIVE, "Leave it active unless you are collapsing this route.");
            return new BeaconAssessment(role, summary, recommendation);
        }

        private bool TryReadRouteMarkerAssessment(Vector3 position, out BeaconAssessment assessment)
        {
            assessment = default;

            if (!FieldTargetSemantics.TryFindNearestRouteMarkerSq(position, 5f, out FieldTargetDescriptor nearest, out _))
                return false;

            assessment = new BeaconAssessment(
                FieldTargetSemantics.BuildRouteRoleLabel(nearest.Role),
                FieldTargetSemantics.BuildDescriptorSummary(
                    nearest,
                    StableText(
                        LocalizationKeys.BEACON_SUMMARY_ROUTE_ALIGNED,
                        "Authored route guide is inside beacon alignment range.")),
                FieldTargetSemantics.BuildRouteRecommendation(nearest.Role));
            return true;
        }

        private string ClassifyRole(float distance)
        {
            if (distance <= 12f)
                return StableText(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK");
            if (distance <= 35f)
                return StableText(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY");
            return StableText(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER");
        }

        private bool TryGetNearestNeighbor(Vector3 origin, string excludeLabel, out BeaconNetworkSnapshot snapshot, out float distance)
        {
            snapshot = default;
            distance = 0f;

            if (!TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork))
                return false;

            int count = beaconNetwork.CopySnapshots(_beaconBuffer);
            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
                return false;

            double bestDistanceSq = double.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                BeaconNetworkSnapshot candidate = _beaconBuffer[i];
                if (string.IsNullOrWhiteSpace(candidate.Label) ||
                    string.Equals(candidate.Label, excludeLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AbsoluteUniversePosition candidateAup = candidate.PositionAup;
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    snapshot = candidate;
                    found = true;
                }
            }

            if (!found)
                return false;

            distance = ApproximateDistance(bestDistanceSq);
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(aup.ToAbsoluteDouble3()));
        }

        private bool TryReadNearestAssessment(out string label, out float distance, out BeaconAssessment assessment)
        {
            label = StableText(LocalizationKeys.BEACON_LABEL_NONE, DefaultNoBeaconLabel);
            distance = 0f;
            assessment = default;

            if (!TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork) || beaconNetwork.ActiveCount == 0)
                return false;

            if (!TryResolvePlayerPose(out _, out _, out AbsoluteUniversePosition playerAup) ||
                !beaconNetwork.TryGetNearestFromTool(in playerAup, out BeaconNetworkSnapshot nearest, out distance))
            {
                return false;
            }

            label = nearest.Label;
            assessment = BuildExistingBeaconAssessment(nearest, distance);
            return true;
        }

        private bool TryGetNearestAssessmentCached(out string label, out float distance, out BeaconAssessment assessment)
        {
            uint currentStamp = _nearestAssessmentEvaluationStamp;
            if (_cachedNearestAssessmentStamp == currentStamp)
            {
                label = _cachedNearestLabel;
                distance = _cachedNearestDistance;
                assessment = _cachedNearestAssessment;
                return _cachedNearestAssessmentValid;
            }

            bool valid = TryReadNearestAssessment(out label, out distance, out assessment);
            _cachedNearestAssessmentStamp = currentStamp;
            _cachedNearestAssessmentValid = valid;
            _cachedNearestLabel = label;
            _cachedNearestDistance = distance;
            _cachedNearestAssessment = assessment;
            return valid;
        }

        private void InvalidateNearestAssessmentCache()
        {
            _cachedNearestAssessmentStamp = uint.MaxValue;
            _cachedNearestAssessmentValid = false;
            _cachedNearestLabel = null;
            _cachedNearestDistance = 0f;
            _cachedNearestAssessment = default;
            _cachedOperationalTextStamp = uint.MaxValue;
            _cachedOperationalDirective = null;
        }

        private void RefreshOperationalDirectiveCache()
        {
            uint currentStamp = _operationalTextEvaluationStamp;
            if (_cachedOperationalTextStamp == currentStamp)
                return;

            if (_cooldown > 0f)
            {
                _cachedOperationalDirective = StableText(
                    LocalizationKeys.BEACON_OPERATIONAL_COOLDOWN_DIRECTIVE,
                    "Wait for deployment hardware to reset.");
                _cachedOperationalTextStamp = currentStamp;
                return;
            }

            if (TryGetNearestAssessmentCached(out _, out _, out BeaconAssessment assessment))
            {
                _cachedOperationalDirective = assessment.Recommendation;
                _cachedOperationalTextStamp = currentStamp;
                return;
            }

            if (TryBuildContextualReadyAssessment(out BeaconAssessment contextualAssessment))
            {
                _cachedOperationalDirective = contextualAssessment.Recommendation;
                _cachedOperationalTextStamp = currentStamp;
                return;
            }

            _cachedOperationalDirective = StableText(
                LocalizationKeys.BEACON_OPERATIONAL_READY_DIRECTIVE,
                "Primary deploys a route marker. Secondary checks or retracts the nearest beacon.");
            _cachedOperationalTextStamp = currentStamp;
        }

        private void CacheColdDependencies()
        {
            _beaconNetwork = Hecton8.Core.GlobalRegistry.BeaconNetworkService;
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);
        }

        private void ApplyBeaconRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime:
                    _beaconNetwork = currentService as IBeaconNetworkService;
                    InvalidateNearestAssessmentCache();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    InvalidateNearestAssessmentCache();
                    break;
            }
        }

        private bool TryGetBeaconNetwork(out IBeaconNetworkService beaconNetwork)
        {
            beaconNetwork = _beaconNetwork;
            return beaconNetwork != null;
        }

        private bool TryResolvePlayerPose(out Vector3 origin, out Vector3 direction, out AbsoluteUniversePosition aup)
        {
            origin = default;
            direction = default;
            aup = default;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(forward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f ||
                !math.all(math.isfinite(snapshot.Aup.ToAbsoluteDouble3())))
            {
                return false;
            }

            float invForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            direction = new Vector3(
                forward.x * invForwardLength,
                forward.y * invForwardLength,
                forward.z * invForwardLength);
            aup = snapshot.Aup;
            return true;
        }

        private bool TryConsumeFeedbackGate()
        {
            if (_feedbackCooldownRemaining > 0f)
                return false;

            _feedbackCooldownRemaining = ResolveFeedbackInterval();
            return true;
        }

        private float ResolveFeedbackInterval()
        {
            float safeInterval = math.max(0.01f, math.isfinite(feedbackInterval) ? feedbackInterval : 0.45f);
            float quality = ResolveGlobalQualityWeight();
            float cadenceScale = math.lerp(1.65f, 0.85f, math.smoothstep(0f, 1f, quality));
            return safeInterval * cadenceScale;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 0.5f);
        }

        private static Quaternion ResolveSafeLookRotation(Vector3 forward, Vector3 fallback)
        {
            float3 candidate = new float3(forward.x, forward.y, forward.z);
            float candidateLengthSq = math.lengthsq(candidate);
            if (!math.all(math.isfinite(candidate)) ||
                !math.isfinite(candidateLengthSq) ||
                candidateLengthSq <= 0.0001f)
            {
                candidate = new float3(fallback.x, fallback.y, fallback.z);
                candidateLengthSq = math.lengthsq(candidate);
            }

            if (!math.all(math.isfinite(candidate)) ||
                !math.isfinite(candidateLengthSq) ||
                candidateLengthSq <= 0.0001f)
            {
                candidate = new float3(0f, 0f, 1f);
                candidateLengthSq = 1f;
            }

            float invLength = math.rsqrt(math.max(candidateLengthSq, 0.0001f));
            Vector3 safeForward = new Vector3(
                candidate.x * invLength,
                candidate.y * invLength,
                candidate.z * invLength);
            return Quaternion.LookRotation(safeForward);
        }

        private void AdvanceEvaluationStamps()
        {
            unchecked
            {
                _nearestAssessmentEvaluationStamp++;
                _operationalTextEvaluationStamp++;
            }
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
                    StableText(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY"),
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
                        ? StableText(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                        : StableText(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER"),
                    string.IsNullOrWhiteSpace(zone.GameplayIntent) ? biome.landmarkGuidance : zone.GameplayIntent,
                    biome.landmarkGuidance);
                return true;
            }

            if (biome != null &&
                biome.rewardPull >= 4 &&
                !string.IsNullOrWhiteSpace(biome.rareRewardHook))
            {
                assessment = new BeaconAssessment(
                    StableText(LocalizationKeys.BEACON_ROLE_FRONTIER, "FRONTIER"),
                    biome.rareRewardHook,
                    biome.rareRewardHook);
                return true;
            }

            if (biome != null &&
                !string.IsNullOrWhiteSpace(biome.commonRewardHook))
            {
                assessment = new BeaconAssessment(
                    StableText(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK"),
                    biome.commonRewardHook,
                    biome.commonRewardHook);
                return true;
            }

            return false;
        }

        private static string StableText(string key, string fallback)
        {
            return fallback;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool AppendFormattedText(
            ref FixedCharBuffer buffer,
            string template,
            string stringArg0,
            string stringArg1,
            float floatArg2,
            byte argumentMask)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> span = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '{' || i + 1 >= span.Length)
                    continue;

                char token = span[i + 1];
                if (token != '0' && token != '1' && token != '2')
                    continue;

                int closeIndex = i + 2;
                while (closeIndex < span.Length && span[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= span.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(span.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendFormattedArgument(ref buffer, token, stringArg0, stringArg1, floatArg2, argumentMask))
                    return false;

                i = closeIndex;
                segmentStart = closeIndex + 1;
            }

            return segmentStart >= span.Length || buffer.Append(span.Slice(segmentStart));
        }

        private static bool AppendFormattedArgument(
            ref FixedCharBuffer buffer,
            char token,
            string stringArg0,
            string stringArg1,
            float floatArg2,
            byte argumentMask)
        {
            switch (token)
            {
                case '0':
                    return (argumentMask & BeaconTextSegment.HasStringArg0) == 0 ||
                           AppendText(ref buffer, stringArg0);
                case '1':
                    return (argumentMask & BeaconTextSegment.HasStringArg1) == 0 ||
                           AppendText(ref buffer, stringArg1);
                case '2':
                    return (argumentMask & BeaconTextSegment.HasFloatArg2) == 0 ||
                           buffer.AppendFloat(floatArg2, 1);
                default:
                    return true;
            }
        }

        private static float ApproximateDistance(float distanceSq)
        {
            return ApproximateDistance((double)distanceSq);
        }

        private static float ApproximateDistance(double distanceSq)
        {
            if (distanceSq <= 0d || double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return 0f;

            if (distanceSq >= float.MaxValue)
                return float.MaxValue;

            float distanceSqFloat = (float)distanceSq;
            return distanceSqFloat * math.rsqrt(distanceSqFloat);
        }

        private bool TryWriteDeploymentLogSummary(
            ref FixedCharBuffer buffer,
            string label,
            Vector3 spawnPosition,
            BeaconAssessment assessment,
            int activeCount)
        {
            string template = StableText(
                LocalizationKeys.BEACON_LOG_DEPLOYED_MESSAGE,
                "{0} established at {1:0.0}, {2:0.0}, {3:0.0}. {4} Recommendation: {5}. Active marker count: {6}.");
            return AppendDeploymentLogTemplate(ref buffer, template, label, spawnPosition, assessment, activeCount);
        }

        private bool TryWriteCheckLogSummary(
            ref FixedCharBuffer buffer,
            string label,
            float distance,
            BeaconAssessment assessment)
        {
            string template = StableText(
                LocalizationKeys.BEACON_LOG_CHECK_MESSAGE,
                "{0} is the nearest active field marker at {1:0.0} m. {2} Recommendation: {3}. Close within {4:0.0} m to retract.");
            return AppendCheckLogTemplate(ref buffer, template, label, distance, assessment, retractRange);
        }

        private bool TryWriteRetractionLogSummary(
            ref FixedCharBuffer buffer,
            string label,
            Vector3 position,
            float distance,
            int activeCount)
        {
            string template = StableText(
                LocalizationKeys.BEACON_LOG_RETRACTED_MESSAGE,
                "{0} was retracted from {1:0.0}, {2:0.0}, {3:0.0} at {4:0.0} m. Active marker count: {5}.");
            return AppendRetractionLogTemplate(ref buffer, template, label, position, distance, activeCount);
        }

        private static bool AppendDeploymentLogTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string label,
            Vector3 spawnPosition,
            BeaconAssessment assessment,
            int activeCount)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> span = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '{' || i + 1 >= span.Length)
                    continue;

                char token = span[i + 1];
                int closeIndex = i + 2;
                while (closeIndex < span.Length && span[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= span.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(span.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wrote = token switch
                {
                    '0' => AppendText(ref buffer, label),
                    '1' => buffer.AppendFloat(spawnPosition.x, 1),
                    '2' => buffer.AppendFloat(spawnPosition.y, 1),
                    '3' => buffer.AppendFloat(spawnPosition.z, 1),
                    '4' => assessment.TryWriteSummary(ref buffer),
                    '5' => AppendText(ref buffer, assessment.Recommendation),
                    '6' => buffer.AppendInt(activeCount),
                    _ => buffer.Append(span.Slice(i, closeIndex - i + 1))
                };

                if (!wrote)
                    return false;

                i = closeIndex;
                segmentStart = closeIndex + 1;
            }

            return segmentStart >= span.Length || buffer.Append(span.Slice(segmentStart));
        }

        private static bool AppendCheckLogTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string label,
            float distance,
            BeaconAssessment assessment,
            float retractRange)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> span = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '{' || i + 1 >= span.Length)
                    continue;

                char token = span[i + 1];
                int closeIndex = i + 2;
                while (closeIndex < span.Length && span[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= span.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(span.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wrote = token switch
                {
                    '0' => AppendText(ref buffer, label),
                    '1' => buffer.AppendFloat(distance, 1),
                    '2' => assessment.TryWriteSummary(ref buffer),
                    '3' => AppendText(ref buffer, assessment.Recommendation),
                    '4' => buffer.AppendFloat(retractRange, 1),
                    _ => buffer.Append(span.Slice(i, closeIndex - i + 1))
                };

                if (!wrote)
                    return false;

                i = closeIndex;
                segmentStart = closeIndex + 1;
            }

            return segmentStart >= span.Length || buffer.Append(span.Slice(segmentStart));
        }

        private bool AppendRetractionLogTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string label,
            Vector3 position,
            float distance,
            int activeCount)
        {
            if (string.IsNullOrEmpty(template))
                return true;

            ReadOnlySpan<char> span = template.AsSpan();
            int segmentStart = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '{' || i + 1 >= span.Length)
                    continue;

                char token = span[i + 1];
                int closeIndex = i + 2;
                while (closeIndex < span.Length && span[closeIndex] != '}')
                    closeIndex++;

                if (closeIndex >= span.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(span.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wrote = token switch
                {
                    '0' => AppendText(ref buffer, label),
                    '1' => buffer.AppendFloat(position.x, 1),
                    '2' => buffer.AppendFloat(position.y, 1),
                    '3' => buffer.AppendFloat(position.z, 1),
                    '4' => buffer.AppendFloat(distance, 1),
                    '5' => buffer.AppendInt(activeCount),
                    _ => buffer.Append(span.Slice(i, closeIndex - i + 1))
                };

                if (!wrote)
                    return false;

                i = closeIndex;
                segmentStart = closeIndex + 1;
            }

            return segmentStart >= span.Length || buffer.Append(span.Slice(segmentStart));
        }

        private void RecordDeploymentLog(string label, Vector3 spawnPosition, BeaconAssessment assessment, int activeCount)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteDeploymentLogSummary(ref _beaconLogBuffer, label, spawnPosition, assessment, activeCount))
                return;

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                StableText(LocalizationKeys.BEACON_LOG_DEPLOYED_TITLE, "FIELD BEACON DEPLOYED"),
                in _beaconLogBuffer,
                "INFO");
        }

        private void RecordBeaconCheckLog(string label, float distance, BeaconAssessment assessment)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteCheckLogSummary(ref _beaconLogBuffer, label, distance, assessment))
                return;

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                StableText(LocalizationKeys.BEACON_LOG_CHECK_TITLE, "BEACON GRID CHECK"),
                in _beaconLogBuffer,
                "INFO");
        }

        private void RecordRetractionLog(string label, Vector3 position, float distance, int activeCount)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteRetractionLogSummary(ref _beaconLogBuffer, label, position, distance, activeCount))
                return;

            FieldOperationLogSystem.RecordOperation(
                StableText(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                StableText(LocalizationKeys.BEACON_LOG_RETRACTED_TITLE, "FIELD BEACON RETRACTED"),
                in _beaconLogBuffer,
                "INFO");
        }
    }
}
