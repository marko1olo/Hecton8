using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    public sealed class EmergencyServiceRelay : MonoBehaviour, IInteractable
    {
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
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

        // COLD ALLOC: EmergencyServiceRelay[16] — active authored relay registry — owner: EmergencyServiceRelay
        private static readonly List<EmergencyServiceRelay> s_ActiveRelays = new List<EmergencyServiceRelay>(16);
        private static int s_RegistryVersion;

        [Header("── Identity ───────────────────────────────")]
        [Tooltip("Unique discovery ID used for persistence and relay-chain state.")]
        [SerializeField] private string relayId = "relay_intro_01";

        [Tooltip("Chain owner ID. Intro relays should stay inside the same chain.")]
        [SerializeField] private string chainId = DefaultChainId;

        [Tooltip("Ordering inside the chain. Lower values are earlier breadcrumbs.")]
        [SerializeField, Min(0)] private int relayOrder;

        [Tooltip("Readable relay label shown in handoff messages.")]
        [SerializeField] private string relayLabel = DefaultLabel;

        [Header("── Route Handoff ──────────────────────────")]
        [Tooltip("Explicit next relay in the chain. If absent, the director falls back to relayOrder.")]
        [SerializeField] private EmergencyServiceRelay nextRelay;

        [Tooltip("Stage-0 route prompt used before the player discovers any relay in the driven chain.")]
        [SerializeField, TextArea(2, 4)] private string initialRouteMessage = DefaultInitialRouteMessage;

        [Tooltip("Reminder used when the player should keep following the relay chain.")]
        [SerializeField, TextArea(2, 4)] private string breadcrumbMessage = DefaultBreadcrumbMessage;

        [Tooltip("Whether this relay should count as a real lore-route contact for first-hour pacing.")]
        [SerializeField] private bool countsAsLoreRouteContact = true;

        [Header("── Lore + Cache ───────────────────────────")]
        [Tooltip("Primary lore beat delivered on first access.")]
        [SerializeField, TextArea(2, 5)] private string loreMessage = DefaultLoreMessage;

        [Tooltip("Optional audio log played when the relay is opened.")]
        [SerializeField] private Hecton8.Narrative.AudioLogData linkedAudioLog;

        [Tooltip("Small cached reward bundle granted on first access.")]
        [SerializeField] private RewardEntry[] rewards = Array.Empty<RewardEntry>();

        [Header("── Scanner Semantics ──────────────────────")]
        [Tooltip("Semantic role exposed to scanners and other field-read systems.")]
        [SerializeField] private FieldTargetRole fieldRole = FieldTargetRole.RouteRelay;

        [Tooltip("Operator note surfaced through field-read tools.")]
        [SerializeField, TextArea(2, 4)] private string fieldOperatorNote = DefaultDescriptorNote;

        [Header("── Interaction ────────────────────────────")]
        [Tooltip("Verb shown before the relay has been opened.")]
        [SerializeField] private string interactVerb = DefaultInteractVerb;

        [Tooltip("Verb shown after the relay has already been opened once.")]
        [SerializeField] private string reviewVerb = DefaultReviewVerb;

        [Tooltip("Optional highlight owner toggled on hover.")]
        [SerializeField] private GameObject highlightObject;

        private FieldTargetDescriptor _descriptor;
        private InteractionHighlighter _highlighter;
        private Transform _cachedTransform;
        private string _cachedInteractText = DefaultInteractVerb + " " + DefaultLabel;
        private uint _relayHash;
        private uint _chainHash;
        private AbsoluteUniversePosition _cachedRelayAup;
        private bool _hasCachedRelayAup;

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

        /// <summary>Readable label shown in route-handoff copy.</summary>
        public string RelayLabel => FallbackOrLocalized(relayLabel, LocalizationKeys.RELAY_LABEL_DEFAULT, DefaultLabel);

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
                HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
                return narrativeDirector != null && _relayHash != 0u && narrativeDirector.HasDiscovery(_relayHash);
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

            RefreshCachedRuntimeIdentity();
            TryResolveComponents();
            ApplyDescriptorSemantics();
            RebuildInteractText();

            if (!s_ActiveRelays.Contains(this))
            {
                s_ActiveRelays.Add(this);
                MarkRegistryDirty();
            }

        }

        private void OnDisable()
        {
            if (s_ActiveRelays.Remove(this))
                MarkRegistryDirty();

            if (highlightObject != null)
                highlightObject.SetActive(false);

            _highlighter?.SetHighlight(false);
        }

        private void OnDestroy()
        {
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
            InteractionEvents.RaiseInteractionStarted(this, interactor);
            EnsureCachedRuntimeIdentity();

            bool firstActivation = !IsDiscovered;
            if (firstActivation && _relayHash != 0u)
                NarrativeEvents.RaiseDiscoveryMade(_relayHash);

            string resolvedLoreMessage = FallbackOrLocalized(loreMessage, LocalizationKeys.RELAY_LORE_DEFAULT, DefaultLoreMessage);
            if (!string.IsNullOrWhiteSpace(resolvedLoreMessage))
                NotificationEvents.PushInfo(resolvedLoreMessage);

            if (linkedAudioLog != null && Hecton8.Core.GlobalRegistry.AudioLogs != null)
                Hecton8.Core.GlobalRegistry.AudioLogs.PlayLog(linkedAudioLog);

            TryGrantRewards(interactor);
            EmergencyServiceRelayEvents.RaiseRelayActivated(this, firstActivation);
            RebuildInteractText();
        }

        /// <inheritdoc />
        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        /// <summary>Builds the first guidance line used before the player has opened any relay in the chain.</summary>
        public string BuildInitialRouteMessage()
        {
            return FallbackOrLocalized(initialRouteMessage, LocalizationKeys.RELAY_ROUTE_INITIAL, DefaultInitialRouteMessage);
        }

        /// <summary>Builds the reminder line used while the player is still following the relay chain.</summary>
        public string BuildBreadcrumbMessage()
        {
            return FallbackOrLocalized(breadcrumbMessage, LocalizationKeys.RELAY_ROUTE_BREADCRUMB, DefaultBreadcrumbMessage);
        }

        /// <summary>Builds the relay handoff that points to the next authored breadcrumb.</summary>
        public string BuildDownloadedRouteMessage(EmergencyServiceRelay resolvedNextRelay)
        {
            if (resolvedNextRelay == null)
            {
                return ResolveLocalized(
                    LocalizationKeys.RELAY_ROUTE_TERMINUS,
                    "SERVICE RELAY: local route ends here. LOOK FOR THE BIGGER TRACE, THE RUINS, AND AN INTACT MODULE.");
            }

            AbsoluteUniversePosition currentAup = RelayAup;
            AbsoluteUniversePosition nextAup = resolvedNextRelay.RelayAup;
            bool deeper = ResolveVerticalDeltaMeters(in currentAup, in nextAup) < 0d;
            return deeper
                ? DownloadedBelowFallback
                : DownloadedFartherFallback;
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

            _cachedRelayAup = AbsoluteUniversePosition.FromRuntimePosition(relayTransform.position);
            _hasCachedRelayAup = true;
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

            string descriptorNote = FallbackOrLocalized(fieldOperatorNote, LocalizationKeys.RELAY_DESCRIPTOR_NOTE, DefaultDescriptorNote);
            _descriptor.Configure(fieldRole, descriptorNote);
        }

        private void RebuildInteractText()
        {
            string verb = IsDiscovered
                ? FallbackOrLocalized(reviewVerb, LocalizationKeys.RELAY_INTERACT_REVIEW, DefaultReviewVerb)
                : FallbackOrLocalized(interactVerb, LocalizationKeys.RELAY_INTERACT_OPEN, DefaultInteractVerb);
            _cachedInteractText = verb + " " + RelayLabel;
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
                NotificationEvents.PushInfo(BuildRewardGrantedMessage());

            if (inventoryFull)
                NotificationEvents.PushWarning(
                    ResolveLocalized(
                        LocalizationKeys.RELAY_REWARD_INVENTORY_FULL,
                        RewardInventoryFullFallback));
        }

        private string BuildRewardGrantedMessage()
        {
            for (int i = 0; i < rewards.Length; i++)
            {
                ItemData item = rewards[i].item;
                int quantity = math.max(0, rewards[i].quantity);
                if (item == null || quantity <= 0)
                    continue;

                return RewardGrantedFallback;
            }

            return ResolveLocalized(LocalizationKeys.RELAY_REWARD_EMPTY, RewardEmptyFallback);
        }

        private static bool TryResolveInventory(Transform interactor, out PlayerInventory inventory)
        {
            inventory = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;

            if (interactor != null)
                inventory = interactor.GetComponent<PlayerInventory>();

            if (inventory != null)
                return true;

            if (playerContext != null && playerContext.Inventory != null)
            {
                inventory = playerContext.Inventory;
                return true;
            }

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return false;

            inventory = playerTransform.GetComponent<PlayerInventory>();
            return inventory != null;
        }

        private static string FallbackOrLocalized(string value, string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ResolveLocalized(key, fallback)
                : value;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
            ApplyDescriptorSemantics();
            RefreshCachedRuntimeIdentity();
            RebuildInteractText();

            if (Application.isPlaying && isActiveAndEnabled)
                MarkRegistryDirty();
        }
#endif
    }
}
