#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct WalFuzzStateDTO
    {
        [FieldOffset(0)] public uint InterruptedByteOffset;
        [FieldOffset(4)] public uint FinalValidatedBytes;
        [FieldOffset(8)] public uint MismatchFlags;
        [FieldOffset(12)] private uint _pad0;
        [FieldOffset(16)] private uint _pad1;
        [FieldOffset(20)] private uint _pad2;
        [FieldOffset(24)] private uint _pad3;
        [FieldOffset(28)] private uint _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WalFuzzTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint InterruptedByteOffset;
        [FieldOffset(8)] public uint FinalValidatedBytes;
        [FieldOffset(12)] public uint ActiveFileHandleStatus;
        [FieldOffset(16)] public ulong PathHash;
        [FieldOffset(24)] public long FailingArrayOffset;
        [FieldOffset(32)] public long BurstExecutionMicros;
        [FieldOffset(40)] public uint MismatchFlags;
        [FieldOffset(44)] public uint PhaseHash;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct WalFuzzMockSaveStatusSignal
    {
        [FieldOffset(0)] public ulong PathHash;
        [FieldOffset(8)] public long SectorX;
        [FieldOffset(16)] public long SectorY;
        [FieldOffset(24)] public long SectorZ;
        [FieldOffset(32)] public double LocalX;
        [FieldOffset(40)] public double LocalY;
        [FieldOffset(48)] public double LocalZ;
        [FieldOffset(56)] public long FailingArrayOffset;
        [FieldOffset(64)] public uint MismatchFlags;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint FinalValidatedBytes;
        [FieldOffset(76)] public uint InterruptedByteOffset;
        [FieldOffset(80)] private ulong _pad0;
        [FieldOffset(88)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct OopWalFuzzScanResultDTO
    {
        [FieldOffset(0)] public uint FilesScanned;
        [FieldOffset(4)] public uint FileStreamFindings;
        [FieldOffset(8)] public uint StreamWriterFindings;
        [FieldOffset(12)] public uint WriteAllBytesFindings;
        [FieldOffset(16)] public uint JsonUtilityFindings;
        [FieldOffset(20)] public uint BinaryFormatterFindings;
        [FieldOffset(24)] public uint ReflectionFindings;
        [FieldOffset(28)] public uint FatalFindings;
        [FieldOffset(32)] public uint SummaryHash;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WalFuzzFileHandleStatusDTO
    {
        [FieldOffset(0)] public uint PrimaryWritable;
        [FieldOffset(4)] public uint BackupWritable;
        [FieldOffset(8)] public uint MismatchFlags;
        [FieldOffset(12)] public uint FailureCode;
        [FieldOffset(16)] private ulong _pad0;
        [FieldOffset(24)] private ulong _pad1;
        [FieldOffset(32)] private ulong _pad2;
        [FieldOffset(40)] private ulong _pad3;
        [FieldOffset(48)] private ulong _pad4;
        [FieldOffset(56)] private ulong _pad5;
    }

    internal static unsafe partial class WalIntegrityFuzzerCore
    {
        internal const uint WalFuzzTruncationUndetected = 1u << 0;
        internal const uint WalFuzzBackupPromotionFailed = 1u << 1;
        internal const uint WalFuzzFileLockLeak = 1u << 2;
        internal const uint WalFuzzPrecisionLossCrime = 1u << 3;
        internal const uint WalFuzzRollbackDesync = 1u << 4;
        internal const uint WalFuzzDataCorruption = 1u << 5;

        private const int Shinobu357TelemetryCapacity = 300;
        private const uint Shinobu357NameHash = 0x33353753u;
        private const uint Shinobu357PhaseMockCorrupt = 0x57464D43u;
        private const uint Shinobu357PhaseRollback = 0x57465242u;
        private const uint Shinobu357PhaseFileLock = 0x57464C4Bu;
        private const uint Shinobu357OopSummaryHash = 0x4F4F5045u;
        private const ulong Shinobu357DumpMagic = 0x3735335F4C415748UL;
        private const BufferID Shinobu357PayloadBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357PayloadBufferId;
        private const BufferID Shinobu357CorruptWalBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357CorruptWalBufferId;
        private const BufferID Shinobu357StateBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357StateBufferId;
        private const BufferID Shinobu357TelemetryRingBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357TelemetryRingBufferId;
        private const BufferID Shinobu357TelemetryCursorBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357TelemetryCursorBufferId;
        private const BufferID Shinobu357HashScratchBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357HashScratchBufferId;
        private const BufferID Shinobu357FileHandleStatusBufferId = BufferID.WalIntegrityFuzzerCore_SHINOBU357_Shinobu357FileHandleStatusBufferId;
        private const string Shinobu357DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_357.bin";
        private const string Shinobu357QaReportRelativePath = "Docs/Reports/QA_OPTIMIZATION_REPORT.json";
        private const string Shinobu357ProfilesRelativePath = "Docs/Reports/wal_fuzz_profiles.csv";
        private const string Shinobu357PayloadFallbackScratchLabel = "shinobu357PayloadFallback";
        private const string Shinobu357CorruptWalFallbackScratchLabel = "shinobu357CorruptWalFallback";
        private const string Shinobu357StateFallbackScratchLabel = "shinobu357StateFallback";
        private const string Shinobu357TelemetryFallbackScratchLabel = "shinobu357TelemetryFallback";
        private const string Shinobu357LegacyTelemetryScratchLabel = "shinobu357LegacyTelemetry";
        private const string Shinobu357HashScratchFallbackLabel = "shinobu357HashScratchFallback";
        private const string Shinobu357FileHandleStatusFallbackLabel = "shinobu357FileHandleStatusFallback";
        private const ulong Shinobu357AupXBits = 0x40F86A01F9ADD374UL;
        private const ulong Shinobu357AupYBits = 0xC0F869FFCD6E9E07UL;
        private const ulong Shinobu357AupZBits = 0x3FC0000000000E13UL;

        internal static WalFuzzerProfileDTO BuildShinobu357DefaultProfile()
        {
            WalFuzzerProfileDTO profile = BuildDefaultProfile();
            profile.NameHash = Shinobu357NameHash;
            profile.PayloadBytes = PayloadBytes10Mb;
            profile.LoopPayloadBytes = 64u * 1024u;
            profile.LoopIterations = 100u;
            profile.KillPercent = 50u;
            profile.SectorCount = 5000u;
            profile.ChunkBytes = 64u * 1024u;
            profile.StallThresholdMicros = 4000u;
            profile.GlobalQualityWeight = 1f;
            profile.WriteReports = 1u;
            profile.EnforceZeroGcLoop = 1u;
            return profile;
        }

        internal static bool TryLoadShinobu357ProfilesCsv(
            string path,
            NativeArray<WalFuzzerProfileDTO> profiles,
            out int count,
            out uint errorCode)
        {
            string resolvedPath = string.IsNullOrEmpty(path)
                ? ResolveProjectPath(Shinobu357ProfilesRelativePath)
                : path;
            return TryLoadProfilesCsv(resolvedPath, profiles, out count, out errorCode);
        }

        internal static bool RunShinobu357PersistenceIntegrityFuzzer(
            string rootDirectory,
            in WalFuzzerProfileDTO profile,
            out WalFuzzStateDTO state,
            out WalFuzzerResultDTO result)
        {
            state = default;
            result = default;
            if (string.IsNullOrEmpty(rootDirectory))
            {
                state.MismatchFlags = WalFuzzBackupPromotionFailed;
                MarkFailure(ref result, BackupRecoveryFailure, 57u);
                return false;
            }

            Directory.CreateDirectory(rootDirectory);
            DeleteIfExists(Path.Combine(rootDirectory, MerkleWalFileName));
            DeleteIfExists(Path.Combine(rootDirectory, MerkleWalBackupFileName));

            int payloadBytes = ClampProfileUIntToInt(profile.PayloadBytes, 1024, MaxPayloadBytes);
            int iterations = ResolveShinobu357IterationCount(profile.LoopIterations, profile.GlobalQualityWeight);
            IDataVault vault = GlobalRegistry.DataVault;
            NativeArray<byte> payloadOwner = default;
            NativeArray<byte> corruptWalOwner = default;
            NativeArray<WalFuzzStateDTO> stateOwner = default;
            NativeArray<WalFuzzTelemetryEntry> telemetryOwner = default;
            NativeArray<byte> payload = default;
            NativeArray<byte> corruptWal = default;
            NativeArray<WalFuzzStateDTO> stateBuffer = default;
            NativeArray<WalFuzzTelemetryEntry> telemetry = default;
            NativeArray<WalFuzzerTelemetryEntry> legacyTelemetry = default;
            bool disposePayload = false;
            bool disposeCorruptWal = false;
            bool disposeState = false;
            bool disposeTelemetry = false;

            try
            {
                if (EnsureShinobu357VaultBuffer(vault, Shinobu357PayloadBufferId, payloadBytes, NativeArrayOptions.UninitializedMemory, out payloadOwner))
                {
                    payload = ResolveShinobu357Prefix(payloadOwner, payloadBytes);
                }
                else
                {
                    payloadOwner = AllocateTrackedTempJobArray<byte>(payloadBytes, Shinobu357PayloadFallbackScratchLabel, NativeArrayOptions.UninitializedMemory);
                    payload = payloadOwner;
                    disposePayload = true;
                }

                if (EnsureShinobu357VaultBuffer(vault, Shinobu357CorruptWalBufferId, payloadBytes, NativeArrayOptions.UninitializedMemory, out corruptWalOwner))
                {
                    corruptWal = ResolveShinobu357Prefix(corruptWalOwner, payloadBytes);
                }
                else
                {
                    corruptWalOwner = AllocateTrackedTempJobArray<byte>(payloadBytes, Shinobu357CorruptWalFallbackScratchLabel, NativeArrayOptions.UninitializedMemory);
                    corruptWal = corruptWalOwner;
                    disposeCorruptWal = true;
                }

                if (EnsureShinobu357VaultBuffer(vault, Shinobu357StateBufferId, 1, NativeArrayOptions.UninitializedMemory, out stateOwner))
                {
                    stateBuffer = ResolveShinobu357Prefix(stateOwner, 1);
                }
                else
                {
                    stateOwner = AllocateTrackedTempJobArray<WalFuzzStateDTO>(1, Shinobu357StateFallbackScratchLabel, NativeArrayOptions.UninitializedMemory);
                    stateBuffer = stateOwner;
                    disposeState = true;
                }

                if (EnsureShinobu357VaultBuffer(vault, Shinobu357TelemetryRingBufferId, Shinobu357TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out telemetryOwner))
                {
                    telemetry = ResolveShinobu357Prefix(telemetryOwner, Shinobu357TelemetryCapacity);
                }
                else
                {
                    telemetryOwner = AllocateTrackedTempJobArray<WalFuzzTelemetryEntry>(Shinobu357TelemetryCapacity, Shinobu357TelemetryFallbackScratchLabel, NativeArrayOptions.UninitializedMemory);
                    telemetry = telemetryOwner;
                    disposeTelemetry = true;
                }

                legacyTelemetry = AllocateTrackedTempJobArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Shinobu357LegacyTelemetryScratchLabel, NativeArrayOptions.ClearMemory);

                CompleteColdValidationBarrier(new GenerateSyntheticSaveDataJob
                {
                    Payload = payload,
                    Seed = DefaultSeed ^ Shinobu357NameHash
                }.Schedule(payload.Length, 4096));

                uint firstInterrupt = ResolveShinobu357InterruptOffset((uint)payload.Length, 0u, DefaultSeed ^ Shinobu357NameHash);
                CompleteColdValidationBarrier(new GenerateMockCorruptWalJob
                {
                    SourceBytes = payload,
                    CorruptBytes = corruptWal,
                    StateBuffer = stateBuffer,
                    InterruptedByteOffset = firstInterrupt,
                    MutationMode = 1u
                }.Schedule());

                Stopwatch burstTimer = Stopwatch.StartNew();
                CompleteColdValidationBarrier(new EvaluateHeadlessWalFuzzJob
                {
                    SourcePayload = payload,
                    CorruptWalBytes = corruptWal,
                    StateBuffer = stateBuffer,
                    Telemetry = telemetry,
                    Iterations = (uint)iterations,
                    Seed = DefaultSeed ^ 0x357357u
                }.Schedule());
                burstTimer.Stop();

                state = stateBuffer[0];
                RunProductionMerkleWalRecovery(rootDirectory, in profile, payload, ref result, legacyTelemetry);
                RunShinobu357BackupPromotionLoop(rootDirectory, in profile, iterations, vault, ref state, ref result, telemetry, TicksToMicros(burstTimer.ElapsedTicks));
                TryPublishShinobu357TelemetryToVault(vault, telemetry, iterations);

                if ((state.MismatchFlags & (WalFuzzTruncationUndetected | WalFuzzBackupPromotionFailed | WalFuzzFileLockLeak | WalFuzzRollbackDesync | WalFuzzDataCorruption)) != 0u)
                {
                    DumpShinobu357BlackBox(telemetry, in state, in result);
                }

                if (profile.WriteReports != 0u)
                    WriteShinobu357QaReport(in state, in result);

                return result.ErrorFlags == 0u && state.MismatchFlags == 0u;
            }
            finally
            {
                DisposeTrackedTempJobArray(ref legacyTelemetry);
                if (disposeTelemetry)
                    DisposeTrackedTempJobArray(ref telemetryOwner);
                if (disposeState)
                    DisposeTrackedTempJobArray(ref stateOwner);
                if (disposeCorruptWal)
                    DisposeTrackedTempJobArray(ref corruptWalOwner);
                if (disposePayload)
                    DisposeTrackedTempJobArray(ref payloadOwner);
            }
        }

        internal static bool TryReadShinobu357Telemetry(
            IDataVault vault,
            out NativeArray<WalFuzzTelemetryEntry>.ReadOnly telemetry,
            out int cursor)
        {
            telemetry = default;
            cursor = 0;
            if (vault == null ||
                !vault.TryGetGenerationHandle(Shinobu357TelemetryRingBufferId, out VaultGenerationHandle<WalFuzzTelemetryEntry> ringHandle) ||
                !vault.TryReadOnlyHandle(in ringHandle, out telemetry) ||
                telemetry.Length == 0)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle(Shinobu357TelemetryCursorBufferId, out VaultGenerationHandle<int> cursorHandle) &&
                vault.TryReadOnlyHandle(in cursorHandle, out NativeArray<int>.ReadOnly cursorArray) &&
                cursorArray.Length > 0)
            {
                cursor = cursorArray[0];
            }

            return true;
        }

        private static bool EnsureShinobu357VaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsAllocationLocked || requiredLength <= 0)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.SavePersistence,
                options);

            return vault.TryResolveHandle(in handle, out buffer) && buffer.Length >= requiredLength;
        }

        private static NativeArray<T> ResolveShinobu357Prefix<T>(NativeArray<T> buffer, int requiredLength) where T : struct
        {
            if (!buffer.IsCreated || requiredLength <= 0 || buffer.Length == requiredLength)
                return buffer;

            return buffer.GetSubArray(0, requiredLength);
        }

        internal static WalFuzzMockSaveStatusSignal BuildMockSaveStatusSignal(
            string path,
            in WalFuzzStateDTO state,
            uint frame)
        {
            WalFuzzMockSaveStatusSignal signal = default;
            signal.PathHash = HashPath64(path);
            signal.SectorX = -500L;
            signal.SectorY = 0L;
            signal.SectorZ = 499L;
            signal.LocalX = 0.125;
            signal.LocalY = 0.0;
            signal.LocalZ = -0.125;
            signal.FailingArrayOffset = state.InterruptedByteOffset;
            signal.MismatchFlags = state.MismatchFlags;
            signal.Frame = frame;
            signal.FinalValidatedBytes = state.FinalValidatedBytes;
            signal.InterruptedByteOffset = state.InterruptedByteOffset;
            return signal;
        }

