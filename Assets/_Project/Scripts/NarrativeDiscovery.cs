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
using System;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    public sealed class NarrativeDiscovery : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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

        [Header("Spatial Trigger Coupling")]
        [Tooltip("Optional quest id that must be active before this POI publishes a HUD breadcrumb.")]
        [SerializeField] private string activeQuestId;

        [Tooltip("Optional biome id emitted as a hash when this POI is triggered.")]
        [SerializeField] private string biomeSignalId;

        [Tooltip("Optional soundscape profile id emitted as a hash when this POI is triggered.")]
        [SerializeField] private string soundscapeProfileId;

        [Tooltip("When enabled, this POI can push a diegetic waypoint while its quest is active.")]
        [SerializeField] private bool publishHudBreadcrumb;

        [Tooltip("Disables this object after direct interaction discovery.")]
        [SerializeField] private bool disableAfterDiscovery = true;

        [Tooltip("Optional highlight object shown while hovered.")]
        [SerializeField] private GameObject highlightObject;

        private const int InteractTextCapacity = 128;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextCapacity];
        private int _cachedInteractTextLength;
        private AbsoluteUniversePosition _cachedAup;
        private double _cachedAupTriggerRadiusSq;
        private uint _cachedDiscoveryHash;
        private uint _cachedQuestHash;
        private uint _cachedBiomeHash;
        private uint _cachedSoundscapeHash;
        private NarrativeSpatialTriggerFlags _cachedSpatialFlags;
        private bool _registeredLifecycle;
        private bool _hotSwapRegistered;
        private INarrativeDiscoveryReadModel _narrativeDiscoveryReadModel;
        private IAudioLogRuntime _audioLogs;
        private ILoreUnlockSink _loreUnlockSink;
        private ILocalizationTextReadModel _localization;
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
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildCache();
            RefreshAupTriggerCache();

            NarrativeEvents.TryNotifyNarrativePOIRegistered(this);
            _registeredLifecycle = true;
            _activeDiscoveryCount++;

            INarrativeDiscoveryReadModel narrativeDiscovery = _narrativeDiscoveryReadModel;
            if (disableAfterDiscovery &&
                narrativeDiscovery != null &&
                _cachedDiscoveryHash != 0u &&
                narrativeDiscovery.HasDiscovery(_cachedDiscoveryHash))
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);

            if (_registeredLifecycle)
            {
                NarrativeEvents.TryNotifyNarrativePOIDisposed(this);
                _registeredLifecycle = false;
                if (_activeDiscoveryCount > 0)
                    _activeDiscoveryCount--;
            }

            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
        }

        private void RebuildCache()
        {
            _cachedInteractTextLength = 0;
            AppendInteractText(ResolveInteractVerbSpan());
            if (_cachedInteractTextLength > 0)
                AppendInteractText(" ".AsSpan());
            AppendInteractText(ResolveDisplayNameSpan());

            if (_cachedInteractTextLength == 0)
                AppendInteractText(DefaultStudyVerbEn.AsSpan());
        }

        private void AppendInteractText(ReadOnlySpan<char> value)
        {
            int remaining = _cachedInteractTextBuffer.Length - _cachedInteractTextLength;
            if (remaining <= 0 || value.Length == 0)
                return;

            int copyLength = value.Length <= remaining ? value.Length : remaining;
            value.Slice(0, copyLength).CopyTo(_cachedInteractTextBuffer.AsSpan(_cachedInteractTextLength));
            _cachedInteractTextLength += copyLength;
        }

        private string ResolveInteractVerb()
        {
            if (HasCustomInteractVerb())
                return interactVerb;

            if (linkedAudioLog == null)
                return DefaultStudyVerbEn;

            if (linkedAudioLog.IsTextOnlyPlayback)
                return DefaultTextVerbEn;

            if (!linkedAudioLog.HasPlaybackPayload && linkedAudioLog.HasVisibleContent)
                return DefaultArchiveVerbEn;

            return DefaultPlaybackVerbEn;
        }

        private ReadOnlySpan<char> ResolveInteractVerbSpan()
        {
            if (HasCustomInteractVerb())
                return interactVerb.AsSpan();

            if (linkedAudioLog == null)
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_STUDY, DefaultStudyVerbEn);

            if (linkedAudioLog.IsTextOnlyPlayback)
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_OPEN_LOG, DefaultTextVerbEn);

            if (!linkedAudioLog.HasPlaybackPayload && linkedAudioLog.HasVisibleContent)
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_OPEN_ARCHIVE, DefaultArchiveVerbEn);

            return ResolveLocalizedSpan(LocalizationKeys.INTERACT_PLAY_LOG, DefaultPlaybackVerbEn);
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
                Hecton8.Core.H8Debug.LogWarning("[Narrative] Missing discoveryId. Interaction ignored.");
