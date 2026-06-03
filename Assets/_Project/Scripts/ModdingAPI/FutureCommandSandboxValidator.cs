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
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public const uint SurvivalOverride = 0x85C0241Fu;
        public const uint HapticPulse = 0xE6E4AEBBu;
        public const uint SubtitleCue = 0xA1B1CCCCu;
    }

    /// <summary>
    /// One 64-byte binary request from UGC. No managed references, properties, or forced byte packing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = FutureCommandSandboxConstants.EnvelopeSizeBytes)]
    public struct FutureCommandEnvelope
    {
        public const int SizeBytes = FutureCommandSandboxConstants.EnvelopeSizeBytes;

        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public uint ModderSignature;
        [FieldOffset(8)] public double3 TargetAUP;
        [FieldOffset(32)] public float4 PayloadData;
        [FieldOffset(48)] public ulong IntegrityHash;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct FutureCommandOpcodeRecord
    {
        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint ManifestCrc32;
        [FieldOffset(12)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FutureCommandSandboxTuning
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
    internal struct ModderMemoryLease
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
    internal struct ModderFrameCounter
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
    internal struct ApprovedAssetRecord
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public uint Crc32;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModSandboxRingState
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
        [FieldOffset(44)] public uint PendingOverflowDropped;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct RollbackRuntimeStateFlagView
    {
        [FieldOffset(44)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModSandboxTelemetryEntry
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
    internal struct ModSpawnRequestSignal : ISignal
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
    internal struct ModAssetReferenceSignal : ISignal
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
    internal struct SandboxMockAcousticSignal : ISignal
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
    internal struct MockDamageSignal : ISignal
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
    internal struct ModFutureDevNullSignal : ISignal
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SurvivalOverrideSignal : ISignal
    {
        [FieldOffset(0)] public uint ModHash;
        [FieldOffset(4)] public uint RequestId;
        [FieldOffset(8)] public float OxygenFloor;
        [FieldOffset(12)] public uint TTL;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModHapticPulseSignal : ISignal
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public uint WaveformHash;
        [FieldOffset(28)] public float Intensity;
        [FieldOffset(32)] public float Duration;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct ModSubtitleCueSignal : ISignal
    {
        [FieldOffset(0)] public uint TokenHash;
        [FieldOffset(4)] public float Duration;
        [FieldOffset(8)] public uint Priority;
        [FieldOffset(12)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct ModCommandKernelOpcodeRecord
    {
        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public uint KernelId;
        [FieldOffset(8)] public float PriorityWeight;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ModKernelTuningProfile
    {
        [FieldOffset(0)] public uint OpcodeHash;
        [FieldOffset(4)] public float PriorityWeight;
        [FieldOffset(8)] public int MaxPerFrame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float MaxDurationSeconds;
        [FieldOffset(20)] public float RangeMeters;
        [FieldOffset(24)] public float IntensityScale;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ModKernelCameraJuiceImpulse
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float Scalar;
        [FieldOffset(28)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModKernelCameraJuiceState
    {
        [FieldOffset(0)] public int Head;
        [FieldOffset(4)] public int Count;
        [FieldOffset(8)] public uint LastFrame;
        [FieldOffset(12)] public uint Dropped;
        [FieldOffset(16)] public ulong Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
        [FieldOffset(32)] public ulong Reserved2;
        [FieldOffset(40)] public ulong Reserved3;
        [FieldOffset(48)] public ulong Reserved4;
        [FieldOffset(56)] public ulong Reserved5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct KernelExecutionTelemetryEntry
    {
        [FieldOffset(0)] public ulong ExecutionTicks;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SurvivalProcessed;
        [FieldOffset(16)] public uint HapticProcessed;
        [FieldOffset(20)] public uint SubtitleProcessed;
        [FieldOffset(24)] public uint ShedByThermal;
        [FieldOffset(28)] public uint Rejected;
        [FieldOffset(32)] public uint RollbackSuppressed;
        [FieldOffset(36)] public uint HapticFallbacks;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public uint AupViolations;
        [FieldOffset(48)] public uint PendingQueueDepth;
        [FieldOffset(52)] public uint FaultHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct ModSandboxMemoryWriteCommand
    {
        [FieldOffset(0)] public int OffsetBytes;
        [FieldOffset(4)] public uint RawValue;
        [FieldOffset(8)] public uint ByteCount;
        [FieldOffset(12)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ModSandboxValidationScratchState
    {
        public FutureCommandValidationStats Stats;
        public int MemoryWriteCount;
        public int DevNullCount;
        public int CameraImpulseCount;
        public int Reserved;
    }

    internal static class FutureCommandSandboxConstants
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
        public const double KernelMaxAupMagnitudeMeters = 100000.0d;
        public const int KernelOpcodeMapCapacity = 16;
        public const int KernelTelemetryCapacity = 300;
        public const int CameraJuiceImpulseCapacity = 256;
        public const int KernelTuningCapacity = 16;
        public const int KernelCsvScratchBytes = 16 * 1024;
        public const int KernelMaxProfileCommandsPerFrame = 10000;
        public const float KernelOptionalPriorityMax = 0.50f;
        public const float KernelSurvivalPriorityMin = 0.90f;
        public const uint DevNullReasonFutureSeam = 0x44564E4Cu;
        public const uint FaultHashInvalidAup = 0x414E414Eu;
        public const uint FaultHashInvalidPayload = 0x5041594Cu;
        public const uint FaultHashMemoryViolation = 0x4D56494Fu;
        public const uint FaultHashLayout = 0x4C41594Fu;
        public const uint FaultHashKernelSpike = 0x4B53504Bu;
        public const uint KernelFlagForceHapticCameraFallback = 1u << 8;
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
        LayoutInvalid = 1u << 10,
        InvalidPayload = 1u << 11,
        AupViolation = 1u << 12,
        ThermalShed = 1u << 13,
        KernelPayload = 1u << 14,
        RollbackSuppressed = 1u << 15
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
        [FieldOffset(36)] public uint SurvivalProcessed;
        [FieldOffset(40)] public uint HapticProcessed;
        [FieldOffset(44)] public uint SubtitleProcessed;
        [FieldOffset(48)] public uint KernelRejected;
        [FieldOffset(52)] public uint KernelSuppressed;
        [FieldOffset(56)] public uint HapticFallbacks;
        [FieldOffset(60)] public uint AupViolations;
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

    internal ref struct MockModQueue
    {
        private NativeQueue<FutureCommandEnvelope> _queue;

        internal static MockModQueue Wrap(NativeQueue<FutureCommandEnvelope> externalQueue)
        {
            MockModQueue queue = default;
            queue.Attach(externalQueue);
            return queue;
        }

        internal bool GetIsCreated()
        {
            return _queue.IsCreated;
        }

        internal bool Attach(NativeQueue<FutureCommandEnvelope> externalQueue)
        {
            if (!externalQueue.IsCreated)
                return false;

            _queue = externalQueue;
            return true;
        }

        internal void Dispose()
        {
            _queue = default;
        }
    }

    /// <summary>
    /// Binary-only mod quarantine. It never invokes mod C#; it validates math packets and emits DOD signals.
    /// </summary>
    internal static unsafe class FutureCommandSandboxValidator
    {
        private const string DumpPath = "Docs/AgentLogs/Dump_QUARANTINE_SURGEON.bin";
        private const string KernelDumpPath = "Docs/AgentLogs/Dump_COMMAND_FORGE.bin";
        private const int DefaultMemoryBytes = FutureCommandSandboxConstants.DefaultMaxModMemoryMb * 1024 * 1024;
        private const uint EnabledAllEmergencyOpcodes = 0xFFFu;
        private const uint RollbackRuntimeStateBufferId = 70752u;
        private const uint RollbackFlagResimulating = 1u << 4;
        private const uint KernelIdSurvivalOverride = 1u;
        private const uint KernelIdHapticPulse = 2u;
        private const uint KernelIdSubtitleCue = 3u;
        private const uint KernelOpcodeMapBufferId = 70914u;
        private const uint KernelTelemetryRingBufferId = 70915u;
        private const uint KernelTelemetryCursorBufferId = 70916u;
        private const uint KernelCameraJuiceImpulseBufferId = 70917u;
        private const uint KernelCameraJuiceStateBufferId = 70918u;
        private const uint KernelTuningProfilesBufferId = 70919u;
        private const long KernelSpikeTicksNumerator = 5L;
        private const long KernelSpikeTicksDenominator = 10000L;

        private struct VaultLane<T> where T : struct
        {
            public VaultGenerationHandle<T> Handle;
            public uint ExpectedBufferID;
            public int Length;
        }

        private static IDataVault _dataVault;
        private static VaultLane<FutureCommandEnvelope> _pendingRingHandle;
        private static VaultLane<FutureCommandEnvelope> _devNullRingHandle;
        private static VaultLane<FutureCommandEnvelope> _stagingHandle;
        private static VaultLane<FutureCommandValidationStats> _statsHandle;
        private static VaultLane<FutureCommandOpcodeRecord> _opcodeRecordsHandle;
        private static VaultLane<ModSandboxTelemetryEntry> _telemetryRingHandle;
        private static VaultLane<int> _telemetryCursorHandle;
        private static VaultLane<byte> _modderBlackboxMemoryHandle;
        private static VaultLane<FutureCommandSandboxTuning> _tuningHandle;
        private static VaultLane<ModderFrameCounter> _perModCountersHandle;
        private static VaultLane<ModderMemoryLease> _memoryLeasesHandle;
        private static VaultLane<ApprovedAssetRecord> _approvedAssetManifestHandle;
        private static VaultLane<ModSandboxRingState> _ringStateHandle;
        private static VaultLane<ModCommandKernelOpcodeRecord> _kernelOpcodeMapHandle;
        private static VaultLane<KernelExecutionTelemetryEntry> _kernelTelemetryRingHandle;
        private static VaultLane<int> _kernelTelemetryCursorHandle;
        private static VaultLane<ModKernelCameraJuiceImpulse> _kernelCameraJuiceImpulseHandle;
        private static VaultLane<ModKernelCameraJuiceState> _kernelCameraJuiceStateHandle;
        private static VaultLane<ModKernelTuningProfile> _kernelTuningProfilesHandle;
        private static NativeArray<ModSandboxValidationScratchState> _validationScratchState;
        private static NativeArray<ModderFrameCounter> _validationCounterScratch;
        private static NativeArray<ModSandboxMemoryWriteCommand> _validationMemoryWriteScratch;
        private static NativeArray<FutureCommandEnvelope> _validationDevNullScratch;
        private static NativeArray<ModKernelCameraJuiceImpulse> _validationCameraImpulseScratch;
        private static JobHandle _scheduledValidationHandle;
        private static ModSandboxScheduledValidationState _scheduledValidationState;
        private static bool _scheduledValidationActive;
        private static bool _initialized;
        private static bool _rollbackFreezeOverride;
        private static int _lastLocalDumpFrame = -1;

        internal static int GetPendingEnvelopeCount()
        {
            return TryReadRingStateSnapshot(out ModSandboxRingState state) ? state.PendingCount : 0;
        }

        internal static int GetDevNullEnvelopeCount()
        {
            return TryReadRingStateSnapshot(out ModSandboxRingState state) ? state.DevNullCount : 0;
        }

        internal static bool GetIsInitialized()
        {
            return _initialized;
        }

        internal static bool GetHasScheduledValidation()
        {
            return _scheduledValidationActive;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallEditorLifecycleShutdownHook()
        {
            H8Memory.RegisterBeforeShutdownOwnerRelease(Shutdown);
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Shutdown();
            }
        }
#endif

        internal static void Initialize()
        {
#if UNITY_EDITOR
            InstallEditorLifecycleShutdownHook();
#endif

            if (_initialized)
                return;

            BindRegistryServicesCold();
            ValidateLayoutOrDump();

            ConfigureSignalLanes();
            AcquireVaultBuffers();
            EnsureValidationScratchBuffersCold();
            GenerateEmergencyMockOpcodes();

            _rollbackFreezeOverride = false;
            _initialized = true;
        }

        internal static void Shutdown()
        {
            try
            {
                CompleteScheduledPreSimulationForBarrier();
            }
            finally
            {
                try
                {
                    ReleaseVaultHandles(_dataVault);
                }
                finally
                {
                    ReleaseValidationScratchBuffers();
                    _dataVault = null;
                    _scheduledValidationHandle = default;
                    _scheduledValidationState = default;
                    _scheduledValidationActive = false;
                    _lastLocalDumpFrame = -1;
                    _initialized = false;
                }
            }
        }

        internal static void BindRegistryServicesCold()
        {
            RebindDataVault(GlobalRegistry.DataVault);
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
        }

        internal static bool Request(in FutureCommandEnvelope envelope)
        {
            if (!_initialized)
                return false;

            AcquireVaultBuffers();
            if (!TryReadRingStateSnapshot(out ModSandboxRingState state))
                return false;

            if (!TryEnqueueSinglePendingEnvelope(in envelope, ref state))
                return false;

            return TryWriteRingStateSnapshot(in state);
        }

        internal static int RequestRawEnvelopeStream(NativeArray<byte> bytes, int byteLength)
        {
            return RequestRawEnvelopeStream(bytes, byteLength, sourceBigEndian: false);
        }

        internal static int RequestRawEnvelopeStream(NativeArray<byte> bytes, int byteLength, bool sourceBigEndian)
        {
            if (!_initialized)
                return 0;

            if (!bytes.IsCreated || byteLength < FutureCommandSandboxConstants.EnvelopeSizeBytes)
                return 0;

            AcquireVaultBuffers();
            if (!TryReadRingStateSnapshot(out ModSandboxRingState state))
                return 0;

            int safeBytes = math.min(byteLength, bytes.Length);
            int count = safeBytes / FutureCommandSandboxConstants.EnvelopeSizeBytes;
            int accepted = 0;
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _pendingRingHandle, out NativeArray<FutureCommandEnvelope> pendingRing, out lockedVault))
            {
                return 0;
            }

            try
            {
                for (int i = 0; i < count; i++)
                {
                    FutureCommandEnvelope envelope = ((FutureCommandEnvelope*)ptr)[i];
                    if (sourceBigEndian)
                        envelope = SwapEnvelopeEndian(in envelope);

                    EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
                    accepted++;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _pendingRingHandle.Handle, SystemID.ModSandbox);
            }

            return accepted > 0 && TryWriteRingStateSnapshot(in state) ? accepted : 0;
        }

        internal static int RequestFromExternalQueue(NativeQueue<FutureCommandEnvelope> sourceQueue, int maxEnvelopeCount)
        {
            if (!_initialized)
                return 0;

            if (!sourceQueue.IsCreated || maxEnvelopeCount <= 0)
                return 0;

            AcquireVaultBuffers();
            if (!TryReadRingStateSnapshot(out ModSandboxRingState state))
                return 0;

            int accepted = 0;
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _pendingRingHandle, out NativeArray<FutureCommandEnvelope> pendingRing, out lockedVault))
            {
                return 0;
            }

            try
            {
                int limit = math.min(maxEnvelopeCount, pendingRing.Length);
                while (accepted < limit && sourceQueue.TryDequeue(out FutureCommandEnvelope envelope))
                {
                    EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
                    accepted++;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _pendingRingHandle.Handle, SystemID.ModSandbox);
            }

            return accepted > 0 && TryWriteRingStateSnapshot(in state) ? accepted : 0;
        }

        internal static void DrainPreSimulation()
        {
            TryFinalizeScheduledPreSimulationNoWait();
            if (_scheduledValidationActive)
                return;

            if (!TryPrepareValidationJob(out ValidateFutureCommandEnvelopeJob job, out ModSandboxScheduledValidationState validationState, recordNoWorkTelemetry: true))
                return;

            JobHandle validationHandle = job.Schedule();
            _scheduledValidationHandle = validationHandle;
            _scheduledValidationState = validationState;
            _scheduledValidationActive = true;
            H8Memory.RegisterActiveJob(SystemID.ModSandbox, validationHandle);
        }

        internal static bool TrySchedulePreSimulation(JobHandle dependsOn, out JobHandle validationHandle)
        {
            validationHandle = dependsOn;
            TryFinalizeScheduledPreSimulationNoWait();
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

        internal static bool TryFinalizeScheduledPreSimulationNoWait()
        {
            if (!_scheduledValidationActive)
                return true;

            if (!_scheduledValidationHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledValidationHandle))
            {
                return false;
            }

            return CommitScheduledValidation();
        }

        internal static bool CompleteScheduledPreSimulationForBarrier()
        {
            if (!_scheduledValidationActive)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _scheduledValidationHandle, forceComplete: true))
                return false;

            return CommitScheduledValidation();
        }

        private static bool CommitScheduledValidation()
        {
            if (!CommitScheduledValidationOutputs())
                return false;

            FinalizeValidationTelemetry(in _scheduledValidationState);
            _scheduledValidationState = default;
            _scheduledValidationActive = false;
            return true;
        }

        private static bool CommitScheduledValidationOutputs()
        {
            if (!_validationScratchState.IsCreated || _validationScratchState.Length == 0)
                return false;

            ModSandboxValidationScratchState output = _validationScratchState[0];
            if (!TryWriteVaultLaneElement(ref _statsHandle, 0, in output.Stats))
                return false;
            if (!TryCommitCounterScratch())
                return false;
            if (!TryCommitMemoryWriteScratch(output.MemoryWriteCount))
                return false;
            if (!TryCommitDevNullScratch(output.DevNullCount))
                return false;
            if (!TryCommitCameraImpulseScratch(output.CameraImpulseCount))
                return false;

            return true;
        }

        private static bool TryCommitCounterScratch()
        {
            if (!_validationCounterScratch.IsCreated)
                return false;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _perModCountersHandle, out NativeArray<ModderFrameCounter> counters, out lockedVault))
                return false;

            try
            {
                int count = math.min(counters.Length, _validationCounterScratch.Length);
                for (int i = 0; i < count; i++)
                    counters[i] = _validationCounterScratch[i];
                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _perModCountersHandle.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryCommitMemoryWriteScratch(int memoryWriteCount)
        {
            if (memoryWriteCount <= 0)
                return true;
            if (!_validationMemoryWriteScratch.IsCreated)
                return false;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _modderBlackboxMemoryHandle, out NativeArray<byte> blackboxMemory, out lockedVault))
                return false;

            try
            {
                int count = math.min(memoryWriteCount, _validationMemoryWriteScratch.Length);
                for (int i = 0; i < count; i++)
                {
                    ModSandboxMemoryWriteCommand command = _validationMemoryWriteScratch[i];
                    int byteCount = (int)math.min(command.ByteCount, 4u);
                    if (command.OffsetBytes < 0 ||
                        byteCount <= 0 ||
                        command.OffsetBytes > blackboxMemory.Length - byteCount)
                    {
                        continue;
                    }

                    for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
                        blackboxMemory[command.OffsetBytes + byteIndex] = (byte)(command.RawValue >> (byteIndex * 8));
                }

                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _modderBlackboxMemoryHandle.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryCommitDevNullScratch(int devNullCount)
        {
            if (devNullCount <= 0)
                return true;
            if (!_validationDevNullScratch.IsCreated ||
                !TryReadRingStateSnapshot(out ModSandboxRingState state))
            {
                return false;
            }

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _devNullRingHandle, out NativeArray<FutureCommandEnvelope> devNullRing, out lockedVault))
                return false;

            try
            {
                int count = math.min(devNullCount, _validationDevNullScratch.Length);
                for (int i = 0; i < count; i++)
                {
                    if (state.DevNullCount >= devNullRing.Length)
                    {
                        state.DevNullHead = AdvanceRingIndex(state.DevNullHead, devNullRing.Length);
                        state.DevNullCount = math.max(0, state.DevNullCount - 1);
                    }

                    devNullRing[state.DevNullTail] = _validationDevNullScratch[i];
                    state.DevNullTail = AdvanceRingIndex(state.DevNullTail, devNullRing.Length);
                    state.DevNullCount = math.min(devNullRing.Length, state.DevNullCount + 1);
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _devNullRingHandle.Handle, SystemID.ModSandbox);
            }

            return TryWriteRingStateSnapshot(in state);
        }

        private static bool TryCommitCameraImpulseScratch(int cameraImpulseCount)
        {
            if (cameraImpulseCount <= 0)
                return true;
            if (!_validationCameraImpulseScratch.IsCreated ||
                !TryOpenVaultLaneRead(ref _kernelCameraJuiceStateHandle, out NativeArray<ModKernelCameraJuiceState>.ReadOnly cameraStateRead) ||
                cameraStateRead.Length == 0)
            {
                return false;
            }

            ModKernelCameraJuiceState state = cameraStateRead[0];
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _kernelCameraJuiceImpulseHandle, out NativeArray<ModKernelCameraJuiceImpulse> cameraImpulses, out lockedVault))
                return false;

            try
            {
                int count = math.min(cameraImpulseCount, math.min(_validationCameraImpulseScratch.Length, cameraImpulses.Length));
                for (int i = 0; i < count; i++)
                {
                    int head = state.Head;
                    if ((uint)head >= (uint)cameraImpulses.Length)
                        head = 0;

                    cameraImpulses[head] = _validationCameraImpulseScratch[i];
                    state.Head = AdvanceRingIndex(head, cameraImpulses.Length);
                    state.Count = math.min(cameraImpulses.Length, state.Count + 1);
                    state.LastFrame = _validationCameraImpulseScratch[i].Frame;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _kernelCameraJuiceImpulseHandle.Handle, SystemID.ModSandbox);
            }

            return TryWriteVaultLaneElement(ref _kernelCameraJuiceStateHandle, 0, in state);
        }

        internal static void DrainLateFrame()
        {
            if (!_initialized)
                return;

            TryFinalizeScheduledPreSimulationNoWait();
            if (_scheduledValidationActive)
                return;

            if (!TryReadRingStateSnapshot(out ModSandboxRingState state))
                return;

            state.DevNullHead = state.DevNullTail;
            state.DevNullCount = 0;
            if (!TryWriteRingStateSnapshot(in state))
                return;
        }

        internal static bool RegisterApprovedAsset(uint assetHash, uint crc32)
        {
            return RegisterApprovedAsset(assetHash, crc32, 0u);
        }

        internal static bool RegisterApprovedAsset(uint assetHash, uint crc32, uint byteLength)
        {
            Initialize();
            if (assetHash == 0u || crc32 == 0u || !TryReadRingStateSnapshot(out ModSandboxRingState state))
                return false;

            bool found = false;
            int assetCapacity = 0;
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _approvedAssetManifestHandle, out NativeArray<ApprovedAssetRecord> approvedAssets, out lockedVault))
                return false;

            try
            {
                assetCapacity = approvedAssets.Length;
                int slot = FindApprovedAssetSlot(approvedAssets, assetHash, out found);
                if (slot < 0)
                    return false;

                approvedAssets[slot] = new ApprovedAssetRecord
                {
                    AssetHash = assetHash,
                    Crc32 = crc32,
                    ByteLength = byteLength,
                    Flags = 1u
                };
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _approvedAssetManifestHandle.Handle, SystemID.ModSandbox);
            }

            if (!found)
            {
                state.ApprovedAssetCount = math.min(assetCapacity, state.ApprovedAssetCount + 1);
                return TryWriteRingStateSnapshot(in state);
            }

            return true;
        }

        internal static bool RegisterApprovedAsset(uint assetHash, NativeArray<byte> assetBytes, int byteLength)
        {
            if (!assetBytes.IsCreated || byteLength <= 0)
                return false;

            int safeLength = math.min(byteLength, assetBytes.Length);
            return RegisterApprovedAsset(assetHash, ComputeCrc32(assetBytes, safeLength), (uint)safeLength);
        }

        internal static uint ComputeCrc32(NativeArray<byte> bytes, int byteLength)
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

        internal static bool SetOpcodeEnabled(uint opcodeHash, bool enabled)
        {
            Initialize();
            if (opcodeHash == 0u || !TryReadRingStateSnapshot(out ModSandboxRingState state))
                return false;

            bool recordFound = false;
            bool inserted = false;
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord> opcodeRecords, out lockedVault))
                return false;

            try
            {
                for (int i = 0; i < opcodeRecords.Length; i++)
                {
                    FutureCommandOpcodeRecord record = opcodeRecords[i];
                    if (record.OpcodeHash != opcodeHash)
                        continue;

                    record.Flags = enabled ? 1u : 0u;
                    opcodeRecords[i] = record;
                    recordFound = true;
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
                inserted = true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _opcodeRecordsHandle.Handle, SystemID.ModSandbox);
            }

            return recordFound || (inserted && TryWriteRingStateSnapshot(in state));
        }

        internal static bool IsOpcodeEnabled(uint opcodeHash)
        {
            Initialize();
            if (opcodeHash == 0u ||
                !TryOpenVaultLaneRead(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord>.ReadOnly opcodeRecords) ||
                !TryReadRingStateSnapshot(out ModSandboxRingState state))
                return false;

            int count = math.min(state.OpcodeCount, opcodeRecords.Length);
            for (int i = 0; i < count; i++)
            {
                FutureCommandOpcodeRecord record = opcodeRecords[i];
                if (record.OpcodeHash == opcodeHash && (record.Flags & 1u) != 0u)
                    return true;
            }

            return false;
        }

        internal static FutureCommandSandboxTuning GetTuningSnapshot()
        {
            Initialize();
            TryOpenVaultLaneRead(ref _tuningHandle, out NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer);
            return ResolveTuning(tuningBuffer);
        }

        internal static void ApplyTuning(in FutureCommandSandboxTuning tuning)
        {
            Initialize();
            AcquireVaultBuffers();
            FutureCommandSandboxTuning safe = tuning;
            safe.MaxCommandsPerFrame = math.clamp(safe.MaxCommandsPerFrame, FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, 10000);
            safe.MaxModMemoryMb = math.clamp(safe.MaxModMemoryMb, 1, 256);
            safe.MaxAssetBytes = (uint)math.clamp((int)safe.MaxAssetBytes, 1024, 256 * 1024 * 1024);
            safe.CpuThermalPressure01 = math.saturate(math.isfinite(safe.CpuThermalPressure01) ? safe.CpuThermalPressure01 : 0f);
            TryWriteVaultLaneElement(ref _tuningHandle, 0, in safe);
        }

        internal static bool ReportCpuThermalPressure(float pressure01)
        {
            Initialize();
            AcquireVaultBuffers();
            if (!TryOpenVaultLaneRead(ref _tuningHandle, out NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length == 0)
                return false;

            FutureCommandSandboxTuning tuning = tuningBuffer[0];
            tuning.CpuThermalPressure01 = math.saturate(math.isfinite(pressure01) ? pressure01 : 1f);
            return TryWriteVaultLaneElement(ref _tuningHandle, 0, in tuning);
        }

        internal static int CopyTelemetrySnapshot(NativeArray<ModSandboxTelemetryEntry> destination)
        {
            Initialize();
            AcquireVaultBuffers();
            if (!destination.IsCreated ||
                !TryOpenVaultLaneRead(ref _telemetryRingHandle, out NativeArray<ModSandboxTelemetryEntry>.ReadOnly telemetryRing))
                return 0;

            int count = math.min(destination.Length, telemetryRing.Length);
            for (int i = 0; i < count; i++)
                destination[i] = telemetryRing[i];
            return count;
        }

        internal static bool TryGetTelemetryEntry(int index, out ModSandboxTelemetryEntry entry)
        {
            Initialize();
            AcquireVaultBuffers();
            if (!TryOpenVaultLaneRead(ref _telemetryRingHandle, out NativeArray<ModSandboxTelemetryEntry>.ReadOnly telemetryRing) ||
                (uint)index >= (uint)telemetryRing.Length)
            {
                entry = default;
                return false;
            }

            entry = telemetryRing[index];
            return true;
        }

#if UNITY_EDITOR
        internal static bool TryIngestAllowedOpcodesCsv(NativeArray<byte> csvBytes, int byteLength)
        {
            Initialize();
            if (!csvBytes.IsCreated || byteLength <= 0)
                return false;

            AcquireVaultBuffers();
            int opcodeCapacity = IsLaneCreated(in _opcodeRecordsHandle) ? _opcodeRecordsHandle.Length : 0;
            if (opcodeCapacity <= 0 || !TryReadRingStateSnapshot(out ModSandboxRingState state))
                return false;

            int length = math.min(byteLength, csvBytes.Length);
            if (!TryValidateAllowedOpcodesCsv(csvBytes, length, opcodeCapacity, out int expectedAccepted))
                return false;

            state.OpcodeCount = 0;
            if (!TryWriteRingStateSnapshot(in state))
                return false;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord> opcodeRecords, out lockedVault))
                return false;

            int accepted = 0;
            try
            {
                MemClearArray(opcodeRecords);
                int tokenStart = 0;
                for (int cursor = 0; cursor <= length; cursor++)
                {
                    byte b = cursor < length ? csvBytes[cursor] : (byte)'\n';
                    if (b != (byte)'\n' && b != (byte)'\r')
                        continue;

                    int lineLength = cursor - tokenStart;
                    if (!IsOpcodeCsvMetadataLine(csvBytes, tokenStart, lineLength) &&
                        TryParseOpcodeCsvLine(csvBytes, tokenStart, lineLength, out uint opcodeHash) &&
                        opcodeHash != 0u &&
                        AddOpcodeRecord(opcodeRecords, ref state, opcodeHash, 1u))
                    {
                        accepted++;
                    }

                    tokenStart = cursor + 1;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _opcodeRecordsHandle.Handle, SystemID.ModSandbox);
            }

            if (accepted != expectedAccepted)
                return false;

            return TryWriteRingStateSnapshot(in state);
        }
