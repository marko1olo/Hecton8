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

        /// <summary>
        /// Ensures owner-local Vault buffers exist and resolves transient job views.
        /// </summary>
        public static bool EnsureBuffers(IDataVault vault, int requestedCapacity, out AupPrecisionVaultViews views)
        {
            views = default;
            if (vault == null)
                return false;

            int capacity = ResolveCapacity(requestedCapacity);
            if (vault.IsAllocationLocked)
            {
                if (!TryResolveExisting(vault, capacity, out views))
                    return false;

                EnsureRuntimeDefaults(views);
                return true;
            }

            VaultGenerationHandle<double3> targetAups = vault.GetGenerationHandle<double3>(
                TargetAupsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionRuntimeStateDTO> runtimeState = vault.GetGenerationHandle<AupPrecisionRuntimeStateDTO>(
                RuntimeStateBuffer,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<float3> localOffsets = vault.GetGenerationHandle<float3>(
                LocalOffsetsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<uint> resultFlags = vault.GetGenerationHandle<uint>(
                ResultFlagsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionTelemetryEntry> telemetryRing = vault.GetGenerationHandle<AupPrecisionTelemetryEntry>(
                TelemetryRingBuffer,
                AupPrecisionMath.TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<AupToleranceProfileDTO> toleranceProfiles = vault.GetGenerationHandle<AupToleranceProfileDTO>(
                ToleranceProfilesBuffer,
                ToleranceProfileCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<byte> csvScratch = vault.GetGenerationHandle<byte>(
                CsvScratchBuffer,
                CsvScratchBytes,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<double3> mockExtremeAups = vault.GetGenerationHandle<double3>(
                MockExtremeAupsBuffer,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            VaultGenerationHandle<AupPrecisionFaultCounter64> faultCounters = vault.GetGenerationHandle<AupPrecisionFaultCounter64>(
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

            EnsureRuntimeDefaults(views);

            return true;
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
            out JobHandle handle)
        {
            handle = dependency;
            if (!EnsureBuffers(vault, activeCount, out AupPrecisionVaultViews views))
                return false;

            int count = math.clamp(activeCount, 0, views.TargetAups.Length);
            if (count <= 0)
                return false;

            float gate = AupPrecisionMath.ResolveGateDistanceMeters(globalQualityWeight);
            AupPrecisionRuntimeStateDTO state = views.RuntimeState[0];
            state.ObserverAup = math.all(math.isfinite(observerAup)) ? observerAup : double3.zero;
            state.Frame = frame;
            state.ActiveCount = count;
            state.GlobalQualityWeight = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            state.GateDistanceMeters = gate;
            state.MaxLocalCastMeters = math.max(1f, state.MaxLocalCastMeters <= 0f ? AupPrecisionMath.DefaultMaxLocalCastMeters : state.MaxLocalCastMeters);
            state.Flags = AupPrecisionJobs.ResultFlagValid;
            views.RuntimeState[0] = state;

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
            return true;
        }

        /// <summary>
        /// Cold-boot parser for `aup_tolerance_profiles.csv` bytes into Vault tuning rows.
        /// </summary>
        public static int LoadToleranceProfilesFromBytes(IDataVault vault, ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length <= 0 || !EnsureBuffers(vault, DefaultCapacity, out AupPrecisionVaultViews views))
                return 0;

            int written = 0;
            int cursor = 0;
            while (cursor < csvBytes.Length && written < views.ToleranceProfiles.Length)
            {
                int start = cursor;
                while (cursor < csvBytes.Length && csvBytes[cursor] != (byte)'\n')
                    cursor++;

                ReadOnlySpan<byte> row = csvBytes.Slice(start, cursor - start);
                if (AupPrecisionMath.TryParseToleranceProfileRow(row, out AupToleranceProfileDTO profile))
                {
                    views.ToleranceProfiles[written] = profile;
                    written++;
                }

                if (cursor < csvBytes.Length)
                    cursor++;
            }

            return written;
        }

        /// <summary>
        /// Dumps the Vault telemetry ring when a fault counter indicates non-finite or clamped precision state.
        /// </summary>
        public static bool TryDumpFaultTelemetry(IDataVault vault)
        {
            if (vault == null ||
                !vault.TryGetBuffer<AupPrecisionTelemetryEntry>(TelemetryRingBuffer, out NativeArray<AupPrecisionTelemetryEntry> ring) ||
                !ring.IsCreated ||
                !vault.TryGetBuffer<AupPrecisionRuntimeStateDTO>(RuntimeStateBuffer, out NativeArray<AupPrecisionRuntimeStateDTO> runtimeState) ||
                !runtimeState.IsCreated ||
                runtimeState.Length <= 0)
            {
                return false;
            }

            if (vault.TryGetBuffer<AupPrecisionFaultCounter64>(FaultCounterBuffer, out NativeArray<AupPrecisionFaultCounter64> counters) &&
                counters.IsCreated &&
                counters.Length > 0)
            {
                AupPrecisionFaultCounter64 counter = counters[0];
                if (counter.NonFiniteCount <= 0 && counter.ClampedCount <= 0)
                    return false;
            }

            return AupPrecisionJobs.TryDumpTelemetry(ring, runtimeState[0].TelemetryCursor);
        }

        private static bool TryResolveExisting(IDataVault vault, int capacity, out AupPrecisionVaultViews views)
        {
            views = default;
            if (!vault.TryGetGenerationHandle<double3>(TargetAupsBuffer, out VaultGenerationHandle<double3> targetAups) ||
                !vault.TryGetGenerationHandle<AupPrecisionRuntimeStateDTO>(RuntimeStateBuffer, out VaultGenerationHandle<AupPrecisionRuntimeStateDTO> runtimeState) ||
                !vault.TryGetGenerationHandle<float3>(LocalOffsetsBuffer, out VaultGenerationHandle<float3> localOffsets) ||
                !vault.TryGetGenerationHandle<uint>(ResultFlagsBuffer, out VaultGenerationHandle<uint> resultFlags) ||
                !vault.TryGetGenerationHandle<AupPrecisionTelemetryEntry>(TelemetryRingBuffer, out VaultGenerationHandle<AupPrecisionTelemetryEntry> telemetryRing) ||
                !vault.TryGetGenerationHandle<AupToleranceProfileDTO>(ToleranceProfilesBuffer, out VaultGenerationHandle<AupToleranceProfileDTO> toleranceProfiles) ||
                !vault.TryGetGenerationHandle<byte>(CsvScratchBuffer, out VaultGenerationHandle<byte> csvScratch) ||
                !vault.TryGetGenerationHandle<double3>(MockExtremeAupsBuffer, out VaultGenerationHandle<double3> mockExtremeAups) ||
                !vault.TryGetGenerationHandle<AupPrecisionFaultCounter64>(FaultCounterBuffer, out VaultGenerationHandle<AupPrecisionFaultCounter64> faultCounters) ||
                !vault.TryResolveHandle(in targetAups, out views.TargetAups) ||
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

            return views.IsValidForCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveCapacity(int requestedCapacity)
        {
            int requested = requestedCapacity > 0 ? requestedCapacity : DefaultCapacity;
            return math.clamp(requested, 1, MaxCapacity);
        }

        private static void EnsureRuntimeDefaults(AupPrecisionVaultViews views)
        {
            AupPrecisionRuntimeStateDTO state = views.RuntimeState[0];
            if (state.Flags != 0u)
                return;

            state.GlobalQualityWeight = 1f;
            state.GateDistanceMeters = AupPrecisionMath.ResolveGateDistanceMeters(1f);
            state.MaxLocalCastMeters = AupPrecisionMath.DefaultMaxLocalCastMeters;
            state.Flags = AupPrecisionJobs.ResultFlagValid;
            views.RuntimeState[0] = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimateLocalizationMicroseconds(int count, float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return math.max(0f, count * math.lerp(0.018f, 0.036f, q));
        }
    }

    public struct AupPrecisionVaultViews
    {
        public NativeArray<double3> TargetAups;
        public NativeArray<AupPrecisionRuntimeStateDTO> RuntimeState;
        public NativeArray<float3> LocalOffsets;
        public NativeArray<uint> ResultFlags;
        public NativeArray<AupPrecisionTelemetryEntry> TelemetryRing;
        public NativeArray<AupToleranceProfileDTO> ToleranceProfiles;
        public NativeArray<byte> CsvScratch;
        public NativeArray<double3> MockExtremeAups;
        public NativeArray<AupPrecisionFaultCounter64> FaultCounters;

        public bool IsValidForCapacity(int capacity)
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
        public const uint ResultFlagValid = 1u << 0;
        public const uint ResultFlagSkippedByGate = 1u << 1;
        public const uint ResultFlagNonFinite = 1u << 2;
        public const uint ResultFlagClamped = 1u << 3;

        private const ulong DumpMagic = 0x3530325F55504148UL; // HAPU_205 little-endian marker.
        private const uint DumpVersion = 1u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_205.bin";

        /// <summary>
        /// Writes a raw binary telemetry dump for AUP precision faults.
        /// </summary>
        public static bool TryDumpTelemetry(NativeArray<AupPrecisionTelemetryEntry> ring, int cursor)
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int stride = UnsafeUtility.SizeOf<AupPrecisionTelemetryEntry>();
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write((uint)ring.Length);
                writer.Write((uint)stride);
                writer.Write((uint)math.clamp(cursor, 0, ring.Length - 1));

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                byte* bytes = (byte*)ptr;
                int byteCount = ring.Length * stride;
                for (int i = 0; i < byteCount; i++)
                    writer.Write(bytes[i]);
            }

            return true;
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
