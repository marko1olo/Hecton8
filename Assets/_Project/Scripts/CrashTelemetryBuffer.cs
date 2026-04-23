#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Captures a bounded stream of low-cost runtime telemetry and exports a binary snapshot on fault conditions.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9500)] // Runs after GameTickManager singleton bootstrap and before most gameplay systems.
    public sealed class CrashTelemetryBuffer : MonoBehaviour, ITickable, IFixedTickable
    {
        private const int LogicalRetentionEntries = 300;
        private const int PhysicalRingCapacity = 512;
        private const int PhysicalRingMask = PhysicalRingCapacity - 1;
        private const int ExportSnapshotEntries = 50;
        private const int ExportCooldownFrames = 30;
        private const int DebugLogEntrySizeBytes = 64;
        private const int CrashExportHeaderSizeBytes = 16;
        private const int ExportScratchSizeBytes = CrashExportHeaderSizeBytes + (ExportSnapshotEntries * DebugLogEntrySizeBytes);
        private const int ExportStateIdle = 0;
        private const int ExportStateQueued = 1;
        private const float PlayerResolveCooldownSeconds = 1f;
        private const float SevereFrameTimeSeconds = 0.025f;
        private const float MaximumTrackedWorldMagnitude = 1000000f;
        private const float MaximumReservedMemoryMb = 4096f;
        private const ulong BinaryMagic = 0x00384E4F54434548ul; // "HECTON8\0" in little-endian byte order.
        private const string ExportFilePrefix = "crash_";
        private const string ExportFileExtension = ".hbin";
        private const string ManifestFileExtension = ".json";
        private const string ExportTimestampFormat = "yyyyMMdd_HHmmss_fff";

        private static readonly WaitCallback _backgroundExportCallback = ExecuteBackgroundExport;

        [Flags]
        private enum ErrorBits : uint
        {
            None = 0u,
            MissingPlayer = 1u << 0,
            NonFiniteDeltaTime = 1u << 1,
            NonFinitePlayerPosition = 1u << 2,
            FrameBudgetExceeded = 1u << 3,
            ReservedMemoryExceeded = 1u << 4,
            ExceptionLogged = 1u << 5,
            ErrorLogged = 1u << 6,
            OutOfBoundsPlayerPosition = 1u << 7,
        }

        [Flags]
        private enum SystemBits : uint
        {
            None = 0u,
            Physics = 1u << 0,
            Voxel = 1u << 1,
            AI = 1u << 2,
            Fluid = 1u << 3,
        }

        private enum ExportReason : uint
        {
            ErrorFlags = 1u,
            UnityException = 2u,
            UnityError = 3u,
            ApplicationQuit = 4u,
        }

        [StructLayout(LayoutKind.Sequential, Size = CrashExportHeaderSizeBytes)]
        private struct CrashExportHeader
        {
            public ulong Magic;
            public uint EntryCount;
            public uint StructSizeBytes;
        }

        [StructLayout(LayoutKind.Sequential, Size = DebugLogEntrySizeBytes)]
        private struct DebugLogEntry
        {
            public uint FrameIndex;
            public uint SystemMask;
            public float DeltaTime;
            public float FixedDeltaTime;
            public float GpuFrameTime;
            public float MemoryUsedMb;
            public float3 PlayerPos;
            public uint ActiveChunkCount;
            public uint BoidAgentCount;
            public uint ErrorFlags;
            public uint ExportReason;
            public uint Reserved0;
            public uint Reserved1;
            public uint Reserved2;
        }

        private NativeArray<DebugLogEntry> _ringBuffer;
        private NativeArray<DebugLogEntry> _exportSnapshot;
        private Transform _playerTransform;
        private float _playerResolveCooldown;
        private float _lastFixedDeltaTime;
        private long _writeCursor;
        private uint _stickyErrorFlags;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private int _lastExportFrame = int.MinValue;
        private int _threadedFaultFlags;
        private int _exportState;
        private int _pendingExportBytes;
        private string _pendingExportPath;
        private string _pendingManifestPath;
        private string _pendingManifestJson;

        // COLD ALLOC: FrameTiming[1] - reusable GPU timing sample buffer - owner: CrashTelemetryBuffer
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];

        // COLD ALLOC: byte[3216] - binary export scratch for 16B header + 50 x 64B entries - owner: CrashTelemetryBuffer
        private readonly byte[] _exportScratch = new byte[ExportScratchSizeBytes];

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only view model for the latest retained crash telemetry frames.
        /// </summary>
        public readonly struct EditorSnapshotEntry
        {
            public readonly uint FrameIndex;
            public readonly uint SystemMask;
            public readonly float DeltaTime;
            public readonly float FixedDeltaTime;
            public readonly float GpuFrameTime;
            public readonly float MemoryUsedMb;
            public readonly Vector3 PlayerPosition;
            public readonly uint ActiveChunkCount;
            public readonly uint BoidAgentCount;
            public readonly uint ErrorFlags;
            public readonly uint ExportReason;

            public EditorSnapshotEntry(
                uint frameIndex,
                uint systemMask,
                float deltaTime,
                float fixedDeltaTime,
                float gpuFrameTime,
                float memoryUsedMb,
                Vector3 playerPosition,
                uint activeChunkCount,
                uint boidAgentCount,
                uint errorFlags,
                uint exportReason)
            {
                FrameIndex = frameIndex;
                SystemMask = systemMask;
                DeltaTime = deltaTime;
                FixedDeltaTime = fixedDeltaTime;
                GpuFrameTime = gpuFrameTime;
                MemoryUsedMb = memoryUsedMb;
                PlayerPosition = playerPosition;
                ActiveChunkCount = activeChunkCount;
                BoidAgentCount = boidAgentCount;
                ErrorFlags = errorFlags;
                ExportReason = exportReason;
            }
        }
