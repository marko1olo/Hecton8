// ============================================================================
// HECTON-8 — EndingSystem.cs
// Система концовок игры.
//
// ЛОР (лор1 — Финал):
//   Игрок добрался до ядра Атлас-6 на -5000м.
//   Три выбора — ни один не "правильный". Это нуар.
//
//   ВЫКЛЮЧИТЬ АТЛАС-6:
//     Сигнал прекращается. Корпорация придёт.
//     Терраформирование продолжится. Жизнь уничтожена.
//     Игрок улетает. Экономически логично — морально нет.
//
//   ОСТАВИТЬ АТЛАС-6:
//     Сигнал продолжается. Корпорация не придёт пока сигнал активен.
//     Жизнь защищена — временно. Сигнал когда-нибудь найдут и заглушат.
//
//   УСИЛИТЬ СИГНАЛ:
//     Сигнал публичный — весь сектор слышит.
//     Корпорацию не остановить — но теперь все знают.
//     Атлас-6 выключается сам — задача выполнена.
//     Игрок становится тем, кто раскрыл тайну.
//
// АРХИТЕКТУРА:
//   • Отслеживает условия активации (глубина + расшифровка сигнала).
//   • Публикует события для всех систем при выборе концовки.
//   • ISaveable: сохраняет выбранную концовку.
//   • Интегрируется с Atlas6DirectiveSystem, QuestManager, NarrativeEvents.
//
// ZERO GC:
//   • Static events, enum state.
//   • Никаких new/LINQ в hot path.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum EndingChoice
    {
        None        = 0,
        ShutDown    = 1,   // Выключить Атлас-6
        Leave       = 2,   // Оставить Атлас-6
        Amplify     = 3    // Усилить сигнал
    }

    public static class EndingEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnEndingConditionMet = null;
            OnEndingChosen = null;
            OnEndingSequenceComplete = null;
        }

        /// <summary>Условия для концовки выполнены — игрок у ядра.</summary>
        public static event Action OnEndingConditionMet;

        /// <summary>Игрок сделал выбор концовки.</summary>
        public static event Action<EndingChoice> OnEndingChosen;

        /// <summary>Финальная последовательность завершена.</summary>
        public static event Action<EndingChoice> OnEndingSequenceComplete;

        public static void RaiseConditionMet()
            => OnEndingConditionMet?.Invoke();

        public static void RaiseChosen(EndingChoice choice)
            => OnEndingChosen?.Invoke(choice);

        public static void RaiseSequenceComplete(EndingChoice choice)
            => OnEndingSequenceComplete?.Invoke(choice);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class EndingSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Activation Conditions ───────────────────")]
        [Tooltip("Минимальная глубина для активации концовки (метры).")]
        [SerializeField] private float requiredDepth = 4800f;

        [Tooltip("Минимальная сила сигнала для активации (расшифровка).")]
        [SerializeField, Range(0f, 1f)] private float requiredSignalStrength = 0.90f;

        [Header("── Quest IDs ───────────────────────────────")]
        [SerializeField] private string endingQuestId = "quest_atlas_core_reached";

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static EndingSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private EndingChoice _chosenEnding = EndingChoice.None;
        private bool _conditionMet;
        private bool _endingComplete;
        private bool _registered;
        private HectonSurvivalSystem _survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 14;
        public int LoadPriority => 14;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public EndingChoice ChosenEnding    => _chosenEnding;
        public bool IsConditionMet          => _conditionMet;
        public bool IsEndingComplete        => _endingComplete;
        public bool CanChooseEnding         => _conditionMet && !_endingComplete;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            AtlasSignalEvents.OnSignalDecoded += HandleSignalDecoded;

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            AtlasSignalEvents.OnSignalDecoded -= HandleSignalDecoded;
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_conditionMet || _endingComplete) return;

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            if (depth < requiredDepth) return;

            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal == null) return;
            if (signal.CurrentStrength < requiredSignalStrength) return;

            // Условия выполнены
            _conditionMet = true;
            EndingEvents.RaiseConditionMet();

            // Активируем квест
            QuestManager qm = QuestManager.Instance;
            if (qm != null && !string.IsNullOrEmpty(endingQuestId))
                qm.ActivateQuest(endingQuestId);

            NarrativeEvents.RaiseDiscoveryMade("atlas6_core_reached");

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_CORE_REACHED,
                "ATLAS-6 CORE DETECTED. TERMINAL ACTIVE. SELECT AN ACTION."));

            LogEndingConditionMet();
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — ВЫБОР КОНЦОВКИ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Игрок выбрал концовку. Вызывается из UI терминала ядра.
        /// </summary>
        public void ChooseEnding(EndingChoice choice)
        {
            if (!CanChooseEnding)
            {
                LogInvalidEndingChoice(_conditionMet, _endingComplete);
                return;
            }

            if (choice == EndingChoice.None) return;

            _chosenEnding = choice;
            EndingEvents.RaiseChosen(choice);

            ExecuteEnding(choice);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — КОНЦОВКИ
        // ══════════════════════════════════════════════════════════

        private void ExecuteEnding(EndingChoice choice)
        {
            switch (choice)
            {
                case EndingChoice.ShutDown:
                    ExecuteShutDown();
                    break;

                case EndingChoice.Leave:
                    ExecuteLeave();
                    break;

                case EndingChoice.Amplify:
                    ExecuteAmplify();
                    break;
            }

            // Завершаем квест
            QuestManager qm = QuestManager.Instance;
            if (qm != null && !string.IsNullOrEmpty(endingQuestId))
                qm.CompleteQuest(endingQuestId);

            _endingComplete = true;
            EndingEvents.RaiseSequenceComplete(choice);

            LogEndingChoiceExecuted(choice);
        }

        private void ExecuteShutDown()
        {
            // Атлас-6 выключен. Сигнал прекращается.
            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null)
                signal.DecodeSignal("atlas6_shutdown");

            Atlas6DirectiveSystem directive = Atlas6DirectiveSystem.Instance;
            if (directive != null)
                directive.RegisterBarterTransaction(); // Корпорация получила что хотела

            NarrativeEvents.RaiseDiscoveryMade("ending_shutdown");

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_SHUTDOWN_COMPLETE,
                "ATLAS-6 SHUT DOWN. SIGNAL TERMINATED. THE CORPORATION WILL GET THE DATA. TERRAFORMING CONTINUES."));
        }

        private void ExecuteLeave()
        {
            // Атлас-6 продолжает работу. Сигнал активен.
            NarrativeEvents.RaiseDiscoveryMade("ending_leave");

            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.ENDING_LEAVE_COMPLETE,
                "ATLAS-6 REMAINS ACTIVE. SIGNAL LIVE. LIFE IS PROTECTED - UNTIL THE SIGNAL IS FOUND."));
        }

        private void ExecuteAmplify()
        {
            // Сигнал усилен — публичный. Атлас-6 выключается сам.
            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null)
                signal.DecodeSignal("atlas6_amplified_public");

            NarrativeEvents.RaiseDiscoveryMade("ending_amplify");

            // Публикуем в шейдер — максимальная интенсивность сигнала
            Shader.SetGlobalFloat(
                Shader.PropertyToID("_AtlasSignalStrength"), 1f);

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_AMPLIFY_COMPLETE,
                "SIGNAL AMPLIFIED. THE WHOLE SECTOR CAN HEAR IT. ATLAS-6 IS ENDING THE PROGRAM. THE TRUTH IS OUT. CONSEQUENCES UNPREDICTABLE."));
        }

        private bool ResolveSurvivalSystem()
        {
            if (_survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out _survivalSystem);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidEndingChoice(bool conditionMet, bool endingComplete)
        {
            Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingChoiceExecuted(EndingChoice choice)
        {
            Debug.Log($"[Ending] Choice executed: {choice}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingConditionMet()
        {
            Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
        }

        private void HandleSignalDecoded(string messageId)
        {
            // Полная расшифровка — условие может быть выполнено
            // SlowTick проверит глубину на следующем тике
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.endingChoice   = (int)_chosenEnding;
            data.endingComplete = _endingComplete;
            data.endingConditionMet = _conditionMet;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _chosenEnding   = (EndingChoice)data.endingChoice;
            _endingComplete = data.endingComplete;
            _conditionMet   = data.endingConditionMet;
        }
    }
}
