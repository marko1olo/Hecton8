using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using NASAPunk.Visor;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Focused player sensory/presentation service extracted from the player god object.
    /// Mirrors the authoritative player runtime context into a narrower GlobalRegistry slot.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9921)]
    public sealed class PlayerSensoryManager : MonoBehaviour, IPlayerSensoryService, IUpdatable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static PlayerSensoryManager s_activeRuntime;
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _syncInProgress;
        private GameObject _playerObject;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private Camera _playerCamera;
        private PlayerFlashlight _flashlight;
        private PlayerThrusterAudio _thrusterAudio;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private VisorHUDController _visorController;
        private HUDNotification _hudNotification;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private readonly List<VisorHUDController> _visorResolveBuffer = new List<VisorHUDController>(2); // COLD ALLOC: List<VisorHUDController>[2] - focused sensory hierarchy resolution buffer - owner: PlayerSensoryManager

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        internal Camera CachedPlayerCamera => _playerCamera;
        internal PlayerFlashlight CachedFlashlight => _flashlight;
        internal PlayerThrusterAudio CachedThrusterAudio => _thrusterAudio;
        internal HectonUnderwaterVisuals CachedUnderwaterVisuals => _underwaterVisuals;
        internal VisorHUDController CachedVisorController => _visorController;
        internal HUDNotification CachedHudNotification => _hudNotification;

        /// <inheritdoc />
        public Camera PlayerCamera
        {
            get { return _playerCamera; }
        }

        /// <inheritdoc />
        public PlayerFlashlight Flashlight
        {
            get { return _flashlight; }
        }

        /// <inheritdoc />
        public PlayerThrusterAudio ThrusterAudio
        {
            get { return _thrusterAudio; }
        }

        /// <inheritdoc />
        public HectonUnderwaterVisuals UnderwaterVisuals
        {
            get { return _underwaterVisuals; }
        }

        /// <inheritdoc />
        public VisorHUDController VisorController
        {
            get { return _visorController; }
        }

        /// <inheritdoc />
        public HUDNotification HudNotification
        {
            get { return _hudNotification; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
            GlobalRegistry.ClearPlayerSensoryRuntime(null);
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerSensoryManager EnsureRuntimeInstance()
        {
            PlayerSensoryManager runtime = s_activeRuntime != null
                ? s_activeRuntime
                : GlobalRegistry.PlayerSensoryRuntime;

            if (runtime != null)
            {
                s_activeRuntime = runtime;
                return runtime;
            }

            GameObject runtimeRoot = new GameObject("[PlayerSensoryManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned player sensory service root - owner: PlayerSensoryManager
            return runtimeRoot.AddComponent<PlayerSensoryManager>();
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                EnsureSingletonOwnership();
                if (!ReferenceEquals(s_activeRuntime, this))
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                SyncSensoryContextCold();
                return;
            }

            EnsureSingletonOwnership();
            if (!ReferenceEquals(s_activeRuntime, this))
                return;

            _isInitialized = true;
            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            TryRegisterService();
            SyncSensoryContextCold();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            RefreshSensoryContextHot();
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
                if (!ReferenceEquals(s_activeRuntime, this))
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterService();
                SyncSensoryContextCold();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterUpdatable();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterUpdatable();

                return;
            }

            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                SyncSensoryContextCold();
            }
        }

        private void ShutdownServiceState()
        {
            TryUnregisterUpdatable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _isInitialized = false;
            _syncInProgress = false;
            _playerObject = null;
            _playerTransform = null;
            _playerMovement = null;
            _playerCamera = null;
            _flashlight = null;
            _thrusterAudio = null;
            _underwaterVisuals = null;
            _visorController = null;
            _hudNotification = null;
            _playerRuntimeContext = null;
            _visorResolveBuffer.Clear();

            GlobalRegistry.ClearPlayerSensoryRuntime(this);
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private void EnsureSingletonOwnership()
        {
            PlayerSensoryManager runtime = s_activeRuntime != null
                ? s_activeRuntime
                : GlobalRegistry.PlayerSensoryRuntime;

            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPlayerSensoryRuntime(this);
            if (ReferenceEquals(GlobalRegistry.PlayerSensoryRuntime, this))
                s_activeRuntime = this;
        }

        private void RefreshSensoryContextHot()
        {
            if (TryApplyPlayerRuntimeContext(_playerRuntimeContext))
                return;

            GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject;
            if (!ReferenceEquals(_playerObject, currentPlayerObject))
            {
                _playerObject = currentPlayerObject;
                _playerTransform = currentPlayerObject != null ? currentPlayerObject.transform : null;
                _playerMovement = null;
                _playerCamera = null;
                _flashlight = null;
                _thrusterAudio = null;
                _visorController = null;
            }

            if (_playerCamera != null && !_playerCamera.isActiveAndEnabled)
                _playerCamera = null;

            if (_flashlight != null && !_flashlight.isActiveAndEnabled)
                _flashlight = null;

            if (_thrusterAudio != null && !_thrusterAudio.isActiveAndEnabled)
                _thrusterAudio = null;

            if (_visorController != null && !_visorController.isActiveAndEnabled)
                _visorController = null;

            if (_underwaterVisuals != null && !_underwaterVisuals.isActiveAndEnabled)
                _underwaterVisuals = null;

            if (_hudNotification != null && !_hudNotification.isActiveAndEnabled)
                _hudNotification = null;
        }

        private void SyncSensoryContextCold()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                if (_playerRuntimeContext == null)
                    _playerRuntimeContext = GlobalRegistry.RegisteredPlayer;

                if (TryApplyPlayerRuntimeContext(_playerRuntimeContext))
                    return;

                GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject;
                if (!ReferenceEquals(_playerObject, currentPlayerObject))
                {
                    _playerObject = currentPlayerObject;
                    _playerTransform = _playerObject != null ? _playerObject.transform : null;
                    _playerMovement = null;
                    _playerCamera = null;
                    _flashlight = null;
                    _thrusterAudio = null;
                    _visorController = null;
                }

                if (_playerObject != null)
                {
                    if (_playerMovement == null)
                        _playerObject.TryGetComponent(out _playerMovement);

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

                    if (_playerCamera == null)
                        _playerObject.TryGetComponent(out _playerCamera);

                    if (_flashlight == null)
                        _playerObject.TryGetComponent(out _flashlight);

                    if (_thrusterAudio == null)
                        _playerObject.TryGetComponent(out _thrusterAudio);

                    if (_playerTransform != null && _visorController == null)
                    {
                        _visorResolveBuffer.Clear();
                        _playerTransform.GetComponentsInChildren(true, _visorResolveBuffer);
                        if (_visorResolveBuffer.Count > 0)
                            _visorController = _visorResolveBuffer[0];
                    }
                }

                if (_underwaterVisuals == null)
                    _underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;

                if (_hudNotification == null || !_hudNotification.isActiveAndEnabled)
                    HUDNotification.TryGetActive(out _hudNotification);
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private bool TryApplyPlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            if (playerRuntimeContext == null)
                return false;

            GameObject playerObject = playerRuntimeContext.PlayerObject;
            if (playerObject == null)
                return false;

            _playerObject = playerObject;
            _playerTransform = playerRuntimeContext.PlayerTransform;
            _playerMovement = playerRuntimeContext.PlayerMovement;
            _playerCamera = playerRuntimeContext.PlayerCamera;
            _flashlight = playerRuntimeContext.Flashlight;
            _thrusterAudio = playerRuntimeContext.ThrusterAudio;
            _visorController = playerRuntimeContext.VisorController;
            _underwaterVisuals = playerRuntimeContext.UnderwaterVisuals;
            _hudNotification = playerRuntimeContext.HudNotification;
            return true;
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
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            IPlayerSensoryService registeredService = GlobalRegistry.RegisteredPlayerSensory;
            if (registeredService != null && !ReferenceEquals(registeredService, this))
                return;

            GlobalRegistry.RegisterPlayerSensoryService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.PlayerSensory, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterPlayerSensoryService(this);
            _registeredService = false;
        }
    }
}
