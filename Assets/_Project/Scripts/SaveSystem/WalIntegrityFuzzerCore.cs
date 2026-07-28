#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WalFuzzerProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public uint PayloadBytes;
        [FieldOffset(8)] public uint LoopPayloadBytes;
        [FieldOffset(12)] public uint LoopIterations;
        [FieldOffset(16)] public uint KillPercent;
        [FieldOffset(20)] public uint SectorCount;
        [FieldOffset(24)] public uint ChunkBytes;
        [FieldOffset(28)] public uint StallThresholdMicros;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint WriteReports;
        [FieldOffset(40)] public uint EnforceZeroGcLoop;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct WalFuzzerResultDTO
    {
        [FieldOffset(0)] public uint ErrorFlags;
        [FieldOffset(4)] public uint ErrorCode;
        [FieldOffset(8)] public uint PhaseHash;
        [FieldOffset(12)] public uint RecoveredBytes;
        [FieldOffset(16)] public ulong TruthHash;
        [FieldOffset(24)] public ulong RecoveredHash;
        [FieldOffset(32)] public long CorruptionOffset;
        [FieldOffset(40)] public long PrimaryBytes;
        [FieldOffset(48)] public long BackupBytes;
        [FieldOffset(56)] public long WriteMicros;
        [FieldOffset(64)] public long ReadMicros;
        [FieldOffset(72)] public long WorkerYieldMicros;
        [FieldOffset(80)] public long ManagedAllocBytes;
        [FieldOffset(88)] public long PagingBytesRead;
        [FieldOffset(96)] public long FailedSectorHash;
        [FieldOffset(104)] public uint LoopIterations;
        [FieldOffset(108)] public uint SectorCount;
        [FieldOffset(112)] public uint FirstMismatchOffset;
        [FieldOffset(116)] public uint CsvFailureRows;
        [FieldOffset(120)] public uint MerkleReplayBytes;
        [FieldOffset(124)] public uint MerkleBlockCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WalFuzzerTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PhaseHash;
        [FieldOffset(8)] public long SectorHash;
        [FieldOffset(16)] public ulong PayloadHash;
        [FieldOffset(24)] public long FileOffset;
        [FieldOffset(32)] public uint Bytes;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint ErrorCode;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct WalSectorIndexEntryDTO
    {
        [FieldOffset(0)] public long SectorHash;
        [FieldOffset(8)] public long ByteOffset;
        [FieldOffset(16)] public uint ByteCount;
        [FieldOffset(20)] public uint PayloadHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WalFuzzerDumpHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong TruthHash;
        [FieldOffset(16)] public ulong RecoveredHash;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public uint HeaderBytes;
        [FieldOffset(32)] public uint EntryBytes;
        [FieldOffset(36)] public uint EntryCount;
        [FieldOffset(40)] public uint ErrorFlags;
        [FieldOffset(44)] public uint ErrorCode;
        [FieldOffset(48)] public uint ResultBytes;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    internal static unsafe partial class WalIntegrityFuzzerCore
    {
        internal const uint DataCorruptionFailure = 1u << 0;
        internal const uint AsyncStallFailure = 1u << 1;
        internal const uint MemoryBloatFailure = 1u << 2;
        internal const uint BackupRecoveryFailure = 1u << 3;
        internal const uint PrimaryAcceptedFailure = 1u << 4;
        internal const uint ManagedAllocationFailure = 1u << 5;
        internal const uint CsvProfileFailure = 1u << 6;
        internal const uint PromotionFailure = 1u << 7;
        internal const uint MerkleWalRecoveryFailure = 1u << 8;

        private const int PayloadBytes10Mb = 10 * 1024 * 1024;
        private const int MaxPayloadBytes = 256 * 1024 * 1024;
        private const int MaxLoopPayloadBytes = 4 * 1024 * 1024;
        private const int MaxLoopIterations = 100000;
        private const int MaxSectorCount = 50000;
        private const int HeaderBytes = 32;
        private const int TelemetryCapacity = 300;
        private const int MerkleCounterCapacity = 16;
        private const int MerkleCounterBytes = 1;
        private const int MerkleCounterStoredBytes = 8;
        private const int MerkleCounterBlockCount = 9;
        private const int MerkleCounterRawBytes = 10;
        private const int MerkleCounterFailure = 11;
        private const double SectorMeters = 100.0;
        private const double AupStressExtentMeters = 49900.0;
        private const uint DefaultSeed = 0x9E3779B9u;
        private const uint FuzzerNameHash = 0x53483235u;
        private const uint PhaseLocalWal = 1921734283u;
        private const uint PhaseMerkleWal = 2862439088u;
        private const uint PhaseSectorSeek = 4091787352u;
        private const uint PhaseLoopFuzzer = 288152410u;
        private const uint PhasePayloadValidate = 4054470592u;
        private const uint PhaseWalFailure = 3853777414u;
        private const ulong DumpMagic = 0x3635325F4C415748UL;
        private const string PrimaryFileName = "slot_shinobu_256.h8log";
        private const string BackupFileName = "slot_shinobu_256.h8log.bak";
        private const string TempFileName = "slot_shinobu_256.h8log.tmp";
        private const string PartialFileSuffix = ".partial";
        private const int PartialWalCopyJoinMilliseconds = 5000;
        private const int PartialWalCopyCancelJoinMilliseconds = 100;
        private const string SectorFileName = "slot_shinobu_256_sector_pages.h8log";
        private const string LoopFileName = "slot_shinobu_256_loop.h8log";
        private const string MerkleWalFileName = "slot_shinobu_256_merkle.wal";
        private const string MerkleWalBackupFileName = "slot_shinobu_256_merkle.wal.bak";
        private const string FailureCsvRelativePath = "Docs/Reports/HEADLESS_WAL_FAILURES.csv";
        private const string QaReportRelativePath = "Docs/Reports/QA_OPTIMIZATION_REPORT.json";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_256.bin";
        private const string ProfileCsvRelativePath = "Assets/_Project/Scripts/SaveSystem/Editor/io_fuzzer_profiles.csv";
        private const string NativeMemoryOwner = nameof(WalIntegrityFuzzerCore);
        private const string ProfilesScratchLabel = "profiles";
        private const string ProfileCsvBytesScratchLabel = "profileCsvBytes";
        private const string PayloadScratchLabel = "payload";
        private const string RecoveredScratchLabel = "recovered";
        private const string JobResultScratchLabel = "jobResult";
        private const string TelemetryScratchLabel = "telemetry";
        private const string MerkleCurrentTreeScratchLabel = "merkleCurrentTree";
        private const string MerklePreviousTreeScratchLabel = "merklePreviousTree";
        private const string MerkleLeafDescriptorsScratchLabel = "merkleLeafDescriptors";
        private const string MerkleDeltaRecordsScratchLabel = "merkleDeltaRecords";
        private const string MerkleDeltaBytesScratchLabel = "merkleDeltaBytes";
        private const string MerklePrunedDeltaBytesScratchLabel = "merklePrunedDeltaBytes";
        private const string MerkleCompressedBytesScratchLabel = "merkleCompressedBytes";
        private const string MerkleLz4BlockHeadersScratchLabel = "merkleLz4BlockHeaders";
        private const string MerkleTelemetryRingScratchLabel = "merkleTelemetryRing";
        private const string MerkleCountersScratchLabel = "merkleCounters";
        private const string MerkleLz4HashTableScratchLabel = "merkleLz4HashTable";
        private const string MerkleReplayedDeltaBytesScratchLabel = "merkleReplayedDeltaBytes";
        private const string MerkleReplayCountersScratchLabel = "merkleReplayCounters";
        private const string LoopPayloadScratchLabel = "loopPayload";
        private const string LoopReadbackScratchLabel = "loopReadback";

        internal static WalFuzzerProfileDTO BuildDefaultProfile()
        {
            return new WalFuzzerProfileDTO
            {
                NameHash = FuzzerNameHash,
                PayloadBytes = PayloadBytes10Mb,
                LoopPayloadBytes = 1024u,
                LoopIterations = 1000u,
                KillPercent = 50u,
                SectorCount = 5000u,
                ChunkBytes = 64u * 1024u,
                StallThresholdMicros = 2000u,
                GlobalQualityWeight = 1f,
                WriteReports = 1u,
                EnforceZeroGcLoop = 1u
            };
        }

        internal static bool RunDefaultEditorFuzzer(out WalFuzzerResultDTO result)
        {
            WalFuzzerProfileDTO profile = BuildDefaultProfile();
            NativeArray<WalFuzzerProfileDTO> profiles = AllocateTrackedTempArray<WalFuzzerProfileDTO>(4, ProfilesScratchLabel, NativeArrayOptions.UninitializedMemory);
            try
            {
                string profilePath = ResolveProjectPath(ProfileCsvRelativePath);
                if (TryLoadProfilesCsv(profilePath, profiles, out int count, out _) && count > 0)
                    profile = profiles[0];
            }
            finally
            {
                DisposeTrackedTempArray(ref profiles);
            }

            string root = Path.Combine(Application.temporaryCachePath, "H8_SHINOBU_256_WAL");
            return RunProfile(root, in profile, out result);
        }

        internal static bool RunProfile(string rootDirectory, in WalFuzzerProfileDTO profile, out WalFuzzerResultDTO result)
        {
            result = default;
            if (string.IsNullOrEmpty(rootDirectory))
            {
                MarkFailure(ref result, BackupRecoveryFailure, 1u);
                return false;
            }

            Directory.CreateDirectory(rootDirectory);
            DeleteIfExists(Path.Combine(rootDirectory, PrimaryFileName));
            DeleteIfExists(Path.Combine(rootDirectory, BackupFileName));
            DeleteIfExists(Path.Combine(rootDirectory, TempFileName));
            DeleteIfExists(Path.Combine(rootDirectory, SectorFileName));
            DeleteIfExists(Path.Combine(rootDirectory, LoopFileName));
            DeleteIfExists(Path.Combine(rootDirectory, MerkleWalFileName));
            DeleteIfExists(Path.Combine(rootDirectory, MerkleWalBackupFileName));

            int payloadBytes = ClampProfileUIntToInt(profile.PayloadBytes, 1024, MaxPayloadBytes);
            NativeArray<byte> payload = default;
            NativeArray<byte> recovered = default;
            NativeArray<WalFuzzerResultDTO> jobResult = default;
            NativeArray<WalFuzzerTelemetryEntry> telemetry = default;

            try
            {
                payload = AllocateTrackedTempJobArray<byte>(payloadBytes, PayloadScratchLabel, NativeArrayOptions.UninitializedMemory);
                recovered = AllocateTrackedTempJobArray<byte>(payloadBytes, RecoveredScratchLabel, NativeArrayOptions.UninitializedMemory);
                jobResult = AllocateTrackedTempJobArray<WalFuzzerResultDTO>(1, JobResultScratchLabel, NativeArrayOptions.ClearMemory);
                telemetry = AllocateTrackedTempJobArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, TelemetryScratchLabel, NativeArrayOptions.ClearMemory);

                GenerateSyntheticSaveDataJob generateJob = new GenerateSyntheticSaveDataJob
                {
                    Payload = payload,
                    Seed = DefaultSeed
                };
                CompleteColdValidationBarrier(generateJob.Schedule(payload.Length, 4096));

                byte* payloadData = (byte*)payload.GetUnsafeReadOnlyPtr();
                ulong truthHash = SaveBinaryStorage.Hash64(payloadData, payload.Length);
                EntityDeltaHeaderDTO header = new EntityDeltaHeaderDTO
                {
                    SectorHash = unchecked((ulong)BuildExtremeSectorHash(0)),
                    CompressedSize = (uint)payload.Length,
                    UncompressedSize = (uint)payload.Length,
                    XXHash3Checksum = truthHash,
                    _pad0 = 0u,
                    _pad1 = 0u
                };

                result.TruthHash = truthHash;
                string primaryPath = Path.Combine(rootDirectory, PrimaryFileName);
                string backupPath = Path.Combine(rootDirectory, BackupFileName);

                if (!TryWriteWalFile(backupPath, in header, payload, out long backupWriteMicros))
                {
                    MarkFailure(ref result, BackupRecoveryFailure, 2u);
                    return CompleteRun(rootDirectory, in profile, ref result, telemetry);
                }

                result.WriteMicros = backupWriteMicros;
                result.BackupBytes = new FileInfo(backupPath).Length;
                RecordTelemetry(telemetry, 0u, HashAscii("backup_write"), (long)header.SectorHash, truthHash, 0L, (uint)payload.Length, 0u, 0u);

                if (!TryCopyPartialWal(backupPath, primaryPath, in profile, out long primaryBytes, out long workerYieldMicros))
                {
                    MarkFailure(ref result, BackupRecoveryFailure, 3u, primaryBytes);
                }

                result.PrimaryBytes = primaryBytes;
                result.WorkerYieldMicros = workerYieldMicros;
                if (workerYieldMicros > profile.StallThresholdMicros)
                {
                    MarkFailure(ref result, AsyncStallFailure, 16u, primaryBytes);
                }

                bool primaryAccepted = TryReadWalFile(primaryPath, recovered, out _, out _, out _, out _);
                if (primaryAccepted)
                {
                    MarkFailure(ref result, PrimaryAcceptedFailure, 15u, primaryBytes);
                }

                bool backupLoaded = TryReadWalFile(backupPath, recovered, out EntityDeltaHeaderDTO recoveredHeader, out ulong recoveredHash, out long readMicros, out long recoveredBytes);
                result.ReadMicros = readMicros;
                result.RecoveredBytes = (uint)math.min(uint.MaxValue, recoveredBytes);
                result.RecoveredHash = recoveredHash;
                if (!backupLoaded)
                {
                    MarkFailure(ref result, BackupRecoveryFailure, 4u);
                    return CompleteRun(rootDirectory, in profile, ref result, telemetry);
                }

                jobResult[0] = result;
                ValidateRecoveredPayloadJob validateJob = new ValidateRecoveredPayloadJob
                {
                    Payload = recovered,
                    ResultPtr = jobResult.GetUnsafePtr(),
                    Seed = DefaultSeed,
                    ExpectedHash = truthHash,
                    ByteCount = (int)recoveredHeader.UncompressedSize
                };
                CompleteColdValidationBarrier(validateJob.Schedule());
                result = jobResult[0];

                RunProductionMerkleWalRecovery(rootDirectory, in profile, payload, ref result, telemetry);

                if (!TryPromoteBackup(backupPath, primaryPath, Path.Combine(rootDirectory, TempFileName)))
                {
                    MarkFailure(ref result, PromotionFailure, 5u, result.BackupBytes);
                }

                RunSectorPagingStress(rootDirectory, in profile, ref result, telemetry);
                RunContinuousWriteFuzzer(rootDirectory, in profile, ref result, telemetry);
                return CompleteRun(rootDirectory, in profile, ref result, telemetry);
            }
            finally
            {
                DisposeTrackedTempJobArray(ref telemetry);
                DisposeTrackedTempJobArray(ref jobResult);
                DisposeTrackedTempJobArray(ref recovered);
                DisposeTrackedTempJobArray(ref payload);
            }
        }

        internal static bool TryLoadProfilesCsv(string path, NativeArray<WalFuzzerProfileDTO> profiles, out int count, out uint errorCode)
        {
            count = 0;
            errorCode = 0u;
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !profiles.IsCreated || profiles.Length == 0)
            {
                errorCode = 1u;
                return false;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0L || info.Length > 64L * 1024L)
            {
                errorCode = 2u;
                return false;
            }

            NativeArray<byte> bytes = AllocateTrackedTempArray<byte>((int)info.Length, ProfileCsvBytesScratchLabel, NativeArrayOptions.UninitializedMemory);
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                {
                    byte* ptr = (byte*)bytes.GetUnsafePtr();
                    int total = 0;
                    while (total < bytes.Length)
                    {
                        int read = stream.Read(new Span<byte>(ptr + total, bytes.Length - total));
                        if (read <= 0)
                            break;
                        total += read;
                    }
                }

                return ParseProfiles(bytes, profiles, out count, out errorCode);
            }
            finally
            {
                DisposeTrackedTempArray(ref bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte ExpectedByte(int index, uint seed)
        {
            uint state = seed + (uint)index * 1664525u + 1013904223u;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;
            return (byte)(state ^ (state >> 8) ^ (state >> 16) ^ (state >> 24));
        }

        private static bool CompleteRun(string rootDirectory, in WalFuzzerProfileDTO profile, ref WalFuzzerResultDTO result, NativeArray<WalFuzzerTelemetryEntry> telemetry)
        {
            result.LoopIterations = (uint)ClampProfileUIntToInt(profile.LoopIterations, 1000, MaxLoopIterations);
            result.SectorCount = (uint)ClampProfileUIntToInt(profile.SectorCount, 5000, MaxSectorCount);
            NormalizeFailureDiagnostics(ref result);

            if (result.ErrorFlags != 0u)
            {
                result.CsvFailureRows = 1u;
                WriteFailureCsv(in result);
                DumpBlackBox(telemetry, in result);
            }
            else if (profile.WriteReports != 0u)
            {
                WriteQaReport(in result);
            }

            return result.ErrorFlags == 0u;
        }

        private static void NormalizeFailureDiagnostics(ref WalFuzzerResultDTO result)
        {
            if (result.ErrorCode == 0u)
                return;

            bool hadCapturedFirstFailure = result.PhaseHash != 0u;
            if (!hadCapturedFirstFailure)
                result.PhaseHash = ResolveFailurePhaseHash(result.ErrorCode);

            if (!hadCapturedFirstFailure)
                result.CorruptionOffset = ResolveFailureOffset(in result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MarkFailure(ref WalFuzzerResultDTO result, uint flags, uint errorCode)
        {
            result.ErrorFlags |= flags;
            if (result.ErrorCode != 0u)
                return;

            result.ErrorCode = errorCode;
            result.PhaseHash = ResolveFailurePhaseHash(errorCode);
            result.CorruptionOffset = ResolveFailureOffset(in result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MarkFailure(ref WalFuzzerResultDTO result, uint flags, uint errorCode, long corruptionOffset)
        {
            result.ErrorFlags |= flags;
            if (result.ErrorCode != 0u)
                return;

            result.ErrorCode = errorCode;
            result.PhaseHash = ResolveFailurePhaseHash(errorCode);
            result.CorruptionOffset = corruptionOffset;
        }

#if UNITY_EDITOR
        internal static WalFuzzerResultDTO BuildFirstFailureDiagnosticRegressionResult()
        {
            WalFuzzerResultDTO result = default;
            result.PrimaryBytes = 111L;
            MarkFailure(ref result, BackupRecoveryFailure, 3u, result.PrimaryBytes);

            result.PrimaryBytes = 999L;
            result.PagingBytesRead = 222L;
            MarkFailure(ref result, DataCorruptionFailure, 9u, result.PagingBytesRead);
            NormalizeFailureDiagnostics(ref result);
            return result;
        }
#endif

        private static uint ResolveFailurePhaseHash(uint errorCode)
        {
            if (errorCode <= 5u || errorCode == 15u || errorCode == 16u)
                return PhaseLocalWal;
            if (errorCode >= 30u && errorCode <= 40u)
                return PhaseMerkleWal;
            if (errorCode >= 6u && errorCode <= 10u)
                return PhaseSectorSeek;
            if (errorCode >= 11u && errorCode <= 14u)
                return PhaseLoopFuzzer;
            if (errorCode >= 20u && errorCode <= 22u)
                return PhasePayloadValidate;

            return PhaseWalFailure;
        }

        private static long ResolveFailureOffset(in WalFuzzerResultDTO result)
        {
            uint code = result.ErrorCode;
            if (code == 22u)
                return result.FirstMismatchOffset;
            if (code == 3u || code == 15u || code == 34u || code == 35u || code == 36u || code == 37u || code == 39u || code == 40u)
                return result.PrimaryBytes > 0L ? result.PrimaryBytes : -1L;
            if (code >= 6u && code <= 10u)
                return result.PagingBytesRead > 0L ? result.PagingBytesRead : -1L;
            if (code == 4u || code == 20u || code == 21u)
                return result.RecoveredBytes > 0u ? result.RecoveredBytes : -1L;

            return -1L;
        }

        private static bool TryWriteWalFile(string path, in EntityDeltaHeaderDTO header, NativeArray<byte> payload, out long elapsedMicros)
        {
            elapsedMicros = 0L;
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.SequentialScan);
                WriteWalToOpenStream(stream, in header, (byte*)payload.GetUnsafeReadOnlyPtr(), payload.Length);
                timer.Stop();
                elapsedMicros = TicksToMicros(timer.ElapsedTicks);
                return true;
            }
            catch
            {
                timer.Stop();
                elapsedMicros = TicksToMicros(timer.ElapsedTicks);
                return false;
            }
        }

        private static void WriteWalToOpenStream(FileStream stream, in EntityDeltaHeaderDTO header, byte* payloadData, int payloadBytes)
        {
            stream.Position = 0L;
            stream.SetLength(0L);
            Span<byte> headerBytes = stackalloc byte[HeaderBytes];
            WriteEntityDeltaHeaderLittleEndian(headerBytes, in header);

            stream.Write(headerBytes);
            stream.Write(new ReadOnlySpan<byte>(payloadData, payloadBytes));
            stream.Flush(true);
        }

        private static bool TryReadWalFile(string path, NativeArray<byte> destination, out EntityDeltaHeaderDTO header, out ulong payloadHash, out long elapsedMicros, out long recoveredBytes)
        {
            header = default;
            payloadHash = 0UL;
            elapsedMicros = 0L;
            recoveredBytes = 0L;
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                bool ok = TryReadWalFromOpenStream(stream, destination, out header, out payloadHash, out recoveredBytes);
                timer.Stop();
                elapsedMicros = TicksToMicros(timer.ElapsedTicks);
                return ok;
            }
            catch
            {
                timer.Stop();
                elapsedMicros = TicksToMicros(timer.ElapsedTicks);
                return false;
            }
        }

        private static bool TryReadWalFromOpenStream(FileStream stream, NativeArray<byte> destination, out EntityDeltaHeaderDTO header, out ulong payloadHash, out long recoveredBytes)
        {
            header = default;
            payloadHash = 0UL;
            recoveredBytes = 0L;
            if (stream.Length < HeaderBytes)
                return false;

            stream.Position = 0L;
            Span<byte> headerBytes = stackalloc byte[HeaderBytes];
            if (!ReadExact(stream, headerBytes))
                return false;

            fixed (byte* headerPtr = headerBytes)
            {
                header = ReadEntityDeltaHeaderLittleEndian(headerPtr);
            }

            if (header.UncompressedSize == 0u ||
                header.UncompressedSize > destination.Length ||
                header.CompressedSize != header.UncompressedSize ||
                stream.Length != HeaderBytes + header.UncompressedSize)
            {
                return false;
            }

            byte* dst = (byte*)destination.GetUnsafePtr();
            if (!ReadExact(stream, new Span<byte>(dst, (int)header.UncompressedSize)))
                return false;

            payloadHash = SaveBinaryStorage.Hash64(dst, header.UncompressedSize);
            recoveredBytes = header.UncompressedSize;
            return payloadHash == header.XXHash3Checksum;
        }

        private static bool ReadExact(FileStream stream, Span<byte> destination)
        {
            int total = 0;
            while (total < destination.Length)
            {
                int read = stream.Read(destination.Slice(total));
                if (read <= 0)
                    return false;
                total += read;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteEntityDeltaHeaderLittleEndian(Span<byte> destination, in EntityDeltaHeaderDTO header)
        {
            WriteULongLittleEndian(destination, 0, header.SectorHash);
            WriteUIntLittleEndian(destination, 8, header.CompressedSize);
            WriteUIntLittleEndian(destination, 12, header.UncompressedSize);
            WriteULongLittleEndian(destination, 16, header.XXHash3Checksum);
            WriteUIntLittleEndian(destination, 24, header._pad0);
            WriteUIntLittleEndian(destination, 28, header._pad1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static EntityDeltaHeaderDTO ReadEntityDeltaHeaderLittleEndian(byte* source)
        {
            return new EntityDeltaHeaderDTO
            {
                SectorHash = ReadULongLittleEndian(source, 0),
                CompressedSize = ReadUIntLittleEndian(source, 8),
                UncompressedSize = ReadUIntLittleEndian(source, 12),
                XXHash3Checksum = ReadULongLittleEndian(source, 16),
                _pad0 = ReadUIntLittleEndian(source, 24),
                _pad1 = ReadUIntLittleEndian(source, 28)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUIntLittleEndian(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteULongLittleEndian(Span<byte> destination, int offset, ulong value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            destination[offset + 4] = (byte)(value >> 32);
            destination[offset + 5] = (byte)(value >> 40);
            destination[offset + 6] = (byte)(value >> 48);
            destination[offset + 7] = (byte)(value >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUIntLittleEndian(byte* source, int offset)
        {
            return (uint)source[offset] |
                ((uint)source[offset + 1] << 8) |
                ((uint)source[offset + 2] << 16) |
                ((uint)source[offset + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadULongLittleEndian(byte* source, int offset)
        {
            return (ulong)source[offset] |
                ((ulong)source[offset + 1] << 8) |
                ((ulong)source[offset + 2] << 16) |
                ((ulong)source[offset + 3] << 24) |
                ((ulong)source[offset + 4] << 32) |
                ((ulong)source[offset + 5] << 40) |
                ((ulong)source[offset + 6] << 48) |
                ((ulong)source[offset + 7] << 56);
        }

        private static void WriteSectorIndexEntryLittleEndian(Span<byte> destination, in WalSectorIndexEntryDTO entry)
        {
            WriteULongLittleEndian(destination, 0, unchecked((ulong)entry.SectorHash));
            WriteULongLittleEndian(destination, 8, unchecked((ulong)entry.ByteOffset));
            WriteUIntLittleEndian(destination, 16, entry.ByteCount);
            WriteUIntLittleEndian(destination, 20, entry.PayloadHash);
            WriteUIntLittleEndian(destination, 24, entry.Flags);
            WriteUIntLittleEndian(destination, 28, 0u);
        }

        private static WalSectorIndexEntryDTO ReadSectorIndexEntryLittleEndian(byte* source)
        {
            return new WalSectorIndexEntryDTO
            {
                SectorHash = unchecked((long)ReadULongLittleEndian(source, 0)),
                ByteOffset = unchecked((long)ReadULongLittleEndian(source, 8)),
                ByteCount = ReadUIntLittleEndian(source, 16),
                PayloadHash = ReadUIntLittleEndian(source, 20),
                Flags = ReadUIntLittleEndian(source, 24),
                _pad0 = 0u
            };
        }

        private static bool TryPromoteBackup(string backupPath, string primaryPath, string tempPath)
        {
            try
            {
                DeleteIfExists(tempPath);
                File.Copy(backupPath, tempPath, true);
                if (File.Exists(primaryPath))
                    File.Replace(tempPath, primaryPath, null, true);
                else
                    File.Move(tempPath, primaryPath);
                DeleteIfExists(tempPath);
                return File.Exists(primaryPath) && !File.Exists(tempPath);
            }
            catch
            {
                DeleteIfExists(tempPath);
                return false;
            }
        }

        private static void RunProductionMerkleWalRecovery(string rootDirectory, in WalFuzzerProfileDTO profile, NativeArray<byte> sourcePayload, ref WalFuzzerResultDTO result, NativeArray<WalFuzzerTelemetryEntry> telemetry)
        {
            if (!sourcePayload.IsCreated || sourcePayload.Length <= 0)
            {
                MarkFailure(ref result, MerkleWalRecoveryFailure, 30u);
                return;
            }

            SaveMerkleRuntimeConfig config = SaveStateMerkleTree.ResolveRuntimeConfigForQuality(
                SaveStateMerkleTree.BuildDefaultConfig(),
                profile.GlobalQualityWeight,
                1f - math.saturate(profile.GlobalQualityWeight));

            int deltaCapacity = Align16(sourcePayload.Length + (SaveStateMerkleTree.LeafCount * UnsafeUtility.SizeOf<StateDeltaRecordDTO>()) + 1024);
            int compressedCapacity = SaveStateMerkleTree.ResolveRequiredCompressedCapacity(deltaCapacity, config.SubBlockBytes);
            int blockHeaderCapacity = SaveStateMerkleTree.ResolveRequiredSubBlockCount(deltaCapacity, config.SubBlockBytes);

            // One leaf per LeafCount slice of the payload. The sum of the leaf byte lengths is exactly
            // sourcePayload.Length and the record count never exceeds LeafCount, which is the layout
            // deltaCapacity above is sized for (payload + LeafCount record headers + slack).
            int leafByteStride = math.max(1, (sourcePayload.Length + SaveStateMerkleTree.LeafCount - 1) / SaveStateMerkleTree.LeafCount);

            SaveMerkleVaultBufferSet buffers = default;
            NativeArray<byte> replayedDeltaBytes = default;
            NativeArray<int> replayCounters = default;

            try
            {
                buffers.CurrentTree = AllocateTrackedTempJobArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, MerkleCurrentTreeScratchLabel, NativeArrayOptions.UninitializedMemory);
                // PreviousTree and LeafDescriptors must match the vault provisioning contract in
                // SaveStateMerkleTree.TryResolveVaultBuffers: EnsureCommittedBaselineJob decides whether to
                // rebuild the baseline from TreeNodes[RootIndex]._pad0, and MerkleLeafHashJob reads the
                // descriptors, so both are read before anything writes them and cannot start as garbage.
                buffers.PreviousTree = AllocateTrackedTempJobArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, MerklePreviousTreeScratchLabel, NativeArrayOptions.ClearMemory);
                buffers.LeafDescriptors = AllocateTrackedTempJobArray<StateLeafDescriptor>(SaveStateMerkleTree.LeafCount, MerkleLeafDescriptorsScratchLabel, NativeArrayOptions.ClearMemory);
                buffers.DeltaRecords = AllocateTrackedTempJobArray<StateDeltaRecordDTO>(SaveStateMerkleTree.LeafCount, MerkleDeltaRecordsScratchLabel, NativeArrayOptions.UninitializedMemory);
                buffers.DeltaBytes = AllocateTrackedTempJobArray<byte>(deltaCapacity, MerkleDeltaBytesScratchLabel, NativeArrayOptions.UninitializedMemory);
                buffers.PrunedDeltaBytes = AllocateTrackedTempJobArray<byte>(deltaCapacity, MerklePrunedDeltaBytesScratchLabel, NativeArrayOptions.UninitializedMemory);
                buffers.CompressedBytes = AllocateTrackedTempJobArray<byte>(compressedCapacity, MerkleCompressedBytesScratchLabel, NativeArrayOptions.UninitializedMemory);
                buffers.Lz4BlockHeaders = AllocateTrackedTempJobArray<Lz4SubBlockHeader>(blockHeaderCapacity, MerkleLz4BlockHeadersScratchLabel, NativeArrayOptions.UninitializedMemory);
                buffers.TelemetryRing = AllocateTrackedTempJobArray<SaveMerkleTelemetryEntry>(SaveStateMerkleTree.TelemetryRingFrames, MerkleTelemetryRingScratchLabel, NativeArrayOptions.ClearMemory);
                buffers.Counters = AllocateTrackedTempJobArray<int>(MerkleCounterCapacity, MerkleCountersScratchLabel, NativeArrayOptions.ClearMemory);
                buffers.Lz4HashTable = AllocateTrackedTempJobArray<int>(SaveStateMerkleTree.HashTableSlots, MerkleLz4HashTableScratchLabel, NativeArrayOptions.UninitializedMemory);
                replayedDeltaBytes = AllocateTrackedTempJobArray<byte>(deltaCapacity, MerkleReplayedDeltaBytesScratchLabel, NativeArrayOptions.UninitializedMemory);
                replayCounters = AllocateTrackedTempJobArray<int>(MerkleCounterCapacity, MerkleReplayCountersScratchLabel, NativeArrayOptions.ClearMemory);

                Stopwatch pipelineTimer = Stopwatch.StartNew();

                // MerkleLeafHashJob consumes LeafDescriptors read-only, so the caller owns the partition.
                // Without it every leaf hashes to default, the current root equals the cleared committed
                // baseline, MerkleChangedLeafExtractionJob reports zero changed bytes, and the WAL backup
                // is never written - leaving nothing to promote or hash-validate.
                JobHandle leafPartition = new SaveStateMerkleTree.MockInventoryLeafDescriptorJob
                {
                    Descriptors = buffers.LeafDescriptors,
                    SourceByteLength = sourcePayload.Length,
                    LeafByteStride = leafByteStride,
                    SectorKeyBase = 0u
                }.Schedule(SaveStateMerkleTree.LeafCount, 64);

                JobHandle pipeline = SaveStateMerkleTree.ScheduleVaultDeltaWalPipeline(
                    sourcePayload,
                    buffers,
                    config,
                    profile.GlobalQualityWeight,
                    1f - math.saturate(profile.GlobalQualityWeight),
                    leafPartition);
                CompleteColdValidationBarrier(pipeline);
                pipelineTimer.Stop();

                if (buffers.Counters[MerkleCounterFailure] != 0)
                {
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 31u);
                    return;
                }

                int rawBytes = buffers.Counters[MerkleCounterRawBytes];
                int storedBytes = buffers.Counters[MerkleCounterStoredBytes];
                int blockCount = buffers.Counters[MerkleCounterBlockCount];
                if (rawBytes <= 0 || rawBytes > buffers.PrunedDeltaBytes.Length || storedBytes <= 0 || storedBytes > buffers.CompressedBytes.Length || blockCount <= 0)
                {
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 32u);
                    return;
                }

                MerkleNodeDTO root = buffers.CurrentTree[SaveStateMerkleTree.RootIndex];
                SaveMerkleWalAppendHeader walHeader = SaveStateMerkleTree.BuildWalHeader(
                    root.HashLo,
                    root.HashHi,
                    256u,
                    0L,
                    rawBytes,
                    storedBytes,
                    (uint)blockCount,
                    0u);

                string primaryPath = Path.Combine(rootDirectory, MerkleWalFileName);
                string backupPath = Path.Combine(rootDirectory, MerkleWalBackupFileName);
                if (!SaveStateMerkleTree.TryAppendCompressedWalMmf(backupPath, buffers.CompressedBytes, storedBytes, walHeader, out _))
                {
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 33u);
                    return;
                }

                if (!TryCopyPartialWal(backupPath, primaryPath, in profile, out long primaryBytes, out long yieldMicros))
                {
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 34u, primaryBytes);
                    return;
                }

                result.PrimaryBytes = primaryBytes;
                result.WorkerYieldMicros = result.WorkerYieldMicros > yieldMicros ? result.WorkerYieldMicros : yieldMicros;
                if (yieldMicros > profile.StallThresholdMicros)
                {
                    MarkFailure(ref result, AsyncStallFailure, 16u, primaryBytes);
                }

                Stopwatch rollbackTimer = Stopwatch.StartNew();
                bool corruptedPrimaryAccepted = SaveStateMerkleTree.TryValidateWalAndRollback(primaryPath, backupPath, out _);
                if (corruptedPrimaryAccepted)
                {
                    rollbackTimer.Stop();
                    MarkFailure(ref result, PrimaryAcceptedFailure | MerkleWalRecoveryFailure, 35u, primaryBytes);
                    return;
                }

                if (!SaveStateMerkleTree.TryValidateWalAndRollback(primaryPath, backupPath, out _))
                {
                    rollbackTimer.Stop();
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 40u, primaryBytes);
                    return;
                }

                if (!SaveStateMerkleTree.TryReplayWalToDeltaArena(primaryPath, replayedDeltaBytes, buffers.CompressedBytes, replayCounters, out _))
                {
                    rollbackTimer.Stop();
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 36u, primaryBytes);
                    return;
                }

                if (replayCounters[MerkleCounterFailure] != 0 || replayCounters[MerkleCounterBytes] != rawBytes)
                {
                    rollbackTimer.Stop();
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 39u, primaryBytes);
                    return;
                }

                rollbackTimer.Stop();
                byte* truthPtr = (byte*)buffers.PrunedDeltaBytes.GetUnsafeReadOnlyPtr();
                byte* replayPtr = (byte*)replayedDeltaBytes.GetUnsafeReadOnlyPtr();
                ulong truthHash = SaveBinaryStorage.Hash64(truthPtr, rawBytes);
                ulong replayHash = SaveBinaryStorage.Hash64(replayPtr, rawBytes);
                result.TruthHash = truthHash;
                result.RecoveredHash = replayHash;
                result.RecoveredBytes = (uint)rawBytes;
                result.MerkleReplayBytes = (uint)rawBytes;
                result.MerkleBlockCount = (uint)blockCount;
                result.BackupBytes = new FileInfo(backupPath).Length;
                result.ReadMicros = TicksToMicros(rollbackTimer.ElapsedTicks);
                long pipelineMicros = TicksToMicros(pipelineTimer.ElapsedTicks);
                result.WriteMicros = result.WriteMicros > pipelineMicros ? result.WriteMicros : pipelineMicros;
                if (truthHash != replayHash)
                {
                    MarkFailure(ref result, DataCorruptionFailure | MerkleWalRecoveryFailure, 37u, primaryBytes);
                }

                RecordTelemetry(telemetry, 3u, HashAscii("merkle_wal"), (long)root.SectorKey, truthHash, rawBytes, (uint)storedBytes, result.ErrorFlags, result.ErrorCode);
            }
            catch
            {
                MarkFailure(ref result, MerkleWalRecoveryFailure, 38u);
            }
            finally
            {
                DisposeTrackedTempJobArray(ref replayCounters);
                DisposeTrackedTempJobArray(ref replayedDeltaBytes);
                DisposeMerkleBuffers(ref buffers);
            }
        }

        private static bool TryCopyPartialWal(string sourcePath, string destinationPath, in WalFuzzerProfileDTO profile, out long fileBytes, out long yieldMicros)
        {
            fileBytes = 0L;
            yieldMicros = 0L;
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath) || !File.Exists(sourcePath))
                return false;

            long sourceBytes = new FileInfo(sourcePath).Length;
            if (sourceBytes <= UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() + 1L)
                return false;

            int killPercent = ClampProfileUIntToInt(profile.KillPercent, 1, 99);
            long requestedKillBytes = (sourceBytes * (long)killPercent) / 100L;
            long minimumKillBytes = UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>();
            long maximumKillBytes = sourceBytes - 1L;
            long killBytes = requestedKillBytes < minimumKillBytes
                ? minimumKillBytes
                : requestedKillBytes > maximumKillBytes
                    ? maximumKillBytes
                    : requestedKillBytes;
            string partialPath = string.Concat(destinationPath, PartialFileSuffix);
            try
            {
                DeleteIfExists(partialPath);
            }
            catch
            {
                return false;
            }

            PartialCopyState state = new PartialCopyState // COLD ALLOC: PartialCopyState[1] - QA partial WAL copy worker state - owner: WalIntegrityFuzzerCore
            {
                SourcePath = sourcePath,
                PartialPath = partialPath,
                KillAfterBytes = killBytes,
                ChunkBytes = (int)math.clamp(profile.ChunkBytes, 1024u, 8192u)
            };

            Thread worker = new Thread(PartialWalCopyThread) // COLD ALLOC: Thread[1] - QA partial WAL copy worker - owner: WalIntegrityFuzzerCore
            {
                IsBackground = true,
                Name = "H8_MERKLE_WAL_SHINOBU_256"
            };

            if (!TryStartPartialWalCopyWorkerNoThrow(worker, state))
                return false;

            Stopwatch yieldTimer = Stopwatch.StartNew();
            while (Volatile.Read(ref state.Yielded) == 0 && Volatile.Read(ref state.ErrorCode) == 0 && yieldTimer.ElapsedMilliseconds < 5000L)
                Thread.Yield();

            yieldTimer.Stop();
            yieldMicros = ResolvePartialWalCopyStallMicros(state, yieldTimer.ElapsedTicks);
            if (!TryJoinPartialWalCopyWorkerNoThrow(worker, PartialWalCopyJoinMilliseconds))
            {
                Volatile.Write(ref state.Cancel, 1);
                TryJoinPartialWalCopyWorkerNoThrow(worker, PartialWalCopyCancelJoinMilliseconds);
                return false;
            }

            fileBytes = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0L;
            if (Volatile.Read(ref state.ErrorCode) != 0 ||
                fileBytes < UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() ||
                fileBytes >= sourceBytes)
            {
                DeleteIfExists(partialPath);
                return false;
            }

            try
            {
                if (File.Exists(destinationPath))
                    File.Replace(partialPath, destinationPath, null, true);
                else
                    File.Move(partialPath, destinationPath);

                fileBytes = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0L;
                return fileBytes >= UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() && fileBytes < sourceBytes;
            }
            catch
            {
                try
                {
                    DeleteIfExists(partialPath);
                }
                catch
                {
                }
                return false;
            }
        }

        private static void PartialWalCopyThread(object boxed)
        {
            PartialCopyState state = (PartialCopyState)boxed;
            long workerEnteredTimestamp = Stopwatch.GetTimestamp();
            try
            {
                using FileStream source = new FileStream(state.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                using FileStream destination = new FileStream(state.PartialPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.SequentialScan);
                Span<byte> scratch = stackalloc byte[8192];
                long copied = 0L;
                while (copied < state.KillAfterBytes && Volatile.Read(ref state.Cancel) == 0)
                {
                    long remaining = state.KillAfterBytes - copied;
                    int requested = (int)Math.Min(Math.Min((long)scratch.Length, (long)state.ChunkBytes), remaining);
                    int read = source.Read(scratch.Slice(0, requested));
                    if (read <= 0)
                        break;

                    destination.Write(scratch.Slice(0, read));
                    copied += read;
                    if (Volatile.Read(ref state.Yielded) == 0)
                    {
                        Volatile.Write(ref state.StallTicks, Stopwatch.GetTimestamp() - workerEnteredTimestamp);
                        Volatile.Write(ref state.Yielded, 1);
                        Thread.Yield();
                    }
                }

                destination.Flush(true);
            }
            catch
            {
                Volatile.Write(ref state.ErrorCode, 1);
            }
        }

        /// <summary>
        /// An async stall is a property of the copy worker's I/O, not of OS thread bootstrap. Once the
        /// worker has published its own first-yield latency that measurement wins; the caller's spin time
        /// is only used when the worker never reached a yield point, which is the genuine stall case and
        /// still trips <see cref="AsyncStallFailure"/>.
        /// </summary>
        private static long ResolvePartialWalCopyStallMicros(PartialCopyState state, long callerWaitTicks)
        {
            if (state != null && Volatile.Read(ref state.Yielded) != 0)
            {
                long workerTicks = Volatile.Read(ref state.StallTicks);
                if (workerTicks >= 0L)
                    return TicksToMicros(workerTicks);
            }

            return TicksToMicros(callerWaitTicks);
        }

        private static bool TryStartPartialWalCopyWorkerNoThrow(Thread worker, PartialCopyState state)
        {
            if (worker == null)
                return false;

            try
            {
                worker.Start(state);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryJoinPartialWalCopyWorkerNoThrow(Thread worker, int timeoutMilliseconds)
        {
            if (worker == null || !worker.IsAlive)
                return true;

            if (Thread.CurrentThread.ManagedThreadId == worker.ManagedThreadId)
                return false;

            try
            {
                worker.Join(timeoutMilliseconds);
                return !worker.IsAlive;
            }
            catch
            {
                return false;
            }
        }

        private static void DisposeMerkleBuffers(ref SaveMerkleVaultBufferSet buffers)
        {
            DisposeTrackedTempJobArray(ref buffers.Lz4HashTable);
            DisposeTrackedTempJobArray(ref buffers.Counters);
            DisposeTrackedTempJobArray(ref buffers.TelemetryRing);
            DisposeTrackedTempJobArray(ref buffers.Lz4BlockHeaders);
            DisposeTrackedTempJobArray(ref buffers.CompressedBytes);
            DisposeTrackedTempJobArray(ref buffers.PrunedDeltaBytes);
            DisposeTrackedTempJobArray(ref buffers.DeltaBytes);
            DisposeTrackedTempJobArray(ref buffers.DeltaRecords);
            DisposeTrackedTempJobArray(ref buffers.LeafDescriptors);
            DisposeTrackedTempJobArray(ref buffers.PreviousTree);
            DisposeTrackedTempJobArray(ref buffers.CurrentTree);
        }

        private static void RunSectorPagingStress(string rootDirectory, in WalFuzzerProfileDTO profile, ref WalFuzzerResultDTO result, NativeArray<WalFuzzerTelemetryEntry> telemetry)
        {
            int sectorCount = ClampProfileUIntToInt(profile.SectorCount, 5000, MaxSectorCount);
            const int sectorPayloadBytes = 128;
            string path = Path.Combine(rootDirectory, SectorFileName);
            long indexBytes = sectorCount * (long)UnsafeUtility.SizeOf<WalSectorIndexEntryDTO>();
            int targetIndex = sectorCount - 17;
            long targetSectorHash = BuildExtremeSectorHash(targetIndex);

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.RandomAccess))
                {
                    Span<byte> entryBytes = stackalloc byte[32];
                    Span<byte> payload = stackalloc byte[sectorPayloadBytes];
                    for (int i = 0; i < sectorCount; i++)
                    {
                        FillSectorPayload(payload, i);
                        fixed (byte* payloadData = payload)
                        {
                            WalSectorIndexEntryDTO entry = new WalSectorIndexEntryDTO
                            {
                                SectorHash = BuildExtremeSectorHash(i),
                                ByteOffset = indexBytes + i * (long)sectorPayloadBytes,
                                ByteCount = sectorPayloadBytes,
                                PayloadHash = unchecked((uint)SaveBinaryStorage.Hash64(payloadData, sectorPayloadBytes)),
                                Flags = 1u
                            };

                            WriteSectorIndexEntryLittleEndian(entryBytes, in entry);
                        }

                        stream.Position = i * (long)UnsafeUtility.SizeOf<WalSectorIndexEntryDTO>();
                        stream.Write(entryBytes);
                    }

                    for (int i = 0; i < sectorCount; i++)
                    {
                        FillSectorPayload(payload, i);
                        stream.Position = indexBytes + i * (long)sectorPayloadBytes;
                        stream.Write(payload);
                    }

                    stream.Flush(true);

                    long bytesRead = 0L;
                    stream.Position = targetIndex * (long)UnsafeUtility.SizeOf<WalSectorIndexEntryDTO>();
                    if (!ReadExact(stream, entryBytes))
                    {
                        MarkFailure(ref result, MemoryBloatFailure, 6u, stream.Position);
                        return;
                    }

                    bytesRead += entryBytes.Length;
                    WalSectorIndexEntryDTO targetEntry;
                    fixed (byte* entryPtr = entryBytes)
                    {
                        targetEntry = ReadSectorIndexEntryLittleEndian(entryPtr);
                    }

                    stream.Position = targetEntry.ByteOffset;
                    if (!ReadExact(stream, payload))
                    {
                        MarkFailure(ref result, MemoryBloatFailure, 7u, targetEntry.ByteOffset);
                        return;
                    }

                    bytesRead += payload.Length;
                    result.PagingBytesRead = bytesRead;
                    result.FailedSectorHash = targetEntry.SectorHash;
                    if (targetEntry.SectorHash != targetSectorHash || bytesRead > UnsafeUtility.SizeOf<WalSectorIndexEntryDTO>() + sectorPayloadBytes)
                    {
                        MarkFailure(ref result, MemoryBloatFailure, 8u, bytesRead);
                    }

                    fixed (byte* payloadData = payload)
                    {
                        uint hash = unchecked((uint)SaveBinaryStorage.Hash64(payloadData, sectorPayloadBytes));
                        if (hash != targetEntry.PayloadHash)
                        {
                            MarkFailure(ref result, DataCorruptionFailure, 9u, bytesRead);
                        }
                    }
                }

                RecordTelemetry(telemetry, 1u, HashAscii("sector_seek"), targetSectorHash, 0UL, result.PagingBytesRead, sectorPayloadBytes, 0u, 0u);
            }
            catch
            {
                MarkFailure(ref result, MemoryBloatFailure, 10u);
            }
        }

        private static void RunContinuousWriteFuzzer(string rootDirectory, in WalFuzzerProfileDTO profile, ref WalFuzzerResultDTO result, NativeArray<WalFuzzerTelemetryEntry> telemetry)
        {
            int payloadBytes = ClampProfileUIntToInt(profile.LoopPayloadBytes, 256, MaxLoopPayloadBytes);
            int iterations = ClampProfileUIntToInt(profile.LoopIterations, 1000, MaxLoopIterations);
            string path = Path.Combine(rootDirectory, LoopFileName);
            NativeArray<byte> payload = default;
            NativeArray<byte> readback = default;

            try
            {
                payload = AllocateTrackedTempJobArray<byte>(payloadBytes, LoopPayloadScratchLabel, NativeArrayOptions.UninitializedMemory);
                readback = AllocateTrackedTempJobArray<byte>(payloadBytes, LoopReadbackScratchLabel, NativeArrayOptions.UninitializedMemory);
                CompleteColdValidationBarrier(new GenerateSyntheticSaveDataJob { Payload = payload, Seed = DefaultSeed ^ 0xC0FFEEu }.Schedule(payload.Length, 256));

                byte* payloadData = (byte*)payload.GetUnsafePtr();
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.RandomAccess);

                EntityDeltaHeaderDTO warmHeader = BuildLoopHeader(payloadData, payloadBytes, 0);
                WriteWalToOpenStream(stream, in warmHeader, payloadData, payloadBytes);
                stream.Position = 0L;
                TryReadWalFromOpenStream(stream, readback, out _, out _, out _);

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; i++)
                {
                    payloadData[i % payloadBytes] ^= (byte)(i + 1);
                    EntityDeltaHeaderDTO header = BuildLoopHeader(payloadData, payloadBytes, i + 1);
                    WriteWalToOpenStream(stream, in header, payloadData, payloadBytes);
                    stream.Position = 0L;
                    if (!TryReadWalFromOpenStream(stream, readback, out EntityDeltaHeaderDTO readHeader, out ulong readHash, out _))
                    {
                        MarkFailure(ref result, DataCorruptionFailure, 11u);
                        break;
                    }

                    if (readHash != readHeader.XXHash3Checksum)
                    {
                        MarkFailure(ref result, DataCorruptionFailure, 12u);
                        break;
                    }
                }

                result.ManagedAllocBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (profile.EnforceZeroGcLoop != 0u && result.ManagedAllocBytes != 0L)
                {
                    MarkFailure(ref result, ManagedAllocationFailure, 14u);
                }

                RecordTelemetry(telemetry, 2u, HashAscii("loop"), BuildExtremeSectorHash(2), 0UL, iterations, (uint)payloadBytes, result.ErrorFlags, result.ErrorCode);
            }
            catch
            {
                MarkFailure(ref result, DataCorruptionFailure, 13u);
            }
            finally
            {
                DisposeTrackedTempJobArray(ref readback);
                DisposeTrackedTempJobArray(ref payload);
            }
        }

        private static NativeArray<T> AllocateTrackedTempArray<T>(int length, string label, NativeArrayOptions options)
            where T : struct
        {
            return AllocateTrackedArray<T>(length, Allocator.Temp, label, NativeAllocationLifetime.Temp, options);
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, string label, NativeArrayOptions options)
            where T : struct
        {
            return AllocateTrackedArray<T>(length, Allocator.TempJob, label, NativeAllocationLifetime.TempJob, options);
        }

        private static NativeArray<T> AllocateTrackedArray<T>(
            int length,
            Allocator allocator,
            string label,
            NativeAllocationLifetime lifetime,
            NativeArrayOptions options)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId > 0)
                    return array;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;
                try
                {
                    DisposeTrackedArray(ref array);
                }
                catch (Exception cleanupFault)
                {
                    cleanupException = cleanupFault;
                }

                if (cleanupException != null)
                    throw new AggregateException(
                        "WAL integrity fuzzer NativeArray allocation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }

            InvalidOperationException registrationException = new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
            Exception registrationCleanupException = null;
            try
            {
                DisposeTrackedArray(ref array);
            }
            catch (Exception cleanupFault)
            {
                registrationCleanupException = cleanupFault;
            }

            if (registrationCleanupException != null)
                throw new AggregateException(
                    "WAL integrity fuzzer NativeArray registration failed and cleanup also failed.",
                    registrationException,
                    registrationCleanupException);

            throw registrationException;
        }

        private static void DisposeTrackedTempArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            DisposeTrackedArray(ref array);
        }

        private static void DisposeTrackedTempJobArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            DisposeTrackedArray(ref array);
        }

        private static unsafe void DisposeTrackedArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static EntityDeltaHeaderDTO BuildLoopHeader(byte* payloadData, int payloadBytes, int iteration)
        {
            ulong hash = SaveBinaryStorage.Hash64(payloadData, payloadBytes);
            return new EntityDeltaHeaderDTO
            {
                SectorHash = unchecked((ulong)BuildExtremeSectorHash(iteration)),
                CompressedSize = (uint)payloadBytes,
                UncompressedSize = (uint)payloadBytes,
                XXHash3Checksum = hash
            };
        }

        private static bool ParseProfiles(NativeArray<byte> bytes, NativeArray<WalFuzzerProfileDTO> profiles, out int count, out uint errorCode)
        {
            count = 0;
            errorCode = 0u;
            byte* ptr = (byte*)bytes.GetUnsafeReadOnlyPtr();
            int cursor = 0;
            SkipLine(ptr, bytes.Length, ref cursor);
            while (cursor < bytes.Length && count < profiles.Length)
            {
                WalFuzzerProfileDTO profile = BuildDefaultProfile();
                profile.NameHash = ParseTokenHash(ptr, bytes.Length, ref cursor);
                byte delimiter;
                profile.PayloadBytes = ParseUInt(ptr, bytes.Length, ref cursor, profile.PayloadBytes, out delimiter);
                profile.LoopPayloadBytes = ParseUInt(ptr, bytes.Length, ref cursor, profile.LoopPayloadBytes, out delimiter);
                profile.LoopIterations = ParseUInt(ptr, bytes.Length, ref cursor, profile.LoopIterations, out delimiter);
                profile.KillPercent = ParseUInt(ptr, bytes.Length, ref cursor, profile.KillPercent, out delimiter);
                profile.SectorCount = ParseUInt(ptr, bytes.Length, ref cursor, profile.SectorCount, out delimiter);
                profile.ChunkBytes = ParseUInt(ptr, bytes.Length, ref cursor, profile.ChunkBytes, out delimiter);
                profile.StallThresholdMicros = ParseUInt(ptr, bytes.Length, ref cursor, profile.StallThresholdMicros, out delimiter);
                uint qualityPermille = 1000u;
                if (delimiter == ',')
                    qualityPermille = ParseUInt(ptr, bytes.Length, ref cursor, qualityPermille, out delimiter);
                profile.GlobalQualityWeight = math.saturate(qualityPermille * 0.001f);
                if (delimiter != '\n')
                    SkipLine(ptr, bytes.Length, ref cursor);
                profiles[count++] = profile;
            }

            return count > 0;
        }

        private static uint ParseTokenHash(byte* ptr, int length, ref int cursor)
        {
            uint hash = 2166136261u;
            while (cursor < length)
            {
                byte value = ptr[cursor++];
                if (value == ',' || value == '\n' || value == '\r')
                    break;
                hash = (hash ^ value) * 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ParseUInt(byte* ptr, int length, ref int cursor, uint fallback, out byte delimiter)
        {
            delimiter = 0;
            uint value = 0u;
            bool hasDigit = false;
            while (cursor < length)
            {
                byte c = ptr[cursor++];
                if (c == ',' || c == '\n' || c == '\r')
                {
                    delimiter = c;
                    break;
                }
                if (c >= '0' && c <= '9')
                {
                    hasDigit = true;
                    uint digit = (uint)(c - '0');
                    value = value > (uint.MaxValue - digit) / 10u
                        ? uint.MaxValue
                        : (value * 10u) + digit;
                }
            }

            return hasDigit ? value : fallback;
        }

        private static int ClampProfileUIntToInt(uint value, int minimum, int maximum)
        {
            uint min = (uint)math.max(0, minimum);
            uint max = (uint)math.max(minimum, maximum);
            if (value < min)
                return (int)min;
            if (value > max)
                return (int)max;
            return (int)value;
        }

        private static void SkipLine(byte* ptr, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte value = ptr[cursor++];
                if (value == '\n')
                    break;
            }
        }

        private static void FillSectorPayload(Span<byte> payload, int index)
        {
            uint seed = DefaultSeed ^ (uint)index * 747796405u;
            for (int i = 0; i < payload.Length; i++)
                payload[i] = ExpectedByte(i, seed);
        }

        private static long BuildExtremeSectorHash(int index)
        {
            unchecked
            {
                long safeIndex = index < 0 ? 0L : index;
                double aupX = -AupStressExtentMeters + (double)((safeIndex * 7919L) % 99800L);
                double aupZ = AupStressExtentMeters - (double)((safeIndex * 6841L) % 99800L);
                int x = ClampToInt32(Math.Floor(aupX / SectorMeters));
                int z = ClampToInt32(Math.Floor(aupZ / SectorMeters));
                return ((long)x << 32) | (uint)z;
            }
        }

        private static int ClampToInt32(double value)
        {
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

        private static void RecordTelemetry(NativeArray<WalFuzzerTelemetryEntry> telemetry, uint frame, uint phase, long sectorHash, ulong payloadHash, long offset, uint bytes, uint flags, uint error)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int index = (int)(frame % (uint)telemetry.Length);
            telemetry[index] = new WalFuzzerTelemetryEntry
            {
                Frame = frame,
                PhaseHash = phase,
                SectorHash = sectorHash,
                PayloadHash = payloadHash,
                FileOffset = offset,
                Bytes = bytes,
                Flags = flags,
                ErrorCode = error
            };
        }

        private static void WriteFailureCsv(in WalFuzzerResultDTO result)
        {
            string path = ResolveProjectPath(FailureCsvRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "phase,error_flags,error_code,offset,sector_hash,first_mismatch,csv_failure_rows,managed_alloc_bytes_current_thread,merkle_replay_bytes,merkle_block_count\n");
            WriteAscii(stream, "wal_integrity,");
            WriteUInt(stream, result.ErrorFlags);
            WriteAscii(stream, ",");
            WriteUInt(stream, result.ErrorCode);
            WriteAscii(stream, ",");
            WriteLong(stream, result.CorruptionOffset);
            WriteAscii(stream, ",");
            WriteLong(stream, result.FailedSectorHash);
            WriteAscii(stream, ",");
            WriteUInt(stream, result.FirstMismatchOffset);
            WriteAscii(stream, ",");
            WriteUInt(stream, result.CsvFailureRows);
            WriteAscii(stream, ",");
            WriteLong(stream, result.ManagedAllocBytes);
            WriteAscii(stream, ",");
            WriteUInt(stream, result.MerkleReplayBytes);
            WriteAscii(stream, ",");
            WriteUInt(stream, result.MerkleBlockCount);
            WriteAscii(stream, "\n");
        }

        private static void WriteQaReport(in WalFuzzerResultDTO result)
        {
            string path = ResolveProjectPath(QaReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "{\"summary\":\"WAL Integrity Verified\",\"agent\":\"SHINOBU_256\",\"truthHash\":\"0x");
            WriteHex64(stream, result.TruthHash);
            WriteAscii(stream, "\",\"recoveredHash\":\"0x");
            WriteHex64(stream, result.RecoveredHash);
            WriteAscii(stream, "\",\"payloadBytes\":");
            WriteUInt(stream, result.RecoveredBytes);
            WriteAscii(stream, ",\"sectorCount\":");
            WriteUInt(stream, result.SectorCount);
            WriteAscii(stream, ",\"loopIterations\":");
            WriteUInt(stream, result.LoopIterations);
            WriteAscii(stream, ",\"merkleReplayBytes\":");
            WriteUInt(stream, result.MerkleReplayBytes);
            WriteAscii(stream, ",\"merkleBlockCount\":");
            WriteUInt(stream, result.MerkleBlockCount);
            WriteAscii(stream, ",\"managedAllocBytesCurrentThread\":");
            WriteLong(stream, result.ManagedAllocBytes);
            WriteAscii(stream, "}\n");
        }

        private static void DumpBlackBox(NativeArray<WalFuzzerTelemetryEntry> telemetry, in WalFuzzerResultDTO result)
        {
            if (!telemetry.IsCreated)
                return;

            string path = ResolveProjectPath(DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WalFuzzerDumpHeader header = new WalFuzzerDumpHeader
            {
                Magic = DumpMagic,
                TruthHash = result.TruthHash,
                RecoveredHash = result.RecoveredHash,
                Version = 1u,
                HeaderBytes = (uint)UnsafeUtility.SizeOf<WalFuzzerDumpHeader>(),
                EntryBytes = (uint)UnsafeUtility.SizeOf<WalFuzzerTelemetryEntry>(),
                EntryCount = (uint)telemetry.Length,
                ErrorFlags = result.ErrorFlags,
                ErrorCode = result.ErrorCode,
                ResultBytes = result.RecoveredBytes
            };

            Span<byte> row = stackalloc byte[64];
            WriteDumpHeaderLittleEndian(row, in header);
            stream.Write(row);
            for (int i = 0; i < telemetry.Length; i++)
            {
                WalFuzzerTelemetryEntry entry = telemetry[i];
                WriteTelemetryEntryLittleEndian(row, in entry);
                stream.Write(row);
            }

            stream.Flush(true);
        }

        private static void WriteDumpHeaderLittleEndian(Span<byte> destination, in WalFuzzerDumpHeader header)
        {
            destination.Clear();
            WriteULongLittleEndian(destination, 0, header.Magic);
            WriteULongLittleEndian(destination, 8, header.TruthHash);
            WriteULongLittleEndian(destination, 16, header.RecoveredHash);
            WriteUIntLittleEndian(destination, 24, header.Version);
            WriteUIntLittleEndian(destination, 28, header.HeaderBytes);
            WriteUIntLittleEndian(destination, 32, header.EntryBytes);
            WriteUIntLittleEndian(destination, 36, header.EntryCount);
            WriteUIntLittleEndian(destination, 40, header.ErrorFlags);
            WriteUIntLittleEndian(destination, 44, header.ErrorCode);
            WriteUIntLittleEndian(destination, 48, header.ResultBytes);
        }

        private static void WriteTelemetryEntryLittleEndian(Span<byte> destination, in WalFuzzerTelemetryEntry entry)
        {
            destination.Clear();
            WriteUIntLittleEndian(destination, 0, entry.Frame);
            WriteUIntLittleEndian(destination, 4, entry.PhaseHash);
            WriteULongLittleEndian(destination, 8, unchecked((ulong)entry.SectorHash));
            WriteULongLittleEndian(destination, 16, entry.PayloadHash);
            WriteULongLittleEndian(destination, 24, unchecked((ulong)entry.FileOffset));
            WriteUIntLittleEndian(destination, 32, entry.Bytes);
            WriteUIntLittleEndian(destination, 36, entry.Flags);
            WriteUIntLittleEndian(destination, 40, entry.ErrorCode);
        }

        private static void WriteAscii(FileStream stream, string value)
        {
            Span<byte> scratch = stackalloc byte[256];
            int cursor = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (cursor == scratch.Length)
                {
                    stream.Write(scratch.Slice(0, cursor));
                    cursor = 0;
                }

                scratch[cursor++] = (byte)(value[i] <= 127 ? value[i] : '?');
            }

            if (cursor > 0)
                stream.Write(scratch.Slice(0, cursor));
        }

        private static void WriteUInt(FileStream stream, uint value)
        {
            Span<byte> scratch = stackalloc byte[16];
            int cursor = scratch.Length;
            if (value == 0u)
            {
                scratch[--cursor] = (byte)'0';
            }
            else
            {
                while (value > 0u)
                {
                    scratch[--cursor] = (byte)('0' + value % 10u);
                    value /= 10u;
                }
            }

            stream.Write(scratch.Slice(cursor));
        }

        private static void WriteLong(FileStream stream, long value)
        {
            if (value < 0L)
            {
                Span<byte> minus = stackalloc byte[1];
                minus[0] = (byte)'-';
                stream.Write(minus);
                ulong magnitude = (ulong)(-(value + 1L)) + 1UL;
                WriteUInt64(stream, magnitude);
                return;
            }

            WriteUInt64(stream, (ulong)value);
        }

        private static void WriteUInt64(FileStream stream, ulong value)
        {
            Span<byte> scratch = stackalloc byte[32];
            int cursor = scratch.Length;
            if (value == 0UL)
            {
                scratch[--cursor] = (byte)'0';
            }
            else
            {
                while (value > 0UL)
                {
                    scratch[--cursor] = (byte)('0' + value % 10UL);
                    value /= 10UL;
                }
            }

            stream.Write(scratch.Slice(cursor));
        }

        private static void WriteHex64(FileStream stream, ulong value)
        {
            Span<byte> scratch = stackalloc byte[16];
            for (int i = 15; i >= 0; i--)
            {
                int nibble = (int)(value & 0xFUL);
                scratch[i] = (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                value >>= 4;
            }

            stream.Write(scratch);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return Directory.GetCurrentDirectory();

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
                return normalized;

            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                string trimmedDataPath = dataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string leaf = Path.GetFileName(trimmedDataPath);
                if (string.Equals(leaf, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    DirectoryInfo assetsDirectory = Directory.GetParent(trimmedDataPath);
                    if (assetsDirectory != null)
                        return Path.Combine(assetsDirectory.FullName, normalized);
                }
            }

            string current = Directory.GetCurrentDirectory();
            DirectoryInfo cursor = new DirectoryInfo(current);
            while (cursor != null)
            {
                if (Directory.Exists(Path.Combine(cursor.FullName, "Assets")) &&
                    Directory.Exists(Path.Combine(cursor.FullName, "ProjectSettings")))
                {
                    return Path.Combine(cursor.FullName, normalized);
                }

                cursor = cursor.Parent;
            }

            return Path.Combine(current, normalized);
        }

        private static uint HashAscii(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ (byte)value[i]) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static long TicksToMicros(long ticks)
        {
            return (ticks * 1000000L) / Stopwatch.Frequency;
        }

        private static int Align16(int value)
        {
            int safe = math.max(0, value);
            return (safe + 15) & ~15;
        }

        private static void CompleteColdValidationBarrier(JobHandle handle)
        {
            // Offline NUnit/editor proof boundary. This fuzzer is never scheduled in the gameplay frame graph.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal struct GenerateSyntheticSaveDataJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<byte> Payload;
            public uint Seed;

            public void Execute(int index)
            {
                Payload[index] = ExpectedByte(index, Seed);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal unsafe struct ValidateRecoveredPayloadJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Payload;
            // Required by SHINOBU_256 prompt: mutate the explicit-layout result DTO through UnsafeUtility.AsRef without property copies.
            [NativeDisableUnsafePtrRestriction] public void* ResultPtr;
            public uint Seed;
            public ulong ExpectedHash;
            public int ByteCount;

            public void Execute()
            {
                ref WalFuzzerResultDTO result = ref UnsafeUtility.AsRef<WalFuzzerResultDTO>(ResultPtr);
                if (!Payload.IsCreated || ByteCount <= 0 || ByteCount > Payload.Length)
                {
                    MarkFailure(ref result, DataCorruptionFailure, 20u);
                    return;
                }

                byte* ptr = (byte*)Payload.GetUnsafeReadOnlyPtr();
                ulong hash = SaveBinaryStorage.Hash64(ptr, ByteCount);
                result.RecoveredHash = hash;
                if (hash != ExpectedHash)
                {
                    MarkFailure(ref result, DataCorruptionFailure, 21u);
                    return;
                }

                for (int i = 0; i < ByteCount; i++)
                {
                    if (ptr[i] == ExpectedByte(i, Seed))
                        continue;

                    result.FirstMismatchOffset = (uint)i;
                    MarkFailure(ref result, DataCorruptionFailure, 22u, i);
                    return;
                }
            }
        }

        private sealed class PartialCopyState
        {
            public string SourcePath;
            public string PartialPath;
            public long KillAfterBytes;
            public int ChunkBytes;
            public int Yielded;
            public int ErrorCode;
            public int Cancel;

            /// <summary>
            /// Stopwatch ticks the worker itself spent between entering its body and reaching the first
            /// yield point. Published before <see cref="Yielded"/> so a reader that sees the flag also
            /// sees the measurement.
            /// </summary>
            public long StallTicks;
        }
    }
}
#endif
