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
    public sealed class PlayerInventoryManager : MonoBehaviour, IPlayerInventoryService, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        [Header("── Inventory Capacity ──────────────────")]
        [Tooltip("Authoritative player carry-capacity ceiling used by UI/readiness systems. Current inventory mass above this value is treated as encumbered.")]
        [SerializeField, Min(1f)] private float carryCapacityKilograms = 200f;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private bool _hotSwapRegistered;
        private bool _syncInProgress;
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
            if (_isInitialized)
            {
                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                SyncInventoryContext();
                return;
            }

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            TryRegisterService();
            SyncInventoryContext();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            SyncInventoryContext();
        }

        private void OnEnable()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            if (_isInitialized)
            {
                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                SyncInventoryContext();
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
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

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterUpdatable();
            TryUnregisterService();
            _isInitialized = false;
            _syncInProgress = false;
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
                TryRegisterUpdatable();
        }

        private void SyncInventoryContext()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject;
                if (!ReferenceEquals(_playerObject, currentPlayerObject))
                {
                    _playerObject = currentPlayerObject;
                    _toolManager = null;
                    _inventory = null;
                    _playerBuilder = null;
                    _handAnchor = null;
                }

                if (_playerObject == null)
                    return;

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

        private void TryRegisterService()
        {
            if (_registeredService)
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
