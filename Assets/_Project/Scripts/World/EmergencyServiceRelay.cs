using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Scene-authored breadcrumb stop that grants small cached rewards, a lore beat, and the next relay handoff.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(FieldTargetDescriptor))]
    [AddComponentMenu("Hecton8/World/Emergency Service Relay")]
    public sealed class EmergencyServiceRelay : MonoBehaviour, IInteractable, IInteractableTextProvider, IGlobalRegistryHotSwapListener
    {
        [Serializable]
        public struct RewardEntry
        {
            [Tooltip("Reward item granted from the relay cache.")]
            public ItemData item;

            [Tooltip("Amount granted on first access.")]
            public int quantity;
        }

        private const string DefaultChainId = "intro_service_route";
        private const string DefaultInteractVerb = "OPEN RELAY";
        private const string DefaultReviewVerb = "REVIEW RELAY";
        private const string DefaultLabel = "EMERGENCY SERVICE RELAY";
        private const string DefaultLoreMessage =
            "SERVICE RELAY: the cache still holds an emergency log, a dry ration, and a fragment of the maintenance route.";
        private const string DefaultInitialRouteMessage =
            "FIND THE FIRST SERVICE RELAY. THEY SIT WHERE THE ROUTE CAN STILL BE READ WITH YOUR EYES.";
        private const string DefaultBreadcrumbMessage =
            "HOLD THE SERVICE LINE. THE NEXT RELAY SITS A LITTLE DEEPER AND HANDS OFF THE NEXT VECTOR.";
        private const string DefaultDescriptorNote =
            "Emergency service relay with cached supplies and a maintenance-route handoff.";
        private const string DownloadedBelowFallback =
            "SERVICE RELAY: NEXT VECTOR RESOLVED BELOW CURRENT DEPTH.";
        private const string DownloadedFartherFallback =
            "SERVICE RELAY: NEXT VECTOR RESOLVED ALONG THE SERVICE LINE.";
        private const string RewardGrantedFallback = "RELAY CACHE DISPENSED.";
        private const string RewardInventoryFullFallback = "RELAY FOUND SUPPLIES, BUT INVENTORY IS FULL. FREE SPACE AND COME BACK.";
        private const string RewardEmptyFallback = "RELAY CACHE DISPENSED";
        private const string DefaultOpenInteractText = "OPEN RELAY EMERGENCY SERVICE RELAY";
        private const string DefaultReviewInteractText = "REVIEW RELAY EMERGENCY SERVICE RELAY";

        // COLD ALLOC: EmergencyServiceRelay[16] — active authored relay registry — owner: EmergencyServiceRelay
        private static readonly List<EmergencyServiceRelay> s_ActiveRelays = new List<EmergencyServiceRelay>(16);
        private static int s_RegistryVersion;
        private readonly char[] _cachedInteractTextBuffer = new char[128];
        private int _cachedInteractTextLength;

        [Header("-- Identity -------------------------------")]
        [Tooltip("Unique discovery ID used for persistence and relay-chain state.")]
        [SerializeField] private string relayId = "relay_intro_01";

        [Tooltip("Chain owner ID. Intro relays should stay inside the same chain.")]
        [SerializeField] private string chainId = DefaultChainId;

        [Tooltip("Ordering inside the chain. Lower values are earlier breadcrumbs.")]
        [SerializeField, Min(0)] private int relayOrder;

        [Tooltip("Readable relay label shown in handoff messages.")]
        [SerializeField] private string relayLabel = DefaultLabel;

        [Header("-- Route Handoff --------------------------")]
        [Tooltip("Explicit next relay in the chain. If absent, the director falls back to relayOrder.")]
        [SerializeField] private EmergencyServiceRelay nextRelay;

        [Tooltip("Stage-0 route prompt used before the player discovers any relay in the driven chain.")]
        [SerializeField, TextArea(2, 4)] private string initialRouteMessage = DefaultInitialRouteMessage;

        [Tooltip("Reminder used when the player should keep following the relay chain.")]
        [SerializeField, TextArea(2, 4)] private string breadcrumbMessage = DefaultBreadcrumbMessage;

        [Tooltip("Whether this relay should count as a real lore-route contact for first-hour pacing.")]
        [SerializeField] private bool countsAsLoreRouteContact = true;

        [Header("-- Lore + Cache ---------------------------")]
        [Tooltip("Primary lore beat delivered on first access.")]
        [SerializeField, TextArea(2, 5)] private string loreMessage = DefaultLoreMessage;

        [Tooltip("Optional audio log played when the relay is opened.")]
        [SerializeField] private Hecton8.Narrative.AudioLogData linkedAudioLog;

        [Tooltip("Small cached reward bundle granted on first access.")]
        [SerializeField] private RewardEntry[] rewards = Array.Empty<RewardEntry>();

        [Header("-- Scanner Semantics ----------------------")]
        [Tooltip("Semantic role exposed to scanners and other field-read systems.")]
        [SerializeField] private FieldTargetRole fieldRole = FieldTargetRole.RouteRelay;

        [Tooltip("Operator note surfaced through field-read tools.")]
        [SerializeField, TextArea(2, 4)] private string fieldOperatorNote = DefaultDescriptorNote;

        [Header("-- Interaction ----------------------------")]
        [Tooltip("Verb shown before the relay has been opened.")]
        [SerializeField] private string interactVerb = DefaultInteractVerb;

        [Tooltip("Verb shown after the relay has already been opened once.")]
        [SerializeField] private string reviewVerb = DefaultReviewVerb;

        [Tooltip("Optional highlight owner toggled on hover.")]
        [SerializeField] private GameObject highlightObject;

        private FieldTargetDescriptor _descriptor;
        private InteractionHighlighter _highlighter;
        private Transform _cachedTransform;
        private uint _relayHash;
        private uint _chainHash;
        private AbsoluteUniversePosition _cachedRelayAup;
        private bool _hasCachedRelayAup;
        private INarrativeDiscoveryReadModel _cachedNarrativeDiscovery;
        private IAudioLogRuntime _cachedAudioLogSystem;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ILocalizationTextReadModel _cachedLocalization;
        private bool _registeredHotSwapListener;

        /// <summary>Unique discovery ID for this relay.</summary>
        public string RelayId => relayId;

        /// <summary>Runtime discovery hash used by event lanes and director caches.</summary>
        public uint RelayHash
        {
            get
            {
                EnsureCachedRuntimeIdentity();
                return _relayHash;
            }
        }

        /// <summary>Chain owner ID used by the relay director.</summary>
        public string ChainId => string.IsNullOrWhiteSpace(chainId) ? DefaultChainId : chainId;

        /// <summary>Runtime chain hash used by relay director caches.</summary>
        public uint ChainHash
        {
            get
            {
                EnsureCachedRuntimeIdentity();
                return _chainHash;
            }
        }

        /// <summary>Authored relay AUP cached outside route-message playback.</summary>
        public AbsoluteUniversePosition RelayAup
        {
            get
            {
                EnsureCachedRuntimeIdentity();
                return _cachedRelayAup;
            }
        }

        /// <summary>Ordering inside the authored relay chain.</summary>
        public int RelayOrder => relayOrder;

        /// <summary>Readable label span shown in route-handoff copy.</summary>
        public ReadOnlySpan<char> ResolveRelayLabelSpan()
        {
            return FallbackOrLocalizedSpan(relayLabel, LocalizationKeys.RELAY_LABEL_DEFAULT, DefaultLabel);
        }

        /// <summary>Explicit next relay, when authored.</summary>
        public EmergencyServiceRelay NextRelay => nextRelay;

        /// <summary>True when this relay should advance first-hour lore-route contact state.</summary>
        public bool CountsAsLoreRouteContact => countsAsLoreRouteContact;

        /// <summary>True when the narrative layer already knows this relay was accessed.</summary>
        public bool IsDiscovered
        {
            get
            {
                EnsureCachedRuntimeIdentity();
                INarrativeDiscoveryReadModel narrativeDiscovery = _cachedNarrativeDiscovery;
                return narrativeDiscovery != null && _relayHash != 0u && narrativeDiscovery.HasDiscovery(_relayHash);
            }
        }

        /// <summary>Number of active relay nodes in the scene.</summary>
        public static int ActiveCount => s_ActiveRelays.Count;
        internal static int RegistryVersion => s_RegistryVersion;

        private void Awake()
        {
            _cachedTransform = transform;
            RefreshCachedRuntimeIdentity();
            TryResolveComponents();
            ApplyDescriptorSemantics();
            RebuildInteractText();
        }

        private void OnEnable()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RefreshCachedRuntimeIdentity();
            TryResolveComponents();
            ApplyDescriptorSemantics();
            InteractableRegistry.RegisterTree(this);
            RebuildInteractText();

            if (!s_ActiveRelays.Contains(this))
            {
                s_ActiveRelays.Add(this);
                MarkRegistryDirty();
            }

        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();

            if (s_ActiveRelays.Remove(this))
                MarkRegistryDirty();

            if (highlightObject != null)
                highlightObject.SetActive(false);

            _highlighter?.SetHighlight(false);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();

            if (s_ActiveRelays.Remove(this))
                MarkRegistryDirty();
        }

        /// <summary>Returns the active relay at the given registry index.</summary>
        public static EmergencyServiceRelay GetActiveRelayAt(int index)
        {
            return index >= 0 && index < s_ActiveRelays.Count
                ? s_ActiveRelays[index]
                : null;
        }

        /// <inheritdoc />
        public void OnHoverStart()
        {
            if (highlightObject != null)
                highlightObject.SetActive(true);

            _highlighter?.SetHighlight(true);
        }

        /// <inheritdoc />
        public void OnHoverEnd()
        {
            if (highlightObject != null)
                highlightObject.SetActive(false);

            _highlighter?.SetHighlight(false);
        }

        /// <inheritdoc />
        public void Interact(Transform interactor)
        {
            InteractionEvents.TryRaiseInteractionStarted(this, interactor);
            EnsureCachedRuntimeIdentity();

            bool firstActivation = !IsDiscovered;
            if (firstActivation && _relayHash != 0u)
                NarrativeEvents.TryRaiseDiscoveryMade(_relayHash);

            ReadOnlySpan<char> resolvedLoreMessage = FallbackOrLocalizedSpan(loreMessage, LocalizationKeys.RELAY_LORE_DEFAULT, DefaultLoreMessage);
            if (!IsWhiteSpace(resolvedLoreMessage))
                NotificationEvents.TryPushInfo(resolvedLoreMessage);

            IAudioLogRuntime audioLogSystem = _cachedAudioLogSystem;
            if (linkedAudioLog != null && audioLogSystem != null)
                audioLogSystem.TryPlayAudioLog(linkedAudioLog.logId);

            TryGrantRewards(interactor);
            EmergencyServiceRelayEvents.TryRaiseRelayActivated(this, firstActivation);
            RebuildInteractText();
        }

        /// <inheritdoc />
        public string GetInteractText()
        {
            return IsDiscovered ? DefaultReviewInteractText : DefaultOpenInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength), destination, out length);
        }

        public ReadOnlySpan<char> BuildInitialRouteMessageSpan()
        {
            return FallbackOrLocalizedSpan(initialRouteMessage, LocalizationKeys.RELAY_ROUTE_INITIAL, DefaultInitialRouteMessage);
        }

        public ReadOnlySpan<char> BuildBreadcrumbMessageSpan()
        {
            return FallbackOrLocalizedSpan(breadcrumbMessage, LocalizationKeys.RELAY_ROUTE_BREADCRUMB, DefaultBreadcrumbMessage);
        }

        public ReadOnlySpan<char> BuildDownloadedRouteMessageSpan(EmergencyServiceRelay resolvedNextRelay)
        {
            if (resolvedNextRelay == null)
            {
                return ResolveLocalizedSpan(
                    LocalizationKeys.RELAY_ROUTE_TERMINUS,
                    "SERVICE RELAY: local route ends here. LOOK FOR THE BIGGER TRACE, THE RUINS, AND AN INTACT MODULE.");
            }

            AbsoluteUniversePosition currentAup = RelayAup;
            AbsoluteUniversePosition nextAup = resolvedNextRelay.RelayAup;
            bool deeper = ResolveVerticalDeltaMeters(in currentAup, in nextAup) < 0d;
            return deeper
                ? DownloadedBelowFallback.AsSpan()
                : DownloadedFartherFallback.AsSpan();
        }

        private void EnsureCachedRuntimeIdentity()
        {
            if (_hasCachedRelayAup && _relayHash != 0u && _chainHash != 0u)
                return;

            RefreshCachedRuntimeIdentity();
        }

        private void RefreshCachedRuntimeIdentity()
        {
            _relayHash = string.IsNullOrWhiteSpace(relayId)
                ? 0u
                : unchecked((uint)LocHash.Compute(relayId));

            string resolvedChainId = string.IsNullOrWhiteSpace(chainId)
                ? DefaultChainId
                : chainId;
            _chainHash = unchecked((uint)LocHash.Compute(resolvedChainId));
            Transform relayTransform = _cachedTransform;
            if (relayTransform == null)
            {
                relayTransform = transform;
                _cachedTransform = relayTransform;
            }

            if (!TryResolveRelayAup(relayTransform, out _cachedRelayAup))
            {
                _hasCachedRelayAup = false;
                return;
            }

            _hasCachedRelayAup = true;
        }

        private static bool TryResolveRelayAup(Transform relayTransform, out AbsoluteUniversePosition relayAup)
        {
            relayAup = default;
            if (relayTransform == null)
                return false;

            Vector3 runtimePosition = relayTransform.position;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            relayAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return relayAup.IsFinite();
        }

        private static double ResolveVerticalDeltaMeters(in AbsoluteUniversePosition from, in AbsoluteUniversePosition to)
        {
            return ((to.GridY - from.GridY) * (double)AbsoluteUniversePosition.CellSizeMeters) + ((double)to.LocalY - from.LocalY);
        }

        private static void MarkRegistryDirty()
        {
            unchecked
            {
                s_RegistryVersion++;
            }

            EmergencyServiceRelayDirector.NotifyRelayRegistryChanged();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.NarrativeDirectorRuntime:
                    _cachedNarrativeDiscovery = currentService as INarrativeDiscoveryReadModel;
                    RebuildInteractText();
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _cachedAudioLogSystem = currentService as IAudioLogRuntime;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationTextReadModel;
                    ApplyDescriptorSemantics();
                    RebuildInteractText();
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedNarrativeDiscovery = GlobalRegistry.NarrativeDiscoveryReadModel;
            _cachedAudioLogSystem = GlobalRegistry.AudioLogRuntime;
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedLocalization = GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryResolveComponents()
        {
            if (_descriptor == null)
                TryGetComponent(out _descriptor);

            if (_highlighter == null)
                TryGetComponent(out _highlighter);
        }

        private void ApplyDescriptorSemantics()
        {
            if (_descriptor == null)
                return;

            string descriptorNote = string.IsNullOrWhiteSpace(fieldOperatorNote)
                ? DefaultDescriptorNote
                : fieldOperatorNote;
            _descriptor.Configure(fieldRole, descriptorNote);
        }

        private void RebuildInteractText()
        {
            ReadOnlySpan<char> verb = IsDiscovered
                ? FallbackOrLocalizedSpan(reviewVerb, LocalizationKeys.RELAY_INTERACT_REVIEW, DefaultReviewVerb)
                : FallbackOrLocalizedSpan(interactVerb, LocalizationKeys.RELAY_INTERACT_OPEN, DefaultInteractVerb);
            ReadOnlySpan<char> label = FallbackOrLocalizedSpan(relayLabel, LocalizationKeys.RELAY_LABEL_DEFAULT, DefaultLabel);
            _cachedInteractTextLength = 0;
            AppendSpan(_cachedInteractTextBuffer, ref _cachedInteractTextLength, verb);
            AppendChar(_cachedInteractTextBuffer, ref _cachedInteractTextLength, ' ');
            AppendSpan(_cachedInteractTextBuffer, ref _cachedInteractTextLength, label);
        }

        private void TryGrantRewards(Transform interactor)
        {
            if (rewards == null || rewards.Length == 0 || !TryResolveInventory(interactor, out PlayerInventory inventory))
                return;

            bool grantedAny = false;
            bool inventoryFull = false;

            for (int i = 0; i < rewards.Length; i++)
            {
                ItemData item = rewards[i].item;
                int quantity = math.max(0, rewards[i].quantity);
                if (item == null || quantity <= 0)
                    continue;

                if (item != null && inventory.TryAddItem(Hecton.Localization.LocHash.Compute(item.PersistentId), quantity))
                {
                    grantedAny = true;
                    continue;
                }

                inventoryFull = true;
            }

            if (grantedAny)
                NotificationEvents.TryPushInfo(BuildRewardGrantedMessageSpan());

            if (inventoryFull)
                NotificationEvents.TryPushWarning(
                    ResolveLocalizedSpan(
                        LocalizationKeys.RELAY_REWARD_INVENTORY_FULL,
                        RewardInventoryFullFallback));
        }

        private ReadOnlySpan<char> BuildRewardGrantedMessageSpan()
        {
            for (int i = 0; i < rewards.Length; i++)
            {
                ItemData item = rewards[i].item;
                int quantity = math.max(0, rewards[i].quantity);
                if (item == null || quantity <= 0)
                    continue;

                return RewardGrantedFallback.AsSpan();
            }

            return ResolveLocalizedSpan(LocalizationKeys.RELAY_REWARD_EMPTY, RewardEmptyFallback);
        }

        private bool TryResolveInventory(Transform interactor, out PlayerInventory inventory)
        {
            inventory = null;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;

            if (interactor != null)
                interactor.TryGetComponent(out inventory);

            if (inventory != null)
                return true;

            if (playerContext != null && playerContext.Inventory != null)
            {
                inventory = playerContext.Inventory;
                return true;
            }

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return false;

            playerTransform.TryGetComponent(out inventory);
            return inventory != null;
        }

        private ReadOnlySpan<char> FallbackOrLocalizedSpan(string value, string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ResolveLocalizedSpan(key, fallback)
                : value.AsSpan();
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private static void AppendSpan(char[] destination, ref int length, ReadOnlySpan<char> source)
        {
            int available = destination.Length - length;
            if (available <= 0)
                return;

            int copyLength = math.min(source.Length, available);
            source.Slice(0, copyLength).CopyTo(destination.AsSpan(length, copyLength));
            length += copyLength;
        }

        private static void AppendChar(char[] destination, ref int length, char value)
        {
            if (length >= destination.Length)
                return;

            destination[length++] = value;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            relayId = string.IsNullOrWhiteSpace(relayId)
                ? relayId
                : relayId.Trim();
            chainId = string.IsNullOrWhiteSpace(chainId)
                ? chainId
                : chainId.Trim();
            relayLabel = string.IsNullOrWhiteSpace(relayLabel)
                ? relayLabel
                : relayLabel.Trim();
            interactVerb = string.IsNullOrWhiteSpace(interactVerb)
                ? interactVerb
                : interactVerb.Trim();
            reviewVerb = string.IsNullOrWhiteSpace(reviewVerb)
                ? reviewVerb
                : reviewVerb.Trim();

            if (string.IsNullOrWhiteSpace(relayId))
                relayId = gameObject.name.ToLowerInvariant().Replace(" ", "_");

            if (string.IsNullOrWhiteSpace(chainId))
                chainId = DefaultChainId;

            if (nextRelay == this)
                nextRelay = null;

            if (rewards != null)
            {
                for (int i = 0; i < rewards.Length; i++)
                {
                    RewardEntry reward = rewards[i];
                    if (reward.quantity < 0)
                    {
                        reward.quantity = 0;
                        rewards[i] = reward;
                    }
                }
            }

            TryResolveComponents();
            CacheRegistryServicesCold();
            ApplyDescriptorSemantics();
            RefreshCachedRuntimeIdentity();
            RebuildInteractText();

            if (Application.isPlaying && isActiveAndEnabled)
                MarkRegistryDirty();
        }
#endif
    }
}
