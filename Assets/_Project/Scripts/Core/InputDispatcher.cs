#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_MMF_AVAILABLE
#endif
using Hecton8.Input;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using System;
using System.IO;
#if HECTON8_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Tools;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    public sealed unsafe class InputDispatcher : MonoBehaviour, IInputService, IUpdatable, ITickable, IServiceHeartbeat, IServiceShutdown, IDispatcherRaycastReceiver, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const int BufferedActionCapacity = 10;
        private const int DeterministicInputRingCapacity = 512;
        private const int ButtonMaskWindowCapacity = 10;
        private const int HapticCommandDtoCapacity = 16;
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
        private const float LookHotSwapBlendDurationSeconds = 0.25f;
        private const float LookCurveDeadzone = 0.035f;
        private const float LookCurveDeadzoneSq = LookCurveDeadzone * LookCurveDeadzone;
        private const float LookCurveRangeSq = 1f - LookCurveDeadzoneSq;
        private const float XRAnalogNoiseFloor = 0.05f;
        private const float XRAnalogNoiseFloorSq = XRAnalogNoiseFloor * XRAnalogNoiseFloor;
        private const float HapticMotorWriteEpsilon = 0.01f;
        private const float XRHapticMotorWriteEpsilon = 0.015f;
        private const float XRHapticImpulseDurationSeconds = 0.02f;
        private const float XRHapticRefreshIntervalSeconds = 0.033f;
        private const float XRToolTriggerPressThreshold = 0.5f;
        private const float XRToolTriggerPublishEpsilon = 0.01f;
        private const byte HapticLowMotorMask = 0b0001;
        private const byte HapticHighMotorMask = 0b0010;
        private const byte HapticBlendOverride = 0;
        private const byte HapticBlendAdditive = 1;
        private const byte DeviceLostFlagGamepad = 1 << 0;
        private const byte DeviceLostFlagXR = 1 << 1;
        private const byte ToolTriggerFlagPrimaryPressed = 1 << 0;
        private const byte ToolTriggerFlagSecondaryPressed = 1 << 1;
        private const uint InputSchemeHashKeyboardMouse = 0x4B424D21u;
        private const uint InputSchemeHashGamepad = 0x47504144u;
        private const uint InputSchemeHashSteamDeck = 0x5354444Bu;
        private const uint InputSchemeHashXRTouch = 0x58525443u;
        private const uint DeviceLostSignalSourceHash = 0x494E5044u;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private const uint XRRuntimeFlagLookAtRayCommandEnabled = 1u << 0;
        private const uint XRRuntimeFlagInputSnapshotActive = 1u << 1;
        private const uint XRRuntimeFlagsAny = XRRuntimeFlagLookAtRayCommandEnabled | XRRuntimeFlagInputSnapshotActive;
        private const int StandardInputRingCapacity = DeterministicInputRingCapacity;
        private const int InputBlackBoxCapacity = 300;
        private const int BufferedActionEntrySizeBytes = 16;
        private const int InputStateSizeBytes = 24;
        private const int PlayerInputStateSizeBytes = 64;
        private const int InputStateDtoSizeBytes = 24;
        private const int HapticCommandDtoSizeBytes = 16;
        private const int XRInputStateSizeBytes = 64;
        private const int InputReplayHeaderBytes = 16;
        private const int InputReplayPayloadBytes = StandardInputRingCapacity * InputStateSizeBytes;
        private const int InputReplayMappedBytes = InputReplayHeaderBytes + InputReplayPayloadBytes;
        private const int InputReplayRetryIntervalFrames = 300;
        private const int MaxInputDelayFrames = 2;
        private const int MaxStandardInputSubstepsPerFrame = 4;
        private const double StandardInputTickIntervalSeconds = 0.016666666666666666;
        private const double StandardInputTickRateHz = 60.0;
        private const ulong InputReplayMagic = 0x594C504552384848ul;
        private const uint InputReplayVersion = 1u;
        private const string InputReplayFileName = "input_determinism_bridge.h8replay";
        private const string InputDumpRelativePath = "Docs/AgentLogs/Dump_INPUT_DETERMINISM.bin";
        private const float DefaultInnerDeadzone = 0.12f;
        private const float DefaultOuterDeadzone = 0.98f;
        private const float DefaultMoveExponent = 1.65f;
        private const float DefaultMouseSensitivity = 1.0f;
        private const float DefaultMouseAcceleration = 0.08f;
        private const float DefaultHapticPowerScale = 1.0f;
        private const float DefaultHapticThermalAmplitudeScale = 0.5f;
        private const float ThermalHapticDispatchIntervalSeconds = 1f / 15f;
        private const uint InputProfileFlagEnableMockCollision = 1u << 0;
        private const uint InputMockSignalSourceHash = 0x5333364Du;
        private const uint InputActionMaskMovement = (uint)(PlayerInputAction.Jump | PlayerInputAction.Dash | PlayerInputAction.Sprint);
        private const uint InputActionMaskTools = (uint)(
            PlayerInputAction.PrimaryFire |
            PlayerInputAction.SecondaryFire |
            PlayerInputAction.Interact |
            PlayerInputAction.ToolSlot1 |
            PlayerInputAction.ToolSlot2 |
            PlayerInputAction.ToolSlot3 |
            PlayerInputAction.ToolSlot4);
        private static readonly QueryParameters XRLookAtEnabledQueryParameters = new QueryParameters(HectonLayerMasks.DefaultRaycastLayerMask, false, QueryTriggerInteraction.Ignore);
        private static readonly QueryParameters XRLookAtDisabledQueryParameters = new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore);
        private static readonly RaycastCommand DisabledXRLookAtRayCommand = new RaycastCommand(Vector3.zero, Vector3.forward, XRLookAtDisabledQueryParameters, 0.01f);

        [StructLayout(LayoutKind.Explicit, Size = BufferedActionEntrySizeBytes)]
        private struct BufferedActionEntry
        {
            [FieldOffset(0)]
            public int Frame;

            [FieldOffset(4)]
            private uint _pad0;

            [FieldOffset(8)]
            public PlayerBufferedAction Action;

            [FieldOffset(9)]
            private byte _pad1;

            [FieldOffset(10)]
            private byte _pad2;

            [FieldOffset(11)]
            private byte _pad3;

            [FieldOffset(12)]
            private uint _pad4;
        }

        private InputManager _nativeInputManager;
        private IPlayerRuntimeContext _playerContext;
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
        private InputAction _pollMoveAction;
        private InputAction _pollLookAction;
        private InputAction _pollJumpAction;
        private InputAction _pollSprintAction;
        private InputAction _pollInteractAction;
        private InputAction _pollPrimaryAction;
        private InputAction _pollSecondaryAction;
        private InputAction _pollPdaAction;
        private InputAction _pollPauseAction;
        private InputAction _pollInventoryAction;
        private InputAction _pollCancelAction;
        private InputAction _pollTabNextAction;
        private InputAction _pollTabPreviousAction;
        private InputAction _pollToolSlot1Action;
        private InputAction _pollToolSlot2Action;
        private InputAction _pollToolSlot3Action;
        private InputAction _pollToolSlot4Action;
        private InputAction _pollFlashlightAction;
        private InputAction _pollVerticalMovementAction;
        private InputAction _pollScrollWheelAction;
        private IDataVault _dataVault;
        private VaultGenerationHandle<InputStateDTO> _currentInputDtoHandle;
        private VaultGenerationHandle<InputStateDTO> _inputJournalHandle;
        private VaultGenerationHandle<InputState> _inputStateBridgeRingHandle;
        private VaultGenerationHandle<uint> _buttonMaskWindowHandle;
        private VaultGenerationHandle<uint> _inputBlockMaskHandle;
        private VaultGenerationHandle<InputProfileDTO> _inputProfileHandle;
        private VaultGenerationHandle<InputTelemetryEntryDTO> _inputTelemetryHandle;
        private VaultGenerationHandle<InputState> _inputReplaySnapshotHandle;
        private VaultGenerationHandle<HapticCommandDTO> _hapticCommandDtoHandle;
        private VaultGenerationHandle<XRInputState> _xrInputStatesHandle;
        private VaultGenerationHandle<RaycastCommand> _xrLookAtRayCommandsHandle;
        private VaultGenerationHandle<byte> _inputProfileCsvScratchHandle;
        private FileStream _inputReplayStream;
        private FileSystemWatcher _inputProfileCsvWatcher;
#if HECTON8_MMF_AVAILABLE
        private MemoryMappedFile _inputReplayMappedFile;
        private MemoryMappedViewAccessor _inputReplayAccessor;
        private byte* _inputReplayPointer;
