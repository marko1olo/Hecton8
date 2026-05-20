namespace Hecton8.Tools
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Hecton8.Core.Memory;
    using Unity.Burst;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// Fixed buffer and mask IDs for SHINOBU_231 upgrade stat compilation.
    /// </summary>
    public static class UpgradeMatrixConstants
    {
        public const int MockMaskCount = 10000;
        public const int TelemetryFrameCount = 300;
        public const int DefaultLutEntryCount = 4096;
        public const int DefaultInventorySlotsPerEntity = 8;
        public const uint LayoutMagic = 0x55323331u; // U231
        public const float FaultCostThresholdMicroseconds = 100f;
        public const ulong ThermalReactorBit = 1UL << 10;
        public const ulong VisualFlagMask = 0xFFFF000000000000UL;
        public const BufferID UpgradeMasksBuffer = (BufferID)71380;
        public const BufferID UpgradeBaseStatsBuffer = (BufferID)71381;
        public const BufferID UpgradeCompiledStatsBuffer = (BufferID)71382;
        public const BufferID UpgradeLutBuffer = (BufferID)71383;
        public const BufferID UpgradeRulesBuffer = (BufferID)71384;
        public const BufferID UpgradeTelemetryRingBuffer = (BufferID)71385;
        public const BufferID UpgradeTelemetryCursorBuffer = (BufferID)71386;
        public const BufferID UpgradeInventorySlotsBuffer = (BufferID)71387;
        public const BufferID UpgradeItemMapBuffer = (BufferID)71388;
        public const BufferID UpgradeVisualFlagsBuffer = (BufferID)71389;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_231.bin";
    }

    /// <summary>
    /// Generation-checked Vault handles for the SHINOBU_231 upgrade matrix route.
    /// </summary>
    public struct UpgradeMatrixVaultHandles
    {
        public VaultGenerationHandle<UpgradeMaskDTO> Masks;
        public VaultGenerationHandle<UpgradeStatVectorDTO> BaseStats;
        public VaultGenerationHandle<UpgradeStatVectorDTO> CompiledStats;
        public VaultGenerationHandle<UpgradeLutEntryDTO> Lut;
        public VaultGenerationHandle<UpgradeBitRuleDTO> Rules;
        public VaultGenerationHandle<UpgradeTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<InventoryUpgradeSlotDTO> InventorySlots;
        public VaultGenerationHandle<UpgradeItemMapDTO> ItemMap;
        public VaultGenerationHandle<uint> VisualFlags;
    }

    /// <summary>
    /// Transient NativeArray views resolved from generation-checked Vault handles.
    /// </summary>
    public struct UpgradeMatrixVaultViews
    {
        public NativeArray<UpgradeMaskDTO> Masks;
        public NativeArray<UpgradeStatVectorDTO> BaseStats;
        public NativeArray<UpgradeStatVectorDTO> CompiledStats;
        public NativeArray<UpgradeLutEntryDTO> Lut;
        public NativeArray<UpgradeBitRuleDTO> Rules;
        public NativeArray<UpgradeTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<InventoryUpgradeSlotDTO> InventorySlots;
        public NativeArray<UpgradeItemMapDTO> ItemMap;
        public NativeArray<uint> VisualFlags;
    }

    /// <summary>
    /// Cold bootstrap bridge for Vault-owned upgrade matrix memory.
    /// </summary>
    public static class UpgradeMatrixVault
    {
        public static UpgradeMatrixVaultHandles AcquireHandles(
            IDataVault vault,
            int equipmentCapacity,
            int lutEntryCapacity,
            int ruleCapacity,
            int inventorySlotCapacity,
            int itemMapCapacity)
        {
            int safeEquipmentCapacity = math.max(1, equipmentCapacity);
            int safeLutCapacity = math.max(1, lutEntryCapacity);
            int safeRuleCapacity = math.max(1, ruleCapacity);
            int safeSlotCapacity = math.max(1, inventorySlotCapacity);
            int safeItemMapCapacity = math.max(1, itemMapCapacity);
            return new UpgradeMatrixVaultHandles
            {
                Masks = vault.GetGenerationHandle<UpgradeMaskDTO>(
                    UpgradeMatrixConstants.UpgradeMasksBuffer,
                    safeEquipmentCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                BaseStats = vault.GetGenerationHandle<UpgradeStatVectorDTO>(
                    UpgradeMatrixConstants.UpgradeBaseStatsBuffer,
                    safeEquipmentCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                CompiledStats = vault.GetGenerationHandle<UpgradeStatVectorDTO>(
                    UpgradeMatrixConstants.UpgradeCompiledStatsBuffer,
                    safeEquipmentCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                Lut = vault.GetGenerationHandle<UpgradeLutEntryDTO>(
                    UpgradeMatrixConstants.UpgradeLutBuffer,
                    safeLutCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                Rules = vault.GetGenerationHandle<UpgradeBitRuleDTO>(
                    UpgradeMatrixConstants.UpgradeRulesBuffer,
                    safeRuleCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                TelemetryRing = vault.GetGenerationHandle<UpgradeTelemetryEntry>(
                    UpgradeMatrixConstants.UpgradeTelemetryRingBuffer,
                    UpgradeMatrixConstants.TelemetryFrameCount,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                TelemetryCursor = vault.GetGenerationHandle<int>(
                    UpgradeMatrixConstants.UpgradeTelemetryCursorBuffer,
                    1,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                InventorySlots = vault.GetGenerationHandle<InventoryUpgradeSlotDTO>(
                    UpgradeMatrixConstants.UpgradeInventorySlotsBuffer,
                    safeSlotCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                ItemMap = vault.GetGenerationHandle<UpgradeItemMapDTO>(
                    UpgradeMatrixConstants.UpgradeItemMapBuffer,
                    safeItemMapCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory),
                VisualFlags = vault.GetGenerationHandle<uint>(
                    UpgradeMatrixConstants.UpgradeVisualFlagsBuffer,
                    safeEquipmentCapacity,
                    SystemID.GameplayTools,
                    NativeArrayOptions.UninitializedMemory)
            };
        }

        public static bool TryResolveViews(IDataVault vault, in UpgradeMatrixVaultHandles handles, out UpgradeMatrixVaultViews views)
        {
            views = default;
            return vault.TryResolveHandle(in handles.Masks, out views.Masks) &&
                   vault.TryResolveHandle(in handles.BaseStats, out views.BaseStats) &&
                   vault.TryResolveHandle(in handles.CompiledStats, out views.CompiledStats) &&
                   vault.TryResolveHandle(in handles.Lut, out views.Lut) &&
                   vault.TryResolveHandle(in handles.Rules, out views.Rules) &&
                   vault.TryResolveHandle(in handles.TelemetryRing, out views.TelemetryRing) &&
                   vault.TryResolveHandle(in handles.TelemetryCursor, out views.TelemetryCursor) &&
                   vault.TryResolveHandle(in handles.InventorySlots, out views.InventorySlots) &&
                   vault.TryResolveHandle(in handles.ItemMap, out views.ItemMap) &&
                   vault.TryResolveHandle(in handles.VisualFlags, out views.VisualFlags);
        }
    }

    /// <summary>
    /// ARM64-safe active upgrade mask. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct UpgradeMaskDTO
    {
        [FieldOffset(0)] public uint EntityHashID;
        [FieldOffset(4)] public uint EquipmentHashID;
        [FieldOffset(8)] public ulong ActiveUpgradesMask;
    }

    /// <summary>
    /// Generic 12-lane stat vector consumed by the LUT compiler. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct UpgradeStatVectorDTO
    {
        [FieldOffset(0)] public float Stat0;
        [FieldOffset(4)] public float Stat1;
        [FieldOffset(8)] public float Stat2;
        [FieldOffset(12)] public float Stat3;
        [FieldOffset(16)] public float Stat4;
        [FieldOffset(20)] public float Stat5;
        [FieldOffset(24)] public float Stat6;
        [FieldOffset(28)] public float Stat7;
        [FieldOffset(32)] public float Stat8;
        [FieldOffset(36)] public float Stat9;
        [FieldOffset(40)] public float Stat10;
        [FieldOffset(44)] public float Stat11;
        [FieldOffset(48)] public uint VisualFlags;
        [FieldOffset(52)] public uint FaultFlags;
        [FieldOffset(56)] public ulong StateHash;
    }

    /// <summary>
    /// Per-LUT stat multipliers and additives. Size: 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct UpgradeLutEntryDTO
    {
        [FieldOffset(0)] public float Mult0;
        [FieldOffset(4)] public float Mult1;
        [FieldOffset(8)] public float Mult2;
        [FieldOffset(12)] public float Mult3;
        [FieldOffset(16)] public float Mult4;
        [FieldOffset(20)] public float Mult5;
        [FieldOffset(24)] public float Mult6;
        [FieldOffset(28)] public float Mult7;
        [FieldOffset(32)] public float Mult8;
        [FieldOffset(36)] public float Mult9;
        [FieldOffset(40)] public float Mult10;
        [FieldOffset(44)] public float Mult11;
        [FieldOffset(48)] public float Add0;
        [FieldOffset(52)] public float Add1;
        [FieldOffset(56)] public float Add2;
        [FieldOffset(60)] public float Add3;
        [FieldOffset(64)] public float Add4;
        [FieldOffset(68)] public float Add5;
        [FieldOffset(72)] public float Add6;
        [FieldOffset(76)] public float Add7;
        [FieldOffset(80)] public float Add8;
        [FieldOffset(84)] public float Add9;
        [FieldOffset(88)] public float Add10;
        [FieldOffset(92)] public float Add11;
        [FieldOffset(96)] public uint VisualFlags;
        [FieldOffset(100)] public uint LookupCount;
        [FieldOffset(104)] public ulong StateHash;
        [FieldOffset(112)] private ulong _pad0;
        [FieldOffset(120)] private ulong _pad1;
    }

    /// <summary>
    /// Cold rule row used to build multiplier LUT entries. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct UpgradeBitRuleDTO
    {
        [FieldOffset(0)] public ulong OriginalBit;
        [FieldOffset(8)] public uint CompressedBit;
        [FieldOffset(12)] public float Multiplier;
        [FieldOffset(16)] public float Additive;
        [FieldOffset(20)] public uint VisualFlags;
        [FieldOffset(24)] public byte StatLane;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    /// <summary>
    /// Inventory slot source row for PRE_SIMULATION mask packing. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InventoryUpgradeSlotDTO
    {
        [FieldOffset(0)] public uint EntityHashID;
        [FieldOffset(4)] public uint EquipmentHashID;
        [FieldOffset(8)] public uint ItemHashID;
        [FieldOffset(12)] public ushort StackCount;
        [FieldOffset(14)] public ushort Flags;
    }

    /// <summary>
    /// Item hash to upgrade-bit mapping. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct UpgradeItemMapDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public uint EquipmentHashID;
        [FieldOffset(8)] public ulong UpgradeBit;
    }

    /// <summary>
    /// Vehicle stat publication target used when Agent 113 has no concrete DTO in this branch. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VehicleKinematicUpgradeDTO
    {
        [FieldOffset(0)] public uint EntityHashID;
        [FieldOffset(4)] public float SafeDepthBonusMeters;
        [FieldOffset(8)] public float MaxIntegrityBonus;
        [FieldOffset(12)] public float ThrustAccelerationMultiplier;
        [FieldOffset(16)] public float MaxSpeedMultiplier;
        [FieldOffset(20)] public float EnergyDrainScale;
        [FieldOffset(24)] public float ChargeDrainScale;
        [FieldOffset(28)] public float ThermalExposureScale;
        [FieldOffset(32)] public float PressureDamageScale;
        [FieldOffset(36)] public float EnergyRecoveryScalar;
        [FieldOffset(40)] public uint VisualFlags;
        [FieldOffset(44)] public uint FaultFlags;
        [FieldOffset(48)] public ulong UpgradeMaskHash;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Upgrade black-box telemetry row. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct UpgradeTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint EvaluatedMaskCount;
        [FieldOffset(8)] public uint ActiveBitCount;
        [FieldOffset(12)] public uint LutLookupCount;
        [FieldOffset(16)] public float BurstMicroseconds;
        [FieldOffset(20)] public uint FaultFlags;
        [FieldOffset(24)] public uint LayoutMagic;
        [FieldOffset(28)] public uint LastEntityHashID;
        [FieldOffset(32)] public ulong LastMask;
        [FieldOffset(40)] public ulong StateHash;
        [FieldOffset(48)] public ulong Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    /// <summary>
    /// Layout verifier for SHINOBU_231 DTOs. Cold path only.
    /// </summary>
    public static class UpgradeMatrixLayoutValidator
    {
        public const uint FaultMaskSize = 1u << 0;
        public const uint FaultMaskAlign = 1u << 1;
        public const uint FaultMaskOffset = 1u << 2;
        public const uint FaultStatSize = 1u << 3;
        public const uint FaultLutSize = 1u << 4;
        public const uint FaultTelemetrySize = 1u << 5;

        public static bool Validate(out uint faultFlags)
        {
            faultFlags = 0u;
            if (UnsafeUtility.SizeOf<UpgradeMaskDTO>() != 16)
                faultFlags |= FaultMaskSize;
            if (UnsafeUtility.AlignOf<UpgradeMaskDTO>() < 8)
                faultFlags |= FaultMaskAlign;
            if (OffsetOf<UpgradeMaskDTO>(nameof(UpgradeMaskDTO.ActiveUpgradesMask)) != 8)
                faultFlags |= FaultMaskOffset;
            if (UnsafeUtility.SizeOf<UpgradeStatVectorDTO>() != 64)
                faultFlags |= FaultStatSize;
            if (UnsafeUtility.SizeOf<UpgradeLutEntryDTO>() != 128)
                faultFlags |= FaultLutSize;
            if (UnsafeUtility.SizeOf<UpgradeTelemetryEntry>() != 64)
                faultFlags |= FaultTelemetrySize;
            return faultFlags == 0u;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
    }

    /// <summary>
    /// Stateless branchless helpers shared by jobs and cold facades.
    /// </summary>
    public static class UpgradeMatrixCompiler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Bit01(ulong mask, ulong bit)
        {
            return math.select(0f, 1f, (mask & bit) != 0UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BitMask32(ulong mask, ulong bit)
        {
            uint enabled = (uint)math.select(0, 1, (mask & bit) != 0UL);
            return 0u - enabled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UpgradeStatVectorDTO ApplyLut(in UpgradeStatVectorDTO baseline, in UpgradeLutEntryDTO lut)
        {
            UpgradeStatVectorDTO result = default;
            result.Stat0 = (baseline.Stat0 * lut.Mult0) + lut.Add0;
            result.Stat1 = (baseline.Stat1 * lut.Mult1) + lut.Add1;
            result.Stat2 = (baseline.Stat2 * lut.Mult2) + lut.Add2;
            result.Stat3 = (baseline.Stat3 * lut.Mult3) + lut.Add3;
            result.Stat4 = (baseline.Stat4 * lut.Mult4) + lut.Add4;
            result.Stat5 = (baseline.Stat5 * lut.Mult5) + lut.Add5;
            result.Stat6 = (baseline.Stat6 * lut.Mult6) + lut.Add6;
            result.Stat7 = (baseline.Stat7 * lut.Mult7) + lut.Add7;
            result.Stat8 = (baseline.Stat8 * lut.Mult8) + lut.Add8;
            result.Stat9 = (baseline.Stat9 * lut.Mult9) + lut.Add9;
            result.Stat10 = (baseline.Stat10 * lut.Mult10) + lut.Add10;
            result.Stat11 = (baseline.Stat11 * lut.Mult11) + lut.Add11;
            result.VisualFlags = baseline.VisualFlags | lut.VisualFlags;
            result.FaultFlags = baseline.FaultFlags;
            result.StateHash = Mix(Mix(lut.StateHash, baseline.StateHash), baseline.VisualFlags);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix(ulong hash, ulong value)
        {
            hash ^= value + 0x9E3779B97F4A7C15UL + (hash << 6) + (hash >> 2);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashMask(ulong mask, uint entityHash, uint equipmentHash)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ mask) * 1099511628211UL;
            hash = (hash ^ entityHash) * 1099511628211UL;
            hash = (hash ^ equipmentHash) * 1099511628211UL;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount64(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL) * 0x0101010101010101UL) >> 56);
        }

        public static unsafe bool DumpTelemetry(ReadOnlySpan<UpgradeTelemetryEntry> telemetry, string projectRoot)
        {
            if (telemetry.Length <= 0 || string.IsNullOrEmpty(projectRoot))
                return false;

            string path = Path.Combine(projectRoot, UpgradeMatrixConstants.DumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            fixed (UpgradeTelemetryEntry* ptr = telemetry)
            {
                ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(ptr, telemetry.Length * UnsafeUtility.SizeOf<UpgradeTelemetryEntry>());
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
            }

            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildUpgradeLUTJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<UpgradeBitRuleDTO> Rules;
        [NoAlias] public NativeArray<UpgradeLutEntryDTO> Lut;

        public void Execute(int index)
        {
            UpgradeLutEntryDTO entry = default;
            entry.Mult0 = 1f;
            entry.Mult1 = 1f;
            entry.Mult2 = 1f;
            entry.Mult3 = 1f;
            entry.Mult4 = 1f;
            entry.Mult5 = 1f;
            entry.Mult6 = 1f;
            entry.Mult7 = 1f;
            entry.Mult8 = 1f;
            entry.Mult9 = 1f;
            entry.Mult10 = 1f;
            entry.Mult11 = 1f;
            entry.StateHash = (ulong)(uint)index;

            for (int i = 0; i < Rules.Length; i++)
            {
                UpgradeBitRuleDTO rule = Rules[i];
                float enabled = math.select(0f, 1f, ((uint)index & rule.CompressedBit) != 0u);
                uint enabledMask = 0u - (uint)math.select(0, 1, enabled > 0f);
                float multiplier = 1f + ((math.max(0.0001f, rule.Multiplier) - 1f) * enabled);
                float additive = rule.Additive * enabled;
                entry.VisualFlags |= rule.VisualFlags & enabledMask;
                entry.LookupCount += (uint)math.select(0, 1, enabled > 0f);
                entry.StateHash = UpgradeMatrixCompiler.Mix(entry.StateHash, rule.OriginalBit & (0UL - (ulong)math.select(0, 1, enabled > 0f)));
                ApplyRule(ref entry, rule.StatLane, multiplier, additive);
            }

            Lut[index] = entry;
        }

        private static void ApplyRule(ref UpgradeLutEntryDTO entry, byte lane, float multiplier, float additive)
        {
            switch (lane)
            {
                case 0: entry.Mult0 *= multiplier; entry.Add0 += additive; break;
                case 1: entry.Mult1 *= multiplier; entry.Add1 += additive; break;
                case 2: entry.Mult2 *= multiplier; entry.Add2 += additive; break;
                case 3: entry.Mult3 *= multiplier; entry.Add3 += additive; break;
                case 4: entry.Mult4 *= multiplier; entry.Add4 += additive; break;
                case 5: entry.Mult5 *= multiplier; entry.Add5 += additive; break;
                case 6: entry.Mult6 *= multiplier; entry.Add6 += additive; break;
                case 7: entry.Mult7 *= multiplier; entry.Add7 += additive; break;
                case 8: entry.Mult8 *= multiplier; entry.Add8 += additive; break;
                case 9: entry.Mult9 *= multiplier; entry.Add9 += additive; break;
                case 10: entry.Mult10 *= multiplier; entry.Add10 += additive; break;
                case 11: entry.Mult11 *= multiplier; entry.Add11 += additive; break;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateUpgradeMasksJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeMaskDTO* Masks;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeStatVectorDTO* BaseStats;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeLutEntryDTO* Lut;
        [NoAlias, NativeDisableUnsafePtrRestriction] public double3* EntityAups;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* ThermalGridCelsius;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeStatVectorDTO* CompiledStats;
        public ulong LutIndexMask;
        public int LutIndexShift;
        public int ThermalWidth;
        public int ThermalHeight;
        public int ThermalDepth;
        public int ThermalGridLength;
        public float ThermalCellSizeMeters;
        public double3 ThermalGridOriginAup;
        public float AmbientFallbackCelsius;
        public float ThermalReactorGain;
        public ulong ThermalReactorBit;

        public void Execute(int index)
        {
            ref UpgradeMaskDTO mask = ref UnsafeUtility.AsRef<UpgradeMaskDTO>(Masks + index);
            ref UpgradeStatVectorDTO baseline = ref UnsafeUtility.AsRef<UpgradeStatVectorDTO>(BaseStats + index);
            int lutIndex = (int)((mask.ActiveUpgradesMask & LutIndexMask) >> LutIndexShift);
            UpgradeLutEntryDTO lut = UnsafeUtility.AsRef<UpgradeLutEntryDTO>(Lut + lutIndex);
            UpgradeStatVectorDTO compiled = UpgradeMatrixCompiler.ApplyLut(in baseline, in lut);

            int width = math.max(1, ThermalWidth);
            int height = math.max(1, ThermalHeight);
            int depth = math.max(1, ThermalDepth);
            int gridLength = math.max(1, ThermalGridLength);
            float cellSize = math.max(0.0001f, ThermalCellSizeMeters);
            double3 deltaAup = UnsafeUtility.AsRef<double3>(EntityAups + index) - ThermalGridOriginAup;
            float3 local = new float3((float)deltaAup.x, (float)deltaAup.y, (float)deltaAup.z);
            float3 gridPosition = local * math.rcp(cellSize);
            int3 cell = (int3)math.floor(gridPosition);
            cell = math.clamp(cell, int3.zero, new int3(width - 1, height - 1, depth - 1));
            int gridIndex = math.min(gridLength - 1, cell.x + (cell.y * width) + (cell.z * width * height));
            float ambient = UnsafeUtility.AsRef<float>(ThermalGridCelsius + gridIndex);
            float hasReactor = UpgradeMatrixCompiler.Bit01(mask.ActiveUpgradesMask, ThermalReactorBit);
            compiled.Stat10 += (ambient - AmbientFallbackCelsius) * ThermalReactorGain * hasReactor;
            compiled.VisualFlags |= (uint)((mask.ActiveUpgradesMask & UpgradeMatrixConstants.VisualFlagMask) >> 48);
            compiled.StateHash = UpgradeMatrixCompiler.HashMask(mask.ActiveUpgradesMask, mask.EntityHashID, mask.EquipmentHashID);
            UnsafeUtility.AsRef<UpgradeStatVectorDTO>(CompiledStats + index) = compiled;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockUpgradeMasksJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<UpgradeMaskDTO> Masks;
        [NoAlias] public NativeArray<UpgradeStatVectorDTO> BaseStats;
        [NoAlias] public NativeArray<double3> EntityAups;
        public uint Seed;
        public double3 OriginAup;

        public void Execute(int index)
        {
            ulong state = ((ulong)Seed << 32) ^ (uint)index ^ 0xA0761D6478BD642FUL;
            state = Next(state);
            ulong mask = state & 0x0000FFFFFFFF0FFFUL;
            Masks[index] = new UpgradeMaskDTO
            {
                EntityHashID = (uint)(0x55320000u + (uint)index),
                EquipmentHashID = (uint)(0x45310000u + (uint)(index & 255)),
                ActiveUpgradesMask = mask
            };
            BaseStats[index] = new UpgradeStatVectorDTO
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
                Stat10 = 0f,
                Stat11 = 0f,
                VisualFlags = 0u,
                FaultFlags = 0u,
                StateHash = mask
            };
            EntityAups[index] = OriginAup + new double3(index * 0.25, (index & 63) * -0.125, (index & 127) * 0.5);
        }

        private static ulong Next(ulong value)
        {
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            return value * 2685821657736338717UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SyncUpgradeMasksJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InventoryUpgradeSlotDTO> Slots;
        [ReadOnly, NoAlias] public NativeArray<UpgradeItemMapDTO> ItemMap;
        [NoAlias] public NativeArray<UpgradeMaskDTO> Masks;
        public int SlotsPerEntity;

        public void Execute(int entityIndex)
        {
            UpgradeMaskDTO mask = Masks[entityIndex];
            ulong packed = 0UL;
            int slotBase = entityIndex * math.max(1, SlotsPerEntity);
            int slotLimit = slotBase + math.max(1, SlotsPerEntity);
            for (int slotIndex = slotBase; slotIndex < slotLimit; slotIndex++)
            {
                InventoryUpgradeSlotDTO slot = Slots[slotIndex];
                ulong occupied = (ulong)math.select(0, 1, slot.StackCount > 0);
                for (int mapIndex = 0; mapIndex < ItemMap.Length; mapIndex++)
                {
                    UpgradeItemMapDTO mapping = ItemMap[mapIndex];
                    ulong itemMatch = (ulong)math.select(0, 1, slot.ItemHashID == mapping.ItemHashID);
                    ulong equipmentMatch = (ulong)math.select(0, 1, mask.EquipmentHashID == mapping.EquipmentHashID || mapping.EquipmentHashID == 0u);
                    packed |= mapping.UpgradeBit & (0UL - (occupied & itemMatch & equipmentMatch));
                }
            }

            mask.ActiveUpgradesMask = packed;
            Masks[entityIndex] = mask;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PublishActiveEquipmentStatsJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* ActiveEquipment;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeStatVectorDTO* CompiledStats;

        public void Execute(int index)
        {
            ref ActiveEquipmentDTO equipment = ref UnsafeUtility.AsRef<ActiveEquipmentDTO>(ActiveEquipment + index);
            ref UpgradeStatVectorDTO stats = ref UnsafeUtility.AsRef<UpgradeStatVectorDTO>(CompiledStats + index);
            equipment.PowerDrawRate *= math.max(0.0001f, stats.Stat7);
            equipment.HeatGenerationRate *= math.max(0.0001f, stats.Stat4);
            equipment.StateFlags |= stats.VisualFlags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct PublishVehicleKinematicStatsJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public VehicleKinematicUpgradeDTO* Vehicles;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeMaskDTO* Masks;
        [NoAlias, NativeDisableUnsafePtrRestriction] public UpgradeStatVectorDTO* CompiledStats;

        public void Execute(int index)
        {
            ref VehicleKinematicUpgradeDTO vehicle = ref UnsafeUtility.AsRef<VehicleKinematicUpgradeDTO>(Vehicles + index);
            ref UpgradeMaskDTO mask = ref UnsafeUtility.AsRef<UpgradeMaskDTO>(Masks + index);
            ref UpgradeStatVectorDTO stats = ref UnsafeUtility.AsRef<UpgradeStatVectorDTO>(CompiledStats + index);
            vehicle.EntityHashID = mask.EntityHashID;
            vehicle.SafeDepthBonusMeters = stats.Stat0;
            vehicle.MaxIntegrityBonus = stats.Stat1;
            vehicle.ThrustAccelerationMultiplier = stats.Stat2;
            vehicle.MaxSpeedMultiplier = stats.Stat3;
            vehicle.EnergyDrainScale = stats.Stat4;
            vehicle.ChargeDrainScale = stats.Stat5;
            vehicle.ThermalExposureScale = stats.Stat6;
            vehicle.PressureDamageScale = stats.Stat7;
            vehicle.EnergyRecoveryScalar = stats.Stat10;
            vehicle.VisualFlags = stats.VisualFlags;
            vehicle.FaultFlags = stats.FaultFlags;
            vehicle.UpgradeMaskHash = stats.StateHash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordUpgradeTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<UpgradeMaskDTO> Masks;
        [ReadOnly, NoAlias] public NativeArray<UpgradeStatVectorDTO> CompiledStats;
        [NoAlias] public NativeArray<UpgradeTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint Frame;
        public float BurstMicroseconds;

        public void Execute()
        {
            int cursor = TelemetryCursor[0];
            int ringIndex = cursor % UpgradeMatrixConstants.TelemetryFrameCount;
            uint activeBits = 0u;
            ulong stateHash = 1469598103934665603UL;
            UpgradeMaskDTO last = default;
            for (int i = 0; i < Masks.Length; i++)
            {
                last = Masks[i];
                activeBits += (uint)UpgradeMatrixCompiler.PopCount64(last.ActiveUpgradesMask);
                stateHash = UpgradeMatrixCompiler.Mix(stateHash, CompiledStats[i].StateHash);
            }

            TelemetryRing[ringIndex] = new UpgradeTelemetryEntry
            {
                Frame = Frame,
                EvaluatedMaskCount = (uint)Masks.Length,
                ActiveBitCount = activeBits,
                LutLookupCount = (uint)Masks.Length,
                BurstMicroseconds = BurstMicroseconds,
                FaultFlags = (uint)math.select(0, 1, BurstMicroseconds > UpgradeMatrixConstants.FaultCostThresholdMicroseconds),
                LayoutMagic = UpgradeMatrixConstants.LayoutMagic,
                LastEntityHashID = last.EntityHashID,
                LastMask = last.ActiveUpgradesMask,
                StateHash = stateHash,
                Reserved0 = 0UL,
                Reserved1 = 0UL
            };
            TelemetryCursor[0] = cursor + 1;
        }
    }

    /// <summary>
    /// Cold boot CSV ingestor for upgrade-chip matrix rows.
    /// </summary>
    public static class UpgradeMatrixCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<UpgradeItemMapDTO> itemMap, NativeArray<UpgradeBitRuleDTO> rules)
        {
            int rowStart = 0;
            int rowIndex = 0;
            int writeIndex = 0;
            for (int i = 0; i <= csv.Length; i++)
            {
                bool end = i == csv.Length;
                byte value = end ? (byte)'\n' : csv[i];
                if (!end && value != (byte)'\n')
                    continue;

                int rowEnd = i;
                if (rowEnd > rowStart && csv[rowEnd - 1] == (byte)'\r')
                    rowEnd--;

                ReadOnlySpan<byte> row = csv.Slice(rowStart, rowEnd - rowStart);
                if (rowIndex > 0 && row.Length > 0 && writeIndex < itemMap.Length && writeIndex < rules.Length)
                {
                    ParseRow(row, out UpgradeItemMapDTO map, out UpgradeBitRuleDTO rule);
                    itemMap[writeIndex] = map;
                    rules[writeIndex] = rule;
                    writeIndex++;
                }

                rowStart = i + 1;
                rowIndex++;
            }

            return writeIndex;
        }

        private static void ParseRow(ReadOnlySpan<byte> row, out UpgradeItemMapDTO map, out UpgradeBitRuleDTO rule)
        {
            int cursor = 0;
            uint itemHash = ParseHash(ReadCell(row, ref cursor));
            uint equipmentHash = ParseUInt(ReadCell(row, ref cursor), 0u);
            byte bitIndex = (byte)math.clamp((int)ParseUInt(ReadCell(row, ref cursor), 0u), 0, 63);
            byte statLane = (byte)math.clamp((int)ParseUInt(ReadCell(row, ref cursor), 0u), 0, 11);
            float multiplier = ParseFloat(ReadCell(row, ref cursor), 1f);
            float additive = ParseFloat(ReadCell(row, ref cursor), 0f);
            uint visualFlags = ParseUInt(ReadCell(row, ref cursor), 0u);
            ulong bit = 1UL << bitIndex;
            map = new UpgradeItemMapDTO
            {
                ItemHashID = itemHash,
                EquipmentHashID = equipmentHash,
                UpgradeBit = bit
            };
            rule = new UpgradeBitRuleDTO
            {
                OriginalBit = bit,
                CompressedBit = 1u << math.min(31, bitIndex),
                Multiplier = multiplier,
                Additive = additive,
                VisualFlags = visualFlags,
                StatLane = statLane
            };
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> row, ref int cursor)
        {
            if (cursor >= row.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < row.Length)
                cursor++;

            while (start < end && IsWhitespace(row[start]))
                start++;
            while (end > start && IsWhitespace(row[end - 1]))
                end--;
            return row.Slice(start, end - start);
        }

        private static uint ParseHash(ReadOnlySpan<byte> value)
        {
            return TryParseUInt(value, out uint parsed) ? parsed : HashLower(value);
        }

        private static uint ParseUInt(ReadOnlySpan<byte> value, uint fallback)
        {
            return TryParseUInt(value, out uint parsed) ? parsed : fallback;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> value, out uint parsed)
        {
            parsed = 0u;
            if (value.Length <= 0)
                return false;

            int start = 0;
            int radix = 10;
            if (value.Length > 2 && value[0] == (byte)'0' && (value[1] == (byte)'x' || value[1] == (byte)'X'))
            {
                start = 2;
                radix = 16;
            }

            bool consumed = false;
            for (int i = start; i < value.Length; i++)
            {
                byte b = value[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (radix == 16 && b >= (byte)'a' && b <= (byte)'f')
                    digit = 10u + (uint)(b - (byte)'a');
                else if (radix == 16 && b >= (byte)'A' && b <= (byte)'F')
                    digit = 10u + (uint)(b - (byte)'A');
                else
                    return false;

                parsed = (parsed * (uint)radix) + digit;
                consumed = true;
            }

            return consumed;
        }

        private static float ParseFloat(ReadOnlySpan<byte> value, float fallback)
        {
            if (value.Length <= 0)
                return fallback;

            int i = 0;
            float sign = 1f;
            if (value[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (value[i] == (byte)'+')
            {
                i++;
            }

            float whole = 0f;
            bool consumed = false;
            while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
            {
                whole = (whole * 10f) + (value[i] - (byte)'0');
                i++;
                consumed = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < value.Length && value[i] == (byte)'.')
            {
                i++;
                while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (value[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    consumed = true;
                }
            }

            float parsed = sign * (whole + (fraction * math.rcp(math.max(1f, scale))));
            return consumed && math.isfinite(parsed) ? parsed : fallback;
        }

        private static uint HashLower(ReadOnlySpan<byte> value)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }
    }
}
