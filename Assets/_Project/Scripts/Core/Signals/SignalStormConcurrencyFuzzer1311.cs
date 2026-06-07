#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct SignalStormFuzzerPayload1311
    {
        [FieldOffset(0)] public ulong Hash;
        [FieldOffset(8)] public uint Producer;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint GlobalSequence;
        [FieldOffset(20)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SignalStormFuzzerResult1311
    {
        [FieldOffset(0)] public ulong ResultHash;
        [FieldOffset(8)] public long ElapsedTicks;
        [FieldOffset(16)] public int ProducerCount;
        [FieldOffset(20)] public int WritesPerProducer;
        [FieldOffset(24)] public int ExpectedWrites;
        [FieldOffset(28)] public int AcceptedWrites;
        [FieldOffset(32)] public int DrainedWrites;
        [FieldOffset(36)] public int UniqueWrites;
        [FieldOffset(40)] public int DroppedWrites;
        [FieldOffset(44)] public int DuplicateWrites;
        [FieldOffset(48)] public int CorruptedWrites;
        [FieldOffset(52)] public int MissingWrites;
        [FieldOffset(56)] public uint Status;
        [FieldOffset(60)] public uint Reserved0;
    }

    internal static class SignalStormConcurrencyFuzzer1311
    {
        private const int DefaultProducerCount = 8;
        private const int DefaultWritesPerProducer = 32768;
        private const int ProducerJoinTimeoutMilliseconds = 5000;
        private const int ProducerStopJoinTimeoutMilliseconds = 250;
        private const uint StatusGreen = 0u;
        private const uint StatusRed = 1u;

        [MenuItem("Hecton8/Diagnostics/Run Signal SPSC-MPSC Fuzzer 1311", priority = 1311)]
        public static void RunMenuItem()
        {
            SignalStormFuzzerResult1311 result = Run(DefaultProducerCount, DefaultWritesPerProducer);
            WriteReport(in result);
        }

        public static SignalStormFuzzerResult1311 Run(int producerCount, int writesPerProducer)
        {
            producerCount = math.clamp(producerCount, 1, 64);
            writesPerProducer = math.clamp(writesPerProducer, 1, 262144);
            int expectedWrites = producerCount * writesPerProducer;

            SignalStormFuzzerResult1311 result = default;
            result.ProducerCount = producerCount;
            result.WritesPerProducer = writesPerProducer;
            result.ExpectedWrites = expectedWrites;

            MpscSignalRingBuffer<SignalStormFuzzerPayload1311> ring =
                new MpscSignalRingBuffer<SignalStormFuzzerPayload1311>(
                    expectedWrites + 1,
                    Allocator.Persistent,
                    SystemID.CoreDataVault);
            NativeArray<byte> seen = H8Memory.Allocate<byte>(
                expectedWrites,
                SystemID.CoreDataVault,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            ManualResetEventSlim startGate = new ManualResetEventSlim(false);
            Thread[] threads = new Thread[producerCount];
            ProducerState[] states = new ProducerState[producerCount];
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!ring.IsCreated || !seen.IsCreated)
                {
                    result.DroppedWrites = expectedWrites;
                    result.MissingWrites = expectedWrites;
                    result.Status = StatusRed;
                    result.ResultHash = BuildResultHash(in result);
                    return result;
                }

                MpscSignalRingBuffer<SignalStormFuzzerPayload1311>.ParallelWriter writer = ring.AsParallelWriter();
                for (int i = 0; i < producerCount; i++)
                {
                    states[i] = new ProducerState
                    {
                        Writer = writer,
                        StartGate = startGate,
                        ProducerIndex = i,
                        WritesPerProducer = writesPerProducer,
                    };
                    threads[i] = new Thread(ProducerThread);
                    threads[i].IsBackground = true;
                    threads[i].Start(states[i]);
                }

                SignalStartGateNoThrow(startGate);
                bool producersCompleted = JoinProducerThreadsNoThrow(threads, states);
                bool producersStopped = !HasAliveProducerThread(threads);
                if (!producersStopped)
                {
                    result.MissingWrites = expectedWrites;
                    result.ElapsedTicks = stopwatch.ElapsedTicks;
                    result.Status = StatusRed;
                    result.ResultHash = BuildResultHash(in result);
                    return result;
                }

                int accepted = 0;
                int dropped = 0;
                int faulted = 0;
                for (int i = 0; i < producerCount; i++)
                {
                    accepted += states[i].AcceptedWrites;
                    dropped += states[i].DroppedWrites;
                    faulted += Volatile.Read(ref states[i].Faulted);
                }

                int drained = 0;
                int unique = 0;
                int duplicate = 0;
                int corrupted = 0;
                while (ring.TryDequeue(out SignalStormFuzzerPayload1311 payload))
                {
                    drained++;
                    if (!IsValidPayload(in payload, producerCount, writesPerProducer))
                    {
                        corrupted++;
                        continue;
                    }

                    int globalIndex = (int)payload.GlobalSequence;
                    if (seen[globalIndex] != 0)
                    {
                        duplicate++;
                        continue;
                    }

                    seen[globalIndex] = 1;
                    unique++;
                }

                result.AcceptedWrites = accepted;
                result.DroppedWrites = dropped;
                result.DrainedWrites = drained;
                result.UniqueWrites = unique;
                result.DuplicateWrites = duplicate;
                result.CorruptedWrites = corrupted;
                result.MissingWrites = expectedWrites - unique;
                result.ElapsedTicks = stopwatch.ElapsedTicks;
                result.Status = producersCompleted &&
                                faulted == 0 &&
                                accepted == expectedWrites &&
                                dropped == 0 &&
                                drained == expectedWrites &&
                                unique == expectedWrites &&
                                duplicate == 0 &&
                                corrupted == 0
                    ? StatusGreen
                    : StatusRed;
                result.ResultHash = BuildResultHash(in result);
                return result;
            }
            finally
            {
                stopwatch.Stop();
                RequestProducerStop(states);
                SignalStartGateNoThrow(startGate);
                JoinProducerThreadsAfterStopNoThrow(threads);
                if (!HasAliveProducerThread(threads))
                {
                    startGate.Dispose();
                    if (seen.IsCreated)
                        H8Memory.Release(ref seen, SystemID.CoreDataVault);
                    if (ring.IsCreated)
                        ring.Dispose();
                }
            }
        }

        private static void ProducerThread(object stateObject)
        {
            ProducerState state = (ProducerState)stateObject;
            try
            {
                state.StartGate.Wait();
                for (int i = 0; i < state.WritesPerProducer; i++)
                {
                    if (Volatile.Read(ref state.StopRequested) != 0)
                        break;

                    uint producer = (uint)state.ProducerIndex;
                    uint sequence = (uint)i;
                    uint globalSequence = (uint)((state.ProducerIndex * state.WritesPerProducer) + i);
                    SignalStormFuzzerPayload1311 payload = default;
                    payload.Producer = producer;
                    payload.Sequence = sequence;
                    payload.GlobalSequence = globalSequence;
                    payload.Hash = BuildPayloadHash(producer, sequence, globalSequence);
                    if (state.Writer.TryEnqueue(in payload))
                        state.AcceptedWrites++;
                    else
                        state.DroppedWrites++;
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref state.Faulted, 1);
            }
        }

        private static bool JoinProducerThreadsNoThrow(Thread[] threads, ProducerState[] states)
        {
            bool completed = true;
            long deadline = Stopwatch.GetTimestamp() + ProducerJoinTimeoutMilliseconds * Stopwatch.Frequency / 1000L;
            for (int i = 0; i < threads.Length; i++)
            {
                int timeoutMilliseconds = ResolveRemainingJoinMilliseconds(deadline);
                if (!TryJoinProducerThreadNoThrow(threads[i], timeoutMilliseconds))
                {
                    completed = false;
                    RequestProducerStop(states);
                }
            }

            if (!completed)
                JoinProducerThreadsAfterStopNoThrow(threads);

            return completed && !HasAliveProducerThread(threads);
        }

        private static int ResolveRemainingJoinMilliseconds(long deadline)
        {
            long remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0L)
                return 1;

            long remainingMilliseconds = remainingTicks * 1000L / Stopwatch.Frequency;
            if (remainingMilliseconds <= 0L)
                return 1;
            return remainingMilliseconds > ProducerJoinTimeoutMilliseconds
                ? ProducerJoinTimeoutMilliseconds
                : (int)remainingMilliseconds;
        }

        private static void JoinProducerThreadsAfterStopNoThrow(Thread[] threads)
        {
            for (int i = 0; i < threads.Length; i++)
                TryJoinProducerThreadNoThrow(threads[i], ProducerStopJoinTimeoutMilliseconds);
        }

        private static bool TryJoinProducerThreadNoThrow(Thread thread, int timeoutMilliseconds)
        {
            if (thread == null || !thread.IsAlive)
                return true;

            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(math.max(1, timeoutMilliseconds));
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasAliveProducerThread(Thread[] threads)
        {
            if (threads == null)
                return false;

            for (int i = 0; i < threads.Length; i++)
            {
                Thread thread = threads[i];
                if (thread != null && thread.IsAlive)
                    return true;
            }

            return false;
        }

        private static void RequestProducerStop(ProducerState[] states)
        {
            if (states == null)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                ProducerState state = states[i];
                if (state != null)
                    Volatile.Write(ref state.StopRequested, 1);
            }
        }

        private static bool SignalStartGateNoThrow(ManualResetEventSlim startGate)
        {
            try
            {
                startGate.Set();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsValidPayload(in SignalStormFuzzerPayload1311 payload, int producerCount, int writesPerProducer)
        {
            if (payload.Producer >= producerCount || payload.Sequence >= writesPerProducer)
                return false;
            uint expectedGlobal = (uint)(((int)payload.Producer * writesPerProducer) + (int)payload.Sequence);
            if (payload.GlobalSequence != expectedGlobal)
                return false;
            return payload.Hash == BuildPayloadHash(payload.Producer, payload.Sequence, payload.GlobalSequence);
        }

        private static ulong BuildPayloadHash(uint producer, uint sequence, uint globalSequence)
        {
            ulong hash = 14695981039346656037ul;
            hash = Fold(hash, producer);
            hash = Fold(hash, sequence);
            hash = Fold(hash, globalSequence);
            return hash == 0ul ? 1ul : hash;
        }

        private static ulong BuildResultHash(in SignalStormFuzzerResult1311 result)
        {
            ulong hash = 14695981039346656037ul;
            hash = Fold(hash, (uint)result.ExpectedWrites);
            hash = Fold(hash, (uint)result.AcceptedWrites);
            hash = Fold(hash, (uint)result.DrainedWrites);
            hash = Fold(hash, (uint)result.UniqueWrites);
            hash = Fold(hash, (uint)result.DroppedWrites);
            hash = Fold(hash, (uint)result.DuplicateWrites);
            hash = Fold(hash, (uint)result.CorruptedWrites);
            hash = Fold(hash, result.Status);
            return hash == 0ul ? 1ul : hash;
        }

        private static ulong Fold(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211ul;
            return hash;
        }

        private static void WriteReport(in SignalStormFuzzerResult1311 result)
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string path = Path.Combine(root, "Docs", "Reports", "SIGNAL_STORM_CONCURRENCY_FUZZER_1311.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string status = result.Status == StatusGreen ? "GREEN_STATIC_EDITOR_RUN" : "RED_EDITOR_RUN";
            string json =
                "{\n" +
                "  \"agent\": 1311,\n" +
                "  \"status\": \"" + status + "\",\n" +
                "  \"producerCount\": " + result.ProducerCount + ",\n" +
                "  \"writesPerProducer\": " + result.WritesPerProducer + ",\n" +
                "  \"expectedWrites\": " + result.ExpectedWrites + ",\n" +
                "  \"acceptedWrites\": " + result.AcceptedWrites + ",\n" +
                "  \"drainedWrites\": " + result.DrainedWrites + ",\n" +
                "  \"uniqueWrites\": " + result.UniqueWrites + ",\n" +
                "  \"droppedWrites\": " + result.DroppedWrites + ",\n" +
                "  \"duplicateWrites\": " + result.DuplicateWrites + ",\n" +
                "  \"corruptedWrites\": " + result.CorruptedWrites + ",\n" +
                "  \"missingWrites\": " + result.MissingWrites + ",\n" +
                "  \"elapsedTicks\": " + result.ElapsedTicks + ",\n" +
                "  \"resultHash\": " + result.ResultHash + "\n" +
                "}\n";
            File.WriteAllText(path, json);
        }

        private sealed class ProducerState
        {
            public MpscSignalRingBuffer<SignalStormFuzzerPayload1311>.ParallelWriter Writer;
            public ManualResetEventSlim StartGate;
            public int ProducerIndex;
            public int WritesPerProducer;
            public int AcceptedWrites;
            public int DroppedWrites;
            public int StopRequested;
            public int Faulted;
        }
    }
}
#endif
