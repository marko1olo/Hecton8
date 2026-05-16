using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        public const ushort FormatVersion = 1;
        public const ushort ExpectedSchemaMajor = 1;
        public const ushort ExpectedSchemaMinor = 2;
        public const uint StaticDataMagic = 0x44533848u;
        public const uint BabelMagic = 0x42413848u;
        public const uint LittleEndianFlag = 1u;
        public const uint SchemaHash = 0xC5AD1200u;
        public const ushort RecordTypeItem = 1;
        public const ushort RecordTypeEconomy = 2;
        public const ushort RecordTypePhysics = 3;
        public const ushort RecordTypeFauna = 4;

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
    }

    /// <summary>
    /// Fixed header for H8StaticData.bin. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct H8BabelDictionaryEntry
    {
        public uint Hash;
        public uint Offset;
        public uint Length;
        public uint Flags;
    }

    /// <summary>
    /// Static item balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
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
}
