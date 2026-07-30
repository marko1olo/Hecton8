using System;
using System.Diagnostics;
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
    /// Sixty-four-byte dump header DTO. The first 16 bytes remain the sealed on-disk prefix.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TelemetryHeaderDTO
    {
        [FieldOffset(0)] public ulong Timestamp;
        [FieldOffset(8)] public uint FrameNumber;
        [FieldOffset(12)] public uint FatalHash;
        [FieldOffset(16)] private ulong _pad0;
        [FieldOffset(24)] private ulong _pad1;
        [FieldOffset(32)] private ulong _pad2;
        [FieldOffset(40)] private ulong _pad3;
        [FieldOffset(48)] private ulong _pad4;
        [FieldOffset(56)] private ulong _pad5;
    }

    /// <summary>
    /// Sixty-four-byte event marker used as the allocation-free callstack surrogate.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TelemetryEventDTO
    {
        [FieldOffset(0)] public uint EventHash;
        [FieldOffset(4)] public float ScalarValue;
        [FieldOffset(8)] public uint EntityId;
        [FieldOffset(12)] public uint _pad0;
        [FieldOffset(16)] private ulong _pad1;
        [FieldOffset(24)] private ulong _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    /// <summary>
    /// Blind physics probe payload. Exactly 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockPhysicsState
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint EntityId;
        [FieldOffset(16)] public float3 Velocity;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public quaternion Rotation;
        [FieldOffset(48)] public float AngularSpeed;
        [FieldOffset(52)] public float Mass;
        [FieldOffset(56)] public float Drag;
        [FieldOffset(60)] public float Buoyancy;
    }

    /// <summary>
    /// Blind origin-shift signal used by SHINOBU_33 without depending on the real origin-shift owner.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockOriginShiftSignal
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public float3 DeltaLocalMeters;
        [FieldOffset(36)] public uint FrameNumber;
        [FieldOffset(40)] public uint ShiftId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public float3 ImpactPosition;
    }

    /// <summary>
    /// Raw view over the SHINOBU blackbox. Fields are intentionally public to avoid CS1612 copies.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal unsafe struct BlackboxRingBufferDTO
    {
        [FieldOffset(0)] internal byte* Bytes;
        [FieldOffset(8)] public int FrameCapacity;
        [FieldOffset(12)] public int ActiveFrameCount;
        [FieldOffset(16)] public int FrameStrideBytes;
        [FieldOffset(20)] public int ValidFrameCount;
        [FieldOffset(24)] public int WriteIndex;
        [FieldOffset(28)] public int TotalWrites;
        [FieldOffset(32)] public uint FatalHash;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public uint _pad1;
        [FieldOffset(44)] public uint _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;

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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct TelemetryLoggingMaskDTO
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint Mask;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint _pad0;
        [FieldOffset(16)] private ulong _pad1;
        [FieldOffset(24)] private ulong _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal unsafe struct BlackboxSourceSlot
    {
        [FieldOffset(0)] internal byte* SourcePtr;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int PayloadBytes;
        [FieldOffset(20)] public int _pad0;
        [FieldOffset(24)] private ulong _pad1;
        [FieldOffset(32)] private ulong _pad2;
        [FieldOffset(40)] private ulong _pad3;
        [FieldOffset(48)] private ulong _pad4;
        [FieldOffset(56)] private ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct NanSweeperJob : IJob
    {
        [FieldOffset(0)]
        [NoAlias] [NativeDisableUnsafePtrRestriction]
        internal byte* Payload;
        [FieldOffset(8)]
        public int PayloadBytes;
        [FieldOffset(12)]
        public uint FatalHash;
        [FieldOffset(16)]
        [NoAlias] [NativeDisableUnsafePtrRestriction]
        internal int* IsCatastrophicFailure;
        [FieldOffset(24)]
        [NoAlias] [NativeDisableUnsafePtrRestriction]
        internal int* FatalHashOutput;

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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct MockOriginShiftFireJob : IJob
    {
        [FieldOffset(0)]
        [NoAlias] [NativeDisableUnsafePtrRestriction]
        internal MockOriginShiftSignal* Output;
        [FieldOffset(8)]
        public int OutputLength;
        [FieldOffset(12)]
        public uint Seed;
        [FieldOffset(16)]
        public uint FrameNumber;
        [FieldOffset(20)]
        public uint _pad0;
        [FieldOffset(24)]
        public uint _pad1;
        [FieldOffset(28)]
        private uint _pad2;

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
        public const int ShinobuBlackboxFrameStrideBytes = BlackboxFrameStrideBytes;
        public const int ShinobuBlackboxSourceCapacity = BlackboxMaxSourceCount;
        public const int ShinobuBlackboxSourcePayloadBytes = BlackboxSourceStrideBytes;
        public const int ShinobuBlackboxMainThreadWatchdogLane = 0;
        public const uint ShinobuBlackboxSourceFlagFloatScan = BlackboxSourceFlagFloatScan;
        internal const uint BlackboxEmergencyFlushHash = 0x454D464Cu; // EMFL

        private const int BlackboxHeaderPrefixBytes = 16;
        private const int BlackboxDumpHeaderBytes = 1024;
        private const int BlackboxDumpMetadataUIntCapacity = (BlackboxDumpHeaderBytes - BlackboxHeaderPrefixBytes) / 4;
        private const int BlackboxCacheLineBytes = 64;
        private const int BlackboxHashHistoryCount = 100;
        private const int BlackboxHashHistoryBytes = BlackboxHashHistoryCount * 4;
        private const int BlackboxMaxSourceCount = 50;
        private const int BlackboxSourceStrideBytes = 64;
        private const int BlackboxDumpSourceDescriptorMetadataIndex = 32;
        private const int BlackboxDumpSourceDescriptorUIntStride = 4;
        private const int BlackboxEventCapacity = 4096;
        private const int BlackboxEventMask = BlackboxEventCapacity - 1;
        private const int BlackboxLoggingMaskCapacity = 64;
        private const int BlackboxWatchdogLaneCount = 64;
        private const int BlackboxWatchdogProbeMilliseconds = 500;
        private const int BlackboxWatchdogStopJoinMilliseconds = 750;
        private const int BlackboxWatchdogStaleProbeLimit = 4;
        private const int BlackboxHashHistoryOffsetBytes = BlackboxCacheLineBytes;
        private const int BlackboxHeaderPadBytes = BlackboxHashHistoryOffsetBytes - BlackboxHeaderPrefixBytes;
        private const int BlackboxSourcePayloadOffsetBytes = 512;
        private const int BlackboxHashHistoryPadBytes = BlackboxSourcePayloadOffsetBytes - (BlackboxHashHistoryOffsetBytes + BlackboxHashHistoryBytes);
        private const int BlackboxMockPhysicsOffsetBytes = BlackboxSourcePayloadOffsetBytes + (BlackboxMaxSourceCount * BlackboxSourceStrideBytes);
        private const int BlackboxMockOriginOffsetBytes = BlackboxMockPhysicsOffsetBytes + BlackboxSourceStrideBytes;
        private const int BlackboxFrameStrideBytes = BlackboxMockOriginOffsetBytes + BlackboxSourceStrideBytes;
        private const string BlackboxWatchdogThreadName = "H8.BlackboxWatchdog";
        private const string BlackboxDumpRelativePath = "Docs/AgentLogs/Dump_GLOBAL_TELEMETRY_BUS.bin";
        private const uint BlackboxDumpMagic = 0x4838444Du; // H8DM
        private const uint BlackboxDumpVersion = 2u;
        private const uint BlackboxNanFatalHash = 0x4E414E21u; // NAN!
        private const uint BlackboxAupJitterFatalHash = 0x41555021u; // AUP!
        private const uint BlackboxWatchdogFatalHash = 0x57444721u; // WDG!
        private const uint BlackboxDesyncWarningHash = 0x4453594Eu; // DSYN
        private const uint BlackboxSourceFlagFloatScan = 1u << 0;
        private const uint MockOriginTeleportFlag = 1u << 0;
        private const float AupJitterFatalDistanceSq = 500f * 500f;

        private static IDataVault _blackboxBoundVault;
        private static IDataVault _blackboxVault;
        private static VaultGenerationHandle<byte> _blackboxBytesHandle;
        private static VaultGenerationHandle<byte> _blackboxDumpScratchHandle;
        private static VaultGenerationHandle<byte> _blackboxDumpHeaderHandle;
        private static VaultGenerationHandle<TelemetryEventDTO> _blackboxEventsHandle;
        private static VaultGenerationHandle<BlackboxSourceSlot> _blackboxSourcesHandle;
        private static VaultGenerationHandle<TelemetryLoggingMaskDTO> _blackboxLoggingMasksHandle;
        private static VaultGenerationHandle<int> _blackboxAtomicStateHandle;
        private static VaultGenerationHandle<int> _blackboxWatchdogCountersHandle;
        private static VaultGenerationHandle<int> _blackboxWatchdogSamplesHandle;
        private static VaultGenerationHandle<int> _blackboxWatchdogStaleProbesHandle;
        private static VaultGenerationHandle<int> _blackboxWatchdogActiveHandle;
        private static int _blackboxActiveFrameCount;
        private static int _blackboxFrameWriteIndex;
        private static int _blackboxValidFrameCount;
        private static int _blackboxTotalFrameWrites;
        private static int _blackboxEventWriteCursor;
        private static int _blackboxSourceCount;
        private static int _blackboxDumpWritten;
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
        private static bool _blackboxVaultGuardHeld;
        private static Thread _blackboxWatchdogThread;
        // COLD ALLOC: object[1] - SHINOBU blackbox native allocation and source registration gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxGate = new object();
        // COLD ALLOC: object[1] - SHINOBU crash dump writer serialization gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxDumpGate = new object();
        // COLD ALLOC: object[1] - SHINOBU watchdog lifecycle gate - owner: GlobalTelemetryBus
        private static readonly object _blackboxWatchdogGate = new object();
        // Scoped bind/init critical-section mask ONLY - never session-held.
        // Watchdog IDs 632-635 => guard bits 56-59 collide with InputOwnerMutationGuardMask
        // (70520-70523 / 75000-75001). Session hold => Input publishOk=0 (L07).
        private static readonly ulong BlackboxVaultMutationGuardMask =
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashBlackboxBytes) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashMmfScratch) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashDumpHeader) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashTelemetryEvents) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashSourceSlots) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashLoggingMasks) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashAtomicState) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashWatchdogCounters) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashWatchdogSamples) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashWatchdogStaleProbes) |
            BlackboxVaultMutationGuardBit(BufferID.ShinobuCrashWatchdogActive);

        public static bool IsCatastrophicFailure => ReadBlackboxAtomic(0) != 0;

        public static int BlackboxActiveFrameCount => Volatile.Read(ref _blackboxActiveFrameCount);

        public static int BlackboxValidFrameCount => Volatile.Read(ref _blackboxValidFrameCount);

        internal static void BindBlackboxDataVaultCold(IDataVault vault)
        {
            lock (_blackboxGate)
            {
                if (ReferenceEquals(_blackboxBoundVault, vault))
                    return;

                DisposeBlackboxArraysNoLock();
                _blackboxBoundVault = vault;
            }
        }

        private static bool IsBlackboxBufferBound()
        {
            return _blackboxVault != null &&
                   _blackboxBytesHandle.BufferID != 0u &&
                   _blackboxBytesHandle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryOpenBlackboxBufferForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _blackboxVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryReadBlackboxBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _blackboxVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length > 0;
        }

        /// <summary>
        /// Pushes a 64-byte telemetry event into the atomic unmanaged event ring.
        /// </summary>
        public static void PushEvent(uint eventHash, float scalarValue)
        {
            PushEvent(eventHash, scalarValue, 0u);
        }

        /// <summary>
        /// Pushes a 64-byte telemetry event into the atomic unmanaged event ring.
        /// </summary>
        public static void PushEvent(uint eventHash, float scalarValue, uint entityId)
        {
            if (eventHash == 0u)
                return;

            if (!TryOpenBlackboxBufferForOwner(in _blackboxEventsHandle, out NativeArray<TelemetryEventDTO> events))
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return;

                EnsureBlackboxInitialized();
                if (!TryOpenBlackboxBufferForOwner(in _blackboxEventsHandle, out events))
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
                if (events.Length > 0)
                    events[slot] = telemetryEvent;
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

            if (!TryOpenBlackboxBufferForOwner(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot> sources))
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!TryOpenBlackboxBufferForOwner(in _blackboxSourcesHandle, out sources))
                return false;

            lock (_blackboxGate)
            {
                int sourceCapacity = math.min(BlackboxMaxSourceCount, sources.Length);
                int count = math.min(math.max(0, _blackboxSourceCount), sourceCapacity);
                for (int i = 0; i < count; i++)
                {
                    BlackboxSourceSlot existing = sources[i];
                    if (existing.SourceHash != sourceHash)
                        continue;

                    existing.SourcePtr = (byte*)sourcePtr;
                    existing.PayloadBytes = payloadBytes;
                    existing.Flags = flags;
                    sources[i] = existing;
                    slot = i;
                    return true;
                }

                if (count >= sourceCapacity)
                    return false;

                BlackboxSourceSlot sourceSlot = default;
                sourceSlot.SourceHash = sourceHash;
                sourceSlot.SourcePtr = (byte*)sourcePtr;
                sourceSlot.PayloadBytes = payloadBytes;
                sourceSlot.Flags = flags;
                sources[count] = sourceSlot;
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
            if (sourceHash == 0u || !TryOpenBlackboxBufferForOwner(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot> sources))
                return;

            lock (_blackboxGate)
            {
                int sourceCapacity = math.min(BlackboxMaxSourceCount, sources.Length);
                int count = math.min(math.max(0, _blackboxSourceCount), sourceCapacity);
                for (int i = 0; i < count; i++)
                {
                    if (sources[i].SourceHash != sourceHash)
                        continue;

                    int last = count - 1;
                    sources[i] = sources[last];
                    sources[last] = default;
                    _blackboxSourceCount = last;
                    return;
                }
            }
        }

        /// <summary>
        /// Opens or initializes the raw ring view on the owner thread.
        /// </summary>
        internal static unsafe bool OpenOrInitializeBlackboxRingBufferView(out BlackboxRingBufferDTO dto)
        {
            dto = default;
            if (!TryOpenBlackboxBufferForOwner(in _blackboxBytesHandle, out NativeArray<byte> bytes))
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!TryOpenBlackboxBufferForOwner(in _blackboxBytesHandle, out bytes))
                return false;

            PopulateBlackboxRingBufferDto(ref dto, bytes);
            return true;
        }

        private static unsafe void PopulateBlackboxRingBufferDto(ref BlackboxRingBufferDTO dto, NativeArray<byte> bytes)
        {
            dto.Bytes = (byte*)bytes.GetUnsafePtr();
            dto.FrameCapacity = bytes.Length / BlackboxFrameStrideBytes;
            dto.ActiveFrameCount = Volatile.Read(ref _blackboxActiveFrameCount);
            dto.FrameStrideBytes = BlackboxFrameStrideBytes;
            dto.ValidFrameCount = Volatile.Read(ref _blackboxValidFrameCount);
            dto.WriteIndex = Volatile.Read(ref _blackboxFrameWriteIndex);
            dto.TotalWrites = Volatile.Read(ref _blackboxTotalFrameWrites);
            dto.FatalHash = unchecked((uint)ReadBlackboxAtomic(1));
        }

        /// <summary>
        /// Records a blind physics state without depending on the real physics owner.
        /// </summary>
        public static void PushMockPhysicsState(in MockPhysicsState state)
        {
            if (!IsBlackboxBufferBound() && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
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
            if (!IsBlackboxBufferBound() && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
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

#if UNITY_EDITOR
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
#endif

        /// <summary>
        /// Returns the active unmanaged logging mask for a prehashed system key.
        /// </summary>
        public static uint GetActiveLoggingMask(uint systemHash)
        {
            if (systemHash == 0u || !TryReadBlackboxBuffer(in _blackboxLoggingMasksHandle, out NativeArray<TelemetryLoggingMaskDTO>.ReadOnly loggingMasks))
                return 0u;

            for (int i = 0; i < loggingMasks.Length; i++)
            {
                TelemetryLoggingMaskDTO entry = loggingMasks[i];
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
            if (!IsBlackboxBufferBound() && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                return false;

            EnsureBlackboxInitialized();
            SetCatastrophicFailure(fatalHash);
            return TryWriteBlackboxDumpSynchronous(fatalHash == 0u ? BlackboxEmergencyFlushHash : fatalHash);
        }

        public static void SignalBlackboxWatchdog(int lane)
        {
            if ((uint)lane >= BlackboxWatchdogLaneCount)
                return;

            if (!TryOpenBlackboxBufferForOwner(in _blackboxWatchdogCountersHandle, out NativeArray<int> watchdogCounters))
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return;

                EnsureBlackboxInitialized();
            }

            if (!TryOpenBlackboxBufferForOwner(in _blackboxWatchdogCountersHandle, out watchdogCounters) ||
                !TryOpenBlackboxBufferForOwner(in _blackboxWatchdogActiveHandle, out NativeArray<int> watchdogActive))
                return;

            unsafe
            {
                try
                {
                    int* counters = (int*)watchdogCounters.GetUnsafePtr();
                    int* active = (int*)watchdogActive.GetUnsafePtr();
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
            if (IsBlackboxBufferBound())
                return;

            lock (_blackboxGate)
            {
                if (IsBlackboxBufferBound())
                    return;

                DisposeBlackboxArraysNoLock();

                int desiredFrameCount = ResolveBlackboxFrameCount();
                int byteCount = desiredFrameCount * BlackboxFrameStrideBytes;
                if (!TryBindBlackboxVaultBuffersNoLock(desiredFrameCount, byteCount))
                    return;
                ClearBlackboxControlStateNoLock();
                // L08/V0: mutation guard is a short critical-section token, not a lifetime pin.
                // Holding BlackboxVaultMutationGuardMask across the session permanently poisons
                // InputOwnerMutationGuardMask high bits 56-59 (watchdog BufferIDs 632-635 fold to
                // the same (id&63) slots as ShinobuInputCurrentDto/Journal/Button/Block + predicted
                // rings). PublishDeterministicInputState then fails 100% (L07 publishOk=0).
                // Hot-path blackbox writers (PushEvent/SignalWatchdog/Probe) already use atomics on
                // resolved handles and do not require the vault mutation guard held.
                ReleaseBlackboxVaultMutationGuardOnlyNoLock();

                _blackboxActiveFrameCount = desiredFrameCount;
                _blackboxFrameWriteIndex = 0;
                _blackboxValidFrameCount = 0;
                _blackboxTotalFrameWrites = 0;
                _blackboxEventWriteCursor = 0;
                _blackboxSourceCount = 0;
                _blackboxDumpWritten = 0;
                _blackboxAppVersionHash = ComputeContextHash(Application.version);
                StartBlackboxWatchdogThread();
            }
        }

        private static bool TryBindBlackboxVaultBuffersNoLock(int desiredFrameCount, int byteCount)
        {
            IDataVault vault = _blackboxBoundVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            int dumpScratchByteCount = BlackboxDumpHeaderBytes + desiredFrameCount * BlackboxFrameStrideBytes;

            VaultGenerationHandle<byte> bytesHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuCrashBlackboxBytes,
                byteCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<byte> dumpScratchHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuCrashMmfScratch,
                dumpScratchByteCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<byte> dumpHeaderHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuCrashDumpHeader,
                BlackboxDumpHeaderBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<TelemetryEventDTO> eventsHandle = vault.EnsureGenerationHandle<TelemetryEventDTO>(
                BufferID.ShinobuCrashTelemetryEvents,
                BlackboxEventCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<BlackboxSourceSlot> sourcesHandle = vault.EnsureGenerationHandle<BlackboxSourceSlot>(
                BufferID.ShinobuCrashSourceSlots,
                BlackboxMaxSourceCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<TelemetryLoggingMaskDTO> loggingMasksHandle = vault.EnsureGenerationHandle<TelemetryLoggingMaskDTO>(
                BufferID.ShinobuCrashLoggingMasks,
                BlackboxLoggingMaskCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> atomicStateHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuCrashAtomicState,
                2,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> watchdogCountersHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuCrashWatchdogCounters,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> watchdogSamplesHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuCrashWatchdogSamples,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> watchdogStaleProbesHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuCrashWatchdogStaleProbes,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> watchdogActiveHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuCrashWatchdogActive,
                BlackboxWatchdogLaneCount,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);

            NativeArray<byte> bytes = default;
            NativeArray<byte> dumpScratch = default;
            NativeArray<byte> dumpHeader = default;
            NativeArray<TelemetryEventDTO> events = default;
            NativeArray<BlackboxSourceSlot> sources = default;
            NativeArray<TelemetryLoggingMaskDTO> loggingMasks = default;
            NativeArray<int> atomicState = default;
            NativeArray<int> watchdogCounters = default;
            NativeArray<int> watchdogSamples = default;
            NativeArray<int> watchdogStaleProbes = default;
            NativeArray<int> watchdogActive = default;

            bool resolved =
                vault.TryResolveHandle(in bytesHandle, out bytes) &&
                vault.TryResolveHandle(in dumpScratchHandle, out dumpScratch) &&
                vault.TryResolveHandle(in dumpHeaderHandle, out dumpHeader) &&
                vault.TryResolveHandle(in eventsHandle, out events) &&
                vault.TryResolveHandle(in sourcesHandle, out sources) &&
                vault.TryResolveHandle(in loggingMasksHandle, out loggingMasks) &&
                vault.TryResolveHandle(in atomicStateHandle, out atomicState) &&
                vault.TryResolveHandle(in watchdogCountersHandle, out watchdogCounters) &&
                vault.TryResolveHandle(in watchdogSamplesHandle, out watchdogSamples) &&
                vault.TryResolveHandle(in watchdogStaleProbesHandle, out watchdogStaleProbes) &&
                vault.TryResolveHandle(in watchdogActiveHandle, out watchdogActive);

            if (!resolved ||
                !bytes.IsCreated || bytes.Length < byteCount ||
                !dumpScratch.IsCreated || dumpScratch.Length < dumpScratchByteCount ||
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
                TryReleaseBlackboxVaultBufferNoThrow(vault, in bytesHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in dumpScratchHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in dumpHeaderHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in eventsHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in sourcesHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in loggingMasksHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in atomicStateHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in watchdogCountersHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in watchdogSamplesHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in watchdogStaleProbesHandle);
                TryReleaseBlackboxVaultBufferNoThrow(vault, in watchdogActiveHandle);
                return false;
            }

            _blackboxVault = vault;
            _blackboxBytesHandle = bytesHandle;
            _blackboxDumpScratchHandle = dumpScratchHandle;
            _blackboxDumpHeaderHandle = dumpHeaderHandle;
            _blackboxEventsHandle = eventsHandle;
            _blackboxSourcesHandle = sourcesHandle;
            _blackboxLoggingMasksHandle = loggingMasksHandle;
            _blackboxAtomicStateHandle = atomicStateHandle;
            _blackboxWatchdogCountersHandle = watchdogCountersHandle;
            _blackboxWatchdogSamplesHandle = watchdogSamplesHandle;
            _blackboxWatchdogStaleProbesHandle = watchdogStaleProbesHandle;
            _blackboxWatchdogActiveHandle = watchdogActiveHandle;
            _blackboxVaultBacked = true;
            _blackboxVaultGuardHeld = false;

            if (TryLockBlackboxVaultBuffersNoLock(vault))
                return true;

            ReleaseBlackboxVaultBindingsNoLock();
            return false;
        }

        private static unsafe void ClearBlackboxControlStateNoLock()
        {
            if (TryOpenBlackboxBufferForOwner(in _blackboxDumpHeaderHandle, out NativeArray<byte> dumpHeader))
                UnsafeUtility.MemClear(dumpHeader.GetUnsafePtr(), dumpHeader.Length);
            if (TryOpenBlackboxBufferForOwner(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot> sources))
                UnsafeUtility.MemClear(sources.GetUnsafePtr(), sources.Length * UnsafeUtility.SizeOf<BlackboxSourceSlot>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxLoggingMasksHandle, out NativeArray<TelemetryLoggingMaskDTO> loggingMasks))
                UnsafeUtility.MemClear(loggingMasks.GetUnsafePtr(), loggingMasks.Length * UnsafeUtility.SizeOf<TelemetryLoggingMaskDTO>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxAtomicStateHandle, out NativeArray<int> atomicState))
                UnsafeUtility.MemClear(atomicState.GetUnsafePtr(), atomicState.Length * UnsafeUtility.SizeOf<int>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxWatchdogCountersHandle, out NativeArray<int> watchdogCounters))
                UnsafeUtility.MemClear(watchdogCounters.GetUnsafePtr(), watchdogCounters.Length * UnsafeUtility.SizeOf<int>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxWatchdogSamplesHandle, out NativeArray<int> watchdogSamples))
                UnsafeUtility.MemClear(watchdogSamples.GetUnsafePtr(), watchdogSamples.Length * UnsafeUtility.SizeOf<int>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxWatchdogStaleProbesHandle, out NativeArray<int> watchdogStaleProbes))
                UnsafeUtility.MemClear(watchdogStaleProbes.GetUnsafePtr(), watchdogStaleProbes.Length * UnsafeUtility.SizeOf<int>());
            if (TryOpenBlackboxBufferForOwner(in _blackboxWatchdogActiveHandle, out NativeArray<int> watchdogActive))
                UnsafeUtility.MemClear(watchdogActive.GetUnsafePtr(), watchdogActive.Length * UnsafeUtility.SizeOf<int>());
        }

        /// <summary>
        /// Acquires BlackboxVaultMutationGuardMask for a short critical section (bind/clear).
        /// MUST be paired with ReleaseBlackboxVaultMutationGuardOnlyNoLock or
        /// ReleaseBlackboxVaultBindingsNoLock - never leave held for the process lifetime.
        /// Watchdog BufferIDs 632-635 collide with Input high guard bits 56-59.
        /// </summary>
        private static bool TryLockBlackboxVaultBuffersNoLock(IDataVault vault)
        {
            if (_blackboxVaultGuardHeld)
                return true;

            if (vault == null || !vault.TryAcquireMutationGuard(BlackboxVaultMutationGuardMask))
                return false;

            _blackboxVaultGuardHeld = true;
            return true;
        }

        private static void ReleaseBlackboxVaultBindingsNoLock()
        {
            IDataVault vault = _blackboxVault;
            if (vault != null && _blackboxVaultGuardHeld)
                TryReleaseBlackboxVaultGuardNoThrow(vault);

            // DataVault owns the blackbox storage. Assembly/domain reload teardown must only
            // detach handles here; freeing vault blocks from this secondary owner can race the
            // vault arena reset path and crash inside UnsafeUtility.MemClear.
            ClearBlackboxVaultBindingsNoLock();
        }

        /// <summary>
        /// Drops the blackbox vault mutation guard while keeping generation handles bound.
        /// Bind/init acquires the guard only around exclusive buffer setup; runtime writers
        /// do not keep it. See EnsureBlackboxInitialized release after ClearBlackboxControlStateNoLock.
        /// </summary>
        private static void ReleaseBlackboxVaultMutationGuardOnlyNoLock()
        {
            if (!_blackboxVaultGuardHeld)
                return;

            IDataVault vault = _blackboxVault;
            if (vault != null)
                TryReleaseBlackboxVaultGuardNoThrow(vault);
            else
                _blackboxVaultGuardHeld = false;
        }

        private static void TryReleaseBlackboxVaultGuardNoThrow(IDataVault vault)
        {
            try
            {
                if (vault != null)
                    vault.ReleaseMutationGuard(BlackboxVaultMutationGuardMask);
            }
            catch (Exception)
            {
            }
            finally
            {
                // Always drop the local hold bit even if vault release threw: a stuck
                // true here would make TryLock short-circuit forever without vault bits,
                // or leave us believing we still own a mask we do not.
                _blackboxVaultGuardHeld = false;
            }
        }

        private static void TryReleaseBlackboxVaultBufferNoThrow<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || handle.BufferID == 0u || handle.Generation == 0u)
                return;

            try
            {
                vault.ReleaseBuffer(in handle);
            }
            catch (Exception)
            {
            }
        }

        private static void ClearBlackboxVaultBindingsNoLock()
        {
            _blackboxVault = null;
            _blackboxBytesHandle = default;
            _blackboxDumpScratchHandle = default;
            _blackboxDumpHeaderHandle = default;
            _blackboxEventsHandle = default;
            _blackboxSourcesHandle = default;
            _blackboxLoggingMasksHandle = default;
            _blackboxAtomicStateHandle = default;
            _blackboxWatchdogCountersHandle = default;
            _blackboxWatchdogSamplesHandle = default;
            _blackboxWatchdogStaleProbesHandle = default;
            _blackboxWatchdogActiveHandle = default;
            _blackboxVaultBacked = false;
            _blackboxVaultGuardHeld = false;
        }

        private static ulong BlackboxVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        private static void DisposeBlackboxState()
        {
            StopBlackboxWatchdogThread();
            lock (_blackboxGate)
            {
                DisposeBlackboxArraysNoLock();
                _blackboxBoundVault = null;
                _blackboxActiveFrameCount = 0;
                _blackboxFrameWriteIndex = 0;
                _blackboxValidFrameCount = 0;
                _blackboxTotalFrameWrites = 0;
                _blackboxEventWriteCursor = 0;
                _blackboxSourceCount = 0;
                _blackboxDumpWritten = 0;
                _blackboxWatchdogStopRequested = 0;
                _blackboxDeterminismExpectedArmed = 0;
                _blackboxExpectedDeterminismHash64 = 0L;
                _blackboxLastDeterminismHash64 = 0L;
                _blackboxMockPhysicsWritten = 0;
                _blackboxMockOriginWritten = 0;
            }
        }

        private static void CommitBlackboxFrame()
        {
            if (!TryOpenBlackboxBufferForOwner(in _blackboxBytesHandle, out NativeArray<byte> bytes))
            {
                EnsureBlackboxInitialized();
                if (!TryOpenBlackboxBufferForOwner(in _blackboxBytesHandle, out bytes))
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

                byte* basePtr = (byte*)bytes.GetUnsafePtr();
                byte* framePtr = basePtr + (frameSlot * BlackboxFrameStrideBytes);
                uint fatalHash = unchecked((uint)ReadBlackboxAtomic(1));
                TelemetryHeaderDTO header = default;
                header.Timestamp = unchecked((ulong)Stopwatch.GetTimestamp());
                header.FrameNumber = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
            if (!TryReadBlackboxBuffer(in _blackboxEventsHandle, out NativeArray<TelemetryEventDTO>.ReadOnly events))
            {
                for (int i = 0; i < BlackboxHashHistoryCount; i++)
                    hashDestination[i] = 0u;
                return;
            }

            int cursor = Volatile.Read(ref _blackboxEventWriteCursor);
            int available = math.min(math.max(0, cursor), BlackboxHashHistoryCount);
            int start = cursor - available;
            int pad = BlackboxHashHistoryCount - available;
            for (int i = 0; i < pad; i++)
                hashDestination[i] = 0u;
            for (int i = 0; i < available; i++)
            {
                TelemetryEventDTO entry = events[(start + i) & BlackboxEventMask];
                hashDestination[pad + i] = entry.EventHash;
            }
        }

        private static unsafe bool CopyBlackboxSourcePayloads(byte* destination)
        {
            bool nonFinite = false;
            bool hasSources = TryReadBlackboxBuffer(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot>.ReadOnly sources);
            int sourceCapacity = hasSources ? math.min(BlackboxMaxSourceCount, sources.Length) : 0;
            int sourceCount = hasSources ? math.min(math.max(0, _blackboxSourceCount), sourceCapacity) : 0;
            for (int i = 0; i < BlackboxMaxSourceCount; i++)
            {
                byte* target = destination + (i * BlackboxSourceStrideBytes);
                UnsafeUtility.MemClear(target, BlackboxSourceStrideBytes);
                if (i >= sourceCount)
                    continue;

                BlackboxSourceSlot source = sources[i];
                if (source.SourcePtr == null || source.PayloadBytes <= 0)
                    continue;

                int copyBytes = math.min(source.PayloadBytes, BlackboxSourceStrideBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(target, BlackboxSourceStrideBytes, source.SourcePtr, copyBytes))
                    continue;

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
            try
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
            catch (Exception)
            {
                Interlocked.Exchange(ref _blackboxDumpWritten, 0);
            }
        }

        private static bool TryWriteBlackboxDumpFromBackground(uint fatalHash)
        {
#if UNITY_EDITOR
            return false;
#else
            if (!IsBlackboxBufferBound())
                return false;

            return CommitBlackboxDumpInMemory(fatalHash);
#endif
        }

        private static bool TryWriteBlackboxDumpSynchronous(uint fatalHash)
        {
            if (!IsBlackboxBufferBound())
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                    return false;

                EnsureBlackboxInitialized();
            }

            if (!IsBlackboxBufferBound())
                return false;

            return CommitBlackboxDumpInMemory(fatalHash);
        }

        private static bool CommitBlackboxDumpInMemory(uint fatalHash)
        {
            lock (_blackboxDumpGate)
            {
                try
                {
                    if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                        return false;

                    int payloadBytes = WriteBlackboxDumpHeader(fatalHash, DateTime.UtcNow, validFrames, activeFrames);
                    return payloadBytes > BlackboxDumpHeaderBytes &&
                           TryStageAndWriteBlackboxDump(validFrames, activeFrames, writeIndex, payloadBytes);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static unsafe bool TryStageAndWriteBlackboxDump(int validFrames, int activeFrames, int writeIndex, int payloadBytes)
        {
            if (validFrames <= 0 ||
                activeFrames <= 0 ||
                payloadBytes <= BlackboxDumpHeaderBytes ||
                !TryReadBlackboxBuffer(in _blackboxDumpHeaderHandle, out NativeArray<byte>.ReadOnly dumpHeader) ||
                !TryReadBlackboxBuffer(in _blackboxBytesHandle, out NativeArray<byte>.ReadOnly bytes) ||
                !TryOpenBlackboxBufferForOwner(in _blackboxDumpScratchHandle, out NativeArray<byte> dumpScratch) ||
                dumpHeader.Length < BlackboxDumpHeaderBytes ||
                dumpScratch.Length < payloadBytes)
            {
                return false;
            }

            try
            {
                byte* destination = (byte*)dumpScratch.GetUnsafePtr();
                byte* header = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dumpHeader);
                byte* sourceBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, BlackboxDumpHeaderBytes, header, BlackboxDumpHeaderBytes))
                    return false;

                int oldestSlot = validFrames >= activeFrames ? writeIndex : 0;
                for (int i = 0; i < validFrames; i++)
                {
                    int slot = oldestSlot + i;
                    if (slot >= activeFrames)
                        slot -= activeFrames;

                    if (!UnsafeMemoryCopyGuard.SafeCopy(
                        destination + BlackboxDumpHeaderBytes + (i * BlackboxFrameStrideBytes),
                        BlackboxFrameStrideBytes,
                        sourceBase + (slot * BlackboxFrameStrideBytes),
                        BlackboxFrameStrideBytes))
                    {
                        return false;
                    }
                }

                return NativeFaultDumpWriter.TryWriteAll(BlackboxDumpRelativePath, dumpScratch, payloadBytes);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static unsafe int WriteBlackboxDumpHeader(uint fatalHash, DateTime generatedUtc, int validFrames, int activeFrames)
        {
            if (!TryOpenBlackboxBufferForOwner(in _blackboxDumpHeaderHandle, out NativeArray<byte> dumpHeader) ||
                !dumpHeader.IsCreated ||
                dumpHeader.Length < BlackboxDumpHeaderBytes)
                return 0;

            try
            {
                int payloadBytes = validFrames * BlackboxFrameStrideBytes;
                byte* headerPtr = (byte*)dumpHeader.GetUnsafePtr();
                if (headerPtr == null)
                    return 0;

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
                metadata[19] = unchecked((uint)BlackboxDumpSourceDescriptorMetadataIndex);
                metadata[20] = unchecked((uint)BlackboxDumpSourceDescriptorUIntStride);
                metadata[21] = unchecked((uint)BlackboxMaxSourceCount);
                WriteBlackboxDumpSourceDescriptorMetadata(metadata);
                return payloadBytes + BlackboxDumpHeaderBytes;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static unsafe void WriteBlackboxDumpSourceDescriptorMetadata(uint* metadata)
        {
            if (metadata == null ||
                !TryReadBlackboxBuffer(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot>.ReadOnly sources))
            {
                return;
            }

            int descriptorCapacity = math.min(
                math.min(BlackboxMaxSourceCount, sources.Length),
                math.max(
                    0,
                    (BlackboxDumpMetadataUIntCapacity - BlackboxDumpSourceDescriptorMetadataIndex) /
                    BlackboxDumpSourceDescriptorUIntStride));
            int sourceCount = math.min(
                math.max(0, Volatile.Read(ref _blackboxSourceCount)),
                descriptorCapacity);
            for (int i = 0; i < sourceCount; i++)
            {
                BlackboxSourceSlot source = sources[i];
                int cursor = BlackboxDumpSourceDescriptorMetadataIndex + (i * BlackboxDumpSourceDescriptorUIntStride);
                metadata[cursor] = source.SourceHash;
                metadata[cursor + 1] = source.Flags;
                metadata[cursor + 2] = unchecked((uint)source.PayloadBytes);
                metadata[cursor + 3] = unchecked((uint)i);
            }
        }

        private static bool TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex)
        {
            validFrames = 0;
            activeFrames = 0;
            writeIndex = 0;
            if (!TryReadBlackboxBuffer(in _blackboxBytesHandle, out NativeArray<byte>.ReadOnly bytes))
                return false;

            int bufferFrames = bytes.Length / BlackboxFrameStrideBytes;
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

        private static void SetCatastrophicFailure(uint fatalHash)
        {
            if (fatalHash == 0u)
                fatalHash = BlackboxEmergencyFlushHash;

            if (TryOpenBlackboxBufferForOwner(in _blackboxAtomicStateHandle, out NativeArray<int> atomicState) &&
                atomicState.Length >= 2)
            {
                try
                {
                    unsafe
                    {
                        int* state = (int*)atomicState.GetUnsafePtr();
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
            if (!TryReadBlackboxBuffer(in _blackboxAtomicStateHandle, out NativeArray<int>.ReadOnly atomicState) ||
                (uint)index >= atomicState.Length)
                return 0;

            try
            {
                unsafe
                {
                    int* state = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(atomicState);
                    return Volatile.Read(ref state[index]);
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
                    Thread thread = new Thread(RunBlackboxWatchdogThread)
                    {
                        IsBackground = true,
                        Name = BlackboxWatchdogThreadName,
                        Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.Heartbeat)
                    };
                    _blackboxWatchdogThread = thread;
                    thread.Start();
                    return true;
                }
                catch (Exception)
                {
                    Volatile.Write(ref _blackboxWatchdogStopRequested, 1);
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

            bool stopped = TryJoinBlackboxWatchdogThreadNoThrow(thread);

            lock (_blackboxWatchdogGate)
            {
                if (stopped && ReferenceEquals(_blackboxWatchdogThread, thread))
                    _blackboxWatchdogThread = null;
                if (stopped)
                    Volatile.Write(ref _blackboxWatchdogStopRequested, 0);
            }
        }

        private static bool TryJoinBlackboxWatchdogThreadNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;

            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(BlackboxWatchdogStopJoinMilliseconds);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RunBlackboxWatchdogThread()
        {
            try
            {
                while (Volatile.Read(ref _blackboxWatchdogStopRequested) == 0)
                {
                    Thread.Sleep(BlackboxWatchdogProbeMilliseconds);
                    if (Volatile.Read(ref _blackboxWatchdogStopRequested) != 0)
                        return;

                    if (ProbeBlackboxWatchdog())
                    {
                        HandleBlackboxWatchdogFatalStall();
                        return;
                    }
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref _blackboxWatchdogStopRequested, 1);
            }
        }

        private static void HandleBlackboxWatchdogFatalStall()
        {
            try
            {
                SetCatastrophicFailure(BlackboxWatchdogFatalHash);
            }
            catch (Exception)
            {
            }

            try
            {
                TryWriteBlackboxDumpFromBackground(BlackboxWatchdogFatalHash);
            }
            catch (Exception)
            {
            }

#if !UNITY_EDITOR
            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception)
            {
            }
#endif
        }

        private static unsafe bool ProbeBlackboxWatchdog()
        {
            if (!TryOpenBlackboxBufferForOwner(in _blackboxWatchdogCountersHandle, out NativeArray<int> watchdogCounters) ||
                !TryOpenBlackboxBufferForOwner(in _blackboxWatchdogSamplesHandle, out NativeArray<int> watchdogSamples) ||
                !TryOpenBlackboxBufferForOwner(in _blackboxWatchdogStaleProbesHandle, out NativeArray<int> watchdogStaleProbes) ||
                !TryOpenBlackboxBufferForOwner(in _blackboxWatchdogActiveHandle, out NativeArray<int> watchdogActive))
            {
                return false;
            }

            try
            {
                int* counters = (int*)watchdogCounters.GetUnsafePtr();
                int* samples = (int*)watchdogSamples.GetUnsafePtr();
                int* staleProbes = (int*)watchdogStaleProbes.GetUnsafePtr();
                int* active = (int*)watchdogActive.GetUnsafePtr();
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

            ClearBlackboxVaultBindingsNoLock();
        }

        private static int ResolveBlackboxFrameCount()
        {
            return ShinobuBlackboxHighFrameCount;
        }

        private static void SetActiveLoggingMask(uint systemHash, uint mask)
        {
            if (systemHash == 0u)
                return;

            EnsureBlackboxInitialized();
            if (!TryOpenBlackboxBufferForOwner(in _blackboxLoggingMasksHandle, out NativeArray<TelemetryLoggingMaskDTO> loggingMasks))
                return;

            for (int i = 0; i < loggingMasks.Length; i++)
            {
                TelemetryLoggingMaskDTO entry = loggingMasks[i];
                if (entry.SystemHash == systemHash || entry.SystemHash == 0u)
                {
                    entry.SystemHash = systemHash;
                    entry.Mask = mask;
                    entry.Version++;
                    loggingMasks[i] = entry;
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
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct BlackboxEditorFrame
        {
            [FieldOffset(0)] public uint FrameNumber;
            [FieldOffset(4)] public uint FatalHash;
            [FieldOffset(8)] public uint LastEventHash;
            [FieldOffset(12)] public Vector3 ImpactPosition;
            [FieldOffset(24)] public int Slot;
            [FieldOffset(28)] public uint _pad0;
            [FieldOffset(32)] private ulong _pad1;
            [FieldOffset(40)] private ulong _pad2;
            [FieldOffset(48)] private ulong _pad3;
            [FieldOffset(56)] private ulong _pad4;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct BlackboxEditorSourcePayload
        {
            [FieldOffset(0)] public ulong Payload0;
            [FieldOffset(8)] public ulong Payload1;
            [FieldOffset(16)] public ulong Payload2;
            [FieldOffset(24)] public ulong Payload3;
            [FieldOffset(32)] public ulong Payload4;
            [FieldOffset(40)] public ulong Payload5;
            [FieldOffset(48)] public ulong Payload6;
            [FieldOffset(56)] public ulong Payload7;

            public uint Word0 => unchecked((uint)Payload0);
            public uint Word1 => unchecked((uint)(Payload0 >> 32));
            public uint Word2 => unchecked((uint)Payload1);
            public uint Word3 => unchecked((uint)(Payload1 >> 32));
            public uint Word4 => unchecked((uint)Payload2);
            public uint Word5 => unchecked((uint)(Payload2 >> 32));
            public uint Word6 => unchecked((uint)Payload3);
            public uint Word7 => unchecked((uint)(Payload3 >> 32));
            public uint Word8 => unchecked((uint)Payload4);
            public uint Word9 => unchecked((uint)(Payload4 >> 32));
            public uint Word10 => unchecked((uint)Payload5);
            public uint Word11 => unchecked((uint)(Payload5 >> 32));
            public uint Word12 => unchecked((uint)Payload6);
            public uint Word13 => unchecked((uint)(Payload6 >> 32));
            public uint Word14 => unchecked((uint)Payload7);
            public uint Word15 => unchecked((uint)(Payload7 >> 32));
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct BlackboxEditorSourceDescriptor
        {
            [FieldOffset(0)] public uint SourceHash;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public int PayloadBytes;
            [FieldOffset(12)] public int Slot;
            [FieldOffset(16)] private ulong _pad0;
            [FieldOffset(24)] private ulong _pad1;
            [FieldOffset(32)] private ulong _pad2;
            [FieldOffset(40)] private ulong _pad3;
            [FieldOffset(48)] private ulong _pad4;
            [FieldOffset(56)] private ulong _pad5;
        }

        public static unsafe int CopyBlackboxEditorFrames(BlackboxEditorFrame[] destination)
        {
            if (destination == null ||
                destination.Length <= 0 ||
                !TryReadBlackboxBuffer(in _blackboxBytesHandle, out NativeArray<byte>.ReadOnly bytes))
            {
                return 0;
            }

            if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                return 0;

            int copyCount = math.min(validFrames, destination.Length);
            if (copyCount <= 0)
                return 0;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
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

        public static unsafe int CopyNewestBlackboxEditorSourcePayloads(BlackboxEditorSourcePayload[] destination)
        {
            if (destination == null ||
                destination.Length <= 0 ||
                !TryReadBlackboxBuffer(in _blackboxBytesHandle, out NativeArray<byte>.ReadOnly bytes))
            {
                return 0;
            }

            if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))
                return 0;

            int sourceCount = math.min(math.max(0, Volatile.Read(ref _blackboxSourceCount)), BlackboxMaxSourceCount);
            int copyCount = math.min(sourceCount, destination.Length);
            if (copyCount <= 0)
                return 0;

            int newestSlot;
            if (validFrames >= activeFrames)
            {
                newestSlot = writeIndex - 1;
                if (newestSlot < 0)
                    newestSlot = activeFrames - 1;
            }
            else
            {
                newestSlot = validFrames - 1;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            byte* framePtr = basePtr + (newestSlot * BlackboxFrameStrideBytes);
            byte* payloadBase = framePtr + BlackboxSourcePayloadOffsetBytes;
            for (int i = 0; i < copyCount; i++)
            {
                destination[i] = UnsafeUtility.ReadArrayElement<BlackboxEditorSourcePayload>(
                    payloadBase + (i * BlackboxSourceStrideBytes),
                    0);
            }

            return copyCount;
        }

        public static int CopyBlackboxEditorSourceDescriptors(BlackboxEditorSourceDescriptor[] destination)
        {
            if (destination == null ||
                destination.Length <= 0 ||
                !TryReadBlackboxBuffer(in _blackboxSourcesHandle, out NativeArray<BlackboxSourceSlot>.ReadOnly sources))
            {
                return 0;
            }

            int sourceCapacity = math.min(BlackboxMaxSourceCount, sources.Length);
            int sourceCount = math.min(math.max(0, Volatile.Read(ref _blackboxSourceCount)), sourceCapacity);
            int copyCount = math.min(sourceCount, destination.Length);
            for (int i = 0; i < copyCount; i++)
            {
                BlackboxSourceSlot source = sources[i];
                BlackboxEditorSourceDescriptor descriptor = default;
                descriptor.SourceHash = source.SourceHash;
                descriptor.Flags = source.Flags;
                descriptor.PayloadBytes = source.PayloadBytes;
                descriptor.Slot = i;
                destination[i] = descriptor;
            }

            return copyCount;
        }

        public static int CopyBlackboxEditorEvents(TelemetryEventDTO[] destination)
        {
            if (destination == null ||
                destination.Length <= 0 ||
                !TryReadBlackboxBuffer(in _blackboxEventsHandle, out NativeArray<TelemetryEventDTO>.ReadOnly events))
            {
                return 0;
            }

            int cursor = Volatile.Read(ref _blackboxEventWriteCursor);
            int available = math.min(math.max(0, cursor), math.min(destination.Length, BlackboxEventCapacity));
            int start = cursor - available;
            for (int i = 0; i < available; i++)
                destination[i] = events[(start + i) & BlackboxEventMask];

            return available;
        }
#endif
    }

}
