using Hecton.Localization;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Upgrade category for the suit progression stack.
    /// </summary>
    public enum SuitUpgradeCategory
    {
        Hull = 0,
        Oxygen = 1,
        Energy = 2,
        Sensors = 3,
        Thermal = 4,
        Radiation = 5,
    }

    [System.Serializable]
    public struct SuitUpgradeRequirement
    {
        [Tooltip("Item ID from the crafting/item catalog.")]
        public string itemId;

        [Tooltip("Required quantity.")]
        public int quantity;
    }

    [CreateAssetMenu(fileName = "SuitUpgrade_", menuName = "Hecton8/Gameplay/Suit Upgrade Data", order = 30)]
    public sealed class SuitUpgradeData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] public string upgradeId;
        [SerializeField] public string displayName = "SUIT UPGRADE";
        [SerializeField] private LocalizedTextReference localizedDisplayName;
        [SerializeField] public SuitUpgradeCategory category = SuitUpgradeCategory.Hull;
        [SerializeField, Range(0, 4)] public int tier;

        [Header("Stat Deltas")]
        [SerializeField] public float deltaMaxOxygen;
        [SerializeField] public float deltaMaxEnergy;
        [SerializeField] public float deltaSafeDepth;
        [SerializeField] public float deltaMaxIntegrity;
        [SerializeField] public float deltaMinSafeTemp;
        [SerializeField] public float deltaMaxSafeTemp;
        [SerializeField] public float deltaRadiationThreshold;

        [Header("Requirements")]
        [SerializeField] public SuitUpgradeRequirement[] requirements = new SuitUpgradeRequirement[0];
        [SerializeField] public string requiredBlueprintId;

        [Header("Description")]
        [SerializeField, TextArea(2, 4)] public string description;
        [SerializeField] private LocalizedTextReference localizedDescription;

        public string DisplayNameOrFallback => localizedDisplayName.ResolveOrFallback(FallbackOrDefault(displayName, "SUIT UPGRADE"));
        public string DescriptionOrFallback => localizedDescription.ResolveOrFallback(description);

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

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
