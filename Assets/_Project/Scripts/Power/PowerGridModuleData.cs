using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Authoritative authored template for grid-facing module defaults.
    /// </summary>
    [System.Serializable]
    public struct PowerGridModuleData
    {
        [Tooltip("Base throughput or source capacity contributed by this module, in watts.")]
        [Min(0f)] public float baseCapacityWatts;

        [Tooltip("Base network resistance injected by this module or connection template.")]
        [Min(0.0001f)] public float baseResistance;

        [Tooltip("Default power-priority lane used when the module is instantiated into a grid node.")]
        [Range(0, 100)] public int defaultPriority;
    }
}
