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
    internal static unsafe class DataMonolithLoadStressProbe
    {
        private const string ReportPath = "Docs/Reports/DATA_MONOLITH_LOAD_STRESS_X_002.json";
        private const int ValidationIterations = 1024;
        private const int AlignmentBytes = 64;
        private const double TargetLoadMicroseconds = 1000.0;
        private const uint GenericRead = 0x80000000u;
        private const uint FileShareRead = 0x00000001u;
        private const uint OpenExisting = 3u;
        private const uint FileFlagSequentialScan = 0x08000000u;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public static bool Run(string projectRoot)
        {
            string blobPath = Path.Combine(projectRoot, H8DataMonolithCompiler.OutputAssetPath);
            string reportPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

            if (!File.Exists(blobPath))
            {
                WriteReport(
                    reportPath,
                    passed: false,
                    setupError: "static_data.h8bin missing",
                    blobBytes: 0,
                    fileReadTicks: 0,
                    fileReadAllocatedBytes: 0,
                    residentCopyTicks: 0,
                    residentCopyAllocatedBytes: 0,
                    nativeReadTicks: 0,
                    nativeReadAllocatedBytes: 0,
                    validationTicks: 0,
                    validationAllocatedBytes: 0,
                    checksumFailureCode: 0,
                    offsetFailureCode: 0,
                    badChecksumRejected: false,
                    badOffsetRejected: false,
                    nativeReadSupported: false,
                    nativeReadOk: false,
                    nativeReadValid: false);
                return false;
            }

            long fileReadAllocBefore = GC.GetAllocatedBytesForCurrentThread();
            long fileReadStart = Stopwatch.GetTimestamp();
            byte[] blob = File.ReadAllBytes(blobPath);
            long fileReadTicks = Stopwatch.GetTimestamp() - fileReadStart;
            long fileReadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - fileReadAllocBefore;

            nuint allocBytes = (nuint)AlignUp(blob.Length, AlignmentBytes);
            void* nativeReadResident = NativeMemory.AlignedAlloc(allocBytes, AlignmentBytes);
            void* resident = NativeMemory.AlignedAlloc(allocBytes, AlignmentBytes);
            if (nativeReadResident == null || resident == null)
            {
                if (nativeReadResident != null)
                    NativeMemory.AlignedFree(nativeReadResident);
                if (resident != null)
                    NativeMemory.AlignedFree(resident);
                WriteReport(
                    reportPath,
                    passed: false,
                    setupError: "NativeMemory.AlignedAlloc failed",
                    blobBytes: blob.Length,
                    fileReadTicks: fileReadTicks,
                    fileReadAllocatedBytes: fileReadAllocatedBytes,
                    residentCopyTicks: 0,
                    residentCopyAllocatedBytes: 0,
                    nativeReadTicks: 0,
                    nativeReadAllocatedBytes: 0,
                    validationTicks: 0,
                    validationAllocatedBytes: 0,
                    checksumFailureCode: 0,
                    offsetFailureCode: 0,
                    badChecksumRejected: false,
                    badOffsetRejected: false,
                    nativeReadSupported: false,
                    nativeReadOk: false,
                    nativeReadValid: false);
                return false;
            }

            try
            {
                bool nativeReadSupported = OperatingSystem.IsWindows();
                bool nativeReadOk = false;
                bool nativeReadValid = false;
                long nativeReadTicks = 0L;
                long nativeReadAllocatedBytes = 0L;
                if (nativeReadSupported && TryReadViaNativeFile(blobPath, (byte*)nativeReadResident, blob.Length))
                {
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                    long nativeReadAllocBefore = GC.GetAllocatedBytesForCurrentThread();
                    long nativeReadStart = Stopwatch.GetTimestamp();
                    nativeReadOk = TryReadViaNativeFile(blobPath, (byte*)nativeReadResident, blob.Length);
                    nativeReadTicks = Stopwatch.GetTimestamp() - nativeReadStart;
                    nativeReadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - nativeReadAllocBefore;
                    nativeReadValid = nativeReadOk && ValidateResidentBlob((byte*)nativeReadResident, blob.Length, out int nativeFailureCode) && nativeFailureCode == FailureNone;
                }

                long residentCopyAllocBefore = GC.GetAllocatedBytesForCurrentThread();
                long residentCopyStart = Stopwatch.GetTimestamp();
                fixed (byte* source = blob)
                {
                    Buffer.MemoryCopy(source, resident, (long)allocBytes, blob.Length);
                }

                long residentCopyTicks = Stopwatch.GetTimestamp() - residentCopyStart;
                long residentCopyAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - residentCopyAllocBefore;

                bool residentValid = ValidateResidentBlob((byte*)resident, blob.Length, out int validFailureCode);

                byte* corruptChecksum = (byte*)resident;
                ulong originalChecksum = ReadUInt64(corruptChecksum, 8);
                WriteUInt64(corruptChecksum, 8, 0UL);
                bool badChecksumRejected = !ValidateResidentBlob(corruptChecksum, blob.Length, out int checksumFailureCode) &&
                                           checksumFailureCode == FailureBadChecksum;
                WriteUInt64(corruptChecksum, 8, originalChecksum);

                uint firstSectionOffset = ReadUInt32((byte*)resident, 128 + 12);
                WriteUInt32((byte*)resident, 128 + 12, (uint)(blob.Length - AlignmentBytes));
                RecomputeHeaderChecksum((byte*)resident, blob.Length);
                bool badOffsetRejected = !ValidateResidentBlob((byte*)resident, blob.Length, out int offsetFailureCode) &&
                                         offsetFailureCode == FailureSectionOutOfRange;
                WriteUInt32((byte*)resident, 128 + 12, firstSectionOffset);
                RecomputeHeaderChecksum((byte*)resident, blob.Length);

                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                long validationAllocBefore = GC.GetAllocatedBytesForCurrentThread();
                long validationStart = Stopwatch.GetTimestamp();
                int validCount = 0;
                int failureAccumulator = 0;
                for (int i = 0; i < ValidationIterations; i++)
                {
                    if (ValidateResidentBlob((byte*)resident, blob.Length, out int failureCode))
                        validCount++;
                    failureAccumulator ^= failureCode;
                }

                long validationTicks = Stopwatch.GetTimestamp() - validationStart;
                long validationAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - validationAllocBefore;

                bool passed = residentValid &&
                              validFailureCode == FailureNone &&
                              badChecksumRejected &&
                              badOffsetRejected &&
                              validCount == ValidationIterations &&
                              failureAccumulator == 0 &&
                              validationAllocatedBytes == 0 &&
                              (!nativeReadSupported || (nativeReadOk && nativeReadValid && nativeReadAllocatedBytes == 0));

                WriteReport(
                    reportPath,
                    passed,
                    string.Empty,
                    blob.Length,
                    fileReadTicks,
                    fileReadAllocatedBytes,
                    residentCopyTicks,
                    residentCopyAllocatedBytes,
                    nativeReadTicks,
                    nativeReadAllocatedBytes,
                    validationTicks,
                    validationAllocatedBytes,
                    checksumFailureCode,
                    offsetFailureCode,
                    badChecksumRejected,
                    badOffsetRejected,
                    nativeReadSupported,
                    nativeReadOk,
                    nativeReadValid);
                return passed;
            }
            finally
            {
                NativeMemory.AlignedFree(nativeReadResident);
                NativeMemory.AlignedFree(resident);
            }
        }

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
                (ReadUInt32(bytes, 36) & H8DataLayoutConstants.BlobFlagLittleEndian) == 0u ||
                ReadUInt32(bytes, 48) != H8DataLayoutConstants.SchemaHash)
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
                sectionTableBytes != directorySectionCount * 16u ||
                directoryBlobBytes != length ||
                dataStartOffset != AlignUp(sectionTableOffset + sectionTableBytes, H8DataLayoutConstants.SectionAlignmentBytes) ||
                (dataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
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
            long fileReadTicks,
            long fileReadAllocatedBytes,
            long residentCopyTicks,
            long residentCopyAllocatedBytes,
            long nativeReadTicks,
            long nativeReadAllocatedBytes,
            long validationTicks,
            long validationAllocatedBytes,
            int checksumFailureCode,
            int offsetFailureCode,
            bool badChecksumRejected,
            bool badOffsetRejected,
            bool nativeReadSupported,
            bool nativeReadOk,
            bool nativeReadValid)
        {
            double fileReadMicroseconds = TicksToMicroseconds(fileReadTicks);
            double residentCopyMicroseconds = TicksToMicroseconds(residentCopyTicks);
            double nativeReadMicroseconds = TicksToMicroseconds(nativeReadTicks);
            double validationTotalMicroseconds = TicksToMicroseconds(validationTicks);
            double validationMeanMicroseconds = validationTicks == 0L ? 0.0 : validationTotalMicroseconds / ValidationIterations;
            double residentPointerLoadEstimateMicroseconds = residentCopyMicroseconds + validationMeanMicroseconds;
            double nativeResidentLoadEstimateMicroseconds = nativeReadMicroseconds + validationMeanMicroseconds;
            bool targetLoadMet = nativeReadSupported && nativeReadOk && nativeReadValid && nativeResidentLoadEstimateMicroseconds < TargetLoadMicroseconds && nativeReadAllocatedBytes + validationAllocatedBytes == 0L;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_MONOLITH_LOAD_STRESS_V1\",");
            report.AppendLine("  \"agent\": \"X_002\",");
            report.Append("  \"generated\": \"").Append(DateTime.UtcNow.ToString("O")).AppendLine("\",");
            report.Append("  \"status\": \"").Append(passed ? (targetLoadMet ? "PASS_NATIVE_READ_ZERO_GC_TARGET_TIME" : "PASS_ZERO_GC_TARGET_TIME_MISSED") : "FAIL").AppendLine("\",");
            report.Append("  \"setupError\": \"").Append(Escape(setupError)).AppendLine("\",");
            report.AppendLine("  \"proofBoundary\": \"Native file read and resident pointer validation are measured in CLI. Runtime source now attempts native read before MMF/FileStream on Windows, but real Unity player GlobalDataVault profiler proof remains required.\",");
            report.Append("  \"blobBytes\": ").Append(blobBytes).AppendLine(",");
            report.Append("  \"validationIterations\": ").Append(ValidationIterations).AppendLine(",");
            report.Append("  \"targetLoadMicroseconds\": ").Append(TargetLoadMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"targetLoadMet\": ").Append(targetLoadMet ? "true" : "false").AppendLine(",");
            report.Append("  \"fileReadMicroseconds\": ").Append(fileReadMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"fileReadAllocatedBytes\": ").Append(fileReadAllocatedBytes).AppendLine(",");
            report.Append("  \"residentCopyMicroseconds\": ").Append(residentCopyMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"residentCopyAllocatedBytes\": ").Append(residentCopyAllocatedBytes).AppendLine(",");
            report.Append("  \"nativeReadSupported\": ").Append(nativeReadSupported ? "true" : "false").AppendLine(",");
            report.Append("  \"nativeReadOk\": ").Append(nativeReadOk ? "true" : "false").AppendLine(",");
            report.Append("  \"nativeReadValid\": ").Append(nativeReadValid ? "true" : "false").AppendLine(",");
            report.Append("  \"nativeReadMicroseconds\": ").Append(nativeReadMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"nativeReadAllocatedBytes\": ").Append(nativeReadAllocatedBytes).AppendLine(",");
            report.Append("  \"residentValidationTotalMicroseconds\": ").Append(validationTotalMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"residentValidationMeanMicroseconds\": ").Append(validationMeanMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"residentValidationAllocatedBytes\": ").Append(validationAllocatedBytes).AppendLine(",");
            report.Append("  \"residentPointerLoadEstimateMicroseconds\": ").Append(residentPointerLoadEstimateMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"residentPointerLoadEstimateAllocatedBytes\": ").Append(residentCopyAllocatedBytes + validationAllocatedBytes).AppendLine(",");
            report.Append("  \"nativeResidentLoadEstimateMicroseconds\": ").Append(nativeResidentLoadEstimateMicroseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
            report.Append("  \"nativeResidentLoadEstimateAllocatedBytes\": ").Append(nativeReadAllocatedBytes + validationAllocatedBytes).AppendLine(",");
            report.Append("  \"badChecksumRejected\": ").Append(badChecksumRejected ? "true" : "false").AppendLine(",");
            report.Append("  \"badOffsetRejected\": ").Append(badOffsetRejected ? "true" : "false").AppendLine(",");
            report.Append("  \"checksumFailureCode\": ").Append(checksumFailureCode).AppendLine(",");
            report.Append("  \"offsetFailureCode\": ").Append(offsetFailureCode).AppendLine(",");
            report.AppendLine("  \"failureCodeLegend\": {");
            report.AppendLine("    \"0\": \"None\",");
            report.AppendLine("    \"3\": \"BadChecksum\",");
            report.AppendLine("    \"8\": \"SectionOutOfRange\"");
            report.AppendLine("  }");
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

        private static bool TryReadViaNativeFile(string absolutePath, byte* destination, int expectedBytes)
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(absolutePath) || destination == null || expectedBytes <= 0)
                return false;

            IntPtr handle = CreateFileW(
                absolutePath,
                GenericRead,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagSequentialScan,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == InvalidHandleValue)
                return false;

            try
            {
                int totalRead = 0;
                while (totalRead < expectedBytes)
                {
                    uint chunkBytes = (uint)Math.Min(1024 * 1024, expectedBytes - totalRead);
                    if (!ReadFile(handle, destination + totalRead, chunkBytes, out uint read, IntPtr.Zero) || read == 0u)
                        return false;

                    totalRead += (int)read;
                }

                return totalRead == expectedBytes;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
