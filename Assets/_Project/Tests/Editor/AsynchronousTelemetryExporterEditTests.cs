using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AsynchronousTelemetryExporterEditTests
    {
        [Test]
        public void AnalyticEventDtoLayoutIsArm64Exact()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<AnalyticEventDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<AnalyticEventDTO>(nameof(AnalyticEventDTO.EventHashID)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<AnalyticEventDTO>(nameof(AnalyticEventDTO.TimestampSeconds)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<AnalyticEventDTO>(nameof(AnalyticEventDTO.EventAUP)));
        }

        [Test]
        public void TelemetryDtoLayoutsStayFalseSharingSafe()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AnalyticsCountersDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AnalyticsTuningDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AnalyticsExporterTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AnalyticsIngressCursorDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<AnalyticsCountersDTO>(nameof(AnalyticsCountersDTO.EnqueuedEvents)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<AnalyticsCountersDTO>(nameof(AnalyticsCountersDTO.EventRingWriteCursor)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<AnalyticsTuningDTO>(nameof(AnalyticsTuningDTO.EndpointHash)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<AnalyticsExporterTelemetryEntry>(nameof(AnalyticsExporterTelemetryEntry.Flags)));
            Assert.AreEqual(60, (int)Marshal.OffsetOf<AnalyticsExporterTelemetryEntry>(nameof(AnalyticsExporterTelemetryEntry.VaultBytes)));
            Assert.AreEqual(0, (int)Marshal.OffsetOf<AnalyticsIngressCursorDTO>(nameof(AnalyticsIngressCursorDTO.RoutineWriteCursor)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<AnalyticsIngressCursorDTO>(nameof(AnalyticsIngressCursorDTO.RoutineCapacity)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<AnalyticsIngressCursorDTO>(nameof(AnalyticsIngressCursorDTO.StateHash)));
        }

        [Test]
        public void RleCompressorConsumesUnmanagedNativeBuffers()
        {
            NativeArray<byte> source = new NativeArray<byte>(32, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> destination = new NativeArray<byte>(96, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < source.Length; i++)
                    source[i] = i < 20 ? (byte)7 : (byte)i;

                int bytes = AnalyticsCompression.CompressRleBlock(source, source.Length, destination);
                Assert.Greater(bytes, 0);
                Assert.Less(bytes, source.Length);
                Assert.AreEqual(0xFF, destination[0]);
                Assert.AreEqual(20, destination[1]);
                Assert.AreEqual(7, destination[2]);
            }
            finally
            {
                if (source.IsCreated)
                    source.Dispose();
                if (destination.IsCreated)
                    destination.Dispose();
            }
        }

        [Test]
        public void TelemetryDumpStartIndex_UsesZeroBeforeRingWrap()
        {
            Assert.AreEqual(0, InvokeStartIndex(cursorValue: 17, ringLength: 300, ringHasWrapped: false));
        }

        [Test]
        public void TelemetryDumpStartIndex_UsesCursorAfterRingWrap()
        {
            Assert.AreEqual(17, InvokeStartIndex(cursorValue: 317, ringLength: 300, ringHasWrapped: true));
        }

        [Test]
        public void TelemetryDumpSourceIndex_WrapsChronologicalSequence()
        {
            Assert.AreEqual(298, InvokeSourceIndex(startIndex: 298, ringLength: 300, offset: 0));
            Assert.AreEqual(299, InvokeSourceIndex(startIndex: 298, ringLength: 300, offset: 1));
            Assert.AreEqual(0, InvokeSourceIndex(startIndex: 298, ringLength: 300, offset: 2));
            Assert.AreEqual(1, InvokeSourceIndex(startIndex: 298, ringLength: 300, offset: 3));
        }

        [Test]
        public void TelemetryDumpSourceIndex_NormalizesNegativeStart()
        {
            Assert.AreEqual(299, InvokeSourceIndex(startIndex: -1, ringLength: 300, offset: 0));
            Assert.AreEqual(0, InvokeSourceIndex(startIndex: -1, ringLength: 300, offset: 1));
        }

        [Test]
        public void RuntimeSourceContainsNoMainThreadWebOrJsonRoute()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            AssertFileTokens(
                path,
                new[]
                {
                    "Name = \"H8_Analytics_IO\"",
                    "DispatcherPhase.PostSimulation",
                    "ResolveFrameId",
                    "Unity.Mathematics.Random",
                    "AnalyticsIngressCursorDTO",
                    "AnalyticsVaultBufferIds.RoutineIngress",
                    "AnalyticsVaultBufferIds.CriticalIngress",
                    "AnalyticsVaultBufferIds.IngressCursor",
                    "TryWriteIngressEvent",
                    "UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>",
                    "private const int IngressWriteOverflow = 2",
                    "writeResult != IngressWriteOverflow",
                    "cursor.RoutineOverflowDrops += 1u",
                    "cursor.CriticalOverflowDrops += 1u",
                    "drained < drainBudget",
                    "ResolveVaultTelemetryBytes",
                    "VaultBytes",
                    "IngestGameplaySignals",
                    "SignalBus<EntityDeathSignal>.GetFrameSnapshot",
                    "SignalBus<ItemAcquiredSignal>.GetFrameSnapshot",
                    "SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot",
                    "SignalBus<FrameTimeSignal>.GetFrameSnapshot",
                    "if (!recordRouteSample || _heatmapTimerSeconds < sampleSeconds)",
                    "math.lerp(20f, 500f, smoothQuality)",
                    "ResolveBacklogPressureEvents() / math.max(1f, (float)pressureLimit)",
                    "FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard",
                    "AnalyticsVaultBufferIds.HandoffA",
                    "AnalyticsVaultBufferIds.RawBatchScratch",
                    "BatchStateWriting",
                    "Interlocked.Exchange(ref _pendingBatchState, BatchStatePending)",
                    "request.Abort()",
                    "TeardownStoppedWorkerState",
                    "CreateLockedWorkerView",
                    "ResolveWorkerHandoffBuffer",
                    "ShouldAcceptHotPathEvent",
                    "HashHotPathGate(eventHashId, timestampSeconds, unchecked((uint)backlog), eventAup)",
                    "ShouldDropRoutineDuringDrain",
                    "HashDrainGate",
                    "math.aslong(value)",
                    "BitConverter.DoubleToInt64Bits",
                    "Interlocked.Increment(ref _ingressPendingEstimate)",
                    "Interlocked.Add(ref _hotEnqueuedDelta, written)",
                    "Interlocked.Add(ref _ingressPendingEstimate, written)",
                    "telemetry_config.csv",
                    "NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray",
                    "AnalyticsVaultBufferIds.DumpSnapshot",
                    "TryWritePendingBlackBoxDump",
                    "File.Move(tmpPath, finalPath)",
                    "bool deleteAfterRead = false",
                    "if (deleteAfterRead)",
                    "TryFlushDiskBacklogUnchecked",
                    "TryDeleteReplayFile",
                    "if (read != length)",
                    "deleteAfterRead = true;",
                    "_fallbackFileSequence",
                    "FileMode.CreateNew",
                    "TryDeleteTempFallbackFile",
                    "response.Dispose();",
                    "IsHttpEndpoint",
                    "StringComparison.OrdinalIgnoreCase",
                    "AnalyticsLayout.ValidateOrThrow();",
                    "NoteHotPathNonFinite",
                    "Interlocked.Exchange(ref _hotNonFiniteDelta, 0)",
                    "value.NonFiniteEvents += unchecked((uint)nonFinite)",
                    "SetWorkerFlag(WorkerFlagFaulted)",
                    "ClearWorkerFlag(WorkerFlagRunning)",
                    "Interlocked.CompareExchange(ref _workerFlags",
                    "PublishWorkerAccumCount(accumCount)",
                    "PublishWorkerAccumCount(0)",
                    "ResolveTelemetryDumpStartIndex",
                    "ResolveTelemetryDumpSourceIndex"
                },
                new[]
                {
                    "UnityWebRequest",
                    "JsonUtility",
                    "ToJson",
                    "Schedule().Complete",
                    "Encoding.UTF8.GetBytes",
                    "using Hecton8.World;",
                    "TryGetParallelWriter",
                    "Time.frameCount",
                    "UnityEngine.Random",
                    "new AnalyticEventDTO",
                    "EventCount = 500",
                    "System.Reflection",
                    ".GetField(",
                    "NativeQueue<AnalyticEventDTO>",
                    "new NativeQueue",
                    "TryGetQueues",
                    "bool pressureCull",
                    "PrewarmQueue",
                    "Volatile.Write(ref _workerFlags, Volatile.Read(ref _workerFlags)",
                    "File.Delete(finalPath)"
                });
        }

        private static int InvokeStartIndex(int cursorValue, int ringLength, bool ringHasWrapped)
        {
            MethodInfo method = ResolvePrivateStaticMethod("ResolveTelemetryDumpStartIndex");
            return (int)method.Invoke(null, new object[] { cursorValue, ringLength, ringHasWrapped });
        }

        private static int InvokeSourceIndex(int startIndex, int ringLength, int offset)
        {
            MethodInfo method = ResolvePrivateStaticMethod("ResolveTelemetryDumpSourceIndex");
            return (int)method.Invoke(null, new object[] { startIndex, ringLength, offset });
        }

        private static MethodInfo ResolvePrivateStaticMethod(string methodName)
        {
            MethodInfo method = typeof(AsynchronousTelemetryExporter).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method, methodName);
            return method;
        }

        private static void AssertFileTokens(string path, string[] required, string[] forbidden)
        {
            bool[] requiredFound = new bool[required.Length];
            foreach (string line in File.ReadLines(path))
            {
                for (int i = 0; i < forbidden.Length; i++)
                {
                    if (line.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                        Assert.Fail(path + " contains forbidden token: " + forbidden[i]);
                }

                for (int i = 0; i < required.Length; i++)
                {
                    if (!requiredFound[i] && line.IndexOf(required[i], StringComparison.Ordinal) >= 0)
                        requiredFound[i] = true;
                }
            }

            for (int i = 0; i < required.Length; i++)
            {
                if (!requiredFound[i])
                    Assert.Fail(path + " missing token: " + required[i]);
            }
        }
    }
}
