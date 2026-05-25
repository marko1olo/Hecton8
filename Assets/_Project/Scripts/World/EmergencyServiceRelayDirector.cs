using System.Collections.Generic;
using Hecton.Localization;
using System;
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
    public sealed class EmergencyServiceRelayDirector : MonoBehaviour, IEmergencyServiceRelayEventListener, IEmergencyRelayRouteReadModel, IGlobalRegistryHotSwapListener
    {
        private const string DefaultIntroChainId = "intro_service_route";
        private const string DefaultRelayFallback =
            "HOLD TO THE SERVICE TRACE. RELAYS AND CACHES GIVE LORE, SUPPLIES, AND THE NEXT FOOTHOLD.";

        private static EmergencyServiceRelayDirector s_activeRuntime;

        public static EmergencyServiceRelayDirector ActiveRuntimeInstance => s_activeRuntime;

        internal static void NotifyRelayRegistryChanged()
        {
            EmergencyServiceRelayDirector runtime = s_activeRuntime;
            if (runtime != null && runtime.isActiveAndEnabled)
                runtime.RefreshRelayCacheAndDiscoveryState();
        }

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

        private uint _introChainHash;
        private bool _hasAnyRelayDiscovery;
        private EmergencyServiceRelay _currentRouteTarget;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private IFirstHourReadModel _firstHourReadModel;
        private IFirstHourRouteContactSink _firstHourRouteContactSink;
        private IAtlasSignalReadModel _atlasSignalSystem;
        private ILocalizationTextReadModel _localizationManager;
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
            RefreshRelayCacheAndDiscoveryState();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            EmergencyServiceRelayEvents.Unregister(this);
            InvalidateRelayCache();
            _currentRouteTarget = null;
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
                    _firstHourReadModel = currentService as IFirstHourReadModel;
                    _firstHourRouteContactSink = currentService as IFirstHourRouteContactSink;
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _atlasSignalSystem = currentService as IAtlasSignalReadModel;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _firstHourReadModel = GlobalRegistry.FirstHourReadModel;
            _firstHourRouteContactSink = _firstHourReadModel as IFirstHourRouteContactSink;
            _atlasSignalSystem = GlobalRegistry.AtlasSignalReadModel;
            _localizationManager = GlobalRegistry.LocalizationText;
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
            return _hasAnyRelayDiscovery;
        }

        /// <summary>Returns true if the provided discovery hash belongs to an authored relay in the driven chain.</summary>
        public bool IsRelayDiscoveryHash(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return false;

            return !_ambiguousRelayHashes.Contains(discoveryHash) && _relayByHash.ContainsKey(discoveryHash);
        }

        public bool TryBuildContextualGuidanceMessageSpan(out ReadOnlySpan<char> message)
        {
            message = ReadOnlySpan<char>.Empty;

            if (!ShouldDriveBreadcrumbs())
                return false;

            EmergencyServiceRelay nextRelay = ResolveCurrentRouteTarget();
            if (nextRelay == null)
                return false;

            uint nextRelayHash = nextRelay.RelayHash;
            message = _hasAnyRelayDiscovery
                ? nextRelay.BuildBreadcrumbMessageSpan()
                : nextRelay.BuildInitialRouteMessageSpan();

            if (IsWhiteSpace(message))
                message = ResolveLocalizedSpan(LocalizationKeys.RELAY_DIRECTOR_FALLBACK, relayFallbackMessage);

            return nextRelayHash != 0u && !IsWhiteSpace(message);
        }

        /// <summary>
        /// Returns the currently active relay target that should be surfaced on HUD route markers.
        /// </summary>
        public EmergencyServiceRelay GetActiveRouteTarget()
        {
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

        public bool TryReadActiveRouteTarget(out EmergencyRelayRouteTargetSnapshot snapshot)
        {
            snapshot = default;

            EmergencyServiceRelay target = GetActiveRouteTarget();
            if (target == null || !target.isActiveAndEnabled)
                return false;

            AbsoluteUniversePosition relayAup = target.RelayAup;
            if (!relayAup.IsFinite())
                return false;

            snapshot = new EmergencyRelayRouteTargetSnapshot
            {
                RelayAup = relayAup,
                RelayHash = target.RelayHash,
                ChainHash = target.ChainHash,
                RelayOrder = target.RelayOrder,
                Flags = EmergencyRelayRouteTargetSnapshot.ActiveFlag
            };
            return snapshot.RelayHash != 0u;
        }

        private void HandleRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            RefreshRelayCacheIfVersionChanged();
            if (!IsValidRelayForRouting(relay))
                return;

            _hasAnyRelayDiscovery = true;
            EmergencyServiceRelay nextRelay = ResolveRouteTargetAfterActivation(relay);
            _currentRouteTarget = nextRelay;

            if (firstActivation && relay.CountsAsLoreRouteContact)
                _firstHourRouteContactSink?.RegisterServiceRelayRouteContact();

            if (!firstActivation || !ShouldDriveBreadcrumbs())
                return;

            ReadOnlySpan<char> routeMessage = relay.BuildDownloadedRouteMessageSpan(nextRelay);
            if (!IsWhiteSpace(routeMessage))
                NotificationEvents.TryPushInfo(routeMessage);

        }

        void IEmergencyServiceRelayEventListener.OnEmergencyServiceRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            HandleRelayActivated(relay, firstActivation);
        }

        private void RefreshRelayCacheAndDiscoveryState()
        {
            RefreshRelayCacheIfVersionChanged();
            RefreshRelayDiscoveryStateFromCache();
        }

        private void RefreshRelayDiscoveryStateFromCache()
        {
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
            IFirstHourReadModel firstHourDirector = _firstHourReadModel;
            if (firstHourDirector != null)
            {
                if (!firstHourDirector.IsFirstHourMilestoneComplete((int)minimumMilestoneToDrive))
                    return false;

                if (firstHourDirector.IsFirstHourMilestoneComplete((int)terminalMilestone))
                    return false;
            }

            IAtlasSignalReadModel atlasSignalSystem = _atlasSignalSystem;
            if (atlasSignalSystem != null && atlasSignalSystem.CurrentAtlasSignalRevealStage > maximumAtlasRevealStageToDrive)
                return false;

            return true;
        }

        private EmergencyServiceRelay ResolveCurrentRouteTarget()
        {
            if (_currentRouteTarget != null &&
                IsValidRelayForRouting(_currentRouteTarget) &&
                !_currentRouteTarget.IsDiscovered &&
                _currentRouteTarget.isActiveAndEnabled)
            {
                return _currentRouteTarget;
            }

            return ResolveRouteTargetFromDiscoveryState(null);
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

        private void RefreshRelayCacheIfVersionChanged()
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

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localizationManager;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private static bool IsWhiteSpace(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }
    }
}
