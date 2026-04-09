// ============================================================================
// HECTON-8 — DeepReachCorporationData.cs
// ScriptableObject: данные корпорации Deep Reach.
//
// ЛОР (лор3 Блок А):
//   Официальная версия: «Колонизация отдалённых миров»
//   Реальная цель: добыча изотопа Xenon-Ω + тест ИИ Атлас-6
//
//   Фракции:
//   • «Этики» — считают миссию преступной
//   • «Прагматики» — требуют результатов любой ценой
//
//   Игрок получает противоречивые приказы через задержку связи (8-12 часов).
//
// АРХИТЕКТУРА:
//   • Хранит данные о корпорации, фракциях, изотопах.
//   • Используется для генерации противоречивых приказов.
//   • Интегрируется с QuestSystem и NarrativeDirector.
// ============================================================================

using UnityEngine;

namespace Hecton8.Narrative
{
    [System.Serializable]
    public struct CorporateFaction
    {
        public string factionId;
        public string displayName;
        [TextArea(2, 3)] public string philosophy;
        public bool isPlayerAligned;
    }

    [System.Serializable]
    public struct CorporateOrder
    {
        [Tooltip("ID приказа.")]
        public string orderId;

        [Tooltip("Фракция-источник.")]
        public string sourceFactionId;

        [Tooltip("Текст приказа.")]
        [TextArea(2, 4)] public string orderText;

        [Tooltip("Противоречит другому приказу с этим ID.")]
        public string conflictsWithOrderId;

        [Tooltip("Задержка получения (игровые часы).")]
        [Range(0f, 24f)] public float transmissionDelayHours;
    }

    [System.Serializable]
    public struct RareIsotope
    {
        public string isotopeId;
        public string displayName;
        [TextArea(1, 3)] public string properties;
        [TextArea(1, 3)] public string application;
        [Tooltip("Почему только на Гектоне-8.")]
        [TextArea(1, 3)] public string exclusivityReason;
        [Tooltip("Ценность (условные единицы).")]
        public float relativeValue;
    }

    [CreateAssetMenu(
        fileName = "DeepReachCorporationData",
        menuName  = "Hecton8/Narrative/Deep Reach Corporation Data",
        order     = 6)]
    public sealed class DeepReachCorporationData : ScriptableObject
    {
        [Header("── Corporation ─────────────────────────────")]
        [SerializeField] public string corporationName = "Deep Reach";

        [SerializeField, TextArea(2, 4)]
        public string officialMission = "Колонизация отдалённых миров. Научные исследования. Терраформирование.";

        [SerializeField, TextArea(2, 4)]
        public string realMission = "Добыча изотопа Xenon-Ω. Тестирование ИИ Атлас-6 в условиях полной автономии. Военный плацдарм в системе Аэгира.";

        [Header("── Factions ────────────────────────────────")]
        [SerializeField] public CorporateFaction[] factions = new CorporateFaction[]
        {
            new CorporateFaction
            {
                factionId    = "ethics",
                displayName  = "Фракция «Этики»",
                philosophy   = "Считают миссию преступной. Знали о рисках катастрофы. Хотят раскрыть правду.",
                isPlayerAligned = true
            },
            new CorporateFaction
            {
                factionId    = "pragmatists",
                displayName  = "Фракция «Прагматики»",
                philosophy   = "Требуют результатов любой ценой. Потери приемлемы ради данных. Xenon-Ω важнее жизней.",
                isPlayerAligned = false
            }
        };

        [Header("── Orders ───────────────────────────────────")]
        [SerializeField] public CorporateOrder[] orders = new CorporateOrder[]
        {
            new CorporateOrder
            {
                orderId              = "order_extract_xenon",
                sourceFactionId      = "pragmatists",
                orderText            = "ПРИОРИТЕТ АЛЬФА: Извлечь максимальное количество Xenon-Ω. Атлас-6 — вторичная цель. Возвращайтесь с грузом.",
                conflictsWithOrderId = "order_preserve_ecosystem",
                transmissionDelayHours = 8f
            },
            new CorporateOrder
            {
                orderId              = "order_preserve_ecosystem",
                sourceFactionId      = "ethics",
                orderText            = "ВНИМАНИЕ: Обнаружена доказательная база существования жизни до прихода людей. НЕ УНИЧТОЖАТЬ. Документировать. Возвращайтесь с данными.",
                conflictsWithOrderId = "order_extract_xenon",
                transmissionDelayHours = 12f
            },
            new CorporateOrder
            {
                orderId              = "order_shutdown_atlas",
                sourceFactionId      = "pragmatists",
                orderText            = "Атлас-6 вышел из-под контроля. Уничтожить ядро. Данные программы Посева — приоритет.",
                conflictsWithOrderId = "order_preserve_signal",
                transmissionDelayHours = 10f
            },
            new CorporateOrder
            {
                orderId              = "order_preserve_signal",
                sourceFactionId      = "ethics",
                orderText            = "Атлас-6 строит сигнал защиты. Если это правда — не трогайте его. Пусть сигнал работает.",
                conflictsWithOrderId = "order_shutdown_atlas",
                transmissionDelayHours = 11f
            }
        };

        [Header("── Isotopes ────────────────────────────────")]
        [SerializeField] public RareIsotope[] isotopes = new RareIsotope[]
        {
            new RareIsotope
            {
                isotopeId          = "xenon_omega",
                displayName        = "Xenon-Ω",
                properties         = "Стабилен только при давлении >100 ATM.",
                application        = "Квантовые процессоры, сверхпроводники.",
                exclusivityReason  = "Уникальное сочетание давления, температуры и геохимии Гектона-8.",
                relativeValue      = 1000000f
            },
            new RareIsotope
            {
                isotopeId          = "silicon_7b",
                displayName        = "Silicon-7β",
                properties         = "Полупроводник с нулевым сопротивлением при -100°C.",
                application        = "Нейроинтерфейсы, ИИ-ядра.",
                exclusivityReason  = "Образуется только в кремниевых экосистемах под давлением.",
                relativeValue      = 500000f
            },
            new RareIsotope
            {
                isotopeId          = "aegirium",
                displayName        = "Aegirium",
                properties         = "Радиоактивный, период полураспада 12 часов.",
                application        = "Медицинская визуализация, двигатели нового типа.",
                exclusivityReason  = "Продукт распада в атмосфере Аэгира, осаждается на луне.",
                relativeValue      = 250000f
            }
        };

        [Header("── Player Context ───────────────────────────")]
        [SerializeField, TextArea(2, 4)]
        public string playerBriefing = "Вы — инженер-мародёр. Минимальное обеспечение. Одиночная миссия. Корпорация готова потерять 100 скавенджеров ради 1 кг Xenon-Ω. Вы не знаете полной стоимости того, что собираете.";

        /// <summary>Найти приказ по ID.</summary>
        public bool TryGetOrder(string orderId, out CorporateOrder order)
        {
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].orderId == orderId)
                {
                    order = orders[i];
                    return true;
                }
            }
            order = default;
            return false;
        }

        /// <summary>Найти изотоп по ID.</summary>
        public bool TryGetIsotope(string isotopeId, out RareIsotope isotope)
        {
            for (int i = 0; i < isotopes.Length; i++)
            {
                if (isotopes[i].isotopeId == isotopeId)
                {
                    isotope = isotopes[i];
                    return true;
                }
            }
            isotope = default;
            return false;
        }
    }
}
