#if UNITY_EDITOR && HECTON8_ENABLE_LEGACY_MEMORY_FUZZER_1310
// Legacy 1310 fuzzer is opt-in only. It uses raw Thread joins and a disposable GlobalDataVault;
// agent 1412 keeps the active compaction stress path in Core/Memory/Editor with bounded cleanup proof.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorValidation
{
    public static class OOP_MemorySentryConcurrentRelocationFuzzer
    {
        private const string AgentId = "1310";
        private const string ReportPath = "Docs/Reports/MEMORY_SENTRY_CONCURRENT_RELOCATION_FUZZER_1310.json";
        private const int TargetFrames = 100000;
        private const int StableBufferBase = 99000;
        private const int StableBufferCount = 16;
        private const int ChurnBufferBase = 99032;
        private const int ChurnBufferCount = 24;
        private const int WorkerCount = 4;

        [MenuItem("Hecton8/Memory/Run 1310 Concurrent Relocation Fuzzer")]
        private static void RunFromMenu()
        {
            bool passed = Run(TargetFrames);
            Debug.Log("[OOP_MemorySentryConcurrentRelocationFuzzer] passed=" + passed + " report=" + ReportPath);
        }

        public static void RunBatch()
        {
            bool passed = Run(TargetFrames);
            Debug.Log("[OOP_MemorySentryConcurrentRelocationFuzzer] batch passed=" + passed + " report=" + ReportPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        internal static bool Run(int targetFrames)
        {
            string projectRoot = ResolveProjectRoot();
            long startManagedBytes = GetAllocatedBytesForCurrentThreadSafe();
            long startTicks = Stopwatch.GetTimestamp();
            int completedFrames = 0;
            int writeLockPasses = 0;
            int writeLockRejects = 0;
            int pinPasses = 0;
            int pinRejects = 0;
            int allocationPasses = 0;
            int releasePasses = 0;
            int compactionTicks = 0;
            int workerFailures = 0;
            int stop = 0;
            string firstFailure = string.Empty;
            object failureLock = new object();

            using GlobalDataVault vault = GlobalDataVault.Create(512, 64L * 1024L * 1024L);
            PreseedStableBuffers(vault);

            Thread[] workers =
            {
                new Thread(() => RunWriteLockWorker(vault, targetFrames, ref completedFrames, ref writeLockPasses, ref writeLockRejects, ref stop, ref workerFailures, ref firstFailure, failureLock)),
                new Thread(() => RunPinWorker(vault, targetFrames, ref completedFrames, ref pinPasses, ref pinRejects, ref stop, ref workerFailures, ref firstFailure, failureLock)),
                new Thread(() => RunAllocationWorker(vault, targetFrames, ref completedFrames, ref allocationPasses, ref releasePasses, ref stop, ref workerFailures, ref firstFailure, failureLock)),
                new Thread(() => RunCompactionWorker(vault, targetFrames, ref completedFrames, ref compactionTicks, ref stop, ref workerFailures, ref firstFailure, failureLock))
            };

            for (int i = 0; i < workers.Length; i++)
            {
                workers[i].IsBackground = true;
                workers[i].Name = "H8MemorySentry1310_" + i.ToString(CultureInfo.InvariantCulture);
                workers[i].Start();
            }

            bool joined = true;
            for (int i = 0; i < workers.Length; i++)
                joined &= workers[i].Join(30000);

            Volatile.Write(ref stop, 1);
            for (int i = 0; i < workers.Length; i++)
            {
                if (workers[i].IsAlive)
                    joined &= workers[i].Join(1000);
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            long endManagedBytes = GetAllocatedBytesForCurrentThreadSafe();
            bool telemetryOk = vault.TryGetVaultTelemetrySnapshot(0, out VaultTelemetrySnapshot snapshot);
            bool completed = Volatile.Read(ref completedFrames) >= targetFrames;
            bool passed = joined && completed && Volatile.Read(ref workerFailures) == 0 && telemetryOk;

            StringBuilder json = new StringBuilder(4096);
            json.AppendLine("{");
            AppendJsonField(json, "agent", AgentId, comma: true, indent: 1);
            AppendJsonField(json, "status", passed ? "PASS" : "FAIL", comma: true, indent: 1);
            AppendJsonNumber(json, "targetFrames", targetFrames, comma: true, indent: 1);
            AppendJsonNumber(json, "completedFrames", Volatile.Read(ref completedFrames), comma: true, indent: 1);
            AppendJsonBool(json, "workersJoined", joined, comma: true, indent: 1);
            AppendJsonNumber(json, "workerCount", WorkerCount, comma: true, indent: 1);
            AppendJsonNumber(json, "workerFailures", Volatile.Read(ref workerFailures), comma: true, indent: 1);
            AppendJsonField(json, "firstFailure", firstFailure, comma: true, indent: 1);
            AppendJsonNumber(json, "writeLockPasses", Volatile.Read(ref writeLockPasses), comma: true, indent: 1);
            AppendJsonNumber(json, "writeLockRejects", Volatile.Read(ref writeLockRejects), comma: true, indent: 1);
            AppendJsonNumber(json, "pinPasses", Volatile.Read(ref pinPasses), comma: true, indent: 1);
            AppendJsonNumber(json, "pinRejects", Volatile.Read(ref pinRejects), comma: true, indent: 1);
            AppendJsonNumber(json, "allocationPasses", Volatile.Read(ref allocationPasses), comma: true, indent: 1);
            AppendJsonNumber(json, "releasePasses", Volatile.Read(ref releasePasses), comma: true, indent: 1);
            AppendJsonNumber(json, "compactionTicks", Volatile.Read(ref compactionTicks), comma: true, indent: 1);
            AppendJsonNumber(json, "elapsedMicroseconds", TicksToMicroseconds(elapsedTicks), comma: true, indent: 1);
            AppendJsonNumber(json, "mainThreadManagedBytesDelta", ComputeDeltaOrMinusOne(startManagedBytes, endManagedBytes), comma: true, indent: 1);
            AppendJsonBool(json, "telemetryResolved", telemetryOk, comma: true, indent: 1);
            AppendJsonNumber(json, "vaultAllocatedBytes", vault.AllocatedBytes, comma: true, indent: 1);
            AppendJsonNumber(json, "vaultArenaBytes", vault.ArenaBytes, comma: true, indent: 1);
            AppendJsonNumber(json, "lastDefragMovedBytes", snapshot.LastMovedBytes, comma: true, indent: 1);
            AppendJsonNumber(json, "lastDefragFlags", snapshot.LastDefragFlags, comma: false, indent: 1);
            json.AppendLine();
            json.AppendLine("}");

            WriteReport(projectRoot, json.ToString());
            return passed;
        }

        private static void PreseedStableBuffers(GlobalDataVault vault)
        {
            for (int i = 0; i < StableBufferCount; i++)
            {
                BufferID id = (BufferID)(StableBufferBase + i);
                vault.EnsureGenerationHandle<int>(id, 64 + i, SystemID.CoreDataVault, NativeArrayOptions.ClearMemory);
            }
        }

        private static void RunWriteLockWorker(
            GlobalDataVault vault,
            int targetFrames,
            ref int completedFrames,
            ref int writeLockPasses,
            ref int writeLockRejects,
            ref int stop,
            ref int workerFailures,
            ref string firstFailure,
            object failureLock)
        {
            try
            {
                int cursor = 0;
                while (TryBeginFrame(targetFrames, ref completedFrames, ref stop))
                {
                    BufferID id = (BufferID)(StableBufferBase + (cursor++ & (StableBufferCount - 1)));
                    if (vault.TryGetGenerationHandle<int>(id, out VaultGenerationHandle<int> handle) &&
                        vault.TryAcquireWriteLock(in handle, SystemID.CoreDataVault, out NativeArray<int> buffer))
                    {
                        if (buffer.IsCreated && buffer.Length > 0)
                            buffer[0] = buffer[0] + 1;
                        vault.ReleaseWriteLock(in handle, SystemID.CoreDataVault);
                        Interlocked.Increment(ref writeLockPasses);
                    }
                    else
                    {
                        Interlocked.Increment(ref writeLockRejects);
                    }
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex, ref workerFailures, ref firstFailure, failureLock);
                Volatile.Write(ref stop, 1);
            }
        }

        private static void RunPinWorker(
            GlobalDataVault vault,
            int targetFrames,
            ref int completedFrames,
            ref int pinPasses,
            ref int pinRejects,
            ref int stop,
            ref int workerFailures,
            ref string firstFailure,
            object failureLock)
        {
            try
            {
                int cursor = 0;
                while (TryBeginFrame(targetFrames, ref completedFrames, ref stop))
                {
                    BufferID id = (BufferID)(StableBufferBase + (cursor++ & (StableBufferCount - 1)));
                    ulong mutationGuardMask = LegacyFuzzerMutationGuardBit(id);
                    if (vault.TryAcquireMutationGuard(mutationGuardMask))
                    {
                        try
                        {
                            Thread.SpinWait(32);
                        }
                        finally
                        {
                            vault.ReleaseMutationGuard(mutationGuardMask);
                        }

                        Interlocked.Increment(ref pinPasses);
                    }
                    else
                    {
                        Interlocked.Increment(ref pinRejects);
                    }
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex, ref workerFailures, ref firstFailure, failureLock);
                Volatile.Write(ref stop, 1);
            }
        }

        private static ulong LegacyFuzzerMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static void RunAllocationWorker(
            GlobalDataVault vault,
            int targetFrames,
            ref int completedFrames,
            ref int allocationPasses,
            ref int releasePasses,
            ref int stop,
            ref int workerFailures,
            ref string firstFailure,
            object failureLock)
        {
            try
            {
                int cursor = 0;
                while (TryBeginFrame(targetFrames, ref completedFrames, ref stop))
                {
                    int local = cursor++;
                    BufferID id = (BufferID)(ChurnBufferBase + (local % ChurnBufferCount));
                    int length = 32 + ((local * 17) & 255);
                    VaultGenerationHandle<int> handle = vault.EnsureGenerationHandle<int>(id, length, SystemID.CoreDataVault, NativeArrayOptions.ClearMemory);
                    if (handle.Generation != 0u)
                        Interlocked.Increment(ref allocationPasses);

                    if ((local & 3) == 0 &&
                        vault.TryGetGenerationHandle<int>(id, out VaultGenerationHandle<int> releaseHandle) &&
                        vault.ReleaseBuffer(in releaseHandle))
                    {
                        Interlocked.Increment(ref releasePasses);
                    }
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex, ref workerFailures, ref firstFailure, failureLock);
                Volatile.Write(ref stop, 1);
            }
        }

        private static void RunCompactionWorker(
            GlobalDataVault vault,
            int targetFrames,
            ref int completedFrames,
            ref int compactionTicks,
            ref int stop,
            ref int workerFailures,
            ref string firstFailure,
            object failureLock)
        {
            try
            {
                int cursor = 0;
                while (TryBeginFrame(targetFrames, ref completedFrames, ref stop))
                {
                    uint externalMask = (cursor++ & 15) == 0 ? 1u : 0u;
                    vault.RequestEditorForceDefragmentation();
                    vault.FrostTickDefrag(1f / 60f, 1f, MemoryDefragPhase.PreSimulation, externalMask);
                    Interlocked.Increment(ref compactionTicks);
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex, ref workerFailures, ref firstFailure, failureLock);
                Volatile.Write(ref stop, 1);
            }
        }

        private static bool TryBeginFrame(int targetFrames, ref int completedFrames, ref int stop)
        {
            if (Volatile.Read(ref stop) != 0)
                return false;

            int frame = Interlocked.Increment(ref completedFrames);
            if (frame <= targetFrames)
                return true;

            Volatile.Write(ref stop, 1);
            return false;
        }

        private static void RecordWorkerFailure(Exception ex, ref int workerFailures, ref string firstFailure, object failureLock)
        {
            Interlocked.Increment(ref workerFailures);
            lock (failureLock)
            {
                if (string.IsNullOrEmpty(firstFailure))
                    firstFailure = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static string ResolveProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static void WriteReport(string projectRoot, string json)
        {
            string path = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static long GetAllocatedBytesForCurrentThreadSafe()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch (NotSupportedException)
            {
                return -1L;
            }
        }

        private static long ComputeDeltaOrMinusOne(long beforeBytes, long afterBytes)
        {
            return beforeBytes >= 0L && afterBytes >= beforeBytes ? afterBytes - beforeBytes : -1L;
        }

        private static double TicksToMicroseconds(long ticks)
        {
            return ticks * (1000000.0d / Stopwatch.Frequency);
        }

        private static void AppendJsonField(StringBuilder builder, string name, string value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonBool(StringBuilder builder, string name, bool value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonNumber(StringBuilder builder, string name, double value, bool comma, int indent)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(name).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            for (int i = 0; i < indent; i++)
                builder.Append("  ");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
