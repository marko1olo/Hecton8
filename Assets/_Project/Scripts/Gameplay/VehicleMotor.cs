using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Physics;
using Hecton8.Physics.CCD;
using Hecton8.World;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Kinematic vehicle motor with deferred capsule sweep consumption for mountable transports.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Vehicle Motor")]
    public sealed class VehicleMotor : MonoBehaviour, IOriginShiftListener, ILateFrameTickable, IPostFixedTickable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        internal struct SubmarineState
        {
            public float3 RuntimePosition;
            public quaternion RuntimeRotation;
            public float3 LinearVelocity;
            public float3 AngularVelocityRadians;
            public AbsoluteUniversePositionBlit128 Aup;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        internal struct HydrodynamicWakeSample
        {
            public float3 RuntimePosition;
            public float3 Velocity;
            public float3 Forward;
            public AbsoluteUniversePositionBlit128 Aup;
            public float SpeedMetersPerSecond;
            public float SubmersionFactor;
            public float Intensity01;
            public float RadiusMeters;
            public int Sequence;
            public int Active;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct ScheduledSweepState
        {
            public Vector3 StartPosition;
            public Vector3 Direction;
            public float Distance;
            public float SkinWidth;
            public int SelfColliderInstanceId;
        }

        private const float MinVectorMagnitudeSq = 0.000001f;
        private const int ScheduledSweepCommandCount = 1;
        private const int ScheduledSweepMaxHits = 8;
        private const int MaxRegisteredMotors = 32;
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float DefaultGroundSlopeLimitDegrees = 45f;
        private const float TractionLossStartDegrees = 45f;
        private const float GroundContactHoldSeconds = 0.2f;
        private const float VehicleGravityAcceleration = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        private const float SlopeDot45Degrees = 0.70710678f;
        private const float GroundAlignmentSharpness = 10f;
        private const float DenormalVelocityFlushThresholdMetersPerSecond = 0.001f;
        private const float CinematicDepthReferenceMeters = 900f;
        private const float WakeSiltVisualSpeedThresholdMetersPerSecond = 15f;
        private const float WakeEmitterOffsetMeters = 4f;
        private const float WakeSiltDecalCooldownSeconds = 0.24f;
        private const int HydrodynamicWakeSampleCapacity = 64;
        private const int HydrodynamicWakeSampleMask = HydrodynamicWakeSampleCapacity - 1;
        private const float HydrodynamicWakeBaseRadiusMeters = 7.5f;
        private const string NativeMemoryOwner = nameof(VehicleMotor);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const float MinEntanglementTetherMeters = 1.25f;
        private const float EntanglementFacingSharpness = 8f;
        private const float KelpPushbackProbeRadiusMeters = 6f;
        private const float KelpPushbackMinSpeedMetersPerSecond = 0.5f;
        private const float KelpDragScale = 1.35f;
        private const float KelpMaxDragCoefficient = 2.8f;

        private static readonly VehicleMotor[] _registeredMotors = new VehicleMotor[MaxRegisteredMotors];
        private static readonly ProfilerMarker _scheduleProfilerMarker = new ProfilerMarker("H8.VehicleMotor.CapsuleSweep.Schedule");
        private static readonly ProfilerMarker _consumeProfilerMarker = new ProfilerMarker("H8.VehicleMotor.CapsuleSweep.Consume");
        private static readonly ProfilerMarker _driveProfilerMarker = new ProfilerMarker("H8.VehicleMotor.Drive");
        private static readonly uint _kinematicCcdSourceHash = unchecked((uint)Hecton.Localization.LocHash.Compute("KINEMATIC_CCD_VEHICLE"));

        [Header("-- Cinematic Hull Feel -------------")]
        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicForwardDragScale = 0.58f;

        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicLateralDragScale = 3.2f;

        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicVerticalDragScale = 1.7f;

        [Tooltip("Cinematic acceleration scalar for heavy hull feel without frame buffers.")]
        [SerializeField, Range(0.1f, 1f)] private float cinematicAccelerationScale = 0.72f;

        [Tooltip("Cinematic drag scalar for hull settle without frame buffers.")]
        [SerializeField, Range(1f, 4f)] private float cinematicDragScale = 1.35f;

        [Tooltip("Analytical drag multiplier applied only to mounted vehicle motors while submerged in generated brine.")]
        [SerializeField, Range(1f, 8f)] private float brineViscosityDragMultiplier = 4f;

        [Header("-- Headless Presentation -----------")]
        [Tooltip("Optional visual-only submarine root interpolated from the authoritative NativeArray state in the late-frame dispatcher lane.")]
        [SerializeField] private Transform headlessVisualRoot;

        [Tooltip("Interpolation sharpness used when smoothing the visual-only submarine root toward the headless kinematic state.")]
        [SerializeField, Min(0.01f)] private float headlessVisualInterpolationSharpness = 18f;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private float _brineViscosityQueryRadiusMeters = 0.5f;
        private float _brineViscosityVerticalHalfExtentMeters = 0.5f;
        private NativeArray<SubmarineState> _submarineState;
        private NativeArray<HydrodynamicWakeSample> _hydrodynamicWakeSamples;
        private NativeArray<int> _hydrodynamicWakeWriteState;
        private NativeArray<CapsulecastCommand> _scheduledSweepCommands;
        private NativeArray<RaycastHit> _scheduledSweepResults;
        private JobHandle _hydrodynamicWakeWriteHandle;
        private JobHandle _scheduledSweepHandle;
        private ScheduledSweepState _scheduledSweepState;
        private bool _hydrodynamicWakeWritePending;
        private bool _hydrodynamicWakeResetPending;
        private bool _scheduledSweepPending;
        private bool _scheduledSweepReady;
        private bool _scheduledSweepDiscardAfterCompletion;
        private bool _safeTeleportCollisionModeCaptured;
        private int _hydrodynamicWakeWriteIndex;
        private int _hydrodynamicWakeSequence;
        private Vector3 _linearVelocity;
        private Vector3 _localAngularVelocityDegrees;
        private float _groundSlopeLimitDegrees = DefaultGroundSlopeLimitDegrees;
        private Vector3 _groundNormal = Vector3.up;
        private float _groundContactTimer;
        private float _hydrodynamicSubmersionFactor;
        private float _hydrodynamicDepthMeters;
        private bool _isEntangled;
        private bool _motorRegistryRegistered;
        private bool _registeredOriginShiftListener;
        private bool _registeredLateFrameTick;
        private bool _registeredPostFixedTick;
        private bool _safeTeleportCollisionGuardActive;
        private bool _visualTeleportPending;
        private CollisionDetectionMode _collisionDetectionModeBeforeSafeTeleportGuard;
        private Vector3 _entanglementAnchorPosition;
        private AbsoluteUniversePosition _floraAnchorAup;
        private float _entanglementTetherLength;
        private float _lastEntanglementTensionNewtons;
        private float _lastKelpDensity01;
        private bool _hasFloraAnchorAup;
        private float _lastBlockingImpactSpeedMetersPerSecond;
        private Vector3 _lastBlockingImpactPoint;
        private Vector3 _lastBlockingImpactNormal = Vector3.up;
        private float _wakeSiltDecalCooldown;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct HydrodynamicWakeWriteJob : IJob
        {
            public NativeArray<HydrodynamicWakeSample> Samples;
            public NativeArray<int> WriteState;
            public float3 RuntimePosition;
            public float3 Velocity;
            public float3 Forward;
            public AbsoluteUniversePositionBlit128 Aup;
            public float SpeedMetersPerSecond;
            public float SubmersionFactor;

            public void Execute()
            {
                if (Samples.Length <= 0 || WriteState.Length < 2)
                    return;

                if (!math.all(math.isfinite(RuntimePosition)) ||
                    !math.all(math.isfinite(Velocity)) ||
                    !math.all(math.isfinite(Forward)))
                {
                    return;
                }

                float intensity01 = math.saturate((SpeedMetersPerSecond - WakeSiltVisualSpeedThresholdMetersPerSecond) / 18f);
                int writeIndex = WriteState[0] & HydrodynamicWakeSampleMask;
                int sequence = WriteState[1] + 1;
                Samples[writeIndex] = new HydrodynamicWakeSample
                {
                    RuntimePosition = RuntimePosition,
                    Velocity = Velocity,
                    Forward = Forward,
                    Aup = Aup,
                    SpeedMetersPerSecond = SpeedMetersPerSecond,
                    SubmersionFactor = SubmersionFactor,
                    Intensity01 = intensity01,
                    RadiusMeters = HydrodynamicWakeBaseRadiusMeters + intensity01 * 4f,
                    Sequence = sequence,
                    Active = 1
                };

                WriteState[0] = (writeIndex + 1) & HydrodynamicWakeSampleMask;
                WriteState[1] = sequence;
            }
        }

        /// <summary>Current kinematic linear velocity in world space.</summary>
        public Vector3 LinearVelocity => _linearVelocity;

        /// <summary>Authoritative rigidbody driven by this vehicle motor.</summary>
        internal Rigidbody Body => _body;

        /// <summary>Current presentation velocity. Inertial history buffers are intentionally purged.</summary>
        public Vector3 PerceivedLinearVelocity => HectonPlayerMotor.SafeVelocity(_linearVelocity);

        internal NativeArray<SubmarineState>.ReadOnly SubmarineStateNative => _submarineState.AsReadOnly();

        internal NativeArray<HydrodynamicWakeSample>.ReadOnly HydrodynamicWakeSamplesNative => _hydrodynamicWakeSamples.AsReadOnly();

        internal int HydrodynamicWakeSequence => _hydrodynamicWakeSequence;

        /// <summary>True while a deferred capsule sweep is waiting for consumption.</summary>
        public bool HasPendingSweep => _scheduledSweepPending || _scheduledSweepReady;

        /// <summary>Returns true when both rigidbody and capsule are available for kinematic sweep driving.</summary>
        public bool IsDriveReady => _body != null && _capsule != null;

        /// <summary>True while macro-flora entanglement is suppressing thrust and driving tethered motion.</summary>
        public bool IsEntangled => _isEntangled;

        /// <summary>Last deterministic tether tension solved by the macro-flora constraint, in newtons.</summary>
        public float LastEntanglementTensionNewtons => _lastEntanglementTensionNewtons;

        /// <summary>Last normalized dense-flora drag density sampled by the vehicle motor.</summary>
        public float LastKelpDensity01 => _lastKelpDensity01;

        internal static bool TryResolveForBody(Rigidbody body, out VehicleMotor motor)
        {
            motor = null;
            if (body == null)
                return false;

            for (int i = 0; i < _registeredMotors.Length; i++)
            {
                VehicleMotor candidate = _registeredMotors[i];
                if (candidate == null || candidate._body != body)
                    continue;

                motor = candidate;
                return true;
            }

            return false;
        }

        internal AbsoluteUniversePositionBlit128 FloraAnchorAup => _hasFloraAnchorAup ? _floraAnchorAup.ToAlignedBlit() : default;

        internal float LastBlockingImpactSpeedMetersPerSecond => _lastBlockingImpactSpeedMetersPerSecond;

        internal Vector3 LastBlockingImpactPoint => _lastBlockingImpactPoint;

        internal Vector3 LastBlockingImpactNormal => _lastBlockingImpactNormal;

        /// <summary>Binds the authoritative rigidbody and sweep capsule.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            _body = body;
            _capsule = capsule;
            CacheBrineViscosityQueryShape();
            EnsureSubmarineState();
            EnsureHydrodynamicWakeBuffer();
            RegisterMotor();
            TryRegisterOriginShiftListener();
            ResetRuntimeState();
            if (headlessVisualRoot != null)
                TryRegisterLateFrameTickable();
        }

        private void OnEnable()
        {
            CacheBrineViscosityQueryShape();
            EnsureHydrodynamicWakeBuffer();
            RegisterMotor();
            TryRegisterOriginShiftListener();
            if (headlessVisualRoot != null)
                TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTickable();
            CompleteSafeTeleportCollisionGuard();
            TryUnregisterOriginShiftListener();
            UnregisterMotor();
            DisposeScheduledSweepState();
            DisposeHydrodynamicWakeBuffer();
            DisposeSubmarineState();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrameTickable();
            CompleteSafeTeleportCollisionGuard();
            TryUnregisterOriginShiftListener();
            UnregisterMotor();
            DisposeScheduledSweepState();
            DisposeHydrodynamicWakeBuffer();
            DisposeSubmarineState();
        }

        /// <summary>Clears all accumulated transport motion state.</summary>
        public void ResetRuntimeState()
        {
            _linearVelocity = Vector3.zero;
            _localAngularVelocityDegrees = Vector3.zero;
            _groundNormal = Vector3.up;
            _groundContactTimer = 0f;
            _hydrodynamicSubmersionFactor = 0f;
            _hydrodynamicDepthMeters = 0f;
            _isEntangled = false;
            _entanglementAnchorPosition = Vector3.zero;
            _floraAnchorAup = default;
            _entanglementTetherLength = 0f;
            _lastEntanglementTensionNewtons = 0f;
            _lastKelpDensity01 = 0f;
            _hasFloraAnchorAup = false;
            ClearBlockingImpactCache();
            RequestDeferredHydrodynamicWakeReset();
            _visualTeleportPending = true;
            WriteSubmarineState(_body != null ? _body.position : Vector3.zero, _body != null ? _body.rotation : Quaternion.identity);
        }

        private void ClearBlockingImpactCache()
        {
            _lastBlockingImpactSpeedMetersPerSecond = 0f;
            _lastBlockingImpactPoint = Vector3.zero;
            _lastBlockingImpactNormal = Vector3.up;
        }

        /// <summary>Purges wake and visual-inertia state after origin teleport or docking hard-lock.</summary>
        public void ResetHydrodynamicPresentationState()
        {
            _linearVelocity = Vector3.zero;
            _localAngularVelocityDegrees = Vector3.zero;
            BeginSafeTeleportCollisionGuard();
            RequestImmediateHydrodynamicWakeResetWithoutSync();
            _visualTeleportPending = true;
            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }

                WriteSubmarineState(_body.position, _body.rotation);
            }
        }

        /// <summary>Configures the maximum climbable ground slope before vehicle drive is flattened against world up.</summary>
        public void ConfigureGroundSlopeLimit(float maxSlopeDegrees)
        {
            _groundSlopeLimitDegrees = math.clamp(maxSlopeDegrees, 5f, 89f);
        }

        /// <summary>Sets the current fluid-submersion factor used by cinematic drag.</summary>
        public void ConfigureHydrodynamicSubmersion(float submersionFactor)
        {
            _hydrodynamicSubmersionFactor = math.saturate(submersionFactor);
            if (_body != null)
                GlobalPhysicsStateManager.SetHydrodynamicSubmersion(_body, _hydrodynamicSubmersionFactor);
        }

        /// <summary>Sets the current water depth used by cinematic velocity bleed.</summary>
        public void ConfigureHydrodynamicDepth(float depthMeters)
        {
            _hydrodynamicDepthMeters = math.max(0f, depthMeters);
        }

        public float SampleMacroFloraDensityAlongVelocity(
            HectonMapMagicVegetationBridge vegetationBridge,
            float probeLengthMeters,
            int probeCount,
            float fixedDeltaTime)
        {
            _lastKelpDensity01 = 0f;
            if (vegetationBridge == null || _body == null || probeCount <= 0)
                return 0f;

            Vector3 velocity = HectonPlayerMotor.SafeVelocity(_linearVelocity);
            float speedSq = velocity.sqrMagnitude;
            if (speedSq <= MinVectorMagnitudeSq)
                return 0f;

            float inverseSpeed = math.rsqrt(speedSq);
            float speed = speedSq * inverseSpeed;
            Vector3 direction = velocity * inverseSpeed;
            int safeProbeCount = math.min(math.max(1, probeCount), 16);
            float probeDistance = math.max(1f, math.max(probeLengthMeters, speed * math.max(fixedDeltaTime, 0.0001f)));
            Vector3 origin = _body.worldCenterOfMass;
            float accumulatedDensity = 0f;
            for (int i = 0; i < safeProbeCount; i++)
            {
                float sampleT = (i + 1f) / safeProbeCount;
                Vector3 samplePosition = origin + direction * (probeDistance * sampleT);
                accumulatedDensity += vegetationBridge.SampleMacroFloraDensityImmediate(samplePosition);
            }

            _lastKelpDensity01 = math.saturate(accumulatedDensity / safeProbeCount);
            return _lastKelpDensity01;
        }

        /// <summary>Activates a kinematic macro-flora tether that suppresses thrust and constrains the vehicle to one anchor.</summary>
        public void BeginEntanglement(Vector3 anchorPosition, float tetherLength)
        {
            if (_body == null)
                return;

            float3 anchor = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            if (!math.all(math.isfinite(anchor)))
                return;

            Vector3 relative = _body.position - anchorPosition;
            float resolvedTetherLength = math.max(MinEntanglementTetherMeters, tetherLength);
            float relativeSqr = relative.sqrMagnitude;
            if (relativeSqr > MinVectorMagnitudeSq)
            {
                Vector3 radialDirection = relative * math.rsqrt(relativeSqr);
                _linearVelocity = ProjectOnUnitPlane(_linearVelocity, radialDirection);
                _linearVelocity = HectonPlayerMotor.SafeVelocity(_linearVelocity);
            }
            else
            {
                _linearVelocity = Vector3.zero;
            }

            _localAngularVelocityDegrees = Vector3.zero;
            _entanglementAnchorPosition = anchorPosition;
            _floraAnchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPosition);
            _entanglementTetherLength = resolvedTetherLength;
            _lastEntanglementTensionNewtons = 0f;
            _hasFloraAnchorAup = true;
            _isEntangled = true;
        }

        /// <summary>Clears the current macro-flora tether and restores normal thrust integration on the next tick.</summary>
        public void ClearEntanglement()
        {
            _isEntangled = false;
            _entanglementAnchorPosition = Vector3.zero;
            _floraAnchorAup = default;
            _entanglementTetherLength = 0f;
            _localAngularVelocityDegrees = Vector3.zero;
            _lastEntanglementTensionNewtons = 0f;
            _hasFloraAnchorAup = false;
        }

        internal bool WouldAmbientForceExtendEntanglement(Vector3 force, ForceMode mode, float fixedDeltaTime)
        {
            Rigidbody body = _body;
            if (!_isEntangled || body == null || fixedDeltaTime <= 0f)
                return false;

            Vector3 velocityDelta = ResolveVelocityDelta(force, mode, math.max(body.mass, 0.0001f), fixedDeltaTime);
            if (velocityDelta.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            Vector3 bodyLinearVelocity = body.linearVelocity;
            Vector3 bodyCenterOfMass = body.worldCenterOfMass;
            Vector3 candidateVelocity = HectonPlayerMotor.SafeVelocity(bodyLinearVelocity + velocityDelta, _linearVelocity);
            Vector3 predictedRelative = (bodyCenterOfMass + candidateVelocity * fixedDeltaTime) - _entanglementAnchorPosition;
            float tetherLength = math.max(MinEntanglementTetherMeters, _entanglementTetherLength);
            return predictedRelative.sqrMagnitude > tetherLength * tetherLength;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            ApplyOriginShift(shiftData.ShiftOffset, shiftData.IsSafeTeleport);
            if (shiftData.IsSafeTeleport)
                _visualTeleportPending = true;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_hydrodynamicWakeResetPending)
                TryResetHydrodynamicWakeBuffer();
            else
                TryCompleteHydrodynamicWakeWriteJob();

            ApplyHeadlessVisualInterpolation();
            if (!_hydrodynamicWakeWritePending && !_hydrodynamicWakeResetPending && headlessVisualRoot == null)
                TryUnregisterLateFrameTickable();
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            if (_scheduledSweepPending)
                TryFinalizeScheduledSweep();

            if (_safeTeleportCollisionGuardActive)
                CompleteSafeTeleportCollisionGuard();

            if (!_safeTeleportCollisionGuardActive && !_scheduledSweepPending)
            {
                TryUnregisterPostFixedTickable();
                return;
            }
        }

        /// <summary>Applies a floating-origin shift to cached kinematic positions owned by the motor.</summary>
        public void ApplyOriginShift(Vector3 shiftOffset)
        {
            ApplyOriginShift(shiftOffset, false);
        }

        private void ApplyOriginShift(Vector3 shiftOffset, bool purgeWake)
        {
            if (shiftOffset.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            if (_isEntangled)
                _entanglementAnchorPosition -= shiftOffset;

            if (_scheduledSweepReady)
            {
                _scheduledSweepReady = false;
                _scheduledSweepState = default;
                ClearBlockingImpactCache();
            }

            if (_scheduledSweepPending)
                _scheduledSweepDiscardAfterCompletion = true;

            if (_lastBlockingImpactPoint.sqrMagnitude > MinVectorMagnitudeSq)
                _lastBlockingImpactPoint -= shiftOffset;

            if (purgeWake)
                RequestImmediateHydrodynamicWakeResetWithoutSync();
            else
                RequestDeferredHydrodynamicWakeReset();
            _visualTeleportPending = true;

            Rigidbody body = _body;
            if (body != null)
                WriteSubmarineState(body.position, body.rotation);
        }

        /// <summary>Advances tethered current-driven motion while propulsion is locked out by macro-flora entanglement.</summary>
        public void AdvanceEntanglement(Vector3 currentFlowVelocity, float currentAcceleration, float linearDamping, float fixedDeltaTime)
        {
            Rigidbody body = _body;
            if (!_isEntangled || body == null || fixedDeltaTime <= 0f)
                return;

            using (_driveProfilerMarker.Auto())
            {
                float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);
                Vector3 currentPosition = body.position;
                Vector3 relative = currentPosition - _entanglementAnchorPosition;
                float relativeSqr = relative.sqrMagnitude;
                if (relativeSqr <= MinVectorMagnitudeSq)
                {
                    relative = body.rotation * Vector3.back * math.max(MinEntanglementTetherMeters, _entanglementTetherLength);
                    relativeSqr = relative.sqrMagnitude;
                }

                float tetherLength = math.max(MinEntanglementTetherMeters, _entanglementTetherLength);
                Vector3 safeFlowVelocity = HectonPlayerMotor.SafeVelocity(currentFlowVelocity);
                Vector3 candidateVelocity = _linearVelocity + (safeFlowVelocity * math.max(0f, currentAcceleration) * safeDeltaTime);
                candidateVelocity = ApplyCinematicVelocityBleed(candidateVelocity, math.max(0f, linearDamping), safeDeltaTime);

                Vector3 predictedRelative = relative + (candidateVelocity * safeDeltaTime);
                float predictedRelativeSqr = predictedRelative.sqrMagnitude;
                if (predictedRelativeSqr <= MinVectorMagnitudeSq)
                {
                    if (relativeSqr > MinVectorMagnitudeSq)
                    {
                        float inverseRelativeLength = math.rsqrt(relativeSqr);
                        predictedRelative = relative * (tetherLength * inverseRelativeLength);
                    }
                    else
                    {
                        predictedRelative = body.rotation * Vector3.back * tetherLength;
                    }

                    predictedRelativeSqr = predictedRelative.sqrMagnitude;
                }

                float inversePredictedLength = math.rsqrt(math.max(predictedRelativeSqr, MinVectorMagnitudeSq));
                float predictedLength = predictedRelativeSqr * inversePredictedLength;
                Vector3 radialDirection = predictedRelative * inversePredictedLength;
                Vector3 constrainedRelative = radialDirection * tetherLength;
                Vector3 targetPosition = _entanglementAnchorPosition + constrainedRelative;
                Vector3 constrainedVelocity = (targetPosition - currentPosition) / safeDeltaTime;
                float extensionMeters = math.max(0f, predictedLength - tetherLength);
                float outwardSpeedMetersPerSecond = math.max(0f, Vector3.Dot(candidateVelocity, radialDirection));
                float constraintAcceleration = (extensionMeters / (safeDeltaTime * safeDeltaTime)) +
                                               (outwardSpeedMetersPerSecond / safeDeltaTime);
                float bodyMass = math.max(1f, body.mass);
                _lastEntanglementTensionNewtons = math.max(0f, bodyMass * constraintAcceleration);
                if (!float.IsFinite(_lastEntanglementTensionNewtons))
                    _lastEntanglementTensionNewtons = 0f;

                _linearVelocity = HectonPlayerMotor.SafeVelocity(constrainedVelocity);

                float linearVelocitySqr = _linearVelocity.sqrMagnitude;
                if (linearVelocitySqr > MinVectorMagnitudeSq)
                {
                    Vector3 targetForward = _linearVelocity * math.rsqrt(linearVelocitySqr);
                    Quaternion targetRotation = ResolveLookRotationNoTrig(targetForward, Vector3.up);
                    float facingBlend = ResolveDecayBlend(EntanglementFacingSharpness, safeDeltaTime);
                    body.MoveRotation(ApproximateNlerpNoSqrt(body.rotation, targetRotation, facingBlend));
                }

                Quaternion bodyRotation = body.rotation;
                TryEmitCinematicWakeSiltDecal(bodyRotation, safeDeltaTime);
                WriteSubmarineState(targetPosition, bodyRotation);
            }
        }

        /// <summary>
        /// Integrates thrust and local pitch/yaw steering into a kinematic velocity and rotation target.
        /// </summary>
        public void IntegrateDrive(
            float forwardInput,
            float yawInput,
            float pitchInput,
            float thrustAcceleration,
            float maxSpeed,
            float linearDamping,
            float yawAngularAccelerationDegrees,
            float pitchAngularAccelerationDegrees,
            float angularDamping,
            float fixedDeltaTime)
        {
            Rigidbody body = _body;
            if (body == null || fixedDeltaTime <= 0f)
                return;

            if (_isEntangled)
                return;

            using (_driveProfilerMarker.Auto())
            {
                float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);

                Vector3 localAngularVelocityDegrees = _localAngularVelocityDegrees;
                localAngularVelocityDegrees.x += (-pitchInput * pitchAngularAccelerationDegrees) * safeDeltaTime;
                localAngularVelocityDegrees.y += (yawInput * yawAngularAccelerationDegrees) * safeDeltaTime;
                float angularDampingFactor = math.saturate(angularDamping * safeDeltaTime);
                localAngularVelocityDegrees = (Vector3)math.lerp(
                    new float3(localAngularVelocityDegrees.x, localAngularVelocityDegrees.y, localAngularVelocityDegrees.z),
                    float3.zero,
                    angularDampingFactor);
                _localAngularVelocityDegrees = HectonPlayerMotor.SafeVelocity(localAngularVelocityDegrees);

                Quaternion deltaRotation = ComposeAxisAngleDegrees(_localAngularVelocityDegrees * safeDeltaTime);
                Quaternion targetRotation = body.rotation * deltaRotation;
                targetRotation = ResolveGroundAlignedRotation(targetRotation, safeDeltaTime);
                body.MoveRotation(targetRotation);

                float clampedForwardInput = math.clamp(forwardInput, -1f, 1f);
                Vector3 targetForward = targetRotation * Vector3.forward;
                EvaluateSlopeTraction(targetForward, safeDeltaTime, out float tractionMultiplier, out float downwardAcceleration);
                float effectiveAcceleration =
                    math.max(0f, thrustAcceleration) *
                    math.saturate(cinematicAccelerationScale) *
                    tractionMultiplier *
                    clampedForwardInput;
                Vector3 candidateVelocity = _linearVelocity + (targetForward * effectiveAcceleration * safeDeltaTime);
                if (downwardAcceleration > 0f)
                    candidateVelocity += Vector3.down * (downwardAcceleration * safeDeltaTime);

                float effectiveDragCoefficient = ResolveCinematicVelocityBleedSharpness(linearDamping) *
                                                math.max(1f, cinematicDragScale) *
                                                ResolveBrineViscosityDragMultiplier();
                candidateVelocity = ApplyCinematicVelocityBleed(candidateVelocity, effectiveDragCoefficient, safeDeltaTime);
                candidateVelocity = ApplyKelpPushback(candidateVelocity, safeDeltaTime);

                float safeMaxSpeed = math.max(0.1f, maxSpeed);
                float sqrMagnitude = candidateVelocity.sqrMagnitude;
                float safeMaxSpeedSq = safeMaxSpeed * safeMaxSpeed;
                if (sqrMagnitude > safeMaxSpeedSq)
                {
                    float inverseMagnitude = math.rsqrt(sqrMagnitude);
                    candidateVelocity *= safeMaxSpeed * inverseMagnitude;
                }

                _linearVelocity = HectonPlayerMotor.SafeVelocity(candidateVelocity);
                TryEmitCinematicWakeSiltDecal(targetRotation, safeDeltaTime);
                WriteSubmarineState(body.position + (_linearVelocity * safeDeltaTime), targetRotation);
            }
        }

        /// <summary>Schedules a deferred capsule sweep for the current kinematic velocity.</summary>
        public bool ScheduleCapsuleSweepBatch(int layerMask, float skinWidth, int selfColliderInstanceId, float fixedDeltaTime)
        {
            Rigidbody body = _body;
            CapsuleCollider capsule = _capsule;
            if (body == null ||
                capsule == null ||
                layerMask == 0 ||
                _scheduledSweepPending ||
                _scheduledSweepReady ||
                !float.IsFinite(fixedDeltaTime) ||
                fixedDeltaTime <= 0f)
                return false;

            Vector3 displacement = _linearVelocity * fixedDeltaTime;
            if (!KinematicCcdMath.ShouldSchedule(new float3(_linearVelocity.x, _linearVelocity.y, _linearVelocity.z)))
                return false;

            float displacementSqr = displacement.sqrMagnitude;
            if (!float.IsFinite(displacementSqr) || displacementSqr <= MinVectorMagnitudeSq)
                return false;

            EnsureScheduledSweepState();
            ResolveCapsulePoints(body, capsule, out Vector3 capsulePoint1, out Vector3 capsulePoint2, out float capsuleRadius);
            float inverseDistance = math.rsqrt(displacementSqr);
            float distance = displacementSqr * inverseDistance;
            if (!float.IsFinite(distance) || distance <= 0f)
                return false;

            float safeSkinWidth = float.IsFinite(skinWidth) ? math.max(0f, skinWidth) : 0f;
            Vector3 direction = displacement * inverseDistance;

            _scheduledSweepState = new ScheduledSweepState
            {
                StartPosition = body.position,
                Direction = direction,
                Distance = distance,
                SkinWidth = safeSkinWidth,
                SelfColliderInstanceId = selfColliderInstanceId
            };

            _scheduledSweepCommands[0] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                math.max(0.01f, capsuleRadius),
                direction,
                new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore),
                distance + safeSkinWidth);
            for (int i = 0; i < ScheduledSweepMaxHits; i++)
                _scheduledSweepResults[i] = default;

            using (_scheduleProfilerMarker.Auto())
            {
                _scheduledSweepHandle = CapsulecastCommand.ScheduleBatch(
                    _scheduledSweepCommands,
                    _scheduledSweepResults,
                    ScheduledSweepCommandCount,
                    ScheduledSweepMaxHits,
                    default);
                _scheduledSweepPending = true;
                _scheduledSweepReady = false;
                TryRegisterPostFixedTickable();
            }

            return true;
        }

        /// <summary>Consumes a completed sweep, applying depenetration and slide projection when needed.</summary>
        public bool TryConsumeScheduledCapsuleSweep(out bool wasBlocked, out RaycastHit blockingHit, out Vector3 resolvedPosition)
        {
            wasBlocked = false;
            blockingHit = default;
            Rigidbody body = _body;
            resolvedPosition = body != null ? body.position : Vector3.zero;
            if (!_scheduledSweepReady)
                return false;

            using (_consumeProfilerMarker.Auto())
            {
                _scheduledSweepReady = false;

                float nearestDistance = float.MaxValue;
                int nearestIndex = -1;
                float maxHitDistance = _scheduledSweepState.Distance + _scheduledSweepState.SkinWidth + MinVectorMagnitudeSq;
                for (int i = 0; i < ScheduledSweepMaxHits; i++)
                {
                    RaycastHit hit = _scheduledSweepResults[i];
                    int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                    if (hitColliderInstanceId == 0 || hitColliderInstanceId == _scheduledSweepState.SelfColliderInstanceId)
                        continue;

                    float hitDistance = hit.distance;
                    if (!float.IsFinite(hitDistance) || hitDistance < 0f || hitDistance > maxHitDistance)
                        continue;

                    if (hitDistance < nearestDistance)
                    {
                        nearestDistance = hitDistance;
                        nearestIndex = i;
                    }
                }

                if (nearestIndex < 0)
                {
                    ClearBlockingImpactCache();
                    resolvedPosition = _scheduledSweepState.StartPosition + (_scheduledSweepState.Direction * _scheduledSweepState.Distance);
                    MovePosition(resolvedPosition);
                    _scheduledSweepState = default;
                    return true;
                }

                wasBlocked = true;
                blockingHit = _scheduledSweepResults[nearestIndex];
                Vector3 safeNormal = ResolveSafeNormal(blockingHit.normal, Vector3.up);
                bool lowTierStop = KinematicCcdMath.IsLowTier(GlobalRegistry.ScalabilityTierProfileByte);
                bool cornerHalt = !lowTierStop &&
                                  HasScheduledSweepCornerHit(nearestIndex, safeNormal, blockingHit.distance);
                float safeDistance = KinematicCcdMath.ResolveRollbackDistance(
                    blockingHit.distance,
                    _scheduledSweepState.Distance,
                    _scheduledSweepState.SkinWidth);
                Vector3 resolvedDisplacement = _scheduledSweepState.Direction * safeDistance;
                Vector3 projectedDisplacement = ProjectOnUnitPlane(
                    _scheduledSweepState.Direction * _scheduledSweepState.Distance,
                    safeNormal);
                if (!lowTierStop && !cornerHalt)
                {
                    float remainingDistance = math.max(0f, _scheduledSweepState.Distance - safeDistance);
                    float projectedDisplacementSq = projectedDisplacement.sqrMagnitude;
                    if (math.isfinite(projectedDisplacementSq) &&
                        projectedDisplacementSq > MinVectorMagnitudeSq &&
                        remainingDistance > 0f)
                    {
                        resolvedDisplacement += projectedDisplacement *
                            (remainingDistance * math.rsqrt(projectedDisplacementSq));
                    }
                }

                resolvedPosition = _scheduledSweepState.StartPosition + resolvedDisplacement;
                MovePosition(resolvedPosition);
                CacheGroundContact(safeNormal);
                Vector3 previousVelocity = _linearVelocity;
                Vector3 projectedVelocity = ProjectOnUnitPlane(previousVelocity, safeNormal);
                if (IsSlopeTooSteep(safeNormal))
                    projectedVelocity = ProjectOnUnitPlane(projectedVelocity, Vector3.up);
                if (lowTierStop || cornerHalt)
                    projectedVelocity = Vector3.zero;

                Vector3 rejectedVelocity = previousVelocity - projectedVelocity;
                float rejectedVelocitySq = rejectedVelocity.sqrMagnitude;
                _lastBlockingImpactSpeedMetersPerSecond = math.isfinite(rejectedVelocitySq) && rejectedVelocitySq > MinVectorMagnitudeSq
                    ? rejectedVelocitySq * math.rsqrt(rejectedVelocitySq)
                    : 0f;
                _lastBlockingImpactPoint = blockingHit.point;
                _lastBlockingImpactNormal = safeNormal;
                float lostKineticEnergy = KinematicCcdMath.LostKineticEnergy(
                    body != null ? body.mass : 1f,
                    previousVelocity.sqrMagnitude,
                    projectedVelocity.sqrMagnitude);
                EmitKinematicCcdConsequences(
                    in blockingHit,
                    safeNormal,
                    _lastBlockingImpactSpeedMetersPerSecond,
                    lostKineticEnergy,
                    lowTierStop,
                    cornerHalt);
                _linearVelocity = projectedVelocity;
                _linearVelocity = HectonPlayerMotor.SafeVelocity(_linearVelocity);
                if (body != null)
                    WriteSubmarineState(resolvedPosition, body.rotation);
                _scheduledSweepState = default;
                return true;
            }
        }

        private bool IsSlopeTooSteep(Vector3 hitNormal)
        {
            float safeLimit = math.clamp(_groundSlopeLimitDegrees, 5f, 89f);
            float minUpDot = ApproximateCosDegrees(safeLimit);
            return hitNormal.y > 0.0001f && hitNormal.y < minUpDot;
        }

        private void EvaluateSlopeTraction(Vector3 vehicleForward, float deltaTime, out float tractionMultiplier, out float downwardAcceleration)
        {
            tractionMultiplier = 1f;
            downwardAcceleration = 0f;
            _groundContactTimer = math.max(0f, _groundContactTimer - math.max(0f, deltaTime));
            if (_groundContactTimer <= 0f)
                return;

            float3 normal = new float3(_groundNormal.x, _groundNormal.y, _groundNormal.z);
            if (!math.all(math.isfinite(normal)))
                return;

            float normalSqr = math.lengthsq(normal);
            if (normalSqr <= MinVectorMagnitudeSq)
                return;

            normal *= math.rsqrt(normalSqr);
            float upDot = math.clamp(normal.y, -1f, 1f);
            float startUpDot = ApproximateCosDegrees(TractionLossStartDegrees);
            if (upDot >= startUpDot)
                return;

            float hardLimitDegrees = math.max(TractionLossStartDegrees, _groundSlopeLimitDegrees);
            float hardUpDot = ApproximateCosDegrees(hardLimitDegrees);
            float3 forward = new float3(vehicleForward.x, vehicleForward.y, vehicleForward.z);
            float forwardSqr = math.lengthsq(forward);
            forward = forwardSqr > MinVectorMagnitudeSq
                ? forward * math.rsqrt(forwardSqr)
                : new float3(0f, 0f, 1f);
            float forwardDotNormal = math.abs(math.dot(forward, normal));
            float slope01 = math.saturate((startUpDot - upDot) / math.max(0.0001f, startUpDot - hardUpDot));
            float directionalLoss01 = math.saturate((forwardDotNormal - SlopeDot45Degrees) / (1f - SlopeDot45Degrees));
            float tractionLoss01 = math.saturate(math.max(slope01, directionalLoss01));

            if (upDot <= hardUpDot)
            {
                tractionMultiplier = 0f;
                downwardAcceleration = VehicleGravityAcceleration * (1.5f + tractionLoss01);
                return;
            }

            tractionMultiplier = 1f / (1f + (3f * tractionLoss01));
            downwardAcceleration = VehicleGravityAcceleration * (2f * tractionLoss01 * (1f + tractionLoss01));
        }

        private void CacheGroundContact(Vector3 hitNormal)
        {
            float3 normal = new float3(hitNormal.x, hitNormal.y, hitNormal.z);
            if (!math.all(math.isfinite(normal)) || hitNormal.y <= 0.0001f)
                return;

            _groundNormal = ResolveSafeNormal(hitNormal, Vector3.up);
            _groundContactTimer = GroundContactHoldSeconds;
        }

        private static Vector3 ResolveSafeNormal(Vector3 value, Vector3 fallback)
        {
            float3 vector = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(vector)))
                return fallback;

            float lengthSq = math.lengthsq(vector);
            if (lengthSq <= MinVectorMagnitudeSq)
                return fallback;

            vector *= math.rsqrt(lengthSq);
            return new Vector3(vector.x, vector.y, vector.z);
        }

        private static Vector3 ProjectOnUnitPlane(Vector3 velocity, Vector3 unitNormal)
        {
            float normalVelocity = math.dot(
                new float3(velocity.x, velocity.y, velocity.z),
                new float3(unitNormal.x, unitNormal.y, unitNormal.z));
            return velocity - unitNormal * normalVelocity;
        }

        private Quaternion ResolveGroundAlignedRotation(Quaternion targetRotation, float deltaTime)
        {
            if (_groundContactTimer <= 0f)
                return targetRotation;

            float3 normal = new float3(_groundNormal.x, _groundNormal.y, _groundNormal.z);
            if (!math.all(math.isfinite(normal)))
                return targetRotation;

            Vector3 projectedForward = ProjectOnUnitPlane(targetRotation * Vector3.forward, _groundNormal);
            if (projectedForward.sqrMagnitude <= MinVectorMagnitudeSq)
                projectedForward = ProjectOnUnitPlane(targetRotation * Vector3.up, _groundNormal);

            if (projectedForward.sqrMagnitude <= MinVectorMagnitudeSq)
                return targetRotation;

            float inverseProjectedLength = math.rsqrt(math.max(projectedForward.sqrMagnitude, MinVectorMagnitudeSq));
            Quaternion alignedRotation = ResolveLookRotationNoTrig(projectedForward * inverseProjectedLength, _groundNormal);
            float blend = ResolveDecayBlend(GroundAlignmentSharpness, deltaTime);
            return ApproximateNlerpNoSqrt(targetRotation, alignedRotation, blend);
        }

        private void MovePosition(Vector3 position)
        {
            Rigidbody body = _body;
            if (body == null)
                return;

            float3 position3 = new float3(position.x, position.y, position.z);
            if (!math.all(math.isfinite(position3)))
                return;

            body.MovePosition(position);
            WriteSubmarineState(position, body.rotation);
        }

        private float ResolveCinematicVelocityBleedSharpness(float baseDragCoefficient)
        {
            float safeBaseDrag = math.max(0f, baseDragCoefficient);
            if (safeBaseDrag <= 0f)
                return 0f;

            float submersionWeight = 1f + (math.saturate(_hydrodynamicSubmersionFactor) * 0.65f);
            float depthWeight = 1f + (math.saturate(_hydrodynamicDepthMeters / CinematicDepthReferenceMeters) * 0.35f);
            float legacyShapeWeight = math.max(
                0.1f,
                (math.max(0f, hydrodynamicForwardDragScale) +
                 math.max(0f, hydrodynamicLateralDragScale) +
                 math.max(0f, hydrodynamicVerticalDragScale)) * 0.33333334f);
            return safeBaseDrag * submersionWeight * depthWeight * legacyShapeWeight;
        }

        private Vector3 ApplyKelpPushback(Vector3 velocity, float deltaTime)
        {
            _lastKelpDensity01 = 0f;
            Rigidbody body = _body;
            if (body == null || _hydrodynamicSubmersionFactor <= 0.01f)
                return velocity;

            float speedSq = velocity.sqrMagnitude;
            float minKelpPushbackSpeedSq = KelpPushbackMinSpeedMetersPerSecond * KelpPushbackMinSpeedMetersPerSecond;
            if (speedSq < minKelpPushbackSpeedSq)
                return velocity;

            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager == null)
                return velocity;

            Vector3 samplePosition = body.worldCenterOfMass;
            if (!floraInteractionManager.TryResolveKelpPushback(
                    samplePosition,
                    KelpPushbackProbeRadiusMeters,
                    out float density01,
                    out float bendRadiusMeters))
            {
                return velocity;
            }

            _lastKelpDensity01 = density01;
            float inverseSpeed = math.rsqrt(speedSq);
            float speed = speedSq * inverseSpeed;
            float dragCoefficient = math.min(KelpMaxDragCoefficient, 1f + density01 * KelpDragScale);
            floraInteractionManager.RegisterExternalInteraction(
                samplePosition,
                velocity,
                math.max(bendRadiusMeters, KelpPushbackProbeRadiusMeters + speed * 0.12f));
            return ApplyCinematicVelocityBleed(velocity, dragCoefficient, deltaTime);
        }

        internal void ApplyVoxelProxyGravityDampener(float dampenerStrength01)
        {
            Rigidbody body = _body;
            if (body == null)
                return;

            Vector3 velocity = _linearVelocity.sqrMagnitude > MinVectorMagnitudeSq
                ? _linearVelocity
                : HectonPlayerMotor.SafeVelocity(body.linearVelocity);
            if (velocity.y >= 0f)
                return;

            velocity.y = math.lerp(velocity.y, 0f, math.saturate(dampenerStrength01));
            _linearVelocity = HectonPlayerMotor.SafeVelocity(velocity);
            body.linearVelocity = _linearVelocity;
            WriteSubmarineState(body.position, body.rotation);
        }

        private float ResolveBrineViscosityDragMultiplier()
        {
            if (_body == null ||
                !HectonBrineToxicMudGrid.HasRegisteredCells ||
                !TryResolveSubmarineAup(out AbsoluteUniversePosition submarineAup))
                return 1f;

            return HectonBrineToxicMudGrid.OverlapsAupSubmergedVolume(
                    in submarineAup,
                    _brineViscosityQueryRadiusMeters,
                    _brineViscosityVerticalHalfExtentMeters)
                ? math.max(1f, brineViscosityDragMultiplier)
                : 1f;
        }

        private void CacheBrineViscosityQueryShape()
        {
            if (_capsule == null)
            {
                _brineViscosityQueryRadiusMeters = 0.5f;
                _brineViscosityVerticalHalfExtentMeters = 0.5f;
                return;
            }

            float radius = math.max(0.25f, _capsule.radius);
            float halfHeight = math.max(radius, _capsule.height * 0.5f);
            bool yAxisCapsule = _capsule.direction == 1;
            _brineViscosityQueryRadiusMeters = yAxisCapsule ? radius : halfHeight;
            _brineViscosityVerticalHalfExtentMeters = yAxisCapsule ? halfHeight : radius;
        }

        internal bool TryResolveSubmarineAup(out AbsoluteUniversePosition submarineAup)
        {
            submarineAup = default;
            if (!_submarineState.IsCreated || _submarineState.Length <= 0)
                return false;

            SubmarineState state = _submarineState[0];
            if (!math.all(math.isfinite(state.Aup.Local)))
                return false;

            submarineAup = AbsoluteUniversePosition.FromAlignedBlit(in state.Aup);
            return true;
        }

        private static Vector3 ResolveVelocityDelta(Vector3 force, ForceMode mode, float mass, float fixedDeltaTime)
        {
            Vector3 safeForce = HectonPlayerMotor.SafeVelocity(force);
            switch (mode)
            {
                case ForceMode.Force:
                    return safeForce * (fixedDeltaTime / math.max(mass, 0.0001f));

                case ForceMode.Acceleration:
                    return safeForce * fixedDeltaTime;

                case ForceMode.Impulse:
                    return safeForce / math.max(mass, 0.0001f);

                case ForceMode.VelocityChange:
                    return safeForce;

                default:
                    return Vector3.zero;
            }
        }

        private static Quaternion ComposeAxisAngleDegrees(Vector3 eulerDegrees)
        {
            ApproximateSinCosFullNoTrig(eulerDegrees.x * DegreesToRadians * 0.5f, out float sx, out float cx);
            ApproximateSinCosFullNoTrig(eulerDegrees.y * DegreesToRadians * 0.5f, out float sy, out float cy);
            ApproximateSinCosFullNoTrig(eulerDegrees.z * DegreesToRadians * 0.5f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            return ToQuaternion(NormalizeQuaternionNoSqrt(MulQuaternionNoSqrt(yaw, MulQuaternionNoSqrt(pitch, roll))));
        }

        private Vector3 ApplyCinematicVelocityBleed(Vector3 velocity, float bleedSharpness, float deltaTime)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float speedSq = math.lengthsq(velocity3);
            float denormalSpeedSq = DenormalVelocityFlushThresholdMetersPerSecond * DenormalVelocityFlushThresholdMetersPerSecond;
            if (speedSq < denormalSpeedSq)
                return Vector3.zero;
            if (bleedSharpness <= 0f)
                return velocity;

            float decay = 1f / (1f + (bleedSharpness * math.max(deltaTime, 0f)));
            float3 bledVelocity = velocity3 * math.saturate(decay);
            return HectonPlayerMotor.SafeVelocity(new Vector3(bledVelocity.x, bledVelocity.y, bledVelocity.z), velocity);
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static float ApproximateCosDegrees(float degrees)
        {
            ApproximateSinCosFullNoTrig(degrees * DegreesToRadians, out _, out float cos);
            return cos;
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion fromRotation, Quaternion toRotation, float blend01)
        {
            float4 from = new float4(fromRotation.x, fromRotation.y, fromRotation.z, fromRotation.w);
            float4 to = new float4(toRotation.x, toRotation.y, toRotation.z, toRotation.w);
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, math.saturate(blend01));
            return ToQuaternion(NormalizeQuaternionNoSqrt(blended));
        }

        private static Quaternion ResolveLookRotationNoTrig(Vector3 forward, Vector3 up)
        {
            Vector3 f = ResolveSafeDirection(forward, Vector3.forward);
            Vector3 u = ResolveSafeDirection(up, Vector3.up);
            float upForwardDot = math.abs(f.x * u.x + f.y * u.y + f.z * u.z);
            if (upForwardDot > 0.94f)
                u = math.abs(f.y) < 0.94f ? Vector3.up : Vector3.right;

            Vector3 r = ResolveSafeDirection(CrossVector(u, f), Vector3.right);
            u = ResolveSafeDirection(CrossVector(f, r), Vector3.up);

            float m00 = r.x;
            float m01 = u.x;
            float m02 = f.x;
            float m10 = r.y;
            float m11 = u.y;
            float m12 = f.y;
            float m20 = r.z;
            float m21 = u.z;
            float m22 = f.z;
            float trace = m00 + m11 + m22;

            float4 q;
            if (trace > 0f)
                q = new float4(m21 - m12, m02 - m20, m10 - m01, 1f + trace);
            else if (m00 >= m11 && m00 >= m22)
                q = new float4(1f + m00 - m11 - m22, m01 + m10, m02 + m20, m21 - m12);
            else if (m11 > m22)
                q = new float4(m01 + m10, 1f + m11 - m00 - m22, m12 + m21, m02 - m20);
            else
                q = new float4(m02 + m20, m12 + m21, 1f + m22 - m00 - m11, m10 - m01);

            return ToQuaternion(NormalizeQuaternionNoSqrt(q));
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= MinVectorMagnitudeSq)
                return fallback;

            float3 value3 = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(value3)))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 CrossVector(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x);
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
            float lengthSq = math.max(math.dot(value, value), 0.000001f);
            return value * math.rsqrt(lengthSq);
        }

        private static Quaternion ToQuaternion(float4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private void TryEmitCinematicWakeSiltDecal(Quaternion bodyRotation, float deltaTime)
        {
            _wakeSiltDecalCooldown = math.max(0f, _wakeSiltDecalCooldown - math.max(0f, deltaTime));
            if (_body == null || _hydrodynamicSubmersionFactor <= 0.01f)
                return;

            float speedSq = _linearVelocity.sqrMagnitude;
            float wakeThresholdSq = WakeSiltVisualSpeedThresholdMetersPerSecond * WakeSiltVisualSpeedThresholdMetersPerSecond;
            if (speedSq <= wakeThresholdSq)
                return;
            float inverseSpeed = math.rsqrt(speedSq);
            float speed = speedSq * inverseSpeed;

            Vector3 forward = bodyRotation * Vector3.forward;
            if (forward.sqrMagnitude <= MinVectorMagnitudeSq)
                forward = _linearVelocity.sqrMagnitude > MinVectorMagnitudeSq ? _linearVelocity : Vector3.forward;

            float inverseForwardLength = math.rsqrt(math.max(forward.sqrMagnitude, MinVectorMagnitudeSq));
            Vector3 safeForward = forward * inverseForwardLength;
            Vector3 emitterPosition = _body.worldCenterOfMass - (safeForward * WakeEmitterOffsetMeters);
            Vector3 visualWakeVelocity = -safeForward * (speed * math.saturate(_hydrodynamicSubmersionFactor) * 0.35f);
            WriteHydrodynamicWakeSample(emitterPosition, visualWakeVelocity, safeForward, speed);
            TryEmitWakeSiltDecal(emitterPosition, visualWakeVelocity, speed);
        }

        private void WriteHydrodynamicWakeSample(
            Vector3 emitterPosition,
            Vector3 wakeVelocity,
            Vector3 safeForward,
            float speedMetersPerSecond)
        {
            if (_hydrodynamicWakeResetPending ||
                !_hydrodynamicWakeSamples.IsCreated ||
                _hydrodynamicWakeSamples.Length <= 0 ||
                !_hydrodynamicWakeWriteState.IsCreated)
            {
                return;
            }

            if (_hydrodynamicWakeWritePending)
                return;

            float3 emitter = new float3(emitterPosition.x, emitterPosition.y, emitterPosition.z);
            float3 velocity = new float3(wakeVelocity.x, wakeVelocity.y, wakeVelocity.z);
            float3 forward = new float3(safeForward.x, safeForward.y, safeForward.z);
            if (!math.all(math.isfinite(emitter)) ||
                !math.all(math.isfinite(velocity)) ||
                !math.all(math.isfinite(forward)))
            {
                return;
            }

            if (!math.isfinite(speedMetersPerSecond))
                return;

            float safeSpeed = math.max(0f, speedMetersPerSecond);
            float safeSubmersion = math.saturate(_hydrodynamicSubmersionFactor);
            AbsoluteUniversePosition wakeAup = AbsoluteUniversePosition.FromRuntimePosition(emitterPosition);
            _hydrodynamicWakeWriteHandle = new HydrodynamicWakeWriteJob
            {
                RuntimePosition = emitter,
                Velocity = velocity,
                Forward = forward,
                Aup = wakeAup.ToAlignedBlit(),
                SpeedMetersPerSecond = safeSpeed,
                SubmersionFactor = safeSubmersion,
                Samples = _hydrodynamicWakeSamples,
                WriteState = _hydrodynamicWakeWriteState
            }.Schedule();
            _hydrodynamicWakeWritePending = true;
            TryRegisterLateFrameTickable();
        }

        private void TryEmitWakeSiltDecal(Vector3 emitterPosition, Vector3 wakeVelocity, float speedMetersPerSecond)
        {
            if (_wakeSiltDecalCooldown > 0f)
                return;

            float3 emitter = new float3(emitterPosition.x, emitterPosition.y, emitterPosition.z);
            float3 velocity = new float3(wakeVelocity.x, wakeVelocity.y, wakeVelocity.z);
            if (!math.all(math.isfinite(emitter)) || !math.all(math.isfinite(velocity)))
                return;

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            float intensity01 = math.saturate((speedMetersPerSecond - WakeSiltVisualSpeedThresholdMetersPerSecond) / 18f);
            fluidDecals.RegisterWakeSilt(emitterPosition, wakeVelocity, intensity01);
            _wakeSiltDecalCooldown = WakeSiltDecalCooldownSeconds;
        }

        private void RegisterMotor()
        {
            if (_motorRegistryRegistered || _body == null)
                return;

            for (int i = 0; i < _registeredMotors.Length; i++)
            {
                if (_registeredMotors[i] != null && !ReferenceEquals(_registeredMotors[i], this))
                    continue;

                _registeredMotors[i] = this;
                _motorRegistryRegistered = true;
                return;
            }
        }

        private void UnregisterMotor()
        {
            if (!_motorRegistryRegistered)
                return;

            for (int i = 0; i < _registeredMotors.Length; i++)
            {
                if (!ReferenceEquals(_registeredMotors[i], this))
                    continue;

                _registeredMotors[i] = null;
                break;
            }

            _motorRegistryRegistered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterPostFixedTickable()
        {
            if (_registeredPostFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixedTickable()
        {
            if (!_registeredPostFixedTick)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = false;
        }

        private void BeginSafeTeleportCollisionGuard()
        {
            if (_body == null)
                return;

            if (!_safeTeleportCollisionGuardActive)
            {
                _collisionDetectionModeBeforeSafeTeleportGuard = _body.collisionDetectionMode;
                _safeTeleportCollisionModeCaptured = true;
            }

            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.PublishTransform();
            _safeTeleportCollisionGuardActive = true;
            TryRegisterPostFixedTickable();
        }

        private void CompleteSafeTeleportCollisionGuard()
        {
            if (!_safeTeleportCollisionGuardActive)
            {
                TryUnregisterPostFixedTickable();
                return;
            }

            if (_body != null)
            {
                _body.collisionDetectionMode = _safeTeleportCollisionModeCaptured
                    ? _collisionDetectionModeBeforeSafeTeleportGuard
                    : CollisionDetectionMode.Discrete;
                _body.PublishTransform();
            }

            _safeTeleportCollisionGuardActive = false;
            _safeTeleportCollisionModeCaptured = false;
            TryUnregisterPostFixedTickable();
        }

        private void ApplyHeadlessVisualInterpolation()
        {
            if (headlessVisualRoot == null || !_submarineState.IsCreated || _submarineState.Length <= 0)
                return;

            if (_body != null && ReferenceEquals(headlessVisualRoot, _body.transform))
                return;

            if (ReferenceEquals(headlessVisualRoot, transform))
                return;

            SubmarineState state = _submarineState[0];
            float3 targetPosition = state.RuntimePosition;
            quaternion targetRotation = state.RuntimeRotation;
            if (!math.all(math.isfinite(targetPosition)) || !math.all(math.isfinite(targetRotation.value)))
                return;

            if (_visualTeleportPending)
            {
                headlessVisualRoot.SetPositionAndRotation(
                    new Vector3(targetPosition.x, targetPosition.y, targetPosition.z),
                    new Quaternion(targetRotation.value.x, targetRotation.value.y, targetRotation.value.z, targetRotation.value.w));
                _visualTeleportPending = false;
                return;
            }

            Vector3 currentPositionVector = headlessVisualRoot.position;
            Quaternion currentRotationQuaternion = headlessVisualRoot.rotation;
            float3 currentPosition = new float3(currentPositionVector.x, currentPositionVector.y, currentPositionVector.z);
            quaternion currentRotation = new quaternion(
                currentRotationQuaternion.x,
                currentRotationQuaternion.y,
                currentRotationQuaternion.z,
                currentRotationQuaternion.w);
            if (!math.all(math.isfinite(currentPosition)) || !math.all(math.isfinite(currentRotation.value)))
                return;

            float alpha = math.saturate(SystemDispatcher.CurrentFrameDeltaTime * math.max(0.01f, headlessVisualInterpolationSharpness));
            float3 nextPosition = math.lerp(currentPosition, targetPosition, alpha);
            quaternion nextRotation = math.slerp(currentRotation, targetRotation, alpha);
            headlessVisualRoot.SetPositionAndRotation(
                new Vector3(nextPosition.x, nextPosition.y, nextPosition.z),
                new Quaternion(nextRotation.value.x, nextRotation.value.y, nextRotation.value.z, nextRotation.value.w));
        }

        private void EnsureHydrodynamicWakeBuffer()
        {
            if (!_hydrodynamicWakeSamples.IsCreated)
            {
                // COLD ALLOC: NativeArray<HydrodynamicWakeSample>[64] - fixed submarine wake turbulence ring buffer - owner: VehicleMotor
                _hydrodynamicWakeSamples = new NativeArray<HydrodynamicWakeSample>(
                    HydrodynamicWakeSampleCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _hydrodynamicWakeSamples,
                    NativeMemoryOwner,
                    nameof(_hydrodynamicWakeSamples),
                    NativeMemoryLifetime);
            }

            if (_hydrodynamicWakeWriteState.IsCreated)
                return;

            // COLD ALLOC: NativeArray<int>[2] - wake write index/sequence state owned by Burst job - owner: VehicleMotor
            _hydrodynamicWakeWriteState = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _hydrodynamicWakeWriteState,
                NativeMemoryOwner,
                nameof(_hydrodynamicWakeWriteState),
                NativeMemoryLifetime);
        }

        private void RequestDeferredHydrodynamicWakeReset()
        {
            _hydrodynamicWakeResetPending = true;
            TryRegisterLateFrameTickable();
        }

        private void RequestImmediateHydrodynamicWakeResetWithoutSync()
        {
            _hydrodynamicWakeResetPending = true;
            _hydrodynamicWakeWriteIndex = 0;
            _hydrodynamicWakeSequence = 0;

            if (_hydrodynamicWakeWriteState.IsCreated && !_hydrodynamicWakeWritePending)
            {
                _hydrodynamicWakeWriteState[0] = 0;
                _hydrodynamicWakeWriteState[1] = 0;
            }

            if (_hydrodynamicWakeSamples.IsCreated && !_hydrodynamicWakeWritePending)
            {
                for (int i = 0; i < _hydrodynamicWakeSamples.Length; i++)
                    _hydrodynamicWakeSamples[i] = default;

                _hydrodynamicWakeResetPending = false;
                return;
            }

            TryRegisterLateFrameTickable();
        }

        private bool TryResetHydrodynamicWakeBuffer()
        {
            if (!TryCompleteHydrodynamicWakeWriteJob())
                return false;

            bool shouldClearSamples = _hydrodynamicWakeResetPending ||
                                      _hydrodynamicWakeWriteIndex != 0 ||
                                      _hydrodynamicWakeSequence != 0;
            _hydrodynamicWakeWriteIndex = 0;
            _hydrodynamicWakeSequence = 0;
            if (_hydrodynamicWakeWriteState.IsCreated)
            {
                _hydrodynamicWakeWriteState[0] = 0;
                _hydrodynamicWakeWriteState[1] = 0;
            }

            _hydrodynamicWakeResetPending = false;
            if (!_hydrodynamicWakeSamples.IsCreated)
                return true;

            if (shouldClearSamples)
            {
                for (int i = 0; i < _hydrodynamicWakeSamples.Length; i++)
                    _hydrodynamicWakeSamples[i] = default;
            }

            return true;
        }

        private bool TryCompleteHydrodynamicWakeWriteJob()
        {
            if (!_hydrodynamicWakeWritePending)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _hydrodynamicWakeWriteHandle, forceComplete: false))
                return false;

            _hydrodynamicWakeWritePending = false;
            _hydrodynamicWakeWriteHandle = default;
            if (_hydrodynamicWakeWriteState.IsCreated && _hydrodynamicWakeWriteState.Length >= 2)
            {
                _hydrodynamicWakeWriteIndex = _hydrodynamicWakeWriteState[0] & HydrodynamicWakeSampleMask;
                _hydrodynamicWakeSequence = _hydrodynamicWakeWriteState[1];
            }

            return true;
        }

        private void DisposeHydrodynamicWakeBuffer()
        {
            if (_hydrodynamicWakeWriteState.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_hydrodynamicWakeWriteState);
                if (_hydrodynamicWakeWritePending)
                    _hydrodynamicWakeWriteState.Dispose(_hydrodynamicWakeWriteHandle);
                else
                    _hydrodynamicWakeWriteState.Dispose();
                _hydrodynamicWakeWriteState = default;
            }

            if (_hydrodynamicWakeSamples.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_hydrodynamicWakeSamples);
                if (_hydrodynamicWakeWritePending)
                    _hydrodynamicWakeSamples.Dispose(_hydrodynamicWakeWriteHandle);
                else
                    _hydrodynamicWakeSamples.Dispose();
                _hydrodynamicWakeSamples = default;
            }

            _hydrodynamicWakeWriteHandle = default;
            _hydrodynamicWakeWritePending = false;
            _hydrodynamicWakeResetPending = false;
            _hydrodynamicWakeWriteIndex = 0;
            _hydrodynamicWakeSequence = 0;
        }

        private void EnsureSubmarineState()
        {
            if (_submarineState.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SubmarineState>[1] - authoritative headless submarine kinematic state lane - owner: VehicleMotor
            _submarineState = new NativeArray<SubmarineState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _submarineState,
                NativeMemoryOwner,
                nameof(_submarineState),
                NativeMemoryLifetime);
        }

        private void WriteSubmarineState(Vector3 runtimePosition, Quaternion runtimeRotation)
        {
            EnsureSubmarineState();
            float3 position3 = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float4 rotation4 = new float4(runtimeRotation.x, runtimeRotation.y, runtimeRotation.z, runtimeRotation.w);
            if (!math.all(math.isfinite(position3)) || !math.all(math.isfinite(rotation4)))
                return;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            _submarineState[0] = new SubmarineState
            {
                RuntimePosition = position3,
                RuntimeRotation = new quaternion(runtimeRotation.x, runtimeRotation.y, runtimeRotation.z, runtimeRotation.w),
                LinearVelocity = new float3(_linearVelocity.x, _linearVelocity.y, _linearVelocity.z),
                AngularVelocityRadians = new float3(
                    _localAngularVelocityDegrees.x * DegreesToRadians,
                    _localAngularVelocityDegrees.y * DegreesToRadians,
                    _localAngularVelocityDegrees.z * DegreesToRadians),
                Aup = aup.ToAlignedBlit()
            };
        }

        private void DisposeSubmarineState()
        {
            if (!_submarineState.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_submarineState);
            _submarineState.Dispose();
            _submarineState = default;
        }

        private static void ResolveCapsulePoints(Rigidbody body, CapsuleCollider capsule, out Vector3 point1, out Vector3 point2, out float radius)
        {
            Transform transform = body.transform;
            Vector3 lossyScale = transform.lossyScale;
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 axis = transform.up;
            float maxScale = math.max(math.abs(lossyScale.x), math.abs(lossyScale.z));
            radius = math.max(0.01f, capsule.radius * maxScale);
            float scaledHeight = math.max(capsule.height * math.abs(lossyScale.y), radius * 2f);
            float hemisphereOffset = math.max(0f, (scaledHeight * 0.5f) - radius);
            point1 = center + (axis * hemisphereOffset);
            point2 = center - (axis * hemisphereOffset);
        }

        private static int GetHitColliderInstanceId(in RaycastHit hit)
        {
            return unchecked((int)EntityId.ToULong(hit.colliderEntityId));
        }

        private static byte ResolveHighSpeedImpactTargetMaterialId(in RaycastHit hit, byte fallbackMaterialId)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return fallbackMaterialId;

            if (hitCollider.TryGetComponent(out IPhysicsImpactMaterialProvider directProvider))
                return directProvider.ImpactAudioMaterialId;

            IPhysicsImpactMaterialProvider parentProvider = hitCollider.GetComponentInParent<IPhysicsImpactMaterialProvider>();
            return parentProvider != null ? parentProvider.ImpactAudioMaterialId : fallbackMaterialId;
        }

        private bool HasScheduledSweepCornerHit(int nearestIndex, Vector3 primaryNormal, float nearestDistance)
        {
            float distanceWindow = math.max(_scheduledSweepState.SkinWidth + 0.05f, 0.1f);
            for (int i = 0; i < ScheduledSweepMaxHits; i++)
            {
                if (i == nearestIndex)
                    continue;

                RaycastHit hit = _scheduledSweepResults[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _scheduledSweepState.SelfColliderInstanceId)
                    continue;

                if (!math.isfinite(hit.distance) || hit.distance < 0f)
                    continue;

                if (math.abs(hit.distance - nearestDistance) > distanceWindow)
                    continue;

                if (KinematicCcdMath.IsCornerNormal((float3)primaryNormal, (float3)hit.normal))
                    return true;
            }

            return false;
        }

        private void EmitKinematicCcdConsequences(
            in RaycastHit hit,
            Vector3 safeNormal,
            float blockedSpeed,
            float lostKineticEnergy,
            bool lowTierStop,
            bool cornerHalt)
        {
            if (blockedSpeed <= 0.0001f)
                return;

            Vector3 point = HectonPlayerMotor.SafeVelocity(hit.point, _body != null ? _body.position : Vector3.zero);
            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(point);
            ulong bodyId = _body != null ? EntityId.ToULong(_body.GetEntityId()) : 0UL;
            uint targetHash = unchecked((uint)GetHitColliderInstanceId(in hit));
            byte targetMaterialId = ResolveHighSpeedImpactTargetMaterialId(in hit, HighSpeedImpactSignal.MaterialMetal);
            byte sourceMaterialId = HighSpeedImpactSignal.MaterialMetal;
            byte flags = 0;
            if (cornerHalt)
                flags |= HighSpeedImpactSignal.FlagCornerHalt;
            if (lowTierStop)
                flags |= HighSpeedImpactSignal.FlagLowTierStop;

            HighSpeedImpactSignal highSpeedImpact = default;
            highSpeedImpact.PointAup = pointAup;
            highSpeedImpact.Normal = new float3(safeNormal.x, safeNormal.y, safeNormal.z);
            highSpeedImpact.LostKineticEnergy = lostKineticEnergy;
            highSpeedImpact.ImpactSpeed = blockedSpeed;
            highSpeedImpact.SourceHash = _kinematicCcdSourceHash;
            highSpeedImpact.TargetHash = targetHash;
            highSpeedImpact.Frame = unchecked((uint)Time.frameCount);
            highSpeedImpact.SourceKind = HighSpeedImpactSignal.SourceVehicle;
            highSpeedImpact.Flags = flags;
            highSpeedImpact.PrimaryMaterialId = targetMaterialId;
            highSpeedImpact.SecondaryMaterialId = sourceMaterialId;
            highSpeedImpact.EffectiveMass = _body != null ? math.max(0f, _body.mass) : 0f;
            highSpeedImpact.MaterialHash = HighSpeedImpactSignal.ComposeMaterialHash(targetHash, targetMaterialId, sourceMaterialId);
            GlobalSignals.Publish(in highSpeedImpact);

            ImpactSignal impact = default;
            impact.PointAup = pointAup;
            impact.Velocity = blockedSpeed;
            impact.Intensity = math.saturate(blockedSpeed * 0.06f + lostKineticEnergy * 0.000025f);
            impact.PrimaryBodyId = unchecked((uint)bodyId);
            impact.WeightClass = 2;
            impact.Flags = flags;
            GlobalSignals.Publish(in impact);
            CameraJuiceSignals.PublishImpact(in impact, new float3(safeNormal.x, safeNormal.y, safeNormal.z));

            DebrisSpawnSignal debris = default;
            debris.PositionAup = pointAup;
            debris.SourceEntityId = unchecked((uint)bodyId);
            debris.Intensity01 = impact.Intensity;
            debris.DebrisKind = DebrisSpawnSignal.DebrisKindSparks;
            debris.Flags = (byte)(flags | DebrisSpawnSignal.FlagComputeShard);
            SignalBus<DebrisSpawnSignal>.Push(in debris);

            HapticRequest haptic = default;
            haptic.Intensity01 = math.saturate(lostKineticEnergy * 0.00008f);
            haptic.DurationSeconds = math.lerp(0.05f, 0.2f, haptic.Intensity01);
            haptic.Frequency01 = math.saturate(blockedSpeed * 0.04f);
            haptic.SourceHash = _kinematicCcdSourceHash;
            haptic.Frame = unchecked((uint)Time.frameCount);
            haptic.Channel = HapticRequest.ChannelCollision;
            haptic.Flags = flags;
            GlobalSignals.Publish(in haptic);

            if (lostKineticEnergy >= KinematicCcdMath.MassiveLostKineticEnergyJoules)
            {
                Hecton8.Core.Contracts.Signals.CombatDamageSignal damage = default;
                damage.WorldPoint = new float3(point.x, point.y, point.z);
                damage.Direction = new float3(safeNormal.x, safeNormal.y, safeNormal.z);
                damage.Magnitude = math.min(500f, lostKineticEnergy * 0.005f);
                damage.DamageType = (uint)DamageTypeMask.Impact;
                damage.TargetHash = targetHash;
                damage.SourceHash = _kinematicCcdSourceHash;
                damage.Frame = unchecked((uint)Time.frameCount);
                damage.SourceId = bodyId > ushort.MaxValue ? ushort.MaxValue : (ushort)bodyId;
                damage.TargetId = targetHash > ushort.MaxValue ? ushort.MaxValue : (ushort)targetHash;
                damage.Channel = 0;
                damage.Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag;
                damage.IntegrityDelta = 1;
                GlobalSignals.Publish(in damage);
            }

            GlobalPhysicsStateManager.ReportKinematicCcdIntervention();
        }

        private void EnsureScheduledSweepState()
        {
            if (!_scheduledSweepCommands.IsCreated)
            {
                // COLD ALLOC: NativeArray<CapsulecastCommand>[1] - deferred vehicle sweep command lane - owner: VehicleMotor
                _scheduledSweepCommands = new NativeArray<CapsulecastCommand>(ScheduledSweepCommandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledSweepCommands,
                    NativeMemoryOwner,
                    nameof(_scheduledSweepCommands),
                    NativeMemoryLifetime);
            }

            if (!_scheduledSweepResults.IsCreated)
            {
                // COLD ALLOC: NativeArray<RaycastHit>[8] - deferred vehicle sweep hit lane - owner: VehicleMotor
                _scheduledSweepResults = new NativeArray<RaycastHit>(ScheduledSweepMaxHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledSweepResults,
                    NativeMemoryOwner,
                    nameof(_scheduledSweepResults),
                    NativeMemoryLifetime);
            }
        }

        private void TryFinalizeScheduledSweep()
        {
            if (!_scheduledSweepPending)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledSweepHandle, forceComplete: false))
                return;

            _scheduledSweepPending = false;
            _scheduledSweepHandle = default;
            if (_scheduledSweepDiscardAfterCompletion)
            {
                _scheduledSweepDiscardAfterCompletion = false;
                _scheduledSweepReady = false;
                _scheduledSweepState = default;
                return;
            }

            _scheduledSweepReady = true;
        }

        private void DisposeScheduledSweepState()
        {
            if (_scheduledSweepCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scheduledSweepCommands);
                if (_scheduledSweepPending)
                    _scheduledSweepCommands.Dispose(_scheduledSweepHandle);
                else
                    _scheduledSweepCommands.Dispose();
                _scheduledSweepCommands = default;
            }

            if (_scheduledSweepResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scheduledSweepResults);
                if (_scheduledSweepPending)
                    _scheduledSweepResults.Dispose(_scheduledSweepHandle);
                else
                    _scheduledSweepResults.Dispose();
                _scheduledSweepResults = default;
            }

            _scheduledSweepHandle = default;
            _scheduledSweepPending = false;
            _scheduledSweepReady = false;
            _scheduledSweepDiscardAfterCompletion = false;
        }
    }
}
