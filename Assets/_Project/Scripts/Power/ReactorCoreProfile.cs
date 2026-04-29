using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Immutable authoring data for reactor heat, output, and electrolysis side effects.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ReactorCoreProfile",
        menuName = "HECTON-8/Power/Reactor Core Profile",
        order = 140)]
    public sealed class ReactorCoreProfile : ScriptableObject
    {
        [Header("Power Output")]
        [SerializeField, Min(0f)]
        [Tooltip("Nominal continuous output in watts before overload.")]
        private float baseOutputWatts = 750000f;

        [Header("Thermal State")]
        [SerializeField, Min(0f)]
        [Tooltip("Heat energy emitted per second while active, in watts.")]
        private float heatGenerationRateWatts = 250000f;

        [SerializeField, Min(0f)]
        [Tooltip("Compartment temperature in Celsius that triggers irreversible reactor meltdown.")]
        private float meltdownThresholdCelsius = 150f;

        [Header("Electrolysis Spillover")]
        [SerializeField, Min(0f)]
        [Tooltip("Hydrogen pocket units created per kilowatt-second of submerged overload heat.")]
        private float hydrogenUnitsPerKilowattSecond = 0.00004f;

        [SerializeField, Min(0f)]
        [Tooltip("Oxygen pocket units created per kilowatt-second of submerged overload heat.")]
        private float oxygenUnitsPerKilowattSecond = 0.00002f;

        /// <summary>Nominal continuous output in watts.</summary>
        public float BaseOutputWatts => baseOutputWatts;

        /// <summary>Heat energy emitted per second in watts.</summary>
        public float HeatGenerationRateWatts => heatGenerationRateWatts;

        /// <summary>Temperature threshold in Celsius for irreversible meltdown.</summary>
        public float MeltdownThresholdCelsius => meltdownThresholdCelsius;

        /// <summary>Hydrogen pocket generation rate per kilowatt-second.</summary>
        public float HydrogenUnitsPerKilowattSecond => hydrogenUnitsPerKilowattSecond;

        /// <summary>Oxygen pocket generation rate per kilowatt-second.</summary>
        public float OxygenUnitsPerKilowattSecond => oxygenUnitsPerKilowattSecond;

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseOutputWatts = Mathf.Max(0f, baseOutputWatts);
            heatGenerationRateWatts = Mathf.Max(0f, heatGenerationRateWatts);
            meltdownThresholdCelsius = Mathf.Max(0f, meltdownThresholdCelsius);
            hydrogenUnitsPerKilowattSecond = Mathf.Max(0f, hydrogenUnitsPerKilowattSecond);
            oxygenUnitsPerKilowattSecond = Mathf.Max(0f, oxygenUnitsPerKilowattSecond);
        }
#endif
    }
}
