// ============================================================================
// HECTON-8 - AudioLogPickup.cs
// Interactive colony audio-log pickup.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Interaction;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Narrative
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AudioLogPickup : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001AudioLogPickupSignalPushDropCount;
        private const uint WfcOutpostDatapadSourceHash = 0x57464341u; // WFCA
        private const byte WfcDatapadLootedFlag = (byte)WfcOutpostCellStateFlags.DatapadLooted;
        private const int MaxRegisteredPickupTemplates = 64;
        private const string DefaultPlaybackVerbRu = "Vosproizvesti zapis";
        private const string DefaultPlaybackVerbEn = "Play Log";
        private const string DefaultTextVerbRu = "Otkryt zapis";
        private const string DefaultTextVerbEn = "Open Log";
        private const string DefaultArchiveVerbRu = "Otkryt arhiv";
        private const string DefaultArchiveVerbEn = "Open Archive";
        private const string DefaultReplaySuffix = "(Replay)";
        private const int InteractTextCapacity = 96;
        private static readonly uint _defaultPlaybackVerbRuHash = QuestFlagHashKernel.ComputeStableHash(DefaultPlaybackVerbRu);
        private static readonly uint _defaultPlaybackVerbEnHash = QuestFlagHashKernel.ComputeStableHash(DefaultPlaybackVerbEn);
        private static readonly uint _defaultTextVerbRuHash = QuestFlagHashKernel.ComputeStableHash(DefaultTextVerbRu);
        private static readonly uint _defaultTextVerbEnHash = QuestFlagHashKernel.ComputeStableHash(DefaultTextVerbEn);
        private static readonly uint _defaultArchiveVerbRuHash = QuestFlagHashKernel.ComputeStableHash(DefaultArchiveVerbRu);
        private static readonly uint _defaultArchiveVerbEnHash = QuestFlagHashKernel.ComputeStableHash(DefaultArchiveVerbEn);

        // COLD ALLOC: RegistryBucket<AudioLogPickup>[64] - active pickup templates for procedural lore lookup - owner: AudioLogPickup
        private static readonly RegistryBucket<AudioLogPickup> _registeredPickupTemplates = new RegistryBucket<AudioLogPickup>(MaxRegisteredPickupTemplates);

        [Header("Audio Log")]
        [Tooltip("Audio log data.")]
        [SerializeField] private AudioLogData logData;

        [Tooltip("Interaction prompt text.")]
        [SerializeField] private string interactVerb = DefaultPlaybackVerbRu;

        [Header("Behaviour")]
        [Tooltip("Deactivate this object after the first interaction.")]
        [SerializeField] private bool deactivateAfterPickup;

        [Tooltip("Hover highlight object.")]
        [SerializeField] private GameObject highlightObject;

        private string _legacyInteractText = DefaultPlaybackVerbEn;
        private readonly char[] _cachedInteractText = new char[InteractTextCapacity];
        private int _cachedInteractTextLength;
        private bool _alreadyDiscovered;
        private bool _pickupTemplateRegistered;
        private ulong _wfcOutpostSectorHash;
        private ushort _wfcOutpostCellIndex;
        private byte _wfcOutpostFlags;
        private bool _wfcOutpostPersistenceConfigured;
        private bool _hotSwapListenerRegistered;
        private IAudioLogRuntime _cachedAudioLogSystem;
        private ILocalizationTextReadModel _cachedLocalization;

        internal static int RegisteredPickupTemplateCount => _registeredPickupTemplates.Count;

        public void ConfigureWfcOutpostPersistence(ulong sectorHash, ushort cellIndex, byte initialFlags)
        {
            if (sectorHash == 0UL || cellIndex >= WfcOutpostPersistenceConstants.CellCount)
            {
                ClearWfcOutpostPersistence();
                RestoreWfcOutpostDatapadBaselineState(true);
                return;
            }

            _wfcOutpostSectorHash = sectorHash;
            _wfcOutpostCellIndex = cellIndex;
            _wfcOutpostFlags = (byte)(initialFlags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostPersistenceConfigured = true;

            if ((_wfcOutpostFlags & WfcDatapadLootedFlag) != 0)
            {
                ApplyWfcOutpostDatapadLootedState();
                return;
            }

            RestoreWfcOutpostDatapadBaselineState(true);
        }

        public void ClearWfcOutpostPersistence()
        {
            _wfcOutpostPersistenceConfigured = false;
            _wfcOutpostSectorHash = 0UL;
            _wfcOutpostCellIndex = 0;
            _wfcOutpostFlags = 0;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterPickupTemplate();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            _alreadyDiscovered = false;

            if (_wfcOutpostPersistenceConfigured)
            {
                if ((_wfcOutpostFlags & WfcDatapadLootedFlag) != 0)
                    ApplyWfcOutpostDatapadLootedState();
                else
                    RestoreWfcOutpostDatapadBaselineState(false);

                return;
            }

            IAudioLogRuntime audioLogSystem = _cachedAudioLogSystem;
            if (logData != null && audioLogSystem != null)
            {
                _alreadyDiscovered = audioLogSystem.IsAudioLogDiscovered(logData.logId);

                if (_alreadyDiscovered && deactivateAfterPickup)
                {
                    BuildCache();
                    gameObject.SetActive(false);
                    return;
                }
            }

            BuildCache();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            ClearWfcOutpostPersistence();
            TryUnregisterPickupTemplate();
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);

            if (highlightObject != null)
                highlightObject.SetActive(false);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterPickupTemplate();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _cachedAudioLogSystem = currentService as IAudioLogRuntime;
                    RefreshDiscoveryStateFromAudioLogSystem();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationTextReadModel;
                    BuildCache();
                    break;
            }
        }

        private void BuildCache()
        {
            if (logData == null)
            {
                CacheInteractText(ResolveInteractVerbSpan(out string legacyVerb), legacyVerb);
                return;
            }

            if (_alreadyDiscovered)
            {
                CacheInteractText(
                    ResolveLocalizedSpan(LocalizationKeys.INTERACT_REPLAY_SUFFIX, DefaultReplaySuffix),
                    DefaultReplaySuffix);
                return;
            }

            CacheInteractText(ResolveInteractVerbSpan(out string resolvedLegacyVerb), resolvedLegacyVerb);
        }

        private ReadOnlySpan<char> ResolveInteractVerbSpan(out string legacyVerb)
        {
            if (HasCustomInteractVerb())
            {
                legacyVerb = interactVerb;
                return interactVerb.AsSpan();
            }

            if (logData == null)
            {
                legacyVerb = DefaultPlaybackVerbEn;
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_PLAY_LOG, DefaultPlaybackVerbEn);
            }

            if (logData.IsTextOnlyPlayback)
            {
                legacyVerb = DefaultTextVerbEn;
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_OPEN_LOG, DefaultTextVerbEn);
            }

            if (!logData.HasPlaybackPayload && logData.HasVisibleContent)
            {
                legacyVerb = DefaultArchiveVerbEn;
                return ResolveLocalizedSpan(LocalizationKeys.INTERACT_OPEN_ARCHIVE, DefaultArchiveVerbEn);
            }

            legacyVerb = DefaultPlaybackVerbEn;
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
            if (logData == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] No AudioLogData assigned.");
