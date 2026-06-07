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
            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (runtime == null)
                runtime = GlobalRegistry.Environment as EnvironmentRuntimeContextService;

            if (runtime != null)
                return runtime;

            if (!Application.isPlaying)
                return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject runtimeRoot = new GameObject("[EnvironmentRuntimeContextService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned environment runtime context root - owner: EnvironmentRuntimeContextService
            return runtimeRoot.AddComponent<EnvironmentRuntimeContextService>();
#else
            return null;
#endif
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                EnsureSingletonOwnership();
                if (GlobalRegistry.EnvironmentRuntimeContextRuntime != this)
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterContext();
                SyncEnvironmentContextCold();
                EnsureHazardZoneManager();
                return;
            }

            EnsureSingletonOwnership();
            if (GlobalRegistry.EnvironmentRuntimeContextRuntime != this)
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            TryRegisterContext();
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
            if (_isInitialized)
            {
                EnsureSingletonOwnership();
                if (GlobalRegistry.EnvironmentRuntimeContextRuntime != this)
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterContext();
                SyncEnvironmentContextCold();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterContext();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hazardZoneManager == null)
            {
                _hazardZoneManager = gameObject.AddComponent<HazardZoneManager>(); // COLD ALLOC: HazardZoneManager[1] - environment-owned runtime hazard registry - owner: EnvironmentRuntimeContextService
            }
#endif

            return _hazardZoneManager;
        }

        private void EnsureSingletonOwnership()
        {
            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (runtime == null)
                runtime = GlobalRegistry.Environment as EnvironmentRuntimeContextService;

            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterEnvironmentRuntimeContextRuntime(this);
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

        private void TryRegisterContext()
        {
            if (_registeredContext)
                return;

            IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;
            if (registeredContext != null && !ReferenceEquals(registeredContext, this))
                return;

            GlobalRegistry.RegisterEnvironmentRuntimeContext(this);
            _registeredContext = ReferenceEquals(GlobalRegistry.Environment, this);
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
