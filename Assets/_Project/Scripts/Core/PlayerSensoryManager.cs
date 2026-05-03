using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using NASAPunk.Visor;
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
        private Camera _playerCamera;
        private PlayerFlashlight _flashlight;
        private PlayerThrusterAudio _thrusterAudio;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private VisorHUDController _visorController;
        private HUDNotification _hudNotification;

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
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            _flashlight = playerContext != null ? playerContext.Flashlight : null;
            _thrusterAudio = playerContext != null ? playerContext.ThrusterAudio : null;
            _underwaterVisuals = playerContext != null ? playerContext.UnderwaterVisuals : null;
            _visorController = playerContext != null ? playerContext.VisorController : null;
            _hudNotification = playerContext != null ? playerContext.HudNotification : null;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterPlayerSensoryService(this);
            _registeredService = true;
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
