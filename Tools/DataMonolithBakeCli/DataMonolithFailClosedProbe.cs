using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Data;
using Hecton8.EditorValidation;
using Unity.Collections;

namespace Hecton8.Tools.DataMonolithBakeCli
{
    internal static unsafe class DataMonolithFailClosedProbe
    {
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_FAIL_CLOSED_RUNTIME_SIM_X_002.json";
        private const int AlignmentBytes = 64;
        private const int CaseCount = 13;
        private const int ValidationIterations = 256;

        private const int FailureNone = 0;
        private const int FailureTooSmall = 1;
        private const int FailureHeader = 2;
        private const int FailureBadChecksum = 3;
        private const int FailureDirectory = 4;
        private const int FailureSectionOrder = 5;
        private const int FailureSectionRecordSize = 6;
        private const int FailureSectionAlignment = 7;
        private const int FailureSectionOutOfRange = 8;
        private const int FailureLocalization = 9;

        public static bool Run(string projectRoot)
        {
            string blobPath = Path.Combine(projectRoot, H8DataMonolithCompiler.OutputAssetPath);
            string reportPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

            if (!File.Exists(blobPath))
            {
                WriteReport(reportPath, false, "static_data.h8bin missing", 0, 0, 0, 0, 0, 0, Array.Empty<CaseResult>());
                return false;
            }

            byte[] baseline = File.ReadAllBytes(blobPath);
            nuint arenaBytes = (nuint)AlignUp(baseline.Length, AlignmentBytes);
            void* active = NativeMemory.AlignedAlloc(arenaBytes, AlignmentBytes);
            void* candidate = NativeMemory.AlignedAlloc(arenaBytes, AlignmentBytes);
            if (active == null || candidate == null)
            {
                if (active != null)
                    NativeMemory.AlignedFree(active);
                if (candidate != null)
                    NativeMemory.AlignedFree(candidate);
                WriteReport(reportPath, false, "NativeMemory.AlignedAlloc failed", baseline.Length, 0, 0, 0, 0, 0, Array.Empty<CaseResult>());
                return false;
            }

            try
            {
                fixed (byte* source = baseline)
                {
                    Buffer.MemoryCopy(source, active, (long)arenaBytes, baseline.Length);
                }

                if (!ValidateResidentBlob((byte*)active, baseline.Length, out int baselineFailure))
                {
                    WriteReport(reportPath, false, "baseline validation failed code=" + baselineFailure, baseline.Length, 0, 0, 0, 0, 0, Array.Empty<CaseResult>());
                    return false;
                }

                ulong publishedChecksum = ReadUInt64((byte*)active, 8);
                int publishCount = 1;
                CaseResult[] results = new CaseResult[CaseCount];
                results[0] = RunCase("bad_stored_checksum", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureBadChecksum, MutateStoredChecksum);
                results[1] = RunCase("bad_payload_checksum", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureBadChecksum, MutatePayloadByte);
                results[2] = RunCase("bad_header_unknown_flags", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader, MutateHeaderUnknownFlags);
                results[3] = RunCase("bad_header_reserved", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader, MutateHeaderReserved);
                results[4] = RunCase("bad_directory_reserved", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureDirectory, MutateDirectoryReserved);
                results[5] = RunCase("bad_header_section_count", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader, MutateHeaderSectionCount);
                results[6] = RunCase("bad_header_section_table_offset", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader, MutateHeaderSectionTableOffset);
                results[7] = RunCase("bad_section_out_of_bounds", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureSectionOutOfRange, MutateSectionOutOfBounds);
                results[8] = RunCase("bad_section_unaligned_offset", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureSectionAlignment, MutateSectionUnalignedOffset);
                results[9] = RunCase("bad_section_table_void", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader, MutateSectionTableVoid);
                results[10] = RunCase("bad_section_overlap", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureSectionOutOfRange, MutateSectionOverlap);
                results[11] = RunCase("bad_localization_directory", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureLocalization, MutateLocalizationDirectory);
                results[12] = RunTruncatedCase("truncated_blob", baseline, candidate, arenaBytes, publishedChecksum, publishCount, FailureHeader);

                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                long validationAllocBefore = GC.GetAllocatedBytesForCurrentThread();
                long validationStart = Stopwatch.GetTimestamp();
                int validationPasses = 0;
                int failureAccumulator = 0;
                for (int i = 0; i < ValidationIterations; i++)
                {
                    if (ValidateResidentBlob((byte*)active, baseline.Length, out int failureCode))
                        validationPasses++;
                    failureAccumulator |= failureCode;
                }

                long validationTicks = Stopwatch.GetTimestamp() - validationStart;
                long validationAllocated = GC.GetAllocatedBytesForCurrentThread() - validationAllocBefore;

                bool passed = validationPasses == ValidationIterations &&
                              failureAccumulator == FailureNone &&
                              validationAllocated == 0L;
                for (int i = 0; i < results.Length; i++)
                    passed &= results[i].Passed;

                WriteReport(
                    reportPath,
                    passed,
                    string.Empty,
                    baseline.Length,
                    publishCount,
                    publishedChecksum,
                    validationTicks,
                    validationAllocated,
                    validationPasses,
                    results);
                return passed;
            }
            finally
            {
                NativeMemory.AlignedFree(active);
                NativeMemory.AlignedFree(candidate);
            }
        }

