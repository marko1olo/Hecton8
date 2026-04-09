// ============================================================================
// HECTON-8 — FaunaLoreRegistry.cs
// ScriptableObject: лорный реестр всех типов существ.
//
// ЛОР (лор2 Раздел 8):
//   Тип 1: Дружелюбные/Нейтральные — Рифоспиннер, Стеклоочиститель
//   Тип 2: Классические хищники — Титановый левиафан, Электрический скат, Кошмар из Бездны
//   Тип 3: «Мягкие стены» — Призрак Термоклина, Корневой дракон
//   Тип 4: Боссы — Термитный Червь, Дредноут
//   Дроны Атлас-6: Строитель, Защитник, Курьер
//
// АРХИТЕКТУРА:
//   • Хранит лорные данные о существах (не геймплейные параметры).
//   • Используется для PDA-кодекса и экологического сторителлинга.
//   • Связывается с CreatureArchetypeData через creatureId.
// ============================================================================

using UnityEngine;

namespace Hecton8.Narrative
{
    public enum FaunaType
    {
        Ambient     = 0,   // Дружелюбные/нейтральные
        Predator    = 1,   // Классические хищники
        SoftWall    = 2,   // «Мягкие стены» (блокируют зоны)
        Boss        = 3,   // Боссы
        Atlas6Drone = 4    // Дроны Атлас-6
    }

    [System.Serializable]
    public struct FaunaLoreEntry
    {
        [Tooltip("ID существа (совпадает с CreatureArchetypeData.creatureId).")]
        public string creatureId;

        [Tooltip("Отображаемое название.")]
        public string displayName;

        [Tooltip("Тип существа.")]
        public FaunaType faunaType;

        [Tooltip("Зона обитания.")]
        public string habitatZone;

        [Tooltip("Размер (метры).")]
        public float sizeMeters;

        [Tooltip("Описание для PDA-кодекса.")]
        [TextArea(3, 6)] public string codexDescription;

        [Tooltip("Геймплейная роль.")]
        [TextArea(1, 3)] public string gameplayRole;

        [Tooltip("Научное обоснование (реальный аналог).")]
        [TextArea(1, 3)] public string scientificBasis;

        [Tooltip("Ночное поведение отличается от дневного.")]
        public bool hasNightBehavior;

        [Tooltip("Описание ночного поведения.")]
        [TextArea(1, 3)] public string nightBehaviorDescription;
    }

