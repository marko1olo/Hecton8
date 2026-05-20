using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Tools;
using Unity.Mathematics;
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
        private float _permanentSafeDepthPenaltyMeters;
        private uint _signalSourceId;

        /// <summary>Combined authored plus runtime-installed upgrade bitmask.</summary>
        public uint ActiveUpgradeBitmask => (uint)ActiveUpgradeMask64;

        /// <summary>Combined authored plus runtime-installed upgrade bitmask in SHINOBU_231 64-bit form.</summary>
        public ulong ActiveUpgradeMask64 => ComposeAuthoredBitmask64() | _runtimeInstalledUpgradeMask;

        /// <summary>Additional safe-depth margin supplied by installed hull and pressure upgrades.</summary>
        public float SafeDepthBonusMeters => CompileStats().SafeDepthBonusMeters;

        /// <summary>Permanent safe-depth rating loss accumulated by micro-fracture fatigue.</summary>
        public float PermanentSafeDepthPenaltyMeters => _permanentSafeDepthPenaltyMeters;

        /// <summary>Additional transport integrity supplied by installed armor and damping upgrades.</summary>
        public float MaxIntegrityBonus => CompileStats().MaxIntegrityBonus;

        /// <summary>Multiplier injected into propulsion acceleration by installed thrust upgrades.</summary>
        public float ThrustAccelerationMultiplier => CompileStats().ThrustAccelerationMultiplier;

        /// <summary>Multiplier injected into propulsion speed ceilings by installed drive upgrades.</summary>
        public float MaxSpeedMultiplier => CompileStats().MaxSpeedMultiplier;

        /// <summary>Suit energy-drain multiplier injected by installed efficiency and stealth upgrades.</summary>
        public float EnergyDrainScale => CompileStats().EnergyDrainScale;

        /// <summary>Battery or drive-charge drain multiplier injected by installed power-routing upgrades.</summary>
        public float ChargeDrainScale => CompileStats().ChargeDrainScale;

        /// <summary>Thermal exposure multiplier injected by installed thermal shielding.</summary>
        public float ThermalExposureScale => CompileStats().ThermalExposureScale;

        /// <summary>Pressure damage-transfer multiplier injected by installed pressure compensation.</summary>
        public float PressureDamageScale => CompileStats().PressureDamageScale;

        /// <summary>Returns true when the supplied upgrade flag is active on this transport.</summary>
        public bool HasUpgrade(VehicleUpgradeBits flag)
        {
            return (ActiveUpgradeMask64 & (ulong)flag) != 0UL;
        }

        /// <summary>
        /// Applies persistent safe-depth rating loss caused by entanglement micro-fracture fatigue.
        /// </summary>
        /// <param name="penaltyMeters">Positive depth penalty in meters.</param>
        public void ApplyPermanentSafeDepthPenalty(float penaltyMeters)
        {
            if (penaltyMeters <= 0f)
                return;

            _permanentSafeDepthPenaltyMeters = Mathf.Max(0f, _permanentSafeDepthPenaltyMeters + penaltyMeters);
            PublishUpgradesChanged(VehicleUpgradesChangedSignal.ReasonPenalty);
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
            PublishUpgradesChanged(VehicleUpgradesChangedSignal.ReasonInstall);
            return true;
        }

        private void PublishUpgradesChanged(byte reason)
        {
            if (_signalSourceId == 0u)
                _signalSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));

            VehicleUpgradesChangedSignal signal = new VehicleUpgradesChangedSignal
            {
                SourceId = _signalSourceId,
                UpgradeMask = ActiveUpgradeBitmask,
                Frame = unchecked((uint)Time.frameCount),
                SafeDepthBonusMeters = SafeDepthBonusMeters,
                PermanentSafeDepthPenaltyMeters = _permanentSafeDepthPenaltyMeters,
                Reason = reason,
                Flags = 0
            };

            GlobalSignals.Publish(in signal);
        }

        private ulong ComposeAuthoredBitmask64()
        {
            uint reinforced = (uint)math.select(0, 1, reinforcedHullInstalled);
            uint efficient = (uint)math.select(0, 1, efficientEngineInstalled);
            uint mask = ((uint)VehicleUpgradeBits.ReinforcedHull & (0u - reinforced)) |
                        ((uint)VehicleUpgradeBits.EfficientEngine & (0u - efficient));
            return mask;
        }

        private static VehicleUpgradeBits ResolveUpgradeBit(int itemHashId)
        {
            uint mask =
                SelectBit(itemHashId, _PressureCompensatorHashId, VehicleUpgradeBits.PressureCompensator) |
                SelectBit(itemHashId, _SonarAmplifierHashId, VehicleUpgradeBits.SonarAmplifier) |
                SelectBit(itemHashId, _ThermalShieldingHashId, VehicleUpgradeBits.ThermalShielding) |
                SelectBit(itemHashId, _EngineOverdriveManifoldHashId, VehicleUpgradeBits.EngineOverdrive) |
                SelectBit(itemHashId, _HullArmorLatticeHashId, VehicleUpgradeBits.HullArmorLattice) |
                SelectBit(itemHashId, _ShockMountArrayHashId, VehicleUpgradeBits.ShockMountArray) |
                SelectBit(itemHashId, _BallastOptimizerHashId, VehicleUpgradeBits.BallastOptimizer) |
                SelectBit(itemHashId, _ReactorBypassCouplerHashId, VehicleUpgradeBits.ReactorBypassCoupler) |
                SelectBit(itemHashId, _SilentRunningBaffleHashId, VehicleUpgradeBits.SilentRunningBaffle) |
                SelectBit(itemHashId, _AbyssalStabilizerHashId, VehicleUpgradeBits.AbyssalStabilizer);
            return (VehicleUpgradeBits)mask;
        }

        private VehicleUpgradeCompiledStats CompileStats()
        {
            ulong mask = ActiveUpgradeMask64;
            float reinforced = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.ReinforcedHull);
            float efficient = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.EfficientEngine);
            float pressure = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.PressureCompensator);
            float overdrive = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.EngineOverdrive);
            float armor = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.HullArmorLattice);
            float thermal = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.ThermalShielding);
            float shock = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.ShockMountArray);
            float ballast = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.BallastOptimizer);
            float reactor = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.ReactorBypassCoupler);
            float silent = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.SilentRunningBaffle);
            float stabilizer = UpgradeMatrixCompiler.Bit01(mask, (ulong)VehicleUpgradeBits.AbyssalStabilizer);

            VehicleUpgradeCompiledStats stats = default;
            stats.SafeDepthBonusMeters =
                (math.max(0f, reinforcedHullSafeDepthBonus) * reinforced) +
                (math.max(0f, pressureCompensatorSafeDepthBonus) * pressure) +
                (math.max(0f, pressureCompensatorSafeDepthBonus * 0.5f) * stabilizer) -
                math.max(0f, _permanentSafeDepthPenaltyMeters);
            stats.MaxIntegrityBonus =
                (math.max(0f, reinforcedHullIntegrityBonus) * reinforced) +
                (math.max(0f, hullArmorIntegrityBonus) * armor) +
                (math.max(0f, shockMountIntegrityBonus) * shock);
            stats.ThrustAccelerationMultiplier =
                SelectMultiplier(math.max(1f, engineOverdriveThrustMultiplier), overdrive) *
                SelectMultiplier(math.max(1f, ballastOptimizerThrustMultiplier), ballast) *
                SelectMultiplier(1.04f, stabilizer);
            stats.MaxSpeedMultiplier =
                SelectMultiplier(math.max(1f, engineOverdriveSpeedMultiplier), overdrive) *
                SelectMultiplier(math.max(1f, ballastOptimizerSpeedMultiplier), ballast) *
                SelectMultiplier(1.05f, stabilizer);
            stats.EnergyDrainScale =
                SelectMultiplier(math.max(0.1f, efficientEngineEnergyDrainScale), efficient) *
                SelectMultiplier(math.max(0.1f, silentRunningEnergyDrainScale), silent);
            stats.ChargeDrainScale =
                SelectMultiplier(math.max(0.1f, efficientEngineChargeDrainScale), efficient) *
                SelectMultiplier(math.max(0.1f, reactorBypassChargeDrainScale), reactor);
            stats.ThermalExposureScale = SelectMultiplier(math.max(0.1f, thermalShieldingExposureScale), thermal);
            stats.PressureDamageScale = SelectMultiplier(math.max(0.1f, pressureCompensatorDamageScale), pressure);
            stats.VisualFlags = (uint)((mask & UpgradeMatrixConstants.VisualFlagMask) >> 48);
            stats.StateHash = UpgradeMatrixCompiler.HashMask(mask, _signalSourceId, 0x56454855u);
            return stats;
        }

        private static uint SelectBit(int itemHashId, int expectedHashId, VehicleUpgradeBits bit)
        {
            uint selected = (uint)math.select(0, 1, itemHashId == expectedHashId);
            return (uint)bit & (0u - selected);
        }

        private static float SelectMultiplier(float multiplier, float enabled01)
        {
            return 1f + ((multiplier - 1f) * enabled01);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VehicleUpgradeCompiledStats
        {
            [FieldOffset(0)] public float SafeDepthBonusMeters;
            [FieldOffset(4)] public float MaxIntegrityBonus;
            [FieldOffset(8)] public float ThrustAccelerationMultiplier;
            [FieldOffset(12)] public float MaxSpeedMultiplier;
            [FieldOffset(16)] public float EnergyDrainScale;
            [FieldOffset(20)] public float ChargeDrainScale;
            [FieldOffset(24)] public float ThermalExposureScale;
            [FieldOffset(28)] public float PressureDamageScale;
            [FieldOffset(32)] public uint VisualFlags;
            [FieldOffset(36)] private uint _pad0;
            [FieldOffset(40)] public ulong StateHash;
            [FieldOffset(48)] private ulong _pad1;
            [FieldOffset(56)] private ulong _pad2;
        }
    }
}