#endif

        /// <summary>
        /// Returns true when the telemetry ring buffer is initialized.
        /// </summary>
        public bool IsInitialized => _ringBuffer.IsCreated;

#if UNITY_EDITOR
        /// <summary>
        /// Copies the latest retained crash telemetry rows into an editor-owned destination list.
        /// </summary>
        /// <param name="destination">Editor-owned destination list.</param>
        /// <returns>Copied frame count.</returns>
        public int CopyEditorSnapshot(System.Collections.Generic.List<EditorSnapshotEntry> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            if (!_ringBuffer.IsCreated)
                return 0;

            long committedEntries = math.min(_writeCursor, ExportSnapshotEntries);
            if (committedEntries <= 0)
                return 0;

            long startCursor = _writeCursor - committedEntries;
            for (int i = 0; i < committedEntries; i++)
            {
                int ringIndex = (int)(startCursor + i) & PhysicalRingMask;
                DebugLogEntry entry = _ringBuffer[ringIndex];
                destination.Add(new EditorSnapshotEntry(
                    entry.FrameIndex,
                    entry.SystemMask,
                    entry.DeltaTime,
                    entry.FixedDeltaTime,
                    entry.GpuFrameTime,
                    entry.MemoryUsedMb,
                    new Vector3(entry.PlayerPos.x, entry.PlayerPos.y, entry.PlayerPos.z),
                    entry.ActiveChunkCount,
                    entry.BoidAgentCount,
                    entry.ErrorFlags,
                    entry.ExportReason));
            }

            return destination.Count;
        }