    [CreateAssetMenu(
        fileName = "FaunaLoreRegistry",
        menuName  = "Hecton8/Narrative/Fauna Lore Registry",
        order     = 7)]
    public sealed class FaunaLoreRegistry : ScriptableObject
    {
        [SerializeField] public FaunaLoreEntry[] entries = new FaunaLoreEntry[]
        {
            // ── Тип 1: Дружелюбные ──────────────────────────────
            new FaunaLoreEntry
            {
                creatureId          = "creature.reef_glider",
                displayName         = "Рифоспиннер (Reef Glider)",
                faunaType           = FaunaType.Ambient,
                habitatZone         = "Мелководье, шельф (0-500м)",
                sizeMeters          = 25f,
                codexDescription    = "Гигантский скат с кораллами и светящимися водорослями на спине. Движения плавные, величественные. Игнорирует игрока. Можно кататься на спине.",
                gameplayRole        = "Показать масштаб. Красивые скриншоты. Ощущение живого мира.",
                scientificBasis     = "Гигантские скаты реальны. Симбиоз с кораллами — реальное явление.",
                hasNightBehavior    = false
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.glassworm",
                displayName         = "Стеклоочиститель (Glassworm)",
                faunaType           = FaunaType.Ambient,
                habitatZone         = "Толща воды (200-800м)",
                sizeMeters          = 40f,
                codexDescription    = "Гигантский полупрозрачный червь-фильтратор. Медленно изгибается в толще воды. Если заплыть внутрь — выплюнет (не смертельно).",
                gameplayRole        = "Визуальное разнообразие. Создание «чуда».",
                scientificBasis     = "Фильтраторы реальны (сальпы, пиросомы). Гигантская версия.",
                hasNightBehavior    = false
            },

            // ── Тип 2: Хищники ──────────────────────────────────
            new FaunaLoreEntry
            {
                creatureId          = "creature.titan_leviathan",
                displayName         = "Титановый Левиафан",
                faunaType           = FaunaType.Predator,
                habitatZone         = "Восточная стена, Бездна (1000м+)",
                sizeMeters          = 55f,
                codexDescription    = "Огромный угорь с металлизированной чешуёй и гидравлической пастью. Вдоль хребта — светящиеся разряды. Медленно патрулирует территорию. Издаёт низкочастотный гул.",
                gameplayRole        = "Главный хищник. Виден издалека. Создаёт anticipation.",
                scientificBasis     = "Огромные угри реальны. Электрические органы — реальны.",
                hasNightBehavior    = true,
                nightBehaviorDescription = "Поднимается выше (до 500м). Активно охотится."
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.volt_manta",
                displayName         = "Электрический Скат-Переросток (Volt Manta)",
                faunaType           = FaunaType.Predator,
                habitatZone         = "Западный шельф, у руин (100-800м)",
                sizeMeters          = 30f,
                codexDescription    = "Огромный скат с длинным хвостом-электрогенератором. Охраняет ценные обломки. Не убивает, но оглушает разрядом.",
                gameplayRole        = "Охраняет ресурсы. Заставляет думать, не просто стрелять.",
                scientificBasis     = "Электрические скаты реальны.",
                hasNightBehavior    = true,
                nightBehaviorDescription = "Активно патрулирует. Агрессивнее."
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.abyssal_nightmare",
                displayName         = "Кошмар из Бездны",
                faunaType           = FaunaType.Predator,
                habitatZone         = "Глубокие пещеры (1000м+)",
                sizeMeters          = 8f,
                codexDescription    = "Полупрозрачный, почти невидимый в темноте. Светятся только глаза и внутренние органы. Висит неподвижно. При приближении — резко «схлопывается» к игроку.",
                gameplayRole        = "Скример. Работает на контрасте с тишиной.",
                scientificBasis     = "Глубоководные рыбы реально полупрозрачны.",
                hasNightBehavior    = false
            },

            // ── Тип 3: Мягкие стены ─────────────────────────────
            new FaunaLoreEntry
            {
                creatureId          = "creature.thermal_phantom",
                displayName         = "Призрак Термоклина",
                faunaType           = FaunaType.SoftWall,
                habitatZone         = "Граница термоклина (1000-1200м)",
                sizeMeters          = 40f,
                codexDescription    = "Полупрозрачная медузообразная тварь. Обитает на границе термоклина. Обжигает щупальцами и выталкивает наверх — не смертельно, но больно.",
                gameplayRole        = "Маркер «дальше опасно, нужен апгрейд».",
                scientificBasis     = "Термоклин реален. Медузы с термальными линзами — гипотеза.",
                hasNightBehavior    = false
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.root_dragon",
                displayName         = "Корневой Дракон",
                faunaType           = FaunaType.SoftWall,
                habitatZone         = "Пещеры, у выходов (любая глубина)",
                sizeMeters          = 60f,
                codexDescription    = "Гигантский организм, наполовину вросший в скалу. Из стены торчит только морда и пара лап. Кажется спящим. Если подплыть — голова медленно поворачивается. Не атакует, но взгляд парализует.",
                gameplayRole        = "Психологический барьер. Игрок не знает, проснётся он или нет.",
                scientificBasis     = "Организмы, вросшие в субстрат — реальны (морские лилии, асцидии).",
                hasNightBehavior    = false
            },

            // ── Тип 4: Боссы ────────────────────────────────────
            new FaunaLoreEntry
            {
                creatureId          = "creature.termite_worm",
                displayName         = "Термитный Червь",
                faunaType           = FaunaType.Boss,
                habitatZone         = "Глубокие зоны (2000м+)",
                sizeMeters          = 100f,
                codexDescription    = "Огромный червь, прорывающий тоннели в скалах. Тело покрыто панцирем из металла и камня. Голова — гигантская фреза. Изначально нейтрален. Может разрушать постройки игрока.",
                gameplayRole        = "Динамическое изменение мира. Открывает новые проходы или уничтожает базу.",
                scientificBasis     = "Черви-бурильщики реальны (Eunice aphroditois).",
                hasNightBehavior    = false
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.atlas_dreadnought",
                displayName         = "Дредноут (Atlas Dreadnought)",
                faunaType           = FaunaType.Boss,
                habitatZone         = "Ядро Атласа (-5000м)",
                sizeMeters          = 30f,
                codexDescription    = "Биомеханический гибрид — помесь краба и промышленного экскаватора. На спине — башни ИИ Атласа, стреляющие плазмой. Охраняет ядро. Финальный босс.",
                gameplayRole        = "Финальный босс. Требует тактики и всего арсенала.",
                scientificBasis     = "Биомеханические гибриды — концепция Атлас-6.",
                hasNightBehavior    = false
            },

            // ── Дроны Атлас-6 ───────────────────────────────────
            new FaunaLoreEntry
            {
                creatureId          = "creature.atlas_builder",
                displayName         = "Строитель (Atlas Builder Drone)",
                faunaType           = FaunaType.Atlas6Drone,
                habitatZone         = "Везде (80-3000м)",
                sizeMeters          = 3f,
                codexDescription    = "Металлический корпус с органическими трубками. Тащит материалы к точкам сборки. Не атакует если не мешать. При угрозе — высокочастотный писк, вызывает Защитника.",
                gameplayRole        = "Первая встреча с дронами. Неопределённость страшнее угрозы.",
                scientificBasis     = "Программа Посева Атлас-6.",
                hasNightBehavior    = false
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.atlas_defender",
                displayName         = "Защитник (Atlas Defender Drone)",
                faunaType           = FaunaType.Atlas6Drone,
                habitatZone         = "Зоны строительства (200-4000м)",
                sizeMeters          = 5f,
                codexDescription    = "Боевой дрон. Реагирует на писк Строителя. Агрессивен. Охраняет строительные зоны и ядро Атлас-6.",
                gameplayRole        = "Угроза при нарушении работы Строителей.",
                scientificBasis     = "Программа Посева Атлас-6.",
                hasNightBehavior    = false
            },
            new FaunaLoreEntry
            {
                creatureId          = "creature.atlas_courier",
                displayName         = "Курьер (Atlas Courier Drone)",
                faunaType           = FaunaType.Atlas6Drone,
                habitatZone         = "Маяки связи",
                sizeMeters          = 1.5f,
                codexDescription    = "Маленький быстрый дрон. Доставляет капсулы бартера к маякам. Никакого взаимодействия. Просто — доставка.",
                gameplayRole        = "Атмосфера: Атлас-6 — не персонаж, а система.",
                scientificBasis     = "Программа Посева Атлас-6.",
                hasNightBehavior    = false
            }
        };

        /// <summary>Найти запись по creatureId.</summary>
        public bool TryGetEntry(string creatureId, out FaunaLoreEntry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].creatureId == creatureId)
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
