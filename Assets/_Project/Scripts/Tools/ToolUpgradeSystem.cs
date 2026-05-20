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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolState
    {
        [FieldOffset(0)] public float CurrentBattery;
        [FieldOffset(4)] public float InternalHeat;
        [FieldOffset(8)] public float Durability;
        [FieldOffset(12)] public uint UpgradeBitmask;
        [FieldOffset(16)] public uint StatusMask;
        [FieldOffset(20)] public byte ToolTypeId;
        [FieldOffset(21)] public byte ModuleSlotCount;
        [FieldOffset(22)] public ushort Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }

    /// <summary>
    /// Cold-path authored profile copied from tool components into the modular runtime.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ToolRuntimeProfile
    {
        [FieldOffset(0)] public uint ToolId;
        [FieldOffset(4)] public float MaxRange;
        [FieldOffset(8)] public float PowerScalar;
        [FieldOffset(12)] public float EfficiencyScalar;
        [FieldOffset(16)] public float SpeedScalar;
        [FieldOffset(20)] public float HeatGenerationRate;
        [FieldOffset(24)] public float CooldownRate;
        [FieldOffset(28)] public float BatteryCapacity;
        [FieldOffset(32)] public float BatteryDrainPerSecond;
        [FieldOffset(36)] public float DurabilityDrainMultiplier;
        [FieldOffset(40)] public float RecoilImpulse;
        [FieldOffset(44)] public byte ModuleSlotCount;
        [FieldOffset(45)] public byte Reserved0;
        [FieldOffset(46)] public byte Reserved1;
        [FieldOffset(47)] public byte Reserved2;
    }

    /// <summary>
    /// Hot-path compiled stats stored beside <see cref="ToolState"/> in native memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ToolRuntimeStats
    {
        [FieldOffset(0)] public float MaxRange;
        [FieldOffset(4)] public float PowerScalar;
        [FieldOffset(8)] public float EfficiencyScalar;
        [FieldOffset(12)] public float SpeedScalar;
        [FieldOffset(16)] public float HeatGenerationRate;
        [FieldOffset(20)] public float CooldownRate;
        [FieldOffset(24)] public float BatteryCapacity;
        [FieldOffset(28)] public float BatteryDrainPerSecond;
        [FieldOffset(32)] public float DurabilityDrainMultiplier;
        [FieldOffset(36)] public float RecoilImpulse;
    }

    /// <summary>
    /// Cold-path module compiler. Hot paths consume only the compiled bitmask and blittable stats.
    /// </summary>
    public static class ToolUpgradeSystem
    {
        public const int MaxModuleSlots = 4;
        public const ulong SupportedMask64 =
            (ulong)ToolUpgradeBits.RangeBoost |
            (ulong)ToolUpgradeBits.EfficiencyPlus |
            (ulong)ToolUpgradeBits.ThermalOverclock |
            (ulong)ToolUpgradeBits.WirelessCharging |
            (ulong)ToolUpgradeBits.HighCapacityCell |
            (ulong)ToolUpgradeBits.CoolingSink |
            (ulong)ToolUpgradeBits.KineticAccelerator |
            (ulong)ToolUpgradeBits.StandardBattery |
            (ulong)ToolUpgradeBits.ThermalShield |
            (ulong)ToolUpgradeBits.DepthHardened |
            (ulong)ToolUpgradeBits.OxygenRebreather;

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

        /// <summary>
        /// Applies a branchless 64-bit upgrade bonus to one compiled stat.
        /// </summary>
        public static float ApplyBitBonus64(float baseRate, ulong upgradeMask, ToolUpgradeBits bit, float bonus)
        {
            float enabled = UpgradeMatrixCompiler.Bit01(upgradeMask, (ulong)bit);
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
            return (uint)CompileUpgradeMask64(modules, slotCount);
        }

        public static ulong CompileUpgradeMask64(ToolModuleData[] modules, int slotCount)
        {
            if (modules == null || slotCount <= 0)
                return 0UL;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, modules.Length));
            ulong mask = 0UL;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData module = modules[i];
                if (module == null)
                    continue;

                mask |= (ulong)module.UpgradeBits;
            }

            return mask & SupportedMask64;
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
            ulong mask = 0UL;
            float coolingSinkBonus = 0f;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolModuleData module = modules[i];
                if (module == null)
                    continue;

                ToolUpgradeBits moduleBits = module.UpgradeBits;
                mask |= (ulong)moduleBits;
                stats.MaxRange *= math.max(0.1f, module.RangeMultiplier);
                stats.PowerScalar *= math.max(0.1f, module.PowerMultiplier);
                stats.EfficiencyScalar *= math.max(0.1f, module.EfficiencyMultiplier);
                stats.SpeedScalar *= math.max(0.1f, module.SpeedMultiplier);
                stats.HeatGenerationRate *= math.max(0.1f, module.HeatGenerationMultiplier);
                stats.BatteryCapacity *= math.max(0.1f, module.BatteryCapacityMultiplier);
                stats.BatteryDrainPerSecond *= math.max(0.1f, module.BatteryDrainMultiplier);
                stats.DurabilityDrainMultiplier *= math.max(0.1f, module.DurabilityDrainMultiplier);
                stats.RecoilImpulse *= math.max(0.1f, module.RecoilMultiplier);

                float cooldownMultiplier = math.max(0.1f, module.CooldownMultiplier);
                float coolingSink = UpgradeMatrixCompiler.Bit01((ulong)moduleBits, (ulong)ToolUpgradeBits.CoolingSink);
                coolingSinkBonus = math.max(coolingSinkBonus, math.max(0f, cooldownMultiplier - 1f) * coolingSink);
                stats.CooldownRate *= math.select(cooldownMultiplier, 1f, coolingSink > 0f);
            }

            stats.CooldownRate = ApplyBitBonus64(stats.CooldownRate, mask, ToolUpgradeBits.CoolingSink, coolingSinkBonus);
            upgradeMask = (uint)(mask & SupportedMask64);
            return stats;
        }
    }
}
