// ============================================================================
// HECTON-8 - RaycastBatchHelper.cs
// Legacy query facade. PhysX command scheduling is intentionally disabled.
// ============================================================================

using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Single legacy query result.
    /// </summary>
    public struct QueryResult
    {
        // ARM64 layout law: no runtime bool on hot result DTO. 0 = miss, 1 = hit.
        public byte hasHit;
        public InteractionSurfaceHit hit;

        public float distance => hasHit != 0 ? hit.distance : float.MaxValue;
        public Vector3 point => hasHit != 0 ? hit.point : Vector3.zero;
        public Collider collider => hasHit != 0 ? hit.collider : null;
    }

    /// <summary>
    /// Compatibility facade for old batched ray query callers.
    /// </summary>
    /// <remarks>
    /// This service no longer executes Unity PhysX queries. Hot gameplay owners
    /// must use owner-local SDF, registered spatial, or typed signal routes.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class RaycastBatchHelper : MonoBehaviour, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IPhysicsQueryTelemetryReadModel, IGlobalRegistryHotSwapListener
    {
        public static int TotalLegacySurfaceQueriesProcessed;

        private const int MaxQueries = 512;
        private const float DirectionLengthMinSq = 0.000001f;

        private int _queryCount;
        private int _completedQueryCount;
        private int _lastFramePrepared = -1;
        private bool _batchExecuted;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _hotSwapRegistered;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _queryOverflowLogged;
#endif

        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                return _registeredService
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.NotStarted;
            }
        }

        public bool IsServiceReady => HeartbeatState == ServiceHeartbeatState.Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TotalLegacySurfaceQueriesProcessed = 0;
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            GameBootstrapper.PersistRuntimeService(this);
            _queryCount = 0;
            _lastFramePrepared = -1;
            _batchExecuted = false;
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
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
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ReleaseBuffers();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterLateFrame();
            if (currentService != null && isActiveAndEnabled)
                TryRegisterLateFrame();
        }

        /// <summary>
        /// Registers a legacy query and returns a deterministic miss slot.
        /// </summary>
        public int AddQuery(
            Vector3 origin,
            Vector3 direction,
            float distance,
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
                    Hecton8.Core.H8Debug.LogWarning("[RaycastBatchHelper] Query buffer overflow. Excess legacy queries are dropped for this frame.");
                }
#endif
                return -1;
            }

            if (!IsFinite(origin) ||
                !IsFinite(direction) ||
                distance <= 0f ||
                direction.sqrMagnitude <= DirectionLengthMinSq)
            {
                return -1;
            }

            int index = _queryCount;
            _queryCount++;
            TotalLegacySurfaceQueriesProcessed++;
            return index;
        }

        /// <summary>
        /// Clears pending legacy query slots.
        /// </summary>
        public void ClearQueries()
        {
            _queryCount = 0;
            _completedQueryCount = 0;
            _batchExecuted = false;
        }

        /// <summary>
        /// Completes the legacy batch as misses without PhysX scheduling.
        /// </summary>
        public void ExecuteBatch()
        {
            TryRegisterLateFrame();

            if (!PrepareForQueryWrite())
                return;

            _completedQueryCount = _queryCount;
            _batchExecuted = true;
        }

        public void LateFrameTick()
        {
            if (!_batchExecuted)
            {
                _completedQueryCount = _queryCount;
                _batchExecuted = true;
            }
        }

        public QueryResult GetResult(int index)
        {
            if (index < 0 || index >= _completedQueryCount)
                return default;

            return default;
        }

        public int QueryCount => _queryCount;

        public bool WasExecuted => _batchExecuted;

        public int LegacySurfaceQueriesProcessed => TotalLegacySurfaceQueriesProcessed;

        public int PlayerLookQueryCacheHits => QueryCacheContext.CacheHits;

        public void ResetPhysicsQueryTelemetryCounters()
        {
            TotalLegacySurfaceQueriesProcessed = 0;
            QueryCacheContext.CacheHits = 0;
        }

        private bool PrepareForQueryWrite()
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastFramePrepared == currentFrame)
                return true;

            _lastFramePrepared = currentFrame;
            ClearQueries();
            return true;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterRaycastBatchRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.RaycastBatch, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            RaycastBatchHelper registered = GlobalRegistry.RaycastBatch;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsRaycastBatchRuntimeUsable(registered))
            {
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterRaycastBatchRuntime(registered);
            return false;
        }

        private static bool IsRaycastBatchRuntimeUsable(RaycastBatchHelper helper)
        {
            return helper != null && helper._registeredService && helper.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            if (ReferenceEquals(GlobalRegistry.RaycastBatch, this))
                GlobalRegistry.UnregisterRaycastBatchRuntime(this);

            _registeredService = false;
        }

        private void ReleaseBuffers()
        {
            _queryCount = 0;
            _completedQueryCount = 0;
            _lastFramePrepared = -1;
            _batchExecuted = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }
    }
}
