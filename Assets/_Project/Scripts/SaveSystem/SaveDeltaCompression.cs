using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    internal readonly struct SaveVoxelDeltaRun8
    {
        public readonly ushort StartIndex;
        public readonly ushort RunLength;
        public readonly sbyte SdfValue;
        public readonly byte MaterialId;
        public readonly byte Flags;
        public readonly byte Reserved0;

        public SaveVoxelDeltaRun8(ushort startIndex, ushort runLength, sbyte sdfValue, byte materialId, byte flags)
        {
            StartIndex = startIndex;
            RunLength = runLength;
            SdfValue = sdfValue;
            MaterialId = materialId;
            Flags = flags;
            Reserved0 = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    internal readonly struct PackedEntityState32
    {
        public readonly uint Value;

        public PackedEntityState32(uint value)
        {
            Value = value;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    internal readonly struct PackedSuitUpgradeState64
    {
        public readonly ulong Value;

        public PackedSuitUpgradeState64(ulong value)
        {
            Value = value;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 6)]
    internal readonly struct QuantizedLocalHalf3
    {
        public readonly ushort X;
        public readonly ushort Y;
        public readonly ushort Z;

        public QuantizedLocalHalf3(float3 localOffsetMeters)
        {
            X = (ushort)math.f32tof16(localOffsetMeters.x);
            Y = (ushort)math.f32tof16(localOffsetMeters.y);
            Z = (ushort)math.f32tof16(localOffsetMeters.z);
        }

        public float3 ToFloat3()
        {
            return new float3(
                math.f16tof32(X),
                math.f16tof32(Y),
                math.f16tof32(Z));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 18)]
    internal readonly struct QuantizedAupSectorHalf3
    {
        public readonly int SectorX;
        public readonly int SectorY;
        public readonly int SectorZ;
        public readonly QuantizedLocalHalf3 LocalOffset;

        public QuantizedAupSectorHalf3(int3 sectorId, QuantizedLocalHalf3 localOffset)
        {
            SectorX = sectorId.x;
            SectorY = sectorId.y;
            SectorZ = sectorId.z;
            LocalOffset = localOffset;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct StrictSaveFileHeader64
    {
        public ulong Magic;
        public uint Version;
        public double PlayTimeSeconds;
        public long AupX;
        public long AupY;
        public long AupZ;
        public ulong Checksum;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    internal struct SaveChunkHeader32
    {
        public ulong ChunkKey;
        public uint PayloadOffset;
        public uint PayloadLength;
        public ulong PayloadHash64;
        public uint Flags;
        public uint Reserved;
    }

    internal static unsafe class SaveDeltaCompression
    {
        internal const ulong StrictHeaderMagic = 0x0038544345485F48UL; // "H_HECT8\0" little-endian marker.
        private const uint EntityHealthMask = 0x000003FFu;
        private const uint EntityHungerMask = 0x000FFC00u;
        private const uint EntityStatusMask = 0xFFF00000u;
        private const int EntityHungerShift = 10;
        private const int EntityStatusShift = 20;
        private const uint TenBitMax = 1023u;
        private const float InvTenBitMax = 1f / TenBitMax;

        internal static PackedEntityState32 PackEntityState32(float health01, float hunger01, uint statusMask12)
        {
            uint health = QuantizeUnit10(health01);
            uint hunger = QuantizeUnit10(hunger01);
            uint status = statusMask12 & 0xFFFu;
            return new PackedEntityState32(
                health |
                (hunger << EntityHungerShift) |
                (status << EntityStatusShift));
        }

        internal static void UnpackEntityState32(PackedEntityState32 packed, out float health01, out float hunger01, out uint statusMask12)
        {
            uint value = packed.Value;
            health01 = (value & EntityHealthMask) * InvTenBitMax;
            hunger01 = ((value & EntityHungerMask) >> EntityHungerShift) * InvTenBitMax;
            statusMask12 = (value & EntityStatusMask) >> EntityStatusShift;
        }

        internal static PackedSuitUpgradeState64 PackSuitUpgrades64(ulong suitUpgrades)
        {
            return new PackedSuitUpgradeState64(suitUpgrades & SuitUpgradeResolver.SupportedMask);
        }

        internal static ulong UnpackSuitUpgrades64(PackedSuitUpgradeState64 packed)
        {
            return packed.Value & SuitUpgradeResolver.SupportedMask;
        }

        internal static QuantizedAupSectorHalf3 QuantizeAupSectorHalf3(float3 absoluteWorldMeters, int sectorSizeMeters)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            float invSectorSize = math.rcp((float)safeSectorSize);
            int3 sector = (int3)math.floor(absoluteWorldMeters * invSectorSize);
            float3 sectorOrigin = new float3(sector) * safeSectorSize;
            return new QuantizedAupSectorHalf3(sector, new QuantizedLocalHalf3(absoluteWorldMeters - sectorOrigin));
        }

        internal static float3 DequantizeAupSectorHalf3(in QuantizedAupSectorHalf3 packed, int sectorSizeMeters)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            return (new float3(packed.SectorX, packed.SectorY, packed.SectorZ) * safeSectorSize) +
                   packed.LocalOffset.ToFloat3();
        }

        internal static StrictSaveFileHeader64 BuildStrictHeader64(
            uint version,
            double playTimeSeconds,
            long aupX,
            long aupY,
            long aupZ,
            ulong checksum)
        {
            return new StrictSaveFileHeader64
            {
                Magic = StrictHeaderMagic,
                Version = version,
                PlayTimeSeconds = playTimeSeconds,
                AupX = aupX,
                AupY = aupY,
                AupZ = aupZ,
                Checksum = checksum,
                Reserved0 = 0u,
                Reserved1 = 0u,
                Reserved2 = 0u
            };
        }

        internal static uint ByteSwap32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        internal static ulong ByteSwap64(ulong value)
        {
            return ((ulong)ByteSwap32((uint)value) << 32) |
                   ByteSwap32((uint)(value >> 32));
        }

        internal static void ByteSwap32InPlace(uint* values, int count)
        {
            if (values == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
                values[i] = ByteSwap32(values[i]);
        }

        internal static bool TryBlitNativeBytes(NativeArray<byte> source, int byteCount, void* destination, int destinationCapacity)
        {
            if (!source.IsCreated || destination == null)
                return false;

            int safeByteCount = math.clamp(byteCount, 0, source.Length);
            if (safeByteCount <= 0)
                return true;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            bool copied = UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationCapacity, sourcePtr, safeByteCount);
            if (!copied)
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveDeltaCompression));

            return copied;
        }

        private static uint QuantizeUnit10(float value01)
        {
            return (uint)math.clamp((int)math.round(math.saturate(value01) * TenBitMax), 0, (int)TenBitMax);
        }
    }
}
