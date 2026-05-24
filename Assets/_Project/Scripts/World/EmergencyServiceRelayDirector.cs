using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Core;
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
    public sealed class EmergencyServiceRelayDirector : MonoBehaviour, IEmergencyServiceRelayEventListener, IGlobalRegistryHotSwapListener
    {
        private const string DefaultIntroChainId = "intro_service_route";
        private const string DefaultRelayFallback =
            "HOLD TO THE SERVICE TRACE. RELAYS AND CACHES GIVE LORE, SUPPLIES, AND THE NEXT FOOTHOLD.";

        private static EmergencyServiceRelayDirector s_activeRuntime;

        public static EmergencyServiceRelayDirector ActiveRuntimeInstance => s_activeRuntime;

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

        private uint _lastGuidanceRelayHash;
        private uint _introChainHash;
        private bool _hasAnyRelayDiscovery;
        private EmergencyServiceRelay _currentRouteTarget;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private FirstHourDirector _firstHourDirector;
        private AtlasSignalSystem _atlasSignalSystem;
        private LocalizationManager _localizationManager;
        // COLD ALLOC: EmergencyServiceRelay[8] — driven relay chain cache — owner: EmergencyServiceRelayDirector
        private readonly List<EmergencyServiceRelay> _drivenChainRelays = new List<EmergencyServiceRelay>(8);
        // COLD ALLOC: Dictionary<uint, EmergencyServiceRelay>[8] - relay-hash lookup cache - owner: EmergencyServiceRelayDirector
        private readonly Dictionary<uint, EmergencyServiceRelay> _relayByHash =
            new Dictionary<uint, EmergencyServiceRelay>(8);
        // COLD ALLOC: Dictionary<int, EmergencyServiceRelay>[8] — relay-order lookup cache — owner: EmergencyServiceRelayDirector
        private readonly Dictionary<int, EmergencyServiceRelay> _relayByOrder =
            new Dictionary<int, EmergencyServiceRelay>(8);
        // COLD ALLOC: HashSet<uint>[8] - duplicate relay-hash guard - owner: EmergencyServiceRelayDirector
        private readonly HashSet<uint> _ambiguousRelayHashes =
            new HashSet<uint>(8);
        // COLD ALLOC: HashSet<int>[8] — duplicate relay-order guard — owner: EmergencyServiceRelayDirector
        private readonly HashSet<int> _ambiguousRelayOrders =
            new HashSet<int>(8);
        private int _observedRelayRegistryVersion = -1;

        private void OnEnable()
        {
            RefreshCachedHashes();
            CacheRegistryServicesCold();
            if (!TryRegisterService())
                return;

            TryRegisterHotSwapListener();
            EmergencyServiceRelayEvents.Register(this);
            InvalidateRelayCache();
            RefreshRelayDiscoveryState();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            EmergencyServiceRelayEvents.Unregister(this);
            InvalidateRelayCache();
            _currentRouteTarget = null;
            _lastGuidanceRelayHash = 0u;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            GlobalRegistry.RegisterEmergencyRelayRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EmergencyRelay, this);
            if (_serviceRegistered)
            {
                s_activeRuntime = this;
                return true;
            }

            Destroy(gameObject);
            return false;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.EmergencyRelay, this))
                GlobalRegistry.UnregisterEmergencyRelayRuntime(this);

            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
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
                    _firstHourDirector = currentService as FirstHourDirector;
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _atlasSignalSystem = currentService as AtlasSignalSystem;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as LocalizationManager;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _firstHourDirector = GlobalRegistry.FirstHour;
            _atlasSignalSystem = GlobalRegistry.AtlasSignal;
            _localizationManager = GlobalRegistry.Localization;
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

            RefreshCachedHashes();
            InvalidateRelayCache();
        }
