// ============================================================================
// HECTON-8 — RaycastBatchHelper.cs  v2.0
// High-performance asynchronous raycast batching with Unity Jobs.
//
// PURPOSE:
//   Offloads multiple raycasts to worker threads via RaycastCommand.
//   Optimized for Zero-GC and low main-thread overhead.
//
// ZERO GC GUARANTEE:
//   • Uses NativeArray<RaycastCommand> and NativeArray<RaycastHit>.
//   • Reuses persistent native memory to avoid allocation frames.
//   • No C# heap allocations in ExecuteBatch or GetResult.
//
// ============================================================================

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Single raycast result (hit or miss).
    /// </summary>
    public struct QueryResult
    {
        public bool hasHit;
        public RaycastHit hit;

        public float distance => hasHit ? hit.distance : float.MaxValue;
        public Vector3 point => hasHit ? hit.point : Vector3.zero;
        public Collider collider => hasHit ? hit.collider : null;
    }

    /// <summary>
    /// Zero-GC asynchronous raycast batch executor using Unity Jobs.
    /// Results are synchronized at the end of ExecuteBatch for immediate use,
    /// but the actual work is distributed across worker threads.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)] // Run BEFORE all gameplay systems
    public sealed class RaycastBatchHelper : MonoBehaviour
    {
        public static int TotalRaycastsProcessed;
        private const int MaxQueries = 512;

        // ── Native Buffers (Persistent) ──
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        
        // ── Managed Mirror for API ──
        private QueryResult[] _results;
        private Collider[] _excludeColliders;
        
        private int _queryCount;
        private bool _batchExecuted;
        private JobHandle _lastJobHandle;

        private static RaycastBatchHelper _instance;
        public static RaycastBatchHelper Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Allocate Native persistence
            _commands = new NativeArray<RaycastCommand>(MaxQueries, Allocator.Persistent);
            _hits = new NativeArray<RaycastHit>(MaxQueries, Allocator.Persistent);
            
            // Managed mirrors for filtering/API
            _results = new QueryResult[MaxQueries];
            _excludeColliders = new Collider[MaxQueries];
            
            _queryCount = 0;
            _batchExecuted = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            // CRITICAL: Prevent memory leaks in Native memory
            if (_commands.IsCreated) _commands.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
        }

        /// <summary>
        /// Registers a raycast query for the next batch.
        /// </summary>
        public int AddQuery(Vector3 origin, Vector3 direction, float distance,
                           int layerMask = -1,
                           QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore,
                           Collider excludeCollider = null)
        {
            if (_queryCount >= MaxQueries)
            {
                Debug.LogWarning("[RaycastBatchHelper] Query buffer overflow!");
                return -1;
            }

            int idx = _queryCount;
            
            _commands[idx] = new RaycastCommand(
                origin, 
                direction.normalized, 
                new QueryParameters(layerMask, false, triggerInteraction), 
                distance);

            _excludeColliders[idx] = excludeCollider;
            _results[idx].hasHit = false; // Reset placeholder

            _queryCount++;
            return idx;
        }

        /// <summary>
        /// Clears the batch buffer. Call at the start of frame if manual management is used.
        /// </summary>
        public void ClearQueries()
        {
            _queryCount = 0;
            _batchExecuted = false;
        }

        /// <summary>
        /// Orchestrates the batch execution on worker threads.
        /// Synchronizes at the end of call to provide same-frame results.
        /// </summary>
        public void ExecuteBatch()
        {
            if (_queryCount <= 0)
            {
                _batchExecuted = true;
                return;
            }
            TotalRaycastsProcessed += _queryCount;

            // Schedule asynchronous batch on Unity's job threads
            _lastJobHandle = RaycastCommand.ScheduleBatch(_commands, _hits, 1, default);
            
            // Ensure results are ready for immediate use this frame.
            // In AA systems, we synchronize here to simplify API, but work was parallelized.
            _lastJobHandle.Complete();

            // Resolve and Filter
            for (int i = 0; i < _queryCount; i++)
            {
                RaycastHit hit = _hits[i];
                bool hasHit = hit.collider != null;

                // Apply exclude filter
                if (hasHit && _excludeColliders[i] != null && hit.collider == _excludeColliders[i])
                {
                    hasHit = false;
                    hit = default;
                }

                _results[i] = new QueryResult
                {
                    hasHit = hasHit,
                    hit = hasHit ? hit : default
                };
            }

            _batchExecuted = true;
        }

        public QueryResult GetResult(int index)
        {
            if (index < 0 || index >= _queryCount) return default;
            return _results[index];
        }

        public int QueryCount => _queryCount;
        public bool WasExecuted => _batchExecuted;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_batchExecuted) return;

            for (int i = 0; i < _queryCount; i++)
            {
                if (_results[i].hasHit)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_commands[i].from, _results[i].point);
                    Gizmos.DrawWireSphere(_results[i].point, 0.05f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(_commands[i].from, _commands[i].direction * _commands[i].distance);
                }
            }
        }
#endif
    }
}
