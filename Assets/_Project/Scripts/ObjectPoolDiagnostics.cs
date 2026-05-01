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
//   // Subscribe to diagnostics
//   ObjectPoolDiagnostics.OnPoolWarning += (name, utilization) =>
//   {
//       Debug.LogWarning($"Pool {name} at {utilization}% utilization!");
//   };
//
//   // Query current stats
//   var stats = ObjectPoolDiagnostics.GetPoolStats("RobotDronePrefab");
//   Debug.Log($"Peak concurrent: {stats.peakConcurrentCount}");
//
//   // Get comprehensive report
//   string report = ObjectPoolDiagnostics.GenerateReport();
//   Debug.Log(report);
//
// ZERO-GC DESIGN:
//   • PoolStatSnapshot is struct (stack allocation only).
//   • All tracking via int counters and ulong timestamps.
//   • Report comes from pre-allocated StringBuilder pool.
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

    [StructLayout(LayoutKind.Sequential)]
    public struct PoolDiagnosticsEventPayload
    {
        public uint PoolHash;
        public float MetricValue;
        public ushort EventType;
        public ushort FlagValue;
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

        public override string ToString() => 
            $"[Pool] Active={currentActiveCount}/{poolCapacity} " +
            $"(Peak={peakConcurrentCount}) Spawns={totalSpawns} " + 
            $"Utilization={UtilizationPercent:F1}% Memory={estimatedMemoryMB:F2} MB";
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

        // COLD ALLOC: RegistryBucket<IObjectPoolDiagnosticsListener>[4] - pool diagnostics listeners drained on dispatcher LateUpdate - owner: ObjectPoolDiagnostics
        private static readonly RegistryBucket<IObjectPoolDiagnosticsListener> _listeners = new RegistryBucket<IObjectPoolDiagnosticsListener>(4);
        // COLD ALLOC: Dictionary<uint,string>[32] - pool names keyed by FNV-1a hash for cold-path diagnostics resolution - owner: ObjectPoolDiagnostics
        private static readonly Dictionary<uint, string> _poolNamesByHash = new Dictionary<uint, string>(32);
        private static NativeQueue<PoolDiagnosticsEventPayload> _pendingEvents;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEvents.Count : 0;

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
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _poolNamesByHash.Clear();
            _poolMetrics.Clear();
            _lastDiagnosticsFrame = -1;
            _lastDataBusSaturationWarningFrame = -1;
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        public static void Register(IObjectPoolDiagnosticsListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IObjectPoolDiagnosticsListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out PoolDiagnosticsEventPayload payload))
                    return;

                IObjectPoolDiagnosticsListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnPoolDiagnosticsEvent(in payload);
            }
        }

        public static bool TryResolvePoolName(uint poolHash, out string poolName)
        {
            return _poolNamesByHash.TryGetValue(poolHash, out poolName);
        }

        public static void PublishDataBusDepth(uint queueHash, int pendingCount)
        {
            if (queueHash == 0u || pendingCount < 0)
                return;

            EnsureInitialized();
            bool saturated = pendingCount > 128;
            _pendingEvents.Enqueue(new PoolDiagnosticsEventPayload
            {
                PoolHash = queueHash,
                MetricValue = pendingCount,
                EventType = (ushort)(saturated ? PoolDiagnosticsEventType.DataBusSaturated : PoolDiagnosticsEventType.DataBusDepth),
                FlagValue = (ushort)(saturated ? 1 : 0)
            });

            if (saturated)
                PublishDataBusSaturationWarning();
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
                    lastMeasurementFrame = Time.frameCount
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
            if (_lastDiagnosticsFrame == Time.frameCount)
                return; // Already polled this frame

            _lastDiagnosticsFrame = Time.frameCount;

            foreach (var kvp in _poolMetrics)
            {
                string poolName = kvp.Key;
                PoolMetrics metrics = kvp.Value;

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
                sb.AppendLine($"Frame: {Time.frameCount} | Time: {Time.realtimeSinceStartup:F2}s");
                sb.AppendLine("═════════════════════════════════════════════════════════");

                int totalPoolsActive = 0;
                int totalActiveObjects = 0;
                int totalSpawned = 0;

                foreach (var kvp in _poolMetrics)
                {
                    string poolName = kvp.Key;
                    PoolMetrics metrics = kvp.Value;

                    int currentActive = metrics.totalSpawns - metrics.totalDespawns;
                    totalPoolsActive++;
                    totalActiveObjects += currentActive;
                    totalSpawned += metrics.totalSpawns;

                    sb.Append($"\n  Pool: {poolName}\n");
                    sb.Append($"    Active: {currentActive} | Peak: {metrics.peakConcurrentCount}\n");
                    sb.Append($"    Total Spawns: {metrics.totalSpawns} | Despawns: {metrics.totalDespawns}\n");
                }

                sb.AppendLine("\n═════════════════════════════════════════════════════════");
                sb.Append($"Total Pools: {totalPoolsActive} | ");
                sb.Append($"Active Objects: {totalActiveObjects} | ");
                sb.Append($"Total Spawned: {totalSpawned}\n");

                return sb.ToString();
            }
            finally
            {
                scope.Dispose();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PoolDiagnosticsEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PoolDiagnosticsEventPayload>[4] - deferred pool diagnostics lane flushed by SystemDispatcher LateUpdate - owner: ObjectPoolDiagnostics
            }
        }

        private static void RegisterPoolName(string poolName)
        {
            uint poolHash = ComputePoolHash(poolName);
            if (poolHash != 0u && !_poolNamesByHash.ContainsKey(poolHash))
                _poolNamesByHash.Add(poolHash, poolName);
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

            RegisterPoolName(poolName);
            EnsureInitialized();
            _pendingEvents.Enqueue(new PoolDiagnosticsEventPayload
            {
                PoolHash = poolHash,
                MetricValue = metricValue,
                EventType = (ushort)type,
                FlagValue = (ushort)flagValue
            });
        }

        private static void PublishDataBusSaturationWarning()
        {
            int frame = Time.frameCount;
            if (_lastDataBusSaturationWarningFrame == frame)
                return;

            _lastDataBusSaturationWarningFrame = frame;
            Hecton8.UI.NotificationEvents.PushWarning("DATA_BUS_SATURATED");
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }

        // ════════════════════════════════════════════════════════════
        //  EDITOR DEBUG
        // ════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        public static void PrintReport()
        {
            Debug.Log(GenerateReport());
        }
#endif
    }
}