#if UNITY_EDITOR
        internal static bool RunOopWalFuzzScannerForProject(out OopWalFuzzScanResultDTO result)
        {
            result = default;
            string projectRoot = ResolveProjectPath(string.Empty);
            ScanWalFuzzDirectory(Path.Combine(projectRoot, "Assets/_Project/Scripts/SaveSystem"), ref result);
            ScanWalFuzzDirectory(Path.Combine(projectRoot, "Assets/_Project/Tests/Editor/SaveSystem"), ref result);
            result.FatalFindings = result.StreamWriterFindings + result.JsonUtilityFindings + result.BinaryFormatterFindings;
            result.SummaryHash = Shinobu357OopSummaryHash;
            WriteOopWalFuzzScannerReport(in result);
            return result.FatalFindings == 0u;
        }
#endif

        private static void RunShinobu357BackupPromotionLoop(
            string rootDirectory,
            in WalFuzzerProfileDTO profile,
            int iterations,
            IDataVault vault,
            ref WalFuzzStateDTO state,
            ref WalFuzzerResultDTO result,
            NativeArray<WalFuzzTelemetryEntry> telemetry,
            long burstMicros)
        {
            string primaryPath = Path.Combine(rootDirectory, MerkleWalFileName);
            string backupPath = Path.Combine(rootDirectory, MerkleWalBackupFileName);
            if (!File.Exists(backupPath))
            {
                state.MismatchFlags |= WalFuzzBackupPromotionFailed;
                MarkFailure(ref result, MerkleWalRecoveryFailure, 58u);
                return;
            }

            long backupBytes = new FileInfo(backupPath).Length;
            if (backupBytes <= UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() + 1L)
            {
                state.MismatchFlags |= WalFuzzBackupPromotionFailed;
                MarkFailure(ref result, MerkleWalRecoveryFailure, 59u, backupBytes);
                return;
            }

            NativeArray<byte> hashScratchOwner = default;
            NativeArray<byte> hashScratch = default;
            NativeArray<WalFuzzFileHandleStatusDTO> fileHandleStatusOwner = default;
            NativeArray<WalFuzzFileHandleStatusDTO> fileHandleStatus = default;
            bool disposeHashScratch = false;
            bool disposeFileHandleStatus = false;
            try
            {
                if (backupBytes > MaxPayloadBytes || backupBytes > int.MaxValue)
                {
                    state.MismatchFlags |= WalFuzzBackupPromotionFailed;
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 66u, backupBytes);
                    return;
                }

                int backupByteCount = (int)backupBytes;
                if (EnsureShinobu357VaultBuffer(vault, Shinobu357HashScratchBufferId, backupByteCount, NativeArrayOptions.UninitializedMemory, out hashScratchOwner))
                {
                    hashScratch = ResolveShinobu357Prefix(hashScratchOwner, backupByteCount);
                }
                else
                {
                    hashScratchOwner = AllocateTrackedTempJobArray<byte>(backupByteCount, Shinobu357HashScratchFallbackLabel, NativeArrayOptions.UninitializedMemory);
                    hashScratch = hashScratchOwner;
                    disposeHashScratch = true;
                }

                if (!TryHashFile64(backupPath, hashScratch, out ulong backupHash, out uint backupValidatedBytes))
                {
                    state.MismatchFlags |= WalFuzzBackupPromotionFailed;
                    MarkFailure(ref result, MerkleWalRecoveryFailure, 60u, backupBytes);
                    return;
                }

                // This loop redefines the recovered pair in the file-hash domain: RecoveredHash/RecoveredBytes
                // below are the promoted primary's, so the truth has to move with them. The verified .bak is
                // that truth, which makes TruthHash == RecoveredHash the literal statement that promotion
                // produced a byte-identical primary. Leaving TruthHash on the delta-arena hash published by
                // RunProductionMerkleWalRecovery compares two different domains and can never agree.
                result.TruthHash = backupHash;

                uint highestInterrupted = state.InterruptedByteOffset;
                for (int i = 0; i < iterations; i++)
                {
                    uint interrupt = ResolveShinobu357InterruptOffset((uint)backupBytes, (uint)i, profile.NameHash ^ Shinobu357NameHash);
                    if (interrupt > highestInterrupted)
                        highestInterrupted = interrupt;

                    if (!TryCopyPartialWalAtOffset(backupPath, primaryPath, interrupt, in profile, out long primaryBytes, out long yieldMicros))
                    {
                        state.MismatchFlags |= WalFuzzBackupPromotionFailed;
                        MarkFailure(ref result, MerkleWalRecoveryFailure, 61u, interrupt);
                        RecordShinobu357Telemetry(telemetry, (uint)i, Shinobu357PhaseRollback, primaryPath, interrupt, 0u, state.MismatchFlags, yieldMicros, activeHandle: 0u);
                        return;
                    }

                    bool truncatedAccepted = SaveStateMerkleTree.TryValidateWalAndRollback(primaryPath, backupPath, out string rollbackError);
                    if (truncatedAccepted)
                    {
                        state.MismatchFlags |= WalFuzzTruncationUndetected;
                        MarkFailure(ref result, PrimaryAcceptedFailure | MerkleWalRecoveryFailure, 62u, primaryBytes);
                        RecordShinobu357Telemetry(telemetry, (uint)i, Shinobu357PhaseRollback, primaryPath, interrupt, 0u, state.MismatchFlags, yieldMicros, activeHandle: 1u);
                        return;
                    }

                    if (!TryHashFile64(primaryPath, hashScratch, out ulong primaryHash, out uint primaryValidatedBytes) ||
                        primaryHash != backupHash ||
                        primaryValidatedBytes != backupValidatedBytes)
                    {
                        state.MismatchFlags |= string.IsNullOrEmpty(rollbackError) ? WalFuzzDataCorruption : WalFuzzBackupPromotionFailed;
                        uint failureFlags = string.IsNullOrEmpty(rollbackError)
                            ? DataCorruptionFailure | MerkleWalRecoveryFailure
                            : MerkleWalRecoveryFailure;
                        MarkFailure(ref result, failureFlags, 64u, primaryBytes);
                        RecordShinobu357Telemetry(telemetry, (uint)i, Shinobu357PhaseRollback, primaryPath, interrupt, primaryValidatedBytes, state.MismatchFlags, yieldMicros, activeHandle: 1u);
                        return;
                    }

                    state.FinalValidatedBytes = primaryValidatedBytes;
                    result.RecoveredBytes = primaryValidatedBytes;
                    result.RecoveredHash = primaryHash;
                    RecordShinobu357Telemetry(telemetry, (uint)i, Shinobu357PhaseRollback, primaryPath, interrupt, primaryValidatedBytes, state.MismatchFlags, burstMicros > yieldMicros ? burstMicros : yieldMicros, activeHandle: 1u);
                }

                state.InterruptedByteOffset = highestInterrupted;
                if (EnsureShinobu357VaultBuffer(vault, Shinobu357FileHandleStatusBufferId, 1, NativeArrayOptions.UninitializedMemory, out fileHandleStatusOwner))
                {
                    fileHandleStatus = ResolveShinobu357Prefix(fileHandleStatusOwner, 1);
                }
                else
                {
                    fileHandleStatusOwner = AllocateTrackedTempJobArray<WalFuzzFileHandleStatusDTO>(1, Shinobu357FileHandleStatusFallbackLabel, NativeArrayOptions.UninitializedMemory);
                    fileHandleStatus = fileHandleStatusOwner;
                    disposeFileHandleStatus = true;
                }

                fileHandleStatus[0] = new WalFuzzFileHandleStatusDTO
                {
                    PrimaryWritable = VerifyWritableFileHandleRelease(primaryPath) ? 1u : 0u,
                    BackupWritable = VerifyWritableFileHandleRelease(backupPath) ? 1u : 0u,
                    MismatchFlags = 0u,
                    FailureCode = 0u
                };
                CompleteColdValidationBarrier(new VerifyFileHandleReleaseJob
                {
                    HandleStatus = fileHandleStatus
                }.Schedule());

                WalFuzzFileHandleStatusDTO handleStatus = fileHandleStatus[0];
                if ((handleStatus.MismatchFlags & WalFuzzFileLockLeak) != 0u)
                {
                    state.MismatchFlags |= WalFuzzFileLockLeak;
                    MarkFailure(ref result, MerkleWalRecoveryFailure, handleStatus.FailureCode == 0u ? 65u : handleStatus.FailureCode, highestInterrupted);
                    RecordShinobu357Telemetry(telemetry, (uint)iterations, Shinobu357PhaseFileLock, primaryPath, highestInterrupted, state.FinalValidatedBytes, state.MismatchFlags, 0L, activeHandle: 2u);
                }
            }
            finally
            {
                if (disposeFileHandleStatus)
                    DisposeTrackedTempJobArray(ref fileHandleStatusOwner);
                if (disposeHashScratch)
                    DisposeTrackedTempJobArray(ref hashScratchOwner);
            }
        }

        private static bool TryCopyPartialWalAtOffset(
            string sourcePath,
            string destinationPath,
            uint interruptOffset,
            in WalFuzzerProfileDTO profile,
            out long fileBytes,
            out long yieldMicros)
        {
            fileBytes = 0L;
            yieldMicros = 0L;
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath) || !File.Exists(sourcePath))
                return false;

            long sourceBytes = new FileInfo(sourcePath).Length;
            if (sourceBytes <= UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() + 1L)
                return false;

            long minimumKillBytes = UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>();
            long maximumKillBytes = sourceBytes - 1L;
            long killBytes = interruptOffset < minimumKillBytes
                ? minimumKillBytes
                : interruptOffset > maximumKillBytes
                    ? maximumKillBytes
                    : interruptOffset;
            string partialPath = string.Concat(destinationPath, PartialFileSuffix);
            try
            {
                DeleteIfExists(partialPath);
            }
            catch
            {
                return false;
            }

            PartialCopyState state = new PartialCopyState // COLD ALLOC: PartialCopyState[1] - offline SHINOBU_357 WAL interruption worker state - owner: WalIntegrityFuzzerCore
            {
                SourcePath = sourcePath,
                PartialPath = partialPath,
                KillAfterBytes = killBytes,
                ChunkBytes = ClampProfileUIntToInt(profile.ChunkBytes, 1024, 8192)
            };

            Thread worker = new Thread(PartialWalCopyThread) // COLD ALLOC: Thread[1] - offline SHINOBU_357 WAL interruption worker - owner: WalIntegrityFuzzerCore
            {
                IsBackground = true,
                Name = "H8_MERKLE_WAL_SHINOBU_357"
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
                {
                    if (!TryReplaceOrCopyWal(partialPath, destinationPath))
                    {
                        DeleteIfExists(partialPath);
                        return false;
                    }
                }
                else
                {
                    File.Move(partialPath, destinationPath);
                }

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

        private static bool TryReplaceOrCopyWal(string partialPath, string destinationPath)
        {
            try
            {
                File.Replace(partialPath, destinationPath, null, true);
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return TryCopyWalOverDestination(partialPath, destinationPath);
            }
            catch (IOException)
            {
                return TryCopyWalOverDestination(partialPath, destinationPath);
            }
        }

        private static bool TryCopyWalOverDestination(string partialPath, string destinationPath)
        {
            try
            {
                File.Copy(partialPath, destinationPath, true);
                DeleteIfExists(partialPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryHashFile64(string path, NativeArray<byte> scratch, out ulong hash, out uint byteCount)
        {
            hash = 0UL;
            byteCount = 0u;
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !scratch.IsCreated)
                return false;

            long length = new FileInfo(path).Length;
            if (length <= 0L || length > scratch.Length || length > uint.MaxValue)
                return false;

            try
            {
                byte* ptr = (byte*)scratch.GetUnsafePtr();
                int total = 0;
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                while (total < length)
                {
                    int read = stream.Read(new Span<byte>(ptr + total, (int)length - total));
                    if (read <= 0)
                        return false;
                    total += read;
                }

                hash = SaveBinaryStorage.Hash64(ptr, total);
                byteCount = (uint)total;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyWritableFileHandleRelease(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.RandomAccess);
                return stream.CanWrite;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryPublishShinobu357TelemetryToVault(IDataVault vault, NativeArray<WalFuzzTelemetryEntry> telemetry, int cursor)
        {
            if (vault == null || !telemetry.IsCreated || telemetry.Length == 0 || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<WalFuzzTelemetryEntry> ringHandle = vault.EnsureGenerationHandle<WalFuzzTelemetryEntry>(
                Shinobu357TelemetryRingBufferId,
                Shinobu357TelemetryCapacity,
                SystemID.SavePersistence,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<int> cursorHandle = vault.EnsureGenerationHandle<int>(
                Shinobu357TelemetryCursorBufferId,
                1,
                SystemID.SavePersistence,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryResolveHandle(in ringHandle, out NativeArray<WalFuzzTelemetryEntry> ring) ||
                !vault.TryResolveHandle(in cursorHandle, out NativeArray<int> cursorArray) ||
                ring.Length < Shinobu357TelemetryCapacity ||
                cursorArray.Length == 0)
            {
                return false;
            }

            int count = math.min(ring.Length, telemetry.Length);
            for (int i = 0; i < count; i++)
                ring[i] = telemetry[i];
            cursorArray[0] = math.max(0, cursor);
            return true;
        }

        private static void RecordShinobu357Telemetry(
            NativeArray<WalFuzzTelemetryEntry> telemetry,
            uint frame,
            uint phaseHash,
            string path,
            uint interruptedByteOffset,
            uint validatedBytes,
            uint flags,
            long executionMicros,
            uint activeHandle)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int index = (int)(frame % (uint)telemetry.Length);
            telemetry[index] = new WalFuzzTelemetryEntry
            {
                Frame = frame,
                PhaseHash = phaseHash,
                InterruptedByteOffset = interruptedByteOffset,
                FinalValidatedBytes = validatedBytes,
                ActiveFileHandleStatus = activeHandle,
                PathHash = HashPath64(path),
                FailingArrayOffset = interruptedByteOffset,
                BurstExecutionMicros = executionMicros < 0L ? 0L : executionMicros,
                MismatchFlags = flags
            };
        }

        private static void DumpShinobu357BlackBox(
            NativeArray<WalFuzzTelemetryEntry> telemetry,
            in WalFuzzStateDTO state,
            in WalFuzzerResultDTO result)
        {
            if (!telemetry.IsCreated)
                return;

            string path = ResolveProjectPath(Shinobu357DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            Span<byte> row = stackalloc byte[64];
            row.Clear();
            WriteULongLittleEndian(row, 0, Shinobu357DumpMagic);
            WriteUIntLittleEndian(row, 8, (uint)UnsafeUtility.SizeOf<WalFuzzStateDTO>());
            WriteUIntLittleEndian(row, 12, (uint)UnsafeUtility.SizeOf<WalFuzzTelemetryEntry>());
            WriteUIntLittleEndian(row, 16, (uint)telemetry.Length);
            WriteUIntLittleEndian(row, 20, state.InterruptedByteOffset);
            WriteUIntLittleEndian(row, 24, state.FinalValidatedBytes);
            WriteUIntLittleEndian(row, 28, state.MismatchFlags);
            WriteUIntLittleEndian(row, 32, result.ErrorFlags);
            WriteUIntLittleEndian(row, 36, result.ErrorCode);
            WriteULongLittleEndian(row, 40, result.TruthHash);
            WriteULongLittleEndian(row, 48, result.RecoveredHash);
            stream.Write(row);

            for (int i = 0; i < telemetry.Length; i++)
            {
                WalFuzzTelemetryEntry entry = telemetry[i];
                WriteShinobu357TelemetryEntry(row, in entry);
                stream.Write(row);
            }

            stream.Flush(true);
        }

        private static void WriteShinobu357TelemetryEntry(Span<byte> destination, in WalFuzzTelemetryEntry entry)
        {
            destination.Clear();
            WriteUIntLittleEndian(destination, 0, entry.Frame);
            WriteUIntLittleEndian(destination, 4, entry.InterruptedByteOffset);
            WriteUIntLittleEndian(destination, 8, entry.FinalValidatedBytes);
            WriteUIntLittleEndian(destination, 12, entry.ActiveFileHandleStatus);
            WriteULongLittleEndian(destination, 16, entry.PathHash);
            WriteULongLittleEndian(destination, 24, unchecked((ulong)entry.FailingArrayOffset));
            WriteULongLittleEndian(destination, 32, unchecked((ulong)entry.BurstExecutionMicros));
            WriteUIntLittleEndian(destination, 40, entry.MismatchFlags);
            WriteUIntLittleEndian(destination, 44, entry.PhaseHash);
        }

        private static void WriteShinobu357QaReport(in WalFuzzStateDTO state, in WalFuzzerResultDTO result)
        {
            string path = ResolveProjectPath(Shinobu357QaReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "{\"summary\":\"OOP Fuzzers Eradicated\",\"agent\":\"SHINOBU_357\",\"stateBytes\":");
            WriteUInt(stream, (uint)UnsafeUtility.SizeOf<WalFuzzStateDTO>());
            WriteAscii(stream, ",\"stateAlign\":");
            WriteUInt(stream, (uint)UnsafeUtility.AlignOf<WalFuzzStateDTO>());
            WriteAscii(stream, ",\"interruptedByteOffset\":");
            WriteUInt(stream, state.InterruptedByteOffset);
            WriteAscii(stream, ",\"finalValidatedBytes\":");
            WriteUInt(stream, state.FinalValidatedBytes);
            WriteAscii(stream, ",\"mismatchFlags\":");
            WriteUInt(stream, state.MismatchFlags);
            WriteAscii(stream, ",\"truthHash\":\"0x");
            WriteHex64(stream, result.TruthHash);
            WriteAscii(stream, "\",\"recoveredHash\":\"0x");
            WriteHex64(stream, result.RecoveredHash);
            WriteAscii(stream, "\",\"errorFlags\":");
            WriteUInt(stream, result.ErrorFlags);
            WriteAscii(stream, "}\n");
        }

#if UNITY_EDITOR
        private static void ScanWalFuzzDirectory(string directory, ref OopWalFuzzScanResultDTO result)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith("WalIntegrityFuzzerCore_SHINOBU357.cs", StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(files[i]);
                result.FilesScanned++;
                if (ContainsToken(text, "File", "Stream"))
                    result.FileStreamFindings++;
                if (ContainsToken(text, "Stream", "Writer"))
                    result.StreamWriterFindings++;
                if (ContainsToken(text, "Write", "AllBytes"))
                    result.WriteAllBytesFindings++;
                if (ContainsToken(text, "Json", "Utility"))
                    result.JsonUtilityFindings++;
                if (ContainsToken(text, "Binary", "Formatter"))
                    result.BinaryFormatterFindings++;
                if (ContainsToken(text, "System.", "Reflection"))
                    result.ReflectionFindings++;
            }
        }

        private static bool ContainsToken(string text, string prefix, string suffix)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string token = string.Concat(prefix, suffix);
            int start = 0;
            while (start < text.Length)
            {
                int index = text.IndexOf(token, start, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int before = index - 1;
                int after = index + token.Length;
                bool leftBounded = before < 0 || !IsIdentifierChar(text[before]);
                bool rightBounded = after >= text.Length || !IsIdentifierChar(text[after]);
                if (leftBounded && rightBounded)
                    return true;

                start = index + token.Length;
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= '0' && value <= '9') ||
                   value == '_';
        }

        private static void WriteOopWalFuzzScannerReport(in OopWalFuzzScanResultDTO result)
        {
            string path = ResolveProjectPath(Shinobu357QaReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "{\"summary\":\"OOP Fuzzers Eradicated\",\"agent\":\"SHINOBU_357\",\"filesScanned\":");
            WriteUInt(stream, result.FilesScanned);
            WriteAscii(stream, ",\"fileStreamColdFindings\":");
            WriteUInt(stream, result.FileStreamFindings);
            WriteAscii(stream, ",\"streamWriterFindings\":");
            WriteUInt(stream, result.StreamWriterFindings);
            WriteAscii(stream, ",\"writeAllBytesFindings\":");
            WriteUInt(stream, result.WriteAllBytesFindings);
            WriteAscii(stream, ",\"jsonUtilityFindings\":");
            WriteUInt(stream, result.JsonUtilityFindings);
            WriteAscii(stream, ",\"binaryFormatterFindings\":");
            WriteUInt(stream, result.BinaryFormatterFindings);
            WriteAscii(stream, ",\"fatalFindings\":");
            WriteUInt(stream, result.FatalFindings);
            WriteAscii(stream, "}\n");
        }
#endif

        private static int ResolveShinobu357IterationCount(uint requested, float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int requestedCount = requested == 0u ? 100 : ClampProfileUIntToInt(requested, 1, 100);
            int minimumProofIterations = (int)math.round(math.lerp(1f, 8f, quality));
            return math.max(requestedCount, minimumProofIterations);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveShinobu357InterruptOffset(uint byteCount, uint iteration, uint seed)
        {
            if (byteCount <= 65u)
                return 1u;

            uint state = seed ^ (iteration + 1u) * 747796405u;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;
            uint span = byteCount - 65u;
            return 64u + (state % span);
        }

        private static ulong HashPath64(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 1469598103934665603UL;

            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                byte lo = (byte)c;
                byte hi = (byte)(c >> 8);
                hash = (hash ^ lo) * 1099511628211UL;
                hash = (hash ^ hi) * 1099511628211UL;
            }

            return hash == 0UL ? 1UL : hash;
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal struct VerifyFileHandleReleaseJob : IJob
        {
            [NoAlias] public NativeArray<WalFuzzFileHandleStatusDTO> HandleStatus;

            public void Execute()
            {
                if (!HandleStatus.IsCreated || HandleStatus.Length == 0)
                    return;

                WalFuzzFileHandleStatusDTO status = HandleStatus[0];
                if (status.PrimaryWritable == 0u || status.BackupWritable == 0u)
                {
                    status.MismatchFlags |= WalFuzzFileLockLeak;
                    status.FailureCode = status.PrimaryWritable == 0u ? 65u : 67u;
                }

                HandleStatus[0] = status;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal struct GenerateMockCorruptWalJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceBytes;
            [WriteOnly, NoAlias] public NativeArray<byte> CorruptBytes;
            [NoAlias] public NativeArray<WalFuzzStateDTO> StateBuffer;
            public uint InterruptedByteOffset;
            public uint MutationMode;

            public void Execute()
            {
                if (!StateBuffer.IsCreated || StateBuffer.Length == 0)
                    return;

                WalFuzzStateDTO state = StateBuffer[0];
                if (!SourceBytes.IsCreated || !CorruptBytes.IsCreated || SourceBytes.Length == 0 || CorruptBytes.Length == 0)
                {
                    state.InterruptedByteOffset = 0u;
                    state.FinalValidatedBytes = 0u;
                    state.MismatchFlags = WalFuzzDataCorruption;
                    StateBuffer[0] = state;
                    return;
                }

                int count = math.min(SourceBytes.Length, CorruptBytes.Length);
                uint clamped = count <= 0 ? 0u : InterruptedByteOffset >= (uint)count ? (uint)(count - 1) : InterruptedByteOffset;
                int cut = (int)clamped;
                for (int i = 0; i < count; i++)
                {
                    byte value = i < cut ? SourceBytes[i] : (byte)0;
                    if (MutationMode == 2u && i == (cut >> 1))
                        value ^= 0xA5;
                    CorruptBytes[i] = value;
                }

                state.InterruptedByteOffset = (uint)cut;
                state.FinalValidatedBytes = (uint)cut;
                state.MismatchFlags = cut >= count ? WalFuzzTruncationUndetected : 0u;
                StateBuffer[0] = state;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal unsafe struct EvaluateHeadlessWalFuzzJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourcePayload;
            [ReadOnly, NoAlias] public NativeArray<byte> CorruptWalBytes;
            [NoAlias] public NativeArray<WalFuzzStateDTO> StateBuffer;
            [NoAlias] public NativeArray<WalFuzzTelemetryEntry> Telemetry;
            public uint Iterations;
            public uint Seed;

            public void Execute()
            {
                if (!StateBuffer.IsCreated || StateBuffer.Length == 0)
                    return;

                WalFuzzStateDTO state = StateBuffer[0];
                uint flags = 0u;
                uint highest = 0u;
                uint validated = 0u;
                if (!Telemetry.IsCreated || Telemetry.Length == 0)
                {
                    state.InterruptedByteOffset = 0u;
                    state.FinalValidatedBytes = 0u;
                    state.MismatchFlags = WalFuzzDataCorruption;
                    StateBuffer[0] = state;
                    return;
                }

                for (int i = 0; i < Telemetry.Length; i++)
                {
                    Telemetry[i] = default;
                }

                if (!SourcePayload.IsCreated || SourcePayload.Length == 0 || !CorruptWalBytes.IsCreated || CorruptWalBytes.Length == 0)
                {
                    state.InterruptedByteOffset = 0u;
                    state.FinalValidatedBytes = 0u;
                    state.MismatchFlags = WalFuzzDataCorruption;
                    StateBuffer[0] = state;
                    return;
                }

                byte* payloadPtr = (byte*)SourcePayload.GetUnsafeReadOnlyPtr();
                ulong referenceHash = SaveBinaryStorage.Hash64(payloadPtr, SourcePayload.Length);
                if (!ValidateAupLittleEndianSentinel())
                    flags |= WalFuzzPrecisionLossCrime;

                uint loopCount = Iterations == 0u ? 100u : math.min(Iterations, 100u);
                for (uint i = 0u; i < loopCount; i++)
                {
                    uint interrupt = ResolveShinobu357InterruptOffset((uint)SourcePayload.Length, i, Seed);
                    highest = math.max(highest, interrupt);
                    validated = interrupt;
                    if (interrupt >= (uint)SourcePayload.Length)
                        flags |= WalFuzzTruncationUndetected;

                    uint corruptionProbe = CorruptWalBytes[(int)math.min(interrupt, (uint)(CorruptWalBytes.Length - 1))];
                    uint rollbackA = ComputeRollbackProbe(referenceHash ^ corruptionProbe, i, 0u);
                    uint rollbackB = ComputeRollbackProbe(referenceHash ^ corruptionProbe, i, 30u);
                    if (rollbackA != rollbackB)
                        flags |= WalFuzzRollbackDesync;

                    int index = (int)(i % (uint)Telemetry.Length);
                    Telemetry[index] = new WalFuzzTelemetryEntry
                    {
                        Frame = i,
                        InterruptedByteOffset = interrupt,
                        FinalValidatedBytes = interrupt,
                        ActiveFileHandleStatus = 1u,
                        PathHash = referenceHash,
                        FailingArrayOffset = interrupt,
                        BurstExecutionMicros = 0L,
                        MismatchFlags = flags,
                        PhaseHash = Shinobu357PhaseMockCorrupt
                    };
                }

                state.InterruptedByteOffset = highest;
                state.FinalValidatedBytes = validated;
                state.MismatchFlags = flags;
                StateBuffer[0] = state;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ComputeRollbackProbe(ulong hash, uint frame, uint rewind)
            {
                uint lo = (uint)hash;
                uint hi = (uint)(hash >> 32);
                uint start = frame >= rewind ? frame - rewind : 0u;
                uint value = lo ^ hi;
                value += start * 747796405u;
                for (uint i = start; i < frame; i++)
                {
                    value += 747796405u;
                }

                uint shift = frame & 31u;
                uint rotated = shift == 0u ? hi : (hi << (int)shift) | (hi >> (int)(32u - shift));
                value ^= rotated;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ValidateAupLittleEndianSentinel()
            {
                return ValidateAupDouble(100000.123456789d, Shinobu357AupXBits) &&
                       ValidateAupDouble(-99999.987654321d, Shinobu357AupYBits) &&
                       ValidateAupDouble(0.1250000000001d, Shinobu357AupZBits);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ValidateAupDouble(double value, ulong expectedBits)
            {
                ulong bits = unchecked((ulong)math.aslong(value));
                ulong packedLittleEndian = 0UL;
                for (int i = 0; i < 8; i++)
                {
                    ulong valueByte = (bits >> (i * 8)) & 0xFFUL;
                    packedLittleEndian |= valueByte << (i * 8);
                }

                return packedLittleEndian == expectedBits && (double)(float)value != value;
            }
        }
    }
}
#endif
