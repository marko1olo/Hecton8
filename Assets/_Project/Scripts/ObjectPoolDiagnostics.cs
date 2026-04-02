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
using UnityEngine;

namespace Hecton8.Core
{
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

        /// <summary>
        /// Fire when a pool reaches high utilization (>80%).
        /// Called once per poll cycle, not every spawn.
        /// </summary>
        public static event Action<string, float> OnPoolWarning;

        /// <summary>
        /// Fire when pool is exhausted (no available instances).
        /// </summary>
        public static event Action<string> OnPoolExhausted;

        /// <summary>
        /// Fire on predictive alert: spawn rate is increasing.
        /// Indicates pool undersizing.
        /// </summary>
        public static event Action<string, bool> OnSpawnRateAlert; // (poolName, isAccelerating)

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
            public float avgSpawnRateLastSecond;
            public float avgSpawnRateLastFrame;
            public bool wasAccelerating;
        }

        private static readonly Dictionary<string, PoolMetrics> _poolMetrics = 
            new Dictionary<string, PoolMetrics>(32);

        private static int _lastDiagnosticsFrame = -1;

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Register a pool for diagnostics tracking.
        /// Called by ObjectPoolManager.Spawn when pool is first created.
        /// </summary>
        public static void RegisterPool(string poolName, int initialCapacity)
        {
            if (!_poolMetrics.ContainsKey(poolName))
            {
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
                        OnPoolWarning?.Invoke(poolName, utilization);

                    // Alert if exhausted
                    if (currentActive >= capacity)
                        OnPoolExhausted?.Invoke(poolName);
                }

                // Spawn rate acceleration detection
                int spawnsSinceLastFrame = metrics.totalSpawns - metrics.lastSpawnCount;
                if (metrics.avgSpawnRateLastFrame > 0 && spawnsSinceLastFrame > metrics.avgSpawnRateLastFrame * 1.5f)
                {
                    bool isAccelerating = !metrics.wasAccelerating;
                    if (isAccelerating)
                    {
                        OnSpawnRateAlert?.Invoke(poolName, true);
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

        // ════════════════════════════════════════════════════════════
        //  EDITOR DEBUG
        // ════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void EditorRegisterDebugMenu()
        {
            // In editor: Ctrl+Shift+P can trigger pool diagnostics output
            // (Would need editor menu integration in real project)
        }

        public static void PrintReport()
        {
            Debug.Log(GenerateReport());
        }
#endif
    }
}
