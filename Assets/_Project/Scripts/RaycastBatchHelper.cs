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

using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Single raycast result (hit or miss).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
    public sealed class RaycastBatchHelper : MonoBehaviour, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        public static int TotalRaycastsProcessed;
        private const int MaxQueries = 512;
        private const int MaxCommandsPerRaycastJob = 16;
        private const float DirectionLengthMinSq = 0.000001f;
        private const float DirectionUnitToleranceSq = 0.0004f;

        // ── Native Buffers (Persistent) ──
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        
        // ── Managed Mirror for API ──
        private QueryResult[] _results;
        private Collider[] _excludeColliders;
        
        private int _queryCount;
        private int _scheduledQueryCount;
        private int _completedQueryCount;
        private int _lastFramePrepared = -1;
        private bool _batchExecuted;
        private bool _jobScheduled;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private JobHandle _lastJobHandle;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _queryOverflowLogged;
#endif

        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                if (!_registeredService)
                    return ServiceHeartbeatState.NotStarted;

                return _commands.IsCreated && _hits.IsCreated
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.Booting;
            }
        }

        public bool IsServiceReady => HeartbeatState == ServiceHeartbeatState.Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TotalRaycastsProcessed = 0;
        }

        private void Awake()
        {
            RaycastBatchHelper registered = GlobalRegistry.RaycastBatch;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GameBootstrapper.PersistRuntimeService(this);

            EnsureBuffersAllocated();
            _queryCount = 0;
            _lastFramePrepared = -1;
            _batchExecuted = false;
        }

        private void OnEnable()
        {
            TryRegisterService();
            EnsureBuffersAllocated();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterService();
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            TryUnregisterLateFrame();
            TryUnregisterService();
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
                if (!_queryOverflowLogged)
                {
                    _queryOverflowLogged = true;
                    Debug.LogWarning("[RaycastBatchHelper] Query buffer overflow. Excess raycasts are dropped for this frame.");
                }
#endif
                return -1;
            }

            if (!TryResolveRayDirection(direction, out Vector3 rayDirection))
                return -1;

            int idx = _queryCount;
            
            _commands[idx] = new RaycastCommand(
                origin, 
                rayDirection,
                new QueryParameters(layerMask, false, triggerInteraction), 
                distance);

            _excludeColliders[idx] = excludeCollider;
            _results[idx] = default;

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

            int usedCount = math.max(_queryCount, math.max(_scheduledQueryCount, _completedQueryCount));
            ClearManagedSlots(usedCount);
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

            // Schedule asynchronous batch on Unity's job threads
            NativeArray<RaycastCommand> scheduledCommands = _commands.GetSubArray(0, _queryCount);
            NativeArray<RaycastHit> scheduledHits = _hits.GetSubArray(0, _queryCount);
            int minCommandsPerJob = math.min(MaxCommandsPerRaycastJob, _queryCount);
            _lastJobHandle = RaycastCommand.ScheduleBatch(scheduledCommands, scheduledHits, minCommandsPerJob, default);
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
                _excludeColliders[i] = null;

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

        private static bool TryResolveRayDirection(Vector3 direction, out Vector3 rayDirection)
        {
            float lengthSq = direction.sqrMagnitude;
            if (lengthSq <= DirectionLengthMinSq)
            {
                rayDirection = default;
                return false;
            }

            if (math.abs(lengthSq - 1f) <= DirectionUnitToleranceSq)
            {
                rayDirection = direction;
                return true;
            }

            rayDirection = direction * math.rsqrt(lengthSq);
            return true;
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
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            RaycastBatchHelper registered = GlobalRegistry.RaycastBatch;
            if (registered != null && registered != this)
                return;

            GlobalRegistry.RegisterRaycastBatchRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.RaycastBatch, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            if (ReferenceEquals(GlobalRegistry.RaycastBatch, this))
                GlobalRegistry.UnregisterRaycastBatchRuntime(this);

            _registeredService = false;
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
            ClearManagedSlots(MaxQueries);
        }

        private void ClearManagedSlots(int count)
        {
            if (_results == null || _excludeColliders == null)
                return;

            int safeCount = math.min(count, MaxQueries);
            for (int i = 0; i < safeCount; i++)
            {
                _results[i] = default;
                _excludeColliders[i] = null;
            }
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
