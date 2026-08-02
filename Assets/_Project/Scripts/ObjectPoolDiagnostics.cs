// ============================================================================
// HECTON-8 — ObjectPoolDiagnostics.cs  v1.0
// Real-time diagnostics and profiling for ObjectPoolManager.
//
// PURPOSE:
//   Offer visibility into pool usage: how many objects spawned/despawned,
//   pool utilization rates, peak concurrent usage, memory footprint,
//   and predictive alerts for pool exhaustion.
//
// WHY THIS MATTERS:
//   • ObjectPoolManager can exhaust if pools aren't sized correctly.
//   • Multiple systems compete for same pooled prefabs.
//   • Without diagnostics, you don't know pool health until runtime crash.
//   • This tracks spawn/despawn rates, detects underprovisioning early.
//
// USAGE:
//   // Subscribe to deferred diagnostics during component enable.
//   ObjectPoolDiagnostics.Register(listener);
//   // Unsubscribe during component disable.
//   ObjectPoolDiagnostics.Unregister(listener);
//
// ZERO-GC DESIGN:
//   • PoolStatSnapshot is struct (stack allocation only).
//   • All tracking via int counters and NativeQueue payloads.
//   • Report generation is cold-path only and returns a managed string.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core
{
    public enum PoolDiagnosticsEventType : byte
    {
        Warning = 0,
        Exhausted = 1,
        SpawnRateAlert = 2,
        DataBusDepth = 3,
        DataBusSaturated = 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PoolDiagnosticsEventPayload
    {
        [FieldOffset(0)] public uint PoolHash;
        [FieldOffset(4)] public float MetricValue;
        [FieldOffset(8)] public ushort EventType;
        [FieldOffset(10)] public ushort FlagValue;
        [FieldOffset(12)] private uint _pad0;
    }

    public interface IObjectPoolDiagnosticsListener
    {
        void OnPoolDiagnosticsEvent(in PoolDiagnosticsEventPayload payload);
    }

    /// <summary>
    /// Snapshot of a pool's current statistics.
    /// Struct-based, zero-heap allocation.
    /// </summary>
    public struct PoolStatSnapshot
    {
        /// <summary>
        /// Total spawns (instances borrowed from pool).
        /// </summary>
        public int totalSpawns;

        /// <summary>
        /// Total despawns (instances returned to pool).
        /// </summary>
        public int totalDespawns;

        /// <summary>
        /// Currently active (borrowed) instances.
        /// </summary>
        public int currentActiveCount;

        /// <summary>
        /// Currently available (idle in pool) instances.
        /// </summary>
        public int currentAvailableCount;

        /// <summary>
        /// Peak number of concurrent active instances.
        /// </summary>
        public int peakConcurrentCount;

        /// <summary>
        /// Pool capacity (max size).
        /// </summary>
        public int poolCapacity;

        /// <summary>
        /// Frame when measurements started.
        /// </summary>
        public int measurementStartFrame;

        /// <summary>
        /// Estimated memory footprint (MB) of this pool.
        /// </summary>
        public float estimatedMemoryMB;

        public float UtilizationPercent =>
            poolCapacity > 0 ? (currentActiveCount / (float)poolCapacity) * 100f : 0f;

        public override string ToString()
        {
            return "[PoolStatSnapshot]";
        }
    }

    /// <summary>
    /// Real-time diagnostics and profiling for ObjectPoolManager.
    /// Must be initialized by ObjectPoolManager on Awake.
    /// </summary>
    public static class ObjectPoolDiagnostics
    {
        // ════════════════════════════════════════════════════════════
        //  EVENTS
        // ════════════════════════════════════════════════════════════

        private const int ListenerCapacity = 4;

        private struct ListenerSlot
        {
            public IObjectPoolDiagnosticsListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[4] - pool diagnostics listeners drained on dispatcher LateUpdate - owner: ObjectPoolDiagnostics
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private const int PendingEventCapacity = 4;
        private const int PoolNameSlotCapacity = 32;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint DataBusSaturationNotificationMissWarningHash =
            unchecked((uint)LocHash.Compute("ObjectPoolDiagnostics.DataBusSaturationNotificationMiss"));
        private static readonly uint DataBusSaturationNotificationContextHash =
            unchecked((uint)LocHash.Compute("ObjectPoolDiagnostics.DataBusSaturationNotification"));

        private struct PoolNameSlot
        {
            public uint PoolHash;
            public string PoolName;
            public byte IsValid;

            public void Clear()
            {
                PoolHash = 0u;
                PoolName = null;
                IsValid = 0;
            }
        }

        // COLD ALLOC: PoolNameSlot[32] - fixed pool-name sidecar keyed by FNV-1a hash; no dictionary growth - owner: ObjectPoolDiagnostics
        private static readonly PoolNameSlot[] _poolNamesByHash = new PoolNameSlot[PoolNameSlotCapacity];
        private static NativeQueue<PoolDiagnosticsEventPayload> _pendingEvents;
        private static NativeQueue<PoolDiagnosticsEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _listenerCount;
        private static int _poolNameSlotCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _dataBusSaturationNotificationMissCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DataBusSaturationNotificationMissCount => _dataBusSaturationNotificationMissCount;

        // ════════════════════════════════════════════════════════════
        //  INTERNAL STATE
        // ════════════════════════════════════════════════════════════

        private sealed class PoolMetrics
        {
            public int totalSpawns;
            public int totalDespawns;
            public int peakConcurrentCount;
            public int lastSpawnCount;
            public int lastDespawnCount;
            public int lastMeasurementFrame;

            // For spawn rate acceleration detection
            public float avgSpawnRateLastSecond = 0f;
            public float avgSpawnRateLastFrame;
            public bool wasAccelerating;
        }

        private static readonly Dictionary<string, PoolMetrics> _poolMetrics =
            new Dictionary<string, PoolMetrics>(32);

        private static int _lastDiagnosticsFrame = -1;
        private static int _lastDataBusSaturationWarningFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            ClearPoolNameSlots();
            _poolMetrics.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastDiagnosticsFrame = -1;
            _lastDataBusSaturationWarningFrame = -1;
            _dataBusSaturationNotificationMissCount = 0;
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        public static void Register(IObjectPoolDiagnosticsListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        public static void Unregister(IObjectPoolDiagnosticsListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        public static void FlushPending()
        {
            // L19 hop2 LIVE: ACCESS_VIOLATION in NativeQueue.IsEmpty / UnsafeUntypedQueue.IsEmpty
            // during LateFrameTick FlushPending after WORLDDRIVER begin. Under batchmode the
            // diagnostics queues can be half-disposed (domain churn / sentinel release race)
            // while IsCreated still reports true. Hop2 validates input/hop, not pool telemetry —
            // soft-disable the flush path under batchmode so native queue ops never run.
            if (Application.isBatchMode)
            {
                SoftDropDiagnosticsQueuesForBatchMode();
                return;
            }

            if (!_pendingEvents.IsCreated || _listenerCount <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget > 0 && QueueIsCreatedAndNonEmpty(ref _pendingEvents))
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryDequeueSafe(ref _pendingEvents, out PoolDiagnosticsEventPayload payload))
                {
                    _pendingEventCount = 0;
                    return;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
                scanBudget--;
                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IObjectPoolDiagnosticsListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnPoolDiagnosticsEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (!QueueIsCreatedAndNonEmpty(ref _pendingEvents))
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }


        public static bool TryResolvePoolName(uint poolHash, out string poolName)
        {
            for (int i = 0; i < _poolNameSlotCount; i++)
            {
                if (_poolNamesByHash[i].IsValid == 0 || _poolNamesByHash[i].PoolHash != poolHash)
                    continue;

                poolName = _poolNamesByHash[i].PoolName;
                return !string.IsNullOrEmpty(poolName);
            }

            poolName = null;
            return false;
        }

        [Obsolete("Use TryPublishDataBusDepth(uint,int) so bounded diagnostics enqueue refusal is visible.", true)]
        public static void PublishDataBusDepth(uint queueHash, int pendingCount)
        {
            TryPublishDataBusDepth(queueHash, pendingCount);
        }

        public static bool TryPublishDataBusDepth(uint queueHash, int pendingCount)
        {
            if (queueHash == 0u || pendingCount <= 0)
                return false;

            // L19 hop2: never allocate/touch native diagnostics queues under batchmode.
            if (Application.isBatchMode)
                return false;

            EnsureInitialized();

            bool saturated = pendingCount > 128;
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                if (saturated)
                    PublishDataBusSaturationWarning();
                return false;
            }

            PoolDiagnosticsEventPayload payload = new PoolDiagnosticsEventPayload
            {
                PoolHash = queueHash,
                MetricValue = pendingCount,
                EventType = (ushort)(saturated ? PoolDiagnosticsEventType.DataBusSaturated : PoolDiagnosticsEventType.DataBusDepth),
                FlagValue = (ushort)(saturated ? 1 : 0)
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
            }
            else
            {
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }

            if (saturated)
                PublishDataBusSaturationWarning();

            return true;
        }

        /// <summary>
        /// Register a pool for diagnostics tracking.
        /// Called by ObjectPoolManager.Spawn when pool is first created.
        /// </summary>
        public static void RegisterPool(string poolName, int initialCapacity)
        {
            if (!_poolMetrics.ContainsKey(poolName))
            {
                RegisterPoolName(poolName);
                _poolMetrics[poolName] = new PoolMetrics
                {
                    lastMeasurementFrame = SystemDispatcher.CurrentFrameIndex
                };
            }
        }

        /// <summary>
        /// Record a spawn event.
        /// Called by ObjectPoolManager.Spawn.
        /// </summary>
        public static void RecordSpawn(string poolName)
        {
            if (_poolMetrics.TryGetValue(poolName, out var metrics))
            {
                metrics.totalSpawns++;
                int currentActive = metrics.totalSpawns - metrics.totalDespawns;
                metrics.peakConcurrentCount = Mathf.Max(metrics.peakConcurrentCount, currentActive);
            }
        }

        /// <summary>
        /// Record a despawn event.
        /// Called by ObjectPoolManager.Despawn.
        /// </summary>
        public static void RecordDespawn(string poolName)
        {
            if (_poolMetrics.TryGetValue(poolName, out var metrics))
                metrics.totalDespawns++;
        }

        /// <summary>
        /// Get current statistics for a named pool.
        /// Must be paired with ObjectPoolManager capacity tracking.
        /// </summary>
        public static PoolStatSnapshot GetPoolStats(string poolName)
        {
            if (!_poolMetrics.TryGetValue(poolName, out var metrics))
                return default;

            int currentActive = metrics.totalSpawns - metrics.totalDespawns;
            return new PoolStatSnapshot
            {
                totalSpawns = metrics.totalSpawns,
                totalDespawns = metrics.totalDespawns,
                currentActiveCount = currentActive,
                peakConcurrentCount = metrics.peakConcurrentCount,
                measurementStartFrame = metrics.lastMeasurementFrame
            };
        }

        /// <summary>
        /// Poll all pools for warnings and alerts.
        /// Call from ObjectPoolManager.LateUpdate (or external monitor).
        /// </summary>
        public static void PollPoolHealth(Func<string, int, int> getPoolCapacity)
        {
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDiagnosticsFrame == currentFrame)
                return; // Already polled this frame

            _lastDiagnosticsFrame = currentFrame;

            Dictionary<string, PoolMetrics>.Enumerator metricsEnumerator = _poolMetrics.GetEnumerator();
            while (metricsEnumerator.MoveNext())
            {
                string poolName = metricsEnumerator.Current.Key;
                PoolMetrics metrics = metricsEnumerator.Current.Value;

                int capacity = getPoolCapacity(poolName, 0);
                int currentActive = metrics.totalSpawns - metrics.totalDespawns;

                // Warn if utilization > 80%
                if (capacity > 0)
                {
                    float utilization = (currentActive / (float)capacity) * 100f;
                    if (utilization > 80f)
                        Publish(poolName, PoolDiagnosticsEventType.Warning, utilization, 0u);

                    // Alert if exhausted
                    if (currentActive >= capacity)
                        Publish(poolName, PoolDiagnosticsEventType.Exhausted, utilization, 0u);
                }

                // Spawn rate acceleration detection
                int spawnsSinceLastFrame = metrics.totalSpawns - metrics.lastSpawnCount;
                if (metrics.avgSpawnRateLastFrame > 0 && spawnsSinceLastFrame > metrics.avgSpawnRateLastFrame * 1.5f)
                {
                    bool isAccelerating = !metrics.wasAccelerating;
                    if (isAccelerating)
                    {
                        Publish(poolName, PoolDiagnosticsEventType.SpawnRateAlert, spawnsSinceLastFrame, 1u);
                        metrics.wasAccelerating = true;
                    }
                }
                else
                {
                    metrics.wasAccelerating = false;
                }

                metrics.avgSpawnRateLastFrame = spawnsSinceLastFrame;
                metrics.lastSpawnCount = metrics.totalSpawns;
                metrics.lastDespawnCount = metrics.totalDespawns;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  REPORTING — Zero-allocation via pre-allocated StringBuilder
        // ════════════════════════════════════════════════════════════

        public static string GenerateReport()
        {
            // Leverage StringBuilderPool for zero allocation
            var scope = StringBuilderScope.Get();
            try
            {
                var sb = scope.Value;
                sb.AppendLine("═════════════════════════════════════════════════════════");
                sb.AppendLine("OBJECT POOL DIAGNOSTICS REPORT");
                sb.Append("Frame: ");
                sb.Append(SystemDispatcher.CurrentFrameIndex);
                sb.Append(" | Time: ");
                sb.Append(UnityEngine.Time.realtimeSinceStartup);
                sb.AppendLine("s");
                sb.AppendLine("═════════════════════════════════════════════════════════");

                int totalPoolsActive = 0;
                int totalActiveObjects = 0;
                int totalSpawned = 0;

                Dictionary<string, PoolMetrics>.Enumerator metricsEnumerator = _poolMetrics.GetEnumerator();
                while (metricsEnumerator.MoveNext())
                {
                    string poolName = metricsEnumerator.Current.Key;
                    PoolMetrics metrics = metricsEnumerator.Current.Value;

                    int currentActive = metrics.totalSpawns - metrics.totalDespawns;
                    totalPoolsActive++;
                    totalActiveObjects += currentActive;
                    totalSpawned += metrics.totalSpawns;

                    sb.Append("\n  Pool: ");
                    sb.Append(poolName);
                    sb.Append('\n');
                    sb.Append("    Active: ");
                    sb.Append(currentActive);
                    sb.Append(" | Peak: ");
                    sb.Append(metrics.peakConcurrentCount);
                    sb.Append('\n');
                    sb.Append("    Total Spawns: ");
                    sb.Append(metrics.totalSpawns);
                    sb.Append(" | Despawns: ");
                    sb.Append(metrics.totalDespawns);
                    sb.Append('\n');
                }

                sb.AppendLine("\n═════════════════════════════════════════════════════════");
                sb.Append("Total Pools: ");
                sb.Append(totalPoolsActive);
                sb.Append(" | Active Objects: ");
                sb.Append(totalActiveObjects);
                sb.Append(" | Total Spawned: ");
                sb.Append(totalSpawned);
                sb.Append('\n');

                return sb.ToString();
            }
            finally
            {
                scope.Dispose();
            }
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<PoolDiagnosticsEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PoolDiagnosticsEventPayload>[4] - deferred pool diagnostics lane flushed by SystemDispatcher LateUpdate - owner: ObjectPoolDiagnostics
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<PoolDiagnosticsEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PoolDiagnosticsEventPayload>[4] - next-frame pool diagnostics lane prevents same-frame reentrant dispatch - owner: ObjectPoolDiagnostics
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                ClearPoolNameSlots();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(ObjectPoolDiagnostics),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void RegisterPoolName(string poolName)
        {
            uint poolHash = ComputePoolHash(poolName);
            if (poolHash == 0u)
                return;

            for (int i = 0; i < _poolNameSlotCount; i++)
            {
                if (_poolNamesByHash[i].IsValid != 0 && _poolNamesByHash[i].PoolHash == poolHash)
                    return;
            }

            if (_poolNameSlotCount >= _poolNamesByHash.Length)
                return;

            _poolNamesByHash[_poolNameSlotCount++] = new PoolNameSlot
            {
                PoolHash = poolHash,
                PoolName = poolName,
                IsValid = 1
            };
        }

        private static void ClearPoolNameSlots()
        {
            for (int i = 0; i < _poolNameSlotCount; i++)
                _poolNamesByHash[i].Clear();

            _poolNameSlotCount = 0;
        }

        private static uint ComputePoolHash(string poolName)
        {
            return string.IsNullOrWhiteSpace(poolName)
                ? 0u
                : unchecked((uint)LocHash.Compute(poolName));
        }

        private static void Publish(string poolName, PoolDiagnosticsEventType type, float metricValue, uint flagValue)
        {
            uint poolHash = ComputePoolHash(poolName);
            if (poolHash == 0u)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            RegisterPoolName(poolName);
            PoolDiagnosticsEventPayload payload = new PoolDiagnosticsEventPayload
            {
                PoolHash = poolHash,
                MetricValue = metricValue,
                EventType = (ushort)type,
                FlagValue = (ushort)flagValue
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void PublishDataBusSaturationWarning()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDataBusSaturationWarningFrame == frame)
                return;

            _lastDataBusSaturationWarningFrame = frame;
            TryPushDataBusSaturationNotification();
        }

        private static void TryPushDataBusSaturationNotification()
        {
            if (Hecton8.UI.NotificationEvents.TryPushWarning("DATA_BUS_SATURATED".AsSpan()))
                return;

            ReportDataBusSaturationNotificationMiss();
        }

        private static void ReportDataBusSaturationNotificationMiss()
        {
            _dataBusSaturationNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DataBusSaturationNotificationMissWarningHash,
                DataBusSaturationNotificationContextHash,
                Mathf.Max(1, _dataBusSaturationNotificationMissCount));
        }

        private static void SoftDropDiagnosticsQueuesForBatchMode()
        {
            // Do not call IsEmpty/TryDequeue — those are the native crash sites when the
            // queue is half-disposed. Release via sentinel path and zero counters.
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            try
            {
                ReleaseNativeQueues();
            }
            catch
            {
                _pendingEvents = default;
                _nextFrameEvents = default;
                _pendingEventsSentinelId = 0;
                _nextFrameEventsSentinelId = 0;
            }
        }

        private static bool QueueIsCreatedAndNonEmpty(ref NativeQueue<PoolDiagnosticsEventPayload> queue)
        {
            // L19 hop2: IsCreated can still be true after a half-dispose; IsEmpty then AVs.
            // Gate every native queue op behind a try and treat any fault as "not usable".
            try
            {
                return queue.IsCreated && !queue.IsEmpty();
            }
            catch
            {
                queue = default;
                return false;
            }
        }

        private static bool TryDequeueSafe(
            ref NativeQueue<PoolDiagnosticsEventPayload> queue,
            out PoolDiagnosticsEventPayload payload)
        {
            payload = default;
            try
            {
                if (!queue.IsCreated)
                    return false;
                return queue.TryDequeue(out payload);
            }
            catch
            {
                queue = default;
                payload = default;
                return false;
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (Application.isBatchMode)
            {
                SoftDropDiagnosticsQueuesForBatchMode();
                return;
            }

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<PoolDiagnosticsEventPayload> queue,
            ref int pendingCount)
        {
            // L19 hop2 LIVE crash site: queue.IsEmpty() / TryDequeue on disposed NativeQueue
            // → UnsafeUntypedQueue.IsEmpty ACCESS_VIOLATION. Never touch native queue ops
            // without IsCreated + try; on fault zero the handle so subsequent frames no-op.
            if (!queue.IsCreated)
            {
                pendingCount = 0;
                return true;
            }

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget > 0)
            {
                if (!QueueIsCreatedAndNonEmpty(ref queue))
                    break;

                if (!TryDequeueSafe(ref queue, out _))
                {
                    pendingCount = 0;
                    return false;
                }

                if (pendingCount > 0)
                    pendingCount--;
                scanBudget--;
            }

            if (!QueueIsCreatedAndNonEmpty(ref queue))
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            // Avoid IsEmpty() here — use counters only (pendingCount is authoritative).
            NativeQueue<PoolDiagnosticsEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }


        // ════════════════════════════════════════════════════════════
        //  EDITOR DEBUG
        // ════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        public static void PrintReport()
        {
            Hecton8.Core.H8Debug.Log(GenerateReport());
        }
#endif
    }
}
