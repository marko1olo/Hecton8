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
    public sealed class EnvironmentRuntimeContextService : MonoBehaviour, IEnvironmentRuntimeContext, IUpdatable, IServiceHeartbeat
    {
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredContext;
        private ConstructionManager _constructionManager;
        private ModuleCatalog _moduleCatalog;
        private HazardZoneManager _hazardZoneManager;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public ConstructionManager ConstructionManager
        {
            get
            {
                SyncEnvironmentContext();
                return _constructionManager;
            }
        }

        /// <inheritdoc />
        public ModuleCatalog ModuleCatalog
        {
            get
            {
                SyncEnvironmentContext();
                return _moduleCatalog;
            }
        }

        /// <inheritdoc />
        public HazardZoneManager HazardZones
        {
            get
            {
                SyncEnvironmentContext();
                return EnsureHazardZoneManager();
            }
        }

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
            if (runtime != null)
                return runtime;

            GameObject runtimeRoot = new GameObject("[EnvironmentRuntimeContextService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned environment runtime context root - owner: EnvironmentRuntimeContextService
            return runtimeRoot.AddComponent<EnvironmentRuntimeContextService>();
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterContext();
                SyncEnvironmentContext();
                EnsureHazardZoneManager();
                return;
            }

            EnsureSingletonOwnership();
            if (GlobalRegistry.EnvironmentRuntimeContextRuntime != this)
                return;

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterContext();
            SyncEnvironmentContext();
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
                TryRegisterUpdatable();
                TryRegisterContext();
                SyncEnvironmentContext();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterContext();
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            TryUnregisterContext();

            GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(this);
        }

        internal HazardZoneManager EnsureHazardZoneManager()
        {
            if (_hazardZoneManager != null)
                return _hazardZoneManager;

            _hazardZoneManager = GlobalRegistry.HazardZones;
            if (_hazardZoneManager != null || !Application.isPlaying)
                return _hazardZoneManager;

            if (!TryGetComponent(out _hazardZoneManager))
            {
                _hazardZoneManager = gameObject.AddComponent<HazardZoneManager>(); // COLD ALLOC: HazardZoneManager[1] - environment-owned runtime hazard registry - owner: EnvironmentRuntimeContextService
            }

            return _hazardZoneManager;
        }

        private void EnsureSingletonOwnership()
        {
            EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterEnvironmentRuntimeContextRuntime(this);
        }

        private void SyncEnvironmentContext()
        {
            if (_constructionManager == null || !_constructionManager.isActiveAndEnabled)
                _constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;

            _moduleCatalog = _constructionManager != null ? _constructionManager.Catalog : null;

            if (_hazardZoneManager == null || !_hazardZoneManager.isActiveAndEnabled)
                _hazardZoneManager = GlobalRegistry.HazardZones;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterContext()
        {
            if (_registeredContext)
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
