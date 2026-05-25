#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    public static class H8DataMonolithCorruptionFuzzer
    {
        private const string AgentId = "X_002";
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_CORRUPTION_FUZZER_X_002.json";
        private const string TempFolder = "Temp/DataMonolithFuzzer";

        [MenuItem("Hecton8/Data Monolith/Run Corruption Fuzzer")]
        public static void RunFromMenu()
        {
            bool passed = Run();
            Debug.Log("[H8DataMonolithCorruptionFuzzer] passed=" + passed + " report=" + ReportPath);
        }

        internal static bool Run()
        {
            string projectRoot = ResolveProjectRoot();
            string outputPath = Path.Combine(projectRoot, H8DataMonolithCompiler.OutputAssetPath);
            string tempFolder = Path.Combine(projectRoot, TempFolder);
            Directory.CreateDirectory(tempFolder);

            StringBuilder cases = new StringBuilder(12288);
            int expectedCaseCount = 0;
            int passCount = 0;
            int failCount = 0;
            string setupError = string.Empty;

            if (!File.Exists(outputPath) || !H8DataMonolithCompiler.TryValidateBlobFile(outputPath, out setupError))
            {
                if (!H8DataMonolithCompiler.BakeAll(logSummary: false))
                    setupError = H8DataMonolithCompiler.LastError;
            }

            if (!File.Exists(outputPath) || !H8DataMonolithCompiler.TryValidateBlobFile(outputPath, out setupError))
            {
                WriteReport(projectRoot, false, setupError, passCount, failCount, cases);
                return false;
            }

            byte[] baseline = File.ReadAllBytes(outputPath);
            RunCase(projectRoot, tempFolder, "bad_magic", baseline, MutateMagic, "magic", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_stored_checksum", baseline, MutateStoredChecksum, "checksum", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_payload_checksum", baseline, MutatePayloadByte, "checksum", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "truncated_blob", baseline, MutateTruncate, "mismatch", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_table_offset", baseline, MutateSectionTableOffset, "Section table range", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_directory_magic", baseline, MutateDirectoryMagic, "Directory magic", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_directory_identity", baseline, MutateDirectoryIdentity, "identity", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_data_start_offset", baseline, MutateDataStartOffset, "Data start", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_record_size", baseline, MutateSectionRecordSize, "record size mismatch", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_unaligned_offset", baseline, MutateSectionUnalignedOffset, "aligned", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_out_of_bounds", baseline, MutateSectionOutOfBounds, "range exceeds", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_overlap", baseline, MutateSectionOverlap, "canonical", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_localization_directory", baseline, MutateLocalizationDirectory, "localization", ref passCount, ref failCount, ref expectedCaseCount, cases);

            bool passed = failCount == 0 && passCount == expectedCaseCount;
            WriteReport(projectRoot, passed, setupError, passCount, failCount, cases);
            return passed;
        }

        private static void RunCase(
            string projectRoot,
            string tempFolder,
            string name,
            byte[] baseline,
            Func<byte[], byte[]> mutate,
            string expectedErrorToken,
            ref int passCount,
            ref int failCount,
            ref int expectedCaseCount,
            StringBuilder cases)
        {
            expectedCaseCount++;
            byte[] mutated = mutate(baseline);
            string path = Path.Combine(tempFolder, name + ".h8bin");
            File.WriteAllBytes(path, mutated);

            bool valid = H8DataMonolithCompiler.TryValidateBlobFile(path, out string error);
            bool passed = !valid && error.IndexOf(expectedErrorToken, StringComparison.OrdinalIgnoreCase) >= 0;
            if (passed)
                passCount++;
            else
                failCount++;

            if (cases.Length > 0)
                cases.AppendLine(",");

            cases.Append("    { \"case\": \"").Append(name)
                .Append("\", \"passed\": ").Append(passed ? "true" : "false")
                .Append(", \"valid\": ").Append(valid ? "true" : "false")
                .Append(", \"expectedErrorToken\": \"").Append(Escape(expectedErrorToken))
                .Append("\", \"error\": \"").Append(Escape(error))
                .Append("\", \"path\": \"").Append(Escape(MakeRelative(projectRoot, path)))
                .Append("\" }");
        }

        private static byte[] MutateMagic(byte[] source)
        {
            byte[] bytes = Clone(source);
            bytes[0] ^= 0xFF;
            return bytes;
        }

        private static byte[] MutatePayloadByte(byte[] source)
        {
            byte[] bytes = Clone(source);
            bytes[bytes.Length - 1] ^= 0x5A;
            return bytes;
        }

        private static byte[] MutateStoredChecksum(byte[] source)
        {
            byte[] bytes = Clone(source);
            WriteUInt64(bytes, 8, 0UL);
            return bytes;
        }

        private static byte[] MutateTruncate(byte[] source)
        {
            int truncatedLength = Math.Max(0, source.Length - Hecton8.Data.H8DataLayoutConstants.SectionAlignmentBytes);
            byte[] bytes = new byte[truncatedLength];
            Buffer.BlockCopy(source, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static byte[] MutateSectionTableOffset(byte[] source)
        {
            byte[] bytes = Clone(source);
            uint badOffset = (uint)Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes +
                             (uint)Hecton8.Data.H8DataLayoutConstants.DirectorySizeBytes +
                             16u;
            WriteUInt32(bytes, Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes + 8, badOffset);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateDirectoryMagic(byte[] source)
        {
            byte[] bytes = Clone(source);
            bytes[Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes] ^= 0x5A;
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateDirectoryIdentity(byte[] source)
        {
            byte[] bytes = Clone(source);
            WriteUInt32(bytes, Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes + 32, 0u);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateDataStartOffset(byte[] source)
        {
            byte[] bytes = Clone(source);
            uint expectedDataStart = AlignUp(
                (uint)Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes +
                (uint)Hecton8.Data.H8DataLayoutConstants.DirectorySizeBytes +
                ((uint)Hecton8.Data.H8DataSectionId.PhysicsConstants * 16u),
                Hecton8.Data.H8DataLayoutConstants.SectionAlignmentBytes);
            WriteUInt32(bytes, Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes + 20, expectedDataStart + 1u);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionRecordSize(byte[] source)
        {
            byte[] bytes = Clone(source);
            int firstEntry = (int)ReadUInt32(bytes, 28);
            WriteUInt32(bytes, firstEntry + 4, 1u);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionUnalignedOffset(byte[] source)
        {
            byte[] bytes = Clone(source);
            int firstEntry = (int)ReadUInt32(bytes, 28);
            uint offset = ReadUInt32(bytes, firstEntry + 12);
            WriteUInt32(bytes, firstEntry + 12, offset + 1u);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionOutOfBounds(byte[] source)
        {
            byte[] bytes = Clone(source);
            int firstEntry = (int)ReadUInt32(bytes, 28);
            WriteUInt32(bytes, firstEntry + 12, (uint)(bytes.Length - Hecton8.Data.H8DataLayoutConstants.SectionAlignmentBytes));
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateSectionOverlap(byte[] source)
        {
            byte[] bytes = Clone(source);
            int firstEntry = (int)ReadUInt32(bytes, 28);
            int secondEntry = firstEntry + 16;
            uint firstOffset = ReadUInt32(bytes, firstEntry + 12);
            WriteUInt32(bytes, secondEntry + 12, firstOffset);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static byte[] MutateLocalizationDirectory(byte[] source)
        {
            byte[] bytes = Clone(source);
            WriteUInt32(bytes, Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes + 24, Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes);
            RecomputeHeaderChecksum(bytes);
            return bytes;
        }

        private static void RecomputeHeaderChecksum(byte[] bytes)
        {
            ulong checksum = H8DataMonolithCompiler.ComputeHash64(
                bytes,
                Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes,
                bytes.Length - Hecton8.Data.H8DataLayoutConstants.HeaderSizeBytes);
            WriteUInt64(bytes, 8, checksum);
        }

        private static uint AlignUp(uint value, int alignment)
        {
            uint mask = (uint)alignment - 1u;
            return (value + mask) & ~mask;
        }

        private static byte[] Clone(byte[] source)
        {
            byte[] bytes = new byte[source.Length];
            Buffer.BlockCopy(source, 0, bytes, 0, source.Length);
            return bytes;
        }

        private static void WriteReport(
            string projectRoot,
            bool passed,
            string setupError,
            int passCount,
            int failCount,
            StringBuilder cases)
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_MONOLITH_CORRUPTION_FUZZER_V1\",");
            report.AppendLine("  \"agent\": \"" + AgentId + "\",");
            report.AppendLine("  \"status\": \"" + (passed ? "PASS" : "FAIL") + "\",");
            report.AppendLine("  \"passCount\": " + passCount + ",");
            report.AppendLine("  \"failCount\": " + failCount + ",");
            report.AppendLine("  \"setupError\": \"" + Escape(setupError) + "\",");
            report.AppendLine("  \"cases\": [");
            report.Append(cases);
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            WriteText(Path.Combine(projectRoot, ReportPath), report.ToString());
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo directory = Directory.GetParent(Application.dataPath);
            return directory == null ? string.Empty : directory.FullName;
        }

        private static string MakeRelative(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(path);
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                int start = normalizedRoot.Length;
                if (start < normalizedPath.Length &&
                    (normalizedPath[start] == Path.DirectorySeparatorChar || normalizedPath[start] == Path.AltDirectorySeparatorChar))
                {
                    start++;
                }

                return normalizedPath.Substring(start).Replace('\\', '/');
            }

            return path.Replace('\\', '/');
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                   ((uint)bytes[offset + 1] << 8) |
                   ((uint)bytes[offset + 2] << 16) |
                   ((uint)bytes[offset + 3] << 24);
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
            bytes[offset + 4] = (byte)(value >> 32);
            bytes[offset + 5] = (byte)(value >> 40);
            bytes[offset + 6] = (byte)(value >> 48);
            bytes[offset + 7] = (byte)(value >> 56);
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
#endif