#endif
                return;
            }

            IAudioLogRuntime system = _cachedAudioLogSystem;
            if (system == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] AudioLogSystem service is not cached.");
#endif
                return;
            }

            bool wasDiscovered = _alreadyDiscovered;
            system.TryPlayAudioLog(logData.logId);
            _alreadyDiscovered = true;
            BuildCache();
            if (!wasDiscovered)
                SetWfcOutpostFlags((byte)(_wfcOutpostFlags | WfcDatapadLootedFlag), (uint)SystemDispatcher.CurrentFrameIndex);

            if (deactivateAfterPickup)
                gameObject.SetActive(false);
        }

        public string GetInteractText() => _legacyInteractText;

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            length = _cachedInteractTextLength;
            if (length <= 0 || destination.Length < length)
            {
                length = 0;
                return InteractableTextCopy.TryCopy(_legacyInteractText, destination, out length);
            }

            _cachedInteractText.AsSpan(0, length).CopyTo(destination);
            return true;
        }

        private void ApplyWfcOutpostDatapadLootedState()
        {
            _alreadyDiscovered = true;
            BuildCache();

            if (deactivateAfterPickup)
                gameObject.SetActive(false);
        }

        private void RestoreWfcOutpostDatapadBaselineState(bool allowReactivate)
        {
            _alreadyDiscovered = false;
            BuildCache();

            if (allowReactivate && deactivateAfterPickup && !gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        private void SetWfcOutpostFlags(byte flags, uint frame)
        {
            byte previous = _wfcOutpostFlags;
            byte current = (byte)(flags & WfcOutpostPersistenceConstants.MutableFlagMask);
            _wfcOutpostFlags = current;
            PublishWfcOutpostFlags(previous, current, frame);
        }

        private void PublishWfcOutpostFlags(byte previous, byte current, uint frame)
        {
            if (!_wfcOutpostPersistenceConfigured)
                return;

            previous = (byte)(previous & WfcOutpostPersistenceConstants.MutableFlagMask);
            current = (byte)(current & WfcOutpostPersistenceConstants.MutableFlagMask);
            if (previous == current)
                return;

            WfcOutpostStateChangedSignal signal = new WfcOutpostStateChangedSignal
            {
                SectorHash = _wfcOutpostSectorHash,
                CellIndex = _wfcOutpostCellIndex,
                PreviousFlags = previous,
                CurrentFlags = current,
                Frame = frame,
                SourceHash = WfcOutpostDatapadSourceHash,
                Flags = 0
            };
            SignalBus<WfcOutpostStateChangedSignal>.TryPushTracked(in signal, ref s_x001AudioLogPickupSignalPushDropCount);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(interactVerb))
                interactVerb = DefaultPlaybackVerbRu;

            BuildCache();
        }
#endif

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged();

        }


        private void HandleLanguageChanged()
        {
            BuildCache();
        }

        private bool HasCustomInteractVerb()
        {
            if (string.IsNullOrWhiteSpace(interactVerb))
                return false;

            return !IsLegacyDefaultVerb(interactVerb);
        }

        private static bool IsLegacyDefaultVerb(string value)
        {
            uint verbHash = QuestFlagHashKernel.ComputeStableHash(value);
            return verbHash == _defaultPlaybackVerbRuHash ||
                   verbHash == _defaultPlaybackVerbEnHash ||
                   verbHash == _defaultTextVerbRuHash ||
                   verbHash == _defaultTextVerbEnHash ||
                   verbHash == _defaultArchiveVerbRuHash ||
                   verbHash == _defaultArchiveVerbEnHash;
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private void CacheInteractText(ReadOnlySpan<char> text, string legacyText)
        {
            _legacyInteractText = string.IsNullOrEmpty(legacyText) ? DefaultPlaybackVerbEn : legacyText;
            _cachedInteractTextLength = 0;

            if (text.IsEmpty || text.Length > _cachedInteractText.Length)
                return;

            text.CopyTo(_cachedInteractText);
            _cachedInteractTextLength = text.Length;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedAudioLogSystem = Hecton8.Core.GlobalRegistry.AudioLogRuntime;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
        }

        private void RefreshDiscoveryStateFromAudioLogSystem()
        {
            if (_wfcOutpostPersistenceConfigured || logData == null)
                return;

            IAudioLogRuntime audioLogSystem = _cachedAudioLogSystem;
            _alreadyDiscovered = audioLogSystem != null && audioLogSystem.IsAudioLogDiscovered(logData.logId);
            BuildCache();
            if (_alreadyDiscovered && deactivateAfterPickup && isActiveAndEnabled)
                gameObject.SetActive(false);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        internal void ConfigureRecoveryPickup(AudioLogData data, bool deactivateAfterUse)
        {
            logData = data;
            interactVerb = string.Empty;
            deactivateAfterPickup = deactivateAfterUse;
            highlightObject = null;
            _alreadyDiscovered = false;
            BuildCache();
        }

        /// <summary>
        /// Resolves the first active audio-log pickup template without scene search.
        /// </summary>
        /// <param name="template">Resolved pickup template.</param>
        /// <returns>True when an active template is registered.</returns>
        internal static bool TryGetRegisteredTemplate(out AudioLogPickup template)
        {
            AudioLogPickup[] rawArray = _registeredPickupTemplates.RawArray;
            int registeredCount = _registeredPickupTemplates.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                AudioLogPickup pickup = rawArray[i];
                if (pickup == null || !pickup.isActiveAndEnabled || pickup.gameObject == null)
                    continue;

                template = pickup;
                return true;
            }

            template = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPickupTemplateRegistry()
        {
            _registeredPickupTemplates.Clear();
        }

        private void TryRegisterPickupTemplate()
        {
            if (_pickupTemplateRegistered)
                return;

            _pickupTemplateRegistered = _registeredPickupTemplates.TryRegister(this);
        }

        private void TryUnregisterPickupTemplate()
        {
            if (!_pickupTemplateRegistered)
                return;

            _registeredPickupTemplates.Unregister(this);
            _pickupTemplateRegistered = false;
        }
    }
}
