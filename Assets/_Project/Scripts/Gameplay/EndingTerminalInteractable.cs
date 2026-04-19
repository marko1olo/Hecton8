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
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EndingTerminalInteractable : MonoBehaviour, IInteractable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Visual ───────────────────────────────────")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private GameObject activeIndicator;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _choiceOpen;

        // Pre-cached interact texts — zero GC
        private const string TextInactive = "ATLAS-6 TERMINAL UNAVAILABLE";
        private const string TextActive = "INTERACT WITH ATLAS-6 CORE";
        private const string TextComplete = "DECISION RECORDED";

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            EndingEvents.OnEndingConditionMet += HandleConditionMet;
            EndingEvents.OnEndingChosen       += HandleEndingChosen;
            UpdateActiveIndicator();
        }

        private void OnDisable()
        {
            EndingEvents.OnEndingConditionMet -= HandleConditionMet;
            EndingEvents.OnEndingChosen       -= HandleEndingChosen;
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

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
            EndingSystem ending = EndingSystem.Instance;
            if (ending == null) return;

            if (ending.IsEndingComplete)
            {
                LogEndingAlreadyComplete();
                return;
            }

            if (!ending.IsConditionMet)
            {
                NarrativeEvents.RaiseDiscoveryMade("atlas6_terminal_inactive");
                return;
            }

            if (_choiceOpen) return;

            OpenChoiceUI();
        }

        public string GetInteractText()
        {
            EndingSystem ending = EndingSystem.Instance;
            if (ending == null) return ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_INACTIVE, TextInactive);
            if (ending.IsEndingComplete) return ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_COMPLETE, TextComplete);
            if (!ending.IsConditionMet)  return ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_INACTIVE, TextInactive);
            return ResolveLocalized(LocalizationKeys.ENDING_TERMINAL_ACTIVE, TextActive);
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
                ResolveLocalized(
                    LocalizationKeys.ENDING_TERMINAL_DATA_LOADED,
                    "ATLAS-6: SEED PROGRAM DATA LOADED. LIFE ON HECTON-8 PRE-DATES HUMAN ARRIVAL. ATLAS-6 BUILT A PROTECTIVE SIGNAL FOR 847 DAYS."));

            LogChoiceUiOpened();
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

            EndingSystem ending = EndingSystem.Instance;
            bool active = ending != null && ending.IsConditionMet && !ending.IsEndingComplete;
            activeIndicator.SetActive(active);
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
                      "Use EndingSystem.Instance.ChooseEnding(EndingChoice.X) to select.");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }
    }
}
