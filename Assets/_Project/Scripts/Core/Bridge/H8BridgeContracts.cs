using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;

namespace Hecton8.Core.Bridge
{
    internal static class H8BridgeContractLayout
    {
        public const int PrefabMappingEntryStrideBytes = 64;
        public const int PrefabLoreLinkEntryStrideBytes = 32;
        public const int DesignValueEntryStrideBytes = 32;
        public const int FacadeTelemetryEntryStrideBytes = 64;
        public const int FacadeTelemetryDumpHeaderStrideBytes = 32;
        public const int InputFacadeBindingEntryStrideBytes = 32;
        public const int FacadeMacroHeaderStrideBytes = 64;
        public const int FloatUInt32UnionStrideBytes = 4;
    }

    [Flags]
    public enum H8PrefabMappingFlags : ushort
    {
        None = 0,
        Addressable = 1 << 0,
        HasLore = 1 << 1,
        HasAcousticSignature = 1 << 2,
        HighTierVisualOverkill = 1 << 3,
        UsesOneDimensionalLut = 1 << 4
    }

    [Flags]
    public enum H8DesignValueFlags : ushort
    {
        None = 0,
        Critical = 1 << 0,
        LiveTuning = 1 << 1,
        DesignerOverride = 1 << 2,
        VramAffecting = 1 << 3,
        UsesOneDimensionalLut = 1 << 4,
        HighTierVisualOverkill = 1 << 5
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.PrefabMappingEntryStrideBytes)]
    public struct H8PrefabMappingEntry
    {
        [FieldOffset(0)] public uint HashID;
        [FieldOffset(4)] public uint AddressHash;
        [FieldOffset(8)] public uint LoreHash;
        [FieldOffset(12)] public uint AcousticSignatureHash;
        [FieldOffset(16)] public long EstimatedVramBytes;
        [FieldOffset(24)] public uint RuntimePrefabId;
        [FieldOffset(28)] public ushort Flags;
        [FieldOffset(30)] public ushort Reserved0;
        [FieldOffset(32)] public uint OneDimensionalLutHash;
        [FieldOffset(36)] public uint HighTierVisualHash;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.PrefabLoreLinkEntryStrideBytes)]
    public struct H8PrefabLoreLinkEntry
    {
        [FieldOffset(0)] public uint PrefabHash;
        [FieldOffset(4)] public uint LoreHash;
        [FieldOffset(8)] public uint AcousticSignatureHash;
        [FieldOffset(12)] public uint OneDimensionalLutHash;
        [FieldOffset(16)] public uint HighTierVisualHash;
        [FieldOffset(20)] public ushort Flags;
        [FieldOffset(22)] public ushort Reserved0;
        [FieldOffset(24)] public uint Reserved1;
        [FieldOffset(28)] private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.DesignValueEntryStrideBytes)]
    public struct H8DesignValueEntry
    {
        [FieldOffset(0)] public uint FieldHash;
        [FieldOffset(4)] public int OffsetBytes;
        [FieldOffset(8)] public float Value;
        [FieldOffset(12)] public float SafeDefault;
        [FieldOffset(16)] public float MinValue;
        [FieldOffset(20)] public float MaxValue;
        [FieldOffset(24)] public uint LutSwapHash;
        [FieldOffset(28)] public ushort Flags;
        [FieldOffset(30)] public ushort Reserved;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.FacadeTelemetryEntryStrideBytes)]
    public struct H8FacadeTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint FacadeHash;
        [FieldOffset(8)] public uint FieldHash;
        [FieldOffset(12)] public int OffsetBytes;
        [FieldOffset(16)] public float OldValue;
        [FieldOffset(20)] public float NewValue;
        [FieldOffset(24)] public float SafeDefault;
        [FieldOffset(28)] public uint LutSwapHash;
        [FieldOffset(32)] public ushort Flags;
        [FieldOffset(34)] public ushort Reserved;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.FacadeTelemetryDumpHeaderStrideBytes)]
    public struct H8FacadeTelemetryDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint EntrySizeBytes;
        [FieldOffset(16)] public uint Cursor;
        [FieldOffset(20)] public uint Capacity;
        [FieldOffset(24)] public uint PayloadHash;
        [FieldOffset(28)] public uint Reserved;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.InputFacadeBindingEntryStrideBytes)]
    public struct H8InputFacadeBindingEntry
    {
        [FieldOffset(0)] public uint ActionNameHash;
        [FieldOffset(4)] public uint ButtonMask;
        [FieldOffset(8)] public byte PlayerCommand;
        [FieldOffset(9)] public byte Flags;
        [FieldOffset(10)] public ushort Reserved0;
        [FieldOffset(12)] public uint DisplayGroupHash;
        [FieldOffset(16)] public uint Reserved1;
        [FieldOffset(20)] public uint Reserved2;
        [FieldOffset(24)] public uint Reserved3;
        [FieldOffset(28)] private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.FacadeMacroHeaderStrideBytes)]
    public struct H8FacadeMacroHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint FacadeHash;
        [FieldOffset(12)] public uint LastChangedFieldHash;
        [FieldOffset(16)] public uint FieldCount;
        [FieldOffset(20)] public uint PrefabCount;
        [FieldOffset(24)] public uint InputBindingCount;
        [FieldOffset(28)] public uint Checksum;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public long EstimatedVramBytes;
        [FieldOffset(48)] public uint OneDimensionalLutHash;
        [FieldOffset(52)] public uint HighTierVisualHash;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    public static class H8BridgeHashes
    {
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;
        public const uint DesignFacade = 0xD01FACA3u;
        public const uint PrefabRegistry = 0xB16D0001u;
        public const uint InputFacade = 0x1A7F0008u;
        public const uint MacroHeaderMagic = 0x48384246u; // H8BF
        public const uint MacroHeaderVersion = 1u;
        public const uint TelemetryDumpMagic = 0x48384244u; // H8BD
        public const uint TelemetryDumpVersion = 1u;
        public const uint AcousticSeed = 0xA60C57C5u;
        public const uint LoreSeed = 0x10AE0001u;
        public const uint AddressSeed = 0xADAD0001u;
        public const uint LutSeed = 0x1D105EEDu;
        public const uint VisualOverkillSeed = 0x4090F00Du;
        public const uint BridgeHeartbeat = 0xB10B0001u;
        public const uint BridgeLayoutFault = 0xB10B0002u;
        public const ulong FacadeMacroHeaderSectorHash = 0xFACADEB8D0D00001UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value & 0xFFu;
                hash *= FnvPrime;
                hash ^= (value >> 8) & 0xFFu;
                hash *= FnvPrime;
                hash ^= (value >> 16) & 0xFFu;
                hash *= FnvPrime;
                hash ^= (value >> 24) & 0xFFu;
                hash *= FnvPrime;
                return hash;
            }
        }

        public static uint ComputeFnv1A(ReadOnlySpan<char> value, uint seed = FnvOffset)
        {
            unchecked
            {
                uint hash = seed == 0u ? FnvOffset : seed;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= FnvPrime;
                }

                return hash == 0u ? FnvOffset : hash;
            }
        }

        public static uint ComputeFnv1A(string value, uint seed = FnvOffset)
        {
            return string.IsNullOrEmpty(value) ? (seed == 0u ? FnvOffset : seed) : ComputeFnv1A(value.AsSpan(), seed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FloatToUInt32Bits(float value)
        {
            FloatUInt32Union bits = default;
            bits.FloatValue = value;
            return bits.UIntValue;
        }

        public static uint ComputeDesignBufferChecksum(ReadOnlySpan<H8DesignValueEntry> entries)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < entries.Length; i++)
            {
                H8DesignValueEntry entry = entries[i];
                hash = Mix(hash, entry.FieldHash);
                hash = Mix(hash, unchecked((uint)entry.OffsetBytes));
                hash = Mix(hash, FloatToUInt32Bits(entry.Value));
                hash = Mix(hash, entry.LutSwapHash);
                hash = Mix(hash, entry.Flags);
            }

            return hash;
        }

        [StructLayout(LayoutKind.Explicit, Size = H8BridgeContractLayout.FloatUInt32UnionStrideBytes)]
        private struct FloatUInt32Union
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public uint UIntValue;
        }
    }
}
