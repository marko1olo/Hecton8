using System;
using Hecton.Localization;
using Hecton8.Core;
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
        private string _cachedOperationalDirective;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private WorldZoneDirector _worldZoneDirector;
        private FixedCharBuffer _beaconHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - beacon HUD staging buffer - owner: BeaconDeployerTool
        private FixedCharBuffer _beaconLogBuffer = new FixedCharBuffer(768); // COLD ALLOC: char[768] - beacon operation log staging buffer - owner: BeaconDeployerTool

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

            if (Hecton8.Core.GlobalRegistry.BeaconNetwork == null || Hecton8.Core.GlobalRegistry.BeaconNetwork.ActiveCount == 0)
            {
                if (Time.time >= _nextFeedbackAt)
                {
                    if (TryWriteNoActiveBeaconHud())
                        ToolHitUtility.ShowWarning(in _beaconHudBuffer);
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
                    if (TryWriteNearestBeaconHud(nearestSnapshot.Label, assessment, nearestDistance, ResolveActiveBeaconCount()))
                        ToolHitUtility.ShowInfo(in _beaconHudBuffer);

                    RecordBeaconCheckLog(nearestSnapshot.Label, nearestDistance, assessment);
                    _nextFeedbackAt = Time.time + feedbackInterval;
                }
                return;
            }

            if (BeaconNetworkSystem.TryRetractNearest(_cachedTransform.position, out BeaconRuntime nearest, out float distance))
            {
                Vector3 position = nearest.transform.position;
                string label = nearest.Label;
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
            if (_cooldown > 0f)
                _cooldown = math.max(0f, _cooldown - deltaTime);

            _debugActiveBeaconCount = Hecton8.Core.GlobalRegistry.BeaconNetwork != null
                ? Hecton8.Core.GlobalRegistry.BeaconNetwork.ActiveCount
                : 0;
        }

        public override string GetOperationalSummary()
        {
            _beaconHudBuffer.Clear();
            WriteOperationalSummary(ref _beaconHudBuffer);
            return CreateLegacyString(in _beaconHudBuffer);
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

            ResolveRuntimeContext();
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

        public override string GetOperationalDirective()
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
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        label,
                        routeAssessment.Summary),
                    routeAssessment.Recommendation);
            }

            if (Hecton8.Core.GlobalRegistry.BeaconNetwork == null || Hecton8.Core.GlobalRegistry.BeaconNetwork.ActiveCount <= 1)
            {
                return new BeaconAssessment(
                    ResolveLocalized(LocalizationKeys.BEACON_ROLE_ANCHOR, "ANCHOR"),
                    BeaconTextSegment.FormatString(
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
                    BeaconTextSegment.FormatString(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_STANDALONE_ANCHOR,
                            "{0} could not resolve a neighbor and is acting as a standalone anchor."),
                        label),
                    ResolveLocalized(LocalizationKeys.BEACON_RECOMMEND_CONFIRM_ROUTE, "Confirm line of travel before extending the grid."));
            }

            string role = ClassifyRole(nearestDistance);
            BeaconTextSegment summary = BeaconTextSegment.FormatStringStringFloat(
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
            return AppendText(ref _beaconHudBuffer, ResolveLocalized(LocalizationKeys.BEACON_HUD_NO_ACTIVE, DefaultNoActiveMarkers));
        }

        private static int ResolveActiveBeaconCount()
        {
            return Hecton8.Core.GlobalRegistry.BeaconNetwork != null
                ? Hecton8.Core.GlobalRegistry.BeaconNetwork.ActiveCount
                : 0;
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
                    BeaconTextSegment.FormatStringString(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_ROUTE_GUIDE,
                            "{0} sits on authored route guidance. {1}"),
                        snapshot.Label,
                        routeAssessment.Summary),
                    routeRecommendation);
            }

            string role = ClassifyRole(distance);
            BeaconTextSegment summary = role switch
            {
                var localMark when localMark == ResolveLocalized(LocalizationKeys.BEACON_ROLE_LOCAL_MARK, "LOCAL MARK")
                    => BeaconTextSegment.FormatString(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_LOCAL_MARK,
                            "{0} is a close-range marker for nearby loot, turns, or hazards."),
                        snapshot.Label),
                var relay when relay == ResolveLocalized(LocalizationKeys.BEACON_ROLE_RELAY, "RELAY")
                    => BeaconTextSegment.FormatString(
                        ResolveLocalized(
                            LocalizationKeys.BEACON_SUMMARY_RELAY,
                            "{0} is holding a mid-range travel lane through the sector."),
                        snapshot.Label),
                _ => BeaconTextSegment.FormatString(
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

            if (!FieldTargetSemantics.TryFindNearestRouteMarkerSq(position, 5f, out FieldTargetDescriptor nearest, out _))
                return false;

            assessment = new BeaconAssessment(
                FieldTargetSemantics.BuildRouteRoleLabel(nearest.Role),
                FieldTargetSemantics.BuildDescriptorSummary(
                    nearest,
                    ResolveLocalized(
                        LocalizationKeys.BEACON_SUMMARY_ROUTE_ALIGNED,
                        "Authored route guide is inside beacon alignment range.")),
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

            if (Hecton8.Core.GlobalRegistry.BeaconNetwork == null)
                return false;

            int count = Hecton8.Core.GlobalRegistry.BeaconNetwork.CopySnapshots(_beaconBuffer);
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            double bestDistanceSq = double.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                BeaconNetworkSystem.BeaconSnapshot candidate = _beaconBuffer[i];
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

        private bool TryReadNearestAssessment(out string label, out float distance, out BeaconAssessment assessment)
        {
            label = ResolveLocalized(LocalizationKeys.BEACON_LABEL_NONE, DefaultNoBeaconLabel);
            distance = 0f;
            assessment = default;

            if (Hecton8.Core.GlobalRegistry.BeaconNetwork == null || Hecton8.Core.GlobalRegistry.BeaconNetwork.ActiveCount == 0)
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
            _cachedOperationalDirective = null;
        }

        private void RefreshOperationalDirectiveCache()
        {
            int currentFrame = Time.frameCount;
            if (_cachedOperationalTextFrame == currentFrame)
                return;

            if (_cooldown > 0f)
            {
                _cachedOperationalDirective = ResolveLocalized(
                    LocalizationKeys.BEACON_OPERATIONAL_COOLDOWN_DIRECTIVE,
                    "Wait for deployment hardware to reset.");
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            if (TryGetNearestAssessmentCached(out _, out _, out BeaconAssessment assessment))
            {
                _cachedOperationalDirective = assessment.Recommendation;
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

            ResolveRuntimeContext();
            if (TryBuildContextualReadyAssessment(out BeaconAssessment contextualAssessment))
            {
                _cachedOperationalDirective = contextualAssessment.Recommendation;
                _cachedOperationalTextFrame = currentFrame;
                return;
            }

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
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0
                ? new string(buffer.Buffer, 0, buffer.Length)
                : string.Empty;
        }

        private static bool TryWriteDeploymentLogSummary(
            ref FixedCharBuffer buffer,
            string label,
            Vector3 spawnPosition,
            BeaconAssessment assessment,
            int activeCount)
        {
            string template = ResolveLocalized(
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
            string template = ResolveLocalized(
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
            string template = ResolveLocalized(
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

        private string CreateLegacyString(BeaconTextSegment segment)
        {
            _beaconLogBuffer.Clear();
            if (!segment.TryWrite(ref _beaconLogBuffer))
                return segment.Template;

            return CreateLegacyString(in _beaconLogBuffer);
        }

        private void RecordDeploymentLog(string label, Vector3 spawnPosition, BeaconAssessment assessment, int activeCount)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteDeploymentLogSummary(ref _beaconLogBuffer, label, spawnPosition, assessment, activeCount))
                return;

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                ResolveLocalized(LocalizationKeys.BEACON_LOG_DEPLOYED_TITLE, "FIELD BEACON DEPLOYED"),
                in _beaconLogBuffer,
                "INFO");
        }

        private void RecordBeaconCheckLog(string label, float distance, BeaconAssessment assessment)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteCheckLogSummary(ref _beaconLogBuffer, label, distance, assessment))
                return;

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                ResolveLocalized(LocalizationKeys.BEACON_LOG_CHECK_TITLE, "BEACON GRID CHECK"),
                in _beaconLogBuffer,
                "INFO");
        }

        private void RecordRetractionLog(string label, Vector3 position, float distance, int activeCount)
        {
            _beaconLogBuffer.Clear();
            if (!TryWriteRetractionLogSummary(ref _beaconLogBuffer, label, position, distance, activeCount))
                return;

            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.BEACON_PREFIX, DefaultBeaconPrefix),
                ResolveLocalized(LocalizationKeys.BEACON_LOG_RETRACTED_TITLE, "FIELD BEACON RETRACTED"),
                in _beaconLogBuffer,
                "INFO");
        }
    }
}
