using System;
using System.Collections.Generic;
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

        private static EmergencyServiceRelayDirector _instance;
        public static EmergencyServiceRelayDirector Instance => ResolveInstance();
        internal static EmergencyServiceRelayDirector ActiveRuntimeInstance => _instance;

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
        // COLD ALLOC: EmergencyServiceRelay[8] — driven relay chain cache — owner: EmergencyServiceRelayDirector
        private readonly List<EmergencyServiceRelay> _drivenChainRelays = new List<EmergencyServiceRelay>(8);
        // COLD ALLOC: Dictionary<string, EmergencyServiceRelay>[8] — relay-id lookup cache — owner: EmergencyServiceRelayDirector
        private readonly Dictionary<string, EmergencyServiceRelay> _relayById =
            new Dictionary<string, EmergencyServiceRelay>(8, StringComparer.Ordinal);
        // COLD ALLOC: Dictionary<int, EmergencyServiceRelay>[8] — relay-order lookup cache — owner: EmergencyServiceRelayDirector
        private readonly Dictionary<int, EmergencyServiceRelay> _relayByOrder =
            new Dictionary<int, EmergencyServiceRelay>(8);
        // COLD ALLOC: HashSet<string>[8] — duplicate relay-id guard — owner: EmergencyServiceRelayDirector
        private readonly HashSet<string> _ambiguousRelayIds =
            new HashSet<string>(StringComparer.Ordinal);
        // COLD ALLOC: HashSet<int>[8] — duplicate relay-order guard — owner: EmergencyServiceRelayDirector
        private readonly HashSet<int> _ambiguousRelayOrders =
            new HashSet<int>();
        private int _observedRelayRegistryVersion = -1;

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
            EmergencyServiceRelayEvents.RegisterRelayActivated(HandleRelayActivated);
            InvalidateRelayCache();
            RefreshRelayDiscoveryState();
        }

        private void OnDisable()
        {
            EmergencyServiceRelayEvents.UnregisterRelayActivated(HandleRelayActivated);
            InvalidateRelayCache();
            _currentRouteTarget = null;
            _lastGuidanceRelayId = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            introChainId = string.IsNullOrWhiteSpace(introChainId)
                ? introChainId
                : introChainId.Trim();

            if (string.IsNullOrWhiteSpace(introChainId))
                introChainId = DefaultIntroChainId;

            if (maximumAtlasRevealStageToDrive < 0)
                maximumAtlasRevealStageToDrive = 0;

            InvalidateRelayCache();
        }
