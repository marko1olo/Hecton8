using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Gameplay
{
    [System.Flags]
    public enum SuitUpgrades : ulong
    {
        None = 0UL,
        HighCapacityTank = 1UL << 0,
        DepthModuleMk1 = 1UL << 1,
        DepthModuleMk2 = 1UL << 2,
        DepthModuleMk3 = 1UL << 3,
        DepthModuleMk4 = 1UL << 4,
        SwimFins = 1UL << 5,
        ThermalLining = 1UL << 6,
        RadiationScrubber = 1UL << 7,
        EnergyCellMk1 = 1UL << 8,
        SonarPing = 1UL << 9,
        ThermalGenerator = 1UL << 10
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SuitStats
    {
        [FieldOffset(0)] public float MaxO2;
        [FieldOffset(4)] public float CrushDepth;
        [FieldOffset(8)] public float SwimSpeedMultiplier;
        [FieldOffset(12)] public float ThermalResistance;
        [FieldOffset(16)] public float MaxEnergy;
        [FieldOffset(20)] public float MaxIntegrity;
        [FieldOffset(24)] public float MinSafeTemperature;
        [FieldOffset(28)] public float MaxSafeTemperature;
        [FieldOffset(32)] public float RadiationThreshold;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    public static class SuitUpgradeResolver
    {
        public const ulong HighCapacityTank = (ulong)SuitUpgrades.HighCapacityTank;
        public const ulong DepthMk1 = (ulong)SuitUpgrades.DepthModuleMk1;
        public const ulong DepthMk2 = (ulong)SuitUpgrades.DepthModuleMk2;
        public const ulong DepthMk3 = (ulong)SuitUpgrades.DepthModuleMk3;
        public const ulong DepthMk4 = (ulong)SuitUpgrades.DepthModuleMk4;
        public const ulong SwimFins = (ulong)SuitUpgrades.SwimFins;
        public const ulong ThermalLining = (ulong)SuitUpgrades.ThermalLining;
        public const ulong RadiationScrubber = (ulong)SuitUpgrades.RadiationScrubber;
        public const ulong EnergyCellMk1 = (ulong)SuitUpgrades.EnergyCellMk1;
        public const ulong SonarPing = (ulong)SuitUpgrades.SonarPing;
        public const ulong ThermalGenerator = (ulong)SuitUpgrades.ThermalGenerator;
        public const ulong DepthModuleMask = DepthMk1 | DepthMk2 | DepthMk3 | DepthMk4;
        public const ulong SupportedMask =
            HighCapacityTank |
            DepthMk1 |
            DepthMk2 |
            DepthMk3 |
            DepthMk4 |
            SwimFins |
            ThermalLining |
            RadiationScrubber |
            EnergyCellMk1 |
            SonarPing |
            ThermalGenerator;

        public const uint SonarPingAbilityHash = 0x534F4E52u;
        public const uint ThermalGeneratorAbilityHash = 0x54484745u;

        public static int SuitStatsSizeBytes
        {
            get
            {
                RequireUnmanaged<SuitStats>();
                return UnsafeUtility.SizeOf<SuitStats>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SuitStats Resolve(ulong upgrades)
        {
            SuitStats baseline = CreateBaseline();
            return Resolve(upgrades, in baseline);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SuitStats Resolve(ulong upgrades, in SuitStats baseline)
        {
            upgrades = NormalizeMask(upgrades);

            SuitStats stats = baseline;

            if ((upgrades & HighCapacityTank) != 0UL)
                stats.MaxO2 += 4f;

            if ((upgrades & DepthMk1) != 0UL)
            {
                stats.MaxO2 += 4f;
                stats.CrushDepth += 350f;
                stats.MaxIntegrity += 5f;
            }

            if ((upgrades & DepthMk2) != 0UL)
            {
                stats.MaxO2 += 11f;
                stats.CrushDepth += 1350f;
                stats.MaxIntegrity += 15f;
            }

            if ((upgrades & DepthMk3) != 0UL)
            {
                stats.MaxO2 += 21f;
                stats.CrushDepth += 3350f;
                stats.MaxIntegrity += 30f;
            }

            if ((upgrades & DepthMk4) != 0UL)
            {
                stats.MaxO2 += 41f;
                stats.CrushDepth += 4850f;
                stats.MaxIntegrity += 50f;
                stats.ThermalResistance += 0.45f;
                stats.MinSafeTemperature -= 5f;
                stats.MaxSafeTemperature += 10f;
                stats.RadiationThreshold += 10f;
            }

            if ((upgrades & SwimFins) != 0UL)
                stats.SwimSpeedMultiplier += 0.18f;

            if ((upgrades & ThermalLining) != 0UL)
            {
                stats.ThermalResistance += 0.35f;
                stats.MinSafeTemperature -= 8f;
                stats.MaxSafeTemperature += 6f;
            }

            if ((upgrades & RadiationScrubber) != 0UL)
                stats.RadiationThreshold += 4f;

            if ((upgrades & EnergyCellMk1) != 0UL)
                stats.MaxEnergy += 50f;

            if ((upgrades & ThermalGenerator) != 0UL)
            {
                stats.MaxEnergy += 25f;
                stats.ThermalResistance += 0.1f;
            }

            return stats;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NormalizeMask(ulong upgrades)
        {
            upgrades &= SupportedMask;

            if ((upgrades & DepthMk4) != 0UL)
                upgrades &= ~(DepthMk1 | DepthMk2 | DepthMk3);
            else if ((upgrades & DepthMk3) != 0UL)
                upgrades &= ~(DepthMk1 | DepthMk2);
            else if ((upgrades & DepthMk2) != 0UL)
                upgrades &= ~DepthMk1;

            return upgrades;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAbility(ulong mask, uint abilityHash)
        {
            ulong normalized = NormalizeMask(mask);
            return ((normalized & SonarPing) != 0UL && abilityHash == SonarPingAbilityHash) ||
                   ((normalized & ThermalGenerator) != 0UL && abilityHash == ThermalGeneratorAbilityHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSwimSpeedMultiplier(in SuitStats stats)
        {
            return stats.SwimSpeedMultiplier > 0f ? stats.SwimSpeedMultiplier : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SuitStats CreateBaseline()
        {
            return new SuitStats
            {
                MaxO2 = 100f,
                CrushDepth = 50f,
                SwimSpeedMultiplier = 1f,
                ThermalResistance = 0f,
                MaxEnergy = 200f,
                MaxIntegrity = 100f,
                MinSafeTemperature = -5f,
                MaxSafeTemperature = 45f,
                RadiationThreshold = 0.5f
            };
        }

        public static SuitUpgrades ResolveUpgradeBit(SuitUpgradeData upgrade)
        {
            if (upgrade == null)
                return SuitUpgrades.None;

            return ResolveUpgradeBit(upgrade.upgradeId, upgrade.category, upgrade.tier);
        }

        public static SuitUpgrades ResolveUpgradeBit(string upgradeId, SuitUpgradeCategory category, int tier)
        {
            switch (upgradeId)
            {
                case "suit_oxygen_t1_aux_reservoir":
                    return SuitUpgrades.HighCapacityTank;
                case "suit_hull_t1_pressure_shell":
                    return SuitUpgrades.DepthModuleMk1;
                case "suit_hull_t2_pressure_lattice":
                    return SuitUpgrades.DepthModuleMk2;
                case "suit_hull_t3_abyssal_frame":
                    return SuitUpgrades.DepthModuleMk3;
                case "suit_hull_t4_thermal_shell":
                    return SuitUpgrades.DepthModuleMk4;
                case "suit_energy_t1_aux_cell":
                    return SuitUpgrades.EnergyCellMk1;
                case "suit_sensor_t1_sonar_ping":
                    return SuitUpgrades.SonarPing;
                case "suit_thermal_t1_lining":
                    return SuitUpgrades.ThermalLining;
                case "suit_thermal_t2_generator":
                    return SuitUpgrades.ThermalGenerator;
                case "suit_radiation_t1_scrubber":
                    return SuitUpgrades.RadiationScrubber;
            }

            if (category == SuitUpgradeCategory.Hull)
            {
                switch (tier)
                {
                    case 1:
                        return SuitUpgrades.DepthModuleMk1;
                    case 2:
                        return SuitUpgrades.DepthModuleMk2;
                    case 3:
                        return SuitUpgrades.DepthModuleMk3;
                    case 4:
                        return SuitUpgrades.DepthModuleMk4;
                }
            }

            if (category == SuitUpgradeCategory.Oxygen && tier == 1)
                return SuitUpgrades.HighCapacityTank;
            if (category == SuitUpgradeCategory.Energy && tier == 1)
                return SuitUpgrades.EnergyCellMk1;
            if (category == SuitUpgradeCategory.Sensors && tier == 1)
                return SuitUpgrades.SonarPing;
            if (category == SuitUpgradeCategory.Thermal)
                return tier >= 2 ? SuitUpgrades.ThermalGenerator : SuitUpgrades.ThermalLining;
            if (category == SuitUpgradeCategory.Radiation && tier == 1)
                return SuitUpgrades.RadiationScrubber;

            return SuitUpgrades.None;
        }

        private static void RequireUnmanaged<T>()
            where T : unmanaged
        {
        }
    }

    [BurstCompile]
    public struct SuitUpgradeResolverJob : IJob
    {
        public ulong Upgrades;
        public SuitStats Baseline;
        [WriteOnly] public NativeSlice<SuitStats> Result;

        public void Execute()
        {
            Result[0] = SuitUpgradeResolver.Resolve(Upgrades, in Baseline);
        }
    }
}