#endif

        private void RefreshCachedHashes()
        {
            _introChainHash = string.IsNullOrWhiteSpace(introChainId)
                ? 0u
                : unchecked((uint)LocHash.Compute(introChainId));

            if (_introChainHash == 0u)
                _introChainHash = unchecked((uint)LocHash.Compute(DefaultIntroChainId));
        }

        /// <summary>Returns true when the first-hour relay chain already made real route contact with the player.</summary>
        public bool HasDiscoveredRelayInDrivenChain()
        {
            RefreshRelayDiscoveryState();
            return _hasAnyRelayDiscovery;
        }

        /// <summary>Returns true if the provided discovery hash belongs to an authored relay in the driven chain.</summary>
        public bool IsRelayDiscoveryHash(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return false;

            EnsureChainCache();
            return !_ambiguousRelayHashes.Contains(discoveryHash) && _relayByHash.ContainsKey(discoveryHash);
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

            uint nextRelayHash = nextRelay.RelayHash;
            if (_lastGuidanceRelayHash != 0u && _lastGuidanceRelayHash == nextRelayHash)
                return false;

            message = _hasAnyRelayDiscovery
                ? nextRelay.BuildBreadcrumbMessage()
                : nextRelay.BuildInitialRouteMessage();

            if (string.IsNullOrWhiteSpace(message))
                message = ResolveLocalized(LocalizationKeys.RELAY_DIRECTOR_FALLBACK, relayFallbackMessage);

            _lastGuidanceRelayHash = nextRelayHash;
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
            _lastGuidanceRelayHash = 0u;
            EmergencyServiceRelay nextRelay = ResolveRouteTargetAfterActivation(relay);
            _currentRouteTarget = nextRelay;

            if (firstActivation && relay.CountsAsLoreRouteContact)
                _firstHourDirector?.RegisterServiceRelayRouteContact();

            if (!firstActivation || !ShouldDriveBreadcrumbs())
                return;

            string routeMessage = relay.BuildDownloadedRouteMessage(nextRelay);
            if (!string.IsNullOrWhiteSpace(routeMessage))
                NotificationEvents.PushInfo(routeMessage);

            if (nextRelay != null)
                _lastGuidanceRelayHash = nextRelay.RelayHash;
        }

        void IEmergencyServiceRelayEventListener.OnEmergencyServiceRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            HandleRelayActivated(relay, firstActivation);
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
            FirstHourDirector firstHourDirector = _firstHourDirector;
            if (firstHourDirector != null)
            {
                if (!firstHourDirector.IsMilestoneComplete(minimumMilestoneToDrive))
                    return false;

                if (firstHourDirector.IsMilestoneComplete(terminalMilestone))
                    return false;
            }

            AtlasSignalSystem atlasSignalSystem = _atlasSignalSystem;
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
            if (relay == null)
                return false;

            uint chainHash = relay.ChainHash;
            return chainHash != 0u && chainHash == _introChainHash;
        }

        private bool IsValidRelayForRouting(EmergencyServiceRelay relay)
        {
            if (relay == null ||
                !relay.isActiveAndEnabled ||
                !IsRelayPartOfDrivenChain(relay))
            {
                return false;
            }

            uint relayHash = relay.RelayHash;
            return relayHash != 0u &&
                   !_ambiguousRelayHashes.Contains(relayHash) &&
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
            if (_introChainHash == 0u)
                RefreshCachedHashes();

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
            _relayByHash.Clear();
            _relayByOrder.Clear();
            _ambiguousRelayHashes.Clear();
            _ambiguousRelayOrders.Clear();

            for (int i = 0; i < EmergencyServiceRelay.ActiveCount; i++)
            {
                EmergencyServiceRelay relay = EmergencyServiceRelay.GetActiveRelayAt(i);
                if (relay == null || !relay.isActiveAndEnabled || !IsRelayPartOfDrivenChain(relay))
                    continue;

                uint relayHash = relay.RelayHash;
                if (relayHash == 0u)
                    continue;

                if (_relayByHash.ContainsKey(relayHash))
                {
                    _relayByHash.Remove(relayHash);
                    _ambiguousRelayHashes.Add(relayHash);
                }
                else if (!_ambiguousRelayHashes.Contains(relayHash))
                {
                    _relayByHash.Add(relayHash, relay);
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

            if (_lastGuidanceRelayHash != 0u &&
                (_ambiguousRelayHashes.Contains(_lastGuidanceRelayHash) || !_relayByHash.ContainsKey(_lastGuidanceRelayHash)))
            {
                _lastGuidanceRelayHash = 0u;
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

        private string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = _localizationManager;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
