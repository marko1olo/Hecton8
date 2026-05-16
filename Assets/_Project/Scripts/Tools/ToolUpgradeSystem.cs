namespace Hecton8.Tools
{
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Stable upgrade-bit ids compiled into the runtime modular-equipment state.
    /// </summary>
    [System.Flags]
    public enum ToolUpgradeBits : uint
    {
        None = 0u,
        RangeBoost = 1u << 0,
        EfficiencyPlus = 1u << 1,
        ThermalOverclock = 1u << 2,
        WirelessCharging = 1u << 3,
        HighCapacityCell = 1u << 4,
        CoolingSink = 1u << 5,
        KineticAccelerator = 1u << 6,
        StandardBattery = 1u << 7,
        ThermalShield = 1u << 8,
        DepthHardened = 1u << 9,
        OxygenRebreather = 1u << 10
    }

    /// <summary>
    /// Runtime equipment status bits mirrored into native SOA storage.
    /// </summary>
    public static class ToolRuntimeStatusMasks
    {
        public const uint Active = 1u << 0;
        public const uint Disabled = 1u << 1;
        public const uint LowPower = 1u << 2;
        public const uint Overheated = 1u << 3;
        public const uint Broken = 1u << 4;
        public const uint DepthFailed = 1u << 5;
        public const uint HeatWarningHapticQueued = 1u << 6;
    }

    /// <summary>
    /// Mutable per-tool runtime state stored in contiguous native memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct ToolState
    {
        public float CurrentBattery;
        public float InternalHeat;
        public float Durability;
        public uint UpgradeBitmask;
        public uint StatusMask;
        public byte ToolTypeId;
        public byte ModuleSlotCount;
        public ushort Reserved0;
        public ulong Reserved1;
    }

    /// <summary>
    /// Cold-path authored profile copied from tool components into the modular runtime.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ToolRuntimeProfile
    {
        public uint ToolId;
        public float MaxRange;
        public float PowerScalar;
        public float EfficiencyScalar;
        public float SpeedScalar;
        public float HeatGenerationRate;
        public float CooldownRate;
        public float BatteryCapacity;
        public float BatteryDrainPerSecond;
        public float DurabilityDrainMultiplier;
        public float RecoilImpulse;
        public byte ModuleSlotCount;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    /// <summary>
    /// Hot-path compiled stats stored beside <see cref="ToolState"/> in native memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ToolRuntimeStats
    {
        public float MaxRange;
        public float PowerScalar;
        public float EfficiencyScalar;
        public float SpeedScalar;
        public float HeatGenerationRate;
        public float CooldownRate;
        public float BatteryCapacity;
        public float BatteryDrainPerSecond;
        public float DurabilityDrainMultiplier;
        public float RecoilImpulse;
    }

    /// <summary>
    /// Cold-path module compiler. Hot paths consume only the compiled bitmask and blittable stats.
    /// </summary>
    public static class ToolUpgradeSystem
    {
        public const int MaxModuleSlots = 4;

        public static bool HasRangeBoost(uint mask) => (mask & (uint)ToolUpgradeBits.RangeBoost) != 0u;
        public static bool HasEfficiencyPlus(uint mask) => (mask & (uint)ToolUpgradeBits.EfficiencyPlus) != 0u;
        public static bool HasThermalOverclock(uint mask) => (mask & (uint)ToolUpgradeBits.ThermalOverclock) != 0u;
        public static bool HasWirelessCharging(uint mask) => (mask & (uint)ToolUpgradeBits.WirelessCharging) != 0u;
        public static bool HasHighCapacityCell(uint mask) => (mask & (uint)ToolUpgradeBits.HighCapacityCell) != 0u;
        public static bool HasCoolingSink(uint mask) => (mask & (uint)ToolUpgradeBits.CoolingSink) != 0u;
        public static bool HasKineticAccelerator(uint mask) => (mask & (uint)ToolUpgradeBits.KineticAccelerator) != 0u;
        public static bool HasThermalShield(uint mask) => (mask & (uint)ToolUpgradeBits.ThermalShield) != 0u;
        public static bool HasDepthHardened(uint mask) => (mask & (uint)ToolUpgradeBits.DepthHardened) != 0u;
        public static bool HasOxygenRebreather(uint mask) => (mask & (uint)ToolUpgradeBits.OxygenRebreather) != 0u;

        /// <summary>
        /// Applies a branchless upgrade bonus to one compiled stat.
        /// </summary>
        public static float ApplyBitBonus(float baseRate, uint upgradeMask, ToolUpgradeBits bit, float bonus)
        {
            float enabled = math.select(0f, 1f, (upgradeMask & (uint)bit) != 0u);
            return baseRate * (1f + bonus * enabled);
        }

        public static bool TryInsertModule(ToolModuleData[] modules, int slotCount, ToolModuleData module)
        {
            if (modules == null || module == null || slotCount <= 0)
                return false;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, modules.Length));
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData existing = modules[i];
                if (existing != null && existing.ModuleId == module.ModuleId)
                    return false;
            }

            for (int i = 0; i < safeSlotCount; i++)
            {
                if (modules[i] != null)
                    continue;

                modules[i] = module;
                return true;
            }

            return false;
        }

        public static bool TryRemoveModule(ToolModuleData[] modules, int slotCount, string moduleId)
        {
            if (modules == null || string.IsNullOrWhiteSpace(moduleId) || slotCount <= 0)
                return false;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, modules.Length));
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData existing = modules[i];
                if (existing == null || existing.ModuleId != moduleId)
                    continue;

                modules[i] = null;
                return true;
            }

            return false;
        }

        public static uint CompileUpgradeMask(ToolModuleData[] modules, int slotCount)
        {
            if (modules == null || slotCount <= 0)
                return 0u;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, modules.Length));
            uint mask = 0u;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData module = modules[i];
                if (module == null)
                    continue;

                mask |= (uint)module.UpgradeBits;
            }

            return mask;
        }

        public static ToolRuntimeStats CompileRuntimeStats(in ToolRuntimeProfile profile, ToolModuleData[] modules, int slotCount, out uint upgradeMask)
        {
            ToolRuntimeStats stats = new ToolRuntimeStats
            {
                MaxRange = math.max(0.1f, profile.MaxRange),
                PowerScalar = math.max(0.1f, profile.PowerScalar),
                EfficiencyScalar = math.max(0.1f, profile.EfficiencyScalar),
                SpeedScalar = math.max(0.1f, profile.SpeedScalar),
                HeatGenerationRate = math.max(0f, profile.HeatGenerationRate),
                CooldownRate = math.max(0f, profile.CooldownRate),
                BatteryCapacity = math.max(0.1f, profile.BatteryCapacity),
                BatteryDrainPerSecond = math.max(0f, profile.BatteryDrainPerSecond),
                DurabilityDrainMultiplier = math.max(0.1f, profile.DurabilityDrainMultiplier),
                RecoilImpulse = math.max(0f, profile.RecoilImpulse)
            };

            if (modules == null || slotCount <= 0)
            {
                upgradeMask = 0u;
                return stats;
            }

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, modules.Length));
            uint mask = 0u;
            float coolingSinkBonus = 0f;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData module = modules[i];
                if (module == null)
                    continue;

                ToolUpgradeBits moduleBits = module.UpgradeBits;
                mask |= (uint)moduleBits;
                stats.MaxRange *= math.max(0.1f, module.RangeMultiplier);
                stats.PowerScalar *= math.max(0.1f, module.PowerMultiplier);
                stats.EfficiencyScalar *= math.max(0.1f, module.EfficiencyMultiplier);
                stats.SpeedScalar *= math.max(0.1f, module.SpeedMultiplier);
                stats.HeatGenerationRate *= math.max(0.1f, module.HeatGenerationMultiplier);
                stats.BatteryCapacity *= math.max(0.1f, module.BatteryCapacityMultiplier);
                stats.BatteryDrainPerSecond *= math.max(0.1f, module.BatteryDrainMultiplier);
                stats.DurabilityDrainMultiplier *= math.max(0.1f, module.DurabilityDrainMultiplier);
                stats.RecoilImpulse *= math.max(0.1f, module.RecoilMultiplier);

                if ((moduleBits & ToolUpgradeBits.CoolingSink) != 0)
                {
                    coolingSinkBonus = math.max(coolingSinkBonus, math.max(0f, module.CooldownMultiplier - 1f));
                }
                else
                {
                    stats.CooldownRate *= math.max(0.1f, module.CooldownMultiplier);
                }
            }

            stats.CooldownRate = ApplyBitBonus(stats.CooldownRate, mask, ToolUpgradeBits.CoolingSink, coolingSinkBonus);
            upgradeMask = mask;
            return stats;
        }
    }
}
