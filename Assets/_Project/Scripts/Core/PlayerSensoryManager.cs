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
    public sealed class PlayerSensoryManager : MonoBehaviour, IPlayerSensoryService, IUpdatable
    {
        private static PlayerSensoryManager _instance;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredService;
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
            _instance = null;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        public static PlayerSensoryManager EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

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
            if (_instance != this)
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

        private void SyncSensoryContext()
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
