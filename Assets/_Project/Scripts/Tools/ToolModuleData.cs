namespace Hecton8.Tools
{
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Authored compile-time module definition consumed by the modular equipment runtime.
    /// Runtime systems copy these values into blittable structs and never mutate the asset.
    /// </summary>
    [CreateAssetMenu(fileName = "ToolModule_", menuName = "Hecton8/Tools/Tool Module")]
    public sealed class ToolModuleData : ScriptableObject
    {
        [Header("── Identity ─────────────────────────────")]
        [Tooltip("Stable module ID used by saves and diagnostics.")]
        [SerializeField] private string moduleId = "module_standard_battery";

        [Tooltip("Optional inventory item that represents this module in designer-facing content.")]
        [SerializeField] private ItemData linkedItem;

        [Tooltip("Bit flags compiled into the runtime tool-state mask.")]
        [SerializeField] private ToolUpgradeBits upgradeBits = ToolUpgradeBits.None;

        [Header("── Compatibility ────────────────────────")]
        [Tooltip("Categories this module can be installed into. Empty = any tool category.")]
        [SerializeField] private ToolCategory[] compatibleCategories = new ToolCategory[0];

        [Header("── Multipliers ──────────────────────────")]
        [Tooltip("Range multiplier applied to the owning tool.")]
        [SerializeField, Min(0.1f)] private float rangeMultiplier = 1f;

        [Tooltip("Power multiplier applied to the owning tool output.")]
        [SerializeField, Min(0.1f)] private float powerMultiplier = 1f;

        [Tooltip("Efficiency multiplier applied to the owning tool.")]
        [SerializeField, Min(0.1f)] private float efficiencyMultiplier = 1f;

        [Tooltip("Operation-speed multiplier applied to the owning tool.")]
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;

        [Tooltip("Heat-generation multiplier applied while the tool is active.")]
        [SerializeField, Min(0.1f)] private float heatGenerationMultiplier = 1f;

        [Tooltip("Cooldown-rate multiplier applied while the tool vents heat.")]
        [SerializeField, Min(0.1f)] private float cooldownMultiplier = 1f;

        [Tooltip("Battery-capacity multiplier applied to the runtime charge reservoir.")]
        [SerializeField, Min(0.1f)] private float batteryCapacityMultiplier = 1f;

        [Tooltip("Battery-drain multiplier applied to the owning tool.")]
        [SerializeField, Min(0.1f)] private float batteryDrainMultiplier = 1f;

        [Tooltip("Durability-drain multiplier applied to the owning tool.")]
        [SerializeField, Min(0.1f)] private float durabilityDrainMultiplier = 1f;

        [Tooltip("Recoil multiplier applied to player kickback.")]
        [SerializeField, Min(0.1f)] private float recoilMultiplier = 1f;

        public string ModuleId => moduleId;
        public ItemData LinkedItem => linkedItem;
        public ToolUpgradeBits UpgradeBits => upgradeBits;
        public float RangeMultiplier => rangeMultiplier;
        public float PowerMultiplier => powerMultiplier;
        public float EfficiencyMultiplier => efficiencyMultiplier;
        public float SpeedMultiplier => speedMultiplier;
        public float HeatGenerationMultiplier => heatGenerationMultiplier;
        public float CooldownMultiplier => cooldownMultiplier;
        public float BatteryCapacityMultiplier => batteryCapacityMultiplier;
        public float BatteryDrainMultiplier => batteryDrainMultiplier;
        public float DurabilityDrainMultiplier => durabilityDrainMultiplier;
        public float RecoilMultiplier => recoilMultiplier;

        /// <summary>
        /// Returns true when the module can be installed into the supplied tool metadata.
        /// </summary>
        public bool IsCompatibleWith(ToolMetadata metadata)
        {
            if (metadata == null)
                return false;

            if (compatibleCategories == null || compatibleCategories.Length == 0)
                return true;

            ToolCategory category = metadata.category;
            for (int i = 0; i < compatibleCategories.Length; i++)
            {
                if (compatibleCategories[i] == category)
                    return true;
            }

            return false;
        }
    }
}
