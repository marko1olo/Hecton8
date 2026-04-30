using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton8.SaveSystem;
using Hecton.Localization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public enum TelemetryEventType : byte
    {
        PlayerDeath = 0,
        BiomeVisited = 1,
        ItemCrafted = 2,
        BootstrapDependencyCycle = 3,
        JobBarrierStall = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TelemetryEvent
    {
        public uint FrameIndex;
        public uint EventType;
        public uint SubjectHash;
        public uint ContextHash;
        public float ScalarValue;
        public float3 WorldPosition;
    }

    public static class GlobalTelemetryBus
    {
        private const int Capacity = 1024;
        private const int BinaryHeaderSizeBytes = 16;
        private const int Version = 1;
        private const uint BinaryMagic = 0x4D4C4554u; // "TELM"
        private const float DrainIntervalSeconds = 60f;
        private const int SnapshotCopyBudgetPerLateFrame = 128;
        private const string ExportFolderName = "Telemetry";
        private const string BinaryExtension = ".tbin";
        private const string JsonExtension = ".json";
        private static readonly WaitCallback _backgroundExportCallback = ExecuteBackgroundExport;

        private static NativeArray<TelemetryEvent> _ringBuffer;
        private static NativeArray<TelemetryEvent> _snapshotBuffer;
        private static byte[] _exportBytes;
        private static int _writeCursor;
        private static float _nextDrainTimeSeconds = DrainIntervalSeconds;
        private static int _exportInFlight;
        private static int _pendingEventCount;
        private static int _pendingByteCount;
        private static string _pendingBinaryPath;
        private static string _pendingJsonPath;
        private static string _pendingTelemetryDirectory;
        private static long _pendingGeneratedUtcTicks;
        private static bool _snapshotInProgress;
        private static int _snapshotStartIndex;
        private static int _snapshotTotalCount;
        private static int _snapshotCopiedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeStaticState();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeStaticState;
            UnityEditor.EditorApplication.quitting -= DisposeStaticState;
            UnityEditor.EditorApplication.quitting += DisposeStaticState;
        }
#endif

        private static void DisposeStaticState()
        {
            if (_ringBuffer.IsCreated)
                _ringBuffer.Dispose();

            if (_snapshotBuffer.IsCreated)
                _snapshotBuffer.Dispose();

            _ringBuffer = default;
            _snapshotBuffer = default;
            _exportBytes = null;
            _writeCursor = 0;
            _nextDrainTimeSeconds = DrainIntervalSeconds;
            _exportInFlight = 0;
            _pendingEventCount = 0;
            _pendingByteCount = 0;
            _pendingBinaryPath = null;
            _pendingJsonPath = null;
            _pendingTelemetryDirectory = null;
            _pendingGeneratedUtcTicks = 0L;
            _snapshotInProgress = false;
            _snapshotStartIndex = 0;
            _snapshotTotalCount = 0;
            _snapshotCopiedCount = 0;
        }

        public static void PublishPlayerDeath(Vector3 worldPosition)
        {
            Publish(TelemetryEventType.PlayerDeath, 0u, 0u, 0f, worldPosition);
        }

        public static void PublishBiomeVisited(string biomeId, int depthTier, float depthMeters)
        {
            Publish(
                TelemetryEventType.BiomeVisited,
                ComputeHash(biomeId),
                unchecked((uint)depthTier),
                depthMeters,
                default);
        }

        public static void PublishItemCrafted(string itemPersistentId)
        {
            Publish(TelemetryEventType.ItemCrafted, ComputeHash(itemPersistentId), 0u, 1f, default);
        }

        public static void PublishBootstrapDependencyCycle(string serviceId, string dependencyId)
        {
            Publish(
                TelemetryEventType.BootstrapDependencyCycle,
                ComputeHash(serviceId),
                ComputeHash(dependencyId),
                0f,
                default);
        }

        public static void PublishJobBarrierStall(string systemName, string phaseName, float stallMilliseconds)
        {
            Publish(
                TelemetryEventType.JobBarrierStall,
                ComputeHash(systemName),
                ComputeHash(phaseName),
                stallMilliseconds,
                default);
        }

        public static void LateFrameUpdate(float unscaledTimeSeconds)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (_snapshotInProgress)
            {
                ContinueSnapshotCopy();
                return;
            }

            if (unscaledTimeSeconds < _nextDrainTimeSeconds)
                return;

            _nextDrainTimeSeconds = unscaledTimeSeconds + DrainIntervalSeconds;
            BeginSnapshotCopy();
        }

        private static void Publish(
            TelemetryEventType eventType,
            uint subjectHash,
            uint contextHash,
            float scalarValue,
            Vector3 worldPosition)
        {
            if (!Application.isPlaying)
                return;

            EnsureInitialized();

            int writeIndex = Interlocked.Increment(ref _writeCursor) - 1;
            int slot = writeIndex % Capacity;
            if (slot < 0)
                slot += Capacity;

            _ringBuffer[slot] = new TelemetryEvent
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                EventType = (uint)eventType,
                SubjectHash = subjectHash,
                ContextHash = contextHash,
                ScalarValue = scalarValue,
                WorldPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z)
            };
        }

        private static void EnsureInitialized()
        {
            if (_ringBuffer.IsCreated)
                return;

            _ringBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] - global telemetry ring buffer - owner: GlobalTelemetryBus
            _snapshotBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] - telemetry export snapshot staging buffer - owner: GlobalTelemetryBus
            _exportBytes = new byte[(Capacity * UnsafeUtility.SizeOf<TelemetryEvent>()) + BinaryHeaderSizeBytes]; // COLD ALLOC: byte[] telemetry export scratch - owner: GlobalTelemetryBus
        }

        private static uint ComputeHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
        }

        private static void BeginSnapshotCopy()
        {
            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return;

            int writeCursor = Volatile.Read(ref _writeCursor);
            int totalWritten = math.min(writeCursor, Capacity);
            if (totalWritten <= 0)
            {
                Interlocked.Exchange(ref _exportInFlight, 0);
                return;
            }

            _snapshotStartIndex = writeCursor - totalWritten;
            _snapshotTotalCount = totalWritten;
            _snapshotCopiedCount = 0;
            _snapshotInProgress = true;
            ContinueSnapshotCopy();
        }

        private static void ContinueSnapshotCopy()
        {
            if (!_snapshotInProgress || !_ringBuffer.IsCreated || !_snapshotBuffer.IsCreated)
                return;

            int remaining = _snapshotTotalCount - _snapshotCopiedCount;
            int copyCount = math.min(remaining, SnapshotCopyBudgetPerLateFrame);
            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = (_snapshotStartIndex + _snapshotCopiedCount + i) % Capacity;
                if (ringIndex < 0)
                    ringIndex += Capacity;

                _snapshotBuffer[_snapshotCopiedCount + i] = _ringBuffer[ringIndex];
            }

            _snapshotCopiedCount += copyCount;
            if (_snapshotCopiedCount < _snapshotTotalCount)
                return;

            CompleteSnapshotCopy();
        }

        private static unsafe void CompleteSnapshotCopy()
        {
            int eventSizeBytes = UnsafeUtility.SizeOf<TelemetryEvent>();
            WriteHeader(_snapshotTotalCount, eventSizeBytes);

            fixed (byte* exportBytesPtr = _exportBytes)
            {
                byte* payloadPtr = exportBytesPtr + BinaryHeaderSizeBytes;
                UnsafeUtility.MemCpy(
                    payloadPtr,
                    _snapshotBuffer.GetUnsafeReadOnlyPtr(),
                    _snapshotTotalCount * eventSizeBytes);
            }

            _pendingTelemetryDirectory = Path.Combine(Application.persistentDataPath, ExportFolderName);
            _pendingGeneratedUtcTicks = DateTime.UtcNow.Ticks;
            _pendingEventCount = _snapshotTotalCount;
            _pendingByteCount = BinaryHeaderSizeBytes + (_snapshotTotalCount * eventSizeBytes);
            _snapshotInProgress = false;
            _snapshotStartIndex = 0;
            _snapshotTotalCount = 0;
            _snapshotCopiedCount = 0;
            ThreadPool.QueueUserWorkItem(_backgroundExportCallback);
        }

        private static unsafe void WriteHeader(int eventCount, int eventSizeBytes)
        {
            fixed (byte* exportBytesPtr = _exportBytes)
            {
                uint* header = (uint*)exportBytesPtr;
                header[0] = BinaryMagic;
                header[1] = Version;
                header[2] = (uint)eventCount;
                header[3] = (uint)eventSizeBytes;
            }
        }

        private static unsafe void ExecuteBackgroundExport(object state)
        {
            try
            {
                string telemetryDirectory = _pendingTelemetryDirectory;
                if (string.IsNullOrEmpty(telemetryDirectory))
                    return;

                Directory.CreateDirectory(telemetryDirectory);
                DateTime generatedUtc = _pendingGeneratedUtcTicks > 0L
                    ? new DateTime(_pendingGeneratedUtcTicks, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                string timestamp = generatedUtc.ToString("yyyyMMdd_HHmmss");
                _pendingBinaryPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{BinaryExtension}");
                _pendingJsonPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{JsonExtension}");

                if (_pendingByteCount > 0 && !string.IsNullOrEmpty(_pendingBinaryPath))
                {
                    fixed (byte* exportPtr = _exportBytes)
                    {
                        AsyncWriteManager.WriteAll(_pendingBinaryPath, exportPtr, _pendingByteCount, out _);
                    }
                }

                if (_pendingEventCount > 0 && !string.IsNullOrEmpty(_pendingJsonPath))
                {
                    StringBuilder builder = new StringBuilder(192);
                    builder.Append("{\"version\":");
                    builder.Append(Version);
                    builder.Append(",\"eventCount\":");
                    builder.Append(_pendingEventCount);
                    builder.Append(",\"binaryFile\":\"");
                    builder.Append(Path.GetFileName(_pendingBinaryPath));
                    builder.Append("\",\"generatedUtc\":\"");
                    builder.Append(DateTime.UtcNow.ToString("O"));
                    builder.Append("\"}");
                    File.WriteAllText(_pendingJsonPath, builder.ToString(), Encoding.UTF8);
                }
            }
            finally
            {
                _pendingEventCount = 0;
                _pendingByteCount = 0;
                _pendingBinaryPath = null;
                _pendingJsonPath = null;
                _pendingTelemetryDirectory = null;
                _pendingGeneratedUtcTicks = 0L;
                Interlocked.Exchange(ref _exportInFlight, 0);
            }
        }
    }
}