        private static CaseResult RunCase(
            string name,
            byte[] baseline,
            void* candidate,
            nuint candidateBytes,
            ulong publishedChecksum,
            int publishCount,
            int expectedFailure,
            CandidateMutator mutate)
        {
            fixed (byte* source = baseline)
            {
                Buffer.MemoryCopy(source, candidate, (long)candidateBytes, baseline.Length);
            }

            mutate((byte*)candidate, baseline.Length);
            bool valid = ValidateResidentBlob((byte*)candidate, baseline.Length, out int failureCode);
            bool published = valid;
            int finalPublishCount = published ? publishCount + 1 : publishCount;
            ulong finalChecksum = published ? ReadUInt64((byte*)candidate, 8) : publishedChecksum;
            bool passed = !valid &&
                          !published &&
                          failureCode == expectedFailure &&
                          finalPublishCount == publishCount &&
                          finalChecksum == publishedChecksum;
            return new CaseResult(name, passed, valid, failureCode, expectedFailure, finalPublishCount, finalChecksum);
        }

        private static CaseResult RunTruncatedCase(
            string name,
            byte[] baseline,
            void* candidate,
            nuint candidateBytes,
            ulong publishedChecksum,
            int publishCount,
            int expectedFailure)
        {
            int truncatedLength = baseline.Length - H8DataLayoutConstants.SectionAlignmentBytes;
            fixed (byte* source = baseline)
            {
                Buffer.MemoryCopy(source, candidate, (long)candidateBytes, truncatedLength);
            }

            bool valid = ValidateResidentBlob((byte*)candidate, truncatedLength, out int failureCode);
            int finalPublishCount = valid ? publishCount + 1 : publishCount;
            ulong finalChecksum = valid ? ReadUInt64((byte*)candidate, 8) : publishedChecksum;
            bool passed = !valid &&
                          failureCode == expectedFailure &&
                          finalPublishCount == publishCount &&
                          finalChecksum == publishedChecksum;
            return new CaseResult(name, passed, valid, failureCode, expectedFailure, finalPublishCount, finalChecksum);
        }

        private static void MutateStoredChecksum(byte* bytes, int length)
        {
            WriteUInt64(bytes, 8, 0UL);
        }

        private static void MutatePayloadByte(byte* bytes, int length)
        {
            bytes[length - 1] ^= 0x5A;
        }

        private static void MutateHeaderUnknownFlags(byte* bytes, int length)
        {
            WriteUInt32(bytes, 36, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u);
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 32, H8DataLayoutConstants.BlobFlagLittleEndian | 0x2u);
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateHeaderReserved(byte* bytes, int length)
        {
            WriteUInt32(bytes, 52, 1u);
        }

        private static void MutateDirectoryReserved(byte* bytes, int length)
        {
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 44, 1u);
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateHeaderSectionCount(byte* bytes, int length)
        {
            WriteUInt32(bytes, 32, (uint)H8DataSectionId.PhysicsConstants - 1u);
        }

        private static void MutateHeaderSectionTableOffset(byte* bytes, int length)
        {
            WriteUInt32(bytes, 28, H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes + 64u);
        }

