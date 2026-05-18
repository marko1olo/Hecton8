using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Stable FNV-1a hashes for binary-only future mod command opcodes.
    /// </summary>
    public static class FutureCommandOpcodes
    {
        public const uint SpawnItem = 0x3A3DA9C4u;
        public const uint AlterHealth = 0xE75AADC0u;
        public const uint AlterGravity = 0x3B73D070u;
        public const uint AssetReference = 0xF7023ACDu;
        public const uint ModMemoryRead = 0xBBFBD0A6u;
        public const uint ModMemoryWrite = 0xE9C540EFu;
        public const uint FaunaAcousticStimulus = 0xCC5BAC8Du;
        public const uint FaunaDamageStimulus = 0x1B7770D3u;
        public const uint TriggerSubtitleCue = 0xBCEE082Au;
    }

    /// <summary>
    /// One 64-byte binary request from UGC. No managed references, properties, or pack=1.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = FutureCommandSandboxConstants.EnvelopeSizeBytes)]
    public struct FutureCommandEnvelope
    {
        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public double3 TargetAUP;
        [FieldOffset(32)] public float4 PayloadData;
        [FieldOffset(48)] public ulong IntegrityHash;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FutureCommandOpcodeRecord
    {
        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint ManifestCrc32;
        [FieldOffset(12)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FutureCommandSandboxTuning
    {
        [FieldOffset(0)] public int MaxCommandsPerFrame;
        [FieldOffset(4)] public int MaxModMemoryMb;
        [FieldOffset(8)] public float GlobalQualityWeightOverride;
        [FieldOffset(12)] public uint EnabledOpcodeMaskLo;
        [FieldOffset(16)] public uint MaxAssetBytes;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float CpuThermalPressure01;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ModderMemoryLease
    {
        [FieldOffset(0)] public uint ModderSignature;
        [FieldOffset(4)] public int OffsetBytes;
        [FieldOffset(8)] public int ByteLength;
        [FieldOffset(12)] public uint LastTouchedFrame;
        [FieldOffset(16)] public uint WrittenBytes;
        [FieldOffset(20)] public uint ReadRequests;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModderFrameCounter
    {
        [FieldOffset(0)] public uint ModderSignature;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public int Dropped;
        [FieldOffset(16)] public ulong Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
        [FieldOffset(32)] public ulong Reserved2;
        [FieldOffset(40)] public ulong Reserved3;
        [FieldOffset(48)] public ulong Reserved4;
        [FieldOffset(56)] public ulong Reserved5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ApprovedAssetRecord
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public uint Crc32;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModSandboxRingState
    {
        [FieldOffset(0)] public int PendingHead;
        [FieldOffset(4)] public int PendingTail;
        [FieldOffset(8)] public int PendingCount;
        [FieldOffset(12)] public int DevNullHead;
        [FieldOffset(16)] public int DevNullTail;
        [FieldOffset(20)] public int DevNullCount;
        [FieldOffset(24)] public int NextLeaseIndex;
        [FieldOffset(28)] public int OpcodeCount;
        [FieldOffset(32)] public int ApprovedAssetCount;
        [FieldOffset(36)] public int LastDumpFrame;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct RollbackRuntimeStateFlagView
    {
        [FieldOffset(44)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModSandboxTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Incoming;
        [FieldOffset(8)] public uint ValidCommandsProcessed;
        [FieldOffset(12)] public uint CommandsRejected;
        [FieldOffset(16)] public uint CommandsDroppedByBudget;
        [FieldOffset(20)] public uint DevNullCommands;
        [FieldOffset(24)] public ulong ValidatorComputeTimeNs;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint RejectionMask;
        [FieldOffset(40)] public uint FaultHash;
        [FieldOffset(44)] public uint PendingQueueDepth;
        [FieldOffset(48)] public uint PeakCommandsForSignature;
        [FieldOffset(52)] public uint MaxCommandsPerSignature;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModSpawnRequestSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public uint OpcodeHash;
        [FieldOffset(12)] public uint AssetHash;
        [FieldOffset(16)] public double3 TargetAUP;
        [FieldOffset(40)] public float4 PayloadData;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModAssetReferenceSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public uint AssetHash;
        [FieldOffset(12)] public uint Crc32;
        [FieldOffset(16)] public double3 TargetAUP;
        [FieldOffset(40)] public uint ByteLength;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong IntegrityHash;
        [FieldOffset(56)] public ulong Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockAcousticSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public uint SourceOpcode;
        [FieldOffset(12)] public float Intensity01;
        [FieldOffset(16)] public double3 TargetAUP;
        [FieldOffset(40)] public float RadiusMeters;
        [FieldOffset(44)] public uint StimulusHash;
        [FieldOffset(48)] public ulong IntegrityHash;
        [FieldOffset(56)] public ulong Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockDamageSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public uint SourceOpcode;
        [FieldOffset(12)] public float DamageAmount;
        [FieldOffset(16)] public double3 TargetAUP;
        [FieldOffset(40)] public float RadiusMeters;
        [FieldOffset(44)] public uint DamageTypeHash;
        [FieldOffset(48)] public ulong IntegrityHash;
        [FieldOffset(56)] public ulong Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModFutureDevNullSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public uint OpcodeHash;
        [FieldOffset(12)] public uint ReasonHash;
        [FieldOffset(16)] public double3 TargetAUP;
        [FieldOffset(40)] public float4 PayloadData;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved;
    }

    public static class FutureCommandSandboxConstants
    {
        public const int EnvelopeSizeBytes = 64;
        public const int PendingCapacity = 4096;
        public const int StagingCapacity = 4096;
        public const int MaxTrackedModders = 128;
        public const int ApprovedAssetCapacity = 512;
        public const int TelemetryCapacity = 300;
        public const int DefaultMaxCommandsPerSignature = 1000;
        public const int LowTierMinCommandsPerSignature = 10;
        public const int DefaultMaxModMemoryMb = 16;
        public const int DefaultMaxAssetBytes = 16 * 1024 * 1024;
        public const int OpcodeRecordCapacity = 32;
        public const int CsvScratchBytes = 16 * 1024;
        public const double MaxAupMagnitudeMeters = 50000.0d;
        public const uint DevNullReasonFutureSeam = 0x44564E4Cu;
        public const uint FaultHashInvalidAup = 0x414E414Eu;
        public const uint FaultHashMemoryViolation = 0x4D56494Fu;
        public const uint FaultHashLayout = 0x4C41594Fu;
    }

    internal enum FutureCommandRejectReason : uint
    {
        None = 0,
        QueueFull = 1u << 0,
        UnknownOpcode = 1u << 1,
        IntegrityMismatch = 1u << 2,
        InvalidAup = 1u << 3,
        CommandFlood = 1u << 4,
        MissingMemoryLease = 1u << 5,
        MemoryViolation = 1u << 6,
        AssetCrcMismatch = 1u << 7,
        AssetTooLarge = 1u << 8,
        RollbackFrozen = 1u << 9,
        LayoutInvalid = 1u << 10
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FutureCommandValidationStats
    {
        [FieldOffset(0)] public uint Incoming;
        [FieldOffset(4)] public uint Valid;
        [FieldOffset(8)] public uint Rejected;
        [FieldOffset(12)] public uint Dropped;
        [FieldOffset(16)] public uint DevNull;
        [FieldOffset(20)] public uint RejectionMask;
        [FieldOffset(24)] public uint FaultHash;
        [FieldOffset(28)] public uint PeakCommandsForSignature;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] public uint Reserved2;
        [FieldOffset(48)] public ulong Reserved3;
        [FieldOffset(56)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModSandboxScheduledValidationState
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PendingAfterDrain;
        [FieldOffset(8)] public uint MaxCommandsPerSignature;
        [FieldOffset(12)] public uint ThermalDropped;
        [FieldOffset(16)] public float Quality;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public long StartTicks;
        [FieldOffset(32)] public ulong Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    public partial struct MockModQueue : IDisposable
    {
        public NativeQueue<FutureCommandEnvelope> Queue;

        public static MockModQueue Wrap(ref NativeQueue<FutureCommandEnvelope> externalQueue)
        {
            MockModQueue queue = default;
            queue.Attach(ref externalQueue);
            return queue;
        }

        public bool GetIsCreated()
        {
            return Queue.IsCreated;
        }

        public bool Attach(ref NativeQueue<FutureCommandEnvelope> externalQueue)
        {
            if (!externalQueue.IsCreated)
                return false;

            Queue = externalQueue;
            return true;
        }

        public void Dispose()
        {
            Queue = default;
        }

        public NativeQueue<FutureCommandEnvelope>.ParallelWriter AsParallelWriter()
        {
            return Queue.IsCreated ? Queue.AsParallelWriter() : default;
        }
    }

    /// <summary>
    /// Binary-only mod quarantine. It never invokes mod C#; it validates math packets and emits DOD signals.
    /// </summary>
    public static unsafe class FutureCommandSandboxValidator
    {
        private const string DumpPath = "Docs/AgentLogs/Dump_QUARANTINE_SURGEON.bin";
        private const int DefaultMemoryBytes = FutureCommandSandboxConstants.DefaultMaxModMemoryMb * 1024 * 1024;
        private const uint EnabledAllEmergencyOpcodes = 0x1FFu;
        private const uint RollbackRuntimeStateBufferId = 70752u;
        private const uint RollbackFlagResimulating = 1u << 4;

        private static VaultBufferHandle<FutureCommandEnvelope> _pendingRingHandle;
        private static VaultBufferHandle<FutureCommandEnvelope> _devNullRingHandle;
        private static VaultBufferHandle<FutureCommandEnvelope> _stagingHandle;
        private static VaultBufferHandle<FutureCommandValidationStats> _statsHandle;
        private static VaultBufferHandle<FutureCommandOpcodeRecord> _opcodeRecordsHandle;
        private static VaultBufferHandle<ModSandboxTelemetryEntry> _telemetryRingHandle;
        private static VaultBufferHandle<int> _telemetryCursorHandle;
        private static VaultBufferHandle<byte> _modderBlackboxMemoryHandle;
        private static VaultBufferHandle<FutureCommandSandboxTuning> _tuningHandle;
        private static VaultBufferHandle<ModderFrameCounter> _perModCountersHandle;
        private static VaultBufferHandle<ModderMemoryLease> _memoryLeasesHandle;
        private static VaultBufferHandle<ApprovedAssetRecord> _approvedAssetManifestHandle;
        private static VaultBufferHandle<ModSandboxRingState> _ringStateHandle;
        private static JobHandle _scheduledValidationHandle;
        private static ModSandboxScheduledValidationState _scheduledValidationState;
        private static bool _scheduledValidationActive;
        private static bool _initialized;
        private static bool _rollbackFreezeOverride;

        public static int GetPendingEnvelopeCount()
        {
            NativeArray<ModSandboxRingState> state = ResolveRingState();
            return state.IsCreated && state.Length > 0 ? state[0].PendingCount : 0;
        }

        public static int GetDevNullEnvelopeCount()
        {
            NativeArray<ModSandboxRingState> state = ResolveRingState();
            return state.IsCreated && state.Length > 0 ? state[0].DevNullCount : 0;
        }

        public static bool GetIsInitialized()
        {
            return _initialized;
        }

        public static bool GetHasScheduledValidation()
        {
            return _scheduledValidationActive;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            ValidateLayoutOrDump();

            ConfigureSignalLanes();
            AcquireVaultBuffers();
            GenerateEmergencyMockOpcodes();

            _rollbackFreezeOverride = false;
            _initialized = true;
        }

        public static void Shutdown()
        {
            TryFinalizeScheduledPreSimulation(forceComplete: true);
            _pendingRingHandle = default;
            _devNullRingHandle = default;
            _stagingHandle = default;
            _statsHandle = default;
            _opcodeRecordsHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _modderBlackboxMemoryHandle = default;
            _tuningHandle = default;
            _perModCountersHandle = default;
            _memoryLeasesHandle = default;
            _approvedAssetManifestHandle = default;
            _ringStateHandle = default;
            _scheduledValidationHandle = default;
            _scheduledValidationState = default;
            _scheduledValidationActive = false;
            _initialized = false;
        }

        public static bool Request(in FutureCommandEnvelope envelope)
        {
            Initialize();
            AcquireVaultBuffers();
            NativeArray<FutureCommandEnvelope> pendingRing = ResolveBuffer(ref _pendingRingHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveBuffer(ref _ringStateHandle);
            if (!pendingRing.IsCreated || !ringState.IsCreated || ringState.Length == 0 || pendingRing.Length == 0)
                return false;

            ModSandboxRingState state = ringState[0];
            EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
            ringState[0] = state;
            return true;
        }

        public static int RequestRawEnvelopeStream(NativeArray<byte> bytes, int byteLength)
        {
            return RequestRawEnvelopeStream(bytes, byteLength, sourceBigEndian: false);
        }

        public static int RequestRawEnvelopeStream(NativeArray<byte> bytes, int byteLength, bool sourceBigEndian)
        {
            Initialize();
            if (!bytes.IsCreated || byteLength < FutureCommandSandboxConstants.EnvelopeSizeBytes)
                return 0;

            AcquireVaultBuffers();
            NativeArray<FutureCommandEnvelope> pendingRing = ResolveBuffer(ref _pendingRingHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveBuffer(ref _ringStateHandle);
            if (!pendingRing.IsCreated || !ringState.IsCreated || ringState.Length == 0 || pendingRing.Length == 0)
                return 0;

            int safeBytes = math.min(byteLength, bytes.Length);
            int count = safeBytes / FutureCommandSandboxConstants.EnvelopeSizeBytes;
            int accepted = 0;
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            ModSandboxRingState state = ringState[0];
            for (int i = 0; i < count; i++)
            {
                FutureCommandEnvelope envelope = ((FutureCommandEnvelope*)ptr)[i];
                if (sourceBigEndian)
                    envelope = SwapEnvelopeEndian(in envelope);

                EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
                accepted++;
            }

            ringState[0] = state;
            return accepted;
        }

        public static int RequestFromExternalQueue(ref NativeQueue<FutureCommandEnvelope> sourceQueue, int maxEnvelopeCount)
        {
            Initialize();
            if (!sourceQueue.IsCreated || maxEnvelopeCount <= 0)
                return 0;

            AcquireVaultBuffers();
            NativeArray<FutureCommandEnvelope> pendingRing = ResolveBuffer(ref _pendingRingHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveBuffer(ref _ringStateHandle);
            if (!pendingRing.IsCreated || !ringState.IsCreated || ringState.Length == 0 || pendingRing.Length == 0)
                return 0;

            ModSandboxRingState state = ringState[0];
            int accepted = 0;
            int limit = math.min(maxEnvelopeCount, pendingRing.Length);
            while (accepted < limit && sourceQueue.TryDequeue(out FutureCommandEnvelope envelope))
            {
                EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
                accepted++;
            }

            ringState[0] = state;
            return accepted;
        }

        public static void DrainPreSimulation()
        {
            TryFinalizeScheduledPreSimulation(forceComplete: false);
            if (_scheduledValidationActive)
                return;

            if (!TryPrepareValidationJob(out ValidateFutureCommandEnvelopeJob job, out ModSandboxScheduledValidationState validationState, recordNoWorkTelemetry: true))
                return;

            job.Run();
            FinalizeValidationTelemetry(in validationState);
        }

        public static bool TrySchedulePreSimulation(JobHandle dependsOn, out JobHandle validationHandle)
        {
            validationHandle = dependsOn;
            TryFinalizeScheduledPreSimulation(forceComplete: false);
            if (_scheduledValidationActive)
                return false;

            if (!TryPrepareValidationJob(out ValidateFutureCommandEnvelopeJob job, out ModSandboxScheduledValidationState validationState, recordNoWorkTelemetry: true))
                return false;

            validationHandle = job.Schedule(dependsOn);
            _scheduledValidationHandle = validationHandle;
            _scheduledValidationState = validationState;
            _scheduledValidationActive = true;
            H8Memory.RegisterActiveJob(SystemID.ModSandbox, validationHandle);
            return true;
        }

        public static bool TryFinalizeScheduledPreSimulation(bool forceComplete)
        {
            if (!_scheduledValidationActive)
                return true;

            if (!forceComplete && !_scheduledValidationHandle.IsCompleted)
                return false;

            _scheduledValidationHandle.Complete();
            FinalizeValidationTelemetry(in _scheduledValidationState);
            _scheduledValidationHandle = default;
            _scheduledValidationState = default;
            _scheduledValidationActive = false;
            return true;
        }

        public static void DrainLateFrame()
        {
            if (!_initialized)
                return;

            TryFinalizeScheduledPreSimulation(forceComplete: false);
            if (_scheduledValidationActive)
                return;

            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (!ringState.IsCreated || ringState.Length == 0)
                return;

            ModSandboxRingState state = ringState[0];
            state.DevNullHead = state.DevNullTail;
            state.DevNullCount = 0;
            ringState[0] = state;
        }

        public static bool RegisterApprovedAsset(uint assetHash, uint crc32)
        {
            return RegisterApprovedAsset(assetHash, crc32, 0u);
        }

        public static bool RegisterApprovedAsset(uint assetHash, uint crc32, uint byteLength)
        {
            Initialize();
            NativeArray<ApprovedAssetRecord> approvedAssets = ResolveBuffer(ref _approvedAssetManifestHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (assetHash == 0u || crc32 == 0u || !approvedAssets.IsCreated || !ringState.IsCreated || ringState.Length == 0)
                return false;

            int slot = FindApprovedAssetSlot(approvedAssets, assetHash, out bool found);
            if (slot < 0)
                return false;

            approvedAssets[slot] = new ApprovedAssetRecord
            {
                AssetHash = assetHash,
                Crc32 = crc32,
                ByteLength = byteLength,
                Flags = 1u
            };

            if (!found)
            {
                ModSandboxRingState state = ringState[0];
                state.ApprovedAssetCount = math.min(approvedAssets.Length, state.ApprovedAssetCount + 1);
                ringState[0] = state;
            }

            return true;
        }

        public static bool RegisterApprovedAsset(uint assetHash, NativeArray<byte> assetBytes, int byteLength)
        {
            if (!assetBytes.IsCreated || byteLength <= 0)
                return false;

            int safeLength = math.min(byteLength, assetBytes.Length);
            return RegisterApprovedAsset(assetHash, ComputeCrc32(assetBytes, safeLength), (uint)safeLength);
        }

        public static uint ComputeCrc32(NativeArray<byte> bytes, int byteLength)
        {
            if (!bytes.IsCreated || byteLength <= 0)
                return 0u;

            int count = math.min(byteLength, bytes.Length);
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++)
            {
                crc ^= bytes[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = (uint)-(int)(crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return ~crc;
        }

        public static bool SetOpcodeEnabled(uint opcodeHash, bool enabled)
        {
            Initialize();
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (opcodeHash == 0u || !opcodeRecords.IsCreated || !ringState.IsCreated || ringState.Length == 0)
                return false;

            ModSandboxRingState state = ringState[0];
            for (int i = 0; i < opcodeRecords.Length; i++)
            {
                FutureCommandOpcodeRecord record = opcodeRecords[i];
                if (record.OpcodeHash != opcodeHash)
                    continue;

                record.Flags = enabled ? 1u : 0u;
                opcodeRecords[i] = record;
                return true;
            }

            if (!enabled)
                return true;

            int slot = state.OpcodeCount;
            if ((uint)slot >= (uint)opcodeRecords.Length)
                return false;

            opcodeRecords[slot] = new FutureCommandOpcodeRecord
            {
                OpcodeHash = opcodeHash,
                Flags = 1u,
                ManifestCrc32 = 0u,
                Reserved = 0u
            };
            state.OpcodeCount = slot + 1;
            ringState[0] = state;
            return true;
        }

        public static bool IsOpcodeEnabled(uint opcodeHash)
        {
            Initialize();
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (opcodeHash == 0u || !opcodeRecords.IsCreated || !ringState.IsCreated || ringState.Length == 0)
                return false;

            int count = math.min(ringState[0].OpcodeCount, opcodeRecords.Length);
            for (int i = 0; i < count; i++)
            {
                FutureCommandOpcodeRecord record = opcodeRecords[i];
                if (record.OpcodeHash == opcodeHash && (record.Flags & 1u) != 0u)
                    return true;
            }

            return false;
        }

        public static FutureCommandSandboxTuning GetTuningSnapshot()
        {
            Initialize();
            return ResolveTuning(ResolveGlobalQualityWeight());
        }

        public static void ApplyTuning(in FutureCommandSandboxTuning tuning)
        {
            Initialize();
            AcquireVaultBuffers();
            NativeArray<FutureCommandSandboxTuning> tuningBuffer = ResolveBuffer(ref _tuningHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            FutureCommandSandboxTuning safe = tuning;
            safe.MaxCommandsPerFrame = math.clamp(safe.MaxCommandsPerFrame, FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, 10000);
            safe.MaxModMemoryMb = math.clamp(safe.MaxModMemoryMb, 1, 256);
            safe.MaxAssetBytes = (uint)math.clamp((int)safe.MaxAssetBytes, 1024, 256 * 1024 * 1024);
            safe.CpuThermalPressure01 = math.saturate(math.isfinite(safe.CpuThermalPressure01) ? safe.CpuThermalPressure01 : 0f);
            tuningBuffer[0] = safe;
        }

        public static bool ReportCpuThermalPressure(float pressure01)
        {
            Initialize();
            AcquireVaultBuffers();
            NativeArray<FutureCommandSandboxTuning> tuningBuffer = ResolveBuffer(ref _tuningHandle);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return false;

            FutureCommandSandboxTuning tuning = tuningBuffer[0];
            tuning.CpuThermalPressure01 = math.saturate(math.isfinite(pressure01) ? pressure01 : 1f);
            tuningBuffer[0] = tuning;
            return true;
        }

        public static int CopyTelemetrySnapshot(NativeArray<ModSandboxTelemetryEntry> destination)
        {
            Initialize();
            AcquireVaultBuffers();
            NativeArray<ModSandboxTelemetryEntry> telemetryRing = ResolveBuffer(ref _telemetryRingHandle);
            if (!destination.IsCreated || !telemetryRing.IsCreated)
                return 0;

            int count = math.min(destination.Length, telemetryRing.Length);
            for (int i = 0; i < count; i++)
                destination[i] = telemetryRing[i];
            return count;
        }

        public static bool TryGetTelemetryEntry(int index, out ModSandboxTelemetryEntry entry)
        {
            Initialize();
            AcquireVaultBuffers();
            NativeArray<ModSandboxTelemetryEntry> telemetryRing = ResolveBuffer(ref _telemetryRingHandle);
            if (!telemetryRing.IsCreated || (uint)index >= (uint)telemetryRing.Length)
            {
                entry = default;
                return false;
            }

            entry = telemetryRing[index];
            return true;
        }

        public static bool TryIngestAllowedOpcodesCsv(NativeArray<byte> csvBytes, int byteLength)
        {
            Initialize();
            if (!csvBytes.IsCreated || byteLength <= 0)
                return false;

            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (!opcodeRecords.IsCreated || !ringState.IsCreated || ringState.Length == 0)
                return false;

            int length = math.min(byteLength, csvBytes.Length);
            MemClearArray(opcodeRecords);
            ModSandboxRingState state = ringState[0];
            state.OpcodeCount = 0;
            int accepted = 0;
            int tokenStart = 0;
            for (int cursor = 0; cursor <= length; cursor++)
            {
                byte b = cursor < length ? csvBytes[cursor] : (byte)'\n';
                if (b != (byte)'\n' && b != (byte)'\r')
                    continue;

                if (TryParseOpcodeCsvLine(csvBytes, tokenStart, cursor - tokenStart, out uint opcodeHash) &&
                    opcodeHash != 0u &&
                    AddOpcodeRecord(opcodeRecords, ref state, opcodeHash, 1u))
                {
                    accepted++;
                }

                tokenStart = cursor + 1;
            }

            if (accepted == 0)
                GenerateEmergencyMockOpcodes();
            else
                ringState[0] = state;

            return accepted > 0;
        }

#if UNITY_EDITOR
        public static bool TryReloadAllowedOpcodesCsvFromDisk()
        {
            string path = Path.Combine(Application.dataPath, "../Docs/Modding/allowed_opcodes.csv");
            if (!File.Exists(path))
                return false;

            byte[] managedBytes = File.ReadAllBytes(path);
            if (managedBytes.Length == 0)
                return false;

            NativeArray<byte> nativeBytes = new NativeArray<byte>(managedBytes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            nativeBytes.CopyFrom(managedBytes);
            bool result = TryIngestAllowedOpcodesCsv(nativeBytes, nativeBytes.Length);
            nativeBytes.Dispose();
            return result;
        }
#endif

        public static void SetRollbackResimulationActive(bool active)
        {
            _rollbackFreezeOverride = active;
        }

        public static bool RunSelfAudit()
        {
            Initialize();
            if (UnsafeUtility.SizeOf<FutureCommandEnvelope>() != FutureCommandSandboxConstants.EnvelopeSizeBytes)
            {
                DumpBlackbox(FutureCommandSandboxConstants.FaultHashLayout);
                return false;
            }

            TryFinalizeScheduledPreSimulation(forceComplete: false);
            if (_scheduledValidationActive)
                return false;

            AcquireVaultBuffers();
            NativeArray<FutureCommandEnvelope> staging = ResolveBuffer(ref _stagingHandle);
            NativeArray<FutureCommandValidationStats> statsBuffer = ResolveBuffer(ref _statsHandle);
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModderFrameCounter> perModCounters = ResolveBuffer(ref _perModCountersHandle);
            NativeArray<ModderMemoryLease> memoryLeases = ResolveBuffer(ref _memoryLeasesHandle);
            NativeArray<ApprovedAssetRecord> approvedAssets = ResolveBuffer(ref _approvedAssetManifestHandle);
            NativeArray<byte> modderBlackboxMemory = ResolveBuffer(ref _modderBlackboxMemoryHandle);
            NativeArray<FutureCommandEnvelope> devNullRing = ResolveBuffer(ref _devNullRingHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (!staging.IsCreated ||
                staging.Length == 0 ||
                !statsBuffer.IsCreated ||
                statsBuffer.Length == 0 ||
                !opcodeRecords.IsCreated ||
                !perModCounters.IsCreated ||
                !memoryLeases.IsCreated ||
                !approvedAssets.IsCreated ||
                !modderBlackboxMemory.IsCreated ||
                !devNullRing.IsCreated ||
                !ringState.IsCreated ||
                ringState.Length == 0)
            {
                return false;
            }

            FutureCommandEnvelope malicious = default;
            malicious.OpcodeHash = FutureCommandOpcodes.SpawnItem;
            malicious.ModderSignature = 0x51554152u;
            malicious.TargetAUP = new double3(double.NaN, 0d, 0d);
            malicious.PayloadData = new float4(1f, 2f, 3f, 4f);
            malicious.IntegrityHash = ComputeIntegrityHash(in malicious);

            float quality = ResolveGlobalQualityWeight();
            FutureCommandSandboxTuning tuning = ResolveTuning(quality);
            int maxPerSignature = ResolveScaledCommandBudget(tuning.MaxCommandsPerFrame, quality);
            ModSandboxRingState state = ringState[0];
            MemClearArray(statsBuffer);
            staging[0] = malicious;

            ValidateFutureCommandEnvelopeJob job = new ValidateFutureCommandEnvelopeJob
            {
                Inputs = staging,
                Stats = statsBuffer,
                OpcodeRecords = opcodeRecords,
                PerModCounters = perModCounters,
                MemoryLeases = memoryLeases,
                ApprovedAssetManifest = approvedAssets,
                ModderBlackboxMemory = modderBlackboxMemory,
                DevNullRing = devNullRing,
                RingState = ringState,
                SpawnWriter = SignalBus<ModSpawnRequestSignal>.ParallelWriter,
                AssetWriter = SignalBus<ModAssetReferenceSignal>.ParallelWriter,
                AcousticWriter = SignalBus<MockAcousticSignal>.ParallelWriter,
                DamageWriter = SignalBus<MockDamageSignal>.ParallelWriter,
                DevNullSignalWriter = SignalBus<ModFutureDevNullSignal>.ParallelWriter,
                Count = 1,
                Frame = (uint)Time.frameCount,
                OpcodeRecordCount = state.OpcodeCount,
                MaxCommandsPerSignature = maxPerSignature,
                GlobalQualityWeight = quality,
                MaxAssetBytes = tuning.MaxAssetBytes
            };

            job.Run();
            FutureCommandValidationStats stats = statsBuffer[0];
            bool rejectedInvalidAup =
                stats.Incoming == 1u &&
                stats.Valid == 0u &&
                stats.Rejected == 1u &&
                (stats.RejectionMask & (uint)FutureCommandRejectReason.InvalidAup) != 0u;
            RecordTelemetry(
                (uint)Time.frameCount,
                stats.Incoming,
                stats.Valid,
                stats.Rejected,
                stats.Dropped,
                stats.DevNull,
                0UL,
                quality,
                stats.RejectionMask,
                rejectedInvalidAup ? 0u : FutureCommandSandboxConstants.FaultHashInvalidAup,
                (uint)state.PendingCount,
                stats.PeakCommandsForSignature,
                (uint)maxPerSignature);

            if (!rejectedInvalidAup)
                DumpBlackbox(FutureCommandSandboxConstants.FaultHashInvalidAup);
            return rejectedInvalidAup;
        }

        public static ulong ComputeIntegrityHash(in FutureCommandEnvelope envelope)
        {
            FutureCommandEnvelope copy = envelope;
            copy.IntegrityHash = 0UL;
            copy._pad0 = 0UL;
            uint2 hash = xxHash3.Hash64(&copy, 48L);
            return ((ulong)hash.y << 32) | hash.x;
        }

        private static bool TryParseOpcodeCsvLine(NativeArray<byte> bytes, int start, int length, out uint opcodeHash)
        {
            opcodeHash = 0u;
            if (length <= 0)
                return false;

            int end = start + length;
            while (start < end && IsWhitespace(bytes[start]))
                start++;
            while (end > start && IsWhitespace(bytes[end - 1]))
                end--;
            if (start >= end || bytes[start] == (byte)'#')
                return false;

            int tokenEnd = start;
            while (tokenEnd < end && bytes[tokenEnd] != (byte)',' && !IsWhitespace(bytes[tokenEnd]))
                tokenEnd++;

            if (tokenEnd <= start)
                return false;

            if (tokenEnd - start > 2 && bytes[start] == (byte)'0' && (bytes[start + 1] == (byte)'x' || bytes[start + 1] == (byte)'X'))
                return TryParseHex32(bytes, start + 2, tokenEnd - start - 2, out opcodeHash);

            opcodeHash = ComputeFnv1A32(bytes, start, tokenEnd - start);
            return opcodeHash != 0u;
        }

        private static bool TryParseHex32(NativeArray<byte> bytes, int start, int length, out uint value)
        {
            value = 0u;
            if (length <= 0 || length > 8)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte b = bytes[start + i];
                uint nibble;
                if (b >= (byte)'0' && b <= (byte)'9')
                    nibble = (uint)(b - (byte)'0');
                else if (b >= (byte)'a' && b <= (byte)'f')
                    nibble = (uint)(b - (byte)'a' + 10);
                else if (b >= (byte)'A' && b <= (byte)'F')
                    nibble = (uint)(b - (byte)'A' + 10);
                else
                    return false;

                value = (value << 4) | nibble;
            }

            return value != 0u;
        }

        private static uint ComputeFnv1A32(NativeArray<byte> bytes, int start, int length)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
            {
                byte b = bytes[start + i];
                if (b >= (byte)'a' && b <= (byte)'z')
                    b = (byte)(b - 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static bool IsWhitespace(byte b)
        {
            return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
        }

        private static void GenerateEmergencyMockOpcodes()
        {
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (!opcodeRecords.IsCreated || !ringState.IsCreated || ringState.Length == 0)
                return;

            MemClearArray(opcodeRecords);
            ModSandboxRingState state = ringState[0];
            state.OpcodeCount = 0;
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.SpawnItem, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AlterHealth, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AlterGravity, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AssetReference, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.ModMemoryRead, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.ModMemoryWrite, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.FaunaAcousticStimulus, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.FaunaDamageStimulus, 1u);
            AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.TriggerSubtitleCue, 1u);
            ringState[0] = state;
        }

        private static void AddEmergencyOpcode(NativeArray<FutureCommandOpcodeRecord> opcodeRecords, ref ModSandboxRingState state, uint opcodeHash, uint flags)
        {
            AddOpcodeRecord(opcodeRecords, ref state, opcodeHash, flags);
        }

        private static bool AddOpcodeRecord(NativeArray<FutureCommandOpcodeRecord> opcodeRecords, ref ModSandboxRingState state, uint opcodeHash, uint flags)
        {
            if (!opcodeRecords.IsCreated || opcodeHash == 0u)
                return false;

            int count = math.min(state.OpcodeCount, opcodeRecords.Length);
            for (int i = 0; i < count; i++)
            {
                FutureCommandOpcodeRecord existing = opcodeRecords[i];
                if (existing.OpcodeHash != opcodeHash)
                    continue;

                existing.Flags = flags;
                opcodeRecords[i] = existing;
                return false;
            }

            if (count >= opcodeRecords.Length)
                return false;

            opcodeRecords[count] = new FutureCommandOpcodeRecord
            {
                OpcodeHash = opcodeHash,
                Flags = flags,
                ManifestCrc32 = 0u,
                Reserved = 0u
            };
            state.OpcodeCount = count + 1;
            return true;
        }

        private static void EnsureModderLease(
            uint signature,
            int maxMemoryMb,
            NativeArray<byte> modderBlackboxMemory,
            NativeArray<ModderMemoryLease> memoryLeases,
            ref ModSandboxRingState state,
            uint frame)
        {
            if (signature == 0u || !memoryLeases.IsCreated || !modderBlackboxMemory.IsCreated)
                return;

            int slot = FindModderLeaseSlot(memoryLeases, signature, out bool found);
            if (slot < 0 || found)
                return;

            int memoryBytes = modderBlackboxMemory.Length;
            int maxMb = math.clamp(maxMemoryMb, 1, 256);
            int requestedBytes = math.min(memoryBytes, maxMb * 1024 * 1024);
            int chunkBytes = math.max(1024, requestedBytes / FutureCommandSandboxConstants.MaxTrackedModders);
            int offset = state.NextLeaseIndex * chunkBytes;
            if (offset < 0 || offset > memoryBytes - chunkBytes)
                return;

            memoryLeases[slot] = new ModderMemoryLease
            {
                ModderSignature = signature,
                OffsetBytes = offset,
                ByteLength = chunkBytes,
                LastTouchedFrame = frame,
                WrittenBytes = 0u,
                ReadRequests = 0u,
                Flags = 1u,
                Reserved = 0u
            };
            state.NextLeaseIndex = math.min(memoryLeases.Length, state.NextLeaseIndex + 1);
        }

        private static FutureCommandSandboxTuning ResolveTuning(float quality)
        {
            FutureCommandSandboxTuning tuning = default;
            tuning.MaxCommandsPerFrame = FutureCommandSandboxConstants.DefaultMaxCommandsPerSignature;
            tuning.MaxModMemoryMb = FutureCommandSandboxConstants.DefaultMaxModMemoryMb;
            tuning.GlobalQualityWeightOverride = -1f;
            tuning.EnabledOpcodeMaskLo = EnabledAllEmergencyOpcodes;
            tuning.MaxAssetBytes = FutureCommandSandboxConstants.DefaultMaxAssetBytes;
            tuning.Flags = 0u;
            tuning.CpuThermalPressure01 = 0f;
            tuning.Reserved = 0u;

            NativeArray<FutureCommandSandboxTuning> tuningBuffer = ResolveBuffer(ref _tuningHandle);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                FutureCommandSandboxTuning stored = tuningBuffer[0];
                if (stored.MaxCommandsPerFrame > 0)
                    tuning.MaxCommandsPerFrame = stored.MaxCommandsPerFrame;
                if (stored.MaxModMemoryMb > 0)
                    tuning.MaxModMemoryMb = stored.MaxModMemoryMb;
                if (stored.MaxAssetBytes > 0u)
                    tuning.MaxAssetBytes = stored.MaxAssetBytes;
                tuning.GlobalQualityWeightOverride = stored.GlobalQualityWeightOverride;
                tuning.EnabledOpcodeMaskLo = stored.EnabledOpcodeMaskLo == 0u ? EnabledAllEmergencyOpcodes : stored.EnabledOpcodeMaskLo;
                tuning.Flags = stored.Flags;
                tuning.CpuThermalPressure01 = math.saturate(math.isfinite(stored.CpuThermalPressure01) ? stored.CpuThermalPressure01 : 0f);
            }

            return tuning;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            NativeArray<FutureCommandSandboxTuning> tuningBuffer = ResolveBuffer(ref _tuningHandle);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                float overrideWeight = tuningBuffer[0].GlobalQualityWeightOverride;
                if (math.isfinite(overrideWeight) && overrideWeight >= 0f)
                    weight = overrideWeight;

                float pressure = math.saturate(math.isfinite(tuningBuffer[0].CpuThermalPressure01)
                    ? tuningBuffer[0].CpuThermalPressure01
                    : 1f);
                float pressureCurve = pressure * pressure * (3f - 2f * pressure);
                weight = math.lerp(weight, 0f, pressureCurve);
            }

            return math.saturate(math.isfinite(weight) ? weight : 0f);
        }

        private static int ResolveScaledCommandBudget(int baseMax, float quality)
        {
            int safeBase = math.clamp(baseMax, FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, 10000);
            float q = math.saturate(quality);
            float scaled = math.lerp(FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, safeBase, q);
            return math.max(FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, (int)math.round(scaled));
        }

        private static bool IsRollbackFrozen()
        {
            if (_rollbackFreezeOverride)
                return true;

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) ||
                !vault.TryGetBuffer((BufferID)RollbackRuntimeStateBufferId, out NativeArray<RollbackRuntimeStateFlagView> rollback) ||
                !rollback.IsCreated ||
                rollback.Length <= 0)
            {
                return false;
            }

            return (rollback[0].Flags & RollbackFlagResimulating) != 0u;
        }

        private static void AcquireVaultBuffers()
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            bool coldAcquire = !_ringStateHandle.IsCreated;

            _pendingRingHandle = vault.GetBufferHandle<FutureCommandEnvelope>(
                BufferID.ShinobuModSandboxPendingRing,
                FutureCommandSandboxConstants.PendingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _devNullRingHandle = vault.GetBufferHandle<FutureCommandEnvelope>(
                BufferID.ShinobuModSandboxDevNullRing,
                FutureCommandSandboxConstants.PendingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _stagingHandle = vault.GetBufferHandle<FutureCommandEnvelope>(
                BufferID.ShinobuModSandboxStaging,
                FutureCommandSandboxConstants.StagingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _statsHandle = vault.GetBufferHandle<FutureCommandValidationStats>(
                BufferID.ShinobuModSandboxStats,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _opcodeRecordsHandle = vault.GetBufferHandle<FutureCommandOpcodeRecord>(
                BufferID.ShinobuModSandboxOpcodeRecords,
                FutureCommandSandboxConstants.OpcodeRecordCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _perModCountersHandle = vault.GetBufferHandle<ModderFrameCounter>(
                BufferID.ShinobuModSandboxModCounters,
                FutureCommandSandboxConstants.MaxTrackedModders,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _memoryLeasesHandle = vault.GetBufferHandle<ModderMemoryLease>(
                BufferID.ShinobuModSandboxMemoryLeases,
                FutureCommandSandboxConstants.MaxTrackedModders,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _approvedAssetManifestHandle = vault.GetBufferHandle<ApprovedAssetRecord>(
                BufferID.ShinobuModSandboxApprovedAssets,
                FutureCommandSandboxConstants.ApprovedAssetCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _modderBlackboxMemoryHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuModSandboxBlackboxMemory,
                DefaultMemoryBytes,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.GetBufferHandle<ModSandboxTelemetryEntry>(
                BufferID.ShinobuModSandboxTelemetryRing,
                FutureCommandSandboxConstants.TelemetryCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuModSandboxTelemetryCursor,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<FutureCommandSandboxTuning>(
                BufferID.ShinobuModSandboxTuning,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _ringStateHandle = vault.GetBufferHandle<ModSandboxRingState>(
                BufferID.ShinobuModSandboxRingState,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);

            if (!coldAcquire)
                return;

            NativeArray<FutureCommandEnvelope> pendingRing = _pendingRingHandle.Resolve(vault);
            NativeArray<FutureCommandEnvelope> devNullRing = _devNullRingHandle.Resolve(vault);
            NativeArray<FutureCommandEnvelope> staging = _stagingHandle.Resolve(vault);
            NativeArray<FutureCommandValidationStats> stats = _statsHandle.Resolve(vault);
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = _opcodeRecordsHandle.Resolve(vault);
            NativeArray<ModderFrameCounter> counters = _perModCountersHandle.Resolve(vault);
            NativeArray<ModderMemoryLease> leases = _memoryLeasesHandle.Resolve(vault);
            NativeArray<ApprovedAssetRecord> approvedAssets = _approvedAssetManifestHandle.Resolve(vault);
            NativeArray<byte> modderBlackboxMemory = _modderBlackboxMemoryHandle.Resolve(vault);
            NativeArray<ModSandboxTelemetryEntry> telemetryRing = _telemetryRingHandle.Resolve(vault);
            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(vault);
            NativeArray<FutureCommandSandboxTuning> tuning = _tuningHandle.Resolve(vault);
            NativeArray<ModSandboxRingState> ringState = _ringStateHandle.Resolve(vault);

            MemClearArray(pendingRing);
            MemClearArray(devNullRing);
            MemClearArray(staging);
            MemClearArray(stats);
            MemClearArray(opcodeRecords);
            MemClearArray(counters);
            MemClearArray(leases);
            MemClearArray(approvedAssets);
            MemClearArray(modderBlackboxMemory);
            MemClearArray(telemetryRing);
            MemClearArray(telemetryCursor);
            MemClearArray(ringState);

            if (ringState.IsCreated && ringState.Length > 0)
            {
                ModSandboxRingState state = default;
                state.LastDumpFrame = -1;
                ringState[0] = state;
            }

            if (tuning.IsCreated && tuning.Length > 0)
            {
                tuning[0] = new FutureCommandSandboxTuning
                {
                    MaxCommandsPerFrame = FutureCommandSandboxConstants.DefaultMaxCommandsPerSignature,
                    MaxModMemoryMb = FutureCommandSandboxConstants.DefaultMaxModMemoryMb,
                    GlobalQualityWeightOverride = -1f,
                    EnabledOpcodeMaskLo = EnabledAllEmergencyOpcodes,
                    MaxAssetBytes = FutureCommandSandboxConstants.DefaultMaxAssetBytes,
                    Flags = 0u,
                    CpuThermalPressure01 = 0f,
                    Reserved = 0u
                };
            }
        }

        private static void RecordTelemetry(
            uint frame,
            uint incoming,
            uint valid,
            uint rejected,
            uint dropped,
            uint devNull,
            ulong computeNs,
            float quality,
            uint rejectionMask,
            uint faultHash,
            uint pendingDepth,
            uint peakCommandsForSignature,
            uint maxCommandsPerSignature)
        {
            NativeArray<ModSandboxTelemetryEntry> telemetryRing = ResolveBuffer(ref _telemetryRingHandle);
            NativeArray<int> telemetryCursor = ResolveBuffer(ref _telemetryCursorHandle);
            if (!telemetryRing.IsCreated || !telemetryCursor.IsCreated || telemetryRing.Length == 0 || telemetryCursor.Length == 0)
                return;

            int cursor = telemetryCursor[0];
            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            telemetryRing[cursor] = new ModSandboxTelemetryEntry
            {
                Frame = frame,
                Incoming = incoming,
                ValidCommandsProcessed = valid,
                CommandsRejected = rejected,
                CommandsDroppedByBudget = dropped,
                DevNullCommands = devNull,
                ValidatorComputeTimeNs = computeNs,
                GlobalQualityWeight = quality,
                RejectionMask = rejectionMask,
                FaultHash = faultHash,
                PendingQueueDepth = pendingDepth,
                PeakCommandsForSignature = peakCommandsForSignature,
                MaxCommandsPerSignature = maxCommandsPerSignature,
                Flags = 0u,
                Reserved = 0u
            };

            cursor++;
            if (cursor >= telemetryRing.Length)
                cursor = 0;
            telemetryCursor[0] = cursor;
        }

        public static void DumpBlackbox(uint faultHash)
        {
            int frame = Time.frameCount;
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            ModSandboxRingState state = ringState.IsCreated && ringState.Length > 0 ? ringState[0] : default;
            if (state.LastDumpFrame == frame)
                return;

            state.LastDumpFrame = frame;
            if (ringState.IsCreated && ringState.Length > 0)
                ringState[0] = state;

            NativeArray<ModSandboxTelemetryEntry> telemetryRing = ResolveBuffer(ref _telemetryRingHandle);
            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x514D4F44u);
                    writer.Write((uint)frame);
                    writer.Write(faultHash);
                    writer.Write(telemetryRing.IsCreated ? (uint)telemetryRing.Length : 0u);
                    writer.Write((uint)state.PendingCount);
                    writer.Write((uint)state.DevNullCount);
                    writer.Write(0UL);

                    if (!telemetryRing.IsCreated)
                        return;

                    int byteLength = telemetryRing.Length * UnsafeUtility.SizeOf<ModSandboxTelemetryEntry>();
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    for (int i = 0; i < byteLength; i++)
                        writer.Write(ptr[i]);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ValidateLayoutOrDump()
        {
            if (UnsafeUtility.SizeOf<FutureCommandEnvelope>() == FutureCommandSandboxConstants.EnvelopeSizeBytes &&
                UnsafeUtility.SizeOf<ModSandboxTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ModSpawnRequestSignal>() == 64 &&
                UnsafeUtility.SizeOf<FutureCommandValidationStats>() == 64 &&
                UnsafeUtility.SizeOf<ModderFrameCounter>() == 64 &&
                UnsafeUtility.SizeOf<ModSandboxRingState>() == 64)
            {
                return;
            }

            DumpBlackbox(FutureCommandSandboxConstants.FaultHashLayout);
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<ModSpawnRequestSignal>.Configure(256, maxFrameSignals: 512, lowTierFrameSignals: 32, laneHash: 0x4D535057u);
            SignalBus<ModSpawnRequestSignal>.EnsureInitialized();
            SignalBus<ModAssetReferenceSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 16, laneHash: 0x4D415354u);
            SignalBus<ModAssetReferenceSignal>.EnsureInitialized();
            SignalBus<MockAcousticSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 16, laneHash: 0x4D414353u);
            SignalBus<MockAcousticSignal>.EnsureInitialized();
            SignalBus<MockDamageSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 16, laneHash: 0x4D444D47u);
            SignalBus<MockDamageSignal>.EnsureInitialized();
            SignalBus<ModFutureDevNullSignal>.Configure(256, maxFrameSignals: 512, lowTierFrameSignals: 32, laneHash: 0x4D444E4Cu);
            SignalBus<ModFutureDevNullSignal>.EnsureInitialized();
        }

        private static NativeArray<T> ResolveBuffer<T>(ref VaultBufferHandle<T> handle) where T : struct
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return default;

            return handle.Resolve(vault);
        }

        private static NativeArray<ModSandboxRingState> ResolveRingState()
        {
            return ResolveBuffer(ref _ringStateHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AdvanceRingIndex(int index, int capacity)
        {
            if (capacity <= 1)
                return 0;

            index++;
            return index >= capacity ? 0 : index;
        }

        private static void EnqueuePendingEnvelope(NativeArray<FutureCommandEnvelope> pendingRing, ref ModSandboxRingState state, in FutureCommandEnvelope envelope)
        {
            if (!pendingRing.IsCreated || pendingRing.Length == 0)
                return;

            if (state.PendingCount >= pendingRing.Length)
            {
                state.PendingHead = AdvanceRingIndex(state.PendingHead, pendingRing.Length);
                state.PendingCount = math.max(0, state.PendingCount - 1);
            }

            pendingRing[state.PendingTail] = envelope;
            state.PendingTail = AdvanceRingIndex(state.PendingTail, pendingRing.Length);
            state.PendingCount = math.min(pendingRing.Length, state.PendingCount + 1);
        }

        private static FutureCommandEnvelope SwapEnvelopeEndian(in FutureCommandEnvelope envelope)
        {
            return new FutureCommandEnvelope
            {
                OpcodeHash = ReverseBytes32(envelope.OpcodeHash),
                ModderSignature = ReverseBytes32(envelope.ModderSignature),
                TargetAUP = new double3(
                    ReverseBytesDouble(envelope.TargetAUP.x),
                    ReverseBytesDouble(envelope.TargetAUP.y),
                    ReverseBytesDouble(envelope.TargetAUP.z)),
                PayloadData = new float4(
                    ReverseBytesFloat(envelope.PayloadData.x),
                    ReverseBytesFloat(envelope.PayloadData.y),
                    ReverseBytesFloat(envelope.PayloadData.z),
                    ReverseBytesFloat(envelope.PayloadData.w)),
                IntegrityHash = ReverseBytes64(envelope.IntegrityHash),
                _pad0 = ReverseBytes64(envelope._pad0)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReverseBytes64(ulong value)
        {
            return ((ulong)ReverseBytes32((uint)value) << 32) | ReverseBytes32((uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ReverseBytesFloat(float value)
        {
            return math.asfloat(ReverseBytes32(math.asuint(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ReverseBytesDouble(double value)
        {
            ulong bits = UnsafeUtility.As<double, ulong>(ref value);
            ulong reversed = ReverseBytes64(bits);
            return UnsafeUtility.As<ulong, double>(ref reversed);
        }

        private static int DropThermalBacklog(ref ModSandboxRingState state, int ringCapacity, float quality, int maxCommandsPerSignature)
        {
            if (ringCapacity <= 0 || state.PendingCount <= 0)
                return 0;

            float thermalShed01 = math.saturate((0.30f - math.saturate(quality)) * 3.3333333f);
            if (thermalShed01 <= 0f)
                return 0;

            int safeWindow = math.max(
                FutureCommandSandboxConstants.LowTierMinCommandsPerSignature * 2,
                maxCommandsPerSignature);
            int overflow = math.max(0, state.PendingCount - safeWindow);
            int dropCount = math.min(state.PendingCount, (int)math.round(overflow * thermalShed01));
            for (int i = 0; i < dropCount; i++)
                state.PendingHead = AdvanceRingIndex(state.PendingHead, ringCapacity);

            state.PendingCount = math.max(0, state.PendingCount - dropCount);
            return dropCount;
        }

        private static int FindModderLeaseSlot(NativeArray<ModderMemoryLease> leases, uint signature, out bool found)
        {
            found = false;
            if (!leases.IsCreated || signature == 0u || leases.Length == 0)
                return -1;

            uint mask = (uint)leases.Length - 1u;
            int start = (leases.Length & (leases.Length - 1)) == 0
                ? (int)(signature & mask)
                : (int)(signature % (uint)leases.Length);
            for (int probe = 0; probe < leases.Length; probe++)
            {
                int index = start + probe;
                if (index >= leases.Length)
                    index -= leases.Length;

                uint stored = leases[index].ModderSignature;
                if (stored == signature)
                {
                    found = true;
                    return index;
                }

                if (stored == 0u)
                    return index;
            }

            return -1;
        }

        private static int FindApprovedAssetSlot(NativeArray<ApprovedAssetRecord> approvedAssets, uint assetHash, out bool found)
        {
            found = false;
            if (!approvedAssets.IsCreated || assetHash == 0u || approvedAssets.Length == 0)
                return -1;

            uint mask = (uint)approvedAssets.Length - 1u;
            int start = (approvedAssets.Length & (approvedAssets.Length - 1)) == 0
                ? (int)(assetHash & mask)
                : (int)(assetHash % (uint)approvedAssets.Length);
            for (int probe = 0; probe < approvedAssets.Length; probe++)
            {
                int index = start + probe;
                if (index >= approvedAssets.Length)
                    index -= approvedAssets.Length;

                uint stored = approvedAssets[index].AssetHash;
                if (stored == assetHash)
                {
                    found = true;
                    return index;
                }

                if (stored == 0u)
                    return index;
            }

            return -1;
        }

        private static bool TryPrepareValidationJob(
            out ValidateFutureCommandEnvelopeJob job,
            out ModSandboxScheduledValidationState validationState,
            bool recordNoWorkTelemetry)
        {
            job = default;
            validationState = default;

            Initialize();
            AcquireVaultBuffers();

            NativeArray<FutureCommandEnvelope> pendingRing = ResolveBuffer(ref _pendingRingHandle);
            NativeArray<FutureCommandEnvelope> devNullRing = ResolveBuffer(ref _devNullRingHandle);
            NativeArray<FutureCommandEnvelope> staging = ResolveBuffer(ref _stagingHandle);
            NativeArray<FutureCommandValidationStats> statsBuffer = ResolveBuffer(ref _statsHandle);
            NativeArray<FutureCommandOpcodeRecord> opcodeRecords = ResolveBuffer(ref _opcodeRecordsHandle);
            NativeArray<ModderFrameCounter> perModCounters = ResolveBuffer(ref _perModCountersHandle);
            NativeArray<ModderMemoryLease> memoryLeases = ResolveBuffer(ref _memoryLeasesHandle);
            NativeArray<ApprovedAssetRecord> approvedAssets = ResolveBuffer(ref _approvedAssetManifestHandle);
            NativeArray<byte> modderBlackboxMemory = ResolveBuffer(ref _modderBlackboxMemoryHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveBuffer(ref _ringStateHandle);
            if (!pendingRing.IsCreated ||
                !devNullRing.IsCreated ||
                !staging.IsCreated ||
                !statsBuffer.IsCreated ||
                !opcodeRecords.IsCreated ||
                !perModCounters.IsCreated ||
                !memoryLeases.IsCreated ||
                !approvedAssets.IsCreated ||
                !modderBlackboxMemory.IsCreated ||
                !ringState.IsCreated ||
                ringState.Length == 0)
            {
                return false;
            }

            float quality = ResolveGlobalQualityWeight();
            FutureCommandSandboxTuning tuning = ResolveTuning(quality);
            int maxPerSignature = ResolveScaledCommandBudget(tuning.MaxCommandsPerFrame, quality);
            int globalBudget = math.min(
                staging.Length,
                math.max(maxPerSignature, maxPerSignature * 8));

            uint frame = (uint)Time.frameCount;
            if (IsRollbackFrozen())
            {
                ModSandboxRingState frozenState = ringState[0];
                if (recordNoWorkTelemetry)
                {
                    RecordTelemetry(
                        frame,
                        0u,
                        0u,
                        0u,
                        0u,
                        0u,
                        0UL,
                        quality,
                        (uint)FutureCommandRejectReason.RollbackFrozen,
                        0u,
                        (uint)frozenState.PendingCount,
                        0u,
                        (uint)maxPerSignature);
                }

                return false;
            }

            ModSandboxRingState state = ringState[0];
            int drainCount = 0;
            while (drainCount < globalBudget && state.PendingCount > 0)
            {
                FutureCommandEnvelope envelope = pendingRing[state.PendingHead];
                state.PendingHead = AdvanceRingIndex(state.PendingHead, pendingRing.Length);
                state.PendingCount = math.max(0, state.PendingCount - 1);
                staging[drainCount] = envelope;
                EnsureModderLease(envelope.ModderSignature, tuning.MaxModMemoryMb, modderBlackboxMemory, memoryLeases, ref state, frame);
                drainCount++;
            }

            int thermalDropped = DropThermalBacklog(ref state, pendingRing.Length, quality, maxPerSignature);
            ringState[0] = state;

            if (drainCount == 0)
            {
                if (recordNoWorkTelemetry)
                {
                    RecordTelemetry(
                        frame,
                        0u,
                        0u,
                        0u,
                        (uint)thermalDropped,
                        0u,
                        0UL,
                        quality,
                        0u,
                        0u,
                        (uint)state.PendingCount,
                        0u,
                        (uint)maxPerSignature);
                }

                return false;
            }

            MemClearArray(statsBuffer);
            validationState = new ModSandboxScheduledValidationState
            {
                Frame = frame,
                PendingAfterDrain = (uint)state.PendingCount,
                MaxCommandsPerSignature = (uint)maxPerSignature,
                ThermalDropped = (uint)thermalDropped,
                Quality = quality,
                Flags = 0u,
                StartTicks = Stopwatch.GetTimestamp()
            };

            job = new ValidateFutureCommandEnvelopeJob
            {
                Inputs = staging,
                Stats = statsBuffer,
                OpcodeRecords = opcodeRecords,
                PerModCounters = perModCounters,
                MemoryLeases = memoryLeases,
                ApprovedAssetManifest = approvedAssets,
                ModderBlackboxMemory = modderBlackboxMemory,
                DevNullRing = devNullRing,
                RingState = ringState,
                SpawnWriter = SignalBus<ModSpawnRequestSignal>.ParallelWriter,
                AssetWriter = SignalBus<ModAssetReferenceSignal>.ParallelWriter,
                AcousticWriter = SignalBus<MockAcousticSignal>.ParallelWriter,
                DamageWriter = SignalBus<MockDamageSignal>.ParallelWriter,
                DevNullSignalWriter = SignalBus<ModFutureDevNullSignal>.ParallelWriter,
                Count = drainCount,
                Frame = frame,
                OpcodeRecordCount = state.OpcodeCount,
                MaxCommandsPerSignature = maxPerSignature,
                GlobalQualityWeight = quality,
                MaxAssetBytes = tuning.MaxAssetBytes
            };
            return true;
        }

        private static void FinalizeValidationTelemetry(in ModSandboxScheduledValidationState validationState)
        {
            NativeArray<FutureCommandValidationStats> statsBuffer = ResolveBuffer(ref _statsHandle);
            NativeArray<ModSandboxRingState> ringState = ResolveRingState();
            if (!statsBuffer.IsCreated || statsBuffer.Length == 0 || !ringState.IsCreated || ringState.Length == 0)
                return;

            ModSandboxRingState state = ringState[0];
            FutureCommandValidationStats stats = statsBuffer[0];
            long elapsedTicks = Stopwatch.GetTimestamp() - validationState.StartTicks;
            if (elapsedTicks < 0L)
                elapsedTicks = 0L;
            ulong elapsedNs = (ulong)((double)elapsedTicks * 1000000000.0d / Stopwatch.Frequency);
            RecordTelemetry(
                validationState.Frame,
                stats.Incoming,
                stats.Valid,
                stats.Rejected,
                stats.Dropped + validationState.ThermalDropped,
                stats.DevNull,
                elapsedNs,
                validationState.Quality,
                stats.RejectionMask,
                stats.FaultHash,
                (uint)state.PendingCount,
                stats.PeakCommandsForSignature,
                validationState.MaxCommandsPerSignature);

            if (stats.FaultHash != 0u)
                DumpBlackbox(stats.FaultHash);
        }

        private static void MemClearArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length == 0)
                return;

            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array),
                (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ValidateFutureCommandEnvelopeJob : IJob
        {
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandEnvelope> Inputs;
            [NoAlias] public NativeArray<FutureCommandValidationStats> Stats;
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandOpcodeRecord> OpcodeRecords;
            [NoAlias] public NativeArray<ModderFrameCounter> PerModCounters;
            [NoAlias] [ReadOnly] public NativeArray<ModderMemoryLease> MemoryLeases;
            [NoAlias] [ReadOnly] public NativeArray<ApprovedAssetRecord> ApprovedAssetManifest;
            [NoAlias] [NativeDisableParallelForRestriction] public NativeArray<byte> ModderBlackboxMemory;
            [NoAlias] public NativeArray<FutureCommandEnvelope> DevNullRing;
            [NoAlias] public NativeArray<ModSandboxRingState> RingState;
            public NativeQueue<ModSpawnRequestSignal>.ParallelWriter SpawnWriter;
            public NativeQueue<ModAssetReferenceSignal>.ParallelWriter AssetWriter;
            public NativeQueue<MockAcousticSignal>.ParallelWriter AcousticWriter;
            public NativeQueue<MockDamageSignal>.ParallelWriter DamageWriter;
            public NativeQueue<ModFutureDevNullSignal>.ParallelWriter DevNullSignalWriter;
            public int Count;
            public int OpcodeRecordCount;
            public int MaxCommandsPerSignature;
            public uint Frame;
            public float GlobalQualityWeight;
            public uint MaxAssetBytes;

            public void Execute()
            {
                FutureCommandValidationStats stats = default;
                int count = math.min(Count, Inputs.Length);
                for (int i = 0; i < count; i++)
                {
                    stats.Incoming++;
                    FutureCommandEnvelope envelope = Inputs[i];
                    if (!TryValidateEnvelope(in envelope, ref stats))
                        continue;

                    RouteEnvelope(in envelope, ref stats);
                }

                Stats[0] = stats;
            }

            private bool TryValidateEnvelope(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (envelope.ModderSignature == 0u ||
                    envelope.OpcodeHash == 0u ||
                    !IsAllowedOpcode(envelope.OpcodeHash))
                {
                    Reject(ref stats, FutureCommandRejectReason.UnknownOpcode, 0u);
                    return false;
                }

                if (!IsFiniteBoundedAup(envelope.TargetAUP))
                {
                    Reject(ref stats, FutureCommandRejectReason.InvalidAup, FutureCommandSandboxConstants.FaultHashInvalidAup);
                    return false;
                }

                if (!VerifyIntegrity(in envelope))
                {
                    Reject(ref stats, FutureCommandRejectReason.IntegrityMismatch, 0u);
                    return false;
                }

                if (!TryAccount(in envelope, ref stats))
                    return false;

                return true;
            }

            private void RouteEnvelope(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                switch (envelope.OpcodeHash)
                {
                    case FutureCommandOpcodes.SpawnItem:
                        SpawnWriter.Enqueue(new ModSpawnRequestSignal
                        {
                            Frame = Frame,
                            ModderSignature = envelope.ModderSignature,
                            OpcodeHash = envelope.OpcodeHash,
                            AssetHash = math.asuint(envelope.PayloadData.x),
                            TargetAUP = envelope.TargetAUP,
                            PayloadData = envelope.PayloadData,
                            Flags = 0u,
                            Reserved = 0u
                        });
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.AssetReference:
                        if (!TryRouteAssetReference(in envelope, ref stats))
                            return;
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.ModMemoryWrite:
                        if (!TryWriteModMemory(in envelope, ref stats))
                            return;
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.ModMemoryRead:
                        if (!TryValidateModMemoryRange(in envelope, ref stats))
                            return;
                        EnqueueDevNull(in envelope, ref stats);
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.FaunaAcousticStimulus:
                        AcousticWriter.Enqueue(new MockAcousticSignal
                        {
                            Frame = Frame,
                            ModderSignature = envelope.ModderSignature,
                            SourceOpcode = envelope.OpcodeHash,
                            Intensity01 = math.saturate(envelope.PayloadData.x),
                            TargetAUP = envelope.TargetAUP,
                            RadiusMeters = math.max(0f, envelope.PayloadData.y),
                            StimulusHash = math.asuint(envelope.PayloadData.z),
                            IntegrityHash = envelope.IntegrityHash,
                            Reserved = 0UL
                        });
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.FaunaDamageStimulus:
                        DamageWriter.Enqueue(new MockDamageSignal
                        {
                            Frame = Frame,
                            ModderSignature = envelope.ModderSignature,
                            SourceOpcode = envelope.OpcodeHash,
                            DamageAmount = math.max(0f, envelope.PayloadData.x),
                            TargetAUP = envelope.TargetAUP,
                            RadiusMeters = math.max(0f, envelope.PayloadData.y),
                            DamageTypeHash = math.asuint(envelope.PayloadData.z),
                            IntegrityHash = envelope.IntegrityHash,
                            Reserved = 0UL
                        });
                        stats.Valid++;
                        break;

                    case FutureCommandOpcodes.AlterHealth:
                    case FutureCommandOpcodes.AlterGravity:
                    case FutureCommandOpcodes.TriggerSubtitleCue:
                        EnqueueDevNull(in envelope, ref stats);
                        stats.Valid++;
                        break;

                    default:
                        Reject(ref stats, FutureCommandRejectReason.UnknownOpcode, 0u);
                        break;
                }
            }

            private bool TryRouteAssetReference(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                uint assetHash = math.asuint(envelope.PayloadData.x);
                uint declaredCrc = math.asuint(envelope.PayloadData.y);
                uint declaredBytes = math.asuint(envelope.PayloadData.z);
                if (declaredBytes == 0u || declaredBytes > MaxAssetBytes)
                {
                    Reject(ref stats, FutureCommandRejectReason.AssetTooLarge, 0u);
                    return false;
                }

                if (assetHash == 0u ||
                    declaredCrc == 0u ||
                    !TryGetApprovedAsset(assetHash, out ApprovedAssetRecord approvedAsset) ||
                    approvedAsset.Crc32 != declaredCrc)
                {
                    Reject(ref stats, FutureCommandRejectReason.AssetCrcMismatch, 0u);
                    return false;
                }

                if (approvedAsset.ByteLength != 0u && declaredBytes > approvedAsset.ByteLength)
                {
                    Reject(ref stats, FutureCommandRejectReason.AssetTooLarge, 0u);
                    return false;
                }

                AssetWriter.Enqueue(new ModAssetReferenceSignal
                {
                    Frame = Frame,
                    ModderSignature = envelope.ModderSignature,
                    AssetHash = assetHash,
                    Crc32 = declaredCrc,
                    TargetAUP = envelope.TargetAUP,
                    ByteLength = declaredBytes,
                    Flags = 0u,
                    IntegrityHash = envelope.IntegrityHash,
                    Reserved = 0UL
                });
                return true;
            }

            private bool TryWriteModMemory(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (!ModderBlackboxMemory.IsCreated)
                {
                    Reject(ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                if (!TryGetMemoryLease(envelope.ModderSignature, out ModderMemoryLease lease))
                {
                    Reject(ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                uint relativeOffset = math.asuint(envelope.PayloadData.x);
                uint rawValue = math.asuint(envelope.PayloadData.y);
                uint byteCount = math.clamp(math.asuint(envelope.PayloadData.z), 1u, 4u);
                if (relativeOffset > (uint)lease.ByteLength ||
                    byteCount > (uint)lease.ByteLength - relativeOffset ||
                    lease.OffsetBytes < 0 ||
                    lease.OffsetBytes > ModderBlackboxMemory.Length - lease.ByteLength)
                {
                    Reject(ref stats, FutureCommandRejectReason.MemoryViolation, FutureCommandSandboxConstants.FaultHashMemoryViolation);
                    return false;
                }

                int absolute = lease.OffsetBytes + (int)relativeOffset;
                for (uint i = 0; i < byteCount; i++)
                    ModderBlackboxMemory[absolute + (int)i] = (byte)(rawValue >> ((int)i * 8));

                return true;
            }

            private bool TryValidateModMemoryRange(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (!ModderBlackboxMemory.IsCreated)
                {
                    Reject(ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                if (!TryGetMemoryLease(envelope.ModderSignature, out ModderMemoryLease lease))
                {
                    Reject(ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                uint relativeOffset = math.asuint(envelope.PayloadData.x);
                uint byteCount = math.max(1u, math.asuint(envelope.PayloadData.z));
                if (relativeOffset > (uint)lease.ByteLength ||
                    byteCount > (uint)lease.ByteLength - relativeOffset ||
                    lease.OffsetBytes < 0 ||
                    lease.OffsetBytes > ModderBlackboxMemory.Length - lease.ByteLength)
                {
                    Reject(ref stats, FutureCommandRejectReason.MemoryViolation, FutureCommandSandboxConstants.FaultHashMemoryViolation);
                    return false;
                }

                return true;
            }

            private bool TryAccount(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                int slot = FindCounterSlot(envelope.ModderSignature, out bool exists);
                if (slot < 0)
                {
                    Reject(ref stats, FutureCommandRejectReason.CommandFlood, 0u);
                    return false;
                }

                ModderFrameCounter counter = exists ? PerModCounters[slot] : default;
                if (!exists || counter.Frame != Frame)
                {
                    counter = new ModderFrameCounter
                    {
                        ModderSignature = envelope.ModderSignature,
                        Frame = Frame,
                        Count = 0,
                        Dropped = 0
                    };
                }

                if (counter.Count >= MaxCommandsPerSignature)
                {
                    counter.Dropped++;
                    stats.Dropped++;
                    PerModCounters[slot] = counter;
                    Reject(ref stats, FutureCommandRejectReason.CommandFlood, 0u);
                    return false;
                }

                counter.Count++;
                stats.PeakCommandsForSignature = math.max(stats.PeakCommandsForSignature, (uint)counter.Count);
                PerModCounters[slot] = counter;
                return true;
            }

            private void EnqueueDevNull(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (DevNullRing.IsCreated && RingState.IsCreated && RingState.Length > 0 && DevNullRing.Length > 0)
                {
                    ModSandboxRingState state = RingState[0];
                    if (state.DevNullCount >= DevNullRing.Length)
                    {
                        state.DevNullHead = AdvanceRingIndex(state.DevNullHead, DevNullRing.Length);
                        state.DevNullCount = math.max(0, state.DevNullCount - 1);
                    }

                    DevNullRing[state.DevNullTail] = envelope;
                    state.DevNullTail = AdvanceRingIndex(state.DevNullTail, DevNullRing.Length);
                    state.DevNullCount = math.min(DevNullRing.Length, state.DevNullCount + 1);
                    RingState[0] = state;
                }
                DevNullSignalWriter.Enqueue(new ModFutureDevNullSignal
                {
                    Frame = Frame,
                    ModderSignature = envelope.ModderSignature,
                    OpcodeHash = envelope.OpcodeHash,
                    ReasonHash = FutureCommandSandboxConstants.DevNullReasonFutureSeam,
                    TargetAUP = envelope.TargetAUP,
                    PayloadData = envelope.PayloadData,
                    Flags = 0u,
                    Reserved = 0u
                });
                stats.DevNull++;
            }

            private bool IsAllowedOpcode(uint opcodeHash)
            {
                if (!OpcodeRecords.IsCreated || opcodeHash == 0u)
                    return false;

                int count = math.min(OpcodeRecordCount, OpcodeRecords.Length);
                for (int i = 0; i < count; i++)
                {
                    FutureCommandOpcodeRecord record = OpcodeRecords[i];
                    if (record.OpcodeHash == opcodeHash && (record.Flags & 1u) != 0u)
                        return true;
                }

                return false;
            }

            private bool TryGetMemoryLease(uint signature, out ModderMemoryLease lease)
            {
                lease = default;
                if (!MemoryLeases.IsCreated || signature == 0u || MemoryLeases.Length == 0)
                    return false;

                int slot = FindLeaseSlot(signature, out bool found);
                if (!found)
                    return false;

                lease = MemoryLeases[slot];
                return lease.ModderSignature == signature && (lease.Flags & 1u) != 0u;
            }

            private bool TryGetApprovedAsset(uint assetHash, out ApprovedAssetRecord asset)
            {
                asset = default;
                if (!ApprovedAssetManifest.IsCreated || assetHash == 0u || ApprovedAssetManifest.Length == 0)
                    return false;

                uint mask = (uint)ApprovedAssetManifest.Length - 1u;
                int start = (ApprovedAssetManifest.Length & (ApprovedAssetManifest.Length - 1)) == 0
                    ? (int)(assetHash & mask)
                    : (int)(assetHash % (uint)ApprovedAssetManifest.Length);
                for (int probe = 0; probe < ApprovedAssetManifest.Length; probe++)
                {
                    int index = start + probe;
                    if (index >= ApprovedAssetManifest.Length)
                        index -= ApprovedAssetManifest.Length;

                    ApprovedAssetRecord record = ApprovedAssetManifest[index];
                    if (record.AssetHash == assetHash && (record.Flags & 1u) != 0u)
                    {
                        asset = record;
                        return true;
                    }

                    if (record.AssetHash == 0u)
                        return false;
                }

                return false;
            }

            private int FindCounterSlot(uint signature, out bool found)
            {
                found = false;
                if (!PerModCounters.IsCreated || signature == 0u || PerModCounters.Length == 0)
                    return -1;

                uint mask = (uint)PerModCounters.Length - 1u;
                int start = (PerModCounters.Length & (PerModCounters.Length - 1)) == 0
                    ? (int)(signature & mask)
                    : (int)(signature % (uint)PerModCounters.Length);
                for (int probe = 0; probe < PerModCounters.Length; probe++)
                {
                    int index = start + probe;
                    if (index >= PerModCounters.Length)
                        index -= PerModCounters.Length;

                    uint stored = PerModCounters[index].ModderSignature;
                    if (stored == signature)
                    {
                        found = true;
                        return index;
                    }

                    if (stored == 0u)
                        return index;
                }

                return -1;
            }

            private int FindLeaseSlot(uint signature, out bool found)
            {
                found = false;
                uint mask = (uint)MemoryLeases.Length - 1u;
                int start = (MemoryLeases.Length & (MemoryLeases.Length - 1)) == 0
                    ? (int)(signature & mask)
                    : (int)(signature % (uint)MemoryLeases.Length);
                for (int probe = 0; probe < MemoryLeases.Length; probe++)
                {
                    int index = start + probe;
                    if (index >= MemoryLeases.Length)
                        index -= MemoryLeases.Length;

                    uint stored = MemoryLeases[index].ModderSignature;
                    if (stored == signature)
                    {
                        found = true;
                        return index;
                    }

                    if (stored == 0u)
                        return index;
                }

                return -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int AdvanceRingIndex(int index, int capacity)
            {
                if (capacity <= 1)
                    return 0;

                index++;
                return index >= capacity ? 0 : index;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool VerifyIntegrity(in FutureCommandEnvelope envelope)
            {
                FutureCommandEnvelope copy = envelope;
                copy.IntegrityHash = 0UL;
                copy._pad0 = 0UL;
                uint2 hash = xxHash3.Hash64(&copy, 48L);
                ulong full = ((ulong)hash.y << 32) | hash.x;
                return full == envelope.IntegrityHash;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsFiniteBoundedAup(double3 aup)
            {
                double3 abs = math.abs(aup);
                return math.all(math.isfinite(aup)) &&
                       math.all(abs <= new double3(FutureCommandSandboxConstants.MaxAupMagnitudeMeters));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Reject(ref FutureCommandValidationStats stats, FutureCommandRejectReason reason, uint faultHash)
            {
                stats.Rejected++;
                stats.RejectionMask |= (uint)reason;
                if (faultHash != 0u)
                    stats.FaultHash = faultHash;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct MockMaliciousEnvelopeInjectionJob : IJob
        {
            [NoAlias]
            public NativeQueue<FutureCommandEnvelope>.ParallelWriter Output;
            public uint ModderSignature;

            public void Execute()
            {
                FutureCommandEnvelope envelope = default;
                envelope.OpcodeHash = FutureCommandOpcodes.SpawnItem;
                envelope.ModderSignature = ModderSignature == 0u ? 0x4D4F434Bu : ModderSignature;
                envelope.TargetAUP = new double3(double.NaN, 1000000d, 0d);
                envelope.PayloadData = new float4(0f, 0f, 0f, 0f);
                envelope.IntegrityHash = 0UL;
                envelope._pad0 = 0UL;
                Output.Enqueue(envelope);
            }
        }
    }
}
