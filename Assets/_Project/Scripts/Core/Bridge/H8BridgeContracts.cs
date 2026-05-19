using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;

namespace Hecton8.Core.Bridge
{
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
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct H8PrefabMappingEntry
    {
        public uint HashID;
        public uint AddressHash;
        public uint LoreHash;
        public uint AcousticSignatureHash;
        public long EstimatedVramBytes;
        public uint RuntimePrefabId;
        public ushort Flags;
        public ushort Reserved0;
        public uint OneDimensionalLutHash;
        public uint HighTierVisualHash;
        public uint Reserved1;
        private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8PrefabLoreLinkEntry
    {
        public uint PrefabHash;
        public uint LoreHash;
        public uint AcousticSignatureHash;
        public uint OneDimensionalLutHash;
        public uint HighTierVisualHash;
        public ushort Flags;
        public ushort Reserved0;
        public uint Reserved1;
        private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8DesignValueEntry
    {
        public uint FieldHash;
        public int OffsetBytes;
        public float Value;
        public float SafeDefault;
        public float MinValue;
        public float MaxValue;
        public uint LutSwapHash;
        public ushort Flags;
        public ushort Reserved;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct H8FacadeTelemetryEntry
    {
        public uint Frame;
        public uint FacadeHash;
        public uint FieldHash;
        public int OffsetBytes;
        public float OldValue;
        public float NewValue;
        public float SafeDefault;
        public uint LutSwapHash;
        public ushort Flags;
        public ushort Reserved;
        private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8FacadeTelemetryDumpHeader
    {
        public uint Magic;
        public uint Version;
        public uint EntryCount;
        public uint EntrySizeBytes;
        public uint Cursor;
        public uint Capacity;
        public uint PayloadHash;
        public uint Reserved;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8InputFacadeBindingEntry
    {
        public uint ActionNameHash;
        public uint ButtonMask;
        public byte PlayerCommand;
        public byte Flags;
        public ushort Reserved0;
        public uint DisplayGroupHash;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        private uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct H8FacadeMacroHeader
    {
        public uint Magic;
        public uint Version;
        public uint FacadeHash;
        public uint LastChangedFieldHash;
        public uint FieldCount;
        public uint PrefabCount;
        public uint InputBindingCount;
        public uint Checksum;
        public uint Frame;
        public uint Flags;
        public long EstimatedVramBytes;
        public uint OneDimensionalLutHash;
        public uint HighTierVisualHash;
        public uint Reserved0;
        public uint Reserved1;
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

        [StructLayout(LayoutKind.Explicit, Size = 4)]
        private struct FloatUInt32Union
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public uint UIntValue;
        }
    }
}
