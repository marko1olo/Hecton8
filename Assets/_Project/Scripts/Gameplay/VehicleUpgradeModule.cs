using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Optional sibling modifier that layers vehicle upgrade bonuses onto existing transport owners without replacing them.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Vehicle Upgrade Module")]
    public sealed class VehicleUpgradeModule : MonoBehaviour
    {
        [Header("-- Reinforced Hull --------------------")]
        [Tooltip("Installs the reinforced hull package.")]
        [SerializeField] private bool reinforcedHullInstalled;

        [Tooltip("Additional safe-depth margin granted while this transport is protecting the rider.")]
        [SerializeField, Range(0f, 2000f)] private float reinforcedHullSafeDepthBonus = 140f;

        [Tooltip("Additional transport integrity granted by the reinforced hull package.")]
        [SerializeField, Range(0f, 250f)] private float reinforcedHullIntegrityBonus = 35f;

        [Header("-- Efficient Engine -------------------")]
        [Tooltip("Installs the efficient engine package.")]
        [SerializeField] private bool efficientEngineInstalled;

        [Tooltip("Multiplier applied to rider suit energy drain while the efficient engine package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float efficientEngineEnergyDrainScale = 0.72f;

        [Tooltip("Multiplier applied to local vehicle battery / drive charge drain while the efficient engine package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float efficientEngineChargeDrainScale = 0.7f;

        /// <summary>Additional safe-depth margin supplied by the reinforced hull package.</summary>
        public float SafeDepthBonusMeters => reinforcedHullInstalled ? Mathf.Max(0f, reinforcedHullSafeDepthBonus) : 0f;

        /// <summary>Additional transport integrity supplied by the reinforced hull package.</summary>
        public float MaxIntegrityBonus => reinforcedHullInstalled ? Mathf.Max(0f, reinforcedHullIntegrityBonus) : 0f;

        /// <summary>Suit energy-drain multiplier injected by the efficient engine package.</summary>
        public float EnergyDrainScale => efficientEngineInstalled ? Mathf.Max(0.1f, efficientEngineEnergyDrainScale) : 1f;

        /// <summary>Local battery or drive-charge drain multiplier injected by the efficient engine package.</summary>
        public float ChargeDrainScale => efficientEngineInstalled ? Mathf.Max(0.1f, efficientEngineChargeDrainScale) : 1f;
    }
}
