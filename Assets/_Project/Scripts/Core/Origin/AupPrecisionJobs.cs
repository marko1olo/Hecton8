using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
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
    /// Owner-local DataVault lane for AUP precision buffers. Stores handles only; no private NativeArray ownership.
    /// </summary>
    public static class AupPrecisionVault
    {
        public const int DefaultCapacity = 4096;
        public const int MaxCapacity = 262144;
        public const int ToleranceProfileCapacity = 64;
        public const int CsvScratchBytes = 16384;
        public const BufferID TargetAupsBuffer = (BufferID)73200;
        public const BufferID RuntimeStateBuffer = (BufferID)73201;
        public const BufferID LocalOffsetsBuffer = (BufferID)73202;
        public const BufferID ResultFlagsBuffer = (BufferID)73203;
        public const BufferID TelemetryRingBuffer = (BufferID)73204;
        public const BufferID ToleranceProfilesBuffer = (BufferID)73205;
        public const BufferID CsvScratchBuffer = (BufferID)73206;
        public const BufferID MockExtremeAupsBuffer = (BufferID)73207;
        public const BufferID FaultCounterBuffer = (BufferID)73208;
        public const SystemID OwnerSystemId = SystemID.CoreDeterminism;
        private const uint ScheduleGuardLeaseToken = 1u;
        private static readonly ulong ScheduledLocalizationMutationGuardMask =
            AupPrecisionMutationGuardBit(TargetAupsBuffer) |
            AupPrecisionMutationGuardBit(RuntimeStateBuffer) |
            AupPrecisionMutationGuardBit(LocalOffsetsBuffer) |
            AupPrecisionMutationGuardBit(ResultFlagsBuffer) |
            AupPrecisionMutationGuardBit(TelemetryRingBuffer) |
            AupPrecisionMutationGuardBit(FaultCounterBuffer);

        /// <summary>
        /// Opens or acquires owner-local Vault buffers and resolves transient owner-route views.
        /// </summary>
        internal static bool OpenOrAcquireBuffersForOwnerRoute(IDataVault vault, int requestedCapacity, out AupPrecisionVaultViews views)
        {
            views = default;
            if (vault == null)
                return false;

            int capacity = ResolveCapacity(requestedCapacity);
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                if (!TryOpenExistingBuffersForOwnerRoute(vault, capacity, out views))
                    return false;

                return EnsureRuntimeDefaults(vault);
            }

            VaultGenerationHandle<double3> targetAups = vault.EnsureGenerationHandle<double3>(
                TargetAupsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionRuntimeStateDTO> runtimeState = vault.EnsureGenerationHandle<AupPrecisionRuntimeStateDTO>(
                RuntimeStateBuffer,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<float3> localOffsets = vault.EnsureGenerationHandle<float3>(
                LocalOffsetsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<uint> resultFlags = vault.EnsureGenerationHandle<uint>(
                ResultFlagsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionTelemetryEntry> telemetryRing = vault.EnsureGenerationHandle<AupPrecisionTelemetryEntry>(
                TelemetryRingBuffer,
                AupPrecisionMath.TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<AupToleranceProfileDTO> toleranceProfiles = vault.EnsureGenerationHandle<AupToleranceProfileDTO>(
                ToleranceProfilesBuffer,
                ToleranceProfileCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<byte> csvScratch = vault.EnsureGenerationHandle<byte>(
                CsvScratchBuffer,
                CsvScratchBytes,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<double3> mockExtremeAups = vault.EnsureGenerationHandle<double3>(
                MockExtremeAupsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionFaultCounter64> faultCounters = vault.EnsureGenerationHandle<AupPrecisionFaultCounter64>(
                FaultCounterBuffer,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryResolveHandle(in targetAups, out views.TargetAups) ||
                !vault.TryResolveHandle(in runtimeState, out views.RuntimeState) ||
                !vault.TryResolveHandle(in localOffsets, out views.LocalOffsets) ||
                !vault.TryResolveHandle(in resultFlags, out views.ResultFlags) ||
                !vault.TryResolveHandle(in telemetryRing, out views.TelemetryRing) ||
                !vault.TryResolveHandle(in toleranceProfiles, out views.ToleranceProfiles) ||
                !vault.TryResolveHandle(in csvScratch, out views.CsvScratch) ||
                !vault.TryResolveHandle(in mockExtremeAups, out views.MockExtremeAups) ||
                !vault.TryResolveHandle(in faultCounters, out views.FaultCounters))
            {
                views = default;
                return false;
            }

            if (!views.IsValidForCapacity(capacity))
            {
                views = default;
                return false;
            }

            return EnsureRuntimeDefaults(vault);
        }

        /// <summary>
        /// Legacy fail-closed overload. Use the lease overload so DataVault pins survive until the returned handle completes.
        /// </summary>
        [Obsolete("Use TryScheduleLocalization(..., out JobHandle handle, out AupPrecisionScheduleLease lease) and release the lease after the handle completes.", false)]
        public static bool TryScheduleLocalization(
            IDataVault vault,
            int activeCount,
            double3 observerAup,
            float globalQualityWeight,
            uint frame,
            JobHandle dependency,
            out JobHandle handle)
        {
            handle = dependency;
            return false;
        }

        /// <summary>
        /// Schedules localization and telemetry folding without completing on the caller thread.
        /// </summary>
        public static bool TryScheduleLocalization(
            IDataVault vault,
            int activeCount,
            double3 observerAup,
            float globalQualityWeight,
            uint frame,
            JobHandle dependency,
            out JobHandle handle,
            out AupPrecisionScheduleLease lease)
        {
            handle = dependency;
            lease = default;
            if (activeCount <= 0 ||
                !TryOpenExistingReadOnlyLane(
                    vault,
                    TargetAupsBuffer,
                    1,
                    out NativeArray<double3>.ReadOnly targetAups))
            {
                return false;
            }

            int count = math.clamp(activeCount, 0, targetAups.Length);
            if (count <= 0)
                return false;

            float gate = AupPrecisionMath.ResolveGateDistanceMeters(globalQualityWeight);
            if (!TryAcquireExistingWriteLane(
                    vault,
                    RuntimeStateBuffer,
                    1,
                    out VaultGenerationHandle<AupPrecisionRuntimeStateDTO> runtimeStateHandle,
                    out NativeArray<AupPrecisionRuntimeStateDTO> runtimeState))
            {
                return false;
            }

            AupPrecisionRuntimeStateDTO state = default;
            try
            {
                state = runtimeState[0];
                state.ObserverAup = math.all(math.isfinite(observerAup)) ? observerAup : double3.zero;
                state.Frame = frame;
                state.ActiveCount = count;
                state.GlobalQualityWeight = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
                state.GateDistanceMeters = gate;
                state.MaxLocalCastMeters = math.max(1f, state.MaxLocalCastMeters <= 0f ? AupPrecisionMath.DefaultMaxLocalCastMeters : state.MaxLocalCastMeters);
                state.Flags = AupPrecisionJobs.ResultFlagValid;
                runtimeState[0] = state;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtimeStateHandle, OwnerSystemId);
            }

            if (!TryAcquireScheduledLocalizationGuard(vault, out uint pinMask))
                return false;

            if (!TryOpenExistingBuffersForOwnerRoute(vault, count, out AupPrecisionVaultViews views))
            {
                ReleaseScheduledLocalizationBuffers(vault, pinMask);
                return false;
            }

            bool scheduled = false;
            try
            {
                JobHandle localization = new LocalizeAupCoordinatesJob
                {
                    TargetAups = views.TargetAups,
                    LocalOffsets = views.LocalOffsets,
                    ResultFlags = views.ResultFlags,
                    ObserverAup = state.ObserverAup,
                    GlobalQualityWeight = state.GlobalQualityWeight,
                    GateMinMeters = AupPrecisionMath.DefaultGateMinMeters,
                    GateMaxMeters = AupPrecisionMath.DefaultGateMaxMeters,
                    MaxLocalCastMeters = state.MaxLocalCastMeters,
                    OutOfBoundsSentinel = AupPrecisionMath.CreateOutOfBoundsSentinel()
                }.Schedule(count, 64, dependency);

                handle = new AupPrecisionTelemetryFoldJob
                {
                    TargetAups = views.TargetAups,
                    LocalOffsets = views.LocalOffsets,
                    ResultFlags = views.ResultFlags,
                    RuntimeState = views.RuntimeState,
                    TelemetryRing = views.TelemetryRing,
                    FaultCounters = views.FaultCounters,
                    ActiveCount = count,
                    TelemetryCursor = math.clamp(state.TelemetryCursor, 0, views.TelemetryRing.Length - 1),
                    Frame = frame,
                    GlobalQualityWeight = state.GlobalQualityWeight,
                    GateDistanceMeters = gate,
                    KernelMicrosecondsEstimate = EstimateLocalizationMicroseconds(count, state.GlobalQualityWeight)
                }.Schedule(localization);
                lease = AupPrecisionScheduleLease.FromPinMask(pinMask);
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseScheduledLocalizationBuffers(vault, pinMask);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only parser for `aup_tolerance_profiles.csv` bytes into Vault tuning rows.
        /// </summary>
        public static int LoadToleranceProfilesFromBytes(IDataVault vault, ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length <= 0 ||
                !OpenOrAcquireBuffersForOwnerRoute(vault, DefaultCapacity, out _) ||
                !TryAcquireExistingWriteLane(
                    vault,
                    ToleranceProfilesBuffer,
                    ToleranceProfileCapacity,
                    out VaultGenerationHandle<AupToleranceProfileDTO> toleranceProfilesHandle,
                    out NativeArray<AupToleranceProfileDTO> toleranceProfiles))
            {
                return 0;
            }

            int written = 0;
            try
            {
                int cursor = 0;
                while (cursor < csvBytes.Length && written < toleranceProfiles.Length)
                {
                    int start = cursor;
                    while (cursor < csvBytes.Length && csvBytes[cursor] != (byte)'\n')
                        cursor++;

                    ReadOnlySpan<byte> row = csvBytes.Slice(start, cursor - start);
                    if (AupPrecisionMath.TryParseToleranceProfileRow(row, out AupToleranceProfileDTO profile))
                    {
                        toleranceProfiles[written] = profile;
                        written++;
                    }

                    if (cursor < csvBytes.Length)
                        cursor++;
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in toleranceProfilesHandle, OwnerSystemId);
            }

            return written;
        }
#endif

        /// <summary>
        /// Dumps the Vault telemetry ring when a fault counter indicates non-finite or clamped precision state.
        /// </summary>
        public static bool TryDumpFaultTelemetry(IDataVault vault)
        {
            if (vault == null ||
                !TryOpenExistingReadOnlyLane(
                    vault,
                    TelemetryRingBuffer,
                    AupPrecisionMath.TelemetryCapacity,
                    out NativeArray<AupPrecisionTelemetryEntry>.ReadOnly ring) ||
                !TryOpenExistingReadOnlyLane(
                    vault,
                    RuntimeStateBuffer,
                    1,
                    out NativeArray<AupPrecisionRuntimeStateDTO>.ReadOnly runtimeState))
            {
                return false;
            }

            if (TryOpenExistingReadOnlyLane(
                    vault,
                    FaultCounterBuffer,
                    1,
                    out NativeArray<AupPrecisionFaultCounter64>.ReadOnly counters))
            {
                AupPrecisionFaultCounter64 counter = counters[0];
                if (counter.NonFiniteCount <= 0 && counter.ClampedCount <= 0)
                    return false;
            }

            return AupPrecisionJobs.TryDumpTelemetry(ring, runtimeState[0].TelemetryCursor);
        }

        private static bool TryOpenExistingBuffersForOwnerRoute(IDataVault vault, int capacity, out AupPrecisionVaultViews views)
        {
            views = default;
            if (!TryOpenExistingLane(vault, TargetAupsBuffer, capacity, out views.TargetAups) ||
                !TryOpenExistingLane(vault, RuntimeStateBuffer, 1, out views.RuntimeState) ||
                !TryOpenExistingLane(vault, LocalOffsetsBuffer, capacity, out views.LocalOffsets) ||
                !TryOpenExistingLane(vault, ResultFlagsBuffer, capacity, out views.ResultFlags) ||
                !TryOpenExistingLane(vault, TelemetryRingBuffer, AupPrecisionMath.TelemetryCapacity, out views.TelemetryRing) ||
                !TryOpenExistingLane(vault, ToleranceProfilesBuffer, ToleranceProfileCapacity, out views.ToleranceProfiles) ||
                !TryOpenExistingLane(vault, CsvScratchBuffer, CsvScratchBytes, out views.CsvScratch) ||
                !TryOpenExistingLane(vault, MockExtremeAupsBuffer, capacity, out views.MockExtremeAups) ||
                !TryOpenExistingLane(vault, FaultCounterBuffer, 1, out views.FaultCounters))
            {
                views = default;
                return false;
            }

            return views.IsValidForCapacity(capacity);
        }

        private static bool TryAcquireExistingWriteLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool handedOff = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    handedOff = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                    vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private static bool TryOpenExistingLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenExistingReadOnlyLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryReadOnlyHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireScheduledLocalizationGuard(IDataVault vault, out uint pinMask)
        {
            pinMask = 0u;
            if (vault == null || !vault.TryAcquireMutationGuard(ScheduledLocalizationMutationGuardMask))
                return false;

            pinMask = ScheduleGuardLeaseToken;
            return true;
        }

        internal static void ReleaseScheduledLocalizationBuffers(IDataVault vault, uint pinMask)
        {
            if (vault == null || pinMask == 0u)
                return;

            vault.ReleaseMutationGuard(ScheduledLocalizationMutationGuardMask);
        }

        private static ulong AupPrecisionMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveCapacity(int requestedCapacity)
        {
            int requested = requestedCapacity > 0 ? requestedCapacity : DefaultCapacity;
            return math.clamp(requested, 1, MaxCapacity);
        }

        private static bool EnsureRuntimeDefaults(IDataVault vault)
        {
            if (!TryOpenExistingReadOnlyLane(
                    vault,
                    RuntimeStateBuffer,
                    1,
                    out NativeArray<AupPrecisionRuntimeStateDTO>.ReadOnly currentState))
            {
                return false;
            }

            AupPrecisionRuntimeStateDTO state = currentState[0];
            if (state.Flags != 0u)
                return true;

            if (!TryAcquireExistingWriteLane(
                    vault,
                    RuntimeStateBuffer,
                    1,
                    out VaultGenerationHandle<AupPrecisionRuntimeStateDTO> runtimeStateHandle,
                    out NativeArray<AupPrecisionRuntimeStateDTO> runtimeState))
            {
                return false;
            }

            try
            {
                state = runtimeState[0];
                if (state.Flags != 0u)
                    return true;

                state.GlobalQualityWeight = 1f;
                state.GateDistanceMeters = AupPrecisionMath.ResolveGateDistanceMeters(1f);
                state.MaxLocalCastMeters = AupPrecisionMath.DefaultMaxLocalCastMeters;
                state.Flags = AupPrecisionJobs.ResultFlagValid;
                runtimeState[0] = state;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtimeStateHandle, OwnerSystemId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimateLocalizationMicroseconds(int count, float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return math.max(0f, count * math.lerp(0.018f, 0.036f, q));
        }
    }

    /// <summary>
    /// Value-type release token for DataVault buffers pinned by AUP precision jobs.
    /// </summary>
    public struct AupPrecisionScheduleLease
    {
        private uint _pinMask;

        private AupPrecisionScheduleLease(uint pinMask)
        {
            _pinMask = pinMask;
        }

        public bool IsValid => _pinMask != 0u;

        internal static AupPrecisionScheduleLease FromPinMask(uint pinMask)
        {
            return new AupPrecisionScheduleLease(pinMask);
        }

        public bool Release(IDataVault vault)
        {
            uint pinMask = _pinMask;
            if (pinMask == 0u || vault == null)
                return false;

            _pinMask = 0u;
            AupPrecisionVault.ReleaseScheduledLocalizationBuffers(vault, pinMask);
            return true;
        }
    }

    internal ref struct AupPrecisionVaultViews
    {
        internal NativeArray<double3> TargetAups;
        internal NativeArray<AupPrecisionRuntimeStateDTO> RuntimeState;
        internal NativeArray<float3> LocalOffsets;
        internal NativeArray<uint> ResultFlags;
        internal NativeArray<AupPrecisionTelemetryEntry> TelemetryRing;
        internal NativeArray<AupToleranceProfileDTO> ToleranceProfiles;
        internal NativeArray<byte> CsvScratch;
        internal NativeArray<double3> MockExtremeAups;
        internal NativeArray<AupPrecisionFaultCounter64> FaultCounters;

        internal bool IsValidForCapacity(int capacity)
        {
            return TargetAups.IsCreated &&
                RuntimeState.IsCreated &&
                LocalOffsets.IsCreated &&
                ResultFlags.IsCreated &&
                TelemetryRing.IsCreated &&
                ToleranceProfiles.IsCreated &&
                CsvScratch.IsCreated &&
                MockExtremeAups.IsCreated &&
                FaultCounters.IsCreated &&
                TargetAups.Length >= capacity &&
                RuntimeState.Length >= 1 &&
                LocalOffsets.Length >= capacity &&
                ResultFlags.Length >= capacity &&
                TelemetryRing.Length >= AupPrecisionMath.TelemetryCapacity &&
                ToleranceProfiles.Length >= AupPrecisionVault.ToleranceProfileCapacity &&
                CsvScratch.Length >= AupPrecisionVault.CsvScratchBytes &&
                MockExtremeAups.Length >= capacity &&
                FaultCounters.Length >= 1;
        }
    }

    /// <summary>
    /// Burst jobs and fault-dump helpers for AUP precision localization.
    /// </summary>
    public static unsafe class AupPrecisionJobs
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1403_AUP_PRECISION.bin";
        private const int DumpHeaderBytes = 24;
        private const uint DumpMagic = 0x41555038u;
        private const uint DumpVersion = 1u;
        public const uint ResultFlagValid = 1u << 0;
        public const uint ResultFlagSkippedByGate = 1u << 1;
        public const uint ResultFlagNonFinite = 1u << 2;
        public const uint ResultFlagClamped = 1u << 3;

        /// <summary>
        /// Writes the fixed telemetry ring for AUP precision faults through the native fault dump route.
        /// </summary>
        public static bool TryDumpTelemetry(NativeArray<AupPrecisionTelemetryEntry>.ReadOnly ring, int cursor)
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<AupPrecisionTelemetryEntry>();
            if (stride <= 0)
                return false;

            long telemetryBytesLong = (long)ring.Length * stride;
            if (telemetryBytesLong <= 0 || telemetryBytesLong > int.MaxValue)
                return false;

            int safeCursor = cursor < 0 || cursor >= ring.Length ? 0 : cursor;
            _ = ring[safeCursor].Frame;
            for (int i = 0; i < ring.Length; i++)
            {
                int sourceIndex = PositiveModulo(safeCursor + i, ring.Length);
                _ = ring[sourceIndex].PositionHash;
            }

            return true;
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static void WriteTelemetryEntry(byte* destination, ref int cursor, AupPrecisionTelemetryEntry entry)
        {
            WriteDouble(destination, ref cursor, entry.MaxLocalDistanceMeters);
            WriteDouble(destination, ref cursor, entry.MaxLocalDistanceSq);
            WriteUInt32(destination, ref cursor, entry.Frame);
            WriteUInt32(destination, ref cursor, entry.ActiveCount);
            WriteUInt32(destination, ref cursor, entry.SkippedCount);
            WriteUInt32(destination, ref cursor, entry.NonFiniteCount);
            WriteUInt32(destination, ref cursor, entry.SafeNormalizeFallbackCount);
            WriteFloat(destination, ref cursor, entry.GlobalQualityWeight);
            WriteFloat(destination, ref cursor, entry.KernelMicrosecondsEstimate);
            WriteFloat(destination, ref cursor, entry.GateDistanceMeters);
            WriteUInt32(destination, ref cursor, entry.Flags);
            WriteUInt32(destination, ref cursor, entry.SectorHash);
            WriteUInt64(destination, ref cursor, entry.PositionHash);
        }

        private static void WriteFloat(byte* destination, ref int cursor, float value)
        {
            WriteUInt32(destination, ref cursor, math.asuint(value));
        }

        private static void WriteDouble(byte* destination, ref int cursor, double value)
        {
            WriteUInt64(destination, ref cursor, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        private static void WriteUInt32(byte* destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static void WriteUInt64(byte* destination, ref int cursor, ulong value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            destination[cursor + 4] = (byte)(value >> 32);
            destination[cursor + 5] = (byte)(value >> 40);
            destination[cursor + 6] = (byte)(value >> 48);
            destination[cursor + 7] = (byte)(value >> 56);
            cursor += sizeof(ulong);
        }

    }

    /// <summary>
    /// Generates synthetic far-edge AUP samples for precision jitter stress tests.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockExtremeAupJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<double3> OutputAups;
        public double3 PositiveEdgeAup;
        public double3 NegativeEdgeAup;
        public double JitterMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)OutputAups.Length)
                return;

            double selector = (index & 1) == 0 ? 1.0d : -1.0d;
            double3 baseAup = selector > 0.0d ? PositiveEdgeAup : NegativeEdgeAup;
            double jitter = math.max(math.abs(JitterMeters), 0.0001d);
            uint seed = unchecked((uint)(index + 1) * 747796405u + 2891336453u);
            double3 offset = new double3(
                HashToSignedUnit(seed ^ 0x9E3779B9u) * jitter,
                HashToSignedUnit(seed ^ 0xBB67AE85u) * jitter,
                HashToSignedUnit(seed ^ 0x3C6EF372u) * jitter);

            double3 sample = baseAup + offset;
            OutputAups[index] = math.all(math.isfinite(sample)) ? sample : double3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double HashToSignedUnit(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return ((value & 0x00FFFFFFu) * (2.0d / 16777215.0d)) - 1.0d;
        }
    }

    /// <summary>
    /// Localizes absolute AUP samples relative to an observer by subtracting in double precision before float output.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LocalizeAupCoordinatesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<double3> TargetAups;
        [NoAlias] public NativeArray<float3> LocalOffsets;
        [NoAlias] public NativeArray<uint> ResultFlags;
        public double3 ObserverAup;
        public float GlobalQualityWeight;
        public float GateMinMeters;
        public float GateMaxMeters;
        public float MaxLocalCastMeters;
        public float3 OutOfBoundsSentinel;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TargetAups.Length || (uint)index >= (uint)LocalOffsets.Length)
                return;

            double3 target = TargetAups[index];
            uint flags = 0u;
            if (!math.all(math.isfinite(target)) || !math.all(math.isfinite(ObserverAup)))
            {
                LocalOffsets[index] = float3.zero;
                flags |= AupPrecisionJobs.ResultFlagNonFinite;
                WriteFlags(index, flags);
                return;
            }

            double distanceSq = AupPrecisionMath.DistanceSqSafeDouble(target, ObserverAup);
            if (AupPrecisionMath.ShouldSkipByDistanceSq(distanceSq, GlobalQualityWeight, GateMinMeters, GateMaxMeters))
            {
                LocalOffsets[index] = math.all(math.isfinite(OutOfBoundsSentinel))
                    ? OutOfBoundsSentinel
                    : AupPrecisionMath.CreateOutOfBoundsSentinel();
                flags |= AupPrecisionJobs.ResultFlagSkippedByGate;
                WriteFlags(index, flags);
                return;
            }

            double3 localDouble = AupPrecisionMath.LocalDeltaDouble(target, ObserverAup);
            float maxLocal = math.max(1f, math.select(AupPrecisionMath.DefaultMaxLocalCastMeters, MaxLocalCastMeters, math.isfinite(MaxLocalCastMeters)));
            float3 local = AupPrecisionMath.DowncastLocalDeltaClamped(localDouble, maxLocal, float3.zero);
            if (!math.all(math.isfinite(local)))
            {
                LocalOffsets[index] = float3.zero;
                flags |= AupPrecisionJobs.ResultFlagNonFinite;
                WriteFlags(index, flags);
                return;
            }

            if (math.any(math.abs(localDouble) > new double3(maxLocal)))
                flags |= AupPrecisionJobs.ResultFlagClamped;

            LocalOffsets[index] = local;
            flags |= AupPrecisionJobs.ResultFlagValid;
            WriteFlags(index, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFlags(int index, uint flags)
        {
            if (ResultFlags.IsCreated && (uint)index < (uint)ResultFlags.Length)
                ResultFlags[index] = flags;
        }
    }

    /// <summary>
    /// Integrates velocity into a local float accumulator and flushes whole meters into double AUP authority.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct KinematicAupAccumulationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<double3> Aups;
        [NoAlias] public NativeArray<float3> LocalAccumulators;
        [ReadOnly, NoAlias] public NativeArray<float3> Velocities;
        public float SimulationTickDelta;
        public float FlushThresholdMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Aups.Length ||
                (uint)index >= (uint)LocalAccumulators.Length ||
                (uint)index >= (uint)Velocities.Length)
            {
                return;
            }

            double3 aup = Aups[index];
            float3 accumulator = LocalAccumulators[index];
            float3 velocity = Velocities[index];
            float dt = math.clamp(math.select(0f, SimulationTickDelta, math.isfinite(SimulationTickDelta)), 0f, 0.1f);

            if (!math.all(math.isfinite(aup)) || !math.all(math.isfinite(accumulator)) || !math.all(math.isfinite(velocity)))
            {
                Aups[index] = math.all(math.isfinite(aup)) ? aup : double3.zero;
                LocalAccumulators[index] = float3.zero;
                return;
            }

            accumulator += velocity * dt;
            float threshold = math.max(1f, math.select(1f, FlushThresholdMeters, math.isfinite(FlushThresholdMeters)));
            double3 flush = double3.zero;

            if (math.abs(accumulator.x) >= threshold)
                flush.x = math.trunc((double)accumulator.x);
            if (math.abs(accumulator.y) >= threshold)
                flush.y = math.trunc((double)accumulator.y);
            if (math.abs(accumulator.z) >= threshold)
                flush.z = math.trunc((double)accumulator.z);

            if (math.any(flush != double3.zero))
            {
                aup += flush;
                accumulator -= new float3((float)flush.x, (float)flush.y, (float)flush.z);
            }

            Aups[index] = math.all(math.isfinite(aup)) ? aup : double3.zero;
            LocalAccumulators[index] = math.all(math.isfinite(accumulator)) ? accumulator : float3.zero;
        }
    }

    /// <summary>
    /// Folds localization result flags into the fixed-size AUP precision telemetry ring.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AupPrecisionTelemetryFoldJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<double3> TargetAups;
        [ReadOnly, NoAlias] public NativeArray<float3> LocalOffsets;
        [ReadOnly, NoAlias] public NativeArray<uint> ResultFlags;
        [NoAlias] public NativeArray<AupPrecisionRuntimeStateDTO> RuntimeState;
        [NoAlias] public NativeArray<AupPrecisionTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<AupPrecisionFaultCounter64> FaultCounters;
        public int ActiveCount;
        public int TelemetryCursor;
        public uint Frame;
        public float GlobalQualityWeight;
        public float GateDistanceMeters;
        public float KernelMicrosecondsEstimate;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int capacityCount = math.min(LocalOffsets.IsCreated ? LocalOffsets.Length : 0, TargetAups.IsCreated ? TargetAups.Length : 0);
            int count = ActiveCount > 0 ? math.min(ActiveCount, capacityCount) : capacityCount;
            uint active = 0u;
            uint skipped = 0u;
            uint nonFinite = 0u;
            uint clamped = 0u;
            double maxDistanceSq = 0.0d;
            ulong hash = 14695981039346656037UL;

            for (int i = 0; i < count; i++)
            {
                uint flags = ResultFlags.IsCreated && i < ResultFlags.Length ? ResultFlags[i] : AupPrecisionJobs.ResultFlagValid;
                if ((flags & AupPrecisionJobs.ResultFlagSkippedByGate) != 0u)
                {
                    skipped++;
                    continue;
                }
                if ((flags & AupPrecisionJobs.ResultFlagNonFinite) != 0u)
                    nonFinite++;
                if ((flags & AupPrecisionJobs.ResultFlagClamped) != 0u)
                    clamped++;
                if ((flags & AupPrecisionJobs.ResultFlagValid) != 0u)
                    active++;

                float3 local = LocalOffsets[i];
                if (!math.all(math.isfinite(local)))
                {
                    nonFinite++;
                    continue;
                }

                double distanceSq = (double)local.x * local.x + (double)local.y * local.y + (double)local.z * local.z;
                if (math.isfinite(distanceSq) && distanceSq > maxDistanceSq)
                    maxDistanceSq = distanceSq;

                hash ^= AupPrecisionMath.HashQuantizedAup(TargetAups[i]);
                hash *= 1099511628211UL;
            }

            int cursor = math.clamp(TelemetryCursor, 0, TelemetryRing.Length - 1);
            double maxDistance = maxDistanceSq > 0.0d ? maxDistanceSq * math.rsqrt(math.max(maxDistanceSq, 0.000001d)) : 0.0d;
            uint aggregateFlags = ResolveAggregateFlags(nonFinite, clamped);
            TelemetryRing[cursor] = new AupPrecisionTelemetryEntry
            {
                MaxLocalDistanceMeters = maxDistance,
                MaxLocalDistanceSq = maxDistanceSq,
                Frame = Frame,
                ActiveCount = active,
                SkippedCount = skipped,
                NonFiniteCount = nonFinite,
                SafeNormalizeFallbackCount = 0u,
                GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight))),
                KernelMicrosecondsEstimate = math.max(0f, math.select(0f, KernelMicrosecondsEstimate, math.isfinite(KernelMicrosecondsEstimate))),
                GateDistanceMeters = math.max(0f, math.select(0f, GateDistanceMeters, math.isfinite(GateDistanceMeters))),
                Flags = aggregateFlags,
                SectorHash = (uint)(hash ^ (hash >> 32)),
                PositionHash = hash
            };

            int nextCursor = cursor + 1;
            if (nextCursor >= TelemetryRing.Length)
                nextCursor = 0;

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                AupPrecisionRuntimeStateDTO state = RuntimeState[0];
                state.TelemetryCursor = nextCursor;
                state.LastKernelMicroseconds = math.max(0f, math.select(0f, KernelMicrosecondsEstimate, math.isfinite(KernelMicrosecondsEstimate)));
                state.Flags = aggregateFlags;
                RuntimeState[0] = state;
            }

            if (FaultCounters.IsCreated && FaultCounters.Length > 0)
            {
                FaultCounters[0] = new AupPrecisionFaultCounter64
                {
                    NonFiniteCount = (int)math.min(nonFinite, 2147483647u),
                    ClampedCount = (int)math.min(clamped, 2147483647u),
                    SkippedCount = (int)math.min(skipped, 2147483647u),
                    SafeNormalizeFallbackCount = 0,
                    MaxErrorMeters = 0f,
                    Flags = aggregateFlags,
                    PositionHash = hash
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveAggregateFlags(uint nonFinite, uint clamped)
        {
            uint flags = 0u;
            if (nonFinite > 0u)
                flags |= AupPrecisionJobs.ResultFlagNonFinite;
            if (clamped > 0u)
                flags |= AupPrecisionJobs.ResultFlagClamped;
            return flags == 0u ? AupPrecisionJobs.ResultFlagValid : flags;
        }
    }
}
