using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Sixteen-byte dump header prefix. Do not add fields here; use the sealed 1024-byte dump header extension.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct TelemetryHeaderDTO
    {
        public ulong Timestamp;
        public uint FrameNumber;
        public uint FatalHash;
    }

    /// <summary>
    /// Sixteen-byte event marker used as the allocation-free callstack surrogate.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct TelemetryEventDTO
    {
        public uint EventHash;
        public float ScalarValue;
        public uint EntityId;
        public uint _pad0;
    }

    /// <summary>
    /// Blind physics probe payload. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockPhysicsState
    {
        public float3 Position;
        public uint EntityId;
        public float3 Velocity;
        public uint Flags;
        public quaternion Rotation;
        public float AngularSpeed;
        public float Mass;
        public float Drag;
        public float Buoyancy;
    }

    /// <summary>
    /// Blind origin-shift signal used by SHINOBU_33 without depending on the real origin-shift owner.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockOriginShiftSignal
    {
        public long SectorX;
        public long SectorY;
        public long SectorZ;
        public float3 DeltaLocalMeters;
        public uint FrameNumber;
        public uint ShiftId;
        public uint Flags;
        public uint SourceHash;
        public float3 ImpactPosition;
    }

    /// <summary>
    /// Raw view over the SHINOBU blackbox. Fields are intentionally public to avoid CS1612 copies.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public unsafe struct BlackboxRingBufferDTO
    {
        public byte* Bytes;
        public int FrameCapacity;
        public int ActiveFrameCount;
        public int FrameStrideBytes;
        public int ValidFrameCount;
        public int WriteIndex;
        public int TotalWrites;
        public uint FatalHash;
        public uint _pad0;
        public uint _pad1;
        public uint _pad2;

        public ref byte GetFrameByteRef(int frameIndex)
        {
            int safeIndex = FrameCapacity <= 0 ? 0 : frameIndex % FrameCapacity;
            if (safeIndex < 0)
                safeIndex += FrameCapacity;

            return ref UnsafeUtility.AsRef<byte>(Bytes + (safeIndex * FrameStrideBytes));
        }

        public ref T GetFrameAsRef<T>(int frameIndex, int byteOffset) where T : unmanaged
        {
            int safeIndex = FrameCapacity <= 0 ? 0 : frameIndex % FrameCapacity;
            if (safeIndex < 0)
                safeIndex += FrameCapacity;

            return ref UnsafeUtility.AsRef<T>(Bytes + (safeIndex * FrameStrideBytes) + byteOffset);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct TelemetryLoggingMaskDTO
    {
        public uint SystemHash;
        public uint Mask;
        public uint Version;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal unsafe struct BlackboxSourceSlot
    {
        public byte* SourcePtr;
        public uint SourceHash;
        public uint Flags;
        public int PayloadBytes;
        public int _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    [BurstCompile]
    public unsafe struct NanSweeperJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public byte* Payload;
        public int PayloadBytes;
        public uint FatalHash;
        [NativeDisableUnsafePtrRestriction]
        public int* IsCatastrophicFailure;
        [NativeDisableUnsafePtrRestriction]
        public int* FatalHashOutput;

        public void Execute()
        {
            if (Payload == null || PayloadBytes <= 0 || IsCatastrophicFailure == null)
                return;

            int floatCount = PayloadBytes >> 2;
            float* values = (float*)Payload;
            for (int i = 0; i < floatCount; i++)
            {
                if (math.isfinite(values[i]))
                    continue;

                Interlocked.Exchange(ref UnsafeUtility.AsRef<int>(IsCatastrophicFailure), 1);
                if (FatalHashOutput != null)
                    Interlocked.Exchange(ref UnsafeUtility.AsRef<int>(FatalHashOutput), unchecked((int)FatalHash));
                return;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    [BurstCompile]
    public unsafe struct MockOriginShiftFireJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public MockOriginShiftSignal* Output;
        public int OutputLength;
        public uint Seed;
        public uint FrameNumber;
        public uint _pad0;
        public uint _pad1;

        public void Execute()
        {
            if (Output == null || OutputLength <= 0)
                return;

            uint state = Seed == 0u ? 2166136261u : Seed;
            state = (state * 1664525u) + 1013904223u;
            float x = ((int)(state & 1023u) - 512) * 0.25f;
            state = (state * 1664525u) + 1013904223u;
            float z = ((int)(state & 1023u) - 512) * 0.25f;

            MockOriginShiftSignal signal = default;
            signal.SectorX = unchecked((long)(state & 7u));
            signal.SectorY = 0L;
            signal.SectorZ = unchecked((long)((state >> 3) & 7u));
            signal.DeltaLocalMeters = new float3(x, 0f, z);
            signal.FrameNumber = FrameNumber;
            signal.ShiftId = state;
            signal.SourceHash = 0x4D4F5348u; // MOSH
            signal.ImpactPosition = signal.DeltaLocalMeters;
            Output[0] = signal;
        }
    }

    public static partial class GlobalTelemetryBus
    {
        public const int ShinobuBlackboxHighFrameCount = 300;
        public const int ShinobuBlackboxLowFrameCount = 60;
        public const int ShinobuBlackboxFrameStrideBytes = BlackboxFrameStrideBytes;
        public const int ShinobuBlackboxMainThreadWatchdogLane = 0;
        public const uint ShinobuBlackboxSourceFlagFloatScan = BlackboxSourceFlagFloatScan;
        internal const uint BlackboxEmergencyFlushHash = 0x454D464Cu; // EMFL

        private const int BlackboxHeaderPrefixBytes = 16;
        private const int BlackboxDumpHeaderBytes = 1024;
        private const int BlackboxCacheLineBytes = 64;
        private const int BlackboxHashHistoryCount = 100;
        private const int BlackboxHashHistoryBytes = BlackboxHashHistoryCount * 4;
        private const int BlackboxMaxSourceCount = 50;
        private const int BlackboxSourceStrideBytes = 64;
        private const int BlackboxEventCapacity = 4096;
        private const int BlackboxEventMask = BlackboxEventCapacity - 1;
        private const int BlackboxLoggingMaskCapacity = 64;
        private const int BlackboxMmfFlushFrameCount = 150;
        private const int BlackboxWatchdogLaneCount = 64;
        private const int BlackboxWatchdogProbeMilliseconds = 500;
        private const int BlackboxWatchdogStaleProbeLimit = 4;
        private const int BlackboxHashHistoryOffsetBytes = BlackboxCacheLineBytes;
        private const int BlackboxHeaderPadBytes = BlackboxHashHistoryOffsetBytes - BlackboxHeaderPrefixBytes;
        private const int BlackboxSourcePayloadOffsetBytes = 512;
        private const int BlackboxHashHistoryPadBytes = BlackboxSourcePayloadOffsetBytes - (BlackboxHashHistoryOffsetBytes + BlackboxHashHistoryBytes);
        private const int BlackboxMockPhysicsOffsetBytes = BlackboxSourcePayloadOffsetBytes + (BlackboxMaxSourceCount * BlackboxSourceStrideBytes);
        private const int BlackboxMockOriginOffsetBytes = BlackboxMockPhysicsOffsetBytes + BlackboxSourceStrideBytes;
        private const int BlackboxFrameStrideBytes = BlackboxMockOriginOffsetBytes + BlackboxSourceStrideBytes;
        private const int BlackboxDumpTimestampCharCount = 23;
        private const string BlackboxDumpTimestampFormat = "yyyyMMdd_HHmmss_fffffff";
        private const string BlackboxMmfFileName = "SHINOBU_33_Blackbox_OldFrames.mmf";
        private const string BlackboxMirrorFileName = "Dump_SHINOBU_33.bin";
        private const string BlackboxDumpPrefix = "Dump_CRASH_";
        private const string BlackboxDumpExtension = ".h8dump";
        private const string BlackboxMmfThreadName = "H8.BlackboxMMF";
        private const string BlackboxWatchdogThreadName = "H8.BlackboxWatchdog";
        private const uint BlackboxDumpMagic = 0x4838444Du; // H8DM
        private const uint BlackboxDumpVersion = 1u;
        private const uint BlackboxNanFatalHash = 0x4E414E21u; // NAN!
        private const uint BlackboxAupJitterFatalHash = 0x41555021u; // AUP!
        private const uint BlackboxWatchdogFatalHash = 0x57444721u; // WDG!
        private const uint BlackboxDesyncWarningHash = 0x4453594Eu; // DSYN
        private const uint BlackboxSourceFlagFloatScan = 1u << 0;
        private const uint MockOriginTeleportFlag = 1u << 0;
        private const float AupJitterFatalDistanceSq = 500f * 500f;
        private const int BlackboxMmfIdle = 0;
        private const int BlackboxMmfQueued = 1;
        private const int BlackboxMmfWriting = 2;

        private static NativeArray<byte> _blackboxBytes;
        private static NativeArray<byte> _blackboxMmfScratch;
        private static NativeArray<byte> _blackboxDumpHeader;
        private static NativeArray<TelemetryEventDTO> _blackboxEvents;
        private static NativeArray<BlackboxSourceSlot> _blackboxSources;
        private static NativeArray<TelemetryLoggingMaskDTO> _blackboxLoggingMasks;
        private static NativeArray<int> _blackboxAtomicState;
        private static NativeArray<int> _blackboxWatchdogCounters;
        private static NativeArray<int> _blackboxWatchdogSamples;
        private static NativeArray<int> _blackboxWatchdogStaleProbes;
        private static NativeArray<int> _blackboxWatchdogActive;
        private static IDataVault _blackboxVault;
        private static VaultBufferHandle<byte> _blackboxBytesHandle;
        private static VaultBufferHandle<byte> _blackboxMmfScratchHandle;
        private static VaultBufferHandle<byte> _blackboxDumpHeaderHandle;
        private static VaultBufferHandle<TelemetryEventDTO> _blackboxEventsHandle;
        private static VaultBufferHandle<BlackboxSourceSlot> _blackboxSourcesHandle;
        private static VaultBufferHandle<TelemetryLoggingMaskDTO> _blackboxLoggingMasksHandle;
        private static VaultBufferHandle<int> _blackboxAtomicStateHandle;
        private static VaultBufferHandle<int> _blackboxWatchdogCountersHandle;
        private static VaultBufferHandle<int> _blackboxWatchdogSamplesHandle;
        private static VaultBufferHandle<int> _blackboxWatchdogStaleProbesHandle;
        private static VaultBufferHandle<int> _blackboxWatchdogActiveHandle;
        private static int _blackboxActiveFrameCount;
        private static int _blackboxFrameWriteIndex;
        private static int _blackboxValidFrameCount;
        private static int _blackboxTotalFrameWrites;
        private static int _blackboxEventWriteCursor;
        private static int _blackboxSourceCount;
        private static int _blackboxDumpWritten;
        private static int _blackboxMmfState;
        private static int _blackboxMmfPendingBytes;
        private static int _blackboxMmfStopRequested;
        private static int _blackboxWatchdogStopRequested;
        private static int _blackboxDeterminismExpectedArmed;
        private static long _blackboxExpectedDeterminismHash64;
        private static long _blackboxLastDeterminismHash64;
        private static uint _blackboxAppVersionHash;
        private static MockPhysicsState _blackboxMockPhysicsState;
        private static MockOriginShiftSignal _blackboxMockOriginShiftSignal;
        private static int _blackboxMockPhysicsWritten;
        private static int _blackboxMockOriginWritten;
        private static bool _blackboxVaultBacked;
        private static int _blackboxVaultLocksHeld;
        private static string _blackboxAgentLogDirectory;
        private static string _blackboxMmfPath;
        private static Thread _blackboxMmfThread;
        private static Thread _blackboxWatchdogThread;
        private static AutoResetEvent _blackboxMmfSignal;
        // COLD ALLOC: object[1] - SHINOBU blackbox native allocation and source registration gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxGate = new object();
        // COLD ALLOC: object[1] - SHINOBU MMF flusher lifecycle gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxMmfGate = new object();
        // COLD ALLOC: object[1] - SHINOBU crash dump writer serialization gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxDumpGate = new object();
        // COLD ALLOC: object[1] - SHINOBU watchdog lifecycle gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxWatchdogGate = new object();

        public static bool IsCatastrophicFailure => ReadBlackboxAtomic(0) != 0;

        public static int BlackboxActiveFrameCount => Volatile.Read(ref _blackboxActiveFrameCount);

        public static int BlackboxValidFrameCount => Volatile.Read(ref _blackboxValidFrameCount);

        /// <summary>
        /// Pushes a 16-byte telemetry event into the atomic unmanaged event ring.
        /// </summary>
        public static void PushEvent(uint eventHash, float scalarValue)
        {
            PushEvent(eventHash, scalarValue, 0u);
        }

        /// <summary>
        /// Pushes a 16-byte telemetry event into the atomic unmanaged event ring.
        /// </summary>
        public static void PushEvent(uint eventHash, float scalarValue, uint entityId)
        {
            if (eventHash == 0u)
                return;

            if (!_blackboxEvents.IsCreated)
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return;

                EnsureBlackboxInitialized();
                if (!_blackboxEvents.IsCreated)
                    return;
            }

            float safeScalar = scalarValue;
            if (!math.isfinite(safeScalar))
            {
                safeScalar = 0f;
                SetCatastrophicFailure(BlackboxNanFatalHash);
            }

            int writeIndex = Interlocked.Increment(ref _blackboxEventWriteCursor) - 1;
            int slot = writeIndex & BlackboxEventMask;
            TelemetryEventDTO telemetryEvent = default;
            telemetryEvent.EventHash = eventHash;
            telemetryEvent.ScalarValue = safeScalar;
            telemetryEvent.EntityId = entityId;
            try
            {
                if (_blackboxEvents.IsCreated && _blackboxEvents.Length > 0)
                    _blackboxEvents[slot] = telemetryEvent;
            }
            catch (Exception)
            {
                // Fault-path only: telemetry must never recursively crash while the blackbox is shutting down.
            }
        }

        /// <summary>
        /// Registers a 64-byte-or-smaller unmanaged payload source copied into each blackbox frame.
        /// </summary>
        public static unsafe bool TryRegisterBlackboxSource(
            uint sourceHash,
            void* sourcePtr,
            int payloadBytes,
            uint flags,
            out int slot)
        {
            slot = -1;
            if (sourceHash == 0u || sourcePtr == null || payloadBytes <= 0 || payloadBytes > BlackboxSourceStrideBytes)
                return false;

            if (!_blackboxSources.IsCreated)
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!_blackboxSources.IsCreated)
                return false;

            lock (_blackboxGate)
            {
                int count = math.min(_blackboxSourceCount, BlackboxMaxSourceCount);
                for (int i = 0; i < count; i++)
                {
                    BlackboxSourceSlot existing = _blackboxSources[i];
                    if (existing.SourceHash != sourceHash)
                        continue;

                    existing.SourcePtr = (byte*)sourcePtr;
                    existing.PayloadBytes = payloadBytes;
                    existing.Flags = flags;
                    _blackboxSources[i] = existing;
                    slot = i;
                    return true;
                }

                if (count >= BlackboxMaxSourceCount)
                    return false;

                BlackboxSourceSlot sourceSlot = default;
                sourceSlot.SourceHash = sourceHash;
                sourceSlot.SourcePtr = (byte*)sourcePtr;
                sourceSlot.PayloadBytes = payloadBytes;
                sourceSlot.Flags = flags;
                _blackboxSources[count] = sourceSlot;
                _blackboxSourceCount = count + 1;
                slot = count;
                return true;
            }
        }

        /// <summary>
        /// Removes a registered blackbox source by hash.
        /// </summary>
        public static void UnregisterBlackboxSource(uint sourceHash)
        {
            if (sourceHash == 0u || !_blackboxSources.IsCreated)
                return;

            lock (_blackboxGate)
            {
                int count = math.min(_blackboxSourceCount, BlackboxMaxSourceCount);
                for (int i = 0; i < count; i++)
                {
                    if (_blackboxSources[i].SourceHash != sourceHash)
                        continue;

                    int last = count - 1;
                    _blackboxSources[i] = _blackboxSources[last];
                    _blackboxSources[last] = default;
                    _blackboxSourceCount = last;
                    return;
                }
            }
        }

        /// <summary>
        /// Exposes a raw view suitable for Burst job field injection.
        /// </summary>
        public static unsafe bool TryGetBlackboxRingBuffer(out BlackboxRingBufferDTO dto)
        {
            dto = default;
            if (!_blackboxBytes.IsCreated)
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!_blackboxBytes.IsCreated)
                return false;

            dto.Bytes = (byte*)_blackboxBytes.GetUnsafePtr();
            dto.FrameCapacity = _blackboxBytes.Length / BlackboxFrameStrideBytes;
            dto.ActiveFrameCount = Volatile.Read(ref _blackboxActiveFrameCount);
            dto.FrameStrideBytes = BlackboxFrameStrideBytes;
            dto.ValidFrameCount = Volatile.Read(ref _blackboxValidFrameCount);
            dto.WriteIndex = Volatile.Read(ref _blackboxFrameWriteIndex);
            dto.TotalWrites = Volatile.Read(ref _blackboxTotalFrameWrites);
            dto.FatalHash = unchecked((uint)ReadBlackboxAtomic(1));
            return true;
        }

        /// <summary>
        /// Records a blind physics state without depending on the real physics owner.
        /// </summary>
        public static void PushMockPhysicsState(in MockPhysicsState state)
        {
            if (!_blackboxBytes.IsCreated && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return;

            EnsureBlackboxInitialized();
            MockPhysicsState copy = state;
            if (!IsFinite(copy.Position) || !IsFinite(copy.Velocity) || !math.isfinite(copy.AngularSpeed))
                SetCatastrophicFailure(BlackboxNanFatalHash);

            _blackboxMockPhysicsState = copy;
            Volatile.Write(ref _blackboxMockPhysicsWritten, 1);
            PushEvent(0x4D504859u, copy.AngularSpeed, copy.EntityId); // MPHY
        }

        /// <summary>
        /// Records a blind origin shift signal and checks the AUP jitter threshold.
        /// </summary>
        public static void PushMockOriginShift(in MockOriginShiftSignal signal)
        {
            if (!_blackboxBytes.IsCreated && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return;

            EnsureBlackboxInitialized();
            MockOriginShiftSignal copy = signal;
            if (!IsFinite(copy.DeltaLocalMeters) || !IsFinite(copy.ImpactPosition))
            {
                SetCatastrophicFailure(BlackboxNanFatalHash);
            }
            else if ((copy.Flags & MockOriginTeleportFlag) == 0u &&
                     math.lengthsq(copy.DeltaLocalMeters) > AupJitterFatalDistanceSq)
            {
                SetCatastrophicFailure(BlackboxAupJitterFatalHash);
            }

            _blackboxMockOriginShiftSignal = copy;
            Volatile.Write(ref _blackboxMockOriginWritten, 1);
            PushEvent(0x4D4F5348u, copy.DeltaLocalMeters.x, copy.SourceHash); // MOSH
        }

        /// <summary>
        /// Applies one CSV line in `key,mask` form without allocating inside the parser.
        /// </summary>
        public static bool TryApplyTelemetryFlagCsvLine(ReadOnlySpan<char> line)
        {
            if (!TrySplitTelemetryFlagLine(line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> maskText))
                return false;
            if (!TryParseUInt(maskText, out uint mask))
                return false;

            uint keyHash = ComputeFnv1A(key);
            SetActiveLoggingMask(keyHash, mask);
            return true;
        }

        /// <summary>
        /// Returns the active unmanaged logging mask for a prehashed system key.
        /// </summary>
        public static uint GetActiveLoggingMask(uint systemHash)
        {
            if (systemHash == 0u || !_blackboxLoggingMasks.IsCreated)
                return 0u;

            for (int i = 0; i < _blackboxLoggingMasks.Length; i++)
            {
                TelemetryLoggingMaskDTO entry = _blackboxLoggingMasks[i];
                if (entry.SystemHash == systemHash)
                    return entry.Mask;
            }

            return 0u;
        }

        /// <summary>
        /// Requests a synchronous crash dump with a caller-owned fatal hash.
        /// </summary>
        public static bool TryDumpBlackboxNow(uint fatalHash)
        {
            if (!_blackboxBytes.IsCreated && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return false;

            EnsureBlackboxInitialized();
            SetCatastrophicFailure(fatalHash);
            return TryWriteBlackboxDumpSynchronous(fatalHash == 0u ? BlackboxEmergencyFlushHash : fatalHash);
        }

        public static void SignalBlackboxWatchdog(int lane)
        {
            if ((uint)lane >= BlackboxWatchdogLaneCount)
                return;

            if (!_blackboxWatchdogCounters.IsCreated)
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return;

                EnsureBlackboxInitialized();
            }

            if (!_blackboxWatchdogCounters.IsCreated || !_blackboxWatchdogActive.IsCreated)
                return;

            unsafe
            {
                try
                {
                    int* counters = (int*)_blackboxWatchdogCounters.GetUnsafePtr();
                    int* active = (int*)_blackboxWatchdogActive.GetUnsafePtr();
                    Volatile.Write(ref active[lane], 1);
                    Interlocked.Increment(ref counters[lane]);
                }
                catch (Exception)
                {
                    // Shutdown races can invalidate vault-backed arrays before threaded log callbacks drain.
                }
            }
        }

        public static void ArmBlackboxExpectedDeterminismHash(ulong expectedHash64)
        {
            Interlocked.Exchange(ref _blackboxExpectedDeterminismHash64, unchecked((long)expectedHash64));
            Volatile.Write(ref _blackboxDeterminismExpectedArmed, expectedHash64 == 0UL ? 0 : 1);
        }

        private static void EnsureBlackboxInitialized()
        {
            if (_blackboxBytes.IsCreated)
                return;

            lock (_blackboxGate)
            {
                if (_blackboxBytes.IsCreated)
                    return;

                DisposeBlackboxArraysNoLock();

                int desiredFrameCount = ResolveBlackboxFrameCount();
                int byteCount = desiredFrameCount * BlackboxFrameStrideBytes;
                if (!TryBindBlackboxVaultBuffersNoLock(desiredFrameCount, byteCount))
                    return;
                ClearBlackboxControlStateNoLock();

                _blackboxActiveFrameCount = desiredFrameCount;
                _blackboxFrameWriteIndex = 0;
                _blackboxValidFrameCount = 0;
                _blackboxTotalFrameWrites = 0;
                _blackboxEventWriteCursor = 0;
                _blackboxSourceCount = 0;
                _blackboxDumpWritten = 0;
                _blackboxAppVersionHash = ComputeContextHash(Application.version);
                _blackboxAgentLogDirectory = ResolveAgentLogDirectory();
                _blackboxMmfPath = Path.Combine(_blackboxAgentLogDirectory, BlackboxMmfFileName);
                StartBlackboxMmfThread();
                StartBlackboxWatchdogThread();
            }
        }

        private static bool TryBindBlackboxVaultBuffersNoLock(int desiredFrameCount, int byteCount)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            int mmfFrames = math.min(BlackboxMmfFlushFrameCount, desiredFrameCount);
            int mmfByteCount = mmfFrames * BlackboxFrameStrideBytes;

            VaultBufferHandle<byte> bytesHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuCrashBlackboxBytes,
                byteCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultBufferHandle<byte> mmfScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuCrashMmfScratch,
                mmfByteCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultBufferHandle<byte> dumpHeaderHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuCrashDumpHeader,
                BlackboxDumpHeaderBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<TelemetryEventDTO> eventsHandle = vault.GetBufferHandle<TelemetryEventDTO>(
                BufferID.ShinobuCrashTelemetryEvents,
                BlackboxEventCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultBufferHandle<BlackboxSourceSlot> sourcesHandle = vault.GetBufferHandle<BlackboxSourceSlot>(
                BufferID.ShinobuCrashSourceSlots,
                BlackboxMaxSourceCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<TelemetryLoggingMaskDTO> loggingMasksHandle = vault.GetBufferHandle<TelemetryLoggingMaskDTO>(
                BufferID.ShinobuCrashLoggingMasks,
                BlackboxLoggingMaskCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<int> atomicStateHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuCrashAtomicState,
                2,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<int> watchdogCountersHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuCrashWatchdogCounters,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<int> watchdogSamplesHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuCrashWatchdogSamples,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<int> watchdogStaleProbesHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuCrashWatchdogStaleProbes,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<int> watchdogActiveHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuCrashWatchdogActive,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);

            NativeArray<byte> bytes = bytesHandle.Resolve(vault);
            NativeArray<byte> mmfScratch = mmfScratchHandle.Resolve(vault);
            NativeArray<byte> dumpHeader = dumpHeaderHandle.Resolve(vault);
            NativeArray<TelemetryEventDTO> events = eventsHandle.Resolve(vault);
            NativeArray<BlackboxSourceSlot> sources = sourcesHandle.Resolve(vault);
            NativeArray<TelemetryLoggingMaskDTO> loggingMasks = loggingMasksHandle.Resolve(vault);
            NativeArray<int> atomicState = atomicStateHandle.Resolve(vault);
            NativeArray<int> watchdogCounters = watchdogCountersHandle.Resolve(vault);
            NativeArray<int> watchdogSamples = watchdogSamplesHandle.Resolve(vault);
            NativeArray<int> watchdogStaleProbes = watchdogStaleProbesHandle.Resolve(vault);
            NativeArray<int> watchdogActive = watchdogActiveHandle.Resolve(vault);

            if (!bytes.IsCreated || bytes.Length < byteCount ||
                !mmfScratch.IsCreated || mmfScratch.Length < mmfByteCount ||
                !dumpHeader.IsCreated || dumpHeader.Length < BlackboxDumpHeaderBytes ||
                !events.IsCreated || events.Length < BlackboxEventCapacity ||
                !sources.IsCreated || sources.Length < BlackboxMaxSourceCount ||
                !loggingMasks.IsCreated || loggingMasks.Length < BlackboxLoggingMaskCapacity ||
                !atomicState.IsCreated || atomicState.Length < 2 ||
                !watchdogCounters.IsCreated || watchdogCounters.Length < BlackboxWatchdogLaneCount ||
                !watchdogSamples.IsCreated || watchdogSamples.Length < BlackboxWatchdogLaneCount ||
                !watchdogStaleProbes.IsCreated || watchdogStaleProbes.Length < BlackboxWatchdogLaneCount ||
                !watchdogActive.IsCreated || watchdogActive.Length < BlackboxWatchdogLaneCount)
            {
                return false;
            }

            _blackboxVault = vault;
            _blackboxBytesHandle = bytesHandle;
            _blackboxMmfScratchHandle = mmfScratchHandle;
            _blackboxDumpHeaderHandle = dumpHeaderHandle;
            _blackboxEventsHandle = eventsHandle;
            _blackboxSourcesHandle = sourcesHandle;
            _blackboxLoggingMasksHandle = loggingMasksHandle;
            _blackboxAtomicStateHandle = atomicStateHandle;
            _blackboxWatchdogCountersHandle = watchdogCountersHandle;
            _blackboxWatchdogSamplesHandle = watchdogSamplesHandle;
            _blackboxWatchdogStaleProbesHandle = watchdogStaleProbesHandle;
            _blackboxWatchdogActiveHandle = watchdogActiveHandle;
            _blackboxBytes = bytes;
            _blackboxMmfScratch = mmfScratch;
            _blackboxDumpHeader = dumpHeader;
            _blackboxEvents = events;
            _blackboxSources = sources;
            _blackboxLoggingMasks = loggingMasks;
            _blackboxAtomicState = atomicState;
            _blackboxWatchdogCounters = watchdogCounters;
            _blackboxWatchdogSamples = watchdogSamples;
            _blackboxWatchdogStaleProbes = watchdogStaleProbes;
            _blackboxWatchdogActive = watchdogActive;
            _blackboxVaultBacked = true;
            _blackboxVaultLocksHeld = 0;

            if (TryLockBlackboxVaultBuffersNoLock(vault))
                return true;

            ReleaseBlackboxVaultBindingsNoLock();
            return false;
        }

        private static unsafe void ClearBlackboxControlStateNoLock()
        {
            if (_blackboxDumpHeader.IsCreated)
                UnsafeUtility.MemClear(_blackboxDumpHeader.GetUnsafePtr(), _blackboxDumpHeader.Length);
            if (_blackboxSources.IsCreated)
                UnsafeUtility.MemClear(_blackboxSources.GetUnsafePtr(), _blackboxSources.Length * UnsafeUtility.SizeOf<BlackboxSourceSlot>());
            if (_blackboxLoggingMasks.IsCreated)
                UnsafeUtility.MemClear(_blackboxLoggingMasks.GetUnsafePtr(), _blackboxLoggingMasks.Length * UnsafeUtility.SizeOf<TelemetryLoggingMaskDTO>());
            if (_blackboxAtomicState.IsCreated)
                UnsafeUtility.MemClear(_blackboxAtomicState.GetUnsafePtr(), _blackboxAtomicState.Length * UnsafeUtility.SizeOf<int>());
            if (_blackboxWatchdogCounters.IsCreated)
                UnsafeUtility.MemClear(_blackboxWatchdogCounters.GetUnsafePtr(), _blackboxWatchdogCounters.Length * UnsafeUtility.SizeOf<int>());
            if (_blackboxWatchdogSamples.IsCreated)
                UnsafeUtility.MemClear(_blackboxWatchdogSamples.GetUnsafePtr(), _blackboxWatchdogSamples.Length * UnsafeUtility.SizeOf<int>());
            if (_blackboxWatchdogStaleProbes.IsCreated)
                UnsafeUtility.MemClear(_blackboxWatchdogStaleProbes.GetUnsafePtr(), _blackboxWatchdogStaleProbes.Length * UnsafeUtility.SizeOf<int>());
            if (_blackboxWatchdogActive.IsCreated)
                UnsafeUtility.MemClear(_blackboxWatchdogActive.GetUnsafePtr(), _blackboxWatchdogActive.Length * UnsafeUtility.SizeOf<int>());
        }

        private static bool TryLockBlackboxVaultBuffersNoLock(IDataVault vault)
        {
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashBlackboxBytes))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashMmfScratch))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashDumpHeader))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashTelemetryEvents))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashSourceSlots))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashLoggingMasks))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashAtomicState))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashWatchdogCounters))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashWatchdogSamples))
                return false;
            if (!TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashWatchdogStaleProbes))
                return false;
            return TryLockBlackboxVaultBufferNoLock(vault, BufferID.ShinobuCrashWatchdogActive);
        }

        private static bool TryLockBlackboxVaultBufferNoLock(IDataVault vault, BufferID bufferId)
        {
            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.CoreDiagnostics))
                return false;

            _blackboxVaultLocksHeld++;
            return true;
        }

        private static void ReleaseBlackboxVaultBindingsNoLock()
        {
            IDataVault vault = _blackboxVault;
            if (vault != null && _blackboxVaultLocksHeld > 0)
            {
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashBlackboxBytes);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashMmfScratch);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashDumpHeader);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashTelemetryEvents);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashSourceSlots);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashLoggingMasks);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashAtomicState);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashWatchdogCounters);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashWatchdogSamples);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashWatchdogStaleProbes);
                TryUnlockBlackboxVaultBufferNoThrow(vault, BufferID.ShinobuCrashWatchdogActive);
            }

            ClearBlackboxVaultBindingsNoLock();
        }

        private static void TryUnlockBlackboxVaultBufferNoThrow(IDataVault vault, BufferID bufferId)
        {
            try
            {
                vault.TryUnlockBuffer(bufferId, SystemID.CoreDiagnostics);
            }
            catch (Exception)
            {
            }
        }

        private static void ClearBlackboxVaultBindingsNoLock()
        {
            _blackboxVault = null;
            _blackboxBytesHandle = default;
            _blackboxMmfScratchHandle = default;
            _blackboxDumpHeaderHandle = default;
            _blackboxEventsHandle = default;
            _blackboxSourcesHandle = default;
            _blackboxLoggingMasksHandle = default;
            _blackboxAtomicStateHandle = default;
            _blackboxWatchdogCountersHandle = default;
            _blackboxWatchdogSamplesHandle = default;
            _blackboxWatchdogStaleProbesHandle = default;
            _blackboxWatchdogActiveHandle = default;
            _blackboxBytes = default;
            _blackboxMmfScratch = default;
            _blackboxDumpHeader = default;
            _blackboxEvents = default;
            _blackboxSources = default;
            _blackboxLoggingMasks = default;
            _blackboxAtomicState = default;
            _blackboxWatchdogCounters = default;
            _blackboxWatchdogSamples = default;
            _blackboxWatchdogStaleProbes = default;
            _blackboxWatchdogActive = default;
            _blackboxVaultBacked = false;
            _blackboxVaultLocksHeld = 0;
        }

        private static void DisposeBlackboxState()
        {
            StopBlackboxWatchdogThread();
            StopBlackboxMmfThread();
            lock (_blackboxGate)
            {
                DisposeBlackboxArraysNoLock();
                _blackboxActiveFrameCount = 0;
                _blackboxFrameWriteIndex = 0;
                _blackboxValidFrameCount = 0;
                _blackboxTotalFrameWrites = 0;
                _blackboxEventWriteCursor = 0;
                _blackboxSourceCount = 0;
                _blackboxDumpWritten = 0;
                _blackboxMmfState = BlackboxMmfIdle;
                _blackboxMmfPendingBytes = 0;
                _blackboxWatchdogStopRequested = 0;
                _blackboxDeterminismExpectedArmed = 0;
                _blackboxExpectedDeterminismHash64 = 0L;
                _blackboxLastDeterminismHash64 = 0L;
                _blackboxMockPhysicsWritten = 0;
                _blackboxMockOriginWritten = 0;
                _blackboxAgentLogDirectory = null;
                _blackboxMmfPath = null;
            }
        }

        private static void CommitBlackboxFrame()
        {
            if (!_blackboxBytes.IsCreated)
            {
                EnsureBlackboxInitialized();
                if (!_blackboxBytes.IsCreated)
                    return;
            }

            try
            {
                SignalBlackboxWatchdog(ShinobuBlackboxMainThreadWatchdogLane);

                unsafe
                {
                    int activeFrames = math.max(1, Volatile.Read(ref _blackboxActiveFrameCount));
                    int frameSlot = Volatile.Read(ref _blackboxFrameWriteIndex);
                    if ((uint)frameSlot >= activeFrames)
                        frameSlot = 0;

                byte* basePtr = (byte*)_blackboxBytes.GetUnsafePtr();
                byte* framePtr = basePtr + (frameSlot * BlackboxFrameStrideBytes);
                uint fatalHash = unchecked((uint)ReadBlackboxAtomic(1));
                TelemetryHeaderDTO header = default;
                header.Timestamp = unchecked((ulong)Stopwatch.GetTimestamp());
                header.FrameNumber = unchecked((uint)Time.frameCount);
                header.FatalHash = fatalHash;
                UnsafeUtility.CopyStructureToPtr(ref header, framePtr);
                UnsafeUtility.MemClear(framePtr + BlackboxHeaderPrefixBytes, BlackboxHeaderPadBytes);

                CopyBlackboxEventHashes(framePtr + BlackboxHashHistoryOffsetBytes);
                UnsafeUtility.MemClear(framePtr + BlackboxHashHistoryOffsetBytes + BlackboxHashHistoryBytes, BlackboxHashHistoryPadBytes);
                bool nonFinite = CopyBlackboxSourcePayloads(framePtr + BlackboxSourcePayloadOffsetBytes);
                nonFinite |= CopyBlackboxMockPayloads(framePtr + BlackboxMockPhysicsOffsetBytes, framePtr + BlackboxMockOriginOffsetBytes);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                VerifyBlackboxDeterminism(framePtr);
#endif

                if (nonFinite)
                {
                    SetCatastrophicFailure(BlackboxNanFatalHash);
                    header.FatalHash = BlackboxNanFatalHash;
                    UnsafeUtility.CopyStructureToPtr(ref header, framePtr);
                }

                int nextSlot = frameSlot + 1;
                if (nextSlot >= activeFrames)
                    nextSlot = 0;
                Volatile.Write(ref _blackboxFrameWriteIndex, nextSlot);
                int validFrameCount = Volatile.Read(ref _blackboxValidFrameCount);
                if (validFrameCount < activeFrames)
                    Volatile.Write(ref _blackboxValidFrameCount, validFrameCount + 1);
                Interlocked.Increment(ref _blackboxTotalFrameWrites);

                    if (ReadBlackboxAtomic(0) != 0 &&
                        Interlocked.CompareExchange(ref _blackboxDumpWritten, 1, 0) == 0)
                    {
                        HaltDispatchersForCatastrophe(header.FatalHash);
                        if (!TryWriteBlackboxDumpSynchronous(header.FatalHash))
                            Interlocked.Exchange(ref _blackboxDumpWritten, 0);
                    }
                }
            }
            catch (Exception)
            {
                // Blackbox commit is diagnostic; it must fail closed during shutdown/crash callbacks.
            }
        }

        private static unsafe void CopyBlackboxEventHashes(byte* destination)
        {
            uint* hashDestination = (uint*)destination;
            int cursor = Volatile.Read(ref _blackboxEventWriteCursor);
            int available = math.min(math.max(0, cursor), BlackboxHashHistoryCount);
            int start = cursor - available;
            int pad = BlackboxHashHistoryCount - available;
            for (int i = 0; i < pad; i++)
                hashDestination[i] = 0u;
            for (int i = 0; i < available; i++)
            {
                TelemetryEventDTO entry = _blackboxEvents[(start + i) & BlackboxEventMask];
                hashDestination[pad + i] = entry.EventHash;
            }
        }

        private static unsafe bool CopyBlackboxSourcePayloads(byte* destination)
        {
            bool nonFinite = false;
            int sourceCount = math.min(_blackboxSourceCount, BlackboxMaxSourceCount);
            for (int i = 0; i < BlackboxMaxSourceCount; i++)
            {
                byte* target = destination + (i * BlackboxSourceStrideBytes);
                UnsafeUtility.MemClear(target, BlackboxSourceStrideBytes);
                if (i >= sourceCount)
                    continue;

                BlackboxSourceSlot source = _blackboxSources[i];
                if (source.SourcePtr == null || source.PayloadBytes <= 0)
                    continue;

                int copyBytes = math.min(source.PayloadBytes, BlackboxSourceStrideBytes);
                UnsafeUtility.MemCpy(target, source.SourcePtr, copyBytes);
                if ((source.Flags & BlackboxSourceFlagFloatScan) != 0u &&
                    ContainsNonFiniteFloat(target, copyBytes))
                {
                    nonFinite = true;
                }
            }

            return nonFinite;
        }

        private static unsafe bool CopyBlackboxMockPayloads(byte* physicsTarget, byte* originTarget)
        {
            bool nonFinite = false;
            UnsafeUtility.MemClear(physicsTarget, BlackboxSourceStrideBytes);
            UnsafeUtility.MemClear(originTarget, BlackboxSourceStrideBytes);

            if (Volatile.Read(ref _blackboxMockPhysicsWritten) != 0)
            {
                MockPhysicsState physicsState = _blackboxMockPhysicsState;
                UnsafeUtility.CopyStructureToPtr(ref physicsState, physicsTarget);
                if (!IsFinite(physicsState.Position) ||
                    !IsFinite(physicsState.Velocity) ||
                    !math.isfinite(physicsState.AngularSpeed) ||
                    !math.isfinite(physicsState.Mass) ||
                    !math.isfinite(physicsState.Drag) ||
                    !math.isfinite(physicsState.Buoyancy))
                {
                    nonFinite = true;
                }
            }

            if (Volatile.Read(ref _blackboxMockOriginWritten) != 0)
            {
                MockOriginShiftSignal originSignal = _blackboxMockOriginShiftSignal;
                UnsafeUtility.CopyStructureToPtr(ref originSignal, originTarget);
                if (!IsFinite(originSignal.DeltaLocalMeters) || !IsFinite(originSignal.ImpactPosition))
                    nonFinite = true;
            }

            return nonFinite;
        }

        private static unsafe bool ContainsNonFiniteFloat(byte* payload, int payloadBytes)
        {
            int floatCount = payloadBytes >> 2;
            float* values = (float*)payload;
            for (int i = 0; i < floatCount; i++)
            {
                if (!math.isfinite(values[i]))
                    return true;
            }

            return false;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static unsafe void VerifyBlackboxDeterminism(byte* framePtr)
        {
            int hotPayloadBytes = BlackboxFrameStrideBytes - BlackboxSourcePayloadOffsetBytes;
            ulong currentHash = ComputeBlackboxHash64(framePtr + BlackboxSourcePayloadOffsetBytes, hotPayloadBytes);
            Interlocked.Exchange(ref _blackboxLastDeterminismHash64, unchecked((long)currentHash));

            if (Volatile.Read(ref _blackboxDeterminismExpectedArmed) == 0)
                return;

            Volatile.Write(ref _blackboxDeterminismExpectedArmed, 0);
            ulong expectedHash = unchecked((ulong)Volatile.Read(ref _blackboxExpectedDeterminismHash64));
            if (expectedHash == 0UL || expectedHash == currentHash)
                return;

            PushEvent(BlackboxDesyncWarningHash, unchecked((uint)currentHash), unchecked((uint)(currentHash >> 32)));
        }

        private static unsafe ulong ComputeBlackboxHash64(byte* payload, int payloadBytes)
        {
            if (payload == null || payloadBytes <= 0)
                return 0UL;

            uint2 hash = xxHash3.Hash64(payload, payloadBytes);
            return ((ulong)hash.y << 32) | hash.x;
        }
#endif

        private static void RequestBlackboxEmergencyDumpAsync(uint fatalHash)
        {
            uint safeFatalHash = fatalHash == 0u ? BlackboxEmergencyFlushHash : fatalHash;
            SetCatastrophicFailure(safeFatalHash);
            if (Interlocked.CompareExchange(ref _blackboxDumpWritten, 1, 0) != 0)
                return;

            bool wrote = Thread.CurrentThread.ManagedThreadId == _mainThreadId
                ? TryWriteBlackboxDumpSynchronous(safeFatalHash)
                : TryWriteBlackboxDumpFromBackground(safeFatalHash);
            if (!wrote)
                Interlocked.Exchange(ref _blackboxDumpWritten, 0);
        }

        private static bool TryWriteBlackboxDumpFromBackground(uint fatalHash)
        {
#if UNITY_EDITOR
            return false;
#else
            if (!_blackboxBytes.IsCreated || string.IsNullOrEmpty(_blackboxAgentLogDirectory))
                return false;

            return WriteBlackboxDumpToPaths(fatalHash);
#endif
        }

        private static bool TryWriteBlackboxDumpSynchronous(uint fatalHash)
        {
            if (!_blackboxBytes.IsCreated)
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!_blackboxBytes.IsCreated)
                return false;

            return WriteBlackboxDumpToPaths(fatalHash);
        }

        private static bool WriteBlackboxDumpToPaths(uint fatalHash)
        {
            string directory = _blackboxAgentLogDirectory;
            if (string.IsNullOrEmpty(directory))
                return false;

            lock (_blackboxDumpGate)
            {
                try
                {
                    if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                        return false;

                    Directory.CreateDirectory(directory);
                    DateTime generatedUtc = DateTime.UtcNow;
                    string crashPath = BuildBlackboxDumpPath(directory, generatedUtc);
                    int payloadBytes = WriteBlackboxDumpHeader(fatalHash, generatedUtc, validFrames, activeFrames);
                    if (payloadBytes <= BlackboxDumpHeaderBytes)
                        return false;

                    bool wroteCrash = WriteBlackboxDumpFile(crashPath, validFrames, activeFrames, writeIndex);
                    string mirrorPath = Path.Combine(directory, BlackboxMirrorFileName);
                    bool wroteMirror = WriteBlackboxDumpFile(mirrorPath, validFrames, activeFrames, writeIndex);
                    return wroteCrash || wroteMirror;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static unsafe bool WriteBlackboxDumpFile(string path, int validFrames, int activeFrames, int writeIndex)
        {
            if (string.IsNullOrEmpty(path) || validFrames <= 0 || activeFrames <= 0)
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte* headerPtr = (byte*)_blackboxDumpHeader.GetUnsafeReadOnlyPtr();
                stream.Write(new ReadOnlySpan<byte>(headerPtr, BlackboxDumpHeaderBytes));

                byte* basePtr = (byte*)_blackboxBytes.GetUnsafeReadOnlyPtr();
                int oldestSlot = validFrames >= activeFrames ? writeIndex : 0;
                for (int i = 0; i < validFrames; i++)
                {
                    int slot = oldestSlot + i;
                    if (slot >= activeFrames)
                        slot -= activeFrames;
                    byte* framePtr = basePtr + (slot * BlackboxFrameStrideBytes);
                    stream.Write(new ReadOnlySpan<byte>(framePtr, BlackboxFrameStrideBytes));
                }

                stream.Flush(true);
            }

            return true;
        }

        private static unsafe int WriteBlackboxDumpHeader(uint fatalHash, DateTime generatedUtc, int validFrames, int activeFrames)
        {
            int payloadBytes = validFrames * BlackboxFrameStrideBytes;
            byte* headerPtr = (byte*)_blackboxDumpHeader.GetUnsafePtr();
            UnsafeUtility.MemClear(headerPtr, BlackboxDumpHeaderBytes);

            TelemetryHeaderDTO prefix = default;
            prefix.Timestamp = unchecked((ulong)generatedUtc.Ticks);
            prefix.FrameNumber = unchecked((uint)math.max(0, Volatile.Read(ref _blackboxTotalFrameWrites)));
            prefix.FatalHash = fatalHash == 0u ? unchecked((uint)ReadBlackboxAtomic(1)) : fatalHash;
            UnsafeUtility.CopyStructureToPtr(ref prefix, headerPtr);

            uint* metadata = (uint*)(headerPtr + BlackboxHeaderPrefixBytes);
            metadata[0] = BlackboxDumpMagic;
            metadata[1] = BlackboxDumpVersion;
            metadata[2] = BlackboxDumpHeaderBytes;
            metadata[3] = unchecked((uint)validFrames);
            metadata[4] = BlackboxFrameStrideBytes;
            metadata[5] = unchecked((uint)payloadBytes);
            metadata[6] = _blackboxAppVersionHash;
            metadata[7] = unchecked((uint)activeFrames);
            metadata[8] = unchecked((uint)Volatile.Read(ref _blackboxSourceCount));
            metadata[9] = unchecked((uint)Volatile.Read(ref _blackboxEventWriteCursor));
            metadata[10] = unchecked((uint)BlackboxHashHistoryOffsetBytes);
            metadata[11] = unchecked((uint)BlackboxSourcePayloadOffsetBytes);
            metadata[12] = unchecked((uint)BlackboxMockPhysicsOffsetBytes);
            metadata[13] = unchecked((uint)BlackboxMockOriginOffsetBytes);
            ulong lastHash = unchecked((ulong)Volatile.Read(ref _blackboxLastDeterminismHash64));
            metadata[14] = unchecked((uint)lastHash);
            metadata[15] = unchecked((uint)(lastHash >> 32));
            metadata[16] = unchecked((uint)BlackboxCacheLineBytes);
            metadata[17] = unchecked((uint)BlackboxHeaderPadBytes);
            metadata[18] = unchecked((uint)BlackboxHashHistoryPadBytes);
            return payloadBytes + BlackboxDumpHeaderBytes;
        }

        private static bool TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex)
        {
            validFrames = 0;
            activeFrames = 0;
            writeIndex = 0;
            if (!_blackboxBytes.IsCreated)
                return false;

            int bufferFrames = _blackboxBytes.Length / BlackboxFrameStrideBytes;
            activeFrames = Volatile.Read(ref _blackboxActiveFrameCount);
            if (activeFrames <= 0 || activeFrames > bufferFrames)
                activeFrames = bufferFrames;
            if (activeFrames <= 0)
                return false;

            validFrames = math.min(math.max(0, Volatile.Read(ref _blackboxValidFrameCount)), activeFrames);
            if (validFrames <= 0)
                return false;

            writeIndex = Volatile.Read(ref _blackboxFrameWriteIndex);
            if ((uint)writeIndex >= activeFrames)
                writeIndex = 0;
            return true;
        }

        private static string BuildBlackboxDumpPath(string directory, DateTime generatedUtc)
        {
            int directoryLength = directory.Length;
            bool needsSeparator =
                directoryLength > 0 &&
                directory[directoryLength - 1] != Path.DirectorySeparatorChar &&
                directory[directoryLength - 1] != Path.AltDirectorySeparatorChar;
            int pathLength =
                directoryLength +
                (needsSeparator ? 1 : 0) +
                BlackboxDumpPrefix.Length +
                BlackboxDumpTimestampCharCount +
                BlackboxDumpExtension.Length;

            return string.Create(
                pathLength,
                (Directory: directory, Timestamp: generatedUtc, NeedsSeparator: needsSeparator),
                (span, state) =>
                {
                    int cursor = 0;
                    state.Directory.AsSpan().CopyTo(span);
                    cursor += state.Directory.Length;
                    if (state.NeedsSeparator)
                        span[cursor++] = Path.DirectorySeparatorChar;

                    BlackboxDumpPrefix.AsSpan().CopyTo(span.Slice(cursor));
                    cursor += BlackboxDumpPrefix.Length;

                    Span<char> timestampSpan = span.Slice(cursor, BlackboxDumpTimestampCharCount);
                    if (!state.Timestamp.TryFormat(timestampSpan, out int _, BlackboxDumpTimestampFormat.AsSpan(), System.Globalization.CultureInfo.InvariantCulture))
                        timestampSpan.Fill('0');
                    cursor += BlackboxDumpTimestampCharCount;

                    BlackboxDumpExtension.AsSpan().CopyTo(span.Slice(cursor));
                });
        }

        private static void SetCatastrophicFailure(uint fatalHash)
        {
            if (fatalHash == 0u)
                fatalHash = BlackboxEmergencyFlushHash;

            if (_blackboxAtomicState.IsCreated && _blackboxAtomicState.Length >= 2)
            {
                try
                {
                    unsafe
                    {
                        int* state = (int*)_blackboxAtomicState.GetUnsafePtr();
                        Interlocked.Exchange(ref state[0], 1);
                        Interlocked.Exchange(ref state[1], unchecked((int)fatalHash));
                    }
                }
                catch (Exception)
                {
                    // Crash telemetry may be called from log/crash callbacks while Unity is tearing native memory down.
                }
            }
        }

        private static int ReadBlackboxAtomic(int index)
        {
            if (!_blackboxAtomicState.IsCreated || (uint)index >= _blackboxAtomicState.Length)
                return 0;

            try
            {
                unsafe
                {
                    return Volatile.Read(ref _blackboxAtomicState.GetUnsafePtrAsIntRef(index));
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void HaltDispatchersForCatastrophe(uint fatalHash)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return;

            GlobalRegistry.SetSystemKillSwitchBits(uint.MaxValue, true);
            SystemDispatcher.ApplyHomeostasisKillSwitch(
                ulong.MaxValue,
                byte.MaxValue,
                0,
                slowTick2Hz: true,
                forceTimeDilation09: true,
                reasonHash: fatalHash == 0u ? BlackboxEmergencyFlushHash : fatalHash);
        }

        private static void RequestBlackboxMmfFlushAsync()
        {
            if (!_blackboxBytes.IsCreated || !_blackboxMmfScratch.IsCreated)
                return;
            if (Interlocked.CompareExchange(ref _blackboxMmfState, BlackboxMmfQueued, BlackboxMmfIdle) != BlackboxMmfIdle)
                return;

            bool queued = false;
            try
            {
                int pendingBytes = CopyOldestFramesToMmfScratch();
                if (pendingBytes <= 0)
                    return;

                Volatile.Write(ref _blackboxMmfPendingBytes, pendingBytes);
                if (StartBlackboxMmfThread())
                {
                    AutoResetEvent signal = Volatile.Read(ref _blackboxMmfSignal);
                    if (signal != null)
                    {
                        signal.Set();
                        queued = true;
                    }
                }
            }
            finally
            {
                if (!queued)
                    Volatile.Write(ref _blackboxMmfState, BlackboxMmfIdle);
            }
        }

        private static unsafe int CopyOldestFramesToMmfScratch()
        {
            if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                return 0;

            int frameCount = math.min(validFrames, _blackboxMmfScratch.Length / BlackboxFrameStrideBytes);
            if (frameCount <= 0)
                return 0;

            byte* sourceBase = (byte*)_blackboxBytes.GetUnsafeReadOnlyPtr();
            byte* destinationBase = (byte*)_blackboxMmfScratch.GetUnsafePtr();
            int oldestSlot = validFrames >= activeFrames ? writeIndex : 0;
            for (int i = 0; i < frameCount; i++)
            {
                int slot = oldestSlot + i;
                if (slot >= activeFrames)
                    slot -= activeFrames;
                UnsafeUtility.MemCpy(
                    destinationBase + (i * BlackboxFrameStrideBytes),
                    sourceBase + (slot * BlackboxFrameStrideBytes),
                    BlackboxFrameStrideBytes);
            }

            return frameCount * BlackboxFrameStrideBytes;
        }

        private static bool StartBlackboxMmfThread()
        {
            lock (_blackboxMmfGate)
            {
                if (_blackboxMmfThread != null)
                {
                    if (_blackboxMmfThread.IsAlive)
                        return Volatile.Read(ref _blackboxMmfStopRequested) == 0;

                    _blackboxMmfSignal?.Dispose();
                    _blackboxMmfSignal = null;
                    _blackboxMmfThread = null;
                }

                try
                {
                    Volatile.Write(ref _blackboxMmfStopRequested, 0);
                    // COLD ALLOC: AutoResetEvent[1] - SHINOBU MMF flush wake signal - owner: GlobalTelemetryBus
                    _blackboxMmfSignal = new AutoResetEvent(false);
                    // COLD ALLOC: Thread[1] - SHINOBU oldest-frame MMF flusher - owner: GlobalTelemetryBus
                    _blackboxMmfThread = new Thread(RunBlackboxMmfThread)
                    {
                        IsBackground = true,
                        Name = BlackboxMmfThreadName,
                        Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                    };
                    _blackboxMmfThread.Start();
                    return true;
                }
                catch (Exception)
                {
                    _blackboxMmfSignal?.Dispose();
                    _blackboxMmfSignal = null;
                    _blackboxMmfThread = null;
                    return false;
                }
            }
        }

        private static void StopBlackboxMmfThread()
        {
            Thread thread;
            AutoResetEvent signal;
            lock (_blackboxMmfGate)
            {
                thread = _blackboxMmfThread;
                signal = _blackboxMmfSignal;
                if (thread == null)
                {
                    signal?.Dispose();
                    _blackboxMmfSignal = null;
                    Volatile.Write(ref _blackboxMmfStopRequested, 0);
                    return;
                }

                Volatile.Write(ref _blackboxMmfStopRequested, 1);
                signal?.Set();
            }

            if (!ReferenceEquals(Thread.CurrentThread, thread))
                thread.Join();

            lock (_blackboxMmfGate)
            {
                if (ReferenceEquals(_blackboxMmfThread, thread))
                    _blackboxMmfThread = null;
                if (ReferenceEquals(_blackboxMmfSignal, signal))
                    _blackboxMmfSignal = null;

                signal?.Dispose();
                Volatile.Write(ref _blackboxMmfStopRequested, 0);
            }
        }

        private static void RunBlackboxMmfThread()
        {
            while (Volatile.Read(ref _blackboxMmfStopRequested) == 0)
            {
                AutoResetEvent signal = Volatile.Read(ref _blackboxMmfSignal);
                if (signal == null)
                    return;

                try
                {
                    signal.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (Volatile.Read(ref _blackboxMmfStopRequested) != 0)
                    return;

                FlushMmfScratchToDisk();
            }
        }

        private static unsafe void FlushMmfScratchToDisk()
        {
            if (Interlocked.CompareExchange(ref _blackboxMmfState, BlackboxMmfWriting, BlackboxMmfQueued) != BlackboxMmfQueued)
                return;

            try
            {
                int pendingBytes = Volatile.Read(ref _blackboxMmfPendingBytes);
                string path = _blackboxMmfPath;
                if (pendingBytes <= 0 || string.IsNullOrEmpty(path) || !_blackboxMmfScratch.IsCreated)
                    return;

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    stream.SetLength(pendingBytes);
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, pendingBytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
                    using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, pendingBytes, MemoryMappedFileAccess.Write))
                    {
                        byte* destination = null;
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);
                        try
                        {
                            byte* source = (byte*)_blackboxMmfScratch.GetUnsafeReadOnlyPtr();
                            UnsafeUtility.MemCpy(destination, source, pendingBytes);
                        }
                        finally
                        {
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                Volatile.Write(ref _blackboxMmfPendingBytes, 0);
                Volatile.Write(ref _blackboxMmfState, BlackboxMmfIdle);
            }
        }

        private static bool StartBlackboxWatchdogThread()
        {
            lock (_blackboxWatchdogGate)
            {
                if (_blackboxWatchdogThread != null)
                {
                    if (_blackboxWatchdogThread.IsAlive)
                        return Volatile.Read(ref _blackboxWatchdogStopRequested) == 0;

                    _blackboxWatchdogThread = null;
                }

                try
                {
                    Volatile.Write(ref _blackboxWatchdogStopRequested, 0);
                    // COLD ALLOC: Thread[1] - SHINOBU 500 ms critical-system watchdog - owner: GlobalTelemetryBus
                    _blackboxWatchdogThread = new Thread(RunBlackboxWatchdogThread)
                    {
                        IsBackground = true,
                        Name = BlackboxWatchdogThreadName,
                        Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.Heartbeat)
                    };
                    _blackboxWatchdogThread.Start();
                    return true;
                }
                catch (Exception)
                {
                    _blackboxWatchdogThread = null;
                    return false;
                }
            }
        }

        private static void StopBlackboxWatchdogThread()
        {
            Thread thread;
            lock (_blackboxWatchdogGate)
            {
                Volatile.Write(ref _blackboxWatchdogStopRequested, 1);
                thread = _blackboxWatchdogThread;
            }

            if (thread != null && thread.IsAlive && !ReferenceEquals(Thread.CurrentThread, thread))
                thread.Join();

            lock (_blackboxWatchdogGate)
            {
                bool stopped = thread == null || !thread.IsAlive;
                if (stopped && ReferenceEquals(_blackboxWatchdogThread, thread))
                    _blackboxWatchdogThread = null;
                if (stopped)
                    Volatile.Write(ref _blackboxWatchdogStopRequested, 0);
            }
        }

        private static void RunBlackboxWatchdogThread()
        {
            while (Volatile.Read(ref _blackboxWatchdogStopRequested) == 0)
            {
                Thread.Sleep(BlackboxWatchdogProbeMilliseconds);
                if (Volatile.Read(ref _blackboxWatchdogStopRequested) != 0)
                    return;

                if (ProbeBlackboxWatchdog())
                {
                    SetCatastrophicFailure(BlackboxWatchdogFatalHash);
                    TryWriteBlackboxDumpFromBackground(BlackboxWatchdogFatalHash);
#if !UNITY_EDITOR
                    Process.GetCurrentProcess().Kill();
#endif
                    return;
                }
            }
        }

        private static unsafe bool ProbeBlackboxWatchdog()
        {
            if (!_blackboxWatchdogCounters.IsCreated ||
                !_blackboxWatchdogSamples.IsCreated ||
                !_blackboxWatchdogStaleProbes.IsCreated ||
                !_blackboxWatchdogActive.IsCreated)
            {
                return false;
            }

            try
            {
                int* counters = (int*)_blackboxWatchdogCounters.GetUnsafePtr();
                int* samples = (int*)_blackboxWatchdogSamples.GetUnsafePtr();
                int* staleProbes = (int*)_blackboxWatchdogStaleProbes.GetUnsafePtr();
                int* active = (int*)_blackboxWatchdogActive.GetUnsafePtr();
                for (int i = 0; i < BlackboxWatchdogLaneCount; i++)
                {
                    if (Volatile.Read(ref active[i]) == 0)
                        continue;

                    int counter = Volatile.Read(ref counters[i]);
                    int previous = Volatile.Read(ref samples[i]);
                    if (counter != previous)
                    {
                        Volatile.Write(ref samples[i], counter);
                        Volatile.Write(ref staleProbes[i], 0);
                        continue;
                    }

                    int stale = Interlocked.Increment(ref staleProbes[i]);
                    if (stale >= BlackboxWatchdogStaleProbeLimit)
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static void DisposeBlackboxArraysNoLock()
        {
            if (_blackboxVaultBacked)
            {
                ReleaseBlackboxVaultBindingsNoLock();
                return;
            }

            DisposeNativeArrayNoJob(ref _blackboxBytes);
            DisposeNativeArrayNoJob(ref _blackboxMmfScratch);
            DisposeNativeArrayNoJob(ref _blackboxDumpHeader);
            DisposeNativeArrayNoJob(ref _blackboxEvents);
            DisposeNativeArrayNoJob(ref _blackboxSources);
            DisposeNativeArrayNoJob(ref _blackboxLoggingMasks);
            DisposeNativeArrayNoJob(ref _blackboxAtomicState);
            DisposeNativeArrayNoJob(ref _blackboxWatchdogCounters);
            DisposeNativeArrayNoJob(ref _blackboxWatchdogSamples);
            DisposeNativeArrayNoJob(ref _blackboxWatchdogStaleProbes);
            DisposeNativeArrayNoJob(ref _blackboxWatchdogActive);
            ClearBlackboxVaultBindingsNoLock();
        }

        private static void DisposeNativeArrayNoJob<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static int ResolveBlackboxFrameCount()
        {
            return GlobalRegistry.ScalabilityTierProfileByte == ScalabilityTierProfiles.LowMx350 ||
                   HardwareTierDetector.SharedMemoryModeActive
                ? ShinobuBlackboxLowFrameCount
                : ShinobuBlackboxHighFrameCount;
        }

        private static string ResolveAgentLogDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
        }

        private static void SetActiveLoggingMask(uint systemHash, uint mask)
        {
            if (systemHash == 0u)
                return;

            EnsureBlackboxInitialized();
            if (!_blackboxLoggingMasks.IsCreated)
                return;

            for (int i = 0; i < _blackboxLoggingMasks.Length; i++)
            {
                TelemetryLoggingMaskDTO entry = _blackboxLoggingMasks[i];
                if (entry.SystemHash == systemHash || entry.SystemHash == 0u)
                {
                    entry.SystemHash = systemHash;
                    entry.Mask = mask;
                    entry.Version++;
                    _blackboxLoggingMasks[i] = entry;
                    return;
                }
            }
        }

        private static bool TrySplitTelemetryFlagLine(ReadOnlySpan<char> line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> mask)
        {
            key = default;
            mask = default;
            ReadOnlySpan<char> trimmed = Trim(line);
            if (trimmed.Length <= 0 || trimmed[0] == '#')
                return false;

            int separator = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == ',' || c == ';')
                {
                    separator = i;
                    break;
                }
            }

            if (separator <= 0 || separator >= trimmed.Length - 1)
                return false;

            key = Trim(trimmed.Slice(0, separator));
            mask = Trim(trimmed.Slice(separator + 1));
            return key.Length > 0 && mask.Length > 0;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool TryParseUInt(ReadOnlySpan<char> value, out uint result)
        {
            result = 0u;
            ReadOnlySpan<char> trimmed = Trim(value);
            if (trimmed.Length <= 0)
                return false;

            int index = 0;
            int numberBase = 10;
            if (trimmed.Length > 2 && trimmed[0] == '0' && (trimmed[1] == 'x' || trimmed[1] == 'X'))
            {
                index = 2;
                numberBase = 16;
            }

            uint parsed = 0u;
            for (; index < trimmed.Length; index++)
            {
                int digit = ParseDigit(trimmed[index]);
                if (digit < 0 || digit >= numberBase)
                    return false;

                parsed = unchecked((parsed * (uint)numberBase) + (uint)digit);
            }

            result = parsed;
            return true;
        }

        private static int ParseDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

#if UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, Size = 32)]
        public struct BlackboxEditorFrame
        {
            public uint FrameNumber;
            public uint FatalHash;
            public uint LastEventHash;
            public Vector3 ImpactPosition;
            public int Slot;
            public uint _pad0;
        }

        public static unsafe int CopyBlackboxEditorFrames(BlackboxEditorFrame[] destination)
        {
            if (destination == null || destination.Length <= 0 || !_blackboxBytes.IsCreated)
                return 0;

            if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                return 0;

            int copyCount = math.min(validFrames, destination.Length);
            if (copyCount <= 0)
                return 0;

            byte* basePtr = (byte*)_blackboxBytes.GetUnsafeReadOnlyPtr();
            int oldestSlot = validFrames >= activeFrames ? writeIndex : 0;
            int skip = validFrames - copyCount;
            for (int i = 0; i < copyCount; i++)
            {
                int slot = oldestSlot + skip + i;
                while (slot >= activeFrames)
                    slot -= activeFrames;

                byte* framePtr = basePtr + (slot * BlackboxFrameStrideBytes);
                TelemetryHeaderDTO header = UnsafeUtility.ReadArrayElement<TelemetryHeaderDTO>(framePtr, 0);
                MockOriginShiftSignal origin = UnsafeUtility.ReadArrayElement<MockOriginShiftSignal>(framePtr + BlackboxMockOriginOffsetBytes, 0);
                uint* hashes = (uint*)(framePtr + BlackboxHashHistoryOffsetBytes);
                BlackboxEditorFrame frame = default;
                frame.FrameNumber = header.FrameNumber;
                frame.FatalHash = header.FatalHash;
                frame.LastEventHash = hashes[BlackboxHashHistoryCount - 1];
                frame.ImpactPosition = new Vector3(origin.ImpactPosition.x, origin.ImpactPosition.y, origin.ImpactPosition.z);
                frame.Slot = slot;
                destination[i] = frame;
            }

            return copyCount;
        }

        public static int CopyBlackboxEditorEvents(TelemetryEventDTO[] destination)
        {
            if (destination == null || destination.Length <= 0 || !_blackboxEvents.IsCreated)
                return 0;

            int cursor = Volatile.Read(ref _blackboxEventWriteCursor);
            int available = math.min(math.max(0, cursor), math.min(destination.Length, BlackboxEventCapacity));
            int start = cursor - available;
            for (int i = 0; i < available; i++)
                destination[i] = _blackboxEvents[(start + i) & BlackboxEventMask];

            return available;
        }
#endif
    }

    internal static unsafe class BlackboxNativeArrayExtensions
    {
        public static ref int GetUnsafePtrAsIntRef(this NativeArray<int> array, int index)
        {
            return ref UnsafeUtility.AsRef<int>((int*)array.GetUnsafePtr() + index);
        }
    }
}
