#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_MMF_AVAILABLE
#endif
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using System;
using System.IO;
#if HECTON8_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.World;
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
    public sealed unsafe partial class InputDispatcher : MonoBehaviour, IInputService, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001InputDispatcherSignalPushDropCount;
        private const int BufferedActionCapacity = 10;
        private const int DeterministicInputRingCapacity = 512;
        private const int ButtonMaskWindowCapacity = 10;
        private const int HapticCommandDtoCapacity = 16;
        private const int XRInputStateCapacity = 2;
        private const int XRControllerActiveBitCount = 5;
        private const int XRDeviceRescanIntervalFrames = 30;
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
        private const byte HapticBlendMax = 2;
        private const byte HapticPriorityMicro = 0;
        private const byte HapticPriorityTool = 1;
        private const byte HapticPriorityCollision = 2;
        private const byte HapticPriorityCritical = 3;
        private const uint HapticCommandMotorMaskBits = 0xFFu;
        private const int HapticCommandPriorityShift = 8;
        private const int HapticCommandBlendShift = 12;
        private const uint HapticCommandNibbleMask = 0xFu;
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
        private const uint XRRuntimeFlagLookAtProbeActive = 1u << 0;
        private const uint XRRuntimeFlagInputSnapshotActive = 1u << 1;
        private const uint XRRuntimeFlagsAny = XRRuntimeFlagLookAtProbeActive | XRRuntimeFlagInputSnapshotActive;
        private const int StandardInputRingCapacity = DeterministicInputRingCapacity;
        private const int InputBlackBoxCapacity = 300;
        private const int BufferedActionEntrySizeBytes = 16;
        private const int InputStateSizeBytes = 24;
        private const int PlayerInputStateSizeBytes = 64;
        private const int InputStateDtoSizeBytes = 24;
        private const int HapticCommandDtoSizeBytes = 16;
        private const int XRInputStateSizeBytes = 64;
        private const int ReplayFrameDtoSizeBytes = 80;
        private const int ReplayTelemetryEntrySizeBytes = 64;
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
        private static readonly ulong InputOwnerMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuInputCurrentDto) |
            MutationGuardBit(BufferID.ShinobuInputJournalRing) |
            MutationGuardBit(BufferID.ShinobuPredictedInputRing) |
            MutationGuardBit(BufferID.ShinobuPredictedInputAupTargets) |
            MutationGuardBit(BufferID.ShinobuInputStateBridgeRing) |
            MutationGuardBit(BufferID.ShinobuInputButtonMaskWindow) |
            MutationGuardBit(BufferID.ShinobuInputBlockMask) |
            MutationGuardBit(BufferID.ShinobuInputProfile) |
            MutationGuardBit(BufferID.ShinobuInputTelemetryRing) |
            MutationGuardBit(BufferID.ShinobuInputReplaySnapshot) |
            MutationGuardBit(BufferID.ShinobuInputReplayFrames) |
            MutationGuardBit(BufferID.ShinobuInputReplayTelemetry) |
            MutationGuardBit(BufferID.ShinobuInputHapticCommands) |
            MutationGuardBit(BufferID.ShinobuInputXRInputStates)
#if UNITY_EDITOR
            | MutationGuardBit(BufferID.ShinobuInputCsvScratch)
#endif
            ;

        // Just ShinobuInputProfile (70524 -> guard bit 60 -> folded active-lock residue 28), for the one
        // write that must land whether or not the broad 14-buffer owner guard above can be taken. The broad
        // mask claims 13 of the vault's 32 folded active-lock residues, so ANY unrelated buffer sharing one of
        // them refuses it; this mask claims one. Seeding the profile through the broad mask is what made the
        // defaults hostage to a lock that has nothing to do with the profile.
        private static readonly ulong InputProfileOnlyMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuInputProfile);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The vault does NOT compare _activeLocks against the 64-bit guard mask. It folds the mask onto its
        // 32-bit active-lock space with low|high (GlobalDataVault.cs:2930-2932) and refuses the guard when
        // ANY of those folded bits is set in _activeLocks (HasActiveLockConflictForMutationMask, :3253-3257).
        // Active-lock bits are allocated as 1 << (bufferId & 31) (ResolveActiveLockBit, :3008-3012), so this
        // owner's 14 buffer IDs claim 13 of the 32 residues - and every OTHER buffer in the project sharing
        // one of those residues can refuse this owner's guard while having nothing to do with input.
        // Derived from the same constant so the two can never drift. Diagnostic-only.
        private static readonly uint InputOwnerActiveLockConflictMask =
            unchecked((uint)InputOwnerMutationGuardMask) |
            unchecked((uint)(InputOwnerMutationGuardMask >> 32));

        // Low and high halves of the owner guard mask, derived from the same constant so they cannot drift.
        //
        // WHY THE SPLIT IS THE DISCRIMINATOR, and it settles a question the census cannot.
        // GlobalDataVault keeps ONE process-wide guard mask pair, _mutationGuardMaskLow /
        // _mutationGuardMaskHigh (GlobalDataVault.cs:2944-2946), and ReleaseMutationGuard (:3010-3019) just
        // clears whatever bits it is handed. There is NO owner identity anywhere in that API. So "bits from
        // this owner's mask are set" does NOT mean this owner set them, and the code-4 text below - "an owner
        // holds bits from THIS mask and did not release them" - cannot separate a leak by THIS component from
        // an unrelated system parked on a colliding residue. Bit arithmetic can.
        //
        // GlobalDataVault.cs:3029-3035 records that this tree uses TWO mask conventions: 1UL << (id & 63),
        // which is MutationGuardBit above, and 1UL << (id & 31), which it calls the 208-call-site majority.
        // An (id & 31) mask can only ever set bits 0-31, i.e. the LOW word - it is arithmetically incapable of
        // touching the high word. So a HIGH-word overlap can only come from an (id & 63) caller, and the tree
        // has exactly three: this file, MemorySentinelRuntime.cs:1153, and the Editor-only
        // ShinobuStormPropagationDebugGizmo.cs:79.
        //
        // This owner's 14 buffer ids fold to guard bits {0,1,2,3,5} low and {56..63} high:
        //   70520..70527 -> 56..63, 70530 -> 2, 70531 -> 3, 70533 -> 5 (editor only),
        //   75000 -> 56, 75001 -> 57, 75008 -> 0, 75009 -> 1.
        // Low half = 0x0000002F, high half = 0xFF000000, and RecordMutationGuardContentionFault's
        // XOR fold (GlobalDataVault.cs:5612 - it is ^, not |, though the two agree here because the halves
        // are disjoint) gives 0xFF00002F & 0x7fffffff = 0x7F00002F = 2130706479 - exactly the
        // vaultLastFaultBufferId printed at Logs/h8_probe7.log:21930.
        // Consequence: the five LOW bits are shared with every BufferID congruent to 0,1,2,3,5 mod 32,
        // roughly one id in six across the whole project, while the eight HIGH bits are all but private to
        // this owner. A low-only overlap is therefore evidence AGAINST a leak here; a high overlap is
        // evidence FOR one. Diagnostic-only.
        private static readonly uint InputOwnerGuardMaskLow = unchecked((uint)InputOwnerMutationGuardMask);
        private static readonly uint InputOwnerGuardMaskHigh = unchecked((uint)(InputOwnerMutationGuardMask >> 32));
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)(uint)(int)bufferId) & 63;
            return 1UL << bitIndex;
        }
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

        private INativeInputManagerRuntime _nativeInputManager;
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
        private VaultGenerationHandle<PredictedInputDTO> _predictedInputHandle;
        private VaultGenerationHandle<PredictedInputAupTargetDTO> _predictedInputAupTargetHandle;
        private VaultGenerationHandle<InputState> _inputStateBridgeRingHandle;
        private VaultGenerationHandle<uint> _buttonMaskWindowHandle;
        private VaultGenerationHandle<uint> _inputBlockMaskHandle;
        private VaultGenerationHandle<InputProfileDTO> _inputProfileHandle;
        private VaultGenerationHandle<InputTelemetryEntryDTO> _inputTelemetryHandle;
        private VaultGenerationHandle<InputState> _inputReplaySnapshotHandle;
        private VaultGenerationHandle<ReplayFrameDTO> _inputReplayFrameHandle;
        private VaultGenerationHandle<MemoryStateTelemetryEntry> _inputReplayTelemetryHandle;
        private VaultGenerationHandle<HapticCommandDTO> _hapticCommandDtoHandle;
        private VaultGenerationHandle<XRInputState> _xrInputStatesHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _inputProfileCsvScratchHandle;
#endif
        private FileStream _inputReplayStream;
#if UNITY_EDITOR
        private FileSystemWatcher _inputProfileCsvWatcher;
#endif
#if HECTON8_MMF_AVAILABLE
        private MemoryMappedFile _inputReplayMappedFile;
        private MemoryMappedViewAccessor _inputReplayAccessor;
        private byte* _inputReplayPointer;
#endif
        private Thread _inputReplayThread;
        private AutoResetEvent _inputReplaySignal;
        private InteractableRegistry.SpatialHit _lastXRLookAtSpatialHit;
        private XRRuntimeAup48 _lastXRLookAtRayOriginAup;
        private Vector3 _lastXRLookAtRayOriginRuntimePosition;
        private Vector3 _lastXRLookAtRayDirection;
        private XRRuntimeAup48 _lastXRLookAtHitPointAup;
        private int _lastXRLookAtProbeFrame = -1;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredInputService;
        private bool _registeredHotSwapListener;
        private bool _isInitialized;
        private bool _subscribedToNativeInput;
        private bool _subscribedToDeviceChanges;
        private bool _subscribedToXRActiveChanged;
        private int _lastCapturedFrame = -1;
        // Dispatcher frame index of the last PreSimulationInputTick, from either caller. -1 means the
        // per-frame input tick has never run in this session, which is the state a NoOp-latched
        // SystemDispatcher leaves it in - see PumpPreSimulationInputIfDispatcherSkipped.
        private int _lastPreSimulationInputFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Per-hop counters for the automation-override lane. Editor/dev only; every write goes through a
        // Diag* method whose body is compiled out of player builds, so the shipping hot path is unchanged.
        // Read the census emitted by DiagEmitHopCensus - the counters are declared and printed in PIPELINE
        // ORDER, so the FIRST zero from the left is the hop that dropped the value. See
        // DiagRecordReadObservation for why the census is emitted from a read accessor and not from a tick.
        private const int DiagReportAtObservation1 = 240;
        private const int DiagReportAtObservation2 = 1200;
        private const int DiagReportAtObservation3 = 3600;
        private int _diagLateFrameTickCalls;
        private int _diagPumpFiredCalls;
        private int _diagPreSimTickCalls;
        private int _diagPreSimSubsteps;
        private int _diagCaptureRan;
        private int _diagCaptureSkippedByFrameGuard;
        private int _diagOverrideApplied;
        private int _diagOverrideRejected;
        private int _diagBlockMaskNonZero;
        private int _diagPublishAttempts;
        private int _diagPublishGuardFail;
        private int _diagPublishBufferFail;
        private int _diagPublishOk;
        private int _diagReadObservations;
        private int _diagReportsEmitted;
        private bool _diagFinalCensusEmitted;
        // Last refusal identity reported by DiagReportPublishRefusal. 0 means nothing reported yet, so the
        // FIRST refusal of a session always prints. Guard refusals use the small codes below; buffer
        // refusals use PublishRefusalBufferFlag plus one nibble per buffer, so a change in ANY of the four
        // reprints exactly once. Transition-gated, not per frame: probe7 shows 1240 refusals in 313 frames.
        private int _diagPublishRefusalReported;
        // Separate latch from _diagPublishRefusalReported on purpose: this one fires at the ACQUIRE, before
        // the vault is asked, so it must not be silenced by - or silence - the post-refusal reporter.
        // 0 = nothing reported yet, so the first residue of a session always prints.
        private int _diagGuardResidueReported;
        // Guard refusal codes, in the order the checks are evaluated - two here, then
        // GlobalDataVault.TryAcquireMutationGuard (GlobalDataVault.cs:2912-2994).
        private const int PublishRefusalGuardVaultNull = 1;
        private const int PublishRefusalGuardOwnerThread = 2;
        private const int PublishRefusalGuardCompactionFence = 3;
        private const int PublishRefusalGuardMaskHeld = 4;
        private const int PublishRefusalGuardActiveLockConflict = 5;
        private const int PublishRefusalGuardOpaque = 6;
        private const int PublishRefusalBufferFlag = 1 << 16;
        // Buffer states from DiagClassifyInputBuffer. 1 and 2 are DIFFERENT failures: 1 means this owner
        // never got a handle at all, 2 means it holds one the vault will not honour.
        private const int DiagBufferOk = 0;
        private const int DiagBufferNoHandle = 1;
        private const int DiagBufferVaultRefused = 2;
        private const int DiagBufferNotCreated = 3;
        private const int DiagBufferTooShort = 4;
        private float _diagLastOverrideMoveX;
        private float _diagLastOverrideMoveY;
        private float _diagLastPostMaskMoveX;
        private float _diagLastPostMaskMoveY;
#endif
        private int _nextXRDeviceRescanFrame;
        private int _lastXRLookAtHitFrame = -1;
        private int _bufferWriteIndex;
        private int _buttonMaskWindowWriteIndex;
        private int _deterministicInputCount;
        private int _deterministicBlackBoxWriteIndex;
        private int _inputReplayStopRequested;
        private int _inputReplayWritePending;
        private int _nextInputReplayRetryFrame;
#if UNITY_EDITOR
        private int _inputProfileCsvDirty;
        private int _inputProfileCsvStageVersion;
        private int _inputProfileCsvAppliedVersion;
        private int _inputProfileCsvStageFault;
        private int _nextInputProfileCsvRetryFrame;
#endif
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
        private float _viewportHeightSnapshot = 1f;
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
        private bool _pendingHapticOutput;
        private uint _pendingHapticSchemeHash;
        private float _pendingHapticLowMotor;
        private float _pendingHapticHighMotor;
        private bool _lookBlendActive;
        private bool _lastAutomationOverrideApplied;
        private bool _pollActionsCached;
        private bool _deterministicVaultBuffersReady;
        private bool _deterministicVaultBuffersCleared;
        private bool _xrVaultBuffersCleared;
        private int _ownerThreadId;
        private int _inputMutationGuardDepth;
        // Latched true only once InitializeDefaultInputProfile has actually written the Default* constants into
        // the vault copy. Separate from _deterministicVaultBuffersCleared because the profile seed no longer
        // rides on the broad owner guard that flag gates.
        private bool _inputProfileDefaultsSeeded;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _inputProfileSeedFailureReported;
#endif
        private IDataVault _inputMutationGuardVault;
        private Vector2 _lookBlendFrom;
        private Vector2 _lastDeliveredLookDelta;
        private PlayerInputState _currentState;
        private InputState _currentInputState;
        private InputState _previousInputState;
#if UNITY_EDITOR
        private InputProfileDTO _stagedInputProfileCsv;
#endif
        private uint _standardInputFrame;
        private uint _inputStateSequence;
        private uint _playerInputSignalSequence;
        private uint _lastDeterministicInputFrame = uint.MaxValue;
        [SerializeField, Range(0, MaxInputDelayFrames)]
        private int _inputDelayFrames;
        private double _standardInputAccumulator;
#if UNITY_EDITOR
        private string _inputProfileCsvPath;
