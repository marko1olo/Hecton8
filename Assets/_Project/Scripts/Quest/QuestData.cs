// ============================================================================
// HECTON-8 — QuestData.cs
// ScriptableObject: данные одного квеста (stateless, data-driven).
//
// Лор: квесты не квест-маркеры — это органические цели.
// Игрок видит структуру на горизонте → квест активируется.
// Нет стрелок. Нет туториала. Только мир.
// ============================================================================

using UnityEngine;

namespace Hecton8.Quest
{
    /// <summary>
    /// Тип триггера активации квеста.
    /// </summary>
    public enum QuestTriggerType
    {
        OnItemCollected     = 0,   // Подобран предмет с указанным itemId
        OnDepthReached      = 1,   // Достигнута глубина (метры)
        OnBiomeEntered      = 2,   // Вход в биом с указанным biomeId
        OnDiscoveryMade     = 3,   // Обнаружен нарративный объект с discoveryId
        OnAudioLogFound     = 4,   // Найден аудиодневник с logId
        OnEclipseStart      = 5,   // Началось Великое Затмение
        OnSignalDetected    = 6,   // Обнаружен сигнал Атлас-6
        Manual              = 99   // Активируется только через QuestManager.ActivateQuest()
    }

    /// <summary>
    /// Тип условия завершения квеста.
    /// </summary>
    public enum QuestCompletionType
    {
        OnItemCollected     = 0,
        OnDepthReached      = 1,
        OnBiomeEntered      = 2,
        OnDiscoveryMade     = 3,
        OnAudioLogFound     = 4,
        OnSignalDecoded     = 5,
        Manual              = 99
    }

    [CreateAssetMenu(
        fileName = "Quest_",
        menuName  = "Hecton8/Quest/Quest Data",
        order     = 20)]
    public sealed class QuestData : ScriptableObject
    {
        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID квеста.")]
        [SerializeField] public string questId;

        [Tooltip("Отображаемое название.")]
        [SerializeField] public string displayTitle = "НЕИЗВЕСТНАЯ ЦЕЛЬ";

        [Tooltip("Краткое описание для PDA.")]
        [SerializeField, TextArea(2, 5)] public string description;

        [Header("── Activation ──────────────────────────────")]
        [Tooltip("Тип триггера активации.")]
        [SerializeField] public QuestTriggerType triggerType = QuestTriggerType.Manual;

        [Tooltip("ID для триггера (itemId / discoveryId / logId / signalId).")]
        [SerializeField] public string triggerId;

        [Tooltip("Числовое значение для триггера (глубина в метрах / biomeId).")]
        [SerializeField] public float triggerValue;

        [Header("── Completion ──────────────────────────────")]
        [Tooltip("Тип условия завершения.")]
        [SerializeField] public QuestCompletionType completionType = QuestCompletionType.Manual;

        [Tooltip("ID для условия завершения.")]
        [SerializeField] public string completionId;

        [Tooltip("Числовое значение для завершения.")]
        [SerializeField] public float completionValue;

        [Header("── Flags ────────────────────────────────────")]
        [Tooltip("Квест активируется автоматически при старте игры.")]
        [SerializeField] public bool autoActivateOnStart;

        [Tooltip("Квест можно выполнить только один раз.")]
        [SerializeField] public bool oneTimeOnly = true;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(questId))
                questId = name.ToLower().Replace(" ", "_");
        }
#endif
    }
}
