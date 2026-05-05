// ============================================================================
// HECTON-8 — EndingTerminalInteractable.cs
// Интерактивный терминал ядра Атлас-6 — точка выбора концовки.
//
// ЛОР: Терминал рядом с ядром на -5000м.
// На терминале: полные данные программы Посева, причина "поломки" Атлас-6,
// и — главное — что он строил 847 дней.
//
// АРХИТЕКТУРА:
//   • IInteractable — взаимодействие открывает UI выбора концовки.
//   • Активен только если EndingSystem.IsConditionMet.
//   • Показывает три варианта через NotificationEvents (временно).
//   • В финальной версии — отдельный UI экран.
// ============================================================================

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
    public sealed class EndingTerminalInteractable : MonoBehaviour, IInteractable, IEndingEventListener, ILocalizationLanguageChangedListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Visual ───────────────────────────────────")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private GameObject activeIndicator;

        [Header("Quest DAG Gate")]
        [SerializeField] private string[] requiredAtlasKeyQuestIds =
        {
            "quest_atlas_signal_detected",
            "quest_atlas_signal_decoded",
            "quest_atlas_core_reached"
        };

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _choiceOpen;
        private string _cachedInactiveText;
        private string _cachedActiveText;
        private string _cachedCompleteText;
        private string _cachedDataLoadedText;

        // Pre-cached interact texts — zero GC
        private const string TextInactive = "ATLAS-6 TERMINAL UNAVAILABLE";
        private const string TextActive = "INTERACT WITH ATLAS-6 CORE";
        private const string TextComplete = "DECISION RECORDED";

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            EndingEvents.Register(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
            UpdateActiveIndicator();
        }

        private void OnDisable()
        {
            SetObjectActive(highlightObject, false);
            _choiceOpen = false;
            LocalizationEvents.UnregisterLanguageListener(this);
            EndingEvents.Unregister(this);
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

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
            EndingSystem ending = GlobalRegistry.Ending;
            if (ending == null) return;

            if (ending.IsEndingComplete)
            {
                LogEndingAlreadyComplete();
                return;
            }

            if (!HasAllAtlasKeys())
            {
                NarrativeEvents.RaiseDiscoveryMade("atlas6_terminal_inactive");
                return;
            }

            if (!ending.IsConditionMet)
                ending.ForceConditionMetFromQuestDAG();

            if (_choiceOpen) return;

            OpenChoiceUI();
        }

        public string GetInteractText()
        {
            EndingSystem ending = GlobalRegistry.Ending;
            if (ending == null) return _cachedInactiveText;
            if (ending.IsEndingComplete) return _cachedCompleteText;
            if (!HasAllAtlasKeys()) return _cachedInactiveText;
            return _cachedActiveText;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void OpenChoiceUI()
        {
            _choiceOpen = true;

            // Показываем данные Атлас-6 через нарратив
            NarrativeEvents.RaiseDiscoveryMade("atlas6_core_data_accessed");

            // Публикуем три варианта через HUD
            // В финальной версии — отдельный UI экран с тремя кнопками
            // Сейчас — уведомления с инструкцией
            Hecton8.UI.NotificationEvents.PushWarning(
                _cachedDataLoadedText);

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

            EndingSystem ending = GlobalRegistry.Ending;
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

            EndingSystem ending = GlobalRegistry.Ending;
            bool active = ending != null && HasAllAtlasKeys() && !ending.IsEndingComplete;
            SetObjectActive(activeIndicator, active);
        }

        private bool HasAllAtlasKeys()
        {
            QuestManager questManager = GlobalRegistry.Quest;
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

        private void RebuildLocalizedTextCache()
        {
            _cachedInactiveText = ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_INACTIVE, TextInactive);
            _cachedActiveText = ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_ACTIVE, TextActive);
            _cachedCompleteText = ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_COMPLETE, TextComplete);
            _cachedDataLoadedText = ResolveLocalized(
                LocalizationKeys.ENDING_TERMINAL_DATA_LOADED,
                "ATLAS-6: SEED PROGRAM DATA LOADED. LIFE ON HECTON-8 PRE-DATES HUMAN ARRIVAL. ATLAS-6 BUILT A PROTECTIVE SIGNAL FOR 847 DAYS.");
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingAlreadyComplete()
        {
            Debug.Log("[EndingTerminal] Ending already complete.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogChoiceUiOpened()
        {
            Debug.Log("[EndingTerminal] Choice UI opened. " +
                      "Use GlobalRegistry.Ending.ChooseEnding(EndingChoice.X) to select.");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
                return fallback;

            return manager.GetOrFallback(manager.CurrentLanguage, key, fallback);
        }
    }
}
