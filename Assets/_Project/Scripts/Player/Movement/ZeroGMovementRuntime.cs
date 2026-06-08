using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Player.Movement
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9826)]
    public sealed class ZeroGMovementRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_ZeroGMovementRuntime;

        private const int TelemetryCapacity = 300;
        private const uint InputSignalMaxFrameAge = 2u;
        private const float InputActiveEpsilonSq = 0.000001f;
        private const float JobBudgetExceededMs = 0.1f;
        private const float MaxRecordedSolverElapsedMs = 60000.0f;
        private const uint SourceHash = 0x5A474B50u;
        private const ulong GuardState = 1UL << ((int)BufferID.ZeroGMovementState & 63);
        private const ulong GuardInput = 1UL << ((int)BufferID.ZeroGMovementInput & 63);
        private const ulong GuardTuning = 1UL << ((int)BufferID.ZeroGMovementTuning & 63);
        private const ulong GuardSurface = 1UL << ((int)BufferID.ZeroGMovementSurfaceHit & 63);
        private const ulong GuardOutput = 1UL << ((int)BufferID.ZeroGMovementSolverOutput & 63);
        private const ulong GuardTelemetry = 1UL << ((int)BufferID.ZeroGMovementTelemetryRing & 63);
        private const ulong GuardTelemetryCursor = 1UL << ((int)BufferID.ZeroGMovementTelemetryCursor & 63);
        private const ulong InitializationGuardMask =
            GuardState |
            GuardInput |
            GuardTuning |
            GuardSurface |
            GuardOutput |
            GuardTelemetry |
            GuardTelemetryCursor;
        private const ulong FrameInputGuardMask = GuardInput | GuardTuning;
        private const ulong JobGuardMask = InitializationGuardMask;
        private static readonly double StopwatchTicksToMilliseconds = Stopwatch.Frequency > 0L
            ? 1000.0 / Stopwatch.Frequency
            : 0.0;

        [Header("Authority")]
        [SerializeField, Tooltip("Transform receiving presentation readback from the zero-G solver.")]
        private Transform _authoritativeTransform;
        [SerializeField, Tooltip("Optional view transform used to seed input orientation.")]
        private Transform _orientationSource;
        [SerializeField, Tooltip("Applies the DataVault readback to the transform in the post-fixed swap window.")]
        private bool _applyPresentationTransform = true;

        [Header("Zero-G Solver")]
        [SerializeField, Min(0f)]
        private float _thrustAcceleration = 6.0f;
        [SerializeField, Min(0f)]
        private float _angularAcceleration = 2.4f;
        [SerializeField, Range(0.25f, 40f)]
        private float _maxSpeedMetersPerSecond = 9.0f;
        [SerializeField, Range(0.05f, 12f)]
        private float _maxAngularRadiansPerSecond = 2.8f;
        [SerializeField, Range(0.1f, 3f)]
        private float _radiusMeters = 0.45f;
        [SerializeField, Range(0f, 1f)]
        private float _collisionRestitution = 0.6f;
        [SerializeField, Min(0f)]
        private float _pushImpulseVelocityChange = 3.2f;
        [SerializeField, Range(0f, 0.25f)]
        private float _depenetrationSlopMeters = 0.015f;
        [SerializeField, Range(0f, 16f)]
        private float _horizonLockStrength = 2.2f;
        [SerializeField, Min(0f)]
        private float _propellantDrainPerSecond = 0.035f;
        [SerializeField, Range(0f, 1f)]
        private float _globalQualityWeight = 0.65f;
        [SerializeField, Range(1, 8)]
        private int _maxSubsteps = 1;

        [Header("Orbit Scene Mock SDF")]
        [SerializeField]
        private Vector3 _orbitBoundsHalfExtents = new Vector3(14f, 9f, 20f);
        [SerializeField]
        private Vector3 _horizonUp = Vector3.up;
        [SerializeField, Min(0f)]
        private float _cameraTraumaScale = 0.18f;
        [SerializeField, Min(0f)]
        private float _hapticScale = 0.2f;

        [Header("Mock Input")]
        [SerializeField]
        private Vector3 _mockLocalThrustAxis = Vector3.zero;
        [SerializeField]
        private Vector3 _mockLocalAngularAxis = Vector3.zero;
        [SerializeField]
        private Vector3 _initialLinearVelocity = Vector3.zero;
        [SerializeField]
        private bool _mockThruster;
        [SerializeField]
        private bool _mockPushAndGlide;
        [SerializeField]
        private bool _mockHorizonLock;
        [SerializeField]
        private bool _mockBrakeAssist;
        [SerializeField, Tooltip("Consumes the core deterministic input sidecar before falling back to mock input.")]
        private bool _consumeDeterministicInputSignal = true;
        [SerializeField, Range(0.001f, 0.5f)]
        private float _lookAngularAxisScale = 0.08f;

        private IDataVault _dataVault;
        private Transform _cachedTransform;
        private VaultGenerationHandle<ZeroGMovementStateDTO> _stateHandle;
        private VaultGenerationHandle<ZeroGInputStateDTO> _inputHandle;
        private VaultGenerationHandle<ZeroGTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ZeroGSurfaceHitDTO> _surfaceHandle;
        private VaultGenerationHandle<ZeroGSolverOutputDTO> _outputHandle;
        private VaultGenerationHandle<ZeroGTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;

        private JobHandle _jobHandle;
        private long _jobStartTimestamp;
        private bool _jobScheduled;
        private bool _jobBuffersLocked;
        private IDataVault _jobGuardedVault;
        private ulong _jobGuardMask;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _pendingDisableTeardown;
        private bool _runtimeActive;
        private bool _buffersInitialized;
        private bool _signalsReady;
        private int _droppedSignalCount;
        private int _pendingVaultAccessDeniedCount;
        private uint _scheduledFrame;
        private ZeroGMovementStateDTO _pendingVisualSyncState;
        private ZeroGSolverOutputDTO _pendingVisualSyncOutput;
        private bool _hasPendingVisualSyncReadback;
        private IDataVault _pendingReplacementVault;
        private bool _hasPendingReplacementVault;

        private static ZeroGMovementRuntime s_activeRuntime;

        public int DroppedSignalCount => _droppedSignalCount;

        public bool ConfigureCold(Transform authoritativeTransform, Transform orientationSource)
        {
            if (Application.isPlaying && _runtimeActive)
                return false;

            Transform target = authoritativeTransform != null ? authoritativeTransform : _authoritativeTransform;
            if (target == null)
                target = _cachedTransform != null ? _cachedTransform : transform;

            _authoritativeTransform = target;
            _orientationSource = orientationSource != null ? orientationSource : target;
            return true;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            if (_authoritativeTransform == null)
                _authoritativeTransform = _cachedTransform;
            if (_orientationSource == null)
                _orientationSource = _authoritativeTransform;
            EnsureSignalLanesReady();
        }

        private void OnEnable()
        {
            _runtimeActive = Application.isPlaying;
            if (!_runtimeActive)
                return;

            _pendingDisableTeardown = false;
            _droppedSignalCount = 0;
            _pendingVaultAccessDeniedCount = 0;
            if (s_activeRuntime != null && !ReferenceEquals(s_activeRuntime, this))
            {
                _runtimeActive = false;
                return;
            }

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            if (EnsureBuffers(true))
                s_activeRuntime = this;

            TryRegisterHotSwapListener();
            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            if (!_runtimeActive)
                return;

            _pendingDisableTeardown = true;
            TryUnregisterPostFixed();
            TryUnregisterFixed();
            TryUnregisterHotSwapListener();
            CompletePendingJob();
            if (_jobScheduled || _hasPendingVisualSyncReadback)
                return;

            FinishDisableTeardown();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_jobScheduled || !_runtimeActive || _pendingDisableTeardown || _hasPendingReplacementVault)
                return;

            if (!TryEnsureRuntimeOwnership())
                return;

            if (!EnsureBuffers(false))
                return;

            uint previousFrame = _scheduledFrame;
            uint frame = WriteFrameInput();
            if (frame == previousFrame)
                return;

            float safeDelta = math.clamp(math.isfinite(fixedDeltaTime) ? fixedDeltaTime : 0.02f, 0.0001f, 0.05f);
            ScheduleSolver(safeDelta, frame);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            CompletePendingJob();
            if (_pendingDisableTeardown && !_jobScheduled && !_hasPendingVisualSyncReadback)
                FinishDisableTeardown();
        }

        public void LateFrameTick()
        {
            CompletePendingJob();
            FlushVisualSyncReadback();
            if (_pendingDisableTeardown && !_jobScheduled && !_hasPendingVisualSyncReadback)
                FinishDisableTeardown();
        }

        public static bool TryWriteExternalInput(in ZeroGInputStateDTO frameInput)
        {
            ZeroGMovementRuntime runtime = s_activeRuntime;
            if (runtime == null)
                return false;

            IDataVault vault = runtime._dataVault;
            if (vault == null ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                runtime._jobBuffersLocked ||
                runtime._pendingDisableTeardown ||
                runtime._hasPendingReplacementVault ||
                !IsHandleCreated(in runtime._inputHandle))
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in runtime._inputHandle, SystemID.GameplayPlayer, out NativeArray<ZeroGInputStateDTO> inputBuffer))
                return false;

            try
            {
                if (!inputBuffer.IsCreated || inputBuffer.Length <= 0)
                    return false;

                ZeroGInputStateDTO input = SanitizeExternalAuthorityInput(frameInput);
                input.ActionMask |= ZeroGInputActions.ExternalAuthority;
                input.Flags |= ZeroGMovementStateFlags.ExternalInput;
                inputBuffer[0] = input;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in runtime._inputHandle, SystemID.GameplayPlayer);
            }
        }

        public static bool TryReadState(out ZeroGMovementStateDTO state, out ZeroGSolverOutputDTO output, out ZeroGTuningDTO tuning)
        {
            state = default;
            output = default;
            tuning = default;
            if (!TryGetCachedVault(out IDataVault vault))
                return false;

            if (!TryReadExistingBuffer(vault, BufferID.ZeroGMovementState, out NativeArray<ZeroGMovementStateDTO> states) ||
                !TryReadExistingBuffer(vault, BufferID.ZeroGMovementSolverOutput, out NativeArray<ZeroGSolverOutputDTO> outputs) ||
                !TryReadExistingBuffer(vault, BufferID.ZeroGMovementTuning, out NativeArray<ZeroGTuningDTO> tunings))
            {
                return false;
            }

            state = states[0];
            output = outputs[0];
            tuning = tunings[0];
            if (state.Frame == 0u ||
                state.StateHash == 0u ||
                output.Frame == 0u ||
                output.StateHash == 0u ||
                StateSnapshotContainsNonFinite(in state) ||
                OutputSnapshotContainsNonFinite(in output) ||
                TuningSnapshotContainsNonFinite(in tuning) ||
                state.Frame != output.Frame ||
                output.StateHash != state.StateHash)
            {
                state = default;
                output = default;
                tuning = default;
                return false;
            }

            return true;
        }

        public static bool TryReadLastTelemetry(out ZeroGTelemetryEntry entry)
        {
            entry = default;
            if (!TryGetCachedVault(out IDataVault vault) ||
                !TryReadExistingBuffer(vault, BufferID.ZeroGMovementTelemetryRing, out NativeArray<ZeroGTelemetryEntry> telemetry) ||
                !TryReadExistingBuffer(vault, BufferID.ZeroGMovementTelemetryCursor, out NativeArray<int> cursor))
            {
                return false;
            }

            if (!TryResolveTelemetryLastIndex(cursor[0], telemetry.Length, out int index))
                return false;

            ZeroGTelemetryEntry candidate = telemetry[index];
            if (candidate.Frame == 0u ||
                candidate.StateHash == 0u ||
                TelemetryEntryContainsNonFinite(in candidate))
            {
                return false;
            }

            entry = candidate;
            return true;
        }

        private void FinishDisableTeardown()
        {
            TryUnregisterLateFrame();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            ReleaseVaultBuffers();
            _pendingVisualSyncState = default;
            _pendingVisualSyncOutput = default;
            _hasPendingVisualSyncReadback = false;
            _pendingReplacementVault = null;
            _hasPendingReplacementVault = false;
            _pendingDisableTeardown = false;
            _runtimeActive = false;
        }

        private bool EnsureBuffers(bool allowColdInitialization)
        {
            if (_dataVault == null)
                return false;

            if (!IsHandleCreated(in _stateHandle))
            {
                if (!allowColdInitialization)
                    return false;

                if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                    return false;

                AllocateVaultBuffers(_dataVault);
            }

            if (!IsHandleCreated(in _stateHandle))
                return false;

            if (!_buffersInitialized && allowColdInitialization)
                _buffersInitialized = GenerateEmergencyMockData();

            return _buffersInitialized;
        }

        private bool TryEnsureRuntimeOwnership()
        {
            if (ReferenceEquals(s_activeRuntime, this))
                return true;

            if (s_activeRuntime != null)
            {
                FinishNonOwnerReplacementTeardown();
                return false;
            }

            if (_dataVault == null)
                return false;

            if (!EnsureBuffers(true))
                return false;

            s_activeRuntime = this;
            return true;
        }

        private void AllocateVaultBuffers(IDataVault vault)
        {
            _stateHandle = vault.EnsureGenerationHandle<ZeroGMovementStateDTO>(BufferID.ZeroGMovementState, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _inputHandle = vault.EnsureGenerationHandle<ZeroGInputStateDTO>(BufferID.ZeroGMovementInput, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<ZeroGTuningDTO>(BufferID.ZeroGMovementTuning, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _surfaceHandle = vault.EnsureGenerationHandle<ZeroGSurfaceHitDTO>(BufferID.ZeroGMovementSurfaceHit, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _outputHandle = vault.EnsureGenerationHandle<ZeroGSolverOutputDTO>(BufferID.ZeroGMovementSolverOutput, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<ZeroGTelemetryEntry>(BufferID.ZeroGMovementTelemetryRing, TelemetryCapacity, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(BufferID.ZeroGMovementTelemetryCursor, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
        }

        private bool GenerateEmergencyMockData()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            Transform target = _authoritativeTransform != null ? _authoritativeTransform : _cachedTransform;
            Vector3 runtimePosition = target != null ? target.position : Vector3.zero;
            double3 cameraAup = ResolveRuntimeOriginAupDouble();
            quaternion orientation = target != null ? ToMathQuaternion(target.rotation) : quaternion.identity;
            ZeroGTuningDTO tuning = BuildSerializedTuning();

            ZeroGMovementStateDTO state = default;
            state.AUP_Position = cameraAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            state.Orientation = orientation;
            state.LinearVelocity = SanitizeVector3(_initialLinearVelocity);
            state.AngularMomentum = float3.zero;
            state.SuitPropellant01 = 1.0f;
            state.RadiusMeters = tuning.RadiusMeters;
            state.Restitution = tuning.Restitution;
            state.HorizonLockWeight = 1.0f;
            state.Flags = ZeroGMovementStateFlags.Active | ZeroGMovementStateFlags.EmergencyMockData;
            state.StateHash = ComputeManagedStateHash(state.LinearVelocity, state.AngularMomentum, state.Flags, 0u);

            ZeroGInputStateDTO input = default;
            input.ViewOrientation = orientation;
            input.GlobalQualityWeight = ResolveFrameQualityWeight(tuning.GlobalQualityWeight);

            if (!vault.TryAcquireMutationGuard(InitializationGuardMask))
                return false;

            try
            {
                if (!TryOpenBufferForOwner(in _stateHandle, out NativeArray<ZeroGMovementStateDTO> stateBuffer) ||
                    !TryOpenBufferForOwner(in _inputHandle, out NativeArray<ZeroGInputStateDTO> inputBuffer) ||
                    !TryOpenBufferForOwner(in _tuningHandle, out NativeArray<ZeroGTuningDTO> tuningBuffer) ||
                    !TryOpenBufferForOwner(in _surfaceHandle, out NativeArray<ZeroGSurfaceHitDTO> surfaceBuffer) ||
                    !TryOpenBufferForOwner(in _outputHandle, out NativeArray<ZeroGSolverOutputDTO> outputBuffer) ||
                    !TryOpenBufferForOwner(in _telemetryHandle, out NativeArray<ZeroGTelemetryEntry> telemetryBuffer) ||
                    !TryOpenBufferForOwner(in _telemetryCursorHandle, out NativeArray<int> cursorBuffer))
                {
                    return false;
                }

                stateBuffer[0] = state;
                inputBuffer[0] = input;
                tuningBuffer[0] = tuning;
                surfaceBuffer[0] = default;
                outputBuffer[0] = default;
                for (int i = 0; i < telemetryBuffer.Length; i++)
                    telemetryBuffer[i] = default;
                cursorBuffer[0] = 0;
                _scheduledFrame = 0u;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(InitializationGuardMask);
            }
        }

        private uint WriteFrameInput()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return _scheduledFrame;
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                RecordVaultAccessDenied();
                return _scheduledFrame;
            }

            uint frame = _scheduledFrame + 1u;
            if (frame == 0u)
                frame = 1u;

            ZeroGTuningDTO tuning = BuildSerializedTuning();
            ZeroGInputStateDTO defaultInput = TryBuildDeterministicSignalInput(frame, out ZeroGInputStateDTO deterministicInput)
                ? deterministicInput
                : BuildMockInput();
            float frameQuality = ResolveFrameQualityWeight(tuning.GlobalQualityWeight);

            if (!vault.TryAcquireMutationGuard(FrameInputGuardMask))
            {
                RecordVaultAccessDenied();
                return _scheduledFrame;
            }

            try
            {
                if (!TryOpenBufferForOwner(in _inputHandle, out NativeArray<ZeroGInputStateDTO> inputBuffer) ||
                    !TryOpenBufferForOwner(in _tuningHandle, out NativeArray<ZeroGTuningDTO> tuningBuffer))
                {
                    RecordVaultAccessDenied();
                    return _scheduledFrame;
                }

                _scheduledFrame = frame;

                tuningBuffer[0] = tuning;

                ZeroGInputStateDTO existingInput = inputBuffer[0];
                ZeroGInputStateDTO input;
                if ((existingInput.ActionMask & ZeroGInputActions.ExternalAuthority) != 0u)
                {
                    input = SanitizeExternalAuthorityInput(existingInput);
                    input.ActionMask &= ~ZeroGInputActions.ExternalAuthority;
                    input.Flags |= ZeroGMovementStateFlags.ExternalInput;
                }
                else
                {
                    input = defaultInput;
                }

                input.Frame = frame;
                input.SimulationTick = frame;
                input.GlobalQualityWeight = frameQuality;
                inputBuffer[0] = input;
                return frame;
            }
            finally
            {
                vault.ReleaseMutationGuard(FrameInputGuardMask);
            }
        }

        private ZeroGInputStateDTO BuildMockInput()
        {
            ZeroGInputStateDTO input = default;
            input.LocalThrustAxis = SanitizeVector3(_mockLocalThrustAxis);
            input.LocalAngularAxis = SanitizeVector3(_mockLocalAngularAxis);
            input.ViewOrientation = ResolveViewOrientation();
            uint actionMask = 0u;
            if (_mockThruster)
                actionMask |= ZeroGInputActions.Thruster;
            if (_mockPushAndGlide)
                actionMask |= ZeroGInputActions.PushAndGlide;
            if (_mockHorizonLock)
                actionMask |= ZeroGInputActions.HorizonLock;
            if (_mockBrakeAssist)
                actionMask |= ZeroGInputActions.BrakeAssist;
            input.ActionMask = actionMask;
            return input;
        }

        private bool TryBuildDeterministicSignalInput(uint frame, out ZeroGInputStateDTO input)
        {
            input = default;
            if (!_consumeDeterministicInputSignal)
                return false;

            quaternion viewOrientation = ResolveViewOrientation();
            if (TryBuildInputStateSignalInput(frame, viewOrientation, out input))
                return true;

            if (!CoreDeterminismSignals.TryGetLatestInput(out InputSignal signal) ||
                !IsFreshInputSignal(in signal))
            {
                return false;
            }

            if (TryPackDeterministicInputSignal(
                in signal,
                frame,
                viewOrientation,
                _lookAngularAxisScale,
                out input))
            {
                return true;
            }

            input.ViewOrientation = viewOrientation;
            input.GlobalQualityWeight = ZeroGMathGuards.DefaultQualityWeight;
            input.Frame = frame;
            input.SimulationTick = frame;
            input.Flags = ZeroGMovementStateFlags.ExternalInput | ZeroGMovementStateFlags.SignalDrop;
            return true;
        }

        private bool TryBuildInputStateSignalInput(uint frame, quaternion viewOrientation, out ZeroGInputStateDTO input)
        {
            input = default;
            System.ReadOnlySpan<InputStateSignal> signals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            for (int i = signals.Length - 1; i >= 0; i--)
            {
                if (TryPackInputStateSignal(in signals[i], frame, viewOrientation, _lookAngularAxisScale, out input))
                    return true;
            }

            return SignalBus<InputStateSignal>.TryGetLatest(out InputStateSignal latestSignal, out _) &&
                   TryPackInputStateSignal(in latestSignal, frame, viewOrientation, _lookAngularAxisScale, out input);
        }

        public static bool TryPackInputStateSignal(
            in InputStateSignal signal,
            uint frame,
            quaternion viewOrientation,
            float lookAngularAxisScale,
            out ZeroGInputStateDTO input)
        {
            input = default;
            if (!IsFreshInputStateSignalForFrame(in signal, frame) ||
                !math.all(math.isfinite(viewOrientation.value)) ||
                !math.isfinite(lookAngularAxisScale))
            {
                return false;
            }

            InputState state = signal.State;
            float2 move = new float2(
                state.MoveX * InputState.AxisInvQuantizeScale,
                state.MoveY * InputState.AxisInvQuantizeScale);
            float2 look = new float2(
                state.LookX * InputState.LookInvQuantizeScale,
                state.LookY * InputState.LookInvQuantizeScale);
            float vertical = math.clamp(state.Vertical * InputState.AxisInvQuantizeScale, -1.0f, 1.0f);
            if (!math.all(math.isfinite(move)) ||
                !math.all(math.isfinite(look)) ||
                !math.isfinite(vertical))
            {
                return false;
            }

            uint actions = state.ButtonsBitmask;
            float angularScale = math.clamp(ZeroGMathGuards.SanitizeFloat(lookAngularAxisScale, 0.08f), 0.001f, 0.5f);
            float3 localThrust = new float3(move.x, vertical, move.y);
            float3 localAngular = new float3(
                -look.y * angularScale,
                look.x * angularScale,
                0.0f);

            input.LocalThrustAxis = ZeroGMathGuards.ClampLength(localThrust, 1.0f);
            input.LocalAngularAxis = ZeroGMathGuards.ClampLength(localAngular, 1.0f);
            input.ViewOrientation = ZeroGMathGuards.SanitizeQuaternion(viewOrientation, quaternion.identity);
            input.GlobalQualityWeight = ZeroGMathGuards.DefaultQualityWeight;
            input.Frame = frame;
            input.SimulationTick = frame;
            input.Flags = ZeroGMovementStateFlags.ExternalInput;

            uint actionMask = 0u;
            if (math.lengthsq(input.LocalThrustAxis) > InputActiveEpsilonSq)
                actionMask |= ZeroGInputActions.Thruster;
            if ((actions & ((uint)PlayerInputAction.Jump | (uint)PlayerInputAction.Dash)) != 0u)
                actionMask |= ZeroGInputActions.PushAndGlide;
            if ((actions & (uint)PlayerInputAction.Interact) != 0u)
                actionMask |= ZeroGInputActions.HorizonLock;
            if ((actions & (uint)PlayerInputAction.Sprint) != 0u)
                actionMask |= ZeroGInputActions.BrakeAssist;
            input.ActionMask = actionMask;
            return true;
        }

        public static bool TryPackDeterministicInputSignal(
            in InputSignal signal,
            uint frame,
            quaternion viewOrientation,
            float lookAngularAxisScale,
            out ZeroGInputStateDTO input)
        {
            input = default;
            if (signal.Sequence == 0u)
                return false;
            if (!math.all(math.isfinite(signal.MoveDelta)) ||
                !math.all(math.isfinite(signal.LookDelta)) ||
                !math.isfinite(signal.VerticalDelta) ||
                !math.all(math.isfinite(viewOrientation.value)) ||
                !math.isfinite(lookAngularAxisScale))
            {
                return false;
            }

            float2 move = SanitizeFloat2(signal.MoveDelta);
            float2 look = SanitizeFloat2(signal.LookDelta);
            float vertical = math.clamp(ZeroGMathGuards.SanitizeFloat(signal.VerticalDelta, 0.0f), -1.0f, 1.0f);
            uint actions = signal.ActionsBitmask;
            float angularScale = math.clamp(ZeroGMathGuards.SanitizeFloat(lookAngularAxisScale, 0.08f), 0.001f, 0.5f);

            float roll = 0.0f;
            if ((actions & (uint)PlayerInputAction.PrimaryFire) != 0u)
                roll += 1.0f;
            if ((actions & (uint)PlayerInputAction.SecondaryFire) != 0u)
                roll -= 1.0f;

            float3 localThrust = new float3(move.x, vertical, move.y);
            float3 localAngular = new float3(
                -look.y * angularScale,
                look.x * angularScale,
                roll);

            input.LocalThrustAxis = ZeroGMathGuards.ClampLength(localThrust, 1.0f);
            input.LocalAngularAxis = ZeroGMathGuards.ClampLength(localAngular, 1.0f);
            input.ViewOrientation = ZeroGMathGuards.SanitizeQuaternion(viewOrientation, quaternion.identity);
            input.GlobalQualityWeight = ZeroGMathGuards.DefaultQualityWeight;
            input.Frame = frame;
            input.SimulationTick = frame;
            input.Flags = ZeroGMovementStateFlags.ExternalInput;

            uint actionMask = 0u;
            if (math.lengthsq(input.LocalThrustAxis) > InputActiveEpsilonSq)
                actionMask |= ZeroGInputActions.Thruster;
            if ((actions & ((uint)PlayerInputAction.Jump | (uint)PlayerInputAction.Dash)) != 0u)
                actionMask |= ZeroGInputActions.PushAndGlide;
            if ((actions & (uint)PlayerInputAction.Interact) != 0u)
                actionMask |= ZeroGInputActions.HorizonLock;
            if ((actions & (uint)PlayerInputAction.Sprint) != 0u)
                actionMask |= ZeroGInputActions.BrakeAssist;
            input.ActionMask = actionMask;
            return true;
        }

        private static bool IsFreshInputSignal(in InputSignal signal)
        {
            return IsFreshInputSignalForFrame(in signal, SystemDispatcher.CurrentFrameId);
        }

        public static bool IsFreshInputSignalForFrame(in InputSignal signal, uint currentFrame)
        {
            if (signal.Sequence == 0u)
                return false;

            if (currentFrame == 0u || signal.Frame == 0u || signal.Frame > currentFrame)
                return false;

            return currentFrame - signal.Frame <= InputSignalMaxFrameAge;
        }

        private static bool IsFreshInputStateSignalForFrame(in InputStateSignal signal, uint currentFrame)
        {
            InputState state = signal.State;
            if (state.Sequence == 0u)
                return false;

            if (currentFrame == 0u || state.Frame == 0u || state.Frame > currentFrame)
                return false;

            return currentFrame - state.Frame <= InputSignalMaxFrameAge;
        }

        private void ScheduleSolver(float deltaTime, uint frame)
        {
            if (!TryAcquireJobBufferViews(
                    out NativeArray<ZeroGMovementStateDTO> state,
                    out NativeArray<ZeroGInputStateDTO> input,
                    out NativeArray<ZeroGTuningDTO> tuning,
                    out NativeArray<ZeroGSurfaceHitDTO> surface,
                    out NativeArray<ZeroGSolverOutputDTO> output,
                    out NativeArray<ZeroGTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
            {
                State = state,
                Input = input,
                Tuning = tuning,
                SurfaceHit = surface,
                Output = output,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                CameraAup = ResolveRuntimeOriginAupDouble(),
                DeltaTime = deltaTime,
                Frame = frame
            };

            if (!job.TryScheduleAdmitted(JobAdmissionLane.Lane0_Critical, default(JobHandle), out _jobHandle))
            {
                UnlockJobBuffers();
                return;
            }

            _jobStartTimestamp = Stopwatch.GetTimestamp();
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.GameplayPlayer, _jobHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private void CompletePendingJob()
        {
            if (!_jobScheduled)
            {
                if (_jobBuffersLocked)
                    UnlockJobBuffers();
                ApplyPendingDataVaultReplacementWhenSafe();
                return;
            }

            if (!_jobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _jobHandle))
                return;

            uint completedFrame = _scheduledFrame;
            _jobScheduled = false;
            float elapsedMs = ResolveElapsedJobMs();
            bool budgetExceeded = elapsedMs > JobBudgetExceededMs;
            ZeroGMovementStateDTO state = default;
            ZeroGSolverOutputDTO output = default;
            bool hasReadback = false;
            try
            {
                PatchLastTelemetryElapsed(completedFrame, elapsedMs, budgetExceeded);
                hasReadback = TryReadHeldReadback(completedFrame, out state, out output);
            }
            finally
            {
                UnlockJobBuffers();
            }

            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<ZeroGPhysicsIntegrationJob>(JobAdmissionLane.Lane0_Critical, elapsedMs);
            if (hasReadback)
            {
                _pendingVisualSyncState = state;
                _pendingVisualSyncOutput = output;
                _hasPendingVisualSyncReadback = true;
            }
            ApplyPendingDataVaultReplacementWhenSafe();
        }

        private void FlushVisualSyncReadback()
        {
            if (!_hasPendingVisualSyncReadback)
                return;

            ZeroGMovementStateDTO state = _pendingVisualSyncState;
            ZeroGSolverOutputDTO output = _pendingVisualSyncOutput;
            _pendingVisualSyncState = default;
            _pendingVisualSyncOutput = default;
            _hasPendingVisualSyncReadback = false;

            ApplyReadbackToTransform(in state, in output);
            EmitReadbackSignals(in state, in output);
            ApplyPendingDataVaultReplacementWhenSafe();
        }

        private bool TryAcquireJobBufferViews(
            out NativeArray<ZeroGMovementStateDTO> state,
            out NativeArray<ZeroGInputStateDTO> input,
            out NativeArray<ZeroGTuningDTO> tuning,
            out NativeArray<ZeroGSurfaceHitDTO> surface,
            out NativeArray<ZeroGSolverOutputDTO> output,
            out NativeArray<ZeroGTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            state = default;
            input = default;
            tuning = default;
            surface = default;
            output = default;
            telemetry = default;
            telemetryCursor = default;

            IDataVault vault = _dataVault;
            if (_jobBuffersLocked || vault == null)
                return false;
            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                RecordVaultAccessDenied();
                return false;
            }

            if (!vault.TryAcquireMutationGuard(JobGuardMask))
            {
                RecordVaultAccessDenied();
                return false;
            }

            bool guardTransferred = false;
            try
            {
                if (!TryOpenJobBufferForOwner(vault, in _stateHandle, BufferID.ZeroGMovementState, out state) ||
                    !TryOpenJobBufferForOwner(vault, in _inputHandle, BufferID.ZeroGMovementInput, out input) ||
                    !TryOpenJobBufferForOwner(vault, in _tuningHandle, BufferID.ZeroGMovementTuning, out tuning) ||
                    !TryOpenJobBufferForOwner(vault, in _surfaceHandle, BufferID.ZeroGMovementSurfaceHit, out surface) ||
                    !TryOpenJobBufferForOwner(vault, in _outputHandle, BufferID.ZeroGMovementSolverOutput, out output) ||
                    !TryOpenJobBufferForOwner(vault, in _telemetryHandle, BufferID.ZeroGMovementTelemetryRing, out telemetry) ||
                    !TryOpenJobBufferForOwner(vault, in _telemetryCursorHandle, BufferID.ZeroGMovementTelemetryCursor, out telemetryCursor) ||
                    telemetry.Length != TelemetryCapacity ||
                    telemetryCursor.Length != 1)
                {
                    RecordVaultAccessDenied();
                    return false;
                }

                _jobGuardedVault = vault;
                _jobGuardMask = JobGuardMask;
                _jobBuffersLocked = true;
                guardTransferred = true;
                return true;
            }
            finally
            {
                if (!guardTransferred)
                    vault.ReleaseMutationGuard(JobGuardMask);
            }
        }

        private bool TryReadHeldReadback(uint expectedFrame, out ZeroGMovementStateDTO state, out ZeroGSolverOutputDTO output)
        {
            state = default;
            output = default;
            if (!TryOpenHeldJobBuffer(in _stateHandle, BufferID.ZeroGMovementState, out NativeArray<ZeroGMovementStateDTO> states) ||
                !TryOpenHeldJobBuffer(in _outputHandle, BufferID.ZeroGMovementSolverOutput, out NativeArray<ZeroGSolverOutputDTO> outputs))
            {
                return false;
            }

            state = states[0];
            output = outputs[0];
            if (state.Frame == 0u ||
                output.Frame == 0u ||
                state.StateHash == 0u ||
                output.StateHash == 0u ||
                StateSnapshotContainsNonFinite(in state) ||
                OutputSnapshotContainsNonFinite(in output) ||
                state.Frame != expectedFrame ||
                output.Frame != expectedFrame ||
                output.StateHash != state.StateHash)
            {
                state = default;
                output = default;
                return false;
            }

            return true;
        }

        private void PatchLastTelemetryElapsed(uint expectedFrame, float elapsedMs, bool budgetExceeded)
        {
            if (!math.isfinite(elapsedMs) || elapsedMs < 0.0f)
            {
                elapsedMs = 0.0f;
                budgetExceeded = false;
            }
            else
            {
                elapsedMs = math.min(elapsedMs, MaxRecordedSolverElapsedMs);
                budgetExceeded = budgetExceeded && elapsedMs > JobBudgetExceededMs;
            }

            if (!TryOpenHeldJobBuffer(in _telemetryHandle, BufferID.ZeroGMovementTelemetryRing, out NativeArray<ZeroGTelemetryEntry> telemetry) ||
                !TryOpenHeldJobBuffer(in _telemetryCursorHandle, BufferID.ZeroGMovementTelemetryCursor, out NativeArray<int> cursorBuffer))
            {
                return;
            }

            if (!TryResolveTelemetryLastIndex(cursorBuffer[0], telemetry.Length, out int index))
                return;

            ZeroGTelemetryEntry entry = telemetry[index];
            if (entry.Frame != expectedFrame)
                return;

            entry.SolverComputeTimeMs = elapsedMs;
            if (budgetExceeded)
                entry.Flags |= ZeroGMovementStateFlags.BudgetExceeded;
            if (_pendingVaultAccessDeniedCount > 0)
            {
                entry.Flags |= ZeroGMovementStateFlags.VaultAccessDenied;
                if (entry.FaultCode == ZeroGMovementFaultCodes.None)
                    entry.FaultCode = ZeroGMovementFaultCodes.VaultAccessDenied;
                _pendingVaultAccessDeniedCount = 0;
            }
            telemetry[index] = entry;
        }

        private float ResolveElapsedJobMs()
        {
            long delta = Stopwatch.GetTimestamp() - _jobStartTimestamp;
            if (delta <= 0L || StopwatchTicksToMilliseconds <= 0.0)
                return 0.0f;

            double elapsedMs = delta * StopwatchTicksToMilliseconds;
            if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs <= 0.0)
                return 0.0f;
            if (elapsedMs >= MaxRecordedSolverElapsedMs)
                return MaxRecordedSolverElapsedMs;
            return (float)elapsedMs;
        }

        private void ApplyReadbackToTransform(in ZeroGMovementStateDTO state, in ZeroGSolverOutputDTO output)
        {
            if (!_applyPresentationTransform || _authoritativeTransform == null)
                return;
            if ((output.Flags & ZeroGSolverOutputDTO.FlagFault) != 0u)
                return;
            if (!math.all(math.isfinite(state.AUP_Position)) || !math.all(math.isfinite(state.Orientation.value)))
                return;

            double3 localDouble = state.AUP_Position - ResolveRuntimeOriginAupDouble();
            if (!LocalDoubleFitsFloat3(localDouble))
                return;

            Vector3 position = new Vector3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            _authoritativeTransform.SetPositionAndRotation(position, ToUnityQuaternion(state.Orientation));
        }

        private void EmitReadbackSignals(in ZeroGMovementStateDTO state, in ZeroGSolverOutputDTO output)
        {
            if (!OutputSignalPayloadIsFinite(in output))
            {
                if ((output.Flags & (ZeroGSolverOutputDTO.FlagCameraTrauma | ZeroGSolverOutputDTO.FlagHaptic)) != 0u)
                    RecordSignalDrop();
                return;
            }

            if ((output.Flags & ZeroGSolverOutputDTO.FlagCameraTrauma) != 0u && output.CameraTrauma01 > 0.0f)
            {
                Vector3 position = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
                Vector3 direction = new Vector3(output.CollisionNormal.x, output.CollisionNormal.y, output.CollisionNormal.z);
                uint profileHash = output.CameraTrauma01 >= 0.35f
                    ? CameraJuiceSignals.SharpKineticImpactProfileHash
                    : CameraJuiceSignals.HighFreqToolVibrationProfileHash;
                byte priority = output.CameraTrauma01 >= 0.55f
                    ? CameraJuiceSignals.HighPriority
                    : CameraJuiceSignals.NormalPriority;
                if (!CameraJuiceSignals.TryPublishImpact(
                        output.CameraTrauma01,
                        position,
                        direction,
                        profileHash,
                        0.95f,
                        priority,
                        0f,
                        0.9f,
                        1.1f,
                        SourceHash))
                {
                    RecordSignalDrop();
                }
            }

            if ((output.Flags & ZeroGSolverOutputDTO.FlagHaptic) != 0u && output.CollisionImpulse > 0.0f)
            {
                float hapticScale = SanitizePresentationHapticScale(_hapticScale);
                float hapticSource = output.CollisionImpulse * hapticScale;
                if (!math.isfinite(hapticSource) || hapticSource <= 0.0f)
                {
                    RecordSignalDrop();
                    return;
                }

                HapticRequest request = default;
                request.Intensity01 = math.saturate(hapticSource);
                request.DurationSeconds = 0.035f + request.Intensity01 * 0.06f;
                request.Frequency01 = math.saturate(0.3f + request.Intensity01 * 0.5f);
                request.SourceHash = SourceHash;
                request.Frame = output.Frame;
                request.Channel = HapticRequest.ChannelCollision;
                request.Flags = HapticRequest.FlagLightThud;
                if (!SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001DirectSignalPushDropCount_ZeroGMovementRuntime))
                    RecordSignalDrop();
            }

            if ((output.Flags & ZeroGSolverOutputDTO.FlagFault) != 0u ||
                (state.Flags & ZeroGMovementStateFlags.NaNDetected) != 0u)
            {
                _droppedSignalCount = math.min(_droppedSignalCount + 1, 0x3FFFFFFF);
            }
        }

        private void RecordSignalDrop()
        {
            _droppedSignalCount = math.min(_droppedSignalCount + 1, 0x3FFFFFFF);
        }

        private void RecordVaultAccessDenied()
        {
            _pendingVaultAccessDeniedCount = math.min(_pendingVaultAccessDeniedCount + 1, 0x3FFFFFFF);
        }

        private void UnlockJobBuffers()
        {
            if (!_jobBuffersLocked)
                return;

            IDataVault vault = _jobGuardedVault;
            if (vault != null)
                vault.ReleaseMutationGuard(_jobGuardMask != 0UL ? _jobGuardMask : JobGuardMask);

            _jobGuardedVault = null;
            _jobGuardMask = 0UL;
            _jobBuffersLocked = false;
        }

        private void ApplyPendingDataVaultReplacementWhenSafe()
        {
            if (!_hasPendingReplacementVault || _jobScheduled || _jobBuffersLocked || _hasPendingVisualSyncReadback)
                return;

            if (s_activeRuntime != null && !ReferenceEquals(s_activeRuntime, this))
            {
                _pendingReplacementVault = null;
                _hasPendingReplacementVault = false;
                FinishNonOwnerReplacementTeardown();
                return;
            }

            IDataVault replacementVault = _pendingReplacementVault;
            _pendingReplacementVault = null;
            _hasPendingReplacementVault = false;
            ReleaseVaultBuffers();
            _dataVault = replacementVault;
            if (EnsureBuffers(true))
            {
                s_activeRuntime = this;
            }
            else if (ReferenceEquals(s_activeRuntime, this))
            {
                s_activeRuntime = null;
            }
        }

        private void FinishNonOwnerReplacementTeardown()
        {
            TryUnregisterPostFixed();
            TryUnregisterFixed();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            ClearVaultHandlesWithoutRelease();
            _dataVault = null;
            _pendingDisableTeardown = false;
            _runtimeActive = false;
        }

        private bool TryOpenBufferForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   IsHandleCreated(in handle) &&
                   _dataVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryOpenJobBufferForOwner<T>(IDataVault vault, in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsHandleCreated(in handle) &&
                   handle.BufferID == (uint)expectedBufferId &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private bool TryOpenHeldJobBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _jobGuardedVault;
            return _jobBuffersLocked &&
                   vault != null &&
                   IsHandleCreated(in handle) &&
                   handle.BufferID == (uint)expectedBufferId &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryGetCachedVault(out IDataVault vault)
        {
            vault = null;
            ZeroGMovementRuntime runtime = s_activeRuntime;
            if (runtime == null)
                return false;

            IDataVault cachedVault = runtime._dataVault;
            if (cachedVault == null ||
                cachedVault.IsAllocationLocked ||
                cachedVault.IsCompactionFenceActive ||
                runtime._jobBuffersLocked ||
                runtime._pendingDisableTeardown ||
                runtime._hasPendingReplacementVault)
            {
                return false;
            }

            vault = cachedVault;
            return true;
        }

        private static bool TryReadExistingBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   IsHandleCreated(in handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TelemetryEntryContainsNonFinite(in ZeroGTelemetryEntry entry)
        {
            return !math.all(math.isfinite(entry.LocalPosition)) ||
                   !math.all(math.isfinite(entry.LinearVelocity)) ||
                   !math.all(math.isfinite(entry.AngularMomentum)) ||
                   !math.isfinite(entry.CollisionImpulse) ||
                   !math.isfinite(entry.Propellant01) ||
                   !math.isfinite(entry.SolverComputeTimeMs);
        }

        private static bool TryResolveTelemetryLastIndex(int cursor, int telemetryLength, out int index)
        {
            index = 0;
            if (telemetryLength != TelemetryCapacity ||
                cursor < 0 ||
                cursor >= telemetryLength)
            {
                return false;
            }

            index = cursor == 0 ? telemetryLength - 1 : cursor - 1;
            return true;
        }

        private static bool StateSnapshotContainsNonFinite(in ZeroGMovementStateDTO state)
        {
            return !math.all(math.isfinite(state.AUP_Position)) ||
                   !math.all(math.isfinite(state.Orientation.value)) ||
                   !math.all(math.isfinite(state.LinearVelocity)) ||
                   !math.all(math.isfinite(state.AngularMomentum)) ||
                   !math.isfinite(state.SuitPropellant01) ||
                   !math.isfinite(state.RadiusMeters) ||
                   !math.isfinite(state.Restitution) ||
                   !math.isfinite(state.HorizonLockWeight) ||
                   !math.isfinite(state.LastCollisionImpulse) ||
                   !math.isfinite(state.LastDepenetration);
        }

        private static bool OutputSnapshotContainsNonFinite(in ZeroGSolverOutputDTO output)
        {
            return !math.all(math.isfinite(output.LocalPosition)) ||
                   !math.all(math.isfinite(output.LinearVelocity)) ||
                   !math.all(math.isfinite(output.CollisionNormal)) ||
                   !math.isfinite(output.CollisionImpulse) ||
                   !math.isfinite(output.CameraTrauma01) ||
                   !math.isfinite(output.Propellant01);
        }

        private static bool TuningSnapshotContainsNonFinite(in ZeroGTuningDTO tuning)
        {
            return !math.isfinite(tuning.ThrustAcceleration) ||
                   !math.isfinite(tuning.AngularAcceleration) ||
                   !math.isfinite(tuning.MaxSpeedMetersPerSecond) ||
                   !math.isfinite(tuning.MaxAngularRadiansPerSecond) ||
                   !math.isfinite(tuning.RadiusMeters) ||
                   !math.isfinite(tuning.Restitution) ||
                   !math.isfinite(tuning.PushImpulseVelocityChange) ||
                   !math.isfinite(tuning.DepenetrationSlopMeters) ||
                   !math.isfinite(tuning.HorizonLockStrength) ||
                   !math.isfinite(tuning.PropellantDrainPerSecond) ||
                   !math.isfinite(tuning.GlobalQualityWeight) ||
                   !math.isfinite(tuning.SurfaceProbeRadiusMeters) ||
                   !math.all(math.isfinite(tuning.OrbitBoundsHalfExtents)) ||
                   !math.all(math.isfinite(tuning.HorizonUp)) ||
                   !math.isfinite(tuning.CameraTraumaScale) ||
                   !math.isfinite(tuning.HapticScale) ||
                   tuning.MaxSubsteps == 0u;
        }

        private static bool LocalDoubleFitsFloat3(double3 value)
        {
            double3 maxFloat = new double3(float.MaxValue);
            return math.all(math.isfinite(value)) && math.all(math.abs(value) <= maxFloat);
        }

        private static bool OutputSignalPayloadIsFinite(in ZeroGSolverOutputDTO output)
        {
            return math.all(math.isfinite(output.LocalPosition)) &&
                   math.all(math.isfinite(output.CollisionNormal)) &&
                   math.isfinite(output.CollisionImpulse) &&
                   math.isfinite(output.CameraTrauma01);
        }

        private static float SanitizePresentationHapticScale(float value)
        {
            return math.isfinite(value) ? math.max(0.0f, value) : 0.0f;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.SystemID == (uint)SystemID.GameplayPlayer;
        }

        private void ReleaseVaultBuffers()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _stateHandle);
            ReleaseVaultBuffer(vault, ref _inputHandle);
            ReleaseVaultBuffer(vault, ref _tuningHandle);
            ReleaseVaultBuffer(vault, ref _surfaceHandle);
            ReleaseVaultBuffer(vault, ref _outputHandle);
            ReleaseVaultBuffer(vault, ref _telemetryHandle);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle);
            _buffersInitialized = false;
        }

        private void ClearVaultHandlesWithoutRelease()
        {
            _stateHandle = default;
            _inputHandle = default;
            _tuningHandle = default;
            _surfaceHandle = default;
            _outputHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _buffersInitialized = false;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private ZeroGTuningDTO BuildSerializedTuning()
        {
            ZeroGTuningDTO tuning = default;
            tuning.ThrustAcceleration = math.max(0.0f, _thrustAcceleration);
            tuning.AngularAcceleration = math.max(0.0f, _angularAcceleration);
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, _maxSpeedMetersPerSecond);
            tuning.MaxAngularRadiansPerSecond = math.max(0.05f, _maxAngularRadiansPerSecond);
            tuning.RadiusMeters = math.clamp(_radiusMeters, 0.1f, 3.0f);
            tuning.Restitution = math.clamp(_collisionRestitution, 0.0f, 1.0f);
            tuning.PushImpulseVelocityChange = math.max(0.0f, _pushImpulseVelocityChange);
            tuning.DepenetrationSlopMeters = math.clamp(_depenetrationSlopMeters, 0.0f, 0.25f);
            tuning.HorizonLockStrength = math.clamp(_horizonLockStrength, 0.0f, 16.0f);
            tuning.PropellantDrainPerSecond = math.max(0.0f, _propellantDrainPerSecond);
            tuning.GlobalQualityWeight = ResolveFrameQualityWeight(_globalQualityWeight);
            tuning.SurfaceProbeRadiusMeters = tuning.RadiusMeters;
            tuning.OrbitBoundsHalfExtents = math.max(SanitizeVector3(_orbitBoundsHalfExtents), new float3(tuning.RadiusMeters + 0.5f));
            tuning.HorizonUp = ZeroGMathGuards.NormalizeWithFallback(SanitizeVector3(_horizonUp), new float3(0f, 1f, 0f));
            tuning.MaxSubsteps = (uint)math.clamp(_maxSubsteps, 1, 8);
            tuning.CameraTraumaScale = math.max(0.0f, _cameraTraumaScale);
            tuning.HapticScale = math.max(0.0f, _hapticScale);
            return ZeroGMathGuards.SanitizeTuning(tuning);
        }

        public static ZeroGInputStateDTO SanitizeExternalAuthorityInput(ZeroGInputStateDTO input)
        {
            bool sourceNonFinite = InputDtoContainsNonFinite(in input);
            input.LocalThrustAxis = ZeroGMathGuards.ClampLength(ZeroGMathGuards.SanitizeFloat3(input.LocalThrustAxis, float3.zero), 1.0f);
            input.LocalAngularAxis = ZeroGMathGuards.ClampLength(ZeroGMathGuards.SanitizeFloat3(input.LocalAngularAxis, float3.zero), 1.0f);
            input.ViewOrientation = ZeroGMathGuards.SanitizeQuaternion(input.ViewOrientation, quaternion.identity);
            input.GlobalQualityWeight = ZeroGMathGuards.Sanitize01(input.GlobalQualityWeight, ZeroGMathGuards.DefaultQualityWeight);
            input.ActionMask &= ZeroGInputActions.ValidMask;
            if (sourceNonFinite)
            {
                input.LocalThrustAxis = float3.zero;
                input.LocalAngularAxis = float3.zero;
                input.ActionMask = 0u;
                input.Flags |= ZeroGMovementStateFlags.SignalDrop;
            }

            return input;
        }

        private static bool InputDtoContainsNonFinite(in ZeroGInputStateDTO input)
        {
            return !math.all(math.isfinite(input.LocalThrustAxis)) ||
                   !math.all(math.isfinite(input.LocalAngularAxis)) ||
                   !math.all(math.isfinite(input.ViewOrientation.value)) ||
                   !math.isfinite(input.GlobalQualityWeight);
        }

        private float ResolveFrameQualityWeight(float tuningQualityWeight)
        {
            float homeostasis = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : ZeroGMathGuards.DefaultQualityWeight);
            float tuning = ZeroGMathGuards.Sanitize01(tuningQualityWeight, homeostasis);
            return math.min(homeostasis, tuning);
        }

        private quaternion ResolveViewOrientation()
        {
            Transform source = _orientationSource != null ? _orientationSource : _authoritativeTransform;
            return source != null ? ToMathQuaternion(source.rotation) : quaternion.identity;
        }

        private static float3 SanitizeVector3(Vector3 value)
        {
            return new float3(
                math.isfinite(value.x) ? value.x : 0.0f,
                math.isfinite(value.y) ? value.y : 0.0f,
                math.isfinite(value.z) ? value.z : 0.0f);
        }

        private static float2 SanitizeFloat2(float2 value)
        {
            return new float2(
                math.isfinite(value.x) ? value.x : 0.0f,
                math.isfinite(value.y) ? value.y : 0.0f);
        }

        private static quaternion ToMathQuaternion(Quaternion rotation)
        {
            return ZeroGMathGuards.SanitizeQuaternion(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w), quaternion.identity);
        }

        private static Quaternion ToUnityQuaternion(quaternion rotation)
        {
            quaternion safe = ZeroGMathGuards.SanitizeQuaternion(rotation, quaternion.identity);
            return new Quaternion(safe.value.x, safe.value.y, safe.value.z, safe.value.w);
        }

        private static double3 ResolveRuntimeOriginAupDouble()
        {
            var originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : double3.zero;
        }

        private static uint ComputeManagedStateHash(float3 velocity, float3 angular, uint flags, uint frame)
        {
            uint hash = 2166136261u;
            hash = (hash ^ SourceHash) * 16777619u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ math.asuint(velocity.x)) * 16777619u;
            hash = (hash ^ math.asuint(velocity.y)) * 16777619u;
            hash = (hash ^ math.asuint(velocity.z)) * 16777619u;
            hash = (hash ^ math.asuint(angular.x)) * 16777619u;
            hash = (hash ^ math.asuint(angular.y)) * 16777619u;
            hash = (hash ^ math.asuint(angular.z)) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private void EnsureSignalLanesReady()
        {
            if (_signalsReady)
                return;

            CameraJuiceSignals.EnsurePrewarmed();
            SignalBus<HapticRequest>.EnsureInitialized();
            _signalsReady = true;
        }

        private void TryRegisterFixed()
        {
            if (_registeredFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterFixed()
        {
            if (!_registeredFixed)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixed = false;
        }

        private void TryRegisterPostFixed()
        {
            if (_registeredPostFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixed()
        {
            if (!_registeredPostFixed)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixed = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterPostFixed();
                TryUnregisterFixed();
                TryUnregisterLateFrame();
                if (currentService != null && _runtimeActive)
                {
                    TryRegisterFixed();
                    TryRegisterPostFixed();
                    TryRegisterLateFrame();
                }
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingJob();
            _pendingReplacementVault = currentService as IDataVault;
            _hasPendingReplacementVault = true;
            ApplyPendingDataVaultReplacementWhenSafe();
        }
    }
}
