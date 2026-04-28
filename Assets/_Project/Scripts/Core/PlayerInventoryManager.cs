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
    public sealed class PlayerInventoryManager : MonoBehaviour, IPlayerInventoryService, IUpdatable
    {
        private static PlayerInventoryManager _instance;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private PlayerBuilder _playerBuilder;
        private Transform _handAnchor;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerInventoryManager EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

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

            EnsureSingletonOwnership();
            if (_instance != this)
                return;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
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

        private void Awake()
        {
            EnsureSingletonOwnership();
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
            TryUnregisterUpdatable();
            TryUnregisterService();

            if (_instance == this)
                _instance = null;
        }

        private void EnsureSingletonOwnership()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void SyncInventoryContext()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _toolManager = playerContext != null ? playerContext.ToolManager : null;
            _inventory = playerContext != null ? playerContext.Inventory : null;
            _playerBuilder = playerContext != null ? playerContext.PlayerBuilder : null;
            _handAnchor = playerContext != null ? playerContext.HandAnchor : null;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = true;
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
            _registeredService = true;
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
