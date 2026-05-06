using Hecton8.Input;
using Hecton8.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Core
{
    /// <summary>
    /// Authoritative frame-cached gameplay input service. Captures native input once per frame and exposes a zero-GC snapshot through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9990)]
    public sealed class InputDispatcher : MonoBehaviour, IInputService, IUpdatable, ITickable, IServiceHeartbeat
    {
        private const int BufferedActionCapacity = 15;
        private const float DefaultBufferedActionMaxAgeSeconds = 0.25f;
        private const float LookHotSwapBlendDurationSeconds = 0.25f;
        private const float LookBlendEpsilon = 0.0001f;
        private const float LookCurveDeadzone = 0.035f;
        private const float LookCurveRange = 1f - LookCurveDeadzone;

        private struct BufferedActionEntry
        {
            public PlayerBufferedAction Action;
            public int Frame;
            public float Time;
        }

        private InputManager _nativeInputManager;
        private Gamepad _cachedGamepad;
        private bool _registeredUpdatable;
        private bool _registeredInputService;
        private bool _isInitialized;
        private bool _subscribedToNativeInput;
        private bool _subscribedToDeviceChanges;
        private int _lastCapturedFrame = -1;
        private int _bufferWriteIndex;
        private Vector2 _pendingLookDelta;
        private uint _latchedActionBits;
        private float _appliedLowMotorSpeed;
        private float _appliedHighMotorSpeed;
        private float _lookBlendElapsed;
        private bool _lookBlendActive;
        private Vector2 _lookBlendFrom;
        private Vector2 _lastDeliveredLookDelta;
        private PlayerInputState _currentState;

        internal static InputDispatcher ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        // COLD ALLOC: BufferedActionEntry[15] - fixed player action buffering ring for pre-commit intent capture - owner: InputDispatcher
        private readonly BufferedActionEntry[] _bufferedActions = new BufferedActionEntry[BufferedActionCapacity];

        /// <summary>
        /// Returns true once the dispatcher is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <summary>
        /// Returns true when the underlying player input map is active and safe for gameplay reads.
        /// </summary>
        public bool IsPlayerInputEnabled => _nativeInputManager != null && _nativeInputManager.IsPlayerInputEnabled;

        internal InputManager NativeInputManager => _nativeInputManager;

        /// <summary>
        /// Binds the bootstrap-owned native input action owner used by this dispatcher.
        /// </summary>
        /// <param name="inputManager">Native input manager validated by the bootstrapper.</param>
        public void BindNativeInputManager(InputManager inputManager)
        {
            if (ReferenceEquals(_nativeInputManager, inputManager))
                return;

            UnsubscribeFromNativeInput();
            _nativeInputManager = inputManager;
            SubscribeToNativeInput();
            CaptureState();
        }

        /// <inheritdoc />
        public event System.Action OnInteract;

        /// <inheritdoc />
        public event System.Action OnPrimaryAction;

        /// <inheritdoc />
        public event System.Action OnSecondaryAction;

        /// <inheritdoc />
        public event System.Action OnPDA;

        /// <inheritdoc />
        public event System.Action OnInventory;

        /// <inheritdoc />
        public event System.Action OnCancel;

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

            EnsureInputBinding();
            EnsureHapticDeviceBinding();
            TryRegisterToDispatcher();
            _isInitialized = true;
            TryRegisterInputService();
            CaptureState();
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            EnsureInputBinding();
            EnsureHapticDeviceBinding();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

            EnsureInputBinding();
            EnsureHapticDeviceBinding();

            if (_isInitialized)
            {
                TryRegisterToDispatcher();
                TryRegisterInputService();
                CaptureState();
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            UnsubscribeFromNativeInput();
            ResetGamepadHaptics();
            UnsubscribeFromDeviceChanges();
            TryUnregisterFromDispatcher();

            TryUnregisterInputService();

            ClearFrameState();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            UnsubscribeFromNativeInput();
            ResetGamepadHaptics();
            UnsubscribeFromDeviceChanges();
            TryUnregisterFromDispatcher();

            TryUnregisterInputService();
        }

        /// <summary>
        /// Captures the frame-cached input snapshot once at the start of the update cadence.
        /// </summary>
        /// <param name="deltaTime">Game tick delta time.</param>
        public void Tick(float deltaTime)
        {
            CaptureState(deltaTime);
            DrainToolHaptics();
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

        /// <inheritdoc />
        public void SwitchToPlayerInput()
        {
            if (_nativeInputManager != null)
            {
                _nativeInputManager.SwitchToPlayerInput();
                BeginLookHotSwapBlend();
            }
        }

        /// <inheritdoc />
        public void SwitchToUIInput()
        {
            if (_nativeInputManager != null)
            {
                _nativeInputManager.SwitchToUIInput();
                _lookBlendActive = false;
                _pendingLookDelta = Vector2.zero;
            }
        }

        private void EnsureInputBinding()
        {
            if (_nativeInputManager == null || _subscribedToNativeInput)
                return;

            SubscribeToNativeInput();
        }

        private void EnsureHapticDeviceBinding()
        {
            SubscribeToDeviceChanges();
            ResolveCachedGamepad();
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
            _nativeInputManager.OnPDA += HandlePDAPressed;
            _nativeInputManager.OnInventory += HandleInventoryPressed;
            _nativeInputManager.OnCancel += HandleCancelPressed;
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
            _nativeInputManager.OnPDA -= HandlePDAPressed;
            _nativeInputManager.OnInventory -= HandleInventoryPressed;
            _nativeInputManager.OnCancel -= HandleCancelPressed;
            _nativeInputManager.OnTabNext -= HandleTabNextPressed;
            _nativeInputManager.OnTabPrevious -= HandleTabPreviousPressed;
            _nativeInputManager.OnSprint -= HandleSprintPressed;
            _subscribedToNativeInput = false;
        }

        private void SubscribeToDeviceChanges()
        {
            if (_subscribedToDeviceChanges)
                return;

            InputSystem.onDeviceChange += HandleDeviceChange;
            _subscribedToDeviceChanges = true;
        }

        private void UnsubscribeFromDeviceChanges()
        {
            if (!_subscribedToDeviceChanges)
                return;

            InputSystem.onDeviceChange -= HandleDeviceChange;
            _subscribedToDeviceChanges = false;
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad gamepad))
                return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.UsageChanged:
                    if (_cachedGamepad == null)
                        _cachedGamepad = gamepad;
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    if (ReferenceEquals(_cachedGamepad, gamepad))
                    {
                        ResetGamepadHaptics();
                        _cachedGamepad = null;
                        ResolveCachedGamepad();
                    }
                    break;
            }
        }

        private void ResolveCachedGamepad()
        {
            if (_cachedGamepad != null && _cachedGamepad.added)
                return;

            _cachedGamepad = null;
            var gamepads = Gamepad.all;
            for (int i = 0; i < gamepads.Count; i++)
            {
                Gamepad gamepad = gamepads[i];
                if (gamepad == null || !gamepad.added)
                    continue;

                _cachedGamepad = gamepad;
                break;
            }
        }

        private void TryRegisterToDispatcher()
        {
            if (_registeredUpdatable)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterFromDispatcher()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterInputService()
        {
            if (!_isInitialized)
                return;

            if (_registeredInputService)
                return;

            if (ReferenceEquals(GlobalRegistry.RegisteredInput, this))
            {
                _registeredInputService = true;
                return;
            }

            GlobalRegistry.RegisterInputService(this);
            _registeredInputService = ReferenceEquals(GlobalRegistry.RegisteredInput, this);
        }

        private void TryUnregisterInputService()
        {
            if (!_registeredInputService)
                return;

            if (ReferenceEquals(GlobalRegistry.RegisteredInput, this))
                GlobalRegistry.UnregisterInputService(this);

            _registeredInputService = false;
        }

        private void CaptureState(float deltaTime = 0f)
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

                Vector2 lookDelta = _pendingLookDelta;
                if (_lookBlendActive)
                    lookDelta = ResolveLookHotSwapBlend(lookDelta, deltaTime);

                state.MoveDelta = inputManager.MoveInput;
                state.LookDelta = lookDelta;
                state.VerticalDelta = Mathf.Clamp(inputManager.VerticalMovementInput, -1f, 1f);
                state.ActionsBitmask = actionBits;
                _lastDeliveredLookDelta = lookDelta;
            }

            _currentState = state;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
        }

        private void BeginLookHotSwapBlend()
        {
            _lookBlendFrom = _lastDeliveredLookDelta;
            _lookBlendElapsed = 0f;
            _lookBlendActive = true;
        }

        private Vector2 ResolveLookHotSwapBlend(Vector2 targetLookDelta, float deltaTime)
        {
            _lookBlendElapsed = math.min(
                _lookBlendElapsed + math.max(0f, deltaTime),
                LookHotSwapBlendDurationSeconds);

            float normalized = LookHotSwapBlendDurationSeconds > 0f
                ? math.saturate(_lookBlendElapsed / LookHotSwapBlendDurationSeconds)
                : 1f;
            float eased = normalized * normalized * (3f - (2f * normalized));
            Vector2 lookDelta = SlerpLookDelta(_lookBlendFrom, targetLookDelta, eased);
            if (normalized >= 1f)
                _lookBlendActive = false;

            return lookDelta;
        }

        private static Vector2 SlerpLookDelta(Vector2 from, Vector2 to, float t)
        {
            float2 fromDelta = new float2(from.x, from.y);
            float2 toDelta = new float2(to.x, to.y);
            float fromMagnitude = math.length(fromDelta);
            float toMagnitude = math.length(toDelta);

            if (fromMagnitude <= LookBlendEpsilon || toMagnitude <= LookBlendEpsilon)
                return Vector2.Lerp(from, to, t);

            float2 fromNormal = fromDelta / fromMagnitude;
            float2 toNormal = toDelta / toMagnitude;
            float magnitude = math.lerp(fromMagnitude, toMagnitude, t);
            quaternion fromRotation = quaternion.AxisAngle(new float3(0f, 0f, 1f), math.atan2(fromNormal.y, fromNormal.x));
            quaternion toRotation = quaternion.AxisAngle(new float3(0f, 0f, 1f), math.atan2(toNormal.y, toNormal.x));
            float3 blendedDirection = math.mul(math.slerp(fromRotation, toRotation, t), new float3(1f, 0f, 0f));
            return new Vector2(blendedDirection.x * magnitude, blendedDirection.y * magnitude);
        }

        private static Vector2 ApplyQuadraticLookCurve(Vector2 lookDelta)
        {
            float2 raw = new float2(lookDelta.x, lookDelta.y);
            float magnitude = math.length(raw);
            if (magnitude <= LookCurveDeadzone)
                return Vector2.zero;

            float normalized = math.saturate((magnitude - LookCurveDeadzone) / LookCurveRange);
            float quadratic = normalized * normalized;
            float gain = quadratic / math.max(normalized, LookBlendEpsilon);
            return new Vector2(lookDelta.x * gain, lookDelta.y * gain);
        }

        private void HandleLookInput(Vector2 lookDelta)
        {
            _pendingLookDelta += ApplyQuadraticLookCurve(lookDelta);
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

        private void HandlePDAPressed()
        {
            OnPDA?.Invoke();
        }

        private void HandleInventoryPressed()
        {
            OnInventory?.Invoke();
        }

        private void HandleCancelPressed()
        {
            OnCancel?.Invoke();
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

        private void DrainToolHaptics()
        {
            if (!ToolHapticsRuntime.TryGetRuntime(out ToolHapticsRuntime runtime) || runtime.FrontCount <= 0)
            {
                ApplyGamepadHaptics(0f, 0f);
                return;
            }

            var commandBuffer = runtime.GetFrontBuffer();
            float lowMotor = 0f;
            float highMotor = 0f;
            byte lowPriority = 0;
            byte highPriority = 0;
            bool hasLowPriority = false;
            bool hasHighPriority = false;
            int commandCount = runtime.FrontCount;
            for (int i = 0; i < commandCount; i++)
            {
                ToolHapticsRuntime.HapticCommand command = commandBuffer[i];
                if (command.DurationRemaining <= 0f)
                    continue;

                float lowContribution = (command.MotorMask & 0b0001) != 0
                    ? math.saturate(command.LowFreqIntensity)
                    : 0f;
                float highContribution = (command.MotorMask & 0b0010) != 0
                    ? math.saturate(command.HighFreqIntensity)
                    : 0f;

                ApplyHapticContribution(
                    lowContribution,
                    command.Priority,
                    command.BlendMode,
                    ref lowMotor,
                    ref lowPriority,
                    ref hasLowPriority);
                ApplyHapticContribution(
                    highContribution,
                    command.Priority,
                    command.BlendMode,
                    ref highMotor,
                    ref highPriority,
                    ref hasHighPriority);
            }

            ApplyGamepadHaptics(lowMotor, highMotor);
        }

        private static void ApplyHapticContribution(
            float contribution,
            byte priority,
            byte blendMode,
            ref float motorValue,
            ref byte motorPriority,
            ref bool hasPriority)
        {
            if (contribution <= 0f)
                return;

            if (!hasPriority || priority > motorPriority)
            {
                motorValue = 0f;
                motorPriority = priority;
                hasPriority = true;
            }

            if (priority < motorPriority)
                return;

            switch (blendMode)
            {
                case 0:
                    motorValue = contribution;
                    break;

                case 1:
                    motorValue = math.saturate(motorValue + contribution);
                    break;

                default:
                    motorValue = math.max(motorValue, contribution);
                    break;
            }
        }

        private void ApplyGamepadHaptics(float lowMotor, float highMotor)
        {
            lowMotor = math.saturate(lowMotor);
            highMotor = math.saturate(highMotor);
            if (math.abs(lowMotor - _appliedLowMotorSpeed) <= 0.001f &&
                math.abs(highMotor - _appliedHighMotorSpeed) <= 0.001f)
            {
                return;
            }

            if (_cachedGamepad != null && !_cachedGamepad.added)
                _cachedGamepad = null;

            if (_cachedGamepad != null)
                _cachedGamepad.SetMotorSpeeds(lowMotor, highMotor);

            _appliedLowMotorSpeed = lowMotor;
            _appliedHighMotorSpeed = highMotor;
        }

        private void ResetGamepadHaptics()
        {
            if (_cachedGamepad != null)
                _cachedGamepad.SetMotorSpeeds(0f, 0f);

            _appliedLowMotorSpeed = 0f;
            _appliedHighMotorSpeed = 0f;
        }

        private void ClearFrameState()
        {
            _lastCapturedFrame = -1;
            _bufferWriteIndex = 0;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
            _appliedLowMotorSpeed = 0f;
            _appliedHighMotorSpeed = 0f;
            _lookBlendElapsed = 0f;
            _lookBlendActive = false;
            _lookBlendFrom = Vector2.zero;
            _lastDeliveredLookDelta = Vector2.zero;
            _currentState = default;

            for (int i = 0; i < BufferedActionCapacity; i++)
                _bufferedActions[i].Action = PlayerBufferedAction.None;
        }
    }
}