#endif

        internal static InputDispatcher ActiveRuntimeInstance;

        internal static bool TryResolveActiveRuntime(ref InputDispatcher target)
        {
            InputDispatcher active = ActiveRuntimeInstance;
            if (active == null || !active.isActiveAndEnabled)
            {
                target = null;
                return false;
            }

            if (!ReferenceEquals(target, active))
                target = active;

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        // COLD ALLOC: BufferedActionEntry[10] - fixed player action buffering ring for pre-commit intent capture - owner: InputDispatcher
        private readonly BufferedActionEntry[] _bufferedActions = new BufferedActionEntry[BufferedActionCapacity];
        // Zero-alloc CAS gate for deterministic input replay MMF copy/flush handoff - owner: InputDispatcher
        private int _inputReplaySnapshotGate;
#if UNITY_EDITOR
        // COLD ALLOC: object[1] - CSV profile stage gate; file I/O happens outside PRE_SIMULATION - owner: InputDispatcher
        private readonly object _inputProfileCsvStageGate = new object();
#endif
        /// <summary>
        /// Returns true once the dispatcher is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        public int InputDelayFrames
        {
            get => _inputDelayFrames;
            set => _inputDelayFrames = Mathf.Clamp(value, 0, MaxInputDelayFrames);
        }

        public InputState CurrentInputState
        {
            get
            {
                // The census rides this accessor because probe5 proves it is reached in the failing
                // configuration while every tick lane this component owns may not be. See the block above
                // DiagRecordPumpFired.
                DiagRecordReadObservation(1);
                return _currentInputState;
            }
        }

        public InputState PreviousInputState => _previousInputState;

        public Vector2 VisualLookDelta => _visualLookDelta;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        public static bool GenerateMockInputHistory(uint startTick, uint count, uint seed)
        {
            InputDispatcher instance = ActiveRuntimeInstance;
            if (instance == null)
                return false;

            return instance.GenerateMockInputHistoryOwnerCold(startTick, count, seed);
        }

        /// <summary>
        /// Returns true when the underlying player input map is active and safe for gameplay reads,
        /// or when an automation locomotion override was applied on the latest CaptureState.
        /// Automation overrides are written into <see cref="_currentState"/> after the device-poll
        /// gate, so hop2 consumers (GetState via TryReadFrame / ProcessPlayerInputFrame) must be
        /// allowed to read that snapshot even when the native player map is still closed.
        /// </summary>
        public bool IsPlayerInputEnabled =>
            (_nativeInputManager != null && _nativeInputManager.IsPlayerInputEnabled)
            || _lastAutomationOverrideApplied;

        internal INativeInputManagerRuntime NativeInputRuntime => _nativeInputManager;

        /// <summary>
        /// Binds the bootstrap-owned native input action owner used by this dispatcher.
        /// </summary>
        /// <param name="inputManager">Native input manager validated by the bootstrapper.</param>
        public void BindNativeInputManager(INativeInputManagerRuntime inputManager)
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
            CaptureOwnerThread();
            TryPersistRuntimeOwner();
            if (_isInitialized)
            {
                EnsureInputBinding();
                RefreshCachedDataVaultCold();
                EnsureDeterministicInputNativeBuffers();
                EnsureInputReplayWriterCold();
                CaptureState();
                return;
            }

            EnsureInputBinding();
            RefreshCachedPlayerRuntimeContext();
            RefreshCachedDataVaultCold();
            EnsureHapticDeviceBinding();
            SubscribeToXRActiveChanged();
            RefreshXRNativeBufferState(allowColdAcquire: true);
            EnsureDeterministicInputNativeBuffers();
#if UNITY_EDITOR
            EnsureInputProfileCsvWatcher();
#endif
            EnsureInputReplayWriterCold();
            TryRegisterToDispatcher();
            _isInitialized = true;
            TryRegisterInputService();
            TryRegisterHotSwapListener();
            CaptureState();
        }

        private void Awake()
        {
            CaptureOwnerThread();
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            RefreshViewportSnapshotSlowSample();
            EnsureInputBinding();
            RefreshCachedPlayerRuntimeContext();
            RefreshCachedDataVaultCold();
            EnsureHapticDeviceBinding();
            SubscribeToXRActiveChanged();
            RefreshXRNativeBufferState(allowColdAcquire: true);
            EnsureDeterministicInputNativeBuffers();
#if UNITY_EDITOR
            EnsureInputProfileCsvWatcher();
#endif
        }

        private void OnEnable()
        {
            CaptureOwnerThread();
            ActiveRuntimeInstance = this;

            RefreshViewportSnapshotSlowSample();
            EnsureInputBinding();
            RefreshCachedDataVaultCold();
            EnsureHapticDeviceBinding();
            SubscribeToXRActiveChanged();
            RefreshXRNativeBufferState(allowColdAcquire: true);
            EnsureDeterministicInputNativeBuffers();
#if UNITY_EDITOR
            EnsureInputProfileCsvWatcher();
#endif
            EnsureInputReplayWriterCold();

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
            // Before anything is torn down: ClearFrameState below resets _lastCapturedFrame, and a run that
            // never tripped an observation threshold would otherwise print no census at all. A probe run
            // costs a whole editor lock, so the session is guaranteed to leave exactly one readable census.
            DiagEmitFinalCensus();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            UnsubscribeFromNativeInput();
            UnsubscribeFromXRActiveChanged();
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
#if UNITY_EDITOR
            DisposeInputProfileCsvWatcher();
#endif
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

        public void LateFrameTick()
        {
            DiagRecordLateFrameTick();
            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            PumpPreSimulationInputIfDispatcherSkipped();
            UpdateVisualLookInterpolation();
            DrainToolHaptics(deltaTime);
            FlushPendingHapticOutput();
        }

        /// <summary>
        /// Runs the per-frame input tick from a lane THIS component owns when the dispatcher did not run it.
        ///
        /// WHY THIS EXISTS, and it is not defensive padding. PreSimulationInputTick is the only per-frame
        /// caller of CaptureState (:719), and CaptureState is the only place the automation-override lane is
        /// consumed (ApplyAutomationOverride, :3345). Its single dispatcher-side caller is
        /// SystemDispatcher.RunDispatcherUpdate:5052-5054:
        ///     IInputDeterminismService inputDeterminism = _inputDeterminism;
        ///     if (inputDeterminism != null &amp;&amp; inputDeterminism.IsInitialized)
        ///         inputDeterminism.PreSimulationInputTick(unscaledDeltaTime);
        /// That field is written in exactly one place, RefreshInputDeterminismDependency
        /// (SystemDispatcher.cs:4163-4168), called once from SystemDispatcher.InitializeService
        /// (SystemDispatcher.cs:2093). The dispatcher is a BootstrapPhase.CoreServices node and this input
        /// dispatcher is a BootstrapPhase.Player node (GameBootstrapper.cs:5607-5638), so at that moment the
        /// registry's Input slot is still empty and GlobalRegistry.InputDeterminism resolves through
        /// GlobalRegistry.Input, which returns the NON-NULL NoOpInputService null object
        /// (GlobalRegistry.cs:920-936). The refresh accepts it because it only rejects null, and
        /// NoOpInputService.IsInitialized is a hardcoded false (GlobalRegistry.cs:8430), so the guard above
        /// is false forever.
        ///
        /// The one recovery path, SystemDispatcher's GlobalRegistryServiceSlot.Input rebound case
        /// (SystemDispatcher.cs:4215-4217), never fires either: GlobalRegistry.Register only queues a rebound
        /// when the slot ALREADY held a service (GlobalRegistry.cs:7352-7353 -
        /// `if (previousService != null) QueueServiceRebound(...)`), and TryRegisterInputService (:2943) fills
        /// an empty slot. First registration notifies nobody.
        ///
        /// Measured consequence, Logs/h8_playprobe_route.json moments[3]: "driver published 139 input
        /// overrides; movementIntent01max=0.000 ... inputServiceRegistered=True inputEnabled=True
        /// blockMask=0x00000000". Both input gates open, the override lane published all run, and not one
        /// override was ever consumed because the consumer never ticked. _currentState kept the all-zero
        /// snapshot taken by the cold CaptureState calls in InitializeService/OnEnable, GetState() (:735)
        /// handed that zero out every frame, and HectonPlayerMovement.ProcessPlayerInputFrame
        /// (HectonPlayerMovement.cs:8100-8102) wrote zeroes into _inputH/_inputV/_inputVertical, which is
        /// the sole source of the intent vector published at HectonPlayerMovement.cs:10019. The deterministic
        /// input ring was equally dead - _standardInputFrame never advanced past 0.
        ///
        /// The real repair belongs in SystemDispatcher (re-resolve the cached service when it is not
        /// initialized, instead of trusting a one-shot cold read). Until that lands, this owner refuses to
        /// depend on another system's stale cache for its own cadence: the late-frame lane is registered by
        /// TryRegisterToDispatcher (:2878) through GlobalRegistry.TryRegisterLateFrameTickable and is walked
        /// every dispatcher frame (SystemDispatcher.cs:5410-5419), so it is a lane this component can prove
        /// it is on. The frame guard means that once the dispatcher calls PreSimulationInputTick again this
        /// method costs one int compare and returns - no double substep, no double publish.
        ///
        /// Late-frame is after the updatable walk, so in the degraded configuration consumers see the
        /// override one frame later than they would from pre-simulation. That is a one-frame latency on a
        /// lane that was previously producing nothing at all.
        /// </summary>
        private void PumpPreSimulationInputIfDispatcherSkipped()
        {
            if (!_isInitialized)
                return;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastPreSimulationInputFrame == currentFrame)
                return;

            DiagRecordPumpFired();
            PreSimulationInputTick(Hecton8.Core.SystemDispatcher.CurrentFrameUnscaledDeltaTime);
        }

        // ---------------------------------------------------------------------------------------------
        // Automation-override hop census.
        //
        // WHY THIS EXISTS. Logs/h8_worldsim_probe5.log reports the Swim moment as
        // "movementIntent01max=0.000 ... inputServiceRegistered=True inputEnabled=True
        // switchToPlayerInputCalled=True blockMask=0x00000000", and the VERBSWEEP row as
        // "overrideFlagSeen=False overridesPublished=152 ... lastResolvedButtons=0x00000000 atFrame=0" plus
        // "LANECENSUS ... InputStateSignal=0". That is five numbers and they do NOT localise the fault,
        // because four of them are read from ONE field.
        //
        // H8_HeadlessWorldDriver.cs:1153-1157 samples overrideFlagSeen, atFrame, lastResolvedButtons and
        // arrivedInResolvedSnapshot all from IInputService.CurrentInputState, which is _currentInputState.
        // That field is assigned in exactly one place, PublishDeterministicInputState (:818), inside the try
        // block AFTER TryAcquireInputMutationGuard and the four TryResolveInputBuffer calls have all
        // succeeded, and publishInputState = true is set in the same straight-line block a few statements
        // later (:831). So "_currentInputState was updated" and "an InputStateSignal was pushed" are the same
        // event. InputStateSignal=0 therefore forces overrideFlagSeen=False, atFrame=0 and
        // lastResolvedButtons=0 REGARDLESS of whether the override lane was consumed. The driver's own hint
        // text - "false means TryConsumeLatestInputOverride never accepted the publish" - does not follow.
        //
        // Two hypotheses survive probe5 and produce byte-identical observables:
        //   H1 LateFrameTick() never ran, so the self-pump above never fired, so PreSimulationInputTick never
        //      ran. CaptureState is its only per-frame caller, so _currentState stayed at the all-zero cold
        //      capture (movementIntent01max=0.000) and nothing was ever published (InputStateSignal=0).
        //      Registration is a live suspect: TryRegisterToDispatcher (:2878) is the sole writer of
        //      _registeredLateFrame, it returns early when GlobalRegistry.Dispatcher is null, its result is
        //      never retried, and a false return is not logged anywhere.
        //   H2 PreSimulationInputTick DID run, but every TryConsumeLatestInputOverride hit the age branch at
        //      CoreDeterminismSignals.cs:197-202, which clears the signal and returns false with no log at
        //      all - and, independently, the publish gate failed, because
        //      OpenOrAcquireInputBufferForOwnerRoute (:1358) returns false on IsAllocationLocked /
        //      IsCompactionFenceActive and TryAcquireInputMutationGuard (:1418) returns false off the owner
        //      thread, both silently. probe5 does show vault turbulence across the menu transition
        //      (FatalMemoryLeakException at 01_MAIN_MENU).
        // The clock-skew LogError at CoreDeterminismSignals.cs:212 fired ZERO times in probe5, which rules
        // out the frame-clock latch fixed in 1261c9fc6 but does not separate H1 from H2.
        //
        // The census below separates them in one run. Counters are printed in pipeline order, so the first
        // zero from the left is the break point: lateFrameTick=0 is H1; lateFrameTick>0 with publishOk=0 and
        // a nonzero publishGuardFail/publishBufferFail is H2's publish gate; overrideRejected>0 with
        // overrideApplied=0 is the consume gate.
        //
        // It is emitted from a READ accessor, not from a tick lane, and that is deliberate. Every tick lane
        // this component owns (LateFrameTick, SlowTick) is registered by the same TryRegisterToDispatcher
        // call, so under H1 all of them are dead and a census emitted from one of them would print nothing -
        // the useless outcome. probe5 PROVES the driver read CurrentInputState (it printed atFrame and
        // lastResolvedButtons from it), so that accessor is the one observation point in this file with
        // measured reachability in the failing configuration.
        // ---------------------------------------------------------------------------------------------
        private void DiagRecordPumpFired()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPumpFiredCalls++;
#endif
        }

        private void DiagRecordLateFrameTick()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagLateFrameTickCalls++;
#endif
        }

        private void DiagRecordPreSimTick()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPreSimTickCalls++;
#endif
        }

        private void DiagRecordPreSimSubstep()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPreSimSubsteps++;
#endif
        }

        private void DiagRecordCaptureSkippedByFrameGuard()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagCaptureSkippedByFrameGuard++;
#endif
        }

        private void DiagRecordCaptureRan()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagCaptureRan++;
#endif
        }

        private void DiagRecordOverrideOutcome(bool applied, Vector2 consumedMove)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (applied)
            {
                _diagOverrideApplied++;
                _diagLastOverrideMoveX = consumedMove.x;
                _diagLastOverrideMoveY = consumedMove.y;
                return;
            }

            _diagOverrideRejected++;
#endif
        }

        private void DiagRecordPostBlockMask(uint blockMask, Vector2 postMaskMove)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (blockMask != 0u)
                _diagBlockMaskNonZero++;

            _diagLastPostMaskMoveX = postMaskMove.x;
            _diagLastPostMaskMoveY = postMaskMove.y;
#endif
        }

        private void DiagRecordPublishAttempt()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPublishAttempts++;
#endif
        }

        private void DiagRecordPublishGuardFail()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPublishGuardFail++;
            DiagReportGuardRefusal();
#endif
        }

        private void DiagRecordPublishBufferFail()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPublishBufferFail++;
            DiagReportBufferRefusal();