#endif

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

            EnsureChainCache();
            return !_ambiguousRelayIds.Contains(discoveryId) && _relayById.ContainsKey(discoveryId);
        }

        /// <summary>
        /// Returns a one-shot contextual guidance message that points the player at the next undiscovered relay.
        /// </summary>
        public bool TryBuildContextualGuidanceMessage(out string message)
        {
            message = null;

            if (!ShouldDriveBreadcrumbs())
                return false;

            EnsureChainCache();
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
            EnsureChainCache();

            if (_currentRouteTarget != null &&
                IsValidRelayForRouting(_currentRouteTarget) &&
                !_currentRouteTarget.IsDiscovered &&
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
            EnsureChainCache();
            if (!IsValidRelayForRouting(relay))
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
            EnsureChainCache();
            _hasAnyRelayDiscovery = false;
            _currentRouteTarget = null;
            EmergencyServiceRelay highestDiscoveredRelay = null;
            int highestDiscoveredOrder = int.MinValue;

            for (int i = 0; i < _drivenChainRelays.Count; i++)
            {
                EmergencyServiceRelay relay = _drivenChainRelays[i];
                if (!IsValidRelayForRouting(relay) || !relay.IsDiscovered)
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
            EnsureChainCache();

            if (_currentRouteTarget != null &&
                IsValidRelayForRouting(_currentRouteTarget) &&
                !_currentRouteTarget.IsDiscovered &&
                _currentRouteTarget.isActiveAndEnabled)
            {
                return _currentRouteTarget;
            }

            RefreshRelayDiscoveryState();
            return _currentRouteTarget;
        }

        private EmergencyServiceRelay ResolveRouteTargetAfterActivation(EmergencyServiceRelay currentRelay)
        {
            if (currentRelay == null)
                return null;

            EmergencyServiceRelay explicitNextRelay = currentRelay.NextRelay;
            if (CanUseExplicitNextRelay(currentRelay, explicitNextRelay))
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
            if (CanUseExplicitNextRelay(currentRelay, explicitNextRelay))
            {
                return explicitNextRelay;
            }

            EmergencyServiceRelay bestRelay = null;
            int minimumOrder = currentRelay != null ? currentRelay.RelayOrder + 1 : int.MinValue;

            for (int i = 0; i < _drivenChainRelays.Count; i++)
            {
                EmergencyServiceRelay relay = _drivenChainRelays[i];
                if (!IsValidRelayForRouting(relay) || relay.IsDiscovered)
                    continue;

                if (relay.RelayOrder < minimumOrder)
                    continue;

                bestRelay = relay;
                break;
            }

            return bestRelay;
        }

        private bool IsRelayPartOfDrivenChain(EmergencyServiceRelay relay)
        {
            return relay != null &&
                string.Equals(relay.ChainId, introChainId, StringComparison.Ordinal);
        }

        private bool IsValidRelayForRouting(EmergencyServiceRelay relay)
        {
            if (relay == null ||
                !relay.isActiveAndEnabled ||
                !IsRelayPartOfDrivenChain(relay))
            {
                return false;
            }

            string relayId = relay.RelayId;
            return !string.IsNullOrWhiteSpace(relayId) &&
                   !_ambiguousRelayIds.Contains(relayId) &&
                   !_ambiguousRelayOrders.Contains(relay.RelayOrder);
        }

        private bool CanUseExplicitNextRelay(EmergencyServiceRelay currentRelay, EmergencyServiceRelay explicitNextRelay)
        {
            if (!IsValidRelayForRouting(explicitNextRelay) || explicitNextRelay.IsDiscovered)
                return false;

            if (currentRelay == null)
                return true;

            return explicitNextRelay.RelayOrder > currentRelay.RelayOrder;
        }

        private void EnsureChainCache()
        {
            int registryVersion = EmergencyServiceRelay.RegistryVersion;
            if (_observedRelayRegistryVersion == registryVersion)
                return;

            RebuildDrivenChainCache(registryVersion);
        }

        private void InvalidateRelayCache()
        {
            _observedRelayRegistryVersion = -1;
        }

        private void RebuildDrivenChainCache(int registryVersion)
        {
            _drivenChainRelays.Clear();
            _relayById.Clear();
            _relayByOrder.Clear();
            _ambiguousRelayIds.Clear();
            _ambiguousRelayOrders.Clear();

            for (int i = 0; i < EmergencyServiceRelay.ActiveCount; i++)
            {
                EmergencyServiceRelay relay = EmergencyServiceRelay.GetActiveRelayAt(i);
                if (relay == null || !relay.isActiveAndEnabled || !IsRelayPartOfDrivenChain(relay))
                    continue;

                string relayId = relay.RelayId;
                if (string.IsNullOrWhiteSpace(relayId))
                    continue;

                if (_relayById.ContainsKey(relayId))
                {
                    _relayById.Remove(relayId);
                    _ambiguousRelayIds.Add(relayId);
                }
                else if (!_ambiguousRelayIds.Contains(relayId))
                {
                    _relayById.Add(relayId, relay);
                }

                int relayOrder = relay.RelayOrder;
                if (_relayByOrder.ContainsKey(relayOrder))
                {
                    _relayByOrder.Remove(relayOrder);
                    _ambiguousRelayOrders.Add(relayOrder);
                }
                else if (!_ambiguousRelayOrders.Contains(relayOrder))
                {
                    _relayByOrder.Add(relayOrder, relay);
                }

                _drivenChainRelays.Add(relay);
            }

            SortDrivenRelaysByOrder();
            _observedRelayRegistryVersion = registryVersion;

            if (!IsValidRelayForRouting(_currentRouteTarget) || (_currentRouteTarget != null && _currentRouteTarget.IsDiscovered))
                _currentRouteTarget = null;

            if (!string.IsNullOrEmpty(_lastGuidanceRelayId) &&
                (_ambiguousRelayIds.Contains(_lastGuidanceRelayId) || !_relayById.ContainsKey(_lastGuidanceRelayId)))
            {
                _lastGuidanceRelayId = null;
            }
        }

        private void SortDrivenRelaysByOrder()
        {
            for (int i = 1; i < _drivenChainRelays.Count; i++)
            {
                EmergencyServiceRelay relay = _drivenChainRelays[i];
                int relayOrder = relay.RelayOrder;
                int insertIndex = i - 1;
                while (insertIndex >= 0 && _drivenChainRelays[insertIndex].RelayOrder > relayOrder)
                {
                    _drivenChainRelays[insertIndex + 1] = _drivenChainRelays[insertIndex];
                    insertIndex--;
                }

                _drivenChainRelays[insertIndex + 1] = relay;
            }
        }

        private static EmergencyServiceRelayDirector ResolveInstance()
        {
            if (_instance != null)
                return _instance;

            if (!Application.isPlaying || EmergencyServiceRelay.ActiveCount <= 0)
                return null;

            GameObject owner = null;
            WorldRuntimeReferenceUtility.TryResolveManagersRoot(ref owner);

            if (owner == null)
            {
                // COLD ALLOC: GameObject[1] — runtime relay director owner fallback when scene roots are missing — owner: EmergencyServiceRelayDirector
                owner = new GameObject("EmergencyServiceRelayDirector_Root");
            }

            if (!owner.TryGetComponent(out _instance))
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