        private static void MutateSectionOutOfBounds(byte* bytes, int length)
        {
            int firstEntry = (int)ReadUInt32(bytes, 28);
            WriteUInt32(bytes, firstEntry + 12, (uint)(length - H8DataLayoutConstants.SectionAlignmentBytes));
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateSectionUnalignedOffset(byte* bytes, int length)
        {
            int firstEntry = (int)ReadUInt32(bytes, 28);
            uint offset = ReadUInt32(bytes, firstEntry + 12);
            WriteUInt32(bytes, firstEntry + 12, offset + 1u);
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateSectionTableVoid(byte* bytes, int length)
        {
            WriteUInt32(bytes, 28, (uint)(length - 16));
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 8, (uint)(length - 16));
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateSectionOverlap(byte* bytes, int length)
        {
            int firstEntry = (int)ReadUInt32(bytes, 28);
            int secondEntry = firstEntry + 16;
            uint firstOffset = ReadUInt32(bytes, firstEntry + 12);
            WriteUInt32(bytes, secondEntry + 12, firstOffset);
            RecomputeHeaderChecksum(bytes, length);
        }

        private static void MutateLocalizationDirectory(byte* bytes, int length)
        {
            WriteUInt32(bytes, H8DataLayoutConstants.HeaderSizeBytes + 24, H8DataLayoutConstants.HeaderSizeBytes);
            RecomputeHeaderChecksum(bytes, length);
        }

        private static bool ValidateResidentBlob(byte* bytes, int length, out int failureCode)
        {
            failureCode = FailureNone;
            if (bytes == null || length < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                failureCode = FailureTooSmall;
                return false;
            }

            if (ReadUInt32(bytes, 0) != H8DataLayoutConstants.BlobMagic ||
                ReadUInt16(bytes, 4) != H8DataLayoutConstants.FormatVersion ||
                ReadUInt16(bytes, 6) != H8DataLayoutConstants.HeaderSizeBytes ||
                ReadUInt32(bytes, 16) != length ||
                ReadUInt32(bytes, 20) != H8DataLayoutConstants.HeaderSizeBytes ||
                ReadUInt32(bytes, 24) != H8DataLayoutConstants.DirectorySizeBytes ||
                ReadUInt32(bytes, 28) != H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes ||
                ReadUInt32(bytes, 32) != (uint)H8DataSectionId.PhysicsConstants ||
                ReadUInt32(bytes, 36) != H8DataLayoutConstants.BlobFlagLittleEndian ||
                ReadUInt32(bytes, 48) != H8DataLayoutConstants.SchemaHash ||
                ReadUInt32(bytes, 52) != 0u ||
                ReadUInt32(bytes, 56) != 0u ||
                ReadUInt32(bytes, 60) != 0u)
            {
                failureCode = FailureHeader;
                return false;
            }

            ulong storedChecksum = ReadUInt64(bytes, 8);
            ulong computedChecksum = ComputeHash64(bytes, H8DataLayoutConstants.HeaderSizeBytes, length - H8DataLayoutConstants.HeaderSizeBytes);
            if (storedChecksum != computedChecksum)
            {
                failureCode = FailureBadChecksum;
                return false;
            }

            int directoryOffset = H8DataLayoutConstants.HeaderSizeBytes;
            ushort directorySectionCount = ReadUInt16(bytes, directoryOffset + 6);
            uint sectionTableOffset = ReadUInt32(bytes, directoryOffset + 8);
            uint sectionTableBytes = ReadUInt32(bytes, directoryOffset + 12);
            uint directoryBlobBytes = ReadUInt32(bytes, directoryOffset + 16);
            uint dataStartOffset = ReadUInt32(bytes, directoryOffset + 20);
            uint localizationOffset = ReadUInt32(bytes, directoryOffset + 24);
            uint localizationBytes = ReadUInt32(bytes, directoryOffset + 28);
            if (ReadUInt32(bytes, directoryOffset) != H8DataLayoutConstants.BlobMagic ||
                ReadUInt16(bytes, directoryOffset + 4) != H8DataLayoutConstants.FormatVersion ||
                directorySectionCount != (ushort)H8DataSectionId.PhysicsConstants ||
                sectionTableOffset != ReadUInt32(bytes, 28) ||
                sectionTableOffset != H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes ||
                sectionTableBytes != directorySectionCount * 16u ||
                directoryBlobBytes != length ||
                dataStartOffset != AlignUp(sectionTableOffset + sectionTableBytes, H8DataLayoutConstants.SectionAlignmentBytes) ||
                (dataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u ||
                ReadUInt32(bytes, directoryOffset + 32) != H8DataLayoutConstants.BlobFlagLittleEndian ||
                ReadUInt32(bytes, directoryOffset + 44) != 0u ||
                ReadUInt32(bytes, directoryOffset + 48) != 0u ||
                ReadUInt32(bytes, directoryOffset + 52) != 0u ||
                ReadUInt32(bytes, directoryOffset + 56) != 0u ||
                ReadUInt32(bytes, directoryOffset + 60) != 0u)
            {
                failureCode = FailureDirectory;
                return false;
            }

            if (sectionTableOffset < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes ||
                sectionTableOffset > (uint)length ||
                sectionTableBytes > (uint)length - sectionTableOffset)
            {
                failureCode = FailureDirectory;
                return false;
            }

            bool sawLocalization = false;
            ulong expectedSectionOffset = dataStartOffset;
            for (int i = 0; i < directorySectionCount; i++)
            {
                int entryOffset = (int)sectionTableOffset + (i * 16);
                uint sectionId = ReadUInt32(bytes, entryOffset);
                uint recordSize = ReadUInt32(bytes, entryOffset + 4);
                uint count = ReadUInt32(bytes, entryOffset + 8);
                uint offset = ReadUInt32(bytes, entryOffset + 12);
                H8DataSectionId expectedId = (H8DataSectionId)(i + 1);
                if (sectionId != (uint)expectedId)
                {
                    failureCode = FailureSectionOrder;
                    return false;
                }

                if (recordSize != H8DataLayoutAudit.GetExpectedRecordSize(expectedId))
                {
                    failureCode = FailureSectionRecordSize;
                    return false;
                }

                if (count == 0u)
                {
                    if (offset != 0u)
                    {
                        failureCode = FailureSectionOutOfRange;
                        return false;
                    }

                    continue;
                }

                if ((offset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
                {
                    failureCode = FailureSectionAlignment;
                    return false;
                }

                ulong sectionBytes = (ulong)recordSize * count;
                if (offset < dataStartOffset || (ulong)offset + sectionBytes > (ulong)length)
                {
                    failureCode = FailureSectionOutOfRange;
                    return false;
                }

                if ((ulong)offset != expectedSectionOffset)
                {
                    failureCode = FailureSectionOutOfRange;
                    return false;
                }

                expectedSectionOffset = AlignUp((ulong)offset + sectionBytes, H8DataLayoutConstants.SectionAlignmentBytes);
                if (expectedSectionOffset > (ulong)length + H8DataLayoutConstants.SectionAlignmentBytes)
                {
                    failureCode = FailureSectionOutOfRange;
                    return false;
                }

                if (expectedId == H8DataSectionId.LocalizationUtf8)
                {
                    sawLocalization = true;
                    if (localizationOffset != offset || localizationBytes != count)
                    {
                        failureCode = FailureLocalization;
                        return false;
                    }
                }
            }

            if (localizationBytes != 0u && !sawLocalization)
            {
                failureCode = FailureLocalization;
                return false;
            }

            return true;
        }

        private static ulong ComputeHash64(byte* bytes, int offset, int count)
        {
            Unity.Mathematics.uint2 hash = xxHash3.Hash64(bytes + offset, count);
            return ((ulong)hash.y << 32) | hash.x;
        }

        private static void RecomputeHeaderChecksum(byte* bytes, int length)
        {
            WriteUInt64(bytes, 8, ComputeHash64(bytes, H8DataLayoutConstants.HeaderSizeBytes, length - H8DataLayoutConstants.HeaderSizeBytes));
        }

        private static ushort ReadUInt16(byte* bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte* bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private static ulong ReadUInt64(byte* bytes, int offset)
        {
            return (ulong)bytes[offset] |
                   ((ulong)bytes[offset + 1] << 8) |
                   ((ulong)bytes[offset + 2] << 16) |
                   ((ulong)bytes[offset + 3] << 24) |
                   ((ulong)bytes[offset + 4] << 32) |
                   ((ulong)bytes[offset + 5] << 40) |
                   ((ulong)bytes[offset + 6] << 48) |
                   ((ulong)bytes[offset + 7] << 56);
        }

        private static void WriteUInt32(byte* bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(byte* bytes, int offset, ulong value)
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

        private static uint AlignUp(uint value, int alignment)
        {
            uint mask = (uint)alignment - 1u;
            return (value + mask) & ~mask;
        }

        private static ulong AlignUp(ulong value, int alignment)
        {
            ulong mask = (ulong)alignment - 1UL;
            return (value + mask) & ~mask;
        }

        private static int AlignUp(int value, int alignment)
        {
            int mask = alignment - 1;
            return (value + mask) & ~mask;
        }

        private static void WriteReport(
            string reportPath,
            bool passed,
            string setupError,
            int blobBytes,
            int publishCount,
            ulong publishedChecksum,
            long validationTicks,
            long validationAllocatedBytes,
            int validationPasses,
            CaseResult[] cases)
        {
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_MONOLITH_FAIL_CLOSED_RUNTIME_SIM_V1\",");
            report.AppendLine("  \"agent\": \"X_002\",");
            report.Append("  \"generated\": \"").Append(DateTime.UtcNow.ToString("O")).AppendLine("\",");
            report.Append("  \"status\": \"").Append(passed ? "PASS_FAIL_CLOSED_NO_POISON_PUBLISH" : "FAIL").AppendLine("\",");
            report.Append("  \"setupError\": \"").Append(Escape(setupError)).AppendLine("\",");
            report.AppendLine("  \"proofBoundary\": \"CLI resident-pointer simulation of the H8StaticDataArena publish gate. It proves corrupt candidates fail before publish and validation allocates zero bytes; Unity player profiler proof is still separate.\",");
            report.Append("  \"blobBytes\": ").Append(blobBytes).AppendLine(",");
            report.Append("  \"baselinePublishCount\": ").Append(publishCount).AppendLine(",");
            report.Append("  \"publishedChecksumHex\": \"0x").Append(publishedChecksum.ToString("X16")).AppendLine("\",");
            report.Append("  \"validationIterations\": ").Append(ValidationIterations).AppendLine(",");
            report.Append("  \"validationPasses\": ").Append(validationPasses).AppendLine(",");
            report.Append("  \"validationMeanMicroseconds\": ").Append((TicksToMicroseconds(validationTicks) / Math.Max(1, ValidationIterations)).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"validationAllocatedBytes\": ").Append(validationAllocatedBytes).AppendLine(",");
            report.AppendLine("  \"failureCodeLegend\": {");
            report.AppendLine("    \"0\": \"None\",");
            report.AppendLine("    \"1\": \"TooSmall\",");
            report.AppendLine("    \"2\": \"Header\",");
            report.AppendLine("    \"3\": \"BadChecksum\",");
            report.AppendLine("    \"4\": \"Directory\",");
            report.AppendLine("    \"5\": \"SectionOrder\",");
            report.AppendLine("    \"6\": \"SectionRecordSize\",");
            report.AppendLine("    \"7\": \"SectionAlignment\",");
            report.AppendLine("    \"8\": \"SectionOutOfRange\",");
            report.AppendLine("    \"9\": \"LocalizationDirectory\"");
            report.AppendLine("  },");
            report.AppendLine("  \"cases\": [");
            for (int i = 0; i < cases.Length; i++)
            {
                CaseResult result = cases[i];
                report.Append("    { \"case\": \"").Append(result.Name)
                    .Append("\", \"passed\": ").Append(result.Passed ? "true" : "false")
                    .Append(", \"candidateValid\": ").Append(result.CandidateValid ? "true" : "false")
                    .Append(", \"failureCode\": ").Append(result.FailureCode)
                    .Append(", \"expectedFailureCode\": ").Append(result.ExpectedFailureCode)
                    .Append(", \"finalPublishCount\": ").Append(result.FinalPublishCount)
                    .Append(", \"finalChecksumHex\": \"0x").Append(result.FinalChecksum.ToString("X16"))
                    .Append("\" }");
                if (i + 1 < cases.Length)
                    report.AppendLine(",");
                else
                    report.AppendLine();
            }

            report.AppendLine("  ]");
            report.AppendLine("}");
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        }

        private static double TicksToMicroseconds(long ticks)
        {
            return ticks * 1000000.0 / Stopwatch.Frequency;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private readonly struct CaseResult
        {
            public readonly string Name;
            public readonly bool Passed;
            public readonly bool CandidateValid;
            public readonly int FailureCode;
            public readonly int ExpectedFailureCode;
            public readonly int FinalPublishCount;
            public readonly ulong FinalChecksum;

            public CaseResult(
                string name,
                bool passed,
                bool candidateValid,
                int failureCode,
                int expectedFailureCode,
                int finalPublishCount,
                ulong finalChecksum)
            {
                Name = name;
                Passed = passed;
                CandidateValid = candidateValid;
                FailureCode = failureCode;
                ExpectedFailureCode = expectedFailureCode;
                FinalPublishCount = finalPublishCount;
                FinalChecksum = finalChecksum;
            }
        }

        private unsafe delegate void CandidateMutator(byte* bytes, int length);
    }
}