#endif
        }

        // -------------------------------------------------------------------------------------------------
        // Asserts "this owner holds no guard bits before it tries to acquire" and, when that is violated,
        // names WHO violated it.
        //
        // WHY THIS EXISTS RATHER THAN A SECOND COPY OF DiagReportGuardRefusal. That method runs AFTER the
        // vault has already refused, and its code 4 is defined as "an owner holds bits from THIS mask" -
        // which is true both when this component leaked its own bits and when a completely unrelated system
        // is parked on one of the five low residues this owner shares with roughly one BufferID in six. Those
        // are opposite defects with opposite owners and the vault cannot tell them apart, because
        // ReleaseMutationGuard (GlobalDataVault.cs:3010-3019) carries no owner identity at all.
        //
        // The split derived on InputOwnerGuardMaskLow/High is what separates them: the (id & 31) convention
        // used by the rest of the tree cannot reach the high word, so a high-word overlap narrows the author
        // to an (id & 63) caller - this file or MemorySentinelRuntime.cs:1153 - while a low-only overlap is
        // positive evidence that this component did NOT leak.
        //
        // Reached only at depth 0 and only from the slow path of TryAcquireInputMutationGuard, so the healthy
        // cost is one interface property read, one AND and one branch; nothing is allocated unless the
        // invariant is already broken. Latched per (code, lowest culprit bit) so a run prints one line per
        // distinct culprit instead of one per attempt - probe7 took 1240 refusals in 313 frames.
        // -------------------------------------------------------------------------------------------------
        private void DiagReportGuardResidue(IDataVault vault)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (vault == null)
                return;

            ulong held = vault.ActiveMutationGuardMask & InputOwnerMutationGuardMask;
            if (held == 0UL)
                return;

            uint heldLow = unchecked((uint)held);
            uint heldHigh = unchecked((uint)(held >> 32));
            // 2 = an (id & 63) caller, i.e. this owner leaked or MemorySentinelRuntime is holding.
            // 1 = low word only, which an (id & 31) caller can produce and this owner's leak cannot produce
            //     alone, because every acquire here claims all eight high bits as well.
            int code = heldHigh != 0u ? 2 : 1;
            int signature = code | ((int)math.tzcnt(held) << 4);
            if (_diagGuardResidueReported == signature)
                return;

            _diagGuardResidueReported = signature;
            // COLD ALLOC: one residue string per distinct culprit bit per session - owner: InputDispatcher
            Hecton8.Core.H8Debug.LogWarning(
                "[H8_INPUTRESIDUE] stage=PRE_ACQUIRE code=" + code +
                " (1=lowWordOnly:FOREIGN-(id&31)-caller-NOT-this-owner" +
                " 2=highWordDirty:(id&63)-caller:this-owner-leaked-or-MemorySentinelRuntime)" +
                " guardDepth=" + _inputMutationGuardDepth +
                " | heldOverlap=" + held +
                " heldLow=" + heldLow +
                " heldHigh=" + heldHigh +
                " lowestHeldBit=" + (int)math.tzcnt(held) +
                " | ownerGuardMask=" + InputOwnerMutationGuardMask +
                " ownerLow=" + InputOwnerGuardMaskLow +
                " ownerHigh=" + InputOwnerGuardMaskHigh +
                " vaultGuardMask=" + vault.ActiveMutationGuardMask +
                " | attempts=" + _diagPublishAttempts +
                " buffersReady=" + _deterministicVaultBuffersReady +
                " buffersCleared=" + _deterministicVaultBuffersCleared +
                " - guardDepth is 0 here by construction (the depth>0 fast path returns before this), so this" +
                " owner believes it holds nothing and every bit in heldOverlap was set by someone else or" +
                " leaked by a previous acquire of ours. code=1 EXONERATES this file: all fifteen" +
                " TryAcquireInputMutationGuard sites release in a finally, and every acquire claims all eight" +
                " high bits, so a leak by this owner would necessarily show heldHigh!=0. code=1 means an" +
                " unrelated buffer whose (id & 31) is 0,1,2,3 or 5 is holding a mutation guard - fix the" +
                " colliding owner or the mask convention, NOT this file. code=2 means an (id & 63) caller, and" +
                " as of 2026-07-29 only two others exist: ShinobuStormPropagationDebugGizmo.cs:79 (Editor-only)" +
                " and MemorySentinelRuntime.cs:1153, which is ABSENT from every live scene and prefab and is" +
                " not in GameBootstrapper, so it should not be able to hold anything in a normal run - if" +
                " code=2 appears, suspect this file first and re-check that wiring second. The sentinel is" +
                " still worth naming because its release path is genuinely unbalanced: it builds an (id & 63)" +
                " mask over ARBITRARY target buffer ids (MemorySentinelRuntime.cs:1130-1142) and holds it" +
                " across a scheduled job whose CompleteValidationJob returns false twice WITHOUT unlocking" +
                " (MemorySentinelRuntime.cs:1184-1202), so if it is ever wired, a job that never finalizes" +
                " parks those bits for the rest of the session.");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // -------------------------------------------------------------------------------------------------
        // Which precondition refused the publish.
        //
        // WHY THIS EXISTS. The hop census above already localised the fault by measurement:
        // Logs/h8_probe7.log reports "publishAttempt=1240 publishGuardFail=1240 publishBufferFail=0
        // publishOk=0" on all three emissions, so TryAcquireInputMutationGuard refuses 100% of publishes and
        // the four TryResolveInputBuffer calls are never even reached (publishBufferFail=0 is unreachable
        // code, not a passing check). What the census can NOT say is WHICH precondition refused, and the
        // guard has six distinct refusal paths that produce byte-identical observables - GlobalDataVault
        // records every one of them through RecordMutationGuardContentionFault (GlobalDataVault.cs:5454)
        // into a telemetry ring that nothing in the probe route prints.
        //
        // Every discriminator below is already public on IDataVault (:42-51), so this reads the vault's own
        // state rather than guessing: IsCompactionFenceActive, ActiveMutationGuardMask, ActiveBurstLockMask.
        // Two refusal paths are NOT observable from outside - a vault whose _initialized went false and a
        // contended _blockMutationGate (TryEnterBlockMutationGate, :3028) - so they collapse into
        // PublishRefusalGuardOpaque, and that code appearing is itself the answer: it excludes the other
        // four. probe7 already excludes two of them at end-of-run, "[H8_PLAYPROBE] DETERMINISM OWNER
        // LIFETIME ... vaultAllocationLocked=False vaultCompactionFenceActive=False".
        //
        // Transition-gated, not per frame. 1240 refusals in one run collapse to one line per distinct cause,
        // and the first refusal always prints because _diagPublishRefusalReported starts at 0.
        // -------------------------------------------------------------------------------------------------
        private void DiagReportGuardRefusal()
        {
            IDataVault vault = _dataVault;
            int code;
            ulong guardHeld = 0UL;
            uint activeLockConflict = 0u;
            if (vault == null)
            {
                code = PublishRefusalGuardVaultNull;
            }
            else if (!IsOwnerThread())
            {
                code = PublishRefusalGuardOwnerThread;
            }
            else if (vault.IsCompactionFenceActive)
            {
                code = PublishRefusalGuardCompactionFence;
            }
            else
            {
                guardHeld = vault.ActiveMutationGuardMask & InputOwnerMutationGuardMask;
                activeLockConflict = vault.ActiveBurstLockMask & InputOwnerActiveLockConflictMask;
                if (guardHeld != 0UL)
                    code = PublishRefusalGuardMaskHeld;
                else if (activeLockConflict != 0u)
                    code = PublishRefusalGuardActiveLockConflict;
                else
                    code = PublishRefusalGuardOpaque;
            }

            if (_diagPublishRefusalReported == code)
                return;

            _diagPublishRefusalReported = code;
            // COLD ALLOC: one refusal string per distinct cause per session - owner: InputDispatcher
            Hecton8.Core.H8Debug.LogWarning(
                "[H8_INPUTREFUSE] stage=GUARD code=" + code +
                " (1=vaultNull 2=notOwnerThread 3=compactionFence 4=guardBitsAlreadyHeld" +
                " 5=activeLockConflict 6=opaque:blockMutationGateContended-or-vaultNotInitialized)" +
                " ownerThread=" + _ownerThreadId +
                " callThread=" + Thread.CurrentThread.ManagedThreadId +
                " | ownerGuardMask=" + InputOwnerMutationGuardMask +
                " vaultGuardMask=" + (vault == null ? 0UL : vault.ActiveMutationGuardMask) +
                " heldOverlap=" + guardHeld +
                " | ownerConflictMask=" + InputOwnerActiveLockConflictMask +
                " vaultActiveLocks=" + (vault == null ? 0u : vault.ActiveBurstLockMask) +
                " lockOverlap=" + activeLockConflict +
                " lowestConflictBit=" + (activeLockConflict == 0u ? -1 : (int)math.tzcnt(activeLockConflict)) +
                " | allocLocked=" + (vault != null && vault.IsAllocationLocked) +
                " fence=" + (vault != null && vault.IsCompactionFenceActive) +
                " vaultGenId=" + (vault == null ? 0u : vault.VaultGenerationID) +
                " genMiss=" + (vault == null ? 0 : vault.GenerationHandleMissCount) +
                " | buffersReady=" + _deterministicVaultBuffersReady +
                " buffersCleared=" + _deterministicVaultBuffersCleared +
                " guardDepth=" + _inputMutationGuardDepth +
                " attempts=" + _diagPublishAttempts +
                " - code 4 means an owner holds bits from THIS mask and did not release them; code 5 means a" +
                " buffer unrelated to input is pinned or write-locked on a colliding residue, because the" +
                " vault folds the 64-bit guard mask onto 32 active-lock bits with (bufferId & 31) and this" +
                " owner claims 13 of those 32. buffersCleared=False additionally means the guarded cold block" +
                " at EnsureDeterministicInputNativeBuffers never ran, so the buffers it clears there still hold" +
                " their allocation-time contents." +
                " NOTE: this no longer implicates ShinobuInputProfile. That buffer is allocated ClearMemory and" +
                " seeded by TryEnsureDefaultInputProfileSeeded under a narrow single-buffer mask outside that" +
                " block, with a SlowTick retry; a seed failure prints [H8_INPUTPROFILE] separately. Read" +
                " [H8_INPUTPIN] too - code 5 can be this owner's OWN haptic schedule pins.");
        }

        private void DiagReportBufferRefusal()
        {
            int journal = DiagClassifyInputBuffer(in _inputJournalHandle, DeterministicInputRingCapacity, out int journalLength);
            int predicted = DiagClassifyInputBuffer(in _predictedInputHandle, DeterministicInputRingCapacity, out int predictedLength);
            int targets = DiagClassifyInputBuffer(in _predictedInputAupTargetHandle, DeterministicInputRingCapacity, out int targetsLength);
            int bridge = DiagClassifyInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out int bridgeLength);
            int code = PublishRefusalBufferFlag | journal | (predicted << 4) | (targets << 8) | (bridge << 12);
            if (_diagPublishRefusalReported == code)
                return;

            _diagPublishRefusalReported = code;
            // COLD ALLOC: one refusal string per distinct cause per session - owner: InputDispatcher
            Hecton8.Core.H8Debug.LogWarning(
                "[H8_INPUTREFUSE] stage=BUFFER need=" + DeterministicInputRingCapacity +
                " (state 0=ok 1=noHandle:neverAllocated 2=vaultRefusedHandle:staleGeneration/missingMetadata/ownerMismatch" +
                " 3=resolvedNotCreated 4=resolvedTooShort)" +
                " | journal id=" + _inputJournalHandle.BufferID +
                " gen=" + _inputJournalHandle.Generation +
                " len=" + journalLength +
                " state=" + journal +
                " | predicted id=" + _predictedInputHandle.BufferID +
                " gen=" + _predictedInputHandle.Generation +
                " len=" + predictedLength +
                " state=" + predicted +
                " | aupTargets id=" + _predictedInputAupTargetHandle.BufferID +
                " gen=" + _predictedInputAupTargetHandle.Generation +
                " len=" + targetsLength +
                " state=" + targets +
                " | bridgeRing id=" + _inputStateBridgeRingHandle.BufferID +
                " gen=" + _inputStateBridgeRingHandle.Generation +
                " len=" + bridgeLength +
                " state=" + bridge +
                " | vaultGenId=" + (_dataVault == null ? 0u : _dataVault.VaultGenerationID) +
                " genMiss=" + (_dataVault == null ? 0 : _dataVault.GenerationHandleMissCount) +
                " - state 1 and state 2 are DIFFERENT defects: 1 means this owner never got a handle" +
                " (EnsureGenerationHandle was never reached or returned empty), 2 means it holds one the" +
                " vault will not honour. Neither is a stale-handle generation stamp unless genMiss is rising.");
        }

        // Mirrors TryResolveInputBuffer's checks but reports WHICH one failed instead of collapsing all four
        // into one bool. Zero allocation; only ever called from the refusal path.
        private int DiagClassifyInputBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out int actualLength) where T : struct
        {
            actualLength = 0;
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
                return DiagBufferNoHandle;

            if (!vault.TryResolveHandle(in handle, out NativeArray<T> buffer))
                return DiagBufferVaultRefused;

            if (!buffer.IsCreated)
                return DiagBufferNotCreated;

            actualLength = buffer.Length;
            return actualLength < requiredLength ? DiagBufferTooShort : DiagBufferOk;
        }
#endif

        private void DiagRecordPublishOk()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagPublishOk++;
#endif
        }

        /// <summary>
        /// Counts a read of the resolved/player snapshot and emits the hop census at three fixed
        /// observation counts. Zero allocation on every call except those three.
        /// </summary>
        /// <param name="readHop">
        /// 0 = end-of-session final census, 1 = CurrentInputState (driver-side), 2 = GetState
        /// (movement-side). readHop=2 appearing at all proves HectonPlayerMovement is reading this service.
        /// </param>
        private void DiagRecordReadObservation(int readHop)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagReadObservations++;

            // Monotone stage gate rather than equality against the thresholds: these counters are plain
            // int increments off two accessors, so an exact-value test could be stepped over and lose the
            // whole report. This cannot be skipped.
            int threshold;
            switch (_diagReportsEmitted)
            {
                case 0:
                    threshold = DiagReportAtObservation1;
                    break;
                case 1:
                    threshold = DiagReportAtObservation2;
                    break;
                case 2:
                    threshold = DiagReportAtObservation3;
                    break;
                default:
                    return;
            }

            if (_diagReadObservations < threshold)
                return;

            _diagReportsEmitted++;
            DiagEmitHopCensus(readHop);
