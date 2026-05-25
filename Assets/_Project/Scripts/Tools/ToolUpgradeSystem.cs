namespace Hecton8.Tools
{
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    using UnityEngine;

    internal static class ToolUpgradeSystemLayout
    {
        public const int ToolStateStrideBytes = 32;
        public const int ToolRuntimeProfileStrideBytes = 48;
        public const int ToolRuntimeStatsStrideBytes = 40;
    }

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
    [StructLayout(LayoutKind.Explicit, Size = ToolUpgradeSystemLayout.ToolStateStrideBytes)]
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
        [FieldOffset(24)] public ulong UpgradeBitmask64;
    }

    /// <summary>
    /// Cold-path authored profile copied from tool components into the modular runtime.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ToolUpgradeSystemLayout.ToolRuntimeProfileStrideBytes)]
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
    [StructLayout(LayoutKind.Explicit, Size = ToolUpgradeSystemLayout.ToolRuntimeStatsStrideBytes)]
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

        public static ToolUpgradeModuleRuleDTO BuildModuleRule(
            ToolModuleData module,
            int slotIndex,
            uint entityHashId = 0u,
            uint equipmentHashId = 0u)
        {
            if (module == null)
                return default;

            int safeSlot = math.clamp(slotIndex, 0, MaxModuleSlots - 1);
            ulong upgradeBit = (ulong)module.UpgradeBits & SupportedMask64;
            uint moduleHashId = equipmentHashId == 0u ? HashModuleId(module.ModuleId) : equipmentHashId;
            ulong stateHash = UpgradeMatrixCompiler.HashMask(upgradeBit, entityHashId, moduleHashId);
            stateHash = UpgradeMatrixCompiler.Mix(stateHash, (uint)safeSlot);
            return new ToolUpgradeModuleRuleDTO
            {
                UpgradeBit = upgradeBit,
                StateHash = stateHash,
                EntityHashID = entityHashId,
                EquipmentHashID = moduleHashId,
                CompressedBit = 1u << safeSlot,
                VisualFlags = (uint)((upgradeBit & UpgradeMatrixConstants.VisualFlagMask) >> 48),
                RangeMultiplier = math.max(0.1f, module.RangeMultiplier),
                PowerMultiplier = math.max(0.1f, module.PowerMultiplier),
                EfficiencyMultiplier = math.max(0.1f, module.EfficiencyMultiplier),
                SpeedMultiplier = math.max(0.1f, module.SpeedMultiplier),
                HeatGenerationMultiplier = math.max(0.1f, module.HeatGenerationMultiplier),
                CooldownMultiplier = math.max(0.1f, module.CooldownMultiplier),
                BatteryCapacityMultiplier = math.max(0.1f, module.BatteryCapacityMultiplier),
                BatteryDrainMultiplier = math.max(0.1f, module.BatteryDrainMultiplier),
                DurabilityDrainMultiplier = math.max(0.1f, module.DurabilityDrainMultiplier),
                RecoilMultiplier = math.max(0.1f, module.RecoilMultiplier),
                Occupied = 1,
                SlotIndex = (byte)safeSlot
            };
        }

        public static uint HashModuleId(string moduleId)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(moduleId))
                return hash;

            for (int i = 0; i < moduleId.Length; i++)
                hash = (hash ^ moduleId[i]) * 16777619u;
            return hash == 0u ? 2166136261u : hash;
        }

        public static ToolRuntimeStats CompileRuntimeStatsFromLut(in ToolRuntimeProfile profile, in UpgradeStatVectorDTO stats)
        {
            return new ToolRuntimeStats
            {
                MaxRange = math.max(0.1f, profile.MaxRange) * math.max(0.0001f, stats.Stat0),
                PowerScalar = math.max(0.1f, profile.PowerScalar) * math.max(0.0001f, stats.Stat1),
                EfficiencyScalar = math.max(0.1f, profile.EfficiencyScalar) * math.max(0.0001f, stats.Stat2),
                SpeedScalar = math.max(0.1f, profile.SpeedScalar) * math.max(0.0001f, stats.Stat3),
                HeatGenerationRate = math.max(0f, profile.HeatGenerationRate) * math.max(0.0001f, stats.Stat4),
                CooldownRate = math.max(0f, profile.CooldownRate) * math.max(0.0001f, stats.Stat5),
                BatteryCapacity = math.max(0.1f, profile.BatteryCapacity) * math.max(0.0001f, stats.Stat6),
                BatteryDrainPerSecond = math.max(0f, profile.BatteryDrainPerSecond) * math.max(0.0001f, stats.Stat7),
                DurabilityDrainMultiplier = math.max(0.1f, profile.DurabilityDrainMultiplier) * math.max(0.0001f, stats.Stat8),
                RecoilImpulse = math.max(0f, profile.RecoilImpulse) * math.max(0.0001f, stats.Stat9)
            };
        }

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

        public static ulong CompileInstalledRuleMask64(ToolUpgradeModuleRuleDTO[] rules, int slotCount, out ulong originalUpgradeMask)
        {
            originalUpgradeMask = 0UL;
            if (rules == null || slotCount <= 0)
                return 0UL;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, rules.Length));
            ulong slotMask = 0UL;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolUpgradeModuleRuleDTO rule = rules[i];
                uint occupied = (uint)math.select(0, 1, rule.Occupied != 0);
                ulong occupiedMask = 0UL - (ulong)occupied;
                originalUpgradeMask |= rule.UpgradeBit & occupiedMask;
                slotMask |= (ulong)occupied << i;
            }

            return slotMask;
        }

        public static bool TryInsertModuleRule(ToolUpgradeModuleRuleDTO[] rules, int slotCount, ToolUpgradeModuleRuleDTO moduleRule)
        {
            if (rules == null || slotCount <= 0 || moduleRule.Occupied == 0)
                return false;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, rules.Length));
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolUpgradeModuleRuleDTO existing = rules[i];
                if (existing.Occupied == 0)
                    continue;

                if (existing.EquipmentHashID == moduleRule.EquipmentHashID)
                    return false;
            }

            for (int i = 0; i < safeSlotCount; i++)
            {
                if (rules[i].Occupied != 0)
                    continue;

                rules[i] = NormalizeRuleSlot(moduleRule, i);
                return true;
            }

            return false;
        }

        public static bool TryRemoveModuleRule(ToolUpgradeModuleRuleDTO[] rules, int slotCount, uint moduleHashId)
        {
            if (rules == null || moduleHashId == 0u || slotCount <= 0)
                return false;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, rules.Length));
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolUpgradeModuleRuleDTO existing = rules[i];
                if (existing.Occupied == 0 || existing.EquipmentHashID != moduleHashId)
                    continue;

                rules[i] = default;
                return true;
            }

            return false;
        }

        public static uint CompileUpgradeMask(ToolUpgradeModuleRuleDTO[] rules, int slotCount)
        {
            return (uint)CompileUpgradeMask64(rules, slotCount);
        }

        public static ulong CompileUpgradeMask64(ToolUpgradeModuleRuleDTO[] rules, int slotCount)
        {
            if (rules == null || slotCount <= 0)
                return 0UL;

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, rules.Length));
            ulong mask = 0UL;
            for (int i = 0; i < safeSlotCount; i++)
            {
                ToolUpgradeModuleRuleDTO rule = rules[i];
                ulong occupiedMask = 0UL - (ulong)math.select(0, 1, rule.Occupied != 0);
                mask |= rule.UpgradeBit & occupiedMask;
            }

            return mask & SupportedMask64;
        }

        public static ToolRuntimeStats CompileRuntimeStatsFromRules(in ToolRuntimeProfile profile, ToolUpgradeModuleRuleDTO[] rules, int slotCount, out uint upgradeMask)
        {
            ToolRuntimeStats stats = CompileRuntimeStatsFromRules64(profile, rules, slotCount, out ulong upgradeMask64);
            upgradeMask = (uint)(upgradeMask64 & 0xFFFFFFFFUL);
            return stats;
        }

        public static ToolRuntimeStats CompileRuntimeStatsFromRules64(in ToolRuntimeProfile profile, ToolUpgradeModuleRuleDTO[] rules, int slotCount, out ulong upgradeMask)
        {
            UpgradeStatVectorDTO baseline = CreateIdentityStatVector(UpgradeMatrixCompiler.HashMask(0UL, profile.ToolId, 0u));
            if (rules == null || slotCount <= 0)
            {
                upgradeMask = 0UL;
                return CompileRuntimeStatsFromLut(profile, in baseline);
            }

            int safeSlotCount = math.clamp(slotCount, 0, math.min(MaxModuleSlots, rules.Length));
            ulong slotMask = CompileInstalledRuleMask64(rules, safeSlotCount, out ulong originalMask);
            ToolUpgradeModuleRuleDTO rule0 = ReadRuleAt(rules, safeSlotCount, 0);
            ToolUpgradeModuleRuleDTO rule1 = ReadRuleAt(rules, safeSlotCount, 1);
            ToolUpgradeModuleRuleDTO rule2 = ReadRuleAt(rules, safeSlotCount, 2);
            ToolUpgradeModuleRuleDTO rule3 = ReadRuleAt(rules, safeSlotCount, 3);
            uint lutIndex = (uint)(slotMask & ((1UL << MaxModuleSlots) - 1UL));
            UpgradeLutEntryDTO lut = UpgradeMatrixCompiler.CreateIdentityLutEntry(lutIndex);
            float coolingSinkBonus = 0f;
            UpgradeMatrixCompiler.ApplyToolModuleRule(ref lut, in rule0, lutIndex, ref coolingSinkBonus);
            UpgradeMatrixCompiler.ApplyToolModuleRule(ref lut, in rule1, lutIndex, ref coolingSinkBonus);
            UpgradeMatrixCompiler.ApplyToolModuleRule(ref lut, in rule2, lutIndex, ref coolingSinkBonus);
            UpgradeMatrixCompiler.ApplyToolModuleRule(ref lut, in rule3, lutIndex, ref coolingSinkBonus);
            lut.Mult5 *= 1f + coolingSinkBonus;

            baseline.StateHash = UpgradeMatrixCompiler.HashMask(originalMask, profile.ToolId, 0u);
            UpgradeStatVectorDTO compiled = UpgradeMatrixCompiler.ApplyLut(in baseline, in lut);
            upgradeMask = originalMask & SupportedMask64;
            return CompileRuntimeStatsFromLut(profile, in compiled);
        }

        public static ToolUpgradeModuleRuleDTO NormalizeRuleSlot(ToolUpgradeModuleRuleDTO rule, int slotIndex)
        {
            if (rule.Occupied == 0)
                return default;

            int safeSlot = math.clamp(slotIndex, 0, MaxModuleSlots - 1);
            rule.SlotIndex = (byte)safeSlot;
            rule.CompressedBit = 1u << safeSlot;
            rule.StateHash = UpgradeMatrixCompiler.HashMask(rule.UpgradeBit, rule.EntityHashID, rule.EquipmentHashID);
            rule.StateHash = UpgradeMatrixCompiler.Mix(rule.StateHash, (uint)safeSlot);
            return rule;
        }

        private static ToolUpgradeModuleRuleDTO ReadRuleAt(ToolUpgradeModuleRuleDTO[] rules, int safeSlotCount, int slotIndex)
        {
            if (rules == null || slotIndex < 0 || slotIndex >= safeSlotCount || slotIndex >= rules.Length)
                return default;

            return NormalizeRuleSlot(rules[slotIndex], slotIndex);
        }

        public static UpgradeStatVectorDTO CreateIdentityStatVector(ulong stateHash)
        {
            return new UpgradeStatVectorDTO
            {
                Stat0 = 1f,
                Stat1 = 1f,
                Stat2 = 1f,
                Stat3 = 1f,
                Stat4 = 1f,
                Stat5 = 1f,
                Stat6 = 1f,
                Stat7 = 1f,
                Stat8 = 1f,
                Stat9 = 1f,
                Stat10 = 1f,
                Stat11 = 1f,
                StateHash = stateHash
            };
        }
    }
}
