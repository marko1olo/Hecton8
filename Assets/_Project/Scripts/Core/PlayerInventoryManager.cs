using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Focused player inventory/tooling service extracted from the player god object.
    /// Mirrors the authoritative player runtime context into a narrower GlobalRegistry slot.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9922)]
    public sealed class PlayerInventoryManager : MonoBehaviour, IPlayerInventoryService, ISlowTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        [Header("── Inventory Capacity ──────────────────")]
        [Tooltip("Authoritative player carry-capacity ceiling used by UI/readiness systems. Current inventory mass above this value is treated as encumbered.")]
        [SerializeField, Min(1f)] private float carryCapacityKilograms = 200f;

        private bool _isInitialized;
        private bool _registeredSlowTickable;
        private bool _registeredService;
        private bool _hotSwapRegistered;
        private bool _syncInProgress;
        private bool _runtimeOwnerAborted;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private GameObject _playerObject;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private PlayerBuilder _playerBuilder;
        private Transform _handAnchor;

        internal static PlayerInventoryManager ActiveRuntimeInstance { get; private set; }

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        public float CarryCapacityKilograms => Mathf.Max(1f, carryCapacityKilograms);
        internal PlayerToolManager CachedToolManager => _toolManager;
        internal PlayerInventory CachedInventory => _inventory;
        internal PlayerBuilder CachedPlayerBuilder => _playerBuilder;
        internal Transform CachedHandAnchor => _handAnchor;

        /// <inheritdoc />
        public PlayerToolManager ToolManager
        {
            get { return _toolManager; }
        }

        /// <inheritdoc />
        public PlayerInventory Inventory
        {
            get { return _inventory; }
        }

        /// <inheritdoc />
        public PlayerBuilder PlayerBuilder
        {
            get { return _playerBuilder; }
        }

        /// <inheritdoc />
        public Transform HandAnchor
        {
            get { return _handAnchor; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerInventoryManager EnsureRuntimeInstance()
        {
            PlayerInventoryManager runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (IsInventoryServiceUsable(registeredService) &&
                ReferenceEquals(registeredService as PlayerInventoryManager, null))
            {
                return null;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Tooling/inventory hot paths resolve through GlobalRegistry.RegisteredPlayerInventory;
            // without create the slot stays null when bootstrap reorders or skips the node.
            GameObject runtimeRoot = new GameObject("[PlayerInventoryManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned player inventory/tooling service root - owner: PlayerInventoryManager
            return runtimeRoot.AddComponent<PlayerInventoryManager>();
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
                if (!TryRegisterService())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterSlowTickable();
                RefreshPlayerRuntimeContextCold();
                SyncInventoryContextCold();
                return;
            }

            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            RefreshPlayerRuntimeContextCold();
            SyncInventoryContextCold();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            SyncInventoryContextHot();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                if (!TryRegisterService())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterSlowTickable();
                RefreshPlayerRuntimeContextCold();
                SyncInventoryContextCold();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(ActiveRuntimeInstance, this))
                    ActiveRuntimeInstance = null;

                return;
            }

            TryUnregisterHotSwapListener();
            TryUnregisterSlowTickable();
            TryUnregisterService();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            PlayerInventoryManager runtime = ActiveRuntimeInstance;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime._registeredService = false;
                runtime._isInitialized = false;
                ActiveRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            ActiveRuntimeInstance = this;
            return true;
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            TryUnregisterSlowTickable();
            TryUnregisterService();
            _isInitialized = false;
            _syncInProgress = false;
            _playerRuntimeContext = null;
            ClearCachedPlayerReferences();
        }

        private void ClearCachedPlayerReferences()
        {
            _playerObject = null;
            _toolManager = null;
            _inventory = null;
            _playerBuilder = null;
            _handAnchor = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTickable();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterSlowTickable();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                if (_playerRuntimeContext == null)
                {
                    ClearCachedPlayerReferences();
                    return;
                }

                SyncInventoryContextHot();
            }
        }

        private void RefreshPlayerRuntimeContextCold()
        {
            _playerRuntimeContext = GlobalRegistry.RegisteredPlayer ?? PlayerRuntimeContextService.ActiveRuntimeContext;
        }

        private void SyncInventoryContextHot()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
                if (runtimeContext == null)
                    return;

                SyncPlayerObjectReference(runtimeContext.PlayerObject);
                if (_playerObject == null || !ReferenceEquals(runtimeContext.PlayerObject, _playerObject))
                    return;

                _toolManager = runtimeContext.ToolManager;
                _inventory = runtimeContext.Inventory;
                _playerBuilder = runtimeContext.PlayerBuilder;
                _handAnchor = runtimeContext.HandAnchor;
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private void SyncInventoryContextCold()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
                GameObject currentPlayerObject = runtimeContext != null && runtimeContext.PlayerObject != null
                    ? runtimeContext.PlayerObject
                    : BootstrapState.CurrentPlayerObject;

                SyncPlayerObjectReference(currentPlayerObject);
                if (_playerObject == null)
                    return;

                if (runtimeContext != null && ReferenceEquals(runtimeContext.PlayerObject, _playerObject))
                {
                    _toolManager = runtimeContext.ToolManager;
                    _inventory = runtimeContext.Inventory;
                    _playerBuilder = runtimeContext.PlayerBuilder;
                    _handAnchor = runtimeContext.HandAnchor;
                }

                if (_toolManager == null)
                    _playerObject.TryGetComponent(out _toolManager);

                if (_inventory == null)
                    _playerObject.TryGetComponent(out _inventory);

                if (_toolManager == null)
                    return;

                if (_inventory == null)
                    _inventory = _toolManager.Inventory;

                _handAnchor = _toolManager.HandAnchor;

                if (_playerBuilder == null)
                    _playerObject.TryGetComponent(out _playerBuilder);
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private void SyncPlayerObjectReference(GameObject currentPlayerObject)
        {
            if (ReferenceEquals(_playerObject, currentPlayerObject))
                return;

            _playerObject = currentPlayerObject;
            _toolManager = null;
            _inventory = null;
            _playerBuilder = null;
            _handAnchor = null;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredSlowTickable = false;
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredService)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (!ReferenceEquals(registeredService, null) && !ReferenceEquals(registeredService, this))
            {
                PlayerInventoryManager staleRuntime = registeredService as PlayerInventoryManager;
                if (ReferenceEquals(staleRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterPlayerInventoryService(registeredService);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(ActiveRuntimeInstance, staleRuntime))
                    ActiveRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPlayerInventoryService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.PlayerInventory, this);
            _runtimeOwnerAborted = !_registeredService;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredService;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterPlayerInventoryService(this);
            _registeredService = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PlayerInventoryManager runtime = ActiveRuntimeInstance;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsInventoryRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                runtime._registeredService = false;
                runtime._isInitialized = false;
                ActiveRuntimeInstance = null;
            }

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (ReferenceEquals(registeredService, null) || ReferenceEquals(registeredService, this))
                return false;

            if (IsInventoryServiceUsable(registeredService))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            PlayerInventoryManager staleRuntime = registeredService as PlayerInventoryManager;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterPlayerInventoryService(registeredService);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(ActiveRuntimeInstance, staleRuntime))
                    ActiveRuntimeInstance = null;
            }

            return false;
        }

        private static PlayerInventoryManager ResolveUsableRuntime()
        {
            PlayerInventoryManager runtime = ActiveRuntimeInstance;
            if (IsInventoryRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                runtime._registeredService = false;
                runtime._isInitialized = false;
                ActiveRuntimeInstance = null;
            }

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (IsInventoryServiceUsable(registeredService))
                return registeredService as PlayerInventoryManager;

            PlayerInventoryManager staleRuntime = registeredService as PlayerInventoryManager;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterPlayerInventoryService(registeredService);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(ActiveRuntimeInstance, staleRuntime))
                    ActiveRuntimeInstance = null;
            }

            return null;
        }

        private static bool IsInventoryServiceUsable(IPlayerInventoryService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            PlayerInventoryManager runtime = service as PlayerInventoryManager;
            return ReferenceEquals(runtime, null) ||
                   (runtime._registeredService && IsInventoryRuntimeUsable(runtime));
        }

        private static bool IsInventoryRuntimeUsable(PlayerInventoryManager runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
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
    }
}