#endif
        private Thread _inputReplayThread;
        private AutoResetEvent _inputReplaySignal;
        private RaycastHit _lastXRLookAtHit;
        private XRRuntimeAup48 _lastXRLookAtRayOriginAup;
        private Vector3 _lastXRLookAtRayOriginRuntimePosition;
        private Vector3 _lastXRLookAtRayDirection;
        private XRRuntimeAup48 _lastXRLookAtHitPointAup;
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
        private int _buttonMaskWindowWriteIndex;
        private int _deterministicInputCount;
        private int _deterministicBlackBoxWriteIndex;
        private int _inputReplayStopRequested;
        private int _inputReplayWritePending;
        private int _nextInputReplayRetryFrame;
        private int _inputProfileCsvDirty;
        private int _inputProfileCsvStageVersion;
        private int _inputProfileCsvAppliedVersion;
        private int _inputProfileCsvStageFault;
        private int _nextInputProfileCsvRetryFrame;
        private Vector2 _pendingLookDelta;
        private Vector2 _visualLookDelta;
        private uint _latchedActionBits;
        private float _appliedLowMotorSpeed;
        private float _appliedHighMotorSpeed;
        private float _appliedLeftXRHapticAmplitude;
        private float _appliedRightXRHapticAmplitude;
        private float _nextLeftXRHapticWriteTime;
        private float _nextRightXRHapticWriteTime;
        private float _lookBlendElapsed;
        private float _lastPublishedXRToolTriggerStrength = -1f;
        private float _lastPublishedXRSecondaryTriggerStrength = -1f;
        private float _hapticDispatchAccumulator;
        private uint _lastPublishedXRToolTriggerMask;
        private uint _xrRuntimeFlags;
        private uint _currentInputSchemeHash;
        private uint _lastTelemetryInputSchemeHash;
        private uint _previousButtonMask;
        private uint _lastPollingTimeMicroseconds;
        private uint _bufferedInputsConsumedThisFrame;
        private ushort _lastHapticCommandsActive;
        private ushort _toolTriggerSequence;
        private ushort _deviceLostPauseSequence;
        private byte _lastPublishedXRToolTriggerFlags;
        private byte _lastPublishedXRToolDominantController;
        private bool _lookBlendActive;
        private bool _lastAutomationOverrideApplied;
        private bool _pollActionsCached;
        private bool _deterministicVaultBuffersReady;
        private bool _deterministicVaultBuffersCleared;
        private bool _xrVaultBuffersCleared;
        private Vector2 _lookBlendFrom;
        private Vector2 _lastDeliveredLookDelta;
        private PlayerInputState _currentState;
        private InputState _currentInputState;
        private InputState _previousInputState;
        private InputProfileDTO _stagedInputProfileCsv;
        private uint _standardInputFrame;
        private uint _inputStateSequence;
        private uint _playerInputSignalSequence;
        private uint _lastDeterministicInputFrame = uint.MaxValue;
        [SerializeField, Range(0, MaxInputDelayFrames)]
        private int _inputDelayFrames;
        private double _standardInputAccumulator;
        private string _inputProfileCsvPath;

        internal static InputDispatcher ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        // COLD ALLOC: BufferedActionEntry[10] - fixed player action buffering ring for pre-commit intent capture - owner: InputDispatcher
        private readonly BufferedActionEntry[] _bufferedActions = new BufferedActionEntry[BufferedActionCapacity];
        // COLD ALLOC: object[1] - deterministic input replay writer gate - owner: InputDispatcher
        private readonly object _inputReplayGate = new object();
        // COLD ALLOC: object[1] - CSV profile stage gate; file I/O happens outside PRE_SIMULATION - owner: InputDispatcher
        private readonly object _inputProfileCsvStageGate = new object();
        /// <summary>
        /// Returns true once the dispatcher is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        public int InputDelayFrames
        {
            get => _inputDelayFrames;
            set => _inputDelayFrames = Mathf.Clamp(value, 0, MaxInputDelayFrames);
        }

        public InputState CurrentInputState => _currentInputState;

        public InputState PreviousInputState => _previousInputState;

        public Vector2 VisualLookDelta => _visualLookDelta;

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
            CachePollingActions();
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
            RefreshCachedPlayerRuntimeContext();
            EnsureHapticDeviceBinding();
            RefreshXRNativeBufferState();
            EnsureDeterministicInputNativeBuffers();
            EnsureInputProfileCsvWatcher();
            EnsureInputReplayWriter();
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
            RefreshCachedPlayerRuntimeContext();
            EnsureHapticDeviceBinding();
            RefreshXRNativeBufferState();
            EnsureDeterministicInputNativeBuffers();
            EnsureInputProfileCsvWatcher();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

            EnsureInputBinding();
            EnsureHapticDeviceBinding();
            RefreshXRNativeBufferState();
            EnsureDeterministicInputNativeBuffers();
            EnsureInputProfileCsvWatcher();
            EnsureInputReplayWriter();

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
            _playerContext = null;
            DisposeXRNativeBuffers(default);
            StopInputReplayWriter();
            DisposeInputProfileCsvWatcher();
            DisposeDeterministicInputNativeBuffers(default);

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
            SteamDeckInputPal.BindGamepad(null);
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
            UpdateVisualLookInterpolation();
            DrainToolHaptics(deltaTime);
        }

        public void PreSimulationInputTick(float deltaTime)
        {
            EnsureDeterministicInputNativeBuffers();
            ApplyPendingInputProfileCsv();
            EnsureInputReplayWriter();

            float sanitizedDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? deltaTime
                : (float)StandardInputTickIntervalSeconds;
            _standardInputAccumulator += sanitizedDeltaTime;

            int substepCount = 0;
            while ((_standardInputAccumulator >= StandardInputTickIntervalSeconds || _inputStateSequence == 0u) &&
                   substepCount < MaxStandardInputSubstepsPerFrame)
            {
                if (_standardInputAccumulator >= StandardInputTickIntervalSeconds)
                    _standardInputAccumulator -= StandardInputTickIntervalSeconds;
                else
                    _standardInputAccumulator = 0d;

                CaptureState((float)StandardInputTickIntervalSeconds);
                PublishDeterministicInputState(_standardInputFrame++);
                RunMockCollisionHapticJob(_standardInputFrame - 1u);
                substepCount++;
            }

            if (substepCount >= MaxStandardInputSubstepsPerFrame &&
                _standardInputAccumulator >= StandardInputTickIntervalSeconds)
            {
                _standardInputAccumulator = 0d;
            }
        }

        /// <summary>
        /// Returns the cached input snapshot for the current frame.
        /// </summary>
        /// <returns>Current frame snapshot.</returns>
        public PlayerInputState GetState()
        {
            return _currentState;
        }

        public bool TryGetInputState(uint frame, out InputState state)
        {
            if (!TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputStateRing))
            {
                state = default;
                return false;
            }

            InputState candidate = inputStateRing[(int)(frame % DeterministicInputRingCapacity)];
            if (candidate.Frame == frame)
            {
                state = candidate;
                return true;
            }

            state = default;
            return false;
        }

        private void PublishDeterministicInputState(uint currentFrame)
        {
            if (_lastDeterministicInputFrame == currentFrame)
                return;

            if (!TryResolveInputBuffer(in _inputJournalHandle, DeterministicInputRingCapacity, out NativeArray<InputStateDTO> inputJournal) ||
                !TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputStateRing))
                return;

            _lastDeterministicInputFrame = currentFrame;
            InputState rawState = BuildInputState(_currentState, currentFrame, unchecked(++_inputStateSequence));
            InputStateDTO rawDto = BuildInputStateDto(_currentState);
            int ringIndex = (int)(currentFrame % DeterministicInputRingCapacity);
            inputJournal[ringIndex] = rawDto;
            inputStateRing[ringIndex] = rawState;
            WriteButtonMaskWindow(rawState.ButtonsBitmask);
            if (_deterministicInputCount < DeterministicInputRingCapacity)
                _deterministicInputCount++;

            int delayFrames = _inputDelayFrames;
            InputState resolvedState = rawState;
            byte appliedDelayFrames = 0;
            if (delayFrames > 0 &&
                _deterministicInputCount > delayFrames &&
                currentFrame >= delayFrames)
            {
                uint delayedFrame = currentFrame - (uint)delayFrames;
                if (TryGetInputState(delayedFrame, out InputState delayedState))
                {
                    resolvedState = delayedState;
                    resolvedState.Flags |= (ushort)InputStateFlags.DelayApplied;
                    appliedDelayFrames = (byte)delayFrames;
                }
            }

            _previousInputState = _currentInputState;
            _currentInputState = resolvedState;
            ApplyResolvedInputStateToPlayerSnapshot(resolvedState);
            WriteCurrentInputDto(BuildInputStateDtoFromResolvedState(in resolvedState));

            InputStateSignal signal = default;
            signal.State = resolvedState;
            signal.CurrentInputSchemeHash = _currentInputSchemeHash;
            signal.InputDelayFrames = (byte)delayFrames;
            signal.AppliedDelayFrames = appliedDelayFrames;
            signal.Flags = resolvedState.Flags;
            SignalBus<InputStateSignal>.Push(in signal);
            PublishDiscreteInputSignals(resolvedState.ButtonsBitmask, _previousButtonMask);
            _previousButtonMask = resolvedState.ButtonsBitmask;
            WriteDeterministicInputBlackBox(in resolvedState, _currentInputSchemeHash);
            if ((resolvedState.Sequence % StandardInputRingCapacity) == 0u)
                StageInputReplaySnapshot();
        }

        private static InputStateDTO BuildInputStateDto(in PlayerInputState source)
        {
            return new InputStateDTO
            {
                LookDelta = new float2(source.LookDelta.x, source.LookDelta.y),
                MoveAxis = new float2(source.MoveDelta.x, source.MoveDelta.y),
                ButtonMask = source.ActionsBitmask
            };
        }

        private static InputStateDTO BuildInputStateDtoFromResolvedState(in InputState source)
        {
            float2 look = new float2(
                source.LookX * InputState.LookInvQuantizeScale,
                source.LookY * InputState.LookInvQuantizeScale);
            float2 move = new float2(
                source.MoveX * InputState.AxisInvQuantizeScale,
                source.MoveY * InputState.AxisInvQuantizeScale);
            return new InputStateDTO
            {
                LookDelta = look,
                MoveAxis = move,
                ButtonMask = source.ButtonsBitmask
            };
        }

        private void WriteCurrentInputDto(in InputStateDTO inputStateDto)
        {
            if (!TryResolveInputBuffer(in _currentInputDtoHandle, 1, out NativeArray<InputStateDTO> currentInputDto))
                return;

            currentInputDto[0] = inputStateDto;
        }

        private void WriteButtonMaskWindow(uint buttonMask)
        {
            if (!TryResolveInputBuffer(in _buttonMaskWindowHandle, ButtonMaskWindowCapacity, out NativeArray<uint> window))
                return;

            int writeIndex = _buttonMaskWindowWriteIndex;
            window[writeIndex] = buttonMask;
            _buttonMaskWindowWriteIndex = (writeIndex + 1) % ButtonMaskWindowCapacity;
        }

        private void PublishDiscreteInputSignals(uint currentButtonMask, uint previousButtonMask)
        {
            uint pressed = currentButtonMask & ~previousButtonMask;
            if (pressed == 0u)
                return;

            InputLatencyTracker.MarkInputCaptured();
            if ((pressed & (uint)PlayerInputAction.Jump) != 0u)
                BufferAction(PlayerBufferedAction.Jump);
            if ((pressed & (uint)PlayerInputAction.Pda) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.TogglePda);
                OnPDA?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.Inventory) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToggleInventory);
                OnInventory?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.Cancel) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.Cancel);
                OnCancel?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.TabNext) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.TabNext);
                OnTabNext?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.TabPrevious) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.TabPrevious);
                OnTabPrevious?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot1) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot1);
                OnToolSlot1?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot2) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot2);
                OnToolSlot2?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot3) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot3);
                OnToolSlot3?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot4) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot4);
                OnToolSlot4?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.Flashlight) != 0u)
                PublishPlayerInputCommand(PlayerInputSignalCommands.Flashlight);
            if ((pressed & (uint)PlayerInputAction.Interact) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.Interact);
                OnInteract?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.PrimaryFire) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.PrimaryAction);
                OnPrimaryAction?.Invoke();
            }
            if ((pressed & (uint)PlayerInputAction.SecondaryFire) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.SecondaryAction);
                OnSecondaryAction?.Invoke();
            }
        }

        private InputState BuildInputState(in PlayerInputState source, uint frame, uint sequence)
        {
            ushort flags = 0;
            if (_lastAutomationOverrideApplied)
                flags |= (ushort)InputStateFlags.AutomationOverride;

            Vector2 move = source.MoveDelta;
            float moveLengthSq = move.sqrMagnitude;
            if (moveLengthSq > 1.0f)
                move *= math.rsqrt(moveLengthSq);

            return new InputState
            {
                Frame = frame,
                Sequence = sequence,
                MoveX = InputState.QuantizeUnit(move.x, ref flags),
                MoveY = InputState.QuantizeUnit(move.y, ref flags),
                LookX = InputState.QuantizeLook(source.LookDelta.x, ref flags),
                LookY = InputState.QuantizeLook(source.LookDelta.y, ref flags),
                Vertical = InputState.QuantizeUnit(source.VerticalDelta, ref flags),
                Flags = flags,
                ButtonsBitmask = source.ActionsBitmask
            };
        }

        private void ApplyResolvedInputStateToPlayerSnapshot(in InputState state)
        {
            float2 move = new float2(
                state.MoveX * InputState.AxisInvQuantizeScale,
                state.MoveY * InputState.AxisInvQuantizeScale);
            float2 look = new float2(
                state.LookX * InputState.LookInvQuantizeScale,
                state.LookY * InputState.LookInvQuantizeScale);
            _currentState.MoveDelta = new Vector2(move.x, move.y);
            _currentState.LookDelta = new Vector2(look.x, look.y);
            _currentState.VerticalDelta = math.clamp(state.Vertical * InputState.AxisInvQuantizeScale, -1f, 1f);
            _currentState.ActionsBitmask = state.ButtonsBitmask;
            _visualLookDelta = _currentState.LookDelta;
        }

        private void UpdateVisualLookInterpolation()
        {
            float alpha = (float)math.saturate(_standardInputAccumulator * StandardInputTickRateHz);
            float2 previous = new float2(
                _previousInputState.LookX * InputState.LookInvQuantizeScale,
                _previousInputState.LookY * InputState.LookInvQuantizeScale);
            float2 current = new float2(
                _currentInputState.LookX * InputState.LookInvQuantizeScale,
                _currentInputState.LookY * InputState.LookInvQuantizeScale);
            float2 interpolated = math.lerp(previous, current, alpha);
            _visualLookDelta = new Vector2(interpolated.x, interpolated.y);
        }

        private void EnsureDeterministicInputNativeBuffers()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnsafeUtility.SizeOf<InputState>() != InputStateSizeBytes)
                Debug.LogError("[InputDispatcher] InputState ABI violation; expected 24 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<PlayerInputState>() != PlayerInputStateSizeBytes)
                Debug.LogError("[InputDispatcher] PlayerInputState ABI violation; expected 64 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<InputStateDTO>() != InputStateDtoSizeBytes)
                Debug.LogError("[InputDispatcher] InputStateDTO ABI violation; expected 24 bytes.");
            if (UnsafeUtility.SizeOf<HapticCommandDTO>() != HapticCommandDtoSizeBytes)
                Debug.LogError("[InputDispatcher] HapticCommandDTO ABI violation; expected 16 bytes.");
            if (UnsafeUtility.SizeOf<XRInputState>() != XRInputStateSizeBytes)
                Debug.LogError("[InputDispatcher] XRInputState ABI violation; expected 64 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<BufferedActionEntry>() != BufferedActionEntrySizeBytes)
                Debug.LogError("[InputDispatcher] BufferedActionEntry ABI violation; expected 16 bytes with natural ARM64 alignment.");
#endif
            if (_deterministicVaultBuffersReady && ValidateDeterministicInputBuffers())
                return;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool ready =
                TryResolveOrAcquireInputBuffer(
                    ref _currentInputDtoHandle,
                    BufferID.ShinobuInputCurrentDto,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputJournalHandle,
                    BufferID.ShinobuInputJournalRing,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputStateBridgeRingHandle,
                    BufferID.ShinobuInputStateBridgeRing,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _buttonMaskWindowHandle,
                    BufferID.ShinobuInputButtonMaskWindow,
                    ButtonMaskWindowCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputBlockMaskHandle,
                    BufferID.ShinobuInputBlockMask,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputProfileHandle,
                    BufferID.ShinobuInputProfile,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputTelemetryHandle,
                    BufferID.ShinobuInputTelemetryRing,
                    InputBlackBoxCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputReplaySnapshotHandle,
                    BufferID.ShinobuInputReplaySnapshot,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticCommandDtoHandle,
                    BufferID.ShinobuInputHapticCommands,
                    HapticCommandDtoCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _inputProfileCsvScratchHandle,
                    BufferID.ShinobuInputCsvScratch,
                    4096,
                    NativeArrayOptions.UninitializedMemory,
                    out _);

            _deterministicVaultBuffersReady = ready;

            if (!_deterministicVaultBuffersReady)
                return;

            if (_deterministicVaultBuffersCleared)
                return;

            ClearVaultBuffer(ref _currentInputDtoHandle);
            ClearVaultBuffer(ref _inputJournalHandle);
            ClearVaultBuffer(ref _inputStateBridgeRingHandle);
            ClearVaultBuffer(ref _buttonMaskWindowHandle);
            ClearVaultBuffer(ref _inputBlockMaskHandle);
            ClearVaultBuffer(ref _inputTelemetryHandle);
            ClearVaultBuffer(ref _inputReplaySnapshotHandle);
            ClearVaultBuffer(ref _hapticCommandDtoHandle);
            ClearVaultBuffer(ref _inputProfileCsvScratchHandle);
            InitializeDefaultInputProfile();
            _deterministicVaultBuffersCleared = true;
        }

        private void DisposeDeterministicInputNativeBuffers(JobHandle dependency)
        {
            ReleaseInputVaultHandles(_dataVault);
            _dataVault = null;
            _deterministicVaultBuffersReady = false;
            _deterministicVaultBuffersCleared = false;
            _xrVaultBuffersCleared = false;
        }

        private bool ValidateDeterministicInputBuffers()
        {
            return TryResolveInputBuffer(in _currentInputDtoHandle, 1, out _) &&
                   TryResolveInputBuffer(in _inputJournalHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _buttonMaskWindowHandle, ButtonMaskWindowCapacity, out _) &&
                   TryResolveInputBuffer(in _inputBlockMaskHandle, 1, out _) &&
                   TryResolveInputBuffer(in _inputProfileHandle, 1, out _) &&
                   TryResolveInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out _) &&
                   TryResolveInputBuffer(in _inputReplaySnapshotHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out _) &&
                   TryResolveInputBuffer(in _inputProfileCsvScratchHandle, 4096, out _);
        }

        private bool TryResolveOrAcquireInputBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryResolveInputBuffer(in handle, requiredLength, out buffer))
                return true;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.CoreDeterminism,
                options);

            return TryResolveInputBuffer(in handle, requiredLength, out buffer);
        }

        private bool TryResolveInputBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void ClearVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!TryResolveInputBuffer(in handle, 1, out NativeArray<T> buffer))
                return;

            UnsafeUtility.MemClear(
                NativeArrayUnsafeUtility.GetUnsafePtr(buffer),
                (long)buffer.Length * UnsafeUtility.SizeOf<T>());
        }

        private void ReleaseInputVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _currentInputDtoHandle);
            ReleaseVaultHandle(vault, ref _inputJournalHandle);
            ReleaseVaultHandle(vault, ref _inputStateBridgeRingHandle);
            ReleaseVaultHandle(vault, ref _buttonMaskWindowHandle);
            ReleaseVaultHandle(vault, ref _inputBlockMaskHandle);
            ReleaseVaultHandle(vault, ref _inputProfileHandle);
            ReleaseVaultHandle(vault, ref _inputTelemetryHandle);
            ReleaseVaultHandle(vault, ref _inputReplaySnapshotHandle);
            ReleaseVaultHandle(vault, ref _hapticCommandDtoHandle);
            ReleaseVaultHandle(vault, ref _xrInputStatesHandle);
            ReleaseVaultHandle(vault, ref _xrLookAtRayCommandsHandle);
            ReleaseVaultHandle(vault, ref _inputProfileCsvScratchHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void InitializeDefaultInputProfile()
        {
            if (!TryResolveInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO> profiles))
                return;

            InputProfileDTO profile = profiles[0];
            profile.InnerDeadzone = DefaultInnerDeadzone;
            profile.OuterDeadzone = DefaultOuterDeadzone;
            profile.MoveExponent = DefaultMoveExponent;
            profile.MouseSensitivity = DefaultMouseSensitivity;
            profile.MouseAcceleration = DefaultMouseAcceleration;
            profile.HapticPowerScale = DefaultHapticPowerScale;
            profile.HapticDispatchIntervalSeconds = ThermalHapticDispatchIntervalSeconds;
            profile.HapticThermalAmplitudeScale = DefaultHapticThermalAmplitudeScale;
            profile.Flags = 0u;
            profiles[0] = profile;

            lock (_inputProfileCsvStageGate)
            {
                _stagedInputProfileCsv = profile;
                _inputProfileCsvStageVersion = 0;
                _inputProfileCsvAppliedVersion = 0;
            }

            Interlocked.Exchange(ref _inputProfileCsvStageFault, 0);
        }

        private void EnsureInputProfileCsvWatcher()
        {
            if (!Application.isPlaying || _inputProfileCsvWatcher != null)
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            _inputProfileCsvPath = Path.Combine(projectRoot, "input_profiles.csv");
            if (TryStageInputProfileCsvFromFile())
            {
                ApplyStagedInputProfileCsvToVault();
                Interlocked.Exchange(ref _inputProfileCsvDirty, 0);
            }

            try
            {
                FileSystemWatcher watcher = new FileSystemWatcher(projectRoot, "input_profiles.csv");
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                watcher.Changed += HandleInputProfileCsvChanged;
                watcher.Created += HandleInputProfileCsvChanged;
                watcher.Renamed += HandleInputProfileCsvChanged;
                watcher.EnableRaisingEvents = true;
                _inputProfileCsvWatcher = watcher;
            }
            catch (Exception)
            {
                _inputProfileCsvWatcher = null;
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
        }

        private void DisposeInputProfileCsvWatcher()
        {
            FileSystemWatcher watcher = _inputProfileCsvWatcher;
            if (watcher == null)
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleInputProfileCsvChanged;
            watcher.Created -= HandleInputProfileCsvChanged;
            watcher.Renamed -= HandleInputProfileCsvChanged;
            watcher.Dispose();
            _inputProfileCsvWatcher = null;
        }

        private void HandleInputProfileCsvChanged(object sender, FileSystemEventArgs args)
        {
            if (!TryStageInputProfileCsvFromFile())
                Interlocked.Exchange(ref _inputProfileCsvDirty, 1);
        }

        private void ApplyPendingInputProfileCsv()
        {
            int currentFrame = Time.frameCount;
            if (_nextInputProfileCsvRetryFrame > 0 && currentFrame < _nextInputProfileCsvRetryFrame)
                return;

            bool dirty = Interlocked.Exchange(ref _inputProfileCsvDirty, 0) != 0;
            bool retryDue = _nextInputProfileCsvRetryFrame > 0 && currentFrame >= _nextInputProfileCsvRetryFrame;
            if (!dirty && !retryDue)
                return;

            if (ApplyStagedInputProfileCsvToVault())
            {
                _nextInputProfileCsvRetryFrame = 0;
                return;
            }

            if (Interlocked.Exchange(ref _inputProfileCsvStageFault, 0) != 0)
            {
                _nextInputProfileCsvRetryFrame = currentFrame + 30;
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return;
            }

            _nextInputProfileCsvRetryFrame = 0;
        }

        private bool TryStageInputProfileCsvFromFile()
        {
            if (string.IsNullOrEmpty(_inputProfileCsvPath) || !File.Exists(_inputProfileCsvPath))
                return false;

            InputProfileDTO profile;
            lock (_inputProfileCsvStageGate)
                profile = _stagedInputProfileCsv;

            try
            {
                using (FileStream stream = new FileStream(
                    _inputProfileCsvPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    512,
                    FileOptions.SequentialScan))
                {
                    Span<byte> line = stackalloc byte[256];
                    int length = 0;
                    bool overflow = false;
                    int next;
                    while ((next = stream.ReadByte()) >= 0)
                    {
                        byte c = (byte)next;
                        if (c == (byte)'\n' || c == (byte)'\r')
                        {
                            if (!overflow && length > 0)
                                ParseInputProfileCsvLine(line.Slice(0, length), ref profile);

                            length = 0;
                            overflow = false;
                            continue;
                        }

                        if (overflow)
                            continue;

                        if (length >= line.Length)
                        {
                            length = 0;
                            overflow = true;
                            continue;
                        }

                        line[length++] = c;
                    }

                    if (!overflow && length > 0)
                        ParseInputProfileCsvLine(line.Slice(0, length), ref profile);
                }

                lock (_inputProfileCsvStageGate)
                {
                    _stagedInputProfileCsv = profile;
                    unchecked
                    {
                        _inputProfileCsvStageVersion++;
                        if (_inputProfileCsvStageVersion == 0)
                            _inputProfileCsvStageVersion = 1;
                    }
                }

                Interlocked.Exchange(ref _inputProfileCsvStageFault, 0);
                Interlocked.Exchange(ref _inputProfileCsvDirty, 1);
                return true;
            }
            catch (IOException)
            {
                Interlocked.Exchange(ref _inputProfileCsvStageFault, 1);
                return false;
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _inputProfileCsvStageFault, 1);
                return false;
            }
        }

        private bool ApplyStagedInputProfileCsvToVault()
        {
            if (!TryResolveInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO> profiles))
                return false;

            InputProfileDTO stagedProfile;
            int stagedVersion;
            lock (_inputProfileCsvStageGate)
            {
                stagedVersion = _inputProfileCsvStageVersion;
                if (stagedVersion == _inputProfileCsvAppliedVersion)
                    return false;

                stagedProfile = _stagedInputProfileCsv;
            }

            profiles[0] = stagedProfile;
            _inputProfileCsvAppliedVersion = stagedVersion;
            return true;
        }

        private static void ParseInputProfileCsvLine(ReadOnlySpan<byte> line, ref InputProfileDTO profile)
        {
            line = TrimAscii(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return;

            int comma = line.IndexOf((byte)',');
            if (comma <= 0 || comma >= line.Length - 1)
                return;

            ReadOnlySpan<byte> key = TrimAscii(line.Slice(0, comma));
            ReadOnlySpan<byte> valueSpan = TrimAscii(line.Slice(comma + 1));
            if (!TryParseAsciiFloat(valueSpan, out float value))
                return;

            uint keyHash = HashLowerAscii(key);
            switch (keyHash)
            {
                case 0x36EF9B38u:
                    profile.InnerDeadzone = math.clamp(value, 0f, 0.95f);
                    break;
                case 0xEDE7BA61u:
                    profile.OuterDeadzone = math.clamp(value, profile.InnerDeadzone + 0.0001f, 1f);
                    break;
                case 0x4F81BC32u:
                    profile.MoveExponent = math.clamp(value, 0.25f, 4f);
                    break;
                case 0xBE81D968u:
                    profile.MouseSensitivity = math.clamp(value, 0.01f, 20f);
                    break;
                case 0x46C10549u:
                    profile.MouseAcceleration = math.clamp(value, 0f, 8f);
                    break;
                case 0x9D5E6D29u:
                    profile.HapticPowerScale = math.clamp(value, 0f, 2f);
                    break;
                case 0x29497DD7u:
                case 0x6007B9B9u:
                    profile.HapticThermalAmplitudeScale = math.clamp(value, 0f, 1f);
                    break;
                case 0x43F56491u:
                case 0x498660BBu:
                    profile.HapticDispatchIntervalSeconds = math.clamp(value, 1f / 120f, 0.25f);
                    break;
                case 0x88784214u:
                    profile.Flags = value > 0.5f ? profile.Flags | InputProfileFlagEnableMockCollision : profile.Flags & ~InputProfileFlagEnableMockCollision;
                    break;
            }
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= (byte)' ')
                start++;
            while (end >= start && value[end] <= (byte)' ')
                end--;

            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
            {
                byte c = key[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (value[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < value.Length)
            {
                byte c = value[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                integer = (integer * 10f) + (c - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fraction = 0f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < value.Length)
                {
                    byte c = value[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit)
                return false;

            result = (integer + fraction) * sign;
            return math.isfinite(result);
        }

        private void WriteDeterministicInputBlackBox(in InputState state, uint currentInputSchemeHash)
        {
            if (!TryResolveInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out NativeArray<InputTelemetryEntryDTO> telemetry))
                return;

            int writeIndex = _deterministicBlackBoxWriteIndex;
            int wrappedIndex = writeIndex % InputBlackBoxCapacity;
            uint packedAxes = PackInputAxes(in state);
            telemetry[wrappedIndex] = new InputTelemetryEntryDTO
            {
                InputSystemTimeSeconds = UnityEngine.InputSystem.LowLevel.InputState.currentTime,
                Frame = state.Frame,
                Sequence = state.Sequence,
                ButtonMask = state.ButtonsBitmask,
                CurrentInputSchemeHash = currentInputSchemeHash,
                PollingTimeMicroseconds = _lastPollingTimeMicroseconds,
                BufferedInputsConsumed = _bufferedInputsConsumedThisFrame,
                HapticCommandsActive = _lastHapticCommandsActive,
                Flags = state.Flags
            };
            _bufferedInputsConsumedThisFrame = 0u;
            _deterministicBlackBoxWriteIndex = (writeIndex + 1) % InputBlackBoxCapacity;
            CrashTelemetryBuffer.ReportDeterministicInputFrame(
                state.Frame,
                state.Sequence,
                state.ButtonsBitmask,
                packedAxes);

            if ((state.Flags & (ushort)InputStateFlags.NonFiniteSanitized) != 0 ||
                _lastPollingTimeMicroseconds > 500u)
            {
                DumpDeterministicInputBlackBox();
            }
        }

        private static uint PackInputAxes(in InputState state)
        {
            return (uint)(ushort)state.MoveX |
                   ((uint)(ushort)state.MoveY << 16);
        }

        private void StageInputReplaySnapshot()
        {
            if (!TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputStateRing) ||
                !TryResolveInputBuffer(in _inputReplaySnapshotHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputReplaySnapshot) ||
                _inputReplaySignal == null)
                return;

            lock (_inputReplayGate)
            {
                for (int i = 0; i < DeterministicInputRingCapacity; i++)
                    inputReplaySnapshot[i] = inputStateRing[i];

#if HECTON8_MMF_AVAILABLE
                if (_inputReplayPointer != null)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputReplaySnapshot);
                    UnsafeUtility.MemCpy(_inputReplayPointer + InputReplayHeaderBytes, source, InputReplayPayloadBytes);
                }
#endif
            }

            Interlocked.Exchange(ref _inputReplayWritePending, 1);
            _inputReplaySignal.Set();
        }

        private void EnsureInputReplayWriter()
        {
            if (!Application.isPlaying || _inputReplayThread != null)
                return;

            int currentFrame = Time.frameCount;
            if (_nextInputReplayRetryFrame > currentFrame)
                return;

#if !HECTON8_MMF_AVAILABLE
            _nextInputReplayRetryFrame = int.MaxValue;
            return;
#else
            try
            {
                string replayPath = Path.Combine(Application.persistentDataPath, InputReplayFileName);
                string directory = Path.GetDirectoryName(replayPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                _inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
                _inputReplayStream.SetLength(InputReplayMappedBytes);
                _inputReplayMappedFile = MemoryMappedFile.CreateFromFile(
                    _inputReplayStream,
                    null,
                    InputReplayMappedBytes,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    false);
                _inputReplayAccessor = _inputReplayMappedFile.CreateViewAccessor(0L, InputReplayMappedBytes, MemoryMappedFileAccess.ReadWrite);
                _inputReplayAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _inputReplayPointer);
                if (_inputReplayPointer == null)
                {
                    MarkInputReplaySetupFailure(currentFrame);
                    ReleaseInputReplayMap();
                    return;
                }

                WriteInputReplayHeader();
                _inputReplaySignal = new AutoResetEvent(false); // COLD ALLOC: AutoResetEvent[1] - deterministic input replay signal - owner: InputDispatcher
                Interlocked.Exchange(ref _inputReplayStopRequested, 0);
                _inputReplayThread = new Thread(InputReplayWriterLoop)
                {
                    IsBackground = true,
                    Name = "H8.InputReplayMMF"
                }; // COLD ALLOC: Thread[1] - deterministic input replay MMF writer - owner: InputDispatcher
                _inputReplayThread.Start();
                _nextInputReplayRetryFrame = 0;
            }
            catch (Exception)
            {
                MarkInputReplaySetupFailure(currentFrame);
                Interlocked.Exchange(ref _inputReplayStopRequested, 1);
                Interlocked.Exchange(ref _inputReplayWritePending, 0);
                _inputReplayThread = null;
                _inputReplaySignal?.Dispose();
                _inputReplaySignal = null;
                ReleaseInputReplayMap();
            }
#endif
        }

        private void MarkInputReplaySetupFailure(int currentFrame)
        {
            CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            _nextInputReplayRetryFrame = currentFrame + InputReplayRetryIntervalFrames;
        }

        private void StopInputReplayWriter()
        {
            Interlocked.Exchange(ref _inputReplayStopRequested, 1);
            AutoResetEvent signal = _inputReplaySignal;
            signal?.Set();

            Thread thread = _inputReplayThread;
            bool stopped = true;
            if (thread != null && thread.IsAlive)
                stopped = thread.Join(2000);

            if (!stopped)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return;
            }

            _inputReplayThread = null;
            _inputReplaySignal?.Dispose();
            _inputReplaySignal = null;
            Interlocked.Exchange(ref _inputReplayWritePending, 0);
            ReleaseInputReplayMap();
        }

        private void ReleaseInputReplayMap()
        {
#if HECTON8_MMF_AVAILABLE
            if (_inputReplayPointer != null && _inputReplayAccessor != null)
            {
                _inputReplayAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _inputReplayPointer = null;
            }

            _inputReplayAccessor?.Dispose();
            _inputReplayAccessor = null;
            _inputReplayMappedFile?.Dispose();
            _inputReplayMappedFile = null;
#endif
            _inputReplayStream?.Dispose();
            _inputReplayStream = null;
        }

        private void WriteInputReplayHeader()
        {
#if HECTON8_MMF_AVAILABLE
            if (_inputReplayPointer == null)
                return;

            UnsafeUtility.WriteArrayElement(_inputReplayPointer, 0, InputReplayMagic);
            UnsafeUtility.WriteArrayElement(_inputReplayPointer + 8, 0, InputReplayVersion);
            UnsafeUtility.WriteArrayElement(_inputReplayPointer + 12, 0, (uint)DeterministicInputRingCapacity);
#endif
        }

        private void InputReplayWriterLoop()
        {
#if HECTON8_MMF_AVAILABLE
            try
            {
                while (Volatile.Read(ref _inputReplayStopRequested) == 0)
                {
                    AutoResetEvent signal = _inputReplaySignal;
                    if (signal == null)
                        return;

                    signal.WaitOne(1000);
                    if (Interlocked.Exchange(ref _inputReplayWritePending, 0) == 0)
                        continue;

                    MemoryMappedViewAccessor accessor = _inputReplayAccessor;
                    lock (_inputReplayGate)
                    {
                        if (_inputReplayPointer == null)
                            continue;
                    }

                    accessor?.Flush();
                }
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                Interlocked.Exchange(ref _inputReplayStopRequested, 1);
            }
#endif
        }

        private void DumpDeterministicInputBlackBox()
        {
            if (!TryResolveInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out NativeArray<InputTelemetryEntryDTO> telemetry))
                return;

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string path = Path.Combine(projectRoot, InputDumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<InputTelemetryEntryDTO>();
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
        }

        /// <summary>
        /// Returns the read-only OpenXR controller snapshot buffer: index 0 left, index 1 right.
        /// </summary>
        internal NativeArray<XRInputState>.ReadOnly GetXRInputStatesReadOnly()
        {
            return TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates) ? xrInputStates.AsReadOnly() : default;
        }

        /// <summary>
        /// Returns the single-command eye/look ray buffer staged for menu and diegetic selection.
        /// </summary>
        internal NativeArray<RaycastCommand>.ReadOnly GetXRLookAtRayCommandsReadOnly()
        {
            return TryResolveXRLookAtRayCommands(out NativeArray<RaycastCommand> commands) ? commands.AsReadOnly() : default;
        }

        internal bool TryGetXRInputState(byte controllerIndex, out XRInputState state)
        {
            state = default;
            if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates) ||
                !HectonXRRuntimeState.IsXRActive ||
                controllerIndex >= xrInputStates.Length)
            {
                return false;
            }

            state = xrInputStates[controllerIndex];
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
        /// Adds a discrete action token to the fixed 10-frame input buffer.
        /// </summary>
        /// <param name="action">Buffered action token.</param>
        public void BufferAction(PlayerBufferedAction action)
        {
            if (action == PlayerBufferedAction.None)
                return;

            _bufferedActions[_bufferWriteIndex].Action = action;
            _bufferedActions[_bufferWriteIndex].Frame = Time.frameCount;
            _bufferWriteIndex++;
            if (_bufferWriteIndex >= BufferedActionCapacity)
                _bufferWriteIndex = 0;
        }

        /// <summary>
        /// Consumes the newest valid buffered action matching the requested token.
        /// </summary>
        /// <param name="action">Buffered action to consume.</param>
        /// <param name="maxAgeSeconds">Maximum valid age converted to a deterministic 60 Hz frame window. Negative values use the full ten-frame ring.</param>
        /// <returns>True when a valid buffered action was consumed.</returns>
        public bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds)
        {
            if (action == PlayerBufferedAction.None)
                return false;

            int validFrameWindow = maxAgeSeconds > 0f
                ? math.clamp((int)math.ceil(maxAgeSeconds * (float)StandardInputTickRateHz), 1, BufferedActionCapacity)
                : BufferedActionCapacity;
            int currentFrame = Time.frameCount;

            for (int offset = 0; offset < BufferedActionCapacity; offset++)
            {
                int index = _bufferWriteIndex - 1 - offset;
                if (index < 0)
                    index += BufferedActionCapacity;

                BufferedActionEntry entry = _bufferedActions[index];
                if (entry.Action != action)
                    continue;

                if (currentFrame - entry.Frame >= validFrameWindow)
                {
                    _bufferedActions[index].Action = PlayerBufferedAction.None;
                    continue;
                }

                _bufferedActions[index].Action = PlayerBufferedAction.None;
                _bufferedInputsConsumedThisFrame++;
                return true;
            }

            return false;
        }

        public bool CheckBufferedInput(uint buttonBit, int frames)
        {
            if (buttonBit == 0u)
                return false;

            if (!TryResolveInputBuffer(in _buttonMaskWindowHandle, ButtonMaskWindowCapacity, out NativeArray<uint> window))
                return false;

            int frameCount = math.clamp(frames, 1, ButtonMaskWindowCapacity);
            for (int offset = 0; offset < frameCount; offset++)
            {
                int index = _buttonMaskWindowWriteIndex - 1 - offset;
                if (index < 0)
                    index += ButtonMaskWindowCapacity;

                if ((window[index] & buttonBit) == 0u)
                    continue;

                _bufferedInputsConsumedThisFrame++;
                return true;
            }

            return false;
        }

        public bool TryGetCurrentInputStateDto(out InputStateDTO state)
        {
            state = default;
            if (!TryResolveInputBuffer(in _currentInputDtoHandle, 1, out NativeArray<InputStateDTO> currentInputDto))
                return false;

            state = currentInputDto[0];
            return true;
        }

        public uint GetInputBlockMask()
        {
            return ReadInputBlockMask();
        }

        public void SetInputBlockMask(uint mask)
        {
            if (!TryResolveInputBuffer(in _inputBlockMaskHandle, 1, out NativeArray<uint> inputBlockMask))
                return;

            inputBlockMask[0] = mask;
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

            CachePollingActions();
            _subscribedToNativeInput = true;
        }

        private void UnsubscribeFromNativeInput()
        {
            if (!_subscribedToNativeInput || _nativeInputManager == null)
                return;

            _subscribedToNativeInput = false;
            ClearPollingActions();
        }

        private void CachePollingActions()
        {
            InputManager inputManager = _nativeInputManager;
            if (inputManager == null)
            {
                ClearPollingActions();
                return;
            }

            _pollMoveAction = inputManager.GetAction("Movement");
            _pollLookAction = inputManager.GetAction("Look");
            _pollJumpAction = inputManager.GetAction("Jump");
            _pollSprintAction = inputManager.GetAction("Sprint");
            _pollInteractAction = inputManager.GetAction("Interact");
            _pollPrimaryAction = inputManager.GetAction("PrimaryAction");
            _pollSecondaryAction = inputManager.GetAction("SecondaryAction");
            _pollPdaAction = inputManager.GetAction("PDA");
            _pollPauseAction = inputManager.GetAction("Pause");
            _pollInventoryAction = inputManager.GetAction("Inventory");
            _pollCancelAction = inputManager.GetAction("Cancel", "UI");
            _pollTabNextAction = inputManager.GetAction("TabNext", "UI");
            _pollTabPreviousAction = inputManager.GetAction("TabPrevious", "UI");
            _pollToolSlot1Action = inputManager.GetAction("ToolSlot1");
            _pollToolSlot2Action = inputManager.GetAction("ToolSlot2");
            _pollToolSlot3Action = inputManager.GetAction("ToolSlot3");
            _pollToolSlot4Action = inputManager.GetAction("ToolSlot4");
            _pollFlashlightAction = inputManager.GetAction("Flashlight");
            _pollVerticalMovementAction = inputManager.GetAction("VerticalMovement");
            _pollScrollWheelAction = inputManager.GetAction("ScrollWheel", "UI");
            _pollActionsCached = true;
        }

        private void ClearPollingActions()
        {
            _pollMoveAction = null;
            _pollLookAction = null;
            _pollJumpAction = null;
            _pollSprintAction = null;
            _pollInteractAction = null;
            _pollPrimaryAction = null;
            _pollSecondaryAction = null;
            _pollPdaAction = null;
            _pollPauseAction = null;
            _pollInventoryAction = null;
            _pollCancelAction = null;
            _pollTabNextAction = null;
            _pollTabPreviousAction = null;
            _pollToolSlot1Action = null;
            _pollToolSlot2Action = null;
            _pollToolSlot3Action = null;
            _pollToolSlot4Action = null;
            _pollFlashlightAction = null;
            _pollVerticalMovementAction = null;
            _pollScrollWheelAction = null;
            _pollActionsCached = false;
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
                    {
                        _cachedGamepad = gamepad;
                        SteamDeckInputPal.BindGamepad(_cachedGamepad);
                    }
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    if (ReferenceEquals(_cachedGamepad, gamepad))
                    {
                        ResetGamepadHaptics();
                        _cachedGamepad = null;
                        SteamDeckInputPal.BindGamepad(null);
                        PublishDeviceLostPauseSignal(DeviceLostFlagGamepad);
                        ResolveCachedGamepad();
                    }
                    break;
            }
        }

        private void ResolveCachedGamepad()
        {
            if (_cachedGamepad != null && _cachedGamepad.added)
            {
                SteamDeckInputPal.BindGamepad(_cachedGamepad);
                return;
            }

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

            SteamDeckInputPal.BindGamepad(_cachedGamepad);
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
                    bool lostTrackedController = ReferenceEquals(_cachedLeftXRController, controller) ||
                                                 ReferenceEquals(_cachedRightXRController, controller);
                    if (ReferenceEquals(_cachedLeftXRController, controller))
                        ClearLeftXRController();
                    if (ReferenceEquals(_cachedRightXRController, controller))
                        ClearRightXRController();
                    if (lostTrackedController)
                        PublishDeviceLostPauseSignal(DeviceLostFlagXR);
                    ResolveCachedXRControllers();
                    break;
            }
        }

        private void PublishDeviceLostPauseSignal(byte flags)
        {
            SimulationPauseSignal signal = default;
            signal.SourceHash = DeviceLostSignalSourceHash;
            signal.Frame = (uint)Time.frameCount;
            signal.Sequence = ++_deviceLostPauseSequence;
            if (signal.Sequence == 0)
                signal.Sequence = ++_deviceLostPauseSequence;
            signal.Paused = 1;
            signal.Flags = flags;
            signal.RestoreScalar = 1f;
            GlobalSignals.Publish(in signal);
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
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                BindNativeInputManager(currentService as InputManager);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseInputVaultHandles(previousService as IDataVault ?? _dataVault);
                _dataVault = currentService as IDataVault;
                _deterministicVaultBuffersReady = false;
                _deterministicVaultBuffersCleared = false;
                _xrVaultBuffersCleared = false;
            }
        }

        private void RefreshCachedPlayerRuntimeContext()
        {
            if (_playerContext != null)
                return;

            _playerContext = GlobalRegistry.Player;
        }

        private void CaptureState(float deltaTime = 0f)
        {
            EnsureInputBinding();
            RefreshXRNativeBufferState();

            int currentFrame = Time.frameCount;
            if (_lastCapturedFrame == currentFrame)
                return;

            _lastCapturedFrame = currentFrame;
            long pollStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            PlayerInputState state = default;
            uint actionBits = 0u;
            InputManager inputManager = _nativeInputManager;
            if (inputManager != null)
            {
                if (!_pollActionsCached)
                    CachePollingActions();

                actionBits = BuildPolledButtonMask();
                if (inputManager.IsPlayerInputEnabled)
                {
                    InputProfileDTO profile = ReadInputProfile();
                    Vector2 rawMove = ReadActionVector2(_pollMoveAction);
                    Vector2 rawLook = ReadActionVector2(_pollLookAction);
                    float2 moveAxis = ApplyAnalogDeadzone(new float2(rawMove.x, rawMove.y), in profile);
                    float2 lookAxis = ResolveAupAgnosticLookDelta(new float2(rawLook.x, rawLook.y), in profile);
                    Vector2 lookDelta = new Vector2(lookAxis.x, lookAxis.y);
                    if (_lookBlendActive)
                        lookDelta = ResolveLookHotSwapBlend(lookDelta, deltaTime);

                    state.MoveDelta = new Vector2(moveAxis.x, moveAxis.y);
                    state.LookDelta = lookDelta;
                    state.ScrollDelta = ReadActionVector2(_pollScrollWheelAction);
                    state.VerticalDelta = math.clamp(ReadActionFloat(_pollVerticalMovementAction), -1f, 1f);
                    SteamDeckInputPal.Capture(ref state, deltaTime);
                    _lastDeliveredLookDelta = state.LookDelta;
                }
            }

            _pendingLookDelta = Vector2.zero;
            _latchedActionBits = 0u;
            if (HectonXRRuntimeState.IsXRActive)
            {
                RefreshXRInputSnapshot();
                actionBits |= ResolveXRToolActionBitsAndPublishSignal(currentFrame);
                StageXRLookAtRayCommand();
            }
            else
            {
                PublishXRToolTriggerReleaseIfNeeded(currentFrame);
                ClearXRRuntimeFrameStateIfActive();
            }

            state.ActionsBitmask = actionBits;
            bool automationOverrideApplied = ApplyAutomationOverride(ref state, (uint)currentFrame);
            _lastAutomationOverrideApplied = automationOverrideApplied;
            if (automationOverrideApplied)
                _lastDeliveredLookDelta = state.LookDelta;

            ApplyInputBlockMask(ref state, ReadInputBlockMask());
            uint resolvedSchemeHash = ResolveCurrentInputSchemeHash();
            if (automationOverrideApplied && state.CurrentInputSchemeHash != 0u)
                resolvedSchemeHash = state.CurrentInputSchemeHash;

            state.CurrentInputSchemeHash = resolvedSchemeHash;
            _currentInputSchemeHash = state.CurrentInputSchemeHash;
            PublishInputSchemeTelemetryIfChanged(state.CurrentInputSchemeHash, state.PlatformInputFlags, (uint)currentFrame);

            _currentState = state;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - pollStartTicks;
            double elapsedMicroseconds = (double)elapsedTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            _lastPollingTimeMicroseconds = elapsedMicroseconds > uint.MaxValue ? uint.MaxValue : (uint)math.max(0.0, elapsedMicroseconds);
        }

        private InputProfileDTO ReadInputProfile()
        {
            InputProfileDTO fallback = default;
            fallback.InnerDeadzone = DefaultInnerDeadzone;
            fallback.OuterDeadzone = DefaultOuterDeadzone;
            fallback.MoveExponent = DefaultMoveExponent;
            fallback.MouseSensitivity = DefaultMouseSensitivity;
            fallback.MouseAcceleration = DefaultMouseAcceleration;
            fallback.HapticPowerScale = DefaultHapticPowerScale;
            fallback.HapticDispatchIntervalSeconds = ThermalHapticDispatchIntervalSeconds;
            fallback.HapticThermalAmplitudeScale = DefaultHapticThermalAmplitudeScale;

            if (!TryResolveInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO> profiles))
                return fallback;

            InputProfileDTO profile = profiles[0];
            if (!math.isfinite(profile.InnerDeadzone) ||
                !math.isfinite(profile.OuterDeadzone) ||
                !math.isfinite(profile.MoveExponent) ||
                !math.isfinite(profile.HapticDispatchIntervalSeconds) ||
                profile.OuterDeadzone <= 0f)
            {
                return fallback;
            }

            profile.InnerDeadzone = math.clamp(profile.InnerDeadzone, 0f, 0.95f);
            profile.OuterDeadzone = math.clamp(profile.OuterDeadzone, profile.InnerDeadzone + 0.0001f, 1f);
            profile.MoveExponent = math.clamp(profile.MoveExponent, 0.25f, 4f);
            profile.MouseSensitivity = math.clamp(profile.MouseSensitivity, 0.01f, 20f);
            profile.MouseAcceleration = math.clamp(profile.MouseAcceleration, 0f, 8f);
            profile.HapticPowerScale = math.clamp(profile.HapticPowerScale, 0f, 2f);
            profile.HapticDispatchIntervalSeconds = profile.HapticDispatchIntervalSeconds > 0f
                ? math.clamp(profile.HapticDispatchIntervalSeconds, 1f / 120f, 0.25f)
                : ThermalHapticDispatchIntervalSeconds;
            profile.HapticThermalAmplitudeScale = math.clamp(profile.HapticThermalAmplitudeScale, 0f, 1f);
            return profile;
        }

        private uint ReadInputBlockMask()
        {
            if (!TryResolveInputBuffer(in _inputBlockMaskHandle, 1, out NativeArray<uint> inputBlockMask))
                return 0u;

            return inputBlockMask[0];
        }

        private void ApplyInputBlockMask(ref PlayerInputState state, uint blockMask)
        {
            if (blockMask == 0u)
                return;

            if ((blockMask & (uint)InputBlockMaskFlags.BlockMovement) != 0u)
            {
                state.MoveDelta = Vector2.zero;
                state.VerticalDelta = 0f;
                state.ActionsBitmask &= ~InputActionMaskMovement;
            }

            if ((blockMask & (uint)InputBlockMaskFlags.BlockLook) != 0u)
                state.LookDelta = Vector2.zero;

            if ((blockMask & (uint)InputBlockMaskFlags.BlockTools) != 0u)
                state.ActionsBitmask &= ~InputActionMaskTools;

            if ((blockMask & (uint)InputBlockMaskFlags.BlockDiscrete) != 0u)
                state.ActionsBitmask = 0u;
        }

        private uint BuildPolledButtonMask()
        {
            uint mask = 0u;
            mask |= IsActionPressed(_pollJumpAction) ? (uint)PlayerInputAction.Jump : 0u;
            mask |= IsActionPressed(_pollInteractAction) ? (uint)PlayerInputAction.Interact : 0u;
            mask |= IsActionPressed(_pollPrimaryAction) ? (uint)PlayerInputAction.PrimaryFire : 0u;
            mask |= IsActionPressed(_pollSecondaryAction) ? (uint)PlayerInputAction.SecondaryFire : 0u;
            mask |= IsActionPressed(_pollSprintAction) ? (uint)PlayerInputAction.Sprint : 0u;
            mask |= IsActionPressed(_pollPdaAction) ? (uint)PlayerInputAction.Pda : 0u;
            mask |= IsActionPressed(_pollPauseAction) ? (uint)PlayerInputAction.Pause : 0u;
            mask |= IsActionPressed(_pollInventoryAction) ? (uint)PlayerInputAction.Inventory : 0u;
            mask |= IsActionPressed(_pollCancelAction) ? (uint)PlayerInputAction.Cancel : 0u;
            mask |= IsActionPressed(_pollTabNextAction) ? (uint)PlayerInputAction.TabNext : 0u;
            mask |= IsActionPressed(_pollTabPreviousAction) ? (uint)PlayerInputAction.TabPrevious : 0u;
            mask |= IsActionPressed(_pollToolSlot1Action) ? (uint)PlayerInputAction.ToolSlot1 : 0u;
            mask |= IsActionPressed(_pollToolSlot2Action) ? (uint)PlayerInputAction.ToolSlot2 : 0u;
            mask |= IsActionPressed(_pollToolSlot3Action) ? (uint)PlayerInputAction.ToolSlot3 : 0u;
            mask |= IsActionPressed(_pollToolSlot4Action) ? (uint)PlayerInputAction.ToolSlot4 : 0u;
            mask |= IsActionPressed(_pollFlashlightAction) ? (uint)PlayerInputAction.Flashlight : 0u;
            return mask;
        }

        private static Vector2 ReadActionVector2(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private static float ReadActionFloat(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<float>() : 0f;
        }

        private static bool IsActionPressed(InputAction action)
        {
            return action != null && action.enabled && action.IsPressed();
        }

        private static float2 ApplyAnalogDeadzone(float2 rawAxis, in InputProfileDTO profile)
        {
            if (!math.all(math.isfinite(rawAxis)))
                return float2.zero;

            float magnitudeSq = math.lengthsq(rawAxis);
            float inner = math.clamp(profile.InnerDeadzone, 0f, 0.95f);
            float outer = math.clamp(profile.OuterDeadzone, inner + 0.0001f, 1f);
            float innerSq = inner * inner;
            if (magnitudeSq <= innerSq)
                return float2.zero;

            float magnitude = math.sqrt(math.max(magnitudeSq, 0.00000001f));
            float normalized = math.saturate((magnitude - inner) / math.max(outer - inner, 0.0001f));
            float exponent = math.clamp(profile.MoveExponent, 0.25f, 4f);
            float curved = math.pow(normalized, exponent);
            float scale = curved / math.max(magnitude, 0.0001f);
            return rawAxis * scale;
        }

        private static float2 ResolveAupAgnosticLookDelta(float2 rawLookDelta, in InputProfileDTO profile)
        {
            if (!math.all(math.isfinite(rawLookDelta)))
                return float2.zero;

            float viewportHeight = math.max(1f, Screen.height);
            float magnitude = math.sqrt(math.max(math.lengthsq(rawLookDelta), 0f));
            float sensitivity = math.clamp(profile.MouseSensitivity, 0.01f, 20f);
            float acceleration = 1f + (math.min(magnitude, 64f) * math.clamp(profile.MouseAcceleration, 0f, 8f));
            return rawLookDelta * (sensitivity * acceleration / viewportHeight);
        }

        private uint ResolveCurrentInputSchemeHash()
        {
            if (HectonXRRuntimeState.IsXRActive &&
                ((_cachedLeftXRController != null && _cachedLeftXRController.added) ||
                 (_cachedRightXRController != null && _cachedRightXRController.added)))
            {
                return InputSchemeHashXRTouch;
            }

            InputManager inputManager = _nativeInputManager;
            if (inputManager != null)
            {
                switch (inputManager.CurrentDisplayStyle)
                {
                    case InputDisplayStyle.SteamDeck:
                        return InputSchemeHashSteamDeck;
                    case InputDisplayStyle.Gamepad:
                        return InputSchemeHashGamepad;
                    case InputDisplayStyle.XRTouch:
                        return InputSchemeHashXRTouch;
                    case InputDisplayStyle.KeyboardMouse:
                        return InputSchemeHashKeyboardMouse;
                }
            }

            if (_cachedGamepad != null && _cachedGamepad.added)
                return SteamDeckInputPal.IsDeckInputAvailable ? InputSchemeHashSteamDeck : InputSchemeHashGamepad;

            return InputSchemeHashKeyboardMouse;
        }

        private void PublishInputSchemeTelemetryIfChanged(uint schemeHash, uint flags, uint frame)
        {
            if (schemeHash == 0u || schemeHash == _lastTelemetryInputSchemeHash)
                return;

            _lastTelemetryInputSchemeHash = schemeHash;
            GlobalTelemetryBus.PublishInputSchemeHash(schemeHash, flags, frame);
        }

        private static bool ApplyAutomationOverride(ref PlayerInputState state, uint currentFrame)
        {
            if (!CoreDeterminismSignals.TryConsumeLatestInputOverride(currentFrame, 2u, out InputSignal overrideSignal))
                return false;

            state.MoveDelta = new Vector2(overrideSignal.MoveDelta.x, overrideSignal.MoveDelta.y);
            state.LookDelta = new Vector2(overrideSignal.LookDelta.x, overrideSignal.LookDelta.y);
            state.ScrollDelta = Vector2.zero;
            state.VerticalDelta = math.clamp(overrideSignal.VerticalDelta, -1f, 1f);
            state.SteamDeckGyroAimDelta = Vector2.zero;
            state.SteamDeckLeftTrackpad = Vector2.zero;
            state.SteamDeckRightTrackpad = Vector2.zero;
            state.ActionsBitmask = overrideSignal.ActionsBitmask;
            state.PlatformInputFlags = 0u;
            state.CurrentInputSchemeHash = overrideSignal.CurrentInputSchemeHash;
            return true;
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
            return _xrInputStatesHandle.BufferID != 0u ||
                   _xrLookAtRayCommandsHandle.BufferID != 0u ||
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
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool statesReady = TryResolveOrAcquireInputBuffer(
                ref _xrInputStatesHandle,
                BufferID.ShinobuInputXRInputStates,
                XRInputStateCapacity,
                NativeArrayOptions.UninitializedMemory,
                out _);
            bool commandsReady = TryResolveOrAcquireInputBuffer(
                ref _xrLookAtRayCommandsHandle,
                BufferID.ShinobuInputXRLookAtRayCommands,
                XRLookAtCommandCapacity,
                NativeArrayOptions.UninitializedMemory,
                out _);

            if (!statesReady || !commandsReady)
                return;

            if (_xrVaultBuffersCleared)
                return;

            ClearVaultBuffer(ref _xrInputStatesHandle);
            ClearVaultBuffer(ref _xrLookAtRayCommandsHandle);
            _xrVaultBuffersCleared = true;
            if (_xrLookAtRayCommandsHandle.BufferID != 0u)
                DisableXRLookAtRayCommand(forceWrite: true);
        }

        private bool TryResolveXRInputStates(out NativeArray<XRInputState> states)
        {
            states = default;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return TryResolveOrAcquireInputBuffer(
                ref _xrInputStatesHandle,
                BufferID.ShinobuInputXRInputStates,
                XRInputStateCapacity,
                NativeArrayOptions.UninitializedMemory,
                out states);
        }

        private bool TryResolveXRLookAtRayCommands(out NativeArray<RaycastCommand> commands)
        {
            commands = default;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return TryResolveOrAcquireInputBuffer(
                ref _xrLookAtRayCommandsHandle,
                BufferID.ShinobuInputXRLookAtRayCommands,
                XRLookAtCommandCapacity,
                NativeArrayOptions.UninitializedMemory,
                out commands);
        }

        private void DisposeXRNativeBuffers(JobHandle dependency)
        {
            ClearVaultBuffer(ref _xrInputStatesHandle);
            ClearVaultBuffer(ref _xrLookAtRayCommandsHandle);
            ReleaseVaultHandle(_dataVault, ref _xrInputStatesHandle);
            ReleaseVaultHandle(_dataVault, ref _xrLookAtRayCommandsHandle);
            _xrVaultBuffersCleared = false;
        }

        private void RefreshXRInputSnapshot()
        {
            if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates))
                return;

            if (!HectonXRRuntimeState.IsXRActive)
            {
                ClearXRInputSnapshotIfActive();
                return;
            }

            ResolveCachedXRControllers();
            xrInputStates[0] = CaptureXRController(
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
            xrInputStates[1] = CaptureXRController(
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

        private uint ResolveXRToolActionBitsAndPublishSignal(int frame)
        {
            if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates))
                return 0u;

            XRInputState left = xrInputStates[0];
            XRInputState right = xrInputStates[1];
            float leftTrigger = left.Trigger;
            float rightTrigger = right.Trigger;
            float leftGrip = left.Grip;
            float rightGrip = right.Grip;
            float primaryStrength = math.max(leftTrigger, rightTrigger);
            float secondaryStrength = math.max(leftGrip, rightGrip);
            byte flags = 0;
            flags |= primaryStrength >= XRToolTriggerPressThreshold ? ToolTriggerFlagPrimaryPressed : (byte)0;
            flags |= secondaryStrength >= XRToolTriggerPressThreshold ? ToolTriggerFlagSecondaryPressed : (byte)0;
            uint actionBits = 0u;
            actionBits |= (flags & ToolTriggerFlagPrimaryPressed) != 0 ? (uint)PlayerInputAction.PrimaryFire : 0u;
            actionBits |= (flags & ToolTriggerFlagSecondaryPressed) != 0 ? (uint)PlayerInputAction.SecondaryFire : 0u;
            actionBits |= (flags & ToolTriggerFlagSecondaryPressed) != 0 ? (uint)PlayerInputAction.Interact : 0u;

            uint controllerMask = left.ActiveMask | right.ActiveMask;
            float leftDominance = math.max(leftTrigger, leftGrip);
            float rightDominance = math.max(rightTrigger, rightGrip);
            byte dominantController = rightDominance > leftDominance ? (byte)1 : (byte)0;
            PublishXRToolTriggerIfChanged(primaryStrength, secondaryStrength, controllerMask, dominantController, flags, frame);
            return actionBits;
        }

        private void PublishXRToolTriggerIfChanged(
            float strength,
            float secondaryStrength,
            uint controllerMask,
            byte dominantController,
            byte flags,
            int frame)
        {
            if (math.abs(strength - _lastPublishedXRToolTriggerStrength) < XRToolTriggerPublishEpsilon &&
                math.abs(secondaryStrength - _lastPublishedXRSecondaryTriggerStrength) < XRToolTriggerPublishEpsilon &&
                controllerMask == _lastPublishedXRToolTriggerMask &&
                dominantController == _lastPublishedXRToolDominantController &&
                flags == _lastPublishedXRToolTriggerFlags)
            {
                return;
            }

            ToolTriggerSignal signal = default;
            signal.Strength = strength;
            signal.SecondaryStrength = secondaryStrength;
            signal.Frame = (uint)frame;
            signal.ControllerMask = controllerMask;
            signal.Sequence = ++_toolTriggerSequence;
            signal.DominantController = dominantController;
            signal.Flags = flags;
            GlobalSignals.Publish(in signal);

            _lastPublishedXRToolTriggerStrength = strength;
            _lastPublishedXRSecondaryTriggerStrength = secondaryStrength;
            _lastPublishedXRToolTriggerMask = controllerMask;
            _lastPublishedXRToolDominantController = dominantController;
            _lastPublishedXRToolTriggerFlags = flags;
        }

        private void PublishXRToolTriggerReleaseIfNeeded(int frame)
        {
            if (_lastPublishedXRToolTriggerStrength <= 0f &&
                _lastPublishedXRSecondaryTriggerStrength <= 0f &&
                _lastPublishedXRToolTriggerMask == 0u &&
                _lastPublishedXRToolDominantController == 0 &&
                _lastPublishedXRToolTriggerFlags == 0)
            {
                return;
            }

            PublishXRToolTriggerIfChanged(0f, 0f, 0u, 0, 0, frame);
        }

        private void ClearXRInputSnapshotIfActive(bool forceWrite = false)
        {
            if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates))
                return;

            if (!forceWrite && (_xrRuntimeFlags & XRRuntimeFlagInputSnapshotActive) == 0u)
                return;

            for (int i = 0; i < xrInputStates.Length; i++)
                xrInputStates[i] = default;

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
            bool positionValid = IsFinite(position);
            bool rotationValid = IsFinite(rotation);
            if (!positionValid)
                position = Vector3.zero;
            if (!rotationValid)
                rotation = Quaternion.identity;
            state.GripPositionWS.x = position.x;
            state.GripPositionWS.y = position.y;
            state.GripPositionWS.z = position.z;
            state.GripRotationWS = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            bool tracked = controller.isTracked != null && controller.isTracked.isPressed && positionValid && rotationValid;
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
            if (!math.isfinite(value))
                return 0f;

            float normalized = math.saturate(value);
            return normalized < XRAnalogNoiseFloor ? 0f : normalized;
        }

        private static float2 ApplyXRJoystickNoiseFloor(Vector2 value)
        {
            float2 joystick = new float2(
                math.isfinite(value.x) ? math.clamp(value.x, -1f, 1f) : 0f,
                math.isfinite(value.y) ? math.clamp(value.y, -1f, 1f) : 0f);
            return math.lengthsq(joystick) < XRAnalogNoiseFloorSq ? float2.zero : joystick;
        }

        private static bool IsPressed(ButtonControl button, float analogValue)
        {
            return (button != null && button.isPressed) || analogValue >= 0.5f;
        }

        private void StageXRLookAtRayCommand()
        {
            if (!TryResolveXRLookAtRayCommands(out NativeArray<RaycastCommand> commands))
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
            float3 direction3 = default;
            direction3.x = direction.x;
            direction3.y = direction.y;
            direction3.z = direction.z;
            if (!math.all(math.isfinite(direction3)))
            {
                direction = Vector3.forward;
                direction3.x = 0f;
                direction3.y = 0f;
                direction3.z = 1f;
            }

            if (!HectonXRRuntimeState.TryResolveCachedHeadAup48(origin, out XRRuntimeAup48 originAup) &&
                !XRRuntimeAup48.TryFromRuntimePosition(origin, out originAup))
            {
                DisableXRLookAtRayCommand(forceWrite: true);
                return;
            }

            if (TryReuseXRLookAtHit(in originAup, in direction3))
            {
                DisableXRLookAtRayCommand();
                return;
            }

            if (!originAup.TryToRuntimeFloat3(out float3 rayOrigin3))
            {
                DisableXRLookAtRayCommand(forceWrite: true);
                return;
            }

            Vector3 rayOrigin = new Vector3(rayOrigin3.x, rayOrigin3.y, rayOrigin3.z);
            RaycastCommand command = default;
            command.from = rayOrigin;
            command.direction = direction;
            command.distance = XRLookAtSelectionDistanceMeters;
            command.queryParameters = XRLookAtEnabledQueryParameters;
            if (SystemDispatcher.QueueDispatcherRaycast(this, XRLookAtSelectionRequestId, in command))
            {
                commands[0] = command;
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

            if (TryResolveXRLookAtRayCommands(out NativeArray<RaycastCommand> commands))
                commands[0] = DisabledXRLookAtRayCommand;
            _xrRuntimeFlags &= ~XRRuntimeFlagLookAtRayCommandEnabled;
        }

        private void ClearXRRuntimeFrameStateIfActive()
        {
            if (_xrInputStatesHandle.BufferID == 0u &&
                _xrLookAtRayCommandsHandle.BufferID == 0u &&
                (_xrRuntimeFlags & XRRuntimeFlagsAny) == 0u &&
                _lastXRLookAtPhysicsQueryFrame < 0 &&
                _lastXRLookAtHitFrame < 0)
            {
                return;
            }

            ClearXRInputSnapshotIfActive(forceWrite: true);

            if (_xrLookAtRayCommandsHandle.BufferID != 0u)
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

        private bool TryReuseXRLookAtHit(in XRRuntimeAup48 originAup, in float3 direction)
        {
            if (_lastXRLookAtPhysicsQueryFrame < 0)
                return false;

            if (Time.frameCount - _lastXRLookAtPhysicsQueryFrame > XRLookAtReuseMaxFrames)
                return false;

            if (!XRRuntimeAup48.TryToRelativeFloat3(in originAup, in _lastXRLookAtRayOriginAup, out float3 originDelta))
                return false;

            if (math.lengthsq(originDelta) > XRLookAtReuseOriginDriftSq)
                return false;

            float3 previousDirection = default;
            previousDirection.x = _lastXRLookAtRayDirection.x;
            previousDirection.y = _lastXRLookAtRayDirection.y;
            previousDirection.z = _lastXRLookAtRayDirection.z;
            if (math.dot(previousDirection, direction) < XRLookAtReuseForwardDot)
                return false;

            if (_lastXRLookAtHit.collider == null)
            {
                _lastXRLookAtHitFrame = Time.frameCount;
                return true;
            }

            if (!XRRuntimeAup48.TryToRelativeFloat3(in _lastXRLookAtHitPointAup, in originAup, out float3 toHit))
                return false;

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

        private Transform ResolveLookAtViewTransform()
        {
            IPlayerRuntimeContext playerContext = _playerContext;
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

        private static bool IsFinite(Quaternion value)
        {
            float lengthSq =
                (value.x * value.x) +
                (value.y * value.y) +
                (value.z * value.z) +
                (value.w * value.w);
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                   !float.IsNaN(value.w) && !float.IsInfinity(value.w) &&
                   !float.IsNaN(lengthSq) && !float.IsInfinity(lengthSq) &&
                   lengthSq > 0.000001f;
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
            PublishPlayerInputCommand(PlayerInputSignalCommands.Interact);
            OnInteract?.Invoke();
        }

        private void HandleToolSlot1Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot1);
            OnToolSlot1?.Invoke();
        }

        private void HandleToolSlot2Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot2);
            OnToolSlot2?.Invoke();
        }

        private void HandleToolSlot3Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot3);
            OnToolSlot3?.Invoke();
        }

        private void HandleToolSlot4Pressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot4);
            OnToolSlot4?.Invoke();
        }

        private void HandlePrimaryActionPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.PrimaryFire;
            PublishPlayerInputCommand(PlayerInputSignalCommands.PrimaryAction);
            OnPrimaryAction?.Invoke();
        }

        private void HandleSecondaryActionPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.SecondaryFire;
            PublishPlayerInputCommand(PlayerInputSignalCommands.SecondaryAction);
            OnSecondaryAction?.Invoke();
        }

        private void HandlePDAPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.TogglePda);
            OnPDA?.Invoke();
        }

        private void HandleInventoryPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.ToggleInventory);
            OnInventory?.Invoke();
        }

        private void HandleCancelPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.Cancel);
            OnCancel?.Invoke();
        }

        private void HandleTabNextPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.TabNext);
            OnTabNext?.Invoke();
        }

        private void HandleTabPreviousPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.TabPrevious);
            OnTabPrevious?.Invoke();
        }

        private void HandleFlashlightPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            PublishPlayerInputCommand(PlayerInputSignalCommands.Flashlight);
        }

        private void PublishPlayerInputCommand(byte command)
        {
            PlayerInputSignal signal = default;
            signal.SourceHash = PlayerInputSignalSourceHash;
            signal.Frame = unchecked((uint)Mathf.Max(0, Time.frameCount));
            signal.Sequence = unchecked(++_playerInputSignalSequence);
            signal.Command = command;
            signal.Flags = 0;
            SignalBus<PlayerInputSignal>.Push(in signal);
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
            if (hit.collider != null &&
                XRRuntimeAup48.TryOffsetLocal(in _lastXRLookAtRayOriginAup, hit.point - _lastXRLookAtRayOriginRuntimePosition, out XRRuntimeAup48 hitPointAup))
            {
                _lastXRLookAtHitPointAup = hitPointAup;
            }
            else
            {
                _lastXRLookAtHitPointAup = default;
            }
        }

        private void DrainToolHaptics(float deltaTime)
        {
            EnsureDeterministicInputNativeBuffers();
            uint schemeHash = _currentInputSchemeHash != 0u ? _currentInputSchemeHash : ResolveCurrentInputSchemeHash();
            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.1f)
                : (float)StandardInputTickIntervalSeconds;
            InputProfileDTO profile = ReadInputProfile();
            bool throttleHaptics = ShouldThrottleHapticDispatch(schemeHash);
            if (throttleHaptics)
                _hapticDispatchAccumulator += safeDeltaTime;
            else
                _hapticDispatchAccumulator = 0f;

            float lowMotor = 0f;
            float highMotor = 0f;
            byte lowPriority = 0;
            byte highPriority = 0;
            bool hasLowPriority = false;
            bool hasHighPriority = false;

            while (GlobalSignals.TryDequeueHapticRequest(out HapticRequest request))
            {
                if (schemeHash == InputSchemeHashKeyboardMouse)
                    continue;

                InsertHapticRequestCommand(in request);
            }

            if (schemeHash == InputSchemeHashKeyboardMouse)
            {
                _lastHapticCommandsActive = 0;
                ApplyGamepadHaptics(0f, 0f);
                ResetXRHaptics();
                return;
            }

            int activeHapticCommands = EvaluateHapticCommandDtos(
                safeDeltaTime,
                ref lowMotor,
                ref highMotor,
                ref lowPriority,
                ref highPriority,
                ref hasLowPriority,
                ref hasHighPriority);

            if (ToolHapticsRuntime.TryGetRuntime(out ToolHapticsRuntime runtime) &&
                runtime.TryGetFrontBufferSnapshot(out ReadOnlySpan<ToolHapticsRuntime.HapticCommand> commandBuffer, out int commandCount))
            {
                for (int i = 0; i < commandCount; i++)
                {
                    ToolHapticsRuntime.HapticCommand command = commandBuffer[i];
                    if (command.DurationRemaining <= 0f)
                        continue;

                    activeHapticCommands++;
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
            }

            _lastHapticCommandsActive = (ushort)math.min(activeHapticCommands, ushort.MaxValue);
            float hapticDispatchInterval = profile.HapticDispatchIntervalSeconds > 0f
                ? math.clamp(profile.HapticDispatchIntervalSeconds, 1f / 120f, 0.25f)
                : ThermalHapticDispatchIntervalSeconds;
            if (throttleHaptics && _hapticDispatchAccumulator < hapticDispatchInterval)
                return;

            if (throttleHaptics)
                _hapticDispatchAccumulator = 0f;

            float amplitudeScale = math.saturate(profile.HapticPowerScale);
            if (throttleHaptics)
                amplitudeScale *= math.saturate(profile.HapticThermalAmplitudeScale);
            lowMotor = ClampFinite01(lowMotor * amplitudeScale);
            highMotor = ClampFinite01(highMotor * amplitudeScale);

            if (schemeHash == InputSchemeHashXRTouch)
            {
                ApplyGamepadHaptics(0f, 0f);
                ApplyXRHaptics(lowMotor, highMotor);
                return;
            }

            ResetXRHaptics();
            ApplyGamepadHaptics(lowMotor, highMotor);
        }

        private static void ApplyHapticRequestContribution(
            in HapticRequest request,
            ref float lowMotor,
            ref float highMotor,
            ref byte lowPriority,
            ref byte highPriority,
            ref bool hasLowPriority,
            ref bool hasHighPriority)
        {
            float intensity = ClampFinite01(request.Intensity01);
            if (intensity <= 0f || request.DurationSeconds <= 0f)
                return;

            byte priority = request.Channel;
            byte blendMode = (request.Flags & HapticBlendAdditive) != 0 ? HapticBlendAdditive : (byte)2;
            float highContribution = intensity * math.max(0.25f, ClampFinite01(request.Frequency01));

            ApplyHapticContribution(
                intensity,
                priority,
                blendMode,
                ref lowMotor,
                ref lowPriority,
                ref hasLowPriority);
            ApplyHapticContribution(
                highContribution,
                priority,
                blendMode,
                ref highMotor,
                ref highPriority,
                ref hasHighPriority);
        }

        private void InsertHapticRequestCommand(in HapticRequest request)
        {
            float intensity = ClampFinite01(request.Intensity01);
            if (intensity <= 0f || request.DurationSeconds <= 0f)
                return;

            float highContribution = intensity * math.max(0.25f, ClampFinite01(request.Frequency01));
            float decayRate = 1f / math.max(request.DurationSeconds, 0.02f);
            InsertHapticCommandDto(intensity, highContribution, decayRate, HapticLowMotorMask | HapticHighMotorMask);
        }

        private void InsertHapticCommandDto(float lowFreqIntensity, float highFreqIntensity, float decayRate, uint motorMask)
        {
            if (!TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out NativeArray<HapticCommandDTO> commands))
                return;

            HapticCommandDTO command = default;
            command.LowFreqIntensity = ClampFinite01(lowFreqIntensity);
            command.HighFreqIntensity = ClampFinite01(highFreqIntensity);
            command.DecayRate = math.clamp(math.isfinite(decayRate) ? decayRate : 1f, 0.01f, 64f);
            command.MotorMask = motorMask;

            int weakestIndex = 0;
            float weakestMagnitude = float.MaxValue;
            for (int i = 0; i < commands.Length; i++)
            {
                HapticCommandDTO existing = commands[i];
                float magnitude = math.max(existing.LowFreqIntensity, existing.HighFreqIntensity);
                if (magnitude <= HapticMotorWriteEpsilon)
                {
                    commands[i] = command;
                    return;
                }

                if (magnitude >= weakestMagnitude)
                    continue;

                weakestMagnitude = magnitude;
                weakestIndex = i;
            }

            commands[weakestIndex] = command;
        }

        private void RunMockCollisionHapticJob(uint frame)
        {
            InputProfileDTO profile = ReadInputProfile();
            if ((profile.Flags & InputProfileFlagEnableMockCollision) == 0u)
                return;

            uint hash = unchecked((frame * 1664525u) + 1013904223u);
            if ((hash & 31u) != 0u)
                return;

            MockCollisionSignal signal = default;
            signal.Magnitude01 = 0.85f + ((hash & 7u) * 0.015f);
            signal.Frame = frame;
            signal.SourceHash = InputMockSignalSourceHash;
            signal.Flags = 0u;
            HandleMockCollisionSignal(in signal);
        }

        private void HandleMockCollisionSignal(in MockCollisionSignal signal)
        {
            float magnitude = ClampFinite01(signal.Magnitude01);
            if (magnitude <= 0f)
                return;

            InsertHapticCommandDto(
                magnitude,
                magnitude * 0.45f,
                7.5f,
                HapticLowMotorMask | HapticHighMotorMask);
        }

        private int EvaluateHapticCommandDtos(
            float deltaTime,
            ref float lowMotor,
            ref float highMotor,
            ref byte lowPriority,
            ref byte highPriority,
            ref bool hasLowPriority,
            ref bool hasHighPriority)
        {
            if (!TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out NativeArray<HapticCommandDTO> commands))
                return 0;

            float safeDeltaTime = math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.1f);
            int activeCount = 0;
            for (int i = 0; i < commands.Length; i++)
            {
                HapticCommandDTO command = commands[i];
                float low = (command.MotorMask & HapticLowMotorMask) != 0u ? ClampFinite01(command.LowFreqIntensity) : 0f;
                float high = (command.MotorMask & HapticHighMotorMask) != 0u ? ClampFinite01(command.HighFreqIntensity) : 0f;
                if (low <= HapticMotorWriteEpsilon && high <= HapticMotorWriteEpsilon)
                {
                    commands[i] = default;
                    continue;
                }

                ApplyHapticContribution(low, 1, HapticBlendAdditive, ref lowMotor, ref lowPriority, ref hasLowPriority);
                ApplyHapticContribution(high, 1, HapticBlendAdditive, ref highMotor, ref highPriority, ref hasHighPriority);

                float decayFactor = ResolveHapticDecayFactor(command.DecayRate, safeDeltaTime);
                command.LowFreqIntensity = ClampFinite01(low * decayFactor);
                command.HighFreqIntensity = ClampFinite01(high * decayFactor);
                if (command.LowFreqIntensity <= HapticMotorWriteEpsilon && command.HighFreqIntensity <= HapticMotorWriteEpsilon)
                    command = default;
                else
                    activeCount++;

                commands[i] = command;
            }

            return activeCount;
        }

        private static float ResolveHapticDecayFactor(float decayRate, float deltaTime)
        {
            float x = math.min(math.max(0f, decayRate) * math.max(0f, deltaTime), 3f);
            float x2 = x * x;
            return 1f / math.max(1f + x + (0.48f * x2) + (0.235f * x2 * x), 0.0001f);
        }

        private static bool ShouldThrottleHapticDispatch(uint schemeHash)
        {
            if (schemeHash != InputSchemeHashSteamDeck && !SteamDeckInputPal.IsDeckInputAvailable)
                return false;

            ReadOnlySpan<SystemHealthIndexSignal> snapshot = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                SystemHealthIndexSignal signal = snapshot[i];
                if (signal.State >= SystemHealthIndexSignal.StateCritical)
                    return true;
                if (math.isfinite(signal.Pressure01) && signal.Pressure01 >= 0.9f)
                    return true;
            }

            return false;
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
            _visualLookDelta = Vector2.zero;
            _currentState = default;
            _currentInputState = default;
            _previousInputState = default;
            _standardInputFrame = 0u;
            _inputStateSequence = 0u;
            _playerInputSignalSequence = 0u;
            _inputDelayFrames = Mathf.Clamp(_inputDelayFrames, 0, MaxInputDelayFrames);
            _lastDeterministicInputFrame = uint.MaxValue;
            _standardInputAccumulator = 0d;
            _deterministicInputCount = 0;
            _deterministicBlackBoxWriteIndex = 0;
            _buttonMaskWindowWriteIndex = 0;
            _nextInputReplayRetryFrame = 0;
            _lastAutomationOverrideApplied = false;
            _previousButtonMask = 0u;
            _lastPollingTimeMicroseconds = 0u;
            _bufferedInputsConsumedThisFrame = 0u;
            _lastHapticCommandsActive = 0;
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

            if (_deterministicVaultBuffersReady)
            {
                ClearVaultBuffer(ref _currentInputDtoHandle);
                ClearVaultBuffer(ref _inputJournalHandle);
                ClearVaultBuffer(ref _inputStateBridgeRingHandle);
                ClearVaultBuffer(ref _buttonMaskWindowHandle);
                ClearVaultBuffer(ref _inputBlockMaskHandle);
                ClearVaultBuffer(ref _inputTelemetryHandle);
                ClearVaultBuffer(ref _inputReplaySnapshotHandle);
                ClearVaultBuffer(ref _hapticCommandDtoHandle);
                ClearVaultBuffer(ref _inputProfileCsvScratchHandle);
            }

            ClearXRInputSnapshotIfActive(forceWrite: true);

            if (_xrLookAtRayCommandsHandle.BufferID != 0u)
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
            double timestamp = UnityEngine.InputSystem.LowLevel.InputState.currentTime;
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

        public static float SampleInputSystemClockDeltaMs()
        {
            double inputTimestamp = UnityEngine.InputSystem.LowLevel.InputState.currentTime;
            double renderTimestamp = Time.unscaledTimeAsDouble;
            if (inputTimestamp <= 0d || renderTimestamp <= 0d)
                return 0f;

            return (float)(math.abs(inputTimestamp - renderTimestamp) * 1000d);
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
