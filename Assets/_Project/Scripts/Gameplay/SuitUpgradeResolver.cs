using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    internal static class SuitUpgradeLayout
    {
        internal const int SuitStatsStrideBytes = 64;
    }

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

    [StructLayout(LayoutKind.Explicit, Size = SuitUpgradeLayout.SuitStatsStrideBytes)]
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
            float highCapacity = UpgradeMatrixCompiler.Bit01(upgrades, HighCapacityTank);
            float depth1 = UpgradeMatrixCompiler.Bit01(upgrades, DepthMk1);
            float depth2 = UpgradeMatrixCompiler.Bit01(upgrades, DepthMk2);
            float depth3 = UpgradeMatrixCompiler.Bit01(upgrades, DepthMk3);
            float depth4 = UpgradeMatrixCompiler.Bit01(upgrades, DepthMk4);
            float fins = UpgradeMatrixCompiler.Bit01(upgrades, SwimFins);
            float thermal = UpgradeMatrixCompiler.Bit01(upgrades, ThermalLining);
            float radiation = UpgradeMatrixCompiler.Bit01(upgrades, RadiationScrubber);
            float energy = UpgradeMatrixCompiler.Bit01(upgrades, EnergyCellMk1);
            float generator = UpgradeMatrixCompiler.Bit01(upgrades, ThermalGenerator);

            stats.MaxO2 += (4f * highCapacity) + (4f * depth1) + (11f * depth2) + (21f * depth3) + (41f * depth4);
            stats.CrushDepth += (350f * depth1) + (1350f * depth2) + (3350f * depth3) + (4850f * depth4);
            stats.MaxIntegrity += (5f * depth1) + (15f * depth2) + (30f * depth3) + (50f * depth4);
            stats.ThermalResistance += (0.45f * depth4) + (0.35f * thermal) + (0.1f * generator);
            stats.MinSafeTemperature -= (5f * depth4) + (8f * thermal);
            stats.MaxSafeTemperature += (10f * depth4) + (6f * thermal);
            stats.RadiationThreshold += (10f * depth4) + (4f * radiation);
            stats.SwimSpeedMultiplier += 0.18f * fins;
            stats.MaxEnergy += (50f * energy) + (25f * generator);

            return stats;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NormalizeMask(ulong upgrades)
        {
            upgrades &= SupportedMask;
            ulong nonDepth = upgrades & ~DepthModuleMask;
            ulong b4 = (upgrades >> 4) & 1UL;
            ulong b3 = ((upgrades >> 3) & 1UL) & (b4 ^ 1UL);
            ulong b2 = ((upgrades >> 2) & 1UL) & (b4 ^ 1UL) & (b3 ^ 1UL);
            ulong b1 = ((upgrades >> 1) & 1UL) & (b4 ^ 1UL) & (b3 ^ 1UL) & (b2 ^ 1UL);
            return nonDepth | (b1 * DepthMk1) | (b2 * DepthMk2) | (b3 * DepthMk3) | (b4 * DepthMk4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasAbility(ulong mask, uint abilityHash)
        {
            ulong normalized = NormalizeMask(mask);
            uint sonar = (uint)math.select(0, 1, (normalized & SonarPing) != 0UL);
            uint thermal = (uint)math.select(0, 1, (normalized & ThermalGenerator) != 0UL);
            uint sonarHash = (uint)math.select(0, 1, abilityHash == SonarPingAbilityHash);
            uint thermalHash = (uint)math.select(0, 1, abilityHash == ThermalGeneratorAbilityHash);
            return ((sonar & sonarHash) | (thermal & thermalHash)) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSwimSpeedMultiplier(in SuitStats stats)
        {
            return math.select(1f, stats.SwimSpeedMultiplier, stats.SwimSpeedMultiplier > 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveHullTier(ulong mask)
        {
            ulong normalized = NormalizeMask(mask);
            ulong b1 = (normalized >> 1) & 1UL;
            ulong b2 = (normalized >> 2) & 1UL;
            ulong b3 = (normalized >> 3) & 1UL;
            ulong b4 = (normalized >> 4) & 1UL;
            return (int)(b1 + (2UL * b2) + (3UL * b3) + (4UL * b4));
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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
