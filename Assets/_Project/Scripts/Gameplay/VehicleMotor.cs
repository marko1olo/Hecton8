using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Kinematic vehicle motor with deferred capsule sweep consumption for mountable transports.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Vehicle Motor")]
    public sealed class VehicleMotor : MonoBehaviour, IOriginShiftListener, ILateFrameTickable
    {
        internal struct SubmarineState
        {
            public float3 RuntimePosition;
            public quaternion RuntimeRotation;
            public float3 LinearVelocity;
            public float3 AngularVelocityRadians;
            public AbsoluteUniversePositionBlit128 Aup;
        }

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
        private const float DefaultGroundSlopeLimitDegrees = 45f;
        private const float TractionLossStartDegrees = 45f;
        private const float GroundContactHoldSeconds = 0.2f;
        private const float VehicleGravityAcceleration = 9.81f;
        private const float SlopeDot45Degrees = 0.70710678f;
        private const float GroundAlignmentSharpness = 10f;
        private const float DenormalVelocityFlushThresholdMetersPerSecond = 0.001f;
        private const float CinematicDepthReferenceMeters = 900f;
        private const float WakeSiltVisualSpeedThresholdMetersPerSecond = 15f;
        private const float WakeEmitterOffsetMeters = 4f;
        private const float WakeSiltDecalCooldownSeconds = 0.24f;
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

        [Header("-- Cinematic Hull Feel -------------")]
        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicForwardDragScale = 0.58f;

        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicLateralDragScale = 3.2f;

        [Tooltip("Legacy scalar folded into cinematic velocity bleed. Kept for serialized preset compatibility.")]
        [SerializeField, Min(0f)] private float hydrodynamicVerticalDragScale = 1.7f;

        [Tooltip("Cinematic acceleration scalar replacing presentation velocity history. Lower values make the hull feel heavy without frame buffers.")]
        [SerializeField, Range(0.1f, 1f)] private float cinematicAccelerationScale = 0.72f;

        [Tooltip("Cinematic drag scalar replacing presentation velocity history. Higher values make the hull settle without inertial ghost buffers.")]
        [SerializeField, Range(1f, 4f)] private float cinematicDragScale = 1.35f;

        [Header("-- Headless Presentation -----------")]
        [Tooltip("Optional visual-only submarine root interpolated from the authoritative NativeArray state in the late-frame dispatcher lane.")]
        [SerializeField] private Transform headlessVisualRoot;

        [Tooltip("Interpolation sharpness used when smoothing the visual-only submarine root toward the headless kinematic state.")]
        [SerializeField, Min(0.01f)] private float headlessVisualInterpolationSharpness = 18f;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private NativeArray<SubmarineState> _submarineState;
        private NativeArray<CapsulecastCommand> _scheduledSweepCommands;
        private NativeArray<RaycastHit> _scheduledSweepResults;
        private JobHandle _scheduledSweepHandle;
        private ScheduledSweepState _scheduledSweepState;
        private bool _scheduledSweepPending;
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
        private bool _visualTeleportPending;
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

        /// <summary>Current kinematic linear velocity in world space.</summary>
        public Vector3 LinearVelocity => _linearVelocity;

        /// <summary>Current presentation velocity. Inertial history buffers are intentionally purged.</summary>
        public Vector3 PerceivedLinearVelocity => HectonPlayerMotor.SafeVelocity(_linearVelocity);

        internal NativeArray<SubmarineState> SubmarineStateNative => _submarineState;

        /// <summary>True while a deferred capsule sweep is waiting for consumption.</summary>
        public bool HasPendingSweep => _scheduledSweepPending;

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
            EnsureSubmarineState();
            RegisterMotor();
            TryRegisterOriginShiftListener();
            TryRegisterLateFrameTickable();
            ResetRuntimeState();
        }

        private void OnEnable()
        {
            RegisterMotor();
            TryRegisterOriginShiftListener();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTickable();
            TryUnregisterOriginShiftListener();
            UnregisterMotor();
            DisposeScheduledSweepState();
            DisposeSubmarineState();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrameTickable();
            TryUnregisterOriginShiftListener();
            UnregisterMotor();
            DisposeScheduledSweepState();
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
            _lastBlockingImpactSpeedMetersPerSecond = 0f;
            _lastBlockingImpactPoint = Vector3.zero;
            _lastBlockingImpactNormal = Vector3.up;
            _visualTeleportPending = true;
            WriteSubmarineState(_body != null ? _body.position : Vector3.zero, _body != null ? _body.rotation : Quaternion.identity);
        }

        /// <summary>Legacy compatibility hook. Velocity history is purged, so this is intentionally a no-op.</summary>
        public void ResetHydrodynamicPresentationState()
        {
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
            float speed = velocity.magnitude;
            if (speed * speed <= MinVectorMagnitudeSq)
                return 0f;

            Vector3 direction = velocity / math.max(speed, 0.0001f);
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
            if (relative.sqrMagnitude > MinVectorMagnitudeSq)
            {
                Vector3 radialDirection = relative.normalized;
                _linearVelocity = Vector3.ProjectOnPlane(_linearVelocity, radialDirection);
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
            if (!_isEntangled || _body == null || fixedDeltaTime <= 0f)
                return false;

            Vector3 velocityDelta = ResolveVelocityDelta(force, mode, math.max(_body.mass, 0.0001f), fixedDeltaTime);
            if (velocityDelta.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            Vector3 candidateVelocity = HectonPlayerMotor.SafeVelocity(_body.linearVelocity + velocityDelta, _linearVelocity);
            Vector3 predictedRelative = (_body.worldCenterOfMass + candidateVelocity * fixedDeltaTime) - _entanglementAnchorPosition;
            float tetherLength = math.max(MinEntanglementTetherMeters, _entanglementTetherLength);
            return predictedRelative.sqrMagnitude > tetherLength * tetherLength;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            ApplyOriginShift(shiftData.ShiftOffset);
            if (shiftData.IsSafeTeleport)
                _visualTeleportPending = true;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ApplyHeadlessVisualInterpolation();
        }

        /// <summary>Applies a floating-origin shift to cached kinematic positions owned by the motor.</summary>
        public void ApplyOriginShift(Vector3 shiftOffset)
        {
            if (shiftOffset.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            if (_isEntangled)
                _entanglementAnchorPosition -= shiftOffset;

            if (_scheduledSweepPending)
                _scheduledSweepState.StartPosition -= shiftOffset;

            if (_lastBlockingImpactPoint.sqrMagnitude > MinVectorMagnitudeSq)
                _lastBlockingImpactPoint -= shiftOffset;

            _visualTeleportPending = true;

            if (_body != null)
                WriteSubmarineState(_body.position, _body.rotation);
        }

        /// <summary>Advances tethered current-driven motion while propulsion is locked out by macro-flora entanglement.</summary>
        public void AdvanceEntanglement(Vector3 currentFlowVelocity, float currentAcceleration, float linearDamping, float fixedDeltaTime)
        {
            if (!_isEntangled || _body == null || fixedDeltaTime <= 0f)
                return;

            using (_driveProfilerMarker.Auto())
            {
                float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);
                Vector3 currentPosition = _body.position;
                Vector3 relative = currentPosition - _entanglementAnchorPosition;
                if (relative.sqrMagnitude <= MinVectorMagnitudeSq)
                    relative = _body.rotation * Vector3.back * math.max(MinEntanglementTetherMeters, _entanglementTetherLength);

                float tetherLength = math.max(MinEntanglementTetherMeters, _entanglementTetherLength);
                Vector3 safeFlowVelocity = HectonPlayerMotor.SafeVelocity(currentFlowVelocity);
                Vector3 candidateVelocity = _linearVelocity + (safeFlowVelocity * math.max(0f, currentAcceleration) * safeDeltaTime);
                candidateVelocity = ApplyCinematicVelocityBleed(candidateVelocity, math.max(0f, linearDamping), safeDeltaTime);

                Vector3 predictedRelative = relative + (candidateVelocity * safeDeltaTime);
                if (predictedRelative.sqrMagnitude <= MinVectorMagnitudeSq)
                    predictedRelative = relative.sqrMagnitude > MinVectorMagnitudeSq
                        ? relative.normalized * tetherLength
                        : _body.rotation * Vector3.back * tetherLength;

                float predictedLength = predictedRelative.magnitude;
                Vector3 radialDirection = predictedRelative / math.max(predictedLength, 0.0001f);
                Vector3 constrainedRelative = radialDirection * tetherLength;
                Vector3 targetPosition = _entanglementAnchorPosition + constrainedRelative;
                Vector3 constrainedVelocity = (targetPosition - currentPosition) / safeDeltaTime;
                float extensionMeters = math.max(0f, predictedLength - tetherLength);
                float outwardSpeedMetersPerSecond = math.max(0f, Vector3.Dot(candidateVelocity, radialDirection));
                float constraintAcceleration = (extensionMeters / (safeDeltaTime * safeDeltaTime)) +
                                               (outwardSpeedMetersPerSecond / safeDeltaTime);
                float bodyMass = _body != null ? math.max(1f, _body.mass) : 1f;
                _lastEntanglementTensionNewtons = math.max(0f, bodyMass * constraintAcceleration);
                if (!float.IsFinite(_lastEntanglementTensionNewtons))
                    _lastEntanglementTensionNewtons = 0f;

                _linearVelocity = HectonPlayerMotor.SafeVelocity(constrainedVelocity);

                if (_linearVelocity.sqrMagnitude > MinVectorMagnitudeSq)
                {
                    Vector3 targetForward = _linearVelocity.normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);
                    float facingBlend = 1f - math.exp(-EntanglementFacingSharpness * safeDeltaTime);
                    _body.MoveRotation(Quaternion.Slerp(_body.rotation, targetRotation, facingBlend));
                }

                TryEmitCinematicWakeSiltDecal(_body.rotation, safeDeltaTime);
                WriteSubmarineState(targetPosition, _body.rotation);
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
            if (_body == null || fixedDeltaTime <= 0f)
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
                Quaternion targetRotation = _body.rotation * deltaRotation;
                targetRotation = ResolveGroundAlignedRotation(targetRotation, safeDeltaTime);
                _body.MoveRotation(targetRotation);

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

                float effectiveDragCoefficient = ResolveCinematicVelocityBleedSharpness(linearDamping) * math.max(1f, cinematicDragScale);
                candidateVelocity = ApplyCinematicVelocityBleed(candidateVelocity, effectiveDragCoefficient, safeDeltaTime);
                candidateVelocity = ApplyKelpPushback(candidateVelocity, safeDeltaTime);

                float safeMaxSpeed = math.max(0.1f, maxSpeed);
                float sqrMagnitude = candidateVelocity.sqrMagnitude;
                if (sqrMagnitude > (safeMaxSpeed * safeMaxSpeed))
                    candidateVelocity = candidateVelocity.normalized * safeMaxSpeed;

                _linearVelocity = HectonPlayerMotor.SafeVelocity(candidateVelocity);
                TryEmitCinematicWakeSiltDecal(targetRotation, safeDeltaTime);
                WriteSubmarineState(_body.position + (_linearVelocity * safeDeltaTime), targetRotation);
            }
        }

        /// <summary>Schedules a deferred capsule sweep for the current kinematic velocity.</summary>
        public bool ScheduleCapsuleSweepBatch(int layerMask, float skinWidth, int selfColliderInstanceId, float fixedDeltaTime)
        {
            if (!IsDriveReady || _scheduledSweepPending || fixedDeltaTime <= 0f)
                return false;

            Vector3 displacement = _linearVelocity * fixedDeltaTime;
            if (displacement.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            EnsureScheduledSweepState();
            ResolveCapsulePoints(_body, _capsule, out Vector3 capsulePoint1, out Vector3 capsulePoint2, out float capsuleRadius);
            float distance = displacement.magnitude;
            Vector3 direction = displacement / math.max(distance, 0.0001f);

            _scheduledSweepState = new ScheduledSweepState
            {
                StartPosition = _body.position,
                Direction = direction,
                Distance = distance,
                SkinWidth = skinWidth,
                SelfColliderInstanceId = selfColliderInstanceId
            };

            _scheduledSweepCommands[0] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                math.max(0.01f, capsuleRadius),
                direction,
                new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore),
                distance + skinWidth);

            using (_scheduleProfilerMarker.Auto())
            {
                _scheduledSweepHandle = CapsulecastCommand.ScheduleBatch(
                    _scheduledSweepCommands,
                    _scheduledSweepResults,
                    ScheduledSweepCommandCount,
                    ScheduledSweepMaxHits,
                    default);
                _scheduledSweepPending = true;
            }

            return true;
        }

        /// <summary>Consumes a completed sweep, applying depenetration and slide projection when needed.</summary>
        public bool TryConsumeScheduledCapsuleSweep(out bool wasBlocked, out RaycastHit blockingHit, out Vector3 resolvedPosition)
        {
            wasBlocked = false;
            blockingHit = default;
            resolvedPosition = _body != null ? _body.position : Vector3.zero;
            if (!_scheduledSweepPending)
                return false;

            using (_consumeProfilerMarker.Auto())
            {
                if (!DispatcherJobSwap.TryComplete(ref _scheduledSweepHandle, forceComplete: false))
                    return false;

                _scheduledSweepPending = false;

                float nearestDistance = float.MaxValue;
                int nearestIndex = -1;
                for (int i = 0; i < ScheduledSweepMaxHits; i++)
                {
                    RaycastHit hit = _scheduledSweepResults[i];
                    int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                    if (hitColliderInstanceId == 0 || hitColliderInstanceId == _scheduledSweepState.SelfColliderInstanceId)
                        continue;

                    if (hit.distance < nearestDistance)
                    {
                        nearestDistance = hit.distance;
                        nearestIndex = i;
                    }
                }

                if (nearestIndex < 0)
                {
                    _lastBlockingImpactSpeedMetersPerSecond = 0f;
                    _lastBlockingImpactPoint = Vector3.zero;
                    _lastBlockingImpactNormal = Vector3.up;
                    resolvedPosition = _scheduledSweepState.StartPosition + (_scheduledSweepState.Direction * _scheduledSweepState.Distance);
                    MovePosition(resolvedPosition);
                    return true;
                }

                wasBlocked = true;
                blockingHit = _scheduledSweepResults[nearestIndex];
                float safeDistance = math.max(0f, blockingHit.distance - _scheduledSweepState.SkinWidth);
                resolvedPosition = _scheduledSweepState.StartPosition + (_scheduledSweepState.Direction * safeDistance);
                MovePosition(resolvedPosition);
                CacheGroundContact(blockingHit.normal);
                _lastBlockingImpactSpeedMetersPerSecond = math.max(0f, -Vector3.Dot(_linearVelocity, blockingHit.normal));
                _lastBlockingImpactPoint = blockingHit.point;
                _lastBlockingImpactNormal = blockingHit.normal.sqrMagnitude > MinVectorMagnitudeSq
                    ? blockingHit.normal.normalized
                    : Vector3.up;
                Vector3 projectedVelocity = Vector3.ProjectOnPlane(_linearVelocity, blockingHit.normal);
                if (IsSlopeTooSteep(blockingHit.normal))
                    projectedVelocity = Vector3.ProjectOnPlane(projectedVelocity, Vector3.up);

                _linearVelocity = projectedVelocity;
                _linearVelocity = HectonPlayerMotor.SafeVelocity(_linearVelocity);
                WriteSubmarineState(resolvedPosition, _body.rotation);
                return true;
            }
        }

        private bool IsSlopeTooSteep(Vector3 hitNormal)
        {
            float safeLimit = math.clamp(_groundSlopeLimitDegrees, 5f, 89f);
            float minUpDot = math.cos(math.radians(safeLimit));
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

            float upDot = math.clamp(_groundNormal.normalized.y, -1f, 1f);
            float slopeDegrees = math.degrees(math.acos(upDot));
            if (slopeDegrees <= TractionLossStartDegrees)
                return;

            float hardLimitDegrees = math.max(TractionLossStartDegrees, _groundSlopeLimitDegrees);
            float3 forward = math.normalizesafe(new float3(vehicleForward.x, vehicleForward.y, vehicleForward.z), new float3(0f, 0f, 1f));
            float forwardDotNormal = math.abs(math.dot(forward, math.normalizesafe(normal, new float3(0f, 1f, 0f))));
            float slope01 = math.saturate((slopeDegrees - TractionLossStartDegrees) / math.max(1f, hardLimitDegrees - TractionLossStartDegrees));
            float directionalLoss01 = math.saturate((forwardDotNormal - SlopeDot45Degrees) / (1f - SlopeDot45Degrees));
            float tractionLoss01 = math.saturate(math.max(slope01, directionalLoss01));

            if (slopeDegrees >= hardLimitDegrees)
            {
                tractionMultiplier = 0f;
                downwardAcceleration = VehicleGravityAcceleration * (1.5f + tractionLoss01);
                return;
            }

            tractionMultiplier = math.exp(-3f * tractionLoss01);
            downwardAcceleration = VehicleGravityAcceleration * (math.exp(2f * tractionLoss01) - 1f);
        }

        private void CacheGroundContact(Vector3 hitNormal)
        {
            float3 normal = new float3(hitNormal.x, hitNormal.y, hitNormal.z);
            if (!math.all(math.isfinite(normal)) || hitNormal.y <= 0.0001f)
                return;

            _groundNormal = hitNormal.normalized;
            _groundContactTimer = GroundContactHoldSeconds;
        }

        private Quaternion ResolveGroundAlignedRotation(Quaternion targetRotation, float deltaTime)
        {
            if (_groundContactTimer <= 0f)
                return targetRotation;

            float3 normal = new float3(_groundNormal.x, _groundNormal.y, _groundNormal.z);
            if (!math.all(math.isfinite(normal)))
                return targetRotation;

            Vector3 projectedForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, _groundNormal);
            if (projectedForward.sqrMagnitude <= MinVectorMagnitudeSq)
                projectedForward = Vector3.ProjectOnPlane(targetRotation * Vector3.up, _groundNormal);

            if (projectedForward.sqrMagnitude <= MinVectorMagnitudeSq)
                return targetRotation;

            Quaternion alignedRotation = Quaternion.LookRotation(projectedForward.normalized, _groundNormal);
            float blend = 1f - math.exp(-GroundAlignmentSharpness * math.max(0f, deltaTime));
            return Quaternion.Slerp(targetRotation, alignedRotation, blend);
        }

        private void MovePosition(Vector3 position)
        {
            if (_body == null)
                return;

            float3 position3 = new float3(position.x, position.y, position.z);
            if (!math.all(math.isfinite(position3)))
                return;

            _body.MovePosition(position);
            WriteSubmarineState(position, _body.rotation);
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
            if (_body == null || _hydrodynamicSubmersionFactor <= 0.01f)
                return velocity;

            float speed = velocity.magnitude;
            if (speed < KelpPushbackMinSpeedMetersPerSecond)
                return velocity;

            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager == null)
                return velocity;

            Vector3 samplePosition = _body.worldCenterOfMass;
            if (!floraInteractionManager.TryResolveKelpPushback(
                    samplePosition,
                    KelpPushbackProbeRadiusMeters,
                    out float density01,
                    out float bendRadiusMeters))
            {
                return velocity;
            }

            _lastKelpDensity01 = density01;
            float dragCoefficient = math.min(KelpMaxDragCoefficient, 1f + density01 * KelpDragScale);
            floraInteractionManager.RegisterExternalInteraction(
                samplePosition,
                velocity,
                math.max(bendRadiusMeters, KelpPushbackProbeRadiusMeters + speed * 0.12f));
            return ApplyCinematicVelocityBleed(velocity, dragCoefficient, deltaTime);
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
            quaternion pitch = quaternion.AxisAngle(new float3(1f, 0f, 0f), eulerDegrees.x * Mathf.Deg2Rad);
            quaternion yaw = quaternion.AxisAngle(new float3(0f, 1f, 0f), eulerDegrees.y * Mathf.Deg2Rad);
            quaternion roll = quaternion.AxisAngle(new float3(0f, 0f, 1f), eulerDegrees.z * Mathf.Deg2Rad);
            quaternion composed = math.mul(yaw, math.mul(pitch, roll));
            return new Quaternion(composed.value.x, composed.value.y, composed.value.z, composed.value.w);
        }

        private Vector3 ApplyCinematicVelocityBleed(Vector3 velocity, float bleedSharpness, float deltaTime)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float speed = math.length(velocity3);
            if (speed < DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;
            if (bleedSharpness <= 0f)
                return velocity;

            float bleed = 1f - math.exp(-bleedSharpness * math.max(deltaTime, 0f));
            float3 bledVelocity = math.lerp(velocity3, float3.zero, math.saturate(bleed));
            return HectonPlayerMotor.SafeVelocity(new Vector3(bledVelocity.x, bledVelocity.y, bledVelocity.z), velocity);
        }

        internal static bool TrySampleAnyHydrodynamicWake(Vector3 worldPosition, out Vector3 acceleration)
        {
            acceleration = Vector3.zero;
            return false;
        }

        private void TryEmitCinematicWakeSiltDecal(Quaternion bodyRotation, float deltaTime)
        {
            _wakeSiltDecalCooldown = math.max(0f, _wakeSiltDecalCooldown - math.max(0f, deltaTime));
            if (_body == null || _hydrodynamicSubmersionFactor <= 0.01f)
                return;

            float speed = _linearVelocity.magnitude;
            if (speed <= WakeSiltVisualSpeedThresholdMetersPerSecond)
                return;

            Vector3 forward = bodyRotation * Vector3.forward;
            if (forward.sqrMagnitude <= MinVectorMagnitudeSq)
                forward = _linearVelocity.sqrMagnitude > MinVectorMagnitudeSq ? _linearVelocity.normalized : Vector3.forward;

            Vector3 safeForward = forward.normalized;
            Vector3 emitterPosition = _body.worldCenterOfMass - (safeForward * WakeEmitterOffsetMeters);
            Vector3 visualWakeVelocity = -safeForward * (speed * math.saturate(_hydrodynamicSubmersionFactor) * 0.35f);
            TryEmitWakeSiltDecal(emitterPosition, visualWakeVelocity, speed);
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

            AbsoluteUniversePosition emitAup = AbsoluteUniversePosition.FromRuntimePosition(emitterPosition);
            float3 runtimeFromAup = emitAup.ToRuntimeFloat3();
            Vector3 aupRuntimePosition = new Vector3(runtimeFromAup.x, runtimeFromAup.y, runtimeFromAup.z);
            float intensity01 = math.saturate((speedMetersPerSecond - WakeSiltVisualSpeedThresholdMetersPerSecond) / 18f);
            fluidDecals.RegisterWakeSilt(aupRuntimePosition, wakeVelocity, intensity01);
            _wakeSiltDecalCooldown = WakeSiltDecalCooldownSeconds;
        }

        private void RegisterMotor()
        {
            if (_motorRegistryRegistered)
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
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

        private void EnsureSubmarineState()
        {
            if (_submarineState.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SubmarineState>[1] - authoritative headless submarine kinematic state lane - owner: VehicleMotor
            _submarineState = new NativeArray<SubmarineState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
                AngularVelocityRadians = math.radians(new float3(_localAngularVelocityDegrees.x, _localAngularVelocityDegrees.y, _localAngularVelocityDegrees.z)),
                Aup = aup.ToAlignedBlit()
            };
        }

        private void DisposeSubmarineState()
        {
            if (!_submarineState.IsCreated)
                return;

            _submarineState.Dispose();
            _submarineState = default;
        }

        private static void ResolveCapsulePoints(Rigidbody body, CapsuleCollider capsule, out Vector3 point1, out Vector3 point2, out float radius)
        {
            Transform transform = body.transform;
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 axis = transform.up;
            float maxScale = math.max(math.abs(transform.lossyScale.x), math.abs(transform.lossyScale.z));
            radius = math.max(0.01f, capsule.radius * maxScale);
            float scaledHeight = math.max(capsule.height * math.abs(transform.lossyScale.y), radius * 2f);
            float hemisphereOffset = math.max(0f, (scaledHeight * 0.5f) - radius);
            point1 = center + (axis * hemisphereOffset);
            point2 = center - (axis * hemisphereOffset);
        }

        private static int GetHitColliderInstanceId(in RaycastHit hit)
        {
            return unchecked((int)EntityId.ToULong(hit.colliderEntityId));
        }

        private void EnsureScheduledSweepState()
        {
            if (!_scheduledSweepCommands.IsCreated)
            {
                // COLD ALLOC: NativeArray<CapsulecastCommand>[1] — deferred vehicle sweep command lane — owner: VehicleMotor
                _scheduledSweepCommands = new NativeArray<CapsulecastCommand>(ScheduledSweepCommandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_scheduledSweepResults.IsCreated)
            {
                // COLD ALLOC: NativeArray<RaycastHit>[8] — deferred vehicle sweep hit lane — owner: VehicleMotor
                _scheduledSweepResults = new NativeArray<RaycastHit>(ScheduledSweepMaxHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }
        }

        private void DisposeScheduledSweepState()
        {
            if (_scheduledSweepCommands.IsCreated)
            {
                if (_scheduledSweepPending)
                    _scheduledSweepCommands.Dispose(_scheduledSweepHandle);
                else
                    _scheduledSweepCommands.Dispose();
                _scheduledSweepCommands = default;
            }

            if (_scheduledSweepResults.IsCreated)
            {
                if (_scheduledSweepPending)
                    _scheduledSweepResults.Dispose(_scheduledSweepHandle);
                else
                    _scheduledSweepResults.Dispose();
                _scheduledSweepResults = default;
            }

            _scheduledSweepHandle = default;
            _scheduledSweepPending = false;
        }
    }
}
