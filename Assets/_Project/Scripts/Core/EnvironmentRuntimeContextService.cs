using Hecton8.Construction;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Bootstrap-owned environment runtime context published through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9925)]
    public sealed class EnvironmentRuntimeContextService : MonoBehaviour, IEnvironmentRuntimeContext, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredContext;
        private bool _hotSwapRegistered;
        private bool _runtimeOwnerAborted;
        private ConstructionManager _constructionManager;
        private ILogisticsService _logisticsService;
        private ModuleCatalog _moduleCatalog;
        private HazardZoneManager _hazardZoneManager;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public ConstructionManager ConstructionManager => _constructionManager;

        /// <inheritdoc />
        public ILogisticsService Logistics => _logisticsService ?? _constructionManager;

        /// <inheritdoc />
        public ModuleCatalog ModuleCatalog => _moduleCatalog;

        /// <inheritdoc />
        public HazardZoneManager HazardZones => _hazardZoneManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(null);
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live environment context instance.</returns>
        public static EnvironmentRuntimeContextService EnsureRuntimeInstance()
        {
            EnvironmentRuntimeContextService runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;
            if (IsEnvironmentContextUsable(registeredContext) &&
                ReferenceEquals(registeredContext as EnvironmentRuntimeContextService, null))
            {
                return null;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Environment + HazardZone context root; zero authored GUID hits in player builds.
            GameObject runtimeRoot = new GameObject("[EnvironmentRuntimeContextService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned environment runtime context root - owner: EnvironmentRuntimeContextService
            return runtimeRoot.AddComponent<EnvironmentRuntimeContextService>();
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                if (!TryRegisterContext())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                SyncEnvironmentContextCold();
                EnsureHazardZoneManager();
                return;
            }

            if (!TryRegisterContext())
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            SyncEnvironmentContextCold();
            EnsureHazardZoneManager();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            SyncEnvironmentContext();
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_isInitialized)
            {
                if (!EnsureSingletonOwnership())
                    return;

                if (!TryRegisterContext())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                SyncEnvironmentContextCold();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterContext();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterContext();
            _isInitialized = false;
            _constructionManager = null;
            _moduleCatalog = null;
            _hazardZoneManager = null;

            GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(this);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (_isInitialized)
                    {
                        TryUnregisterUpdatable();
                        if (currentService != null && isActiveAndEnabled)
                            TryRegisterUpdatable();
                    }
                    break;

                case GlobalRegistryServiceSlot.Logistics:
                    _constructionManager = currentService as ConstructionManager;
                    _logisticsService = currentService as ILogisticsService;
                    _moduleCatalog = _logisticsService != null ? _logisticsService.Catalog : null;
                    break;

                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    _hazardZoneManager = currentService as HazardZoneManager;
                    break;
            }
        }

        internal HazardZoneManager EnsureHazardZoneManager()
        {
            if (_hazardZoneManager != null)
                return _hazardZoneManager;

            _hazardZoneManager = GlobalRegistry.HazardZones;
            if (_hazardZoneManager != null || !Application.isPlaying)
                return _hazardZoneManager;

            TryGetComponent(out _hazardZoneManager);
            // Player-build construction path (not editor-only): HazardZoneManager has zero
            // authored scene/prefab hits (GUID 008e5f84c0b54c23a0b2341464541d1e). Owned by
            // EnvironmentRuntimeContextService; InitializeService always calls this.
            if (_hazardZoneManager == null)
            {
                _hazardZoneManager = gameObject.AddComponent<HazardZoneManager>(); // COLD ALLOC: HazardZoneManager[1] - environment-owned runtime hazard registry - owner: EnvironmentRuntimeContextService
            }

            return _hazardZoneManager;
        }


        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterEnvironmentRuntimeContextRuntime(this);
            return ReferenceEquals(GlobalRegistry.EnvironmentRuntimeContextRuntime, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsEnvironmentRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;
            if (ReferenceEquals(registeredContext, null) || ReferenceEquals(registeredContext, this))
                return false;

            if (IsEnvironmentContextUsable(registeredContext))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            EnvironmentRuntimeContextService staleContext = registeredContext as EnvironmentRuntimeContextService;
            if (!ReferenceEquals(staleContext, null))
            {
                GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);
                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(staleContext);
                staleContext._registeredContext = false;
                staleContext._isInitialized = false;
            }

            return false;
        }

        private static EnvironmentRuntimeContextService ResolveUsableRuntime()
        {
            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (IsEnvironmentRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;
            if (IsEnvironmentContextUsable(registeredContext))
                return registeredContext as EnvironmentRuntimeContextService;

            EnvironmentRuntimeContextService staleContext = registeredContext as EnvironmentRuntimeContextService;
            if (!ReferenceEquals(staleContext, null))
            {
                GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);
                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(staleContext);
                staleContext._registeredContext = false;
                staleContext._isInitialized = false;
            }

            return null;
        }

        private static bool IsEnvironmentContextUsable(IEnvironmentRuntimeContext context)
        {
            if (ReferenceEquals(context, null))
                return false;

            EnvironmentRuntimeContextService runtime = context as EnvironmentRuntimeContextService;
            return ReferenceEquals(runtime, null) ||
                   (runtime._registeredContext && IsEnvironmentRuntimeUsable(runtime));
        }

        private static bool IsEnvironmentRuntimeUsable(EnvironmentRuntimeContextService runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private void SyncEnvironmentContext()
        {
            if (_constructionManager != null && !_constructionManager.isActiveAndEnabled)
            {
                _constructionManager = null;
                _logisticsService = null;
            }

            if (_logisticsService == null && _constructionManager != null)
                _logisticsService = _constructionManager;

            _moduleCatalog = _logisticsService != null ? _logisticsService.Catalog : null;

            if (_hazardZoneManager != null && !_hazardZoneManager.isActiveAndEnabled)
                _hazardZoneManager = null;
        }

        private void SyncEnvironmentContextCold()
        {
            if (_constructionManager == null || !_constructionManager.isActiveAndEnabled)
                _constructionManager = GlobalRegistry.ConstructionRuntime;

            _logisticsService = GlobalRegistry.Logistics ?? _constructionManager;

            if (_hazardZoneManager == null || !_hazardZoneManager.isActiveAndEnabled)
                _hazardZoneManager = GlobalRegistry.HazardZones;

            SyncEnvironmentContext();
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
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

        private bool TryRegisterContext()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredContext)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;
            if (!ReferenceEquals(registeredContext, null) && !ReferenceEquals(registeredContext, this))
            {
                EnvironmentRuntimeContextService staleContext = registeredContext as EnvironmentRuntimeContextService;
                if (ReferenceEquals(staleContext, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);
                GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(staleContext);
                staleContext._registeredContext = false;
                staleContext._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterEnvironmentRuntimeContext(this);
            _registeredContext = ReferenceEquals(GlobalRegistry.Environment, this);
            _runtimeOwnerAborted = !_registeredContext;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredContext;
        }

        private void TryUnregisterContext()
        {
            if (!_registeredContext)
                return;

            GlobalRegistry.UnregisterEnvironmentRuntimeContext(this);
            _registeredContext = false;
        }
    }
}