#endif
        }

        private void DiagEmitFinalCensus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_diagFinalCensusEmitted)
                return;

            _diagFinalCensusEmitted = true;
            DiagEmitHopCensus(0);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DiagEmitHopCensus(int readHop)
        {
            // COLD ALLOC: one census string - emitted at most three times per session from a read accessor -
            // owner: InputDispatcher
            Hecton8.Core.H8Debug.LogWarning(
                "[H8_INPUTHOP] readHop=" + readHop +
                " obs=" + _diagReadObservations +
                " | lateFrameTick=" + _diagLateFrameTickCalls +
                " pumpFired=" + _diagPumpFiredCalls +
                " presimTick=" + _diagPreSimTickCalls +
                " presimSubsteps=" + _diagPreSimSubsteps +
                " | captureRan=" + _diagCaptureRan +
                " captureSkippedByFrameGuard=" + _diagCaptureSkippedByFrameGuard +
                " | overrideApplied=" + _diagOverrideApplied +
                " overrideRejected=" + _diagOverrideRejected +
                " lastOverrideMove=(" + _diagLastOverrideMoveX + "," + _diagLastOverrideMoveY + ")" +
                " | blockMaskNonZero=" + _diagBlockMaskNonZero +
                " postMaskMove=(" + _diagLastPostMaskMoveX + "," + _diagLastPostMaskMoveY + ")" +
                " | publishAttempt=" + _diagPublishAttempts +
                " publishGuardFail=" + _diagPublishGuardFail +
                " publishBufferFail=" + _diagPublishBufferFail +
                " publishOk=" + _diagPublishOk +
                " | currentStateMove=(" + _currentState.MoveDelta.x + "," + _currentState.MoveDelta.y + ")" +
                " currentInputStateFrame=" + _currentInputState.Frame +
                " standardInputFrame=" + _standardInputFrame +
                " inputStateSequence=" + _inputStateSequence +
                " | initialized=" + _isInitialized +
                " regLateFrame=" + _registeredLateFrame +
                " regSlowTick=" + _registeredSlowTick +
                " regInputService=" + _registeredInputService +
                " vaultNull=" + (_dataVault == null) +
                " vaultBuffersReady=" + _deterministicVaultBuffersReady +
                " lastPresimFrame=" + _lastPreSimulationInputFrame +
                " lastCapturedFrame=" + _lastCapturedFrame +
                " frameIndex=" + Hecton8.Core.SystemDispatcher.CurrentFrameIndex +
                " frameId=" + Hecton8.Core.SystemDispatcher.CurrentFrameId +
                " - counters are in PIPELINE ORDER; the first zero from the left is the hop that dropped the" +
                " override. lateFrameTick=0 means this component's late-frame lane never ran, so the" +
                " self-pump never fired and PreSimulationInputTick - the only per-frame caller of" +
                " CaptureState - never ran; check regLateFrame and TryRegisterToDispatcher, NOT the override" +
                " lane. lateFrameTick>0 with captureRan=0 means the CaptureState frame guard suppressed every" +
                " capture; compare lastCapturedFrame against frameIndex. captureRan>0 with" +
                " overrideApplied=0 means TryConsumeLatestInputOverride rejected every publish - with no" +
                " clock-skew LogError that leaves the silent age>maxFrameAge branch at" +
                " CoreDeterminismSignals.cs:197. Read overrideApplied, NOT the applied/rejected ratio:" +
                " overrideRejected is expected to be large even in a fully healthy run because it counts" +
                " every poll on a frame where the driver published nothing (Sequence==0 early return)." +
                " overrideApplied>0 with currentStateMove=(0,0) means the" +
                " value was consumed and then erased downstream; read postMaskMove and blockMaskNonZero." +
                " publishOk=0 explains overrideFlagSeen/atFrame/lastResolvedButtons/InputStateSignal being" +
                " zero on the driver side ALL BY ITSELF, because _currentInputState and the signal push are" +
                " the same event - those four driver numbers are not evidence about the override lane.");
        }
#endif

        public void SlowTick()
        {
            RefreshViewportSnapshotSlowSample();
            // The profile seed's only retry lane. EnsureDeterministicInputNativeBuffers cannot serve as one:
            // it returns at its top once the buffers are ready and valid, so its seed attempt is one-shot.
            // Costs one bool test per slow tick once seeded, and nothing else.
            if (!_inputProfileDefaultsSeeded && _deterministicVaultBuffersReady)
                TryEnsureDefaultInputProfileSeeded();
        }

        public void PreSimulationInputTick(float deltaTime)
        {
            // L18: heal LateFrame/Slow lane membership after ClearAllLanes while PreSim still runs.
            TryRegisterToDispatcher();

            // Stamped for BOTH callers (SystemDispatcher.cs:5054 and the late-frame self-pump), so whichever
            // one reaches the frame first suppresses the other. Written before any early exit inside the
            // substep loop so a frame that legitimately produces zero substeps still counts as ticked.
            _lastPreSimulationInputFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            DiagRecordPreSimTick();
#if UNITY_EDITOR
            if (_deterministicVaultBuffersReady)
                ApplyPendingInputProfileCsv();
#endif

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

                DiagRecordPreSimSubstep();
                CaptureState((float)StandardInputTickIntervalSeconds);
                PublishDeterministicInputState(_standardInputFrame++);
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
            DiagRecordReadObservation(2);
            return _currentState;
        }

        public bool TryGetInputState(uint frame, out InputState state)
        {
            if (!TryReadInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState>.ReadOnly inputStateRing))
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

            DiagRecordPublishAttempt();
            if (!TryAcquireInputMutationGuard())
            {
                DiagRecordPublishGuardFail();
                return;
            }

            bool stageReplaySnapshot = false;
            bool dumpDeterministicBlackBox = false;
            bool publishInputState = false;
            bool reportDeterministicFrame = false;
            uint deterministicPackedAxes = 0u;
            uint discretePreviousButtonMask = 0u;
            uint discreteCurrentButtonMask = 0u;
            InputStateSignal signal = default;
            try
            {
                if (!TryResolveInputBuffer(in _inputJournalHandle, DeterministicInputRingCapacity, out NativeArray<InputStateDTO> inputJournal) ||
                    !TryResolveInputBuffer(in _predictedInputHandle, DeterministicInputRingCapacity, out NativeArray<PredictedInputDTO> predictedInputs) ||
                    !TryResolveInputBuffer(in _predictedInputAupTargetHandle, DeterministicInputRingCapacity, out NativeArray<PredictedInputAupTargetDTO> predictedInputTargets) ||
                    !TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputStateRing))
                {
                    DiagRecordPublishBufferFail();
                    return;
                }

                _lastDeterministicInputFrame = currentFrame;
                InputState rawState = BuildInputState(_currentState, currentFrame, unchecked(++_inputStateSequence));
                InputStateDTO rawDto = BuildInputStateDto(_currentState);
                int ringIndex = (int)(currentFrame % DeterministicInputRingCapacity);
                inputJournal[ringIndex] = rawDto;
                inputStateRing[ringIndex] = rawState;
                double3 targetAup = double3.zero;
                PredictedInputRingWriter.WriteLocalInput(
                    predictedInputs,
                    predictedInputTargets,
                    in rawDto,
                    in targetAup,
                    currentFrame,
                    PredictedInputFlags.None);
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
                WriteReplayFrameDto(currentFrame, in resolvedState);

                signal.State = resolvedState;
                signal.CurrentInputSchemeHash = _currentInputSchemeHash;
                signal.InputDelayFrames = (byte)delayFrames;
                signal.AppliedDelayFrames = appliedDelayFrames;
                signal.Flags = resolvedState.Flags;
                discretePreviousButtonMask = _previousButtonMask;
                discreteCurrentButtonMask = resolvedState.ButtonsBitmask;
                _previousButtonMask = resolvedState.ButtonsBitmask;
                publishInputState = true;
                DiagRecordPublishOk();
                dumpDeterministicBlackBox = WriteDeterministicInputBlackBox(
                    in resolvedState,
                    _currentInputSchemeHash,
                    out deterministicPackedAxes,
                    out reportDeterministicFrame);
                if ((resolvedState.Sequence % StandardInputRingCapacity) == 0u)
                    stageReplaySnapshot = true;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }

            if (publishInputState)
            {
                SignalBus<InputStateSignal>.TryPushTracked(in signal, ref s_x001InputDispatcherSignalPushDropCount);
                PublishDiscreteInputSignals(discreteCurrentButtonMask, discretePreviousButtonMask);
            }

            if (reportDeterministicFrame)
            {
                CrashTelemetryBuffer.ReportDeterministicInputFrame(
                    signal.State.Frame,
                    signal.State.Sequence,
                    signal.State.ButtonsBitmask,
                    deterministicPackedAxes);
            }

            if (dumpDeterministicBlackBox)
                DumpDeterministicInputBlackBox();

            if (stageReplaySnapshot)
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

        private void WriteReplayFrameDto(uint currentFrame, in InputState resolvedState)
        {
            if (!IsInputReplayRecordingActive())
                return;

            if (!TryResolveInputBuffer(in _inputReplayFrameHandle, DeterministicInputRingCapacity, out NativeArray<ReplayFrameDTO> replayFrames))
                return;

            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) ||
                !TryResolveReplayAup(in pose, out double3 recordedAup))
            {
                return;
            }

            float3 velocity = float3.zero;
            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                velocity = SanitizeReplayFloat3(movementState.Velocity);

            float3 moveAxis = ResolveReplayMoveAxis(in resolvedState);
            uint inputFlags = (uint)resolvedState.Flags;
            uint inputHash = HashReplayInput(in resolvedState, in moveAxis, currentFrame);
            uint stateHash = HashReplayState(in recordedAup, in velocity, currentFrame, inputFlags);
            int ringIndex = (int)(currentFrame % DeterministicInputRingCapacity);
            ReplayFrameDTO frame = default;
            frame.RecordedAup = recordedAup;
            frame.Tick = currentFrame;
            frame.InputMoveAxis = moveAxis;
            frame.Velocity = velocity;
            frame.DeltaTime = (float)StandardInputTickIntervalSeconds;
            frame.Frame = currentFrame;
            frame.InputFlags = inputFlags;
            frame.StateHash = stateHash;
            frame.InputHash = inputHash;
            replayFrames[ringIndex] = frame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInputReplayRecordingActive()
        {
            return _inputReplaySignal != null &&
                   _inputReplayThread != null &&
                   Volatile.Read(ref _inputReplayStopRequested) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveReplayAup(in PlayerRuntimePoseSnapshot pose, out double3 recordedAup)
        {
            recordedAup = double3.zero;
            AbsoluteUniversePosition aup = pose.Aup;
            if (!aup.IsFinite())
                return false;

            double3 candidate = aup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(candidate)))
                return false;

            recordedAup = default;
            recordedAup.x = CanonicalizeReplayDouble(candidate.x);
            recordedAup.y = CanonicalizeReplayDouble(candidate.y);
            recordedAup.z = CanonicalizeReplayDouble(candidate.z);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveReplayMoveAxis(in InputState state)
        {
            float3 moveAxis = default;
            moveAxis.x = state.MoveX * InputState.AxisInvQuantizeScale;
            moveAxis.y = state.Vertical * InputState.AxisInvQuantizeScale;
            moveAxis.z = state.MoveY * InputState.AxisInvQuantizeScale;
            return SanitizeReplayFloat3(moveAxis);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeReplayFloat3(float3 value)
        {
            float3 sanitized = default;
            sanitized.x = CanonicalizeReplayFloat(value.x);
            sanitized.y = CanonicalizeReplayFloat(value.y);
            sanitized.z = CanonicalizeReplayFloat(value.z);
            return sanitized;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CanonicalizeReplayFloat(float value)
        {
            return math.isfinite(value) && value != 0f ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CanonicalizeReplayDouble(double value)
        {
            return math.isfinite(value) && value != 0d ? value : 0d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashReplayInput(in InputState state, in float3 moveAxis, uint currentFrame)
        {
            uint hash = 2166136261u;
            hash = MixReplayHash(hash, currentFrame);
            hash = MixReplayHash(hash, state.Frame);
            hash = MixReplayHash(hash, state.Sequence);
            hash = MixReplayHash(hash, state.ButtonsBitmask);
            hash = MixReplayHash(hash, (uint)state.Flags);
            hash = MixReplayHash(hash, math.asuint(moveAxis.x));
            hash = MixReplayHash(hash, math.asuint(moveAxis.y));
            return MixReplayHash(hash, math.asuint(moveAxis.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashReplayState(in double3 recordedAup, in float3 velocity, uint currentFrame, uint flags)
        {
            uint hash = 0x811C9DC5u ^ 0x1626A11Cu;
            hash = MixReplayHash(hash, currentFrame);
            hash = MixReplayHash(hash, flags);
            hash = MixReplayHash(hash, FoldReplayHash(math.asulong(recordedAup.x)));
            hash = MixReplayHash(hash, FoldReplayHash(math.asulong(recordedAup.y)));
            hash = MixReplayHash(hash, FoldReplayHash(math.asulong(recordedAup.z)));
            hash = MixReplayHash(hash, math.asuint(velocity.x));
            hash = MixReplayHash(hash, math.asuint(velocity.y));
            return MixReplayHash(hash, math.asuint(velocity.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixReplayHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FoldReplayHash(ulong value)
        {
            return (uint)value ^ (uint)(value >> 32);
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
            }
            if ((pressed & (uint)PlayerInputAction.Inventory) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToggleInventory);
            }
            if ((pressed & (uint)PlayerInputAction.Cancel) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.Cancel);
            }
            if ((pressed & (uint)PlayerInputAction.TabNext) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.TabNext);
            }
            if ((pressed & (uint)PlayerInputAction.TabPrevious) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.TabPrevious);
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot1) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot1);
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot2) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot2);
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot3) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot3);
            }
            if ((pressed & (uint)PlayerInputAction.ToolSlot4) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.ToolSlot4);
            }
            if ((pressed & (uint)PlayerInputAction.Flashlight) != 0u)
                PublishPlayerInputCommand(PlayerInputSignalCommands.Flashlight);
            if ((pressed & (uint)PlayerInputAction.Interact) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.Interact);
            }
            if ((pressed & (uint)PlayerInputAction.PrimaryFire) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.PrimaryAction);
            }
            if ((pressed & (uint)PlayerInputAction.SecondaryFire) != 0u)
            {
                PublishPlayerInputCommand(PlayerInputSignalCommands.SecondaryAction);
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
#if UNITY_EDITOR
            if (UnsafeUtility.SizeOf<InputState>() != InputStateSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] InputState ABI violation; expected 24 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<PlayerInputState>() != PlayerInputStateSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] PlayerInputState ABI violation; expected 64 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<InputStateDTO>() != InputStateDtoSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] InputStateDTO ABI violation; expected 24 bytes.");
            if (UnsafeUtility.SizeOf<HapticCommandDTO>() != HapticCommandDtoSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] HapticCommandDTO ABI violation; expected 16 bytes.");
            if (UnsafeUtility.SizeOf<XRInputState>() != XRInputStateSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] XRInputState ABI violation; expected 64 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<BufferedActionEntry>() != BufferedActionEntrySizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] BufferedActionEntry ABI violation; expected 16 bytes with natural ARM64 alignment.");
            if (UnsafeUtility.SizeOf<ReplayFrameDTO>() != ReplayFrameDtoSizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] ReplayFrameDTO ABI violation; expected 80 bytes.");
            if (UnsafeUtility.SizeOf<MemoryStateTelemetryEntry>() != ReplayTelemetryEntrySizeBytes)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] MemoryStateTelemetryEntry ABI violation; expected 64 bytes.");
