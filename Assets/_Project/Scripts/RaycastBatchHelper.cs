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

using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
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
    /// Results are published from the dispatcher late-frame window to avoid
    /// mid-frame stalls in gameplay lanes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)] // Run BEFORE all gameplay systems
    public sealed class RaycastBatchHelper : MonoBehaviour, ILateFrameTickable
    {
        public static int TotalRaycastsProcessed;
        private const int MaxQueries = 512;

        // ── Native Buffers (Persistent) ──
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        
        // ── Managed Mirror for API ──
        // COLD ALLOC: RaycastHit[8] - synchronous single-query fallback buffer - owner: RaycastBatchHelper
        private readonly RaycastHit[] _singleHitBuffer = new RaycastHit[8];
        private QueryResult[] _results;
        private Collider[] _excludeColliders;
        
        private int _queryCount;
        private int _scheduledQueryCount;
        private int _completedQueryCount;
        private int _lastFramePrepared = -1;
        private bool _batchExecuted;
        private bool _jobScheduled;
        private bool _registeredLateFrame;
        private JobHandle _lastJobHandle;

        private static RaycastBatchHelper _instance;
        public static RaycastBatchHelper Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            TotalRaycastsProcessed = 0;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            GameBootstrapper.PersistRuntimeService(this);

            EnsureBuffersAllocated();
            _queryCount = 0;
            _lastFramePrepared = -1;
            _batchExecuted = false;
        }

        private void OnEnable()
        {
            EnsureBuffersAllocated();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            ReleaseBuffers();
        }

        /// <summary>
        /// Registers a raycast query for the next batch.
        /// </summary>
        public int AddQuery(Vector3 origin, Vector3 direction, float distance,
                           int layerMask = HectonLayerMasks.DataTemplateAuthoringMaskValue,
                           QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore,
                           Collider excludeCollider = null)
        {
            if (!PrepareForQueryWrite())
                return -1;

            if (_queryCount >= MaxQueries)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[RaycastBatchHelper] Query buffer overflow!");
#endif
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
            if (_jobScheduled)
                return;

            _queryCount = 0;
            _scheduledQueryCount = 0;
            _completedQueryCount = 0;
            _batchExecuted = false;
        }

        /// <summary>
        /// Orchestrates the batch execution on worker threads.
        /// Results are published in the late-frame swap window.
        /// </summary>
        public void ExecuteBatch()
        {
            TryRegisterLateFrame();

            if (!PrepareForQueryWrite())
                return;

            if (_queryCount <= 0)
            {
                _batchExecuted = true;
                _completedQueryCount = 0;
                return;
            }
            TotalRaycastsProcessed += _queryCount;

            if (_queryCount == 1)
            {
                ResolveSingleQuery();
                _batchExecuted = true;
                _completedQueryCount = 1;
                return;
            }

            // Schedule asynchronous batch on Unity's job threads
            _lastJobHandle = RaycastCommand.ScheduleBatch(_commands, _hits, 16, default);
            _scheduledQueryCount = _queryCount;
            _completedQueryCount = 0;
            _batchExecuted = false;
            _jobScheduled = true;
        }

        public void LateFrameTick()
        {
            TryConsumeScheduledBatch(false);
        }

        private bool TryConsumeScheduledBatch(bool forceComplete)
        {
            if (!_jobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _lastJobHandle, forceComplete))
                return false;

            // Resolve and Filter
            for (int i = 0; i < _scheduledQueryCount; i++)
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

            _queryCount = _scheduledQueryCount;
            _completedQueryCount = _scheduledQueryCount;
            _scheduledQueryCount = 0;
            _batchExecuted = true;
            _jobScheduled = false;
            return true;
        }

        private void ResolveSingleQuery()
        {
            RaycastCommand command = _commands[0];
            bool hasHit = TryResolveNearestSingleHit(command, out RaycastHit hit);

            if (hasHit && _excludeColliders[0] != null && hit.collider == _excludeColliders[0])
            {
                hasHit = false;
                hit = default;
            }

            _results[0] = new QueryResult
            {
                hasHit = hasHit,
                hit = hasHit ? hit : default
            };
        }

        private bool TryResolveNearestSingleHit(RaycastCommand command, out RaycastHit nearestHit)
        {
            QueryParameters parameters = command.queryParameters;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                command.from,
                command.direction,
                _singleHitBuffer,
                command.distance,
                parameters.layerMask,
                parameters.hitTriggers);

            nearestHit = default;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _singleHitBuffer[i];
                if (candidate.collider == null || float.IsNaN(candidate.distance) || float.IsInfinity(candidate.distance))
                    continue;

                if (candidate.distance >= nearestDistance)
                    continue;

                nearestDistance = candidate.distance;
                nearestHit = candidate;
            }

            return nearestHit.collider != null;
        }

        public QueryResult GetResult(int index)
        {
            if (index < 0 || index >= _completedQueryCount) return default;
            return _results[index];
        }

        public int QueryCount
        {
            get
            {
                return _queryCount;
            }
        }

        public bool WasExecuted
        {
            get
            {
                return _batchExecuted;
            }
        }

        private bool PrepareForQueryWrite()
        {
            int currentFrame = Time.frameCount;
            if (_lastFramePrepared == currentFrame)
                return !_jobScheduled;

            if (_jobScheduled)
            {
                TryConsumeScheduledBatch(false);
                if (_jobScheduled)
                    return false;
            }

            _lastFramePrepared = currentFrame;
            ClearQueries();
            return true;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = true;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void EnsureBuffersAllocated()
        {
            if (!_commands.IsCreated)
            {
                // COLD ALLOC: NativeArray<RaycastCommand>[512] — persistent batched raycast commands — owner: RaycastBatchHelper
                _commands = new NativeArray<RaycastCommand>(MaxQueries, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeArray(
                    _commands,
                    nameof(RaycastBatchHelper),
                    nameof(_commands),
                    NativeAllocationLifetime.Scene);
            }

            if (!_hits.IsCreated)
            {
                // COLD ALLOC: NativeArray<RaycastHit>[512] — persistent batched raycast hits — owner: RaycastBatchHelper
                _hits = new NativeArray<RaycastHit>(MaxQueries, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeArray(
                    _hits,
                    nameof(RaycastBatchHelper),
                    nameof(_hits),
                    NativeAllocationLifetime.Scene);
            }

            if (_results == null || _results.Length != MaxQueries)
            {
                // COLD ALLOC: QueryResult[512] — managed result mirror for same-frame API — owner: RaycastBatchHelper
                _results = new QueryResult[MaxQueries];
            }

            if (_excludeColliders == null || _excludeColliders.Length != MaxQueries)
            {
                // COLD ALLOC: Collider[512] — managed exclude mirror for same-frame API — owner: RaycastBatchHelper
                _excludeColliders = new Collider[MaxQueries];
            }
        }

        private void ReleaseBuffers()
        {
            if (_jobScheduled)
            {
                TryConsumeScheduledBatch(true);
            }

            if (_commands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_commands);
                _commands.Dispose();
                _commands = default;
            }

            if (_hits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_hits);
                _hits.Dispose();
                _hits = default;
            }

            _queryCount = 0;
            _scheduledQueryCount = 0;
            _completedQueryCount = 0;
            _lastFramePrepared = -1;
            _batchExecuted = false;
            _jobScheduled = false;
        }

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
