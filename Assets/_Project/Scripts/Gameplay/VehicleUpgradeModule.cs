using System;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [Flags]
    public enum VehicleUpgradeBits : uint
    {
        None = 0u,
        ReinforcedHull = 1u << 0,
        EfficientEngine = 1u << 1,
        PressureCompensator = 1u << 2,
        EngineOverdrive = 1u << 3,
        HullArmorLattice = 1u << 4,
        ThermalShielding = 1u << 5,
        SonarAmplifier = 1u << 6,
        ShockMountArray = 1u << 7,
        BallastOptimizer = 1u << 8,
        ReactorBypassCoupler = 1u << 9,
        SilentRunningBaffle = 1u << 10,
        AbyssalStabilizer = 1u << 11
    }

    /// <summary>
    /// Optional sibling modifier that layers bitmask-driven vehicle upgrade bonuses onto transport owners.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Vehicle Upgrade Module")]
    public sealed class VehicleUpgradeModule : MonoBehaviour
    {
        private static readonly int _PressureCompensatorHashId = LocHash.Compute("Comp_PressureCompensator");
        private static readonly int _SonarAmplifierHashId = LocHash.Compute("Comp_SonarAmplifier");
        private static readonly int _ThermalShieldingHashId = LocHash.Compute("Comp_ThermalShielding");
        private static readonly int _EngineOverdriveManifoldHashId = LocHash.Compute("Comp_EngineOverdriveManifold");
        private static readonly int _HullArmorLatticeHashId = LocHash.Compute("Comp_HullArmorLattice");
        private static readonly int _ShockMountArrayHashId = LocHash.Compute("Comp_ShockMountArray");
        private static readonly int _BallastOptimizerHashId = LocHash.Compute("Comp_BallastOptimizer");
        private static readonly int _ReactorBypassCouplerHashId = LocHash.Compute("Comp_ReactorBypassCoupler");
        private static readonly int _SilentRunningBaffleHashId = LocHash.Compute("Comp_SilentRunningBaffle");
        private static readonly int _AbyssalStabilizerHashId = LocHash.Compute("Comp_AbyssalStabilizer");

        [Header("-- Authored Install State ----------")]
        [Tooltip("Legacy authored hull package state. Compiled into the runtime upgrade bitmask.")]
        [SerializeField] private bool reinforcedHullInstalled;

        [Tooltip("Legacy authored efficient-engine package state. Compiled into the runtime upgrade bitmask.")]
        [SerializeField] private bool efficientEngineInstalled;

        [Header("-- Hull / Pressure -----------------")]
        [Tooltip("Additional safe-depth margin granted by the reinforced hull package.")]
        [SerializeField, Range(0f, 2000f)] private float reinforcedHullSafeDepthBonus = 140f;

        [Tooltip("Additional transport integrity granted by the reinforced hull package.")]
        [SerializeField, Range(0f, 250f)] private float reinforcedHullIntegrityBonus = 35f;

        [Tooltip("Additional safe-depth margin granted by the pressure compensator upgrade.")]
        [SerializeField, Range(0f, 3000f)] private float pressureCompensatorSafeDepthBonus = 220f;

        [Tooltip("Additional transport integrity granted by the hull armor lattice upgrade.")]
        [SerializeField, Range(0f, 400f)] private float hullArmorIntegrityBonus = 55f;

        [Tooltip("Additional transport integrity granted by the shock-mount array upgrade.")]
        [SerializeField, Range(0f, 200f)] private float shockMountIntegrityBonus = 22f;

        [Header("-- Propulsion ----------------------")]
        [Tooltip("Multiplier applied to rider suit energy drain while the efficient engine package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float efficientEngineEnergyDrainScale = 0.72f;

        [Tooltip("Multiplier applied to local vehicle battery or drive charge drain while the efficient engine package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float efficientEngineChargeDrainScale = 0.7f;

        [Tooltip("Multiplier applied to thrust acceleration while the engine overdrive package is installed.")]
        [SerializeField, Range(1f, 3f)] private float engineOverdriveThrustMultiplier = 1.18f;

        [Tooltip("Multiplier applied to max speed while the engine overdrive package is installed.")]
        [SerializeField, Range(1f, 3f)] private float engineOverdriveSpeedMultiplier = 1.14f;

        [Tooltip("Multiplier applied to thrust acceleration while the ballast optimizer package is installed.")]
        [SerializeField, Range(1f, 3f)] private float ballastOptimizerThrustMultiplier = 1.08f;

        [Tooltip("Multiplier applied to max speed while the ballast optimizer package is installed.")]
        [SerializeField, Range(1f, 3f)] private float ballastOptimizerSpeedMultiplier = 1.06f;

        [Tooltip("Multiplier applied to battery or drive charge drain while the reactor bypass package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float reactorBypassChargeDrainScale = 0.82f;

        [Tooltip("Multiplier applied to rider suit energy drain while the silent-running baffle package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float silentRunningEnergyDrainScale = 0.88f;

        [Header("-- Environmental Shielding --------")]
        [Tooltip("Multiplier applied to thermal exposure while the thermal shielding package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float thermalShieldingExposureScale = 0.68f;

        [Tooltip("Multiplier applied to pressure damage transfer while the pressure compensator package is installed.")]
        [SerializeField, Range(0.1f, 1f)] private float pressureCompensatorDamageScale = 0.7f;

        private uint _runtimeInstalledUpgradeMask;

        /// <summary>Raised whenever the effective vehicle upgrade bitmask changes.</summary>
        public event Action UpgradesChanged;

        /// <summary>Combined authored plus runtime-installed upgrade bitmask.</summary>
        public uint ActiveUpgradeBitmask => ComposeAuthoredBitmask() | _runtimeInstalledUpgradeMask;

        /// <summary>Additional safe-depth margin supplied by installed hull and pressure upgrades.</summary>
        public float SafeDepthBonusMeters
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float bonus = 0f;
                if ((mask & (uint)VehicleUpgradeBits.ReinforcedHull) != 0u)
                    bonus += Mathf.Max(0f, reinforcedHullSafeDepthBonus);
                if ((mask & (uint)VehicleUpgradeBits.PressureCompensator) != 0u)
                    bonus += Mathf.Max(0f, pressureCompensatorSafeDepthBonus);
                if ((mask & (uint)VehicleUpgradeBits.AbyssalStabilizer) != 0u)
                    bonus += Mathf.Max(0f, pressureCompensatorSafeDepthBonus * 0.5f);
                return bonus;
            }
        }

        /// <summary>Additional transport integrity supplied by installed armor and damping upgrades.</summary>
        public float MaxIntegrityBonus
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float bonus = 0f;
                if ((mask & (uint)VehicleUpgradeBits.ReinforcedHull) != 0u)
                    bonus += Mathf.Max(0f, reinforcedHullIntegrityBonus);
                if ((mask & (uint)VehicleUpgradeBits.HullArmorLattice) != 0u)
                    bonus += Mathf.Max(0f, hullArmorIntegrityBonus);
                if ((mask & (uint)VehicleUpgradeBits.ShockMountArray) != 0u)
                    bonus += Mathf.Max(0f, shockMountIntegrityBonus);
                return bonus;
            }
        }

        /// <summary>Multiplier injected into propulsion acceleration by installed thrust upgrades.</summary>
        public float ThrustAccelerationMultiplier
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float multiplier = 1f;
                if ((mask & (uint)VehicleUpgradeBits.EngineOverdrive) != 0u)
                    multiplier *= Mathf.Max(1f, engineOverdriveThrustMultiplier);
                if ((mask & (uint)VehicleUpgradeBits.BallastOptimizer) != 0u)
                    multiplier *= Mathf.Max(1f, ballastOptimizerThrustMultiplier);
                if ((mask & (uint)VehicleUpgradeBits.AbyssalStabilizer) != 0u)
                    multiplier *= 1.04f;
                return multiplier;
            }
        }

        /// <summary>Multiplier injected into propulsion speed ceilings by installed drive upgrades.</summary>
        public float MaxSpeedMultiplier
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float multiplier = 1f;
                if ((mask & (uint)VehicleUpgradeBits.EngineOverdrive) != 0u)
                    multiplier *= Mathf.Max(1f, engineOverdriveSpeedMultiplier);
                if ((mask & (uint)VehicleUpgradeBits.BallastOptimizer) != 0u)
                    multiplier *= Mathf.Max(1f, ballastOptimizerSpeedMultiplier);
                if ((mask & (uint)VehicleUpgradeBits.AbyssalStabilizer) != 0u)
                    multiplier *= 1.05f;
                return multiplier;
            }
        }

        /// <summary>Suit energy-drain multiplier injected by installed efficiency and stealth upgrades.</summary>
        public float EnergyDrainScale
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float scale = 1f;
                if ((mask & (uint)VehicleUpgradeBits.EfficientEngine) != 0u)
                    scale *= Mathf.Max(0.1f, efficientEngineEnergyDrainScale);
                if ((mask & (uint)VehicleUpgradeBits.SilentRunningBaffle) != 0u)
                    scale *= Mathf.Max(0.1f, silentRunningEnergyDrainScale);
                return scale;
            }
        }

        /// <summary>Battery or drive-charge drain multiplier injected by installed power-routing upgrades.</summary>
        public float ChargeDrainScale
        {
            get
            {
                uint mask = ActiveUpgradeBitmask;
                float scale = 1f;
                if ((mask & (uint)VehicleUpgradeBits.EfficientEngine) != 0u)
                    scale *= Mathf.Max(0.1f, efficientEngineChargeDrainScale);
                if ((mask & (uint)VehicleUpgradeBits.ReactorBypassCoupler) != 0u)
                    scale *= Mathf.Max(0.1f, reactorBypassChargeDrainScale);
                return scale;
            }
        }

        /// <summary>Thermal exposure multiplier injected by installed thermal shielding.</summary>
        public float ThermalExposureScale => HasUpgrade(VehicleUpgradeBits.ThermalShielding)
            ? Mathf.Max(0.1f, thermalShieldingExposureScale)
            : 1f;

        /// <summary>Pressure damage-transfer multiplier injected by installed pressure compensation.</summary>
        public float PressureDamageScale => HasUpgrade(VehicleUpgradeBits.PressureCompensator)
            ? Mathf.Max(0.1f, pressureCompensatorDamageScale)
            : 1f;

        /// <summary>Returns true when the supplied upgrade flag is active on this transport.</summary>
        public bool HasUpgrade(VehicleUpgradeBits flag)
        {
            return (ActiveUpgradeBitmask & (uint)flag) != 0u;
        }

        /// <summary>
        /// Attempts to install a crafted upgrade component by its shared item hash.
        /// </summary>
        public bool TryInstallUpgrade(int itemHashId)
        {
            VehicleUpgradeBits bit = ResolveUpgradeBit(itemHashId);
            if (bit == VehicleUpgradeBits.None)
                return false;

            uint bitMask = (uint)bit;
            if ((_runtimeInstalledUpgradeMask & bitMask) != 0u)
                return false;

            _runtimeInstalledUpgradeMask |= bitMask;
            UpgradesChanged?.Invoke();
            return true;
        }

        private uint ComposeAuthoredBitmask()
        {
            uint mask = 0u;
            if (reinforcedHullInstalled)
                mask |= (uint)VehicleUpgradeBits.ReinforcedHull;
            if (efficientEngineInstalled)
                mask |= (uint)VehicleUpgradeBits.EfficientEngine;
            return mask;
        }

        private static VehicleUpgradeBits ResolveUpgradeBit(int itemHashId)
        {
            if (itemHashId == _PressureCompensatorHashId)
                return VehicleUpgradeBits.PressureCompensator;
            if (itemHashId == _SonarAmplifierHashId)
                return VehicleUpgradeBits.SonarAmplifier;
            if (itemHashId == _ThermalShieldingHashId)
                return VehicleUpgradeBits.ThermalShielding;
            if (itemHashId == _EngineOverdriveManifoldHashId)
                return VehicleUpgradeBits.EngineOverdrive;
            if (itemHashId == _HullArmorLatticeHashId)
                return VehicleUpgradeBits.HullArmorLattice;
            if (itemHashId == _ShockMountArrayHashId)
                return VehicleUpgradeBits.ShockMountArray;
            if (itemHashId == _BallastOptimizerHashId)
                return VehicleUpgradeBits.BallastOptimizer;
            if (itemHashId == _ReactorBypassCouplerHashId)
                return VehicleUpgradeBits.ReactorBypassCoupler;
            if (itemHashId == _SilentRunningBaffleHashId)
                return VehicleUpgradeBits.SilentRunningBaffle;
            if (itemHashId == _AbyssalStabilizerHashId)
                return VehicleUpgradeBits.AbyssalStabilizer;

            return VehicleUpgradeBits.None;
        }
    }
}
