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
    public sealed class PlayerInventoryManager : MonoBehaviour, IPlayerInventoryService, IUpdatable, IServiceHeartbeat, IServiceShutdown
    {
        [Header("── Inventory Capacity ──────────────────")]
        [Tooltip("Authoritative player carry-capacity ceiling used by UI/readiness systems. Current inventory mass above this value is treated as encumbered.")]
        [SerializeField, Min(1f)] private float carryCapacityKilograms = 200f;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private bool _syncInProgress;
        private GameObject _playerObject;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private PlayerBuilder _playerBuilder;
        private Transform _handAnchor;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        internal float CarryCapacityKilograms => Mathf.Max(1f, carryCapacityKilograms);
        internal PlayerToolManager CachedToolManager => _toolManager;
        internal PlayerInventory CachedInventory => _inventory;
        internal PlayerBuilder CachedPlayerBuilder => _playerBuilder;
        internal Transform CachedHandAnchor => _handAnchor;

        /// <inheritdoc />
        public PlayerToolManager ToolManager
        {
            get
            {
                SyncInventoryContext();
                return _toolManager;
            }
        }

        /// <inheritdoc />
        public PlayerInventory Inventory
        {
            get
            {
                SyncInventoryContext();
                return _inventory;
            }
        }

        /// <inheritdoc />
        public PlayerBuilder PlayerBuilder
        {
            get
            {
                SyncInventoryContext();
                return _playerBuilder;
            }
        }

        /// <inheritdoc />
        public Transform HandAnchor
        {
            get
            {
                SyncInventoryContext();
                return _handAnchor;
            }
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerInventoryManager EnsureRuntimeInstance()
        {
            IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;
            if (registeredService != null)
                return registeredService as PlayerInventoryManager;

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
                TryRegisterUpdatable();
                TryRegisterService();
                SyncInventoryContext();
                return;
            }

            _isInitialized = true;
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
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterService();
                SyncInventoryContext();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterService();
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
            TryUnregisterService();
            _isInitialized = false;
            _syncInProgress = false;
            _playerObject = null;
            _toolManager = null;
            _inventory = null;
            _playerBuilder = null;
            _handAnchor = null;
        }

        private void SyncInventoryContext()
        {
            if (_syncInProgress)
                return;

            if (!GlobalRegistry.TryBeginResolution(GlobalRegistry.GlobalRegistryResolutionScope.PlayerInventory))
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
                _playerBuilder = _toolManager.CurrentTool as PlayerBuilder;
            }
            finally
            {
                _syncInProgress = false;
                GlobalRegistry.EndResolution(GlobalRegistry.GlobalRegistryResolutionScope.PlayerInventory);
            }
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
    }
}
