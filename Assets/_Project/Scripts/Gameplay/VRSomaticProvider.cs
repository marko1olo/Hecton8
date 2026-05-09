using System.Runtime.InteropServices;
using Hecton8.Core;
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
    public sealed class VRSomaticProvider : MonoBehaviour, IVRSomaticProvider, IUpdatable, ILateFrameTickable
    {
        private const int HeadCollisionCommandCount = 6;
        private const int HeadCollisionMaxHitsPerCommand = 1;
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
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
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte HapticBlendAdditive = ToolHapticsRuntime.BlendModeAdditive;
        private const float AupCellSizeMeters = 5000f;
        private const float HapticSideThreshold = 0.2f;
        private const uint StateHeadCollisionScheduled = 1u << 0;
        private const uint StateRegisteredService = 1u << 1;
        private const uint StateRegisteredUpdate = 1u << 2;
        private const uint StateRegisteredLateFrame = 1u << 3;
        private const uint StateHasPreviousHeadPose = 1u << 4;
        private const uint StateSubscribedXRRuntime = 1u << 5;
        private const uint StateBreathingAudioStaticApplied = 1u << 6;
        private const uint StateBreathingLowPassStaticApplied = 1u << 7;
        private const uint StateBreathingSourcePlaying = 1u << 8;

        private static readonly int NearCollisionIntensityId = Shader.PropertyToID("_HectonVRNearCollisionIntensity");
        private static readonly int SomaticCondensationId = Shader.PropertyToID("_HectonVRSomaticCondensation");
        private static readonly int SomaticStateId = Shader.PropertyToID("_HectonVRSomaticState");

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

        [Header("Chest Sockets")]
        [SerializeField] private Vector3 pdaChestOffset = new Vector3(-0.18f, -0.34f, 0.22f);
        [SerializeField] private Vector3 pdaChestRotationEuler = new Vector3(8f, -12f, -6f);
        [SerializeField] private Vector3 flareToolChestOffset = new Vector3(0.18f, -0.36f, 0.19f);
        [SerializeField] private Vector3 flareToolChestRotationEuler = new Vector3(10f, 14f, 8f);

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
        private JobHandle _headCollisionHandle;
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
        private Transform _fallbackHmdTransform;
        private float _headLinearSpeedMetersPerSecond;
        private float _headAngularSpeedRadiansPerSecond;
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
        private float _playerSignalSampleRemaining;
        private Vector4 _lastPublishedSomaticState = Vector4.positiveInfinity;
        private VRSomaticChestSocketPose _pdaSocketPose;
        private VRSomaticChestSocketPose _flareSocketPose;
        private VRSomaticCollisionState _collisionState;
        private VRSomaticSnapshot _snapshot = VRSomaticSnapshot.Inactive;

        public bool IsActive => _snapshot.IsActive;
        public VRSomaticSnapshot CurrentSnapshot => _snapshot;

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

        public bool TryGetNearFieldCollision(out VRSomaticCollisionState collisionState)
        {
            collisionState = _collisionState;
            return _snapshot.IsActive && _collisionState.HasContact && _collisionState.Intensity01 > 0.001f;
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _lastTickDeltaTime = safeDeltaTime;
            AdvanceSomaticTimers(safeDeltaTime);

            if (!TryResolveActiveHmd(out Transform activeHmd))
            {
                ApplyInactiveState(safeDeltaTime);
                return;
            }

            activeHmd.GetPositionAndRotation(out Vector3 headPosition, out Quaternion headRotation);
            if (!IsFiniteVector(headPosition) || !TrySanitizeQuaternion(headRotation, out headRotation))
            {
                ApplyInactiveState(safeDeltaTime);
                return;
            }

            AbsoluteUniversePosition headAup = HectonXRRuntimeState.TryResolveCachedHeadAup(headPosition, out AbsoluteUniversePosition cachedHeadAup)
                ? cachedHeadAup
                : AbsoluteUniversePosition.FromRuntimePosition(headPosition);
            UpdateHeadMotion(headPosition, headRotation, safeDeltaTime);
            RefreshPlayerSignalsIfDue();
            UpdateChestSockets(in headAup, headRotation);
            Quaternion visorRotation = ResolveVisorHudRotation(headPosition, headRotation);
            RefreshNearFieldCollisionQueryAvailability(safeDeltaTime);
            UpdateBreathingAudio();
            UpdateCondensation();
            PublishSnapshot(in headAup, headPosition, headRotation, visorRotation);
            PublishShaderState();
            ScheduleHeadCollisionBatch(headPosition, headRotation);
        }

        public void LateFrameTick()
        {
            if ((_stateFlags & StateHeadCollisionScheduled) == 0u)
                return;

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
                return;
            }

            ConsumeHeadCollisionSamples();
            PublishShaderState();
        }

        private void Awake()
        {
            CacheSocketRotations();
        }

        private void OnEnable()
        {
            CacheSocketRotations();
            TrySubscribeXRRuntime();
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
                bool hadRuntimeState = HasRuntimeRegistrationOrActiveSnapshot();
                TryUnregisterLateFrame();
                TryUnregisterUpdate();
                TryUnregisterService();
                ApplyInactiveState(0f, hadRuntimeState);
                DisposeNativeBuffers();
                return;
            }

            EnsureNativeBuffers();
            TryRegisterService();
            TryRegisterUpdate();
            TryRegisterLateFrame();
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

        private bool HasRuntimeRegistrationOrActiveSnapshot()
        {
            const uint runtimeMask =
                StateHeadCollisionScheduled |
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
                q *= math.rsqrt(math.max(lengthSq, 0.000001f));

            sanitized = new Quaternion(q.x, q.y, q.z, q.w);
            return true;
        }

        private void UpdateHeadMotion(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if ((_stateFlags & StateHasPreviousHeadPose) == 0u)
            {
                ResetHeadMotionHistory(headPosition, headRotation);
                return;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, MinimumDeltaTime) : MinimumDeltaTime;
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
                ResetHeadMotionHistory(headPosition, headRotation);
                return;
            }

            _headLinearSpeedMetersPerSecond = math.min(
                ApproximateMagnitudeNoSqrt(headDelta) / safeDeltaTime,
                MaxSomaticHeadLinearSpeedMetersPerSecond);
            _headAngularSpeedRadiansPerSecond = math.min(
                angularDelta / safeDeltaTime,
                MaxSomaticHeadAngularSpeedRadiansPerSecond);

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
            _playerSignalSampleRemaining = 0f;
            _impactHapticCooldownRemaining = 0f;
            _fallbackHmdTransform = null;
            _collisionState = default;
            _snapshot = VRSomaticSnapshot.Inactive;
            _stateFlags &= ~StateHasPreviousHeadPose;
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
        }

        private void InvalidateShaderPublishCache()
        {
            _lastPublishedNearCollision01 = float.PositiveInfinity;
            _lastPublishedCondensation01 = float.PositiveInfinity;
            _lastPublishedSomaticState = Vector4.positiveInfinity;
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

        private void EnsureNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            if (!_headCollisionDisposeHandle.IsCompleted)
                return;

            if (_headCollisionCommands.IsCreated)
                return;

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

        private void DisposeNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            bool hasPendingDispose = !_headCollisionDisposeHandle.IsCompleted;
            JobHandle disposeHandle = hasPendingDispose
                ? JobHandle.CombineDependencies(_headCollisionDisposeHandle, _headCollisionHandle)
                : _headCollisionHandle;
            bool scheduledDispose = false;

            DisposeNativeArray(ref _headCollisionCommands, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _headCollisionHits, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _headCollisionSamples, ref disposeHandle, ref scheduledDispose);
            _headCollisionHandle = default;
            _stateFlags &= ~StateHeadCollisionScheduled;

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
        [StructLayout(LayoutKind.Sequential)]
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
        [StructLayout(LayoutKind.Sequential)]
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
