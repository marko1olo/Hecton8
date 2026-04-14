// ============================================================================
// HECTON-8 — SuitUpgradeData.cs
// ScriptableObject: данные одного апгрейда скафандра.
//
// ЛОР (лор1):
//   Tier 0: до -150м, O2 4 мин
//   Tier 1: до -500м, O2 8 мин  (первый крафт)
//   Tier 2: до -1500м, O2 15 мин
//   Tier 3: до -3500м, O2 25 мин (рециркуляция)
//   Tier 4: до -5000м, O2 45 мин (замкнутый)
//
// АРХИТЕКТУРА:
//   • Апгрейд — дельта к базовым параметрам SurvivalStats.
//   • SuitUpgradeManager применяет апгрейды через OverrideStats().
//   • Требования: список ItemData + опциональный discoveryId чертежа.
// ============================================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Категория апгрейда скафандра.
    /// </summary>
    public enum SuitUpgradeCategory
    {
        Hull        = 0,   // Корпус — увеличивает SafeDepth
        Oxygen      = 1,   // Кислород — увеличивает MaxOxygen
        Energy      = 2,   // Энергия — увеличивает MaxEnergy
        Sensors     = 3,   // Сенсоры — расширяет возможности сканера
        Thermal     = 4,   // Термозащита — расширяет MinSafeTemp/MaxSafeTemp
        Radiation   = 5    // Радиозащита — увеличивает RadiationThreshold
    }

    [System.Serializable]
    public struct SuitUpgradeRequirement
    {
        [Tooltip("ID предмета из ItemCatalog.")]
        public string itemId;

        [Tooltip("Количество.")]
        public int quantity;
    }

    [CreateAssetMenu(
        fileName = "SuitUpgrade_",
        menuName  = "Hecton8/Gameplay/Suit Upgrade Data",
        order     = 30)]
    public sealed class SuitUpgradeData : ScriptableObject
    {
        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID апгрейда.")]
        [SerializeField] public string upgradeId;

        [Tooltip("Отображаемое название.")]
        [SerializeField] public string displayName = "АПГРЕЙД СКАФАНДРА";

        [Tooltip("Категория апгрейда.")]
        [SerializeField] public SuitUpgradeCategory category = SuitUpgradeCategory.Hull;

        [Tooltip("Тир (0-4). Апгрейды применяются последовательно.")]
        [SerializeField, Range(0, 4)] public int tier;

        [Header("── Stat Deltas ─────────────────────────────")]
        [Tooltip("Дельта MaxOxygen (добавляется к базовому).")]
        [SerializeField] public float deltaMaxOxygen;

        [Tooltip("Дельта MaxEnergy.")]
        [SerializeField] public float deltaMaxEnergy;

        [Tooltip("Дельта SafeDepth (метры). Положительное = глубже.")]
        [SerializeField] public float deltaSafeDepth;

        [Tooltip("Дельта MaxIntegrity.")]
        [SerializeField] public float deltaMaxIntegrity;

        [Tooltip("Дельта MinSafeTemp (°C). Отрицательное = холоднее.")]
        [SerializeField] public float deltaMinSafeTemp;

        [Tooltip("Дельта MaxSafeTemp (°C). Положительное = горячее.")]
        [SerializeField] public float deltaMaxSafeTemp;

        [Tooltip("Дельта RadiationThreshold.")]
        [SerializeField] public float deltaRadiationThreshold;

        [Header("── Requirements ────────────────────────────")]
        [Tooltip("Необходимые ресурсы для крафта.")]
        [SerializeField] public SuitUpgradeRequirement[] requirements = new SuitUpgradeRequirement[0];

        [Tooltip("ID чертежа (discoveryId) — нужен для разблокировки. Пусто = доступен сразу.")]
        [SerializeField] public string requiredBlueprintId;

        [Header("── Description ─────────────────────────────")]
        [Tooltip("Описание для PDA.")]
        [SerializeField, TextArea(2, 4)] public string description;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                upgradeId = NormalizeId(name);
            else
                upgradeId = NormalizeId(upgradeId);

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name.Replace("_", " ");

            tier = Mathf.Clamp(tier, 0, 4);

            if (requirements == null)
                requirements = new SuitUpgradeRequirement[0];
        }

        private static string NormalizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "suit_upgrade";

            return source.Trim()
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
        }
#endif
    }
}
