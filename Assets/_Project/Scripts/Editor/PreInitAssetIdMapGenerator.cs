using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    internal static class PreInitAssetIdMapGenerator
    {
        private const string MenuPath = "Hecton8/Bootstrap/Generate PreInit Asset GUID Map";
        private const string GeneratedFilePath = "Assets/_Project/Scripts/Optimization/GeneratedAssetGuidIdTable.cs";
        private const string AssetSearchRoot = "Assets/_Project";
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        [MenuItem(MenuPath)]
        internal static void Generate()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { AssetSearchRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            GeneratedAssetGuidRecord[] records = new GeneratedAssetGuidRecord[guids.Length]; // COLD ALLOC: GeneratedAssetGuidRecord[asset GUID count] - editor-only sorted GUID table emission - owner: PreInitAssetIdMapGenerator

            for (int i = 0; i < guids.Length; i++)
            {
                records[i] = new GeneratedAssetGuidRecord(
                    ComputeGuidHash(guids[i].AsSpan()),
                    (uint)(i + 1));
            }

            Array.Sort(records, CompareGeneratedRecords);

            StringBuilder copyBody = new StringBuilder(Mathf.Max(256, records.Length * 72));
            int emittedCount = 0;
            uint previousHash = 0u;
            for (int i = 0; i < records.Length; i++)
            {
                GeneratedAssetGuidRecord record = records[i];
                if (record.GuidHash == 0u || record.AssetId == 0u)
                    continue;

                if (i > 0 && record.GuidHash == previousHash)
                {
                    Debug.LogError("[PreInitAssetIdMapGenerator] Duplicate GUID hash skipped.");
                    continue;
                }

                copyBody.Append("            records[");
                copyBody.Append(emittedCount);
                copyBody.Append("] = new AssetGuidIdRecord(0x");
                    copyBody.Append(record.GuidHash.ToString("X8", CultureInfo.InvariantCulture));
                copyBody.Append("u, ");
                copyBody.Append(record.AssetId);
                copyBody.AppendLine("u);");
                emittedCount++;
                previousHash = record.GuidHash;
            }

            StringBuilder builder = new StringBuilder(Mathf.Max(1024, copyBody.Length + 256));
            builder.AppendLine("namespace Hecton8.Optimization");
            builder.AppendLine("{");
            builder.AppendLine("    internal static partial class GeneratedAssetGuidIdTable");
            builder.AppendLine("    {");
            builder.Append("        internal const int RecordCount = ");
            builder.Append(emittedCount);
            builder.AppendLine(";");
            builder.AppendLine();
            builder.AppendLine("        internal static void CopyTo(Unity.Collections.NativeArray<AssetGuidIdRecord> records)");
            builder.AppendLine("        {");
            builder.Append(copyBody.ToString());
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            string absolutePath = Path.GetFullPath(GeneratedFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            WriteTextAtomic(absolutePath, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(GeneratedFilePath);
            Debug.Log("[PreInitAssetIdMapGenerator] Generated " + guids.Length + " GUID mappings.");
        }

        private static void WriteTextAtomic(string path, string text, Encoding encoding)
        {
            string tempPath = path + ".tmp";
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                File.WriteAllText(tempPath, text, encoding);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null, true);
                else
                    File.Move(tempPath, path);
            }
            catch
            {
                TryDeleteFileNoThrow(tempPath);
                throw;
            }
        }

        private static void TryDeleteFileNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private readonly struct GeneratedAssetGuidRecord
        {
            internal readonly uint GuidHash;
            internal readonly uint AssetId;

            internal GeneratedAssetGuidRecord(uint guidHash, uint assetId)
            {
                GuidHash = guidHash;
                AssetId = assetId;
            }
        }

        private static int CompareGeneratedRecords(GeneratedAssetGuidRecord left, GeneratedAssetGuidRecord right)
        {
            int hashCompare = left.GuidHash.CompareTo(right.GuidHash);
            return hashCompare != 0 ? hashCompare : left.AssetId.CompareTo(right.AssetId);
        }

        private static uint ComputeGuidHash(ReadOnlySpan<char> guid)
        {
            if (guid.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffset;
                for (int i = 0; i < guid.Length; i++)
                {
                    char value = guid[i];
                    if (value == '-')
                        continue;

                    if ((uint)(value - 'A') <= 5u)
                        value = (char)(value + 32);

                    hash ^= value;
                    hash *= FnvPrime;
                }

                return hash != 0u ? hash : 1u;
            }
        }
    }
}
