using Hecton8.Input;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Authoritative frame-cached gameplay input service. Captures native input once per frame and exposes a zero-GC snapshot through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9990)]
    public sealed class InputDispatcher : MonoBehaviour, IInputService, IUpdatable, ITickable
    {
        private const int BufferedActionCapacity = 15;
        private const float DefaultBufferedActionMaxAgeSeconds = 0.25f;

        private struct BufferedActionEntry
        {
            public PlayerBufferedAction Action;
            public int Frame;
            public float Time;
        }

        private static InputDispatcher _instance;

        private InputManager _nativeInputManager;
        private bool _registeredUpdatable;
        private bool _isInitialized;
        private bool _subscribedToNativeInput;
        private int _lastCapturedFrame = -1;
        private int _bufferWriteIndex;
        private Vector2 _pendingLookDelta;
        private uint _latchedActionBits;
        private PlayerInputState _currentState;

        // COLD ALLOC: BufferedActionEntry[15] - fixed player action buffering ring for pre-commit intent capture - owner: InputDispatcher
        private readonly BufferedActionEntry[] _bufferedActions = new BufferedActionEntry[BufferedActionCapacity];

        /// <summary>
        /// Returns true once the dispatcher is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Returns true when the underlying player input map is active and safe for gameplay reads.
        /// </summary>
        public bool IsPlayerInputEnabled => _nativeInputManager != null && _nativeInputManager.IsPlayerInputEnabled;

        /// <inheritdoc />
        public event System.Action OnInteract;

        /// <inheritdoc />
        public event System.Action OnPrimaryAction;

        /// <inheritdoc />
        public event System.Action OnSecondaryAction;

        /// <inheritdoc />
        public event System.Action OnTabNext;

        /// <inheritdoc />
        public event System.Action OnTabPrevious;

        /// <inheritdoc />
        public event System.Action OnToolSlot1;

        /// <inheritdoc />
        public event System.Action OnToolSlot2;

        /// <inheritdoc />
        public event System.Action OnToolSlot3;

        /// <inheritdoc />
        public event System.Action OnToolSlot4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Ensures a persistent runtime input dispatcher exists.
        /// </summary>
        /// <returns>Live dispatcher instance.</returns>
        public static InputDispatcher EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[InputDispatcher]"); // COLD ALLOC: GameObject[1] - runtime input dispatcher root - owner: InputDispatcher
            return runtimeRoot.AddComponent<InputDispatcher>();
        }

        /// <summary>
        /// Explicitly initializes the dispatcher and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                EnsureInputBinding();
                CaptureState();
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

            EnsureInputBinding();
            TryRegisterToDispatcher();
            GlobalRegistry.RegisterInputService(this);
            _isInitialized = true;
            CaptureState();
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            EnsureInputBinding();

            if (_isInitialized)
            {
                TryRegisterToDispatcher();
                GlobalRegistry.RegisterInputService(this);
                CaptureState();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromNativeInput();
            TryUnregisterFromDispatcher();

            if (_isInitialized)
                GlobalRegistry.UnregisterInputService(this);

            ClearFrameState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNativeInput();
            TryUnregisterFromDispatcher();

            if (_isInitialized)
                GlobalRegistry.UnregisterInputService(this);

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Captures the frame-cached input snapshot once at the start of the update cadence.
        /// </summary>
        /// <param name="deltaTime">Game tick delta time.</param>
        public void Tick(float deltaTime)
        {
            CaptureState();
        }

        /// <summary>
        /// Returns the cached input snapshot for the current frame.
        /// </summary>
        /// <returns>Current frame snapshot.</returns>
        public PlayerInputState GetState()
        {
            return _currentState;
        }

        /// <summary>
        /// Adds a discrete action token to the fixed 15-frame input buffer.
        /// </summary>
        /// <param name="action">Buffered action token.</param>
        public void BufferAction(PlayerBufferedAction action)
        {
            if (action == PlayerBufferedAction.None)
                return;

            _bufferedActions[_bufferWriteIndex].Action = action;
            _bufferedActions[_bufferWriteIndex].Frame = Time.frameCount;
            _bufferedActions[_bufferWriteIndex].Time = Time.time;
            _bufferWriteIndex++;
            if (_bufferWriteIndex >= BufferedActionCapacity)
                _bufferWriteIndex = 0;
        }

        /// <summary>
        /// Consumes the newest valid buffered action matching the requested token.
        /// </summary>
        /// <param name="action">Buffered action to consume.</param>
        /// <param name="maxAgeSeconds">Maximum valid age in seconds. Negative values fall back to the default 0.25s window.</param>
        /// <returns>True when a valid buffered action was consumed.</returns>
        public bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds)
        {
            if (action == PlayerBufferedAction.None)
                return false;

            float validWindowSeconds = maxAgeSeconds > 0f ? maxAgeSeconds : DefaultBufferedActionMaxAgeSeconds;
            int currentFrame = Time.frameCount;
            float currentTime = Time.time;

            for (int offset = 0; offset < BufferedActionCapacity; offset++)
            {
                int index = _bufferWriteIndex - 1 - offset;
                if (index < 0)
                    index += BufferedActionCapacity;

                BufferedActionEntry entry = _bufferedActions[index];
                if (entry.Action != action)
                    continue;

                if (currentFrame - entry.Frame >= BufferedActionCapacity || currentTime - entry.Time > validWindowSeconds)
                {
                    _bufferedActions[index].Action = PlayerBufferedAction.None;
                    continue;
                }

                _bufferedActions[index].Action = PlayerBufferedAction.None;
                return true;
            }

            return false;
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

        private void EnsureInputBinding()
        {
            InputManager currentInputManager = InputManager.Instance;
            if (ReferenceEquals(_nativeInputManager, currentInputManager))
                return;

            UnsubscribeFromNativeInput();
            _nativeInputManager = currentInputManager;
            SubscribeToNativeInput();
        }

        private void SubscribeToNativeInput()
        {
            if (_subscribedToNativeInput || _nativeInputManager == null)
                return;

            _nativeInputManager.OnLook += HandleLookInput;
            _nativeInputManager.OnJump += HandleJumpPressed;
            _nativeInputManager.OnInteract += HandleInteractPressed;
            _nativeInputManager.OnToolSlot1 += HandleToolSlot1Pressed;
            _nativeInputManager.OnToolSlot2 += HandleToolSlot2Pressed;
            _nativeInputManager.OnToolSlot3 += HandleToolSlot3Pressed;
            _nativeInputManager.OnToolSlot4 += HandleToolSlot4Pressed;
            _nativeInputManager.OnPrimaryAction += HandlePrimaryActionPressed;
            _nativeInputManager.OnSecondaryAction += HandleSecondaryActionPressed;
            _nativeInputManager.OnTabNext += HandleTabNextPressed;
            _nativeInputManager.OnTabPrevious += HandleTabPreviousPressed;
            _nativeInputManager.OnSprint += HandleSprintPressed;
            _subscribedToNativeInput = true;
        }

        private void UnsubscribeFromNativeInput()
        {
            if (!_subscribedToNativeInput || _nativeInputManager == null)
                return;

            _nativeInputManager.OnLook -= HandleLookInput;
            _nativeInputManager.OnJump -= HandleJumpPressed;
            _nativeInputManager.OnInteract -= HandleInteractPressed;
            _nativeInputManager.OnToolSlot1 -= HandleToolSlot1Pressed;
            _nativeInputManager.OnToolSlot2 -= HandleToolSlot2Pressed;
            _nativeInputManager.OnToolSlot3 -= HandleToolSlot3Pressed;
            _nativeInputManager.OnToolSlot4 -= HandleToolSlot4Pressed;
            _nativeInputManager.OnPrimaryAction -= HandlePrimaryActionPressed;
            _nativeInputManager.OnSecondaryAction -= HandleSecondaryActionPressed;
            _nativeInputManager.OnTabNext -= HandleTabNextPressed;
            _nativeInputManager.OnTabPrevious -= HandleTabPreviousPressed;
            _nativeInputManager.OnSprint -= HandleSprintPressed;
            _subscribedToNativeInput = false;
        }

        private void TryRegisterToDispatcher()
        {
            if (_registeredUpdatable)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = true;
        }

        private void TryUnregisterFromDispatcher()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void CaptureState()
        {
            EnsureInputBinding();

            int currentFrame = Time.frameCount;
            if (_lastCapturedFrame == currentFrame)
                return;

            _lastCapturedFrame = currentFrame;

            PlayerInputState state = default;
            InputManager inputManager = _nativeInputManager;
            if (inputManager != null && inputManager.IsPlayerInputEnabled)
            {
                uint actionBits = _latchedActionBits;
                if (inputManager.IsJumping)
                    actionBits |= (uint)PlayerInputAction.Jump;
                if (inputManager.IsPrimaryActionHeld)
                    actionBits |= (uint)PlayerInputAction.PrimaryFire;
                if (inputManager.IsSecondaryActionHeld)
                    actionBits |= (uint)PlayerInputAction.SecondaryFire;
                if (inputManager.IsSprinting)
                    actionBits |= (uint)PlayerInputAction.Sprint;

                state.MoveDelta = inputManager.MoveInput;
                state.LookDelta = _pendingLookDelta;
                state.VerticalDelta = Mathf.Clamp(inputManager.VerticalMovementInput, -1f, 1f);
                state.ActionsBitmask = actionBits;
            }

            _currentState = state;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
        }

        private void HandleLookInput(Vector2 lookDelta)
        {
            _pendingLookDelta += lookDelta;
        }

        private void HandleJumpPressed()
        {
            _latchedActionBits |= (uint)PlayerInputAction.Jump;
            BufferAction(PlayerBufferedAction.Jump);
        }

        private void HandleInteractPressed()
        {
            _latchedActionBits |= (uint)PlayerInputAction.Interact;
            OnInteract?.Invoke();
        }

        private void HandleToolSlot1Pressed()
        {
            OnToolSlot1?.Invoke();
        }

        private void HandleToolSlot2Pressed()
        {
            OnToolSlot2?.Invoke();
        }

        private void HandleToolSlot3Pressed()
        {
            OnToolSlot3?.Invoke();
        }

        private void HandleToolSlot4Pressed()
        {
            OnToolSlot4?.Invoke();
        }

        private void HandlePrimaryActionPressed()
        {
            _latchedActionBits |= (uint)PlayerInputAction.PrimaryFire;
            OnPrimaryAction?.Invoke();
        }

        private void HandleSecondaryActionPressed()
        {
            _latchedActionBits |= (uint)PlayerInputAction.SecondaryFire;
            OnSecondaryAction?.Invoke();
        }

        private void HandleTabNextPressed()
        {
            OnTabNext?.Invoke();
        }

        private void HandleTabPreviousPressed()
        {
            OnTabPrevious?.Invoke();
        }

        private void HandleSprintPressed()
        {
            _latchedActionBits |= (uint)PlayerInputAction.Sprint;
        }

        private void ClearFrameState()
        {
            _lastCapturedFrame = -1;
            _bufferWriteIndex = 0;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
            _currentState = default;

            for (int i = 0; i < BufferedActionCapacity; i++)
                _bufferedActions[i].Action = PlayerBufferedAction.None;
        }
    }
}
