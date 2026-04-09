// ============================================================================
// HECTON-8 — ColonistLoreRegistry.cs
// ScriptableObject: каталог всех лорных объектов колонии.
//
// ЛОР: Chen_M датапад, аудиозаписи, схема дрона, личные вещи.
// Используется для назначения NarrativeDiscovery и AudioLogPickup в сцене.
//
// АРХИТЕКТУРА:
//   • Единый реестр — не нужно искать объекты по сцене.
//   • Каждая запись: discoveryId + опциональный AudioLogData.
//   • Используется PDADataLogTab для отображения архива.
// ============================================================================

using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Тип лорного объекта — определяет иконку и поведение в PDA.
    /// </summary>
    public enum LoreObjectType
    {
        DataPad     = 0,   // КПК / датапад
        AudioLog    = 1,   // Аудиозапись
        Blueprint   = 2,   // Чертёж / схема
        PersonalItem = 3,  // Личный предмет
        Terminal    = 4,   // Терминал / лог системы
        Wreckage    = 5    // Обломки / скафандр
    }

    [System.Serializable]
    public struct LoreEntry
    {
        [Tooltip("Уникальный ID (совпадает с NarrativeDiscovery.discoveryId).")]
        public string discoveryId;

        [Tooltip("Отображаемое название в PDA.")]
        public string displayName;

        [Tooltip("Тип объекта.")]
        public LoreObjectType objectType;

        [Tooltip("Связанный аудиодневник (опционально).")]
        public AudioLogData linkedAudioLog;

        [Tooltip("Краткое описание для PDA.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Зона/модуль где находится объект.")]
        public string locationHint;
    }

    [CreateAssetMenu(
        fileName = "ColonistLoreRegistry",
        menuName  = "Hecton8/Narrative/Colonist Lore Registry",
        order     = 5)]
    public sealed class ColonistLoreRegistry : ScriptableObject
    {
        [Header("── Колонисты ───────────────────────────────")]
        [Tooltip("Все лорные объекты колонии. Заполнить вручную.")]
        [SerializeField] public LoreEntry[] entries = new LoreEntry[]
        {
            // Chen_M — инженер, заблокировал шлюз изнутри
            new LoreEntry
            {
                discoveryId  = "chen_m_datapad_01",
                displayName  = "КПК — Chen_M (Запись 1/3)",
                objectType   = LoreObjectType.DataPad,
                description  = "Технический отчёт. Скучный. Дата: 847 дней назад.",
                locationHint = "Жилой модуль A, стол"
            },
            new LoreEntry
            {
                discoveryId  = "chen_m_datapad_02",
                displayName  = "КПК — Chen_M (Запись 2/3)",
                objectType   = LoreObjectType.DataPad,
                description  = "«Слышим движение под модулем. Докладываю.»",
                locationHint = "Жилой модуль A, стол"
            },
            new LoreEntry
            {
                discoveryId  = "chen_m_datapad_03",
                displayName  = "КПК — Chen_M (Запись 3/3)",
                objectType   = LoreObjectType.DataPad,
                description  = "Запись обрывается на полуслове.",
                locationHint = "Жилой модуль A, стол"
            },
            new LoreEntry
            {
                discoveryId  = "chen_m_blueprint",
                displayName  = "Рукописная схема — Модификация дрона",
                objectType   = LoreObjectType.Blueprint,
                description  = "Схема модификации стандартного дрона Атлас-6. Кто-то пытался перепрограммировать дрона.",
                locationHint = "Жилой модуль A, стол"
            },
            new LoreEntry
            {
                discoveryId  = "chen_m_suit",
                displayName  = "Скафандр — Идентификатор: CHEN_M",
                objectType   = LoreObjectType.Wreckage,
                description  = "Скафандр на дне. Рядом — самодельный инструмент неизвестного назначения.",
                locationHint = "Глубина 800м, тектонический разлом"
            },
            // Капитан
            new LoreEntry
            {
                discoveryId  = "captain_last_broadcast",
                displayName  = "Последняя трансляция — Капитан",
                objectType   = LoreObjectType.AudioLog,
                description  = "«Атлас... он не отвечает. Но мы видим, как он... растёт.» Обрывается на звуке скрежета.",
                locationHint = "Командный модуль, терминал"
            },
            // Биолог
            new LoreEntry
            {
                discoveryId  = "biologist_samples",
                displayName  = "Образцы — Биолог (пометки)",
                objectType   = LoreObjectType.DataPad,
                description  = "Образцы кремниевой флоры с пометками о «странном поведении». Первые признаки адаптации Атласа к биомассе.",
                locationHint = "Лаборатория, полка"
            },
            // Медик
            new LoreEntry
            {
                discoveryId  = "medic_diary",
                displayName  = "Дневник симптомов — Медик",
                objectType   = LoreObjectType.DataPad,
                description  = "Описание «синдрома глубины» — галлюцинации, паранойя. Пустые ампулы.",
                locationHint = "Медицинский отсек"
            },
            // Ребёнок колониста
            new LoreEntry
            {
                discoveryId  = "child_drawing",
                displayName  = "Рисунок — «Наш дом под водой»",
                objectType   = LoreObjectType.PersonalItem,
                description  = "Детский рисунок. Игрушка рядом. Невинность, контрастирующая с катастрофой.",
                locationHint = "Жилой модуль C, детская полка"
            },
            // Терминал Атлас-6
            new LoreEntry
            {
                discoveryId  = "atlas6_terminal_sector3",
                displayName  = "Терминал — Попытка взлома Атлас-6",
                objectType   = LoreObjectType.Terminal,
                description  = "Попытка взлома системы Атлас-6 пользователем CHEN_M. Неудачная. Дата: 847 дней назад.",
                locationHint = "Сектор 3, навигационный пост"
            },
        };

        /// <summary>Найти запись по discoveryId. Возвращает default если не найдена.</summary>
        public bool TryGetEntry(string discoveryId, out LoreEntry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].discoveryId == discoveryId)
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
