// ============================================================================
// HECTON-8 — AudioLogData.cs
// ScriptableObject: данные одного аудиодневника колонии.
// Лор: записи Chen_M, капитана, биолога, медика — фрагменты истории катастрофы.
// ============================================================================

using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Категория аудиодневника — определяет иконку и цвет в архиве PDA.
    /// </summary>
    public enum AudioLogCategory
    {
        Personal    = 0,   // Личные записи колонистов
        Technical   = 1,   // Технические отчёты
        Emergency   = 2,   // Экстренные сообщения
        Atlas6      = 3,   // Данные Атлас-6
        Unknown     = 4    // Неизвестный источник
    }

    [CreateAssetMenu(
        fileName = "AudioLog_",
        menuName  = "Hecton8/Narrative/Audio Log Data",
        order     = 10)]
    public sealed class AudioLogData : ScriptableObject
    {
        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID (используется в NarrativeEvents и сохранении).")]
        [SerializeField] public string logId;

        [Tooltip("Отображаемое название в архиве PDA.")]
        [SerializeField] public string displayTitle = "НЕИЗВЕСТНАЯ ЗАПИСЬ";

        [Tooltip("Автор записи (Chen_M, Captain, Biologist...).")]
        [SerializeField] public string author = "НЕИЗВЕСТНО";

        [Tooltip("Категория для фильтрации в PDA.")]
        [SerializeField] public AudioLogCategory category = AudioLogCategory.Unknown;

        [Header("── Content ──────────────────────────────────")]
        [Tooltip("Аудиоклип записи (30-90 сек, с помехами).")]
        [SerializeField] public AudioClip audioClip;

        [Tooltip("Текст субтитров (для локализации и доступности).")]
        [SerializeField, TextArea(3, 8)] public string subtitleText;

        [Tooltip("Длительность в секундах (авто из клипа если 0).")]
        [SerializeField] public float durationOverride;

        [Header("── Lore ─────────────────────────────────────")]
        [Tooltip("Краткое описание для архива PDA (1-2 предложения).")]
        [SerializeField, TextArea(2, 4)] public string archiveSummary;

        [Tooltip("Дата записи (игровое время колонии).")]
        [SerializeField] public string recordDate = "ДАТА НЕИЗВЕСТНА";

        /// <summary>Реальная длительность клипа или override.</summary>
        public float Duration =>
            durationOverride > 0f
                ? durationOverride
                : (audioClip != null ? audioClip.length : 0f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(logId))
                logId = name.ToLower().Replace(" ", "_");
        }
#endif
    }
}
