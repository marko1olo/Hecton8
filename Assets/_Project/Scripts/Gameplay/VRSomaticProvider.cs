using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// VR-only somatic suit provider. PC/console code reads <see cref="IVRSomaticProvider"/> through GlobalRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9915)]
    [AddComponentMenu("Hecton8/Gameplay/VR Somatic Provider")]
    public sealed class VRSomaticProvider : MonoBehaviour, IVRSomaticProvider, IUpdatable, ILateFrameTickable, IOriginShiftListener
    {
        private const int HeadCollisionCommandCount = 6;
        private const int HeadCollisionMaxHitsPerCommand = 1;
        private const int HandCount = 2;
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float HorizonLockStartSinSq = 0.0669873f;
        private const float HorizonLockMaxCorrectionRadians = 0.2617994f;
        private const float MinimumDeltaTime = 0.0001f;
        private const float ShaderPublishEpsilon = 0.0001f;
        private const float AudioPublishEpsilon = 0.001f;
        private const float LowPassPublishEpsilonHz = 1f;
        private const float PlayerSignalSampleIntervalSeconds = 0.05f;
        private const float QuaternionLengthSqMinimum = 0.25f;
        private const float QuaternionLengthSqMaximum = 2.25f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;
        private const float HitNormalLengthSqMinimum = 0.25f;
        private const float HitNormalLengthSqMaximum = 2.25f;
        private const float HitNormalUnitLengthSqEpsilon = 0.015625f;
        private const float MinimumNearFieldDistanceMeters = 0.01f;
        private const float MinimumHeadCapsuleRadiusMeters = 0.01f;
        private const float MinimumHeadCapsuleHalfHeightMeters = 0.01f;
        private const float MinimumImpactDebounceSeconds = 0.02f;
        private const float HeadTrackingJumpDistanceMetersSq = 1.44f;
        private const float HeadTrackingJumpAngularRadians = 1.35f;
        private const float MaxSomaticHeadLinearSpeedMetersPerSecond = 12f;
        private const float MaxSomaticHeadAngularSpeedRadiansPerSecond = 18f;
        private const float MaxSomaticHeadAngularJerkRadiansPerSecondCubed = 1440f;
        private const byte HapticPriorityComfort = 2;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte HapticBlendAdditive = ToolHapticsRuntime.BlendModeAdditive;
        private const float AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersFloat;
        private const float HapticSideThreshold = 0.2f;
        private const float JerkEventDebounceSeconds = 0.2f;
        private const float VrComfortTelemetryStep01 = 0.05f;
        private const float Quest2ComfortVignetteMaximum = 0.52f;
        private const float Quest2ComfortAccelerationSoftTunnelStartRadS2 = 42f;
        private const float Quest2ComfortAccelerationEmergencyClampRadS2 = 150f;
        private const float Quest2ComfortAccelerationReleaseBelowRadS2 = 24f;
        private const float Quest2ComfortAccelerationReleaseHysteresisSeconds = 0.25f;
        private const float Quest2ComfortVignetteAttackSlewPerFrame = 0.055f;
        private const float Quest2ComfortVignetteReleaseSlewPerFrame = 0.025f;
        private const float Quest2ComfortFrameSafetyDeltaSeconds = 0.01667f;
        private const float Quest2ComfortFrameSafetyMinOpacity = 0.12f;
        private const int Quest2ComfortFrameSafetyConsecutiveFrames = 2;
        private const int Quest2ComfortFrameSafetyReleaseStableFrames = 12;
        private const float Quest3ComfortFrameSafetyDeltaSeconds = 0.01389f;
        private const float Quest3ComfortFrameSafetyMinOpacity = 0.10f;
        private const int Quest3ComfortFrameSafetyConsecutiveFrames = 2;
        private const int Quest3ComfortFrameSafetyReleaseStableFrames = 12;
        private const uint VrComfortTelemetryContextHash = 0x56524346u; // VRCF
        private const uint VrComfortJerkEventHash = 0x4A524B31u; // JRK1
        private const uint VrComfortMaxVignetteHash = 0x4D565231u; // MVR1
        private const uint BlackBoxMagic = 0x5652534Du; // VRSM
        private const uint BlackBoxVersion = 1u;
        private const int BlackBoxFrameCapacity = 300;
        private const ushort BlackBoxFlagActive = 1 << 0;
        private const ushort BlackBoxFlagNonFinite = 1 << 1;
        private const ushort BlackBoxFlagLeftGhost = 1 << 2;
        private const ushort BlackBoxFlagRightGhost = 1 << 3;
        private const ushort BlackBoxFlagRootJobScheduled = 1 << 4;
        private const ushort BlackBoxFlagHandJobScheduled = 1 << 5;
        private const ushort BlackBoxFlagNearCollision = 1 << 6;
        private const ushort BlackBoxFlagAupShiftSeen = 1 << 7;
        private const ushort BlackBoxFlagLowTier = 1 << 8;
        private const ushort BlackBoxFlagFramePressure = 1 << 9;
        private const ushort BlackBoxFlagQuest2Fallback = 1 << 10;
        private const ushort BlackBoxFlagAccelerationTunnel = 1 << 11;
        private const string BlackBoxDumpFileName = "Dump_VR_SOMATIC_ENGINEER.bin";
        private const uint StateHeadCollisionScheduled = 1u << 0;
        private const uint StateRegisteredService = 1u << 1;
        private const uint StateRegisteredUpdate = 1u << 2;
        private const uint StateRegisteredLateFrame = 1u << 3;
        private const uint StateHasPreviousHeadPose = 1u << 4;
        private const uint StateSubscribedXRRuntime = 1u << 5;
        private const uint StateBreathingAudioStaticApplied = 1u << 6;
        private const uint StateBreathingLowPassStaticApplied = 1u << 7;
        private const uint StateBreathingSourcePlaying = 1u << 8;
        private const uint StateRootSyncScheduled = 1u << 9;
        private const uint StateHandKinematicsScheduled = 1u << 10;
        private const uint StateHandsInitialized = 1u << 11;
        private const uint StateRootInitialized = 1u << 12;

        private static readonly int NearCollisionIntensityId = Shader.PropertyToID("_HectonVRNearCollisionIntensity");
        private static readonly int SomaticCondensationId = Shader.PropertyToID("_HectonVRSomaticCondensation");
        private static readonly int SomaticStateId = Shader.PropertyToID("_HectonVRSomaticState");
        private static readonly int VrComfortJerkStateId = Shader.PropertyToID("_HectonVRComfortJerkState");
        private static readonly int VrComfortVignetteId = Shader.PropertyToID("_VRComfortVignette");

        [Header("Rig")]
        [SerializeField] private Transform hmdTransform;
        [SerializeField] private Transform visorHudRoot;
        [SerializeField] private Transform pdaChestSocket;
        [SerializeField] private Transform flareToolChestSocket;

        [Header("Collision")]
        [SerializeField] private LayerMask nearFieldCollisionMask =
            HectonLayerMasks.BaseModuleLayerMask |
            HectonLayerMasks.VoxelCaveLayerMask |
            HectonLayerMasks.TerrainLayerMask;
        [SerializeField, Range(0.05f, 0.25f)] private float nearFieldDistanceMeters = 0.15f;
        [SerializeField, Range(0.02f, 0.12f)] private float headCapsuleRadiusMeters = 0.055f;
        [SerializeField, Range(0.01f, 0.12f)] private float headCapsuleHalfHeightMeters = 0.045f;
        [SerializeField, Range(1f, 60f)] private float nearFieldFadeSharpness = 22f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 8f)] private float impactSpeedThresholdMetersPerSecond = 2f;
        [SerializeField, Range(0.02f, 0.35f)] private float impactHapticDurationSeconds = 0.14f;
        [SerializeField, Range(0.5f, 10f)] private float impactHapticDecayRate = 4.4f;
        [SerializeField, Range(0.02f, 0.25f)] private float impactHapticDebounceSeconds = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maxLowFrequencyImpact = 0.55f;
        [SerializeField, Range(0f, 1f)] private float maxHighFrequencyImpact = 0.88f;

        [Header("Helmet")]
        [SerializeField] private bool applyVisorHudHeadLag = true;
        [SerializeField, Range(0f, 1f)] private float visorLagMaximumBlend = 0.62f;
        [SerializeField, Range(0.25f, 12f)] private float visorLagAngularSpeedForFull = 5.25f;
        [SerializeField, Range(30f, 960f), Tooltip("Angular jerk where VR comfort clamps visor HUD response.")]
        private float rotationJerkLimitRadiansPerSecondCubed = 320f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum extra visor HUD blend used to cull rotation jerk.")]
        private float rotationJerkCullMaximumBlend = 0.42f;
        [SerializeField, Range(1f, 40f), Tooltip("Smoothing sharpness for rotation jerk comfort culling.")]
        private float rotationJerkCullSharpness = 18f;
        [SerializeField, Range(0f, 1f), Tooltip("Extra vignette contributed by severe rotation jerk.")]
        private float rotationJerkVignetteContribution = 0.28f;
        [SerializeField, Range(2f, 40f), Tooltip("Smoothing sharpness for the decoupled somatic root.")]
        private float rootRotationSmoothingSharpness = 14f;
        [SerializeField, Range(0.25f, 12f), Tooltip("Head angular speed where the comfort vignette begins.")]
        private float comfortVignetteAngularSpeedStart = 2.6f;
        [SerializeField, Range(1f, 18f), Tooltip("Head angular speed where the comfort vignette reaches full value.")]
        private float comfortVignetteAngularSpeedFull = 8f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum scalar written to the VR comfort vignette globals.")]
        private float comfortVignetteMaximum = 0.46f;
        [SerializeField, Range(10f, 180f), Tooltip("Angular acceleration where the Quest 3 somatic tunnel starts.")]
        private float comfortAccelerationSoftTunnelStartRadS2 = 50f;
        [SerializeField, Range(20f, 240f), Tooltip("Angular acceleration where the Quest 3 somatic tunnel reaches maximum opacity.")]
        private float comfortAccelerationEmergencyClampRadS2 = 180f;
        [SerializeField, Range(0f, 120f), Tooltip("Angular acceleration below which the acceleration tunnel can release after hysteresis.")]
        private float comfortAccelerationReleaseBelowRadS2 = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("Seconds acceleration must stay below release threshold before tunnel release.")]
        private float comfortAccelerationReleaseHysteresisSeconds = 0.22f;
        [SerializeField, Range(0.001f, 0.1f), Tooltip("Maximum acceleration tunnel opacity increase per VR frame.")]
        private float comfortVignetteAttackSlewPerFrame = 0.05f;
        [SerializeField, Range(0.001f, 0.1f), Tooltip("Maximum acceleration tunnel opacity decrease per VR frame.")]
        private float comfortVignetteReleaseSlewPerFrame = 0.022f;

        [Header("Chest Sockets")]
        [SerializeField] private Vector3 pdaChestOffset = new Vector3(-0.18f, -0.34f, 0.22f);
        [SerializeField] private Vector3 pdaChestRotationEuler = new Vector3(8f, -12f, -6f);
        [SerializeField] private Vector3 flareToolChestOffset = new Vector3(0.18f, -0.36f, 0.19f);
        [SerializeField] private Vector3 flareToolChestRotationEuler = new Vector3(10f, 14f, 8f);
        [SerializeField, Range(3f, 12f), Tooltip("Head speed where a short somatic haptic anchor begins.")]
        private float velocityHapticThresholdMetersPerSecond = 5f;
        [SerializeField, Range(0.03f, 0.25f), Tooltip("Minimum seconds between velocity haptic anchors.")]
        private float velocityHapticIntervalSeconds = 0.12f;
        [SerializeField, Range(0.01f, 0.2f), Tooltip("Duration of each velocity haptic anchor pulse.")]
        private float velocityHapticDurationSeconds = 0.075f;

        [Header("Physical Hands")]
        [SerializeField, Range(2f, 80f)] private float handSpringForce = 24f;
        [SerializeField, Range(0.08f, 0.5f)] private float ghostHandDistanceMeters = 0.2f;
        [SerializeField] private bool disableGhostHandsOnLowTier = true;

        [Header("Breathing Audio")]
        [SerializeField] private AudioSource breathingSource;
        [SerializeField] private AudioLowPassFilter breathingLowPassFilter;
        [SerializeField, Range(0f, 1f)] private float breathingBaseVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float breathingStressVolume = 0.46f;
        [SerializeField, Range(0.5f, 2f)] private float breathingMinimumPitch = 0.92f;
        [SerializeField, Range(0.5f, 2f)] private float breathingMaximumPitch = 1.22f;
        [SerializeField, Range(200f, 22000f)] private float breathingLowPassOpenHz = 18000f;
        [SerializeField, Range(200f, 22000f)] private float breathingLowPassClosedHz = 680f;

        private NativeArray<CapsulecastCommand> _headCollisionCommands;
        private NativeArray<RaycastHit> _headCollisionHits;
        private NativeArray<HeadCastSample> _headCollisionSamples;
        private NativeArray<VRSomaticRootSyncInput> _rootSyncInput;
        private NativeArray<VRSomaticRootSyncOutput> _rootSyncOutput;
        private NativeArray<float3> HandTargets;
        private NativeArray<float3> HandPhysicalPositions;
        private NativeArray<VRSomaticBlackBoxEntry> _blackBox;
        private JobHandle _headCollisionHandle;
        private JobHandle _rootSyncHandle;
        private JobHandle _handKinematicsHandle;
        private JobHandle _headCollisionDisposeHandle;
        private uint _stateFlags;
        private Vector3 _previousHeadPosition;
        private Quaternion _previousHeadRotation = Quaternion.identity;
        private Quaternion _headRotationFrame1 = Quaternion.identity;
        private Quaternion _headRotationFrame2 = Quaternion.identity;
        private Quaternion _headRotationFrame3 = Quaternion.identity;
        private Quaternion _torsoRotation = Quaternion.identity;
        private Quaternion _pdaSocketLocalRotation = Quaternion.identity;
        private Quaternion _flareSocketLocalRotation = Quaternion.identity;
        private quaternion _lastRootRotation = quaternion.identity;
        private Transform _fallbackHmdTransform;
        private Transform _decoupledRootTransform;
        private float _headLinearSpeedMetersPerSecond;
        private float _headAngularSpeedRadiansPerSecond;
        private float _headAngularAccelerationRadiansPerSecondSq;
        private float3 _previousHeadAngularVelocityRadiansPerSecond;
        private float3 _previousHeadAngularAccelerationRadiansPerSecondSq;
        private float _headAngularJerkRadiansPerSecondCubed;
        private float _headAngularJerk01;
        private float _accelerationComfortVignette01;
        private float _accelerationReleaseBelowTimer;
        private int _comfortFramePressureConsecutiveFrames;
        private int _comfortFramePressureStableFrames;
        private float _jerkCullBlend01;
        private uint _jerkEventCount;
        private uint _lastTelemetryJerkEventCount;
        private float _jerkEventCooldownRemaining;
        private float _maxSomaticComfortVignetteTelemetry01;
        private float _lastSomaticComfortVignetteTelemetry01;
        private float _velocityHapticCooldownRemaining;
        private float _lastTickDeltaTime;
        private float _impactHapticCooldownRemaining;
        private float _nearFieldCollision01;
        private float _playerStress01;
        private float _oxygen01 = 1f;
        private float _depthMeters;
        private float _condensation01;
        private float _lastPublishedNearCollision01 = float.PositiveInfinity;
        private float _lastPublishedCondensation01 = float.PositiveInfinity;
        private float _lastPublishedBreathingVolume = float.PositiveInfinity;
        private float _lastPublishedBreathingPitch = float.PositiveInfinity;
        private float _lastPublishedBreathingLowPassHz = float.PositiveInfinity;
        private float _lastPublishedBreathingLowPassQ = float.PositiveInfinity;
        private float _lastPublishedComfortVignette01 = float.PositiveInfinity;
        private float _playerSignalSampleRemaining;
        private Vector4 _lastPublishedSomaticState = Vector4.positiveInfinity;
        private Vector4 _lastPublishedJerkState = Vector4.positiveInfinity;
        private uint _lastObservedAupShiftSequence;
        private uint _handGhostMask;
        private int _blackBoxCursor;
        private int _blackBoxLastRecordedFrame = -1;
        private bool _blackBoxDumped;
        private bool _comfortFramePressureActive;
        private bool _useQuest2ComfortFallback;
        private VRSomaticChestSocketPose _pdaSocketPose;
        private VRSomaticChestSocketPose _flareSocketPose;
        private VRSomaticCollisionState _collisionState;
        private VRSomaticSnapshot _snapshot = VRSomaticSnapshot.Inactive;

        public bool IsActive => _snapshot.IsActive;
        public VRSomaticSnapshot CurrentSnapshot => _snapshot;
        public uint HandGhostMask => _handGhostMask;

        public void BindRig(
            Transform hmdTransform,
            Transform visorHudRoot,
            Transform pdaChestSocket,
            Transform flareToolChestSocket,
            AudioSource breathingSource,
            AudioLowPassFilter breathingLowPassFilter)
        {
            AudioSource resolvedBreathingSource = breathingSource != null ? breathingSource : this.breathingSource;
            AudioLowPassFilter resolvedLowPassFilter = breathingLowPassFilter != null ? breathingLowPassFilter : this.breathingLowPassFilter;
            bool breathingBindingChanged =
                !ReferenceEquals(this.breathingSource, resolvedBreathingSource) ||
                !ReferenceEquals(this.breathingLowPassFilter, resolvedLowPassFilter);
            if (breathingBindingChanged && this.breathingSource != null && (_stateFlags & StateBreathingSourcePlaying) != 0u)
            {
                this.breathingSource.Stop();
                _stateFlags &= ~StateBreathingSourcePlaying;
            }
            if (breathingBindingChanged &&
                this.breathingLowPassFilter != null &&
                !ReferenceEquals(this.breathingLowPassFilter, resolvedLowPassFilter) &&
                this.breathingLowPassFilter.enabled)
            {
                this.breathingLowPassFilter.enabled = false;
            }

            this.hmdTransform = hmdTransform;
            this.visorHudRoot = visorHudRoot;
            this.pdaChestSocket = pdaChestSocket;
            this.flareToolChestSocket = flareToolChestSocket;
            this.breathingSource = resolvedBreathingSource;
            this.breathingLowPassFilter = resolvedLowPassFilter;
            _fallbackHmdTransform = null;
            if (breathingBindingChanged)
                ResetBreathingAudioPublishCache();
        }

        public void BindDecoupledRoot(Transform vrRootTransform)
        {
            _decoupledRootTransform = vrRootTransform;
            _lastObservedAupShiftSequence = 0u;
        }

        public bool TryGetChestSocket(VRSomaticChestSocketId socketId, out VRSomaticChestSocketPose socketPose)
        {
            if (!_snapshot.IsActive)
            {
                socketPose = default;
                return false;
            }

            socketPose = socketId == VRSomaticChestSocketId.FlareTool
                ? _flareSocketPose
                : _pdaSocketPose;
            return true;
        }

        public bool TryGetHandPose(byte handIndex, out VRSomaticHandPose handPose)
        {
            handPose = default;
            if (!_snapshot.IsActive ||
                handIndex >= HandCount ||
                !HandTargets.IsCreated ||
                !HandPhysicalPositions.IsCreated ||
                (_stateFlags & StateHandKinematicsScheduled) != 0u)
            {
                return false;
            }

            float3 target = HandTargets[handIndex];
            float3 physical = HandPhysicalPositions[handIndex];
            float3 separation = target - physical;
            float separationSq = math.lengthsq(separation);
            if (!IsFiniteFloat3(target) || !IsFiniteFloat3(physical) || !math.isfinite(separationSq))
                return false;

            InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;
            bool hasTracking = dispatcher != null &&
                               dispatcher.TryGetXRInputState(handIndex, out XRInputState state) &&
                               state.IsTracked != 0;
            bool ghostVisible = (_handGhostMask & (1u << handIndex)) != 0u;
            handPose = new VRSomaticHandPose(
                handIndex,
                hasTracking,
                ghostVisible,
                ToVector3(target),
                ToVector3(physical),
                separationSq);
            return true;
        }

        public bool TryGetNearFieldCollision(out VRSomaticCollisionState collisionState)
        {
            collisionState = _collisionState;
            return _snapshot.IsActive && _collisionState.HasContact && _collisionState.Intensity01 > 0.001f;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            DispatcherJobSwap.TryComplete(ref _headCollisionHandle, true);
            DispatcherJobSwap.TryComplete(ref _rootSyncHandle, true);
            DispatcherJobSwap.TryComplete(ref _handKinematicsHandle, true);
            _stateFlags &= ~(StateHeadCollisionScheduled | StateRootSyncScheduled | StateHandKinematicsScheduled | StateHasPreviousHeadPose | StateRootInitialized);
            _lastObservedAupShiftSequence = shiftData.Sequence;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
            _jerkEventCooldownRemaining = 0f;
            _nearFieldCollision01 = 0f;
            _collisionState = default;
            _lastPublishedNearCollision01 = float.PositiveInfinity;
            PublishComfortVignette(0f);
            PublishShaderState();

            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!IsFiniteVector(shiftOffset))
                return;

            float3 shift = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            RebaseHandArray(HandTargets, shift);
            RebaseHandArray(HandPhysicalPositions, shift);
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _lastTickDeltaTime = safeDeltaTime;
            AdvanceSomaticTimers(safeDeltaTime);

            if (!TryResolveActiveHmd(out Transform activeHmd))
            {
                ApplyInactiveState(safeDeltaTime);
                RefreshLateFrameRegistration();
                return;
            }

            activeHmd.GetPositionAndRotation(out Vector3 headPosition, out Quaternion headRotation);
            bool hasFiniteHeadPosition = IsFiniteVector(headPosition);
            bool hasFiniteHeadRotation = TrySanitizeQuaternion(headRotation, out Quaternion sanitizedHeadRotation);
            if (!hasFiniteHeadPosition || !hasFiniteHeadRotation)
            {
                RecordBlackBoxFrame(headPosition, hasFiniteHeadRotation ? sanitizedHeadRotation : headRotation, BlackBoxFlagNonFinite);
                ApplyInactiveState(safeDeltaTime);
                RefreshLateFrameRegistration();
                return;
            }

            headRotation = sanitizedHeadRotation;
            AbsoluteUniversePosition headAup = HectonXRRuntimeState.TryResolveCachedHeadAup(headPosition, out AbsoluteUniversePosition cachedHeadAup)
                ? cachedHeadAup
                : AbsoluteUniversePosition.FromRuntimePosition(headPosition);
            ResetHeadMotionIfAupShifted(headPosition, headRotation);
            UpdateHeadMotion(headPosition, headRotation, safeDeltaTime);
            ScheduleRootSync(headPosition, headRotation, safeDeltaTime);
            ScheduleHandKinematics(headPosition, headRotation, safeDeltaTime);
            RefreshPlayerSignalsIfDue();
            UpdateChestSockets(in headAup, headRotation);
            Quaternion visorRotation = ResolveVisorHudRotation(headPosition, headRotation);
            RefreshNearFieldCollisionQueryAvailability(safeDeltaTime);
            UpdateBreathingAudio();
            UpdateCondensation();
            PublishSnapshot(in headAup, headPosition, headRotation, visorRotation);
            PublishShaderState();
            TryEmitVelocityAnchorHaptics();
            PublishComfortTelemetry();
            ScheduleHeadCollisionBatch(headPosition, headRotation);
            RecordBlackBoxFrame(headPosition, headRotation, 0);
        }

        public void LateFrameTick()
        {
            CompleteRootSyncIfReady();
            CompleteHandKinematicsIfReady();

            if ((_stateFlags & StateHeadCollisionScheduled) == 0u)
            {
                RefreshLateFrameRegistration();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _headCollisionHandle, false))
            {
                FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                PublishShaderState();
                return;
            }

            _stateFlags &= ~StateHeadCollisionScheduled;
            if (!_snapshot.IsActive || !CanRunHeadCollisionQuery())
            {
                FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                PublishShaderState();
                RefreshLateFrameRegistration();
                return;
            }

            ConsumeHeadCollisionSamples();
            PublishShaderState();
            RefreshLateFrameRegistration();
        }

        private void Awake()
        {
            CacheSocketRotations();
        }

        private void OnEnable()
        {
            CacheSocketRotations();
            TrySubscribeXRRuntime();
            HectonFloatingOrigin.RegisterListener(this);
            RefreshRuntimeRegistration(IsVRSomaticRuntimeActive());
        }

        private void OnDisable()
        {
            ReleaseRuntimeState();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeState();
        }

        private void ReleaseRuntimeState()
        {
            if (!Application.isPlaying)
            {
                DisposeNativeBuffers();
                return;
            }

            bool hadRuntimeState = HasRuntimeRegistrationOrActiveSnapshot();
            TryUnsubscribeXRRuntime();
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
            ApplyInactiveState(0f, hadRuntimeState);
            DisposeNativeBuffers();
        }

        private void OnValidate()
        {
            CacheSocketRotations();
        }

        private void TrySubscribeXRRuntime()
        {
            if ((_stateFlags & StateSubscribedXRRuntime) != 0u || !Application.isPlaying)
                return;

            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            _stateFlags |= StateSubscribedXRRuntime;
        }

        private void TryUnsubscribeXRRuntime()
        {
            if ((_stateFlags & StateSubscribedXRRuntime) == 0u)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _stateFlags &= ~StateSubscribedXRRuntime;
        }

        private void HandleXRActiveChanged(bool isActive)
        {
            RefreshRuntimeRegistration(isActive);
        }

        private void RefreshRuntimeRegistration(bool isActive)
        {
            if (!Application.isPlaying)
                return;

            if (!isActive)
            {
                _useQuest2ComfortFallback = false;
                bool hadRuntimeState = HasRuntimeRegistrationOrActiveSnapshot();
                TryUnregisterLateFrame();
                TryUnregisterUpdate();
                TryUnregisterService();
                ApplyInactiveState(0f, hadRuntimeState);
                DisposeNativeBuffers();
                return;
            }

            RefreshComfortProfileSelection();
            EnsureNativeBuffers();
            TryRegisterService();
            TryRegisterUpdate();
            RefreshLateFrameRegistration();
        }

        private void TryRegisterService()
        {
            if ((_stateFlags & StateRegisteredService) != 0u || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterVRSomaticProvider(this);
            _stateFlags |= StateRegisteredService;
        }

        private void TryUnregisterService()
        {
            if ((_stateFlags & StateRegisteredService) == 0u)
                return;

            GlobalRegistry.UnregisterVRSomaticProvider(this);
            _stateFlags &= ~StateRegisteredService;
        }

        private void TryRegisterUpdate()
        {
            if ((_stateFlags & StateRegisteredUpdate) != 0u || !Application.isPlaying)
                return;

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player))
                _stateFlags |= StateRegisteredUpdate;
        }

        private void TryUnregisterUpdate()
        {
            if ((_stateFlags & StateRegisteredUpdate) == 0u)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _stateFlags &= ~StateRegisteredUpdate;
        }

        private void TryRegisterLateFrame()
        {
            if ((_stateFlags & StateRegisteredLateFrame) != 0u || !Application.isPlaying)
                return;

            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player))
                _stateFlags |= StateRegisteredLateFrame;
        }

        private void TryUnregisterLateFrame()
        {
            if ((_stateFlags & StateRegisteredLateFrame) == 0u)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _stateFlags &= ~StateRegisteredLateFrame;
        }

        private void RefreshLateFrameRegistration()
        {
            if (!Application.isPlaying)
                return;

            if (ShouldKeepLateFrameRegistered())
                TryRegisterLateFrame();
            else
                TryUnregisterLateFrame();
        }

        private bool ShouldKeepLateFrameRegistered()
        {
            return (_stateFlags & (StateHeadCollisionScheduled | StateRootSyncScheduled | StateHandKinematicsScheduled)) != 0u ||
                   (_snapshot.IsActive && CanRunHeadCollisionQuery());
        }

        private bool HasRuntimeRegistrationOrActiveSnapshot()
        {
            const uint runtimeMask =
                StateHeadCollisionScheduled |
                StateRootSyncScheduled |
                StateHandKinematicsScheduled |
                StateRegisteredService |
                StateRegisteredUpdate |
                StateRegisteredLateFrame;
            return (_stateFlags & runtimeMask) != 0u || _snapshot.IsActive;
        }

        private bool TryResolveActiveHmd(out Transform activeHmd)
        {
            activeHmd = hmdTransform;
            if (!Application.isPlaying || !IsVRSomaticRuntimeActive())
                return false;

            if (activeHmd != null)
                return true;

            Camera playerCamera = null;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                playerCamera = runtimeContext.PlayerCamera;

            if (playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            if (playerCamera == null)
            {
                _fallbackHmdTransform = null;
                return false;
            }

            Transform resolvedHmd = playerCamera.transform;
            if (!ReferenceEquals(_fallbackHmdTransform, resolvedHmd))
                _fallbackHmdTransform = resolvedHmd;

            activeHmd = _fallbackHmdTransform;
            return activeHmd != null;
        }

        private static bool IsVRSomaticRuntimeActive()
        {
            return HectonXRRuntimeState.IsXRActive;
        }

        private void RefreshComfortProfileSelection()
        {
            _useQuest2ComfortFallback = IsQuest2LikeRuntime();
        }

        private static bool IsQuest2LikeRuntime()
        {
            if (HardwareTierDetector.IsQuest3Like)
                return false;

            return ContainsIgnoreCase(SystemInfo.deviceModel, "quest 2") ||
                   ContainsIgnoreCase(SystemInfo.deviceModel, "quest2") ||
                   ContainsIgnoreCase(SystemInfo.deviceName, "quest 2") ||
                   ContainsIgnoreCase(SystemInfo.deviceName, "quest2") ||
                   ContainsIgnoreCase(SystemInfo.operatingSystem, "quest 2");
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool TrySanitizeQuaternion(Quaternion value, out Quaternion sanitized)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) ||
                !math.isfinite(lengthSq) ||
                lengthSq < QuaternionLengthSqMinimum ||
                lengthSq > QuaternionLengthSqMaximum)
            {
                sanitized = Quaternion.identity;
                return false;
            }

            if (math.abs(lengthSq - 1f) > QuaternionUnitLengthSqEpsilon)
                q *= ApproximateInverseLengthNoSqrt(lengthSq);

            sanitized = new Quaternion(q.x, q.y, q.z, q.w);
            return true;
        }

        private void UpdateHeadMotion(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if ((_stateFlags & StateHasPreviousHeadPose) == 0u)
            {
                ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
                return;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, MinimumDeltaTime) : MinimumDeltaTime;
            float invSafeDeltaTime = math.rcp(safeDeltaTime);
            Vector3 headDelta = headPosition - _previousHeadPosition;
            float headDeltaSq =
                (headDelta.x * headDelta.x) +
                (headDelta.y * headDelta.y) +
                (headDelta.z * headDelta.z);
            float angularDelta = ApproximateAngularDeltaRadiansNoAcos(_previousHeadRotation, headRotation);
            if (!math.isfinite(headDeltaSq) ||
                headDeltaSq > HeadTrackingJumpDistanceMetersSq ||
                angularDelta > HeadTrackingJumpAngularRadians)
            {
                ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
                return;
            }

            _headLinearSpeedMetersPerSecond = math.min(
                ApproximateMagnitudeNoSqrt(headDelta) * invSafeDeltaTime,
                MaxSomaticHeadLinearSpeedMetersPerSecond);

            float3 headAngularVelocity = ResolveAngularVelocityRadiansPerSecond(
                _previousHeadRotation,
                headRotation,
                angularDelta,
                invSafeDeltaTime);
            float angularSpeed = ApproximateMagnitudeNoSqrt(headAngularVelocity);
            if (angularSpeed > MaxSomaticHeadAngularSpeedRadiansPerSecond)
            {
                float clampScale = MaxSomaticHeadAngularSpeedRadiansPerSecond * math.rcp(math.max(angularSpeed, 0.0001f));
                headAngularVelocity *= clampScale;
                angularSpeed = MaxSomaticHeadAngularSpeedRadiansPerSecond;
            }

            _headAngularSpeedRadiansPerSecond = angularSpeed;
            UpdateRotationJerkState(safeDeltaTime, headAngularVelocity);

            _headRotationFrame3 = _headRotationFrame2;
            _headRotationFrame2 = _headRotationFrame1;
            _headRotationFrame1 = headRotation;
            _previousHeadPosition = headPosition;
            _previousHeadRotation = headRotation;
        }

        private void ResetHeadMotionHistory(Vector3 headPosition, Quaternion headRotation)
        {
            _previousHeadPosition = headPosition;
            _previousHeadRotation = headRotation;
            _headRotationFrame1 = headRotation;
            _headRotationFrame2 = headRotation;
            _headRotationFrame3 = headRotation;
            _stateFlags |= StateHasPreviousHeadPose;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
        }

        private void ResetHeadMotionHistoryAndPublishedComfort(Vector3 headPosition, Quaternion headRotation)
        {
            ResetHeadMotionHistory(headPosition, headRotation);
            PublishComfortVignette(0f);
            PublishShaderState();
        }

        private void ResetHeadMotionIfAupShifted(Vector3 headPosition, Quaternion headRotation)
        {
            uint currentShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            if (_lastObservedAupShiftSequence == currentShiftSequence)
                return;

            _lastObservedAupShiftSequence = currentShiftSequence;
            ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
        }

        private void UpdateRotationJerkState(float deltaTime, float3 headAngularVelocityRadiansPerSecond)
        {
            float safeDeltaTime = math.max(deltaTime, MinimumDeltaTime);
            float invSafeDeltaTime = math.rcp(safeDeltaTime);
            float3 angularVelocity = SanitizeFiniteFloat3(headAngularVelocityRadiansPerSecond);
            float3 angularAcceleration = (angularVelocity - _previousHeadAngularVelocityRadiansPerSecond) * invSafeDeltaTime;
            if (!IsFiniteFloat3(angularAcceleration))
                angularAcceleration = float3.zero;
            float angularAccelerationMagnitude = ApproximateMagnitudeNoSqrt(angularAcceleration);
            if (!math.isfinite(angularAccelerationMagnitude))
                angularAccelerationMagnitude = 0f;
            _headAngularAccelerationRadiansPerSecondSq = angularAccelerationMagnitude;
            UpdateAccelerationComfortState(safeDeltaTime, angularAccelerationMagnitude);

            float3 angularJerkVector = (angularAcceleration - _previousHeadAngularAccelerationRadiansPerSecondSq) * invSafeDeltaTime;
            float angularJerk = ApproximateMagnitudeNoSqrt(angularJerkVector);
            if (!math.isfinite(angularJerk))
                angularJerk = 0f;

            _headAngularJerkRadiansPerSecondCubed = math.min(angularJerk, MaxSomaticHeadAngularJerkRadiansPerSecondCubed);
            float jerkLimit = SanitizeMinimum(rotationJerkLimitRadiansPerSecondCubed, 1f);
            float targetJerk01 = math.saturate(_headAngularJerkRadiansPerSecondCubed * math.rcp(jerkLimit));
            float blend = ResolveCinematicBlendApprox(SanitizeMinimum(rotationJerkCullSharpness, 1f), safeDeltaTime);
            _headAngularJerk01 = math.lerp(_headAngularJerk01, targetJerk01, blend);
            if (targetJerk01 >= 1f && _jerkEventCooldownRemaining <= 0f)
            {
                _jerkEventCount++;
                _jerkEventCooldownRemaining = JerkEventDebounceSeconds;
            }

            _previousHeadAngularVelocityRadiansPerSecond = angularVelocity;
            _previousHeadAngularAccelerationRadiansPerSecondSq = angularAcceleration;
        }

        private void UpdateAccelerationComfortState(float deltaTime, float angularAccelerationRadS2)
        {
            UpdateComfortFramePressureState(deltaTime);

            float softStart = SanitizeMinimum(ResolveComfortAccelerationSoftTunnelStartRadS2(), 0.01f);
            float emergencyClamp = math.max(softStart + 0.01f, SanitizeMinimum(ResolveComfortAccelerationEmergencyClampRadS2(), softStart + 0.01f));
            float releaseBelow = math.min(softStart, SanitizeNonNegative(ResolveComfortAccelerationReleaseBelowRadS2()));
            float hysteresisSeconds = SanitizeNonNegative(ResolveComfortAccelerationReleaseHysteresisSeconds());
            float safeAcceleration = SanitizeNonNegative(angularAccelerationRadS2);

            if (safeAcceleration <= releaseBelow)
                _accelerationReleaseBelowTimer = math.min(hysteresisSeconds, _accelerationReleaseBelowTimer + math.max(deltaTime, 0f));
            else
                _accelerationReleaseBelowTimer = 0f;

            bool canRelease = _accelerationReleaseBelowTimer >= hysteresisSeconds;
            float target = 0f;
            if (safeAcceleration > softStart || !canRelease)
            {
                float clampedAcceleration = math.min(safeAcceleration, emergencyClamp);
                float acceleration01 = math.saturate((clampedAcceleration - softStart) * math.rcp(math.max(0.001f, emergencyClamp - softStart)));
                target = Smoothstep01(acceleration01) * Sanitize01(ResolveComfortVignetteMaximum(), 0f);
                if (!canRelease && target < _accelerationComfortVignette01)
                    target = _accelerationComfortVignette01;
            }

            float framePressureTarget = _comfortFramePressureActive
                ? Sanitize01(ResolveComfortFrameSafetyMinOpacity(), 0f)
                : 0f;
            target = math.max(target, framePressureTarget);
            float maxDelta = target > _accelerationComfortVignette01
                ? math.min(SanitizeMinimum(ResolveComfortVignetteAttackSlewPerFrame(), 0.001f), 0.1f)
                : math.min(SanitizeMinimum(ResolveComfortVignetteReleaseSlewPerFrame(), 0.001f), 0.1f);
            float delta = math.clamp(target - _accelerationComfortVignette01, -maxDelta, maxDelta);
            _accelerationComfortVignette01 = Sanitize01(_accelerationComfortVignette01 + delta, 0f);
        }

        private void UpdateComfortFramePressureState(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            float frameSafetyDeltaSeconds = ResolveComfortFrameSafetyDeltaSeconds();
            int consecutiveFrames = math.max(1, ResolveComfortFrameSafetyConsecutiveFrames());
            int releaseStableFrames = math.max(1, ResolveComfortFrameSafetyReleaseStableFrames());
            if (safeDeltaTime > frameSafetyDeltaSeconds)
            {
                _comfortFramePressureConsecutiveFrames = math.min(consecutiveFrames, _comfortFramePressureConsecutiveFrames + 1);
                _comfortFramePressureStableFrames = 0;
                if (_comfortFramePressureConsecutiveFrames >= consecutiveFrames)
                    _comfortFramePressureActive = true;
                return;
            }

            _comfortFramePressureConsecutiveFrames = 0;
            if (!_comfortFramePressureActive)
            {
                _comfortFramePressureStableFrames = 0;
                return;
            }

            _comfortFramePressureStableFrames = math.min(releaseStableFrames, _comfortFramePressureStableFrames + 1);
            if (_comfortFramePressureStableFrames >= releaseStableFrames)
            {
                _comfortFramePressureStableFrames = 0;
                _comfortFramePressureActive = false;
            }
        }

        private void ResetComfortFramePressureState()
        {
            _comfortFramePressureConsecutiveFrames = 0;
            _comfortFramePressureStableFrames = 0;
            _comfortFramePressureActive = false;
        }

        private float ResolveComfortVignetteMaximum()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortVignetteMaximum : comfortVignetteMaximum;
        }

        private float ResolveComfortAccelerationSoftTunnelStartRadS2()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortAccelerationSoftTunnelStartRadS2 : comfortAccelerationSoftTunnelStartRadS2;
        }

        private float ResolveComfortAccelerationEmergencyClampRadS2()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortAccelerationEmergencyClampRadS2 : comfortAccelerationEmergencyClampRadS2;
        }

        private float ResolveComfortAccelerationReleaseBelowRadS2()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortAccelerationReleaseBelowRadS2 : comfortAccelerationReleaseBelowRadS2;
        }

        private float ResolveComfortAccelerationReleaseHysteresisSeconds()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortAccelerationReleaseHysteresisSeconds : comfortAccelerationReleaseHysteresisSeconds;
        }

        private float ResolveComfortVignetteAttackSlewPerFrame()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortVignetteAttackSlewPerFrame : comfortVignetteAttackSlewPerFrame;
        }

        private float ResolveComfortVignetteReleaseSlewPerFrame()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortVignetteReleaseSlewPerFrame : comfortVignetteReleaseSlewPerFrame;
        }

        private float ResolveComfortFrameSafetyDeltaSeconds()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortFrameSafetyDeltaSeconds : Quest3ComfortFrameSafetyDeltaSeconds;
        }

        private float ResolveComfortFrameSafetyMinOpacity()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortFrameSafetyMinOpacity : Quest3ComfortFrameSafetyMinOpacity;
        }

        private int ResolveComfortFrameSafetyConsecutiveFrames()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortFrameSafetyConsecutiveFrames : Quest3ComfortFrameSafetyConsecutiveFrames;
        }

        private int ResolveComfortFrameSafetyReleaseStableFrames()
        {
            return _useQuest2ComfortFallback ? Quest2ComfortFrameSafetyReleaseStableFrames : Quest3ComfortFrameSafetyReleaseStableFrames;
        }

        private void RefreshPlayerSignalsIfDue()
        {
            if (_playerSignalSampleRemaining > 0f)
                return;

            _playerSignalSampleRemaining = PlayerSignalSampleIntervalSeconds;
            ResolvePlayerSignals(out _playerStress01, out _oxygen01, out _depthMeters);
        }

        private void AdvanceSomaticTimers(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (_playerSignalSampleRemaining > 0f)
                _playerSignalSampleRemaining = math.max(0f, _playerSignalSampleRemaining - deltaTime);
            if (_impactHapticCooldownRemaining > 0f)
                _impactHapticCooldownRemaining = math.max(0f, _impactHapticCooldownRemaining - deltaTime);
            if (_jerkEventCooldownRemaining > 0f)
                _jerkEventCooldownRemaining = math.max(0f, _jerkEventCooldownRemaining - deltaTime);
            if (_velocityHapticCooldownRemaining > 0f)
                _velocityHapticCooldownRemaining = math.max(0f, _velocityHapticCooldownRemaining - deltaTime);
        }

        private void ResolvePlayerSignals(out float stress01, out float oxygen01, out float depthMeters)
        {
            stress01 = 0f;
            oxygen01 = 1f;
            depthMeters = 0f;

            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                return;

            PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
            PlayerSurvivalRuntimeState survivalState = runtimeContext.SurvivalState;
            bool hasSurvival = (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u;
            bool hasMovement = (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u;

            depthMeters = hasMovement ? SanitizeNonNegative(movementState.DepthMeters) : 0f;
            if (hasSurvival)
            {
                oxygen01 = Sanitize01(survivalState.OxygenNormalized, 1f);
                stress01 = math.max(
                    1f - oxygen01,
                    math.max(
                        Sanitize01(survivalState.PressureExposureSeverity01, 0f),
                        math.max(
                            Sanitize01(survivalState.ThermalStressSeverity01, 0f),
                            math.max(
                                Sanitize01(survivalState.RapidAscentRisk01, 0f),
                                Sanitize01(survivalState.NitrogenNarcosis01, 0f)))));
            }

            if (hasMovement)
                stress01 = math.max(stress01, Sanitize01(movementState.UnderwaterStressIntensity01, 0f));

            HectonPlayerMovement movement = runtimeContext.PlayerMovement;
            if (movement != null)
                stress01 = math.max(
                    stress01,
                    math.max(
                        Sanitize01(movement.CurrentHullStress01, 0f),
                        Sanitize01(movement.CurrentUnderwaterStressIntensity01, 0f)));

            HectonSurvivalSystem survival = runtimeContext.SurvivalSystem;
            if (survival != null && !hasSurvival)
            {
                oxygen01 = Sanitize01(survival.OxygenNormalized, 1f);
                depthMeters = math.max(depthMeters, SanitizeNonNegative(survival.Depth));
                stress01 = math.max(
                    stress01,
                    math.max(
                        1f - oxygen01,
                        math.max(
                            Sanitize01(survival.PressureExposureSeverity01, 0f),
                            Sanitize01(survival.ThermalStressSeverity01, 0f))));
            }

            stress01 = Sanitize01(stress01, 0f);
            oxygen01 = Sanitize01(oxygen01, 1f);
            depthMeters = SanitizeNonNegative(depthMeters);
        }

        private void UpdateChestSockets(in AbsoluteUniversePosition headAup, Quaternion headRotation)
        {
            _torsoRotation = ResolveTorsoYawFromQuaternionNoTrig(headRotation, _torsoRotation);

            _pdaSocketPose = ResolveSocketPose(in headAup, pdaChestOffset, _pdaSocketLocalRotation);
            _flareSocketPose = ResolveSocketPose(in headAup, flareToolChestOffset, _flareSocketLocalRotation);

            if (pdaChestSocket != null)
                pdaChestSocket.SetPositionAndRotation(_pdaSocketPose.RuntimePosition, _pdaSocketPose.RuntimeRotation);
            if (flareToolChestSocket != null)
                flareToolChestSocket.SetPositionAndRotation(_flareSocketPose.RuntimePosition, _flareSocketPose.RuntimeRotation);
        }

        private VRSomaticChestSocketPose ResolveSocketPose(
            in AbsoluteUniversePosition headAup,
            Vector3 localOffset,
            Quaternion localRotation)
        {
            Vector3 socketOffset = RotateYawOffsetNoMatrix(localOffset, _torsoRotation);
            AbsoluteUniversePosition socketAup = OffsetAupLocal(in headAup, socketOffset);
            Vector3 socketPosition = socketAup.ToRuntimeFloat3();
            Quaternion socketRotation = _torsoRotation * localRotation;
            return new VRSomaticChestSocketPose(
                socketAup,
                socketPosition,
                socketRotation);
        }

        private Quaternion ResolveVisorHudRotation(Vector3 headPosition, Quaternion headRotation)
        {
            Quaternion laggedRotation = headRotation;
            if (applyVisorHudHeadLag)
            {
                float angular01 = math.saturate(_headAngularSpeedRadiansPerSecond / SanitizeMinimum(visorLagAngularSpeedForFull, 0.25f));
                float lagBlend = math.saturate(angular01 * Sanitize01(visorLagMaximumBlend, 0f));
                laggedRotation = ApproximateNlerpNoSqrt(headRotation, _headRotationFrame3, lagBlend);
            }

            float jerkCull01 = math.saturate(_headAngularJerk01 * Sanitize01(rotationJerkCullMaximumBlend, 0f));
            _jerkCullBlend01 = math.lerp(
                _jerkCullBlend01,
                jerkCull01,
                ResolveCinematicBlendApprox(SanitizeMinimum(rotationJerkCullSharpness, 1f), _lastTickDeltaTime));
            if (_jerkCullBlend01 > 0.001f)
                laggedRotation = ApproximateNlerpNoSqrt(laggedRotation, _headRotationFrame3, _jerkCullBlend01);

            if (visorHudRoot != null)
                visorHudRoot.SetPositionAndRotation(headPosition, laggedRotation);

            return laggedRotation;
        }

        private void UpdateBreathingAudio()
        {
            if (breathingSource == null)
                return;

            float stress01 = Sanitize01(_playerStress01, 0f);
            float oxygen01 = Sanitize01(_oxygen01, 1f);
            float nearField01 = Sanitize01(_nearFieldCollision01, 0f);
            float oxygenDanger01 = 1f - oxygen01;
            float depth01 = math.saturate(SanitizeNonNegative(_depthMeters) / 1400f);
            float drive01 = math.saturate(math.max(stress01, math.max(oxygenDanger01 * 1.15f, nearField01 * 0.5f)));

            if ((_stateFlags & StateBreathingAudioStaticApplied) == 0u)
            {
                breathingSource.spatialBlend = 0f;
                breathingSource.panStereo = 0f;
                breathingSource.loop = true;
                _stateFlags |= StateBreathingAudioStaticApplied;
            }

            float targetVolume = math.lerp(Sanitize01(breathingBaseVolume, 0f), Sanitize01(breathingStressVolume, 0f), drive01);
            if (math.abs(targetVolume - _lastPublishedBreathingVolume) > AudioPublishEpsilon)
            {
                breathingSource.volume = targetVolume;
                _lastPublishedBreathingVolume = targetVolume;
            }

            float targetPitch = math.lerp(SanitizeMinimum(breathingMinimumPitch, 0.5f), SanitizeMinimum(breathingMaximumPitch, 0.5f), math.max(stress01, oxygenDanger01));
            if (math.abs(targetPitch - _lastPublishedBreathingPitch) > AudioPublishEpsilon)
            {
                breathingSource.pitch = targetPitch;
                _lastPublishedBreathingPitch = targetPitch;
            }

            if (breathingLowPassFilter != null)
            {
                float lowPass01 = math.saturate(math.max(oxygenDanger01, depth01 * 0.55f));
                if ((_stateFlags & StateBreathingLowPassStaticApplied) == 0u)
                {
                    breathingLowPassFilter.enabled = true;
                    _stateFlags |= StateBreathingLowPassStaticApplied;
                }

                float openCutoffHz = SanitizeAudioCutoffHz(breathingLowPassOpenHz);
                float closedCutoffHz = SanitizeAudioCutoffHz(breathingLowPassClosedHz);
                float targetCutoffHz = math.lerp(math.max(openCutoffHz, closedCutoffHz), math.min(openCutoffHz, closedCutoffHz), lowPass01);
                if (math.abs(targetCutoffHz - _lastPublishedBreathingLowPassHz) > LowPassPublishEpsilonHz)
                {
                    breathingLowPassFilter.cutoffFrequency = targetCutoffHz;
                    _lastPublishedBreathingLowPassHz = targetCutoffHz;
                }

                float targetResonanceQ = math.lerp(1f, 1.65f, lowPass01);
                if (math.abs(targetResonanceQ - _lastPublishedBreathingLowPassQ) > AudioPublishEpsilon)
                {
                    breathingLowPassFilter.lowpassResonanceQ = targetResonanceQ;
                    _lastPublishedBreathingLowPassQ = targetResonanceQ;
                }
            }

            if ((_stateFlags & StateBreathingSourcePlaying) == 0u && breathingSource.clip != null)
            {
                if (!breathingSource.isPlaying)
                    breathingSource.Play();

                _stateFlags |= StateBreathingSourcePlaying;
            }
        }

        private void UpdateCondensation()
        {
            float oxygenDanger01 = 1f - Sanitize01(_oxygen01, 1f);
            float depth01 = math.saturate(SanitizeNonNegative(_depthMeters) / 1400f);
            float target = math.saturate((Sanitize01(_playerStress01, 0f) * 0.58f) + (oxygenDanger01 * 0.32f) + (depth01 * 0.28f));
            float blend = ResolveCinematicBlendApprox(8f, _lastTickDeltaTime);
            _condensation01 = math.lerp(_condensation01, target, blend);
        }

        private void PublishSnapshot(
            in AbsoluteUniversePosition headAup,
            Vector3 headPosition,
            Quaternion headRotation,
            Quaternion visorRotation)
        {
            _snapshot = new VRSomaticSnapshot(
                true,
                headAup,
                headPosition,
                headRotation,
                visorRotation,
                _playerStress01,
                _oxygen01,
                _depthMeters,
                _nearFieldCollision01,
                _condensation01);
        }

        private void ScheduleRootSync(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if (_decoupledRootTransform == null || (_stateFlags & StateRootSyncScheduled) != 0u)
                return;

            if (!_rootSyncInput.IsCreated || !_rootSyncOutput.IsCreated)
                EnsureNativeBuffers();
            if (!_rootSyncInput.IsCreated || !_rootSyncOutput.IsCreated)
                return;

            quaternion sanitizedHeadRotation = (quaternion)headRotation;
            quaternion previousRootRotation = (_stateFlags & StateRootInitialized) != 0u
                ? _lastRootRotation
                : sanitizedHeadRotation;
            _rootSyncInput[0] = new VRSomaticRootSyncInput
            {
                HeadPosition = new float3(headPosition.x, headPosition.y, headPosition.z),
                HeadRotation = sanitizedHeadRotation,
                PreviousRootRotation = previousRootRotation,
                DeltaTime = math.max(deltaTime, MinimumDeltaTime),
                HeadAngularSpeed = SanitizeNonNegative(_headAngularSpeedRadiansPerSecond),
                RootRotationSharpness = SanitizeMinimum(rootRotationSmoothingSharpness, 1f),
                VignetteAngularSpeedStart = SanitizeMinimum(comfortVignetteAngularSpeedStart, 0.01f),
                VignetteAngularSpeedFull = SanitizeMinimum(comfortVignetteAngularSpeedFull, 0.02f),
                VignetteMaximum = Sanitize01(ResolveComfortVignetteMaximum(), 0f),
                AccelerationVignette01 = Sanitize01(_accelerationComfortVignette01, 0f)
            };

            VRSomaticRootSyncJob job = new VRSomaticRootSyncJob
            {
                Input = _rootSyncInput,
                Output = _rootSyncOutput
            };
            _rootSyncHandle = job.Schedule();
            _stateFlags |= StateRootSyncScheduled;
            TryRegisterLateFrame();
        }

        private void CompleteRootSyncIfReady()
        {
            if ((_stateFlags & StateRootSyncScheduled) == 0u)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _rootSyncHandle, false))
                return;

            _stateFlags &= ~StateRootSyncScheduled;
            if (!_rootSyncOutput.IsCreated)
                return;

            VRSomaticRootSyncOutput output = _rootSyncOutput[0];
            if (!math.all(math.isfinite(output.RootPosition)) || !IsFiniteQuaternion(output.RootRotation))
            {
                RecordBlackBoxFrame(_previousHeadPosition, _previousHeadRotation, BlackBoxFlagNonFinite);
                return;
            }

            _lastRootRotation = output.RootRotation;
            _stateFlags |= StateRootInitialized;
            if (_decoupledRootTransform != null)
                _decoupledRootTransform.SetPositionAndRotation(ToVector3(output.RootPosition), ToQuaternion(output.RootRotation.value));

            PublishComfortVignette(output.ComfortVignette01);
        }

        private void ScheduleHandKinematics(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if ((_stateFlags & StateHandKinematicsScheduled) != 0u)
                return;

            if (!HandTargets.IsCreated || !HandPhysicalPositions.IsCreated)
                EnsureNativeBuffers();
            if (!HandTargets.IsCreated || !HandPhysicalPositions.IsCreated)
                return;

            CaptureHandTargets(headPosition, headRotation);
            if ((_stateFlags & StateHandsInitialized) == 0u)
            {
                for (int i = 0; i < HandCount; i++)
                    HandPhysicalPositions[i] = HandTargets[i];

                _stateFlags |= StateHandsInitialized;
            }

            VRSomaticHandKinematicsJob job = new VRSomaticHandKinematicsJob
            {
                DeltaTime = math.max(deltaTime, MinimumDeltaTime),
                SpringForce = SanitizeMinimum(handSpringForce, 1f),
                HandTargets = HandTargets,
                HandPhysicalPositions = HandPhysicalPositions
            };
            _handKinematicsHandle = job.Schedule(HandCount, 1);
            _stateFlags |= StateHandKinematicsScheduled;
            TryRegisterLateFrame();
        }

        private void CompleteHandKinematicsIfReady()
        {
            if ((_stateFlags & StateHandKinematicsScheduled) == 0u)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _handKinematicsHandle, false))
                return;

            _stateFlags &= ~StateHandKinematicsScheduled;
            _handGhostMask = ResolveHandGhostMask();
            RecordBlackBoxFrame(_previousHeadPosition, _previousHeadRotation, 0);
        }

        private void CaptureHandTargets(Vector3 headPosition, Quaternion headRotation)
        {
            InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;
            HandTargets[0] = ResolveHandTarget(dispatcher, 0, headPosition, headRotation, -0.22f);
            HandTargets[1] = ResolveHandTarget(dispatcher, 1, headPosition, headRotation, 0.22f);
        }

        private float3 ResolveHandTarget(
            InputDispatcher dispatcher,
            byte handIndex,
            Vector3 headPosition,
            Quaternion headRotation,
            float lateralOffset)
        {
            if (dispatcher != null &&
                dispatcher.TryGetXRInputState(handIndex, out XRInputState state) &&
                IsFiniteFloat3(state.GripPositionWS))
            {
                return state.GripPositionWS;
            }

            if ((_stateFlags & StateHandsInitialized) != 0u &&
                HandTargets.IsCreated &&
                handIndex < HandTargets.Length &&
                IsFiniteFloat3(HandTargets[handIndex]))
            {
                return HandTargets[handIndex];
            }

            return ResolveFallbackHandTarget(headPosition, headRotation, lateralOffset);
        }

        private uint ResolveHandGhostMask()
        {
            if (!HandTargets.IsCreated || !HandPhysicalPositions.IsCreated)
                return 0u;

            if (disableGhostHandsOnLowTier && IsLowTier(GlobalRegistry.ScalabilityTier))
                return 0u;

            float threshold = SanitizeMinimum(ghostHandDistanceMeters, 0.01f);
            float thresholdSq = threshold * threshold;
            uint mask = 0u;
            for (int i = 0; i < HandCount; i++)
            {
                float3 delta = HandTargets[i] - HandPhysicalPositions[i];
                float distanceSq = math.lengthsq(delta);
                if (math.isfinite(distanceSq) && distanceSq > thresholdSq)
                    mask |= 1u << i;
            }

            return mask;
        }

        private static float3 ResolveFallbackHandTarget(Vector3 headPosition, Quaternion headRotation, float lateralOffset)
        {
            float3 offset = new float3(lateralOffset, -0.32f, 0.42f);
            float3 rotated = math.rotate((quaternion)headRotation, offset);
            return new float3(headPosition.x, headPosition.y, headPosition.z) + rotated;
        }

        private void ScheduleHeadCollisionBatch(Vector3 headPosition, Quaternion headRotation)
        {
            if ((_stateFlags & StateHeadCollisionScheduled) != 0u)
                return;

            if (!CanRunHeadCollisionQuery())
                return;

            if (!_headCollisionCommands.IsCreated)
                EnsureNativeBuffers();

            if (!HasHeadCollisionBuffers())
                return;

            QueryParameters queryParameters = new QueryParameters(
                nearFieldCollisionMask.value,
                false,
                QueryTriggerInteraction.Ignore);

            BuildHeadCapsulecastCommandsJob buildJob = new BuildHeadCapsulecastCommandsJob
            {
                HeadPosition = headPosition,
                HeadRotation = headRotation,
                HeadUp = math.rotate((quaternion)headRotation, new float3(0f, 1f, 0f)),
                CapsuleHalfHeight = SanitizeMinimum(headCapsuleHalfHeightMeters, MinimumHeadCapsuleHalfHeightMeters),
                CapsuleRadius = SanitizeMinimum(headCapsuleRadiusMeters, MinimumHeadCapsuleRadiusMeters),
                CastDistance = SanitizeMinimum(nearFieldDistanceMeters, MinimumNearFieldDistanceMeters),
                QueryParameters = queryParameters,
                Commands = _headCollisionCommands
            };

            ProcessHeadCapsulecastHitsJob processJob = new ProcessHeadCapsulecastHitsJob
            {
                Hits = _headCollisionHits,
                Samples = _headCollisionSamples
            };

            JobHandle buildHandle = buildJob.Schedule(HeadCollisionCommandCount, 1);
            JobHandle castHandle = CapsulecastCommand.ScheduleBatch(
                _headCollisionCommands,
                _headCollisionHits,
                1,
                HeadCollisionMaxHitsPerCommand,
                buildHandle);
            _headCollisionHandle = processJob.Schedule(HeadCollisionCommandCount, 1, castHandle);
            _stateFlags |= StateHeadCollisionScheduled;
            TryRegisterLateFrame();
        }

        private void RefreshNearFieldCollisionQueryAvailability(float deltaTime)
        {
            if (CanRunHeadCollisionQuery())
                return;

            FadeNearFieldCollisionToZero(deltaTime);
        }

        private bool CanRunHeadCollisionQuery()
        {
            return nearFieldCollisionMask.value != 0 &&
                   math.isfinite(nearFieldDistanceMeters) &&
                   nearFieldDistanceMeters >= MinimumNearFieldDistanceMeters;
        }

        private void FadeNearFieldCollisionToZero(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (safeDeltaTime <= 0f)
            {
                _nearFieldCollision01 = 0f;
            }
            else
            {
                float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, safeDeltaTime);
                _nearFieldCollision01 = math.lerp(_nearFieldCollision01, 0f, blend);
                if (_nearFieldCollision01 <= ShaderPublishEpsilon)
                    _nearFieldCollision01 = 0f;
            }

            _collisionState = default;
        }

        private void ConsumeHeadCollisionSamples()
        {
            if (!HasHeadCollisionBuffers())
            {
                _collisionState = default;
                _nearFieldCollision01 = 0f;
                return;
            }

            bool hasContact = false;
            HeadCastSample bestSample = default;
            float nearFieldDistance = SanitizeMinimum(nearFieldDistanceMeters, MinimumNearFieldDistanceMeters);
            float bestDistance = nearFieldDistance;
            for (int i = 0; i < HeadCollisionCommandCount; i++)
            {
                HeadCastSample sample = _headCollisionSamples[i];
                if (sample.HasHit == 0 ||
                    !math.isfinite(sample.Distance) ||
                    sample.Distance < 0f ||
                    sample.Distance > bestDistance)
                {
                    continue;
                }

                bestDistance = sample.Distance;
                bestSample = sample;
                hasContact = true;
            }

            float targetIntensity = 0f;
            if (hasContact)
                targetIntensity = 1f - math.saturate(bestDistance / nearFieldDistance);

            float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, _lastTickDeltaTime);
            _nearFieldCollision01 = math.lerp(_nearFieldCollision01, targetIntensity, blend);

            if (!hasContact)
            {
                _collisionState = default;
                return;
            }

            Vector3 normal = (Vector3)bestSample.Normal;
            Vector3 point = (Vector3)bestSample.Point;
            AbsoluteUniversePosition headAup = _snapshot.HeadAup;
            AbsoluteUniversePosition contactAup = OffsetAupLocal(in headAup, point - _snapshot.HeadRuntimePosition);
            _collisionState = new VRSomaticCollisionState(
                true,
                contactAup,
                point,
                normal,
                bestDistance,
                _nearFieldCollision01,
                _headLinearSpeedMetersPerSecond);

            TryEmitImpactHaptics(bestSample.LocalSide, _nearFieldCollision01);
        }

        private void TryEmitImpactHaptics(float localSide, float intensity01)
        {
            float impactThreshold = SanitizeMinimum(impactSpeedThresholdMetersPerSecond, 0.01f);
            if (_headLinearSpeedMetersPerSecond < impactThreshold)
                return;

            if (_impactHapticCooldownRemaining > 0f)
                return;

            float speedSpan = math.max(impactThreshold, 0.25f);
            float speed01 = math.saturate((_headLinearSpeedMetersPerSecond - impactThreshold) / speedSpan);
            float impact01 = math.saturate(math.max(intensity01, speed01));
            byte motorMask = ResolveDirectionalMotorMask(localSide);
            ToolHapticsRuntime.EnqueueCommand(
                Sanitize01(maxLowFrequencyImpact, 0f) * impact01,
                Sanitize01(maxHighFrequencyImpact, 0f) * impact01,
                SanitizeMinimum(impactHapticDurationSeconds, 0.02f),
                SanitizeMinimum(impactHapticDecayRate, 0f),
                HapticPriorityCritical,
                motorMask,
                HapticBlendAdditive);
            _impactHapticCooldownRemaining = SanitizeMinimum(impactHapticDebounceSeconds, MinimumImpactDebounceSeconds);
        }

        private static byte ResolveDirectionalMotorMask(float localSide)
        {
            if (localSide > HapticSideThreshold)
                return RightMotorMask;
            if (localSide < -HapticSideThreshold)
                return LeftMotorMask;
            return BothMotorMask;
        }

        private void ApplyInactiveState(float deltaTime, bool publishShaderState = true)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (safeDeltaTime <= 0f)
            {
                _nearFieldCollision01 = 0f;
                _condensation01 = 0f;
                InvalidateShaderPublishCache();
            }
            else
            {
                float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, safeDeltaTime);
                _nearFieldCollision01 = math.lerp(_nearFieldCollision01, 0f, blend);
                _condensation01 = math.lerp(_condensation01, 0f, blend);
            }

            _playerStress01 = 0f;
            _oxygen01 = 1f;
            _depthMeters = 0f;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
            _jerkEventCooldownRemaining = 0f;
            _playerSignalSampleRemaining = 0f;
            _impactHapticCooldownRemaining = 0f;
            _velocityHapticCooldownRemaining = 0f;
            _fallbackHmdTransform = null;
            _handGhostMask = 0u;
            _collisionState = default;
            _snapshot = VRSomaticSnapshot.Inactive;
            _stateFlags &= ~(StateHasPreviousHeadPose | StateHandsInitialized | StateRootInitialized);
            PublishComfortVignette(0f);
            if (breathingSource != null)
            {
                if ((_stateFlags & StateBreathingSourcePlaying) != 0u)
                {
                    breathingSource.Stop();
                    _stateFlags &= ~StateBreathingSourcePlaying;
                }

                breathingSource.volume = 0f;
                _lastPublishedBreathingVolume = 0f;
            }
            if (breathingLowPassFilter != null)
            {
                if (breathingLowPassFilter.enabled)
                    breathingLowPassFilter.enabled = false;

                _stateFlags &= ~StateBreathingLowPassStaticApplied;
            }
            if (publishShaderState)
                PublishShaderState();

            if (_blackBox.IsCreated)
                RecordBlackBoxFrame(Vector3.zero, Quaternion.identity, 0);
        }

        private void InvalidateShaderPublishCache()
        {
            _lastPublishedNearCollision01 = float.PositiveInfinity;
            _lastPublishedCondensation01 = float.PositiveInfinity;
            _lastPublishedComfortVignette01 = float.PositiveInfinity;
            _lastPublishedSomaticState = Vector4.positiveInfinity;
            _lastPublishedJerkState = Vector4.positiveInfinity;
        }

        private void ResetBreathingAudioPublishCache()
        {
            _stateFlags &= ~(StateBreathingAudioStaticApplied | StateBreathingLowPassStaticApplied | StateBreathingSourcePlaying);
            _lastPublishedBreathingVolume = float.PositiveInfinity;
            _lastPublishedBreathingPitch = float.PositiveInfinity;
            _lastPublishedBreathingLowPassHz = float.PositiveInfinity;
            _lastPublishedBreathingLowPassQ = float.PositiveInfinity;
        }

        private void PublishShaderState()
        {
            float nearCollision01 = Sanitize01(_nearFieldCollision01, 0f);
            if (math.abs(nearCollision01 - _lastPublishedNearCollision01) > ShaderPublishEpsilon)
            {
                Shader.SetGlobalFloat(NearCollisionIntensityId, nearCollision01);
                _lastPublishedNearCollision01 = nearCollision01;
            }

            float condensation01 = Sanitize01(_condensation01, 0f);
            if (math.abs(condensation01 - _lastPublishedCondensation01) > ShaderPublishEpsilon)
            {
                Shader.SetGlobalFloat(SomaticCondensationId, condensation01);
                _lastPublishedCondensation01 = condensation01;
            }

            Vector4 somaticState = new Vector4(
                Sanitize01(_playerStress01, 0f),
                Sanitize01(_oxygen01, 1f),
                SanitizeNonNegative(_depthMeters),
                SanitizeNonNegative(_headLinearSpeedMetersPerSecond));
            if (!Approximately(in somaticState, in _lastPublishedSomaticState))
            {
                Shader.SetGlobalVector(SomaticStateId, somaticState);
                _lastPublishedSomaticState = somaticState;
            }

            Vector4 jerkState = new Vector4(
                Sanitize01(_headAngularJerk01, 0f),
                Sanitize01(_jerkCullBlend01, 0f),
                SanitizeNonNegative(_headAngularJerkRadiansPerSecondCubed),
                Sanitize01(rotationJerkVignetteContribution, 0f));
            if (!Approximately(in jerkState, in _lastPublishedJerkState))
            {
                Shader.SetGlobalVector(VrComfortJerkStateId, jerkState);
                _lastPublishedJerkState = jerkState;
            }
        }

        private void PublishComfortVignette(float vignette01)
        {
            float sanitized = Sanitize01(vignette01, 0f);
            if (math.abs(sanitized - _lastPublishedComfortVignette01) <= ShaderPublishEpsilon)
                return;

            Shader.SetGlobalFloat(VrComfortVignetteId, sanitized);
            _lastPublishedComfortVignette01 = sanitized;
            PublishSomaticComfortVignetteTelemetry(sanitized);
        }

        private void PublishSomaticComfortVignetteTelemetry(float vignette01)
        {
            float sanitized = Sanitize01(vignette01, 0f);
            if (sanitized <= _maxSomaticComfortVignetteTelemetry01)
                return;

            _maxSomaticComfortVignetteTelemetry01 = sanitized;
            if (_maxSomaticComfortVignetteTelemetry01 - _lastSomaticComfortVignetteTelemetry01 < VrComfortTelemetryStep01)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                VrComfortMaxVignetteHash,
                VrComfortTelemetryContextHash,
                _maxSomaticComfortVignetteTelemetry01);
            _lastSomaticComfortVignetteTelemetry01 = _maxSomaticComfortVignetteTelemetry01;
        }

        private void TryEmitVelocityAnchorHaptics()
        {
            float threshold = SanitizeMinimum(velocityHapticThresholdMetersPerSecond, 0.01f);
            if (_headLinearSpeedMetersPerSecond < threshold || _velocityHapticCooldownRemaining > 0f)
                return;

            float speed01 = math.saturate((_headLinearSpeedMetersPerSecond - threshold) * math.rcp(math.max(threshold, 0.25f)));
            float lowFrequency = math.lerp(0.035f, 0.16f, speed01);
            float highFrequency = math.lerp(0.015f, 0.09f, speed01);
            ToolHapticsRuntime.EnqueueCommand(
                lowFrequency,
                highFrequency,
                SanitizeMinimum(velocityHapticDurationSeconds, 0.01f),
                0f,
                HapticPriorityComfort,
                BothMotorMask,
                HapticBlendAdditive);
            _velocityHapticCooldownRemaining = SanitizeMinimum(velocityHapticIntervalSeconds, 0.03f);
        }

        private void PublishComfortTelemetry()
        {
            if (_jerkEventCount != _lastTelemetryJerkEventCount)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    VrComfortJerkEventHash,
                    VrComfortTelemetryContextHash,
                    _jerkEventCount);
                _lastTelemetryJerkEventCount = _jerkEventCount;
            }
        }

        private void RecordBlackBoxFrame(Vector3 headPosition, Quaternion headRotation, ushort extraFlags)
        {
            if (!_blackBox.IsCreated)
            {
                DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
                if (!_headCollisionDisposeHandle.IsCompleted || !Application.isPlaying)
                    return;

                EnsureBlackBoxBuffer();
            }

            float4 headRotationValue = new float4(headRotation.x, headRotation.y, headRotation.z, headRotation.w);
            bool hasFiniteRotation = math.all(math.isfinite(headRotationValue));
            float rotationLengthSq = math.lengthsq(headRotationValue);
            bool hasValidRotationLength = math.isfinite(rotationLengthSq) &&
                                          rotationLengthSq >= QuaternionLengthSqMinimum &&
                                          rotationLengthSq <= QuaternionLengthSqMaximum;
            ushort flags = ResolveBlackBoxFlags(extraFlags);
            if (!IsFiniteVector(headPosition) || !hasFiniteRotation || !hasValidRotationLength)
                flags |= BlackBoxFlagNonFinite;

            float leftHandSeparationSq = ResolveHandSeparationSq(0);
            float rightHandSeparationSq = ResolveHandSeparationSq(1);
            int frame = Time.frameCount;
            int index;
            if (_blackBoxCursor > 0 && _blackBoxLastRecordedFrame == frame)
            {
                index = (_blackBoxCursor - 1) % BlackBoxFrameCapacity;
                flags |= _blackBox[index].Flags;
            }
            else
            {
                index = _blackBoxCursor % BlackBoxFrameCapacity;
                _blackBoxCursor++;
                _blackBoxLastRecordedFrame = frame;
            }

            VRSomaticBlackBoxEntry entry = new VRSomaticBlackBoxEntry
            {
                Frame = frame,
                StateHash = ResolveBlackBoxStateHash(headPosition, headRotationValue, leftHandSeparationSq, rightHandSeparationSq, flags),
                Flags = flags,
                HandGhostMask = (ushort)(_handGhostMask & 0xFFFFu),
                HeadPosition = new float3(headPosition.x, headPosition.y, headPosition.z),
                HeadRotation = headRotationValue,
                NearCollision01 = Sanitize01(_nearFieldCollision01, 0f),
                ComfortVignette01 = Sanitize01(_lastPublishedComfortVignette01, 0f),
                LeftHandSeparationSq = leftHandSeparationSq,
                RightHandSeparationSq = rightHandSeparationSq,
                HeadAngularSpeedRadiansPerSecond = SanitizeNonNegative(_headAngularSpeedRadiansPerSecond),
                AupShiftSequence = _lastObservedAupShiftSequence
            };

            _blackBox[index] = entry;
            if ((flags & BlackBoxFlagNonFinite) != 0)
                DumpBlackBoxOnce();
        }

        private ushort ResolveBlackBoxFlags(ushort extraFlags)
        {
            uint flags = extraFlags;
            if (_snapshot.IsActive)
                flags |= BlackBoxFlagActive;
            if ((_handGhostMask & 1u) != 0u)
                flags |= BlackBoxFlagLeftGhost;
            if ((_handGhostMask & 2u) != 0u)
                flags |= BlackBoxFlagRightGhost;
            if ((_stateFlags & StateRootSyncScheduled) != 0u)
                flags |= BlackBoxFlagRootJobScheduled;
            if ((_stateFlags & StateHandKinematicsScheduled) != 0u)
                flags |= BlackBoxFlagHandJobScheduled;
            if (_collisionState.HasContact || _nearFieldCollision01 > 0.001f)
                flags |= BlackBoxFlagNearCollision;
            if (_lastObservedAupShiftSequence != 0u)
                flags |= BlackBoxFlagAupShiftSeen;
            if (IsLowTier(GlobalRegistry.ScalabilityTier))
                flags |= BlackBoxFlagLowTier;
            if (_comfortFramePressureActive)
                flags |= BlackBoxFlagFramePressure;
            if (_useQuest2ComfortFallback)
                flags |= BlackBoxFlagQuest2Fallback;
            if (_accelerationComfortVignette01 > 0.001f)
                flags |= BlackBoxFlagAccelerationTunnel;

            return (ushort)(flags & 0xFFFFu);
        }

        private float ResolveHandSeparationSq(int index)
        {
            if ((_stateFlags & StateHandKinematicsScheduled) != 0u ||
                !HandTargets.IsCreated ||
                !HandPhysicalPositions.IsCreated ||
                index < 0 ||
                index >= HandCount)
            {
                return 0f;
            }

            float3 delta = HandTargets[index] - HandPhysicalPositions[index];
            float distanceSq = math.lengthsq(delta);
            return math.isfinite(distanceSq) ? distanceSq : 0f;
        }

        private static uint ResolveBlackBoxStateHash(
            Vector3 headPosition,
            float4 headRotation,
            float leftHandSeparationSq,
            float rightHandSeparationSq,
            ushort flags)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, math.asuint(headPosition.x));
            hash = MixHash(hash, math.asuint(headPosition.y));
            hash = MixHash(hash, math.asuint(headPosition.z));
            hash = MixHash(hash, math.asuint(headRotation.x));
            hash = MixHash(hash, math.asuint(headRotation.y));
            hash = MixHash(hash, math.asuint(headRotation.z));
            hash = MixHash(hash, math.asuint(headRotation.w));
            hash = MixHash(hash, math.asuint(leftHandSeparationSq));
            hash = MixHash(hash, math.asuint(rightHandSeparationSq));
            return MixHash(hash, flags);
        }

        private static uint MixHash(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    BlackBoxDumpFileName));
                using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    int count = math.min(_blackBoxCursor, BlackBoxFrameCapacity);
                    int start = _blackBoxCursor - count;
                    writer.Write(BlackBoxMagic);
                    writer.Write(BlackBoxVersion);
                    writer.Write(BlackBoxFrameCapacity);
                    writer.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        VRSomaticBlackBoxEntry entry = _blackBox[(start + i) % BlackBoxFrameCapacity];
                        writer.Write(entry.Frame);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.Flags);
                        writer.Write(entry.HandGhostMask);
                        writer.Write(entry.HeadPosition.x);
                        writer.Write(entry.HeadPosition.y);
                        writer.Write(entry.HeadPosition.z);
                        writer.Write(entry.HeadRotation.x);
                        writer.Write(entry.HeadRotation.y);
                        writer.Write(entry.HeadRotation.z);
                        writer.Write(entry.HeadRotation.w);
                        writer.Write(entry.NearCollision01);
                        writer.Write(entry.ComfortVignette01);
                        writer.Write(entry.LeftHandSeparationSq);
                        writer.Write(entry.RightHandSeparationSq);
                        writer.Write(entry.HeadAngularSpeedRadiansPerSecond);
                        writer.Write(entry.AupShiftSequence);
                    }
                }
            }
            catch (System.Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[VRSomaticProvider] Black box dump failed: " + exception.Message);
#endif
            }
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : fallback;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizeMinimum(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        private static float SanitizeAudioCutoffHz(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 200f, 22000f) : 200f;
        }

        private static bool Approximately(in Vector4 left, in Vector4 right)
        {
            return math.abs(left.x - right.x) <= ShaderPublishEpsilon &&
                   math.abs(left.y - right.y) <= ShaderPublishEpsilon &&
                   math.abs(left.z - right.z) <= ShaderPublishEpsilon &&
                   math.abs(left.w - right.w) <= ShaderPublishEpsilon;
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 SanitizeFiniteFloat3(float3 value)
        {
            return IsFiniteFloat3(value) ? value : float3.zero;
        }

        private static bool IsFiniteQuaternion(quaternion value)
        {
            float4 q = value.value;
            float lengthSq = math.lengthsq(q);
            return math.all(math.isfinite(q)) &&
                   math.isfinite(lengthSq) &&
                   lengthSq >= QuaternionLengthSqMinimum &&
                   lengthSq <= QuaternionLengthSqMaximum;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown ||
                   GlobalRegistry.H8_LOW_MEMORY_PROFILE;
        }

        private static void RebaseHandArray(NativeArray<float3> array, float3 shift)
        {
            if (!array.IsCreated || !math.all(math.isfinite(shift)))
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] -= shift;
        }

        private void CacheSocketRotations()
        {
            _pdaSocketLocalRotation = ResolveEulerRotationNoTrig(pdaChestRotationEuler);
            _flareSocketLocalRotation = ResolveEulerRotationNoTrig(flareToolChestRotationEuler);
        }

        private static Quaternion ResolveEulerRotationNoTrig(Vector3 eulerDegrees)
        {
            if (!IsFiniteVector(eulerDegrees))
                return Quaternion.identity;

            ApproximateSinCosFullNoTrig(eulerDegrees.x * DegreesToRadians * 0.5f, out float sx, out float cx);
            ApproximateSinCosFullNoTrig(eulerDegrees.y * DegreesToRadians * 0.5f, out float sy, out float cy);
            ApproximateSinCosFullNoTrig(eulerDegrees.z * DegreesToRadians * 0.5f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            return ToQuaternion(NormalizeQuaternionNoSqrt(MulQuaternionNoSqrt(MulQuaternionNoSqrt(yaw, pitch), roll)));
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static float4 MulQuaternionNoSqrt(float4 lhs, float4 rhs)
        {
            return new float4(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lengthSq = math.dot(value, value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return new float4(0f, 0f, 0f, 1f);

            return value * ApproximateInverseLengthNoSqrt(lengthSq);
        }

        private static float Smoothstep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static Quaternion ToQuaternion(float4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static float ResolveCinematicBlendApprox(float sharpness, float deltaTime)
        {
            if (!math.isfinite(deltaTime) || !math.isfinite(sharpness) || deltaTime <= 0f || sharpness <= 0f)
                return 1f;

            float x = math.min(sharpness * deltaTime, 32f);
            return math.saturate(x / (1f + x));
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs((float3)value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static float3 ResolveAngularVelocityRadiansPerSecond(
            Quaternion previousRotation,
            Quaternion currentRotation,
            float angularDeltaRadians,
            float invDeltaTime)
        {
            if (!math.isfinite(angularDeltaRadians) ||
                angularDeltaRadians <= 0.000001f ||
                !math.isfinite(invDeltaTime) ||
                invDeltaTime <= 0f)
            {
                return float3.zero;
            }

            float4 previous = ((quaternion)previousRotation).value;
            float4 current = ((quaternion)currentRotation).value;
            if (math.dot(previous, current) < 0f)
                current = -current;

            float4 inversePrevious = new float4(-previous.x, -previous.y, -previous.z, previous.w);
            float4 delta = MulQuaternionNoSqrt(current, inversePrevious);
            if (delta.w < 0f)
                delta = -delta;

            float3 deltaVector = new float3(delta.x, delta.y, delta.z);
            if (!IsFiniteFloat3(deltaVector))
                return float3.zero;

            float deltaVectorMagnitude = ApproximateMagnitudeNoSqrt(deltaVector);
            if (deltaVectorMagnitude <= 0.000001f)
                return float3.zero;

            return deltaVector * (angularDeltaRadians * math.rcp(deltaVectorMagnitude) * invDeltaTime);
        }

        private static float ApproximateAngularDeltaRadiansNoAcos(Quaternion previousRotation, Quaternion currentRotation)
        {
            float4 previous = ((quaternion)previousRotation).value;
            float4 current = ((quaternion)currentRotation).value;
            if (math.dot(previous, current) < 0f)
                current = -current;

            float4 absDelta = math.abs(current - previous);
            float maxA = math.max(absDelta.x, absDelta.y);
            float maxB = math.max(absDelta.z, absDelta.w);
            float minA = math.min(absDelta.x, absDelta.y);
            float minB = math.min(absDelta.z, absDelta.w);
            float largest = math.max(maxA, maxB);
            float smallest = math.min(minA, minB);
            float middleSum = absDelta.x + absDelta.y + absDelta.z + absDelta.w - largest - smallest;
            return (largest + (middleSum * 0.33333334f) + (smallest * 0.125f)) * 2f;
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion fromRotation, Quaternion toRotation, float blend01)
        {
            float4 from = ((quaternion)fromRotation).value;
            float4 to = ((quaternion)toRotation).value;
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, blend01);
            float inverseLengthApprox = ApproximateInverseLengthNoSqrt(math.dot(blended, blended));
            quaternion approximated = blended * inverseLengthApprox;
            return approximated;
        }

        private static Quaternion ResolveTorsoYawFromQuaternionNoTrig(Quaternion headRotation, Quaternion fallbackRotation)
        {
            float4 head = ((quaternion)headRotation).value;
            float lengthSq = (head.y * head.y) + (head.w * head.w);
            if (lengthSq <= 0.000001f || !math.isfinite(lengthSq))
                return fallbackRotation;

            float inverseLengthApprox = ApproximateInverseLengthNoSqrt(lengthSq);
            float yawY = head.y * inverseLengthApprox;
            float yawW = head.w * inverseLengthApprox;
            if (yawW < 0f)
            {
                yawY = -yawY;
                yawW = -yawW;
            }

            return new Quaternion(0f, yawY, 0f, yawW);
        }

        private static Vector3 RotateYawOffsetNoMatrix(Vector3 localOffset, Quaternion yawRotation)
        {
            float yawY = yawRotation.y;
            float yawW = yawRotation.w;
            float sinYaw = 2f * yawY * yawW;
            float cosYaw = 1f - (2f * yawY * yawY);
            return new Vector3(
                (cosYaw * localOffset.x) + (sinYaw * localOffset.z),
                localOffset.y,
                (cosYaw * localOffset.z) - (sinYaw * localOffset.x));
        }

        private static float ApproximateInverseLengthNoSqrt(float lengthSq)
        {
            return math.rcp(0.5f + (0.5f * math.max(lengthSq, 0.000001f)));
        }

        private bool HasHeadCollisionBuffers()
        {
            return _headCollisionCommands.IsCreated &&
                   _headCollisionHits.IsCreated &&
                   _headCollisionSamples.IsCreated;
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
            if (local >= 0f && local < AupCellSizeMeters)
                return;

            long gridDelta = (long)math.floor(local / AupCellSizeMeters);
            grid += gridDelta;
            local -= gridDelta * AupCellSizeMeters;
            if (local < 0f)
            {
                local += AupCellSizeMeters;
                grid--;
                return;
            }

            if (local >= AupCellSizeMeters)
            {
                local -= AupCellSizeMeters;
                grid++;
            }
        }

        private void EnsureBlackBoxBuffer()
        {
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<VRSomaticBlackBoxEntry>(
                BlackBoxFrameCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<VRSomaticBlackBoxEntry>[300] - VR somatic postmortem black box - owner: VRSomaticProvider
            NativeMemorySentinel.RegisterNativeArray(_blackBox, nameof(VRSomaticProvider), nameof(_blackBox), NativeAllocationLifetime.Scene);
        }

        private void EnsureNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            if (!_headCollisionDisposeHandle.IsCompleted)
                return;

            EnsureBlackBoxBuffer();

            if (!_headCollisionCommands.IsCreated)
            {
                _headCollisionCommands = new NativeArray<CapsulecastCommand>(
                    HeadCollisionCommandCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<CapsulecastCommand>[6] - VR somatic head near-field sweep commands - owner: VRSomaticProvider
                _headCollisionHits = new NativeArray<RaycastHit>(
                    HeadCollisionCommandCount * HeadCollisionMaxHitsPerCommand,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[6] - VR somatic head near-field sweep hits - owner: VRSomaticProvider
                _headCollisionSamples = new NativeArray<HeadCastSample>(
                    HeadCollisionCommandCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadCastSample>[6] - VR somatic processed contact samples - owner: VRSomaticProvider

                NativeMemorySentinel.RegisterNativeArray(_headCollisionCommands, nameof(VRSomaticProvider), nameof(_headCollisionCommands), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(_headCollisionHits, nameof(VRSomaticProvider), nameof(_headCollisionHits), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(_headCollisionSamples, nameof(VRSomaticProvider), nameof(_headCollisionSamples), NativeAllocationLifetime.Scene);
            }

            if (!_rootSyncInput.IsCreated)
            {
                _rootSyncInput = new NativeArray<VRSomaticRootSyncInput>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<VRSomaticRootSyncInput>[1] - decoupled VR root sync input - owner: VRSomaticProvider
                _rootSyncOutput = new NativeArray<VRSomaticRootSyncOutput>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<VRSomaticRootSyncOutput>[1] - decoupled VR root sync output - owner: VRSomaticProvider
                NativeMemorySentinel.RegisterNativeArray(_rootSyncInput, nameof(VRSomaticProvider), nameof(_rootSyncInput), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(_rootSyncOutput, nameof(VRSomaticProvider), nameof(_rootSyncOutput), NativeAllocationLifetime.Scene);
            }

            if (!HandTargets.IsCreated)
            {
                HandTargets = new NativeArray<float3>(HandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[2] - OpenXR hand targets - owner: VRSomaticProvider
                HandPhysicalPositions = new NativeArray<float3>(HandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[2] - spring-driven physical hand positions - owner: VRSomaticProvider
                NativeMemorySentinel.RegisterNativeArray(HandTargets, nameof(VRSomaticProvider), nameof(HandTargets), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(HandPhysicalPositions, nameof(VRSomaticProvider), nameof(HandPhysicalPositions), NativeAllocationLifetime.Scene);
            }
        }

        private void DisposeNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            bool hasPendingDispose = !_headCollisionDisposeHandle.IsCompleted;
            JobHandle activeJobs = JobHandle.CombineDependencies(
                _headCollisionHandle,
                JobHandle.CombineDependencies(_rootSyncHandle, _handKinematicsHandle));
            JobHandle disposeHandle = hasPendingDispose
                ? JobHandle.CombineDependencies(_headCollisionDisposeHandle, activeJobs)
                : activeJobs;
            bool scheduledDispose = false;

            DisposeNativeArray(ref _headCollisionCommands, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _headCollisionHits, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _headCollisionSamples, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _rootSyncInput, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _rootSyncOutput, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref HandTargets, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref HandPhysicalPositions, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _blackBox, ref disposeHandle, ref scheduledDispose);
            _headCollisionHandle = default;
            _rootSyncHandle = default;
            _handKinematicsHandle = default;
            _blackBoxCursor = 0;
            _blackBoxLastRecordedFrame = -1;
            _stateFlags &= ~(StateHeadCollisionScheduled | StateRootSyncScheduled | StateHandKinematicsScheduled | StateHandsInitialized | StateRootInitialized);

            if (!scheduledDispose)
                return;

            _headCollisionDisposeHandle = disposeHandle;
            JobHandle.ScheduleBatchedJobs();
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle disposeHandle, ref bool scheduledDispose) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            disposeHandle = array.Dispose(disposeHandle);
            array = default;
            scheduledDispose = true;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct VRSomaticRootSyncJob : IJob
        {
            [ReadOnly] public NativeArray<VRSomaticRootSyncInput> Input;
            [WriteOnly] public NativeArray<VRSomaticRootSyncOutput> Output;

            public void Execute()
            {
                VRSomaticRootSyncInput input = Input[0];
                quaternion headRotation = SanitizeQuaternion(input.HeadRotation, quaternion.identity);
                quaternion previousRootRotation = SanitizeQuaternion(input.PreviousRootRotation, headRotation);
                float3 worldUp = new float3(0f, 1f, 0f);
                float3 headUp = math.rotate(headRotation, worldUp);
                if (!math.all(math.isfinite(headUp)))
                    headUp = worldUp;

                float3 correctionAxis = math.cross(headUp, worldUp);
                float axisLenSq = math.lengthsq(correctionAxis);
                quaternion horizonCorrection = quaternion.identity;
                if (math.isfinite(axisLenSq) && axisLenSq > HorizonLockStartSinSq)
                {
                    float3 axis = correctionAxis * math.rsqrt(math.max(axisLenSq, 0.000001f));
                    float correctionRcp = math.rcp(math.max(0.000001f, 1f - HorizonLockStartSinSq));
                    float correction01 = math.saturate((axisLenSq - HorizonLockStartSinSq) * correctionRcp);
                    horizonCorrection = quaternion.AxisAngle(axis, HorizonLockMaxCorrectionRadians * correction01);
                }

                quaternion desiredRootRotation = SanitizeQuaternion(math.mul(horizonCorrection, headRotation), headRotation);
                float blend = ResolveJobBlend(input.RootRotationSharpness, input.DeltaTime);
                quaternion rootRotation = Nlerp(previousRootRotation, desiredRootRotation, blend);
                float speedStart = math.max(0.01f, input.VignetteAngularSpeedStart);
                float speedFull = math.max(speedStart + 0.01f, input.VignetteAngularSpeedFull);
                float speedSpanRcp = math.rcp(speedFull - speedStart);
                float vignette01 = math.saturate((input.HeadAngularSpeed - speedStart) * speedSpanRcp);
                vignette01 *= math.saturate(input.VignetteMaximum);
                vignette01 = math.max(vignette01, math.saturate(input.AccelerationVignette01));

                Output[0] = new VRSomaticRootSyncOutput
                {
                    RootPosition = math.all(math.isfinite(input.HeadPosition)) ? input.HeadPosition : float3.zero,
                    RootRotation = rootRotation,
                    ComfortVignette01 = vignette01
                };
            }

            private static float ResolveJobBlend(float sharpness, float deltaTime)
            {
                float x = math.min(math.max(sharpness, 0f) * math.max(deltaTime, MinimumDeltaTime), 32f);
                return math.saturate(x * math.rcp(1f + x));
            }

            private static quaternion Nlerp(quaternion fromRotation, quaternion toRotation, float blend01)
            {
                float4 from = fromRotation.value;
                float4 to = toRotation.value;
                if (math.dot(from, to) < 0f)
                    to = -to;

                float4 blended = math.lerp(from, to, math.saturate(blend01));
                return SanitizeQuaternion(new quaternion(blended), toRotation);
            }

            private static quaternion SanitizeQuaternion(quaternion value, quaternion fallback)
            {
                float4 q = value.value;
                float lengthSq = math.lengthsq(q);
                if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                    return fallback;

                return new quaternion(q * math.rsqrt(lengthSq));
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct VRSomaticHandKinematicsJob : IJobParallelFor
        {
            public float DeltaTime;
            public float SpringForce;
            [ReadOnly] public NativeArray<float3> HandTargets;
            public NativeArray<float3> HandPhysicalPositions;

            public void Execute(int index)
            {
                float3 target = HandTargets[index];
                float3 physical = HandPhysicalPositions[index];
                if (!math.all(math.isfinite(target)))
                    target = physical;
                if (!math.all(math.isfinite(physical)))
                    physical = target;

                float3 velocity = (target - physical) * math.max(0f, SpringForce);
                float3 next = physical + (velocity * math.max(DeltaTime, MinimumDeltaTime));
                HandPhysicalPositions[index] = math.all(math.isfinite(next)) ? next : target;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct VRSomaticBlackBoxEntry
        {
            public int Frame;
            public uint StateHash;
            public ushort Flags;
            public ushort HandGhostMask;
            public float3 HeadPosition;
            public float4 HeadRotation;
            public float NearCollision01;
            public float ComfortVignette01;
            public float LeftHandSeparationSq;
            public float RightHandSeparationSq;
            public float HeadAngularSpeedRadiansPerSecond;
            public uint AupShiftSequence;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct VRSomaticRootSyncInput
        {
            public float3 HeadPosition;
            public quaternion HeadRotation;
            public quaternion PreviousRootRotation;
            public float DeltaTime;
            public float HeadAngularSpeed;
            public float RootRotationSharpness;
            public float VignetteAngularSpeedStart;
            public float VignetteAngularSpeedFull;
            public float VignetteMaximum;
            public float AccelerationVignette01;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct VRSomaticRootSyncOutput
        {
            public float3 RootPosition;
            public quaternion RootRotation;
            public float ComfortVignette01;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct BuildHeadCapsulecastCommandsJob : IJobParallelFor
        {
            public float3 HeadPosition;
            public quaternion HeadRotation;
            public float3 HeadUp;
            public float CapsuleHalfHeight;
            public float CapsuleRadius;
            public float CastDistance;
            public QueryParameters QueryParameters;

            [WriteOnly] public NativeArray<CapsulecastCommand> Commands;

            public void Execute(int index)
            {
                float3 localDirection = ResolveLocalDirection(index);
                float3 direction = math.rotate(HeadRotation, localDirection);
                float3 up = HeadUp;
                float3 point1 = HeadPosition - (up * CapsuleHalfHeight);
                float3 point2 = HeadPosition + (up * CapsuleHalfHeight);

                Commands[index] = new CapsulecastCommand(
                    point1,
                    point2,
                    CapsuleRadius,
                    direction,
                    QueryParameters,
                    CastDistance);
            }

            private static float3 ResolveLocalDirection(int index)
            {
                switch (index)
                {
                    case 1: return new float3(0f, 0f, -1f);
                    case 2: return new float3(1f, 0f, 0f);
                    case 3: return new float3(-1f, 0f, 0f);
                    case 4: return new float3(0f, 1f, 0f);
                    case 5: return new float3(0f, -1f, 0f);
                    default: return new float3(0f, 0f, 1f);
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct ProcessHeadCapsulecastHitsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [WriteOnly] public NativeArray<HeadCastSample> Samples;

            public void Execute(int index)
            {
                RaycastHit hit = Hits[index * HeadCollisionMaxHitsPerCommand];
                float3 point = hit.point;
                float3 normal = hit.normal;
                float normalSq = math.lengthsq(normal);
                bool hasHit =
                    hit.distance >= 0f &&
                    math.isfinite(hit.distance) &&
                    math.all(math.isfinite(point)) &&
                    math.all(math.isfinite(normal)) &&
                    math.isfinite(normalSq) &&
                    normalSq >= HitNormalLengthSqMinimum &&
                    normalSq <= HitNormalLengthSqMaximum;
                float3 safeNormal = float3.zero;
                if (hasHit)
                {
                    float inverseNormalLength = math.abs(normalSq - 1f) <= HitNormalUnitLengthSqEpsilon
                        ? 1f
                        : ApproximateInverseLengthNoSqrt(normalSq);
                    safeNormal = normal * inverseNormalLength;
                }

                Samples[index] = new HeadCastSample
                {
                    HasHit = hasHit ? 1 : 0,
                    Distance = hasHit ? math.max(0f, hit.distance) : 0f,
                    Point = hasHit ? point : float3.zero,
                    Normal = safeNormal,
                    LocalSide = ResolveLocalSide(index)
                };
            }

            private static float ResolveLocalSide(int index)
            {
                switch (index)
                {
                    case 2: return 1f;
                    case 3: return -1f;
                    default: return 0f;
                }
            }

            private static float ApproximateInverseLengthNoSqrt(float lengthSq)
            {
                return math.rcp(0.5f + (0.5f * math.max(lengthSq, 0.000001f)));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct HeadCastSample
        {
            public float3 Point;
            public float3 Normal;
            public float Distance;
            public float LocalSide;
            public int HasHit;
        }
    }
}
