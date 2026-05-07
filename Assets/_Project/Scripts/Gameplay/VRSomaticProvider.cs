using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

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
        private const float MinimumDeltaTime = 0.0001f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte HapticPriorityCritical = 3;
        private const byte HapticBlendAdditive = 2;

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
        private NativeArray<HeadCastRuntime> _headCollisionRuntime;
        private NativeArray<HeadCastSample> _headCollisionSamples;
        private JobHandle _headCollisionHandle;
        private bool _headCollisionScheduled;

        private bool _registeredService;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _hasPreviousHeadPose;
        private Vector3 _previousHeadPosition;
        private Quaternion _previousHeadRotation = Quaternion.identity;
        private Quaternion _headRotationFrame1 = Quaternion.identity;
        private Quaternion _headRotationFrame2 = Quaternion.identity;
        private Quaternion _headRotationFrame3 = Quaternion.identity;
        private Quaternion _torsoRotation = Quaternion.identity;
        private Quaternion _pdaSocketLocalRotation = Quaternion.identity;
        private Quaternion _flareSocketLocalRotation = Quaternion.identity;
        private float _headLinearSpeedMetersPerSecond;
        private float _headAngularSpeedRadiansPerSecond;
        private float _lastTickDeltaTime;
        private float _nextImpactHapticTime;
        private float _nearFieldCollision01;
        private float _playerStress01;
        private float _oxygen01 = 1f;
        private float _depthMeters;
        private float _condensation01;
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
            this.hmdTransform = hmdTransform;
            this.visorHudRoot = visorHudRoot;
            this.pdaChestSocket = pdaChestSocket;
            this.flareToolChestSocket = flareToolChestSocket;
            this.breathingSource = breathingSource;
            this.breathingLowPassFilter = breathingLowPassFilter;
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
            float safeDeltaTime = math.max(0f, deltaTime);
            _lastTickDeltaTime = safeDeltaTime;

            if (!TryResolveActiveHmd(out Transform activeHmd))
            {
                ApplyInactiveState(safeDeltaTime);
                return;
            }

            Vector3 headPosition = activeHmd.position;
            Quaternion headRotation = activeHmd.rotation;
            UpdateHeadMotion(headPosition, headRotation, safeDeltaTime);
            ResolvePlayerSignals(out _playerStress01, out _oxygen01, out _depthMeters);
            UpdateChestSockets(headPosition, headRotation);
            Quaternion visorRotation = ResolveVisorHudRotation(headPosition, headRotation);
            UpdateBreathingAudio();
            UpdateCondensation();
            PublishSnapshot(headPosition, headRotation, visorRotation);
            PublishShaderState();
            ScheduleHeadCollisionBatch(headPosition, headRotation);
        }

        public void LateFrameTick()
        {
            if (!_headCollisionScheduled || !_headCollisionHandle.IsCompleted)
                return;

            _headCollisionHandle.Complete();
            _headCollisionScheduled = false;
            if (!_snapshot.IsActive)
            {
                _collisionState = default;
                PublishShaderState();
                return;
            }

            ConsumeHeadCollisionSamples();
            PublishShaderState();
        }

        private void Awake()
        {
            CacheSocketRotations();
            EnsureNativeBuffers();
        }

        private void OnEnable()
        {
            CacheSocketRotations();
            EnsureNativeBuffers();
            TryRegisterService();
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
            ApplyInactiveState(0f);
        }

        private void OnDestroy()
        {
            DisposeNativeBuffers();
        }

        private void OnValidate()
        {
            CacheSocketRotations();
        }

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterVRSomaticProvider(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterVRSomaticProvider(this);
            _registeredService = false;
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = true;
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = true;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private bool TryResolveActiveHmd(out Transform activeHmd)
        {
            activeHmd = hmdTransform;
            if (!Application.isPlaying || !IsVRSomaticRuntimeActive())
                return false;

            if (activeHmd != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                return false;

            activeHmd = playerCamera.transform;
            return activeHmd != null;
        }

        private static bool IsVRSomaticRuntimeActive()
        {
            return HectonXRRuntimeState.IsXRActive || (XRSettings.enabled && XRSettings.isDeviceActive);
        }

        private void UpdateHeadMotion(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if (!_hasPreviousHeadPose)
            {
                _previousHeadPosition = headPosition;
                _previousHeadRotation = headRotation;
                _headRotationFrame1 = headRotation;
                _headRotationFrame2 = headRotation;
                _headRotationFrame3 = headRotation;
                _hasPreviousHeadPose = true;
                _headLinearSpeedMetersPerSecond = 0f;
                _headAngularSpeedRadiansPerSecond = 0f;
                return;
            }

            float safeDeltaTime = math.max(deltaTime, MinimumDeltaTime);
            _headLinearSpeedMetersPerSecond = ApproximateMagnitudeNoSqrt(headPosition - _previousHeadPosition) / safeDeltaTime;
            _headAngularSpeedRadiansPerSecond =
                ApproximateAngularDeltaRadiansNoAcos(_previousHeadRotation, headRotation) / safeDeltaTime;

            _headRotationFrame3 = _headRotationFrame2;
            _headRotationFrame2 = _headRotationFrame1;
            _headRotationFrame1 = headRotation;
            _previousHeadPosition = headPosition;
            _previousHeadRotation = headRotation;
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

            depthMeters = hasMovement ? math.max(0f, movementState.DepthMeters) : 0f;
            if (hasSurvival)
            {
                oxygen01 = math.saturate(survivalState.OxygenNormalized);
                stress01 = math.max(
                    1f - oxygen01,
                    math.max(
                        survivalState.PressureExposureSeverity01,
                        math.max(
                            survivalState.ThermalStressSeverity01,
                            math.max(survivalState.RapidAscentRisk01, survivalState.NitrogenNarcosis01))));
            }

            if (hasMovement)
                stress01 = math.max(stress01, math.saturate(movementState.UnderwaterStressIntensity01));

            HectonPlayerMovement movement = runtimeContext.PlayerMovement;
            if (movement != null)
                stress01 = math.max(stress01, math.max(movement.CurrentHullStress01, movement.CurrentUnderwaterStressIntensity01));

            HectonSurvivalSystem survival = runtimeContext.SurvivalSystem;
            if (survival != null && !hasSurvival)
            {
                oxygen01 = math.saturate(survival.OxygenNormalized);
                depthMeters = math.max(depthMeters, survival.Depth);
                stress01 = math.max(
                    stress01,
                    math.max(1f - oxygen01, math.max(survival.PressureExposureSeverity01, survival.ThermalStressSeverity01)));
            }

            stress01 = math.saturate(stress01);
            oxygen01 = math.saturate(oxygen01);
            depthMeters = math.max(0f, depthMeters);
        }

        private void UpdateChestSockets(Vector3 headPosition, Quaternion headRotation)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(headRotation * Vector3.forward, Vector3.up);
            if (planarForward.sqrMagnitude > 0.0001f)
                _torsoRotation = Quaternion.LookRotation(planarForward.normalized, Vector3.up);

            _pdaSocketPose = ResolveSocketPose(headPosition, pdaChestOffset, _pdaSocketLocalRotation);
            _flareSocketPose = ResolveSocketPose(headPosition, flareToolChestOffset, _flareSocketLocalRotation);

            if (pdaChestSocket != null)
                pdaChestSocket.SetPositionAndRotation(_pdaSocketPose.RuntimePosition, _pdaSocketPose.RuntimeRotation);
            if (flareToolChestSocket != null)
                flareToolChestSocket.SetPositionAndRotation(_flareSocketPose.RuntimePosition, _flareSocketPose.RuntimeRotation);
        }

        private VRSomaticChestSocketPose ResolveSocketPose(
            Vector3 headPosition,
            Vector3 localOffset,
            Quaternion localRotation)
        {
            Vector3 socketPosition = headPosition + (_torsoRotation * localOffset);
            Quaternion socketRotation = _torsoRotation * localRotation;
            return new VRSomaticChestSocketPose(
                AbsoluteUniversePosition.FromRuntimePosition(socketPosition),
                socketPosition,
                socketRotation);
        }

        private Quaternion ResolveVisorHudRotation(Vector3 headPosition, Quaternion headRotation)
        {
            Quaternion laggedRotation = headRotation;
            if (applyVisorHudHeadLag)
            {
                float angular01 = math.saturate(_headAngularSpeedRadiansPerSecond / math.max(0.25f, visorLagAngularSpeedForFull));
                float lagBlend = math.saturate(angular01 * visorLagMaximumBlend);
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

            float oxygenDanger01 = 1f - _oxygen01;
            float depth01 = math.saturate(_depthMeters / 1400f);
            float drive01 = math.saturate(math.max(_playerStress01, math.max(oxygenDanger01 * 1.15f, _nearFieldCollision01 * 0.5f)));

            breathingSource.spatialBlend = 0f;
            breathingSource.panStereo = 0f;
            breathingSource.loop = true;
            breathingSource.volume = math.lerp(breathingBaseVolume, breathingStressVolume, drive01);
            breathingSource.pitch = math.lerp(breathingMinimumPitch, breathingMaximumPitch, math.max(_playerStress01, oxygenDanger01));

            if (breathingLowPassFilter != null)
            {
                float lowPass01 = math.saturate(math.max(oxygenDanger01, depth01 * 0.55f));
                breathingLowPassFilter.enabled = true;
                breathingLowPassFilter.cutoffFrequency = math.lerp(breathingLowPassOpenHz, breathingLowPassClosedHz, lowPass01);
                breathingLowPassFilter.lowpassResonanceQ = math.lerp(1f, 1.65f, lowPass01);
            }

            if (!breathingSource.isPlaying && breathingSource.clip != null)
                breathingSource.Play();
        }

        private void UpdateCondensation()
        {
            float oxygenDanger01 = 1f - _oxygen01;
            float depth01 = math.saturate(_depthMeters / 1400f);
            float target = math.saturate((_playerStress01 * 0.58f) + (oxygenDanger01 * 0.32f) + (depth01 * 0.28f));
            float blend = ResolveCinematicBlendApprox(8f, _lastTickDeltaTime);
            _condensation01 = math.lerp(_condensation01, target, blend);
        }

        private void PublishSnapshot(Vector3 headPosition, Quaternion headRotation, Quaternion visorRotation)
        {
            _snapshot = new VRSomaticSnapshot(
                true,
                AbsoluteUniversePosition.FromRuntimePosition(headPosition),
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
            if (_headCollisionScheduled || !_headCollisionCommands.IsCreated)
                return;

            QueryParameters queryParameters = new QueryParameters(
                nearFieldCollisionMask.value,
                false,
                QueryTriggerInteraction.Ignore);

            BuildHeadCapsulecastCommandsJob buildJob = new BuildHeadCapsulecastCommandsJob
            {
                HeadPosition = headPosition,
                HeadRotation = headRotation,
                CapsuleHalfHeight = math.max(0.01f, headCapsuleHalfHeightMeters),
                CapsuleRadius = math.max(0.01f, headCapsuleRadiusMeters),
                CastDistance = math.max(0.01f, nearFieldDistanceMeters),
                QueryParameters = queryParameters,
                Commands = _headCollisionCommands,
                Runtime = _headCollisionRuntime
            };

            ProcessHeadCapsulecastHitsJob processJob = new ProcessHeadCapsulecastHitsJob
            {
                Hits = _headCollisionHits,
                Runtime = _headCollisionRuntime,
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
            _headCollisionScheduled = true;
        }

        private void ConsumeHeadCollisionSamples()
        {
            bool hasContact = false;
            HeadCastSample bestSample = default;
            float bestDistance = math.max(0.01f, nearFieldDistanceMeters);
            for (int i = 0; i < HeadCollisionCommandCount; i++)
            {
                HeadCastSample sample = _headCollisionSamples[i];
                if (sample.HasHit == 0 || sample.Distance > bestDistance)
                    continue;

                bestDistance = sample.Distance;
                bestSample = sample;
                hasContact = true;
            }

            float targetIntensity = 0f;
            if (hasContact)
                targetIntensity = 1f - math.saturate(bestDistance / math.max(0.01f, nearFieldDistanceMeters));

            float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, _lastTickDeltaTime);
            _nearFieldCollision01 = math.lerp(_nearFieldCollision01, targetIntensity, blend);

            if (!hasContact)
            {
                _collisionState = default;
                return;
            }

            Vector3 normal = (Vector3)bestSample.Normal;
            Vector3 point = (Vector3)bestSample.Point;
            _collisionState = new VRSomaticCollisionState(
                true,
                AbsoluteUniversePosition.FromRuntimePosition(point),
                point,
                normal,
                bestDistance,
                _nearFieldCollision01,
                _headLinearSpeedMetersPerSecond);

            TryEmitImpactHaptics(normal, _nearFieldCollision01);
        }

        private void TryEmitImpactHaptics(Vector3 worldNormal, float intensity01)
        {
            if (_headLinearSpeedMetersPerSecond < impactSpeedThresholdMetersPerSecond)
                return;

            float now = Time.unscaledTime;
            if (now < _nextImpactHapticTime)
                return;

            float speedSpan = math.max(impactSpeedThresholdMetersPerSecond, 0.25f);
            float speed01 = math.saturate((_headLinearSpeedMetersPerSecond - impactSpeedThresholdMetersPerSecond) / speedSpan);
            float impact01 = math.saturate(math.max(intensity01, speed01));
            byte motorMask = ResolveDirectionalMotorMask(worldNormal);
            ToolHapticsRuntime.EnqueueCommand(
                maxLowFrequencyImpact * impact01,
                maxHighFrequencyImpact * impact01,
                impactHapticDurationSeconds,
                impactHapticDecayRate,
                HapticPriorityCritical,
                motorMask,
                HapticBlendAdditive);
            _nextImpactHapticTime = now + impactHapticDebounceSeconds;
        }

        private byte ResolveDirectionalMotorMask(Vector3 worldNormal)
        {
            if (!_snapshot.IsActive)
                return BothMotorMask;

            Vector3 localNormal = Quaternion.Inverse(_snapshot.HeadRuntimeRotation) * worldNormal;
            if (localNormal.x > 0.2f)
                return RightMotorMask;
            if (localNormal.x < -0.2f)
                return LeftMotorMask;
            return BothMotorMask;
        }

        private void ApplyInactiveState(float deltaTime)
        {
            float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, deltaTime);
            _nearFieldCollision01 = math.lerp(_nearFieldCollision01, 0f, blend);
            _condensation01 = math.lerp(_condensation01, 0f, blend);
            _playerStress01 = 0f;
            _oxygen01 = 1f;
            _depthMeters = 0f;
            _collisionState = default;
            _snapshot = VRSomaticSnapshot.Inactive;
            _hasPreviousHeadPose = false;
            if (breathingSource != null)
                breathingSource.volume = 0f;
            PublishShaderState();
        }

        private void PublishShaderState()
        {
            Shader.SetGlobalFloat(NearCollisionIntensityId, math.saturate(_nearFieldCollision01));
            Shader.SetGlobalFloat(SomaticCondensationId, math.saturate(_condensation01));
            Shader.SetGlobalVector(
                SomaticStateId,
                new Vector4(_playerStress01, _oxygen01, _depthMeters, _headLinearSpeedMetersPerSecond));
        }

        private void CacheSocketRotations()
        {
            _pdaSocketLocalRotation = Quaternion.Euler(pdaChestRotationEuler);
            _flareSocketLocalRotation = Quaternion.Euler(flareToolChestRotationEuler);
        }

        private static float ResolveCinematicBlendApprox(float sharpness, float deltaTime)
        {
            if (deltaTime <= 0f || sharpness <= 0f)
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
            float lengthSq = math.dot(blended, blended);
            float inverseLengthApprox = math.max(0.25f, 1.5f - (0.5f * lengthSq));
            quaternion approximated = blended * inverseLengthApprox;
            return approximated;
        }

        private void EnsureNativeBuffers()
        {
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
            _headCollisionRuntime = new NativeArray<HeadCastRuntime>(
                HeadCollisionCommandCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<HeadCastRuntime>[6] - VR somatic sweep directions - owner: VRSomaticProvider
            _headCollisionSamples = new NativeArray<HeadCastSample>(
                HeadCollisionCommandCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadCastSample>[6] - VR somatic processed contact samples - owner: VRSomaticProvider

            NativeMemorySentinel.RegisterNativeArray(_headCollisionCommands, nameof(VRSomaticProvider), nameof(_headCollisionCommands), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_headCollisionHits, nameof(VRSomaticProvider), nameof(_headCollisionHits), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_headCollisionRuntime, nameof(VRSomaticProvider), nameof(_headCollisionRuntime), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_headCollisionSamples, nameof(VRSomaticProvider), nameof(_headCollisionSamples), NativeAllocationLifetime.Scene);
        }

        private void DisposeNativeBuffers()
        {
            JobHandle disposeHandle = _headCollisionHandle;
            DisposeNativeArray(ref _headCollisionCommands, ref disposeHandle);
            DisposeNativeArray(ref _headCollisionHits, ref disposeHandle);
            DisposeNativeArray(ref _headCollisionRuntime, ref disposeHandle);
            DisposeNativeArray(ref _headCollisionSamples, ref disposeHandle);
            _headCollisionHandle = disposeHandle;
            _headCollisionScheduled = false;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle disposeHandle) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            disposeHandle = array.Dispose(disposeHandle);
            array = default;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildHeadCapsulecastCommandsJob : IJobParallelFor
        {
            public float3 HeadPosition;
            public quaternion HeadRotation;
            public float CapsuleHalfHeight;
            public float CapsuleRadius;
            public float CastDistance;
            public QueryParameters QueryParameters;

            [WriteOnly] public NativeArray<CapsulecastCommand> Commands;
            [WriteOnly] public NativeArray<HeadCastRuntime> Runtime;

            public void Execute(int index)
            {
                float3 localDirection = ResolveLocalDirection(index);
                float3 direction = math.rotate(HeadRotation, localDirection);
                float3 up = math.rotate(HeadRotation, new float3(0f, 1f, 0f));
                float3 point1 = HeadPosition - (up * CapsuleHalfHeight);
                float3 point2 = HeadPosition + (up * CapsuleHalfHeight);

                Commands[index] = new CapsulecastCommand(
                    point1,
                    point2,
                    CapsuleRadius,
                    direction,
                    QueryParameters,
                    CastDistance);
                Runtime[index] = new HeadCastRuntime
                {
                    Direction = direction
                };
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
        private struct ProcessHeadCapsulecastHitsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public NativeArray<HeadCastRuntime> Runtime;
            [WriteOnly] public NativeArray<HeadCastSample> Samples;

            public void Execute(int index)
            {
                RaycastHit hit = Hits[index * HeadCollisionMaxHitsPerCommand];
                HeadCastRuntime runtime = Runtime[index];
                float3 point = hit.point;
                float3 normal = hit.normal;
                bool hasHit =
                    hit.distance >= 0f &&
                    math.lengthsq(normal) > 0.000001f &&
                    !math.any(math.isnan(point));

                Samples[index] = new HeadCastSample
                {
                    HasHit = hasHit ? 1 : 0,
                    Distance = hasHit ? math.max(0f, hit.distance) : 0f,
                    Point = hasHit ? point : float3.zero,
                    Normal = hasHit ? normal : float3.zero,
                    Direction = runtime.Direction
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeadCastRuntime
        {
            public float3 Direction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeadCastSample
        {
            public float3 Point;
            public float3 Normal;
            public float3 Direction;
            public float Distance;
            public int HasHit;
        }
    }
}
