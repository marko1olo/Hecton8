// ============================================================================
// HECTON-8 — EndingTerminalInteractable.cs
// Interaktivnyy terminal yadra Atlas-6 — tochka vybora kontsovki.
//
// LOR: Terminal ryadom s yadrom na -5000m.
// Na terminale: polnye dannye programmy Poseva, prichina "polomki" Atlas-6,
// i — glavnoe — chto on stroil 847 dney.
//
// ARHITEKTURA:
//   • IInteractable — vzaimodeystvie otkryvaet UI vybora kontsovki.
//   • Aktiven tolko esli EndingSystem.IsConditionMet.
//   • Pokazyvaet tri varianta cherez NotificationEvents (vremenno).
//   • V finalnoy versii — otdelnyy UI ekran.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EndingTerminalInteractable : MonoBehaviour, IInteractable, IInteractableTextProvider, IEndingEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Visual -----------------------------------")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private GameObject activeIndicator;

        [Header("Quest DAG Gate")]
        [SerializeField] private string[] requiredAtlasKeyQuestIds =
        {
            "quest_atlas_signal_detected",
            "quest_atlas_signal_decoded",
            "quest_atlas_core_reached"
        };

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private bool _choiceOpen;
        private readonly char[] _inactiveTextBuffer = new char[96];
        private readonly char[] _activeTextBuffer = new char[96];
        private readonly char[] _completeTextBuffer = new char[96];
        private readonly char[] _dataLoadedTextBuffer = new char[256];
        private int _inactiveTextLength;
        private int _activeTextLength;
        private int _completeTextLength;
        private int _dataLoadedTextLength;
        private EndingSystem _cachedEnding;
        private IQuestSystem _cachedQuest;
        private ILocalizationTextReadModel _cachedLocalization;
        private bool _hotSwapListenerRegistered;

        // Pre-cached interact texts — zero GC
        private const string TextInactive = "ATLAS-6 TERMINAL UNAVAILABLE";
        private const string TextActive = "INTERACT WITH ATLAS-6 CORE";
        private const string TextComplete = "DECISION RECORDED";
        private static readonly uint s_terminalInactiveDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash("atlas6_terminal_inactive");
        private static readonly uint s_atlasCoreDataAccessedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash("atlas6_core_data_accessed");

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            EndingEvents.Register(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
            UpdateActiveIndicator();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            SetObjectActive(highlightObject, false);
            _choiceOpen = false;
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
            EndingEvents.Unregister(this);
        }

        // ----------------------------------------------------------
        //  IInteractable
        // ----------------------------------------------------------

        public void OnHoverStart()
        {
            SetObjectActive(highlightObject, true);
        }

        public void OnHoverEnd()
        {
            SetObjectActive(highlightObject, false);
        }

        public void Interact(Transform interactor)
        {
            EndingSystem ending = _cachedEnding;
            if (ending == null) return;

            if (ending.IsEndingComplete)
            {
                LogEndingAlreadyComplete();
                return;
            }

            if (!HasAllAtlasKeys())
            {
                NarrativeEvents.TryRaiseDiscoveryMade(s_terminalInactiveDiscoveryHash);
                return;
            }

            if (!ending.IsConditionMet)
                ending.ForceConditionMetFromQuestDAG();

            if (_choiceOpen) return;

            OpenChoiceUI();
        }

        public string GetInteractText()
        {
            EndingSystem ending = _cachedEnding;
            if (ending == null) return TextInactive;
            if (ending.IsEndingComplete) return TextComplete;
            if (!HasAllAtlasKeys()) return TextInactive;
            return TextActive;
        }

        private ReadOnlySpan<char> ResolveInteractTextSpan()
        {
            EndingSystem ending = _cachedEnding;
            if (ending == null) return _inactiveTextBuffer.AsSpan(0, _inactiveTextLength);
            if (ending.IsEndingComplete) return _completeTextBuffer.AsSpan(0, _completeTextLength);
            if (!HasAllAtlasKeys()) return _inactiveTextBuffer.AsSpan(0, _inactiveTextLength);
            return _activeTextBuffer.AsSpan(0, _activeTextLength);
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(ResolveInteractTextSpan(), destination, out length);
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private void OpenChoiceUI()
        {
            _choiceOpen = true;

            // Pokazyvaem dannye Atlas-6 cherez narrativ
            NarrativeEvents.TryRaiseDiscoveryMade(s_atlasCoreDataAccessedDiscoveryHash);

            // Publikuem tri varianta cherez HUD
            // V finalnoy versii — otdelnyy UI ekran s tremya knopkami
            // Seychas — uvedomleniya s instruktsiey
            Hecton8.UI.NotificationEvents.TryPushWarning(
                _dataLoadedTextBuffer.AsSpan(0, _dataLoadedTextLength));

            LogChoiceUiOpened();
        }

        public void ChooseStay()
        {
            SubmitTerminalChoice(EndingChoice.Leave);
        }

        public void ChooseLeave()
        {
            SubmitTerminalChoice(EndingChoice.ShutDown);
        }

        public void ChooseAmplify()
        {
            SubmitTerminalChoice(EndingChoice.Amplify);
        }

        public void OnEndingEvent(in EndingEventPayload payload)
        {
            switch ((EndingEventType)payload.EventType)
            {
                case EndingEventType.ConditionMet:
                    HandleConditionMet();
                    break;
                case EndingEventType.Chosen:
                    HandleEndingChosen((EndingChoice)payload.Choice);
                    break;
                case EndingEventType.SequenceComplete:
                    HandleEndingChosen((EndingChoice)payload.Choice);
                    break;
            }
        }

        private void SubmitTerminalChoice(EndingChoice choice)
        {
            if (!_choiceOpen || choice == EndingChoice.None)
                return;

            EndingSystem ending = _cachedEnding;
            if (ending == null || !ending.CanChooseEnding || !HasAllAtlasKeys())
                return;

            ending.ChooseEnding(choice);
        }

        /// <inheritdoc />
        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            RebuildLocalizedTextCache();
        }

        private void HandleConditionMet()
        {
            UpdateActiveIndicator();
        }

        private void HandleEndingChosen(EndingChoice choice)
        {
            _choiceOpen = false;
            UpdateActiveIndicator();
        }

        private void UpdateActiveIndicator()
        {
            if (activeIndicator == null) return;

            EndingSystem ending = _cachedEnding;
            bool active = ending != null && HasAllAtlasKeys() && !ending.IsEndingComplete;
            SetObjectActive(activeIndicator, active);
        }

        private bool HasAllAtlasKeys()
        {
            IQuestSystem questManager = _cachedQuest;
            if (questManager == null || requiredAtlasKeyQuestIds == null || requiredAtlasKeyQuestIds.Length == 0)
                return false;

            for (int i = 0; i < requiredAtlasKeyQuestIds.Length; i++)
            {
                string questId = requiredAtlasKeyQuestIds[i];
                if (string.IsNullOrWhiteSpace(questId))
                    continue;

                bool completed = questManager.IsCompleted(questId);
                bool finalCoreReachedGate = i == requiredAtlasKeyQuestIds.Length - 1;
                if (!completed && !(finalCoreReachedGate && questManager.IsActive(questId)))
                    return false;
            }

            return true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.EndingRuntime)
            {
                _cachedEnding = currentService as EndingSystem;
                UpdateActiveIndicator();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.QuestRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.QuestSystem)
            {
                _cachedQuest = currentService as IQuestSystem;
                UpdateActiveIndicator();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _cachedLocalization = currentService as ILocalizationTextReadModel;
                RebuildLocalizedTextCache();
            }
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

        private void CacheRegistryServicesCold()
        {
            _cachedEnding = GlobalRegistry.Ending;
            _cachedQuest = GlobalRegistry.QuestSystem;
            _cachedLocalization = GlobalRegistry.LocalizationText;
        }

        private void RebuildLocalizedTextCache()
        {
            CopyLocalizedSpanToBuffer(LocalizationKeys.ENDING_TERMINAL_INACTIVE, TextInactive, _inactiveTextBuffer, out _inactiveTextLength);
            CopyLocalizedSpanToBuffer(LocalizationKeys.ENDING_TERMINAL_ACTIVE, TextActive, _activeTextBuffer, out _activeTextLength);
            CopyLocalizedSpanToBuffer(LocalizationKeys.ENDING_TERMINAL_COMPLETE, TextComplete, _completeTextBuffer, out _completeTextLength);
            CopyLocalizedSpanToBuffer(
                LocalizationKeys.ENDING_TERMINAL_DATA_LOADED,
                "ATLAS-6: SEED PROGRAM DATA LOADED. LIFE ON HECTON-8 PRE-DATES HUMAN ARRIVAL. ATLAS-6 BUILT A PROTECTIVE SIGNAL FOR 847 DAYS.",
                _dataLoadedTextBuffer,
                out _dataLoadedTextLength);
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingAlreadyComplete()
        {
            Hecton8.Core.H8Debug.Log("[EndingTerminal] Ending already complete.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogChoiceUiOpened()
        {
            Hecton8.Core.H8Debug.Log("[EndingTerminal] Choice UI opened. " +
                      "Use GlobalRegistry.Ending.ChooseEnding(EndingChoice.X) to select.");
        }

        private void CopyLocalizedSpanToBuffer(string key, string fallback, char[] destination, out int length)
        {
            ReadOnlySpan<char> source = ResolveLocalizedSpan(key, fallback);
            length = Math.Min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination);
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }
    }
}
