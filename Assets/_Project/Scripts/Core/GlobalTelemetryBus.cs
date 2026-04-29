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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
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
            if (!_ringBuffer.IsCreated || unscaledTimeSeconds < _nextDrainTimeSeconds)
                return;

            _nextDrainTimeSeconds = unscaledTimeSeconds + DrainIntervalSeconds;
            QueueBackgroundExport();
        }

        private static void Publish(
            TelemetryEventType eventType,
            uint subjectHash,
            uint contextHash,
            float scalarValue,
            Vector3 worldPosition)
        {
            EnsureInitialized();

            int writeIndex = Interlocked.Increment(ref _writeCursor) - 1;
            int slot = writeIndex % Capacity;
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

        private static unsafe void QueueBackgroundExport()
        {
            if (Interlocked.CompareExchange(ref _exportInFlight, 1, 0) != 0)
                return;

            int totalWritten = math.min(_writeCursor, Capacity);
            if (totalWritten <= 0)
            {
                Interlocked.Exchange(ref _exportInFlight, 0);
                return;
            }

            int startIndex = _writeCursor - totalWritten;
            for (int i = 0; i < totalWritten; i++)
            {
                int ringIndex = (startIndex + i) % Capacity;
                if (ringIndex < 0)
                    ringIndex += Capacity;

                _snapshotBuffer[i] = _ringBuffer[ringIndex];
            }

            int eventSizeBytes = UnsafeUtility.SizeOf<TelemetryEvent>();
            WriteHeader(totalWritten, eventSizeBytes);

            fixed (byte* exportBytesPtr = _exportBytes)
            {
                byte* payloadPtr = exportBytesPtr + BinaryHeaderSizeBytes;
                UnsafeUtility.MemCpy(
                    payloadPtr,
                    _snapshotBuffer.GetUnsafeReadOnlyPtr(),
                    totalWritten * eventSizeBytes);
            }

            string telemetryDirectory = Path.Combine(Application.persistentDataPath, ExportFolderName);
            Directory.CreateDirectory(telemetryDirectory);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            _pendingBinaryPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{BinaryExtension}");
            _pendingJsonPath = Path.Combine(telemetryDirectory, $"telemetry_{timestamp}{JsonExtension}");
            _pendingEventCount = totalWritten;
            _pendingByteCount = BinaryHeaderSizeBytes + (totalWritten * eventSizeBytes);
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
                Interlocked.Exchange(ref _exportInFlight, 0);
            }
        }
    }
}
