using System;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes low-frequency world readability guidance using active biome and zone context.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4025)]
    [AddComponentMenu("Hecton8/World/World Readability Director")]
    public sealed class WorldReadabilityDirector : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        internal static WorldReadabilityDirector ActiveRuntimeInstance { get; private set; }
        private const int SeverityInfo = 0;
        private const int SeverityWarning = 1;
        private const int SeverityCritical = 2;

        [Header("References")]
        [Tooltip("Live biome matrix owner used for biome framing reads.")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [Tooltip("Live zone owner used for route and landmark guidance reads.")]
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [Tooltip("Live depth-zone owner used for hazard and hull-readability cues.")]
        [SerializeField] private DepthZoneDirector depthZoneDirector;

        [Header("Runtime Auto Resolve")]
        [Tooltip("Retry cadence for runtime auto-resolve when authoring references are absent.")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Cadence")]
        [Tooltip("Minimum delay between readability notifications to avoid HUD spam.")]
        [SerializeField, Min(0f)] private float notificationCooldown = 8f;

        [Header("First-Hour Gate")]
        [Tooltip("Do not let readability notifications compete with first-hour onboarding before this milestone is reached.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToPublish = FirstHourMilestone.Orientation;

        [Header("Diagnostics")]
        [Tooltip("Last observed biome label for runtime readback.")]
        [SerializeField] private string _debugBiome = "None";
        [Tooltip("Last observed world zone label for runtime readback.")]
        [SerializeField] private string _debugZone = "None";
        [Tooltip("Last observed depth-zone label for runtime readback.")]
        [SerializeField] private string _debugDepthZone = "None";
        [Tooltip("Pending message held back by cadence gating.")]
        [SerializeField] private string _debugPendingMessage = "None";
        [Tooltip("Severity of the pending message.")]
        [SerializeField] private int _debugPendingSeverity;
        [Tooltip("Last message published into the HUD notification bus.")]
        [SerializeField] private string _debugLastPublishedMessage = "None";
        [Tooltip("Severity of the last published message.")]
        [SerializeField] private int _debugLastPublishedSeverity;
        [Tooltip("Next allowed notification timestamp in unscaled time.")]
        [SerializeField] private float _debugNextNotificationTime;
        [Tooltip("Last observed biome depth tier for runtime readback.")]
        [SerializeField] private int _debugDepthTier = 1;
        [Tooltip("Last observed biome depth in meters for runtime readback.")]
        [SerializeField] private float _debugDepthMeters;
        [Tooltip("Whether the current world context still reads as an authored route lane.")]
        [SerializeField] private bool _debugRouteLegible;
        [Tooltip("Whether the current world context still reads as a safe pocket.")]
        [SerializeField] private bool _debugSafePocket;

        private bool _registeredToTickManager;
        private bool _hasObservedContext;
        private HectonBiomeMatrixProfile _lastBiomeProfile;
        private WorldZoneAnchor _lastZone;
        private DepthZoneProfile _lastDepthZone;
        private int _lastDepthTier = -1;
        private bool _lastRouteLegible;
        private bool _lastSafePocket;
        private string _pendingMessage;
        private int _pendingSeverity;
        private bool _hasPendingMessage;
        private bool _hotSwapRegistered;
        private IFirstHourReadModel _firstHourDirector;
        private DepthZoneDirector _cachedDepthZoneDirector;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;
        private float _nextNotificationTime;

        private void Awake()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            ResolveReferences(force: true);
            ResetObservedState();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();

            ResolveReferences(force: true);
            ResetObservedState();
            UpdateDiagnostics();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            TryRegister();

            ResolveReferences(force: true);
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterHotSwapListener();
            TryUnregister();

            _hasPendingMessage = false;
            _hasObservedContext = false;
            _pendingMessage = null;
            _pendingSeverity = SeverityInfo;
            _nextNotificationTime = 0f;
            _debugPendingMessage = "None";
            _debugPendingSeverity = SeverityInfo;
            _debugNextNotificationTime = 0f;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    _firstHourDirector = currentService as IFirstHourReadModel;
                    break;
                case GlobalRegistryServiceSlot.DepthZoneRuntime:
                    _cachedDepthZoneDirector = currentService as DepthZoneDirector;
                    if (depthZoneDirector == null)
                        depthZoneDirector = _cachedDepthZoneDirector;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _firstHourDirector = GlobalRegistry.FirstHourReadModel;
            _cachedDepthZoneDirector = GlobalRegistry.DepthZone;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        internal void ApplyRuntimeDependencies(
            WorldZoneDirector runtimeWorldZoneDirector,
            BiomeMatrixDirector runtimeBiomeMatrixDirector)
        {
            if (worldZoneDirector == null)
                worldZoneDirector = runtimeWorldZoneDirector;

            if (biomeMatrixDirector == null)
                biomeMatrixDirector = runtimeBiomeMatrixDirector;

            ResolveReferences(force: true);
            UpdateDiagnostics();
        }

        /// <summary>
        /// Evaluates active biome and zone context and queues readability guidance when context changes.
        /// </summary>
        public void SlowTick()
        {
            ResolveReferences();
            TryPublishPending();

            HectonBiomeMatrixProfile currentBiome = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
            WorldZoneAnchor currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            DepthZoneProfile currentDepthZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;
            int currentDepthTier = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthTier : 1;
            float currentDepthMeters = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthMeters : 0f;

            if (!CanPublishReadability())
            {
                ClearPendingMessage();
                CaptureObservedContext(currentBiome, currentZone, currentDepthZone, currentDepthTier);
                UpdateDiagnostics();
                return;
            }

            if (!_hasObservedContext || currentBiome != _lastBiomeProfile)
            {
                _lastBiomeProfile = currentBiome;
                TryQueueBiomeGuidance(currentBiome);
            }

            if (!_hasObservedContext || currentZone != _lastZone)
            {
                _lastZone = currentZone;
                TryQueueZoneGuidance(currentZone);
            }

            if (!_hasObservedContext || currentDepthZone != _lastDepthZone)
            {
                _lastDepthZone = currentDepthZone;
                TryQueueDepthZoneGuidance(currentBiome, currentZone, currentDepthZone);
            }

            if (!_hasObservedContext || currentDepthTier != _lastDepthTier)
            {
                _lastDepthTier = currentDepthTier;
                TryQueueDepthGuidance(currentBiome, currentZone, currentDepthZone, currentDepthTier, currentDepthMeters);
            }

            TryQueueRouteStateGuidance(currentBiome, currentZone, currentDepthZone, currentDepthTier);

            _hasObservedContext = true;
            TryPublishPending();
            UpdateDiagnostics();
        }

        private bool CanPublishReadability()
        {
            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToPublish);
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && biomeMatrixDirector != null && worldZoneDirector != null && depthZoneDirector != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            if (depthZoneDirector == null)
                depthZoneDirector = _cachedDepthZoneDirector;
        }

        private void ResetObservedState()
        {
            _hasObservedContext = false;
            _lastBiomeProfile = null;
            _lastZone = null;
            _lastDepthZone = null;
            _lastDepthTier = -1;
            _lastRouteLegible = false;
            _lastSafePocket = false;
        }

        private void ClearPendingMessage()
        {
            _pendingMessage = null;
            _pendingSeverity = SeverityInfo;
            _hasPendingMessage = false;
            _debugPendingMessage = "None";
            _debugPendingSeverity = SeverityInfo;
        }

        private void CaptureObservedContext(
            HectonBiomeMatrixProfile currentBiome,
            WorldZoneAnchor currentZone,
            DepthZoneProfile currentDepthZone,
            int currentDepthTier)
        {
            _lastBiomeProfile = currentBiome;
            _lastZone = currentZone;
            _lastDepthZone = currentDepthZone;
            _lastDepthTier = currentDepthTier;
            _lastRouteLegible = IsRouteLegible(currentBiome, currentZone);
            _lastSafePocket = IsSafePocket(currentZone);
            _hasObservedContext = true;
        }

        private void TryQueueBiomeGuidance(HectonBiomeMatrixProfile profile)
        {
            string message = ResolveBiomeGuidanceMessage(profile, out int severity);
            QueueOrPublish(message, severity);
        }

        private void TryQueueZoneGuidance(WorldZoneAnchor zone)
        {
            string message = ResolveZoneGuidanceMessage(zone, out int severity);
            QueueOrPublish(message, severity);
        }

        private void TryQueueDepthZoneGuidance(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone)
        {
            string message = ResolveDepthZoneGuidanceMessage(profile, zone, depthZone, out int severity);
            QueueOrPublish(message, severity);
        }

        private void TryQueueDepthGuidance(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone,
            int depthTier,
            float depthMeters)
        {
            string message = ResolveDepthGuidanceMessage(profile, zone, depthZone, depthTier, depthMeters, out int severity);
            QueueOrPublish(message, severity);
        }

        private void TryQueueRouteStateGuidance(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone,
            int depthTier)
        {
            bool routeLegible = IsRouteLegible(profile, zone);
            bool safePocket = IsSafePocket(zone);

            if (_hasObservedContext && routeLegible != _lastRouteLegible)
            {
                string message = routeLegible
                    ? ResolveRouteRecoveryMessage(profile, zone, depthZone)
                    : ResolveRouteLossMessage(profile, zone, depthZone, depthTier);
                int severity = routeLegible ? SeverityInfo : SeverityWarning;
                QueueOrPublish(message, severity);
            }

            if (_hasObservedContext && safePocket && !_lastSafePocket)
            {
                string message = ResolveSafePocketMessage(profile, zone);
                QueueOrPublish(message, SeverityInfo);
            }

            _lastRouteLegible = routeLegible;
            _lastSafePocket = safePocket;
        }

        private void QueueOrPublish(string message, int severity)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (Time.unscaledTime >= _nextNotificationTime)
            {
                PublishNotification(message, severity);
                return;
            }

            if (_hasPendingMessage && _pendingMessage == message && _pendingSeverity == severity)
                return;

            if (_hasPendingMessage && _pendingSeverity > severity)
                return;

            _pendingMessage = message;
            _pendingSeverity = severity;
            _hasPendingMessage = true;
            _debugPendingMessage = message;
            _debugPendingSeverity = severity;
        }

        private void TryPublishPending()
        {
            if (!_hasPendingMessage)
                return;

            if (Time.unscaledTime < _nextNotificationTime)
                return;

            PublishNotification(_pendingMessage, _pendingSeverity);
            _pendingMessage = null;
            _pendingSeverity = SeverityInfo;
            _hasPendingMessage = false;
            _debugPendingMessage = "None";
            _debugPendingSeverity = SeverityInfo;
        }

        private void PublishNotification(string message, int severity)
        {
            switch (severity)
            {
                case SeverityCritical:
                    NotificationEvents.TryPushCritical(message.AsSpan());
                    break;
                case SeverityWarning:
                    NotificationEvents.TryPushWarning(message.AsSpan());
                    break;
                default:
                    NotificationEvents.TryPushInfo(message.AsSpan());
                    break;
            }

            _debugLastPublishedMessage = message;
            _debugLastPublishedSeverity = severity;
            _nextNotificationTime = Time.unscaledTime + Mathf.Max(0f, notificationCooldown);
            _debugNextNotificationTime = _nextNotificationTime;
        }

        private static string ResolveBiomeGuidanceMessage(HectonBiomeMatrixProfile profile, out int severity)
        {
            severity = SeverityInfo;

            if (profile == null)
                return null;

            if (profile.survivalPressure >= 4 && !string.IsNullOrWhiteSpace(profile.riskSummary))
            {
                severity = SeverityWarning;
                return profile.riskSummary;
            }

            if (!string.IsNullOrWhiteSpace(profile.visitPurpose))
                return profile.visitPurpose;

            if (profile.routePressure >= 4 && !string.IsNullOrWhiteSpace(profile.landmarkGuidance))
                return profile.landmarkGuidance;

            if (!string.IsNullOrWhiteSpace(profile.commonRewardHook))
                return profile.commonRewardHook;

            if (!string.IsNullOrWhiteSpace(profile.landmarkGuidance))
                return profile.landmarkGuidance;

            if (!string.IsNullOrWhiteSpace(profile.riskSummary))
            {
                severity = profile.survivalPressure >= 3 ? SeverityWarning : SeverityInfo;
                return profile.riskSummary;
            }

            if (!string.IsNullOrWhiteSpace(profile.rareRewardHook))
                return profile.rareRewardHook;

            return null;
        }

        private static string ResolveZoneGuidanceMessage(WorldZoneAnchor zone, out int severity)
        {
            severity = SeverityInfo;

            if (zone == null)
                return null;

            HectonBiomeMatrixProfile dominantBiome = zone.DominantMatrixBiome;
            if ((zone.Kind == WorldZoneAnchor.ZoneKind.Trial || zone.Kind == WorldZoneAnchor.ZoneKind.Combat) &&
                dominantBiome != null &&
                !string.IsNullOrWhiteSpace(dominantBiome.riskSummary))
            {
                severity = SeverityWarning;
                return dominantBiome.riskSummary;
            }

            if (zone.RouteCritical &&
                dominantBiome != null &&
                dominantBiome.survivalPressure >= 3 &&
                !string.IsNullOrWhiteSpace(dominantBiome.safePocketIdentity))
            {
                severity = SeverityWarning;
                return dominantBiome.safePocketIdentity;
            }

            if (zone.RouteCritical &&
                dominantBiome != null &&
                !string.IsNullOrWhiteSpace(dominantBiome.landmarkGuidance))
            {
                return dominantBiome.landmarkGuidance;
            }

            if (!string.IsNullOrWhiteSpace(zone.GameplayIntent))
                return zone.GameplayIntent;

            if (dominantBiome != null && !string.IsNullOrWhiteSpace(dominantBiome.landmarkGuidance))
                return dominantBiome.landmarkGuidance;

            return null;
        }

        private static string ResolveDepthZoneGuidanceMessage(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone,
            out int severity)
        {
            severity = SeverityInfo;

            if (depthZone == null)
                return null;

            if (depthZone.dangerLevel >= 0.72f)
            {
                severity = SeverityWarning;
                return !string.IsNullOrWhiteSpace(depthZone.description)
                    ? depthZone.description
                    : profile != null
                        ? profile.riskSummary
                        : null;
            }

            if (depthZone.requiredHullTier >= 2)
            {
                severity = SeverityWarning;
                return !string.IsNullOrWhiteSpace(depthZone.description)
                    ? depthZone.description
                    : "Hull margin is narrowing. Respect the descent envelope.";
            }

            if (depthZone.isThermal || depthZone.hasCaves)
            {
                if (!string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;

                if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                    return zone.GameplayIntent;
            }

            return null;
        }

        private static string ResolveDepthGuidanceMessage(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone,
            int depthTier,
            float depthMeters,
            out int severity)
        {
            severity = SeverityInfo;

            if (profile == null || depthTier <= 1 || depthMeters <= 0f)
                return null;

            if (depthZone != null)
            {
                if (depthZone.requiredHullTier >= 2 && !string.IsNullOrWhiteSpace(depthZone.description))
                {
                    severity = SeverityWarning;
                    return depthZone.description;
                }

                if ((depthZone.isThermal || depthZone.hasCaves) && !string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;
            }

            if (profile.survivalPressure >= 4 && !string.IsNullOrWhiteSpace(profile.safePocketIdentity))
            {
                severity = SeverityWarning;
                return profile.safePocketIdentity;
            }

            if (zone != null &&
                zone.RouteCritical &&
                !string.IsNullOrWhiteSpace(profile.landmarkGuidance))
            {
                return profile.landmarkGuidance;
            }

            if (profile.rewardPull >= 4 && !string.IsNullOrWhiteSpace(profile.rareRewardHook))
                return profile.rareRewardHook;

            if (profile.routePressure >= 4 && !string.IsNullOrWhiteSpace(profile.landmarkGuidance))
                return profile.landmarkGuidance;

            if (!string.IsNullOrWhiteSpace(profile.visitPurpose))
                return profile.visitPurpose;

            return null;
        }

        private static bool IsRouteLegible(HectonBiomeMatrixProfile profile, WorldZoneAnchor zone)
        {
            if (zone == null)
                return false;

            if (zone.RouteCritical)
                return true;

            switch (zone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Navigation:
                case WorldZoneAnchor.ZoneKind.Progression:
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                    return profile != null && profile.routePressure >= 3;
                default:
                    return false;
            }
        }

        private static bool IsSafePocket(WorldZoneAnchor zone)
        {
            if (zone == null)
                return false;

            switch (zone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Construction:
                case WorldZoneAnchor.ZoneKind.Power:
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveRouteRecoveryMessage(HectonBiomeMatrixProfile profile, WorldZoneAnchor zone)
        {
            if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                return zone.GameplayIntent;

            if (profile != null && !string.IsNullOrWhiteSpace(profile.landmarkGuidance))
                return profile.landmarkGuidance;

            if (profile != null && !string.IsNullOrWhiteSpace(profile.visitPurpose))
                return profile.visitPurpose;

            return null;
        }

        private static string ResolveRouteRecoveryMessage(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone)
        {
            if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                return zone.GameplayIntent;

            if (depthZone != null &&
                (depthZone.isThermal || depthZone.hasCaves) &&
                !string.IsNullOrWhiteSpace(depthZone.description))
            {
                return depthZone.description;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.landmarkGuidance))
                return profile.landmarkGuidance;

            if (profile != null && !string.IsNullOrWhiteSpace(profile.visitPurpose))
                return profile.visitPurpose;

            return null;
        }

        private static string ResolveRouteLossMessage(
            HectonBiomeMatrixProfile profile,
            WorldZoneAnchor zone,
            DepthZoneProfile depthZone,
            int depthTier)
        {
            if (profile == null || depthTier <= 1)
                return null;

            if (depthZone != null)
            {
                if (depthZone.requiredHullTier >= 2 && !string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;

                if (depthZone.dangerLevel >= 0.72f && !string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;
            }

            if (!string.IsNullOrWhiteSpace(profile.safePocketIdentity))
                return profile.safePocketIdentity;

            if (!string.IsNullOrWhiteSpace(profile.riskSummary))
                return profile.riskSummary;

            if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                return zone.GameplayIntent;

            return null;
        }

        private static string ResolveSafePocketMessage(HectonBiomeMatrixProfile profile, WorldZoneAnchor zone)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.safePocketIdentity))
                return profile.safePocketIdentity;

            if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                return zone.GameplayIntent;

            return null;
        }

        private void UpdateDiagnostics()
        {
            _debugBiome = _lastBiomeProfile != null && !string.IsNullOrWhiteSpace(_lastBiomeProfile.biomeName)
                ? _lastBiomeProfile.biomeName
                : "None";
            _debugZone = _lastZone != null && !string.IsNullOrWhiteSpace(_lastZone.ZoneLabel)
                ? _lastZone.ZoneLabel
                : "None";
            _debugDepthZone = _lastDepthZone != null && !string.IsNullOrWhiteSpace(_lastDepthZone.displayName)
                ? _lastDepthZone.displayName
                : "None";
            _debugDepthTier = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthTier : 1;
            _debugDepthMeters = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentDepthMeters : 0f;
            _debugRouteLegible = _lastRouteLegible;
            _debugSafePocket = _lastSafePocket;
        }
    }
}