#endif
            if (_deterministicVaultBuffersReady && ValidateDeterministicInputBuffers())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool ready =
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _currentInputDtoHandle,
                    BufferID.ShinobuInputCurrentDto,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputJournalHandle,
                    BufferID.ShinobuInputJournalRing,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _predictedInputHandle,
                    BufferID.ShinobuPredictedInputRing,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _predictedInputAupTargetHandle,
                    BufferID.ShinobuPredictedInputAupTargets,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputStateBridgeRingHandle,
                    BufferID.ShinobuInputStateBridgeRing,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _buttonMaskWindowHandle,
                    BufferID.ShinobuInputButtonMaskWindow,
                    ButtonMaskWindowCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                // ClearMemory, NOT UninitializedMemory, and these two are the only entries in this list that
                // need it. Their readers cannot survive dirty bytes and both are read BEFORE any writer runs:
                //   ShinobuInputBlockMask  -> ReadInputBlockMask() returns inputBlockMask[0] raw, with no
                //     validation of any kind, straight into ApplyInputBlockMask. A dirty low bit there zeroes
                //     MoveDelta/LookDelta/ActionsBitmask every frame - input dies looking exactly like "the
                //     player is not pressing anything". Every writer of this buffer (ClearVaultBuffer at the
                //     cold block, SetInputBlockMask) is behind TryAcquireInputMutationGuard, so when the guard
                //     is contended nothing ever overwrites the garbage.
                //   ShinobuInputProfile    -> ReadInputProfile() sanitises the eight floats (defaults fallback
                //     plus per-field clamp) but NEVER touches .Flags, and a dirty bit 0 there is
                //     InputProfileFlagEnableMockCollision - it silently switches the haptic synth onto the mock
                //     impulse storm path (HectonInputRuntime_HapticSynth.cs:167,:367) and takes an extra vault
                //     pin with it.
                // The vault honours the option (GlobalDataVault.ShouldClear, :6463) and its arena reuses freed
                // blocks (TryAllocateBlockLocked, :6114-6160), so UninitializedMemory here really can hand
                // back a previous tenant's bytes rather than fresh zero pages. SanitizeFinitePayload<T> does
                // not help: it only special-cases float/float2/float3/float4, never a DTO struct.
                // Zero is the correct, already-handled value for both: 0u block mask makes ApplyInputBlockMask
                // early-return, and an all-zero profile has OuterDeadzone == 0f, which is exactly the
                // ReadInputProfile reject condition that returns the full Default* fallback. Cost is one
                // MemClear of 4 + 64 bytes at cold allocation; the clear happens inside the vault's own block
                // mutation gate, so it does NOT depend on this owner's mutation guard.
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputBlockMaskHandle,
                    BufferID.ShinobuInputBlockMask,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputProfileHandle,
                    BufferID.ShinobuInputProfile,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputTelemetryHandle,
                    BufferID.ShinobuInputTelemetryRing,
                    InputBlackBoxCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputReplaySnapshotHandle,
                    BufferID.ShinobuInputReplaySnapshot,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputReplayFrameHandle,
                    BufferID.ShinobuInputReplayFrames,
                    DeterministicInputRingCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputReplayTelemetryHandle,
                    BufferID.ShinobuInputReplayTelemetry,
                    InputBlackBoxCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _hapticCommandDtoHandle,
                    BufferID.ShinobuInputHapticCommands,
                    HapticCommandDtoCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _);
#if UNITY_EDITOR
            ready = ready &&
                OpenOrAcquireInputBufferForOwnerRoute(
                    ref _inputProfileCsvScratchHandle,
                    BufferID.ShinobuInputCsvScratch,
                    4096,
                    NativeArrayOptions.UninitializedMemory,
                    out _);
#endif

            _deterministicVaultBuffersReady = ready;

            if (!_deterministicVaultBuffersReady)
                return;

            // BEFORE the broad guard and before the _deterministicVaultBuffersCleared early-return, because
            // the block below is one-shot: from the next call onward this method returns at its top on
            // _deterministicVaultBuffersReady && ValidateDeterministicInputBuffers(). Anything that lives only
            // inside that block gets a single attempt and no retry.
            TryEnsureDefaultInputProfileSeeded();

            if (_deterministicVaultBuffersCleared)
                return;

            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                ClearVaultBuffer(ref _currentInputDtoHandle);
                ClearVaultBuffer(ref _inputJournalHandle);
                InitializePredictedInputBuffers(0u);
                ClearVaultBuffer(ref _inputStateBridgeRingHandle);
                ClearVaultBuffer(ref _buttonMaskWindowHandle);
                ClearVaultBuffer(ref _inputBlockMaskHandle);
                ClearVaultBuffer(ref _inputTelemetryHandle);
                ClearVaultBuffer(ref _inputReplaySnapshotHandle);
                ClearVaultBuffer(ref _inputReplayFrameHandle);
                ClearVaultBuffer(ref _inputReplayTelemetryHandle);
                ClearVaultBuffer(ref _hapticCommandDtoHandle);
#if UNITY_EDITOR
                ClearVaultBuffer(ref _inputProfileCsvScratchHandle);
#endif
                // Reached with the broad guard held at depth 1, so this takes the depth>0 branch and writes
                // directly rather than asking the vault for the narrow mask it would collide with. Normally a
                // no-op, because the call above this block already seeded it.
                TryEnsureDefaultInputProfileSeeded();
                _deterministicVaultBuffersCleared = true;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
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
                   TryResolveInputBuffer(in _predictedInputHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _predictedInputAupTargetHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _buttonMaskWindowHandle, ButtonMaskWindowCapacity, out _) &&
                   TryResolveInputBuffer(in _inputBlockMaskHandle, 1, out _) &&
                   TryResolveInputBuffer(in _inputProfileHandle, 1, out _) &&
                   TryResolveInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out _) &&
                   TryResolveInputBuffer(in _inputReplaySnapshotHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _inputReplayFrameHandle, DeterministicInputRingCapacity, out _) &&
                   TryResolveInputBuffer(in _inputReplayTelemetryHandle, InputBlackBoxCapacity, out _) &&
                   TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out _)
#if UNITY_EDITOR
                   && TryResolveInputBuffer(in _inputProfileCsvScratchHandle, 4096, out _)
#endif
                   ;
        }

        private bool OpenOrAcquireInputBufferForOwnerRoute<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryResolveInputBuffer(in handle, requiredLength, out buffer))
                return true;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
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

        private bool TryReadInputBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                handle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireInputMutationGuard()
        {
            if (_inputMutationGuardDepth > 0)
            {
                _inputMutationGuardDepth++;
                return true;
            }

            IDataVault vault = _dataVault;
            if (vault == null || !IsOwnerThread())
                return false;

            // Drop our OWN stale haptic schedule pins before asking the vault, or this owner deadlocks against
            // itself: those pins sit on vault active-lock residues inside the fold of
            // InputOwnerMutationGuardMask, so they refuse this guard. Only pins stamped with an earlier
            // dispatcher frame are released - a current-frame pin may still have a job reading the buffers.
            // Full derivation of the residue collision is on ReleaseStaleHapticSynthesisSchedulePins in
            // HectonInputRuntime_HapticSynth.cs. Cheap: one uint test when nothing is pinned.
            ReleaseStaleHapticSynthesisSchedulePins();

            // Invariant check at the LEAK, not 1240 refusals later. Reached only with
            // _inputMutationGuardDepth == 0 (the depth > 0 fast path returned above), which means this owner
            // believes it holds no guard bits at all. Any overlap here therefore has exactly two possible
            // authors, and DiagReportGuardResidue separates them by which half of the mask is dirty.
            // Purely observational - it does not alter the return value, so the guard stays fail-closed.
            DiagReportGuardResidue(vault);

            if (!vault.TryAcquireMutationGuard(InputOwnerMutationGuardMask))
                return false;

            _inputMutationGuardVault = vault;
            _inputMutationGuardDepth = 1;
            return true;
        }

        private void ReleaseInputMutationGuard()
        {
            int depth = _inputMutationGuardDepth;
            if (depth <= 0)
                return;

            depth--;
            _inputMutationGuardDepth = depth;
            if (depth != 0)
                return;

            IDataVault vault = _inputMutationGuardVault;
            _inputMutationGuardVault = null;
            if (vault != null)
                vault.ReleaseMutationGuard(InputOwnerMutationGuardMask);
        }

        private void CaptureOwnerThread()
        {
            if (_ownerThreadId == 0)
                _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private bool IsOwnerThread()
        {
            return _ownerThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _ownerThreadId;
        }

        private void ClearVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveInputBuffer(in handle, 1, out NativeArray<T> buffer))
                    return;

                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafePtr(buffer),
                    (long)buffer.Length * UnsafeUtility.SizeOf<T>());
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private void InitializePredictedInputBuffers(uint startTick)
        {
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveInputBuffer(in _predictedInputHandle, 1, out NativeArray<PredictedInputDTO> predictedInputs))
                    return;

                TryResolveInputBuffer(in _predictedInputAupTargetHandle, 1, out NativeArray<PredictedInputAupTargetDTO> targetAups);
                InitializePredictedInputRingJob initialize = default;
                initialize.PredictedInputs = predictedInputs;
                initialize.TargetAups = targetAups;
                initialize.StartTick = startTick;
                initialize.DefaultFlags = PredictedInputFlags.Local;
                initialize.Execute();
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private bool GenerateMockInputHistoryOwnerCold(uint startTick, uint count, uint seed)
        {
            EnsureDeterministicInputNativeBuffers();
            if (!TryAcquireInputMutationGuard())
                return false;

            try
            {
                if (!TryResolveInputBuffer(in _predictedInputHandle, 1, out NativeArray<PredictedInputDTO> predictedInputs))
                    return false;

                TryResolveInputBuffer(in _predictedInputAupTargetHandle, 1, out NativeArray<PredictedInputAupTargetDTO> targetAups);
                GenerateMockInputHistoryJob mock = default;
                mock.PredictedInputs = predictedInputs;
                mock.TargetAups = targetAups;
                mock.StartTick = startTick;
                mock.Count = math.min(count, (uint)predictedInputs.Length);
                mock.Seed = seed;
                mock.Execute();
                return true;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private void ReleaseInputVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _currentInputDtoHandle);
            ReleaseVaultHandle(vault, ref _inputJournalHandle);
            ReleaseVaultHandle(vault, ref _predictedInputHandle);
            ReleaseVaultHandle(vault, ref _predictedInputAupTargetHandle);
            ReleaseVaultHandle(vault, ref _inputStateBridgeRingHandle);
            ReleaseVaultHandle(vault, ref _buttonMaskWindowHandle);
            ReleaseVaultHandle(vault, ref _inputBlockMaskHandle);
            ReleaseVaultHandle(vault, ref _inputProfileHandle);
            ReleaseVaultHandle(vault, ref _inputTelemetryHandle);
            ReleaseVaultHandle(vault, ref _inputReplaySnapshotHandle);
            ReleaseVaultHandle(vault, ref _inputReplayFrameHandle);
            ReleaseVaultHandle(vault, ref _inputReplayTelemetryHandle);
            ReleaseVaultHandle(vault, ref _hapticCommandDtoHandle);
            ReleaseVaultHandle(vault, ref _xrInputStatesHandle);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref _inputProfileCsvScratchHandle);
#endif
            ReleaseHapticSynthesisVaultHandles(vault);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        // -------------------------------------------------------------------------------------------------
        // Seeds ShinobuInputProfile with the Default* constants WITHOUT depending on the broad owner
        // mutation guard.
        //
        // WHY. Before this split, the only call to InitializeDefaultInputProfile sat inside the
        // TryAcquireInputMutationGuard block of EnsureDeterministicInputNativeBuffers, and that block gets
        // ONE attempt per ready-transition: the method returns at its top whenever
        // _deterministicVaultBuffersReady && ValidateDeterministicInputBuffers(), which is true from the very
        // next call onward. So a guard that happened to be contended on the single frame the buffers became
        // ready left the profile unseeded for the rest of the session with no retry and no message. probe7
        // reports publishGuardFail=1240 of 1240 attempts, i.e. the guard was contended for that whole run.
        //
        // Two independent changes make the seed reachable: the narrow single-buffer mask here, and the
        // SlowTick retry that can re-attempt after the one-shot cold block is behind us. The buffer is also
        // now allocated ClearMemory, so even a total seed failure leaves zeros, and zeros are the value
        // ReadInputProfile already rejects into its Default* fallback - the seed is a correctness nicety for
        // direct readers of the vault copy (the Editor tuner window, the CSV stage), not the safety net.
        // -------------------------------------------------------------------------------------------------
        private bool TryEnsureDefaultInputProfileSeeded()
        {
            if (_inputProfileDefaultsSeeded)
                return true;

            // An enclosing block already holds the broad owner mask, which CONTAINS the profile bit. Asking
            // the vault for the narrow mask here would collide with our own held bit and refuse
            // (TryAcquireMutationGuard, GlobalDataVault.cs:2935), so write under the guard we already own.
            if (_inputMutationGuardDepth > 0)
                return TryWriteDefaultInputProfile();

            IDataVault vault = _dataVault;
            if (vault == null || !IsOwnerThread() || !vault.TryAcquireMutationGuard(InputProfileOnlyMutationGuardMask))
            {
                ReportInputProfileSeedFailureOnce();
                return false;
            }

            try
            {
                return TryWriteDefaultInputProfile();
            }
            finally
            {
                vault.ReleaseMutationGuard(InputProfileOnlyMutationGuardMask);
            }
        }

        private void ReportInputProfileSeedFailureOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Fail loudly, once. Silent failure here is what let an unseeded profile look identical to a
            // seeded one for a whole session.
            if (_inputProfileSeedFailureReported || _dataVault == null)
                return;

            _inputProfileSeedFailureReported = true;
            // COLD ALLOC: one seed-failure string per session - owner: InputDispatcher
            Hecton8.Core.H8Debug.LogError(
                "[H8_INPUTPROFILE] seed=REFUSED ShinobuInputProfile still holds its allocation-time value." +
                " ownerThread=" + _ownerThreadId +
                " callThread=" + Thread.CurrentThread.ManagedThreadId +
                " narrowMask=" + InputProfileOnlyMutationGuardMask +
                " vaultGuardMask=" + _dataVault.ActiveMutationGuardMask +
                " vaultActiveLocks=" + _dataVault.ActiveBurstLockMask +
                " fence=" + _dataVault.IsCompactionFenceActive +
                " buffersReady=" + _deterministicVaultBuffersReady +
                " - the buffer is allocated ClearMemory so the live values are zeros, and ReadInputProfile" +
                " rejects OuterDeadzone==0f into its Default* fallback, so control feel is still correct." +
                " What is NOT correct is any direct reader of the vault copy: the Editor curve/haptics tuner" +
                " and the input_profiles.csv stage will show zeros instead of the defaults.");
#endif
        }

        private bool TryWriteDefaultInputProfile()
        {
            if (!TryResolveInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO> profiles))
                return false;

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

#if UNITY_EDITOR
            lock (_inputProfileCsvStageGate)
            {
                _stagedInputProfileCsv = profile;
                _inputProfileCsvStageVersion = 0;
                _inputProfileCsvAppliedVersion = 0;
            }

            Interlocked.Exchange(ref _inputProfileCsvStageFault, 0);
#endif
            _inputProfileDefaultsSeeded = true;
            return true;
        }

