using System;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Owns the early breadcrumb chain for emergency service relays and hands off the next relay after each discovery.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4020)]
    [AddComponentMenu("Hecton8/World/Emergency Service Relay Director")]
    public sealed class EmergencyServiceRelayDirector : MonoBehaviour
    {
        private const string DefaultIntroChainId = "intro_service_route";
        private const string DefaultRelayFallback =
            "HOLD TO THE SERVICE TRACE. RELAYS AND CACHES GIVE LORE, SUPPLIES, AND THE NEXT FOOTHOLD.";
        private const string ManagersRootName = "[MANAGERS]";
        private const string SystemsRootName = "--- SYSTEMS ---";

        private static EmergencyServiceRelayDirector _instance;
        public static EmergencyServiceRelayDirector Instance => ResolveInstance();

        [Header("── Relay Chain ────────────────────────────")]
        [Tooltip("Chain ID used by the first-hour breadcrumb route.")]
        [SerializeField] private string introChainId = DefaultIntroChainId;

        [Tooltip("Atlas should remain background noise while the intro relay chain is still active.")]
        [SerializeField] private int maximumAtlasRevealStageToDrive = 1;

        [Tooltip("Do not let relays compete with first-hour onboarding before this milestone.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToDrive = FirstHourMilestone.Orientation;

        [Tooltip("Once this milestone is complete, relay breadcrumbs stop being the main driver.")]
        [SerializeField] private FirstHourMilestone terminalMilestone = FirstHourMilestone.FirstModule;

        [Header("── Messaging ──────────────────────────────")]
        [Tooltip("Fallback message used if the active target relay has no authored breadcrumb copy.")]
        [SerializeField, TextArea(2, 4)] private string relayFallbackMessage = DefaultRelayFallback;

        private string _lastGuidanceRelayId;
        private bool _hasAnyRelayDiscovery;
        private EmergencyServiceRelay _currentRouteTarget;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            EmergencyServiceRelayEvents.OnRelayActivated += HandleRelayActivated;
            RefreshRelayDiscoveryState();
        }

        private void OnDisable()
        {
            EmergencyServiceRelayEvents.OnRelayActivated -= HandleRelayActivated;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Returns true when the first-hour relay chain already made real route contact with the player.</summary>
        public bool HasDiscoveredRelayInDrivenChain()
        {
            RefreshRelayDiscoveryState();
            return _hasAnyRelayDiscovery;
        }

        /// <summary>Returns true if the provided discovery ID belongs to an authored relay in the driven chain.</summary>
        public bool IsRelayDiscoveryId(string discoveryId)
        {
            if (string.IsNullOrEmpty(discoveryId))
                return false;

            for (int i = 0; i < EmergencyServiceRelay.ActiveCount; i++)
            {
                EmergencyServiceRelay relay = EmergencyServiceRelay.GetActiveRelayAt(i);
                if (relay == null || !IsRelayPartOfDrivenChain(relay))
                    continue;

                if (string.Equals(relay.RelayId, discoveryId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a one-shot contextual guidance message that points the player at the next undiscovered relay.
        /// </summary>
        public bool TryBuildContextualGuidanceMessage(out string message)
        {
            message = null;

            if (!ShouldDriveBreadcrumbs())
                return false;

            EmergencyServiceRelay nextRelay = ResolveCurrentRouteTarget();
            if (nextRelay == null)
                return false;

            if (string.Equals(_lastGuidanceRelayId, nextRelay.RelayId, StringComparison.Ordinal))
                return false;

            message = _hasAnyRelayDiscovery
                ? nextRelay.BuildBreadcrumbMessage()
                : nextRelay.BuildInitialRouteMessage();

            if (string.IsNullOrWhiteSpace(message))
                message = ResolveLocalized(LocalizationKeys.RELAY_DIRECTOR_FALLBACK, relayFallbackMessage);

            _lastGuidanceRelayId = nextRelay.RelayId;
            return !string.IsNullOrWhiteSpace(message);
        }

        /// <summary>
        /// Returns the currently active relay target that should be surfaced on HUD route markers.
        /// </summary>
        public EmergencyServiceRelay GetActiveRouteTarget()
        {
            if (_currentRouteTarget != null &&
                IsRelayPartOfDrivenChain(_currentRouteTarget) &&
                _currentRouteTarget.isActiveAndEnabled)
            {
                return _currentRouteTarget;
            }

            return ShouldDriveBreadcrumbs()
                ? ResolveCurrentRouteTarget()
                : null;
        }

        private void HandleRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            if (relay == null || !IsRelayPartOfDrivenChain(relay))
                return;

            _hasAnyRelayDiscovery = true;
            _lastGuidanceRelayId = null;
            EmergencyServiceRelay nextRelay = ResolveRouteTargetAfterActivation(relay);
            _currentRouteTarget = nextRelay;

            if (firstActivation && relay.CountsAsLoreRouteContact)
                FirstHourDirector.Instance?.RegisterServiceRelayRouteContact();

            if (!firstActivation || !ShouldDriveBreadcrumbs())
                return;

            string routeMessage = relay.BuildDownloadedRouteMessage(nextRelay);
            if (!string.IsNullOrWhiteSpace(routeMessage))
                NotificationEvents.PushInfo(routeMessage);

            if (nextRelay != null)
                _lastGuidanceRelayId = nextRelay.RelayId;
        }

        private void RefreshRelayDiscoveryState()
        {
            _hasAnyRelayDiscovery = false;
            _currentRouteTarget = null;
            EmergencyServiceRelay highestDiscoveredRelay = null;
            int highestDiscoveredOrder = int.MinValue;

            for (int i = 0; i < EmergencyServiceRelay.ActiveCount; i++)
            {
                EmergencyServiceRelay relay = EmergencyServiceRelay.GetActiveRelayAt(i);
                if (relay == null || !IsRelayPartOfDrivenChain(relay) || !relay.IsDiscovered)
                    continue;

                _hasAnyRelayDiscovery = true;
                if (relay.RelayOrder < highestDiscoveredOrder)
                    continue;

                highestDiscoveredOrder = relay.RelayOrder;
                highestDiscoveredRelay = relay;
            }

            _currentRouteTarget = ResolveRouteTargetFromDiscoveryState(highestDiscoveredRelay);
        }

        private bool ShouldDriveBreadcrumbs()
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null)
            {
                if (!firstHourDirector.IsMilestoneComplete(minimumMilestoneToDrive))
                    return false;

                if (firstHourDirector.IsMilestoneComplete(terminalMilestone))
                    return false;
            }

            AtlasSignalSystem atlasSignalSystem = AtlasSignalSystem.Instance;
            if (atlasSignalSystem != null && atlasSignalSystem.CurrentRevealStage > maximumAtlasRevealStageToDrive)
                return false;

            return true;
        }

        private EmergencyServiceRelay ResolveCurrentRouteTarget()
        {
            if (_currentRouteTarget != null &&
                IsRelayPartOfDrivenChain(_currentRouteTarget) &&
                _currentRouteTarget.isActiveAndEnabled)
            {
                return _currentRouteTarget;
            }

            _currentRouteTarget = ResolveRouteTargetFromDiscoveryState(null);
            return _currentRouteTarget;
        }

        private EmergencyServiceRelay ResolveRouteTargetAfterActivation(EmergencyServiceRelay currentRelay)
        {
            if (currentRelay == null)
                return null;

            EmergencyServiceRelay explicitNextRelay = currentRelay.NextRelay;
            if (explicitNextRelay != null &&
                IsRelayPartOfDrivenChain(explicitNextRelay) &&
                explicitNextRelay.isActiveAndEnabled)
            {
                return explicitNextRelay;
            }

            return ResolveNextRelayInDrivenChain(currentRelay);
        }

        private EmergencyServiceRelay ResolveRouteTargetFromDiscoveryState(EmergencyServiceRelay highestDiscoveredRelay)
        {
            if (highestDiscoveredRelay != null)
            {
                EmergencyServiceRelay discoveredRouteTarget = ResolveRouteTargetAfterActivation(highestDiscoveredRelay);
                if (discoveredRouteTarget != null)
                    return discoveredRouteTarget;
            }

            return ResolveNextRelayInDrivenChain(null);
        }

        private EmergencyServiceRelay ResolveNextRelayInDrivenChain(EmergencyServiceRelay currentRelay)
        {
            EmergencyServiceRelay explicitNextRelay = currentRelay != null ? currentRelay.NextRelay : null;
            if (explicitNextRelay != null &&
                IsRelayPartOfDrivenChain(explicitNextRelay) &&
                !explicitNextRelay.IsDiscovered)
            {
                return explicitNextRelay;
            }

            EmergencyServiceRelay bestRelay = null;
            int minimumOrder = currentRelay != null ? currentRelay.RelayOrder + 1 : int.MinValue;
            int bestOrder = int.MaxValue;

            for (int i = 0; i < EmergencyServiceRelay.ActiveCount; i++)
            {
                EmergencyServiceRelay relay = EmergencyServiceRelay.GetActiveRelayAt(i);
                if (relay == null || !IsRelayPartOfDrivenChain(relay) || relay.IsDiscovered)
                    continue;

                if (relay.RelayOrder < minimumOrder || relay.RelayOrder >= bestOrder)
                    continue;

                bestOrder = relay.RelayOrder;
                bestRelay = relay;
            }

            return bestRelay;
        }

        private bool IsRelayPartOfDrivenChain(EmergencyServiceRelay relay)
        {
            return relay != null &&
                string.Equals(relay.ChainId, introChainId, StringComparison.Ordinal);
        }

        private static EmergencyServiceRelayDirector ResolveInstance()
        {
            if (_instance != null)
                return _instance;

            _instance = UnityEngine.Object.FindAnyObjectByType<EmergencyServiceRelayDirector>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;

            if (!Application.isPlaying || EmergencyServiceRelay.ActiveCount <= 0)
                return null;

            GameObject owner = GameObject.Find(ManagersRootName);
            if (owner == null)
                owner = GameObject.Find(SystemsRootName);

            if (owner == null)
            {
                // COLD ALLOC: GameObject[1] — runtime relay director owner fallback when scene roots are missing — owner: EmergencyServiceRelayDirector
                owner = new GameObject("EmergencyServiceRelayDirector_Root");
            }

            _instance = owner.AddComponent<EmergencyServiceRelayDirector>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[EmergencyServiceRelayDirector] Spawned runtime relay director via Instance self-heal because no live owner existed. " +
                "Owner='" + owner.name + "'. This is a fail-safe, not a substitute for authored setup.");
#endif

            return _instance;
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
