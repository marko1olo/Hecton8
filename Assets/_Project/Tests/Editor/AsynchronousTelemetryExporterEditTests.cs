using System.IO;
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
        public void RuntimeSourceContainsNoMainThreadWebOrJsonRoute()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Not.Contain("UnityWebRequest"));
            Assert.That(source, Does.Not.Contain("JsonUtility"));
            Assert.That(source, Does.Not.Contain("ToJson"));
            Assert.That(source, Does.Not.Contain("Schedule().Complete"));
            Assert.That(source, Does.Not.Contain("Encoding.UTF8.GetBytes"));
            Assert.That(source, Does.Not.Contain("using Hecton8.World;"));
            Assert.That(source, Does.Not.Contain("TryGetParallelWriter"));
            Assert.That(source, Does.Not.Contain("Time.frameCount"));
            Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(source, Does.Not.Contain("new AnalyticEventDTO"));
            Assert.That(source, Does.Not.Contain("EventCount = 500"));
            Assert.That(source, Does.Not.Contain("System.Reflection"));
            Assert.That(source, Does.Not.Contain(".GetField("));
            Assert.That(source, Does.Contain("Name = \"H8_Analytics_IO\""));
            Assert.That(source, Does.Contain("DispatcherPhase.PostSimulation"));
            Assert.That(source, Does.Contain("ResolveFrameId"));
            Assert.That(source, Does.Contain("Unity.Mathematics.Random"));
            Assert.That(source, Does.Contain("AnalyticsIngressCursorDTO"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.RoutineIngress"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.CriticalIngress"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.IngressCursor"));
            Assert.That(source, Does.Contain("TryWriteIngressEvent"));
            Assert.That(source, Does.Contain("UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>"));
            Assert.That(source, Does.Contain("private const int IngressWriteOverflow = 2"));
            Assert.That(source, Does.Contain("writeResult != IngressWriteOverflow"));
            Assert.That(source, Does.Contain("cursor.RoutineOverflowDrops += 1u"));
            Assert.That(source, Does.Contain("cursor.CriticalOverflowDrops += 1u"));
            Assert.That(source, Does.Not.Contain("NativeQueue<AnalyticEventDTO>"));
            Assert.That(source, Does.Not.Contain("new NativeQueue"));
            Assert.That(source, Does.Not.Contain("TryGetQueues"));
            Assert.That(source, Does.Contain("drained < drainBudget"));
            Assert.That(source, Does.Contain("ResolveVaultTelemetryBytes"));
            Assert.That(source, Does.Contain("VaultBytes"));
            Assert.That(source, Does.Contain("IngestGameplaySignals"));
            Assert.That(source, Does.Contain("SignalBus<EntityDeathSignal>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("SignalBus<ItemAcquiredSignal>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("SignalBus<FrameTimeSignal>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("if (!recordRouteSample || _heatmapTimerSeconds < sampleSeconds)"));
            Assert.That(source, Does.Contain("math.lerp(20f, 500f, smoothQuality)"));
            Assert.That(source, Does.Contain("ResolveBacklogPressureEvents() / math.max(1f, (float)pressureLimit)"));
            Assert.That(source, Does.Contain("FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.HandoffA"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.RawBatchScratch"));
            Assert.That(source, Does.Contain("BatchStateWriting"));
            Assert.That(source, Does.Contain("Interlocked.Exchange(ref _pendingBatchState, BatchStatePending)"));
            Assert.That(source, Does.Contain("request.Abort()"));
            Assert.That(source, Does.Contain("TeardownStoppedWorkerState"));
            Assert.That(source, Does.Contain("CreateLockedWorkerView"));
            Assert.That(source, Does.Contain("ResolveWorkerHandoffBuffer"));
            Assert.That(source, Does.Contain("ShouldAcceptHotPathEvent"));
            Assert.That(source, Does.Contain("HashHotPathGate(eventHashId, timestampSeconds, unchecked((uint)backlog), eventAup)"));
            Assert.That(source, Does.Contain("ShouldDropRoutineDuringDrain"));
            Assert.That(source, Does.Contain("HashDrainGate"));
            Assert.That(source, Does.Contain("math.aslong(value)"));
            Assert.That(source, Does.Not.Contain("bool pressureCull"));
            Assert.That(source, Does.Contain("BitConverter.DoubleToInt64Bits"));
            Assert.That(source, Does.Contain("Interlocked.Increment(ref _ingressPendingEstimate)"));
            Assert.That(source, Does.Contain("Interlocked.Add(ref _hotEnqueuedDelta, written)"));
            Assert.That(source, Does.Contain("Interlocked.Add(ref _ingressPendingEstimate, written)"));
            Assert.That(source, Does.Contain("telemetry_config.csv"));
            Assert.That(source, Does.Contain("NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray"));
            Assert.That(source, Does.Contain("AnalyticsVaultBufferIds.DumpSnapshot"));
            Assert.That(source, Does.Contain("TryWritePendingBlackBoxDump"));
            Assert.That(source, Does.Contain("File.Move(tmpPath, finalPath)"));
            Assert.That(source, Does.Contain("bool deleteAfterRead = false"));
            Assert.That(source, Does.Contain("if (deleteAfterRead)"));
            Assert.That(source, Does.Contain("TryFlushDiskBacklogUnchecked"));
            Assert.That(source, Does.Contain("TryDeleteReplayFile"));
            Assert.That(source, Does.Contain("if (read != length)"));
            Assert.That(source, Does.Contain("deleteAfterRead = true;"));
            Assert.That(source, Does.Contain("_fallbackFileSequence"));
            Assert.That(source, Does.Contain("FileMode.CreateNew"));
            Assert.That(source, Does.Contain("TryDeleteTempFallbackFile"));
            Assert.That(source, Does.Contain("response.Dispose();"));
            Assert.That(source, Does.Contain("IsHttpEndpoint"));
            Assert.That(source, Does.Contain("StringComparison.OrdinalIgnoreCase"));
            Assert.That(source, Does.Not.Contain("PrewarmQueue"));
            Assert.That(source, Does.Contain("AnalyticsLayout.ValidateOrThrow();"));
            Assert.That(source, Does.Contain("NoteHotPathNonFinite"));
            Assert.That(source, Does.Contain("Interlocked.Exchange(ref _hotNonFiniteDelta, 0)"));
            Assert.That(source, Does.Contain("value.NonFiniteEvents += unchecked((uint)nonFinite)"));
            Assert.That(source, Does.Contain("SetWorkerFlag(WorkerFlagFaulted)"));
            Assert.That(source, Does.Contain("ClearWorkerFlag(WorkerFlagRunning)"));
            Assert.That(source, Does.Contain("Interlocked.CompareExchange(ref _workerFlags"));
            Assert.That(source, Does.Contain("PublishWorkerAccumCount(accumCount)"));
            Assert.That(source, Does.Contain("PublishWorkerAccumCount(0)"));
            Assert.That(source, Does.Not.Contain("Volatile.Write(ref _workerFlags, Volatile.Read(ref _workerFlags)"));
            Assert.That(source, Does.Not.Contain("File.Delete(finalPath)"));
        }
    }
}
