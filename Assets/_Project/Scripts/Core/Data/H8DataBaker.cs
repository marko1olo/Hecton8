using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Cold-path CSV validator and binary compiler for static balance data.
    /// </summary>
    public static unsafe class H8DataBaker
    {
        private const string ExpectedVersionText = "1.2";
        private const int HeaderSizeBytes = 64;
        private const int BabelHeaderSizeBytes = 32;

        // COLD ALLOC: SheetSchema[4] - static balance schema catalog - owner: H8DataBaker
        private static readonly SheetSchema[] Schemas =
        {
            new SheetSchema(
                "Items.csv",
                H8StaticDataFormat.RecordTypeItem,
                new ColumnSpec[]
                {
                    ColumnSpec.Key("Id"),
                    ColumnSpec.Version("version_id"),
                    ColumnSpec.Text("Name"),
                    ColumnSpec.Text("Description"),
                    ColumnSpec.UInt("CategoryId"),
                    ColumnSpec.Int("Cost"),
                    ColumnSpec.UShort("StackMax"),
                    ColumnSpec.Float("MassKg"),
                    ColumnSpec.UShort("IconIndex"),
                    ColumnSpec.Float("AccessFrequency")
                }),
            new SheetSchema(
                "Economy.csv",
                H8StaticDataFormat.RecordTypeEconomy,
                new ColumnSpec[]
                {
                    ColumnSpec.Key("Id"),
                    ColumnSpec.Version("version_id"),
                    ColumnSpec.Text("Name"),
                    ColumnSpec.Text("Description"),
                    ColumnSpec.Float("BasePrice"),
                    ColumnSpec.Float("Scarcity01"),
                    ColumnSpec.Float("Demand01"),
                    ColumnSpec.Float("SupplyRefreshSeconds"),
                    ColumnSpec.Float("AccessFrequency")
                }),
            new SheetSchema(
                "Physics.csv",
                H8StaticDataFormat.RecordTypePhysics,
                new ColumnSpec[]
                {
                    ColumnSpec.Key("Id"),
                    ColumnSpec.Version("version_id"),
                    ColumnSpec.Text("Name"),
                    ColumnSpec.Text("Description"),
                    ColumnSpec.Float("MassKg"),
                    ColumnSpec.Float("AddedMass"),
                    ColumnSpec.Float("LinearDrag"),
                    ColumnSpec.Float("Buoyancy"),
                    ColumnSpec.Float("CrushDepthM"),
                    ColumnSpec.Float("AccessFrequency")
                }),
            new SheetSchema(
                "Fauna.csv",
                H8StaticDataFormat.RecordTypeFauna,
                new ColumnSpec[]
                {
                    ColumnSpec.Key("Id"),
                    ColumnSpec.Version("version_id"),
                    ColumnSpec.Text("Name"),
                    ColumnSpec.Text("Description"),
                    ColumnSpec.Float("SwimSpeed"),
                    ColumnSpec.Float("TurnRate"),
                    ColumnSpec.Float("Aggression01"),
                    ColumnSpec.Float("FleeDistanceM"),
                    ColumnSpec.Float("BiolumIntensity"),
                    ColumnSpec.Float("AccessFrequency")
                })
        };

        public static H8DataBakeResult BakeDefault()
        {
            string projectRoot = ResolveProjectRoot();
            return Bake(
                Path.Combine(projectRoot, "Data", "Balance"),
                Path.Combine(projectRoot, "Data", "Balance", "Baked"));
        }

        public static H8DataBakeResult Bake(string balanceDirectory, string outputDirectory)
        {
            if (!BitConverter.IsLittleEndian)
                return Fail("Static data bake rejected: little-endian packing is mandatory.");

            H8DataBakeResult layoutValidation = ValidateLayoutContracts();
            if (!layoutValidation.Success)
                return layoutValidation;

            if (string.IsNullOrEmpty(balanceDirectory) || !Directory.Exists(balanceDirectory))
                return Fail("Balance directory missing: " + balanceDirectory);

            Directory.CreateDirectory(outputDirectory);

            List<PendingRecord> records = new List<PendingRecord>(128);
            Dictionary<uint, string> stringPool = new Dictionary<uint, string>(256);
            for (int i = 0; i < Schemas.Length; i++)
            {
                SheetSchema schema = Schemas[i];
                string path = Path.Combine(balanceDirectory, schema.FileName);
                if (!File.Exists(path))
                    return Fail("Required balance sheet missing: " + schema.FileName);

                H8DataBakeResult validation = ParseSheet(path, schema, records, stringPool);
                if (!validation.Success)
                    return validation;
            }

            records.Sort(ComparePendingRecordAccessDescending);

            string babelPath = Path.Combine(outputDirectory, H8StaticDataFormat.BabelDictionaryFileName);
            H8DataBakeResult babelResult = WriteBabelDictionary(babelPath, stringPool);
            if (!babelResult.Success)
                return babelResult;

            string staticPath = Path.Combine(outputDirectory, H8StaticDataFormat.StaticDataFileName);
            H8DataBakeResult staticResult = WriteStaticData(staticPath, records, babelResult.BabelCrc32);
            if (!staticResult.Success)
                return staticResult;

            staticResult.StringCount = babelResult.StringCount;
            staticResult.BabelCrc32 = babelResult.BabelCrc32;
            staticResult.BabelPath = babelPath;
            staticResult.Message = "Static data bake complete.";
            return staticResult;
        }

#if UNITY_EDITOR
        [MenuItem("Hecton8/Data/Bake Static Data")]
        private static void BakeStaticDataMenu()
        {
            H8DataBakeResult result = BakeDefault();
            if (result.Success)
            {
                AssetDatabase.Refresh();
                Debug.Log("[H8DataBaker] Static data bake complete. Records=" + result.RecordCount.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                Debug.LogError("[H8DataBaker] " + result.Message);
            }
        }
#endif

        private static H8DataBakeResult ParseSheet(
            string path,
            SheetSchema schema,
            List<PendingRecord> records,
            Dictionary<uint, string> stringPool)
        {
            H8CsvTable table;
            try
            {
                table = H8CsvReader.Read(path);
            }
            catch (Exception ex)
            {
                return Fail("CSV read failed for " + schema.FileName + ": " + ex.Message);
            }

            if (table.HeaderCount == 0)
                return Fail("CSV header missing in " + schema.FileName);

            int[] columnMap = new int[schema.Columns.Length];
            for (int i = 0; i < schema.Columns.Length; i++)
            {
                int index = table.FindHeader(schema.Columns[i].Name);
                if (index < 0)
                    return Fail("[CRITICAL_DATA_VOID]: Column '" + schema.Columns[i].Name + "' in " + schema.FileName + " is empty.");

                columnMap[i] = index;
            }

            for (int row = 0; row < table.RowCount; row++)
            {
                H8DataBakeResult validated = ValidateRow(table, schema, columnMap, row);
                if (!validated.Success)
                    return validated;

                PendingRecord record;
                try
                {
                    record = BuildRecord(table, schema, columnMap, row, stringPool);
                }
                catch (InvalidDataException ex)
                {
                    return Fail("[CRITICAL_DATA_COLLISION]: " + ex.Message);
                }

                records.Add(record);
            }

            return new H8DataBakeResult
            {
                Success = true,
                RecordCount = table.RowCount
            };
        }

        private static H8DataBakeResult ValidateRow(H8CsvTable table, SheetSchema schema, int[] columnMap, int row)
        {
            for (int i = 0; i < schema.Columns.Length; i++)
            {
                ColumnSpec spec = schema.Columns[i];
                string value = table.Get(row, columnMap[i]);
                if (string.IsNullOrWhiteSpace(value))
                    return Fail("[CRITICAL_DATA_VOID]: Column '" + spec.Name + "' in " + schema.FileName + " is empty.");

                switch (spec.Type)
                {
                    case ColumnType.Key:
                    case ColumnType.Text:
                        break;
                    case ColumnType.Version:
                        if (!string.Equals(value, ExpectedVersionText, StringComparison.Ordinal))
                            return Fail("Schema version mismatch in " + schema.FileName + ": expected " + ExpectedVersionText + " but found " + value + ".");
                        break;
                    case ColumnType.UInt:
                        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            return TypeFail(spec.Name, schema.FileName, "uint", value);
                        break;
                    case ColumnType.UShort:
                        if (!ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            return TypeFail(spec.Name, schema.FileName, "ushort", value);
                        break;
                    case ColumnType.Int:
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            return TypeFail(spec.Name, schema.FileName, "int", value);
                        break;
                    case ColumnType.Float:
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
                            return TypeFail(spec.Name, schema.FileName, "float", value);
                        break;
                }
            }

            return new H8DataBakeResult { Success = true };
        }

        private static H8DataBakeResult TypeFail(string column, string fileName, string expectedType, string value)
        {
            return Fail("[CRITICAL_DATA_TYPE]: Column '" + column + "' in " + fileName + " expected " + expectedType + " but got '" + value + "'.");
        }

        private static PendingRecord BuildRecord(
            H8CsvTable table,
            SheetSchema schema,
            int[] columnMap,
            int row,
            Dictionary<uint, string> stringPool)
        {
            string id = GetByName(table, schema, columnMap, row, "Id");
            string name = GetByName(table, schema, columnMap, row, "Name");
            string description = GetByName(table, schema, columnMap, row, "Description");
            uint hash = H8DataHashTool.ComputeFnv1a32(id.AsSpan());
            uint nameHash = AddString(stringPool, name);
            uint descriptionHash = AddString(stringPool, description);

            PendingRecord pending = new PendingRecord
            {
                Hash = hash,
                RecordType = schema.RecordType
            };

            switch (schema.RecordType)
            {
                case H8StaticDataFormat.RecordTypeItem:
                    H8ItemStaticRecord item = new H8ItemStaticRecord
                    {
                        Hash = hash,
                        NameHash = nameHash,
                        DescriptionHash = descriptionHash,
                        CategoryId = ReadUInt(table, schema, columnMap, row, "CategoryId"),
                        Cost = ReadInt(table, schema, columnMap, row, "Cost"),
                        StackMax = ReadUShort(table, schema, columnMap, row, "StackMax"),
                        IconIndex = ReadUShort(table, schema, columnMap, row, "IconIndex"),
                        MassKg = ReadFloat(table, schema, columnMap, row, "MassKg"),
                        AccessFrequency = ReadFloat(table, schema, columnMap, row, "AccessFrequency")
                    };
                    pending.Item = item;
                    pending.AccessFrequency = item.AccessFrequency;
                    break;
                case H8StaticDataFormat.RecordTypeEconomy:
                    H8EconomyStaticRecord economy = new H8EconomyStaticRecord
                    {
                        Hash = hash,
                        NameHash = nameHash,
                        DescriptionHash = descriptionHash,
                        BasePrice = ReadFloat(table, schema, columnMap, row, "BasePrice"),
                        Scarcity01 = ReadFloat(table, schema, columnMap, row, "Scarcity01"),
                        Demand01 = ReadFloat(table, schema, columnMap, row, "Demand01"),
                        SupplyRefreshSeconds = ReadFloat(table, schema, columnMap, row, "SupplyRefreshSeconds"),
                        AccessFrequency = ReadFloat(table, schema, columnMap, row, "AccessFrequency")
                    };
                    pending.Economy = economy;
                    pending.AccessFrequency = economy.AccessFrequency;
                    break;
                case H8StaticDataFormat.RecordTypePhysics:
                    H8PhysicsStaticRecord physics = new H8PhysicsStaticRecord
                    {
                        Hash = hash,
                        NameHash = nameHash,
                        DescriptionHash = descriptionHash,
                        MassKg = ReadFloat(table, schema, columnMap, row, "MassKg"),
                        AddedMass = ReadFloat(table, schema, columnMap, row, "AddedMass"),
                        LinearDrag = ReadFloat(table, schema, columnMap, row, "LinearDrag"),
                        Buoyancy = ReadFloat(table, schema, columnMap, row, "Buoyancy"),
                        CrushDepthM = ReadFloat(table, schema, columnMap, row, "CrushDepthM"),
                        AccessFrequency = ReadFloat(table, schema, columnMap, row, "AccessFrequency")
                    };
                    pending.Physics = physics;
                    pending.AccessFrequency = physics.AccessFrequency;
                    break;
                case H8StaticDataFormat.RecordTypeFauna:
                    H8FaunaStaticRecord fauna = new H8FaunaStaticRecord
                    {
                        Hash = hash,
                        NameHash = nameHash,
                        DescriptionHash = descriptionHash,
                        SwimSpeed = ReadFloat(table, schema, columnMap, row, "SwimSpeed"),
                        TurnRate = ReadFloat(table, schema, columnMap, row, "TurnRate"),
                        Aggression01 = ReadFloat(table, schema, columnMap, row, "Aggression01"),
                        FleeDistanceM = ReadFloat(table, schema, columnMap, row, "FleeDistanceM"),
                        BiolumIntensity = ReadFloat(table, schema, columnMap, row, "BiolumIntensity"),
                        AccessFrequency = ReadFloat(table, schema, columnMap, row, "AccessFrequency")
                    };
                    pending.Fauna = fauna;
                    pending.AccessFrequency = fauna.AccessFrequency;
                    break;
            }

            return pending;
        }

        private static H8DataBakeResult WriteBabelDictionary(string outputPath, Dictionary<uint, string> stringPool)
        {
            List<BabelBuildEntry> entries = new List<BabelBuildEntry>(stringPool.Count);
            foreach (KeyValuePair<uint, string> pair in stringPool)
            {
                entries.Add(new BabelBuildEntry
                {
                    Hash = pair.Key,
                    Text = pair.Value
                });
            }

            entries.Sort(CompareBabelHashAscending);

            int indexOffset = H8StaticDataFormat.AlignUp16(BabelHeaderSizeBytes);
            int dataOffset = H8StaticDataFormat.AlignUp16(indexOffset + (entries.Count * UnsafeUtility.SizeOf<H8BabelDictionaryEntry>()));
            int totalBytes = dataOffset;
            for (int i = 0; i < entries.Count; i++)
            {
                totalBytes = H8StaticDataFormat.AlignUp16(totalBytes);
                entries[i] = entries[i].WithOffset(totalBytes);
                totalBytes += Encoding.UTF8.GetByteCount(entries[i].Text);
            }

            byte[] bytes = new byte[totalBytes];
            fixed (byte* basePtr = bytes)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BabelBuildEntry buildEntry = entries[i];
                    int byteCount = Encoding.UTF8.GetBytes(buildEntry.Text, 0, buildEntry.Text.Length, bytes, buildEntry.Offset);
                    H8BabelDictionaryEntry entry = new H8BabelDictionaryEntry
                    {
                        Hash = buildEntry.Hash,
                        Offset = (uint)buildEntry.Offset,
                        Length = (uint)byteCount
                    };
                    WriteStruct(basePtr + indexOffset + (i * UnsafeUtility.SizeOf<H8BabelDictionaryEntry>()), in entry);
                }

                uint crc = H8Crc32.Compute(basePtr + BabelHeaderSizeBytes, totalBytes - BabelHeaderSizeBytes);
                H8BabelDictionaryHeader header = new H8BabelDictionaryHeader
                {
                    Magic = H8StaticDataFormat.BabelMagic,
                    FormatVersion = H8StaticDataFormat.FormatVersion,
                    HeaderSizeBytes = BabelHeaderSizeBytes,
                    EntryCount = (uint)entries.Count,
                    IndexOffset = (uint)indexOffset,
                    DataOffset = (uint)dataOffset,
                    FileByteLength = (uint)totalBytes,
                    PayloadCrc32 = crc,
                    Flags = H8StaticDataFormat.LittleEndianFlag
                };
                WriteStruct(basePtr, in header);
                AtomicWrite(outputPath, bytes);
                return new H8DataBakeResult
                {
                    Success = true,
                    StringCount = entries.Count,
                    BabelCrc32 = crc,
                    BabelPath = outputPath
                };
            }
        }

        private static H8DataBakeResult WriteStaticData(string outputPath, List<PendingRecord> records, uint babelCrc32)
        {
            int lookupEntrySize = UnsafeUtility.SizeOf<H8StaticDataLookupEntry>();
            int lookupOffset = HeaderSizeBytes;
            int recordsOffset = H8StaticDataFormat.AlignUp16(lookupOffset + (records.Count * lookupEntrySize));
            int currentOffset = recordsOffset;
            int paddingRepairCount = 0;

            H8StaticDataLookupEntry[] lookupEntries = new H8StaticDataLookupEntry[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                currentOffset = AlignOffsetWithRepair(currentOffset, ref paddingRepairCount);
                PendingRecord pending = records[i];
                int recordSize = H8StaticDataFormat.RecordSizeBytes(pending.RecordType);
                lookupEntries[i] = new H8StaticDataLookupEntry
                {
                    Hash = pending.Hash,
                    RecordType = pending.RecordType,
                    ByteSize = (ushort)recordSize,
                    Offset = currentOffset
                };
                currentOffset += recordSize;
            }

            byte[] bytes = new byte[currentOffset];
            fixed (byte* basePtr = bytes)
            {
                for (int i = 0; i < lookupEntries.Length; i++)
                    WriteStruct(basePtr + lookupOffset + (i * lookupEntrySize), in lookupEntries[i]);

                for (int i = 0; i < records.Count; i++)
                {
                    PendingRecord pending = records[i];
                    byte* destination = basePtr + lookupEntries[i].Offset;
                    switch (pending.RecordType)
                    {
                        case H8StaticDataFormat.RecordTypeItem:
                            WriteStruct(destination, in pending.Item);
                            break;
                        case H8StaticDataFormat.RecordTypeEconomy:
                            WriteStruct(destination, in pending.Economy);
                            break;
                        case H8StaticDataFormat.RecordTypePhysics:
                            WriteStruct(destination, in pending.Physics);
                            break;
                        case H8StaticDataFormat.RecordTypeFauna:
                            WriteStruct(destination, in pending.Fauna);
                            break;
                    }
                }

                uint crc = H8Crc32.Compute(basePtr + HeaderSizeBytes, bytes.Length - HeaderSizeBytes);
                H8StaticDataHeader header = new H8StaticDataHeader
                {
                    Magic = H8StaticDataFormat.StaticDataMagic,
                    FormatVersion = H8StaticDataFormat.FormatVersion,
                    HeaderSizeBytes = HeaderSizeBytes,
                    SchemaMajor = H8StaticDataFormat.ExpectedSchemaMajor,
                    SchemaMinor = H8StaticDataFormat.ExpectedSchemaMinor,
                    FileByteLength = (uint)bytes.Length,
                    PayloadCrc32 = crc,
                    LookupCount = (uint)lookupEntries.Length,
                    RecordCount = (uint)records.Count,
                    LookupOffset = (uint)lookupOffset,
                    RecordsOffset = (uint)recordsOffset,
                    RecordBytes = (uint)(bytes.Length - recordsOffset),
                    BabelCrc32 = babelCrc32,
                    Flags = H8StaticDataFormat.LittleEndianFlag,
                    SchemaHash = H8StaticDataFormat.SchemaHash
                };
                WriteStruct(basePtr, in header);
                AtomicWrite(outputPath, bytes);
                return new H8DataBakeResult
                {
                    Success = true,
                    RecordCount = records.Count,
                    PaddingRepairCount = paddingRepairCount,
                    StaticDataCrc32 = crc,
                    StaticDataPath = outputPath
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStruct<T>(byte* destination, in T value) where T : unmanaged
        {
            T local = value;
            UnsafeUtility.MemCpy(destination, &local, UnsafeUtility.SizeOf<T>());
        }

        private static int AlignOffsetWithRepair(int offset, ref int repairCount)
        {
            int aligned = H8StaticDataFormat.AlignUp16(offset);
            if (aligned != offset)
                repairCount++;

            return aligned;
        }

        private static void AtomicWrite(string path, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                string backupPath = path + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Replace(tempPath, path, backupPath, true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private static H8DataBakeResult ValidateLayoutContracts()
        {
            if (UnsafeUtility.SizeOf<H8StaticDataHeader>() != HeaderSizeBytes)
                return Fail("Static data header ABI drift: expected 64 bytes.");
            if (UnsafeUtility.SizeOf<H8StaticDataLookupEntry>() != 16)
                return Fail("Static data lookup ABI drift: expected 16 bytes.");
            if (UnsafeUtility.SizeOf<H8BabelDictionaryHeader>() != BabelHeaderSizeBytes)
                return Fail("Babel header ABI drift: expected 32 bytes.");
            if (UnsafeUtility.SizeOf<H8BabelDictionaryEntry>() != 16)
                return Fail("Babel entry ABI drift: expected 16 bytes.");
            if (UnsafeUtility.SizeOf<H8StaticDataTelemetryEntry>() != 64)
                return Fail("Static data telemetry ABI drift: expected 64 bytes.");
            if (UnsafeUtility.SizeOf<H8ItemStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8EconomyStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8PhysicsStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8FaunaStaticRecord>() != 48)
            {
                return Fail("Static data record ABI drift: expected 48-byte records.");
            }

            return new H8DataBakeResult { Success = true };
        }

        private static uint AddString(Dictionary<uint, string> stringPool, string value)
        {
            uint hash = H8DataHashTool.ComputeFnv1a32(value.AsSpan());
            if (stringPool.TryGetValue(hash, out string existing))
            {
                if (!string.Equals(existing, value, StringComparison.Ordinal))
                    throw new InvalidDataException("Babel hash collision for text hash 0x" + hash.ToString("X8", CultureInfo.InvariantCulture));
            }
            else
            {
                stringPool.Add(hash, value);
            }

            return hash;
        }

        private static string GetByName(H8CsvTable table, SheetSchema schema, int[] columnMap, int row, string column)
        {
            int index = FindColumn(schema, column);
            return table.Get(row, columnMap[index]);
        }

        private static uint ReadUInt(H8CsvTable table, SheetSchema schema, int[] columnMap, int row, string column)
        {
            uint.TryParse(GetByName(table, schema, columnMap, row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value);
            return value;
        }

        private static ushort ReadUShort(H8CsvTable table, SheetSchema schema, int[] columnMap, int row, string column)
        {
            ushort.TryParse(GetByName(table, schema, columnMap, row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value);
            return value;
        }

        private static int ReadInt(H8CsvTable table, SheetSchema schema, int[] columnMap, int row, string column)
        {
            int.TryParse(GetByName(table, schema, columnMap, row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
            return value;
        }

        private static float ReadFloat(H8CsvTable table, SheetSchema schema, int[] columnMap, int row, string column)
        {
            float.TryParse(GetByName(table, schema, columnMap, row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
            return value;
        }

        private static int FindColumn(SheetSchema schema, string name)
        {
            for (int i = 0; i < schema.Columns.Length; i++)
            {
                if (string.Equals(schema.Columns[i].Name, name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static int ComparePendingRecordAccessDescending(PendingRecord left, PendingRecord right)
        {
            int access = right.AccessFrequency.CompareTo(left.AccessFrequency);
            return access != 0 ? access : left.Hash.CompareTo(right.Hash);
        }

        private static int CompareBabelHashAscending(BabelBuildEntry left, BabelBuildEntry right)
        {
            return left.Hash.CompareTo(right.Hash);
        }

        private static string ResolveProjectRoot()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!string.IsNullOrEmpty(Application.dataPath))
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#endif
            return Directory.GetCurrentDirectory();
        }

        private static H8DataBakeResult Fail(string message)
        {
            return new H8DataBakeResult
            {
                Success = false,
                Message = message
            };
        }

        private enum ColumnType : byte
        {
            Key,
            Version,
            Text,
            UInt,
            UShort,
            Int,
            Float
        }

        private readonly struct ColumnSpec
        {
            public readonly string Name;
            public readonly ColumnType Type;

            private ColumnSpec(string name, ColumnType type)
            {
                Name = name;
                Type = type;
            }

            public static ColumnSpec Key(string name) { return new ColumnSpec(name, ColumnType.Key); }
            public static ColumnSpec Version(string name) { return new ColumnSpec(name, ColumnType.Version); }
            public static ColumnSpec Text(string name) { return new ColumnSpec(name, ColumnType.Text); }
            public static ColumnSpec UInt(string name) { return new ColumnSpec(name, ColumnType.UInt); }
            public static ColumnSpec UShort(string name) { return new ColumnSpec(name, ColumnType.UShort); }
            public static ColumnSpec Int(string name) { return new ColumnSpec(name, ColumnType.Int); }
            public static ColumnSpec Float(string name) { return new ColumnSpec(name, ColumnType.Float); }
        }

        private readonly struct SheetSchema
        {
            public readonly string FileName;
            public readonly ushort RecordType;
            public readonly ColumnSpec[] Columns;

            public SheetSchema(string fileName, ushort recordType, ColumnSpec[] columns)
            {
                FileName = fileName;
                RecordType = recordType;
                Columns = columns;
            }
        }

        private struct PendingRecord
        {
            public uint Hash;
            public ushort RecordType;
            public float AccessFrequency;
            public H8ItemStaticRecord Item;
            public H8EconomyStaticRecord Economy;
            public H8PhysicsStaticRecord Physics;
            public H8FaunaStaticRecord Fauna;
        }

        private struct BabelBuildEntry
        {
            public uint Hash;
            public string Text;
            public int Offset;

            public BabelBuildEntry WithOffset(int offset)
            {
                Offset = offset;
                return this;
            }
        }
    }

    internal sealed class H8CsvTable
    {
        private readonly string[] _headers;
        private readonly List<string[]> _rows;

        public H8CsvTable(string[] headers, List<string[]> rows)
        {
            _headers = headers;
            _rows = rows;
        }

        public int HeaderCount => _headers.Length;
        public int RowCount => _rows.Count;

        public string Get(int row, int column)
        {
            string[] values = _rows[row];
            return column >= 0 && column < values.Length ? values[column] : string.Empty;
        }

        public int FindHeader(string name)
        {
            for (int i = 0; i < _headers.Length; i++)
            {
                if (string.Equals(_headers[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }

    internal static class H8CsvReader
    {
        private const int CsvReadBufferBytes = 64 * 1024;

        public static H8CsvTable Read(string path)
        {
            string text;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, CsvReadBufferBytes, FileOptions.SequentialScan))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                text = reader.ReadToEnd();

            List<string[]> rows = new List<string[]>(64);
            List<string> cells = new List<string>(16);
            StringBuilder cell = new StringBuilder(64);
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && c == ',')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Length = 0;
                    continue;
                }

                if (!inQuotes && (c == '\r' || c == '\n'))
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    FinishRow(rows, cells, cell);
                    continue;
                }

                cell.Append(c);
            }

            FinishRow(rows, cells, cell);
            if (rows.Count == 0)
                return new H8CsvTable(Array.Empty<string>(), new List<string[]>(0));

            string[] headers = rows[0];
            rows.RemoveAt(0);
            return new H8CsvTable(headers, rows);
        }

        private static void FinishRow(List<string[]> rows, List<string> cells, StringBuilder cell)
        {
            if (cell.Length == 0 && cells.Count == 0)
                return;

            cells.Add(cell.ToString().Trim());
            cell.Length = 0;

            bool any = false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(cells[i]))
                {
                    any = true;
                    break;
                }
            }

            if (any)
                rows.Add(cells.ToArray());

            cells.Clear();
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class H8StaticDataEditorHotReloadBootstrap
    {
        private static H8StaticDataHotReloadWatcher _watcher;

        static H8StaticDataEditorHotReloadBootstrap()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_watcher == null)
                _watcher = new H8StaticDataHotReloadWatcher();

            _watcher.TickEditor();
        }
    }

    internal sealed class H8StaticDataHotReloadWatcher : IDisposable
    {
        private const double DebounceSeconds = 0.35d;
        private FileSystemWatcher _watcher;
        private double _dirtyAt;
        private bool _dirty;
        private bool _pausedByStress;

        public H8StaticDataHotReloadWatcher()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance");
            if (!Directory.Exists(root))
                return;

            _watcher = new FileSystemWatcher(root, "*.csv");
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        public void TickEditor()
        {
            if (_watcher == null)
                return;

            float stress = SignalBusRegistry.SystemStress01;
            _pausedByStress = stress > 0.9f;
            if (_pausedByStress || !_dirty)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _dirtyAt < DebounceSeconds)
                return;

            _dirty = false;
            H8DataBakeResult result = H8DataBaker.BakeDefault();
            if (!result.Success)
                Debug.LogError("[H8DataHotReload] " + result.Message);
            else
                AssetDatabase.Refresh();
        }

        public void Dispose()
        {
            if (_watcher == null)
                return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        private void OnChanged(object sender, FileSystemEventArgs args)
        {
            _dirty = true;
            _dirtyAt = EditorApplication.timeSinceStartup;
        }

        private void OnRenamed(object sender, RenamedEventArgs args)
        {
            _dirty = true;
            _dirtyAt = EditorApplication.timeSinceStartup;
        }
    }
#endif
}