#endif
                return;
            }

            INarrativeDiscoveryReadModel narrativeDiscovery = _narrativeDiscoveryReadModel;
            if (narrativeDiscovery != null &&
                _cachedDiscoveryHash != 0u &&
                narrativeDiscovery.HasDiscovery(_cachedDiscoveryHash))
            {
                if (linkedAudioLog != null && _audioLogs != null)
                    _audioLogs.TryPlayAudioLog(linkedAudioLog.logId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log("[Narrative] Discovery already registered.");
#endif
                return;
            }

            NarrativeEvents.TryRaiseDiscoveryMade(_cachedDiscoveryHash);
            ILoreUnlockSink loreUnlockSink = _loreUnlockSink;
            if (loreUnlockSink != null)
                loreUnlockSink.TryUnlockByHash(LocHash.ComputeAscii(discoveryId));

            if (linkedAudioLog != null && _audioLogs != null)
                _audioLogs.TryPlayAudioLog(linkedAudioLog.logId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Narrative] Discovery made.");
#endif

            if (disableAfterDiscovery)
                gameObject.SetActive(false);
        }

        public string GetInteractText() => ResolveInteractVerb();

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(
                _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength),
                destination,
                out length);
        }

        internal bool TryGetAupTrigger(
            out int bitIndex,
            out double radiusSq,
            out AbsoluteUniversePosition aup,
            out uint discoveryHash)
        {
            bitIndex = aupTriggerBitIndex;
            radiusSq = _cachedAupTriggerRadiusSq;
            aup = _cachedAup;
            discoveryHash = _cachedDiscoveryHash;
            return triggerWhenAupWithinRadius &&
                   HasValidDiscoveryId &&
                   discoveryHash != 0u &&
                   (uint)bitIndex < 64u &&
                   radiusSq > 0d;
        }

        internal bool TryGetSpatialTrigger(out NarrativeSpatialTriggerAuthoring trigger)
        {
            if (!triggerWhenAupWithinRadius ||
                !HasValidDiscoveryId ||
                _cachedDiscoveryHash == 0u ||
                (uint)aupTriggerBitIndex >= 64u ||
                _cachedAupTriggerRadiusSq <= 0d)
            {
                trigger = default;
                return false;
            }

            trigger = new NarrativeSpatialTriggerAuthoring
            {
                PositionAup = _cachedAup,
                RadiusMeters = aupTriggerRadiusMeters,
                RadiusSq = (float)_cachedAupTriggerRadiusSq,
                PoiHash = _cachedDiscoveryHash,
                QuestHash = _cachedQuestHash,
                BiomeHash = _cachedBiomeHash,
                SoundscapeHash = _cachedSoundscapeHash,
                LoreHash = LocHash.ComputeAscii(discoveryId),
                BitIndex = aupTriggerBitIndex,
                Flags = _cachedSpatialFlags
            };
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(discoveryId))
                discoveryId = gameObject.name.ToLower().Replace(" ", "_");

            if (aupTriggerRadiusMeters <= 0f)
                aupTriggerRadiusMeters = 50f;

            RebuildCache();
            RefreshAupTriggerCache();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged();

        }


        private void HandleLanguageChanged()
        {
            RebuildCache();
        }

        private string ResolveDisplayName()
        {
            return localizedDisplayName.ResolveOrFallback(_localization, FallbackOrDefault(displayName, "Object"));
        }

        private ReadOnlySpan<char> ResolveDisplayNameSpan()
        {
            return localizedDisplayName.ResolveSpanOrFallback(_localization, FallbackOrDefault(displayName, "Object"));
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.NarrativeDirectorRuntime:
                    _narrativeDiscoveryReadModel = currentService as INarrativeDiscoveryReadModel;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _audioLogs = currentService as IAudioLogRuntime;
                    break;
                case GlobalRegistryServiceSlot.LoreDatabaseRuntime:
                    _loreUnlockSink = currentService as ILoreUnlockSink;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    RebuildCache();
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _narrativeDiscoveryReadModel = GlobalRegistry.NarrativeDiscoveryReadModel;
            _audioLogs = GlobalRegistry.AudioLogRuntime;
            _loreUnlockSink = GlobalRegistry.LoreUnlockSink;
            _localization = GlobalRegistry.LocalizationText;
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

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localization;
            return manager != null && !string.IsNullOrEmpty(key)
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback.AsSpan())
                : fallback.AsSpan();
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
            float safeRadiusMeters = aupTriggerRadiusMeters > 0f ? aupTriggerRadiusMeters : 0f;
            _cachedAupTriggerRadiusSq = (double)safeRadiusMeters * safeRadiusMeters;
            if (!TryResolveAupFromRuntimeOrigin(transform.position, out _cachedAup))
                _cachedAup = default;

            _cachedQuestHash = ComputeQuestHash(activeQuestId);
            _cachedBiomeHash = ComputeStableHash(biomeSignalId);
            _cachedSoundscapeHash = ComputeStableHash(soundscapeProfileId);
            _cachedSpatialFlags = publishHudBreadcrumb
                ? NarrativeSpatialTriggerFlags.HudBreadcrumb
                : NarrativeSpatialTriggerFlags.None;
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new Unity.Mathematics.double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static uint ComputeQuestHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0u;

            unchecked
            {
                uint hash = LocHash.FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    ushort current = value[i];
                    hash ^= (byte)current;
                    hash *= LocHash.FnvPrime;
                    hash ^= (byte)(current >> 8);
                    hash *= LocHash.FnvPrime;
                }

                return hash;
            }
        }

        private static uint ComputeStableHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }
    }
}
