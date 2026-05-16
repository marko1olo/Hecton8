using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Animation.Locomotion
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9921)]
    internal sealed class ProceduralLadderClimbRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable
    {
        private const float DefaultPcSlideSpeedMetersPerSecond = 1.35f;
        private const float StaminaDrainPerMeter = 0.18f;
        private const float SlipVelocityMetersPerSecond = -2.25f;
        private const float ShoulderWidthMeters = 0.44f;
        private const float ShoulderHeightMeters = 1.36f;
        private const float ShoulderBacksetMeters = 0.14f;
        private const float ElbowPoleMeters = 0.38f;
        private const float FastClimbStressSpeedMetersPerSecond = 1.4f;
        private const float ClimbStressOxygenDrainBonus = 0.28f;
        private const float LookDownGripReleaseDotThreshold = 0.9848077f;
        private const float HeadStabilizationSharpness = 12f;
        private const uint DefaultGripActionMask = 1u << 6;
        private const byte GripMaskLeft = 1 << 0;
        private const byte GripMaskRight = 1 << 1;

        [Header("IK Targets")]
        [SerializeField] private Transform leftHandIkTarget;
        [SerializeField] private Transform rightHandIkTarget;
        [SerializeField] private Transform leftElbowIkTarget;
        [SerializeField] private Transform rightElbowIkTarget;
        [SerializeField] private Transform cameraSlideTarget;

        [Header("Runtime")]
        [SerializeField] private float pcSlideSpeedMetersPerSecond = DefaultPcSlideSpeedMetersPerSecond;
        [SerializeField] private bool forceVrGripPullMode;
        [SerializeField] private uint universalGripActionMask = DefaultGripActionMask;

        private IDataVault _dataVault;
        private VaultBufferHandle<LadderClimbIkInput> _inputHandle;
        private VaultBufferHandle<LadderClimbIkOutput> _outputHandle;
        private VaultBufferHandle<AbsoluteUniversePosition> _ladderAupHandle;
        private VaultBufferHandle<LadderClimbTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;

        private JobHandle _solveHandle;
        private bool _solveScheduled;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _active;
        private bool _pendingFinish;
        private bool _pendingSlip;
        private bool _matchRotation;
        private bool _vrGripRequired;
        private bool _lowTierCameraSlide;
        private Transform _playerRoot;
        private Transform _ladderTransform;
        private Transform _entryPoint;
        private Transform _exitPoint;
        private IPlayerMovementForceSink _movementForceSink;
        private IPlayerRuntimeContext _playerContext;
        private float3 _ladderUp;
        private float3 _ladderForward;
        private float _climbDirection;
        private float _climbProgressMeters;
        private float _climbHeightMeters;
        private float _stamina01;
        private float _pendingGripPullMeters;
        private Vector3 _cameraSlideEntryPosition;
        private Vector3 _cameraSlideExitPosition;
        private Quaternion _headStabilizedRotation = Quaternion.identity;
        private byte _pendingGripMask;
        private byte _lastResolvedGripMask;
        private int _lastLeftRung = -1;
        private int _lastRightRung = -1;
        private byte _qualityTier;
        private bool _headStabilizationInitialized;

        internal static ProceduralLadderClimbRuntime EnsureRuntimeInstance()
        {
            ProceduralLadderClimbRuntime registered = GlobalRegistry.ProceduralLadderClimbRuntime;
            if (registered != null)
                return registered;

            if (!Application.isPlaying)
                return null;

            GameObject runtimeRoot = new GameObject("[ProceduralLadderClimbRuntime]"); // COLD ALLOC: one registry-owned animation locomotion runtime root.
            DontDestroyOnLoad(runtimeRoot);
            return runtimeRoot.AddComponent<ProceduralLadderClimbRuntime>();
        }

        internal static bool TryBeginClimb(
            Transform ladderTransform,
            Transform entryPoint,
            Transform exitPoint,
            Transform player,
            bool goingUp,
            bool matchRotation)
        {
            ProceduralLadderClimbRuntime runtime = EnsureRuntimeInstance();
            return runtime != null &&
                   runtime.TryBeginClimbInstance(ladderTransform, entryPoint, exitPoint, player, goingUp, matchRotation);
        }

        internal void ConfigureIkTargets(
            Transform leftHand,
            Transform rightHand,
            Transform leftElbow,
            Transform rightElbow,
            Transform slideTarget)
        {
            leftHandIkTarget = leftHand;
            rightHandIkTarget = rightHand;
            leftElbowIkTarget = leftElbow;
            rightElbowIkTarget = rightElbow;
            cameraSlideTarget = slideTarget;
        }

        internal void SetVrGripPullMode(bool required)
        {
            forceVrGripPullMode = required;
        }

        internal void SubmitUniversalInputState(uint actionsBitmask, float leftHandDeltaAlongLadderMeters, float rightHandDeltaAlongLadderMeters)
        {
            if ((actionsBitmask & universalGripActionMask) == 0u)
                return;

            SubmitGripPullDelta(leftHandDeltaAlongLadderMeters, rightHandDeltaAlongLadderMeters, (byte)(GripMaskLeft | GripMaskRight));
        }

        internal void SubmitGripPullDelta(float leftHandDeltaAlongLadderMeters, float rightHandDeltaAlongLadderMeters, byte gripMask)
        {
            if (!_active || gripMask == 0)
                return;

            float pullMeters = 0f;
            int samples = 0;
            if ((gripMask & GripMaskLeft) != 0 && IsFinite(leftHandDeltaAlongLadderMeters))
            {
                pullMeters += leftHandDeltaAlongLadderMeters;
                samples++;
            }

            if ((gripMask & GripMaskRight) != 0 && IsFinite(rightHandDeltaAlongLadderMeters))
            {
                pullMeters += rightHandDeltaAlongLadderMeters;
                samples++;
            }

            if (samples <= 0)
                return;

            float averagedPull = pullMeters * math.rcp((float)math.max(1, samples));
            _pendingGripPullMeters = math.clamp(_pendingGripPullMeters + math.clamp(averagedPull, -0.45f, 0.45f), -0.45f, 0.45f);
            _pendingGripMask |= gripMask;
        }

        private void Awake()
        {
            ProceduralLadderClimbRuntime registered = GlobalRegistry.ProceduralLadderClimbRuntime;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterProceduralLadderClimbRuntime(this);
            CacheVaultDependency();
            EnsureVaultBuffers();
        }

        private void OnEnable()
        {
            if (GlobalRegistry.ProceduralLadderClimbRuntime == null)
                GlobalRegistry.RegisterProceduralLadderClimbRuntime(this);

            CacheVaultDependency();
            EnsureVaultBuffers();
        }

        private void OnDisable()
        {
            StopClimb(false, false);
            CompleteOutstandingJob();
            UnregisterTickables();
        }

        private void OnDestroy()
        {
            StopClimb(false, false);
            CompleteOutstandingJob();
            UnregisterTickables();
            ClearVaultHandles();
            GlobalRegistry.ClearProceduralLadderClimbRuntime(this);
        }

        public void FastTick(float deltaTime)
        {
            if (!_active)
                return;

            if (_solveScheduled)
                return;

            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            float previousProgress = _climbProgressMeters;
            float progressDelta = ResolveProgressDelta(safeDeltaTime);
            _climbProgressMeters = math.clamp(_climbProgressMeters + progressDelta, 0f, _climbHeightMeters);
            float appliedProgressDelta = _climbProgressMeters - previousProgress;
            DrainStamina(appliedProgressDelta);
            PublishClimbPhysiology(math.abs(appliedProgressDelta), safeDeltaTime);

            if ((_stamina01 <= 0.0001f && math.abs(appliedProgressDelta) > 0.0001f) ||
                ShouldDropFromLookDownGripRelease())
            {
                _pendingSlip = true;
                _pendingFinish = true;
            }

            if ((_climbDirection > 0f && _climbProgressMeters >= _climbHeightMeters - 0.0001f) ||
                (_climbDirection < 0f && _climbProgressMeters <= 0.0001f))
            {
                _pendingFinish = true;
            }

            ApplyPresentationDelta(appliedProgressDelta, safeDeltaTime);
            PublishClimbState(false);
            ScheduleSolve();
        }

        public void LateFrameTick()
        {
            if (!_solveScheduled)
            {
                if (_pendingFinish)
                    StopClimb(true, _pendingSlip);
                return;
            }

            _solveHandle.Complete();
            _solveScheduled = false;
            if (!TryResolveOutput(out NativeArray<LadderClimbIkOutput> outputs))
            {
                StopClimb(false, true);
                return;
            }

            LadderClimbIkOutput output = outputs[0];
            if (!IsFinite(output.LeftHandTarget) ||
                !IsFinite(output.RightHandTarget) ||
                !IsFinite(output.LeftElbowTarget) ||
                !IsFinite(output.RightElbowTarget))
            {
                DumpBlackBox();
                StopClimb(false, true);
                return;
            }

            ApplyIkTargets(in output);
            EmitRungContactHaptics(in output);
            PublishClimbState(_pendingSlip);

            if (_pendingFinish)
                StopClimb(true, _pendingSlip);
        }

        private bool TryBeginClimbInstance(
            Transform ladderTransform,
            Transform entryPoint,
            Transform exitPoint,
            Transform player,
            bool goingUp,
            bool matchRotation)
        {
            if (ladderTransform == null || entryPoint == null || exitPoint == null || player == null)
                return false;

            if (!CacheVaultDependency() ||
                !EnsureVaultBuffers() ||
                !TryResolveLadderAups(out NativeArray<AbsoluteUniversePosition> ladderAups))
            {
                return false;
            }

            CompleteOutstandingJob();
            _playerRoot = player;
            _ladderTransform = ladderTransform;
            _entryPoint = entryPoint;
            _exitPoint = exitPoint;
            _matchRotation = matchRotation;
            _movementForceSink = GlobalRegistry.PlayerMovementContracts;
            _playerContext = GlobalRegistry.Player;
            _qualityTier = GlobalRegistry.ScalabilityTierProfileByte;
            _vrGripRequired = forceVrGripPullMode || UnityEngine.XR.XRSettings.enabled;
            _lowTierCameraSlide = !_vrGripRequired || _qualityTier == 0;
            _climbDirection = goingUp ? 1f : -1f;
            _stamina01 = 1f;
            _pendingGripPullMeters = 0f;
            _pendingGripMask = 0;
            _lastResolvedGripMask = 0;
            _pendingFinish = false;
            _pendingSlip = false;
            _lastLeftRung = -1;
            _lastRightRung = -1;

            ResolveLadderFrame(entryPoint.position, exitPoint.position, ladderTransform);
            InitializePresentationAnchors(entryPoint.position, exitPoint.position);
            _climbProgressMeters = goingUp ? 0f : _climbHeightMeters;
            ladderAups[0] = AbsoluteUniversePosition.FromRuntimePosition(entryPoint.position);

            if (!TryRegisterTickables())
            {
                UnregisterTickables();
                _playerRoot = null;
                _ladderTransform = null;
                _entryPoint = null;
                _exitPoint = null;
                _movementForceSink = null;
                _playerContext = null;
                return false;
            }

            _active = true;
            PublishClimbState(false);
            ScheduleSolve();
            return true;
        }

        private bool CacheVaultDependency()
        {
            if (_dataVault != null)
                return true;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault != null;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!_inputHandle.IsCreated)
                _inputHandle = vault.GetBufferHandle<LadderClimbIkInput>(BufferID.LadderClimbIkInput, 1, SystemID.GameplayPlayer, NativeArrayOptions.ClearMemory);

            if (!_outputHandle.IsCreated)
                _outputHandle = vault.GetBufferHandle<LadderClimbIkOutput>(BufferID.LadderClimbIkOutput, 1, SystemID.GameplayPlayer, NativeArrayOptions.ClearMemory);

            if (!_ladderAupHandle.IsCreated)
                _ladderAupHandle = vault.GetBufferHandle<AbsoluteUniversePosition>(
                    BufferID.LadderAUPs,
                    LadderClimbIkConstants.MaxActiveLadders,
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);

            if (!_telemetryRingHandle.IsCreated)
                _telemetryRingHandle = vault.GetBufferHandle<LadderClimbTelemetryEntry>(
                    BufferID.LadderClimbIkTelemetryRing,
                    LadderClimbIkConstants.BlackBoxFrameCapacity,
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);

            if (!_telemetryCursorHandle.IsCreated)
                _telemetryCursorHandle = vault.GetBufferHandle<int>(
                    BufferID.LadderClimbIkTelemetryCursor,
                    1,
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);

            return _inputHandle.IsCreated &&
                   _outputHandle.IsCreated &&
                   _ladderAupHandle.IsCreated &&
                   _telemetryRingHandle.IsCreated &&
                   _telemetryCursorHandle.IsCreated;
        }

        private bool TryResolveVaultViews(
            out NativeArray<LadderClimbIkInput> inputs,
            out NativeArray<LadderClimbIkOutput> outputs,
            out NativeArray<AbsoluteUniversePosition> ladderAups,
            out NativeArray<LadderClimbTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            inputs = default;
            outputs = default;
            ladderAups = default;
            telemetryRing = default;
            telemetryCursor = default;

            if (!EnsureVaultBuffers())
                return false;

            inputs = _inputHandle.Resolve(_dataVault);
            outputs = _outputHandle.Resolve(_dataVault);
            ladderAups = _ladderAupHandle.Resolve(_dataVault);
            telemetryRing = _telemetryRingHandle.Resolve(_dataVault);
            telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);

            return inputs.IsCreated &&
                   outputs.IsCreated &&
                   ladderAups.IsCreated &&
                   telemetryRing.IsCreated &&
                   telemetryCursor.IsCreated &&
                   inputs.Length >= 1 &&
                   outputs.Length >= 1 &&
                   ladderAups.Length >= 1 &&
                   telemetryRing.Length >= LadderClimbIkConstants.BlackBoxFrameCapacity &&
                   telemetryCursor.Length >= 1;
        }

        private bool TryResolveOutput(out NativeArray<LadderClimbIkOutput> outputs)
        {
            outputs = default;
            if (!EnsureVaultBuffers())
                return false;

            outputs = _outputHandle.Resolve(_dataVault);
            return outputs.IsCreated && outputs.Length >= 1;
        }

        private bool TryResolveLadderAups(out NativeArray<AbsoluteUniversePosition> ladderAups)
        {
            ladderAups = default;
            if (!EnsureVaultBuffers())
                return false;

            ladderAups = _ladderAupHandle.Resolve(_dataVault);
            return ladderAups.IsCreated && ladderAups.Length >= 1;
        }

        private bool TryResolveTelemetryRing(out NativeArray<LadderClimbTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            if (!EnsureVaultBuffers())
                return false;

            telemetryRing = _telemetryRingHandle.Resolve(_dataVault);
            return telemetryRing.IsCreated && telemetryRing.Length > 0;
        }

        private void ClearVaultHandles()
        {
            _inputHandle = default;
            _outputHandle = default;
            _ladderAupHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _dataVault = null;
        }

        private bool TryRegisterTickables()
        {
            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            return _registeredFastTick && _registeredLateFrame;
        }

        private void UnregisterTickables()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredFastTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
        }

        private void ScheduleSolve()
        {
            if (!_active ||
                !TryResolveVaultViews(
                    out NativeArray<LadderClimbIkInput> inputs,
                    out NativeArray<LadderClimbIkOutput> outputs,
                    out NativeArray<AbsoluteUniversePosition> ladderAups,
                    out NativeArray<LadderClimbTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            float3 playerRoot = SanitizeFinite(ToFloat3(_playerRoot.position), float3.zero);
            BuildShoulders(playerRoot, out float3 leftShoulder, out float3 rightShoulder, out float3 leftPole, out float3 rightPole);
            byte flags = LadderClimbIkConstants.FlagActive;
            if (_lowTierCameraSlide)
                flags |= LadderClimbIkConstants.FlagLowTier;
            if (_vrGripRequired)
                flags |= LadderClimbIkConstants.FlagVrGrip;
            if (_pendingSlip)
                flags |= LadderClimbIkConstants.FlagSlip;

            inputs[0] = new LadderClimbIkInput
            {
                PlayerRoot = playerRoot,
                LadderUp = _ladderUp,
                LadderForward = _ladderForward,
                LeftShoulder = leftShoulder,
                RightShoulder = rightShoulder,
                LeftPole = leftPole,
                RightPole = rightPole,
                ProgressMeters = _climbProgressMeters,
                LadderHeightMeters = _climbHeightMeters,
                RungSpacingMeters = LadderClimbIkConstants.DefaultRungSpacingMeters,
                UpperArmMeters = LadderClimbIkConstants.DefaultUpperArmMeters,
                LowerArmMeters = LadderClimbIkConstants.DefaultLowerArmMeters,
                Stamina01 = _stamina01,
                LadderIndex = 0,
                Frame = Time.frameCount,
                Flags = flags
            };

            _solveHandle = new LadderClimbIkSolveJob
            {
                Inputs = inputs,
                LadderAups = ladderAups,
                Outputs = outputs,
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
                CommittedOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble
            }.Schedule();

            _solveScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.GameplayPlayer, _solveHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private float ResolveProgressDelta(float deltaTime)
        {
            if (_vrGripRequired)
            {
                bool hasGrip = _pendingGripMask != 0;
                float pull = hasGrip ? _pendingGripPullMeters : 0f;
                _pendingGripPullMeters = 0f;
                _pendingGripMask = 0;
                return math.clamp(pull, -0.35f, 0.35f);
            }

            float speed = SanitizePositive(pcSlideSpeedMetersPerSecond, DefaultPcSlideSpeedMetersPerSecond);
            return _climbDirection * speed * deltaTime;
        }

        private void DrainStamina(float appliedProgressDelta)
        {
            float drain = math.abs(appliedProgressDelta) * StaminaDrainPerMeter;
            _stamina01 = math.max(0f, _stamina01 - drain);
        }

        private void ApplyPresentationDelta(float appliedProgressDelta, float deltaTime)
        {
            if (math.abs(appliedProgressDelta) <= 0.000001f || !IsFinite(appliedProgressDelta))
                return;

            float invDeltaTime = math.rcp(math.max(deltaTime, 0.0001f));
            float3 delta = SanitizeFinite(_ladderUp * appliedProgressDelta, float3.zero);
            Vector3 velocityDelta = ToVector3(delta * invDeltaTime);
            if (_movementForceSink != null)
            {
                _movementForceSink.QueueExternalVelocityChange(velocityDelta);
                return;
            }

            if (cameraSlideTarget != null)
                cameraSlideTarget.Translate(ToVector3(delta), Space.World);
        }

        private void ApplyIkTargets(in LadderClimbIkOutput output)
        {
            SetTargetPosition(leftHandIkTarget, output.LeftHandTarget);
            SetTargetPosition(rightHandIkTarget, output.RightHandTarget);
            SetTargetPosition(leftElbowIkTarget, output.LeftElbowTarget);
            SetTargetPosition(rightElbowIkTarget, output.RightElbowTarget);
        }

        private void EmitRungContactHaptics(in LadderClimbIkOutput output)
        {
            if (output.LeftRungIndex != _lastLeftRung)
            {
                _lastLeftRung = output.LeftRungIndex;
                EmitHapticThud(0.45f);
            }

            if (output.RightRungIndex != _lastRightRung)
            {
                _lastRightRung = output.RightRungIndex;
                EmitHapticThud(0.4f);
            }
        }

        private static void EmitHapticThud(float intensity01)
        {
            HapticRequest request = new HapticRequest
            {
                Intensity01 = math.saturate(intensity01),
                DurationSeconds = 0.045f,
                Frequency01 = 0.62f,
                SourceHash = LadderClimbIkConstants.SourceHash,
                Frame = (uint)Time.frameCount,
                Channel = HapticRequest.ChannelLightThud,
                Flags = HapticRequest.FlagLightThud
            };
            GlobalSignals.Publish(in request);
        }

        private void PublishClimbState(bool slip)
        {
            if (!TryResolveLadderAups(out NativeArray<AbsoluteUniversePosition> ladderAups))
                return;

            byte flags = PlayerStateSignal.FlagAupShiftSafe;
            if (_active && !slip)
                flags |= (byte)(PlayerStateSignal.FlagActive | PlayerStateSignal.FlagClimbing);
            if (_vrGripRequired)
                flags |= PlayerStateSignal.FlagVrGrip;
            if (_lowTierCameraSlide)
                flags |= PlayerStateSignal.FlagLowTierCameraSlide;
            if (slip)
                flags |= PlayerStateSignal.FlagLadderSlip;

            PlayerStateSignal signal = new PlayerStateSignal
            {
                PositionAup = ladderAups[0],
                Intensity01 = _climbHeightMeters > 0.0001f ? math.saturate(_climbProgressMeters * math.rcp(_climbHeightMeters)) : 0f,
                SourceHash = LadderClimbIkConstants.SourceHash,
                Frame = (uint)Time.frameCount,
                State = PlayerStateSignal.StateClimbing,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);
        }

        private void StopClimb(bool finished, bool slipped)
        {
            if (!_active && !_pendingFinish && !_pendingSlip)
                return;

            _active = false;
            _pendingFinish = false;
            _pendingSlip = false;
            if (finished && !slipped && _matchRotation && _playerRoot != null)
            {
                Transform target = _climbDirection > 0f ? _exitPoint : _entryPoint;
                if (target != null)
                    _playerRoot.rotation = target.rotation;
            }

            if (slipped && _movementForceSink != null)
                _movementForceSink.QueueExternalVelocityChange(new Vector3(0f, SlipVelocityMetersPerSecond, 0f));

            PublishClimbState(slipped);
            _playerRoot = null;
            _ladderTransform = null;
            _entryPoint = null;
            _exitPoint = null;
            _movementForceSink = null;
            _pendingGripPullMeters = 0f;
            _pendingGripMask = 0;
            UnregisterTickables();
        }

        private void CompleteOutstandingJob()
        {
            if (!_solveScheduled)
                return;

            _solveHandle.Complete();
            _solveScheduled = false;
        }

        private void ResolveLadderFrame(Vector3 entryPosition, Vector3 exitPosition, Transform ladderTransform)
        {
            float3 entry = ToFloat3(entryPosition);
            float3 exit = ToFloat3(exitPosition);
            float3 axis = exit - entry;
            float lengthSq = math.lengthsq(axis);
            if (lengthSq <= 0.0001f || !math.isfinite(lengthSq))
            {
                _ladderUp = NormalizeSafe(ToFloat3(ladderTransform.up), new float3(0f, 1f, 0f));
                _climbHeightMeters = 2f;
            }
            else
            {
                _climbHeightMeters = lengthSq * math.rsqrt(lengthSq);
                _ladderUp = axis * math.rcp(math.max(_climbHeightMeters, 0.0001f));
            }

            float3 forward = ToFloat3(ladderTransform.forward);
            forward -= _ladderUp * math.dot(forward, _ladderUp);
            _ladderForward = NormalizeSafe(forward, ResolvePerpendicular(_ladderUp));
        }

        private void BuildShoulders(
            float3 playerRoot,
            out float3 leftShoulder,
            out float3 rightShoulder,
            out float3 leftPole,
            out float3 rightPole)
        {
            float3 right = NormalizeSafe(math.cross(_ladderForward, _ladderUp), new float3(1f, 0f, 0f));
            float3 center = playerRoot + (_ladderUp * ShoulderHeightMeters) - (_ladderForward * ShoulderBacksetMeters);
            leftShoulder = center - right * (ShoulderWidthMeters * 0.5f);
            rightShoulder = center + right * (ShoulderWidthMeters * 0.5f);
            leftPole = leftShoulder + _ladderForward * ElbowPoleMeters;
            rightPole = rightShoulder + _ladderForward * ElbowPoleMeters;
        }

        private void DumpBlackBox()
        {
            if (!TryResolveTelemetryRing(out NativeArray<LadderClimbTelemetryEntry> telemetryRing))
                return;

            string directory = Path.Combine(ResolveProjectRoot(), "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_LADDER_CLIMB_IK.bin");
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(LadderClimbIkConstants.BlackBoxFrameCapacity);
                writer.Write(telemetryRing.Length);
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    LadderClimbTelemetryEntry entry = telemetryRing[i];
                    WriteFloat3(writer, entry.PlayerRoot);
                    WriteFloat3(writer, entry.LeftHandTarget);
                    WriteFloat3(writer, entry.RightHandTarget);
                    WriteFloat3(writer, entry.LeftElbowTarget);
                    WriteFloat3(writer, entry.RightElbowTarget);
                    writer.Write(entry.ProgressMeters);
                    writer.Write(entry.Stamina01);
                    writer.Write(entry.LeftRungIndex);
                    writer.Write(entry.RightRungIndex);
                    writer.Write(entry.Frame);
                    writer.Write(entry.Hash);
                    writer.Write(entry.Flags);
                }
            }
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo dataDirectory = new DirectoryInfo(Application.dataPath);
            return dataDirectory.Parent != null ? dataDirectory.Parent.FullName : Application.dataPath;
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static void SetTargetPosition(Transform target, float3 position)
        {
            if (target != null)
                target.position = ToVector3(position);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return deltaTime > 0f && math.isfinite(deltaTime) ? math.min(deltaTime, 0.05f) : 0.0166667f;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return value > 0.0001f && math.isfinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.0000001f || !math.isfinite(lengthSq))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float3 ResolvePerpendicular(float3 direction)
        {
            float3 axis = math.abs(direction.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return NormalizeSafe(math.cross(direction, axis), new float3(1f, 0f, 0f));
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
