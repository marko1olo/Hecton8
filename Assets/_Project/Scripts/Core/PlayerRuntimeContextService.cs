using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.UI;
using NASAPunk.Visor;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Bootstrap-owned player runtime context published through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9930)]
    public sealed class PlayerRuntimeContextService : MonoBehaviour, IPlayerRuntimeContext, IUpdatable
    {
        private static PlayerRuntimeContextService _instance;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredContext;
        private GameObject _playerRootOverride;
        private GameObject _playerObject;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private Rigidbody _playerRigidbody;
        private HectonSurvivalSystem _survivalSystem;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private TraumaDispatcher _traumaDispatcher;
        private Camera _playerCamera;
        private PlayerPDA _playerPda;
        private PlayerBuilder _playerBuilder;
        private VisorHUDController _visorController;
        private PlayerFlashlight _flashlight;
        private PlayerThrusterAudio _thrusterAudio;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private Transform _handAnchor;
        private Collider _playerCollider;
        private HUDNotification _hudNotification;
        private readonly PlayerRuntimeContext _runtimeContext = new PlayerRuntimeContext();
        private readonly List<VisorHUDController> _visorResolveBuffer = new List<VisorHUDController>(2); // COLD ALLOC: List<VisorHUDController>[2] â€” one-shot player visor child resolution buffer used during root rebinds â€” owner: PlayerRuntimeContextService

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public GameObject PlayerObject
        {
            get
            {
                SyncPlayerContext();
                return _playerObject;
            }
        }

        /// <inheritdoc />
        public Transform PlayerTransform
        {
            get
            {
                SyncPlayerContext();
                return _playerTransform;
            }
        }

        /// <inheritdoc />
        public HectonPlayerMovement PlayerMovement
        {
            get
            {
                SyncPlayerContext();
                return _playerMovement;
            }
        }

        /// <inheritdoc />
        public Rigidbody PlayerRigidbody
        {
            get
            {
                SyncPlayerContext();
                return _playerRigidbody;
            }
        }

        /// <inheritdoc />
        public PlayerToolManager ToolManager
        {
            get
            {
                SyncPlayerContext();
                return _toolManager;
            }
        }

        /// <inheritdoc />
        public PlayerInventory Inventory
        {
            get
            {
                SyncPlayerContext();
                return _inventory;
            }
        }

        /// <inheritdoc />
        public Camera PlayerCamera
        {
            get
            {
                SyncPlayerContext();
                return _playerCamera;
            }
        }

        /// <inheritdoc />
        public PlayerPDA PlayerPDA
        {
            get
            {
                SyncPlayerContext();
                return _playerPda;
            }
        }

        /// <inheritdoc />
        public PlayerBuilder PlayerBuilder
        {
            get
            {
                SyncPlayerContext();
                return _playerBuilder;
            }
        }

        /// <inheritdoc />
        public VisorHUDController VisorController
        {
            get
            {
                SyncPlayerContext();
                return _visorController;
            }
        }

        /// <inheritdoc />
        public PlayerFlashlight Flashlight
        {
            get
            {
                SyncPlayerContext();
                return _flashlight;
            }
        }

        /// <inheritdoc />
        public PlayerThrusterAudio ThrusterAudio
        {
            get
            {
                SyncPlayerContext();
                return _thrusterAudio;
            }
        }

        /// <inheritdoc />
        public HectonUnderwaterVisuals UnderwaterVisuals
        {
            get
            {
                SyncPlayerContext();
                return _underwaterVisuals;
            }
        }

        /// <inheritdoc />
        public Transform HandAnchor
        {
            get
            {
                SyncPlayerContext();
                return _handAnchor;
            }
        }

        /// <inheritdoc />
        public Collider PlayerCollider
        {
            get
            {
                SyncPlayerContext();
                return _playerCollider;
            }
        }

        /// <inheritdoc />
        public HUDNotification HudNotification
        {
            get
            {
                SyncPlayerContext();
                return _hudNotification;
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
        /// <returns>Live player context instance.</returns>
        public static PlayerRuntimeContextService EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[PlayerRuntimeContextService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned player runtime context root - owner: PlayerRuntimeContextService
            return runtimeRoot.AddComponent<PlayerRuntimeContextService>();
        }

        /// <summary>
        /// Binds a player root to the central runtime context even before bootstrap publishes it globally.
        /// </summary>
        public static bool TryBindPlayerRoot(GameObject playerRoot, out PlayerRuntimeContext runtimeContext)
        {
            runtimeContext = null;
            if (playerRoot == null)
                return false;

            PlayerRuntimeContextService runtimeService = EnsureRuntimeInstance();
            runtimeService.BindPlayerRoot(playerRoot);
            runtimeContext = runtimeService._runtimeContext;
            return runtimeContext != null && runtimeContext.IsBound;
        }

        /// <summary>
        /// Resolves the currently active player runtime context when one is available.
        /// </summary>
        public static bool TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            runtimeContext = null;
            if (_instance == null)
                return false;

            _instance.SyncPlayerContext();
            runtimeContext = _instance._runtimeContext;
            return runtimeContext != null && runtimeContext.IsBound;
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
                SyncPlayerContext();
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
            TryRegisterContext();
            SyncPlayerContext();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            SyncPlayerContext();
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
                SyncPlayerContext();
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

        private void BindPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            _playerRootOverride = playerRoot;
            SyncPlayerContext();
        }

        private void SyncPlayerContext()
        {
            GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject != null ? BootstrapState.CurrentPlayerObject : _playerRootOverride;
            if (ReferenceEquals(_playerObject, currentPlayerObject) &&
                _playerTransform != null &&
                (_hudNotification != null || !Application.isPlaying))
            {
                RefreshDynamicContextReferences();
                PublishMovementSnapshot();
                return;
            }

            _playerObject = currentPlayerObject;
            _playerTransform = _playerObject != null ? _playerObject.transform : null;
            _playerMovement = null;
            _playerRigidbody = null;
            _survivalSystem = null;
            _toolManager = null;
            _inventory = null;
            _playerTransportCoordinator = null;
            _traumaDispatcher = null;
            _playerCamera = null;
            _playerPda = null;
            _playerBuilder = null;
            _visorController = null;
            _flashlight = null;
            _thrusterAudio = null;
            _underwaterVisuals = null;
            _handAnchor = null;
            _playerCollider = null;
            _runtimeContext.Clear();

            if (_playerObject != null)
            {
                _playerObject.TryGetComponent(out _playerMovement);
                _playerObject.TryGetComponent(out _playerRigidbody);
                _playerObject.TryGetComponent(out _survivalSystem);
                _playerObject.TryGetComponent(out _toolManager);
                _playerObject.TryGetComponent(out _inventory);
                _playerObject.TryGetComponent(out _playerTransportCoordinator);
                _playerObject.TryGetComponent(out _traumaDispatcher);
                _playerObject.TryGetComponent(out _playerPda);
                _playerObject.TryGetComponent(out _playerCollider);

                ResolvePlayerHierarchyReferencesCold();
                RefreshDynamicContextReferences();
            }

            if (_underwaterVisuals == null)
                _underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;

            if (_hudNotification == null || !_hudNotification.isActiveAndEnabled)
                HUDNotification.TryGetActive(out _hudNotification);

            _runtimeContext.SyncReferences(
                _playerObject,
                _playerTransform,
                _playerMovement,
                _playerRigidbody,
                _survivalSystem,
                _toolManager,
                _inventory,
                _playerTransportCoordinator,
                _traumaDispatcher,
                _playerCamera,
                _playerPda,
                _playerBuilder,
                _visorController,
                _flashlight,
                _thrusterAudio,
                _underwaterVisuals,
                _handAnchor,
                _playerCollider,
                _hudNotification);
            PublishMovementSnapshot();
        }

        private void PublishMovementSnapshot()
        {
            if (!_runtimeContext.IsBound || _playerTransform == null)
                return;

            float3 velocity = _playerRigidbody != null ? (float3)_playerRigidbody.linearVelocity : float3.zero;
            float3 forward = _playerTransform.forward;
            float3 cameraForward = _playerCamera != null ? (float3)_playerCamera.transform.forward : forward;
            float depthMeters = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            float transportSpeedMultiplier = _playerTransportCoordinator != null
                ? math.max(0.01f, _playerTransportCoordinator.ResolveTransportSpeedMultiplier())
                : 1f;
            float underwaterStress01 = _playerMovement != null ? math.saturate(_playerMovement.CurrentUnderwaterStressIntensity01) : 0f;

            uint flags = (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;
            if (_playerMovement != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasMovement;
            if (_playerRigidbody != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasRigidbody;
            if (_survivalSystem != null)
            {
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasSurvival;
                if (_survivalSystem.IsAlive)
                    flags |= (uint)PlayerRuntimeSnapshotFlags.PlayerAlive;
                if (_survivalSystem.IsOxygenGraceActive)
                    flags |= (uint)PlayerRuntimeSnapshotFlags.OxygenGraceActive;
            }
            if (_toolManager != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasToolManager;
            if (_inventory != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasInventory;
            if (_playerTransportCoordinator != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTransport;
            if (_traumaDispatcher != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTrauma;
            if (_playerMovement != null && _playerMovement.CurrentDepth > 0f)
                flags |= (uint)PlayerRuntimeSnapshotFlags.Underwater;

            PlayerMovementRuntimeState movementState = default;
            movementState.WorldPosition = _playerTransform.position;
            movementState.Velocity = velocity;
            movementState.Forward = forward;
            movementState.CameraForward = cameraForward;
            movementState.DepthMeters = depthMeters;
            movementState.TransportSpeedMultiplier = transportSpeedMultiplier;
            movementState.UnderwaterStressIntensity01 = underwaterStress01;
            movementState.Flags = flags;
            _runtimeContext.PublishMovementState(in movementState);
        }

        private void RefreshDynamicContextReferences()
        {
            IPlayerInventoryService playerInventoryService = GlobalRegistry.PlayerInventory;
            if (playerInventoryService != null)
            {
                if (_toolManager == null)
                    _toolManager = playerInventoryService.ToolManager;

                if (_inventory == null)
                    _inventory = playerInventoryService.Inventory;

                if (_playerBuilder == null)
                    _playerBuilder = playerInventoryService.PlayerBuilder;

                if (_handAnchor == null)
                    _handAnchor = playerInventoryService.HandAnchor;
            }

            IPlayerSensoryService playerSensoryService = GlobalRegistry.PlayerSensory;
            if (playerSensoryService != null)
            {
                if (_playerCamera == null)
                    _playerCamera = playerSensoryService.PlayerCamera;

                if (_flashlight == null)
                    _flashlight = playerSensoryService.Flashlight;

                if (_thrusterAudio == null)
                    _thrusterAudio = playerSensoryService.ThrusterAudio;

                if (_underwaterVisuals == null)
                    _underwaterVisuals = playerSensoryService.UnderwaterVisuals;

                if (_visorController == null)
                    _visorController = playerSensoryService.VisorController;

                if (_hudNotification == null)
                    _hudNotification = playerSensoryService.HudNotification;
            }

            if (_toolManager != null)
            {
                if (_inventory == null)
                    _inventory = _toolManager.Inventory;

                _handAnchor = _toolManager.HandAnchor;
                _playerBuilder = _toolManager.CurrentTool as PlayerBuilder;
            }

            if (_playerMovement != null)
            {
                if (_playerRigidbody == null)
                    _playerMovement.TryGetComponent(out _playerRigidbody);
            }

            if (_playerObject != null)
            {
                if (_flashlight == null)
                    _playerObject.TryGetComponent(out _flashlight);

                if (_thrusterAudio == null)
                    _playerObject.TryGetComponent(out _thrusterAudio);

                if (_playerBuilder == null)
                    _playerObject.TryGetComponent(out _playerBuilder);
            }

            if (_playerPda == null)
                _playerPda = PlayerPDA.ActiveRuntimeInstance;
        }

        private void ResolvePlayerHierarchyReferencesCold()
        {
            if (_playerMovement != null)
            {
                Transform playerCameraTransform = _playerMovement.PlayerCameraTransform;
                if (playerCameraTransform != null)
                {
                    if (_playerCamera == null)
                        playerCameraTransform.TryGetComponent(out _playerCamera);

                    if (_flashlight == null)
                        playerCameraTransform.TryGetComponent(out _flashlight);

                    if (_thrusterAudio == null)
                        playerCameraTransform.TryGetComponent(out _thrusterAudio);
                }
            }

            if (_playerTransform != null && _visorController == null)
            {
                _visorResolveBuffer.Clear();
                _playerTransform.GetComponentsInChildren(true, _visorResolveBuffer);
                if (_visorResolveBuffer.Count > 0)
                    _visorController = _visorResolveBuffer[0];
            }
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable)
                return;

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

        private void TryRegisterContext()
        {
            if (_registeredContext)
                return;

            GlobalRegistry.RegisterPlayerRuntimeContext(this);
            _registeredContext = true;
        }

        private void TryUnregisterContext()
        {
            if (!_registeredContext)
                return;

            GlobalRegistry.UnregisterPlayerRuntimeContext(this);
            _registeredContext = false;
        }
    }
}