#endif

#if UNITY_EDITOR
        internal static bool TryReloadAllowedOpcodesCsvFromDisk()
        {
            string path = Path.Combine(Application.dataPath, "../Docs/Modding/allowed_opcodes.csv");
            if (!File.Exists(path))
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long fileLength = stream.Length;
                    if (fileLength <= 0L || fileLength > FutureCommandSandboxConstants.KernelCsvScratchBytes)
                        return false;

                    int readLength = (int)fileLength;
                    NativeArray<byte> fileBytes = H8Memory.Allocate<byte>(
                        readLength,
                        SystemID.ModSandbox,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    if (!fileBytes.IsCreated)
                        return false;

                    try
                    {
                        byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                        int read = stream.Read(new Span<byte>(ptr, readLength));
                        if (read != readLength)
                            return false;

                        return TryIngestAllowedOpcodesCsv(fileBytes, read);
                    }
                    finally
                    {
                        H8Memory.Release(ref fileBytes, SystemID.ModSandbox);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool TryReloadKernelTuningProfilesCsvFromDisk()
        {
            string path = Path.Combine(Application.dataPath, "../Docs/Modding/kernel_tuning_profiles.csv");
            if (!File.Exists(path))
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long fileLength = stream.Length;
                    if (fileLength <= 0L || fileLength > FutureCommandSandboxConstants.KernelCsvScratchBytes)
                        return false;

                    int readLength = (int)fileLength;
                    NativeArray<byte> fileBytes = H8Memory.Allocate<byte>(
                        readLength,
                        SystemID.ModSandbox,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    if (!fileBytes.IsCreated)
                        return false;

                    try
                    {
                        byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                        int read = stream.Read(new Span<byte>(ptr, readLength));
                        if (read != readLength)
                            return false;

                        return TryIngestKernelTuningProfilesCsv(new ReadOnlySpan<byte>(ptr, read));
                    }
                    finally
                    {
                        H8Memory.Release(ref fileBytes, SystemID.ModSandbox);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif

#if UNITY_EDITOR
        internal static bool TryIngestKernelTuningProfilesCsv(ReadOnlySpan<byte> csvBytes)
        {
            AcquireVaultBuffers();
            int profileCapacity = IsLaneCreated(in _kernelTuningProfilesHandle) ? _kernelTuningProfilesHandle.Length : 0;
            if (profileCapacity <= 0 || csvBytes.Length == 0)
                return false;

            if (!TryValidateKernelTuningProfilesCsv(csvBytes, profileCapacity, out int profileCount))
                return false;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _kernelTuningProfilesHandle, out NativeArray<ModKernelTuningProfile> profiles, out lockedVault))
                return false;

            int accepted = 0;
            try
            {
                MemClearArray(profiles);
                int lineStart = 0;
                for (int cursor = 0; cursor <= csvBytes.Length; cursor++)
                {
                    byte b = cursor < csvBytes.Length ? csvBytes[cursor] : (byte)'\n';
                    if (b != (byte)'\n' && b != (byte)'\r')
                        continue;

                    int length = cursor - lineStart;
                    ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, length);
                    if (!IsKernelTuningCsvMetadataLine(line) &&
                        TryParseKernelTuningCsvLine(line, out ModKernelTuningProfile profile))
                    {
                        profiles[accepted] = profile;
                        accepted++;
                    }

                    lineStart = cursor + 1;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _kernelTuningProfilesHandle.Handle, SystemID.ModSandbox);
            }

            return accepted == profileCount;
        }
#endif

        internal static void SetRollbackResimulationActive(bool active)
        {
            _rollbackFreezeOverride = active;
        }

        internal static bool RunSelfAudit()
        {
            Initialize();
            if (UnsafeUtility.SizeOf<FutureCommandEnvelope>() != FutureCommandSandboxConstants.EnvelopeSizeBytes)
            {
                DumpBlackbox(FutureCommandSandboxConstants.FaultHashLayout);
                return false;
            }

            TryFinalizeScheduledPreSimulationNoWait();
            if (_scheduledValidationActive)
                return false;

            AcquireVaultBuffers();
            EnsureValidationScratchBuffersCold();
            NativeArray<FutureCommandEnvelope> auditInputs = H8Memory.Allocate<FutureCommandEnvelope>(
                2,
                SystemID.ModSandbox,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            if (!auditInputs.IsCreated)
                return false;

            try
            {
                if (!TryOpenVaultLaneRead(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord>.ReadOnly opcodeRecords) ||
                    !TryOpenVaultLaneRead(ref _memoryLeasesHandle, out NativeArray<ModderMemoryLease>.ReadOnly memoryLeases) ||
                    !TryOpenVaultLaneRead(ref _approvedAssetManifestHandle, out NativeArray<ApprovedAssetRecord>.ReadOnly approvedAssets) ||
                    !TryOpenVaultLaneRead(ref _modderBlackboxMemoryHandle, out NativeArray<byte>.ReadOnly modderBlackboxMemory) ||
                    !TryOpenVaultLaneRead(ref _kernelTuningProfilesHandle, out NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles) ||
                    !TryOpenVaultLaneRead(ref _tuningHandle, out NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer) ||
                    !TryReadRingStateSnapshot(out ModSandboxRingState state) ||
                    !TryPrepareValidationScratchOutputs())
                {
                    return false;
                }

                FutureCommandEnvelope maliciousAup = default;
                maliciousAup.OpcodeHash = FutureCommandOpcodes.SpawnItem;
                maliciousAup.ModderSignature = 0x51554152u;
                maliciousAup.TargetAUP = new double3(double.NaN, 0d, 0d);
                maliciousAup.PayloadData = new float4(1f, 2f, 3f, 4f);
                maliciousAup.IntegrityHash = ComputeIntegrityHash(in maliciousAup);

                FutureCommandEnvelope maliciousPayload = default;
                maliciousPayload.OpcodeHash = FutureCommandOpcodes.FaunaAcousticStimulus;
                maliciousPayload.ModderSignature = 0x51554153u;
                maliciousPayload.TargetAUP = new double3(0d, 0d, 0d);
                maliciousPayload.PayloadData = new float4(float.NaN, 12f, 0f, 0f);
                maliciousPayload.IntegrityHash = ComputeIntegrityHash(in maliciousPayload);

                float quality = ResolveGlobalQualityWeight(tuningBuffer);
                FutureCommandSandboxTuning tuning = ResolveTuning(tuningBuffer);
                int maxPerSignature = ResolveScaledCommandBudget(tuning.MaxCommandsPerFrame, quality);
                auditInputs[0] = maliciousAup;
                auditInputs[1] = maliciousPayload;

                ValidateFutureCommandEnvelopeJob job = new ValidateFutureCommandEnvelopeJob
                {
                    Inputs = auditInputs.AsReadOnly(),
                    ScratchState = _validationScratchState,
                    OpcodeRecords = opcodeRecords,
                    PerModCounters = _validationCounterScratch,
                    MemoryLeases = memoryLeases,
                    ApprovedAssetManifest = approvedAssets,
                    MemoryWriteCommands = _validationMemoryWriteScratch,
                    ModderBlackboxMemoryLength = modderBlackboxMemory.Length,
                    DevNullScratch = _validationDevNullScratch,
                    SpawnWriter = SignalBus<ModSpawnRequestSignal>.ParallelWriter,
                    AssetWriter = SignalBus<ModAssetReferenceSignal>.ParallelWriter,
                    AcousticWriter = SignalBus<SandboxMockAcousticSignal>.ParallelWriter,
                    DamageWriter = SignalBus<MockDamageSignal>.ParallelWriter,
                    DevNullSignalWriter = SignalBus<ModFutureDevNullSignal>.ParallelWriter,
                    SurvivalWriter = SignalBus<SurvivalOverrideSignal>.ParallelWriter,
                    HapticWriter = SignalBus<ModHapticPulseSignal>.ParallelWriter,
                    SubtitleWriter = SignalBus<ModSubtitleCueSignal>.ParallelWriter,
                    RejectionWriter = SignalBus<ModInteractionRejectedPayload>.ParallelWriter,
                    CameraJuiceScratch = _validationCameraImpulseScratch,
                    KernelProfiles = kernelProfiles,
                    Count = 2,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    OpcodeRecordCount = state.OpcodeCount,
                    MaxCommandsPerSignature = maxPerSignature,
                    GlobalQualityWeight = quality,
                    MaxAssetBytes = tuning.MaxAssetBytes,
                    TuningFlags = tuning.Flags,
                    RollbackActive = IsRollbackFrozen() ? 1u : 0u,
                    ObserverAUP = ResolveObserverAup()
                };

                job.Execute();
                FutureCommandValidationStats stats = _validationScratchState[0].Stats;
                bool rejectedInvalidPackets =
                    stats.Incoming == 2u &&
                    stats.Valid == 0u &&
                    stats.Rejected == 2u &&
                    (stats.RejectionMask & (uint)FutureCommandRejectReason.InvalidAup) != 0u &&
                    (stats.RejectionMask & (uint)FutureCommandRejectReason.InvalidPayload) != 0u;
                bool exactAupTelemetry = stats.AupViolations == 1u;
                ModSandboxRingState overflowProbe = default;
                overflowProbe.PendingCount = auditInputs.Length;
                overflowProbe.PendingHead = 0;
                overflowProbe.PendingTail = 0;
                EnqueuePendingEnvelope(auditInputs, ref overflowProbe, in maliciousPayload);
                bool overflowCounterWorked =
                    overflowProbe.PendingOverflowDropped == 1u &&
                    overflowProbe.PendingCount == auditInputs.Length &&
                    overflowProbe.PendingHead == 1;
                bool selfAuditPassed = rejectedInvalidPackets && exactAupTelemetry && overflowCounterWorked;
                uint auditFaultHash = rejectedInvalidPackets
                    ? FutureCommandSandboxConstants.FaultHashLayout
                    : FutureCommandSandboxConstants.FaultHashInvalidPayload;
                RecordTelemetry(
                    Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    stats.Incoming,
                    stats.Valid,
                    stats.Rejected,
                    stats.Dropped,
                    stats.DevNull,
                    0UL,
                    quality,
                    stats.RejectionMask,
                    selfAuditPassed ? 0u : auditFaultHash,
                    (uint)state.PendingCount,
                    stats.PeakCommandsForSignature,
                    (uint)maxPerSignature);

                if (!selfAuditPassed)
                    DumpBlackbox(auditFaultHash);
                return selfAuditPassed;
            }
            finally
            {
                H8Memory.Release(ref auditInputs, SystemID.ModSandbox);
            }
        }

        internal static ulong ComputeIntegrityHash(in FutureCommandEnvelope envelope)
        {
            FutureCommandEnvelope copy = envelope;
            copy.IntegrityHash = 0UL;
            copy._pad0 = 0UL;
            return MemorySentinelMath.ComputeDeterministicHash64(&copy, 48);
        }

#if UNITY_EDITOR
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

        private static bool TryValidateAllowedOpcodesCsv(NativeArray<byte> bytes, int length, int capacity, out int accepted)
        {
            accepted = 0;
            int tokenStart = 0;
            for (int cursor = 0; cursor <= length; cursor++)
            {
                byte b = cursor < length ? bytes[cursor] : (byte)'\n';
                if (b != (byte)'\n' && b != (byte)'\r')
                    continue;

                int lineLength = cursor - tokenStart;
                if (IsOpcodeCsvMetadataLine(bytes, tokenStart, lineLength))
                {
                    tokenStart = cursor + 1;
                    continue;
                }

                if (!TryParseOpcodeCsvLine(bytes, tokenStart, lineLength, out uint opcodeHash) ||
                    opcodeHash == 0u ||
                    !IsRuntimeAllowedFutureCommandOpcode(opcodeHash) ||
                    ContainsOpcodeCsvLineBefore(bytes, tokenStart, opcodeHash))
                {
                    return false;
                }

                accepted++;
                if (accepted > capacity)
                    return false;

                tokenStart = cursor + 1;
            }

            return accepted > 0;
        }

        private static bool ContainsOpcodeCsvLineBefore(NativeArray<byte> bytes, int stopExclusive, uint opcodeHash)
        {
            int tokenStart = 0;
            for (int cursor = 0; cursor <= stopExclusive; cursor++)
            {
                byte b = cursor < stopExclusive ? bytes[cursor] : (byte)'\n';
                if (b != (byte)'\n' && b != (byte)'\r')
                    continue;

                int lineLength = cursor - tokenStart;
                if (!IsOpcodeCsvMetadataLine(bytes, tokenStart, lineLength) &&
                    TryParseOpcodeCsvLine(bytes, tokenStart, lineLength, out uint previousHash) &&
                    previousHash == opcodeHash)
                {
                    return true;
                }

                tokenStart = cursor + 1;
            }

            return false;
        }

        private static bool IsOpcodeCsvMetadataLine(NativeArray<byte> bytes, int start, int length)
        {
            if (length <= 0)
                return true;

            int end = start + length;
            while (start < end && IsWhitespace(bytes[start]))
                start++;
            while (end > start && IsWhitespace(bytes[end - 1]))
                end--;
            if (start >= end || bytes[start] == (byte)'#')
                return true;

            int tokenEnd = start;
            while (tokenEnd < end && bytes[tokenEnd] != (byte)',' && !IsWhitespace(bytes[tokenEnd]))
                tokenEnd++;

            int tokenLength = tokenEnd - start;
            return IsAsciiToken(bytes, start, tokenLength, "opcode") ||
                IsAsciiToken(bytes, start, tokenLength, "opcodehash");
        }

        private static bool TryParseKernelTuningCsvLine(ReadOnlySpan<byte> line, out ModKernelTuningProfile profile)
        {
            profile = default;
            line = TrimAscii(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> opcodeToken = NextCsvToken(line, 0, out int next);
            if (IsAsciiToken(opcodeToken, "opcode") || IsAsciiToken(opcodeToken, "opcodehash"))
                return false;

            if (CountCsvDelimiters(line) != 6)
                return false;

            if (!TryParseOpcodeToken(opcodeToken, out uint opcodeHash))
                return false;

            ReadOnlySpan<byte> priorityToken = NextCsvToken(line, next, out next);
            ReadOnlySpan<byte> maxPerFrameToken = NextCsvToken(line, next, out next);
            ReadOnlySpan<byte> flagsToken = NextCsvToken(line, next, out next);
            ReadOnlySpan<byte> rangeToken = NextCsvToken(line, next, out next);
            ReadOnlySpan<byte> maxDurationToken = NextCsvToken(line, next, out next);
            ReadOnlySpan<byte> intensityScaleToken = NextCsvToken(line, next, out next);
            if (!TryParseFloatAscii(priorityToken, out float priority) ||
                !TryParseIntAscii(maxPerFrameToken, out int maxPerFrame) ||
                !TryParseUIntTokenStrict(flagsToken, out uint flags) ||
                !TryParseFloatAscii(rangeToken, out float range) ||
                !TryParseFloatAscii(maxDurationToken, out float maxDuration) ||
                !TryParseFloatAscii(intensityScaleToken, out float intensityScale))
            {
                return false;
            }

            if (!IsKernelTuningSemanticRangeValid(priority, maxPerFrame, range, maxDuration, intensityScale))
                return false;

            if (next < line.Length && TrimAscii(line.Slice(next)).Length != 0)
                return false;

            profile = new ModKernelTuningProfile
            {
                OpcodeHash = opcodeHash,
                PriorityWeight = priority,
                MaxPerFrame = maxPerFrame,
                Flags = flags,
                MaxDurationSeconds = maxDuration,
                RangeMeters = range,
                IntensityScale = intensityScale,
                Reserved = 0u
            };
            return true;
        }

        private static bool IsKernelTuningSemanticRangeValid(
            float priority,
            int maxPerFrame,
            float range,
            float maxDuration,
            float intensityScale)
        {
            return priority >= 0f && priority <= 1f &&
                maxPerFrame > 0 &&
                maxPerFrame <= FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame &&
                range >= 1f && range <= (float)FutureCommandSandboxConstants.KernelMaxAupMagnitudeMeters &&
                maxDuration >= 0.01f && maxDuration <= 30f &&
                intensityScale >= 0f;
        }

        private static bool TryValidateKernelTuningProfilesCsv(ReadOnlySpan<byte> csvBytes, int capacity, out int accepted)
        {
            accepted = 0;
            int lineStart = 0;
            for (int cursor = 0; cursor <= csvBytes.Length; cursor++)
            {
                byte b = cursor < csvBytes.Length ? csvBytes[cursor] : (byte)'\n';
                if (b != (byte)'\n' && b != (byte)'\r')
                    continue;

                int length = cursor - lineStart;
                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, length);
                if (IsKernelTuningCsvMetadataLine(line))
                {
                    lineStart = cursor + 1;
                    continue;
                }

                if (!TryParseKernelTuningCsvLine(line, out ModKernelTuningProfile profile) ||
                    ContainsKernelTuningProfileBefore(csvBytes, lineStart, profile.OpcodeHash))
                    return false;

                accepted++;
                if (accepted > capacity)
                    return false;

                lineStart = cursor + 1;
            }

            return accepted > 0;
        }

        private static bool ContainsKernelTuningProfileBefore(ReadOnlySpan<byte> csvBytes, int stopExclusive, uint opcodeHash)
        {
            int lineStart = 0;
            for (int cursor = 0; cursor <= stopExclusive; cursor++)
            {
                byte b = cursor < stopExclusive ? csvBytes[cursor] : (byte)'\n';
                if (b != (byte)'\n' && b != (byte)'\r')
                    continue;

                int length = cursor - lineStart;
                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, length);
                if (!IsKernelTuningCsvMetadataLine(line) &&
                    TryParseKernelTuningCsvLine(line, out ModKernelTuningProfile previous) &&
                    previous.OpcodeHash == opcodeHash)
                {
                    return true;
                }

                lineStart = cursor + 1;
            }

            return false;
        }

        private static bool IsKernelTuningCsvMetadataLine(ReadOnlySpan<byte> line)
        {
            line = TrimAscii(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return true;

            ReadOnlySpan<byte> opcodeToken = NextCsvToken(line, 0, out _);
            return IsAsciiToken(opcodeToken, "opcode") || IsAsciiToken(opcodeToken, "opcodehash");
        }

        private static bool IsAsciiToken(NativeArray<byte> bytes, int start, int length, string literal)
        {
            if (length != literal.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte b = bytes[start + i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)literal[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> NextCsvToken(ReadOnlySpan<byte> line, int start, out int next)
        {
            int cursor = math.clamp(start, 0, line.Length);
            int tokenStart = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            next = cursor < line.Length ? cursor + 1 : line.Length;
            return TrimAscii(line.Slice(tokenStart, cursor - tokenStart));
        }

        private static int CountCsvDelimiters(ReadOnlySpan<byte> line)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == (byte)',')
                    count++;
            }

            return count;
        }

        private static bool TryParseUIntTokenStrict(ReadOnlySpan<byte> token, out uint value)
        {
            if (TryParseHex32(token, out value))
                return true;
            return TryParseUIntAscii(token, out value);
        }

        private static bool TryParseOpcodeToken(ReadOnlySpan<byte> token, out uint opcodeHash)
        {
            opcodeHash = 0u;
            if (token.Length == 0)
                return false;

            if (TryParseHex32(token, out opcodeHash))
                return opcodeHash != 0u;

            opcodeHash = ComputeFnv1A32(token);
            return opcodeHash != 0u;
        }
#endif

        private static bool IsAsciiToken(ReadOnlySpan<byte> token, string literal)
        {
            token = TrimAscii(token);
            if (token.Length != literal.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)literal[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length;
            while (start < end && IsWhitespace(token[start]))
                start++;
            while (end > start && IsWhitespace(token[end - 1]))
                end--;
            return token.Slice(start, end - start);
        }

        private static bool TryParseHex32(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = TrimAscii(token);
            int start = 0;
            if (token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X'))
                start = 2;
            int length = token.Length - start;
            if (length <= 0 || length > 8)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte b = token[start + i];
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

        private static bool TryParseUIntAscii(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                uint digit = (uint)(b - (byte)'0');
                if (value > (uint.MaxValue - digit) / 10u)
                    return false;

                value = value * 10u + digit;
            }

            return true;
        }

        private static bool TryParseIntAscii(ReadOnlySpan<byte> token, out int value)
        {
            value = 0;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            int start = 0;
            int sign = 1;
            if (token[0] == (byte)'-')
            {
                sign = -1;
                start = 1;
            }

            bool digitSeen = false;
            int accumulator = 0;
            for (int i = start; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                digitSeen = true;
                int digit = b - (byte)'0';
                if (accumulator > (int.MaxValue - digit) / 10)
                    return false;

                accumulator = accumulator * 10 + digit;
            }

            if (!digitSeen)
                return false;

            value = accumulator * sign;
            return true;
        }

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            int start = 0;
            float sign = 1f;
            if (token[0] == (byte)'-')
            {
                sign = -1f;
                start = 1;
            }

            float whole = 0f;
            float fraction = 0f;
            float divisor = 1f;
            bool decimalSeen = false;
            bool digitSeen = false;
            for (int i = start; i < token.Length; i++)
            {
                byte b = token[i];
                if (b == (byte)'.' && !decimalSeen)
                {
                    decimalSeen = true;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                digitSeen = true;
                int digit = b - (byte)'0';
                if (decimalSeen)
                {
                    divisor *= 10f;
                    fraction += digit / divisor;
                }
                else
                {
                    whole = whole * 10f + digit;
                }
            }

            if (!digitSeen)
                return false;

            value = (whole + fraction) * sign;
            return math.isfinite(value);
        }

        private static uint ComputeFnv1A32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'a' && b <= (byte)'z')
                    b = (byte)(b - 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static float DefaultPriorityForOpcode(uint opcodeHash)
        {
            if (opcodeHash == FutureCommandOpcodes.SurvivalOverride)
                return 1f;
            if (opcodeHash == FutureCommandOpcodes.HapticPulse)
                return 0.35f;
            if (opcodeHash == FutureCommandOpcodes.SubtitleCue)
                return 0.25f;
            return 0.5f;
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
            if (!TryReadRingStateSnapshot(out ModSandboxRingState state))
                return;

            state.OpcodeCount = 0;
            if (!TryWriteRingStateSnapshot(in state))
                return;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord> opcodeRecords, out lockedVault))
                return;

            try
            {
                MemClearArray(opcodeRecords);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.SpawnItem, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AlterHealth, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AlterGravity, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.AssetReference, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.ModMemoryRead, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.ModMemoryWrite, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.FaunaAcousticStimulus, 1u);
                AddEmergencyOpcode(opcodeRecords, ref state, FutureCommandOpcodes.FaunaDamageStimulus, 1u);
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _opcodeRecordsHandle.Handle, SystemID.ModSandbox);
            }

            if (TryWriteRingStateSnapshot(in state))
                GenerateEmergencyOpcodeMap();
        }

        internal static void GenerateEmergencyOpcodeMap()
        {
            if (!TryOpenVaultLaneRead(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord>.ReadOnly opcodeRecords) ||
                !TryReadRingStateSnapshot(out _))
                return;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _kernelOpcodeMapHandle, out NativeArray<ModCommandKernelOpcodeRecord> kernelMap, out lockedVault))
                return;

            try
            {
                MemClearArray(kernelMap);
                // Reserved command kernels remain dormant until an owning runtime system makes them public.
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _kernelOpcodeMapHandle.Handle, SystemID.ModSandbox);
            }
        }

        private static bool IsRuntimeAllowedFutureCommandOpcode(uint opcodeHash)
        {
            switch (opcodeHash)
            {
                case FutureCommandOpcodes.SpawnItem:
                case FutureCommandOpcodes.AlterHealth:
                case FutureCommandOpcodes.AlterGravity:
                case FutureCommandOpcodes.AssetReference:
                case FutureCommandOpcodes.ModMemoryRead:
                case FutureCommandOpcodes.ModMemoryWrite:
                case FutureCommandOpcodes.FaunaAcousticStimulus:
                case FutureCommandOpcodes.FaunaDamageStimulus:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddEmergencyOpcode(NativeArray<FutureCommandOpcodeRecord> opcodeRecords, ref ModSandboxRingState state, uint opcodeHash, uint flags)
        {
            if (!IsRuntimeAllowedFutureCommandOpcode(opcodeHash))
                return;

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

            EnsureModderLease(signature, maxMemoryMb, modderBlackboxMemory.Length, memoryLeases, ref state, frame);
        }

        private static void EnsureModderLease(
            uint signature,
            int maxMemoryMb,
            int memoryBytes,
            NativeArray<ModderMemoryLease> memoryLeases,
            ref ModSandboxRingState state,
            uint frame)
        {
            if (signature == 0u || !memoryLeases.IsCreated || memoryBytes <= 0)
                return;

            int slot = FindModderLeaseSlot(memoryLeases, signature, out bool found);
            if (slot < 0 || found)
                return;

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

        private static FutureCommandSandboxTuning ResolveTuning(NativeArray<FutureCommandSandboxTuning> tuningBuffer)
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

        private static FutureCommandSandboxTuning ResolveTuning(NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer)
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

            if (tuningBuffer.Length > 0)
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
            TryOpenVaultLaneRead(ref _tuningHandle, out NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer);
            return ResolveGlobalQualityWeight(tuningBuffer);
        }

        private static float ResolveGlobalQualityWeight(NativeArray<FutureCommandSandboxTuning> tuningBuffer)
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                FutureCommandSandboxTuning stored = tuningBuffer[0];
                float overrideWeight = stored.GlobalQualityWeightOverride;
                if (math.isfinite(overrideWeight) && overrideWeight >= 0f)
                    weight = overrideWeight;

                float pressure = math.saturate(math.isfinite(stored.CpuThermalPressure01)
                    ? stored.CpuThermalPressure01
                    : 1f);
                float pressureCurve = pressure * pressure * (3f - 2f * pressure);
                weight = math.lerp(weight, 0f, pressureCurve);
            }

            return math.saturate(math.isfinite(weight) ? weight : 0f);
        }

        private static float ResolveGlobalQualityWeight(NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer)
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (tuningBuffer.Length > 0)
            {
                FutureCommandSandboxTuning stored = tuningBuffer[0];
                float overrideWeight = stored.GlobalQualityWeightOverride;
                if (math.isfinite(overrideWeight) && overrideWeight >= 0f)
                    weight = overrideWeight;

                float pressure = math.saturate(math.isfinite(stored.CpuThermalPressure01)
                    ? stored.CpuThermalPressure01
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
            float curve = q * q;
            float scaled = math.lerp(FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, safeBase, curve);
            return math.max(FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, (int)math.round(scaled));
        }

        private static int ResolveKernelProfileFrameBudget(NativeArray<ModKernelTuningProfile> profiles, int fallbackBudget)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return fallbackBudget;

            int sum = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                ModKernelTuningProfile profile = profiles[i];
                if (profile.OpcodeHash == 0u)
                    continue;
                if (profile.MaxPerFrame > 0)
                {
                    int profileBudget = math.min(profile.MaxPerFrame, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame);
                    int remainingBudget = math.max(0, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame - sum);
                    sum += math.min(profileBudget, remainingBudget);
                }
            }

            if (sum <= 0)
                return fallbackBudget;

            return math.clamp(sum, FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, fallbackBudget);
        }

        private static int ResolveKernelProfileFrameBudget(NativeArray<ModKernelTuningProfile>.ReadOnly profiles, int fallbackBudget)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return fallbackBudget;

            int sum = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                ModKernelTuningProfile profile = profiles[i];
                if (profile.OpcodeHash == 0u)
                    continue;
                if (profile.MaxPerFrame > 0)
                {
                    int profileBudget = math.min(profile.MaxPerFrame, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame);
                    int remainingBudget = math.max(0, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame - sum);
                    sum += math.min(profileBudget, remainingBudget);
                }
            }

            if (sum <= 0)
                return fallbackBudget;

            return math.clamp(sum, FutureCommandSandboxConstants.LowTierMinCommandsPerSignature, fallbackBudget);
        }

        private static int ResolveSmallestKernelProfileFrameBudget(NativeArray<ModKernelTuningProfile> profiles, int fallbackBudget)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return fallbackBudget;

            int smallest = fallbackBudget;
            for (int i = 0; i < profiles.Length; i++)
            {
                ModKernelTuningProfile profile = profiles[i];
                if (profile.OpcodeHash == 0u)
                    continue;
                if (profile.MaxPerFrame > 0)
                    smallest = math.min(smallest, math.min(profile.MaxPerFrame, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame));
            }

            return smallest;
        }

        private static int ResolveSmallestKernelProfileFrameBudget(NativeArray<ModKernelTuningProfile>.ReadOnly profiles, int fallbackBudget)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return fallbackBudget;

            int smallest = fallbackBudget;
            for (int i = 0; i < profiles.Length; i++)
            {
                ModKernelTuningProfile profile = profiles[i];
                if (profile.OpcodeHash == 0u)
                    continue;
                if (profile.MaxPerFrame > 0)
                    smallest = math.min(smallest, math.min(profile.MaxPerFrame, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame));
            }

            return smallest;
        }

        private static void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            CompleteScheduledPreSimulationForBarrier();
            ReleaseVaultHandles(_dataVault);
            _dataVault = vault;

            if (_initialized && vault != null)
                AcquireVaultBuffers();
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultLane(vault, ref _pendingRingHandle);
            ReleaseVaultLane(vault, ref _devNullRingHandle);
            ReleaseVaultLane(vault, ref _stagingHandle);
            ReleaseVaultLane(vault, ref _statsHandle);
            ReleaseVaultLane(vault, ref _opcodeRecordsHandle);
            ReleaseVaultLane(vault, ref _telemetryRingHandle);
            ReleaseVaultLane(vault, ref _telemetryCursorHandle);
            ReleaseVaultLane(vault, ref _modderBlackboxMemoryHandle);
            ReleaseVaultLane(vault, ref _tuningHandle);
            ReleaseVaultLane(vault, ref _perModCountersHandle);
            ReleaseVaultLane(vault, ref _memoryLeasesHandle);
            ReleaseVaultLane(vault, ref _approvedAssetManifestHandle);
            ReleaseVaultLane(vault, ref _ringStateHandle);
            ReleaseVaultLane(vault, ref _kernelOpcodeMapHandle);
            ReleaseVaultLane(vault, ref _kernelTelemetryRingHandle);
            ReleaseVaultLane(vault, ref _kernelTelemetryCursorHandle);
            ReleaseVaultLane(vault, ref _kernelCameraJuiceImpulseHandle);
            ReleaseVaultLane(vault, ref _kernelCameraJuiceStateHandle);
            ReleaseVaultLane(vault, ref _kernelTuningProfilesHandle);
        }

        private static void EnsureValidationScratchBuffersCold()
        {
            if (!_validationScratchState.IsCreated)
            {
                _validationScratchState = H8Memory.Allocate<ModSandboxValidationScratchState>(
                    1,
                    SystemID.ModSandbox,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: tracked validation output state, owner: ModSandbox
            }

            if (!_validationCounterScratch.IsCreated)
            {
                _validationCounterScratch = H8Memory.Allocate<ModderFrameCounter>(
                    FutureCommandSandboxConstants.MaxTrackedModders,
                    SystemID.ModSandbox,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: tracked mod counter scratch, owner: ModSandbox
            }

            if (!_validationMemoryWriteScratch.IsCreated)
            {
                _validationMemoryWriteScratch = H8Memory.Allocate<ModSandboxMemoryWriteCommand>(
                    FutureCommandSandboxConstants.StagingCapacity,
                    SystemID.ModSandbox,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: tracked memory write command scratch, owner: ModSandbox
            }

            if (!_validationDevNullScratch.IsCreated)
            {
                _validationDevNullScratch = H8Memory.Allocate<FutureCommandEnvelope>(
                    FutureCommandSandboxConstants.StagingCapacity,
                    SystemID.ModSandbox,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: tracked dev-null output scratch, owner: ModSandbox
            }

            if (!_validationCameraImpulseScratch.IsCreated)
            {
                _validationCameraImpulseScratch = H8Memory.Allocate<ModKernelCameraJuiceImpulse>(
                    FutureCommandSandboxConstants.CameraJuiceImpulseCapacity,
                    SystemID.ModSandbox,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: tracked haptic fallback camera scratch, owner: ModSandbox
            }
        }

        private static void ReleaseValidationScratchBuffers()
        {
            try
            {
                ReleaseValidationScratchBuffer(ref _validationScratchState);
            }
            finally
            {
                try
                {
                    ReleaseValidationScratchBuffer(ref _validationCounterScratch);
                }
                finally
                {
                    try
                    {
                        ReleaseValidationScratchBuffer(ref _validationMemoryWriteScratch);
                    }
                    finally
                    {
                        try
                        {
                            ReleaseValidationScratchBuffer(ref _validationDevNullScratch);
                        }
                        finally
                        {
                            ReleaseValidationScratchBuffer(ref _validationCameraImpulseScratch);
                        }
                    }
                }
            }
        }

        private static void ReleaseValidationScratchBuffer<T>(ref NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            bool memoryTrackerAvailable = H8Memory.IsInitialized;
            if (memoryTrackerAvailable)
                H8Memory.Release(ref buffer, SystemID.ModSandbox);

            if (buffer.IsCreated)
                buffer.Dispose();

            buffer = default;
        }

        private static void ReleaseVaultLane<T>(IDataVault vault, ref VaultLane<T> lane) where T : struct
        {
            if (vault != null && IsLaneCreated(in lane))
                vault.ReleaseBuffer(in lane.Handle);

            lane = default;
        }

        private static bool IsRollbackFrozen()
        {
            if (_rollbackFreezeOverride)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !TryReadVaultBuffer(
                    vault,
                    (BufferID)RollbackRuntimeStateBufferId,
                    out NativeArray<RollbackRuntimeStateFlagView>.ReadOnly rollback) ||
                !rollback.IsCreated ||
                rollback.Length <= 0)
            {
                return false;
            }

            return (rollback[0].Flags & RollbackFlagResimulating) != 0u;
        }

        private static void AcquireVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool coldAcquire = !IsLaneCreated(in _ringStateHandle);

            _pendingRingHandle = AcquireVaultLane<FutureCommandEnvelope>(
                vault,
                BufferID.ShinobuModSandboxPendingRing,
                FutureCommandSandboxConstants.PendingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _devNullRingHandle = AcquireVaultLane<FutureCommandEnvelope>(
                vault,
                BufferID.ShinobuModSandboxDevNullRing,
                FutureCommandSandboxConstants.PendingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _stagingHandle = AcquireVaultLane<FutureCommandEnvelope>(
                vault,
                BufferID.ShinobuModSandboxStaging,
                FutureCommandSandboxConstants.StagingCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _statsHandle = AcquireVaultLane<FutureCommandValidationStats>(
                vault,
                BufferID.ShinobuModSandboxStats,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _opcodeRecordsHandle = AcquireVaultLane<FutureCommandOpcodeRecord>(
                vault,
                BufferID.ShinobuModSandboxOpcodeRecords,
                FutureCommandSandboxConstants.OpcodeRecordCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _perModCountersHandle = AcquireVaultLane<ModderFrameCounter>(
                vault,
                BufferID.ShinobuModSandboxModCounters,
                FutureCommandSandboxConstants.MaxTrackedModders,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _memoryLeasesHandle = AcquireVaultLane<ModderMemoryLease>(
                vault,
                BufferID.ShinobuModSandboxMemoryLeases,
                FutureCommandSandboxConstants.MaxTrackedModders,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _approvedAssetManifestHandle = AcquireVaultLane<ApprovedAssetRecord>(
                vault,
                BufferID.ShinobuModSandboxApprovedAssets,
                FutureCommandSandboxConstants.ApprovedAssetCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _modderBlackboxMemoryHandle = AcquireVaultLane<byte>(
                vault,
                BufferID.ShinobuModSandboxBlackboxMemory,
                DefaultMemoryBytes,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = AcquireVaultLane<ModSandboxTelemetryEntry>(
                vault,
                BufferID.ShinobuModSandboxTelemetryRing,
                FutureCommandSandboxConstants.TelemetryCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = AcquireVaultLane<int>(
                vault,
                BufferID.ShinobuModSandboxTelemetryCursor,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = AcquireVaultLane<FutureCommandSandboxTuning>(
                vault,
                BufferID.ShinobuModSandboxTuning,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _ringStateHandle = AcquireVaultLane<ModSandboxRingState>(
                vault,
                BufferID.ShinobuModSandboxRingState,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelOpcodeMapHandle = AcquireVaultLane<ModCommandKernelOpcodeRecord>(
                vault,
                (BufferID)KernelOpcodeMapBufferId,
                FutureCommandSandboxConstants.KernelOpcodeMapCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelTelemetryRingHandle = AcquireVaultLane<KernelExecutionTelemetryEntry>(
                vault,
                (BufferID)KernelTelemetryRingBufferId,
                FutureCommandSandboxConstants.KernelTelemetryCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelTelemetryCursorHandle = AcquireVaultLane<int>(
                vault,
                (BufferID)KernelTelemetryCursorBufferId,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelCameraJuiceImpulseHandle = AcquireVaultLane<ModKernelCameraJuiceImpulse>(
                vault,
                (BufferID)KernelCameraJuiceImpulseBufferId,
                FutureCommandSandboxConstants.CameraJuiceImpulseCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelCameraJuiceStateHandle = AcquireVaultLane<ModKernelCameraJuiceState>(
                vault,
                (BufferID)KernelCameraJuiceStateBufferId,
                1,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            _kernelTuningProfilesHandle = AcquireVaultLane<ModKernelTuningProfile>(
                vault,
                (BufferID)KernelTuningProfilesBufferId,
                FutureCommandSandboxConstants.KernelTuningCapacity,
                SystemID.ModSandbox,
                NativeArrayOptions.UninitializedMemory);
            if (!coldAcquire)
                return;

            if (!TryClearVaultLane(ref _pendingRingHandle) ||
                !TryClearVaultLane(ref _devNullRingHandle) ||
                !TryClearVaultLane(ref _stagingHandle) ||
                !TryClearVaultLane(ref _statsHandle) ||
                !TryClearVaultLane(ref _opcodeRecordsHandle) ||
                !TryClearVaultLane(ref _perModCountersHandle) ||
                !TryClearVaultLane(ref _memoryLeasesHandle) ||
                !TryClearVaultLane(ref _approvedAssetManifestHandle) ||
                !TryClearVaultLane(ref _modderBlackboxMemoryHandle) ||
                !TryClearVaultLane(ref _telemetryRingHandle) ||
                !TryClearVaultLane(ref _telemetryCursorHandle) ||
                !TryClearVaultLane(ref _tuningHandle) ||
                !TryClearVaultLane(ref _ringStateHandle) ||
                !TryClearVaultLane(ref _kernelOpcodeMapHandle) ||
                !TryClearVaultLane(ref _kernelTelemetryRingHandle) ||
                !TryClearVaultLane(ref _kernelTelemetryCursorHandle) ||
                !TryClearVaultLane(ref _kernelCameraJuiceStateHandle) ||
                !TryClearVaultLane(ref _kernelTuningProfilesHandle))
            {
                return;
            }

            ModSandboxRingState state = default;
            state.LastDumpFrame = -1;
            if (!TryWriteRingStateSnapshot(in state))
                return;

            FutureCommandSandboxTuning tuning = new FutureCommandSandboxTuning
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
            if (!TryWriteVaultLaneElement(ref _tuningHandle, 0, in tuning))
                return;
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
            if (!TryOpenVaultLaneRead(ref _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryCursor.Length == 0)
                return;

            int cursor = telemetryCursor[0];
            int nextCursor = cursor;
            ModSandboxTelemetryEntry entry;
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _telemetryRingHandle, out NativeArray<ModSandboxTelemetryEntry> telemetryRing, out lockedVault))
            {
                return;
            }

            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            entry = new ModSandboxTelemetryEntry
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

            try
            {
                telemetryRing[cursor] = entry;
                nextCursor = cursor + 1;
                if (nextCursor >= telemetryRing.Length)
                    nextCursor = 0;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _telemetryRingHandle.Handle, SystemID.ModSandbox);
            }

            TryWriteVaultLaneElement(ref _telemetryCursorHandle, 0, nextCursor);
        }

        internal static bool TryGetKernelTelemetryEntry(int index, out KernelExecutionTelemetryEntry entry)
        {
            entry = default;
            if (!TryOpenVaultLaneRead(ref _kernelTelemetryRingHandle, out NativeArray<KernelExecutionTelemetryEntry>.ReadOnly telemetryRing) ||
                telemetryRing.Length == 0 ||
                (uint)index >= (uint)telemetryRing.Length)
                return false;

            entry = telemetryRing[index];
            return true;
        }

        private static void RecordKernelTelemetry(
            uint frame,
            in FutureCommandValidationStats stats,
            uint thermalDropped,
            ulong elapsedTicks,
            float quality,
            uint pendingDepth)
        {
            if (!TryOpenVaultLaneRead(ref _kernelTelemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryCursor.Length == 0)
                return;

            int cursor = telemetryCursor[0];
            int nextCursor = cursor;
            KernelExecutionTelemetryEntry entry;
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _kernelTelemetryRingHandle, out NativeArray<KernelExecutionTelemetryEntry> telemetryRing, out lockedVault))
            {
                return;
            }

            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            uint rollbackSuppressed = (stats.RejectionMask & (uint)FutureCommandRejectReason.RollbackSuppressed) != 0u
                ? stats.KernelSuppressed
                : 0u;
            entry = new KernelExecutionTelemetryEntry
            {
                ExecutionTicks = elapsedTicks,
                Frame = frame,
                SurvivalProcessed = stats.SurvivalProcessed,
                HapticProcessed = stats.HapticProcessed,
                SubtitleProcessed = stats.SubtitleProcessed,
                ShedByThermal = thermalDropped,
                Rejected = stats.Rejected,
                RollbackSuppressed = rollbackSuppressed,
                HapticFallbacks = stats.HapticFallbacks,
                GlobalQualityWeight = quality,
                AupViolations = stats.AupViolations,
                PendingQueueDepth = pendingDepth,
                FaultHash = stats.FaultHash,
                Flags = stats.RejectionMask,
                _pad0 = 0u
            };

            try
            {
                telemetryRing[cursor] = entry;
                nextCursor = cursor + 1;
                if (nextCursor >= telemetryRing.Length)
                    nextCursor = 0;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _kernelTelemetryRingHandle.Handle, SystemID.ModSandbox);
            }

            TryWriteVaultLaneElement(ref _kernelTelemetryCursorHandle, 0, nextCursor);

            long thresholdTicks = (Stopwatch.Frequency * KernelSpikeTicksNumerator) / KernelSpikeTicksDenominator;
            long safeThresholdTicks = thresholdTicks > 1L ? thresholdTicks : 1L;
            if (elapsedTicks > (ulong)safeThresholdTicks)
                DumpKernelTelemetry(FutureCommandSandboxConstants.FaultHashKernelSpike);
        }

        private static void DumpKernelTelemetry(uint faultHash)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            bool hasTelemetryRing = TryOpenVaultLaneRead(ref _kernelTelemetryRingHandle, out NativeArray<KernelExecutionTelemetryEntry>.ReadOnly telemetryRing);
            NativeArray<byte> payload = default;
            try
            {
                const int headerBytes = 24;
                int entryBytes = UnsafeUtility.SizeOf<KernelExecutionTelemetryEntry>();
                int entryCount = hasTelemetryRing ? telemetryRing.Length : 0;
                int byteCount = headerBytes + entryCount * entryBytes;
                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, 0x4B464F52u);
                    WriteUInt(bytes, 4, unchecked((uint)frame));
                    WriteUInt(bytes, 8, faultHash);
                    WriteUInt(bytes, 12, unchecked((uint)entryCount));
                    WriteULong(bytes, 16, 0UL);

                    if (entryCount > 0)
                    {
                        byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                        UnsafeUtility.MemCpy(bytes + headerBytes, ptr, entryCount * entryBytes);
                    }
                }

                NativeFaultDumpWriter.TryWriteAll(KernelDumpPath, payload, byteCount);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
            }
        }

        internal static void DumpBlackbox(uint faultHash)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastLocalDumpFrame == frame)
                return;

            TryReadRingStateSnapshot(out ModSandboxRingState state);
            if (state.LastDumpFrame == frame)
                return;

            state.LastDumpFrame = frame;
            _lastLocalDumpFrame = frame;
            TryWriteRingStateSnapshot(in state);

            bool hasTelemetryRing = TryOpenVaultLaneRead(ref _telemetryRingHandle, out NativeArray<ModSandboxTelemetryEntry>.ReadOnly telemetryRing);
            NativeArray<byte> payload = default;
            try
            {
                const int headerBytes = 32;
                int entryBytes = UnsafeUtility.SizeOf<ModSandboxTelemetryEntry>();
                int entryCount = hasTelemetryRing ? telemetryRing.Length : 0;
                int byteCount = headerBytes + entryCount * entryBytes;
                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, 0x514D4F44u);
                    WriteUInt(bytes, 4, unchecked((uint)frame));
                    WriteUInt(bytes, 8, faultHash);
                    WriteUInt(bytes, 12, unchecked((uint)entryCount));
                    WriteUInt(bytes, 16, unchecked((uint)state.PendingCount));
                    WriteUInt(bytes, 20, unchecked((uint)state.DevNullCount));
                    WriteULong(bytes, 24, 0UL);

                    if (entryCount > 0)
                    {
                        byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                        UnsafeUtility.MemCpy(bytes + headerBytes, ptr, entryCount * entryBytes);
                    }
                }

                NativeFaultDumpWriter.TryWriteAll(DumpPath, payload, byteCount);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (payload.IsCreated)
                    payload.Dispose();
            }
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteULong(byte* data, int offset, ulong value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
            data[offset + 4] = (byte)(value >> 32);
            data[offset + 5] = (byte)(value >> 40);
            data[offset + 6] = (byte)(value >> 48);
            data[offset + 7] = (byte)(value >> 56);
        }

        private static void ValidateLayoutOrDump()
        {
            if (UnsafeUtility.SizeOf<FutureCommandEnvelope>() == FutureCommandSandboxConstants.EnvelopeSizeBytes &&
                UnsafeUtility.SizeOf<ModSandboxTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ModSpawnRequestSignal>() == 64 &&
                UnsafeUtility.SizeOf<SurvivalOverrideSignal>() == 32 &&
                UnsafeUtility.SizeOf<ModHapticPulseSignal>() == 64 &&
                UnsafeUtility.SizeOf<ModSubtitleCueSignal>() == 16 &&
                UnsafeUtility.SizeOf<KernelExecutionTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ModCommandKernelOpcodeRecord>() == 16 &&
                UnsafeUtility.SizeOf<ModKernelTuningProfile>() == 32 &&
                UnsafeUtility.SizeOf<ModKernelCameraJuiceImpulse>() == 32 &&
                UnsafeUtility.SizeOf<ModKernelCameraJuiceState>() == 64 &&
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
            SignalBus<SandboxMockAcousticSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 16, laneHash: 0x4D414353u);
            SignalBus<SandboxMockAcousticSignal>.EnsureInitialized();
            SignalBus<MockDamageSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 16, laneHash: 0x4D444D47u);
            SignalBus<MockDamageSignal>.EnsureInitialized();
            SignalBus<ModFutureDevNullSignal>.Configure(256, maxFrameSignals: 512, lowTierFrameSignals: 32, laneHash: 0x4D444E4Cu);
            SignalBus<ModFutureDevNullSignal>.EnsureInitialized();
            SignalBus<SurvivalOverrideSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 64, laneHash: 0x53564F52u);
            SignalBus<SurvivalOverrideSignal>.EnsureInitialized();
            SignalBus<ModHapticPulseSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 8, laneHash: 0x48505450u);
            SignalBus<ModHapticPulseSignal>.EnsureInitialized();
            SignalBus<ModSubtitleCueSignal>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 8, laneHash: 0x53554251u);
            SignalBus<ModSubtitleCueSignal>.EnsureInitialized();
            SignalBus<ModInteractionRejectedPayload>.Configure(128, maxFrameSignals: 256, lowTierFrameSignals: 32, laneHash: 0x4D52454Au);
            SignalBus<ModInteractionRejectedPayload>.EnsureInitialized();
        }

        private static VaultLane<T> AcquireVaultLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            return new VaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };
        }

        private static bool IsLaneCreated<T>(in VaultLane<T> lane) where T : struct
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private static bool TryOpenVaultLaneRead<T>(
            ref VaultLane<T> lane,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsLaneCreated(in lane) ||
                !vault.TryReadOnlyHandle(in lane.Handle, out buffer) ||
                buffer.Length < lane.Length)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireVaultLaneWrite<T>(
            ref VaultLane<T> lane,
            out NativeArray<T> buffer,
            out IDataVault lockedVault) where T : struct
        {
            buffer = default;
            lockedVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsLaneCreated(in lane) ||
                !vault.TryAcquireWriteLock(in lane.Handle, SystemID.ModSandbox, out buffer))
            {
                buffer = default;
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!buffer.IsCreated || buffer.Length < lane.Length)
                {
                    buffer = default;
                    return false;
                }

                lockedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in lane.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryReadRingStateSnapshot(out ModSandboxRingState state)
        {
            state = default;
            if (!TryOpenVaultLaneRead(ref _ringStateHandle, out NativeArray<ModSandboxRingState>.ReadOnly ringState) ||
                ringState.Length == 0)
            {
                return false;
            }

            state = ringState[0];
            return true;
        }

        private static bool TryWriteRingStateSnapshot(in ModSandboxRingState state)
        {
            return TryWriteVaultLaneElement(ref _ringStateHandle, 0, in state);
        }

        private static bool TryWriteVaultLaneElement<T>(
            ref VaultLane<T> lane,
            int index,
            in T value) where T : struct
        {
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref lane, out NativeArray<T> buffer, out lockedVault))
                return false;

            try
            {
                if ((uint)index >= (uint)buffer.Length)
                    return false;

                buffer[index] = value;
                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in lane.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryEnqueueSinglePendingEnvelope(
            in FutureCommandEnvelope envelope,
            ref ModSandboxRingState state)
        {
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _pendingRingHandle, out NativeArray<FutureCommandEnvelope> pendingRing, out lockedVault))
            {
                return false;
            }

            try
            {
                EnqueuePendingEnvelope(pendingRing, ref state, in envelope);
                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _pendingRingHandle.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.Generation == 0u)
            {
                return false;
            }

            return vault.TryReadOnlyHandle(in handle, out buffer) && buffer.Length > 0;
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
                state.PendingOverflowDropped = state.PendingOverflowDropped == uint.MaxValue
                    ? uint.MaxValue
                    : state.PendingOverflowDropped + 1u;
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

            if (!_initialized)
                return false;

            AcquireVaultBuffers();

            if (!TryOpenVaultLaneRead(ref _pendingRingHandle, out NativeArray<FutureCommandEnvelope>.ReadOnly pendingRing) ||
                !TryOpenVaultLaneRead(ref _opcodeRecordsHandle, out NativeArray<FutureCommandOpcodeRecord>.ReadOnly opcodeRecords) ||
                !TryOpenVaultLaneRead(ref _approvedAssetManifestHandle, out NativeArray<ApprovedAssetRecord>.ReadOnly approvedAssets) ||
                !TryOpenVaultLaneRead(ref _kernelTuningProfilesHandle, out NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles) ||
                !TryOpenVaultLaneRead(ref _tuningHandle, out NativeArray<FutureCommandSandboxTuning>.ReadOnly tuningBuffer) ||
                !TryReadRingStateSnapshot(out ModSandboxRingState state))
            {
                return false;
            }

            float quality = ResolveGlobalQualityWeight(tuningBuffer);
            FutureCommandSandboxTuning tuning = ResolveTuning(tuningBuffer);
            int maxPerSignature = ResolveScaledCommandBudget(tuning.MaxCommandsPerFrame, quality);
            maxPerSignature = ResolveKernelProfileFrameBudget(kernelProfiles, maxPerSignature);
            int stagingCapacity = IsLaneCreated(in _stagingHandle) ? _stagingHandle.Length : 0;
            if (stagingCapacity <= 0)
                return false;

            int globalBudget = math.min(
                stagingCapacity,
                maxPerSignature);

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            uint rollbackActive = IsRollbackFrozen() ? 1u : 0u;
            uint enqueueOverflowDropped = state.PendingOverflowDropped;
            state.PendingOverflowDropped = 0u;

            int smallestProfileBudget = ResolveSmallestKernelProfileFrameBudget(kernelProfiles, int.MaxValue);
            bool profileCapMayTrip = smallestProfileBudget != int.MaxValue && state.PendingCount > smallestProfileBudget;
            if (!TryDrainPendingToValidationStaging(
                    pendingRing,
                    kernelProfiles,
                    ref state,
                    globalBudget,
                    profileCapMayTrip,
                    out NativeArray<FutureCommandEnvelope>.ReadOnly staging,
                    out int drainCount,
                    out uint shedDropped))
                return false;

            uint thermalDropped = SaturatingAdd(enqueueOverflowDropped, shedDropped);
            if (!TryEnsureModderLeasesForInputs(staging, drainCount, tuning.MaxModMemoryMb, ref state, frame) ||
                !TryWriteRingStateSnapshot(in state))
                return false;

            if (drainCount == 0)
            {
                if (recordNoWorkTelemetry)
                {
                    RecordTelemetry(
                        frame,
                        0u,
                        0u,
                        0u,
                        thermalDropped,
                        0u,
                        0UL,
                        quality,
                        0u,
                        0u,
                        (uint)state.PendingCount,
                        0u,
                        (uint)maxPerSignature);
                    FutureCommandValidationStats kernelNoWorkStats = default;
                    kernelNoWorkStats.Dropped = thermalDropped;
                    kernelNoWorkStats.RejectionMask = thermalDropped > 0u
                        ? (uint)FutureCommandRejectReason.ThermalShed
                        : 0u;
                    RecordKernelTelemetry(
                        frame,
                        in kernelNoWorkStats,
                        thermalDropped,
                        0UL,
                        quality,
                        (uint)state.PendingCount);
                }

                return false;
            }

            if (!TryClearVaultLane(ref _statsHandle))
                return false;

            if (!TryPrepareValidationScratchOutputs())
                return false;

            if (!TryOpenVaultLaneRead(ref _memoryLeasesHandle, out NativeArray<ModderMemoryLease>.ReadOnly memoryLeases))
                return false;

            int modderBlackboxMemoryLength = IsLaneCreated(in _modderBlackboxMemoryHandle)
                ? _modderBlackboxMemoryHandle.Length
                : 0;
            if (modderBlackboxMemoryLength <= 0)
                return false;

            validationState = new ModSandboxScheduledValidationState
            {
                Frame = frame,
                PendingAfterDrain = (uint)state.PendingCount,
                MaxCommandsPerSignature = (uint)maxPerSignature,
                ThermalDropped = thermalDropped,
                Quality = quality,
                Flags = rollbackActive,
                StartTicks = Stopwatch.GetTimestamp()
            };

            job = new ValidateFutureCommandEnvelopeJob
            {
                Inputs = staging,
                ScratchState = _validationScratchState,
                OpcodeRecords = opcodeRecords,
                PerModCounters = _validationCounterScratch,
                MemoryLeases = memoryLeases,
                ApprovedAssetManifest = approvedAssets,
                MemoryWriteCommands = _validationMemoryWriteScratch,
                ModderBlackboxMemoryLength = modderBlackboxMemoryLength,
                DevNullScratch = _validationDevNullScratch,
                SpawnWriter = SignalBus<ModSpawnRequestSignal>.ParallelWriter,
                AssetWriter = SignalBus<ModAssetReferenceSignal>.ParallelWriter,
                AcousticWriter = SignalBus<SandboxMockAcousticSignal>.ParallelWriter,
                DamageWriter = SignalBus<MockDamageSignal>.ParallelWriter,
                DevNullSignalWriter = SignalBus<ModFutureDevNullSignal>.ParallelWriter,
                SurvivalWriter = SignalBus<SurvivalOverrideSignal>.ParallelWriter,
                HapticWriter = SignalBus<ModHapticPulseSignal>.ParallelWriter,
                SubtitleWriter = SignalBus<ModSubtitleCueSignal>.ParallelWriter,
                RejectionWriter = SignalBus<ModInteractionRejectedPayload>.ParallelWriter,
                CameraJuiceScratch = _validationCameraImpulseScratch,
                KernelProfiles = kernelProfiles,
                Count = drainCount,
                Frame = frame,
                OpcodeRecordCount = state.OpcodeCount,
                MaxCommandsPerSignature = maxPerSignature,
                GlobalQualityWeight = quality,
                MaxAssetBytes = tuning.MaxAssetBytes,
                TuningFlags = tuning.Flags,
                RollbackActive = rollbackActive,
                ObserverAUP = ResolveObserverAup()
            };
            return true;
        }

        private static bool TryPrepareValidationScratchOutputs()
        {
            if (!_validationScratchState.IsCreated ||
                !_validationCounterScratch.IsCreated ||
                !_validationMemoryWriteScratch.IsCreated ||
                !_validationDevNullScratch.IsCreated ||
                !_validationCameraImpulseScratch.IsCreated ||
                _validationScratchState.Length == 0)
            {
                return false;
            }

            if (!TryOpenVaultLaneRead(ref _perModCountersHandle, out NativeArray<ModderFrameCounter>.ReadOnly counters))
                return false;

            int count = math.min(_validationCounterScratch.Length, counters.Length);
            for (int i = 0; i < count; i++)
                _validationCounterScratch[i] = counters[i];
            for (int i = count; i < _validationCounterScratch.Length; i++)
                _validationCounterScratch[i] = default;

            _validationScratchState[0] = default;
            return true;
        }

        private static bool TryDrainPendingToValidationStaging(
            NativeArray<FutureCommandEnvelope>.ReadOnly pendingRing,
            NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles,
            ref ModSandboxRingState state,
            int globalBudget,
            bool profileCapMayTrip,
            out NativeArray<FutureCommandEnvelope>.ReadOnly stagingRead,
            out int drainCount,
            out uint shedDropped)
        {
            stagingRead = default;
            drainCount = 0;
            shedDropped = 0u;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _stagingHandle, out NativeArray<FutureCommandEnvelope> stagingWrite, out lockedVault))
                return false;

            try
            {
                int budget = math.clamp(globalBudget, 0, stagingWrite.Length);
                MemClearElements(stagingWrite, 0, budget);
                if (!pendingRing.IsCreated || pendingRing.Length == 0 || state.PendingCount <= 0 || budget <= 0)
                    return true;

                bool shouldShed = state.PendingCount > budget || profileCapMayTrip;
                if (shouldShed)
                {
                    DrainPendingWithLoadShedding(pendingRing, kernelProfiles, stagingWrite, ref state, budget, out drainCount, out shedDropped);
                }
                else
                {
                    int count = math.min(state.PendingCount, math.min(pendingRing.Length, budget));
                    for (int i = 0; i < count; i++)
                    {
                        int index = RingIndex(state.PendingHead, i, pendingRing.Length);
                        stagingWrite[i] = pendingRing[index];
                    }

                    state.PendingHead = AdvanceRingIndexBy(state.PendingHead, count, pendingRing.Length);
                    state.PendingCount = math.max(0, state.PendingCount - count);
                    drainCount = count;
                }
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _stagingHandle.Handle, SystemID.ModSandbox);
            }

            return TryOpenVaultLaneRead(ref _stagingHandle, out stagingRead);
        }

        private static void DrainPendingWithLoadShedding(
            NativeArray<FutureCommandEnvelope>.ReadOnly pendingRing,
            NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles,
            NativeArray<FutureCommandEnvelope> stagingWrite,
            ref ModSandboxRingState state,
            int budget,
            out int kept,
            out uint dropped)
        {
            kept = 0;
            dropped = 0u;
            int count = math.min(state.PendingCount, pendingRing.Length);
            int safeBudget = math.clamp(budget, 0, math.min(count, stagingWrite.Length));
            int overflow = math.max(0, count - safeBudget);
            int survivalCap = ResolveKernelOpcodeFrameBudget(kernelProfiles, FutureCommandOpcodes.SurvivalOverride);
            int hapticCap = ResolveKernelOpcodeFrameBudget(kernelProfiles, FutureCommandOpcodes.HapticPulse);
            int subtitleCap = ResolveKernelOpcodeFrameBudget(kernelProfiles, FutureCommandOpcodes.SubtitleCue);

            int optionalCount = 0;
            int standardCount = 0;
            int survivalCount = 0;
            for (int i = 0; i < count; i++)
            {
                int index = RingIndex(state.PendingHead, i, pendingRing.Length);
                int priority = ResolveKernelDropPriority(kernelProfiles, pendingRing[index].OpcodeHash);
                if (priority == 0)
                    optionalCount++;
                else if (priority == 1)
                    standardCount++;
                else
                    survivalCount++;
            }

            int dropOptional = math.min(overflow, optionalCount);
            int remaining = overflow - dropOptional;
            int dropStandard = math.min(remaining, standardCount);
            remaining -= dropStandard;
            int dropSurvival = math.min(remaining, survivalCount);
            int optionalDropped = 0;
            int standardDropped = 0;
            int survivalDropped = 0;
            int survivalKept = 0;
            int hapticKept = 0;
            int subtitleKept = 0;

            for (int i = 0; i < count; i++)
            {
                int index = RingIndex(state.PendingHead, i, pendingRing.Length);
                FutureCommandEnvelope envelope = pendingRing[index];
                int priority = ResolveKernelDropPriority(kernelProfiles, envelope.OpcodeHash);
                if (priority == 0 && optionalDropped < dropOptional)
                {
                    optionalDropped++;
                    continue;
                }

                if (priority == 1 && standardDropped < dropStandard)
                {
                    standardDropped++;
                    continue;
                }

                if (priority > 1 && survivalDropped < dropSurvival)
                {
                    survivalDropped++;
                    continue;
                }

                uint normalizedOpcode = NormalizeKernelProfileOpcode(envelope.OpcodeHash);
                if (normalizedOpcode == FutureCommandOpcodes.SurvivalOverride)
                {
                    if (survivalKept >= survivalCap)
                        continue;
                    survivalKept++;
                }
                else if (normalizedOpcode == FutureCommandOpcodes.HapticPulse)
                {
                    if (hapticKept >= hapticCap)
                        continue;
                    hapticKept++;
                }
                else if (normalizedOpcode == FutureCommandOpcodes.SubtitleCue)
                {
                    if (subtitleKept >= subtitleCap)
                        continue;
                    subtitleKept++;
                }

                if (kept >= safeBudget)
                    continue;

                stagingWrite[kept] = envelope;
                kept++;
            }

            dropped = (uint)math.max(0, count - kept);
            state.PendingHead = state.PendingTail;
            state.PendingCount = 0;
        }

        private static bool TryEnsureModderLeasesForInputs(
            NativeArray<FutureCommandEnvelope>.ReadOnly inputs,
            int inputCount,
            int maxMemoryMb,
            ref ModSandboxRingState state,
            uint frame)
        {
            if (inputCount <= 0)
                return true;

            if (!inputs.IsCreated ||
                !TryOpenVaultLaneRead(ref _modderBlackboxMemoryHandle, out NativeArray<byte>.ReadOnly modderBlackboxMemory))
                return false;

            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref _memoryLeasesHandle, out NativeArray<ModderMemoryLease> memoryLeases, out lockedVault))
                return false;

            try
            {
                int count = math.min(inputCount, inputs.Length);
                for (int i = 0; i < count; i++)
                    EnsureModderLease(inputs[i].ModderSignature, maxMemoryMb, modderBlackboxMemory.Length, memoryLeases, ref state, frame);
                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _memoryLeasesHandle.Handle, SystemID.ModSandbox);
            }
        }

        private static bool TryClearVaultLane<T>(ref VaultLane<T> lane) where T : struct
        {
            IDataVault lockedVault = null;
            if (!TryAcquireVaultLaneWrite(ref lane, out NativeArray<T> buffer, out lockedVault))
                return false;

            try
            {
                MemClearArray(buffer);
                return true;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in lane.Handle, SystemID.ModSandbox);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RingIndex(int head, int offset, int capacity)
        {
            int index = head + offset;
            while (index >= capacity)
                index -= capacity;
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AdvanceRingIndexBy(int index, int count, int capacity)
        {
            for (int i = 0; i < count; i++)
                index = AdvanceRingIndex(index, capacity);
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NormalizeKernelProfileOpcode(uint opcodeHash)
        {
            return opcodeHash == FutureCommandOpcodes.TriggerSubtitleCue
                ? FutureCommandOpcodes.SubtitleCue
                : opcodeHash;
        }

        private static int ResolveKernelDropPriority(NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles, uint opcodeHash)
        {
            float profileWeight = ResolveKernelPriorityWeight(kernelProfiles, opcodeHash);
            if (profileWeight >= 0f)
            {
                if (profileWeight <= FutureCommandSandboxConstants.KernelOptionalPriorityMax)
                    return 0;
                if (profileWeight >= FutureCommandSandboxConstants.KernelSurvivalPriorityMin)
                    return 2;
                return 1;
            }

            if (opcodeHash == FutureCommandOpcodes.HapticPulse ||
                opcodeHash == FutureCommandOpcodes.SubtitleCue ||
                opcodeHash == FutureCommandOpcodes.TriggerSubtitleCue)
            {
                return 0;
            }

            return opcodeHash == FutureCommandOpcodes.SurvivalOverride ? 2 : 1;
        }

        private static int ResolveKernelOpcodeFrameBudget(NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles, uint opcodeHash)
        {
            uint normalizedOpcode = NormalizeKernelProfileOpcode(opcodeHash);
            if (!kernelProfiles.IsCreated || normalizedOpcode == 0u)
                return int.MaxValue;

            for (int i = 0; i < kernelProfiles.Length; i++)
            {
                ModKernelTuningProfile profile = kernelProfiles[i];
                if (profile.OpcodeHash == normalizedOpcode)
                    return profile.MaxPerFrame > 0
                        ? math.min(profile.MaxPerFrame, FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame)
                        : int.MaxValue;
                if (profile.OpcodeHash == 0u)
                    return int.MaxValue;
            }

            return int.MaxValue;
        }

        private static float ResolveKernelPriorityWeight(NativeArray<ModKernelTuningProfile>.ReadOnly kernelProfiles, uint opcodeHash)
        {
            uint normalizedOpcode = NormalizeKernelProfileOpcode(opcodeHash);
            if (!kernelProfiles.IsCreated || normalizedOpcode == 0u)
                return -1f;

            for (int i = 0; i < kernelProfiles.Length; i++)
            {
                ModKernelTuningProfile profile = kernelProfiles[i];
                if (profile.OpcodeHash == normalizedOpcode)
                    return profile.PriorityWeight;
                if (profile.OpcodeHash == 0u)
                    return -1f;
            }

            return -1f;
        }

        private static void FinalizeValidationTelemetry(in ModSandboxScheduledValidationState validationState)
        {
            if (!TryOpenVaultLaneRead(ref _statsHandle, out NativeArray<FutureCommandValidationStats>.ReadOnly statsBuffer) ||
                statsBuffer.Length == 0 ||
                !TryReadRingStateSnapshot(out ModSandboxRingState state))
                return;

            FutureCommandValidationStats stats = statsBuffer[0];
            long elapsedTicks = Stopwatch.GetTimestamp() - validationState.StartTicks;
            if (elapsedTicks < 0L)
                elapsedTicks = 0L;
            ulong elapsedNs = (ulong)((double)elapsedTicks * 1000000000.0d / Stopwatch.Frequency);
            ulong elapsedKernelTicks = (ulong)elapsedTicks;
            FutureCommandValidationStats telemetryStats = stats;
            if (validationState.ThermalDropped != 0u)
                telemetryStats.RejectionMask |= (uint)FutureCommandRejectReason.ThermalShed;
            uint totalDropped = SaturatingAdd(stats.Dropped, validationState.ThermalDropped);
            RecordTelemetry(
                validationState.Frame,
                stats.Incoming,
                stats.Valid,
                stats.Rejected,
                totalDropped,
                stats.DevNull,
                elapsedNs,
                validationState.Quality,
                telemetryStats.RejectionMask,
                stats.FaultHash,
                (uint)state.PendingCount,
                stats.PeakCommandsForSignature,
                validationState.MaxCommandsPerSignature);
            RecordKernelTelemetry(
                validationState.Frame,
                in telemetryStats,
                validationState.ThermalDropped,
                elapsedKernelTicks,
                validationState.Quality,
                (uint)state.PendingCount);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SaturatingAdd(uint left, uint right)
        {
            return left > uint.MaxValue - right ? uint.MaxValue : left + right;
        }

        private static void MemClearElements<T>(NativeArray<T> array, int start, int count) where T : struct
        {
            if (!array.IsCreated || array.Length == 0 || count <= 0)
                return;

            int safeStart = math.clamp(start, 0, array.Length);
            int safeCount = math.min(count, array.Length - safeStart);
            if (safeCount <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            int stride = UnsafeUtility.SizeOf<T>();
            UnsafeUtility.MemClear(ptr + safeStart * stride, (long)safeCount * stride);
        }

        private static double3 ResolveObserverAup()
        {
            double3 observer = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(observer)) ? observer : double3.zero;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ValidateFutureCommandEnvelopeJob : IJob
        {
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandEnvelope>.ReadOnly Inputs;
            [NoAlias] public NativeArray<ModSandboxValidationScratchState> ScratchState;
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandOpcodeRecord>.ReadOnly OpcodeRecords;
            [NoAlias] public NativeArray<ModderFrameCounter> PerModCounters;
            [NoAlias] [ReadOnly] public NativeArray<ModderMemoryLease>.ReadOnly MemoryLeases;
            [NoAlias] [ReadOnly] public NativeArray<ApprovedAssetRecord>.ReadOnly ApprovedAssetManifest;
            [NoAlias] public NativeArray<ModSandboxMemoryWriteCommand> MemoryWriteCommands;
            [NoAlias] public NativeArray<FutureCommandEnvelope> DevNullScratch;
            [NoAlias] public NativeArray<ModKernelCameraJuiceImpulse> CameraJuiceScratch;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModSpawnRequestSignal>.ParallelWriter SpawnWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModAssetReferenceSignal>.ParallelWriter AssetWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<SandboxMockAcousticSignal>.ParallelWriter AcousticWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<MockDamageSignal>.ParallelWriter DamageWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModFutureDevNullSignal>.ParallelWriter DevNullSignalWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<SurvivalOverrideSignal>.ParallelWriter SurvivalWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModHapticPulseSignal>.ParallelWriter HapticWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModSubtitleCueSignal>.ParallelWriter SubtitleWriter;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModInteractionRejectedPayload>.ParallelWriter RejectionWriter;
            [NoAlias] [ReadOnly] public NativeArray<ModKernelTuningProfile>.ReadOnly KernelProfiles;
            public int Count;
            public int OpcodeRecordCount;
            public int MaxCommandsPerSignature;
            public uint Frame;
            public float GlobalQualityWeight;
            public uint MaxAssetBytes;
            public uint TuningFlags;
            public uint RollbackActive;
            public int ModderBlackboxMemoryLength;
            public double3 ObserverAUP;

            public void Execute()
            {
                if (!ScratchState.IsCreated || ScratchState.Length == 0)
                    return;

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

                ModSandboxValidationScratchState output = ScratchState[0];
                output.Stats = stats;
                ScratchState[0] = output;
            }

            private bool TryValidateEnvelope(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (envelope.ModderSignature == 0u ||
                    envelope.OpcodeHash == 0u ||
                    !IsAllowedOpcode(envelope.OpcodeHash))
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.UnknownOpcode, 0u);
                    return false;
                }

                if (!IsFiniteBoundedAup(envelope.TargetAUP))
                {
                    stats.AupViolations++;
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidAup, FutureCommandSandboxConstants.FaultHashInvalidAup);
                    stats.RejectionMask |= (uint)FutureCommandRejectReason.AupViolation;
                    stats.KernelRejected++;
                    return false;
                }

                if (!VerifyIntegrity(in envelope))
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.IntegrityMismatch, 0u);
                    return false;
                }

                if (!TryValidatePayload(in envelope, ref stats))
                    return false;

                if (!TryAccount(in envelope, ref stats))
                    return false;

                return true;
            }

            private void RouteEnvelope(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                switch (envelope.OpcodeHash)
                {
                    case FutureCommandOpcodes.SurvivalOverride:
                        if (TryRouteSurvivalOverride(in envelope, ref stats))
                        {
                            stats.Valid++;
                            stats.SurvivalProcessed++;
                        }
                        break;

                    case FutureCommandOpcodes.HapticPulse:
                        if (TryRouteHapticPulse(in envelope, ref stats))
                        {
                            stats.Valid++;
                            stats.HapticProcessed++;
                        }
                        break;

                    case FutureCommandOpcodes.SubtitleCue:
                    case FutureCommandOpcodes.TriggerSubtitleCue:
                        if (TryRouteSubtitleCue(in envelope, ref stats))
                        {
                            stats.Valid++;
                            stats.SubtitleProcessed++;
                        }
                        break;

                    case FutureCommandOpcodes.SpawnItem:
                        SpawnWriter.TryEnqueue(new ModSpawnRequestSignal
                        {
                            Frame = Frame,
                            ModderSignature = envelope.ModderSignature,
                            OpcodeHash = envelope.OpcodeHash,
                            AssetHash = math.asuint(envelope.PayloadData.x),
                            TargetAUP = envelope.TargetAUP,
                            PayloadData = SanitizeFinitePayload(envelope.PayloadData),
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
                        AcousticWriter.TryEnqueue(new SandboxMockAcousticSignal
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
                        DamageWriter.TryEnqueue(new MockDamageSignal
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
                        EnqueueDevNull(in envelope, ref stats);
                        stats.Valid++;
                        break;

                    default:
                        RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.UnknownOpcode, 0u);
                        break;
                }
            }

            private bool TryRouteSurvivalOverride(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                float oxygenFloor = math.saturate(envelope.PayloadData.x);
                uint ttl = (uint)math.clamp((int)math.round(math.max(0f, envelope.PayloadData.y)), 1, 3600);
                uint flags = math.asuint(envelope.PayloadData.z);
                SurvivalOverrideSignal signal = new SurvivalOverrideSignal
                {
                    ModHash = envelope.ModderSignature,
                    RequestId = (uint)envelope.IntegrityHash,
                    OxygenFloor = oxygenFloor,
                    TTL = ttl,
                    Flags = flags,
                    _pad0 = 0u,
                    _pad1 = 0UL
                };
                SurvivalWriter.TryEnqueue(in signal);
                return true;
            }

            private bool TryRouteHapticPulse(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (!IsKernelAupFiniteBounded(envelope.TargetAUP))
                {
                    stats.KernelRejected++;
                    stats.AupViolations++;
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AupViolation, FutureCommandSandboxConstants.FaultHashInvalidAup);
                    return false;
                }

                double3 localDeltaD = envelope.TargetAUP - ObserverAUP;
                if (!math.all(math.isfinite(localDeltaD)))
                {
                    stats.KernelRejected++;
                    stats.AupViolations++;
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AupViolation, FutureCommandSandboxConstants.FaultHashInvalidAup);
                    return false;
                }

                float3 localDelta = (float3)localDeltaD;
                if (!math.all(math.isfinite(localDelta)))
                {
                    stats.KernelRejected++;
                    stats.AupViolations++;
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AupViolation, FutureCommandSandboxConstants.FaultHashInvalidAup);
                    return false;
                }

                if (RollbackActive != 0u)
                {
                    stats.KernelSuppressed++;
                    stats.RejectionMask |= (uint)FutureCommandRejectReason.RollbackSuppressed;
                    return true;
                }

                uint waveformHash = math.asuint(envelope.PayloadData.x);
                bool hasProfile = TryResolveKernelProfile(FutureCommandOpcodes.HapticPulse, out ModKernelTuningProfile profile);
                float intensityScale = hasProfile ? math.max(0f, profile.IntensityScale) : 1f;
                float intensity = math.saturate(envelope.PayloadData.y * intensityScale);
                float durationMax = hasProfile ? math.max(0.01f, profile.MaxDurationSeconds) : 5f;
                float duration = math.clamp(envelope.PayloadData.z, 0.01f, durationMax);
                float rangeMax = hasProfile ? math.max(1f, profile.RangeMeters) : 32f;
                float requestedRange = math.isfinite(envelope.PayloadData.w) && envelope.PayloadData.w > 0f ? envelope.PayloadData.w : rangeMax;
                float range = math.clamp(requestedRange, 1f, rangeMax);
                float distanceSq = math.max(1f, math.lengthsq(localDelta));
                float rangeSq = math.max(1f, range * range);
                if (distanceSq > rangeSq)
                {
                    stats.KernelSuppressed++;
                    return true;
                }

                float inverseSquare = math.rcp(math.max(1f, distanceSq));
                float rangeEnergy = rangeSq * inverseSquare;
                float scaledIntensity = math.saturate(intensity * rangeEnergy);
                float fallback01 = math.saturate((0.35f - math.saturate(GlobalQualityWeight)) * 2.8571429f);
                uint hapticFlags = TuningFlags | (hasProfile ? profile.Flags : 0u);
                float fallbackScalar = (hapticFlags & FutureCommandSandboxConstants.KernelFlagForceHapticCameraFallback) != 0u
                    ? 1f
                    : fallback01;
                if (fallbackScalar > 0.0001f)
                {
                    WriteCameraJuiceImpulse(envelope.TargetAUP, scaledIntensity * fallbackScalar, ref stats);
                    return true;
                }

                HapticWriter.TryEnqueue(new ModHapticPulseSignal
                {
                    TargetAUP = envelope.TargetAUP,
                    WaveformHash = waveformHash == 0u ? (envelope.ModderSignature ^ FutureCommandOpcodes.HapticPulse) : waveformHash,
                    Intensity = scaledIntensity,
                    Duration = duration,
                    Flags = 0u,
                    _pad0 = 0UL
                });
                return true;
            }

            private bool TryRouteSubtitleCue(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (RollbackActive != 0u)
                {
                    stats.KernelSuppressed++;
                    stats.RejectionMask |= (uint)FutureCommandRejectReason.RollbackSuppressed;
                    return true;
                }

                uint tokenHash = math.asuint(envelope.PayloadData.x);
                if (tokenHash == 0u)
                {
                    stats.KernelRejected++;
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.KernelPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                    return false;
                }

                bool hasProfile = TryResolveKernelProfile(
                    envelope.OpcodeHash == FutureCommandOpcodes.TriggerSubtitleCue
                        ? FutureCommandOpcodes.SubtitleCue
                        : envelope.OpcodeHash,
                    out ModKernelTuningProfile profile);
                float durationMax = hasProfile ? math.max(0.05f, profile.MaxDurationSeconds) : 30f;
                SubtitleWriter.TryEnqueue(new ModSubtitleCueSignal
                {
                    TokenHash = tokenHash,
                    Duration = math.clamp(envelope.PayloadData.y, 0.05f, durationMax),
                    Priority = (uint)math.clamp((int)math.round(math.max(0f, envelope.PayloadData.z)), 0, 255),
                    _pad0 = 0u
                });
                return true;
            }

            private void WriteCameraJuiceImpulse(double3 targetAup, float scalar, ref FutureCommandValidationStats stats)
            {
                if (!CameraJuiceScratch.IsCreated || CameraJuiceScratch.Length == 0)
                    return;

                ModSandboxValidationScratchState output = ScratchState[0];
                int index = output.CameraImpulseCount;
                if ((uint)index >= (uint)CameraJuiceScratch.Length)
                    return;

                CameraJuiceScratch[index] = new ModKernelCameraJuiceImpulse
                {
                    TargetAUP = targetAup,
                    Scalar = math.saturate(scalar),
                    Frame = Frame
                };
                output.CameraImpulseCount = index + 1;
                ScratchState[0] = output;
                stats.KernelSuppressed++;
                stats.HapticFallbacks++;
            }

            private bool TryRouteAssetReference(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                uint assetHash = math.asuint(envelope.PayloadData.x);
                uint declaredCrc = math.asuint(envelope.PayloadData.y);
                uint declaredBytes = math.asuint(envelope.PayloadData.z);
                if (declaredBytes == 0u || declaredBytes > MaxAssetBytes)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AssetTooLarge, 0u);
                    return false;
                }

                if (assetHash == 0u ||
                    declaredCrc == 0u ||
                    !TryGetApprovedAsset(assetHash, out ApprovedAssetRecord approvedAsset) ||
                    approvedAsset.Crc32 != declaredCrc)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AssetCrcMismatch, 0u);
                    return false;
                }

                if (approvedAsset.ByteLength != 0u && declaredBytes > approvedAsset.ByteLength)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.AssetTooLarge, 0u);
                    return false;
                }

                AssetWriter.TryEnqueue(new ModAssetReferenceSignal
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
                if (!MemoryWriteCommands.IsCreated || ModderBlackboxMemoryLength <= 0)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                if (!TryGetMemoryLease(envelope.ModderSignature, out ModderMemoryLease lease))
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                uint relativeOffset = math.asuint(envelope.PayloadData.x);
                uint rawValue = math.asuint(envelope.PayloadData.y);
                uint byteCount = math.clamp(math.asuint(envelope.PayloadData.z), 1u, 4u);
                if (relativeOffset > (uint)lease.ByteLength ||
                    byteCount > (uint)lease.ByteLength - relativeOffset ||
                    lease.OffsetBytes < 0 ||
                    lease.OffsetBytes > ModderBlackboxMemoryLength - lease.ByteLength)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MemoryViolation, FutureCommandSandboxConstants.FaultHashMemoryViolation);
                    return false;
                }

                ModSandboxValidationScratchState output = ScratchState[0];
                int commandIndex = output.MemoryWriteCount;
                if ((uint)commandIndex >= (uint)MemoryWriteCommands.Length)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.CommandFlood, 0u);
                    return false;
                }

                int absolute = lease.OffsetBytes + (int)relativeOffset;
                MemoryWriteCommands[commandIndex] = new ModSandboxMemoryWriteCommand
                {
                    OffsetBytes = absolute,
                    RawValue = rawValue,
                    ByteCount = byteCount,
                    Reserved = 0u
                };
                output.MemoryWriteCount = commandIndex + 1;
                ScratchState[0] = output;

                return true;
            }

            private bool TryValidateModMemoryRange(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (ModderBlackboxMemoryLength <= 0)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                if (!TryGetMemoryLease(envelope.ModderSignature, out ModderMemoryLease lease))
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MissingMemoryLease, 0u);
                    return false;
                }

                uint relativeOffset = math.asuint(envelope.PayloadData.x);
                uint byteCount = math.max(1u, math.asuint(envelope.PayloadData.z));
                if (relativeOffset > (uint)lease.ByteLength ||
                    byteCount > (uint)lease.ByteLength - relativeOffset ||
                    lease.OffsetBytes < 0 ||
                    lease.OffsetBytes > ModderBlackboxMemoryLength - lease.ByteLength)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.MemoryViolation, FutureCommandSandboxConstants.FaultHashMemoryViolation);
                    return false;
                }

                return true;
            }

            private bool TryAccount(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                int slot = FindCounterSlot(envelope.ModderSignature, out bool exists);
                if (slot < 0)
                {
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.CommandFlood, 0u);
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
                    RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.CommandFlood, 0u);
                    return false;
                }

                counter.Count++;
                stats.PeakCommandsForSignature = math.max(stats.PeakCommandsForSignature, (uint)counter.Count);
                PerModCounters[slot] = counter;
                return true;
            }

            private void EnqueueDevNull(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                if (DevNullScratch.IsCreated && DevNullScratch.Length > 0)
                {
                    ModSandboxValidationScratchState output = ScratchState[0];
                    int index = output.DevNullCount;
                    if ((uint)index < (uint)DevNullScratch.Length)
                    {
                        DevNullScratch[index] = envelope;
                        output.DevNullCount = index + 1;
                        ScratchState[0] = output;
                    }
                }
                DevNullSignalWriter.TryEnqueue(new ModFutureDevNullSignal
                {
                    Frame = Frame,
                    ModderSignature = envelope.ModderSignature,
                    OpcodeHash = envelope.OpcodeHash,
                    ReasonHash = FutureCommandSandboxConstants.DevNullReasonFutureSeam,
                    TargetAUP = envelope.TargetAUP,
                    PayloadData = SanitizeFinitePayload(envelope.PayloadData),
                    Flags = 0u,
                    Reserved = 0u
                });
                stats.DevNull++;
            }

            private bool TryValidatePayload(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats)
            {
                switch (envelope.OpcodeHash)
                {
                    case FutureCommandOpcodes.SurvivalOverride:
                        if (!math.isfinite(envelope.PayloadData.x) ||
                            !math.isfinite(envelope.PayloadData.y))
                        {
                            RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                            return false;
                        }
                        return true;

                    case FutureCommandOpcodes.HapticPulse:
                        if (!math.all(math.isfinite(new float3(envelope.PayloadData.y, envelope.PayloadData.z, envelope.PayloadData.w))) ||
                            envelope.PayloadData.y < 0f ||
                            envelope.PayloadData.z <= 0f)
                        {
                            RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                            return false;
                        }
                        return true;

                    case FutureCommandOpcodes.SubtitleCue:
                    case FutureCommandOpcodes.TriggerSubtitleCue:
                        if (!math.all(math.isfinite(new float2(envelope.PayloadData.y, envelope.PayloadData.z))) ||
                            math.asuint(envelope.PayloadData.x) == 0u ||
                            envelope.PayloadData.y <= 0f)
                        {
                            RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                            return false;
                        }
                        return true;

                    case FutureCommandOpcodes.SpawnItem:
                        if (!math.all(math.isfinite(new float3(envelope.PayloadData.y, envelope.PayloadData.z, envelope.PayloadData.w))))
                        {
                            RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                            return false;
                        }
                        return true;

                    case FutureCommandOpcodes.FaunaAcousticStimulus:
                    case FutureCommandOpcodes.FaunaDamageStimulus:
                        if (!math.all(math.isfinite(envelope.PayloadData)))
                        {
                            RejectEnvelope(in envelope, ref stats, FutureCommandRejectReason.InvalidPayload, FutureCommandSandboxConstants.FaultHashInvalidPayload);
                            return false;
                        }
                        return true;

                    default:
                        return true;
                }
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

            private bool TryResolveKernelProfile(uint opcodeHash, out ModKernelTuningProfile profile)
            {
                profile = default;
                if (!KernelProfiles.IsCreated || opcodeHash == 0u || KernelProfiles.Length == 0)
                    return false;

                for (int i = 0; i < KernelProfiles.Length; i++)
                {
                    ModKernelTuningProfile candidate = KernelProfiles[i];
                    if (candidate.OpcodeHash == opcodeHash)
                    {
                        profile = candidate;
                        return true;
                    }

                    if (candidate.OpcodeHash == 0u)
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
                ulong full = MemorySentinelMath.ComputeDeterministicHash64(&copy, 48);
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
            private static bool IsKernelAupFiniteBounded(double3 aup)
            {
                double3 abs = math.abs(aup);
                return math.all(math.isfinite(aup)) &&
                       math.all(abs <= new double3(FutureCommandSandboxConstants.KernelMaxAupMagnitudeMeters));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float4 SanitizeFinitePayload(float4 payload)
            {
                return new float4(
                    math.isfinite(payload.x) ? payload.x : 0f,
                    math.isfinite(payload.y) ? payload.y : 0f,
                    math.isfinite(payload.z) ? payload.z : 0f,
                    math.isfinite(payload.w) ? payload.w : 0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Reject(ref FutureCommandValidationStats stats, FutureCommandRejectReason reason, uint faultHash)
            {
                stats.Rejected++;
                stats.RejectionMask |= (uint)reason;
                if (faultHash != 0u)
                    stats.FaultHash = faultHash;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectEnvelope(in FutureCommandEnvelope envelope, ref FutureCommandValidationStats stats, FutureCommandRejectReason reason, uint faultHash)
            {
                Reject(ref stats, reason, faultHash);
                RejectionWriter.TryEnqueue(new ModInteractionRejectedPayload
                {
                    ModHash = envelope.ModderSignature,
                    RequestId = (uint)envelope.IntegrityHash,
                    OpcodeHash = envelope.OpcodeHash,
                    Reason = (uint)reason
                });
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct SurvivalOverrideKernelJob : IJob
        {
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandEnvelope> Inputs;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<SurvivalOverrideSignal>.ParallelWriter Output;
            public int Count;

            public void Execute()
            {
                int count = math.min(Count, Inputs.Length);
                for (int i = 0; i < count; i++)
                {
                    FutureCommandEnvelope envelope = Inputs[i];
                    if (envelope.OpcodeHash != FutureCommandOpcodes.SurvivalOverride ||
                        !math.isfinite(envelope.PayloadData.x) ||
                        !math.isfinite(envelope.PayloadData.y))
                    {
                        continue;
                    }

                    Output.TryEnqueue(new SurvivalOverrideSignal
                    {
                        ModHash = envelope.ModderSignature,
                        RequestId = (uint)envelope.IntegrityHash,
                        OxygenFloor = math.saturate(envelope.PayloadData.x),
                        TTL = (uint)math.clamp((int)math.round(math.max(0f, envelope.PayloadData.y)), 1, 3600),
                        Flags = math.asuint(envelope.PayloadData.z),
                        _pad0 = 0u,
                        _pad1 = 0UL
                    });
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct SubtitleCueKernelJob : IJob
        {
            [NoAlias] [ReadOnly] public NativeArray<FutureCommandEnvelope> Inputs;
            [NoAlias] [ReadOnly] public NativeArray<ModKernelTuningProfile> KernelProfiles;
            [NoAlias] [WriteOnly] public global::Hecton8.Core.MpscSignalRingBuffer<ModSubtitleCueSignal>.ParallelWriter Output;
            public int Count;
            public uint RollbackActive;

            public void Execute()
            {
                if (RollbackActive != 0u)
                    return;

                int count = math.min(Count, Inputs.Length);
                for (int i = 0; i < count; i++)
                {
                    FutureCommandEnvelope envelope = Inputs[i];
                    bool subtitleOpcode = envelope.OpcodeHash == FutureCommandOpcodes.SubtitleCue ||
                                          envelope.OpcodeHash == FutureCommandOpcodes.TriggerSubtitleCue;
                    if (!subtitleOpcode ||
                        math.asuint(envelope.PayloadData.x) == 0u ||
                        !math.all(math.isfinite(new float2(envelope.PayloadData.y, envelope.PayloadData.z))) ||
                        envelope.PayloadData.y <= 0f)
                    {
                        continue;
                    }

                    bool hasProfile = TryResolveKernelProfile(
                        envelope.OpcodeHash == FutureCommandOpcodes.TriggerSubtitleCue
                            ? FutureCommandOpcodes.SubtitleCue
                            : envelope.OpcodeHash,
                        out ModKernelTuningProfile profile);
                    Output.TryEnqueue(new ModSubtitleCueSignal
                    {
                        TokenHash = math.asuint(envelope.PayloadData.x),
                        Duration = math.clamp(envelope.PayloadData.y, 0.05f, hasProfile ? math.max(0.05f, profile.MaxDurationSeconds) : 30f),
                        Priority = (uint)math.clamp((int)math.round(math.max(0f, envelope.PayloadData.z)), 0, 255),
                        _pad0 = 0u
                    });
                }
            }

            private bool TryResolveKernelProfile(uint opcodeHash, out ModKernelTuningProfile profile)
            {
                profile = default;
                if (!KernelProfiles.IsCreated || opcodeHash == 0u || KernelProfiles.Length == 0)
                    return false;

                for (int i = 0; i < KernelProfiles.Length; i++)
                {
                    ModKernelTuningProfile candidate = KernelProfiles[i];
                    if (candidate.OpcodeHash == opcodeHash)
                    {
                        profile = candidate;
                        return true;
                    }

                    if (candidate.OpcodeHash == 0u)
                        return false;
                }

                return false;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MockMaliciousEnvelopeInjectionJob : IJob
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
