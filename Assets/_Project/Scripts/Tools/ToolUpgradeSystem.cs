namespace Hecton8.Tools
{
    using System.Runtime.InteropServices;
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
        StandardBattery = 1u << 7
    }

    /// <summary>
    /// Mutable per-tool runtime state stored in contiguous native memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ToolState
    {
        public float CurrentBattery;
        public float InternalHeat;
        public float Durability;
        public uint UpgradeBitmask;
    }

    /// <summary>
    /// Cold-path authored profile copied from tool components into the modular runtime.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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

        public static bool TryInsertModule(ToolModuleData[] modules, int slotCount, ToolModuleData module)
        {
            if (modules == null || module == null || slotCount <= 0)
                return false;

            int safeSlotCount = Mathf.Clamp(slotCount, 0, Mathf.Min(MaxModuleSlots, modules.Length));
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

            int safeSlotCount = Mathf.Clamp(slotCount, 0, Mathf.Min(MaxModuleSlots, modules.Length));
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

            int safeSlotCount = Mathf.Clamp(slotCount, 0, Mathf.Min(MaxModuleSlots, modules.Length));
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
                MaxRange = Mathf.Max(0.1f, profile.MaxRange),
                PowerScalar = Mathf.Max(0.1f, profile.PowerScalar),
                EfficiencyScalar = Mathf.Max(0.1f, profile.EfficiencyScalar),
                SpeedScalar = Mathf.Max(0.1f, profile.SpeedScalar),
                HeatGenerationRate = Mathf.Max(0f, profile.HeatGenerationRate),
                CooldownRate = Mathf.Max(0f, profile.CooldownRate),
                BatteryCapacity = Mathf.Max(0.1f, profile.BatteryCapacity),
                BatteryDrainPerSecond = Mathf.Max(0f, profile.BatteryDrainPerSecond),
                DurabilityDrainMultiplier = Mathf.Max(0.1f, profile.DurabilityDrainMultiplier),
                RecoilImpulse = Mathf.Max(0f, profile.RecoilImpulse)
            };

            if (modules == null || slotCount <= 0)
            {
                upgradeMask = 0u;
                return stats;
            }

            int safeSlotCount = Mathf.Clamp(slotCount, 0, Mathf.Min(MaxModuleSlots, modules.Length));
            uint mask = 0u;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData module = modules[i];
                if (module == null)
                    continue;

                mask |= (uint)module.UpgradeBits;
                stats.MaxRange *= Mathf.Max(0.1f, module.RangeMultiplier);
                stats.PowerScalar *= Mathf.Max(0.1f, module.PowerMultiplier);
                stats.EfficiencyScalar *= Mathf.Max(0.1f, module.EfficiencyMultiplier);
                stats.SpeedScalar *= Mathf.Max(0.1f, module.SpeedMultiplier);
                stats.HeatGenerationRate *= Mathf.Max(0.1f, module.HeatGenerationMultiplier);
                stats.CooldownRate *= Mathf.Max(0.1f, module.CooldownMultiplier);
                stats.BatteryCapacity *= Mathf.Max(0.1f, module.BatteryCapacityMultiplier);
                stats.BatteryDrainPerSecond *= Mathf.Max(0.1f, module.BatteryDrainMultiplier);
                stats.DurabilityDrainMultiplier *= Mathf.Max(0.1f, module.DurabilityDrainMultiplier);
                stats.RecoilImpulse *= Mathf.Max(0.1f, module.RecoilMultiplier);
            }

            upgradeMask = mask;
            return stats;
        }
    }
}
