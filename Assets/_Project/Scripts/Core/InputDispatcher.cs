using Hecton8.Input;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;

namespace Hecton8.Core
{
    /// <summary>
    /// Authoritative frame-cached gameplay input service. Captures native input once per frame and exposes a zero-GC snapshot through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9990)]
    public sealed class InputDispatcher : MonoBehaviour, IInputService, IUpdatable, ITickable, IServiceHeartbeat, IServiceShutdown, IDispatcherRaycastReceiver, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const int BufferedActionCapacity = 15;
        private const int XRInputStateCapacity = 2;
        private const int XRControllerActiveBitCount = 5;
        private const int XRLookAtCommandCapacity = 1;
        private const int XRDeviceRescanIntervalFrames = 30;
        private const int XRLookAtSelectionRequestId = 8801;
        private const float XRLookAtSelectionDistanceMeters = 12f;
        private const float XRLookAtSelectionDistanceSq = XRLookAtSelectionDistanceMeters * XRLookAtSelectionDistanceMeters;
        private const float XRLookAtReuseOriginDriftMeters = 0.08f;
        private const float XRLookAtReuseOriginDriftSq = XRLookAtReuseOriginDriftMeters * XRLookAtReuseOriginDriftMeters;
        private const float XRLookAtReuseLateralDriftMeters = 0.12f;
        private const float XRLookAtReuseLateralDriftSq = XRLookAtReuseLateralDriftMeters * XRLookAtReuseLateralDriftMeters;
        private const float XRLookAtReuseForwardDot = 0.9992f;
        private const float XRLookAtReuseForwardDotSq = XRLookAtReuseForwardDot * XRLookAtReuseForwardDot;
        private const int XRLookAtReuseMaxFrames = 3;
        private const float DefaultBufferedActionMaxAgeSeconds = 0.25f;
        private const float LookHotSwapBlendDurationSeconds = 0.25f;
        private const float LookCurveDeadzone = 0.035f;
        private const float LookCurveDeadzoneSq = LookCurveDeadzone * LookCurveDeadzone;
        private const float LookCurveRangeSq = 1f - LookCurveDeadzoneSq;
        private const float XRAnalogNoiseFloor = 0.05f;
        private const float XRAnalogNoiseFloorSq = XRAnalogNoiseFloor * XRAnalogNoiseFloor;
        private const float HapticMotorWriteEpsilon = 0.01f;
        private const float XRHapticMotorWriteEpsilon = 0.015f;
        private const float XRHapticImpulseDurationSeconds = 0.045f;
        private const float XRHapticRefreshIntervalSeconds = 0.033f;
        private const byte HapticLowMotorMask = 0b0001;
        private const byte HapticHighMotorMask = 0b0010;
        private const byte HapticBlendOverride = 0;
        private const byte HapticBlendAdditive = 1;
        private const uint XRRuntimeFlagLookAtRayCommandEnabled = 1u << 0;
        private const uint XRRuntimeFlagInputSnapshotActive = 1u << 1;
        private const uint XRRuntimeFlagsAny = XRRuntimeFlagLookAtRayCommandEnabled | XRRuntimeFlagInputSnapshotActive;
        private static readonly QueryParameters XRLookAtEnabledQueryParameters = new QueryParameters(HectonLayerMasks.DefaultRaycastLayerMask, false, QueryTriggerInteraction.Ignore);
        private static readonly QueryParameters XRLookAtDisabledQueryParameters = new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore);
        private static readonly RaycastCommand DisabledXRLookAtRayCommand = new RaycastCommand(Vector3.zero, Vector3.forward, XRLookAtDisabledQueryParameters, 0.01f);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BufferedActionEntry
        {
            public PlayerBufferedAction Action;
            public int Frame;
            public float Time;
        }

        private InputManager _nativeInputManager;
        private Gamepad _cachedGamepad;
        private XRController _cachedLeftXRController;
        private XRController _cachedRightXRController;
        private AxisControl _leftTriggerAxis;
        private AxisControl _rightTriggerAxis;
        private AxisControl _leftGripAxis;
        private AxisControl _rightGripAxis;
        private Vector2Control _leftJoystickAxis;
        private Vector2Control _rightJoystickAxis;
        private ButtonControl _leftTriggerButton;
        private ButtonControl _rightTriggerButton;
        private ButtonControl _leftGripButton;
        private ButtonControl _rightGripButton;
        private ButtonControl _leftJoystickButton;
        private ButtonControl _rightJoystickButton;
        private ButtonControl _leftPrimaryButton;
        private ButtonControl _rightPrimaryButton;
        private ButtonControl _leftSecondaryButton;
        private ButtonControl _rightSecondaryButton;
        private NativeArray<XRInputState> _xrInputStates;
        private NativeArray<RaycastCommand> _xrLookAtRayCommands;
        private JobHandle _xrNativeDisposeHandle;
        private RaycastHit _lastXRLookAtHit;
        private AbsoluteUniversePosition _lastXRLookAtRayOriginAup;
        private Vector3 _lastXRLookAtRayOriginRuntimePosition;
        private Vector3 _lastXRLookAtRayDirection;
        private AbsoluteUniversePosition _lastXRLookAtHitPointAup;
        private int _lastXRLookAtPhysicsQueryFrame = -1;
        private bool _registeredUpdatable;
        private bool _registeredInputService;
        private bool _registeredHotSwapListener;
        private bool _isInitialized;
        private bool _subscribedToNativeInput;
        private bool _subscribedToDeviceChanges;
        private int _lastCapturedFrame = -1;
        private int _nextXRDeviceRescanFrame;
        private int _lastXRLookAtHitFrame = -1;
        private int _bufferWriteIndex;
        private Vector2 _pendingLookDelta;
        private uint _latchedActionBits;
        private float _appliedLowMotorSpeed;
        private float _appliedHighMotorSpeed;
        private float _appliedLeftXRHapticAmplitude;
        private float _appliedRightXRHapticAmplitude;
        private float _nextLeftXRHapticWriteTime;
        private float _nextRightXRHapticWriteTime;
        private float _lookBlendElapsed;
        private uint _xrRuntimeFlags;
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
            RefreshXRNativeBufferState();
            TryRegisterToDispatcher();
            _isInitialized = true;
            TryRegisterInputService();
            TryRegisterHotSwapListener();
            CaptureState();
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            EnsureInputBinding();
            EnsureHapticDeviceBinding();
            RefreshXRNativeBufferState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

            EnsureInputBinding();
            EnsureHapticDeviceBinding();
            RefreshXRNativeBufferState();

            if (_isInitialized)
            {
                TryRegisterToDispatcher();
                TryRegisterInputService();
                TryRegisterHotSwapListener();
                CaptureState();
            }
        }

        private void OnDisable()
        {
            ShutdownServiceState(resetInitialization: false, clearSubscribers: false);
        }

        private void OnDestroy()
        {
            ShutdownServiceState(resetInitialization: true, clearSubscribers: true);
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState(resetInitialization: true, clearSubscribers: true);
        }

        private void ShutdownServiceState(bool resetInitialization, bool clearSubscribers)
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            UnsubscribeFromNativeInput();
            ResetGamepadHaptics();
            ResetXRHaptics();
            UnsubscribeFromDeviceChanges();
            TryUnregisterFromDispatcher();
            TryUnregisterInputService();
            TryUnregisterHotSwapListener();
            ClearFrameState();
            ClearCachedInputDevices();
            DisposeXRNativeBuffers(default);

            if (resetInitialization)
            {
                _nativeInputManager = null;
                _isInitialized = false;
            }

            if (clearSubscribers)
                ClearInputSubscribers();
        }

        private void ClearCachedInputDevices()
        {
            _cachedGamepad = null;
            ClearCachedXRControllers();
        }

        private void ClearInputSubscribers()
        {
            OnInteract = null;
            OnPrimaryAction = null;
            OnSecondaryAction = null;
            OnPDA = null;
            OnInventory = null;
            OnCancel = null;
            OnTabNext = null;
            OnTabPrevious = null;
            OnToolSlot1 = null;
            OnToolSlot2 = null;
            OnToolSlot3 = null;
            OnToolSlot4 = null;
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
        /// Returns the read-only OpenXR controller snapshot buffer: index 0 left, index 1 right.
        /// </summary>
        internal NativeArray<XRInputState>.ReadOnly GetXRInputStatesReadOnly()
        {
            return _xrInputStates.IsCreated ? _xrInputStates.AsReadOnly() : default;
        }

        /// <summary>
        /// Returns the single-command eye/look ray buffer staged for menu and diegetic selection.
        /// </summary>
        internal NativeArray<RaycastCommand>.ReadOnly GetXRLookAtRayCommandsReadOnly()
        {
            return _xrLookAtRayCommands.IsCreated ? _xrLookAtRayCommands.AsReadOnly() : default;
        }

        internal bool TryGetXRInputState(byte controllerIndex, out XRInputState state)
        {
            state = default;
            if (!_xrInputStates.IsCreated ||
                !HectonXRRuntimeState.IsXRActive ||
                controllerIndex >= _xrInputStates.Length)
            {
                return false;
            }

            state = _xrInputStates[controllerIndex];
            return state.IsTracked != 0;
        }

        /// <summary>
        /// Returns the latest dispatcher-resolved XR look-at hit for O(1) menu selection.
        /// </summary>
        internal bool TryGetXRLookAtHit(out RaycastHit hit)
        {
            hit = _lastXRLookAtHit;
            return _lastXRLookAtHitFrame == Time.frameCount;
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
            if (HectonXRRuntimeState.IsXRActive)
                ResolveCachedXRControllers();
            else
                ClearCachedXRControllers();
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
            if (device is XRController xrController)
                HandleXRDeviceChange(xrController, change);

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

        private void HandleXRDeviceChange(XRController controller, InputDeviceChange change)
        {
            if (!HectonXRRuntimeState.IsXRActive)
            {
                ClearCachedXRControllers();
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.UsageChanged:
                    ResolveCachedXRControllers();
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    if (ReferenceEquals(_cachedLeftXRController, controller))
                        ClearLeftXRController();
                    if (ReferenceEquals(_cachedRightXRController, controller))
                        ClearRightXRController();
                    ResolveCachedXRControllers();
                    break;
            }
        }

        private void ClearCachedXRControllers()
        {
            ClearLeftXRController();
            ClearRightXRController();
            _nextXRDeviceRescanFrame = 0;
        }

        private void ResolveCachedXRControllers()
        {
            int frame = Time.frameCount;
            if (_cachedLeftXRController != null && _cachedLeftXRController.added &&
                _cachedRightXRController != null && _cachedRightXRController.added &&
                frame < _nextXRDeviceRescanFrame)
            {
                return;
            }

            _nextXRDeviceRescanFrame = frame + XRDeviceRescanIntervalFrames;

            XRController left = XRController.leftHand;
            XRController right = XRController.rightHand;
            if (!ReferenceEquals(_cachedLeftXRController, left))
                BindLeftXRController(left);
            if (!ReferenceEquals(_cachedRightXRController, right))
                BindRightXRController(right);
        }

        private void BindLeftXRController(XRController controller)
        {
            if (!ReferenceEquals(_cachedLeftXRController, controller))
            {
                ResetXRControllerHaptics(
                    _cachedLeftXRController,
                    ref _appliedLeftXRHapticAmplitude,
                    ref _nextLeftXRHapticWriteTime);
            }

            _cachedLeftXRController = controller != null && controller.added ? controller : null;
            ResolveXRControls(
                _cachedLeftXRController,
                ref _leftTriggerAxis,
                ref _leftGripAxis,
                ref _leftJoystickAxis,
                ref _leftTriggerButton,
                ref _leftGripButton,
                ref _leftJoystickButton,
                ref _leftPrimaryButton,
                ref _leftSecondaryButton);
        }

        private void BindRightXRController(XRController controller)
        {
            if (!ReferenceEquals(_cachedRightXRController, controller))
            {
                ResetXRControllerHaptics(
                    _cachedRightXRController,
                    ref _appliedRightXRHapticAmplitude,
                    ref _nextRightXRHapticWriteTime);
            }

            _cachedRightXRController = controller != null && controller.added ? controller : null;
            ResolveXRControls(
                _cachedRightXRController,
                ref _rightTriggerAxis,
                ref _rightGripAxis,
                ref _rightJoystickAxis,
                ref _rightTriggerButton,
                ref _rightGripButton,
                ref _rightJoystickButton,
                ref _rightPrimaryButton,
                ref _rightSecondaryButton);
        }

        private void ClearLeftXRController()
        {
            ResetXRControllerHaptics(
                _cachedLeftXRController,
                ref _appliedLeftXRHapticAmplitude,
                ref _nextLeftXRHapticWriteTime);
            _cachedLeftXRController = null;
            _leftTriggerAxis = null;
            _leftGripAxis = null;
            _leftJoystickAxis = null;
            _leftTriggerButton = null;
            _leftGripButton = null;
            _leftJoystickButton = null;
            _leftPrimaryButton = null;
            _leftSecondaryButton = null;
        }

        private void ClearRightXRController()
        {
            ResetXRControllerHaptics(
                _cachedRightXRController,
                ref _appliedRightXRHapticAmplitude,
                ref _nextRightXRHapticWriteTime);
            _cachedRightXRController = null;
            _rightTriggerAxis = null;
            _rightGripAxis = null;
            _rightJoystickAxis = null;
            _rightTriggerButton = null;
            _rightGripButton = null;
            _rightJoystickButton = null;
            _rightPrimaryButton = null;
            _rightSecondaryButton = null;
        }

        private static void ResolveXRControls(
            XRController controller,
            ref AxisControl triggerAxis,
            ref AxisControl gripAxis,
            ref Vector2Control joystickAxis,
            ref ButtonControl triggerButton,
            ref ButtonControl gripButton,
            ref ButtonControl joystickButton,
            ref ButtonControl primaryButton,
            ref ButtonControl secondaryButton)
        {
            triggerAxis = controller != null ? TryGetAxisControl(controller, "trigger") : null;
            gripAxis = controller != null ? TryGetAxisControl(controller, "grip") : null;
            joystickAxis = controller != null ? TryGetVector2Control(controller, "thumbstick") : null;
            if (joystickAxis == null && controller != null)
                joystickAxis = TryGetVector2Control(controller, "primary2DAxis");

            triggerButton = controller != null ? TryGetButtonControl(controller, "triggerPressed") : null;
            gripButton = controller != null ? TryGetButtonControl(controller, "gripPressed") : null;
            joystickButton = controller != null ? TryGetButtonControl(controller, "thumbstickClicked") : null;
            if (joystickButton == null && controller != null)
                joystickButton = TryGetButtonControl(controller, "primary2DAxisClick");
            primaryButton = controller != null ? TryGetButtonControl(controller, "primaryButton") : null;
            secondaryButton = controller != null ? TryGetButtonControl(controller, "secondaryButton") : null;
        }

        private static AxisControl TryGetAxisControl(XRController controller, string path)
        {
            InputControl control = controller.TryGetChildControl(path);
            return control as AxisControl;
        }

        private static Vector2Control TryGetVector2Control(XRController controller, string path)
        {
            InputControl control = controller.TryGetChildControl(path);
            return control as Vector2Control;
        }

        private static ButtonControl TryGetButtonControl(XRController controller, string path)
        {
            InputControl control = controller.TryGetChildControl(path);
            return control as ButtonControl;
        }

        private void TryRegisterToDispatcher()
        {
            if (_registeredUpdatable)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
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

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterInputService()
        {
            if (!_registeredInputService)
                return;

            if (ReferenceEquals(GlobalRegistry.RegisteredInput, this))
                GlobalRegistry.UnregisterInputService(this);

            _registeredInputService = false;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                return;

            BindNativeInputManager(currentService as InputManager);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
        }

        private void CaptureState(float deltaTime = 0f)
        {
            EnsureInputBinding();
            RefreshXRNativeBufferState();

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
                state.VerticalDelta = math.clamp(inputManager.VerticalMovementInput, -1f, 1f);
                state.ActionsBitmask = actionBits;
                _lastDeliveredLookDelta = lookDelta;
            }

            _currentState = state;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
            if (HectonXRRuntimeState.IsXRActive)
            {
                RefreshXRInputSnapshot();
                StageXRLookAtRayCommand();
            }
            else
            {
                ClearXRRuntimeFrameStateIfActive();
            }
        }

        private void RefreshXRNativeBufferState()
        {
            if (HectonXRRuntimeState.IsXRActive)
            {
                EnsureXRNativeBuffers();
                return;
            }

            if (!HasXRRuntimeStateToClear())
            {
                return;
            }

            ClearCachedXRControllers();
            ClearXRRuntimeFrameStateIfActive();
            DisposeXRNativeBuffers(default);
        }

        private bool HasXRRuntimeStateToClear()
        {
            return _xrInputStates.IsCreated ||
                   _xrLookAtRayCommands.IsCreated ||
                   (_xrRuntimeFlags & XRRuntimeFlagsAny) != 0u ||
                   _lastXRLookAtPhysicsQueryFrame >= 0 ||
                   _lastXRLookAtHitFrame >= 0 ||
                   _cachedLeftXRController != null ||
                   _cachedRightXRController != null ||
                   _appliedLeftXRHapticAmplitude > HapticMotorWriteEpsilon ||
                   _appliedRightXRHapticAmplitude > HapticMotorWriteEpsilon;
        }

        private void EnsureXRNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _xrNativeDisposeHandle);
            if (!_xrNativeDisposeHandle.IsCompleted)
                return;

            if (!_xrInputStates.IsCreated)
            {
                _xrInputStates = new NativeArray<XRInputState>(XRInputStateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<XRInputState>[2] - OpenXR left/right frame cache - owner: InputDispatcher
                NativeMemorySentinel.RegisterNativeArray(
                    _xrInputStates,
                    nameof(InputDispatcher),
                    nameof(_xrInputStates),
                    NativeAllocationLifetime.Session);
            }

            if (!_xrLookAtRayCommands.IsCreated)
            {
                _xrLookAtRayCommands = new NativeArray<RaycastCommand>(XRLookAtCommandCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[1] - XR eye/look selection command cache - owner: InputDispatcher
                NativeMemorySentinel.RegisterNativeArray(
                    _xrLookAtRayCommands,
                    nameof(InputDispatcher),
                    nameof(_xrLookAtRayCommands),
                    NativeAllocationLifetime.Session);
                DisableXRLookAtRayCommand(forceWrite: true);
            }
        }

        private void DisposeXRNativeBuffers(JobHandle dependency)
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _xrNativeDisposeHandle);
            bool hasPendingDispose = !_xrNativeDisposeHandle.IsCompleted;
            JobHandle disposeHandle = hasPendingDispose
                ? JobHandle.CombineDependencies(_xrNativeDisposeHandle, dependency)
                : dependency;
            bool scheduledDispose = false;

            if (_xrInputStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_xrInputStates);
                disposeHandle = _xrInputStates.Dispose(disposeHandle);
                _xrInputStates = default;
                scheduledDispose = true;
            }

            if (_xrLookAtRayCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_xrLookAtRayCommands);
                disposeHandle = _xrLookAtRayCommands.Dispose(disposeHandle);
                _xrLookAtRayCommands = default;
                scheduledDispose = true;
            }

            if (!scheduledDispose)
                return;

            _xrNativeDisposeHandle = disposeHandle;
            JobHandle.ScheduleBatchedJobs();
        }

        private void RefreshXRInputSnapshot()
        {
            if (!_xrInputStates.IsCreated)
                return;

            if (!HectonXRRuntimeState.IsXRActive)
            {
                ClearXRInputSnapshotIfActive();
                return;
            }

            ResolveCachedXRControllers();
            _xrInputStates[0] = CaptureXRController(
                0,
                _cachedLeftXRController,
                _leftTriggerAxis,
                _leftGripAxis,
                _leftJoystickAxis,
                _leftTriggerButton,
                _leftGripButton,
                _leftJoystickButton,
                _leftPrimaryButton,
                _leftSecondaryButton);
            _xrInputStates[1] = CaptureXRController(
                1,
                _cachedRightXRController,
                _rightTriggerAxis,
                _rightGripAxis,
                _rightJoystickAxis,
                _rightTriggerButton,
                _rightGripButton,
                _rightJoystickButton,
                _rightPrimaryButton,
                _rightSecondaryButton);
            _xrRuntimeFlags |= XRRuntimeFlagInputSnapshotActive;
        }

        private void ClearXRInputSnapshotIfActive(bool forceWrite = false)
        {
            if (!_xrInputStates.IsCreated)
                return;

            if (!forceWrite && (_xrRuntimeFlags & XRRuntimeFlagInputSnapshotActive) == 0u)
                return;

            for (int i = 0; i < _xrInputStates.Length; i++)
                _xrInputStates[i] = default;

            _xrRuntimeFlags &= ~XRRuntimeFlagInputSnapshotActive;
        }

        private static XRInputState CaptureXRController(
            byte controllerIndex,
            XRController controller,
            AxisControl triggerAxis,
            AxisControl gripAxis,
            Vector2Control joystickAxis,
            ButtonControl triggerButton,
            ButtonControl gripButton,
            ButtonControl joystickButton,
            ButtonControl primaryButton,
            ButtonControl secondaryButton)
        {
            XRInputState state = default;
            state.Frame = Time.frameCount;
            state.ControllerIndex = controllerIndex;
            state.GripRotationWS = quaternion.identity;

            if (controller == null || !controller.added)
                return state;

            state.Trigger = ApplyXRAnalogNoiseFloor(triggerAxis != null ? triggerAxis.ReadValue() : 0f);
            state.Grip = ApplyXRAnalogNoiseFloor(gripAxis != null ? gripAxis.ReadValue() : 0f);
            Vector2 joystick = joystickAxis != null ? joystickAxis.ReadValue() : Vector2.zero;
            state.Joystick = ApplyXRJoystickNoiseFloor(joystick);
            Vector3 position = controller.devicePosition != null ? controller.devicePosition.ReadValue() : Vector3.zero;
            Quaternion rotation = controller.deviceRotation != null ? controller.deviceRotation.ReadValue() : Quaternion.identity;
            state.GripPositionWS = new float3(position.x, position.y, position.z);
            state.GripRotationWS = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            bool tracked = controller.isTracked != null && controller.isTracked.isPressed;
            state.IsTracked = tracked ? (byte)1 : (byte)0;

            bool triggerActive = IsPressed(triggerButton, state.Trigger);
            bool gripActive = IsPressed(gripButton, state.Grip);
            bool joystickClickActive = IsPressed(joystickButton, 0f);
            bool joystickActive = math.lengthsq(state.Joystick) >= XRAnalogNoiseFloorSq || joystickClickActive;
            bool primaryActive = IsPressed(primaryButton, 0f);
            bool secondaryActive = IsPressed(secondaryButton, 0f);

            uint buttons = 0u;
            buttons |= triggerActive ? (uint)XRInputButton.Trigger : 0u;
            buttons |= gripActive ? (uint)XRInputButton.Grip : 0u;
            buttons |= joystickClickActive ? (uint)XRInputButton.JoystickClick : 0u;
            buttons |= primaryActive ? (uint)XRInputButton.Primary : 0u;
            buttons |= secondaryActive ? (uint)XRInputButton.Secondary : 0u;
            state.ButtonsBitmask = buttons;
            state.ActiveMask = BuildControllerActiveMask(
                controllerIndex,
                triggerActive,
                gripActive,
                joystickActive,
                primaryActive,
                secondaryActive);
            return state;
        }

        private static uint BuildControllerActiveMask(
            byte controllerIndex,
            bool triggerActive,
            bool gripActive,
            bool joystickActive,
            bool primaryActive,
            bool secondaryActive)
        {
            uint localMask = 0u;
            localMask |= triggerActive ? (uint)XRInputActiveBit.Trigger : 0u;
            localMask |= gripActive ? (uint)XRInputActiveBit.Grip : 0u;
            localMask |= joystickActive ? (uint)XRInputActiveBit.Joystick : 0u;
            localMask |= primaryActive ? (uint)XRInputActiveBit.Primary : 0u;
            localMask |= secondaryActive ? (uint)XRInputActiveBit.Secondary : 0u;
            return localMask << (controllerIndex * XRControllerActiveBitCount);
        }

        private static float ApplyXRAnalogNoiseFloor(float value)
        {
            float normalized = math.saturate(value);
            return normalized < XRAnalogNoiseFloor ? 0f : normalized;
        }

        private static float2 ApplyXRJoystickNoiseFloor(Vector2 value)
        {
            float2 joystick = new float2(value.x, value.y);
            return math.lengthsq(joystick) < XRAnalogNoiseFloorSq ? float2.zero : joystick;
        }

        private static bool IsPressed(ButtonControl button, float analogValue)
        {
            return (button != null && button.isPressed) || analogValue >= 0.5f;
        }

        private void StageXRLookAtRayCommand()
        {
            if (!_xrLookAtRayCommands.IsCreated)
                return;

            if (!HectonXRRuntimeState.IsXRActive)
            {
                DisableXRLookAtRayCommand();
                return;
            }

            Transform viewTransform = ResolveLookAtViewTransform();
            if (viewTransform == null)
            {
                DisableXRLookAtRayCommand();
                return;
            }

            viewTransform.GetPositionAndRotation(out Vector3 origin, out Quaternion viewRotation);
            Vector3 direction = viewRotation * Vector3.forward;
            float3 direction3 = new float3(direction.x, direction.y, direction.z);
            if (!math.all(math.isfinite(direction3)))
            {
                direction = Vector3.forward;
                direction3 = new float3(0f, 0f, 1f);
            }

            AbsoluteUniversePosition originAup = HectonXRRuntimeState.TryResolveCachedHeadAup(origin, out AbsoluteUniversePosition cachedHeadAup)
                ? cachedHeadAup
                : AbsoluteUniversePosition.FromRuntimePosition(origin);
            if (TryReuseXRLookAtHit(in originAup, in direction3))
            {
                DisableXRLookAtRayCommand();
                return;
            }

            Vector3 rayOrigin = (Vector3)originAup.ToRuntimeFloat3();
            RaycastCommand command = default;
            command.from = rayOrigin;
            command.direction = direction;
            command.distance = XRLookAtSelectionDistanceMeters;
            command.queryParameters = XRLookAtEnabledQueryParameters;
            if (SystemDispatcher.QueueDispatcherRaycast(this, XRLookAtSelectionRequestId, in command))
            {
                _xrLookAtRayCommands[0] = command;
                _xrRuntimeFlags |= XRRuntimeFlagLookAtRayCommandEnabled;
                _lastXRLookAtRayOriginAup = originAup;
                _lastXRLookAtRayOriginRuntimePosition = rayOrigin;
                _lastXRLookAtRayDirection = direction;
                return;
            }

            DisableXRLookAtRayCommand(forceWrite: true);
        }

        private void DisableXRLookAtRayCommand(bool forceWrite = false)
        {
            if (!forceWrite && (_xrRuntimeFlags & XRRuntimeFlagLookAtRayCommandEnabled) == 0u)
                return;

            _xrLookAtRayCommands[0] = DisabledXRLookAtRayCommand;
            _xrRuntimeFlags &= ~XRRuntimeFlagLookAtRayCommandEnabled;
        }

        private void ClearXRRuntimeFrameStateIfActive()
        {
            if (!_xrInputStates.IsCreated &&
                !_xrLookAtRayCommands.IsCreated &&
                (_xrRuntimeFlags & XRRuntimeFlagsAny) == 0u &&
                _lastXRLookAtPhysicsQueryFrame < 0 &&
                _lastXRLookAtHitFrame < 0)
            {
                return;
            }

            ClearXRInputSnapshotIfActive(forceWrite: true);

            if (_xrLookAtRayCommands.IsCreated)
                DisableXRLookAtRayCommand(forceWrite: true);

            _lastXRLookAtHit = default;
            _lastXRLookAtHitFrame = -1;
            _lastXRLookAtRayOriginAup = default;
            _lastXRLookAtRayOriginRuntimePosition = Vector3.zero;
            _lastXRLookAtRayDirection = Vector3.forward;
            _lastXRLookAtHitPointAup = default;
            _lastXRLookAtPhysicsQueryFrame = -1;
            _xrRuntimeFlags = 0u;
        }

        private bool TryReuseXRLookAtHit(in AbsoluteUniversePosition originAup, in float3 direction)
        {
            if (_lastXRLookAtPhysicsQueryFrame < 0)
                return false;

            if (Time.frameCount - _lastXRLookAtPhysicsQueryFrame > XRLookAtReuseMaxFrames)
                return false;

            float3 originDelta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in originAup, in _lastXRLookAtRayOriginAup);
            if (math.lengthsq(originDelta) > XRLookAtReuseOriginDriftSq)
                return false;

            float3 previousDirection = new float3(_lastXRLookAtRayDirection.x, _lastXRLookAtRayDirection.y, _lastXRLookAtRayDirection.z);
            if (math.dot(previousDirection, direction) < XRLookAtReuseForwardDot)
                return false;

            if (_lastXRLookAtHit.collider == null)
            {
                _lastXRLookAtHitFrame = Time.frameCount;
                return true;
            }

            float3 toHit = AbsoluteUniversePosition.ToCameraRelativeFloat3(in _lastXRLookAtHitPointAup, in originAup);
            float hitDistanceSq = math.lengthsq(toHit);
            if (hitDistanceSq <= 0.0001f || hitDistanceSq > XRLookAtSelectionDistanceSq)
                return false;

            float forwardDistance = math.dot(toHit, direction);
            if (forwardDistance <= 0f || (forwardDistance * forwardDistance) < XRLookAtReuseForwardDotSq * hitDistanceSq)
                return false;

            float lateralDriftSq = math.max(0f, hitDistanceSq - (forwardDistance * forwardDistance));
            if (lateralDriftSq > XRLookAtReuseLateralDriftSq)
                return false;

            _lastXRLookAtHitFrame = Time.frameCount;
            return true;
        }

        private static Transform ResolveLookAtViewTransform()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return null;

            if (playerContext.PlayerCamera != null)
                return playerContext.PlayerCamera.transform;

            return playerContext.PlayerTransform;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
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
            Vector2 lookDelta = BlendLookDeltaLinear(_lookBlendFrom, targetLookDelta, eased);
            if (normalized >= 1f)
                _lookBlendActive = false;

            return lookDelta;
        }

        private static Vector2 BlendLookDeltaLinear(Vector2 from, Vector2 to, float t)
        {
            float2 fromDelta = new float2(from.x, from.y);
            float2 toDelta = new float2(to.x, to.y);
            float2 blended = math.lerp(fromDelta, toDelta, t);
            return new Vector2(blended.x, blended.y);
        }

        private static Vector2 ApplyQuadraticLookCurve(Vector2 lookDelta)
        {
            float2 raw = new float2(lookDelta.x, lookDelta.y);
            float magnitudeSq = math.lengthsq(raw);
            if (magnitudeSq <= LookCurveDeadzoneSq)
                return Vector2.zero;

            float normalizedSq = math.saturate((magnitudeSq - LookCurveDeadzoneSq) / LookCurveRangeSq);
            float gain = normalizedSq * normalizedSq;
            float2 curved = raw * gain;
            return new Vector2(curved.x, curved.y);
        }

        private void HandleLookInput(Vector2 lookDelta)
        {
            _pendingLookDelta += ApplyQuadraticLookCurve(lookDelta);
        }

        private void HandleJumpPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.Jump;
            BufferAction(PlayerBufferedAction.Jump);
        }

        private void HandleInteractPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.Interact;
            OnInteract?.Invoke();
        }

        private void HandleToolSlot1Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnToolSlot1?.Invoke();
        }

        private void HandleToolSlot2Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnToolSlot2?.Invoke();
        }

        private void HandleToolSlot3Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnToolSlot3?.Invoke();
        }

        private void HandleToolSlot4Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnToolSlot4?.Invoke();
        }

        private void HandlePrimaryActionPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.PrimaryFire;
            OnPrimaryAction?.Invoke();
        }

        private void HandleSecondaryActionPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.SecondaryFire;
            OnSecondaryAction?.Invoke();
        }

        private void HandlePDAPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnPDA?.Invoke();
        }

        private void HandleInventoryPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnInventory?.Invoke();
        }

        private void HandleCancelPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnCancel?.Invoke();
        }

        private void HandleTabNextPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnTabNext?.Invoke();
        }

        private void HandleTabPreviousPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            OnTabPrevious?.Invoke();
        }

        private void HandleSprintPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.Sprint;
        }

        void IDispatcherRaycastReceiver.ConsumeDispatcherRaycastHit(int requestId, in RaycastHit hit)
        {
            if (requestId != XRLookAtSelectionRequestId)
                return;

            _lastXRLookAtHit = hit;
            _lastXRLookAtHitFrame = Time.frameCount;
            _lastXRLookAtPhysicsQueryFrame = Time.frameCount;
            if (hit.collider != null)
                _lastXRLookAtHitPointAup = OffsetAupLocal(in _lastXRLookAtRayOriginAup, hit.point - _lastXRLookAtRayOriginRuntimePosition);
        }

        private void DrainToolHaptics()
        {
            if (!ToolHapticsRuntime.TryGetRuntime(out ToolHapticsRuntime runtime) ||
                !runtime.TryGetFrontBufferSnapshot(out NativeArray<ToolHapticsRuntime.HapticCommand>.ReadOnly commandBuffer, out int commandCount))
            {
                ApplyGamepadHaptics(0f, 0f);
                if (HectonXRRuntimeState.IsXRActive)
                    ApplyXRHaptics(0f, 0f);
                return;
            }

            float lowMotor = 0f;
            float highMotor = 0f;
            byte lowPriority = 0;
            byte highPriority = 0;
            bool hasLowPriority = false;
            bool hasHighPriority = false;
            for (int i = 0; i < commandCount; i++)
            {
                ToolHapticsRuntime.HapticCommand command = commandBuffer[i];
                if (command.DurationRemaining <= 0f)
                    continue;

                float lowContribution = (command.MotorMask & HapticLowMotorMask) != 0
                    ? ClampFinite01(command.LowFreqIntensity)
                    : 0f;
                float highContribution = (command.MotorMask & HapticHighMotorMask) != 0
                    ? ClampFinite01(command.HighFreqIntensity)
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
            if (HectonXRRuntimeState.IsXRActive)
                ApplyXRHaptics(lowMotor, highMotor);
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
                case HapticBlendOverride:
                    motorValue = contribution;
                    break;

                case HapticBlendAdditive:
                    motorValue = math.saturate(motorValue + contribution);
                    break;

                default:
                    motorValue = math.max(motorValue, contribution);
                    break;
            }
        }

        private void ApplyGamepadHaptics(float lowMotor, float highMotor)
        {
            lowMotor = ClampFinite01(lowMotor);
            highMotor = ClampFinite01(highMotor);

            if (_cachedGamepad != null && !_cachedGamepad.added)
                _cachedGamepad = null;

            if (_cachedGamepad == null)
            {
                _appliedLowMotorSpeed = 0f;
                _appliedHighMotorSpeed = 0f;
                return;
            }

            if (math.abs(lowMotor - _appliedLowMotorSpeed) <= HapticMotorWriteEpsilon &&
                math.abs(highMotor - _appliedHighMotorSpeed) <= HapticMotorWriteEpsilon)
            {
                return;
            }

            _cachedGamepad.SetMotorSpeeds(lowMotor, highMotor);
            _appliedLowMotorSpeed = lowMotor;
            _appliedHighMotorSpeed = highMotor;
        }

        private void ApplyXRHaptics(float leftAmplitude, float rightAmplitude)
        {
            if (!HectonXRRuntimeState.IsXRActive)
            {
                ResetXRHaptics();
                return;
            }

            ApplyXRControllerHaptic(
                _cachedLeftXRController,
                ClampFinite01(leftAmplitude),
                ref _appliedLeftXRHapticAmplitude,
                ref _nextLeftXRHapticWriteTime);
            ApplyXRControllerHaptic(
                _cachedRightXRController,
                ClampFinite01(rightAmplitude),
                ref _appliedRightXRHapticAmplitude,
                ref _nextRightXRHapticWriteTime);
        }

        private static void ApplyXRControllerHaptic(
            XRController controller,
            float amplitude,
            ref float appliedAmplitude,
            ref float nextWriteTime)
        {
            if (!(controller is XRControllerWithRumble rumbleController) || !rumbleController.added)
            {
                appliedAmplitude = 0f;
                nextWriteTime = 0f;
                return;
            }

            float now = Time.unscaledTime;
            bool hasAppliedOutput = appliedAmplitude > HapticMotorWriteEpsilon;
            if (amplitude <= HapticMotorWriteEpsilon)
            {
                if (hasAppliedOutput)
                    rumbleController.SendImpulse(0f, 0f);

                appliedAmplitude = 0f;
                nextWriteTime = 0f;
                return;
            }

            bool changed = math.abs(amplitude - appliedAmplitude) > XRHapticMotorWriteEpsilon;
            if (!changed && now < nextWriteTime)
                return;

            rumbleController.SendImpulse(amplitude, XRHapticImpulseDurationSeconds);
            appliedAmplitude = amplitude;
            nextWriteTime = now + XRHapticRefreshIntervalSeconds;
        }

        private static float ClampFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private void ResetGamepadHaptics()
        {
            if (_cachedGamepad != null && !_cachedGamepad.added)
                _cachedGamepad = null;

            bool hadMotorOutput =
                _appliedLowMotorSpeed > HapticMotorWriteEpsilon ||
                _appliedHighMotorSpeed > HapticMotorWriteEpsilon;

            if (_cachedGamepad != null && hadMotorOutput)
                _cachedGamepad.SetMotorSpeeds(0f, 0f);

            _appliedLowMotorSpeed = 0f;
            _appliedHighMotorSpeed = 0f;
        }

        private void ResetXRHaptics()
        {
            ResetXRControllerHaptics(
                _cachedLeftXRController,
                ref _appliedLeftXRHapticAmplitude,
                ref _nextLeftXRHapticWriteTime);
            ResetXRControllerHaptics(
                _cachedRightXRController,
                ref _appliedRightXRHapticAmplitude,
                ref _nextRightXRHapticWriteTime);
        }

        private static void ResetXRControllerHaptics(
            XRController controller,
            ref float appliedAmplitude,
            ref float nextWriteTime)
        {
            if (appliedAmplitude > HapticMotorWriteEpsilon &&
                controller is XRControllerWithRumble rumbleController &&
                rumbleController.added)
            {
                rumbleController.SendImpulse(0f, 0f);
            }

            appliedAmplitude = 0f;
            nextWriteTime = 0f;
        }

        private void ClearFrameState()
        {
            _lastCapturedFrame = -1;
            _bufferWriteIndex = 0;
            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
            _appliedLowMotorSpeed = 0f;
            _appliedHighMotorSpeed = 0f;
            _appliedLeftXRHapticAmplitude = 0f;
            _appliedRightXRHapticAmplitude = 0f;
            _nextLeftXRHapticWriteTime = 0f;
            _nextRightXRHapticWriteTime = 0f;
            _lookBlendElapsed = 0f;
            _lookBlendActive = false;
            _lookBlendFrom = Vector2.zero;
            _lastDeliveredLookDelta = Vector2.zero;
            _currentState = default;
            _lastXRLookAtHit = default;
            _lastXRLookAtHitFrame = -1;
            _lastXRLookAtRayOriginAup = default;
            _lastXRLookAtRayOriginRuntimePosition = Vector3.zero;
            _lastXRLookAtRayDirection = Vector3.forward;
            _lastXRLookAtHitPointAup = default;
            _lastXRLookAtPhysicsQueryFrame = -1;
            _xrRuntimeFlags = 0u;

            for (int i = 0; i < BufferedActionCapacity; i++)
                _bufferedActions[i].Action = PlayerBufferedAction.None;

            ClearXRInputSnapshotIfActive(forceWrite: true);

            if (_xrLookAtRayCommands.IsCreated)
                DisableXRLookAtRayCommand(forceWrite: true);
        }
    }

    /// <summary>
    /// Main-thread stopwatch bridge from player intent capture to render completion.
    /// </summary>
    public static class InputLatencyTracker
    {
        private static double _pendingInputTimestamp;
        private static int _pendingInputFrame;
        private static float _lastCompletedLatencyMs;
        private static uint _completedSequence;

        public static uint CompletedSequence => _completedSequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingInputTimestamp = 0d;
            _pendingInputFrame = -1;
            _lastCompletedLatencyMs = 0f;
            _completedSequence = 0u;
        }

        public static void MarkInputCaptured()
        {
            double timestamp = InputSystem.currentTime;
            if (timestamp <= 0d)
                timestamp = Time.unscaledTimeAsDouble;

            if (_pendingInputTimestamp <= 0d || Time.frameCount != _pendingInputFrame)
            {
                _pendingInputTimestamp = timestamp;
                _pendingInputFrame = Time.frameCount;
            }
        }

        public static void MarkRenderCompleted()
        {
            double inputTimestamp = _pendingInputTimestamp;
            if (inputTimestamp <= 0d)
                return;

            double renderTimestamp = Time.unscaledTimeAsDouble;
            if (renderTimestamp <= 0d)
                renderTimestamp = Time.realtimeSinceStartupAsDouble;

            double elapsedSeconds = renderTimestamp - inputTimestamp;
            if (elapsedSeconds <= 0d)
            {
                _pendingInputTimestamp = 0d;
                _pendingInputFrame = -1;
                return;
            }

            _lastCompletedLatencyMs = (float)(elapsedSeconds * 1000.0);
            _completedSequence++;
            _pendingInputTimestamp = 0d;
            _pendingInputFrame = -1;
        }

        public static float SampleCompletedLatencyMs()
        {
            return _lastCompletedLatencyMs;
        }
    }

    /// <summary>
    /// Numeric debt counter for frame-deferred Awaitable continuations.
    /// </summary>
    public static class AwaitableDebtMonitor
    {
        public const int LatencyCrimeThreshold = 50;
        private const int ReportCooldownFrames = 30;
        private const uint LatencyCrimeWarningHash = 2752459530u;
        private const uint AwaitableDebtContextHash = 3334278855u;
        private static int _pendingNextFrameContinuations;
        private static int _peakNextFrameContinuations;
        private static int _lastLatencyCrimeReportFrame = -ReportCooldownFrames;

        public static int PendingNextFrameContinuations => Volatile.Read(ref _pendingNextFrameContinuations);

        public static int ConsumePeakNextFrameContinuations()
        {
            int pending = PendingNextFrameContinuations;
            int peak = Interlocked.Exchange(ref _peakNextFrameContinuations, pending);
            return math.max(peak, pending);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volatile.Write(ref _pendingNextFrameContinuations, 0);
            Volatile.Write(ref _peakNextFrameContinuations, 0);
            _lastLatencyCrimeReportFrame = -ReportCooldownFrames;
        }

        public static async Awaitable NextFrameAsync(CancellationToken cancellationToken = default)
        {
            int pending = Interlocked.Increment(ref _pendingNextFrameContinuations);
            RecordPeakNextFrameContinuations(pending);
            try
            {
                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }
            finally
            {
                DecrementPendingNextFrameContinuations();
            }
        }

        private static void RecordPeakNextFrameContinuations(int pending)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _peakNextFrameContinuations);
                if (pending <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref _peakNextFrameContinuations, pending, current) != current);
        }

        private static void DecrementPendingNextFrameContinuations()
        {
            int current;
            int next;
            do
            {
                current = Volatile.Read(ref _pendingNextFrameContinuations);
                if (current <= 0)
                    return;

                next = current - 1;
            }
            while (Interlocked.CompareExchange(ref _pendingNextFrameContinuations, next, current) != current);
        }

        public static void AuditLatencyDebt(int pendingContinuationCount, float latencyMs)
        {
            if (pendingContinuationCount <= LatencyCrimeThreshold)
                return;

            int frame = Time.frameCount;
            if (frame - _lastLatencyCrimeReportFrame < ReportCooldownFrames)
                return;

            _lastLatencyCrimeReportFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                LatencyCrimeWarningHash,
                AwaitableDebtContextHash,
                pendingContinuationCount);
            CrashTelemetryBuffer.ReportLatencyCrime(pendingContinuationCount, latencyMs);
        }
    }
}