#endif

        private void Awake()
        {
            InitializeBuffers();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnEnable()
        {
            Subscribe();
            TryRegister();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregister();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            TryUnregister();
            DisposeBuffers();
        }

        private void OnApplicationQuit()
        {
            if (_stickyErrorFlags != 0u)
                TryExportSnapshot(ExportReason.ApplicationQuit, _stickyErrorFlags, writeSynchronously: true);

            DisposeBuffers();
        }

        /// <summary>
        /// Records one telemetry entry on the shared game tick.
        /// </summary>
        /// <param name="dt">Frame delta passed by <see cref="GameTickManager"/>.</param>
        public void Tick(float dt)
        {
            if (!_ringBuffer.IsCreated)
                return;

            using (ProfilerRegistry.TelemetryWrite.Auto())
            {
                ResolvePlayerTransform(dt);

                FrameTimingManager.CaptureFrameTimings();
                float gpuFrameTime = SampleGpuFrameTimeMs();
                float reservedMemoryMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
                float3 playerPos = SamplePlayerPosition(out bool hasPlayer);
                uint systemMask = SampleSystemMask();
                uint activeChunkCount = SampleActiveChunkCount();
                uint boidAgentCount = 0u;
                uint errorFlags = BuildErrorFlags(dt, reservedMemoryMb, playerPos, hasPlayer);
                uint threadedFaultFlags = unchecked((uint)Interlocked.Exchange(ref _threadedFaultFlags, 0));
                if (threadedFaultFlags != 0u)
                    errorFlags |= threadedFaultFlags;

                uint frameIndex = unchecked((uint)Time.frameCount);
                int writeIndex = Time.frameCount & PhysicalRingMask;

                DebugLogEntry entry = default;
                entry.FrameIndex = frameIndex;
                entry.SystemMask = systemMask;
                entry.DeltaTime = dt;
                entry.FixedDeltaTime = _lastFixedDeltaTime;
                entry.GpuFrameTime = gpuFrameTime;
                entry.MemoryUsedMb = reservedMemoryMb;
                entry.PlayerPos = playerPos;
                entry.ActiveChunkCount = activeChunkCount;
                entry.BoidAgentCount = boidAgentCount;
                entry.ErrorFlags = errorFlags;
                _ringBuffer[writeIndex] = entry;

                _writeCursor++;
                if (errorFlags != 0u)
                {
                    _stickyErrorFlags |= errorFlags;
                    TryExportSnapshot(ExportReason.ErrorFlags, errorFlags, writeSynchronously: false);
                }
            }
        }

        /// <summary>
        /// Caches the fixed-step delta so it can be emitted alongside frame telemetry.
        /// </summary>
        /// <param name="fdt">Fixed delta passed by <see cref="GameTickManager"/>.</param>
        public void FixedTick(float fdt)
        {
            _lastFixedDeltaTime = fdt;
        }

        private void InitializeBuffers()
        {
            if (_ringBuffer.IsCreated)
                return;

            if (!UnsafeUtility.IsBlittable<DebugLogEntry>() ||
                UnsafeUtility.SizeOf<CrashExportHeader>() != CrashExportHeaderSizeBytes ||
                UnsafeUtility.SizeOf<DebugLogEntry>() != DebugLogEntrySizeBytes)
            {
                enabled = false;
                Debug.LogError("CrashTelemetryBuffer requires a blittable 16-byte header and a blittable 64-byte DebugLogEntry.");
                return;
            }

            // COLD ALLOC: NativeArray<DebugLogEntry>[512] - power-of-two backing store for lockless telemetry ring buffer - owner: CrashTelemetryBuffer
            _ringBuffer = new NativeArray<DebugLogEntry>(PhysicalRingCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // COLD ALLOC: NativeArray<DebugLogEntry>[50] - pre-crash binary export snapshot staging buffer - owner: CrashTelemetryBuffer
            _exportSnapshot = new NativeArray<DebugLogEntry>(ExportSnapshotEntries, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void DisposeBuffers()
        {
            if (_ringBuffer.IsCreated)
                _ringBuffer.Dispose();

            if (_exportSnapshot.IsCreated)
                _exportSnapshot.Dispose();
        }

        private void Subscribe()
        {
            Application.logMessageReceived += HandleLogMessageReceived;
            Application.logMessageReceivedThreaded += HandleLogMessageReceivedThreaded;
        }

        private void Unsubscribe()
        {
            Application.logMessageReceived -= HandleLogMessageReceived;
            Application.logMessageReceivedThreaded -= HandleLogMessageReceivedThreaded;
        }

        private void TryRegister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredFixedTick)
            {
                tickManager.Register((IFixedTickable)this);
                _registeredFixedTick = true;
            }
        }

        private void TryUnregister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
            {
                _registeredTick = false;
                _registeredFixedTick = false;
                return;
            }

            if (_registeredTick)
            {
                tickManager.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                tickManager.Unregister((IFixedTickable)this);
                _registeredFixedTick = false;
            }
        }

        private void ResolvePlayerTransform(float dt)
        {
            if (_playerTransform != null)
                return;

            _playerResolveCooldown -= dt;
            if (_playerResolveCooldown > 0f)
                return;

            _playerResolveCooldown = PlayerResolveCooldownSeconds;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _playerTransform = playerTransform;
        }

        private static uint SampleSystemMask()
        {
            uint systemMask = 0u;
            if (HectonFluidEngine.Instance != null)
            {
                systemMask |= (uint)SystemBits.Physics;
                systemMask |= (uint)SystemBits.Fluid;
            }

            if (HectonVoxelEngine.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.Voxel;

            if (HectonDirectorAI.ActiveRuntimeInstance != null)
                systemMask |= (uint)SystemBits.AI;

            return systemMask;
        }

        private static uint SampleActiveChunkCount()
        {
            HectonVoxelEngine voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;
            return voxelEngine != null
                ? unchecked((uint)Mathf.Max(0, voxelEngine.ActiveVolumeCount))
                : 0u;
        }

        private float3 SamplePlayerPosition(out bool hasPlayer)
        {
            if (_playerTransform == null)
            {
                hasPlayer = false;
                return float3.zero;
            }

            hasPlayer = true;
            Vector3 runtimePosition = _playerTransform.position;
            return ToAbsoluteUniversePosition(runtimePosition);
        }

        private static float3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            Vector3 bridgeUniversePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            Vector3 absolutePosition = bridgeUniversePosition != runtimePosition
                ? bridgeUniversePosition
                : HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            return new float3(absolutePosition.x, absolutePosition.y, absolutePosition.z);
        }

        private uint BuildErrorFlags(float dt, float reservedMemoryMb, float3 playerPos, bool hasPlayer)
        {
            uint errorFlags = 0u;

            if (!hasPlayer)
                errorFlags |= (uint)ErrorBits.MissingPlayer;

            if (!math.isfinite(dt) || dt < 0f)
                errorFlags |= (uint)ErrorBits.NonFiniteDeltaTime;

            if (!math.all(math.isfinite(playerPos)))
                errorFlags |= (uint)ErrorBits.NonFinitePlayerPosition;

            if (dt >= SevereFrameTimeSeconds)
                errorFlags |= (uint)ErrorBits.FrameBudgetExceeded;

            if (reservedMemoryMb >= MaximumReservedMemoryMb)
                errorFlags |= (uint)ErrorBits.ReservedMemoryExceeded;

            if (math.lengthsq(playerPos) > (MaximumTrackedWorldMagnitude * MaximumTrackedWorldMagnitude))
                errorFlags |= (uint)ErrorBits.OutOfBoundsPlayerPosition;

            return errorFlags;
        }

        private float SampleGpuFrameTimeMs()
        {
            uint sampleCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            if (sampleCount == 0u)
                return 0f;

            return (float)_frameTimingScratch[0].gpuFrameTime;
        }

        private void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            uint faultFlags = 0u;
            ExportReason exportReason = ExportReason.ErrorFlags;

            if (type == LogType.Exception)
            {
                faultFlags = (uint)ErrorBits.ExceptionLogged;
                exportReason = ExportReason.UnityException;
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                faultFlags = (uint)ErrorBits.ErrorLogged;
                exportReason = ExportReason.UnityError;
            }

            if (faultFlags == 0u)
                return;

            _stickyErrorFlags |= faultFlags;
            Interlocked.Exchange(ref _threadedFaultFlags, 0);
            TryExportSnapshot(exportReason, faultFlags, writeSynchronously: false);
        }

        private void HandleLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                OrThreadedFaultFlags((int)ErrorBits.ExceptionLogged);
            }
            else if (type == LogType.Error || type == LogType.Assert)
            {
                OrThreadedFaultFlags((int)ErrorBits.ErrorLogged);
            }
        }

        private void OrThreadedFaultFlags(int flags)
        {
            int snapshot;
            int combined;
            do
            {
                snapshot = _threadedFaultFlags;
                combined = snapshot | flags;
            }
            while (Interlocked.CompareExchange(ref _threadedFaultFlags, combined, snapshot) != snapshot);
        }

        private void TryExportSnapshot(ExportReason exportReason, uint exportFlags, bool writeSynchronously)
        {
            if (!_ringBuffer.IsCreated)
                return;

            if (Interlocked.CompareExchange(ref _exportState, ExportStateQueued, ExportStateIdle) != ExportStateIdle)
                return;

            bool exportQueued = false;
            try
            {
                int currentFrame = Time.frameCount;
                if (!writeSynchronously && currentFrame - _lastExportFrame < ExportCooldownFrames)
                    return;

                using (ProfilerRegistry.TelemetryExport.Auto())
                {
                    int snapshotCount = SnapshotRecentEntries(exportReason);
                    if (snapshotCount <= 0)
                        return;

                    _lastExportFrame = currentFrame;
                    _pendingExportBytes = BuildExportScratch(snapshotCount);
                    PreparePendingExportMetadata(exportReason, exportFlags, snapshotCount);

                    if (writeSynchronously)
                    {
                        WritePreparedExportToDisk();
                        return;
                    }

                    ThreadPool.UnsafeQueueUserWorkItem(_backgroundExportCallback, this);
                    exportQueued = true;
                }
            }
            finally
            {
                if (!exportQueued)
                    Volatile.Write(ref _exportState, ExportStateIdle);
            }
        }

        private int SnapshotRecentEntries(ExportReason exportReason)
        {
            if (!_ringBuffer.IsCreated || !_exportSnapshot.IsCreated)
                return 0;

            long committedEntries = math.min(_writeCursor, LogicalRetentionEntries);
            long skipNewestEntry = exportReason == ExportReason.ErrorFlags ? 1L : 0L;
            long availableEntries = math.min(ExportSnapshotEntries, committedEntries - skipNewestEntry);
            if (availableEntries <= 0)
                return 0;

            long startCursor = _writeCursor - skipNewestEntry - availableEntries;
            for (int i = 0; i < availableEntries; i++)
            {
                int ringIndex = (int)(startCursor + i) & PhysicalRingMask;
                _exportSnapshot[i] = _ringBuffer[ringIndex];
            }

            return (int)availableEntries;
        }

        private int BuildExportScratch(int snapshotCount)
        {
            unsafe
            {
                CrashExportHeader header = default;
                header.Magic = BinaryMagic;
                header.EntryCount = unchecked((uint)snapshotCount);
                header.StructSizeBytes = DebugLogEntrySizeBytes;

                int entryBytes = snapshotCount * DebugLogEntrySizeBytes;
                int totalBytes = CrashExportHeaderSizeBytes + entryBytes;

                fixed (byte* destination = _exportScratch)
                {
                    UnsafeUtility.MemClear(destination, totalBytes);
                    UnsafeUtility.CopyStructureToPtr(ref header, destination);
                    void* snapshotPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_exportSnapshot);
                    UnsafeUtility.MemCpy(destination + CrashExportHeaderSizeBytes, snapshotPtr, entryBytes);
                }

                return totalBytes;
            }
        }

        private void PreparePendingExportMetadata(ExportReason exportReason, uint exportFlags, int snapshotCount)
        {
            uint triggerFrame = unchecked((uint)Time.frameCount);
            string timestampUtc = DateTime.UtcNow.ToString(ExportTimestampFormat);
            string fileStem = ExportFilePrefix + timestampUtc + "_f" + triggerFrame;
            _pendingExportPath = Path.Combine(Application.persistentDataPath, fileStem + ExportFileExtension);
            _pendingManifestPath = Path.Combine(Application.persistentDataPath, fileStem + ManifestFileExtension);
            _pendingManifestJson = BuildManifestJson(timestampUtc, triggerFrame, exportReason, exportFlags, snapshotCount);
        }

        private string BuildManifestJson(string timestampUtc, uint triggerFrame, ExportReason exportReason, uint exportFlags, int snapshotCount)
        {
            StringBuilder builder = new StringBuilder(768);
            builder.Append("{\n");
            builder.Append("  \"timestampUtc\":\"").Append(timestampUtc).Append("\",\n");
            builder.Append("  \"triggerFrame\":").Append(triggerFrame).Append(",\n");
            builder.Append("  \"reason\":\"").Append(GetExportReasonName(exportReason)).Append("\",\n");
            builder.Append("  \"errorFlagsHex\":\"0x").Append(exportFlags.ToString("X8")).Append("\",\n");
            builder.Append("  \"entryCount\":").Append(snapshotCount).Append(",\n");
            builder.Append("  \"structSizeBytes\":").Append(DebugLogEntrySizeBytes).Append(",\n");
            builder.Append("  \"logicalRetentionEntries\":").Append(LogicalRetentionEntries).Append(",\n");
            builder.Append("  \"physicalRingCapacity\":").Append(PhysicalRingCapacity).Append(",\n");
            builder.Append("  \"platform\":\"").Append(Application.platform).Append("\",\n");
            builder.Append("  \"unityVersion\":\"").Append(Application.unityVersion).Append("\",\n");
            builder.Append("  \"buildGuid\":\"").Append(Application.buildGUID).Append("\",\n");
            builder.Append("  \"scene\":\"").Append(SceneManager.GetActiveScene().name).Append("\",\n");
            builder.Append("  \"systemFlagsLegend\":{\n");
            builder.Append("    \"0\":\"Physics\",\n");
            builder.Append("    \"1\":\"Voxel\",\n");
            builder.Append("    \"2\":\"AI\",\n");
            builder.Append("    \"3\":\"Fluid\"\n");
            builder.Append("  }\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string GetExportReasonName(ExportReason exportReason)
        {
            switch (exportReason)
            {
                case ExportReason.ErrorFlags:
                    return "ErrorFlags";
                case ExportReason.UnityException:
                    return "UnityException";
                case ExportReason.UnityError:
                    return "UnityError";
                case ExportReason.ApplicationQuit:
                    return "ApplicationQuit";
                default:
                    return "Unknown";
            }
        }

        private static void ExecuteBackgroundExport(object state)
        {
            if (state is CrashTelemetryBuffer crashTelemetryBuffer)
                crashTelemetryBuffer.WritePreparedExportToDisk();
        }

        private void WritePreparedExportToDisk()
        {
            try
            {
                string exportPath = _pendingExportPath;
                int exportBytes = _pendingExportBytes;
                if (!string.IsNullOrEmpty(exportPath) && exportBytes > 0)
                {
                    using (FileStream stream = new FileStream(exportPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(_exportScratch, 0, exportBytes);
                        stream.Flush();
                    }
                }

                if (!string.IsNullOrEmpty(_pendingManifestPath) && !string.IsNullOrEmpty(_pendingManifestJson))
                    File.WriteAllText(_pendingManifestPath, _pendingManifestJson);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                _pendingExportBytes = 0;
                _pendingExportPath = null;
                _pendingManifestPath = null;
                _pendingManifestJson = null;
                Volatile.Write(ref _exportState, ExportStateIdle);
            }
        }
    }
}
#endif
