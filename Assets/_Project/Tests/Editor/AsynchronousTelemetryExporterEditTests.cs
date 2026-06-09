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
                    "SurvivalSignalRoute.TryGetLatestDeathForSource",
                    "_lastSurvivalDeathSignalSequence",
                    "IngestLatestSurvivalDeathSignal",
                    "TryRecordSurvivalDeathTelemetry",
                    "ResolveSurvivalDeathSignalFrame",
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
                    "FileOptions.WriteThrough",
                    "stream.Length != byteCount",
                    "TryGetFallbackFileLength(finalPath, out long finalBytes)",
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

        [Test]
        public void ItemAcquiredTelemetry_RecordsOnlyResourceDeltaSources()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string ingest = ExtractMethodBody(source, "private void IngestItemAcquiredSignals(uint timestampSeconds)");
            string filter = ExtractMethodBody(source, "private static bool IsResourceDeltaSource(byte sourceKind)");

            Assert.That(ingest, Does.Contain("if (!IsResourceDeltaSource(signal.SourceKind))"));
            Assert.That(ingest, Does.Contain("TryRecordEvent(AnalyticsEventHashes.ResourceDelta, timestampSeconds, aup);"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.Unknown"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.ResourceNode"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.ProceduralOreSpawner"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.DeployableSdfDrill"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.VoxelCarve"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.ScavengingLootOracle"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.HarvestableOutcrop"));
            Assert.That(filter, Does.Contain("ItemAcquiredSignalSourceKinds.DroneMining"));
            Assert.That(filter, Does.Not.Contain("ItemAcquiredSignalSourceKinds.Fabricator"));
            Assert.That(filter, Does.Not.Contain("ItemAcquiredSignalSourceKinds.DeconstructionRefund"));
            Assert.That(filter, Does.Not.Contain("ItemAcquiredSignalSourceKinds.ManualPickup"));
            Assert.That(filter, Does.Not.Contain("ItemAcquiredSignalSourceKinds.LootMagnet"));
        }

        [Test]
        public void SurvivalDeathTelemetryUsesLatestBridgeBeforeFrameSnapshot()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string callback = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string reset = ExtractMethodBody(source, "private void ResetHotPathCounters()");
            string ingest = ExtractMethodBody(source, "private void IngestSurvivalDeathSignals(uint timestampSeconds, uint frameId)");
            string latest = ExtractMethodBody(source, "private void IngestLatestSurvivalDeathSignal(uint timestampSeconds, uint frameId, uint sourceId)");
            string record = ExtractMethodBody(source, "private bool TryRecordSurvivalDeathTelemetry(");
            string frame = ExtractMethodBody(source, "private static uint ResolveSurvivalDeathSignalFrame(");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)");
            string clearPlayer = ExtractMethodBody(source, "private void ClearPlayerRuntimeContext()");
            string refreshSurvival = ExtractMethodBody(source, "private void RefreshSurvivalSignalBinding()");
            string resolveSource = ExtractMethodBody(source, "private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)");

            StringAssert.Contains("using Hecton8.Gameplay;", source);
            StringAssert.Contains("private uint _survivalDeathSignalSourceId;", source);
            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("private HectonSurvivalSystem _survivalSystem;", source);
            StringAssert.Contains("private int _lastSurvivalDeathSignalSequence;", source);

            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player);", onEnable);
            StringAssert.Contains("RefreshSurvivalSignalBinding();", onEnable);
            Assert.That(
                onEnable.IndexOf("ResetHotPathCounters();", StringComparison.Ordinal),
                Is.LessThan(onEnable.IndexOf("CachePlayerRuntimeContext(GlobalRegistry.Player);", StringComparison.Ordinal)));
            Assert.That(
                onEnable.IndexOf("CachePlayerRuntimeContext(GlobalRegistry.Player);", StringComparison.Ordinal),
                Is.LessThan(onEnable.IndexOf("RefreshSurvivalSignalBinding();", StringComparison.Ordinal)));

            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Player)", callback);
            StringAssert.Contains("CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);", callback);
            StringAssert.Contains("RefreshSurvivalSignalBinding();", callback);
            Assert.That(
                callback.IndexOf("if (serviceSlot == GlobalRegistryServiceSlot.Player)", StringComparison.Ordinal),
                Is.LessThan(callback.IndexOf("if (serviceSlot != GlobalRegistryServiceSlot.DataVault)", StringComparison.Ordinal)));

            StringAssert.Contains("_lastSurvivalDeathSignalSequence = 0;", reset);
            StringAssert.Contains("ClearPlayerRuntimeContext();", onDisable);
            StringAssert.Contains("ClearPlayerRuntimeContext();", onDestroy);
            Assert.That(
                onDisable.IndexOf("ClearPlayerRuntimeContext();", StringComparison.Ordinal),
                Is.LessThan(onDisable.IndexOf("if (!StopWorker())", StringComparison.Ordinal)));
            Assert.That(
                onDestroy.IndexOf("ClearPlayerRuntimeContext();", StringComparison.Ordinal),
                Is.LessThan(onDestroy.IndexOf("if (StopWorker())", StringComparison.Ordinal)));
            StringAssert.Contains("if (!_hasLastKnownPlayerAup)", ingest);
            StringAssert.Contains("uint sourceId = _survivalDeathSignalSourceId;", ingest);
            StringAssert.Contains("if (sourceId == 0u)", ingest);
            StringAssert.Contains("IngestLatestSurvivalDeathSignal(timestampSeconds, frameId, sourceId);", ingest);
            StringAssert.Contains("SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot()", ingest);
            StringAssert.Contains("ResolveSurvivalDeathSignalFrame(in signal, frameId)", ingest);
            StringAssert.Contains("TryRecordSurvivalDeathTelemetry(in signal, sourceId, timestampSeconds, signalFrame);", ingest);
            Assert.That(
                ingest.IndexOf("IngestLatestSurvivalDeathSignal(timestampSeconds, frameId, sourceId);", StringComparison.Ordinal),
                Is.LessThan(ingest.IndexOf("SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot()", StringComparison.Ordinal)));

            StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out SurvivalVitalsChangedSignal signal, out int sequence)", latest);
            StringAssert.Contains("if (sequence == _lastSurvivalDeathSignalSequence)", latest);
            StringAssert.Contains("if (TryRecordSurvivalDeathTelemetry(in signal, sourceId, timestampSeconds, signalFrame))", latest);
            StringAssert.Contains("_lastSurvivalDeathSignalSequence = sequence;", latest);
            StringAssert.DoesNotContain("SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence)", latest);
            Assert.That(
                latest.IndexOf("TryRecordSurvivalDeathTelemetry(in signal, sourceId, timestampSeconds, signalFrame)", StringComparison.Ordinal),
                Is.LessThan(latest.IndexOf("_lastSurvivalDeathSignalSequence = sequence;", StringComparison.Ordinal)));

            StringAssert.Contains("if (sourceId == 0u || signal.SourceId != sourceId)", record);
            StringAssert.Contains("SurvivalVitalsChangedSignalFlags.Death", record);
            StringAssert.Contains("if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)", record);
            StringAssert.DoesNotContain("&& signal.DeathCause == 0", record);
            StringAssert.Contains("signalFrame != 0u && signalFrame == _lastSurvivalDeathFrame", record);
            StringAssert.Contains("if (!TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, _lastKnownPlayerAup))", record);
            StringAssert.Contains("_lastSurvivalDeathFrame = signalFrame;", record);
            StringAssert.DoesNotContain("return TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, _lastKnownPlayerAup);", record);
            Assert.That(
                record.IndexOf("TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, _lastKnownPlayerAup)", StringComparison.Ordinal),
                Is.LessThan(record.IndexOf("_lastSurvivalDeathFrame = signalFrame;", StringComparison.Ordinal)));

            StringAssert.Contains("return signal.Frame != 0u ? signal.Frame : fallbackFrameId;", frame);
            StringAssert.Contains("_playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;", cachePlayer);
            StringAssert.Contains("_survivalSystem = _playerRuntimeContext != null ? _playerRuntimeContext.SurvivalSystem : null;", cachePlayer);
            StringAssert.Contains("_playerRuntimeContext = null;", clearPlayer);
            StringAssert.Contains("_survivalSystem = null;", clearPlayer);
            StringAssert.Contains("_survivalDeathSignalSourceId = 0u;", clearPlayer);
            StringAssert.Contains("_lastSurvivalDeathSignalSequence = 0;", clearPlayer);
            StringAssert.Contains("uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);", refreshSurvival);
            StringAssert.Contains("_survivalDeathSignalSourceId = sourceId;", refreshSurvival);
            StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)", refreshSurvival);
            StringAssert.Contains("RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))", resolveSource);
        }

        [Test]
        public void DiskFallbackWriteUsesWriteThroughAndFinalLengthCheck()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethodBody(source, "private void WriteDiskFallback(NativeArray<byte> payload, int byteCount)");
            string lengthHelper = ExtractMethodBody(source, "private static bool TryGetFallbackFileLength(string path, out long bytes)");

            StringAssert.Contains("new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)", method);
            StringAssert.Contains("stream.Flush(true);", method);
            StringAssert.Contains("stream.Length != byteCount", method);
            StringAssert.Contains("File.Move(tmpPath, finalPath);", method);
            StringAssert.Contains("TryGetFallbackFileLength(finalPath, out long finalBytes)", method);
            StringAssert.Contains("finalBytes != byteCount", method);
            StringAssert.Contains("TryDeleteReplayFile(finalPath);", method);
            StringAssert.Contains("TryDeleteTempFallbackFile(tmpPath);", method);
            Assert.IsTrue(ContainsTokensInOrder(
                method,
                "FileOptions.WriteThrough",
                "stream.Write(AsReadOnlySpan(payload, byteCount));",
                "stream.Flush(true);",
                "stream.Length != byteCount",
                "File.Move(tmpPath, finalPath);",
                "TryGetFallbackFileLength(finalPath, out long finalBytes)",
                "finalBytes != byteCount",
                "TryDeleteReplayFile(finalPath);",
                "TryDeleteTempFallbackFile(tmpPath);"));

            StringAssert.Contains("bytes = new FileInfo(path).Length;", lengthHelper);
            StringAssert.Contains("return bytes >= 0L;", lengthHelper);
            StringAssert.Contains("catch", lengthHelper);
            StringAssert.Contains("return false;", lengthHelper);
        }

        [Test]
        public void WorkerIngressAcceptanceWaitsForSuccessfulWorkerStart()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string rebind = ExtractMethodBody(source, "private void RebindDataVault(IDataVault nextVault, IDataVault previousVault = null)");
            string startWorker = ExtractMethodBody(source, "private bool StartWorker()");
            string releaseFailure = ExtractMethodBody(source, "private void ReleaseWorkerStorageAfterStartFailure()");
            string disposeSignal = ExtractMethodBody(source, "private void DisposeWorkerSignalNoThrow()");
            string stopWorker = ExtractMethodBody(source, "private bool StopWorker()");
            string joinWorker = ExtractMethodBody(source, "private bool TryJoinWorkerNoThrow(Thread thread)");
            string signalWorker = ExtractMethodBody(source, "private bool SignalWorkerNoThrow()");
            string releaseVaultHandle = ExtractMethodBody(source, "private void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)");
            string unlockWorkerVault = ExtractMethodBody(source, "private void UnlockWorkerVaultBuffers()");

            StringAssert.Contains("if (_storageReady && StartWorker())", onEnable);
            Assert.That(
                onEnable.IndexOf("StartWorker()", StringComparison.Ordinal),
                Is.LessThan(onEnable.IndexOf("Volatile.Write(ref _acceptingIngress, 1);", StringComparison.Ordinal)));

            StringAssert.Contains("if (StartWorker())", rebind);
            StringAssert.Contains("ReleaseWorkerStorageAfterStartFailure();", rebind);

            StringAssert.Contains("workerThread.Start();", startWorker);
            StringAssert.Contains("return true;", startWorker);
            StringAssert.Contains("return false;", startWorker);
            StringAssert.Contains("catch (Exception)", startWorker);
            StringAssert.Contains("SetWorkerFlag(WorkerFlagFaulted);", startWorker);

            StringAssert.Contains("Volatile.Write(ref _acceptingIngress, 0);", releaseFailure);
            StringAssert.Contains("_storageReady = false;", releaseFailure);
            StringAssert.Contains("ReleaseVaultHandles(_workerStorageVault ?? _dataVault);", releaseFailure);
            StringAssert.Contains("DisposeWorkerSignalNoThrow();", releaseFailure);
            StringAssert.Contains("_flushSignal.Dispose();", disposeSignal);
            StringAssert.Contains("catch (Exception)", disposeSignal);
            StringAssert.Contains("_flushSignal = null;", disposeSignal);
            Assert.AreEqual(1, CountOccurrences(source, "_flushSignal.Dispose();"));

            Assert.AreEqual(0, CountOccurrences(source, "_flushSignal.Set();"));
            StringAssert.Contains("signal.Set();", signalWorker);
            StringAssert.Contains("catch (Exception)", signalWorker);
            StringAssert.Contains("SetWorkerFlag(WorkerFlagFaulted);", signalWorker);

            StringAssert.Contains("TryJoinWorkerNoThrow(thread);", stopWorker);
            StringAssert.Contains("thread.Join(WorkerJoinMilliseconds);", joinWorker);
            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", joinWorker);
            StringAssert.Contains("return !thread.IsAlive;", joinWorker);
            StringAssert.Contains("catch (Exception)", joinWorker);
            StringAssert.Contains("SetWorkerFlag(WorkerFlagFaulted);", joinWorker);

            StringAssert.Contains("vault.ReleaseBuffer(in handle);", releaseVaultHandle);
            StringAssert.Contains("catch (Exception)", releaseVaultHandle);
            StringAssert.Contains("finally", releaseVaultHandle);
            StringAssert.Contains("handle = default;", releaseVaultHandle);
            StringAssert.Contains("vault.ReleaseMutationGuard(WorkerVaultMutationGuardMask);", unlockWorkerVault);
            StringAssert.Contains("IDataVault vault = _workerBufferGuardVault ?? _workerStorageVault ?? _dataVault;", unlockWorkerVault);
            StringAssert.Contains("_workerBufferGuardVault = null;", unlockWorkerVault);
            StringAssert.Contains("catch (Exception)", unlockWorkerVault);
            StringAssert.Contains("finally", unlockWorkerVault);
            StringAssert.Contains("_workerBuffersLocked = false;", unlockWorkerVault);
        }

        [Test]
        public void DataVaultRebindReleasesExporterWorkerStorageThroughOwningVault()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string callback = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string acquire = ExtractMethodBody(source, "private bool TryAcquireVaultStorage()");
            string releaseHandles = ExtractMethodBody(source, "private void ReleaseVaultHandles(IDataVault releaseVaultFallback = null)");
            string rebind = ExtractMethodBody(source, "private void RebindDataVault(IDataVault nextVault, IDataVault previousVault = null)");
            string lockWorkerVault = ExtractMethodBody(source, "private bool LockWorkerVaultBuffers()");
            string unlockWorkerVault = ExtractMethodBody(source, "private void UnlockWorkerVaultBuffers()");

            StringAssert.Contains("private IDataVault _workerStorageVault;", source);
            StringAssert.Contains("private IDataVault _workerBufferGuardVault;", source);
            StringAssert.Contains("RebindDataVault(currentService as IDataVault, previousService as IDataVault);", callback);
            StringAssert.Contains("_workerStorageVault = vault;", acquire);
            StringAssert.Contains("ReleaseVaultHandles(vault);", acquire);
            StringAssert.Contains("IDataVault vault = _workerStorageVault ?? releaseVaultFallback ?? _dataVault;", releaseHandles);
            StringAssert.Contains("_workerStorageVault = null;", releaseHandles);
            StringAssert.Contains("ReleaseVaultHandles(_workerStorageVault ?? _dataVault ?? previousVault);", rebind);
            StringAssert.Contains("_workerBufferGuardVault = vault;", lockWorkerVault);
            StringAssert.Contains("IDataVault vault = _workerBufferGuardVault ?? _workerStorageVault ?? _dataVault;", unlockWorkerVault);
            StringAssert.Contains("_workerBufferGuardVault = null;", unlockWorkerVault);
            StringAssert.DoesNotContain("IDataVault vault = _dataVault;", unlockWorkerVault);
        }

        [Test]
        public void PendingBlackBoxDumpUsesRuntimeTimestampPathAndCountsDoubleWriteFailure()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethodBody(source, "private void TryWritePendingBlackBoxDump()");

            StringAssert.Contains("BuildAnalyticsCrashDumpPath(DateTime.UtcNow.Ticks)", method);
            StringAssert.Contains("bool wroteTimestampedDump = Hecton8.Core.NativeFaultDumpWriter.TryWriteAll", method);
            StringAssert.Contains("bool wroteLatestDump = Hecton8.Core.NativeFaultDumpWriter.TryWriteAll", method);
            StringAssert.Contains("Docs/AgentLogs/Dump_ANALYTICS_CRASH.bin", method);
            StringAssert.Contains("if (!wroteTimestampedDump && !wroteLatestDump)", method);
            StringAssert.Contains("Interlocked.Increment(ref _workerFaultCount);", method);
            StringAssert.Contains("SetWorkerFlag(WorkerFlagFaulted);", method);
            StringAssert.DoesNotContain("Dump_SHINOBU_160.bin", method);
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

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int braceStart = source.IndexOf('{', signatureIndex);
            Assert.Greater(braceStart, signatureIndex, signature);

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            Assert.Fail("Could not find method body for " + signature);
            return string.Empty;
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }
    }
}