#if UNITY_EDITOR
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

            FileSystemWatcher watcher = TryCreateInputProfileCsvWatcher(projectRoot);
            if (watcher == null)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return;
            }

            _inputProfileCsvWatcher = watcher;
        }

        private void DisposeInputProfileCsvWatcher()
        {
            FileSystemWatcher watcher = _inputProfileCsvWatcher;
            if (watcher == null)
                return;

            _inputProfileCsvWatcher = null;
            StopInputProfileCsvWatcherNoThrow(watcher);
        }

        private FileSystemWatcher TryCreateInputProfileCsvWatcher(string projectRoot)
        {
            try
            {
                FileSystemWatcher watcher = new FileSystemWatcher(projectRoot, "input_profiles.csv");
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                watcher.Changed += HandleInputProfileCsvChanged;
                watcher.Created += HandleInputProfileCsvChanged;
                watcher.Renamed += HandleInputProfileCsvChanged;
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void StopInputProfileCsvWatcherNoThrow(FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
            }
            catch (Exception)
            {
            }

            try
            {
                watcher.Changed -= HandleInputProfileCsvChanged;
                watcher.Created -= HandleInputProfileCsvChanged;
                watcher.Renamed -= HandleInputProfileCsvChanged;
                watcher.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void HandleInputProfileCsvChanged(object sender, FileSystemEventArgs args)
        {
            if (!TryStageInputProfileCsvFromFile())
                Interlocked.Exchange(ref _inputProfileCsvDirty, 1);
        }

        private void ApplyPendingInputProfileCsv()
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
                    Span<byte> readBuffer = stackalloc byte[512];
                    int length = 0;
                    bool overflow = false;
                    while (true)
                    {
                        int read = stream.Read(readBuffer);
                        if (read <= 0)
                            break;

                        for (int i = 0; i < read; i++)
                        {
                            byte c = readBuffer[i];
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
            InputProfileDTO stagedProfile;
            int stagedVersion;
            lock (_inputProfileCsvStageGate)
            {
                stagedVersion = _inputProfileCsvStageVersion;
                if (stagedVersion == _inputProfileCsvAppliedVersion)
                    return false;

                stagedProfile = _stagedInputProfileCsv;
            }

            if (!TryAcquireInputMutationGuard())
                return false;

            bool wroteProfile = false;
            try
            {
                if (!TryResolveInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO> profiles))
                    return false;

                profiles[0] = stagedProfile;
                wroteProfile = true;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }

            if (!wroteProfile)
                return false;

            lock (_inputProfileCsvStageGate)
            {
                _inputProfileCsvAppliedVersion = stagedVersion;
            }

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
#endif

        private bool WriteDeterministicInputBlackBox(
            in InputState state,
            uint currentInputSchemeHash,
            out uint packedAxes,
            out bool recordedFrame)
        {
            packedAxes = 0u;
            recordedFrame = false;
            if (!TryResolveInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out NativeArray<InputTelemetryEntryDTO> telemetry))
                return false;

            int writeIndex = _deterministicBlackBoxWriteIndex;
            int wrappedIndex = writeIndex % InputBlackBoxCapacity;
            packedAxes = PackInputAxes(in state);
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
            recordedFrame = true;

            return (state.Flags & (ushort)InputStateFlags.NonFiniteSanitized) != 0 ||
                   _lastPollingTimeMicroseconds > 500u;
        }

        private static uint PackInputAxes(in InputState state)
        {
            return (uint)(ushort)state.MoveX |
                   ((uint)(ushort)state.MoveY << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquireInputReplaySnapshotGate()
        {
            return Interlocked.CompareExchange(ref _inputReplaySnapshotGate, 1, 0) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseInputReplaySnapshotGate()
        {
            Volatile.Write(ref _inputReplaySnapshotGate, 0);
        }

        private void StageInputReplaySnapshot()
        {
            AutoResetEvent signal = _inputReplaySignal;
            if (signal == null)
                return;

            if (!TryAcquireInputMutationGuard())
                return;

            bool replayGateTaken = false;
            try
            {
                if (!TryResolveInputBuffer(in _inputStateBridgeRingHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputStateRing) ||
                    !TryResolveInputBuffer(in _inputReplaySnapshotHandle, DeterministicInputRingCapacity, out NativeArray<InputState> inputReplaySnapshot))
                    return;

                if (!TryAcquireInputReplaySnapshotGate())
                    return;

                replayGateTaken = true;
                for (int i = 0; i < DeterministicInputRingCapacity; i++)
                    inputReplaySnapshot[i] = inputStateRing[i];

#if HECTON8_MMF_AVAILABLE
                if (_inputReplayPointer != null)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputReplaySnapshot);
                    if (!Hecton8.Core.UnsafeMemoryCopyGuard.SafeCopy(
                            _inputReplayPointer + InputReplayHeaderBytes,
                            InputReplayPayloadBytes,
                            source,
                            InputReplayPayloadBytes))
                    {
                        return;
                    }
                }
#endif
            }
            finally
            {
                if (replayGateTaken)
                    ReleaseInputReplaySnapshotGate();
                ReleaseInputMutationGuard();
            }

            Interlocked.Exchange(ref _inputReplayWritePending, 1);
            if (!SignalInputReplayWriterNoThrow(signal))
                Interlocked.Exchange(ref _inputReplayWritePending, 0);
        }

        private void EnsureInputReplayWriterCold()
        {
            if (!Application.isPlaying || _inputReplayThread != null)
                return;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
                DisposeInputReplaySignalNoThrow();
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
            SignalInputReplayWriterNoThrow(signal);

            Thread thread = _inputReplayThread;
            bool stopped = TryJoinInputReplayThreadNoThrow(thread);

            if (!stopped)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return;
            }

            _inputReplayThread = null;
            DisposeInputReplaySignalNoThrow();
            Interlocked.Exchange(ref _inputReplayWritePending, 0);
            ReleaseInputReplayMap();
        }

        private static bool SignalInputReplayWriterNoThrow(AutoResetEvent signal)
        {
            if (signal == null)
                return false;

            try
            {
                signal.Set();
                return true;
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return false;
            }
        }

        private static bool TryJoinInputReplayThreadNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(2000);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                return false;
            }
        }

        private void DisposeInputReplaySignalNoThrow()
        {
            if (_inputReplaySignal == null)
                return;

            try
            {
                _inputReplaySignal.Dispose();
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                _inputReplaySignal = null;
            }
        }

        private void ReleaseInputReplayMap()
        {
            Volatile.Write(ref _inputReplaySnapshotGate, 0);
#if HECTON8_MMF_AVAILABLE
            if (_inputReplayPointer != null)
            {
                try
                {
                    if (_inputReplayAccessor != null)
                        _inputReplayAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
                catch (Exception)
                {
                    CrashTelemetryBuffer.ReportBlackBoxExportFailure();
                }
                finally
                {
                    _inputReplayPointer = null;
                }
            }

            DisposeInputReplayResourceNoThrow(_inputReplayAccessor);
            _inputReplayAccessor = null;
            DisposeInputReplayResourceNoThrow(_inputReplayMappedFile);
            _inputReplayMappedFile = null;
#endif
            DisposeInputReplayResourceNoThrow(_inputReplayStream);
            _inputReplayStream = null;
        }

        private static void DisposeInputReplayResourceNoThrow(IDisposable resource)
        {
            if (resource == null)
                return;

            try
            {
                resource.Dispose();
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
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
                    bool replayGateTaken = false;
                    try
                    {
                        if (!TryAcquireInputReplaySnapshotGate())
                        {
                            Interlocked.Exchange(ref _inputReplayWritePending, 1);
                            if (!SignalInputReplayWriterNoThrow(signal))
                            {
                                Interlocked.Exchange(ref _inputReplayWritePending, 0);
                                Interlocked.Exchange(ref _inputReplayStopRequested, 1);
                            }
                            Thread.Yield();
                            continue;
                        }

                        replayGateTaken = true;
                        if (_inputReplayPointer == null)
                            continue;

                        accessor?.Flush();
                    }
                    finally
                    {
                        if (replayGateTaken)
                            ReleaseInputReplaySnapshotGate();
                    }
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
            if (!TryReadInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out NativeArray<InputTelemetryEntryDTO>.ReadOnly telemetry))
                return;

            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "deterministicInputBlackBoxDumpPayload";
            try
            {
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<InputTelemetryEntryDTO>();
                void* source = telemetry.GetUnsafeReadOnlyPtr();
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(InputDispatcher),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                if (UnsafeMemoryCopyGuard.SafeCopy(destination, byteCount, source, byteCount))
                    NativeFaultDumpWriter.TryWriteAll(InputDumpRelativePath, payload, byteCount);
            }
            catch (Exception)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(InputDispatcher), dumpPayloadLabel);
            }
        }

        /// <summary>
        /// Returns the read-only OpenXR controller snapshot buffer: index 0 left, index 1 right.
        /// </summary>
        internal NativeArray<XRInputState>.ReadOnly GetXRInputStatesReadOnly()
        {
            return TryReadXRInputStates(out NativeArray<XRInputState>.ReadOnly xrInputStates) ? xrInputStates : default;
        }

        internal bool TryGetXRInputState(byte controllerIndex, out XRInputState state)
        {
            state = default;
            if (!TryReadXRInputStates(out NativeArray<XRInputState>.ReadOnly xrInputStates) ||
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
        /// <summary>
        /// Adds a discrete action token to the fixed 10-frame input buffer.
        /// </summary>
        /// <param name="action">Buffered action token.</param>
        public void BufferAction(PlayerBufferedAction action)
        {
            if (action == PlayerBufferedAction.None)
                return;

            _bufferedActions[_bufferWriteIndex].Action = action;
            _bufferedActions[_bufferWriteIndex].Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
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
            int currentFrame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);

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

            if (!TryReadInputBuffer(in _buttonMaskWindowHandle, ButtonMaskWindowCapacity, out NativeArray<uint>.ReadOnly window))
                return false;

            int frameCount = math.clamp(frames, 1, ButtonMaskWindowCapacity);
            for (int offset = 0; offset < frameCount; offset++)
            {
                int index = _buttonMaskWindowWriteIndex - 1 - offset;
                if (index < 0)
                    index += ButtonMaskWindowCapacity;

                if ((window[index] & buttonBit) == 0u)
                    continue;

                return true;
            }

            return false;
        }

        public bool TryGetCurrentInputStateDto(out InputStateDTO state)
        {
            state = default;
            if (!TryReadInputBuffer(in _currentInputDtoHandle, 1, out NativeArray<InputStateDTO>.ReadOnly currentInputDto))
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
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveInputBuffer(in _inputBlockMaskHandle, 1, out NativeArray<uint> inputBlockMask))
                    return;

                inputBlockMask[0] = mask;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        /// <inheritdoc />
        public void SwitchToPlayerInput()
        {
            // L19: hot-swap / first-registration can leave _nativeInputManager null while
            // GlobalRegistry.NativeInputRuntime still holds the bootstrap InputManager. A silent
            // no-op here left IsPlayerInputEnabled false for the whole route even though the driver
            // called SwitchToPlayerInput every settle/swim tick (EnsureGameplayLocomotionInputReady).
            TryEnsureNativeInputBound();
            if (_nativeInputManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DiagWarnNativeInputMissingOnce("SwitchToPlayerInput");
#endif
                return;
            }

            _nativeInputManager.SwitchToPlayerInput();
            BeginLookHotSwapBlend();
        }

        /// <inheritdoc />
        public void SwitchToUIInput()
        {
            TryEnsureNativeInputBound();
            if (_nativeInputManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DiagWarnNativeInputMissingOnce("SwitchToUIInput");
#endif
                return;
            }

            _nativeInputManager.SwitchToUIInput();
            _lookBlendActive = false;
            _pendingLookDelta = Vector2.zero;
        }

        /// <summary>
        /// Rebinds <see cref="_nativeInputManager"/> from <see cref="GlobalRegistry.NativeInputRuntime"/>
        /// when the local slot is empty. Product path for headless/menu transitions that drop the
        /// bootstrap-owned native owner without notifying this dispatcher.
        /// </summary>
        private void TryEnsureNativeInputBound()
        {
            if (_nativeInputManager != null)
                return;

            INativeInputManagerRuntime native = GlobalRegistry.NativeInputRuntime;
            if (native == null || ReferenceEquals(native, this))
                return;

            BindNativeInputManager(native);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _diagNativeInputMissingWarned;

        private void DiagWarnNativeInputMissingOnce(string caller)
        {
            if (_diagNativeInputMissingWarned)
                return;

            _diagNativeInputMissingWarned = true;
            // COLD ALLOC: one native-missing string per session - owner: InputDispatcher
            Hecton8.Core.H8Debug.LogWarning(
                "[H8_INPUTNATIVE] caller=" + caller +
                " native=null registryNativeNull=" + (GlobalRegistry.NativeInputRuntime == null) +
                " - SwitchToPlayer/UI is a no-op until GlobalRegistry.NativeInputRuntime is bound." +
                " hop2 consumers stay gated unless an automation override is applied.");
        }
#endif


        private void EnsureInputBinding()
        {
            if (_nativeInputManager == null || _subscribedToNativeInput)
                return;

            SubscribeToNativeInput();
        }

        private void EnsureHapticDeviceBinding()
        {
            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
            SubscribeToDeviceChanges();
            RefreshCachedGamepadBinding();
            if (HectonXRRuntimeState.IsXRActive)
                RefreshCachedXRControllerBindings();
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
            INativeInputManagerRuntime inputManager = _nativeInputManager;
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
                        RefreshCachedGamepadBinding();
                    }
                    break;
            }
        }

        private void RefreshCachedGamepadBinding()
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
                    RefreshCachedXRControllerBindings();
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
                    RefreshCachedXRControllerBindings();
                    break;
            }
        }

        private void PublishDeviceLostPauseSignal(byte flags)
        {
            SimulationPauseSignal signal = default;
            signal.SourceHash = DeviceLostSignalSourceHash;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Sequence = ++_deviceLostPauseSequence;
            if (signal.Sequence == 0)
                signal.Sequence = ++_deviceLostPauseSequence;
            signal.Paused = 1;
            signal.Flags = flags;
            signal.RestoreScalar = 1f;
            SimulationSignalRoute.TryQueuePause(in signal);
        }

        private void ClearCachedXRControllers()
        {
            ClearLeftXRController();
            ClearRightXRController();
            _nextXRDeviceRescanFrame = 0;
        }

        private void RefreshCachedXRControllerBindings()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            // L18: sticky alone is insufficient (L15 HPM Fixed parity). ClearAllLanes / soft-reset
            // can empty the Core late-frame lane while _registeredLateFrame stays true, so this
            // owner never re-TryRegisters. PreSim still advances (direct IInputDeterminismService
            // call) while lateFrameTick freezes and hop2 starves (Fixed also cleared until healed).
            // Do NOT Unregister+Register thrash — verify lane membership, clear sticky when missing.
            if (_registeredLateFrame &&
                !SystemDispatcher.GetLateFrameLane(PriorityLayer.Core).Contains(this))
                _registeredLateFrame = false;
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);

            if (_registeredSlowTick &&
                !SystemDispatcher.GetSlowLane(PriorityLayer.Core).Contains(this))
                _registeredSlowTick = false;
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            TryRegisterHapticSynthesisPostSimulation();
        }

        private void TryUnregisterFromDispatcher()
        {
            TryUnregisterFromDispatcher(clearPendingHapticOutput: true);
        }

        private void TryUnregisterFromDispatcher(bool clearPendingHapticOutput)
        {
            TryUnregisterHapticSynthesisPostSimulation();
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = false;
                if (clearPendingHapticOutput)
                    _pendingHapticOutput = false;
            }
        }

        /// <summary>
        /// Moves this dispatcher under the bootstrap persistent root so a scene transition cannot destroy
        /// it and silently empty <see cref="GlobalRegistry"/>'s Input slot.
        ///
        /// WHY THIS IS NOT DEFENSIVE PADDING. GameBootstrapper.EnsureInputDispatcherRegistered
        /// (GameBootstrapper.cs:6309-6327) is the ONLY producer of this component in the project - the
        /// string "InputDispatcher" occurs in zero scenes and zero prefabs - and it does
        /// <c>new GameObject("[InputDispatcher]")</c> in whatever scene happens to be active, then calls
        /// BindNativeInputManager and InitializeService. Its two sibling factories,
        /// EnsureSystemDispatcherRegistered (:5877) and EnsureRenderDispatcherRegistered (:6063), both call
        /// PersistRuntimeService on the line before InitializeService. The input factory does not, and the
        /// authored InputManager next to it is persisted explicitly (:3585). That single omission is the
        /// whole defect.
        ///
        /// The consequence, and it is measured rather than reasoned: the InputDispatcher bootstrap node
        /// runs exactly once, in BootstrapPhase.Player, while 00_BOOTSTRAP is still the active scene, and
        /// it passed its readiness gate there (Logs/omega_route16.log:5842-5884, node readiness reads
        /// GlobalRegistry.RegisteredInput). The route then loads 01_MAIN_MENU and 02_HECTON_WORLD and ends
        /// with one scene loaded, so the object created in 00_BOOTSTRAP is gone. OnDestroy runs
        /// ShutdownServiceState -> TryUnregisterInputService, the Input slot returns to null, and nothing
        /// refills it because the Player phase does not run a second time. By gameplay the headless route
        /// reported "no drivable player ... inputService=False" - a NULL service, not a disabled one, which
        /// is why every consumer's leading null check (HectonPlayerInputHandler.cs:37,
        /// HectonPlayerMovement.cs:7992, PlayerToolManager.cs:414) short-circuited before
        /// IsPlayerInputEnabled was ever asked.
        ///
        /// Identical call and identical purpose to WorldStateManager.cs:85. PersistRuntimeService is inert
        /// outside play mode and inert when no GameBootstrapper owns startup, and it returns after one
        /// transform compare once the parent already matches, so this cold entry point pays nothing on
        /// re-entry.
        /// </summary>
        private void TryPersistRuntimeOwner()
        {
            Hecton8.Bootstrap.GameBootstrapper.PersistRuntimeService(this);
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
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindCachedDataVault(currentService as IDataVault, _dataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                BindNativeInputManager(currentService as INativeInputManagerRuntime);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterFromDispatcher(clearPendingHapticOutput: false);
                if (currentService != null && isActiveAndEnabled && _isInitialized)
                    TryRegisterToDispatcher();

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindCachedDataVault(currentService as IDataVault, previousService as IDataVault ?? _dataVault);
                return;
            }
        }

        private void RefreshCachedDataVaultCold()
        {
            if (_dataVault != null)
                return;

            _dataVault = GlobalRegistry.DataVault;
        }

        private void RebindCachedDataVault(IDataVault dataVault, IDataVault releaseVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            ReleaseInputVaultHandles(releaseVault);
            _dataVault = dataVault;
            _deterministicVaultBuffersReady = false;
            _deterministicVaultBuffersCleared = false;
            _xrVaultBuffersCleared = false;

            if (dataVault != null && Application.isPlaying && _isInitialized)
            {
                EnsureDeterministicInputNativeBuffers();
                TryRegisterHapticSynthesisPostSimulation();
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
            RefreshXRNativeBufferState(allowColdAcquire: false);

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastCapturedFrame == currentFrame)
            {
                DiagRecordCaptureSkippedByFrameGuard();
                return;
            }

            _lastCapturedFrame = currentFrame;
            DiagRecordCaptureRan();
            long pollStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            PlayerInputState state = default;
            uint actionBits = 0u;
            INativeInputManagerRuntime inputManager = _nativeInputManager;
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
                        lookDelta = AdvanceLookHotSwapBlend(lookDelta, deltaTime);

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
                actionBits |= CaptureXRToolActionBitsAndPublishSignal(currentFrame);
                StageXRLookAtSpatialProbe();
            }
            else
            {
                PublishXRToolTriggerReleaseIfNeeded(currentFrame);
                ClearXRRuntimeFrameStateIfActive();
            }

            state.ActionsBitmask = actionBits;
            // Freshness must be judged on the SAME clock the producers stamp with. Every synthetic-input
            // producer in the project publishes signal.Frame = SystemDispatcher.CurrentFrameId, which is
            // TimeSliceScheduler.CurrentFrameId - a counter running since boot. `currentFrame` here is
            // SystemDispatcher.CurrentFrameIndex, which resolves to the dispatcher instance's own
            // _dispatcherFrameSequence (SystemDispatcher.cs:2697-2703), reset to 0 on init at :2060. The two
            // counters are independent, so the dispatcher value is far SMALLER, and
            // TryConsumeLatestInputOverride's `if (frame < signal.Frame) return false` guard fired on every
            // poll - never consuming, never clearing. Measured effect in Logs/omega_route28.log: 124 overrides
            // published, movementIntent01max=0.000, Swim row FAIL with the input path fully open.
            bool automationOverrideApplied = ApplyAutomationOverride(ref state, Hecton8.Core.SystemDispatcher.CurrentFrameId);
            // L19d: latch sticky once any override lands this session. Assigning only the
            // per-capture bool cleared hop2 on every subsequent CaptureState where consume
            // rejected (overrideRejected stays large by design). HPM FixedTick then saw
            // IsPlayerInputEnabled false and never called GetState — L19 LIVE readHop=1 only
            // despite overrideApplied rising and currentStateMove=(0,1). Cleared only in
            // full input reset path below.
            if (automationOverrideApplied)
                _lastAutomationOverrideApplied = true;
            DiagRecordOverrideOutcome(automationOverrideApplied, state.MoveDelta);

            if (automationOverrideApplied)
                _lastDeliveredLookDelta = state.LookDelta;

            uint activeInputBlockMask = ReadInputBlockMask();
            ApplyInputBlockMask(ref state, activeInputBlockMask);
            DiagRecordPostBlockMask(activeInputBlockMask, state.MoveDelta);
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

            if (!TryReadInputBuffer(in _inputProfileHandle, 1, out NativeArray<InputProfileDTO>.ReadOnly profiles))
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
            if (!TryReadInputBuffer(in _inputBlockMaskHandle, 1, out NativeArray<uint>.ReadOnly inputBlockMask))
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

            float magnitude = FastInputLengthFromSq(magnitudeSq, 0.00000001f);
            float normalized = Hecton8.PureLogic.Systems.AnalogStickDeadzoneNormalizer.Normalize(magnitude, inner, outer);
            float exponent = math.clamp(profile.MoveExponent, 0.25f, 4f);
            float curved = MathLodApproximation.ApproxPow01Curve(normalized, exponent);
            float scale = curved * math.rcp(math.max(magnitude, 0.0001f));
            return rawAxis * scale;
        }

        private float2 ResolveAupAgnosticLookDelta(float2 rawLookDelta, in InputProfileDTO profile)
        {
            if (!math.all(math.isfinite(rawLookDelta)))
                return float2.zero;

            float viewportHeight = _viewportHeightSnapshot;
            float magnitude = FastInputLengthFromSq(math.lengthsq(rawLookDelta), 0f);
            float sensitivity = math.clamp(profile.MouseSensitivity, 0.01f, 20f);
            float acceleration = 1f + (math.min(magnitude, 64f) * math.clamp(profile.MouseAcceleration, 0f, 8f));
            return rawLookDelta * (sensitivity * acceleration * math.rcp(viewportHeight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastInputLengthFromSq(float lengthSq, float minLengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            float safeLengthSq = math.max(lengthSq, minLengthSq);
            return safeLengthSq > 0f ? safeLengthSq * math.rsqrt(safeLengthSq) : 0f;
        }

        private void RefreshViewportSnapshotSlowSample()
        {
            _viewportHeightSnapshot = math.max(1f, Screen.height);
        }

        private uint ResolveCurrentInputSchemeHash()
        {
            if (HectonXRRuntimeState.IsXRActive &&
                ((_cachedLeftXRController != null && _cachedLeftXRController.added) ||
                 (_cachedRightXRController != null && _cachedRightXRController.added)))
            {
                return InputSchemeHashXRTouch;
            }

            INativeInputManagerRuntime inputManager = _nativeInputManager;
            if (inputManager != null)
            {
                switch (inputManager.CurrentDisplayStyleCode)
                {
                    case NativeInputDisplayStyle.SteamDeck:
                        return InputSchemeHashSteamDeck;
                    case NativeInputDisplayStyle.Gamepad:
                        return InputSchemeHashGamepad;
                    case NativeInputDisplayStyle.XRTouch:
                        return InputSchemeHashXRTouch;
                    case NativeInputDisplayStyle.KeyboardMouse:
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

        private void RefreshXRNativeBufferState(bool allowColdAcquire)
        {
            if (HectonXRRuntimeState.IsXRActive)
            {
                if (allowColdAcquire)
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

        private void SubscribeToXRActiveChanged()
        {
            if (_subscribedToXRActiveChanged)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            _subscribedToXRActiveChanged = true;
        }

        private void UnsubscribeFromXRActiveChanged()
        {
            if (!_subscribedToXRActiveChanged)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _subscribedToXRActiveChanged = false;
        }

        private void HandleXRActiveChanged(bool isActive)
        {
            if (isActive)
            {
                RefreshXRNativeBufferState(allowColdAcquire: true);
                RefreshCachedXRControllerBindings();
                _nextXRDeviceRescanFrame = 0;
                return;
            }

            ResetXRHaptics();
            RefreshXRNativeBufferState(allowColdAcquire: false);
        }

        private bool HasXRRuntimeStateToClear()
        {
            return _xrInputStatesHandle.BufferID != 0u ||
                   (_xrRuntimeFlags & XRRuntimeFlagsAny) != 0u ||
                   _lastXRLookAtProbeFrame >= 0 ||
                   _lastXRLookAtHitFrame >= 0 ||
                   _cachedLeftXRController != null ||
                   _cachedRightXRController != null ||
                   _appliedLeftXRHapticAmplitude > HapticMotorWriteEpsilon ||
                   _appliedRightXRHapticAmplitude > HapticMotorWriteEpsilon;
        }

        private void EnsureXRNativeBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            bool statesReady = OpenOrAcquireInputBufferForOwnerRoute(
                ref _xrInputStatesHandle,
                BufferID.ShinobuInputXRInputStates,
                XRInputStateCapacity,
                NativeArrayOptions.UninitializedMemory,
                out _);
            if (!statesReady)
                return;

            if (_xrVaultBuffersCleared)
                return;

            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                ClearVaultBuffer(ref _xrInputStatesHandle);
                _xrVaultBuffersCleared = true;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private bool TryResolveXRInputStates(out NativeArray<XRInputState> states)
        {
            return TryResolveInputBuffer(in _xrInputStatesHandle, XRInputStateCapacity, out states);
        }

        private bool TryReadXRInputStates(out NativeArray<XRInputState>.ReadOnly states)
        {
            return TryReadInputBuffer(in _xrInputStatesHandle, XRInputStateCapacity, out states);
        }

        private void DisposeXRNativeBuffers(JobHandle dependency)
        {
            if (TryAcquireInputMutationGuard())
            {
                try
                {
                    ClearVaultBuffer(ref _xrInputStatesHandle);
                }
                finally
                {
                    ReleaseInputMutationGuard();
                }
            }

            ReleaseVaultHandle(_dataVault, ref _xrInputStatesHandle);
            _xrVaultBuffersCleared = false;
        }

        private void RefreshXRInputSnapshot()
        {
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates))
                    return;

                if (!HectonXRRuntimeState.IsXRActive)
                {
                    ClearXRInputSnapshotIfActive();
                    return;
                }

                RefreshCachedXRControllerBindings();
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
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private uint CaptureXRToolActionBitsAndPublishSignal(int frame)
        {
            if (!TryReadXRInputStates(out NativeArray<XRInputState>.ReadOnly xrInputStates))
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
            SignalBus<ToolTriggerSignal>.TryPushTracked(in signal, ref s_x001InputDispatcherSignalPushDropCount);

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
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveXRInputStates(out NativeArray<XRInputState> xrInputStates))
                    return;

                if (!forceWrite && (_xrRuntimeFlags & XRRuntimeFlagInputSnapshotActive) == 0u)
                    return;

                for (int i = 0; i < xrInputStates.Length; i++)
                    xrInputStates[i] = default;

                _xrRuntimeFlags &= ~XRRuntimeFlagInputSnapshotActive;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
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
            state.Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
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

        private void StageXRLookAtSpatialProbe()
        {
            if (!HectonXRRuntimeState.IsXRActive)
            {
                ClearXRLookAtSpatialProbe();
                return;
            }

            Transform viewTransform = ResolveLookAtViewTransform();
            if (viewTransform == null)
            {
                ClearXRLookAtSpatialProbe();
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
                ClearXRLookAtSpatialProbe(forceWrite: true);
                return;
            }

            if (TryReuseXRLookAtHit(in originAup, in direction3))
                return;

            if (!originAup.TryToRuntimeFloat3(out float3 probeOrigin3))
            {
                ClearXRLookAtSpatialProbe(forceWrite: true);
                return;
            }

            Vector3 probeOrigin = new Vector3(probeOrigin3.x, probeOrigin3.y, probeOrigin3.z);
            InteractableRegistry.EnsureSceneRegistryCold();
            Ray probe = new Ray(probeOrigin, direction);
            if (InteractableRegistry.TryResolveSpatialTarget(
                    in probe,
                    XRLookAtSelectionDistanceMeters,
                    HectonLayerMasks.InteractableLayerMask | HectonLayerMasks.StrictInteractionLayerMask,
                    QueryTriggerInteraction.Ignore,
                    out InteractableRegistry.SpatialHit spatialHit))
            {
                _lastXRLookAtSpatialHit = spatialHit;
                _lastXRLookAtHitFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                _lastXRLookAtProbeFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                _lastXRLookAtRayOriginAup = originAup;
                _lastXRLookAtRayOriginRuntimePosition = probeOrigin;
                _lastXRLookAtRayDirection = direction;
                _xrRuntimeFlags |= XRRuntimeFlagLookAtProbeActive;
                if (XRRuntimeAup48.TryOffsetLocal(in originAup, spatialHit.Point - probeOrigin, out XRRuntimeAup48 hitPointAup))
                    _lastXRLookAtHitPointAup = hitPointAup;
                else
                    _lastXRLookAtHitPointAup = default;
                return;
            }

            _lastXRLookAtSpatialHit = default;
            _lastXRLookAtHitFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _lastXRLookAtProbeFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _lastXRLookAtRayOriginAup = originAup;
            _lastXRLookAtRayOriginRuntimePosition = probeOrigin;
            _lastXRLookAtRayDirection = direction;
            _lastXRLookAtHitPointAup = default;
            _xrRuntimeFlags |= XRRuntimeFlagLookAtProbeActive;
        }

        private void ClearXRLookAtSpatialProbe(bool forceWrite = false)
        {
            if (!forceWrite && (_xrRuntimeFlags & XRRuntimeFlagLookAtProbeActive) == 0u)
                return;

            _lastXRLookAtSpatialHit = default;
            _lastXRLookAtHitFrame = -1;
            _lastXRLookAtRayOriginAup = default;
            _lastXRLookAtRayOriginRuntimePosition = Vector3.zero;
            _lastXRLookAtRayDirection = Vector3.forward;
            _lastXRLookAtHitPointAup = default;
            _lastXRLookAtProbeFrame = -1;
            _xrRuntimeFlags &= ~XRRuntimeFlagLookAtProbeActive;
        }

        private void ClearXRRuntimeFrameStateIfActive()
        {
            if (_xrInputStatesHandle.BufferID == 0u &&
                (_xrRuntimeFlags & XRRuntimeFlagsAny) == 0u &&
                _lastXRLookAtProbeFrame < 0 &&
                _lastXRLookAtHitFrame < 0)
            {
                return;
            }

            ClearXRInputSnapshotIfActive(forceWrite: true);
            ClearXRLookAtSpatialProbe(forceWrite: true);

            _lastXRLookAtSpatialHit = default;
            _lastXRLookAtHitFrame = -1;
            _lastXRLookAtRayOriginAup = default;
            _lastXRLookAtRayOriginRuntimePosition = Vector3.zero;
            _lastXRLookAtRayDirection = Vector3.forward;
            _lastXRLookAtHitPointAup = default;
            _lastXRLookAtProbeFrame = -1;
            _xrRuntimeFlags = 0u;
        }

        private bool TryReuseXRLookAtHit(in XRRuntimeAup48 originAup, in float3 direction)
        {
            if (_lastXRLookAtProbeFrame < 0)
                return false;

            if (Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _lastXRLookAtProbeFrame > XRLookAtReuseMaxFrames)
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

            if (!_lastXRLookAtSpatialHit.HasHit)
            {
                _lastXRLookAtHitFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

            _lastXRLookAtHitFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

        private Vector2 AdvanceLookHotSwapBlend(Vector2 targetLookDelta, float deltaTime)
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
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Sequence = unchecked(++_playerInputSignalSequence);
            signal.Command = command;
            signal.Flags = 0;
            SignalBus<PlayerInputSignal>.TryPushTracked(in signal, ref s_x001InputDispatcherSignalPushDropCount);
        }

        private void HandleSprintPressed()
        {
            InputLatencyTracker.MarkInputCaptured();
            _latchedActionBits |= (uint)PlayerInputAction.Sprint;
        }

        private void DrainToolHaptics(float deltaTime)
        {
            uint schemeHash = _currentInputSchemeHash != 0u ? _currentInputSchemeHash : ResolveCurrentInputSchemeHash();
            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.1f)
                : (float)StandardInputTickIntervalSeconds;
            if (ToolHapticsRuntime.PowerSaveMuteActive)
            {
                DrainSuppressedHapticRequests();
                DrainSuppressedHapticPulses();
                ClearVaultBuffer(ref _hapticCommandDtoHandle);
                _lastHapticCommandsActive = 0;
                _hapticDispatchAccumulator = 0f;
                QueueHapticOutput(schemeHash, 0f, 0f);
                return;
            }

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

            if (!IsHapticSynthesisDispatcherRouteRegistered())
                QueueSynthesizedHapticCommand(safeDeltaTime, in profile, schemeHash);

            while (SignalBus<HapticRequest>.TryConsumeFrame(out HapticRequest request))
            {
                if (schemeHash == InputSchemeHashKeyboardMouse)
                    continue;

                InsertHapticRequestCommand(in request);
            }

            while (SignalBus<HapticPulseSignal>.TryConsumeFrame(out HapticPulseSignal pulse))
            {
                if (schemeHash == InputSchemeHashKeyboardMouse)
                    continue;

                InsertHapticPulseCommand(in pulse);
            }

            if (schemeHash == InputSchemeHashKeyboardMouse)
            {
                _lastHapticCommandsActive = 0;
                QueueHapticOutput(schemeHash, 0f, 0f);
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
                QueueHapticOutput(schemeHash, lowMotor, highMotor);
                return;
            }

            QueueHapticOutput(schemeHash, lowMotor, highMotor);
        }

        private static void DrainSuppressedHapticRequests()
        {
            while (SignalBus<HapticRequest>.TryConsumeFrame(out _))
            {
            }
        }

        private static void DrainSuppressedHapticPulses()
        {
            while (SignalBus<HapticPulseSignal>.TryConsumeFrame(out _))
            {
            }
        }

        private void QueueHapticOutput(uint schemeHash, float lowMotor, float highMotor)
        {
            _pendingHapticSchemeHash = schemeHash;
            _pendingHapticLowMotor = ClampFinite01(lowMotor);
            _pendingHapticHighMotor = ClampFinite01(highMotor);
            _pendingHapticOutput = true;
        }

        private void FlushPendingHapticOutput()
        {
            if (!_pendingHapticOutput)
                return;

            _pendingHapticOutput = false;
            uint schemeHash = _pendingHapticSchemeHash;
            float lowMotor = _pendingHapticLowMotor;
            float highMotor = _pendingHapticHighMotor;
            _pendingHapticLowMotor = 0f;
            _pendingHapticHighMotor = 0f;

            if (schemeHash == InputSchemeHashKeyboardMouse)
            {
                ApplyGamepadHaptics(0f, 0f);
                ResetXRHaptics();
                return;
            }

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

            byte priority = ResolveHapticRequestPriority(in request);
            byte blendMode = ResolveHapticRequestBlendMode(in request);
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
            InsertHapticCommandDto(
                intensity,
                highContribution,
                decayRate,
                HapticLowMotorMask | HapticHighMotorMask,
                ResolveHapticRequestPriority(in request),
                ResolveHapticRequestBlendMode(in request));
        }

        private void InsertHapticPulseCommand(in HapticPulseSignal pulse)
        {
            float low = ClampFinite01(pulse.LowFrequencyMotor01);
            float high = ClampFinite01(pulse.HighFrequencyMotor01);
            if ((low <= HapticMotorWriteEpsilon && high <= HapticMotorWriteEpsilon) ||
                pulse.DurationSeconds <= 0f)
            {
                return;
            }

            float decayRate = 1f / math.max(pulse.DurationSeconds, 0.02f);
            InsertHapticCommandDto(
                low,
                high,
                decayRate,
                HapticLowMotorMask | HapticHighMotorMask,
                ResolveHapticPulsePriority(pulse.PriorityFlags),
                ResolveHapticPulseBlendMode(pulse.PriorityFlags));
        }

        private void InsertHapticCommandDto(float lowFreqIntensity, float highFreqIntensity, float decayRate, uint motorMask)
        {
            InsertHapticCommandDto(
                lowFreqIntensity,
                highFreqIntensity,
                decayRate,
                motorMask,
                HapticPriorityTool,
                HapticBlendAdditive);
        }

        private void InsertHapticCommandDto(float lowFreqIntensity, float highFreqIntensity, float decayRate, uint motorMask, byte priority, byte blendMode)
        {
            if (!TryAcquireInputMutationGuard())
                return;

            try
            {
                if (!TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out NativeArray<HapticCommandDTO> commands))
                    return;

                HapticCommandDTO command = default;
                command.LowFreqIntensity = ClampFinite01(lowFreqIntensity);
                command.HighFreqIntensity = ClampFinite01(highFreqIntensity);
                command.DecayRate = math.clamp(math.isfinite(decayRate) ? decayRate : 1f, 0.01f, 64f);
                command.MotorMask = PackHapticCommandMotorMask(motorMask, priority, blendMode);

                int weakestIndex = 0;
                float weakestMagnitude = float.MaxValue;
                byte weakestPriority = byte.MaxValue;
                float commandMagnitude = math.max(command.LowFreqIntensity, command.HighFreqIntensity);
                for (int i = 0; i < commands.Length; i++)
                {
                    HapticCommandDTO existing = commands[i];
                    float magnitude = math.max(existing.LowFreqIntensity, existing.HighFreqIntensity);
                    if (magnitude <= HapticMotorWriteEpsilon)
                    {
                        commands[i] = command;
                        return;
                    }

                    byte existingPriority = ExtractHapticCommandPriority(existing.MotorMask);
                    if (existingPriority > weakestPriority)
                        continue;

                    if (existingPriority == weakestPriority && magnitude >= weakestMagnitude)
                        continue;

                    weakestMagnitude = magnitude;
                    weakestPriority = existingPriority;
                    weakestIndex = i;
                }

                if (priority < weakestPriority)
                    return;

                if (priority == weakestPriority && commandMagnitude <= weakestMagnitude)
                    return;

                commands[weakestIndex] = command;
            }
            finally
            {
                ReleaseInputMutationGuard();
            }
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
            if (!TryAcquireInputMutationGuard())
                return 0;

            try
            {
                if (!TryResolveInputBuffer(in _hapticCommandDtoHandle, HapticCommandDtoCapacity, out NativeArray<HapticCommandDTO> commands))
                    return 0;

                float safeDeltaTime = math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.1f);
                int activeCount = 0;
                for (int i = 0; i < commands.Length; i++)
                {
                    HapticCommandDTO command = commands[i];
                    uint motorMask = ExtractHapticCommandMotorMask(command.MotorMask);
                    byte priority = ExtractHapticCommandPriority(command.MotorMask);
                    byte blendMode = ExtractHapticCommandBlendMode(command.MotorMask);
                    float low = (motorMask & HapticLowMotorMask) != 0u ? ClampFinite01(command.LowFreqIntensity) : 0f;
                    float high = (motorMask & HapticHighMotorMask) != 0u ? ClampFinite01(command.HighFreqIntensity) : 0f;
                    if (low <= HapticMotorWriteEpsilon && high <= HapticMotorWriteEpsilon)
                    {
                        commands[i] = default;
                        continue;
                    }

                    ApplyHapticContribution(low, priority, blendMode, ref lowMotor, ref lowPriority, ref hasLowPriority);
                    ApplyHapticContribution(high, priority, blendMode, ref highMotor, ref highPriority, ref hasHighPriority);

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
            finally
            {
                ReleaseInputMutationGuard();
            }
        }

        private static float ResolveHapticDecayFactor(float decayRate, float deltaTime)
        {
            float x = math.min(math.max(0f, decayRate) * math.max(0f, deltaTime), 3f);
            float x2 = x * x;
            return 1f / math.max(1f + x + (0.48f * x2) + (0.235f * x2 * x), 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackHapticCommandMotorMask(uint motorMask, byte priority, byte blendMode)
        {
            byte clampedPriority = priority > HapticPriorityCritical ? HapticPriorityCritical : priority;
            byte clampedBlend = blendMode > HapticBlendMax ? HapticBlendMax : blendMode;
            uint packedPriority = (uint)(clampedPriority + 1);
            uint packedBlend = (uint)(clampedBlend + 1);
            return (motorMask & HapticCommandMotorMaskBits) |
                   (packedPriority << HapticCommandPriorityShift) |
                   (packedBlend << HapticCommandBlendShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ExtractHapticCommandMotorMask(uint packedMotorMask)
        {
            return packedMotorMask & HapticCommandMotorMaskBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ExtractHapticCommandPriority(uint packedMotorMask)
        {
            uint encoded = (packedMotorMask >> HapticCommandPriorityShift) & HapticCommandNibbleMask;
            uint priority = encoded == 0u ? HapticPriorityTool : encoded - 1u;
            return priority > HapticPriorityCritical ? HapticPriorityCritical : (byte)priority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ExtractHapticCommandBlendMode(uint packedMotorMask)
        {
            uint encoded = (packedMotorMask >> HapticCommandBlendShift) & HapticCommandNibbleMask;
            uint blendMode = encoded == 0u ? HapticBlendAdditive : encoded - 1u;
            return blendMode > HapticBlendMax ? HapticBlendMax : (byte)blendMode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveHapticRequestPriority(in HapticRequest request)
        {
            if ((request.Flags & HapticRequest.FlagCrush) != 0 ||
                request.Channel == HapticRequest.ChannelVehicleCritical ||
                request.Channel == HapticRequest.ChannelCrush)
            {
                return HapticPriorityCritical;
            }

            if (request.Channel == HapticRequest.ChannelCollision)
                return HapticPriorityCollision;

            if ((request.Flags & HapticRequest.FlagMicroVibration) != 0 ||
                request.Channel == HapticRequest.ChannelMicroVibration)
            {
                return HapticPriorityMicro;
            }

            return HapticPriorityTool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveHapticRequestBlendMode(in HapticRequest request)
        {
            if ((request.Flags & HapticRequest.FlagCrush) != 0 ||
                request.Channel == HapticRequest.ChannelVehicleCritical ||
                request.Channel == HapticRequest.ChannelCrush ||
                request.Channel == HapticRequest.ChannelCollision ||
                request.Channel == HapticRequest.ChannelLightThud ||
                (request.Flags & HapticRequest.FlagLightThud) != 0)
            {
                return HapticBlendMax;
            }

            return HapticBlendAdditive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveHapticPulsePriority(uint priorityFlags)
        {
            uint priority = HapticPulseSignal.ExtractPriorityFlags(priorityFlags);
            if ((priority & HapticPulseSignal.PriorityExplosion) != 0u)
                return HapticPriorityCritical;
            if ((priority & HapticPulseSignal.PriorityCollision) != 0u)
                return HapticPriorityCollision;
            if ((priority & HapticPulseSignal.PriorityTool) != 0u)
                return HapticPriorityTool;
            return HapticPriorityMicro;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveHapticPulseBlendMode(uint priorityFlags)
        {
            uint priority = HapticPulseSignal.ExtractPriorityFlags(priorityFlags);
            return (priority & (HapticPulseSignal.PriorityExplosion | HapticPulseSignal.PriorityCollision)) != 0u
                ? HapticBlendMax
                : HapticBlendAdditive;
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

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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
            _lastPreSimulationInputFrame = -1;
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
            _lastXRLookAtSpatialHit = default;
            _lastXRLookAtHitFrame = -1;
            _lastXRLookAtRayOriginAup = default;
            _lastXRLookAtRayOriginRuntimePosition = Vector3.zero;
            _lastXRLookAtRayDirection = Vector3.forward;
            _lastXRLookAtHitPointAup = default;
            _lastXRLookAtProbeFrame = -1;
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
                ClearVaultBuffer(ref _inputReplayFrameHandle);
                ClearVaultBuffer(ref _inputReplayTelemetryHandle);
                ClearVaultBuffer(ref _hapticCommandDtoHandle);
#if UNITY_EDITOR
                ClearVaultBuffer(ref _inputProfileCsvScratchHandle);
#endif
            }

            ClearXRInputSnapshotIfActive(forceWrite: true);
            ClearXRLookAtSpatialProbe(forceWrite: true);
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
                timestamp = SystemDispatcher.CurrentUnscaledTimeSeconds;

            int frameIndex = SystemDispatcher.CurrentFrameIndex;
            if (_pendingInputTimestamp <= 0d || frameIndex != _pendingInputFrame)
            {
                _pendingInputTimestamp = timestamp;
                _pendingInputFrame = frameIndex;
            }
        }

        public static void MarkRenderCompleted()
        {
            double inputTimestamp = _pendingInputTimestamp;
            if (inputTimestamp <= 0d)
                return;

            double renderTimestamp = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (renderTimestamp <= 0d)
                renderTimestamp = SystemDispatcher.CurrentUnscaledTimeSeconds;

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
            double renderTimestamp = SystemDispatcher.CurrentUnscaledTimeSeconds;
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
            if (UnityEngine.Application.isBatchMode)
            {
                await System.Threading.Tasks.Task.Yield();
                await Awaitable.MainThreadAsync();
                return;
            }
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

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
