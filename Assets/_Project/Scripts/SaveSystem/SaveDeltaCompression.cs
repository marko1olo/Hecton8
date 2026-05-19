using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal readonly struct SaveVoxelDeltaRun5
    {
        public readonly ushort StartIndex;
        public readonly ushort RunLength;
        public readonly byte SdfValue;
        public readonly byte Reserved0;
        public readonly ushort Reserved1;

        public SaveVoxelDeltaRun5(ushort startIndex, byte sdfValue, ushort runLength)
        {
            StartIndex = startIndex;
            RunLength = runLength;
            SdfValue = sdfValue;
            Reserved0 = 0;
            Reserved1 = 0;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 8)]
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

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal readonly struct PackedEntityState32
    {
        public readonly uint Value;
        public readonly uint Reserved0;

        public PackedEntityState32(uint value)
        {
            Value = value;
            Reserved0 = 0u;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal readonly struct PackedSuitUpgradeState64
    {
        public readonly ulong Value;

        public PackedSuitUpgradeState64(ulong value)
        {
            Value = value;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal readonly struct QuantizedLocalHalf3
    {
        public readonly ushort X;
        public readonly ushort Y;
        public readonly ushort Z;
        public readonly ushort Reserved0;

        public QuantizedLocalHalf3(float3 localOffsetMeters)
        {
            X = (ushort)math.f32tof16(localOffsetMeters.x);
            Y = (ushort)math.f32tof16(localOffsetMeters.y);
            Z = (ushort)math.f32tof16(localOffsetMeters.z);
            Reserved0 = 0;
        }

        public float3 ToFloat3()
        {
            return new float3(
                math.f16tof32(X),
                math.f16tof32(Y),
                math.f16tof32(Z));
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    internal readonly struct QuantizedAupSectorHalf3
    {
        public readonly int SectorX;
        public readonly int SectorY;
        public readonly int SectorZ;
        public readonly QuantizedLocalHalf3 LocalOffset;
        public readonly uint Reserved0;

        public QuantizedAupSectorHalf3(int3 sectorId, QuantizedLocalHalf3 localOffset)
        {
            SectorX = sectorId.x;
            SectorY = sectorId.y;
            SectorZ = sectorId.z;
            LocalOffset = localOffset;
            Reserved0 = 0u;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal readonly struct SaveAupLocalOffset32
    {
        public readonly uint SectorKey;
        public readonly uint ShiftFrameId;
        public readonly float LocalOffsetX;
        public readonly float LocalOffsetY;
        public readonly float LocalOffsetZ;
        public readonly uint Flags;
        public readonly uint _pad0;
        public readonly uint _pad1;

        public SaveAupLocalOffset32(uint sectorKey, uint shiftFrameId, float3 localOffsetMeters, uint flags)
        {
            SectorKey = sectorKey;
            ShiftFrameId = shiftFrameId;
            LocalOffsetX = localOffsetMeters.x;
            LocalOffsetY = localOffsetMeters.y;
            LocalOffsetZ = localOffsetMeters.z;
            Flags = flags;
            _pad0 = 0u;
            _pad1 = 0u;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct MockStatePayload
    {
        public SaveAupLocalOffset32 LocalAup;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct StrictSaveFileHeader64
    {
        public ulong Magic;
        public double PlayTimeSeconds;
        public long AupX;
        public long AupY;
        public long AupZ;
        public ulong Checksum;
        public uint Version;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct SaveChunkHeader32
    {
        public ulong ChunkKey;
        public uint PayloadOffset;
        public uint PayloadLength;
        public ulong PayloadHash64;
        public uint Flags;
        public uint Reserved;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Size = 264)]
    internal unsafe struct SectorPayloadDTO
    {
        public const int FixedPayloadBytes = 256;

        public uint SectorHash;
        public uint DataLength;
        public fixed byte Data[FixedPayloadBytes];
    }

    internal static unsafe class SaveDeltaCompression
    {
        internal const ulong StrictHeaderMagic = 0x0038544345485F48UL; // "H_HECT8\0" little-endian marker.
        internal const byte DeletedEntityMask = 0xFF;
        internal const int MinimumLz4PayloadBytes = 1024;
        internal const ulong SupportedSuitUpgradeMask = 0x00000000000007FFUL;
        private const uint EntityHealthMask = 0x000003FFu;
        private const uint EntityHungerMask = 0x000FFC00u;
        private const uint EntityStatusMask = 0xFFF00000u;
        private const int EntityHungerShift = 10;
        private const int EntityStatusShift = 20;
        private const uint TenBitMax = 1023u;
        private const float InvTenBitMax = 1f / TenBitMax;
        private const float LocalOffsetMillimeterScale = 1000f;

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
            return new PackedSuitUpgradeState64(suitUpgrades & SupportedSuitUpgradeMask);
        }

        internal static ulong UnpackSuitUpgrades64(PackedSuitUpgradeState64 packed)
        {
            return packed.Value & SupportedSuitUpgradeMask;
        }

        internal static QuantizedAupSectorHalf3 QuantizeAupSectorHalf3(double3 absoluteUniverseMeters, int sectorSizeMeters)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            double invSectorSize = 1d / safeSectorSize;
            int3 sector = (int3)math.floor(absoluteUniverseMeters * invSectorSize);
            double3 sectorOrigin = new double3(sector) * safeSectorSize;
            double3 localOffset = absoluteUniverseMeters - sectorOrigin;
            return new QuantizedAupSectorHalf3(
                sector,
                new QuantizedLocalHalf3(new float3(
                    (float)localOffset.x,
                    (float)localOffset.y,
                    (float)localOffset.z)));
        }

        internal static SaveAupLocalOffset32 QuantizeAupLocalOffset32(
            double3 absoluteUniverseMeters,
            uint sectorKey,
            int sectorSizeMeters,
            uint shiftFrameId,
            uint flags)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            double invSectorSize = 1d / safeSectorSize;
            int3 sector = (int3)math.floor(absoluteUniverseMeters * invSectorSize);
            double3 sectorOrigin = new double3(sector) * safeSectorSize;
            return QuantizeAupLocalOffset32(
                absoluteUniverseMeters,
                sectorOrigin,
                sectorKey,
                safeSectorSize,
                shiftFrameId,
                flags);
        }

        internal static SaveAupLocalOffset32 QuantizeAupLocalOffset32(
            double3 absoluteUniverseMeters,
            double3 sectorOriginMeters,
            uint sectorKey,
            int sectorSizeMeters,
            uint shiftFrameId,
            uint flags)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            double3 localOffset = absoluteUniverseMeters - sectorOriginMeters;
            float upper = math.max(0f, safeSectorSize - 0.001f);
            float3 local = new float3(
                QuantizeLocalOffsetMillimeters(localOffset.x, upper),
                QuantizeLocalOffsetMillimeters(localOffset.y, upper),
                QuantizeLocalOffsetMillimeters(localOffset.z, upper));
            return new SaveAupLocalOffset32(sectorKey, shiftFrameId, local, flags);
        }

        private static float QuantizeLocalOffsetMillimeters(double value, float upper)
        {
            float local = (float)value;
            if (!math.isfinite(local))
                return 0f;

            float quantized = math.round(local * LocalOffsetMillimeterScale) / LocalOffsetMillimeterScale;
            return math.clamp(quantized, 0f, upper);
        }

        internal static double3 DequantizeAupSectorHalf3(in QuantizedAupSectorHalf3 packed, int sectorSizeMeters)
        {
            int safeSectorSize = math.max(1, sectorSizeMeters);
            return (new double3(packed.SectorX, packed.SectorY, packed.SectorZ) * safeSectorSize) +
                   new double3(packed.LocalOffset.ToFloat3());
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

        internal static StrictSaveFileHeader64 GenerateMockSaveSchema()
        {
            return BuildStrictHeader64(
                1u,
                0d,
                0L,
                0L,
                0L,
                0x534348454D413634UL);
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

        internal static JobHandle ScheduleEndianSwap32IfNeeded(
            NativeArray<uint> words,
            JobHandle dependency = default)
        {
            if (System.BitConverter.IsLittleEndian || !words.IsCreated || words.Length <= 0)
                return dependency;

            return new EndianSwap32Job
            {
                Words = words
            }.Schedule(words.Length, 64, dependency);
        }

        internal static float ComputeCompressionSavedRatio01(int rawBytes, int compressedBytes)
        {
            if (rawBytes <= 0 || compressedBytes < 0)
                return 0f;

            return math.saturate(1f - ((float)compressedBytes / rawBytes));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EndianSwap32Job : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<uint> Words;

            public void Execute(int index)
            {
                Words[index] = ByteSwap32(Words[index]);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct ActiveRecordCompactionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceRecords;
            [NoAlias]
            public NativeArray<byte> DestinationRecords;
            [NoAlias]
            public NativeArray<int> ActiveRecordCount;
            public int RecordStrideBytes;
            public int DeletedFlagOffsetBytes;
            public int RecordCount;

            public void Execute()
            {
                if (!SourceRecords.IsCreated ||
                    !DestinationRecords.IsCreated ||
                    !ActiveRecordCount.IsCreated ||
                    ActiveRecordCount.Length <= 0 ||
                    RecordStrideBytes <= 0 ||
                    DeletedFlagOffsetBytes < 0 ||
                    DeletedFlagOffsetBytes >= RecordStrideBytes ||
                    RecordCount <= 0)
                {
                    return;
                }

                int sourceBytes = SourceRecords.Length;
                int destinationBytes = DestinationRecords.Length;
                int writeRecord = 0;
                for (int i = 0; i < RecordCount; i++)
                {
                    int sourceOffset = i * RecordStrideBytes;
                    if (sourceOffset < 0 || sourceOffset > sourceBytes - RecordStrideBytes)
                        break;

                    if (SourceRecords[sourceOffset + DeletedFlagOffsetBytes] == DeletedEntityMask)
                        continue;

                    int destinationOffset = writeRecord * RecordStrideBytes;
                    if (destinationOffset < 0 || destinationOffset > destinationBytes - RecordStrideBytes)
                        break;

                    byte* src = (byte*)SourceRecords.GetUnsafeReadOnlyPtr() + sourceOffset;
                    byte* dst = (byte*)DestinationRecords.GetUnsafePtr() + destinationOffset;
                    UnsafeUtility.MemCpy(dst, src, RecordStrideBytes);
                    writeRecord++;
                }

                ActiveRecordCount[0] = writeRecord;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MockSaveDataGeneratorJob : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<SectorPayloadDTO> Payloads;
            public uint Seed;

            public void Execute(int index)
            {
                if (!Payloads.IsCreated)
                    return;

                uint state = Seed ^ ((uint)index * 0x9E3779B9u);
                SectorPayloadDTO dto = default;
                dto.SectorHash = Mix32(state);
                dto.DataLength = SectorPayloadDTO.FixedPayloadBytes;
                byte* data = dto.Data;
                for (int i = 0; i < SectorPayloadDTO.FixedPayloadBytes; i++)
                {
                    state = Mix32(state + (uint)i);
                    data[i] = unchecked((byte)(state >> 24));
                }

                Payloads[index] = dto;
            }

            private static uint Mix32(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelRleCompressionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceDensityIds;
            [NoAlias]
            public NativeArray<SaveVoxelDeltaRun8> Runs;
            [NoAlias]
            public NativeArray<int> RunCount;
            public byte MaterialId;
            public byte Flags;

            public void Execute()
            {
                if (!SourceDensityIds.IsCreated ||
                    !Runs.IsCreated ||
                    !RunCount.IsCreated ||
                    RunCount.Length <= 0)
                {
                    return;
                }

                int sourceLength = math.min(SourceDensityIds.Length, ushort.MaxValue + 1);
                int read = 0;
                int write = 0;
                while (read < sourceLength)
                {
                    byte value = SourceDensityIds[read];
                    int start = read;
                    int run = 1;
                    while (read + run < sourceLength &&
                           run < ushort.MaxValue &&
                           SourceDensityIds[read + run] == value)
                    {
                        run++;
                    }

                    if (write >= Runs.Length)
                    {
                        RunCount[0] = -1;
                        return;
                    }

                    Runs[write++] = new SaveVoxelDeltaRun8(
                        (ushort)start,
                        (ushort)run,
                        unchecked((sbyte)value),
                        MaterialId,
                        Flags);
                    read += run;
                }

                RunCount[0] = write;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct Lz4BlockCompressionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [NoAlias]
            public NativeArray<byte> Destination;
            [NoAlias]
            public NativeArray<int> HashTable;
            [NoAlias]
            public NativeArray<int> ResultLength;

            public void Execute()
            {
                if (!Source.IsCreated ||
                    !Destination.IsCreated ||
                    !HashTable.IsCreated ||
                    !ResultLength.IsCreated ||
                    ResultLength.Length <= 0)
                {
                    return;
                }

                int sourceLength = Source.Length;
                if (sourceLength < MinimumLz4PayloadBytes)
                {
                    ResultLength[0] = -2;
                    return;
                }

                for (int i = 0; i < HashTable.Length; i++)
                    HashTable[i] = -1;

                int anchor = 0;
                int read = 0;
                int write = 0;
                int lastMatchStart = math.max(0, sourceLength - 12);
                while (read <= lastMatchStart)
                {
                    uint sequence = ReadUInt32(read);
                    int hash = ResolveLz4Hash(sequence, HashTable.Length);
                    int previous = HashTable[hash];
                    HashTable[hash] = read;

                    if (previous >= 0 &&
                        read - previous <= ushort.MaxValue &&
                        previous + 4 <= sourceLength &&
                        Equals4(previous, read))
                    {
                        int matchLength = 4;
                        while (read + matchLength < sourceLength &&
                               Source[previous + matchLength] == Source[read + matchLength])
                        {
                            matchLength++;
                        }

                        if (!WriteSequence(anchor, read, previous, matchLength, ref write))
                        {
                            ResultLength[0] = -1;
                            return;
                        }

                        read += matchLength;
                        anchor = read;
                        continue;
                    }

                    read++;
                }

                if (!WriteLastLiterals(anchor, sourceLength - anchor, ref write) || write >= sourceLength)
                {
                    ResultLength[0] = -1;
                    return;
                }

                ResultLength[0] = write;
            }

            private uint ReadUInt32(int offset)
            {
                return Source[offset] |
                       ((uint)Source[offset + 1] << 8) |
                       ((uint)Source[offset + 2] << 16) |
                       ((uint)Source[offset + 3] << 24);
            }

            private bool Equals4(int left, int right)
            {
                return Source[left] == Source[right] &&
                       Source[left + 1] == Source[right + 1] &&
                       Source[left + 2] == Source[right + 2] &&
                       Source[left + 3] == Source[right + 3];
            }

            private static int ResolveLz4Hash(uint sequence, int hashLength)
            {
                uint mixed = sequence * 2654435761u;
                return hashLength <= 1 ? 0 : (int)(mixed % (uint)hashLength);
            }

            private bool WriteSequence(int anchor, int read, int previous, int matchLength, ref int write)
            {
                int literalLength = read - anchor;
                int tokenOffset = write++;
                if (tokenOffset >= Destination.Length)
                    return false;

                byte token = (byte)(math.min(literalLength, 15) << 4);
                if (!WriteLengthExtension(literalLength, ref write))
                    return false;

                if (!CopySource(anchor, literalLength, ref write))
                    return false;

                int offset = read - previous;
                if (write + 2 > Destination.Length)
                    return false;

                Destination[write++] = unchecked((byte)offset);
                Destination[write++] = unchecked((byte)(offset >> 8));

                int matchCode = matchLength - 4;
                token |= (byte)math.min(matchCode, 15);
                Destination[tokenOffset] = token;
                return WriteLengthExtension(matchCode, ref write);
            }

            private bool WriteLastLiterals(int start, int literalLength, ref int write)
            {
                int tokenOffset = write++;
                if (tokenOffset >= Destination.Length)
                    return false;

                Destination[tokenOffset] = (byte)(math.min(literalLength, 15) << 4);
                return WriteLengthExtension(literalLength, ref write) &&
                       CopySource(start, literalLength, ref write);
            }

            private bool WriteLengthExtension(int totalLength, ref int write)
            {
                if (totalLength < 15)
                    return true;

                int value = totalLength - 15;
                while (value >= 255)
                {
                    if (write >= Destination.Length)
                        return false;

                    Destination[write++] = 255;
                    value -= 255;
                }

                if (write >= Destination.Length)
                    return false;

                Destination[write++] = (byte)value;
                return true;
            }

            private bool CopySource(int start, int length, ref int write)
            {
                if (length < 0 || write > Destination.Length - length)
                    return false;

                for (int i = 0; i < length; i++)
                    Destination[write++] = Source[start + i];

                return true;
            }
        }

        private static uint QuantizeUnit10(float value01)
        {
            return (uint)math.clamp((int)math.round(math.saturate(value01) * TenBitMax), 0, (int)TenBitMax);
        }
    }
}
