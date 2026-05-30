using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Power
{
    public static class BatteryChargerLogisticsConstants
    {
        public const int ChargerLinkDtoSizeBytes = 32;
        public const int ChargerTuningDtoSizeBytes = 32;
        public const int ChargerTelemetryEntrySizeBytes = 64;
        public const int ChargerVisualStateDtoSizeBytes = 32;
        public const int ChargerProfileDtoSizeBytes = 32;
        public const int ChargerAtomicCountersSizeBytes = 64;
        public const int CounterLaneCount = 128;
        public const int TelemetryCapacity = 300;
        public const int DefaultLinkCapacity = 5000;
        public const int DefaultNodeCapacity = 5000;
        public const int DefaultProfileCapacity = 128;
        public const int CsvScratchBytes = 16 * 1024;
        public const float DefaultMaxChargeRate01PerSecond = 0.12f;
        public const float DefaultEfficiencyExponent = 0.5f;
        public const float DefaultBatteryCapacity01 = 1.0f;
        public const float FaultDumpFenceElapsedThresholdMicroseconds = 500.0f;
        public const uint LinkFlagActive = 1u << 0;
        public const uint LinkFlagFull = 1u << 1;
        public const uint LinkFlagUnpowered = 1u << 2;
        public const uint LinkFlagMock = 1u << 3;
        public const uint LinkFlagNodeHashMismatch = 1u << 4;
        public const uint LinkFlagAtomicConflict = 1u << 5;
        public const uint TelemetryFlagAtomicConflict = 1u << 0;
        public const uint TelemetryFlagNodeDisconnected = 1u << 1;
        public const uint TelemetryFlagExceededBudget = 1u << 2;
        public const uint TelemetryFlagNaN = 1u << 3;
        public const uint TelemetryFlagSkippedCadence = 1u << 4;
        public const uint LockToken = 0x53483233u; // SH23
        public const uint HumSourceHash = 0x4348554Du; // CHUM
    }

    public static class BatteryChargerLogisticsBufferIds
    {
        public const BufferID Links = (BufferID)72300;
        public const BufferID LinkAup = (BufferID)72301;
        public const BufferID ExpectedPowerNodeHashes = (BufferID)72302;
        public const BufferID VisualStates = (BufferID)72303;
        public const BufferID Tuning = (BufferID)72304;
        public const BufferID TelemetryRing = (BufferID)72305;
        public const BufferID TelemetryCursor = (BufferID)72306;
        public const BufferID AtomicCounters = (BufferID)72307;
        public const BufferID Profiles = (BufferID)72308;
        public const BufferID CsvScratch = (BufferID)72309;
        public const BufferID MockInventorySlots = (BufferID)72310;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerLinkDtoSizeBytes)]
    public struct ChargerLinkDTO
    {
        [FieldOffset(0)] public uint InventorySlotIndex;
        [FieldOffset(4)] public uint PowerGraphNodeIndex;
        [FieldOffset(8)] public float ChargeRate;
        [FieldOffset(12)] public float EfficiencyScalar;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerTuningDtoSizeBytes)]
    public struct ChargerTuningDTO
    {
        [FieldOffset(0)] public float GlobalMaxChargeRate;
        [FieldOffset(4)] public float EfficiencyCurveExponent;
        [FieldOffset(8)] public float GlobalQualityWeight;
        [FieldOffset(12)] public float BatteryCapacity;
        [FieldOffset(16)] public float CadenceHz;
        [FieldOffset(20)] public float QualityOverride;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerVisualStateDtoSizeBytes)]
    public struct ChargerVisualStateDTO
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float Charge01;
        [FieldOffset(12)] public float EnergyDraw01;
        [FieldOffset(16)] public uint LinkIndex;
        [FieldOffset(20)] public uint InventorySlotIndex;
        [FieldOffset(24)] public uint PowerGraphNodeIndex;
        [FieldOffset(28)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerTelemetryEntrySizeBytes)]
    public struct ChargerTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public int ActiveLinks;
        [FieldOffset(16)] public int FullLinks;
        [FieldOffset(20)] public int UnpoweredLinks;
        [FieldOffset(24)] public int AtomicLockFailures;
        [FieldOffset(28)] public int FenceElapsedMicroseconds;
        [FieldOffset(32)] public float TotalEnergyDrawn;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float CadenceHz;
        [FieldOffset(44)] public float DeltaSeconds;
        [FieldOffset(48)] public float AverageCharge01;
        [FieldOffset(52)] public int LinkCapacity;
        [FieldOffset(56)] public uint LastFaultLink;
        [FieldOffset(60)] public uint SkippedCadenceFrames;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerProfileDtoSizeBytes)]
    public struct ChargerProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float MaxChargeRate;
        [FieldOffset(8)] public float EfficiencyScalar;
        [FieldOffset(12)] public float EfficiencyCurveExponent;
        [FieldOffset(16)] public float GridLoadScalar;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BatteryChargerLogisticsConstants.ChargerAtomicCountersSizeBytes)]
    public struct ChargerAtomicCountersDTO
    {
        [FieldOffset(0)] public int ActiveLinks;
        [FieldOffset(4)] public int FullLinks;
        [FieldOffset(8)] public int UnpoweredLinks;
        [FieldOffset(12)] public int AtomicFailures;
        [FieldOffset(16)] public int TotalEnergyMilli;
        [FieldOffset(20)] public int ChargeMilliSum;
        [FieldOffset(24)] public uint FaultFlags;
        [FieldOffset(28)] public uint LastFaultLink;
        [FieldOffset(32)] public uint LastActiveLink;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    public struct BatteryChargerLogisticsHandles
    {
        public VaultGenerationHandle<ChargerLinkDTO> Links;
        public VaultGenerationHandle<double3> LinkAup;
        public VaultGenerationHandle<uint> ExpectedPowerNodeHashes;
        public VaultGenerationHandle<ChargerVisualStateDTO> VisualStates;
        public VaultGenerationHandle<ChargerTuningDTO> Tuning;
        public VaultGenerationHandle<ChargerTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<uint> TelemetryCursor;
        public VaultGenerationHandle<ChargerAtomicCountersDTO> AtomicCounters;
        public VaultGenerationHandle<ChargerProfileDTO> Profiles;
        public VaultGenerationHandle<InventorySlotDTO> MockInventorySlots;
    }

    public static class BatteryChargerLogisticsVaultRuntime
    {
        public static bool EnsureBuffers(IDataVault vault, int linkCapacity, out BatteryChargerLogisticsHandles handles)
        {
            handles = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int safeLinks = math.max(1, linkCapacity);
            handles.Links = vault.EnsureGenerationHandle<ChargerLinkDTO>(
                BatteryChargerLogisticsBufferIds.Links,
                safeLinks,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.LinkAup = vault.EnsureGenerationHandle<double3>(
                BatteryChargerLogisticsBufferIds.LinkAup,
                safeLinks,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.ExpectedPowerNodeHashes = vault.EnsureGenerationHandle<uint>(
                BatteryChargerLogisticsBufferIds.ExpectedPowerNodeHashes,
                safeLinks,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.VisualStates = vault.EnsureGenerationHandle<ChargerVisualStateDTO>(
                BatteryChargerLogisticsBufferIds.VisualStates,
                safeLinks,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.EnsureGenerationHandle<ChargerTuningDTO>(
                BatteryChargerLogisticsBufferIds.Tuning,
                1,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<ChargerTelemetryEntry>(
                BatteryChargerLogisticsBufferIds.TelemetryRing,
                BatteryChargerLogisticsConstants.TelemetryCapacity,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<uint>(
                BatteryChargerLogisticsBufferIds.TelemetryCursor,
                1,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.AtomicCounters = vault.EnsureGenerationHandle<ChargerAtomicCountersDTO>(
                BatteryChargerLogisticsBufferIds.AtomicCounters,
                BatteryChargerLogisticsConstants.CounterLaneCount,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.Profiles = vault.EnsureGenerationHandle<ChargerProfileDTO>(
                BatteryChargerLogisticsBufferIds.Profiles,
                BatteryChargerLogisticsConstants.DefaultProfileCapacity,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.MockInventorySlots = vault.EnsureGenerationHandle<InventorySlotDTO>(
                BatteryChargerLogisticsBufferIds.MockInventorySlots,
                safeLinks,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);

            return HasBuffer(vault, in handles.Links, safeLinks) &&
                   HasBuffer(vault, in handles.LinkAup, safeLinks) &&
                   HasBuffer(vault, in handles.ExpectedPowerNodeHashes, safeLinks) &&
                   HasBuffer(vault, in handles.VisualStates, safeLinks) &&
                   HasBuffer(vault, in handles.Tuning, 1) &&
                   HasBuffer(vault, in handles.TelemetryRing, BatteryChargerLogisticsConstants.TelemetryCapacity) &&
                   HasBuffer(vault, in handles.TelemetryCursor, 1) &&
                   HasBuffer(vault, in handles.AtomicCounters, BatteryChargerLogisticsConstants.CounterLaneCount) &&
                   HasBuffer(vault, in handles.Profiles, BatteryChargerLogisticsConstants.DefaultProfileCapacity) &&
                   HasBuffer(vault, in handles.MockInventorySlots, safeLinks);
        }

        private static bool HasBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle, int minLength) where T : struct
        {
            return handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= minLength;
        }
    }

#if UNITY_EDITOR
    public static class BatteryChargerLogisticsLayoutAudit
    {
        public static bool ValidateAll()
        {
            return ValidateChargerLinkDTO() &&
                   ValidateChargerTuningDTO() &&
                   ValidateChargerVisualStateDTO() &&
                   ValidateChargerTelemetryEntry() &&
                   ValidateChargerProfileDTO() &&
                   ValidateChargerAtomicCountersDTO();
        }

        public static bool ValidateChargerLinkDTO()
        {
            return UnsafeUtility.SizeOf<ChargerLinkDTO>() == BatteryChargerLogisticsConstants.ChargerLinkDtoSizeBytes &&
                   UnsafeUtility.AlignOf<ChargerLinkDTO>() >= 4 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO.InventorySlotIndex)) == 0 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO.PowerGraphNodeIndex)) == 4 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO.ChargeRate)) == 8 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO.EfficiencyScalar)) == 12 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO.Flags)) == 16 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO._pad0)) == 20 &&
                   OffsetOf<ChargerLinkDTO>(nameof(ChargerLinkDTO._pad11)) == 31;
        }

        private static bool ValidateChargerTuningDTO()
        {
            return UnsafeUtility.SizeOf<ChargerTuningDTO>() == BatteryChargerLogisticsConstants.ChargerTuningDtoSizeBytes &&
                   OffsetOf<ChargerTuningDTO>(nameof(ChargerTuningDTO.GlobalMaxChargeRate)) == 0 &&
                   OffsetOf<ChargerTuningDTO>(nameof(ChargerTuningDTO.Reserved0)) == 28;
        }

        private static bool ValidateChargerVisualStateDTO()
        {
            return UnsafeUtility.SizeOf<ChargerVisualStateDTO>() == BatteryChargerLogisticsConstants.ChargerVisualStateDtoSizeBytes &&
                   OffsetOf<ChargerVisualStateDTO>(nameof(ChargerVisualStateDTO.Status)) == 0 &&
                   OffsetOf<ChargerVisualStateDTO>(nameof(ChargerVisualStateDTO.StateHash)) == 28;
        }

        private static bool ValidateChargerTelemetryEntry()
        {
            return UnsafeUtility.SizeOf<ChargerTelemetryEntry>() == BatteryChargerLogisticsConstants.ChargerTelemetryEntrySizeBytes &&
                   OffsetOf<ChargerTelemetryEntry>(nameof(ChargerTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<ChargerTelemetryEntry>(nameof(ChargerTelemetryEntry.FenceElapsedMicroseconds)) == 28 &&
                   OffsetOf<ChargerTelemetryEntry>(nameof(ChargerTelemetryEntry.SkippedCadenceFrames)) == 60;
        }

        private static bool ValidateChargerProfileDTO()
        {
            return UnsafeUtility.SizeOf<ChargerProfileDTO>() == BatteryChargerLogisticsConstants.ChargerProfileDtoSizeBytes &&
                   OffsetOf<ChargerProfileDTO>(nameof(ChargerProfileDTO.ProfileHash)) == 0 &&
                   OffsetOf<ChargerProfileDTO>(nameof(ChargerProfileDTO.Reserved1)) == 28;
        }

        private static bool ValidateChargerAtomicCountersDTO()
        {
            return UnsafeUtility.SizeOf<ChargerAtomicCountersDTO>() == BatteryChargerLogisticsConstants.ChargerAtomicCountersSizeBytes &&
                   OffsetOf<ChargerAtomicCountersDTO>(nameof(ChargerAtomicCountersDTO.ActiveLinks)) == 0 &&
                   OffsetOf<ChargerAtomicCountersDTO>(nameof(ChargerAtomicCountersDTO.LastActiveLink)) == 32 &&
                   OffsetOf<ChargerAtomicCountersDTO>(nameof(ChargerAtomicCountersDTO.Reserved3)) == 56;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            var field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
#endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearChargerCountersJob : IJob
    {
        [NoAlias] public NativeArray<ChargerAtomicCountersDTO> Counters;

        public void Execute()
        {
            if (!Counters.IsCreated)
                return;

            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockChargerNetworkJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ChargerLinkDTO> Links;
        [NoAlias] public NativeArray<double3> LinkAup;
        [NoAlias] public NativeArray<uint> ExpectedPowerNodeHashes;
        [NoAlias] public NativeArray<ChargerVisualStateDTO> VisualStates;
        [NoAlias] public NativeArray<InventorySlotDTO> InventorySlots;
        [NoAlias] public NativeArray<PowerNodeDTO> PowerNodes;
        [NoAlias] public NativeArray<double3> PowerNodeAup;
        public int LinkCount;
        public double3 BaseAup;

        public void Execute(int index)
        {
            int count = math.min(LinkCount, math.min(Links.Length, math.min(LinkAup.Length, ExpectedPowerNodeHashes.Length)));
            if ((uint)index >= (uint)count)
                return;

            int powerNodeCount = math.min(PowerNodes.Length, PowerNodeAup.IsCreated ? PowerNodeAup.Length : PowerNodes.Length);
            if (powerNodeCount <= 0)
                return;

            int nodeIndex = index % powerNodeCount;
            uint nodeHash = Hash32((uint)(nodeIndex + 1));
            if ((uint)index < (uint)powerNodeCount)
            {
                PowerNodeDTO node = default;
                node.NodeHash = nodeHash;
                node.Potential = 0.35f + ((nodeIndex & 31) * (0.65f / 31f));
                node.MaxCapacity = 1.0f;
                node.CurrentStorage = node.Potential;
                node.Flags = PowerGridJacobiConstants.NodeFlagActive | ((nodeIndex & 7) == 0 ? PowerGridJacobiConstants.NodeFlagBattery : 0u);
                node.InternalResistance = 0.05f + ((nodeIndex & 15) * 0.01f);
                PowerNodes[nodeIndex] = node;
                if (PowerNodeAup.IsCreated && (uint)nodeIndex < (uint)PowerNodeAup.Length)
                    PowerNodeAup[nodeIndex] = BaseAup + new double3((nodeIndex % 100) * 2.0, 0.0, (nodeIndex / 100) * 2.0);
            }

            if ((uint)index < (uint)InventorySlots.Length)
            {
                InventorySlotDTO slot = default;
                slot.ItemHashID = 0xB477E000u + (uint)(index & 1023);
                slot.Quantity = 1u;
                slot.ContainerAUPHash = Hash64((uint)index);
                slot.ConditionFlags = math.asuint(math.saturate((index & 127) * (1f / 160f)));
                slot.ReservedLock = 0u;
                InventorySlots[index] = slot;
            }

            ChargerLinkDTO link = default;
            link.InventorySlotIndex = (uint)index;
            link.PowerGraphNodeIndex = (uint)nodeIndex;
            link.ChargeRate = 0.035f + ((index & 15) * 0.006f);
            link.EfficiencyScalar = 0.78f + ((index & 7) * 0.02f);
            link.Flags = BatteryChargerLogisticsConstants.LinkFlagActive | BatteryChargerLogisticsConstants.LinkFlagMock;
            Links[index] = link;
            LinkAup[index] = BaseAup + new double3((index % 100) * 1.75, 1.2, (index / 100) * 1.75);
            ExpectedPowerNodeHashes[index] = nodeHash;

            if ((uint)index < (uint)VisualStates.Length)
            {
                ChargerVisualStateDTO visual = default;
                visual.Status = 0u;
                visual.Flags = link.Flags;
                visual.LinkIndex = (uint)index;
                visual.InventorySlotIndex = (uint)index;
                visual.PowerGraphNodeIndex = (uint)nodeIndex;
                visual.StateHash = Hash32((uint)index);
                VisualStates[index] = visual;
            }
        }

        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        private static ulong Hash64(uint value)
        {
            ulong x = value + 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ExecuteBatteryChargingJob : IJobParallelFor
    {
        // SAFETY: All pointer fields are generation-resolved only after BatteryChargerLogisticsRuntime
        // locks their Vault buffers for the full dispatcher-owned job window. Unlock happens after
        // DispatcherJobFence reports completion, or during teardown forced completion.
        // ALIASING: Links, LinkAup, ExpectedPowerNodeHashes, VisualStates, InventorySlots,
        // PowerNodes, and Counters are distinct Vault buffers. [NoAlias] is therefore valid and
        // lets Burst vectorize scalar link math without assuming overlapping raw pointer ranges.
        // LIFETIME: No pointer is stored outside this job struct. The runtime fail-closes on reentry,
        // mock hydration, missing owner buffers, and resolve failure before any pointer is passed in.
        [NoAlias, NativeDisableUnsafePtrRestriction] public ChargerLinkDTO* Links;
        [NoAlias, NativeDisableUnsafePtrRestriction] public double3* LinkAup;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* ExpectedPowerNodeHashes;
        [NoAlias, NativeDisableUnsafePtrRestriction] public ChargerVisualStateDTO* VisualStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public InventorySlotDTO* InventorySlots;
        [NoAlias, NativeDisableUnsafePtrRestriction] public PowerNodeDTO* PowerNodes;
        [NoAlias, NativeDisableUnsafePtrRestriction] public ChargerAtomicCountersDTO* Counters;
        [NativeSetThreadIndex] public int ThreadIndex;
        public int LinkCount;
        public int InventorySlotCount;
        public int PowerNodeCount;
        public int CounterLaneCount;
        public float DeltaSeconds;
        public float GlobalMaxChargeRate;
        public float EfficiencyCurveExponent;
        public float BatteryCapacity;

        public void Execute(int index)
        {
            if (Links == null ||
                VisualStates == null ||
                InventorySlots == null ||
                PowerNodes == null ||
                Counters == null ||
                (uint)index >= (uint)LinkCount)
            {
                return;
            }

            ref ChargerLinkDTO link = ref UnsafeUtility.AsRef<ChargerLinkDTO>(Links + index);
            uint flags = link.Flags;
            if ((flags & BatteryChargerLogisticsConstants.LinkFlagActive) == 0u)
            {
                WriteVisual(index, in link, 0u, flags, 0f, 0f);
                return;
            }

            int slotIndex = (int)link.InventorySlotIndex;
            int nodeIndex = (int)link.PowerGraphNodeIndex;
            if ((uint)slotIndex >= (uint)InventorySlotCount || (uint)nodeIndex >= (uint)PowerNodeCount)
            {
                MarkUnpowered(index, ref link, flags | BatteryChargerLogisticsConstants.LinkFlagUnpowered);
                return;
            }

            InventorySlotDTO* slotPtr = InventorySlots + slotIndex;
            PowerNodeDTO* nodePtr = PowerNodes + nodeIndex;
            uint expectedNodeHash = ExpectedPowerNodeHashes != null ? ExpectedPowerNodeHashes[index] : 0u;
            PowerNodeDTO nodeSnapshot = *nodePtr;
            if (!math.isfinite(DeltaSeconds) ||
                !math.isfinite(GlobalMaxChargeRate) ||
                !math.isfinite(EfficiencyCurveExponent) ||
                !math.isfinite(BatteryCapacity) ||
                !math.isfinite(link.ChargeRate) ||
                !math.isfinite(link.EfficiencyScalar) ||
                !math.isfinite(nodeSnapshot.Potential) ||
                !math.isfinite(nodeSnapshot.MaxCapacity))
            {
                AddFaultFlags(BatteryChargerLogisticsConstants.TelemetryFlagNaN, (uint)index);
            }

            uint disconnectedMask = PowerGridJacobiConstants.NodeFlagDamaged |
                                    PowerGridJacobiConstants.NodeFlagFlooded |
                                    PowerGridJacobiConstants.NodeFlagOffline;
            if ((nodeSnapshot.Flags & disconnectedMask) != 0u ||
                (expectedNodeHash != 0u && nodeSnapshot.NodeHash != expectedNodeHash))
            {
                uint nextFlags = (flags | BatteryChargerLogisticsConstants.LinkFlagUnpowered) &
                                 ~BatteryChargerLogisticsConstants.LinkFlagFull;
                if (expectedNodeHash != 0u && nodeSnapshot.NodeHash != expectedNodeHash)
                    nextFlags |= BatteryChargerLogisticsConstants.LinkFlagNodeHashMismatch;
                MarkUnpowered(index, ref link, nextFlags);
                AddFaultFlags(BatteryChargerLogisticsConstants.TelemetryFlagNodeDisconnected, (uint)index);
                return;
            }

            ref InventorySlotDTO slot = ref UnsafeUtility.AsRef<InventorySlotDTO>(slotPtr);
            if (slot.ItemHashID == 0u || slot.Quantity == 0u)
            {
                link.Flags = (flags & ~(BatteryChargerLogisticsConstants.LinkFlagFull |
                                        BatteryChargerLogisticsConstants.LinkFlagUnpowered |
                                        BatteryChargerLogisticsConstants.LinkFlagAtomicConflict));
                WriteVisual(index, in link, 0u, link.Flags, 0f, 0f);
                return;
            }

            uint* lockPtr = &slotPtr->ReservedLock;
            if (!TryAcquireSlotLock(lockPtr))
            {
                AtomicFailure(index, ref link);
                return;
            }

            uint finalFlags = flags;
            ref InventorySlotDTO lockedSlot = ref UnsafeUtility.AsRef<InventorySlotDTO>(slotPtr);
            if (lockedSlot.ItemHashID == 0u || lockedSlot.Quantity == 0u)
            {
                finalFlags &= ~(BatteryChargerLogisticsConstants.LinkFlagFull |
                                BatteryChargerLogisticsConstants.LinkFlagUnpowered |
                                BatteryChargerLogisticsConstants.LinkFlagAtomicConflict);
                WriteVisual(index, in link, 0u, finalFlags, 0f, 0f);
                ReleaseSlotLock(lockPtr);
                return;
            }

            uint oldChargeBits = lockedSlot.ConditionFlags;
            if (!math.isfinite(math.asfloat(oldChargeBits)))
                AddFaultFlags(BatteryChargerLogisticsConstants.TelemetryFlagNaN, (uint)index);

            float oldCharge = SanitizeCharge(math.asfloat(oldChargeBits));
            float capacity = math.max(0.0001f, SanitizePositive(BatteryCapacity));
            float percentage = math.saturate(oldCharge * math.rcp(capacity));
            if (percentage >= 0.9999f)
            {
                finalFlags = (finalFlags | BatteryChargerLogisticsConstants.LinkFlagFull) &
                             ~(BatteryChargerLogisticsConstants.LinkFlagUnpowered | BatteryChargerLogisticsConstants.LinkFlagAtomicConflict);
                link.Flags = finalFlags;
                IncrementFull();
                IncrementActive(index, 0f, capacity);
                WriteVisual(index, in link, 2u, finalFlags, 1f, 0f);
                ReleaseSlotLock(lockPtr);
                return;
            }

            float curve = MathLodApproximation.ApproxPow01Curve(1f - percentage, math.max(0.0001f, SanitizePositive(EfficiencyCurveExponent)));
            float rate = math.min(SanitizePositive(link.ChargeRate), SanitizePositive(GlobalMaxChargeRate));
            rate *= math.max(0f, SanitizePositive(link.EfficiencyScalar)) * curve;
            float request = rate * math.max(0f, SanitizePositive(DeltaSeconds));
            float remaining = math.max(0f, capacity - oldCharge);
            if (request <= 0f || remaining <= 0f)
            {
                WriteVisual(index, in link, 0u, finalFlags, percentage, 0f);
                ReleaseSlotLock(lockPtr);
                return;
            }

            float* potentialPtr = &nodePtr->Potential;
            float potential = SanitizePositive(*potentialPtr);
            float nodeCapacity = math.max(0.0001f, SanitizePositive(nodePtr->MaxCapacity));
            float available = potential * nodeCapacity;
            float transfer = math.min(math.min(request, remaining), available);
            if (transfer <= 0f)
            {
                MarkUnpowered(index, ref link, finalFlags | BatteryChargerLogisticsConstants.LinkFlagUnpowered);
                ReleaseSlotLock(lockPtr);
                return;
            }

            float nextCharge = math.min(capacity, oldCharge + transfer);
            uint nextChargeBits = math.asuint(nextCharge);
            uint* conditionPtr = &slotPtr->ConditionFlags;
            if (!CompareExchangeUInt(conditionPtr, oldChargeBits, nextChargeBits))
            {
                AtomicFailure(index, ref link);
                ReleaseSlotLock(lockPtr);
                return;
            }

            float nextPotential = math.max(0f, potential - transfer * math.rcp(nodeCapacity));
            if (!CompareExchangeFloat(potentialPtr, potential, nextPotential))
            {
                ExchangeUInt(conditionPtr, oldChargeBits);
                AtomicFailure(index, ref link);
                ReleaseSlotLock(lockPtr);
                return;
            }

            finalFlags = (finalFlags & ~(BatteryChargerLogisticsConstants.LinkFlagUnpowered |
                                         BatteryChargerLogisticsConstants.LinkFlagAtomicConflict |
                                         BatteryChargerLogisticsConstants.LinkFlagFull));
            if (nextCharge >= capacity - 0.0001f)
            {
                finalFlags |= BatteryChargerLogisticsConstants.LinkFlagFull;
                IncrementFull();
            }

            link.Flags = finalFlags;
            IncrementActive(index, transfer, nextCharge);
            WriteVisual(index, in link, (finalFlags & BatteryChargerLogisticsConstants.LinkFlagFull) != 0u ? 2u : 1u, finalFlags, math.saturate(nextCharge * math.rcp(capacity)), transfer);
            ReleaseSlotLock(lockPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarkUnpowered(int index, ref ChargerLinkDTO link, uint flags)
        {
            link.Flags = flags;
            IncrementUnpowered();
            WriteVisual(index, in link, 0u, flags, 0f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AtomicFailure(int index, ref ChargerLinkDTO link)
        {
            link.Flags |= BatteryChargerLogisticsConstants.LinkFlagAtomicConflict;
            IncrementAtomicFailures((uint)index);
            WriteVisual(index, in link, 0u, link.Flags, 0f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteVisual(int index, in ChargerLinkDTO link, uint status, uint flags, float charge01, float energyDraw)
        {
            if (VisualStates == null || (uint)index >= (uint)LinkCount)
                return;

            ChargerVisualStateDTO visual = default;
            visual.Status = status;
            visual.Flags = flags;
            visual.Charge01 = math.saturate(charge01);
            visual.EnergyDraw01 = math.saturate(energyDraw);
            visual.LinkIndex = (uint)index;
            visual.InventorySlotIndex = link.InventorySlotIndex;
            visual.PowerGraphNodeIndex = link.PowerGraphNodeIndex;
            visual.StateHash = Mix(Mix(Mix(2166136261u, (uint)index), math.asuint(visual.Charge01)), flags);
            VisualStates[index] = visual;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementActive(int index, float transfer, float charge)
        {
            ChargerAtomicCountersDTO* lane = CounterLane();
            lane->ActiveLinks++;
            lane->TotalEnergyMilli += ClampMilli(transfer);
            lane->ChargeMilliSum += ClampMilli(charge);
            if (transfer > 0f && math.isfinite(transfer))
                lane->LastActiveLink = (uint)index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementFull()
        {
            CounterLane()->FullLinks++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementUnpowered()
        {
            CounterLane()->UnpoweredLinks++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementAtomicFailures(uint index)
        {
            CounterLane()->AtomicFailures++;
            AddFaultFlags(BatteryChargerLogisticsConstants.TelemetryFlagAtomicConflict, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddFaultFlags(uint flags, uint index)
        {
            ChargerAtomicCountersDTO* lane = CounterLane();
            lane->FaultFlags |= flags;
            lane->LastFaultLink = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChargerAtomicCountersDTO* CounterLane()
        {
            int laneCount = math.max(1, CounterLaneCount);
            int lane = ThreadIndex % laneCount;
            if (lane < 0)
                lane = 0;
            return Counters + lane;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampMilli(float value)
        {
            float scaled = math.round(math.max(0f, SanitizePositive(value)) * 1000f);
            return (int)math.min(scaled, int.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryAcquireSlotLock(uint* lockPtr)
        {
            return CompareExchangeUInt(lockPtr, 0u, BatteryChargerLogisticsConstants.LockToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReleaseSlotLock(uint* lockPtr)
        {
            ExchangeUInt(lockPtr, 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CompareExchangeFloat(float* ptr, float expected, float desired)
        {
            uint expectedBits = math.asuint(expected);
            uint desiredBits = math.asuint(desired);
            return CompareExchangeUInt((uint*)ptr, expectedBits, desiredBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CompareExchangeUInt(uint* ptr, uint expected, uint desired)
        {
            ref int location = ref UnsafeUtility.AsRef<int>(ptr);
            int observed = Interlocked.CompareExchange(ref location, unchecked((int)desired), unchecked((int)expected));
            return unchecked((uint)observed) == expected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExchangeUInt(uint* ptr, uint value)
        {
            ref int location = ref UnsafeUtility.AsRef<int>(ptr);
            Interlocked.Exchange(ref location, unchecked((int)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeCharge(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, 1f) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    #if UNITY_EDITOR
    public static class BatteryChargerProfileCsvParser
    {
        public static bool TryParseProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<ChargerProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int lineStart = 0;
            while (lineStart < csvBytes.Length && profileCount < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = Trim(csvBytes.Slice(lineStart, lineEnd - lineStart));
                if (TryParseLine(line, out ChargerProfileDTO profile))
                    profiles[profileCount++] = profile;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return profileCount > 0;
        }

        public static bool TryParseProfiles(ReadOnlySpan<byte> csvBytes, Span<ChargerProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (profiles.Length <= 0)
                return false;

            int lineStart = 0;
            while (lineStart < csvBytes.Length && profileCount < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = Trim(csvBytes.Slice(lineStart, lineEnd - lineStart));
                if (TryParseLine(line, out ChargerProfileDTO profile))
                    profiles[profileCount++] = profile;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return profileCount > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out ChargerProfileDTO profile)
        {
            profile = default;
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> name = NextField(ref line);
            if (name.Length == 0 || IsHeader(name))
                return false;

            if (!TryParseFiniteFloat(NextField(ref line), out float maxChargeRate) ||
                !TryParseFiniteFloat(NextField(ref line), out float efficiencyScalar) ||
                !TryParseFiniteFloat(NextField(ref line), out float efficiencyCurveExponent) ||
                !TryParseFiniteFloat(NextField(ref line), out float gridLoadScalar) ||
                Trim(line).Length != 0)
            {
                return false;
            }

            profile.ProfileHash = Fnva32(name);
            profile.MaxChargeRate = math.max(0f, maxChargeRate);
            profile.EfficiencyScalar = math.max(0f, efficiencyScalar);
            profile.EfficiencyCurveExponent = math.max(0.0001f, efficiencyCurveExponent);
            profile.GridLoadScalar = math.max(0f, gridLoadScalar);
            profile.Flags = profile.MaxChargeRate > 0f ? 1u : 0u;
            return true;
        }

        private static ReadOnlySpan<byte> NextField(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> last = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                return last;
            }

            ReadOnlySpan<byte> field = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return field;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsSpace(value[start]))
                start++;
            while (end >= start && IsSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsHeader(ReadOnlySpan<byte> value)
        {
            return value.Length >= 4 &&
                   ToLower(value[0]) == (byte)'n' &&
                   ToLower(value[1]) == (byte)'a' &&
                   ToLower(value[2]) == (byte)'m' &&
                   ToLower(value[3]) == (byte)'e';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static uint Fnva32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static bool TryParseFiniteFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            bool sawDigit = false;

            float result = 0f;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                sawDigit = true;
                result = (result * 10f) + (value[index] - (byte)'0');
                if (!math.isfinite(result))
                    return false;
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    sawDigit = true;
                    result += (value[index] - (byte)'0') * place;
                    if (!math.isfinite(result))
                        return false;
                    place *= 0.1f;
                    index++;
                }
            }

            if (!sawDigit || index != value.Length)
                return false;

            parsed = result * sign;
            return math.isfinite(parsed);
        }
    }
    #endif
}
