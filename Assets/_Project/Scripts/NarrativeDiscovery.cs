// ============================================================================
// HECTON-8 - NarrativeDiscovery.cs
// Interaction component for lore objects, black boxes, PDAs, and wreckage.
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Modding;
using Hecton8.Narrative;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    public sealed class NarrativeDiscovery : MonoBehaviour, IInteractable, ILocalizationLanguageChangedListener
    {
        private const string DefaultStudyVerbRu = "Izuchit";
        private const string DefaultStudyVerbEn = "Study";
        private const string DefaultPlaybackVerbRu = "Vosproizvesti zapis";
        private const string DefaultPlaybackVerbEn = "Play Log";
        private const string DefaultTextVerbRu = "Otkryt zapis";
        private const string DefaultTextVerbEn = "Open Log";
        private const string DefaultArchiveVerbRu = "Otkryt arhiv";
        private const string DefaultArchiveVerbEn = "Open Archive";

        [Header("Discovery")]
        [Tooltip("Unique discovery ID for saves and triggers.")]
        [SerializeField] private string discoveryId;

        [Tooltip("Interaction prompt text, such as 'Take PDA' or 'Study black box'.")]
        [SerializeField] private string interactVerb = DefaultStudyVerbRu;

        [Tooltip("Object name used in the discovery log.")]
        [SerializeField] private string displayName = "Obekt";
        [SerializeField] private LocalizedTextReference localizedDisplayName;

        [Header("Audio Log")]
        [Tooltip("Optional audio log played on interaction.")]
        [SerializeField] private AudioLogData linkedAudioLog;

        [Header("Settings")]
        [Tooltip("If enabled, the narrative director fires this discovery when the player AUP enters the configured radius.")]
        [SerializeField] private bool triggerWhenAupWithinRadius;

        [Tooltip("Single bit index in the director's ulong AUP trigger mask.")]
        [SerializeField, Range(0, 63)] private int aupTriggerBitIndex;

        [Tooltip("AUP distance radius in meters for automatic narrative discovery.")]
        [SerializeField, Min(0.1f)] private float aupTriggerRadiusMeters = 50f;

        [Tooltip("Disables this object after direct interaction discovery.")]
        [SerializeField] private bool disableAfterDiscovery = true;

        [Tooltip("Optional highlight object shown while hovered.")]
        [SerializeField] private GameObject highlightObject;

        private string _cachedInteractText;
        private AbsoluteUniversePosition _cachedAup;
        private uint _cachedDiscoveryHash;
        private bool _registeredLifecycle;
        private static int _activeDiscoveryCount;

        public string DiscoveryId => discoveryId;
        public uint DiscoveryHash => _cachedDiscoveryHash;
        public AbsoluteUniversePosition CachedAup => _cachedAup;
        public bool HasValidDiscoveryId => !string.IsNullOrWhiteSpace(discoveryId);
        internal static int ActiveDiscoveryCount => _activeDiscoveryCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDiscoveryRegistry()
        {
            _activeDiscoveryCount = 0;
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildCache();
            RefreshAupTriggerCache();

            NarrativeEvents.RaiseNarrativePOIRegistered(this);
            _registeredLifecycle = true;
            _activeDiscoveryCount++;

            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (disableAfterDiscovery && narrativeDirector != null &&
                narrativeDirector.HasDiscovery(discoveryId))
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);

            if (_registeredLifecycle)
            {
                NarrativeEvents.RaiseNarrativePOIDisposed(this);
                _registeredLifecycle = false;
                if (_activeDiscoveryCount > 0)
                    _activeDiscoveryCount--;
            }

            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        private void RebuildCache()
        {
            _cachedInteractText = ResolveInteractVerb() + " " + ResolveDisplayName();
        }

        private string ResolveInteractVerb()
        {
            if (HasCustomInteractVerb())
                return interactVerb;

            if (linkedAudioLog == null)
                return ResolveLocalized(LocalizationKeys.INTERACT_STUDY, DefaultStudyVerbEn);

            if (linkedAudioLog.IsTextOnlyPlayback)
                return ResolveLocalized(LocalizationKeys.INTERACT_OPEN_LOG, DefaultTextVerbEn);

            if (!linkedAudioLog.HasPlaybackPayload && linkedAudioLog.HasVisibleContent)
                return ResolveLocalized(LocalizationKeys.INTERACT_OPEN_ARCHIVE, DefaultArchiveVerbEn);

            return ResolveLocalized(LocalizationKeys.INTERACT_PLAY_LOG, DefaultPlaybackVerbEn);
        }

        public void OnHoverStart()
        {
            if (highlightObject != null)
                highlightObject.SetActive(true);
        }

        public void OnHoverEnd()
        {
            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        public void Interact(Transform interactor)
        {
            if (!HasValidDiscoveryId)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[Narrative] '{name}' has no discoveryId. Interaction ignored.");
#endif
                return;
            }

            HectonNarrativeDirector narrativeDirector = GlobalRegistry.NarrativeDirector;
            if (narrativeDirector != null && narrativeDirector.HasDiscovery(discoveryId))
            {
                if (linkedAudioLog != null && Hecton8.Core.GlobalRegistry.AudioLogs != null)
                    Hecton8.Core.GlobalRegistry.AudioLogs.PlayLog(linkedAudioLog);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Narrative] '{discoveryId}' already discovered.");
#endif
                return;
            }

            NarrativeEvents.RaiseDiscoveryMade(discoveryId);
            LoreDatabaseManager loreDatabase = Hecton8.Core.GlobalRegistry.LoreDatabase;
            if (loreDatabase != null)
                loreDatabase.TryUnlockByHash(LoreDatabaseManager.ComputeLoreHash(discoveryId));

            if (linkedAudioLog != null && Hecton8.Core.GlobalRegistry.AudioLogs != null)
                Hecton8.Core.GlobalRegistry.AudioLogs.PlayLog(linkedAudioLog);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery made: {discoveryId} ({ResolveDisplayName()})");
#endif

            if (disableAfterDiscovery)
                gameObject.SetActive(false);
        }

        public string GetInteractText() => _cachedInteractText;

        internal bool TryGetAupTrigger(
            out int bitIndex,
            out float radiusMeters,
            out AbsoluteUniversePosition aup,
            out uint discoveryHash)
        {
            bitIndex = aupTriggerBitIndex;
            radiusMeters = aupTriggerRadiusMeters;
            aup = _cachedAup;
            discoveryHash = _cachedDiscoveryHash;
            return triggerWhenAupWithinRadius &&
                   HasValidDiscoveryId &&
                   discoveryHash != 0u &&
                   (uint)bitIndex < 64u &&
                   radiusMeters > 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(discoveryId))
                discoveryId = gameObject.name.ToLower().Replace(" ", "_");

            if (aupTriggerRadiusMeters <= 0f)
                aupTriggerRadiusMeters = 50f;

            RebuildCache();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildCache();
        }

        private string ResolveDisplayName()
        {
            return localizedDisplayName.ResolveOrFallback(FallbackOrDefault(displayName, "Object"));
        }

        private bool HasCustomInteractVerb()
        {
            if (string.IsNullOrWhiteSpace(interactVerb))
                return false;

            return !IsLegacyDefaultVerb(interactVerb);
        }

        private static bool IsLegacyDefaultVerb(string value)
        {
            return string.Equals(value, DefaultStudyVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultStudyVerbEn, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultPlaybackVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultPlaybackVerbEn, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultTextVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultTextVerbEn, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultArchiveVerbRu, System.StringComparison.Ordinal) ||
                   string.Equals(value, DefaultArchiveVerbEn, System.StringComparison.Ordinal);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        internal void ConfigureRecoveryPlacement(
            string id,
            string fallbackDisplayName,
            AudioLogData logData,
            bool disableAfterUse)
        {
            discoveryId = id;
            displayName = fallbackDisplayName;
            localizedDisplayName = default;
            interactVerb = string.Empty;
            linkedAudioLog = logData;
            disableAfterDiscovery = disableAfterUse;
            highlightObject = null;
            RebuildCache();
            RefreshAupTriggerCache();
        }

        private void RefreshAupTriggerCache()
        {
            _cachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
            _cachedAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
        }
    }
}
