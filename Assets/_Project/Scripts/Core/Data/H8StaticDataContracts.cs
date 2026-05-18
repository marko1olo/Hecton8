using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Numeric file contracts for the static balance monolith.
    /// </summary>
    public static unsafe class H8StaticDataFormat
    {
        public const string StaticDataFileName = "H8StaticData.bin";
        public const string BabelDictionaryFileName = "Babel_Dictionary.h8bin";
        public const int AlignmentBytes = 16;
        public const int TelemetryFrameCount = 300;
        public const int TelemetryDumpHeaderSizeBytes = 32;
        public const ushort FormatVersion = 1;
        public const ushort ExpectedSchemaMajor = 1;
        public const ushort ExpectedSchemaMinor = 2;
        public const uint StaticDataMagic = 0x44533848u;
        public const uint BabelMagic = 0x42413848u;
        public const ulong TelemetryDumpMagic = 0x484543544F4E3800ul;
        public const uint LittleEndianFlag = 1u;
        public const uint SchemaHash = 0x5C43DD40u;
        public const ushort RecordTypeItem = 1;
        public const ushort RecordTypeEconomy = 2;
        public const ushort RecordTypePhysics = 3;
        public const ushort RecordTypeFauna = 4;
        public const ushort MaxPackedRecordType = 15;
        private const long LookupOffsetMask = ~15L;
        private const long LookupRecordTypeMask = MaxPackedRecordType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp16(int value)
        {
            return (value + (AlignmentBytes - 1)) & ~(AlignmentBytes - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignUp16(long value)
        {
            return (value + (AlignmentBytes - 1L)) & ~(AlignmentBytes - 1L);
        }

        public static int RecordSizeBytes(ushort recordType)
        {
            switch (recordType)
            {
                case RecordTypeItem:
                    return UnsafeUtility.SizeOf<H8ItemStaticRecord>();
                case RecordTypeEconomy:
                    return UnsafeUtility.SizeOf<H8EconomyStaticRecord>();
                case RecordTypePhysics:
                    return UnsafeUtility.SizeOf<H8PhysicsStaticRecord>();
                case RecordTypeFauna:
                    return UnsafeUtility.SizeOf<H8FaunaStaticRecord>();
                default:
                    return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort RecordTypeOf<T>() where T : unmanaged
        {
            if (typeof(T) == typeof(H8ItemStaticRecord))
                return RecordTypeItem;
            if (typeof(T) == typeof(H8EconomyStaticRecord))
                return RecordTypeEconomy;
            if (typeof(T) == typeof(H8PhysicsStaticRecord))
                return RecordTypePhysics;
            if (typeof(T) == typeof(H8FaunaStaticRecord))
                return RecordTypeFauna;

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PackLookupValue(long offset, ushort recordType)
        {
            return (offset & LookupOffsetMask) | (recordType & LookupRecordTypeMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanPackRecordType(ushort recordType)
        {
            return recordType > 0 && recordType <= MaxPackedRecordType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UnpackLookupOffset(long packedValue)
        {
            return packedValue & LookupOffsetMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort UnpackLookupRecordType(long packedValue)
        {
            return (ushort)(packedValue & LookupRecordTypeMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PackBabelSlice(uint offset, uint length)
        {
            return ((long)offset << 32) | length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint UnpackBabelOffset(long packedValue)
        {
            return (uint)(packedValue >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint UnpackBabelLength(long packedValue)
        {
            return (uint)packedValue;
        }
    }

    /// <summary>
    /// Fixed header for H8StaticData.bin. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct H8StaticDataHeader
    {
        public uint Magic;
        public ushort FormatVersion;
        public ushort HeaderSizeBytes;
        public ushort SchemaMajor;
        public ushort SchemaMinor;
        public uint FileByteLength;
        public uint PayloadCrc32;
        public uint LookupCount;
        public uint RecordCount;
        public uint LookupOffset;
        public uint RecordsOffset;
        public uint RecordBytes;
        public uint BabelCrc32;
        public uint Flags;
        public uint SchemaHash;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    /// <summary>
    /// Hash-to-offset lookup entry. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct H8StaticDataLookupEntry
    {
        public uint Hash;
        public ushort RecordType;
        public ushort ByteSize;
        public long Offset;
    }

    /// <summary>
    /// Fixed header for Babel_Dictionary.h8bin. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8BabelDictionaryHeader
    {
        public uint Magic;
        public ushort FormatVersion;
        public ushort HeaderSizeBytes;
        public uint EntryCount;
        public uint IndexOffset;
        public uint DataOffset;
        public uint FileByteLength;
        public uint PayloadCrc32;
        public uint Flags;
    }

    /// <summary>
    /// Hash-to-UTF8 block index entry. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct H8BabelDictionaryEntry
    {
        public uint Hash;
        public uint Offset;
        public uint Length;
        public uint Flags;
    }

    /// <summary>
    /// Hash-to-UTF8 Babel index row. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct BabelIndexDTO
    {
        public uint StringHash;
        public uint ByteOffset;
        public uint ByteLength;
        public uint _pad0;
    }

    /// <summary>
    /// Result row for Burst lookup kernels. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct BabelLookupResultDTO
    {
        public uint TextHash;
        public uint ByteOffset;
        public uint ByteLength;
        public uint Flags;
    }

    /// <summary>
    /// Dependency-free text request payload used by Babel vacuum tests. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockTextRequestSignal : ISignal
    {
        public uint TextHash;
        public uint FrameIndex;
        public ushort LocaleId;
        public ushort Flags;
        public uint _pad0;
    }

    /// <summary>
    /// Decoupled voice-over request. Audio owns consumption. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct PlayVoiceOverSignal : ISignal
    {
        public uint TextHash;
        public uint VoiceHash;
        public uint FrameIndex;
        public uint Flags;
    }

    /// <summary>
    /// Blind UI output buffer contract for lookup smoke jobs. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public unsafe struct MockUIBuffer
    {
        public byte* Ptr;
        public int CapacityBytes;
        public int WrittenBytes;
    }

    public partial struct MockSpanConverter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountBytes(ReadOnlySpan<byte> utf8Bytes)
        {
            return utf8Bytes.Length;
        }
    }

    /// <summary>
    /// Static item balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct H8ItemStaticRecord
    {
        public uint Hash;
        public uint NameHash;
        public uint DescriptionHash;
        public uint CategoryId;
        public int Cost;
        public ushort StackMax;
        public ushort IconIndex;
        public float MassKg;
        public float AccessFrequency;
        public uint Flags;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    /// <summary>
    /// Static economy balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct H8EconomyStaticRecord
    {
        public uint Hash;
        public uint NameHash;
        public uint DescriptionHash;
        public uint ReservedKey;
        public float BasePrice;
        public float Scarcity01;
        public float Demand01;
        public float SupplyRefreshSeconds;
        public float AccessFrequency;
        public uint Flags;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Static physics balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct H8PhysicsStaticRecord
    {
        public uint Hash;
        public uint NameHash;
        public uint DescriptionHash;
        public uint Flags;
        public float MassKg;
        public float AddedMass;
        public float LinearDrag;
        public float Buoyancy;
        public float CrushDepthM;
        public float AccessFrequency;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Static fauna balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct H8FaunaStaticRecord
    {
        public uint Hash;
        public uint NameHash;
        public uint DescriptionHash;
        public uint Flags;
        public float SwimSpeed;
        public float TurnRate;
        public float Aggression01;
        public float FleeDistanceM;
        public float BiolumIntensity;
        public float AccessFrequency;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Static data black-box entry. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct H8StaticDataTelemetryEntry
    {
        public uint FrameIndex;
        public uint StateHash;
        public uint LastRequestedHash;
        public uint LookupCount;
        public uint RecordCount;
        public uint PayloadCrc32;
        public uint Flags;
        public uint SchemaHash;
        public long FileByteLength;
        public long LastOffset;
        public uint ErrorHash;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    /// <summary>
    /// Fixed header for static-data black-box dumps. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct H8StaticDataDumpHeader
    {
        public ulong Magic;
        public uint EntryCount;
        public uint EntrySizeBytes;
        public uint SchemaHash;
        public uint PayloadCrc32;
        public uint Flags;
        public uint Reserved0;
    }

    /// <summary>
    /// Bake result for cold-path editor and test callers.
    /// </summary>
    public struct H8DataBakeResult
    {
        public bool Success;
        public int RecordCount;
        public int StringCount;
        public int PaddingRepairCount;
        public uint StaticDataCrc32;
        public uint BabelCrc32;
        public string StaticDataPath;
        public string BabelPath;
        public string Message;
    }

    /// <summary>
    /// Sanity scan result for static binary validation.
    /// </summary>
    public struct H8StaticDataSanityReport
    {
        public bool IsClean;
        public int RecordsScanned;
        public uint FailedHash;
        public ushort FailedRecordType;
        public string Message;
    }

    /// <summary>
    /// Allocation-free hash helper and hash-manifest cold tool.
    /// </summary>
    public static class H8DataHashTool
    {
        public const uint FnvOffset32 = 2166136261u;
        public const uint FnvPrime32 = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1a32(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset32;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);

                hash = unchecked((hash ^ (byte)c) * FnvPrime32);
            }

            return hash == 0u ? FnvOffset32 : hash;
        }

        /// <summary>
        /// Computes FNV-1a over the UTF8 byte representation of human-facing Babel text.
        /// </summary>
        public static uint ComputeFnv1a32Utf8(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset32;
            for (int i = 0; i < value.Length; i++)
            {
                uint codePoint;
                char c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    codePoint = (uint)char.ConvertToUtf32(c, value[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(c))
                {
                    codePoint = 0xFFFDu;
                }
                else
                {
                    codePoint = c;
                }

                hash = HashUtf8CodePoint(hash, codePoint);
            }

            return hash == 0u ? FnvOffset32 : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashByte(uint hash, byte value)
        {
            return unchecked((hash ^ value) * FnvPrime32);
        }

        private static uint HashUtf8CodePoint(uint hash, uint codePoint)
        {
            if (codePoint <= 0x7Fu)
                return HashByte(hash, (byte)codePoint);

            if (codePoint <= 0x7FFu)
            {
                hash = HashByte(hash, (byte)(0xC0u | (codePoint >> 6)));
                return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
            }

            if (codePoint <= 0xFFFFu)
            {
                hash = HashByte(hash, (byte)(0xE0u | (codePoint >> 12)));
                hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 6) & 0x3Fu)));
                return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
            }

            hash = HashByte(hash, (byte)(0xF0u | (codePoint >> 18)));
            hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 12) & 0x3Fu)));
            hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 6) & 0x3Fu)));
            return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
        }

        public static H8DataBakeResult GenerateHashManifest(string csvPath, string outputPath)
        {
            if (string.IsNullOrEmpty(csvPath) || !System.IO.File.Exists(csvPath))
                return Fail("CSV file missing.");

            H8CsvTable table = H8CsvReader.Read(csvPath);
            using (System.IO.FileStream stream = new System.IO.FileStream(
                outputPath,
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.Read))
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("Id,Fnv1a32");
                for (int i = 0; i < table.RowCount; i++)
                {
                    string id = table.Get(i, 0);
                    uint hash = ComputeFnv1a32(id.AsSpan());
                    writer.Write(id);
                    writer.Write(',');
                    writer.Write(hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine();
                }
            }

            return new H8DataBakeResult
            {
                Success = true,
                RecordCount = table.RowCount,
                StaticDataPath = outputPath,
                Message = "Hash manifest generated."
            };
        }

        private static H8DataBakeResult Fail(string message)
        {
            return new H8DataBakeResult
            {
                Success = false,
                Message = message
            };
        }
    }

    /// <summary>
    /// CRC32 without runtime table allocation.
    /// </summary>
    public static unsafe class H8Crc32
    {
        public static uint Compute(byte* data, int byteLength)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < byteLength; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return ~crc;
        }
    }

    internal static unsafe class H8StaticDataBlackBoxDump
    {
        public static void Write(
            string path,
            H8StaticDataTelemetryEntry* ring,
            int cursorValue,
            uint payloadCrc32,
            uint flags)
        {
            if (ring == null || string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if ((uint)cursorValue >= H8StaticDataFormat.TelemetryFrameCount)
                cursorValue = 0;

            int entrySize = UnsafeUtility.SizeOf<H8StaticDataTelemetryEntry>();
            H8StaticDataDumpHeader header = new H8StaticDataDumpHeader
            {
                Magic = H8StaticDataFormat.TelemetryDumpMagic,
                EntryCount = H8StaticDataFormat.TelemetryFrameCount,
                EntrySizeBytes = (uint)entrySize,
                SchemaHash = H8StaticDataFormat.SchemaHash,
                PayloadCrc32 = payloadCrc32,
                Flags = flags
            };

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(new ReadOnlySpan<byte>(&header, UnsafeUtility.SizeOf<H8StaticDataDumpHeader>()));
                for (int i = 0; i < H8StaticDataFormat.TelemetryFrameCount; i++)
                {
                    int sourceIndex = (cursorValue + i) % H8StaticDataFormat.TelemetryFrameCount;
                    stream.Write(new ReadOnlySpan<byte>(ring + sourceIndex, entrySize));
                }
            }
        }
    }
}
