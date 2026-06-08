using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes low-frequency world readability guidance using active biome and zone context.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4025)]
    [AddComponentMenu("Hecton8/World/World Readability Director")]
    public sealed class WorldReadabilityDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        internal static WorldReadabilityDirector ActiveRuntimeInstance { get; private set; }
        private const int SeverityInfo = 0;
        private const int SeverityWarning = 1;
        private const int SeverityCritical = 2;
        private const int NotificationPublishRetryFrameLimit = 3;
        private static readonly uint _NotificationMissWarningHash = unchecked((uint)LocHash.Compute("WorldReadabilityDirector.NotificationMiss"));
        private static readonly uint _NotificationContextHash = unchecked((uint)LocHash.Compute("WorldReadabilityDirector.Notification"));

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
        private bool _registeredLateFrame;
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
        private IDepthZoneReadModel _cachedDepthZoneReadModel;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;
        private float _nextNotificationTime;
        private int _notificationMissCount;
        private int _pendingNotificationRetryCount;

        public int NotificationMissCount => _notificationMissCount;

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
            _pendingNotificationRetryCount = 0;
            _nextNotificationTime = 0f;
            _notificationMissCount = 0;
            _debugPendingMessage = "None";
            _debugPendingSeverity = SeverityInfo;
            _debugNextNotificationTime = 0f;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

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
                    _cachedDepthZoneReadModel = currentService as IDepthZoneReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _firstHourDirector = GlobalRegistry.FirstHourReadModel;
            _cachedDepthZoneReadModel = GlobalRegistry.DepthZoneReadModel;
            _playerRuntimeContext = GlobalRegistry.Player;
            if (_cachedDepthZoneReadModel == null && depthZoneDirector != null)
                _cachedDepthZoneReadModel = depthZoneDirector;
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

            HectonBiomeMatrixProfile currentBiome = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
            WorldZoneAnchor currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            IDepthZoneReadModel depthZoneReadModel = _cachedDepthZoneReadModel ?? depthZoneDirector;
            DepthZoneProfile currentDepthZone = depthZoneReadModel != null ? depthZoneReadModel.CurrentZone : null;
            ResolveCurrentDepthContext(out int currentDepthTier, out float currentDepthMeters);

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
            UpdateDiagnostics();
        }

        public void LateFrameTick()
        {
            TryPublishPending();
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
            if (!force && biomeMatrixDirector != null && worldZoneDirector != null && (depthZoneDirector != null || _cachedDepthZoneReadModel != null))
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            if (_cachedDepthZoneReadModel == null && depthZoneDirector != null)
                _cachedDepthZoneReadModel = depthZoneDirector;
        }

        private void ResolveCurrentDepthContext(out int depthTier, out float depthMeters)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depthMeters = math.max(0f, movementState.DepthMeters);
                depthTier = ResolveFallbackDepthTier(depthMeters);
                return;
            }

            BiomeMatrixDirector biomeMatrix = biomeMatrixDirector;
            if (biomeMatrix != null &&
                biomeMatrix.isActiveAndEnabled &&
                math.isfinite(biomeMatrix.CurrentDepthMeters))
            {
                depthMeters = math.max(0f, biomeMatrix.CurrentDepthMeters);
                depthTier = math.max(1, biomeMatrix.CurrentDepthTier);
                return;
            }

            depthMeters = 0f;
            depthTier = 1;
        }

        private static int ResolveFallbackDepthTier(float depth)
        {
            if (!math.isfinite(depth) || depth <= 0f)
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

            float clamped = math.clamp(depth, 3500f, 14000f);
            float normalized = (clamped - 3500f) / 10500f;
            int tier = 10 + (int)math.floor(normalized * 17f);
            return math.clamp(tier, 10, 26);
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
            _pendingNotificationRetryCount = 0;
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

            if (_hasPendingMessage && _pendingMessage == message && _pendingSeverity == severity)
                return;

            if (_hasPendingMessage && _pendingSeverity > severity)
                return;

            _pendingMessage = message;
            _pendingSeverity = severity;
            _hasPendingMessage = true;
            _pendingNotificationRetryCount = 0;
            _debugPendingMessage = message;
            _debugPendingSeverity = severity;
        }

        private void TryPublishPending()
        {
            if (!_hasPendingMessage)
                return;

            if ((float)SystemDispatcher.CurrentUnscaledTimeSeconds < _nextNotificationTime)
                return;

            if (!PublishNotification(_pendingMessage, _pendingSeverity))
            {
                if (ShouldDropPendingNotificationAfterMiss())
                    ClearPendingMessage();

                return;
            }

            ClearPendingMessage();
        }

        private bool PublishNotification(string message, int severity)
        {
            bool pushed;
            switch (severity)
            {
                case SeverityCritical:
                    pushed = NotificationEvents.TryPushCritical(message.AsSpan());
                    break;
                case SeverityWarning:
                    pushed = NotificationEvents.TryPushWarning(message.AsSpan());
                    break;
                default:
                    pushed = NotificationEvents.TryPushInfo(message.AsSpan());
                    break;
            }

            if (!pushed)
            {
                ReportReadabilityNotificationMiss(severity);
                return false;
            }

            _debugLastPublishedMessage = message;
            _debugLastPublishedSeverity = severity;
            _nextNotificationTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds + Mathf.Max(0f, notificationCooldown);
            _debugNextNotificationTime = _nextNotificationTime;
            return true;
        }

        private bool ShouldDropPendingNotificationAfterMiss()
        {
            _pendingNotificationRetryCount++;
            return _pendingNotificationRetryCount >= NotificationPublishRetryFrameLimit;
        }

        private void ReportReadabilityNotificationMiss(int severity)
        {
            _notificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _NotificationMissWarningHash,
                _NotificationContextHash ^ unchecked((uint)math.max(0, severity)),
                math.max(1, _notificationMissCount));
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

            if (depthTier <= 1 || depthMeters <= 0f)
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

            if (profile == null)
            {
                if (zone != null &&
                    zone.RouteCritical &&
                    !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                {
                    return zone.GameplayIntent;
                }

                return null;
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
            if (depthTier <= 1)
                return null;

            if (depthZone != null)
            {
                if (depthZone.requiredHullTier >= 2 && !string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;

                if (depthZone.dangerLevel >= 0.72f && !string.IsNullOrWhiteSpace(depthZone.description))
                    return depthZone.description;
            }

            if (profile == null)
            {
                if (zone != null && !string.IsNullOrWhiteSpace(zone.GameplayIntent))
                    return zone.GameplayIntent;

                return null;
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
            ResolveCurrentDepthContext(out _debugDepthTier, out _debugDepthMeters);
            _debugRouteLegible = _lastRouteLegible;
            _debugSafePocket = _lastSafePocket;
        }
    }
}
