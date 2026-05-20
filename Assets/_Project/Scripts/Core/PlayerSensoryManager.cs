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
    public sealed class PlayerSensoryManager : MonoBehaviour, IPlayerSensoryService, IUpdatable, IServiceHeartbeat, IServiceShutdown
    {
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
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
            get
            {
                SyncSensoryContext();
                return _playerCamera;
            }
        }

        /// <inheritdoc />
        public PlayerFlashlight Flashlight
        {
            get
            {
                SyncSensoryContext();
                return _flashlight;
            }
        }

        /// <inheritdoc />
        public PlayerThrusterAudio ThrusterAudio
        {
            get
            {
                SyncSensoryContext();
                return _thrusterAudio;
            }
        }

        /// <inheritdoc />
        public HectonUnderwaterVisuals UnderwaterVisuals
        {
            get
            {
                SyncSensoryContext();
                return _underwaterVisuals;
            }
        }

        /// <inheritdoc />
        public VisorHUDController VisorController
        {
            get
            {
                SyncSensoryContext();
                return _visorController;
            }
        }

        /// <inheritdoc />
        public HUDNotification HudNotification
        {
            get
            {
                SyncSensoryContext();
                return _hudNotification;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GlobalRegistry.ClearPlayerSensoryRuntime(null);
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerSensoryManager EnsureRuntimeInstance()
        {
            PlayerSensoryManager runtime = GlobalRegistry.PlayerSensoryRuntime;
            if (runtime != null)
                return runtime;

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
                TryRegisterUpdatable();
                TryRegisterService();
                SyncSensoryContext();
                return;
            }

            EnsureSingletonOwnership();
            if (GlobalRegistry.PlayerSensoryRuntime != this)
                return;

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterService();
            SyncSensoryContext();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            SyncSensoryContext();
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
                SyncSensoryContext();
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
            _playerTransform = null;
            _playerMovement = null;
            _playerCamera = null;
            _flashlight = null;
            _thrusterAudio = null;
            _underwaterVisuals = null;
            _visorController = null;
            _hudNotification = null;
            _visorResolveBuffer.Clear();

            GlobalRegistry.ClearPlayerSensoryRuntime(this);
        }

        private void EnsureSingletonOwnership()
        {
            PlayerSensoryManager runtime = GlobalRegistry.PlayerSensoryRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPlayerSensoryRuntime(this);
        }

        private void SyncSensoryContext()
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
