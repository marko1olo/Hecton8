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
#if UNITY_EDITOR
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
                    ColumnSpec.IntMin("Cost", 0d),
                    ColumnSpec.UShortMin("StackMax", 1d),
                    ColumnSpec.FloatMin("MassKg", 0d),
                    ColumnSpec.UShort("IconIndex"),
                    ColumnSpec.FloatMin("AccessFrequency", 0d)
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
                    ColumnSpec.FloatMin("BasePrice", 0d),
                    ColumnSpec.FloatRange("Scarcity01", 0d, 1d),
                    ColumnSpec.FloatRange("Demand01", 0d, 1d),
                    ColumnSpec.FloatMin("SupplyRefreshSeconds", 0d),
                    ColumnSpec.FloatMin("AccessFrequency", 0d)
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
                    ColumnSpec.FloatMin("MassKg", 0d),
                    ColumnSpec.FloatMin("AddedMass", 0d),
                    ColumnSpec.FloatMin("LinearDrag", 0d),
                    ColumnSpec.FloatMin("Buoyancy", 0d),
                    ColumnSpec.FloatMin("CrushDepthM", 0d),
                    ColumnSpec.FloatMin("AccessFrequency", 0d)
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
                    ColumnSpec.FloatMin("SwimSpeed", 0d),
                    ColumnSpec.FloatMin("TurnRate", 0d),
                    ColumnSpec.FloatRange("Aggression01", 0d, 1d),
                    ColumnSpec.FloatMin("FleeDistanceM", 0d),
                    ColumnSpec.FloatMin("BiolumIntensity", 0d),
                    ColumnSpec.FloatMin("AccessFrequency", 0d)
                })
        };

        public static H8DataBakeResult BakeDefault()
        {
            string projectRoot = ResolveProjectRoot();
            return Bake(
                Path.Combine(projectRoot, "Data", "Balance"),
                Path.Combine(projectRoot, "Data", "Balance", "Baked"));
        }

        /// <summary>
        /// Deterministic hash of the active cold bake schema catalog.
        /// </summary>
        public static uint CurrentSchemaHash => ComputeSchemaHash();

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

            // COLD ALLOC: List<PendingRecord>[128] - validated bake records before contiguous binary write - owner: H8DataBaker
            List<PendingRecord> records = new List<PendingRecord>(128);
            // COLD ALLOC: List<BabelBuildEntry>[256] - flat Babel text pool for cold bake output - owner: H8DataBaker
            List<BabelBuildEntry> stringPool = new List<BabelBuildEntry>(256);
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
            staticResult.PaddingRepairCount = babelResult.PaddingRepairCount;
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
            List<BabelBuildEntry> stringPool)
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
                {
                    if (table.CountHeaderIgnoreCase(schema.Columns[i].Name) > 0)
                        return Fail("[CRITICAL_DATA_SCHEMA]: Column '" + schema.Columns[i].Name + "' in " + schema.FileName + " must match exact header case.");

                    return Fail("[CRITICAL_DATA_VOID]: Column '" + schema.Columns[i].Name + "' in " + schema.FileName + " is empty.");
                }

                if (table.CountHeader(schema.Columns[i].Name) != 1)
                    return Fail("[CRITICAL_DATA_SCHEMA]: Column '" + schema.Columns[i].Name + "' in " + schema.FileName + " is duplicated.");

                if (i == 0 && index != 0)
                    return Fail("[CRITICAL_DATA_SCHEMA]: First column in " + schema.FileName + " must be 'Id' for stable FNV identity.");

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

                if (ContainsRecordHash(records, record.Hash))
                    return Fail("[CRITICAL_DATA_COLLISION]: Duplicate ID hash 0x" + record.Hash.ToString("X8", CultureInfo.InvariantCulture) + " in " + schema.FileName + " row " + (row + 2).ToString(CultureInfo.InvariantCulture) + ".");

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
                        if (!IsCanonicalKey(value))
                            return Fail("[CRITICAL_DATA_KEY]: Column '" + spec.Name + "' in " + schema.FileName + " must be lowercase ASCII snake_case, got '" + value + "'.");
                        break;
                    case ColumnType.Text:
                        if (TryFindControlCharacter(value, out char controlCharacter))
                            return Fail("[CRITICAL_DATA_TEXT]: Column '" + spec.Name + "' in " + schema.FileName + " contains control character 0x" + ((int)controlCharacter).ToString("X2", CultureInfo.InvariantCulture) + ".");
                        break;
                    case ColumnType.Version:
                        if (!string.Equals(value, ExpectedVersionText, StringComparison.Ordinal))
                            return Fail("Schema version mismatch in " + schema.FileName + ": expected " + ExpectedVersionText + " but found " + value + ".");
                        break;
                    case ColumnType.UInt:
                        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedUInt))
                            return TypeFail(spec.Name, schema.FileName, "uint", value);
                        if (!IsInRange(parsedUInt, spec))
                            return RangeFail(spec.Name, schema.FileName, value, spec);
                        break;
                    case ColumnType.UShort:
                        if (!ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort parsedUShort))
                            return TypeFail(spec.Name, schema.FileName, "ushort", value);
                        if (!IsInRange(parsedUShort, spec))
                            return RangeFail(spec.Name, schema.FileName, value, spec);
                        break;
                    case ColumnType.Int:
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                            return TypeFail(spec.Name, schema.FileName, "int", value);
                        if (!IsInRange(parsedInt, spec))
                            return RangeFail(spec.Name, schema.FileName, value, spec);
                        break;
                    case ColumnType.Float:
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
                            return TypeFail(spec.Name, schema.FileName, "float", value);
                        if (!IsInRange(parsed, spec))
                            return RangeFail(spec.Name, schema.FileName, value, spec);
                        break;
                }
            }

            return new H8DataBakeResult { Success = true };
        }

        private static H8DataBakeResult TypeFail(string column, string fileName, string expectedType, string value)
        {
            return Fail("[CRITICAL_DATA_TYPE]: Column '" + column + "' in " + fileName + " expected " + expectedType + " but got '" + value + "'.");
        }

        private static H8DataBakeResult RangeFail(string column, string fileName, string value, ColumnSpec spec)
        {
            return Fail("[CRITICAL_DATA_RANGE]: Column '" + column + "' in " + fileName + " is outside " + FormatRange(spec) + ", got '" + value + "'.");
        }

        private static bool IsInRange(double value, ColumnSpec spec)
        {
            if (spec.HasMin && value < spec.MinValue)
                return false;
            if (spec.HasMax && value > spec.MaxValue)
                return false;

            return true;
        }

        private static string FormatRange(ColumnSpec spec)
        {
            string min = spec.HasMin ? spec.MinValue.ToString(CultureInfo.InvariantCulture) : "-inf";
            string max = spec.HasMax ? spec.MaxValue.ToString(CultureInfo.InvariantCulture) : "+inf";
            return "[" + min + ", " + max + "]";
        }

        private static bool IsCanonicalKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            char first = value[0];
            if (first < 'a' || first > 'z')
                return false;

            char previous = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_';
                if (!valid)
                    return false;

                if (c == '_' && (i == value.Length - 1 || previous == '_'))
                    return false;

                previous = c;
            }

            return true;
        }

        private static bool TryFindControlCharacter(string value, out char controlCharacter)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c))
                {
                    controlCharacter = c;
                    return true;
                }
            }

            controlCharacter = default;
            return false;
        }

        private static bool ContainsRecordHash(List<PendingRecord> records, uint hash)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Hash == hash)
                    return true;
            }

            return false;
        }

        private static PendingRecord BuildRecord(
            H8CsvTable table,
            SheetSchema schema,
            int[] columnMap,
            int row,
            List<BabelBuildEntry> stringPool)
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

        private static H8DataBakeResult WriteBabelDictionary(string outputPath, List<BabelBuildEntry> stringPool)
        {
            List<BabelBuildEntry> entries = new List<BabelBuildEntry>(stringPool.Count);
            for (int i = 0; i < stringPool.Count; i++)
            {
                entries.Add(stringPool[i]);
            }

            entries.Sort(CompareBabelHashAscending);

            int indexOffset = H8StaticDataFormat.AlignUp16(BabelHeaderSizeBytes);
            int indexBytes = entries.Count * UnsafeUtility.SizeOf<BabelIndexDTO>();
            int btreeOffset = H8StaticDataFormat.AlignUp64(indexOffset + indexBytes);
            BTreeBuildRecord[] btreeRecords = new BTreeBuildRecord[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                btreeRecords[i] = new BTreeBuildRecord
                {
                    Key = entries[i].Hash,
                    Value = (uint)i
                };
            }

            BTreeNodeDTO[] btreeNodes = BuildCacheBTreeNodes(btreeRecords, (uint)btreeOffset);
            int dataOffset = H8StaticDataFormat.AlignUp16(btreeOffset + (btreeNodes.Length * UnsafeUtility.SizeOf<BTreeNodeDTO>()));
            int totalBytes = dataOffset;
            for (int i = 0; i < entries.Count; i++)
            {
                totalBytes = H8StaticDataFormat.AlignUp16(totalBytes);
                entries[i] = entries[i].WithOffset(totalBytes);
                totalBytes += Encoding.UTF8.GetByteCount(entries[i].Text);
            }
            int paddingRepairBytes = H8StaticDataFormat.AlignUp16(totalBytes) - totalBytes;
            totalBytes = H8StaticDataFormat.AlignUp16(totalBytes);

            byte[] bytes = new byte[totalBytes];
            fixed (byte* basePtr = bytes)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BabelBuildEntry buildEntry = entries[i];
                    int byteCount = Encoding.UTF8.GetBytes(buildEntry.Text, 0, buildEntry.Text.Length, bytes, buildEntry.Offset);
                    BabelIndexDTO entry = new BabelIndexDTO
                    {
                        StringHash = buildEntry.Hash,
                        ByteOffset = (uint)buildEntry.Offset,
                        ByteLength = (uint)byteCount
                    };
                    WriteStruct(basePtr + indexOffset + (i * UnsafeUtility.SizeOf<BabelIndexDTO>()), in entry);
                }

                for (int i = 0; i < btreeNodes.Length; i++)
                    WriteStruct(basePtr + btreeOffset + (i * UnsafeUtility.SizeOf<BTreeNodeDTO>()), in btreeNodes[i]);

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
                    Flags = H8StaticDataFormat.LittleEndianFlag | H8StaticDataFormat.CacheBTreeFlag
                };
                WriteStruct(basePtr, in header);
                AtomicWrite(outputPath, bytes);
                return new H8DataBakeResult
                {
                    Success = true,
                    StringCount = entries.Count,
                    PaddingRepairCount = paddingRepairBytes,
                    BabelCrc32 = crc,
                    BabelPath = outputPath
                };
            }
        }

        private static H8DataBakeResult WriteStaticData(string outputPath, List<PendingRecord> records, uint babelCrc32)
        {
            int lookupEntrySize = UnsafeUtility.SizeOf<H8StaticDataLookupEntry>();
            int lookupOffset = HeaderSizeBytes;
            int lookupBytes = records.Count * lookupEntrySize;
            int btreeOffset = H8StaticDataFormat.AlignUp64(lookupOffset + lookupBytes);
            BTreeBuildRecord[] btreeRecords = new BTreeBuildRecord[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                btreeRecords[i] = new BTreeBuildRecord
                {
                    Key = records[i].Hash,
                    Value = (uint)i
                };
            }

            BTreeNodeDTO[] btreeNodes = BuildCacheBTreeNodes(btreeRecords, (uint)btreeOffset);
            int recordsOffset = H8StaticDataFormat.AlignUp64(btreeOffset + (btreeNodes.Length * UnsafeUtility.SizeOf<BTreeNodeDTO>()));
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

                for (int i = 0; i < btreeNodes.Length; i++)
                    WriteStruct(basePtr + btreeOffset + (i * UnsafeUtility.SizeOf<BTreeNodeDTO>()), in btreeNodes[i]);

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
                    Flags = H8StaticDataFormat.LittleEndianFlag | H8StaticDataFormat.CacheBTreeFlag,
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

        private static BTreeNodeDTO[] BuildCacheBTreeNodes(BTreeBuildRecord[] records, uint btreeOffset)
        {
            if ((btreeOffset & 63u) != 0u)
                throw new InvalidDataException("B-Tree section offset is not 64-byte aligned.");

            if (records == null || records.Length == 0)
            {
                BTreeNodeDTO emptyRoot = default;
                emptyRoot.Meta = H8CacheBTree.MakeLeafMeta(0);
                return new[] { emptyRoot };
            }

            Array.Sort(records, CompareBTreeBuildRecordHashAscending);

            int leafCount = (records.Length + H8StaticDataFormat.BTreeNodeKeyCapacity - 1) / H8StaticDataFormat.BTreeNodeKeyCapacity;
            int maxNodeCount = (leafCount * 2) + 8;
            BTreeNodeDTO[] nodes = new BTreeNodeDTO[maxNodeCount];
            BTreeLevelEntry[] currentLevel = new BTreeLevelEntry[maxNodeCount];
            BTreeLevelEntry[] nextLevel = new BTreeLevelEntry[maxNodeCount];
            int nodeCount = 0;
            int currentCount = 0;

            for (int recordIndex = 0; recordIndex < records.Length;)
            {
                BTreeNodeDTO node = default;
                int keyCount = Math.Min(H8StaticDataFormat.BTreeNodeKeyCapacity, records.Length - recordIndex);
                for (int key = 0; key < keyCount; key++)
                {
                    BTreeBuildRecord record = records[recordIndex + key];
                    H8CacheBTree.SetKey(ref node, key, record.Key);
                    H8CacheBTree.SetChild(ref node, key, record.Value);
                }

                node.Meta = H8CacheBTree.MakeLeafMeta(keyCount);
                nodes[nodeCount] = node;
                currentLevel[currentCount] = new BTreeLevelEntry
                {
                    NodeIndex = nodeCount,
                    MaxKey = records[recordIndex + keyCount - 1].Key
                };
                nodeCount++;
                currentCount++;
                recordIndex += keyCount;
            }

            while (currentCount > 1)
            {
                int nextCount = 0;
                for (int levelIndex = 0; levelIndex < currentCount;)
                {
                    int childCount = Math.Min(H8StaticDataFormat.BTreeNodeChildCapacity, currentCount - levelIndex);
                    BTreeNodeDTO node = default;
                    for (int child = 0; child < childCount; child++)
                    {
                        BTreeLevelEntry childEntry = currentLevel[levelIndex + child];
                        H8CacheBTree.SetChild(
                            ref node,
                            child,
                            btreeOffset + ((uint)childEntry.NodeIndex * H8StaticDataFormat.CacheLineBytes));

                        if (child < childCount - 1)
                            H8CacheBTree.SetKey(ref node, child, childEntry.MaxKey);
                    }

                    node.Meta = H8CacheBTree.MakeInternalMeta(childCount - 1);
                    nodes[nodeCount] = node;
                    nextLevel[nextCount] = new BTreeLevelEntry
                    {
                        NodeIndex = nodeCount,
                        MaxKey = currentLevel[levelIndex + childCount - 1].MaxKey
                    };
                    nodeCount++;
                    nextCount++;
                    levelIndex += childCount;
                }

                BTreeLevelEntry[] swap = currentLevel;
                currentLevel = nextLevel;
                nextLevel = swap;
                currentCount = nextCount;
            }

            BTreeNodeDTO[] compact = new BTreeNodeDTO[nodeCount];
            Array.Copy(nodes, compact, nodeCount);
            return compact;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStruct<T>(byte* destination, in T value) where T : unmanaged
        {
            T local = value;
            UnsafeUtility.MemCpy(destination, &local, UnsafeUtility.SizeOf<T>());
        }

        private static int AlignOffsetWithRepair(int offset, ref int repairCount)
        {
            int aligned = H8StaticDataFormat.AlignUp64(offset);
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
            if (UnsafeUtility.SizeOf<BabelIndexDTO>() != 16)
                return Fail("Babel index DTO ABI drift: expected 16 bytes.");
            if (UnsafeUtility.SizeOf<BabelLookupResultDTO>() != 16)
                return Fail("Babel lookup result ABI drift: expected 16 bytes.");
            if (UnsafeUtility.SizeOf<BTreeNodeDTO>() != H8StaticDataFormat.CacheLineBytes)
                return Fail("Cache B-Tree node ABI drift: expected 64 bytes.");
            if (UnsafeUtility.AlignOf<BTreeNodeDTO>() < UnsafeUtility.AlignOf<uint>())
                return Fail("Cache B-Tree node ABI drift: expected at least uint alignment.");
            if (UnsafeUtility.SizeOf<DataOffsetLengthDTO>() != 16)
                return Fail("Cache B-Tree lookup result ABI drift: expected 16 bytes.");
            if (UnsafeUtility.SizeOf<BTreeTelemetryEntry>() != H8StaticDataFormat.CacheLineBytes)
                return Fail("Cache B-Tree telemetry ABI drift: expected 64 bytes.");
            if (UnsafeUtility.AlignOf<BTreeTelemetryEntry>() < UnsafeUtility.AlignOf<uint>())
                return Fail("Cache B-Tree telemetry ABI drift: expected at least uint alignment.");
            if (UnsafeUtility.SizeOf<BTreeTelemetryAccumulatorDTO>() != H8StaticDataFormat.CacheLineBytes)
                return Fail("Cache B-Tree accumulator ABI drift: expected 64 bytes.");
            if (UnsafeUtility.AlignOf<BTreeTelemetryAccumulatorDTO>() < UnsafeUtility.AlignOf<uint>())
                return Fail("Cache B-Tree accumulator ABI drift: expected at least uint alignment.");
            if (UnsafeUtility.SizeOf<BTreeTuningProfileDTO>() != H8StaticDataFormat.CacheLineBytes)
                return Fail("Cache B-Tree tuning profile ABI drift: expected 64 bytes.");
            if (UnsafeUtility.AlignOf<BTreeTuningProfileDTO>() < UnsafeUtility.AlignOf<uint>())
                return Fail("Cache B-Tree tuning profile ABI drift: expected at least uint alignment.");
            if (UnsafeUtility.SizeOf<MortonBTreeNodeDTO>() != H8StaticDataFormat.CacheLineBytes)
                return Fail("Spatial Morton B-Tree node ABI drift: expected 64 bytes.");
            if (UnsafeUtility.AlignOf<MortonBTreeNodeDTO>() < UnsafeUtility.AlignOf<ulong>())
                return Fail("Spatial Morton B-Tree node ABI drift: expected at least ulong alignment.");
            if (UnsafeUtility.SizeOf<SpatialMortonBTreeRecordDTO>() != 16)
                return Fail("Spatial Morton B-Tree record ABI drift: expected 16 bytes.");
            if (UnsafeUtility.AlignOf<SpatialMortonBTreeRecordDTO>() < UnsafeUtility.AlignOf<ulong>())
                return Fail("Spatial Morton B-Tree record ABI drift: expected at least ulong alignment.");
            if (UnsafeUtility.SizeOf<SpatialMortonLevelEntryDTO>() != 16)
                return Fail("Spatial Morton B-Tree level scratch ABI drift: expected 16 bytes.");
            if (UnsafeUtility.AlignOf<SpatialMortonLevelEntryDTO>() < UnsafeUtility.AlignOf<ulong>())
                return Fail("Spatial Morton B-Tree level scratch ABI drift: expected at least ulong alignment.");
            if (UnsafeUtility.SizeOf<H8StaticDataTelemetryEntry>() != 64)
                return Fail("Static data telemetry ABI drift: expected 64 bytes.");
            if (UnsafeUtility.SizeOf<H8StaticDataDumpHeader>() != H8StaticDataFormat.TelemetryDumpHeaderSizeBytes)
                return Fail("Static data telemetry dump header ABI drift: expected 32 bytes.");
            if (UnsafeUtility.SizeOf<H8ItemStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8EconomyStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8PhysicsStaticRecord>() != 48 ||
                UnsafeUtility.SizeOf<H8FaunaStaticRecord>() != 48)
            {
                return Fail("Static data record ABI drift: expected 48-byte records.");
            }

            uint schemaHash = ComputeSchemaHash();
            if (schemaHash != H8StaticDataFormat.SchemaHash)
                return Fail("Static data schema hash drift: expected 0x" + H8StaticDataFormat.SchemaHash.ToString("X8", CultureInfo.InvariantCulture) + " but computed 0x" + schemaHash.ToString("X8", CultureInfo.InvariantCulture) + ".");

            return new H8DataBakeResult { Success = true };
        }

        private static uint ComputeSchemaHash()
        {
            uint hash = H8DataHashTool.FnvOffset32;
            hash = HashAsciiTerminated(hash, ExpectedVersionText);
            hash = HashByte(hash, (byte)Schemas.Length);
            for (int i = 0; i < Schemas.Length; i++)
            {
                SheetSchema schema = Schemas[i];
                hash = HashAsciiTerminated(hash, schema.FileName);
                hash = HashUShort(hash, schema.RecordType);
                hash = HashByte(hash, (byte)schema.Columns.Length);
                for (int c = 0; c < schema.Columns.Length; c++)
                {
                    ColumnSpec column = schema.Columns[c];
                    hash = HashAsciiTerminated(hash, column.Name);
                    hash = HashByte(hash, (byte)column.Type);
                    hash = HashByte(hash, column.HasMin ? (byte)1 : (byte)0);
                    hash = HashByte(hash, column.HasMax ? (byte)1 : (byte)0);
                    if (column.HasMin)
                        hash = HashAsciiTerminated(hash, column.MinValue.ToString("R", CultureInfo.InvariantCulture));
                    if (column.HasMax)
                        hash = HashAsciiTerminated(hash, column.MaxValue.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            return hash == 0u ? H8DataHashTool.FnvOffset32 : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashByte(uint hash, byte value)
        {
            return unchecked((hash ^ value) * H8DataHashTool.FnvPrime32);
        }

        private static uint HashUShort(uint hash, ushort value)
        {
            hash = HashByte(hash, (byte)value);
            return HashByte(hash, (byte)(value >> 8));
        }

        private static uint HashAsciiTerminated(uint hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
                hash = HashByte(hash, (byte)value[i]);

            return HashByte(hash, 0);
        }

        private static uint AddString(List<BabelBuildEntry> stringPool, string value)
        {
            uint hash = H8DataHashTool.ComputeFnv1a32Utf8(value.AsSpan());
            for (int i = 0; i < stringPool.Count; i++)
            {
                BabelBuildEntry existing = stringPool[i];
                if (existing.Hash != hash)
                    continue;

                if (!string.Equals(existing.Text, value, StringComparison.Ordinal))
                    throw new InvalidDataException("Babel hash collision for text hash 0x" + hash.ToString("X8", CultureInfo.InvariantCulture));

                return hash;
            }

            stringPool.Add(new BabelBuildEntry
            {
                Hash = hash,
                Text = value
            });
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

        private static int CompareBTreeBuildRecordHashAscending(BTreeBuildRecord left, BTreeBuildRecord right)
        {
            return left.Key.CompareTo(right.Key);
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
            public readonly double MinValue;
            public readonly double MaxValue;
            public readonly bool HasMin;
            public readonly bool HasMax;

            private ColumnSpec(string name, ColumnType type)
            {
                Name = name;
                Type = type;
                MinValue = 0d;
                MaxValue = 0d;
                HasMin = false;
                HasMax = false;
            }

            private ColumnSpec(string name, ColumnType type, double minValue, double maxValue, bool hasMin, bool hasMax)
            {
                Name = name;
                Type = type;
                MinValue = minValue;
                MaxValue = maxValue;
                HasMin = hasMin;
                HasMax = hasMax;
            }

            public static ColumnSpec Key(string name) { return new ColumnSpec(name, ColumnType.Key); }
            public static ColumnSpec Version(string name) { return new ColumnSpec(name, ColumnType.Version); }
            public static ColumnSpec Text(string name) { return new ColumnSpec(name, ColumnType.Text); }
            public static ColumnSpec UInt(string name) { return new ColumnSpec(name, ColumnType.UInt); }
            public static ColumnSpec UIntMin(string name, double minValue) { return new ColumnSpec(name, ColumnType.UInt, minValue, 0d, true, false); }
            public static ColumnSpec UShort(string name) { return new ColumnSpec(name, ColumnType.UShort); }
            public static ColumnSpec UShortMin(string name, double minValue) { return new ColumnSpec(name, ColumnType.UShort, minValue, 0d, true, false); }
            public static ColumnSpec Int(string name) { return new ColumnSpec(name, ColumnType.Int); }
            public static ColumnSpec IntMin(string name, double minValue) { return new ColumnSpec(name, ColumnType.Int, minValue, 0d, true, false); }
            public static ColumnSpec Float(string name) { return new ColumnSpec(name, ColumnType.Float); }
            public static ColumnSpec FloatMin(string name, double minValue) { return new ColumnSpec(name, ColumnType.Float, minValue, 0d, true, false); }
            public static ColumnSpec FloatRange(string name, double minValue, double maxValue) { return new ColumnSpec(name, ColumnType.Float, minValue, maxValue, true, true); }
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

        private struct BTreeBuildRecord
        {
            public uint Key;
            public uint Value;
        }

        private struct BTreeLevelEntry
        {
            public int NodeIndex;
            public uint MaxKey;
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
                if (string.Equals(_headers[i], name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public int CountHeader(string name)
        {
            int count = 0;
            for (int i = 0; i < _headers.Length; i++)
            {
                if (string.Equals(_headers[i], name, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public int CountHeaderIgnoreCase(string name)
        {
            int count = 0;
            for (int i = 0; i < _headers.Length; i++)
            {
                if (string.Equals(_headers[i], name, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }
    }

    internal static class H8CsvReader
    {
        private const int CsvReadBufferBytes = 64 * 1024;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static H8CsvTable Read(string path)
        {
            string text;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, CsvReadBufferBytes, FileOptions.SequentialScan))
            using (StreamReader reader = new StreamReader(stream, StrictUtf8, true))
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

            if (inQuotes)
                throw new InvalidDataException("Unclosed quoted field in " + Path.GetFileName(path) + ".");

            FinishRow(rows, cells, cell);
            if (rows.Count == 0)
                return new H8CsvTable(Array.Empty<string>(), new List<string[]>(0));

            string[] headers = rows[0];
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Length != headers.Length)
                {
                    throw new InvalidDataException(
                        "Row " + (i + 1).ToString(CultureInfo.InvariantCulture) +
                        " has " + rows[i].Length.ToString(CultureInfo.InvariantCulture) +
                        " cells; expected " + headers.Length.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

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
#endif
}
