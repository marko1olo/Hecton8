#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Data;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    public static class H8DataMonolithCorruptionFuzzer
    {
        private const string AgentId = "X_002";
        private const string AgentId1313 = "1313";
        private const string AgentId1330 = "1330";
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_CORRUPTION_FUZZER_X_002.json";
        private const string ReportPath1313 = "Docs/Reports/DATA_MONOLITH_CORRUPTION_FUZZER_1313.json";
        private const string ReportPath1330 = "Docs/Reports/DATA_MONOLITH_CORRUPTION_FUZZER_1330.json";
        private const string TempFolder = "Temp/DataMonolithFuzzer";

        [MenuItem("Hecton8/Data Monolith/Run Corruption Fuzzer")]
        public static void RunFromMenu()
        {
            bool passed = Run();
            Debug.Log("[H8DataMonolithCorruptionFuzzer] passed=" + passed + " report=" + ReportPath + " report1313=" + ReportPath1313 + " report1330=" + ReportPath1330);
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

            RunCase(projectRoot, tempFolder, "bad_magic", outputPath, MutateMagic, "magic", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_stored_checksum", outputPath, MutateStoredChecksum, "checksum", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_payload_checksum", outputPath, MutatePayloadByte, "checksum", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "truncated_blob", outputPath, MutateTruncate, "mismatch", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_header_unknown_flags", outputPath, MutateHeaderUnknownFlags, "flags", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_header_reserved", outputPath, MutateHeaderReserved, "reserved", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_directory_reserved", outputPath, MutateDirectoryReserved, "reserved", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_header_section_count", outputPath, MutateHeaderSectionCount, "sections", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_header_section_table_offset", outputPath, MutateHeaderSectionTableOffset, "tableOffset", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_table_offset", outputPath, MutateSectionTableOffset, "Section table range", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_directory_magic", outputPath, MutateDirectoryMagic, "Directory magic", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_directory_identity", outputPath, MutateDirectoryIdentity, "identity", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_data_start_offset", outputPath, MutateDataStartOffset, "Data start", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_record_size", outputPath, MutateSectionRecordSize, "record size mismatch", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_unaligned_offset", outputPath, MutateSectionUnalignedOffset, "aligned", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_out_of_bounds", outputPath, MutateSectionOutOfBounds, "range exceeds", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_section_overlap", outputPath, MutateSectionOverlap, "canonical", ref passCount, ref failCount, ref expectedCaseCount, cases);
            RunCase(projectRoot, tempFolder, "bad_localization_directory", outputPath, MutateLocalizationDirectory, "localization", ref passCount, ref failCount, ref expectedCaseCount, cases);

            bool passed = failCount == 0 && passCount == expectedCaseCount;
            WriteReport(projectRoot, passed, setupError, passCount, failCount, cases);
            return passed;
        }

        private delegate bool BlobFileMutation(string path, out string error);

        private static void RunCase(
            string projectRoot,
            string tempFolder,
            string name,
            string baselinePath,
            BlobFileMutation mutate,
            string expectedErrorToken,
            ref int passCount,
            ref int failCount,
            ref int expectedCaseCount,
            StringBuilder cases)
        {
            expectedCaseCount++;
            string path = Path.Combine(tempFolder, name + ".h8bin");
            bool mutationReady = TryCopyFile(baselinePath, path, out string mutationError) &&
                                 mutate(path, out mutationError);
            string error = mutationError;
            bool valid = false;
            if (mutationReady)
                valid = H8DataMonolithCompiler.TryValidateBlobFile(path, out error);
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

        private static bool MutateMagic(string path, out string error)
        {
            return TryXorByte(path, 0L, 0xFF, out error);
        }

        private static bool MutatePayloadByte(string path, out string error)
        {
            if (!TryGetFileLength(path, out long length, out error))
                return false;

            long offset = Math.Max(H8DataLayoutConstants.HeaderSizeBytes, length - 1L);
            return TryXorByte(path, offset, 0x5A, out error);
        }

        private static bool MutateStoredChecksum(string path, out string error)
        {
            return TryWriteUInt64(path, 8L, 0UL, out error);
        }

        private static bool MutateTruncate(string path, out string error)
        {
            if (!TryGetFileLength(path, out long length, out error))
                return false;

            long truncatedLength = Math.Max(0L, length - H8DataLayoutConstants.SectionAlignmentBytes);
            return TrySetFileLength(path, truncatedLength, out error);
        }

        private static bool MutateHeaderUnknownFlags(string path, out string error)
        {
            return TryWriteUInt32(path, 36L, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u, out error) &&
                   TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 32L, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateHeaderReserved(string path, out string error)
        {
            return TryWriteUInt32(path, 52L, 1u, out error);
        }

        private static bool MutateDirectoryReserved(string path, out string error)
        {
            return TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 44L, 1u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateHeaderSectionCount(string path, out string error)
        {
            return TryWriteUInt32(path, 32L, (uint)H8DataSectionId.PhysicsConstants - 1u, out error);
        }

        private static bool MutateHeaderSectionTableOffset(string path, out string error)
        {
            return TryWriteUInt32(path, 28L, (uint)(H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + 64), out error);
        }

        private static bool MutateSectionTableOffset(string path, out string error)
        {
            uint badOffset = (uint)H8DataLayoutConstants.HeaderSizeBytes +
                             (uint)H8DataLayoutConstants.DirectorySizeBytes +
                             16u;
            return TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 8L, badOffset, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateDirectoryMagic(string path, out string error)
        {
            return TryXorByte(path, H8DataLayoutConstants.HeaderSizeBytes, 0x5A, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateDirectoryIdentity(string path, out string error)
        {
            return TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 32L, 0u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateDataStartOffset(string path, out string error)
        {
            uint expectedDataStart = AlignUp(
                (uint)H8DataLayoutConstants.HeaderSizeBytes +
                (uint)H8DataLayoutConstants.DirectorySizeBytes +
                ((uint)H8DataSectionId.PhysicsConstants * 16u),
                H8DataLayoutConstants.SectionAlignmentBytes);
            return TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 20L, expectedDataStart + 1u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateSectionRecordSize(string path, out string error)
        {
            if (!TryReadUInt32(path, 28L, out uint firstEntry, out error))
                return false;

            return TryWriteUInt32(path, (long)firstEntry + 4L, 1u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateSectionUnalignedOffset(string path, out string error)
        {
            if (!TryReadUInt32(path, 28L, out uint firstEntry, out error) ||
                !TryReadUInt32(path, (long)firstEntry + 12L, out uint offset, out error))
            {
                return false;
            }

            return TryWriteUInt32(path, (long)firstEntry + 12L, offset + 1u, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateSectionOutOfBounds(string path, out string error)
        {
            if (!TryGetFileLength(path, out long length, out error) ||
                !TryReadUInt32(path, 28L, out uint firstEntry, out error))
            {
                return false;
            }

            uint badOffset = (uint)Math.Max(0L, length - H8DataLayoutConstants.SectionAlignmentBytes);
            return TryWriteUInt32(path, (long)firstEntry + 12L, badOffset, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateSectionOverlap(string path, out string error)
        {
            if (!TryReadUInt32(path, 28L, out uint firstEntry, out error) ||
                !TryReadUInt32(path, (long)firstEntry + 12L, out uint firstOffset, out error))
            {
                return false;
            }

            uint secondEntry = firstEntry + 16u;
            return TryWriteUInt32(path, (long)secondEntry + 12L, firstOffset, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool MutateLocalizationDirectory(string path, out string error)
        {
            return TryWriteUInt32(path, H8DataLayoutConstants.HeaderSizeBytes + 24L, H8DataLayoutConstants.HeaderSizeBytes, out error) &&
                   RecomputeHeaderChecksum(path, out error);
        }

        private static bool RecomputeHeaderChecksum(string path, out string error)
        {
            if (!TryGetFileLength(path, out long length, out error))
                return false;

            if (!H8DataMonolithCompiler.TryComputeFileHash64(
                    path,
                    H8DataLayoutConstants.HeaderSizeBytes,
                    length - H8DataLayoutConstants.HeaderSizeBytes,
                    out ulong checksum,
                    out error))
            {
                return false;
            }

            return TryWriteUInt64(path, 8L, checksum, out error);
        }

        private static uint AlignUp(uint value, int alignment)
        {
            uint mask = (uint)alignment - 1u;
            return (value + mask) & ~mask;
        }

        private static bool TryCopyFile(string sourcePath, string destinationPath, out string error)
        {
            error = string.Empty;
            try
            {
                File.Copy(sourcePath, destinationPath, true);
                return true;
            }
            catch (IOException ex) { return FailFileMutation("copy", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("copy", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("copy", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("copy", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("copy", ex.Message, out error); }
        }

        private static bool TryGetFileLength(string path, out long length, out string error)
        {
            length = 0L;
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                length = stream.Length;
                return true;
            }
            catch (IOException ex) { return FailFileMutation("length", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("length", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("length", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("length", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("length", ex.Message, out error); }
        }

        private static bool TrySetFileLength(string path, long length, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                stream.SetLength(length);
                return true;
            }
            catch (IOException ex) { return FailFileMutation("truncate", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("truncate", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("truncate", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("truncate", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("truncate", ex.Message, out error); }
        }

        private static bool TryXorByte(string path, long offset, byte mask, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (offset < 0L || offset >= stream.Length)
                    return FailFileMutation("xor", "offset outside file: " + offset, out error);

                stream.Position = offset;
                int value = stream.ReadByte();
                if (value < 0)
                    return FailFileMutation("xor", "failed to read byte at offset: " + offset, out error);

                stream.Position = offset;
                stream.WriteByte((byte)(value ^ mask));
                return true;
            }
            catch (IOException ex) { return FailFileMutation("xor", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("xor", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("xor", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("xor", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("xor", ex.Message, out error); }
        }

        private static bool TryReadUInt32(string path, long offset, out uint value, out string error)
        {
            value = 0u;
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (offset < 0L || offset + 4L > stream.Length)
                    return FailFileMutation("read_u32", "offset outside file: " + offset, out error);

                stream.Position = offset;
                int b0 = stream.ReadByte();
                int b1 = stream.ReadByte();
                int b2 = stream.ReadByte();
                int b3 = stream.ReadByte();
                if ((b0 | b1 | b2 | b3) < 0)
                    return FailFileMutation("read_u32", "short read at offset: " + offset, out error);

                value = (uint)b0 | ((uint)b1 << 8) | ((uint)b2 << 16) | ((uint)b3 << 24);
                return true;
            }
            catch (IOException ex) { return FailFileMutation("read_u32", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("read_u32", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("read_u32", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("read_u32", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("read_u32", ex.Message, out error); }
        }

        private static bool TryWriteUInt32(string path, long offset, uint value, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (offset < 0L || offset + 4L > stream.Length)
                    return FailFileMutation("write_u32", "offset outside file: " + offset, out error);

                stream.Position = offset;
                stream.WriteByte((byte)value);
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 24));
                return true;
            }
            catch (IOException ex) { return FailFileMutation("write_u32", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("write_u32", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("write_u32", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("write_u32", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("write_u32", ex.Message, out error); }
        }

        private static bool TryWriteUInt64(string path, long offset, ulong value, out string error)
        {
            error = string.Empty;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (offset < 0L || offset + 8L > stream.Length)
                    return FailFileMutation("write_u64", "offset outside file: " + offset, out error);

                stream.Position = offset;
                stream.WriteByte((byte)value);
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 24));
                stream.WriteByte((byte)(value >> 32));
                stream.WriteByte((byte)(value >> 40));
                stream.WriteByte((byte)(value >> 48));
                stream.WriteByte((byte)(value >> 56));
                return true;
            }
            catch (IOException ex) { return FailFileMutation("write_u64", ex.Message, out error); }
            catch (UnauthorizedAccessException ex) { return FailFileMutation("write_u64", ex.Message, out error); }
            catch (ArgumentException ex) { return FailFileMutation("write_u64", ex.Message, out error); }
            catch (NotSupportedException ex) { return FailFileMutation("write_u64", ex.Message, out error); }
            catch (System.Security.SecurityException ex) { return FailFileMutation("write_u64", ex.Message, out error); }
        }

        private static bool FailFileMutation(string stage, string message, out string error)
        {
            error = stage + ": " + message;
            return false;
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
            string text = report.ToString();
            WriteText(Path.Combine(projectRoot, ReportPath), text);
            WriteText(Path.Combine(projectRoot, ReportPath1313), text.Replace("\"agent\": \"" + AgentId + "\"", "\"agent\": \"" + AgentId1313 + "\""));
            WriteText(Path.Combine(projectRoot, ReportPath1330), text.Replace("\"agent\": \"" + AgentId + "\"", "\"agent\": \"" + AgentId1330 + "\""));
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
