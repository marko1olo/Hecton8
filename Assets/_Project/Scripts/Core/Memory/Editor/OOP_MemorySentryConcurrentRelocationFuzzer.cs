#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Hecton8.Core.Memory.Editor
{
    /// <summary>
    /// Editor-only hostile compaction fuzzer for GlobalDataVault relocation, lock, and arena-growth barriers.
    /// </summary>
    public static class OOP_MemorySentryConcurrentRelocationFuzzer
    {
        private const string ReportPath = "Docs/Reports/VAULT_COMPACTION_STRESS_REPORT_1412.json";
        private const string ReportSidecarPath = ReportPath + ".sha256";
        private const string DumpPath = "Docs/AgentLogs/Dump_1412.bin";
        private const string SourcePath = "Assets/_Project/Scripts/Core/Memory/Editor/OOP_MemorySentryConcurrentRelocationFuzzer.cs";
        private const int BlackBoxFrameCount = 300;
        private const int FuzzerTelemetryEntrySizeBytes = 64;
        private const int MinimumWorkers = 4;
        private const int MaximumWorkers = 8;
        private const int MinimumSlots = 8;
        private const int MaximumSlots = 18;
        private const int MinimumDurationMilliseconds = 1200;
        private const int MaximumDurationMilliseconds = 5000;
        private const int MinimumOperations = 2000;
        private const int MaximumOperations = 100000;
        private const int MinimumJobIterations = 64;
        private const int MaximumJobIterations = 1024;
        private const int NormalMaxBufferLength = 4096;
        private const int ArenaGrowthTriggerIntLength = 33816576;
        private const int FailureFlagManagedException = 1 << 0;
        private const int FailureFlagIntegrity = 1 << 1;
        private const int FailureFlagLockedSkipMissing = 1 << 2;
        private const int FailureFlagQuarantine = 1 << 3;
        private const int FailureFlagJobPayload = 1 << 4;
        private const int FailureFlagArenaGrowth = 1 << 5;
        private const int FailureFlagTimeout = 1 << 6;
        private const int FailureFlagLockRelease = 1 << 7;
        private const int FailureFlagPinUnlock = 1 << 8;
        private const int FailureFlagGrowthResolverTimeout = 1 << 9;
        private const int FailureFlagCleanupRelease = 1 << 10;
        private const int FailureFlagArenaGrowthSerialized = 1 << 11;
        private const int FailureFlagWriteLockAcquire = 1 << 12;
        private const int FailureFlagPinLockAcquire = 1 << 13;
        private const int FailureFlagResolveActiveSlot = 1 << 14;
        private const byte DataVaultDefragFlagAliasBlocked = 1 << 7;
        private const SystemID Owner = SystemID.CoreDiagnostics;

        private static readonly BufferID[] s_bufferIds =
        {
            BufferID.ShinobuCrashBlackboxBytes,
            BufferID.ShinobuCrashMmfScratch,
            BufferID.ShinobuCrashTelemetryEvents,
            BufferID.ShinobuCrashSourceSlots,
            BufferID.ShinobuCrashLoggingMasks,
            BufferID.ShinobuCrashAtomicState,
            BufferID.ShinobuCrashWatchdogCounters,
            BufferID.ShinobuCrashWatchdogSamples,
            BufferID.ShinobuCrashWatchdogStaleProbes,
            BufferID.ShinobuCrashWatchdogActive,
            BufferID.AcousticEchoPendingTaps,
            BufferID.VaultAupSectorLocal32,
            BufferID.VaultSovereigntyActiveEntityCount,
            BufferID.VaultMemoryProfileCsvScratch,
            BufferID.VaultMemoryAddressShiftRecords,
            BufferID.VaultMemoryAddressShiftCount,
            BufferID.Arm64AlignmentTelemetryRing,
            BufferID.Arm64AlignmentTelemetryCursor
        };

        private delegate bool CompactionSliceInvoker(uint activeBurstLockMask);

        [MenuItem("Hecton8/Memory/Run DataVault Compaction Stress Fuzzer 1412")]
        private static void RunDefaultFromMenu()
        {
            bool passed = RunDefault(out MemorySentryFuzzerResult result);
            if (!passed)
                throw new FatalMemoryCorruptionException("DataVault compaction fuzzer failed. Report: " + ReportPath);

            Debug.Log("DataVault compaction fuzzer completed. Iterations=" + result.TotalOperations + " Report=" + ReportPath);
        }

        [MenuItem("Hecton8/Memory/Run DataVault False Positive Probe 1412")]
        private static void RunFalsePositiveFromMenu()
        {
            bool caught = RunFalsePositiveProbe(out MemorySentryFuzzerResult result);
            if (!caught)
                throw new FatalMemoryCorruptionException("False-positive corruption probe did not trip verification. Report: " + ReportPath);

            Debug.Log("DataVault false-positive corruption probe caught expected corruption. Report=" + ReportPath);
        }

        /// <summary>Runs the default bounded stress profile.</summary>
        public static bool RunDefault(out MemorySentryFuzzerResult result)
        {
            MemorySentryFuzzerConfig config = CreateConfig(ResolveHomeostasisGlobalQualityWeight(0.35f));
            return Run(in config, injectCorruption: false, out result);
        }

        /// <summary>Runs a deliberate corruption probe and returns true only when verification catches it.</summary>
        public static bool RunFalsePositiveProbe(out MemorySentryFuzzerResult result)
        {
            MemorySentryFuzzerConfig config = CreateConfig(0.05f);
            config.DurationMilliseconds = 250;
            config.TargetOperations = 256;
            config.WorkerCount = MinimumWorkers;
            config.SlotCount = MinimumSlots;
            return Run(in config, injectCorruption: true, out result);
        }

        /// <summary>Creates a continuous-quality config. 0.0 is survival, 1.0 is hostile overkill.</summary>
        public static MemorySentryFuzzerConfig CreateConfig(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            float curve = q * q * (3f - (2f * q));
            MemorySentryFuzzerConfig config = default;
            config.GlobalQualityWeight = q;
            config.WorkerCount = math.clamp((int)math.round(math.lerp(MinimumWorkers, MaximumWorkers, curve)), MinimumWorkers, MaximumWorkers);
            config.SlotCount = math.clamp((int)math.round(math.lerp(MinimumSlots, MaximumSlots, curve)), MinimumSlots, MaximumSlots);
            config.DurationMilliseconds = math.clamp((int)math.round(math.lerp(MinimumDurationMilliseconds, MaximumDurationMilliseconds, curve)), MinimumDurationMilliseconds, MaximumDurationMilliseconds);
            config.TargetOperations = math.clamp((int)math.round(math.lerp(MinimumOperations, MaximumOperations, curve)), MinimumOperations, MaximumOperations);
            config.JobInnerIterations = math.clamp((int)math.round(math.lerp(MinimumJobIterations, MaximumJobIterations, curve)), MinimumJobIterations, MaximumJobIterations);
            config.MaxBufferLength = math.clamp((int)math.round(math.lerp(512, NormalMaxBufferLength, curve)), 512, NormalMaxBufferLength);
            config.ArenaGrowthLength = ArenaGrowthTriggerIntLength + (int)math.round(262144f * curve);
            config.CompactionSpinWait = math.clamp((int)math.round(math.lerp(96, 12, curve)), 12, 96);
            return config;
        }

        /// <summary>Runs the stress fuzzer and writes the forensic JSON report.</summary>
        public static bool Run(in MemorySentryFuzzerConfig requestedConfig, bool injectCorruption, out MemorySentryFuzzerResult result)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            result = default;
            MemorySentryFuzzerConfig config = SanitizeConfig(in requestedConfig);
            SlotState[] slots = CreateSlots(config.SlotCount);
            ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
            CancellationTokenSource cts = new CancellationTokenSource();
            GlobalDataVault vault = null;
            FuzzerState state = null;
            NativeArray<FuzzerTelemetryEntry> blackBox = default;
            NativeArray<int> jobFailures = default;
            bool canDisposeNativeState = true;
            try
            {
                vault = CreateIsolatedVault(64, GlobalDataVault.MinimumQualityArenaLimitBytes);
                CompactionSliceInvoker directCompaction = ResolveCompactionInvoker(vault);
                FieldInfo lockedSkipField = ResolveLockedSkipField();
                blackBox = H8Memory.Allocate<FuzzerTelemetryEntry>(
                    BlackBoxFrameCount,
                    Owner,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                jobFailures = H8Memory.Allocate<int>(
                    config.SlotCount,
                    Owner,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);

                state = new FuzzerState(
                    vault,
                    slots,
                    directCompaction,
                    lockedSkipField,
                    blackBox,
                    jobFailures,
                    config,
                    exceptions,
                    cts);

                    if (!blackBox.IsCreated)
                        throw new FatalMemoryCorruptionException("Failed to allocate 300-entry fuzzer blackbox.");
                    if (!jobFailures.IsCreated)
                        throw new FatalMemoryCorruptionException("Failed to allocate fuzzer job failure flags.");

                    long staticScanStart = Stopwatch.GetTimestamp();
                    result.EditorQuarantineVerified = ValidateEditorQuarantine() ? 1 : 0;
                    result.StaticScanMicroseconds = TicksToMicroseconds(Stopwatch.GetTimestamp() - staticScanStart);
                    if (result.EditorQuarantineVerified == 0)
                        result.FailureFlags |= FailureFlagQuarantine;

                    PrepareFragmentedVault(state);
                    RunArenaGrowthProbe(state, ref result);
                    RunChaos(state);
                    if (Volatile.Read(ref state.TasksCompleted) == 0)
                    {
                        result.DataIntegrity = 0;
                        PopulateResultSnapshot(state, stopwatch, ref result);
                        WriteReport(in result, injectCorruption, passed: false);
                        WriteFailureDump(state, in result);
                        return false;
                    }

                    EnsureVerificationPopulation(state);

                    if (injectCorruption)
                        InjectDeterministicCorruption(state);

                    try
                    {
                        VerifyAllActiveSlots(state, ref result);
                        result.DataIntegrity = 1;
                    }
                    catch (FatalMemoryCorruptionException ex)
                    {
                        exceptions.Enqueue(ex);
                        result.DataIntegrity = 0;
                        if (injectCorruption && ex is PatternMismatchMemoryCorruptionException)
                        {
                            result.ExpectedCorruptionCaught = 1;
                        }
                        else
                        {
                            result.FailureFlags |= FailureFlagIntegrity;
                        }
                    }

                    PopulateResultSnapshot(state, stopwatch, ref result);
                    ReleaseActiveSlots(state, recordFailures: true);
                    result.FailureFlags |= Volatile.Read(ref state.FailureFlags);
                    if (result.LockContentionEvidenceCount <= 0 && !injectCorruption)
                        result.FailureFlags |= FailureFlagLockedSkipMissing;

                    bool passed = result.FailureFlags == 0 && result.DataIntegrity == 1 && result.LockContentionEvidenceCount > 0;
                    if (injectCorruption)
                        passed = result.ExpectedCorruptionCaught == 1 && result.FailureFlags == 0;

                    WriteReport(in result, injectCorruption, passed);
                    if (!passed)
                        WriteFailureDump(state, in result);
                    return passed;
                }
            catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                    result.FailureFlags |= FailureFlagManagedException;
                    if (state != null)
                    {
                        PopulateResultSnapshot(state, stopwatch, ref result);
                    }
                    else
                    {
                        result.ManagedExceptionCount = exceptions.Count;
                        result.ElapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                        result.WorkerCount = config.WorkerCount;
                        result.SlotCount = config.SlotCount;
                        result.TargetOperations = config.TargetOperations;
                        result.JobInnerIterations = config.JobInnerIterations;
                        result.GlobalQualityWeightMilli = (int)math.round(config.GlobalQualityWeight * 1000f);
                        result.SourceSha256 = ComputeSelfSha256();
                    }

                    WriteReport(in result, injectCorruption, passed: false);
                    if (state != null)
                        WriteFailureDump(state, in result);
                    return false;
                }
                finally
                {
                    cts.Cancel();
                    canDisposeNativeState = state == null || Volatile.Read(ref state.TasksCompleted) != 0;
                    if (canDisposeNativeState && state != null)
                        ReleaseActiveSlots(state, recordFailures: false);
                    if (canDisposeNativeState && jobFailures.IsCreated)
                        H8Memory.Release(ref jobFailures, Owner);
                    if (canDisposeNativeState && blackBox.IsCreated)
                        H8Memory.Release(ref blackBox, Owner);
                    if (canDisposeNativeState && vault != null)
                        vault.Dispose();
                    if (canDisposeNativeState)
                        cts.Dispose();
                    else if (state != null)
                        QueueDeferredCleanup(state);
                }
        }

        private static MemorySentryFuzzerConfig SanitizeConfig(in MemorySentryFuzzerConfig requested)
        {
            MemorySentryFuzzerConfig config = requested;
            config.GlobalQualityWeight = math.saturate(config.GlobalQualityWeight);
            config.WorkerCount = math.clamp(config.WorkerCount, MinimumWorkers, MaximumWorkers);
            config.SlotCount = math.clamp(config.SlotCount, MinimumSlots, math.min(MaximumSlots, s_bufferIds.Length));
            config.DurationMilliseconds = math.clamp(config.DurationMilliseconds, 100, MaximumDurationMilliseconds);
            config.TargetOperations = math.clamp(config.TargetOperations, 1, MaximumOperations);
            config.JobInnerIterations = math.clamp(config.JobInnerIterations, 1, MaximumJobIterations);
            config.MaxBufferLength = math.clamp(config.MaxBufferLength, 16, NormalMaxBufferLength);
            config.ArenaGrowthLength = math.max(config.ArenaGrowthLength, ArenaGrowthTriggerIntLength);
            config.CompactionSpinWait = math.clamp(config.CompactionSpinWait, 1, 256);
            return config;
        }

        private static SlotState[] CreateSlots(int slotCount)
        {
            SlotState[] slots = new SlotState[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = new SlotState(s_bufferIds[i], i);
            return slots;
        }

        private static void PrepareFragmentedVault(FuzzerState state)
        {
            for (int i = 0; i < state.Slots.Length; i++)
            {
                int length = 64 + ((i * 37) & 255);
                AllocateOrGrowSlot(state, state.Slots[i], length, FuzzerOperation.Allocate);
            }

            for (int i = 0; i < state.Slots.Length; i += 3)
                ReleaseSlot(state, state.Slots[i], FuzzerOperation.Release);

            ForceCompactionPulse(state, FuzzerOperation.Defrag);
        }

        private static void RunArenaGrowthProbe(FuzzerState state, ref MemorySentryFuzzerResult result)
        {
            SlotState growthSlot = state.Slots[0];
            Task resolver = Task.Factory.StartNew(
                () => ResolveLoopDuringGrowth(state),
                state.Cancellation,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            try
            {
                AllocateOrGrowSlot(state, growthSlot, state.Config.ArenaGrowthLength, FuzzerOperation.ArenaGrowth);
                result.ArenaGrowthProbeExecuted = 1;
                ForceCompactionPulse(state, FuzzerOperation.ArenaGrowth);
            }
            finally
            {
                if (!WaitTaskNoThrow(resolver, state.Exceptions))
                    state.RecordFailure(FailureFlagGrowthResolverTimeout, growthSlot.BufferId, FuzzerOperation.ArenaGrowth);
                ReleaseSlot(state, growthSlot, FuzzerOperation.Release);
            }

            long arenaBytes;
            lock (state.StructuralGate)
            {
                arenaBytes = state.Vault.ArenaBytes;
            }

            if (arenaBytes <= 128L * 1024L * 1024L)
                result.FailureFlags |= FailureFlagArenaGrowth;
            result.GrowthResolveAttempts = Interlocked.Read(ref state.GrowthResolveAttempts);
            result.GrowthResolveMisses = Interlocked.Read(ref state.GrowthResolveMisses);
            result.GrowthResolveStructuralGateBlocks = Interlocked.Read(ref state.GrowthResolveStructuralGateBlocks);
            if (result.GrowthResolveStructuralGateBlocks > 0L || result.GrowthResolveAttempts <= 0L)
                result.FailureFlags |= FailureFlagArenaGrowthSerialized;
        }

        private static void ResolveLoopDuringGrowth(FuzzerState state)
        {
            XorShift32 rng = new XorShift32(0x9E3779B9u);
            long stopTicks = Stopwatch.GetTimestamp() + MillisecondsToTicks(250);
            while (Stopwatch.GetTimestamp() < stopTicks && !state.Cancellation.IsCancellationRequested)
            {
                SlotState slot = state.Slots[rng.NextInt(state.Slots.Length)];
                lock (slot.Gate)
                {
                    if (slot.Active != 0)
                    {
                        Interlocked.Increment(ref state.GrowthResolveAttempts);
                        if (!TryResolveSlotForArenaGrowthProbe(state, slot, out _))
                            Interlocked.Increment(ref state.GrowthResolveMisses);
                    }
                }

                if ((rng.NextUInt() & 3u) == 0u)
                    Thread.Yield();
            }
        }

        private static void RunChaos(FuzzerState state)
        {
            Volatile.Write(ref state.TasksCompleted, 0);
            Task[] tasks = new Task[state.Config.WorkerCount + 1];
            state.RunningTasks = tasks;
            for (int i = 0; i < state.Config.WorkerCount; i++)
            {
                int workerIndex = i;
                tasks[i] = Task.Factory.StartNew(
                    () => WorkerLoop(state, workerIndex),
                    state.Cancellation,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            tasks[tasks.Length - 1] = Task.Factory.StartNew(
                () => DefragLoop(state),
                state.Cancellation,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            long endTicks = Stopwatch.GetTimestamp() + MillisecondsToTicks(state.Config.DurationMilliseconds);
            while (!state.Cancellation.IsCancellationRequested && Stopwatch.GetTimestamp() < endTicks)
            {
                if (Interlocked.Read(ref state.TotalOperations) >= state.Config.TargetOperations)
                {
                    state.Cancel();
                    break;
                }

                Thread.Sleep(1);
            }

            state.Cancel();
            bool allFinished = WaitTasksNoThrow(tasks, state.Config.DurationMilliseconds + 5000, state.Exceptions);

            if (!allFinished)
            {
                state.RecordFailure(FailureFlagTimeout, BufferID.Unknown, FuzzerOperation.None);
                int timeoutTaskCount = 0;
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i] != null && !tasks[i].IsCompleted)
                        timeoutTaskCount++;
                }

                if (timeoutTaskCount > 0)
                {
                    Interlocked.Add(ref state.TimeoutTaskCount, timeoutTaskCount);
                }
            }

            Volatile.Write(ref state.TasksCompleted, allFinished ? 1 : 0);
        }

        private static void WorkerLoop(FuzzerState state, int workerIndex)
        {
            XorShift32 rng = new XorShift32((uint)(0xA511E9B3u + (workerIndex * 747796405u)));
            while (!state.Cancellation.IsCancellationRequested)
            {
                long opIndex = Interlocked.Increment(ref state.TotalOperations);
                if (opIndex > state.Config.TargetOperations)
                {
                    state.Cancel();
                    return;
                }

                SlotState slot = state.Slots[rng.NextInt(state.Slots.Length)];
                int choice = rng.NextInt(100);
                try
                {
                    if (choice < 30)
                    {
                        int length = 16 + rng.NextInt(state.Config.MaxBufferLength);
                        AllocateOrGrowSlot(state, slot, length, FuzzerOperation.Allocate);
                    }
                    else if (choice < 60)
                    {
                        WriteLockVerifySlot(state, slot, FuzzerOperation.WriteLock);
                    }
                    else if (choice < 90)
                    {
                        PinAndScheduleJob(state, slot, ref rng);
                    }
                    else
                    {
                        ReleaseSlot(state, slot, FuzzerOperation.Release);
                    }
                }
                catch (Exception ex)
                {
                    state.Exceptions.Enqueue(ex);
                    state.RecordFailure(FailureFlagManagedException, slot.BufferId, FuzzerOperation.ManagedException);
                    state.Cancel();
                    return;
                }

                if ((opIndex & 15L) == 0L)
                    Thread.Yield();
            }
        }

        private static void DefragLoop(FuzzerState state)
        {
            while (!state.Cancellation.IsCancellationRequested)
            {
                ForceCompactionPulse(state, FuzzerOperation.Defrag);
                Thread.SpinWait(state.Config.CompactionSpinWait);
            }
        }

        private static void AllocateOrGrowSlot(FuzzerState state, SlotState slot, int requestedLength, FuzzerOperation operation)
        {
            lock (slot.Gate)
            {
                int length = math.max(1, requestedLength);
                bool wasActive = slot.Active != 0;
                VaultGenerationHandle<int> previousHandle = slot.Handle;
                int previousLength = slot.Length;
                VaultGenerationHandle<int> handle;
                NativeArray<int> buffer;
                lock (state.StructuralGate)
                {
                    handle = state.Vault.EnsureGenerationHandle<int>(
                        slot.BufferId,
                        length,
                        Owner,
                        NativeArrayOptions.UninitializedMemory);

                    if (handle.BufferID == 0u)
                        return;

                    if (!state.Vault.TryAcquireWriteLock(in handle, Owner, out buffer))
                    {
                        state.RecordFailure(FailureFlagWriteLockAcquire, slot.BufferId, operation);
                        state.Cancel();
                        if (!wasActive ||
                            previousLength != length ||
                            previousHandle.Generation != handle.Generation)
                        {
                            bool released = state.Vault.ReleaseBuffer(in handle);
                            if (released)
                                ClearSlot(slot);
                            else
                                state.RecordFailure(FailureFlagCleanupRelease, slot.BufferId, operation);
                        }

                        return;
                    }
                }

                try
                {
                    int patternEpoch = NextPatternEpoch(slot.PatternEpoch);
                    WritePattern(slot.BufferId, patternEpoch, buffer);
                    slot.Handle = handle;
                    slot.Length = length;
                    slot.PatternEpoch = patternEpoch;
                    slot.Bytes = (long)buffer.Length * UnsafeUtility.SizeOf<int>();
                    slot.Active = 1;
                    Interlocked.Increment(ref state.AllocationAttempts);
                    RecordTelemetry(state, slot.BufferId, operation, 0u);
                }
                finally
                {
                    ReleaseWriteLockOrRecord(state, in handle, slot.BufferId, operation);
                }
            }
        }

        private static void WriteLockVerifySlot(FuzzerState state, SlotState slot, FuzzerOperation operation)
        {
            lock (slot.Gate)
            {
                if (slot.Active == 0)
                    return;

                if (!TryAcquireWriteLock(state, slot, out NativeArray<int> buffer))
                {
                    state.RecordFailure(FailureFlagWriteLockAcquire, slot.BufferId, operation);
                    state.Cancel();
                    return;
                }

                try
                {
                    if (!TryVerifyPattern(slot.BufferId, slot.PatternEpoch, buffer, out _, out _, out _))
                    {
                        state.RecordFailure(FailureFlagIntegrity, slot.BufferId, operation);
                        state.Cancel();
                        return;
                    }

                    WritePattern(slot.BufferId, slot.PatternEpoch, buffer);
                    Interlocked.Increment(ref state.WriteLockPasses);
                    RecordTelemetry(state, slot.BufferId, operation, 0u);
                    ForceCompactionPulse(state, operation);
                }
                finally
                {
                    ReleaseWriteLockOrRecord(state, slot, operation);
                }
            }
        }

        private static void PinAndScheduleJob(FuzzerState state, SlotState slot, ref XorShift32 rng)
        {
            lock (slot.Gate)
            {
                if (slot.Active == 0)
                    return;

                if (!TryRefreshSlotHandle(state, slot))
                {
                    state.RecordFailure(FailureFlagResolveActiveSlot, slot.BufferId, FuzzerOperation.PinJob);
                    state.Cancel();
                    return;
                }

                bool lockedBuffer;
                lock (state.StructuralGate)
                {
                    lockedBuffer = state.Vault.TryAcquireMutationGuard(MemorySentryMutationGuardBit(slot.BufferId));
                }

                if (!lockedBuffer)
                {
                    state.RecordFailure(FailureFlagPinLockAcquire, slot.BufferId, FuzzerOperation.PinJob);
                    state.Cancel();
                    return;
                }

                try
                {
                    if (!TryResolveSlot(state, slot, out NativeArray<int> buffer))
                    {
                        state.RecordFailure(FailureFlagResolveActiveSlot, slot.BufferId, FuzzerOperation.PinJob);
                        state.Cancel();
                        return;
                    }

                    int failureIndex = slot.Index;
                    NativeArray<int> jobFailures = state.JobFailures;
                    jobFailures[failureIndex] = 0;

                    ReadWriteStressJob job = new ReadWriteStressJob
                    {
                        Buffer = buffer,
                        Failure = jobFailures,
                        FailureIndex = failureIndex,
                        BufferId = (int)slot.BufferId,
                        PatternEpoch = slot.PatternEpoch,
                        Seed = rng.NextUInt(),
                        InnerIterations = state.Config.JobInnerIterations
                    };

                    JobHandle handle = job.Schedule();
                    Interlocked.Increment(ref state.PinJobPasses);
                    RecordTelemetry(state, slot.BufferId, FuzzerOperation.PinJob, 0u);
                    ForceCompactionPulse(state, FuzzerOperation.PinJob);
                    handle.Complete();
                    handle = default;

                    if (state.JobFailures[failureIndex] != 0)
                    {
                        state.RecordFailure(FailureFlagJobPayload, slot.BufferId, FuzzerOperation.PinJob);
                        state.Cancel();
                        return;
                    }
                }
                finally
                {
                    ReleaseMutationGuard(state, slot);
                }
            }
        }

        private static void ReleaseSlot(FuzzerState state, SlotState slot, FuzzerOperation operation)
        {
            lock (slot.Gate)
            {
                if (slot.Active == 0)
                    return;

                if (!TryRefreshSlotHandle(state, slot))
                {
                    state.RecordFailure(FailureFlagCleanupRelease, slot.BufferId, operation);
                    state.Cancel();
                    return;
                }

                bool released;
                lock (state.StructuralGate)
                {
                    released = state.Vault.ReleaseBuffer(in slot.Handle);
                }

                if (released)
                {
                    ClearSlot(slot);
                    Interlocked.Increment(ref state.ReleaseAttempts);
                    RecordTelemetry(state, slot.BufferId, operation, 0u);
                }
            }
        }

        private static void ForceCompactionPulse(FuzzerState state, FuzzerOperation operation)
        {
            lock (state.CompactionGate)
            {
                lock (state.StructuralGate)
                {
                    Interlocked.Increment(ref state.CompactionPasses);
                    uint activeMask = state.Vault.ActiveBurstLockMask;
                    if (activeMask != 0u)
                        Interlocked.Increment(ref state.MaskedDefragPasses);

                    state.Vault.RequestEditorForceDefragmentation();
                    state.Vault.FrostTickDefrag(0.2f, 0f, MemoryDefragPhase.PreSimulation, activeMask);
                    Interlocked.Increment(ref state.PublicDefragPasses);

                    CompactionSliceInvoker direct = state.DirectCompaction;
                    if (direct != null)
                    {
                        direct(activeMask);
                        Interlocked.Increment(ref state.DirectCompactionPasses);
                    }

                    if ((state.Vault.LastDefragFlags & DataVaultDefragFlagAliasBlocked) != 0)
                        Interlocked.Increment(ref state.AliasBlockedDefragPasses);

                    UpdateMaxLockedSkipCount(state);
                    RecordTelemetry(state, BufferID.Unknown, operation, activeMask);
                }
            }
        }

        private static bool TryAcquireWriteLock(FuzzerState state, SlotState slot, out NativeArray<int> buffer)
        {
            buffer = default;
            lock (state.StructuralGate)
            {
                if (state.Vault.TryAcquireWriteLock(in slot.Handle, Owner, out buffer))
                    return true;

                if (!TryRefreshSlotHandle(state, slot))
                    return false;

                return state.Vault.TryAcquireWriteLock(in slot.Handle, Owner, out buffer);
            }
        }

        private static bool TryResolveSlot(FuzzerState state, SlotState slot, out NativeArray<int> buffer)
        {
            buffer = default;
            lock (state.StructuralGate)
            {
                if (state.Vault.TryReadHandle(in slot.Handle, out buffer))
                    return true;

                if (!TryRefreshSlotHandle(state, slot))
                    return false;

                return state.Vault.TryReadHandle(in slot.Handle, out buffer);
            }
        }

        private static bool TryResolveSlotForArenaGrowthProbe(FuzzerState state, SlotState slot, out NativeArray<int> buffer)
        {
            buffer = default;
            bool entered = false;
            try
            {
                Monitor.TryEnter(state.StructuralGate, 0, ref entered);
                if (!entered)
                {
                    Interlocked.Increment(ref state.GrowthResolveStructuralGateBlocks);
                    return false;
                }

                if (state.Vault.TryReadHandle(in slot.Handle, out buffer))
                    return true;

                if (!TryRefreshSlotHandleUnlocked(state, slot))
                    return false;

                return state.Vault.TryReadHandle(in slot.Handle, out buffer);
            }
            finally
            {
                if (entered)
                    Monitor.Exit(state.StructuralGate);
            }
        }

        private static bool TryRefreshSlotHandle(FuzzerState state, SlotState slot)
        {
            lock (state.StructuralGate)
            {
                return TryRefreshSlotHandleUnlocked(state, slot);
            }
        }

        private static bool TryRefreshSlotHandleUnlocked(FuzzerState state, SlotState slot)
        {
            if (slot.Active == 0)
                return false;

            if (!state.Vault.TryGetGenerationHandle<int>(slot.BufferId, out VaultGenerationHandle<int> refreshed))
                return false;

            if (slot.Handle.Generation != refreshed.Generation)
                Interlocked.Increment(ref state.GenerationRefreshes);

            slot.Handle = refreshed;
            return true;
        }

        private static void VerifyAllActiveSlots(FuzzerState state, ref MemorySentryFuzzerResult result)
        {
            for (int i = 0; i < state.Slots.Length; i++)
            {
                SlotState slot = state.Slots[i];
                lock (slot.Gate)
                {
                    if (slot.Active == 0)
                        continue;

                    if (!TryResolveSlot(state, slot, out NativeArray<int> buffer))
                        throw new FatalMemoryCorruptionException("Failed to resolve active slot " + slot.BufferId);

                    VerifyPatternOrThrow(slot.BufferId, slot.PatternEpoch, buffer);
                    result.VerifiedIntegers += buffer.Length;
                }
            }

            if (result.VerifiedIntegers <= 0L)
                throw new FatalMemoryCorruptionException("Verification phase had zero active deterministic integers.");
        }

        private static void InjectDeterministicCorruption(FuzzerState state)
        {
            if (TryCorruptFirstActiveSlot(state))
                return;

            EnsureVerificationPopulation(state);
            if (TryCorruptFirstActiveSlot(state))
                return;

            throw new FatalMemoryCorruptionException("Unable to create deterministic corruption target.");
        }

        private static bool TryCorruptFirstActiveSlot(FuzzerState state)
        {
            for (int i = 0; i < state.Slots.Length; i++)
            {
                SlotState slot = state.Slots[i];
                lock (slot.Gate)
                {
                    if (slot.Active == 0)
                        continue;

                    if (!TryAcquireWriteLock(state, slot, out NativeArray<int> buffer))
                        continue;

                    try
                    {
                        int index = buffer.Length >> 1;
                        buffer[index] = buffer[index] ^ 0x13579BDF;
                        RecordTelemetry(state, slot.BufferId, FuzzerOperation.InjectCorruption, 0u);
                        return true;
                    }
                    finally
                    {
                        ReleaseWriteLockOrRecord(state, slot, FuzzerOperation.InjectCorruption);
                    }
                }
            }

            return false;
        }

        private static void EnsureVerificationPopulation(FuzzerState state)
        {
            for (int i = 0; i < state.Slots.Length; i++)
            {
                if (Volatile.Read(ref state.Slots[i].Active) != 0)
                    return;
            }

            AllocateOrGrowSlot(state, state.Slots[0], 64, FuzzerOperation.Allocate);
        }

        private static void ReleaseActiveSlots(FuzzerState state, bool recordFailures)
        {
            for (int i = 0; i < state.Slots.Length; i++)
            {
                SlotState slot = state.Slots[i];
                lock (slot.Gate)
                {
                    if (slot.Active == 0)
                        continue;

                    lock (state.StructuralGate)
                    {
                        if (state.Vault.TryGetGenerationHandle<int>(slot.BufferId, out VaultGenerationHandle<int> handle))
                        {
                            bool released = state.Vault.ReleaseBuffer(in handle);
                            if (!released && recordFailures)
                            {
                                state.RecordFailure(FailureFlagCleanupRelease, slot.BufferId, FuzzerOperation.Release);
                                continue;
                            }
                        }
                        else if (recordFailures)
                        {
                            state.RecordFailure(FailureFlagCleanupRelease, slot.BufferId, FuzzerOperation.Release);
                            continue;
                        }
                    }

                    ClearSlot(slot);
                }
            }
        }

        private static void QueueDeferredCleanup(FuzzerState state)
        {
            if (Interlocked.Exchange(ref state.DeferredCleanupQueued, 1) != 0)
                return;

            Task.Factory.StartNew(
                () => DeferredCleanupAfterTimeout(state),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static void DeferredCleanupAfterTimeout(FuzzerState state)
        {
            try
            {
                Task[] tasks = state.RunningTasks;
                bool completed = tasks == null || WaitTasksNoThrow(tasks, Timeout.Infinite, state.Exceptions);
                if (!completed)
                {
                    state.RecordFailure(FailureFlagTimeout, BufferID.Unknown, FuzzerOperation.None);
                    return;
                }

                ReleaseActiveSlots(state, recordFailures: false);
                NativeArray<int> jobFailures = state.JobFailures;
                if (jobFailures.IsCreated)
                    H8Memory.Release(ref jobFailures, Owner);

                NativeArray<FuzzerTelemetryEntry> blackBox = state.BlackBox;
                if (blackBox.IsCreated)
                    H8Memory.Release(ref blackBox, Owner);

                state.Vault.Dispose();
                state.CancellationSource.Dispose();
            }
            catch (Exception ex)
            {
                state.Exceptions.Enqueue(ex);
            }
        }

        private static void PopulateResultSnapshot(FuzzerState state, Stopwatch stopwatch, ref MemorySentryFuzzerResult result)
        {
            result.LockedSkipCount = ReadLockedSkipCount(state);
            if (state.MaxLockedSkipCount > result.LockedSkipCount)
                result.LockedSkipCount = state.MaxLockedSkipCount;

            result.FailureFlags |= Volatile.Read(ref state.FailureFlags);
            result.TotalOperations = Interlocked.Read(ref state.TotalOperations);
            result.AllocationAttempts = Interlocked.Read(ref state.AllocationAttempts);
            result.ReleaseAttempts = Interlocked.Read(ref state.ReleaseAttempts);
            result.WriteLockPasses = Interlocked.Read(ref state.WriteLockPasses);
            result.PinJobPasses = Interlocked.Read(ref state.PinJobPasses);
            result.CompactionPasses = Interlocked.Read(ref state.CompactionPasses);
            result.DirectCompactionPasses = Interlocked.Read(ref state.DirectCompactionPasses);
            result.PublicDefragPasses = Interlocked.Read(ref state.PublicDefragPasses);
            result.MaskedDefragPasses = Interlocked.Read(ref state.MaskedDefragPasses);
            result.AliasBlockedDefragPasses = Interlocked.Read(ref state.AliasBlockedDefragPasses);
            result.GrowthResolveAttempts = Interlocked.Read(ref state.GrowthResolveAttempts);
            result.GrowthResolveMisses = Interlocked.Read(ref state.GrowthResolveMisses);
            result.GrowthResolveStructuralGateBlocks = Interlocked.Read(ref state.GrowthResolveStructuralGateBlocks);
            lock (state.StructuralGate)
            {
                result.CompactionMovedBytes = state.Vault.TotalDefragMovedBytes;
                result.LastRelocationRecordCount = state.Vault.LastRelocationRecordCount;
                result.TotalBytesAllocated = state.Vault.AllocatedBytes;
                result.ArenaBytes = state.Vault.ArenaBytes;
            }

            result.GenerationRefreshes = Interlocked.Read(ref state.GenerationRefreshes);
            result.ManagedExceptionCount = state.Exceptions.Count;
            result.TimeoutTaskCount = Volatile.Read(ref state.TimeoutTaskCount);
            result.LockContentionEvidenceCount = SaturatingLongToInt((long)result.LockedSkipCount + result.AliasBlockedDefragPasses);
            int expectedExceptionAllowance = result.ExpectedCorruptionCaught != 0 ? 1 : 0;
            if (result.ManagedExceptionCount > expectedExceptionAllowance)
                result.FailureFlags |= FailureFlagManagedException;
            result.ElapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            result.WorkerCount = state.Config.WorkerCount;
            result.SlotCount = state.Config.SlotCount;
            result.TargetOperations = state.Config.TargetOperations;
            result.JobInnerIterations = state.Config.JobInnerIterations;
            result.GlobalQualityWeightMilli = (int)math.round(state.Config.GlobalQualityWeight * 1000f);
            result.SourceSha256 = ComputeSelfSha256();
        }

        private static void ReleaseWriteLockOrRecord(FuzzerState state, SlotState slot, FuzzerOperation operation)
        {
            ReleaseWriteLockOrRecord(state, in slot.Handle, slot.BufferId, operation);
        }

        private static void ReleaseWriteLockOrRecord(
            FuzzerState state,
            in VaultGenerationHandle<int> handle,
            BufferID bufferId,
            FuzzerOperation operation)
        {
            lock (state.StructuralGate)
            {
                if (state.Vault.ReleaseWriteLock(in handle, Owner))
                    return;
            }

            state.RecordFailure(FailureFlagLockRelease, bufferId, operation);
        }

        private static void ReleaseMutationGuard(FuzzerState state, SlotState slot)
        {
            lock (state.StructuralGate)
            {
                state.Vault.ReleaseMutationGuard(MemorySentryMutationGuardBit(slot.BufferId));
            }
        }

        private static void ClearSlot(SlotState slot)
        {
            slot.Active = 0;
            slot.Handle = default;
            slot.Length = 0;
            slot.Bytes = 0L;
        }

        private static int NextPatternEpoch(int currentEpoch)
        {
            int next = unchecked(currentEpoch + 1);
            return next == 0 ? 1 : next;
        }

        private static ulong MemorySentryMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static void WritePattern(BufferID bufferId, int patternEpoch, NativeArray<int> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = ComputePattern(bufferId, patternEpoch, i);
        }

        private static bool TryVerifyPattern(
            BufferID bufferId,
            int patternEpoch,
            NativeArray<int> buffer,
            out int mismatchIndex,
            out int expectedValue,
            out int actualValue)
        {
            mismatchIndex = -1;
            expectedValue = 0;
            actualValue = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                int expected = ComputePattern(bufferId, patternEpoch, i);
                int actual = buffer[i];
                if (actual != expected)
                {
                    mismatchIndex = i;
                    expectedValue = expected;
                    actualValue = actual;
                    return false;
                }
            }

            return true;
        }

        private static void VerifyPatternOrThrow(BufferID bufferId, int patternEpoch, NativeArray<int> buffer)
        {
            if (!TryVerifyPattern(bufferId, patternEpoch, buffer, out int index, out int expected, out int actual))
                throw new PatternMismatchMemoryCorruptionException("Pattern mismatch buffer=" + (int)bufferId + " index=" + index + " expected=" + expected + " actual=" + actual);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputePattern(BufferID bufferId, int patternEpoch, int index)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)(int)bufferId) * 16777619u;
                hash = (hash ^ (uint)patternEpoch) * 16777619u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return (int)hash;
            }
        }

        private static int ReadLockedSkipCount(FuzzerState state)
        {
            FieldInfo field = state.LockedSkipField;
            if (field == null)
                return 0;

            lock (state.StructuralGate)
            {
                object value = field.GetValue(state.Vault);
                return value is int count ? count : 0;
            }
        }

        private static void UpdateMaxLockedSkipCount(FuzzerState state)
        {
            int count = ReadLockedSkipCount(state);
            int observed;
            do
            {
                observed = Volatile.Read(ref state.MaxLockedSkipCount);
                if (count <= observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref state.MaxLockedSkipCount, count, observed) != observed);
        }

        private static CompactionSliceInvoker ResolveCompactionInvoker(GlobalDataVault vault)
        {
            try
            {
                MethodInfo method = typeof(GlobalDataVault).GetMethod(
                    "TryRunLiveCompactionSlice",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                    return null;

                return (CompactionSliceInvoker)Delegate.CreateDelegate(typeof(CompactionSliceInvoker), vault, method);
            }
            catch
            {
                return null;
            }
        }

        private static FieldInfo ResolveLockedSkipField()
        {
            return typeof(GlobalDataVault).GetField("_defragLockedSkipCount", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static FieldInfo ResolveLatestCreatedField()
        {
            return typeof(GlobalDataVault).GetField("_latestCreated", BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static GlobalDataVault CreateIsolatedVault(int capacity, long arenaCapacityLimitBytes)
        {
            FieldInfo latestCreatedField = ResolveLatestCreatedField();
            object previousLatest = latestCreatedField != null ? latestCreatedField.GetValue(null) : null;
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity, arenaCapacityLimitBytes);
            RestoreLatestCreated(latestCreatedField, previousLatest, vault);
            return vault;
        }

        private static void RestoreLatestCreated(FieldInfo latestCreatedField, object previousLatest, GlobalDataVault isolatedVault)
        {
            if (latestCreatedField == null)
                return;

            object currentLatest = latestCreatedField.GetValue(null);
            if (ReferenceEquals(currentLatest, isolatedVault))
                latestCreatedField.SetValue(null, previousLatest);
        }

        private static float ResolveHomeostasisGlobalQualityWeight(float fallback)
        {
            Type type = Type.GetType("Hecton8.Core.HomeostasisBrain, Assembly-CSharp");
            if (type == null)
            {
                global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    type = assemblies[i].GetType("Hecton8.Core.HomeostasisBrain", throwOnError: false);
                    if (type != null)
                        break;
                }
            }

            PropertyInfo property = type != null
                ? type.GetProperty("GlobalQualityWeight", BindingFlags.Public | BindingFlags.Static)
                : null;
            if (property == null || property.PropertyType != typeof(float))
                return math.saturate(fallback);

            object value = property.GetValue(null);
            return value is float quality && math.isfinite(quality)
                ? math.saturate(quality)
                : math.saturate(fallback);
        }

        private static bool ValidateEditorQuarantine()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), SourcePath);
            if (!File.Exists(path))
                return false;

            string source = File.ReadAllText(path);
            return source.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal) &&
                   source.TrimEnd().EndsWith("#endif", StringComparison.Ordinal) &&
                   source.IndexOf("namespace Hecton8.Core.Memory.Editor", StringComparison.Ordinal) >= 0;
        }

        private static string ComputeSelfSha256()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), SourcePath);
            return ComputeFileSha256(path);
        }

        private static string ComputeFileSha256(string path)
        {
            if (!File.Exists(path))
                return string.Empty;

            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static void WriteReportSha256Sidecar(string fullPath)
        {
            string hash = ComputeFileSha256(fullPath);
            if (string.IsNullOrEmpty(hash))
                return;

            string sidecarPath = Path.Combine(Directory.GetCurrentDirectory(), ReportSidecarPath);
            File.WriteAllText(sidecarPath, hash + "  " + ReportPath + Environment.NewLine);
        }

        private static void WriteReport(in MemorySentryFuzzerResult result, bool injectedCorruption, bool passed)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            AppendJson(builder, "agent_id", "1412", comma: true);
            AppendJson(builder, "role", "LIVE_DATAVAULT_COMPACTION_STRESS_FUZZER", comma: true);
            AppendJson(builder, "status", "RUNTIME_EXECUTED_BY_UNITY_EDITOR", comma: true);
            AppendJson(builder, "runtime_executed", true, comma: true);
            AppendJson(builder, "fuzzer_compile_proof", true, comma: true);
            AppendJson(builder, "fuzzer_asmdef_project_present", GeneratedAsmdefProjectPresent(), comma: true);
            AppendJson(builder, "fuzzer_source_present_in_generated_csproj", GeneratedCsprojContainsFuzzerSource(), comma: true);
            AppendJson(builder, "fuzzer_compile_gap", "Runtime execution proves Unity editor compiled the fuzzer; generated dotnet csproj proof may still differ.", comma: true);
            AppendJson(builder, "passed", passed, comma: true);
            AppendJson(builder, "injected_corruption_probe", injectedCorruption, comma: true);
            AppendJson(builder, "data_integrity", result.DataIntegrity != 0, comma: true);
            AppendJson(builder, "expected_corruption_caught", result.ExpectedCorruptionCaught != 0, comma: true);
            AppendJson(builder, "failure_flags", result.FailureFlags, comma: true);
            AppendJson(builder, "worker_count", result.WorkerCount, comma: true);
            AppendJson(builder, "slot_count", result.SlotCount, comma: true);
            AppendJson(builder, "target_operations", result.TargetOperations, comma: true);
            AppendJson(builder, "total_operations", result.TotalOperations, comma: true);
            AppendJson(builder, "allocation_attempts", result.AllocationAttempts, comma: true);
            AppendJson(builder, "release_attempts", result.ReleaseAttempts, comma: true);
            AppendJson(builder, "write_lock_passes", result.WriteLockPasses, comma: true);
            AppendJson(builder, "pin_job_passes", result.PinJobPasses, comma: true);
            AppendJson(builder, "compaction_passes", result.CompactionPasses, comma: true);
            AppendJson(builder, "public_defrag_passes", result.PublicDefragPasses, comma: true);
            AppendJson(builder, "direct_compaction_passes", result.DirectCompactionPasses, comma: true);
            AppendJson(builder, "masked_defrag_passes", result.MaskedDefragPasses, comma: true);
            AppendJson(builder, "alias_blocked_defrag_passes", result.AliasBlockedDefragPasses, comma: true);
            AppendJson(builder, "growth_resolve_attempts", result.GrowthResolveAttempts, comma: true);
            AppendJson(builder, "growth_resolve_misses", result.GrowthResolveMisses, comma: true);
            AppendJson(builder, "growth_resolve_structural_gate_blocks", result.GrowthResolveStructuralGateBlocks, comma: true);
            AppendJson(builder, "locked_skip_count", result.LockedSkipCount, comma: true);
            AppendJson(builder, "lock_contention_evidence_count", result.LockContentionEvidenceCount, comma: true);
            AppendJson(builder, "arena_growth_probe_executed", result.ArenaGrowthProbeExecuted != 0, comma: true);
            AppendJson(builder, "arena_bytes", result.ArenaBytes, comma: true);
            AppendJson(builder, "total_bytes_allocated", result.TotalBytesAllocated, comma: true);
            AppendJson(builder, "compaction_moved_bytes", result.CompactionMovedBytes, comma: true);
            AppendJson(builder, "last_relocation_record_count", result.LastRelocationRecordCount, comma: true);
            AppendJson(builder, "verified_integers", result.VerifiedIntegers, comma: true);
            AppendJson(builder, "generation_refreshes", result.GenerationRefreshes, comma: true);
            AppendJson(builder, "managed_exception_count", result.ManagedExceptionCount, comma: true);
            AppendJson(builder, "timeout_task_count", result.TimeoutTaskCount, comma: true);
            AppendJson(builder, "elapsed_microseconds", result.ElapsedMicroseconds, comma: true);
            AppendJson(builder, "static_scan_microseconds", result.StaticScanMicroseconds, comma: true);
            AppendJson(builder, "global_quality_weight_milli", result.GlobalQualityWeightMilli, comma: true);
            AppendJson(builder, "job_inner_iterations", result.JobInnerIterations, comma: true);
            AppendJson(builder, "editor_quarantine_verified", result.EditorQuarantineVerified != 0, comma: true);
            AppendJson(builder, "source_sha256", result.SourceSha256, comma: false);
            builder.AppendLine("}");
            File.WriteAllText(fullPath, builder.ToString());
            WriteReportSha256Sidecar(fullPath);
        }

        private static bool GeneratedAsmdefProjectPresent()
        {
            string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "Hecton8.Core.Memory.Editor.csproj");
            return File.Exists(projectPath);
        }

        private static bool GeneratedCsprojContainsFuzzerSource()
        {
            string root = Directory.GetCurrentDirectory();
            string[] projects = Directory.GetFiles(root, "*.csproj", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < projects.Length; i++)
            {
                string text = File.ReadAllText(projects[i]);
                if (text.IndexOf("OOP_MemorySentryConcurrentRelocationFuzzer.cs", StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(EscapeJson(value)).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteFailureDump(FuzzerState state, in MemorySentryFuzzerResult result)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), DumpPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x323134315A5A5546UL);
                writer.Write(2);
                writer.Write(FuzzerTelemetryEntrySizeBytes);
                writer.Write(BlackBoxFrameCount);
                writer.Write(result.FailureFlags);
                writer.Write(result.TotalOperations);
                writer.Write(result.LockedSkipCount);
                lock (state.TelemetryGate)
                {
                    int capacity = state.BlackBox.IsCreated ? state.BlackBox.Length : 0;
                    int count = state.BlackBoxRecordedCount;
                    if (count > capacity)
                        count = capacity;
                    int cursor = state.BlackBoxCursor;
                    int start = count == capacity ? cursor : 0;
                    writer.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        int index = start + i;
                        if (index >= capacity)
                            index -= capacity;
                        FuzzerTelemetryEntry entry = state.BlackBox[index];
                        writer.Write(entry.TotalOperations);
                        writer.Write(entry.CompactionPasses);
                        writer.Write(entry.Reserved0);
                        writer.Write(entry.Reserved1);
                        writer.Write(entry.Reserved2);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.BufferId);
                        writer.Write(entry.Operation);
                        writer.Write(entry.Flags);
                        writer.Write(entry.ActiveLockMask);
                        writer.Write(0u);
                    }
                }
            }
        }

        private static void RecordTelemetry(FuzzerState state, BufferID bufferId, FuzzerOperation operation, uint activeLockMask)
        {
            if (!state.BlackBox.IsCreated)
                return;

            lock (state.TelemetryGate)
            {
                int cursor = state.BlackBoxCursor;
                if ((uint)cursor >= (uint)state.BlackBox.Length)
                    cursor = 0;

                FuzzerTelemetryEntry entry = default;
                entry.Sequence = unchecked((uint)Interlocked.Increment(ref state.TelemetrySequence));
                entry.BufferId = (int)bufferId;
                entry.Operation = (int)operation;
                entry.ActiveLockMask = activeLockMask;
                entry.TotalOperations = Interlocked.Read(ref state.TotalOperations);
                entry.CompactionPasses = Interlocked.Read(ref state.CompactionPasses);
                entry.Flags = unchecked((uint)Volatile.Read(ref state.FailureFlags));
                NativeArray<FuzzerTelemetryEntry> blackBox = state.BlackBox;
                blackBox[cursor] = entry;
                cursor++;
                if (cursor >= state.BlackBox.Length)
                    cursor = 0;
                state.BlackBoxCursor = cursor;
                if (state.BlackBoxRecordedCount < state.BlackBox.Length)
                    state.BlackBoxRecordedCount++;
            }
        }

        private static bool WaitTaskNoThrow(Task task, ConcurrentQueue<Exception> exceptions)
        {
            try
            {
                return task.Wait(500);
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
                return true;
            }
        }

        private static bool WaitTasksNoThrow(Task[] tasks, int timeoutMilliseconds, ConcurrentQueue<Exception> exceptions)
        {
            if (tasks == null)
                return true;

            bool completed = true;
            bool infinite = timeoutMilliseconds == Timeout.Infinite;
            long deadlineTicks = infinite
                ? 0L
                : Stopwatch.GetTimestamp() + MillisecondsToTicks(math.max(0, timeoutMilliseconds));

            for (int i = 0; i < tasks.Length; i++)
            {
                Task task = tasks[i];
                if (task == null)
                    continue;

                bool faultCaptured = false;
                try
                {
                    int waitMilliseconds = infinite ? Timeout.Infinite : RemainingMilliseconds(deadlineTicks);
                    if (!infinite && waitMilliseconds <= 0)
                    {
                        completed = false;
                        continue;
                    }

                    if (!task.Wait(waitMilliseconds))
                    {
                        completed = false;
                        continue;
                    }
                }
                catch (AggregateException ex)
                {
                    for (int innerIndex = 0; innerIndex < ex.InnerExceptions.Count; innerIndex++)
                        exceptions.Enqueue(ex.InnerExceptions[innerIndex]);
                    faultCaptured = true;
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                    faultCaptured = true;
                }

                if (!task.IsCompleted)
                {
                    completed = false;
                }
                else if (!faultCaptured)
                {
                    CaptureTaskFault(tasks[i], exceptions);
                }
            }

            return completed;
        }

        private static int RemainingMilliseconds(long deadlineTicks)
        {
            long remainingTicks = deadlineTicks - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0L)
                return 0;

            long milliseconds = ((remainingTicks * 1000L) + Stopwatch.Frequency - 1L) / Stopwatch.Frequency;
            return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
        }

        private static void CaptureTaskFault(Task task, ConcurrentQueue<Exception> exceptions)
        {
            if (task == null || !task.IsFaulted || task.Exception == null)
                return;

            for (int i = 0; i < task.Exception.InnerExceptions.Count; i++)
                exceptions.Enqueue(task.Exception.InnerExceptions[i]);
        }

        private static long MillisecondsToTicks(int milliseconds)
        {
            return (Stopwatch.Frequency * (long)milliseconds) / 1000L;
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (ticks * 1000000L) / Stopwatch.Frequency;
        }

        private static int SaturatingLongToInt(long value)
        {
            if (value <= 0L)
                return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ReadWriteStressJob : IJob
        {
            public NativeArray<int> Buffer;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Failure is a fuzzer-owned per-slot flag array. The job writes exactly one int at FailureIndex while the
            // scheduling worker holds slot.Gate and a DataVault pin for the same slot, so Unity's generic alias check
            // cannot express the single-writer slot partition but the harness can.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A TempJob failure flag per pin operation was rejected because it adds a native allocation to the hot
            // fuzzer path. A managed exception or queue from Burst was rejected because Burst jobs cannot own managed
            // references and would invalidate the zero-GC stress signal.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // The invariant is FailureIndex == SlotState.Index, SlotState.Index is unique within the fixed slot table,
            // and slot.Gate serializes all jobs for that slot before the next job can reuse the same FailureIndex.
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> Failure;
            public int FailureIndex;
            public int BufferId;
            public int PatternEpoch;
            public uint Seed;
            public int InnerIterations;

            public void Execute()
            {
                uint accumulator = Seed == 0u ? 0x811C9DC5u : Seed;
                int iterations = math.max(1, InnerIterations);
                for (int pass = 0; pass < iterations; pass++)
                {
                    for (int i = 0; i < Buffer.Length; i++)
                    {
                        int expected = ComputePatternBurst(BufferId, PatternEpoch, i);
                        int actual = Buffer[i];
                        if (actual != expected)
                            Failure[FailureIndex] = 1;
                        accumulator ^= (uint)actual + (uint)(i * 374761393);
                        accumulator *= 16777619u;
                        Buffer[i] = expected;
                    }
                }

                if (accumulator == 0xFFFFFFFFu)
                    Failure[FailureIndex] = 2;
            }

            private static int ComputePatternBurst(int bufferId, int patternEpoch, int index)
            {
                unchecked
                {
                    uint hash = 2166136261u;
                    hash = (hash ^ (uint)bufferId) * 16777619u;
                    hash = (hash ^ (uint)patternEpoch) * 16777619u;
                    hash = (hash ^ (uint)index) * 16777619u;
                    hash ^= hash >> 16;
                    hash *= 0x7FEB352Du;
                    hash ^= hash >> 15;
                    hash *= 0x846CA68Bu;
                    hash ^= hash >> 16;
                    return (int)hash;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct MemorySentryFuzzerConfig
        {
            [FieldOffset(0)]
            public float GlobalQualityWeight;
            [FieldOffset(4)]
            public int WorkerCount;
            [FieldOffset(8)]
            public int SlotCount;
            [FieldOffset(12)]
            public int DurationMilliseconds;
            [FieldOffset(16)]
            public int TargetOperations;
            [FieldOffset(20)]
            public int JobInnerIterations;
            [FieldOffset(24)]
            public int MaxBufferLength;
            [FieldOffset(28)]
            public int ArenaGrowthLength;
            [FieldOffset(32)]
            public int CompactionSpinWait;
            [FieldOffset(36)]
            public int Reserved0;
            [FieldOffset(40)]
            public int Reserved1;
            [FieldOffset(44)]
            public int Reserved2;
            [FieldOffset(48)]
            public int Reserved3;
            [FieldOffset(52)]
            public int Reserved4;
            [FieldOffset(56)]
            public int Reserved5;
            [FieldOffset(60)]
            public int Reserved6;
        }

        public struct MemorySentryFuzzerResult
        {
            public int FailureFlags;
            public int DataIntegrity;
            public int ExpectedCorruptionCaught;
            public int WorkerCount;
            public int SlotCount;
            public int TargetOperations;
            public int JobInnerIterations;
            public int GlobalQualityWeightMilli;
            public long TotalOperations;
            public long AllocationAttempts;
            public long ReleaseAttempts;
            public long WriteLockPasses;
            public long PinJobPasses;
            public long CompactionPasses;
            public long DirectCompactionPasses;
            public long PublicDefragPasses;
            public long MaskedDefragPasses;
            public long AliasBlockedDefragPasses;
            public long GrowthResolveAttempts;
            public long GrowthResolveMisses;
            public long GrowthResolveStructuralGateBlocks;
            public int LockedSkipCount;
            public int LockContentionEvidenceCount;
            public long TotalBytesAllocated;
            public long ArenaBytes;
            public long CompactionMovedBytes;
            public int LastRelocationRecordCount;
            public long VerifiedIntegers;
            public long GenerationRefreshes;
            public int ManagedExceptionCount;
            public int TimeoutTaskCount;
            public int ArenaGrowthProbeExecuted;
            public int EditorQuarantineVerified;
            public long ElapsedMicroseconds;
            public long StaticScanMicroseconds;
            public string SourceSha256;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct FuzzerTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public long TotalOperations;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public long CompactionPasses;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public long Reserved0;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public long Reserved1;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public long Reserved2;
            [System.Runtime.InteropServices.FieldOffset(40)]
            public uint Sequence;
            [System.Runtime.InteropServices.FieldOffset(44)]
            public int BufferId;
            [System.Runtime.InteropServices.FieldOffset(48)]
            public int Operation;
            [System.Runtime.InteropServices.FieldOffset(52)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(56)]
            public uint ActiveLockMask;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad3;
        }

        private enum FuzzerOperation : int
        {
            None = 0,
            Allocate = 1,
            WriteLock = 2,
            PinJob = 3,
            Release = 4,
            Defrag = 5,
            ArenaGrowth = 6,
            InjectCorruption = 7,
            ManagedException = 8
        }

        private sealed class SlotState
        {
            public readonly object Gate = new object();
            public readonly BufferID BufferId;
            public readonly int Index;
            public VaultGenerationHandle<int> Handle;
            public int Length;
            public int PatternEpoch;
            public int Active;
            public long Bytes;

            public SlotState(BufferID bufferId, int index)
            {
                BufferId = bufferId;
                Index = index;
            }
        }

        private sealed class FuzzerState
        {
            public readonly GlobalDataVault Vault;
            public readonly SlotState[] Slots;
            public readonly CompactionSliceInvoker DirectCompaction;
            public readonly FieldInfo LockedSkipField;
            public readonly NativeArray<FuzzerTelemetryEntry> BlackBox;
            public readonly NativeArray<int> JobFailures;
            public readonly MemorySentryFuzzerConfig Config;
            public readonly ConcurrentQueue<Exception> Exceptions;
            public readonly CancellationTokenSource CancellationSource;
            public readonly object StructuralGate = new object();
            public readonly object CompactionGate = new object();
            public readonly object TelemetryGate = new object();
            public long TotalOperations;
            public long AllocationAttempts;
            public long ReleaseAttempts;
            public long WriteLockPasses;
            public long PinJobPasses;
            public long CompactionPasses;
            public long DirectCompactionPasses;
            public long PublicDefragPasses;
            public long MaskedDefragPasses;
            public long AliasBlockedDefragPasses;
            public long GenerationRefreshes;
            public long GrowthResolveAttempts;
            public long GrowthResolveMisses;
            public long GrowthResolveStructuralGateBlocks;
            public Task[] RunningTasks;
            public int TelemetrySequence;
            public int BlackBoxCursor;
            public int BlackBoxRecordedCount;
            public int MaxLockedSkipCount;
            public int FailureFlags;
            public int TasksCompleted = 1;
            public int TimeoutTaskCount;
            public int DeferredCleanupQueued;

            public FuzzerState(
                GlobalDataVault vault,
                SlotState[] slots,
                CompactionSliceInvoker directCompaction,
                FieldInfo lockedSkipField,
                NativeArray<FuzzerTelemetryEntry> blackBox,
                NativeArray<int> jobFailures,
                in MemorySentryFuzzerConfig config,
                ConcurrentQueue<Exception> exceptions,
                CancellationTokenSource cancellationSource)
            {
                Vault = vault;
                Slots = slots;
                DirectCompaction = directCompaction;
                LockedSkipField = lockedSkipField;
                BlackBox = blackBox;
                JobFailures = jobFailures;
                Config = config;
                Exceptions = exceptions;
                CancellationSource = cancellationSource;
            }

            public CancellationToken Cancellation => CancellationSource.Token;

            public void Cancel()
            {
                if (!CancellationSource.IsCancellationRequested)
                    CancellationSource.Cancel();
            }

            public void RecordFailure(int flag, BufferID bufferId, FuzzerOperation operation)
            {
                int observed;
                int next;
                do
                {
                    observed = Volatile.Read(ref FailureFlags);
                    next = observed | flag;
                }
                while (Interlocked.CompareExchange(ref FailureFlags, next, observed) != observed);

                RecordTelemetry(this, bufferId, operation, ReadActiveLockMaskForTelemetry());
            }

            private uint ReadActiveLockMaskForTelemetry()
            {
                bool entered = false;
                try
                {
                    Monitor.TryEnter(StructuralGate, 0, ref entered);
                    if (!entered)
                        return 0u;

                    return Vault.ActiveBurstLockMask;
                }
                finally
                {
                    if (entered)
                        Monitor.Exit(StructuralGate);
                }
            }
        }

        private struct XorShift32
        {
            private uint _state;

            public XorShift32(uint seed)
            {
                _state = seed == 0u ? 0x6D2B79F5u : seed;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint NextUInt()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x == 0u ? 0x6D2B79F5u : x;
                return _state;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int NextInt(int exclusiveMax)
            {
                if (exclusiveMax <= 1)
                    return 0;
                return (int)(NextUInt() % (uint)exclusiveMax);
            }
        }
    }

    public class FatalMemoryCorruptionException : Exception
    {
        public FatalMemoryCorruptionException(string message) : base(message)
        {
        }
    }

    public sealed class PatternMismatchMemoryCorruptionException : FatalMemoryCorruptionException
    {
        public PatternMismatchMemoryCorruptionException(string message) : base(message)
        {
        }
    }
}
#endif
