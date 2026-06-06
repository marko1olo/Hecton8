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
            if (ActiveRuntimeInstance != null)
                return ActiveRuntimeInstance;

            GameObject runtimeRoot = new GameObject("[PlayerInventoryManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned player inventory/tooling service root - owner: PlayerInventoryManager
            return runtimeRoot.AddComponent<PlayerInventoryManager>();
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (!EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                TryRegisterHotSwapListener();
                TryRegisterSlowTickable();
                TryRegisterService();
                RefreshPlayerRuntimeContextCold();
                SyncInventoryContextCold();
                return;
            }

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            TryRegisterService();
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
            if (!EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                TryRegisterHotSwapListener();
                TryRegisterSlowTickable();
                TryRegisterService();
                RefreshPlayerRuntimeContextCold();
                SyncInventoryContextCold();
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterSlowTickable();
            TryUnregisterService();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            ShutdownServiceState();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private bool EnsureSingletonOwnership()
        {
            PlayerInventoryManager runtime = ActiveRuntimeInstance;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return false;
            }

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (registeredService != null && !ReferenceEquals(registeredService, this))
            {
                Destroy(gameObject);
                return false;
            }

            ActiveRuntimeInstance = this;
            return true;
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
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
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredSlowTickable = false;
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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (registeredService != null && !ReferenceEquals(registeredService, this))
                return;

            GlobalRegistry.RegisterPlayerInventoryService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.PlayerInventory, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterPlayerInventoryService(this);
            _registeredService = false;
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
